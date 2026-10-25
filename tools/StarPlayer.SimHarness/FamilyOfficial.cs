using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.Domain.Common;

namespace StarPlayer.SimHarness;

/// <summary>One unit of an official family plan: a calibration stratum (L1..L4) or an integration called-pitch mode. A null <see cref="CohortId"/> binds by mode only (fc3-int, whose cohort is undeclared).</summary>
public sealed record FamilyUnit(string Id, string? CohortId, string RulesProfileId);

/// <summary>
/// The approved shape of an official family run (F-C2 B-6). <see cref="Calibration"/>: the four strata of ADR-008 FC-D7 (<see cref="CalibrationStrata"/>), each a stop-aware run under ADR-009
/// (SR-D1, SR-D5). <see cref="Integration"/>: one exactly-100-game plan per called-pitch mode (FC-D7.2), full execution, no stop rule (SR-D5). Strata and modes are never pooled (FC-D7.3).
/// </summary>
public sealed record OfficialFamilyPlan(string Family, string RunType, bool StopAware, IReadOnlyList<FamilyUnit> Units)
{
    public static OfficialFamilyPlan Calibration { get; } =
        new("fc3-cal", "official-calibration", true, CalibrationStrata.All.Select(s => new FamilyUnit(s.Id, s.CohortId, s.RulesProfileId)).ToList());

    public static OfficialFamilyPlan Integration { get; } =
        new("fc3-int", "official-integration", false, [new("automated", null, HarnessData.AutomatedProfile), new("non-automated", null, HarnessData.NonAutomatedProfile)]);

    /// <summary>The directory of a unit's run inside the official output (<c>units/l1</c>, <c>units/automated</c>).</summary>
    public static string UnitDirectory(FamilyUnit unit) => "units/" + unit.Id.ToLowerInvariant();
}

/// <summary>A unit and the approved seed file bound to it (read against its external pin).</summary>
public sealed record PinnedUnit(FamilyUnit Unit, string SeedManifestPath, SeedManifest Manifest);

/// <summary>The trusted family plan derived from the approvals' pins only; <see cref="Units"/> is in plan order and complete only when PASS.</summary>
public sealed record UnitScan(CheckStatus Status, string? Code, string Message, IReadOnlyList<PinnedUnit> Units);

/// <summary>One unit's unchanged single-manifest official preflight (<see cref="OfficialPreflight"/>), whose attestation (v1) is that unit's.</summary>
public sealed record FamilyUnitPreflight(PinnedUnit Pinned, PreflightReport Report);

/// <summary>The family preflight. <see cref="Ready"/> only when every family check passed and every unit's preflight is ready. A refusal is never a statistical verdict.</summary>
public sealed record FamilyPreflightReport(OfficialFamilyPlan Plan, IReadOnlyList<PreflightCheck> Checks, IReadOnlyList<FamilyUnitPreflight> Units, string? HeadSha, string? OutputDirectory)
{
    public bool Ready => Checks.All(c => c.Status == CheckStatus.Pass) && Units.Count == Plan.Units.Count && Units.All(u => u.Report.Ready);

    public string? ErrorCode => Ready ? null : Checks.FirstOrDefault(c => c.Status == CheckStatus.Fail)?.Code ?? "Official.not_ready";
}

/// <summary>
/// The official calibration / integration preflight (FC-D10; same structure as the 48-cell one). The unit → seed-file binding is derived only from the approvals' pinned files of the family: each
/// must bind to exactly one unit (calibration: cohort and rules profile of its stratum; integration: rules profile of its mode), and every unit must be covered exactly once. Each unit then passes
/// the unchanged single-manifest preflight (cohort approval and frozen InputsHash, rules hash, policy, park, execution policy), which yields its attestation. Reads only pinned files, writes
/// nothing, starts no game.
/// </summary>
public static class FamilyPreflight
{
    /// <summary>The <c>unit_preflight</c> message when every unit is ready (one definition, also the B1 verifier's expected READY record, T11 R2-B).</summary>
    public static string AllUnitsReady(string family, int units) => $"{family} {units}개 단위 모두 단일 Manifest 공식 Preflight를 통과했습니다(단위별 Attestation v1).";

    public static FamilyPreflightReport Run(OfficialFamilyPlan plan, DominanceOfficialRequest r, OfficialApprovals a, GitIntegrityReport git)
    {
        var checks = new List<PreflightCheck>();
        void Add(string id, CheckStatus status, string? code, string message) => checks.Add(new PreflightCheck(id, status, code, message));

        Add("cli", CheckStatus.Pass, null, "명령줄이 엄격 파서를 통과했습니다(Seed·코호트·정책·규칙·정지 조건은 승인값에서만).");
        OfficialPreflight.EnvironmentChecks(checks, a, git);
        var output = OfficialOutput.Resolve(r.RepositoryRoot, r.OutDirectory, r.RunId);
        Add("output_directory", output.IsSuccess ? CheckStatus.Pass : CheckStatus.Fail, output.IsSuccess ? null : output.Error!.Value.Code, output.IsSuccess ? output.Value : output.Error!.Value.Message);
        var scan = PinnedUnits(plan, r.RepositoryRoot, a);
        Add("seed_manifests", scan.Status, scan.Code, scan.Message);

        var units = new List<FamilyUnitPreflight>();
        if (scan.Status != CheckStatus.Pass || !output.IsSuccess)
            Add("unit_preflight", CheckStatus.NotReady, "Official.input_unavailable", "앞선 검사가 통과하지 않아 확인할 입력이 없습니다.");
        else
        {
            var rel = r.OutDirectory ?? $"{OfficialOutput.RootRelative}/{r.RunId}";
            units.AddRange(scan.Units.Select(p => new FamilyUnitPreflight(p, OfficialPreflight.Run(new OfficialRequest(r.RepositoryRoot, p.SeedManifestPath, r.RunId, $"{rel}/{OfficialFamilyPlan.UnitDirectory(p.Unit)}"), a, git))));
            static PreflightCheck FirstUnmet(PreflightReport p) => p.Checks.FirstOrDefault(k => k.Status == CheckStatus.Fail) ?? p.Checks.First(k => k.Status != CheckStatus.Pass);
            var bad = units.Where(u => !u.Report.Ready).Select(u => (u.Pinned.Unit, Check: FirstUnmet(u.Report))).ToList();
            if (bad.Count == 0)
                Add("unit_preflight", CheckStatus.Pass, null, AllUnitsReady(plan.Family, units.Count));
            else
            {
                var worst = bad.FirstOrDefault(b => b.Check.Status == CheckStatus.Fail) is { Check: not null } failed ? failed : bad[0];
                Add("unit_preflight", worst.Check.Status, worst.Check.Code, $"{bad.Count}개 단위의 Preflight가 준비되지 않았습니다: {string.Join(", ", bad.Select(b => $"{b.Unit.Id}({b.Check.Id} {b.Check.Code})"))}");
            }
        }

        OfficialPreflight.ExecutionChecks(checks, a);
        return new FamilyPreflightReport(plan, checks, units, git.HeadSha, output.IsSuccess ? output.Value : null);
    }

