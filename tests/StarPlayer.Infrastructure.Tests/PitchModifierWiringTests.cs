using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-D: the nine live ability/strategy/count/handedness modifier slots on the pitch branch. Expected distributions, factors and outcomes come from the INDEPENDENT Python
/// oracle tests/Fixtures/generator/generate_pitch_modifier_golden.py (own PCG32 + modifier arithmetic from ADR-006 item 8); the direction, mapping and no-leak tests use literal
/// tables written from the approved decisions D-ABILITY-1 (base ratings) and D-ABILITY-2 (count pressure). Nothing here computes an expected value with StageProbability.
/// </summary>
public class PitchModifierWiringTests
{
    private static readonly Lazy<JsonElement> Doc = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tests", "Fixtures", "phase3d_pitch_modifier_golden.json"))).RootElement);

    internal static JsonElement ModifierGolden => Doc.Value;

    private static readonly string[] NineSlots =
    [
        "pitcher.control", "batter.eye", "strategy.approach", "situation.count_pressure", "pitcher.stuff", "batter.contact", "matchup.handedness", "pitcher.velocity", "batter.power",
    ];

    private static readonly Dictionary<string, string[]> SlotsByStage = new()
    {
        [StageIds.TargetZone] = [],
        [StageIds.ActualLocation] = ["pitcher.control"],
        [StageIds.BatterDecision] = ["batter.eye", "strategy.approach", "situation.count_pressure"],
        [StageIds.CalledPitch] = [],
        [StageIds.SwingResult] = ["pitcher.stuff", "batter.contact", "matchup.handedness"],
        [StageIds.BattedBall] = ["pitcher.velocity", "batter.power"],
        [StageIds.DefenseResult] = [],
    };

    // ------------------------------------------------------------------ building the state of a spec (real events only)

    private static ApproachKind Kind(string approach) => approach switch { "patient" => ApproachKind.Patient, "aggressive" => ApproachKind.Aggressive, _ => ApproachKind.Normal };

    /// <summary>A real plate-appearance state: <paramref name="balls"/> balls then <paramref name="strikes"/> called strikes by explicit Takes, then (unless an explicit Take is wanted) the approach.</summary>
    private static GameState StateOf(GameRuleSnapshot rules, string game, int balls, int strikes, bool explicitTake, string? approach)
    {
        var state = StartState(rules, game);
        for (var i = 0; i < balls; i++)
            state = Apply(state, new TakeEvent($"w-b{i}", new PitchSpec(PitchOutcome.Ball)));
        for (var i = 0; i < strikes; i++)
            state = Apply(state, new TakeEvent($"w-s{i}", new PitchSpec(PitchOutcome.CalledStrike)));
        return explicitTake ? state : Apply(state, new ApproachEvent("w-approach", Kind(approach ?? "normal")));
    }

    private static GameState Apply(GameState state, GameEvent e)
    {
        var applied = GameReducer.Apply(state, e);
        Assert.True(applied.IsSuccess, applied.Error?.Code + " " + applied.Error?.Message);
        return applied.Value.State;
    }

    private static PitchAbilities Abilities(JsonElement spec) => new(
        spec.GetProperty("batter").GetProperty("contact").GetInt32(), spec.GetProperty("batter").GetProperty("power").GetInt32(), spec.GetProperty("batter").GetProperty("eye").GetInt32(),
        spec.GetProperty("pitcher").GetProperty("control").GetInt32(), spec.GetProperty("pitcher").GetProperty("stuff").GetInt32(), spec.GetProperty("pitcher").GetProperty("velocity").GetInt32());

    private static PitchBranchDraft DrawSpec(JsonElement id, JsonElement spec, ulong seed, string context, bool withAbilities = true)
    {
        var take = spec.GetProperty("explicit_take").GetBoolean();
        var approach = spec.GetProperty("approach").ValueKind == JsonValueKind.Null ? null : spec.GetProperty("approach").GetString();
        var state = StateOf(RulesFor(spec.GetProperty("called_pitch_mode").GetString()!), context, spec.GetProperty("balls").GetInt32(), spec.GetProperty("strikes").GetInt32(), take, approach);
        var request = Request(state, Snapshot(seed, context), Intent(spec.GetProperty("intent").GetString()!), take, Bats(spec.GetProperty("batter_side").GetString()!),
            Throws(spec.GetProperty("pitcher_hand").GetString()!)) with { Abilities = withAbilities ? Abilities(spec) : null };
        _ = id;
        return Resolved(request);
    }

