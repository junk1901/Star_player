using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// Codex final focused review (High): a hit must not be recorded on a batted ball that forces out a preceding runner (2026 OBR 9.05(b)(1), docs/2026-official-baseball-rules.pdf
/// p.116-117, applied through the approved KBO-first / OBR-fallback policy; not a KBO certification). The force status of each out is derived from the state by
/// the order resolution, never taken from the batting result label. Expectations are written by hand.
/// </summary>
public class HitWithForceOutTests
{
    private static RunnerMove ForceOut(int from, FieldPosition assist, FieldPosition putout, OutMethod? method = null) =>
        M(from, 0, MoveCause.Play, OutKind.Force, A(assist), Po(putout)) with { Method = method };

    private static RunnerMove TagOutAt(int from, int outAt, FieldPosition assist, FieldPosition putout) =>
        M(from, 0, MoveCause.Play, OutKind.Tag, A(assist), Po(putout)) with { OutAt = outAt };

    /// <summary>Peter's runner (a1) on second, Roger's (a2) on first, Sierra (hb2) pitching, the next batter's approach already chosen.</summary>
    private static Script PeterSecondRogerFirstSierraPitching()
    {
        var s = new Script().Walk();
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");
        s.Walk();
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb2");
        s.Approach();
        return s;
    }

    private static readonly RunnerMove ForcedAtSecond = ForceOut(1, FieldPosition.Shortstop, FieldPosition.SecondBase);
    private static readonly RunnerMove ThrownOutAtHome = TagOutAt(2, 4, FieldPosition.Shortstop, FieldPosition.Catcher);

    private static void AssertRefusedUntouched(Script s, string id, InPlayResult result, params RunnerMove[] moves)
    {
        var before = s.State;
        var e = new InPlayEvent(id, BattedBallType.Ground, result, false, moves.ToImmutableList());

        var first = s.Try(e);
        var second = s.Try(e);

        Assert.Equal("Game.hit_with_force_out", first.Error!.Value.Code);
        Assert.Equal(first.Error!.Value, second.Error!.Value);
        Assert.Same(before, s.State);
        Assert.DoesNotContain(s.State.AppliedEvents, r => r.EventId == id);
    }

    // ---- Case A: the Codex reproduction entered as a single

    [Fact]
    public void A_single_on_a_batted_ball_that_forces_out_a_preceding_runner_is_refused_and_nothing_is_recorded()
    {
        var s = PeterSecondRogerFirstSierraPitching();
        var slots = (s.State.Bases.First!.ResponsiblePitcher.Value, s.State.Bases.Second!.ResponsiblePitcher.Value);

        AssertRefusedUntouched(s, "hit1", InPlayResult.Single, ForcedAtSecond, ThrownOutAtHome);

        Assert.Equal(("hb1", "hp"), slots);
        Assert.Equal(2, s.State.Plays.Count); // the two walks only: no play was appended
        Assert.Equal((0, 0), (s.State.Outs, s.State.AwayRuns));
        Assert.Equal((0, 0, 0), (s.Stat("a3", StatKey.BatH), s.Stat("a3", StatKey.BatAB), s.Stat("a3", StatKey.BatPA)));
        Assert.Equal(("hb1", "hp"), (s.State.Bases.First!.ResponsiblePitcher.Value, s.State.Bases.Second!.ResponsiblePitcher.Value));
    }

    [Theory]
    [InlineData(InPlayResult.Single)]
    [InlineData(InPlayResult.Double)]
    [InlineData(InPlayResult.Triple)]
    public void Every_hit_kind_is_refused_when_a_runner_is_forced_out_on_the_same_batted_ball(InPlayResult hit)
    {
        var s = new Script().Walk();                                // a1 on first
        s.Approach();

        AssertRefusedUntouched(s, "hit2", hit, ForceOut(1, FieldPosition.Shortstop, FieldPosition.SecondBase));
        Assert.Equal((0, 0), (s.Stat("a2", StatKey.BatH), s.Stat("a2", StatKey.BatAB)));
    }

    // ---- Case B: the fielder's-choice route keeps the C33 refusal, so relabelling cannot bypass it

    [Fact]
    public void The_same_play_entered_as_a_fielders_choice_still_gets_the_responsibility_refusal()
    {
        var s = PeterSecondRogerFirstSierraPitching();
        var before = s.State;

        var fc = s.Try(new InPlayEvent("fc1", BattedBallType.Ground, InPlayResult.FieldersChoice, false, [ForcedAtSecond, ThrownOutAtHome]));

        Assert.Equal("Game.ambiguous_responsibility", fc.Error!.Value.Code);
        Assert.Same(before, s.State);
    }

