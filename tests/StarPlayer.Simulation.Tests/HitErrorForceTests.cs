using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// C43 (2026 OBR 9.05(b)(1), second clause: no base hit when a runner "would have been forced out except for a fielding error"). What the input can prove is the scorer's
/// explicit cause <see cref="MoveCause.ErrorSavedOut"/> plus the force status derived from the state; an error that no move explains cannot be tied to a force out,
/// so a hit with a runner on first and such an error is refused as unsupported instead of being credited or reclassified. Expectations are written by hand.
/// </summary>
public class HitErrorForceTests
{
    private static InPlayEvent Hit(string id, InPlayResult result, FieldPosition[]? errors, params RunnerMove[] moves) =>
        new(id, BattedBallType.Ground, result, false, moves.ToImmutableList(), null, errors?.ToImmutableList());

    private static void AssertRefused(Script s, string code, InPlayEvent e)
    {
        var before = s.State;
        var first = s.Try(e);
        var second = s.Try(e);

        Assert.Equal(code, first.Error!.Value.Code);
        Assert.Equal(first.Error!.Value, second.Error!.Value);
        Assert.Same(before, s.State);
        Assert.DoesNotContain(s.State.AppliedEvents, r => r.EventId == e.Id);
    }

    private static RunnerMove TagOutAt(int from, int outAt, FieldPosition assist, FieldPosition putout) =>
        M(from, 0, MoveCause.Play, OutKind.Tag, A(assist), Po(putout)) with { OutAt = outAt };

    // ---- the confirmed difference: the force out was prevented by an error, the scorer says so

    [Fact]
    public void A_hit_is_refused_when_an_error_saved_a_forced_runner_from_being_forced_out()
    {
        // a double or triple puts the batter on second or third, where a first-base runner saved at the forced base (second) cannot stand: that input is already invalid
        const InPlayResult hit = InPlayResult.Single;
        var s = new Script().Walk().Approach();                     // a1 on first, a2 batting

        AssertRefused(s, "Game.hit_with_force_out_saved_by_error", Hit("c43-1", hit, [FieldPosition.Shortstop], M(1, 2, MoveCause.ErrorSavedOut)));

        Assert.Equal((0, 0, 0), (s.Stat("a2", StatKey.BatH), s.Stat("a2", StatKey.BatAB), s.Stat("a2", StatKey.BatPA)));
        Assert.Equal(0, s.Stat("h6", StatKey.FldE) + s.Stat("h2", StatKey.FldE));      // no error credited either
        Assert.Equal("hp", s.State.Bases.First!.ResponsiblePitcher.Value);               // slot untouched
        Assert.Equal((0, 0), (s.State.Outs, s.State.AwayRuns));
    }

    [Fact]
    public void The_official_alternative_is_not_chosen_for_the_scorer_and_the_same_play_as_a_reach_on_error_is_accepted_as_entered()
    {
        // the engine never relabels; the scorer's own classification of the same ball as a reach on error is a different event that it checks as before (limit: section 15, C42)
        var s = new Script().Walk().Approach();
        var asError = Hit("c43-2", InPlayResult.ReachedOnError, [FieldPosition.Shortstop], M(1, 2, MoveCause.ErrorSavedOut));

        Assert.True(s.Try(asError).IsSuccess);
        Assert.Equal((0, 1, 1), (s.Stat("a2", StatKey.BatH), s.Stat("a2", StatKey.BatROE), s.Stat("a2", StatKey.BatAB)));
    }

    // ---- a runner saved from an out that was NOT a force out keeps the hit

    [Fact]
    public void A_runner_saved_by_an_error_from_a_non_force_out_does_not_take_the_hit_away()
    {
        var s = new Script().Double().Approach();                   // a1 on second, first base empty: nobody is forced
        s.Ok(Hit("c43-14", InPlayResult.Single, [FieldPosition.RightField], M(2, 3, MoveCause.ErrorSavedOut)));   // a1 would have been out at third (a tag play), saved by the error

        Assert.Equal((1, 1, 0), (s.Stat("a2", StatKey.BatH), s.Stat("a2", StatKey.BatAB), s.State.Outs));
    }

    // ---- force removed by an earlier out is respected, in both orders

    [Fact]
    public void A_force_removed_by_an_earlier_real_out_leaves_the_error_saved_runner_a_non_force_case_and_the_hit_stands()
    {
        var s = new Script().Walk().Walk().Approach();              // a1 second, a2 first
        s.Ok(Hit("c43-3", InPlayResult.Single, [FieldPosition.ThirdBase],
            TagOutAt(1, 3, FieldPosition.RightField, FieldPosition.ThirdBase) with { Sequence = 1 },    // a2 out going for third first: the force on a1 is gone
            M(2, 3, MoveCause.ErrorSavedOut) with { Sequence = 2 }));                                    // a1 would have been out at third, saved by the error: not a force case

        Assert.Equal((1, 1, 1), (s.Stat("a3", StatKey.BatH), s.Stat("a3", StatKey.BatAB), s.State.Outs));
    }

