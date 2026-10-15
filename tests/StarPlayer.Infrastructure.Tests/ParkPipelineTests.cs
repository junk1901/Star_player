using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-D v2 Part A through the REAL pipeline (resolver, preparation, in-play planning, commit gate): the park snapshot comes from the game's own execution contract, is applied to
/// <c>defense_result</c> only, never draws a number, survives the commit, and a different snapshot in a preview is caught. Expected distributions come from the independent Python oracle.
/// </summary>
public class ParkPipelineTests
{
    private static readonly int[] Hitter = [1100, 1020, 1050];

    private static PitchBranchRequest Req(GameState state, GameRandomSnapshot rng, TuningProfile tuning) => Request(state, rng, tuning: tuning);

    private static GameExecution ExecFor(int[] park) => Gx.Exec(Gx.Park(park[0], park[1], park[2]));

    private static PitchBranchDraft Resolve(GameExecution exec, ulong seed, int drawsBefore = 0)
    {
        var state = Gx.InSequence(exec).State;
        return Resolved(Req(state, Snapshot(seed, Gid), Gx.Tuning2));
    }

    // ---------------------------------------------------------------- the resolver refuses what the contract forbids

    [Fact]
    public void A_model_v2_tuning_needs_an_explicit_snapshot_even_for_a_neutral_stadium()
    {
        var legacy = Gx.InSequence(null).State;

        var made = PitchBranchResolver.Resolve(Req(legacy, Snapshot(1, Gid), Gx.Tuning2));

        Assert.Equal("PitchBranch.park_snapshot_required", Code(made));
        Assert.True(PitchBranchResolver.Resolve(Req(Gx.InSequence(Gx.Exec(Gx.Park())).State, Snapshot(1, Gid), Gx.Tuning2)).IsSuccess);     // recorded neutral is enough
    }

    [Fact]
    public void A_v1_tuning_cannot_run_a_game_that_pinned_v2()
    {
        var state = Gx.InSequence(Gx.Exec()).State;

        Assert.Equal("PitchBranch.execution_mismatch", Code(PitchBranchResolver.Resolve(Req(state, Snapshot(1, Gid), Gx.Tuning1))));
    }

    [Fact]
    public void A_tuning_whose_hash_differs_from_the_pin_is_refused()
    {
        var state = Gx.InSequence(Gx.Exec() with { TuningHash = new string('b', 64) }).State;

        Assert.Equal("PitchBranch.execution_mismatch", Code(PitchBranchResolver.Resolve(Req(state, Snapshot(1, Gid), Gx.Tuning2))));
    }

    [Fact]
    public void A_state_whose_execution_was_tampered_after_the_start_is_refused_by_the_resolver()
    {
        var state = Gx.InSequence(Gx.Exec()).State;

        Assert.Equal("Game.park_out_of_model_range", Code(PitchBranchResolver.Resolve(Req(state with { Execution = Gx.Exec(Gx.Park(799)) }, Snapshot(1, Gid), Gx.Tuning2))));
        Assert.Equal("Game.park_snapshot_invalid", Code(PitchBranchResolver.Resolve(Req(state with { Execution = Gx.Exec(Gx.Park() with { Version = 0 }) }, Snapshot(1, Gid), Gx.Tuning2))));
    }

