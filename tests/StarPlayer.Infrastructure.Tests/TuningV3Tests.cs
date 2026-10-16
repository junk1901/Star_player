using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E Package A: <c>tuning-virtual-proto-v3</c> for <c>prob-model-v3</c>. v1 and v2 files, hashes and behaviour are untouched; v3 is v2 plus the active <c>catcher.framing</c> slot
/// (neutral 50, 1000 ppm per rating point, factor 950,000..1,050,000). Expected distributions come from the independent Python oracle (<c>generate_phase3e_framing_oracle.py</c>),
/// which reads only content/tuning/virtual_tuning_v3.json and writes its own boundary table.
/// </summary>
public class TuningV3Tests
{
    // SHA-256 of the canonical text of content/tuning/virtual_tuning_v3.json, computed by the INDEPENDENT Python implementation (gen_virtual_tuning_v3.py --hash).
    internal const string GoldenHashV3 = "682a5f06b46ae607ef73c7d66236f7a8bceebc69b15cffe98b005d94f33e389a";

    private static readonly TuningProfile V1 = Gx.Tuning1;
    private static readonly TuningProfile V2 = Gx.Tuning2;
    private static readonly TuningProfile V3 = Gx.Tuning3;

    internal static readonly Lazy<JsonElement> OracleDoc = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(PitchBranchFixture.Root, "tests", "Fixtures", "phase3e_framing_oracle.json"))).RootElement);

    private static TuningProfileDraft DraftV3() => TuningProfileJson.ReadDraft(ScriptedGameSource.ReadResource(TuningProfileJson.ResourceNameV3)).Value;

    private static TuningProfileDraft DraftV2() => TuningProfileJson.ReadDraft(ScriptedGameSource.ReadResource(TuningProfileJson.ResourceNameV2)).Value;

    private static TuningProfileDraft EditModifier(TuningProfileDraft d, string slot, Func<ModifierDraft, ModifierDraft> edit) =>
        d with { Modifiers = d.Modifiers!.Select(m => m.Slot == slot ? edit(m) : m).ToList() };

    // ---------------------------------------------------------------- identity and v1/v2 protection

    [Fact]
    public void The_v1_and_v2_tuning_identities_are_unchanged_by_the_v3_work()
    {
        Assert.Equal(TuningProfileTests.GoldenHash, V1.ContentHash);
        Assert.Equal(TuningV2Tests.GoldenHashV2, V2.ContentHash);
        Assert.Equal(("prob-model-v1", "tuning-virtual-proto-v1"), (V1.Model.Version.Value, V1.Tuning.Value));
        Assert.Equal(("prob-model-v2", "tuning-virtual-proto-v2"), (V2.Model.Version.Value, V2.Tuning.Value));
        Assert.Same(ProbabilityModel.V1, V1.Model);
        Assert.Same(ProbabilityModel.V2, V2.Model);
        // framing is still reserved, with no numeric authority, in both
        foreach (var t in new[] { V1, V2 })
        {
            var m = t.Modifier("catcher.framing").Value;
            Assert.Equal((50, 0, 1_000_000, 1_000_000), (m.Neutral, m.SlopePpmPerUnit, m.MinPpm, m.MaxPpm));
        }
    }

    [Fact]
    public void The_v3_tuning_loads_with_its_own_versions_and_the_independently_computed_hash()
    {
        Assert.Equal("prob-model-v3", V3.Model.Version.Value);
        Assert.Equal("tuning-virtual-proto-v3", V3.Tuning.Value);
        Assert.Same(ProbabilityModel.V3, V3.Model);
        Assert.Equal(GoldenHashV3, V3.ContentHash);
        Assert.NotEqual(V1.ContentHash, V3.ContentHash);
        Assert.NotEqual(V2.ContentHash, V3.ContentHash);
        Assert.StartsWith("VIRTUAL PROTOTYPE", V3.Provenance, StringComparison.Ordinal);
        Assert.Contains("not derived from KBO", V3.Provenance, StringComparison.Ordinal);
        Assert.Contains("Phase 3-F", V3.Provenance, StringComparison.Ordinal);
        Assert.Contains("catcher.framing", V3.Provenance, StringComparison.Ordinal);
    }

    [Fact]
    public void V3_differs_from_v2_only_in_its_versions_its_provenance_and_the_framing_modifier()
    {
        Assert.Equal(V2.ZoneModel.Value, V3.ZoneModel.Value);
        Assert.Equal(V2.Reference.Id, V3.Reference.Id);
        Assert.Equal((V2.CombinedMinPpm, V2.CombinedMaxPpm), (V3.CombinedMinPpm, V3.CombinedMaxPpm));
        Assert.Equal((V2.ContactCapPpm, V2.QualityCapPpm, V2.TendencyMinMassPpm), (V3.ContactCapPpm, V3.QualityCapPpm, V3.TendencyMinMassPpm));
        Assert.Equal(V2.MinPositiveBaselinePpm, V3.MinPositiveBaselinePpm);
        foreach (var stage in V2.Model.Stages)
            foreach (var condition in stage.Conditions)
                Assert.Equal(V2.Baseline(stage.Id, condition.Key).Value.Ppm, V3.Baseline(stage.Id, condition.Key).Value.Ppm);
        foreach (var slot in V2.Model.Slots.Where(s => s.Id != "catcher.framing"))
        {
            var a = V2.Modifier(slot.Id).Value;
            var b = V3.Modifier(slot.Id).Value;
            Assert.Equal((a.Neutral, a.SlopePpmPerUnit, a.MinPpm, a.MaxPpm), (b.Neutral, b.SlopePpmPerUnit, b.MinPpm, b.MaxPpm));
            Assert.Equal(a.Table.Select(t => (t.Key, t.Value)), b.Table.Select(t => (t.Key, t.Value)));
        }

        Assert.Equal(V2.Scenarios.Select(s => (s.Id, s.Stage, s.Condition, s.Seed, s.ContextKey, s.Samples, s.ToleranceRule)), V3.Scenarios.Select(s => (s.Id, s.Stage, s.Condition, s.Seed, s.ContextKey, s.Samples, s.ToleranceRule)));
        Assert.Equal(V2.Targets.Select(t => (t.Id, t.LowPpm, t.HighPpm, t.Status)), V3.Targets.Select(t => (t.Id, t.LowPpm, t.HighPpm, t.Status)));
    }

    [Fact]
    public void The_framing_modifier_carries_the_approved_numbers()
    {
        var m = V3.Modifier("catcher.framing").Value;
        Assert.Equal((50, 1000, 950_000, 1_050_000), (m.Neutral, m.SlopePpmPerUnit, m.MinPpm, m.MaxPpm));
        Assert.Empty(m.Table);
    }

    [Theory]
    [InlineData(0, 950_000)]
    [InlineData(50, 1_000_000)]
    [InlineData(100, 1_050_000)]
    [InlineData(1, 951_000)]
    [InlineData(25, 975_000)]
    [InlineData(75, 1_025_000)]
    [InlineData(99, 1_049_000)]
    public void The_framing_factor_is_one_million_plus_rating_minus_fifty_times_one_thousand(int rating, int expectedFactor)
    {
        Assert.Equal(1_000_000 + (rating - 50) * 1000, expectedFactor);      // the approved formula, restated
        var made = StageProbability.Compute(V3, StageIds.CalledPitch, "non_automated:in.r1c2", [ModifierInput.Linear("catcher.framing", rating)]);
        var listed = Assert.Single(made.Value.Trace.Modifiers);
        Assert.Equal(("catcher.framing", rating.ToString(), expectedFactor, true), (listed.Slot, listed.Input, listed.FactorPpm, listed.Applied));
        Assert.Equal([1_000_000, expectedFactor], made.Value.Trace.CombinedFactorPpm);   // only strike is targeted
        Assert.Equal([false, false], made.Value.Trace.CombinedCapped);                    // the factor stays inside the combined cap, nothing is clipped
    }

    // ---------------------------------------------------------------- model/tuning pairing

    [Fact]
    public void A_model_and_a_tuning_of_different_versions_never_combine_in_any_of_the_six_wrong_ways()
    {
        var drafts = new Dictionary<string, TuningProfileDraft>
        {
            ["v1"] = TuningProfileTests.Draft(),
            ["v2"] = DraftV2(),
            ["v3"] = DraftV3(),
        };
        string[] models = ["prob-model-v1", "prob-model-v2", "prob-model-v3"];
        string[] tunings = ["tuning-virtual-proto-v1", "tuning-virtual-proto-v2", "tuning-virtual-proto-v3"];
        var checkedPairs = 0;
        for (var m = 0; m < 3; m++)
            for (var t = 0; t < 3; t++)
            {
                // take the data of tuning t, label it with model m: only m == t may load
                var made = TuningProfile.Create(drafts[$"v{t + 1}"] with { ModelVersion = models[m] });
                if (m == t)
                    Assert.True(made.IsSuccess, made.Error?.Message);
                else
                {
                    Assert.False(made.IsSuccess);
                    Assert.Equal("Tuning.model_tuning_mismatch", made.Error!.Value.Code);
                    checkedPairs++;
                }

                // and the other direction: the data of tuning m, labelled with tuning version t
                var other = TuningProfile.Create(drafts[$"v{m + 1}"] with { TuningVersion = tunings[t] });
                Assert.Equal(m == t, other.IsSuccess);
                if (m != t)
                    Assert.Equal("Tuning.model_tuning_mismatch", other.Error!.Value.Code);
            }

        Assert.Equal(6, checkedPairs);
    }

    [Fact]
    public void V3_data_cannot_be_loaded_under_the_v2_model_or_v1_data_under_the_v3_model_whatever_the_slot_numbers_say()
    {
        // a v3 file relabelled as model v2 would re-activate nothing: it is refused by the pairing before any slot is looked at
        Assert.Equal("Tuning.model_tuning_mismatch", TuningProfileTests.Reject(DraftV3() with { ModelVersion = "prob-model-v2" }).Code);
        Assert.Equal("Tuning.model_tuning_mismatch", TuningProfileTests.Reject(TuningProfileTests.Draft() with { ModelVersion = "prob-model-v3" }).Code);
        // v2-versioned data still carries the reserved framing numbers: re-using them under v3 labels is a mismatch, and v3 numbers under v2 labels are not accepted
        Assert.Equal("Tuning.model_tuning_mismatch", TuningProfileTests.Reject(DraftV2() with { ModelVersion = "prob-model-v3", TuningVersion = "tuning-virtual-proto-v2" }).Code);
        var v2Labelled = DraftV3() with { ModelVersion = "prob-model-v2", TuningVersion = "tuning-virtual-proto-v2" };
        Assert.Equal("Tuning.modifier_reserved_authority", TuningProfileTests.Reject(v2Labelled).Code);
        var v1Labelled = DraftV3() with { ModelVersion = "prob-model-v1", TuningVersion = "tuning-virtual-proto-v1" };
        Assert.Equal("Tuning.modifier_reserved_authority", TuningProfileTests.Reject(v1Labelled).Code);
    }

    [Fact]
    public void An_active_framing_slot_is_validated_like_every_other_linear_slot()
    {
        var d = DraftV3();
        // slope sign must follow the model's direction (higher rating, higher factor)
        Assert.Equal("Tuning.modifier_direction_invalid", TuningProfileTests.Reject(EditModifier(d, "catcher.framing", m => m with { SlopePpmPerUnit = -1000 })).Code);
        // the edge ratings must stay inside the slot's own cap
        Assert.Equal("Tuning.modifier_range_exceeds_cap", TuningProfileTests.Reject(EditModifier(d, "catcher.framing", m => m with { SlopePpmPerUnit = 1100 })).Code);
        // the slot's own cap must sit inside the stage-wide combined cap
        Assert.Equal("Tuning.modifier_cap_invalid", TuningProfileTests.Reject(EditModifier(d, "catcher.framing", m => m with { MinPpm = 700_000 })).Code);
        Assert.Equal("Tuning.modifier_cap_invalid", TuningProfileTests.Reject(EditModifier(d, "catcher.framing", m => m with { MaxPpm = 1_300_000 })).Code);
        // neutral must be a legal input, and a linear slot has no table
        Assert.Equal("Tuning.modifier_neutral_invalid", TuningProfileTests.Reject(EditModifier(d, "catcher.framing", m => m with { Neutral = 101 })).Code);
        Assert.Equal("Tuning.modifier_shape_invalid", TuningProfileTests.Reject(EditModifier(d, "catcher.framing", m => m with { Table = [new TableEntryDraft("neutral", 1_000_000)] })).Code);
        // slots that stay reserved in v3 still carry no numeric authority
        foreach (var slot in new[] { "catcher.game_calling", "battery.chemistry" })
            Assert.Equal("Tuning.modifier_reserved_authority", TuningProfileTests.Reject(EditModifier(d, slot, m => m with { SlopePpmPerUnit = 10 })).Code);
    }

    [Fact]
    public void The_v3_framing_edge_factors_equal_the_approved_950000_and_1050000_so_the_combined_cap_is_never_reached_by_framing_alone()
    {
        var m = V3.Modifier("catcher.framing").Value;
        Assert.InRange(m.MinPpm, V3.CombinedMinPpm, 1_000_000);
        Assert.InRange(m.MaxPpm, 1_000_000, V3.CombinedMaxPpm);
        Assert.Equal((800_000, 1_250_000), (V3.CombinedMinPpm, V3.CombinedMaxPpm));
    }
}
