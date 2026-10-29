using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.Domain.Common;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// A TEST-ONLY official world for the F-C2 B-6 calibration / integration paths: a TEMP root holding four synthetic <c>fc3-cal</c> seed files (one per stratum L1..L4, 12 fictional seeds, block 2,
/// pa_target 100,000) and two synthetic <c>fc3-int</c> files (100 fictional seeds per mode), and approvals that pin them, freeze the cohort input hashes, declare a FIXTURE integration cohort
/// (<c>neutral-v1</c>; ADR-008 names none, this is not a proposal) and mark the path ready. None of it is an official seed, pin, cohort, sample size or approval; the real
/// <see cref="OfficialApprovals.Current"/> is untouched. Calibration runs on synthetic games (a 100,000-PA stratum is not a unit test, ADR-009); integration runs ONCE on the real engine (200 games).
/// </summary>
public sealed class FamilyOfficialWorld : IDisposable
{
    public const string Sha = "fa11fa11fa11fa11fa11fa11fa11fa11fa11fa11";

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "simharness-tests", "fam-official-" + Guid.NewGuid().ToString("N"));

    public OfficialApprovals Approvals { get; }

    public FamilyPreflightReport CalPreflight { get; }

    public FamilyOfficialResult CalResult { get; }

    public List<(string Key, int Index)> CalPlays { get; } = [];

    public FamilyPreflightReport IntPreflight { get; }

    public FamilyOfficialResult IntResult { get; }

    public FamilyOfficialWorld()
    {
        var pins = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var s in CalibrationStrata.All)
            pins[CalPath(s.Id)] = DominanceOfficialWorld.Put(Root, CalPath(s.Id), CalFile(s));
        foreach (var (mode, rules) in IntModes)
            pins[IntPath(mode)] = DominanceOfficialWorld.Put(Root, IntPath(mode), IntFile(mode, rules));
        Approvals = new OfficialApprovals(Sha, OfficialProfile.Id, OfficialProfile.Hash(), pins,
            new Dictionary<string, string>
            {
                [CalibrationCohorts.NeutralMixedId] = CalibrationCohorts.NeutralMixed().InputsHash, [CalibrationCohorts.VirtualLeagueId] = CalibrationCohorts.VirtualLeague().InputsHash,
                [NeutralCohort.Id] = NeutralCohort.Create().InputsHash,
            }, true, IntegrationCohortIds: new HashSet<string>(StringComparer.Ordinal) { NeutralCohort.Id });
        CalPreflight = FamilyPreflight.Run(OfficialFamilyPlan.Calibration, new DominanceOfficialRequest(Root, "cal-fixture", null), Approvals, Git());
        CalResult = FamilyOfficial.Execute(CalPreflight, "cal-fixture", Approvals, Root, play: (c, s) =>
        {
            CalPlays.Add((c.CompetitionKey, s.GameIndex));
            return Fake(s, Good());
        });
        IntPreflight = FamilyPreflight.Run(OfficialFamilyPlan.Integration, new DominanceOfficialRequest(Root, "int-fixture", null), Approvals, Git());
        IntResult = FamilyOfficial.Execute(IntPreflight, "int-fixture", Approvals, Root);         // the production executor (no seam)
    }

    public void Dispose() => Directory.Delete(Root, true);

    public static readonly (string Mode, string Rules)[] IntModes = [("a", HarnessData.AutomatedProfile), ("n", HarnessData.NonAutomatedProfile)];

    public static GitIntegrityReport Git(string? head = Sha) => GitIntegrityPolicy.Evaluate([], head);

    public static string CalPath(string stratum) => $"seeds/cal/{stratum.ToLowerInvariant()}.json";

    public static string IntPath(string mode) => $"seeds/int/{mode}.json";

    public static JsonObject CalFile(CalibrationStratum s, string? key = null, int games = 12)
    {
        var m = SeedManifestTests.Manifest("fc3-cal", key ?? $"fc3-cal-fx-{s.Id.ToLowerInvariant()}", games, 2, games, 100_000, s.CohortId);
        m["rules_profile_id"] = s.RulesProfileId;
        return m;
    }

    public static JsonObject IntFile(string mode, string rules, string? key = null, string cohort = NeutralCohort.Id)
    {
        var m = SeedManifestTests.Manifest("fc3-int", key ?? $"fc3-int-fx-{mode}", 100, 10, 100, null, cohort);
        m["rules_profile_id"] = rules;
        return m;
    }

    /// <summary>In-range counters of one synthetic game (F1 0.20 … F8 0.24, as in the stratified-evaluation tests), 10,000 PA.</summary>
    public static MetricCounters Good(long pa = 10_000) => new()
    {
        PlateAppearances = pa, Strikeouts = pa / 5, Walks = pa * 8 / 100, HitByPitch = pa * 9 / 1000, HomeRuns = pa / 40, AtBats = pa * 9 / 10, Hits = pa * 2256 / 10_000, SacrificeFlies = pa / 200,
        Pitches = pa * 3, LocatedPitches = pa * 3, ZonePitches = pa * 165 / 100, HitByPitchPitches = pa * 9 / 1000, Swings = pa * 144 / 100, SwingingStrikes = pa * 3456 / 10_000,
    };

    public static GameRunResult Fake(GameSpec spec, MetricCounters c, GameRunStatus status = GameRunStatus.Completed) => StopReplayTests.Fake(spec, c.PlateAppearances, status) with { Counters = c };
}

