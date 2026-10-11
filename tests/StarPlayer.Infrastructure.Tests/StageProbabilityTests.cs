using System.Numerics;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>Phase 3-B2: what a stage computation guarantees - neutrality, bounds, monotonicity, exact sums, traceability and tendency neutrality. Nothing here draws a random number.</summary>
public class StageProbabilityTests
{
    private const int D = ProbabilityScale.Denominator;

    private static readonly TuningProfile Profile = TuningProfileTests.Profile();
    private static readonly ProbabilityModel Model = ProbabilityModel.V1;

    private static StageResult Run(string stage, string condition, IReadOnlyList<ModifierInput>? inputs = null, TendencyContext? tendency = null, TuningProfile? profile = null)
    {
        var made = StageProbability.Compute(profile ?? Profile, stage, condition, inputs, tendency);
        Assert.True(made.IsSuccess, $"{stage}/{condition}: {made.Error?.Code} {made.Error?.Message}");
        return made.Value;
    }

    private static Error Fail(string stage, string condition, IReadOnlyList<ModifierInput>? inputs = null, TendencyContext? tendency = null)
    {
        var made = StageProbability.Compute(Profile, stage, condition, inputs, tendency);
        Assert.False(made.IsSuccess);
        return made.Error!.Value;
    }

    private static ModifierInput Neutral(ModifierSlot slot) =>
        slot.Kind == ModifierKind.Table ? ModifierInput.Choice(slot.Id, "neutral") : ModifierInput.Linear(slot.Id, Profile.Modifier(slot.Id).Value.Neutral);

    private static List<ModifierInput> AllNeutral(string stage) => Model.Slots.Where(s => s.Stage == stage).Select(Neutral).ToList();

    private static IEnumerable<(StageDef Stage, StageCondition Condition)> Everything() =>
        Model.Stages.SelectMany(s => s.Conditions.Select(c => (s, c)));

    private static IEnumerable<int> TargetIndexes(ModifierSlot slot, StageCondition condition) =>
        condition.Outcomes.Select((o, i) => (o, i)).Where(x => slot.Targets.Any(t => t == ProbabilityModel.CondTarget ? x.o == condition.Key : t == x.o)).Select(x => x.i);

    private static TendencyContext Context(string zone, params int[] values) =>
        new(PitchZoneId.Create(zone).Value, values.Length == 0 ? BatterZoneTendency.Neutral : BatterZoneTendency.Create(values, ZoneReferenceDistribution.UniformV1).Value);

    private static string ConditionFor(string stage, string zone) =>
        stage == StageIds.SwingResult ? zone : ConditionKeys.BattedBallGroup(PitchZoneId.Create(zone).Value).Value;

    private static readonly string[] InnerZones = PitchZoneModel.V1.Zones.Where(z => z.Region == PitchZoneRegion.Inner).Select(z => z.Id.Value).ToArray();

    // ---- neutrality and exact sums ----

    [Fact]
    public void With_no_modifier_every_condition_returns_exactly_its_baseline()
    {
        var count = 0;
        foreach (var (stage, condition) in Everything())
        {
            var result = Run(stage.Id, condition.Key);
            Assert.Equal(Profile.Baseline(stage.Id, condition.Key).Value, result.Distribution);
            Assert.Equal(result.Trace.Baseline, result.Trace.Final);
            Assert.Empty(result.Trace.Modifiers);
            Assert.All(result.Trace.CombinedFactorPpm, f => Assert.Equal(D, f));
            Assert.All(result.Trace.CombinedCapped, c => Assert.False(c));
            count++;
        }

        Assert.Equal(3 + 17 + 17 + 34 + 17 + 3 + 10 + 4, count);
    }

    [Fact]
    public void Supplying_every_slot_at_its_neutral_input_changes_nothing()
    {
        foreach (var (stage, condition) in Everything())
        {
            var neutral = Run(stage.Id, condition.Key, AllNeutral(stage.Id));
            Assert.Equal(Profile.Baseline(stage.Id, condition.Key).Value, neutral.Distribution);
            Assert.All(neutral.Trace.Modifiers, m => Assert.Equal(D, m.FactorPpm));
        }
    }

    [Fact]
    public void Neutral_tendency_context_changes_nothing_for_every_inner_zone()
    {
        foreach (var stage in new[] { StageIds.SwingResult, StageIds.BattedBall })
            foreach (var zone in InnerZones)
            {
                var plain = Run(stage, ConditionFor(stage, zone));
                var withNeutral = Run(stage, ConditionFor(stage, zone), null, Context(zone));
                Assert.Equal(plain.Distribution, withNeutral.Distribution);
                Assert.Equal(0, withNeutral.Trace.GuardShiftPpm);
                Assert.Equal(0, withNeutral.Trace.TendencyShiftPpm);
            }
    }

    [Fact]
    public void Random_inputs_always_give_an_exactly_normalized_valid_distribution_that_keeps_every_possible_outcome()
    {
        var random = new Random(7_001); // seeds only the test's input generation
        var slots = Model.Slots.ToList();
        for (var round = 0; round < 6000; round++)
        {
            var (stage, condition) = Everything().ElementAt(random.Next(Everything().Count()));
            var inputs = new List<ModifierInput>();
            foreach (var slot in slots.Where(s => s.Stage == stage.Id && !s.Reserved && random.Next(3) != 0))
                inputs.Add(slot.Kind == ModifierKind.Table
                    ? ModifierInput.Choice(slot.Id, slot.TableOrder[random.Next(slot.TableOrder.Count)])
                    : ModifierInput.Linear(slot.Id, random.Next(slot.InputMin, slot.InputMax + 1)));
            TendencyContext? tendency = null;
            if ((stage.Id == StageIds.SwingResult || stage.Id == StageIds.BattedBall) && random.Next(2) == 0)
            {
                var zone = InnerZones[random.Next(InnerZones.Length)];
                var candidate = ConditionFor(stage.Id, zone);
                if (candidate == condition.Key)
                    tendency = Context(zone);
            }

            var result = Run(stage.Id, condition.Key, inputs, tendency);
            var baseline = Profile.Baseline(stage.Id, condition.Key).Value;

            Assert.Equal(D, result.Distribution.Ppm.Sum());
            for (var i = 0; i < baseline.Count; i++)
            {
                Assert.InRange(result.Distribution.Ppm[i], 0, D);
                Assert.Equal(baseline.Ppm[i] > 0, result.Distribution.Ppm[i] > 0);
            }
        }
    }

