using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.SimHarness;

/// <summary>
/// <c>StarPlayer.SimHarness run [--seeds 3,11,29] [--rules non-automated|automated] [--advancement v1|v2] [--event-budget N] [--tuning FILE] [--run-id ID] [--out DIR]</c> plays the listed games on the neutral
/// cohort and writes <c>manifest.json</c>, <c>summary.json</c> and <c>summary.txt</c>; <c>replay --manifest FILE</c> plays a manifest again and compares the hashes (a stop-aware <c>fc2-dry</c> manifest also
/// needs <c>--seed-manifest REL --seed-sha256 HEX</c>, the trusted source of its stop parameters); <c>dry --seed-manifest REL --seed-sha256 HEX</c> is the non-official stop-aware dry run (ADR-009 SR-D4). <c>--tuning FILE</c> reads a
/// tuning file through the same strict reader instead of the embedded one (a test seam: its hash is recorded in the manifest and such a manifest cannot be replayed).
/// Exit code (<see cref="ExitCodeOf"/>): 0 every game completed and the metrics were computed (a metric verdict of NOT_EVALUATED / INSUFFICIENT_SAMPLE is still 0), 1 a game failed / was cancelled or a replay
/// differed, 2 bad arguments, 3 every game completed but the metrics could not be computed (for instance <c>Metrics.target_missing</c>). This phase is smoke-sized only: more than
/// <see cref="SmokeMaxGames"/> games is refused (the 100-game and 100,000-PA validations are a separate, unapproved package).
/// </summary>
public static class Program
{
    public const int SmokeMaxGames = 10;

    public const int ExitMetricsError = 3;

    /// <summary>
    /// 0 success; 1 a game did not complete (it outranks a metrics error, whose code stays in the summary) or a stop-aware run was cancelled; 3 the games completed but the metrics could not be computed;
    /// 2 a stop-aware run aborted because trust in its inputs broke (the existing "refused input" code, ADR-009 SR-D3.4 / SR-D9.2). No new code means success, and 0 is never a gate verdict.
    /// </summary>
    public static int ExitCodeOf(RunResult run) =>
        run.StopAware && run.Termination == RunTermination.AbortedIntegrity ? 2 : !run.AllCompleted ? 1 : run.MetricsComputed ? 0 : ExitMetricsError;

    public static int Main(string[] args)
    {
        try
        {
            var parsed = Cli.Parse(args);
            if (!parsed.IsSuccess)
                return Usage($"{parsed.Error!.Value.Code}: {parsed.Error.Value.Message}");
            var options = parsed.Value.Options;
            return parsed.Value.Name switch
            {
                "run" => RunCommand(options),
                "replay" => ReplayCommand(options),
                "official" => OfficialCommand(options),
                "dry" => DryCommand(options),
                "official-dom" => OfficialDominanceCommand(options),
                "replay-dom" => ReplayDominanceCommand(options),
                _ => Usage($"Cli.command_unknown: {parsed.Value.Name}"),
            };
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or JsonException or IOException or KeyNotFoundException or InvalidOperationException)
        {
            return Usage($"입력을 처리할 수 없습니다: {e.Message}");
        }
    }

