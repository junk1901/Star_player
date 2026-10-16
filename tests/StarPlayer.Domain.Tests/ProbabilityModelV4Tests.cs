using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;

namespace StarPlayer.Domain.Tests;

/// <summary>
/// Phase 3-E (Game Calling v4, GC-A): <c>prob-model-v4</c> is <c>prob-model-v3</c> with exactly one slot changed - <c>catcher.game_calling</c> is active and limited by
/// <see cref="ModifierConditionScope.BorderlineTarget"/>. v1, v2 and v3 are never edited; model/tuning pairs are an explicit table; the new scope is enforced at the
/// <see cref="StageProbability"/> boundary and fails closed for any pairing it does not define.
/// </summary>
public class ProbabilityModelV4Tests
{
    private static string Describe(ModifierSlot s) =>
        $"{s.Id}|{s.Stage}|{string.Join(",", s.Targets)}|{s.Kind}|{s.Scope}|{s.InputMin}|{s.InputMax}|{s.HigherInputRaisesFactor}|{string.Join(",", s.TableOrder)}|{s.Reserved}|{s.OwnerNote}|{s.ConditionScope}";

    // The Borderline set, written out here from docs/game-engine.md section 18 (NOT read from the model): the eight non-centre inner cells and the four outer zones that border an edge.
    private static readonly string[] BorderlineZones =
        ["in.r1c1", "in.r1c2", "in.r1c3", "in.r2c1", "in.r2c3", "in.r3c1", "in.r3c2", "in.r3c3", "out.high", "out.in", "out.low", "out.out"];

    private static readonly string[] ClearZones = ["in.r2c2", "out.high_in", "out.low_in", "out.low_out", "out.high_out"];

    private static readonly ProbabilityModel[] Models = [ProbabilityModel.V1, ProbabilityModel.V2, ProbabilityModel.V3, ProbabilityModel.V4];

    private static ModifierSlot GameCallingSlot => ProbabilityModel.V4.Slot("catcher.game_calling")!;

    // ---------------------------------------------------------------- versions and the explicit pairing table

    [Fact]
    public void V4_is_a_known_model_and_tuning_version_and_a_fifth_is_not()
    {
        Assert.Equal("prob-model-v4", ProbabilityModelVersion.Create("prob-model-v4").Value.Value);
        Assert.Equal("tuning-virtual-proto-v4", TuningVersion.Create("tuning-virtual-proto-v4").Value.Value);
        Assert.Same(ProbabilityModel.V4, ProbabilityModel.Resolve(ProbabilityModelVersion.V4).Value);
        Assert.Equal("prob-model-v4", ProbabilityModel.V4.Version.Value);
        foreach (var unknown in new[] { null, "", "prob-model-v5", "PROB-MODEL-V4", " prob-model-v4", "prob-model-v4 ", "prob-model-v40" })
            Assert.Equal("ProbabilityModelVersion.unsupported", ProbabilityModelVersion.Create(unknown).Error!.Value.Code);
        foreach (var unknown in new[] { null, "", "tuning-virtual-proto-v5", "TUNING-VIRTUAL-PROTO-V4", "tuning-virtual-proto-v4 " })
            Assert.Equal("TuningVersion.unsupported", TuningVersion.Create(unknown).Error!.Value.Code);
        // the earlier versions are still the same objects
        Assert.Same(ProbabilityModel.V1, ProbabilityModel.Resolve(ProbabilityModelVersion.V1).Value);
        Assert.Same(ProbabilityModel.V2, ProbabilityModel.Resolve(ProbabilityModelVersion.V2).Value);
        Assert.Same(ProbabilityModel.V3, ProbabilityModel.Resolve(ProbabilityModelVersion.V3).Value);
    }

