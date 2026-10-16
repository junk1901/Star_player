using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E (Game Calling v4, GC-B): the pitch resolver under <c>prob-model-v4</c>. Game Calling reaches <c>actual_location</c> only, scoped by the AIMED zone; the pitcher's Control is the pinned one; no
/// stage, stream or draw is added; and with a neutral Game Calling rating a v4 pitch is the v3 pitch (same draws, same outcomes, same position) with a different version label.
/// </summary>
public class GameCallingResolverTests
{
    private static string[] Modes => ["automated", "non_automated"];

    private static (AuthoritativeGame G, Gc.World W, PitchBranchRequest R) Request(string mode = "non_automated", int control = 60, int calling = 40)
    {
        var w = Gc.Game(RulesFor(mode), control, calling);
        var g = Aggregate(w.Driver, 21);
        var (b, p) = Gc.SheetsOf(g.State, control);
        var fielding = PitchFieldingInputs.Build(g.State, w.Set, "t").Value;
        var catcher = FrozenCatcherInput.Build(g.State, w.Set, Gc.T4.Model, "t").Value;
        var pitcher = FrozenPitcherInput.Build(g.State, w.Set, "t").Value;
        return (g, w, new PitchBranchRequest(g.State, Gc.T4, PitchIntent.Balanced, false, BattingSide.Right, ThrowingHand.Right, g.Rng, null, PitchAbilities.From(b, p).Value, fielding, catcher, pitcher));
    }

    private static string Code(PitchBranchRequest request) => Pc.Code(PitchBranchResolver.Resolve(request));

    // ---------------------------------------------------------------- the resolver's own refusals, before the first draw

    [Fact]
    public void A_v4_request_needs_the_pinned_pitcher_input_the_abilities_and_the_catcher_input()
    {
        foreach (var mode in Modes)
        {
            var (g, _, ok) = Request(mode);
            Assert.True(PitchBranchResolver.Resolve(ok).IsSuccess);

            Assert.Equal("PitchBranch.pitcher_input_missing", Code(ok with { PitcherInput = null }));
            Assert.Equal("PitchBranch.abilities_missing", Code(ok with { Abilities = null }));                       // pitcher.control has one connection: the abilities
            Assert.Equal("PitchBranch.catcher_missing", Code(ok with { Catcher = null }));
            Assert.Equal("PitchBranch.player_inputs_missing", Code(ok with { Fielding = null }));
            Assert.Equal(g.Rng.StateHash, ok.Rng!.StateHash);                                                           // the caller's snapshot is only read
        }
    }

    [Fact]
    public void The_pitcher_input_must_belong_to_this_game_this_set_the_pitcher_on_the_mound_and_agree_with_the_abilities()
    {
        foreach (var mode in Modes)
        {
            var (_, _, ok) = Request(mode);
            var f = ok.PitcherInput!;
            var other = BasePlayers.First(id => id != f.Pitcher);

            Assert.Equal("PitchBranch.pitcher_game_mismatch", Code(ok with { PitcherInput = new FrozenPitcherInput("game:other", f.InputSetId, f.Pitcher, f.Control) }));
            Assert.Equal("PitchBranch.pitcher_inputs_mismatch", Code(ok with { PitcherInput = new FrozenPitcherInput(f.GameId, new string('9', 64), f.Pitcher, f.Control) }));
            Assert.Equal("PitchBranch.pitcher_input_mismatch", Code(ok with { PitcherInput = new FrozenPitcherInput(f.GameId, f.InputSetId, other, f.Control) }));
            Assert.Equal("PitchBranch.ability_out_of_range", Code(ok with { PitcherInput = new FrozenPitcherInput(f.GameId, f.InputSetId, f.Pitcher, 101) }));
            Assert.Equal("PitchBranch.ability_out_of_range", Code(ok with { PitcherInput = new FrozenPitcherInput(f.GameId, f.InputSetId, f.Pitcher, -1) }));
            // the frozen Control and the Control the abilities feed to the stage must be one number
            Assert.Equal("PitchBranch.pitcher_control_mismatch", Code(ok with { PitcherInput = new FrozenPitcherInput(f.GameId, f.InputSetId, f.Pitcher, f.Control + 1) }));
            Assert.Equal("PitchBranch.pitcher_control_mismatch", Code(ok with { Abilities = ok.Abilities! with { PitcherControl = f.Control - 1 } }));
        }
    }

