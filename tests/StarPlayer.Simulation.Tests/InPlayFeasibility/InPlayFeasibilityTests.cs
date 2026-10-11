using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.InPlayFeasibility.FeasibilityHarness;
using Root = StarPlayer.Simulation.Tests.InPlayFeasibility.InPlayStates.Root;

namespace StarPlayer.Simulation.Tests.InPlayFeasibility;

/// <summary>
/// Phase 3-C2B-2-P (policy lock and template feasibility). Nothing here tests production code that does not exist yet: the in-play candidate generator is NOT
/// implemented. These tests prove, with the REAL <see cref="GameReducer.Apply"/>, that every positive-probability (batted ball, defence result) combination can be written
/// as a legal virtual play under the owner-approved prototype policy, and they separate what is final from what must wait for the 3-C2B-3 advancement draw.
/// Expected values come from three sources that do not share code: the independent Python oracle (tests/Fixtures/generator/generate_inplay_feasibility_oracle.py),
/// hand-derived literal tables in this file, and the reducer itself.
/// </summary>
public class InPlayFeasibilityTests
{
    private static readonly Dictionary<string, int> ExpectedPositivePerBall = new()
    {
        ["ground_weak"] = 3, ["ground_medium"] = 4, ["ground_hard"] = 4, ["line_weak"] = 4, ["line_medium"] = 5, ["line_hard"] = 6,
        ["fly_weak"] = 4, ["fly_medium"] = 6, ["fly_hard"] = 6, ["popup"] = 3,
    };

    // ---------------------------------------------------------------- the 60 / 45 / 15 combinations

    [Fact]
    public void The_tuning_has_10_balls_x_6_results_60_combinations_45_positive_and_15_zero()
    {
        var all = FeasibilityData.AllCombos;
        Assert.Equal(60, all.Count);
        Assert.Equal(10, all.Select(c => c.Batted).Distinct().Count());
        Assert.Equal(6, all.Select(c => c.Result).Distinct().Count());
        Assert.Equal(45, all.Count(c => c.Positive));
        Assert.Equal(15, all.Count(c => !c.Positive));
        foreach (var (ball, want) in ExpectedPositivePerBall)
            Assert.Equal(want, all.Count(c => c.Batted == ball && c.Positive));
        Assert.Equal(45, ExpectedPositivePerBall.Values.Sum());
    }

    [Fact]
    public void The_fifteen_zero_probability_combinations_are_exactly_the_hand_derived_list()
    {
        // read by hand from content/tuning/virtual_tuning_v1.json (defense_result rows): a result with 0 ppm
        string[] zero =
        [
            "ground_weak/double", "ground_weak/triple", "ground_weak/home_run",
            "ground_medium/triple", "ground_medium/home_run",
            "ground_hard/triple", "ground_hard/home_run",
            "line_weak/triple", "line_weak/home_run",
            "line_medium/home_run",
            "fly_weak/triple", "fly_weak/home_run",
            "popup/double", "popup/triple", "popup/home_run",
        ];
        Assert.Equal(zero.OrderBy(z => z, StringComparer.Ordinal), FeasibilityData.AllCombos.Where(c => !c.Positive).Select(c => c.Key).OrderBy(z => z, StringComparer.Ordinal));
        // popup single and popup reached-on-error are positive (they must not be forgotten)
        Assert.Contains(FeasibilityData.Positive, c => c.Key == "popup/single" && c.Ppm == 5000);
        Assert.Contains(FeasibilityData.Positive, c => c.Key == "popup/reached_on_error" && c.Ppm == 20000);
        // the defence-result distribution never produces a fielder's choice
        Assert.DoesNotContain(FeasibilityData.AllCombos, c => c.Result.Contains("fielder", StringComparison.Ordinal));
    }

    [Fact]
    public void The_python_oracle_agrees_on_the_combinations_and_the_template_catalogue()
    {
        var oracle = FeasibilityData.Oracle;
        Assert.Equal(45, oracle.GetProperty("positive_count").GetInt32());
        Assert.Equal(15, oracle.GetProperty("zero_count").GetInt32());
        var oracleCombos = oracle.GetProperty("combos").EnumerateArray().Select(c => (Key: c.GetProperty("batted").GetString() + "/" + c.GetProperty("result").GetString(), Ppm: c.GetProperty("ppm").GetInt32())).ToList();
        Assert.Equal(FeasibilityData.AllCombos.Select(c => (c.Key, c.Ppm)), oracleCombos);

        var templates = oracle.GetProperty("templates").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        Assert.Equal(19, templates.Count);
        Assert.Equal(19, InPlayTemplates.All.Count);
        Assert.Equal(templates.Keys.OrderBy(k => k, StringComparer.Ordinal), InPlayTemplates.All.Select(t => t.Id).OrderBy(k => k, StringComparer.Ordinal));
        foreach (var t in InPlayTemplates.All)
        {
            var o = templates[t.Id];
            Assert.Equal(t.Class, o.GetProperty("class").GetString());
            Assert.Equal(t.Result, o.GetProperty("result").GetString());
            Assert.Equal(t.BatterDest, o.GetProperty("batter_dest").GetInt32());
            Assert.Equal(InPlayTemplates.CreditText(t.Credits), string.Join(",", o.GetProperty("credits").EnumerateArray().Select(c => c.GetString())));
            Assert.Equal(t.Error is { } e ? InPlayTemplates.Short(e) : null, o.GetProperty("error").GetString());
        }
    }

