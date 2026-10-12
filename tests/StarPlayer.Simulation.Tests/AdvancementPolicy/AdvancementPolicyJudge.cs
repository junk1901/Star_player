using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Tests.InPlayFeasibility;
using Root = StarPlayer.Simulation.Tests.InPlayFeasibility.InPlayStates.Root;

namespace StarPlayer.Simulation.Tests.AdvancementPolicy;

// TEST-ONLY. The judge is written from the rules in docs/game-engine.md (sections 4, 23) and the OBR basis of the project, NOT from the reducer's code path, so that "the reducer
// accepted it" can be compared with "the rules allow it". It is a STATIC_POLICY_CHECK: no engine is called.

internal sealed record Judged(IReadOnlyList<string> Violations, int CountedRuns, IReadOnlyList<int> CountedKeys, int OutsAfter, bool HalfOver, bool WalkOffReached, int WinnerKey)
{
    public bool Legal => Violations.Count == 0;
}

internal static class AdvJudge
{
    public const string AttemptIntoOccupied = "ATTEMPT_INTO_OCCUPIED_BASE";
    public const string MoveAfterTerminal = "MOVE_AFTER_THIRD_OUT_OR_WINNING_RUN";
    public const string TooManyOuts = "MORE_THAN_THREE_OUTS";
    public const string BaseOccupiedTwice = "TWO_RUNNERS_ON_ONE_BASE";
    public const string RunnerPassed = "RUNNER_PASSED_A_RUNNER";
    public const string RunAfterWinning = "RUN_AFTER_WINNING_RUN";

    /// <summary>
    /// Judges one vector of actions (keyed by start base; Void is Hold). The movers are processed in event order: batter out first, then runners lead-first (<c>Lead</c>, also the
    /// reducer's default order) or trailing-first (<c>Trail</c>). A run counts if no third out was made before it, or if the third out is a non-force runner out made after it.
    /// </summary>
    public static Judged Judge(GameState s, InPlayPlanFacts f, IReadOnlyList<AdvDecision> ds, IReadOnlyDictionary<int, AdvAction> actions, AdvModel.SeqMode mode)
    {
        var violations = new List<string>();
        void Flag(string v) { if (!violations.Contains(v)) violations.Add(v); }
        var w = Work.From(s, f);
        var limit = w.Limit;
        var outs = s.Outs + (f.Official.Result == InPlayResult.Out ? 1 : 0);
        var runs = new List<(int Key, int Order)>();
        var order = 0;
        var thirdOrder = int.MaxValue;
        var thirdForcesNoRuns = false;
        var walkOffAt = int.MaxValue;
        var runsSoFar = f.Runs - f.Moves.Count(m => m.To == 4);               // the batter's own run (home run) is not a runner move; pending plans have none

        var movers = f.Moves.ToList();
        movers = mode == AdvModel.SeqMode.Trail ? movers.OrderBy(m => m.From).ToList() : movers.OrderByDescending(m => m.From).ToList();
        if (f.Official.Result == InPlayResult.Out && outs >= limit)
        {
            thirdOrder = 0;
            thirdForcesNoRuns = true;                                               // the batter-runner's own out is the third out
        }

        var final = new List<(int From, int To)>();
        foreach (var m in movers)
        {
            var d = ds.FirstOrDefault(x => x.StartBase == m.From);
            var a = d is null ? AdvAction.Hold : actions.GetValueOrDefault(d.StartBase, AdvAction.Hold);
            if (a == AdvAction.Void) a = AdvAction.Hold;
            var to = a switch { AdvAction.Extra => d!.TargetTo, AdvAction.Out => 0, _ => m.To };
            final.Add((m.From, to));
            order++;
            var ended = outs >= limit || (w.WalkOff && runsSoFar >= w.Need);
            if (a != AdvAction.Hold && ended)
                Flag(MoveAfterTerminal);
            if (a == AdvAction.Extra && d!.TargetTo < 4)
            {
                if (w.Occ[d.TargetTo] != 0) Flag(AttemptIntoOccupied);
                w.Occ[d.PlanTo] = 0;
                w.Occ[d.TargetTo] = d.RunnerKey;
            }
            else if (a == AdvAction.Out)
            {
                if (d!.TargetTo < 4 && w.Occ[d.TargetTo] != 0) Flag(AttemptIntoOccupied);
                w.Occ[d.PlanTo] = 0;
                outs++;
                if (outs > limit) Flag(TooManyOuts);
                if (outs == limit) thirdOrder = order;                              // a runner tagged out away from his base is a non-force out
            }
            else if (to == 4)
            {
                if (a == AdvAction.Extra) w.Occ[d!.PlanTo] = 0;
                runs.Add((m.RunnerKey, order));
                runsSoFar++;
                if (w.WalkOff && runsSoFar >= w.Need && walkOffAt == int.MaxValue) walkOffAt = order;
            }
            else if (a == AdvAction.Extra)
                w.Occ[d!.PlanTo] = 0;
        }

        // end positions: no two runners (or runner and batter-runner) on one base, nobody passes a runner ahead
        var onBaseList = final.Where(x => x.To != 0).ToList();
        if (f.Official.BatterDestination is >= 1 and <= 4)
            onBaseList.Add((0, f.Official.BatterDestination));
        var onBase = onBaseList.OrderByDescending(x => x.From).ToList();
        for (var i = 0; i < onBase.Count; i++)
            for (var j = i + 1; j < onBase.Count; j++)
            {
                if (onBase[j].To > onBase[i].To) Flag(RunnerPassed);
                else if (onBase[j].To == onBase[i].To && onBase[j].To < 4) Flag(BaseOccupiedTwice);
            }

        var counted = runs.Where(r => thirdOrder == int.MaxValue || (!thirdForcesNoRuns && r.Order < thirdOrder)).ToList();
        var walkOff = w.WalkOff && counted.Count >= w.Need;
        if (walkOff && counted.Count > w.Need) Flag(RunAfterWinning);
        return new Judged(violations, counted.Count, counted.Select(r => r.Key).ToList(), outs, outs >= limit, walkOff, walkOff ? counted[w.Need - 1].Key : -1);
    }
}