    /// <summary>
    /// The trusted plan, shared by the preflight and the replay: every approved pin is read (an unreadable pinned file fails the whole approved set, B-7); the family's files must have unique
    /// competition keys, each must bind to exactly one unit, a calibration file must carry a stop plan (pa_target, whole blocks; the reader enforces the floor and SR-D6), and every unit must be covered once.
    /// </summary>
    public static UnitScan PinnedUnits(OfficialFamilyPlan plan, string root, OfficialApprovals a)
    {
        static UnitScan Fail(string code, string message) => new(CheckStatus.Fail, code, message, []);
        if (OfficialPins.ReadAll(root, a, out var all) is { } unreadable)
            return Fail(unreadable.Code, unreadable.Message);
        var files = all.Where(f => f.Manifest.Family == plan.Family).ToList();
        if (files.Count == 0)
            return new UnitScan(CheckStatus.NotReady, "Official.family_seed_manifests_not_frozen", $"고정된 {plan.Family} Seed 파일이 없습니다(Final Execution Freeze 전; 공식 Seed와 Pin은 승인값에서만).", []);
        if (SeedManifestReader.CheckUnique(files.Select(f => f.Manifest)) is { } reused)
            return Fail(reused.Code, reused.Message);
        var bound = new List<PinnedUnit>();
        foreach (var (path, m) in files)
        {
            var matches = plan.Units.Where(u => u.RulesProfileId == m.RulesProfileId && (u.CohortId is null || u.CohortId == m.CohortId)).ToList();
            if (matches.Count != 1)
                return Fail("Official.family_unit_unbound", $"고정된 Seed 파일 '{path}'(코호트 '{m.CohortId}', 규칙 '{m.RulesProfileId}')는 {plan.Family}의 어느 단위에도 결속되지 않습니다.");
            if (plan.StopAware && StopPlan.FromSeedManifest(m, path) is { IsSuccess: false } noStop)
                return Fail(noStop.Error!.Value.Code, noStop.Error.Value.Message);
            bound.Add(new PinnedUnit(matches[0], path, m));
        }

        if (bound.GroupBy(b => b.Unit.Id).FirstOrDefault(g => g.Count() > 1) is { } twice)
            return Fail("Official.family_unit_duplicate", $"단위 {twice.Key}에 고정된 Seed 파일이 {twice.Count()}개입니다(층·모드는 정확히 하나의 계획).");
        var missing = plan.Units.Where(u => bound.All(b => b.Unit.Id != u.Id)).Select(u => u.Id).ToList();
        return missing.Count > 0
            ? Fail("Official.family_units_missing", $"{plan.Family} 단위 {string.Join(", ", missing)}에 고정된 Seed 파일이 없습니다(일부 단위만 공식 실행할 수 없음).")
            : new UnitScan(CheckStatus.Pass, null, $"고정된 {plan.Family} Seed 파일 {bound.Count}개가 {plan.Units.Count}개 단위에 하나씩 결속됩니다.", plan.Units.Select(u => bound.Single(b => b.Unit.Id == u.Id)).ToList());
    }
}

/// <summary>One unit's verdict: the gate, its reason, a metrics error code when the evaluation could not be made, and the canonical metric lines (comparison text, not a hash).</summary>
public sealed record UnitVerdict(string UnitId, GateStatus Gate, string Reason, string? ErrorCode, IReadOnlyList<string> Lines);

/// <summary>The family evaluation: every unit's verdict and the family gate (units never pooled; a FAIL is never compensated or hidden).</summary>
public sealed record FamilyEvaluation(string Family, IReadOnlyList<UnitVerdict> Units, GateStatus Gate, string Reason)
{
    public const string TextVersion = "family-evaluation-text-draft";

    public string Canonical()
    {
        var t = new StringBuilder(TextVersion).Append('\n').Append("family=").Append(Family).Append('\n');
        if (Family == OfficialFamilyPlan.Calibration.Family)
            t.Append("policy=").Append(EvaluationPolicy.FcEvaluationV1.Id).Append('|').Append(EvaluationPolicy.FcEvaluationV1.Hash()).Append('\n');
        foreach (var u in Units)
        {
            t.Append("unit|").Append(u.UnitId).Append('|').Append(GateText.Of(u.Gate)).Append('|').Append(u.Reason).Append("|error=").Append(u.ErrorCode ?? "-").Append('\n');
            foreach (var line in u.Lines)
                t.Append(line).Append('\n');
        }

        return t.Append("gate|").Append(GateText.Of(Gate)).Append('|').Append(Reason).Append('\n').ToString();
    }
}

/// <summary>One executed unit: its preflight (plan and attestation), its run and the SHA-256 of the unit manifest file written for it.</summary>
public sealed record FamilyUnitRun(FamilyUnitPreflight Plan, RunResult Run, string ManifestSha256);

