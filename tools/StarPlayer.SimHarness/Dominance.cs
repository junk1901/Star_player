using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.SimHarness;

/// <summary>ADR-008 FC-D4.1: the four batter strategies of the matrix (proposal §6.1). The letter is the cell-id abbreviation N / P / A / T.</summary>
public enum BatterStrategy { Normal, Patient, Aggressive, TakeFirst }

/// <summary>ADR-008 FC-D4.1: the three pitcher strategies (letters B / K / C).</summary>
public enum PitcherStrategy { Balanced, Attack, Careful }

/// <summary>ADR-008 FC-D4.1: the handedness condition of a cell (letters S / O): <c>neutral-v1</c> (all right-handed) or <c>neutral-opp-v1</c> (left-handed pitchers).</summary>
public enum MatrixHand { Same, Opposite }

/// <summary>
/// One strategy-matrix policy (proposal §6.1 / §6.2): both teams bat with <see cref="Batter"/> and pitch with <see cref="Pitcher"/>. A pure function of the public count only (assumption A-1, FC-D4.5):
/// Normal / Patient / Aggressive choose that approach at every decision point; TakeFirst takes exactly the 0-0 pitch and otherwise chooses Normal; the intent is fixed for every pitch. It holds no
/// state, reads no score, inning, out, base, rating or probability, and never substitutes. The constructor is private, so the twelve instances of <see cref="All"/> are the only ones.
/// </summary>
public sealed class DominancePolicy : IGamePolicy
{
    private DominancePolicy(BatterStrategy batter, PitcherStrategy pitcher) => (Batter, Pitcher, Id) = (batter, pitcher, $"dom-{Lower(batter)}-{Lower(pitcher)}-v1");

    public BatterStrategy Batter { get; }

    public PitcherStrategy Pitcher { get; }

    public string Id { get; }

    /// <summary>The twelve policies of proposal §6.6 in batter-major order (<c>dom-normal-balanced-v1</c> … <c>dom-takefirst-careful-v1</c>).</summary>
    public static IReadOnlyList<DominancePolicy> All { get; } =
        [.. from b in Enum.GetValues<BatterStrategy>() from p in Enum.GetValues<PitcherStrategy>() select new DominancePolicy(b, p)];

    public static DominancePolicy Of(BatterStrategy batter, PitcherStrategy pitcher) => All[(int)batter * Enum.GetValues<PitcherStrategy>().Length + (int)pitcher];

    public PaDecision AtPlateAppearance(GameState state) => Batter switch
    {
        BatterStrategy.Patient => PaDecision.Choose(ApproachKind.Patient),
        BatterStrategy.Aggressive => PaDecision.Choose(ApproachKind.Aggressive),
        BatterStrategy.TakeFirst when state.Count == PitchCountState.Zero => PaDecision.TakeOne,
        _ => PaDecision.Choose(ApproachKind.Normal),
    };

    public PitchIntent IntentFor(GameState state) => Pitcher switch
    {
        PitcherStrategy.Attack => PitchIntent.Attack,
        PitcherStrategy.Careful => PitchIntent.Careful,
        _ => PitchIntent.Balanced,
    };

    internal static string Lower<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();
}

/// <summary>
/// One cell of the ADR-008 FC-D4 matrix exactly as tabulated in proposal §6.6: number, cell id, the four axes, cohort, rules profile, policy, competition key and its two comparison columns
/// (batter side <c>&lt;Pitcher&gt;&lt;Hand&gt;&lt;Mode&gt;</c>, pitcher side <c>&lt;Batter&gt;&lt;Hand&gt;&lt;Mode&gt;</c>).
/// </summary>
public sealed record DominanceCell(
    int Number, string Id, BatterStrategy Batter, PitcherStrategy Pitcher, MatrixHand Hand, CalledPitchMode Mode, string CohortId, string RulesProfileId, string PolicyId, string CompetitionKey,
    string BatterColumn, string PitcherColumn)
{
    /// <summary>The handedness stratum every game of this cell must have (<see cref="CohortInputs.StratumOf"/>).</summary>
    public HandednessStratum Stratum => Hand == MatrixHand.Same ? HandednessStratum.Same : HandednessStratum.Opposite;
}

/// <summary>
/// The declared 48-cell matrix (ADR-008 FC-D4.1): generated from the approved axes — hand × mode × batter × pitcher, in that nesting, which is the row order of proposal §6.6 — so no cell can be
/// missing, repeated or mis-labelled by hand. <see cref="Config"/> builds a cell's run on the fixed official profile (FC-D7.5: model / tuning v4, the cell's fictional-test rules, advancement v2,
/// neutral park, budget 3,000); a cell runs as its OWN full-execution run with its own competition key (ADR-009 SR-D5: no stop rule).
/// </summary>
public static class DominanceMatrix
{
    public const string Family = "fc3-dom";

    /// <summary>The 48 cells in the row order of proposal §6.6. A <see cref="ReadOnlyCollection{T}"/> over a private list: a cast to <c>IList</c> / <c>ICollection</c> refuses every change, and no mutable reference leaves this class.</summary>
    public static IReadOnlyList<DominanceCell> Cells { get; } = new ReadOnlyCollection<DominanceCell>(Build());

    /// <summary>The same cell instances by competition key; a <see cref="FrozenDictionary{TKey,TValue}"/> built from <see cref="Cells"/>, so a cast to <c>IDictionary</c> refuses every change.</summary>
    public static IReadOnlyDictionary<string, DominanceCell> ByCompetitionKey { get; } = Cells.ToFrozenDictionary(c => c.CompetitionKey, StringComparer.Ordinal);

