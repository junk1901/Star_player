using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>The embedded GameRules profile pack: honest status labels, open items preserved, bad data rejected, snapshot content hash stable.</summary>
public class GameRulesProfileTests
{
    private static IReadOnlyList<GameRuleSnapshot> Embedded()
    {
        var read = GameRulesJson.Read(ScriptedGameSource.ReadResource("game_rules_profiles.json"));
        Assert.True(read.IsSuccess, read.Error?.Message);
        return read.Value;
    }

    [Fact]
    public void The_kbo_oriented_profile_is_labelled_unverified_and_keeps_its_open_items_while_test_profiles_are_labelled_fictional()
    {
        var profiles = Embedded();
        var kbo = Assert.Single(profiles, p => p.Status == GameRulesStatus.KboOrientedUnverified);

        Assert.Equal("kbo-oriented-unverified-v0", kbo.ProfileId);
        Assert.NotEmpty(kbo.OpenItems);
        Assert.False(string.IsNullOrWhiteSpace(kbo.Provenance));
        Assert.All(profiles.Where(p => p != kbo), p => Assert.Equal(GameRulesStatus.FictionalTest, p.Status));
        Assert.Equal(7, profiles.Count);
    }

    [Fact]
    public void The_profile_content_hash_is_deterministic_and_differs_between_profiles()
    {
        var first = Embedded();
        var second = Embedded();

        Assert.Equal(first.Select(p => p.ContentHash), second.Select(p => p.ContentHash));
        Assert.Equal(first.Count, first.Select(p => p.ContentHash).Distinct().Count());
    }

    [Fact]
    public void The_exposed_open_items_cannot_be_changed_through_the_list_interface()
    {
        var items = (System.Collections.Generic.IList<string>)Embedded().Single(p => p.Status == GameRulesStatus.KboOrientedUnverified).OpenItems;

        Assert.Throws<NotSupportedException>(() => items.Add("x"));
        Assert.Throws<NotSupportedException>(items.Clear);
    }

    [Theory]
    [InlineData("\"tie_allowed\": true", "\"tie_allowed\": false", "GameRules.tie_inconsistent")]
    [InlineData("\"balls_for_walk\": 4", "\"balls_for_walk\": 0", "GameRules.value_invalid")]
    [InlineData("\"format_version\": 1", "\"format_version\": 9", "Fixture.version_unsupported")]
    public void A_corrupted_profile_pack_is_rejected_with_a_stable_code(string find, string replace, string code)
    {
        var json = ScriptedGameSource.ReadResource("game_rules_profiles.json");
        var at = json.IndexOf(find, StringComparison.Ordinal);
        Assert.True(at >= 0, find);

        var read = GameRulesJson.Read(json.Remove(at, find.Length).Insert(at, replace));

        Assert.False(read.IsSuccess);
        Assert.Equal(code, read.Error!.Value.Code);
    }

    [Fact]
    public void An_unknown_status_text_and_malformed_json_are_rejected()
    {
        var json = ScriptedGameSource.ReadResource("game_rules_profiles.json");

        Assert.False(GameRulesJson.Read(json.Replace("fictional_test", "official_2026", StringComparison.Ordinal)).IsSuccess);
        Assert.Equal("Fixture.json_invalid", GameRulesJson.Read("{ not json").Error!.Value.Code);
    }

    [Fact]
    public void The_embedded_diagnostic_games_load_against_the_embedded_profiles()
    {
        var loaded = new ScriptedGameSource().Load();

        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        Assert.Equal(6, loaded.Value.Count);
        Assert.All(loaded.Value, g => Assert.NotNull(g.Expected));
    }
}
