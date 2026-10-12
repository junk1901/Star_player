using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-C2B-3 (P-02 sequential conditional composition, P-03 Hold attribution, P-04 rule pre-check): what is possible for one extra-base decision on the play as it stands,
// and a pre-check that names every forbidden situation of a vector of results. The unchanged GameReducer is a SECOND, weaker check (it accepts 480 of the 840 illegal vectors of
// the policy validation): "the reducer accepted it" is never the test of legality. Only the four declared contexts exist (single_runner_on_first/second, double_runner_on_first,
// fly_out_runner_on_third); a new context needs its own approved rules, so nothing here is written as a general baserunning model.

/// <summary>The play as it stands while decisions are applied one by one: occupancy after the base plan, outs, runs, and the two ways the play can already be over.</summary>
internal sealed class AdvancementWork
{
    /// <summary>Index 1-3: the runner key standing there (0 = empty, -1 = the batter-runner).</summary>
    public int[] Occ { get; } = new int[5];

    public int Outs { get; set; }

    public int Runs { get; set; }

    public int Limit { get; init; }

    public bool WalkOff { get; init; }

    public int Need { get; init; }

    public AdvancementWork Clone()
    {
        var copy = new AdvancementWork { Outs = Outs, Runs = Runs, Limit = Limit, WalkOff = WalkOff, Need = Need };
        Array.Copy(Occ, copy.Occ, Occ.Length);
        return copy;
    }
}

internal static class AdvancementRules
{
    public const string AttemptIntoOccupiedBase = "attempt_into_occupied_base";
    public const string MoveAfterPlayEnded = "move_after_play_ended";
    public const string TooManyOuts = "too_many_outs";
    public const string BaseOccupiedTwice = "base_occupied_twice";
    public const string RunnerPassedRunner = "runner_passed_runner";
    public const string RunAfterWinningRun = "run_after_winning_run";
    public const string SourceRunnerMissing = "source_runner_missing";
    public const string TargetInvalid = "target_invalid";
    public const string DuplicateRunnerAction = "duplicate_runner_action";
    public const string ActionCountMismatch = "action_count_mismatch";

    /// <summary>The play after the base plan and before any extra-base decision. The batter's own out (a caught fly ball) is already an out of the play.</summary>
    public static AdvancementWork Start(InPlayPlanFacts facts, GameState state)
    {
        var work = new AdvancementWork
        {
            Outs = facts.PrePlay.Outs + (facts.Official.Result == InPlayResult.Out ? 1 : 0), Runs = facts.Runs, Limit = state.Rules.OutsPerHalfInning,
            WalkOff = facts.WalkOffContext, Need = facts.RunsNeeded,
        };
        foreach (var move in facts.Moves)
            if (move.To is >= 1 and <= 3)
                work.Occ[move.To] = move.RunnerKey;
        if (facts.Official.BatterDestination is >= 1 and <= 3)
            work.Occ[facts.Official.BatterDestination] = -1;
        return work;
    }

    /// <summary>P-02 R2: the third out has been made, or the winning run has scored. No decision applies after that.</summary>
    public static bool IsEnded(AdvancementWork work) => work.Outs >= work.Limit || (work.WalkOff && work.Runs >= work.Need);

    /// <summary>P-02 R1: home is never blocked; any other base is open only while nobody stands on it. An occupied base cannot be attempted, so the extra base AND being thrown out there are both impossible.</summary>
    public static bool TargetOpen(AdvancementContext context, AdvancementWork work) => context.TargetTo == 4 || work.Occ[context.TargetTo] == 0;

    /// <summary>Applies an outcome the caller has already proved possible. Hold and Void move nobody.</summary>
    public static void Apply(AdvancementContext context, AdvancementOutcome outcome, AdvancementWork work)
    {
        if (outcome == AdvancementOutcome.Extra)
        {
            work.Occ[context.PlanTo] = 0;
            if (context.TargetTo == 4)
                work.Runs++;
            else
                work.Occ[context.TargetTo] = context.RunnerKey;
        }
        else if (outcome == AdvancementOutcome.Out)
        {
            work.Occ[context.PlanTo] = 0;
            work.Outs++;
        }
    }

