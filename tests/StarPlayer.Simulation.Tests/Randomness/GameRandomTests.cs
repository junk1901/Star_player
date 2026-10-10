using System.Collections.Immutable;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Tests.Randomness;

public class GameRandomTests
{
    private const ulong Seed = 0x0123456789ABCDEF;
    private const string Context = "game:KBO-T:fx-0001";

    private static GameRandom New(ulong seed = Seed, string context = Context) => GameRandom.Create(seed, context).Value;

    private static RngStreamId Id(string name, int version = 1) => RngStreamId.Create(name, version).Value;

    private static uint[] Draw(GameRandom r, RngStreamId id, int n) => Enumerable.Range(0, n).Select(_ => r.Stream(id).NextUInt32()).ToArray();

    // ---- key derivation: independent Python vectors ----

    [Fact]
    public void Key_derivation_matches_independent_python_vectors()
    {
        foreach (var v in RngFixture.Golden.GetProperty("kd_vectors").EnumerateArray())
        {
            string[] parts = v.GetProperty("stream").GetString()!.Split('@');
            var id = Id(parts[0], int.Parse(parts[1]));
            var (state, seq) = StreamKeyDerivation.Derive(RngFixture.Hex64(v.GetProperty("seed")), v.GetProperty("context").GetString()!, id);
            Assert.Equal(RngFixture.Hex64(v.GetProperty("initstate")), state);
            Assert.Equal(RngFixture.Hex64(v.GetProperty("initseq")), seq);
            var pcg = new Pcg32(state, seq);
            Assert.Equal(v.GetProperty("u32").EnumerateArray().Select(RngFixture.Hex32).ToArray(), Enumerable.Range(0, 6).Select(_ => pcg.NextUInt32()).ToArray());
        }
    }

    [Fact]
    public void Key_derivation_depends_on_every_input()
    {
        var baseKey = StreamKeyDerivation.Derive(Seed, Context, GameRngStreams.Location);
        Assert.NotEqual(baseKey, StreamKeyDerivation.Derive(Seed + 1, Context, GameRngStreams.Location));
        Assert.NotEqual(baseKey, StreamKeyDerivation.Derive(Seed, Context + "x", GameRngStreams.Location));
        Assert.NotEqual(baseKey, StreamKeyDerivation.Derive(Seed, Context, GameRngStreams.Pitch));
        Assert.NotEqual(baseKey, StreamKeyDerivation.Derive(Seed, Context, Id("game/location", 2)));
        Assert.Equal(baseKey, StreamKeyDerivation.Derive(Seed, Context, GameRngStreams.Location));
    }

    // ---- reproducibility and independence ----

    [Fact]
    public void Same_seed_context_and_stream_give_identical_output()
    {
        Assert.Equal(Draw(New(), GameRngStreams.Pitch, 50), Draw(New(), GameRngStreams.Pitch, 50));
    }

    [Fact]
    public void Different_seed_or_context_gives_a_different_sequence()
    {
        uint[] a = Draw(New(), GameRngStreams.Pitch, 20);
        Assert.NotEqual(a, Draw(New(Seed + 1), GameRngStreams.Pitch, 20));
        Assert.NotEqual(a, Draw(New(Seed, "game:KBO-T:fx-0002"), GameRngStreams.Pitch, 20));
    }

    [Fact]
    public void Streams_are_independent_of_each_other_and_of_creation_order()
    {
        RngStreamId[] all = [GameRngStreams.Pitch, GameRngStreams.Contact, GameRngStreams.Field, GameRngStreams.Runner, GameRngStreams.Location, GameRngStreams.Ai];

        // Reference: each stream drawn alone in a fresh GameRandom.
        var alone = all.ToDictionary(id => id, id => Draw(New(), id, 30));

        // Same streams created in reverse order and drawn in an interleaved pattern.
        var shared = New();
        var interleaved = all.ToDictionary(id => id, _ => new List<uint>());
        for (int round = 0; round < 30; round++)
            foreach (RngStreamId id in all.Reverse())
                interleaved[id].Add(shared.Stream(id).NextUInt32());
        foreach (RngStreamId id in all)
            Assert.Equal(alone[id], interleaved[id]);

        // The six streams are pairwise different sequences.
        Assert.Equal(all.Length, alone.Values.Select(v => string.Join(",", v)).Distinct().Count());
    }

