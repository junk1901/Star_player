using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// F-C2-A Parts D and E: the cohort / policy registries, the versioned official attestation, the official preflight (fail-closed; with the real approvals of F-C2-A it can only answer NOT_READY), the
/// official output directory rules, and replay through the registries with the legacy hashes unchanged. Approvals that make a preflight READY exist only as TEST fixtures with TEMP files.
/// </summary>
public sealed class OfficialAndReplayTests : IDisposable
{
    private const string Sha = "97045829d5836ff6dec8b40109c84d016d92c340";
    private const string LegacyNeutralInputsHash = "9343ad41d1e808c0afc2f47a01e8c9f54c1028656dcb6f75f44e48a21305cb70";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "simharness-tests", "official-" + Guid.NewGuid().ToString("N"));

    public OfficialAndReplayTests() => Directory.CreateDirectory(Path.Combine(_root, "seeds"));

    public void Dispose() => Directory.Delete(_root, true);

    private static string Fixture(string name) => Path.Combine(H.Root, "tests", "StarPlayer.SimHarness.Tests", "Fixtures", name);

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    // ---------------------------------------------------------------- registries

    [Fact]
    public void The_registries_resolve_only_implemented_ids_and_never_substitute_another_cohort()
    {
        var neutral = H.Ok(CohortRegistry.Resolve("neutral-v1"));
        Assert.Equal(LegacyNeutralInputsHash, neutral.InputsHash);                          // the same input hash as before the registry
        Assert.Equal("normal-balanced-v1", H.Ok(PolicyRegistry.Resolve("normal-balanced-v1")).Id);

        Assert.Equal("Registry.cohort_not_implemented", CohortRegistry.Resolve("neutral-opp-v1").Error!.Value.Code);   // the strategy-matrix cohort is F-C2-C; no placeholder now
        foreach (var built in new[] { "neutral-mixed-v1", "virtual-league-v1" })                                          // F-C2-B builds the two calibration cohorts (ADR-008 FC-D1 / FC-D2)
            Assert.Equal(built, H.Ok(CohortRegistry.Resolve(built)).CohortId);
        foreach (var unknown in new[] { "nope", "Neutral-v1", "neutral-v2", "", null })
            Assert.Equal("Registry.cohort_unknown", CohortRegistry.Resolve(unknown).Error!.Value.Code);
        foreach (var unknown in new[] { "patient-careful-v1", "Normal-balanced-v1", "", null })
            Assert.Equal("Registry.policy_unknown", PolicyRegistry.Resolve(unknown).Error!.Value.Code);
        Assert.Equal(new[] { "neutral-mixed-v1", "neutral-v1", "virtual-league-v1" }, CohortRegistry.Implemented.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "normal-balanced-v1" }, PolicyRegistry.Implemented.ToArray());
    }

    // ---------------------------------------------------------------- attestation

    private static OfficialAttestation Attestation() => new(
        OfficialAttestation.Version, Sha, OfficialProfile.Id, OfficialProfile.Hash(), "seeds/m.json", new string('a', 64), "fc3-dom", "fc3-dom-x", SeedManifestReader.GameIdRuleV1,
        "neutral-v1", LegacyNeutralInputsHash, "normal-balanced-v1", "prob-model-v4", "tuning-virtual-proto-v4", OfficialProfile.TuningHash, "test-pzl-automated", OfficialProfile.RulesHashes["test-pzl-automated"],
        "advancement-policy-v2", OfficialProfile.ParkText, 3000, new string('c', 64));

    [Fact]
    public void Every_attestation_field_is_bound_into_the_attestation_hash()
    {
        var a = Attestation();
        Assert.Equal(a.Hash(), Attestation().Hash());
        Assert.Matches("^[0-9a-f]{64}$", a.Hash());
        var node = JsonSerializer.SerializeToNode(a, RunReport.Json)!.AsObject();
        Assert.Equal(typeof(OfficialAttestation).GetProperties().Length, node.Count);           // every property is serialized ...
        foreach (var name in node.Select(p => p.Key).ToList())
        {
            var changed = JsonNode.Parse(node.ToJsonString())!.AsObject();
            changed[name] = changed[name]!.GetValueKind() == JsonValueKind.Number ? 2999 : changed[name]!.GetValue<string>() + "x";
            var other = H.Ok(OfficialAttestation.FromJson(Json(changed.ToJsonString())));
            Assert.True(other.Hash() != a.Hash(), $"{name} is not bound into the attestation hash");   // ... and every one changes the hash
        }
    }

    [Fact]
    public void A_malformed_attestation_is_refused()
    {
        var node = JsonSerializer.SerializeToNode(Attestation(), RunReport.Json)!.AsObject();
        Assert.True(OfficialAttestation.FromJson(Json(node.ToJsonString())).IsSuccess);
        Assert.Equal("Official.attestation_invalid", OfficialAttestation.FromJson(Json(new JsonObject(node.Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())).Append(KeyValuePair.Create("extra", (JsonNode?)1))).ToJsonString())).Error!.Value.Code);
        foreach (var name in new[] { "git_commit_sha", "config_hash", "seed_manifest_sha256" })
        {
            var missing = JsonNode.Parse(node.ToJsonString())!.AsObject();
            missing.Remove(name);
            Assert.Equal("Official.attestation_invalid", OfficialAttestation.FromJson(Json(missing.ToJsonString())).Error!.Value.Code);
            var empty = JsonNode.Parse(node.ToJsonString())!.AsObject();
            empty[name] = "";
            Assert.Equal("Official.attestation_invalid", OfficialAttestation.FromJson(Json(empty.ToJsonString())).Error!.Value.Code);
        }
    }

    [Fact]
    public void The_official_profile_is_the_ADR_008_fixed_run_profile()
    {
        Assert.Equal("5011f8acaae4848481db6bc60f636d7594b2a30fcdf594aeab375f0aa98b8fd8", OfficialProfile.TuningHash);
        Assert.Equal(OfficialProfile.TuningHash, H.Tuning.ContentHash);
        Assert.Equal(H.Rules(true).ContentHash, OfficialProfile.RulesHashes["test-pzl-automated"]);
        Assert.Equal(H.Rules().ContentHash, OfficialProfile.RulesHashes["test-pzl-non-automated"]);
        Assert.Equal(2, OfficialProfile.RulesHashes.Count);
        Assert.Equal(("prob-model-v4", "tuning-virtual-proto-v4", "advancement-policy-v2", 3000), (OfficialProfile.ModelVersion, OfficialProfile.TuningVersion, OfficialProfile.AdvancementPolicyVersion, OfficialProfile.EventBudget));
        // nothing official is frozen in F-C2-A
        var current = OfficialApprovals.Current;
        Assert.Null(current.GitCommitSha);
        Assert.Null(current.ExecutionPolicyId);
        Assert.Null(current.ExecutionPolicyHash);
        Assert.Empty(current.SeedManifestPins);
        Assert.Empty(current.CohortInputsHashes);
        Assert.False(current.ExecutionPathReady);
    }

    // ---------------------------------------------------------------- preflight

    private (string Rel, string Sha) PutManifest(JsonObject m, string name = "m.json")
    {
        var bytes = Encoding.UTF8.GetBytes(m.ToJsonString());
        File.WriteAllBytes(Path.Combine(_root, "seeds", name), bytes);
        return ("seeds/" + name, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static JsonObject DomManifest(string cohort = "neutral-v1", string rules = "test-pzl-automated", string family = "fc3-dom")
    {
        var m = SeedManifestTests.Manifest(family, family + "-x", cohort: cohort);
        m["rules_profile_id"] = rules;
        return m;
    }

    /// <summary>TEST-ONLY approvals that pin a TEMP manifest: the only way any preflight can be READY in F-C2-A.</summary>
    private static OfficialApprovals FixtureApprovals(string rel, string sha) =>
        new(Sha, OfficialProfile.Id, OfficialProfile.Hash(), new Dictionary<string, string> { [rel] = sha }, new Dictionary<string, string> { ["neutral-v1"] = LegacyNeutralInputsHash }, true);

    private static GitIntegrityReport CleanGit(string? head = Sha) => GitIntegrityPolicy.Evaluate([], head);

    private PreflightReport Preflight(string rel, OfficialApprovals approvals, GitIntegrityReport? git = null, string? @out = null, string runId = "official-test") =>
        OfficialPreflight.Run(new OfficialRequest(_root, rel, runId, @out), approvals, git ?? CleanGit());

    private static PreflightCheck Check(PreflightReport r, string id) => r.Checks.Single(c => c.Id == id);

    [Fact]
    public void With_the_real_F_C2A_approvals_the_preflight_is_never_ready()
    {
        var (rel, _) = PutManifest(DomManifest());
        var report = Preflight(rel, OfficialApprovals.Current);

        Assert.False(report.Ready);
        Assert.Null(report.Attestation);
        Assert.Equal(CheckStatus.NotReady, Check(report, "git_sha").Status);
        Assert.Equal("Official.git_sha_not_approved", Check(report, "git_sha").Code);
        Assert.Equal("Official.seed_manifest_not_frozen", Check(report, "seed_manifest").Code);     // an unpinned file is not even read
        Assert.Null(report.Manifest);
        Assert.Equal("Official.execution_policy_not_frozen", Check(report, "execution_policy").Code);
        Assert.Equal("Official.not_ready", Check(report, "execution_path").Code);
        Assert.Equal(CheckStatus.Pass, Check(report, "model_tuning").Status);                     // the fixed model / tuning is genuinely the approved one
        Assert.Equal("Official.not_ready", report.ErrorCode);
        Assert.False(Directory.Exists(Path.Combine(_root, "build")));                              // a preflight writes nothing
    }

    [Fact]
    public void Only_fully_pinned_test_approvals_make_a_preflight_ready_and_its_attestation_names_every_input()
    {
        var (rel, sha) = PutManifest(DomManifest());
        var report = Preflight(rel, FixtureApprovals(rel, sha));

        Assert.True(report.Ready, string.Join("; ", report.Checks.Where(c => c.Status != CheckStatus.Pass).Select(c => $"{c.Id} {c.Code} {c.Message}")));
        Assert.Null(report.ErrorCode);
        var a = report.Attestation!;
        Assert.Equal((Sha, rel, sha, "fc3-dom", "fc3-dom-x"), (a.GitCommitSha, a.SeedManifestPath, a.SeedManifestSha256, a.SeedFamily, a.CompetitionKey));
        Assert.Equal(("neutral-v1", LegacyNeutralInputsHash, "normal-balanced-v1", "test-pzl-automated"), (a.CohortId, a.CohortInputsHash, a.PolicyId, a.RulesProfileId));
        Assert.Equal((OfficialProfile.Id, OfficialProfile.Hash()), (a.ExecutionPolicyId, a.ExecutionPolicyHash));
        // the config hash is the EXISTING ConfigHash of the run the manifest describes (unchanged formula), bound into the new attestation
        var config = H.Ok(RunConfig.Create("official-test", "official", NeutralCohort.Create(), H.Rules(true), H.Tuning, H.Park(), "advancement-policy-v2", 3000, competitionKey: "fc3-dom-x"));
        Assert.Equal(config.ConfigHash(report.Manifest!.Seeds.Select(s => config.Spec(s.Index, s.Seed))), a.ConfigHash);
        Assert.Equal(Path.Combine(_root, "build", "simharness", "official", "official-test"), report.OutputDirectory);
        Assert.False(Directory.Exists(report.OutputDirectory));
    }

    [Fact]
    public void Each_unmet_condition_fails_closed_on_its_own_check()
    {
        var (rel, sha) = PutManifest(DomManifest());
        var ok = FixtureApprovals(rel, sha);

        void Refused(PreflightReport r, string id, CheckStatus status, string code)
        {
            Assert.False(r.Ready);
            Assert.Null(r.Attestation);
            Assert.Equal((status, code), (Check(r, id).Status, Check(r, id).Code));
        }

        Refused(Preflight(rel, ok, GitIntegrityPolicy.Evaluate(H.Ok(GitStatusParser.Parse(" M src/a.cs\0")), Sha)), "git_integrity", CheckStatus.Fail, "Git.dirty");
        Refused(Preflight(rel, ok, GitIntegrityPolicy.Evaluate(H.Ok(GitStatusParser.Parse("!! src/P/bin/p.dll\0")), Sha)), "git_integrity", CheckStatus.NotReady, "Git.build_output_unverified");
        Refused(Preflight(rel, ok, new GitIntegrityReport(GitIntegrityState.Error, "Git.query_failed", "x", null, [], [])), "git_integrity", CheckStatus.Fail, "Git.query_failed");
        Refused(Preflight(rel, ok, CleanGit(new string('1', 40))), "git_sha", CheckStatus.Fail, "Official.git_sha_mismatch");
        Refused(Preflight(rel, ok with { SeedManifestPins = new Dictionary<string, string> { [rel] = new string('0', 64) } }), "seed_manifest", CheckStatus.Fail, "Seed.file_hash_mismatch");
        Refused(Preflight("../m.json", ok), "seed_manifest", CheckStatus.Fail, "Seed.path_invalid");
        Refused(Preflight(rel, ok with { CohortInputsHashes = new Dictionary<string, string> { ["neutral-v1"] = new string('9', 64) } }), "cohort_hash", CheckStatus.Fail, "Official.cohort_hash_mismatch");
        Refused(Preflight(rel, ok with { CohortInputsHashes = new Dictionary<string, string>() }), "cohort_hash", CheckStatus.NotReady, "Official.cohort_hash_not_frozen");
        Refused(Preflight(rel, ok with { ExecutionPolicyHash = new string('e', 64) }), "execution_policy", CheckStatus.Fail, "Official.execution_policy_mismatch");
        Refused(Preflight(rel, ok with { ExecutionPathReady = false }), "execution_path", CheckStatus.NotReady, "Official.not_ready");
        Refused(Preflight(rel, ok, @out: "../escape"), "output_directory", CheckStatus.Fail, "Official.output_invalid");

        var (dry, drySha) = PutManifest(DomManifest(family: "fc2-dry"), "dry.json");
        Refused(Preflight(dry, FixtureApprovals(dry, drySha)), "family", CheckStatus.Fail, "Official.family_not_official");
        var (rules, rulesSha) = PutManifest(DomManifest(rules: "test-one-inning-tie"), "rules.json");
        Refused(Preflight(rules, FixtureApprovals(rules, rulesSha)), "rules", CheckStatus.Fail, "Official.rules_not_approved");
        var (opp, oppSha) = PutManifest(DomManifest(cohort: "neutral-opp-v1"), "opp.json");
        Refused(Preflight(opp, FixtureApprovals(opp, oppSha)), "cohort", CheckStatus.NotReady, "Registry.cohort_not_implemented");
        var (mixed, mixedSha) = PutManifest(DomManifest(cohort: "neutral-mixed-v1"), "mixed.json");
        Refused(Preflight(mixed, FixtureApprovals(mixed, mixedSha)), "cohort", CheckStatus.Fail, "Official.cohort_not_approved_for_family");   // FC-D4: the matrix runs on neutral-v1 / neutral-opp-v1
        var cal = DomManifest(family: "fc3-cal");
        cal["pa_target"] = 100_000;
        var (calRel, calSha) = PutManifest(cal, "cal.json");
        Refused(Preflight(calRel, FixtureApprovals(calRel, calSha)), "cohort", CheckStatus.Fail, "Official.cohort_not_approved_for_family");  // FC-D7: calibration is neutral-mixed-v1 / virtual-league-v1
        var policy = DomManifest();
        policy["policy_id"] = "aggressive-attack-v1";
        var (pRel, pSha) = PutManifest(policy, "policy.json");
        Refused(Preflight(pRel, FixtureApprovals(pRel, pSha)), "policy", CheckStatus.Fail, "Registry.policy_unknown");
    }

    // ---------------------------------------------------------------- output directory

    [Theory]
    [InlineData("../escape")]
    [InlineData("build/simharness/official/../../../escape")]
    [InlineData("build/simharness/official")]                    // the root itself
    [InlineData("build/simharness/smoke-run")]                   // smoke output is separate
    [InlineData("src/StarPlayer.Domain")]                       // never into tracked source
    [InlineData("C:/elsewhere")]
    [InlineData("/elsewhere")]
    [InlineData("build\\simharness\\official\\x")]
    [InlineData("build/simharness/official/a//b")]
    [InlineData("")]
    public void Official_output_must_be_a_new_directory_under_the_official_root(string @out) =>
        Assert.Equal("Official.output_invalid", OfficialOutput.Resolve(_root, @out, "r1").Error!.Value.Code);

    [Fact]
    public void Official_output_is_never_written_over_an_existing_path()
    {
        Assert.Equal(Path.Combine(_root, "build", "simharness", "official", "r1"), H.Ok(OfficialOutput.Resolve(_root, null, "r1")));
        Assert.Equal(Path.Combine(_root, "build", "simharness", "official", "a", "b"), H.Ok(OfficialOutput.Resolve(_root, "build/simharness/official/a/b", "r1")));
        Directory.CreateDirectory(Path.Combine(_root, "build", "simharness", "official", "r1"));
        Assert.Equal("Official.output_exists", OfficialOutput.Resolve(_root, null, "r1").Error!.Value.Code);
        File.WriteAllText(Path.Combine(_root, "build", "simharness", "official", "f"), "");
        Assert.Equal("Official.output_exists", OfficialOutput.Resolve(_root, "build/simharness/official/f", "r1").Error!.Value.Code);
        Assert.Equal("Official.output_invalid", OfficialOutput.Resolve(_root, null, "..").Error!.Value.Code);
        Assert.Equal("Official.output_invalid", OfficialOutput.Resolve(_root, null, "a/b").Error!.Value.Code);
    }

    // ---------------------------------------------------------------- the official command

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

    [Fact]
    public void The_official_command_refuses_to_start_and_reports_not_ready_without_a_gate_verdict()
    {
        var runId = "fc2a-refusal-" + Guid.NewGuid().ToString("N")[..8];
        var child = Spawn("official", "--seed-manifest", "content/seeds/none.json", "--run-id", runId);

        Assert.Equal(2, child.ExitCode);
        var result = Json(child.Output);
        Assert.Equal(("simharness-official-result-v1", "official", true), (result.GetProperty("schema_version").GetString(), result.GetProperty("run_type").GetString(), result.GetProperty("official").GetBoolean()));
        Assert.Equal("PREFLIGHT_REFUSED", result.GetProperty("run_status").GetString());
        Assert.Equal("NOT_EVALUATED", result.GetProperty("gate_status").GetString());              // exit code and the statistical gate are separate (FC-D12)
        Assert.False(string.IsNullOrEmpty(result.GetProperty("error_code").GetString()));
        foreach (var name in new[] { "execution_policy_id", "execution_policy_hash", "cohort_hash", "seed_manifest_hash", "config_hash", "attestation_hash" })
            Assert.Equal(JsonValueKind.Null, result.GetProperty(name).ValueKind);                    // nothing unapproved is filled in
        Assert.Equal("prob-model-v4", result.GetProperty("model_version").GetString());
        Assert.Contains(result.GetProperty("checks").EnumerateArray(), c => c.GetProperty("code").GetString() == "Official.not_ready");
        Assert.DoesNotContain("games:", child.Output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(H.Root, "build", "simharness", "official", runId)));
        Assert.Equal(2, Program.Main(["official", "--seed-manifest", "content/seeds/none.json", "--run-id", runId]));
        Assert.Equal(new[] { "NOT_EVALUATED", "INSUFFICIENT_SAMPLE", "PASS", "FAIL" }, OfficialOutput.GateStatuses.ToArray());
    }

    // ---------------------------------------------------------------- replay through the registries

    [Theory]
    [InlineData("legacy-manifest-neutral-default.json", "28e0536c644d6a38b4eefb4f673423229b35725ef87f7fa51a39e476cd552ec6")]
    [InlineData("legacy-manifest-automated-v1.json", "9e991e896d1bb4761e183644aeda01a64195ebc6cd8c1de857bc617bba7a8903")]
    public void Manifests_written_before_F_C2A_still_replay_to_their_recorded_hashes(string file, string runResultHash)
    {
        var root = Json(File.ReadAllText(Fixture(file)));
        Assert.Equal(runResultHash, root.GetProperty("results").GetProperty("run_result_hash").GetString());
        var plan = H.Ok(ReplayCheck.Validate(root, OfficialApprovals.Current));
        Assert.Equal("neutral-v1", plan.Cohort.CohortId);
        Assert.Equal(0, Program.Main(["replay", "--manifest", Fixture(file)]));
    }

    private string Tampered(string file, Action<JsonObject> edit)
    {
        var node = JsonNode.Parse(File.ReadAllText(Fixture(file)))!.AsObject();
        edit(node);
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, node.ToJsonString());
        return path;
    }

    private string ReplayCode(Action<JsonObject> edit)
    {
        var path = Tampered("legacy-manifest-neutral-default.json", edit);
        Assert.Equal(2, Program.Main(["replay", "--manifest", path]));
        var check = ReplayCheck.Validate(Json(File.ReadAllText(path)), OfficialApprovals.Current);
        Assert.False(check.IsSuccess);
        return check.Error!.Value.Code;
    }

    [Fact]
    public void Replay_refuses_unregistered_or_mismatched_inputs_instead_of_falling_back()
    {
        Assert.Equal("Registry.cohort_unknown", ReplayCode(m => m["run"]!["cohort_id"] = "neutral-v9"));
        Assert.Equal("Registry.cohort_not_implemented", ReplayCode(m => m["run"]!["cohort_id"] = "neutral-opp-v1"));
        Assert.Equal("Replay.cohort_hash_mismatch", ReplayCode(m => m["run"]!["cohort_id"] = "neutral-mixed-v1"));      // F-C2-B: now built, so the recorded neutral-v1 hash no longer matches it
        Assert.Equal("Replay.cohort_hash_mismatch", ReplayCode(m => m["engine"]!["cohort_inputs_hash"] = new string('0', 64)));
        Assert.Equal("Registry.policy_unknown", ReplayCode(m => m["engine"]!["policy_id"] = "patient-careful-v1"));
        Assert.Equal("Replay.schema_unsupported", ReplayCode(m => m["manifest_version"] = "simharness-manifest-v2"));
        Assert.Equal("Replay.schema_unsupported", ReplayCode(m => m["run"]!["input_schema_version"] = "simharness-input-v2"));
        Assert.Equal("Replay.rules_unknown", ReplayCode(m => m["engine"]!["rules_profile_id"] = "test-pzl-bogus"));                  // used to replay as non-automated
        Assert.Equal("Replay.advancement_unknown", ReplayCode(m => m["engine"]!["advancement_policy_version"] = "advancement-policy-v9"));   // used to replay as v2
        Assert.Equal("Replay.test_tuning_not_replayable", ReplayCode(m => m["engine"]!["tuning_file_sha256"] = new string('f', 64)));
        Assert.Equal("Replay.manifest_invalid", ReplayCode(m => m.Remove("engine")));
    }

    [Fact]
    public void An_official_manifest_is_never_replayed_as_official_while_nothing_is_frozen()
    {
        var a = Attestation();
        var config = Json(File.ReadAllText(Fixture("legacy-manifest-neutral-default.json"))).GetProperty("run").GetProperty("config_hash").GetString()!;
        var bound = a with { ConfigHash = config };
        JsonNode Envelope(OfficialAttestation att, string? hash = null) => JsonSerializer.SerializeToNode(new { schema_version = OfficialAttestation.EnvelopeVersion, attestation = att, attestation_hash = hash ?? att.Hash() }, RunReport.Json)!;
        string ReplayOfficialCode(Action<JsonObject> edit) => ReplayCode(m => { m["run"]!["mode"] = "official"; edit(m); });       // N-01: an envelope is only read on an official-mode manifest

        Assert.Equal("Replay.official_mode_mismatch", ReplayCode(m => m["official"] = Envelope(bound)));                          // the legacy fixture's mode is "smoke"

        Assert.Equal("Official.seed_manifest_not_frozen", ReplayOfficialCode(m => m["official"] = Envelope(bound)));                       // a correct envelope, but nothing is pinned
        Assert.Equal("Replay.attestation_mismatch", ReplayOfficialCode(m => m["official"] = Envelope(bound, new string('0', 64))));
        Assert.Equal("Replay.attestation_mismatch", ReplayOfficialCode(m => { m["official"] = Envelope(bound); m["official"]!["attestation"]!["cohort_id"] = "neutral-v2"; }));
        Assert.Equal("Replay.attestation_config_mismatch", ReplayOfficialCode(m => m["official"] = Envelope(a)));
        Assert.Equal("Replay.official_schema_unsupported", ReplayOfficialCode(m => { m["official"] = Envelope(bound); m["official"]!["schema_version"] = "simharness-official-v0"; }));
        Assert.Equal("Official.attestation_invalid", ReplayOfficialCode(m => { m["official"] = Envelope(bound); m["official"]!["attestation"]!.AsObject().Remove("policy_id"); }));
    }

    [Fact]
    public void An_official_envelope_must_also_agree_with_the_manifest_it_is_attached_to()
    {
        var node = JsonNode.Parse(File.ReadAllText(Fixture("legacy-manifest-neutral-default.json")))!.AsObject();
        var config = node["run"]!["config_hash"]!.GetValue<string>();
        var a = new OfficialAttestation(OfficialAttestation.Version, Sha, OfficialProfile.Id, OfficialProfile.Hash(), "seeds/m.json", new string('a', 64), "fc3-dom", "sim", SeedManifestReader.GameIdRuleV1,
            "neutral-v1", LegacyNeutralInputsHash, "normal-balanced-v1", "prob-model-v4", "tuning-virtual-proto-v4", OfficialProfile.TuningHash, "test-pzl-non-automated",
            OfficialProfile.RulesHashes["test-pzl-non-automated"], "advancement-policy-v2", OfficialProfile.ParkText, 3000, config);
        var approvals = new OfficialApprovals(Sha, OfficialProfile.Id, OfficialProfile.Hash(), new Dictionary<string, string> { ["seeds/m.json"] = new string('a', 64) },
            new Dictionary<string, string> { ["neutral-v1"] = LegacyNeutralInputsHash }, true);
        JsonElement With(OfficialAttestation att)
        {
            var copy = JsonNode.Parse(node.ToJsonString())!.AsObject();
            copy["official"] = JsonSerializer.SerializeToNode(new { schema_version = OfficialAttestation.EnvelopeVersion, attestation = att, attestation_hash = att.Hash() }, RunReport.Json);
            copy["run"]!["mode"] = "official";                                                                                  // N-01: an envelope requires mode "official"
            return Json(copy.ToJsonString());
        }

        // Codex F-02: a matching hash STRING used to be enough (this line asserted success with no seed file at all). The pinned file must now exist and its parse must bind the games:
        // `seeds/m.json` is not in the TEMP root here, so the replay is refused; the accepted case with a real file is CodexFindingsTests.A_consistent_official_world_*.
        Assert.Equal("Seed.file_missing", ReplayCheck.Validate(With(a), approvals, _root).Error!.Value.Code);               // TEST approvals only
        Assert.Equal("Replay.official_seed_manifest_unavailable", ReplayCheck.Validate(With(a), approvals).Error!.Value.Code);   // no root, no verification
        Assert.Equal("Replay.attestation_manifest_mismatch", ReplayCheck.Validate(With(a with { CompetitionKey = "fc3-dom-x" }), approvals).Error!.Value.Code);
        Assert.Equal("Replay.attestation_manifest_mismatch", ReplayCheck.Validate(With(a with { RulesProfileId = "test-pzl-automated" }), approvals).Error!.Value.Code);
        Assert.Equal("Replay.attestation_manifest_mismatch", ReplayCheck.Validate(With(a with { EventBudget = 2999 }), approvals).Error!.Value.Code);
        Assert.Equal("Official.seed_manifest_not_frozen", ReplayCheck.Validate(With(a), OfficialApprovals.Current).Error!.Value.Code);   // the real approvals refuse it
    }

    [Fact]
    public void Official_replay_checks_every_pinned_value_against_the_approvals()
    {
        var a = Attestation();
        var approvals = new OfficialApprovals(Sha, OfficialProfile.Id, OfficialProfile.Hash(), new Dictionary<string, string> { [a.SeedManifestPath] = a.SeedManifestSha256 },
            new Dictionary<string, string> { ["neutral-v1"] = LegacyNeutralInputsHash }, true);
        JsonElement Envelope(OfficialAttestation att) => JsonSerializer.SerializeToElement(new { schema_version = OfficialAttestation.EnvelopeVersion, attestation = att, attestation_hash = att.Hash() }, RunReport.Json);

        Assert.Null(OfficialReplay.Verify(Envelope(a), a.ConfigHash, approvals));
        Assert.Equal("Replay.seed_manifest_hash_mismatch", OfficialReplay.Verify(Envelope(a with { SeedManifestSha256 = new string('b', 64) }), a.ConfigHash, approvals)!.Value.Code);
        Assert.Equal("Replay.execution_policy_mismatch", OfficialReplay.Verify(Envelope(a with { ExecutionPolicyHash = new string('b', 64) }), a.ConfigHash, approvals)!.Value.Code);
        Assert.Equal("Replay.cohort_hash_mismatch", OfficialReplay.Verify(Envelope(a with { CohortInputsHash = new string('b', 64) }), a.ConfigHash, approvals)!.Value.Code);
        Assert.Equal("Replay.git_sha_mismatch", OfficialReplay.Verify(Envelope(a with { GitCommitSha = new string('1', 40) }), a.ConfigHash, approvals)!.Value.Code);
        Assert.Equal("Official.not_ready", OfficialReplay.Verify(Envelope(a), a.ConfigHash, approvals with { ExecutionPathReady = false })!.Value.Code);
    }
}
