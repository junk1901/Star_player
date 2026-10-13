using System.Text.RegularExpressions;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Tests.Randomness;
using static StarPlayer.Simulation.Tests.Authority.Auth;

namespace StarPlayer.Simulation.Tests.Authority;

/// <summary>Phase 3-C3-A, game-engine 27.7 and 27.16 O-3/O-5: Publish by compare-and-swap, Revision, races and cancellation. Races are held at a deterministic point with a Barrier, never with Sleep.</summary>
public class GameCursorTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

    private static string PublishCode(GameCursor cursor, StagedCommit? staged) => Code(cursor.Publish(staged));

    // ---------------------------------------------------------------- plain publication

    [Fact]
    public void A_publish_makes_the_staged_aggregate_current_and_returns_the_very_record_that_the_aggregate_carries()
    {
        var cursor = Cursor();
        var before = cursor.Current;
        var staged = Ok(CommitGate.Stage(before, Approach(1)));

        var record = Ok(cursor.Publish(staged));

        Assert.Same(staged.Next, cursor.Current);
        Assert.Same(staged.Record, record);
        Assert.Same(record, cursor.Current.LastCommit);
        Assert.Equal((1L, 1), (cursor.Current.Revision, cursor.Current.LastEventSequence));
        Assert.Empty(before.State.AppliedEvents);   // the old aggregate value is untouched
    }

    [Fact]
    public void A_staged_commit_alone_changes_nothing_until_it_is_published()
    {
        var cursor = Cursor();
        var before = cursor.Current;

        _ = Ok(CommitGate.Stage(before, Approach(1)));

        Assert.Same(before, cursor.Current);
    }

    [Fact]
    public void Successive_publishes_raise_the_revision_by_exactly_one_each_and_the_chain_of_records_is_continuous()
    {
        var cursor = Cursor(new CanonScript().Triple().ToAuthority());   // the away batter stands on third, last sequence 2
        var run = new SubstitutionEvent(Id(3), SubstitutionKind.PinchRun, TeamSide.Away, Script.P("ab1"), 3);
        var records = new[] { Commit(cursor, run), Commit(cursor, new RunnerPlayEvent(Id(4), RunnerPlayKind.Balk, [Script.M(3, 4, MoveCause.Balk)])), Commit(cursor, PinchHit(5, "ab2")) };

        Assert.Equal([(0L, 1L, 3), (1L, 2L, 4), (2L, 3L, 5)], records.Select(r => (r.RevisionBefore, r.RevisionAfter, r.EventSequence)).ToArray());
        Assert.Equal((3L, 5, 5), (cursor.Current.Revision, cursor.Current.LastEventSequence, cursor.Current.State.AppliedEvents.Count));
        Assert.Same(records[2], cursor.Current.LastCommit);
    }

    [Fact]
    public void Restored_revision_and_sequence_stay_independent_through_publishes()
    {
        var start = Ok(AuthoritativeGame.Restore(New().State, Rng(), 0, 100));
        var cursor = Cursor(start);

        var first = Commit(cursor, PinchHit(1));
        var second = Commit(cursor, Approach(2));

        Assert.Equal((100L, 101L, 1), (first.RevisionBefore, first.RevisionAfter, first.EventSequence));
        Assert.Equal((101L, 102L, 2), (second.RevisionBefore, second.RevisionAfter, second.EventSequence));
        Assert.Equal((102L, 2), (cursor.Current.Revision, cursor.Current.LastEventSequence));
    }

    [Fact]
    public void A_null_staged_commit_is_refused()
    {
        var cursor = Cursor();
        var before = cursor.Current;

        Assert.Equal("Commit.staged_missing", PublishCode(cursor, null));
        Assert.Same(before, cursor.Current);
    }

    // ---------------------------------------------------------------- duplicates and stale commits

    [Fact]
    public void Publishing_the_same_staged_commit_twice_succeeds_once_and_the_second_is_stale()
    {
        var cursor = Cursor();
        var staged = Ok(CommitGate.Stage(cursor.Current, Approach(1)));
        Ok(cursor.Publish(staged));
        var after = cursor.Current;

        Assert.Equal("Commit.revision_stale", PublishCode(cursor, staged));

        Assert.Same(after, cursor.Current);
        Assert.Equal(1L, cursor.Current.Revision);
        Assert.Single(cursor.Current.State.AppliedEvents);
    }

    [Fact]
    public void A_commit_staged_from_an_old_aggregate_is_stale_and_changes_nothing()
    {
        var cursor = Cursor();
        var old = cursor.Current;
        var stale = Ok(CommitGate.Stage(old, PinchHit(1)));
        Commit(cursor, PinchHit(1, "ab2"));
        var current = cursor.Current;

        Assert.Equal("Commit.revision_stale", PublishCode(cursor, stale));

        Assert.Same(current, cursor.Current);
    }

    [Fact]
    public void A_commit_staged_for_another_games_cursor_is_refused()
    {
        var mine = Cursor();
        var other = Cursor(New());   // an equal-looking but different aggregate instance
        var staged = Ok(CommitGate.Stage(other.Current, Approach(1)));
        var before = mine.Current;

        Assert.Equal("Commit.revision_stale", PublishCode(mine, staged));

        Assert.Same(before, mine.Current);
        Assert.Same(other.Current, staged.Expected);
    }

    [Fact]
    public void An_exhausted_revision_never_produces_a_staged_commit_to_publish()
    {
        var cursor = Cursor(Ok(AuthoritativeGame.Restore(New().State, Rng(), 0, long.MaxValue)));

        Assert.Equal("Commit.revision_exhausted", Code(CommitGate.Stage(cursor.Current, Approach(1))));
        Assert.Equal(long.MaxValue, cursor.Current.Revision);
    }

    // ---------------------------------------------------------------- races

    [Fact]
    public async Task Two_commits_staged_from_the_same_aggregate_publish_exactly_one_even_when_both_pass_the_freshness_check_together()
    {
        var cursor = Cursor();
        var @base = cursor.Current;
        var a = Ok(CommitGate.Stage(@base, Approach(1)));
        var b = Ok(CommitGate.Stage(@base, PinchHit(1)));
        using var barrier = new Barrier(2);
        void Hold(string stage) => Assert.True(barrier.SignalAndWait(Wait), "both publishers must reach the swap together");

        var first = Task.Run(() => cursor.Publish(a, Hold, default));
        var second = Task.Run(() => cursor.Publish(b, Hold, default));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, r => r.IsSuccess);
        var lost = results.Single(r => !r.IsSuccess);
        Assert.Equal("Commit.cas_lost", lost.Error!.Value.Code);
        var winner = results[0].IsSuccess ? a : b;
        Assert.Same(winner.Next, cursor.Current);
        Assert.Same(winner.Record, cursor.Current.LastCommit);
        Assert.Equal((1L, 1), (cursor.Current.Revision, cursor.Current.LastEventSequence));
        Assert.Single(cursor.Current.State.AppliedEvents);   // no duplicate receipt, no reused event id
    }

    [Fact]
    public async Task The_same_staged_commit_published_by_two_threads_together_succeeds_once()
    {
        var cursor = Cursor();
        var staged = Ok(CommitGate.Stage(cursor.Current, Approach(1)));
        using var barrier = new Barrier(2);
        void Hold(string stage) => Assert.True(barrier.SignalAndWait(Wait));

        var tasks = Enumerable.Range(0, 2).Select(_ => Task.Run(() => cursor.Publish(staged, Hold, default))).ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.Single(results, r => r.IsSuccess);
        Assert.Equal("Commit.cas_lost", results.Single(r => !r.IsSuccess).Error!.Value.Code);
        Assert.Equal(1L, cursor.Current.Revision);
    }

    [Fact]
    public void A_loser_cannot_raise_the_revision_and_the_winners_chain_continues()
    {
        var cursor = Cursor();
        var @base = cursor.Current;
        var winner = Ok(CommitGate.Stage(@base, PinchHit(1)));
        var loser = Ok(CommitGate.Stage(@base, Approach(1)));
        Ok(cursor.Publish(winner));

        Assert.Equal("Commit.revision_stale", PublishCode(cursor, loser));
        Assert.Equal(1L, cursor.Current.Revision);

        var next = Commit(cursor, Approach(2));
        Assert.Equal((1L, 2L), (next.RevisionBefore, next.RevisionAfter));
        Assert.Equal(2, cursor.Current.State.AppliedEvents.Count);
    }

    // ---------------------------------------------------------------- cancellation

    [Fact]
    public void A_token_cancelled_before_the_swap_throws_and_nothing_is_published()
    {
        var cursor = Cursor();
        var before = cursor.Current;
        var staged = Ok(CommitGate.Stage(before, Approach(1)));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => cursor.Publish(staged, cts.Token));

        Assert.Same(before, cursor.Current);
        Ok(cursor.Publish(staged));                // the staged commit is still publishable afterwards
        Assert.Same(staged.Next, cursor.Current);
    }

    [Fact]
    public void Cancelling_at_the_last_moment_before_the_swap_throws_and_nothing_is_published()
    {
        var cursor = Cursor();
        var before = cursor.Current;
        var staged = Ok(CommitGate.Stage(before, Approach(1)));
        using var cts = new CancellationTokenSource();
        var seen = new List<string>();

        Assert.Throws<OperationCanceledException>(() => cursor.Publish(staged, name => { seen.Add(name); cts.Cancel(); }, cts.Token));

        Assert.Equal(["before_swap"], seen);
        Assert.Same(before, cursor.Current);
    }

    [Fact]
    public void A_hook_that_fails_before_the_swap_publishes_nothing_but_the_hook_is_never_reached_after_it()
    {
        var cursor = Cursor();
        var before = cursor.Current;
        var staged = Ok(CommitGate.Stage(before, Approach(1)));
        var calls = 0;

        Assert.Throws<InvalidOperationException>(() => cursor.Publish(staged, _ => { calls++; throw new InvalidOperationException("hook"); }, default));
        Assert.Same(before, cursor.Current);

        var ok = cursor.Publish(staged, _ => calls++, default);
        Assert.True(ok.IsSuccess);
        Assert.Equal(2, calls);   // exactly once per publish attempt: the hook runs before the swap and never after it
    }

    [Fact]
    public void A_cancellation_after_a_successful_publish_does_not_turn_the_committed_change_into_a_failure()
    {
        var cursor = Cursor();
        var staged = Ok(CommitGate.Stage(cursor.Current, Approach(1)));
        using var cts = new CancellationTokenSource();

        var record = Ok(cursor.Publish(staged, cts.Token));
        cts.Cancel();

        Assert.Same(staged.Record, record);
        Assert.Same(staged.Next, cursor.Current);
        Assert.Same(record, cursor.Current.LastCommit);
    }

    [Fact]
    public void The_publish_source_has_no_cancellation_check_hook_call_or_throw_after_the_swap()
    {
        var file = Path.Combine(RngFixture.Root, "src", "StarPlayer.Simulation", "Baseball", "GameCursor.cs");
        var code = Regex.Replace(Regex.Replace(File.ReadAllText(file), @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\r\n]*", "");

        var swap = code.IndexOf("Interlocked.CompareExchange", StringComparison.Ordinal);
        Assert.True(swap > 0);
        var tail = code[swap..];
        foreach (var forbidden in new[] { "ThrowIfCancellationRequested", "IsCancellationRequested", "cancellation", "checkpoint", ".Invoke(", "throw ", "Wait(", "await " })
            Assert.DoesNotContain(forbidden, tail.Split("private static Result<CommitRecord> Fail")[0]);
        // and the only thing that follows the swap is the answer
        Assert.Contains("ReferenceEquals(seen, staged.Expected)", tail);
    }

    [Fact]
    public void The_stage_source_reads_the_aggregate_and_never_touches_a_cursor_or_a_shared_field()
    {
        var file = Path.Combine(RngFixture.Root, "src", "StarPlayer.Simulation", "Baseball", "CommitGate.cs");
        var code = Regex.Replace(Regex.Replace(File.ReadAllText(file), @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\r\n]*", "");

        foreach (var forbidden in new[] { "GameCursor", "Interlocked", "Volatile", "lock (", "static AuthoritativeGame", "GameRandom.Create", "GameRandom.Restore", ".Stream(", "NextBounded" })
            Assert.DoesNotContain(forbidden, code);
    }
}
