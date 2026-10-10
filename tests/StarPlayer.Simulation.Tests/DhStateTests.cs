using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// High 3 of the Codex review: whether a designated hitter exists (GameRules.DesignatedHitter) is separate from whether a team still has one
/// right now (TeamGameState.DhActive). The forfeiture transitions below need DhMayBeForfeited. Both are OBR 5.11(a)(12) (DH takes the field) and
/// 5.11(a)(14) (a fielder goes to the mound), original text read 2026-10-11, and are on in the KBO-oriented profile as approved OBR fallbacks (not a verified KBO
/// rule); the test profiles TestRules.DhForfeit / DhOnDefenseOnly isolate them (docs/game-engine.md sections 4 and 5).
/// Lineup (both teams): 1 CF, 2 SS, 3 RF, 4 1B, 5 DH, 6 LF, 7 3B, 8 2B, 9 C; pitcher hp / ap; bench hb1..hb5 / ab1..ab5.
/// </summary>
public class DhStateTests
{
    private static Script Fresh(GameRuleSnapshot? rules = null) => new(rules ?? TestRules.DhForfeit);

    private static LineupSlot Slot(TeamGameState team, int order) => team.SlotByOrder(order);

    [Fact]
    public void Both_teams_start_with_an_active_dh_exactly_when_the_profile_has_one()
    {
        Assert.True(Fresh().State.Away.DhActive && Fresh().State.Home.DhActive);
        Assert.True(Fresh(TestRules.Dh).State.Away.DhActive);
        var noDh = new Script(TestRules.NoDh);
        Assert.False(noDh.State.Away.DhActive || noDh.State.Home.DhActive);
    }

