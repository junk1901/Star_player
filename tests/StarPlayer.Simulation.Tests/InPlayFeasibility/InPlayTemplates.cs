using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Tests.InPlayFeasibility;

// Phase 3-C2B-2-P (policy lock and template feasibility). TEST-ONLY code: nothing here is production code and nothing in src/ was changed.
// It is a candidate reading of the owner-approved prototype policy (ADR-007 "Decisions (3-C2B-2 prototype policy)", docs/game-engine.md section 23),
// used to prove that every positive-probability combination can be expressed as a legal GameReducer event. The production generator of 3-C2B-2 is NOT
// derived from this file; it is checked against the independent Python oracle tests/Fixtures/generator/generate_inplay_feasibility_oracle.py instead.

/// <summary>One virtual play template: the physical story, the official-record facts and the reducer event shape. IDs are stable; a change is a new catalogue version.</summary>
internal sealed record InPlayTemplate(
    string Id, string Class, string Result, BattedBallType Batted, InPlayResult Reducer, int BatterDest, PositionCredit[] Credits, FieldPosition? Error,
    FieldPosition? Retriever, string Action, string Premise)
{
    /// <summary>The runner policy id of the template's result (see docs/game-engine.md section 23).</summary>
    public string RunnerPolicy => Result switch
    {
        "Out" when Class == "ground" => "RP-OUT-GROUND-FORCED",
        "Out" => "RP-OUT-HOLD",
        "Single" => "RP-HIT-PLUS-1",
        "Double" => "RP-HIT-PLUS-2",
        "Triple" => "RP-HIT-PLUS-3",
        "HomeRun" => "RP-HR-ALL-SCORE",
        _ => "RP-ROE-FORCED-PUSH",
    };
}

internal static class InPlayTemplates
{
    public const string CatalogVersion = "inplay-template-v1";

    private static PositionCredit Po(FieldPosition p) => new(p, CreditKind.Putout);
    private static PositionCredit A(FieldPosition p) => new(p, CreditKind.Assist);

