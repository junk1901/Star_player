using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// The six differences between the engine and the original 2026 OBR text that the 2026-10-11 comparison found (evidence rows C12, C14, C19, C24, C32, C34)
/// plus the explicit DH position request (C29). Every expectation is worked out by hand from the quoted clause (docs/game-engine.md section 11), none is read
/// from the engine, and each test was written to FAIL on the engine before the fix. Standard game: away a1..a9 (a5 DH), home h1..h9 (h5 DH), pitchers ap / hp,
/// benches ab1..ab5 / hb1..hb5.
/// </summary>
public class ObrDeviationTests
{
    private static int Sum(BoxScore box, TeamSide team, StatKey key) => box.Players.Where(p => p.Team == team).Sum(p => p.Get(key));

    /// <summary>OBR 9.03(c): at-bats + walks + hit batters + sacrifices must equal runs + left on base + the opponent's putouts.</summary>
    private static (int Batters, int Proof) BoxProof(GameState state, TeamSide batting)
    {
        var box = BoxScoreBuilder.Build(state);
        var fielding = batting == TeamSide.Away ? TeamSide.Home : TeamSide.Away;
        var totals = batting == TeamSide.Away ? box.Away : box.Home;
        var batters = Sum(box, batting, StatKey.BatAB) + Sum(box, batting, StatKey.BatBB) + Sum(box, batting, StatKey.BatHBP) + Sum(box, batting, StatKey.BatSF) + Sum(box, batting, StatKey.BatSH);
        return (batters, totals.Runs + totals.LeftOnBase + Sum(box, fielding, StatKey.FldPO));
    }

    // ================================================================== C12: double play (9.11) is not a grounded-into double play (9.02(a)(17), 9.04(b)(1))