    [Fact]
    public void Every_positive_combination_has_a_declared_versioned_template_and_every_template_is_used()
    {
        foreach (var c in FeasibilityData.Positive)
        {
            var t = InPlayTemplates.Find(InPlayTemplates.ClassOf(c.Batted), InPlayTemplates.ResultText(c.Result));
            Assert.NotNull(t);
            Assert.StartsWith("ipt1.", t!.Id, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(t.Action));
            Assert.False(string.IsNullOrWhiteSpace(t.Premise));
        }

        Assert.Equal(InPlayTemplates.All.Select(t => t.Id).Distinct().Count(), InPlayTemplates.All.Count);
        var used = FeasibilityData.Positive.Select(c => $"ipt1.{InPlayTemplates.ClassOf(c.Batted)}.{Short(InPlayTemplates.ResultText(c.Result))}").Distinct().ToList();
        Assert.Equal(InPlayTemplates.All.Select(t => t.Id).OrderBy(x => x, StringComparer.Ordinal), used.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal("inplay-template-v1", InPlayTemplates.CatalogVersion);
        Assert.Equal("inplay-mapping-v1", InPlayPlanner.MappingVersion);
        Assert.Equal("walkoff-credit-v1", InPlayPlanner.WalkOffVersion);
    }

    [Fact]
    public void Template_shapes_follow_the_credit_rules()
    {
        foreach (var t in InPlayTemplates.All)
        {
            if (t.Result == "Out")
            {
                Assert.Equal(1, t.Credits.Count(c => c.Kind == CreditKind.Putout));                       // exactly one putout for the batter's out
                Assert.Null(t.Error);
            }
            else
                Assert.Empty(t.Credits);                                                                  // no out, no credit (no phantom putout or assist)

            if (t.Result == "ReachedOnError")
            {
                Assert.NotNull(t.Error);                                                                  // one error, one position
                Assert.Equal(InPlayResult.ReachedOnError, t.Reducer);
            }
            else
                Assert.Null(t.Error);
        }

        // assists only where the template really contains a throw
        Assert.Equal(new[] { "ipt1.ground.out" }, InPlayTemplates.All.Where(t => t.Credits.Any(c => c.Kind == CreditKind.Assist)).Select(t => t.Id));
        // the four out templates keep their class and use the fixed fielders of the task
        Assert.Equal("SS:A,1B:PO", InPlayTemplates.CreditText(InPlayTemplates.ById("ipt1.ground.out").Credits));
        Assert.Equal("SS:PO", InPlayTemplates.CreditText(InPlayTemplates.ById("ipt1.line.out").Credits));
        Assert.Equal("CF:PO", InPlayTemplates.CreditText(InPlayTemplates.ById("ipt1.fly.out").Credits));
        Assert.Equal("2B:PO", InPlayTemplates.CreditText(InPlayTemplates.ById("ipt1.pop.out").Credits));
        Assert.Equal(FieldPosition.Shortstop, InPlayTemplates.ById("ipt1.ground.roe").Error);
        Assert.Equal(FieldPosition.Shortstop, InPlayTemplates.ById("ipt1.line.roe").Error);
        Assert.Equal(FieldPosition.CenterField, InPlayTemplates.ById("ipt1.fly.roe").Error);
        Assert.Equal(FieldPosition.CenterField, InPlayTemplates.ById("ipt1.pop.roe").Error);
        // batted-ball class is preserved
        foreach (var t in InPlayTemplates.All)
            Assert.Equal(t.Class switch { "ground" => BattedBallType.Ground, "line" => BattedBallType.Line, "fly" => BattedBallType.Fly, _ => BattedBallType.Pop }, t.Batted);
    }

    // ---------------------------------------------------------------- the base running table (hand-derived)

    private static readonly string[] OccupancyNames = ["empty", "1B", "2B", "1B+2B", "3B", "1B+3B", "2B+3B", "loaded"];

    // moves from the lead runner back, "from>to"; derived by hand from the policy text, not from the planner or the oracle
    private static readonly Dictionary<string, string[]> HandTable = new()
    {
        ["ground out, fewer than two outs"] = ["", "1>2", "2>2", "2>3,1>2", "3>3", "3>3,1>2", "3>3,2>2", "3>4,2>3,1>2"],
        ["ground out, two outs / line / fly / pop out"] = ["", "1>1", "2>2", "2>2,1>1", "3>3", "3>3,1>1", "3>3,2>2", "3>3,2>2,1>1"],
        ["Single"] = ["", "1>2", "2>3", "2>3,1>2", "3>4", "3>4,1>2", "3>4,2>3", "3>4,2>3,1>2"],
        ["Double"] = ["", "1>3", "2>4", "2>4,1>3", "3>4", "3>4,1>3", "3>4,2>4", "3>4,2>4,1>3"],
        ["Triple"] = ["", "1>4", "2>4", "2>4,1>4", "3>4", "3>4,1>4", "3>4,2>4", "3>4,2>4,1>4"],
        ["HomeRun"] = ["", "1>4", "2>4", "2>4,1>4", "3>4", "3>4,1>4", "3>4,2>4", "3>4,2>4,1>4"],
        ["ReachedOnError"] = ["", "1>2", "2>2", "2>3,1>2", "3>3", "3>3,1>2", "3>3,2>2", "3>4,2>3,1>2"],
    };

    [Fact]
    public void The_base_running_table_matches_the_hand_derived_table_for_all_eight_situations()
    {
        string Moves(Root root, int occ, int outs, string batted, string result) =>
            string.Join(",", InPlayPlanner.Plan(InPlayStates.Make(root, 0, occ, outs), batted, result, "p1", Location).Moves.Select(m => $"{m.From}>{m.To}"));

        for (var occ = 0; occ < 8; occ++)
        {
            Assert.Equal(HandTable["ground out, fewer than two outs"][occ], Moves(Root.GeneralTop, occ, 0, "ground_medium", "out"));
            Assert.Equal(HandTable["ground out, fewer than two outs"][occ], Moves(Root.GeneralTop, occ, 1, "ground_hard", "out"));
            Assert.Equal(HandTable["ground out, two outs / line / fly / pop out"][occ], Moves(Root.GeneralTop, occ, 2, "ground_medium", "out"));
            foreach (var ball in new[] { "line_medium", "fly_medium", "popup" })
                foreach (var outs in new[] { 0, 1, 2 })
                    Assert.Equal(HandTable["ground out, two outs / line / fly / pop out"][occ], Moves(Root.GeneralTop, occ, outs, ball, "out"));
            foreach (var (result, ball) in new[] { ("Single", "line_medium"), ("Double", "line_hard"), ("Triple", "fly_hard"), ("HomeRun", "fly_hard"), ("ReachedOnError", "ground_weak") })
                foreach (var outs in new[] { 0, 1, 2 })
                    Assert.Equal(HandTable[result][occ], Moves(Root.GeneralTop, occ, outs, ball, result switch { "Single" => "single", "Double" => "double", "Triple" => "triple", "HomeRun" => "home_run", _ => "reached_on_error" }));
        }

        Assert.Equal(8, OccupancyNames.Length);
    }

    [Fact]
    public void A_forced_runner_pushed_by_a_reached_on_error_has_the_Forced_cause_and_the_others_hold_with_Play()
    {
        foreach (var occ in new[] { 1, 3, 5, 7 })
        {
            var s = InPlayStates.Make(Root.GeneralTop, 0, occ, 0);
            var plan = InPlayPlanner.Plan(s, "ground_weak", "reached_on_error", "p1", Location);
            var applied = GameReducer.Apply(s, plan.Event);
            Assert.True(applied.IsSuccess, applied.Error?.Message);
            var play = applied.Value.NewPlays.Single();
            // no separate move of the batter-runner is part of the event (the reducer records the batter's own plan, not an input move)
            Assert.DoesNotContain(plan.Event.MoveList, m => m.From == 0);
            foreach (var m in play.Moves.Where(m => m.From >= 1))
            {
                var forced = Enumerable.Range(1, m.From - 1).All(b => s.Bases.At(b) is not null);
                Assert.Equal(forced ? MoveCause.Forced : MoveCause.Play, m.Cause);
                Assert.Equal(forced ? m.From + 1 : m.From, m.To);
            }

            Assert.Null(play.Moves.Single(m => m.From == 0).OutKind);                                       // the batter's reach adds no invented out or putout
            Assert.Empty(play.Credits);
        }
    }

    // ---------------------------------------------------------------- the 1,080 ordinary cases

    private static string Summarise(IReadOnlyCollection<CaseRow> rows) =>
        string.Join(", ", Counts(rows).Select(kv => $"{kv.Key}={kv.Value}")) + $", reducer accepted {rows.Count(r => r.ReducerOk)}/{rows.Count}, oracle match {rows.Count(r => r.OracleMatch)}/{rows.Count}";

    private static void AssertClean(IReadOnlyCollection<CaseRow> rows, int total, int legal, int pending, int walkOffPolicy)
    {
        var bad = rows.Where(r => !r.OracleMatch || !r.ReducerOk).Take(5).Select(r => $"{r.Key}: {r.Mismatch}").ToList();
        Assert.True(bad.Count == 0, "first failures:\n" + string.Join("\n", bad));
        Assert.Equal(total, rows.Count);
        var counts = Counts(rows);
        Assert.Equal(legal, counts.GetValueOrDefault("LEGAL_BASE"));
        Assert.Equal(pending, counts.GetValueOrDefault("PENDING_ADVANCEMENT"));
        Assert.Equal(walkOffPolicy, counts.GetValueOrDefault("NEEDS_WALKOFF_POLICY"));
        Assert.Equal(0, counts.GetValueOrDefault("UNSUPPORTED_TEMPLATE"));
        Assert.Equal(0, counts.GetValueOrDefault("DEFECT_OR_UNRESOLVED"));
        Assert.DoesNotContain(rows, r => r.Status == "UNREACHABLE_ZERO_PPM");                              // zero-probability pairs are never executed
        Assert.DoesNotContain(rows, r => r.Ppm <= 0);
        Assert.DoesNotContain(rows, r => r.HalfOver && r.Runs > 0);                                        // a base plan never has a third out and a run together: no order (Sequence) is needed
        Assert.All(rows, r => Assert.Equal("ef2", r.Receipt));
    }

    [Fact]
    public void The_1080_ordinary_cases_are_all_legal_and_match_the_independent_oracle()
    {
        var rows = RunAll(Root.GeneralTop, "general_top", false, [0]);
        Assert.Equal(45 * 8 * 3, rows.Count);
        AssertClean(rows, 1080, legal: 780, pending: 300, walkOffPolicy: 0);
        Assert.All(rows, r => Assert.Equal("none", r.WalkOff));                                            // an ordinary situation never needs the walk-off policy
        Assert.All(rows, r => Assert.Equal(r.Drawn, r.Official));                                           // and never changes the drawn result
        Assert.DoesNotContain(rows, r => r.GameOver);
    }

    [Fact]
    public void The_same_1080_cases_also_hold_for_the_home_half_a_game_without_a_designated_hitter_and_a_full_count()
    {
        var top = RunAll(Root.GeneralTop, "general_top", false, [0]);
        foreach (var (root, context, full) in new[] { (Root.GeneralBottom, "general_bottom", false), (Root.GeneralTopNoDh, "general_top_no_dh", false), (Root.GeneralTop, "general_top_full_count", true) })
        {
            var rows = RunAll(root, context, false, [0], fullCount: full);
            AssertClean(rows, 1080, legal: 780, pending: 300, walkOffPolicy: 0);
            Assert.Equal(top.Select(r => (r.Key, r.Official, r.Runs, r.OutsAfter, r.BasesAfter, r.Status)), rows.Select(r => (r.Key, r.Official, r.Runs, r.OutsAfter, r.BasesAfter, r.Status)));
        }
    }

    [Fact]
    public void Pending_cases_are_exactly_the_four_declared_advancement_contexts()
    {
        var rows = RunAll(Root.GeneralTop, "general_top", false, [0]);
        foreach (var r in rows)
        {
            var occ = r.Occupancy;
            var first = (occ & 1) != 0; var second = (occ & 2) != 0; var third = (occ & 4) != 0;
            var ctx = r.Official == "Single" && (first || second)
                || r.Official == "Double" && first
                || r.Official == "Out" && r.Batted.StartsWith("fly", StringComparison.Ordinal) && r.Outs < 2 && third;
            Assert.Equal(ctx, r.Pending);
            Assert.Equal(ctx ? "PENDING_ADVANCEMENT" : "LEGAL_BASE", r.Status);
        }

        // counted by hand: 10 single combinations x 6 situations with a runner on first or second x 3 outs; 8 double combinations x 4 situations with a runner on first x 3 outs;
        // 3 fly-out combinations x 4 situations with a runner on third x 2 outs (0 and 1)
        Assert.Equal(10 * 6 * 3, rows.Count(r => r.Pending && r.Official == "Single"));
        Assert.Equal(8 * 4 * 3, rows.Count(r => r.Pending && r.Official == "Double"));
        Assert.Equal(3 * 4 * 2, rows.Count(r => r.Pending && r.Official == "Out"));
        Assert.Equal(180 + 96 + 24, rows.Count(r => r.Pending));
    }

    [Fact]
    public void Outs_of_the_batter_that_end_the_inning_keep_every_runner_in_place_and_score_nothing()
    {
        var rows = RunAll(Root.GeneralTop, "general_top", false, [0]).Where(r => r.Official == "Out" && r.Outs == 2).ToList();
        Assert.Equal(10 * 8, rows.Count);                                                                  // 10 out combinations x 8 situations
        Assert.All(rows, r => { Assert.Equal(0, r.Runs); Assert.True(r.HalfOver); Assert.Equal(3, r.OutsAfter); Assert.Equal(r.Occupancy.BitCount(), r.Lob); });
    }

    // ---------------------------------------------------------------- the walk-off matrix

    [Fact]
    public void The_4320_walk_off_combinations_are_all_executable_and_match_the_oracle()
    {
        var rows = RunAll(Root.WalkOff, "bottom_ninth", true, [0, 1, 2, 3]);
        Assert.Equal(45 * 8 * 3 * 4, rows.Count);
        AssertClean(rows, 4320, legal: 3114, pending: 1014, walkOffPolicy: 192);
        // every walk-off by a non-home-run play scores exactly the needed runs; a home run scores everyone
        foreach (var r in rows.Where(r => r.WalkOff is "game_ends" or "credit_reduced"))
        {
            var need = r.Diff + 1;
            if (r.Official == "HomeRun")
                Assert.True(r.Runs >= need);
            else
                Assert.Equal(need, r.Runs);
            Assert.True(r.GameOver);
        }

        // the credit is reduced only for a drawn double or triple and never for anything else
        Assert.All(rows.Where(r => r.WalkOff == "credit_reduced"), r =>
        {
            Assert.Contains(r.Drawn, new[] { "Double", "Triple" });
            Assert.NotEqual(r.Drawn, r.Official);
        });
        Assert.All(rows.Where(r => r.WalkOff != "credit_reduced"), r => Assert.Equal(r.Drawn, r.Official));
        // a walk-off candidate is never also pending: the winning run makes later advancement moot
        Assert.DoesNotContain(rows, r => r.Pending && r.GameOver);
    }

    [Fact]
    public void The_first_extra_inning_behaves_like_the_ninth()
    {
        var rows = RunAll(Root.ExtraInning, "bottom_tenth", true, [0]);
        var ninth = RunAll(Root.WalkOff, "bottom_ninth", true, [0]);
        Assert.Equal(1080, rows.Count);
        AssertClean(rows, 1080, legal: ninth.Count(r => r.Status == "LEGAL_BASE"), pending: ninth.Count(r => r.Status == "PENDING_ADVANCEMENT"), walkOffPolicy: ninth.Count(r => r.Status == "NEEDS_WALKOFF_POLICY"));
        Assert.Equal(ninth.Select(r => (r.Key, r.Official, r.Runs, r.GameOver)), rows.Select(r => (r.Key, r.Official, r.Runs, r.GameOver)));
    }

    [Fact]
    public void A_home_team_that_stays_behind_loses_after_the_third_out_and_a_tied_game_goes_on()
    {
        var rows = RunAll(Root.WalkOff, "bottom_ninth", true, [0, 1, 2, 3]).Where(r => r.HalfOver).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(r.Diff >= 1, r.GameOver));                                       // behind: the last out ends the game; tied after 9: extra innings
    }