/// <summary>What the real, unchanged <see cref="GameReducer"/> did with an event (ACTUAL_REDUCER_CHECK).</summary>
internal sealed record RedRow(bool Ok, string Code, int Runs, int Voided, int OutsAfter, bool HalfOver, bool GameOver, GameOutcome? Outcome, BatterResult? Result, int Rbi, int Lob, IReadOnlyList<int> RunKeys, GameStep? Step);

internal static class AdvReducer
{
    public static RedRow Reduce(GameState s, InPlayEvent e)
    {
        var r = GameReducer.Apply(s, e);
        if (!r.IsSuccess)
            return new RedRow(false, r.Error!.Value.Code, 0, 0, 0, false, false, null, null, 0, 0, [], null);
        var step = r.Value;
        var play = step.NewPlays.Single();
        return new RedRow(true, "", play.Runs.Count, play.Moves.Count(m => m.RunVoided), play.OutsAfter, play.OutsAfter >= s.Rules.OutsPerHalfInning, step.State.Status == GameStatus.Final,
            step.State.Status == GameStatus.Final ? step.State.Outcome : null, play.Result,
            step.Delta.Entries.Where(x => x.Key == StatKey.BatRBI).Sum(x => x.Amount), step.Delta.Entries.Where(x => x.Key == StatKey.TeamLOB).Sum(x => x.Amount),
            play.Runs.Select(x => x.RunnerKey).ToList(), step);
    }

    /// <summary>LEGAL_ACCEPTED, LEGAL_MISMATCH (the judge allows it but the reducer refuses it or counts differently: a contract gap), ILLEGAL_REJECTED:code, ILLEGAL_ACCEPTED (the reducer cannot see the violation).</summary>
    public static string Classify(Judged j, RedRow r)
    {
        if (j.Legal)
        {
            if (!r.Ok) return "LEGAL_MISMATCH:rejected:" + r.Code;
            var same = r.Runs == j.CountedRuns && r.OutsAfter == j.OutsAfter && r.HalfOver == j.HalfOver && (r.GameOver && r.Outcome == GameOutcome.HomeWin) == j.WalkOffReached
                && r.RunKeys.OrderBy(k => k).SequenceEqual(j.CountedKeys.OrderBy(k => k));
            return same ? "LEGAL_ACCEPTED" : "LEGAL_MISMATCH:different_record";
        }

        return r.Ok ? "ILLEGAL_ACCEPTED" : "ILLEGAL_REJECTED:" + r.Code;
    }
}