    private static string Stepline(PitchBranchStep s) =>
        $"{s.Stage}|{s.Condition}|{s.Outcome}|{s.Stream.Canonical}|{s.Roll}|{s.RawDraws}|{string.Join(",", s.Trace.Final)}|{string.Join(",", s.Trace.CombinedFactorPpm)}|" +
        string.Join(";", s.Trace.Modifiers.Select(m => $"{m.Slot}={m.Input}@{m.FactorPpm}/{m.Applied}"));

    private static string Stepline(JsonElement s) =>
        $"{s.GetProperty("stage").GetString()}|{s.GetProperty("condition").GetString()}|{s.GetProperty("outcome").GetString()}|{s.GetProperty("stream").GetString()}|{s.GetProperty("roll").GetUInt32()}|" +
        $"{s.GetProperty("raw_draws").GetInt64()}|{string.Join(",", s.GetProperty("final").EnumerateArray().Select(v => v.GetInt32()))}|" +
        $"{string.Join(",", s.GetProperty("combined").EnumerateArray().Select(v => v.GetInt32()))}|" +
        string.Join(";", s.GetProperty("modifiers").EnumerateArray().Select(m => $"{m.GetProperty("slot").GetString()}={m.GetProperty("input").GetString()}@{m.GetProperty("factor_ppm").GetInt32()}/{m.GetProperty("applied").GetBoolean()}"));

    // ------------------------------------------------------------------ the independent oracle

    public static IEnumerable<object[]> MatrixIds() => ModifierGolden.GetProperty("matrix").EnumerateArray().Select(m => new object[] { m.GetProperty("id").GetString()! });

    [Fact]
    public void Every_matrix_pitch_reproduces_the_independent_oracle_distribution_factors_outcomes_and_rolls()
    {
        var steps = 0;
        var stages = new HashSet<string>();
        foreach (var item in ModifierGolden.GetProperty("matrix").EnumerateArray())
        {
            var draft = DrawSpec(item.GetProperty("id"), item.GetProperty("spec"), Hex64(item.GetProperty("seed")), item.GetProperty("context").GetString()!);
            var expected = item.GetProperty("expected");
            Assert.Equal(expected.GetProperty("kind").GetString(), draft.Kind switch
            {
                PitchBranchKind.HitByPitch => "hit_by_pitch", PitchBranchKind.CalledBall => "called_ball", PitchBranchKind.CalledStrike => "called_strike",
                PitchBranchKind.SwingingStrike => "swinging_strike", PitchBranchKind.Foul => "foul", _ => "in_play",
            });
            Assert.Equal(expected.GetProperty("location_zone").GetString(), draft.Location.Zone.Value);
            Assert.Equal(expected.GetProperty("total_calls").GetInt32(), draft.NextBoundedCalls);
            var want = expected.GetProperty("steps").EnumerateArray().Select(Stepline).ToArray();
            Assert.Equal(want, draft.Steps.Select(Stepline).ToArray());
            steps += want.Length;
            foreach (var s in draft.Steps)
                stages.Add(s.Stage);
        }

        Assert.True(steps > 1500);
        Assert.Contains(StageIds.SwingResult, stages);
        Assert.Contains(StageIds.BattedBall, stages);
    }

    // ------------------------------------------------------------------ which slot reaches which stage, and nothing else