    private static List<DominanceCell> Build()
    {
        static char B(BatterStrategy b) => "NPAT"[(int)b];
        static char P(PitcherStrategy p) => "BKC"[(int)p];
        static char Hd(MatrixHand h) => "SO"[(int)h];
        static char M(CalledPitchMode m) => m == CalledPitchMode.Automated ? 'A' : 'N';
        var cells = new List<DominanceCell>();
        foreach (var hand in Enum.GetValues<MatrixHand>())
            foreach (var mode in new[] { CalledPitchMode.Automated, CalledPitchMode.NonAutomated })
                foreach (var b in Enum.GetValues<BatterStrategy>())
                    foreach (var p in Enum.GetValues<PitcherStrategy>())
                    {
                        string hm = $"{Hd(hand)}{M(mode)}", bp = $"{B(b)}{P(p)}";
                        cells.Add(new DominanceCell(cells.Count + 1, $"DOM-{hm}-{bp}", b, p, hand, mode, hand == MatrixHand.Same ? NeutralCohort.Id : NeutralCohort.OppositeId,
                            mode == CalledPitchMode.Automated ? HarnessData.AutomatedProfile : HarnessData.NonAutomatedProfile, DominancePolicy.Of(b, p).Id,
                            $"{Family}-{hm.ToLowerInvariant()}-{bp.ToLowerInvariant()}", $"{P(p)}{hm}", $"{B(b)}{hm}"));
                    }

        return cells;
    }

    /// <summary>The run configuration of <paramref name="cell"/>: every input from the registries and the fixed official profile, nothing from a caller.</summary>
    public static Result<RunConfig> Config(DominanceCell cell, string runId)
    {
        var tuning = HarnessData.LoadTuningV4();
        var rules = HarnessData.LoadRules(cell.RulesProfileId);
        var park = HarnessData.NeutralPark();
        var cohort = CohortRegistry.Resolve(cell.CohortId);
        var policy = PolicyRegistry.Resolve(cell.PolicyId);
        if (new Error?[] { tuning.Error, rules.Error, park.Error, cohort.Error, policy.Error }.FirstOrDefault(e => e is not null) is { } error)
            return Result<RunConfig>.Failure(error);
        return RunConfig.Create(runId, "dominance", cohort.Value, rules.Value, tuning.Value, park.Value, OfficialProfile.AdvancementPolicyVersion, OfficialProfile.EventBudget, policy.Value,
            cell.CompetitionKey);
    }

    /// <summary>Plays one cell over the given seeds as a full-execution run (every seed, in order; a failed game stays in the result; no stop rule).</summary>
    public static RunResult Run(DominanceCell cell, IReadOnlyList<ulong> seeds, string? runId = null, CancellationToken cancellation = default)
    {
        var config = Config(cell, runId ?? cell.Id.ToLowerInvariant());
        return SimRunner.Run(config.IsSuccess ? config.Value : throw new InvalidOperationException($"{config.Error!.Value.Code}: {config.Error.Value.Message}"), seeds, null, cancellation);
    }

    /// <summary>
    /// The cell a validated <c>fc3-dom</c> seed manifest belongs to: its competition key names exactly one cell and its cohort, policy and rules profile are that cell's; a PA target is refused
    /// (SR-D5: the matrix keeps the full-execution contract). The manifest must already have been read against its external pin (<see cref="SeedManifestReader.Read"/>).
    /// </summary>
    public static Result<DominanceCell> CellOf(SeedManifest manifest)
    {
        static Result<DominanceCell> Bad(string code, string message) => Result<DominanceCell>.Failure(new Error(code, message));
        if (manifest.Family != Family)
            return Bad("Dominance.family_invalid", $"전략 행렬 Seed 파일의 family는 {Family}여야 합니다(받은 값 '{manifest.Family}').");
        if (!ByCompetitionKey.TryGetValue(manifest.CompetitionKey, out var cell))
            return Bad("Dominance.cell_unknown", $"Competition Key '{manifest.CompetitionKey}'는 48셀 행렬의 셀이 아닙니다.");
        if (manifest.CohortId != cell.CohortId || manifest.PolicyId != cell.PolicyId || manifest.RulesProfileId != cell.RulesProfileId)
            return Bad("Dominance.cell_binding_mismatch", $"{cell.Id}의 코호트·정책·규칙은 {cell.CohortId}·{cell.PolicyId}·{cell.RulesProfileId}입니다(Seed 파일: {manifest.CohortId}·{manifest.PolicyId}·{manifest.RulesProfileId}).");
        return manifest.PaTarget is not null
            ? Bad("Dominance.stop_rule_not_allowed", "fc3-dom은 전체 실행 계약이며 pa_target·정지 규칙을 쓰지 않습니다(ADR-009 SR-D5).")
            : Result<DominanceCell>.Success(cell);
    }
}

/// <summary>
/// The strategy-matrix observation of one finished game (FC-D4.2, proposal §6.3): the runs of its 16 half-innings of innings 1-8 (top 1, bottom 1, …, bottom 8), read from the engine's line score and
/// cross-checked against a second derivation, the runs of the scored plays of the same half-inning. A short line score, a line total that is not the final score, or a disagreement is an error code
/// (a metrics failure of the cell), never a repaired value.
/// </summary>
public sealed record RegulationRuns(ImmutableArray<int> HalfInnings, string? ErrorCode)
{
    public const int Innings = 8;

    public bool Ok => ErrorCode is null;

    /// <summary>The game's runs over the 16 half-innings; the game observation of FC-D4.2 is this total / 16.</summary>
    public int Total => HalfInnings.Sum();

    public bool SameAs(RegulationRuns? other) => other is not null && ErrorCode == other.ErrorCode && HalfInnings.SequenceEqual(other.HalfInnings);

    internal static RegulationRuns Of(GameState s) =>
        Compare(s.AwayLine, s.HomeLine, s.AwayRuns, s.HomeRuns, s.Plays.GroupBy(p => (p.Inning, p.Half)).ToDictionary(g => g.Key, g => g.Sum(p => p.Runs.Count)));

