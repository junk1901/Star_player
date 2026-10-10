using System.Collections.Immutable;
using System.Globalization;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Tests.Randomness;

// Phase 3-A final fix round (TASK.md): B (stream access must not change the hash), C (Atomically misuse), D (Draws limit), E (small items).
// Each test is written against the behaviour the fix must give; the RED run on the unfixed code is kept in docs/reports/PHASE_3A_RED_EVIDENCE.md.
public class RngHardeningTests
{
    private const ulong Seed = 0x0123456789ABCDEF;
    private const string Context = "game:KBO-T:fx-0001";

    private static GameRandom New() => GameRandom.Create(Seed, Context).Value;

    private static uint[] Draw(GameRandom r, RngStreamId id, int n) => Enumerable.Range(0, n).Select(_ => r.Stream(id).NextUInt32()).ToArray();

    private static string Code(GameRandomSnapshot s) => GameRandom.Restore(s).Error!.Value.Code;

    // ---- B. M1: looking at a stream is not using it ----

    [Fact]
    public void Touching_a_stream_without_drawing_leaves_the_state_hash_equal_to_never_touching_it()
    {
        var untouched = New();
        var touched = New();
        _ = touched.Stream(GameRngStreams.Location).Draws; // a read-only looking peek at game/location

        Assert.Equal(untouched.Snapshot().StateHash, touched.Snapshot().StateHash);
        Assert.Equal(untouched.Snapshot().Canonical(), touched.Snapshot().Canonical());
        Assert.Empty(touched.Snapshot().Streams);
    }

    [Fact]
    public void Touching_other_streams_does_not_change_the_hash_of_a_game_that_has_drawn()
    {
        var a = New();
        var b = New();
        Draw(a, GameRngStreams.Pitch, 4);
        Draw(b, GameRngStreams.Pitch, 4);
        foreach (RngStreamId id in new[] { GameRngStreams.Location, GameRngStreams.Contact, GameRngStreams.Ai })
            _ = b.Stream(id).Draws;

        Assert.Equal(a.Snapshot().StateHash, b.Snapshot().StateHash);
        Assert.Equal(["game/pitch@1"], b.Snapshot().Streams.Select(s => s.Stream.Canonical));
    }

    [Fact]
    public void A_stream_enters_the_snapshot_with_its_exact_state_after_its_first_draw()
    {
        var r = New();
        _ = r.Stream(GameRngStreams.Location).Draws;
        Assert.Empty(r.Snapshot().Streams);

        uint first = r.Stream(GameRngStreams.Location).NextUInt32();
        var entry = Assert.Single(r.Snapshot().Streams);
        Assert.Equal(1, entry.Draws);

        var restored = GameRandom.Restore(r.Snapshot()).Value;
        Assert.Equal(Draw(r, GameRngStreams.Location, 10), Draw(restored, GameRngStreams.Location, 10));
        Assert.Equal(first, New().Stream(GameRngStreams.Location).NextUInt32());
    }

    [Fact]
    public void Touching_a_stream_inside_an_atomic_scope_does_not_change_the_hash_either()
    {
        var r = New();
        Draw(r, GameRngStreams.Pitch, 2);
        string before = r.Snapshot().StateHash;
        r.Atomically(work => { _ = work.Stream(GameRngStreams.Location).Draws; return Result<int>.Success(0); });
        Assert.Equal(before, r.Snapshot().StateHash);
    }

    // ---- C. Atomically: nesting, direct access, pre-acquired references, escaped streams ----

    [Fact]
    public void Nested_scope_on_the_same_instance_is_rejected_and_nothing_is_published()
    {
        var r = New();
        Draw(r, GameRngStreams.Pitch, 3);
        string before = r.Snapshot().Canonical();

        Assert.Throws<InvalidOperationException>(() => r.Atomically(outer =>
        {
            Draw(outer, GameRngStreams.Pitch, 5);
            return r.Atomically(inner => Result<int>.Success(1)); // nested on the original
        }));

        Assert.Equal(before, r.Snapshot().Canonical());
    }

