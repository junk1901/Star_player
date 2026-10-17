using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>F-B: the verdict logic, the denominators and the rounding. A test threshold passed here proves the LOGIC only; the harness itself approves no threshold.</summary>
public class EvaluationTests
{
    private static readonly CohortInputs Neutral = NeutralCohort.Create();

    private static MetricCounters Counts(long pa = 0, long so = 0, long bb = 0, long hbp = 0, long hr = 0, long ab = 0, long h = 0, long sf = 0, long pitches = 0, long located = 0, long zone = 0,
        long hbpPitches = 0, long swings = 0, long swinging = 0) => new()
    {
        PlateAppearances = pa, Strikeouts = so, Walks = bb, HitByPitch = hbp, HomeRuns = hr, AtBats = ab, Hits = h, SacrificeFlies = sf, Pitches = pitches, LocatedPitches = located, ZonePitches = zone,
        HitByPitchPitches = hbpPitches, Swings = swings, SwingingStrikes = swinging,
    };

    private static MetricResult One(MetricCounters c, string fId, CohortInputs? cohort = null, EvaluationPolicy? policy = null) =>
        H.Ok(MetricsEvaluator.Evaluate(c, cohort ?? Neutral, H.Tuning, policy ?? EvaluationPolicy.Unapproved)).Metrics.Single(m => m.FId == fId);

    private static EvaluationPolicy Approved(long min, params string[] cohorts) =>
        new(MetricsEvaluator.Definitions.ToDictionary(d => d.TargetId, _ => min), new HashSet<string>(cohorts));

    // ---------------------------------------------------------------- the definitions and the targets

    [Fact]
    public void The_eight_metrics_and_their_targets_come_from_the_tuning_file_and_nowhere_else()
    {
        Assert.Equal(["F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8"], MetricsEvaluator.Definitions.Select(d => d.FId).ToArray());
        Assert.Equal(MetricsEvaluator.Definitions.Select(d => d.TargetId).Order().ToArray(), H.Tuning.Targets.Select(t => t.Id).Order().ToArray());
        var report = H.Ok(MetricsEvaluator.Evaluate(Counts(pa: 1000), Neutral, H.Tuning, EvaluationPolicy.Unapproved));
        foreach (var m in report.Metrics)
        {
            var declared = H.Tuning.Targets.Single(t => t.Id == m.TargetId);
            Assert.Equal((declared.LowPpm, declared.HighPpm), (m.TargetLowPpm, m.TargetHighPpm));
        }

        // a changed target in the file changes the report: nothing is cached or typed in the harness
        var edited = H.Ok(HarnessData.ParseTuning(HarnessData.ReadResource(HarnessData.TuningResource).Replace("\"low_ppm\": 160000", "\"low_ppm\": 161000", StringComparison.Ordinal)));
        Assert.Equal(161000L, H.Ok(MetricsEvaluator.Evaluate(Counts(pa: 10), Neutral, edited, EvaluationPolicy.Unapproved)).Metrics[0].TargetLowPpm);
        Assert.Equal(new[] { 160000L, 60000L, 4000L, 15000L, 270000L, 450000L, 400000L, 180000L }, report.Metrics.Select(m => m.TargetLowPpm).ToArray());
        Assert.Equal(new[] { 260000L, 110000L, 15000L, 35000L, 320000L, 650000L, 560000L, 300000L }, report.Metrics.Select(m => m.TargetHighPpm).ToArray());
    }

    [Fact]
    public void A_missing_declared_target_is_refused()
    {
        var text = HarnessData.ReadResource(HarnessData.TuningResource).Replace("\"id\": \"babip\"", "\"id\": \"babip_renamed\"", StringComparison.Ordinal);
        var tuning = H.Ok(HarnessData.ParseTuning(text));
        Assert.Equal("Metrics.target_missing", MetricsEvaluator.Evaluate(Counts(pa: 10), Neutral, tuning, EvaluationPolicy.Unapproved).Error!.Value.Code);
    }

