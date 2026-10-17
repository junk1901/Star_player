using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;

namespace StarPlayer.SimHarness;

/// <summary>PASS / FAIL are produced only when every approval the verdict needs is present; otherwise the value is still reported but the verdict is one of the other two.</summary>
public enum MetricStatus { Pass, Fail, InsufficientSample, NotEvaluated }

/// <summary>One declared target and how its numerator and denominator are read from the counters. <see cref="RecommendedCohort"/> is the cohort the F-A/B scope recommends for the metric (a plan, not a verdict).</summary>
public sealed record MetricDefinition(string FId, string TargetId, CohortKind RecommendedCohort, string NumeratorText, string DenominatorText, Func<MetricCounters, (long Numerator, long Denominator)> Read);

/// <summary>
/// What the later official evaluation has approved. NOTHING is approved in F-A/B: <see cref="Unapproved"/> has no minimum denominator for any metric and no approved cohort, so every official
/// verdict stays <see cref="MetricStatus.NotEvaluated"/> (a denominator of zero is <see cref="MetricStatus.InsufficientSample"/>). A test passes an explicit policy to exercise the verdict logic; that proves
/// the logic, never a threshold.
/// </summary>
public sealed record EvaluationPolicy(IReadOnlyDictionary<string, long> MinDenominatorByTarget, IReadOnlySet<string> ApprovedCohortIds)
{
    public static EvaluationPolicy Unapproved { get; } = new(new Dictionary<string, long>(), new HashSet<string>());
}

public sealed record WilsonInterval(long LowPpm, long HighPpm);

public sealed record MetricResult(
    string FId, string TargetId, string Numerator, string Denominator, string RecommendedCohort, string EvaluatedCohortId, string EvaluatedCohortKind,
    long NumeratorCount, long DenominatorCount, long? MeasuredPpm, long TargetLowPpm, long TargetHighPpm, long? DeviationPpm, long? MinDenominator, string SampleRule,
    MetricStatus Status, string Reason, WilsonInterval? Wilson);

public sealed record MetricsReport(string CohortId, string CohortKind, IReadOnlyList<MetricResult> Metrics);

