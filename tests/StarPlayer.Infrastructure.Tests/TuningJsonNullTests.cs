using System.Text.Json.Nodes;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-B2 fix round (Codex Medium): a missing, null or wrongly typed part of the tuning file is an explicit <c>Result.Failure</c> with a stable code - never an
/// exception from the reader, never a default tuning. Fields the schema defines as optional (<c>table</c> of a linear modifier) stay optional.
/// </summary>
public class TuningJsonNullTests
{
    private static JsonNode Root() => JsonNode.Parse(TuningProfileTests.Raw())!;

    private static Result<TuningProfile> Read(JsonNode node) => TuningProfileJson.Read(node.ToJsonString());

    // Every call must come back as a Result: an escaping exception fails the test through xUnit itself.
    private static Error Rejected(JsonNode node)
    {
        var result = Read(node);
        Assert.False(result.IsSuccess, "the malformed tuning file was accepted");
        return result.Error!.Value;
    }

    [Fact]
    public void The_shipped_tuning_still_loads()
    {
        var made = Read(Root());
        Assert.True(made.IsSuccess, made.Error?.Message);
        Assert.Equal(TuningProfileTests.GoldenHash, made.Value.ContentHash);
    }

    [Theory]
    [InlineData("baselines")]
    [InlineData("modifiers")]
    [InlineData("scenarios")]
    [InlineData("targets")]
    [InlineData("tendency")]
    public void A_null_required_collection_or_object_is_a_stable_error(string member)
    {
        var node = Root();
        node[member] = null;
        Assert.Equal("Fixture.field_missing", Rejected(node).Code);
    }

    [Theory]
    [InlineData("baselines")]
    [InlineData("modifiers")]
    [InlineData("scenarios")]
    [InlineData("targets")]
    [InlineData("tendency")]
    [InlineData("denominator")]
    [InlineData("model_version")]
    public void A_missing_required_member_is_reported_as_invalid_json(string member)
    {
        var node = Root();
        node.AsObject().Remove(member);
        Assert.Equal("Fixture.json_invalid", Rejected(node).Code);
    }

    [Theory]
    [InlineData("baselines")]
    [InlineData("modifiers")]
    [InlineData("scenarios")]
    [InlineData("targets")]
    public void A_null_element_inside_a_required_array_is_a_stable_error(string member)
    {
        var node = Root();
        node[member]!.AsArray()[0] = null;
        Assert.Equal("Fixture.field_missing", Rejected(node).Code);
    }

    [Theory]
    [InlineData("outcomes", "Fixture.field_missing")]
    [InlineData("ppm", "Fixture.field_missing")]
    [InlineData("stage", "Tuning.baseline_row_invalid")]
    [InlineData("condition", "Tuning.baseline_row_invalid")]
    public void A_null_part_of_a_baseline_row_is_a_stable_error(string member, string code)
    {
        var node = Root();
        node["baselines"]![0]![member] = null;
        Assert.Equal(code, Rejected(node).Code);
    }

    [Fact]
    public void A_null_outcome_id_or_probability_inside_a_row_is_an_error()
    {
        var withNullId = Root();
        withNullId["baselines"]![0]!["outcomes"]!.AsArray()[0] = null;
        Assert.Equal("Tuning.baseline_outcomes_mismatch", Rejected(withNullId).Code);

        var withNullPpm = Root();
        withNullPpm["baselines"]![0]!["ppm"]!.AsArray()[0] = null;
        Assert.Equal("Fixture.json_invalid", Rejected(withNullPpm).Code);
    }

    [Fact]
    public void A_null_slot_or_null_table_entry_of_a_modifier_is_a_stable_error()
    {
        var noSlot = Root();
        noSlot["modifiers"]![0]!["slot"] = null;
        Assert.Equal("Tuning.modifier_invalid", Rejected(noSlot).Code);

        var node = Root();
        var table = node["modifiers"]!.AsArray().First(m => m!["table"] is not null)!["table"]!.AsArray();
        table[0] = null;
        Assert.Equal("Fixture.field_missing", Rejected(node).Code);

        var noKey = Root();
        noKey["modifiers"]!.AsArray().First(m => m!["table"] is not null)!["table"]![0]!["key"] = null;
        Assert.Equal("Tuning.modifier_table_keys", Rejected(noKey).Code);
    }

    [Fact]
    public void An_optional_modifier_table_stays_optional_for_a_linear_slot()
    {
        var node = Root();
        var linear = node["modifiers"]!.AsArray().First(m => m!["table"] is null)!;
        Assert.Null(linear["table"]);
        Assert.True(Read(node).IsSuccess);
    }

    [Fact]
    public void A_null_cap_value_or_a_wrong_type_is_a_stable_error()
    {
        var nullCap = Root();
        nullCap["tendency"]!["contact_cap_ppm"] = null;
        Assert.Equal("Fixture.json_invalid", Rejected(nullCap).Code);

        foreach (var (member, wrong) in new (string, JsonNode)[]
        {
            ("denominator", "1000000"), ("baselines", 5), ("baselines", "none"), ("modifiers", new JsonObject()), ("tendency", new JsonArray()),
            ("combined_min_ppm", true), ("scenarios", new JsonArray(1, 2)), ("provenance", 7), ("format_version", "one"),
        })
        {
            var node = Root();
            node[member] = wrong;
            Assert.Equal("Fixture.json_invalid", Rejected(node).Code);
        }
    }

    [Fact]
    public void Empty_or_non_object_documents_are_errors_not_exceptions()
    {
        Assert.Equal("Fixture.json_invalid", TuningProfileJson.Read("null").Error!.Value.Code);
        Assert.Equal("Fixture.json_invalid", TuningProfileJson.Read("[]").Error!.Value.Code);
        Assert.Equal("Fixture.json_invalid", TuningProfileJson.Read("").Error!.Value.Code);
        Assert.Equal("Fixture.json_invalid", TuningProfileJson.Read("{}").Error!.Value.Code);
        Assert.Equal("Fixture.json_invalid", TuningProfileJson.ReadDraft("{ \"baselines\": null }").Error!.Value.Code);
    }

    [Fact]
    public void A_null_free_but_invalid_value_still_gets_the_existing_domain_error_not_a_reader_error()
    {
        var node = Root();
        node["status"] = "official_kbo";
        Assert.Equal("Tuning.status_invalid", Rejected(node).Code);
        var other = Root();
        other["model_version"] = "prob-model-v3";
        Assert.Equal("ProbabilityModelVersion.unsupported", Rejected(other).Code);
    }
}
