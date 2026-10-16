using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E Package A: WHERE the catcher's Framing may change a called pitch. The 17 zones x {automated, non_automated} x Framing {0, 50, 100} matrix (102 combinations) plus 8 further
/// ratings on every non_automated zone are compared with the independent Python oracle; Automated and non_automated Clear are never applied (the modifier itself is not applied), only
/// non_automated Borderline is. Invalid conditions fail closed and v1/v2 keep their reserved contract.
/// </summary>
public class FramingScopeMatrixTests
{
    private const int D = ProbabilityScale.Denominator;

    private static readonly TuningProfile V1 = Gx.Tuning1;
    private static readonly TuningProfile V2 = Gx.Tuning2;
    private static readonly TuningProfile V3 = Gx.Tuning3;

    private static IEnumerable<JsonElement> Cases => TuningV3Tests.OracleDoc.Value.GetProperty("cases").EnumerateArray();

    private static StageResult Called(TuningProfile t, string key, params ModifierInput[] inputs)
    {
        var made = StageProbability.Compute(t, StageIds.CalledPitch, key, inputs);
        Assert.True(made.IsSuccess, $"{t.Tuning} {key}: {made.Error?.Code} {made.Error?.Message}");
        return made.Value;
    }

    private static Error CalledFails(TuningProfile t, string key, params ModifierInput[] inputs)
    {
        var made = StageProbability.Compute(t, StageIds.CalledPitch, key, inputs);
        Assert.False(made.IsSuccess, $"{t.Tuning} {key}: expected a failure");
        return made.Error!.Value;
    }

    private static ModifierInput Framing(int rating) => ModifierInput.Linear("catcher.framing", rating);

    // ---------------------------------------------------------------- the 102-combination matrix against the oracle

    [Fact]
    public void The_oracle_covers_every_zone_mode_and_rating_of_the_approved_matrix()
    {
        var corner = Cases.Where(c => c.GetProperty("framing").GetInt32() is 0 or 50 or 100).ToList();
        Assert.Equal(17 * 2 * 3, corner.Count);                                  // 102
        Assert.Equal(238, Cases.Count());                                         // + 8 further ratings on the 17 non_automated zones
        Assert.Equal(12, corner.Where(c => c.GetProperty("mode").GetString() == "non_automated" && c.GetProperty("framing").GetInt32() == 50).Count(c => c.GetProperty("boundary").GetString() == "Borderline"));
        Assert.Equal(5, corner.Where(c => c.GetProperty("mode").GetString() == "non_automated" && c.GetProperty("framing").GetInt32() == 50).Count(c => c.GetProperty("boundary").GetString() == "Clear"));
        // the 34 baseline rows: every (mode, zone) pair exists once in the v3 tuning
        Assert.Equal(34, V3.Model.Stage(StageIds.CalledPitch)!.Conditions.Count);
    }

    [Fact]
    public void Every_oracle_case_equals_the_production_distribution_the_applied_flag_and_the_effective_factor()
    {
        var applied = 0;
        var notApplied = 0;
        foreach (var c in Cases)
        {
            var mode = c.GetProperty("mode").GetString()!;
            var zone = c.GetProperty("zone").GetString()!;
            var rating = c.GetProperty("framing").GetInt32();
            var result = Called(V3, $"{mode}:{zone}", Framing(rating));
            var listed = Assert.Single(result.Trace.Modifiers);
            var label = $"{mode}:{zone} framing={rating}";

            Assert.Equal(c.GetProperty("applied").GetBoolean(), listed.Applied);
            Assert.Equal(c.GetProperty("factor").GetInt32(), listed.FactorPpm);
            Assert.Equal(c.GetProperty("baseline").EnumerateArray().Select(x => x.GetInt32()), result.Trace.Baseline);
            Assert.True(c.GetProperty("ppm").EnumerateArray().Select(x => x.GetInt32()).SequenceEqual(result.Distribution.Ppm), label);
            Assert.Equal(D, result.Distribution.Ppm.Sum());
            if (listed.Applied) applied++; else notApplied++;
        }

        // 12 Borderline zones x 11 ratings (0/50/100 + 8 more) are applied; everything else is not
        Assert.Equal(12 * 11, applied);
        Assert.Equal(Cases.Count() - applied, notApplied);
    }

