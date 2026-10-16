using System.Text.Json;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E (Game Calling v4, GC-A): <c>tuning-virtual-proto-v4</c> for <c>prob-model-v4</c>. The v1, v2 and v3 files, hashes and behaviour are untouched; v4 is v3 plus the active
/// <c>catcher.game_calling</c> slot (neutral 50, 1000 ppm per rating point, factor 950,000..1,050,000). The hash below was computed by the INDEPENDENT Python implementation
/// (<c>gen_virtual_tuning_v4.py --hash</c>), never by this code.
/// </summary>
public class TuningV4Tests
{
    internal const string GoldenHashV4 = "5011f8acaae4848481db6bc60f636d7594b2a30fcdf594aeab375f0aa98b8fd8";

    private static readonly TuningProfile V1 = Gx.Tuning1;
    private static readonly TuningProfile V2 = Gx.Tuning2;
    private static readonly TuningProfile V3 = Gx.Tuning3;
    internal static readonly TuningProfile V4 = Gx.Tuning4;

    private static TuningProfileDraft DraftOf(string resource) => TuningProfileJson.ReadDraft(ScriptedGameSource.ReadResource(resource)).Value;

    private static TuningProfileDraft DraftV4() => DraftOf(TuningProfileJson.ResourceNameV4);

    private static TuningProfileDraft EditModifier(TuningProfileDraft d, string slot, Func<ModifierDraft, ModifierDraft> edit) =>
        d with { Modifiers = d.Modifiers!.Select(m => m.Slot == slot ? edit(m) : m).ToList() };

    // ---------------------------------------------------------------- identity and v1/v2/v3 protection

    [Fact]
    public void The_v1_v2_and_v3_tuning_identities_are_unchanged_by_the_v4_work()
    {
        Assert.Equal(TuningProfileTests.GoldenHash, V1.ContentHash);
        Assert.Equal(TuningV2Tests.GoldenHashV2, V2.ContentHash);
        Assert.Equal(TuningV3Tests.GoldenHashV3, V3.ContentHash);
        Assert.Equal(("prob-model-v1", "tuning-virtual-proto-v1"), (V1.Model.Version.Value, V1.Tuning.Value));
        Assert.Equal(("prob-model-v2", "tuning-virtual-proto-v2"), (V2.Model.Version.Value, V2.Tuning.Value));
        Assert.Equal(("prob-model-v3", "tuning-virtual-proto-v3"), (V3.Model.Version.Value, V3.Tuning.Value));
        Assert.Same(ProbabilityModel.V1, V1.Model);
        Assert.Same(ProbabilityModel.V2, V2.Model);
        Assert.Same(ProbabilityModel.V3, V3.Model);
        // game calling is still reserved, with no numeric authority, in all three
        foreach (var t in new[] { V1, V2, V3 })
        {
            var m = t.Modifier("catcher.game_calling").Value;
            Assert.Equal((50, 0, 1_000_000, 1_000_000), (m.Neutral, m.SlopePpmPerUnit, m.MinPpm, m.MaxPpm));
        }

        // framing keeps its v3 numbers
        var f = V3.Modifier("catcher.framing").Value;
        Assert.Equal((50, 1000, 950_000, 1_050_000), (f.Neutral, f.SlopePpmPerUnit, f.MinPpm, f.MaxPpm));
    }

    [Fact]
    public void The_v4_tuning_loads_with_its_own_versions_and_the_independently_computed_hash()
    {
        Assert.Equal("prob-model-v4", V4.Model.Version.Value);
        Assert.Equal("tuning-virtual-proto-v4", V4.Tuning.Value);
        Assert.Same(ProbabilityModel.V4, V4.Model);
        Assert.Equal(GoldenHashV4, V4.ContentHash);
        foreach (var other in new[] { V1, V2, V3 })
            Assert.NotEqual(other.ContentHash, V4.ContentHash);
        Assert.StartsWith("VIRTUAL PROTOTYPE", V4.Provenance, StringComparison.Ordinal);
        Assert.Contains("not derived from KBO", V4.Provenance, StringComparison.Ordinal);
        Assert.Contains("Phase 3-F", V4.Provenance, StringComparison.Ordinal);
        Assert.Contains("catcher.game_calling", V4.Provenance, StringComparison.Ordinal);
        Assert.True(TuningProfileJson.LoadVirtualV4().IsSuccess);
    }

