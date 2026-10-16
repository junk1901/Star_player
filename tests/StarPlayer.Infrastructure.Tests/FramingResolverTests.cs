using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E Package B, the RESOLVER side: the frozen catcher reaches the called-pitch decision, the model's scope (not the resolver) decides where it acts, no draw is added, and a
/// neutral rating reproduces the unchanged prob-model-v2 game pitch for pitch. The expected called-pitch distributions come from the independent Python oracle
/// (<c>generate_phase3e_framing_oracle.py</c>); the expected outcome of a roll is chosen by a threshold scan written in the test (<c>Gx.Pick</c>), never by the production selector.
/// </summary>
public class FramingResolverTests
{
    private const int Seeds = 400;

    internal static JsonElement OracleCaseOf(string mode, string zone, int rating) =>
        TuningV3Tests.OracleDoc.Value.GetProperty("cases").EnumerateArray()
            .Single(c => c.GetProperty("mode").GetString() == mode && c.GetProperty("zone").GetString() == zone && c.GetProperty("framing").GetInt32() == rating);

    private static JsonElement OracleCase(string mode, string zone, int rating) => OracleCaseOf(mode, zone, rating);

    private static int[] Ppm(JsonElement c) => c.GetProperty("ppm").EnumerateArray().Select(x => x.GetInt32()).ToArray();

    private static PitchBranchDraft Draft(int rating, ulong seed, bool take, GameRuleSnapshot? rules = null)
    {
        var (d, set) = rules == Automated ? Fr.AutomatedGame(Fr.SetV2(_ => rating), approach: !take) : Fr.NonAutomatedGame(rating, approach: !take);
        return Fr.Prepare(Aggregate(d, seed), set, take: take).Draft;
    }

    // ---------------------------------------------------------------- the oracle, the scope and the rolls

    [Fact]
    public void Every_called_pitch_of_a_seed_scan_equals_the_oracle_row_for_its_zone_and_rating_and_the_roll_picks_the_oracles_call()
    {
        var applied = 0;
        var notApplied = 0;
        foreach (var rating in new[] { 0, 50, 100 })
            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                var draft = Draft(rating, seed, take: true);
                var step = Fr.Called(draft);
                if (step is null)
                {
                    Assert.Equal(PitchBranchKind.HitByPitch, draft.Kind);                   // a take that is not a hit by pitch is always called
                    continue;
                }

                var zone = draft.Location.Zone.Value;
                var oracle = OracleCase("non_automated", zone, rating);
                Assert.Equal($"non_automated:{zone}", step.Condition);
                Assert.True(Ppm(oracle).SequenceEqual(step.Trace.Final), $"{zone} framing={rating} seed={seed}");
                var entry = Assert.Single(step.Trace.Modifiers);
                Assert.Equal(("catcher.framing", oracle.GetProperty("applied").GetBoolean(), oracle.GetProperty("factor").GetInt32()), (entry.Slot, entry.Applied, entry.FactorPpm));
                Assert.Equal(Gx.Pick(["ball", "strike"], Ppm(oracle), step.Roll), step.Outcome);          // the roll chooses the call by thresholds written here
                Assert.Equal(step.Outcome == "ball" ? PitchBranchKind.CalledBall : PitchBranchKind.CalledStrike, draft.Kind);
                if (entry.Applied) applied++; else notApplied++;
            }

