using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>Phase 3-B2: the virtual tuning data, its deterministic identity, and every validation rule (each rule has a mutation that must be rejected).</summary>
public class TuningProfileTests
{
    // SHA-256 of the canonical text of content/tuning/virtual_tuning_v1.json, computed by the INDEPENDENT Python implementation in
    // tests/Fixtures/generator/gen_virtual_tuning_v1.py (--hash) and cross-checked against the C# value. Changing any number, id or the provenance changes it.
    internal const string GoldenHash = "5b70dca79ec3a3c815f741b57c4c4490e93c5bcdca19900ad452732c66d73ea5";

    internal static string Raw() => ScriptedGameSource.ReadResource(TuningProfileJson.ResourceName);

    internal static TuningProfile Profile()
    {
        var made = TuningProfileJson.LoadVirtualV1();
        Assert.True(made.IsSuccess, made.Error?.Code + " " + made.Error?.Message);
        return made.Value;
    }

    internal static TuningProfileDraft Draft()
    {
        var made = TuningProfileJson.ReadDraft(Raw());
        Assert.True(made.IsSuccess, made.Error?.Message);
        return made.Value;
    }

    internal static Error Reject(TuningProfileDraft draft)
    {
        var made = TuningProfile.Create(draft);
        Assert.False(made.IsSuccess, "the mutated profile was accepted");
        return made.Error!.Value;
    }

    private static TuningProfileDraft EditRow(TuningProfileDraft d, string stage, string condition, Func<List<OutcomeDraft>, List<OutcomeDraft>> edit) =>
        d with { Baselines = d.Baselines!.Select(r => r.Stage == stage && r.Condition == condition ? r with { Outcomes = edit(r.Outcomes!.ToList()) } : r).ToList() };

    private static TuningProfileDraft Move(TuningProfileDraft d, string stage, string condition, string from, string to, int ppm) =>
        EditRow(d, stage, condition, o => o.Select(x => x.Id == from ? x with { Ppm = x.Ppm - ppm } : x.Id == to ? x with { Ppm = x.Ppm + ppm } : x).ToList());

    private static TuningProfileDraft EditModifier(TuningProfileDraft d, string slot, Func<ModifierDraft, ModifierDraft> edit) =>
        d with { Modifiers = d.Modifiers!.Select(m => m.Slot == slot ? edit(m) : m).ToList() };

    // ---- loading and identity ----

    [Fact]
    public void The_embedded_virtual_tuning_loads_and_declares_itself_a_virtual_prototype()
    {
        var p = Profile();
        Assert.Equal("prob-model-v1", p.Model.Version.Value);
        Assert.Equal("tuning-virtual-proto-v1", p.Tuning.Value);
        Assert.Equal("pzl-zones-v1", p.ZoneModel.Value);
        Assert.Equal("pzl-ref-uniform-v1", p.Reference.Id);
        Assert.StartsWith("VIRTUAL PROTOTYPE", p.Provenance, StringComparison.Ordinal);
        Assert.Contains("not derived from KBO", p.Provenance, StringComparison.Ordinal);
        Assert.Equal(1_000_000, ProbabilityScale.Denominator);
    }

    [Fact]
    public void The_content_hash_matches_the_independently_computed_value()
    {
        Assert.Equal(GoldenHash, Profile().ContentHash);
    }

    [Fact]
    public void Loading_twice_gives_the_same_identity_and_canonical_text()
    {
        var a = Profile();
        var b = Profile();
        Assert.Equal(a.ContentHash, b.ContentHash);
        Assert.Equal(a.CanonicalText, b.CanonicalText);
        Assert.Equal(64, a.ContentHash.Length);
    }

