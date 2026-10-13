using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C3-B (B-04, B-06): the pitch entry ends in the unchanged <see cref="GameCursor.Publish"/> compare-and-swap. Races are held at the deterministic point inside Publish with a
/// <see cref="Barrier"/>, never with Sleep. A failed or cancelled Stage or Publish leaves the aggregate the work was based on exactly as it was.
/// </summary>
public class PitchCommitGateConcurrencyTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

    private static readonly Lazy<NonInPlay> Pitch = new(() =>
    {
        var d = Situation(0, 0, true);
        return FindNonInPlay(d.State, d.LastIssued, false, (x, _) => x.Kind == PitchBranchKind.CalledStrike, PriorDraws);
    });

    /// <summary>A different, equally valid confirmation of the same base: the C3-A gate's approach is not legal mid-sequence, so the rival is another real pitch preparation.</summary>
    private static NonInPlay Rival(NonInPlay f)
    {
        foreach (var intent in new[] { PitchIntent.Attack, PitchIntent.Careful })
        {
            var prepared = Prepare(f.Game, false, intent);
            if (prepared.Draft.Kind != PitchBranchKind.InPlay && BuildNonInPlay(f.Game, prepared) is { IsSuccess: true } built)
                return new NonInPlay(f.Game, prepared, built.Value);
        }

        throw new InvalidOperationException("no rival candidate");
    }

    [Fact]
    public async Task Two_candidates_staged_from_the_same_aggregate_publish_exactly_one_winner_and_the_loser_changes_nothing()
    {
        var a = Pitch.Value;
        var b = Rival(a);
        var cursor = new GameCursor(a.Game);
        var stagedA = Ok(Stage(a.Game, a.Prepared, a.Candidate));
        var stagedB = Ok(Stage(a.Game, b.Prepared, b.Candidate));
        using var barrier = new Barrier(2);
        void Hold(string stage) => Assert.True(barrier.SignalAndWait(Wait), "both publishers must reach the swap together");

        var results = await Task.WhenAll(Task.Run(() => cursor.Publish(stagedA, Hold, default)), Task.Run(() => cursor.Publish(stagedB, Hold, default)));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Equal("Commit.cas_lost", results.Single(r => !r.IsSuccess).Error!.Value.Code);
        var winner = results[0].IsSuccess ? stagedA : stagedB;
        Assert.Same(winner.Next, cursor.Current);
        Assert.Same(winner.Record, cursor.Current.LastCommit);
        Assert.Equal(a.Game.Revision + 1, cursor.Current.Revision);                                  // one revision, never two
        Assert.Equal(a.Game.State.AppliedEvents.Count + 1, cursor.Current.State.AppliedEvents.Count); // no second receipt, no reused id
        Assert.Same(winner == stagedA ? a.Prepared.Draft.NextRng : b.Prepared.Draft.NextRng, cursor.Current.Rng);   // only the winner's position was adopted
    }

    [Fact]
    public void A_staged_pitch_that_lost_to_an_earlier_publish_is_stale_and_cannot_raise_the_revision()
    {
        var a = Pitch.Value;
        var b = Rival(a);
        var cursor = new GameCursor(a.Game);
        var winner = Ok(Stage(a.Game, a.Prepared, a.Candidate));
        var loser = Ok(Stage(a.Game, b.Prepared, b.Candidate));
        Ok(cursor.Publish(winner));

        Assert.Equal("Commit.revision_stale", Code(cursor.Publish(loser)));
        Assert.Same(winner.Next, cursor.Current);
        Assert.Equal(a.Game.Revision + 1, cursor.Current.Revision);
    }

    [Fact]
    public void The_same_candidate_submitted_again_after_it_was_published_is_already_committed_and_a_second_publish_of_it_is_stale()
    {
        var f = Pitch.Value;
        var cursor = new GameCursor(f.Game);
        var staged = Ok(Stage(f.Game, f.Prepared, f.Candidate));
        Ok(cursor.Publish(staged));
        var after = cursor.Current;

        Assert.Equal("Commit.already_committed", Code(Stage(after, f.Prepared, f.Candidate)));
        Assert.Equal("Commit.revision_stale", Code(cursor.Publish(staged)));
        Assert.Same(after, cursor.Current);
    }

    [Fact]
    public async Task The_same_staged_pitch_published_by_two_threads_together_succeeds_once()
    {
        var f = Pitch.Value;
        var cursor = new GameCursor(f.Game);
        var staged = Ok(Stage(f.Game, f.Prepared, f.Candidate));
        using var barrier = new Barrier(2);
        void Hold(string stage) => Assert.True(barrier.SignalAndWait(Wait));

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() => cursor.Publish(staged, Hold, default))));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Equal("Commit.cas_lost", results.Single(r => !r.IsSuccess).Error!.Value.Code);
        Assert.Equal(f.Game.Revision + 1, cursor.Current.Revision);
    }

    // ---------------------------------------------------------------- cancellation

    [Theory]
    [InlineData("start")]
    [InlineData("validated")]
    [InlineData("applied")]
    public void A_cancellation_at_any_stage_of_the_pitch_entry_throws_and_changes_nothing(string stage)
    {
        var f = Pitch.Value;
        var (b, p) = Sheets(f.Game.State);
        using var cts = new CancellationTokenSource();
        var cursor = new GameCursor(f.Game);
        var seen = new List<string>();

        void Hook(string name)
        {
            seen.Add(name);
            if (name == stage)
                cts.Cancel();
        }

        Assert.Throws<OperationCanceledException>(() => PitchCommitGate.Stage(f.Game, f.Prepared, f.Candidate, Tuning, b, p, Hook, cts.Token));
        Assert.Equal(stage, seen[^1]);                                                              // nothing ran after the cancelled stage
        Assert.Same(f.Game, cursor.Current);
        Assert.Equal((f.Game.Revision, f.Game.LastEventSequence, f.Game.State.AppliedEvents.Count), (cursor.Current.Revision, cursor.Current.LastEventSequence, cursor.Current.State.AppliedEvents.Count));
        Assert.Null(cursor.Current.LastCommit);
        Assert.Same(f.Game.Rng, cursor.Current.Rng);
    }

    [Fact]
    public void A_cancellation_at_the_finalize_stage_of_a_game_ending_pitch_throws_and_changes_nothing()
    {
        var d = Situation(7, 0, true, tiedBottomNinth: true);
        var f = FindNonInPlay(d.State, d.LastIssued, false, (x, _) => x.Kind == PitchBranchKind.HitByPitch);
        var (b, p) = Sheets(f.Game.State);
        using var cts = new CancellationTokenSource();
        var seen = new List<string>();

        void Hook(string name)
        {
            seen.Add(name);
            if (name == CommitGate.StageName.Finalized)
                cts.Cancel();
        }

        Assert.Throws<OperationCanceledException>(() => PitchCommitGate.Stage(f.Game, f.Prepared, f.Candidate, Tuning, b, p, Hook, cts.Token));
        Assert.Contains(CommitGate.StageName.Finalized, seen);
        Assert.Equal(GameStatus.InProgress, f.Game.State.Status);
        Assert.Null(f.Game.State.Final);
    }

    [Fact]
    public void A_cancellation_between_stage_and_publish_stops_before_the_swap_and_a_token_cancelled_afterwards_changes_nothing()
    {
        var f = Pitch.Value;
        using var cts = new CancellationTokenSource();
        var cursor = new GameCursor(f.Game);
        var staged = Ok(Stage(f.Game, f.Prepared, f.Candidate, cts.Token));

        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => cursor.Publish(staged, cts.Token));
        Assert.Same(f.Game, cursor.Current);                                                         // cancelled before the swap: nothing happened

        using var late = new CancellationTokenSource();
        var record = Ok(cursor.Publish(staged, late.Token));
        late.Cancel();                                                                               // after the swap a token changes nothing, and nothing consults it
        Assert.Same(record, cursor.Current.LastCommit);
        Assert.Same(staged.Next, cursor.Current);
    }

    [Fact]
    public void A_cancellation_requested_by_the_publish_hook_before_the_swap_leaves_the_cursor_unchanged()
    {
        var f = Pitch.Value;
        using var cts = new CancellationTokenSource();
        var cursor = new GameCursor(f.Game);
        var staged = Ok(Stage(f.Game, f.Prepared, f.Candidate));

        Assert.Throws<OperationCanceledException>(() => cursor.Publish(staged, _ => cts.Cancel(), cts.Token));
        Assert.Same(f.Game, cursor.Current);
        Ok(cursor.Publish(staged));                                                                  // the staged commit was not consumed
    }

    // ---------------------------------------------------------------- failure invariance of every refused Stage

    [Fact]
    public void Every_refused_stage_leaves_the_authoritative_values_and_their_references_unchanged()
    {
        var f = Pitch.Value;
        var before = (f.Game.State, f.Game.Rng, f.Game.LastEventSequence, f.Game.Revision, f.Game.LastCommit, f.Game.State.AppliedEvents, f.Game.Rng.Streams);
        var forged = f.Candidate.WithStep(f.Candidate.UnconfirmedStep with { WasDuplicate = true });
        var twin = Prepare(f.Game);

        Assert.Equal("Commit.preview_mismatch", Code(Stage(f.Game, f.Prepared, forged)));
        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(f.Game, twin, f.Candidate)));
        Assert.Equal("Commit.candidate_missing", Code(PitchCommitGate.Stage(f.Game, f.Prepared, null, Tuning, Sheets(f.Game.State).Batter, Sheets(f.Game.State).Pitcher)));

        var after = (f.Game.State, f.Game.Rng, f.Game.LastEventSequence, f.Game.Revision, f.Game.LastCommit, f.Game.State.AppliedEvents, f.Game.Rng.Streams);
        Assert.Equal(before.Item3, after.Item3);
        Assert.Equal(before.Item4, after.Item4);
        Assert.Same(before.Item1, after.Item1);
        Assert.Same(before.Item2, after.Item2);
        Assert.Same(before.Item5, after.Item5);
        Assert.Equal(before.Item6.Count, after.Item6.Count);
        Assert.Equal(before.Item7, after.Item7);
    }
}
