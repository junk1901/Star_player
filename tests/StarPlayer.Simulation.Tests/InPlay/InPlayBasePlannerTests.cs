using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using Root = StarPlayer.Simulation.Tests.InPlayFeasibility.InPlayStates.Root;
using States = StarPlayer.Simulation.Tests.InPlayFeasibility.InPlayStates;

namespace StarPlayer.Simulation.Tests.InPlay;

/// <summary>
/// Phase 3-C2B-2, Part A: the production base planner against HAND-DERIVED expectations (the approved 8 x 6 runner table of docs/game-engine.md section 23.3, the worked
/// walk-off examples of ADR-007, the declared advancement contexts). Every literal below was written from the policy text, not produced by the planner or the oracle.
/// </summary>
public class InPlayBasePlannerTests
{
    private static InPlayPlanFacts Plan(GameState s, string batted, string result)
    {
        var made = InPlayBasePlanner.Plan(s, batted, result);
        Assert.True(made.IsSuccess, made.Error?.Code + " " + made.Error?.Message);
        return made.Value;
    }

    private static string Moves(InPlayPlanFacts f) => ProdHarness.MovesText(f);

    // runners by occupancy mask: ground out (fewer than two outs) | any other out or a two-out out | single | double | triple or home run | reached on error
    private static readonly Dictionary<int, string[]> Table = new()
    {
        [0] = ["", "", "", "", "", ""],
        [1] = ["1>2", "1>1", "1>2", "1>3", "1>4", "1>2"],
        [2] = ["2>2", "2>2", "2>3", "2>4", "2>4", "2>2"],
        [3] = ["2>3,1>2", "2>2,1>1", "2>3,1>2", "2>4,1>3", "2>4,1>4", "2>3,1>2"],
        [4] = ["3>3", "3>3", "3>4", "3>4", "3>4", "3>3"],
        [5] = ["3>3,1>2", "3>3,1>1", "3>4,1>2", "3>4,1>3", "3>4,1>4", "3>3,1>2"],
        [6] = ["3>3,2>2", "3>3,2>2", "3>4,2>3", "3>4,2>4", "3>4,2>4", "3>3,2>2"],
        [7] = ["3>4,2>3,1>2", "3>3,2>2,1>1", "3>4,2>3,1>2", "3>4,2>4,1>3", "3>4,2>4,1>4", "3>4,2>3,1>2"],
    };

    [Fact]
    public void The_runner_table_matches_the_hand_derived_8_by_6_table()
    {
        for (var occ = 0; occ < 8; occ++)
        {
            var open = States.Make(Root.GeneralTop, 0, occ, 0);
            var twoOuts = States.Make(Root.GeneralTop, 0, occ, 2);
            var row = Table[occ];

            Assert.Equal(row[0], Moves(Plan(open, "ground_hard", "out")));        // forced runners move, the rest hold
            Assert.Equal(row[1], Moves(Plan(open, "fly_weak", "out")));           // a fly out: nobody moves
            Assert.Equal(row[1], Moves(Plan(open, "line_medium", "out")));
            Assert.Equal(row[1], Moves(Plan(open, "popup", "out")));
            Assert.Equal(row[1], Moves(Plan(twoOuts, "ground_hard", "out")));     // the batter's out is the third out: everybody holds
            Assert.Equal(row[2], Moves(Plan(open, "line_medium", "single")));
            Assert.Equal(row[3], Moves(Plan(open, "line_medium", "double")));
            Assert.Equal(row[4], Moves(Plan(open, "line_medium", "triple")));
            Assert.Equal(row[4], Moves(Plan(open, "line_hard", "home_run")));
            Assert.Equal(row[5], Moves(Plan(open, "ground_medium", "reached_on_error")));
        }
    }

