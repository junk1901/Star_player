using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// Medium 4 of the Codex review: a hit by pitch makes the ball dead. It can never carry a wild pitch, a passed ball or a dropped third strike,
/// and the only runner movement is the plain forced advance. (The dead-ball principle is the general baseball rule; the KBO text is not
/// certified, see docs/game-engine.md section 5.) Expectations are worked out by hand.
/// </summary>
public class HitByPitchTests
{
    [Fact]
    public void A_hit_by_pitch_with_empty_bases_puts_the_batter_on_first_without_a_hit_or_an_at_bat()
    {
        var s = new Script().HitByPitch();

        Assert.Equal("a1", s.State.Bases.First!.Player.Value);
        Assert.Equal((1, 0, 1, 0), (s.Stat("a1", StatKey.BatPA), s.Stat("a1", StatKey.BatAB), s.Stat("a1", StatKey.BatHBP), s.Stat("a1", StatKey.BatH)));
        Assert.Equal((1, 1, 1), (s.Stat("hp", StatKey.PitchHBP), s.Stat("hp", StatKey.PitchBF), s.Stat("hp", StatKey.PitchPitches)));
        Assert.Equal((0, 0, 0), (s.State.AwayRuns, s.State.Outs, s.State.Count.Balls));
        Assert.Equal(BatterResult.HitByPitch, s.State.Plays.Last().Result);
    }

    [Fact]
    public void A_hit_by_pitch_forces_only_the_runners_it_displaces()
    {
        var s = new Script().Single();                         // a1 on first
        s.HitByPitch();                                         // a2: a1 is forced to second
        Assert.Equal(("a2", "a1", null), (s.State.Bases.First!.Player.Value, s.State.Bases.Second!.Player.Value, s.State.Bases.Third?.Player.Value));
        Assert.Equal(MoveCause.Forced, s.State.Plays.Last().Moves.Single(m => m.From == 1).Cause);

        var t = new Script().Triple();                          // a1 on third only
        t.HitByPitch();                                         // a2 on first, a1 is not displaced
        Assert.Equal(("a2", null, "a1"), (t.State.Bases.First!.Player.Value, t.State.Bases.Second?.Player.Value, t.State.Bases.Third!.Player.Value));
        Assert.Equal(0, t.State.AwayRuns);

        var gap = new Script().Triple().Single(M(3, 3));        // a1 on third, a2 on first
        gap.Runner(RunnerPlayKind.Steal, M(1, 2, MoveCause.Steal));
        gap.HitByPitch();                                       // a3: first is empty so the runner on second is not forced
        Assert.Equal(("a3", "a2", "a1"), (gap.State.Bases.First!.Player.Value, gap.State.Bases.Second!.Player.Value, gap.State.Bases.Third!.Player.Value));
        Assert.Equal(0, gap.State.AwayRuns);
    }