    [Fact]
    public void The_catcher_input_must_carry_exactly_the_ratings_the_model_and_rules_need()
    {
        var (_, _, automated) = Request("automated");
        var c = automated.Catcher!;
        Assert.Null(c.ReceivingFraming);
        // automated: a Framing rating nobody needs is refused (nothing is silently ignored), and Game Calling is required
        Assert.Equal("PitchBranch.catcher_framing_mismatch", Code(automated with { Catcher = new FrozenCatcherInput(c.GameId, c.InputSetId, c.Catcher, 50, c.GameCalling) }));
        Assert.Equal("PitchBranch.catcher_game_calling_mismatch", Code(automated with { Catcher = new FrozenCatcherInput(c.GameId, c.InputSetId, c.Catcher, null, null) }));

        var (_, _, manual) = Request("non_automated");
        var m = manual.Catcher!;
        Assert.Equal("PitchBranch.catcher_framing_mismatch", Code(manual with { Catcher = new FrozenCatcherInput(m.GameId, m.InputSetId, m.Catcher, null, m.GameCalling) }));
        Assert.Equal("PitchBranch.catcher_game_calling_mismatch", Code(manual with { Catcher = new FrozenCatcherInput(m.GameId, m.InputSetId, m.Catcher, m.ReceivingFraming, null) }));
        Assert.Equal("PitchBranch.ability_out_of_range", Code(manual with { Catcher = new FrozenCatcherInput(m.GameId, m.InputSetId, m.Catcher, m.ReceivingFraming, 101) }));
        Assert.Equal("PitchBranch.ability_out_of_range", Code(manual with { Catcher = new FrozenCatcherInput(m.GameId, m.InputSetId, m.Catcher, -1, m.GameCalling) }));
        Assert.Equal("PitchBranch.catcher_game_mismatch", Code(manual with { Catcher = new FrozenCatcherInput("game:other", m.InputSetId, m.Catcher, m.ReceivingFraming, m.GameCalling) }));
        Assert.Equal("PitchBranch.catcher_inputs_mismatch", Code(manual with { Catcher = new FrozenCatcherInput(m.GameId, new string('8', 64), m.Catcher, m.ReceivingFraming, m.GameCalling) }));
        Assert.Equal("PitchBranch.catcher_mismatch", Code(manual with { Catcher = new FrozenCatcherInput(m.GameId, m.InputSetId, BasePlayers.First(id => id != m.Catcher), m.ReceivingFraming, m.GameCalling) }));
    }

    [Fact]
    public void Model_v3_still_refuses_a_pitcher_input_and_a_game_calling_rating_it_does_not_use()
    {
        var (d, set) = Fr.NonAutomatedGame(60);
        var g = Aggregate(d, 21);
        var (b, p) = Sheets(g.State);
        var fielding = PitchFieldingInputs.Build(g.State, set, "t").Value;
        var catcher = FrozenCatcherInput.Build(g.State, set, Fr.T3.Model, "t").Value;
        var ok = new PitchBranchRequest(g.State, Fr.T3, PitchIntent.Balanced, false, BattingSide.Right, ThrowingHand.Right, g.Rng, null, PitchAbilities.From(b, p).Value, fielding, catcher);

        Assert.True(PitchBranchResolver.Resolve(ok).IsSuccess);
        Assert.Null(catcher.GameCalling);
        Assert.Equal("PitchBranch.pitcher_input_unexpected", Code(ok with { PitcherInput = new FrozenPitcherInput(Gid, set.Id, g.State.Fielding.Pitcher, 50) }));
        Assert.Equal("PitchBranch.catcher_game_calling_mismatch", Code(ok with { Catcher = new FrozenCatcherInput(catcher.GameId, catcher.InputSetId, catcher.Catcher, catcher.ReceivingFraming, 50) }));
    }

    [Fact]
    public void A_v4_game_refuses_a_request_made_with_another_tuning_and_a_v3_game_refuses_the_v4_tuning()
    {
        var (_, _, ok) = Request();
        Assert.Equal("PitchBranch.execution_mismatch", Code(ok with { Tuning = Gx.Tuning3 }));
        var (d, set) = Fr.NonAutomatedGame(60);
        var g3 = Aggregate(d, 21);
        var (b, p) = Sheets(g3.State);
        var v3 = new PitchBranchRequest(g3.State, Fr.T3, PitchIntent.Balanced, false, BattingSide.Right, ThrowingHand.Right, g3.Rng, null, PitchAbilities.From(b, p).Value,
            PitchFieldingInputs.Build(g3.State, set, "t").Value, FrozenCatcherInput.Build(g3.State, set, Fr.T3.Model, "t").Value);
        Assert.Equal("PitchBranch.execution_mismatch", Code(v3 with { Tuning = Gx.Tuning4 }));
    }