    [Fact]
    public void A_legacy_game_on_the_v1_tuning_is_exactly_what_it_was()
    {
        var state = InSequenceState(NonAutomated, Gid);
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var draft = Resolved(Req(state, Snapshot(seed, Gid), Gx.Tuning1));
            Assert.Equal("prob-model-v1", draft.ProbabilityModelVersion);
            Assert.All(draft.Steps, step => Assert.DoesNotContain(step.Trace.Modifiers, m => m.Slot.StartsWith("park.", StringComparison.Ordinal)));
        }
    }

    // ---------------------------------------------------------------- the three park slots reach defense_result and nothing else

    [Fact]
    public void Only_the_defense_result_step_receives_the_three_park_inputs_of_the_games_own_snapshot()
    {
        var inPlay = 0;
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var draft = Resolve(ExecFor(Hitter), seed);
            foreach (var step in draft.Steps)
            {
                var park = step.Trace.Modifiers.Where(m => m.Slot.StartsWith("park.", StringComparison.Ordinal)).ToArray();
                if (step.Stage != StageIds.DefenseResult)
                {
                    Assert.Empty(park);
                    continue;
                }

                inPlay++;
                Assert.Equal(["park.home_runs", "park.hits", "park.extra_base_hits"], park.Select(m => m.Slot));
                Assert.Equal(["1100", "1020", "1050"], park.Select(m => m.Input));
                Assert.Equal([1_100_000, 1_020_000, 1_050_000], park.Select(m => m.FactorPpm));
                Assert.All(park, m => Assert.True(m.Applied));
            }
        }

        Assert.True(inPlay >= 40, $"only {inPlay} in-play pitches in 400 seeds");
    }

    [Fact]
    public void The_defense_result_distribution_and_the_chosen_outcome_follow_the_independent_oracle_for_each_park()
    {
        int[][] parks = [[1000, 1000, 1000], [800, 800, 800], [1250, 1250, 1250], [1100, 1020, 1050], [900, 1150, 1000], [1250, 800, 1000]];
        var checkedCases = 0;
        foreach (var park in parks)
            for (ulong seed = 1; seed <= 600; seed++)
            {
                var draft = Resolve(ExecFor(park), seed);
                if (draft.Steps.SingleOrDefault(s => s.Stage == StageIds.DefenseResult) is not { } step)
                    continue;
                var expected = Gx.OracleDefense(step.Condition, park, null);
                Assert.Equal(expected, step.Trace.Final);
                Assert.Equal(Gx.Pick(step.Trace.Outcomes, expected, step.Roll), step.Outcome);          // selection with the recorded roll, by the test's own cumulative rule
                Assert.Equal(step.Outcome, draft.DefenseResult);
                checkedCases++;
            }

        Assert.True(checkedCases >= 200, $"only {checkedCases} in-play cases");
    }

    [Fact]
    public void A_neutral_snapshot_is_the_no_park_computation_for_every_in_play_pitch()
    {
        var neutral = 0;
        for (ulong seed = 1; seed <= 600; seed++)
        {
            var draft = Resolve(Gx.Exec(Gx.Park()), seed);
            if (draft.Steps.SingleOrDefault(s => s.Stage == StageIds.DefenseResult) is not { } step)
                continue;
            Assert.Equal(Gx.Tuning2.Baseline(StageIds.DefenseResult, step.Condition).Value.Ppm, step.Trace.Final);
            Assert.All(step.Trace.CombinedFactorPpm, f => Assert.Equal(1_000_000, f));
            neutral++;
        }

        Assert.True(neutral >= 40);
    }

    [Fact]
    public void A_park_snapshot_adds_no_draw_and_changes_no_earlier_roll_the_end_position_is_the_same()
    {
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var neutral = Resolve(Gx.Exec(Gx.Park()), seed);
            var hitter = Resolve(ExecFor(Hitter), seed);
            var pitcher = Resolve(ExecFor([800, 800, 800]), seed);

            foreach (var other in new[] { hitter, pitcher })
            {
                Assert.Equal(neutral.NextBoundedCalls, other.NextBoundedCalls);
                Assert.Equal(neutral.StreamUsage.Select(u => (u.Stream.Canonical, u.Calls, u.RawDraws)), other.StreamUsage.Select(u => (u.Stream.Canonical, u.Calls, u.RawDraws)));
                Assert.Equal(neutral.NextRng.StateHash, other.NextRng.StateHash);
                Assert.Equal(neutral.Steps.Select(s => (s.Stage, s.Condition, s.Stream.Canonical, s.Roll, s.RawDraws)), other.Steps.Select(s => (s.Stage, s.Condition, s.Stream.Canonical, s.Roll, s.RawDraws)));
                // only the defense_result choice may differ; every earlier outcome is identical
                Assert.Equal(neutral.Steps.Where(s => s.Stage != StageIds.DefenseResult).Select(s => s.Outcome), other.Steps.Where(s => s.Stage != StageIds.DefenseResult).Select(s => s.Outcome));
            }
        }
    }

    [Fact]
    public void The_same_seed_and_the_same_snapshot_give_the_same_draft_every_time()
    {
        for (ulong seed = 1; seed <= 100; seed++)
            Assert.Equal(Summary(Resolve(ExecFor(Hitter), seed)), Summary(Resolve(ExecFor(Hitter), seed)));
    }

    [Fact]
    public void A_later_game_in_another_stadium_gets_its_own_effect_and_the_running_game_keeps_its_snapshot()
    {
        var first = Gx.Exec(Gx.Park(1250, 1250, 1250, stadium: "stadium-a", profile: "pf-a"));
        var second = Gx.Exec(Gx.Park(800, 800, 800, stadium: "stadium-b", profile: "pf-b", version: 2));
        var stateA = Gx.InSequence(first).State;

        // the second game is started and played AFTER the first one was started: it cannot change the first one's retained snapshot
        var draftB = Resolve(second, 5);
        var draftA = Resolved(Req(stateA, Snapshot(5, Gid), Gx.Tuning2));

        Assert.Same(first, stateA.Execution);
        Assert.Equal((1250, 1250, 1250), (first.Park.HomeRuns.Permille, first.Park.Hits.Permille, first.Park.ExtraBaseHits.Permille));
        Assert.Equal(neutralCalls(draftA), neutralCalls(draftB));
        foreach (var (draft, park) in new[] { (draftA, new[] { 1250, 1250, 1250 }), (draftB, new[] { 800, 800, 800 }) })
            if (draft.Steps.SingleOrDefault(s => s.Stage == StageIds.DefenseResult) is { } step)
                Assert.Equal(Gx.OracleDefense(step.Condition, park, null), step.Trace.Final);

        static int neutralCalls(PitchBranchDraft d) => d.NextBoundedCalls;
    }

    // ---------------------------------------------------------------- preparation, planning and the commit gate

    [Fact]
    public void Preparation_binds_the_pinned_tuning_and_a_different_tuning_makes_the_preparation_stale()
    {
        var d = Gx.InSequence(Gx.Exec());
        var (g, prepared, _) = Gx.FindInPlay(d, Gx.Tuning2);

        Assert.Equal("prob-model-v2", prepared.ProbabilityModelVersion);
        Assert.Equal(Gx.Tuning2.ContentHash, prepared.TuningHash);
        Assert.Same(g.State.Execution, prepared.State.Execution);
        Assert.Equal("PitchCandidate.tuning_changed", Code(Gx.BuildInPlay(g, prepared, Gx.Tuning1)));
    }

    [Fact]
    public void An_in_play_candidate_of_a_v2_game_commits_and_the_published_state_keeps_the_same_snapshot()
    {
        var execution = Gx.Exec(Gx.Park(1100, 1020, 1050));
        var (g, _, candidate) = Gx.FindInPlay(Gx.InSequence(execution), Gx.Tuning2);

        var staged = Ok(Gx.Stage(g, candidate, Gx.Tuning2));
        var published = Ok(new GameCursor(g).Publish(staged));

        Assert.Same(execution, staged.Next.State.Execution);
        Assert.Equal(g.LastEventSequence + 1, published.EventSequence);
        Assert.Same(published, staged.Record);
        Assert.Equal(1, staged.Next.State.AppliedEvents.Count - g.State.AppliedEvents.Count);
    }

    [Fact]
    public void A_non_in_play_candidate_of_a_v2_game_commits_with_the_same_snapshot_too()
    {
        var execution = Gx.Exec(Gx.Park(1250, 900, 1000));
        var d = Gx.InSequence(execution);
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var g = Gx.Aggregate(d, seed);
            var prepared = Gx.Prepare(g, Gx.Tuning2);
            if (prepared.Draft.Kind == PitchBranchKind.InPlay)
                continue;
            var candidate = Ok(Gx.BuildNonInPlay(g, prepared, Gx.Tuning2));
            var staged = Ok(Gx.Stage(g, prepared, candidate, Gx.Tuning2));
            Assert.Same(execution, staged.Next.State.Execution);
            return;
        }

        throw new InvalidOperationException("no non-in-play pitch found");
    }

    [Fact]
    public void A_preview_that_carries_another_park_snapshot_is_caught_by_the_commit_gate()
    {
        var execution = Gx.Exec(Gx.Park(1100, 1020, 1050));
        var (g, _, candidate) = Gx.FindInPlay(Gx.InSequence(execution), Gx.Tuning2);
        Assert.True(Gx.Stage(g, candidate, Gx.Tuning2).IsSuccess);          // the real candidate is accepted

        foreach (var forged in new[]
        {
            Gx.Exec(Gx.Park(1101, 1020, 1050)), Gx.Exec(Gx.Park(1100, 1021, 1050)), Gx.Exec(Gx.Park(1100, 1020, 1051)),
            Gx.Exec(Gx.Park(1100, 1020, 1050, stadium: "stadium-other")), Gx.Exec(Gx.Park(1100, 1020, 1050, version: 2)), null,
        })
        {
            var step = candidate.UnconfirmedStep with { State = candidate.UnconfirmedStep.State with { Execution = forged } };

            var staged = Gx.Stage(g, candidate.WithStep(step), Gx.Tuning2);

            Assert.Equal("Commit.preview_mismatch", Code(staged));
            Assert.Contains("Step.State.Execution", Message(staged), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_game_ending_commit_finalizes_with_the_snapshot_retained_in_the_final_record()
    {
        var execution = Gx.Exec(Gx.Park(1250, 1000, 1000));
        var d = new Driver(StartState(NonAutomated, Gid, execution), Gid);
        for (var i = 0; i < 17; i++)
            d.Outs(3);
        d.Approach();
        Assert.Equal((9, InningHalf.Bottom), (d.State.Inning, d.State.Half));

        var (g, _, candidate) = Gx.FindInPlay(d, Gx.Tuning2, draft => draft.DefenseResult == "home_run", maxSeeds: 60000);
        var staged = Ok(Gx.Stage(g, candidate, Gx.Tuning2));

        Assert.True(staged.Record.Finalized);
        var final = staged.Next.State.Final!;
        Assert.Same(execution, final.Execution);
        Assert.Same(execution, staged.Next.State.Execution);
        Assert.Equal(GameOutcome.HomeWin, final.Outcome);
    }
}