    // ---------------------------------------------------------------- ten representative cases with hand-derived expectations

    private sealed record Hand(string Name, Root Root, int Diff, int Occ, int Outs, string Ball, string Result, string Official, int Runs, bool Over, string WalkOff, string Status,
        int BasesAfter, int Lob, int Rbi, string Moves);

    private static readonly Hand[] RepresentativeCases =
    [
        // 1  tied, runner on third, a double is drawn: only one run is needed and the runner on third advanced one base -> official single (OBR 9.06(f))
        new("1 tie, 3B, drawn double -> single", Root.WalkOff, 0, 4, 0, "fly_hard", "double", "Single", 1, true, "credit_reduced", "NEEDS_WALKOFF_POLICY", 1, 1, 1, "3>4"),
        // 2  tied, bases loaded, a double is drawn: the lead runner (from third) is the winning run -> single, the facts are those of a single
        new("2 tie, loaded, drawn double -> single", Root.WalkOff, 0, 7, 1, "line_hard", "double", "Single", 1, true, "credit_reduced", "NEEDS_WALKOFF_POLICY", 7, 3, 1, "3>4,2>3,1>2"),
        // 3  tied, runner on second, a triple is drawn: the winning run advanced two bases -> double (9.06(f) comment: the batter-runner reaches second naturally)
        new("3 tie, 2B, drawn triple -> double", Root.WalkOff, 0, 2, 0, "fly_hard", "triple", "Double", 1, true, "credit_reduced", "NEEDS_WALKOFF_POLICY", 2, 1, 1, "2>4"),
        // 4  tied, runner on first, a triple is drawn: the runner scores from first (three bases) -> triple stays
        new("4 tie, 1B, drawn triple stays triple", Root.WalkOff, 0, 1, 2, "fly_hard", "triple", "Triple", 1, true, "game_ends", "LEGAL_BASE", 4, 1, 1, "1>4"),
        // 5  tied, bases loaded, an out-of-the-park home run: everyone scores
        new("5 tie, loaded, home run -> 4 runs", Root.WalkOff, 0, 7, 0, "fly_hard", "home_run", "HomeRun", 4, true, "game_ends", "LEGAL_BASE", 0, 0, 4, "3>4,2>4,1>4"),
        // 6  one run behind, runners on second and third, a double: both score, the second (winning) run came from second (two bases) -> double stays
        new("6 behind 1, 2B+3B, double -> 2 runs", Root.WalkOff, 1, 6, 1, "line_hard", "double", "Double", 2, true, "game_ends", "LEGAL_BASE", 2, 1, 2, "3>4,2>4"),
        // 7  two runs behind, bases loaded, a triple: three runs, the third (winning) one from first (three bases) -> triple stays
        new("7 behind 2, loaded, triple -> 3 runs", Root.WalkOff, 2, 7, 2, "line_hard", "triple", "Triple", 3, true, "game_ends", "LEGAL_BASE", 4, 1, 3, "3>4,2>4,1>4"),
        // 8  tied, runner on second, a single: the base plan moves him to third; whether he scores is the advancement draw (3-C2B-3) -> pending, no final score invented
        new("8 tie, 2B, single -> pending", Root.WalkOff, 0, 2, 0, "ground_medium", "single", "Single", 0, false, "none", "PENDING_ADVANCEMENT", 5, 0, 0, "2>3"),
        // 9  two outs, tied, bases loaded, a single reached-on-error: the forced run from third scores (bases-loaded push), no hit, no RBI
        new("9 tie, loaded, 2 outs, ROE -> walk-off", Root.WalkOff, 0, 7, 2, "popup", "reached_on_error", "ReachedOnError", 1, true, "game_ends", "LEGAL_BASE", 7, 3, 0, "3>4,2>3,1>2"),
        // 10 tied, nobody on, a double: nothing is at stake, the game goes on with the batter on second
        new("10 tie, empty, double -> continues", Root.WalkOff, 0, 0, 0, "line_medium", "double", "Double", 0, false, "none", "LEGAL_BASE", 2, 0, 0, ""),
    ];