    // ---- Case C: a forced runner who is tagged is still a force out

    [Fact]
    public void A_forced_runner_put_out_by_a_tag_is_still_a_force_out_and_the_hit_is_refused()
    {
        var s = new Script().Walk();
        s.Approach();

        AssertRefusedUntouched(s, "hit3", InPlayResult.Single, ForceOut(1, FieldPosition.Shortstop, FieldPosition.SecondBase, OutMethod.Tag));
    }

    // ---- Case D: a force removed by an earlier out is respected, the out is no force out

    [Fact]
    public void A_force_removed_by_an_earlier_out_leaves_the_later_out_a_tag_out_and_the_hit_stands_in_that_order_only()
    {
        // a1 second, a2 first, single by a3: a2 is out going for third FIRST (a tag, he was not put out at the base he was forced to), then a1 is out at third:
        // the force on a1 was removed by the earlier out of the runner behind him, so no runner is forced out and the single is not refused by this rule
        var removed = new Script().Walk().Walk().Approach();
        removed.InPlay(BattedBallType.Line, InPlayResult.Single,
        [
            TagOutAt(1, 3, FieldPosition.Shortstop, FieldPosition.ThirdBase) with { Sequence = 1 },
            TagOutAt(2, 3, FieldPosition.RightField, FieldPosition.ThirdBase) with { Sequence = 2 },
        ]);

        Assert.Equal((1, 1, 2), (removed.Stat("a3", StatKey.BatH), removed.Stat("a3", StatKey.BatAB), removed.State.Outs));
        Assert.Equal(OutClassification.NonForce, removed.State.Plays.Last().Moves.Single(m => m.From == 2).Classification);

        // the same two outs in the other order: a1 is forced out at third first (a force out) and the single is refused
        var forced = new Script().Walk().Walk().Approach();
        AssertRefusedUntouched(forced, "hit4", InPlayResult.Single,
            ForceOut(2, FieldPosition.Shortstop, FieldPosition.ThirdBase) with { Sequence = 1 },
            TagOutAt(1, 3, FieldPosition.RightField, FieldPosition.ThirdBase) with { Sequence = 2 });
    }

    // ---- Case E: legal hits are unchanged

    [Fact]
    public void Hits_without_a_force_out_keep_their_records_including_a_runner_thrown_out_on_his_own_advance()
    {
        var s = new Script().Double().Walk();                       // a1 second, a2 first
        s.Single(M(2, 4), TagOutAt(1, 3, FieldPosition.RightField, FieldPosition.ThirdBase));   // a3 singles: a1 scores, a2 thrown out going for third

        Assert.Equal((1, 1, 1), (s.Stat("a3", StatKey.BatH), s.Stat("a3", StatKey.Bat1B), s.Stat("a3", StatKey.BatAB)));
        Assert.Equal((1, 1, 1), (s.Stat("a3", StatKey.BatRBI), s.State.Outs, s.State.AwayRuns));

        var triple = new Script().Walk().Triple(M(1, 4));
        Assert.Equal((1, 1), (triple.Stat("a2", StatKey.Bat3B), triple.Stat("a2", StatKey.BatRBI)));
    }

    [Fact]
    public void A_batter_put_out_stretching_a_hit_is_not_a_force_out_of_a_preceding_runner()
    {
        var s = new Script().Walk().Approach();
        s.InPlay(BattedBallType.Line, InPlayResult.Single, [M(1, 2), M(0, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.RightField), Po(FieldPosition.SecondBase)) with { OutAt = 2 }]);

        Assert.Equal((1, 1), (s.Stat("a2", StatKey.BatH), s.State.Outs));
    }

    // ---- Case F: determinism and idempotency

    [Fact]
    public void An_accepted_hit_is_a_harmless_duplicate_when_delivered_again()
    {
        var s = new Script().Walk().Approach();
        var hit = new InPlayEvent("h1", BattedBallType.Line, InPlayResult.Single, false, [M(1, 2)]);
        s.Ok(hit);
        var after = s.State;

        var again = GameReducer.Apply(after, hit);

        Assert.True(again.IsSuccess && again.Value.WasDuplicate);
        Assert.Same(after, again.Value.State);
    }
}