    /// <summary>The 19 templates: every (class, result) pair with at least one positive-probability combination in tuning-virtual-proto-v1 (45 combinations).</summary>
    public static readonly IReadOnlyList<InPlayTemplate> All =
    [
        T("ground", "Out", BattedBallType.Ground, InPlayResult.Out, 0, [A(FieldPosition.Shortstop), Po(FieldPosition.FirstBase)], null, null,
            "SS fields the grounder and throws to 1B; 1B makes the only putout.", "No other play is attempted (no double play, no play at another base)."),
        T("ground", "Single", BattedBallType.Ground, InPlayResult.Single, 1, [], null, FieldPosition.LeftField,
            "Grounder through the infield into left field; no out is made, no throw is credited.", "Safe hit: no putout, no error."),
        T("ground", "Double", BattedBallType.Ground, InPlayResult.Double, 2, [], null, FieldPosition.LeftField,
            "Grounder down the line into the left-field corner; the batter-runner stops on second.", "Safe hit: no putout, no error; the batter-runner's final base is second."),
        T("ground", "ReachedOnError", BattedBallType.Ground, InPlayResult.ReachedOnError, 1, [], FieldPosition.Shortstop, null,
            "SS misplays the grounder (E6); no out is made.", "One error by one fielder; never reclassified as a fielder's choice or a compound error."),
        T("line", "Out", BattedBallType.Line, InPlayResult.Out, 0, [Po(FieldPosition.Shortstop)], null, null,
            "SS catches the line drive.", "Caught in the air: the only putout is the batter's; nobody is doubled off."),
        T("line", "Single", BattedBallType.Line, InPlayResult.Single, 1, [], null, FieldPosition.CenterField,
            "Line drive falls in front of the center fielder.", "Safe hit: no putout, no error."),
        T("line", "Double", BattedBallType.Line, InPlayResult.Double, 2, [], null, FieldPosition.RightField,
            "Line drive into the right-center gap; the batter-runner stops on second.", "Safe hit: no putout, no error."),
        T("line", "Triple", BattedBallType.Line, InPlayResult.Triple, 3, [], null, FieldPosition.RightField,
            "Line drive into the right-field corner; the batter-runner stops on third.", "Safe hit: no putout, no error."),
        T("line", "HomeRun", BattedBallType.Line, InPlayResult.HomeRun, 4, [], null, null,
            "Line drive over the outfield fence (out of the playing field).", "Out-of-the-park home run (owner D2); inside-the-park home runs are not generated."),
        T("line", "ReachedOnError", BattedBallType.Line, InPlayResult.ReachedOnError, 1, [], FieldPosition.Shortstop, null,
            "SS misplays the line drive (E6); no out is made.", "One error by one fielder."),
        T("fly", "Out", BattedBallType.Fly, InPlayResult.Out, 0, [Po(FieldPosition.CenterField)], null, null,
            "CF catches the fly ball.", "Deep fly ball in the center fielder's range, beyond the infielders' ordinary effort: the Infield Fly rule does not apply."),
        T("fly", "Single", BattedBallType.Fly, InPlayResult.Single, 1, [], null, FieldPosition.CenterField,
            "Fly ball drops in front of the center fielder.", "Safe hit: no putout, no error; beyond the infielders' ordinary effort."),
        T("fly", "Double", BattedBallType.Fly, InPlayResult.Double, 2, [], null, FieldPosition.CenterField,
            "Fly ball drops in the gap; the batter-runner stops on second.", "Safe hit: no putout, no error."),
        T("fly", "Triple", BattedBallType.Fly, InPlayResult.Triple, 3, [], null, FieldPosition.RightField,
            "Fly ball over the right fielder; the batter-runner stops on third.", "Safe hit: no putout, no error."),
        T("fly", "HomeRun", BattedBallType.Fly, InPlayResult.HomeRun, 4, [], null, null,
            "Fly ball over the outfield fence (out of the playing field).", "Out-of-the-park home run (owner D2)."),
        T("fly", "ReachedOnError", BattedBallType.Fly, InPlayResult.ReachedOnError, 1, [], FieldPosition.CenterField, null,
            "CF drops the fly ball (E8); no out is made.", "A ball the center fielder could ordinarily catch, beyond the infielders' ordinary effort."),
        T("pop", "Out", BattedBallType.Pop, InPlayResult.Out, 0, [Po(FieldPosition.SecondBase)], null, null,
            "2B catches the pop-up.", "A pop-up an infielder catches with ordinary effort: with runners on first and second (or loaded) and fewer than two outs this is an Infield Fly; the caught ball gives the same record."),
        T("pop", "Single", BattedBallType.Pop, InPlayResult.Single, 1, [], null, FieldPosition.CenterField,
            "Pop-up drops in shallow center field, beyond any infielder's ordinary effort; CF retrieves it.", "Not catchable by an infielder with ordinary effort: the Infield Fly rule does NOT apply (by premise), whatever the runners."),
        T("pop", "ReachedOnError", BattedBallType.Pop, InPlayResult.ReachedOnError, 1, [], FieldPosition.CenterField, null,
            "Pop-up in shallow center field, beyond any infielder's ordinary effort; CF could ordinarily catch it and drops it (E8).", "Not catchable by an infielder with ordinary effort (no Infield Fly, by premise); catchable by the outfielder in his own range."),
    ];

    private static InPlayTemplate T(string cls, string result, BattedBallType batted, InPlayResult reducer, int dest, PositionCredit[] credits, FieldPosition? error, FieldPosition? retriever,
        string action, string premise) => new($"ipt1.{cls}.{(result == "ReachedOnError" ? "roe" : result == "HomeRun" ? "homerun" : result.ToLowerInvariant())}", cls, result, batted, reducer, dest, credits, error, retriever, action, premise);

    public static string ClassOf(string battedBall) => battedBall == "popup" ? "pop" : battedBall.Split('_')[0];

    /// <summary>The tuning JSON's <c>defense_result</c> text (<c>home_run</c>, <c>reached_on_error</c>) to the template/result text.</summary>
    public static string ResultText(string defenseResult) => defenseResult switch
    {
        "out" => "Out", "single" => "Single", "double" => "Double", "triple" => "Triple", "home_run" => "HomeRun", "reached_on_error" => "ReachedOnError",
        _ => throw new ArgumentOutOfRangeException(nameof(defenseResult), defenseResult, null),
    };

    public static InPlayTemplate? Find(string cls, string resultText) => All.FirstOrDefault(t => t.Class == cls && t.Result == resultText);

