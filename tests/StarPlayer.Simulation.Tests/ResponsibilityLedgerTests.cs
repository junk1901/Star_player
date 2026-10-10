using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// C33 independent oracles: the official comment examples (1)-(7) of 2026 OBR 9.16(g), written out from the rule text (docs/2026-official-baseball-rules.pdf,
/// pages 136-137) with Abel..Edward = a1..a5, Peter = hp, Roger = hb1, Sierra = hb2. Expected values are the figures the rule text states, never engine output.
/// Further tests cover 9.16(h)/(i), 9.17(d) and the bookkeeping guarantees (one charge per run, totals equal the credited runs, no change on a rejected event).
/// </summary>
public class ResponsibilityLedgerTests
{
    private static RunnerMove Force(int from, FieldPosition assist, FieldPosition putout) => M(from, 0, MoveCause.Play, OutKind.Force, A(assist), Po(putout));

    private static Script Relieved(string pitcher = "hb1") // a1 (Abel) walked off hp, then the relief pitcher comes in with a1 on first
    {
        var s = new Script().Walk();
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, pitcher);
        return s;
    }

    /// <summary>Every run is charged to exactly one pitcher of the defending team: the pitchers' R sum equals the credited runs of the batting team.</summary>
    private static void AssertEveryRunChargedOnce(Script s)
    {
        var scored = s.State.Plays.SelectMany(p => p.Runs).Count();
        Assert.Equal(scored, s.State.AwayRuns + s.State.HomeRuns);
        Assert.Equal(s.State.AwayRuns, s.State.Stats.Where(e => e.Team == TeamSide.Home && e.Key == StatKey.PitchR).Sum(e => e.Amount));
        Assert.Equal(s.State.HomeRuns, s.State.Stats.Where(e => e.Team == TeamSide.Away && e.Key == StatKey.PitchR).Sum(e => e.Amount));
        Assert.Equal(scored, s.State.Plays.SelectMany(p => p.Runs).Select(r => r.RunnerKey).Distinct().Count()); // no run appears twice
    }

    // ---------------------------------------------------------------- 9.16(g) Comment examples 1-7

    [Fact]
    public void Example_1_a_runner_advanced_by_ground_outs_and_scored_by_a_single_is_charged_to_Peter()
    {
        var s = Relieved();
        s.GroundOut(M(1, 2));        // Baker (a2) grounds out, Abel to second
        s.FlyOut(M(2, 2));           // Charlie (a3) flies out
        s.Single(M(2, 4));           // Daniel (a4) singles, scoring Abel

        Assert.Equal((1, 0), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR)));
        AssertEveryRunChargedOnce(s);
    }

    [Fact]
    public void Example_2_a_runner_forced_out_by_a_fielders_choice_leaves_Peters_charge_with_the_batter_who_took_his_place()
    {
        var s = Relieved();
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [Force(1, FieldPosition.Shortstop, FieldPosition.SecondBase)]); // Baker forces Abel at second
        s.GroundOut(M(1, 2));        // Charlie grounds out, Baker to second
        s.Single(M(2, 4));           // Daniel singles, scoring Baker

        Assert.Equal((1, 0), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR))); // Baker's run is Peter's
        AssertEveryRunChargedOnce(s);
    }

    [Fact]
    public void Example_3_a_runner_of_Peter_out_at_home_on_a_fielders_choice_keeps_Peters_charge_for_the_next_run_to_score()
    {
        var s = Relieved();
        s.Single(M(1, 3));           // Baker singles, Abel to third
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice,
            [M(3, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.Catcher)), M(1, 2)]); // Charlie grounds to short: Abel out at home, Baker to second
        s.FlyOut(M(2, 2), M(1, 1));  // Daniel flies out
        s.Single(M(2, 4), M(1, 2));  // Edward singles, scoring Baker

        Assert.Equal((1, 0), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR))); // Baker's run is charged to Peter
        AssertEveryRunChargedOnce(s);
    }

    [Fact]
    public void Example_4_a_runner_of_Peter_picked_off_is_no_longer_Peters_and_the_run_of_Rogers_runner_is_Rogers()
    {
        var s = Relieved();
        s.Walk();                    // Baker walks (Roger's runner): Abel second, Baker first
        s.FlyOut(M(2, 2), M(1, 1));  // Charlie flies out
        s.Runner(RunnerPlayKind.Pickoff, M(2, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Pitcher), Po(FieldPosition.Shortstop))); // Abel picked off second
        s.Double(M(1, 4));           // Daniel doubles, scoring Baker from first

        Assert.Equal((0, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR)));
        AssertEveryRunChargedOnce(s);
    }

    [Fact]
    public void Example_5_three_pitchers_two_fielders_choice_force_outs_and_a_home_run_charge_one_run_to_each()
    {
        var s = Relieved();          // Peter -> Roger
        s.Walk();                    // Baker walks (Roger)
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb2"); // Sierra relieves Roger
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [Force(2, FieldPosition.Shortstop, FieldPosition.ThirdBase), M(1, 2)]); // Charlie forces Abel at third
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [Force(2, FieldPosition.Shortstop, FieldPosition.ThirdBase), M(1, 2)]); // Daniel forces Baker at third
        s.HomeRun(M(2, 4), M(1, 4)); // Edward homers, three runs

        Assert.Equal(3, s.State.AwayRuns);
        Assert.Equal((1, 1, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR), s.Stat("hb2", StatKey.PitchR)));
        AssertEveryRunChargedOnce(s);
    }

    [Fact]
    public void Example_6_a_runner_forced_out_at_home_leaves_the_runs_of_Baker_and_Charlie_split_between_Peter_and_Roger()
    {
        var s = Relieved();
        s.Walk();                    // Baker walks (Roger)
        s.Single(M(2, 3), M(1, 2));  // Charlie singles, filling the bases (Abel third, Baker second)
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [Force(3, FieldPosition.Shortstop, FieldPosition.Catcher), M(2, 3), M(1, 2)]); // Daniel forces Abel at home
        s.Single(M(3, 4), M(2, 4), M(1, 2)); // Edward singles, scoring Baker and Charlie

        Assert.Equal(2, s.State.AwayRuns);
        Assert.Equal((1, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR)));
        AssertEveryRunChargedOnce(s);
    }

    [Fact]
    public void Example_7_supported_equivalent_a_runner_of_Peter_out_trying_for_an_extra_base_is_no_longer_Peters()
    {
        // The rule text has Baker advance to second on the throw that retires Abel. A batter's own advance on a throw is not an event of the Phase 2 model
        // (docs/game-engine.md, C33 unsupported scope): Baker is therefore moved to second by a stolen base before the scoring single. Responsibility is unaffected.
        var s = Relieved();
        s.Single(M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.RightField), Po(FieldPosition.ThirdBase)) with { OutAt = 3 }); // Baker singles, Abel out at third
        s.Runner(RunnerPlayKind.Steal, M(1, 2, MoveCause.Steal));
        s.Single(M(2, 4));           // Charlie singles, scoring Baker

        Assert.Equal(1, s.State.Outs);
        Assert.Equal((0, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR))); // Baker's run is charged to Roger
        AssertEveryRunChargedOnce(s);
    }

    // ---------------------------------------------------------------- the boundaries of the rule

    [Fact]
    public void A_caught_stealing_ends_the_charge_of_the_pitcher_who_left_the_runner()
    {
        var s = Relieved();
        s.Runner(RunnerPlayKind.Steal, M(1, 0, MoveCause.Steal, OutKind.Tag, A(FieldPosition.Catcher), Po(FieldPosition.SecondBase)));
        s.Walk();                    // Baker
        s.HomeRun(M(1, 4));          // Charlie homers: Baker and Charlie score

        Assert.Equal((0, 2), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR)));
        AssertEveryRunChargedOnce(s);
    }

    [Fact]
    public void The_charge_Peter_keeps_through_a_fielders_choice_ends_when_the_batter_who_took_it_is_later_picked_off()
    {
        var s = Relieved();
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [Force(1, FieldPosition.Shortstop, FieldPosition.SecondBase)]); // Baker forces Abel: Baker carries Peter's charge
        s.Runner(RunnerPlayKind.Pickoff, M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Pitcher), Po(FieldPosition.FirstBase)));            // Baker picked off first
        s.Walk();                    // Charlie
        s.HomeRun(M(1, 4));          // Daniel homers: Charlie and Daniel score

        Assert.Equal((0, 2), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR)));
        AssertEveryRunChargedOnce(s);
    }

    [Fact]
    public void A_fielders_choice_that_puts_nobody_out_gives_the_batter_to_the_pitcher_on_the_mound()
    {
        var s = Relieved();
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [M(1, 2)]); // Baker reaches on a fielder's choice, Abel safe at second
        s.Single(M(2, 4), M(1, 3));  // Charlie singles: Abel scores, Baker to third
        s.Single(M(3, 4), M(1, 2));  // Daniel singles: Baker scores

        Assert.Equal((1, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR)));
        AssertEveryRunChargedOnce(s);
    }

    [Fact]
    public void A_pinch_runner_for_Peters_runner_carries_Peters_charge()
    {
        var s = Relieved();
        s.Sub(SubstitutionKind.PinchRun, TeamSide.Away, "ab1", @base: 1);
        s.HomeRun(M(1, 4));          // Baker homers: the pinch runner and Baker score

        Assert.Equal("ab1", s.State.Plays.Last().Runs[0].Runner.Value);
        Assert.Equal((1, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR)));
        AssertEveryRunChargedOnce(s);
    }

    [Fact]
    public void When_the_relief_pitcher_walks_the_first_batter_on_a_decided_count_the_walked_runner_is_still_Peters_charge()
    {
        var s = new Script();        // 9.16(h): 3-1 when Roger enters, ball four is Peter's walk
        s.Take(PitchOutcome.Ball).Take(PitchOutcome.Ball).Take(PitchOutcome.Ball).Take(PitchOutcome.CalledStrike);
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");
        s.Take(PitchOutcome.Ball);
        s.Walk();                    // Baker walks against Roger
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [Force(2, FieldPosition.Shortstop, FieldPosition.ThirdBase), M(1, 2)]); // Charlie forces Abel at third
        s.HomeRun(M(2, 4), M(1, 4)); // Daniel homers: Baker, Charlie, Daniel

        Assert.Equal((1, 1), (s.Stat("hp", StatKey.PitchBB), s.Stat("hb1", StatKey.PitchBB))); // the 3-1 walk is Peter's, Baker's is Roger's
        Assert.Equal((1, 2), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR)));
        AssertEveryRunChargedOnce(s);
    }

    // ---------------------------------------------------------------- R and ER are separate figures

    [Fact]
    public void Runs_charged_by_the_ledger_get_their_earned_verdict_from_each_pitchers_own_reconstruction()
    {
        var s = Relieved();
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [Force(1, FieldPosition.Shortstop, FieldPosition.SecondBase)]); // Baker forces Abel: Baker carries Peter's charge
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.ReachedOnError, [M(1, 2)], errors: [FieldPosition.Shortstop]);                    // Charlie reaches on E6
        s.HomeRun(M(2, 4), M(1, 4)); // Daniel homers: Baker, Charlie, Daniel
        s.Strikeout().Strikeout();

        Assert.Equal(3, s.State.AwayRuns);
        Assert.Equal((1, 2), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR)));
        Assert.Equal((1, 1), (s.Stat("hp", StatKey.PitchER), s.Stat("hb1", StatKey.PitchER))); // Baker earned (Peter), Charlie unearned and Daniel earned (Roger)
        Assert.Equal(2, s.Team(TeamSide.Away, StatKey.TeamER) + s.Team(TeamSide.Home, StatKey.TeamER));
        AssertEveryRunChargedOnce(s);
    }

    [Fact]
    public void Official_9_16_i_example_1_keeps_its_figures_with_the_ledger()
    {
        // two out, Peter: Abel walks, Baker reaches on an error; Roger relieves; Charlie homers, three runs. Two unearned to Peter, one earned to Roger, three unearned to the team.
        var s = new Script().Outs(2).Walk();
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.ReachedOnError, [M(1, 2)], errors: [FieldPosition.Shortstop]);
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");
        s.HomeRun(M(2, 4), M(1, 4));
        s.Strikeout();

        Assert.Equal((2, 1), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR)));
        Assert.Equal((0, 1), (s.Stat("hp", StatKey.PitchER), s.Stat("hb1", StatKey.PitchER)));
        AssertEveryRunChargedOnce(s);
    }

    // ---------------------------------------------------------------- 9.17(d) losing pitcher

    [Fact]
    public void The_loss_goes_to_Peter_when_the_go_ahead_run_is_the_one_Peters_charge_was_carried_to_by_a_fielders_choice()
    {
        var rules = TestRules.Make("test-c33-loss", starterMin: 0);
        var s = new Script(rules);
        s.Walk();                    // Abel walks against Peter
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [Force(1, FieldPosition.Shortstop, FieldPosition.SecondBase)]); // Baker forces Abel
        s.GroundOut(M(1, 2));        // Charlie
        s.Single(M(2, 4));           // Daniel singles: Baker scores, the only run of the game
        s.FlyOut(M(1, 1));
        s.ThreeUp(17).Finish();

        Assert.Equal((1, 0), (s.State.AwayRuns, s.State.HomeRuns));
        Assert.Equal("hp", s.Box.Decisions.Single(d => d.Kind == DecisionKind.Loss).Pitcher.Value);
        Assert.Equal("ap", s.Box.Decisions.Single(d => d.Kind == DecisionKind.Win).Pitcher.Value);
        Assert.Equal((1, 0), (s.Line("hp", StatKey.PitchR), s.Line("hb1", StatKey.PitchR)));
        AssertEveryRunChargedOnce(s);
    }

    // ---------------------------------------------------------------- invalid events change nothing

    [Fact]
    public void A_rejected_event_leaves_the_state_and_every_charge_untouched()
    {
        var s = Relieved();
        s.Approach();
        var before = s.State;

        // Abel is forced at second by Baker, so the out is a force out: declaring a tag is rejected
        var wrongKind = s.Try(new InPlayEvent("bad1", BattedBallType.Ground, InPlayResult.FieldersChoice, false,
            [M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase))]));
        Assert.Equal("Game.out_kind_mismatch", wrongKind.Error!.Value.Code);
        Assert.Same(before, s.State);

        var noPutout = s.Try(new InPlayEvent("bad2", BattedBallType.Ground, InPlayResult.FieldersChoice, false, [M(1, 0, MoveCause.Play, OutKind.Force)]));
        Assert.Equal("Game.out_credit_invalid", noPutout.Error!.Value.Code);
        Assert.Same(before, s.State);

        s.InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [Force(1, FieldPosition.Shortstop, FieldPosition.SecondBase)]);
        s.GroundOut(M(1, 2));
        s.Single(M(2, 4));
        Assert.Equal((1, 0), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR))); // the rejected attempts changed no later charge
    }

    // ---------------------------------------------------------------- deterministic replay

    [Fact]
    public void Replaying_the_same_events_gives_the_same_charges_and_the_same_receipts()
    {
        var s = Relieved();
        s.Walk();
        s.Single(M(2, 3), M(1, 2));
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [Force(3, FieldPosition.Shortstop, FieldPosition.Catcher), M(2, 3), M(1, 2)]);
        s.Single(M(3, 4), M(2, 4), M(1, 2));

        var again = GameReducer.Start(new GameStart("g1", s.Rules, Script.Team("a", s.Rules.DesignatedHitter), Script.Team("h", s.Rules.DesignatedHitter))).Value;
        foreach (var e in s.Log)
            again = GameReducer.Apply(again, e).Value.State;

        Assert.Equal(s.State.Stats, again.Stats);
        Assert.Equal(s.State.AppliedEvents, again.AppliedEvents);
        Assert.Equal(s.State.Plays.SelectMany(p => p.Runs).Select(r => (r.RunnerKey, r.ResponsiblePitcher)),
            again.Plays.SelectMany(p => p.Runs).Select(r => (r.RunnerKey, r.ResponsiblePitcher)));
    }

    // ---------------------------------------------------------------- ambiguous fielder's choice (Codex re-review, High)

    /// <summary>Peter's runner on second, Roger's on first, Sierra (hb2) pitching: Abel (a1) walked off hp, Baker (a2) walked off hb1.</summary>
    private static Script PeterSecondRogerFirstSierraPitching()
    {
        var s = Relieved();
        s.Walk();                                                  // Baker (Roger): Abel second, Baker first
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb2"); // Sierra
        s.Approach();
        return s;
    }

    private static RunnerMove ForcedAtSecond(int? sequence = null) =>
        sequence is { } n ? Seq(Force(1, FieldPosition.Shortstop, FieldPosition.SecondBase), n) : Force(1, FieldPosition.Shortstop, FieldPosition.SecondBase);

    private static RunnerMove ThrownOutAtHome(int? sequence = null)
    {
        var move = M(2, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.Catcher)) with { OutAt = 4 };
        return sequence is { } n ? move with { Sequence = n } : move;
    }

    private static void AssertRejectedAsAmbiguousAndUntouched(Script s, string id, params RunnerMove[] moves)
    {
        var before = s.State;
        var e = new InPlayEvent(id, BattedBallType.Ground, InPlayResult.FieldersChoice, false, moves.ToImmutableList());

        var first = s.Try(e);
        var second = s.Try(e); // delivered again: same error, still nothing recorded

        Assert.Equal("Game.ambiguous_responsibility", first.Error!.Value.Code);
        Assert.Equal(first.Error!.Value, second.Error!.Value);
        Assert.Same(before, s.State);
        Assert.DoesNotContain(s.State.AppliedEvents, r => r.EventId == id);
    }

    [Fact]
    public void A_fielders_choice_that_retires_runners_of_two_different_pitchers_is_refused_because_the_slot_to_pass_on_is_not_decided_by_the_input()
    {
        var s = PeterSecondRogerFirstSierraPitching();
        var runnersBefore = (s.State.Bases.First!.ResponsiblePitcher.Value, s.State.Bases.Second!.ResponsiblePitcher.Value);

        AssertRejectedAsAmbiguousAndUntouched(s, "amb1", ForcedAtSecond(), ThrownOutAtHome());

        Assert.Equal(("hb1", "hp"), runnersBefore);
        Assert.Equal(("hb1", "hp"), (s.State.Bases.First!.ResponsiblePitcher.Value, s.State.Bases.Second!.ResponsiblePitcher.Value)); // no slot was handed to the batter
        Assert.Equal((0, 0, 0), (s.State.Outs, s.State.AwayRuns, s.Stat("hb2", StatKey.PitchBF)));
    }

    [Fact]
    public void The_order_of_the_two_outs_does_not_make_the_play_decidable()
    {
        var forcedFirst = PeterSecondRogerFirstSierraPitching();
        AssertRejectedAsAmbiguousAndUntouched(forcedFirst, "amb2", ForcedAtSecond(1), ThrownOutAtHome(2));

        var homeFirst = PeterSecondRogerFirstSierraPitching();
        AssertRejectedAsAmbiguousAndUntouched(homeFirst, "amb3", ForcedAtSecond(2), ThrownOutAtHome(1));
    }

    [Fact]
    public void A_refused_play_can_be_replaced_by_a_decidable_one_and_the_game_goes_on_with_the_old_charges()
    {
        var s = PeterSecondRogerFirstSierraPitching();
        s.Try(new InPlayEvent("amb4", BattedBallType.Ground, InPlayResult.FieldersChoice, false, [ForcedAtSecond(), ThrownOutAtHome()]));

        // the scorer decides it was an ordinary single with Abel thrown out going for more: not a fielder's choice, so no slot is passed on
        s.InPlay(BattedBallType.Line, InPlayResult.Single, [M(2, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.Catcher)) with { OutAt = 4 }, M(1, 3)]);

        Assert.Equal(1, s.State.Outs);
        Assert.Equal(1, s.Stat("hb2", StatKey.PitchBF));
        Assert.Equal("hb1", s.State.Bases.Third!.ResponsiblePitcher.Value); // Baker keeps Roger's slot, Abel's (Peter's) slot is gone
    }

    [Fact]
    public void Two_runners_of_the_same_pitcher_retired_on_a_fielders_choice_pass_that_pitchers_slot_on_without_ambiguity()
    {
        var s = new Script().Walk().Walk();                         // a1 second, a2 first: both hp's
        s.Sub(SubstitutionKind.PitchingChange, TeamSide.Home, "hb1");
        s.Approach().InPlay(BattedBallType.Ground, InPlayResult.FieldersChoice, [ForcedAtSecond(), ThrownOutAtHome()]);
        s.Walk();                                                    // Charlie (Roger) forces the FC batter to second
        s.Single(M(2, 4), M(1, 3));                                  // Daniel: the batter who took the FC slot scores from second

        Assert.Equal(2, s.State.Outs);
        Assert.Equal(1, s.State.AwayRuns);
        Assert.Equal((1, 0), (s.Stat("hp", StatKey.PitchR), s.Stat("hb1", StatKey.PitchR))); // the slot Peter kept was charged first
        AssertEveryRunChargedOnce(s);
    }

    [Fact]
    public void Other_ways_to_enter_the_same_play_are_not_fielders_choices_and_remove_both_slots()
    {
        // 9.16(g) passes a slot on only when the batter reaches on a fielder's choice; a hit or an error with outs on the bases is the scorer's other classification
        var hit = PeterSecondRogerFirstSierraPitching();
        hit.InPlay(BattedBallType.Line, InPlayResult.Single,
            [M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.ThirdBase)) with { OutAt = 3 }, ThrownOutAtHome()]);
        var error = PeterSecondRogerFirstSierraPitching();
        error.InPlay(BattedBallType.Ground, InPlayResult.ReachedOnError,
            [M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Shortstop), Po(FieldPosition.ThirdBase)) with { OutAt = 3 }, ThrownOutAtHome()], errors: [FieldPosition.Shortstop]);

        foreach (var s in new[] { hit, error })
        {
            Assert.Equal(2, s.State.Outs);
            Assert.Equal("hb2", s.State.Bases.First!.ResponsiblePitcher.Value); // only the batter, with Sierra's own slot
            Assert.Null(s.State.Bases.Second);
        }
    }

    [Fact]
    public void Independent_running_outs_still_end_the_charge_of_the_pitchers_they_belong_to()
    {
        var pickoff = Relieved();
        pickoff.Runner(RunnerPlayKind.Pickoff, M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.Pitcher), Po(FieldPosition.FirstBase)));
        pickoff.Walk();
        pickoff.HomeRun(M(1, 4));

        var thrownOutGoingForMore = Relieved();
        thrownOutGoingForMore.Single(M(1, 0, MoveCause.Play, OutKind.Tag, A(FieldPosition.RightField), Po(FieldPosition.ThirdBase)) with { OutAt = 3 });
        thrownOutGoingForMore.Runner(RunnerPlayKind.Steal, M(1, 2, MoveCause.Steal));
        thrownOutGoingForMore.Single(M(2, 4));

        Assert.Equal((0, 2), (pickoff.Stat("hp", StatKey.PitchR), pickoff.Stat("hb1", StatKey.PitchR)));
        Assert.Equal((0, 1), (thrownOutGoingForMore.Stat("hp", StatKey.PitchR), thrownOutGoingForMore.Stat("hb1", StatKey.PitchR)));
    }

    [Fact]
    public void An_accepted_fielders_choice_is_a_harmless_duplicate_when_delivered_again_and_a_refused_one_never_becomes_one()
    {
        var s = Relieved().Approach();
        var accepted = new InPlayEvent("fc1", BattedBallType.Ground, InPlayResult.FieldersChoice, false, [Force(1, FieldPosition.Shortstop, FieldPosition.SecondBase)]);
        s.Ok(accepted);
        var after = s.State;

        var again = GameReducer.Apply(after, accepted);

        Assert.True(again.IsSuccess && again.Value.WasDuplicate);
        Assert.Same(after, again.Value.State);

        var two = PeterSecondRogerFirstSierraPitching();
        var refused = new InPlayEvent("fc2", BattedBallType.Ground, InPlayResult.FieldersChoice, false, [ForcedAtSecond(), ThrownOutAtHome()]);
        Assert.Equal("Game.ambiguous_responsibility", GameReducer.Apply(two.State, refused).Error!.Value.Code);
        Assert.Equal("Game.ambiguous_responsibility", GameReducer.Apply(two.State, refused).Error!.Value.Code); // never a duplicate: it has no receipt
    }
}
