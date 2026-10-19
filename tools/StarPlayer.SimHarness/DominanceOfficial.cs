using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StarPlayer.Domain.Common;

namespace StarPlayer.SimHarness;

/// <summary>An official 48-cell request: only the run id and the output directory come from the operator; the plan comes from <see cref="OfficialApprovals"/> alone.</summary>
public sealed record DominanceOfficialRequest(string RepositoryRoot, string RunId, string? OutDirectory);

/// <summary>A cell and the approved seed file it is bound to: read against its external pin and bound by <see cref="DominanceMatrix.CellOf"/>.</summary>
public sealed record PinnedCell(DominanceCell Cell, string SeedManifestPath, SeedManifest Manifest);

/// <summary>The trusted 48-cell plan derived from the approvals' pins (<see cref="DominancePreflight.PinnedCells"/>); <see cref="Cells"/> is in matrix order and complete only when PASS.</summary>
public sealed record PinScan(CheckStatus Status, string? Code, string Message, IReadOnlyList<PinnedCell> Cells);

/// <summary>One cell's single-manifest official preflight (F-C2-A <see cref="OfficialPreflight"/>, unchanged), whose attestation (v1) is that cell's.</summary>
public sealed record DominanceCellPreflight(PinnedCell Pinned, PreflightReport Report);

/// <summary>
/// The 48-cell preflight: the matrix checks plus every cell's own preflight. <see cref="Ready"/> only when every matrix check passed and all 48 cell preflights are ready. A refusal is a statement
/// about the inputs, never a dominance verdict (the dominance gate of a refused run is NOT_EVALUATED).
/// </summary>
public sealed record DominancePreflightReport(IReadOnlyList<PreflightCheck> Checks, IReadOnlyList<DominanceCellPreflight> Cells, string? HeadSha, string? OutputDirectory, int? GamesPerCell)
{
    public bool Ready => Checks.All(c => c.Status == CheckStatus.Pass) && Cells.Count == DominanceMatrix.Cells.Count && Cells.All(c => c.Report.Ready);

    public string? ErrorCode => Ready ? null : Checks.FirstOrDefault(c => c.Status == CheckStatus.Fail)?.Code ?? "Official.not_ready";
}

/// <summary>
/// F-C2 official integration, 48-cell preflight (ADR-008 FC-D4 / FC-D5 / FC-D10, ADR-009 SR-D5 / SR-D6). Fail-closed and complete: every check runs and is reported. The cell → seed-file mapping is
/// derived only from the approvals' pinned files (never from a command line or a result file); each file is read against its pin, must be <c>fc3-dom</c>, must bind to exactly one cell, and the
/// 48 cells must each have exactly one file. The frozen per-cell sample size (FC-D4.7) must equal every file's seed count (SR-D6 makes it the per-cell cap too). Each cell then passes the unchanged
/// single-manifest preflight, which yields its attestation. Reads only pinned files, writes nothing, starts no game.
/// </summary>
public static class DominancePreflight
{
    public static DominancePreflightReport Run(DominanceOfficialRequest r, OfficialApprovals a, GitIntegrityReport git)
    {
        var checks = new List<PreflightCheck>();
        void Add(string id, CheckStatus status, string? code, string message) => checks.Add(new PreflightCheck(id, status, code, message));
        void Unavailable(string id) => Add(id, CheckStatus.NotReady, "Official.input_unavailable", "앞선 검사가 통과하지 않아 확인할 입력이 없습니다.");

        Add("cli", CheckStatus.Pass, null, "명령줄이 엄격 파서를 통과했습니다(Seed·코호트·정책·규칙은 승인값에서만).");
        OfficialPreflight.EnvironmentChecks(checks, a, git);
        var output = OfficialOutput.Resolve(r.RepositoryRoot, r.OutDirectory, r.RunId);
        Add("output_directory", output.IsSuccess ? CheckStatus.Pass : CheckStatus.Fail, output.IsSuccess ? null : output.Error!.Value.Code, output.IsSuccess ? output.Value : output.Error!.Value.Message);

        var scan = PinnedCells(r.RepositoryRoot, a);
        Add("seed_manifests", scan.Status, scan.Code, scan.Message);

        checks.Add(PlanSizeCheck(a, scan));

        var cells = new List<DominanceCellPreflight>();
        if (scan.Status != CheckStatus.Pass || !output.IsSuccess)
            Unavailable("cell_preflight");
        else
        {
            var rel = r.OutDirectory ?? $"{OfficialOutput.RootRelative}/{r.RunId}";
            cells.AddRange(scan.Cells.Select(p => new DominanceCellPreflight(p, OfficialPreflight.Run(new OfficialRequest(r.RepositoryRoot, p.SeedManifestPath, r.RunId, $"{rel}/{CellDirectory(p.Cell)}"), a, git))));
            // the first unmet check of each refused cell, a FAIL before a NOT_READY (a cell's own ErrorCode folds every NOT_READY into Official.not_ready)
            static PreflightCheck FirstUnmet(PreflightReport p) => p.Checks.FirstOrDefault(k => k.Status == CheckStatus.Fail) ?? p.Checks.First(k => k.Status != CheckStatus.Pass);
            var bad = cells.Where(c => !c.Report.Ready).Select(c => (c.Pinned.Cell, Check: FirstUnmet(c.Report))).ToList();
            if (bad.Count == 0)
                Add("cell_preflight", CheckStatus.Pass, null, "48셀 모두 단일 Manifest 공식 Preflight를 통과했습니다(셀별 Attestation v1).");
            else
            {
                var worst = bad.FirstOrDefault(b => b.Check.Status == CheckStatus.Fail) is { Check: not null } failed ? failed : bad[0];
                Add("cell_preflight", worst.Check.Status, worst.Check.Code,
                    $"{bad.Count}셀의 Preflight가 준비되지 않았습니다: {string.Join(", ", bad.Take(6).Select(b => $"{b.Cell.Id}({b.Check.Id} {b.Check.Code})"))}{(bad.Count > 6 ? " …" : "")}");
            }
        }

        OfficialPreflight.ExecutionChecks(checks, a);
        return new DominancePreflightReport(checks, cells, git.HeadSha, output.IsSuccess ? output.Value : null, a.DominanceGamesPerCell);
    }

