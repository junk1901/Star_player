using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Simulation.Tests.Authority.Auth;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests.Authority;

/// <summary>Phase 3-C3-A, game-engine 27.16 O-2/O-4/O-5/O-9: the pure Stage step. Every failure leaves the aggregate exactly as it was.</summary>
public class CommitGateStageTests
{
    private static PitchEvent Pitch(int n) => new(Id(n), new PitchSpec(PitchOutcome.Ball));

    private static TakeEvent Take(int n) => new(Id(n), new PitchSpec(PitchOutcome.Ball));

    private static InPlayEvent InPlay(int n) => new(Id(n), BattedBallType.Fly, InPlayResult.Out, false, null, [Po(FieldPosition.LeftField)]);

    private static RunnerPlayEvent Balk(int n) => new(Id(n), RunnerPlayKind.Balk, [M(3, 4, MoveCause.Balk)]);

    /// <summary>Bottom of the ninth, tied, the home batter on third: a balk ends the game.</summary>
    private static AuthoritativeGame WalkOffSetUp(long revision = 0) => CanonScript.ToBottomOfNinthTied().Triple().ToAuthority(revision);

    private static AuthoritativeGame AfterOneApproach() => Commit1(New());

    private static AuthoritativeGame Commit1(AuthoritativeGame game)
    {
        var cursor = Cursor(game);
        Commit(cursor, Approach(game.LastEventSequence + 1));
        return cursor.Current;
    }

    private static void AssertUnchanged(AuthoritativeGame game, GameState state, GameRandomSnapshot rng, int last, long revision, CommitRecord? commit)
    {
        Assert.Same(state, game.State);
        Assert.Same(rng, game.Rng);
        Assert.Equal((last, revision), (game.LastEventSequence, game.Revision));
        Assert.Same(commit, game.LastCommit);
    }

    private static string Failure(AuthoritativeGame? game, GameEvent? e, CancellationToken ct = default)
    {
        var (state, rng, last, revision, commit) = game is null ? default : (game.State, game.Rng, game.LastEventSequence, game.Revision, game.LastCommit);
        var code = Code(CommitGate.Stage(game, e, ct));
        if (game is not null)
            AssertUnchanged(game, state, rng, last, revision, commit);
        return code;
    }

    // ---------------------------------------------------------------- the three allowed events

    [Fact]
    public void An_approach_event_stages_the_next_aggregate_with_one_receipt_revision_plus_one_and_the_same_rng_reference()
    {
        var game = New();
        var e = Approach(1);

        var staged = Ok(CommitGate.Stage(game, e));

        Assert.Same(game, staged.Expected);
        Assert.NotSame(game, staged.Next);
        Assert.Same(staged.Record, staged.Next.LastCommit);
        Assert.Equal((1, 1L), (staged.Next.LastEventSequence, staged.Next.Revision));
        Assert.Same(game.Rng, staged.Next.Rng);
        Assert.Equal(game.Rng.StateHash, staged.Next.Rng.StateHash);
        Assert.Equal(1, staged.Next.State.AppliedEvents.Count - game.State.AppliedEvents.Count);
        Assert.Equal(EventFingerprint.Receipt(e), staged.Next.State.AppliedEvents[^1]);
        Assert.Equal(PaStage.InSequence, staged.Next.State.Stage);
        // the input is untouched
        AssertUnchanged(game, game.State, game.Rng, 0, 0, null);
        Assert.Empty(game.State.AppliedEvents);
    }

    [Fact]
    public void The_commit_record_holds_exactly_the_minimal_contract_values()
    {
        var game = New();
        var e = Approach(1);

        var record = Ok(CommitGate.Stage(game, e)).Record;

        Assert.Same(e, record.Event);
        Assert.Equal(EventFingerprint.Receipt(e), record.Receipt);
        Assert.Equal(1, record.EventSequence);
        Assert.Equal((0L, 1L), (record.RevisionBefore, record.RevisionAfter));
        Assert.Equal(game.Rng.StateHash, record.RngStateHashBefore);
        Assert.Equal(record.RngStateHashBefore, record.RngStateHashAfter);
        Assert.Empty(record.Facts);
        Assert.False(record.Finalized);
    }