    [Fact]
    public void Exactly_the_four_diagonal_pairs_are_allowed_and_the_other_twelve_combinations_are_not()
    {
        ProbabilityModelVersion[] models = [ProbabilityModelVersion.V1, ProbabilityModelVersion.V2, ProbabilityModelVersion.V3, ProbabilityModelVersion.V4];
        TuningVersion[] tunings = [TuningVersion.VirtualProtoV1, TuningVersion.VirtualProtoV2, TuningVersion.VirtualProtoV3, TuningVersion.VirtualProtoV4];
        var allowed = 0;
        for (var m = 0; m < 4; m++)
            for (var t = 0; t < 4; t++)
            {
                Assert.Equal(m == t, ProbabilityModel.Pairs(models[m], tunings[t]));
                if (ProbabilityModel.Pairs(models[m], tunings[t])) allowed++;
            }

        Assert.Equal(4, allowed);
        for (var i = 0; i < 4; i++)
            Assert.Equal(tunings[i], ProbabilityModel.TuningFor(models[i]).Value);
        Assert.Equal("ProbabilityModelVersion.unsupported", ProbabilityModel.TuningFor(default).Error!.Value.Code);
        Assert.False(ProbabilityModel.Pairs(default, TuningVersion.VirtualProtoV1));
        Assert.False(ProbabilityModel.Pairs(ProbabilityModelVersion.V4, default));
    }

    // ---------------------------------------------------------------- structure: v4 = v3 + one slot

    [Fact]
    public void V4_differs_from_v3_in_exactly_the_game_calling_slot()
    {
        Assert.Equal(ProbabilityModel.V3.Slots.Select(s => s.Id), ProbabilityModel.V4.Slots.Select(s => s.Id));
        foreach (var v3 in ProbabilityModel.V3.Slots)
        {
            var v4 = ProbabilityModel.V4.Slot(v3.Id)!;
            if (v3.Id == "catcher.game_calling")
                Assert.NotEqual(Describe(v3), Describe(v4));
            else
                Assert.Equal(Describe(v3), Describe(v4));   // every other slot, including the Framing scope, the Control slot and its owner note, is identical in every member
        }

        static IEnumerable<(string, int, int, string, string)> StageShape(ProbabilityModel m) =>
            m.Stages.Select(s => (s.Id, s.Order, s.RoadmapStep, s.ConditionedOn, string.Join(";", s.Conditions.Select(c => c.Key + "=" + string.Join(",", c.Outcomes)))));
        Assert.Equal(StageShape(ProbabilityModel.V3), StageShape(ProbabilityModel.V4));
        Assert.Equal(ProbabilityModel.V3.TendencyStages.Select(t => (t.Stage, string.Join(",", t.Favoured), t.CapIsContact)),
            ProbabilityModel.V4.TendencyStages.Select(t => (t.Stage, string.Join(",", t.Favoured), t.CapIsContact)));
    }

    [Fact]
    public void The_v4_game_calling_slot_is_active_scoped_to_the_aimed_zone_and_shares_the_control_outcome()
    {
        var slot = GameCallingSlot;
        Assert.False(slot.Reserved);
        Assert.True(ProbabilityModel.V4.GameCallingActive);
        Assert.Equal(ModifierConditionScope.BorderlineTarget, slot.ConditionScope);
        Assert.Equal(StageIds.ActualLocation, slot.Stage);
        Assert.Equal([ProbabilityModel.CondTarget], slot.Targets);
        Assert.Equal(ModifierKind.Linear, slot.Kind);
        Assert.Equal((0, 100), (slot.InputMin, slot.InputMax));
        Assert.True(slot.HigherInputRaisesFactor);
        Assert.Equal(ZoneScope.All, slot.Scope);                       // the zone scope is the batter_decision tool; this slot's scope is the new condition scope
        Assert.Empty(slot.TableOrder);
        // it multiplies the same outcome as pitcher.control, directly after it in the fixed slot order
        var control = ProbabilityModel.V4.Slot("pitcher.control")!;
        Assert.Equal(control.Stage, slot.Stage);
        Assert.Equal(control.Targets, slot.Targets);
        Assert.Equal(ProbabilityModel.V4.Slots.ToList().IndexOf(control) + 1, ProbabilityModel.V4.Slots.ToList().IndexOf(slot));
        Assert.Equal(ModifierConditionScope.All, control.ConditionScope);
    }