    [Fact]
    public void Ten_representative_cases_match_their_hand_derived_expectations()
    {
        Assert.Equal(10, RepresentativeCases.Length);
        foreach (var h in RepresentativeCases)
        {
            var state = InPlayStates.Make(h.Root, h.Diff, h.Occ, h.Outs);
            var plan = InPlayPlanner.Plan(state, h.Ball, h.Result, "p1", Location);
            var applied = GameReducer.Apply(state, plan.Event);
            Assert.True(applied.IsSuccess, $"{h.Name}: {applied.Error?.Code} {applied.Error?.Message}");
            var step = applied.Value;
            var play = step.NewPlays.Single();
            Assert.Equal(h.Official, play.Result!.Value.ToString());
            Assert.Equal(h.Runs, play.Runs.Count);
            Assert.Equal(h.Over, step.State.Status == GameStatus.Final);
            Assert.Equal(h.WalkOff, plan.WalkOff);
            Assert.Equal(h.Status, plan.Status);
            Assert.Equal(h.Moves, string.Join(",", plan.Moves.Select(m => $"{m.From}>{m.To}")));
            Assert.Equal(h.Lob, step.Delta.Entries.Where(e => e.Key == StatKey.TeamLOB).Sum(e => e.Amount));
            Assert.Equal(h.Rbi, step.Delta.Entries.Where(e => e.Key == StatKey.BatRBI).Sum(e => e.Amount));
            Assert.Equal(h.BasesAfter, Mask(step.State.Bases));
            if (h.Over)
                Assert.Equal(GameOutcome.HomeWin, step.State.Outcome);
            // the third-out/run order is never needed for any of the ten
            Assert.All(plan.Event.MoveList, m => Assert.Null(m.Sequence));
        }
    }

