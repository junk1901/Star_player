using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Tests.InPlayFeasibility;

namespace StarPlayer.Simulation.Tests.InPlay;

/// <summary>
/// Phase 3-C2B-2: runs the PRODUCTION base planner (not the test-only feasibility planner) over the same real states and compares every field with the independent
/// Python oracle and with the recorded feasibility results. The planner under test takes (state, drawn batted ball, drawn defence result) only; the event it builds is
/// judged by the real <see cref="GameReducer.Apply"/>.
/// </summary>
internal sealed record ProdRow(
    string Key, InPlayPlanFacts? Facts, string Status, bool ReducerOk, string ErrorCode, int Runs, int OutsAfter, bool HalfOver, bool GameOver, int BasesAfter, int Lob, int Rbi,
    string Credits, string Errors, string Receipt, string Moves, bool Pending, bool Composite, List<string> Mismatches);

internal static class ProdHarness
{
    public static string MovesText(InPlayPlanFacts f) => string.Join(",", f.Moves.Select(m => $"{m.From}>{m.To}"));

    public static string StatusOf(InPlayPlanFacts f) =>
        f.WalkOffEffect == InPlayWalkOffEffect.CreditReduced ? "NEEDS_WALKOFF_POLICY" : f.AdvancementRequired ? "PENDING_ADVANCEMENT" : "LEGAL_BASE";

    public static string WalkOffText(InPlayWalkOffEffect e) => e switch { InPlayWalkOffEffect.None => "none", InPlayWalkOffEffect.GameEnds => "game_ends", _ => "credit_reduced" };

    public static string CreditText(IEnumerable<PositionCredit> credits) => string.Join(",", credits.Select(c => $"{InPlayTemplates.Short(c.Position)}:{(c.Kind == CreditKind.Putout ? "PO" : "A")}"));

