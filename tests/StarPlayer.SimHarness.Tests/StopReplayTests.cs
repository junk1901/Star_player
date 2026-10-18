using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// F-C2-B B-1: ADR-009 N-04 (SR-D1 to SR-D9). The stop rule against synthetic PA sequences, the outcome table, the <c>run-result-v2</c> golden vector, the official <c>fc3-cal</c> replay derivation
/// and its refusals (synthetic games: a 100,000-PA run is not a unit test), and a small <c>fc2-dry</c> run and replay on the real engine. Every seed file is a TEMP fixture; nothing official is pinned.
/// </summary>
public sealed class StopReplayTests : IDisposable
{
    private const string CalKey = "fc3-cal-na";
    private const string FixtureSha = "0123456789abcdef0123456789abcdef01234567";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "simharness-tests", "stop-" + Guid.NewGuid().ToString("N"));

    public StopReplayTests() => Directory.CreateDirectory(Path.Combine(_root, "seeds"));

    public void Dispose() => Directory.Delete(_root, true);

    private static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static RunConfig CalConfig(string key = CalKey, string mode = "official") =>
        H.Ok(RunConfig.Create("stop-test", mode, CalibrationCohorts.NeutralMixed(), H.Rules(true), H.Tuning, H.Park(), "advancement-policy-v2", 3000, competitionKey: key));

    private static StopPlan Plan(RunConfig config, int games, int block, long target, string family = "fc3-cal") =>
        new(family, block, target, Enumerable.Range(1, games).Select(i => config.Spec(i, 990000UL + (ulong)i)).ToList(), "seeds/x.json", new string('a', 64));

    /// <summary>A synthetic game result (no engine): <paramref name="pa"/> plate appearances, a deterministic result hash.</summary>
    internal static GameRunResult Fake(GameSpec spec, long pa, GameRunStatus status = GameRunStatus.Completed, string stage = "advance")
    {
        var failure = status == GameRunStatus.Completed ? null : new RunFailure(status == GameRunStatus.Cancelled ? "Harness.cancelled" : "Harness.synthetic_failure", "synthetic", stage, 0, 0, "-", []);
        var done = status == GameRunStatus.Completed;
        return new GameRunResult(spec, status, failure, done ? GameOutcome.HomeWin : null, done ? GameEndKind.Regulation : GameEndKind.None, 0, 1, 9, InningHalf.Bottom, 1,
            new MetricCounters { PlateAppearances = pa }, null, "e", "r", Hashing.Sha256($"fake|{spec.GameId}|{spec.Seed}|{status}|{pa}"), 0);
    }

    private sealed class Counting(Func<GameSpec, GameRunResult> play)
    {
        public List<int> Played { get; } = [];

        public GameRunResult Play(GameSpec spec)
        {
            Played.Add(spec.GameIndex);
            return play(spec);
        }
    }

    // ---------------------------------------------------------------- SR-D1: the rule against synthetic PA sequences

    [Fact]
    public void The_run_ends_at_the_first_block_boundary_whose_valid_pa_reaches_the_target_inclusive()
    {
        var config = CalConfig();
        var play = new Counting(s => Fake(s, 20_000));
        var o = StopRule.Execute(Plan(config, 21, 7, 100_000), play.Play);
        Assert.Equal((RunTermination.Rule, new StopRecord(7, StopReason.TargetReached, 140_000, 7)), (o.Termination, o.Stop));
        Assert.Equal(Enumerable.Range(1, 7), play.Played);                                            // seeds after the stop are never played (SR-D1.3)

        var exact = StopRule.Execute(Plan(config, 15, 5, 100_000), s => Fake(s, 20_000));            // exactly the target at the first boundary: inclusive
        Assert.Equal(new StopRecord(5, StopReason.TargetReached, 100_000, 5), exact.Stop);
        var short1 = StopRule.Execute(Plan(config, 15, 5, 100_000), s => Fake(s, s.GameIndex == 5 ? 19_999 : 20_000));   // one PA short: the next block runs
        Assert.Equal(new StopRecord(10, StopReason.TargetReached, 199_999, 5), short1.Stop);
    }

    [Fact]
    public void A_target_reached_mid_block_completes_the_block_in_progress()
    {
        var play = new Counting(s => Fake(s, s.GameIndex == 1 ? 100_000 : 1));
        var o = StopRule.Execute(Plan(CalConfig(), 14, 7, 100_000), play.Play);
        Assert.Equal(new StopRecord(7, StopReason.TargetReached, 100_006, 7), o.Stop);
        Assert.Equal(7, play.Played.Count);
    }

    [Fact]
    public void Exhaustion_is_reported_only_when_the_target_was_never_reached()
    {
        var config = CalConfig();
        Assert.Equal(new StopRecord(14, StopReason.SeedsExhausted, 14, 7), StopRule.Execute(Plan(config, 14, 7, 100_000), s => Fake(s, 1)).Stop);
        // the target is tested before exhaustion: reaching it exactly at the last boundary is TARGET_REACHED
        Assert.Equal(new StopRecord(14, StopReason.TargetReached, 100_002, 7),
            StopRule.Execute(Plan(config, 14, 7, 100_000), s => Fake(s, s.GameIndex == 14 ? 100_000 - 13 + 2 : 1)).Stop);
        Assert.Equal(BlockDecision.StopTargetReached, StopRule.Decide(7, 100_000, 14, 14, 100_000));
        Assert.Equal(BlockDecision.StopSeedsExhausted, StopRule.Decide(7, 100_000, 14, 14, 99_999));
    }

    [Fact]
    public void A_failed_game_counts_as_played_its_pa_is_not_valid_and_it_is_never_replaced()
    {
        var play = new Counting(s => Fake(s, 60, s.GameIndex == 2 ? GameRunStatus.Failed : GameRunStatus.Completed));
        var o = StopRule.Execute(Plan(CalConfig(), 6, 2, 100), play.Play);
        // block 1: n = 2 but only 60 valid PA (the failed game's 60 are not counted); block 2 reaches 180
        Assert.Equal(new StopRecord(4, StopReason.TargetReached, 180, 2), o.Stop);
        Assert.Equal([1, 2, 3, 4], play.Played);
        Assert.Equal(GameRunStatus.Failed, o.Games[1].Status);
        Assert.Equal(990002UL, o.Games[1].Game.Seed);                                                // the failed seed stays in the result; the next seed is not substituted
    }

    [Fact]
    public void Cancellation_and_broken_integrity_end_the_run_without_a_stop_record()
    {
        var config = CalConfig();
        var cancelled = new Counting(s => Fake(s, 20_000, s.GameIndex == 3 ? GameRunStatus.Cancelled : GameRunStatus.Completed));
        var c = StopRule.Execute(Plan(config, 14, 7, 100_000), cancelled.Play);
        Assert.Equal((RunTermination.Cancelled, (StopRecord?)null, 3), (c.Termination, c.Stop, c.Games.Count));
        Assert.Equal([1, 2, 3], cancelled.Played);

        var checks = 0;
        var aborted = StopRule.Execute(Plan(config, 21, 7, 1_000_000), s => Fake(s, 1), () => ++checks == 2 ? new Error("Seed.file_hash_mismatch", "pin broke mid-run") : null);
        Assert.Equal((RunTermination.AbortedIntegrity, (StopRecord?)null, 7, "Seed.file_hash_mismatch"), (aborted.Termination, aborted.Stop, aborted.Games.Count, aborted.Abort!.Value.Code));

        // a game that failed the harness' own pin check of the run record aborts too (a failure in any other stage does not)
        var pin = StopRule.Execute(Plan(config, 14, 7, 100_000), s => Fake(s, 1, s.GameIndex == 4 ? GameRunStatus.Failed : GameRunStatus.Completed, s.GameIndex == 4 ? "config" : "advance"));
        Assert.Equal((RunTermination.AbortedIntegrity, (StopRecord?)null, 4), (pin.Termination, pin.Stop, pin.Games.Count));
    }

    [Fact]
    public void The_seed_manifest_rule_and_the_executor_rule_are_one_function()
    {
        var bytes = Encoding.UTF8.GetBytes(SeedManifestTests.Manifest("fc3-cal", "fc3-cal-l1", games: 21, block: 7, max: 21, paTarget: 100_000).ToJsonString());
        File.WriteAllBytes(Path.Combine(_root, "seeds", "r.json"), bytes);
        var m = H.Ok(SeedManifestReader.Read(_root, "seeds/r.json", Sha(bytes)));
        for (var n = 0; n <= 21; n++)
            foreach (var pa in new long[] { 0, 99_999, 100_000, 150_000 })
                Assert.Equal(StopRule.Decide(7, 100_000, 21, n, pa), m.AfterGames(n, pa));
        Assert.Throws<ArgumentException>(() => StopRule.Execute(Plan(CalConfig(), 10, 7, 100_000), s => Fake(s, 1)));       // an incomplete block is never a plan
    }

    // ---------------------------------------------------------------- SR-D9: technical outcome versus the gate

    [Fact]
    public void The_outcome_table_keeps_exit_codes_separate_and_never_reports_a_cancel_or_abort_as_a_stop()
    {
        var config = CalConfig();
        RunResult Run(Func<GameSpec, GameRunResult> play, RunConfig? c = null, Func<Error?>? integrity = null) =>
            SimRunner.RunStopAware(c ?? config, Plan(c ?? config, 14, 7, 100_000), integrity: integrity, play: play);

        var reached = Run(s => Fake(s, 20_000));
        Assert.Equal((0, "COMPLETED", true, RunResultText.V2, 0, 7), (Program.ExitCodeOf(reached), RunReport.RunStatus(reached), reached.AllCompleted, reached.ResultVersion, reached.NotStartedCount, reached.UnexecutedAfterStop));
        Assert.Equal(14, reached.Requested);                                                          // the plan, not the prefix
        Assert.Equal(config.ConfigHash(reached.Plan!.Games), reached.ConfigHash);                     // the unchanged whole-plan ConfigHash (SR-D1.1)

        var exhausted = Run(s => Fake(s, 1));
        Assert.Equal((0, "COMPLETED", StopReason.SeedsExhausted), (Program.ExitCodeOf(exhausted), RunReport.RunStatus(exhausted), exhausted.Stop!.Reason));

        var failed = Run(s => Fake(s, 20_000, s.GameIndex == 2 ? GameRunStatus.Failed : GameRunStatus.Completed));
        Assert.Equal((1, "GAMES_FAILED", false, RunResultText.V2), (Program.ExitCodeOf(failed), RunReport.RunStatus(failed), failed.AllCompleted, failed.ResultVersion));   // still terminated by the rule

        var cancelled = Run(s => Fake(s, 20_000, s.GameIndex == 2 ? GameRunStatus.Cancelled : GameRunStatus.Completed));
        Assert.Equal((1, "CANCELLED", false, (string?)null, (StopRecord?)null), (Program.ExitCodeOf(cancelled), RunReport.RunStatus(cancelled), cancelled.AllCompleted, cancelled.ResultVersion, cancelled.Stop));

        var aborted = Run(s => Fake(s, 20_000), integrity: () => new Error("Official.git_dirty", "x"));
        Assert.Equal((2, "ABORTED_INTEGRITY", (string?)null), (Program.ExitCodeOf(aborted), RunReport.RunStatus(aborted), aborted.ResultVersion));

        var noTarget = H.Ok(HarnessData.ParseTuning(HarnessData.ReadResource(HarnessData.TuningResource).Replace("\"id\": \"babip\"", "\"id\": \"babip_renamed\"", StringComparison.Ordinal)));
        var broken = H.Ok(RunConfig.Create("stop-test", "official", CalibrationCohorts.NeutralMixed(), H.Rules(true), noTarget, H.Park(), "advancement-policy-v2", 3000, competitionKey: CalKey));
        var metrics = Run(s => Fake(s, 20_000), broken);
        Assert.Equal((3, "METRICS_ERROR"), (Program.ExitCodeOf(metrics), RunReport.RunStatus(metrics)));

        // the manifest of a cancelled run has no stop record, no version and no result hash: it is not a result
        var m = JsonNode.Parse(JsonSerializer.Serialize(RunReport.Manifest(cancelled, null), RunReport.Json))!["results"]!.AsObject();
        Assert.Equal(("CANCELLED", (JsonNode?)null, (JsonNode?)null, false), (m["run_status"]!.GetValue<string>(), m["run_result_version"], m["run_result_hash"], m.ContainsKey("stop")));
    }

    // ---------------------------------------------------------------- SR-D7.3: run-result-v2 golden vector

    [Fact]
    public void Run_result_v2_has_a_fixed_byte_layout_and_golden_hash()
    {
        var text = RunResultText.V2Text(new string('a', 64), 14, 100_000, new StopRecord(7, StopReason.TargetReached, 140_000, 7), ["g1", "g2", "g3", "g4", "g5", "g6", "g7"], "AGG");
        Assert.Equal("run-result-v2\n" + new string('a', 64) + "\nplan|14|100000\nstop|7|TARGET_REACHED|140000|7\ng1\ng2\ng3\ng4\ng5\ng6\ng7\nAGG", text);
        Assert.Equal("6d3c35ec182e60e0201fa0d396a1329aa51aefca22caa84e7adbc338d4cd84f5", Hashing.Sha256(text));   // computed independently (Python hashlib) from the literal above
        var exhausted = RunResultText.V2Text("c", 2, 5, new StopRecord(2, StopReason.SeedsExhausted, 4, 1), ["x", "y"], "A");
        Assert.Equal("run-result-v2\nc\nplan|2|5\nstop|2|SEEDS_EXHAUSTED|4|1\nx\ny\nA", exhausted);
        // the v1 text is unchanged (legacy fixtures replay to their recorded hashes in other tests)
        Assert.Equal("run-result-v1\nc\nx\ny\nA", RunResultText.V1Text("c", ["x", "y"], "A"));
    }

    [Fact]
    public void The_v2_hash_binds_the_plan_every_stop_field_and_every_executed_game()
    {
        var stop = new StopRecord(7, StopReason.TargetReached, 140_000, 7);
        string[] games = ["g1", "g2", "g3", "g4", "g5", "g6", "g7"];
        var baseHash = Hashing.Sha256(RunResultText.V2Text("c", 14, 100_000, stop, games, "A"));
        var variants = new[]
        {
            RunResultText.V2Text("d", 14, 100_000, stop, games, "A"),
            RunResultText.V2Text("c", 21, 100_000, stop, games, "A"),
            RunResultText.V2Text("c", 14, 100_001, stop, games, "A"),
            RunResultText.V2Text("c", 14, 100_000, stop with { ExecutedGames = 14 }, games, "A"),
            RunResultText.V2Text("c", 14, 100_000, stop with { Reason = StopReason.SeedsExhausted }, games, "A"),
            RunResultText.V2Text("c", 14, 100_000, stop with { CumulativePa = 140_001 }, games, "A"),
            RunResultText.V2Text("c", 14, 100_000, stop with { BlockSize = 1 }, games, "A"),
            RunResultText.V2Text("c", 14, 100_000, stop, games.Reverse().ToArray(), "A"),
            RunResultText.V2Text("c", 14, 100_000, stop, games[..6], "A"),
            RunResultText.V2Text("c", 14, 100_000, stop, games, "B"),
        };
        Assert.All(variants, v => Assert.NotEqual(baseHash, Hashing.Sha256(v)));
        Assert.Equal(variants.Length, variants.Select(Hashing.Sha256).Distinct().Count());
    }

    // ---------------------------------------------------------------- SR-D4 / SR-D6: the reader

    [Fact]
    public void Official_files_must_declare_max_games_equal_to_the_plan_and_dry_targets_must_be_small()
    {
        string Code(JsonObject m)
        {
            var bytes = Encoding.UTF8.GetBytes(m.ToJsonString());
            File.WriteAllBytes(Path.Combine(_root, "seeds", "c.json"), bytes);
            var read = SeedManifestReader.Read(_root, "seeds/c.json", Sha(bytes));
            return read.IsSuccess ? "OK" : read.Error!.Value.Code;
        }

        foreach (var family in new[] { "fc3-cal", "fc3-dom" })
        {
            long? target = family == "fc3-cal" ? 100_000 : null;
            Assert.Equal("Seed.max_games_mismatch", Code(SeedManifestTests.Manifest(family, family + "-x", games: 7, block: 7, max: 14, paTarget: target)));
            Assert.Equal("OK", Code(SeedManifestTests.Manifest(family, family + "-x", games: 14, block: 7, max: 14, paTarget: target)));
        }

        Assert.Equal("OK", Code(SeedManifestTests.Manifest(games: 7, block: 7, max: 14)));                                       // fc2-dry keeps its slack (SR-D6)
        Assert.Equal("OK", Code(SeedManifestTests.Manifest(paTarget: 1)));
        Assert.Equal("OK", Code(SeedManifestTests.Manifest(paTarget: SeedManifestReader.DryMaxPa)));
        foreach (var bad in new long[] { 0, -1, SeedManifestReader.CalibrationMinPa, 10_000_000 })
            Assert.Equal("Seed.pa_target_invalid", Code(SeedManifestTests.Manifest(paTarget: bad)));
        Assert.Equal("Seed.pa_target_invalid", Code(SeedManifestTests.Manifest("fc3-int", "fc3-int-a", games: 100, block: 10, max: 100, paTarget: 50)));   // SR-D5: no target outside cal / dry
        Assert.Equal("Seed.pa_target_invalid", Code(SeedManifestTests.Manifest("fc3-cal", "fc3-cal-x", paTarget: 99_999)));                            // the official floor stays (SR-D4.2)
    }

    // ---------------------------------------------------------------- SR-D7 / SR-D8: the official fc3-cal replay (fixture approvals, synthetic games)

    private sealed record CalWorld(JsonObject Manifest, OfficialApprovals Approvals, RunConfig Config, StopPlan Plan, Func<GameSpec, GameRunResult> Play);

    /// <summary>A consistent official calibration world: a pinned TEMP fc3-cal seed file, the stop-aware result of synthetic games on it, and its manifest with a correct attestation.</summary>
    private CalWorld Cal(int games = 21, long paPerGame = 8_000, Func<GameSpec, GameRunStatus>? status = null)
    {
        var seed = SeedManifestTests.Manifest("fc3-cal", CalKey, games: games, block: 7, max: games, paTarget: 100_000, cohort: CalibrationCohorts.NeutralMixedId);
        seed["rules_profile_id"] = HarnessData.AutomatedProfile;
        var bytes = Encoding.UTF8.GetBytes(seed.ToJsonString());
        File.WriteAllBytes(Path.Combine(_root, "seeds", "cal.json"), bytes);
        var sha = Sha(bytes);
        var config = CalConfig();
        var plan = H.Ok(StopPlan.FromSeedManifest(H.Ok(SeedManifestReader.Read(_root, "seeds/cal.json", sha)), "seeds/cal.json"));
        GameRunResult Play(GameSpec s) => Fake(s, paPerGame, status?.Invoke(s) ?? GameRunStatus.Completed);
        var result = SimRunner.RunStopAware(config, plan, play: Play);
        var manifest = JsonNode.Parse(JsonSerializer.Serialize(RunReport.Manifest(result, null), RunReport.Json))!.AsObject();
        var cohort = config.Cohort;
        var att = new OfficialAttestation(OfficialAttestation.Version, FixtureSha, OfficialProfile.Id, OfficialProfile.Hash(), "seeds/cal.json", sha, "fc3-cal", CalKey, SeedManifestReader.GameIdRuleV1,
            cohort.CohortId, cohort.InputsHash, "normal-balanced-v1", "prob-model-v4", "tuning-virtual-proto-v4", OfficialProfile.TuningHash, HarnessData.AutomatedProfile,
            OfficialProfile.RulesHashes[HarnessData.AutomatedProfile], "advancement-policy-v2", OfficialProfile.ParkText, 3000, result.ConfigHash);
        manifest["official"] = JsonSerializer.SerializeToNode(new { schema_version = OfficialAttestation.EnvelopeVersion, attestation = att, attestation_hash = att.Hash() }, RunReport.Json);
        var approvals = new OfficialApprovals(FixtureSha, OfficialProfile.Id, OfficialProfile.Hash(), new Dictionary<string, string> { ["seeds/cal.json"] = sha },
            new Dictionary<string, string> { [cohort.CohortId] = cohort.InputsHash }, true);
        return new CalWorld(manifest, approvals, config, plan, Play);
    }

    private Result<ReplayPlan> Validate(CalWorld w, Action<JsonObject>? edit = null, OfficialApprovals? approvals = null, DryStopPin? pin = null)
    {
        var copy = JsonNode.Parse(w.Manifest.ToJsonString())!.AsObject();
        edit?.Invoke(copy);
        return ReplayCheck.Validate(Json(copy.ToJsonString()), approvals ?? w.Approvals, _root, pin);
    }

    private string Refused(CalWorld w, Action<JsonObject>? edit = null, OfficialApprovals? approvals = null, DryStopPin? pin = null)
    {
        var r = Validate(w, edit, approvals, pin);
        Assert.False(r.IsSuccess, "expected a refusal before any game");
        return r.Error!.Value.Code;
    }

    /// <summary>The post-execution half of a replay: validate, re-run the TRUSTED plan with the same executor, compare.</summary>
    private static Error? Derive(CalWorld w, ReplayPlan plan)
    {
        Assert.Null(plan.CheckAgainst(w.Config));
        var derived = SimRunner.RunStopAware(w.Config, plan.Stop!, play: w.Play);
        return StopReplay.Compare(plan.Recorded!, plan.RunResultHash, derived);
    }

    private static JsonObject Results(JsonObject m) => m["results"]!.AsObject();

    private static JsonObject Stop(JsonObject m) => Results(m)["stop"]!.AsObject();

    [Fact]
    public void A_consistent_stopped_calibration_replays_by_re_deriving_its_stop_from_the_pinned_file()
    {
        var w = Cal();
        var plan = H.Ok(Validate(w));
        Assert.Equal((7, 100_000L, 21, "fc3-cal"), (plan.Stop!.BlockSize, plan.Stop.PaTarget, plan.Stop.Games.Count, plan.Stop.Family));   // from the pinned file, not from results.stop
        Assert.Equal(21, plan.Games.Count);                                                           // the manifest keeps the whole plan
        Assert.Equal(new StopRecord(14, StopReason.TargetReached, 112_000, 7), plan.Recorded!.Stop);
        Assert.Null(Derive(w, plan));
        Assert.Equal(RunResultText.V2, Results(w.Manifest)["run_result_version"]!.GetValue<string>());
        Assert.Equal(7, Results(w.Manifest)["games_unexecuted_after_stop"]!.GetValue<int>());

        // never accepted with the real approvals, and the official family set is unchanged by the stop
        Assert.Equal("Official.seed_manifest_not_frozen", Refused(w, approvals: OfficialApprovals.Current));
        Assert.False(OfficialApprovals.Current.ExecutionPathReady);
        Assert.Empty(SeedManifestPins.Official);
    }

    [Fact]
    public void Earlier_later_and_inconsistent_stops_are_refused()
    {
        var w = Cal();   // derived: 14 games, 112,000 valid PA, TARGET_REACHED
        void Claim(JsonObject m, int executed, string reason, long pa)
        {
            var r = Results(m);
            var games = r["games"]!.AsArray();
            while (games.Count > executed)
                games.RemoveAt(games.Count - 1);
            for (var i = games.Count; i < executed; i++)
                games.Add(new JsonObject { ["game_id"] = i < w.Plan.Games.Count ? w.Plan.Games[i].GameId : $"game:{CalKey}:neutral-mixed-v1-{i + 1:D6}", ["seed"] = i < w.Plan.Games.Count ? w.Plan.Games[i].Seed : 1UL, ["status"] = "Completed", ["end_kind"] = "Regulation", ["event_hash"] = "e", ["rng_hash"] = "r", ["result_hash"] = "forged", ["error_code"] = null });
            r["stop"] = new JsonObject { ["executed_games"] = executed, ["stop_reason"] = reason, ["cumulative_pa"] = pa, ["block_size"] = 7 };
            r["plate_appearances"] = pa;
        }

        // an earlier stop with a self-consistent claim passes the precheck and is caught by the re-derivation
        var early = H.Ok(Validate(w, m => Claim(m, 7, "TARGET_REACHED", 100_000)));
        Assert.Equal("Replay.stop_too_early", Derive(w, early)!.Value.Code);
        var late = H.Ok(Validate(w, m => Claim(m, 21, "TARGET_REACHED", 168_000)));
        Assert.Equal("Replay.stop_too_late", Derive(w, late)!.Value.Code);
        Assert.Equal("Replay.stop_too_late", Refused(w, m => Claim(m, 28, "TARGET_REACHED", 224_000)));        // past the plan

        // claims the fixed rule itself contradicts are refused before any game
        Assert.Equal("Replay.stop_record_inconsistent", Refused(w, m => Claim(m, 7, "TARGET_REACHED", 56_000)));      // target "reached" below the target
        Assert.Equal("Replay.stop_record_inconsistent", Refused(w, m => Claim(m, 14, "SEEDS_EXHAUSTED", 99_000)));    // exhausted before the last seed
        Assert.Equal("Replay.stop_record_inconsistent", Refused(w, m => Claim(m, 21, "SEEDS_EXHAUSTED", 168_000)));   // exhausted although the target was reached
    }

    [Fact]
    public void Forged_stop_fields_pa_and_hashes_are_refused()
    {
        var w = Cal();
        Assert.Equal("Replay.stop_cumulative_pa_mismatch", Derive(w, H.Ok(Validate(w, m => { Stop(m)["cumulative_pa"] = 150_000; Results(m)["plate_appearances"] = 150_000; })))!.Value.Code);
        Assert.Equal("Replay.plate_appearances_mismatch", Refused(w, m => Results(m)["plate_appearances"] = 112_001));
        Assert.Equal("Replay.stop_block_size_mismatch", Refused(w, m => Stop(m)["block_size"] = 14));
        Assert.Equal("Replay.result_games_mismatch", Derive(w, H.Ok(Validate(w, m => Results(m)["games"]![3]!["result_hash"] = "0")))!.Value.Code);
        Assert.Equal("Replay.run_result_hash_mismatch", Derive(w, H.Ok(Validate(w, m => Results(m)["run_result_hash"] = new string('0', 64))))!.Value.Code);

        foreach (var malformed in new Action<JsonObject>[]
        {
            m => Stop(m)["executed_games"] = "14", m => Stop(m)["stop_reason"] = "STOPPED", m => Stop(m)["stop_reason"] = "target_reached", m => Stop(m)["executed_games"] = 13,
            m => Stop(m)["cumulative_pa"] = -1, m => Stop(m)["extra"] = 1, m => Stop(m).Remove("block_size"), m => Results(m)["stop"] = null, m => Stop(m)["executed_games"] = 0,
        })
            Assert.Equal("Replay.stop_record_invalid", Refused(w, malformed));
    }

    /// <summary>The replay's post-execution comparison of a one-field edit; asserts the edit was NOT silently accepted (FC2B-02) and returns the refusing code.</summary>
    private static string Caught(CalWorld w, Action<JsonObject> edit, Func<Action<JsonObject>, Result<ReplayPlan>> validate)
    {
        var error = Derive(w, H.Ok(validate(edit)));
        Assert.True(error is not null, "a forged execution-evidence field was accepted as REPLAY MATCH");
        return error!.Value.Code;
    }

    [Fact]
    public void Every_recorded_execution_field_of_a_v2_result_is_compared_with_the_re_execution()
    {
        // FC2B-02: the counts, the status and each game's event / rng hash and error code are evidence, never trusted. None of these edits touches the stop record, the plate appearances,
        // a game's status or result hash, or run_result_hash, so only the comparison with the re-execution can see them.
        foreach (var failing in new[] { false, true })
        {
            var w = failing ? Cal(status: s => s.GameIndex == 3 ? GameRunStatus.Failed : GameRunStatus.Completed) : Cal();
            string Code(Action<JsonObject> edit) => Caught(w, edit, e => Validate(w, e));
            Assert.Null(Derive(w, H.Ok(Validate(w))));                                                       // the unedited manifest still matches
            JsonNode Game(JsonObject m, int i) => Results(m)["games"]![i]!;

            Assert.Equal("Replay.result_counts_mismatch", Code(m => Results(m)["games_completed"] = Results(m)["games_completed"]!.GetValue<int>() - 1));
            Assert.Equal("Replay.result_counts_mismatch", Code(m => Results(m)["games_failed"] = Results(m)["games_failed"]!.GetValue<int>() + 1));
            Assert.Equal("Replay.result_counts_mismatch", Code(m => Results(m)["games_requested"] = Results(m)["games_requested"]!.GetValue<int>() - 1));
            Assert.Equal("Replay.result_counts_mismatch", Code(m => Results(m)["pitches"] = Results(m)["pitches"]!.GetValue<long>() + 1));
            Assert.Equal("Replay.result_games_mismatch", Code(m => Game(m, 5)["event_hash"] = "forged"));
            Assert.Equal("Replay.result_games_mismatch", Code(m => Game(m, 5)["rng_hash"] = "forged"));
            Assert.Equal("Replay.result_games_mismatch", Code(m => Game(m, 5)["error_code"] = "Harness.forged"));
            Assert.Equal("Replay.result_games_mismatch", Code(m => Game(m, 2)["error_code"] = failing ? null : "Harness.forged"));    // a failed game's code erased / a clean game given one
            Assert.Equal("Replay.run_status_mismatch", Code(m => Results(m)["run_status"] = failing ? "COMPLETED" : "GAMES_FAILED"));
            Assert.Equal("Replay.run_status_mismatch", Code(m => Results(m)["run_status"] = "METRICS_ERROR"));
        }

        // a failed run is replayable (SR-D9.2) and its failure fields are part of what must match
        var f = Cal(status: s => s.GameIndex == 3 ? GameRunStatus.Failed : GameRunStatus.Completed);
        Assert.Equal(("Failed", "GAMES_FAILED", 1), (Results(f.Manifest)["games"]![2]!["status"]!.GetValue<string>(), Results(f.Manifest)["run_status"]!.GetValue<string>(), Results(f.Manifest)["games_failed"]!.GetValue<int>()));

        // a missing field is a malformed manifest, never a default
        var w2 = Cal();
        foreach (var name in new[] { "games_completed", "games_failed", "games_requested", "pitches", "run_status" })
            Assert.Equal("Replay.manifest_invalid", Refused(w2, m => Results(m).Remove(name)));
        foreach (var name in new[] { "event_hash", "rng_hash", "error_code" })
            Assert.Equal("Replay.manifest_invalid", Refused(w2, m => Results(m)["games"]![0]!.AsObject().Remove(name)));
    }

    [Fact]
    public void Dropped_replaced_reordered_or_added_seeds_and_a_prefix_plan_are_refused()
    {
        var w = Cal();
        static void Swap(JsonArray a, int i, int j)
        {
            var x = a[i]!.DeepClone();
            a[i] = a[j]!.DeepClone();
            a[j] = x;
        }

        Assert.Equal("Replay.result_games_not_prefix", Refused(w, m => Results(m)["games"]!.AsArray().RemoveAt(2)));
        Assert.Equal("Replay.result_games_not_prefix", Refused(w, m => Results(m)["games"]![2]!["seed"] = 123));
        Assert.Equal("Replay.result_games_not_prefix", Refused(w, m => Swap(Results(m)["games"]!.AsArray(), 1, 2)));
        Assert.Equal("Replay.result_games_not_prefix", Refused(w, m => Results(m)["games"]!.AsArray().Add(Results(m)["games"]![0]!.DeepClone())));

        // the plan itself: the approved whole list only (a recorded prefix is the N-04 failure this ADR rules out)
        Assert.Equal("Replay.seed_games_mismatch", Refused(w, m => { var g = m["games"]!.AsArray(); while (g.Count > 14) g.RemoveAt(g.Count - 1); }));
        Assert.Equal("Replay.seed_games_mismatch", Refused(w, m => m["games"]![5]!["seed"] = 1));
        Assert.Equal("Replay.game_index_mismatch", Refused(w, m => Swap(m["games"]!.AsArray(), 0, 1)));
        Assert.Equal("Replay.seed_games_mismatch", Refused(w, m => m["games"]!.AsArray().Add(new JsonObject { ["index"] = 22, ["game_id"] = $"game:{CalKey}:neutral-mixed-v1-000022", ["seed"] = 1 })));
    }

    [Fact]
    public void A_v1_downgrade_a_missing_stop_and_a_cancelled_run_are_refused_for_calibration()
    {
        var w = Cal();
        Assert.Equal("Replay.result_version_downgrade", Refused(w, m => { Results(m).Remove("stop"); Results(m)["run_result_version"] = "run-result-v1"; }));
        Assert.Equal("Replay.result_version_downgrade", Refused(w, m => { Results(m).Remove("stop"); Results(m).Remove("run_result_version"); }));   // absent = v1
        Assert.Equal("Replay.stop_record_missing", Refused(w, m => Results(m).Remove("stop")));
        Assert.Equal("Replay.result_version_unknown", Refused(w, m => Results(m)["run_result_version"] = "run-result-v3"));
        Assert.Equal("Replay.run_not_terminated", Refused(w, m => Results(m)["run_status"] = "CANCELLED"));
        Assert.Equal("Replay.run_not_terminated", Refused(w, m => Results(m)["run_status"] = "ABORTED_INTEGRITY"));
        Assert.Equal("Replay.run_not_terminated", Refused(w, m => Results(m)["games"]![4]!["status"] = "Cancelled"));    // a cancelled game presented under a stop record
        // the official envelope rules still come first (F-C2-A unchanged)
        Assert.Equal("Replay.official_envelope_missing", Refused(w, m => { m.Remove("official"); m["run"]!["mode"] = "smoke"; }));
    }

    [Fact]
    public void A_failed_game_reproduces_under_replay_and_keeps_the_run_terminated_by_the_rule()
    {
        var w = Cal();
        var failing = w with { Play = s => Fake(s, 8_000, s.GameIndex == 3 ? GameRunStatus.Failed : GameRunStatus.Completed) };
        var result = SimRunner.RunStopAware(failing.Config, failing.Plan, play: failing.Play);
        Assert.Equal((new StopRecord(14, StopReason.TargetReached, 104_000, 7), 1), (result.Stop, Program.ExitCodeOf(result)));
        var recorded = RecordedStopResult.Of(result);
        Assert.Null(StopReplay.Precheck(failing.Plan, recorded));
        Assert.Null(StopReplay.Compare(recorded, result.RunResultHash, SimRunner.RunStopAware(failing.Config, failing.Plan, play: failing.Play)));   // the failure is reproduced, never repaired
        // a replay in which the game does NOT fail derives other valid PA (112,000): the recorded failure cannot be replayed away
        Assert.Equal("Replay.stop_cumulative_pa_mismatch", StopReplay.Compare(recorded, result.RunResultHash, SimRunner.RunStopAware(w.Config, w.Plan, play: w.Play))!.Value.Code);
    }

    [Fact]
    public void A_stop_record_or_v2_result_is_refused_outside_calibration_and_dry()
    {
        // a non-stop official family keeps the exact whole-list contract and refuses a stop record (SR-D5)
        var seed = SeedManifestTests.Manifest("fc3-dom", "fc3-dom-x");
        seed["rules_profile_id"] = HarnessData.AutomatedProfile;
        var bytes = Encoding.UTF8.GetBytes(seed.ToJsonString());
        File.WriteAllBytes(Path.Combine(_root, "seeds", "dom.json"), bytes);
        var sha = Sha(bytes);
        var config = H.Ok(RunConfig.Create("dom", "official", NeutralCohort.Create(), H.Rules(true), H.Tuning, H.Park(), "advancement-policy-v2", 3000, competitionKey: "fc3-dom-x"));
        var specs = seed["seeds"]!.AsArray().Select(s => config.Spec(s!["index"]!.GetValue<int>(), (ulong)s["seed"]!.GetValue<int>())).ToList();
        var full = new RunResult(config, specs, specs.Select(s => Fake(s, 70)).ToList(), new MetricCounters(), MetricsEvaluator.Evaluate(new MetricCounters(), config.Cohort, H.Tuning, EvaluationPolicy.Unapproved),
            EvaluationPolicy.Unapproved, config.ConfigHash(specs), "h");
        var manifest = JsonNode.Parse(JsonSerializer.Serialize(RunReport.Manifest(full, null), RunReport.Json))!.AsObject();
        var att = new OfficialAttestation(OfficialAttestation.Version, FixtureSha, OfficialProfile.Id, OfficialProfile.Hash(), "seeds/dom.json", sha, "fc3-dom", "fc3-dom-x", SeedManifestReader.GameIdRuleV1,
            "neutral-v1", config.Cohort.InputsHash, "normal-balanced-v1", "prob-model-v4", "tuning-virtual-proto-v4", OfficialProfile.TuningHash, HarnessData.AutomatedProfile,
            OfficialProfile.RulesHashes[HarnessData.AutomatedProfile], "advancement-policy-v2", OfficialProfile.ParkText, 3000, full.ConfigHash);
        manifest["official"] = JsonSerializer.SerializeToNode(new { schema_version = OfficialAttestation.EnvelopeVersion, attestation = att, attestation_hash = att.Hash() }, RunReport.Json);
        var approvals = new OfficialApprovals(FixtureSha, OfficialProfile.Id, OfficialProfile.Hash(), new Dictionary<string, string> { ["seeds/dom.json"] = sha },
            new Dictionary<string, string> { ["neutral-v1"] = config.Cohort.InputsHash }, true);
        string Code(Action<JsonObject>? edit = null)
        {
            var copy = JsonNode.Parse(manifest.ToJsonString())!.AsObject();
            edit?.Invoke(copy);
            var r = ReplayCheck.Validate(Json(copy.ToJsonString()), approvals, _root);
            return r.IsSuccess ? "OK" : r.Error!.Value.Code;
        }

        Assert.Equal("OK", Code());                                                                   // the full-execution official replay is unchanged
        Assert.Equal("Replay.stop_not_allowed", Code(m => Results(m)["run_result_version"] = "run-result-v2"));
        Assert.Equal("Replay.stop_not_allowed", Code(m => Results(m)["stop"] = new JsonObject { ["executed_games"] = 7, ["stop_reason"] = "TARGET_REACHED", ["cumulative_pa"] = 1, ["block_size"] = 7 }));
        Assert.Equal("Replay.seed_games_mismatch", Code(m => { var g = m["games"]!.AsArray(); while (g.Count > 7) g.RemoveAt(g.Count - 1); }));   // A4-A7: a shortened list is still refused

        // a legacy key never takes a stop record either, and the dry pin is not accepted for it
        var legacy = JsonNode.Parse(File.ReadAllText(Path.Combine(H.Root, "tests", "StarPlayer.SimHarness.Tests", "Fixtures", "legacy-manifest-neutral-default.json")))!.AsObject();
        Results(legacy)["run_result_version"] = "run-result-v2";
        Assert.Equal("Replay.stop_not_allowed", ReplayCheck.Validate(Json(legacy.ToJsonString()), OfficialApprovals.Current).Error!.Value.Code);
        Assert.Equal("Replay.dry_seed_manifest_unexpected", ReplayCheck.Validate(Json(legacy.ToJsonString()), OfficialApprovals.Current, _root, new DryStopPin("seeds/dom.json", sha)).Error!.Value.Code);
        Assert.Equal("Replay.dry_seed_manifest_unexpected", ReplayCheck.Validate(Json(manifest.ToJsonString()), approvals, _root, new DryStopPin("seeds/dom.json", sha)).Error!.Value.Code);   // never on an official replay
    }

    // ---------------------------------------------------------------- SR-D4 / SR-D8.6: a small fc2-dry run and replay on the real engine

    private (string Rel, string Sha) DrySeed(string key, int games, int block, long target, string cohort = CalibrationCohorts.NeutralMixedId, string name = "dry.json", string rules = HarnessData.AutomatedProfile)
    {
        var seed = SeedManifestTests.Manifest("fc2-dry", key, games: games, block: block, max: games, paTarget: target, cohort: cohort);
        seed["rules_profile_id"] = rules;
        var bytes = Encoding.UTF8.GetBytes(seed.ToJsonString());
        File.WriteAllBytes(Path.Combine(_root, "seeds", name), bytes);
        return ("seeds/" + name, Sha(bytes));
    }

    private static RunConfig DryConfig(string key, CohortInputs? cohort = null, bool automated = true) =>
        H.Ok(RunConfig.Create("dry-test", "dry", cohort ?? CalibrationCohorts.NeutralMixed(), H.Rules(automated), H.Tuning, H.Park(), "advancement-policy-v2", 3000, competitionKey: key));

    [Fact]
    public void A_small_target_dry_run_stops_by_the_rule_and_replays_only_with_the_operator_pin()
    {
        const string key = "fc2-dry-stop";
        var (rel, sha) = DrySeed(key, 6, 2, 120);
        var plan = H.Ok(StopPlan.FromSeedManifest(H.Ok(SeedManifestReader.Read(_root, rel, sha)), rel));
        var config = DryConfig(key);
        var first = SimRunner.RunStopAware(config, plan);
        var second = SimRunner.RunStopAware(config, plan);
        Assert.Equal(RunTermination.Rule, first.Termination);
        Assert.True(first.Stop!.ExecutedGames is 2 or 4 or 6 && first.Stop.ExecutedGames % 2 == 0);
        Assert.Equal(first.Games.Where(g => g.Completed).Sum(g => g.Counters.PlateAppearances), first.Stop.CumulativePa);
        Assert.Equal(first.Stop.ExecutedGames == 6 && first.Stop.CumulativePa < 120 ? StopReason.SeedsExhausted : StopReason.TargetReached, first.Stop.Reason);
        Assert.Equal((first.RunResultHash, first.ConfigHash, first.Stop), (second.RunResultHash, second.ConfigHash, second.Stop));       // deterministic
        Assert.Equal(0, Program.ExitCodeOf(first));

        var dir = Path.Combine(_root, "out1");
        RunReport.Write(first, null, dir);
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json")))!.AsObject();
        Assert.Equal((rel, sha), (manifest["seed_manifest"]!["path"]!.GetValue<string>(), manifest["seed_manifest"]!["sha256"]!.GetValue<string>()));
        var pin = new DryStopPin(rel, sha);
        string Code(Action<JsonObject>? edit = null, DryStopPin? p = null, bool usePin = true)
        {
            var copy = JsonNode.Parse(manifest.ToJsonString())!.AsObject();
            edit?.Invoke(copy);
            var r = ReplayCheck.Validate(Json(copy.ToJsonString()), OfficialApprovals.Current, _root, usePin ? p ?? pin : null);
            if (!r.IsSuccess)
                return r.Error!.Value.Code;
            Assert.Null(r.Value.CheckAgainst(config));
            return StopReplay.Compare(r.Value.Recorded!, r.Value.RunResultHash, SimRunner.RunStopAware(config, r.Value.Stop!))?.Code ?? "MATCH";
        }

        Assert.Equal("MATCH", Code());
        Assert.Equal("Replay.stop_parameters_untrusted", Code(usePin: false));                            // the recorded stop is never its own authority
        var (otherRel, otherSha) = DrySeed(key, 6, 2, 300, name: "other.json");                           // another target, same games
        Assert.Equal("Replay.seed_manifest_hash_mismatch", Code(p: new DryStopPin(otherRel, otherSha)));
        Assert.Equal("Seed.file_hash_mismatch", Code(p: new DryStopPin(rel, otherSha)));
        // a forged but internally consistent PA: refused by the precheck (if the rule contradicts it) or by the re-derivation
        Assert.Contains(Code(m => { Stop(m)["cumulative_pa"] = Stop(m)["cumulative_pa"]!.GetValue<long>() + 1; Results(m)["plate_appearances"] = Stop(m)["cumulative_pa"]!.GetValue<long>(); }),
            new[] { "Replay.stop_cumulative_pa_mismatch", "Replay.stop_record_inconsistent" });
        Assert.Equal("Replay.plate_appearances_mismatch", Code(m => Results(m)["plate_appearances"] = 1));
        Assert.Equal("Replay.result_version_downgrade", Code(m => { Results(m).Remove("stop"); Results(m).Remove("run_result_version"); }));
        Assert.Equal("Replay.run_result_hash_mismatch", Code(m => Results(m)["run_result_hash"] = new string('1', 64)));

        // the dry run never becomes official: no envelope, no official key, no gate input
        Assert.False(manifest.ContainsKey("official"));
        Assert.False(SeedFamilies.IsOfficialCompetitionKey(key));
    }

    [Fact]
    public void A_dry_run_that_never_reaches_its_target_is_seeds_exhausted_and_still_replays()
    {
        const string key = "fc2-dry-exhaust";
        var (rel, sha) = DrySeed(key, 2, 1, SeedManifestReader.DryMaxPa, cohort: CalibrationCohorts.VirtualLeagueId, rules: HarnessData.NonAutomatedProfile);
        var plan = H.Ok(StopPlan.FromSeedManifest(H.Ok(SeedManifestReader.Read(_root, rel, sha)), rel));
        var config = DryConfig(key, CalibrationCohorts.VirtualLeague(), automated: false);
        var run = SimRunner.RunStopAware(config, plan);
        Assert.Equal((StopReason.SeedsExhausted, 2, 0), (run.Stop!.Reason, run.Stop.ExecutedGames, Program.ExitCodeOf(run)));
        var recorded = RecordedStopResult.Of(run);
        Assert.Null(StopReplay.Precheck(plan, recorded));
        Assert.Null(StopReplay.Compare(recorded, run.RunResultHash, SimRunner.RunStopAware(config, plan)));
    }

    // ---------------------------------------------------------------- the command line, end to end (child processes in a TEMP repository root)

    private sealed record Child(int ExitCode, string Output, string Error);

    private static Child Spawn(string workingDirectory, params string[] arguments)
    {
        var host = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "") == "dotnet" ? Environment.ProcessPath! : "dotnet";
        var start = new ProcessStartInfo(host) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = workingDirectory };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (var a in arguments)
            start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000), "the harness process did not finish in 120 s");
        return new Child(process.ExitCode, output.Result, error.Result);
    }

    [Fact]
    public void The_dry_command_and_its_replay_work_end_to_end_and_refuse_without_the_pin()
    {
        File.WriteAllText(Path.Combine(_root, "StarPlayer.sln"), "");                                   // a TEMP repository root: nothing is written into the real repository
        var (rel, sha) = DrySeed("fc2-dry-cli", 4, 2, 120);
        var dry = Spawn(_root, "dry", "--seed-manifest", rel, "--seed-sha256", sha, "--run-id", "cli-dry");
        Assert.True(dry.ExitCode == 0, dry.Output + dry.Error);
        Assert.Contains("stop: ", dry.Output, StringComparison.Ordinal);
        var manifest = Path.Combine(_root, "build", "simharness", "cli-dry", "manifest.json");
        Assert.True(File.Exists(manifest));

        var replay = Spawn(_root, "replay", "--manifest", manifest, "--seed-manifest", rel, "--seed-sha256", sha);
        Assert.True(replay.ExitCode == 0, replay.Output + replay.Error);
        Assert.Contains("REPLAY MATCH", replay.Output, StringComparison.Ordinal);
        Assert.Contains(RunResultText.V2, replay.Output, StringComparison.Ordinal);

        var unpinned = Spawn(_root, "replay", "--manifest", manifest);
        Assert.Equal(2, unpinned.ExitCode);
        Assert.Contains("Replay.stop_parameters_untrusted", unpinned.Error, StringComparison.Ordinal);
        Assert.Equal(2, Spawn(_root, "replay", "--manifest", manifest, "--seed-manifest", rel).ExitCode);           // the pin comes as a pair
        Assert.Equal(2, Spawn(_root, "dry", "--seed-manifest", rel, "--seed-sha256", sha, "--run-id", "cli-dry").ExitCode);   // never overwrites an earlier output
        Assert.Equal(2, Spawn(_root, "dry", "--seed-manifest", rel, "--seed-sha256", sha, "--seeds", "1").ExitCode);           // the plan comes from the file only

        // an official family is not a dry run, and a dry file without a target has no stop rule
        var cal = SeedManifestTests.Manifest("fc3-cal", "fc3-cal-x", paTarget: 100_000);
        var calBytes = Encoding.UTF8.GetBytes(cal.ToJsonString());
        File.WriteAllBytes(Path.Combine(_root, "seeds", "cal.json"), calBytes);
        Assert.Contains("Dry.family_not_dry", Spawn(_root, "dry", "--seed-manifest", "seeds/cal.json", "--seed-sha256", Sha(calBytes)).Error, StringComparison.Ordinal);
        var plain = Encoding.UTF8.GetBytes(SeedManifestTests.Manifest(key: "fc2-dry-plain").ToJsonString());
        File.WriteAllBytes(Path.Combine(_root, "seeds", "plain.json"), plain);
        Assert.Contains("Stop.pa_target_missing", Spawn(_root, "dry", "--seed-manifest", "seeds/plain.json", "--seed-sha256", Sha(plain)).Error, StringComparison.Ordinal);
    }
}