    [Fact]
    public void Zero_and_certain_baselines_stay_exactly_zero_and_certain_under_any_modifier()
    {
        foreach (var zone in PitchZoneModel.V1.Zones)
        {
            var key = $"automated:{zone.Id}";
            var result = Run(StageIds.CalledPitch, key, [ModifierInput.Linear("catcher.framing", Profile.Modifier("catcher.framing").Value.Neutral)]);
            Assert.Equal(zone.Region == PitchZoneRegion.Inner ? [0, D] : [D, 0], result.Distribution.Ppm);
        }

        Assert.Equal(0, Run(StageIds.DefenseResult, "ground_weak", [ModifierInput.Linear("defense.fielding", 100)]).Distribution.Of("home_run").Value);
    }

    [Fact]
    public void Joint_path_mass_through_the_batter_response_stages_is_exactly_one()
    {
        // Each stage keeps its own denominator; the product of conditional stage probabilities over every mutually exclusive path must add up to exactly D^4 / D^4.
        foreach (var zone in PitchZoneModel.V1.Zones)
            foreach (var mode in new[] { ConditionKeys.Automated, ConditionKeys.NonAutomated })
            {
                var z = zone.Id.Value;
                var decision = Run(StageIds.BatterDecision, z).Distribution;
                var called = Run(StageIds.CalledPitch, $"{mode}:{z}").Distribution;
                var swing = Run(StageIds.SwingResult, z).Distribution;
                var batted = Run(StageIds.BattedBall, ConditionKeys.BattedBallGroup(zone.Id).Value).Distribution;

                BigInteger total = 0;
                BigInteger Leaf(BigInteger pathMass, int depth) => pathMass * BigInteger.Pow(D, 4 - depth);
                for (var i = 0; i < decision.Count; i++)
                {
                    BigInteger p1 = decision.Ppm[i];
                    switch (decision.Outcomes[i])
                    {
                        case "hit_by_pitch": total += Leaf(p1, 1); break;
                        case "take":
                            for (var c = 0; c < called.Count; c++)
                                total += Leaf(p1 * called.Ppm[c], 2);
                            break;
                        default:
                            for (var s = 0; s < swing.Count; s++)
                            {
                                BigInteger p2 = p1 * swing.Ppm[s];
                                if (swing.Outcomes[s] != "in_play") { total += Leaf(p2, 2); continue; }
                                for (var b = 0; b < batted.Count; b++)
                                {
                                    var defence = Run(StageIds.DefenseResult, batted.Outcomes[b]).Distribution;
                                    for (var r = 0; r < defence.Count; r++)
                                        total += Leaf(p2 * batted.Ppm[b] * defence.Ppm[r], 4);
                                }
                            }
                            break;
                    }
                }

                Assert.Equal(BigInteger.Pow(D, 4), total);
            }
    }

    // ---- modifier input validation ----

    [Fact]
    public void Bad_modifier_input_is_rejected_with_a_stable_code()
    {
        Assert.Equal("Modifier.slot_unknown", Fail(StageIds.SwingResult, "in.r1c1", [ModifierInput.Linear("batter.charisma", 50)]).Code);
        Assert.Equal("Modifier.input_invalid", Fail(StageIds.SwingResult, "in.r1c1", [new ModifierInput(null!, 5)]).Code);
        Assert.Equal("Modifier.slot_stage_mismatch", Fail(StageIds.SwingResult, "in.r1c1", [ModifierInput.Linear("batter.power", 50)]).Code);
        Assert.Equal("Modifier.input_out_of_range", Fail(StageIds.SwingResult, "in.r1c1", [ModifierInput.Linear("batter.contact", -1)]).Code);
        Assert.Equal("Modifier.input_out_of_range", Fail(StageIds.SwingResult, "in.r1c1", [ModifierInput.Linear("batter.contact", 101)]).Code);
        Assert.Equal("Modifier.key_unexpected", Fail(StageIds.SwingResult, "in.r1c1", [ModifierInput.Choice("batter.contact", "neutral")]).Code);
        Assert.Equal("Modifier.key_missing", Fail(StageIds.SwingResult, "in.r1c1", [ModifierInput.Linear("matchup.handedness", 3)]).Code);
        Assert.Equal("Modifier.key_unknown", Fail(StageIds.SwingResult, "in.r1c1", [ModifierInput.Choice("matchup.handedness", "lefty_killer")]).Code);
        Assert.Equal("Probability.stage_unknown", Fail("nope", "x").Code);
        Assert.Equal("Probability.condition_unknown", Fail(StageIds.SwingResult, "heart").Code);
        Assert.Equal("Tuning.missing", StageProbability.Compute(null!, StageIds.SwingResult, "in.r1c1").Error!.Value.Code);
    }