    [Fact]
    public void The_recommended_cohort_of_each_metric_is_the_scope_decision_and_is_reported_not_inferred_from_the_file()
    {
        var recommended = MetricsEvaluator.Definitions.ToDictionary(d => d.FId, d => d.RecommendedCohort);
        Assert.All(new[] { "F1", "F2", "F3", "F4", "F5" }, f => Assert.Equal(CohortKind.VirtualLeague, recommended[f]));
        Assert.All(new[] { "F6", "F7", "F8" }, f => Assert.Equal(CohortKind.Neutral, recommended[f]));
        // the file's own descriptions are NOT reinterpreted: F1 says "neutral inputs", F2-F5 say "league average"
        Assert.Contains("neutral inputs", H.Tuning.Targets.Single(t => t.Id == "pa_strikeout_rate").Description, StringComparison.Ordinal);
        Assert.Contains("league average", H.Tuning.Targets.Single(t => t.Id == "pa_walk_rate").Description, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- formulas and denominators

    [Fact]
    public void The_pa_rates_use_plate_appearances_as_the_denominator()
    {
        var c = Counts(pa: 400, so: 80, bb: 36, hbp: 8, hr: 12, ab: 300, h: 100);
        Assert.Equal((80L, 400L, 200_000L), (One(c, "F1").NumeratorCount, One(c, "F1").DenominatorCount, One(c, "F1").MeasuredPpm!.Value));
        Assert.Equal(90_000L, One(c, "F2").MeasuredPpm);
        Assert.Equal(20_000L, One(c, "F3").MeasuredPpm);
        Assert.Equal(30_000L, One(c, "F4").MeasuredPpm);
    }

    [Fact]
    public void Babip_is_hits_minus_home_runs_over_at_bats_minus_strikeouts_minus_home_runs_plus_sacrifice_flies()
    {
        // H=110 HR=20 AB=400 SO=80 SF=10: (110 - 20) / (400 - 80 - 20 + 10) = 90/310
        var f5 = One(Counts(pa: 500, ab: 400, so: 80, hr: 20, h: 110, sf: 10), "F5");
        Assert.Equal((90L, 310L, 290_323L), (f5.NumeratorCount, f5.DenominatorCount, f5.MeasuredPpm!.Value));
        // a walk, hit by pitch or plate appearance count does NOT enter it
        Assert.Equal(290_323L, One(Counts(pa: 999, bb: 77, hbp: 5, ab: 400, so: 80, hr: 20, h: 110, sf: 10), "F5").MeasuredPpm);
    }

    [Fact]
    public void An_impossible_babip_is_refused_not_repaired()
    {
        // more non-home-run hits (80) than balls in play (100 - 30 - 0 + 0 = 70)
        Assert.Equal("Metrics.counts_inconsistent", MetricsEvaluator.Evaluate(Counts(pa: 1000, ab: 100, so: 30, h: 80), Neutral, H.Tuning, EvaluationPolicy.Unapproved).Error!.Value.Code);
        // fewer hits than home runs: a negative numerator
        Assert.Equal("Metrics.counts_inconsistent", MetricsEvaluator.Evaluate(Counts(pa: 1000, ab: 100, so: 10, hr: 5, h: 3), Neutral, H.Tuning, EvaluationPolicy.Unapproved).Error!.Value.Code);
        // a negative denominator: 10 - 20 - 0 + 0
        Assert.Equal("Metrics.counts_inconsistent", MetricsEvaluator.Evaluate(Counts(pa: 1000, ab: 10, so: 20), Neutral, H.Tuning, EvaluationPolicy.Unapproved).Error!.Value.Code);
        // more strikeouts than plate appearances
        Assert.Equal("Metrics.counts_inconsistent", MetricsEvaluator.Evaluate(Counts(pa: 5, so: 6, ab: 100), Neutral, H.Tuning, EvaluationPolicy.Unapproved).Error!.Value.Code);
    }

    [Fact]
    public void The_swing_rate_excludes_hit_by_pitch_pitches_from_the_denominator()
    {
        var c = Counts(pitches: 100, hbpPitches: 4, swings: 48, located: 100);
        var f7 = One(c, "F7");
        Assert.Equal((48L, 96L, 500_000L), (f7.NumeratorCount, f7.DenominatorCount, f7.MeasuredPpm!.Value));
    }

    [Fact]
    public void The_swing_and_miss_rate_is_over_swings_and_the_zone_rate_over_located_pitches()
    {
        var c = Counts(pitches: 100, located: 80, zone: 50, swings: 40, swinging: 10);
        Assert.Equal(625_000L, One(c, "F6").MeasuredPpm);                              // 50 / 80, not 50 / 100
        Assert.Equal(250_000L, One(c, "F8").MeasuredPpm);                              // 10 / 40
    }

    // ---------------------------------------------------------------- ppm rounding, 0% and 100%

    [Theory]
    [InlineData(1, 3, 333_333)]
    [InlineData(2, 3, 666_667)]
    [InlineData(1, 8, 125_000)]
    [InlineData(1, 2_000_000, 1)]            // exactly 0.5 ppm rounds up
    [InlineData(3, 2_000_000, 2)]            // exactly 1.5 ppm rounds up
    [InlineData(1, 4_000_000, 0)]            // 0.25 ppm rounds down
    [InlineData(0, 7, 0)]
    [InlineData(7, 7, 1_000_000)]
    [InlineData(999_999, 1_000_000, 999_999)]
    public void Ppm_is_rounded_half_up_in_integers(long numerator, long denominator, long expected) =>
        Assert.Equal(expected, MetricsEvaluator.Ppm(numerator, denominator));

    [Fact]
    public void Zero_and_one_hundred_percent_are_measured_exactly_and_judged_by_the_target()
    {
        var none = One(Counts(pa: 50), "F1");
        Assert.Equal((0L, 50L, 0L), (none.NumeratorCount, none.DenominatorCount, none.MeasuredPpm!.Value));
        Assert.Equal(-160_000L, none.DeviationPpm);
        var all = One(Counts(pa: 50, so: 50, ab: 50), "F1");
        Assert.Equal(1_000_000L, all.MeasuredPpm);
        Assert.Equal(1_000_000L - 260_000L, all.DeviationPpm);
        var judged = One(Counts(pa: 50, so: 50, ab: 50), "F1", policy: Approved(10, Neutral.CohortId));
        Assert.Equal(MetricStatus.NotEvaluated, judged.Status);                          // F1's cohort is the virtual league, not the neutral one
    }

    // ---------------------------------------------------------------- the status ladder

    [Fact]
    public void A_zero_denominator_is_insufficient_sample_and_has_no_measured_value()
    {
        var empty = H.Ok(MetricsEvaluator.Evaluate(Counts(), Neutral, H.Tuning, EvaluationPolicy.Unapproved));
        Assert.All(empty.Metrics, m =>
        {
            Assert.Equal(MetricStatus.InsufficientSample, m.Status);
            Assert.Null(m.MeasuredPpm);
            Assert.Null(m.DeviationPpm);
            Assert.Null(m.Wilson);
        });
    }

    [Fact]
    public void With_no_approved_minimum_sample_every_official_verdict_stays_not_evaluated_whatever_the_numbers_are()
    {
        var big = Counts(pa: 1_000_000, so: 200_000, bb: 80_000, hbp: 9_000, hr: 25_000, ab: 900_000, h: 250_000, sf: 8_000, pitches: 3_000_000, located: 3_000_000, zone: 1_700_000, hbpPitches: 9_000,
            swings: 1_400_000, swinging: 350_000);
        var report = H.Ok(MetricsEvaluator.Evaluate(big, Neutral, H.Tuning, EvaluationPolicy.Unapproved));

        Assert.All(report.Metrics, m =>
        {
            Assert.Equal(MetricStatus.NotEvaluated, m.Status);
            Assert.NotNull(m.MeasuredPpm);                                              // the value is still reported
            Assert.Equal("unapproved", m.SampleRule);
        });
        Assert.Empty(EvaluationPolicy.Unapproved.MinDenominatorByTarget);
        Assert.Empty(EvaluationPolicy.Unapproved.ApprovedCohortIds);
    }

    [Fact]
    public void Below_an_explicit_minimum_is_insufficient_sample_and_at_it_the_verdict_is_possible()
    {
        var policy = Approved(100, Neutral.CohortId);
        Assert.Equal(MetricStatus.InsufficientSample, One(Counts(pitches: 99, located: 99, zone: 55), "F6", policy: policy).Status);
        Assert.Equal(MetricStatus.Pass, One(Counts(pitches: 100, located: 100, zone: 55), "F6", policy: policy).Status);
    }

    [Fact]
    public void The_verdict_is_a_point_estimate_with_inclusive_bounds()
    {
        var policy = Approved(1000, Neutral.CohortId);
        MetricStatus Zone(long zone) => One(Counts(pitches: 1000, located: 1000, zone: zone), "F6", policy: policy).Status;   // F6 target 450,000..650,000

        Assert.Equal(MetricStatus.Pass, Zone(450));       // exactly the low bound
        Assert.Equal(MetricStatus.Pass, Zone(650));       // exactly the high bound
        Assert.Equal(MetricStatus.Pass, Zone(550));
        Assert.Equal(MetricStatus.Fail, Zone(449));
        Assert.Equal(MetricStatus.Fail, Zone(651));
        Assert.Equal(0L, One(Counts(pitches: 1000, located: 1000, zone: 450), "F6", policy: policy).DeviationPpm);
        Assert.Equal(-1_000L, One(Counts(pitches: 1000, located: 1000, zone: 449), "F6", policy: policy).DeviationPpm);
        Assert.Equal(1_000L, One(Counts(pitches: 1000, located: 1000, zone: 651), "F6", policy: policy).DeviationPpm);
    }

    [Fact]
    public void The_wilson_interval_is_a_diagnostic_and_never_rescues_a_failed_point_estimate()
    {
        var policy = Approved(1000, Neutral.CohortId);
        var c = Counts(pitches: 1000, located: 1000, zone: 440);                         // 440,000 ppm: just under the 450,000 bound
        var f6 = One(c, "F6", policy: policy);

        Assert.Equal(MetricStatus.Fail, f6.Status);
        Assert.True(f6.Wilson!.LowPpm < 450_000 && f6.Wilson.HighPpm > 450_000);          // the interval overlaps the target, yet the status stays FAIL
        Assert.InRange(f6.Wilson.LowPpm, 408_000, 412_000);                              // 440/1000 at 95%: about 0.409 .. 0.471
        Assert.InRange(f6.Wilson.HighPpm, 470_000, 473_000);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 10)]
    [InlineData(5, 10)]
    public void The_wilson_interval_stays_inside_zero_to_one_and_contains_the_point_estimate(long successes, long trials)
    {
        var w = MetricsEvaluator.Wilson(successes, trials);
        var p = MetricsEvaluator.Ppm(successes, trials);
        Assert.InRange(w.LowPpm, 0, 1_000_000);
        Assert.InRange(w.HighPpm, 0, 1_000_000);
        Assert.True(w.LowPpm <= p && p <= w.HighPpm);
    }

    [Fact]
    public void A_cohort_that_is_not_the_metrics_recommended_one_never_gets_a_verdict_even_when_approved_by_name()
    {
        var league = H.Variant("league-x", _ => null);
        Assert.Equal(CohortKind.VirtualLeague, league.Kind);
        var policy = Approved(10, league.CohortId, Neutral.CohortId);
        var c = Counts(pa: 1000, so: 200, bb: 80, hbp: 9, hr: 25, ab: 900, h: 250, sf: 8, pitches: 1000, located: 1000, zone: 550, swings: 450, swinging: 100);

        // the virtual-league cohort IS the recommended one for F1-F5 (verdict possible once approved) and NOT for F6-F8; the neutral cohort is the reverse
        var asLeague = H.Ok(MetricsEvaluator.Evaluate(c, league, H.Tuning, policy)).Metrics;
        Assert.All(asLeague.Take(5), m => Assert.NotEqual(MetricStatus.NotEvaluated, m.Status));
        Assert.All(asLeague.Skip(5), m => Assert.Equal(MetricStatus.NotEvaluated, m.Status));
        var asNeutral = H.Ok(MetricsEvaluator.Evaluate(c, Neutral, H.Tuning, policy)).Metrics;
        Assert.All(asNeutral.Take(5), m => Assert.Equal(MetricStatus.NotEvaluated, m.Status));
        Assert.All(asNeutral.Skip(5), m => Assert.NotEqual(MetricStatus.NotEvaluated, m.Status));
    }

    [Fact]
    public void Every_result_records_the_cohort_it_was_evaluated_on()
    {
        var report = H.Ok(MetricsEvaluator.Evaluate(Counts(pa: 10), Neutral, H.Tuning, EvaluationPolicy.Unapproved));
        Assert.Equal(("neutral-v1", "Neutral"), (report.CohortId, report.CohortKind));
        Assert.All(report.Metrics, m => Assert.Equal(("neutral-v1", "Neutral"), (m.EvaluatedCohortId, m.EvaluatedCohortKind)));
    }
}
