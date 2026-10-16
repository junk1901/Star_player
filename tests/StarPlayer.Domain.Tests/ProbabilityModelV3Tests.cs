using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;

namespace StarPlayer.Domain.Tests;

/// <summary>
/// Phase 3-E (Framing v3, Package A): <c>prob-model-v3</c> is <c>prob-model-v2</c> with exactly one slot changed - <c>catcher.framing</c> is active and limited by
/// <see cref="ModifierConditionScope.NonAutomatedBorderline"/>. v1 and v2 are never edited; model/tuning pairs are an explicit table; the called-pitch condition key is read back strictly.
/// </summary>
public class ProbabilityModelV3Tests
{
    private static string Describe(ModifierSlot s) =>
        $"{s.Id}|{s.Stage}|{string.Join(",", s.Targets)}|{s.Kind}|{s.Scope}|{s.InputMin}|{s.InputMax}|{s.HigherInputRaisesFactor}|{string.Join(",", s.TableOrder)}|{s.Reserved}|{s.OwnerNote}|{s.ConditionScope}";

    // The Borderline set of the zone model, written out here from docs/game-engine.md section 18 (NOT read from the model): the eight non-centre inner cells and the four outer zones that border an edge.
    private static readonly string[] BorderlineZones =
        ["in.r1c1", "in.r1c2", "in.r1c3", "in.r2c1", "in.r2c3", "in.r3c1", "in.r3c2", "in.r3c3", "out.high", "out.in", "out.low", "out.out"];

    private static readonly string[] ClearZones = ["in.r2c2", "out.high_in", "out.low_in", "out.low_out", "out.high_out"];

    // ---------------------------------------------------------------- versions and the explicit pairing table

    [Fact]
    public void V3_is_a_known_model_and_tuning_version_and_a_fourth_is_not()
    {
        Assert.Equal("prob-model-v3", ProbabilityModelVersion.Create("prob-model-v3").Value.Value);
        Assert.Equal("tuning-virtual-proto-v3", TuningVersion.Create("tuning-virtual-proto-v3").Value.Value);
        Assert.Same(ProbabilityModel.V3, ProbabilityModel.Resolve(ProbabilityModelVersion.V3).Value);
        Assert.Equal("prob-model-v3", ProbabilityModel.V3.Version.Value);
        foreach (var unknown in new[] { null, "", "prob-model-v4", "PROB-MODEL-V3", " prob-model-v3", "prob-model-v3 ", "prob-model-v30" })
            Assert.Equal("ProbabilityModelVersion.unsupported", ProbabilityModelVersion.Create(unknown).Error!.Value.Code);
        foreach (var unknown in new[] { null, "", "tuning-virtual-proto-v4", "TUNING-VIRTUAL-PROTO-V3", "tuning-virtual-proto-v3 " })
            Assert.Equal("TuningVersion.unsupported", TuningVersion.Create(unknown).Error!.Value.Code);
    }

    [Fact]
    public void Exactly_the_three_diagonal_pairs_are_allowed_and_the_other_six_combinations_are_not()
    {
        ProbabilityModelVersion[] models = [ProbabilityModelVersion.V1, ProbabilityModelVersion.V2, ProbabilityModelVersion.V3];
        TuningVersion[] tunings = [TuningVersion.VirtualProtoV1, TuningVersion.VirtualProtoV2, TuningVersion.VirtualProtoV3];
        for (var m = 0; m < 3; m++)
            for (var t = 0; t < 3; t++)
                Assert.Equal(m == t, ProbabilityModel.Pairs(models[m], tunings[t]));
        for (var i = 0; i < 3; i++)
            Assert.Equal(tunings[i], ProbabilityModel.TuningFor(models[i]).Value);
        Assert.Equal("ProbabilityModelVersion.unsupported", ProbabilityModel.TuningFor(default).Error!.Value.Code);
        Assert.False(ProbabilityModel.Pairs(default, TuningVersion.VirtualProtoV1));
        Assert.False(ProbabilityModel.Pairs(ProbabilityModelVersion.V3, default));
    }

    // ---------------------------------------------------------------- structure: v3 = v2 + one slot

    [Fact]
    public void V3_differs_from_v2_in_exactly_the_framing_slot()
    {
        Assert.Equal(ProbabilityModel.V2.Slots.Select(s => s.Id), ProbabilityModel.V3.Slots.Select(s => s.Id));
        foreach (var v2 in ProbabilityModel.V2.Slots)
        {
            var v3 = ProbabilityModel.V3.Slot(v2.Id)!;
            if (v2.Id == "catcher.framing")
                Assert.NotEqual(Describe(v2), Describe(v3));
            else
                Assert.Equal(Describe(v2), Describe(v3));   // every other slot, including its owner note, is identical in every member
        }

        Assert.Equal(ProbabilityModel.V2.Stages.Select(s => (s.Id, s.Order, s.RoadmapStep, s.ConditionedOn, string.Join(";", s.Conditions.Select(c => c.Key + "=" + string.Join(",", c.Outcomes))))),
            ProbabilityModel.V3.Stages.Select(s => (s.Id, s.Order, s.RoadmapStep, s.ConditionedOn, string.Join(";", s.Conditions.Select(c => c.Key + "=" + string.Join(",", c.Outcomes))))));
        Assert.Equal(ProbabilityModel.V2.TendencyStages.Select(t => (t.Stage, string.Join(",", t.Favoured), t.CapIsContact)),
            ProbabilityModel.V3.TendencyStages.Select(t => (t.Stage, string.Join(",", t.Favoured), t.CapIsContact)));
    }

