using System.Text;
using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.Simulation.Tests.InPlayFeasibility;

/// <summary>One (batted ball, defence result) pair of the virtual tuning, with its current probability.</summary>
internal sealed record Combo(string Batted, string Result, int Ppm)
{
    public bool Positive => Ppm > 0;
    public string Key => $"{Batted}/{Result}";
}

/// <summary>The inputs of the feasibility run: the 60 combinations read straight from the tuning JSON, and the independent Python oracle's expected values.</summary>
internal static class FeasibilityData
{
    public static readonly string Root = FindRoot();

    public static readonly string[] DefenseOrder = ["out", "single", "double", "triple", "home_run", "reached_on_error"];

    public static readonly IReadOnlyList<Combo> AllCombos = ReadCombos();

    public static IReadOnlyList<Combo> Positive => AllCombos.Where(c => c.Positive).ToList();

    private static readonly Lazy<JsonDocument> OracleDoc = new(() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tests", "Fixtures", "phase3c2b2p_inplay_oracle.json"))));

    public static JsonElement Oracle => OracleDoc.Value.RootElement;

    private static readonly Lazy<Dictionary<string, JsonElement>> CaseIndex = new(() =>
        Oracle.GetProperty("cases").EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal));

    /// <summary>The oracle's expected values for one case key (<c>g|0|occ|outs|batted/result</c> or <c>w|diff|occ|outs|batted/result</c>).</summary>
    public static JsonElement Case(string key) => CaseIndex.Value[key];

    private static IReadOnlyList<Combo> ReadCombos()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "content", "tuning", "virtual_tuning_v1.json")));
        var list = new List<Combo>();
        foreach (var row in doc.RootElement.GetProperty("baselines").EnumerateArray().Where(r => r.GetProperty("stage").GetString() == "defense_result"))
        {
            var batted = row.GetProperty("condition").GetString()!;
            var outcomes = row.GetProperty("outcomes").EnumerateArray().Select(o => o.GetString()!).ToList();
            var ppm = row.GetProperty("ppm").EnumerateArray().Select(p => p.GetInt32()).ToList();
            foreach (var result in DefenseOrder)
                list.Add(new Combo(batted, result, ppm[outcomes.IndexOf(result)]));
        }

        return list;
    }

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                return d.FullName;
        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }
}

/// <summary>The recorded facts of one executed case: what the planner chose, what the real reducer did, and whether the independent oracle agrees.</summary>
internal sealed record CaseRow(
    string Key, string Context, int Diff, int Occupancy, int Outs, string Batted, string DefenseResult, int Ppm, string DrawnTemplate, string OfficialTemplate,
    string Drawn, string Official, string Moves, int? WinnerBase, bool ReducerOk, string ErrorCode, int Runs, int OutsAfter, bool HalfOver, bool GameOver, int BasesAfter,
    int Lob, int Rbi, string Credits, string Errors, string Status, string WalkOff, bool ContextPresent, bool Pending, bool OracleMatch, string Mismatch, string Receipt);

internal static class FeasibilityHarness
{
    public static readonly PitchLocation Location = PitchLocation.FromZone(ZoneModelVersion.Pzl1, PitchZoneId.Create("in.r2c2").Value, AppliedBatterSide.Right).Value;

    public static string Short(string result) => result switch { "ReachedOnError" => "roe", "HomeRun" => "homerun", _ => result.ToLowerInvariant() };

    public static int Mask(RunnerState b) => (b.First is null ? 0 : 1) | (b.Second is null ? 0 : 2) | (b.Third is null ? 0 : 4);

    /// <summary>A structural digest of the observable game state, to prove that applying an event to a state leaves the state it was applied to untouched.</summary>
    public static string Digest(GameState s) =>
        $"{s.Inning}|{s.Half}|{s.Outs}|{s.Count}|{s.Stage}|{s.AwayRuns}-{s.HomeRuns}|{Mask(s.Bases)}|{s.Bases.First?.RunnerKey}|{s.Bases.Second?.RunnerKey}|{s.Bases.Third?.RunnerKey}|" +
        $"{s.Plays.Count}|{s.Stats.Count}|{s.AppliedEvents.Count}|{s.Status}|{s.NextRunnerKey}|{string.Join(",", s.AwayLine)}|{string.Join(",", s.HomeLine)}";

    private static string Name(BatterResult r) => r.ToString();

