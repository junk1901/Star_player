using System.Collections.Immutable;
using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Tests.InPlayFeasibility;

namespace StarPlayer.Simulation.Tests.AdvancementPolicy;

// Phase 3-C2B-3-P (policy validation). TEST-ONLY code: nothing here is production code, nothing in src/ was changed, and none of it is a candidate implementation of
// the future 3-C2B-3 engine. It reads the approved C2B-2 planner facts (InPlayBasePlanner.Plan), the approved `advancement` rows of virtual_tuning_v1.json and the real
// GameReducer, and compares candidate policies for P-01..P-10 of TASK.md section 12. Three kinds of evidence are kept apart in every test name and report line:
//   STATIC_POLICY_CHECK  - rules and combinations evaluated by the independent judge below (no engine involved)
//   TEST_HARNESS_CHECK   - candidate policies implemented here (resolver, exact distributions, RNG consumption)
//   ACTUAL_REDUCER_CHECK - events built here and applied to the unchanged production GameReducer

/// <summary>The three outcomes of the approved <c>advancement</c> stage, in the order of the model (<c>advance_extra</c>, <c>hold</c>, <c>out_on_bases</c>), plus "no longer applicable".</summary>
internal enum AdvAction { Extra = 0, Hold = 1, Out = 2, Void = 3 }

/// <summary>How a draw is made. <c>Ind</c> = independent per-context draws, no repair (the naive reading of prob-model-v1).</summary>
internal enum PolicyKind { Ind, A, B, C }

/// <summary>Which outcomes count as impossible when the base the runner would attempt is occupied: only the extra base (<c>ExtraOnly</c>) or every attempt, extra base and being thrown out (<c>AttemptCoupled</c>).</summary>
internal enum Coupling { AttemptCoupled, ExtraOnly }

/// <summary>Fixed = exactly one roll per declared context, always; Skip = no roll when the outcome is already forced (a single feasible outcome) or the play has ended.</summary>
internal enum Consumption { Fixed, Skip }

internal enum DrawOrder { LeadFirst, TrailFirst }

/// <summary>Pre = feasibility is decided before each draw from the working play; Post = every context is drawn independently first and the result is repaired afterwards.</summary>
internal enum Repair { Pre, Post }

internal sealed record PolicySpec(string Name, PolicyKind Kind, Coupling Coupling, Consumption Consumption, DrawOrder Order, Repair Repair)
{
    public static readonly PolicySpec IndLead = new("IND-L", PolicyKind.Ind, Coupling.AttemptCoupled, Consumption.Fixed, DrawOrder.LeadFirst, Repair.Pre);
    public static readonly PolicySpec ALead = new("A-L-coupled-fixed", PolicyKind.A, Coupling.AttemptCoupled, Consumption.Fixed, DrawOrder.LeadFirst, Repair.Pre);
    public static readonly PolicySpec BLead = new("B-L-coupled-fixed", PolicyKind.B, Coupling.AttemptCoupled, Consumption.Fixed, DrawOrder.LeadFirst, Repair.Pre);
    public static readonly PolicySpec ALeadSkip = new("A-L-coupled-skip", PolicyKind.A, Coupling.AttemptCoupled, Consumption.Skip, DrawOrder.LeadFirst, Repair.Pre);
    public static readonly PolicySpec BLeadSkip = new("B-L-coupled-skip", PolicyKind.B, Coupling.AttemptCoupled, Consumption.Skip, DrawOrder.LeadFirst, Repair.Pre);
    public static readonly PolicySpec ALeadX = new("A-L-extra-only-fixed", PolicyKind.A, Coupling.ExtraOnly, Consumption.Fixed, DrawOrder.LeadFirst, Repair.Pre);
    public static readonly PolicySpec BLeadX = new("B-L-extra-only-fixed", PolicyKind.B, Coupling.ExtraOnly, Consumption.Fixed, DrawOrder.LeadFirst, Repair.Pre);
    public static readonly PolicySpec ATrailPre = new("A-T-pre-fixed", PolicyKind.A, Coupling.AttemptCoupled, Consumption.Fixed, DrawOrder.TrailFirst, Repair.Pre);
    public static readonly PolicySpec BTrailPre = new("B-T-pre-fixed", PolicyKind.B, Coupling.AttemptCoupled, Consumption.Fixed, DrawOrder.TrailFirst, Repair.Pre);
    public static readonly PolicySpec BTrailPost = new("B-T-post-fixed", PolicyKind.B, Coupling.AttemptCoupled, Consumption.Fixed, DrawOrder.TrailFirst, Repair.Post);
    public static readonly PolicySpec CLead = new("C-L-redraw-coupled", PolicyKind.C, Coupling.AttemptCoupled, Consumption.Skip, DrawOrder.LeadFirst, Repair.Pre);
    public static readonly PolicySpec CLeadX = new("C-L-redraw-extra-only", PolicyKind.C, Coupling.ExtraOnly, Consumption.Skip, DrawOrder.LeadFirst, Repair.Pre);