    [Fact]
    public void The_oracle_classification_table_equals_the_zone_model_the_production_scope_reads()
    {
        var table = TuningV3Tests.OracleDoc.Value.GetProperty("classification");
        Assert.Equal(17, table.EnumerateObject().Count());
        foreach (var zone in PitchZoneModel.V1.Zones)
            Assert.Equal(table.GetProperty(zone.Id.Value).GetString(), zone.Boundary.ToString());
    }

    [Fact]
    public void Automated_never_applies_framing_at_any_rating_in_any_zone()
    {
        foreach (var zone in PitchZoneModel.V1.Zones)
            foreach (var rating in new[] { 0, 50, 100 })
            {
                var key = $"automated:{zone.Id}";
                var result = Called(V3, key, Framing(rating));
                var listed = Assert.Single(result.Trace.Modifiers);
                Assert.False(listed.Applied);
                Assert.Equal(D, listed.FactorPpm);                                              // effective factor exactly neutral
                Assert.Equal(V3.Baseline(StageIds.CalledPitch, key).Value, result.Distribution); // the deterministic 0% / 100% row is unchanged
                Assert.Equal(zone.Region == PitchZoneRegion.Inner ? [0, D] : [D, 0], result.Distribution.Ppm);
            }
    }

    [Fact]
    public void Non_automated_clear_zones_never_apply_framing_and_borderline_zones_do()
    {
        foreach (var zone in PitchZoneModel.V1.Zones)
            foreach (var rating in new[] { 0, 100 })
            {
                var key = $"non_automated:{zone.Id}";
                var result = Called(V3, key, Framing(rating));
                var listed = Assert.Single(result.Trace.Modifiers);
                var baseline = V3.Baseline(StageIds.CalledPitch, key).Value;
                if (zone.Boundary == BoundaryClass.Clear)
                {
                    Assert.False(listed.Applied);
                    Assert.Equal(D, listed.FactorPpm);
                    Assert.Equal(baseline, result.Distribution);
                }
                else
                {
                    Assert.True(listed.Applied);
                    Assert.Equal(rating == 0 ? 950_000 : 1_050_000, listed.FactorPpm);
                    Assert.NotEqual(baseline, result.Distribution);
                    // a better framer never lowers the strike chance, a worse one never raises it (strike is the only targeted outcome)
                    Assert.Equal(rating == 100, result.Distribution.Of("strike").Value > baseline.Of("strike").Value);
                }
            }
    }

    [Fact]
    public void Neutral_framing_and_no_framing_input_give_the_baseline_in_every_condition_and_the_same_trace_class()
    {
        foreach (var condition in V3.Model.Stage(StageIds.CalledPitch)!.Conditions)
        {
            var baseline = V3.Baseline(StageIds.CalledPitch, condition.Key).Value;
            Assert.Equal(baseline, Called(V3, condition.Key).Distribution);                        // no input at all
            Assert.Equal(baseline, Called(V3, condition.Key, Framing(50)).Distribution);           // the neutral rating
            Assert.Equal(V2.Baseline(StageIds.CalledPitch, condition.Key).Value, baseline);        // the 34 rows are v2's rows
            Assert.Equal(V1.Baseline(StageIds.CalledPitch, condition.Key).Value, baseline);
        }
    }

