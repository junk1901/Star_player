using System.Text;
using StarPlayer.Domain.Common;

namespace StarPlayer.SimHarness;

/// <summary>The statistical gate axis (ADR-008 FC-D9 / FC-D10, ADR-009 SR-D9): always separate from the process exit code. Exit 0 is never <see cref="Pass"/>.</summary>
public enum GateStatus { NotEvaluated, InsufficientSample, Pass, Fail }

/// <summary>One calibration stratum of ADR-008 FC-D7: a cohort in one called-pitch mode, judged on its own official metrics and never pooled with another stratum.</summary>
public sealed record CalibrationStratum(string Id, string CohortId, string RulesProfileId, IReadOnlyList<string> OfficialMetrics);

public static class CalibrationStrata
{
    private static readonly string[] Neutral = ["F1", "F6", "F7", "F8"];
    private static readonly string[] League = ["F2", "F3", "F4", "F5"];

    /// <summary>FC-D7: L1 / L2 <c>neutral-mixed-v1</c> (F1, F6, F7, F8), L3 / L4 <c>virtual-league-v1</c> (F2 to F5), Automated / NonAutomated.</summary>
    public static IReadOnlyList<CalibrationStratum> All { get; } =
    [
        new("L1", CalibrationCohorts.NeutralMixedId, HarnessData.AutomatedProfile, Neutral),
        new("L2", CalibrationCohorts.NeutralMixedId, HarnessData.NonAutomatedProfile, Neutral),
        new("L3", CalibrationCohorts.VirtualLeagueId, HarnessData.AutomatedProfile, League),
        new("L4", CalibrationCohorts.VirtualLeagueId, HarnessData.NonAutomatedProfile, League),
    ];

    public static CalibrationStratum? Of(string cohortId, string rulesProfileId) => All.FirstOrDefault(s => s.CohortId == cohortId && s.RulesProfileId == rulesProfileId);
}

public static class GateText
{
    public static string Of(GateStatus g) => g switch
    {
        GateStatus.Pass => "PASS", GateStatus.Fail => "FAIL", GateStatus.InsufficientSample => "INSUFFICIENT_SAMPLE", _ => "NOT_EVALUATED",
    };
}

/// <summary>Per-stratum diagnostics of one metric (floating point, never in a hash, never a verdict): the observed aggregate, the game-cluster standard error and DEFF, and the batch-means DEFF.</summary>
public sealed record MetricDiagnostics(long? ObservedPpm, double? StandardErrorPpm, double? DeffGameCluster, double? DeffBatchMeans, int BatchGames);

/// <summary>One official-metric evaluation of a calibration run. <see cref="MeasuredPpm"/> is the official point estimate (standardized for F1 / F8, observed for the others).</summary>
public sealed record StratifiedMetric(
    string FId, string TargetId, string Method, bool Official, long Numerator, long Denominator, long NumeratorSame, long DenominatorSame, long NumeratorOpposite, long DenominatorOpposite,
    long? MeasuredPpm, long TargetLowPpm, long TargetHighPpm, long MinDenominator, long? MinSame, long? MinOpposite, GateStatus Status, string Reason, MetricDiagnostics Diagnostics);

/// <summary>The observed handedness shares of a run (reported, never assumed equal to 3/7; ADR-008 FC-D1.5).</summary>
public sealed record StratumShares(int SameGames, int OppositeGames, int MixedGames, long SamePa, long OppositePa, long SamePitches, long OppositePitches, long SameSwings, long OppositeSwings);

/// <summary>
/// The evaluation of one run, traceable to its inputs: the policy (id + hash), the stratum, the cohort input hash, the whole-plan config hash, the result hash and the stop record. <see cref="Official"/>
/// is true only for an <c>fc3-cal</c> stop-aware run; a dry run is evaluated the same way but its <see cref="Gate"/> is always NOT_EVALUATED (ADR-009 SR-D4.3).
/// </summary>
public sealed record CalibrationEvaluation(
    string PolicyId, string PolicyHash, string? StratumId, string CohortId, string CohortInputsHash, string RulesProfileId, string? Family, bool Official, string ConfigHash, string? RunResultHash,
    StopRecord? Stop, int CompletedGames, int FailedGames, long ValidPa, StratumShares Shares, IReadOnlyList<StratifiedMetric> Metrics, GateStatus Gate, string GateReason);