    [Fact]
    public void A_runner_play_event_is_staged_with_the_reducers_facts_and_no_rng_change()
    {
        var early = new CanonScript().Triple().ToAuthority();

        var staged = Ok(CommitGate.Stage(early, Balk(early.LastEventSequence + 1)));

        Assert.Equal(1, staged.Next.State.AwayRuns);
        Assert.Contains(staged.Record.Facts, f => f is RunnerPlayCompleted { Kind: PlayKind.Balk, RunsScored: 1 });
        Assert.Same(early.Rng, staged.Next.Rng);
        Assert.False(staged.Record.Finalized);
        Assert.Equal(GameStatus.InProgress, staged.Next.State.Status);
    }

    [Fact]
    public void A_substitution_event_is_staged_with_the_reducers_fact_and_no_rng_change()
    {
        var game = New();

        var staged = Ok(CommitGate.Stage(game, PinchHit(1)));

        Assert.Contains(staged.Record.Facts, f => f is SubstitutionMade { Team: TeamSide.Away, In: var who } && who == P("ab1"));
        Assert.Single(staged.Next.State.Substitutions);
        Assert.Equal(Id(1), staged.Next.State.Substitutions[0].EventId);
        Assert.Same(game.Rng, staged.Next.Rng);
    }

    [Fact]
    public void Revision_and_the_event_sequence_are_independent_values()
    {
        var game = CanonScript.ToBottomOfNinthTied().ToAuthority(revision: 10);

        var staged = Ok(CommitGate.Stage(game, Approach(game.LastEventSequence + 1)));

        Assert.Equal((103, 11L), (staged.Next.LastEventSequence, staged.Next.Revision));
        Assert.Equal((10L, 11L), (staged.Record.RevisionBefore, staged.Record.RevisionAfter));
    }

    [Fact]
    public void Staging_the_same_input_twice_gives_equal_content_in_two_separate_instances_and_changes_nothing()
    {
        var game = New();
        var e = Approach(1);

        var first = Ok(CommitGate.Stage(game, e));
        var second = Ok(CommitGate.Stage(game, e));

        Assert.NotSame(first, second);
        Assert.NotSame(first.Next, second.Next);
        Assert.Equal(first.Record.Receipt, second.Record.Receipt);
        Assert.Equal((first.Next.Revision, first.Next.LastEventSequence), (second.Next.Revision, second.Next.LastEventSequence));
        Assert.Equal(first.Next.State.AppliedEvents, second.Next.State.AppliedEvents);
        AssertUnchanged(game, game.State, game.Rng, 0, 0, null);
    }

    // ---------------------------------------------------------------- the three refused kinds

    [Fact]
    public void Pitch_take_and_in_play_events_are_refused_in_c3a_with_nothing_changed()
    {
        var game = New();

        Assert.Equal("Commit.event_kind_not_supported", Failure(game, Pitch(1)));
        Assert.Equal("Commit.event_kind_not_supported", Failure(game, Take(1)));
        Assert.Equal("Commit.event_kind_not_supported", Failure(game, InPlay(1)));
    }

    // ---------------------------------------------------------------- existence

    [Fact]
    public void A_missing_aggregate_or_event_is_refused_and_the_aggregate_check_comes_first()
    {
        Assert.Equal("Commit.authority_missing", Failure(null, Approach(1)));
        Assert.Equal("Commit.event_missing", Failure(New(), null));
        Assert.Equal("Commit.authority_missing", Failure(null, null));
        Assert.Equal("Commit.authority_missing", Failure(null, Pitch(1)));
    }

    // ---------------------------------------------------------------- id form, game, sequence

    [Fact]
    public void A_non_canonical_id_is_invalid_including_a_diagnostic_id_a_seven_digit_sequence_and_sequence_zero()
    {
        var game = New();
        foreach (var bad in new[] { "e1", "e000001", $"{GameId}:e1", $"{GameId}:e0000001", $"{GameId}:e1000000", $"{GameId}:e000000", $"{GameId}:E000001",
                     $" {GameId}:e000001", $"{GameId}:e000001 ", $"{GameId}:e00000a", $"{GameId}:e-00001", $"{GameId}:e+00001", "", " ", "game:x" })
            Assert.Equal("Commit.event_id_invalid", Failure(game, new ApproachEvent(bad, ApproachKind.Normal)));
        Assert.Equal("Commit.event_id_invalid", Failure(game, new ApproachEvent(null!, ApproachKind.Normal)));
    }

