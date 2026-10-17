using System.Text.Json.Nodes;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.SimHarness;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Codex F-02. The harness cannot call the internal Infrastructure readers of the two content files it is pinned to (<see cref="TuningProfileJson"/>, <see cref="GameRulesJson"/>), so it has a second
/// reader (<see cref="HarnessData"/>). This matrix runs BOTH readers over the same defective files and requires the same decision: whatever the production reader refuses (by an error, or by throwing)
/// the harness reader must refuse as an error; whatever the production reader accepts the harness reader must accept with the same content hash. Message texts are not compared.
/// </summary>
public class SimHarnessLoaderConformanceTests
{
    private static readonly string Root = FindRoot();
    private static readonly string TuningText = File.ReadAllText(Path.Combine(Root, "content", "tuning", "virtual_tuning_v4.json"));
    private static readonly string RulesText = File.ReadAllText(Path.Combine(Root, "content", "rules", "game_rules_profiles_v2.json"));

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                return d.FullName;
        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }

    private static string Edit(string json, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        change(node);
        return node.ToJsonString();
    }

    private static JsonObject Modifier(JsonObject root, int index) => root["modifiers"]!.AsArray()[index]!.AsObject();

    private static JsonObject TableModifier(JsonObject root) => root["modifiers"]!.AsArray().Select(m => m!.AsObject()).First(m => m["table"] is JsonArray);

    private static JsonObject Profile(JsonObject root, int index) => root["profiles"]!.AsArray()[index]!.AsObject();

    private static void SetProfileText(JsonObject root, string member, JsonNode? value) => Profile(root, 0)[member] = value;

    // ------------------------------------------------------------------------------------------------ the matrix

    public sealed record Case(string Id, bool Rules, string Json, string Why);

    private static readonly Case[] Cases =
    [
        // ---- J12: the real files
        new("J12-tuning-v4-unchanged", false, TuningText, "the authoritative tuning file"),
        new("J12-rules-v2-unchanged", true, RulesText, "the authoritative rules file"),

        // ---- J1: a required member is missing (never read as 0)
        new("J1-modifier-slope-missing", false, Edit(TuningText, r => Modifier(r, 0).Remove("slope_ppm_per_unit")), "pitcher.control.slope_ppm_per_unit absent (Codex counter-example)"),
        new("J1-modifier-neutral-missing", false, Edit(TuningText, r => Modifier(r, 0).Remove("neutral")), "a required int absent"),
        new("J1-modifier-min-ppm-missing", false, Edit(TuningText, r => Modifier(r, 0).Remove("min_ppm")), "a required int absent"),
        new("J1-denominator-missing", false, Edit(TuningText, r => r.Remove("denominator")), "a required top-level int absent"),
        new("J1-tendency-cap-missing", false, Edit(TuningText, r => r["tendency"]!.AsObject().Remove("contact_cap_ppm")), "a required nested int absent"),
        new("J1-target-low-ppm-missing", false, Edit(TuningText, r => r["targets"]![0]!.AsObject().Remove("low_ppm")), "a required long absent"),
        new("J1-scenario-seed-missing", false, Edit(TuningText, r => r["scenarios"]![0]!.AsObject().Remove("seed")), "a required ulong absent"),
        new("J1-table-factor-missing", false, Edit(TuningText, r => TableModifier(r)["table"]![0]!.AsObject().Remove("factor_ppm")), "a required table entry int absent"),
        new("J1-rules-outs-missing", true, Edit(RulesText, r => Profile(r, 0).Remove("outs_per_half_inning")), "a required rule int absent"),
        new("J1-rules-bool-missing", true, Edit(RulesText, r => Profile(r, 0).Remove("tie_allowed")), "a required bool absent"),
        new("J1-rules-note-missing", true, Edit(RulesText, r => r.Remove("note")), "a required file string absent"),
        new("J1-rules-policy-missing", true, Edit(RulesText, r => Profile(r, 0).Remove("rule_policy")), "a required string absent"),
        new("J1-rules-called-pitch-mode-missing", true, Edit(RulesText, r => Profile(r, 0).Remove("called_pitch_mode")), "a format-2 member absent"),
        new("J1-rules-extra-innings-limit-missing", true, Edit(RulesText, r => Profile(r, 0).Remove("extra_innings_limit")), "required (it may be null, but it must be present)"),

        // ---- J2: duplicate profile id
        new("J2-rules-duplicate-profile-id", true, Edit(RulesText, r => r["profiles"]!.AsArray().Add(JsonNode.Parse(Profile(r, 0).ToJsonString()))), "the same ProfileId twice (Codex counter-example)"),
        new("J2-rules-duplicate-id-different-content", true, Edit(RulesText, r =>
        {
            var copy = JsonNode.Parse(Profile(r, 0).ToJsonString())!.AsObject();
            copy["version"] = "other";
            r["profiles"]!.AsArray().Add(copy);
        }), "the same ProfileId with different content: the first must not win silently"),

        // ---- J3: null where an element is required
        new("J3-modifier-table-entry-null", false, Edit(TuningText, r => TableModifier(r)["table"]!.AsArray()[0] = null), "table[0] = null (Codex counter-example)"),
        new("J3-baseline-row-null", false, Edit(TuningText, r => r["baselines"]!.AsArray()[0] = null), "baselines[0] = null"),
        new("J3-modifier-null", false, Edit(TuningText, r => r["modifiers"]!.AsArray()[0] = null), "modifiers[0] = null"),
        new("J3-scenario-null", false, Edit(TuningText, r => r["scenarios"]!.AsArray()[0] = null), "scenarios[0] = null"),
        new("J3-target-null", false, Edit(TuningText, r => r["targets"]!.AsArray()[0] = null), "targets[0] = null"),
        new("J3-modifier-table-member-null", false, Edit(TuningText, r => TableModifier(r)["table"] = null), "table = null is allowed by the schema (a linear modifier); the Domain refuses it for a slot that needs a table"),
        new("J3-rules-profile-null", true, Edit(RulesText, r => r["profiles"]!.AsArray()[0] = null), "profiles[0] = null"),

        // ---- J7: a required object / list is null
        new("J7-tendency-null", false, Edit(TuningText, r => r["tendency"] = null), "tendency = null"),
        new("J7-baseline-outcomes-null", false, Edit(TuningText, r => r["baselines"]![0]!.AsObject()["outcomes"] = null), "outcomes = null"),
        new("J7-baseline-ppm-null", false, Edit(TuningText, r => r["baselines"]![0]!.AsObject()["ppm"] = null), "ppm = null"),
        new("J7-rules-profiles-null", true, Edit(RulesText, r => r["profiles"] = null), "profiles = null"),
        new("J7-rules-open-items-null", true, Edit(RulesText, r => SetProfileText(r, "open_items", null)), "open_items = null"),
        new("J7-rules-mode-null", true, Edit(RulesText, r => SetProfileText(r, "called_pitch_mode", null)), "called_pitch_mode = null"),
        new("J7-rules-policy-null", true, Edit(RulesText, r => SetProfileText(r, "rule_policy", null)), "rule_policy = null (an explicit null is not a missing member; the Domain decides)"),
        new("J7-tuning-model-version-null", false, Edit(TuningText, r => r["model_version"] = null), "a required string explicitly null (the Domain decides)"),

        // ---- J8: a required list is missing
        new("J8-targets-missing", false, Edit(TuningText, r => r.Remove("targets")), "targets absent"),
        new("J8-baselines-missing", false, Edit(TuningText, r => r.Remove("baselines")), "baselines absent"),
        new("J8-modifiers-missing", false, Edit(TuningText, r => r.Remove("modifiers")), "modifiers absent"),
        new("J8-modifier-table-member-missing", false, Edit(TuningText, r => Modifier(r, 0).Remove("table")), "table is declared required (it may be null, not absent)"),
        new("J8-rules-profiles-missing", true, Edit(RulesText, r => r.Remove("profiles")), "profiles absent"),
        new("J8-rules-open-items-missing", true, Edit(RulesText, r => Profile(r, 0).Remove("open_items")), "open_items absent"),

        // ---- J9: duplicate keys
        new("J9-duplicate-modifier-slot", false, Edit(TuningText, r => r["modifiers"]!.AsArray().Add(JsonNode.Parse(Modifier(r, 0).ToJsonString()))), "the same slot twice (the Domain decides)"),
        new("J9-duplicate-table-key", false, Edit(TuningText, r =>
        {
            var table = TableModifier(r)["table"]!.AsArray();
            table.Add(JsonNode.Parse(table[0]!.ToJsonString()));
        }), "the same table key twice (the Domain decides)"),
        new("J9-duplicate-target-id", false, Edit(TuningText, r => r["targets"]!.AsArray().Add(JsonNode.Parse(r["targets"]![0]!.ToJsonString()))), "the same target id twice (the Domain decides)"),

        // ---- J10: numbers outside the type or the allowed range
        new("J10-denominator-over-int", false, Edit(TuningText, r => r["denominator"] = 3_000_000_000L), "int overflow"),
        new("J10-slope-over-int", false, Edit(TuningText, r => Modifier(r, 0)["slope_ppm_per_unit"] = 3_000_000_000L), "int overflow in a modifier"),
        new("J10-slope-negative", false, Edit(TuningText, r => Modifier(r, 0)["slope_ppm_per_unit"] = -1500), "a value the Domain judges"),
        new("J10-combined-max-below-min", false, Edit(TuningText, r => r["combined_max_ppm"] = 1), "a value the Domain judges"),
        new("J10-seed-negative", false, Edit(TuningText, r => r["scenarios"]![0]!.AsObject()["seed"] = -1), "ulong cannot be negative"),
        new("J10-denominator-as-text", false, Edit(TuningText, r => r["denominator"] = "1000000"), "a number given as text"),
        new("J10-denominator-fraction", false, Edit(TuningText, r => r["denominator"] = 1000000.5), "a fraction for an int"),
        new("J10-rules-outs-over-int", true, Edit(RulesText, r => Profile(r, 0)["outs_per_half_inning"] = 3_000_000_000L), "int overflow in a rule"),
        new("J10-rules-outs-zero", true, Edit(RulesText, r => Profile(r, 0)["outs_per_half_inning"] = 0), "a value the Domain judges"),
        new("J10-rules-bool-as-text", true, Edit(RulesText, r => Profile(r, 0)["tie_allowed"] = "true"), "a bool given as text"),

        // ---- J4 / J5 / J6: enum text (status, called_pitch_mode)
        new("J4-status-upper-case", true, Edit(RulesText, r => SetProfileText(r, "status", "FICTIONAL_TEST")), "case changed"),
        new("J4-status-pascal-case", true, Edit(RulesText, r => SetProfileText(r, "status", "FictionalTest")), "PascalCase spelling"),
        new("J4-status-without-underscore", true, Edit(RulesText, r => SetProfileText(r, "status", "fictionaltest")), "underscore removed"),
        new("J4-mode-upper-case", true, Edit(RulesText, r => SetProfileText(r, "called_pitch_mode", "AUTOMATED")), "case changed"),
        new("J4-mode-pascal-case", true, Edit(RulesText, r => SetProfileText(r, "called_pitch_mode", "NonAutomated")), "PascalCase spelling"),
        new("J4-mode-without-underscore", true, Edit(RulesText, r => SetProfileText(r, "called_pitch_mode", "nonautomated")), "underscore removed"),
        new("J5-status-number-text", true, Edit(RulesText, r => SetProfileText(r, "status", "0")), "numeric text"),
        new("J5-mode-number-text", true, Edit(RulesText, r => SetProfileText(r, "called_pitch_mode", "1")), "numeric text"),
        new("J5-mode-number", true, Edit(RulesText, r => SetProfileText(r, "called_pitch_mode", 1)), "a JSON number for an enum"),
        new("J5-status-leading-space", true, Edit(RulesText, r => SetProfileText(r, "status", " fictional_test")), "whitespace"),
        new("J6-status-unknown", true, Edit(RulesText, r => SetProfileText(r, "status", "no_such_status")), "unregistered value"),
        new("J6-mode-unknown", true, Edit(RulesText, r => SetProfileText(r, "called_pitch_mode", "no_such_mode")), "unregistered value"),
        new("J6-status-empty", true, Edit(RulesText, r => SetProfileText(r, "status", "")), "empty text"),
        new("J6-status-comma-list", true, Edit(RulesText, r => SetProfileText(r, "status", "fictional_test,fictional_test")), "a flags-style list"),
        new("J6-zone-model-unknown", true, Edit(RulesText, r => SetProfileText(r, "zone_model", "pzl-99")), "an unknown zone model"),

        // ---- structure
        new("S-extra-member-root", false, Edit(TuningText, r => r["extra_member"] = 1), "an unknown member at the root"),
        new("S-extra-member-nested", false, Edit(TuningText, r => Modifier(r, 0)["extra_member"] = 1), "an unknown nested member"),
        new("S-rules-extra-member", true, Edit(RulesText, r => Profile(r, 0)["extra_member"] = 1), "an unknown rule member"),
        new("S-tuning-format-2", false, Edit(TuningText, r => r["format_version"] = 2), "an unsupported tuning format"),
        new("S-tuning-format-missing", false, Edit(TuningText, r => r.Remove("format_version")), "format_version absent"),
        new("S-rules-format-3", true, Edit(RulesText, r => r["format_version"] = 3), "an unsupported rules format"),
        new("S-baseline-length-mismatch", false, Edit(TuningText, r => r["baselines"]![0]!.AsObject()["ppm"]!.AsArray().RemoveAt(0)), "outcomes and ppm of different length"),
        new("S-root-is-array", false, "[]", "a root that is not an object"),
        new("S-root-is-null", false, "null", "the JSON null"),
        new("S-rules-root-is-array", true, "[]", "a root that is not an object"),
        new("S-rules-root-is-null", true, "null", "the JSON null"),

        // ---- J11: JSON syntax
        new("J11-tuning-truncated", false, TuningText[..(TuningText.Length / 2)], "cut in half"),
        new("J11-tuning-empty", false, "", "an empty file"),
        new("J11-tuning-trailing-comma", false, TuningText.Replace("\"targets\"", "\"targets\" ,,", StringComparison.Ordinal), "a stray comma"),
        new("J11-rules-truncated", true, RulesText[..(RulesText.Length / 2)], "cut in half"),
        new("J11-rules-empty", true, "", "an empty file"),
        new("J11-rules-garbage", true, "{ not json", "not JSON"),
    ];

    public static IEnumerable<object[]> CaseIds() => Cases.Select(c => new object[] { c.Id });

    // ------------------------------------------------------------------------------------------------ the two readers

    private enum Verdict { Accepted, RefusedWithError, Threw }

    private sealed record Outcome(Verdict Verdict, IReadOnlyList<string> Hashes, string Detail);

    private static Outcome Production(Case c)
    {
        try
        {
            if (c.Rules)
            {
                var read = GameRulesJson.Read(c.Json);
                return read.IsSuccess ? new Outcome(Verdict.Accepted, read.Value.Select(r => $"{r.ProfileId}:{r.Version}:{r.ContentHash}").ToList(), "") : new Outcome(Verdict.RefusedWithError, [], read.Error!.Value.Code);
            }

            var tuning = TuningProfileJson.Read(c.Json);
            return tuning.IsSuccess ? new Outcome(Verdict.Accepted, [tuning.Value.ContentHash], "") : new Outcome(Verdict.RefusedWithError, [], tuning.Error!.Value.Code);
        }
        catch (Exception e)
        {
            return new Outcome(Verdict.Threw, [], e.GetType().Name);
        }
    }

    private static Outcome Harness(Case c)
    {
        try
        {
            if (c.Rules)
            {
                var read = HarnessData.ParseRules(c.Json);
                return read.IsSuccess ? new Outcome(Verdict.Accepted, read.Value.Select(r => $"{r.ProfileId}:{r.Version}:{r.ContentHash}").ToList(), "") : new Outcome(Verdict.RefusedWithError, [], read.Error!.Value.Code);
            }

            var tuning = HarnessData.ParseTuning(c.Json);
            return tuning.IsSuccess ? new Outcome(Verdict.Accepted, [tuning.Value.ContentHash], "") : new Outcome(Verdict.RefusedWithError, [], tuning.Error!.Value.Code);
        }
        catch (Exception e)
        {
            return new Outcome(Verdict.Threw, [], e.GetType().Name);
        }
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void The_harness_reader_decides_every_defective_file_like_the_production_reader(string id)
    {
        var c = Cases.Single(x => x.Id == id);
        var production = Production(c);
        var harness = Harness(c);

        // the harness reader never throws: a bad file is an error value
        Assert.NotEqual(Verdict.Threw, harness.Verdict);
        if (production.Verdict == Verdict.Accepted)
        {
            Assert.True(harness.Verdict == Verdict.Accepted, $"{id} ({c.Why}): production accepted, harness refused with {harness.Detail}");
            Assert.Equal(production.Hashes, harness.Hashes);
        }
        else
            Assert.True(harness.Verdict == Verdict.RefusedWithError, $"{id} ({c.Why}): production {production.Verdict} ({production.Detail}), harness accepted");
    }

    [Fact]
    public void The_real_files_are_accepted_by_both_readers_with_the_same_hashes_and_the_matrix_has_real_refusals()
    {
        Assert.Equal(Verdict.Accepted, Production(Cases.Single(x => x.Id == "J12-tuning-v4-unchanged")).Verdict);
        Assert.Equal(Verdict.Accepted, Harness(Cases.Single(x => x.Id == "J12-tuning-v4-unchanged")).Verdict);
        Assert.Equal(Verdict.Accepted, Production(Cases.Single(x => x.Id == "J12-rules-v2-unchanged")).Verdict);
        Assert.Equal(Verdict.Accepted, Harness(Cases.Single(x => x.Id == "J12-rules-v2-unchanged")).Verdict);

        // the matrix is not vacuous: most of its files are refused by the production reader (the cases that are accepted are the ones the Domain / the schema allows)
        var refused = Cases.Count(c => Production(c).Verdict != Verdict.Accepted);
        Assert.True(refused >= 55, $"only {refused} of {Cases.Length} cases are refused by the production reader");
    }

    [Fact]
    public void The_codex_counter_examples_are_refused_by_both_readers_each_for_its_own_reason()
    {
        foreach (var id in new[] { "J1-modifier-slope-missing", "J2-rules-duplicate-profile-id", "J3-modifier-table-entry-null", "J4-status-upper-case", "J5-status-number-text" })
        {
            var c = Cases.Single(x => x.Id == id);
            Assert.NotEqual(Verdict.Accepted, Production(c).Verdict);
            Assert.Equal(Verdict.RefusedWithError, Harness(c).Verdict);
        }

        Assert.Equal("Harness.rules_duplicate_id", HarnessData.ParseRules(Cases.Single(x => x.Id == "J2-rules-duplicate-profile-id").Json).Error!.Value.Code);
        Assert.Equal("Harness.tuning_json_invalid", HarnessData.ParseTuning(Cases.Single(x => x.Id == "J1-modifier-slope-missing").Json).Error!.Value.Code);
        Assert.Equal("Harness.tuning_shape_invalid", HarnessData.ParseTuning(Cases.Single(x => x.Id == "J3-modifier-table-entry-null").Json).Error!.Value.Code);
        Assert.Equal("Harness.rules_enum_invalid", HarnessData.ParseRules(Cases.Single(x => x.Id == "J4-status-upper-case").Json).Error!.Value.Code);
    }

    [Fact]
    public void The_case_ids_are_unique()
    {
        Assert.Equal(Cases.Length, Cases.Select(c => c.Id).Distinct().Count());
    }
}