    [Fact]
    public void A_reverse_force_double_play_batter_out_at_first_then_the_runner_tagged_at_second_is_a_double_play_and_a_GIDP()
    {
        // definition of terms: runner on first, one out; the batter grounds to the first baseman who steps on first (force play) and throws to second (tag play on the runner whose force was removed)
        var s = new Script().Single().FlyOut(M(1, 1));                 // a1 on first, one out
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.Out,
            [Seq(M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.FirstBase), Po(FieldPosition.SecondBase)), 2, outAt: 2)],
            [Po(FieldPosition.FirstBase)], batterSequence: 1);

        Assert.Equal((1, InningHalf.Bottom), (s.State.Inning, s.State.Half));   // one out before plus two: the inning ends
        Assert.Equal(3, s.Stat("hp", StatKey.PitchOuts));
        Assert.True(s.State.Plays.Last().IsDoublePlay);
        Assert.Equal((1, 0), (s.Stat("a3", StatKey.BatGIDP), s.Stat("a3", StatKey.BatRBI)));
        Assert.Equal((1, 1), (s.Stat("h4", StatKey.FldDP), s.Stat("h8", StatKey.FldDP)));
    }

    [Fact]
    public void A_ground_ball_double_play_whose_first_out_is_not_a_force_out_is_a_double_play_but_not_a_GIDP()
    {
        // a1 on third (not forced: first and second are empty) is thrown out at the plate first, then the batter is out at first: two outs, the first one a non-force out
        var s = new Script().Triple();
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.Out,
            [Seq(M(3, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.Catcher)), 1, outAt: 4)],
            [A(FieldPosition.SecondBase), Po(FieldPosition.FirstBase)], batterSequence: 2);

        Assert.Equal(2, s.State.Outs);
        Assert.True(s.State.Plays.Last().IsDoublePlay);                                   // defensive double play (9.11): two outs on one play
        Assert.Equal(0, s.Stat("a2", StatKey.BatGIDP));                                   // neither a force nor a reverse-force double play (9.02(a)(17))
        Assert.Equal((1, 1, 1, 1), (s.Stat("h2", StatKey.FldDP), s.Stat("h9", StatKey.FldDP), s.Stat("h8", StatKey.FldDP), s.Stat("h4", StatKey.FldDP)));
    }

    [Fact]
    public void A_run_that_scores_on_a_ground_ball_double_play_that_is_not_a_force_or_reverse_force_double_play_keeps_its_RBI()
    {
        // a1 on third, a2 on second (first empty: nobody is forced). a3 grounds out, a1 scores, a2 is thrown out at third: 9.04(b)(1) withholds the RBI only for a force / reverse-force double play
        var s = new Script().Triple().Double(M(3, 3));
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.Out,
            [M(3, 4), M(2, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.ThirdBase))],
            [A(FieldPosition.SecondBase), Po(FieldPosition.FirstBase)]);

        Assert.Equal((1, 2), (s.State.AwayRuns, s.State.Outs));
        Assert.Equal((1, 0), (s.Stat("a3", StatKey.BatRBI), s.Stat("a3", StatKey.BatGIDP)));
        Assert.Equal(1, s.Stat("a1", StatKey.RunR));
    }

    [Fact]
    public void A_line_drive_caught_and_a_runner_thrown_out_is_a_defensive_double_play_never_a_GIDP()
    {
        var s = new Script().Single();                               // a1 on first
        s.Approach().InPlay(BattedBallType.Line, InPlayResult.Out,
            [M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase))],
            [Po(FieldPosition.Shortstop)]);

        Assert.Equal(2, s.State.Outs);
        Assert.True(s.State.Plays.Last().IsDoublePlay);
        Assert.Equal(0, s.Stat("a2", StatKey.BatGIDP));
    }

    [Fact]
    public void A_hit_with_two_outs_and_a_strikeout_followed_by_a_caught_stealing_are_never_a_GIDP()
    {
        var s = new Script().Single().Single(M(1, 2));                // a1 second, a2 first
        s.Single(M(2, 4), M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.RightField), Po(FieldPosition.ThirdBase)) with { OutAt = 3 });  // a3 single: a1 scores, a2 thrown out at third
        Assert.Equal(0, s.Stat("a3", StatKey.BatGIDP));
        Assert.False(s.State.Plays.Last().IsDoublePlay);

        var t = new Script().Single().Strikeout();                    // a2 strikes out, then a1 is caught stealing: two outs, two events
        t.Runner(RunnerPlayKind.Steal, M(1, 0, MoveCause.Steal, OutKind.Tag, A(FieldPosition.Catcher), Po(FieldPosition.SecondBase)));
        Assert.Equal(0, t.Stat("a2", StatKey.BatGIDP));
        Assert.Equal(2, t.State.Outs);
    }

    // ================================================================== C14: 9.16(i) the relief pitcher gets no benefit of the previous pitcher's chances (official examples)

    [Fact]
    public void Obr_9_16_i_example_1_with_two_out_a_walk_and_an_error_then_a_relief_home_run_gives_the_reliever_one_earned_run_and_the_team_none()
    {
        // two out, Peter pitching: Abel walks, Baker reaches on an error; Roger relieves; Charlie homers, three runs. Two unearned to Peter, one earned to Roger, three unearned to the team.
        var s = new Script().Out().Out();
        s.Walk();                                                                                                // Abel = a3
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.ReachedOnError, [M(1, 2)], errors: [FieldPosition.Shortstop]); // Baker = a4
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");                                            // Roger
        s.HomeRun(M(2, 4), M(1, 4));                                                                             // Charlie = a5
        s.Out();                                                                                                 // a6 ends the inning

        Assert.Equal(3, s.State.AwayRuns);
        Assert.Equal((2, 0), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER)));
        Assert.Equal((1, 1), (s.Stat("hb1", StatKey.PitchR), s.Stat("hb1", StatKey.PitchER)));
        Assert.Equal(0, s.Team(TeamSide.Home, StatKey.TeamER));
    }

    [Fact]
    public void Obr_9_16_i_example_2_an_error_by_the_reliever_s_defense_makes_both_pitchers_runs_unearned()
    {
        // two out, Peter: Abel and Baker walk; Roger relieves; Charlie reaches on an error; Daniel homers, four runs. Two unearned to Peter, two unearned to Roger.
        var s = new Script().Out().Out();
        s.Walk().Walk();                                                                                         // Abel = a3, Baker = a4
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.ReachedOnError, [M(2, 3), M(1, 2)], errors: [FieldPosition.Shortstop]); // Charlie = a5
        s.HomeRun(M(3, 4), M(2, 4), M(1, 4));                                                                    // Daniel = a6
        s.Out();

        Assert.Equal(4, s.State.AwayRuns);
        Assert.Equal((2, 0), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER)));
        Assert.Equal((2, 0), (s.Stat("hb1", StatKey.PitchR), s.Stat("hb1", StatKey.PitchER)));
        Assert.Equal(0, s.Team(TeamSide.Home, StatKey.TeamER));
    }

    [Fact]
    public void Obr_9_16_i_example_3_five_runs_two_of_them_earned_for_the_team_one_each_for_the_two_pitchers()
    {
        // none out, Peter: Abel walks, Baker reaches on an error; Roger relieves; Charlie homers (3 runs); Daniel and Edward strike out; Frank reaches on an error; George homers (2 runs).
        // Peter 2 runs / 1 earned (Abel), Roger 3 runs / 1 earned (Charlie), team 5 runs / 2 earned (only Abel and Charlie would have scored without the errors).
        var s = new Script().Walk();                                                                             // Abel = a1
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.ReachedOnError, [M(1, 2)], errors: [FieldPosition.Shortstop]); // Baker = a2
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");
        s.HomeRun(M(2, 4), M(1, 4));                                                                             // Charlie = a3
        s.Strikeout().Strikeout();                                                                               // Daniel = a4, Edward = a5
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.ReachedOnError, errors: [FieldPosition.Shortstop]);  // Frank = a6 (two out)
        s.HomeRun(M(1, 4));                                                                                      // George = a7
        s.Out();                                                                                                 // a8 ends the inning

        Assert.Equal(5, s.State.AwayRuns);
        Assert.Equal((2, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hp", StatKey.PitchER)));
        Assert.Equal((3, 1), (s.Stat("hb1", StatKey.PitchR), s.Stat("hb1", StatKey.PitchER)));
        Assert.Equal(2, s.Team(TeamSide.Home, StatKey.TeamER));
    }

    // ================================================================== C19: box proof (9.03(c))

    [Fact]
    public void A_run_voided_by_a_batter_runner_third_out_is_left_on_base_and_the_box_proof_balances()
    {
        // two out; a3 single, a4 double (a3 to third); a5 grounds out for the third out before reaching first while a3 crosses the plate: the run does not count (5.08(a) exception 1)
        var s = new Script().Out().Out().Single();
        s.Double(M(1, 3));
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.Out, [Seq(M(3, 4), 1), M(2, 2)], [A(FieldPosition.Shortstop), Po(FieldPosition.FirstBase)], batterSequence: 2);

        var team = BoxScoreBuilder.Build(s.State).Away;
        Assert.Equal((0, 2), (team.Runs, team.LeftOnBase));            // a4 stands on second, a3 crossed the plate without a counting run: both are left on base
        var (batters, proof) = BoxProof(s.State, TeamSide.Away);
        Assert.Equal((5, 5), (batters, proof));                        // 5 at-bats = 0 runs + 2 left + 3 putouts
    }

    [Fact]
    public void A_force_out_third_out_with_a_voided_run_counts_the_voided_runner_once_beside_the_runners_still_on_base()
    {
        // two out, a1 on third and a2 on first; a5 grounds into a fielder's choice: a2 is forced out at second (third out) while a1 crosses the plate: no run
        var s = new Script().Triple().Walk();
        s.FlyOut(M(3, 3), M(1, 1)).FlyOut(M(3, 3), M(1, 1));
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice,
            [Seq(M(3, 4), 1), Seq(M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase)), 2)]);

        var team = BoxScoreBuilder.Build(s.State).Away;
        Assert.Equal((0, 2), (team.Runs, team.LeftOnBase));            // a5 on first plus the voided a1; a2 was put out and is no one's left runner
        var (batters, proof) = BoxProof(s.State, TeamSide.Away);
        Assert.Equal((5, 5), (batters, proof));                        // a1 triple, a3, a4, a5 = 4 at-bats + a2's walk = 5; 0 runs + 2 left + 3 putouts
    }

    [Fact]
    public void A_walk_off_inning_ends_the_game_with_the_runners_still_on_base_counted_once_and_the_proof_balances()
    {
        var s = new Script().ThreeUp(17);                              // nine innings tied 0-0: the top of the ninth is done, the bottom starts
        Assert.Equal((9, InningHalf.Bottom), (s.State.Inning, s.State.Half));
        s.Triple().Walk();                                             // h1 on third, h2 on first
        s.FlyOut(M(3, 3), M(1, 1)).FlyOut(M(3, 3), M(1, 1));           // two outs
        s.Single(M(3, 4), M(1, 2));                                    // h5 singles: h1 scores the winning run, h2 to second

        Assert.Equal(GameStatus.Final, s.State.Status);
        var team = BoxScoreBuilder.Build(s.State).Home;
        Assert.Equal((1, 2), (team.Runs, team.LeftOnBase));            // h2 on second and h5 on first when the game ends
        var (batters, proof) = BoxProof(s.State, TeamSide.Home);
        Assert.Equal((29, 29), (batters, proof));                      // 24 earlier at-bats (eight innings) + 4 at-bats + 1 walk = 24 + 1 run + 2 left + 2 putouts
    }

    // ================================================================== C24: the starting DH must bat once (5.11(a)(2))

    [Fact]
    public void The_starting_dh_cannot_be_pinch_hit_for_before_his_first_plate_appearance_while_the_opponent_keeps_its_pitcher()
    {
        var s = new Script().Walk().Walk().Walk().Walk();             // a1..a4 walk, the DH a5 is due
        var before = s.State;

        var pinch = s.Try(new SubstitutionEvent("x1", SubstitutionKind.PinchHit, TeamSide.Away, P("ab1")));
        Assert.Equal("Game.dh_must_bat", pinch.Error!.Value.Code);
        Assert.Same(before, s.State);                                  // nothing changed: lineup, bench and both teams' DH state
        Assert.True(s.State.Away.DhActive && s.State.Home.DhActive);

        // a defensive substitution for the DH slot is the same removal and is refused the same way (home DH h5 has not batted either)
        var defensive = s.Try(new SubstitutionEvent("x2", SubstitutionKind.DefensiveSub, TeamSide.Home, P("hb1"), Slot: 5));
        Assert.Equal("Game.dh_must_bat", defensive.Error!.Value.Code);
        Assert.Same(before, s.State);
    }

    [Fact]
    public void The_restriction_ends_when_the_opposing_club_changes_pitchers()
    {
        var s = new Script().Walk().Walk().Walk().Walk();
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");  // the opposing club changes pitchers

        s.Sub(SubstitutionKind.PinchHit, TeamSide.Away, "ab1");

        Assert.Equal(("ab1", FieldPosition.DesignatedHitter), (s.State.Away.SlotByOrder(5).Player.Value, s.State.Away.SlotByOrder(5).Position));
        Assert.True(s.State.Away.DhActive && s.State.Home.DhActive);
        Assert.Contains(P("a5"), s.State.Away.Removed);
    }

    [Fact]
    public void A_dh_who_has_batted_once_can_be_replaced_without_the_opponent_changing_pitchers()
    {
        var s = new Script().Walk().Walk().Walk().Walk();
        s.FlyOut(M(3, 3), M(2, 2), M(1, 1));                           // the DH a5 comes to bat and flies out
        s.Sub(SubstitutionKind.DefensiveSub, TeamSide.Away, "ab1", slot: 5);

        Assert.Equal("ab1", s.State.Away.SlotByOrder(5).Player.Value);
        Assert.True(s.State.Away.DhActive);
    }

    // ================================================================== C32: the two-way exception belongs to the starting pitcher listed as DH (5.11(b))

    [Fact]
    public void An_ordinary_dh_who_goes_to_the_mound_ends_the_dh_role_instead_of_keeping_it()
    {
        var s = new Script(TestRules.DhForfeit);                        // a profile where the DH may end
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "h5");   // h5 is the DH, not the starting pitcher

        var home = s.State.Home;
        Assert.False(home.DhActive);                                    // 5.11(a)(12): a DH who takes a defensive position ends the role
        Assert.Equal(("h5", FieldPosition.Pitcher), (home.SlotByOrder(5).Player.Value, home.SlotByOrder(5).Position));
        Assert.Equal("h5", home.Pitcher.Value);
        Assert.Contains(P("hp"), home.Removed);
        Assert.True(s.State.Away.DhActive);                             // the other club is untouched
    }

    [Fact]
    public void Without_a_forfeiture_profile_an_ordinary_dh_cannot_go_to_the_mound_at_all_and_nothing_changes()
    {
        var s = new Script(TestRules.Dh);
        var before = s.State;

        var result = s.Try(new SubstitutionEvent("x1", SubstitutionKind.PitchingChange, TeamSide.Home, P("h5")));

        Assert.Equal("Game.pitcher_not_eligible", result.Error!.Value.Code);
        Assert.Same(before, s.State);
    }

    [Fact]
    public void A_replacement_dh_who_then_pitches_ends_the_dh_role_too()
    {
        var s = new Script(TestRules.DhForfeit).ThreeUp();                            // top 1 done; bottom 1, the away club is in the field
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Away, "ab2");                  // the opposing club changes pitchers, so the home DH h5 may be replaced before he bats
        s.Sub(SubstitutionKind.DefensiveSub, TeamSide.Home, "hb2", slot: 5);           // hb2 is now the home DH
        s.Outs(3);                                                                     // top 2: the home club is in the field again

        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb2");                  // the replacement DH goes to the mound: not the starting pitcher, so no 5.11(b) exception

        Assert.False(s.State.Home.DhActive);
        Assert.Equal(("hb2", FieldPosition.Pitcher), (s.State.Home.SlotByOrder(5).Player.Value, s.State.Home.SlotByOrder(5).Position));
        Assert.True(s.State.Away.DhActive);
    }

    [Fact]
    public void The_two_way_starter_keeps_the_dh_role_after_leaving_the_mound_and_cannot_pitch_again()
    {
        var home = Team("h", dh: true, starter: P("h5"));               // the starting pitcher h5 is also listed as the DH
        var s = new Script(TestRules.DhForfeit, Team("a", dh: true), home);
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");   // the starter leaves the mound

        Assert.True(s.State.Home.DhActive);                             // 5.11(b): replaced as the pitcher, he can continue as the DH
        Assert.Equal(("h5", FieldPosition.DesignatedHitter), (s.State.Home.SlotByOrder(5).Player.Value, s.State.Home.SlotByOrder(5).Position));
        var before = s.State;
        Assert.Equal("Game.pitcher_already_used", s.Try(new SubstitutionEvent("x1", SubstitutionKind.PitchingChange, TeamSide.Home, P("h5"))).Error!.Value.Code);
        Assert.Same(before, s.State);
    }

    [Fact]
    public void The_two_way_starter_who_has_left_the_mound_ends_the_dh_role_by_taking_a_fielding_position()
    {
        var home = Team("h", dh: true, starter: P("h5"));
        var s = new Script(TestRules.DhForfeit, Team("a", dh: true), home);
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");
        s.Sub(SubstitutionKind.DhToField, TeamSide.Home, slot: 3);      // 5.11(b) last sentence: switched to a position other than pitcher terminates the DH

        Assert.False(s.State.Home.DhActive);
        Assert.True(s.State.Away.DhActive);
    }

    // ================================================================== C34: 9.16(h) the walk of a relief pitcher's first batter

    private static Script CountThenRelieve(int balls, int strikes)
    {
        var s = new Script();
        for (var i = 0; i < balls; i++) s.Take(PitchOutcome.Ball);
        for (var i = 0; i < strikes; i++) s.Take(PitchOutcome.CalledStrike);
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");
        s.Approach();
        for (var i = 0; i < 4 - balls; i++) s.Ball();                  // the batter walks
        return s;
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    public void A_walk_to_the_first_batter_a_reliever_faces_at_these_counts_is_charged_to_the_previous_pitcher(int balls, int strikes)
    {
        var s = CountThenRelieve(balls, strikes);

        Assert.Equal("a1", s.State.Bases.First!.Player.Value);
        Assert.Equal("hp", s.State.Bases.First.ResponsiblePitcher.Value);
        Assert.Equal((1, 0), (s.Stat("hp", StatKey.PitchBB), s.Stat("hb1", StatKey.PitchBB)));
        Assert.Equal(1, s.Stat("a1", StatKey.BatBB));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    public void At_every_other_count_the_relief_pitcher_is_charged_with_the_walk(int balls, int strikes)
    {
        var s = CountThenRelieve(balls, strikes);

        Assert.Equal(("hb1", 1, 0), (s.State.Bases.First!.ResponsiblePitcher.Value, s.Stat("hb1", StatKey.PitchBB), s.Stat("hp", StatKey.PitchBB)));
    }

    [Fact]
    public void Any_other_result_of_that_first_batter_belongs_to_the_relief_pitcher()
    {
        var s = new Script();
        s.Take(PitchOutcome.Ball).Take(PitchOutcome.Ball).Take(PitchOutcome.Ball).Take(PitchOutcome.CalledStrike).Take(PitchOutcome.CalledStrike); // 3-2
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");
        s.Approach().Swing();                                            // strikeout by the reliever (9.16(h)(2): any other action is charged to him)

        Assert.Equal((1, 0), (s.Stat("hb1", StatKey.PitchK), s.Stat("hp", StatKey.PitchK)));
        Assert.Equal((0, 0), (s.Stat("hp", StatKey.PitchBB), s.Stat("hb1", StatKey.PitchBB)));
    }

    // ================================================================== C29: the DH takes the vacated position only when the event says so (5.11(a)(14) leaves it to the manager)

    [Fact]
    public void A_fielder_going_to_the_mound_with_an_active_dh_needs_the_explicit_request_for_the_dh_to_fill_the_vacated_position()
    {
        var s = new Script(TestRules.DhForfeit);
        var before = s.State;

        var unspecified = s.Try(new SubstitutionEvent("x1", SubstitutionKind.PitchingChange, TeamSide.Home, P("h6")));
        Assert.Equal("Game.dh_vacated_position_unspecified", unspecified.Error!.Value.Code);
        var wrongSlot = s.Try(new SubstitutionEvent("x2", SubstitutionKind.PitchingChange, TeamSide.Home, P("h6"), Slot: 4));
        Assert.Equal("Game.dh_vacated_position_invalid", wrongSlot.Error!.Value.Code);
        Assert.Same(before, s.State);                                   // no automatic correction, no invented position

        s.Ok(new SubstitutionEvent("x3", SubstitutionKind.PitchingChange, TeamSide.Home, P("h6"), Slot: 5));   // slot 5 = the DH takes the left fielder's position
        Assert.False(s.State.Home.DhActive);
        Assert.Equal(("h5", FieldPosition.LeftField), (s.State.Home.SlotByOrder(5).Player.Value, s.State.Home.SlotByOrder(5).Position));
        Assert.Equal(("h6", FieldPosition.Pitcher), (s.State.Home.SlotByOrder(6).Player.Value, s.State.Home.SlotByOrder(6).Position));
    }
}
