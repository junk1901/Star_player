using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// Codex re-review (High): a batter-runner put out BEFORE touching first base and one put out AFTER a safe hit while trying for more are different:
/// the first voids every run of the play (third out), the second is an ordinary non-force tag out, so a run that crossed the plate earlier counts
/// (general baseball principle, 2026 OBR as the approved fallback; the KBO text is unread, docs/game-engine.md section 5).
/// The "reached first" fact is not a flag: a hit gives the batter a base, and the out must be recorded AT a later base (<c>OutAt</c>) in the order of the play.
/// Situation: two outs, a1 on third, batter a4 (away); home fielders h1 CF, h2 SS, h3 RF, h4 1B, h6 LF, h7 3B, h8 2B, h9 C, pitcher hp.
/// All expected numbers are counted by hand.
/// </summary>
public class BatterRunnerAfterHitTests
{
    private static Script TwoOutsRunnerOnThird()
    {
        var s = new Script().Triple();                          // a1 triples: on third
        s.FlyOut(M(3, 3)).FlyOut(M(3, 3));                      // a2, a3 out, a1 holds
        Assert.Equal((2, 0), (s.State.Outs, s.State.AwayRuns));
        return s;
    }

    private static RunnerMove BatterOutAtSecond(int? sequence, int? outAt = 2, OutKind? kind = OutKind.Tag) =>
        new(0, 0, MoveCause.Play, kind, [A(FieldPosition.RightField), Po(FieldPosition.SecondBase)], sequence, outAt);

    // ---------------------------------------------------------------- Case 1: out before reaching first

    [Fact]
    public void A_batter_runner_put_out_before_first_base_ends_the_inning_with_no_run_and_no_hit()
    {
        var s = TwoOutsRunnerOnThird();
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.Out, [Seq(M(3, 4), 1)], [A(FieldPosition.Shortstop), Po(FieldPosition.FirstBase)], batterSequence: 2);

