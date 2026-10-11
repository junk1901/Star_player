using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>Phase 3-B1: legacy GameRules profiles are byte-for-byte unchanged and declare no called-pitch mode; gamerules-v2 profiles must declare it.</summary>
public class GameRulesV2ProfileTests
{
    // SHA-256 of the legacy canonical text, computed independently in Python from content/rules/game_rules_profiles.json BEFORE this phase changed any code
    // (the generator is documented in docs/reports/PHASE_3B1_REPORT.md). Any change here means a legacy hash moved.
    private static readonly Dictionary<string, string> LegacyHashes = new()
    {
        ["kbo-oriented-unverified-v0"] = "9deedbde13ee37719f1f12a9bb6451ad91e5dc870e34ff73eab667f9828dcdbc",
        ["test-dh-cap11"] = "877790af4840d1d4b57166ab14d6521a7acaf64a3bd1a24adbfa415feb09509b",
        ["test-no-dh-cap11"] = "7d0a675ce67d160f07a500312461991cf02eda2936de99ac7330abb08a4ba277",
        ["test-dh-unlimited"] = "14d9f4e0a474e8f6d662c6c22daa4af68b86212cf644318d2a5abdd51e085e81",
        ["test-dh-cap10"] = "32193437d60a40159f44cf23fb76900e9bc42b94a54bfc8b3ac03a8a4018c13c",
        ["test-dh-no-two-way"] = "921c23e0c0230c28f749764f83b28f992ad4b0cb1230e57cc3c6f2d1b0ee6520",
        ["test-dh-forfeit"] = "b402dd5cd96c087b1ffc277870d6086c4e46bbf8168dac09b68182f634001287",
    };

    private const string V2Resource = "game_rules_profiles_v2.json";

    private static IReadOnlyList<GameRuleSnapshot> Legacy()
    {
        var read = GameRulesJson.Read(ScriptedGameSource.ReadResource("game_rules_profiles.json"));
        Assert.True(read.IsSuccess, read.Error?.Message);
        return read.Value;
    }

    private static IReadOnlyList<GameRuleSnapshot> V2()
    {
        var read = GameRulesJson.Read(ScriptedGameSource.ReadResource(V2Resource));
        Assert.True(read.IsSuccess, read.Error?.Message);
        return read.Value;
    }

    private static string Mutate(string find, string replace)
    {
        var json = ScriptedGameSource.ReadResource(V2Resource).Replace("\r\n", "\n", StringComparison.Ordinal);
        var at = json.IndexOf(find, StringComparison.Ordinal);
        Assert.True(at >= 0, find);
        return json.Remove(at, find.Length).Insert(at, replace);
    }

    [Fact]
    public void Every_legacy_profile_keeps_its_independently_computed_content_hash()
    {
        var legacy = Legacy();

        Assert.Equal(LegacyHashes.Count, legacy.Count);
        foreach (var profile in legacy)
            Assert.Equal(LegacyHashes[profile.ProfileId], profile.ContentHash);
    }

    [Fact]
    public void Legacy_profiles_declare_no_called_pitch_mode_including_the_kbo_oriented_one_and_are_schema_v1()
    {
        Assert.All(Legacy(), p =>
        {
            Assert.Null(p.CalledPitchMode);
            Assert.Null(p.ZoneModel);
            Assert.False(p.CalledPitchModeDeclared);
            Assert.Equal(GameRuleSnapshot.SchemaV1, p.RulesSchema);
            Assert.False(p.FramingMayChangeCalls().IsSuccess);
        });
        Assert.Contains(Legacy(), p => p.Status == GameRulesStatus.KboOrientedUnverified);
    }

    [Fact]
    public void The_v2_fixture_declares_one_automated_and_one_non_automated_fictional_profile_with_the_zone_model()
    {
        var v2 = V2();

        Assert.Equal(2, v2.Count);
        Assert.All(v2, p =>
        {
            Assert.Equal(GameRulesStatus.FictionalTest, p.Status);
            Assert.Equal(ZoneModelVersion.Pzl1, p.ZoneModel);
            Assert.Equal(GameRuleSnapshot.SchemaV2, p.RulesSchema);
        });
        Assert.Equal(CalledPitchMode.Automated, Assert.Single(v2, p => p.ProfileId == "test-pzl-automated").CalledPitchMode);
        Assert.Equal(CalledPitchMode.NonAutomated, Assert.Single(v2, p => p.ProfileId == "test-pzl-non-automated").CalledPitchMode);
    }

    [Fact]
    public void The_v2_fixture_framing_contract_follows_its_mode_and_its_hashes_are_new_and_deterministic()
    {
        var first = V2();
        var second = V2();

        Assert.False(first.Single(p => p.CalledPitchMode == CalledPitchMode.Automated).FramingMayChangeCalls().Value);
        Assert.True(first.Single(p => p.CalledPitchMode == CalledPitchMode.NonAutomated).FramingMayChangeCalls().Value);
        Assert.Equal(first.Select(p => p.ContentHash), second.Select(p => p.ContentHash));
        Assert.Empty(first.Select(p => p.ContentHash).Intersect(LegacyHashes.Values));
        Assert.Equal(2, first.Select(p => p.ContentHash).Distinct().Count());
    }