    [Fact]
    public void Formatting_key_order_and_list_order_do_not_change_the_identity()
    {
        var compact = JsonNode.Parse(Raw())!.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        Assert.Equal(GoldenHash, TuningProfileJson.Read(compact).Value.ContentHash);

        var d = Draft();
        var shuffled = d with
        {
            Baselines = d.Baselines!.Reverse().ToList(),
            Modifiers = d.Modifiers!.Reverse().ToList(),
            Scenarios = d.Scenarios!.Reverse().ToList(),
            Targets = d.Targets!.Reverse().ToList(),
        };
        Assert.Equal(GoldenHash, TuningProfile.Create(shuffled).Value.ContentHash);
    }

    [Fact]
    public void Free_text_descriptions_do_not_change_the_identity_but_every_number_and_the_provenance_do()
    {
        var d = Draft();
        Assert.Equal(GoldenHash, TuningProfile.Create(d with { Scenarios = d.Scenarios!.Select(s => s with { Description = s.Description + " (edited)" }).ToList() }).Value.ContentHash);
        Assert.Equal(GoldenHash, TuningProfile.Create(d with { Targets = d.Targets!.Select(t => t with { Description = "x" }).ToList() }).Value.ContentHash);

        var changed = new List<TuningProfileDraft>
        {
            Move(d, "defense_result", "fly_hard", "out", "single", 1),
            d with { Provenance = d.Provenance + "." },
            d with { CombinedMaxPpm = 1_240_000 },
            d with { MinPositiveBaselinePpm = 900 },
            d with { Tendency = d.Tendency! with { ContactCapPpm = 59_900 } },
            EditModifier(d, "batter.power", m => m with { SlopePpmPerUnit = 1_499 }),
            EditModifier(d, "strategy.approach", m => m with { Table = m.Table!.Select(t => t.Key == "aggressive" ? t with { FactorPpm = 1_099_999 } : t).ToList() }),
            d with { Scenarios = d.Scenarios!.Select(s => s.Id == "s2_location_on_target_center" ? s with { Seed = s.Seed + 1 } : s).ToList() },
            d with { Targets = d.Targets!.Select(t => t.Id == "babip" ? t with { HighPpm = t.HighPpm + 1 } : t).ToList() },
        };
        var hashes = changed.Select(c => TuningProfile.Create(c).Value.ContentHash).ToList();
        Assert.All(hashes, h => Assert.NotEqual(GoldenHash, h));
        Assert.Equal(hashes.Count, hashes.Distinct().Count());
    }

    // ---- versions ----

    [Fact]
    public void Unknown_model_tuning_zone_and_reference_versions_are_rejected()
    {
        var d = Draft();
        Assert.Equal("ProbabilityModelVersion.unsupported", Reject(d with { ModelVersion = "prob-model-v2" }).Code);
        Assert.Equal("ProbabilityModelVersion.unsupported", Reject(d with { ModelVersion = null }).Code);
        Assert.Equal("TuningVersion.unsupported", Reject(d with { TuningVersion = "tuning-virtual-proto-v2" }).Code);
        Assert.Equal("TuningVersion.unsupported", Reject(d with { TuningVersion = null }).Code);
        Assert.Equal("ZoneModelVersion.unsupported", Reject(d with { ZoneModel = "pzl-zones-v2" }).Code);
        Assert.Equal("ZoneReferenceDistribution.id_unknown", Reject(d with { ReferenceDistributionId = "pzl-ref-uniform-v2" }).Code);
        Assert.Equal("ZoneReferenceDistribution.id_unknown", Reject(d with { ReferenceDistributionId = null }).Code);
        Assert.Equal("Tuning.missing", TuningProfile.Create(null).Error!.Value.Code);
    }

    [Fact]
    public void File_level_problems_are_rejected_with_fixture_codes()
    {
        var raw = Raw();
        Assert.Equal("Fixture.version_unsupported", TuningProfileJson.Read(raw.Replace("\"format_version\": 1", "\"format_version\": 2", StringComparison.Ordinal)).Error!.Value.Code);
        Assert.Equal("Fixture.json_invalid", TuningProfileJson.Read(raw.Replace("\"denominator\": 1000000,", "\"denominator\": 1000000, \"extra\": 1,", StringComparison.Ordinal)).Error!.Value.Code);
        Assert.Equal("Fixture.json_invalid", TuningProfileJson.Read("{ not json").Error!.Value.Code);
        var lengths = JsonNode.Parse(raw)!;
        lengths["baselines"]![0]!["ppm"]!.AsArray().RemoveAt(0);
        Assert.Equal("Fixture.shape_invalid", TuningProfileJson.Read(lengths.ToJsonString()).Error!.Value.Code);
    }

