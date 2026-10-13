using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C3-B (game-engine 27.18 B-01 to B-07), the normal paths: a REAL prepared pitch and its REAL candidate are staged by <see cref="PitchCommitGate"/> through the common
/// <see cref="CommitGate"/> core and published by the unchanged <see cref="GameCursor"/>. Expected RNG positions are derived here from the starting position with the existing
/// <see cref="GameRandom"/> primitives (raw 32-bit outputs per stream), never from the code under test.
/// </summary>
public class PitchCommitGateTests
{
    private static GameCursor CursorOf(AuthoritativeGame g) => new(g);

    // ---------------------------------------------------------------- each of the four candidate paths

    [Fact]
    public void A_pitch_is_confirmed_through_the_gate_after_a_gate_confirmed_approach_and_adopts_h1()
    {
        // the whole chain through the official gates: StartNew, the C3-A gate for the approach, then the pitch gate
        var cursor = CursorOf(Ok(AuthoritativeGame.StartNew(StartState(NonAutomated, Gid), Snapshot(5, Gid, PriorDraws))));
        Ok(cursor.Publish(Ok(CommitGate.Stage(cursor.Current, new ApproachEvent($"{Gid}:e000001", ApproachKind.Normal)))));
        var g = cursor.Current;
        Assert.Equal((1, 1L), (g.LastEventSequence, g.Revision));

        PreparedPitch? prepared = null;
        NonInPlayPitchCandidate? candidate = null;
        for (ulong seed = 1; candidate is null && seed < 3000; seed++)
        {
            var probe = Aggregate(g.State, g.LastEventSequence, seed, PriorDraws, g.Revision);
            var p = Prepare(probe);
            if (p.Draft.Kind == PitchBranchKind.CalledStrike && BuildNonInPlay(probe, p) is { IsSuccess: true } built)
                (g, prepared, candidate, cursor) = (probe, p, built.Value, CursorOf(probe));
        }

        var staged = Ok(Stage(g, prepared!, candidate!));
        var record = Ok(cursor.Publish(staged));

        var h0 = g.Rng;
        var h1 = prepared!.Draft.NextRng;
        Assert.IsType<PitchEvent>(record.Event);
        Assert.Same(candidate!.Event, record.Event);
        Assert.Same(h1, cursor.Current.Rng);                                           // the verified candidate position is adopted, not recomputed
        Assert.Equal((h0.StateHash, h1.StateHash), (record.RngStateHashBefore, record.RngStateHashAfter));
        Assert.Equal((g.Revision, g.Revision + 1, g.LastEventSequence + 1), (record.RevisionBefore, record.RevisionAfter, record.EventSequence));
        Assert.Equal(g.LastEventSequence + 1, cursor.Current.LastEventSequence);
        Assert.Same(record, cursor.Current.LastCommit);
        Assert.Equal(EventFingerprint.VersionV2, record.Receipt.FingerprintVersion);   // ef2: the located pitch
        Assert.Equal(record.Receipt, cursor.Current.State.AppliedEvents[^1]);
        Assert.False(record.Finalized);
        Assert.Equal(g.Revision + 1, cursor.Current.Revision);
        Assert.Equal(PitchesOf(g.State) + 1, PitchesOf(cursor.Current.State));
    }

    [Fact]
    public void The_position_adopted_by_a_pitch_is_exactly_the_start_plus_the_draft_s_raw_draws_per_stream()
    {
        var driver = Situation(0, 0, approach: true);
        var found = FindNonInPlay(driver.State, driver.LastIssued, false, (d, _) => d.Kind == PitchBranchKind.CalledBall, PriorDraws);

        var staged = Ok(Stage(found.Game, found.Prepared, found.Candidate));

        var independent = Advance(found.Game.Rng, Usage(found.Prepared.Draft));
        Assert.Equal(independent.StateHash, staged.Next.Rng.StateHash);                 // derived with raw 32-bit outputs, not with the resolver
        Assert.Equal(independent.Canonical(), staged.Next.Rng.Canonical());
        Assert.Same(found.Candidate.NextRng, staged.Next.Rng);
        foreach (var held in found.Game.Rng.Streams)                                    // every stream H0 had is still there, never backwards
            Assert.Contains(staged.Next.Rng.Streams, s => s.Stream == held.Stream && s.Draws >= held.Draws);
    }

