using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>Expected values below are worked out by hand from baseball scoring rules, never copied from the engine's output.</summary>
public class CountAndPlateAppearanceTests
{
    // ---- real count progression (D1)

    [Fact]
    public void Count_follows_real_pitches_with_foul_with_two_strikes_not_ending_the_at_bat()
    {
        var s = new Script().Approach();

        s.Ball();   Assert.Equal((1, 0), (s.State.Count.Balls, s.State.Count.Strikes));
        s.Strike(); Assert.Equal((1, 1), (s.State.Count.Balls, s.State.Count.Strikes));
        s.Foul();   Assert.Equal((1, 2), (s.State.Count.Balls, s.State.Count.Strikes));
        s.Foul();   Assert.Equal((1, 2), (s.State.Count.Balls, s.State.Count.Strikes));
        s.Ball().Ball(); Assert.Equal((3, 2), (s.State.Count.Balls, s.State.Count.Strikes));
        s.Foul();   Assert.Equal((3, 2), (s.State.Count.Balls, s.State.Count.Strikes));
        Assert.Equal(PaStage.InSequence, s.State.Stage);
        Assert.Equal(0, s.State.Outs);

        s.Ball(); // eighth pitch: ball four

        Assert.Equal(PaStage.Start, s.State.Stage);
        Assert.Equal(PitchCountState.Zero, s.State.Count);
        Assert.Equal("a1", s.State.Bases.First!.Player.Value);
        Assert.Equal(8, s.Stat("hp", StatKey.PitchPitches));
        Assert.Equal(8, s.Stat("h9", StatKey.CatPitches));
        Assert.Equal(1, s.Stat("a1", StatKey.BatBB));
        Assert.Equal(0, s.Stat("a1", StatKey.BatAB));
        Assert.Equal(1, s.Stat("a1", StatKey.BatPA));
    }

    [Fact]
    public void Strikeout_looking_swinging_and_foul_bunt_all_end_on_the_third_strike()
    {
        var s = new Script();
        s.Approach().Strike().Strike().Strike();
        s.Approach().Swing().Foul().Foul().Swing();
        s.Approach().Strike().Swing().Pitch(PitchOutcome.FoulBunt);

        Assert.Equal((1, InningHalf.Bottom, 0), (s.State.Inning, s.State.Half, s.State.Outs)); // the third out closed the half
        Assert.Equal(3, s.Stat("hp", StatKey.PitchK));
        Assert.Equal(3, s.Stat("hp", StatKey.PitchOuts));
        Assert.Equal(3, s.Stat("h9", StatKey.FldPO)); // the catcher gets the putout on every strikeout
        Assert.Equal(3 + 4 + 3, s.Stat("hp", StatKey.PitchPitches));
        Assert.Equal(new[] { 0 }, s.State.AwayLine);
    }

    [Fact]
    public void Take_consumes_exactly_one_pitch_and_hands_control_back_with_the_exact_count_and_runners()
    {
        var s = new Script().Walk(); // a1 on first
        Assert.Equal("a1", s.State.Bases.First!.Player.Value);

        s.Take(PitchOutcome.Ball);
        Assert.Equal((PaStage.Decision, 1, 0), (s.State.Stage, s.State.Count.Balls, s.State.Count.Strikes));
        Assert.Equal(1, s.Stat("hp", StatKey.PitchPitches) - 4); // exactly one more pitch than the walk's four
        Assert.Equal("a1", s.State.Bases.First!.Player.Value);
        Assert.Null(s.State.Approach);

        s.Take(PitchOutcome.CalledStrike);
        Assert.Equal((PaStage.Decision, 1, 1), (s.State.Stage, s.State.Count.Balls, s.State.Count.Strikes));
        Assert.True(GameReducer.LegalActions(s.State).Allows(GameActionKind.Approach));
        Assert.True(GameReducer.LegalActions(s.State).Allows(GameActionKind.Take));
        Assert.False(GameReducer.LegalActions(s.State).Allows(GameActionKind.Pitch));

        s.Approach().Swing().InPlay(BattedBallType.Line, InPlayResult.Single, [M(1, 2)]); // resumes in the same plate appearance

        Assert.Equal(PaStage.Start, s.State.Stage);
        Assert.Equal("a2", s.State.Bases.First!.Player.Value);
        Assert.Equal("a1", s.State.Bases.Second!.Player.Value);
        Assert.Equal(1, s.Stat("a2", StatKey.BatH));
        Assert.Equal(1, s.Stat("a2", StatKey.BatPA)); // one plate appearance despite two takes
        Assert.Equal(4 + 2 + 1 + 1, s.Stat("hp", StatKey.PitchPitches)); // walk 4 + two takes + swinging strike + the pitch put in play
    }

