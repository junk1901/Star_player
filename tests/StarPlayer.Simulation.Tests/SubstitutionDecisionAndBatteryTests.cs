using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>Legal substitutions, DH and two-way cases, pitcher decisions, and actual catcher / battery exposure.</summary>
public class SubstitutionDecisionAndBatteryTests
{
    // ---- pinch hitters, pinch runners, defensive moves

    [Fact]
    public void A_pinch_hitter_takes_the_batters_turn_and_the_replaced_player_can_never_return()
    {
        var s = new Script();

        s.Sub(SubstitutionKind.PinchHit, TeamSide.Away, "ab1");

        Assert.Equal("ab1", s.State.Away.SlotByOrder(1).Player.Value);
        Assert.Equal(FieldPosition.CenterField, s.State.Away.SlotByOrder(1).Position); // keeps the replaced player's position
        Assert.DoesNotContain(P("ab1"), s.State.Away.Bench);
        Assert.Contains(P("a1"), s.State.Away.Removed);
        s.Walk();
        Assert.Equal((1, 0), (s.Stat("ab1", StatKey.BatBB), s.Stat("a1", StatKey.BatBB)));
        Assert.Equal("Game.substitute_not_on_bench", s.Try(new SubstitutionEvent("x", SubstitutionKind.PinchHit, TeamSide.Away, P("a1"))).Error!.Value.Code);
        Assert.Equal(SubstitutionKind.PinchHit, s.State.Substitutions.Single().Kind);
    }

    [Fact]
    public void A_pinch_hitter_may_come_in_inside_a_plate_appearance_after_a_take_and_inherits_the_count()
    {
        var s = new Script().Take(PitchOutcome.Ball);

        s.Sub(SubstitutionKind.PinchHit, TeamSide.Away, "ab1");

        Assert.Equal((PaStage.Decision, 1, 0), (s.State.Stage, s.State.Count.Balls, s.State.Count.Strikes));
        s.Take(PitchOutcome.Ball).Take(PitchOutcome.Ball).Take(PitchOutcome.Ball);
        Assert.Equal(1, s.Stat("ab1", StatKey.BatBB));
        Assert.Equal(1, s.Stat("ab1", StatKey.BatPA));
    }

    [Fact]
    public void Substitutions_are_rejected_for_the_wrong_team_during_pitch_sequences_and_for_unavailable_players()
    {
        var s = new Script();

        Assert.Equal("Game.pinch_hit_team", s.Try(new SubstitutionEvent("x1", SubstitutionKind.PinchHit, TeamSide.Home, P("hb1"))).Error!.Value.Code);
        Assert.Equal("Game.substitute_not_on_bench", s.Try(new SubstitutionEvent("x2", SubstitutionKind.PinchHit, TeamSide.Away, P("nobody"))).Error!.Value.Code);
        Assert.Equal("Game.substitute_not_on_bench", s.Try(new SubstitutionEvent("x3", SubstitutionKind.PinchHit, TeamSide.Away, P("h1"))).Error!.Value.Code);
        s.Approach();
        Assert.Equal("Game.substitution_not_legal", s.Try(new SubstitutionEvent("x4", SubstitutionKind.PinchHit, TeamSide.Away, P("ab1"))).Error!.Value.Code);
    }

    [Fact]
    public void A_pinch_runner_takes_over_the_base_the_lineup_slot_and_the_original_pitchers_responsibility()
    {
        var s = new Script().Single(); // a1 on first, charged to hp

        s.Sub(SubstitutionKind.PinchRun, TeamSide.Away, "ab2", @base: 1);

        Assert.Equal("ab2", s.State.Bases.First!.Player.Value);
        Assert.Equal("hp", s.State.Bases.First.ResponsiblePitcher.Value);
        Assert.Equal("ab2", s.State.Away.SlotByOrder(1).Player.Value);
        Assert.Contains(P("a1"), s.State.Away.Removed);
        s.Double(M(1, 4));
        Assert.Equal((1, 0), (s.Stat("ab2", StatKey.RunR), s.Stat("a1", StatKey.RunR)));
        Assert.Equal(1, s.Stat("hp", StatKey.PitchR));
        Assert.Equal("Game.pinch_run_base", s.Try(new SubstitutionEvent("x", SubstitutionKind.PinchRun, TeamSide.Away, P("ab3"), 3)).Error!.Value.Code);
    }