/// <summary>One pending in-play case of the 5,400 approved C2B-2 cases (300 ordinary + 1,014 walk-off), with its real state, the production planner's facts and the open decisions.</summary>
internal sealed record PendingCase(string Key, bool WalkOffRoot, int Diff, int Occupancy, int Outs, Combo Combo, GameState State, InPlayPlanFacts Facts, IReadOnlyList<AdvDecision> Decisions)
{
    public int K => Decisions.Count;

    public Work StartWork => Work.From(State, Facts);
}

internal static class AdvCases
{
    private static readonly Lazy<IReadOnlyList<PendingCase>> Cached = new(Build);

    public static IReadOnlyList<PendingCase> All => Cached.Value;

    private static IReadOnlyList<PendingCase> Build()
    {
        var list = new List<PendingCase>();
        void Scan(Root root, bool walkOffKeys, int[] diffs)
        {
            foreach (var diff in diffs)
                for (var occ = 0; occ < 8; occ++)
                    for (var outs = 0; outs < 3; outs++)
                    {
                        var state = InPlayStates.Make(root, diff, occ, outs);
                        foreach (var combo in FeasibilityData.Positive)
                        {
                            var planned = InPlayBasePlanner.Plan(state, combo.Batted, combo.Result);
                            if (!planned.IsSuccess)
                                throw new InvalidOperationException($"planner refused {combo.Key}: {planned.Error!.Value.Code}");
                            var facts = planned.Value;
                            if (!facts.AdvancementRequired)
                                continue;
                            var key = $"{(walkOffKeys ? "w" : "g")}|{(walkOffKeys ? diff : 0)}|{occ}|{outs}|{combo.Key}";
                            list.Add(new PendingCase(key, walkOffKeys, walkOffKeys ? diff : 0, occ, outs, combo, state, facts, AdvModel.Decisions(facts)));
                        }
                    }
        }

        Scan(Root.GeneralTop, false, [0]);
        Scan(Root.WalkOff, true, [0, 1, 2, 3]);
        return list;
    }

    /// <summary>All 3^k raw action vectors of a case (decisions in lead-first order), as dictionaries keyed by start base.</summary>
    public static IEnumerable<(string Label, Dictionary<int, AdvAction> Actions)> RawVectors(PendingCase c)
    {
        var lead = AdvModel.InOrder(c.Decisions, DrawOrder.LeadFirst);
        var total = (int)Math.Pow(3, lead.Count);
        for (var n = 0; n < total; n++)
        {
            var actions = new Dictionary<int, AdvAction>();
            var rest = n;
            foreach (var d in lead)
            {
                actions[d.StartBase] = (AdvAction)(rest % 3);
                rest /= 3;
            }

            yield return (string.Join("/", lead.Select(d => actions[d.StartBase] switch { AdvAction.Extra => "E", AdvAction.Hold => "H", _ => "O" })), actions);
        }
    }

    /// <summary>The final-action key (start-base order) of an action dictionary, in the same notation as <see cref="AdvModel.Exact"/>.</summary>
    public static string KeyOf(PendingCase c, IReadOnlyDictionary<int, AdvAction> a) =>
        string.Join("/", c.Decisions.OrderBy(d => d.StartBase).Select(d => a.GetValueOrDefault(d.StartBase, AdvAction.Hold) switch { AdvAction.Extra => "E", AdvAction.Hold => "H", AdvAction.Out => "O", _ => "V" }));

    public static Dictionary<int, AdvAction> FromKey(PendingCase c, string key)
    {
        var parts = key.Split('/');
        var ordered = c.Decisions.OrderBy(d => d.StartBase).ToList();
        var map = new Dictionary<int, AdvAction>();
        for (var i = 0; i < ordered.Count; i++)
            map[ordered[i].StartBase] = parts[i] switch { "E" => AdvAction.Extra, "H" => AdvAction.Hold, "O" => AdvAction.Out, _ => AdvAction.Void };
        return map;
    }
}