    [Fact]
    public void Extra_draws_on_another_stream_do_not_change_an_existing_stream()
    {
        var plain = New();
        uint[] expected = Draw(plain, GameRngStreams.Contact, 20);

        var disturbed = New();
        Draw(disturbed, GameRngStreams.Location, 7);
        var contact = disturbed.Stream(GameRngStreams.Contact);
        var got = new List<uint>();
        for (int i = 0; i < 20; i++)
        {
            got.Add(contact.NextUInt32());
            Draw(disturbed, GameRngStreams.Location, 3); // a Pitch Zone Lite style extra consumer between contact draws
            Draw(disturbed, GameRngStreams.Ai, 1);
        }
        Assert.Equal(expected, got);
    }

    [Fact]
    public void Stream_version_changes_the_sequence_so_a_new_draw_order_cannot_reuse_an_old_one()
    {
        var r = New();
        Assert.NotEqual(Draw(r, Id("game/location", 1), 10), Draw(r, Id("game/location", 2), 10));
    }

    [Fact]
    public void Stream_returns_the_same_object_and_counts_draws()
    {
        var r = New();
        var a = r.Stream(GameRngStreams.Pitch);
        a.NextUInt32();
        a.NextBounded(6);
        Assert.Same(a, r.Stream(GameRngStreams.Pitch));
        Assert.True(a.Draws >= 2);
        Assert.Equal(0, r.Stream(GameRngStreams.Runner).Draws);
    }

    [Fact]
    public void Game_stream_names_are_the_documented_six()
    {
        Assert.Equal(["game/pitch@1", "game/contact@1", "game/field@1", "game/runner@1", "game/location@1", "game/ai@1"],
            new[] { GameRngStreams.Pitch, GameRngStreams.Contact, GameRngStreams.Field, GameRngStreams.Runner, GameRngStreams.Location, GameRngStreams.Ai }.Select(s => s.Canonical));
    }

    // ---- input validation ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Game/pitch")]
    [InlineData("game//pitch")]
    [InlineData("/game")]
    [InlineData("game/")]
    [InlineData("game/pit ch")]
    [InlineData("game/pitch-1")]
    [InlineData("게임/투구")]
    public void Invalid_stream_names_are_rejected(string? name)
    {
        Assert.Equal("Rng.stream_name_invalid", RngStreamId.Create(name, 1).Error!.Value.Code);
    }