    [Fact]
    public void A_bases_loaded_hit_by_pitch_forces_in_a_run_with_an_RBI_and_leaves_the_bases_loaded()
    {
        var s = new Script().Single().Single(M(1, 2)).Single(M(2, 3), M(1, 2));  // a1 third, a2 second, a3 first
        s.HitByPitch();                                                           // a4

        Assert.Equal((1, 1, 1), (s.State.AwayRuns, s.Stat("a4", StatKey.BatRBI), s.Stat("a1", StatKey.RunR)));
        Assert.Equal(("a4", "a3", "a2"), (s.State.Bases.First!.Player.Value, s.State.Bases.Second!.Player.Value, s.State.Bases.Third!.Player.Value));
        Assert.Equal((1, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchHBP)));
        Assert.Equal((1, 0), (s.Stat("a4", StatKey.BatHBP), s.Stat("a4", StatKey.BatAB)));
    }

    [Theory]
    [InlineData(PitchMishap.WildPitch)]
    [InlineData(PitchMishap.PassedBall)]
    public void A_hit_by_pitch_cannot_be_combined_with_a_wild_pitch_or_passed_ball_and_nothing_is_recorded(PitchMishap mishap)
    {
        var s = new Script().Single();                           // a1 on first
        s.Approach();
        var before = s.State;
        var cause = mishap == PitchMishap.WildPitch ? MoveCause.WildPitch : MoveCause.PassedBall;

        var withMove = s.Try(new PitchEvent("x1", new PitchSpec(PitchOutcome.HitByPitch, mishap, [M(1, 3, cause)])));
        var withoutMove = s.Try(new PitchEvent("x2", new PitchSpec(PitchOutcome.HitByPitch, mishap)));

        Assert.All(new[] { withMove, withoutMove }, r => Assert.Equal("Game.hbp_with_mishap", r.Error!.Value.Code));
        Assert.Same(before, s.State);

        var t = new Script().Single();                           // the one-pitch Take is held to the same rule
        var beforeTake = t.State;
        var take = t.Try(new TakeEvent("x3", new PitchSpec(PitchOutcome.HitByPitch, mishap, [M(1, 3, cause)])));
        Assert.Equal("Game.hbp_with_mishap", take.Error!.Value.Code);
        Assert.Same(beforeTake, t.State);
        Assert.DoesNotContain(s.State.AppliedEvents, r => r.EventId.StartsWith('x'));
    }

    [Fact]
    public void A_hit_by_pitch_cannot_be_a_dropped_third_strike_or_carry_arbitrary_extra_advances()
    {
        var s = new Script().Single();
        s.Approach();
        var before = s.State;

        Assert.Equal("Game.hbp_with_mishap", s.Try(new PitchEvent("x1", new PitchSpec(PitchOutcome.HitByPitch, DroppedThirdStrike: PitchMishap.WildPitch))).Error!.Value.Code);
        Assert.Equal("Game.hbp_extra_advance_invalid", s.Try(new PitchEvent("x2", new PitchSpec(PitchOutcome.HitByPitch, Moves: [M(1, 3)]))).Error!.Value.Code);
        Assert.Equal("Game.hbp_extra_advance_invalid", s.Try(new PitchEvent("x3", new PitchSpec(PitchOutcome.HitByPitch, Moves: [M(1, 2, MoveCause.Forced)]))).Error!.Value.Code);
        Assert.Equal("Game.hbp_extra_advance_invalid", s.Try(new PitchEvent("x4", new PitchSpec(PitchOutcome.HitByPitch, Moves: [M(1, 4)]))).Error!.Value.Code);
        Assert.Same(before, s.State);

        s.Ok(new PitchEvent("ok", new PitchSpec(PitchOutcome.HitByPitch)));   // the plain hit by pitch is accepted
        Assert.Equal("a1", s.State.Bases.Second!.Player.Value);
    }

    [Fact]
    public void Ball_four_that_gets_away_may_still_carry_a_wild_pitch_so_the_dead_ball_rule_is_specific_to_the_hit_by_pitch()
    {
        var s = new Script().Single().Single(M(1, 2));          // a1 on second, a2 on first
        s.Approach().Ball().Ball().Ball();
        s.Pitch(PitchOutcome.Ball, PitchMishap.WildPitch, [M(2, 3, MoveCause.WildPitch)]); // ball four, a1 takes third on the wild pitch

        Assert.Equal(("a3", "a2", "a1"), (s.State.Bases.First!.Player.Value, s.State.Bases.Second!.Player.Value, s.State.Bases.Third!.Player.Value));
        Assert.Equal((1, 1, 1), (s.Stat("hp", StatKey.PitchWP), s.Stat("hp", StatKey.PitchBB), s.Stat("a3", StatKey.BatBB)));
    }

    [Fact]
    public void A_strikeout_credit_list_is_only_for_a_batter_who_really_strikes_out()
    {
        var s = new Script();
        s.Approach();
        var before = s.State;

        var result = s.Try(new PitchEvent("x1", new PitchSpec(PitchOutcome.Ball, StrikeoutCredits: [Po(FieldPosition.Catcher)])));

        Assert.Equal("Game.strikeout_credits_invalid", result.Error!.Value.Code);
        Assert.Same(before, s.State);
    }
}