    [Fact]
    public void The_v3_framing_slot_is_active_scoped_and_owns_only_the_strike_outcome_of_called_pitch()
    {
        var framing = ProbabilityModel.V3.Slot("catcher.framing")!;
        Assert.False(framing.Reserved);
        Assert.True(ProbabilityModel.V3.FramingActive);
        Assert.Equal(ModifierConditionScope.NonAutomatedBorderline, framing.ConditionScope);
        Assert.Equal(StageIds.CalledPitch, framing.Stage);
        Assert.Equal(["strike"], framing.Targets);
        Assert.Equal(ModifierKind.Linear, framing.Kind);
        Assert.Equal((0, 100), (framing.InputMin, framing.InputMax));
        Assert.True(framing.HigherInputRaisesFactor);
        Assert.Equal(ZoneScope.All, framing.Scope);    // the zone scope is the batter_decision tool; Framing's scope is the new condition scope
    }

    [Fact]
    public void V1_and_V2_keep_framing_reserved_and_every_other_slot_of_every_model_has_the_open_condition_scope()
    {
        foreach (var model in new[] { ProbabilityModel.V1, ProbabilityModel.V2 })
        {
            var framing = model.Slot("catcher.framing")!;
            Assert.True(framing.Reserved);
            Assert.False(model.FramingActive);
            Assert.Equal(ModifierConditionScope.All, framing.ConditionScope);
        }

        foreach (var model in new[] { ProbabilityModel.V1, ProbabilityModel.V2, ProbabilityModel.V3 })
            Assert.All(model.Slots.Where(s => s.Id != "catcher.framing" || model != ProbabilityModel.V3), s => Assert.Equal(ModifierConditionScope.All, s.ConditionScope));
        // reserved slots stay reserved in v3, except framing
        Assert.Equal(["catcher.game_calling", "battery.chemistry"], ProbabilityModel.V3.Slots.Where(s => s.Reserved).Select(s => s.Id));
    }

    [Fact]
    public void Game_calling_and_battery_chemistry_stay_reserved_neutral_only_in_v3()
    {
        foreach (var id in new[] { "catcher.game_calling", "battery.chemistry" })
        {
            var slot = ProbabilityModel.V3.Slot(id)!;
            Assert.True(slot.Reserved);
            Assert.Equal(ModifierConditionScope.All, slot.ConditionScope);
        }
    }

    // ---------------------------------------------------------------- the zone classification Framing depends on

    [Fact]
    public void The_zone_model_classifies_twelve_zones_as_borderline_and_five_as_clear_exactly_as_the_design_table_says()
    {
        var model = PitchZoneModel.V1;
        Assert.Equal(17, model.Zones.Count);
        Assert.Equal(BorderlineZones.Order(StringComparer.Ordinal), model.Zones.Where(z => z.Boundary == BoundaryClass.Borderline).Select(z => z.Id.Value).Order(StringComparer.Ordinal));
        Assert.Equal(ClearZones.Order(StringComparer.Ordinal), model.Zones.Where(z => z.Boundary == BoundaryClass.Clear).Select(z => z.Id.Value).Order(StringComparer.Ordinal));
    }

    // ---------------------------------------------------------------- the called_pitch key is read back strictly

    [Fact]
    public void Every_key_the_model_builds_is_read_back_to_its_mode_and_zone()
    {
        foreach (var zone in PitchZoneModel.V1.Zones)
            foreach (var mode in new[] { CalledPitchMode.Automated, CalledPitchMode.NonAutomated })
            {
                var key = ConditionKeys.CalledPitch(mode, zone.Id).Value;
                var parsed = ConditionKeys.ParseCalledPitch(key).Value;
                Assert.Equal((mode, zone.Id), (parsed.Mode, parsed.Zone));
            }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(":")]
    [InlineData("automated")]
    [InlineData("non_automated")]
    [InlineData(":in.r1c1")]
    [InlineData("automated:")]
    [InlineData("non_automated:")]
    [InlineData("Automated:in.r1c1")]
    [InlineData("NON_AUTOMATED:in.r1c1")]
    [InlineData(" automated:in.r1c1")]
    [InlineData("automated :in.r1c1")]
    [InlineData("automated: in.r1c1")]
    [InlineData("automated:in.r1c1 ")]
    [InlineData("automated:in.r1c1:x")]
    [InlineData("automated::in.r1c1")]
    [InlineData("non:in.r1c1")]
    [InlineData("auto:in.r1c1")]
    [InlineData("xnon_automated:in.r1c1")]
    [InlineData("non_automated_x:in.r1c1")]
    [InlineData("non_automatedin.r1c1")]
    [InlineData("automated:in.r9c9")]
    [InlineData("automated:IN.R1C1")]
    [InlineData("manual:in.r1c1")]
    [InlineData("in.r1c1")]
    public void A_malformed_mode_zone_or_separator_is_an_error_never_a_guess(string? key)
    {
        var parsed = ConditionKeys.ParseCalledPitch(key);
        Assert.False(parsed.IsSuccess);
        Assert.Equal("CalledPitchCondition.invalid", parsed.Error!.Value.Code);
    }

    [Fact]
    public void An_undeclared_or_invalid_call_mode_still_cannot_build_a_key()
    {
        var zone = PitchZoneId.Create("in.r1c2").Value;
        Assert.Equal("CalledPitchMode.undeclared", ConditionKeys.CalledPitch(null, zone).Error!.Value.Code);
        Assert.Equal("CalledPitchMode.invalid", ConditionKeys.CalledPitch((CalledPitchMode)7, zone).Error!.Value.Code);
    }
}