    [Fact]
    public void V1_V2_and_V3_keep_game_calling_reserved_with_the_open_condition_scope()
    {
        foreach (var model in new[] { ProbabilityModel.V1, ProbabilityModel.V2, ProbabilityModel.V3 })
        {
            var slot = model.Slot("catcher.game_calling")!;
            Assert.True(slot.Reserved);
            Assert.False(model.GameCallingActive);
            Assert.Equal(ModifierConditionScope.All, slot.ConditionScope);
            Assert.Equal(StageIds.ActualLocation, slot.Stage);
        }

        Assert.False(ProbabilityModel.V1.FramingActive);
        Assert.False(ProbabilityModel.V2.FramingActive);
        Assert.True(ProbabilityModel.V3.FramingActive);
        Assert.True(ProbabilityModel.V4.FramingActive);               // v4 keeps the Framing slot of v3
        Assert.Equal(ModifierConditionScope.NonAutomatedBorderline, ProbabilityModel.V4.Slot("catcher.framing")!.ConditionScope);
    }

    [Fact]
    public void Only_battery_chemistry_stays_reserved_in_v4_and_every_reserved_slot_has_the_open_scope()
    {
        Assert.Equal(["battery.chemistry"], ProbabilityModel.V4.Slots.Where(s => s.Reserved).Select(s => s.Id));
        Assert.True(ProbabilityModel.V4.Slot("battery.chemistry")!.Reserved);
        foreach (var model in Models)
            Assert.All(model.Slots.Where(s => s.Reserved), s => Assert.Equal(ModifierConditionScope.All, s.ConditionScope));
        // exactly one non-open scope per active model: v1/v2 none, v3 framing, v4 framing + game calling
        Assert.All(ProbabilityModel.V1.Slots, s => Assert.Equal(ModifierConditionScope.All, s.ConditionScope));
        Assert.All(ProbabilityModel.V2.Slots, s => Assert.Equal(ModifierConditionScope.All, s.ConditionScope));
        Assert.Equal(["catcher.framing"], ProbabilityModel.V3.Slots.Where(s => s.ConditionScope != ModifierConditionScope.All).Select(s => s.Id));
        Assert.Equal(["catcher.game_calling", "catcher.framing"], ProbabilityModel.V4.Slots.Where(s => s.ConditionScope != ModifierConditionScope.All).Select(s => s.Id));
    }

    [Fact]
    public void The_scope_enum_has_exactly_the_three_stable_values()
    {
        Assert.Equal([(0, "All"), (1, "NonAutomatedBorderline"), (2, "BorderlineTarget")],
            Enum.GetValues<ModifierConditionScope>().Select(v => ((int)v, v.ToString())).OrderBy(x => x.Item1));
    }

    // ---------------------------------------------------------------- the scope at the StageProbability boundary

    [Fact]
    public void The_borderline_target_scope_reads_the_aimed_zone_class_of_all_seventeen_zones()
    {
        Assert.Equal(12, BorderlineZones.Length);
        Assert.Equal(5, ClearZones.Length);
        var stage = ProbabilityModel.V4.Stage(StageIds.ActualLocation)!;
        var applies = 0;
        foreach (var zone in PitchZoneModel.V1.Zones.Select(z => z.Id.Value))
        {
            var result = StageProbability.AppliesTo(GameCallingSlot, stage, zone);
            Assert.True(result.IsSuccess, zone);
            Assert.Equal(BorderlineZones.Contains(zone), result.Value);
            Assert.Equal(!ClearZones.Contains(zone), result.Value);
            if (result.Value) applies++;
        }

        Assert.Equal(12, applies);
    }

