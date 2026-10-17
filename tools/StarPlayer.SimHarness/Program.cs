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
            var options = Parse(args);
            return options.GetValueOrDefault("") switch
            {
                "run" => RunCommand(options),
                "replay" => ReplayCommand(options),
                _ => Usage("명령은 run 또는 replay여야 합니다."),
            };
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or JsonException or IOException or KeyNotFoundException or InvalidOperationException)
        {
            return Usage($"입력을 처리할 수 없습니다: {e.Message}");
        }
    }

    private static int RunCommand(Dictionary<string, string> o)
    {
        var seeds = ParseSeeds(o.GetValueOrDefault("seeds", "3,11,29"));
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

    private static int ReplayCommand(Dictionary<string, string> o)
    {
        if (!o.TryGetValue("manifest", out var path))
            return Usage("--manifest FILE이 필요합니다.");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var engine = root.GetProperty("engine");
        if (engine.TryGetProperty("tuning_file_sha256", out var customTuning) && customTuning.ValueKind == JsonValueKind.String)
            return Usage("replay는 --tuning으로 읽은 시험용 튜닝 파일의 실행을 다시 만들 수 없습니다(매니페스트에 파일 해시만 있음).");
        var cohort = root.GetProperty("run").GetProperty("cohort_id").GetString();
        if (cohort != NeutralCohort.Id)
            return Usage($"replay는 '{NeutralCohort.Id}' 코호트만 다시 만들 수 있습니다(받은 값: '{cohort}'; 다른 코호트의 입력은 매니페스트에 해시만 있음).");
        var built = Build(root.GetProperty("run").GetProperty("run_id").GetString()!, "replay",
            engine.GetProperty("rules_profile_id").GetString() == HarnessData.AutomatedProfile ? "automated" : "non-automated",
            engine.GetProperty("advancement_policy_version").GetString() == AdvancementPolicy.Version ? "v1" : "v2", engine.GetProperty("event_budget").GetInt32());
        if (!built.IsSuccess)
            return Usage($"{built.Error!.Value.Code}: {built.Error.Value.Message}");
        var seeds = root.GetProperty("games").EnumerateArray().Select(g => g.GetProperty("seed").GetUInt64()).ToList();
        var result = SimRunner.Run(built.Value, seeds);
        var expected = root.GetProperty("results").GetProperty("run_result_hash").GetString();
        var same = result.RunResultHash == expected && result.ConfigHash == root.GetProperty("run").GetProperty("config_hash").GetString();
        Console.WriteLine(same ? $"REPLAY MATCH run_result_hash {result.RunResultHash}" : $"REPLAY DIFFERS: expected {expected}, got {result.RunResultHash}");
        return same ? 0 : 1;
    }

    private static Result<RunConfig> Build(string runId, string mode, string rules, string advancement, int eventBudget, TuningProfile? custom = null)
    {
        var tuning = custom is null ? HarnessData.LoadTuningV4() : Result<TuningProfile>.Success(custom);
        if (!tuning.IsSuccess)
            return Result<RunConfig>.Failure(tuning.Error!.Value);
        var profile = HarnessData.LoadRules(rules == "automated" ? HarnessData.AutomatedProfile : HarnessData.NonAutomatedProfile);
        if (!profile.IsSuccess)
            return Result<RunConfig>.Failure(profile.Error!.Value);
        var park = ParkFactorProfile.Create(ParkFactorProfileId.Create("pf-sim-neutral").Value, StadiumId.Create("stadium-sim-neutral").Value, 1, new DateOnly(2030, 1, 1),
            ParkFactor.Neutral, ParkFactor.Neutral, ParkFactor.Neutral, ParkFactorBounds.Create(1, ParkFactorBounds.AbsoluteMaxPermille).Value);
        if (!park.IsSuccess)
            return Result<RunConfig>.Failure(park.Error!.Value);
        return RunConfig.Create(runId, mode, NeutralCohort.Create(), profile.Value, tuning.Value, park.Value.ToSnapshot(), advancement == "v1" ? AdvancementPolicy.Version : AdvancementPolicy.VersionV2, eventBudget);
    }

    private static List<ulong> ParseSeeds(string text) => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => ulong.Parse(s, CultureInfo.InvariantCulture)).ToList();

    private static Dictionary<string, string> Parse(string[] args)
    {
        var o = new Dictionary<string, string>();
        if (args.Length == 0)
            throw new ArgumentException("명령이 없습니다.");
        o[""] = args[0];
        for (var i = 1; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
                throw new ArgumentException($"알 수 없는 인수: {args[i]}");
            o[args[i][2..]] = args[i + 1];
        }

        return o;
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine("usage: StarPlayer.SimHarness run [--seeds 3,11,29] [--rules non-automated|automated] [--advancement v1|v2] [--event-budget N] [--tuning FILE] [--run-id ID] [--out DIR]");
        Console.Error.WriteLine("       StarPlayer.SimHarness replay --manifest FILE");
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