    public static ProdRow RunOne(bool walkOffKeys, int diff, int occupancy, int outs, Combo combo, GameState state)
    {
        var key = $"{(walkOffKeys ? "w" : "g")}|{(walkOffKeys ? diff : 0)}|{occupancy}|{outs}|{combo.Key}";
        var expected = FeasibilityData.Case(key);
        var oracle = FeasibilityData.Oracle;
        var mismatches = new List<string>();
        void Check(string name, object? actual, object? want)
        {
            if (!Equals(actual, want))
                mismatches.Add($"{name}: actual {actual} expected {want}");
        }

        var before = FeasibilityHarness.Digest(state);
        var planned = InPlayBasePlanner.Plan(state, combo.Batted, combo.Result);
        if (!planned.IsSuccess)
            return new ProdRow(key, null, "DEFECT_OR_UNRESOLVED", false, planned.Error!.Value.Code, 0, 0, false, false, 0, 0, 0, "", "", "", "", false, false, [$"the planner refused: {planned.Error!.Value.Code}"]);
        var facts = planned.Value;
        var evt = InPlayBasePlanner.BuildEvent(facts, "p1", FeasibilityHarness.Location);
        var applied = GameReducer.Apply(state, evt);
        var verified = InPlayPitchPlanning.Verify(facts, state, evt, applied);
        if (verified is { } problem)
            mismatches.Add($"post-reducer verification failed: {problem.Code}");
        if (FeasibilityHarness.Digest(state) != before)
            mismatches.Add("the state the plan was made on changed");

        // ---- the plan itself against the oracle
        var cls = facts.BattedBallClass;
        var officialShort = FeasibilityHarness.Short(expected.GetProperty("official").GetString()!);
        var oracleTpl = oracle.GetProperty("templates").GetProperty($"ipt1.{cls}.{officialShort}");
        Check("drawn result", facts.Drawn.Result.ToString(), expected.GetProperty("drawn").GetString());
        Check("official result", facts.Official.Result.ToString(), expected.GetProperty("official").GetString());
        Check("drawn template", facts.Drawn.Id, expected.GetProperty("template").GetString());
        Check("official template", facts.Official.Id, $"ipt1.{cls}.{officialShort}");
        var oracleMoves = string.Join(",", expected.GetProperty("moves").EnumerateObject().Select(p => (From: int.Parse(p.Name), To: p.Value.GetInt32())).OrderByDescending(m => m.From).Select(m => $"{m.From}>{m.To}"));
        Check("runner moves", MovesText(facts), oracleMoves);
        Check("batter destination", facts.Official.BatterDestination, oracleTpl.GetProperty("batter_dest").GetInt32());
        Check("walk-off effect", WalkOffText(facts.WalkOffEffect), expected.GetProperty("walkoff").GetString());
        Check("context present", facts.AdvancementKeys.Length > 0, expected.GetProperty("context_present").GetBoolean());
        Check("advancement required", facts.AdvancementRequired, expected.GetProperty("pending").GetBoolean());
        Check("status", StatusOf(facts), expected.GetProperty("status").GetString());
        Check("planned runs", facts.Runs, expected.GetProperty("runs").GetInt32());
        Check("planned credits", CreditText(facts.Official.Credits), string.Join(",", oracleTpl.GetProperty("credits").EnumerateArray().Select(c => c.GetString())));
        var oracleError = oracleTpl.GetProperty("error").ValueKind == JsonValueKind.Null ? "" : oracleTpl.GetProperty("error").GetString();
        Check("planned error", facts.Official.Error is { } e ? InPlayTemplates.Short(e) : "", oracleError);
        Check("every runner has one move", facts.Moves.Length, state.Bases.Count);
        Check("every move names its runner", facts.Moves.Select(m => m.RunnerKey).Distinct().Count(), state.Bases.Count);
        if (evt.MoveList.Any(m => m.Sequence is not null || m.OutKind is not null || m.Credits is not null) || evt.BatterSequence is not null)
            mismatches.Add("a base plan must carry no sequence, out kind or move credit");

        string errorCode = "", credits = "", errors = "", receipt = "";
        int runs = 0, outsAfter = 0, basesAfter = 0, lob = 0, rbi = 0;
        bool halfOver = false, gameOver = false;
        if (!applied.IsSuccess)
        {
            errorCode = applied.Error!.Value.Code;
            mismatches.Add($"the reducer refused the event: {errorCode}");
        }
        else
        {
            var step = applied.Value;
            var play = step.NewPlays.Single();
            runs = play.Runs.Count;
            outsAfter = play.OutsAfter;
            halfOver = outsAfter >= state.Rules.OutsPerHalfInning;
            gameOver = step.State.Status == GameStatus.Final;
            basesAfter = halfOver ? 0 : FeasibilityHarness.Mask(step.State.Bases);
            lob = step.Delta.Entries.Where(x => x.Key == StatKey.TeamLOB).Sum(x => x.Amount);
            rbi = step.Delta.Entries.Where(x => x.Key == StatKey.BatRBI).Sum(x => x.Amount);
            credits = CreditText(play.Credits.Select(c => new PositionCredit(c.Position, c.Kind)));
            errors = string.Join(",", play.Errors.Select(InPlayTemplates.Short));
            receipt = step.State.AppliedEvents.Last().FingerprintVersion;

            Check("reducer official result", play.Result!.Value.ToString(), expected.GetProperty("official").GetString());
            Check("reducer runs", runs, expected.GetProperty("runs").GetInt32());
            Check("reducer outs after", outsAfter, expected.GetProperty("outs_after").GetInt32());
            Check("reducer half over", halfOver, expected.GetProperty("half_over").GetBoolean());
            Check("reducer game over", gameOver, expected.GetProperty("game_over").GetBoolean());
            if (!halfOver)
                Check("reducer runners after", basesAfter, expected.GetProperty("bases_after").GetInt32());
            Check("reducer left on base", lob, expected.GetProperty("lob").GetInt32());
            Check("reducer rbi", rbi, expected.GetProperty("rbi").GetInt32());
            Check("reducer hits", step.Delta.Entries.Where(x => x.Key == StatKey.BatH).Sum(x => x.Amount), expected.GetProperty("hit").GetInt32());
            Check("reducer at bats", step.Delta.Entries.Where(x => x.Key == StatKey.BatAB).Sum(x => x.Amount), expected.GetProperty("ab").GetInt32());
            Check("reducer credits", credits, CreditText(facts.Official.Credits));
            Check("reducer errors", errors, oracleError);
            Check("fingerprint version", receipt, "ef2");
            Check("one pitch counted", step.Delta.Entries.Count(x => x.Key == StatKey.PitchPitches), 1);
            Check("move count", play.Moves.Count, state.Bases.Count + 1);

            // determinism: the same situation gives the same plan, event fingerprint and resulting state
            var again = InPlayBasePlanner.Plan(state, combo.Batted, combo.Result).Value;
            var evt2 = InPlayBasePlanner.BuildEvent(again, "p1", FeasibilityHarness.Location);
            var applied2 = GameReducer.Apply(state, evt2);
            if (!applied2.IsSuccess || EventFingerprint.Receipt(evt2) != EventFingerprint.Receipt(evt) || FeasibilityHarness.Digest(applied2.Value.State) != FeasibilityHarness.Digest(step.State)
                || MovesText(again) != MovesText(facts))
                mismatches.Add("the same situation did not give the same result twice");
        }

        var status = mismatches.Count == 0 ? StatusOf(facts) : "DEFECT_OR_UNRESOLVED";
        return new ProdRow(key, facts, status, applied.IsSuccess, errorCode, runs, outsAfter, halfOver, gameOver, basesAfter, lob, rbi, credits, errors, receipt, MovesText(facts),
            facts.AdvancementRequired, facts.Composite, mismatches);
    }

    public static List<ProdRow> RunAll(InPlayStates.Root root, bool walkOffKeys, IEnumerable<int> diffs, bool fullCount = false)
    {
        var rows = new List<ProdRow>();
        foreach (var diff in diffs)
            for (var occ = 0; occ < 8; occ++)
                for (var outs = 0; outs < 3; outs++)
                {
                    var state = fullCount ? InPlayStates.WithFullCount(root, diff, occ, outs) : InPlayStates.Make(root, diff, occ, outs);
                    foreach (var combo in FeasibilityData.Positive)
                        rows.Add(RunOne(walkOffKeys, diff, occ, outs, combo, state));
                }

        return rows;
    }

    public static Dictionary<string, int> Counts(IEnumerable<ProdRow> rows) =>
        rows.GroupBy(r => r.Status).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());
}