        Assert.True(applied > 150 && notApplied > 20, $"the scan must cover both borderline and clear pitches (applied {applied}, not applied {notApplied})");
    }

    [Fact]
    public void The_same_seed_with_three_ratings_differs_only_in_the_called_pitch_row_and_never_in_the_draws()
    {
        var callsChanged = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var d0 = Draft(0, seed, take: true);
            var d50 = Draft(50, seed, take: true);
            var d100 = Draft(100, seed, take: true);

            foreach (var d in new[] { d0, d100 })
            {
                Assert.Equal(d50.Steps.Length, d.Steps.Length);                                // the SAME decisions, in the SAME order, are drawn: no draw is added or removed
                for (var i = 0; i < d50.Steps.Length; i++)
                {
                    var (a, b) = (d50.Steps[i], d.Steps[i]);
                    Assert.Equal((a.Stage, a.Condition, a.Stream, a.Roll, a.RawDraws), (b.Stage, b.Condition, b.Stream, b.Roll, b.RawDraws));   // the roll of every decision is the same ...
                    if (a.Stage != StageIds.CalledPitch)
                    {
                        Assert.Equal(a.Outcome, b.Outcome);
                        Assert.Equal(a.Trace.Final, b.Trace.Final);                                                                              // ... and only the called-pitch row may differ
                    }
                }

                Assert.Equal(d50.NextBoundedCalls, d.NextBoundedCalls);
                Assert.Equal(d50.StreamUsage.Select(u => (u.Stream.Canonical, u.Calls, u.RawDraws)), d.StreamUsage.Select(u => (u.Stream.Canonical, u.Calls, u.RawDraws)));
                Assert.Equal(d50.NextRng.Canonical(), d.NextRng.Canonical());                  // H1 is the same position whatever the framing
                Assert.Equal(d50.Location, d.Location);
            }

            if (d0.Kind != d100.Kind)
                callsChanged++;
        }

        Assert.True(callsChanged > 0, "no seed in the scan flips a call between framing 0 and 100");
    }

    [Fact]
    public void One_and_the_same_roll_gives_a_ball_for_a_poor_framer_and_a_strike_for_a_great_one_on_a_borderline_pitch_as_the_oracle_predicts()
    {
        var found = 0;
        for (ulong seed = 1; seed <= 4000 && found < 5; seed++)
        {
            var poor = Draft(0, seed, take: true);
            var great = Draft(100, seed, take: true);
            if (Fr.Called(poor) is not { } sp || Fr.Called(great) is not { } sg || poor.Kind != PitchBranchKind.CalledBall || great.Kind != PitchBranchKind.CalledStrike)
                continue;

            var zone = poor.Location.Zone.Value;
            Assert.Equal(sp.Roll, sg.Roll);                                                  // the very same roll
            Assert.Equal(BoundaryClass.Borderline, PitchZoneModel.V1.Classify(poor.Location.Zone).Value);
            // the oracle's two rows and the written-here threshold scan explain the difference on their own
            Assert.Equal("ball", Gx.Pick(["ball", "strike"], Ppm(OracleCase("non_automated", zone, 0)), sp.Roll));
            Assert.Equal("strike", Gx.Pick(["ball", "strike"], Ppm(OracleCase("non_automated", zone, 100)), sg.Roll));
            Assert.True(Ppm(OracleCase("non_automated", zone, 0))[0] > sp.Roll && Ppm(OracleCase("non_automated", zone, 100))[0] <= sg.Roll);
            found++;
        }

        Assert.Equal(5, found);
    }

    [Fact]
    public void A_clear_zone_never_changes_with_the_rating_and_lists_the_modifier_as_not_applied()
    {
        var checkedClear = 0;
        for (ulong seed = 1; seed <= 2000 && checkedClear < 12; seed++)
        {
            var d0 = Draft(0, seed, take: true);
            if (Fr.Called(d0) is not { } s0 || d0.Location.Boundary != BoundaryClass.Clear)
                continue;
            var d100 = Draft(100, seed, take: true);
            var s100 = Fr.Called(d100)!;
            Assert.Equal(Summary(d0), Summary(d100));                                        // identical in every recorded fact
            Assert.Equal(s0.Trace.Final, s100.Trace.Final);
            Assert.False(Assert.Single(s0.Trace.Modifiers).Applied);
            Assert.Equal(1_000_000, Assert.Single(s100.Trace.Modifiers).FactorPpm);
            Assert.Equal(V3Baseline($"non_automated:{d0.Location.Zone}"), s0.Trace.Final);
            checkedClear++;
        }

        Assert.Equal(12, checkedClear);
    }

    private static IReadOnlyList<int> V3Baseline(string key) => Fr.T3.Baseline(StageIds.CalledPitch, key).Value.Ppm;

    [Fact]
    public void Automated_rules_ignore_the_catchers_rating_completely_and_the_pitch_is_identical_for_any_set()
    {
        var calls = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var low = Draft(0, seed, take: true, Automated);
            var high = Draft(100, seed, take: true, Automated);
            Assert.Equal(Summary(low), Summary(high));
            Assert.DoesNotContain(low.Steps, s => s.Trace.Modifiers.Any(m => m.Slot == "catcher.framing"));
            if (Fr.Called(low) is { } step)
            {
                calls++;
                Assert.StartsWith("automated:", step.Condition, StringComparison.Ordinal);
                Assert.Equal(low.Location.Region(), step.Outcome == "strike" ? "inner" : "outer");   // the deterministic 0% / 100% rows
            }
        }

        Assert.True(calls > 300);
    }

    // ---------------------------------------------------------------- neutral = unchanged v2, pitch for pitch

    [Fact]
    public void A_neutral_rating_reproduces_the_model_v2_game_pitch_for_pitch_every_decision_roll_draw_and_rng_position()
    {
        var kinds = new HashSet<PitchBranchKind>();
        var v2Set = Gx.Set();
        var v2Exec = Gx.Exec(inputsId: v2Set.Id, tuning: Gx.Tuning2);

        foreach (var approach in new[] { true, false })
            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                var (d3, set3) = Fr.NonAutomatedGame(50, approach);
                var d2 = approach ? Gx.InSequence(v2Exec) : new Driver(StartState(NonAutomated, Gid, v2Exec), Gid);
                var p3 = Fr.Prepare(Aggregate(d3, seed), set3, take: !approach).Draft;
                var p2 = Gx.Prepare(Aggregate(d2, seed), Gx.Tuning2, take: !approach, inputs: v2Set).Draft;

                Assert.Equal(p2.Steps.Length, p3.Steps.Length);
                for (var i = 0; i < p2.Steps.Length; i++)
                {
                    var (a, b) = (p2.Steps[i], p3.Steps[i]);
                    Assert.Equal((a.Stage, a.Condition, a.Outcome, a.Stream, a.Roll, a.RawDraws), (b.Stage, b.Condition, b.Outcome, b.Stream, b.Roll, b.RawDraws));
                    Assert.Equal(a.Trace.Final, b.Trace.Final);
                    Assert.Equal(a.Trace.CombinedFactorPpm, b.Trace.CombinedFactorPpm);
                }

                Assert.Equal((p2.Kind, p2.Location, p2.NextBoundedCalls, p2.NextRng.Canonical()), (p3.Kind, p3.Location, p3.NextBoundedCalls, p3.NextRng.Canonical()));
                Assert.Equal(p2.StreamUsage.Select(u => (u.Stream.Canonical, u.Calls, u.RawDraws)), p3.StreamUsage.Select(u => (u.Stream.Canonical, u.Calls, u.RawDraws)));
                kinds.Add(p3.Kind);
            }

        // hit by pitch, called ball and strike, swinging strike, foul and in play were all compared (take, hit-by-pitch and swing paths included)
        Assert.Equal(Enum.GetValues<PitchBranchKind>().Length, kinds.Count);
    }

    [Fact]
    public void The_framing_input_is_attached_to_the_called_pitch_only_so_no_other_stage_sees_it_and_nothing_is_applied_twice()
    {
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var draft = Draft(90, seed, take: false);
            foreach (var step in draft.Steps)
            {
                var framing = step.Trace.Modifiers.Where(m => m.Slot == "catcher.framing").ToList();
                Assert.Equal(step.Stage == StageIds.CalledPitch ? 1 : 0, framing.Count);
                Assert.DoesNotContain(step.Trace.Modifiers, m => m.Slot is "catcher.game_calling" or "battery.chemistry");
            }

            Assert.True(draft.Steps.Count(s => s.Stage == StageIds.CalledPitch) <= 1);
        }
    }

    // ---------------------------------------------------------------- the resolver's own refusals, before the first draw

    private static (AuthoritativeGame G, PlayerInputSet Set, PitchBranchRequest Request) Request(bool automated = false, TuningProfile? tuning = null)
    {
        var (d, set) = automated ? Fr.AutomatedGame() : Fr.NonAutomatedGame(60);
        var g = Aggregate(d, 21);
        var (b, p) = Sheets(g.State);
        var fielding = PitchFieldingInputs.Build(g.State, set, "t").Value;
        var catcher = automated ? null : FrozenCatcherInput.Build(g.State, set, Fr.T3.Model, "t").Value;
        return (g, set, new PitchBranchRequest(g.State, tuning ?? Fr.T3, PitchIntent.Balanced, false, BattingSide.Right, ThrowingHand.Right, g.Rng, null, PitchAbilities.From(b, p).Value, fielding, catcher));
    }

    [Fact]
    public void The_resolver_refuses_a_missing_or_an_unneeded_catcher_input()
    {
        var (_, _, ok) = Request();
        Assert.True(PitchBranchResolver.Resolve(ok).IsSuccess);
        Assert.Equal("PitchBranch.catcher_missing", Code(PitchBranchResolver.Resolve(ok with { Catcher = null })));

        var (_, _, automated) = Request(automated: true);
        Assert.True(PitchBranchResolver.Resolve(automated).IsSuccess);
        var stray = new FrozenCatcherInput(Gid, automated.Fielding!.InputSetId, Fr.CatcherOf(automated.State!), 50);
        Assert.Equal("PitchBranch.catcher_unexpected", Code(PitchBranchResolver.Resolve(automated with { Catcher = stray })));

        // under model v2 (reserved Framing) a catcher input is just as unwanted
        var v2Set = Gx.Set();
        var v2Game = Aggregate(Gx.InSequence(Gx.Exec(inputsId: v2Set.Id, tuning: Gx.Tuning2)), 21);
        var (b, p) = Sheets(v2Game.State);
        var v2Request = new PitchBranchRequest(v2Game.State, Gx.Tuning2, PitchIntent.Balanced, false, BattingSide.Right, ThrowingHand.Right, v2Game.Rng, null, PitchAbilities.From(b, p).Value,
            PitchFieldingInputs.Build(v2Game.State, v2Set, "t").Value);
        Assert.True(PitchBranchResolver.Resolve(v2Request).IsSuccess);
        Assert.Equal("PitchBranch.catcher_unexpected", Code(PitchBranchResolver.Resolve(v2Request with { Catcher = new FrozenCatcherInput(Gid, v2Set.Id, Fr.CatcherOf(v2Game.State), 50) })));
    }

    [Fact]
    public void The_resolver_refuses_a_catcher_input_that_is_not_this_games_current_catchers_from_the_pinned_set()
    {
        var (_, _, ok) = Request();
        var c = ok.Catcher!;
        var other = BasePlayers.First(p => p != c.Catcher);

        Assert.Equal("PitchBranch.catcher_mismatch", Code(PitchBranchResolver.Resolve(ok with { Catcher = new FrozenCatcherInput(c.GameId, c.InputSetId, other, c.ReceivingFraming) })));
        Assert.Equal("PitchBranch.catcher_game_mismatch", Code(PitchBranchResolver.Resolve(ok with { Catcher = new FrozenCatcherInput("game:other", c.InputSetId, c.Catcher, c.ReceivingFraming) })));
        Assert.Equal("PitchBranch.catcher_inputs_mismatch", Code(PitchBranchResolver.Resolve(ok with { Catcher = new FrozenCatcherInput(c.GameId, new string('b', 64), c.Catcher, c.ReceivingFraming) })));
        foreach (var rating in new[] { -1, 101, int.MaxValue })
            Assert.Equal("PitchBranch.ability_out_of_range", Code(PitchBranchResolver.Resolve(ok with { Catcher = new FrozenCatcherInput(c.GameId, c.InputSetId, c.Catcher, rating) })));
    }

    private static string Summary(PitchBranchDraft d) => PitchBranchFixture.Summary(d);
}

internal static class LocationExtensions
{
    /// <summary>"inner" for a strike-zone cell, "outer" for the eight regions around it.</summary>
    public static string Region(this PitchLocation location) =>
        PitchZoneModel.V1.Info(location.Zone).Value.Region == PitchZoneRegion.Inner ? "inner" : "outer";
}
