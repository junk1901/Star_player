using StarPlayer.Domain.Baseball.Probability;

namespace StarPlayer.Domain.Tests;

/// <summary>
/// Phase 3-B2 fix round (Codex High x2): the public collections of a <see cref="Distribution"/> and of the shared <see cref="ProbabilityModel.V1"/> graph must not
/// hand out their backing storage. The tests deliberately try to write through casts (array, <c>IList</c>, <c>List</c>) instead of trusting the declared interface type.
/// </summary>
public class ProbabilityImmutabilityTests
{
    private const int D = ProbabilityScale.Denominator;

    private static Distribution Dist() => Distribution.Create(["a", "b"], [400_000, 600_000]).Value;

    // Tries every writer a consumer could reach by casting; none may succeed in changing what the object reports.
    private static void TryToWrite<T>(IReadOnlyList<T> exposed, T replacement)
    {
        if (exposed is T[] array)
            array[0] = replacement;
        if (exposed is IList<T> list)
        {
            try { list[0] = replacement; } catch (NotSupportedException) { }
            try { list.Add(replacement); } catch (NotSupportedException) { }
            try { list.Clear(); } catch (NotSupportedException) { }
        }

        if (exposed is System.Collections.IList nonGeneric)
        {
            try { nonGeneric[0] = replacement; } catch (NotSupportedException) { }
        }
    }

    [Fact]
    public void The_Codex_counterexample_cannot_change_the_reported_probabilities()
    {
        var d = Dist();

        TryToWrite(d.Ppm, 0);

        Assert.Equal([400_000, 600_000], d.Ppm);
        Assert.Equal(D, d.Ppm.Sum());
        Assert.Equal(400_000, d.Of("a").Value);
        Assert.Equal("a", d.Select(0).Value);
    }

    [Fact]
    public void The_outcome_ids_cannot_be_changed_through_the_public_collection()
    {
        var d = Dist();

        TryToWrite(d.Outcomes, "z");

        Assert.Equal(["a", "b"], d.Outcomes);
        Assert.Equal(400_000, d.Of("a").Value);
        Assert.Equal("Distribution.outcome_unknown", d.Of("z").Error!.Value.Code);
        Assert.Equal("a", d.Select(0).Value);
    }

    [Fact]
    public void The_public_collections_are_not_backed_by_an_array_or_a_writable_list()
    {
        var d = Dist();
        Assert.IsNotType<int[]>(d.Ppm);
        Assert.IsNotType<string[]>(d.Outcomes);
        Assert.Empty(ImmutabilityAudit.Violations(d));
    }

    [Fact]
    public void Repeated_reads_never_expose_a_collection_that_a_previous_write_could_have_changed()
    {
        var d = Dist();
        TryToWrite(d.Ppm, 7);
        TryToWrite(d.Ppm, 9);
        Assert.Equal([400_000, 600_000], d.Ppm.ToArray());
        Assert.Equal(["a", "b"], d.Outcomes.ToArray());
    }

    [Fact]
    public void The_public_probabilities_and_the_selection_always_describe_the_same_creation_time_values()
    {
        var ids = new[] { "a", "zero", "c" };
        var ppm = new[] { 250_000, 0, 750_000 };
        var d = Distribution.Create(ids, ppm).Value;
        ppm[0] = 0;
        ppm[1] = D;
        ids[2] = "q";
        TryToWrite(d.Ppm, 5);
        TryToWrite(d.Outcomes, "w");

        var cumulative = 0;
        for (var i = 0; i < d.Count; i++)
        {
            if (d.Ppm[i] == 0)
                continue;
            Assert.Equal(d.Outcomes[i], d.Select((uint)cumulative).Value);
            Assert.Equal(d.Outcomes[i], d.Select((uint)(cumulative + d.Ppm[i] - 1)).Value);
            cumulative += d.Ppm[i];
        }

        Assert.Equal(D, cumulative);
        Assert.Equal("a", d.Select(0).Value);
        Assert.Equal("c", d.Select(ProbabilityScale.RollBound - 1).Value);
    }

