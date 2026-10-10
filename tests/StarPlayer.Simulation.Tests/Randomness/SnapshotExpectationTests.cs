using System.Collections.Immutable;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Tests.Randomness;

// A. A snapshot cannot say which streams SHOULD be in it. The caller commits the live state's hash with the game state (Phase 4) and passes it back
// on load; the restored state must hash to exactly that value.
public class SnapshotExpectationTests
{
    private const ulong Seed = 0x0123456789ABCDEF;
    private const string Context = "game:KBO-T:fx-0001";

    private static GameRandom New() => GameRandom.Create(Seed, Context).Value;

    private static uint[] Draw(GameRandom r, RngStreamId id, int n) => Enumerable.Range(0, n).Select(_ => r.Stream(id).NextUInt32()).ToArray();

    // A game that consumed pitch, contact and location.
    private static GameRandom Played()
    {
        var r = New();
        Draw(r, GameRngStreams.Pitch, 5);
        Draw(r, GameRngStreams.Contact, 3);
        Draw(r, GameRngStreams.Location, 2);
        return r;
    }

    private static GameRandomSnapshot Without(GameRandomSnapshot s, RngStreamId id) =>
        s with { Streams = s.Streams.Where(x => x.Stream != id).ToImmutableArray() };

    private static string Code<T>(Result<T> r) => r.Error!.Value.Code;

    // ---- the hazard, kept as a documented limit of the structure-only overload ----

    [Fact]
    public void Limit_without_an_expectation_a_deleted_consumed_stream_restores_and_silently_restarts()
    {
        var live = Played();
        var truncated = Without(live.Snapshot(), GameRngStreams.Location);

        var restored = GameRandom.Restore(truncated); // structurally consistent, so it succeeds
        Assert.True(restored.IsSuccess);

        // The deleted stream is back at its very first value although the game had already drawn twice from it.
        uint firstEver = New().Stream(GameRngStreams.Location).NextUInt32();
        uint liveNext = live.Stream(GameRngStreams.Location).NextUInt32();
        Assert.Equal(firstEver, restored.Value.Stream(GameRngStreams.Location).NextUInt32());
        Assert.NotEqual(liveNext, firstEver);
        Assert.NotEqual(live.Snapshot().StateHash, truncated.StateHash); // a hash recomputed from the stored snapshot alone would call it fine
    }

    // ---- the contract ----

    [Fact]
    public void The_genuine_snapshot_restores_against_the_hash_committed_from_the_live_state()
    {
        var live = Played();
        string committed = live.Snapshot().StateHash; // taken from the live state at commit time

        var restored = GameRandom.Restore(live.Snapshot(), committed);

        Assert.True(restored.IsSuccess);
        Assert.Equal(committed, restored.Value.Snapshot().StateHash);
        Assert.Equal(Draw(live, GameRngStreams.Pitch, 8), Draw(restored.Value, GameRngStreams.Pitch, 8));
    }

    [Fact]
    public void A_removed_consumed_stream_is_rejected_even_though_the_stored_snapshot_is_self_consistent()
    {
        var live = Played();
        string committed = live.Snapshot().StateHash;

        foreach (RngStreamId removed in new[] { GameRngStreams.Pitch, GameRngStreams.Contact, GameRngStreams.Location })
        {
            var truncated = Without(live.Snapshot(), removed);
            Assert.True(GameRandom.Restore(truncated).IsSuccess);                                    // structure alone cannot tell
            Assert.Equal("Rng.state_hash_mismatch", Code(GameRandom.Restore(truncated, committed))); // the committed expectation can
            // ...whereas a hash computed from the truncated snapshot itself would accept the loss: that is why it is never used as the expectation.
            Assert.True(GameRandom.Restore(truncated, truncated.StateHash).IsSuccess);
        }
    }

    [Fact]
    public void Removing_every_stream_is_rejected_too()
    {
        var live = Played();
        var empty = live.Snapshot() with { Streams = ImmutableArray<RngStreamSnapshot>.Empty };
        Assert.Equal("Rng.state_hash_mismatch", Code(GameRandom.Restore(empty, live.Snapshot().StateHash)));
    }

    [Fact]
    public void An_added_or_advanced_stream_is_rejected_against_the_committed_hash()
    {
        var live = Played();
        string committed = live.Snapshot().StateHash;

        // a stream that the live game never used
        var other = New();
        Draw(other, GameRngStreams.Ai, 1);
        var added = live.Snapshot();
        added = added with { Streams = added.Streams.Concat(other.Snapshot().Streams).OrderBy(s => s.Stream.Canonical, StringComparer.Ordinal).ToImmutableArray() };
        Assert.True(GameRandom.Restore(added).IsSuccess);
        Assert.Equal("Rng.state_hash_mismatch", Code(GameRandom.Restore(added, committed)));

        // a consistent snapshot of a later moment of the same game
        Draw(live, GameRngStreams.Pitch, 1);
        Assert.Equal("Rng.state_hash_mismatch", Code(GameRandom.Restore(live.Snapshot(), committed)));
    }