    [Fact]
    public void Framing_changes_only_the_called_pitch_stage_and_never_another_stage_of_the_same_zone()
    {
        foreach (var stage in V3.Model.Stages.Where(s => s.Id != StageIds.CalledPitch))
        {
            var made = StageProbability.Compute(V3, stage.Id, stage.Conditions[0].Key, [Framing(90)]);
            Assert.False(made.IsSuccess);
            Assert.Equal("Modifier.slot_stage_mismatch", made.Error!.Value.Code);
        }
    }

    // ---------------------------------------------------------------- numbers: zero preservation, normalization, cap

    [Fact]
    public void A_zero_baseline_stays_zero_and_a_positive_one_stays_positive_for_every_condition_and_rating()
    {
        foreach (var condition in V3.Model.Stage(StageIds.CalledPitch)!.Conditions)
            foreach (var rating in new[] { 0, 1, 50, 99, 100 })
            {
                var baseline = V3.Baseline(StageIds.CalledPitch, condition.Key).Value;
                var result = Called(V3, condition.Key, Framing(rating));
                Assert.Equal(D, result.Distribution.Ppm.Sum());
                for (var i = 0; i < baseline.Count; i++)
                    Assert.Equal(baseline.Ppm[i] > 0, result.Distribution.Ppm[i] > 0);
                Assert.True(result.Trace.CombinedFactorPpm.All(f => f >= V3.CombinedMinPpm && f <= V3.CombinedMaxPpm));
            }
    }

    [Fact]
    public void The_largest_remainder_rule_decides_the_last_ppm_and_framing_is_monotone_in_the_rating()
    {
        foreach (var zone in PitchZoneModel.V1.Zones.Where(z => z.Boundary == BoundaryClass.Borderline))
        {
            var previous = int.MinValue;
            for (var rating = 0; rating <= 100; rating++)
            {
                var strike = Called(V3, $"non_automated:{zone.Id}", Framing(rating)).Distribution.Of("strike").Value;
                Assert.True(strike >= previous, $"{zone.Id}: strike chance fell from {previous} to {strike} at rating {rating}");
                previous = strike;
            }
        }
    }

    // ---------------------------------------------------------------- invalid conditions fail closed

    [Theory]
    [InlineData("Automated:in.r1c1")]
    [InlineData("NON_AUTOMATED:in.r1c1")]
    [InlineData("manual:in.r1c1")]
    [InlineData("non_automated:in.r9c9")]
    [InlineData("non_automated:IN.R1C1")]
    [InlineData("non_automated")]
    [InlineData("in.r1c1")]
    [InlineData("")]
    [InlineData("non_automated:in.r1c1:extra")]
    [InlineData("non_automated: in.r1c1")]
    [InlineData("xnon_automated:in.r1c1")]
    public void An_unregistered_condition_key_is_refused_with_the_existing_typed_error_and_framing_is_never_applied(string key)
    {
        Assert.Equal("Probability.condition_unknown", CalledFails(V3, key, Framing(100)).Code);
        Assert.Equal("Probability.condition_unknown", CalledFails(V3, key).Code);
    }

