using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// Codex F-04: a run whose games all finish but whose metrics could not be computed must not exit 0. NOT_EVALUATED and INSUFFICIENT_SAMPLE are reported results of a working computation, not errors.
/// The exit code is checked on a REAL child process (the built harness dll), not only on the value <see cref="Program.Main"/> returns.
/// </summary>
public class CliExitCodeTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), "simharness-tests", "cli-" + Guid.NewGuid().ToString("N"));

    /// <summary>The embedded tuning file with the declared babip target renamed, so the metric evaluation cannot find it (<c>Metrics.target_missing</c>). Written to a TEMP file; the versioned content file is never touched.</summary>
    private static string WriteTuningWithoutBabipTarget(string dir)
    {
        Directory.CreateDirectory(dir);
        var node = JsonNode.Parse(HarnessData.ReadResource(HarnessData.TuningResource))!.AsObject();
        var target = node["targets"]!.AsArray().Select(t => t!.AsObject()).Single(t => t["id"]!.GetValue<string>() == "babip");
        target["id"] = "babip_renamed_for_test";
        var path = Path.Combine(dir, "tuning-no-babip.json");
        File.WriteAllText(path, node.ToJsonString());
        return path;
    }

    private sealed record Child(int ExitCode, string Output, string Error);

    private static Child Spawn(params string[] arguments)
    {
        var dll = typeof(Program).Assembly.Location;
        var host = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "") == "dotnet" ? Environment.ProcessPath! : "dotnet";
        var start = new ProcessStartInfo(host) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(dll);
        foreach (var a in arguments)
            start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000), "the harness process did not finish in 120 s");
        return new Child(process.ExitCode, output.Result, error.Result);
    }

    private static JsonElement Read(string dir, string file) => JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, file))).RootElement.Clone();

    [Fact]
    public void A_normal_process_run_exits_zero_and_reports_every_metric_as_not_evaluated_without_an_error()
    {
        var dir = TempDir();
        try
        {
            var child = Spawn("run", "--seeds", "3", "--run-id", "proc-ok", "--out", dir);

            Assert.Equal(0, child.ExitCode);
            var summary = Read(dir, "summary.json");
            Assert.Equal(JsonValueKind.Null, summary.GetProperty("metrics").GetProperty("error").ValueKind);
            Assert.Equal(JsonValueKind.Null, summary.GetProperty("metrics").GetProperty("error_code").ValueKind);
            Assert.All(summary.GetProperty("metrics").GetProperty("items").EnumerateArray(), m => Assert.Equal("NotEvaluated", m.GetProperty("status").GetString()));
            Assert.Equal("COMPLETED", summary.GetProperty("run_status").GetString());
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Finished_games_with_a_metrics_error_make_the_process_exit_nonzero_and_the_error_is_kept_for_reproduction()
    {
        var dir = TempDir();
        try
        {
            var tuning = WriteTuningWithoutBabipTarget(dir);
            var child = Spawn("run", "--seeds", "3,11", "--run-id", "proc-metrics-error", "--tuning", tuning, "--out", Path.Combine(dir, "out"));

            Assert.NotEqual(0, child.ExitCode);
            Assert.Equal(3, child.ExitCode);                                               // 3: every game completed, the metrics could not be computed
            Assert.Contains("Metrics.target_missing", child.Output, StringComparison.Ordinal);
            Assert.Contains("babip", child.Output, StringComparison.Ordinal);               // the failed metric is named

            var out_ = Path.Combine(dir, "out");
            var summary = Read(out_, "summary.json");
            Assert.Equal("Metrics.target_missing", summary.GetProperty("metrics").GetProperty("error_code").GetString());
            Assert.StartsWith("Metrics.target_missing:", summary.GetProperty("metrics").GetProperty("error").GetString(), StringComparison.Ordinal);
            Assert.Equal("METRICS_ERROR", summary.GetProperty("run_status").GetString());
            Assert.Equal(0, summary.GetProperty("metrics").GetProperty("items").GetArrayLength());
            // the games themselves finished, and the summary still names them (GameId, seed) next to the error
            var games = summary.GetProperty("games").EnumerateArray().ToList();
            Assert.Equal(new[] { 3UL, 11UL }, games.Select(g => g.GetProperty("seed").GetUInt64()).ToArray());
            Assert.All(games, g => Assert.Equal("Completed", g.GetProperty("status").GetString()));

            var manifest = Read(out_, "manifest.json");
            Assert.Equal("proc-metrics-error", manifest.GetProperty("run").GetProperty("run_id").GetString());
            Assert.Equal(64, manifest.GetProperty("run").GetProperty("config_hash").GetString()!.Length);
            Assert.Equal(64, manifest.GetProperty("engine").GetProperty("cohort_inputs_hash").GetString()!.Length);
            Assert.Equal("file", manifest.GetProperty("engine").GetProperty("tuning_source").GetString());
            Assert.Equal(64, manifest.GetProperty("engine").GetProperty("tuning_file_sha256").GetString()!.Length);
            Assert.Equal(2, manifest.GetProperty("games").GetArrayLength());
            Assert.Equal(2, manifest.GetProperty("results").GetProperty("games_completed").GetInt32());

            // a manifest of a run on a test tuning file cannot be replayed (it has only the file's hash)
            Assert.Equal(2, Spawn("replay", "--manifest", Path.Combine(out_, "manifest.json")).ExitCode);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void A_failed_game_still_makes_the_process_exit_one_whether_or_not_the_metrics_computed()
    {
        var dir = TempDir();
        try
        {
            var failed = Spawn("run", "--seeds", "29,3", "--run-id", "proc-game-failed", "--event-budget", "40", "--out", dir);
            Assert.Equal(1, failed.ExitCode);
            Assert.Equal("GAMES_FAILED", Read(dir, "summary.json").GetProperty("run_status").GetString());

            var both = Spawn("run", "--seeds", "29", "--run-id", "proc-both", "--event-budget", "40", "--tuning", WriteTuningWithoutBabipTarget(dir), "--out", Path.Combine(dir, "both"));
            Assert.Equal(1, both.ExitCode);                                                // a failed game outranks the metrics error: 1, never 0 or 3
            Assert.Equal("Metrics.target_missing", Read(Path.Combine(dir, "both"), "summary.json").GetProperty("metrics").GetProperty("error_code").GetString());
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void The_value_Main_returns_is_the_process_exit_code()
    {
        var dir = TempDir();
        try
        {
            var tuning = WriteTuningWithoutBabipTarget(dir);
            Assert.Equal(0, Program.Main(["run", "--seeds", "3", "--run-id", "inproc-ok", "--out", Path.Combine(dir, "a")]));
            Assert.Equal(3, Program.Main(["run", "--seeds", "3", "--run-id", "inproc-bad", "--tuning", tuning, "--out", Path.Combine(dir, "b")]));
            Assert.Equal(1, Program.Main(["run", "--seeds", "29", "--run-id", "inproc-fail", "--event-budget", "40", "--out", Path.Combine(dir, "c")]));
            Assert.Equal(2, Program.Main(["run", "--seeds", "3", "--tuning", Path.Combine(dir, "missing.json")]));
            var broken = Path.Combine(dir, "broken.json");
            File.WriteAllText(broken, "{ not json");
            Assert.Equal(2, Program.Main(["run", "--seeds", "3", "--tuning", broken]));        // a bad tuning file is bad input, read by the same strict reader

            // the same codes from real processes
            Assert.Equal(0, Spawn("run", "--seeds", "3", "--run-id", "p-ok", "--out", Path.Combine(dir, "pa")).ExitCode);
            Assert.Equal(3, Spawn("run", "--seeds", "3", "--run-id", "p-bad", "--tuning", tuning, "--out", Path.Combine(dir, "pb")).ExitCode);
            Assert.Equal(2, Spawn("run", "--seeds", "3", "--tuning", broken).ExitCode);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Insufficient_sample_and_not_evaluated_are_results_of_a_working_computation_not_errors()
    {
        var run = SimRunner.Run(H.Config(), [3]);
        Assert.True(run.AllCompleted && run.Metrics.IsSuccess);
        Assert.Equal(0, Program.ExitCodeOf(run));
        Assert.All(run.Metrics.Value.Metrics, m => Assert.Equal(MetricStatus.NotEvaluated, m.Status));

        // every denominator zero: INSUFFICIENT_SAMPLE for all eight, and still a successful computation
        var empty = run with { Metrics = MetricsEvaluator.Evaluate(new MetricCounters(), run.Config.Cohort, run.Config.Tuning, EvaluationPolicy.Unapproved) };
        Assert.All(empty.Metrics.Value.Metrics, m => Assert.Equal(MetricStatus.InsufficientSample, m.Status));
        Assert.Equal(0, Program.ExitCodeOf(empty));
        Assert.Equal("COMPLETED", RunReport.RunStatus(empty));

        // an official FAIL verdict is a gate result, not a program error (an explicit test policy, proving the logic and no threshold)
        var failing = run with { Metrics = MetricsEvaluator.Evaluate(run.Aggregate, run.Config.Cohort, run.Config.Tuning, new EvaluationPolicy(new Dictionary<string, long> { ["pitch_zone_rate"] = 1 }, new HashSet<string> { run.Config.Cohort.CohortId })) };
        Assert.Equal(0, Program.ExitCodeOf(failing));
    }

    [Fact]
    public void A_metrics_error_is_distinct_from_not_evaluated_in_the_library_result_too()
    {
        var run = SimRunner.Run(H.Config(), [3]);
        var tuning = H.Ok(HarnessData.ParseTuning(JsonNode.Parse(HarnessData.ReadResource(HarnessData.TuningResource))!.AsObject().Also(n =>
            n["targets"]!.AsArray().Select(t => t!.AsObject()).Single(t => t["id"]!.GetValue<string>() == "babip")["id"] = "babip_renamed_for_test").ToJsonString()));
        var broken = SimRunner.Run(H.Ok(RunConfig.Create("t-broken", "smoke", NeutralCohort.Create(), H.Rules(), tuning, H.Park())), [3]);

        Assert.True(broken.AllCompleted);                                                  // the games are fine ...
        Assert.False(broken.Metrics.IsSuccess);                                            // ... the metrics are not
        Assert.Equal("Metrics.target_missing", broken.Metrics.Error!.Value.Code);
        Assert.Equal(3, Program.ExitCodeOf(broken));
        Assert.Equal("METRICS_ERROR", RunReport.RunStatus(broken));
        Assert.Equal(0, Program.ExitCodeOf(run));
    }
}

internal static class Ext
{
    public static T Also<T>(this T value, Action<T> act)
    {
        act(value);
        return value;
    }
}
