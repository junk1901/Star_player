using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Players;
using StarPlayer.SimHarness;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>F-A: one game through the production engine, deterministic, legal, ended legally, failing loudly.</summary>
public class ExecutionTests
{
    [Theory]
    [InlineData(3UL)]
    [InlineData(11UL)]
    [InlineData(29UL)]
    public void A_normal_game_completes_and_is_consistent(ulong seed)
    {
        var result = H.Play(H.Config(), seed);

        Assert.Equal(GameRunStatus.Completed, result.Status);
        Assert.Null(result.Failure);
        Assert.NotNull(result.Outcome);
        Assert.Equal(GameEndKind.Regulation, result.EndKind);                     // these three seeds end in regulation; a seed that went to extras would show as ExtraInnings
        Assert.Equal(9, result.FinalInning);
        Assert.Equal(result.Outcome == GameOutcome.HomeWin, result.HomeRuns > result.AwayRuns);
        Assert.Equal(result.Outcome == GameOutcome.AwayWin, result.AwayRuns > result.HomeRuns);
        Assert.Equal(result.CommittedEvents, result.Counters.Events);              // everything committed was counted, nothing else
        Assert.True(result.Counters.PlateAppearances > 50 && result.Counters.Pitches > result.Counters.PlateAppearances);
        Assert.True(result.CrossCheck!.Passed);
    }

    [Fact]
    public void The_same_game_and_inputs_give_identical_results_hashes_and_counters()
    {
        foreach (var seed in new ulong[] { 3, 11, 29 })
        {
            var first = H.Play(H.Config("run-a"), seed);
            var second = H.Play(H.Config("run-b"), seed);                         // another run id and a freshly built cohort: only the pinned inputs matter

            Assert.Equal(first.EventHash, second.EventHash);
            Assert.Equal(first.RngHash, second.RngHash);
            Assert.Equal(first.ResultHash, second.ResultHash);
            Assert.Equal(first.Counters.Canonical(), second.Counters.Canonical());
            Assert.Equal((first.AwayRuns, first.HomeRuns, first.FinalInning, first.FinalHalf), (second.AwayRuns, second.HomeRuns, second.FinalInning, second.FinalHalf));
        }
    }

    [Fact]
    public void The_seed_and_the_game_id_each_change_the_result()
    {
        var config = H.Config();
        var a = H.Play(config, 3, 1);
        var otherSeed = H.Play(config, 4, 1);
        var otherId = H.Play(config, 3, 2);                                       // same seed, another GameId: the RNG context key is the game id

        Assert.NotEqual(a.EventHash, otherSeed.EventHash);
        Assert.NotEqual(a.EventHash, otherId.EventHash);
        Assert.NotEqual(a.RngHash, otherId.RngHash);
        Assert.Equal("game:sim:neutral-v1-000001", a.Game.GameId);
        Assert.Equal("game:sim:neutral-v1-000002", otherId.Game.GameId);
    }

    [Fact]
    public void A_legal_tie_at_the_extra_innings_limit_completes_and_is_not_a_failure()
    {
        var config = H.Config("tie", rules: H.OneInningTieRules());
        var results = Enumerable.Range(1, 40).Select(i => H.Play(config, (ulong)i, i)).ToList();

        Assert.All(results, r => Assert.Equal(GameRunStatus.Completed, r.Status));
        var tie = results.First(r => r.Outcome == GameOutcome.Tie);               // a plain deterministic scan of seeds 1..40, not a tuned selection
        Assert.Equal(GameEndKind.TieAtExtraInningsLimit, tie.EndKind);
        Assert.Equal(tie.AwayRuns, tie.HomeRuns);
        Assert.Null(tie.Failure);
        Assert.Contains(results, r => r.Outcome != GameOutcome.Tie);
    }

    [Fact]
    public void A_game_that_runs_out_of_budget_fails_explicitly_with_its_reproduction_data()
    {
        var config = H.Config("budget", budget: 25);
        var result = H.Play(config, 29, 7);

        Assert.Equal(GameRunStatus.Failed, result.Status);
        Assert.False(result.Completed);
        Assert.Equal("Harness.event_budget_exceeded", result.Failure!.Code);
        Assert.Equal(25, result.CommittedEvents);
        Assert.Equal(25, result.Failure.EventSequence);
        Assert.Null(result.Outcome);
        Assert.Equal(GameEndKind.None, result.EndKind);
        Assert.Equal((7, "game:sim:neutral-v1-000007", 29UL), (result.Game.GameIndex, result.Game.GameId, result.Game.Seed));
        Assert.Equal(12, result.Failure.LastReceipts.Count);
        Assert.Equal(result.RngHash, result.Failure.RngHash);
        Assert.Equal(25, result.Counters.Events);
    }