    [Fact]
    public void The_v2_fixture_leaves_the_phase_2_rule_values_equal_to_the_matching_legacy_profile()
    {
        var legacy = Legacy().Single(p => p.ProfileId == "test-dh-cap11");

        Assert.All(V2(), p => Assert.Equal(
            (legacy.OutsPerHalfInning, legacy.BallsForWalk, legacy.StrikesForStrikeout, legacy.RegulationInnings, legacy.ExtraInningsLimit, legacy.TieAllowed, legacy.DesignatedHitter, legacy.TwoWayPlayerRule),
            (p.OutsPerHalfInning, p.BallsForWalk, p.StrikesForStrikeout, p.RegulationInnings, p.ExtraInningsLimit, p.TieAllowed, p.DesignatedHitter, p.TwoWayPlayerRule)));
    }

    [Fact]
    public void An_unknown_called_pitch_mode_is_rejected()
    {
        var read = GameRulesJson.Read(Mutate("\"called_pitch_mode\": \"automated\"", "\"called_pitch_mode\": \"abs_2026\""));

        Assert.False(read.IsSuccess);
        Assert.Equal("Fixture.enum_invalid", read.Error!.Value.Code);
    }

    [Fact]
    public void An_unknown_or_unsupported_zone_model_is_rejected()
    {
        var read = GameRulesJson.Read(Mutate("\"zone_model\": \"pzl-zones-v1\"", "\"zone_model\": \"pzl-zones-v2\""));

        Assert.False(read.IsSuccess);
        Assert.Equal("ZoneModelVersion.unsupported", read.Error!.Value.Code);
    }

    [Fact]
    public void A_v2_profile_missing_a_required_new_field_is_rejected()
    {
        var withoutMode = GameRulesJson.Read(Mutate("      \"called_pitch_mode\": \"automated\",\n", ""));
        var withoutZone = GameRulesJson.Read(Mutate(",\n      \"zone_model\": \"pzl-zones-v1\"\n    },\n    {\n      \"profile_id\": \"test-pzl-non", "\n    },\n    {\n      \"profile_id\": \"test-pzl-non"));

        Assert.False(withoutMode.IsSuccess);
        Assert.Equal("Fixture.json_invalid", withoutMode.Error!.Value.Code);
        Assert.False(withoutZone.IsSuccess);
        Assert.Equal("Fixture.json_invalid", withoutZone.Error!.Value.Code);
    }

    [Fact]
    public void A_legacy_format_file_that_sneaks_in_the_new_fields_is_rejected_so_a_legacy_profile_can_never_change_silently()
    {
        var legacyJson = ScriptedGameSource.ReadResource("game_rules_profiles.json");
        var needle = "\"rule_policy\": \"kbo-first-obr2026-fallback-v1\"";
        var at = legacyJson.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(at >= 0);
        var sneaked = legacyJson.Insert(at + needle.Length, ", \"called_pitch_mode\": \"automated\"");

        var read = GameRulesJson.Read(sneaked);

        Assert.False(read.IsSuccess);
        Assert.Equal("Fixture.json_invalid", read.Error!.Value.Code);
    }

    [Theory]
    [InlineData("\"format_version\": 2", "\"format_version\": 3", "Fixture.version_unsupported")]
    [InlineData("\"format_version\": 2", "\"format_version\": 1", "Fixture.json_invalid")]
    public void An_unsupported_or_mismatched_format_version_is_rejected(string find, string replace, string code)
    {
        var read = GameRulesJson.Read(Mutate(find, replace));

        Assert.False(read.IsSuccess);
        Assert.Equal(code, read.Error!.Value.Code);
    }

    [Fact]
    public void A_duplicate_v2_profile_id_is_rejected()
    {
        var read = GameRulesJson.Read(Mutate("\"profile_id\": \"test-pzl-non-automated\"", "\"profile_id\": \"test-pzl-automated\""));

        Assert.Equal("Fixture.duplicate_id", read.Error!.Value.Code);
    }

    [Fact]
    public void The_phase_2_diagnostic_games_still_load_against_the_untouched_legacy_pack()
    {
        var loaded = new ScriptedGameSource().Load();

        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        Assert.Equal(6, loaded.Value.Count);
        Assert.All(loaded.Value, g => Assert.False(g.Rules.CalledPitchModeDeclared));
    }

    [Fact]
    public void Malformed_json_in_either_format_gives_the_same_stable_code_as_before()
    {
        Assert.Equal("Fixture.json_invalid", GameRulesJson.Read("{ not json").Error!.Value.Code);
        Assert.Equal("Fixture.json_invalid", GameRulesJson.Read("[]").Error!.Value.Code);
        Assert.Equal("Fixture.json_invalid", GameRulesJson.Read("{}").Error!.Value.Code);
    }
}