    [Fact]
    public void A_canonical_id_of_another_game_is_a_game_mismatch_but_a_non_canonical_foreign_id_is_just_invalid()
    {
        var game = New();

        Assert.Equal("Commit.event_game_mismatch", Failure(game, new ApproachEvent(Id(1, "game:test:other"), ApproachKind.Normal)));
        Assert.Equal("Commit.event_game_mismatch", Failure(game, new ApproachEvent(Id(1, "g2"), ApproachKind.Normal)));
        Assert.Equal("Commit.event_id_invalid", Failure(game, new ApproachEvent("game:test:other:e1", ApproachKind.Normal)));
        Assert.Equal("Commit.event_id_invalid", Failure(game, new ApproachEvent("bad game!:e000001", ApproachKind.Normal)));
        Assert.Equal("Commit.event_id_invalid", Failure(game, new ApproachEvent("game:test:other:e1000000", ApproachKind.Normal)));
    }

    [Fact]
    public void Exactly_the_next_sequence_succeeds_and_a_skipped_number_is_refused()
    {
        var cursor = Cursor();
        Commit(cursor, PinchHit(1));
        var game = cursor.Current; // last = 1

        Assert.True(CommitGate.Stage(game, Approach(2)).IsSuccess);
        Assert.Equal("Commit.event_sequence_gap", Failure(game, Approach(3)));
        Assert.Equal("Commit.event_sequence_gap", Failure(game, Approach(999_999)));
        Assert.Equal("Commit.event_sequence_gap", Failure(New(), Approach(2)));
    }

    // ---------------------------------------------------------------- duplicates

    [Fact]
    public void Re_submitting_a_committed_event_is_already_committed_and_changes_nothing()
    {
        var cursor = Cursor();
        var e = Approach(1);
        Commit(cursor, e);
        var current = cursor.Current;

        Assert.Equal("Commit.already_committed", Failure(current, e));
        Assert.Equal("Commit.already_committed", Failure(current, Approach(1))); // a separate but equal instance
        Assert.Same(current, cursor.Current);
    }

    [Fact]
    public void The_same_id_with_another_kind_or_another_content_is_in_use_not_a_duplicate()
    {
        var cursor = Cursor();
        Commit(cursor, PinchHit(1));
        Commit(cursor, Approach(2));
        var current = cursor.Current;

        Assert.Equal("Commit.event_id_in_use", Failure(current, Approach(1)));                                      // other kind
        Assert.Equal("Commit.event_id_in_use", Failure(current, new ApproachEvent(Id(2), ApproachKind.Patient)));  // same kind, other content
        Assert.Equal("Commit.event_id_in_use", Failure(current, PinchHit(1, "ab2")));                               // same kind, other content
        Assert.Equal("Commit.already_committed", Failure(current, PinchHit(1)));
        Assert.Equal("Commit.already_committed", Failure(current, Approach(2)));
    }

    // ---------------------------------------------------------------- reducer re-run, receipt

    [Fact]
    public void A_reducer_refusal_passes_through_unchanged()
    {
        var game = AfterOneApproach();

        // a second approach in the same plate appearance is illegal; the gate reports the reducer's own code
        Assert.Equal("Game.approach_not_legal", Failure(game, Approach(2)));
    }

    [Fact]
    public void An_aggregate_whose_last_receipt_does_not_match_its_last_sequence_is_authority_inconsistent()
    {
        var real = EventFingerprint.Receipt(Approach(1));
        var state = New().State;

        var countMismatch = new AuthoritativeGame(state with { AppliedEvents = [real] }, Rng(), 0, 0, null);
        var lastIdMismatch = new AuthoritativeGame(state with { AppliedEvents = [real with { EventId = Id(2) }] }, Rng(), 1, 0, null);
        var unparsable = new AuthoritativeGame(state with { AppliedEvents = [real with { EventId = "e1" }] }, Rng(), 1, 0, null);

        Assert.Equal("Commit.authority_inconsistent", Failure(countMismatch, Approach(1)));
        Assert.Equal("Commit.authority_inconsistent", Failure(lastIdMismatch, Approach(2)));
        Assert.Equal("Commit.authority_inconsistent", Failure(unparsable, Approach(2)));
    }

