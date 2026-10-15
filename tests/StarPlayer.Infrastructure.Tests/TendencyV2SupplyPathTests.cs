using System.Reflection;
using System.Text.Json.Nodes;
using StarPlayer.Application.Ports;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.TendencyPackFixture;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-D (BatterTendency v2, D-T3 / D-T5 / D-T6): the REAL supply path. A hidden tendency written in a DataPack v2 file is read into the world, looked up by the CURRENT batter's PlayerId in
/// <see cref="PitchPreparation.PrepareFromProfiles"/>, handed to the unchanged resolver and shows up in the swing_result / batted_ball distributions; then a real pitch is staged and published through
/// the commit gate. The game fixture is the 3-C3-B one (batters a1..a9 / pitcher hp of <see cref="Pc"/>); the world adds sheets with those ids (clones of fixture hitters/pitchers, so two of them
/// share a display name with other players) and hand-written vectors. Expected shifts are plain arithmetic from the ADR-006 contract (value x cap / 100); nothing is computed with the model.
/// <para>
/// WHAT THIS DOES NOT PROVE: there is no production game runner yet, so "a runner loads the pack once and passes the same set pitch after pitch" is shown by the shape of the API (no source,
/// path or clock reaches Simulation) and by an immutable set reused across pitches, not by a running game loop.
/// </para>
/// </summary>
public class TendencyV2SupplyPathTests
{
    private const int SwingCap = 60_000;
    private const int BattedCap = 40_000;

    private static PlayerId P(string id) => PlayerId.Create(id).Value;

    // ---------------------------------------------------------------- the world

    private static void AddGamePlayers(JsonNode root)
    {
        AddPlayerLike(root, "p-0001", "a1");   // left-handed hitter named like p-0005
        AddPlayerLike(root, "p-0002", "a2");
        AddPlayerLike(root, "p-0005", "a3");   // same display name as a1 and ab1
        AddPlayerLike(root, "p-0001", "ab1");  // pinch hitter: same name AND same ratings as a1, another player
        AddPlayerLike(root, "p-0003", "hp");   // the fielding pitcher of the fixture game
    }

    private static readonly JsonObject[] FullRows =
    [
        Row("a1", CenterPlus), Row("a2", CenterMinus), Row("ab1", Pull), Row("p-0001", Away), // a3 deliberately has no row
    ];

    private static PlayerWorld ReadWorld(string json)
    {
        var read = WorldJson.Read(json);
        Assert.True(read.IsSuccess, read.Error?.Code + " " + read.Error?.Message);
        return read.Value;
    }

    private static PlayerWorld V2World(IEnumerable<JsonNode>? rows) => ReadWorld(V2(rows, AddGamePlayers));

    private static PlayerWorld V1World()
    {
        var root = V1();
        AddGamePlayers(root);
        return ReadWorld(root.ToJsonString());
    }

    private static readonly Lazy<PlayerWorld> Full = new(() => V2World(FullRows));

    private static PlayerSheet Sheet(PlayerWorld world, string id) => world.Players.Single(p => p.Id == P(id));

    // ---------------------------------------------------------------- states

    private static GameState Start => StartState(NonAutomated, Pc.Gid);

    private static GameState Approach(GameState s, string id) => GameReducer.Apply(s, new ApproachEvent(id, ApproachKind.Normal)).Value.State;

    private static GameState PinchHitter(GameState s, string player) =>
        GameReducer.Apply(s, new SubstitutionEvent("t-sub", SubstitutionKind.PinchHit, TeamSide.Away, P(player))).Value.State;

    /// <summary>First batter a1 (pitcher hp), approach chosen: an approach-driven pitch is legal.</summary>
    private static GameState Batter1 => Approach(Start, "t-a");

    /// <summary>The second batter of the order, a2 (the batting order index moved without playing a plate appearance).</summary>
    private static GameState Batter2 => Approach(Start with { Away = Start.Away with { NextBatterIndex = 1 } }, "t-a");

    private static GameState PinchHit => Approach(PinchHitter(Start, "ab1"), "t-b");

    // ---------------------------------------------------------------- helpers

    private static Result<PreparedPitch> FromProfiles(GameState state, ulong seed, PlayerWorld world, bool take = false) =>
        PitchPreparation.PrepareFromProfiles(state, Snapshot(seed, Pc.Gid), Tuning, PitchIntent.Balanced, take,
            Sheet(world, state.Batting.CurrentBatterSlot.Player.Value), Sheet(world, state.Fielding.Pitcher.Value), world.BatterTendencies);

