using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// High 1 of the Codex review: which runs count on the play that ends a half-inning depends on the real third out (batter-runner out, force
/// out, or any other out) and on whether the run crossed the plate before it. Every expectation below is worked out by hand from that rule
/// (OBR-style principle; KBO certification pending, docs/game-engine.md section 5), none is read from the engine.
/// Standard situation: two outs, a1 on third, a2 on first, a5 batting (away team; home fielders h1..h9, pitcher hp).
/// </summary>
public class ThirdOutAndOrderTests
{
    private static Script TwoOutsFirstAndThird()
    {
        var s = new Script().Triple().Walk();                      // a1 on third, a2 on first
        s.FlyOut(M(3, 3), M(1, 1)).FlyOut(M(3, 3), M(1, 1));       // a3, a4 out; runners hold
        Assert.Equal((2, 0), (s.State.Outs, s.State.AwayRuns));
        return s;
    }

    private static Script BasesLoadedOneOut()
    {
        var s = new Script().Single().Single(M(1, 2)).Single(M(2, 3), M(1, 2)); // a1 third, a2 second, a3 first
        s.FlyOut(M(3, 3), M(2, 2), M(1, 1));                                    // a4 out
        Assert.Equal((1, 0), (s.State.Outs, s.State.AwayRuns));
        return s;
    }