    [Fact]
    public void A_reducer_that_answers_a_duplicate_instead_of_one_new_receipt_is_a_receipt_mismatch()
    {
        // Crafted through the internal constructor: the receipt of e3 is already in the history although the last sequence is 2, so the reducer sees a duplicate.
        var third = Approach(3);
        var receipts = ImmutableList.Create(EventFingerprint.Receipt(third), EventFingerprint.Receipt(PinchHit(2)));
        var crafted = new AuthoritativeGame(New().State with { AppliedEvents = receipts }, Rng(), 2, 0, null);

        Assert.Equal("Commit.receipt_mismatch", Failure(crafted, third));
    }

    // ---------------------------------------------------------------- order of the checks (O-2 alternative C)

    [Fact]
    public void The_checks_run_in_the_approved_order()
    {
        var one = AfterOneApproach(); // an approach has happened: another approach is illegal (reducer), last = 1

        // 1 before 2: a missing aggregate beats an unsupported kind
        Assert.Equal("Commit.authority_missing", Failure(null, Pitch(1)));
        // 2 before 4 and 5/6: an unsupported kind beats a bad id, a foreign game, a gap and a duplicate sequence
        Assert.Equal("Commit.event_kind_not_supported", Failure(one, new PitchEvent("e1", new PitchSpec(PitchOutcome.Ball))));
        Assert.Equal("Commit.event_kind_not_supported", Failure(one, new PitchEvent(Id(1, "game:test:other"), new PitchSpec(PitchOutcome.Ball))));
        Assert.Equal("Commit.event_kind_not_supported", Failure(one, Pitch(9)));
        Assert.Equal("Commit.event_kind_not_supported", Failure(one, Pitch(1)));
        // 4 before 6: a foreign game beats a gap; a bad form beats a gap
        Assert.Equal("Commit.event_game_mismatch", Failure(one, new ApproachEvent(Id(9, "game:test:other"), ApproachKind.Normal)));
        Assert.Equal("Commit.event_id_invalid", Failure(one, new ApproachEvent($"{GameId}:e1000000", ApproachKind.Normal)));
        // 6 before 9: a gap beats a reducer refusal (an illegal second approach numbered e5)
        Assert.Equal("Commit.event_sequence_gap", Failure(one, Approach(5)));
        Assert.Equal("Game.approach_not_legal", Failure(one, Approach(2)));
        // 5 before 8: identical is already_committed, different is in_use
        Assert.Equal("Commit.already_committed", Failure(one, Approach(1)));
        Assert.Equal("Commit.event_id_in_use", Failure(one, PinchHit(1)));
    }

    [Fact]
    public void On_a_finished_game_a_re_sent_event_is_a_duplicate_error_a_new_one_is_game_already_over_and_a_changed_one_is_in_use()
    {
        var cursor = Cursor(WalkOffSetUp());
        var last = cursor.Current.LastEventSequence;
        Commit(cursor, Balk(last + 1));
        var done = cursor.Current;
        Assert.Equal(GameStatus.Final, done.State.Status);

        // the reducer would answer a re-sent identical event with a success before it looks at the finished status; the gate must not
        Assert.Equal("Commit.already_committed", Failure(done, Balk(last + 1)));
        Assert.Equal("Commit.already_committed", Failure(done, Approach(1)));
        Assert.Equal("Commit.event_id_in_use", Failure(done, PinchHit(last + 1)));
        Assert.Equal("Game.already_over", Failure(done, Approach(last + 2)));
        Assert.Equal("Commit.event_sequence_gap", Failure(done, Approach(last + 3)));
    }