    [Fact]
    public void Structural_errors_win_over_the_hash_comparison()
    {
        var live = Played();
        string committed = live.Snapshot().StateHash;
        var s = live.Snapshot();
        var forged = s with { Streams = s.Streams.SetItem(0, s.Streams[0] with { State = s.Streams[0].State + 1 }) };

        Assert.Equal("Rng.state_mismatch", Code(GameRandom.Restore(forged, committed)));
        Assert.Equal("Rng.algorithm_unsupported", Code(GameRandom.Restore(s with { AlgorithmVersion = "pcg32-xsh-rr-v2" }, committed)));
        Assert.Equal("Rng.snapshot_missing", Code(GameRandom.Restore(null, committed)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("5E8EFC9CF7155E8EFC9CF7155E8EFC9CF7155E8EFC9CF7155E8EFC9CF7155E8E")] // upper case is not the canonical form
    [InlineData("zz8efc9cf7155e8efc9cf7155e8efc9cf7155e8efc9cf7155e8efc9cf7155e8e")]
    [InlineData("5e8efc9cf7155e8efc9cf7155e8efc9cf7155e8efc9cf7155e8efc9cf7155e8e0")] // 65 characters
    public void A_malformed_expectation_is_rejected_before_anything_is_rebuilt(string? bad)
    {
        Assert.Equal("Rng.expected_hash_invalid", Code(GameRandom.Restore(Played().Snapshot(), bad)));
    }

    [Fact]
    public void A_flipped_character_in_the_expected_hash_is_rejected()
    {
        var live = Played();
        string committed = live.Snapshot().StateHash;
        string flipped = (committed[0] == '0' ? "1" : "0") + committed[1..];
        Assert.Equal("Rng.state_hash_mismatch", Code(GameRandom.Restore(live.Snapshot(), flipped)));
    }

    [Fact]
    public void The_independent_python_hash_is_an_accepted_expectation_for_the_python_scenario()
    {
        var v = RngFixture.Golden.GetProperty("set_vector");
        var r = GameRandom.Create(RngFixture.Hex64(v.GetProperty("seed")), v.GetProperty("context").GetString()).Value;
        foreach (var op in v.GetProperty("ops").EnumerateArray())
        {
            string[] parts = op.GetProperty("stream").GetString()!.Split('@');
            var s = r.Stream(RngStreamId.Create(parts[0], int.Parse(parts[1])).Value);
            uint arg = op.GetProperty("arg").GetUInt32();
            if (op.GetProperty("kind").GetString() == "u32")
                for (uint i = 0; i < arg; i++) s.NextUInt32();
            else
                s.NextBounded(arg);
        }
        string pythonHash = v.GetProperty("state_hash").GetString()!; // computed by the Python reference, not by this code

        var restored = GameRandom.Restore(r.Snapshot(), pythonHash);

        Assert.True(restored.IsSuccess);
        Assert.Equal(v.GetProperty("canonical").GetString(), restored.Value.Snapshot().Canonical()); // canonical form and golden vector unchanged
        Assert.Equal("Rng.state_hash_mismatch", Code(GameRandom.Restore(Without(r.Snapshot(), GameRngStreams.Location), pythonHash)));
    }

    // ---- failure changes nothing ----

    [Fact]
    public void A_failed_restore_changes_neither_the_live_state_nor_the_stored_snapshot()
    {
        var live = Played();
        var stored = live.Snapshot();
        string committed = stored.StateHash;
        string canonicalBefore = stored.Canonical();
        var truncated = Without(stored, GameRngStreams.Pitch);

        Assert.False(GameRandom.Restore(truncated, committed).IsSuccess);
        Assert.False(GameRandom.Restore(truncated, "nothex").IsSuccess);

        Assert.Equal(committed, live.Snapshot().StateHash);  // the live instance is untouched
        Assert.Equal(canonicalBefore, stored.Canonical());   // the stored snapshot is untouched
        Assert.Equal(5, live.Stream(GameRngStreams.Pitch).Draws);

        // the genuine snapshot still restores afterwards and continues exactly like the live game
        var again = GameRandom.Restore(stored, committed).Value;
        Assert.Equal(Draw(live, GameRngStreams.Pitch, 4), Draw(again, GameRngStreams.Pitch, 4));
    }

    [Fact]
    public void A_failed_restore_does_not_disturb_a_running_atomic_scope_or_later_scopes()
    {
        var live = Played();
        string before = live.Snapshot().Canonical();

        Assert.False(GameRandom.Restore(live.Snapshot(), new string('0', 64)).IsSuccess);

        Assert.Equal(before, live.Snapshot().Canonical());
        var result = live.Atomically(w => Result<uint[]>.Success(Draw(w, GameRngStreams.Pitch, 2)));
        Assert.True(result.IsSuccess);
        Assert.Equal(7, live.Stream(GameRngStreams.Pitch).Draws);
    }
}