    public static readonly IReadOnlyList<PolicySpec> All =
        [IndLead, ALead, BLead, ALeadSkip, BLeadSkip, ALeadX, BLeadX, ATrailPre, BTrailPre, BTrailPost, CLead, CLeadX];
}

/// <summary>
/// One open extra-base decision of a pending in-play pitch: the runner, where the approved base plan left him (<see cref="PlanTo"/>), the base he would attempt
/// (<see cref="TargetTo"/>, 4 = home) and the three approved probabilities (ppm: extra, hold, out).
/// </summary>
internal sealed record AdvDecision(string Key, int RunnerKey, PlayerId Runner, int StartBase, int PlanTo, int TargetTo, FieldPosition Retriever, FieldPosition BaseFielder, int[] Ppm);

/// <summary>The approved <c>advancement</c> baseline rows, read straight from content/tuning/virtual_tuning_v1.json (neutral: no skill modifier is applied anywhere).</summary>
internal static class AdvData
{
    public static readonly string[] Outcomes = ["advance_extra", "hold", "out_on_bases"];

    private static readonly Lazy<Dictionary<string, int[]>> Table = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(FeasibilityData.Root, "content", "tuning", "virtual_tuning_v1.json")));
        var map = new Dictionary<string, int[]>(StringComparer.Ordinal);
        foreach (var row in doc.RootElement.GetProperty("baselines").EnumerateArray().Where(r => r.GetProperty("stage").GetString() == "advancement"))
        {
            var outcomes = row.GetProperty("outcomes").EnumerateArray().Select(o => o.GetString()!).ToArray();
            if (!outcomes.SequenceEqual(Outcomes))
                throw new InvalidOperationException("advancement outcomes changed order");
            map[row.GetProperty("condition").GetString()!] = row.GetProperty("ppm").EnumerateArray().Select(p => p.GetInt32()).ToArray();
        }

        return map;
    });

    public static int[] Ppm(string contextKey) => Table.Value[contextKey];

    public static IReadOnlyCollection<string> Keys => Table.Value.Keys;
}

/// <summary>The play as it stands while decisions are applied one by one: occupancy after the base plan, outs, runs, and whether the half-inning or the game is already over.</summary>
internal sealed class Work
{
    public int[] Occ { get; } = new int[5];     // 1..3: runner key, 0 empty, -1 the batter-runner

    public int Outs { get; set; }

    public int Runs { get; set; }

    public int Limit { get; init; }

    public bool WalkOff { get; init; }

    public int Need { get; init; }

    public bool Ended => Outs >= Limit || (WalkOff && Runs >= Need);

    public Work Clone()
    {
        var c = new Work { Outs = Outs, Runs = Runs, Limit = Limit, WalkOff = WalkOff, Need = Need };
        Array.Copy(Occ, c.Occ, Occ.Length);
        return c;
    }

    public static Work From(GameState s, InPlayPlanFacts f)
    {
        var w = new Work { Outs = s.Outs + (f.Official.Result == InPlayResult.Out ? 1 : 0), Runs = f.Runs, Limit = s.Rules.OutsPerHalfInning, WalkOff = f.WalkOffContext, Need = f.RunsNeeded };
        foreach (var m in f.Moves)
            if (m.To is >= 1 and <= 3)
                w.Occ[m.To] = m.RunnerKey;
        if (f.Official.BatterDestination is >= 1 and <= 3)
            w.Occ[f.Official.BatterDestination] = -1;
        return w;
    }
}

