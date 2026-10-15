using System.Text.Json.Nodes;
using StarPlayer.Application.Ports;
using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Domain.Teams;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>The fictional two-club pack: scenarios, JSON round-trip of independent fields, and rejection of bad content.</summary>
public class FixtureWorldTests
{
    private static string Pack => FixturePlayerWorldSource.ReadEmbeddedPack();

    private static PlayerWorld World() => WorldJson.Read(Pack).Value;

    private static PlayerId P(string id) => PlayerId.Create(id).Value;

    private static PlayerSheet Sheet(PlayerWorld world, string id) => world.Players.Single(p => p.Id == P(id));

    /// <summary>Applies an edit to the parsed pack and returns the edited JSON text.</summary>
    private static string Edit(Action<JsonNode> edit)
    {
        var node = JsonNode.Parse(Pack)!;
        edit(node);
        return node.ToJsonString();
    }

    private static JsonObject Player(JsonNode root, string id) =>
        root["players"]!.AsArray().Single(p => p!["player_id"]!.GetValue<string>() == id)!.AsObject();

    private static string FirstErrorCode(string json) => WorldJson.Read(json).Error!.Value.Code;

    // ---- the pack itself ----

    [Fact]
    public void Embedded_pack_loads_two_clubs_and_ten_fictional_players()
    {
        var world = World();

        Assert.Equal(2, world.Clubs.Count);
        Assert.Equal(10, world.Players.Count);
        Assert.Equal(10, world.Development.Count);
        Assert.Equal(P("p-0001"), world.ControlledPlayer);
        Assert.Contains("가상", world.Label);
        Assert.All(world.Players, p => Assert.Contains("가상", p.Player.Biography));
    }

    [Fact]
    public void Source_port_returns_the_same_world_as_reading_the_text_directly()
    {
        var viaPort = new FixturePlayerWorldSource().Load();
        var viaText = new FixturePlayerWorldSource(Pack).Load();

        Assert.True(viaPort.IsSuccess);
        Assert.Equal(WorldJson.Write(viaPort.Value), WorldJson.Write(viaText.Value));
    }

    [Fact]
    public void Scenarios_hitter_pitcher_two_way_and_same_name_are_all_present()
    {
        var world = World();

        Assert.Equal(PlayerKind.Hitter, Sheet(world, "p-0001").Kind);
        Assert.Equal(PlayerKind.Pitcher, Sheet(world, "p-0003").Kind);
        var twoWay = Sheet(world, "p-0004");
        Assert.Equal(PlayerKind.TwoWay, twoWay.Kind);
        Assert.NotNull(twoWay.Ratings.Hitter);
        Assert.NotNull(twoWay.Ratings.Pitcher);
        Assert.Equal(FieldPosition.Pitcher, twoWay.Proficiency.PreferredPosition);
        Assert.True(twoWay.Proficiency.RoleProficiency(PlayerRole.Batter).Value > 0 && twoWay.Proficiency.RoleProficiency(PlayerRole.StartingPitcher).Value > 0);

        var same = world.Players.Where(p => p.Player.DisplayName == "박민수").ToList();
        Assert.Equal(2, same.Count);
        Assert.NotEqual(same[0].Id, same[1].Id);
        Assert.NotEqual(same[0].Status.CurrentAffiliation!.Club, same[1].Status.CurrentAffiliation!.Club);
    }

    [Fact]
    public void Catchers_with_opposite_tool_and_c_proficiency_combinations_are_present()
    {
        var world = World();
        var eliteToolsNoviceC = Sheet(world, "p-0006");
        var averageToolsExpertC = Sheet(world, "p-0007");

        Assert.True(eliteToolsNoviceC.Ratings.Catcher!.ReceivingFraming.Value > averageToolsExpertC.Ratings.Catcher!.ReceivingFraming.Value);
        Assert.True(eliteToolsNoviceC.Proficiency.PositionProficiency(FieldPosition.Catcher).Value < averageToolsExpertC.Proficiency.PositionProficiency(FieldPosition.Catcher).Value);
        Assert.True(eliteToolsNoviceC.Ratings.Catcher!.PopTime.Value >= 90 && eliteToolsNoviceC.Proficiency.PositionProficiency(FieldPosition.Catcher).Value <= 20);
        Assert.True(averageToolsExpertC.Ratings.Catcher!.PopTime.Value <= 60 && averageToolsExpertC.Proficiency.PositionProficiency(FieldPosition.Catcher).Value >= 85);
    }