    // ---------------------------------------------------------------- no new stage, stream or draw

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Game_calling_adds_no_draw_the_target_and_location_draws_are_identical_for_every_rating(int calling)
    {
        foreach (var mode in Modes)
        {
            var baseline = Gc.Game(RulesFor(mode), calling: 50);
            var rated = Gc.Game(RulesFor(mode), calling: calling);
            for (ulong seed = 1; seed <= 80; seed++)
            {
                var a = Gc.Prepare(Aggregate(baseline.Driver, seed), baseline.Set).Draft;
                var b = Gc.Prepare(Aggregate(rated.Driver, seed), rated.Set).Draft;
                foreach (var stage in new[] { StageIds.TargetZone, StageIds.ActualLocation })
                {
                    var (x, y) = (a.Steps.Single(s => s.Stage == stage), b.Steps.Single(s => s.Stage == stage));
                    Assert.Equal((x.Stream, x.Roll, x.RawDraws), (y.Stream, y.Roll, y.RawDraws));                      // the same generator output, whatever the rating
                }

                Assert.Equal(StageIds.TargetZone, b.Steps[0].Stage);                                                  // the order and the streams of the stages are the existing ones
                Assert.Equal(StageIds.ActualLocation, b.Steps[1].Stage);
                Assert.Equal((GameRngStreams.Target, GameRngStreams.Location), (b.Steps[0].Stream, b.Steps[1].Stream));
                Assert.Equal("game/location@1", b.Steps[1].Stream.Canonical);
                Assert.All(b.StreamUsage, u => Assert.Contains(u.Stream.Canonical, new[] { "game/target@1", "game/location@1", "game/pitch@1", "game/contact@1", "game/field@1" }));
                Assert.Equal(1, b.StreamUsage.Single(u => u.Stream == GameRngStreams.Location).Calls);                // one location decision per pitch, with or without Game Calling
                Assert.Equal(1, b.StreamUsage.Single(u => u.Stream == GameRngStreams.Target).Calls);
            }
        }
    }

    // ---------------------------------------------------------------- the scope is the AIMED zone

    [Fact]
    public void Game_calling_is_applied_exactly_when_the_aimed_zone_is_borderline_whatever_the_landing_zone_and_the_calling_mode()
    {
        var seen = new HashSet<(BoundaryClass Aimed, BoundaryClass Landed)>();
        foreach (var mode in Modes)
        {
            var w = Gc.Game(RulesFor(mode), control: 70, calling: 90);
            for (ulong seed = 1; seed <= 400; seed++)
            {
                var draft = Gc.Prepare(Aggregate(w.Driver, seed), w.Set, 70).Draft;
                var calling = Gc.Location(draft).Trace.Modifiers.Single(m => m.Slot == "catcher.game_calling");
                var aimed = Gc.AimedClass(draft);
                Assert.Equal(aimed == BoundaryClass.Borderline, calling.Applied);                                    // the aimed class, never the landed one
                Assert.Equal(calling.Applied ? 1_040_000 : 1_000_000, calling.FactorPpm);
                Assert.Equal(Gc.Location(draft).Condition, draft.Target.Zone.Value);
                seen.Add((aimed, draft.Location.Boundary));
            }
        }

        // all four combinations really occur, so the rule above is not satisfied by accident
        Assert.Equal(4, seen.Count);
    }

    [Fact]
    public void Control_and_game_calling_combine_exactly_for_the_aimed_outcome_and_touch_no_other_outcome()
    {
        foreach (var (control, calling, expected) in new[] { (0, 0, 878_750), (0, 100, 971_250), (100, 0, 1_021_250), (100, 100, 1_128_750), (50, 50, 1_000_000) })
        {
            var w = Gc.Game(NonAutomated, control, calling);
            var checkedPitches = 0;
            for (ulong seed = 1; seed <= 120 && checkedPitches < 25; seed++)
            {
                var draft = Gc.Prepare(Aggregate(w.Driver, seed), w.Set, control).Draft;
                if (Gc.AimedClass(draft) != BoundaryClass.Borderline)
                    continue;
                var location = Gc.Location(draft);
                var aimedIndex = location.Trace.Outcomes.ToList().IndexOf(draft.Target.Zone.Value);
                Assert.Equal(expected, location.Trace.CombinedFactorPpm[aimedIndex]);
                Assert.All(location.Trace.CombinedFactorPpm.Where((_, i) => i != aimedIndex), f => Assert.Equal(1_000_000, f));
                Assert.True(location.Trace.Modifiers.Single(m => m.Slot == "catcher.game_calling").Applied);
                checkedPitches++;
            }

            Assert.True(checkedPitches > 0);
        }
    }