    [Fact]
    public void The_profile_does_not_alter_the_approved_zone_model_or_reference_definitions()
    {
        var p = Profile();
        Assert.Equal(17, PitchZoneModel.V1.Zones.Count);
        Assert.Equal(5, PitchZoneModel.V1.Zones.Count(z => z.Boundary == BoundaryClass.Clear));
        Assert.Equal(12, PitchZoneModel.V1.Zones.Count(z => z.Boundary == BoundaryClass.Borderline));
        Assert.Equal("pzl-ref-uniform-v1", ZoneReferenceDistribution.UniformV1.Id);
        Assert.Equal([1, 1, 1, 1, 1, 1, 1, 1, 1], ZoneReferenceDistribution.UniformV1.Weights);
        Assert.Same(ZoneReferenceDistribution.UniformV1, p.Reference);
        Assert.Equal("weights_mismatch", ZoneReferenceDistribution.Create("pzl-ref-uniform-v1", [2, 1, 1, 1, 1, 1, 1, 1, 1]).Error!.Value.Code.Split('.')[1]);
    }

    // ---- policy fields ----

    [Fact]
    public void Status_provenance_policies_and_denominator_are_enforced()
    {
        var d = Draft();
        Assert.Equal("Tuning.status_invalid", Reject(d with { Status = "official_kbo" }).Code);
        Assert.Equal("Tuning.provenance_invalid", Reject(d with { Provenance = "" }).Code);
        Assert.Equal("Tuning.provenance_invalid", Reject(d with { Provenance = " padded" }).Code);
        Assert.Equal("Tuning.provenance_invalid", Reject(d with { Provenance = "two\nlines" }).Code);
        Assert.Equal("Tuning.provenance_invalid", Reject(d with { Provenance = "has|bar" }).Code);
        Assert.Equal("Tuning.normalization_unsupported", Reject(d with { NormalizationPolicy = "largest-remainder-highest-index-v1" }).Code);
        Assert.Equal("Tuning.rounding_unsupported", Reject(d with { RoundingPolicy = "half-even-v1" }).Code);
        Assert.Equal("Tuning.denominator_unsupported", Reject(d with { Denominator = 100_000 }).Code);
        Assert.Equal("Tuning.denominator_unsupported", Reject(d with { Denominator = 0 }).Code);
    }

    [Fact]
    public void Combined_caps_and_the_positive_baseline_floor_are_enforced()
    {
        var d = Draft();
        Assert.Equal("Tuning.min_positive_invalid", Reject(d with { MinPositiveBaselinePpm = 0 }).Code);
        Assert.Equal("Tuning.min_positive_invalid", Reject(d with { MinPositiveBaselinePpm = 10_001 }).Code);
        Assert.Equal("Tuning.combined_cap_invalid", Reject(d with { CombinedMinPpm = 0 }).Code);
        Assert.Equal("Tuning.combined_cap_invalid", Reject(d with { CombinedMinPpm = 1_000_001 }).Code);
        Assert.Equal("Tuning.combined_cap_invalid", Reject(d with { CombinedMaxPpm = 999_999 }).Code);
        Assert.Equal("Tuning.combined_cap_invalid", Reject(d with { CombinedMaxPpm = 2_000_001 }).Code);
        Assert.Equal("Tuning.combined_cap_too_wide", Reject(d with { CombinedMinPpm = 600_000, CombinedMaxPpm = 1_250_000 }).Code);
    }