/// <summary>
/// ADR-008 calibration evaluation of a stop-aware run (FC-D1, FC-D2, FC-D6, FC-D7, FC-D9; ADR-009 SR-D9). Counts are the COMPLETED games' (a failed game's partial counters never count, FC-D9.2), split by
/// each game's handedness stratum (<see cref="CohortInputs.StratumOf"/>). F1 / F8 are the pre-weighted standardization <c>3/7 · p_S + 4/7 · p_O</c> over their own denominators in exact integer ppm
/// (half-up, no intermediate rounding); every other metric is the observed aggregate. A metric meeting its minimums is PASS / FAIL by its point estimate against the declared interval (bounds
/// inclusive); Wilson, standard errors and DEFF are diagnostics only. The gate is never PASS with a failed game, an exhausted plan, a missed stratum minimum, a metric error, a cancelled or aborted run,
/// or outside <c>fc3-cal</c>.
/// </summary>
public static class StratifiedEvaluator
{
    /// <summary>Proposal §8.1 batch size for the batch-means DEFF diagnostic (a reporting parameter, not an approved threshold: nothing changes on its value, ADR-008 N-2).</summary>
    public const int BatchGames = 20;

    /// <summary>
    /// Exact standardized ppm (FC-D1.4): <c>floor((2·10^6·(3·N_S·D_O + 4·N_O·D_S) + 7·D_S·D_O) / (14·D_S·D_O))</c> in checked 128-bit integers. Both denominators must be positive and every numerator within its denominator.
    /// </summary>
    public static long StandardizedPpm(long nSame, long dSame, long nOpposite, long dOpposite)
    {
        if (dSame <= 0 || dOpposite <= 0 || nSame < 0 || nOpposite < 0 || nSame > dSame || nOpposite > dOpposite)
            throw new ArgumentOutOfRangeException(nameof(dSame), "층별 분모는 양수이고 분자는 0 이상 분모 이하여야 합니다.");
        checked
        {
            Int128 ns = nSame, ds = dSame, no = nOpposite, d0 = dOpposite;
            var value = (2 * (Int128)1_000_000 * (3 * ns * d0 + 4 * no * ds) + 7 * ds * d0) / (14 * ds * d0);
            return (long)value;
        }
    }

    /// <summary>The linearized variance of a ratio estimator with the given clusters (games or batches): <c>n/(n−1) · Σ(y − p̂·x)² / (Σx)²</c>; null with fewer than two clusters or no denominator.</summary>
    public static double? ClusterVariance(IReadOnlyList<(long Y, long X)> clusters)
    {
        double sumX = clusters.Sum(c => (double)c.X), sumY = clusters.Sum(c => (double)c.Y);
        if (clusters.Count < 2 || sumX <= 0)
            return null;
        var p = sumY / sumX;
        var ss = clusters.Sum(c => (c.Y - p * c.X) * (c.Y - p * c.X));
        return clusters.Count / (clusters.Count - 1.0) * ss / (sumX * sumX);
    }

    private static IReadOnlyList<(long, long)> Batches(IReadOnlyList<(long Y, long X)> games) =>
        Enumerable.Range(0, games.Count / BatchGames).Select(b => games.Skip(b * BatchGames).Take(BatchGames)).Select(g => (g.Sum(x => x.Y), g.Sum(x => x.X))).ToList();

    private static double Binomial(long n, long d) => d <= 0 ? double.NaN : (double)n / d * (1 - (double)n / d) / d;

    private static double? Ratio(double? a, double b) => a is { } v && b > 0 && !double.IsNaN(b) ? v / b : null;