    [Fact]
    public void Each_ability_is_connected_once_framing_never_reaches_the_location_game_calling_never_reaches_the_call_and_chemistry_never_reaches_anything()
    {
        foreach (var mode in Modes)
        {
            var w = Gc.Game(RulesFor(mode), control: 65, calling: 70, framing: 35);
            for (ulong seed = 1; seed <= 250; seed++)
            {
                var draft = Gc.Prepare(Aggregate(w.Driver, seed), w.Set, 65).Draft;
                foreach (var step in draft.Steps)
                {
                    var slots = step.Trace.Modifiers.Select(m => m.Slot).ToList();
                    Assert.Equal(slots.Count, slots.Distinct().Count());                                               // no slot twice in one stage
                    Assert.DoesNotContain("battery.chemistry", slots);
                    switch (step.Stage)
                    {
                        case StageIds.ActualLocation:
                            Assert.Equal(["pitcher.control", "catcher.game_calling"], slots);
                            break;
                        case StageIds.CalledPitch:
                            Assert.Equal(mode == "non_automated" ? ["catcher.framing"] : [], slots);                    // automated: Framing is not even listed
                            break;
                        default:
                            Assert.DoesNotContain("catcher.game_calling", slots);
                            Assert.DoesNotContain("catcher.framing", slots);
                            Assert.DoesNotContain("pitcher.control", slots);
                            break;
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------- v3 and v4: a neutral Game Calling rating changes nothing

    private static PitchBranchDraft PrepareV3(string mode, ulong seed, int control, int framing)
    {
        var (d, set) = mode == "automated" ? Fr.AutomatedGame() : Fr.NonAutomatedGame(framing);
        var g = Aggregate(d, seed);
        var (b, p) = Gc.SheetsOf(g.State, control);
        return Ok(PitchPreparation.Prepare(g.State, g.Rng, Fr.T3, PitchIntent.Balanced, false, b, p, default, null, set)).Draft;
    }

    private static string Shape(PitchBranchStep s) =>
        $"{s.Stage}|{s.Condition}|{s.Outcome}|{s.Stream.Canonical}|{s.Roll}|{s.RawDraws}|{string.Join(",", s.Trace.Final)}|{string.Join(",", s.Trace.CombinedFactorPpm)}";

    [Fact]
    public void A_neutral_game_calling_pitch_equals_the_v3_pitch_in_every_draw_probability_outcome_and_position_for_every_control()
    {
        foreach (var mode in Modes)
            foreach (var control in new[] { 30, 50, 90 })
            {
                var w = Gc.Game(RulesFor(mode), control: control, calling: 50, framing: 61);
                for (ulong seed = 1; seed <= 150; seed++)
                {
                    var v4 = Gc.Prepare(Aggregate(w.Driver, seed), w.Set, control).Draft;
                    var v3 = PrepareV3(mode, seed, control, 61);

                    Assert.Equal(v3.Steps.Select(Shape), v4.Steps.Select(Shape));
                    Assert.Equal((v3.Kind, v3.Target, v3.Location, v3.CalledPitch, v3.SwingResult, v3.BattedBall, v3.DefenseResult), (v4.Kind, v4.Target, v4.Location, v4.CalledPitch, v4.SwingResult, v4.BattedBall, v4.DefenseResult));
                    Assert.Equal(v3.NextRng.StateHash, v4.NextRng.StateHash);                                          // the same position H1: the same draws
                    Assert.Equal(v3.NextBoundedCalls, v4.NextBoundedCalls);
                    Assert.True(v3.StreamUsage.SequenceEqual(v4.StreamUsage));
                    // what legitimately differs: the version labels and the extra (neutral) modifier entry
                    Assert.NotEqual(v3.ProbabilityModelVersion, v4.ProbabilityModelVersion);
                    Assert.NotEqual(v3.TuningHash, v4.TuningHash);
                    Assert.Single(Gc.Location(v3).Trace.Modifiers);
                    Assert.Equal(2, Gc.Location(v4).Trace.Modifiers.Count);
                    Assert.Equal(1_000_000, Gc.Location(v4).Trace.Modifiers[1].FactorPpm);
                }
            }
    }

    [Fact]
    public void A_non_neutral_game_calling_rating_changes_only_borderline_aimed_distributions_never_the_draws()
    {
        var changed = 0;
        foreach (var mode in Modes)
        {
            var neutral = Gc.Game(RulesFor(mode), calling: 50);
            var strong = Gc.Game(RulesFor(mode), calling: 100);
            for (ulong seed = 1; seed <= 150; seed++)
            {
                var a = Gc.Prepare(Aggregate(neutral.Driver, seed), neutral.Set).Draft;
                var b = Gc.Prepare(Aggregate(strong.Driver, seed), strong.Set).Draft;
                Assert.Equal(Gc.Target(a).Outcome, Gc.Target(b).Outcome);                                                // the target stage has nothing to do with Game Calling
                if (Gc.AimedClass(a) == BoundaryClass.Clear)
                {
                    Assert.True(a.Steps.Select(Shape).SequenceEqual(b.Steps.Select(Shape)));                              // a Clear aim: the whole pitch is the neutral pitch
                    Assert.Equal(a.NextRng.StateHash, b.NextRng.StateHash);
                }
                else if (!Gc.Location(a).Trace.Final.SequenceEqual(Gc.Location(b).Trace.Final))
                    changed++;
            }
        }

        Assert.True(changed > 0);
    }
}