    /// <summary>
    /// The approved per-cell plan size, shared by the preflight and the replay (FIX1, OI-02) so the two can never diverge: the frozen size (FC-D4.7) must exist and be at least 2, and every one of
    /// the 48 pinned seed files must have exactly that many games (SR-D6 makes it the per-cell cap too). Without a PASS scan the files are unavailable.
    /// </summary>
    public static PreflightCheck PlanSizeCheck(OfficialApprovals a, PinScan scan)
    {
        const string id = "games_per_cell";
        if (a.DominanceGamesPerCell is not { } n)
            return new PreflightCheck(id, CheckStatus.NotReady, "Official.dominance_games_per_cell_not_frozen", "셀당 공식 표본 수가 아직 동결되지 않았습니다(ADR-008 FC-D4.7, Final Execution Freeze).");
        if (n < 2)
            return new PreflightCheck(id, CheckStatus.Fail, "Official.dominance_games_per_cell_invalid", $"동결된 셀당 표본 수 {n}로는 경기 단위 분산을 계산할 수 없습니다(2 이상).");
        if (scan.Status != CheckStatus.Pass)
            return new PreflightCheck(id, CheckStatus.NotReady, "Official.input_unavailable", "앞선 검사가 통과하지 않아 확인할 입력이 없습니다.");
        return scan.Cells.FirstOrDefault(p => p.Manifest.Seeds.Count != n) is { } off
            ? new PreflightCheck(id, CheckStatus.Fail, "Official.dominance_plan_size_mismatch", $"{off.Cell.Id}의 Seed 파일 '{off.SeedManifestPath}'는 {off.Manifest.Seeds.Count}경기로, 동결된 셀당 {n}경기와 다릅니다.")
            : new PreflightCheck(id, CheckStatus.Pass, null, $"48셀 모두 {n}경기(max_games = Seed 수 = 동결 표본 수, SR-D6)");
    }

    /// <summary>The directory of a cell's run inside the official output, relative to it (<c>cells/dom-sa-nb</c>).</summary>
    public static string CellDirectory(DominanceCell cell) => "cells/" + cell.Id.ToLowerInvariant();

    /// <summary>
    /// The trusted plan: every approved pin is read (a pinned file that cannot be read against its pin is an integrity failure of the approved set, fail-closed); the <c>fc3-dom</c> files must have
    /// unique competition keys, each must bind to a cell (<see cref="DominanceMatrix.CellOf"/>: key, cohort, policy, rules, no PA target), and every one of the 48 cells must be covered.
    /// </summary>
    public static PinScan PinnedCells(string root, OfficialApprovals a)
    {
        static PinScan Fail(string code, string message) => new(CheckStatus.Fail, code, message, []);
        if (OfficialPins.ReadAll(root, a, out var all) is { } unreadable)
            return Fail(unreadable.Code, unreadable.Message);
        var dom = all.Where(p => p.Manifest.Family == DominanceMatrix.Family).ToList();

        if (dom.Count == 0)
            return new PinScan(CheckStatus.NotReady, "Official.dominance_seed_manifests_not_frozen", "고정된 fc3-dom Seed 파일이 없습니다(Final Execution Freeze 전; 공식 Seed와 Pin은 승인값에서만).", []);
        if (SeedManifestReader.CheckUnique(dom.Select(d => d.Manifest)) is { } reused)
            return Fail(reused.Code, reused.Message);
        var cells = new List<PinnedCell>();
        foreach (var (path, manifest) in dom)
        {
            var cell = DominanceMatrix.CellOf(manifest);
            if (!cell.IsSuccess)
                return Fail(cell.Error!.Value.Code, $"고정된 Seed 파일 '{path}': {cell.Error.Value.Message}");
            cells.Add(new PinnedCell(cell.Value, path, manifest));
        }

        var missing = DominanceMatrix.Cells.Where(c => cells.All(p => p.Cell.Number != c.Number)).Select(c => c.Id).ToList();
        return missing.Count > 0
            ? Fail("Official.dominance_cells_missing", $"48셀 중 {missing.Count}셀에 고정된 Seed 파일이 없습니다: {string.Join(", ", missing.Take(8))}{(missing.Count > 8 ? " …" : "")}(일부 셀만 공식 실행할 수 없음).")
            : new PinScan(CheckStatus.Pass, null, $"고정된 fc3-dom Seed 파일 48개가 48셀에 하나씩 결속됩니다.", cells.OrderBy(c => c.Cell.Number).ToList());
    }
}