    [Fact]
    public void Tendency_caps_must_divide_evenly_and_leave_room()
    {
        var d = Draft();
        Assert.Equal("Tuning.tendency_missing", Reject(d with { Tendency = null }).Code);
        Assert.Equal("Tuning.tendency_cap_invalid", Reject(d with { Tendency = d.Tendency! with { ContactCapPpm = 60_050 } }).Code);
        Assert.Equal("Tuning.tendency_cap_invalid", Reject(d with { Tendency = d.Tendency! with { QualityCapPpm = 0 } }).Code);
        Assert.Equal("Tuning.tendency_cap_invalid", Reject(d with { Tendency = d.Tendency! with { ContactCapPpm = 300_000 } }).Code);
        Assert.Equal("Tuning.tendency_min_mass_invalid", Reject(d with { Tendency = d.Tendency! with { MinMassPpm = 10 } }).Code);
        Assert.Equal("Tuning.tendency_min_mass_invalid", Reject(d with { Tendency = d.Tendency! with { MinMassPpm = 60_000 } }).Code);
        // a cap so large that an inner zone's favourable mass no longer fits the window
        Assert.Equal("Tuning.tendency_window_contact", Reject(d with { Tendency = d.Tendency! with { ContactCapPpm = 200_000 } }).Code);
        var thinHardContact = EditRow(d, "batted_ball", "heart", o =>
        {
            var hard = o.Where(x => x.Id!.EndsWith("_hard", StringComparison.Ordinal)).Sum(x => x.Ppm);
            return o.Select(x => x.Id!.EndsWith("_hard", StringComparison.Ordinal) ? x with { Ppm = 1_000 } : x.Id == "ground_weak" ? x with { Ppm = x.Ppm + hard - 3_000 } : x).ToList();
        });
        Assert.Equal("Tuning.tendency_window_quality", Reject(thinHardContact).Code);
    }

    // ---- baseline tables ----

    [Fact]
    public void Baseline_rows_must_be_complete_exact_and_sum_to_the_denominator()
    {
        var d = Draft();
        Assert.Equal("Tuning.baselines_missing", Reject(d with { Baselines = null }).Code);
        Assert.Equal("Tuning.baseline_missing", Reject(d with { Baselines = d.Baselines!.Skip(1).ToList() }).Code);
        Assert.Equal("Tuning.baseline_duplicate", Reject(d with { Baselines = d.Baselines!.Append(d.Baselines![0]).ToList() }).Code);
        Assert.Equal("Tuning.baseline_stage_unknown", Reject(d with { Baselines = d.Baselines!.Select((r, i) => i == 0 ? r with { Stage = "nope" } : r).ToList() }).Code);
        Assert.Equal("Tuning.baseline_condition_unknown", Reject(d with { Baselines = d.Baselines!.Select((r, i) => i == 0 ? r with { Condition = "reckless" } : r).ToList() }).Code);
        Assert.Equal("Tuning.baseline_row_invalid", Reject(d with { Baselines = d.Baselines!.Select((r, i) => i == 0 ? r with { Outcomes = null } : r).ToList() }).Code);
        Assert.Equal("Tuning.baseline_outcomes_mismatch", Reject(EditRow(d, "defense_result", "fly_hard", o => o.AsEnumerable().Reverse().ToList())).Code);
        Assert.Equal("Tuning.baseline_outcomes_mismatch", Reject(EditRow(d, "defense_result", "fly_hard", o => o.Take(5).ToList())).Code);

        Assert.Equal("Distribution.sum_invalid", Reject(EditRow(d, "defense_result", "fly_hard", o => o.Select(x => x.Id == "out" ? x with { Ppm = x.Ppm + 1 } : x).ToList())).Code);
        Assert.Equal("Distribution.sum_invalid", Reject(EditRow(d, "actual_location", "in.r2c2", o => o.Select(x => x.Id == "out.low" ? x with { Ppm = x.Ppm - 1 } : x).ToList())).Code);
        Assert.Equal("Distribution.probability_out_of_range", Reject(Move(d, "defense_result", "fly_hard", "out", "single", 400_000)).Code);
    }