    [Fact]
    public void An_ordinary_dh_game_keeps_the_dh_through_normal_replacements()
    {
        var s = Fresh();
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");                 // top 1: a bench pitcher relieves, the DH is untouched
        s.Outs(3);
        Assert.True(s.State.Home.DhActive && s.State.Away.DhActive);
        Assert.Equal(FieldPosition.DesignatedHitter, Slot(s.State.Home, 5).Position);

        s.Sub(SubstitutionKind.PinchHit, TeamSide.Home, "hb2");                       // bottom 1: pinch hit for the batter due, h1
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Away, "ab1");                 // the opposing club changes pitchers: the home DH may now be replaced before he bats (OBR 5.11(a)(2))
        s.Sub(SubstitutionKind.DefensiveSub, TeamSide.Home, "hb3", slot: 5);          // the DH slot may get another bench player, still the DH
        Assert.True(s.State.Home.DhActive);
        Assert.Equal(("hb3", FieldPosition.DesignatedHitter), (Slot(s.State.Home, 5).Player.Value, Slot(s.State.Home, 5).Position));
    }

    [Fact]
    public void A_profile_without_forfeiture_never_allows_the_dh_to_end_and_changes_nothing()
    {
        var s = Fresh(TestRules.Dh);
        var before = s.State;

        Assert.Equal("Game.dh_forfeit_not_allowed", s.Try(new SubstitutionEvent("x1", SubstitutionKind.DhToField, TeamSide.Home, Slot: 3)).Error!.Value.Code);
        Assert.Equal("Game.pitcher_not_eligible", s.Try(new SubstitutionEvent("x2", SubstitutionKind.PitchingChange, TeamSide.Home, P("h6"))).Error!.Value.Code);
        Assert.Same(before, s.State);
        Assert.False(TestRules.Dh.DhMayBeForfeited);
    }

    [Fact]
    public void The_dh_takes_the_field_the_fielder_leaves_the_pitcher_bats_and_only_that_team_loses_its_dh()
    {
        var s = Fresh();
        s.Sub(SubstitutionKind.DhToField, TeamSide.Home, slot: 3);                    // h5 (DH) takes right field, h3 leaves, hp bats third

        var home = s.State.Home;
        Assert.False(home.DhActive);
        Assert.True(s.State.Away.DhActive);                                           // the other team is independent
        Assert.Equal(("h5", FieldPosition.RightField), (Slot(home, 5).Player.Value, Slot(home, 5).Position));
        Assert.Equal(("hp", FieldPosition.Pitcher), (Slot(home, 3).Player.Value, Slot(home, 3).Position));
        Assert.Contains(P("h3"), home.Removed);
        Assert.Contains(P("hp"), home.EverInLineup);
        Assert.Equal(9, home.Lineup.Select(l => l.Position).Distinct().Count());
        Assert.DoesNotContain(home.Lineup, l => l.Position == FieldPosition.DesignatedHitter);
        Assert.True(GameReducer.LegalActions(s.State).DefenseComplete);
        var record = s.State.Substitutions.Single();
        Assert.Equal((SubstitutionKind.DhToField, "h3", "hp", 3), (record.Kind, record.Out.Value, record.In.Value, record.Slot));

        s.Outs(3);                                                                    // top 1 ends; bottom 1: h1, h2 out, then the pitcher is due
        s.Out().Out();
        Assert.Equal("hp", s.State.Home.CurrentBatterSlot.Player.Value);
        s.Strikeout();
        Assert.Equal((1, 1), (s.Stat("hp", StatKey.BatPA), s.Stat("hp", StatKey.BatSO)));   // the pitcher really bats
        Assert.Equal(GameStatus.InProgress, s.State.Status);
    }

    [Fact]
    public void After_the_dh_is_gone_it_cannot_be_brought_back_and_a_live_dh_cannot_be_named_either()
    {
        var s = Fresh();
        s.Sub(SubstitutionKind.DhToField, TeamSide.Home, slot: 3);
        var before = s.State;

        Assert.Equal("Game.dh_cannot_be_reactivated", s.Try(new SubstitutionEvent("x1", SubstitutionKind.NameDesignatedHitter, TeamSide.Home)).Error!.Value.Code);
        Assert.Equal("Game.dh_not_active", s.Try(new SubstitutionEvent("x2", SubstitutionKind.DhToField, TeamSide.Home, Slot: 6)).Error!.Value.Code);
        Assert.Equal("Game.position_swap_invalid", s.Try(new SubstitutionEvent("x3", SubstitutionKind.PositionSwap, TeamSide.Home, Slot: 3, Slot2: 6)).Error!.Value.Code);
        Assert.Same(before, s.State);
        Assert.False(s.State.Home.DhActive);

        Assert.Equal("Game.dh_already_active", s.Try(new SubstitutionEvent("x4", SubstitutionKind.NameDesignatedHitter, TeamSide.Away)).Error!.Value.Code);
        Assert.Same(before, s.State);
    }

    [Fact]
    public void A_lineup_fielder_who_goes_to_the_mound_ends_the_dh_who_takes_his_position_and_the_old_pitcher_leaves()
    {
        var s = Fresh();
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "h6", slot: 5);         // left fielder h6 pitches; Slot = 5 asks the DH (slot 5) to take the vacated position

        var home = s.State.Home;
        Assert.False(home.DhActive);
        Assert.Equal(("h6", FieldPosition.Pitcher), (Slot(home, 6).Player.Value, Slot(home, 6).Position));
        Assert.Equal(("h5", FieldPosition.LeftField), (Slot(home, 5).Player.Value, Slot(home, 5).Position)); // the DH takes the vacated position
        Assert.Equal("h6", home.Pitcher.Value);
        Assert.Contains(P("hp"), home.Removed);
        Assert.Equal("h6", home.PitcherUsages[^1].Pitcher.Value);
        Assert.True(GameReducer.LegalActions(s.State).DefenseComplete);
        Assert.True(s.State.Away.DhActive);
    }

    [Fact]
    public void After_the_dh_is_gone_a_pitcher_may_swap_to_a_field_position_through_the_ordinary_no_dh_rules()
    {
        var s = Fresh();
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "h6", slot: 5);         // DH ended (the DH takes the left field), h6 pitches
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "h7");                  // third baseman h7 takes the mound, h6 goes to third

        var home = s.State.Home;
        Assert.Equal(("h7", FieldPosition.Pitcher), (Slot(home, 7).Player.Value, Slot(home, 7).Position));
        Assert.Equal(("h6", FieldPosition.ThirdBase), (Slot(home, 6).Player.Value, Slot(home, 6).Position));
        Assert.DoesNotContain(P("h6"), home.Removed);
        Assert.False(home.DhActive);

        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");                 // a bench pitcher takes over the pitcher slot
        Assert.Equal(("hb1", FieldPosition.Pitcher), (Slot(s.State.Home, 7).Player.Value, Slot(s.State.Home, 7).Position));
        Assert.Contains(P("h7"), s.State.Home.Removed);
        Assert.Equal("Game.pitcher_already_used", s.Try(new SubstitutionEvent("x1", SubstitutionKind.PitchingChange, TeamSide.Home, P("h6"))).Error!.Value.Code);
    }

    [Fact]
    public void A_pinch_hitter_for_the_pitcher_after_forfeiture_leaves_the_defense_incomplete_until_a_pitcher_is_named()
    {
        var s = Fresh();
        s.Sub(SubstitutionKind.DhToField, TeamSide.Home, slot: 3);
        s.Outs(3);
        s.Out().Out();                                                                // the pitcher (slot 3) is due
        s.Sub(SubstitutionKind.PinchHit, TeamSide.Home, "hb1");                       // hb1 hits for him and holds the pitcher slot

        Assert.Equal(("hb1", FieldPosition.Pitcher), (Slot(s.State.Home, 3).Player.Value, Slot(s.State.Home, 3).Position));
        s.Strikeout();                                                                // hb1 strikes out: third out, the home team must now take the field
        Assert.False(GameReducer.LegalActions(s.State).DefenseComplete);              // hp (the pitcher) is no longer the player in the pitcher slot
        Assert.Equal("Game.defense_incomplete", s.Try(new ApproachEvent("x1", ApproachKind.Normal)).Error!.Value.Code);
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb2");
        Assert.True(GameReducer.LegalActions(s.State).DefenseComplete);
        Assert.False(s.State.Home.DhActive);
    }

    [Fact]
    public void The_dh_move_is_validated_target_batter_runner_and_slot_and_a_failure_changes_nothing()
    {
        var s = Fresh();
        var before = s.State;
        SubstitutionEvent Ev(string id, int? slot) => new(id, SubstitutionKind.DhToField, TeamSide.Home, Slot: slot);

        Assert.Equal("Game.substitution_slot_invalid", s.Try(Ev("a", null)).Error!.Value.Code);
        Assert.Equal("Game.substitution_slot_invalid", s.Try(Ev("b", 10)).Error!.Value.Code);
        Assert.Equal("Game.dh_forfeit_target_invalid", s.Try(Ev("c", 5)).Error!.Value.Code);   // the DH cannot replace himself
        Assert.Same(before, s.State);

        // the batting team may not move a fielder who is the batter now due or a runner on base
        var t = Fresh().Outs(3);                                                      // bottom 1: h1 is due
        Assert.Equal("Game.use_pinch_substitution", t.Try(Ev("d", 1)).Error!.Value.Code);
        t.Single();                                                                   // h1 on first, h2 due
        Assert.Equal("Game.use_pinch_substitution", t.Try(Ev("e", 1)).Error!.Value.Code);
        Assert.Equal("Game.use_pinch_substitution", t.Try(Ev("f", 2)).Error!.Value.Code);
        Assert.True(t.State.Home.DhActive);
    }

    [Fact]
    public void A_profile_with_only_the_defense_transition_ends_the_dh_when_he_plays_the_field_but_keeps_the_mound_transition_off()
    {
        // the OBR-style transition (DH used on defense ends the DH) without the OBR 5.11(a)(14) fielder-to-mound one (a profile shape that is no longer the KBO-oriented one since 2026-10-11)
        var s = Fresh(TestRules.DhOnDefenseOnly);
        Assert.True(TestRules.DhOnDefenseOnly.DhMayBeForfeited);
        Assert.False(TestRules.DhOnDefenseOnly.FielderToMoundForfeitsDh);
        var before = s.State;

        Assert.Equal("Game.pitcher_not_eligible", s.Try(new SubstitutionEvent("x1", SubstitutionKind.PitchingChange, TeamSide.Home, P("h6"))).Error!.Value.Code);
        Assert.Same(before, s.State);

        s.Sub(SubstitutionKind.DhToField, TeamSide.Home, slot: 6);
        Assert.False(s.State.Home.DhActive);
        Assert.Equal(("h5", FieldPosition.LeftField), (Slot(s.State.Home, 5).Player.Value, Slot(s.State.Home, 5).Position)); // he keeps batting in his own spot
    }

    [Fact]
    public void The_mound_transition_flag_needs_the_dh_forfeiture_flag_and_every_profile_names_its_rule_policy()
    {
        var invalid = GameRuleSnapshot.Create("x", "v", GameRulesStatus.FictionalTest, "p", [], 3, 4, 3, 9, 11, true, true, true, 15, 3, 3, 9, dhMayBeForfeited: false, fielderToMoundForfeitsDh: true);
        Assert.Equal("GameRules.dh_forfeit_requires_dh", invalid.Error!.Value.Code);
        Assert.Equal("GameRules.rule_policy_invalid", GameRuleSnapshot.Create("x", "v", GameRulesStatus.FictionalTest, "p", [], 3, 4, 3, 9, 11, true, true, true, 15, 3, 3, 9, rulePolicyId: " ").Error!.Value.Code);
        Assert.Equal("fictional-test-v1", TestRules.Dh.RulePolicyId);
    }

    // ---------------------------------------------------------------- two-way players

    private static Script TwoWayAway(GameRuleSnapshot? rules = null) =>
        new(rules ?? TestRules.DhForfeit, Script.Team("a", true, P("a5")), Script.Team("h", true));   // a5 is the DH and the starting pitcher

    [Fact]
    public void A_two_way_pitcher_who_is_the_dh_cannot_move_to_the_field_or_cause_a_forfeiture_while_he_pitches()
    {
        var s = TwoWayAway().ThreeUp(1);                                              // top 1 done: a5 pitches to the home team
        var before = s.State;

        Assert.Equal("Game.two_way_dh_field_while_pitching", s.Try(new SubstitutionEvent("x1", SubstitutionKind.DhToField, TeamSide.Away, Slot: 6)).Error!.Value.Code);
        Assert.Equal("Game.two_way_dh_forfeit_conflict", s.Try(new SubstitutionEvent("x2", SubstitutionKind.PitchingChange, TeamSide.Away, P("a6"))).Error!.Value.Code);
        Assert.Same(before, s.State);
        Assert.True(s.State.Away.DhActive);
    }

    [Fact]
    public void A_two_way_dh_who_has_left_the_mound_may_then_take_the_field_and_end_the_dh_role()
    {
        var s = TwoWayAway().ThreeUp(1);
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Away, "ab1");                 // allowed ordinary relief: a5 stays the DH
        Assert.True(s.State.Away.DhActive);
        Assert.Equal(FieldPosition.DesignatedHitter, Slot(s.State.Away, 5).Position);

        s.Sub(SubstitutionKind.DhToField, TeamSide.Away, slot: 6);                    // a5 plays left field, a6 leaves, pitcher ab1 bats sixth

        var away = s.State.Away;
        Assert.False(away.DhActive);
        Assert.Equal(("a5", FieldPosition.LeftField), (Slot(away, 5).Player.Value, Slot(away, 5).Position));
        Assert.Equal(("ab1", FieldPosition.Pitcher), (Slot(away, 6).Player.Value, Slot(away, 6).Position));
        Assert.True(s.State.Home.DhActive);
        Assert.Equal(1, away.PitcherUsages.Count(u => u.Pitcher.Value == "a5"));      // his stint as the starting pitcher is kept
    }

    [Fact]
    public void Without_the_two_way_rule_a_starting_pitcher_cannot_be_the_dh_even_in_a_forfeiture_profile()
    {
        var away = Script.Team("a", true, P("a5"));
        var home = Script.Team("h", true);

        Assert.Equal("Game.pitcher_in_lineup", GameReducer.Start(new GameStart("g", TestRules.DhForfeitNoTwoWay, away, home)).Error!.Value.Code);
        var s = new Script(TestRules.DhForfeitNoTwoWay);
        s.Sub(SubstitutionKind.DhToField, TeamSide.Home, slot: 3);                    // the forfeiture itself is independent of the two-way rule
        Assert.False(s.State.Home.DhActive);
    }

    [Fact]
    public void The_profile_rejects_forfeiture_without_a_dh_and_records_the_flags_in_its_hash()
    {
        var invalid = GameRuleSnapshot.Create("x", "v", GameRulesStatus.FictionalTest, "p", [], 3, 4, 3, 9, 11, true, false, false, 15, 3, 3, 9, dhMayBeForfeited: true);
        Assert.Equal("GameRules.dh_forfeit_requires_dh", invalid.Error!.Value.Code);
        Assert.NotEqual(TestRules.Dh.ContentHash, TestRules.Make("test-dh-cap11", dhForfeit: true).ContentHash);
        Assert.NotEqual(TestRules.Dh.ContentHash, TestRules.Make("test-dh-cap11", walkOffLob: false).ContentHash);
    }
}