    /// <summary>Runs one combination on one prepared state through the planner and the real reducer and compares everything with the oracle.</summary>
    public static CaseRow RunOne(string context, bool walkOffKeys, int diff, int occupancy, int outs, Combo combo, GameState state, Fault fault)
    {
        var key = $"{(walkOffKeys ? "w" : "g")}|{(walkOffKeys ? diff : 0)}|{occupancy}|{outs}|{combo.Key}";
        var oracle = FeasibilityData.Oracle;
        var expected = FeasibilityData.Case(key);
        var before = Digest(state);
        var plan = InPlayPlanner.Plan(state, combo.Batted, combo.Result, "p1", Location, fault);
        var applied = GameReducer.Apply(state, plan.Event);
        var mismatches = new List<string>();
        void Check(string name, object? actual, object? want)
        {
            if (!Equals(actual, want))
                mismatches.Add($"{name}: actual {actual} expected {want}");
        }

        if (Digest(state) != before)
            mismatches.Add("the state the event was applied to changed");
        if (plan.Event.MoveList.Any(m => m.Sequence is not null) || plan.Event.BatterSequence is not null)
            mismatches.Add("a play sequence was given although no out/run order conflict exists in a base plan");

        var officialTplShort = Short(expected.GetProperty("official").GetString()!);
        var cls = InPlayTemplates.ClassOf(combo.Batted);
        var officialId = $"ipt1.{cls}.{officialTplShort}";
        var oracleTpl = oracle.GetProperty("templates").GetProperty(officialId);
        Check("planned official result", plan.Official.Result, expected.GetProperty("official").GetString());
        Check("planned drawn result", plan.Drawn.Result, expected.GetProperty("drawn").GetString());
        Check("planned status", plan.Status, expected.GetProperty("status").GetString());
        Check("planned pending", plan.Pending, expected.GetProperty("pending").GetBoolean());
        Check("planned context", plan.ContextPresent, expected.GetProperty("context_present").GetBoolean());
        Check("planned walk-off effect", plan.WalkOff, expected.GetProperty("walkoff").GetString());
        var plannedMoves = string.Join(",", plan.Moves.Select(m => $"{m.From}>{m.To}"));
        var oracleMoves = string.Join(",", expected.GetProperty("moves").EnumerateObject().Select(p => (From: int.Parse(p.Name), To: p.Value.GetInt32())).OrderByDescending(m => m.From).Select(m => $"{m.From}>{m.To}"));
        Check("planned runner moves", plannedMoves, oracleMoves);
        var oracleCredits = string.Join(",", oracleTpl.GetProperty("credits").EnumerateArray().Select(c => c.GetString()));
        var plannedCredits = InPlayTemplates.CreditText(plan.Event.BatterCreditList.ToArray());
        Check("planned batter credits", plannedCredits, oracleCredits);
        var oracleError = oracleTpl.GetProperty("error").ValueKind == JsonValueKind.Null ? "" : oracleTpl.GetProperty("error").GetString();
        var plannedError = string.Join(",", plan.Event.ErrorList.Select(InPlayTemplates.Short));
        Check("planned errors", plannedError, oracleError);

        string errorCode = "", credits = "", errors = "", receipt = "";
        int runs = 0, outsAfter = 0, basesAfter = 0, lob = 0, rbi = 0;
        bool halfOver = false, gameOver = false;
        string official = "";
        if (!applied.IsSuccess)
        {
            errorCode = applied.Error!.Value.Code;
            mismatches.Add($"the reducer refused the event: {errorCode}");
        }
        else
        {
            var step = applied.Value;
            var play = step.NewPlays.Single();
            official = Name(play.Result!.Value);
            runs = play.Runs.Count;
            outsAfter = play.OutsAfter;
            halfOver = outsAfter >= state.Rules.OutsPerHalfInning;
            gameOver = step.State.Status == GameStatus.Final;
            basesAfter = halfOver ? 0 : Mask(step.State.Bases);
            lob = step.Delta.Entries.Where(e => e.Key == StatKey.TeamLOB).Sum(e => e.Amount);
            rbi = step.Delta.Entries.Where(e => e.Key == StatKey.BatRBI).Sum(e => e.Amount);
            credits = string.Join(",", play.Credits.Select(c => $"{InPlayTemplates.Short(c.Position)}:{(c.Kind == CreditKind.Putout ? "PO" : "A")}"));
            errors = string.Join(",", play.Errors.Select(InPlayTemplates.Short));
            receipt = step.State.AppliedEvents.Last().FingerprintVersion;

            Check("official result", official, expected.GetProperty("official").GetString());
            Check("runs", runs, expected.GetProperty("runs").GetInt32());
            Check("outs after", outsAfter, expected.GetProperty("outs_after").GetInt32());
            Check("half over", halfOver, expected.GetProperty("half_over").GetBoolean());
            Check("game over", gameOver, expected.GetProperty("game_over").GetBoolean());
            if (gameOver)
                Check("game outcome", step.State.Outcome, halfOver ? GameOutcome.AwayWin : GameOutcome.HomeWin);   // a walk-off is a home win; the last out of a lost last half is an away win
            if (!halfOver)
                Check("runners after", basesAfter, expected.GetProperty("bases_after").GetInt32());
            Check("left on base", lob, expected.GetProperty("lob").GetInt32());
            Check("rbi", rbi, expected.GetProperty("rbi").GetInt32());
            Check("hits", step.Delta.Entries.Where(e => e.Key == StatKey.BatH).Sum(e => e.Amount), expected.GetProperty("hit").GetInt32());
            Check("at bats", step.Delta.Entries.Where(e => e.Key == StatKey.BatAB).Sum(e => e.Amount), expected.GetProperty("ab").GetInt32());
            Check("credits", credits, oracleCredits);
            Check("errors", errors, oracleError);
            Check("fingerprint version", receipt, "ef2");
            Check("one pitch counted", step.Delta.Entries.Count(e => e.Key == StatKey.PitchPitches), 1);

            // every existing runner has exactly one move, and the batter exactly one
            Check("move count", play.Moves.Count, state.Bases.Count + 1);
            Check("distinct movers", play.Moves.Select(m => m.RunnerKey).Distinct().Count(), play.Moves.Count);
            foreach (var (_, runner) in state.Bases.Occupied())
                Check($"runner {runner.RunnerKey} moved once", play.Moves.Count(m => m.RunnerKey == runner.RunnerKey), 1);

            // putouts and assists are credited to the players who really stand in those positions; one putout per out; errors have an error entry each
            var fielding = state.FieldingSide;
            foreach (var c in play.Credits)
            {
                var player = state.DefenderAt(fielding, c.Position);
                if (player is null || c.Player != player)
                    mismatches.Add($"credit {c.Position} does not name the defender at that position");
            }

            Check("putouts equal outs made", play.Credits.Count(c => c.Kind == CreditKind.Putout), outsAfter - state.Outs);
            Check("error entries equal errors", step.Delta.Entries.Count(e => e.Key == StatKey.FldE), play.Errors.Count);
            foreach (var p in play.Errors)
                if (state.DefenderAt(fielding, p) is null)
                    mismatches.Add($"error position {p} has no defender");

            // determinism: planning and applying again gives the identical event fingerprint and the identical resulting state
            var plan2 = InPlayPlanner.Plan(state, combo.Batted, combo.Result, "p1", Location, fault);
            var applied2 = GameReducer.Apply(state, plan2.Event);
            if (!applied2.IsSuccess || EventFingerprint.Receipt(plan2.Event) != EventFingerprint.Receipt(plan.Event) || Digest(applied2.Value.State) != Digest(step.State))
                mismatches.Add("the same situation did not give the same result twice");
        }

        var status = mismatches.Count == 0 ? plan.Status : "DEFECT_OR_UNRESOLVED";
        return new CaseRow(key, context, diff, occupancy, outs, combo.Batted, combo.Result, combo.Ppm, plan.Drawn.Id, plan.Official.Id, plan.Drawn.Result, plan.Official.Result,
            plannedMoves, plan.WinningRunnerBase, applied.IsSuccess, errorCode, runs, outsAfter, halfOver, gameOver, basesAfter, lob, rbi, credits, errors, status, plan.WalkOff,
            plan.ContextPresent, plan.Pending, mismatches.Count == 0, string.Join("; ", mismatches), receipt);
    }