        var play = s.State.Plays.Last();
        Assert.Equal(0, s.State.AwayRuns);
        Assert.Equal((1, 1, 0, 0), (s.Stat("a4", StatKey.BatPA), s.Stat("a4", StatKey.BatAB), s.Stat("a4", StatKey.BatH), s.Stat("a4", StatKey.BatRBI)));
        Assert.Equal(OutClassification.BatterRunner, play.Moves.Single(m => m.From == 0).Classification);
        Assert.True(play.Moves.Single(m => m.From == 3).RunVoided);
        Assert.Equal(0, s.Stat("hp", StatKey.PitchR));
    }

    // ---------------------------------------------------------------- Cases 2 and 4: safe hit, then put out going for more

    [Fact]
    public void A_single_whose_batter_is_put_out_at_second_for_the_third_out_counts_the_run_that_crossed_first_and_records_one_hit_one_at_bat_one_out()
    {
        var s = TwoOutsRunnerOnThird();
        s.Approach();
        var step = GameReducer.Apply(s.State, new InPlayEvent("sx", BattedBallType.Line, InPlayResult.Single, Moves: [Seq(M(3, 4), 1), BatterOutAtSecond(2)]));
        Assert.True(step.IsSuccess, step.Error?.Message);
        var state = step.Value.State;

        // one plate appearance, one play, one fact: nothing is counted twice
        Assert.Single(step.Value.NewPlays);
        Assert.Single(step.Value.Facts);
        var fact = Assert.IsType<PlateAppearanceCompleted>(step.Value.Facts[0]);
        Assert.Equal((BatterResult.Single, 1), (fact.Result, fact.RunsScored));
        Assert.Equal(s.State.Plays.Count + 1, state.Plays.Count);
        Assert.Equal((InningHalf.Bottom, 0), (state.Half, state.Outs));       // the third out ended the half: bottom of the first, a fresh count of outs

        var stats = state.Stats;
        int Get(string player, StatKey key) => stats.Where(e => e.Player == P(player) && e.Key == key).Sum(e => e.Amount);
        Assert.Equal((1, 1, 1, 1, 1), (Get("a4", StatKey.BatPA), Get("a4", StatKey.BatAB), Get("a4", StatKey.BatH), Get("a4", StatKey.Bat1B), Get("a4", StatKey.BatRBI)));
        Assert.Equal((0, 0, 0), (Get("a4", StatKey.BatSO), Get("a4", StatKey.BatSF), Get("a4", StatKey.BatGIDP)));
        Assert.Equal((1, 1), (Get("a1", StatKey.RunR), Get("a1", StatKey.BatPA)));            // a1 tripled once (one PA) and scored now (one run)
        Assert.Equal((1, 1, 2, 3), (Get("hp", StatKey.PitchR), Get("hp", StatKey.PitchER), Get("hp", StatKey.PitchH), Get("hp", StatKey.PitchOuts))); // hits: the triple and the single
        Assert.Equal((1, 1), (Get("h8", StatKey.FldPO), Get("h3", StatKey.FldA)));       // second baseman putout, right fielder assist
        Assert.Equal((1, InningHalf.Bottom, 1), (state.Inning, state.Half, state.AwayRuns));

        var play = state.Plays.Last();
        var batter = play.Moves.Single(m => m.From == 0);
        Assert.Equal((0, OutClassification.NonForce, 2, 2), (batter.To, batter.Classification, batter.OutAt, batter.Sequence));
        Assert.False(play.Moves.Single(m => m.From == 3).RunVoided);
        Assert.False(play.IsDoublePlay);
        Assert.Equal(OutKind.Tag, batter.OutKind);

        // official record
        var box = BoxScoreBuilder.Build(state);
        Assert.Equal((1, 2, 0, 0), (box.Away.Runs, box.Away.Hits, box.Away.Errors, box.Away.LeftOnBase));  // the triple and the single; nobody left on
        Assert.Equal(1, state.AwayLine[0]);       // line score of the first inning: the one run
    }

    [Fact]
    public void The_same_single_with_the_out_first_and_the_run_after_it_gives_no_run_but_the_hit_and_the_at_bat_stand()
    {
        var s = TwoOutsRunnerOnThird();
        s.Approach().InPlay(BattedBallType.Line, InPlayResult.Single, [Seq(M(3, 4), 2), BatterOutAtSecond(1)]);

        var play = s.State.Plays.Last();
        Assert.Equal(0, s.State.AwayRuns);
        Assert.Equal((1, 1, 1, 0), (s.Stat("a4", StatKey.BatPA), s.Stat("a4", StatKey.BatAB), s.Stat("a4", StatKey.BatH), s.Stat("a4", StatKey.BatRBI)));
        Assert.Equal((0, 0, 0), (s.Stat("a1", StatKey.RunR), s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER)));
        Assert.True(play.Moves.Single(m => m.From == 3).RunVoided);
        Assert.Empty(play.Runs);
        var box = BoxScoreBuilder.Build(s.State);
        Assert.Equal((0, 2, 1), (box.Away.Runs, box.Away.Hits, box.Away.LeftOnBase));   // a1 crossed the plate without a counting run: left on base (OBR 9.03(c))
        Assert.Equal(3, s.Stat("hp", StatKey.PitchOuts));
        Assert.Equal((1, InningHalf.Bottom), (s.State.Inning, s.State.Half));
    }

    [Fact]
    public void A_double_or_triple_batter_can_also_be_put_out_stretching_it_and_a_batter_out_with_no_run_is_just_an_out()
    {
        // first batter of the game doubles and is thrown out at third: one hit, one at-bat, one out, nobody on base
        var s = new Script();
        s.Approach().InPlay(BattedBallType.Line, InPlayResult.Double, [new RunnerMove(0, 0, MoveCause.Play, OutKind.Tag, [A(FieldPosition.LeftField), Po(FieldPosition.ThirdBase)], null, 3)]);
        Assert.Equal((1, 1, 1, 1), (s.Stat("a1", StatKey.BatPA), s.Stat("a1", StatKey.BatAB), s.Stat("a1", StatKey.BatH), s.Stat("a1", StatKey.Bat2B)));
        Assert.Equal((1, 0, 1), (s.State.Outs, s.State.Bases.Count, s.Stat("hp", StatKey.PitchOuts)));
        Assert.Equal((1, 1), (s.Stat("h7", StatKey.FldPO), s.Stat("h6", StatKey.FldA)));
        Assert.Equal("a2", s.State.Away.CurrentBatterSlot.Player.Value);                  // the order moved on exactly once

        // a triple batter thrown out at home: allowed (home is base 4)
        var t = new Script();
        t.Approach().InPlay(BattedBallType.Line, InPlayResult.Triple, [new RunnerMove(0, 0, MoveCause.Play, OutKind.Tag, [A(FieldPosition.CenterField), Po(FieldPosition.Catcher)], null, 4)]);
        Assert.Equal((1, 1, 1), (t.Stat("a1", StatKey.Bat3B), t.State.Outs, t.Stat("h9", StatKey.FldPO)));
    }

    [Fact]
    public void A_runner_ahead_who_is_thrown_out_with_a_batter_out_after_a_hit_is_not_a_double_play_and_keeps_the_rbi()
    {
        var s = new Script().Triple();                          // a1 on third, no outs
        s.Approach().InPlay(BattedBallType.Line, InPlayResult.Single, [
            Seq(M(3, 4), 1),
            BatterOutAtSecond(2)]);                             // one out only: a run, a hit, the batter out at second
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.Single, [new RunnerMove(0, 0, MoveCause.Play, OutKind.Tag, [A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase)], 1, 2)]);

        Assert.Equal((2, 1), (s.State.Outs, s.State.AwayRuns));
        Assert.Equal(0, s.Stat("a3", StatKey.BatGIDP));         // a hit is never grounded into a double play
        Assert.Equal((1, 1, 1), (s.Stat("a2", StatKey.BatRBI), s.Stat("a2", StatKey.BatH), s.Stat("a1", StatKey.RunR)));
    }

    [Fact]
    public void A_ground_ball_hit_with_two_outs_on_the_play_is_not_a_grounded_into_double_play_and_the_run_keeps_its_rbi()
    {
        var s = new Script().Triple().Walk();                   // a1 on third, a2 on first, no outs; a3 bats
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.Single, [
            M(3, 4),                                                                                                  // a1 scores
            M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.RightField), Po(FieldPosition.ThirdBase)) with { OutAt = 3 },   // a2 thrown out at third
            BatterOutAtSecond(null)]);                                                                                // a3 safe at first, then out at second

        var play = s.State.Plays.Last();
        Assert.Equal((2, 1), (s.State.Outs, s.State.AwayRuns));
        Assert.Equal((1, 1, 1, 1, 0), (s.Stat("a3", StatKey.BatPA), s.Stat("a3", StatKey.BatAB), s.Stat("a3", StatKey.BatH), s.Stat("a3", StatKey.BatRBI), s.Stat("a3", StatKey.BatGIDP)));
        Assert.False(play.IsDoublePlay);
        Assert.Equal((0, 0), (s.Stat("h7", StatKey.FldDP), s.Stat("h8", StatKey.FldDP)));
        Assert.Equal((1, 1), (s.Stat("h7", StatKey.FldPO), s.Stat("h8", StatKey.FldPO)));
        Assert.Equal((OutClassification.NonForce, OutClassification.NonForce), (play.Moves.Single(m => m.From == 1).Classification, play.Moves.Single(m => m.From == 0).Classification));
    }

    // ---------------------------------------------------------------- Case 5: a claimed first-base arrival without the evidence

    [Fact]
    public void A_batter_out_on_a_hit_without_proof_that_he_reached_the_base_is_rejected_and_nothing_changes()
    {
        var s = TwoOutsRunnerOnThird();
        s.Approach();
        var before = s.State;
        System.Collections.Immutable.ImmutableList<PositionCredit> credits = [A(FieldPosition.RightField), Po(FieldPosition.SecondBase)];
        InPlayEvent Hit(string id, InPlayResult result, RunnerMove batter) => new(id, BattedBallType.Line, result, Moves: [batter]);

        // no out base, or an out at or before the base the hit gave him: no evidence he was ever safe
        Assert.Equal("Game.batter_reach_not_established", s.Try(Hit("a", InPlayResult.Single, new RunnerMove(0, 0, MoveCause.Play, OutKind.Tag, credits))).Error!.Value.Code);
        Assert.Equal("Game.batter_reach_not_established", s.Try(Hit("b", InPlayResult.Single, new RunnerMove(0, 0, MoveCause.Play, OutKind.Tag, credits, null, 1))).Error!.Value.Code);
        Assert.Equal("Game.batter_reach_not_established", s.Try(Hit("c", InPlayResult.Double, new RunnerMove(0, 0, MoveCause.Play, OutKind.Tag, credits, null, 2))).Error!.Value.Code);
        Assert.Equal("Game.batter_reach_not_established", s.Try(Hit("d", InPlayResult.Triple, new RunnerMove(0, 0, MoveCause.Play, OutKind.Tag, credits, null, 3))).Error!.Value.Code);
        Assert.Equal("Game.batter_reach_not_established", s.Try(Hit("e", InPlayResult.Single, new RunnerMove(0, 0, MoveCause.Play, OutKind.Tag, credits, null, 5))).Error!.Value.Code);
        // a force out is impossible for a batter who already stands on a base, and the kind is mandatory
        Assert.Equal("Game.out_kind_mismatch", s.Try(Hit("f", InPlayResult.Single, new RunnerMove(0, 0, MoveCause.Play, OutKind.Force, credits, null, 2))).Error!.Value.Code);
        Assert.Equal("Game.out_kind_missing", s.Try(Hit("g", InPlayResult.Single, new RunnerMove(0, 0, MoveCause.Play, null, credits, null, 2))).Error!.Value.Code);
        // a destination other than "out", or a batter move on results that do not allow one
        Assert.Equal("Game.batter_move_invalid", s.Try(Hit("h", InPlayResult.Single, new RunnerMove(0, 2, MoveCause.Play, null, null, null, 2))).Error!.Value.Code);
        Assert.Equal("Game.batter_move_invalid", s.Try(Hit("i", InPlayResult.HomeRun, new RunnerMove(0, 0, MoveCause.Play, OutKind.Tag, credits, null, 4))).Error!.Value.Code);
        Assert.Equal("Game.batter_move_invalid", s.Try(Hit("j", InPlayResult.Out, new RunnerMove(0, 0, MoveCause.Play, OutKind.Tag, credits, null, 2))).Error!.Value.Code);
        // a second order number that disagrees
        Assert.Equal("Game.play_order_invalid", s.Try(new InPlayEvent("k", BattedBallType.Line, InPlayResult.Single, Moves: [Seq(M(3, 4), 1), BatterOutAtSecond(2)], BatterSequence: 3)).Error!.Value.Code);

        Assert.Same(before, s.State);
        Assert.DoesNotContain(s.State.AppliedEvents, r => r.EventId.Length == 1);
    }

    [Fact]
    public void Without_an_order_a_third_out_after_a_hit_and_a_run_is_rejected_like_any_other_play()
    {
        var s = TwoOutsRunnerOnThird();
        s.Approach();
        var before = s.State;

        var result = s.Try(new InPlayEvent("x", BattedBallType.Line, InPlayResult.Single, Moves: [M(3, 4), BatterOutAtSecond(null)]));

        Assert.Equal("Game.play_order_required", result.Error!.Value.Code);
        Assert.Same(before, s.State);
    }

    // ---------------------------------------------------------------- Case 6: order is part of the event identity

    [Fact]
    public void Swapping_the_order_changes_the_result_and_the_receipt_fingerprint()
    {
        InPlayEvent Make(int run, int batter) => new("same", BattedBallType.Line, InPlayResult.Single, Moves: [Seq(M(3, 4), run), BatterOutAtSecond(batter)]);
        var runFirst = Make(1, 2);
        var outFirst = Make(2, 1);

        Assert.NotEqual(EventFingerprint.Receipt(runFirst).Fingerprint, EventFingerprint.Receipt(outFirst).Fingerprint);

        var s = TwoOutsRunnerOnThird();
        s.Approach();
        s.Ok(runFirst);
        Assert.Equal("Game.event_id_conflict", GameReducer.Apply(s.State, outFirst).Error!.Value.Code);   // same id, other order: a conflict, not a duplicate
        Assert.Equal(1, s.State.AwayRuns);
        var again = GameReducer.Apply(s.State, Make(1, 2));
        Assert.True(again.IsSuccess && again.Value.WasDuplicate);
    }
}