    [Fact]
    public void Causes_are_forced_only_for_a_runner_pushed_by_a_reaching_batter_and_play_otherwise()
    {
        var loaded = States.Make(Root.GeneralTop, 0, 7, 0);
        var roe = Plan(loaded, "ground_medium", "reached_on_error");
        Assert.All(roe.Moves, m => Assert.Equal(MoveCause.Forced, m.Cause));

        var third = States.Make(Root.GeneralTop, 0, 4, 0);
        Assert.Equal(MoveCause.Play, Plan(third, "ground_medium", "reached_on_error").Moves.Single().Cause);   // not forced: holds
        Assert.All(Plan(loaded, "line_medium", "double").Moves, m => Assert.Equal(MoveCause.Play, m.Cause));
        Assert.All(Plan(loaded, "ground_hard", "out").Moves, m => Assert.Equal(MoveCause.Play, m.Cause));
    }

    [Fact]
    public void Every_existing_runner_has_exactly_one_move_in_scoring_order_and_the_runner_keys_come_from_the_state()
    {
        var loaded = States.Make(Root.GeneralTop, 0, 7, 0);
        var plan = Plan(loaded, "line_medium", "single");

        Assert.Equal([3, 2, 1], plan.Moves.Select(m => m.From));                                    // lead runner first
        Assert.Equal([loaded.Bases.Third!.RunnerKey, loaded.Bases.Second!.RunnerKey, loaded.Bases.First!.RunnerKey], plan.Moves.Select(m => m.RunnerKey));
        Assert.Equal([loaded.Bases.Third.Player, loaded.Bases.Second.Player, loaded.Bases.First.Player], plan.Moves.Select(m => m.Runner));
        Assert.Equal(3, plan.Moves.Select(m => m.RunnerKey).Distinct().Count());
    }

    [Theory]
    [InlineData("ground_hard", "out", 0)]
    [InlineData("line_medium", "single", 1)]
    [InlineData("line_medium", "double", 2)]
    [InlineData("line_medium", "triple", 3)]
    [InlineData("line_hard", "home_run", 4)]
    [InlineData("ground_medium", "reached_on_error", 1)]
    public void The_batter_final_base_is_the_credited_base(string batted, string result, int dest)
    {
        var plan = Plan(States.Make(Root.GeneralTop, 0, 0, 0), batted, result);

        Assert.Equal(dest, plan.Official.BatterDestination);
        Assert.Equal(plan.Drawn.Id, plan.Official.Id);                      // an ordinary game never changes the drawn result
        Assert.Equal(InPlayWalkOffEffect.None, plan.WalkOffEffect);
        Assert.Equal("none", plan.RecordChangeReason);
    }

    [Fact]
    public void The_event_has_no_batter_move_no_sequence_no_out_kind_and_no_invented_credit()
    {
        var loaded = States.Make(Root.GeneralTop, 0, 7, 0);
        foreach (var (batted, result) in new[] { ("ground_hard", "out"), ("line_medium", "single"), ("fly_hard", "triple"), ("ground_medium", "reached_on_error"), ("fly_hard", "home_run") })
        {
            var plan = Plan(loaded, batted, result);
            var evt = InPlayBasePlanner.BuildEvent(plan, "e1", null);

            Assert.Equal(3, evt.MoveList.Count);                              // three runners, no batter move
            Assert.All(evt.MoveList, m => { Assert.Null(m.Sequence); Assert.Null(m.OutKind); Assert.Null(m.Credits); Assert.Null(m.OutAt); });
            Assert.Null(evt.BatterSequence);
            Assert.False(evt.Sacrifice);
            Assert.Equal(plan.Official.Batted, evt.Batted);
            Assert.Equal(plan.Official.Result, evt.Result);
        }
    }