/// <summary>Shared by the official multi-run paths (48-cell matrix, calibration, integration) so they read the approved pins and write a run's files in exactly one way.</summary>
internal static class OfficialPins
{
    /// <summary>
    /// Every approved pin, read against its pin, in path order. A pinned file that cannot be read is an integrity failure of the WHOLE approved set, whatever its family (F-C2 B-7: the conservative
    /// fail-closed coupling, kept until the owner decides otherwise).
    /// </summary>
    public static Error? ReadAll(string root, OfficialApprovals a, out List<(string Path, SeedManifest Manifest)> files)
    {
        files = [];
        foreach (var (path, pin) in a.SeedManifestPins.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var read = SeedManifestReader.Read(root, path, pin);
            if (!read.IsSuccess)
                return new Error(read.Error!.Value.Code, $"고정된 Seed 파일 '{path}': {read.Error.Value.Message}");
            files.Add((path, read.Value));
        }

        return null;
    }

    /// <summary>
    /// A run's directory and its three files, all created new through handles pinned below <paramref name="parent"/>: <c>manifest.json</c> (the existing <c>simharness-manifest-v1</c> with the existing
    /// <c>simharness-official-v1</c> envelope and attestation v1), <c>summary.json</c>, <c>summary.txt</c>; returns the manifest's SHA-256.
    /// </summary>
    public static Result<string> WriteRun(PinnedDirectory parent, string name, OfficialAttestation att, RunResult run, EnvironmentInfo environment)
    {
        var created = parent.CreateSubdirectory(name);
        if (!created.IsSuccess)
            return Result<string>.Failure(created.Error!.Value);
        using var directory = created.Value;
        var manifest = JsonSerializer.SerializeToNode(RunReport.Manifest(run, environment), RunReport.Json)!.AsObject();
        manifest["official"] = JsonSerializer.SerializeToNode(new { schema_version = OfficialAttestation.EnvelopeVersion, attestation = att, attestation_hash = att.Hash() }, RunReport.Json);
        var sha = directory.WriteNewFile("manifest.json", Utf8(JsonSerializer.Serialize(manifest, RunReport.Json)));
        if (!sha.IsSuccess)
            return sha;
        var summary = directory.WriteNewFile("summary.json", Utf8(JsonSerializer.Serialize(RunReport.Summary(run), RunReport.Json)));
        if (!summary.IsSuccess)
            return summary;
        var text = directory.WriteNewFile("summary.txt", Utf8(RunReport.Text(run)));
        return text.IsSuccess ? sha : text;
    }

    public static byte[] Utf8(string content) => new UTF8Encoding(false).GetBytes(content);
}

public enum DominanceRunTermination { Completed, Cancelled, AbortedIntegrity }

/// <summary>One executed cell: its preflight (plan and attestation), its full-execution run and the SHA-256 of the cell manifest file written for it.</summary>
public sealed record DominanceCellRun(DominanceCellPreflight Plan, RunResult Run, string ManifestSha256);

/// <summary>
/// The outcome of an official 48-cell execution. <see cref="Complete"/> only when all 48 cells ran to the end of their plans with no integrity break and no cancellation; only then is there an
/// <see cref="Evaluation"/>. A partial run keeps its executed cells as evidence and is never a complete result.
/// </summary>
public sealed record DominanceOfficialResult(
    string RunId, DominancePreflightReport Preflight, OfficialApprovals Approvals, IReadOnlyList<DominanceCellRun> Cells, DominanceRunTermination Termination, Error? Abort, DominanceEvaluation? Evaluation)
{
    public bool Complete => Termination == DominanceRunTermination.Completed && Cells.Count == DominanceMatrix.Cells.Count && Evaluation is not null;

    public string RunStatus => DominanceOfficial.StatusOf(Termination, Cells.Select(c => c.Run).ToList(), Evaluation);

    /// <summary>The technical outcome (the existing codes, never a gate): 0 completed, 1 games failed or cancelled, 2 aborted for integrity, 3 metrics failure.</summary>
    public int ExitCode => RunStatus switch
    {
        RunReport.StatusCompleted => 0, RunReport.StatusMetricsError => Program.ExitMetricsError, RunReport.StatusAborted => 2, _ => 1,
    };
}