    [Fact]
    public void Take_can_end_the_plate_appearance_and_then_the_next_batter_starts_at_zero_zero()
    {
        var s = new Script();

        s.Take(PitchOutcome.Ball).Take(PitchOutcome.Ball).Take(PitchOutcome.Ball).Take(PitchOutcome.Ball);

        Assert.Equal((PaStage.Start, 0, 0), (s.State.Stage, s.State.Count.Balls, s.State.Count.Strikes));
        Assert.Equal("a1", s.State.Bases.First!.Player.Value);
        s.Take(PitchOutcome.CalledStrike).Take(PitchOutcome.CalledStrike).Take(PitchOutcome.CalledStrike);
        Assert.Equal(1, s.State.Outs);
        Assert.Equal(1, s.Stat("a2", StatKey.BatSO));
    }

    [Fact]
    public void Take_and_pitch_are_rejected_outside_their_stage_and_for_non_take_outcomes()
    {
        var s = new Script();

        Assert.Equal("Game.pitch_not_legal", s.Try(new PitchEvent("x1", new PitchSpec(PitchOutcome.Ball))).Error!.Value.Code);
        Assert.Equal("Game.take_outcome_invalid", s.Try(new TakeEvent("x2", new PitchSpec(PitchOutcome.SwingingStrike))).Error!.Value.Code);
        Assert.Equal("Game.take_outcome_invalid", s.Try(new TakeEvent("x3", new PitchSpec(PitchOutcome.Foul))).Error!.Value.Code);
        s.Approach();
        Assert.Equal("Game.take_not_legal", s.Try(new TakeEvent("x4", new PitchSpec(PitchOutcome.Ball))).Error!.Value.Code);
        Assert.Equal("Game.approach_not_legal", s.Try(new ApproachEvent("x5", ApproachKind.Patient)).Error!.Value.Code);
        Assert.Equal("Game.in_play_invalid", s.Try(new InPlayEvent("x6", BattedBallType.None, InPlayResult.Single)).Error!.Value.Code);
        Assert.Equal(PaStage.InSequence, s.State.Stage);
        Assert.Equal(PitchCountState.Zero, s.State.Count);
    }

    // ---- walks, hit by pitch, strikeouts with runners

    [Fact]
    public void Bases_loaded_walk_forces_in_a_run_with_an_RBI_and_charges_the_pitcher()
    {
        var s = new Script().Walk().Walk().Walk();
        Assert.Equal(3, s.State.Bases.Count);

        s.Walk();

        Assert.Equal(1, s.State.AwayRuns);
        Assert.Equal(new[] { 1 }, s.State.AwayLine);
        Assert.Equal(("a4", "a3", "a2"), (s.State.Bases.First!.Player.Value, s.State.Bases.Second!.Player.Value, s.State.Bases.Third!.Player.Value));
        Assert.Equal(1, s.Stat("a4", StatKey.BatRBI));
        Assert.Equal(1, s.Stat("a1", StatKey.RunR));
        Assert.Equal(4, s.Stat("hp", StatKey.PitchBB));
        Assert.Equal(1, s.Stat("hp", StatKey.PitchR));
        Assert.Single(s.State.Plays.Last().Runs);
        Assert.Equal(MoveCause.Forced, s.State.Plays.Last().Runs[0].Cause);
    }