    [Fact]
    public void Case_9_the_bases_loaded_push_is_a_Forced_run_with_no_hit_and_an_E8_error()
    {
        var state = InPlayStates.Make(Root.WalkOff, 0, 7, 2);
        var plan = InPlayPlanner.Plan(state, "popup", "reached_on_error", "p1", Location);
        var step = GameReducer.Apply(state, plan.Event).Value;
        var play = step.NewPlays.Single();
        Assert.Equal(MoveCause.Forced, play.Moves.Single(m => m.From == 3).Cause);
        Assert.Equal(new[] { FieldPosition.CenterField }, play.Errors);
        Assert.Equal(0, step.Delta.Entries.Where(e => e.Key == StatKey.BatH).Sum(e => e.Amount));
        Assert.Equal(1, step.Delta.Entries.Where(e => e.Key == StatKey.BatAB).Sum(e => e.Amount));
        Assert.Equal(1, step.Delta.Entries.Where(e => e.Key == StatKey.BatROE).Sum(e => e.Amount));
        Assert.Equal(1, step.Delta.Entries.Where(e => e.Key == StatKey.FldE && e.Player == state.DefenderAt(state.FieldingSide, FieldPosition.CenterField)).Sum(e => e.Amount));
        Assert.Empty(play.Credits);
    }

    // ---------------------------------------------------------------- Popup / Infield Fly

    private static bool InfieldFlyConditions(int occupancy, int outs) => (occupancy & 3) == 3 && outs < 2;   // first and second (or loaded) occupied, fewer than two outs