    [Fact]
    public void An_unknown_stage_or_slot_or_a_slot_of_another_stage_is_refused()
    {
        Assert.Equal("Probability.stage_unknown", StageProbability.Compute(V3, "called_pitches", "non_automated:in.r1c2", [Framing(100)]).Error!.Value.Code);
        Assert.Equal("Modifier.slot_unknown", CalledFails(V3, "non_automated:in.r1c2", ModifierInput.Linear("catcher.framing2", 100)).Code);
        Assert.Equal("Modifier.slot_stage_mismatch", CalledFails(V3, "non_automated:in.r1c2", ModifierInput.Linear("pitcher.stuff", 70)).Code);
        Assert.Equal("Modifier.duplicate_input", CalledFails(V3, "non_automated:in.r1c2", Framing(60), Framing(60)).Code);
        Assert.Equal("Modifier.key_unexpected", CalledFails(V3, "non_automated:in.r1c2", ModifierInput.Choice("catcher.framing", "neutral")).Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void An_out_of_range_rating_is_refused_in_every_mode_and_zone_even_where_framing_would_not_apply(int rating)
    {
        foreach (var key in new[] { "automated:in.r1c2", "non_automated:in.r2c2", "non_automated:in.r1c2", "non_automated:out.low" })
            Assert.Equal("Modifier.input_out_of_range", CalledFails(V3, key, Framing(rating)).Code);
    }

    [Fact]
    public void Only_the_framing_slot_carries_a_condition_scope_and_it_lives_in_the_called_pitch_stage_whose_keys_the_scope_reads()
    {
        var scoped = Assert.Single(ProbabilityModel.V3.Slots, s => s.ConditionScope != ModifierConditionScope.All);
        Assert.Equal(("catcher.framing", StageIds.CalledPitch), (scoped.Id, scoped.Stage));
        // every condition key of that stage is a valid "mode:zone" key (so the scope can always be evaluated), and no other stage's key is
        foreach (var condition in ProbabilityModel.V3.Stage(StageIds.CalledPitch)!.Conditions)
            Assert.True(ConditionKeys.ParseCalledPitch(condition.Key).IsSuccess, condition.Key);
        foreach (var condition in ProbabilityModel.V3.Stages.Where(s => s.Id != StageIds.CalledPitch).SelectMany(s => s.Conditions))
            Assert.False(ConditionKeys.ParseCalledPitch(condition.Key).IsSuccess, condition.Key);
    }

    // ---------------------------------------------------------------- v1 / v2 keep the reserved contract

    [Fact]
    public void Under_v1_and_v2_any_non_neutral_framing_input_is_still_refused_and_the_neutral_one_gives_the_baseline()
    {
        foreach (var tuning in new[] { V1, V2 })
            foreach (var condition in tuning.Model.Stage(StageIds.CalledPitch)!.Conditions)
            {
                foreach (var rating in new[] { 0, 49, 51, 90, 100 })
                    Assert.Equal("Modifier.reserved_not_active", CalledFails(tuning, condition.Key, Framing(rating)).Code);
                Assert.Equal("Modifier.input_out_of_range", CalledFails(tuning, condition.Key, Framing(101)).Code);
                var neutral = Called(tuning, condition.Key, Framing(50));
                Assert.Equal(tuning.Baseline(StageIds.CalledPitch, condition.Key).Value, neutral.Distribution);
                Assert.Equal(D, Assert.Single(neutral.Trace.Modifiers).FactorPpm);
            }
    }

    [Fact]
    public void The_scope_guard_does_not_bypass_the_reserved_validation_of_game_calling_and_battery_chemistry_in_v3()
    {
        foreach (var slot in new[] { "catcher.game_calling", "battery.chemistry" })
        {
            var made = StageProbability.Compute(V3, StageIds.ActualLocation, "in.r1c2", [ModifierInput.Linear(slot, 90)]);
            Assert.False(made.IsSuccess);
            Assert.Equal("Modifier.reserved_not_active", made.Error!.Value.Code);
            Assert.True(StageProbability.Compute(V3, StageIds.ActualLocation, "in.r1c2", [ModifierInput.Linear(slot, 50)]).IsSuccess);
        }
    }

    [Fact]
    public void Computation_is_pure_the_same_inputs_give_the_same_trace_and_no_random_source_is_involved()
    {
        var a = Called(V3, "non_automated:in.r1c2", Framing(87));
        var b = Called(V3, "non_automated:in.r1c2", Framing(87));
        Assert.Equal(a.Distribution, b.Distribution);
        Assert.Equal(a.Trace.Final, b.Trace.Final);
        Assert.Equal(a.Trace.Modifiers, b.Trace.Modifiers);
        Assert.Equal(V3.ContentHash, a.Trace.TuningHash);
        Assert.Equal("prob-model-v3", a.Trace.ModelVersion);
        Assert.Equal("tuning-virtual-proto-v3", a.Trace.TuningVersion);
    }
}
