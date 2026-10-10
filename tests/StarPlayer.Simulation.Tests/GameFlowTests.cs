using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>Innings, game end, walk-offs, extra innings per rule profile, finalization once, legal action sets and idempotence.</summary>
public class GameFlowTests
{
    private static Script ToBottomOfNinthTied(GameRuleSnapshot? rules = null) => new Script(rules).ThreeUp(17); // 0-0 after eight and a half innings

    // ---- innings

    [Fact]
    public void Three_outs_end_the_half_and_the_line_score_adds_a_new_inning_only_when_it_starts()
    {
        var s = new Script().ThreeUp(2);

        Assert.Equal((2, InningHalf.Top, 0, PaStage.Start), (s.State.Inning, s.State.Half, s.State.Outs, s.State.Stage));
        Assert.Equal(new[] { 0, 0 }, s.State.AwayLine);
        Assert.Equal(new[] { 0 }, s.State.HomeLine);
        Assert.Equal(0, s.State.Bases.Count);
        Assert.Equal(GameStatus.InProgress, s.State.Status);
    }

    [Fact]
    public void Batting_order_continues_across_innings_where_the_previous_half_left_off()
    {
        var s = new Script().ThreeUp(2); // away batted a1, a2, a3 in the first

        Assert.Equal("a4", s.State.Batting.CurrentBatterSlot.Player.Value);
        s.Outs(3).ThreeUp(1);
        Assert.Equal("a7", s.State.Batting.CurrentBatterSlot.Player.Value);
        Assert.Equal(1, s.Stat("a1", StatKey.BatPA));
    }

    // ---- regulation endings