    [Theory]
    [InlineData("ground_hard", "out", "SS:A,1B:PO", "")]
    [InlineData("line_medium", "out", "SS:PO", "")]
    [InlineData("fly_weak", "out", "CF:PO", "")]
    [InlineData("popup", "out", "2B:PO", "")]
    [InlineData("ground_medium", "reached_on_error", "", "SS")]
    [InlineData("line_weak", "reached_on_error", "", "SS")]
    [InlineData("fly_medium", "reached_on_error", "", "CF")]
    [InlineData("popup", "reached_on_error", "", "CF")]
    [InlineData("line_medium", "single", "", "")]
    [InlineData("popup", "single", "", "")]
    [InlineData("fly_hard", "triple", "", "")]
    [InlineData("fly_hard", "home_run", "", "")]
    public void Fielding_credits_and_errors_follow_the_templates_and_a_hit_has_none(string batted, string result, string credits, string error)
    {
        var plan = Plan(States.Make(Root.GeneralTop, 0, 0, 0), batted, result);
        var evt = InPlayBasePlanner.BuildEvent(plan, "e1", null);

        Assert.Equal(credits, ProdHarness.CreditText(evt.BatterCreditList));
        Assert.Equal(error, string.Join(",", evt.ErrorList.Select(e => e.ToString() switch { "Shortstop" => "SS", "CenterField" => "CF", var o => o })));
    }

    [Fact]
    public void A_pop_up_hit_or_error_is_beyond_the_infielders_ordinary_effort_and_never_an_infielder_error()
    {
        var s = States.Make(Root.GeneralTop, 0, 3, 0);               // runners on first and second, nobody out: an Infield Fly situation if an infielder could catch it

        var single = Plan(s, "popup", "single");
        var roe = Plan(s, "popup", "reached_on_error");
        var caught = Plan(s, "popup", "out");

        Assert.Contains("pop_beyond_infielder_ordinary_effort", single.Official.VirtualFacts);
        Assert.Contains("pop_beyond_infielder_ordinary_effort", roe.Official.VirtualFacts);
        Assert.Equal(FieldPosition.CenterField, roe.Official.Error);                          // the error is the outfielder's, never changed to hide an Infield Fly
        Assert.NotEqual(FieldPosition.SecondBase, roe.Official.Error);
        Assert.Empty(single.Official.Credits);
        Assert.DoesNotContain("pop_beyond_infielder_ordinary_effort", caught.Official.VirtualFacts);
        Assert.Equal("2B:PO", ProdHarness.CreditText(caught.Official.Credits));
        Assert.Equal("2>2,1>1", Moves(caught));                                                // runners hold on a caught pop-up
    }

    // ---------------------------------------------------------------- the walk-off record (walkoff-credit-v1), worked examples of the policy

    [Fact]
    public void Tied_with_a_runner_on_third_a_drawn_double_is_officially_a_single()
    {
        var s = States.Make(Root.WalkOff, 0, 4, 0);
        var plan = Plan(s, "line_medium", "double");

        Assert.Equal(InPlayResult.Double, plan.Drawn.Result);
        Assert.Equal(InPlayResult.Single, plan.Official.Result);
        Assert.Equal("ipt1.line.single", plan.Official.Id);
        Assert.Equal(InPlayWalkOffEffect.CreditReduced, plan.WalkOffEffect);
        Assert.Equal("walkoff_credit_reduced", plan.RecordChangeReason);
        Assert.Equal("3>4", Moves(plan));
        Assert.Equal(1, plan.Runs);
        Assert.Equal(1, plan.Official.BatterDestination);
        Assert.Equal(3, plan.WinningRunner!.StartingBase);
        Assert.Equal(s.Bases.Third!.RunnerKey, plan.WinningRunner.RunnerKey);
        Assert.False(plan.AdvancementRequired);
    }

    [Fact]
    public void Tied_with_the_bases_loaded_a_drawn_double_is_officially_a_single_and_the_other_runners_stay_on_base()
    {
        var plan = Plan(States.Make(Root.WalkOff, 0, 7, 0), "line_medium", "double");

        Assert.Equal(InPlayResult.Single, plan.Official.Result);
        Assert.Equal("3>4,2>3,1>2", Moves(plan));                         // the single's moves, not the double's
        Assert.Equal(1, plan.Runs);
        Assert.Equal(3, plan.WinningRunner!.StartingBase);
    }