internal static class AdvModel
{
    /// <summary>The open decisions of a pending plan, one per declared context key, in the order the PRODUCTION planner lists the keys (<c>InPlayBasePlanner.ContextKeys</c>: base order, trailing runner first).</summary>
    public static List<AdvDecision> Decisions(InPlayPlanFacts f)
    {
        var list = new List<AdvDecision>();
        foreach (var key in f.AdvancementKeys)
        {
            var start = key switch
            {
                "single_runner_on_first" => 1, "single_runner_on_second" => 2, "double_runner_on_first" => 1, "fly_out_runner_on_third" => 3,
                _ => throw new InvalidOperationException("unknown advancement context " + key),
            };
            var move = f.Moves.Single(x => x.From == start);
            var retriever = f.Official.Retriever ?? f.Official.Credits.First(c => c.Kind == CreditKind.Putout).Position;
            var target = move.To + 1;
            list.Add(new AdvDecision(key, move.RunnerKey, move.Runner, start, move.To, target, retriever, target == 3 ? FieldPosition.ThirdBase : FieldPosition.Catcher, AdvData.Ppm(key)));
        }

        return list;
    }

    public static List<AdvDecision> InOrder(IEnumerable<AdvDecision> ds, DrawOrder order) =>
        (order == DrawOrder.LeadFirst ? ds.OrderByDescending(d => d.StartBase) : ds.OrderBy(d => d.StartBase)).ToList();

    // ------------------------------------------------------------------ feasibility (the working play decides what is possible)

    public static bool[] Feasible(AdvDecision d, Work w, Coupling coupling)
    {
        if (w.Ended)
            return [false, false, false];
        var open = d.TargetTo == 4 || w.Occ[d.TargetTo] == 0;
        return [open, true, coupling == Coupling.AttemptCoupled ? open : true];
    }

    public static void Apply(AdvDecision d, AdvAction action, Work w)
    {
        if (action == AdvAction.Extra)
        {
            w.Occ[d.PlanTo] = 0;
            if (d.TargetTo == 4) w.Runs++; else w.Occ[d.TargetTo] = d.RunnerKey;
        }
        else if (action == AdvAction.Out)
        {
            w.Occ[d.PlanTo] = 0;
            w.Outs++;
        }
    }

    // ------------------------------------------------------------------ the candidate resolvers

    public sealed record Resolved(AdvAction[] Actions, int Calls, long RawDraws);

    /// <summary>
    /// Resolves the decisions of one pending play with the given policy. <paramref name="roll"/> returns the next integer in [0, 1,000,000) and counts as ONE call (the real
    /// draw is one <c>NextBounded(1,000,000)</c> on <c>game/runner@1</c>). The returned actions are aligned to <paramref name="ordered"/>.
    /// </summary>
    public static Resolved Resolve(PolicySpec spec, IReadOnlyList<AdvDecision> ordered, Work start, Func<uint> roll)
    {
        var w = start.Clone();
        var acts = new AdvAction[ordered.Count];
        var calls = 0;
        uint Roll() { calls++; return roll(); }

        if (spec.Repair == Repair.Post)
        {
            // every context is drawn from the approved row, in draw order, with no look at the play; the repair pass below then replays them lead runner first
            var drawn = ordered.Select(d => (AdvAction)PickIndex(d.Ppm, Roll())).ToArray();
            var lead = Enumerable.Range(0, ordered.Count).OrderByDescending(i => ordered[i].StartBase).ToList();
            foreach (var i in lead)
            {
                var d = ordered[i];
                if (w.Ended) { acts[i] = AdvAction.Void; continue; }
                var ok = Feasible(d, w, spec.Coupling);
                acts[i] = ok[(int)drawn[i]] ? drawn[i] : AdvAction.Hold;
                Apply(d, acts[i], w);
            }

            return new Resolved(acts, calls, 0);
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            var d = ordered[i];
            if (spec.Kind == PolicyKind.Ind)
            {
                acts[i] = (AdvAction)PickIndex(d.Ppm, Roll());
                Apply(d, acts[i], w);
                continue;
            }

            var ok = Feasible(d, w, spec.Coupling);
            var feasibleCount = ok.Count(x => x);
            if (feasibleCount == 0)                                                     // the play is over: nothing left to decide
            {
                if (spec.Consumption == Consumption.Fixed && spec.Kind != PolicyKind.C) Roll();
                acts[i] = AdvAction.Void;
                continue;
            }

            if (feasibleCount == 1 && spec.Consumption == Consumption.Skip)             // only Hold is possible: no roll
            {
                acts[i] = AdvAction.Hold;
                Apply(d, acts[i], w);
                continue;
            }

            switch (spec.Kind)
            {
                case PolicyKind.A:
                {
                    var weights = d.Ppm.Select((p, k) => ok[k] ? (long)p : 0L).ToArray();
                    var normalized = ProbabilityScale.Normalize(weights).Value;
                    acts[i] = (AdvAction)PickIndex(normalized, Roll());
                    break;
                }
                case PolicyKind.B:
                {
                    var o = (AdvAction)PickIndex(d.Ppm, Roll());
                    acts[i] = ok[(int)o] ? o : AdvAction.Hold;
                    break;
                }
                default:                                                                   // C: draw again until the outcome is possible (bounded)
                {
                    var o = (AdvAction)PickIndex(d.Ppm, Roll());
                    for (var tries = 0; !ok[(int)o] && tries < 64; tries++)
                        o = (AdvAction)PickIndex(d.Ppm, Roll());
                    acts[i] = ok[(int)o] ? o : AdvAction.Hold;
                    break;
                }
            }

            Apply(d, acts[i], w);
        }

        return new Resolved(acts, calls, 0);
    }