/// <summary>
/// The official 48-cell execution (entered only from a READY <see cref="DominancePreflightReport"/>). Cells run in matrix order as full-execution runs over their pinned plans (no stop rule,
/// SR-D5); before each cell and after the last, every pinned seed file is re-read against its pin (SR-D3.4: a break aborts the run, which then has no evaluation). Each cell's output is written as
/// soon as it finishes (reproduction evidence survives a later failure): <c>cells/&lt;cell&gt;/manifest.json</c> (the existing <c>simharness-manifest-v1</c> with the existing
/// <c>simharness-official-v1</c> envelope and attestation v1, so the existing replay accepts it), <c>summary.json</c> and <c>summary.txt</c>; the matrix index <see cref="ResultFile"/> is written
/// last, in a DRAFT format that is not an approved official schema and carries no new hash.
/// </summary>
public static class DominanceOfficial
{
    public const string ResultFile = "dominance-result.json";

    /// <summary>The matrix index format. A draft: no official schema for the 48-cell result is approved (reported as an owner decision); it binds nothing by a new hash.</summary>
    public const string ResultSchema = "simharness-dominance-result-draft";

    public const string SchemaStatus = "DRAFT_NOT_APPROVED";

    public const string RunType = "official-dominance";

    public static DominanceOfficialResult Execute(DominancePreflightReport report, string runId, OfficialApprovals a, string repositoryRoot, CancellationToken cancellation = default,
        Func<int, Error?>? integrity = null)
    {
        if (!report.Ready)
            throw new InvalidOperationException($"{report.ErrorCode}: Preflight가 준비되지 않아 공식 실행에 진입할 수 없습니다.");
        integrity ??= _ => Reverify(repositoryRoot, a, report);
        var runs = new List<DominanceCellRun>();
        PinnedDirectory? output = null, cells = null;
        DominanceOfficialResult Done(DominanceRunTermination t, Error? abort)
        {
            var evaluation = t == DominanceRunTermination.Completed ? DominanceEvaluator.Evaluate(runs.Select(c => c.Run), a.DominanceGamesPerCell) : null;
            var result = new DominanceOfficialResult(runId, report, a, runs, t, abort, evaluation);
            if (output is null)                                                                // never into a directory this run did not create
                return result;
            var written = output.WriteNewFile(ResultFile, Utf8(JsonSerializer.Serialize(ResultDocument(result), RunReport.Json)));
            return written.IsSuccess ? result : result with { Termination = DominanceRunTermination.AbortedIntegrity, Abort = written.Error, Evaluation = null };
        }

        try
        {
            // OI-01: the output is created and written only through handles pinned from the volume root down (no re-resolved path, no link followed, exclusive creation)
            var relative = Path.GetRelativePath(repositoryRoot, report.OutputDirectory!);
            if (Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar).Any(s => s is "" or "." or ".."))
                return Done(DominanceRunTermination.AbortedIntegrity, new Error("Official.output_invalid", $"공식 출력 경로가 저장소 밖입니다: '{report.OutputDirectory}'."));
            var pinned = PinnedDirectory.CreateNew(repositoryRoot, relative.Split(Path.DirectorySeparatorChar));
            if (!pinned.IsSuccess)
                return Done(DominanceRunTermination.AbortedIntegrity, pinned.Error);
            output = pinned.Value;
            var cellsDirectory = output.CreateSubdirectory("cells");
            if (!cellsDirectory.IsSuccess)
                return Done(DominanceRunTermination.AbortedIntegrity, cellsDirectory.Error);
            cells = cellsDirectory.Value;
            foreach (var plan in report.Cells)
            {
                if (integrity(runs.Count) is { } broken)
                    return Done(DominanceRunTermination.AbortedIntegrity, broken);
                var config = DominanceMatrix.Config(plan.Pinned.Cell, runId, "official");
                if (!config.IsSuccess)
                    return Done(DominanceRunTermination.AbortedIntegrity, config.Error);
                var specs = plan.Pinned.Manifest.Seeds.Select(s => config.Value.Spec(s.Index, s.Seed)).ToList();
                if (specs.Zip(plan.Pinned.Manifest.Seeds).Any(p => p.First.GameId != p.Second.GameId) || config.Value.ConfigHash(specs) != plan.Report.Attestation!.ConfigHash)
                    return Done(DominanceRunTermination.AbortedIntegrity, new Error("Official.attestation_config_mismatch", $"{plan.Pinned.Cell.Id}: 실행 설정의 config_hash가 Preflight Attestation과 다릅니다."));
                var started = DateTimeOffset.UtcNow;
                var run = SimRunner.RunSpecs(config.Value, specs, null, cancellation);
                var sha = WriteCell(cells, plan, run, EnvironmentInfo.Capture(started, DateTimeOffset.UtcNow, report.HeadSha, false));
                if (!sha.IsSuccess)
                    return Done(DominanceRunTermination.AbortedIntegrity, sha.Error);
                runs.Add(new DominanceCellRun(plan, run, sha.Value));
                if (run.CancelledCount > 0)
                    return Done(DominanceRunTermination.Cancelled, null);
            }

            return integrity(runs.Count) is { } late ? Done(DominanceRunTermination.AbortedIntegrity, late) : Done(DominanceRunTermination.Completed, null);
        }
        finally
        {
            cells?.Dispose();
            output?.Dispose();
        }
    }

    /// <summary>Every planned seed file is still exactly the pinned one the preflight attested.</summary>
    private static Error? Reverify(string root, OfficialApprovals a, DominancePreflightReport report)
    {
        foreach (var c in report.Cells)
        {
            var read = SeedManifestReader.Read(root, c.Pinned.SeedManifestPath, a.SeedManifestPins.GetValueOrDefault(c.Pinned.SeedManifestPath));
            if (!read.IsSuccess)
                return read.Error;
            if (read.Value.FileSha256 != c.Report.Attestation!.SeedManifestSha256)
                return new Error("Official.seed_manifest_changed", $"{c.Pinned.Cell.Id}: Seed 파일이 Preflight 이후 바뀌었습니다.");
        }

        return null;
    }

    /// <summary>COMPLETED / GAMES_FAILED / METRICS_ERROR for a complete run (failures outrank metrics, as for a single run); CANCELLED / ABORTED_INTEGRITY otherwise.</summary>
    internal static string StatusOf(DominanceRunTermination t, IReadOnlyList<RunResult> runs, DominanceEvaluation? evaluation) => t switch
    {
        DominanceRunTermination.AbortedIntegrity => RunReport.StatusAborted,
        DominanceRunTermination.Cancelled => RunReport.StatusCancelled,
        _ when runs.Any(r => !r.AllCompleted) => RunReport.StatusGamesFailed,
        _ when evaluation is null || evaluation.Cells.Any(c => c.MetricsFailures > 0) => RunReport.StatusMetricsError,
        _ => RunReport.StatusCompleted,
    };

    /// <summary>The cell's directory and its three files, all created new through handles pinned below the run directory; returns the manifest's SHA-256.</summary>
    private static Result<string> WriteCell(PinnedDirectory cells, DominanceCellPreflight plan, RunResult run, EnvironmentInfo environment) =>
        OfficialPins.WriteRun(cells, plan.Pinned.Cell.Id.ToLowerInvariant(), plan.Report.Attestation!, run, environment);

    private static byte[] Utf8(string content) => OfficialPins.Utf8(content);

    private static object Checks(IEnumerable<PreflightCheck> checks) =>
        checks.Select(c => new { id = c.Id, status = c.Status switch { CheckStatus.Pass => "PASS", CheckStatus.Fail => "FAIL", _ => "NOT_READY" }, code = c.Code, message = c.Message }).ToList();

    private const string EvidenceNote = "결과 형식이 승인된 공식 스키마가 아니고 Final Execution Freeze가 없으므로 이 문서는 Official Evidence가 아닙니다.";

    /// <summary>The document a refused (or ready but not executed) preflight prints. The dominance gate is NOT_EVALUATED: a preflight result is never a statistical verdict.</summary>
    public static object PreflightDocument(DominancePreflightReport report, string runId) => new
    {
        schema_version = ResultSchema,
        schema_status = SchemaStatus,
        official_evidence = false,
        official_evidence_note = EvidenceNote,
        run_id = runId,
        run_type = RunType,
        family = DominanceMatrix.Family,
        run_status = report.Ready ? "PREFLIGHT_READY" : "PREFLIGHT_REFUSED",
        complete = false,
        preflight = new
        {
            ready = report.Ready,
            error_code = report.ErrorCode,
            games_per_cell = report.GamesPerCell,
            checks = Checks(report.Checks),
            cells = report.Cells.Select(c => new { cell_id = c.Pinned.Cell.Id, seed_manifest_path = c.Pinned.SeedManifestPath, ready = c.Report.Ready, error_code = c.Report.ErrorCode }).ToList(),
        },
        dominance_gate = DominanceEvaluation.Text(DominanceGate.NotEvaluated),
        dominance_gate_reason = "게임을 실행하지 않았습니다. Preflight 결과는 지배 전략 통계 판정이 아닙니다.",
    };

    /// <summary>The matrix index (<see cref="ResultFile"/>): what ran, bound to which approved inputs, and the evaluation (its canonical comparison text, not a hash). Evidence for replay, never trusted by it.</summary>
    public static object ResultDocument(DominanceOfficialResult r) => new
    {
        schema_version = ResultSchema,
        schema_status = SchemaStatus,
        official_evidence = false,
        official_evidence_note = EvidenceNote,
        run_id = r.RunId,
        run_type = RunType,
        family = DominanceMatrix.Family,
        run_status = r.RunStatus,
        complete = r.Complete,
        abort_code = r.Abort?.Code,
        abort_message = r.Abort?.Message,
        git_commit_sha = r.Preflight.HeadSha,
        execution_policy_id = r.Approvals.ExecutionPolicyId,
        execution_policy_hash = r.Approvals.ExecutionPolicyHash,
        games_per_cell = r.Approvals.DominanceGamesPerCell,
        evaluation_policy_id = DominanceEvaluator.PolicyId,
        preflight = new { ready = r.Preflight.Ready, checks = Checks(r.Preflight.Checks) },
        cells_planned = DominanceMatrix.Cells.Count,
        cells = r.Cells.Select(c =>
        {
            var att = c.Plan.Report.Attestation!;
            var evaluated = r.Evaluation?.Cells.Single(e => e.Cell.Number == c.Plan.Pinned.Cell.Number);
            return new
            {
                number = c.Plan.Pinned.Cell.Number, cell_id = c.Plan.Pinned.Cell.Id, competition_key = c.Plan.Pinned.Cell.CompetitionKey, seed_manifest_path = att.SeedManifestPath,
                seed_manifest_sha256 = att.SeedManifestSha256, manifest_path = DominancePreflight.CellDirectory(c.Plan.Pinned.Cell) + "/manifest.json", manifest_sha256 = c.ManifestSha256,
                config_hash = c.Run.ConfigHash, run_result_hash = c.Run.RunResultHash, attestation_hash = att.Hash(), run_status = RunReport.RunStatus(c.Run), planned_games = c.Run.Requested,
                completed_games = c.Run.CompletedCount, failed_games = c.Run.FailedCount, cancelled_games = c.Run.CancelledCount, cell_state = evaluated?.State.ToString(), cell_reason = evaluated?.Reason,
            };
        }).ToList(),
        dominance_gate = DominanceEvaluation.Text(r.Evaluation?.Gate ?? DominanceGate.NotEvaluated),
        dominance_gate_reason = r.Evaluation?.GateReason ?? "취소·무결성 중단된 불완전 실행은 통계 판정 대상이 아닙니다(SR-D3.4·SR-D3.5).",
        batter_verdict = r.Evaluation is { } e ? SideResult.Text(e.Batter.Verdict) : null,
        pitcher_verdict = r.Evaluation is { } f ? SideResult.Text(f.Pitcher.Verdict) : null,
        evaluation_canonical = r.Evaluation?.Canonical(),
    };

    public static string Text(DominanceOfficialResult r) =>
        $"official 48-cell run {r.RunId}: {r.RunStatus}, {r.Cells.Count}/{DominanceMatrix.Cells.Count} cells, complete {r.Complete}{(r.Abort is { } a ? $", abort {a.Code}" : "")}; "
        + $"dominance gate {DominanceEvaluation.Text(r.Evaluation?.Gate ?? DominanceGate.NotEvaluated)} (official evidence: no, {SchemaStatus})";
}