public static class MetricsEvaluator
{
    /// <summary>F1-F8 in order. Formulas are the F-A/B scope's; the target ids are the ones declared in the tuning file's <c>targets[]</c>.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        new("F1", "pa_strikeout_rate", CohortKind.VirtualLeague, "SO", "PA", c => (c.Strikeouts, c.PlateAppearances)),
        new("F2", "pa_walk_rate", CohortKind.VirtualLeague, "BB", "PA", c => (c.Walks, c.PlateAppearances)),
        new("F3", "pa_hit_by_pitch_rate", CohortKind.VirtualLeague, "HBP", "PA", c => (c.HitByPitch, c.PlateAppearances)),
        new("F4", "pa_home_run_rate", CohortKind.VirtualLeague, "HR", "PA", c => (c.HomeRuns, c.PlateAppearances)),
        new("F5", "babip", CohortKind.VirtualLeague, "H-HR", "AB-SO-HR+SF", c => (c.Hits - c.HomeRuns, c.AtBats - c.Strikeouts - c.HomeRuns + c.SacrificeFlies)),
        new("F6", "pitch_zone_rate", CohortKind.Neutral, "pitches located in the nine inner cells", "pitches with a confirmed actual location", c => (c.ZonePitches, c.LocatedPitches)),
        new("F7", "pitch_swing_rate", CohortKind.Neutral, "swings (swinging strike, foul, in play)", "pitches - hit-by-pitch pitches", c => (c.Swings, c.Pitches - c.HitByPitchPitches)),
        new("F8", "swing_miss_rate", CohortKind.Neutral, "swinging strikes", "swings", c => (c.SwingingStrikes, c.Swings)),
    ];

    /// <summary>Round-half-up of <c>numerator / denominator</c> in parts per million, in integers (denominator &gt; 0, 0 &lt;= numerator).</summary>
    public static long Ppm(long numerator, long denominator) => checked((2 * numerator * 1_000_000 + denominator) / (2 * denominator));

    /// <summary>
    /// Evaluates the eight metrics for <paramref name="counters"/> measured on <paramref name="cohort"/>. Targets are read from <paramref name="tuning"/>, never typed here. A missing declared
    /// target, a negative count or a numerator above its denominator (an impossible rate, for instance a BABIP with more non-HR hits than balls in play) is refused, not repaired.
    /// </summary>
    public static Result<MetricsReport> Evaluate(MetricCounters counters, CohortInputs cohort, TuningProfile tuning, EvaluationPolicy policy)
    {
        var results = new List<MetricResult>();
        foreach (var definition in Definitions)
        {
            var target = tuning.Targets.FirstOrDefault(t => t.Id == definition.TargetId);
            if (target is null)
                return Result<MetricsReport>.Failure(new Error("Metrics.target_missing", $"튜닝 프로필에 목표 '{definition.TargetId}'가 선언되어 있지 않습니다."));
            var (num, den) = definition.Read(counters);
            if (num < 0 || den < 0 || num > den)
                return Result<MetricsReport>.Failure(new Error("Metrics.counts_inconsistent", $"{definition.FId} {definition.TargetId}: 분자 {num}, 분모 {den}은(는) 올바른 비율이 아닙니다."));

            long? measured = den > 0 ? Ppm(num, den) : null;
            long? deviation = measured is { } m ? (m < target.LowPpm ? m - target.LowPpm : m > target.HighPpm ? m - target.HighPpm : 0) : null;
            policy.MinDenominatorByTarget.TryGetValue(definition.TargetId, out var min);
            var minKnown = policy.MinDenominatorByTarget.ContainsKey(definition.TargetId);
            var cohortApproved = definition.RecommendedCohort == cohort.Kind && policy.ApprovedCohortIds.Contains(cohort.CohortId);

            MetricStatus status;
            string reason;
            if (den == 0)
                (status, reason) = (MetricStatus.InsufficientSample, "분모가 0입니다.");
            else if (!minKnown)
                (status, reason) = (MetricStatus.NotEvaluated, "최소 표본 기준이 승인되지 않았습니다(F-C 선행 결정).");
            else if (den < min)
                (status, reason) = (MetricStatus.InsufficientSample, $"분모 {den}이(가) 최소 표본 {min}에 못 미칩니다.");
            else if (!cohortApproved)
                (status, reason) = (MetricStatus.NotEvaluated, $"코호트 '{cohort.CohortId}'({cohort.Kind})는 이 지표의 승인된 평가 코호트({definition.RecommendedCohort})가 아닙니다.");
            else
                (status, reason) = measured >= target.LowPpm && measured <= target.HighPpm ? (MetricStatus.Pass, "점추정이 목표 구간 안(경계 포함)입니다.") : (MetricStatus.Fail, "점추정이 목표 구간 밖입니다.");

            results.Add(new MetricResult(definition.FId, definition.TargetId, definition.NumeratorText, definition.DenominatorText, definition.RecommendedCohort.ToString(), cohort.CohortId, cohort.Kind.ToString(),
                num, den, measured, target.LowPpm, target.HighPpm, deviation, minKnown ? min : null, minKnown ? $"min_denominator={min}" : "unapproved", status, reason, den > 0 ? Wilson(num, den) : null));
        }

        return Result<MetricsReport>.Success(new MetricsReport(cohort.CohortId, cohort.Kind.ToString(), results));
    }

    /// <summary>
    /// The 95% Wilson score interval, in ppm, as a DIAGNOSTIC only: it never changes a status, it assumes independent trials (pitches and plate appearances of one game are correlated), and it is not a
    /// guarantee of anything. Floating point, so it is kept out of every hash.
    /// </summary>
    public static WilsonInterval Wilson(long successes, long trials)
    {
        const double z = 1.959963984540054;
        double n = trials, p = (double)successes / n, z2 = z * z;
        var denominator = 1 + z2 / n;
        var center = (p + z2 / (2 * n)) / denominator;
        var half = z * Math.Sqrt(p * (1 - p) / n + z2 / (4 * n * n)) / denominator;
        long Ppm(double v) => (long)Math.Round(Math.Clamp(v, 0, 1) * 1_000_000, MidpointRounding.AwayFromZero);
        return new WilsonInterval(Ppm(center - half), Ppm(center + half));
    }
}