    /// <summary>The index <see cref="Distribution.SelectIndex"/> gives for a roll (the production mapping), built over the three advancement outcomes.</summary>
    public static int PickIndex(IReadOnlyList<int> ppm, uint roll) =>
        Distribution.Create(AdvData.Outcomes, ppm).Value.SelectIndex(roll).Value;

    // ------------------------------------------------------------------ exact joint distribution of final actions (integer arithmetic, denominator 1,000,000^k)

    /// <summary>Every reachable final action vector with its exact probability numerator over 1,000,000^k, keyed by the actions in START-BASE order (stable across draw orders).</summary>
    public static Dictionary<string, long> Exact(PolicySpec spec, IReadOnlyList<AdvDecision> decisions, Work start)
    {
        var ordered = InOrder(decisions, spec.Order);
        var acc = new Dictionary<string, long>(StringComparer.Ordinal);
        if (spec.Repair == Repair.Post)
        {
            var lead = ordered.Select((d, i) => (d, i)).OrderByDescending(x => x.d.StartBase).Select(x => x.i).ToList();
            ExactPost(spec, ordered, lead, start, 0, new AdvAction[ordered.Count], 1L, acc);
        }
        else
            ExactPre(spec, ordered, 0, start.Clone(), new AdvAction[ordered.Count], 1L, acc);
        return acc;
    }

    private static string Key(IReadOnlyList<AdvDecision> ordered, AdvAction[] acts) =>
        string.Join("/", ordered.Select((d, i) => (d.StartBase, a: acts[i])).OrderBy(x => x.StartBase).Select(x => x.a switch { AdvAction.Extra => "E", AdvAction.Hold => "H", AdvAction.Out => "O", _ => "V" }));

    private static void ExactPre(PolicySpec spec, IReadOnlyList<AdvDecision> ordered, int i, Work w, AdvAction[] acts, long p, Dictionary<string, long> acc)
    {
        if (i == ordered.Count)
        {
            acc[Key(ordered, acts)] = acc.GetValueOrDefault(Key(ordered, acts)) + p;
            return;
        }

        var d = ordered[i];
        if (spec.Kind == PolicyKind.Ind)
        {
            for (var o = 0; o < 3; o++)
                if (d.Ppm[o] > 0)
                    Branch(w, acts, i, (AdvAction)o, p * d.Ppm[o]);
            return;
        }

        var ok = Feasible(d, w, spec.Coupling);
        if (!ok.Any(x => x))                                                           // play over: the roll is consumed (Fixed) or not (Skip), the result is Void either way
        {
            Branch(w, acts, i, AdvAction.Void, p * ProbabilityScale.Denominator);
            return;
        }

        int[] q;
        if (spec.Kind == PolicyKind.A)
            q = ProbabilityScale.Normalize(d.Ppm.Select((x, k) => ok[k] ? (long)x : 0L).ToArray()).Value;
        else if (spec.Kind == PolicyKind.C)
        {
            // rejection redraw is exactly the conditional distribution (no rounding); expressed over 1,000,000 by the same Normalize rule for comparability
            q = ProbabilityScale.Normalize(d.Ppm.Select((x, k) => ok[k] ? (long)x : 0L).ToArray()).Value;
        }
        else
            q = d.Ppm;

        for (var o = 0; o < 3; o++)
        {
            if (q[o] == 0)
                continue;
            var action = ok[o] ? (AdvAction)o : AdvAction.Hold;                         // only reachable for B: an impossible draw becomes Hold
            Branch(w, acts, i, action, p * q[o]);
        }

        void Branch(Work current, AdvAction[] a, int index, AdvAction action, long weight)
        {
            var next = current.Clone();
            var copy = (AdvAction[])a.Clone();
            copy[index] = action;
            if (action != AdvAction.Void)
                Apply(ordered[index], action, next);
            ExactPre(spec, ordered, index + 1, next, copy, weight, acc);
        }
    }