    [Fact]
    public void Exactly_the_nine_approved_slots_reach_exactly_their_own_stage_and_no_other_slot_or_stage_gets_a_modifier()
    {
        var seen = new HashSet<string>();
        foreach (var item in ModifierGolden.GetProperty("matrix").EnumerateArray())
        {
            var draft = DrawSpec(item.GetProperty("id"), item.GetProperty("spec"), Hex64(item.GetProperty("seed")), item.GetProperty("context").GetString()!);
            foreach (var step in draft.Steps)
            {
                Assert.Equal(SlotsByStage[step.Stage], step.Trace.Modifiers.Select(m => m.Slot).ToArray());
                foreach (var m in step.Trace.Modifiers)
                    seen.Add(m.Slot);
            }
        }

        Assert.Equal(NineSlots.OrderBy(x => x, StringComparer.Ordinal), seen.OrderBy(x => x, StringComparer.Ordinal));
        foreach (var banned in new[] { "defense.", "runner.", "park.", "catcher.", "battery." })
            Assert.DoesNotContain(seen, s => s.StartsWith(banned, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_applied_factor_and_every_combined_factor_stays_inside_the_approved_caps()
    {
        // slot caps from content/tuning/virtual_tuning_v1.json (min_ppm, max_ppm); combined cap 800,000..1,250,000
        var caps = new Dictionary<string, (int Min, int Max)>
        {
            ["pitcher.control"] = (900_000, 1_100_000), ["batter.eye"] = (900_000, 1_100_000), ["strategy.approach"] = (850_000, 1_150_000),
            ["situation.count_pressure"] = (850_000, 1_150_000), ["pitcher.stuff"] = (900_000, 1_100_000), ["batter.contact"] = (900_000, 1_100_000),
            ["matchup.handedness"] = (950_000, 1_050_000), ["pitcher.velocity"] = (900_000, 1_100_000), ["batter.power"] = (900_000, 1_100_000),
        };
        foreach (var item in ModifierGolden.GetProperty("matrix").EnumerateArray())
            foreach (var step in DrawSpec(item.GetProperty("id"), item.GetProperty("spec"), Hex64(item.GetProperty("seed")), item.GetProperty("context").GetString()!).Steps)
            {
                Assert.All(step.Trace.Modifiers, m =>
                {
                    var (min, max) = caps[m.Slot];
                    Assert.InRange(m.FactorPpm, min, max);
                });
                Assert.All(step.Trace.CombinedFactorPpm, c => Assert.InRange(c, 800_000, 1_250_000));
                Assert.Equal(1_000_000, step.Trace.Final.Sum());
            }
    }

    // ------------------------------------------------------------------ the two approved decisions

    [Theory]
    [InlineData(0, 0, "neutral")]
    [InlineData(1, 0, "neutral")]
    [InlineData(2, 0, "neutral")]
    [InlineData(3, 0, "three_balls")]
    [InlineData(0, 1, "neutral")]
    [InlineData(1, 1, "neutral")]
    [InlineData(2, 1, "neutral")]
    [InlineData(3, 1, "three_balls")]
    [InlineData(0, 2, "two_strikes")]
    [InlineData(1, 2, "two_strikes")]
    [InlineData(2, 2, "two_strikes")]
    [InlineData(3, 2, "neutral")]   // full count: never both states, and no new full-count coefficient
    public void Count_pressure_is_a_single_state_taken_from_the_count_that_exists_before_the_pitch(int balls, int strikes, string key)
    {
        var state = StateOf(Automated, "w-count", balls, strikes, explicitTake: true, approach: null);
        Assert.Equal(new PitchCountState(balls, strikes), state.Count);
        var draft = Resolved(Request(state, Snapshot(5, "w-count"), explicitTake: true) with { Abilities = new PitchAbilities(50, 50, 50, 50, 50, 50) });
        var step = draft.Steps.Single(s => s.Stage == StageIds.BatterDecision);
        var count = step.Trace.Modifiers.Single(m => m.Slot == "situation.count_pressure");
        Assert.Equal(key, count.Input);
        Assert.Equal(key switch { "three_balls" => 880_000, "two_strikes" => 1_100_000, _ => 1_000_000 }, count.FactorPpm);
        Assert.Equal(1, step.Trace.Modifiers.Count(m => m.Slot == "situation.count_pressure")); // one input, never two
    }

    [Theory]
    [InlineData(ApproachKind.Normal, "neutral", 1_000_000)]
    [InlineData(ApproachKind.Patient, "patient", 880_000)]
    [InlineData(ApproachKind.Aggressive, "aggressive", 1_100_000)]
    public void The_chosen_approach_is_the_strategy_input_and_no_approach_chosen_is_neutral(ApproachKind approach, string key, int factor)
    {
        var chosen = Resolved(Request(InSequenceStateWith(approach), Snapshot(5, "w-approach")) with { Abilities = new PitchAbilities(50, 50, 50, 50, 50, 50) });
        var m = chosen.Steps.Single(s => s.Stage == StageIds.BatterDecision).Trace.Modifiers.Single(x => x.Slot == "strategy.approach");
        Assert.Equal((key, factor), (m.Input, m.FactorPpm));

        var taking = Resolved(Request(StartState(Automated, "w-approach"), Snapshot(5, "w-approach"), explicitTake: true) with { Abilities = new PitchAbilities(50, 50, 50, 50, 50, 50) });
        var none = taking.Steps.Single(s => s.Stage == StageIds.BatterDecision).Trace.Modifiers.Single(x => x.Slot == "strategy.approach");
        Assert.Equal(("neutral", 1_000_000), (none.Input, none.FactorPpm));
    }

    private static GameState InSequenceStateWith(ApproachKind approach) => Apply(StartState(Automated, "w-approach"), new ApproachEvent("w-approach-1", approach));

    [Theory]
    [InlineData(BattingSide.Right, ThrowingHand.Right, "same_hand", 1_040_000)]
    [InlineData(BattingSide.Right, ThrowingHand.Left, "opposite_hand", 970_000)]
    [InlineData(BattingSide.Left, ThrowingHand.Left, "same_hand", 1_040_000)]
    [InlineData(BattingSide.Left, ThrowingHand.Right, "opposite_hand", 970_000)]
    [InlineData(BattingSide.Switch, ThrowingHand.Right, "opposite_hand", 970_000)]   // a switch hitter stands opposite the pitcher's hand
    [InlineData(BattingSide.Switch, ThrowingHand.Left, "opposite_hand", 970_000)]
    public void Handedness_follows_the_applied_batter_side_against_the_throwing_hand(BattingSide bats, ThrowingHand throws, string key, int factor)
    {
        for (ulong seed = 1; seed < 2000; seed++)
        {
            var draft = Resolved(Request(InSequenceState(Automated, "w-hand"), Snapshot(seed, "w-hand"), bats: bats, throws: throws) with { Abilities = new PitchAbilities(50, 50, 50, 50, 50, 50) });
            var swing = draft.Steps.FirstOrDefault(s => s.Stage == StageIds.SwingResult);
            if (swing is null)
                continue;
            var m = swing.Trace.Modifiers.Single(x => x.Slot == "matchup.handedness");
            Assert.Equal((key, factor), (m.Input, m.FactorPpm));
            return;
        }

        Assert.Fail("no swing found");
    }

    // ------------------------------------------------------------------ direction: more of an ability moves one outcome one way

    /// <summary>The first stage distribution of <paramref name="stage"/> in the first seed that reaches it (the same seed and prefix whatever the varied slot is), as a mass per outcome.</summary>
    private static IReadOnlyDictionary<string, int> Distribution(PitchAbilities abilities, string stage, Func<PitchBranchDraft, bool> reaches, GameState? state = null, ulong seedFrom = 1)
    {
        for (var seed = seedFrom; seed < seedFrom + 3000; seed++)
        {
            var neutral = Resolved(Request(state ?? InSequenceState(Automated, "w-dir"), Snapshot(seed, "w-dir"), bats: BattingSide.Right, throws: ThrowingHand.Left) with { Abilities = new PitchAbilities(50, 50, 50, 50, 50, 50) });
            if (!neutral.Steps.Any(s => s.Stage == stage) || !reaches(neutral))
                continue;
            var draft = Resolved(Request(state ?? InSequenceState(Automated, "w-dir"), Snapshot(seed, "w-dir"), bats: BattingSide.Right, throws: ThrowingHand.Left) with { Abilities = abilities });
            var step = draft.Steps.FirstOrDefault(s => s.Stage == stage);
            if (step is null)
                continue;
            return step.Trace.Outcomes.Zip(step.Trace.Final).ToDictionary(p => p.First, p => p.Second);
        }

        throw new Xunit.Sdk.XunitException("no seed reaches " + stage);
    }

    private static readonly PitchAbilities Mid = new(50, 50, 50, 50, 50, 50);

    private static int Share(IReadOnlyDictionary<string, int> d, params string[] outcomes) => outcomes.Sum(o => d[o]);

    [Fact]
    public void More_of_each_ability_moves_its_own_outcome_in_the_documented_direction_and_not_the_other_way()
    {
        bool Outer(PitchBranchDraft d) => d.Location.Zone.Value.StartsWith("out.", StringComparison.Ordinal);
        bool Any(PitchBranchDraft d) => true;
        string[] hard = ["ground_hard", "line_hard", "fly_hard"];

        // pitcher.control up -> the aimed zone is likelier. The same target (first step) in every run, so the aimed-zone mass is comparable.
        var targetSeed = Resolved(Request(InSequenceState(Automated, "w-dir"), Snapshot(1, "w-dir")) with { Abilities = Mid }).Steps[0].Outcome;
        double Control(int v) => Distribution(Mid with { PitcherControl = v }, StageIds.ActualLocation, Any)[targetSeed];
        Assert.True(Control(0) < Control(50) && Control(50) < Control(100));

        // batter.eye up -> a ball outside the zone is chased less (swing falls)
        int Eye(int v) => Distribution(Mid with { BatterEye = v }, StageIds.BatterDecision, Outer)["swing"];
        Assert.True(Eye(0) > Eye(50) && Eye(50) > Eye(100));

        // pitcher.stuff up -> more misses; batter.contact up -> fewer misses
        int Stuff(int v) => Share(Distribution(Mid with { PitcherStuff = v }, StageIds.SwingResult, Any), "miss");
        int Contact(int v) => Share(Distribution(Mid with { BatterContact = v }, StageIds.SwingResult, Any), "miss");
        Assert.True(Stuff(0) < Stuff(50) && Stuff(50) < Stuff(100));
        Assert.True(Contact(0) > Contact(50) && Contact(50) > Contact(100));

        // pitcher.velocity up -> less hard contact; batter.power up -> more hard contact
        int Velocity(int v) => Share(Distribution(Mid with { PitcherVelocity = v }, StageIds.BattedBall, Any), hard);
        int Power(int v) => Share(Distribution(Mid with { BatterPower = v }, StageIds.BattedBall, Any), hard);
        Assert.True(Velocity(0) > Velocity(50) && Velocity(50) > Velocity(100));
        Assert.True(Power(0) < Power(50) && Power(50) < Power(100));

        // a neutral rating (50) leaves the factor exactly neutral for all six
        var step = Resolved(Request(InSequenceState(Automated, "w-dir"), Snapshot(3, "w-dir")) with { Abilities = Mid }).Steps;
        Assert.All(step.SelectMany(s => s.Trace.Modifiers).Where(m => m.Slot is "pitcher.control" or "batter.eye" or "pitcher.stuff" or "batter.contact" or "pitcher.velocity" or "batter.power"),
            m => Assert.Equal(1_000_000, m.FactorPpm));
    }

    // ------------------------------------------------------------------ no new draw, no changed roll

    [Fact]
    public void The_modifiers_consume_no_random_number_add_no_stage_and_never_change_a_roll()
    {
        var diverged = 0;
        foreach (var item in ModifierGolden.GetProperty("matrix").EnumerateArray())
        {
            var spec = item.GetProperty("spec");
            var seed = Hex64(item.GetProperty("seed"));
            var context = item.GetProperty("context").GetString()!;
            var with = DrawSpec(item.GetProperty("id"), spec, seed, context);
            var without = DrawSpec(item.GetProperty("id"), spec, seed, context, withAbilities: false);
            foreach (var (a, b) in with.Steps.Zip(without.Steps).TakeWhile(p => p.First.Stage == p.Second.Stage))
            {
                Assert.Equal(a.Stream, b.Stream);
                Assert.Equal(a.Roll, b.Roll); // the same stream position gives the same roll; only the distribution it is read from may differ
                Assert.Equal(a.RawDraws, b.RawDraws);
            }

            if (with.Steps.Select(s => s.Stage).SequenceEqual(without.Steps.Select(s => s.Stage)))
            {
                Assert.Equal(without.NextBoundedCalls, with.NextBoundedCalls);
                Assert.Equal(without.NextRng.Canonical(), with.NextRng.Canonical());
                Assert.Equal(without.StreamUsage.Select(u => (u.Stream, u.Calls, u.RawDraws)), with.StreamUsage.Select(u => (u.Stream, u.Calls, u.RawDraws)));
            }
            else
                diverged++;
        }

        Assert.True(diverged > 0); // the modifiers do change some outcomes, otherwise this proves nothing
    }

    [Fact]
    public void The_same_request_gives_the_same_draft_every_time()
    {
        foreach (var item in ModifierGolden.GetProperty("matrix").EnumerateArray().Take(60))
        {
            var a = DrawSpec(item.GetProperty("id"), item.GetProperty("spec"), Hex64(item.GetProperty("seed")), item.GetProperty("context").GetString()!);
            var b = DrawSpec(item.GetProperty("id"), item.GetProperty("spec"), Hex64(item.GetProperty("seed")), item.GetProperty("context").GetString()!);
            Assert.Equal(string.Join("\n", a.Steps.Select(Stepline)), string.Join("\n", b.Steps.Select(Stepline)));
            Assert.Equal(Summary(a), Summary(b));
        }
    }

    [Fact]
    public void An_ability_outside_0_to_100_is_refused_before_any_draw_and_never_corrected()
    {
        var state = InSequenceState(Automated, "w-range");
        var rng = Snapshot(9, "w-range");
        foreach (var bad in new[] { Mid with { BatterContact = 101 }, Mid with { BatterPower = -1 }, Mid with { BatterEye = 200 }, Mid with { PitcherControl = 101 }, Mid with { PitcherStuff = -5 }, Mid with { PitcherVelocity = 101 } })
        {
            var made = PitchBranchResolver.Resolve(Request(state, rng) with { Abilities = bad });
            Assert.False(made.IsSuccess);
            Assert.Equal("PitchBranch.ability_out_of_range", made.Error!.Value.Code);
        }
    }
}
