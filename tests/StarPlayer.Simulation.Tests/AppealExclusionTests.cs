using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// Owner decision of 2026-10-11: appeal plays are not part of Star Player (missed base, failed retouch, appeal third outs, fourth-out appeals). A game
/// state assumes every runner touched the bases legally. An appeal that is entered explicitly is refused as unsupported, and the engine never turns
/// an appeal situation into an ordinary tag out. This is a game simplification, not a statement about KBO rules (docs/game-engine.md section 4a).
/// </summary>
public class AppealExclusionTests
{
    private static PositionCredit[] Cr(FieldPosition assist, FieldPosition putout) => [A(assist), Po(putout)];

    [Fact]
    public void An_appeal_request_is_refused_as_unsupported_and_the_state_is_unchanged()
    {
        var s = new Script().Single();                           // a1 on first
        var before = s.State;

        var result = s.Try(new RunnerPlayEvent("ap", RunnerPlayKind.Appeal, [M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.FirstBase))]));

        Assert.Equal("Game.appeal_not_supported", result.Error!.Value.Code);
        Assert.Same(before, s.State);
        Assert.DoesNotContain(s.State.AppliedEvents, r => r.EventId == "ap");
    }

    [Fact]
    public void An_out_that_is_neither_a_force_out_nor_a_tag_out_is_not_accepted_for_a_runner()
    {
        var s = new Script().Single();
        var before = s.State;

        var other = s.Try(new RunnerPlayEvent("o1", RunnerPlayKind.Other, [M(1, 0, MoveCause.Play, OutKind.Other, A(FieldPosition.Shortstop), Po(FieldPosition.FirstBase)) with { OutAt = 1 }]));

        Assert.Equal("Game.appeal_not_supported", other.Error!.Value.Code);
        Assert.Same(before, s.State);
    }

    [Fact]
    public void A_runner_put_out_back_at_his_own_base_after_a_caught_ball_is_a_retouch_appeal_and_is_refused_instead_of_becoming_a_tag_out()
    {
        var s = new Script().Single();                           // a1 on first, no outs
        s.Approach();
        var before = s.State;

        var doubledOff = s.Try(new InPlayEvent("x1", BattedBallType.Fly, InPlayResult.Out,
            Moves: [M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.CenterField), Po(FieldPosition.FirstBase)) with { OutAt = 1 }], BatterCredits: [Po(FieldPosition.CenterField)]));

        Assert.Equal("Game.appeal_not_supported", doubledOff.Error!.Value.Code);
        Assert.Same(before, s.State);

        // a runner who tagged up and was thrown out at the next base is an ordinary tag out and stays supported
        s.Ok(new InPlayEvent("x2", BattedBallType.Fly, InPlayResult.Out,
            Moves: [M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.CenterField), Po(FieldPosition.SecondBase))], BatterCredits: [Po(FieldPosition.CenterField)]));
        Assert.Equal(2, s.State.Outs);
    }

    [Fact]
    public void Retiring_a_runner_who_is_not_forced_by_touching_the_base_is_an_appeal_and_is_refused()
    {
        var s = new Script().Triple().Walk();                    // a1 on third, a2 on first
        s.FlyOut(M(3, 3), M(1, 1)).FlyOut(M(3, 3), M(1, 1));
        s.Approach();
        var before = s.State;

        // the batter hit safely and is "put out" by a fielder touching a base: only an appeal can do that to a runner who is not forced
        var batterByTouch = s.Try(new InPlayEvent("x1", BattedBallType.Line, InPlayResult.Single,
            Moves: [M(1, 1), Seq(M(3, 4), 1), Seq(new RunnerMove(0, 0, MoveCause.Play, OutKind.Tag, Cr(FieldPosition.RightField, FieldPosition.SecondBase).ToImmutableList(), 2, 2, OutMethod.BaseTouch), 2, 2, OutMethod.BaseTouch)]));
        Assert.Equal("Game.appeal_not_supported", batterByTouch.Error!.Value.Code);

        var runnerByTouch = s.Try(new InPlayEvent("x2", BattedBallType.Line, InPlayResult.Single,
            Moves: [Seq(M(3, 4), 1), Seq(M(1, 0, MoveCause.Play, OutKind.Tag, Cr(FieldPosition.RightField, FieldPosition.ThirdBase)), 2, 3, OutMethod.BaseTouch)]));
        Assert.Equal("Game.out_method_invalid", runnerByTouch.Error!.Value.Code);   // tag out declared together with a base touch: contradictory
        Assert.Same(before, s.State);
    }

    [Fact]
    public void A_pickoff_tag_at_the_runners_own_base_between_pitches_is_not_an_appeal_and_stays_supported()
    {
        var s = new Script().Single();
        s.Runner(RunnerPlayKind.Pickoff, M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Pitcher), Po(FieldPosition.FirstBase)) with { OutAt = 1 });

        Assert.Equal((1, 0), (s.State.Outs, s.State.Bases.Count));
        Assert.Equal(1, s.Stat("a1", StatKey.RunPicked));
    }

    [Fact]
    public void The_generated_games_never_contain_an_appeal_and_nothing_in_the_event_model_creates_one_by_itself()
    {
        // every RunnerPlayKind except Appeal is a supported play; the only way to name an appeal is the explicit refused kind
        Assert.Equal(["Appeal", "Balk", "Other", "Pickoff", "Steal"], Enum.GetNames<RunnerPlayKind>().Order().ToArray());
        Assert.Equal(["Force", "Other", "Tag"], Enum.GetNames<OutKind>().Order().ToArray());
    }
}