    public static InPlayTemplate ById(string id) => All.Single(t => t.Id == id);

    public static string CreditText(PositionCredit[] credits) => string.Join(",", credits.Select(c => $"{Short(c.Position)}:{(c.Kind == CreditKind.Putout ? "PO" : "A")}"));

    public static string Short(FieldPosition p) => p switch
    {
        FieldPosition.Pitcher => "P", FieldPosition.Catcher => "C", FieldPosition.FirstBase => "1B", FieldPosition.SecondBase => "2B", FieldPosition.ThirdBase => "3B",
        FieldPosition.Shortstop => "SS", FieldPosition.LeftField => "LF", FieldPosition.CenterField => "CF", FieldPosition.RightField => "RF", _ => "DH",
    };
}

/// <summary>Deliberate defects injected into the test-only planner to prove that the harness (reducer + independent oracle + template audit) notices each one.</summary>
internal enum Fault
{
    None,
    DrawnLongHitAlwaysSingle,
    GroundNoForcedAdvance,
    GroundMissingPutout,
    GroundDoublePutout,
    LineOutWithAssist,
    RoeWithoutError,
    NoWalkOffCredit,
    PopRoeByInfielder,
    RoeForcedRunnerHolds,
    TripleAsDouble,
    FlyOutRunnerScoresFromThird,
    PendingTreatedAsLegal,
}

/// <summary>What the test-only planner produced for one case, with the evidence a real audit record would need (D5).</summary>
internal sealed record PlannedPlay(
    InPlayTemplate Drawn, InPlayTemplate Official, InPlayEvent Event, ImmutableList<(int From, int To, MoveCause Cause)> Moves, bool WalkOffContext, int NeedRuns,
    string WalkOff, int? WinningRunnerBase, bool ContextPresent, bool Pending, string Status);

internal static class InPlayPlanner
{
    public const string MappingVersion = "inplay-mapping-v1";
    public const string WalkOffVersion = "walkoff-credit-v1";

    public static string Name(InPlayResult r) => r switch { InPlayResult.ReachedOnError => "ReachedOnError", _ => r.ToString() };

    private static int BasesOf(string result) => result switch { "Single" => 1, "Double" => 2, "Triple" => 3, _ => 0 };

    private static bool Forced(int @base, RunnerState bases) => Enumerable.Range(1, @base - 1).All(b => bases.At(b) is not null);