    [Fact]
    public void A_budget_that_is_exactly_enough_is_a_completed_game_not_a_failure()
    {
        var full = H.Play(H.Config(), 3);
        var exact = H.Play(H.Config("exact", budget: full.CommittedEvents), 3);
        var short1 = H.Play(H.Config("short", budget: full.CommittedEvents - 1), 3);

        Assert.Equal(GameRunStatus.Completed, exact.Status);
        Assert.Equal(full.ResultHash, exact.ResultHash);
        Assert.Equal("Harness.event_budget_exceeded", short1.Failure!.Code);
    }

    [Fact]
    public void A_cancelled_game_stops_with_only_committed_events_and_is_not_finished()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var config = H.Config();
        var result = GameExecutor.Play(config, config.Spec(1, 3), source.Token);

        Assert.Equal(GameRunStatus.Cancelled, result.Status);
        Assert.Equal("Harness.cancelled", result.Failure!.Code);
        Assert.Equal(0, result.CommittedEvents);
        Assert.Null(result.Outcome);
    }

    [Fact]
    public void A_cancellation_in_the_middle_leaves_exactly_the_committed_prefix()
    {
        using var source = new CancellationTokenSource();
        var config = H.Config("cancel", policy: new CancelAfter(source, 40));
        var result = GameExecutor.Play(config, config.Spec(1, 11), source.Token);

        Assert.Equal(GameRunStatus.Cancelled, result.Status);
        Assert.InRange(result.CommittedEvents, 1, 120);
        Assert.Equal(result.CommittedEvents, result.Counters.Events);
        Assert.Equal(result.CommittedEvents, result.Failure!.EventSequence);
    }

    private sealed class CancelAfter(CancellationTokenSource source, int calls) : IGamePolicy
    {
        private int _n;

        public string Id => "cancel-after-test";

        public PaDecision AtPlateAppearance(GameState state)
        {
            if (++_n >= calls / 4)
                source.Cancel();
            return PaDecision.Choose(ApproachKind.Normal);
        }

        public PitchIntent IntentFor(GameState state) => PitchIntent.Balanced;
    }

    // ---------------------------------------------------------------- inputs and pins

    [Fact]
    public void A_model_and_tuning_that_do_not_belong_together_are_refused_by_the_data_and_by_the_engine()
    {
        var text = HarnessData.ReadResource(HarnessData.TuningResource).Replace("\"model_version\": \"prob-model-v4\"", "\"model_version\": \"prob-model-v3\"", StringComparison.Ordinal);
        var mismatched = HarnessData.ParseTuning(text);
        Assert.False(mismatched.IsSuccess);
        Assert.Equal("Tuning.model_tuning_mismatch", mismatched.Error!.Value.Code);

        // the engine refuses a game execution contract that pairs the v4 model with another tuning version
        var good = H.Config();
        var bad = good.Execution with { TuningVersion = "tuning-virtual-proto-v3" };
        var begun = GameStarter.Begin(new GameStart("game:sim:x-000001", good.Rules, good.Cohort.Away, good.Cohort.Home, bad), 1);
        Assert.Equal("Game.execution_tuning_unsupported", begun.Error!.Value.Code);

        // and the harness refuses to run a configuration whose pins disagree
        var pinned = good with { Execution = bad };
        var result = GameExecutor.Play(pinned, pinned.Spec(1, 3));
        Assert.Equal("Harness.pin_tuning_mismatch", result.Failure!.Code);
        Assert.Equal(0, result.CommittedEvents);
    }

    [Fact]
    public void Only_the_active_model_with_a_version_3_input_set_can_be_configured()
    {
        var cohort = NeutralCohort.Create();
        Assert.Equal(PlayerInputSet.ContentV3, cohort.InputSet.ContentVersion);
        Assert.Equal(cohort.InputSet.Id, H.Config().Execution.PlayerInputsId);

        // the real, valid tuning-virtual-proto-v3 file of the repository: a good profile of ANOTHER model is not runnable here
        var v3 = H.Ok(HarnessData.ParseTuning(File.ReadAllText(Path.Combine(H.Root, "content", "tuning", "virtual_tuning_v3.json"))));
        Assert.Equal("prob-model-v3", v3.Model.Version.Value);
        Assert.Equal("Harness.model_unsupported", RunConfig.Create("r", "m", cohort, H.Rules(), v3, H.Park()).Error!.Value.Code);
        Assert.Equal("Harness.event_budget_invalid", RunConfig.Create("r", "m", cohort, H.Rules(), H.Tuning, H.Park(), eventBudget: 0).Error!.Value.Code);
        Assert.Equal("Harness.run_id_invalid", RunConfig.Create("bad id!", "m", cohort, H.Rules(), H.Tuning, H.Park()).Error!.Value.Code);
        Assert.Equal("Harness.config_incomplete", RunConfig.Create("r", "m", cohort, null, H.Tuning, H.Park()).Error!.Value.Code);
    }

    [Fact]
    public void A_different_player_input_set_is_refused_by_the_harness_pin_and_by_the_engine()
    {
        var config = H.Config();
        var other = H.Variant("other", s => s.Id.Value == "sim-a-b1" ? H.WithRatings(s, new HitterRatings(H.Skill(50), H.Skill(50), H.Skill(50), H.Skill(51), H.Skill(50), H.Skill(50)), null, null) : null);
        Assert.NotEqual(config.Cohort.InputSet.Id, other.InputSet.Id);

        var result = GameExecutor.Play(config with { Cohort = other }, config.Spec(1, 3));
        Assert.Equal("Harness.pin_input_set_mismatch", result.Failure!.Code);
        Assert.Equal(0, result.CommittedEvents);

        // the engine itself: a game that pinned set A refuses to prepare a pitch with set B
        var spec = config.Spec(1, 3);
        var cursor = H.Ok(GameStarter.Begin(new GameStart(spec.GameId, config.Rules, config.Cohort.Away, config.Cohort.Home, config.Execution), spec.Seed));
        var approach = H.Ok(CommitGate.Stage(cursor.Current, new ApproachEvent($"{spec.GameId}:e000001", ApproachKind.Normal)));
        H.Ok(cursor.Publish(approach));
        var state = cursor.Current.State;
        var batter = config.Cohort.Sheets[state.Batting.CurrentBatterSlot.Player];
        var pitcher = config.Cohort.Sheets[state.Fielding.Pitcher];
        var refused = PitchPreparation.Prepare(state, cursor.Current.Rng, config.Tuning, PitchIntent.Balanced, false, batter, pitcher, default, null, other.InputSet);
        Assert.False(refused.IsSuccess);
        var withNone = PitchPreparation.Prepare(state, cursor.Current.Rng, config.Tuning, PitchIntent.Balanced, false, batter, pitcher, default, null, null);
        Assert.False(withNone.IsSuccess);
    }

    [Fact]
    public void A_missing_catcher_or_pitcher_rating_fails_the_game_with_the_engines_code_and_nothing_is_defaulted()
    {
        // the catcher of the away team's DEFENSE is the home catcher (sim-h-b9); remove his catcher tools
        var noCatcher = H.Variant("no-catcher", s => s.Id.Value == "sim-h-b9" ? H.WithRatings(s, s.Ratings.Hitter, null, null) : null);
        var a = H.Play(H.Config("nc", noCatcher), 3);
        Assert.Equal(GameRunStatus.Failed, a.Status);
        Assert.StartsWith("PitchPrepare.", a.Failure!.Code);
        Assert.Contains("catcher", a.Failure.Code, StringComparison.Ordinal);
        Assert.Equal((3UL, "game:sim:no-catcher-000001"), (a.Game.Seed, a.Game.GameId));

        // the home starter has no pitching ratings
        var noPitcher = H.Variant("no-pitcher", s => s.Id.Value == "sim-h-sp" ? H.AsHitterOnly(s) : null);
        var b = H.Play(H.Config("np", noPitcher), 3);
        Assert.Equal(GameRunStatus.Failed, b.Status);
        Assert.Equal("PitchAbilities.pitcher_pitching_missing", b.Failure!.Code);
        Assert.Equal("game:sim:no-pitcher-000001", b.Game.GameId);
    }

    [Fact]
    public void A_cohort_with_an_unrecorded_or_missing_player_is_refused()
    {
        var baseline = NeutralCohort.Create();
        Assert.Equal("Harness.cohort_sheet_missing", CohortInputs.Create("c", CohortKind.VirtualLeague, "d", baseline.Away, baseline.Home, baseline.Sheets.Values.Skip(1)).Error!.Value.Code);
        var extra = H.WithRatings(baseline.Sheets.Values.First(), baseline.Sheets.Values.First().Ratings.Hitter, null, null);
        var stranger = StarPlayer.Domain.Players.PlayerSheet.Create(
            H.Ok(Player.Create(H.Ok(StarPlayer.Domain.Common.PlayerId.Create("someone-else")), "남", new DateOnly(2000, 1, 1), ThrowingHand.Right, BattingSide.Right, H.Ok(BodyMeasurements.Create(180, 80)), [], "")),
            extra.Ratings, extra.State, extra.Proficiency, extra.Status);
        Assert.Equal("Harness.cohort_sheet_unused", CohortInputs.Create("c", CohortKind.VirtualLeague, "d", baseline.Away, baseline.Home, baseline.Sheets.Values.Append(H.Ok(stranger))).Error!.Value.Code);
        Assert.Equal("Harness.cohort_id_invalid", CohortInputs.Create("bad id", CohortKind.Neutral, "d", baseline.Away, baseline.Home, baseline.Sheets.Values).Error!.Value.Code);
    }

    // ---------------------------------------------------------------- policy and legality

    [Fact]
    public void An_illegal_policy_choice_is_refused_before_anything_is_committed()
    {
        var config = H.Config("illegal", policy: new FixedPolicy(new PaDecision(false, (ApproachKind)99)));
        var result = H.Play(config, 3);

        Assert.Equal(GameRunStatus.Failed, result.Status);
        Assert.Equal(0, result.CommittedEvents);                                    // the engine rejected the approach; no state, id or RNG moved
        Assert.False(string.IsNullOrEmpty(result.Failure!.Code));
    }

    [Fact]
    public void An_explicit_take_each_plate_appearance_is_legal_consumes_one_real_pitch_and_returns_to_a_decision()
    {
        var config = H.Config("take", policy: new TakeThenApproach());
        var result = H.Play(config, 11);

        Assert.Equal(GameRunStatus.Completed, result.Status);
        Assert.True(result.Counters.Takes > 50);                                    // about one explicit Take per plate appearance
        Assert.True(result.Counters.Takes <= result.Counters.PlateAppearances + 5);
        Assert.True(result.CrossCheck!.Passed);
    }

    [Fact]
    public void An_exception_inside_a_game_is_a_failed_game_with_its_reproduction_data_and_the_run_goes_on()
    {
        var config = H.Config("throwing", policy: new ThrowingPolicy());
        var run = SimRunner.Run(config, [3, 11]);

        Assert.Equal((0, 2), (run.CompletedCount, run.FailedCount));                  // the second game is still attempted
        Assert.All(run.Games, g =>
        {
            Assert.Equal("Harness.unhandled_exception", g.Failure!.Code);
            Assert.Contains("InvalidOperationException", g.Failure.Message, StringComparison.Ordinal);
            Assert.Equal("exception", g.Failure.Stage);
            Assert.True(g.CommittedEvents > 0);
        });
        Assert.Equal(new[] { 3UL, 11UL }, run.Games.Select(g => g.Game.Seed).ToArray());
    }

    private sealed class ThrowingPolicy : IGamePolicy
    {
        public string Id => "throwing-test";

        // a pure function of the state, like any policy: it fails from the second inning on
        public PaDecision AtPlateAppearance(GameState state) => state.Inning >= 2 ? throw new InvalidOperationException("policy failure") : PaDecision.Choose(ApproachKind.Normal);

        public PitchIntent IntentFor(GameState state) => PitchIntent.Balanced;
    }

    [Fact]
    public void A_cohort_id_too_long_to_make_a_game_id_is_refused_when_the_run_is_configured()
    {
        var baseline = NeutralCohort.Create();
        var longId = H.Ok(CohortInputs.Create(new string('c', 60), CohortKind.VirtualLeague, "d", baseline.Away, baseline.Home, baseline.Sheets.Values));
        Assert.Equal("Id.game_too_long", RunConfig.Create("r", "m", longId, H.Rules(), H.Tuning, H.Park()).Error!.Value.Code);
    }

    private sealed class FixedPolicy(PaDecision decision) : IGamePolicy
    {
        public string Id => "fixed-test";

        public PaDecision AtPlateAppearance(GameState state) => decision;

        public PitchIntent IntentFor(GameState state) => PitchIntent.Balanced;
    }

    private sealed class TakeThenApproach : IGamePolicy
    {
        public string Id => "take-then-approach-test";

        // a new plate appearance: watch one pitch; back at a decision point after it: swing away with a normal approach
        public PaDecision AtPlateAppearance(GameState state) => state.Stage == PaStage.Start ? PaDecision.TakeOne : PaDecision.Choose(ApproachKind.Normal);

        public PitchIntent IntentFor(GameState state) => PitchIntent.Balanced;
    }

    [Fact]
    public void The_default_policy_is_a_pure_function_of_the_public_state_and_reads_no_rating()
    {
        var policy = new NormalBalancedPolicy();
        var config = H.Config();
        var cursor = H.Ok(GameStarter.Begin(new GameStart("game:sim:p-000001", config.Rules, config.Cohort.Away, config.Cohort.Home, config.Execution), 1));
        var state = cursor.Current.State;

        Assert.Equal(PaDecision.Choose(ApproachKind.Normal), policy.AtPlateAppearance(state));
        Assert.Equal(PaDecision.Choose(ApproachKind.Normal), policy.AtPlateAppearance(state));
        Assert.Equal(PitchIntent.Balanced, policy.IntentFor(state));
        Assert.Equal("normal-balanced-v1", policy.Id);
        // it takes the game state only: no sheet, rating or input set can reach it through the interface
        Assert.All(typeof(IGamePolicy).GetMethods(), m => Assert.All(m.GetParameters(), p => Assert.Equal(typeof(GameState), p.ParameterType)));
    }

    // ---------------------------------------------------------------- authority

    [Fact]
    public void Only_a_new_game_can_be_begun_and_its_state_is_made_by_the_reducer()
    {
        var config = H.Config();
        Assert.Equal("Game.start_invalid", GameStarter.Begin(null, 1).Error!.Value.Code);
        Assert.Equal("Game.start_invalid", GameStarter.Begin(new GameStart(" bad ", config.Rules, config.Cohort.Away, config.Cohort.Home, config.Execution), 1).Error!.Value.Code);
        Assert.Equal("Rng.context_invalid", GameStarter.Begin(new GameStart("bad id with spaces", config.Rules, config.Cohort.Away, config.Cohort.Home, config.Execution), 1).Error!.Value.Code);
        var lineupBroken = config.Cohort.Away with { Lineup = config.Cohort.Away.Lineup.Take(8).ToList() };
        Assert.Equal("Game.lineup_invalid", GameStarter.Begin(new GameStart("game:sim:x-000001", config.Rules, lineupBroken, config.Cohort.Home, config.Execution), 1).Error!.Value.Code);

        var a = H.Ok(GameStarter.Begin(new GameStart("game:sim:x-000001", config.Rules, config.Cohort.Away, config.Cohort.Home, config.Execution), 5));
        var b = H.Ok(GameStarter.Begin(new GameStart("game:sim:x-000001", config.Rules, config.Cohort.Away, config.Cohort.Home, config.Execution), 5));
        Assert.NotSame(a, b);                                                       // one cursor per game
        Assert.Equal(a.Current.Rng.StateHash, b.Current.Rng.StateHash);
        Assert.Null(a.Current.LastCommit);
    }

    [Fact]
    public void A_stale_or_repeated_commit_is_never_counted()
    {
        var config = H.Config();
        var spec = config.Spec(1, 3);
        var cursor = H.Ok(GameStarter.Begin(new GameStart(spec.GameId, config.Rules, config.Cohort.Away, config.Cohort.Home, config.Execution), spec.Seed));
        var first = H.Ok(CommitGate.Stage(cursor.Current, new ApproachEvent($"{spec.GameId}:e000001", ApproachKind.Normal)));
        var rival = H.Ok(CommitGate.Stage(cursor.Current, new ApproachEvent($"{spec.GameId}:e000001", ApproachKind.Patient)));
        var recorder = new MetricsRecorder();

        var published = H.Ok(cursor.Publish(first));
        recorder.Observe(published);
        var stale = cursor.Publish(rival);                                          // the compare-and-swap refuses it: there is no CommitRecord to count
        Assert.False(stale.IsSuccess);
        Assert.Equal("Commit.revision_stale", stale.Error!.Value.Code);
        Assert.Equal(ObserveOutcome.DuplicateIgnored, recorder.Observe(published));  // the same confirmed record again
        Assert.Equal(1, recorder.Counters.Events);
        Assert.Equal(1, recorder.Counters.DuplicatesIgnored);
        Assert.Equal(1, cursor.Current.LastEventSequence);
    }

    [Fact]
    public void Every_event_of_a_finished_game_was_counted_once_and_a_skipped_sequence_is_reported()
    {
        var recorder = new MetricsRecorder();
        var ball = new PitchEvent("e1", new PitchSpec(PitchOutcome.Ball));
        Assert.Equal(ObserveOutcome.Counted, recorder.Observe(ball, 1, []));
        Assert.Equal(ObserveOutcome.SequenceGap, recorder.Observe(ball, 3, []));
        Assert.Equal(1, recorder.Counters.Events);
        Assert.Equal(ObserveOutcome.Counted, recorder.Observe(ball, 2, []));
    }
}
