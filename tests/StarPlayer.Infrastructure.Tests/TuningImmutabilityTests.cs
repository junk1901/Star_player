using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Tests;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-B2 fix round (Codex High): a loaded <see cref="TuningProfile"/> is deeply immutable, so its <c>ContentHash</c> always describes the values that calculations
/// really use. Every test tries to write through the public surface by casting; none may change a probability or leave the hash describing something else.
/// </summary>
public class TuningImmutabilityTests
{
    private const int D = ProbabilityScale.Denominator;

    private static StageResult Run(TuningProfile profile, string stage, string condition, params ModifierInput[] inputs) =>
        StageProbability.Compute(profile, stage, condition, inputs).Value;

    private static void TryToWrite<T>(IReadOnlyList<T> exposed, T replacement)
    {
        if (exposed is T[] array)
            array[0] = replacement;
        if (exposed is IList<T> list)
        {
            try { list[0] = replacement; } catch (NotSupportedException) { }
            try { list.Add(replacement); } catch (NotSupportedException) { }
            try { list.Clear(); } catch (NotSupportedException) { }
        }
    }

    // ---- an independent re-derivation of the identity from the PUBLIC accessors only ----

    private static string IndependentHash(TuningProfile p)
    {
        static string N(long v) => v.ToString(CultureInfo.InvariantCulture);
        var lines = new List<string>
        {
            "tuning-profile-canonical-v1", $"model={p.Model.Version}", $"tuning={p.Tuning}", "status=virtual_prototype", $"zone_model={p.ZoneModel}",
            $"reference={p.Reference.Id}|{string.Join(",", p.Reference.Weights.Select(w => N(w)))}",
            $"normalization={ProbabilityScale.NormalizationPolicy}", $"rounding={ProbabilityScale.RoundingPolicy}", $"denominator={N(D)}",
            $"min_positive_baseline_ppm={N(p.MinPositiveBaselinePpm)}", $"combined_ppm={N(p.CombinedMinPpm)},{N(p.CombinedMaxPpm)}",
            $"tendency_caps_ppm=contact:{N(p.ContactCapPpm)}|quality:{N(p.QualityCapPpm)}|min_mass:{N(p.TendencyMinMassPpm)}", $"provenance={p.Provenance}",
        };
        foreach (var stage in p.Model.Stages)
            foreach (var condition in stage.Conditions)
            {
                var d = p.Baseline(stage.Id, condition.Key).Value;
                lines.Add($"baseline|{stage.Id}|{condition.Key}|{string.Join(",", d.Outcomes.Select((o, i) => $"{o}={N(d.Ppm[i])}"))}");
            }

        foreach (var slot in p.Model.Slots)
        {
            var m = p.Modifier(slot.Id).Value;
            lines.Add(slot.Kind == ModifierKind.Linear
                ? $"modifier|{slot.Id}|linear|neutral={N(m.Neutral)}|slope={N(m.SlopePpmPerUnit)}|min={N(m.MinPpm)}|max={N(m.MaxPpm)}"
                : $"modifier|{slot.Id}|table|min={N(m.MinPpm)}|max={N(m.MaxPpm)}|{string.Join(",", m.Table.Select(t => $"{t.Key}={N(t.Value)}"))}");
        }

        foreach (var s in p.Scenarios.OrderBy(s => s.Id, StringComparer.Ordinal))
            lines.Add($"scenario|{s.Id}|{s.Stage}|{s.Condition}|seed={s.Seed.ToString(CultureInfo.InvariantCulture)}|context={s.ContextKey}|samples={N(s.Samples)}|rule={s.ToleranceRule}");
        foreach (var t in p.Targets.OrderBy(t => t.Id, StringComparer.Ordinal))
            lines.Add($"target|{t.Id}|{N(t.LowPpm)}|{N(t.HighPpm)}|{t.Status}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)))).ToLowerInvariant();
    }

    // Tries every write path the public surface offers.
    private static void Tamper(TuningProfile p)
    {
        foreach (var slot in p.Model.Slots)
        {
            var table = p.Modifier(slot.Id).Value.Table;
            if (table.Count > 0)
                TryToWrite(table, new KeyValuePair<string, int>("aggressive", 1_250_000));
        }

        foreach (var stage in p.Model.Stages)
            foreach (var condition in stage.Conditions)
            {
                TryToWrite(condition.Outcomes, "tampered");
                var d = p.Baseline(stage.Id, condition.Key).Value;
                TryToWrite(d.Ppm, 0);
                TryToWrite(d.Outcomes, "tampered");
            }

        TryToWrite(p.Scenarios, p.Scenarios[0]);
        TryToWrite(p.Targets, p.Targets[0]);
        TryToWrite(p.Reference.Weights, 9);
        TryToWrite(p.Model.Stages, p.Model.Stages[0]);
        TryToWrite(p.Model.Slots, p.Model.Slots[0]);
    }

    [Fact]
    public void The_Codex_modifier_table_tamper_cannot_change_a_calculation_or_the_hash()
    {
        var p = TuningProfileTests.Profile();
        var swingBefore = Run(p, StageIds.BatterDecision, "out.low", ModifierInput.Choice("strategy.approach", "aggressive")).Distribution.Of("swing").Value;
        var hashBefore = p.ContentHash;

        var table = p.Modifier("strategy.approach").Value.Table;
        if (table is List<KeyValuePair<string, int>> concrete)
            concrete[2] = new KeyValuePair<string, int>("aggressive", 1_250_000); // the Codex reproduction: swing 568966 -> 600000 ppm, hash unchanged

        var swingAfterReplace = Run(p, StageIds.BatterDecision, "out.low", ModifierInput.Choice("strategy.approach", "aggressive")).Distribution.Of("swing").Value;
        Assert.Equal(swingBefore, swingAfterReplace);

        TryToWrite(table, new KeyValuePair<string, int>("aggressive", 1_250_000)); // add / clear attempts

        var swingAfter = Run(p, StageIds.BatterDecision, "out.low", ModifierInput.Choice("strategy.approach", "aggressive")).Distribution.Of("swing").Value;
        Assert.Equal(swingBefore, swingAfter);
        Assert.Equal(1_100_000, p.Modifier("strategy.approach").Value.Table.Single(t => t.Key == "aggressive").Value);
        Assert.Equal(hashBefore, p.ContentHash);
        Assert.Equal(p.ContentHash, IndependentHash(p));
    }

    [Fact]
    public void No_public_path_of_the_tuning_profile_leads_to_an_array_or_a_writable_collection()
    {
        var p = TuningProfileTests.Profile();
        Assert.Empty(ImmutabilityAudit.Violations(p));
        foreach (var slot in p.Model.Slots)
            Assert.Empty(ImmutabilityAudit.Violations(p.Modifier(slot.Id).Value));
        foreach (var stage in p.Model.Stages)
            foreach (var condition in stage.Conditions)
                Assert.Empty(ImmutabilityAudit.Violations(p.Baseline(stage.Id, condition.Key).Value));
    }

    [Fact]
    public void After_every_tamper_attempt_the_calculations_the_content_and_the_hash_are_unchanged()
    {
        var p = TuningProfileTests.Profile();
        var digestBefore = ResultDigest(p);
        var canonicalBefore = p.CanonicalText;

        Tamper(p);

        Assert.Equal(digestBefore, ResultDigest(p));
        Assert.Equal(canonicalBefore, p.CanonicalText);
        Assert.Equal(TuningProfileTests.GoldenHash, p.ContentHash);
        Assert.Equal(TuningProfileTests.GoldenHash, IndependentHash(p));
    }

    [Fact]
    public void The_hash_recomputed_independently_from_the_public_content_equals_the_stored_hash()
    {
        var p = TuningProfileTests.Profile();
        Assert.Equal(p.ContentHash, IndependentHash(p));
        Assert.Equal(TuningProfileTests.GoldenHash, IndependentHash(p));
    }

    [Fact]
    public void Changing_the_input_collections_after_creation_changes_neither_the_profile_nor_its_hash()
    {
        var draft = TuningProfileTests.Draft();
        var p = TuningProfile.Create(draft).Value;
        var digestBefore = ResultDigest(p);

        // the reader hands out List instances: write to every nested one after the profile exists
        if (draft.Baselines is List<BaselineRowDraft> rows)
        {
            foreach (var row in rows)
                if (row.Outcomes is List<OutcomeDraft> outcomes)
                    outcomes[0] = outcomes[0] with { Ppm = 0 };
            rows.Clear();
        }

        if (draft.Modifiers is List<ModifierDraft> modifiers)
        {
            foreach (var m in modifiers)
                if (m.Table is List<TableEntryDraft> table && table.Count > 0)
                    table[0] = table[0] with { FactorPpm = 1_250_000 };
            modifiers.Clear();
        }

        (draft.Scenarios as List<ScenarioDraft>)?.Clear();
        (draft.Targets as List<TargetDraft>)?.Clear();

        Assert.Equal(digestBefore, ResultDigest(p));
        Assert.Equal(TuningProfileTests.GoldenHash, p.ContentHash);
        Assert.Equal(TuningProfileTests.GoldenHash, IndependentHash(p));
    }

    [Fact]
    public void Tampering_with_one_loaded_profile_does_not_affect_another_consumer_of_the_same_data()
    {
        var first = TuningProfileTests.Profile();
        var second = TuningProfileTests.Profile();
        var expected = ResultDigest(second);

        Tamper(first);

        Assert.Equal(expected, ResultDigest(second));
        Assert.Equal(expected, ResultDigest(TuningProfileTests.Profile()));
        Assert.Equal(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void The_trace_and_result_of_a_stage_cannot_be_used_to_change_later_results()
    {
        var p = TuningProfileTests.Profile();
        var result = Run(p, StageIds.SwingResult, "in.r1c1", ModifierInput.Linear("batter.contact", 70));
        var before = ResultDigest(p);

        TryToWrite(result.Trace.Baseline, 0);
        TryToWrite(result.Trace.Final, 0);
        TryToWrite(result.Trace.Outcomes, "tampered");
        TryToWrite(result.Trace.CombinedFactorPpm, 0);
        TryToWrite(result.Trace.Modifiers, result.Trace.Modifiers[0]);
        TryToWrite(result.Trace.Modifiers[0].Targets, "tampered");
        TryToWrite(result.Distribution.Ppm, 0);

        Assert.Empty(ImmutabilityAudit.Violations(result));
        Assert.Equal(before, ResultDigest(p));
        var again = Run(p, StageIds.SwingResult, "in.r1c1", ModifierInput.Linear("batter.contact", 70));
        Assert.Equal(again.Distribution, result.Distribution);
        Assert.Equal(again.Trace.Final, result.Trace.Final);
        Assert.Equal(D, result.Trace.Final.Sum());
    }

    // ---- golden results: the same numbers before and after the fix ----

    // SHA-256 over the full result of every stage/condition under neutral, low-extreme and high-extreme inputs plus a mean-neutral tendency in every inner zone.
    // The pinned value was produced by the code BEFORE the immutability fix (see docs/reports/PHASE_3B2_REPORT.md part G), so equality proves unchanged results.
    internal const string GoldenResultDigest = "2ffa361ac49bc56cd7133d10916d85320c362dbfd7b54abb71df7028ef2b9988";

    private static string ResultDigest(TuningProfile p)
    {
        var lines = new List<string>();
        var innerZones = PitchZoneModel.V1.Zones.Where(z => z.Region == PitchZoneRegion.Inner).Select(z => z.Id).ToList();
        var vector = BatterZoneTendency.Create([30, -30, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).Value;
        foreach (var stage in p.Model.Stages)
        {
            ModifierInput[] Extreme(bool high) => p.Model.Slots.Where(s => s.Stage == stage.Id && !s.Reserved).Select(s => s.Kind == ModifierKind.Table
                ? ModifierInput.Choice(s.Id, s.TableOrder[high ? ^1 : 0])
                : ModifierInput.Linear(s.Id, high ? s.InputMax : s.InputMin)).ToArray();
            foreach (var condition in stage.Conditions)
            {
                foreach (var (name, inputs) in new[] { ("neutral", Array.Empty<ModifierInput>()), ("low", Extreme(false)), ("high", Extreme(true)) })
                {
                    var r = Run(p, stage.Id, condition.Key, inputs);
                    lines.Add($"{stage.Id}|{condition.Key}|{name}|{string.Join(",", r.Distribution.Ppm)}|{string.Join(",", r.Trace.CombinedFactorPpm)}");
                }

                if (stage.Id is StageIds.SwingResult or StageIds.BattedBall)
                    foreach (var zone in innerZones.Where(z => (stage.Id == StageIds.SwingResult ? z.Value : ConditionKeys.BattedBallGroup(z).Value) == condition.Key))
                    {
                        var r = StageProbability.Compute(p, stage.Id, condition.Key, null, new TendencyContext(zone, vector)).Value;
                        lines.Add($"{stage.Id}|{condition.Key}|tendency:{zone}|{string.Join(",", r.Distribution.Ppm)}|{r.Trace.GuardShiftPpm}|{r.Trace.TendencyShiftPpm}");
                    }
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)))).ToLowerInvariant();
    }

    [Fact]
    public void Normal_calculations_give_exactly_the_results_they_gave_before_the_fix()
    {
        Assert.Equal(GoldenResultDigest, ResultDigest(TuningProfileTests.Profile()));
    }

    [Fact]
    public void The_shipped_tuning_hash_and_values_are_unchanged()
    {
        var p = TuningProfileTests.Profile();
        Assert.Equal("5b70dca79ec3a3c815f741b57c4c4490e93c5bcdca19900ad452732c66d73ea5", p.ContentHash);
        Assert.Equal(60_000, p.ContactCapPpm);
        Assert.Equal(40_000, p.QualityCapPpm);
        Assert.Equal((800_000, 1_250_000), (p.CombinedMinPpm, p.CombinedMaxPpm));
        Assert.Equal([2026101101UL, 2026101102UL, 2026101103UL, 2026101104UL], p.Scenarios.Select(s => s.Seed).Order());
        Assert.All(p.Scenarios, s => Assert.Equal(200_000, s.Samples));
        Assert.Equal(8, p.Targets.Count);
    }
}