    [Fact]
    public void Normal_inputs_select_exactly_what_they_selected_before_the_fix()
    {
        // Frozen pre-fix behaviour: roll -> outcome for a mixed distribution (boundaries of every interval).
        var d = Distribution.Create(["w", "x", "y", "z"], [1, 0, 999_998, 1]).Value;
        Assert.Equal("w", d.Select(0).Value);
        Assert.Equal("y", d.Select(1).Value);
        Assert.Equal("y", d.Select(999_998).Value);
        Assert.Equal("z", d.Select(999_999).Value);
        Assert.Equal(3, d.SelectIndex(999_999).Value);
        Assert.Equal(ProbabilityScale.Denominator, d.Ppm.Sum());
        Assert.Equal("w=1,x=0,y=999998,z=1", d.ToString());
        Assert.True(d.Equals(Distribution.Create(["w", "x", "y", "z"], [1, 0, 999_998, 1]).Value));
        Assert.False(d.Equals(Distribution.Create(["w", "x", "y", "z"], [2, 0, 999_997, 1]).Value));
    }

    // ---- the shared model graph ----

    [Fact]
    public void Nested_outcome_lists_of_the_shared_model_cannot_be_changed()
    {
        var model = ProbabilityModel.V1;
        var expected = model.Stage(StageIds.ActualLocation)!.Find("in.r2c2")!.Outcomes.ToList();

        foreach (var stage in model.Stages)
            foreach (var condition in stage.Conditions)
                TryToWrite(condition.Outcomes, "tampered");

        Assert.Equal(expected, ProbabilityModel.V1.Stage(StageIds.ActualLocation)!.Find("in.r2c2")!.Outcomes);
        Assert.DoesNotContain("tampered", ProbabilityModel.V1.Stages.SelectMany(s => s.Conditions).SelectMany(c => c.Outcomes));
    }

    [Fact]
    public void The_stage_slot_and_tendency_lists_of_the_shared_model_cannot_be_changed()
    {
        var model = ProbabilityModel.V1;
        var stageIds = model.Stages.Select(s => s.Id).ToList();
        var slotTargets = model.Slots.Select(s => string.Join(",", s.Targets)).ToList();
        var tendency = model.TendencyStages.Select(t => string.Join(",", t.Favoured)).ToList();

        TryToWrite(model.Stages, model.Stages[1]);
        TryToWrite(model.Slots, model.Slots[0]);
        TryToWrite(model.TendencyStages, model.TendencyStages[0]);
        foreach (var stage in model.Stages)
            TryToWrite(stage.Conditions, stage.Conditions[0]);
        foreach (var slot in model.Slots)
        {
            TryToWrite(slot.Targets, "tampered");
            if (slot.TableOrder.Count > 0)
                TryToWrite(slot.TableOrder, "tampered");
        }

        foreach (var t in model.TendencyStages)
            TryToWrite(t.Favoured, "tampered");

        Assert.Equal(stageIds, ProbabilityModel.V1.Stages.Select(s => s.Id));
        Assert.Equal(slotTargets, ProbabilityModel.V1.Slots.Select(s => string.Join(",", s.Targets)));
        Assert.Equal(tendency, ProbabilityModel.V1.TendencyStages.Select(t => string.Join(",", t.Favoured)));
        Assert.Equal(34, ProbabilityModel.V1.Stage(StageIds.CalledPitch)!.Conditions.Count);
    }

    [Fact]
    public void The_whole_public_graph_of_the_shared_model_holds_no_array_and_no_writable_collection()
    {
        Assert.Empty(ImmutabilityAudit.Violations(ProbabilityModel.V1));
    }

    [Fact]
    public void Two_consumers_of_the_shared_model_see_the_same_structure_whatever_the_other_does()
    {
        var consumerA = ProbabilityModel.V1;
        var consumerB = ProbabilityModel.Resolve(ProbabilityModelVersion.V1).Value;
        Assert.Same(consumerA, consumerB); // a shared static instance, so it must be safe to share

        var before = Describe(consumerB);
        foreach (var stage in consumerA.Stages)
            foreach (var condition in stage.Conditions)
                TryToWrite(condition.Outcomes, "tampered");
        TryToWrite(consumerA.Stages, consumerA.Stages[0]);

        Assert.Equal(before, Describe(consumerB));
    }

    private static string Describe(ProbabilityModel model) =>
        string.Join(";", model.Stages.Select(s => $"{s.Id}:{string.Join("/", s.Conditions.Select(c => c.Key + "=" + string.Join(",", c.Outcomes)))}"))
        + "|" + string.Join(";", model.Slots.Select(s => $"{s.Id}:{string.Join(",", s.Targets)}:{string.Join(",", s.TableOrder)}"));
}