    private static PitchBranchDraft Draft(Result<PreparedPitch> r)
    {
        Assert.True(r.IsSuccess, r.Error?.Code + " " + r.Error?.Message);
        return r.Value.Draft;
    }

    private static PitchBranchDraft Plain(GameState state, ulong seed, PlayerWorld world, BatterZoneTendency? tendency = null) =>
        Draft(PitchPreparation.Prepare(state, Snapshot(seed, Pc.Gid), Tuning, PitchIntent.Balanced, false,
            Sheet(world, state.Batting.CurrentBatterSlot.Player.Value), Sheet(world, state.Fielding.Pitcher.Value), batterTendency: tendency));

    private static string TraceText(PitchBranchDraft d) =>
        string.Join("/", d.Steps.Select(s => $"{s.Stage}:{s.Outcome}:{s.Roll}:{string.Join(",", s.Trace.Final)}:{s.Trace.GuardShiftPpm}:{s.Trace.TendencyShiftPpm}"));

    /// <summary>Row-major index of an inner cell ("in.r{row}c{col}"); -1 for an outer zone.</summary>
    private static int InnerIndex(string zone) =>
        zone.StartsWith("in.r", StringComparison.Ordinal) ? (zone[4] - '1') * 3 + (zone[6] - '1') : -1;

    /// <summary>The ADR-006 arithmetic: only swing_result and batted_ball in an inner cell move, by value x cap / 100 ppm.</summary>
    private static int ExpectedShift(string stage, string zone, int[] vector)
    {
        var index = InnerIndex(zone);
        if (index < 0 || stage is not (StageIds.SwingResult or StageIds.BattedBall))
            return 0;
        return vector[index] * ((stage == StageIds.SwingResult ? SwingCap : BattedCap) / 100);
    }

    /// <summary>Asserts every step of the draft carries exactly the expected shift for this vector; returns how many tendency-stage steps were non-zero.</summary>
    private static int AssertShifts(PitchBranchDraft d, int[] vector)
    {
        var moved = 0;
        foreach (var step in d.Steps)
        {
            var expected = ExpectedShift(step.Stage, d.Location.Zone.Value, vector);
            Assert.Equal(expected, step.Trace.TendencyShiftPpm);
            Assert.Equal(0, step.Trace.GuardShiftPpm);
            Assert.Equal(1_000_000, step.Trace.Final.Sum());
            if (expected != 0)
                moved++;
        }

        return moved;
    }

    private const int Seeds = 3000;

    // ---------------------------------------------------------------- the loaded profile reaches the resolver

    [Fact]
    public void The_pack_loaded_profile_of_the_current_batter_moves_the_swing_and_batted_ball_distributions_by_value_times_cap_over_100()
    {
        var world = Full.Value;
        int swing = 0, batted = 0, plus = 0, minus = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var draft = Draft(FromProfiles(Batter1, seed, world));
            AssertShifts(draft, CenterPlus);
            foreach (var step in draft.Steps)
            {
                var shift = step.Trace.TendencyShiftPpm;
                if (shift > 0) plus++;
                if (shift < 0) minus++;
                if (shift != 0 && step.Stage == StageIds.SwingResult) swing++;
                if (shift != 0 && step.Stage == StageIds.BattedBall) batted++;
            }
        }