/// <summary>The answer of a 48-cell replay: null error is a match; <see cref="GamesExecuted"/> tells a refusal before any game from a difference found by re-execution.</summary>
public sealed record DominanceReplayOutcome(Error? Error, bool GamesExecuted, DominanceEvaluation? Evaluation);

/// <summary>
/// Replay of an official 48-cell result directory against TRUSTED inputs only. The plan is re-derived from the approvals' pins (<see cref="DominancePreflight.PinnedCells"/>), never from the result
/// files; the per-cell paths are fixed by the cell id; each cell manifest must hash to the recorded value and pass the existing official replay check (<see cref="ReplayCheck.Validate"/>:
/// envelope, attestation, approvals, the REAL pinned seed file and its exact game list); its attestation must name that cell's approved seed file; every run must share the recorded run id. Only then
/// are the 48 plans re-executed from the cell definitions: every game's status, hashes and failure code and each run's <c>run-result-v1</c> must match, and the evaluation re-derived from the
/// re-executed runs with the APPROVED per-cell sample size must equal the recorded one exactly (canonical text, gate, reason, run status). Anything else is a refusal, never a warning.
/// </summary>
public static class DominanceOfficialReplay
{
    /// <param name="rerun">A test seam that observes every re-execution (default: <see cref="SimRunner.RunSpecs"/>); it never changes what is compared.</param>
    public static DominanceReplayOutcome Verify(string resultDirectory, OfficialApprovals a, string repositoryRoot, CancellationToken cancellation = default,
        Func<RunConfig, IReadOnlyList<GameSpec>, CancellationToken, RunResult>? rerun = null)
    {
        rerun ??= (config, games, token) => SimRunner.RunSpecs(config, games, null, token);
        static DominanceReplayOutcome Refuse(string code, string message) => new(new Error(code, message), false, null);
        static DominanceReplayOutcome Differs(string code, string message) => new(new Error(code, message), true, null);
        var plans = new List<(DominanceCell Cell, ReplayPlan Plan, RunConfig Config, IReadOnlyList<(string Id, ulong Seed, string Status, string Result, string Event, string Rng, string? Error)> Games,
            (string? Status, int Planned, int Completed, int Failed, int Cancelled, string? State, string? Reason) Record)>();
        string? recordedCanonical, recordedGate, recordedReason, recordedStatus, recordedBatter, recordedPitcher;
        try
        {
            var path = Path.Combine(resultDirectory, DominanceOfficial.ResultFile);
            if (!File.Exists(path))
                return Refuse("DomOfficial.result_missing", $"'{DominanceOfficial.ResultFile}'가 없습니다(완료되지 않았거나 다른 디렉터리).");
            var raw = File.ReadAllBytes(path);
            if (SeedManifestReader.DuplicateMember(raw) is { } repeated)
                return Refuse("DomOfficial.result_invalid", $"같은 JSON 항목이 두 번 나옵니다: '{repeated}'.");
            using var parsed = JsonDocument.Parse(raw);
            var doc = parsed.RootElement;
            if (doc.GetProperty("schema_version").GetString() != DominanceOfficial.ResultSchema)
                return Refuse("DomOfficial.result_schema_unsupported", $"지원하지 않는 결과 형식입니다(지원: {DominanceOfficial.ResultSchema}).");
            (recordedCanonical, recordedGate, recordedReason, recordedStatus) = (doc.GetProperty("evaluation_canonical").GetString(), doc.GetProperty("dominance_gate").GetString(),
                doc.GetProperty("dominance_gate_reason").GetString(), doc.GetProperty("run_status").GetString());
            if (!doc.GetProperty("complete").GetBoolean() || recordedStatus is not (RunReport.StatusCompleted or RunReport.StatusGamesFailed or RunReport.StatusMetricsError))
                return Refuse("DomOfficial.run_not_complete", $"run_status '{recordedStatus}'인 불완전 실행(취소·무결성 중단·일부 셀)은 replay 대상 결과가 아닙니다.");
            // OI-02: the same approved-plan rules as the preflight (48 pinned cells, then the frozen size against every pinned file), before anything recorded is trusted or any game runs
            var scan = DominancePreflight.PinnedCells(repositoryRoot, a);
            var size = DominancePreflight.PlanSizeCheck(a, scan);
            if (size.Status != CheckStatus.Pass)
                return size.Code == "Official.input_unavailable" ? Refuse(scan.Code!, scan.Message) : Refuse(size.Code!, size.Message);
            var n = a.DominanceGamesPerCell!.Value;
            if (doc.GetProperty("games_per_cell").GetInt32() != n)
                return Refuse("DomOfficial.games_per_cell_mismatch", $"기록된 셀당 표본 수가 승인값 {n}과 다릅니다.");
            if ((doc.GetProperty("git_commit_sha").GetString(), doc.GetProperty("execution_policy_id").GetString(), doc.GetProperty("execution_policy_hash").GetString(), doc.GetProperty("evaluation_policy_id").GetString())
                != (a.GitCommitSha, a.ExecutionPolicyId, a.ExecutionPolicyHash, DominanceEvaluator.PolicyId))
                return Refuse("DomOfficial.approval_record_mismatch", "결과 색인의 Git SHA·실행 정책·평가 정책 기록이 승인값과 다릅니다.");
            (recordedBatter, recordedPitcher) = (doc.GetProperty("batter_verdict").GetString(), doc.GetProperty("pitcher_verdict").GetString());
            var cells = doc.GetProperty("cells").EnumerateArray().ToList();
            if (cells.Count != DominanceMatrix.Cells.Count)
                return Refuse("DomOfficial.cell_set_mismatch", $"기록된 셀 {cells.Count}개는 48셀 행렬이 아닙니다.");
            var runId = doc.GetProperty("run_id").GetString();
            for (var i = 0; i < cells.Count; i++)
            {
                var (cell, recorded, trusted) = (DominanceMatrix.Cells[i], cells[i], scan.Cells[i]);
                string Field(string name) => recorded.GetProperty(name).GetString() ?? throw new FormatException($"'{name}'가 null입니다.");
                if (recorded.GetProperty("number").GetInt32() != cell.Number || Field("cell_id") != cell.Id || Field("competition_key") != cell.CompetitionKey)
                    return Refuse("DomOfficial.cell_order_mismatch", $"{i + 1}번째 기록이 행렬 순서의 {cell.Id}가 아닙니다(순서·교체 변조).");
                var rel = DominancePreflight.CellDirectory(cell) + "/manifest.json";
                if (Field("manifest_path") != rel)
                    return Refuse("DomOfficial.cell_path_mismatch", $"{cell.Id}의 Manifest 경로는 '{rel}'로 고정됩니다.");
                var bytes = File.ReadAllBytes(Path.Combine([resultDirectory, .. rel.Split('/')]));
                if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != Field("manifest_sha256"))
                    return Refuse("DomOfficial.cell_manifest_hash_mismatch", $"{cell.Id}의 Manifest 파일 SHA-256이 기록값과 다릅니다.");
                if (SeedManifestReader.DuplicateMember(bytes) is { } dup)
                    return Refuse("Replay.manifest_invalid", $"{cell.Id}: 같은 JSON 항목이 두 번 나옵니다: '{dup}'.");
                using var manifestDoc = JsonDocument.Parse(bytes);
                var manifest = manifestDoc.RootElement;
                var plan = ReplayCheck.Validate(manifest, a, repositoryRoot);
                if (!plan.IsSuccess)
                    return new DominanceReplayOutcome(new Error(plan.Error!.Value.Code, $"{cell.Id}: {plan.Error.Value.Message}"), false, null);
                if (plan.Value.Stop is not null)
                    return Refuse("DomOfficial.stop_not_allowed", $"{cell.Id}: fc3-dom은 전체 실행 계약입니다(SR-D5).");
                var envelope = manifest.GetProperty("official");
                var att = OfficialAttestation.FromJson(envelope.GetProperty("attestation")).Value;
                if (att.SeedManifestPath != trusted.SeedManifestPath || att.CompetitionKey != cell.CompetitionKey)
                    return Refuse("DomOfficial.seed_plan_mismatch", $"{cell.Id}: Attestation의 Seed 파일 '{att.SeedManifestPath}'이(가) 승인값이 이 셀에 결속한 '{trusted.SeedManifestPath}'가 아닙니다.");
                if (plan.Value.RunId != runId)
                    return Refuse("DomOfficial.run_id_mismatch", $"{cell.Id}: 실행 ID '{plan.Value.RunId}'가 결과의 '{runId}'와 다릅니다(서로 다른 실행의 혼용).");
                if ((Field("config_hash"), Field("run_result_hash"), Field("attestation_hash"), Field("seed_manifest_sha256"), Field("seed_manifest_path"))
                    != (plan.Value.ConfigHash, plan.Value.RunResultHash, envelope.GetProperty("attestation_hash").GetString(), att.SeedManifestSha256, att.SeedManifestPath))
                    return Refuse("DomOfficial.cell_record_mismatch", $"{cell.Id}: 결과 색인의 해시·Seed 파일 기록이 셀 Manifest와 다릅니다.");
                var config = DominanceMatrix.Config(cell, plan.Value.RunId, "official");
                if (!config.IsSuccess)
                    return new DominanceReplayOutcome(config.Error, false, null);
                if (plan.Value.CheckAgainst(config.Value) is { } unlike)
                    return Refuse(unlike.Code, $"{cell.Id}: {unlike.Message}");
                var games = manifest.GetProperty("results").GetProperty("games").EnumerateArray()
                    .Select(g => (g.GetProperty("game_id").GetString()!, g.GetProperty("seed").GetUInt64(), g.GetProperty("status").GetString()!, g.GetProperty("result_hash").GetString()!,
                        g.GetProperty("event_hash").GetString()!, g.GetProperty("rng_hash").GetString()!, g.GetProperty("error_code").GetString())).ToList();
                plans.Add((cell, plan.Value, config.Value, games, (Field("run_status"), recorded.GetProperty("planned_games").GetInt32(), recorded.GetProperty("completed_games").GetInt32(),
                    recorded.GetProperty("failed_games").GetInt32(), recorded.GetProperty("cancelled_games").GetInt32(), recorded.GetProperty("cell_state").GetString(), recorded.GetProperty("cell_reason").GetString())));
            }
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException or JsonException or IOException)
        {
            return Refuse("DomOfficial.result_invalid", $"48셀 결과를 읽을 수 없습니다: {e.Message}");
        }