    [Fact]
    public void A_scope_started_on_the_working_copy_is_rejected_too()
    {
        var r = New();
        Assert.Throws<InvalidOperationException>(() => r.Atomically(copy => copy.Atomically(x => Result<int>.Success(1))));
        Assert.Empty(r.Snapshot().Streams);
    }

    [Fact]
    public void Drawing_from_the_original_while_a_scope_runs_is_blocked_before_any_state_change()
    {
        var r = New();
        var pitch = r.Stream(GameRngStreams.Pitch); // reference acquired before the scope
        pitch.NextUInt32();
        string before = r.Snapshot().Canonical();

        r.Atomically(copy =>
        {
            Assert.Throws<InvalidOperationException>(() => pitch.NextUInt32());                 // pre-acquired original stream
            Assert.Throws<InvalidOperationException>(() => pitch.NextBounded(6));
            Assert.Throws<InvalidOperationException>(() => r.Stream(GameRngStreams.Contact));   // original lookup
            Assert.Equal(1, pitch.Draws);                                                       // reading is allowed and unchanged
            Assert.Equal(before, r.Snapshot().Canonical());                                     // the original shows its last committed state
            return Result<int>.Failure(new Error("Test.failed", "x"));
        });

        Assert.Equal(before, r.Snapshot().Canonical());
        Assert.Equal(1, pitch.Draws);
    }

    [Fact]
    public void Work_that_swallows_the_misuse_error_still_publishes_only_the_copy()
    {
        var r = New();
        Draw(r, GameRngStreams.Pitch, 2);
        var pitch = r.Stream(GameRngStreams.Pitch);

        r.Atomically(copy =>
        {
            try { pitch.NextUInt32(); } catch (InvalidOperationException) { /* the work hides the misuse */ }
            copy.Stream(GameRngStreams.Pitch).NextUInt32();
            return Result<int>.Success(0);
        });

        Assert.Equal(3, pitch.Draws); // 2 before + exactly 1 from the copy; the blocked draw consumed nothing
        Assert.Equal(Draw(New(), GameRngStreams.Pitch, 6)[3..], Draw(r, GameRngStreams.Pitch, 3));
    }

    [Fact]
    public void Working_copy_and_original_share_no_mutable_object()
    {
        var r = New();
        var pre = r.Stream(GameRngStreams.Pitch);
        pre.NextUInt32();

        r.Atomically(copy =>
        {
            var inside = copy.Stream(GameRngStreams.Pitch);
            Assert.NotSame(pre, inside);
            Assert.Equal(1, inside.Draws);
            inside.NextUInt32();
            inside.NextUInt32();
            Assert.Equal(3, inside.Draws);
            Assert.Equal(1, pre.Draws); // the original did not move while the copy did
            return Result<int>.Failure(new Error("Test.failed", "x"));
        });

        Assert.Equal(1, pre.Draws);
    }

    [Fact]
    public void A_stream_that_escapes_the_scope_cannot_draw_afterwards()
    {
        var r = New();
        Draw(r, GameRngStreams.Pitch, 1);
        RngStream? escaped = null;
        GameRandom? escapedCopy = null;

        r.Atomically(copy => { escaped = copy.Stream(GameRngStreams.Pitch); escapedCopy = copy; return Result<int>.Success(0); });
        string after = r.Snapshot().Canonical();

        Assert.Throws<InvalidOperationException>(() => escaped!.NextUInt32());
        Assert.Throws<InvalidOperationException>(() => escapedCopy!.Stream(GameRngStreams.Contact));
        Assert.Throws<InvalidOperationException>(() => escapedCopy!.Atomically(x => Result<int>.Success(0)));
        Assert.Equal(after, r.Snapshot().Canonical());
    }

    [Fact]
    public void After_a_rejected_nested_call_an_ordinary_scope_runs_again_and_equals_direct_drawing()
    {
        var r = New();
        Assert.Throws<InvalidOperationException>(() => r.Atomically(outer => r.Atomically(inner => Result<int>.Success(1))));

        var result = r.Atomically(work => Result<uint[]>.Success(Draw(work, GameRngStreams.Pitch, 5)));
        Assert.Equal(Draw(New(), GameRngStreams.Pitch, 5), result.Value);
        Assert.Equal(5, r.Stream(GameRngStreams.Pitch).Draws);
    }

