using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// F-C2-B B-3: ADR-008 stratified metrics and the approved evaluation policy (FC-D1, FC-D2, FC-D6, FC-D7, FC-D9) on stop-aware results (ADR-009 SR-D9). Synthetic games carry exact counters, so
/// every boundary is hit exactly; nothing here is an official calibration, and no verdict below is a result of the engine.
/// </summary>
public class StratifiedEvaluationTests
{
    private static readonly EvaluationPolicy Policy = EvaluationPolicy.FcEvaluationV1;

    /// <summary>In-range counters of one game (F1 0.20, F2 0.08, F3 0.009, F4 0.025, F5 0.295, F6 0.55, F7 0.48, F8 0.24), scaled by <paramref name="scale"/>.</summary>
    private static MetricCounters Good(long scale = 1, long so = 2_000) => new()
    {
        PlateAppearances = 10_000 * scale, Strikeouts = so * scale, Walks = 800 * scale, HitByPitch = 90 * scale, HomeRuns = 250 * scale, AtBats = 9_000 * scale, Hits = 2_256 * scale,
        SacrificeFlies = 50 * scale, Pitches = 30_000 * scale, LocatedPitches = 30_000 * scale, ZonePitches = 16_500 * scale, HitByPitchPitches = 90 * scale, Swings = 14_400 * scale,
        SwingingStrikes = 3_456 * scale,
    };

    private static RunConfig Config(string cohort, bool automated, string key) =>
        H.Ok(RunConfig.Create("eval", "official", H.Ok(CohortRegistry.Resolve(cohort)), H.Rules(automated), H.Tuning, H.Park(), "advancement-policy-v2", 3000, competitionKey: key));

    private static GameRunResult Game(GameSpec spec, MetricCounters c, GameRunStatus status = GameRunStatus.Completed)
    {
        var g = StopReplayTests.Fake(spec, c.PlateAppearances, status);
        return g with { Counters = c };
    }

    private static RunResult Run(Func<int, MetricCounters> counters, string cohort = CalibrationCohorts.NeutralMixedId, bool automated = true, string family = "fc3-cal", int games = 14, long target = 100_000,
        Func<int, GameRunStatus>? status = null, Func<Error?>? integrity = null, RunConfig? config = null)
    {
        var c = config ?? Config(cohort, automated, family == "fc3-cal" ? "fc3-cal-x" : "fc2-dry-x");
        var plan = new StopPlan(family, 7, target, Enumerable.Range(1, games).Select(i => c.Spec(i, (ulong)i)).ToList(), "seeds/x.json", new string('a', 64));
        return SimRunner.RunStopAware(c, plan, integrity: integrity, play: s => Game(s, counters(s.GameIndex), status?.Invoke(s.GameIndex) ?? GameRunStatus.Completed));
    }

    private static CalibrationEvaluation Eval(RunResult run, EvaluationPolicy? policy = null) => H.Ok(StratifiedEvaluator.Evaluate(run, policy ?? Policy));

    private static StratifiedMetric M(CalibrationEvaluation e, string f) => e.Metrics.Single(m => m.FId == f);

    // ---------------------------------------------------------------- the approved policy

    [Fact]
    public void The_approved_policy_is_exactly_ADR_008_and_its_minimums_follow_from_the_tuning_targets()
    {
        Assert.Equal(("fc-evaluation-policy-v1", true, 100_000L), (Policy.Id, Policy.IsApproved, Policy.StratumMinPa));
        Assert.False(EvaluationPolicy.Unapproved.IsApproved);
        // FC-D6.2 recomputed independently from the tuning file: ceil(DEFF z^2 p(1-p) / (k h)^2), ceiling once
        foreach (var d in MetricsEvaluator.Definitions)
        {
            var t = H.Tuning.Targets.Single(x => x.Id == d.TargetId);
            double p = (t.LowPpm + t.HighPpm) / 2.0 / 1e6, h = (t.HighPpm - t.LowPpm) / 2.0 / 1e6, z = 1.959964;
            Assert.Equal((long)Math.Ceiling(2 * z * z * p * (1 - p) / (0.25 * h * 0.25 * h)), Policy.MinDenominatorByTarget[d.TargetId]);
        }

        Assert.Equal(new long[] { 8_158, 15_297, 38_239, 29_964, 40_906, 3_043, 4_795, 6_229 }, MetricsEvaluator.Definitions.Select(d => Policy.MinDenominatorByTarget[d.TargetId]).ToArray());
        foreach (var (f, target) in new[] { ("F1", "pa_strikeout_rate"), ("F8", "swing_miss_rate") })
            Assert.Equal(((long)Math.Ceiling(3.0 / 7 * Policy.MinDenominatorByTarget[target]), (long)Math.Ceiling(4.0 / 7 * Policy.MinDenominatorByTarget[target])), Policy.StratumMinimums[f]);
        Assert.Equal(((3_497L, 4_662L), (2_670L, 3_560L)), (Policy.StratumMinimums["F1"], Policy.StratumMinimums["F8"]));
        Assert.Equal(new[] { "F1", "F8" }, Policy.StandardizedMetrics.Order().ToArray());
        Assert.All(new[] { "F1", "F6", "F7", "F8" }, f => Assert.Equal("neutral-mixed-v1", Policy.OfficialCohortByMetric[f]));
        Assert.All(new[] { "F2", "F3", "F4", "F5" }, f => Assert.Equal("virtual-league-v1", Policy.OfficialCohortByMetric[f]));
        Assert.Equal(Policy.Hash(), EvaluationPolicy.FcEvaluationV1.Hash());
        Assert.NotEqual(Policy.Hash(), (Policy with { StratumMinPa = 99_999 }).Hash());
    }