    [Fact]
    public void An_explicit_take_is_confirmed_as_a_take_event_and_the_pitch_count_grows_by_one()
    {
        var driver = Situation(0, 0, approach: false);
        var found = FindNonInPlay(driver.State, driver.LastIssued, true, (d, _) => d.Kind is PitchBranchKind.CalledBall or PitchBranchKind.CalledStrike);
        var cursor = CursorOf(found.Game);

        var record = Ok(cursor.Publish(Ok(Stage(found.Game, found.Prepared, found.Candidate))));

        var take = Assert.IsType<TakeEvent>(record.Event);
        Assert.NotNull(take.Spec.Location);
        Assert.Equal(PaStage.Decision, cursor.Current.State.Stage);                      // control is back at a decision point (D1)
        Assert.Equal(PitchesOf(found.Game.State) + 1, PitchesOf(cursor.Current.State));
        Assert.Same(found.Prepared.Draft.NextRng, cursor.Current.Rng);
        Assert.Equal($"{Gid}:e{found.Game.LastEventSequence + 1:D6}", record.Event.Id);
    }

    [Fact]
    public void A_base_in_play_candidate_is_confirmed_and_adopts_h1()
    {
        var driver = Situation(0, 0, approach: true);
        var found = FindBaseInPlay(driver.State, driver.LastIssued, c => c.Plan.OfficialResult == InPlayResult.Single, PriorDraws);
        var cursor = CursorOf(found.Game);

        var record = Ok(cursor.Publish(Ok(Stage(found.Game, found.Candidate))));

        Assert.IsType<InPlayEvent>(record.Event);
        Assert.Null(found.Candidate.Audit.AdvancementDetail);
        Assert.Same(found.Prepared.Draft.NextRng, cursor.Current.Rng);                   // H1, the very instance
        Assert.Equal(found.Prepared.Draft.NextRng.StateHash, record.RngStateHashAfter);
        Assert.Equal(EventFingerprint.VersionV2, record.Receipt.FingerprintVersion);
        Assert.Equal(PitchesOf(found.Game.State) + 1, PitchesOf(cursor.Current.State));
        Assert.NotNull(cursor.Current.State.Bases.First);                                // the single put the batter on first
    }

    [Fact]
    public void A_resolved_advancement_candidate_is_confirmed_and_adopts_h2_with_exactly_the_runner_draws()
    {
        var driver = Situation(1, 0, approach: true);
        var found = FindResolved(driver.State, driver.LastIssued, c => c.Plan.OfficialResult == InPlayResult.Single, PriorDraws);
        var cursor = CursorOf(found.Game);
        var detail = found.Candidate.Audit.AdvancementDetail!;

        var record = Ok(cursor.Publish(Ok(Stage(found.Game, found.Candidate))));

        var h1 = found.Prepared.Draft.NextRng;
        var h2 = found.Candidate.NextRng;
        Assert.NotSame(h1, h2);
        Assert.Same(h2, cursor.Current.Rng);                                             // H2, the candidate's own snapshot
        Assert.Equal((found.Game.Rng.StateHash, h2.StateHash), (record.RngStateHashBefore, record.RngStateHashAfter));
        var independent = Advance(Advance(found.Game.Rng, Usage(found.Prepared.Draft)), (GameRngStreams.Runner, detail.RawDraws));
        Assert.Equal(independent.StateHash, cursor.Current.Rng.StateHash);               // H0 + the pitch's raw draws + the runner stream's raw draws
        Assert.True(detail.RawDraws >= detail.LogicalDraws);
        Assert.Equal(EventFingerprint.VersionV2, record.Receipt.FingerprintVersion);
    }

    // ---------------------------------------------------------------- a game-ending pitch on each path: Finalize in the single Publish