/// <summary>
/// F-C2 B-6: the official calibration (<c>fc3-cal</c>) and integration (<c>fc3-int</c>) paths — family preflight against the approvals' pins (fail-closed, blocked with the real approvals), the
/// execution entered only from a READY preflight (calibration: the ADR-009 block stop rule per stratum; integration: the whole plan per mode), the stored result (existing manifest / envelope /
/// attestation contracts per unit plus a DRAFT family index) and the replay that re-derives everything from trusted inputs before and after re-execution.
/// </summary>
public sealed class FamilyOfficialTests : IClassFixture<FamilyOfficialWorld>, IDisposable
{
    private static readonly OfficialFamilyPlan Cal = OfficialFamilyPlan.Calibration, Int = OfficialFamilyPlan.Integration;
    private readonly FamilyOfficialWorld _w;
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "simharness-tests", "fam-official-scratch-" + Guid.NewGuid().ToString("N"));

    public FamilyOfficialTests(FamilyOfficialWorld world)
    {
        _w = world;
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        for (var attempt = 0; ; attempt++)
            try
            {
                if (Directory.Exists(_scratch))
                    Directory.Delete(_scratch, true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 10)
            {
                Thread.Sleep(200);
            }
    }

    /// <summary>A game seam that counts every call (refusal paths must call it zero times).</summary>
    private sealed class Plays(Func<GameSpec, GameRunResult> play)
    {
        public int Count;

        public GameRunResult Play(RunConfig config, GameSpec spec)
        {
            Interlocked.Increment(ref Count);
            return play(spec);
        }
    }

    private static Plays Good() => new(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good()));

    private static PreflightCheck Check(FamilyPreflightReport r, string id) => r.Checks.Single(c => c.Id == id);

    private FamilyPreflightReport Preflight(OfficialFamilyPlan plan, OfficialApprovals? a = null, GitIntegrityReport? git = null, string runId = "fam-pre", string? root = null) =>
        FamilyPreflight.Run(plan, new DominanceOfficialRequest(root ?? _w.Root, runId, null), a ?? _w.Approvals, git ?? FamilyOfficialWorld.Git());

    private static void Refused(FamilyPreflightReport r, string id, CheckStatus status, string code)
    {
        Assert.False(r.Ready);
        Assert.Equal((status, code), (Check(r, id).Status, Check(r, id).Code));
    }

    private string CopyRoot()
    {
        var root = Path.Combine(_scratch, "root-" + Guid.NewGuid().ToString("N")[..8]);
        foreach (var file in Directory.GetFiles(Path.Combine(_w.Root, "seeds"), "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(root, Path.GetRelativePath(_w.Root, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        return root;
    }

    private string CopyResult(string from)
    {
        var dir = Path.Combine(_scratch, "result-" + Guid.NewGuid().ToString("N")[..8]);
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(dir, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        return dir;
    }

    private static JsonObject ReadNode(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static void WriteNode(string path, JsonNode node) => File.WriteAllText(path, node.ToJsonString(), new UTF8Encoding(false));

    private static string FileSha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string UnitManifest(string dir, OfficialFamilyPlan plan, int unit) => Path.Combine(dir, "units", plan.Units[unit].Id.ToLowerInvariant(), "manifest.json");

    /// <summary>Edits one unit manifest and (consistently) the index's record of its file hash, so only deeper checks can notice.</summary>
    private static void EditUnit(string dir, OfficialFamilyPlan plan, int unit, Action<JsonObject> edit)
    {
        var path = UnitManifest(dir, plan, unit);
        var m = ReadNode(path);
        edit(m);
        WriteNode(path, m);
        EditIndex(dir, d => d["units"]![unit]!["manifest_sha256"] = FileSha(path));
    }

    private static void EditIndex(string dir, Action<JsonObject> edit)
    {
        var path = Path.Combine(dir, FamilyOfficial.ResultFile);
        var d = ReadNode(path);
        edit(d);
        WriteNode(path, d);
    }

    private static void RefusedBeforeGames(FamilyReplayOutcome o, string code, Plays plays)
    {
        Assert.Equal(code, o.Error?.Code);
        Assert.False(o.GamesExecuted);
        Assert.Equal(0, plays.Count);
    }

    /// <summary>Executes a fresh calibration run on the world's pinned plan with the given synthetic games (the world's approvals; a new run id).</summary>
    private FamilyOfficialResult RunCal(string runId, Plays plays, string? root = null, Func<int, Error?>? integrity = null, CancellationToken cancellation = default) =>
        FamilyOfficial.Execute(Preflight(Cal, runId: runId, root: root), runId, _w.Approvals, root ?? _w.Root, cancellation, integrity, plays.Play);

    private FamilyOfficialResult RunInt(string runId, Plays plays, string? root = null) => FamilyOfficial.Execute(Preflight(Int, runId: runId, root: root), runId, _w.Approvals, root ?? _w.Root, play: plays.Play);

    private string Out(string runId, string? root = null) => Path.Combine(root ?? _w.Root, "build", "simharness", "official", runId);

    // ---------------------------------------------------------------- the real approvals: blocked

    [Fact]
    public void With_the_real_approvals_both_family_preflights_are_blocked_and_no_game_can_start()
    {
        foreach (var plan in new[] { Cal, Int })
        {
            var r = Preflight(plan, OfficialApprovals.Current, runId: "fam-blocked");
            Assert.False(r.Ready);
            Assert.Empty(r.Units);
            Refused(r, "seed_manifests", CheckStatus.NotReady, "Official.family_seed_manifests_not_frozen");
            Refused(r, "unit_preflight", CheckStatus.NotReady, "Official.input_unavailable");
            Refused(r, "git_sha", CheckStatus.NotReady, "Official.git_sha_not_approved");
            Refused(r, "execution_policy", CheckStatus.NotReady, "Official.execution_policy_not_frozen");
            Refused(r, "execution_path", CheckStatus.NotReady, "Official.not_ready");
            Assert.Equal("Official.not_ready", r.ErrorCode);
            var plays = Good();
            Assert.Throws<InvalidOperationException>(() => FamilyOfficial.Execute(r, "fam-blocked", OfficialApprovals.Current, _w.Root, play: plays.Play));   // never enters execution
            Assert.Equal(0, plays.Count);
            Assert.False(Directory.Exists(Out("fam-blocked")));
            RefusedBeforeGames(FamilyOfficialReplay.Verify(plan, plan.StopAware ? _w.CalPreflight.OutputDirectory! : _w.IntPreflight.OutputDirectory!, OfficialApprovals.Current, _w.Root, play: plays.Play),
                "Official.family_seed_manifests_not_frozen", plays);                         // a fixture result is never accepted against the real approvals
        }

        var current = OfficialApprovals.Current;                                              // nothing frozen, no integration cohort declared, no internal default or dry fixture stands in
        Assert.Equal((null, false, 0, (IReadOnlySet<string>?)null), (current.GitCommitSha, current.ExecutionPathReady, current.SeedManifestPins.Count, current.IntegrationCohortIds));
        Assert.Null(OfficialProfile.CohortsOf("fc3-int", current));
        Assert.Same(OfficialProfile.FamilyCohorts["fc3-cal"], OfficialProfile.CohortsOf("fc3-cal", current));
        Assert.False(OfficialProfile.FamilyCohorts.ContainsKey("fc3-int"));                  // the execution profile (and its hash) is unchanged
    }

    [Fact]
    public void The_family_commands_refuse_with_the_real_approvals_and_start_no_game()
    {
        foreach (var (command, family) in new[] { ("official-cal", "fc3-cal"), ("official-int", "fc3-int") })
        {
            var runId = $"b6-refusal-{family}-{Guid.NewGuid().ToString("N")[..8]}";
            var child = Spawn(command, "--run-id", runId);
            Assert.Equal(2, child.ExitCode);
            using var doc = JsonDocument.Parse(child.Output);
            var result = doc.RootElement;
            Assert.Equal((FamilyOfficial.ResultSchema, false, "PREFLIGHT_REFUSED", "NOT_EVALUATED", false, family), (result.GetProperty("schema_version").GetString(),
                result.GetProperty("official_evidence").GetBoolean(), result.GetProperty("run_status").GetString(), result.GetProperty("family_gate").GetString(), result.GetProperty("complete").GetBoolean(),
                result.GetProperty("family").GetString()));
            Assert.Contains(result.GetProperty("preflight").GetProperty("checks").EnumerateArray(), c => c.GetProperty("code").GetString() == "Official.family_seed_manifests_not_frozen");
            Assert.DoesNotContain("output:", child.Output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(H.Root, "build", "simharness", "official", runId)));
        }

        Assert.Equal(2, Program.Main(["replay-cal", "--result", _w.CalPreflight.OutputDirectory!]));   // fixture results are not accepted against the real approvals
        Assert.Equal(2, Program.Main(["replay-int", "--result", _w.IntPreflight.OutputDirectory!]));
        Assert.Equal(2, Program.Main(["replay-cal", "--result", Path.Combine(_scratch, "nothing")]));
        Assert.Equal(2, Program.Main(["official-cal", "--run-id", "x", "--seed-manifest", FamilyOfficialWorld.CalPath("L1")]));
    }

    [Fact]
    public void The_family_commands_accept_only_their_whitelisted_options()
    {
        foreach (var (run, replay) in new[] { ("official-cal", "replay-cal"), ("official-int", "replay-int") })
        {
            Assert.Equal(run, H.Ok(Cli.Parse([run, "--run-id", "r1"])).Name);
            Assert.Equal(2, H.Ok(Cli.Parse([run, "--run-id", "r1", "--out", "build/simharness/official/x"])).Options.Count);
            Assert.Equal(replay, H.Ok(Cli.Parse([replay, "--result", "x"])).Name);
            foreach (var forbidden in new[] { "--seed-manifest", "--seed-sha256", "--seeds", "--tuning", "--rules", "--advancement", "--event-budget" })
                Assert.Equal("Cli.option_forbidden", Cli.Parse([run, "--run-id", "r1", forbidden, "x"]).Error!.Value.Code);      // no plan input from the command line
            foreach (var unknown in new[] { "--family", "--pa-target", "--block-size", "--max-games", "--games", "--cohort", "--approvals" })
                Assert.Equal("Cli.option_unknown", Cli.Parse([run, "--run-id", "r1", unknown, "1"]).Error!.Value.Code);         // no hidden plan value can stand in for the freeze
            Assert.Equal("Cli.option_required", Cli.Parse([run]).Error!.Value.Code);
            Assert.Equal("Cli.option_duplicate", Cli.Parse([run, "--run-id", "r1", "--run-id", "r1"]).Error!.Value.Code);
            Assert.Equal("Cli.option_required", Cli.Parse([replay]).Error!.Value.Code);
            Assert.Equal("Cli.option_unknown", Cli.Parse([replay, "--result", "x", "--manifest", "y"]).Error!.Value.Code);
            Assert.Equal("Cli.option_unknown", Cli.Parse([replay, "--result", "x", "--seed-sha256", "y"]).Error!.Value.Code);
        }
    }

    // ---------------------------------------------------------------- calibration: preflight

    [Fact]
    public void A_fully_pinned_calibration_plan_is_ready_with_one_attestation_per_stratum_in_order()
    {
        var r = _w.CalPreflight;
        Assert.True(r.Ready, string.Join("; ", r.Checks.Where(c => c.Status != CheckStatus.Pass).Select(c => $"{c.Id} {c.Code} {c.Message}")));
        Assert.Equal(new[] { "L1", "L2", "L3", "L4" }, r.Units.Select(u => u.Pinned.Unit.Id));
        foreach (var u in r.Units)
        {
            var att = u.Report.Attestation!;
            var s = CalibrationStrata.All.Single(x => x.Id == u.Pinned.Unit.Id);
            Assert.Equal((FamilyOfficialWorld.CalPath(s.Id), "fc3-cal", s.CohortId, s.RulesProfileId), (att.SeedManifestPath, att.SeedFamily, att.CohortId, att.RulesProfileId));
            var config = H.Ok(FamilyOfficial.Config(u.Pinned.Manifest, "cal-fixture"));
            Assert.Equal(config.ConfigHash(u.Pinned.Manifest.Seeds.Select(x => config.Spec(x.Index, x.Seed))), att.ConfigHash);   // the WHOLE plan (SR-D1.1)
        }

        Assert.True(Preflight(Cal, _w.Approvals with { IntegrationCohortIds = null }).Ready);  // the undeclared integration cohort never blocks calibration
    }

    [Fact]
    public void The_calibration_plan_comes_only_from_the_pins_and_every_gap_or_conflict_is_refused()
    {
        var a = _w.Approvals;
        Dictionary<string, string> Pins(Action<Dictionary<string, string>> edit)
        {
            var p = new Dictionary<string, string>(a.SeedManifestPins, StringComparer.Ordinal);
            edit(p);
            return p;
        }

        var l1 = CalibrationStrata.All[0];
        Refused(Preflight(Cal, a with { SeedManifestPins = Pins(p => p.Remove(FamilyOfficialWorld.CalPath("L3"))) }), "seed_manifests", CheckStatus.Fail, "Official.family_units_missing");
        var twice = Pins(p => p["seeds/cal/l1b.json"] = DominanceOfficialWorld.Put(_w.Root, "seeds/cal/l1b.json", FamilyOfficialWorld.CalFile(l1, "fc3-cal-fx-l1b")));
        Refused(Preflight(Cal, a with { SeedManifestPins = twice }), "seed_manifests", CheckStatus.Fail, "Official.family_unit_duplicate");
        var reused = Pins(p => p["seeds/cal/l1c.json"] = DominanceOfficialWorld.Put(_w.Root, "seeds/cal/l1c.json", FamilyOfficialWorld.CalFile(l1)));
        Refused(Preflight(Cal, a with { SeedManifestPins = reused }), "seed_manifests", CheckStatus.Fail, "Seed.competition_key_reused");
        var stray = FamilyOfficialWorld.CalFile(l1, "fc3-cal-fx-stray");
        stray["cohort_id"] = NeutralCohort.Id;                                                // a calibration file of no FC-D7 stratum
        stray["seeds"] = SeedManifestTests.Manifest("fc3-cal", "fc3-cal-fx-stray", 12, 2, 12, 100_000, NeutralCohort.Id)["seeds"]!.DeepClone();
        var unbound = Pins(p => p["seeds/cal/stray.json"] = DominanceOfficialWorld.Put(_w.Root, "seeds/cal/stray.json", stray));
        Refused(Preflight(Cal, a with { SeedManifestPins = unbound }), "seed_manifests", CheckStatus.Fail, "Official.family_unit_unbound");
        // B-7 (kept fail-closed): an approved pin of ANY family that cannot be read fails the whole approved set
        Refused(Preflight(Cal, a with { SeedManifestPins = Pins(p => p["seeds/dom/missing.json"] = new string('d', 64)) }), "seed_manifests", CheckStatus.Fail, "Seed.file_missing");
        Refused(Preflight(Cal, a with { SeedManifestPins = Pins(p => p[FamilyOfficialWorld.CalPath("L2")] = new string('0', 64)) }), "seed_manifests", CheckStatus.Fail, "Seed.file_hash_mismatch");

        var tampered = CopyRoot();
        File.AppendAllText(Path.Combine(tampered, "seeds", "cal", "l4.json"), " ");
        Refused(Preflight(Cal, root: tampered), "seed_manifests", CheckStatus.Fail, "Seed.file_hash_mismatch");
        Refused(Preflight(Cal, a with { CohortInputsHashes = new Dictionary<string, string> { [CalibrationCohorts.NeutralMixedId] = a.CohortInputsHashes[CalibrationCohorts.NeutralMixedId] } }),
            "unit_preflight", CheckStatus.NotReady, "Official.cohort_hash_not_frozen");
        Refused(Preflight(Cal, git: GitIntegrityPolicy.Evaluate(H.Ok(GitStatusParser.Parse(" M src/a.cs\0")), FamilyOfficialWorld.Sha)), "git_integrity", CheckStatus.Fail, "Git.dirty");
        Refused(Preflight(Cal, git: FamilyOfficialWorld.Git(new string('1', 40))), "git_sha", CheckStatus.Fail, "Official.git_sha_mismatch");
        Refused(Preflight(Cal, a with { ExecutionPolicyHash = new string('e', 64) }), "execution_policy", CheckStatus.Fail, "Official.execution_policy_mismatch");
        Refused(Preflight(Cal, a with { ExecutionPathReady = false }), "execution_path", CheckStatus.NotReady, "Official.not_ready");
        Refused(Preflight(Cal, runId: "cal-fixture"), "output_directory", CheckStatus.Fail, "Official.output_exists");   // never over an existing result
    }

    // ---------------------------------------------------------------- calibration: execution, stop rule and store

    [Fact]
    public void Each_stratum_stops_at_the_first_block_boundary_reaching_the_target_and_is_stored_under_existing_contracts()
    {
        var r = _w.CalResult;
        Assert.True(r.Complete, $"{r.Abort?.Code}: {r.Abort?.Message}");
        Assert.Equal((RunReport.StatusCompleted, 0), (r.RunStatus, r.ExitCode));
        foreach (var (u, i) in r.Units.Select((u, i) => (u, i)))
        {
            Assert.Equal(new StopRecord(10, StopReason.TargetReached, 100_000, 2), u.Run.Stop);      // 10,000 PA per game, block 2: the first boundary at which PA >= 100,000
            Assert.Equal((12, 10, 2, RunResultText.V2), (u.Run.Requested, u.Run.Games.Count, u.Run.UnexecutedAfterStop, u.Run.ResultVersion));
            var key = u.Plan.Pinned.Manifest.CompetitionKey;
            Assert.Equal(Enumerable.Range(1, 10), _w.CalPlays.Where(p => p.Key == key).Select(p => p.Index));   // the executed prefix, in plan order; games 11-12 never played (SR-D1.3)
            var manifest = JsonDocument.Parse(File.ReadAllBytes(UnitManifest(_w.CalPreflight.OutputDirectory!, Cal, i))).RootElement;
            var plan = H.Ok(ReplayCheck.Validate(manifest, _w.Approvals, _w.Root));            // a normal official stop-aware manifest the EXISTING replay check accepts
            Assert.Equal((12, 2, 100_000L), (plan.Stop!.Games.Count, plan.Stop.BlockSize, plan.Stop.PaTarget));
        }

        Assert.Equal(40, _w.CalPlays.Count);
        Assert.All(r.Evaluation!.Units, u => Assert.Equal(GateStatus.Pass, u.Gate));
        Assert.Equal(GateStatus.Pass, r.Evaluation.Gate);
        var index = ReadNode(Path.Combine(_w.CalPreflight.OutputDirectory!, FamilyOfficial.ResultFile));
        Assert.Equal((FamilyOfficial.ResultSchema, "DRAFT_NOT_APPROVED", false, "fc3-cal", "PASS"), (index["schema_version"]!.GetValue<string>(), index["schema_status"]!.GetValue<string>(),
            index["official_evidence"]!.GetValue<bool>(), index["family"]!.GetValue<string>(), index["family_gate"]!.GetValue<string>()));
        Assert.Equal("TARGET_REACHED", index["units"]![0]!["stop"]!["stop_reason"]!.GetValue<string>());
    }

    [Fact]
    public void A_target_reached_mid_block_completes_the_block_and_exhaustion_is_seeds_exhausted_and_insufficient()
    {
        var early = new Plays(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(s.GameIndex == 1 ? 100_000 : 10)));
        var mid = RunCal("cal-mid", early);
        Assert.All(mid.Units, u => Assert.Equal(new StopRecord(2, StopReason.TargetReached, 100_010, 2), u.Run.Stop));   // never an early stop after game 1
        Assert.Equal(8, early.Count);
        Assert.Equal(0, mid.ExitCode);
        var again = new Plays(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(s.GameIndex == 1 ? 100_000 : 10)));
        Assert.Null(FamilyOfficialReplay.Verify(Cal, Out("cal-mid"), _w.Approvals, _w.Root, play: again.Play).Error);

        var small = new Plays(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(100)));
        var exhausted = RunCal("cal-exhausted", small);
        Assert.All(exhausted.Units, u => Assert.Equal(new StopRecord(12, StopReason.SeedsExhausted, 1_200, 2), u.Run.Stop));   // no seed is added
        Assert.Equal((RunReport.StatusCompleted, 0, GateStatus.InsufficientSample), (exhausted.RunStatus, exhausted.ExitCode, exhausted.Evaluation!.Gate));  // technical success, never PASS (SR-D9.2)
        Assert.All(exhausted.Evaluation.Units, u => Assert.Equal(GateStatus.InsufficientSample, u.Gate));
        var replay = new Plays(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(100)));
        var outcome = FamilyOfficialReplay.Verify(Cal, Out("cal-exhausted"), _w.Approvals, _w.Root, play: replay.Play);
        Assert.Null(outcome.Error);
        Assert.Equal(48, replay.Count);
    }

    [Fact]
    public void A_failed_game_keeps_its_seed_and_fails_the_gate_and_a_metrics_error_is_not_evaluated_with_the_existing_priority()
    {
        GameRunResult FailedThird(GameSpec s) => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(), s.GameIndex == 3 ? GameRunStatus.Failed : GameRunStatus.Completed);
        var failed = RunCal("cal-failed", new Plays(FailedThird));
        Assert.True(failed.Complete);
        Assert.Equal((RunReport.StatusGamesFailed, 1, GateStatus.Fail), (failed.RunStatus, failed.ExitCode, failed.Evaluation!.Gate));
        var l1 = failed.Units[0].Run;
        Assert.Equal(new StopRecord(12, StopReason.TargetReached, 110_000, 2), l1.Stop);       // the failed game's PA is not valid, it still counts as played; the next seed is not substituted
        Assert.Equal((GameRunStatus.Failed, 990003UL), (l1.Games[2].Status, l1.Games[2].Game.Seed));
        Assert.Null(FamilyOfficialReplay.Verify(Cal, Out("cal-failed"), _w.Approvals, _w.Root, play: new Plays(FailedThird).Play).Error);   // failures reproduce (SR-D8.5), never a PASS

        static MetricCounters Broken() => new() { PlateAppearances = 10_000, Strikeouts = 10_001 };           // SO > PA: the metrics cannot be computed
        var metrics = RunCal("cal-metrics", new Plays(s => FamilyOfficialWorld.Fake(s, s.GameIndex >= 1 && Stratum(s) == "L1" ? Broken() : FamilyOfficialWorld.Good())));
        var l1Verdict = metrics.Evaluation!.Units[0];
        Assert.Equal((GateStatus.NotEvaluated, "Metrics.counts_inconsistent"), (l1Verdict.Gate, l1Verdict.ErrorCode));       // SR-D9.2: metrics error = NOT_EVALUATED
        Assert.Equal((RunReport.StatusMetricsError, 3), (metrics.RunStatus, metrics.ExitCode));
        Assert.Equal(GateStatus.NotEvaluated, metrics.Evaluation.Gate);                        // never PASS while a stratum is unevaluated
        Assert.Null(FamilyOfficialReplay.Verify(Cal, Out("cal-metrics"), _w.Approvals, _w.Root,
            play: new Plays(s => FamilyOfficialWorld.Fake(s, Stratum(s) == "L1" ? Broken() : FamilyOfficialWorld.Good())).Play).Error);

        GameRunResult Both(GameSpec s) => Stratum(s) == "L1" ? FamilyOfficialWorld.Fake(s, Broken(), s.GameIndex == 2 ? GameRunStatus.Failed : GameRunStatus.Completed) : FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good());
        var both = RunCal("cal-both", new Plays(Both));
        Assert.Equal((GateStatus.Fail, "Metrics.counts_inconsistent"), (both.Evaluation!.Units[0].Gate, both.Evaluation.Units[0].ErrorCode));   // a failed game is still FAIL (FC-D9.1)
        Assert.Equal((GateStatus.Fail, RunReport.StatusGamesFailed, 1), (both.Evaluation.Gate, both.RunStatus, both.ExitCode));   // the FAIL is not hidden as a "missing" stratum
    }

    private static string Stratum(GameSpec s) => s.GameId.Split(':')[1][^2..].ToUpperInvariant();

    // B6-01 (FIX1): a stratum whose metrics could not be computed was dropped from the combination and so counted as a MISSING one, which outranked another stratum's INSUFFICIENT_SAMPLE.
    [Fact]
    public void A_metrics_error_stratum_keeps_its_state_in_the_family_combination_and_never_outranks_a_sample_shortage()
    {
        static MetricCounters Broken() => new() { PlateAppearances = 10_000, Strikeouts = 10_001 };          // SO > PA: the metrics cannot be computed
        var ids = CalibrationStrata.All.Select(s => s.Id).ToList();
        FamilyOfficialResult Mixed(string runId, int broken, int? shortage) => RunCal(runId, new Plays(s =>
            Stratum(s) == ids[broken] ? FamilyOfficialWorld.Fake(s, Broken())
            : shortage is { } n && Stratum(s) == ids[n] ? FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(100))   // 12 games x 100 PA: SEEDS_EXHAUSTED
            : FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good())));

        for (var broken = 0; broken < 4; broken++)                                           // the position of the failed stratum never changes the combination
        {
            var shortage = (broken + 1) % 4;
            var mixed = Mixed($"cal-mixed-{broken}", broken, shortage);
            var units = mixed.Evaluation!.Units;
            Assert.Equal((GateStatus.NotEvaluated, "Metrics.counts_inconsistent"), (units[broken].Gate, units[broken].ErrorCode));           // SR-D9.2 for the stratum itself is unchanged
            Assert.Equal((GateStatus.InsufficientSample, (string?)null), (units[shortage].Gate, units[shortage].ErrorCode));
            Assert.All(units.Where((_, i) => i != broken && i != shortage), u => Assert.Equal(GateStatus.Pass, u.Gate));
            Assert.Equal(GateStatus.InsufficientSample, mixed.Evaluation.Gate);              // the shortage is the combined state; the metrics error stays on the stratum
            Assert.Contains($"{ids[broken]}=NOT_EVALUATED", mixed.Evaluation.Reason, StringComparison.Ordinal);   // no state is dropped from the explanation
            Assert.Contains($"{ids[shortage]}=INSUFFICIENT_SAMPLE", mixed.Evaluation.Reason, StringComparison.Ordinal);
            Assert.Equal((RunReport.StatusMetricsError, 3), (mixed.RunStatus, mixed.ExitCode));

            var only = Mixed($"cal-only-{broken}", broken, null);                            // a metrics error alone is still NOT_EVALUATED (never PASS, never FAIL)
            Assert.Equal(GateStatus.NotEvaluated, only.Evaluation!.Gate);
            Assert.Equal((GateStatus.NotEvaluated, "Metrics.counts_inconsistent"), (only.Evaluation.Units[broken].Gate, only.Evaluation.Units[broken].ErrorCode));
        }

        var stored = Mixed("cal-mixed-replay", 0, 1);
        var outcome = FamilyOfficialReplay.Verify(Cal, Out("cal-mixed-replay"), _w.Approvals, _w.Root, play: new Plays(s => Stratum(s) switch
        {
            "L1" => FamilyOfficialWorld.Fake(s, Broken()), "L2" => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(100)), _ => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good()),
        }).Play);
        Assert.Null(outcome.Error);                                                           // the stored family result equals the replay's recomputation
        Assert.Equal(stored.Evaluation!.Canonical(), outcome.Evaluation!.Canonical());
        Assert.Equal((GateStatus.InsufficientSample, "INSUFFICIENT_SAMPLE"), (outcome.Evaluation.Gate, ReadNode(Path.Combine(Out("cal-mixed-replay"), FamilyOfficial.ResultFile))["family_gate"]!.GetValue<string>()));
    }

    [Fact]
    public void A_fail_stratum_still_outranks_a_metrics_error_and_a_sample_shortage_in_the_family_combination()
    {
        static MetricCounters Broken() => new() { PlateAppearances = 10_000, Strikeouts = 10_001 };
        // L1: metrics error AND a failed game (FAIL, FC-D9.1); L2: shortage; L3: failed game with computable metrics (FAIL); L4: PASS
        var both = RunCal("cal-fail-mixed", new Plays(s => Stratum(s) switch
        {
            "L1" => FamilyOfficialWorld.Fake(s, Broken(), s.GameIndex == 2 ? GameRunStatus.Failed : GameRunStatus.Completed),
            "L2" => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(100)),
            "L3" => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(), s.GameIndex == 2 ? GameRunStatus.Failed : GameRunStatus.Completed),
            _ => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good()),
        }));
        Assert.Equal(new[] { GateStatus.Fail, GateStatus.InsufficientSample, GateStatus.Fail, GateStatus.Pass }, both.Evaluation!.Units.Select(u => u.Gate));
        Assert.Equal("Metrics.counts_inconsistent", both.Evaluation.Units[0].ErrorCode);       // the FAIL does not hide the metrics error
        Assert.Equal((GateStatus.Fail, RunReport.StatusGamesFailed, 1), (both.Evaluation.Gate, both.RunStatus, both.ExitCode));
        Assert.Contains("L1", both.Evaluation.Reason, StringComparison.Ordinal);               // the failed stratum with the metrics error is named, not reported as "missing"
        Assert.DoesNotContain("평가가 없습니다", both.Evaluation.Reason, StringComparison.Ordinal);

        var onlyError = RunCal("cal-fail-error", new Plays(s => FamilyOfficialWorld.Fake(s, Stratum(s) == "L2" ? new MetricCounters { PlateAppearances = 10_000, Strikeouts = 10_001 } : FamilyOfficialWorld.Good(),
            Stratum(s) == "L2" && s.GameIndex == 3 ? GameRunStatus.Failed : GameRunStatus.Completed)));
        Assert.Equal((GateStatus.Fail, GateStatus.Fail), (onlyError.Evaluation!.Units[1].Gate, onlyError.Evaluation.Gate));
    }

    [Fact]
    public void Cancellation_and_broken_integrity_leave_an_incomplete_result_that_replay_refuses()
    {
        var cancel = RunCal("cal-cancel", new Plays(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(), Stratum(s) == "L2" && s.GameIndex == 1 ? GameRunStatus.Cancelled : GameRunStatus.Completed)));
        Assert.Equal((DominanceRunTermination.Cancelled, 2, false, RunReport.StatusCancelled, 1), (cancel.Termination, cancel.Units.Count, cancel.Complete, cancel.RunStatus, cancel.ExitCode));
        Assert.Null(cancel.Units[1].Run.Stop);                                                // never presented as a rule stop (SR-D3.5)
        var plays = Good();
        RefusedBeforeGames(FamilyOfficialReplay.Verify(Cal, Out("cal-cancel"), _w.Approvals, _w.Root, play: plays.Play), "FamOfficial.run_not_complete", plays);

        var calls = 0;
        var aborted = RunCal("cal-abort", Good(), integrity: _ => ++calls == 4 ? new Error("Seed.file_hash_mismatch", "pin broke mid-run") : null);
        Assert.Equal((DominanceRunTermination.AbortedIntegrity, "Seed.file_hash_mismatch", RunReport.StatusAborted, 2), (aborted.Termination, aborted.Abort?.Code, aborted.RunStatus, aborted.ExitCode));
        Assert.Equal((1, RunTermination.AbortedIntegrity, (StopRecord?)null, 4), (aborted.Units.Count, aborted.Units[0].Run.Termination, aborted.Units[0].Run.Stop, aborted.Units[0].Run.Games.Count));  // aborted before L1's third block
        Assert.Null(aborted.Evaluation);
        Assert.True(File.Exists(UnitManifest(Out("cal-abort"), Cal, 0)));                    // the evidence is kept
        Assert.False(ReadNode(Path.Combine(Out("cal-abort"), FamilyOfficial.ResultFile))["complete"]!.GetValue<bool>());
        RefusedBeforeGames(FamilyOfficialReplay.Verify(Cal, Out("cal-abort"), _w.Approvals, _w.Root, play: plays.Play), "FamOfficial.run_not_complete", plays);
        EditIndex(Out("cal-abort"), d => { d["complete"] = true; d["run_status"] = RunReport.StatusCompleted; });   // an aborted run presented as complete
        RefusedBeforeGames(FamilyOfficialReplay.Verify(Cal, Out("cal-abort"), _w.Approvals, _w.Root, play: plays.Play), "FamOfficial.unit_set_mismatch", plays);

        // a pinned file changed DURING the run: the default re-verification before the next block aborts it
        var root = CopyRoot();
        var report = Preflight(Cal, root: root, runId: "cal-changed");
        Assert.True(report.Ready);
        var changed = FamilyOfficial.Execute(report, "cal-changed", _w.Approvals, root, play: (c, s) =>
        {
            if (s.GameIndex == 3)
                File.AppendAllText(Path.Combine(root, "seeds", "cal", "l3.json"), " ");
            return FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good());
        });
        Assert.Equal((DominanceRunTermination.AbortedIntegrity, 1, 4, "Seed.file_hash_mismatch"), (changed.Termination, changed.Units.Count, changed.Units[0].Run.Games.Count, changed.Abort?.Code));
    }

    // ---------------------------------------------------------------- calibration: replay

    [Fact]
    public void The_calibration_result_replays_by_re_deriving_every_stop_from_the_pinned_plan()
    {
        var plays = Good();
        var outcome = FamilyOfficialReplay.Verify(Cal, _w.CalPreflight.OutputDirectory!, _w.Approvals, _w.Root, play: plays.Play);
        Assert.Null(outcome.Error);
        Assert.True(outcome.GamesExecuted);
        Assert.Equal(40, plays.Count);                                                        // only the derived prefix of each stratum is re-run
        Assert.Equal(_w.CalResult.Evaluation!.Canonical(), outcome.Evaluation!.Canonical());

        var other = new Plays(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(9_000)));   // a different execution: the stop moves, the replay differs
        var differs = FamilyOfficialReplay.Verify(Cal, _w.CalPreflight.OutputDirectory!, _w.Approvals, _w.Root, play: other.Play);
        Assert.Equal(("Replay.stop_too_early", true), (differs.Error?.Code, differs.GamesExecuted));   // 9,000 PA per game: the rule stops at 12, the record at 10
    }

    [Fact]
    public void G5T01_the_byte_replay_asks_for_each_file_once_and_equals_the_directory_replay()
    {
        var dir = _w.CalPreflight.OutputDirectory!;
        var asked = new List<string>();
        byte[]? Read(string path)
        {
            asked.Add(path);
            var file = Path.Combine([dir, .. path.Split('/')]);
            return File.Exists(file) ? File.ReadAllBytes(file) : null;
        }

        var bytes = FamilyOfficialReplay.Verify(Cal, Read, _w.Approvals, _w.Root, play: Good().Play);
        var path = FamilyOfficialReplay.Verify(Cal, dir, _w.Approvals, _w.Root, play: Good().Play);
        Assert.Null(bytes.Error);
        Assert.True(bytes.GamesExecuted);
        Assert.Equal(path.Evaluation!.Canonical(), bytes.Evaluation!.Canonical());
        Assert.Equal(path.Reruns!.Select(r => r.RunResultHash), bytes.Reruns!.Select(r => r.RunResultHash));
        Assert.Equal([FamilyOfficial.ResultFile, .. Cal.Units.Select(u => OfficialFamilyPlan.UnitDirectory(u) + "/manifest.json")], asked);

        // an absent manifest is the same refusal as a missing file in a directory, before any game
        var plays = Good();
        var missing = FamilyOfficialReplay.Verify(Cal, p => p == FamilyOfficial.ResultFile ? Read(p) : null, _w.Approvals, _w.Root, play: plays.Play);
        Assert.Equal(("FamOfficial.result_invalid", false, 0), (missing.Error?.Code, missing.GamesExecuted, plays.Count));
    }

    [Fact]
    public void A_calibration_replay_refuses_every_tampered_or_untrusted_record_before_any_game()
    {
        var src = _w.CalPreflight.OutputDirectory!;
        void Expect(string code, Action<string> tamper, OfficialApprovals? a = null, string? root = null)
        {
            var dir = CopyResult(src);
            tamper(dir);
            var plays = Good();
            RefusedBeforeGames(FamilyOfficialReplay.Verify(Cal, dir, a ?? _w.Approvals, root ?? _w.Root, play: plays.Play), code, plays);
        }

        Expect("Replay.stop_record_inconsistent", d => EditUnit(d, Cal, 0, m => m["results"]!["stop"]!["stop_reason"] = "SEEDS_EXHAUSTED"));     // a forged stop reason
        Expect("Replay.result_games_not_prefix", d => EditUnit(d, Cal, 1, m => m["results"]!["stop"]!["executed_games"] = 8));                 // an earlier stop
        Expect("Replay.stop_too_late", d => EditUnit(d, Cal, 1, m => m["results"]!["stop"]!["executed_games"] = 14));                         // past the plan
        Expect("Replay.result_version_downgrade", d => EditUnit(d, Cal, 2, m => m["results"]!["run_result_version"] = RunResultText.V1));       // v1 for fc3-cal
        Expect("Replay.stop_record_missing", d => EditUnit(d, Cal, 2, m => m["results"]!.AsObject().Remove("stop")));
        Expect("Replay.seed_games_mismatch", d => EditUnit(d, Cal, 3, m => m["games"]!.AsArray().RemoveAt(11)));                                 // the plan is the whole file, never a prefix
        Expect("FamOfficial.unit_record_mismatch", d => EditIndex(d, x => x["units"]![0]!["stop"]!["stop_reason"] = "SEEDS_EXHAUSTED"));      // an index-only edit
        Expect("FamOfficial.unit_record_mismatch", d => EditIndex(d, x => x["units"]![1]!["run_result_hash"] = new string('0', 64)));
        Expect("FamOfficial.unit_order_mismatch", d => EditIndex(d, x =>
        {
            var units = x["units"]!.AsArray();
            var first = units[0]!.DeepClone();
            units[0] = units[1]!.DeepClone();
            units[1] = first;
        }));
        Expect("FamOfficial.unit_set_mismatch", d => EditIndex(d, x => x["units"]!.AsArray().RemoveAt(3)));
        Expect("FamOfficial.unit_path_mismatch", d => EditIndex(d, x => x["units"]![0]!["manifest_path"] = "units/l2/manifest.json"));
        Expect("FamOfficial.unit_manifest_hash_mismatch", d => File.AppendAllText(UnitManifest(d, Cal, 0), " "));
        Expect("FamOfficial.seed_plan_mismatch", d =>                                                           // another stratum's genuine manifest in L1's place
        {
            File.Copy(UnitManifest(d, Cal, 1), UnitManifest(d, Cal, 0), true);
            EditIndex(d, x => x["units"]![0]!["manifest_sha256"] = FileSha(UnitManifest(d, Cal, 0)));
        });
        Expect("FamOfficial.run_not_complete", d => EditIndex(d, x => x["complete"] = false));
        Expect("FamOfficial.run_not_complete", d => EditIndex(d, x => x["run_status"] = RunReport.StatusCancelled));
        Expect("FamOfficial.result_schema_unsupported", d => EditIndex(d, x => x["schema_version"] = "simharness-family-result-v1"));   // no self-promotion to an official schema
        Expect("FamOfficial.approval_record_mismatch", d => EditIndex(d, x => x["approvals"]!["evaluation_policy_hash"] = new string('0', 64)));
        Expect("FamOfficial.result_missing", d => File.Delete(Path.Combine(d, FamilyOfficial.ResultFile)));
        Expect("FamOfficial.result_invalid", d => File.WriteAllText(Path.Combine(d, FamilyOfficial.ResultFile), "[]"));
        Expect("FamOfficial.approval_record_mismatch", _ => { }, _w.Approvals with { ExecutionPolicyId = "x", ExecutionPolicyHash = new string('e', 64) });
        var tampered = CopyRoot();
        File.AppendAllText(Path.Combine(tampered, "seeds", "cal", "l2.json"), " ");
        Expect("Seed.file_hash_mismatch", _ => { }, root: tampered);                                            // the plan is re-derived from the pins, not from the result

        var plays = Good();                                                                    // the result of one family is not a result of the other (each command is bound to its family)
        RefusedBeforeGames(FamilyOfficialReplay.Verify(Int, src, _w.Approvals, _w.Root, play: plays.Play), "FamOfficial.family_mismatch", plays);
    }

    [Fact]
    public void A_calibration_replay_rejects_a_forged_evaluation_after_re_execution()
    {
        foreach (var tamper in new Action<JsonObject>[]
        {
            x => x["family_gate"] = "FAIL",
            x => x["family_gate_reason"] = "forged",
            x => x["evaluation_canonical"] = "forged",
            x => x["run_status"] = RunReport.StatusGamesFailed,
            x => x["units"]![2]!["gate"] = "INSUFFICIENT_SAMPLE",
            x => x["units"]![2]!["valid_pa"] = 1,
        })
        {
            var dir = CopyResult(_w.CalPreflight.OutputDirectory!);
            EditIndex(dir, tamper);
            var o = FamilyOfficialReplay.Verify(Cal, dir, _w.Approvals, _w.Root, play: Good().Play);
            Assert.True(o.GamesExecuted);
            Assert.Contains(o.Error?.Code, new[] { "FamOfficial.evaluation_mismatch", "FamOfficial.unit_record_mismatch" });
        }
    }

    // ---------------------------------------------------------------- integration

    [Fact]
    public void The_integration_preflight_needs_a_declared_cohort_and_exactly_one_plan_per_mode()
    {
        var r = _w.IntPreflight;
        Assert.True(r.Ready, string.Join("; ", r.Checks.Where(c => c.Status != CheckStatus.Pass).Select(c => $"{c.Id} {c.Code} {c.Message}")));
        Assert.Equal(new[] { "automated", "non-automated" }, r.Units.Select(u => u.Pinned.Unit.Id));
        Assert.All(r.Units, u => Assert.Equal((100, (long?)null), (u.Pinned.Manifest.Seeds.Count, u.Pinned.Manifest.PaTarget)));

        Refused(Preflight(Int, _w.Approvals with { IntegrationCohortIds = null }), "unit_preflight", CheckStatus.NotReady, "Official.family_cohorts_not_declared");   // ADR-008 names none
        Refused(Preflight(Int, _w.Approvals with { IntegrationCohortIds = new HashSet<string> { CalibrationCohorts.VirtualLeagueId } }), "unit_preflight", CheckStatus.Fail, "Official.cohort_not_approved_for_family");
        var a = _w.Approvals;
        var pins = new Dictionary<string, string>(a.SeedManifestPins, StringComparer.Ordinal)
        {
            ["seeds/int/a2.json"] = DominanceOfficialWorld.Put(_w.Root, "seeds/int/a2.json", FamilyOfficialWorld.IntFile("a2", HarnessData.AutomatedProfile)),
        };
        Refused(Preflight(Int, a with { SeedManifestPins = pins }), "seed_manifests", CheckStatus.Fail, "Official.family_unit_duplicate");
        var noN = new Dictionary<string, string>(a.SeedManifestPins, StringComparer.Ordinal);
        noN.Remove(FamilyOfficialWorld.IntPath("n"));
        Refused(Preflight(Int, a with { SeedManifestPins = noN }), "seed_manifests", CheckStatus.Fail, "Official.family_units_missing");
        var short98 = SeedManifestTests.Manifest("fc3-int", "fc3-int-fx-s", 98, 7, 98, null, NeutralCohort.Id);
        var shortPins = new Dictionary<string, string>(a.SeedManifestPins, StringComparer.Ordinal) { ["seeds/int/s.json"] = DominanceOfficialWorld.Put(_w.Root, "seeds/int/s.json", short98) };
        Refused(Preflight(Int, a with { SeedManifestPins = shortPins }), "seed_manifests", CheckStatus.Fail, "Seed.integration_size_invalid");   // never a shortened plan
    }

    [Fact]
    public void Integration_runs_the_whole_plan_of_each_mode_on_the_real_engine_and_replays_independently()
    {
        var r = _w.IntResult;
        Assert.True(r.Complete, $"{r.Abort?.Code}: {r.Abort?.Message}");
        foreach (var (u, i) in r.Units.Select((u, i) => (u, i)))
        {
            Assert.Equal((false, 100, 100, (StopRecord?)null, RunResultText.V1, "official"), (u.Run.StopAware, u.Run.Requested, u.Run.Games.Count, u.Run.Stop, u.Run.ResultVersion, u.Run.Config.Mode));
            Assert.Equal(StratifiedEvaluator.Integration(u.Run).Gate, r.Evaluation!.Units[i].Gate);
            var manifest = JsonDocument.Parse(File.ReadAllBytes(UnitManifest(_w.IntPreflight.OutputDirectory!, Int, i))).RootElement;
            Assert.Null(H.Ok(ReplayCheck.Validate(manifest, _w.Approvals, _w.Root)).Stop);   // the existing full-execution official replay contract
        }

        Assert.Equal(r.Units.All(u => u.Run.AllCompleted && u.Run.MetricsComputed) ? GateStatus.Pass : GateStatus.Fail, r.Evaluation!.Gate);
        Assert.Equal(r.RunStatus == RunReport.StatusCompleted ? 0 : r.RunStatus == RunReport.StatusMetricsError ? 3 : 1, r.ExitCode);

        var outcome = FamilyOfficialReplay.Verify(Int, _w.IntPreflight.OutputDirectory!, _w.Approvals, _w.Root);   // the real engine again, from the pinned plan
        Assert.Null(outcome.Error);
        Assert.True(outcome.GamesExecuted);
        Assert.Equal(r.Evaluation.Canonical(), outcome.Evaluation!.Canonical());
    }

    [Fact]
    public void Integration_failures_and_metrics_errors_fail_the_gate_and_a_partial_run_is_never_a_complete_one()
    {
        GameRunResult FailedSeventh(GameSpec s) => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(70), s.GameIndex == 7 ? GameRunStatus.Failed : GameRunStatus.Completed);
        var failed = RunInt("int-failed", new Plays(FailedSeventh));
        Assert.Equal((true, RunReport.StatusGamesFailed, 1, GateStatus.Fail), (failed.Complete, failed.RunStatus, failed.ExitCode, failed.Evaluation!.Gate));
        Assert.All(failed.Units, u => Assert.Equal(100, u.Run.Games.Count));                  // the run goes on by the plan after a failure
        Assert.Null(FamilyOfficialReplay.Verify(Int, Out("int-failed"), _w.Approvals, _w.Root, play: new Plays(FailedSeventh).Play).Error);

        GameRunResult OnlyAutomated(GameSpec s) => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(70), s.GameId.Contains(":fc3-int-fx-a:", StringComparison.Ordinal) && s.GameIndex == 7 ? GameRunStatus.Failed : GameRunStatus.Completed);
        var one = RunInt("int-one-mode", new Plays(OnlyAutomated));
        Assert.Equal((GateStatus.Fail, GateStatus.Pass), (one.Evaluation!.Units[0].Gate, one.Evaluation.Units[1].Gate));
        Assert.Equal((GateStatus.Fail, RunReport.StatusGamesFailed), (one.Evaluation.Gate, one.RunStatus));                  // one failed mode is never compensated by the other

        var metrics = RunInt("int-metrics",new Plays(s => FamilyOfficialWorld.Fake(s, new MetricCounters { PlateAppearances = 70, Strikeouts = 71 })));
        Assert.Equal((RunReport.StatusMetricsError, 3, GateStatus.Fail), (metrics.RunStatus, metrics.ExitCode, metrics.Evaluation!.Gate));   // FC-D9.1: a metrics failure FAILs integration
        Assert.All(metrics.Evaluation.Units, u => Assert.Equal((GateStatus.Fail, "Metrics.counts_inconsistent"), (u.Gate, u.ErrorCode)));

        var good = new Plays(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(70)));
        var ok = RunInt("int-ok", good);
        Assert.Equal((RunReport.StatusCompleted, 0, GateStatus.Pass, 200), (ok.RunStatus, ok.ExitCode, ok.Evaluation!.Gate, good.Count));
        void Expect(string code, Action<string> tamper)
        {
            var dir = CopyResult(Out("int-ok"));
            tamper(dir);
            var plays = new Plays(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(70)));
            RefusedBeforeGames(FamilyOfficialReplay.Verify(Int, dir, _w.Approvals, _w.Root, play: plays.Play), code, plays);
        }

        Expect("FamOfficial.plan_not_completed", d => EditUnit(d, Int, 1, m => m["results"]!["games"]!.AsArray().RemoveAt(99)));   // a partial run presented as complete
        Expect("FamOfficial.plan_not_completed", d => EditUnit(d, Int, 0, m => m["results"]!["games_cancelled"] = 1));
        Expect("Replay.stop_not_allowed", d => EditUnit(d, Int, 0, m => m["results"]!["stop"] = new JsonObject { ["executed_games"] = 10, ["stop_reason"] = "TARGET_REACHED", ["cumulative_pa"] = 700, ["block_size"] = 10 }));
        Expect("Replay.seed_games_mismatch", d => EditUnit(d, Int, 0, m => m["games"]!.AsArray().RemoveAt(99)));
        Expect("FamOfficial.unit_record_mismatch", d => EditIndex(d, x => x["units"]![0]!["run_result_hash"] = new string('0', 64)));
        Expect("FamOfficial.unit_record_mismatch", d => EditIndex(d, x => x["units"]![1]!["completed_games"] = 99));
        Expect("FamOfficial.approval_record_mismatch", d => EditIndex(d, x => x["approvals"]!["integration_cohort_ids"] = new JsonArray("virtual-league-v1")));
        Expect("FamOfficial.family_mismatch", d => EditIndex(d, x => x["family"] = "fc3-cal"));
        var dir = CopyResult(Out("int-ok"));
        EditUnit(dir, Int, 0, m => m["results"]!["games"]![4]!["result_hash"] = new string('0', 64));          // a forged game hash: found by re-execution
        var after = FamilyOfficialReplay.Verify(Int, dir, _w.Approvals, _w.Root, play: new Plays(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(70))).Play);
        Assert.Equal(("FamOfficial.result_games_mismatch", true), (after.Error?.Code, after.GamesExecuted));
        var undeclared = new Plays(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(70)));          // the declaration is required at replay time too
        RefusedBeforeGames(FamilyOfficialReplay.Verify(Int, Out("int-ok"), _w.Approvals with { IntegrationCohortIds = null }, _w.Root, play: undeclared.Play), "FamOfficial.approval_record_mismatch", undeclared);

        var cancelled = RunInt("int-cancel", new Plays(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(70), s.GameIndex == 5 ? GameRunStatus.Cancelled : GameRunStatus.Completed)));
        Assert.Equal((DominanceRunTermination.Cancelled, 1, 5, false), (cancelled.Termination, cancelled.Units.Count, cancelled.Units[0].Run.Games.Count, cancelled.Complete));
        var none = Good();
        RefusedBeforeGames(FamilyOfficialReplay.Verify(Int, Out("int-cancel"), _w.Approvals, _w.Root, play: none.Play), "FamOfficial.run_not_complete", none);
    }

    // B6-02 (FIX1): results.pitches / results.plate_appearances of a full-execution manifest were never compared with the re-execution, so editing one together with the index's manifest_sha256 still MATCHed.
    [Fact]
    public void Integration_replay_compares_the_recorded_pitches_and_plate_appearances_of_every_mode_with_the_re_execution()
    {
        Plays Replayed() => new(s => FamilyOfficialWorld.Fake(s, FamilyOfficialWorld.Good(70)));
        var ok = RunInt("int-agg", Replayed());
        Assert.Equal((RunReport.StatusCompleted, 0), (ok.RunStatus, ok.ExitCode));
        var clean = Replayed();
        var match = FamilyOfficialReplay.Verify(Int, Out("int-agg"), _w.Approvals, _w.Root, play: clean.Play);
        Assert.Null(match.Error);                                                             // D: the untouched result still MATCHes
        Assert.Equal(200, clean.Count);

        var manifest0 = JsonDocument.Parse(File.ReadAllBytes(UnitManifest(Out("int-agg"), Int, 0))).RootElement.GetProperty("results");
        Assert.Equal((7_000L, 21_000L), (manifest0.GetProperty("plate_appearances").GetInt64(), manifest0.GetProperty("pitches").GetInt64()));   // 100 games x (70 PA, 210 pitches)

        foreach (var unit in new[] { 0, 1 })                                                    // A / B / C: either field of either mode, one at a time
            foreach (var field in new[] { "pitches", "plate_appearances" })
            {
                var dir = CopyResult(Out("int-agg"));
                EditUnit(dir, Int, unit, m => m["results"]![field] = m["results"]![field]!.GetValue<long>() + 1);     // EditUnit also rewrites the index's manifest_sha256: the file hash is consistent
                var plays = Replayed();
                var o = FamilyOfficialReplay.Verify(Int, dir, _w.Approvals, _w.Root, play: plays.Play);
                Assert.Equal(("FamOfficial.result_counts_mismatch", true), (o.Error?.Code, o.GamesExecuted));   // found by re-execution, not by the pre-game plan checks
                Assert.Contains(field, o.Error!.Value.Message, StringComparison.Ordinal);
                Assert.Equal((unit + 1) * 100, plays.Count);                                    // the whole plan of the tampered mode was re-run first
            }

        var both = CopyResult(Out("int-agg"));
        EditUnit(both, Int, 1, m => { m["results"]!["pitches"] = 1; m["results"]!["plate_appearances"] = 1; });
        Assert.Equal("FamOfficial.result_counts_mismatch", FamilyOfficialReplay.Verify(Int, both, _w.Approvals, _w.Root, play: Replayed().Play).Error?.Code);
    }

    [Fact]
    public void Integration_replay_on_the_real_engine_also_rejects_a_tampered_aggregate()
    {
        foreach (var field in new[] { "pitches", "plate_appearances" })
        {
            var dir = CopyResult(_w.IntPreflight.OutputDirectory!);
            EditUnit(dir, Int, 1, m => m["results"]![field] = m["results"]![field]!.GetValue<long>() - 1);      // the second mode only
            var o = FamilyOfficialReplay.Verify(Int, dir, _w.Approvals, _w.Root);
            Assert.Equal(("FamOfficial.result_counts_mismatch", true), (o.Error?.Code, o.GamesExecuted));
        }
    }

    // ---------------------------------------------------------------- output safety (OI-01 contract reused)

    [Fact]
    public void Two_ready_runs_with_one_run_id_never_share_an_output_and_existing_evidence_is_never_overwritten()
    {
        var first = Preflight(Cal, runId: "cal-race");
        var second = Preflight(Cal, runId: "cal-race");
        Assert.True(first.Ready && second.Ready);
        var a = Good();
        Assert.True(FamilyOfficial.Execute(first, "cal-race", _w.Approvals, _w.Root, play: a.Play).Complete);
        var before = Directory.GetFiles(Out("cal-race"), "*", SearchOption.AllDirectories).ToDictionary(f => f, FileSha);
        var b = Good();
        var late = FamilyOfficial.Execute(second, "cal-race", _w.Approvals, _w.Root, play: b.Play);
        Assert.Equal((DominanceRunTermination.AbortedIntegrity, "Official.output_exists", 0, 0), (late.Termination, late.Abort?.Code, late.Units.Count, b.Count));
        Assert.Equal(before, Directory.GetFiles(Out("cal-race"), "*", SearchOption.AllDirectories).ToDictionary(f => f, FileSha));
    }

    [Fact]
    public void A_parent_swapped_for_a_junction_after_the_preflight_never_redirects_the_family_output()
    {
        var root = CopyRoot();
        var outside = Path.Combine(_scratch, "outside-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outside);
        var report = Preflight(Int, root: root, runId: "int-junction");
        Assert.True(report.Ready);
        var link = Path.Combine(root, "build", "simharness", "official");
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in new[] { "/c", "mklink", "/J", link, outside })
            start.ArgumentList.Add(arg);
        using (var p = Process.Start(start)!)
            p.WaitForExit();
        Assert.True(File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint));
        var plays = Good();
        var result = FamilyOfficial.Execute(report, "int-junction", _w.Approvals, root, play: plays.Play);
        Assert.Equal((DominanceRunTermination.AbortedIntegrity, "Official.output_reparse_point", 0, 0), (result.Termination, result.Abort?.Code, result.Units.Count, plays.Count));
        Assert.Empty(Directory.GetFileSystemEntries(outside));
        Directory.Delete(link);                                                               // removes the junction only
    }

    private sealed record Child(int ExitCode, string Output, string Error);

    private static Child Spawn(params string[] arguments)
    {
        var host = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "") == "dotnet" ? Environment.ProcessPath! : "dotnet";
        var start = new ProcessStartInfo(host) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = H.Root };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (var a in arguments)
            start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000), "the harness process did not finish in 120 s");
        return new Child(process.ExitCode, output.Result, error.Result);
    }
}