        var reruns = new List<RunResult>();
        foreach (var (cell, plan, config, games, _) in plans)
        {
            var again = rerun(config, plan.Games, cancellation);
            if (again.CancelledCount > 0)
                return Differs("DomOfficial.replay_cancelled", $"{cell.Id}: replay가 취소되었습니다(일치로 보지 않음).");
            if (again.Games.Count != games.Count || again.Games.Zip(games).Any(p => (p.First.Game.GameId, p.First.Game.Seed, p.First.Status.ToString(), p.First.ResultHash, p.First.EventHash, p.First.RngHash, p.First.Failure?.Code) != p.Second))
                return Differs("DomOfficial.result_games_mismatch", $"{cell.Id}: 경기별 상태·결과/이벤트/RNG 해시·실패 코드가 재실행과 다릅니다.");
            if (again.ConfigHash != plan.ConfigHash || again.RunResultHash != plan.RunResultHash)
                return Differs("DomOfficial.run_result_hash_mismatch", $"{cell.Id}: 재실행 run_result_hash {again.RunResultHash}이(가) 기록값 {plan.RunResultHash}과 다릅니다.");
            reruns.Add(again);
        }

        var evaluation = DominanceEvaluator.Evaluate(reruns, a.DominanceGamesPerCell);
        for (var i = 0; i < reruns.Count; i++)
        {
            var (run, e) = (reruns[i], evaluation.Cells[i]);
            if (plans[i].Record != (RunReport.RunStatus(run), run.Requested, run.CompletedCount, run.FailedCount, run.CancelledCount, e.State.ToString(), e.Reason))
                return Differs("DomOfficial.cell_record_mismatch", $"{plans[i].Cell.Id}: 결과 색인의 셀 상태·사유·경기 수가 재실행 결과와 다릅니다.");
        }

        var status = DominanceOfficial.StatusOf(DominanceRunTermination.Completed, reruns, evaluation);
        return recordedCanonical == evaluation.Canonical() && recordedGate == DominanceEvaluation.Text(evaluation.Gate) && recordedReason == evaluation.GateReason && recordedStatus == status
            && recordedBatter == SideResult.Text(evaluation.Batter.Verdict) && recordedPitcher == SideResult.Text(evaluation.Pitcher.Verdict)
            ? new DominanceReplayOutcome(null, true, evaluation)
            : Differs("DomOfficial.evaluation_mismatch", "재실행 결과로 다시 계산한 48셀 판정·Gate·실행 상태가 기록과 다릅니다.");
    }
}