    internal static RegulationRuns Compare(IReadOnlyList<int> away, IReadOnlyList<int> home, int awayRuns, int homeRuns, IReadOnlyDictionary<(int Inning, InningHalf Half), int> fromPlays)
    {
        static RegulationRuns Error(string code) => new(ImmutableArray<int>.Empty, code);
        if (away.Count < Innings || home.Count < Innings)
            return Error("Dominance.regulation_incomplete");
        if (away.Sum() != awayRuns || home.Sum() != homeRuns)
            return Error("Dominance.line_score_total_mismatch");
        var runs = ImmutableArray.CreateBuilder<int>(2 * Innings);
        for (var inning = 1; inning <= Innings; inning++)
            foreach (var (half, line) in new[] { (InningHalf.Top, away), (InningHalf.Bottom, home) })
            {
                var value = line[inning - 1];
                if (value < 0 || value != fromPlays.GetValueOrDefault((inning, half)))
                    return Error("Dominance.line_score_mismatch");
                runs.Add(value);
            }

        return new RegulationRuns(runs.MoveToImmutable(), null);
    }
}

/// <summary>The game-cluster sample of one cell (FC-D4.2): n games and the sum and sum of squares of each game's innings-1-8 runs. Exact integers; the observation of a game is its total / 16.</summary>
public sealed record CellSample(int Games, long TotalRuns, long SumOfSquares)
{
    public const int HalfInnings = 2 * RegulationRuns.Innings;

    public static CellSample Of(IEnumerable<int> gameRuns)
    {
        int n = 0;
        long t = 0, q = 0;
        foreach (var r in gameRuns)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(r);
            checked { n++; t += r; q += (long)r * r; }
        }

        return new CellSample(n, t, q);
    }

    /// <summary>R̄ = total runs / (16 · games), in runs per half-inning.</summary>
    public double Mean => TotalRuns / ((double)HalfInnings * Games);

    /// <summary>The sample variance of the game observations (runs / 16): <c>(n·Q − T²) / (256 · n · (n − 1))</c>, the numerator in exact 128-bit integers; null below two games.</summary>
    public double? Variance => Games < 2 ? null : (double)((Int128)Games * SumOfSquares - (Int128)TotalRuns * TotalRuns) / ((double)HalfInnings * HalfInnings * Games * (Games - 1));
}

public enum StrategySide { Batter, Pitcher }

/// <summary>ADR-008 FC-D4.4: the four verdicts of one side.</summary>
public enum SideVerdict { NotEvaluated, DominanceDetected, NoDominanceSupported, Inconclusive }

/// <summary>
/// One (τ, column) comparison of a strategy σ (proposal §6.7): the difference D in runs per half-inning (positive when σ is better: batter <c>R̄(σ) − R̄(τ)</c>, pitcher <c>R̄(τ) − R̄(σ)</c>), the unpaired
/// Welch standard error, the one-sided lower bound <c>D − z(1−α)·SE</c> and its superiority flag, the non-superiority statistic <c>Z = (δ − D) / SE</c>, and its Holm critical value (null after the
/// Holm sequence stopped) and rejection.
/// </summary>
public sealed record DominanceWitness(
    string Rival, string Column, string StrategyCell, string RivalCell, double Difference, double StandardError, double LowerBound, bool Superior, double NonSuperiorityZ, double? HolmCritical, bool HolmRejected);

/// <summary>
/// The result for one strategy σ: <see cref="Dominant"/> (IUT: every witness superior), <see cref="NotDominant"/> (at least one Holm rejection of H0 "D ≥ δ"), and the label — dominance takes precedence
/// when both hold (FC-D4.3, proposal §6.4-3).
/// </summary>
public sealed record StrategyVerdict(string Strategy, StrategySide Side, bool Dominant, bool NotDominant, IReadOnlyList<DominanceWitness> Witnesses)
{
    public string Label => Dominant ? "DOMINANT" : NotDominant ? "NOT_DOMINANT" : "UNDETERMINED";
}

public sealed record SideResult(StrategySide Side, SideVerdict Verdict, string Reason, IReadOnlyList<StrategyVerdict> Strategies)
{
    public static string Text(SideVerdict v) => v switch
    {
        SideVerdict.DominanceDetected => "DOMINANCE_DETECTED", SideVerdict.NoDominanceSupported => "NO_DOMINANCE_SUPPORTED", SideVerdict.Inconclusive => "INCONCLUSIVE", _ => "NOT_EVALUATED",
    };
}

/// <summary>
/// The approved statistics of ADR-008 FC-D4.2 / proposal §6.7: δ = 0.05 runs per half-inning, α = 0.05 one-sided, game-cluster variance, unpaired Welch z, IUT superiority without adjustment inside σ,
/// Holm non-superiority over |W_σ| (36 batter, 32 pitcher), dominance precedence, no paired test. Target power 0.8 and the per-cell sample size belong to the sample-size freeze, not to this verdict.
/// Pure and deterministic: only IEEE arithmetic, square roots and the fixed normal-quantile approximation below; no RNG, no clock.
/// </summary>
public static class DominanceStatistics
{
    public const double Delta = 0.05;
    public const double Alpha = 0.05;

    /// <summary>The approved target power (FC-D4.2). It sizes the frozen per-cell sample (FC-D4.7, Final Execution Freeze); no verdict reads it.</summary>
    public const double TargetPower = 0.8;

    /// <summary>z(1 − α) of the one-sided superiority bound.</summary>
    public static readonly double ZAlpha = UpperQuantile(Alpha);