    [Fact]
    public void A_revision_at_the_limit_is_refused_only_after_the_reducer_accepted_the_event()
    {
        var atLimit = Ok(AuthoritativeGame.Restore(New().State, Rng(), 0, long.MaxValue));
        var nearLimit = Ok(AuthoritativeGame.Restore(New().State, Rng(), 0, long.MaxValue - 1));

        Assert.Equal("Commit.revision_exhausted", Failure(atLimit, Approach(1)));
        Assert.Equal(long.MaxValue, Ok(CommitGate.Stage(nearLimit, Approach(1))).Next.Revision);

        var oneApproach = Ok(AuthoritativeGame.Restore(AfterOneApproach().State, Rng(), 1, long.MaxValue));
        Assert.Equal("Game.approach_not_legal", Failure(oneApproach, Approach(2)));
    }

    // ---------------------------------------------------------------- Finalize in the same stage

    [Fact]
    public void A_game_ending_event_is_applied_and_finalized_in_one_stage_with_one_revision_step_and_one_completed_fact()
    {
        var game = WalkOffSetUp(revision: 5);
        var last = game.LastEventSequence;
        Assert.Equal(GameStatus.InProgress, game.State.Status);

        var staged = Ok(CommitGate.Stage(game, Balk(last + 1)));

        var next = staged.Next;
        Assert.Equal(GameStatus.Final, next.State.Status);
        Assert.NotNull(next.State.Final);
        Assert.Equal((next.State.GameId, GameOutcome.HomeWin, 0, 1), (next.State.Final!.GameId, next.State.Final.Outcome, next.State.Final.AwayRuns, next.State.Final.HomeRuns));
        Assert.Equal((last + 1, 6L), (next.LastEventSequence, next.Revision));
        Assert.Equal(game.State.AppliedEvents.Count + 1, next.State.AppliedEvents.Count);   // no receipt for the finalization
        Assert.True(staged.Record.Finalized);
        Assert.Single(staged.Record.Facts.OfType<GameCompleted>());
        Assert.Equal(new GameCompleted(GameId, GameOutcome.HomeWin, 0, 1), staged.Record.Facts.OfType<GameCompleted>().Single());
        Assert.Single(staged.Record.Facts.OfType<RunnerPlayCompleted>());
        Assert.Same(game.Rng, next.Rng);
        Assert.Equal(staged.Record.RngStateHashBefore, staged.Record.RngStateHashAfter);
        Assert.True(AuthoritativeGame.IsComplete(next.State));
        // the input is still the unfinished game
        Assert.Equal(GameStatus.InProgress, game.State.Status);
        Assert.Null(game.State.Final);
    }

    // ---------------------------------------------------------------- cancellation

    [Fact]
    public void A_token_cancelled_before_the_call_throws_before_anything_else_is_looked_at()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => CommitGate.Stage(New(), Approach(1), cts.Token));
        Assert.Throws<OperationCanceledException>(() => CommitGate.Stage(null, null, cts.Token));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("validated")]
    [InlineData("applied")]
    public void Cancelling_at_any_stage_throws_and_leaves_the_aggregate_untouched(string stage)
    {
        var game = New();
        using var cts = new CancellationTokenSource();

        Assert.Throws<OperationCanceledException>(() =>
            CommitGate.Stage(game, Approach(1), name => { if (name == stage) cts.Cancel(); }, cts.Token));

        AssertUnchanged(game, game.State, game.Rng, 0, 0, null);
    }

    [Fact]
    public void Cancelling_after_the_finalization_throws_and_no_staged_commit_exists()
    {
        var game = WalkOffSetUp();
        using var cts = new CancellationTokenSource();
        var seen = new List<string>();

        Assert.Throws<OperationCanceledException>(() =>
            CommitGate.Stage(game, Balk(game.LastEventSequence + 1), name => { seen.Add(name); if (name == "finalized") cts.Cancel(); }, cts.Token));

        Assert.Equal(["start", "validated", "applied", "finalized"], seen);
        Assert.Equal(GameStatus.InProgress, game.State.Status);
        Assert.Null(game.State.Final);
    }

    [Fact]
    public void A_failure_before_a_stage_is_reached_does_not_report_that_stage()
    {
        var seen = new List<string>();

        Assert.False(CommitGate.Stage(New(), Pitch(1), seen.Add, default).IsSuccess);

        Assert.Equal(["start"], seen);
    }
}
