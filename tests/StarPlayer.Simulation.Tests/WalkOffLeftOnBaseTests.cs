using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// Left on base after a walk-off. Definition used (MLB glossary, general baseball: team LOB = runners still on base when an inning ends; only
/// the team figure is kept, an individual batter LOB is not recorded). A walk-off ends the inning unfinished, so what counts is a profile rule
/// (CountWalkOffRunnersAsLeftOnBase, true in every shipped profile): the runners standing on base when the game ends. KBO's own
/// record rule text could not be read (docs/game-engine.md section 5), so this is an explicit simplification, not a certified KBO rule.
/// Expected values are counted by hand from the final base picture.
/// </summary>
public class WalkOffLeftOnBaseTests
{
    private static Script BottomOfNinthTied(GameRuleSnapshot? rules = null) => new Script(rules).ThreeUp(17);

    [Fact]
    public void A_walk_off_single_counts_the_runners_still_on_base_when_the_game_ends()
    {
        var s = new Script();
        s.ThreeUp(16);                                          // eight innings, 0-0; the home order is now at h7
        s.Single().FlyOut(M(1, 1)).FlyOut(M(1, 1)).FlyOut(M(1, 1)); // top 9: a1 stranded on first, three outs
        s.Triple().Walk();                                      // bottom 9: h7 on third, h8 on first
        s.Single(M(3, 4), M(1, 2));                             // h9 singles: h7 scores the winning run, h8 to second

        Assert.Equal((GameStatus.Final, GameOutcome.HomeWin, 1), (s.State.Status, s.State.Outcome!.Value, s.State.HomeRuns));
        Assert.Equal(("h9", "h8", null), (s.State.Bases.First!.Player.Value, s.State.Bases.Second!.Player.Value, s.State.Bases.Third?.Player.Value));
        s.Finish();
        Assert.Equal((1, 2), (s.Box.Away.LeftOnBase, s.Box.Home.LeftOnBase));              // completed top half: 1; unfinished bottom half: h9 + h8
    }

    [Fact]
    public void A_walk_off_home_run_leaves_nobody_and_scores_every_runner()
    {
        var s = BottomOfNinthTied();
        s.Single();                                             // h7 on first
        s.HomeRun(M(1, 4));                                     // h8 homers: two runs

        Assert.Equal((GameStatus.Final, 2), (s.State.Status, s.State.HomeRuns));
        Assert.Equal(0, s.Team(TeamSide.Home, StatKey.TeamLOB));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_bases_loaded_walk_or_hit_by_pitch_wins_the_game_and_the_three_runners_on_base_are_left(bool hitByPitch)
    {
        var s = BottomOfNinthTied();
        s.Single().Single(M(1, 2)).Single(M(2, 3), M(1, 2));    // h7 third, h8 second, h9 first, no outs, still tied
        if (hitByPitch) s.HitByPitch(); else s.Walk();          // h1: the runner from third is forced home

        Assert.Equal((GameStatus.Final, 1, 0), (s.State.Status, s.State.HomeRuns, s.State.Outs));
        Assert.Equal(("h1", "h9", "h8"), (s.State.Bases.First!.Player.Value, s.State.Bases.Second!.Player.Value, s.State.Bases.Third!.Player.Value));
        Assert.Equal((1, 1), (s.Stat("h1", StatKey.BatRBI), s.Stat("h7", StatKey.RunR)));
        Assert.Equal(3, s.Team(TeamSide.Home, StatKey.TeamLOB));
    }

    [Fact]
    public void An_unfinished_inning_with_outs_counts_only_the_runners_on_base_at_the_end()
    {
        var s = BottomOfNinthTied();
        s.Out();                                                // h7 out: one out
        s.Triple().Walk();                                      // h8 on third, h9 on first
        s.Single(M(3, 4), M(1, 2));                             // h1 singles: winning run; h9 to second, h1 on first

        Assert.Equal((GameStatus.Final, 1, 1), (s.State.Status, s.State.Outs, s.State.HomeRuns));
        Assert.Equal(2, s.Team(TeamSide.Home, StatKey.TeamLOB));
    }

    [Fact]
    public void Only_a_team_total_exists_and_no_individual_left_on_base_is_recorded()
    {
        Assert.Equal(["TeamLOB"], Enum.GetNames<StatKey>().Where(n => n.Contains("LOB", StringComparison.OrdinalIgnoreCase)).ToArray());

        var s = BottomOfNinthTied();
        s.Single().Single(M(1, 2)).Single(M(2, 3), M(1, 2));
        s.Walk();
        Assert.All(s.State.Stats.Where(e => e.Key == StatKey.TeamLOB), e => Assert.Equal(default, e.Player)); // never charged to a batter
    }

    [Fact]
    public void A_profile_that_does_not_count_walk_off_runners_records_none_for_the_unfinished_inning_but_still_counts_completed_halves()
    {
        var s = new Script(TestRules.WalkOffNoLob);
        s.ThreeUp(16);
        s.Single().FlyOut(M(1, 1)).FlyOut(M(1, 1)).FlyOut(M(1, 1)); // away strands one in the ninth
        s.Triple().Walk();
        s.Single(M(3, 4), M(1, 2));
        s.Finish();

        Assert.Equal((1, 0), (s.Box.Away.LeftOnBase, s.Box.Home.LeftOnBase));
        Assert.False(TestRules.WalkOffNoLob.CountWalkOffRunnersAsLeftOnBase);
    }

    [Fact]
    public void When_the_home_team_leads_after_the_top_of_the_last_inning_the_bottom_is_not_played_and_the_completed_top_counts_its_left_on_base()
    {
        var t = new Script();
        t.ThreeUp(1);                                           // top 1
        t.HomeRun().Outs(3);                                    // bottom 1: h1 homers, 1-0 home, then three outs
        t.ThreeUp(14);                                          // innings 2 to 8, both halves
        t.Single().FlyOut(M(1, 1)).FlyOut(M(1, 1)).FlyOut(M(1, 1)); // top 9: a1 on first, three outs; home leads 1-0

        Assert.Equal((GameStatus.Final, GameOutcome.HomeWin, 9), (t.State.Status, t.State.Outcome!.Value, t.State.Inning));
        t.Finish();
        Assert.Equal((1, 0), (t.Box.Away.LeftOnBase, t.Box.Home.LeftOnBase));
        Assert.Equal(8, t.Box.HomeLine.Count);                   // the home team never batted in the ninth
    }
}