    /// <summary>
    /// z with P(Z ≥ z) = <paramref name="tail"/> for 0 &lt; tail &lt; 1: P. J. Acklam's rational approximation of the normal quantile (relative error below 1.15e-9). A technical choice, not a policy:
    /// it only fixes the numerical method of the approved α / Holm thresholds.
    /// </summary>
    public static double UpperQuantile(double tail)
    {
        if (!(tail > 0 && tail < 1))
            throw new ArgumentOutOfRangeException(nameof(tail));
        double[] a = [-3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00];
        double[] b = [-5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01];
        double[] c = [-7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00];
        double[] d = [7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00];
        const double low = 0.02425;
        var p = 1 - tail;                                                                      // the lower-tail probability whose quantile is wanted
        if (tail < low)                                                                        // upper region: by symmetry the negated lower-region formula at the tail itself
        {
            var q = Math.Sqrt(-2 * Math.Log(tail));
            return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        if (p < low)
        {
            var q = Math.Sqrt(-2 * Math.Log(p));
            return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        var m = p - 0.5;
        var r = m * m;
        return (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * m / (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
    }

    /// <summary>The one-sided (1 − α) lower bound <c>D − z(1−α)·SE</c>; superiority is <c>LowerBound &gt; 0</c> (strict).</summary>
    public static double LowerBound(double difference, double standardError) => difference - ZAlpha * standardError;

    /// <summary>Superiority of one witness: the one-sided lower bound is strictly positive.</summary>
    public static bool Superior(double difference, double standardError) => LowerBound(difference, standardError) > 0;

    /// <summary><c>Z = (δ − D) / SE</c> for H0 "D ≥ δ". Defined only for SE &gt; 0: a zero standard error (both cells constant) makes the Welch z undefined, and the evaluation refuses it.</summary>
    public static double NonSuperiorityZ(double difference, double standardError) =>
        standardError > 0 ? (Delta - difference) / standardError : throw new ArgumentOutOfRangeException(nameof(standardError), "표준오차가 0이면 Welch z가 정의되지 않습니다.");

    /// <summary>
    /// Holm's step-down procedure at α over the given statistics (larger Z = smaller p): sorted by Z descending (ties by input position), step k (from 0) rejects when <c>Z ≥ z(1 − α/(m − k))</c>
    /// (inclusive, i.e. p ≤ α/(m − k)) and the sequence stops at the first non-rejection. Returns per input position the critical value used (null after the stop) and the rejection.
    /// </summary>
    public static (double? Critical, bool Rejected)[] Holm(IReadOnlyList<double> z)
    {
        var result = new (double?, bool)[z.Count];
        var order = Enumerable.Range(0, z.Count).OrderByDescending(i => z[i]).ThenBy(i => i).ToList();
        for (var k = 0; k < order.Count; k++)
        {
            var critical = UpperQuantile(Alpha / (order.Count - k));
            var rejected = z[order[k]] >= critical;
            result[order[k]] = (critical, rejected);
            if (!rejected)
                break;
        }

        return result;
    }

    /// <summary>
    /// Both sides of the matrix from the samples of all 48 cells (keyed by cell id; each with at least two games). The caller (<see cref="DominanceEvaluator"/>) guarantees completeness. A comparison of
    /// two zero-variance cells has no defined Welch z, so both sides are then NOT_EVALUATED (never treated as infinitely precise).
    /// </summary>
    public static (SideResult Batter, SideResult Pitcher) Evaluate(IReadOnlyDictionary<string, CellSample> samples)
    {
        var cells = DominanceMatrix.Cells;
        if (cells.Any(c => !samples.TryGetValue(c.Id, out var s) || s.Games < 2))
            throw new ArgumentException("48셀 모두 2경기 이상의 표본이 있어야 합니다.", nameof(samples));
        DominanceCell Cell(BatterStrategy b, PitcherStrategy p, MatrixHand h, CalledPitchMode m) => cells.Single(c => c.Batter == b && c.Pitcher == p && c.Hand == h && c.Mode == m);
        var constant = cells.Where(c => samples[c.Id].Variance == 0).ToList();
        var degenerate = constant.SelectMany(c => constant.Where(o => o.Number > c.Number && o.Hand == c.Hand && o.Mode == c.Mode && (o.Pitcher == c.Pitcher || o.Batter == c.Batter))
            .Select(o => $"{c.Id}/{o.Id}")).ToList();
        if (degenerate.Count > 0)
        {
            var why = $"분산 0인 셀끼리의 비교가 있어 Welch z가 정의되지 않습니다: {string.Join(", ", degenerate)}.";
            return (new SideResult(StrategySide.Batter, SideVerdict.NotEvaluated, why, []), new SideResult(StrategySide.Pitcher, SideVerdict.NotEvaluated, why, []));
        }

        DominanceWitness Witness(string rival, string column, DominanceCell mine, DominanceCell theirs, bool batterSide)
        {
            CellSample sm = samples[mine.Id], st = samples[theirs.Id];
            var d = batterSide ? sm.Mean - st.Mean : st.Mean - sm.Mean;
            var se = Math.Sqrt(sm.Variance!.Value / sm.Games + st.Variance!.Value / st.Games);
            return new DominanceWitness(rival, column, mine.Id, theirs.Id, d, se, LowerBound(d, se), Superior(d, se), NonSuperiorityZ(d, se), null, false);
        }

        StrategyVerdict Judge(string strategy, StrategySide side, List<DominanceWitness> witnesses)
        {
            var holm = Holm(witnesses.Select(w => w.NonSuperiorityZ).ToList());
            var judged = witnesses.Select((w, i) => w with { HolmCritical = holm[i].Critical, HolmRejected = holm[i].Rejected }).ToList();
            return new StrategyVerdict(strategy, side, judged.All(w => w.Superior), judged.Any(w => w.HolmRejected), judged);
        }

        var hands = Enum.GetValues<MatrixHand>();
        var modes = new[] { CalledPitchMode.Automated, CalledPitchMode.NonAutomated };
        var batter = new List<StrategyVerdict>();
        foreach (var sigma in Enum.GetValues<BatterStrategy>())
        {
            var w = new List<DominanceWitness>();
            foreach (var tau in Enum.GetValues<BatterStrategy>().Where(t => t != sigma))
                foreach (var h in hands) foreach (var m in modes) foreach (var p in Enum.GetValues<PitcherStrategy>())
                {
                    var mine = Cell(sigma, p, h, m);
                    w.Add(Witness(tau.ToString(), mine.BatterColumn, mine, Cell(tau, p, h, m), true));
                }

            batter.Add(Judge(sigma.ToString(), StrategySide.Batter, w));
        }

        var pitcher = new List<StrategyVerdict>();
        foreach (var sigma in Enum.GetValues<PitcherStrategy>())
        {
            var w = new List<DominanceWitness>();
            foreach (var tau in Enum.GetValues<PitcherStrategy>().Where(t => t != sigma))
                foreach (var h in hands) foreach (var m in modes) foreach (var b in Enum.GetValues<BatterStrategy>())
                {
                    var mine = Cell(b, sigma, h, m);
                    w.Add(Witness(tau.ToString(), mine.PitcherColumn, mine, Cell(b, tau, h, m), false));
                }

            pitcher.Add(Judge(sigma.ToString(), StrategySide.Pitcher, w));
        }

        return (Side(StrategySide.Batter, batter), Side(StrategySide.Pitcher, pitcher));
    }

    /// <summary>FC-D4.4 / proposal §6.4-4: DOMINANCE_DETECTED if some σ is dominant; NO_DOMINANCE_SUPPORTED if every σ is shown not dominant; otherwise INCONCLUSIVE (lack of evidence is never a pass).</summary>
    private static SideResult Side(StrategySide side, IReadOnlyList<StrategyVerdict> strategies)
    {
        if (strategies.Where(s => s.Dominant).Select(s => s.Strategy).ToList() is { Count: > 0 } dominant)
            return new SideResult(side, SideVerdict.DominanceDetected, $"지배 전략: {string.Join(", ", dominant)}(모든 비교에서 단측 하한 > 0, IUT).", strategies);
        if (strategies.All(s => s.NotDominant))
            return new SideResult(side, SideVerdict.NoDominanceSupported, "모든 전략에서 δ 미만 우위 대결이 Holm으로 확인되었습니다.", strategies);
        return new SideResult(side, SideVerdict.Inconclusive,
            $"지배도 비지배도 확인되지 않은 전략: {string.Join(", ", strategies.Where(s => !s.NotDominant).Select(s => s.Strategy))}(검정력 부족 포함; PASS 아님).", strategies);
    }
}

/// <summary>What happened to one cell's evidence. Only <see cref="Ok"/> enters the statistics.</summary>
public enum CellState { Ok, Missing, IntegrityViolation, Incomplete, EngineFailure, MetricsFailure, InsufficientSample }

/// <summary>The evaluation of one cell: its state and reason, the run's (unchanged) config and <c>run-result-v1</c> hashes, the plan size, the failure counts of a trusted run, and the sample (Ok only).</summary>
public sealed record CellEvaluation(DominanceCell Cell, CellState State, string Reason, string? ConfigHash, string? RunResultHash, int PlannedGames, int FailedGames, int MetricsFailures, CellSample? Sample);

/// <summary>The dominance gate (ADR-008 FC-D4.4, FC-D9; proposal §6.5 / §6.7 row 8). Separate from any exit code; <see cref="Incomplete"/> is the INCONCLUSIVE outcome, which the owner resolves.</summary>
public enum DominanceGate { NotEvaluated, InsufficientSample, Incomplete, Pass, Fail }

public sealed record DominanceEvaluation(
    string PolicyId, int? FrozenGamesPerCell, IReadOnlyList<CellEvaluation> Cells, IReadOnlyList<string> SetProblems, SideResult Batter, SideResult Pitcher, DominanceGate Gate, string GateReason)
{
    public static string Text(DominanceGate g) => g switch
    {
        DominanceGate.Pass => "PASS", DominanceGate.Fail => "FAIL", DominanceGate.Incomplete => "INCOMPLETE", DominanceGate.InsufficientSample => "INSUFFICIENT_SAMPLE", _ => "NOT_EVALUATED",
    };

    /// <summary>A complete, culture-invariant text of the evaluation (doubles round-trip), so two evaluations can be compared exactly. A comparison text only; it is not an official hash.</summary>
    public string Canonical()
    {
        static string D(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        var t = new StringBuilder("dominance-evaluation-text-v1\n").Append(PolicyId).Append('|').Append(FrozenGamesPerCell?.ToString(CultureInfo.InvariantCulture) ?? "unfrozen").Append('|').Append(Text(Gate)).Append('|').Append(GateReason).Append('\n');
        foreach (var p in SetProblems)
            t.Append("set|").Append(p).Append('\n');
        foreach (var c in Cells)
            t.Append("cell|").Append(c.Cell.Id).Append('|').Append(c.State).Append('|').Append(c.Reason).Append('|').Append(c.ConfigHash ?? "-").Append('|').Append(c.RunResultHash ?? "-").Append('|')
                .Append(c.PlannedGames).Append('|').Append(c.FailedGames).Append('|').Append(c.MetricsFailures).Append('|')
                .Append(c.Sample is { } s ? $"{s.Games},{s.TotalRuns},{s.SumOfSquares}" : "-").Append('\n');
        foreach (var side in new[] { Batter, Pitcher })
        {
            t.Append("side|").Append(side.Side).Append('|').Append(SideResult.Text(side.Verdict)).Append('|').Append(side.Reason).Append('\n');
            foreach (var s in side.Strategies)
            {
                t.Append("strategy|").Append(s.Strategy).Append('|').Append(s.Dominant).Append('|').Append(s.NotDominant).Append('\n');
                foreach (var w in s.Witnesses)
                    t.Append("w|").Append(w.Rival).Append('|').Append(w.Column).Append('|').Append(D(w.Difference)).Append('|').Append(D(w.StandardError)).Append('|').Append(D(w.LowerBound)).Append('|')
                        .Append(w.Superior).Append('|').Append(D(w.NonSuperiorityZ)).Append('|').Append(w.HolmCritical is { } h ? D(h) : "-").Append('|').Append(w.HolmRejected).Append('\n');
            }
        }

        return t.ToString();
    }
}

/// <summary>
/// The 48-cell dominance check (ADR-008 FC-D4, FC-D9). Each cell's run is accepted only as that cell's full-execution evidence (binding, profile, plan, hashes, handedness); statistics are computed
/// only when all 48 cells are Ok and have the same plan size, and the gate is decided in this order: a failed game or a metrics failure in any trusted cell run → FAIL (FC-D9.1, never re-aggregated
/// without it); a missing, untrusted or incomplete cell or a set problem → NOT_EVALUATED; only too-small cells → INSUFFICIENT_SAMPLE; otherwise the side verdicts — DOMINANCE_DETECTED on either side
/// → FAIL, both NO_DOMINANCE_SUPPORTED → PASS, else INCOMPLETE. Every non-Ok cell is named in the reason, so no state hides another. Cell order and run order do not matter (cells are matched by key).
/// </summary>
public static class DominanceEvaluator
{
    public const string PolicyId = "fc3-dominance-v1";

    /// <summary>The evaluation against the current approvals (<see cref="OfficialApprovals.Current"/>: the per-cell sample size is not frozen, so the gate cannot be decided yet).</summary>
    public static DominanceEvaluation Evaluate(IEnumerable<RunResult?> runs) => Evaluate(runs, OfficialApprovals.Current.DominanceGamesPerCell);

    /// <summary>
    /// <paramref name="frozenGamesPerCell"/> is the frozen per-cell sample size (FC-D4.7, Final Execution Freeze). Unfrozen (null): the statistics are still computed as a diagnostic, but the gate is
    /// NOT_EVALUATED. Frozen: a cell with fewer games is INSUFFICIENT_SAMPLE and a cell planned with more is not the frozen plan.
    /// </summary>
    public static DominanceEvaluation Evaluate(IEnumerable<RunResult?> runs, int? frozenGamesPerCell)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var setProblems = new List<string>();
        var byKey = new Dictionary<string, List<RunResult>>(StringComparer.Ordinal);
        foreach (var run in runs)
        {
            if (run is null)
                setProblems.Add("빈 실행 결과가 있습니다.");
            else if (!DominanceMatrix.ByCompetitionKey.ContainsKey(run.Config.CompetitionKey))
                setProblems.Add($"행렬 셀이 아닌 실행이 있습니다(Competition Key '{run.Config.CompetitionKey}').");
            else
                (byKey.TryGetValue(run.Config.CompetitionKey, out var list) ? list : byKey[run.Config.CompetitionKey] = []).Add(run);
        }

        var minimum = Math.Max(2, frozenGamesPerCell ?? 2);
        var cells = DominanceMatrix.Cells.Select(c => Cell(c, byKey.GetValueOrDefault(c.CompetitionKey), minimum)).ToList();
        var planned = cells.Where(c => c.State is not (CellState.Missing or CellState.IntegrityViolation)).Select(c => c.PlannedGames).Distinct().ToList();
        if (planned.Count > 1)
            setProblems.Add("셀마다 계획 경기 수가 다릅니다(proposal §6.6: 모든 셀 같은 n).");
        if (frozenGamesPerCell is { } frozen && planned.Any(n => n > frozen))
            setProblems.Add($"동결된 셀당 경기 수 {frozen}보다 많은 계획이 있습니다(동결된 계획이 아님).");

        var notOk = cells.Where(c => c.State != CellState.Ok).ToList();
        var listed = string.Join("; ", notOk.Select(c => $"{c.Cell.Id}={c.State} ({c.Reason})").Concat(setProblems));
        SideResult NotEvaluated(StrategySide side) => new(side, SideVerdict.NotEvaluated, "48셀 전체가 평가 가능한 상태가 아닙니다.", []);
        DominanceEvaluation Done(SideResult b, SideResult p, DominanceGate gate, string reason) => new(PolicyId, frozenGamesPerCell, cells, setProblems, b, p, gate, reason);

        var failing = cells.Where(c => c.State != CellState.IntegrityViolation && (c.FailedGames > 0 || c.MetricsFailures > 0)).ToList();
        if (failing.Count > 0)
            return Done(NotEvaluated(StrategySide.Batter), NotEvaluated(StrategySide.Pitcher), DominanceGate.Fail,
                $"경기 실패 또는 지표 실패가 있는 셀: {string.Join(", ", failing.Select(c => c.Cell.Id))}(FC-D9.1; 제외 재집계 없음); {listed}.");
        if (setProblems.Count > 0 || notOk.Any(c => c.State is CellState.Missing or CellState.IntegrityViolation or CellState.Incomplete))
            return Done(NotEvaluated(StrategySide.Batter), NotEvaluated(StrategySide.Pitcher), DominanceGate.NotEvaluated, $"평가할 수 없는 셀 또는 실행 집합: {listed}.");
        if (notOk.Count > 0)
            return Done(NotEvaluated(StrategySide.Batter), NotEvaluated(StrategySide.Pitcher), DominanceGate.InsufficientSample, $"표본 부족 셀: {listed}.");

        var (batter, pitcher) = DominanceStatistics.Evaluate(cells.ToDictionary(c => c.Cell.Id, c => c.Sample!));
        if (frozenGamesPerCell is null)
            return Done(batter, pitcher, DominanceGate.NotEvaluated,
                $"셀당 공식 표본 수가 아직 동결되지 않았습니다(ADR-008 FC-D4.7, Final Execution Freeze). 진단: 타자 측 {SideResult.Text(batter.Verdict)}, 투수 측 {SideResult.Text(pitcher.Verdict)}.");
        var (gate, reason) = GateOf(batter, pitcher);
        return Done(batter, pitcher, gate, reason);
    }

    /// <summary>FC-D4.4: PASS only when both sides are NO_DOMINANCE_SUPPORTED; DOMINANCE_DETECTED on either side is FAIL; anything else (INCONCLUSIVE, NOT_EVALUATED) is never PASS.</summary>
    internal static (DominanceGate Gate, string Reason) GateOf(SideResult batter, SideResult pitcher)
    {
        var verdicts = $"타자 측 {SideResult.Text(batter.Verdict)}, 투수 측 {SideResult.Text(pitcher.Verdict)}";
        if (batter.Verdict == SideVerdict.DominanceDetected || pitcher.Verdict == SideVerdict.DominanceDetected)
            return (DominanceGate.Fail, $"{verdicts}: 지배 전략이 검출되었습니다.");
        if (batter.Verdict == SideVerdict.NotEvaluated || pitcher.Verdict == SideVerdict.NotEvaluated)
            return (DominanceGate.NotEvaluated, $"{verdicts}: 평가되지 않은 측이 있습니다.");
        return batter.Verdict == SideVerdict.NoDominanceSupported && pitcher.Verdict == SideVerdict.NoDominanceSupported
            ? (DominanceGate.Pass, $"{verdicts}: 양 측 모두 지배 전략 없음이 지지됩니다.")
            : (DominanceGate.Incomplete, $"{verdicts}: 증거가 부족해 PASS가 아닙니다(INCONCLUSIVE, 소유자 결정).");
    }

    private static CellEvaluation Cell(DominanceCell cell, List<RunResult>? runs, int minimum)
    {
        if (runs is null)
            return new CellEvaluation(cell, CellState.Missing, "실행 결과가 없습니다.", null, null, 0, 0, 0, null);
        if (runs.Count > 1)
            return new CellEvaluation(cell, CellState.IntegrityViolation, $"같은 셀의 실행이 {runs.Count}개 있습니다.", null, null, 0, 0, 0, null);
        var run = runs[0];
        if (IntegrityProblem(cell, run) is { } problem)
            return new CellEvaluation(cell, CellState.IntegrityViolation, problem, run.ConfigHash, run.RunResultHash, run.Specs.Count, 0, 0, null);

        var failed = run.FailedCount;
        var regulation = run.Games.Count(g => g.Completed && g.Regulation is not { Ok: true });
        var computation = run.MetricsComputed ? 0 : 1;                                         // FC-D9.1: a failed metric computation of the run (e.g. Metrics.counts_inconsistent) is a metrics failure like a failed observation
        var metrics = regulation + computation;
        var incomplete = run.Games.Count < run.Specs.Count || run.CancelledCount > 0;
        var notes = new List<string>();
        if (incomplete)
            notes.Add($"계획 {run.Specs.Count}경기 중 결과 {run.Games.Count}경기, 취소 {run.CancelledCount}경기(전체 실행 아님)");
        if (failed > 0)
            notes.Add($"실패 경기 {failed}건({string.Join(", ", run.Games.Where(g => g.Status == GameRunStatus.Failed).Select(g => $"{g.Game.GameIndex}:{g.Failure?.Code}"))})");
        if (computation > 0)
            notes.Add($"지표 계산 실패({run.Metrics.Error!.Value.Code})");
        if (regulation > 0)
            notes.Add($"지표(1~8회 반이닝 득점) 실패 {regulation}건({string.Join(", ", run.Games.Where(g => g.Completed && g.Regulation is not { Ok: true }).Select(g => $"{g.Game.GameIndex}:{g.Regulation?.ErrorCode ?? "없음"}"))})");
        var state = incomplete ? CellState.Incomplete : failed > 0 ? CellState.EngineFailure : metrics > 0 ? CellState.MetricsFailure : run.Games.Count < minimum ? CellState.InsufficientSample : CellState.Ok;
        if (state == CellState.InsufficientSample)
            notes.Add($"{run.Games.Count}경기 < 필요 {minimum}경기(경기 단위 분산은 2경기 이상, 공식 판정은 동결된 셀당 경기 수)");
        var sample = state == CellState.Ok ? CellSample.Of(run.Games.Select(g => g.Regulation!.Total)) : null;
        return new CellEvaluation(cell, state, notes.Count == 0 ? "평가 가능" : string.Join("; ", notes), run.ConfigHash, run.RunResultHash, run.Specs.Count, failed, metrics, sample);
    }

    /// <summary>Why <paramref name="run"/> is not trustworthy evidence of <paramref name="cell"/>, or null.</summary>
    private static string? IntegrityProblem(DominanceCell cell, RunResult run)
    {
        var c = run.Config;
        if (run.StopAware || run.Stop is not null)
            return "정지 인식 실행입니다(fc3-dom은 전체 실행 계약, ADR-009 SR-D5).";
        if (c.CompetitionKey != cell.CompetitionKey || c.Cohort.CohortId != cell.CohortId || c.Rules.ProfileId != cell.RulesProfileId
            || c.Policy is not DominancePolicy policy || policy.Batter != cell.Batter || policy.Pitcher != cell.Pitcher || c.Policy.Id != cell.PolicyId)
            return $"셀 결속 불일치: Competition Key·코호트·규칙·정책이 {cell.CompetitionKey}·{cell.CohortId}·{cell.RulesProfileId}·{cell.PolicyId}가 아닙니다.";
        if (c.Rules.CalledPitchMode != cell.Mode)
            return $"규칙 프로파일의 CalledPitchMode가 셀의 {cell.Mode}가 아닙니다.";
        if (CohortRegistry.Resolve(cell.CohortId) is not { IsSuccess: true } registered || registered.Value.InputsHash != c.Cohort.InputsHash)
            return $"코호트 '{cell.CohortId}'의 InputsHash가 등록된 코호트와 다릅니다.";
        if ((c.Execution.ProbabilityModelVersion, c.Execution.TuningVersion, c.Execution.TuningHash, c.AdvancementPolicyVersion, OfficialProfile.ParkTextOf(c.Park), c.EventBudget)
            != (OfficialProfile.ModelVersion, OfficialProfile.TuningVersion, OfficialProfile.TuningHash, OfficialProfile.AdvancementPolicyVersion, OfficialProfile.ParkText, OfficialProfile.EventBudget)
            || OfficialProfile.RulesHashes.GetValueOrDefault(cell.RulesProfileId) != c.Rules.ContentHash || c.CheckPins() is not null)
            return "모델·튜닝·규칙·진루·구장·이벤트 예산이 고정된 실행 프로파일(FC-D7.5)과 다릅니다.";
        if (run.Specs.Count == 0 || run.Specs.Where((s, i) => s.GameIndex != i + 1 || c.Spec(s.GameIndex, s.Seed) != s).Any())
            return "계획 경기 목록이 비었거나 index·GameId가 규칙(game:{key}:{cohort}-{index})과 다릅니다.";
        if (c.ConfigHash(run.Specs) != run.ConfigHash)
            return "config_hash가 계획 경기 목록으로 다시 계산한 값과 다릅니다.";
        if (run.Games.Count > run.Specs.Count || run.Games.Where((g, i) => g.Game != run.Specs[i]).Any())
            return "경기 결과가 계획의 앞쪽 경기와 순서·GameId·Seed까지 같지 않습니다.";
        var aggregate = new MetricCounters();
        foreach (var g in run.Games.Where(g => g.Completed))
            aggregate.Add(g.Counters);
        if (Hashing.Sha256(RunResultText.V1Text(run.ConfigHash, run.Games.Select(g => g.ResultHash), aggregate.Canonical())) != run.RunResultHash
            || aggregate.Canonical() != run.Aggregate.Canonical())
            return "run_result_hash(run-result-v1) 또는 집계가 경기 결과로 다시 계산한 값과 다릅니다.";
        return run.Games.FirstOrDefault(g => c.Cohort.StratumOf(g.Game.GameIndex) != cell.Stratum) is { } wrong
            ? $"경기 {wrong.Game.GameIndex}의 손잡이 관계가 셀의 {cell.Hand}가 아닙니다."
            : null;
    }
}

/// <summary>
/// Replay of a 48-cell evaluation: every recorded cell run is rebuilt from its CELL definition (never from the recorded configuration), its recorded plan is checked against the rule and its config
/// hash before any game, the plan is re-executed in full, every game's status, result / event / RNG hashes, failure code and innings-1-8 observation must equal the record, the recomputed
/// <c>run-result-v1</c> must equal the recorded hash, and the evaluation re-derived from the re-executed runs must equal the recorded one exactly (<see cref="DominanceEvaluation.Canonical"/>).
/// </summary>
public static class DominanceReplay
{
    /// <summary>Replay against the current approvals' per-cell sample size (never the size the record claims).</summary>
    public static Error? Verify(IReadOnlyList<RunResult> recorded, DominanceEvaluation recordedEvaluation, CancellationToken cancellation = default) =>
        Verify(recorded, recordedEvaluation, OfficialApprovals.Current.DominanceGamesPerCell, cancellation);

    public static Error? Verify(IReadOnlyList<RunResult> recorded, DominanceEvaluation recordedEvaluation, int? frozenGamesPerCell, CancellationToken cancellation = default)
    {
        static Error E(string code, string message) => new(code, message);
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(recordedEvaluation);
        var plans = new List<(RunResult Recorded, RunConfig Config)>();
        foreach (var r in recorded)                                                            // every refusal that needs no game comes first
        {
            if (r is null || !DominanceMatrix.ByCompetitionKey.TryGetValue(r.Config.CompetitionKey, out var cell))
                return E("DomReplay.cell_unknown", "기록된 실행이 48셀 행렬의 셀이 아닙니다.");
            var config = DominanceMatrix.Config(cell, r.Config.RunId);
            if (!config.IsSuccess)
                return config.Error;
            if (r.Specs.Count == 0 || r.Specs.Where((s, i) => s.GameIndex != i + 1 || config.Value.Spec(s.GameIndex, s.Seed) != s).Any())
                return E("DomReplay.game_id_mismatch", $"{cell.Id}: 기록된 경기 목록이 셀 규칙의 index·GameId와 다릅니다.");
            if (config.Value.ConfigHash(r.Specs) != r.ConfigHash)
                return E("DomReplay.config_hash_mismatch", $"{cell.Id}: 셀 정의로 다시 만든 config_hash가 기록값과 다릅니다.");
            plans.Add((r, config.Value));
        }

        var rerun = new List<RunResult>();
        foreach (var (r, config) in plans)
        {
            var again = SimRunner.RunSpecs(config, r.Specs, null, cancellation);
            var cell = DominanceMatrix.ByCompetitionKey[config.CompetitionKey].Id;
            if (again.Games.Count != r.Games.Count || again.Games.Zip(r.Games).Any(p => !SameGame(p.First, p.Second)))
                return E("DomReplay.result_games_mismatch", $"{cell}: 경기별 상태·결과/이벤트/RNG 해시·실패 코드·1~8회 반이닝 득점이 재실행과 다릅니다.");
            if (again.RunResultHash != r.RunResultHash)
                return E("DomReplay.run_result_hash_mismatch", $"{cell}: 재실행 run_result_hash {again.RunResultHash}이(가) 기록값 {r.RunResultHash}과 다릅니다.");
            rerun.Add(again);
        }

        return DominanceEvaluator.Evaluate(rerun, frozenGamesPerCell).Canonical() == recordedEvaluation.Canonical()
            ? null
            : E("DomReplay.evaluation_mismatch", "재실행 결과로 다시 계산한 지배 전략 판정이 기록된 판정과 다릅니다.");
    }

    private static bool SameGame(GameRunResult a, GameRunResult b) =>
        a.Game == b.Game && a.Status == b.Status && a.ResultHash == b.ResultHash && a.EventHash == b.EventHash && a.RngHash == b.RngHash && a.Failure?.Code == b.Failure?.Code
        && (a.Regulation is null ? b.Regulation is null : a.Regulation.SameAs(b.Regulation));
}