/// <summary>The outcome of an official family execution. <see cref="Complete"/> only when every unit ran to the end of its plan (calibration: to its rule stop) with no integrity break and no cancellation.</summary>
public sealed record FamilyOfficialResult(
    string RunId, FamilyPreflightReport Preflight, OfficialApprovals Approvals, IReadOnlyList<FamilyUnitRun> Units, DominanceRunTermination Termination, Error? Abort, FamilyEvaluation? Evaluation)
{
    public bool Complete => Termination == DominanceRunTermination.Completed && Units.Count == Preflight.Plan.Units.Count && Evaluation is not null;

    public string RunStatus => FamilyOfficial.StatusOf(Termination, Units.Select(u => u.Run).ToList(), Evaluation);

    /// <summary>The technical outcome (the existing codes, never a gate): 0 completed, 1 games failed or cancelled, 2 aborted for integrity, 3 metrics failure.</summary>
    public int ExitCode => RunStatus switch
    {
        RunReport.StatusCompleted => 0, RunReport.StatusMetricsError => Program.ExitMetricsError, RunReport.StatusAborted => 2, _ => 1,
    };
}

/// <summary>
/// The official calibration / integration execution (entered only from a READY <see cref="FamilyPreflightReport"/>). Units run in plan order. Calibration: each stratum is a stop-aware run of its
/// pinned plan through the ONE stop-rule executor (<see cref="SimRunner.RunStopAware"/>, ADR-009 SR-D1.4), so it ends at the first legal block boundary (TARGET_REACHED / SEEDS_EXHAUSTED) and the
/// pinned files are re-verified before every block (SR-D3.4). Integration: each mode is a full run of its whole 100-game plan (<see cref="SimRunner.RunSpecs"/>, SR-D5), files re-verified before
/// each unit. Each unit's files are written as soon as it ends through handles pinned below the run directory (OI-01); the family index <see cref="ResultFile"/> is written last in a DRAFT format
/// that is no approved official schema and carries no new hash.
/// </summary>
public static class FamilyOfficial
{
    public const string ResultFile = "family-result.json";

    /// <summary>The family index format. A draft: no official calibration / integration result schema is approved (B-1); it binds nothing by a new hash.</summary>
    public const string ResultSchema = "simharness-family-result-draft";

    private const string EvidenceNote = "결과 형식이 승인된 공식 스키마가 아니고 Final Execution Freeze가 없으므로 이 문서는 Official Evidence가 아닙니다.";