    /// <summary>
    /// Plans one virtual play for the drawn (batted ball, defence result) on the given state. The drawn result is never re-drawn or silently changed: only in the last
    /// half of the final or an extra inning, and only for a drawn double or triple, the official result may be reduced to the bases the winning-run scorer advanced
    /// (walkoff-credit-v1, OBR 9.06(f)); the facts of the play are then those of the reduced template (the batter really stops where the official record says).
    /// </summary>
    public static PlannedPlay Plan(GameState s, string battedBall, string defenseResult, string eventId, PitchLocation? location, Fault fault = Fault.None)
    {
        var cls = InPlayTemplates.ClassOf(battedBall);
        var drawn = InPlayTemplates.Find(cls, InPlayTemplates.ResultText(defenseResult)) ?? throw new InvalidOperationException($"no template for {battedBall}/{defenseResult}");
        var bases = s.Bases;
        var walkOffContext = s.BattingSide == TeamSide.Home && s.Inning >= s.Rules.RegulationInnings;
        var need = s.AwayRuns - s.HomeRuns + 1;

        var official = drawn;
        string walkOff = "none";
        int? winner = null;
        if (fault == Fault.TripleAsDouble && drawn.Result == "Triple")
            official = InPlayTemplates.Find(cls, "Double")!;
        if (fault == Fault.DrawnLongHitAlwaysSingle && drawn.Result is "Double" or "Triple")
            official = InPlayTemplates.Find(cls, "Single")!;
        else if (ReferenceEquals(official, drawn) && walkOffContext && fault != Fault.NoWalkOffCredit && drawn.Result is "Double" or "Triple")
        {
            var k = BasesOf(drawn.Result);
            var scorers = bases.Occupied().Select(o => o.Base).Where(b => b + k >= 4).OrderByDescending(b => b).ToList();
            if (scorers.Count >= need)
            {
                winner = scorers[need - 1];
                var credited = Math.Min(k, 4 - winner.Value);
                if (credited != k)
                {
                    official = InPlayTemplates.Find(cls, credited == 1 ? "Single" : "Double")!;
                    walkOff = "credit_reduced";
                }
            }
        }

        // ---- runner moves of the base template
        var moves = ImmutableList.CreateBuilder<(int From, int To, MoveCause Cause)>();
        foreach (var (b, _) in bases.Occupied().OrderByDescending(o => o.Base))
        {
            var forced = Forced(b, bases);
            (int to, MoveCause cause) = official.Result switch
            {
                "Out" when official.Class == "ground" && s.Outs < s.Rules.OutsPerHalfInning - 1 && fault != Fault.GroundNoForcedAdvance => (forced ? b + 1 : b, MoveCause.Play),
                "Out" => (b, MoveCause.Play),
                "Single" or "Double" or "Triple" => (Math.Min(4, b + BasesOf(official.Result)), MoveCause.Play),
                "HomeRun" => (4, MoveCause.Play),
                _ => forced && fault != Fault.RoeForcedRunnerHolds ? (b + 1, MoveCause.Forced) : (b, MoveCause.Play),
            };
            if (fault == Fault.FlyOutRunnerScoresFromThird && official.Class == "fly" && official.Result == "Out" && b == 3 && s.Outs < s.Rules.OutsPerHalfInning - 1)
                to = 4;
            moves.Add((b, to, cause));
        }

        var runs = moves.Count(m => m.To == 4) + (official.Result == "HomeRun" ? 1 : 0);
        var ends = walkOffContext && runs >= need;
        if (ends && walkOff == "none")
            walkOff = "game_ends";
        var halfOver = official.Result == "Out" && s.Outs + 1 >= s.Rules.OutsPerHalfInning;

        // ---- the declared 'advancement' contexts of prob-model-v1 (single with a runner on first/second, double with a runner on first, fly out with a runner on third before two outs)
        var occ = bases.Occupied().Select(o => o.Base).ToHashSet();
        var context = official.Result == "Single" && (occ.Contains(1) || occ.Contains(2))
            || official.Result == "Double" && occ.Contains(1)
            || official.Result == "Out" && official.Class == "fly" && s.Outs < s.Rules.OutsPerHalfInning - 1 && occ.Contains(3);
        var pending = context && !ends && !halfOver && fault != Fault.PendingTreatedAsLegal;
        var status = walkOff == "credit_reduced" ? "NEEDS_WALKOFF_POLICY" : pending ? "PENDING_ADVANCEMENT" : "LEGAL_BASE";

        // ---- the reducer event
        var credits = official.Credits;
        var errors = official.Error is { } err ? new[] { err } : Array.Empty<FieldPosition>();
        if (fault == Fault.GroundMissingPutout && official is { Class: "ground", Result: "Out" })
            credits = [new PositionCredit(FieldPosition.Shortstop, CreditKind.Assist)];
        if (fault == Fault.GroundDoublePutout && official is { Class: "ground", Result: "Out" })
            credits = [new PositionCredit(FieldPosition.Shortstop, CreditKind.Assist), new PositionCredit(FieldPosition.FirstBase, CreditKind.Putout), new PositionCredit(FieldPosition.SecondBase, CreditKind.Putout)];
        if (fault == Fault.LineOutWithAssist && official is { Class: "line", Result: "Out" })
            credits = [new PositionCredit(FieldPosition.ThirdBase, CreditKind.Assist), new PositionCredit(FieldPosition.Shortstop, CreditKind.Putout)];
        if (fault == Fault.RoeWithoutError && official.Result == "ReachedOnError")
            errors = [];
        if (fault == Fault.PopRoeByInfielder && official is { Class: "pop", Result: "ReachedOnError" })
            errors = [FieldPosition.SecondBase];

        var runnerMoves = moves.Select(m => new RunnerMove(m.From, m.To, m.Cause)).ToImmutableList();
        var e = new InPlayEvent(eventId, official.Batted, official.Reducer, false, runnerMoves, credits.Length == 0 ? null : credits.ToImmutableList(),
            errors.Length == 0 ? null : errors.ToImmutableList(), null, location);
        return new PlannedPlay(drawn, official, e, moves.ToImmutable(), walkOffContext, need, walkOff, winner, context, pending, status);
    }
}
