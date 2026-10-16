using System.Text.RegularExpressions;
using StarPlayer.Simulation.Tests.InPlayFeasibility;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// Phase 3-D v2: which source files may create a <c>ModifierInput</c> and for which slots. The nine ability / strategy slots stay where 3-D put them; the park and fielding slots of
/// <c>defense_result</c> are connected in exactly one file and the runner / arm slots of <c>advancement</c> in exactly one other. The resolver and the planner never build an input
/// themselves, and no slot is connected in two places (that would be a double application).
/// </summary>
public class ModifierWiringPinTests
{
    private static readonly string Baseball = Path.Combine(FeasibilityData.Root, "src", "StarPlayer.Simulation", "Baseball");

    private static string[] Slots(string file) =>
        Regex.Matches(File.ReadAllText(Path.Combine(Baseball, file)), @"ModifierInput\.(?:Linear|Choice)\(""([a-z_.]+)""")
            .Select(m => m.Groups[1].Value).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    [Fact]
    public void The_nine_ability_and_strategy_slots_stay_in_the_pitch_modifier_inputs_file_and_nothing_else_does()
    {
        Assert.Equal(["batter.contact", "batter.eye", "batter.power", "matchup.handedness", "pitcher.control", "pitcher.stuff", "pitcher.velocity", "situation.count_pressure", "strategy.approach"],
            Slots("PitchModifierInputs.cs"));
    }

    [Fact]
    public void The_park_and_fielding_slots_are_connected_in_one_file_and_it_names_no_runner_or_arm_slot()
    {
        var file = File.ReadAllText(Path.Combine(Baseball, "DefenseSlots.cs"));

        Assert.Equal(["defense.fielding", "park.extra_base_hits", "park.hits", "park.home_runs"], Slots("DefenseSlots.cs"));
        Assert.DoesNotContain("runner.", file);
        Assert.DoesNotContain("defense.arm", file);
    }

    [Fact]
    public void The_runner_and_arm_slots_are_connected_in_one_file_and_it_names_no_park_or_fielding_slot()
    {
        var file = File.ReadAllText(Path.Combine(Baseball, "AdvancementSlots.cs"));

        Assert.Equal(["defense.arm", "runner.speed"], Slots("AdvancementSlots.cs"));
        Assert.DoesNotContain("park.", file);
        Assert.DoesNotContain("defense.fielding", file);
    }

    [Fact]
    public void No_other_simulation_file_builds_a_modifier_input_so_no_slot_can_be_connected_twice()
    {
        var creators = Directory.GetFiles(Baseball, "*.cs", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"ModifierInput\.(?:Linear|Choice)\("))
            .Select(f => Path.GetFileName(f)!).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["AdvancementSlots.cs", "DefenseSlots.cs", "FrozenCatcherInput.cs", "PitchModifierInputs.cs"], creators);   // Phase 3-E added exactly one file: the catcher's Framing (and, in v4, Game Calling); the pitcher's Control stays in PitchModifierInputs
    }

    [Fact]
    public void The_catcher_slots_are_connected_in_one_file_each_to_its_own_stage_only_and_name_no_other_slot()
    {
        var file = File.ReadAllText(Path.Combine(Baseball, "FrozenCatcherInput.cs"));

        // Framing -> called_pitch (Phase 3-E); Game Calling -> actual_location (Game Calling v4); nothing else is created in this file
        Assert.Equal(["catcher.framing", "catcher.game_calling"], Slots("FrozenCatcherInput.cs"));
        Assert.Contains("stage == StageIds.CalledPitch && catcher.ReceivingFraming is { } framing", file);
        Assert.Contains("stage == StageIds.ActualLocation && catcher.GameCalling is { } gameCalling", file);
        Assert.Single(Regex.Matches(file, @"ModifierInput\.Linear\(""catcher\.framing"""));
        Assert.Single(Regex.Matches(file, @"ModifierInput\.Linear\(""catcher\.game_calling"""));
        Assert.DoesNotContain("park.", file);
        Assert.DoesNotContain("defense.", file);
        Assert.DoesNotContain("pitcher.control", file);
        Assert.DoesNotContain("battery.chemistry", file);
        // the resolver only calls Extend; it never names either slot itself, and the frozen pitcher input creates no ModifierInput at all (pitcher.control keeps its single connection)
        var resolver = File.ReadAllText(Path.Combine(Baseball, "PitchBranchResolver.cs")).Replace("<c>catcher.framing</c>", "").Replace("<c>catcher.game_calling</c>", "");
        Assert.DoesNotContain("catcher.framing", resolver);
        Assert.DoesNotContain("catcher.game_calling", resolver);
        Assert.Empty(Slots("FrozenPitcherInput.cs"));
    }

    [Fact]
    public void Each_slot_is_passed_to_exactly_one_stage_computation_per_decision()
    {
        var planning = File.ReadAllText(Path.Combine(Baseball, "AdvancementPlanning.cs"));
        var resolver = File.ReadAllText(Path.Combine(Baseball, "PitchBranchResolver.cs"));

        // the planner computes each advancement row once, with the whole input list of the decision (R4: one Compute call, one normalisation)
        Assert.Single(Regex.Matches(planning, @"StageProbability\.Compute\("));
        Assert.Contains("StageProbability.Compute(tuning!, \"advancement\", contexts[i].Key, inputs)", planning);
        Assert.Single(Regex.Matches(resolver, @"StageProbability\.Compute\("));
        Assert.Contains("DefenseSlots.Extend(inputs, stage, condition, state, request.Fielding)", resolver);
    }
}