    [Fact]
    public void Tied_with_a_runner_on_second_a_drawn_triple_is_officially_a_double()
    {
        var plan = Plan(States.Make(Root.WalkOff, 0, 2, 0), "line_medium", "triple");

        Assert.Equal(InPlayResult.Triple, plan.Drawn.Result);
        Assert.Equal(InPlayResult.Double, plan.Official.Result);
        Assert.Equal("2>4", Moves(plan));
        Assert.Equal(2, plan.Official.BatterDestination);
        Assert.Equal(2, plan.WinningRunner!.StartingBase);
        Assert.Equal(InPlayWalkOffEffect.CreditReduced, plan.WalkOffEffect);
    }

    [Fact]
    public void Tied_with_a_runner_on_first_a_drawn_triple_stays_a_triple()
    {
        var plan = Plan(States.Make(Root.WalkOff, 0, 1, 0), "line_medium", "triple");

        Assert.Equal(InPlayResult.Triple, plan.Official.Result);
        Assert.Equal("1>4", Moves(plan));
        Assert.Equal(3, plan.Official.BatterDestination);
        Assert.Equal(1, plan.WinningRunner!.StartingBase);
        Assert.Equal(InPlayWalkOffEffect.GameEnds, plan.WalkOffEffect);
        Assert.Equal("none", plan.RecordChangeReason);
    }

    [Fact]
    public void Two_behind_with_the_bases_loaded_a_drawn_triple_stays_a_triple_and_scores_exactly_the_needed_runs()
    {
        var plan = Plan(States.Make(Root.WalkOff, 2, 7, 1), "fly_hard", "triple");

        Assert.Equal(InPlayResult.Triple, plan.Official.Result);
        Assert.Equal(3, plan.RunsNeeded);
        Assert.Equal(3, plan.Runs);
        Assert.Equal(1, plan.WinningRunner!.StartingBase);
        Assert.Equal(InPlayWalkOffEffect.GameEnds, plan.WalkOffEffect);
    }

    [Fact]
    public void A_drawn_single_an_out_and_a_reached_on_error_are_never_reduced()
    {
        var s = States.Make(Root.WalkOff, 0, 4, 0);

        Assert.Equal(InPlayResult.Single, Plan(s, "line_medium", "single").Official.Result);
        Assert.Equal(InPlayResult.Out, Plan(s, "line_medium", "out").Official.Result);
        Assert.Equal(InPlayResult.Out, Plan(s, "ground_hard", "out").Official.Result);
        var roe = Plan(States.Make(Root.WalkOff, 0, 7, 2), "ground_medium", "reached_on_error");
        Assert.Equal(InPlayResult.ReachedOnError, roe.Official.Result);
        Assert.Equal("3>4,2>3,1>2", Moves(roe));                           // the forced run scores
        Assert.Equal(InPlayWalkOffEffect.GameEnds, roe.WalkOffEffect);
    }

    [Fact]
    public void An_out_of_the_park_home_run_that_ends_the_game_scores_everyone_and_is_never_converted()
    {
        var plan = Plan(States.Make(Root.WalkOff, 0, 7, 0), "fly_hard", "home_run");

        Assert.Equal(InPlayResult.HomeRun, plan.Official.Result);
        Assert.Equal("3>4,2>4,1>4", Moves(plan));
        Assert.Equal(4, plan.Runs);
        Assert.Equal(4, plan.Official.BatterDestination);
        Assert.Equal(InPlayWalkOffEffect.GameEnds, plan.WalkOffEffect);
        Assert.Equal(3, plan.WinningRunner!.StartingBase);                          // one run is needed: the lead runner (from third) scores it, the others and the batter follow
    }

    [Fact]
    public void A_walk_off_home_run_with_nobody_on_is_won_by_the_batter()
    {
        var plan = Plan(States.Make(Root.WalkOff, 0, 0, 0), "fly_hard", "home_run");

        Assert.Equal(InPlayResult.HomeRun, plan.Official.Result);
        Assert.Equal(1, plan.Runs);
        Assert.Equal(InPlayWalkOffEffect.GameEnds, plan.WalkOffEffect);
        Assert.Equal(InPlayScorerKind.BatterRunner, plan.WinningRunner!.Kind);   // the batter-runner scores the winning run (never the same value as "no winning scorer")
    }

