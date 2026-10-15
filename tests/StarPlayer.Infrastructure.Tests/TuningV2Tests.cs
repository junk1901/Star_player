using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-D (owner decision P4): <c>tuning-virtual-proto-v2</c> for <c>prob-model-v2</c>. v1 file, hash and behaviour are untouched; v2 is v1 plus the three active park slots
/// (neutral 1000 permille, 1000 ppm per permille, factor 800,000..1,250,000, input 800..1250 permille). Expected distributions come from the independent Python oracle.
/// </summary>
public class TuningV2Tests
{
    // SHA-256 of the canonical text of content/tuning/virtual_tuning_v2.json, computed by the INDEPENDENT Python implementation (gen_virtual_tuning_v2.py --hash).
    internal const string GoldenHashV2 = "7f02cb4fa4f86800434e430713eafbaf65cb2e06ff73f96518298b69e8dfdd70";

    private static readonly string[] ParkSlots = ["park.home_runs", "park.hits", "park.extra_base_hits"];

    private static readonly string[] Batted =
        ["ground_weak", "ground_medium", "ground_hard", "line_weak", "line_medium", "line_hard", "fly_weak", "fly_medium", "fly_hard", "popup"];

    private static readonly TuningProfile V1 = Gx.Tuning1;
    private static readonly TuningProfile V2 = Gx.Tuning2;

    private static StageResult Defence(TuningProfile t, string batted, params ModifierInput[] inputs)
    {
        var made = StageProbability.Compute(t, StageIds.DefenseResult, batted, inputs);
        Assert.True(made.IsSuccess, made.Error?.Code + " " + made.Error?.Message);
        return made.Value;
    }

    // ---------------------------------------------------------------- identity and v1 protection

    [Fact]
    public void The_v1_tuning_hash_and_identity_are_unchanged_by_the_v2_work()
    {
        Assert.Equal(TuningProfileTests.GoldenHash, V1.ContentHash);
        Assert.Equal("prob-model-v1", V1.Model.Version.Value);
        Assert.Equal("tuning-virtual-proto-v1", V1.Tuning.Value);
        Assert.Same(ProbabilityModel.V1, V1.Model);
    }

    [Fact]
    public void The_v2_tuning_loads_with_its_own_versions_and_the_independently_computed_hash()
    {
        Assert.Equal("prob-model-v2", V2.Model.Version.Value);
        Assert.Equal("tuning-virtual-proto-v2", V2.Tuning.Value);
        Assert.Same(ProbabilityModel.V2, V2.Model);
        Assert.Equal(GoldenHashV2, V2.ContentHash);
        Assert.NotEqual(V1.ContentHash, V2.ContentHash);
        Assert.StartsWith("VIRTUAL PROTOTYPE", V2.Provenance, StringComparison.Ordinal);
        Assert.Contains("not derived from KBO", V2.Provenance, StringComparison.Ordinal);
        Assert.Contains("Phase 3-F", V2.Provenance, StringComparison.Ordinal);
    }

    [Fact]
    public void V2_differs_from_v1_only_in_its_versions_its_provenance_and_the_three_park_modifiers()
    {
        Assert.Equal(V1.ZoneModel.Value, V2.ZoneModel.Value);
        Assert.Equal(V1.Reference.Id, V2.Reference.Id);
        Assert.Equal((V1.CombinedMinPpm, V1.CombinedMaxPpm), (V2.CombinedMinPpm, V2.CombinedMaxPpm));
        Assert.Equal((V1.ContactCapPpm, V1.QualityCapPpm, V1.TendencyMinMassPpm), (V2.ContactCapPpm, V2.QualityCapPpm, V2.TendencyMinMassPpm));
        Assert.Equal(V1.MinPositiveBaselinePpm, V2.MinPositiveBaselinePpm);
        foreach (var stage in V1.Model.Stages)
            foreach (var condition in stage.Conditions)
                Assert.Equal(V1.Baseline(stage.Id, condition.Key).Value.Ppm, V2.Baseline(stage.Id, condition.Key).Value.Ppm);
        foreach (var slot in V1.Model.Slots.Where(s => !ParkSlots.Contains(s.Id)))
        {
            var a = V1.Modifier(slot.Id).Value;
            var b = V2.Modifier(slot.Id).Value;
            Assert.Equal((a.Neutral, a.SlopePpmPerUnit, a.MinPpm, a.MaxPpm), (b.Neutral, b.SlopePpmPerUnit, b.MinPpm, b.MaxPpm));
            Assert.Equal(a.Table.Select(t => (t.Key, t.Value)), b.Table.Select(t => (t.Key, t.Value)));
        }

        Assert.Equal(V1.Scenarios.Select(s => (s.Id, s.Stage, s.Condition, s.Seed, s.ContextKey, s.Samples, s.ToleranceRule)), V2.Scenarios.Select(s => (s.Id, s.Stage, s.Condition, s.Seed, s.ContextKey, s.Samples, s.ToleranceRule)));
        Assert.Equal(V1.Targets.Select(t => (t.Id, t.LowPpm, t.HighPpm, t.Status)), V2.Targets.Select(t => (t.Id, t.LowPpm, t.HighPpm, t.Status)));
    }