    [Fact]
    public void Too_long_stream_name_and_bad_versions_are_rejected()
    {
        Assert.Equal("Rng.stream_name_invalid", RngStreamId.Create(new string('a', 65), 1).Error!.Value.Code);
        Assert.True(RngStreamId.Create(new string('a', 64), 1).IsSuccess);
        Assert.Equal("Rng.stream_version_invalid", RngStreamId.Create("game/pitch", 0).Error!.Value.Code);
        Assert.Equal("Rng.stream_version_invalid", RngStreamId.Create("game/pitch", -3).Error!.Value.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("pipe|char")]
    [InlineData("new\nline")]
    public void Invalid_context_keys_are_rejected(string? context)
    {
        Assert.Equal("Rng.context_invalid", GameRandom.Create(1, context).Error!.Value.Code);
    }

    [Fact]
    public void Default_stream_id_is_refused()
    {
        Assert.Throws<ArgumentException>(() => New().Stream(default));
    }

    // ---- snapshot ----

    [Fact]
    public void Snapshot_matches_the_independent_python_set_vector()
    {
        var v = RngFixture.Golden.GetProperty("set_vector");
        var r = GameRandom.Create(RngFixture.Hex64(v.GetProperty("seed")), v.GetProperty("context").GetString()).Value;
        var ops = v.GetProperty("ops").EnumerateArray().ToArray();
        var produced = v.GetProperty("produced").EnumerateArray().ToArray();
        for (int i = 0; i < ops.Length; i++)
        {
            string[] parts = ops[i].GetProperty("stream").GetString()!.Split('@');
            var s = r.Stream(Id(parts[0], int.Parse(parts[1])));
            uint arg = ops[i].GetProperty("arg").GetUInt32();
            if (ops[i].GetProperty("kind").GetString() == "u32")
                Assert.Equal(produced[i].EnumerateArray().Select(RngFixture.Hex32).ToArray(), Enumerable.Range(0, (int)arg).Select(_ => s.NextUInt32()).ToArray());
            else
                Assert.Equal(produced[i].EnumerateArray().Select(e => e.GetUInt32()).ToArray(), new[] { s.NextBounded(arg) });
        }

        var snapshot = r.Snapshot();
        Assert.Equal(v.GetProperty("canonical").GetString(), snapshot.Canonical());
        Assert.Equal(v.GetProperty("state_hash").GetString(), snapshot.StateHash);

        // After restore each stream continues with exactly the value the reference produced next.
        var restored = GameRandom.Restore(snapshot).Value;
        foreach (var kv in v.GetProperty("next_u32_per_stream").EnumerateObject())
        {
            string[] parts = kv.Name.Split('@');
            Assert.Equal(RngFixture.Hex32(kv.Value), restored.Stream(Id(parts[0], int.Parse(parts[1]))).NextUInt32());
        }
    }

    [Fact]
    public void Snapshot_records_versions_streams_in_ordinal_order_and_draw_counts()
    {
        var r = New();
        Draw(r, GameRngStreams.Runner, 3);
        Draw(r, GameRngStreams.Contact, 1);
        var snap = r.Snapshot();
        Assert.Equal("rngset1", snap.FormatVersion);
        Assert.Equal("pcg32-xsh-rr-v1", snap.AlgorithmVersion);
        Assert.Equal("kd1", snap.KeyDerivationVersion);
        Assert.Equal(Seed, snap.MasterSeed);
        Assert.Equal(Context, snap.ContextKey);
        Assert.Equal(["game/contact@1", "game/runner@1"], snap.Streams.Select(s => s.Stream.Canonical));
        Assert.Equal([1L, 3L], snap.Streams.Select(s => s.Draws));
        Assert.All(snap.Streams, s => Assert.Equal(1UL, s.Increment & 1UL));
    }

    [Fact]
    public void Restore_continues_every_stream_identically_and_a_never_created_stream_still_derives_from_the_seed()
    {
        var original = New();
        Draw(original, GameRngStreams.Pitch, 13);
        original.Stream(GameRngStreams.Contact).NextBounded(0x80000001);
        var restored = GameRandom.Restore(original.Snapshot()).Value;

        Assert.Equal(original.Snapshot().Canonical(), restored.Snapshot().Canonical());
        Assert.Equal(Draw(original, GameRngStreams.Pitch, 25), Draw(restored, GameRngStreams.Pitch, 25));
        Assert.Equal(Draw(original, GameRngStreams.Contact, 25), Draw(restored, GameRngStreams.Contact, 25));
        Assert.Equal(Draw(original, GameRngStreams.Location, 25), Draw(restored, GameRngStreams.Location, 25));
        Assert.Equal(original.Snapshot().StateHash, restored.Snapshot().StateHash);
    }

    [Fact]
    public void Restore_of_an_empty_set_is_valid()
    {
        var restored = GameRandom.Restore(New().Snapshot()).Value;
        Assert.Empty(restored.Snapshot().Streams);
        Assert.Equal(Draw(New(), GameRngStreams.Ai, 5), Draw(restored, GameRngStreams.Ai, 5));
    }

    private static GameRandomSnapshot Valid()
    {
        var r = New();
        Draw(r, GameRngStreams.Pitch, 5);
        Draw(r, GameRngStreams.Location, 2);
        return r.Snapshot();
    }

    private static string Code(GameRandomSnapshot? s) => GameRandom.Restore(s).Error!.Value.Code;

    [Fact]
    public void Restore_rejects_wrong_versions()
    {
        Assert.Equal("Rng.snapshot_format_unsupported", Code(Valid() with { FormatVersion = "rngset2" }));
        Assert.Equal("Rng.algorithm_unsupported", Code(Valid() with { AlgorithmVersion = "pcg32-xsh-rr-v2" }));
        Assert.Equal("Rng.key_derivation_unsupported", Code(Valid() with { KeyDerivationVersion = "kd2" }));
        Assert.Equal("Rng.snapshot_missing", Code(null));
        Assert.Equal("Rng.snapshot_missing", Code(Valid() with { Streams = default }));
    }

    [Fact]
    public void Restore_rejects_corrupt_or_forged_state()
    {
        var s = Valid();
        RngStreamSnapshot first = s.Streams[0];

        Assert.Equal("Rng.state_mismatch", Code(s with { Streams = s.Streams.SetItem(0, first with { State = first.State + 1 }) }));
        Assert.Equal("Rng.state_mismatch", Code(s with { Streams = s.Streams.SetItem(0, first with { Draws = first.Draws + 1 }) }));
        Assert.Equal("Rng.increment_mismatch", Code(s with { Streams = s.Streams.SetItem(0, first with { Increment = first.Increment + 2 }) }));
        Assert.Equal("Rng.increment_even", Code(s with { Streams = s.Streams.SetItem(0, first with { Increment = first.Increment + 1 }) }));
        Assert.Equal("Rng.draws_negative", Code(s with { Streams = s.Streams.SetItem(0, first with { Draws = -1 }) }));
        Assert.Equal("Rng.increment_mismatch", Code(s with { MasterSeed = s.MasterSeed + 1 }));
    }

    [Fact]
    public void Restore_rejects_bad_streams_and_contexts()
    {
        var s = Valid();
        Assert.Equal("Rng.stream_duplicate", Code(s with { Streams = s.Streams.Add(s.Streams[0]) }));
        Assert.Equal("Rng.stream_name_invalid", Code(s with { Streams = s.Streams.SetItem(0, s.Streams[0] with { Stream = default }) }));
        Assert.Equal("Rng.context_invalid", Code(s with { ContextKey = "bad context" }));
    }

    [Fact]
    public void A_snapshot_with_a_different_master_seed_is_rejected_not_silently_reseeded()
    {
        var s = Valid();
        Assert.False(GameRandom.Restore(s with { MasterSeed = s.MasterSeed ^ 1 }).IsSuccess);
        Assert.False(GameRandom.Restore(s with { ContextKey = "game:KBO-T:fx-0009" }).IsSuccess);
    }

    // ---- atomic work scope ----

    [Fact]
    public void Successful_work_is_published_and_equals_drawing_directly()
    {
        var atomic = New();
        var direct = New();
        Draw(atomic, GameRngStreams.Pitch, 4);
        Draw(direct, GameRngStreams.Pitch, 4);

        var result = atomic.Atomically(work =>
        {
            Draw(work, GameRngStreams.Pitch, 6);
            Draw(work, GameRngStreams.Location, 3); // a stream first created inside the scope
            return Result<int>.Success(7);
        });
        Draw(direct, GameRngStreams.Pitch, 6);
        Draw(direct, GameRngStreams.Location, 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
        Assert.Equal(direct.Snapshot().Canonical(), atomic.Snapshot().Canonical());
    }

    [Fact]
    public void Failed_work_leaves_the_original_untouched()
    {
        var r = New();
        Draw(r, GameRngStreams.Pitch, 4);
        string before = r.Snapshot().Canonical();

        var result = r.Atomically<int>(work =>
        {
            Draw(work, GameRngStreams.Pitch, 10);
            Draw(work, GameRngStreams.Runner, 10);
            return Result<int>.Failure(new Error("Test.failed", "실패"));
        });

        Assert.Equal("Test.failed", result.Error!.Value.Code);
        Assert.Equal(before, r.Snapshot().Canonical());
    }

    [Fact]
    public void Exception_in_work_leaves_the_original_untouched_and_propagates()
    {
        var r = New();
        Draw(r, GameRngStreams.Pitch, 4);
        string before = r.Snapshot().Canonical();

        Assert.Throws<InvalidOperationException>(() => r.Atomically<int>(work =>
        {
            Draw(work, GameRngStreams.Pitch, 10);
            throw new InvalidOperationException("boom");
        }));

        Assert.Equal(before, r.Snapshot().Canonical());
    }

    [Fact]
    public void Cancellation_before_or_after_the_work_leaves_the_original_untouched()
    {
        var r = New();
        Draw(r, GameRngStreams.Pitch, 4);
        string before = r.Snapshot().Canonical();

        using var already = new CancellationTokenSource();
        already.Cancel();
        bool ran = false;
        Assert.Throws<OperationCanceledException>(() => r.Atomically(work => { ran = true; return Result<int>.Success(1); }, already.Token));
        Assert.False(ran);

        using var during = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => r.Atomically(work =>
        {
            Draw(work, GameRngStreams.Pitch, 10);
            during.Cancel(); // cancelled after drawing, before the commit
            return Result<int>.Success(1);
        }, during.Token));

        Assert.Equal(before, r.Snapshot().Canonical());
    }

    [Fact]
    public void Stream_objects_obtained_before_a_commit_see_the_committed_position()
    {
        var r = New();
        var pitch = r.Stream(GameRngStreams.Pitch);
        r.Atomically(work => { Draw(work, GameRngStreams.Pitch, 5); return Result<int>.Success(0); });
        Assert.Equal(5, pitch.Draws);
        Assert.Same(pitch, r.Stream(GameRngStreams.Pitch));
        Assert.Equal(Draw(New(), GameRngStreams.Pitch, 8)[5..], Draw(r, GameRngStreams.Pitch, 3));
    }

    [Fact]
    public void A_failed_scope_can_be_retried_and_gives_the_same_result()
    {
        var r = New();
        Func<GameRandom, Result<uint>> work = w => Result<uint>.Success(w.Stream(GameRngStreams.Contact).NextBounded(1000));
        r.Atomically<uint>(w => { work(w); return Result<uint>.Failure(new Error("Test.failed", "x")); });
        var retry = r.Atomically(work);
        Assert.Equal(New().Stream(GameRngStreams.Contact).NextBounded(1000), retry.Value);
    }
}