    [Fact]
    public void A_walk_off_context_that_scores_nothing_leaves_the_drawn_result_and_ends_nothing()
    {
        var plan = Plan(States.Make(Root.WalkOff, 0, 0, 0), "line_medium", "double");

        Assert.Equal(InPlayResult.Double, plan.Official.Result);
        Assert.Equal(InPlayWalkOffEffect.None, plan.WalkOffEffect);
        Assert.True(plan.WalkOffContext);
        Assert.Equal(1, plan.RunsNeeded);
        Assert.Null(plan.WinningRunner);
        Assert.Equal(0, plan.Runs);
    }

    [Fact]
    public void An_ordinary_inning_and_the_top_of_the_ninth_have_no_walk_off_context()
    {
        Assert.False(Plan(States.Make(Root.GeneralBottom, 0, 4, 0), "line_medium", "double").WalkOffContext);   // bottom of the 3rd
        Assert.False(Plan(States.Make(Root.GeneralTop, 0, 4, 0), "line_medium", "double").WalkOffContext);
        var third = Plan(States.Make(Root.GeneralBottom, 0, 4, 0), "line_medium", "double");
        Assert.Equal(InPlayResult.Double, third.Official.Result);                                            // a runner on third and a drawn double stay a double in the 3rd
        Assert.Equal("none", third.RecordChangeReason);
    }

    [Fact]
    public void A_last_half_third_out_in_a_lost_game_ends_the_half_inning_not_the_walk_off()
    {
        var plan = Plan(States.Make(Root.WalkOff, 1, 7, 2), "ground_hard", "out");

        Assert.True(plan.EndsHalfInning);
        Assert.False(plan.WalkOffEndsGame);
        Assert.Equal("3>3,2>2,1>1", Moves(plan));                           // the third-out play holds everybody: no run, no order problem
        Assert.Equal(0, plan.Runs);
    }

    // ---------------------------------------------------------------- the declared advancement contexts and the composite marker

    [Theory]
    [InlineData(1, "single", "single_runner_on_first", false)]
    [InlineData(2, "single", "single_runner_on_second", false)]
    [InlineData(3, "single", "single_runner_on_first+single_runner_on_second", true)]
    [InlineData(5, "single", "single_runner_on_first", true)]                  // the runner on third is not part of any single context but is another runner
    [InlineData(6, "single", "single_runner_on_second", true)]
    [InlineData(7, "single", "single_runner_on_first+single_runner_on_second", true)]
    [InlineData(4, "single", "", false)]
    [InlineData(0, "single", "", false)]
    [InlineData(1, "double", "double_runner_on_first", false)]
    [InlineData(3, "double", "double_runner_on_first", true)]
    [InlineData(5, "double", "double_runner_on_first", true)]
    [InlineData(7, "double", "double_runner_on_first", true)]
    [InlineData(2, "double", "", false)]
    [InlineData(6, "double", "", false)]
    [InlineData(1, "triple", "", false)]
    [InlineData(7, "home_run", "", false)]
    [InlineData(7, "reached_on_error", "", false)]
    public void The_four_declared_advancement_contexts_and_the_composite_marker(int occupancy, string result, string keys, bool composite)
    {
        var plan = Plan(States.Make(Root.GeneralTop, 0, occupancy, 0), result == "home_run" ? "line_hard" : "line_medium", result);   // line_medium has no home run (0 ppm)

        Assert.Equal(keys, string.Join("+", plan.AdvancementKeys));
        Assert.Equal(composite, plan.Composite);
        Assert.Equal(keys.Length > 0, plan.AdvancementRequired);
    }

    [Theory]
    [InlineData(4, 0, true, false)]
    [InlineData(4, 1, true, false)]
    [InlineData(4, 2, false, false)]                                     // the fly out is the third out: no extra-base decision
    [InlineData(5, 1, true, true)]
    [InlineData(6, 0, true, true)]
    [InlineData(7, 1, true, true)]
    [InlineData(1, 0, false, false)]
    [InlineData(0, 0, false, false)]
    public void A_fly_out_with_a_runner_on_third_before_two_outs_is_the_fourth_context(int occupancy, int outs, bool required, bool composite)
    {
        var plan = Plan(States.Make(Root.GeneralTop, 0, occupancy, outs), "fly_medium", "out");

        Assert.Equal(required, plan.AdvancementRequired);
        Assert.Equal(composite, plan.Composite);
        Assert.Equal(required ? "fly_out_runner_on_third" : "", string.Join("+", plan.AdvancementKeys));
    }