    [Fact]
    public void A_defensive_substitute_receives_later_fielding_credits_and_a_current_batter_or_runner_cannot_be_replaced_that_way()
    {
        var s = new Script();
        s.Sub(SubstitutionKind.DefensiveSub, TeamSide.Home, "hb3", slot: 7);
        s.Approach().InPlay(BattedBallType.Line, InPlayResult.Out, batterCredits: [Po(FieldPosition.ThirdBase)]);

        Assert.Equal((1, 0), (s.Stat("hb3", StatKey.FldPO), s.Stat("h7", StatKey.FldPO)));
        Assert.Equal("Game.use_pinch_substitution", s.Try(new SubstitutionEvent("x1", SubstitutionKind.DefensiveSub, TeamSide.Away, P("ab1"), Slot: 2)).Error!.Value.Code); // a2 is the batter now due
        s.Approach().InPlay(BattedBallType.Line, InPlayResult.Single);
        Assert.Equal("Game.use_pinch_substitution", s.Try(new SubstitutionEvent("x2", SubstitutionKind.DefensiveSub, TeamSide.Away, P("ab1"), Slot: 2)).Error!.Value.Code); // a2 is on first
        Assert.True(s.Try(new SubstitutionEvent("x3", SubstitutionKind.DefensiveSub, TeamSide.Away, P("ab1"), Slot: 7)).IsSuccess);
    }

    [Fact]
    public void Position_swap_exchanges_two_fielders_but_never_involves_the_pitcher_or_the_dh_slot()
    {
        var s = new Script();

        s.Sub(SubstitutionKind.PositionSwap, TeamSide.Home, slot: 6, slot2: 7);
        s.Approach().InPlay(BattedBallType.Fly, InPlayResult.Out, batterCredits: [Po(FieldPosition.LeftField)]);

        Assert.Equal((FieldPosition.ThirdBase, FieldPosition.LeftField), (s.State.Home.SlotByOrder(6).Position, s.State.Home.SlotByOrder(7).Position));
        Assert.Equal(1, s.Stat("h7", StatKey.FldPO)); // the old third baseman now plays left
        Assert.Equal("Game.position_swap_invalid", s.Try(new SubstitutionEvent("x1", SubstitutionKind.PositionSwap, TeamSide.Home, Slot: 5, Slot2: 6)).Error!.Value.Code);
        Assert.Equal("Game.substitution_slot_invalid", s.Try(new SubstitutionEvent("x2", SubstitutionKind.PositionSwap, TeamSide.Home, Slot: 6, Slot2: 6)).Error!.Value.Code);
    }

    // ---- pitching changes

    [Fact]
    public void A_pitching_change_records_the_stint_with_the_score_and_runners_at_entry_and_the_old_pitcher_cannot_return()
    {
        var s = new Script().HomeRun().Walk();                 // away leads 1-0 and a2 is on first

        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");

        var usage = s.State.Home.PitcherUsages;
        Assert.Equal(["hp", "hb1"], usage.Select(u => u.Pitcher.Value));
        Assert.Equal((-1, 1, 2), (usage[1].EnteredWithLead, usage[1].EnteredRunnersOnBase, usage[1].EntrySequence));
        Assert.Equal("hb1", s.State.Home.Pitcher.Value);
        Assert.Contains(P("hp"), s.State.Home.Removed);
        Assert.Equal("Game.pitcher_already_used", s.Try(new SubstitutionEvent("x1", SubstitutionKind.PitchingChange, TeamSide.Home, P("hp"))).Error!.Value.Code);
        Assert.Equal("Game.pitching_change_invalid", s.Try(new SubstitutionEvent("x2", SubstitutionKind.PitchingChange, TeamSide.Home, P("hb1"))).Error!.Value.Code);
        Assert.Equal("Game.pitching_change_team", s.Try(new SubstitutionEvent("x3", SubstitutionKind.PitchingChange, TeamSide.Away, P("ab1"))).Error!.Value.Code);
        Assert.Equal("Game.pitcher_not_eligible", s.Try(new SubstitutionEvent("x4", SubstitutionKind.PitchingChange, TeamSide.Home, P("a1"))).Error!.Value.Code);
    }