    /// <summary>
    /// P-04: applies a vector of results EXACTLY as given (no repair) in processing order and names every forbidden situation it reaches, each code once, in order of first
    /// occurrence: wrong number of results, a runner decided twice, a start base without that runner, an attempt that is not one base further, an attempt on an occupied base
    /// (extra base or out), a move after the third out or the winning run, more than three outs, a run after the winning run, two runners on one base, and a runner who passed
    /// another. A Void result is a no-move like Hold. An empty result means the vector is a legal play of the declared contexts.
    /// </summary>
    public static ImmutableArray<string> Check(InPlayPlanFacts facts, GameState state, IReadOnlyList<AdvancementContext> contexts, IReadOnlyList<AdvancementOutcome> actions)
    {
        var found = new List<string>();
        void Flag(string code)
        {
            if (!found.Contains(code))
                found.Add(code);
        }

        if (actions.Count != contexts.Count)
        {
            Flag(ActionCountMismatch);
            return [.. found];
        }

        if (contexts.Select(c => c.RunnerKey).Distinct().Count() != contexts.Count)
            Flag(DuplicateRunnerAction);

        var work = Start(facts, state);
        var finalTo = new Dictionary<int, int>();   // start base -> final base of a decided runner
        for (var i = 0; i < contexts.Count; i++)
        {
            var context = contexts[i];
            var outcome = actions[i] == AdvancementOutcome.Void ? AdvancementOutcome.Hold : actions[i];
            var sourceOk = context.PlanTo is >= 1 and <= 3 && work.Occ[context.PlanTo] == context.RunnerKey;
            if (!sourceOk)
                Flag(SourceRunnerMissing);
            var targetOk = context.TargetTo == context.PlanTo + 1 && context.TargetTo <= 4;
            if (!targetOk)
                Flag(TargetInvalid);
            if (!sourceOk || !targetOk)
            {
                finalTo[context.StartBase] = context.PlanTo;
                continue;                                                                       // nothing sound to apply: the structural fault is already named
            }

            if (outcome != AdvancementOutcome.Hold)
            {
                if (IsEnded(work))
                    Flag(MoveAfterPlayEnded);
                if (!TargetOpen(context, work))
                    Flag(AttemptIntoOccupiedBase);
            }

            Apply(context, outcome, work);
            if (work.Outs > work.Limit)
                Flag(TooManyOuts);
            finalTo[context.StartBase] = outcome switch { AdvancementOutcome.Extra => context.TargetTo, AdvancementOutcome.Out => 0, _ => context.PlanTo };
        }

        if (work.WalkOff && work.Runs > work.Need)
            Flag(RunAfterWinningRun);

        // end positions: nobody shares a base (home excepted), nobody finishes ahead of a runner who started ahead of him
        var positions = facts.Moves.Select(m => (m.From, To: finalTo.TryGetValue(m.From, out var to) ? to : m.To)).Where(p => p.To != 0).ToList();
        if (facts.Official.BatterDestination is >= 1 and <= 4)
            positions.Add((0, facts.Official.BatterDestination));
        positions = positions.OrderByDescending(p => p.From).ToList();
        for (var i = 0; i < positions.Count; i++)
            for (var j = i + 1; j < positions.Count; j++)
            {
                if (positions[j].To > positions[i].To)
                    Flag(RunnerPassedRunner);
                else if (positions[j].To == positions[i].To && positions[j].To < 4)
                    Flag(BaseOccupiedTwice);
            }

        return [.. found];
    }

    /// <summary>
    /// Which runs count when the play ends the half-inning (the unchanged <see cref="ThirdOutRule"/>, the one place the reducer uses too): none after a third out that is the
    /// batter-runner or a force out, and otherwise only the runs made before it. The terminals must be given in sequence order.
    /// </summary>
    public static HashSet<int> CountedRuns(int outsBefore, int limit, IReadOnlyList<Terminal> ordered) => ThirdOutRule.Evaluate(outsBefore, limit, ordered, includeReconOuts: false).CountedRuns;
}
