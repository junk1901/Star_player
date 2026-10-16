using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E (Game Calling v4, GC-B and GC-S1): the pitcher's Control and the catcher's GameCalling are read from the player input set the GAME pinned, for the players the real <see cref="GameState"/> says are on the
/// mound and behind the plate, and frozen when the pitch is prepared - before the first draw. <c>CheckPrepared</c> re-derives both from the real state and the real set (never from the preparation, an audit or a
/// sheet someone hands in later), so a changed Control, a changed rating, another player, another game or another set is each refused with its own reason.
/// </summary>
public class GameCallingPrepareTests
{
    private static string[] Modes => ["automated", "non_automated"];

    private static Result<NonInPlayPitchCandidate> Rebuild(AuthoritativeGame g, PreparedPitch prepared, GameState nowState, PlayerInputSet? set, int sheetControl = 50, TuningProfile? tuning = null)
    {
        var (b, p) = Gc.SheetsOf(nowState, sheetControl);
        return PitchPreparation.BuildNonInPlayCandidate(prepared, nowState, g.Rng, tuning ?? Gc.T4, b, p, g.LastEventSequence, default, set);
    }

    private static GameState SwapCatcherWithFirstBase(GameState state)
    {
        var side = state.FieldingSide;
        var a = state.Team(side).SlotAt(FieldPosition.Catcher)!.Order;
        var b = state.Team(side).SlotAt(FieldPosition.FirstBase)!.Order;
        return Ok(GameReducer.Apply(state, new SubstitutionEvent("sub-swap", SubstitutionKind.PositionSwap, side, default, null, a, b))).State;
    }

    // ---------------------------------------------------------------- Prepare freezes the real inputs

    [Theory]
    [InlineData("automated")]
    [InlineData("non_automated")]
    public void Prepare_freezes_the_real_pitcher_control_and_catcher_game_calling_before_the_first_draw(string mode)
    {
        var w = Gc.Game(RulesFor(mode), control: 83, calling: 33, framing: 61);
        var g = Aggregate(w.Driver, 5);

        var prepared = Gc.Prepare(g, w.Set, 83);

        var pitcher = Assert.IsType<FrozenPitcherInput>(prepared.PitcherInput);
        Assert.Equal((g.State.Fielding.Pitcher, 83, w.Set.Id, Gid), (pitcher.Pitcher, pitcher.Control, pitcher.InputSetId, pitcher.GameId));
        var catcher = Assert.IsType<FrozenCatcherInput>(prepared.Catcher);
        Assert.Equal((Gc.CatcherOf(g.State), 33, Gid, w.Set.Id), (catcher.Catcher, catcher.GameCalling, catcher.GameId, catcher.InputSetId));
        Assert.Equal(mode == "non_automated" ? 61 : null, catcher.ReceivingFraming);                 // automated: Game Calling only; Framing is not read
        Assert.Equal(w.Set.Id, prepared.PlayerInputsId);
        Assert.Equal(g.Rng.StateHash, prepared.StartRngStateHash);                                   // H0 is the caller's own position: nothing was drawn from it
        Assert.Equal(("prob-model-v4", "tuning-virtual-proto-v4"), (prepared.ProbabilityModelVersion, prepared.TuningVersion));
    }