    [Fact]
    public void Only_a_fly_ball_out_opens_the_fourth_context()
    {
        var s = States.Make(Root.GeneralTop, 0, 4, 0);

        Assert.True(Plan(s, "fly_medium", "out").AdvancementRequired);
        Assert.False(Plan(s, "line_medium", "out").AdvancementRequired);
        Assert.False(Plan(s, "popup", "out").AdvancementRequired);
        Assert.False(Plan(s, "ground_hard", "out").AdvancementRequired);
    }

    [Fact]
    public void A_game_ending_walk_off_keeps_the_applicable_keys_but_is_not_pending()
    {
        var plan = Plan(States.Make(Root.WalkOff, 0, 5, 0), "line_medium", "single");     // runners on first and third, tied: the runner on third wins the game

        Assert.True(plan.WalkOffEndsGame);
        Assert.Equal("single_runner_on_first", string.Join("+", plan.AdvancementKeys));
        Assert.False(plan.AdvancementRequired);
    }

    [Fact]
    public void A_tied_single_with_a_runner_on_second_does_not_end_the_game_and_is_pending()
    {
        var plan = Plan(States.Make(Root.WalkOff, 0, 2, 0), "line_medium", "single");

        Assert.Equal(0, plan.Runs);                                         // no final score is invented for the unresolved runner
        Assert.False(plan.WalkOffEndsGame);
        Assert.True(plan.AdvancementRequired);
        Assert.Equal("single_runner_on_second", string.Join("+", plan.AdvancementKeys));
        Assert.Equal("2>3", Moves(plan));
    }

    // ---------------------------------------------------------------- the pre-play situation kept for the audit

    [Fact]
    public void The_pre_play_situation_is_recorded_with_runner_keys_in_base_order()
    {
        var s = States.Make(Root.WalkOff, 2, 5, 1);
        var plan = Plan(s, "line_medium", "single");

        Assert.Equal((9, InningHalf.Bottom, 1, 2, 0), (plan.PrePlay.Inning, plan.PrePlay.Half, plan.PrePlay.Outs, plan.PrePlay.AwayRuns, plan.PrePlay.HomeRuns));
        Assert.Equal([1, 3], plan.PrePlay.Runners.Select(r => r.Base));
        Assert.Equal([s.Bases.First!.RunnerKey, s.Bases.Third!.RunnerKey], plan.PrePlay.Runners.Select(r => r.RunnerKey));
    }

    // ---------------------------------------------------------------- refusals

    [Fact]
    public void A_game_that_is_not_in_progress_or_a_missing_state_is_refused()
    {
        var s = States.Make(Root.GeneralTop, 0, 0, 0);

        Assert.Equal("InPlayPlan.state_missing", InPlayBasePlanner.Plan(null!, "fly_hard", "out").Error!.Value.Code);
        Assert.Equal("InPlayPlan.state_invalid", InPlayBasePlanner.Plan(s with { Status = GameStatus.Final }, "fly_hard", "out").Error!.Value.Code);
    }

    [Fact]
    public void Planning_changes_nothing_and_is_deterministic()
    {
        var s = States.Make(Root.WalkOff, 0, 7, 1);
        var before = InPlayFeasibility.FeasibilityHarness.Digest(s);

        var a = Plan(s, "fly_hard", "triple");
        var b = Plan(s, "fly_hard", "triple");

        Assert.Equal(before, InPlayFeasibility.FeasibilityHarness.Digest(s));
        Assert.Equal(Moves(a), Moves(b));
        Assert.Equal((a.Official.Id, a.Runs, a.WalkOffEffect, a.AdvancementRequired), (b.Official.Id, b.Runs, b.WalkOffEffect, b.AdvancementRequired));
    }
}