    public static Result<CalibrationEvaluation> Evaluate(RunResult run, EvaluationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(policy);
        var cohort = run.Config.Cohort;
        var stratum = CalibrationStrata.Of(cohort.CohortId, run.Config.Rules.ProfileId);
        var completed = run.Games.Where(g => g.Completed).ToList();
        var byStratum = completed.ToLookup(g => cohort.StratumOf(g.Game.GameIndex));
        MetricCounters Sum(IEnumerable<GameRunResult> games)
        {
            var c = new MetricCounters();
            foreach (var g in games)
                c.Add(g.Counters);
            return c;
        }

        var same = Sum(byStratum[HandednessStratum.Same]);
        var opposite = Sum(byStratum[HandednessStratum.Opposite]);
        var shares = new StratumShares(byStratum[HandednessStratum.Same].Count(), byStratum[HandednessStratum.Opposite].Count(), byStratum[HandednessStratum.Mixed].Count(),
            same.PlateAppearances, opposite.PlateAppearances, same.Pitches, opposite.Pitches, same.Swings, opposite.Swings);

        var metrics = new List<StratifiedMetric>();
        foreach (var definition in MetricsEvaluator.Definitions)
        {
            var target = run.Config.Tuning.Targets.FirstOrDefault(t => t.Id == definition.TargetId);
            if (target is null)
                return Result<CalibrationEvaluation>.Failure(new Error("Metrics.target_missing", $"튜닝 프로필에 목표 '{definition.TargetId}'가 선언되어 있지 않습니다."));
            var perGame = completed.Select(g => definition.Read(g.Counters)).ToList();
            var (n, d) = definition.Read(run.Aggregate);
            var (ns, ds) = definition.Read(same);
            var (no, dO) = definition.Read(opposite);
            if (new[] { (n, d), (ns, ds), (no, dO) }.Concat(perGame).Any(x => x.Item1 < 0 || x.Item2 < 0 || x.Item1 > x.Item2))
                return Result<CalibrationEvaluation>.Failure(new Error("Metrics.counts_inconsistent", $"{definition.FId}: 분자·분모가 올바른 비율이 아닙니다."));

            var standardized = policy.StandardizedMetrics.Contains(definition.FId);
            var official = stratum is not null && stratum.OfficialMetrics.Contains(definition.FId) && policy.OfficialCohortByMetric.GetValueOrDefault(definition.FId) == cohort.CohortId;
            var min = policy.MinDenominatorByTarget.GetValueOrDefault(definition.TargetId);
            var hasMin = policy.MinDenominatorByTarget.ContainsKey(definition.TargetId);
            (long Same, long Opposite)? stratumMin = policy.StratumMinimums.TryGetValue(definition.FId, out var sm) ? sm : null;

            long? measured = standardized
                ? (ds > 0 && dO > 0 ? StandardizedPpm(ns, ds, no, dO) : null)
                : (d > 0 ? MetricsEvaluator.Ppm(n, d) : null);
            GateStatus status;
            string reason;
            if (!official)
                (status, reason) = (GateStatus.NotEvaluated, stratum is null ? "공식 calibration 층(L1~L4)의 코호트·모드가 아닙니다(진단)." : $"이 층({stratum.Id})의 공식 판정 지표가 아닙니다(진단).");
            else if (!hasMin || (standardized && stratumMin is null))
                (status, reason) = (GateStatus.NotEvaluated, "승인된 최소 표본 기준이 없습니다.");
            else if (standardized && shares.MixedGames > 0)
                (status, reason) = (GateStatus.NotEvaluated, "손잡이 층이 정의되지 않은 경기가 있어 표준화할 수 없습니다(계약 미충족).");
            else if (measured is null || d < min || (standardized && (ds < stratumMin!.Value.Same || dO < stratumMin.Value.Opposite)))
                (status, reason) = (GateStatus.InsufficientSample, standardized
                    ? $"분모 {d}(S {ds}, O {dO})가 최소 {min}(S {stratumMin!.Value.Same}, O {stratumMin.Value.Opposite})에 못 미칩니다."
                    : $"분모 {d}이(가) 최소 표본 {min}에 못 미칩니다.");
            else
                (status, reason) = measured >= target.LowPpm && measured <= target.HighPpm ? (GateStatus.Pass, "점추정이 목표 구간 안(경계 포함)입니다.") : (GateStatus.Fail, "점추정이 목표 구간 밖입니다.");

            // diagnostics: game clusters (FC-D1.7) stratum-wise for the standardized metrics, over all games otherwise; batch means over consecutive plan-order games
            var games = completed.Select(g => (Stratum: cohort.StratumOf(g.Game.GameIndex), Pair: definition.Read(g.Counters))).ToList();
            double? variance, binomial, batch;
            if (standardized)
            {
                double ws = 3.0 / 7, wo = 4.0 / 7;
                var vs = ClusterVariance(games.Where(g => g.Stratum == HandednessStratum.Same).Select(g => g.Pair).ToList());
                var vo = ClusterVariance(games.Where(g => g.Stratum == HandednessStratum.Opposite).Select(g => g.Pair).ToList());
                variance = vs is { } a && vo is { } b ? ws * ws * a + wo * wo * b : null;
                binomial = ws * ws * Binomial(ns, ds) + wo * wo * Binomial(no, dO);
                var bs = ClusterVariance(Batches(games.Where(g => g.Stratum == HandednessStratum.Same).Select(g => g.Pair).ToList()));
                var bo = ClusterVariance(Batches(games.Where(g => g.Stratum == HandednessStratum.Opposite).Select(g => g.Pair).ToList()));
                batch = bs is { } c && bo is { } e ? ws * ws * c + wo * wo * e : null;
            }
            else
            {
                variance = ClusterVariance(perGame);
                binomial = Binomial(n, d);
                batch = ClusterVariance(Batches(perGame));
            }

            var diagnostics = new MetricDiagnostics(d > 0 ? MetricsEvaluator.Ppm(n, d) : null, variance is { } v ? Math.Sqrt(v) * 1_000_000 : null, Ratio(variance, binomial ?? double.NaN),
                Ratio(batch, binomial ?? double.NaN), BatchGames);
            metrics.Add(new StratifiedMetric(definition.FId, definition.TargetId, standardized ? "standardized_3_7_4_7" : "observed_aggregate", official, n, d, ns, ds, no, dO, measured,
                target.LowPpm, target.HighPpm, min, stratumMin?.Same, stratumMin?.Opposite, status, reason, diagnostics));
        }

        var family = run.Plan?.Family;
        var (gate, why) = Gate(run, policy, stratum, family, metrics);
        return Result<CalibrationEvaluation>.Success(new CalibrationEvaluation(policy.Id, policy.Hash(), stratum?.Id, cohort.CohortId, cohort.InputsHash, run.Config.Rules.ProfileId, family,
            family == "fc3-cal", run.ConfigHash, run.ResultVersion is null ? null : run.RunResultHash, run.Stop, run.CompletedCount, run.FailedCount, run.Aggregate.PlateAppearances, shares, metrics, gate, why));
    }

