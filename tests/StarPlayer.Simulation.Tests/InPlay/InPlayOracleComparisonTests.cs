using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Tests.InPlayFeasibility;
using Root = StarPlayer.Simulation.Tests.InPlayFeasibility.InPlayStates.Root;

namespace StarPlayer.Simulation.Tests.InPlay;

/// <summary>
/// Phase 3-C2B-2, Part A: the PRODUCTION base planner against the 5,400 recorded feasibility cases. Three independent sources are compared field by field: the Python oracle
/// (tests/Fixtures/phase3c2b2p_inplay_oracle.json), the recorded raw results of the feasibility package (docs/reports/PHASE_3C2B2_FEASIBILITY_RAW.json) and the real
/// <see cref="GameReducer.Apply"/>. The classification counts are not targets: a planner that was bent to hit them would still be compared per case.
/// </summary>
public class InPlayOracleComparisonTests
{
    private static readonly Lazy<List<ProdRow>> General = new(() => ProdHarness.RunAll(Root.GeneralTop, false, [0]));
    private static readonly Lazy<List<ProdRow>> WalkOff = new(() => ProdHarness.RunAll(Root.WalkOff, true, [0, 1, 2, 3]));

    private static readonly Lazy<JsonElement> Raw = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(FeasibilityData.Root, "docs", "reports", "PHASE_3C2B2_FEASIBILITY_RAW.json"))).RootElement);

    private static string Report(IEnumerable<ProdRow> rows) => string.Join("\n", rows.Where(r => r.Mismatches.Count > 0).Take(10).Select(r => $"{r.Key}: {string.Join("; ", r.Mismatches)}"));

    [Fact]
    public void All_1080_ordinary_cases_are_planned_applied_and_equal_to_the_oracle()
    {
        var rows = General.Value;

        Assert.Equal(1080, rows.Count);
        Assert.True(rows.All(r => r.Mismatches.Count == 0), Report(rows));
        Assert.Equal(1080, rows.Count(r => r.ReducerOk));
        Assert.Equal(780, rows.Count(r => r.Status == "LEGAL_BASE"));
        Assert.Equal(300, rows.Count(r => r.Status == "PENDING_ADVANCEMENT"));
        Assert.Equal(0, rows.Count(r => r.Status is "NEEDS_WALKOFF_POLICY" or "DEFECT_OR_UNRESOLVED"));
    }

    [Fact]
    public void All_4320_walk_off_cases_are_planned_applied_and_equal_to_the_oracle()
    {
        var rows = WalkOff.Value;

        Assert.Equal(4320, rows.Count);
        Assert.True(rows.All(r => r.Mismatches.Count == 0), Report(rows));
        Assert.Equal(4320, rows.Count(r => r.ReducerOk));
        Assert.Equal(3114, rows.Count(r => r.Status == "LEGAL_BASE"));
        Assert.Equal(1014, rows.Count(r => r.Status == "PENDING_ADVANCEMENT"));
        Assert.Equal(192, rows.Count(r => r.Status == "NEEDS_WALKOFF_POLICY"));
        Assert.Equal(0, rows.Count(r => r.Status == "DEFECT_OR_UNRESOLVED"));
    }

    [Fact]
    public void The_5400_cases_cover_45_combinations_and_all_19_templates()
    {
        var rows = General.Value.Concat(WalkOff.Value).ToList();

        Assert.Equal(5400, rows.Count);
        Assert.Equal(45, rows.Select(r => r.Key[(r.Key.LastIndexOf('|') + 1)..]).Distinct().Count());
        Assert.Equal(19, rows.Select(r => r.Facts!.Drawn.Id).Distinct().Count());
        Assert.Equal(19, InPlayTemplateCatalog.All.Length);
        Assert.Equal(InPlayTemplateCatalog.All.Select(t => t.Id).OrderBy(i => i, StringComparer.Ordinal), rows.Select(r => r.Facts!.Drawn.Id).Distinct().OrderBy(i => i, StringComparer.Ordinal));
    }

    [Fact]
    public void The_192_walk_off_credit_reductions_are_exactly_double_to_single_96_triple_to_double_48_and_triple_to_single_48()
    {
        var reduced = WalkOff.Value.Where(r => r.Facts!.WalkOffEffect == InPlayWalkOffEffect.CreditReduced).ToList();

        Assert.Equal(192, reduced.Count);
        Assert.Equal(96, reduced.Count(r => r.Facts!.Drawn.Result == InPlayResult.Double && r.Facts.Official.Result == InPlayResult.Single));
        Assert.Equal(48, reduced.Count(r => r.Facts!.Drawn.Result == InPlayResult.Triple && r.Facts.Official.Result == InPlayResult.Double));
        Assert.Equal(48, reduced.Count(r => r.Facts!.Drawn.Result == InPlayResult.Triple && r.Facts.Official.Result == InPlayResult.Single));
        Assert.All(reduced, r =>
        {
            Assert.True(r.GameOver);
            Assert.Equal("walkoff_credit_reduced", r.Facts!.RecordChangeReason);
            Assert.NotNull(r.Facts.WinningRunner);
            Assert.Equal(r.Facts.RunsNeeded, r.Runs);                       // the winning run is the last run scored: exactly the needed runs
            Assert.Equal(r.Facts.Official.BatterDestination, r.Facts.Official.Result switch { InPlayResult.Single => 1, InPlayResult.Double => 2, _ => 3 });
        });
    }

    [Fact]
    public void Pending_and_composite_counts_match_the_recorded_findings()
    {
        Assert.Equal(300, General.Value.Count(r => r.Pending));
        Assert.Equal(210, General.Value.Count(r => r.Pending && r.Composite));
        Assert.Equal(1014, WalkOff.Value.Count(r => r.Pending));
        Assert.Equal(654, WalkOff.Value.Count(r => r.Pending && r.Composite));
        // a walk-off play that ended the game is never pending, whatever contexts apply
        Assert.DoesNotContain(WalkOff.Value, r => r.Pending && r.Facts!.WalkOffEndsGame);
        Assert.DoesNotContain(WalkOff.Value, r => r.Pending && r.Facts!.EndsHalfInning);
    }

    [Theory]
    [InlineData("general")]
    [InlineData("walkoff")]
    public void Every_row_equals_the_recorded_raw_feasibility_result_field_by_field(string group)
    {
        var mine = (group == "general" ? General.Value : WalkOff.Value).ToDictionary(r => r.Key, StringComparer.Ordinal);
        var raw = Raw.Value.GetProperty(group).GetProperty("rows").EnumerateArray().ToList();
        var problems = new List<string>();

        Assert.Equal(mine.Count, raw.Count);
        foreach (var r in raw)
        {
            var key = r.GetProperty("k").GetString()!;
            var p = mine[key];
            void Check(string name, object? actual, object? want)
            {
                if (!Equals(actual, want))
                    problems.Add($"{key} {name}: actual {actual} recorded {want}");
            }

            Check("drawn", p.Facts!.Drawn.Result.ToString(), r.GetProperty("drawn").GetString());
            Check("official", p.Facts.Official.Result.ToString(), r.GetProperty("official").GetString());
            Check("template", p.Facts.Official.Id, r.GetProperty("tpl").GetString());
            Check("moves", p.Moves, r.GetProperty("mv").GetString());
            if (r.TryGetProperty("win", out var win))
                Check("winning runner base", p.Facts.WinningRunner?.StartingBase, win.GetInt32());
            Check("reducer ok", p.ReducerOk, r.GetProperty("ok").GetBoolean());
            Check("runs", p.Runs, r.GetProperty("runs").GetInt32());
            Check("outs", p.OutsAfter, r.GetProperty("outs").GetInt32());
            Check("half over", p.HalfOver, r.GetProperty("half").GetBoolean());
            Check("game over", p.GameOver, r.GetProperty("over").GetBoolean());
            Check("bases", p.BasesAfter, r.GetProperty("bases").GetInt32());
            Check("lob", p.Lob, r.GetProperty("lob").GetInt32());
            Check("rbi", p.Rbi, r.GetProperty("rbi").GetInt32());
            Check("credits", p.Credits, r.GetProperty("cr").GetString());
            Check("errors", p.Errors, r.GetProperty("e").GetString());
            Check("status", p.Status, r.GetProperty("st").GetString());
            Check("walk-off", ProdHarness.WalkOffText(p.Facts.WalkOffEffect), r.GetProperty("wo").GetString());
            Check("pending", p.Pending, r.GetProperty("pend").GetBoolean());
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(10)));
    }

    [Fact]
    public void A_winning_runner_is_recorded_for_every_non_batter_walk_off_and_is_the_last_scoring_runner()
    {
        foreach (var r in WalkOff.Value.Where(r => r.Facts!.WalkOffEndsGame))
        {
            var scorers = r.Facts!.Moves.Where(m => m.To == 4).ToList();
            if (scorers.Count >= r.Facts.RunsNeeded)
            {
                var w = r.Facts.WinningRunner!;                                        // the need-th scorer, lead runner first
                Assert.Equal(InPlayScorerKind.ExistingRunner, w.Kind);
                Assert.Equal((scorers[r.Facts.RunsNeeded - 1].RunnerKey, scorers[r.Facts.RunsNeeded - 1].Runner, scorers[r.Facts.RunsNeeded - 1].From), (w.RunnerKey, w.Player, w.StartingBase));
            }
            else
                Assert.Equal(InPlayScorerKind.BatterRunner, r.Facts.WinningRunner!.Kind);   // the batter scored the winning run (out-of-the-park home run)
        }
    }

    // ---------------------------------------------------------------- mirror contexts: the plan must not depend on the half, the lineup rules, the count or the extra inning

    [Theory]
    [InlineData("GeneralBottom")]
    [InlineData("GeneralTopNoDh")]
    public void The_plan_does_not_depend_on_the_batting_side_or_the_designated_hitter(string root)
    {
        var rows = ProdHarness.RunAll(Enum.Parse<Root>(root), false, [0]);

        Assert.Equal(1080, rows.Count);
        Assert.True(rows.All(r => r.Mismatches.Count == 0), Report(rows));
        Assert.Equal(780, rows.Count(r => r.Status == "LEGAL_BASE"));
        Assert.Equal(300, rows.Count(r => r.Status == "PENDING_ADVANCEMENT"));
    }

    [Fact]
    public void The_plan_does_not_depend_on_the_count()
    {
        var rows = ProdHarness.RunAll(Root.GeneralTop, false, [0], fullCount: true);

        Assert.True(rows.All(r => r.Mismatches.Count == 0), Report(rows));
        Assert.Equal(General.Value.Select(r => r.Moves + "|" + r.Status), rows.Select(r => r.Moves + "|" + r.Status));
    }

    [Fact]
    public void A_tied_bottom_of_the_tenth_is_planned_like_a_tied_bottom_of_the_ninth()
    {
        var rows = ProdHarness.RunAll(Root.ExtraInning, true, [0]);
        var ninth = WalkOff.Value.Where(r => r.Key.StartsWith("w|0|", StringComparison.Ordinal)).ToList();

        Assert.Equal(1080, rows.Count);
        Assert.True(rows.All(r => r.Mismatches.Count == 0), Report(rows));
        Assert.Equal(ninth.Select(r => r.Key + r.Moves + r.Status), rows.Select(r => r.Key + r.Moves + r.Status));
    }
}