    [Fact]
    public void The_scope_fails_closed_for_any_stage_or_scope_pairing_it_does_not_define()
    {
        // BorderlineTarget on every stage except actual_location
        foreach (var stage in ProbabilityModel.V4.Stages.Where(s => s.Id != StageIds.ActualLocation))
        {
            var slot = GameCallingSlot with { Stage = stage.Id };
            var result = StageProbability.AppliesTo(slot, stage, stage.Conditions[0].Key);
            Assert.False(result.IsSuccess, stage.Id);
            Assert.Equal("Modifier.condition_scope_unsupported", result.Error!.Value.Code);
        }

        // NonAutomatedBorderline on actual_location, and each scope on the other stage
        var location = ProbabilityModel.V4.Stage(StageIds.ActualLocation)!;
        var called = ProbabilityModel.V4.Stage(StageIds.CalledPitch)!;
        Assert.Equal("Modifier.condition_scope_unsupported",
            StageProbability.AppliesTo(GameCallingSlot with { ConditionScope = ModifierConditionScope.NonAutomatedBorderline }, location, "in.r1c1").Error!.Value.Code);
        Assert.Equal("Modifier.condition_scope_unsupported",
            StageProbability.AppliesTo(ProbabilityModel.V4.Slot("catcher.framing")! with { ConditionScope = ModifierConditionScope.BorderlineTarget }, called, "non_automated:in.r1c1").Error!.Value.Code);
        // a value this build does not know is an error, never "applies" and never "does not apply"
        foreach (var undefined in new[] { (ModifierConditionScope)3, (ModifierConditionScope)99, (ModifierConditionScope)(-1) })
            foreach (var stage in new[] { location, called })
                Assert.Equal("Modifier.condition_scope_unsupported", StageProbability.AppliesTo(GameCallingSlot with { ConditionScope = undefined }, stage, stage.Conditions[0].Key).Error!.Value.Code);
    }

    [Fact]
    public void An_aimed_zone_condition_that_is_not_a_registered_zone_is_an_error_not_a_neutral_result()
    {
        var location = ProbabilityModel.V4.Stage(StageIds.ActualLocation)!;
        foreach (var bad in new[] { "", " ", "bogus", "IN.R1C1", "in.r1c1 ", "in.r4c1", "out.up", "non_automated:in.r1c1", "automated:in.r1c1", "balanced", "attack" })
        {
            var result = StageProbability.AppliesTo(GameCallingSlot, location, bad);
            Assert.False(result.IsSuccess, $"'{bad}' must be an error");
        }
    }

    [Fact]
    public void The_open_scope_and_the_framing_scope_keep_their_previous_meaning()
    {
        var called = ProbabilityModel.V3.Stage(StageIds.CalledPitch)!;
        var framing = ProbabilityModel.V3.Slot("catcher.framing")!;
        Assert.True(StageProbability.AppliesTo(framing, called, "non_automated:in.r1c1").Value);        // non_automated Borderline
        Assert.False(StageProbability.AppliesTo(framing, called, "non_automated:in.r2c2").Value);       // non_automated Clear
        Assert.False(StageProbability.AppliesTo(framing, called, "automated:in.r1c1").Value);           // automated Borderline
        Assert.False(StageProbability.AppliesTo(framing, called, "automated:in.r2c2").Value);
        Assert.Equal("CalledPitchCondition.invalid", StageProbability.AppliesTo(framing, called, "in.r1c1").Error!.Value.Code);
        // an open scope applies everywhere in its own stage
        var control = ProbabilityModel.V4.Slot("pitcher.control")!;
        var location = ProbabilityModel.V4.Stage(StageIds.ActualLocation)!;
        Assert.All(PitchZoneModel.V1.Zones, z => Assert.True(StageProbability.AppliesTo(control, location, z.Id.Value).Value));
    }
}