    /// <summary>
    /// The gate branches that come BEFORE the metrics (SR-D9.2 priority; shared by <see cref="Gate"/> and <see cref="GateOfFailedEvaluation"/>): not a calibration / dry / unapproved evaluation and a
    /// cancelled or aborted run are NOT_EVALUATED; a failed game is FAIL whatever the metrics did. Null: go on with the metrics.
    /// </summary>
    private static (GateStatus, string)? BeforeMetrics(RunResult run, EvaluationPolicy policy, string? family)
    {
        if (!run.StopAware || family is null)
            return (GateStatus.NotEvaluated, "정지 인식 calibration 실행이 아닙니다.");
        if (family == "fc2-dry")
            return (GateStatus.NotEvaluated, "fc2-dry는 진단 실행입니다(공식 통계·Gate 아님, ADR-009 SR-D4.3).");
        if (family != "fc3-cal")
            return (GateStatus.NotEvaluated, $"family '{family}'는 calibration이 아닙니다.");
        if (!policy.IsApproved)
            return (GateStatus.NotEvaluated, "승인된 평가 정책이 아닙니다.");
        if (run.Termination != RunTermination.Rule || run.Stop is null)
            return (GateStatus.NotEvaluated, run.Termination == RunTermination.Cancelled ? "취소된 실행입니다(SR-D3.5)." : "무결성 중단된 실행입니다(SR-D3.4).");
        if (run.FailedCount > 0)
            return (GateStatus.Fail, $"실패 경기 {run.FailedCount}건(FC-D9.1; 제외 재집계 없음).");
        return null;
    }