    [Fact]
    public void Hit_by_pitch_with_a_runner_on_second_only_does_not_advance_that_runner()
    {
        var s = new Script().Double();          // a1 on second
        s.HitByPitch();                         // a2 hit by pitch

        Assert.Equal("a2", s.State.Bases.First!.Player.Value);
        Assert.Equal("a1", s.State.Bases.Second!.Player.Value);
        Assert.Null(s.State.Bases.Third);
        Assert.Equal((1, 0, 0), (s.Stat("a2", StatKey.BatPA), s.Stat("a2", StatKey.BatAB), s.Stat("a2", StatKey.BatH)));
        Assert.Equal(1, s.Stat("a2", StatKey.BatHBP));
        Assert.Equal(1, s.Stat("hp", StatKey.PitchHBP));
        Assert.Equal(0, s.State.AwayRuns);
    }

    // ---- hits

    [Theory]
    [InlineData(InPlayResult.Single, StatKey.Bat1B, 1)]
    [InlineData(InPlayResult.Double, StatKey.Bat2B, 2)]
    [InlineData(InPlayResult.Triple, StatKey.Bat3B, 3)]
    public void Hit_with_empty_bases_puts_the_batter_on_the_base_and_records_one_hit(InPlayResult result, StatKey kind, int @base)
    {
        var s = new Script().Approach().InPlay(BattedBallType.Line, result);

        Assert.Equal("a1", s.State.Bases.At(@base)!.Player.Value);
        Assert.Equal(1, s.State.Bases.Count);
        Assert.Equal((1, 1, 1, 1), (s.Stat("a1", StatKey.BatPA), s.Stat("a1", StatKey.BatAB), s.Stat("a1", StatKey.BatH), s.Stat("a1", kind)));
        Assert.Equal(1, s.Stat("hp", StatKey.PitchH));
        Assert.Equal(0, s.State.Outs);
    }

    [Fact]
    public void Home_run_with_bases_loaded_scores_four_and_credits_four_RBI_four_runs_and_one_home_run()
    {
        var s = new Script().Walk().Walk().Walk();

        s.HomeRun();

        Assert.Equal(4, s.State.AwayRuns);
        Assert.Equal(0, s.State.Bases.Count);
        Assert.Equal((1, 1, 1, 4, 1), (s.Stat("a4", StatKey.BatPA), s.Stat("a4", StatKey.BatAB), s.Stat("a4", StatKey.BatHR), s.Stat("a4", StatKey.BatRBI), s.Stat("a4", StatKey.RunR)));
        Assert.Equal(1, s.Stat("a1", StatKey.RunR));
        Assert.Equal(1, s.Stat("hp", StatKey.PitchHR));
        Assert.Equal(1, s.Stat("hp", StatKey.PitchH));
        Assert.Equal(4, s.Stat("hp", StatKey.PitchR));
        Assert.Equal(4, s.State.Plays.Last().Runs.Count);
        Assert.Equal(new[] { 4 }, s.State.AwayLine);
    }

    [Fact]
    public void Single_scoring_from_second_and_advancing_from_first_gets_the_RBI_and_leaves_runners_on_first_and_third()
    {
        var s = new Script().Double().Walk(); // a1 second -> a2 walks: a1 stays second? no: walk forces only if first occupied
        // a1 on 2nd, a2 on 1st: now a3 singles, a1 scores, a2 to third
        s.Single(M(2, 4), M(1, 3));

        Assert.Equal(1, s.State.AwayRuns);
        Assert.Equal(("a3", "a2"), (s.State.Bases.First!.Player.Value, s.State.Bases.Third!.Player.Value));
        Assert.Equal(1, s.Stat("a3", StatKey.BatRBI));
        Assert.Equal(1, s.Stat("a3", StatKey.BatH));
    }