    [Fact]
    public void The_same_slot_twice_is_rejected_so_one_effect_cannot_be_applied_twice()
    {
        var twice = new[] { ModifierInput.Linear("batter.power", 80), ModifierInput.Linear("batter.power", 80) };
        Assert.Equal("Modifier.duplicate_input", Fail(StageIds.BattedBall, "heart", twice).Code);
        // the double-application the rule prevents really would be a different (wrong) answer: applying the single factor twice by hand differs from the one-time result
        var once = Run(StageIds.BattedBall, "heart", [ModifierInput.Linear("batter.power", 80)]).Distribution.Of("fly_hard").Value;
        var factor = Profile.Modifier("batter.power").Value;
        var doubled = Profile.Baseline(StageIds.BattedBall, "heart").Value;
        var hardFactor = D + (80 - factor.Neutral) * factor.SlopePpmPerUnit;
        var weights = doubled.Outcomes.Select((o, i) => (long)doubled.Ppm[i] * (o.EndsWith("_hard", StringComparison.Ordinal) ? (long)hardFactor * hardFactor / D : D)).ToArray();
        Assert.NotEqual(once, ProbabilityScale.Normalize(weights).Value[Array.IndexOf(doubled.Outcomes.ToArray(), "fly_hard")]);
    }

    [Fact]
    public void Reserved_future_effects_accept_only_the_neutral_input_and_change_nothing()
    {
        foreach (var slot in Model.Slots.Where(s => s.Reserved))
        {
            var stage = slot.Stage;
            var condition = Model.Stage(stage)!.Conditions.First(c => TargetIndexes(slot, c).Any()).Key;
            var neutralValue = Profile.Modifier(slot.Id).Value.Neutral;
            var neutral = Run(stage, condition, [ModifierInput.Linear(slot.Id, neutralValue)]);
            Assert.Equal(Profile.Baseline(stage, condition).Value, neutral.Distribution);

            var other = neutralValue == slot.InputMax ? slot.InputMin : neutralValue + 1;
            Assert.Equal("Modifier.reserved_not_active", Fail(stage, condition, [ModifierInput.Linear(slot.Id, other)]).Code);
            Assert.Equal("Modifier.reserved_not_active", Fail(stage, condition, [ModifierInput.Linear(slot.Id, slot.InputMax == neutralValue ? slot.InputMin : slot.InputMax)]).Code);
        }

        // the park factor really is a permille: 1000 is the only legal value now, and a C46 bound violation is an out-of-range input before it is a reserved one
        Assert.Equal("Modifier.input_out_of_range", Fail(StageIds.DefenseResult, "fly_hard", [ModifierInput.Linear("park.home_runs", 0)]).Code);
        Assert.Equal("Modifier.input_out_of_range", Fail(StageIds.DefenseResult, "fly_hard", [ModifierInput.Linear("park.home_runs", 3001)]).Code);
        Assert.Equal("Modifier.reserved_not_active", Fail(StageIds.DefenseResult, "fly_hard", [ModifierInput.Linear("park.home_runs", 1100)]).Code);
    }

    [Fact]
    public void Input_order_does_not_matter_and_repeated_runs_are_identical()
    {
        var a = new[] { ModifierInput.Linear("batter.contact", 80), ModifierInput.Linear("pitcher.stuff", 30), ModifierInput.Choice("matchup.handedness", "same_hand") };
        var forward = Run(StageIds.SwingResult, "in.r1c3", a);
        var backward = Run(StageIds.SwingResult, "in.r1c3", a.Reverse().ToList());
        Assert.Equal(forward.Distribution, backward.Distribution);
        Assert.Equal(forward.Trace.Modifiers.Select(m => m.Slot), backward.Trace.Modifiers.Select(m => m.Slot));
        Assert.Equal(forward.Distribution, Run(StageIds.SwingResult, "in.r1c3", a).Distribution);
        Assert.Equal(["pitcher.stuff", "batter.contact", "matchup.handedness"], forward.Trace.Modifiers.Select(m => m.Slot)); // the model's fixed application order
    }

    // ---- monotonicity ----

    [Fact]
    public void Every_ability_slot_moves_its_target_outcomes_one_way_across_the_whole_input_range_in_every_condition()
    {
        foreach (var slot in Model.Slots.Where(s => !s.Reserved && s.Kind == ModifierKind.Linear))
        {
            var stage = Model.Stage(slot.Stage)!;
            foreach (var condition in stage.Conditions)
            {
                var targets = TargetIndexes(slot, condition).ToHashSet();
                if (targets.Count == 0 || (slot.Scope == ZoneScope.Outer && condition.Key.StartsWith("in.", StringComparison.Ordinal)))
                    continue;
                var previous = Run(slot.Stage, condition.Key, [ModifierInput.Linear(slot.Id, slot.InputMin)]).Distribution.Ppm.ToArray();
                for (var value = slot.InputMin + 1; value <= slot.InputMax; value++)
                {
                    var current = Run(slot.Stage, condition.Key, [ModifierInput.Linear(slot.Id, value)]).Distribution.Ppm.ToArray();
                    for (var i = 0; i < current.Length; i++)
                    {
                        var targetMovesUp = slot.HigherInputRaisesFactor;
                        if (targets.Contains(i))
                            Assert.True(targetMovesUp ? current[i] >= previous[i] : current[i] <= previous[i], $"{slot.Id} {condition.Key}[{i}] at {value}: {previous[i]} -> {current[i]}");
                        else
                            Assert.True(targetMovesUp ? current[i] <= previous[i] : current[i] >= previous[i], $"{slot.Id} {condition.Key}[{i}] (other) at {value}: {previous[i]} -> {current[i]}");
                    }

                    previous = current;
                }

                var low = Run(slot.Stage, condition.Key, [ModifierInput.Linear(slot.Id, slot.InputMin)]).Distribution;
                var high = Run(slot.Stage, condition.Key, [ModifierInput.Linear(slot.Id, slot.InputMax)]).Distribution;
                var lowMass = targets.Sum(i => low.Ppm[i]);
                var highMass = targets.Sum(i => high.Ppm[i]);
                var baselineHasMass = targets.Any(i => Profile.Baseline(slot.Stage, condition.Key).Value.Ppm[i] > 0);
                if (baselineHasMass && Profile.Modifier(slot.Id).Value.SlopePpmPerUnit != 0)
                    Assert.True(slot.HigherInputRaisesFactor ? highMass > lowMass : highMass < lowMass, $"{slot.Id} {condition.Key} has no effect across its whole range");
            }
        }
    }