    [Fact]
    public void A_positive_baseline_below_the_floor_is_rejected_but_an_explicit_zero_is_allowed()
    {
        var d = Draft();
        Assert.Equal("Tuning.baseline_positive_too_small", Reject(Move(d, "defense_result", "fly_hard", "reached_on_error", "out", 9_500)).Code);
        Assert.Equal("Tuning.baseline_positive_too_small", Reject(Move(d, "swing_result", "in.r2c2", "foul", "miss", 249_500)).Code);
        // zero baselines already exist (for example a triple on a weak ground ball) and are accepted
        Assert.Equal(0, Profile().Baseline(StageIds.DefenseResult, "ground_weak").Value.Of("triple").Value);
    }

    [Fact]
    public void Hit_by_pitch_is_possible_only_on_the_batter_side_outer_zones_and_never_above_ten_percent()
    {
        var d = Draft();
        Assert.Equal("Tuning.hbp_rule", Reject(Move(d, "batter_decision", "in.r2c2", "take", "hit_by_pitch", 1_000)).Code);
        Assert.Equal("Tuning.hbp_rule", Reject(Move(d, "batter_decision", "out.out", "take", "hit_by_pitch", 1_000)).Code);
        Assert.Equal("Tuning.hbp_rule", Reject(Move(d, "batter_decision", "out.in", "take", "hit_by_pitch", 90_000)).Code);
        Assert.True(Profile().Baseline(StageIds.BatterDecision, "out.in").Value.Of("hit_by_pitch").Value > 0);
    }

    [Fact]
    public void Called_pitch_tables_follow_the_call_mode_and_the_boundary_class()
    {
        var d = Draft();
        // automated: the strike zone is a hard line
        Assert.Equal("Tuning.called_pitch_automated_rule", Reject(Move(d, "called_pitch", "automated:in.r1c1", "strike", "ball", 1_000)).Code);
        Assert.Equal("Tuning.called_pitch_automated_rule", Reject(Move(d, "called_pitch", "automated:out.low", "ball", "strike", 1_000)).Code);
        // non-automated Clear zones are near-certain; Borderline zones are genuinely uncertain and lean the way of their side of the line
        Assert.Equal("Tuning.called_pitch_clear_rule", Reject(Move(d, "called_pitch", "non_automated:in.r2c2", "strike", "ball", 60_000)).Code);
        Assert.Equal("Tuning.called_pitch_clear_rule", Reject(Move(d, "called_pitch", "non_automated:out.high_out", "ball", "strike", 40_000)).Code);
        Assert.Equal("Tuning.called_pitch_borderline_rule", Reject(Move(d, "called_pitch", "non_automated:in.r1c2", "ball", "strike", 120_000)).Code);
        Assert.Equal("Tuning.called_pitch_borderline_rule", Reject(Move(d, "called_pitch", "non_automated:out.in", "strike", "ball", 190_000)).Code);
        Assert.Equal("Tuning.called_pitch_borderline_rule", Reject(Move(d, "called_pitch", "non_automated:in.r2c1", "strike", "ball", 400_000)).Code);
    }

    // ---- modifiers ----

    [Fact]
    public void Every_slot_needs_valid_tuning_and_unknown_or_duplicate_slots_are_rejected()
    {
        var d = Draft();
        Assert.Equal("Tuning.modifiers_missing", Reject(d with { Modifiers = null }).Code);
        Assert.Equal("Tuning.modifier_missing", Reject(d with { Modifiers = d.Modifiers!.Skip(1).ToList() }).Code);
        Assert.Equal("Tuning.modifier_slot_unknown", Reject(d with { Modifiers = d.Modifiers!.Append(d.Modifiers![0] with { Slot = "batter.charisma" }).ToList() }).Code);
        Assert.Equal("Tuning.modifier_duplicate", Reject(d with { Modifiers = d.Modifiers!.Append(d.Modifiers![0]).ToList() }).Code);
        Assert.Equal("Tuning.modifier_invalid", Reject(d with { Modifiers = d.Modifiers!.Append(new ModifierDraft(null, 0, 0, 1_000_000, 1_000_000, null)).ToList() }).Code);
    }