    [Fact]
    public void Popup_templates_and_the_Infield_Fly_rule_are_kept_apart_by_an_explicit_premise()
    {
        var rows = RunAll(Root.GeneralTop, "general_top", false, [0]).Where(r => r.Batted == "popup").ToList();
        Assert.Equal(3 * 8 * 3, rows.Count);                                                               // popup: out, single, reached-on-error
        foreach (var r in rows)
        {
            var ifr = InfieldFlyConditions(r.Occupancy, r.Outs);
            switch (r.Official)
            {
                case "Out":
                    // an infielder (2B) catches it with ordinary effort: with first and second occupied and fewer than two outs this is an Infield Fly, the batter is out, the record is the same
                    Assert.Equal("2B:PO", r.Credits);
                    Assert.Equal(0, r.Runs);
                    if (r.Outs == 2)
                        Assert.Equal(r.Occupancy.BitCount(), r.Lob);                                     // the inning is over: everybody who stood on base is left
                    if (ifr)
                        Assert.Equal(r.Occupancy, r.BasesAfter);                                         // runners stay (they may advance at their own risk; the base plan does not move them)
                    break;
                case "Single":
                    Assert.Empty(r.Credits);
                    Assert.Equal("", r.Errors);
                    break;
                case "ReachedOnError":
                    Assert.Equal("CF", r.Errors);                                                       // an outfielder, never an infielder: the ball was beyond the infielders' ordinary effort
                    Assert.Empty(r.Credits);
                    break;
            }
        }

        // the premise text of the two non-out popup templates says the Infield Fly rule does not apply, the out template says when it does
        Assert.Contains("does NOT apply", InPlayTemplates.ById("ipt1.pop.single").Premise, StringComparison.Ordinal);
        Assert.Contains("no Infield Fly", InPlayTemplates.ById("ipt1.pop.roe").Premise, StringComparison.Ordinal);
        Assert.Contains("Infield Fly", InPlayTemplates.ById("ipt1.pop.out").Premise, StringComparison.Ordinal);
        Assert.True(InPlayTemplates.ById("ipt1.pop.roe").Error is FieldPosition.CenterField or FieldPosition.LeftField or FieldPosition.RightField);
        Assert.Equal(8 * 3, rows.Count(r => r.Official == "Out"));                                        // 24 popup outs
        // the situations the task names: first and second, and bases loaded, with 0 and 1 outs
        Assert.Equal(4, new[] { 3, 7 }.SelectMany(o => new[] { 0, 1 }.Select(u => (o, u))).Count(t => InfieldFlyConditions(t.o, t.u)));
        Assert.False(InfieldFlyConditions(3, 2));
        Assert.False(InfieldFlyConditions(5, 0));
    }

    [Fact]
    public void A_popup_reached_on_error_by_an_infielder_is_detected_as_a_premise_violation()
    {
        var bad = RunAll(Root.GeneralTop, "general_top", false, [0], Fault.PopRoeByInfielder);
        Assert.Contains(bad, r => !r.OracleMatch && r.Mismatch.Contains("errors", StringComparison.Ordinal));
        Assert.All(bad.Where(r => !r.OracleMatch), r => Assert.Equal("popup", r.Batted));
    }

    // ---------------------------------------------------------------- counterexamples (reducer accepts, record wrong; reducer refuses; unsupported)

    [Fact]
    public void The_unmodified_triple_in_a_tied_bases_loaded_walk_off_is_refused_by_the_reducer()
    {
        var state = InPlayStates.Make(Root.WalkOff, 0, 7, 0);
        var naive = InPlayPlanner.Plan(state, "fly_hard", "triple", "p1", Location, Fault.NoWalkOffCredit);
        var refused = GameReducer.Apply(state, naive.Event);
        Assert.False(refused.IsSuccess);
        Assert.Equal("Game.run_after_winning_run", refused.Error!.Value.Code);
        // the approved policy turns the same draw into a legal single with exactly one run
        var policy = InPlayPlanner.Plan(state, "fly_hard", "triple", "p1", Location);
        Assert.True(GameReducer.Apply(state, policy.Event).IsSuccess);
    }

    [Fact]
    public void The_reducer_accepts_a_tied_third_base_double_but_the_official_record_must_be_a_single()
    {
        var state = InPlayStates.Make(Root.WalkOff, 0, 4, 0);
        var naive = InPlayPlanner.Plan(state, "fly_hard", "double", "p1", Location, Fault.NoWalkOffCredit);
        var step = GameReducer.Apply(state, naive.Event);
        Assert.True(step.IsSuccess);                                                                        // the reducer cannot know about OBR 9.06(f)
        Assert.Equal(BatterResult.Double, step.Value.NewPlays.Single().Result);
        var row = RunOne("counterexample", true, 0, 4, 0, new Combo("fly_hard", "double", 200000), state, Fault.NoWalkOffCredit);
        Assert.False(row.OracleMatch);                                                                      // the independent policy check catches it
        Assert.Contains("official result", row.Mismatch, StringComparison.Ordinal);
        Assert.Equal("DEFECT_OR_UNRESOLVED", row.Status);
    }

    [Fact]
    public void A_batter_who_touched_third_but_is_credited_a_double_cannot_be_expressed_and_is_excluded_by_policy_D3()
    {
        var state = InPlayStates.Make(Root.WalkOff, 0, 4, 0);
        // the only way the event model lets a hit batter's own move be named is "put out beyond the base the hit gave him"; a claim that he reached third is not expressible
        var forged = new InPlayEvent("p1", BattedBallType.Line, InPlayResult.Double, false, ImmutableList.Create(new RunnerMove(3, 4), new RunnerMove(0, 3)), null, null, null, Location);
        var r = GameReducer.Apply(state, forged);
        Assert.False(r.IsSuccess);
        Assert.Equal("Game.batter_move_invalid", r.Error!.Value.Code);
        var plan = InPlayPlanner.Plan(state, "line_hard", "double", "p1", Location);
        Assert.DoesNotContain(plan.Event.MoveList, m => m.From == 0);                                       // the planner never needs the batter's move: his final base is his official base
    }

