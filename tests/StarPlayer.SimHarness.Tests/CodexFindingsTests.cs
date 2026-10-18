using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// Codex review findings of F-C2-A. F-01: a replay checks the RECORDED index / game id / seed of every game against the run configuration instead of regenerating them. F-02: an official replay is
/// accepted only when a trusted approval pins a REAL seed file whose parsed game list equals the games the replay would play, and every attestation field agrees with that file, the approved
/// profile and the manifest (a self-consistent attestation hash proves nothing). F-03: a reparse point (junction / symlink) on the seed or output path is refused. Re-review N-01: an official-family
/// manifest cannot be replayed as a legacy one; N-02: a link above the root is refused too; N-03: a symlink test that could not run is SKIPPED as NOT_RUN, never PASS. All inputs are TEMP copies.
/// </summary>
public sealed class CodexFindingsTests : IDisposable
{
    private const string Sha = "97045829d5836ff6dec8b40109c84d016d92c340";
    private const string NeutralInputsHash = "9343ad41d1e808c0afc2f47a01e8c9f54c1028656dcb6f75f44e48a21305cb70";
    private const string Legacy = "legacy-manifest-neutral-default.json";
    private const string Key = "fc3-dom-x";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "simharness-tests", "codex-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _links = [];

    public CodexFindingsTests() => Directory.CreateDirectory(Path.Combine(_root, "seeds"));

    public void Dispose()
    {
        foreach (var link in _links)
            if (Directory.Exists(link))
                Directory.Delete(link);                                                           // removes the link only, never its target
        Directory.Delete(_root, true);
    }

    private static string Fixture(string name) => Path.Combine(H.Root, "tests", "StarPlayer.SimHarness.Tests", "Fixtures", name);

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static ulong U(JsonNode? n) => ulong.Parse(n!.ToJsonString());

    private static JsonObject LegacyNode() => JsonNode.Parse(File.ReadAllText(Fixture(Legacy)))!.AsObject();

    // ---------------------------------------------------------------- F-01 replay game identity

