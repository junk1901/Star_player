using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// Reproductions of the Codex Phase 2 review findings. Written first against the pre-fix reducer (they failed there, results are
/// recorded in docs/reports/PHASE_2_REPORT.md) and kept as regression tests: each asserts the CORRECT behaviour.
/// </summary>
public class CodexReproTests
{
    [Fact]
    public void Repro_1_a_forced_runner_put_out_for_the_third_out_cannot_be_declared_a_plain_tag_so_that_a_run_still_counts()
    {
        var s = new Script().Single().Single(M(1, 3));                 // a1 on third, a2 on first
        s.FlyOut(M(3, 3), M(1, 1)).FlyOut(M(3, 3), M(1, 1));            // two outs, runners hold
        s.Approach();
        var result = s.Try(new InPlayEvent("rx", BattedBallType.Ground, InPlayResult.FieldersChoice, false,
            [Seq(M(3, 4), 1), Seq(M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase)), 2)]));

        // The out at second is a force out, so no run may count: the declared Tag is rejected and nothing changes.
        Assert.Equal("Game.out_kind_mismatch", result.Error!.Value.Code);
        Assert.Equal((0, 2), (s.State.AwayRuns, s.State.Outs));
    }

    [Fact]
    public void Repro_2_a_runner_out_without_a_declared_out_kind_is_rejected()
    {
        var s = new Script().Single();
        var result = s.Try(new RunnerPlayEvent("rx", RunnerPlayKind.Steal, [M(1, 0, MoveCause.Steal, null, A(FieldPosition.Catcher), Po(FieldPosition.SecondBase))]));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Repro_3_the_same_event_id_with_different_content_is_a_conflict_not_a_duplicate()
    {
        var s = new Script();
        s.Ok(new ApproachEvent("same-id", ApproachKind.Normal));
        var before = s.State;

        var result = s.Try(new PitchEvent("same-id", new PitchSpec(PitchOutcome.Ball)));

        Assert.False(result.IsSuccess);
        Assert.Same(before, s.State);
    }

    [Fact]
    public void Repro_4_a_hit_by_pitch_cannot_be_combined_with_a_wild_pitch()
    {
        var s = new Script().Single();
        s.Approach();
        var result = s.Try(new PitchEvent("rx", new PitchSpec(PitchOutcome.HitByPitch, PitchMishap.WildPitch, [M(1, 3, MoveCause.WildPitch)])));

        Assert.False(result.IsSuccess);
    }
}