    [Fact]
    public void The_park_modifiers_carry_the_approved_numbers_and_the_v1_reserved_ones_carry_none()
    {
        foreach (var id in ParkSlots)
        {
            var m = V2.Modifier(id).Value;
            Assert.Equal((1000, 1000, 800_000, 1_250_000), (m.Neutral, m.SlopePpmPerUnit, m.MinPpm, m.MaxPpm));
            var reserved = V1.Modifier(id).Value;
            Assert.Equal((0, 1_000_000, 1_000_000), (reserved.SlopePpmPerUnit, reserved.MinPpm, reserved.MaxPpm));
        }
    }

    [Fact]
    public void A_model_and_a_tuning_of_different_versions_never_combine()
    {
        var v2 = TuningProfileJson.ReadDraft(ScriptedGameSource.ReadResource(TuningProfileJson.ResourceNameV2)).Value;
        Assert.Equal("Tuning.model_tuning_mismatch", TuningProfileTests.Reject(v2 with { ModelVersion = "prob-model-v1" }).Code);
        Assert.Equal("Tuning.model_tuning_mismatch", TuningProfileTests.Reject(TuningProfileTests.Draft() with { ModelVersion = "prob-model-v2" }).Code);
        Assert.Equal("Tuning.model_tuning_mismatch", TuningProfileTests.Reject(TuningProfileTests.Draft() with { TuningVersion = "tuning-virtual-proto-v2" }).Code);
        // v2 data whose park slot lost its authority (a reserved-style entry) or exceeds the cap is rejected by the ordinary validation
        var park = v2.Modifiers!.Select(m => m.Slot == "park.hits" ? m with { SlopePpmPerUnit = 2000 } : m).ToList();
        Assert.Equal("Tuning.modifier_range_exceeds_cap", TuningProfileTests.Reject(v2 with { Modifiers = park }).Code);
        var wrongSign = v2.Modifiers!.Select(m => m.Slot == "park.hits" ? m with { SlopePpmPerUnit = -1000 } : m).ToList();
        Assert.Equal("Tuning.modifier_direction_invalid", TuningProfileTests.Reject(v2 with { Modifiers = wrongSign }).Code);
    }

    // ---------------------------------------------------------------- the park factor and its range

    [Theory]
    [InlineData(800, 800_000)]
    [InlineData(1000, 1_000_000)]
    [InlineData(1250, 1_250_000)]
    [InlineData(900, 900_000)]
    [InlineData(1100, 1_100_000)]
    public void Each_park_slot_turns_a_permille_into_exactly_1000_ppm_per_permille(int permille, int expectedFactor)
    {
        foreach (var slot in ParkSlots)
        {
            var result = Defence(V2, "fly_hard", ModifierInput.Linear(slot, permille));
            var listed = Assert.Single(result.Trace.Modifiers);
            Assert.Equal((slot, permille.ToString(), expectedFactor, true), (listed.Slot, listed.Input, listed.FactorPpm, listed.Applied));
        }
    }

