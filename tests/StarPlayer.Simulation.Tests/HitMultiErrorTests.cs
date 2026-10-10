using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// C44 (multi-error gap). The event lists the fielding positions of the errors and each move carries a cause, but nothing ties an error to the move it caused, so counting
/// error-caused moves proves nothing once a play has two or more errors. With a runner on first (a forced runner exists) such a hit is refused as unsupported
/// (<c>Game.hit_error_cause_unclear</c>): a Phase 2 support limit of the input model, not a statement that the play is illegal in baseball. Expectations are written by hand.
/// </summary>
public class HitMultiErrorTests
{
    private static InPlayEvent Hit(string id, InPlayResult result, FieldPosition[]? errors, params RunnerMove[] moves) =>
        new(id, BattedBallType.Ground, result, false, moves.ToImmutableList(), null, errors?.ToImmutableList());

    private static readonly FieldPosition[] TwoErrors = [FieldPosition.RightField, FieldPosition.CenterField];

    // refused: same error twice, original instance untouched, no receipt, no statistic, no responsibility slot change
    private static void AssertRefused(Script s, InPlayEvent e)
    {
        var before = s.State;
        var first = s.Try(e);
        var second = s.Try(e);

        Assert.False(first.IsSuccess, "the multi-error hit was accepted");
        Assert.Equal("Game.hit_error_cause_unclear", first.Error!.Value.Code);
        Assert.Equal(first.Error!.Value, second.Error!.Value);
        Assert.Same(before, s.State);
        Assert.DoesNotContain(s.State.AppliedEvents, r => r.EventId == e.Id);
        Assert.Equal(0, Errors(s));
    }

    private static int Errors(Script s) => s.State.Stats.Where(e => e.Key == StatKey.FldE).Sum(e => e.Amount);

    // A: runner on first + Single + two errors + one ErrorExtraBase move
    [Fact]
    public void A_single_with_a_runner_on_first_two_errors_and_one_error_move_is_refused()
    {
        var s = new Script().Double().Walk().Approach();             // a1 second, a2 first, a3 batting
        AssertRefused(s, Hit("c44m-A", InPlayResult.Single, TwoErrors, M(2, 4, MoveCause.ErrorExtraBase), M(1, 2)));
        Assert.Equal((0, 0, 0), (s.Stat("a3", StatKey.BatH), s.Stat("a3", StatKey.BatAB), s.State.AwayRuns));
        Assert.Equal(("a1", "a2"), (s.State.Bases.Second!.Player.Value, s.State.Bases.First!.Player.Value));   // nobody moved
    }

    // B: runner on first + Double / Triple + two errors + one ErrorExtraBase move
    [Theory]
    [InlineData(InPlayResult.Double)]
    [InlineData(InPlayResult.Triple)]
    public void A_double_or_triple_with_a_runner_on_first_two_errors_and_one_error_move_is_refused(InPlayResult hit)
    {
        var s = new Script().Walk().Approach();                      // a1 on first, a2 batting
        AssertRefused(s, Hit("c44m-B", hit, TwoErrors, M(1, 4, MoveCause.ErrorExtraBase)));
        Assert.Equal((0, 0, 0), (s.Stat("a2", StatKey.BatH), s.Stat("a2", StatKey.BatAB), s.State.AwayRuns));
    }

    // C: runner on first + two errors + two cause moves, no per-error link
    [Fact]
    public void Two_errors_and_two_error_moves_without_a_link_between_them_are_not_taken_as_explained()
    {
        var s = new Script().Double().Walk().Approach();
        AssertRefused(s, Hit("c44m-C", InPlayResult.Single, TwoErrors, M(2, 4, MoveCause.ErrorExtraBase), M(1, 3, MoveCause.ErrorExtraBase)));
        Assert.Equal((0, 0), (s.Stat("a3", StatKey.BatH), s.State.AwayRuns));
    }

    // priority: the confirmed C43 force-out check runs first, then the unclear-cause check
    [Fact]
    public void The_confirmed_c43_force_out_error_wins_over_the_multi_error_refusal()
    {
        var s = new Script().Walk().Approach();
        var e = Hit("c44m-P", InPlayResult.Single, TwoErrors, M(1, 2, MoveCause.ErrorSavedOut));
        Assert.Equal("Game.hit_with_force_out_saved_by_error", s.Try(e).Error!.Value.Code);
    }

    // D: one error + a valid ErrorExtraBase keeps its hit (also in the C43 test file)
    [Fact]
    public void One_error_with_a_matching_error_move_and_a_runner_on_first_keeps_its_hit()
    {
        var s = new Script().Double().Walk().Approach();
        s.Ok(Hit("c44m-D", InPlayResult.Single, [FieldPosition.RightField], M(2, 4, MoveCause.ErrorExtraBase), M(1, 2)));
        Assert.Equal((1, 1, 1, 1), (s.Stat("a3", StatKey.BatH), s.Stat("a3", StatKey.BatAB), s.State.AwayRuns, s.Stat("h3", StatKey.FldE)));
    }

    // E: nothing doubted without a runner on first, however many errors are recorded; a hit with no error is untouched
    [Fact]
    public void Without_a_runner_on_first_a_hit_with_several_errors_keeps_its_hit()
    {
        var empty = new Script().Approach();
        empty.Ok(Hit("c44m-E1", InPlayResult.Single, TwoErrors));

        var second = new Script().Double().Approach();               // runner on second only: nobody is forced
        second.Ok(Hit("c44m-E2", InPlayResult.Single, TwoErrors, M(2, 4, MoveCause.ErrorExtraBase)));

        var none = new Script().Walk().Approach();
        none.Ok(Hit("c44m-E3", InPlayResult.Single, null, M(1, 2)));

        Assert.Equal((1, 1, 1), (empty.Stat("a1", StatKey.BatH), second.Stat("a2", StatKey.BatH), none.Stat("a2", StatKey.BatH)));
        Assert.Equal((2, 2), (Errors(empty), Errors(second)));
    }

    // G / replay: a legal single-error play is a harmless duplicate, the refused one is never recorded
    [Fact]
    public void A_re_delivered_accepted_event_is_a_duplicate_and_the_refused_one_stays_refused()
    {
        var ok = new Script().Double().Walk().Approach();
        var good = Hit("c44m-G1", InPlayResult.Single, [FieldPosition.RightField], M(2, 4, MoveCause.ErrorExtraBase), M(1, 2));
        ok.Ok(good);
        var again = GameReducer.Apply(ok.State, good);
        Assert.True(again.IsSuccess && again.Value.WasDuplicate);
        Assert.Same(ok.State, again.Value.State);

        var bad = new Script().Double().Walk().Approach();
        var multi = Hit("c44m-G2", InPlayResult.Single, TwoErrors, M(2, 4, MoveCause.ErrorExtraBase), M(1, 2));
        var r1 = GameReducer.Apply(bad.State, multi);
        Assert.False(r1.IsSuccess, "the multi-error hit was accepted");
        Assert.Equal(r1.Error!.Value, GameReducer.Apply(bad.State, multi).Error!.Value);
    }
}
