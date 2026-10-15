using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-D v2 (owner decisions P1/P2, R1): the slot inputs of the `defense_result` stage that the nine ability / strategy slots of PitchModifierInputs do not carry: the three park slots
// of the game's retained ParkFactorSnapshot and the defence.fielding slot of the representative fielder (R1). It draws nothing, owns no probability and changes no rating or snapshot.

/// <summary>The park and fielding inputs of <c>defense_result</c>.</summary>
internal static class DefenseSlots
{
    /// <summary>
    /// <paramref name="inputs"/> (the stage's nine-slot inputs, or null when the request carries no abilities) extended, for <c>defense_result</c> only, with (1) the game's park inputs:
    /// <c>park.home_runs</c> (home_run only), <c>park.hits</c> (single only) and <c>park.extra_base_hits</c> (double and triple only), the permille values going in as they are (the active
    /// model range-checks them: no clamp, no neutral substitute; a legacy game with no execution contract adds nothing), and (2) <c>defense.fielding</c> (out only) with the base fielding
    /// rating of the representative fielder of the drawn batted ball <paramref name="battedBall"/> (the stage's condition key), when fielding inputs were frozen for the pitch.
    /// </summary>
    public static IReadOnlyList<ModifierInput>? Extend(IReadOnlyList<ModifierInput>? inputs, string stage, string battedBall, GameState state, PitchFieldingInputs? fielding)
    {
        if (stage != StageIds.DefenseResult)
            return inputs;
        var list = new List<ModifierInput>(inputs ?? []);
        if (fielding?.FieldingFor(battedBall) is { } rating)
            list.Add(ModifierInput.Linear("defense.fielding", rating));
        if (state.Execution is { } execution)
        {
            var park = execution.Park;
            list.Add(ModifierInput.Linear("park.home_runs", park.HomeRuns.Permille));
            list.Add(ModifierInput.Linear("park.hits", park.Hits.Permille));
            list.Add(ModifierInput.Linear("park.extra_base_hits", park.ExtraBaseHits.Permille));
        }

        return list.Count == 0 ? inputs : list;
    }
}
