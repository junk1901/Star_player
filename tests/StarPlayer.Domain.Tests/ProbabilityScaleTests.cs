using System.Text.RegularExpressions;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;

namespace StarPlayer.Domain.Tests;

/// <summary>Phase 3-B2: the integer probability scale, normalization rule, distributions and the model structure. No tuning data needed.</summary>
public class ProbabilityScaleTests
{
    private const int D = ProbabilityScale.Denominator;

    private static int[] Norm(long[] weights, int total = D)
    {
        var result = ProbabilityScale.Normalize(weights, total);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    // ---- fixed examples of the rounding rule ----

    [Fact]
    public void Leftover_units_go_to_the_largest_remainder_and_ties_to_the_lowest_index()
    {
        Assert.Equal([333_334, 333_333, 333_333], Norm([1, 1, 1]));
        Assert.Equal([333_333, 666_667], Norm([1, 2]));
        Assert.Equal([142_858, 142_857, 142_857, 142_857, 142_857, 142_857, 142_857], Norm([1, 1, 1, 1, 1, 1, 1]));
        // 1/6 -> 166666.67, 1/3 -> 333333.33, 1/2 -> 500000.00: the .67 is the largest remainder and takes the one leftover unit
        Assert.Equal([166_667, 333_333, 500_000], Norm([1, 2, 3]));
    }

    [Fact]
    public void A_zero_weight_is_exactly_zero_and_never_receives_a_leftover_unit()
    {
        Assert.Equal([0, D], Norm([0, 5]));
        Assert.Equal([333_333, 0, 666_667], Norm([1, 0, 2]));
        Assert.Equal([700_000, 0, 0, 300_000], Norm([7, 0, 0, 3]));
    }

    [Fact]
    public void Normalizing_to_a_group_total_keeps_that_total_exactly()
    {
        var group = Norm([30, 20, 50], 123_457);
        Assert.Equal(123_457, group.Sum());
        Assert.Equal([37_037, 24_691, 61_729], group); // 37037.1, 24691.4, 61728.5: the .5 remainder is the largest and takes the leftover unit
    }

    [Fact]
    public void Normalization_is_deterministic_and_scale_invariant()
    {
        long[] weights = [13, 7, 0, 21, 5];
        Assert.Equal(Norm(weights), Norm(weights));
        Assert.Equal(Norm(weights), Norm(weights.Select(w => w * 1_000_003).ToArray()));
    }

    [Fact]
    public void Random_weight_vectors_always_sum_to_the_total_stay_in_range_and_keep_zeros()
    {
        var random = new Random(20261011); // seeds only this test's own input generation; the property must hold for every input
        for (var round = 0; round < 3000; round++)
        {
            var count = random.Next(1, ProbabilityScale.MaxOutcomes + 1);
            var weights = new long[count];
            for (var i = 0; i < count; i++)
                weights[i] = random.Next(0, 4) == 0 ? 0 : random.NextInt64(0, round % 2 == 0 ? 1_000 : ProbabilityScale.MaxWeight + 1);
            if (weights.All(w => w == 0))
                weights[0] = 1;
            var total = random.Next(1, D + 1);

            var result = Norm(weights, total);

            Assert.Equal(total, result.Sum());
            Assert.All(result, p => Assert.InRange(p, 0, total));
            for (var i = 0; i < count; i++)
                if (weights[i] == 0)
                    Assert.Equal(0, result[i]);
        }
    }

    [Fact]
    public void Extreme_weights_cannot_overflow_and_a_negligible_positive_weight_can_round_to_zero()
    {
        var maximum = Enumerable.Repeat(ProbabilityScale.MaxWeight, ProbabilityScale.MaxOutcomes).ToArray();
        Assert.All(Norm(maximum), p => Assert.Equal(D / ProbabilityScale.MaxOutcomes, p));
        // This is why the tuning profile requires every positive baseline to be at least MinPositiveBaselinePpm and bounds the combined factor: rounding alone can erase a tiny weight.
        Assert.Equal([D, 0], Norm([ProbabilityScale.MaxWeight, 1]));
    }

    [Fact]
    public void Impossible_inputs_are_rejected_with_stable_codes()
    {
        Assert.Equal("Probability.weights_count_invalid", ProbabilityScale.Normalize(null).Error!.Value.Code);
        Assert.Equal("Probability.weights_count_invalid", ProbabilityScale.Normalize([]).Error!.Value.Code);
        Assert.Equal("Probability.weights_count_invalid", ProbabilityScale.Normalize(new long[ProbabilityScale.MaxOutcomes + 1]).Error!.Value.Code);
        Assert.Equal("Probability.weights_all_zero", ProbabilityScale.Normalize([0, 0]).Error!.Value.Code);
        Assert.Equal("Probability.weight_out_of_range", ProbabilityScale.Normalize([1, -1]).Error!.Value.Code);
        Assert.Equal("Probability.weight_out_of_range", ProbabilityScale.Normalize([ProbabilityScale.MaxWeight + 1]).Error!.Value.Code);
        Assert.Equal("Probability.total_invalid", ProbabilityScale.Normalize([1], 0).Error!.Value.Code);
        Assert.Equal("Probability.total_invalid", ProbabilityScale.Normalize([1], D + 1).Error!.Value.Code);
    }

    [Fact]
    public void Product_rounding_is_half_up_and_range_checked()
    {
        Assert.Equal(1, ProbabilityScale.MultiplyRatio(1, 1, 2).Value);
        Assert.Equal(0, ProbabilityScale.MultiplyRatio(1, 1, 3).Value);
        Assert.Equal(3, ProbabilityScale.MultiplyRatio(5, 1, 2).Value);
        Assert.Equal(1_100_000, ProbabilityScale.MultiplyRatio(1_000_000, 1_100_000, 1_000_000).Value);
        Assert.Equal("Probability.ratio_operand_invalid", ProbabilityScale.MultiplyRatio(-1, 1, 1).Error!.Value.Code);
        Assert.Equal("Probability.ratio_operand_invalid", ProbabilityScale.MultiplyRatio(1, 1, 0).Error!.Value.Code);
        Assert.Equal("Probability.ratio_overflow", ProbabilityScale.MultiplyRatio(long.MaxValue, long.MaxValue, 1).Error!.Value.Code);
    }

    // ---- distributions ----

    private static Distribution Dist(string[] ids, int[] ppm)
    {
        var made = Distribution.Create(ids, ppm);
        Assert.True(made.IsSuccess, made.Error?.Message);
        return made.Value;
    }

    [Fact]
    public void A_distribution_must_sum_to_exactly_the_denominator()
    {
        Assert.True(Distribution.Create(["a", "b"], [400_000, 600_000]).IsSuccess);
        Assert.True(Distribution.Create(["only"], [D]).IsSuccess);
        Assert.Equal("Distribution.sum_invalid", Distribution.Create(["a", "b"], [400_000, 599_999]).Error!.Value.Code);
        Assert.Equal("Distribution.sum_invalid", Distribution.Create(["a", "b"], [400_000, 600_001]).Error!.Value.Code);
        Assert.Equal("Distribution.probability_out_of_range", Distribution.Create(["a", "b"], [-1, D + 1]).Error!.Value.Code);
        Assert.Equal("Distribution.probability_out_of_range", Distribution.Create(["a"], [D + 1]).Error!.Value.Code);
        Assert.Equal("Distribution.outcome_duplicate", Distribution.Create(["a", "a"], [500_000, 500_000]).Error!.Value.Code);
        Assert.Equal("Distribution.outcome_id_invalid", Distribution.Create(["A", "b"], [500_000, 500_000]).Error!.Value.Code);
        Assert.Equal("Distribution.outcome_id_invalid", Distribution.Create(["", "b"], [500_000, 500_000]).Error!.Value.Code);
        Assert.Equal("Distribution.shape_invalid", Distribution.Create(["a", "b"], [D]).Error!.Value.Code);
        Assert.Equal("Distribution.shape_invalid", Distribution.Create(null, null).Error!.Value.Code);
        Assert.Equal("Distribution.count_invalid", Distribution.Create([], []).Error!.Value.Code);
        var tooMany = Enumerable.Range(0, ProbabilityScale.MaxOutcomes + 1).Select(i => $"o{i}").ToArray();
        Assert.Equal("Distribution.count_invalid", Distribution.Create(tooMany, new int[tooMany.Length]).Error!.Value.Code);
    }

    [Fact]
    public void A_distribution_does_not_alias_the_callers_arrays()
    {
        string[] ids = ["a", "b"];
        int[] ppm = [400_000, 600_000];
        var d = Dist(ids, ppm);
        ppm[0] = 1;
        ids[0] = "z";
        Assert.Equal(400_000, d.Of("a").Value);
        Assert.Equal("Distribution.outcome_unknown", d.Of("z").Error!.Value.Code);
    }

    [Fact]
    public void Select_gives_each_outcome_exactly_its_ppm_of_the_rolls_and_never_picks_a_zero_outcome()
    {
        foreach (var d in new[]
        {
            Dist(["a", "zero", "c"], [250_000, 0, 750_000]),
            Dist(["x"], [D]),
            Dist(["a", "b", "c", "d"], [1, 0, 999_998, 1]),
        })
        {
            var seen = new int[d.Count];
            for (uint roll = 0; roll < ProbabilityScale.RollBound; roll++)
                seen[d.SelectIndex(roll).Value]++;
            Assert.Equal(d.Ppm, seen);
        }
    }

    [Fact]
    public void Select_boundaries_are_exact()
    {
        var d = Dist(["a", "zero", "c"], [250_000, 0, 750_000]);
        Assert.Equal("a", d.Select(0).Value);
        Assert.Equal("a", d.Select(249_999).Value);
        Assert.Equal("c", d.Select(250_000).Value);
        Assert.Equal("c", d.Select(999_999).Value);
        Assert.Equal("Distribution.roll_out_of_range", d.Select(ProbabilityScale.RollBound).Error!.Value.Code);
        Assert.Equal("Distribution.roll_out_of_range", d.Select(uint.MaxValue).Error!.Value.Code);
    }

    // ---- model structure and versions ----

    [Fact]
    public void Unknown_model_and_tuning_versions_are_rejected_never_defaulted()
    {
        Assert.True(ProbabilityModelVersion.Create("prob-model-v1").IsSuccess);
        Assert.True(ProbabilityModelVersion.Create("prob-model-v2").IsSuccess); // Phase 3-D (owner decision P4): v2 is a known version; this list pinned it as unknown before
        foreach (var bad in new[] { null, "", "prob-model-v5", "PROB-MODEL-V1", " prob-model-v1", "prob-model-v1 " })
            Assert.Equal("ProbabilityModelVersion.unsupported", ProbabilityModelVersion.Create(bad).Error!.Value.Code);
        Assert.Equal("ProbabilityModelVersion.unsupported", ProbabilityModel.Resolve(default).Error!.Value.Code);

        Assert.True(TuningVersion.Create("tuning-virtual-proto-v1").IsSuccess);
        Assert.True(TuningVersion.Create("tuning-virtual-proto-v2").IsSuccess); // Phase 3-D (P4)
        foreach (var bad in new[] { null, "", "tuning-virtual-proto-v5", "tuning-real-kbo-v1" })
            Assert.Equal("TuningVersion.unsupported", TuningVersion.Create(bad).Error!.Value.Code);
    }

    [Fact]
    public void The_stage_order_follows_roadmap_4_1_and_each_stage_has_its_own_condition_family()
    {
        var model = ProbabilityModel.V1;
        Assert.Equal(
            [StageIds.TargetZone, StageIds.ActualLocation, StageIds.BatterDecision, StageIds.CalledPitch, StageIds.SwingResult, StageIds.BattedBall, StageIds.DefenseResult, StageIds.Advancement],
            model.Stages.Select(s => s.Id));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], model.Stages.Select(s => s.Order));
        Assert.Equal([1, 2, 3, 3, 3, 4, 5, 6], model.Stages.Select(s => s.RoadmapStep));
        var zones = PitchZoneModel.V1.Zones.Select(z => z.Id.Value).ToList();
        Assert.Equal(17, zones.Count);
        Assert.Equal(zones, model.Stage(StageIds.ActualLocation)!.Find("in.r2c2")!.Outcomes);
        Assert.Equal(zones, model.Stage(StageIds.TargetZone)!.Find("careful")!.Outcomes);
        Assert.Equal(34, model.Stage(StageIds.CalledPitch)!.Conditions.Count);
        Assert.Equal(["hit_by_pitch", "take", "swing"], model.Stage(StageIds.BatterDecision)!.Find("out.in")!.Outcomes);
        Assert.Equal(["ball", "strike"], model.Stage(StageIds.CalledPitch)!.Find("non_automated:in.r1c1")!.Outcomes);
        Assert.Equal(["miss", "foul", "in_play"], model.Stage(StageIds.SwingResult)!.Find("out.out")!.Outcomes);
        Assert.Equal(["out", "single", "double", "triple", "home_run", "reached_on_error"], model.Stage(StageIds.DefenseResult)!.Find("fly_hard")!.Outcomes);
        Assert.Null(model.Stage(StageIds.BatterDecision)!.Find("heart"));
        Assert.Null(model.Stage("nope"));
    }

    [Fact]
    public void Every_modifier_slot_is_well_formed_and_acts_in_one_stage_on_outcomes_that_exist_there()
    {
        var model = ProbabilityModel.V1;
        Assert.Equal(model.Slots.Count, model.Slots.Select(s => s.Id).Distinct().Count());
        foreach (var slot in model.Slots)
        {
            var stage = model.Stage(slot.Stage);
            Assert.NotNull(stage);
            foreach (var condition in stage!.Conditions)
                foreach (var target in slot.Targets)
                    Assert.True(target == ProbabilityModel.CondTarget ? condition.Outcomes.Contains(condition.Key) : condition.Outcomes.Contains(target),
                        $"{slot.Id} target {target} is not an outcome of {slot.Stage}/{condition.Key}");
            if (slot.Kind == ModifierKind.Table)
                Assert.Contains("neutral", slot.TableOrder);
            Assert.True(slot.Scope == ZoneScope.All || slot.Stage == StageIds.BatterDecision, $"{slot.Id}: a zone scope is only defined for the zone-keyed batter_decision stage");
        }

        Assert.Equal(
            ["catcher.game_calling", "battery.chemistry", "catcher.framing", "park.home_runs", "park.hits", "park.extra_base_hits"],
            model.Slots.Where(s => s.Reserved).Select(s => s.Id));
    }

    [Fact]
    public void The_slot_catalog_of_prob_model_v1_is_pinned()
    {
        // Any change to which outcomes a slot acts on, its stage, kind, scope, direction or reserved status is a NEW model version (ADR-006), so it must be a deliberate edit of this table.
        string[] expected =
        [
            "pitcher.control|actual_location|@condition|Linear|All|0..100|up|live",
            "catcher.game_calling|actual_location|@condition|Linear|All|0..100|up|reserved",
            "battery.chemistry|actual_location|@condition|Linear|All|0..100|up|reserved",
            "batter.eye|batter_decision|swing|Linear|Outer|0..100|down|live",
            "strategy.approach|batter_decision|swing|Table|All|patient,neutral,aggressive|up|live",
            "situation.count_pressure|batter_decision|swing|Table|All|three_balls,neutral,two_strikes|up|live",
            "catcher.framing|called_pitch|strike|Linear|All|0..100|up|reserved",
            "pitcher.stuff|swing_result|miss|Linear|All|0..100|up|live",
            "batter.contact|swing_result|miss|Linear|All|0..100|down|live",
            "matchup.handedness|swing_result|miss|Table|All|opposite_hand,neutral,same_hand|up|live",
            "pitcher.velocity|batted_ball|ground_hard,line_hard,fly_hard|Linear|All|0..100|down|live",
            "batter.power|batted_ball|ground_hard,line_hard,fly_hard|Linear|All|0..100|up|live",
            "defense.fielding|defense_result|out|Linear|All|0..100|up|live",
            "park.home_runs|defense_result|home_run|Linear|All|1..3000|up|reserved",
            "park.hits|defense_result|single|Linear|All|1..3000|up|reserved",
            "park.extra_base_hits|defense_result|double,triple|Linear|All|1..3000|up|reserved",
            "runner.speed|advancement|advance_extra|Linear|All|0..100|up|live",
            "defense.arm|advancement|out_on_bases|Linear|All|0..100|up|live",
        ];
        var actual = ProbabilityModel.V1.Slots.Select(s =>
            $"{s.Id}|{s.Stage}|{string.Join(",", s.Targets)}|{s.Kind}|{s.Scope}|{(s.Kind == ModifierKind.Table ? string.Join(",", s.TableOrder) : $"{s.InputMin}..{s.InputMax}")}|{(s.HigherInputRaisesFactor ? "up" : "down")}|{(s.Reserved ? "reserved" : "live")}");
        Assert.Equal(expected, actual);
        Assert.Equal(
            [("swing_result", "foul,in_play", true), ("batted_ball", "ground_hard,line_hard,fly_hard", false)],
            ProbabilityModel.V1.TendencyStages.Select(t => (t.Stage, string.Join(",", t.Favoured), t.CapIsContact)));
    }

    [Fact]
    public void Each_input_feeds_exactly_one_slot_so_no_ability_is_applied_twice()
    {
        var ids = ProbabilityModel.V1.Slots.Select(s => s.Id).ToList();
        // the input behind a slot is named by its id; none of the ability inputs is listed twice, and Power/Contact/Control/... each have exactly one slot
        foreach (var ability in new[] { "pitcher.control", "pitcher.stuff", "pitcher.velocity", "batter.eye", "batter.contact", "batter.power", "defense.fielding", "defense.arm", "runner.speed" })
            Assert.Single(ids, id => id == ability);
        // Power acts on batted-ball quality only: no slot lets it touch home_run, whose chance is already conditioned on the batted-ball quality
        Assert.DoesNotContain(ProbabilityModel.V1.Slots, s => s.Id.StartsWith("batter.", StringComparison.Ordinal) && s.Stage == StageIds.DefenseResult);
    }

    [Fact]
    public void The_park_slots_own_disjoint_outcomes_so_no_hit_type_is_counted_twice()
    {
        var owned = ProbabilityModel.V1.Slots.Where(s => s.Id.StartsWith("park.", StringComparison.Ordinal)).SelectMany(s => s.Targets).ToList();
        Assert.Equal(owned.Count, owned.Distinct().Count());
        Assert.Equal(["double", "home_run", "single", "triple"], owned.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void Condition_keys_are_built_in_one_place_and_an_undeclared_call_mode_is_an_error()
    {
        var zone = PitchZoneId.Create("out.high_in").Value;
        Assert.Equal("automated:out.high_in", ConditionKeys.CalledPitch(CalledPitchMode.Automated, zone).Value);
        Assert.Equal("non_automated:out.high_in", ConditionKeys.CalledPitch(CalledPitchMode.NonAutomated, zone).Value);
        Assert.Equal("CalledPitchMode.undeclared", ConditionKeys.CalledPitch(null, zone).Error!.Value.Code);
        Assert.Equal("CalledPitchMode.invalid", ConditionKeys.CalledPitch((CalledPitchMode)9, zone).Error!.Value.Code);
        Assert.Equal("heart", ConditionKeys.BattedBallGroup(PitchZoneId.Create("in.r2c2").Value).Value);
        Assert.Equal("edge", ConditionKeys.BattedBallGroup(PitchZoneId.Create("in.r1c3").Value).Value);
        Assert.Equal("outside", ConditionKeys.BattedBallGroup(PitchZoneId.Create("out.low").Value).Value);
        Assert.Equal("PitchZoneId.unknown", ConditionKeys.BattedBallGroup(default).Error!.Value.Code);
    }

    // ---- player ratings are read-only inputs of other types, never reachable from the probability model ----

    private static IEnumerable<Type> SignatureTypes(Type type)
    {
        const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
        IEnumerable<Type> Expand(Type t) => t.IsGenericType ? t.GetGenericArguments().SelectMany(Expand).Append(t) : t.HasElementType ? Expand(t.GetElementType()!).Append(t) : [t];
        foreach (var f in type.GetFields(all)) foreach (var x in Expand(f.FieldType)) yield return x;
        foreach (var p in type.GetProperties(all)) foreach (var x in Expand(p.PropertyType)) yield return x;
        foreach (var m in type.GetMethods(all).Cast<System.Reflection.MethodBase>().Concat(type.GetConstructors(all)))
        {
            foreach (var parameter in m.GetParameters()) foreach (var x in Expand(parameter.ParameterType)) yield return x;
            if (m is System.Reflection.MethodInfo info) foreach (var x in Expand(info.ReturnType)) yield return x;
        }
    }

    [Fact]
    public void The_probability_model_cannot_touch_a_players_ratings_or_state()
    {
        var types = typeof(ProbabilityModel).Assembly.GetTypes().Where(t => t.Namespace == "StarPlayer.Domain.Baseball.Probability" && !t.Name.StartsWith("<", StringComparison.Ordinal)).ToList();
        Assert.True(types.Count >= 15);
        foreach (var type in types)
            Assert.DoesNotContain(SignatureTypes(type), t => t.Namespace == "StarPlayer.Domain.Players");
    }

    // ---- the probability code uses no floating point and no non-deterministic input ----

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StarPlayer.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("저장소 루트를 찾을 수 없습니다.");
    }

    [Fact]
    public void Probability_sources_use_no_floating_point_random_or_clock()
    {
        string[] forbidden =
        [
            @"\bdouble\b", @"\bfloat\b", @"\bdecimal\b", @"(?<![.\w])Single\b", @"\bMath\.(Round|Floor|Ceiling|Truncate|Sqrt|Pow|Exp|Log)", @"\bMathF\b",
            @"\bSystem\.Random\b", @"\bnew\s+Random\s*\(", @"\bRandom\.Shared\b", @"\bDateTime", @"\bStopwatch\b", @"\bGuid\b", @"\.GetHashCode\s*\(", @"\bTask\b", @"\bThread\b",
            @"\bGameRandom\b", @"\bRngStream\b", @"\bPcg32\b",
        ];
        var files = Directory.GetFiles(Path.Combine(FindRoot(), "src", "StarPlayer.Domain", "Baseball", "Probability"), "*.cs");
        Assert.True(files.Length >= 4);
        foreach (var file in files)
        {
            var code = Regex.Replace(Regex.Replace(File.ReadAllText(file), @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\r\n]*", "");
            code = Regex.Replace(code, "\"(?:[^\"\\\\]|\\\\.)*\"", "\"\""); // string literals may name the forbidden words in messages
            foreach (var pattern in forbidden)
                Assert.False(Regex.IsMatch(code, pattern), $"{Path.GetFileName(file)} matches forbidden pattern {pattern}");
        }
    }
}
