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
/// cohort and writes <c>manifest.json</c>, <c>summary.json</c> and <c>summary.txt</c>; <c>replay --manifest FILE</c> plays a neutral-cohort manifest again and compares the hashes. <c>--tuning FILE</c> reads a
/// tuning file through the same strict reader instead of the embedded one (a test seam: its hash is recorded in the manifest and such a manifest cannot be replayed).
/// Exit code (<see cref="ExitCodeOf"/>): 0 every game completed and the metrics were computed (a metric verdict of NOT_EVALUATED / INSUFFICIENT_SAMPLE is still 0), 1 a game failed / was cancelled or a replay
/// differed, 2 bad arguments, 3 every game completed but the metrics could not be computed (for instance <c>Metrics.target_missing</c>). This phase is smoke-sized only: more than
/// <see cref="SmokeMaxGames"/> games is refused (the 100-game and 100,000-PA validations are a separate, unapproved package).
/// </summary>
public static class Program
{
    public const int SmokeMaxGames = 10;

    public const int ExitMetricsError = 3;

    /// <summary>0 success; 1 a game did not complete (it outranks a metrics error, whose code stays in the summary); 3 the games completed but the metrics could not be computed.</summary>
    public static int ExitCodeOf(RunResult run) => !run.AllCompleted ? 1 : run.MetricsComputed ? 0 : ExitMetricsError;

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
        var plan = ReplayCheck.Validate(document.RootElement, OfficialApprovals.Current, RepositoryRoot());
        if (!plan.IsSuccess)
            return Usage($"{plan.Error!.Value.Code}: {plan.Error.Value.Message}");
        var p = plan.Value;
        var built = Build(p.RunId, "replay", p.Rules, p.Advancement, p.EventBudget, null, p.Cohort, p.Policy, p.CompetitionKey);
        if (!built.IsSuccess)
            return Usage($"{built.Error!.Value.Code}: {built.Error.Value.Message}");
        if (p.CheckAgainst(built.Value) is { } unlike)
            return Usage($"{unlike.Code}: {unlike.Message}");
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
        return Usage(report.Ready ? "Official.execution_not_implemented: 공식 실행 경로는 F-C2-B/C에서 구현됩니다." : $"{report.ErrorCode}: 공식 Preflight가 실행을 거부했습니다(게임 미실행).");
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
        Console.Error.WriteLine("       StarPlayer.SimHarness replay --manifest FILE");
        Console.Error.WriteLine("       StarPlayer.SimHarness official --seed-manifest REPO_RELATIVE_FILE --run-id ID [--out build/simharness/official/DIR]");
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
