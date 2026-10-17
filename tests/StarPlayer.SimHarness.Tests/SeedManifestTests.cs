using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// F-C2-A Part C (FC-D5): the seed-manifest reader. Every fixture here is a TEMP file with made-up seeds; no official seed file is created, read or pinned. A manifest is accepted only against a
/// SHA-256 of its raw bytes pinned OUTSIDE the file (the file never vouches for itself), and the official pin table is empty in F-C2-A.
/// </summary>
public sealed class SeedManifestTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "simharness-tests", "seeds-" + Guid.NewGuid().ToString("N"));

    public SeedManifestTests() => Directory.CreateDirectory(Path.Combine(_root, "seeds"));

    public void Dispose() => Directory.Delete(_root, true);

    private static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    /// <summary>A valid manifest of <paramref name="games"/> games (fictional seeds 990001...), changed by <paramref name="edit"/>.</summary>
    internal static JsonObject Manifest(string family = "fc2-dry", string key = "fc2-dry-test", int games = 14, int block = 7, int max = 14, long? paTarget = null, string cohort = "neutral-v1")
    {
        var seeds = new JsonArray();
        for (var i = 1; i <= games; i++)
            seeds.Add(new JsonObject { ["index"] = i, ["seed"] = 990000 + i, ["game_id"] = $"game:{key}:{cohort}-{i:D6}" });
        return new JsonObject
        {
            ["schema_version"] = "simharness-seed-manifest-v1",
            ["family"] = family,
            ["competition_key"] = key,
            ["cohort_id"] = cohort,
            ["policy_id"] = "normal-balanced-v1",
            ["rules_profile_id"] = "test-pzl-non-automated",
            ["game_id_rule"] = "game-id-v1",
            ["block_size_games"] = block,
            ["max_games"] = max,
            ["pa_target"] = paTarget,
            ["seeds"] = seeds,
        };
    }

    private (string Rel, string Sha) Put(string text, string name = "m.json")
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(Path.Combine(_root, "seeds", name), bytes);
        return ("seeds/" + name, Sha(bytes));
    }

    private string CodeOf(JsonObject m) => CodeOf(m.ToJsonString());

    private string CodeOf(string text)
    {
        var (rel, sha) = Put(text);
        var read = SeedManifestReader.Read(_root, rel, sha);
        Assert.False(read.IsSuccess, "expected a refusal");
        return read.Error!.Value.Code;
    }

    private static JsonObject With(JsonObject m, Action<JsonObject> edit)
    {
        edit(m);
        return m;
    }

    [Fact]
    public void A_valid_manifest_is_read_against_its_pinned_raw_file_hash()
    {
        var (rel, sha) = Put(Manifest().ToJsonString());
        var m = H.Ok(SeedManifestReader.Read(_root, rel, sha));

        Assert.Equal(("fc2-dry", "fc2-dry-test", "neutral-v1", "normal-balanced-v1", "test-pzl-non-automated"), (m.Family, m.CompetitionKey, m.CohortId, m.PolicyId, m.RulesProfileId));
        Assert.Equal((7, 14, (long?)null), (m.BlockSizeGames, m.MaxGames, m.PaTarget));
        Assert.Equal(sha, m.FileSha256);
        Assert.Equal(14, m.Seeds.Count);
        Assert.Equal(new SeedEntry(1, 990001, "game:fc2-dry-test:neutral-v1-000001"), m.Seeds[0]);
        Assert.False(SeedFamilies.All["fc2-dry"].Official);                                           // the dry run is never official
        Assert.All(new[] { "fc3-cal", "fc3-int", "fc3-dom" }, f => Assert.True(SeedFamilies.All[f].Official));
        Assert.Equal(4, SeedFamilies.All.Count);
    }

    [Fact]
    public void The_game_ids_are_exactly_the_ones_the_run_configuration_would_make()
    {
        var (rel, sha) = Put(Manifest().ToJsonString());
        var m = H.Ok(SeedManifestReader.Read(_root, rel, sha));
        var config = H.Ok(RunConfig.Create("x", "smoke", NeutralCohort.Create(), H.Rules(), H.Tuning, H.Park(), competitionKey: m.CompetitionKey));
        Assert.All(m.Seeds, s => Assert.Equal(config.Spec(s.Index, s.Seed), new GameSpec(s.Index, s.GameId, s.Seed)));
    }

    [Fact]
    public void The_hash_is_checked_against_the_pin_and_never_against_the_file_itself()
    {
        var text = Manifest().ToJsonString();
        var (rel, sha) = Put(text);

        Assert.Equal("Seed.file_hash_mismatch", SeedManifestReader.Read(_root, rel, new string('0', 64)).Error!.Value.Code);
        Assert.Equal("Seed.pin_invalid", SeedManifestReader.Read(_root, rel, null).Error!.Value.Code);                 // no pin, no read
        Assert.Equal("Seed.pin_invalid", SeedManifestReader.Read(_root, rel, sha.ToUpperInvariant()).Error!.Value.Code);
        Assert.Equal("Seed.pin_invalid", SeedManifestReader.Read(_root, rel, sha[..63]).Error!.Value.Code);
        // a self-declared hash member is not part of the schema at all
        Assert.Equal("Seed.json_invalid", CodeOf(With(Manifest(), m => m["file_sha256"] = sha)));
        Assert.Empty(SeedManifestPins.Official);                                                      // F-C2-A pins no official seed file
    }

    [Fact]
    public void Partial_corruption_is_caught_by_the_pin_or_by_the_strict_reader()
    {
        var text = Manifest().ToJsonString();
        var (rel, sha) = Put(text);
        // one seed digit flipped: still well-formed JSON with legal game ids, so ONLY the pinned raw hash can catch it
        File.WriteAllText(Path.Combine(_root, rel), text.Replace("990003", "990004", StringComparison.Ordinal));
        Assert.Equal("Seed.file_hash_mismatch", SeedManifestReader.Read(_root, rel, sha).Error!.Value.Code);
        // truncated: caught by the pin, and by the reader even when it is pinned as it is
        Assert.Equal("Seed.file_hash_mismatch", SeedManifestReader.Read(_root, Put(text[..(text.Length / 2)], "t.json").Rel, sha).Error!.Value.Code);
        Assert.Equal("Seed.json_invalid", CodeOf(text[..(text.Length / 2)]));
        Assert.Equal("Seed.json_invalid", CodeOf(""));
        Assert.Equal("Seed.json_invalid", CodeOf(text + "x"));
    }

    [Theory]
    [InlineData("/abs/m.json")]
    [InlineData("C:/x/m.json")]
    [InlineData("../m.json")]
    [InlineData("seeds/../../m.json")]
    [InlineData("seeds/m.txt")]
    [InlineData("")]
    public void Illegal_file_paths_are_refused(string rel) => Assert.Equal("Seed.path_invalid", SeedManifestReader.Read(_root, rel, new string('a', 64)).Error!.Value.Code);

    [Fact]
    public void A_missing_file_is_refused()
    {
        Assert.Equal("Seed.file_missing", SeedManifestReader.Read(_root, "seeds/none.json", new string('a', 64)).Error!.Value.Code);
        Assert.Equal("Seed.path_invalid", SeedManifestReader.Read(_root, Path.GetFullPath(Path.Combine(_root, "seeds", "m.json")), new string('a', 64)).Error!.Value.Code);
    }

    [Fact]
    public void Schema_family_and_rule_versions_are_checked()
    {
        Assert.Equal("Seed.schema_unsupported", CodeOf(With(Manifest(), m => m["schema_version"] = "simharness-seed-manifest-v2")));
        Assert.Equal("Seed.family_unknown", CodeOf(With(Manifest(), m => m["family"] = "fc4-x")));
        Assert.Equal("Seed.family_unknown", CodeOf(With(Manifest(), m => m["family"] = "FC2-DRY")));
        Assert.Equal("Seed.game_id_rule_unknown", CodeOf(With(Manifest(), m => m["game_id_rule"] = "game-id-v2")));
    }

    [Fact]
    public void Missing_null_unknown_and_repeated_members_are_refused()
    {
        foreach (var name in new[] { "schema_version", "family", "competition_key", "cohort_id", "policy_id", "rules_profile_id", "game_id_rule", "block_size_games", "max_games", "pa_target", "seeds" })
            Assert.Equal("Seed.json_invalid", CodeOf(With(Manifest(), m => m.Remove(name))));
        foreach (var name in new[] { "family", "competition_key", "cohort_id", "policy_id", "rules_profile_id", "seeds" })
            Assert.Equal("Seed.shape_invalid", CodeOf(With(Manifest(), m => m[name] = null)));
        Assert.Equal("Seed.json_invalid", CodeOf(With(Manifest(), m => m["extra"] = 1)));
        Assert.Equal("Seed.json_invalid", CodeOf(With(Manifest(), m => m["seeds"]![0]!.AsObject().Remove("game_id"))));
        Assert.Equal("Seed.shape_invalid", CodeOf(With(Manifest(), m => m["seeds"]![0] = null)));
        // a repeated member would otherwise be resolved silently (last one wins)
        var text = Manifest().ToJsonString();
        Assert.Equal("Seed.json_duplicate_member", CodeOf(text.Replace("\"family\":\"fc2-dry\",", "\"family\":\"fc2-dry\",\"family\":\"fc3-cal\",", StringComparison.Ordinal)));
        Assert.Equal("Seed.json_duplicate_member", CodeOf(text.Replace("\"index\":1,", "\"index\":1,\"index\":2,", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("\"3\"")]
    [InlineData("18446744073709551616")]
    [InlineData("null")]
    [InlineData("true")]
    public void Malformed_seed_values_are_refused(string seed) =>
        Assert.Equal("Seed.json_invalid", CodeOf(Manifest().ToJsonString().Replace("\"seed\":990001", "\"seed\":" + seed, StringComparison.Ordinal)));

    [Fact]
    public void Seed_values_cover_the_whole_unsigned_range_and_may_repeat_under_different_game_ids()
    {
        var m = With(Manifest(), m => { m["seeds"]![0]!["seed"] = 0; m["seeds"]![1]!["seed"] = ulong.MaxValue; m["seeds"]![2]!["seed"] = 0; });
        var (rel, sha) = Put(m.ToJsonString());
        var read = H.Ok(SeedManifestReader.Read(_root, rel, sha));
        Assert.Equal(new ulong[] { 0, ulong.MaxValue, 0 }, read.Seeds.Take(3).Select(s => s.Seed).ToArray());   // FC-D4.6: same seed, other GameId = other RNG path; not a collision
    }

    [Fact]
    public void Index_and_game_id_must_be_unique_and_in_the_declared_order()
    {
        Assert.Equal("Seed.seeds_empty", CodeOf(Manifest(games: 0)));
        Assert.Equal("Seed.index_duplicate", CodeOf(With(Manifest(), m => m["seeds"]![1]!["index"] = 1)));
        Assert.Equal("Seed.game_id_duplicate", CodeOf(With(Manifest(), m => m["seeds"]![1]!["game_id"] = "game:fc2-dry-test:neutral-v1-000001")));
        Assert.Equal("Seed.order_invalid", CodeOf(With(Manifest(), m => { m["seeds"]![0]!["index"] = 2; m["seeds"]![1]!["index"] = 1; })));     // swapped
        Assert.Equal("Seed.order_invalid", CodeOf(With(Manifest(), m => m["seeds"]![13]!["index"] = 15)));                                    // a gap
        Assert.Equal("Seed.order_invalid", CodeOf(With(Manifest(), m => { foreach (var s in m["seeds"]!.AsArray()) s!["index"] = s["index"]!.GetValue<int>() - 1; })));   // from 0
    }

    [Fact]
    public void The_competition_key_must_belong_to_the_family_and_be_the_one_in_every_game_id()
    {
        Assert.Equal("Seed.competition_key_family_mismatch", CodeOf(Manifest(key: "fc3-cal-l1")));                         // a calibration key in a dry-run manifest
        Assert.Equal("Seed.competition_key_family_mismatch", CodeOf(Manifest(key: "fc2-drytest")));                       // the family is a whole prefix segment
        Assert.Equal("Seed.competition_key_invalid", CodeOf(With(Manifest(), m => m["competition_key"] = "fc2-dry:x")));
        Assert.Equal("Seed.game_id_rule_mismatch", CodeOf(With(Manifest(), m => m["seeds"]![4]!["game_id"] = "game:fc2-dry-other:neutral-v1-000005")));
        Assert.Equal("Seed.game_id_rule_mismatch", CodeOf(With(Manifest(), m => m["seeds"]![4]!["game_id"] = "game:fc2-dry-test:neutral-v1-000099")));    // index and id disagree
        Assert.Equal("Seed.game_id_rule_mismatch", CodeOf(With(Manifest(), m => m["seeds"]![4]!["game_id"] = "game:fc2-dry-test:neutral-mixed-v1-000005")));
        Assert.Equal("Seed.cohort_id_invalid", CodeOf(With(Manifest(), m => m["cohort_id"] = "bad cohort")));
        Assert.Equal("Seed.shape_invalid", CodeOf(With(Manifest(), m => m["policy_id"] = "")));
    }

    [Fact]
    public void Competition_keys_are_never_reused_across_manifests()
    {
        SeedManifest Read(JsonObject j, string name)
        {
            var (rel, sha) = Put(j.ToJsonString(), name);
            return H.Ok(SeedManifestReader.Read(_root, rel, sha));
        }

        var a = Read(Manifest(key: "fc2-dry-a"), "a.json");
        var b = Read(Manifest(key: "fc2-dry-b"), "b.json");
        var a2 = Read(Manifest(key: "fc2-dry-a", cohort: "neutral-opp-v1"), "a2.json");

        Assert.Null(SeedManifestReader.CheckUnique([a, b]));
        Assert.Equal("Seed.competition_key_reused", SeedManifestReader.CheckUnique([a, b, a2])!.Value.Code);
        Assert.Equal("Seed.competition_key_reused", SeedManifestReader.CheckUnique([a, a])!.Value.Code);
    }

    [Fact]
    public void Blocks_must_tile_the_declared_cap()
    {
        Assert.Equal("Seed.block_boundary_invalid", CodeOf(Manifest(block: 0, games: 14, max: 14)));
        Assert.Equal("Seed.block_boundary_invalid", CodeOf(Manifest(block: 5, games: 10, max: 14)));          // the cap is not a whole number of blocks
        Assert.Equal("Seed.block_boundary_invalid", CodeOf(Manifest(block: 21, games: 14, max: 14)));         // a block larger than the cap
        Assert.Equal("Seed.block_incomplete", CodeOf(Manifest(block: 7, games: 10, max: 14)));                // a partial block
        Assert.Equal("Seed.cap_exceeded", CodeOf(Manifest(block: 7, games: 21, max: 14)));
        Assert.Equal("Seed.cap_exceeded", CodeOf(Manifest(block: 1, games: 1, max: 1_000_000)));               // beyond the six-digit game index
    }

    [Fact]
    public void The_pa_target_belongs_to_calibration_only_and_is_at_least_the_approved_100000()
    {
        Assert.Equal("Seed.pa_target_invalid", CodeOf(Manifest("fc3-cal", "fc3-cal-l1", paTarget: null)));
        Assert.Equal("Seed.pa_target_invalid", CodeOf(Manifest("fc3-cal", "fc3-cal-l1", paTarget: 99_999)));
        Assert.Equal("Seed.pa_target_invalid", CodeOf(Manifest("fc2-dry", "fc2-dry-x", paTarget: 100_000)));
        Assert.Equal("Seed.pa_target_invalid", CodeOf(Manifest("fc3-dom", "fc3-dom-x", paTarget: 100_000)));
        var (rel, sha) = Put(Manifest("fc3-cal", "fc3-cal-l1", paTarget: 100_000).ToJsonString());
        Assert.Equal(100_000, H.Ok(SeedManifestReader.Read(_root, rel, sha)).PaTarget);
    }

    [Fact]
    public void An_integration_manifest_is_exactly_one_hundred_games()
    {
        Assert.Equal("Seed.integration_size_invalid", CodeOf(Manifest("fc3-int", "fc3-int-a", games: 98, block: 7, max: 98)));
        var (rel, sha) = Put(Manifest("fc3-int", "fc3-int-a", games: 100, block: 10, max: 100).ToJsonString());
        Assert.Equal(100, H.Ok(SeedManifestReader.Read(_root, rel, sha)).Seeds.Count);
    }

    [Fact]
    public void The_block_stop_rule_finishes_the_block_in_progress_and_never_runs_past_the_seeds()
    {
        var (rel, sha) = Put(Manifest("fc3-cal", "fc3-cal-l1", games: 21, block: 7, max: 21, paTarget: 100_000).ToJsonString());
        var cal = H.Ok(SeedManifestReader.Read(_root, rel, sha));

        Assert.Equal(BlockDecision.Continue, cal.AfterGames(0, 0));
        Assert.Equal(BlockDecision.Continue, cal.AfterGames(3, 150_000));                     // target reached mid-block: the block is completed first (FC-D5.3)
        Assert.Equal(BlockDecision.StopTargetReached, cal.AfterGames(7, 150_000));
        Assert.Equal(BlockDecision.StopTargetReached, cal.AfterGames(14, 100_000));           // inclusive
        Assert.Equal(BlockDecision.Continue, cal.AfterGames(14, 99_999));
        Assert.Equal(BlockDecision.StopSeedsExhausted, cal.AfterGames(21, 99_999));            // the declared cap ends the run; the sample is then insufficient, no seed is added
        Assert.Throws<ArgumentOutOfRangeException>(() => cal.AfterGames(22, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => cal.AfterGames(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => cal.AfterGames(1, -1));

        var (rel2, sha2) = Put(Manifest().ToJsonString(), "dry.json");
        var dry = H.Ok(SeedManifestReader.Read(_root, rel2, sha2));                           // no PA target: every declared seed runs
        Assert.Equal(BlockDecision.Continue, dry.AfterGames(7, long.MaxValue));
        Assert.Equal(BlockDecision.StopSeedsExhausted, dry.AfterGames(14, 0));
    }
}