    [Fact]
    public void Linear_slots_must_follow_the_modelled_direction_stay_inside_their_cap_and_inside_the_combined_cap()
    {
        var d = Draft();
        Assert.Equal("Tuning.modifier_direction_invalid", Reject(EditModifier(d, "batter.power", m => m with { SlopePpmPerUnit = -100 })).Code);
        Assert.Equal("Tuning.modifier_direction_invalid", Reject(EditModifier(d, "batter.contact", m => m with { SlopePpmPerUnit = 100 })).Code);
        Assert.Equal("Tuning.modifier_range_exceeds_cap", Reject(EditModifier(d, "batter.power", m => m with { SlopePpmPerUnit = 2_100 })).Code);
        Assert.Equal("Tuning.modifier_range_exceeds_cap", Reject(EditModifier(d, "pitcher.control", m => m with { Neutral = 10 })).Code);
        Assert.Equal("Tuning.modifier_neutral_invalid", Reject(EditModifier(d, "pitcher.control", m => m with { Neutral = 101 })).Code);
        Assert.Equal("Tuning.modifier_cap_invalid", Reject(EditModifier(d, "pitcher.control", m => m with { MaxPpm = 1_300_000 })).Code);
        Assert.Equal("Tuning.modifier_cap_invalid", Reject(EditModifier(d, "pitcher.control", m => m with { MinPpm = 700_000 })).Code);
        Assert.Equal("Tuning.modifier_cap_invalid", Reject(EditModifier(d, "pitcher.control", m => m with { MinPpm = 1_000_001 })).Code);
        Assert.Equal("Tuning.modifier_shape_invalid", Reject(EditModifier(d, "pitcher.control", m => m with { Table = [new TableEntryDraft("neutral", 1_000_000)] })).Code);
        Assert.True(TuningProfile.Create(EditModifier(d, "batter.power", m => m with { SlopePpmPerUnit = 0 })).IsSuccess); // a slope of 0 disables an effect and is legal
    }

    [Fact]
    public void Table_slots_must_keep_the_modelled_order_and_a_neutral_that_changes_nothing()
    {
        var d = Draft();
        Assert.Equal("Tuning.modifier_neutral_not_identity", Reject(EditModifier(d, "strategy.approach", m => m with { Table = m.Table!.Select(t => t.Key == "neutral" ? t with { FactorPpm = 1_000_001 } : t).ToList() })).Code);
        Assert.Equal("Tuning.modifier_table_order", Reject(EditModifier(d, "strategy.approach", m => m with { Table = m.Table!.Select(t => t.Key == "patient" ? t with { FactorPpm = 1_050_000 } : t).ToList() })).Code);
        Assert.Equal("Tuning.modifier_table_order", Reject(EditModifier(d, "matchup.handedness", m => m with { Table = m.Table!.Select(t => t.Key == "same_hand" ? t with { FactorPpm = 960_000 } : t).ToList() })).Code);
        Assert.Equal("Tuning.modifier_table_keys", Reject(EditModifier(d, "strategy.approach", m => m with { Table = m.Table!.Reverse().ToList() })).Code);
        Assert.Equal("Tuning.modifier_table_keys", Reject(EditModifier(d, "strategy.approach", m => m with { Table = m.Table!.Take(2).ToList() })).Code);
        Assert.Equal("Tuning.modifier_table_keys", Reject(EditModifier(d, "strategy.approach", m => m with { Table = null })).Code);
        Assert.Equal("Tuning.modifier_range_exceeds_cap", Reject(EditModifier(d, "strategy.approach", m => m with { Table = m.Table!.Select(t => t.Key == "aggressive" ? t with { FactorPpm = 1_200_000 } : t).ToList() })).Code);
        Assert.Equal("Tuning.modifier_shape_invalid", Reject(EditModifier(d, "strategy.approach", m => m with { SlopePpmPerUnit = 5 })).Code);
    }

