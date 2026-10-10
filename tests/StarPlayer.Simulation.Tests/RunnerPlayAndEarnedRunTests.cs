using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>Hand-computed oracles for non-PA plays, WP vs PB, dropped third strikes, responsibility and earned runs.</summary>
public class RunnerPlayAndEarnedRunTests
{
    // ---- steals, pickoffs, balks

    [Fact]
    public void Stolen_base_moves_the_runner_up_one_base_and_charges_the_catcher_side_without_a_plate_appearance()
    {
        var s = new Script().Single();

        s.Runner(RunnerPlayKind.Steal, M(1, 2, MoveCause.Steal));

        Assert.Equal("a1", s.State.Bases.Second!.Player.Value);
        Assert.Equal((1, 1), (s.Stat("a1", StatKey.RunSB), s.Stat("h9", StatKey.CatSBA)));
        Assert.Equal(1, s.Stat("a1", StatKey.BatPA)); // the steal added no plate appearance
        Assert.Equal(PlayKind.Steal, s.State.Plays.Last().Kind);
        Assert.Equal(0, s.State.Outs);
    }

    [Fact]
    public void Stealing_home_scores_a_run_with_a_stolen_base_and_no_RBI()
    {
        var s = new Script().Triple();

        s.Runner(RunnerPlayKind.Steal, M(3, 4, MoveCause.Steal));

        Assert.Equal(1, s.State.AwayRuns);
        Assert.Equal((1, 1), (s.Stat("a1", StatKey.RunSB), s.Stat("a1", StatKey.RunR)));
        Assert.Equal(1, s.Stat("hp", StatKey.PitchR));
        Assert.Equal(0, s.State.Stats.Where(e => e.Key == StatKey.BatRBI).Sum(e => e.Amount));
        Assert.Equal(MoveCause.Steal, s.State.Plays.Last().Runs[0].Cause);
    }

    [Fact]
    public void Caught_stealing_credits_the_catcher_assist_the_infielder_putout_and_the_runner_CS()
    {
        var s = new Script().Single();

        s.Runner(RunnerPlayKind.Steal, M(1, 0, MoveCause.Steal, OutKind.Tag, A(FieldPosition.Catcher), Po(FieldPosition.SecondBase)));

        Assert.Equal(1, s.State.Outs);
        Assert.Equal((1, 1, 1, 1), (s.Stat("a1", StatKey.RunCS), s.Stat("h9", StatKey.CatCS), s.Stat("h9", StatKey.FldA), s.Stat("h8", StatKey.FldPO)));
        Assert.Equal(0, s.Stat("a1", StatKey.RunSB));
        Assert.Equal(PlayKind.CaughtStealing, s.State.Plays.Last().Kind);
        Assert.Equal(1, s.Stat("hp", StatKey.PitchOuts));
    }

    [Fact]
    public void Caught_stealing_for_the_third_out_ends_the_inning_and_the_batter_leads_off_next_time_with_a_fresh_count()
    {
        var s = new Script().Single().FlyOut(M(1, 1)).FlyOut(M(1, 1)); // two outs, a1 on first, a4 due
        s.Approach().Ball();
        Assert.Equal((1, 0), (s.State.Count.Balls, s.State.Count.Strikes));

        s.Runner(RunnerPlayKind.Steal, M(1, 0, MoveCause.Steal, OutKind.Tag, A(FieldPosition.Catcher), Po(FieldPosition.SecondBase)));

        Assert.Equal((1, InningHalf.Bottom, 0), (s.State.Inning, s.State.Half, s.State.Outs));
        Assert.Equal(PitchCountState.Zero, s.State.Count);
        s.ThreeUp();
        Assert.Equal((2, InningHalf.Top), (s.State.Inning, s.State.Half));
        Assert.Equal("a4", s.State.Batting.CurrentBatterSlot.Player.Value); // his plate appearance never finished
        Assert.Equal(PaStage.Start, s.State.Stage);
        Assert.Equal(0, s.Stat("a4", StatKey.BatPA));
    }