    [Fact]
    public void One_catcher_and_three_different_pitchers_give_three_distinct_neutral_pairs()
    {
        var pairs = World().Batteries.Where(b => b.Pair.Catcher == P("p-0006")).ToList();

        Assert.Equal(3, pairs.Count);
        Assert.Equal(3, pairs.Select(b => b.Pair).Distinct().Count());
        Assert.Equal(3, pairs.Select(b => b.Pair.Pitcher).Distinct().Count());
        Assert.All(pairs, b => Assert.True(b.IsNeutral));
    }

    [Fact]
    public void Development_profiles_are_a_separate_list_and_the_sheets_do_not_contain_them()
    {
        var world = World();

        Assert.All(world.Players, sheet => Assert.Single(world.Development, d => d.PlayerId == sheet.Id));
        Assert.DoesNotContain(typeof(PlayerSheet).GetProperties(), p => p.PropertyType == typeof(DevelopmentProfile));
        var json = JsonNode.Parse(Pack)!;
        Assert.All(json["players"]!.AsArray(), p => Assert.False(p!.AsObject().ContainsKey("development")));
    }

    [Fact]
    public void Jersey_numbers_are_dated_per_club_rows_and_history_survives_a_later_retirement()
    {
        var jerseys = World().Jerseys;
        var hangang = ClubId.Create("club-hangang").Value;
        var changed = jerseys.History(P("p-0002"));
        var wasWearingOne = jerseys.History(P("p-0008"));

        Assert.Equal([17, 6], changed.Select(a => a.Number.Value));
        Assert.Equal(changed[0].EffectiveTo, changed[1].EffectiveFrom);
        Assert.Equal(6, jerseys.ActiveOn(P("p-0002"), DateOnly.Parse("2030-03-01")).Value!.Number.Value);
        Assert.Equal(17, jerseys.ActiveOn(P("p-0002"), DateOnly.Parse("2027-01-01")).Value!.Number.Value);
        Assert.Equal(1, wasWearingOne[0].Number.Value);
        Assert.True(jerseys.IsRestricted(hangang, JerseyNumber.Create(1).Value, DateOnly.Parse("2030-03-01")));
        Assert.DoesNotContain(JerseyNumber.Create(1).Value, jerseys.AvailableNumbers(hangang, DateOnly.Parse("2030-03-01")));
    }

    [Fact]
    public void Stadium_profiles_are_versioned_and_inside_the_cap()
    {
        var world = World();
        var starlight = world.ParkFactorProfiles.Where(p => p.Stadium.Value == "stadium-starlight").OrderBy(p => p.Version).ToList();

        Assert.Equal([1, 2], starlight.Select(p => p.Version));
        Assert.All(world.ParkFactorProfiles, p => Assert.InRange(p.HomeRuns.Permille, world.ParkFactorBounds.MinPermille, world.ParkFactorBounds.MaxPermille));
        Assert.Equal("pf-starlight", starlight[0].Id.Value);
    }

    [Fact]
    public void Stadium_rename_survives_a_json_round_trip_with_the_same_id_and_profile_links()
    {
        var world = World();
        var stadium = world.Stadiums.First();
        var renamed = stadium.WithPresentation(stadium.Presentation with { DisplayName = "새 이름 스타디움" }).Value;
        var edited = world with { Stadiums = world.Stadiums.Select(s => s.Id == stadium.Id ? renamed : s).ToList() };

        var reread = WorldJson.Read(WorldJson.Write(edited)).Value;

        Assert.Equal(stadium.Id, reread.Stadiums.First().Id);
        Assert.Equal("새 이름 스타디움", reread.Stadiums.First().Presentation.DisplayName);
        Assert.Equal(world.ParkFactorProfiles.Count, reread.ParkFactorProfiles.Count);
        Assert.All(reread.ParkFactorProfiles, p => Assert.Contains(reread.Stadiums, s => s.Id == p.Stadium));
    }

    // ---- JSON round-trip ----