    private static int RunCommand(IReadOnlyDictionary<string, string> o)
    {
        var seeds = Cli.ParseSeeds(o.GetValueOrDefault("seeds", "3,11,29")).Value;
        if (seeds.Count is < 1 or > SmokeMaxGames)
            return Usage($"스모크 실행은 1~{SmokeMaxGames}경기까지만 허용됩니다(대규모 검증은 승인 전).");
        string? tuningFileSha256 = null;
        TuningProfile? tuning = null;
        if (o.TryGetValue("tuning", out var tuningPath))
        {
            var text = File.ReadAllText(tuningPath);
            var parsed = HarnessData.ParseTuning(text);
            if (!parsed.IsSuccess)
                return Usage($"{parsed.Error!.Value.Code}: {parsed.Error.Value.Message}");
            (tuning, tuningFileSha256) = (parsed.Value, Hashing.Sha256(text));
        }

        var built = Build(o.GetValueOrDefault("run-id", "smoke-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)), "smoke",
            o.GetValueOrDefault("rules", "non-automated"), o.GetValueOrDefault("advancement", "v2"), int.Parse(o.GetValueOrDefault("event-budget", "3000"), CultureInfo.InvariantCulture), tuning);
        if (!built.IsSuccess)
            return Usage($"{built.Error!.Value.Code}: {built.Error.Value.Message}");

        var started = DateTimeOffset.UtcNow;
        var result = SimRunner.Run(built.Value, seeds);
        var (sha, dirty) = Git();
        var environment = EnvironmentInfo.Capture(started, DateTimeOffset.UtcNow, sha, dirty);
        var directory = o.GetValueOrDefault("out") ?? Path.Combine(RepositoryRoot() ?? Path.GetTempPath(), "build", "simharness", built.Value.RunId);
        RunReport.Write(result, environment, directory, tuningFileSha256);
        Console.Write(RunReport.Text(result));
        Console.WriteLine($"output: {directory}");
        if (!result.MetricsComputed)
            Console.Error.WriteLine($"METRICS ERROR {result.Metrics.Error!.Value.Code}: {result.Metrics.Error.Value.Message} (run {built.Value.RunId}, config {result.ConfigHash}, inputs {built.Value.Cohort.InputsHash})");
        return ExitCodeOf(result);
    }

    private static int ReplayCommand(IReadOnlyDictionary<string, string> o)
    {
        var raw = File.ReadAllBytes(o["manifest"]);
        if (SeedManifestReader.DuplicateMember(raw) is { } repeated)
            return Usage($"Replay.manifest_invalid: 같은 JSON 항목이 두 번 나옵니다: '{repeated}'.");
        using var document = JsonDocument.Parse(raw);
        if (o.ContainsKey("seed-manifest") != o.ContainsKey("seed-sha256"))
            return Usage("Cli.option_required: --seed-manifest와 --seed-sha256는 함께 지정해야 합니다.");
        var dryPin = o.TryGetValue("seed-manifest", out var dryPath) ? new DryStopPin(dryPath, o["seed-sha256"]) : null;
        var plan = ReplayCheck.Validate(document.RootElement, OfficialApprovals.Current, RepositoryRoot(), dryPin);
        if (!plan.IsSuccess)
            return Usage($"{plan.Error!.Value.Code}: {plan.Error.Value.Message}");
        var p = plan.Value;
        var built = Build(p.RunId, "replay", p.Rules, p.Advancement, p.EventBudget, null, p.Cohort, p.Policy, p.CompetitionKey);
        if (!built.IsSuccess)
            return Usage($"{built.Error!.Value.Code}: {built.Error.Value.Message}");
        if (p.CheckAgainst(built.Value) is { } unlike)
            return Usage($"{unlike.Code}: {unlike.Message}");
        if (p.Stop is { } trusted)
        {
            // ADR-009 SR-D8: the same stop-aware executor on the TRUSTED plan re-derives the stop point; the recorded values are only compared with it
            var derived = SimRunner.RunStopAware(built.Value, trusted, integrity: dryPin is null ? null : () => SeedManifestReader.Read(RepositoryRoot() ?? "", dryPin.Path, dryPin.Sha256).Error);
            var differs = StopReplay.Compare(p.Recorded!, p.RunResultHash, derived);
            Console.WriteLine(differs is null
                ? $"REPLAY MATCH run_result_hash {derived.RunResultHash} ({RunResultText.V2}, stop {derived.Stop!.ReasonText} after {derived.Stop.ExecutedGames} games, valid PA {derived.Stop.CumulativePa})"
                : $"REPLAY DIFFERS {differs.Value.Code}: {differs.Value.Message}");
            return differs is null ? 0 : 1;
        }

        var result = SimRunner.RunSpecs(built.Value, p.Games);
        var same = result.RunResultHash == p.RunResultHash && result.ConfigHash == p.ConfigHash;
        Console.WriteLine(same ? $"REPLAY MATCH run_result_hash {result.RunResultHash}" : $"REPLAY DIFFERS: expected {p.RunResultHash}, got {result.RunResultHash}");
        return same ? 0 : 1;
    }

    /// <summary>
    /// The official run (FC-D10): the preflight only. It prints the official result document and refuses (exit 2) unless every check passes; with the F-C2-A approvals nothing is frozen, so it always
    /// refuses with NOT_READY. Even a ready preflight starts no game here: the official execution path is F-C2-B/C.
    /// </summary>
    private static int OfficialCommand(IReadOnlyDictionary<string, string> o)
    {
        var root = RepositoryRoot();
        if (root is null)
            return Usage("Official.repository_unknown: StarPlayer.sln이 있는 저장소 안에서 실행해야 합니다.");
        var report = OfficialPreflight.Run(new OfficialRequest(root, o["seed-manifest"], o["run-id"], o.GetValueOrDefault("out")), OfficialApprovals.Current, GitIntegrity.Check(root));
        Console.WriteLine(JsonSerializer.Serialize(OfficialOutput.Result(report), RunReport.Json));
        foreach (var c in report.Checks.Where(c => c.Status != CheckStatus.Pass))
            Console.Error.WriteLine($"OFFICIAL PREFLIGHT {c.Status} {c.Id} {c.Code}: {c.Message}");
        return Usage(report.Manifest?.Family == DominanceMatrix.Family
            ? "Official.dominance_matrix_required: fc3-dom Seed 파일 하나는 48셀 행렬의 한 셀이므로 단독 실행하지 않습니다(official-dom 명령; 게임 미실행)."
            : report.Ready ? "Official.execution_not_implemented: 이 계열의 공식 실행 경로는 구현되지 않았습니다(게임 미실행)." : $"{report.ErrorCode}: 공식 Preflight가 실행을 거부했습니다(게임 미실행).");
    }

    /// <summary>
    /// The official 48-cell run: <see cref="DominancePreflight"/> against <see cref="OfficialApprovals.Current"/> and the real Git state; a refused preflight prints its document and exits 2 without
    /// starting a game. Only a READY preflight enters <see cref="DominanceOfficial.Execute"/>. With the current approvals (no Final Execution Freeze) it always refuses.
    /// </summary>
    private static int OfficialDominanceCommand(IReadOnlyDictionary<string, string> o)
    {
        var root = RepositoryRoot();
        if (root is null)
            return Usage("Official.repository_unknown: StarPlayer.sln이 있는 저장소 안에서 실행해야 합니다.");
        var approvals = OfficialApprovals.Current;
        var report = DominancePreflight.Run(new DominanceOfficialRequest(root, o["run-id"], o.GetValueOrDefault("out")), approvals, GitIntegrity.Check(root));
        if (!report.Ready)
        {
            Console.WriteLine(JsonSerializer.Serialize(DominanceOfficial.PreflightDocument(report, o["run-id"]), RunReport.Json));
            foreach (var c in report.Checks.Where(c => c.Status != CheckStatus.Pass))
                Console.Error.WriteLine($"OFFICIAL-DOM PREFLIGHT {c.Status} {c.Id} {c.Code}: {c.Message}");
            return Usage($"{report.ErrorCode}: 48셀 공식 Preflight가 실행을 거부했습니다(게임 미실행, 통계 판정 아님).");
        }

        var result = DominanceOfficial.Execute(report, o["run-id"], approvals, root);
        Console.WriteLine(DominanceOfficial.Text(result));
        Console.WriteLine($"output: {report.OutputDirectory}");
        return result.ExitCode;
    }

    /// <summary>Replay of an official 48-cell result directory against <see cref="OfficialApprovals.Current"/>: 0 match, 1 a difference found by re-execution, 2 refused before any game.</summary>
    private static int ReplayDominanceCommand(IReadOnlyDictionary<string, string> o)
    {
        var root = RepositoryRoot();
        if (root is null)
            return Usage("Official.repository_unknown: StarPlayer.sln이 있는 저장소 안에서 실행해야 합니다.");
        var outcome = DominanceOfficialReplay.Verify(o["result"], OfficialApprovals.Current, root);
        if (outcome.Error is { } e && !outcome.GamesExecuted)
            return Usage($"{e.Code}: {e.Message}");
        Console.WriteLine(outcome.Error is { } d ? $"REPLAY DIFFERS {d.Code}: {d.Message}" : $"REPLAY MATCH 48 cells, dominance gate {DominanceEvaluation.Text(outcome.Evaluation!.Gate)}");
        return outcome.Error is null ? 0 : 1;
    }

    /// <summary>
    /// The non-official stop-aware dry run (ADR-009 SR-D4): the <c>fc2-dry</c> seed file is read only against the given pin, must declare a small PA target, and supplies the plan, cohort, policy and rules;
    /// the rest is the fixed official run profile (embedded tuning v4, advancement v2, budget 3,000, neutral park). The result is diagnostic: never an official statistic, never a gate input.
    /// </summary>
    private static int DryCommand(IReadOnlyDictionary<string, string> o)
    {
        var root = RepositoryRoot();
        if (root is null)
            return Usage("Dry.repository_unknown: StarPlayer.sln이 있는 저장소 안에서 실행해야 합니다.");
        var read = SeedManifestReader.Read(root, o["seed-manifest"], o["seed-sha256"]);
        if (!read.IsSuccess)
            return Usage($"{read.Error!.Value.Code}: {read.Error.Value.Message}");
        var sm = read.Value;
        if (sm.Family != "fc2-dry")
            return Usage($"Dry.family_not_dry: dry 명령은 fc2-dry Seed 파일만 실행합니다(받은 family '{sm.Family}'; 공식 실행은 official 명령).");
        var plan = StopPlan.FromSeedManifest(sm, o["seed-manifest"]);
        if (!plan.IsSuccess)
            return Usage($"{plan.Error!.Value.Code}: {plan.Error.Value.Message}");
        var cohort = CohortRegistry.Resolve(sm.CohortId);
        if (!cohort.IsSuccess)
            return Usage($"{cohort.Error!.Value.Code}: {cohort.Error.Value.Message}");
        var policy = PolicyRegistry.Resolve(sm.PolicyId);
        if (!policy.IsSuccess)
            return Usage($"{policy.Error!.Value.Code}: {policy.Error.Value.Message}");
        var rules = sm.RulesProfileId switch { HarnessData.AutomatedProfile => "automated", HarnessData.NonAutomatedProfile => "non-automated", _ => null };
        if (rules is null)
            return Usage($"Dry.rules_unknown: 알 수 없는 규칙 프로파일입니다: '{sm.RulesProfileId}'.");
        var runId = o.GetValueOrDefault("run-id", "dry-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
        var built = Build(runId, "dry", rules, "v2", OfficialProfile.EventBudget, null, cohort.Value, policy.Value, sm.CompetitionKey);
        if (!built.IsSuccess)
            return Usage($"{built.Error!.Value.Code}: {built.Error.Value.Message}");
        var directory = o.GetValueOrDefault("out") ?? Path.Combine(root, "build", "simharness", built.Value.RunId);
        if (Directory.Exists(directory) || File.Exists(directory))
            return Usage($"Dry.output_exists: 출력 경로가 이미 있습니다(덮어쓰지 않음): '{directory}'.");

        var started = DateTimeOffset.UtcNow;
        // SR-D3.4: the pinned seed file is re-verified before every block; a change mid-run aborts the run without a stop record
        var result = SimRunner.RunStopAware(built.Value, plan.Value, EvaluationPolicy.Unapproved, integrity: () => SeedManifestReader.Read(root, o["seed-manifest"], o["seed-sha256"]).Error);
        var (sha, dirty) = Git();
        RunReport.Write(result, EnvironmentInfo.Capture(started, DateTimeOffset.UtcNow, sha, dirty), directory);
        Console.Write(RunReport.Text(result));
        Console.WriteLine($"output: {directory}");
        return ExitCodeOf(result);
    }

    private static Result<RunConfig> Build(string runId, string mode, string rules, string advancement, int eventBudget, TuningProfile? custom = null, CohortInputs? cohort = null, IGamePolicy? policy = null,
        string competitionKey = "sim")
    {
        var tuning = custom is null ? HarnessData.LoadTuningV4() : Result<TuningProfile>.Success(custom);
        if (!tuning.IsSuccess)
            return Result<RunConfig>.Failure(tuning.Error!.Value);
        var profile = HarnessData.LoadRules(rules switch { "automated" => HarnessData.AutomatedProfile, "non-automated" => HarnessData.NonAutomatedProfile, _ => "" });
        if (!profile.IsSuccess)
            return Result<RunConfig>.Failure(profile.Error!.Value);
        var park = HarnessData.NeutralPark();
        if (!park.IsSuccess)
            return Result<RunConfig>.Failure(park.Error!.Value);
        if (advancement is not ("v1" or "v2"))
            return Result<RunConfig>.Failure(new Error("Harness.advancement_unknown", $"진루 정책은 v1 또는 v2여야 합니다(받은 값: '{advancement}')."));
        return RunConfig.Create(runId, mode, cohort ?? NeutralCohort.Create(), profile.Value, tuning.Value, park.Value, advancement == "v1" ? AdvancementPolicy.Version : AdvancementPolicy.VersionV2, eventBudget,
            policy, competitionKey);
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine("usage: StarPlayer.SimHarness run [--seeds 3,11,29] [--rules non-automated|automated] [--advancement v1|v2] [--event-budget N] [--tuning FILE] [--run-id ID] [--out DIR]");
        Console.Error.WriteLine("       StarPlayer.SimHarness replay --manifest FILE [--seed-manifest REPO_RELATIVE_FC2_DRY_FILE --seed-sha256 HEX]");
        Console.Error.WriteLine("       StarPlayer.SimHarness dry --seed-manifest REPO_RELATIVE_FC2_DRY_FILE --seed-sha256 HEX [--run-id ID] [--out DIR]");
        Console.Error.WriteLine("       StarPlayer.SimHarness official --seed-manifest REPO_RELATIVE_FILE --run-id ID [--out build/simharness/official/DIR]");
        Console.Error.WriteLine("       StarPlayer.SimHarness official-dom --run-id ID [--out build/simharness/official/DIR]");
        Console.Error.WriteLine("       StarPlayer.SimHarness replay-dom --result OFFICIAL_48_CELL_RESULT_DIR");
        return 2;
    }

    private static string? RepositoryRoot()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                return d.FullName;
        return null;
    }

    private static (string? Sha, bool? Dirty) Git()
    {
        static string? Run(string arguments)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("git", arguments) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = RepositoryRoot() ?? "." });
                var output = p?.StandardOutput.ReadToEnd();
                p?.WaitForExit();
                return p?.ExitCode == 0 ? output : null;
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                return null;
            }
        }

        var sha = Run("rev-parse HEAD")?.Trim();
        var status = Run("status --porcelain");
        return (sha, status is null ? null : status.Length > 0);
    }
}