    [Theory]
    [InlineData(799)]
    [InlineData(1251)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3000)]      // the domain's absolute bound: valid for C46, not supported by the active model
    [InlineData(1_000_000)]
    public void A_park_input_outside_800_to_1250_is_refused_never_clamped(int permille)
    {
        foreach (var slot in ParkSlots)
        {
            var made = StageProbability.Compute(V2, StageIds.DefenseResult, "fly_hard", [ModifierInput.Linear(slot, permille)]);
            Assert.False(made.IsSuccess);
            Assert.Equal("Modifier.input_out_of_range", made.Error!.Value.Code);
        }
    }

    [Fact]
    public void Under_v1_a_non_neutral_park_input_is_still_refused_and_neutral_is_still_accepted()
    {
        foreach (var slot in ParkSlots)
        {
            Assert.Equal("Modifier.reserved_not_active", StageProbability.Compute(V1, StageIds.DefenseResult, "fly_hard", [ModifierInput.Linear(slot, 1100)]).Error!.Value.Code);
            Assert.True(StageProbability.Compute(V1, StageIds.DefenseResult, "fly_hard", [ModifierInput.Linear(slot, 1000)]).IsSuccess);
        }
    }

    // ---------------------------------------------------------------- direct effect, neutrality, caps and sums

    [Fact]
    public void A_neutral_park_is_the_baseline_exactly_for_all_ten_batted_balls()
    {
        foreach (var batted in Batted)
        {
            var neutral = Defence(V2, batted, ParkSlots.Select(s => ModifierInput.Linear(s, 1000)).ToArray());
            var none = Defence(V2, batted);
            Assert.Equal(V2.Baseline(StageIds.DefenseResult, batted).Value.Ppm, neutral.Distribution.Ppm);
            Assert.Equal(none.Distribution.Ppm, neutral.Distribution.Ppm);
            Assert.All(neutral.Trace.CombinedFactorPpm, f => Assert.Equal(1_000_000, f));
        }
    }

    [Fact]
    public void Each_park_slot_acts_directly_only_on_the_outcomes_it_owns()
    {
        var owned = new Dictionary<string, string[]> { ["park.home_runs"] = ["home_run"], ["park.hits"] = ["single"], ["park.extra_base_hits"] = ["double", "triple"] };
        foreach (var (slot, outcomes) in owned)
            foreach (var batted in Batted)
            {
                var result = Defence(V2, batted, ModifierInput.Linear(slot, 1100));
                for (var i = 0; i < result.Trace.Outcomes.Count; i++)
                {
                    var o = result.Trace.Outcomes[i];
                    var isOwned = outcomes.Contains(o);
                    var baseline = result.Trace.Baseline[i];
                    // direct factor: exactly the slot's factor on an owned outcome, exactly neutral on every other outcome
                    Assert.Equal(isOwned ? 1_100_000 : 1_000_000, result.Trace.CombinedFactorPpm[i]);
                    if (baseline == 0)
                        Assert.Equal(0, result.Distribution.Ppm[i]);              // an impossible outcome stays impossible
                    else if (isOwned)
                        Assert.True(result.Distribution.Ppm[i] > baseline, $"{slot} {batted} {o}");
                    else
                        Assert.True(result.Distribution.Ppm[i] <= baseline, $"{slot} {batted} {o} (indirect, normalisation only)");
                }
            }
    }

    [Fact]
    public void A_lower_park_value_lowers_the_owned_outcome_and_a_higher_one_raises_it_monotonically()
    {
        foreach (var batted in new[] { "line_hard", "fly_medium", "fly_hard" })
        {
            var homeRuns = new[] { 800, 900, 1000, 1100, 1250 }.Select(p => Defence(V2, batted, ModifierInput.Linear("park.home_runs", p)).Distribution.Of("home_run").Value).ToArray();
            Assert.True(homeRuns.Zip(homeRuns.Skip(1), (a, b) => a < b).All(x => x), string.Join(",", homeRuns));
        }

        var singles = new[] { 800, 900, 1000, 1100, 1250 }.Select(p => Defence(V2, "ground_medium", ModifierInput.Linear("park.hits", p)).Distribution.Of("single").Value).ToArray();
        Assert.True(singles.Zip(singles.Skip(1), (a, b) => a < b).All(x => x), string.Join(",", singles));
        var doubles = new[] { 800, 1000, 1250 }.Select(p => Defence(V2, "fly_hard", ModifierInput.Linear("park.extra_base_hits", p)).Distribution.Of("double").Value).ToArray();
        Assert.True(doubles[0] < doubles[1] && doubles[1] < doubles[2]);
    }

    [Fact]
    public void The_grid_of_park_and_fielding_values_keeps_the_sum_the_zeros_and_the_caps()
    {
        int[] park = [800, 900, 1000, 1100, 1250];
        foreach (var batted in Batted)
            foreach (var hr in park)
                foreach (var hits in park)
                    foreach (var xbh in park)
                        foreach (var fielding in new[] { 0, 50, 100 })
                        {
                            var result = Defence(V2, batted, ModifierInput.Linear("park.home_runs", hr), ModifierInput.Linear("park.hits", hits),
                                ModifierInput.Linear("park.extra_base_hits", xbh), ModifierInput.Linear("defense.fielding", fielding));
                            Assert.Equal(1_000_000, result.Distribution.Ppm.Sum());
                            Assert.All(result.Trace.CombinedFactorPpm, f => Assert.InRange(f, 800_000, 1_250_000));
                            Assert.All(result.Trace.CombinedCapped, capped => Assert.False(capped));    // the combined cap never has to act: no outcome has two slots
                            for (var i = 0; i < result.Trace.Baseline.Count; i++)
                                Assert.Equal(result.Trace.Baseline[i] > 0, result.Distribution.Ppm[i] > 0);
                        }
    }

    [Fact]
    public void The_defense_result_distributions_match_the_independent_oracle_for_every_park_and_fielding_case()
    {
        var checkedCases = 0;
        foreach (var c in Gx.Oracle.GetProperty("defense").EnumerateArray())
        {
            var inputs = new List<ModifierInput>();
            if (c.GetProperty("fielding") is { ValueKind: System.Text.Json.JsonValueKind.Number } f)
                inputs.Add(ModifierInput.Linear("defense.fielding", f.GetInt32()));
            if (c.GetProperty("park") is { ValueKind: System.Text.Json.JsonValueKind.Array } p)
            {
                var v = p.EnumerateArray().Select(x => x.GetInt32()).ToArray();
                inputs.Add(ModifierInput.Linear("park.home_runs", v[0]));
                inputs.Add(ModifierInput.Linear("park.hits", v[1]));
                inputs.Add(ModifierInput.Linear("park.extra_base_hits", v[2]));
            }

            var result = Defence(V2, c.GetProperty("batted").GetString()!, [.. inputs]);
            Assert.Equal(c.GetProperty("ppm").EnumerateArray().Select(x => x.GetInt32()).ToArray(), result.Distribution.Ppm);
            Assert.Equal(c.GetProperty("combined").EnumerateArray().Select(x => x.GetInt32()).ToArray(), result.Trace.CombinedFactorPpm);
            checkedCases++;
        }

        Assert.Equal(900, checkedCases);          // 10 batted balls x 15 park cases (none + 14) x 6 fielding cases (none + 5)
    }

    [Fact]
    public void Park_and_fielding_changes_move_other_outcomes_only_through_normalisation()
    {
        // a hitter's park (HR/hits/XBH raised) with the best fielder: the out share falls from park normalisation and rises from fielding; no outcome is counted twice
        var park = Defence(V2, "fly_hard", ModifierInput.Linear("park.home_runs", 1250), ModifierInput.Linear("park.hits", 1250), ModifierInput.Linear("park.extra_base_hits", 1250));
        var both = Defence(V2, "fly_hard", ModifierInput.Linear("park.home_runs", 1250), ModifierInput.Linear("park.hits", 1250), ModifierInput.Linear("park.extra_base_hits", 1250),
            ModifierInput.Linear("defense.fielding", 100));
        Assert.True(both.Distribution.Of("out").Value > park.Distribution.Of("out").Value);
        Assert.Equal(1_040_000, both.Trace.CombinedFactorPpm[0]);           // fielding alone owns "out": 1,000,000 + (100 - 50) x 800
        Assert.Equal(1_250_000, both.Trace.CombinedFactorPpm[4]);           // the park alone owns "home_run"
    }
}