    [Fact]
    public void Reserved_slots_carry_no_numeric_authority()
    {
        var d = Draft();
        foreach (var slot in new[] { "catcher.framing", "catcher.game_calling", "battery.chemistry", "park.home_runs", "park.hits", "park.extra_base_hits" })
        {
            Assert.Equal("Tuning.modifier_reserved_authority", Reject(EditModifier(d, slot, m => m with { SlopePpmPerUnit = 10 })).Code);
            Assert.Equal("Tuning.modifier_reserved_authority", Reject(EditModifier(d, slot, m => m with { MaxPpm = 1_010_000 })).Code);
            Assert.Equal("Tuning.modifier_reserved_authority", Reject(EditModifier(d, slot, m => m with { MinPpm = 990_000 })).Code);
        }

        Assert.Equal("Tuning.modifier_neutral_invalid", Reject(EditModifier(d, "park.hits", m => m with { Neutral = 0 })).Code);
        Assert.Equal("Tuning.modifier_neutral_invalid", Reject(EditModifier(d, "catcher.framing", m => m with { Neutral = 200 })).Code);
    }

    // ---- scenarios and targets ----

    [Fact]
    public void Validation_scenarios_and_targets_are_declared_up_front_and_validated()
    {
        var p = Profile();
        Assert.Equal(4, p.Scenarios.Count);
        Assert.Equal(8, p.Targets.Count);
        Assert.All(p.Scenarios, s => Assert.Equal(TuningProfile.ToleranceRuleBinomial, s.ToleranceRule));
        Assert.All(p.Targets, t => Assert.Equal(TuningProfile.TargetStatusDeclared, t.Status));
        Assert.Equal(p.Scenarios.Select(s => s.Id).OrderBy(x => x, StringComparer.Ordinal), p.Scenarios.Select(s => s.Id));

        var d = Draft();
        Assert.Equal("Tuning.scenarios_missing", Reject(d with { Scenarios = [] }).Code);
        Assert.Equal("Tuning.scenario_duplicate", Reject(d with { Scenarios = d.Scenarios!.Append(d.Scenarios![0]).ToList() }).Code);
        Assert.Equal("Tuning.scenario_target_unknown", Reject(d with { Scenarios = d.Scenarios!.Select((s, i) => i == 0 ? s with { Condition = "in.r9c9" } : s).ToList() }).Code);
        Assert.Equal("Tuning.scenario_samples_invalid", Reject(d with { Scenarios = d.Scenarios!.Select((s, i) => i == 0 ? s with { Samples = 10 } : s).ToList() }).Code);
        Assert.Equal("Tuning.scenario_context_invalid", Reject(d with { Scenarios = d.Scenarios!.Select((s, i) => i == 0 ? s with { ContextKey = "has space" } : s).ToList() }).Code);
        Assert.Equal("Tuning.scenario_tolerance_unsupported", Reject(d with { Scenarios = d.Scenarios!.Select((s, i) => i == 0 ? s with { ToleranceRule = "eyeball" } : s).ToList() }).Code);
        Assert.Equal("Tuning.scenario_invalid", Reject(d with { Scenarios = d.Scenarios!.Select((s, i) => i == 0 ? s with { Id = null } : s).ToList() }).Code);

        Assert.Equal("Tuning.targets_missing", Reject(d with { Targets = null }).Code);
        Assert.Equal("Tuning.target_duplicate", Reject(d with { Targets = d.Targets!.Append(d.Targets![0]).ToList() }).Code);
        Assert.Equal("Tuning.target_range_invalid", Reject(d with { Targets = d.Targets!.Select((t, i) => i == 0 ? t with { LowPpm = 500_000, HighPpm = 400_000 } : t).ToList() }).Code);
        Assert.Equal("Tuning.target_status_invalid", Reject(d with { Targets = d.Targets!.Select((t, i) => i == 0 ? t with { Status = "passed" } : t).ToList() }).Code);
    }
}