    [Fact]
    public void Hits_require_every_runner_to_be_described_and_may_not_break_base_order()
    {
        var s = new Script().Double().Walk();
        s.Approach();

        Assert.Equal("Game.runner_move_missing", s.Try(new InPlayEvent("x1", BattedBallType.Line, InPlayResult.Single, Moves: [M(2, 4)])).Error!.Value.Code);
        Assert.Equal("Game.runner_passed_runner", s.Try(new InPlayEvent("x2", BattedBallType.Line, InPlayResult.Single, Moves: [M(2, 3), M(1, 4)])).Error!.Value.Code);
        Assert.Equal("Game.base_occupied_twice", s.Try(new InPlayEvent("x3", BattedBallType.Line, InPlayResult.Single, Moves: [M(2, 3), M(1, 3)])).Error!.Value.Code);
        Assert.Equal("Game.runner_moved_backward", s.Try(new InPlayEvent("x4", BattedBallType.Line, InPlayResult.Single, Moves: [M(2, 1), M(1, 3)])).Error!.Value.Code);
        Assert.Equal("Game.move_source_invalid", s.Try(new InPlayEvent("x5", BattedBallType.Line, InPlayResult.Single, Moves: [M(3, 4), M(2, 3), M(1, 2)])).Error!.Value.Code);
        // none of the rejected events changed anything
        Assert.Equal(PaStage.InSequence, s.State.Stage);
        Assert.Equal(("a1", "a2"), (s.State.Bases.Second!.Player.Value, s.State.Bases.First!.Player.Value));
        Assert.Equal(0, s.State.AwayRuns);
    }

    // ---- outs

    [Fact]
    public void Fly_out_ground_out_and_line_out_record_putouts_and_assists_to_the_right_fielders()
    {
        var s = new Script();

        s.Approach().InPlay(BattedBallType.Fly, InPlayResult.Out, batterCredits: [Po(FieldPosition.CenterField)]);
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.Out, batterCredits: [A(FieldPosition.Shortstop), Po(FieldPosition.FirstBase)]);
        s.Approach().InPlay(BattedBallType.Line, InPlayResult.Out, batterCredits: [Po(FieldPosition.ThirdBase)]);