        // the sampled pitches cover both tendency stages, the +100 cell (r2c2) and the -100 cell (r1c1)
        Assert.True(swing > 100 && batted > 30 && plus > 50 && minus > 50, $"swing={swing} batted={batted} plus={plus} minus={minus}");
    }

    [Fact]
    public void The_shifted_distribution_is_the_unshifted_one_plus_the_shift_in_the_favoured_mass_and_the_ability_modifiers_are_not_applied_twice()
    {
        var world = Full.Value;
        string[] swingFavoured = ["foul", "in_play"], battedFavoured = ["ground_hard", "line_hard", "fly_hard"];
        var compared = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var neutral = Plain(Batter1, seed, world);
            var profiled = Draft(FromProfiles(Batter1, seed, world));
            // up to the first step where the paths split (another stage or another condition, e.g. a different batted-ball type), the same decision is rolled from the same position
            foreach (var (n, p) in neutral.Steps.Zip(profiled.Steps).TakeWhile(x => x.First.Stage == x.Second.Stage && x.First.Condition == x.Second.Condition))
            {
                Assert.Equal(n.Roll, p.Roll);
                Assert.Equal(n.Trace.AfterModifiers, p.Trace.AfterModifiers);  // the ability modifiers: identical, not applied a second time
                Assert.Equal(n.Trace.Modifiers.Select(m => (m.Slot, m.FactorPpm)), p.Trace.Modifiers.Select(m => (m.Slot, m.FactorPpm)));
                var favoured = p.Stage == StageIds.SwingResult ? swingFavoured : p.Stage == StageIds.BattedBall ? battedFavoured : null;
                if (favoured is null || p.Trace.TendencyShiftPpm == 0)
                {
                    Assert.Equal(n.Trace.Final, p.Trace.Final);
                    continue;
                }

                int Mass(ProbabilityTrace t, IReadOnlyList<int> v) => t.Outcomes.Select((o, i) => favoured.Contains(o) ? v[i] : 0).Sum();
                Assert.Equal(Mass(n.Trace, n.Trace.Final) + p.Trace.TendencyShiftPpm, Mass(p.Trace, p.Trace.Final));
                compared++;
            }
        }

        Assert.True(compared > 100);
    }

    [Fact]
    public void The_profile_path_is_exactly_the_explicit_vector_path_and_adds_no_calculation_of_its_own()
    {
        var world = Full.Value;
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var viaProfiles = Draft(FromProfiles(Batter1, seed, world));
            var viaVector = Plain(Batter1, seed, world, world.BatterTendencies.TendencyFor(P("a1")));
            Assert.Equal(Summary(viaVector), Summary(viaProfiles));
            Assert.Equal(TraceText(viaVector), TraceText(viaProfiles));
        }
    }

    [Fact]
    public void Each_batter_gets_his_own_profile_and_the_original_template_player_is_not_mixed_up_with_his_clone()
    {
        var world = Full.Value;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            AssertShifts(Draft(FromProfiles(Batter1, seed, world)), CenterPlus);
            AssertShifts(Draft(FromProfiles(Batter2, seed, world)), CenterMinus);
            AssertShifts(Draft(FromProfiles(PinchHit, seed, world)), Pull);
        }

        // sanity: the three vectors really differ, so passing all three above means three different lookups
        Assert.False(CenterPlus.SequenceEqual(CenterMinus) || CenterPlus.SequenceEqual(Pull) || CenterMinus.SequenceEqual(Pull));
        Assert.Equal(P("a1"), Batter1.Batting.CurrentBatterSlot.Player);
        Assert.Equal(P("a2"), Batter2.Batting.CurrentBatterSlot.Player);
        Assert.Equal(P("ab1"), PinchHit.Batting.CurrentBatterSlot.Player);
        Assert.Equal(Sheet(world, "a1").Player.DisplayName, Sheet(world, "ab1").Player.DisplayName); // same name, same ratings, different PlayerId
    }

    [Fact]
    public void A_pinch_hitter_gets_his_own_profile_never_the_replaced_batters()
    {
        var world = Full.Value;
        var before = Batter1;
        var after = PinchHit;

        Assert.Equal(P("a1"), before.Batting.CurrentBatterSlot.Player);
        Assert.Equal(P("ab1"), after.Batting.CurrentBatterSlot.Player);
        for (ulong seed = 1; seed <= 600; seed++)
        {
            var asA1 = Draft(FromProfiles(before, seed, world));
            var asPinch = Draft(FromProfiles(after, seed, world));
            Assert.Equal(asA1.Location.Zone, asPinch.Location.Zone);          // same pitch location: the location is drawn before and apart from any batter tendency
            AssertShifts(asA1, CenterPlus);
            AssertShifts(asPinch, Pull);
        }

        // the replaced batter's sheet is no longer the current batter: refused, and no profile is looked up for him
        var stale = PitchPreparation.PrepareFromProfiles(after, Snapshot(1, Pc.Gid), Tuning, PitchIntent.Balanced, false, Sheet(world, "a1"), Sheet(world, "hp"), world.BatterTendencies);
        Assert.Equal("PitchPrepare.batter_mismatch", stale.Error!.Value.Code);
    }

    [Fact]
    public void A_batter_without_a_row_is_neutral_and_equals_the_unchanged_no_tendency_path()
    {
        var world = Full.Value;
        var third = Approach(Start with { Away = Start.Away with { NextBatterIndex = 2 } }, "t-a"); // a3 bats: no row
        Assert.Equal(P("a3"), third.Batting.CurrentBatterSlot.Player);
        Assert.False(world.BatterTendencies.Contains(P("a3")));
        for (ulong seed = 1; seed <= 600; seed++)
        {
            var profiled = Draft(FromProfiles(third, seed, world));
            var plain = Plain(third, seed, world);
            var explicitNeutral = Plain(third, seed, world, BatterZoneTendency.Neutral);
            Assert.Equal(Summary(plain), Summary(profiled));
            Assert.Equal(TraceText(plain), TraceText(profiled));
            Assert.Equal(TraceText(plain), TraceText(explicitNeutral));
            Assert.All(profiled.Steps, s => Assert.Equal(0, s.Trace.TendencyShiftPpm));
        }
    }

    [Fact]
    public void Every_neutral_shape_of_the_input_gives_the_same_pitches_as_the_legacy_path()
    {
        // v1 pack, v2 omitted, v2 empty array, v2 without the current batter's row, v2 with an explicit all-zero row for him
        var shapes = new[]
        {
            V1World(),
            V2World(null),
            V2World(new List<JsonNode>()),
            V2World([Row("a2", CenterMinus), Row("ab1", Pull)]),
            V2World([Row("a1", new int[9])]),
        };
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var legacy = Plain(Batter1, seed, shapes[0]);
            foreach (var world in shapes)
            {
                var d = Draft(FromProfiles(Batter1, seed, world));
                Assert.Equal(Summary(legacy), Summary(d));
                Assert.Equal(TraceText(legacy), TraceText(d));
            }
        }
    }

    [Fact]
    public void The_profile_path_works_for_explicit_take_and_a_tendency_never_reaches_a_stage_a_take_does_not_run()
    {
        var world = Full.Value;
        for (ulong seed = 1; seed <= 600; seed++)
        {
            var d = Draft(FromProfiles(Start, seed, world, take: true));
            Assert.True(d.ExplicitTake);
            Assert.All(d.Steps, s => Assert.Equal(0, s.Trace.TendencyShiftPpm)); // a take has no swing_result or batted_ball
        }
    }

    // ---------------------------------------------------------------- RNG contract (D-T6)

    [Fact]
    public void A_tendency_adds_no_stage_and_no_draw_of_its_own_and_each_executed_stage_is_exactly_one_call()
    {
        var world = Full.Value;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var neutral = Plain(Batter1, seed, world);
            var profiled = Draft(FromProfiles(Batter1, seed, world));
            Assert.Equal(profiled.Steps.Length, profiled.NextBoundedCalls);                      // one call per executed stage, none for the tendency
            Assert.Equal(neutral.Steps.Length, neutral.NextBoundedCalls);
            Assert.Equal(profiled.NextBoundedCalls, profiled.StreamUsage.Sum(u => u.Calls));
            Assert.Subset(new[] { GameRngStreams.Target, GameRngStreams.Location, GameRngStreams.Pitch, GameRngStreams.Contact, GameRngStreams.Field }.ToHashSet(),
                profiled.StreamUsage.Select(u => u.Stream).ToHashSet());                        // no stream outside the five the resolver already uses
        }
    }

    [Fact]
    public void On_the_same_executed_path_the_calls_the_rolls_and_the_final_rng_position_are_identical()
    {
        var world = Full.Value;
        var samePath = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var neutral = Plain(Batter1, seed, world);
            var profiled = Draft(FromProfiles(Batter1, seed, world));
            foreach (var (n, p) in neutral.Steps.Zip(profiled.Steps).TakeWhile(x => x.First.Stage == x.Second.Stage))
            {
                Assert.Equal(n.Stream, p.Stream);
                Assert.Equal(n.Roll, p.Roll);
                Assert.Equal(n.RawDraws, p.RawDraws);
            }

            if (!neutral.Steps.Select(s => s.Stage).SequenceEqual(profiled.Steps.Select(s => s.Stage)))
                continue;
            samePath++;
            Assert.Equal(neutral.NextBoundedCalls, profiled.NextBoundedCalls);
            Assert.Equal(neutral.NextRng.Canonical(), profiled.NextRng.Canonical());
            Assert.Equal(neutral.StreamUsage.Select(u => (u.Stream, u.Calls, u.RawDraws)), profiled.StreamUsage.Select(u => (u.Stream, u.Calls, u.RawDraws)));
        }

        Assert.True(samePath > Seeds / 2);
    }

    [Fact]
    public void A_tendency_may_change_the_branch_and_then_later_stages_and_the_total_draw_count_legitimately_differ_but_stay_deterministic()
    {
        var world = Full.Value;
        PitchBranchDraft? neutralOne = null, profiledOne = null;
        for (ulong seed = 1; seed <= Seeds && neutralOne is null; seed++)
        {
            var neutral = Plain(Batter1, seed, world);
            var profiled = Draft(FromProfiles(Batter1, seed, world));
            // a swing that stays out of play without the tendency but is put in play with it (the favoured mass includes in_play): batted_ball and defense_result now run as well
            if (neutral.Kind != PitchBranchKind.InPlay && profiled.Kind == PitchBranchKind.InPlay)
                (neutralOne, profiledOne) = (neutral, profiled);
        }

        Assert.NotNull(neutralOne);
        Assert.NotNull(profiledOne);
        Assert.True(profiledOne!.Steps.Length > neutralOne!.Steps.Length);
        Assert.Equal(neutralOne.Steps.Length + 2, profiledOne.Steps.Length);                        // + batted_ball + defense_result: stages of the existing contract, no tendency stage
        Assert.True(profiledOne.NextBoundedCalls > neutralOne.NextBoundedCalls);                  // not a violation: more stages ran
        Assert.NotEqual(neutralOne.NextRng.Canonical(), profiledOne.NextRng.Canonical());
        Assert.Contains(profiledOne.Steps, s => s.Stage == StageIds.BattedBall);
        Assert.DoesNotContain(neutralOne.Steps, s => s.Stage == StageIds.BattedBall);
        Assert.Equal(neutralOne.Steps[0].Roll, profiledOne.Steps[0].Roll);                       // up to the split everything is shared
        // the same input and seed give the same result every time
        var seedOfOne = Enumerable.Range(1, Seeds).Select(i => (ulong)i).First(s => Plain(Batter1, s, world).Kind != PitchBranchKind.InPlay && Draft(FromProfiles(Batter1, s, world)).Kind == PitchBranchKind.InPlay);
        Assert.Equal(Summary(profiledOne), Summary(Draft(FromProfiles(Batter1, seedOfOne, world))));
        Assert.Equal(TraceText(profiledOne), TraceText(Draft(FromProfiles(Batter1, seedOfOne, world))));
    }

    // ---------------------------------------------------------------- guards of the new door

    [Fact]
    public void Preparation_errors_keep_the_prepare_codes_and_a_null_profile_set_is_refused_but_an_empty_one_is_valid()
    {
        var world = Full.Value;
        var state = Batter1;
        var rng = Snapshot(1, Pc.Gid);
        var (b, p) = (Sheet(world, "a1"), Sheet(world, "hp"));
        Assert.Equal("PitchPrepare.profiles_missing", PitchPreparation.PrepareFromProfiles(state, rng, Tuning, PitchIntent.Balanced, false, b, p, null).Error!.Value.Code);
        Assert.True(PitchPreparation.PrepareFromProfiles(state, rng, Tuning, PitchIntent.Balanced, false, b, p, BatterZoneProfiles.Empty).IsSuccess);
        Assert.Equal("PitchPrepare.state_missing", PitchPreparation.PrepareFromProfiles(null, rng, Tuning, PitchIntent.Balanced, false, b, p, world.BatterTendencies).Error!.Value.Code);
        Assert.Equal("PitchPrepare.rng_missing", PitchPreparation.PrepareFromProfiles(state, null, Tuning, PitchIntent.Balanced, false, b, p, world.BatterTendencies).Error!.Value.Code);
        Assert.Equal("PitchPrepare.tuning_missing", PitchPreparation.PrepareFromProfiles(state, rng, null, PitchIntent.Balanced, false, b, p, world.BatterTendencies).Error!.Value.Code);
        Assert.Equal("PitchPrepare.batter_sheet_missing", PitchPreparation.PrepareFromProfiles(state, rng, Tuning, PitchIntent.Balanced, false, null, p, world.BatterTendencies).Error!.Value.Code);
        Assert.Equal("PitchPrepare.pitcher_sheet_missing", PitchPreparation.PrepareFromProfiles(state, rng, Tuning, PitchIntent.Balanced, false, b, null, world.BatterTendencies).Error!.Value.Code);
        Assert.Equal("PitchPrepare.batter_mismatch", PitchPreparation.PrepareFromProfiles(state, rng, Tuning, PitchIntent.Balanced, false, Sheet(world, "a2"), p, world.BatterTendencies).Error!.Value.Code);
        Assert.Equal("PitchPrepare.pitcher_mismatch", PitchPreparation.PrepareFromProfiles(state, rng, Tuning, PitchIntent.Balanced, false, b, Sheet(world, "a2"), world.BatterTendencies).Error!.Value.Code);
        Assert.Equal("PitchBranch.game_not_in_progress", PitchPreparation.PrepareFromProfiles(state with { Status = GameStatus.Final }, rng, Tuning, PitchIntent.Balanced, false, b, p, world.BatterTendencies).Error!.Value.Code);
        Assert.Equal("PitchBranch.pitch_not_legal", PitchPreparation.PrepareFromProfiles(Start, rng, Tuning, PitchIntent.Balanced, false, b, p, world.BatterTendencies).Error!.Value.Code); // no approach yet
        // the error text carries no vector
        foreach (var bad in new[] { PitchPreparation.PrepareFromProfiles(state, rng, Tuning, PitchIntent.Balanced, false, Sheet(world, "a2"), p, world.BatterTendencies) })
            Assert.DoesNotContain("100", bad.Error!.Value.Message);
    }

    [Fact]
    public void A_name_is_never_the_key_two_players_with_one_display_name_are_looked_up_by_their_ids()
    {
        var world = Full.Value;
        Assert.Equal(Sheet(world, "a1").Player.DisplayName, Sheet(world, "a3").Player.DisplayName);
        Assert.Equal(Sheet(world, "a1").Player.DisplayName, Sheet(world, "p-0005").Player.DisplayName);
        Assert.NotSame(world.BatterTendencies.TendencyFor(P("a1")), world.BatterTendencies.TendencyFor(P("a3")));
        Assert.Equal(CenterPlus, world.BatterTendencies.TendencyFor(P("a1")).Values);
        Assert.True(world.BatterTendencies.TendencyFor(P("a3")).IsNeutral);
        Assert.Equal(Away, world.BatterTendencies.TendencyFor(P("p-0001")).Values);           // the template player keeps his own row
    }

    [Fact]
    public void A_tendency_balanced_against_another_reference_is_refused_up_front_not_only_on_pitches_that_reach_a_tendency_stage()
    {
        // only one reference is registered today, so a foreign-reference vector can exist only through the private constructor (a stand-in for a future reference)
        var foreign = (BatterZoneTendency)typeof(BatterZoneTendency).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0].Invoke(["pzl-ref-future-v9", new int[9]]);
        var profiles = BatterZoneProfiles.Create([new BatterZoneProfileEntry(P("a1"), foreign)]).Value;
        var world = Full.Value;
        var (b, p) = (Sheet(world, "a1"), Sheet(world, "hp"));
        int explicitOk = 0, explicitFailed = 0;
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var viaProfiles = PitchPreparation.PrepareFromProfiles(Batter1, Snapshot(seed, Pc.Gid), Tuning, PitchIntent.Balanced, false, b, p, profiles);
            Assert.Equal("PitchPrepare.tendency_reference_mismatch", viaProfiles.Error!.Value.Code);   // every pitch, whatever the branch
            if (PitchPreparation.Prepare(Batter1, Snapshot(seed, Pc.Gid), Tuning, PitchIntent.Balanced, false, b, p, batterTendency: foreign).IsSuccess)
                explicitOk++;
            else
                explicitFailed++;
        }

        Assert.True(explicitOk > 0 && explicitFailed > 0); // without the guard the failure would depend on the drawn branch
    }

    [Fact]
    public void The_simulation_assembly_cannot_reload_a_pack_and_the_door_takes_no_source_path_or_vector()
    {
        var referenced = typeof(PitchPreparation).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.DoesNotContain("StarPlayer.Application", referenced);
        Assert.DoesNotContain("StarPlayer.Infrastructure", referenced);

        var door = typeof(PitchPreparation).GetMethod(nameof(PitchPreparation.PrepareFromProfiles))!;
        var parameters = door.GetParameters().Select(x => x.ParameterType).ToArray();
        Assert.Contains(typeof(BatterZoneProfiles), parameters);
        Assert.DoesNotContain(typeof(BatterZoneTendency), parameters);     // never a set AND an explicit vector together
        Assert.DoesNotContain(typeof(string), parameters);
        Assert.DoesNotContain(parameters, t => typeof(Delegate).IsAssignableFrom(t));
        // the explicit-vector door still exists, separately
        Assert.Contains(typeof(PitchPreparation).GetMethod(nameof(PitchPreparation.Prepare))!.GetParameters(), x => x.ParameterType == typeof(BatterZoneTendency));
        Assert.DoesNotContain(typeof(PitchPreparation).GetMethod(nameof(PitchPreparation.Prepare))!.GetParameters(), x => x.ParameterType == typeof(BatterZoneProfiles));
    }

    [Fact]
    public void The_set_a_game_holds_is_one_immutable_instance_that_replacing_the_world_member_or_the_source_text_cannot_change()
    {
        var json = V2(FullRows, AddGamePlayers);
        var world = ReadWorld(json);
        var held = world.BatterTendencies;                       // what a game takes once at its start
        var snapshot = Enumerable.Range(1, 200).Select(s => TraceText(Draft(FromProfiles(Batter1, (ulong)s, world)))).ToArray();

        var edited = JsonNode.Parse(json)!;
        edited["batter_zone_tendencies"]![0]!["values"] = new JsonArray(0, 0, 0, 0, 0, 0, 0, 0, 0); // the file text changes afterwards
        var swapped = world with { BatterTendencies = BatterZoneProfiles.Empty };                      // a later world no longer has them
        var reloaded = ReadWorld(edited.ToJsonString());

        Assert.Same(held, world.BatterTendencies);
        Assert.Equal(CenterPlus, held.TendencyFor(P("a1")).Values);
        Assert.Equal(0, swapped.BatterTendencies.Count);
        Assert.True(reloaded.BatterTendencies.TendencyFor(P("a1")).IsNeutral);
        // the held set still drives the same pitches pitch after pitch
        var again = Enumerable.Range(1, 200).Select(s => TraceText(Draft(PitchPreparation.PrepareFromProfiles(Batter1, Snapshot((ulong)s, Pc.Gid), Tuning, PitchIntent.Balanced, false,
            Sheet(world, "a1"), Sheet(world, "hp"), held)))).ToArray();
        Assert.Equal(snapshot, again);
    }

    // ---------------------------------------------------------------- the approved commit path

    private static (AuthoritativeGame Game, PlayerSheet Batter, PlayerSheet Pitcher) Aggregate(PlayerWorld world, ulong seed)
    {
        var driver = Pc.Situation(0, 0, approach: true);
        var game = Pc.Aggregate(driver, seed);
        return (game, Sheet(world, game.State.Batting.CurrentBatterSlot.Player.Value), Sheet(world, game.State.Fielding.Pitcher.Value));
    }

    [Fact]
    public void A_pitch_decided_with_the_loaded_tendency_is_staged_and_published_through_the_commit_gate_with_the_resolvers_rng_position()
    {
        var world = Full.Value;
        var committed = 0;
        PitchBranchKind? tendencyChangedBranch = null;
        for (ulong seed = 1; seed <= Seeds && committed < 12; seed++)
        {
            var (game, batter, pitcher) = Aggregate(world, seed);
            var prepared = Ok(PitchPreparation.PrepareFromProfiles(game.State, game.Rng, Tuning, PitchIntent.Balanced, false, batter, pitcher, world.BatterTendencies));
            if (prepared.Draft.Kind == PitchBranchKind.InPlay || AssertShifts(prepared.Draft, CenterPlus) == 0)
                continue;                                    // keep pitches where the tendency really moved a distribution and the C3-B non-in-play gate applies
            var candidate = Ok(PitchPreparation.BuildNonInPlayCandidate(prepared, game.State, game.Rng, Tuning, batter, pitcher, game.LastEventSequence));
            var staged = Ok(PitchCommitGate.Stage(game, prepared, candidate, Tuning, batter, pitcher));
            var cursor = new GameCursor(game);
            var record = Ok(cursor.Publish(staged));

            Assert.Equal(game.Revision + 1, cursor.Current.Revision);
            Assert.Same(prepared.Draft.NextRng, candidate.NextRng);
            Assert.Equal(prepared.Draft.NextRng.StateHash, cursor.Current.Rng.StateHash);          // the resolver's position is what the game adopts: the tendency made no extra draw
            Assert.Equal(prepared.Draft.NextRng.StateHash, record.RngStateHashAfter);
            Assert.Equal(game.Rng.StateHash, record.RngStateHashBefore);
            Assert.Equal(EventFingerprint.VersionV2, record.Receipt.FingerprintVersion);           // ef2: the location is in the event, the tendency is in nothing
            Assert.Equal(game.State.AppliedEvents.Count + 1, cursor.Current.State.AppliedEvents.Count);
            Assert.NotNull(GameEventLocation.Of(record.Event));
            Assert.Equal(prepared.Draft.Location.Zone, GameEventLocation.Of(record.Event)!.Zone);
            committed++;

            var neutralKind = Draft(PitchPreparation.Prepare(game.State, game.Rng, Tuning, PitchIntent.Balanced, false, batter, pitcher)).Kind;
            if (neutralKind != prepared.Draft.Kind)
                tendencyChangedBranch ??= prepared.Draft.Kind;
        }

        Assert.Equal(12, committed);
        // at least one committed pitch is one the tendency decided differently from the neutral path (the committed event follows the profile-based draft)
        Assert.NotNull(tendencyChangedBranch);
    }

    [Fact]
    public void The_committed_record_and_the_receipt_expose_nothing_of_the_tendency()
    {
        var world = Full.Value;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var (game, batter, pitcher) = Aggregate(world, seed);
            var prepared = Ok(PitchPreparation.PrepareFromProfiles(game.State, game.Rng, Tuning, PitchIntent.Balanced, false, batter, pitcher, world.BatterTendencies));
            if (prepared.Draft.Kind == PitchBranchKind.InPlay)
                continue;
            var candidate = Ok(PitchPreparation.BuildNonInPlayCandidate(prepared, game.State, game.Rng, Tuning, batter, pitcher, game.LastEventSequence));
            var cursor = new GameCursor(game);
            var record = Ok(cursor.Publish(Ok(PitchCommitGate.Stage(game, prepared, candidate, Tuning, batter, pitcher))));

            var text = $"{record} {record.Event} {record.Receipt} {cursor.Current} {cursor.Current.State.AppliedEvents.Last()}";
            Assert.DoesNotContain("BatterZoneProfiles", text);
            Assert.DoesNotContain("BatterZoneTendency", text);
            Assert.DoesNotContain(nameof(PreparedPitch), record.GetType().GetProperties().Select(x => x.PropertyType.Name));
            Assert.DoesNotContain(typeof(CommitRecord).GetProperties(), x => x.PropertyType == typeof(BatterZoneTendency) || x.PropertyType == typeof(BatterZoneProfiles));
            Assert.DoesNotContain(typeof(GameState).GetProperties(), x => x.PropertyType == typeof(BatterZoneTendency) || x.PropertyType == typeof(BatterZoneProfiles));
            return;
        }

        Assert.Fail("no non-in-play pitch found");
    }

    [Fact]
    public void An_in_play_pitch_prepared_from_the_profiles_reaches_the_in_play_planner_unchanged()
    {
        var world = Full.Value;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var (game, batter, pitcher) = Aggregate(world, seed);
            var prepared = Ok(PitchPreparation.PrepareFromProfiles(game.State, game.Rng, Tuning, PitchIntent.Balanced, false, batter, pitcher, world.BatterTendencies));
            if (prepared.Draft.Kind != PitchBranchKind.InPlay)
                continue;
            var planned = InPlayPitchPlanning.Build(prepared, game.State, game.Rng, Tuning, batter, pitcher, game.LastEventSequence);
            Assert.True(planned.IsSuccess, planned.Error?.Code + " " + planned.Error?.Message);
            Assert.Contains(prepared.Draft.Steps, s => s.Stage == StageIds.BattedBall);
            return;
        }

        Assert.Fail("no in-play pitch found");
    }

    private static T Ok<T>(Result<T> r) => Pc.Ok(r);
}