    [Fact]
    public void Without_a_dh_a_pinch_hit_for_the_pitcher_leaves_an_incomplete_defense_until_a_pitching_change()
    {
        var s = new Script(TestRules.NoDh).ThreeUp(2);        // away's first inning done; a4 leads off the second
        s.Out();                                                // a4 out; the pitcher ap is due fifth
        Assert.Equal("ap", s.State.Away.CurrentBatterSlot.Player.Value);

        s.Sub(SubstitutionKind.PinchHit, TeamSide.Away, "ab1");
        s.Out().Out();                                          // ab1 and a6 make it three outs

        Assert.Equal((2, InningHalf.Bottom), (s.State.Inning, s.State.Half));
        var legal = GameReducer.LegalActions(s.State);
        Assert.False(legal.DefenseComplete);
        Assert.Equal([GameActionKind.Substitution], legal.Actions);
        Assert.Equal("Game.defense_incomplete", s.Try(new ApproachEvent("x", ApproachKind.Normal)).Error!.Value.Code);

        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Away, "ab2");

        Assert.True(GameReducer.LegalActions(s.State).DefenseComplete);
        Assert.Equal("ab2", s.State.Away.SlotAt(FieldPosition.Pitcher)!.Player.Value);
        Assert.Contains(P("ab1"), s.State.Away.Removed);
        Assert.Contains(P("ap"), s.State.Away.Removed);
        s.Approach();
    }

    [Fact]
    public void Without_a_dh_a_lineup_fielder_may_become_the_pitcher_by_swapping_positions_with_the_old_pitcher()
    {
        var s = new Script(TestRules.NoDh);
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "h6"); // the left fielder takes the mound

        Assert.Equal("h6", s.State.Home.Pitcher.Value);
        Assert.Equal(("h6", FieldPosition.Pitcher), (s.State.Home.SlotOf(Script.P("h6"))!.Player.Value, s.State.Home.SlotOf(Script.P("h6"))!.Position));
        Assert.Equal(FieldPosition.LeftField, s.State.Home.SlotOf(Script.P("hp"))!.Position); // the old pitcher goes to left field and stays in the game
        Assert.True(GameReducer.LegalActions(s.State).DefenseComplete);
        Assert.Empty(s.State.Home.Removed);
    }

    // ---- two-way players and the DH

    private static Script TwoWayAway(GameRuleSnapshot? rules = null) =>
        new(rules ?? TestRules.Dh, Script.Team("a", true, P("a5")), Script.Team("h", true));

    [Fact]
    public void A_starting_pitcher_may_also_be_the_dh_only_when_the_profile_has_the_two_way_rule()
    {
        var rule = TestRules.Dh;
        var away = Script.Team("a", true, P("a5"));
        var home = Script.Team("h", true);

        Assert.True(GameReducer.Start(new GameStart("g", rule, away, home)).IsSuccess);
        Assert.Equal("Game.pitcher_in_lineup", GameReducer.Start(new GameStart("g", TestRules.DhNoTwoWay, away, home)).Error!.Value.Code);
        Assert.Equal("Game.pitcher_in_lineup", GameReducer.Start(new GameStart("g", rule, Script.Team("a", true, P("a4")), home)).Error!.Value.Code); // a4 plays first base, not DH
    }

    [Fact]
    public void A_two_way_player_keeps_batting_and_pitching_records_apart_and_appears_in_the_game_once()
    {
        var s = TwoWayAway().ThreeUp(1);                    // top 1; a5 now pitches to the home batters
        s.Single();                                          // h1 singles off a5
        Assert.Equal(1, s.Stat("a5", StatKey.PitchH));
        s.Strikeout().Strikeout().Strikeout();               // h2, h3, h4 strike out against a5
        s.Out();                                             // top 2: a4 out
        s.Single();                                          // a5 bats and singles while he is the pitcher
        s.Strikeout().Strikeout();                           // a6, a7 -> three outs

        Assert.Equal((1, 1, 1), (s.Stat("a5", StatKey.BatPA), s.Stat("a5", StatKey.BatAB), s.Stat("a5", StatKey.BatH)));
        Assert.Equal((4, 3, 1), (s.Stat("a5", StatKey.PitchBF), s.Stat("a5", StatKey.PitchK), s.Stat("a5", StatKey.PitchH)));
        var box = BoxScoreBuilder.Build(s.State);
        var appearances = box.Appearances.Where(a => a.Player == P("a5")).ToList();
        Assert.Single(appearances);
        Assert.True(appearances[0].InBattingOrder && appearances[0].Pitched);
        Assert.Single(box.Players, p => p.Player == P("a5")); // one stat line with independent batting and pitching keys
    }

    [Fact]
    public void A_two_way_dh_who_is_pitching_cannot_be_pinch_hit_for_but_can_leave_the_mound_and_keep_hitting()
    {
        var s = TwoWayAway().ThreeUp(1).Strikeout().Strikeout().Strikeout(); // away batted, home batted three times against a5
        s.Out();                                                              // top 2: a4 out, a5 is due
        Assert.Equal("a5", s.State.Away.CurrentBatterSlot.Player.Value);

        Assert.Equal("Game.two_way_dh_replaced_while_pitching", s.Try(new SubstitutionEvent("x1", SubstitutionKind.PinchHit, TeamSide.Away, P("ab1"))).Error!.Value.Code);
        Assert.Equal("Game.two_way_dh_replaced_while_pitching", s.Try(new SubstitutionEvent("x2", SubstitutionKind.DefensiveSub, TeamSide.Away, P("ab1"), Slot: 5)).Error!.Value.Code);

        s.Out().Out();                                                        // a5 and a6 out: bottom 2
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Away, "ab1");

        Assert.Equal("ab1", s.State.Away.Pitcher.Value);
        Assert.Equal(FieldPosition.DesignatedHitter, s.State.Away.SlotOf(P("a5"))!.Position); // still in the lineup as the DH
        Assert.DoesNotContain(P("a5"), s.State.Away.Removed);
        Assert.Equal("Game.pitcher_already_used", s.Try(new SubstitutionEvent("x3", SubstitutionKind.PitchingChange, TeamSide.Away, P("a5"))).Error!.Value.Code);
    }

    [Fact]
    public void A_dh_who_walks_to_the_mound_ends_the_dh_role_or_is_refused_but_never_keeps_the_role_unless_he_is_the_two_way_starter()
    {
        // OBR 5.11(a)(12): the DH assuming a defensive position ends the DH role; the 5.11(b) exception belongs to the starting pitcher listed twice (until 2026-10-11 any DH
        // could pitch and keep the role under the two-way rule: evidence row C32)
        var forfeit = new Script(TestRules.DhForfeit);
        forfeit.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "h5");
        Assert.Equal(("h5", FieldPosition.Pitcher, false), (forfeit.State.Home.Pitcher.Value, forfeit.State.Home.SlotOf(P("h5"))!.Position, forfeit.State.Home.DhActive));
        Assert.Contains(P("hp"), forfeit.State.Home.Removed);

        foreach (var rules in new[] { TestRules.Dh, TestRules.DhNoTwoWay })
        {
            var denied = new Script(rules);
            var before = denied.State;
            Assert.Equal("Game.pitcher_not_eligible", denied.Try(new SubstitutionEvent("x", SubstitutionKind.PitchingChange, TeamSide.Home, P("h5"))).Error!.Value.Code);
            Assert.Equal("Game.pitcher_not_eligible", denied.Try(new SubstitutionEvent("y", SubstitutionKind.PitchingChange, TeamSide.Home, P("h4"))).Error!.Value.Code); // a fielder cannot jump to the mound under the DH
            Assert.Same(before, denied.State);
        }
    }

    // ---- pitcher decisions

    private static string Who(BoxScore box, DecisionKind kind) => box.Decisions.SingleOrDefault(d => d.Kind == kind)?.Pitcher.Value ?? "-";

    [Fact]
    public void Complete_game_gives_the_starter_the_win_and_the_pitcher_charged_with_the_go_ahead_run_the_loss_and_no_save()
    {
        var s = new Script().HomeRun().Outs(3).ThreeUp(17).Finish();

        Assert.Equal(("ap", "hp", "-"), (Who(s.Box, DecisionKind.Win), Who(s.Box, DecisionKind.Loss), Who(s.Box, DecisionKind.Save)));
        Assert.Equal(27, s.Stat("ap", StatKey.PitchOuts));
    }

    [Fact]
    public void A_starter_with_too_few_outs_loses_the_win_to_the_relief_pitcher_who_recorded_the_most_outs()
    {
        var s = new Script().HomeRun().Outs(3).ThreeUp(4);          // ap works bottoms 1 and 2: 6 outs
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Away, "ab1");
        s.ThreeUp(4);                                               // ab1 works bottoms 3 and 4: 6 outs
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Away, "ab2");
        s.ThreeUp(9).Finish();                                      // ab2 works bottoms 5-9: 15 outs

        Assert.Equal(("ab2", "hp", "-"), (Who(s.Box, DecisionKind.Win), Who(s.Box, DecisionKind.Loss), Who(s.Box, DecisionKind.Save)));
    }

    [Fact]
    public void A_closer_who_holds_a_small_lead_for_three_outs_gets_the_save_and_the_starter_keeps_the_win()
    {
        var s = new Script().HomeRun().Outs(3).ThreeUp(12);         // ap has 18 outs after the sixth bottom
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Away, "ab1");
        s.ThreeUp(5).Finish();

        Assert.Equal(("ap", "hp", "ab1"), (Who(s.Box, DecisionKind.Win), Who(s.Box, DecisionKind.Loss), Who(s.Box, DecisionKind.Save)));
    }

    [Fact]
    public void No_save_when_the_lead_is_large_and_the_stint_short()
    {
        var s = new Script().Walk().Walk().Walk().HomeRun().Outs(3).ThreeUp(11);   // four-run lead, ap has 18 outs
        s.ThreeUp(1);
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Away, "ab1");
        s.ThreeUp(4);                                                               // ab1: bottoms 7 and 8
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Away, "ab2");
        s.ThreeUp(1).Finish();                                                      // ab2: bottom 9, three outs

        Assert.Equal(("ap", "hp", "-"), (Who(s.Box, DecisionKind.Win), Who(s.Box, DecisionKind.Loss), Who(s.Box, DecisionKind.Save)));
        Assert.Equal((4, 0), (s.State.AwayRuns, s.State.HomeRuns));
    }

    [Fact]
    public void The_loss_goes_to_the_pitcher_responsible_for_the_go_ahead_run_even_when_a_reliever_gave_up_the_homer()
    {
        var s = new Script().ThreeUp(4);                            // two scoreless innings each
        s.Single();                                                 // a7 reaches against hp
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");
        s.HomeRun(M(1, 4));                                         // a8 homers off hb1: a7 (hp's runner) scores first, then a8
        s.Outs(3).ThreeUp(13).Finish();

        Assert.Equal((2, 0), (s.State.AwayRuns, s.State.HomeRuns));
        Assert.Equal(("ap", "hp"), (Who(s.Box, DecisionKind.Win), Who(s.Box, DecisionKind.Loss)));
    }

    // ---- catcher and battery exposure

    [Fact]
    public void A_catcher_change_mid_inning_splits_pitches_outs_and_battery_pairs_by_who_was_actually_behind_the_plate()
    {
        var s = new Script().Strikeout();                            // h9 catches a1's three strikes and the strikeout out
        s.Sub(SubstitutionKind.DefensiveSub, TeamSide.Home, "hb1", slot: 9);
        s.Out().Out();                                               // hb1 catches two balls in play and two outs

        var box = BoxScoreBuilder.Build(s.State);
        var h9 = box.CatcherExposures.Single(c => c.Catcher == P("h9"));
        var hb1 = box.CatcherExposures.Single(c => c.Catcher == P("hb1"));
        Assert.Equal((3, 1), (h9.PitchesReceived, h9.OutsWhileCatching));
        Assert.Equal((2, 2), (hb1.PitchesReceived, hb1.OutsWhileCatching));
        var first = box.BatteryExposures.Single(b => b.Catcher == P("h9") && b.Pitcher == P("hp"));
        var second = box.BatteryExposures.Single(b => b.Catcher == P("hb1") && b.Pitcher == P("hp"));
        Assert.Equal((3, 1, 1, 1), (first.Pitches, first.Outs, first.BattersFaced, first.SharedAppearances));
        Assert.Equal((2, 2, 2, 1), (second.Pitches, second.Outs, second.BattersFaced, second.SharedAppearances));
        Assert.Equal(2, box.BatteryExposures.Count);
        Assert.Contains(box.Appearances, a => a.Player == P("hb1") && a.InBattingOrder);
    }

    [Fact]
    public void Battery_exposure_is_unchanged_by_replaying_events_and_by_finalizing_twice()
    {
        var s = new Script().HomeRun().Outs(3).ThreeUp(17);
        var before = BoxScoreBuilder.Build(s.State).BatteryExposures;

        foreach (var e in s.Log)
            GameReducer.Apply(s.State, e);
        s.Finish();
        var first = s.State.Final!;
        var again = GameReducer.Finalize(s.State).Value;

        Assert.Equal(before, first.BoxScore.BatteryExposures);
        Assert.Same(first, again.Game);
        Assert.Equal(first.BoxScore.BatteryExposures, again.Game.BoxScore.BatteryExposures);
        Assert.Equal(27, first.BoxScore.BatteryExposures.Where(b => b.Team == TeamSide.Home).Sum(b => b.Outs)); // nine home-fielding halves of three outs
    }

    [Fact]
    public void The_catcher_exposure_counts_stolen_bases_caught_stealing_and_passed_balls_against_him()
    {
        var s = new Script().Single();
        s.Runner(RunnerPlayKind.Steal, M(1, 2, MoveCause.Steal));
        s.Approach().Pitch(PitchOutcome.Ball, PitchMishap.PassedBall, [M(2, 3, MoveCause.PassedBall)]);
        s.Swing().Swing().Swing();
        s.Single(M(3, 4));
        s.Runner(RunnerPlayKind.Steal, M(1, 0, MoveCause.Steal, OutKind.Tag, A(FieldPosition.Catcher), Po(FieldPosition.SecondBase)));

        var exposure = BoxScoreBuilder.Build(s.State).CatcherExposures.Single(c => c.Catcher == P("h9"));

        Assert.Equal((1, 1, 1), (exposure.StolenBasesAllowed, exposure.CaughtStealing, exposure.PassedBalls));
        Assert.Equal(2, s.Stat("hp", StatKey.PitchOuts)); // the strikeout and the caught stealing
    }
}