    /// <summary>Runs every positive-probability combination on all 8 runner situations x 3 out counts of the given root (diffs: the walk-off scores behind).</summary>
    public static List<CaseRow> RunAll(InPlayStates.Root root, string context, bool walkOffKeys, IEnumerable<int> diffs, Fault fault = Fault.None, bool fullCount = false)
    {
        var rows = new List<CaseRow>();
        foreach (var diff in diffs)
            for (var occ = 0; occ < 8; occ++)
                for (var outs = 0; outs < 3; outs++)
                {
                    var state = fullCount ? InPlayStates.WithFullCount(root, diff, occ, outs) : InPlayStates.Make(root, diff, occ, outs);
                    foreach (var combo in FeasibilityData.Positive)
                        rows.Add(RunOne(context, walkOffKeys, diff, occ, outs, combo, state, fault));
                }

        return rows;
    }

    public static Dictionary<string, int> Counts(IEnumerable<CaseRow> rows) =>
        rows.GroupBy(r => r.Status).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Raw machine-readable results (one JSON document) for the report; written only when the caller asks for it.</summary>
    public static string ToJson(string title, IReadOnlyDictionary<string, IReadOnlyList<CaseRow>> groups, IReadOnlyDictionary<string, string> extra)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();
            w.WriteString("format", "phase3c2b2p-feasibility-raw-v1");
            w.WriteString("title", title);
            w.WriteString("inplay_mapping", InPlayPlanner.MappingVersion);
            w.WriteString("walkoff_credit", InPlayPlanner.WalkOffVersion);
            w.WriteString("template_catalog", InPlayTemplates.CatalogVersion);
            foreach (var (k, v) in extra)
                w.WriteString(k, v);
            w.WriteStartArray("combos");
            foreach (var c in FeasibilityData.AllCombos)
            {
                w.WriteStartObject();
                w.WriteString("batted", c.Batted);
                w.WriteString("result", c.Result);
                w.WriteNumber("ppm", c.Ppm);
                w.WriteBoolean("positive", c.Positive);
                w.WriteString("template", c.Positive ? $"ipt1.{InPlayTemplates.ClassOf(c.Batted)}.{Short(InPlayTemplates.ResultText(c.Result))}" : null);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("templates");
            foreach (var t in InPlayTemplates.All)
            {
                w.WriteStartObject();
                w.WriteString("id", t.Id);
                w.WriteString("class", t.Class);
                w.WriteString("result", t.Result);
                w.WriteNumber("batter_dest", t.BatterDest);
                w.WriteString("credits", InPlayTemplates.CreditText(t.Credits));
                w.WriteString("error", t.Error is { } e ? InPlayTemplates.Short(e) : null);
                w.WriteString("retriever", t.Retriever is { } r ? InPlayTemplates.Short(r) : null);
                w.WriteString("runner_policy", t.RunnerPolicy);
                w.WriteString("action", t.Action);
                w.WriteString("premise", t.Premise);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            foreach (var (name, rows) in groups)
            {
                w.WriteStartObject(name);
                w.WriteStartObject("summary");
                foreach (var (k, v) in Counts(rows))
                    w.WriteNumber(k, v);
                w.WriteNumber("total", rows.Count);
                w.WriteNumber("reducer_accepted", rows.Count(r => r.ReducerOk));
                w.WriteNumber("oracle_match", rows.Count(r => r.OracleMatch));
                w.WriteEndObject();
                w.WriteStartArray("rows");
                foreach (var r in rows)
                {
                    w.WriteStartObject();
                    w.WriteString("k", r.Key);
                    w.WriteString("drawn", r.Drawn);
                    w.WriteString("official", r.Official);
                    w.WriteString("tpl", r.OfficialTemplate);
                    w.WriteString("mv", r.Moves);
                    if (r.WinnerBase is { } wb) w.WriteNumber("win", wb);
                    w.WriteBoolean("ok", r.ReducerOk);
                    if (r.ErrorCode.Length > 0) w.WriteString("err", r.ErrorCode);
                    w.WriteNumber("runs", r.Runs);
                    w.WriteNumber("outs", r.OutsAfter);
                    w.WriteBoolean("half", r.HalfOver);
                    w.WriteBoolean("over", r.GameOver);
                    w.WriteNumber("bases", r.BasesAfter);
                    w.WriteNumber("lob", r.Lob);
                    w.WriteNumber("rbi", r.Rbi);
                    w.WriteString("cr", r.Credits);
                    w.WriteString("e", r.Errors);
                    w.WriteString("st", r.Status);
                    w.WriteString("wo", r.WalkOff);
                    w.WriteBoolean("pend", r.Pending);
                    w.WriteBoolean("match", r.OracleMatch);
                    if (r.Mismatch.Length > 0) w.WriteString("mm", r.Mismatch);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray()) + "\n";
    }
}
