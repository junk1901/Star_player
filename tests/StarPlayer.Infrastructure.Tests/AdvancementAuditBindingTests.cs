using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.Pc;
using Policy = StarPlayer.Simulation.Baseball.AdvancementPolicy;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Codex F-01 (Phase 3-D final review): the commit gate must bind a resolved advancement's audit to the REAL <see cref="AdvancementContext"/> of the plan, not only to itself.
/// Every forgery here is internally consistent (the same Speed or Arm, the player is in the set, the recorded row/roll/result are untouched), so only the binding to the game's
/// own runner and Retriever can refuse it.
/// </summary>
public class AdvancementAuditBindingTests
{
    private const string Mismatch = "Commit.candidate_link_mismatch";

    // everyone has the same Speed and Arm: swapping in another registered player changes no rating, so ratings-only checks cannot see the swap
    private static (Driver D, PlayerInputSet Set) V2Game(int occupancy)
    {
        var set = Gx.Set(_ => (50, 70, 60));
        return (Gx.Situation(Gx.Exec(Gx.Park(), Policy.VersionV2, set.Id), occupancy, 0, false).Approach(), set);
    }

    private static IEnumerable<(AuthoritativeGame G, InPlayPitchCandidate C)> Resolved(Driver d, PlayerInputSet set, Func<PendingAdvancement, bool> where)
    {
        foreach (var (g, _, pending) in Gx.Pendings(d, Gx.Tuning2, set))
            if (where(pending))
                yield return (g, Ok(Gx.Resolve(g, pending, Gx.Tuning2, set)));
    }

    private static (AuthoritativeGame G, InPlayPitchCandidate C, PlayerInputSet Set) First(int occupancy, string key, Func<InPlayPitchCandidate, bool>? where = null)
    {
        var (d, set) = V2Game(occupancy);
        foreach (var (g, c) in Resolved(d, set, p => p.ContextKeys.Contains(key)))
            if (where?.Invoke(c) ?? true)
                return (g, c, set);
        throw new InvalidOperationException("no matching candidate");
    }

    private static InPlayPitchCandidate Forge(InPlayPitchCandidate c, Func<AdvancementAudit, AdvancementAudit> edit) =>
        c.WithAudit(c.Audit with { AdvancementDetail = edit(c.Audit.AdvancementDetail!) });

    private static AdvancementAudit WithDecision(AdvancementAudit a, int index, Func<AdvancementDecisionAudit, AdvancementDecisionAudit> edit) =>
        a with { Decisions = a.Decisions.SetItem(index, edit(a.Decisions[index])) };

    /// <summary>The commit gate refuses the forgery before any CAS: the aggregate, its RNG, the sequence and the cursor are exactly what they were.</summary>
    private static void AssertRefused(AuthoritativeGame g, PlayerInputSet set, InPlayPitchCandidate forged, string why)
    {
        var cursor = new GameCursor(g);
        var (hash, sequence, revision) = (g.Rng.StateHash, g.LastEventSequence, g.Revision);

        Assert.True(Code(Gx.Stage(g, forged, Gx.Tuning2, set)) == Mismatch, why);

        Assert.Same(g, cursor.Current);
        Assert.Equal((hash, sequence, revision), (g.Rng.StateHash, g.LastEventSequence, g.Revision));
    }

    // T1 — a different registered player with the SAME Speed as the real runner, named consistently in decision.Runner and Abilities.Runner
    [Fact]
    public void A_registered_player_with_the_same_speed_cannot_stand_in_for_the_real_runner()
    {
        var (g, c, set) = First(2, "single_runner_on_second");
        var decision = c.Audit.AdvancementDetail!.Decisions.Single();
        var other = PitchBranchFixture.BasePlayers.First(p => p != decision.Runner);
        Assert.Equal(set.Find(decision.Runner)!.Speed, set.Find(other)!.Speed);

        var forged = Forge(c, a => WithDecision(a, 0, x => x with { Runner = other, Abilities = x.Abilities! with { Runner = other } }));

        Assert.True(Gx.Stage(g, c, Gx.Tuning2, set).IsSuccess);
        AssertRefused(g, set, forged, "same-speed runner");
    }

    // T2 — another fielder (other position AND player) with the SAME Arm as the real Retriever, on a play that credits no throw-out so the assist check cannot catch it
    [Fact]
    public void A_fielder_with_the_same_arm_cannot_stand_in_for_the_real_retriever()
    {
        var (g, c, set) = First(2, "single_runner_on_second", x => x.Audit.AdvancementDetail!.Decisions.Single().Applied != AdvancementOutcome.Out);
        var decision = c.Audit.AdvancementDetail!.Decisions.Single();
        var real = decision.Abilities!;
        var alt = Enum.GetValues<FieldPosition>()
            .Select(p => (Position: p, Player: g.State.DefenderAt(g.State.FieldingSide, p)))
            .First(x => x.Position != real.Retriever && x.Player is { } id && id != real.RetrieverPlayer && set.Find(id)?.Arm == real.RetrieverArm);
        var (position, player) = (alt.Position, alt.Player!.Value);

        var forged = Forge(c, a => WithDecision(a, 0, x => x with { Abilities = real with { Retriever = position, RetrieverPlayer = player } }));

        AssertRefused(g, set, forged, "same-arm retriever");
    }

    // T3 — the context-bound fields of a decision, singly
    [Fact]
    public void A_decision_that_disagrees_with_the_real_context_in_any_bound_field_is_refused()
    {
        var (g, c, set) = First(2, "single_runner_on_second");
        var forgeries = new Dictionary<string, Func<AdvancementDecisionAudit, AdvancementDecisionAudit>>
        {
            ["runner key"] = x => x with { RunnerKey = x.RunnerKey + 1 },
            ["start base"] = x => x with { StartBase = x.StartBase + 1 },
            ["plan to"] = x => x with { PlanTo = x.PlanTo + 1 },
            ["target to"] = x => x with { TargetTo = x.TargetTo - 1 },
            ["order"] = x => x with { Order = x.Order + 1 },
        };

        foreach (var (name, edit) in forgeries)
            AssertRefused(g, set, Forge(c, a => WithDecision(a, 0, edit)), name);
    }

    // T3 — the decision list against the context list: order, duplicate, missing
    [Fact]
    public void Swapped_duplicated_or_missing_decisions_are_refused_when_the_play_has_two_contexts()
    {
        var (d, set) = V2Game(3);
        var (g, c) = Resolved(d, set, p => p.ContextKeys.Length == 2).First();
        var decisions = c.Audit.AdvancementDetail!.Decisions;
        Assert.Equal(2, decisions.Length);

        AssertRefused(g, set, Forge(c, a => a with { Decisions = [decisions[1], decisions[0]] }), "swapped");
        AssertRefused(g, set, Forge(c, a => a with { Decisions = [decisions[0], decisions[0]] }), "duplicated");
        AssertRefused(g, set, Forge(c, a => a with { Decisions = [decisions[0]] }), "missing");
        AssertRefused(g, set, Forge(c, a => WithDecision(a, 1, x => x with { Runner = decisions[0].Runner, Abilities = x.Abilities! with { Runner = decisions[0].Runner } })), "two decisions, one runner");
        Assert.True(Gx.Stage(g, c, Gx.Tuning2, set).IsSuccess);
    }
}