    private static void AssertFinalizedOnce(GameCursor cursor, CommitRecord record, AuthoritativeGame before)
    {
        var state = cursor.Current.State;
        Assert.True(record.Finalized);
        Assert.Equal(GameStatus.Final, state.Status);
        Assert.NotNull(state.Final);                                                     // the Finalize result is part of the one published state
        Assert.Equal(state.Outcome, state.Final!.Outcome);
        Assert.Single(record.Facts, f => f is GameCompleted);                            // exactly once
        Assert.Equal(before.Revision + 1, cursor.Current.Revision);                      // one Publish, one revision
        Assert.Same(record, cursor.Current.LastCommit);
        Assert.Equal(before.LastEventSequence + 1, cursor.Current.LastEventSequence);
    }

    [Fact]
    public void A_game_ending_hit_by_pitch_is_finalized_in_the_same_stage_and_published_once()
    {
        var driver = Situation(7, 0, approach: true, tiedBottomNinth: true);            // bases loaded, bottom of the 9th, tied
        var found = FindNonInPlay(driver.State, driver.LastIssued, false, (d, _) => d.Kind == PitchBranchKind.HitByPitch);
        Assert.Equal(GameStatus.Final, found.Candidate.UnconfirmedStep.State.Status);
        var cursor = CursorOf(found.Game);

        var record = Ok(cursor.Publish(Ok(Stage(found.Game, found.Prepared, found.Candidate))));

        AssertFinalizedOnce(cursor, record, found.Game);
        Assert.Equal(GameOutcome.HomeWin, cursor.Current.State.Outcome);
        Assert.Same(found.Prepared.Draft.NextRng, cursor.Current.Rng);
    }

    [Fact]
    public void A_game_ending_base_in_play_hit_is_finalized_in_the_same_stage_and_published_once()
    {
        var driver = Situation(4, 0, approach: true, tiedBottomNinth: true);            // runner on third, tied
        var found = FindBaseInPlay(driver.State, driver.LastIssued, c => c.UnconfirmedStep.State.Status == GameStatus.Final && c.Audit.AdvancementDetail is null);
        var cursor = CursorOf(found.Game);

        var record = Ok(cursor.Publish(Ok(Stage(found.Game, found.Candidate))));

        AssertFinalizedOnce(cursor, record, found.Game);
        Assert.Same(found.Prepared.Draft.NextRng, cursor.Current.Rng);
    }

    [Fact]
    public void A_game_ending_resolved_advancement_is_finalized_in_the_same_stage_and_adopts_h2()
    {
        var driver = Situation(2, 0, approach: true, tiedBottomNinth: true);            // runner on second, tied
        var found = FindResolved(driver.State, driver.LastIssued, c => c.UnconfirmedStep.State.Status == GameStatus.Final);
        var cursor = CursorOf(found.Game);

        var record = Ok(cursor.Publish(Ok(Stage(found.Game, found.Candidate))));

        AssertFinalizedOnce(cursor, record, found.Game);
        Assert.Same(found.Candidate.NextRng, cursor.Current.Rng);
    }

    // ---------------------------------------------------------------- the staged commit changes nothing until it is published

    [Fact]
    public void Staging_a_pitch_changes_nothing_until_the_cursor_publishes_it_and_the_staged_values_are_the_core_s()
    {
        var driver = Situation(0, 0, approach: true);
        var found = FindNonInPlay(driver.State, driver.LastIssued, false, (d, _) => d.Kind == PitchBranchKind.Foul);
        var cursor = CursorOf(found.Game);

        var staged = Ok(Stage(found.Game, found.Prepared, found.Candidate));

        Assert.Same(found.Game, cursor.Current);
        Assert.Same(found.Game, staged.Expected);
        Assert.Same(staged.Record, staged.Next.LastCommit);
        Assert.NotSame(found.Candidate.UnconfirmedStep.State, staged.Next.State);        // the preview state is never adopted; the re-run's result is
        Assert.Equal(found.Game.Revision + 1, staged.Next.Revision);
    }

    private static int PitchesOf(GameState s) => s.Fielding.Pitcher is var pitcher ? s.Team(s.FieldingSide).PitcherUsages.Where(u => u.Pitcher == pitcher).Sum(u => u.PitchCount) : 0;
}