    /// <param name="integrity">Test seam called before each unit and (calibration) before every block with the number of units done; default: every planned seed file is still the attested one.</param>
    /// <param name="play">Test seam replacing the production <see cref="GameExecutor"/> (synthetic games); null in every production call.</param>
    public static FamilyOfficialResult Execute(FamilyPreflightReport report, string runId, OfficialApprovals a, string repositoryRoot, CancellationToken cancellation = default,
        Func<int, Error?>? integrity = null, Func<RunConfig, GameSpec, GameRunResult>? play = null)
    {
        if (!report.Ready)
            throw new InvalidOperationException($"{report.ErrorCode}: Preflight가 준비되지 않아 공식 실행에 진입할 수 없습니다.");
        integrity ??= _ => Reverify(repositoryRoot, a, report.Units);
        var plan = report.Plan;
        var runs = new List<FamilyUnitRun>();
        PinnedDirectory? output = null, unitsDirectory = null;
        FamilyOfficialResult Done(DominanceRunTermination t, Error? abort)
        {
            var evaluation = t == DominanceRunTermination.Completed ? Evaluate(plan, runs.Select(u => u.Run).ToList()) : null;
            var result = new FamilyOfficialResult(runId, report, a, runs, t, abort, evaluation);
            if (output is null)                                                                // never into a directory this run did not create
                return result;
            var written = output.WriteNewFile(ResultFile, OfficialPins.Utf8(JsonSerializer.Serialize(ResultDocument(result), RunReport.Json)));
            return written.IsSuccess ? result : result with { Termination = DominanceRunTermination.AbortedIntegrity, Abort = written.Error, Evaluation = null };
        }

        try
        {
            var relative = Path.GetRelativePath(repositoryRoot, report.OutputDirectory!);
            if (Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar).Any(s => s is "" or "." or ".."))
                return Done(DominanceRunTermination.AbortedIntegrity, new Error("Official.output_invalid", $"공식 출력 경로가 저장소 밖입니다: '{report.OutputDirectory}'."));
            var pinned = PinnedDirectory.CreateNew(repositoryRoot, relative.Split(Path.DirectorySeparatorChar));
            if (!pinned.IsSuccess)
                return Done(DominanceRunTermination.AbortedIntegrity, pinned.Error);
            output = pinned.Value;
            if (OfficialPins.WritePreflight(output, PreflightDocument(report, runId), report.Units.Select(u => (u.Pinned.Unit.Id, u.Report))) is { } unrecorded)   // B1 T11 R2-B: before the first unit
                return Done(DominanceRunTermination.AbortedIntegrity, unrecorded);
            var created = output.CreateSubdirectory("units");
            if (!created.IsSuccess)
                return Done(DominanceRunTermination.AbortedIntegrity, created.Error);
            unitsDirectory = created.Value;
            foreach (var unit in report.Units)
            {
                if (integrity(runs.Count) is { } broken)
                    return Done(DominanceRunTermination.AbortedIntegrity, broken);
                var config = Config(unit.Pinned.Manifest, runId);
                if (!config.IsSuccess)
                    return Done(DominanceRunTermination.AbortedIntegrity, config.Error);
                var specs = unit.Pinned.Manifest.Seeds.Select(s => config.Value.Spec(s.Index, s.Seed)).ToList();
                if (specs.Zip(unit.Pinned.Manifest.Seeds).Any(p => p.First.GameId != p.Second.GameId) || config.Value.ConfigHash(specs) != unit.Report.Attestation!.ConfigHash)
                    return Done(DominanceRunTermination.AbortedIntegrity, new Error("Official.attestation_config_mismatch", $"{unit.Pinned.Unit.Id}: 실행 설정의 config_hash가 Preflight Attestation과 다릅니다."));
                var started = DateTimeOffset.UtcNow;
                var run = RunUnit(plan, unit.Pinned, config.Value, specs, cancellation, () => integrity(runs.Count), play);
                var sha = OfficialPins.WriteRun(unitsDirectory, unit.Pinned.Unit.Id.ToLowerInvariant(), unit.Report.Attestation!, run, EnvironmentInfo.Capture(started, DateTimeOffset.UtcNow, report.HeadSha, false));
                if (!sha.IsSuccess)
                    return Done(DominanceRunTermination.AbortedIntegrity, sha.Error);
                runs.Add(new FamilyUnitRun(unit, run, sha.Value));
                if (run.CancelledCount > 0 || run.Termination == RunTermination.Cancelled)
                    return Done(DominanceRunTermination.Cancelled, null);
                if (run.Termination == RunTermination.AbortedIntegrity)                       // only a stop-aware run can end so (SR-D3.4); its evidence is written, it has no stop record
                    return Done(DominanceRunTermination.AbortedIntegrity, run.Abort);
            }

            return integrity(runs.Count) is { } late ? Done(DominanceRunTermination.AbortedIntegrity, late) : Done(DominanceRunTermination.Completed, null);
        }
        finally
        {
            unitsDirectory?.Dispose();
            output?.Dispose();
        }
    }

    /// <summary>One unit's run: the stop-aware executor on the pinned stop plan (calibration) or the whole plan (integration). The same function serves the execution and the replay.</summary>
    internal static RunResult RunUnit(OfficialFamilyPlan plan, PinnedUnit unit, RunConfig config, IReadOnlyList<GameSpec> specs, CancellationToken cancellation, Func<Error?> integrity,
        Func<RunConfig, GameSpec, GameRunResult>? play)
    {
        Func<GameSpec, GameRunResult>? seam = play is null ? null : s => play(config, s);
        return plan.StopAware
            ? SimRunner.RunStopAware(config, StopPlan.FromSeedManifest(unit.Manifest, unit.SeedManifestPath).Value, null, cancellation, integrity, seam)
            : SimRunner.RunSpecs(config, specs, null, cancellation, seam);
    }

    /// <summary>The run configuration of a pinned seed file under the fixed official profile (the same construction as <see cref="OfficialPreflight"/>'s attestation; its config hash is compared with it).</summary>
    public static Result<RunConfig> Config(SeedManifest m, string runId)
    {
        var cohort = CohortRegistry.Resolve(m.CohortId);
        var policy = PolicyRegistry.Resolve(m.PolicyId);
        var rules = HarnessData.LoadRules(m.RulesProfileId);
        var tuning = HarnessData.LoadTuningV4();
        var park = HarnessData.NeutralPark();
        var failure = new[] { cohort.Error, policy.Error, rules.Error, tuning.Error, park.Error }.FirstOrDefault(e => e is not null);
        return failure is { } e
            ? Result<RunConfig>.Failure(e)
            : RunConfig.Create(runId, "official", cohort.Value, rules.Value, tuning.Value, park.Value, OfficialProfile.AdvancementPolicyVersion, OfficialProfile.EventBudget, policy.Value, m.CompetitionKey);
    }

    /// <summary>Every planned seed file is still exactly the pinned one the preflight attested.</summary>
    private static Error? Reverify(string root, OfficialApprovals a, IEnumerable<FamilyUnitPreflight> units)
    {
        foreach (var u in units)
        {
            var read = SeedManifestReader.Read(root, u.Pinned.SeedManifestPath, a.SeedManifestPins.GetValueOrDefault(u.Pinned.SeedManifestPath));
            if (!read.IsSuccess)
                return read.Error;
            if (read.Value.FileSha256 != u.Report.Attestation!.SeedManifestSha256)
                return new Error("Official.seed_manifest_changed", $"{u.Pinned.Unit.Id}: Seed 파일이 Preflight 이후 바뀌었습니다.");
        }

        return null;
    }

    /// <summary>
    /// The family evaluation of complete unit runs, with the existing evaluators only. Calibration: <see cref="StratifiedEvaluator.Evaluate"/> under the approved <see cref="EvaluationPolicy.FcEvaluationV1"/>
    /// per stratum (a metrics error keeps the SR-D9.2 priority of <see cref="StratifiedEvaluator.GateOfFailedEvaluation"/>: a failed game is still FAIL, otherwise NOT_EVALUATED), combined by
    /// <see cref="StratifiedEvaluator.CombineStrata"/>; a stratum whose evaluation failed stays in the combination with that gate (never counted as "missing"; B6-01). Integration: <see cref="StratifiedEvaluator.Integration"/> per
    /// mode (a metrics error is FAIL, FC-D9.1); FAIL if any mode fails, PASS only if both pass, otherwise NOT_EVALUATED (no compensation).
    /// </summary>
    public static FamilyEvaluation Evaluate(OfficialFamilyPlan plan, IReadOnlyList<RunResult> runs)
    {
        var units = new List<UnitVerdict>();
        if (!plan.StopAware)
        {
            for (var i = 0; i < runs.Count; i++)
            {
                var (gate, reason) = StratifiedEvaluator.Integration(runs[i]);
                units.Add(new UnitVerdict(plan.Units[i].Id, gate, reason, runs[i].MetricsComputed ? null : runs[i].Metrics.Error!.Value.Code, []));
            }

            var named = string.Join("; ", units.Select(u => $"{u.UnitId}={GateText.Of(u.Gate)}"));
            var family = units.Any(u => u.Gate == GateStatus.Fail) ? (GateStatus.Fail, $"FAIL 모드가 있습니다(다른 모드로 보상하지 않음): {named}.")
                : units.Count == plan.Units.Count && units.All(u => u.Gate == GateStatus.Pass) ? (GateStatus.Pass, $"두 모드 모두 {SeedManifestReader.IntegrationGames}경기 완료·지표 계산 성공: {named}.")
                : (GateStatus.NotEvaluated, $"판정되지 않은 모드가 있습니다: {named}.");
            return new FamilyEvaluation(plan.Family, units, family.Item1, family.Item2);
        }

        var evaluations = new List<CalibrationEvaluation>();
        for (var i = 0; i < runs.Count; i++)
        {
            var evaluated = StratifiedEvaluator.Evaluate(runs[i], EvaluationPolicy.FcEvaluationV1);
            if (!evaluated.IsSuccess)
            {
                // the stratum RAN: it stays in the combination with its own gate (SR-D9.2) instead of reading as a missing one, which would outrank another stratum's INSUFFICIENT_SAMPLE (B6-01)
                var (gate, reason) = StratifiedEvaluator.GateOfFailedEvaluation(runs[i], EvaluationPolicy.FcEvaluationV1, evaluated.Error!.Value);
                units.Add(new UnitVerdict(plan.Units[i].Id, gate, reason, evaluated.Error.Value.Code, []));
                evaluations.Add(Unmeasured(plan.Units[i].Id, runs[i], gate, reason));
                continue;
            }

            var e = evaluated.Value;
            evaluations.Add(e);
            units.Add(new UnitVerdict(plan.Units[i].Id, e.Gate, e.GateReason, runs[i].MetricsComputed ? null : runs[i].Metrics.Error!.Value.Code,
                [.. e.Metrics.Select(m => $"metric|{m.FId}|{m.Official}|{GateText.Of(m.Status)}|{m.MeasuredPpm?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}|{m.Numerator}|{m.Denominator}|{m.NumeratorSame}|{m.DenominatorSame}|{m.NumeratorOpposite}|{m.DenominatorOpposite}|{m.Reason}")]));
        }

        var (combined, why) = StratifiedEvaluator.CombineStrata(evaluations);
        return new FamilyEvaluation(plan.Family, units, combined, why);
    }

    /// <summary>The combination entry of a stratum whose metrics could not be computed: its identity and the gate of <see cref="StratifiedEvaluator.GateOfFailedEvaluation"/>, no metric lines (a FAIL stays a FAIL, never "missing").</summary>
    private static CalibrationEvaluation Unmeasured(string stratumId, RunResult run, GateStatus gate, string reason) =>
        new(EvaluationPolicy.FcEvaluationV1.Id, EvaluationPolicy.FcEvaluationV1.Hash(), stratumId, run.Config.Cohort.CohortId, run.Config.Cohort.InputsHash, run.Config.Rules.ProfileId, run.Plan?.Family,
            run.Plan?.Family == "fc3-cal", run.ConfigHash, run.ResultVersion is null ? null : run.RunResultHash, run.Stop, run.CompletedCount, run.FailedCount, run.Aggregate.PlateAppearances,
            new StratumShares(0, 0, 0, 0, 0, 0, 0, 0, 0), [], gate, reason);

    /// <summary>COMPLETED / GAMES_FAILED / METRICS_ERROR for a complete run (failures outrank metrics); CANCELLED / ABORTED_INTEGRITY otherwise.</summary>
    internal static string StatusOf(DominanceRunTermination t, IReadOnlyList<RunResult> runs, FamilyEvaluation? evaluation) => t switch
    {
        DominanceRunTermination.AbortedIntegrity => RunReport.StatusAborted,
        DominanceRunTermination.Cancelled => RunReport.StatusCancelled,
        _ when runs.Any(r => !r.AllCompleted) => RunReport.StatusGamesFailed,
        _ when evaluation is null || runs.Any(r => !r.MetricsComputed) || evaluation.Units.Any(u => u.ErrorCode is not null) => RunReport.StatusMetricsError,
        _ => RunReport.StatusCompleted,
    };

    private static object Checks(IEnumerable<PreflightCheck> checks) =>
        checks.Select(c => new { id = c.Id, status = c.Status switch { CheckStatus.Pass => "PASS", CheckStatus.Fail => "FAIL", _ => "NOT_READY" }, code = c.Code, message = c.Message }).ToList();

    /// <summary>The approvals a result was made under, as the index records them (compared by the replay, never trusted).</summary>
    internal static object ApprovalRecord(OfficialFamilyPlan plan, OfficialApprovals a) => new
    {
        execution_policy_id = a.ExecutionPolicyId,
        execution_policy_hash = a.ExecutionPolicyHash,
        evaluation_policy_id = plan.StopAware ? EvaluationPolicy.FcEvaluationV1.Id : null,
        evaluation_policy_hash = plan.StopAware ? EvaluationPolicy.FcEvaluationV1.Hash() : null,
        integration_cohort_ids = plan.StopAware ? null : a.IntegrationCohortIds?.Order(StringComparer.Ordinal).ToList(),
    };

    /// <summary>The document a refused (or ready but not executed) preflight prints. The gate is NOT_EVALUATED: a preflight result is never a statistical verdict.</summary>
    public static object PreflightDocument(FamilyPreflightReport report, string runId) => new
    {
        schema_version = ResultSchema,
        schema_status = DominanceOfficial.SchemaStatus,
        official_evidence = false,
        official_evidence_note = EvidenceNote,
        run_id = runId,
        run_type = report.Plan.RunType,
        family = report.Plan.Family,
        run_status = report.Ready ? "PREFLIGHT_READY" : "PREFLIGHT_REFUSED",
        complete = false,
        preflight = new
        {
            ready = report.Ready,
            error_code = report.ErrorCode,
            checks = Checks(report.Checks),
            units = report.Units.Select(u => new { unit_id = u.Pinned.Unit.Id, seed_manifest_path = u.Pinned.SeedManifestPath, ready = u.Report.Ready, error_code = u.Report.ErrorCode }).ToList(),
        },
        family_gate = GateText.Of(GateStatus.NotEvaluated),
        family_gate_reason = "게임을 실행하지 않았습니다. Preflight 결과는 통계 판정이 아닙니다.",
    };

    /// <summary>One unit's index entry; the replay rebuilds it from the re-execution and requires it to equal the recorded entry exactly.</summary>
    internal static object UnitEntry(PinnedUnit p, OfficialAttestation att, RunResult run, string manifestSha256, UnitVerdict? verdict) => new
    {
        unit_id = p.Unit.Id, cohort_id = att.CohortId, rules_profile_id = att.RulesProfileId, competition_key = att.CompetitionKey, seed_manifest_path = att.SeedManifestPath,
        seed_manifest_sha256 = att.SeedManifestSha256, manifest_path = OfficialFamilyPlan.UnitDirectory(p.Unit) + "/manifest.json", manifest_sha256 = manifestSha256, config_hash = run.ConfigHash,
        run_result_version = run.ResultVersion, run_result_hash = run.ResultVersion is null ? null : run.RunResultHash, attestation_hash = att.Hash(), run_status = RunReport.RunStatus(run),
        planned_games = run.Requested, executed_games = run.Games.Count, completed_games = run.CompletedCount, failed_games = run.FailedCount, cancelled_games = run.CancelledCount,
        unexecuted_after_stop = run.UnexecutedAfterStop,
        stop = run.Stop is { } s ? new { executed_games = s.ExecutedGames, stop_reason = s.ReasonText, cumulative_pa = s.CumulativePa, block_size = s.BlockSize } : null,
        valid_pa = run.Aggregate.PlateAppearances, metrics_error_code = run.MetricsComputed ? null : run.Metrics.Error!.Value.Code,
        gate = verdict is null ? null : GateText.Of(verdict.Gate), gate_reason = verdict?.Reason, evaluation_error_code = verdict?.ErrorCode,
    };

    /// <summary>The family index (<see cref="ResultFile"/>): what ran, bound to which approved inputs, and the evaluation (its canonical comparison text, not a hash). Evidence for replay, never trusted by it.</summary>
    public static object ResultDocument(FamilyOfficialResult r) => new
    {
        schema_version = ResultSchema,
        schema_status = DominanceOfficial.SchemaStatus,
        official_evidence = false,
        official_evidence_note = EvidenceNote,
        run_id = r.RunId,
        run_type = r.Preflight.Plan.RunType,
        family = r.Preflight.Plan.Family,
        run_status = r.RunStatus,
        complete = r.Complete,
        abort_code = r.Abort?.Code,
        abort_message = r.Abort?.Message,
        git_commit_sha = r.Preflight.HeadSha,
        approvals = ApprovalRecord(r.Preflight.Plan, r.Approvals),
        preflight = new { ready = r.Preflight.Ready, checks = Checks(r.Preflight.Checks) },
        units_planned = r.Preflight.Plan.Units.Count,
        units = r.Units.Select((u, i) => UnitEntry(u.Plan.Pinned, u.Plan.Report.Attestation!, u.Run, u.ManifestSha256, r.Evaluation?.Units[i])).ToList(),
        family_gate = GateText.Of(r.Evaluation?.Gate ?? GateStatus.NotEvaluated),
        family_gate_reason = r.Evaluation?.Reason ?? "취소·무결성 중단된 불완전 실행은 통계 판정 대상이 아닙니다(SR-D3.4·SR-D3.5).",
        evaluation_canonical = r.Evaluation?.Canonical(),
    };

    public static string Text(FamilyOfficialResult r) =>
        $"official {r.Preflight.Plan.Family} run {r.RunId}: {r.RunStatus}, {r.Units.Count}/{r.Preflight.Plan.Units.Count} units, complete {r.Complete}{(r.Abort is { } a ? $", abort {a.Code}" : "")}; "
        + string.Join(", ", r.Units.Select(u => $"{u.Plan.Pinned.Unit.Id} {RunReport.RunStatus(u.Run)}{(u.Run.Stop is { } s ? $" {s.ReasonText}@{s.ExecutedGames}" : "")}"))
        + $"; family gate {GateText.Of(r.Evaluation?.Gate ?? GateStatus.NotEvaluated)} (official evidence: no, {DominanceOfficial.SchemaStatus})";
}

