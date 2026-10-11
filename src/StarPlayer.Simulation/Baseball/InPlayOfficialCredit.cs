using StarPlayer.Domain.Baseball.Game;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-C2B-2 (ADR-007 item 3, docs/game-engine.md section 23.4): the official record of a game-ending hit, `walkoff-credit-v1`. The draw itself is never changed or re-drawn:
// only the template that states the FACTS of the play is chosen, and the batter really stops where the official record says (D3). Prototype simplification on a temporary
// 2026 OBR basis (9.06(f) with its comment, 9.06(g)); it is not a KBO certification.

internal static class InPlayOfficialCredit
{
    /// <summary>The last half of the final or an extra inning: the home team batting at or beyond the regulation length.</summary>
    public static bool WalkOffContext(GameState state) => state.BattingSide == TeamSide.Home && state.Inning >= state.Rules.RegulationInnings;

    /// <summary>Runs the home team needs to take the lead.</summary>
    public static int RunsNeeded(GameState state) => state.AwayRuns - state.HomeRuns + 1;

    /// <summary>Bases of a drawn hit (0 for anything else).</summary>
    public static int BasesOf(InPlayResult result) => result switch { InPlayResult.Single => 1, InPlayResult.Double => 2, InPlayResult.Triple => 3, _ => 0 };

    /// <summary>
    /// The template whose facts become the official play. Outside a walk-off context, and for everything but a drawn double or triple, it is the drawn template. In a walk-off
    /// context the runners that score under the drawn hit are listed lead runner first; when at least <paramref name="need"/> of them score, the need-th is the winning-run
    /// scorer and the hit is credited with only as many bases as he advanced (OBR 9.06(f)): <c>min(drawn bases, 4 - his starting base)</c>.
    /// </summary>
    public static InPlayTemplate Official(GameState state, InPlayTemplate drawn, int need)
    {
        var k = BasesOf(drawn.Result);
        if (!WalkOffContext(state) || drawn.Result is not (InPlayResult.Double or InPlayResult.Triple))
            return drawn;
        var scorers = state.Bases.Occupied().Select(o => o.Base).Where(b => b + k >= 4).OrderByDescending(b => b).ToList();
        if (scorers.Count < need)
            return drawn;
        var credited = Math.Min(k, 4 - scorers[need - 1]);
        return credited == k ? drawn : InPlayTemplateCatalog.Find(drawn.Class, credited == 1 ? InPlayResult.Single : InPlayResult.Double)!;
    }
}