    private static RunnerMove TagOutAtThird(int sequence) =>
        Seq(M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.RightField), Po(FieldPosition.ThirdBase)), sequence, outAt: 3);

    // ---------------------------------------------------------------- non-force third out and the order of the run

    [Fact]
    public void A_run_that_crossed_the_plate_before_a_non_force_third_out_counts()
    {
        var s = TwoOutsFirstAndThird();
        s.Approach().InPlay(BattedBallType.Line, InPlayResult.Single, [Seq(M(3, 4), 1), TagOutAtThird(2)]);

        var play = s.State.Plays.Last();
        Assert.Equal((1, 0), (s.State.AwayRuns, s.State.HomeRuns));
        Assert.Equal((1, InningHalf.Bottom), (s.State.Inning, s.State.Half));
        Assert.Equal((1, 1, 1), (s.Stat("a5", StatKey.BatRBI), s.Stat("a1", StatKey.RunR), s.Stat("a5", StatKey.BatH)));
        Assert.Equal((1, 1, 3), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER), s.Stat("hp", StatKey.PitchOuts)));
        Assert.Equal(1, s.Team(TeamSide.Away, StatKey.TeamLOB)); // a5 stands on first when the inning ends
        Assert.Equal(OutClassification.NonForce, play.Moves.Single(m => m.From == 1).Classification); // forced to second, but put out at third
        Assert.Equal((1, 2), (play.Moves.Single(m => m.From == 3).Sequence, play.Moves.Single(m => m.From == 1).Sequence));
        Assert.False(play.Moves.Single(m => m.From == 3).RunVoided);
    }

    [Fact]
    public void The_same_play_with_the_out_first_and_the_run_after_it_gives_no_run()
    {
        var s = TwoOutsFirstAndThird();
        s.Approach().InPlay(BattedBallType.Line, InPlayResult.Single, [Seq(M(3, 4), 2), TagOutAtThird(1)]);

        var play = s.State.Plays.Last();
        Assert.Equal(0, s.State.AwayRuns);
        Assert.Equal((0, 0, 0), (s.Stat("a5", StatKey.BatRBI), s.Stat("a1", StatKey.RunR), s.Stat("hp", StatKey.PitchR)));
        Assert.Equal(0, s.Stat("hp", StatKey.PitchER));
        Assert.True(play.Moves.Single(m => m.From == 3).RunVoided);
        Assert.Empty(play.Runs);
        var box = BoxScoreBuilder.Build(s.State);
        Assert.Equal((0, 2, 2), (box.Away.Runs, box.Away.Hits, box.Away.LeftOnBase)); // the triple and the single count; left on base: the batter on first and a1 whose run did not count (OBR 9.03(c))
    }

    [Fact]
    public void Without_an_order_a_play_with_a_third_out_and_a_run_is_rejected_and_nothing_changes()
    {
        var s = TwoOutsFirstAndThird();
        s.Approach();
        var before = s.State;

        var result = s.Try(new InPlayEvent("x1", BattedBallType.Line, InPlayResult.Single, Moves: [M(3, 4), M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.RightField), Po(FieldPosition.ThirdBase)) with { OutAt = 3 }]));

        Assert.Equal("Game.play_order_required", result.Error!.Value.Code);
        Assert.Same(before, s.State);
    }

    // ---------------------------------------------------------------- force outs

    [Fact]
    public void A_force_out_for_the_third_out_voids_the_run_whatever_the_order()
    {
        foreach (var runSequence in new[] { 1, 2 })
        {
            var s = TwoOutsFirstAndThird();
            var forceOut = Seq(M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase)), runSequence == 1 ? 2 : 1);
            s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [Seq(M(3, 4), runSequence), forceOut]);

            Assert.Equal(0, s.State.AwayRuns);
            Assert.Equal((0, 0, 0), (s.Stat("a5", StatKey.BatRBI), s.Stat("a1", StatKey.RunR), s.Stat("hp", StatKey.PitchR)));
            Assert.Equal(OutClassification.Force, s.State.Plays.Last().Moves.Single(m => m.From == 1).Classification);
            Assert.True(s.State.Plays.Last().Moves.Single(m => m.From == 3).RunVoided);
            Assert.Equal(2, s.Team(TeamSide.Away, StatKey.TeamLOB)); // a5 on first after the fielder's choice, and a1 whose run did not count (OBR 9.03(c))
        }
    }

    [Fact]
    public void A_forced_runner_put_out_at_the_base_he_was_forced_to_is_a_force_out_even_if_the_input_calls_it_a_tag_and_the_state_is_unchanged()
    {
        var s = TwoOutsFirstAndThird();
        s.Approach();
        var before = s.State;

        var asTag = s.Try(new InPlayEvent("x1", BattedBallType.Ground, InPlayResult.FieldersChoice,
            Moves: [Seq(M(3, 4), 1), Seq(M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase)), 2, method: OutMethod.Tag)]));

        Assert.Equal("Game.out_kind_mismatch", asTag.Error!.Value.Code);
        Assert.Same(before, s.State);

        // the same out made by touching the base, declared as the force out it is, is accepted
        s.Ok(new InPlayEvent("x2", BattedBallType.Ground, InPlayResult.FieldersChoice,
            Moves: [Seq(M(3, 4), 1), Seq(M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase)), 2, method: OutMethod.BaseTouch)]));
        Assert.Equal(0, s.State.AwayRuns);
    }

    [Fact]
    public void A_runner_put_out_beyond_the_base_he_was_forced_to_is_not_a_force_out()
    {
        var s = TwoOutsFirstAndThird();
        s.Approach();
        var before = s.State;

        var declaredForce = s.Try(new InPlayEvent("x1", BattedBallType.Line, InPlayResult.Single,
            Moves: [Seq(M(3, 4), 1), Seq(M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.RightField), Po(FieldPosition.ThirdBase)), 2, outAt: 3)]));

        Assert.Equal("Game.force_out_invalid", declaredForce.Error!.Value.Code);
        Assert.Same(before, s.State);
    }

    [Fact]
    public void A_force_that_an_earlier_out_removed_makes_the_next_out_a_tag_out_and_the_earlier_run_counts()
    {
        var s = BasesLoadedOneOut();                // a1 third, a2 second, a3 first, one out; a5 bats
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [
            Seq(M(3, 4), 1),                                                                                              // a1 scores first
            Seq(M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase)), 2),    // a3 forced out at second: out two
            Seq(M(2, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.ThirdBase)), 3),       // a2 tagged at third: a3 is gone, no force left
        ]);

        var play = s.State.Plays.Last();
        Assert.Equal((OutClassification.Force, OutClassification.NonForce), (play.Moves.Single(m => m.From == 1).Classification, play.Moves.Single(m => m.From == 2).Classification));
        Assert.Equal(1, s.State.AwayRuns);                       // third out was not a force out and a1 crossed the plate first
        Assert.Equal((1, 1, 0), (s.Stat("a1", StatKey.RunR), s.Stat("hp", StatKey.PitchER), s.Stat("a6", StatKey.BatRBI))); // double play: no RBI
        Assert.True(play.IsDoublePlay);
        Assert.Equal((1, InningHalf.Bottom), (s.State.Inning, s.State.Half));
    }

    [Fact]
    public void The_same_outs_in_the_other_order_leave_the_force_in_place_so_the_third_out_is_a_force_out_and_no_run_counts()
    {
        var s = BasesLoadedOneOut();
        s.Approach();
        var before = s.State;
        var asTag = s.Try(new InPlayEvent("x1", BattedBallType.Ground, InPlayResult.FieldersChoice, Moves: [
            Seq(M(3, 4), 1),
            Seq(M(2, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.ThirdBase)), 2),
            Seq(M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase)), 3)]));
        Assert.Equal("Game.out_kind_mismatch", asTag.Error!.Value.Code);   // a2 was still forced by a3 when he was put out
        Assert.Same(before, s.State);

        s.Ok(new InPlayEvent("x2", BattedBallType.Ground, InPlayResult.FieldersChoice, Moves: [
            Seq(M(3, 4), 1),
            Seq(M(2, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.ThirdBase)), 2),
            Seq(M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase)), 3)]));

        Assert.Equal(0, s.State.AwayRuns);
        Assert.Equal((0, 0), (s.Stat("a1", StatKey.RunR), s.Stat("hp", StatKey.PitchR)));
        Assert.True(s.State.Plays.Last().Moves.Single(m => m.From == 3).RunVoided);
    }

    // ---------------------------------------------------------------- batter-runner out

    [Fact]
    public void A_batter_runner_out_for_the_third_out_voids_the_run_in_either_order()
    {
        foreach (var batterSequence in new[] { 1, 2 })
        {
            var s = TwoOutsFirstAndThird();
            s.Approach().InPlay(BattedBallType.Ground, InPlayResult.Out, [Seq(M(3, 4), batterSequence == 1 ? 2 : 1), M(1, 2)],
                [A(FieldPosition.Shortstop), Po(FieldPosition.FirstBase)], batterSequence: batterSequence);

            var play = s.State.Plays.Last();
            Assert.Equal(0, s.State.AwayRuns);
            Assert.Equal((BatterResult.Out, 0), (play.Result, play.Runs.Count));
            Assert.Equal((1, 0, 0), (s.Stat("a5", StatKey.BatAB), s.Stat("a5", StatKey.BatRBI), s.Stat("a1", StatKey.RunR)));
            Assert.Equal(OutClassification.BatterRunner, play.Moves.Single(m => m.From == 0).Classification);
            Assert.Equal(2, s.Team(TeamSide.Away, StatKey.TeamLOB)); // a2 on second, and a1 who crossed the plate without a counting run (OBR 9.03(c): neither out nor scored, so left)
        }
    }

    [Fact]
    public void A_caught_ball_puts_the_batter_out_first_so_a_runner_thrown_out_after_tagging_up_is_never_a_force_out()
    {
        var s = new Script().Single();                  // a1 on first, no outs
        s.Approach();
        var before = s.State;
        var tagUpOut = new[] { A(FieldPosition.CenterField), Po(FieldPosition.SecondBase) };
        var asForce = s.Try(new InPlayEvent("x1", BattedBallType.Fly, InPlayResult.Out,
            Moves: [M(1, 0, MoveCause.Play, OutKind.Force, tagUpOut)], BatterCredits: [Po(FieldPosition.CenterField)]));
        Assert.Equal("Game.force_out_invalid", asForce.Error!.Value.Code);   // the batter was caught, nobody is forced
        Assert.Same(before, s.State);

        s.Ok(new InPlayEvent("x2", BattedBallType.Fly, InPlayResult.Out,
            Moves: [M(1, 0, MoveCause.Play, OutKind.Tag, tagUpOut)], BatterCredits: [Po(FieldPosition.CenterField)]));

        var play = s.State.Plays.Last();
        Assert.Equal((2, true), (s.State.Outs, play.IsDoublePlay));
        Assert.Equal((OutClassification.BatterRunner, OutClassification.NonForce), (play.Moves.Single(m => m.From == 0).Classification, play.Moves.Single(m => m.From == 1).Classification));
        Assert.Equal((1, 2), (play.Moves.Single(m => m.From == 0).Sequence, play.Moves.Single(m => m.From == 1).Sequence)); // default order: the catch comes first
    }

    // ---------------------------------------------------------------- plays between pitches

    [Fact]
    public void A_double_steal_where_the_runner_from_third_scores_before_the_caught_stealing_out_gives_a_run()
    {
        var s = TwoOutsFirstAndThird();
        s.Runner(RunnerPlayKind.Steal, Seq(M(3, 4, MoveCause.Steal), 1),
            Seq(M(1, 0, MoveCause.Steal, OutKind.Tag, A(FieldPosition.Catcher), Po(FieldPosition.SecondBase)), 2));

        Assert.Equal(1, s.State.AwayRuns);
        Assert.Equal((1, 1, 1), (s.Stat("a1", StatKey.RunSB), s.Stat("a2", StatKey.RunCS), s.Stat("h9", StatKey.CatCS)));
        Assert.Equal((1, 1, 0), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER), s.Stat("a1", StatKey.BatRBI)));
        Assert.Equal((1, InningHalf.Bottom), (s.State.Inning, s.State.Half));
        Assert.Equal(0, s.Team(TeamSide.Away, StatKey.TeamLOB));
    }

    [Fact]
    public void A_double_steal_where_the_caught_stealing_out_comes_first_gives_no_run()
    {
        var s = TwoOutsFirstAndThird();
        s.Runner(RunnerPlayKind.Steal, Seq(M(3, 4, MoveCause.Steal), 2),
            Seq(M(1, 0, MoveCause.Steal, OutKind.Tag, A(FieldPosition.Catcher), Po(FieldPosition.SecondBase)), 1));

        Assert.Equal(0, s.State.AwayRuns);
        Assert.Equal((0, 0), (s.Stat("a1", StatKey.RunR), s.Stat("hp", StatKey.PitchR)));
        Assert.Equal(1, s.Stat("a2", StatKey.RunCS));
    }

    // ---------------------------------------------------------------- the order and out details are validated

    [Fact]
    public void Order_numbers_must_be_all_or_none_positive_and_different_and_a_caught_batter_must_stay_first()
    {
        var s = TwoOutsFirstAndThird();
        s.Approach();
        var before = s.State;
        var credits = new[] { A(FieldPosition.RightField), Po(FieldPosition.ThirdBase) };

        InPlayEvent Ev(string id, params RunnerMove[] moves) => new(id, BattedBallType.Line, InPlayResult.Single, Moves: [.. moves]);
        var tag = M(1, 0, MoveCause.Play, OutKind.Tag, credits);
        Assert.Equal("Game.play_order_incomplete", s.Try(Ev("a", Seq(M(3, 4), 1), tag with { OutAt = 3 })).Error!.Value.Code);
        Assert.Equal("Game.play_order_invalid", s.Try(Ev("b", Seq(M(3, 4), 0), Seq(tag, 1, 3))).Error!.Value.Code);
        Assert.Equal("Game.play_order_invalid", s.Try(Ev("c", Seq(M(3, 4), 2), Seq(tag, 2, 3))).Error!.Value.Code);
        Assert.Same(before, s.State);

        // a sequence on a runner who neither scores nor is out has nothing to order
        Assert.Equal("Game.play_order_invalid", s.Try(Ev("n", Seq(M(3, 4), 1), Seq(M(1, 2), 2))).Error!.Value.Code);
        Assert.Same(before, s.State);

        // a ball caught for an out always comes first: the batter's number must be the smallest
        var tagUp = M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.CenterField), Po(FieldPosition.SecondBase));
        var s2 = new Script().Single();                  // a1 on first
        s2.Approach();
        var late = s2.Try(new InPlayEvent("e", BattedBallType.Fly, InPlayResult.Out, Moves: [Seq(tagUp, 1)], BatterCredits: [Po(FieldPosition.LeftField)], BatterSequence: 2));
        Assert.Equal("Game.batter_out_order_invalid", late.Error!.Value.Code);
        var first = s2.Try(new InPlayEvent("f", BattedBallType.Fly, InPlayResult.Out, Moves: [Seq(tagUp, 2)], BatterCredits: [Po(FieldPosition.LeftField)], BatterSequence: 1));
        Assert.True(first.IsSuccess, first.Error?.Message);
    }

    [Fact]
    public void Where_an_out_was_made_and_how_is_validated_and_a_runner_out_needs_its_kind()
    {
        var s = new Script().Single();                                    // a1 on first
        var before = s.State;
        var credits = new[] { A(FieldPosition.Catcher), Po(FieldPosition.SecondBase) };

        RunnerPlayEvent Steal(string id, RunnerMove move) => new(id, RunnerPlayKind.Steal, [move]);
        Assert.Equal("Game.out_kind_missing", s.Try(Steal("a", M(1, 0, MoveCause.Steal, null, credits))).Error!.Value.Code);
        Assert.Equal("Game.out_at_invalid", s.Try(Steal("b", M(1, 0, MoveCause.Steal, OutKind.Tag, credits) with { OutAt = 5 })).Error!.Value.Code);
        Assert.Equal("Game.out_at_without_out", s.Try(Steal("c", M(1, 2, MoveCause.Steal) with { OutAt = 2 })).Error!.Value.Code);
        Assert.Equal("Game.out_method_invalid", s.Try(Steal("d", M(1, 0, MoveCause.Steal, OutKind.Tag, credits) with { Method = OutMethod.BaseTouch })).Error!.Value.Code);
        Assert.Same(before, s.State);

        s.Ok(Steal("e", M(1, 0, MoveCause.Steal, OutKind.Tag, credits) with { Method = OutMethod.Tag }));
        Assert.Equal(1, s.State.Outs);
    }

    // ---------------------------------------------------------------- the earned-run reconstruction reads the same order

    private static Script BasesLoadedTwoOuts()
    {
        var s = new Script().Single().Single(M(1, 2)).Single(M(2, 3), M(1, 2));  // a1 third, a2 second, a3 first
        s.FlyOut(M(3, 3), M(2, 2), M(1, 1)).FlyOut(M(3, 3), M(2, 2), M(1, 1));    // a4, a5 out
        Assert.Equal((2, 0), (s.State.Outs, s.State.AwayRuns));
        return s;
    }

    [Theory]
    [InlineData(1, 2, 1)]   // a1 crosses first, then the out the error prevented would have been the third: a1's run is earned
    [InlineData(2, 1, 0)]   // that out first would have ended the inning before a1 scored: no earned run
    public void A_run_is_earned_only_if_it_crossed_the_plate_before_the_third_out_the_error_prevented(int a1Sequence, int a2Sequence, int earnedExpected)
    {
        var s = BasesLoadedTwoOuts();                                      // batter a6
        s.Approach().InPlay(BattedBallType.Line, InPlayResult.Single, [
            Seq(M(3, 4), a1Sequence),
            Seq(M(2, 4, MoveCause.ErrorSavedOut), a2Sequence, outAt: 4),   // a2 was safe at home only because of the catcher's error
            M(1, 3)], errors: [FieldPosition.Catcher]);
        s.FlyOut(M(3, 3), M(1, 1));                                        // a7 ends the inning

        Assert.Equal(2, s.State.AwayRuns);
        Assert.Equal((2, earnedExpected), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER)));
        Assert.Equal((1, 1), (s.Stat("a6", StatKey.BatRBI), s.Stat("h9", StatKey.FldE))); // a2's run is not an RBI
        Assert.Equal(2, s.Team(TeamSide.Away, StatKey.TeamLOB));                          // a3 on third, a6 on first
    }
}