        Assert.Equal((1, 1, 1), (s.Stat("h1", StatKey.FldPO), s.Stat("h4", StatKey.FldPO), s.Stat("h7", StatKey.FldPO)));
        Assert.Equal(1, s.Stat("h2", StatKey.FldA));
        Assert.Equal(3, s.Stat("hp", StatKey.PitchOuts));
        Assert.Equal(3, s.Stat("h9", StatKey.CatOuts)); // the catcher was behind the plate for all three outs
        Assert.Equal(3, s.Stat("a1", StatKey.BatAB) + s.Stat("a2", StatKey.BatAB) + s.Stat("a3", StatKey.BatAB));
        Assert.Equal(InningHalf.Bottom, s.State.Half);
    }

    [Fact]
    public void Every_out_needs_exactly_one_putout_and_a_real_fielding_position()
    {
        var s = new Script().Approach();

        Assert.Equal("Game.out_credit_invalid", s.Try(new InPlayEvent("x1", BattedBallType.Fly, InPlayResult.Out)).Error!.Value.Code);
        Assert.Equal("Game.out_credit_invalid", s.Try(new InPlayEvent("x2", BattedBallType.Fly, InPlayResult.Out, BatterCredits: [Po(FieldPosition.LeftField), Po(FieldPosition.CenterField)])).Error!.Value.Code);
        Assert.Equal("Game.out_credit_invalid", s.Try(new InPlayEvent("x3", BattedBallType.Fly, InPlayResult.Out, BatterCredits: [Po(FieldPosition.DesignatedHitter)])).Error!.Value.Code);
        Assert.Equal("Game.batter_credits_invalid", s.Try(new InPlayEvent("x4", BattedBallType.Fly, InPlayResult.Single, BatterCredits: [Po(FieldPosition.LeftField)])).Error!.Value.Code);
        Assert.Equal(0, s.State.Outs);
    }

    [Fact]
    public void Sacrifice_fly_scores_the_runner_without_an_at_bat_and_with_an_RBI()
    {
        var s = new Script().Triple(); // a1 on third

        s.Approach().InPlay(BattedBallType.Fly, InPlayResult.Out, [M(3, 4)], [Po(FieldPosition.CenterField)]);

        var play = s.State.Plays.Last();
        Assert.Equal(BatterResult.SacrificeFly, play.Result);
        Assert.Equal((1, 0, 1, 1), (s.Stat("a2", StatKey.BatPA), s.Stat("a2", StatKey.BatAB), s.Stat("a2", StatKey.BatSF), s.Stat("a2", StatKey.BatRBI)));
        Assert.Equal(1, s.State.AwayRuns);
        Assert.Equal(1, s.State.Outs);
    }

    [Fact]
    public void A_run_on_the_play_that_makes_the_third_out_by_the_batter_does_not_count_so_no_sacrifice_fly()
    {
        var s = new Script().Triple();                                   // a1 on third
        s.Approach().InPlay(BattedBallType.Fly, InPlayResult.Out, [M(3, 3)], [Po(FieldPosition.LeftField)]);
        s.Approach().InPlay(BattedBallType.Fly, InPlayResult.Out, [M(3, 3)], [Po(FieldPosition.LeftField)]); // two outs
        s.Approach();

        // a runner "scoring" on the play that has a third out needs the order of the play spelled out
        Assert.Equal("Game.play_order_required", s.Try(new InPlayEvent("x0", BattedBallType.Fly, InPlayResult.Out, Moves: [M(3, 4)], BatterCredits: [Po(FieldPosition.LeftField)])).Error!.Value.Code);
        Assert.Equal((2, 0), (s.State.Outs, s.State.AwayRuns));

        s.Ok(new InPlayEvent("x1", BattedBallType.Fly, InPlayResult.Out, Moves: [Seq(M(3, 4), 1)], BatterCredits: [Po(FieldPosition.LeftField)]));

        var play = s.State.Plays.Last();
        Assert.Equal(0, s.State.AwayRuns);
        Assert.Equal((BatterResult.Out, 0), (play.Result, play.Runs.Count)); // not a sacrifice fly: the out was made on the batter-runner
        Assert.True(play.Moves.Single(m => m.From == 3).RunVoided);
        Assert.Equal((0, 0, 0), (s.Stat("a1", StatKey.RunR), s.Stat("a4", StatKey.BatRBI), s.Stat("hp", StatKey.PitchR)));
        Assert.Equal((1, InningHalf.Bottom), (s.State.Inning, s.State.Half));
        Assert.Equal(1, s.Team(TeamSide.Away, StatKey.TeamLOB)); // a1 crossed the plate on a run that does not count: neither out nor scored, so left on base (OBR 9.03(c) box proof)
    }

    [Fact]
    public void Sacrifice_bunt_advances_the_runner_without_an_at_bat_only_when_flagged_and_valid()
    {
        var s = new Script().Single(); // a1 on first
        s.Approach();

        Assert.Equal("Game.sacrifice_bunt_invalid", s.Try(new InPlayEvent("x1", BattedBallType.Ground, InPlayResult.Out, Sacrifice: true, Moves: [M(1, 2)], BatterCredits: [A(FieldPosition.ThirdBase), Po(FieldPosition.FirstBase)])).Error!.Value.Code);
        s.InPlay(BattedBallType.Bunt, InPlayResult.Out, [M(1, 2)], [A(FieldPosition.ThirdBase), Po(FieldPosition.FirstBase)], sacrifice: true);

        Assert.Equal(BatterResult.SacrificeBunt, s.State.Plays.Last().Result);
        Assert.Equal((1, 0, 1), (s.Stat("a2", StatKey.BatPA), s.Stat("a2", StatKey.BatAB), s.Stat("a2", StatKey.BatSH)));
        Assert.Equal("a1", s.State.Bases.Second!.Player.Value);
        Assert.Equal(1, s.State.Outs);
    }

    [Fact]
    public void An_unflagged_bunt_out_is_a_normal_at_bat()
    {
        var s = new Script().Single();
        s.Approach().InPlay(BattedBallType.Bunt, InPlayResult.Out, [M(1, 2)], [A(FieldPosition.ThirdBase), Po(FieldPosition.FirstBase)]);

        Assert.Equal(BatterResult.Out, s.State.Plays.Last().Result);
        Assert.Equal((1, 1, 0), (s.Stat("a2", StatKey.BatPA), s.Stat("a2", StatKey.BatAB), s.Stat("a2", StatKey.BatSH)));
    }

    [Fact]
    public void Ground_ball_double_play_gives_two_outs_one_GIDP_no_RBI_and_double_play_credit_to_each_fielder()
    {
        var s = new Script().Single().Single(M(1, 2)); // a1 on second, a2 on first
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.Out,
            [M(2, 3), M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase))],
            [A(FieldPosition.SecondBase), Po(FieldPosition.FirstBase)]);

        Assert.Equal(2, s.State.Outs);
        Assert.True(s.State.Plays.Last().IsDoublePlay);
        Assert.Equal((1, 1, 1, 0), (s.Stat("a3", StatKey.BatPA), s.Stat("a3", StatKey.BatAB), s.Stat("a3", StatKey.BatGIDP), s.Stat("a3", StatKey.BatRBI)));
        Assert.Equal((1, 1, 1), (s.Stat("h2", StatKey.FldDP), s.Stat("h8", StatKey.FldDP), s.Stat("h4", StatKey.FldDP)));
        Assert.Equal((1, 1), (s.Stat("h8", StatKey.FldPO), s.Stat("h4", StatKey.FldPO)));
        Assert.Equal((1, 1), (s.Stat("h2", StatKey.FldA), s.Stat("h8", StatKey.FldA)));
        Assert.Equal(2, s.Stat("hp", StatKey.PitchOuts));
        Assert.Equal(("a1", null), (s.State.Bases.Third!.Player.Value, s.State.Bases.First?.Player.Value));
    }

    [Fact]
    public void A_run_that_scores_on_a_ground_ball_double_play_counts_as_a_run_but_gives_the_batter_no_RBI()
    {
        var s = new Script().Single().Single(M(1, 3)); // a1 third, a2 first
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.Out,
            [M(3, 4), M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase))],
            [A(FieldPosition.SecondBase), Po(FieldPosition.FirstBase)]);

        Assert.Equal((1, 2), (s.State.AwayRuns, s.State.Outs));
        Assert.Equal((1, 1, 0), (s.Stat("a1", StatKey.RunR), s.Stat("a3", StatKey.BatGIDP), s.Stat("a3", StatKey.BatRBI)));
        Assert.Equal(1, s.Stat("hp", StatKey.PitchR));
    }

    [Fact]
    public void Triple_play_ends_the_half_inning_with_no_runners_left_and_credits_every_fielder_involved()
    {
        var s = new Script().Single().Single(M(1, 2)); // a1 second, a2 first
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.Out,
            [M(2, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.ThirdBase)),
             M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.ThirdBase), Po(FieldPosition.SecondBase))],
            [A(FieldPosition.SecondBase), Po(FieldPosition.FirstBase)]);

        Assert.Equal((1, InningHalf.Bottom, 0), (s.State.Inning, s.State.Half, s.State.Outs));
        Assert.Equal(3, s.Stat("hp", StatKey.PitchOuts));
        Assert.Equal(1, s.Stat("a3", StatKey.BatGIDP));
        Assert.Equal((1, 1, 1, 1), (s.Stat("h2", StatKey.FldDP), s.Stat("h7", StatKey.FldDP), s.Stat("h8", StatKey.FldDP), s.Stat("h4", StatKey.FldDP)));
        Assert.Equal(0, s.Team(TeamSide.Away, StatKey.TeamLOB));
        Assert.Equal(0, s.State.Bases.Count);
    }

    [Fact]
    public void More_than_three_outs_in_one_inning_is_rejected()
    {
        var s = new Script().Single().Single(M(1, 2));
        s.Approach().InPlay(BattedBallType.Fly, InPlayResult.Out, [M(2, 2), M(1, 1)], [Po(FieldPosition.LeftField)]);
        s.Approach();

        var tooMany = s.Try(new InPlayEvent("x1", BattedBallType.Ground, InPlayResult.Out,
            Moves: [M(2, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.ThirdBase)),
                    M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.ThirdBase), Po(FieldPosition.SecondBase))],
            BatterCredits: [A(FieldPosition.SecondBase), Po(FieldPosition.FirstBase)]));

        Assert.Equal("Game.too_many_outs", tooMany.Error!.Value.Code);
        Assert.Equal(1, s.State.Outs);
    }

    [Fact]
    public void Reached_on_error_needs_an_error_and_is_an_at_bat_without_a_hit_or_RBI()
    {
        var s = new Script().Approach();
        Assert.Equal("Game.error_missing", s.Try(new InPlayEvent("x1", BattedBallType.Ground, InPlayResult.ReachedOnError)).Error!.Value.Code);

        s.InPlay(BattedBallType.Ground, InPlayResult.ReachedOnError, errors: [FieldPosition.Shortstop]);

        Assert.Equal((1, 1, 0, 1), (s.Stat("a1", StatKey.BatPA), s.Stat("a1", StatKey.BatAB), s.Stat("a1", StatKey.BatH), s.Stat("a1", StatKey.BatROE)));
        Assert.Equal(1, s.Stat("h2", StatKey.FldE));
        Assert.Equal(0, s.Stat("hp", StatKey.PitchH));
        Assert.Equal("a1", s.State.Bases.First!.Player.Value);
        Assert.Equal(0, s.State.Outs);
    }

    [Fact]
    public void Fielders_choice_retires_the_lead_runner_by_force_and_puts_the_batter_on_first()
    {
        var s = new Script().Single(); // a1 on first
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice,
            [M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.ThirdBase), Po(FieldPosition.SecondBase))]);

        Assert.Equal("a2", s.State.Bases.First!.Player.Value);
        Assert.Equal(1, s.State.Outs);
        Assert.Equal((1, 1, 0, 1), (s.Stat("a2", StatKey.BatPA), s.Stat("a2", StatKey.BatAB), s.Stat("a2", StatKey.BatH), s.Stat("a2", StatKey.BatFC)));
        Assert.Equal(1, s.Stat("h8", StatKey.FldPO));
        Assert.Equal(1, s.Stat("h7", StatKey.FldA));
        Assert.Equal(1, s.Stat("hp", StatKey.PitchH)); // only the first-inning single; a fielder's choice is not a hit
    }

    [Fact]
    public void Force_out_is_rejected_for_a_runner_who_is_not_forced_and_fielders_choice_needs_a_runner()
    {
        var s = new Script().Double(); // a1 on second, first base empty: not forced
        s.Approach();

        Assert.Equal("Game.force_out_invalid", s.Try(new InPlayEvent("x1", BattedBallType.Ground, InPlayResult.FieldersChoice,
            Moves: [M(2, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.ThirdBase))])).Error!.Value.Code);
        Assert.True(s.Try(new InPlayEvent("x2", BattedBallType.Ground, InPlayResult.FieldersChoice,
            Moves: [M(2, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.ThirdBase))])).IsSuccess);

        var empty = new Script().Approach();
        Assert.Equal("Game.fielders_choice_invalid", empty.Try(new InPlayEvent("x3", BattedBallType.Ground, InPlayResult.FieldersChoice)).Error!.Value.Code);
    }
}