    [Fact]
    public void A_missing_or_doubled_putout_a_phantom_assist_an_unneeded_error_and_a_missing_error_are_all_caught()
    {
        foreach (var (fault, reducerCode, ball, result) in new[]
        {
            (Fault.GroundMissingPutout, "Game.out_credit_invalid", "ground_medium", "out"),
            (Fault.GroundDoublePutout, "Game.out_credit_invalid", "ground_medium", "out"),
            (Fault.RoeWithoutError, "Game.error_missing", "ground_weak", "reached_on_error"),
        })
        {
            var state = InPlayStates.Make(Root.GeneralTop, 0, 0, 0);
            var refused = GameReducer.Apply(state, InPlayPlanner.Plan(state, ball, result, "p1", Location, fault).Event);
            Assert.False(refused.IsSuccess);
            Assert.Equal(reducerCode, refused.Error!.Value.Code);
        }

        // an assist on a line-drive out is accepted by the reducer (it has no idea what was thrown) and caught only by the template check
        var s = InPlayStates.Make(Root.GeneralTop, 0, 0, 0);
        Assert.True(GameReducer.Apply(s, InPlayPlanner.Plan(s, "line_medium", "out", "p1", Location, Fault.LineOutWithAssist).Event).IsSuccess);
        var row = RunOne("counterexample", false, 0, 0, 0, new Combo("line_medium", "out", 300000), s, Fault.LineOutWithAssist);
        Assert.False(row.OracleMatch);
        Assert.Contains("credits", row.Mismatch, StringComparison.Ordinal);
        // a fielder position that does not exist in the defence cannot be credited (the designated hitter does not field)
        var dhCredit = new InPlayEvent("p1", BattedBallType.Fly, InPlayResult.Out, false, ImmutableList<RunnerMove>.Empty, ImmutableList.Create(new PositionCredit(FieldPosition.DesignatedHitter, CreditKind.Putout)), null, null, Location);
        Assert.Equal("Game.out_credit_invalid", GameReducer.Apply(s, dhCredit).Error!.Value.Code);
    }

    [Fact]
    public void A_forced_runner_that_is_not_pushed_by_a_reached_on_error_is_refused_by_the_reducer()
    {
        var state = InPlayStates.Make(Root.GeneralTop, 0, 1, 0);
        var r = GameReducer.Apply(state, InPlayPlanner.Plan(state, "ground_weak", "reached_on_error", "p1", Location, Fault.RoeForcedRunnerHolds).Event);
        Assert.False(r.IsSuccess);
        Assert.Equal("Game.base_occupied_twice", r.Error!.Value.Code);
    }

    [Fact]
    public void A_base_plan_that_invents_an_extra_advance_is_caught_even_though_the_reducer_accepts_it()
    {
        var state = InPlayStates.Make(Root.GeneralTop, 0, 4, 0);
        var plan = InPlayPlanner.Plan(state, "fly_medium", "out", "p1", Location, Fault.FlyOutRunnerScoresFromThird);
        var step = GameReducer.Apply(state, plan.Event);
        Assert.True(step.IsSuccess);                                                                        // it is a legal sacrifice fly ...
        Assert.Equal(BatterResult.SacrificeFly, step.Value.NewPlays.Single().Result);
        var row = RunOne("counterexample", false, 0, 4, 0, new Combo("fly_medium", "out", 760000), state, Fault.FlyOutRunnerScoresFromThird);
        Assert.False(row.OracleMatch);                                                                      // ... but not the base plan: whether he tags up is the advancement draw
        var honest = RunOne("counterexample", false, 0, 4, 0, new Combo("fly_medium", "out", 760000), state, Fault.None);
        Assert.True(honest.OracleMatch);
        Assert.Equal("PENDING_ADVANCEMENT", honest.Status);
        Assert.Equal("Out", honest.Official);
    }

    [Fact]
    public void The_reducer_refuses_the_malformed_runner_plans_a_wrong_template_could_produce_and_leaves_the_state_alone()
    {
        static ImmutableList<RunnerMove> L(params RunnerMove[] m) => m.ToImmutableList();
        var po2b = ImmutableList.Create(new PositionCredit(FieldPosition.SecondBase, CreditKind.Putout));
        var bat1b = ImmutableList.Create(new PositionCredit(FieldPosition.Shortstop, CreditKind.Assist), new PositionCredit(FieldPosition.FirstBase, CreditKind.Putout));
        var cases = new (string Code, int Occ, int Outs, InPlayEvent Event)[]
        {
            // a runner without a move
            ("Game.runner_move_missing", 7, 0, new InPlayEvent("p1", BattedBallType.Line, InPlayResult.Single, false, L(new RunnerMove(3, 4), new RunnerMove(2, 3)), null, null, null, Location)),
            // the same runner moved twice
            ("Game.runner_moved_twice", 1, 0, new InPlayEvent("p1", BattedBallType.Line, InPlayResult.Single, false, L(new RunnerMove(1, 2), new RunnerMove(1, 3)), null, null, null, Location)),
            // a trailing runner passes the runner ahead
            ("Game.runner_passed_runner", 3, 0, new InPlayEvent("p1", BattedBallType.Line, InPlayResult.Single, false, L(new RunnerMove(2, 2), new RunnerMove(1, 3)), null, null, null, Location)),
            // two runners on one base
            ("Game.base_occupied_twice", 3, 0, new InPlayEvent("p1", BattedBallType.Line, InPlayResult.Single, false, L(new RunnerMove(2, 3), new RunnerMove(1, 3)), null, null, null, Location)),
            // a runner moved backward
            ("Game.runner_moved_backward", 4, 0, new InPlayEvent("p1", BattedBallType.Line, InPlayResult.Single, false, L(new RunnerMove(3, 2)), null, null, null, Location)),
            // a fourth out
            ("Game.too_many_outs", 1, 2, new InPlayEvent("p1", BattedBallType.Ground, InPlayResult.Out, false, L(new RunnerMove(1, 0, MoveCause.Play, OutKind.Force, po2b)), bat1b, null, null, Location)),
            // a run on the play that makes the third out, without the order of the out and the run
            ("Game.play_order_required", 7, 2, new InPlayEvent("p1", BattedBallType.Ground, InPlayResult.Out, false, L(new RunnerMove(3, 4), new RunnerMove(2, 3), new RunnerMove(1, 2)), bat1b, null, null, Location)),
        };
        foreach (var (code, occ, outs, e) in cases)
        {
            var state = InPlayStates.Make(Root.GeneralTop, 0, occ, outs);
            var before = Digest(state);
            var r = GameReducer.Apply(state, e);
            Assert.False(r.IsSuccess, code);
            Assert.Equal(code, r.Error!.Value.Code);
            Assert.Equal(before, Digest(state));
        }
    }