    private static void ExactPost(PolicySpec spec, IReadOnlyList<AdvDecision> ordered, List<int> lead, Work start, int i, AdvAction[] drawn, long p, Dictionary<string, long> acc)
    {
        if (i == ordered.Count)
        {
            var final = RepairLead(spec, ordered, lead, start, drawn);
            acc[final] = acc.GetValueOrDefault(final) + p;
            return;
        }

        for (var o = 0; o < 3; o++)
            if (ordered[i].Ppm[o] > 0)
            {
                var copy = (AdvAction[])drawn.Clone();
                copy[i] = (AdvAction)o;
                ExactPost(spec, ordered, lead, start, i + 1, copy, p * ordered[i].Ppm[o], acc);
            }
    }

    /// <summary>Replays independently drawn actions lead runner first against the working play and repairs the impossible ones (the same rule as the Post branch of <see cref="Resolve"/>).</summary>
    private static string RepairLead(PolicySpec spec, IReadOnlyList<AdvDecision> ordered, List<int> lead, Work start, AdvAction[] drawn)
    {
        var w = start.Clone();
        var final = new AdvAction[ordered.Count];
        foreach (var i in lead)
        {
            if (w.Ended) { final[i] = AdvAction.Void; continue; }
            var ok = Feasible(ordered[i], w, spec.Coupling);
            final[i] = ok[(int)drawn[i]] ? drawn[i] : AdvAction.Hold;
            Apply(ordered[i], final[i], w);
        }

        return Key(ordered, final);
    }

    // ------------------------------------------------------------------ the event for a vector

    public enum SeqMode { Lead, Trail, Implicit }

    /// <summary>Builds the reducer event for the base plan with the given actions per decision (keyed by start base; Void acts as Hold). Sequence numbers are explicit for every out and run unless <paramref name="mode"/> is Implicit.</summary>
    public static InPlayEvent BuildEvent(InPlayPlanFacts f, IReadOnlyList<AdvDecision> decisions, IReadOnlyDictionary<int, AdvAction> actions, SeqMode mode, string id = "p1")
    {
        var baseEvent = InPlayBasePlanner.BuildEvent(f, id, FeasibilityHarness.Location);
        var moves = new List<RunnerMove>();
        foreach (var m in baseEvent.MoveList)
        {
            var d = decisions.FirstOrDefault(x => x.StartBase == m.From);
            var a = d is null ? AdvAction.Hold : actions.GetValueOrDefault(d.StartBase, AdvAction.Hold);
            moves.Add(a switch
            {
                AdvAction.Extra => m with { To = d!.TargetTo, Cause = MoveCause.Play },
                AdvAction.Out => m with
                {
                    To = 0, Cause = MoveCause.Play, OutKind = OutKind.Tag, OutAt = d!.TargetTo, Method = OutMethod.Tag,
                    Credits = ImmutableList.Create(new PositionCredit(d.Retriever, CreditKind.Assist), new PositionCredit(d.BaseFielder, CreditKind.Putout)),
                },
                _ => m,
            });
        }

        if (mode != SeqMode.Implicit)
        {
            var terminals = moves.Where(m => m.To is 0 or 4).ToList();
            var ordered = mode == SeqMode.Lead ? terminals.OrderByDescending(m => m.From).ToList() : terminals.OrderBy(m => m.From).ToList();
            for (var i = 0; i < ordered.Count; i++)
                moves[moves.IndexOf(ordered[i])] = ordered[i] with { Sequence = i + 1 };
        }

        return baseEvent with { Moves = moves.ToImmutableList() };
    }
}