    [Fact]
    public void The_same_two_moves_in_the_other_order_keep_the_force_on_the_saved_runner_and_the_hit_is_refused()
    {
        var s = new Script().Walk().Walk().Approach();
        AssertRefused(s, "Game.hit_with_force_out_saved_by_error", Hit("c43-4", InPlayResult.Single, [FieldPosition.ThirdBase],
            M(2, 3, MoveCause.ErrorSavedOut) with { Sequence = 1 },
            TagOutAt(1, 3, FieldPosition.RightField, FieldPosition.ThirdBase) with { Sequence = 2 }));
    }

    // ---- information missing: an error that no move explains while a runner is forced

    [Fact]
    public void A_hit_with_a_runner_on_first_and_an_error_that_no_move_explains_is_refused_as_unsupported()
    {
        var s = new Script().Walk().Approach();                     // the plain forced advance of a1 might hide a force out the error prevented: the input cannot say

        AssertRefused(s, "Game.hit_error_cause_unclear", Hit("c43-5", InPlayResult.Single, [FieldPosition.RightField], M(1, 2)));

        Assert.Equal((0, 0), (s.Stat("a2", StatKey.BatH), s.Stat("a2", StatKey.BatAB)));
    }

    // ---- no over-rejection

    [Fact]
    public void A_single_without_a_runner_on_first_keeps_its_hit_whatever_error_is_recorded_on_the_play()
    {
        var empty = new Script().Approach();
        empty.Ok(Hit("c43-6", InPlayResult.Single, [FieldPosition.LeftField]));

        var second = new Script().Double().Approach();             // runner on second only: no force is possible
        second.Ok(Hit("c43-7", InPlayResult.Single, [FieldPosition.LeftField], M(2, 3)));

        Assert.Equal((1, 1), (empty.Stat("a1", StatKey.BatH), second.Stat("a2", StatKey.BatH)));
    }

    [Fact]
    public void A_single_with_a_runner_on_first_and_an_error_that_a_move_explains_keeps_its_hit()
    {
        var s = new Script().Double().Walk().Approach();           // a1 second, a2 first
        s.Ok(Hit("c43-8", InPlayResult.Single, [FieldPosition.RightField], M(2, 4, MoveCause.ErrorExtraBase), M(1, 2)));   // a1 scores on the error in right, a2 forced up normally

        Assert.Equal((1, 1, 1), (s.Stat("a3", StatKey.BatH), s.Stat("a3", StatKey.BatAB), s.State.AwayRuns));
        Assert.Equal(1, s.Stat("h3", StatKey.FldE));
    }

    [Fact]
    public void A_single_with_a_runner_on_first_and_no_error_at_all_keeps_its_hit()
    {
        var s = new Script().Walk().Approach();
        s.Ok(Hit("c43-9", InPlayResult.Single, null, M(1, 2)));

        Assert.Equal((1, 1), (s.Stat("a2", StatKey.BatH), s.Stat("a2", StatKey.BatAB)));
    }

    // ---- other results are not touched, replay is idempotent

    [Fact]
    public void A_fielders_choice_or_reach_on_error_with_the_same_moves_is_not_judged_by_the_hit_rule()
    {
        var fc = new Script().Walk().Approach();
        Assert.True(fc.Try(Hit("c43-10", InPlayResult.FieldersChoice, null, M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase)))).IsSuccess);

        var roe = new Script().Walk().Approach();
        Assert.True(roe.Try(Hit("c43-11", InPlayResult.ReachedOnError, [FieldPosition.RightField], M(1, 2))).IsSuccess);
    }

    [Fact]
    public void An_accepted_single_with_an_explained_error_is_a_harmless_duplicate_and_the_refused_one_never_is()
    {
        var s = new Script().Double().Walk().Approach();
        var ok = Hit("c43-12", InPlayResult.Single, [FieldPosition.RightField], M(2, 4, MoveCause.ErrorExtraBase), M(1, 2));
        s.Ok(ok);
        var after = s.State;

        var again = GameReducer.Apply(after, ok);
        Assert.True(again.IsSuccess && again.Value.WasDuplicate);
        Assert.Same(after, again.Value.State);

        var refused = new Script().Walk().Approach();
        var bad = Hit("c43-13", InPlayResult.Single, [FieldPosition.Shortstop], M(1, 2, MoveCause.ErrorSavedOut));
        Assert.Equal("Game.hit_with_force_out_saved_by_error", GameReducer.Apply(refused.State, bad).Error!.Value.Code);
        Assert.Equal("Game.hit_with_force_out_saved_by_error", GameReducer.Apply(refused.State, bad).Error!.Value.Code);
    }
}
