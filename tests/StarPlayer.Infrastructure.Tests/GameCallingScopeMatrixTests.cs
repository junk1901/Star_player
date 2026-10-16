using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E (Game Calling v4, GC-A): WHERE the catcher's Game Calling may change the actual location, and how it combines with the pitcher's Control. The 17 aimed zones x {automated, non_automated} x
/// GameCalling {0, 50, 100} x Control {0, 50, 100} matrix (306 combinations) plus 408 further ratings are compared with the independent Python oracle
/// (<c>generate_phase3e_game_calling_oracle.py</c>, which reads only content/tuning/virtual_tuning_v4.json and DERIVES the zone classes itself). The scope is enforced by calling
/// <see cref="StageProbability.Compute"/> directly - no resolver is involved - so a caller cannot bypass it.
/// </summary>
public class GameCallingScopeMatrixTests
{
    private const int D = ProbabilityScale.Denominator;

    private static readonly TuningProfile V1 = Gx.Tuning1;
    private static readonly TuningProfile V2 = Gx.Tuning2;
    private static readonly TuningProfile V3 = Gx.Tuning3;
    private static readonly TuningProfile V4 = Gx.Tuning4;

    internal static readonly Lazy<JsonElement> OracleDoc = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(PitchBranchFixture.Root, "tests", "Fixtures", "phase3e_game_calling_oracle.json"))).RootElement);

    private static IEnumerable<JsonElement> Cases => OracleDoc.Value.GetProperty("cases").EnumerateArray();

    private static ModifierInput Control(int rating) => ModifierInput.Linear("pitcher.control", rating);

    private static ModifierInput Calling(int rating) => ModifierInput.Linear("catcher.game_calling", rating);

    private static StageResult Location(TuningProfile t, string aimed, params ModifierInput[] inputs)
    {
        var made = StageProbability.Compute(t, StageIds.ActualLocation, aimed, inputs);
        Assert.True(made.IsSuccess, $"{t.Tuning} {aimed}: {made.Error?.Code} {made.Error?.Message}");
        return made.Value;
    }

    private static Error LocationFails(TuningProfile t, string aimed, params ModifierInput[] inputs)
    {
        var made = StageProbability.Compute(t, StageIds.ActualLocation, aimed, inputs);
        Assert.False(made.IsSuccess, $"{t.Tuning} {aimed}: expected a failure");
        return made.Error!.Value;
    }

    private static TraceModifier Mod(StageResult r, string slot) => Assert.Single(r.Trace.Modifiers, m => m.Slot == slot);

    private static int TargetIndex(string aimed) => V4.Model.Stage(StageIds.ActualLocation)!.Find(aimed)!.Outcomes.ToList().IndexOf(aimed);

    // ---------------------------------------------------------------- the matrix against the oracle

    [Fact]
    public void The_oracle_covers_every_zone_mode_and_rating_pair_of_the_approved_matrix()
    {
        var corner = Cases.Where(c => c.GetProperty("control").GetInt32() is 0 or 50 or 100 && c.GetProperty("game_calling").GetInt32() is 0 or 50 or 100).ToList();
        Assert.Equal(17 * 2 * 3 * 3, corner.Count);                       // 306
        Assert.Equal(306 + 17 * 3 * 8, Cases.Count());                    // + 8 further game-calling ratings on the 17 zones x 3 controls
        var neutral = corner.Where(c => c.GetProperty("mode").GetString() == "non_automated" && c.GetProperty("control").GetInt32() == 50 && c.GetProperty("game_calling").GetInt32() == 50).ToList();
        Assert.Equal(12, neutral.Count(c => c.GetProperty("boundary").GetString() == "Borderline"));
        Assert.Equal(5, neutral.Count(c => c.GetProperty("boundary").GetString() == "Clear"));
        Assert.Equal(17, V4.Model.Stage(StageIds.ActualLocation)!.Conditions.Count);
    }

    [Fact]
    public void Every_oracle_case_equals_the_production_distribution_the_applied_flag_and_the_factors()
    {
        var applied = 0;
        var notApplied = 0;
        foreach (var c in Cases)
        {
            var zone = c.GetProperty("zone").GetString()!;
            var control = c.GetProperty("control").GetInt32();
            var gc = c.GetProperty("game_calling").GetInt32();
            var result = Location(V4, zone, Control(control), Calling(gc));
            var label = $"{c.GetProperty("mode").GetString()} aimed={zone} control={control} gc={gc}";
            var gcListed = Mod(result, "catcher.game_calling");
            var controlListed = Mod(result, "pitcher.control");

            Assert.Equal(2, result.Trace.Modifiers.Count);
            Assert.Equal(c.GetProperty("applied").GetBoolean(), gcListed.Applied);
            Assert.Equal(c.GetProperty("gc_factor").GetInt32(), gcListed.FactorPpm);
            Assert.True(controlListed.Applied);
            Assert.Equal(c.GetProperty("control_factor").GetInt32(), controlListed.FactorPpm);
            Assert.Equal(c.GetProperty("combined_target").GetInt32(), result.Trace.CombinedFactorPpm[TargetIndex(zone)]);
            Assert.Equal(c.GetProperty("baseline").EnumerateArray().Select(x => x.GetInt32()), result.Trace.Baseline);
            Assert.True(c.GetProperty("ppm").EnumerateArray().Select(x => x.GetInt32()).SequenceEqual(result.Distribution.Ppm), label);
            Assert.Equal(D, result.Distribution.Ppm.Sum());
            Assert.DoesNotContain(true, result.Trace.CombinedCapped);        // the product stays inside the combined cap for every approved rating
            for (var i = 0; i < result.Trace.Baseline.Count; i++)           // a zero stays zero and a positive stays positive
                Assert.Equal(result.Trace.Baseline[i] > 0, result.Distribution.Ppm[i] > 0);
            if (gcListed.Applied) applied++; else notApplied++;
        }

        // 12 Borderline zones x (306/17 + 24) ratings per zone are applied; everything else is not
        Assert.Equal(12 * (2 * 9 + 3 * 8), applied);
        Assert.Equal(Cases.Count() - applied, notApplied);
    }

    [Fact]
    public void The_oracle_classification_table_equals_the_zone_model_the_production_scope_reads()
    {
        var table = OracleDoc.Value.GetProperty("classification");
        Assert.Equal(17, table.EnumerateObject().Count());
        foreach (var zone in PitchZoneModel.V1.Zones)
            Assert.Equal(table.GetProperty(zone.Id.Value).GetString(), zone.Boundary.ToString());
    }

    [Fact]
    public void Automated_and_non_automated_give_the_same_actual_location_row_because_the_scope_reads_the_aimed_zone_only()
    {
        foreach (var zone in PitchZoneModel.V1.Zones)
            foreach (var control in new[] { 0, 50, 100 })
                foreach (var gc in new[] { 0, 50, 100 })
                {
                    var automated = Cases.Single(c => c.GetProperty("mode").GetString() == "automated" && c.GetProperty("zone").GetString() == zone.Id.Value
                        && c.GetProperty("control").GetInt32() == control && c.GetProperty("game_calling").GetInt32() == gc);
                    var manual = Cases.Single(c => c.GetProperty("mode").GetString() == "non_automated" && c.GetProperty("zone").GetString() == zone.Id.Value
                        && c.GetProperty("control").GetInt32() == control && c.GetProperty("game_calling").GetInt32() == gc);
                    Assert.Equal(automated.GetProperty("ppm").GetRawText(), manual.GetProperty("ppm").GetRawText());
                    Assert.Equal(automated.GetProperty("applied").GetBoolean(), manual.GetProperty("applied").GetBoolean());
                }
    }

    // ---------------------------------------------------------------- Clear targets: listed, never applied

    [Fact]
    public void A_clear_target_lists_game_calling_but_never_applies_it_at_any_rating()
    {
        foreach (var zone in PitchZoneModel.V1.Zones.Where(z => z.Boundary == BoundaryClass.Clear))
            foreach (var control in new[] { 0, 50, 100 })
                foreach (var gc in new[] { 0, 1, 25, 49, 50, 51, 75, 99, 100 })
                {
                    var with = Location(V4, zone.Id.Value, Control(control), Calling(gc));
                    var without = Location(V4, zone.Id.Value, Control(control));
                    var listed = Mod(with, "catcher.game_calling");
                    Assert.False(listed.Applied);
                    Assert.Equal(D, listed.FactorPpm);                                          // the effective factor is exactly neutral
                    Assert.Equal(without.Distribution, with.Distribution);                      // identical to a call that never mentioned game calling
                    Assert.True(without.Trace.CombinedFactorPpm.SequenceEqual(with.Trace.CombinedFactorPpm));
                    Assert.Equal(Mod(without, "pitcher.control").FactorPpm, Mod(with, "pitcher.control").FactorPpm);
                }
    }

    [Fact]
    public void A_clear_target_with_neutral_control_equals_the_baseline_row_whatever_the_game_calling_rating()
    {
        foreach (var zone in PitchZoneModel.V1.Zones.Where(z => z.Boundary == BoundaryClass.Clear))
            foreach (var gc in new[] { 0, 100 })
            {
                var result = Location(V4, zone.Id.Value, Control(50), Calling(gc));
                Assert.Equal(V4.Baseline(StageIds.ActualLocation, zone.Id.Value).Value, result.Distribution);
            }
    }

    // ---------------------------------------------------------------- Borderline targets: applied, monotone, bounded

    [Fact]
    public void A_borderline_target_applies_game_calling_and_the_target_share_never_falls_as_the_rating_rises()
    {
        foreach (var zone in PitchZoneModel.V1.Zones.Where(z => z.Boundary == BoundaryClass.Borderline))
            foreach (var control in new[] { 0, 50, 100 })
            {
                var previous = -1;
                foreach (var gc in Enumerable.Range(0, 101))
                {
                    var result = Location(V4, zone.Id.Value, Control(control), Calling(gc));
                    var listed = Mod(result, "catcher.game_calling");
                    Assert.True(listed.Applied);
                    Assert.Equal(1_000_000 + (gc - 50) * 1000, listed.FactorPpm);
                    var share = result.Distribution.Ppm[TargetIndex(zone.Id.Value)];
                    Assert.True(share >= previous, $"{zone.Id} control={control} gc={gc}: {share} < {previous}");
                    previous = share;
                }
            }
    }

    [Fact]
    public void The_aimed_zone_decides_not_the_landing_zone_so_a_borderline_aim_that_may_land_clear_still_applies()
    {
        // in.r1c1 (Borderline) can land on in.r2c2 (Clear) and a Clear aim can land on a Borderline cell: the class that counts is the aimed one only
        var borderlineAim = Location(V4, "in.r1c1", Calling(100));
        Assert.True(Mod(borderlineAim, "catcher.game_calling").Applied);
        Assert.True(borderlineAim.Distribution.Ppm[V4.Model.Stage(StageIds.ActualLocation)!.Find("in.r1c1")!.Outcomes.ToList().IndexOf("in.r2c2")] > 0);
        var clearAim = Location(V4, "in.r2c2", Calling(100));
        Assert.False(Mod(clearAim, "catcher.game_calling").Applied);
        Assert.True(clearAim.Distribution.Ppm[V4.Model.Stage(StageIds.ActualLocation)!.Find("in.r2c2")!.Outcomes.ToList().IndexOf("in.r1c1")] > 0);
    }

    // ---------------------------------------------------------------- Control x Game Calling

    [Theory]
    [InlineData(0, 0, 878_750)]
    [InlineData(0, 100, 971_250)]
    [InlineData(50, 50, 1_000_000)]
    [InlineData(100, 0, 1_021_250)]
    [InlineData(100, 100, 1_128_750)]
    public void The_approved_combined_factors_of_a_borderline_target_are_exact(int control, int gc, int expected)
    {
        var result = Location(V4, "in.r1c1", Control(control), Calling(gc));
        Assert.Equal(expected, result.Trace.CombinedFactorPpm[TargetIndex("in.r1c1")]);
        Assert.Equal(Mod(result, "pitcher.control").FactorPpm, 1_000_000 + (control - 50) * 1500);
        Assert.Equal(Mod(result, "catcher.game_calling").FactorPpm, 1_000_000 + (gc - 50) * 1000);
        // the approved table is also what the oracle recorded
        var row = OracleDoc.Value.GetProperty("combined_factors").EnumerateArray().Single(r => r.GetProperty("control").GetInt32() == control && r.GetProperty("game_calling").GetInt32() == gc);
        Assert.Equal(expected, row.GetProperty("factor").GetInt32());
        Assert.DoesNotContain(true, result.Trace.CombinedCapped);
    }

    [Fact]
    public void Control_alone_is_unchanged_by_the_new_slot_and_the_two_factors_multiply_with_a_single_rounding_step_per_slot()
    {
        foreach (var zone in new[] { "in.r1c1", "out.low", "in.r2c2" })
            foreach (var control in new[] { 0, 30, 50, 77, 100 })
            {
                var v3 = Location(V3, zone, Control(control));
                var v4 = Location(V4, zone, Control(control));
                Assert.Equal(v3.Distribution, v4.Distribution);                               // v4 without game calling = v3
                Assert.True(v3.Trace.CombinedFactorPpm.SequenceEqual(v4.Trace.CombinedFactorPpm));
            }

        // 925,000 * 950,000 / 1,000,000 = 878,750 exactly; and with the intermediate rounding of a non-exact pair (Control 33, GC 37)
        var odd = Location(V4, "in.r1c1", Control(33), Calling(37));
        var controlFactor = 1_000_000 + (33 - 50) * 1500;                                    // 974,500
        var gcFactor = 1_000_000 + (37 - 50) * 1000;                                         // 987,000
        var expected = (int)(((long)controlFactor * gcFactor + 500_000) / 1_000_000);        // one half-up rounding of the running product
        Assert.Equal(expected, odd.Trace.CombinedFactorPpm[TargetIndex("in.r1c1")]);
    }

    [Fact]
    public void The_overlap_of_control_and_game_calling_is_bounded_by_the_combined_cap_and_not_removed()
    {
        // the largest and smallest possible products stay inside the stage-wide cap; the two slots really do multiply the same outcome (the overlap is documented, not hidden)
        var best = Location(V4, "in.r1c1", Control(100), Calling(100)).Trace.CombinedFactorPpm[TargetIndex("in.r1c1")];
        var worst = Location(V4, "in.r1c1", Control(0), Calling(0)).Trace.CombinedFactorPpm[TargetIndex("in.r1c1")];
        Assert.InRange(best, V4.CombinedMinPpm, V4.CombinedMaxPpm);
        Assert.InRange(worst, V4.CombinedMinPpm, V4.CombinedMaxPpm);
        Assert.True(best > 1_075_000 && worst < 925_000);                                    // game calling moves the aimed share beyond what Control alone can
        var m = V4.Model;
        Assert.Equal(m.Slot("pitcher.control")!.Targets, m.Slot("catcher.game_calling")!.Targets);
    }

    // ---------------------------------------------------------------- direct Compute cannot bypass the scope

    [Fact]
    public void Calling_compute_directly_with_a_game_calling_input_for_a_clear_target_still_does_not_apply_it()
    {
        // no resolver, no GameExecution: the scope is a property of the model and Compute enforces it
        var direct = StageProbability.Compute(V4, StageIds.ActualLocation, "out.low_in", [Calling(100)]);
        Assert.True(direct.IsSuccess);
        Assert.False(Assert.Single(direct.Value.Trace.Modifiers).Applied);
        Assert.Equal(V4.Baseline(StageIds.ActualLocation, "out.low_in").Value, direct.Value.Distribution);
    }

    [Fact]
    public void A_game_calling_input_for_any_other_stage_is_rejected_by_compute_itself()
    {
        foreach (var stage in V4.Model.Stages.Where(s => s.Id != StageIds.ActualLocation))
        {
            var condition = stage.Conditions[0].Key;
            var made = StageProbability.Compute(V4, stage.Id, condition, [Calling(80)]);
            Assert.False(made.IsSuccess, stage.Id);
            Assert.Equal("Modifier.slot_stage_mismatch", made.Error!.Value.Code);
        }
    }

    [Fact]
    public void Invalid_conditions_and_inputs_fail_closed_never_as_applies_or_as_neutral()
    {
        Assert.Equal("Probability.condition_unknown", LocationFails(V4, "bogus", Calling(80)).Code);
        Assert.Equal("Probability.condition_unknown", LocationFails(V4, "non_automated:in.r1c1", Calling(80)).Code);
        Assert.Equal("Probability.condition_unknown", LocationFails(V4, "IN.R1C1", Calling(80)).Code);
        Assert.Equal("Probability.condition_unknown", LocationFails(V4, "", Calling(80)).Code);
        Assert.Equal("Modifier.input_out_of_range", LocationFails(V4, "in.r1c1", Calling(-1)).Code);
        Assert.Equal("Modifier.input_out_of_range", LocationFails(V4, "in.r1c1", Calling(101)).Code);
        Assert.Equal("Modifier.input_out_of_range", LocationFails(V4, "in.r2c2", Calling(101)).Code);   // a Clear aim does not excuse an illegal rating
        Assert.Equal("Modifier.duplicate_input", LocationFails(V4, "in.r1c1", Calling(60), Calling(60)).Code);
        Assert.Equal("Modifier.key_unexpected", LocationFails(V4, "in.r1c1", ModifierInput.Choice("catcher.game_calling", "neutral")).Code);
        Assert.Equal("Modifier.reserved_not_active", LocationFails(V4, "in.r1c1", ModifierInput.Linear("battery.chemistry", 80)).Code);
        Assert.Equal("Modifier.slot_unknown", LocationFails(V4, "in.r1c1", ModifierInput.Linear("catcher.game_calling2", 80)).Code);
    }

    [Fact]
    public void Before_v4_game_calling_accepts_only_the_neutral_input_and_changes_nothing()
    {
        foreach (var t in new[] { V1, V2, V3 })
        {
            Assert.Equal("Modifier.reserved_not_active", LocationFails(t, "in.r1c1", Calling(80)).Code);
            Assert.Equal("Modifier.reserved_not_active", LocationFails(t, "in.r2c2", Calling(0)).Code);   // the reserved rule does not depend on the aimed class
            var neutral = Location(t, "in.r1c1", Calling(50));
            Assert.Equal(Location(t, "in.r1c1").Distribution, neutral.Distribution);
            Assert.Equal(D, Mod(neutral, "catcher.game_calling").FactorPpm);
            Assert.Equal(ModifierConditionScope.All, t.Model.Slot("catcher.game_calling")!.ConditionScope);
        }
    }

    // ---------------------------------------------------------------- v3 and v4: neutral behaviour and everything else unchanged

    [Fact]
    public void A_neutral_game_calling_rating_gives_exactly_the_v3_distribution_for_every_zone_and_control()
    {
        foreach (var zone in PitchZoneModel.V1.Zones)
            foreach (var control in new[] { 0, 25, 50, 75, 100 })
            {
                var v3 = Location(V3, zone.Id.Value, Control(control), Calling(50));
                var v4 = Location(V4, zone.Id.Value, Control(control), Calling(50));
                Assert.Equal(v3.Distribution, v4.Distribution);
                Assert.True(v3.Trace.CombinedFactorPpm.SequenceEqual(v4.Trace.CombinedFactorPpm));
                Assert.True(Mod(v4, "catcher.game_calling").Applied == (zone.Boundary == BoundaryClass.Borderline));
                Assert.Equal(D, Mod(v4, "catcher.game_calling").FactorPpm);                  // neutral rating: factor exactly 1,000,000 even where the scope applies
            }
    }

    [Fact]
    public void Every_other_stage_distribution_and_the_framing_scope_are_identical_in_v3_and_v4()
    {
        foreach (var stage in V3.Model.Stages)
            foreach (var condition in stage.Conditions)
            {
                var a = StageProbability.Compute(V3, stage.Id, condition.Key).Value;
                var b = StageProbability.Compute(V4, stage.Id, condition.Key).Value;
                Assert.Equal(a.Distribution, b.Distribution);
            }

        foreach (var zone in PitchZoneModel.V1.Zones)
            foreach (var mode in new[] { "automated", "non_automated" })
                foreach (var framing in new[] { 0, 50, 100 })
                {
                    var key = $"{mode}:{zone.Id}";
                    var a = StageProbability.Compute(V3, StageIds.CalledPitch, key, [ModifierInput.Linear("catcher.framing", framing)]).Value;
                    var b = StageProbability.Compute(V4, StageIds.CalledPitch, key, [ModifierInput.Linear("catcher.framing", framing)]).Value;
                    Assert.Equal(a.Distribution, b.Distribution);
                    Assert.Equal(Mod(a, "catcher.framing").Applied, Mod(b, "catcher.framing").Applied);
                    Assert.Equal(Mod(a, "catcher.framing").FactorPpm, Mod(b, "catcher.framing").FactorPpm);
                }
    }
}
