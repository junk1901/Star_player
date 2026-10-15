using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.Application.Ports;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;
using StarPlayer.Infrastructure.Fixtures;
using static StarPlayer.Infrastructure.Tests.TendencyPackFixture;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-D (BatterTendency v2, D-T1 / D-T4): DataPack format_version 2 carries the optional hidden <c>batter_zone_tendencies</c> collection. v1 stays exactly as it was (read, write, strict
/// fields); v2 omitted / empty / partial / full each write back in their own shape and order; every corrupt row is a typed error and the world is never created (nothing is repaired).
/// </summary>
public class WorldTendencyV2Tests
{
    private static PlayerId P(string id) => PlayerId.Create(id).Value;

    private static PlayerWorld Read(string json)
    {
        var read = WorldJson.Read(json);
        Assert.True(read.IsSuccess, read.Error?.Code + " " + read.Error?.Message);
        return read.Value;
    }

    private static string Code(string json) => WorldJson.Read(json).Error!.Value.Code;

    private static JsonNode Parse(string json) => JsonNode.Parse(json)!;

    // ---------------------------------------------------------------- v1: unchanged

    [Fact]
    public void The_official_v1_pack_is_unchanged_and_loads_as_v1_with_every_player_neutral()
    {
        var pack = FixturePlayerWorldSource.ReadEmbeddedPack();
        var world = Read(pack);

        Assert.Equal(1, Parse(pack)["format_version"]!.GetValue<int>());
        Assert.Null(Parse(pack)["batter_zone_tendencies"]);
        Assert.Equal(new WorldFormat(1, false), world.Format);
        Assert.Equal(0, world.BatterTendencies.Count);
        Assert.All(world.Players, p => Assert.Same(BatterZoneTendency.Neutral, world.BatterTendencies.TendencyFor(p.Id)));
    }

    [Fact]
    public void A_v1_pack_is_written_back_as_v1_without_any_v2_member_and_is_byte_stable()
    {
        var pack = FixturePlayerWorldSource.ReadEmbeddedPack();
        var written = WorldJson.Write(Read(pack));

        Assert.True(JsonNode.DeepEquals(Parse(pack), Parse(written)));
        Assert.Equal(1, Parse(written)["format_version"]!.GetValue<int>());
        Assert.DoesNotContain("batter_zone_tendencies", written, StringComparison.Ordinal);
        Assert.Equal(written, WorldJson.Write(Read(written)));
    }

    [Fact]
    public void A_v1_pack_with_the_new_field_is_rejected_whatever_the_field_holds()
    {
        foreach (var value in new JsonNode?[] { new JsonArray(), new JsonArray(Row("p-0001", Pull)), null })
        {
            var json = TendencyPackFixture.Omitted(root =>
            {
                root["format_version"] = 1;
                root["batter_zone_tendencies"] = value?.DeepClone();
            });

            Assert.Equal("Fixture.json_invalid", Code(json));
        }
    }

    [Fact]
    public void Unsupported_versions_are_refused_and_never_converted()
    {
        foreach (var version in new[] { 0, 3, 9, -1 })
            Assert.Equal("Fixture.version_unsupported", Code(Omitted(root => root["format_version"] = version)));
        // a version that is not an integer is not a version
        Assert.Equal("Fixture.json_invalid", Code(Omitted(root => root["format_version"] = "2")));
        Assert.Equal("Fixture.json_invalid", Code(Omitted(root => root["format_version"] = 2.5)));
        Assert.Equal("Fixture.json_invalid", Code(Omitted(root => root.AsObject().Remove("format_version"))));
        Assert.Equal("Fixture.json_invalid", Code("[]"));
        Assert.Equal("Fixture.json_invalid", Code("null"));
        Assert.Equal("Fixture.json_invalid", Code(""));
        // the v2 field with an unsupported version is still an unsupported version, not a v2 read
        Assert.Equal("Fixture.version_unsupported", Code(V2([Row("p-0001", Pull)], root => root["format_version"] = 3)));
    }

    [Fact]
    public void The_shared_serializer_options_are_not_relaxed_for_any_loader()
    {
        Assert.Equal(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow, WorldJson.Options.UnmappedMemberHandling);
        Assert.Equal(System.Text.Json.Serialization.JsonIgnoreCondition.Never, WorldJson.Options.DefaultIgnoreCondition);
        Assert.Equal(JsonNamingPolicy.SnakeCaseLower, WorldJson.Options.PropertyNamingPolicy);
        Assert.True(WorldJson.Options.WriteIndented);
    }

    // ---------------------------------------------------------------- v2: shapes and round trip

    [Fact]
    public void V2_with_the_collection_omitted_means_everyone_neutral_and_writes_back_omitted()
    {
        var json = Omitted();
        var world = Read(json);

        Assert.Equal(new WorldFormat(2, false), world.Format);
        Assert.Equal(0, world.BatterTendencies.Count);
        Assert.Same(BatterZoneTendency.Neutral, world.BatterTendencies.TendencyFor(P("p-0001")));

        var written = WorldJson.Write(world);
        Assert.Equal(2, Parse(written)["format_version"]!.GetValue<int>());
        Assert.DoesNotContain("batter_zone_tendencies", written, StringComparison.Ordinal);
        Assert.True(JsonNode.DeepEquals(Parse(json), Parse(written)));
        Assert.Equal(written, WorldJson.Write(Read(written)));
    }

    [Fact]
    public void V2_with_an_empty_array_means_everyone_neutral_and_writes_back_as_v2_with_an_empty_array_never_as_v1()
    {
        var json = V2(new List<JsonNode>());
        var world = Read(json);

        Assert.Equal(new WorldFormat(2, true), world.Format);
        Assert.Equal(0, world.BatterTendencies.Count);
        Assert.Same(BatterZoneTendency.Neutral, world.BatterTendencies.TendencyFor(P("p-0001")));

        var written = WorldJson.Write(world);
        var node = Parse(written);
        Assert.Equal(2, node["format_version"]!.GetValue<int>());
        Assert.Empty(node["batter_zone_tendencies"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(Parse(json), node));
        Assert.Equal(written, WorldJson.Write(Read(written)));
    }

    [Fact]
    public void The_omitted_and_the_empty_shapes_are_different_files_with_the_same_meaning()
    {
        var omitted = Read(Omitted());
        var empty = Read(V2(new List<JsonNode>()));

        Assert.NotEqual(omitted.Format, empty.Format);
        Assert.NotEqual(WorldJson.Write(omitted), WorldJson.Write(empty));
        Assert.Equal(omitted.BatterTendencies.Count, empty.BatterTendencies.Count);
    }

    [Fact]
    public void V2_with_some_profiles_applies_them_to_those_players_only_and_keeps_rows_and_order()
    {
        var json = V2(Row("p-0005", Away), Row("p-0001", Pull));
        var world = Read(json);

        Assert.Equal(new WorldFormat(2, true), world.Format);
        Assert.Equal(["p-0005", "p-0001"], world.BatterTendencies.Entries.Select(e => e.Player.Value)); // source order, not sorted
        Assert.Equal(Pull, world.BatterTendencies.TendencyFor(P("p-0001")).Values);
        Assert.Equal(Away, world.BatterTendencies.TendencyFor(P("p-0005")).Values);
        Assert.Same(BatterZoneTendency.Neutral, world.BatterTendencies.TendencyFor(P("p-0002"))); // no row: neutral
        Assert.Same(BatterZoneTendency.Neutral, world.BatterTendencies.TendencyFor(P("p-0004")));

        var written = WorldJson.Write(world);
        Assert.True(JsonNode.DeepEquals(Parse(json), Parse(written)));
        Assert.Equal(["p-0005", "p-0001"], Parse(written)["batter_zone_tendencies"]!.AsArray().Select(r => r!["player_id"]!.GetValue<string>()));
        Assert.Equal(written, WorldJson.Write(Read(written)));
    }

    [Fact]
    public void V2_with_a_profile_for_every_hitter_round_trips_exactly_including_an_explicit_all_zero_row()
    {
        var rows = new[]
        {
            Row("p-0007", CenterMinus), Row("p-0001", Pull), Row("p-0002", new int[9]), Row("p-0004", CenterPlus), Row("p-0005", Away), Row("p-0006", Pull),
        };
        var json = V2(rows);
        var world = Read(json);
        var written = WorldJson.Write(world);

        Assert.Equal(6, world.BatterTendencies.Count);
        Assert.True(world.BatterTendencies.Contains(P("p-0002")));
        Assert.True(world.BatterTendencies.TendencyFor(P("p-0002")).IsNeutral);
        Assert.True(JsonNode.DeepEquals(Parse(json), Parse(written)));
        Assert.Equal(written, WorldJson.Write(Read(written)));
        // the rest of the world is untouched by the tendencies
        Assert.True(JsonNode.DeepEquals(Parse(WorldJson.Write(Read(FixturePlayerWorldSource.ReadEmbeddedPack())))["players"], Parse(written)["players"]));
        Assert.True(JsonNode.DeepEquals(Parse(FixturePlayerWorldSource.ReadEmbeddedPack())["development_profiles"], Parse(written)["development_profiles"]));
    }

    [Fact]
    public void A_v2_world_loaded_through_the_source_port_is_the_same_as_reading_the_text()
    {
        var json = V2(Row("p-0001", Pull));
        var viaPort = new FixturePlayerWorldSource(json).Load();

        Assert.True(viaPort.IsSuccess);
        Assert.Equal(WorldJson.Write(Read(json)), WorldJson.Write(viaPort.Value));
        Assert.Equal(Pull, viaPort.Value.BatterTendencies.TendencyFor(P("p-0001")).Values);
    }

    [Fact]
    public void Writing_never_silently_drops_or_invents_hidden_data()
    {
        var v1 = Read(FixturePlayerWorldSource.ReadEmbeddedPack());
        var v2 = Read(V2(Row("p-0001", Pull)));

        // a v1 world holding tendencies, or a world that claims "no list" while holding some, is a programming error
        Assert.Throws<InvalidOperationException>(() => WorldJson.Write(v1 with { BatterTendencies = v2.BatterTendencies }));
        Assert.Throws<InvalidOperationException>(() => WorldJson.Write(v1 with { Format = new WorldFormat(1, true) }));
        Assert.Throws<InvalidOperationException>(() => WorldJson.Write(v2 with { Format = new WorldFormat(2, false) }));
        Assert.Throws<InvalidOperationException>(() => WorldJson.Write(v2 with { Format = new WorldFormat(7, true) }));
    }

    // ---------------------------------------------------------------- v2: corrupt data is never repaired

    [Fact]
    public void An_explicit_null_collection_and_a_non_array_collection_are_rejected()
    {
        Assert.Equal("Fixture.json_invalid", Code(Omitted(root => root["batter_zone_tendencies"] = null)));
        Assert.Equal("Fixture.json_invalid", Code(Omitted(root => root["batter_zone_tendencies"] = new JsonObject())));
        Assert.Equal("Fixture.json_invalid", Code(Omitted(root => root["batter_zone_tendencies"] = "none")));
        Assert.Equal("Fixture.json_invalid", Code(Omitted(root => root["batter_zone_tendencies"] = 0)));
        Assert.Contains("batter_zone_tendencies", WorldJson.Read(Omitted(root => root["batter_zone_tendencies"] = null)).Error!.Value.Message);
    }

    [Fact]
    public void A_null_row_and_null_or_missing_fields_are_rejected_with_a_typed_error()
    {
        Assert.Equal("Fixture.json_invalid", Code(Omitted(root => root["batter_zone_tendencies"] = new JsonArray(Row("p-0001", Pull), null))));

        foreach (var field in new[] { "player_id", "zone_model", "reference_id", "values" })
        {
            var asNull = Row("p-0001", Pull);
            asNull[field] = null;
            Assert.Equal("Fixture.field_missing", Code(V2(asNull)));

            var removed = Row("p-0001", Pull);
            removed.Remove(field);
            Assert.Equal("Fixture.json_invalid", Code(V2(removed)));
        }
    }

    [Fact]
    public void Wrong_json_types_and_unknown_members_are_rejected()
    {
        JsonObject With(string field, JsonNode? value) { var row = Row("p-0001", Pull); row[field] = value; return row; }

        Assert.Equal("Fixture.json_invalid", Code(V2(With("values", "60,-60"))));
        Assert.Equal("Fixture.json_invalid", Code(V2(With("values", new JsonObject()))));
        Assert.Equal("Fixture.json_invalid", Code(V2(With("values", new JsonArray(1.5, -1.5, 0, 0, 0, 0, 0, 0, 0)))));
        Assert.Equal("Fixture.json_invalid", Code(V2(With("values", new JsonArray("1", "-1", "0", "0", "0", "0", "0", "0", "0")))));
        Assert.Equal("Fixture.json_invalid", Code(V2(With("values", new JsonArray(null, 0, 0, 0, 0, 0, 0, 0, 0)))));
        Assert.Equal("Fixture.json_invalid", Code(V2(With("player_id", 1))));
        Assert.Equal("Fixture.json_invalid", Code(V2(With("zone_model", true))));
        Assert.Equal("Fixture.json_invalid", Code(V2(With("reference_id", new JsonArray()))));
        Assert.Equal("Fixture.json_invalid", Code(V2(With("surprise", 1)))); // unknown member inside a row
        Assert.Equal("Fixture.json_invalid", Code(Omitted(root => root["surprise"] = 1)));
        Assert.Equal("Fixture.json_invalid", Code(V2(new JsonArray(Row("p-0001", Pull)).DeepClone())));
    }

    [Fact]
    public void A_repeated_unknown_and_non_hitter_player_is_rejected()
    {
        Assert.Equal("Fixture.duplicate_id", Code(V2(Row("p-0001", Pull), Row("p-0002", Away), Row("p-0001", Away))));
        Assert.Equal("Fixture.duplicate_id", Code(V2(Row("p-0001", Pull), Row("p-0001", Pull))));
        Assert.Equal("Fixture.unknown_reference", Code(V2(Row("p-9999", Pull))));
        Assert.Equal("Fixture.tendency_batter_invalid", Code(V2(Row("p-0003", Pull)))); // a pure pitcher has no hitting ratings
        Assert.Equal("Fixture.tendency_batter_invalid", Code(V2(Row("p-0010", Pull))));
        Assert.Equal("PlayerId.invalid_character", Code(V2(Row("p 0001", Pull))));
        Assert.Equal("PlayerId.empty", Code(V2(Row("", Pull))));
        // the two-way player and a catcher have hitting ratings
        Assert.Equal(2, Read(V2(Row("p-0004", Pull), Row("p-0006", Away))).BatterTendencies.Count);
    }

    [Fact]
    public void An_unregistered_model_or_reference_and_a_malformed_vector_are_rejected_without_repair()
    {
        Assert.Equal("ZoneModelVersion.unsupported", Code(V2(Row("p-0001", Pull, model: "pzl-zones-v9"))));
        Assert.Equal("ZoneModelVersion.unsupported", Code(V2(Row("p-0001", Pull, model: ""))));
        Assert.Equal("ZoneReferenceDistribution.id_unknown", Code(V2(Row("p-0001", Pull, reference: "pzl-ref-other"))));
        Assert.Equal("ZoneReferenceDistribution.id_unknown", Code(V2(Row("p-0001", Pull, reference: ""))));
        Assert.Equal("BatterZoneTendency.length_invalid", Code(V2(Row("p-0001", [1, -1]))));
        Assert.Equal("BatterZoneTendency.length_invalid", Code(V2(Row("p-0001", [-60, 10, 20, 30, 40, -50, -40, 50, 0, 0]))));
        Assert.Equal("BatterZoneTendency.length_invalid", Code(V2(Row("p-0001", []))));
        Assert.Equal("BatterZoneTendency.value_out_of_range", Code(V2(Row("p-0001", [101, -101, 0, 0, 0, 0, 0, 0, 0]))));
        Assert.Equal("BatterZoneTendency.value_out_of_range", Code(V2(Row("p-0001", [-101, 101, 0, 0, 0, 0, 0, 0, 0]))));
        Assert.Equal("BatterZoneTendency.not_average_neutral", Code(V2(Row("p-0001", [1, 0, 0, 0, 0, 0, 0, 0, 0]))));
        Assert.Equal("BatterZoneTendency.not_average_neutral", Code(V2(Row("p-0001", [60, -59, 0, 0, 0, 0, 0, 0, 0]))));
        // the boundary values themselves are fine
        Assert.True(WorldJson.Read(V2(Row("p-0001", [100, -100, 0, 0, 0, 0, 0, 0, 0]))).IsSuccess);
    }

    [Fact]
    public void One_bad_row_fails_the_whole_world_no_partial_world_and_no_neutral_fallback()
    {
        var good = Row("p-0001", Pull);
        var bad = Row("p-0005", [1, 0, 0, 0, 0, 0, 0, 0, 0]);
        var result = WorldJson.Read(V2(good, bad));

        Assert.False(result.IsSuccess);
        Assert.Equal("BatterZoneTendency.not_average_neutral", result.Error!.Value.Code);
        Assert.Contains("batter_zone_tendencies[1]", result.Error.Value.Message);
        Assert.Contains("p-0005", result.Error.Value.Message);
        Assert.False(new FixturePlayerWorldSource(V2(good, bad)).Load().IsSuccess);
    }

    [Fact]
    public void Error_messages_name_the_row_and_the_player_but_never_print_the_vector()
    {
        var hostile = new[] { 987, 654, 321, 111, 222, 333, 444, 555, 666 };
        foreach (var json in new[] { V2(Row("p-0001", hostile)), V2(Row("p-0001", [91, -91, 0, 0, 0, 0, 0, 0, 1])), V2(Row("p-0001", Pull), Row("p-0001", Pull)) })
        {
            var message = WorldJson.Read(json).Error!.Value.Message;
            Assert.DoesNotContain("987", message);
            Assert.DoesNotContain("654", message);
            Assert.DoesNotContain("91", message);
        }
    }

    [Fact]
    public void The_required_development_profile_policy_still_applies_to_a_v2_pack()
    {
        Assert.Equal("Fixture.development_missing", Code(V2([Row("p-0001", Pull)], root => root["development_profiles"]!.AsArray().RemoveAt(0))));
        Assert.Equal("Fixture.duplicate_id", Code(Omitted(root => root["players"]![1]!["player_id"] = "p-0001")));
        Assert.Equal("Fixture.unknown_reference", Code(Omitted(root => root["controlled_player_id"] = "p-9999")));
    }

    [Fact]
    public void A_profile_row_may_name_a_player_only_once_even_when_the_player_is_added_by_the_same_pack()
    {
        var json = V2([Row("x-hit-1", Pull)], root => AddPlayerLike(root, "p-0001", "x-hit-1"));
        var world = Read(json);

        Assert.Equal(Pull, world.BatterTendencies.TendencyFor(P("x-hit-1")).Values);
        Assert.Same(BatterZoneTendency.Neutral, world.BatterTendencies.TendencyFor(P("p-0001"))); // a copy of the sheet is another player: same ratings, no shared profile
    }
}