    [Theory]
    [InlineData("automated")]
    [InlineData("non_automated")]
    public void The_location_step_carries_exactly_the_pinned_control_and_the_catcher_game_calling_in_slot_order(string mode)
    {
        var w = Gc.Game(RulesFor(mode), control: 83, calling: 33, framing: 61);
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var draft = Gc.Prepare(Aggregate(w.Driver, seed), w.Set, 83).Draft;
            var location = Gc.Location(draft);
            var control = location.Trace.Modifiers[0];
            var calling = location.Trace.Modifiers[1];

            Assert.Equal(2, location.Trace.Modifiers.Count);
            Assert.Equal(("pitcher.control", "83", true), (control.Slot, control.Input, control.Applied));
            Assert.Equal(("catcher.game_calling", "33", Gc.AimedClass(draft) == StarPlayer.Domain.Baseball.PitchZone.BoundaryClass.Borderline), (calling.Slot, calling.Input, calling.Applied));
            Assert.Equal(calling.Applied ? 1_000_000 + (33 - 50) * 1000 : 1_000_000, calling.FactorPpm);      // 983,000 on a Borderline aim, exactly neutral on a Clear one
            Assert.Equal(1_000_000 + (83 - 50) * 1500, control.FactorPpm);
            // the target stage has no modifier, Framing never reaches the location stage and Game Calling never reaches the called pitch
            Assert.Empty(Gc.Target(draft).Trace.Modifiers);
            Assert.DoesNotContain(location.Trace.Modifiers, m => m.Slot is "catcher.framing" or "battery.chemistry");
            foreach (var called in draft.Steps.Where(s => s.Stage == StageIds.CalledPitch))
                Assert.DoesNotContain(called.Trace.Modifiers, m => m.Slot is "catcher.game_calling" or "pitcher.control");
        }
    }

    // ---------------------------------------------------------------- Prepare refuses before drawing

    [Fact]
    public void Prepare_refuses_a_missing_set_the_wrong_content_version_and_a_foreign_set_with_typed_errors()
    {
        foreach (var mode in Modes)
        {
            var w = Gc.Game(RulesFor(mode));
            var g = Aggregate(w.Driver, 3);

            Assert.Equal("PitchPrepare.player_inputs_missing", Code(Gc.TryPrepare(g, null)));                                    // the game pins a set: it is needed
            Assert.Equal("PitchPrepare.catcher_input_format", Code(Gc.TryPrepare(g, Fr.SetV2(_ => 50))));                         // a content-version-2 set carries no game calling
            Assert.Equal("PitchPrepare.catcher_input_format", Code(Gc.TryPrepare(g, Fr.SetV1(_ => 50))));
            var foreign = Gc.SetV3(id => new Gc.Spec(50, (51, 51)));                                                              // a v3 set, but not the pinned one
            Assert.NotEqual(w.Set.Id, foreign.Id);
            Assert.Equal("PitchBranch.player_inputs_mismatch", Code(Gc.TryPrepare(g, foreign)));
            Assert.True(Gc.TryPrepare(g, w.Set).IsSuccess);                                                                       // control
        }
    }

    [Fact]
    public void Prepare_refuses_a_missing_required_rating_for_the_player_standing_there_and_never_uses_fifty()
    {
        var automatedProbe = Fr.AtStart(Automated, null).State;
        var (pitcher, catcher) = (Gc.PitcherOf(automatedProbe), Gc.CatcherOf(automatedProbe));

        foreach (var (mode, expectedCode) in new[] { ("automated", "PitchPrepare.catcher_game_calling_missing"), ("non_automated", "PitchPrepare.catcher_ratings_missing") })
        {
            // the catcher has no catcher ratings in the pinned set
            var w = GameWith(mode, id => new Gc.Spec(50, id == catcher ? null : (50, 50)));
            Assert.Equal(expectedCode, Code(Gc.TryPrepare(Aggregate(w.Driver, 4), w.Set)));

            // the pitcher has no pitching ratings in the pinned set (the sheet handed in still shows a Control)
            var noPitching = GameWith(mode, id => new Gc.Spec(id == pitcher ? null : 50, (50, 50)));
            Assert.Equal("PitchPrepare.pitcher_ratings_missing", Code(Gc.TryPrepare(Aggregate(noPitching.Driver, 4), noPitching.Set)));
        }
    }

    private static Gc.World GameWith(string mode, Func<PlayerId, Gc.Spec> spec)
    {
        var rules = RulesFor(mode);
        var probe = Fr.AtStart(rules, null).State;
        var set = Gc.SetV3(spec);
        return new Gc.World(Fr.InSequence(rules, Gc.Exec(set)), set, Gc.PitcherOf(probe), Gc.CatcherOf(probe), 50);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(49)]
    [InlineData(51)]
    [InlineData(80)]
    [InlineData(100)]
    public void Prepare_refuses_a_pitcher_sheet_whose_control_is_not_the_pinned_one_before_any_draw(int sheetControl)
    {
        foreach (var mode in Modes)
        {
            var w = Gc.Game(RulesFor(mode), control: 50);
            var g = Aggregate(w.Driver, 6);
            var before = Gc.Basis.Of(g);

            var refused = Gc.TryPrepare(g, w.Set, control: sheetControl);

            Assert.Equal("PitchPrepare.pitcher_control_mismatch", Code(refused));                  // Prepare's own code: the resolver was never reached
            Assert.Same(before.Game, g);
            Assert.Equal(before.RngHash, g.Rng.StateHash);
        }
    }

    [Fact]
    public void The_pinned_control_is_used_not_the_sheet_so_two_different_pinned_values_give_two_different_pitches()
    {
        var weak = Gc.Game(NonAutomated, control: 0);
        var strong = Gc.Game(NonAutomated, control: 100);
        var differing = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var a = Gc.Prepare(Aggregate(weak.Driver, seed), weak.Set, 0).Draft;
            var b = Gc.Prepare(Aggregate(strong.Driver, seed), strong.Set, 100).Draft;
            Assert.Equal(Gc.Target(a).Roll, Gc.Target(b).Roll);                                         // the same seed gives the same draws ...
            Assert.Equal(Gc.Location(a).Roll, Gc.Location(b).Roll);
            Assert.NotEqual(Gc.Location(a).Trace.Final, Gc.Location(b).Trace.Final);                    // ... read from different Control distributions
            if (Gc.Location(a).Outcome != Gc.Location(b).Outcome)
                differing++;
        }

        Assert.True(differing > 0, "a Control of 0 and a Control of 100 must land differently on some seed");
    }

    [Fact]
    public void An_automated_game_never_reads_the_catchers_framing_so_changing_it_changes_no_pitch()
    {
        var low = Gc.Game(Automated, framing: 0, calling: 70);
        var high = Gc.Game(Automated, framing: 100, calling: 70);
        Assert.NotEqual(low.Set.Id, high.Set.Id);                                                        // the identity covers framing (content version 3) ...
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var a = Gc.Prepare(Aggregate(low.Driver, seed), low.Set).Draft;
            var b = Gc.Prepare(Aggregate(high.Driver, seed), high.Set).Draft;
            Assert.Null(Gc.Prepare(Aggregate(low.Driver, seed), low.Set).Catcher!.ReceivingFraming);
            Assert.True(a.Steps.Select(Describe).SequenceEqual(b.Steps.Select(Describe)));              // ... but no step of any pitch differs
            Assert.Equal(a.NextRng.StateHash, b.NextRng.StateHash);
        }
    }

    private static string Describe(PitchBranchStep s) =>
        $"{s.Stage}|{s.Condition}|{s.Outcome}|{s.Stream.Canonical}|{s.Roll}|{s.RawDraws}|{string.Join(",", s.Trace.Final)}|{string.Join(";", s.Trace.Modifiers.Select(m => $"{m.Slot}={m.Input}/{m.FactorPpm}/{m.Applied}"))}";

    // ---------------------------------------------------------------- CheckPrepared (GC-S1): the Control and the catcher are looked at again

    [Fact]
    public void An_untouched_preparation_is_accepted_and_every_change_below_is_not()
    {
        foreach (var mode in Modes)
        {
            var w = Gc.Game(RulesFor(mode), control: 60, calling: 40, framing: 55, approach: false);
            var g = Aggregate(w.Driver, 11);
            var prepared = Gc.Prepare(g, w.Set, 60, take: true);
            Assert.Null(Rebuild(g, prepared, g.State, w.Set, 60).Error);
        }
    }

    [Fact]
    public void The_same_pitcher_with_another_control_on_the_sheet_is_refused_with_its_own_reason()
    {
        foreach (var mode in Modes)
        {
            var w = Gc.Game(RulesFor(mode), control: 50, approach: false);
            var g = Aggregate(w.Driver, 12);
            var prepared = Gc.Prepare(g, w.Set, 50, take: true);
            var sheet80 = Gc.Sheet(g.State.Fielding.Pitcher, new Gc.Spec(80));                                  // same PlayerId, same handedness, another Control

            var (b, _) = Gc.SheetsOf(g.State);
            var result = PitchPreparation.BuildNonInPlayCandidate(prepared, g.State, g.Rng, Gc.T4, b, sheet80, g.LastEventSequence, default, w.Set);
            Assert.Equal("PitchCandidate.pitcher_control_changed", Code(result));
            Assert.Equal(ThrowingHand.Right, sheet80.Player.Throws);                                               // nothing else about him differs
            Assert.Equal(prepared.PitcherId, sheet80.Id);
        }
    }

    [Fact]
    public void A_new_set_that_changes_only_the_pitchers_control_is_another_identity_and_is_refused()
    {
        foreach (var mode in Modes)
        {
            var w = Gc.Game(RulesFor(mode), control: 50, approach: false);
            var g = Aggregate(w.Driver, 13);
            var prepared = Gc.Prepare(g, w.Set, 50, take: true);
            var otherSet = Gc.SetFor(Fr.AtStart(RulesFor(mode), null).State, 80, 50, 50);

            Assert.NotEqual(w.Set.Id, otherSet.Id);
            Assert.Equal("PitchCandidate.player_inputs_changed", Code(Rebuild(g, prepared, g.State, otherSet, 80)));        // set and sheet agree on 80: still another set
            Assert.Equal("PitchCandidate.player_inputs_changed", Code(Rebuild(g, prepared, g.State, otherSet, 50)));
            Assert.Equal("PitchCandidate.player_inputs_changed", Code(Rebuild(g, prepared, g.State, null, 50)));
            Assert.Equal("PitchCandidate.player_inputs_changed", Code(Rebuild(g, prepared, g.State, Fr.SetV2(_ => 50), 50)));
        }
    }

    [Fact]
    public void A_recorded_control_that_is_changed_is_not_believed_the_expectation_is_the_real_set()
    {
        foreach (var mode in Modes)
        {
            var w = Gc.Game(RulesFor(mode), control: 50, approach: false);
            var g = Aggregate(w.Driver, 14);
            var prepared = Gc.Prepare(g, w.Set, 50, take: true);
            var real = prepared.PitcherInput!;

            // the frozen record says 80: the real set (and the sheet) still say 50
            var forged = prepared.WithPitcher(new FrozenPitcherInput(real.GameId, real.InputSetId, real.Pitcher, 80));
            Assert.Equal("PitchCandidate.pitcher_control_changed", Code(Rebuild(g, forged, g.State, w.Set, 50)));
            // ... and when the sheet is made to agree with the forged record the pinned set still disagrees with both
            Assert.Equal("PitchCandidate.pitcher_control_changed", Code(Rebuild(g, forged, g.State, w.Set, 80)));
            // the draft is rewritten as if Control 80 had been used (distribution + modifier input) and the record agrees: the set is still the authority
            var lie = prepared.Draft.WithStep(1, s => s with { Trace = s.Trace with { Modifiers = s.Trace.Modifiers.Select(m => m.Slot == "pitcher.control" ? m with { Input = "80", FactorPpm = 1_045_000 } : m).ToArray() } });
            var both = prepared.With(lie).WithPitcher(new FrozenPitcherInput(real.GameId, real.InputSetId, real.Pitcher, 80));
            Assert.Equal("PitchCandidate.pitcher_control_changed", Code(Rebuild(g, both, g.State, w.Set, 50)));
        }
    }

    [Fact]
    public void A_preparation_with_a_missing_or_an_unneeded_pitcher_input_is_refused_by_the_current_model()
    {
        var w = Gc.Game(NonAutomated, approach: false);
        var g = Aggregate(w.Driver, 15);
        var prepared = Gc.Prepare(g, w.Set, 50, take: true);
        Assert.Equal("PitchCandidate.pitcher_input_mismatch", Code(Rebuild(g, prepared.WithPitcher(null), g.State, w.Set)));

        // a model v3 preparation that carries a pitcher input is refused as well (it has nothing to freeze)
        var v3 = Fr.NonAutomatedGame(60, approach: false);
        var g3 = Aggregate(v3.Driver, 15);
        var p3 = Fr.Prepare(g3, v3.Set, take: true);
        var attached = p3.WithPitcher(new FrozenPitcherInput(Gid, v3.Set.Id, g3.State.Fielding.Pitcher, 50));
        var (b, p) = Sheets(g3.State);
        Assert.Equal("PitchCandidate.pitcher_input_mismatch", Code(PitchPreparation.BuildNonInPlayCandidate(attached, g3.State, g3.Rng, Fr.T3, b, p, g3.LastEventSequence, default, v3.Set)));
        Assert.True(PitchPreparation.BuildNonInPlayCandidate(p3, g3.State, g3.Rng, Fr.T3, b, p, g3.LastEventSequence, default, v3.Set).IsSuccess);   // v3 itself is unchanged
    }

    [Fact]
    public void A_pitcher_input_of_another_game_another_set_another_pitcher_or_a_set_the_game_did_not_pin_is_refused()
    {
        var w = Gc.Game(NonAutomated, approach: false);
        var g = Aggregate(w.Driver, 16);
        var prepared = Gc.Prepare(g, w.Set, 50, take: true);
        var real = prepared.PitcherInput!;

        Assert.Equal("PitchCandidate.pitcher_game_mismatch", Code(Rebuild(g, prepared.WithPitcher(new FrozenPitcherInput("game:other", real.InputSetId, real.Pitcher, 50)), g.State, w.Set)));
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Rebuild(g, prepared.WithPitcher(new FrozenPitcherInput(real.GameId, new string('c', 64), real.Pitcher, 50)), g.State, w.Set)));
        var otherPitcher = BasePlayers.First(id => id != real.Pitcher);
        Assert.Equal("PitchCandidate.pitcher_changed", Code(Rebuild(g, prepared.WithPitcher(new FrozenPitcherInput(real.GameId, real.InputSetId, otherPitcher, 50)), g.State, w.Set)));

        // the set the caller holds and the preparation agree, but the GAME pinned another one (the execution is the authority, not the preparation)
        var unpinned = g.State with { Execution = g.State.Execution! with { PlayerInputsId = new string('d', 64) } };
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Rebuild(g, prepared, unpinned, w.Set)));
    }

    [Fact]
    public void A_real_pitching_change_after_the_preparation_is_found_even_when_the_reliever_has_the_same_control()
    {
        foreach (var rating in new[] { 50, 77 })
        {
            var w = Gc.Game(NonAutomated, control: rating, approach: false);
            var g = Aggregate(w.Driver, 17);
            var prepared = Gc.Prepare(g, w.Set, rating, take: true);
            Assert.Null(Rebuild(g, prepared, g.State, w.Set, rating).Error);

            var reliever = g.State.FieldingSide == TeamSide.Home ? "hb1" : "ab1";
            w.Driver.Substitute(SubstitutionKind.PitchingChange, g.State.FieldingSide, PlayerId.Create(reliever).Value);
            var now = w.Driver.State;
            Assert.NotEqual(g.State.Fielding.Pitcher, now.Fielding.Pitcher);

            Assert.Equal("PitchCandidate.pitcher_changed", Code(Rebuild(g, prepared, now, w.Set, rating)));
        }
    }

    [Fact]
    public void A_real_catcher_change_is_found_and_so_is_a_new_game_calling_rating_for_the_same_catcher()
    {
        foreach (var mode in Modes)
        {
            var w = Gc.Game(RulesFor(mode), calling: 60, approach: false);
            var g = Aggregate(w.Driver, 18);
            var prepared = Gc.Prepare(g, w.Set, 50, take: true);
            Assert.Null(Rebuild(g, prepared, g.State, w.Set).Error);

            var swapped = SwapCatcherWithFirstBase(g.State);
            Assert.NotEqual(Gc.CatcherOf(g.State), Gc.CatcherOf(swapped));
            Assert.Equal("PitchCandidate.catcher_changed", Code(Rebuild(g, prepared, swapped, w.Set)));

            // the same catcher, a different GameCalling: another set identity ...
            var probe = Fr.AtStart(RulesFor(mode), null).State;
            var other = Gc.SetFor(probe, 50, 50, 61);
            Assert.NotEqual(w.Set.Id, other.Id);
            Assert.Equal("PitchCandidate.player_inputs_changed", Code(Rebuild(g, prepared, g.State, other)));
            // ... and a frozen record that claims another GameCalling under the SAME identity is not believed either
            var real = prepared.Catcher!;
            var forged = prepared.WithCatcher(new FrozenCatcherInput(real.GameId, real.InputSetId, real.Catcher, real.ReceivingFraming, 99));
            Assert.Equal("PitchCandidate.game_calling_changed", Code(Rebuild(g, forged, g.State, w.Set)));
            // a catcher input of another game / another set
            Assert.Equal("PitchCandidate.catcher_game_mismatch", Code(Rebuild(g, prepared.WithCatcher(new FrozenCatcherInput("game:other", real.InputSetId, real.Catcher, real.ReceivingFraming, real.GameCalling)), g.State, w.Set)));
            Assert.Equal("PitchCandidate.player_inputs_changed", Code(Rebuild(g, prepared.WithCatcher(new FrozenCatcherInput(real.GameId, new string('e', 64), real.Catcher, real.ReceivingFraming, real.GameCalling)), g.State, w.Set)));
            // a missing catcher input where the model needs one, and an unneeded one
            Assert.Equal("PitchCandidate.catcher_input_mismatch", Code(Rebuild(g, prepared.WithCatcher(null), g.State, w.Set)));
        }
    }

    [Fact]
    public void A_catcher_change_that_keeps_the_same_game_calling_still_changes_the_catcher_and_is_found()
    {
        var w = Gc.Game(Automated, calling: 50, approach: false);          // everybody has 50: only the PlayerId differs after the swap
        var g = Aggregate(w.Driver, 19);
        var prepared = Gc.Prepare(g, w.Set, 50, take: true);
        var swapped = SwapCatcherWithFirstBase(g.State);
        Assert.Equal("PitchCandidate.catcher_changed", Code(Rebuild(g, prepared, swapped, w.Set)));
    }

    [Fact]
    public void Every_refusal_leaves_the_aggregate_the_revision_the_rng_and_the_last_commit_untouched()
    {
        var w = Gc.Game(NonAutomated, approach: false);
        var g = Aggregate(w.Driver, 20);
        var cursor = new GameCursor(g);
        var before = Gc.Basis.Of(cursor);
        var prepared = Gc.Prepare(g, w.Set, 50, take: true);
        var real = prepared.PitcherInput!;

        foreach (var attempt in new Func<Result<NonInPlayPitchCandidate>>[]
        {
            () => Rebuild(g, prepared, g.State, w.Set, 80),
            () => Rebuild(g, prepared, g.State, null),
            () => Rebuild(g, prepared.WithPitcher(new FrozenPitcherInput(real.GameId, real.InputSetId, real.Pitcher, 80)), g.State, w.Set),
            () => Rebuild(g, prepared.WithPitcher(null), g.State, w.Set),
            () => Rebuild(g, prepared, SwapCatcherWithFirstBase(g.State), w.Set),
        })
            Assert.False(attempt().IsSuccess);

        Gc.AssertUnchanged(before, cursor);
    }
}