    // ---------------------------------------------------------------- mutation checks of the harness itself

    [Fact]
    public void Every_injected_defect_is_detected_and_the_unmodified_planner_has_none()
    {
        var baseline = RunAll(Root.GeneralTop, "general_top", false, [0]).Concat(RunAll(Root.WalkOff, "bottom_ninth", true, [0, 1, 2, 3])).ToList();
        Assert.Equal(0, baseline.Count(r => !r.OracleMatch));
        var report = new List<string>();
        foreach (var fault in Enum.GetValues<Fault>().Where(f => f != Fault.None))
        {
            var rows = RunAll(Root.GeneralTop, "general_top", false, [0], fault).Concat(RunAll(Root.WalkOff, "bottom_ninth", true, [0, 1, 2, 3], fault)).ToList();
            var detected = rows.Count(r => !r.OracleMatch);
            report.Add($"{fault}: {detected}/{rows.Count}");
            Assert.True(detected > 0, $"{fault} was NOT detected");
            // a refused event never changes the state it was applied to
            Assert.DoesNotContain(rows, r => r.Mismatch.Contains("the state the event was applied to changed", StringComparison.Ordinal));
        }

        Assert.Equal(12, report.Count);
    }

    [Fact]
    public void Quietly_replacing_a_drawn_long_hit_by_a_single_is_caught_in_ordinary_play()
    {
        var rows = RunAll(Root.GeneralTop, "general_top", false, [0], Fault.DrawnLongHitAlwaysSingle);
        var changed = rows.Where(r => r.Drawn is "Double" or "Triple").ToList();
        Assert.NotEmpty(changed);
        Assert.All(changed, r => Assert.False(r.OracleMatch));
    }

    // ---------------------------------------------------------------- reproducibility and isolation

    [Fact]
    public void The_planner_and_the_harness_use_no_random_numbers_no_clock_and_no_id_counter()
    {
        foreach (var type in new[] { typeof(InPlayPlanner), typeof(FeasibilityHarness), typeof(InPlayStates) })
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                foreach (var p in method.GetParameters())
                    Assert.DoesNotContain("Randomness", p.ParameterType.Namespace ?? "", StringComparison.Ordinal);
        var source = File.ReadAllText(Path.Combine(FeasibilityData.Root, "tests", "StarPlayer.Simulation.Tests", "InPlayFeasibility", "InPlayTemplates.cs"));
        Assert.DoesNotContain("DateTime", source, StringComparison.Ordinal);
        Assert.DoesNotContain("using StarPlayer.Simulation.Randomness", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Random", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GameEventIds.Create", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_the_raw_results_when_asked()
    {
        var dir = Environment.GetEnvironmentVariable("STAR_FEASIBILITY_OUT");
        if (string.IsNullOrWhiteSpace(dir))
            return;
        Directory.CreateDirectory(dir);
        var general = RunAll(Root.GeneralTop, "general_top", false, [0]);
        var walkOff = RunAll(Root.WalkOff, "bottom_ninth", true, [0, 1, 2, 3]);
        var groups = new Dictionary<string, IReadOnlyList<CaseRow>> { ["general"] = general, ["walkoff"] = walkOff };
        var mirrors = new Dictionary<string, string>
        {
            ["general_bottom"] = Summarise(RunAll(Root.GeneralBottom, "general_bottom", false, [0])),
            ["general_top_no_dh"] = Summarise(RunAll(Root.GeneralTopNoDh, "general_top_no_dh", false, [0])),
            ["general_top_full_count"] = Summarise(RunAll(Root.GeneralTop, "general_top_full_count", false, [0], fullCount: true)),
            ["bottom_tenth_tie"] = Summarise(RunAll(Root.ExtraInning, "bottom_tenth", true, [0])),
        };
        var mutants = Enum.GetValues<Fault>().Where(f => f != Fault.None).ToDictionary(f => $"mutant_{f}", f =>
        {
            var rows = RunAll(Root.GeneralTop, "general_top", false, [0], f).Concat(RunAll(Root.WalkOff, "bottom_ninth", true, [0, 1, 2, 3], f)).ToList();
            return $"detected {rows.Count(r => !r.OracleMatch)} of {rows.Count} (reducer refused {rows.Count(r => !r.ReducerOk)})";
        });
        var extra = mirrors.Concat(mutants).ToDictionary(kv => kv.Key, kv => kv.Value);
        File.WriteAllText(Path.Combine(dir, "PHASE_3C2B2_FEASIBILITY_RAW.json"), ToJson("Phase 3-C2B-2-P feasibility raw results", groups, extra));
        Assert.Equal(1080, general.Count);
        Assert.Equal(4320, walkOff.Count);
        _ = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "PHASE_3C2B2_FEASIBILITY_RAW.json")));
    }
}

internal static class IntExtensions
{
    public static int BitCount(this int value) => System.Numerics.BitOperations.PopCount((uint)value);
}