    private string Write(JsonObject node)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, node.ToJsonString());
        return path;
    }

    /// <summary>The refusal code of a tampered legacy manifest, the same refusal at the command line, and proof that no game ran.</summary>
    private string IdentityRefusal(Action<JsonObject> edit)
    {
        var node = LegacyNode();
        edit(node);
        var path = Write(node);
        Assert.Equal(2, Program.Main(["replay", "--manifest", path]));
        var check = ReplayCheck.Validate(Json(node.ToJsonString()), OfficialApprovals.Current);
        Assert.False(check.IsSuccess);
        return check.Error!.Value.Code;
    }

    private static JsonObject Game(JsonObject node, int i) => node["games"]![i]!.AsObject();

    [Fact]
    public void R1_to_R8_a_replay_checks_the_recorded_index_game_id_and_seed()
    {
        Assert.Equal("Replay.game_index_mismatch", IdentityRefusal(m => Game(m, 0)["index"] = 5));                                  // R1
        Assert.Equal("Replay.game_identity_duplicate", IdentityRefusal(m => Game(m, 1)["game_id"] = Game(m, 0)["game_id"]!.GetValue<string>()));   // R2
        Assert.Equal("Replay.game_id_mismatch", IdentityRefusal(m => Game(m, 0)["game_id"] = "game:sim:neutral-v1-000099"));        // R3: a well-formed id that is not the rule's
        Assert.Equal("Replay.game_identity_duplicate", IdentityRefusal(m => Game(m, 1)["index"] = 1));                              // R4
        Assert.Equal("Replay.game_index_mismatch", IdentityRefusal(m => { Game(m, 0)["index"] = 2; Game(m, 1)["index"] = 1; }));     // R5
        Assert.Equal("Replay.game_index_mismatch", IdentityRefusal(m =>                                                              // R6: the games array in another order
        {
            var games = m["games"]!.AsArray();
            var first = games[0]!.DeepClone();
            games[0] = games[1]!.DeepClone();
            games[1] = first;
        }));
        Assert.Equal("Replay.game_id_mismatch", IdentityRefusal(m => { Game(m, 0)["game_id"] = "game:sim:neutral-v1-000042"; Game(m, 0)["seed"] = 4; }));   // R8
        Assert.Equal("Replay.game_list_mismatch", IdentityRefusal(m => m["games"] = new JsonArray()));
        Assert.Equal("Replay.game_id_mismatch", IdentityRefusal(m => Game(m, 2)["game_id"] = "game:other:neutral-v1-000003"));   // another competition key's id is not this run's
    }

    [Fact]
    public void R7_a_changed_seed_is_refused_before_any_game_runs_and_a_recorded_id_is_never_regenerated()
    {
        var node = LegacyNode();
        Game(node, 0)["seed"] = 4;
        var path = Write(node);
        var plan = H.Ok(ReplayCheck.Validate(Json(node.ToJsonString()), OfficialApprovals.Current));       // identity is fine ...
        Assert.Equal("Replay.config_hash_mismatch", plan.CheckAgainst(H.Config("base"))!.Value.Code);          // ... the recorded config hash is not the one these games make
        Assert.Equal(2, Program.Main(["replay", "--manifest", path]));
        Assert.Equal(new[] { 4UL, 11UL, 29UL }, plan.Games.Select(g => g.Seed).ToArray());                   // the games are the recorded ones, in the recorded order
        Assert.Equal(new[] { "game:sim:neutral-v1-000001", "game:sim:neutral-v1-000002", "game:sim:neutral-v1-000003" }, plan.Games.Select(g => g.GameId).ToArray());
    }

    [Fact]
    public void The_untouched_legacy_manifests_still_check_out_against_their_configuration()
    {
        foreach (var file in new[] { Legacy, "legacy-manifest-automated-v1.json" })
        {
            var root = Json(File.ReadAllText(Fixture(file)));
            var plan = H.Ok(ReplayCheck.Validate(root, OfficialApprovals.Current));
            var automated = file.Contains("automated", StringComparison.Ordinal);
            var config = H.Config("base", rules: H.Rules(automated), advancement: root.GetProperty("engine").GetProperty("advancement_policy_version").GetString());
            Assert.Null(plan.CheckAgainst(config));
            Assert.Equal(root.GetProperty("games").GetArrayLength(), plan.Games.Count);
        }
    }

    // ---------------------------------------------------------------- F-02 official replay: the trusted approval -> the real seed file -> the games

    private sealed record Setup(string Rel, string SeedSha, JsonObject Seed, JsonObject Manifest, OfficialAttestation Att, OfficialApprovals Approvals);

    private static OfficialApprovals Approvals(string rel, string sha) =>
        new(Sha, OfficialProfile.Id, OfficialProfile.Hash(), new Dictionary<string, string> { [rel] = sha }, new Dictionary<string, string> { ["neutral-v1"] = NeutralInputsHash }, true);

    private static string ConfigHashOf(string key, IEnumerable<(int Index, ulong Seed)> games)
    {
        var config = H.Ok(RunConfig.Create("official-test", "official", NeutralCohort.Create(), H.Rules(true), H.Tuning, H.Park(), "advancement-policy-v2", 3000, competitionKey: key));
        return config.ConfigHash(games.Select(g => config.Spec(g.Index, g.Seed)));
    }

    private static JsonObject Envelope(OfficialAttestation att) =>
        JsonSerializer.SerializeToNode(new { schema_version = OfficialAttestation.EnvelopeVersion, attestation = att, attestation_hash = att.Hash() }, RunReport.Json)!.AsObject();

    /// <summary>A consistent official world: a pinned TEMP seed file, the manifest of a run on exactly those games, and an attestation that is right about all of it.</summary>
    private Setup Consistent()
    {
        var seed = SeedManifestTests.Manifest("fc3-dom", Key);
        seed["rules_profile_id"] = "test-pzl-automated";
        var bytes = Encoding.UTF8.GetBytes(seed.ToJsonString());
        File.WriteAllBytes(Path.Combine(_root, "seeds", "m.json"), bytes);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var games = seed["seeds"]!.AsArray().Select(s => (s!["index"]!.GetValue<int>(), U(s["seed"]))).ToList();
        var config = ConfigHashOf(Key, games);

        var m = LegacyNode();
        m["run"]!["mode"] = "official";
        m["run"]!["config_hash"] = config;
        m["engine"]!["competition_key"] = Key;
        m["engine"]!["rules_profile_id"] = "test-pzl-automated";
        m["engine"]!["rules_hash"] = OfficialProfile.RulesHashes["test-pzl-automated"];
        m["games"] = new JsonArray(games.Select(g => (JsonNode)new JsonObject { ["index"] = g.Item1, ["game_id"] = $"game:{Key}:neutral-v1-{g.Item1:D6}", ["seed"] = g.Item2 }).ToArray());
        var att = new OfficialAttestation(OfficialAttestation.Version, Sha, OfficialProfile.Id, OfficialProfile.Hash(), "seeds/m.json", sha, "fc3-dom", Key, SeedManifestReader.GameIdRuleV1, "neutral-v1",
            NeutralInputsHash, "normal-balanced-v1", "prob-model-v4", "tuning-virtual-proto-v4", OfficialProfile.TuningHash, "test-pzl-automated", OfficialProfile.RulesHashes["test-pzl-automated"],
            "advancement-policy-v2", OfficialProfile.ParkText, 3000, config);
        m["official"] = Envelope(att);
        return new Setup("seeds/m.json", sha, seed, m, att, Approvals("seeds/m.json", sha));
    }

    private string OfficialCode(Setup s, Action<JsonObject>? edit = null, OfficialApprovals? approvals = null, string? repositoryRoot = "")
    {
        var copy = JsonNode.Parse(s.Manifest.ToJsonString())!.AsObject();
        edit?.Invoke(copy);
        var result = ReplayCheck.Validate(Json(copy.ToJsonString()), approvals ?? s.Approvals, repositoryRoot == "" ? _root : repositoryRoot);
        Assert.False(result.IsSuccess, "expected a refusal");
        return result.Error!.Value.Code;
    }

    private static void Attest(JsonObject manifest, Func<OfficialAttestation, OfficialAttestation> change)
    {
        var current = H.Ok(OfficialAttestation.FromJson(Json(manifest["official"]!["attestation"]!.ToJsonString())));
        manifest["official"] = Envelope(change(current));                                               // the attestation hash is recomputed: self-consistent on purpose
    }

    [Fact]
    public void A_consistent_official_world_is_accepted_only_with_the_trusted_pin_and_the_real_file()
    {
        var s = Consistent();
        var ok = ReplayCheck.Validate(Json(s.Manifest.ToJsonString()), s.Approvals, _root);
        Assert.True(ok.IsSuccess, ok.Error?.Message);
        Assert.Equal(14, ok.Value.Games.Count);
        Assert.Null(ok.Value.CheckAgainst(H.Ok(RunConfig.Create("official-test", "replay", NeutralCohort.Create(), H.Rules(true), H.Tuning, H.Park(), "advancement-policy-v2", 3000, competitionKey: Key))));

        // a correct attestation hash is not an approval: the real approvals pin nothing
        Assert.Equal("Official.seed_manifest_not_frozen", OfficialCode(s, approvals: OfficialApprovals.Current));
        // no repository root, no real file: the hash string alone verifies nothing
        Assert.Equal("Replay.official_seed_manifest_unavailable", OfficialCode(s, repositoryRoot: null));
        // an official-mode manifest cannot be replayed as a legacy one by dropping its envelope
        Assert.Equal("Replay.official_envelope_missing", OfficialCode(s, m => m.Remove("official")));
        // the pinned file is gone / the wrong repository
        File.Delete(Path.Combine(_root, "seeds", "m.json"));
        Assert.Equal("Seed.file_missing", OfficialCode(s));
    }

    // ---------------------------------------------------------------- N-01 re-review: an official-family manifest cannot be downgraded to a legacy replay

    private static void Downgrade(JsonObject m, string mode)
    {
        m.Remove("official");
        m["run"]!["mode"] = mode;
    }

    [Fact]
    public void N01_an_official_competition_key_without_its_envelope_is_refused_whatever_the_mode_says()
    {
        var s = Consistent();
        foreach (var mode in new[] { "smoke", "replay", "legacy", "" })
            Assert.Equal("Replay.official_envelope_missing", OfficialCode(s, m => Downgrade(m, mode)));
        Assert.Equal("Replay.official_envelope_missing", OfficialCode(s, m => Downgrade(m, "smoke"), approvals: OfficialApprovals.Current, repositoryRoot: null));   // no approval and no root needed to refuse
        Assert.Equal("Replay.official_envelope_missing", OfficialCode(s, m => { Downgrade(m, "smoke"); m["engine"]!["policy_id"] = "aggressive-attack-v1"; }));   // refused before anything else is looked at

        // the same downgrade at the command line: refused (exit 2) before any game is played
        var downgraded = JsonNode.Parse(s.Manifest.ToJsonString())!.AsObject();
        Downgrade(downgraded, "smoke");
        Assert.Equal(2, Program.Main(["replay", "--manifest", Write(downgraded)]));
    }

    [Fact]
    public void N01_an_envelope_with_a_mode_other_than_official_is_refused()
    {
        var s = Consistent();
        Assert.Equal("Replay.official_mode_mismatch", OfficialCode(s, m => m["run"]!["mode"] = "smoke"));
        Assert.Equal("Replay.official_mode_mismatch", OfficialCode(s, m => m["run"]!["mode"] = "Official"));
        Assert.Equal("Replay.manifest_invalid", OfficialCode(s, m => m["official"] = null));                    // a null envelope is not an absent one
    }

    [Fact]
    public void N01_the_official_competition_key_namespace_is_exactly_what_an_official_seed_file_may_declare()
    {
        foreach (var key in new[] { "fc3-cal", "fc3-cal-l1", "fc3-int", "fc3-int-auto", "fc3-dom", "fc3-dom-x", "FC3-DOM-X", "Fc3-Cal" })
            Assert.True(SeedFamilies.IsOfficialCompetitionKey(key), key);
        foreach (var key in new[] { "sim", "fc2-dry", "fc2-dry-x", "fc3", "fc3-calx", "fc3-domx", "xfc3-dom", "" })
            Assert.False(SeedFamilies.IsOfficialCompetitionKey(key), key);

        // every key an official seed file accepts is in the namespace, and a key the namespace excludes is refused by the seed reader for every official family
        foreach (var family in SeedFamilies.All.Values.Where(f => f.Official).Select(f => f.Id))
        {
            Assert.True(SeedFamilies.IsOfficialCompetitionKey(family));
            var bytes = Encoding.UTF8.GetBytes(SeedManifestTests.Manifest(family, family + "x").ToJsonString());
            File.WriteAllBytes(Path.Combine(_root, "seeds", "k.json"), bytes);
            Assert.Equal("Seed.competition_key_family_mismatch", SeedManifestReader.Read(_root, "seeds/k.json", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()).Error!.Value.Code);
        }
    }

    [Fact]
    public void N01_legacy_and_dry_run_keys_still_replay_without_an_envelope()
    {
        foreach (var file in new[] { Legacy, "legacy-manifest-automated-v1.json" })
            Assert.True(ReplayCheck.Validate(Json(File.ReadAllText(Fixture(file))), OfficialApprovals.Current).IsSuccess, file);

        // a non-official fc2-dry key passes the official-family gate (whatever is checked after it is unchanged)
        var dry = LegacyNode();
        dry["engine"]!["competition_key"] = "fc2-dry-x";
        foreach (var g in dry["games"]!.AsArray())
            g!["game_id"] = $"game:fc2-dry-x:neutral-v1-{g["index"]!.GetValue<int>():D6}";
        Assert.True(ReplayCheck.Validate(Json(dry.ToJsonString()), OfficialApprovals.Current).IsSuccess);
    }

    [Fact]
    public void A1_to_A3_self_hashed_attestation_fields_must_match_the_verified_seed_file_and_the_approved_profile()
    {
        var s = Consistent();
        Assert.Equal("Replay.attestation_seed_family_mismatch", OfficialCode(s, m => Attest(m, a => a with { SeedFamily = "fc2-dry" })));          // A1
        Assert.Equal("Replay.attestation_seed_family_mismatch", OfficialCode(s, m => Attest(m, a => a with { SeedFamily = "fc3-cal" })));          // A9
        Assert.Equal("Replay.game_id_rule_mismatch", OfficialCode(s, m => Attest(m, a => a with { GameIdRule = "game-id-v2" })));                  // A2
        Assert.Equal("Replay.park_snapshot_mismatch", OfficialCode(s, m => Attest(m, a => a with { ParkSnapshot = "stadium-sim-neutral|pf-sim-neutral|1|1100|1000|1000" })));   // A3
        Assert.Equal("Replay.park_snapshot_mismatch", OfficialCode(s, m => m["engine"]!["park"]!["home_runs_permille"] = 1100));                   // A3: the park the manifest ran with
    }

    [Fact]
    public void A4_to_A7_the_replay_games_must_equal_the_games_of_the_pinned_seed_file()
    {
        var s = Consistent();
        Assert.Equal("Replay.seed_games_mismatch", OfficialCode(s, m => Game(m, 3)["seed"] = 5));                                                    // A4: games changed, hash strings kept
        Assert.Equal("Replay.seed_games_mismatch", OfficialCode(s, m =>                                                                              // A5: another seed order under the same ids
        {
            var first = U(Game(m, 0)["seed"]);
            Game(m, 0)["seed"] = U(Game(m, 1)["seed"]);
            Game(m, 1)["seed"] = first;
        }));
        Assert.Equal("Replay.game_id_mismatch", OfficialCode(s, m => Game(m, 0)["game_id"] = "game:fc3-dom-x:neutral-v1-000099"));                    // A7
        Assert.Equal("Replay.seed_games_mismatch", OfficialCode(s, m => m["games"]!.AsArray().RemoveAt(13)));                                        // a shorter game list under the same pin
        // the file itself changed under the pin
        File.WriteAllText(Path.Combine(_root, "seeds", "m.json"), s.Seed.ToJsonString().Replace("990003", "990103", StringComparison.Ordinal));
        Assert.Equal("Seed.file_hash_mismatch", OfficialCode(s));
    }

    [Fact]
    public void A6_A8_A10_a_forged_manifest_and_attestation_that_agree_with_each_other_still_meet_the_external_pin()
    {
        var s = Consistent();
        // A6: competition key changed everywhere the manifest controls (engine, game ids, config hash, attestation) -> the real seed file says otherwise
        const string key2 = "fc3-dom-y";
        var games = s.Manifest["games"]!.AsArray().Select(g => (g!["index"]!.GetValue<int>(), U(g["seed"]))).ToList();
        Assert.Equal("Replay.competition_key_mismatch", OfficialCode(s, m =>
        {
            m["engine"]!["competition_key"] = key2;
            m["run"]!["config_hash"] = ConfigHashOf(key2, games);
            foreach (var g in m["games"]!.AsArray())
                g!["game_id"] = $"game:{key2}:neutral-v1-{g["index"]!.GetValue<int>():D6}";
            Attest(m, a => a with { CompetitionKey = key2, ConfigHash = ConfigHashOf(key2, games) });
        }));

        // A8: a different seed file whose hash the attestation names -> not the pinned one
        var other = Encoding.UTF8.GetBytes(s.Seed.ToJsonString() + " ");
        Assert.Equal("Replay.seed_manifest_hash_mismatch", OfficialCode(s, m => Attest(m, a => a with { SeedManifestSha256 = Convert.ToHexString(SHA256.HashData(other)).ToLowerInvariant() })));

        // A10: a policy / cohort nobody approved, substituted consistently
        Assert.Equal("Replay.attestation_manifest_mismatch", OfficialCode(s, m => Attest(m, a => a with { PolicyId = "aggressive-attack-v1" })));
        Assert.Equal("Registry.policy_unknown", OfficialCode(s, m => { m["engine"]!["policy_id"] = "aggressive-attack-v1"; Attest(m, a => a with { PolicyId = "aggressive-attack-v1" }); }));
        Assert.Equal("Replay.cohort_hash_mismatch", OfficialCode(s, m => { m["run"]!["cohort_id"] = "neutral-opp-v1"; Attest(m, a => a with { CohortId = "neutral-opp-v1" }); }));   // F-C2-C: built now; its inputs are not the recorded ones
        // the profile fields: a model / tuning / rules hash / budget the approved execution profile does not name
        Assert.Equal("Replay.attestation_manifest_mismatch", OfficialCode(s, m => Attest(m, a => a with { ModelVersion = "prob-model-v3" })));
        Assert.Equal("Replay.official_profile_mismatch", OfficialCode(s, m => { m["engine"]!["event_budget"] = 2999; Attest(m, a => a with { EventBudget = 2999 }); }));
    }

    [Fact]
    public void A_fixture_approval_cannot_reach_the_real_replay_command_and_the_real_official_command_stays_not_ready()
    {
        var s = Consistent();
        var path = Write(s.Manifest);
        Assert.Equal(2, Program.Main(["replay", "--manifest", path]));                                    // the command uses OfficialApprovals.Current only
        Assert.Equal(2, Spawn(H.Root, "replay", "--manifest", path).ExitCode);

        var child = Spawn(H.Root, "official", "--seed-manifest", "content/seeds/none.json", "--run-id", "fc2a-codex-" + Guid.NewGuid().ToString("N")[..8]);
        Assert.Equal(2, child.ExitCode);
        var result = Json(child.Output);
        Assert.Equal(("PREFLIGHT_REFUSED", "NOT_EVALUATED"), (result.GetProperty("run_status").GetString(), result.GetProperty("gate_status").GetString()));
        Assert.Contains(result.GetProperty("checks").EnumerateArray(), c => c.GetProperty("code").GetString() == "Official.not_ready");
    }

    // ---------------------------------------------------------------- F-03 reparse points

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

    /// <summary>A directory junction (no privilege needed on Windows) from <paramref name="link"/> to <paramref name="target"/>; a symlink fallback elsewhere.</summary>
    private void Junction(string link, string target)
    {
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        if (OperatingSystem.IsWindows())
        {
            using var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true })!;
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            Assert.Equal(0, p.ExitCode);
        }
        else
            Directory.CreateSymbolicLink(link, target);
        _links.Add(link);
        Assert.True(new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint), "the test link is not a reparse point");
    }

    private (string Rel, string Sha) SeedIn(string directory)
    {
        Directory.CreateDirectory(directory);
        var bytes = Encoding.UTF8.GetBytes(SeedManifestTests.Manifest().ToJsonString());
        File.WriteAllBytes(Path.Combine(directory, "m.json"), bytes);
        return ("", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    [Fact]
    public void F03_a_seed_path_through_a_junction_out_of_the_root_is_refused_and_a_normal_path_is_not()
    {
        var outside = Path.Combine(Path.GetTempPath(), "simharness-tests", "outside-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (_, sha) = SeedIn(outside);
            Junction(Path.Combine(_root, "linked"), outside);
            Assert.Equal("Seed.path_reparse_point", SeedManifestReader.Read(_root, "linked/m.json", sha).Error!.Value.Code);      // the string prefix says "inside"; the real directory is outside
            Assert.Equal("Seed.path_reparse_point", SeedManifestReader.Read(_root, "linked/m.json", null).Error!.Value.Code);      // refused before the pin is even consulted
            var (_, ok) = SeedIn(Path.Combine(_root, "seeds"));
            Assert.True(SeedManifestReader.Read(_root, "seeds/m.json", ok).IsSuccess);                                               // the normal in-root path
        }
        finally
        {
            foreach (var link in _links.Where(Directory.Exists).ToList())
                Directory.Delete(link);
            _links.Clear();
            if (Directory.Exists(outside))
                Directory.Delete(outside, true);
        }
    }

    [Fact]
    public void F03_a_root_that_is_itself_a_link_is_refused()
    {
        var real = Path.Combine(Path.GetTempPath(), "simharness-tests", "realroot-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (_, sha) = SeedIn(Path.Combine(real, "seeds"));
            var linkedRoot = Path.Combine(Path.GetTempPath(), "simharness-tests", "linkroot-" + Guid.NewGuid().ToString("N"));
            Junction(linkedRoot, real);
            Assert.Equal("Seed.path_reparse_point", SeedManifestReader.Read(linkedRoot, "seeds/m.json", sha).Error!.Value.Code);
            Assert.Equal("Official.output_reparse_point", OfficialOutput.Resolve(linkedRoot, null, "r1").Error!.Value.Code);
        }
        finally
        {
            foreach (var link in _links.Where(Directory.Exists).ToList())
                Directory.Delete(link);
            _links.Clear();
            Directory.Delete(real, true);
        }
    }

    [Fact]
    public void N02_a_link_ABOVE_the_root_is_refused_for_the_seed_and_the_output_path()
    {
        var outside = Path.Combine(Path.GetTempPath(), "simharness-tests", "outside-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (_, sha) = SeedIn(Path.Combine(outside, "repo", "seeds"));
            Junction(Path.Combine(_root, "up"), outside);
            foreach (var root in new[] { Path.Combine(_root, "up", "repo"), Path.Combine(_root, "up", "repo") + Path.DirectorySeparatorChar })   // the root itself is a plain directory; its PARENT is a link
            {
                Assert.False(new DirectoryInfo(root).Attributes.HasFlag(FileAttributes.ReparsePoint));
                Assert.Equal("Seed.path_reparse_point", SeedManifestReader.Read(root, "seeds/m.json", sha).Error!.Value.Code);
                Assert.Equal("Official.output_reparse_point", OfficialOutput.Resolve(root, null, "r1").Error!.Value.Code);
            }

            Junction(Path.Combine(_root, "deep", "a"), Path.Combine(outside, "repo"));                                 // a link two levels above the root
            Directory.CreateDirectory(Path.Combine(_root, "deep", "a", "b"));
            Assert.Equal("Official.output_reparse_point", OfficialOutput.Resolve(Path.Combine(_root, "deep", "a", "b"), null, "r1").Error!.Value.Code);

            // a root whose ancestors (up to the volume root) are all plain directories is unaffected
            var (_, ok) = SeedIn(Path.Combine(_root, "seeds"));
            Assert.True(SeedManifestReader.Read(_root, "seeds/m.json", ok).IsSuccess);
            Assert.True(OfficialOutput.Resolve(_root, null, "r1").IsSuccess);
        }
        finally
        {
            foreach (var link in _links.Where(Directory.Exists).ToList())
                Directory.Delete(link);
            _links.Clear();
            if (Directory.Exists(outside))
                Directory.Delete(outside, true);
        }
    }

    [Fact]
    public void F03_an_official_output_below_a_junction_is_refused_wherever_the_link_sits()
    {
        var outside = Path.Combine(Path.GetTempPath(), "simharness-tests", "outside-" + Guid.NewGuid().ToString("N"));
        try
        {
            Junction(Path.Combine(_root, "build"), outside);                                                          // the first component is a link
            Assert.Equal("Official.output_reparse_point", OfficialOutput.Resolve(_root, null, "r1").Error!.Value.Code);
            Directory.Delete(Path.Combine(_root, "build"));
            _links.Clear();

            Directory.CreateDirectory(Path.Combine(_root, "build", "simharness", "official"));
            Junction(Path.Combine(_root, "build", "simharness", "official", "linked"), outside);                      // the output PARENT is a link
            Assert.Equal("Official.output_reparse_point", OfficialOutput.Resolve(_root, "build/simharness/official/linked/run", "r1").Error!.Value.Code);
            Assert.Equal("Official.output_reparse_point", OfficialOutput.Resolve(_root, "build/simharness/official/linked", "r1").Error!.Value.Code);   // an existing link as the output itself
            Assert.Equal(Path.Combine(_root, "build", "simharness", "official", "fresh"), H.Ok(OfficialOutput.Resolve(_root, "build/simharness/official/fresh", "r1")));   // a normal new path still resolves
            Assert.Equal("Official.output_invalid", OfficialOutput.Resolve(_root, "build/simharness/official-evil/x", "r1").Error!.Value.Code);        // a root look-alike prefix
        }
        finally
        {
            foreach (var link in _links.Where(Directory.Exists).ToList())
                Directory.Delete(link);
            _links.Clear();
            if (Directory.Exists(outside))
                Directory.Delete(outside, true);
        }
    }

    [Fact]
    public void F03_the_official_command_reports_a_reparse_point_in_the_output_path_and_exits_nonzero()
    {
        var outside = Path.Combine(Path.GetTempPath(), "simharness-tests", "outside-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(Path.Combine(_root, "StarPlayer.sln"), "");                                              // a stand-in repository root for RepositoryRoot()
            Junction(Path.Combine(_root, "build"), outside);
            var child = Spawn(_root, "official", "--seed-manifest", "seeds/none.json", "--run-id", "linked-run");
            Assert.Equal(2, child.ExitCode);
            var checks = Json(child.Output).GetProperty("checks").EnumerateArray().ToList();
            Assert.Equal("Official.output_reparse_point", checks.Single(c => c.GetProperty("id").GetString() == "output_directory").GetProperty("code").GetString());
            Assert.False(Directory.Exists(Path.Combine(outside, "simharness")));                                       // nothing was created behind the link
        }
        finally
        {
            foreach (var link in _links.Where(Directory.Exists).ToList())
                Directory.Delete(link);
            _links.Clear();
            if (Directory.Exists(outside))
                Directory.Delete(outside, true);
        }
    }

    // N-05: a JSON null in a required string is a malformed manifest (an explicit refusal, exit 2), never an unhandled NullReferenceException.

    [Fact]
    public void N05_a_null_required_string_is_a_manifest_refusal_not_an_exception()
    {
        var s = Consistent();
        var nulled = new (string Section, string Field)[] { ("engine", "competition_key"), ("run", "config_hash"), ("run", "run_id"), ("results", "run_result_hash") };
        foreach (var (section, field) in nulled)
        {
            Assert.Equal("Replay.manifest_invalid", OfficialCode(s, m => m[section]![field] = null));                          // official envelope present
            var legacy = LegacyNode();
            legacy[section]![field] = null;
            var check = ReplayCheck.Validate(Json(legacy.ToJsonString()), OfficialApprovals.Current);                          // legacy path
            Assert.False(check.IsSuccess, $"{section}.{field}");
            Assert.Equal("Replay.manifest_invalid", check.Error!.Value.Code);
            Assert.Equal(2, Program.Main(["replay", "--manifest", Write(legacy)]));                                              // and at the command line
        }

        // the other strings a manifest names were already refused by their registries / comparisons: still an error value, never a throw
        foreach (var (section, field) in new[] { ("run", "cohort_id"), ("engine", "policy_id"), ("engine", "rules_profile_id"), ("engine", "advancement_policy_version"), ("engine", "cohort_inputs_hash") })
        {
            var legacy = LegacyNode();
            legacy[section]![field] = null;
            Assert.False(ReplayCheck.Validate(Json(legacy.ToJsonString()), OfficialApprovals.Current).IsSuccess, $"{section}.{field}");
            Assert.Equal(2, Program.Main(["replay", "--manifest", Write(legacy)]));
        }
    }

    // N-03: a symlink test needs a real symlink. Without one it is SKIPPED with a NOT_RUN reason (never a silent PASS); once the probe could make one, any failure to make it again is a FAIL.

    [SymlinkFact(file: false)]
    public void N03_a_directory_symlink_on_the_seed_and_output_paths_is_refused()
    {
        var outside = Path.Combine(Path.GetTempPath(), "simharness-tests", "outside-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (_, sha) = SeedIn(outside);
            var link = Path.Combine(_root, "symdir");
            Directory.CreateSymbolicLink(link, outside);
            _links.Add(link);
            Assert.NotNull(new DirectoryInfo(link).LinkTarget);                                                       // a real symbolic link, not a junction stand-in
            Assert.Equal("Seed.path_reparse_point", SeedManifestReader.Read(_root, "symdir/m.json", sha).Error!.Value.Code);
            Assert.Equal("Seed.path_reparse_point", SeedManifestReader.Read(link, "m.json", sha).Error!.Value.Code);   // the link as the root
            Directory.CreateDirectory(Path.Combine(outside, "repo"));
            Assert.Equal("Official.output_reparse_point", OfficialOutput.Resolve(Path.Combine(link, "repo"), null, "r1").Error!.Value.Code);   // the link above the root (N-02)
        }
        finally
        {
            foreach (var link in _links.Where(Directory.Exists).ToList())
                Directory.Delete(link);
            _links.Clear();
            if (Directory.Exists(outside))
                Directory.Delete(outside, true);
        }
    }

    [SymlinkFact(file: true)]
    public void N03_a_file_symlink_as_the_seed_manifest_is_refused()
    {
        var outside = Path.Combine(Path.GetTempPath(), "simharness-tests", "outside-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (_, sha) = SeedIn(outside);
            var link = Path.Combine(_root, "seeds", "file-link.json");
            File.CreateSymbolicLink(link, Path.Combine(outside, "m.json"));
            Assert.NotNull(new FileInfo(link).LinkTarget);
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(link))).ToLowerInvariant());   // the pin matches the bytes behind the link: only the link check refuses it
            Assert.Equal("Seed.path_reparse_point", SeedManifestReader.Read(_root, "seeds/file-link.json", sha).Error!.Value.Code);
        }
        finally
        {
            if (Directory.Exists(outside))
                Directory.Delete(outside, true);
        }
    }
}