    [Fact]
    public void The_scope_lock_is_released_after_a_failure_an_exception_and_a_cancellation()
    {
        var r = New();
        r.Atomically<int>(w => Result<int>.Failure(new Error("Test.failed", "x")));
        Assert.True(r.Atomically(w => Result<int>.Success(1)).IsSuccess);

        Assert.Throws<InvalidOperationException>(() => r.Atomically<int>(w => throw new InvalidOperationException("boom")));
        Assert.True(r.Atomically(w => Result<int>.Success(2)).IsSuccess);

        using var cts = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => r.Atomically(w => { cts.Cancel(); return Result<int>.Success(3); }, cts.Token));
        Draw(r, GameRngStreams.Pitch, 1); // the original is writable again
        Assert.True(r.Atomically(w => Result<int>.Success(4)).IsSuccess);
    }

    // ---- D. Draws limit ----

    private static Pcg32 At(Pcg32 p, long draws) => Pcg32.FromState(p.State, p.Increment, draws).Value;

    [Fact]
    public void NextUInt32_at_the_draw_limit_throws_before_changing_anything()
    {
        var p = At(new Pcg32(7, 9), long.MaxValue);
        ulong state = p.State;

        Assert.Throws<InvalidOperationException>(() => p.NextUInt32());
        Assert.Equal(state, p.State);
        Assert.Equal(long.MaxValue, p.Draws);
    }

    [Fact]
    public void The_last_allowed_draw_succeeds_and_equals_the_unlimited_output()
    {
        var reference = new Pcg32(7, 9);
        var p = At(new Pcg32(7, 9), long.MaxValue - 1);
        Assert.Equal(reference.NextUInt32(), p.NextUInt32());
        Assert.Equal(long.MaxValue, p.Draws);
        Assert.Throws<InvalidOperationException>(() => p.NextUInt32());
    }

    [Fact]
    public void Advance_validates_the_input_and_the_remaining_limit_before_changing_anything()
    {
        var p = At(new Pcg32(7, 9), long.MaxValue - 5);
        ulong state = p.State;

        Assert.Throws<ArgumentOutOfRangeException>(() => p.Advance((ulong)long.MaxValue + 1)); // not a possible count at all
        Assert.Throws<InvalidOperationException>(() => p.Advance(6));                          // 1 more than remains
        Assert.Throws<ArgumentOutOfRangeException>(() => p.Advance(ulong.MaxValue));
        Assert.Throws<InvalidOperationException>(() => p.Advance((ulong)long.MaxValue));      // a valid count, far above what remains
        Assert.Equal(state, p.State);
        Assert.Equal(long.MaxValue - 5, p.Draws);

        p.Advance(5); // exactly the remainder is allowed
        Assert.Equal(long.MaxValue, p.Draws);
        Assert.Throws<InvalidOperationException>(() => p.NextUInt32());
    }

    // For bound 0xC0000001 the threshold is 0x3FFFFFFF: an output below it is rejected, so the call needs a second draw.
    private const uint RejectingBound = 0xC0000001;

    private static Pcg32 FindGenerator(bool firstOutputRejected)
    {
        for (ulong seed = 1; seed < 10_000; seed++)
        {
            uint first = new Pcg32(seed, 1).NextUInt32();
            if ((first < 0x3FFFFFFF) == firstOutputRejected)
                return new Pcg32(seed, 1);
        }
        throw new InvalidOperationException("no suitable seed");
    }

    [Fact]
    public void NextBounded_that_needs_a_draw_beyond_the_limit_leaves_the_generator_exactly_as_it_was()
    {
        var p = At(FindGenerator(firstOutputRejected: true), long.MaxValue - 1);
        ulong state = p.State;

        Assert.Throws<InvalidOperationException>(() => p.NextBounded(RejectingBound)); // first draw rejected, second would exceed the limit
        Assert.Equal(state, p.State);
        Assert.Equal(long.MaxValue - 1, p.Draws);
    }

    [Fact]
    public void NextBounded_that_fits_the_limit_exactly_still_succeeds()
    {
        var reference = FindGenerator(firstOutputRejected: false);
        var p = At(FindGenerator(firstOutputRejected: false), long.MaxValue - 1);
        Assert.Equal(reference.NextBounded(RejectingBound), p.NextBounded(RejectingBound));
        Assert.Equal(long.MaxValue, p.Draws);
    }

    [Fact]
    public void A_stream_restored_at_the_draw_limit_refuses_further_draws_and_keeps_its_snapshot()
    {
        var r = New();
        var (initState, initSeq) = StreamKeyDerivation.Derive(Seed, Context, GameRngStreams.Pitch);
        var limit = new Pcg32(initState, initSeq);
        limit.Advance((ulong)long.MaxValue);
        var snapshot = new GameRandomSnapshot(
            RngVersions.SnapshotFormat, RngVersions.Algorithm, RngVersions.KeyDerivation, Seed, Context,
            [new RngStreamSnapshot(GameRngStreams.Pitch, limit.State, limit.Increment, long.MaxValue)]);

        var restored = GameRandom.Restore(snapshot).Value;
        Assert.Throws<InvalidOperationException>(() => restored.Stream(GameRngStreams.Pitch).NextUInt32());
        Assert.Equal(snapshot.Canonical(), restored.Snapshot().Canonical());
        Assert.Empty(r.Snapshot().Streams);
    }

    // ---- E. small items ----

    [Fact]
    public void A_snapshot_whose_streams_are_not_in_canonical_order_is_rejected_not_silently_reordered()
    {
        var r = New();
        Draw(r, GameRngStreams.Pitch, 1);
        Draw(r, GameRngStreams.Contact, 1);
        var s = r.Snapshot();
        Assert.Equal(["game/contact@1", "game/pitch@1"], s.Streams.Select(x => x.Stream.Canonical));

        Assert.Equal("Rng.stream_order_noncanonical", Code(s with { Streams = s.Streams.Reverse().ToImmutableArray() }));
        Assert.True(GameRandom.Restore(s).IsSuccess); // the canonical order restores
    }

    [Fact]
    public void A_snapshot_entry_for_a_stream_without_draws_is_rejected_as_non_canonical()
    {
        var r = New();
        Draw(r, GameRngStreams.Pitch, 1);
        var s = r.Snapshot();
        var (initState, initSeq) = StreamKeyDerivation.Derive(Seed, Context, GameRngStreams.Ai);
        var fresh = new Pcg32(initState, initSeq);
        var withUnused = s with { Streams = [new RngStreamSnapshot(GameRngStreams.Ai, fresh.State, fresh.Increment, 0), s.Streams[0]] };

        Assert.Equal("Rng.stream_unused", Code(withUnused));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("fa-IR")]
    [InlineData("th-TH")]
    [InlineData("tr-TR")]
    public void Canonical_texts_and_ids_do_not_depend_on_the_current_culture(string culture)
    {
        var r = New();
        Draw(r, GameRngStreams.Location, 12);
        Draw(r, GameRngStreams.Pitch, 3);
        string snapshot = r.Snapshot().Canonical();
        string stream = GameRngStreams.Location.Canonical;
        string eventId = GameEventIds.Create("game:A:1").Value.Next().Value;
        int parsed = GameEventIds.Create("game:A:1").Value.ParseSequence("game:A:1:e000042").Value;

        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try
        {
            var other = New();
            Draw(other, GameRngStreams.Location, 12);
            Draw(other, GameRngStreams.Pitch, 3);
            Assert.Equal(snapshot, other.Snapshot().Canonical());
            Assert.Equal(stream, GameRngStreams.Location.Canonical);
            Assert.Equal(eventId, GameEventIds.Create("game:A:1").Value.Next().Value);
            Assert.Equal(parsed, GameEventIds.Create("game:A:1").Value.ParseSequence("game:A:1:e000042").Value);
            Assert.Equal("game/location@12", RngStreamId.Create("game/location", 12).Value.Canonical);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