    [Fact]
    public void A_nine_inning_game_with_the_away_team_ahead_ends_after_the_home_half_of_the_ninth()
    {
        var s = new Script().HomeRun().Outs(3).ThreeUp(17);

        Assert.Equal((GameStatus.Final, GameOutcome.AwayWin), (s.State.Status, s.State.Outcome!.Value));
        Assert.Equal(new[] { 1, 0, 0, 0, 0, 0, 0, 0, 0 }, s.State.AwayLine);
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 }, s.State.HomeLine);
        Assert.Equal((1, 0), (s.State.AwayRuns, s.State.HomeRuns));
        s.Finish();
        Assert.Equal((new TeamTotals(1, 1, 0, 0), new TeamTotals(0, 0, 0, 0)), (s.Box.Away, s.Box.Home));
    }

    [Fact]
    public void The_home_team_does_not_bat_in_the_ninth_when_it_already_leads()
    {
        var s = new Script().ThreeUp(1).HomeRun().Outs(3).ThreeUp(15); // home leads 1-0 from the first; fifteen more halves reach the end of the top of the ninth

        Assert.Equal((GameStatus.Final, GameOutcome.HomeWin), (s.State.Status, s.State.Outcome!.Value));
        Assert.Equal((9, InningHalf.Top), (s.State.Inning, s.State.Half));
        Assert.Equal(9, s.State.AwayLine.Count);
        Assert.Equal(8, s.State.HomeLine.Count); // no bottom of the ninth was played
        Assert.True(GameReducer.LegalActions(s.State).IsOver);
    }

    [Fact]
    public void Events_after_the_game_is_over_are_rejected_and_the_state_is_untouched()
    {
        var s = new Script().HomeRun().Outs(3).ThreeUp(17);
        var before = s.State;

        Assert.Equal("Game.already_over", s.Try(new ApproachEvent("late", ApproachKind.Normal)).Error!.Value.Code);
        Assert.Equal("Game.already_over", s.Try(new SubstitutionEvent("late2", SubstitutionKind.PinchHit, TeamSide.Away, P("ab1"))).Error!.Value.Code);
        Assert.Same(before, s.State);
    }

    // ---- walk-offs

    [Fact]
    public void A_walk_off_single_ends_the_game_at_once_credits_the_RBI_and_leaves_the_batter_on_first()
    {
        var s = ToBottomOfNinthTied();
        s.Triple();                          // home batter reaches third
        var runner = s.State.Bases.Third!.Player.Value;

        s.Single(M(3, 4));

        Assert.Equal((GameStatus.Final, GameOutcome.HomeWin), (s.State.Status, s.State.Outcome!.Value));
        Assert.Equal((0, 1), (s.State.AwayRuns, s.State.HomeRuns));
        Assert.Equal(1, s.State.HomeLine[8]);
        Assert.Equal(1, s.Stat(runner, StatKey.RunR));
        Assert.Equal(1, s.Stat("ap", StatKey.PitchR));
        Assert.Equal(1, s.Stat("ap", StatKey.PitchER));
        Assert.Equal(1, s.Team(TeamSide.Home, StatKey.TeamLOB)); // the batter stands on first when the game ends (profile: walk-off runners count)
        Assert.Equal(1, s.State.Plays.Last().Runs.Count(r => r.RbiBatter is not null));
        Assert.Equal("Game.already_over", s.Try(new ApproachEvent("late", ApproachKind.Normal)).Error!.Value.Code);
    }

    [Fact]
    public void A_walk_off_that_is_not_a_home_run_cannot_count_runs_after_the_winning_run()
    {
        var s = ToBottomOfNinthTied().Walk().Walk(); // second and first occupied... first and second
        s.Approach();

        var tooMany = s.Try(new InPlayEvent("x1", BattedBallType.Line, InPlayResult.Double, Moves: [M(2, 4), M(1, 4)]));

        Assert.Equal("Game.run_after_winning_run", tooMany.Error!.Value.Code);
        Assert.Equal(GameStatus.InProgress, s.State.Status);
        s.InPlay(BattedBallType.Line, InPlayResult.Double, [M(2, 4), M(1, 3)]);
        Assert.Equal((GameStatus.Final, 1), (s.State.Status, s.State.HomeRuns));
    }

    [Fact]
    public void A_walk_off_home_run_counts_every_run()
    {
        var s = ToBottomOfNinthTied().Walk().Walk().HomeRun();

        Assert.Equal((GameStatus.Final, 3, 3), (s.State.Status, s.State.HomeRuns, s.Stat(s.State.Plays.Last().Batter!.Value.Value, StatKey.BatRBI)));
        Assert.Equal(3, s.Stat("ap", StatKey.PitchR));
        Assert.Equal(1, s.Stat("ap", StatKey.PitchHR));
    }

    [Fact]
    public void A_bases_loaded_walk_can_end_the_game_with_a_forced_run()
    {
        var s = ToBottomOfNinthTied().Walk().Walk().Walk().Walk();

        Assert.Equal((GameStatus.Final, GameOutcome.HomeWin, 1), (s.State.Status, s.State.Outcome!.Value, s.State.HomeRuns));
        Assert.Equal(4, s.Stat("ap", StatKey.PitchBB)); // three walks load the bases and the fourth forces the winning run
    }

    [Fact]
    public void A_walk_off_in_an_extra_inning_ends_the_game_too()
    {
        var s = new Script().ThreeUp(19).HomeRun(); // bottom of the tenth

        Assert.Equal((GameStatus.Final, GameOutcome.HomeWin, 10), (s.State.Status, s.State.Outcome!.Value, s.State.Inning));
        Assert.Equal(10, s.State.HomeLine.Count);
    }

    // ---- extra innings by rule profile

    [Fact]
    public void Profile_with_an_eleventh_inning_limit_ends_a_scoreless_game_as_a_tie_after_eleven()
    {
        var s = new Script(TestRules.Dh).ThreeUp(21);
        Assert.Equal((11, InningHalf.Bottom, GameStatus.InProgress), (s.State.Inning, s.State.Half, s.State.Status));

        s.ThreeUp(1);

        Assert.Equal((GameStatus.Final, GameOutcome.Tie, 11), (s.State.Status, s.State.Outcome!.Value, s.State.Inning));
        s.Finish();
        Assert.Equal(GameOutcome.Tie, s.State.Final!.Outcome);
        Assert.Empty(s.Box.Decisions);
    }

    [Fact]
    public void Profile_with_a_tenth_inning_limit_ends_a_tie_after_ten()
    {
        var s = new Script(TestRules.Cap10).ThreeUp(20);

        Assert.Equal((GameStatus.Final, GameOutcome.Tie, 10), (s.State.Status, s.State.Outcome!.Value, s.State.Inning));
    }

    [Fact]
    public void Extra_innings_start_with_empty_bases_and_no_automatic_runner()
    {
        var s = new Script().ThreeUp(18);                        // nine scoreless innings
        Assert.Equal((10, InningHalf.Top, GameStatus.InProgress), (s.State.Inning, s.State.Half, s.State.Status));
        Assert.Equal((0, 0), (s.State.Bases.Count, s.State.Outs));          // no MLB-style runner placed on second base
        s.ThreeUp(1);
        Assert.Equal((10, InningHalf.Bottom, 0), (s.State.Inning, s.State.Half, s.State.Bases.Count));
        Assert.Equal((0, 0), (s.State.AwayRuns, s.State.HomeRuns));
    }

    [Fact]
    public void Profile_without_a_limit_keeps_playing_until_somebody_leads_at_the_end_of_an_inning()
    {
        var s = new Script(TestRules.Unlimited).ThreeUp(22); // eleven full innings, still tied
        Assert.Equal((12, InningHalf.Top, GameStatus.InProgress), (s.State.Inning, s.State.Half, s.State.Status));

        s.HomeRun().Outs(3).ThreeUp(1);

        Assert.Equal((GameStatus.Final, GameOutcome.AwayWin, 12), (s.State.Status, s.State.Outcome!.Value, s.State.Inning));
    }

    [Fact]
    public void Away_scoring_in_an_extra_inning_does_not_end_the_game_until_the_home_half_is_over()
    {
        var s = new Script().ThreeUp(18).HomeRun().Outs(3);

        Assert.Equal((GameStatus.InProgress, 10, InningHalf.Bottom), (s.State.Status, s.State.Inning, s.State.Half));
        s.ThreeUp(1);
        Assert.Equal((GameStatus.Final, GameOutcome.AwayWin), (s.State.Status, s.State.Outcome!.Value));
    }

    // ---- rule snapshots

    [Fact]
    public void Rule_snapshots_validate_their_values_and_expose_read_only_content_with_a_stable_hash()
    {
        static Domain.Common.Result<GameRuleSnapshot> Make(int regulation = 9, int? limit = 11, bool tie = true, bool dh = true, bool twoWay = false, IEnumerable<string>? open = null, string? provenance = "근거") =>
            GameRuleSnapshot.Create("p-1", "v1", GameRulesStatus.FictionalTest, provenance, open, 3, 4, 3, regulation, limit, tie, dh, twoWay, 15, 3, 3, 9);

        Assert.True(Make().IsSuccess);
        Assert.Equal("GameRules.extra_limit_invalid", Make(limit: 8).Error!.Value.Code);
        Assert.Equal("GameRules.tie_inconsistent", Make(limit: 11, tie: false).Error!.Value.Code);
        Assert.Equal("GameRules.tie_inconsistent", Make(limit: null, tie: true).Error!.Value.Code);
        Assert.Equal("GameRules.two_way_requires_dh", Make(dh: false, twoWay: true, limit: 11).Error!.Value.Code);
        Assert.Equal("GameRules.provenance_missing", Make(provenance: " ").Error!.Value.Code);
        Assert.Equal("GameRules.open_item_blank", Make(open: [" "]).Error!.Value.Code);

        var items = new List<string> { "A" };
        var snapshot = Make(open: items).Value;
        items.Add("B");
        Assert.Single(snapshot.OpenItems);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)snapshot.OpenItems).Add("C"));
        Assert.Equal(Make().Value.ContentHash, Make().Value.ContentHash);
        Assert.NotEqual(Make().Value.ContentHash, Make(limit: 12).Value.ContentHash);
        Assert.Equal(64, snapshot.ContentHash.Length);
    }

    [Fact]
    public void The_rules_a_game_started_with_are_the_rules_it_finishes_with_and_are_recorded_on_the_final_game()
    {
        var s = new Script(TestRules.Cap10).ThreeUp(20).Finish();

        Assert.Same(TestRules.Cap10, s.State.Rules);
        Assert.Equal(("test-dh-cap10", TestRules.Cap10.ContentHash), (s.State.Final!.RulesProfileId, s.State.Final.RulesContentHash));
        Assert.Equal(10, TestRules.Cap10.ExtraInningsLimit);
    }

    // ---- start validation

    [Fact]
    public void Start_rejects_bad_lineups_positions_duplicates_and_shared_players()
    {
        var rules = TestRules.Dh;
        var good = Script.Team("a", true);
        var home = Script.Team("h", true);
        GameStart With(TeamSetup away) => new("g", rules, away, home);

        Assert.True(GameReducer.Start(With(good)).IsSuccess);
        Assert.Equal("Game.lineup_invalid", GameReducer.Start(With(good with { Lineup = good.Lineup.Take(8).ToList() })).Error!.Value.Code);
        Assert.Equal("Game.lineup_positions_invalid", GameReducer.Start(With(good with { Lineup = good.Lineup.Select(s => s.Position == FieldPosition.Catcher ? s with { Position = FieldPosition.FirstBase } : s).ToList() })).Error!.Value.Code);
        Assert.Equal("Game.lineup_duplicate_player", GameReducer.Start(With(good with { Lineup = good.Lineup.Select(s => s.Order == 2 ? s with { Player = good.Lineup[0].Player } : s).ToList() })).Error!.Value.Code);
        Assert.Equal("Game.bench_invalid", GameReducer.Start(With(good with { Bench = [good.Lineup[0].Player] })).Error!.Value.Code);
        Assert.Equal("Game.pitcher_missing", GameReducer.Start(With(good with { StartingPitcher = default })).Error!.Value.Code);
        Assert.Equal("Game.pitcher_in_lineup", GameReducer.Start(With(good with { StartingPitcher = good.Lineup[0].Player })).Error!.Value.Code);
        Assert.Equal("Game.player_on_both_teams", GameReducer.Start(With(good with { Bench = [home.Lineup[0].Player] })).Error!.Value.Code);
        Assert.Equal("Game.start_invalid", GameReducer.Start(new GameStart(" ", rules, good, home)).Error!.Value.Code);
        Assert.Equal("Game.rules_missing", GameReducer.Start(new GameStart("g", null!, good, home)).Error!.Value.Code);
    }

    [Fact]
    public void Without_a_dh_the_pitcher_must_bat_in_the_pitchers_slot_and_the_positions_must_include_pitcher()
    {
        var rules = TestRules.NoDh;
        var noDhTeam = Script.Team("a", false);
        var home = Script.Team("h", false);

        Assert.True(GameReducer.Start(new GameStart("g", rules, noDhTeam, home)).IsSuccess);
        Assert.Equal("Game.lineup_positions_invalid", GameReducer.Start(new GameStart("g", rules, Script.Team("a", true), home)).Error!.Value.Code);
        Assert.Equal("Game.pitcher_slot_mismatch", GameReducer.Start(new GameStart("g", rules, noDhTeam with { StartingPitcher = Script.P("ab1") }, home)).Error!.Value.Code);
    }

    // ---- legal action sets

    [Fact]
    public void Legal_actions_follow_the_plate_appearance_stage()
    {
        var s = new Script();
        var start = GameReducer.LegalActions(s.State);
        Assert.Equal([GameActionKind.Approach, GameActionKind.Take, GameActionKind.Substitution], start.Actions);

        s.Approach();
        Assert.Equal([GameActionKind.Pitch, GameActionKind.InPlay], GameReducer.LegalActions(s.State).Actions);

        var onBase = new Script().Single();
        Assert.Contains(GameActionKind.RunnerPlay, GameReducer.LegalActions(onBase.State).Actions);
        onBase.Take(PitchOutcome.Ball);
        Assert.Equal([GameActionKind.Approach, GameActionKind.Take, GameActionKind.RunnerPlay, GameActionKind.Substitution], GameReducer.LegalActions(onBase.State).Actions);
    }

    [Fact]
    public void Legal_actions_offer_only_finalize_once_over_and_nothing_after_finalizing()
    {
        var s = new Script().HomeRun().Outs(3).ThreeUp(17);
        Assert.Equal([GameActionKind.Finalize], GameReducer.LegalActions(s.State).Actions);

        s.Finish();

        Assert.Empty(GameReducer.LegalActions(s.State).Actions);
        Assert.True(GameReducer.LegalActions(s.State).IsOver);
    }

    // ---- finalize once, duplicates, determinism

    [Fact]
    public void Finalizing_twice_returns_the_same_result_once_and_does_not_recount()
    {
        var s = new Script().HomeRun().Outs(3).ThreeUp(17);
        Assert.Equal("Game.not_over", GameReducer.Finalize(new Script().State).Error!.Value.Code);

        var first = GameReducer.Finalize(s.State).Value;
        var second = GameReducer.Finalize(first.State).Value;

        Assert.True(first.WasNew);
        Assert.False(second.WasNew);
        Assert.Same(first.Game, second.Game);
        Assert.All(first.Game.BoxScore.BatteryExposures, b => Assert.Equal(1, b.SharedAppearances)); // one shared appearance per pair, however many events
        Assert.Equal(first.Game.BoxScore.BatteryExposures, second.Game.BoxScore.BatteryExposures);
        Assert.Equal(new GameCompleted("g1", GameOutcome.AwayWin, 1, 0), GameReducer.CompletedFact(first.Game));
    }

    [Fact]
    public void Replaying_every_event_a_second_time_changes_nothing_and_is_flagged_as_a_duplicate()
    {
        var s = new Script().Walk().Single(M(1, 3)).FlyOut(M(3, 4), M(1, 1)).Strikeout().Strikeout();
        var finalState = s.State;
        var statsBefore = finalState.Stats.Count;

        foreach (var e in s.Log)
        {
            var again = GameReducer.Apply(finalState, e);
            Assert.True(again.IsSuccess);
            Assert.True(again.Value.WasDuplicate);
            Assert.Same(finalState, again.Value.State);
            Assert.Empty(again.Value.Delta.Entries);
            Assert.Empty(again.Value.NewPlays);
        }

        Assert.Equal(statsBefore, finalState.Stats.Count);
    }

    [Fact]
    public void Rejected_events_never_change_the_state_object()
    {
        var s = new Script().Single();
        var before = s.State;
        var attempts = new GameEvent[]
        {
            new PitchEvent("r1", new PitchSpec(PitchOutcome.Ball)),                          // no approach yet
            new InPlayEvent("r2", BattedBallType.Line, InPlayResult.Single),                  // no approach yet
            new RunnerPlayEvent("r3", RunnerPlayKind.Steal, [M(2, 3, MoveCause.Steal)]),      // no runner on second
            new SubstitutionEvent("r4", SubstitutionKind.PinchHit, TeamSide.Home, P("hb1")), // wrong team
            new SubstitutionEvent("r5", SubstitutionKind.PitchingChange, TeamSide.Away, P("ab1")),
            new ApproachEvent("", ApproachKind.Normal),
        };

        foreach (var e in attempts)
            Assert.False(s.Try(e).IsSuccess, e.Id);

        Assert.Same(before, s.State);
        Assert.DoesNotContain(s.State.AppliedEvents.Select(r => r.EventId), id => id.StartsWith('r'));
    }

    [Fact]
    public void The_same_events_from_the_same_start_always_give_the_same_state_and_box_score()
    {
        static Script Play() => new Script().Walk().Single(M(1, 3)).HomeRun(M(1, 4), M(3, 4)).ThreeUp(1);
        var a = Play();
        var b = Play();

        Assert.Equal(a.State.Stats, b.State.Stats);
        Assert.Equal(a.State.Plays.Select(p => p.Id), b.State.Plays.Select(p => p.Id));
        Assert.Equal(a.State.AwayLine, b.State.AwayLine);
        Assert.Equal(BoxScoreBuilder.Build(a.State).Players.Select(p => (p.Player, p.Stats.ToList())), BoxScoreBuilder.Build(b.State).Players.Select(p => (p.Player, p.Stats.ToList())));
    }

    [Fact]
    public void Every_run_on_the_scoreboard_has_a_scored_play_behind_it_after_each_event_of_a_long_game()
    {
        var s = new Script().Walk().Single(M(1, 3)).HomeRun(M(1, 4), M(3, 4)).Strikeout().Triple().Single(M(3, 4)).Strikeout().Strikeout().ThreeUp(17);
        var state = GameReducer.Start(new GameStart("g1", TestRules.Dh, Script.Team("a", true), Script.Team("h", true))).Value;

        foreach (var e in s.Log)
        {
            state = GameReducer.Apply(state, e).Value.State;
            var away = state.Plays.Where(p => p.Batting == TeamSide.Away).Sum(p => p.Runs.Count);
            var home = state.Plays.Where(p => p.Batting == TeamSide.Home).Sum(p => p.Runs.Count);
            Assert.Equal((state.AwayRuns, state.HomeRuns), (away, home));
            Assert.Equal(state.AwayRuns, state.AwayLine.Sum());
            Assert.Equal(state.HomeRuns, state.HomeLine.Sum());
            Assert.InRange(state.Outs, 0, 3);
            var keys = state.Bases.Occupied().Select(o => o.Runner.RunnerKey).ToList();
            Assert.Equal(keys.Count, keys.Distinct().Count());
            Assert.Equal(state.Stats.Where(x => x.Key == StatKey.RunR).Sum(x => x.Amount), state.AwayRuns + state.HomeRuns);
        }
    }
}