/// <summary>
/// A fact that needs a real symbolic link of one kind (file or directory). The kind is probed once by actually creating one in TEMP; when this account cannot (no SeCreateSymbolicLinkPrivilege and no
/// Developer Mode), the test is reported SKIPPED with a <c>NOT_RUN</c> reason, which the verify gate counts as INCOMPLETE, never as PASS. It is never turned into a pass inside the test body.
/// </summary>
public sealed class SymlinkFactAttribute : FactAttribute
{
    private static readonly Lazy<string?> DirectoryProbe = new(() => Probe(false));
    private static readonly Lazy<string?> FileProbe = new(() => Probe(true));

    public SymlinkFactAttribute(bool file)
    {
        if ((file ? FileProbe : DirectoryProbe).Value is { } why)
            Skip = $"NOT_RUN: 이 계정은 {(file ? "파일" : "디렉터리")} Symlink를 만들 수 없어 검증하지 않았습니다({why}). 개발자 모드 또는 Symlink 생성 권한이 있는 계정에서 실행해야 증거가 됩니다.";
    }

    /// <summary>Null when a link of the kind could be created and is seen as a reparse point; otherwise why not.</summary>
    private static string? Probe(bool file)
    {
        var dir = Path.Combine(Path.GetTempPath(), "simharness-tests", "symprobe-" + Guid.NewGuid().ToString("N"));
        var link = Path.Combine(dir, "link");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "target"));
            File.WriteAllText(Path.Combine(dir, "target.txt"), "x");
            FileSystemInfo created = file ? File.CreateSymbolicLink(link, Path.Combine(dir, "target.txt")) : Directory.CreateSymbolicLink(link, Path.Combine(dir, "target"));
            return File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint) && created.LinkTarget is not null ? null : "만든 링크가 reparse point로 보이지 않음";
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return $"{e.GetType().Name}: {e.Message}";
        }
        finally
        {
            try
            {
                if (Directory.Exists(link))
                    Directory.Delete(link);
                else if (File.Exists(link))
                    File.Delete(link);
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