    [Fact]
    public void Table_slots_order_their_target_outcome_by_the_modelled_order()
    {
        foreach (var slot in Model.Slots.Where(s => !s.Reserved && s.Kind == ModifierKind.Table))
        {
            var stage = Model.Stage(slot.Stage)!;
            foreach (var condition in stage.Conditions)
            {
                var targets = TargetIndexes(slot, condition).ToList();
                if (targets.Count == 0)
                    continue;
                var masses = slot.TableOrder.Select(key => targets.Sum(i => Run(slot.Stage, condition.Key, [ModifierInput.Choice(slot.Id, key)]).Distribution.Ppm[i])).ToList();
                for (var k = 1; k < masses.Count; k++)
                    Assert.True(masses[k] >= masses[k - 1], $"{slot.Id} {condition.Key}: {string.Join(",", masses)}");
                Assert.True(masses[^1] > masses[0] || Profile.Baseline(slot.Stage, condition.Key).Value.Ppm[targets[0]] == 0);
            }
        }
    }

    [Fact]
    public void A_better_pitcher_input_never_helps_the_batter_and_a_better_batter_input_never_helps_the_pitcher()
    {
        int Miss(params ModifierInput[] inputs) => Run(StageIds.SwingResult, "in.r2c2", inputs).Distribution.Of("miss").Value;
        Assert.True(Miss(ModifierInput.Linear("pitcher.stuff", 90)) > Miss(ModifierInput.Linear("pitcher.stuff", 10)));
        Assert.True(Miss(ModifierInput.Linear("batter.contact", 90)) < Miss(ModifierInput.Linear("batter.contact", 10)));
        Assert.True(Miss(ModifierInput.Choice("matchup.handedness", "same_hand")) > Miss(ModifierInput.Choice("matchup.handedness", "opposite_hand")));
        int Hard(params ModifierInput[] inputs) => Run(StageIds.BattedBall, "heart", inputs).Distribution.Ppm.Where((_, i) => Model.Stage(StageIds.BattedBall)!.Find("heart")!.Outcomes[i].EndsWith("_hard", StringComparison.Ordinal)).Sum();
        Assert.True(Hard(ModifierInput.Linear("batter.power", 90)) > Hard(ModifierInput.Linear("batter.power", 10)));
        Assert.True(Hard(ModifierInput.Linear("pitcher.velocity", 90)) < Hard(ModifierInput.Linear("pitcher.velocity", 10)));
        int Out(params ModifierInput[] inputs) => Run(StageIds.DefenseResult, "line_medium", inputs).Distribution.Of("out").Value;
        Assert.True(Out(ModifierInput.Linear("defense.fielding", 90)) > Out(ModifierInput.Linear("defense.fielding", 10)));
        int OnTarget(params ModifierInput[] inputs) => Run(StageIds.ActualLocation, "in.r1c1", inputs).Distribution.Of("in.r1c1").Value;
        Assert.True(OnTarget(ModifierInput.Linear("pitcher.control", 90)) > OnTarget(ModifierInput.Linear("pitcher.control", 10)));
        int Extra(params ModifierInput[] inputs) => Run(StageIds.Advancement, "single_runner_on_first", inputs).Distribution.Of("advance_extra").Value;
        Assert.True(Extra(ModifierInput.Linear("runner.speed", 90)) > Extra(ModifierInput.Linear("runner.speed", 10)));
        int Thrown(params ModifierInput[] inputs) => Run(StageIds.Advancement, "single_runner_on_first", inputs).Distribution.Of("out_on_bases").Value;
        Assert.True(Thrown(ModifierInput.Linear("defense.arm", 90)) > Thrown(ModifierInput.Linear("defense.arm", 10)));
        int Swing(string zone, params ModifierInput[] inputs) => Run(StageIds.BatterDecision, zone, inputs).Distribution.Of("swing").Value;
        Assert.True(Swing("out.low", ModifierInput.Choice("strategy.approach", "aggressive")) > Swing("out.low", ModifierInput.Choice("strategy.approach", "patient")));
        Assert.True(Swing("in.r2c2", ModifierInput.Choice("situation.count_pressure", "two_strikes")) > Swing("in.r2c2", ModifierInput.Choice("situation.count_pressure", "three_balls")));
    }

    [Fact]
    public void Eye_acts_only_outside_the_strike_zone_and_is_listed_in_the_trace_as_not_applied_inside()
    {
        var inner = Run(StageIds.BatterDecision, "in.r1c1", [ModifierInput.Linear("batter.eye", 100)]);
        Assert.Equal(Profile.Baseline(StageIds.BatterDecision, "in.r1c1").Value, inner.Distribution);
        Assert.False(Assert.Single(inner.Trace.Modifiers).Applied);

        var outer = Run(StageIds.BatterDecision, "out.low", [ModifierInput.Linear("batter.eye", 100)]);
        Assert.True(outer.Distribution.Of("swing").Value < Profile.Baseline(StageIds.BatterDecision, "out.low").Value.Of("swing").Value);
        Assert.True(Assert.Single(outer.Trace.Modifiers).Applied);
        Assert.Equal(Profile.Baseline(StageIds.BatterDecision, "out.low").Value.Of("hit_by_pitch").Value == 0, outer.Distribution.Of("hit_by_pitch").Value == 0);
    }