    /// <summary>
    /// The gate of a stop-aware run whose stratified evaluation could not be made at all (<paramref name="error"/> is a metrics error): the same priority as <see cref="Gate"/>, so a failed game is still
    /// FAIL (FC-D9.1, SR-D3.3), a cancelled / aborted / non-calibration run is NOT_EVALUATED, and the metrics error itself is NOT_EVALUATED naming the error (SR-D9.2). Never PASS.
    /// </summary>
    public static (GateStatus Gate, string Reason) GateOfFailedEvaluation(RunResult run, EvaluationPolicy policy, Error error)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(policy);
        return BeforeMetrics(run, policy, run.Plan?.Family) is { } early
            ? (early.Item1, early.Item1 == GateStatus.Fail ? $"{early.Item2} 지표 계산 오류({error.Code})도 있습니다." : early.Item2)   // the FAIL does not hide the metrics error
            : (GateStatus.NotEvaluated, $"지표를 계산할 수 없습니다({error.Code}).");
    }

    /// <summary>The gate of one stratum run, in this order (FC-D9, SR-D9.2): not a calibration run / dry; not terminated; a failed game; a metric error; exhausted plan; stratum PA floor; the official metrics.</summary>
    private static (GateStatus, string) Gate(RunResult run, EvaluationPolicy policy, CalibrationStratum? stratum, string? family, IReadOnlyList<StratifiedMetric> metrics)
    {
        if (BeforeMetrics(run, policy, family) is { } early)
            return early;
        if (!run.MetricsComputed)
            return (GateStatus.NotEvaluated, $"지표를 계산할 수 없습니다({run.Metrics.Error!.Value.Code}).");
        if (stratum is null)
            return (GateStatus.NotEvaluated, "코호트·규칙 프로파일이 L1~L4 층이 아닙니다.");
        if (run.Stop!.Reason == StopReason.SeedsExhausted)   // BeforeMetrics returned for a null Stop
            return (GateStatus.InsufficientSample, $"Seed 소진(누적 유효 PA {run.Stop.CumulativePa} < 목표; Seed를 추가하지 않음, SR-D9.2).");
        if (run.Stop.CumulativePa < policy.StratumMinPa)
            return (GateStatus.InsufficientSample, $"층 유효 PA {run.Stop.CumulativePa} < {policy.StratumMinPa}(FC-D7.1).");
        var official = metrics.Where(m => m.Official).ToList();
        if (official.Count != stratum.OfficialMetrics.Count)
            return (GateStatus.NotEvaluated, "층의 공식 지표가 정책에 모두 승인되어 있지 않습니다.");
        if (official.Any(m => m.Status == GateStatus.Fail))
            return (GateStatus.Fail, $"공식 지표 FAIL: {string.Join(", ", official.Where(m => m.Status == GateStatus.Fail).Select(m => m.FId))}.");
        if (official.Any(m => m.Status == GateStatus.InsufficientSample))
            return (GateStatus.InsufficientSample, $"공식 지표 표본 부족: {string.Join(", ", official.Where(m => m.Status == GateStatus.InsufficientSample).Select(m => m.FId))}.");
        if (official.Any(m => m.Status == GateStatus.NotEvaluated))
            return (GateStatus.NotEvaluated, "판정되지 않은 공식 지표가 있습니다.");
        return (GateStatus.Pass, $"{stratum.Id}의 공식 지표 {string.Join(", ", stratum.OfficialMetrics)} 모두 PASS.");
    }

    /// <summary>
    /// FC-D7.3 / FC-D12(4): the four strata judged together WITHOUT pooling. Each of L1 to L4 must appear exactly once, from distinct runs (competition keys / config hashes); PASS only if all four pass,
    /// FAIL if any fails (no compensation), otherwise the weakest state. A missing or repeated stratum is NOT_EVALUATED.
    /// </summary>
    public static (GateStatus Gate, string Reason) CombineStrata(IReadOnlyList<CalibrationEvaluation> strata)
    {
        if (strata.Any(s => !s.Official || s.StratumId is null))
            return (GateStatus.NotEvaluated, "공식 calibration 층 평가가 아닌 항목이 있습니다.");
        if (strata.GroupBy(s => s.StratumId).Any(g => g.Count() > 1) || strata.GroupBy(s => s.ConfigHash).Any(g => g.Count() > 1))
            return (GateStatus.NotEvaluated, "같은 층 또는 같은 실행이 두 번 있습니다(층은 합치지 않음).");
        var missing = CalibrationStrata.All.Select(s => s.Id).Except(strata.Select(s => s.StratumId!)).ToList();
        // every stratum that is not PASS is named with its own state and reason, in stratum order (the verdict order below is unchanged; no state is dropped by a higher-priority one)
        var notPass = string.Join("; ", strata.Where(s => s.Gate != GateStatus.Pass).OrderBy(s => s.StratumId, StringComparer.Ordinal).Select(s => $"{s.StratumId}={GateText.Of(s.Gate)} ({s.GateReason})"));
        // a known FAIL is never hidden by a missing stratum (FC-D7.3, FC-D9.1): the missing one cannot make it better
        if (strata.Any(s => s.Gate == GateStatus.Fail))
            return (GateStatus.Fail, $"FAIL 층: {string.Join(", ", strata.Where(s => s.Gate == GateStatus.Fail).OrderBy(s => s.StratumId, StringComparer.Ordinal).Select(s => s.StratumId))}(다른 층으로 보상하지 않음); {notPass}{(missing.Count > 0 ? $"; 평가 없는 층 {string.Join(", ", missing)}" : "")}.");
        if (missing.Count > 0)
            return (GateStatus.NotEvaluated, $"층 {string.Join(", ", missing)}의 평가가 없습니다.");
        if (strata.Any(s => s.Gate == GateStatus.InsufficientSample))
            return (GateStatus.InsufficientSample, $"표본 부족 층이 있습니다: {notPass}");
        return strata.All(s => s.Gate == GateStatus.Pass) ? (GateStatus.Pass, "L1~L4 모두 PASS.") : (GateStatus.NotEvaluated, $"판정되지 않은 층이 있습니다: {notPass}");
    }

    /// <summary>FC-D7.2: an integration run is exactly 100 games per mode, all complete; its individual metric verdicts are diagnostic only. PASS means the completion condition holds AND the metric computation succeeded.</summary>
    public static (GateStatus Gate, string Reason) Integration(RunResult run)
    {
        if (run.StopAware || run.Requested != SeedManifestReader.IntegrationGames)
            return (GateStatus.NotEvaluated, $"integration은 정지 규칙 없이 정확히 {SeedManifestReader.IntegrationGames}경기입니다.");
        if (run.CancelledCount > 0 || run.NotStartedCount > 0)
            return (GateStatus.NotEvaluated, "취소되었거나 시작하지 않은 경기가 있습니다.");
        if (run.FailedCount > 0)
            return (GateStatus.Fail, $"실패 경기 {run.FailedCount}건(FC-D9.1).");
        // a metrics ERROR (not a NOT_EVALUATED verdict, which is a result of a working computation) fails the gate like an engine failure (FC-D9.1)
        return run.MetricsComputed
            ? (GateStatus.Pass, $"{SeedManifestReader.IntegrationGames}경기 모두 완료, 지표 계산 성공(개별 지표 판정은 진단).")
            : (GateStatus.Fail, $"지표 계산 실패 {run.Metrics.Error!.Value.Code}(FC-D9.1).");
    }

    /// <summary>Canonical text of the policy, so an evaluation names exactly the rules it applied (the hash is not an approval by itself).</summary>
    internal static string Canonical(EvaluationPolicy p)
    {
        var t = new StringBuilder("evaluation-policy-text-v1\n").Append(p.Id).Append('|').Append(p.IsApproved).Append('|').Append(p.StratumMinPa).Append('\n');
        foreach (var m in p.MinDenominatorByTarget.OrderBy(x => x.Key, StringComparer.Ordinal))
            t.Append("min=").Append(m.Key).Append('|').Append(m.Value).Append('\n');
        foreach (var m in p.StratumMinimums.OrderBy(x => x.Key, StringComparer.Ordinal))
            t.Append("stratum_min=").Append(m.Key).Append('|').Append(m.Value.Same).Append('|').Append(m.Value.Opposite).Append('\n');
        foreach (var m in p.OfficialCohortByMetric.OrderBy(x => x.Key, StringComparer.Ordinal))
            t.Append("cohort=").Append(m.Key).Append('|').Append(m.Value).Append('\n');
        t.Append("standardized=").Append(string.Join(',', p.StandardizedMetrics.Order(StringComparer.Ordinal))).Append("|weights=3/7,4/7\n");
        return t.Append("approved_cohorts=").Append(string.Join(',', p.ApprovedCohortIds.Order(StringComparer.Ordinal))).Append('\n').ToString();
    }
}
