using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StarPlayer.Simulation.Baseball;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>F-B: the manifest, the summary, the deterministic hashes and the replay of a manifest (a successful run and a failed one).</summary>
public class ReportAndReplayTests
{
    private const string GoldenTuningHashV4 = "5011f8acaae4848481db6bc60f636d7594b2a30fcdf594aeab375f0aa98b8fd8";

    private static JsonElement Parse(object value) => JsonDocument.Parse(JsonSerializer.Serialize(value, RunReport.Json)).RootElement;

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "simharness-tests", Guid.NewGuid().ToString("N"));

    // ---------------------------------------------------------------- pinned inputs

    [Fact]
    public void The_harness_reads_the_same_tuning_and_rules_as_the_production_readers()
    {
        Assert.Equal(GoldenTuningHashV4, H.Tuning.ContentHash);                                  // the same literal TuningV4Tests pins on the Infrastructure reader
        Assert.Equal("2d3f7557750c65d920ee79ec0ff57121ae8b8990f13368ae4daa7520492dce2d", H.Rules().ContentHash);          // pinned in SimHarnessInputPinTests on the Infrastructure reader
        Assert.Equal("7ad9776729b4a71d60af352d620df561d02f699e036fd6f4a4870b82635f4fa7", H.Rules(true).ContentHash);
        Assert.Equal(("prob-model-v4", "tuning-virtual-proto-v4"), (H.Tuning.Model.Version.Value, H.Tuning.Tuning.Value));
        Assert.Equal(StarPlayer.Domain.Baseball.Game.CalledPitchMode.NonAutomated, H.Rules().CalledPitchMode);
        Assert.Equal(11, H.Rules().ExtraInningsLimit);
        Assert.True(H.Rules().TieAllowed);
    }

    [Fact]
    public void The_embedded_files_are_the_content_files_unchanged()
    {
        Assert.Equal(File.ReadAllText(Path.Combine(H.Root, "content", "tuning", "virtual_tuning_v4.json")), HarnessData.ReadResource(HarnessData.TuningResource));
        Assert.Equal(File.ReadAllText(Path.Combine(H.Root, "content", "rules", "game_rules_profiles_v2.json")), HarnessData.ReadResource(HarnessData.RulesResource));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(HarnessData.ReadResource(HarnessData.TuningResource)))).ToLowerInvariant(), HarnessData.ResourceHash(HarnessData.TuningResource));
        Assert.Equal("Harness.rules_profile_unknown", HarnessData.LoadRules("nope").Error!.Value.Code);
    }

    [Fact]
    public void A_malformed_data_file_is_refused_with_a_typed_error()
    {
        Assert.Equal("Harness.tuning_json_invalid", HarnessData.ParseTuning("{ not json").Error!.Value.Code);
        Assert.Equal("Harness.tuning_json_invalid", HarnessData.ParseTuning(HarnessData.ReadResource(HarnessData.TuningResource).Replace("\"denominator\"", "\"extra_member\": 1, \"denominator\"", StringComparison.Ordinal)).Error!.Value.Code);
        Assert.Equal("Harness.rules_shape_invalid", HarnessData.ParseRules(HarnessData.ReadResource(HarnessData.RulesResource).Replace("\"format_version\": 2", "\"format_version\": 1", StringComparison.Ordinal)).Error!.Value.Code);
    }

    // ---------------------------------------------------------------- manifest

    [Fact]
    public void The_manifest_records_everything_that_pins_the_run()
    {
        var config = H.Config("manifest-run");
        var run = SimRunner.Run(config, [3, 11]);
        var m = Parse(RunReport.Manifest(run, null));

        Assert.Equal("simharness-manifest-v1", m.GetProperty("manifest_version").GetString());
        var r = m.GetProperty("run");
        Assert.Equal(("manifest-run", "smoke", "neutral-v1", "Neutral", "simharness-input-v1"),
            (r.GetProperty("run_id").GetString(), r.GetProperty("mode").GetString(), r.GetProperty("cohort_id").GetString(), r.GetProperty("cohort_kind").GetString(), r.GetProperty("input_schema_version").GetString()));
        Assert.Equal(run.ConfigHash, r.GetProperty("config_hash").GetString());

        var e = m.GetProperty("engine");
        Assert.Equal(("prob-model-v4", "tuning-virtual-proto-v4", GoldenTuningHashV4), (e.GetProperty("probability_model_version").GetString(), e.GetProperty("tuning_version").GetString(), e.GetProperty("tuning_hash").GetString()));
        Assert.Equal(("test-pzl-non-automated", "test-v2", "FictionalTest"), (e.GetProperty("rules_profile_id").GetString(), e.GetProperty("rules_version").GetString(), e.GetProperty("rules_status").GetString()));
        Assert.Equal(H.Rules().ContentHash, e.GetProperty("rules_hash").GetString());
        Assert.Equal("advancement-policy-v2", e.GetProperty("advancement_policy_version").GetString());
        Assert.Equal(("stadium-sim-neutral", "pf-sim-neutral", 1, 1000), (e.GetProperty("park").GetProperty("stadium").GetString(), e.GetProperty("park").GetProperty("profile").GetString(),
            e.GetProperty("park").GetProperty("version").GetInt32(), e.GetProperty("park").GetProperty("home_runs_permille").GetInt32()));
        Assert.Equal(("player-input-set-v3", config.Cohort.InputSet.Id, config.Cohort.InputsHash), (e.GetProperty("player_input_set_content_version").GetString(), e.GetProperty("player_input_set_id").GetString(), e.GetProperty("cohort_inputs_hash").GetString()));
        Assert.Equal(("normal-balanced-v1", 3000), (e.GetProperty("policy_id").GetString(), e.GetProperty("event_budget").GetInt32()));

        var games = m.GetProperty("games").EnumerateArray().ToList();
        Assert.Equal(new[] { 3UL, 11UL }, games.Select(g => g.GetProperty("seed").GetUInt64()).ToArray());
        Assert.Equal(new[] { "game:sim:neutral-v1-000001", "game:sim:neutral-v1-000002" }, games.Select(g => g.GetProperty("game_id").GetString()).ToArray());

        var res = m.GetProperty("results");
        Assert.Equal((2, 2, 0, 0, 0), (res.GetProperty("games_requested").GetInt32(), res.GetProperty("games_completed").GetInt32(), res.GetProperty("games_failed").GetInt32(),
            res.GetProperty("games_cancelled").GetInt32(), res.GetProperty("games_not_started").GetInt32()));
        Assert.Equal(run.Aggregate.PlateAppearances, res.GetProperty("plate_appearances").GetInt64());
        Assert.Equal(run.Aggregate.Pitches, res.GetProperty("pitches").GetInt64());
        Assert.Equal(run.RunResultHash, res.GetProperty("run_result_hash").GetString());
        Assert.Equal(run.Games.Select(g => g.RngHash).ToArray(), res.GetProperty("games").EnumerateArray().Select(g => g.GetProperty("rng_hash").GetString()).ToArray());
        Assert.Equal(run.Games.Select(g => g.EventHash).ToArray(), res.GetProperty("games").EnumerateArray().Select(g => g.GetProperty("event_hash").GetString()).ToArray());
        Assert.Empty(res.GetProperty("error_codes").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, m.GetProperty("environment_not_hashed").ValueKind);
    }

    [Fact]
    public void Time_and_machine_facts_are_recorded_but_belong_to_no_hash()
    {
        var config = H.Config();
        var a = SimRunner.Run(config, [3]);
        var b = SimRunner.Run(config, [3]);
        var early = EnvironmentInfo.Capture(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 1, 0, 0, 5, TimeSpan.Zero), "aaaa", true);
        var late = EnvironmentInfo.Capture(new DateTimeOffset(2030, 6, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2030, 6, 1, 1, 0, 0, TimeSpan.Zero), "bbbb", false);

        Assert.Equal(a.RunResultHash, b.RunResultHash);
        Assert.Equal(a.ConfigHash, b.ConfigHash);
        var ma = Parse(RunReport.Manifest(a, early));
        var mb = Parse(RunReport.Manifest(b, late));
        Assert.NotEqual(ma.GetProperty("environment_not_hashed").GetProperty("started_utc").GetString(), mb.GetProperty("environment_not_hashed").GetProperty("started_utc").GetString());
        Assert.Equal("aaaa", ma.GetProperty("environment_not_hashed").GetProperty("git_commit").GetString());
        Assert.Equal(ma.GetProperty("results").GetProperty("run_result_hash").GetString(), mb.GetProperty("results").GetProperty("run_result_hash").GetString());
        Assert.Equal(5000d, ma.GetProperty("environment_not_hashed").GetProperty("elapsed_milliseconds").GetDouble());
    }

    [Fact]
    public void Anything_that_can_change_a_result_changes_the_config_or_the_result_hash()
    {
        var specs = new[] { 3UL };
        var baseline = SimRunner.Run(H.Config(), specs);
        string ConfigOf(RunConfig c) => SimRunner.Run(c, specs).ConfigHash;

        Assert.Equal(baseline.ConfigHash, ConfigOf(H.Config("another-run-id")));                         // the run id is a label
        Assert.NotEqual(baseline.ConfigHash, ConfigOf(H.Config(rules: H.Rules(true))));                   // the rules
        Assert.NotEqual(baseline.ConfigHash, ConfigOf(H.Config(advancement: AdvancementPolicy.Version)));  // the advancement policy
        Assert.NotEqual(baseline.ConfigHash, ConfigOf(H.Config(budget: 2999)));                           // the budget
        var park = RunConfig.Create("p", "smoke", NeutralCohort.Create(), H.Rules(), H.Tuning, H.Park(homeRuns: 1100));
        Assert.NotEqual(baseline.ConfigHash, ConfigOf(H.Ok(park)));                                       // the park
        var variant = H.Variant("neutral-v1", s => s.Id.Value == "sim-a-b1" ? H.WithRatings(s, new StarPlayer.Domain.Players.HitterRatings(H.Skill(50), H.Skill(50), H.Skill(50), H.Skill(52), H.Skill(50), H.Skill(50)), null, null) : null, kind: CohortKind.Neutral);
        Assert.NotEqual(baseline.ConfigHash, ConfigOf(H.Config(cohort: variant)));                        // a rating (same cohort id, other input hash)
        Assert.NotEqual(baseline.RunResultHash, SimRunner.Run(H.Config(), [4]).RunResultHash);            // the seed
        Assert.NotEqual(baseline.ConfigHash, SimRunner.Run(H.Config(), [3, 4]).ConfigHash);               // the game list
    }

    [Fact]
    public void The_cohort_input_hash_covers_ratings_the_engines_input_set_identity_does_not()
    {
        var baseline = NeutralCohort.Create();
        // contact is read by the pitch resolver but is NOT part of the player input set (fielding, speed, arm, framing, game calling, control)
        var changed = H.Variant("neutral-v1", s => s.Id.Value == "sim-a-b1" ? H.WithRatings(s, new StarPlayer.Domain.Players.HitterRatings(H.Skill(70), H.Skill(50), H.Skill(50), H.Skill(50), H.Skill(50), H.Skill(50)), null, null) : null, kind: CohortKind.Neutral);

        Assert.Equal(baseline.InputSet.Id, changed.InputSet.Id);
        Assert.NotEqual(baseline.InputsHash, changed.InputsHash);
        Assert.Equal(NeutralCohort.Create().InputsHash, baseline.InputsHash);                              // construction is deterministic
        Assert.Equal(baseline.Kind, changed.Kind);                                                          // only the rating differs
    }

    // ---------------------------------------------------------------- summary and text

    [Fact]
    public void The_summary_lists_every_game_every_metric_and_both_derivations_of_the_statistics()
    {
        var run = SimRunner.Run(H.Config(), [3, 11, 29]);
        var s = Parse(RunReport.Summary(run));

        Assert.Equal("NOT_EVALUATED", s.GetProperty("official_verdict").GetString());
        var games = s.GetProperty("games").EnumerateArray().ToList();
        Assert.Equal(3, games.Count);
        Assert.All(games, g =>
        {
            Assert.Equal("Completed", g.GetProperty("status").GetString());
            Assert.Equal(64, g.GetProperty("event_hash").GetString()!.Length);
            Assert.Equal(64, g.GetProperty("rng_hash").GetString()!.Length);
            Assert.Equal(JsonValueKind.Null, g.GetProperty("error_code").ValueKind);
        });

        var metrics = s.GetProperty("metrics");
        Assert.Equal("neutral-v1", metrics.GetProperty("cohort_id").GetString());
        var items = metrics.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8" }, items.Select(i => i.GetProperty("f_id").GetString()).ToArray());
        Assert.All(items, i =>
        {
            foreach (var name in new[] { "metric_id", "numerator", "denominator", "numerator_count", "denominator_count", "measured_ppm", "target_low_ppm", "target_high_ppm", "deviation_ppm", "recommended_cohort",
                         "evaluated_cohort", "sample_rule", "status", "wilson_95_diagnostic_ppm" })
                Assert.True(i.TryGetProperty(name, out _), name);
            Assert.Equal("NotEvaluated", i.GetProperty("status").GetString());
            Assert.Equal("unapproved", i.GetProperty("sample_rule").GetString());
        });

        var cross = s.GetProperty("cross_check").EnumerateArray().ToList();
        Assert.Equal(3, cross.Count);
        Assert.All(cross, c => Assert.True(c.GetProperty("passed").GetBoolean()));
        Assert.Empty(s.GetProperty("failures").EnumerateArray());

        var text = RunReport.Text(run);
        foreach (var f in new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8" })
            Assert.Contains(f + " ", text, StringComparison.Ordinal);
        Assert.Contains("all completed games match", text, StringComparison.Ordinal);
        Assert.Contains("NOT evaluated", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Aggregate_counters_are_the_sum_of_the_completed_games()
    {
        var run = SimRunner.Run(H.Config(), [3, 11, 29]);
        Assert.Equal(run.Games.Sum(g => g.Counters.PlateAppearances), run.Aggregate.PlateAppearances);
        Assert.Equal(run.Games.Sum(g => g.Counters.Pitches), run.Aggregate.Pitches);
        Assert.Equal(run.Games.Sum(g => g.Counters.Hits), run.Aggregate.Hits);
        Assert.Equal(run.Games.Sum(g => (long)g.CommittedEvents), run.Aggregate.Events);
        Assert.True(run.AllCompleted);
        Assert.Equal(new[] { 1, 2, 3 }, run.Games.Select(g => g.Game.GameIndex).ToArray());
    }

    // ---------------------------------------------------------------- failure runs

    [Fact]
    public void A_failed_game_is_kept_with_its_game_id_and_seed_excluded_from_the_aggregate_and_reported_everywhere()
    {
        var run = SimRunner.Run(H.Config("failing", budget: 30), [29, 3]);

        Assert.Equal((0, 2, 2), (run.CompletedCount, run.FailedCount, run.Games.Count));
        Assert.False(run.AllCompleted);
        Assert.Equal(0, run.Aggregate.PlateAppearances);                                        // nothing from an unfinished game is averaged in
        Assert.All(run.Metrics.Value.Metrics, m => Assert.Equal(MetricStatus.InsufficientSample, m.Status));
        Assert.All(run.Games, g => Assert.True(g.Counters.Events == 30 && g.Failure!.LastReceipts.Count == 12));

        var manifest = Parse(RunReport.Manifest(run, null));
        Assert.Equal(new[] { "Harness.event_budget_exceeded" }, manifest.GetProperty("results").GetProperty("error_codes").EnumerateArray().Select(c => c.GetString()).ToArray());
        Assert.Equal(2, manifest.GetProperty("results").GetProperty("games_failed").GetInt32());

        var summary = Parse(RunReport.Summary(run));
        var failures = summary.GetProperty("failures").EnumerateArray().ToList();
        Assert.Equal(new[] { 29UL, 3UL }, failures.Select(f => f.GetProperty("seed").GetUInt64()).ToArray());
        Assert.Equal(new[] { "game:sim:neutral-v1-000001", "game:sim:neutral-v1-000002" }, failures.Select(f => f.GetProperty("game_id").GetString()).ToArray());
        Assert.All(failures, f =>
        {
            Assert.Equal(12, f.GetProperty("last_receipts").GetArrayLength());
            Assert.Equal(30, f.GetProperty("event_sequence").GetInt32());
            Assert.Equal(64, f.GetProperty("rng_hash").GetString()!.Length);
        });
        var text = RunReport.Text(run);
        Assert.Contains("FAILURE Harness.event_budget_exceeded", text, StringComparison.Ordinal);
        Assert.Contains("seed 29", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_that_is_cancelled_stops_at_the_cancelled_game_and_counts_the_games_it_never_started()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var run = SimRunner.Run(H.Config(), [3, 11, 29], null, source.Token);

        Assert.Equal((1, 1, 2, 0), (run.Games.Count, run.CancelledCount, run.NotStartedCount, run.CompletedCount));
        Assert.Equal(3, run.Requested);
        Assert.False(run.AllCompleted);
    }

    // ---------------------------------------------------------------- files and the command line

    [Fact]
    public void The_report_is_written_as_three_files_and_the_command_line_replays_it_to_the_same_hash()
    {
        var dir = TempDir();
        try
        {
            Assert.Equal(0, Program.Main(["run", "--seeds", "3,11", "--run-id", "cli-run", "--out", dir]));
            Assert.Equal(new[] { "manifest.json", "summary.json", "summary.txt" }, Directory.GetFiles(dir).Select(Path.GetFileName).Order().ToArray());
            var manifest = Path.Combine(dir, "manifest.json");
            Assert.Equal(0, Program.Main(["replay", "--manifest", manifest]));

            var env = Parse(JsonDocument.Parse(File.ReadAllText(manifest)).RootElement.GetProperty("environment_not_hashed"));
            Assert.Equal(64, JsonDocument.Parse(File.ReadAllText(manifest)).RootElement.GetProperty("results").GetProperty("run_result_hash").GetString()!.Length);
            Assert.Equal(JsonValueKind.Object, env.ValueKind);

            // a manifest whose recorded result hash was edited no longer replays to a match
            var edited = File.ReadAllText(manifest);
            var hash = JsonDocument.Parse(edited).RootElement.GetProperty("results").GetProperty("run_result_hash").GetString()!;
            var tampered = Path.Combine(dir, "tampered.json");
            File.WriteAllText(tampered, edited.Replace(hash, new string('0', 64), StringComparison.Ordinal));
            Assert.Equal(1, Program.Main(["replay", "--manifest", tampered]));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void A_failed_run_is_reproduced_from_its_manifest_with_the_same_failure()
    {
        var dir = TempDir();
        try
        {
            Assert.Equal(1, Program.Main(["run", "--seeds", "29,3", "--run-id", "cli-fail", "--event-budget", "40", "--out", dir]));          // exit code 1: a game failed
            Assert.Equal(0, Program.Main(["replay", "--manifest", Path.Combine(dir, "manifest.json")]));                                // the same failure, hash for hash
            var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "summary.json"))).RootElement;
            Assert.Equal(2, summary.GetProperty("failures").GetArrayLength());
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void The_command_line_refuses_large_runs_unknown_commands_and_other_cohorts()
    {
        Assert.Equal(2, Program.Main(["run", "--seeds", string.Join(',', Enumerable.Range(1, Program.SmokeMaxGames + 1))]));    // 100 games / 100,000 PA are not approved
        Assert.Equal(2, Program.Main(["bogus"]));
        Assert.Equal(2, Program.Main([]));
        Assert.Equal(2, Program.Main(["run", "--rules"]));
        Assert.Equal(2, Program.Main(["replay"]));
        Assert.Equal(2, Program.Main(["run", "--seeds", "abc"]));                                                         // bad numbers and unreadable files are bad input, not a crash
        Assert.Equal(2, Program.Main(["run", "--seeds", "3", "--event-budget", "many"]));
        Assert.Equal(2, Program.Main(["replay", "--manifest", Path.Combine(Path.GetTempPath(), "simharness-tests", "does-not-exist.json")]));
        Assert.Equal(10, Program.SmokeMaxGames);
    }
}