    [Fact]
    public void Pickoff_retires_the_runner_with_a_pitcher_assist_and_a_first_baseman_putout()
    {
        var s = new Script().Single();

        s.Runner(RunnerPlayKind.Pickoff, M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Pitcher), Po(FieldPosition.FirstBase)));

        Assert.Equal((1, 1, 1), (s.Stat("a1", StatKey.RunPicked), s.Stat("hp", StatKey.FldA), s.Stat("h4", StatKey.FldPO)));
        Assert.Equal(1, s.State.Outs);
        Assert.Equal(PlayKind.Pickoff, s.State.Plays.Last().Kind);
        var invalid = new Script().Single();
        Assert.Equal("Game.pickoff_invalid", invalid.Try(new RunnerPlayEvent("x", RunnerPlayKind.Pickoff, [M(1, 2)])).Error!.Value.Code);
    }

    [Fact]
    public void Balk_advances_every_runner_one_base_charges_the_pitcher_and_a_runner_from_third_scores()
    {
        var s = new Script().Triple();

        s.Runner(RunnerPlayKind.Balk, M(3, 4, MoveCause.Balk));

        Assert.Equal(1, s.State.AwayRuns);
        Assert.Equal((1, 1), (s.Stat("hp", StatKey.PitchBK), s.Stat("hp", StatKey.PitchR)));
        Assert.Equal(PlayKind.Balk, s.State.Plays.Last().Kind);

        var partial = new Script().Double().Walk(); // a1 second, a2 first
        Assert.Equal("Game.balk_invalid", partial.Try(new RunnerPlayEvent("x", RunnerPlayKind.Balk, [M(2, 3, MoveCause.Balk)])).Error!.Value.Code);
        Assert.Equal("Game.balk_invalid", partial.Try(new RunnerPlayEvent("y", RunnerPlayKind.Balk, [M(2, 4, MoveCause.Balk), M(1, 2, MoveCause.Balk)])).Error!.Value.Code);
        Assert.True(partial.Try(new RunnerPlayEvent("z", RunnerPlayKind.Balk, [M(2, 3, MoveCause.Balk), M(1, 2, MoveCause.Balk)])).IsSuccess);
        Assert.Equal(("a2", "a1"), (partial.State.Bases.Second!.Player.Value, partial.State.Bases.Third!.Player.Value));
    }

    [Fact]
    public void Runner_plays_without_a_runner_or_move_are_rejected_without_changing_state()
    {
        var s = new Script();
        var before = s.State;

        Assert.Equal("Game.empty_play", s.Try(new RunnerPlayEvent("x", RunnerPlayKind.Steal)).Error!.Value.Code);
        Assert.Equal("Game.move_source_invalid", s.Try(new RunnerPlayEvent("y", RunnerPlayKind.Steal, [M(1, 2, MoveCause.Steal)])).Error!.Value.Code);
        Assert.Same(before, s.State);
    }

    // ---- wild pitch vs passed ball

    [Fact]
    public void Wild_pitch_and_passed_ball_are_recorded_separately_to_the_pitcher_and_the_catcher()
    {
        var wp = new Script().Single();
        wp.Approach().Pitch(PitchOutcome.Ball, PitchMishap.WildPitch, [M(1, 2, MoveCause.WildPitch)]);
        var pb = new Script().Single();
        pb.Approach().Pitch(PitchOutcome.CalledStrike, PitchMishap.PassedBall, [M(1, 2, MoveCause.PassedBall)]);

        Assert.Equal((1, 0, PlayKind.WildPitch), (wp.Stat("hp", StatKey.PitchWP), wp.Stat("h9", StatKey.FldPB), wp.State.Plays.Last().Kind));
        Assert.Equal((0, 1, PlayKind.PassedBall), (pb.Stat("hp", StatKey.PitchWP), pb.Stat("h9", StatKey.FldPB), pb.State.Plays.Last().Kind));
        Assert.Equal("a1", wp.State.Bases.Second!.Player.Value);
        Assert.Equal((PaStage.InSequence, 1, 0), (wp.State.Stage, wp.State.Count.Balls, wp.State.Count.Strikes)); // the plate appearance goes on
        Assert.Equal(MoveCause.WildPitch, wp.State.Plays.Last().Moves.Single().Cause);
        Assert.Equal(MoveCause.PassedBall, pb.State.Plays.Last().Moves.Single().Cause);
    }

    [Fact]
    public void Mishap_rules_reject_mismatched_causes_empty_mishaps_and_stray_moves()
    {
        var s = new Script().Single().Approach();

        Assert.Equal("Game.mishap_without_move", s.Try(new PitchEvent("x1", new PitchSpec(PitchOutcome.Ball, PitchMishap.WildPitch))).Error!.Value.Code);
        Assert.Equal("Game.moves_without_mishap", s.Try(new PitchEvent("x2", new PitchSpec(PitchOutcome.Ball, PitchMishap.WildPitch, [M(1, 2, MoveCause.PassedBall)]))).Error!.Value.Code);
        Assert.Equal("Game.moves_without_mishap", s.Try(new PitchEvent("x3", new PitchSpec(PitchOutcome.Ball, PitchMishap.None, [M(1, 2, MoveCause.WildPitch)]))).Error!.Value.Code);
        Assert.Equal("Game.mishap_on_foul", s.Try(new PitchEvent("x4", new PitchSpec(PitchOutcome.Foul, PitchMishap.WildPitch, [M(1, 2, MoveCause.WildPitch)]))).Error!.Value.Code);
        Assert.Equal("Game.mishap_conflict", s.Try(new PitchEvent("x5", new PitchSpec(PitchOutcome.SwingingStrike, PitchMishap.WildPitch, [M(1, 2, MoveCause.WildPitch)], PitchMishap.WildPitch))).Error!.Value.Code);
        Assert.Equal(PitchCountState.Zero, s.State.Count);
        Assert.Equal(0, s.Stat("hp", StatKey.PitchPitches) - 1); // only the earlier single's pitch exists
    }

    // ---- dropped third strike

    [Fact]
    public void Dropped_third_strike_with_a_wild_pitch_puts_the_batter_on_first_with_a_strikeout_and_a_wild_pitch()
    {
        var s = new Script().Approach().Swing().Swing();

        s.Pitch(PitchOutcome.SwingingStrike, dropped: PitchMishap.WildPitch);

        Assert.Equal("a1", s.State.Bases.First!.Player.Value);
        Assert.Equal(0, s.State.Outs);
        Assert.Equal((1, 1, 1, 0), (s.Stat("a1", StatKey.BatSO), s.Stat("a1", StatKey.BatAB), s.Stat("hp", StatKey.PitchK), s.Stat("hp", StatKey.PitchOuts)));
        Assert.Equal((1, 0), (s.Stat("hp", StatKey.PitchWP), s.Stat("h9", StatKey.FldPO)));
        Assert.Equal(BatterResult.StrikeoutReachedBase, s.State.Plays.Last().Result);
    }

    [Fact]
    public void Dropped_third_strike_with_a_passed_ball_charges_the_catcher_not_the_pitcher()
    {
        var s = new Script().Approach().Swing().Swing();

        s.Pitch(PitchOutcome.SwingingStrike, dropped: PitchMishap.PassedBall);

        Assert.Equal((1, 0, 1), (s.Stat("h9", StatKey.FldPB), s.Stat("hp", StatKey.PitchWP), s.Stat("hp", StatKey.PitchK)));
    }

    [Fact]
    public void Dropped_third_strike_needs_first_base_open_or_two_outs_and_can_be_a_throw_out_with_credits()
    {
        var closed = new Script().Single().Approach().Swing().Swing();
        Assert.Equal("Game.dropped_third_not_allowed", closed.Try(new PitchEvent("x", new PitchSpec(PitchOutcome.SwingingStrike, DroppedThirdStrike: PitchMishap.WildPitch))).Error!.Value.Code);

        var twoOuts = new Script().Single().FlyOut(M(1, 1)).FlyOut(M(1, 1)).Approach().Swing().Swing();
        twoOuts.Pitch(PitchOutcome.SwingingStrike, dropped: PitchMishap.WildPitch);
        Assert.Equal(("a4", "a1"), (twoOuts.State.Bases.First!.Player.Value, twoOuts.State.Bases.Second!.Player.Value)); // a1 was forced up

        var thrownOut = new Script().Approach().Swing().Swing();
        thrownOut.Pitch(PitchOutcome.SwingingStrike, ks: [A(FieldPosition.Catcher), Po(FieldPosition.FirstBase)]);
        Assert.Equal((1, 1, 0), (thrownOut.Stat("h9", StatKey.FldA), thrownOut.Stat("h4", StatKey.FldPO), thrownOut.Stat("h9", StatKey.FldPO)));
        Assert.Equal(1, thrownOut.State.Outs);
    }

    // ---- earned runs

    private static Script HalfWithRunnerOnThirdAndAMishapRun(PitchMishap mishap)
    {
        var s = new Script().Triple();                                  // a1 on third
        var cause = mishap == PitchMishap.WildPitch ? MoveCause.WildPitch : MoveCause.PassedBall;
        s.Approach().Pitch(PitchOutcome.Ball, mishap, [M(3, 4, cause)]); // a2's plate appearance: the run scores on the pitch
        s.Swing().Swing().Swing();                                       // 1-0, then three swings: strikeout
        return s.Outs(2);
    }

    [Fact]
    public void A_run_scoring_on_a_wild_pitch_is_earned()
    {
        var s = HalfWithRunnerOnThirdAndAMishapRun(PitchMishap.WildPitch);

        Assert.Equal((1, 1, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER), s.Stat("hp", StatKey.PitchWP)));
        Assert.Equal(InningHalf.Bottom, s.State.Half);
    }

    [Fact]
    public void A_run_scoring_on_a_passed_ball_is_unearned_but_still_charged_to_the_pitcher_as_a_run()
    {
        var s = HalfWithRunnerOnThirdAndAMishapRun(PitchMishap.PassedBall);

        Assert.Equal((1, 0, 0, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER), s.Stat("hp", StatKey.PitchWP), s.Stat("h9", StatKey.FldPB)));
    }

    [Fact]
    public void An_error_that_would_have_ended_the_inning_makes_every_later_run_unearned()
    {
        var s = new Script().Strikeout().Strikeout();                                                  // two outs
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.ReachedOnError, errors: [FieldPosition.ThirdBase]); // a3 reaches on E5
        s.HomeRun(M(1, 4));                                                                             // a4 homers: two runs
        s.Strikeout();

        Assert.Equal(2, s.State.AwayRuns);
        Assert.Equal((2, 0, 2), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER), s.Stat("a4", StatKey.BatRBI)));
        Assert.Equal(1, s.Stat("h5", StatKey.FldE) + s.Stat("h7", StatKey.FldE)); // the third baseman (h7) is charged once
    }

    [Fact]
    public void The_same_inning_without_the_error_gives_two_earned_runs()
    {
        var s = new Script().Strikeout().Strikeout().Walk();
        s.HomeRun(M(1, 4));
        s.Strikeout();

        Assert.Equal((2, 2), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER)));
    }

    [Fact]
    public void A_runner_who_reached_on_error_scores_unearned_while_the_walked_runner_ahead_of_him_scores_earned()
    {
        var s = new Script().Walk();                                                                    // a1 walks (earned runner)
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.ReachedOnError, [M(1, 2)], errors: [FieldPosition.Shortstop]); // a2 reaches on E6, a1 to second
        s.Single(M(2, 4), M(1, 3));                                                                     // a3 singles: a1 scores, a2 to third
        s.Strikeout().Strikeout().Strikeout();

        Assert.Equal(1, s.State.AwayRuns);
        Assert.Equal((1, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER))); // without the error a1 still scores on the single
        Assert.Equal(2, s.Team(TeamSide.Away, StatKey.TeamLOB));
    }

    [Fact]
    public void A_run_after_the_reconstructed_third_out_is_unearned_even_though_the_real_inning_was_still_alive()
    {
        var s = new Script().Walk();
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.ReachedOnError, [M(1, 2)], errors: [FieldPosition.Shortstop]); // a2 on E6
        s.Strikeout().Strikeout();                                                                       // real outs: 2; reconstructed: error out + 2 = 3
        s.Single(M(2, 4), M(1, 3));                                                                      // a1 scores after the reconstructed third out
        s.Strikeout();

        Assert.Equal((1, 0), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER)));
    }

    [Fact]
    public void Home_run_by_a_batter_after_an_inherited_error_runner_keeps_the_batters_run_unearned_only_when_the_inning_is_over()
    {
        // error first, then clean outs: the reconstructed inning (E out + 2 more) is not over until the third real out
        var s = new Script();
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.ReachedOnError, errors: [FieldPosition.Shortstop]); // a1 reaches
        s.Strikeout();                                                                                    // a2 K: reconstructed outs 2
        s.HomeRun(M(1, 4));                                                                               // a3 HR: a1 + a3 score (both before the reconstructed third out)
        s.Strikeout().Strikeout();

        Assert.Equal(2, s.State.AwayRuns);
        Assert.Equal((2, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER))); // a1 (error runner) unearned, a3's own HR run earned
    }

    [Fact]
    public void A_runner_who_took_extra_bases_on_an_error_does_not_score_in_the_reconstruction_so_his_run_is_unearned()
    {
        var s = new Script().Single();                                                     // a1 on first
        s.Approach().InPlay(BattedBallType.Line, InPlayResult.Single, [M(1, 3, MoveCause.ErrorExtraBase)], errors: [FieldPosition.RightField]); // a2 singles, a1 takes third on E9
        s.Single(M(3, 4), M(1, 2));                                                        // a3 singles: a1 scores, a2 to second
        s.Strikeout().Strikeout().Strikeout();

        Assert.Equal((1, 1), (s.Stat("h3", StatKey.FldE), s.Stat("hp", StatKey.PitchR)));
        Assert.Equal(0, s.Stat("hp", StatKey.PitchER)); // reconstructed: a1 is forced only to second, and a single moves him one base to third
    }

    [Fact]
    public void A_runner_saved_from_an_out_by_an_error_is_out_in_the_reconstruction()
    {
        var s = new Script().Single();                                                     // a1 on first
        s.Ok(new RunnerPlayEvent(s.Id(), RunnerPlayKind.Other, [M(1, 2, MoveCause.ErrorSavedOut)], [FieldPosition.Shortstop]));
        s.Single(M(2, 4));                                                                 // a2 singles and a1 scores from second
        s.Strikeout().Strikeout().Strikeout();

        Assert.Equal((1, 0, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER), s.Stat("h2", StatKey.FldE)));
        Assert.Equal(0, s.Stat("a1", StatKey.RunSB) + s.Stat("a1", StatKey.RunCS)); // neither a stolen base nor a caught stealing
        Assert.Equal(1, s.Team(TeamSide.Away, StatKey.TeamLOB));
    }

    // ---- responsibility across pitching changes

    [Fact]
    public void Inherited_runners_are_charged_to_the_pitcher_who_put_them_on_and_the_reliever_only_for_his_own()
    {
        var s = new Script().Single().Single(M(1, 2));                  // hp allows a1 (second) and a2 (first)
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");
        s.Double(M(2, 4), M(1, 4));                                     // a3: both inherited runners score
        s.Single(M(2, 4));                                              // a4: a3 (hb1's runner) scores
        s.Strikeout().Strikeout().Strikeout();

        Assert.Equal(3, s.State.AwayRuns);
        Assert.Equal((2, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR)));
        Assert.Equal((2, 1), (s.Stat("hp", StatKey.PitchER), s.Stat("hb1", StatKey.PitchER)));
        Assert.Equal((2, 5), (s.Stat("hp", StatKey.PitchBF), s.Stat("hb1", StatKey.PitchBF)));
        Assert.Equal((0, 3), (s.Stat("hp", StatKey.PitchOuts), s.Stat("hb1", StatKey.PitchOuts)));
        Assert.Equal(1, s.Team(TeamSide.Away, StatKey.TeamLOB));
        Assert.Equal(2, s.Stat("a3", StatKey.BatRBI));
    }

    [Theory]
    [InlineData(0, "hp", "hb1")] // three balls and no strike when relieved: the walk is the first pitcher's (9.16(h)(1))
    [InlineData(1, "hp", "hb1")] // three balls and one strike: the first pitcher's
    [InlineData(2, "hp", "hb1")] // three balls and two strikes: the first pitcher's too (9.16(h)(1) lists 3-2; until 2026-10-11 this was charged to the reliever)
    public void A_walk_after_a_mid_count_relief_is_charged_by_the_count_the_reliever_inherited(int strikes, string chargedWith, string notCharged)
    {
        var s = new Script();
        s.Take(PitchOutcome.Ball).Take(PitchOutcome.Ball).Take(PitchOutcome.Ball);
        for (var i = 0; i < strikes; i++)
            s.Take(PitchOutcome.CalledStrike);
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");

        s.Take(PitchOutcome.Ball); // ball four

        Assert.Equal((1, 0), (s.Stat(chargedWith, StatKey.PitchBB), s.Stat(notCharged, StatKey.PitchBB)));
        Assert.Equal(1, s.Stat("hb1", StatKey.PitchBF)); // the reliever faces the batter either way
        s.HomeRun(M(1, 4));                              // the walked runner scores: charged to whoever was charged with the walk
        Assert.Equal(1, s.Stat(chargedWith, StatKey.PitchR) - (chargedWith == "hb1" ? 1 : 0)); // hb1 would also own the home run batter's run
    }
}
