using StarPlayer.Domain.Baseball.Probability;

namespace StarPlayer.Domain.Tests;

/// <summary>
/// Phase 3-D (owner decision P4): <c>prob-model-v2</c> is v1 with the three park slots active. v1 is never edited; v2 keeps every slot name, stage and owned outcome, adds no stage and no
/// slot, and activates only the three park slots over the model's own 800-1250 permille range.
/// </summary>
public class ProbabilityModelV2Tests
{
    private static readonly string[] ParkSlots = ["park.home_runs", "park.hits", "park.extra_base_hits"];

    [Fact]
    public void V1_is_unchanged_its_park_slots_stay_reserved_over_the_domain_range()
    {
        foreach (var id in ParkSlots)
        {
            var slot = ProbabilityModel.V1.Slot(id)!;
            Assert.True(slot.Reserved);
            Assert.Equal((1, 3000), (slot.InputMin, slot.InputMax));
        }

        Assert.Equal("prob-model-v1", ProbabilityModel.V1.Version.Value);
    }

    [Fact]
    public void V2_activates_exactly_the_three_park_slots_over_800_to_1250_permille()
    {
        Assert.Equal("prob-model-v2", ProbabilityModel.V2.Version.Value);
        foreach (var id in ParkSlots)
        {
            var slot = ProbabilityModel.V2.Slot(id)!;
            Assert.False(slot.Reserved);
            Assert.Equal(ModifierKind.Linear, slot.Kind);
            Assert.Equal((ProbabilityModel.ParkInputMin, ProbabilityModel.ParkInputMax), (slot.InputMin, slot.InputMax));
            Assert.Equal((800, 1250), (slot.InputMin, slot.InputMax));
            Assert.True(slot.HigherInputRaisesFactor);
            Assert.Equal(ZoneScope.All, slot.Scope);
        }

        // still reserved in v2: the slots that belong to later packages
        foreach (var id in new[] { "catcher.game_calling", "battery.chemistry", "catcher.framing" })
            Assert.True(ProbabilityModel.V2.Slot(id)!.Reserved);
        Assert.Equal(ParkSlots.Length, ProbabilityModel.V2.Slots.Count(s => ParkSlots.Contains(s.Id)));
    }

    [Fact]
    public void V2_keeps_every_slot_name_stage_target_and_the_whole_stage_structure_of_v1()
    {
        Assert.Equal(ProbabilityModel.V1.Slots.Select(s => s.Id), ProbabilityModel.V2.Slots.Select(s => s.Id));
        Assert.Equal(ProbabilityModel.V1.Stages.Select(s => (s.Id, s.Order, s.RoadmapStep)), ProbabilityModel.V2.Stages.Select(s => (s.Id, s.Order, s.RoadmapStep)));
        for (var i = 0; i < ProbabilityModel.V1.Stages.Count; i++)
            Assert.Equal(ProbabilityModel.V1.Stages[i].Conditions.Select(c => (c.Key, string.Join(",", c.Outcomes))), ProbabilityModel.V2.Stages[i].Conditions.Select(c => (c.Key, string.Join(",", c.Outcomes))));
        foreach (var v1 in ProbabilityModel.V1.Slots)
        {
            var v2 = ProbabilityModel.V2.Slot(v1.Id)!;
            Assert.Equal(v1.Stage, v2.Stage);
            Assert.Equal(v1.Targets, v2.Targets);
            if (!ParkSlots.Contains(v1.Id))
                Assert.Equal(Describe(v1), Describe(v2));   // every other slot is identical in every member (the lists are compared by content)
        }

        Assert.Equal(ProbabilityModel.V1.TendencyStages.Select(t => (t.Stage, t.CapIsContact)), ProbabilityModel.V2.TendencyStages.Select(t => (t.Stage, t.CapIsContact)));
    }

    private static string Describe(ModifierSlot s) =>
        $"{s.Id}|{s.Stage}|{string.Join(",", s.Targets)}|{s.Kind}|{s.Scope}|{s.InputMin}|{s.InputMax}|{s.HigherInputRaisesFactor}|{string.Join(",", s.TableOrder)}|{s.Reserved}|{s.OwnerNote}";

    [Fact]
    public void The_three_park_slots_own_disjoint_outcomes_of_defense_result_only()
    {
        var owned = ParkSlots.SelectMany(id => ProbabilityModel.V2.Slot(id)!.Targets.Select(t => (Slot: id, Outcome: t))).ToArray();
        Assert.Equal(["double", "home_run", "single", "triple"], owned.Select(o => o.Outcome).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(owned.Length, owned.Select(o => o.Outcome).Distinct().Count());
        Assert.All(ParkSlots, id => Assert.Equal(StageIds.DefenseResult, ProbabilityModel.V2.Slot(id)!.Stage));
        // the fielding slot owns "out" and nothing else of that stage, so no outcome of defense_result has two slots
        var defenceTargets = ProbabilityModel.V2.Slots.Where(s => s.Stage == StageIds.DefenseResult).SelectMany(s => s.Targets).ToArray();
        Assert.Equal(defenceTargets.Length, defenceTargets.Distinct().Count());
    }

    [Fact]
    public void Resolve_knows_exactly_the_two_versions_and_hands_out_the_shared_instances()
    {
        Assert.Same(ProbabilityModel.V1, ProbabilityModel.Resolve(ProbabilityModelVersion.V1).Value);
        Assert.Same(ProbabilityModel.V2, ProbabilityModel.Resolve(ProbabilityModelVersion.V2).Value);
        Assert.False(ProbabilityModel.Resolve(default).IsSuccess);
        Assert.NotSame(ProbabilityModel.V1, ProbabilityModel.V2);
    }
}