    [Fact]
    public void The_v4_file_differs_from_the_v3_file_only_in_the_versions_the_provenance_and_the_game_calling_modifier()
    {
        using var v3 = JsonDocument.Parse(ScriptedGameSource.ReadResource(TuningProfileJson.ResourceNameV3));
        using var v4 = JsonDocument.Parse(ScriptedGameSource.ReadResource(TuningProfileJson.ResourceNameV4));
        var a = v3.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetRawText());
        var b = v4.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetRawText());
        Assert.Equal(a.Keys.Order(), b.Keys.Order());
        Assert.Equal(["model_version", "modifiers", "provenance", "tuning_version"], a.Keys.Where(k => a[k] != b[k]).Order());

        var modsA = v3.RootElement.GetProperty("modifiers").EnumerateArray().ToList();
        var modsB = v4.RootElement.GetProperty("modifiers").EnumerateArray().ToList();
        Assert.Equal(modsA.Count, modsB.Count);
        var changed = modsA.Zip(modsB).Where(p => p.First.GetRawText() != p.Second.GetRawText()).ToList();
        var pair = Assert.Single(changed);
        Assert.Equal("catcher.game_calling", pair.First.GetProperty("slot").GetString());
        Assert.Equal("catcher.game_calling", pair.Second.GetProperty("slot").GetString());
    }

    [Fact]
    public void V4_differs_from_v3_only_in_its_versions_its_provenance_and_the_game_calling_modifier()
    {
        Assert.Equal(V3.ZoneModel.Value, V4.ZoneModel.Value);
        Assert.Equal(V3.Reference.Id, V4.Reference.Id);
        Assert.Equal((V3.CombinedMinPpm, V3.CombinedMaxPpm), (V4.CombinedMinPpm, V4.CombinedMaxPpm));
        Assert.Equal((V3.ContactCapPpm, V3.QualityCapPpm, V3.TendencyMinMassPpm), (V4.ContactCapPpm, V4.QualityCapPpm, V4.TendencyMinMassPpm));
        Assert.Equal(V3.MinPositiveBaselinePpm, V4.MinPositiveBaselinePpm);
        foreach (var stage in V3.Model.Stages)
            foreach (var condition in stage.Conditions)
                Assert.Equal(V3.Baseline(stage.Id, condition.Key).Value.Ppm, V4.Baseline(stage.Id, condition.Key).Value.Ppm);
        foreach (var slot in V3.Model.Slots.Where(s => s.Id != "catcher.game_calling"))
        {
            var a = V3.Modifier(slot.Id).Value;
            var b = V4.Modifier(slot.Id).Value;
            Assert.Equal((a.Neutral, a.SlopePpmPerUnit, a.MinPpm, a.MaxPpm), (b.Neutral, b.SlopePpmPerUnit, b.MinPpm, b.MaxPpm));
            Assert.Equal(a.Table.Select(t => (t.Key, t.Value)), b.Table.Select(t => (t.Key, t.Value)));
        }

        Assert.Equal(V3.Scenarios.Select(s => (s.Id, s.Stage, s.Condition, s.Seed, s.ContextKey, s.Samples, s.ToleranceRule)), V4.Scenarios.Select(s => (s.Id, s.Stage, s.Condition, s.Seed, s.ContextKey, s.Samples, s.ToleranceRule)));
        Assert.Equal(V3.Targets.Select(t => (t.Id, t.LowPpm, t.HighPpm, t.Status)), V4.Targets.Select(t => (t.Id, t.LowPpm, t.HighPpm, t.Status)));
    }

    [Fact]
    public void The_game_calling_modifier_carries_the_approved_numbers_and_control_keeps_its_own()
    {
        var gc = V4.Modifier("catcher.game_calling").Value;
        Assert.Equal((50, 1000, 950_000, 1_050_000), (gc.Neutral, gc.SlopePpmPerUnit, gc.MinPpm, gc.MaxPpm));
        Assert.Empty(gc.Table);
        var control = V4.Modifier("pitcher.control").Value;
        Assert.Equal((50, 1500, 900_000, 1_100_000), (control.Neutral, control.SlopePpmPerUnit, control.MinPpm, control.MaxPpm));
        // battery.chemistry stays reserved
        var chemistry = V4.Modifier("battery.chemistry").Value;
        Assert.Equal((50, 0, 1_000_000, 1_000_000), (chemistry.Neutral, chemistry.SlopePpmPerUnit, chemistry.MinPpm, chemistry.MaxPpm));
        Assert.Equal((800_000, 1_250_000), (V4.CombinedMinPpm, V4.CombinedMaxPpm));
    }

    [Theory]
    [InlineData(0, 950_000)]
    [InlineData(50, 1_000_000)]
    [InlineData(100, 1_050_000)]
    [InlineData(1, 951_000)]
    [InlineData(25, 975_000)]
    [InlineData(75, 1_025_000)]
    [InlineData(99, 1_049_000)]
    public void The_game_calling_factor_is_one_million_plus_rating_minus_fifty_times_one_thousand(int rating, int expectedFactor)
    {
        Assert.Equal(1_000_000 + (rating - 50) * 1000, expectedFactor);      // the approved formula, restated
        var made = StageProbability.Compute(V4, StageIds.ActualLocation, "in.r1c2", [ModifierInput.Linear("catcher.game_calling", rating)]);
        var listed = Assert.Single(made.Value.Trace.Modifiers);
        Assert.Equal(("catcher.game_calling", rating.ToString(), expectedFactor, true), (listed.Slot, listed.Input, listed.FactorPpm, listed.Applied));
        var target = V4.Model.Stage(StageIds.ActualLocation)!.Find("in.r1c2")!.Outcomes.ToList().IndexOf("in.r1c2");
        Assert.Equal(expectedFactor, made.Value.Trace.CombinedFactorPpm[target]);   // only the aimed outcome is targeted
        Assert.All(made.Value.Trace.CombinedFactorPpm.Where((_, i) => i != target), f => Assert.Equal(1_000_000, f));
        Assert.DoesNotContain(true, made.Value.Trace.CombinedCapped);
    }

    // ---------------------------------------------------------------- model/tuning pairing: 4 x 4

    [Fact]
    public void A_model_and_a_tuning_of_different_versions_never_combine_in_any_of_the_twelve_wrong_ways()
    {
        var drafts = new[] { TuningProfileTests.Draft(), DraftOf(TuningProfileJson.ResourceNameV2), DraftOf(TuningProfileJson.ResourceNameV3), DraftV4() };
        string[] models = ["prob-model-v1", "prob-model-v2", "prob-model-v3", "prob-model-v4"];
        string[] tunings = ["tuning-virtual-proto-v1", "tuning-virtual-proto-v2", "tuning-virtual-proto-v3", "tuning-virtual-proto-v4"];
        var wrong = 0;
        for (var m = 0; m < 4; m++)
            for (var t = 0; t < 4; t++)
            {
                // take the data of tuning t, label it with model m: only m == t may load
                var made = TuningProfile.Create(drafts[t] with { ModelVersion = models[m] });
                if (m == t)
                    Assert.True(made.IsSuccess, made.Error?.Message);
                else
                {
                    Assert.False(made.IsSuccess);
                    Assert.Equal("Tuning.model_tuning_mismatch", made.Error!.Value.Code);
                    wrong++;
                }

                // and the other direction: the data of tuning m, labelled with tuning version t
                var other = TuningProfile.Create(drafts[m] with { TuningVersion = tunings[t] });
                Assert.Equal(m == t, other.IsSuccess);
                if (m != t)
                    Assert.Equal("Tuning.model_tuning_mismatch", other.Error!.Value.Code);
            }

        Assert.Equal(12, wrong);
    }

    [Fact]
    public void V4_data_cannot_be_loaded_under_an_older_model_and_older_data_cannot_borrow_the_v4_labels()
    {
        foreach (var older in new[] { 1, 2, 3 })
        {
            Assert.Equal("Tuning.model_tuning_mismatch", TuningProfileTests.Reject(DraftV4() with { ModelVersion = $"prob-model-v{older}" }).Code);
            Assert.Equal("Tuning.model_tuning_mismatch", TuningProfileTests.Reject(DraftV4() with { TuningVersion = $"tuning-virtual-proto-v{older}" }).Code);
        }

        // v3-versioned data (reserved game calling) relabelled as model v4 is a mismatch: a version is never converted to its neighbour
        var v3 = DraftOf(TuningProfileJson.ResourceNameV3);
        Assert.Equal("Tuning.model_tuning_mismatch", TuningProfileTests.Reject(v3 with { ModelVersion = "prob-model-v4" }).Code);
        Assert.Equal("Tuning.model_tuning_mismatch", TuningProfileTests.Reject(v3 with { TuningVersion = "tuning-virtual-proto-v4" }).Code);
        // v3 data with BOTH labels moved to v4 is a flat (slope 0) game-calling slot: legal data, but a different identity from the shipped v4 file, so no game can mistake it for it
        var promoted = TuningProfile.Create(v3 with { ModelVersion = "prob-model-v4", TuningVersion = "tuning-virtual-proto-v4" });
        Assert.True(promoted.IsSuccess);
        Assert.NotEqual(GoldenHashV4, promoted.Value.ContentHash);

        // v4's numbers under older labels are numeric authority for a reserved slot
        foreach (var older in new[] { 1, 2, 3 })
        {
            var relabelled = DraftV4() with { ModelVersion = $"prob-model-v{older}", TuningVersion = $"tuning-virtual-proto-v{older}" };
            Assert.Equal("Tuning.modifier_reserved_authority", TuningProfileTests.Reject(relabelled).Code);
        }
    }

    [Fact]
    public void An_active_game_calling_slot_is_validated_like_every_other_linear_slot()
    {
        var d = DraftV4();
        Assert.Equal("Tuning.modifier_direction_invalid", TuningProfileTests.Reject(EditModifier(d, "catcher.game_calling", m => m with { SlopePpmPerUnit = -1000 })).Code);
        Assert.Equal("Tuning.modifier_range_exceeds_cap", TuningProfileTests.Reject(EditModifier(d, "catcher.game_calling", m => m with { SlopePpmPerUnit = 1100 })).Code);
        Assert.Equal("Tuning.modifier_cap_invalid", TuningProfileTests.Reject(EditModifier(d, "catcher.game_calling", m => m with { MinPpm = 700_000 })).Code);
        Assert.Equal("Tuning.modifier_cap_invalid", TuningProfileTests.Reject(EditModifier(d, "catcher.game_calling", m => m with { MaxPpm = 1_300_000 })).Code);
        Assert.Equal("Tuning.modifier_neutral_invalid", TuningProfileTests.Reject(EditModifier(d, "catcher.game_calling", m => m with { Neutral = 101 })).Code);
        Assert.Equal("Tuning.modifier_shape_invalid", TuningProfileTests.Reject(EditModifier(d, "catcher.game_calling", m => m with { Table = [new TableEntryDraft("neutral", 1_000_000)] })).Code);
        // the slot that stays reserved in v4 still carries no numeric authority
        Assert.Equal("Tuning.modifier_reserved_authority", TuningProfileTests.Reject(EditModifier(d, "battery.chemistry", m => m with { SlopePpmPerUnit = 10 })).Code);
        Assert.Equal("Tuning.modifier_reserved_authority", TuningProfileTests.Reject(EditModifier(d, "battery.chemistry", m => m with { MinPpm = 990_000 })).Code);
    }

    [Fact]
    public void The_embedded_v4_resource_is_registered_and_the_file_on_disk_is_the_same_content()
    {
        var embedded = ScriptedGameSource.ReadResource(TuningProfileJson.ResourceNameV4);
        var onDisk = File.ReadAllText(Path.Combine(PitchBranchFixture.Root, "content", "tuning", "virtual_tuning_v4.json"));
        Assert.Equal(onDisk.ReplaceLineEndings("\n"), embedded.ReplaceLineEndings("\n"));
        Assert.Equal("virtual_tuning_v4.json", TuningProfileJson.ResourceNameV4);
        // the three earlier resources are still embedded under their own names
        foreach (var name in new[] { TuningProfileJson.ResourceName, TuningProfileJson.ResourceNameV2, TuningProfileJson.ResourceNameV3 })
            Assert.False(string.IsNullOrWhiteSpace(ScriptedGameSource.ReadResource(name)));
    }
}