    [Fact]
    public void The_standardized_ppm_is_the_approved_integer_formula_with_half_up_and_no_intermediate_rounding()
    {
        Assert.Equal(242_857, StratifiedEvaluator.StandardizedPpm(30, 100, 20, 100));          // 3/7 · 0.3 + 4/7 · 0.2 = 0.2428571…
        Assert.Equal(428_571, StratifiedEvaluator.StandardizedPpm(1, 1, 0, 1));                 // 428,571.43
        Assert.Equal(571_429, StratifiedEvaluator.StandardizedPpm(0, 1, 1, 1));                 // 571,428.57 (half-up)
        Assert.Equal(250_000, StratifiedEvaluator.StandardizedPpm(1, 4, 25, 100));              // equal stratum rates give the plain rate whatever the shares
        Assert.Equal(500_000, StratifiedEvaluator.StandardizedPpm(500_000_000, 1_000_000_000, 500_000_000, 1_000_000_000));   // wide integers, no overflow
        // no intermediate rounding: rounding each stratum first would give 333,333 / 666,667 → 523,810; the exact value is 523,809.52
        Assert.Equal(523_810, StratifiedEvaluator.StandardizedPpm(1, 3, 2, 3));
        Assert.Equal(523_809, StratifiedEvaluator.StandardizedPpm(333_333, 1_000_000, 666_666, 1_000_000));
        var random = new Random(20261018);
        for (var i = 0; i < 2_000; i++)
        {
            long ds = random.Next(1, 200_000), dO = random.Next(1, 200_000), ns = random.NextInt64(0, ds + 1), no = random.NextInt64(0, dO + 1);
            var exact = 1e6 * (3.0 * ns / ds + 4.0 * no / dO) / 7;
            Assert.True(Math.Abs(StratifiedEvaluator.StandardizedPpm(ns, ds, no, dO) - exact) <= 0.5 + 1e-6, $"{ns}/{ds} {no}/{dO}");
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => StratifiedEvaluator.StandardizedPpm(1, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => StratifiedEvaluator.StandardizedPpm(2, 1, 1, 1));
    }

    // ---------------------------------------------------------------- one stratum

    [Fact]
    public void A_stratum_whose_official_metrics_all_hold_passes_and_its_evaluation_is_traceable()
    {
        var run = Run(_ => Good());
        var e = Eval(run);
        Assert.Equal((GateStatus.Pass, "L1", true), (e.Gate, e.StratumId, e.Official));
        Assert.Equal(new[] { "F1", "F6", "F7", "F8" }, e.Metrics.Where(m => m.Official).Select(m => m.FId).ToArray());
        Assert.All(e.Metrics.Where(m => !m.Official), m => Assert.Equal(GateStatus.NotEvaluated, m.Status));     // diagnostics on this stratum
        Assert.Equal(("standardized_3_7_4_7", 200_000L), (M(e, "F1").Method, M(e, "F1").MeasuredPpm!.Value));
        Assert.Equal(("observed_aggregate", 550_000L), (M(e, "F6").Method, M(e, "F6").MeasuredPpm!.Value));
        Assert.Equal((6, 8, 0), (e.Shares.SameGames, e.Shares.OppositeGames, e.Shares.MixedGames));                // games 1-3, 8-10 same; the others opposite
        Assert.Equal((60_000L, 80_000L), (M(e, "F1").DenominatorSame, M(e, "F1").DenominatorOpposite));
        Assert.Equal((run.ConfigHash, run.RunResultHash, run.Config.Cohort.InputsHash, Policy.Id, Policy.Hash(), run.Stop), (e.ConfigHash, e.RunResultHash, e.CohortInputsHash, e.PolicyId, e.PolicyHash, e.Stop));
        // deterministic: the same run evaluates to the same document
        Assert.Equal(JsonSerializer.Serialize(e), JsonSerializer.Serialize(Eval(Run(_ => Good()))));

        var league = Eval(Run(_ => Good(), CalibrationCohorts.VirtualLeagueId, automated: false));
        Assert.Equal((GateStatus.Pass, "L4"), (league.Gate, league.StratumId));
        Assert.Equal(new[] { "F2", "F3", "F4", "F5" }, league.Metrics.Where(m => m.Official).Select(m => m.FId).ToArray());
        Assert.Equal(("observed_aggregate", 295_000L), (M(league, "F5").Method, M(league, "F5").MeasuredPpm!.Value));
    }

    [Fact]
    public void A_metric_outside_its_interval_fails_the_stratum_and_the_bounds_are_inclusive()
    {
        var high = Eval(Run(_ => Good(so: 3_000)));                                                    // F1 0.30 > 0.26
        Assert.Equal((GateStatus.Fail, GateStatus.Fail), (high.Gate, M(high, "F1").Status));
        var edge = Eval(Run(_ => Good(so: 2_600)));                                                    // exactly 260,000 ppm: inside
        Assert.Equal((260_000L, GateStatus.Pass, GateStatus.Pass), (M(edge, "F1").MeasuredPpm!.Value, M(edge, "F1").Status, edge.Gate));
        // the standardized estimate, not the observed aggregate, decides: same games at 0.12 and opposite games (twice the PA) at 0.30 give 3/7·0.12 + 4/7·0.30 = 0.222857 (PASS),
        // while the PA-weighted aggregate is (3·1,200 + 4·6,000) / (3·10,000 + 4·20,000) = 0.250909 - the observed share is not 3/7 and does not move the weights (FC-D1.5)
        var mixed = Eval(Run(g => (g - 1) % 7 < 3 ? Good(so: 1_200) : Good(2, so: 3_000)));
        Assert.Equal((7, 222_857L, 250_909L), (mixed.Stop!.ExecutedGames, M(mixed, "F1").MeasuredPpm!.Value, M(mixed, "F1").Diagnostics.ObservedPpm!.Value));
        Assert.Equal(GateStatus.Pass, M(mixed, "F1").Status);
    }

    [Fact]
    public void F1_and_F8_need_both_the_total_and_each_stratum_minimum()
    {
        // same-hand games carry 5% of the counters: PA_S = 6 × 500 = 3,000 < 3,497 although the total is far above 8,158
        var e = Eval(Run(g => (g - 1) % 7 < 3 ? Scaled(Good(), 20) : Good(2)));
        Assert.Equal((3_000L, GateStatus.InsufficientSample), (M(e, "F1").DenominatorSame, M(e, "F1").Status));
        Assert.Equal((4_320L, GateStatus.Pass), (M(e, "F8").DenominatorSame, M(e, "F8").Status));      // F8 has its own minimums (Sw_S 4,320 >= 2,670) and meets them
        Assert.Equal(GateStatus.InsufficientSample, e.Gate);
        Assert.Equal(GateStatus.Pass, M(e, "F6").Status);                                              // an observed metric only needs its total

        // the opposite stratum short of swings: Sw_O = 4 × 360 = 1,440 < 3,560 while the total is far above 6,229
        var o = Eval(Run(g => (g - 1) % 7 < 3 ? Good(4) : Scaled(Good(), 40)));
        Assert.Equal((7, 1_440L, GateStatus.InsufficientSample), (o.Stop!.ExecutedGames, M(o, "F8").DenominatorOpposite, M(o, "F8").Status));
        Assert.True(M(o, "F8").Denominator >= 6_229);
    }

    private static MetricCounters Scaled(MetricCounters c, long divisor)
    {
        var copy = new MetricCounters();
        foreach (var p in typeof(MetricCounters).GetProperties())
            p.SetValue(copy, (long)p.GetValue(c)! / divisor);
        return copy;
    }

    [Fact]
    public void Failure_cancellation_exhaustion_and_metric_errors_take_the_documented_gate_branch()
    {
        var failed = Run(_ => Good(), status: g => g == 2 ? GameRunStatus.Failed : GameRunStatus.Completed);
        Assert.Equal(GateStatus.Fail, Eval(failed).Gate);                                              // FC-D9.1, even though every metric holds on the completed games
        Assert.All(Eval(failed).Metrics.Where(m => m.Official), m => Assert.Equal(GateStatus.Pass, m.Status));

        Assert.Equal(GateStatus.NotEvaluated, Eval(Run(_ => Good(), status: g => g == 2 ? GameRunStatus.Cancelled : GameRunStatus.Completed)).Gate);
        Assert.Equal(GateStatus.NotEvaluated, Eval(Run(_ => Good(), integrity: () => new Error("Seed.file_hash_mismatch", "x"))).Gate);

        var exhausted = Eval(Run(_ => Scaled(Good(), 10)));                                             // 14 × 1,000 PA: the plan runs out first
        Assert.Equal((StopReason.SeedsExhausted, GateStatus.InsufficientSample), (exhausted.Stop!.Reason, exhausted.Gate));

        var aboveFloor = Eval(Run(_ => Good(), target: 200_000));                                       // exhausted with 140,000 valid PA: above the floor, every metric holds, still not PASS
        Assert.Equal((StopReason.SeedsExhausted, 140_000L, GateStatus.InsufficientSample), (aboveFloor.Stop!.Reason, aboveFloor.ValidPa, aboveFloor.Gate));
        Assert.All(aboveFloor.Metrics.Where(m => m.Official), m => Assert.Equal(GateStatus.Pass, m.Status));

        var belowFloor = Eval(Run(_ => Scaled(Good(), 10), target: 5_000));                             // a (synthetic) target below the FC-D7.1 floor never passes
        Assert.Equal((StopReason.TargetReached, GateStatus.InsufficientSample), (belowFloor.Stop!.Reason, belowFloor.Gate));

        var noTarget = H.Ok(HarnessData.ParseTuning(HarnessData.ReadResource(HarnessData.TuningResource).Replace("\"id\": \"babip\"", "\"id\": \"babip_renamed\"", StringComparison.Ordinal)));
        var broken = H.Ok(RunConfig.Create("eval", "official", CalibrationCohorts.NeutralMixed(), H.Rules(true), noTarget, H.Park(), "advancement-policy-v2", 3000, competitionKey: "fc3-cal-x"));
        Assert.Equal("Metrics.target_missing", StratifiedEvaluator.Evaluate(Run(_ => Good(), config: broken), Policy).Error!.Value.Code);

        var inconsistent = Run(_ => new MetricCounters { PlateAppearances = 10_000, Strikeouts = 10_001 });
        Assert.Equal("Metrics.counts_inconsistent", StratifiedEvaluator.Evaluate(inconsistent, Policy).Error!.Value.Code);
    }

    [Fact]
    public void Dry_runs_unapproved_policies_and_non_calibration_cohorts_are_never_a_gate()
    {
        var dry = Eval(Run(_ => Good(), family: "fc2-dry"));
        Assert.Equal((GateStatus.NotEvaluated, false), (dry.Gate, dry.Official));
        Assert.Equal(GateStatus.Pass, M(dry, "F1").Status);                                            // the diagnostic metric statuses are still computed
        Assert.Equal(GateStatus.NotEvaluated, Eval(Run(_ => Good()), EvaluationPolicy.Unapproved).Gate);
        var neutralV1 = Eval(Run(_ => Good(), "neutral-v1"));
        Assert.Equal(((string?)null, GateStatus.NotEvaluated), (neutralV1.StratumId, neutralV1.Gate));
        Assert.All(neutralV1.Metrics, m => Assert.Equal(GateStatus.NotEvaluated, m.Status));

        // a full-execution run is not a calibration stratum run
        var config = Config(CalibrationCohorts.NeutralMixedId, true, "sim");
        var specs = Enumerable.Range(1, 3).Select(i => config.Spec(i, (ulong)i)).ToList();
        var legacy = new RunResult(config, specs, specs.Select(s => Game(s, Good())).ToList(), Good(3), MetricsEvaluator.Evaluate(Good(3), config.Cohort, H.Tuning, Policy), Policy, "c", "h");
        Assert.Equal(GateStatus.NotEvaluated, Eval(legacy).Gate);
        // the F-A/B evaluator never gives an official verdict to a standardized metric
        Assert.All(H.Ok(legacy.Metrics).Metrics.Where(m => m.FId is "F1" or "F8"), m => Assert.Equal(MetricStatus.NotEvaluated, m.Status));
    }

    [Fact]
    public void The_four_strata_are_judged_together_without_pooling_or_compensation()
    {
        CalibrationEvaluation Stratum(string cohort, bool automated, long so = 2_000, string? keySuffix = null) =>
            Eval(Run(_ => Good(so: so), cohort, automated, config: Config(cohort, automated, "fc3-cal-" + (keySuffix ?? (cohort[..2] + (automated ? "a" : "n"))))));
        var l1 = Stratum(CalibrationCohorts.NeutralMixedId, true);
        var l2 = Stratum(CalibrationCohorts.NeutralMixedId, false);
        var l3 = Stratum(CalibrationCohorts.VirtualLeagueId, true);
        var l4 = Stratum(CalibrationCohorts.VirtualLeagueId, false);
        Assert.Equal(new[] { "L1", "L2", "L3", "L4" }, new[] { l1, l2, l3, l4 }.Select(s => s.StratumId).ToArray());
        Assert.Equal(GateStatus.Pass, StratifiedEvaluator.CombineStrata([l1, l2, l3, l4]).Gate);

        var l2Fail = Stratum(CalibrationCohorts.NeutralMixedId, false, so: 3_000);
        Assert.Equal(GateStatus.Fail, StratifiedEvaluator.CombineStrata([l1, l2Fail, l3, l4]).Gate);   // three passing strata never compensate
        Assert.Equal(GateStatus.NotEvaluated, StratifiedEvaluator.CombineStrata([l1, l2, l3]).Gate);
        Assert.Equal(GateStatus.NotEvaluated, StratifiedEvaluator.CombineStrata([l1, l2, l3, l4, Stratum(CalibrationCohorts.NeutralMixedId, true, keySuffix: "na2")]).Gate);
        Assert.Equal(GateStatus.NotEvaluated, StratifiedEvaluator.CombineStrata([l1, l2, l3, Eval(Run(_ => Good(), CalibrationCohorts.VirtualLeagueId, false, family: "fc2-dry"))]).Gate);
        var exhausted = Eval(Run(_ => Scaled(Good(), 10), config: Config(CalibrationCohorts.VirtualLeagueId, true, "fc3-cal-va")));
        Assert.Equal(GateStatus.InsufficientSample, StratifiedEvaluator.CombineStrata([l1, l2, exhausted, l4]).Gate);
    }

    // ---------------------------------------------------------------- FC2B-03: a metrics error in a stratum run (ADR-009 SR-D9.2 priority) and the combination

    private static MetricCounters Impossible() => new() { PlateAppearances = 10_000, Strikeouts = 10_001 };   // a numerator above its denominator: Metrics.counts_inconsistent

    /// <summary>The gate a run's summary.json reports (the path a metrics error takes: <c>Evaluate</c> returns an error, not an evaluation).</summary>
    private static (string Status, string? Reason, string? Code) Reported(RunResult run)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(RunReport.Summary(run), RunReport.Json))!["calibration_evaluation"]!;
        return (node["gate_status"]!.GetValue<string>(), node["gate_reason"]?.GetValue<string>(), node["error_code"]?.GetValue<string>());
    }

    [Fact]
    public void A_metrics_error_is_not_evaluated_but_never_hides_a_failed_game_and_never_passes()
    {
        // SR-D9.2 rows: terminated by the rule + failed game -> FAIL; terminated by the rule + metrics could not be computed -> NOT_EVALUATED; cancelled / aborted -> NOT_EVALUATED
        var metricsOnly = Run(_ => Impossible());
        Assert.Equal("Metrics.counts_inconsistent", StratifiedEvaluator.Evaluate(metricsOnly, Policy).Error!.Value.Code);
        var (status, reason, code) = Reported(metricsOnly);
        Assert.Equal(("NOT_EVALUATED", "Metrics.counts_inconsistent"), (status, code));                    // the error is reported, not turned into a verdict
        Assert.Contains("Metrics.counts_inconsistent", reason);

        var failedToo = Run(_ => Impossible(), status: g => g == 2 ? GameRunStatus.Failed : GameRunStatus.Completed);
        Assert.Equal("FAIL", Reported(failedToo).Status);                                                  // the failed game is FAIL whatever the metrics did (FC-D9.1, SR-D3.3); it was reported NOT_EVALUATED
        Assert.Contains("Metrics.counts_inconsistent", Reported(failedToo).Reason);                        // and the metrics error is still named

        Assert.Equal("NOT_EVALUATED", Reported(Run(_ => Impossible(), status: g => g == 2 ? GameRunStatus.Cancelled : GameRunStatus.Completed)).Status);   // SR-D3.5 keeps its contract
        Assert.Equal("NOT_EVALUATED", Reported(Run(_ => Impossible(), integrity: () => new Error("Seed.file_hash_mismatch", "x"))).Status);                 // SR-D3.4 keeps its contract
        Assert.Equal("NOT_EVALUATED", Reported(Run(_ => Impossible(), status: g => g == 2 ? GameRunStatus.Failed : GameRunStatus.Completed, family: "fc2-dry")).Status);   // a dry run is never a gate

        // the metrics error takes precedence over a sample shortage (the shortage is a verdict of a working computation)
        var exhausted = Run(_ => new MetricCounters { PlateAppearances = 1_000, Strikeouts = 1_001 });
        Assert.Equal((StopReason.SeedsExhausted, "NOT_EVALUATED"), (exhausted.Stop!.Reason, Reported(exhausted).Status));

        // the library gate itself, with a metric report that failed although the stratified counts are fine: never PASS, and a failed game still FAILs first
        var failure = Result<MetricsReport>.Failure(new Error("Metrics.target_missing", "x"));
        var good = Run(_ => Good());
        Assert.Equal(GateStatus.Pass, Eval(good).Gate);
        var broken = Eval(good with { Metrics = failure });
        Assert.Equal(GateStatus.NotEvaluated, broken.Gate);
        Assert.Contains("Metrics.target_missing", broken.GateReason);
        var failed = Run(_ => Good(), status: g => g == 2 ? GameRunStatus.Failed : GameRunStatus.Completed);
        Assert.Equal(GateStatus.Fail, Eval(failed with { Metrics = failure }).Gate);
        Assert.Equal(GateStatus.NotEvaluated, Eval(Run(_ => Good(), status: g => g == 2 ? GameRunStatus.Cancelled : GameRunStatus.Completed) with { Metrics = failure }).Gate);
    }

    [Fact]
    public void The_combination_never_hides_a_failed_stratum_or_a_stratum_that_could_not_be_evaluated()
    {
        CalibrationEvaluation Stratum(string cohort, bool automated, long so = 2_000) =>
            Eval(Run(_ => Good(so: so), cohort, automated, config: Config(cohort, automated, "fc3-cal-" + cohort[..2] + (automated ? "a" : "n"))));
        var l1 = Stratum(CalibrationCohorts.NeutralMixedId, true);
        var l2 = Stratum(CalibrationCohorts.NeutralMixedId, false);
        var l3 = Stratum(CalibrationCohorts.VirtualLeagueId, true);
        var l4 = Stratum(CalibrationCohorts.VirtualLeagueId, false);
        var l2Fail = Stratum(CalibrationCohorts.NeutralMixedId, false, so: 3_000);
        Assert.Equal(GateStatus.Fail, l2Fail.Gate);

        // a stratum whose evaluation failed (metrics error) has no evaluation to combine: the others can never reach PASS ...
        Assert.Equal(GateStatus.NotEvaluated, StratifiedEvaluator.CombineStrata([l1, l2, l3]).Gate);
        // ... and a missing stratum never turns a known FAIL into NOT_EVALUATED (it was: the missing check came first)
        var (gate, reason) = StratifiedEvaluator.CombineStrata([l1, l2Fail, l3]);
        Assert.Equal(GateStatus.Fail, gate);
        Assert.Contains("L2", reason);

        // a stratum that is not PASS is named with its own state and reason, so a NOT_EVALUATED / metrics reason is not swallowed by another stratum's INSUFFICIENT_SAMPLE
        var exhausted = Eval(Run(_ => Scaled(Good(), 10), CalibrationCohorts.VirtualLeagueId, true, config: Config(CalibrationCohorts.VirtualLeagueId, true, "fc3-cal-va")));
        var unevaluated = Eval(Run(_ => Good(), CalibrationCohorts.NeutralMixedId, false, config: Config(CalibrationCohorts.NeutralMixedId, false, "fc3-cal-nn")) with { Metrics = Result<MetricsReport>.Failure(new Error("Metrics.target_missing", "x")) });
        Assert.Equal((GateStatus.NotEvaluated, "L2"), (unevaluated.Gate, unevaluated.StratumId));
        var insufficient = StratifiedEvaluator.CombineStrata([l1, unevaluated, exhausted, l4]);
        Assert.Equal(GateStatus.InsufficientSample, insufficient.Gate);                                    // the verdict ordering is unchanged ...
        Assert.Contains("L3", insufficient.Reason);                                                         // ... but both non-PASS strata, with their own reasons, are in the combined reason
        Assert.Contains(exhausted.GateReason, insufficient.Reason);
        Assert.Contains("L2", insufficient.Reason);
        Assert.Contains("Metrics.target_missing", insufficient.Reason);
        var notEvaluated = StratifiedEvaluator.CombineStrata([l1, unevaluated, l3, l4]);
        Assert.Equal(GateStatus.NotEvaluated, notEvaluated.Gate);
        Assert.Contains("Metrics.target_missing", notEvaluated.Reason);
    }

    [Fact]
    public void A_combined_fail_still_names_every_other_stratum_that_is_not_pass_in_a_stable_order()
    {
        // FC2B final MINOR: the verdict and its priority (FAIL > INSUFFICIENT_SAMPLE > NOT_EVALUATED) are unchanged; the FAIL branch used to drop the other strata's states and reasons
        CalibrationEvaluation Stratum(string cohort, bool automated, long so = 2_000, bool metricsError = false, bool exhausted = false)
        {
            var run = Run(_ => exhausted ? Scaled(Good(so: so), 10) : Good(so: so), cohort, automated, config: Config(cohort, automated, "fc3-cal-" + cohort[..2] + (automated ? "a" : "n")));
            return Eval(metricsError ? run with { Metrics = Result<MetricsReport>.Failure(new Error("Metrics.target_missing", "x")) } : run);
        }

        var l1 = Stratum(CalibrationCohorts.NeutralMixedId, true);
        var l2Fail = Stratum(CalibrationCohorts.NeutralMixedId, false, so: 3_000);
        var l3 = Stratum(CalibrationCohorts.VirtualLeagueId, true);
        var l3Insufficient = Stratum(CalibrationCohorts.VirtualLeagueId, true, exhausted: true);
        var l3Unevaluated = Stratum(CalibrationCohorts.VirtualLeagueId, true, metricsError: true);
        var l4 = Stratum(CalibrationCohorts.VirtualLeagueId, false);
        Assert.Equal((GateStatus.Fail, GateStatus.InsufficientSample, GateStatus.NotEvaluated), (l2Fail.Gate, l3Insufficient.Gate, l3Unevaluated.Gate));

        // FAIL + INSUFFICIENT_SAMPLE: still FAIL (not summed, not downgraded), both reasons shown
        var (gate, reason) = StratifiedEvaluator.CombineStrata([l1, l2Fail, l3Insufficient, l4]);
        Assert.Equal(GateStatus.Fail, gate);
        Assert.Contains($"L2={GateText.Of(GateStatus.Fail)} ({l2Fail.GateReason})", reason);
        Assert.Contains($"L3={GateText.Of(GateStatus.InsufficientSample)} ({l3Insufficient.GateReason})", reason);

        // FAIL + NOT_EVALUATED (a metrics error): still FAIL, the error code is not lost
        (gate, reason) = StratifiedEvaluator.CombineStrata([l1, l2Fail, l3Unevaluated, l4]);
        Assert.Equal(GateStatus.Fail, gate);
        Assert.Contains($"L2={GateText.Of(GateStatus.Fail)} ({l2Fail.GateReason})", reason);
        Assert.Contains($"L3={GateText.Of(GateStatus.NotEvaluated)} ({l3Unevaluated.GateReason})", reason);
        Assert.Contains("Metrics.target_missing", reason);

        // FAIL + passing strata: no PASS reason is added
        (gate, reason) = StratifiedEvaluator.CombineStrata([l1, l2Fail, l3, l4]);
        Assert.Equal(GateStatus.Fail, gate);
        Assert.Contains("L2", reason);
        Assert.DoesNotContain("L1", reason);
        Assert.DoesNotContain("L3", reason);
        Assert.DoesNotContain("L4", reason);
        Assert.DoesNotContain("PASS", reason);

        // a missing stratum still does not hide a known FAIL, and the other non-PASS strata are still named
        (gate, reason) = StratifiedEvaluator.CombineStrata([l1, l2Fail, l3Insufficient]);
        Assert.Equal(GateStatus.Fail, gate);
        Assert.Contains("L4", reason);
        Assert.Contains($"L3={GateText.Of(GateStatus.InsufficientSample)} ({l3Insufficient.GateReason})", reason);

        // two FAIL strata are both named; the order does not depend on the order of the input
        var l3Fail = Stratum(CalibrationCohorts.VirtualLeagueId, true, so: 3_000);
        var both = StratifiedEvaluator.CombineStrata([l4, l3Fail, l2Fail, l1]);
        Assert.Equal(GateStatus.Fail, both.Gate);
        Assert.Equal(both, StratifiedEvaluator.CombineStrata([l1, l2Fail, l3Fail, l4]));
        Assert.True(both.Reason.IndexOf("L2=", StringComparison.Ordinal) < both.Reason.IndexOf("L3=", StringComparison.Ordinal));

        // no new status and no pooling: without a FAIL the earlier verdicts are as before
        Assert.Equal(GateStatus.InsufficientSample, StratifiedEvaluator.CombineStrata([l1, l2Fail with { Gate = GateStatus.Pass }, l3Insufficient, l4]).Gate);
        Assert.Equal(GateStatus.Pass, StratifiedEvaluator.CombineStrata([l1, Stratum(CalibrationCohorts.NeutralMixedId, false), l3, l4]).Gate);
    }

    [Fact]
    public void The_integration_condition_is_one_hundred_complete_games_and_its_metrics_stay_diagnostic()
    {
        var config = Config(CalibrationCohorts.VirtualLeagueId, true, "fc3-int-a");
        RunResult Of(int n, Func<int, GameRunStatus>? status = null)
        {
            var specs = Enumerable.Range(1, n).Select(i => config.Spec(i, (ulong)i)).ToList();
            var games = specs.Select(s => Game(s, Good(), status?.Invoke(s.GameIndex) ?? GameRunStatus.Completed)).ToList();
            return new RunResult(config, specs, games, Good(n), MetricsEvaluator.Evaluate(Good(n), config.Cohort, H.Tuning, EvaluationPolicy.Unapproved), EvaluationPolicy.Unapproved, "c", "h");
        }

        Assert.Equal(GateStatus.Pass, StratifiedEvaluator.Integration(Of(100)).Gate);
        Assert.Equal(GateStatus.Fail, StratifiedEvaluator.Integration(Of(100, g => g == 50 ? GameRunStatus.Failed : GameRunStatus.Completed)).Gate);
        Assert.Equal(GateStatus.NotEvaluated, StratifiedEvaluator.Integration(Of(99)).Gate);
        Assert.Equal(GateStatus.NotEvaluated, StratifiedEvaluator.Integration(Of(100, g => g == 50 ? GameRunStatus.Cancelled : GameRunStatus.Completed)).Gate);
        Assert.Equal(GateStatus.NotEvaluated, StratifiedEvaluator.Integration(Run(_ => Good())).Gate);    // a stop-aware run is never an integration run
    }

    [Fact]
    public void The_integration_gate_is_never_pass_when_the_metric_computation_itself_failed()
    {
        // FC2B-01 (ADR-008 FC-D9.1): 100 complete games are necessary, not sufficient; a metrics ERROR is not a NOT_EVALUATED verdict
        var config = Config(CalibrationCohorts.VirtualLeagueId, true, "fc3-int-a");
        RunResult Of(Result<MetricsReport> metrics, int n = 100, Func<int, GameRunStatus>? status = null)
        {
            var specs = Enumerable.Range(1, n).Select(i => config.Spec(i, (ulong)i)).ToList();
            var games = specs.Select(s => Game(s, Good(), status?.Invoke(s.GameIndex) ?? GameRunStatus.Completed)).ToList();
            return new RunResult(config, specs, games, Good(n), metrics, EvaluationPolicy.Unapproved, "c", "h");
        }

        var ok = MetricsEvaluator.Evaluate(Good(100), config.Cohort, H.Tuning, EvaluationPolicy.Unapproved);
        Assert.True(ok.IsSuccess);
        Assert.All(ok.Value.Metrics, m => Assert.NotEqual(MetricStatus.Pass, m.Status));                 // an unapproved policy: every verdict is a verdict, none is an error
        Assert.Equal(GateStatus.Pass, StratifiedEvaluator.Integration(Of(ok)).Gate);                      // diagnostic NOT_EVALUATED / INSUFFICIENT verdicts do not block the gate

        var targetMissing = Result<MetricsReport>.Failure(new Error("Metrics.target_missing", "x"));
        var inconsistent = MetricsEvaluator.Evaluate(new MetricCounters { PlateAppearances = 1, Strikeouts = 5 }, config.Cohort, H.Tuning, EvaluationPolicy.Unapproved);
        Assert.Equal("Metrics.counts_inconsistent", inconsistent.Error!.Value.Code);
        foreach (var failed in new[] { targetMissing, inconsistent })
        {
            var (gate, reason) = StratifiedEvaluator.Integration(Of(failed));
            Assert.Equal(GateStatus.Fail, gate);
            Assert.Contains(failed.Error!.Value.Code, reason);
        }

        // the existing completion conditions keep their own verdict whatever the metrics did
        Assert.Equal(GateStatus.NotEvaluated, StratifiedEvaluator.Integration(Of(targetMissing, 99)).Gate);
        Assert.Equal(GateStatus.NotEvaluated, StratifiedEvaluator.Integration(Of(targetMissing, 100, g => g == 50 ? GameRunStatus.Cancelled : GameRunStatus.Completed)).Gate);
        Assert.Equal(GateStatus.Fail, StratifiedEvaluator.Integration(Of(ok, 100, g => g == 50 ? GameRunStatus.Failed : GameRunStatus.Completed)).Gate);
    }

    // ---------------------------------------------------------------- diagnostics

    [Fact]
    public void The_cluster_variance_is_the_linearized_ratio_estimator_and_diagnostics_never_change_a_status()
    {
        Assert.Equal(4.0 / 324, StratifiedEvaluator.ClusterVariance([(1, 2), (3, 4)])!.Value, 12);
        Assert.Null(StratifiedEvaluator.ClusterVariance([(1, 2)]));
        Assert.Null(StratifiedEvaluator.ClusterVariance([(0, 0), (0, 0)]));
        Assert.Equal(0.0, StratifiedEvaluator.ClusterVariance([(1, 4), (2, 8), (3, 12)])!.Value, 12);   // identical game rates: no between-game variance

        var flat = Eval(Run(_ => Good()));
        var noisy = Eval(Run(g => g % 2 == 0 ? Good(so: 1_700) : Good(so: 2_300)));                  // same totals per stratum pair, other dispersion
        Assert.Equal(0.0, M(flat, "F1").Diagnostics.StandardErrorPpm!.Value, 9);
        Assert.True(M(noisy, "F1").Diagnostics.StandardErrorPpm > 0);
        Assert.Equal((M(flat, "F6").Status, flat.Gate), (M(noisy, "F6").Status, noisy.Gate));
        Assert.Null(M(flat, "F1").Diagnostics.DeffBatchMeans);                                         // 6 / 8 games per stratum: fewer than two 20-game batches
    }

    // ---------------------------------------------------------------- the real engine: a small dry run reports the stratified evaluation

    [Fact]
    public void A_real_dry_run_reports_both_handedness_strata_and_a_not_evaluated_gate()
    {
        var config = Config(CalibrationCohorts.NeutralMixedId, false, "fc2-dry-eval");
        var plan = new StopPlan("fc2-dry", 4, SeedManifestReader.DryMaxPa, Enumerable.Range(1, 4).Select(i => config.Spec(i, 2026101800UL + (ulong)i)).ToList(), "seeds/x.json", new string('a', 64));
        var run = SimRunner.RunStopAware(config, plan);
        var e = Eval(run);
        Assert.Equal((3, 1, GateStatus.NotEvaluated, false), (e.Shares.SameGames, e.Shares.OppositeGames, e.Gate, e.Official));
        Assert.Equal(run.Aggregate.PlateAppearances, e.Shares.SamePa + e.Shares.OppositePa);
        Assert.Equal(run.Aggregate.Swings, e.Shares.SameSwings + e.Shares.OppositeSwings);
        var summary = JsonDocument.Parse(JsonSerializer.Serialize(RunReport.Summary(run), RunReport.Json)).RootElement.GetProperty("calibration_evaluation");
        Assert.Equal(("NOT_EVALUATED", "fc-evaluation-policy-v1", false), (summary.GetProperty("gate_status").GetString(), summary.GetProperty("policy_id").GetString(), summary.GetProperty("official").GetBoolean()));
        Assert.Equal(8, summary.GetProperty("metrics").GetArrayLength());
    }
}