    // ---- caps ----

    [Fact]
    public void Each_slot_factor_stays_inside_its_own_cap_at_both_ends_of_its_range()
    {
        foreach (var slot in Model.Slots.Where(s => !s.Reserved && s.Kind == ModifierKind.Linear))
        {
            var tuning = Profile.Modifier(slot.Id).Value;
            var condition = Model.Stage(slot.Stage)!.Conditions.First(c => TargetIndexes(slot, c).Any() && !(slot.Scope == ZoneScope.Outer && c.Key.StartsWith("in.", StringComparison.Ordinal))).Key;
            foreach (var value in new[] { slot.InputMin, slot.InputMax, tuning.Neutral })
            {
                var factor = Assert.Single(Run(slot.Stage, condition, [ModifierInput.Linear(slot.Id, value)]).Trace.Modifiers).FactorPpm;
                Assert.InRange(factor, tuning.MinPpm, tuning.MaxPpm);
            }
        }

        foreach (var slot in Model.Slots.Where(s => !s.Reserved && s.Kind == ModifierKind.Table))
        {
            var tuning = Profile.Modifier(slot.Id).Value;
            var condition = Model.Stage(slot.Stage)!.Conditions[0].Key;
            foreach (var key in slot.TableOrder)
                Assert.InRange(Assert.Single(Run(slot.Stage, condition, [ModifierInput.Choice(slot.Id, key)]).Trace.Modifiers).FactorPpm, tuning.MinPpm, tuning.MaxPpm);
        }
    }

    [Fact]
    public void The_combined_cap_clips_the_product_of_several_modifiers_and_the_trace_says_so()
    {
        var stack = new[]
        {
            ModifierInput.Linear("batter.eye", 0), ModifierInput.Choice("strategy.approach", "aggressive"), ModifierInput.Choice("situation.count_pressure", "two_strikes"),
        };
        var high = Run(StageIds.BatterDecision, "out.low", stack);
        var swingIndex = high.Trace.Outcomes.ToList().IndexOf("swing");
        Assert.Equal(Profile.CombinedMaxPpm, high.Trace.CombinedFactorPpm[swingIndex]); // 1.075 x 1.10 x 1.10 = 1.3 > the 1.25 cap
        Assert.True(high.Trace.CombinedCapped[swingIndex]);
        Assert.False(high.Trace.CombinedCapped[high.Trace.Outcomes.ToList().IndexOf("take")]);

        var lowStack = new[]
        {
            ModifierInput.Linear("batter.eye", 100), ModifierInput.Choice("strategy.approach", "patient"), ModifierInput.Choice("situation.count_pressure", "three_balls"),
        };
        var low = Run(StageIds.BatterDecision, "out.low", lowStack);
        Assert.Equal(Profile.CombinedMinPpm, low.Trace.CombinedFactorPpm[swingIndex]); // 0.925 x 0.88 x 0.88 = 0.716 < the 0.80 cap
        Assert.True(low.Trace.CombinedCapped[swingIndex]);

        // clipping stops the effect: a deeper eye value than the one that already reaches the cap moves nothing further
        var halfStack = stack.Skip(1).Append(ModifierInput.Linear("batter.eye", 25)).ToArray();
        var deeper = stack.Skip(1).Append(ModifierInput.Linear("batter.eye", 0)).ToArray();
        Assert.Equal(Run(StageIds.BatterDecision, "out.low", halfStack).Distribution.Of("swing").Value, Run(StageIds.BatterDecision, "out.low", deeper).Distribution.Of("swing").Value);
    }

    [Fact]
    public void The_most_extreme_legal_inputs_in_every_stage_still_give_valid_distributions_inside_the_combined_cap()
    {
        foreach (var stage in Model.Stages)
        {
            foreach (var extreme in new[] { true, false })
            {
                var inputs = Model.Slots.Where(s => s.Stage == stage.Id && !s.Reserved).Select(s => s.Kind == ModifierKind.Table
                    ? ModifierInput.Choice(s.Id, s.TableOrder[extreme ? ^1 : 0])
                    : ModifierInput.Linear(s.Id, s.HigherInputRaisesFactor == extreme ? s.InputMax : s.InputMin)).ToList();
                foreach (var condition in stage.Conditions)
                {
                    var result = Run(stage.Id, condition.Key, inputs);
                    Assert.Equal(D, result.Distribution.Ppm.Sum());
                    Assert.All(result.Trace.CombinedFactorPpm, f => Assert.InRange(f, Profile.CombinedMinPpm, Profile.CombinedMaxPpm));
                }
            }
        }
    }

    // ---- traceability ----