/// <summary>The answer of a family replay: null error is a match; <see cref="GamesExecuted"/> tells a refusal before any game from a difference found by re-execution.</summary>
public sealed record FamilyReplayOutcome(Error? Error, bool GamesExecuted, FamilyEvaluation? Evaluation)
{
    /// <summary>B-1 (B1-T06b): the re-executed unit runs of a MATCH, in plan order, for the independent evidence projection; null otherwise. Not part of any comparison.</summary>
    public IReadOnlyList<RunResult>? Reruns { get; init; }
}

/// <summary>
/// Replay of an official calibration / integration result directory against TRUSTED inputs only. Before any game: the index format, family and completeness; the plan re-derived from the
/// approvals' pins (<see cref="FamilyPreflight.PinnedUnits"/>, never from the result files); the recorded approvals; the units in plan order at paths fixed by unit id with the recorded file hashes;
/// each unit manifest through the existing official replay check (<see cref="ReplayCheck.Validate"/>: envelope, attestation, approvals, the REAL pinned seed file, its exact whole game list, and
/// for calibration the mandatory <c>run-result-v2</c> and stop-record pre-check of SR-D7.4 / SR-D8.4); its attestation must name the unit's approved file; one run id; a full-execution unit must
/// have played its whole plan. Then every unit is re-executed from the pinned plan with the same executor (calibration: the stop point is re-derived, SR-D8.1, and compared by
/// <see cref="StopReplay.Compare"/>); the family is re-evaluated and every unit entry, the canonical evaluation, the family gate and the run status must equal the record exactly.
/// </summary>
public static class FamilyOfficialReplay
{
    /// <param name="play">Test seam replacing the production <see cref="GameExecutor"/>; it never changes what is compared.</param>
    public static FamilyReplayOutcome Verify(OfficialFamilyPlan plan, string resultDirectory, OfficialApprovals a, string repositoryRoot, CancellationToken cancellation = default,
        Func<RunConfig, GameSpec, GameRunResult>? play = null, ReplayAuthority? authority = null)
    {
        static FamilyReplayOutcome Refuse(string code, string message) => new(new Error(code, message), false, null);
        static FamilyReplayOutcome Differs(string code, string message) => new(new Error(code, message), true, null);
        var units = new List<(PinnedUnit Trusted, ReplayPlan Plan, RunConfig Config, OfficialAttestation Att, JsonNode Recorded, string ManifestSha, IReadOnlyList<RecordedGame> Games, long Pa, long Pitches)>();
        string? recordedCanonical, recordedGate, recordedReason, recordedStatus;
        try
        {
            var path = Path.Combine(resultDirectory, FamilyOfficial.ResultFile);
            if (!File.Exists(path))
                return Refuse("FamOfficial.result_missing", $"'{FamilyOfficial.ResultFile}'가 없습니다(완료되지 않았거나 다른 디렉터리).");
            var raw = File.ReadAllBytes(path);
            if (SeedManifestReader.DuplicateMember(raw) is { } repeated)
                return Refuse("FamOfficial.result_invalid", $"같은 JSON 항목이 두 번 나옵니다: '{repeated}'.");
            var doc = JsonNode.Parse(raw) as JsonObject ?? throw new FormatException("결과 색인이 JSON 객체가 아닙니다.");
            string? Text(JsonNode? n) => n?.GetValue<string>();
            static JsonNode Need(JsonNode? n, string name) => n ?? throw new FormatException($"'{name}'가 없거나 null입니다.");
            if (Text(doc["schema_version"]) != FamilyOfficial.ResultSchema)
                return Refuse("FamOfficial.result_schema_unsupported", $"지원하지 않는 결과 형식입니다(지원: {FamilyOfficial.ResultSchema}).");
            if (Text(doc["family"]) != plan.Family || Text(doc["run_type"]) != plan.RunType)
                return Refuse("FamOfficial.family_mismatch", $"결과의 family/run_type이 '{plan.Family}'/'{plan.RunType}'가 아닙니다(명령별 Family 고정).");
            (recordedCanonical, recordedGate, recordedReason, recordedStatus) = (Text(doc["evaluation_canonical"]), Text(doc["family_gate"]), Text(doc["family_gate_reason"]), Text(doc["run_status"]));
            if (!Need(doc["complete"], "complete").GetValue<bool>() || recordedStatus is not (RunReport.StatusCompleted or RunReport.StatusGamesFailed or RunReport.StatusMetricsError))
                return Refuse("FamOfficial.run_not_complete", $"run_status '{recordedStatus}'인 불완전 실행(취소·무결성 중단·일부 단위)은 replay 대상 결과가 아닙니다.");
            var scan = FamilyPreflight.PinnedUnits(plan, repositoryRoot, a);
            if (scan.Status != CheckStatus.Pass)
                return Refuse(scan.Code!, scan.Message);
            if (Text(doc["git_commit_sha"]) != a.GitCommitSha || !JsonNode.DeepEquals(doc["approvals"], JsonSerializer.SerializeToNode(FamilyOfficial.ApprovalRecord(plan, a), RunReport.Json)))
                return Refuse("FamOfficial.approval_record_mismatch", "결과 색인의 Git SHA·실행 정책·평가 정책·integration 코호트 기록이 승인값과 다릅니다.");
            var recordedUnits = Need(doc["units"], "units").AsArray();
            if (recordedUnits.Count != plan.Units.Count || Need(doc["units_planned"], "units_planned").GetValue<int>() != plan.Units.Count)
                return Refuse("FamOfficial.unit_set_mismatch", $"기록된 단위 {recordedUnits.Count}개는 {plan.Family}의 {plan.Units.Count}개 단위 계획이 아닙니다.");
            var runId = Text(doc["run_id"]);
            for (var i = 0; i < recordedUnits.Count; i++)
            {
                var (trusted, recorded) = (scan.Units[i], Need(recordedUnits[i], "units[]").AsObject());
                string Field(string name) => Text(recorded[name]) ?? throw new FormatException($"'{name}'가 null입니다.");
                if (Field("unit_id") != trusted.Unit.Id)
                    return Refuse("FamOfficial.unit_order_mismatch", $"{i + 1}번째 기록이 계획 순서의 {trusted.Unit.Id}가 아닙니다(순서·교체 변조).");
                var rel = OfficialFamilyPlan.UnitDirectory(trusted.Unit) + "/manifest.json";
                if (Field("manifest_path") != rel)
                    return Refuse("FamOfficial.unit_path_mismatch", $"{trusted.Unit.Id}의 Manifest 경로는 '{rel}'로 고정됩니다.");
                var bytes = File.ReadAllBytes(Path.Combine([resultDirectory, .. rel.Split('/')]));
                var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (sha != Field("manifest_sha256"))
                    return Refuse("FamOfficial.unit_manifest_hash_mismatch", $"{trusted.Unit.Id}의 Manifest 파일 SHA-256이 기록값과 다릅니다.");
                if (SeedManifestReader.DuplicateMember(bytes) is { } dup)
                    return Refuse("Replay.manifest_invalid", $"{trusted.Unit.Id}: 같은 JSON 항목이 두 번 나옵니다: '{dup}'.");
                using var manifestDoc = JsonDocument.Parse(bytes);
                var manifest = manifestDoc.RootElement;
                var validated = ReplayCheck.Validate(manifest, a, repositoryRoot, null, authority);
                if (!validated.IsSuccess)
                    return Refuse(validated.Error!.Value.Code, $"{trusted.Unit.Id}: {validated.Error.Value.Message}");
                var p = validated.Value;
                if (plan.StopAware != p.Stop is not null)
                    return Refuse("FamOfficial.stop_contract_mismatch", $"{trusted.Unit.Id}: {plan.Family}의 정지 계약(정지 인식 {plan.StopAware})과 다른 결과입니다(SR-D5).");
                var envelope = manifest.GetProperty("official");
                var att = OfficialAttestation.FromJson(envelope.GetProperty("attestation")).Value;
                if (att.SeedManifestPath != trusted.SeedManifestPath || att.SeedManifestSha256 != trusted.Manifest.FileSha256)
                    return Refuse("FamOfficial.seed_plan_mismatch", $"{trusted.Unit.Id}: Attestation의 Seed 파일 '{att.SeedManifestPath}'이(가) 승인값이 이 단위에 결속한 '{trusted.SeedManifestPath}'가 아닙니다.");
                if (p.RunId != runId)
                    return Refuse("FamOfficial.run_id_mismatch", $"{trusted.Unit.Id}: 실행 ID '{p.RunId}'가 결과의 '{runId}'와 다릅니다(서로 다른 실행의 혼용).");
                var recordedHash = Text(recorded["run_result_hash"]);
                if ((Field("config_hash"), recordedHash, Field("attestation_hash"), Field("seed_manifest_sha256"), Field("seed_manifest_path"))
                    != (p.ConfigHash, p.RunResultHash, envelope.GetProperty("attestation_hash").GetString(), att.SeedManifestSha256, att.SeedManifestPath))
                    return Refuse("FamOfficial.unit_record_mismatch", $"{trusted.Unit.Id}: 결과 색인의 해시·Seed 파일 기록이 단위 Manifest와 다릅니다.");
                var config = FamilyOfficial.Config(trusted.Manifest, p.RunId);
                if (!config.IsSuccess)
                    return Refuse(config.Error!.Value.Code, config.Error.Value.Message);
                if (p.CheckAgainst(config.Value) is { } unlike)
                    return Refuse(unlike.Code, $"{trusted.Unit.Id}: {unlike.Message}");
                var results = manifest.GetProperty("results");
                var games = results.GetProperty("games").EnumerateArray()
                    .Select(g => new RecordedGame(g.GetProperty("game_id").GetString()!, g.GetProperty("seed").GetUInt64(), g.GetProperty("status").GetString()!, g.GetProperty("result_hash").GetString()!,
                        g.GetProperty("event_hash").GetString()!, g.GetProperty("rng_hash").GetString()!, g.GetProperty("error_code").GetString())).ToList();
                // a full-execution plan is complete only when every planned game was played (no cancelled or unstarted game presented as a complete run)
                if (!plan.StopAware && (games.Count != p.Games.Count || results.GetProperty("games_cancelled").GetInt32() != 0 || results.GetProperty("games_not_started").GetInt32() != 0))
                    return Refuse("FamOfficial.plan_not_completed", $"{trusted.Unit.Id}: 계획 {p.Games.Count}경기 중 {games.Count}경기만 기록되어 있습니다(부분 실행은 완료 결과가 아님).");
                // the index's stop record, result version and game counts must be the manifest's own (an index-only edit is refused here; both are re-derived after the re-execution)
                var manifestStop = results.TryGetProperty("stop", out var stop) ? JsonNode.Parse(stop.GetRawText()) : null;
                var manifestVersion = results.TryGetProperty("run_result_version", out var version) ? version.GetString() : RunResultText.V1;
                if (!JsonNode.DeepEquals(recorded["stop"], manifestStop) || Text(recorded["run_result_version"]) != manifestVersion
                    || (Need(recorded["planned_games"], "planned_games").GetValue<int>(), Need(recorded["executed_games"], "executed_games").GetValue<int>(), Need(recorded["completed_games"], "completed_games").GetValue<int>(),
                        Need(recorded["failed_games"], "failed_games").GetValue<int>(), Need(recorded["cancelled_games"], "cancelled_games").GetValue<int>())
                    != (results.GetProperty("games_requested").GetInt32(), games.Count, results.GetProperty("games_completed").GetInt32(), results.GetProperty("games_failed").GetInt32(),
                        results.GetProperty("games_cancelled").GetInt32()))
                    return Refuse("FamOfficial.unit_record_mismatch", $"{trusted.Unit.Id}: 결과 색인의 정지 기록·결과 버전·경기 수가 단위 Manifest와 다릅니다.");
                units.Add((trusted, p, config.Value, att, recorded, sha, games, results.GetProperty("plate_appearances").GetInt64(), results.GetProperty("pitches").GetInt64()));
            }
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException or JsonException or IOException)
        {
            return Refuse("FamOfficial.result_invalid", $"{plan.Family} 결과를 읽을 수 없습니다: {e.Message}");
        }

        var reruns = new List<RunResult>();
        foreach (var u in units)
        {
            var again = FamilyOfficial.RunUnit(plan, u.Trusted, u.Config, u.Plan.Games, cancellation,
                () => SeedManifestReader.Read(repositoryRoot, u.Trusted.SeedManifestPath, a.SeedManifestPins.GetValueOrDefault(u.Trusted.SeedManifestPath)).Error, play);
            if (again.CancelledCount > 0 || again.Termination == RunTermination.Cancelled)
                return Differs("FamOfficial.replay_cancelled", $"{u.Trusted.Unit.Id}: replay가 취소되었습니다(일치로 보지 않음).");
            if (plan.StopAware)
            {
                if (StopReplay.Compare(u.Plan.Recorded!, u.Plan.RunResultHash, again) is { } differs)
                    return Differs(differs.Code, $"{u.Trusted.Unit.Id}: {differs.Message}");
            }
            else if (!again.Games.Select(RecordedGame.Of).SequenceEqual(u.Games))
                return Differs("FamOfficial.result_games_mismatch", $"{u.Trusted.Unit.Id}: 경기별 상태·결과/이벤트/RNG 해시·실패 코드가 재실행과 다릅니다.");
            // the manifest's own aggregates are a claim like the rest: compared with the re-execution's, never read back as the expectation (B6-02; a calibration unit gets this from StopReplay.Compare)
            else if ((u.Pa, u.Pitches) != (again.Aggregate.PlateAppearances, again.Aggregate.Pitches))
                return Differs("FamOfficial.result_counts_mismatch", $"{u.Trusted.Unit.Id}: 기록된 plate_appearances/pitches ({u.Pa}/{u.Pitches})가 재실행 ({again.Aggregate.PlateAppearances}/{again.Aggregate.Pitches})와 다릅니다.");
            else if (again.ConfigHash != u.Plan.ConfigHash || again.RunResultHash != u.Plan.RunResultHash)
                return Differs("FamOfficial.run_result_hash_mismatch", $"{u.Trusted.Unit.Id}: 재실행 run_result_hash {again.RunResultHash}이(가) 기록값 {u.Plan.RunResultHash}과 다릅니다.");
            reruns.Add(again);
        }

        var evaluation = FamilyOfficial.Evaluate(plan, reruns);
        for (var i = 0; i < units.Count; i++)
        {
            var expected = JsonSerializer.SerializeToNode(FamilyOfficial.UnitEntry(units[i].Trusted, units[i].Att, reruns[i], units[i].ManifestSha, evaluation.Units[i]), RunReport.Json);
            if (!JsonNode.DeepEquals(expected, units[i].Recorded))
                return Differs("FamOfficial.unit_record_mismatch", $"{units[i].Trusted.Unit.Id}: 결과 색인의 단위 기록(상태·경기 수·정지 기록·지표·판정)이 재실행 결과와 다릅니다.");
        }

        var status = FamilyOfficial.StatusOf(DominanceRunTermination.Completed, reruns, evaluation);
        return recordedCanonical == evaluation.Canonical() && recordedGate == GateText.Of(evaluation.Gate) && recordedReason == evaluation.Reason && recordedStatus == status
            ? new FamilyReplayOutcome(null, true, evaluation) { Reruns = reruns }
            : Differs("FamOfficial.evaluation_mismatch", "재실행 결과로 다시 계산한 판정·Gate·실행 상태가 기록과 다릅니다.");
    }
}