    [Fact]
    public void Pack_round_trips_to_identical_json()
    {
        var written = WorldJson.Write(World());

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Pack), JsonNode.Parse(written)), "write(read(pack)) differs from the pack");
        Assert.Equal(written, WorldJson.Write(WorldJson.Read(written).Value));
    }

    [Fact]
    public void Changing_only_state_changes_only_state_in_the_round_trip()
    {
        var edited = Edit(root => Player(root, "p-0001")["state"]!["fatigue"] = 99);

        var before = JsonNode.Parse(WorldJson.Write(World()))!;
        var after = JsonNode.Parse(WorldJson.Write(WorldJson.Read(edited).Value))!;
        var changedPlayer = Player(after, "p-0001");

        Assert.Equal(99, changedPlayer["state"]!["fatigue"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(Player(before, "p-0001")["ratings"], changedPlayer["ratings"]));
        Assert.True(JsonNode.DeepEquals(Player(before, "p-0001")["proficiency"], changedPlayer["proficiency"]));
        Assert.True(JsonNode.DeepEquals(before["development_profiles"], after["development_profiles"]));
        Assert.True(JsonNode.DeepEquals(before["jersey_assignments"], after["jersey_assignments"]));
    }

    [Fact]
    public void Changing_only_the_hidden_profile_leaves_every_player_record_untouched()
    {
        var edited = Edit(root =>
        {
            var profile = root["development_profiles"]!.AsArray().Single(d => d!["player_id"]!.GetValue<string>() == "p-0001")!;
            profile["true_potential"] = 99;
            profile["growth_type"] = "early";
        });

        var before = JsonNode.Parse(WorldJson.Write(World()))!;
        var after = JsonNode.Parse(WorldJson.Write(WorldJson.Read(edited).Value))!;

        Assert.True(JsonNode.DeepEquals(before["players"], after["players"]));
        Assert.False(JsonNode.DeepEquals(before["development_profiles"], after["development_profiles"]));
    }

    [Fact]
    public void Changing_only_proficiency_leaves_ratings_and_state_untouched()
    {
        var edited = Edit(root => Player(root, "p-0006")["proficiency"]!["positions"]!.AsArray()
            .Single(x => x!["position"]!.GetValue<string>() == "catcher")!["value"] = 77);

        var before = Player(JsonNode.Parse(WorldJson.Write(World()))!, "p-0006");
        var after = Player(JsonNode.Parse(WorldJson.Write(WorldJson.Read(edited).Value))!, "p-0006");

        Assert.True(JsonNode.DeepEquals(before["ratings"], after["ratings"]));
        Assert.True(JsonNode.DeepEquals(before["state"], after["state"]));
        Assert.False(JsonNode.DeepEquals(before["proficiency"], after["proficiency"]));
    }

    [Fact]
    public void Hitter_pitcher_and_two_way_serialize_with_the_right_rating_sets()
    {
        var players = JsonNode.Parse(WorldJson.Write(World()))!["players"]!.AsArray();
        JsonNode Ratings(string id) => players.Single(p => p!["player_id"]!.GetValue<string>() == id)!["ratings"]!;

        Assert.NotNull(Ratings("p-0001")["hitter"]);
        Assert.Null(Ratings("p-0001")["pitcher"]);
        Assert.Null(Ratings("p-0003")["hitter"]);
        Assert.NotNull(Ratings("p-0003")["pitcher"]);
        Assert.NotNull(Ratings("p-0004")["hitter"]);
        Assert.NotNull(Ratings("p-0004")["pitcher"]);
        Assert.Equal(6, Ratings("p-0004")["hitter"]!.AsObject().Count);
        Assert.Equal(5, Ratings("p-0004")["pitcher"]!.AsObject().Count);
        Assert.Equal(4, Ratings("p-0006")["catcher"]!.AsObject().Count);
    }

    [Fact]
    public void Json_field_names_are_snake_case()
    {
        var text = WorldJson.Write(World());

        Assert.Contains("\"receiving_framing\"", text);
        Assert.Contains("\"as_of_date\"", text);
        Assert.Contains("\"home_run_permille\"", text);
        Assert.DoesNotContain("\"ReceivingFraming\"", text);
        Assert.Contains("\"center_field\"", text);
    }

    // ---- rejection of bad content (no partial world, located error) ----

    [Fact]
    public void Skill_over_100_is_rejected_with_the_player_located()
    {
        var result = WorldJson.Read(Edit(root => Player(root, "p-0001")["ratings"]!["hitter"]!["contact"] = 101));

        Assert.False(result.IsSuccess);
        Assert.Equal("SkillValue.out_of_range", result.Error!.Value.Code);
        Assert.Contains("p-0001", result.Error.Value.Message);
    }

    [Fact]
    public void Skill_below_zero_is_rejected()
        => Assert.Equal("SkillValue.out_of_range", FirstErrorCode(Edit(root => Player(root, "p-0003")["ratings"]!["pitcher"]!["stuff"] = -1)));

    [Fact]
    public void Unknown_rule_version_in_the_profile_is_rejected_explicitly()
        => Assert.Equal("RuleCatalog.version_not_found", FirstErrorCode(Edit(root => root["rules"]!["profile"]!["areas"]![0]!["version"] = "fixture-game-v9")));

    [Fact]
    public void Park_factor_outside_the_cap_is_rejected()
        => Assert.Equal("ParkFactor.out_of_bounds", FirstErrorCode(Edit(root => root["park_factor_profiles"]![0]!["home_run_permille"] = 1300)));

    [Fact]
    public void Duplicate_jersey_number_at_one_club_is_rejected()
        => Assert.Equal("JerseyNumberLedger.number_in_use", FirstErrorCode(Edit(root =>
            root["jersey_assignments"]!.AsArray().Single(a => a!["player_id"]!.GetValue<string>() == "p-0005")!["number"] = 27)));

    [Fact]
    public void Retired_number_assigned_after_retirement_is_rejected()
        => Assert.Equal("JerseyNumberLedger.retired_number", FirstErrorCode(Edit(root =>
        {
            var row = root["jersey_assignments"]!.AsArray().Single(a => a!["player_id"]!.GetValue<string>() == "p-0003")!;
            row["number"] = 1;
            row["effective_from"] = "2030-01-01"; // after the 2029 retirement and after the previous #1 holder left
        })));

    [Fact]
    public void Number_outside_the_rule_range_is_rejected()
        => Assert.Equal("JerseyNumberLedger.out_of_range", FirstErrorCode(Edit(root =>
            root["jersey_assignments"]!.AsArray().Single(a => a!["player_id"]!.GetValue<string>() == "p-0003")!["number"] = 100)));

    [Fact]
    public void Duplicate_player_id_and_missing_development_profile_are_rejected()
    {
        Assert.Equal("Fixture.duplicate_id", FirstErrorCode(Edit(root => Player(root, "p-0002")["player_id"] = "p-0001")));
        Assert.Equal("Fixture.development_missing", FirstErrorCode(Edit(root => root["development_profiles"]!.AsArray().RemoveAt(0))));
    }

    [Fact]
    public void Ceiling_below_current_skill_is_rejected()
        => Assert.Equal("DevelopmentProfile.ceiling_below_current", FirstErrorCode(Edit(root =>
            root["development_profiles"]![0]!["skill_ceilings"]![0]!["ceiling"] = 10)));

    [Fact]
    public void Unknown_references_and_enum_values_are_rejected()
    {
        Assert.Equal("Fixture.unknown_reference", FirstErrorCode(Edit(root => root["controlled_player_id"] = "p-9999")));
        Assert.Equal("Fixture.enum_invalid", FirstErrorCode(Edit(root => Player(root, "p-0001")["throws"] = "both")));
        Assert.Equal("Fixture.battery_catcher_invalid", FirstErrorCode(Edit(root => root["battery_pairs"]![0]!["catcher_id"] = "p-0001")));
        Assert.Equal("Fixture.battery_pitcher_invalid", FirstErrorCode(Edit(root => root["battery_pairs"]![0]!["pitcher_id"] = "p-0001")));
        Assert.Equal("Fixture.duplicate_id", FirstErrorCode(Edit(root => root["battery_pairs"]!.AsArray().Add(root["battery_pairs"]![0]!.DeepClone()))));
    }

    [Fact]
    public void Structurally_bad_json_is_a_typed_error_not_an_exception()
    {
        Assert.Equal("Fixture.json_invalid", FirstErrorCode("{ not json"));
        Assert.Equal("Fixture.json_invalid", FirstErrorCode("null"));
        Assert.Equal("Fixture.json_invalid", FirstErrorCode(Edit(root => root.AsObject().Remove("clubs"))));
        Assert.Equal("Fixture.json_invalid", FirstErrorCode(Edit(root => root.AsObject()["surprise_field"] = 1)));
        // Phase 3-D: 2 became a supported version (DataPack v2, WorldTendencyV2Tests); the unsupported neighbours stay refused
        Assert.Equal("Fixture.version_unsupported", FirstErrorCode(Edit(root => root["format_version"] = 3)));
        Assert.Equal("Fixture.version_unsupported", FirstErrorCode(Edit(root => root["format_version"] = 0)));
    }

    // ---- Codex review: rule snapshot and cross-club jersey data through the pack ----

    [Fact]
    public void Rule_snapshot_serializes_identically_every_time_and_regardless_of_input_order()
    {
        var reordered = Edit(root =>
        {
            var catalog = root["rules"]!["catalog"]!.AsArray();
            var items = catalog.Select(n => n!.DeepClone()).Reverse().ToList();
            catalog.Clear();
            items.ForEach(catalog.Add);
            var areas = root["rules"]!["profile"]!["areas"]!.AsArray();
            var refs = areas.Select(n => n!.DeepClone()).Reverse().ToList();
            areas.Clear();
            refs.ForEach(areas.Add);
        });

        var first = JsonNode.Parse(WorldJson.Write(World()))!["rules"];
        var second = JsonNode.Parse(WorldJson.Write(World()))!["rules"];
        var fromReordered = JsonNode.Parse(WorldJson.Write(WorldJson.Read(reordered).Value))!["rules"];

        Assert.True(JsonNode.DeepEquals(first, second));
        Assert.True(JsonNode.DeepEquals(first, fromReordered), "rule snapshot text depends on input order");
        Assert.Equal("no-rule-content", first!["catalog"]![0]!["simplifications"]![0]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void Invalid_rule_definitions_in_the_pack_are_rejected_with_typed_errors()
    {
        static JsonNode Catalog0(JsonNode root) => root["rules"]!["catalog"]![0]!;

        Assert.Equal("RuleSimplification.description_invalid", FirstErrorCode(Edit(root => Catalog0(root)["simplifications"]![0]!["description"] = " ")));
        Assert.Equal("RuleSimplification.id_invalid", FirstErrorCode(Edit(root => Catalog0(root)["simplifications"]![0]!["id"] = "bad id")));
        Assert.Equal("RuleSimplification.duplicate_id", FirstErrorCode(Edit(root => Catalog0(root)["simplifications"]!.AsArray().Add(Catalog0(root)["simplifications"]![0]!.DeepClone()))));
        Assert.Equal("Fixture.json_invalid", FirstErrorCode(Edit(root => Catalog0(root)["simplifications"]![0]!.AsObject().Remove("description"))));
        Assert.Equal("Fixture.json_invalid", FirstErrorCode(Edit(root => Catalog0(root).AsObject().Remove("simplifications"))));
        Assert.Equal("RuleVersionId.empty", FirstErrorCode(Edit(root => Catalog0(root)["version"] = "")));
        Assert.Equal("RuleVersionId.empty", FirstErrorCode(Edit(root => root["rules"]!["profile"]!["areas"]![0]!["version"] = " ")));
        Assert.Equal("RuleCatalog.overlapping_versions", FirstErrorCode(Edit(root =>
        {
            var clone = Catalog0(root).DeepClone();
            clone["version"] = "fixture-game-v2";
            root["rules"]!["catalog"]!.AsArray().Add(clone);
        })));
        Assert.Equal("RuleCatalog.provenance_missing", FirstErrorCode(Edit(root => Catalog0(root)["provenance"] = "")));
    }

    [Fact]
    public void Same_player_holding_numbers_at_two_clubs_over_overlapping_periods_is_rejected_by_the_pack_loader()
        => Assert.Equal("JerseyNumberLedger.player_at_other_club", FirstErrorCode(Edit(root =>
            root["jersey_assignments"]!.AsArray().Add(JsonNode.Parse(
                "{\"player_id\":\"p-0003\",\"club_id\":\"club-namhae\",\"number\":50,\"effective_from\":\"2030-01-01\",\"effective_to\":null,\"reason\":\"transfer\"}")))));

    [Fact]
    public void A_clean_transfer_between_clubs_loads_and_keeps_both_rows()
    {
        var json = Edit(root =>
        {
            var rows = root["jersey_assignments"]!.AsArray();
            var row = rows.Single(a => a!["player_id"]!.GetValue<string>() == "p-0003")!;
            row["effective_to"] = "2029-01-01";
            rows.Add(JsonNode.Parse("{\"player_id\":\"p-0003\",\"club_id\":\"club-namhae\",\"number\":50,\"effective_from\":\"2029-01-01\",\"effective_to\":null,\"reason\":\"transfer\"}"));
            var affiliations = Player(root, "p-0003")["status"]!["affiliations"]!.AsArray();
            affiliations[0]!["to"] = "2029-01-01";
            affiliations.Add(JsonNode.Parse("{\"club_id\":\"club-namhae\",\"from\":\"2029-01-01\",\"to\":null}"));
        });

        var world = WorldJson.Read(json).Value;

        Assert.Equal([18, 50], world.Jerseys.History(P("p-0003")).Select(a => a.Number.Value));
        Assert.Equal("club-namhae", world.Jerseys.ActiveOn(P("p-0003"), DateOnly.Parse("2030-03-01")).Value!.Club.Value);
    }

    [Fact]
    public void Source_port_reports_a_load_failure_as_a_result()
    {
        var failed = new FixturePlayerWorldSource("{ nope").Load();

        Assert.False(failed.IsSuccess);
        Assert.StartsWith("Fixture.", failed.Error!.Value.Code);
    }
}