    [Fact]
    public void The_trace_records_versions_inputs_factors_and_reproduces_the_result()
    {
        var inputs = new[] { ModifierInput.Linear("batter.contact", 70), ModifierInput.Linear("pitcher.stuff", 40), ModifierInput.Choice("matchup.handedness", "same_hand") };
        var result = Run(StageIds.SwingResult, "in.r1c1", inputs);
        var t = result.Trace;

        Assert.Equal("prob-model-v1", t.ModelVersion);
        Assert.Equal("tuning-virtual-proto-v1", t.TuningVersion);
        Assert.Equal(Profile.ContentHash, t.TuningHash);
        Assert.Equal("pzl-zones-v1", t.ZoneModel);
        Assert.Equal("pzl-ref-uniform-v1", t.ReferenceId);
        Assert.Equal(ProbabilityScale.NormalizationPolicy, t.NormalizationPolicy);
        Assert.Equal((StageIds.SwingResult, "in.r1c1"), (t.Stage, t.Condition));
        Assert.Equal(["miss", "foul", "in_play"], t.Outcomes);
        Assert.Equal(Profile.Baseline(StageIds.SwingResult, "in.r1c1").Value.Ppm, t.Baseline);
        Assert.Equal(["pitcher.stuff:40", "batter.contact:70", "matchup.handedness:same_hand"], t.Modifiers.Select(m => $"{m.Slot}:{m.Input}"));
        Assert.All(t.Modifiers, m => Assert.Equal(["miss"], m.Targets));
        Assert.Equal(D + (40 - 50) * 1_200, t.Modifiers[0].FactorPpm);
        Assert.Equal(D + (70 - 50) * -1_500, t.Modifiers[1].FactorPpm);
        Assert.Equal(1_040_000, t.Modifiers[2].FactorPpm);
        Assert.Equal(t.Final, result.Distribution.Ppm);

        // independent re-derivation from the recorded facts alone
        var weights = t.Baseline.Select((b, i) => (long)b * t.CombinedFactorPpm[i]).ToArray();
        Assert.Equal(t.AfterModifiers, ProbabilityScale.Normalize(weights).Value);
        long miss = D;
        foreach (var m in t.Modifiers)
            miss = ProbabilityScale.MultiplyRatio(miss, m.FactorPpm, D).Value;
        Assert.Equal((int)miss, t.CombinedFactorPpm[0]);
        Assert.Equal(D, t.CombinedFactorPpm[1]);
        Assert.Equal(D, t.CombinedFactorPpm[2]);
    }

    [Fact]
    public void Before_and_after_values_are_both_recorded_for_the_tendency_step()
    {
        var result = Run(StageIds.SwingResult, "in.r1c1", null, Context("in.r1c1", 60, -60, 0, 0, 0, 0, 0, 0, 0));
        Assert.Equal(Profile.ContactCapPpm * 60 / 100, result.Trace.TendencyShiftPpm);
        Assert.NotEqual(result.Trace.AfterModifiers, result.Trace.Final);
        Assert.Equal(result.Trace.AfterModifiers.Sum(), result.Trace.Final.Sum());
        var favBefore = result.Trace.AfterModifiers[1] + result.Trace.AfterModifiers[2];
        var favAfter = result.Trace.Final[1] + result.Trace.Final[2];
        Assert.Equal(result.Trace.TendencyShiftPpm, favAfter - favBefore);
    }

    [Fact]
    public void The_trace_text_form_and_result_types_never_carry_the_tendency_vector()
    {
        var vector = BatterZoneTendency.Create([77, -77, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).Value;
        var result = Run(StageIds.SwingResult, "in.r1c1", null, new TendencyContext(PitchZoneId.Create("in.r1c1").Value, vector));
        Assert.DoesNotContain("77", result.Trace.ToString());
        foreach (var type in new[] { typeof(StageResult), typeof(ProbabilityTrace), typeof(TraceModifier), typeof(Distribution), typeof(TuningProfile) })
            Assert.DoesNotContain(type.GetProperties(), p => p.PropertyType == typeof(BatterZoneTendency) || p.PropertyType == typeof(TendencyContext));
    }

    // ---- tendency: bounds, monotonicity, neutrality ----

    private static int FavouredMass(StageResult r, string stage) =>
        r.Distribution.Ppm.Where((_, i) => Model.TendencyFor(stage)!.Favoured.Contains(r.Distribution.Outcomes[i])).Sum();

    [Fact]
    public void A_tendency_value_moves_the_favourable_mass_by_exactly_value_times_cap_over_100_inside_the_cap()
    {
        foreach (var (stage, cap) in new[] { (StageIds.SwingResult, Profile.ContactCapPpm), (StageIds.BattedBall, Profile.QualityCapPpm) })
            for (var zoneIndex = 0; zoneIndex < InnerZones.Length; zoneIndex++)
            {
                var zone = InnerZones[zoneIndex];
                var neutralMass = FavouredMass(Run(stage, ConditionFor(stage, zone), null, Context(zone)), stage);
                var previous = int.MinValue;
                foreach (var value in new[] { -100, -50, -1, 0, 1, 50, 100 })
                {
                    // a one-element nonzero vector is not neutral; put the opposite amount on the next zone so the vector is average-neutral
                    var values = new int[9];
                    values[zoneIndex] = value;
                    values[(zoneIndex + 1) % 9] = -value;
                    var mass = FavouredMass(Run(stage, ConditionFor(stage, zone), null, Context(zone, values)), stage);
                    Assert.Equal(neutralMass + value * (cap / 100), mass);
                    Assert.True(mass > previous, "the favourable mass must rise with the tendency value");
                    previous = mass;
                }
            }
    }

    [Fact]
    public void The_full_tendency_range_never_exceeds_the_stated_cap_and_never_empties_a_side()
    {
        foreach (var (stage, cap) in new[] { (StageIds.SwingResult, Profile.ContactCapPpm), (StageIds.BattedBall, Profile.QualityCapPpm) })
            foreach (var zone in InnerZones)
            {
                var neutral = Run(stage, ConditionFor(stage, zone), null, Context(zone));
                foreach (var sign in new[] { 100, -100 })
                {
                    var values = new int[9];
                    var at = Array.IndexOf(InnerZones, zone);
                    values[at] = sign;
                    values[(at + 1) % 9] = -sign;
                    var shifted = Run(stage, ConditionFor(stage, zone), null, Context(zone, values));
                    Assert.Equal(cap, Math.Abs(FavouredMass(shifted, stage) - FavouredMass(neutral, stage)));
                    for (var i = 0; i < shifted.Distribution.Count; i++)
                        Assert.Equal(neutral.Distribution.Ppm[i] > 0, shifted.Distribution.Ppm[i] > 0);
                }
            }
    }

    [Fact]
    public void Outer_zones_are_not_adjusted_by_a_tendency()
    {
        var vector = Context("in.r1c1", 100, -100, 0, 0, 0, 0, 0, 0, 0).Tendency;
        foreach (var zone in PitchZoneModel.V1.Zones.Where(z => z.Region == PitchZoneRegion.Outer))
        {
            var context = new TendencyContext(zone.Id, vector);
            Assert.Equal(Run(StageIds.SwingResult, zone.Id.Value).Distribution, Run(StageIds.SwingResult, zone.Id.Value, null, context).Distribution);
            var group = ConditionKeys.BattedBallGroup(zone.Id).Value;
            Assert.Equal(Run(StageIds.BattedBall, group).Distribution, Run(StageIds.BattedBall, group, null, context).Distribution);
        }
    }

    public static IEnumerable<object[]> NeutralVectors() =>
    [
        [new[] { 10, -10, 0, 0, 0, 0, 0, 0, 0 }],
        [new[] { 100, -100, 0, 0, 0, 0, 0, 0, 0 }],
        [new[] { 100, 0, 0, 0, -100, 0, 0, 0, 0 }],
        [new[] { 30, 30, 30, -90, 0, 0, 0, 0, 0 }],
        [new[] { -100, 50, 50, 0, 0, 0, 0, 0, 0 }],
        [new[] { 100, 100, 100, -100, -100, -100, 0, 0, 0 }],
        [new[] { -100, 100, -100, 100, -100, 100, -100, 100, 0 }],
        [new[] { 7, -3, 12, -19, 4, 0, -11, 9, 1 }],
    ];

    [Theory]
    [MemberData(nameof(NeutralVectors))]
    public void An_average_neutral_vector_leaves_the_reference_weighted_stage_mass_exactly_unchanged(int[] values)
    {
        var tendency = BatterZoneTendency.Create(values, ZoneReferenceDistribution.UniformV1).Value; // rejected unless sum(value x weight) = 0
        foreach (var (stage, _) in new[] { (StageIds.SwingResult, 0), (StageIds.BattedBall, 0) })
        {
            long with = 0, without = 0;
            for (var i = 0; i < 9; i++)
            {
                var zone = InnerZones[i];
                var context = new TendencyContext(PitchZoneId.Create(zone).Value, tendency);
                with += (long)ZoneReferenceDistribution.UniformV1.Weights[i] * FavouredMass(Run(stage, ConditionFor(stage, zone), null, context), stage);
                without += (long)ZoneReferenceDistribution.UniformV1.Weights[i] * FavouredMass(Run(stage, ConditionFor(stage, zone), null, Context(zone)), stage);
            }

            Assert.Equal(without, with);
        }
    }

    [Fact]
    public void Neutrality_survives_extreme_ability_inputs_because_the_guard_does_not_depend_on_the_vector()
    {
        var inputs = new[] { ModifierInput.Linear("batter.contact", 100), ModifierInput.Linear("pitcher.stuff", 0), ModifierInput.Choice("matchup.handedness", "opposite_hand") };
        var tendency = BatterZoneTendency.Create([100, -100, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).Value;
        long with = 0, without = 0;
        for (var i = 0; i < 9; i++)
        {
            var zone = InnerZones[i];
            with += FavouredMass(Run(StageIds.SwingResult, zone, inputs, new TendencyContext(PitchZoneId.Create(zone).Value, tendency)), StageIds.SwingResult);
            without += FavouredMass(Run(StageIds.SwingResult, zone, inputs, Context(zone)), StageIds.SwingResult);
        }

        Assert.Equal(without, with);
    }

    [Fact]
    public void Under_the_real_zone_mix_the_league_average_effect_of_a_uniform_neutral_vector_is_bounded_but_not_zero()
    {
        // The vector is neutral only against pzl-ref-uniform-v1 (ADR-005). The actual pitch mix is not uniform, so the average over real pitches is generally NOT zero.
        // This test documents the size of that gap for the shipped virtual mix; it does not claim zero.
        var mix = new long[9];
        var balanced = Profile.Baseline(StageIds.TargetZone, "balanced").Value;
        for (var t = 0; t < balanced.Count; t++)
        {
            var location = Profile.Baseline(StageIds.ActualLocation, balanced.Outcomes[t]).Value;
            for (var i = 0; i < 9; i++)
                mix[i] += (long)balanced.Ppm[t] * location.Ppm[Array.IndexOf(PitchZoneModel.V1.Zones.Select(z => z.Id.Value).ToArray(), InnerZones[i])];
        }

        var tendency = BatterZoneTendency.Create([100, 0, 0, 0, 0, 0, 0, 0, -100], ZoneReferenceDistribution.UniformV1).Value;
        BigInteger gap = 0, total = 0;
        for (var i = 0; i < 9; i++)
        {
            var zone = InnerZones[i];
            var delta = FavouredMass(Run(StageIds.SwingResult, zone, null, new TendencyContext(PitchZoneId.Create(zone).Value, tendency)), StageIds.SwingResult)
                      - FavouredMass(Run(StageIds.SwingResult, zone, null, Context(zone)), StageIds.SwingResult);
            gap += mix[i] * delta;
            total += mix[i];
        }

        Assert.NotEqual(BigInteger.Zero, gap); // the corners (r1c1, r3c3) are pitched equally often here, but the structure shows the contract is reference-relative
        var averageShiftPpm = (long)(gap / total);
        Assert.InRange(Math.Abs(averageShiftPpm), 0, Profile.ContactCapPpm); // always bounded by the cap
    }

    [Fact]
    public void The_window_guard_keeps_a_shift_valid_when_modifiers_push_the_favourable_mass_to_the_edge()
    {
        // A profile with a deliberately large contact cap: the baseline of in.r2c2 still fits the window, but strong contact inputs push it past the top.
        var d = TuningProfileTests.Draft();
        var wide = TuningProfile.Create(d with { Tendency = d.Tendency! with { ContactCapPpm = 140_000 } }).Value;
        var inputs = new[] { ModifierInput.Linear("batter.contact", 100), ModifierInput.Linear("pitcher.stuff", 0), ModifierInput.Choice("matchup.handedness", "opposite_hand") };

        var neutral = Run(StageIds.SwingResult, "in.r2c2", inputs, Context("in.r2c2"), wide);
        Assert.True(neutral.Trace.GuardShiftPpm < 0);
        Assert.Equal(D - 140_000 - wide.TendencyMinMassPpm, FavouredMass(neutral, StageIds.SwingResult));

        var full = Run(StageIds.SwingResult, "in.r2c2", inputs, Context("in.r2c2", 0, 0, 0, 0, 100, -100, 0, 0, 0), wide);
        Assert.Equal(D - wide.TendencyMinMassPpm, FavouredMass(full, StageIds.SwingResult));
        Assert.True(full.Distribution.Of("miss").Value >= wide.TendencyMinMassPpm - 0);
        Assert.Equal(140_000, FavouredMass(full, StageIds.SwingResult) - FavouredMass(neutral, StageIds.SwingResult));
    }

    [Fact]
    public void A_bad_tendency_context_is_rejected()
    {
        Assert.Equal("Tendency.stage_unsupported", Fail(StageIds.DefenseResult, "fly_hard", null, Context("in.r1c1")).Code);
        Assert.Equal("Tendency.stage_unsupported", Fail(StageIds.BatterDecision, "in.r1c1", null, Context("in.r1c1")).Code);
        Assert.Equal("Tendency.context_mismatch", Fail(StageIds.SwingResult, "in.r1c1", null, Context("in.r1c2")).Code);
        Assert.Equal("Tendency.context_mismatch", Fail(StageIds.BattedBall, "heart", null, Context("in.r1c2")).Code);
        Assert.Equal("Tendency.missing", Fail(StageIds.SwingResult, "in.r1c1", null, new TendencyContext(PitchZoneId.Create("in.r1c1").Value, null!)).Code);
        Assert.Equal("PitchZoneId.unknown", Fail(StageIds.SwingResult, "in.r1c1", null, new TendencyContext(default, BatterZoneTendency.Neutral)).Code);
    }

    // ---- called-pitch mode and the zone/class split ----

    [Fact]
    public void Baselines_distinguish_zone_boundary_class_and_called_pitch_mode()
    {
        foreach (var zone in PitchZoneModel.V1.Zones)
        {
            var inner = zone.Region == PitchZoneRegion.Inner;
            var automated = Run(StageIds.CalledPitch, ConditionKeys.CalledPitch(StarPlayer.Domain.Baseball.Game.CalledPitchMode.Automated, zone.Id).Value).Distribution;
            var manual = Run(StageIds.CalledPitch, ConditionKeys.CalledPitch(StarPlayer.Domain.Baseball.Game.CalledPitchMode.NonAutomated, zone.Id).Value).Distribution;
            Assert.Equal(inner ? D : 0, automated.Of("strike").Value);
            var strike = manual.Of("strike").Value;
            if (zone.Boundary == BoundaryClass.Clear)
                Assert.True(inner ? strike >= 950_000 : strike <= 50_000);
            else
                Assert.InRange(strike, 50_000, 950_000);
        }

        // inside the zone Borderline cells are called strikes less often than the centre; outside, borderline zones are called strikes more often than clear ones
        int Strike(string zone) => Run(StageIds.CalledPitch, $"non_automated:{zone}").Distribution.Of("strike").Value;
        Assert.True(Strike("in.r2c2") > Strike("in.r1c2"));
        Assert.True(Strike("in.r1c2") > Strike("in.r1c1"));
        Assert.True(Strike("out.high") > Strike("out.high_in"));
    }

    [Fact]
    public void Framing_cannot_change_a_call_in_either_mode_until_phase_3e_defines_it()
    {
        foreach (var key in new[] { "automated:in.r1c2", "non_automated:in.r1c2", "non_automated:out.low" })
        {
            Assert.Equal("Modifier.reserved_not_active", Fail(StageIds.CalledPitch, key, [ModifierInput.Linear("catcher.framing", 90)]).Code);
            Assert.Equal(Profile.Baseline(StageIds.CalledPitch, key).Value, Run(StageIds.CalledPitch, key, [ModifierInput.Linear("catcher.framing", 50)]).Distribution);
        }
    }

    // ---- counter-examples: wrong implementations are visibly different ----

    [Fact]
    public void A_highest_index_tie_break_or_plain_rounding_would_give_different_numbers()
    {
        static int[] HighestIndexTieBreak(long[] w)
        {
            var sum = w.Sum();
            var floor = w.Select(x => (int)(x * D / sum)).ToArray();
            var remainder = w.Select(x => x * D % sum).ToArray();
            var left = D - floor.Sum();
            foreach (var i in Enumerable.Range(0, w.Length).OrderByDescending(i => remainder[i]).ThenByDescending(i => i).Take(left))
                floor[i]++;
            return floor;
        }

        static int[] PlainRounding(long[] w) => w.Select(x => (int)((x * D * 2 + w.Sum()) / (w.Sum() * 2))).ToArray();

        long[] equal = [1, 1, 1];
        Assert.Equal([333_334, 333_333, 333_333], ProbabilityScale.Normalize(equal).Value);
        Assert.NotEqual(ProbabilityScale.Normalize(equal).Value, HighestIndexTieBreak(equal));
        Assert.NotEqual(D, PlainRounding([1, 1, 1]).Sum()); // naive rounding does not sum to the denominator - the failure the exact rule prevents
    }
}
