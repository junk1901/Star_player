using System.Text.Json;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;

namespace StarPlayer.Infrastructure.Fixtures;

// JSON for the Phase 3-B2 virtual tuning data (content/tuning/virtual_tuning_v1.json, ADR-006). Same conventions as the other fixtures: snake_case, unknown members
// rejected. This reader only builds a TuningProfileDraft; every validity decision (versions, caps, sums, directions) is TuningProfile.Create's.

internal sealed class TuningFileDto
{
    public required int FormatVersion { get; set; }
    public required string ModelVersion { get; set; }
    public required string TuningVersion { get; set; }
    public required string Status { get; set; }
    public required string Provenance { get; set; }
    public required string ZoneModel { get; set; }
    public required string ReferenceDistributionId { get; set; }
    public required string NormalizationPolicy { get; set; }
    public required string RoundingPolicy { get; set; }
    public required int Denominator { get; set; }
    public required int MinPositiveBaselinePpm { get; set; }
    public required int CombinedMinPpm { get; set; }
    public required int CombinedMaxPpm { get; set; }
    public required TuningTendencyDto Tendency { get; set; }
    public required List<TuningBaselineDto> Baselines { get; set; }
    public required List<TuningModifierDto> Modifiers { get; set; }
    public required List<TuningScenarioDto> Scenarios { get; set; }
    public required List<TuningTargetDto> Targets { get; set; }
}

internal sealed class TuningTendencyDto
{
    public required int ContactCapPpm { get; set; }
    public required int QualityCapPpm { get; set; }
    public required int MinMassPpm { get; set; }
}

internal sealed class TuningBaselineDto
{
    public required string Stage { get; set; }
    public required string Condition { get; set; }
    public required List<string> Outcomes { get; set; }
    public required List<int> Ppm { get; set; }
}

internal sealed class TuningModifierDto
{
    public required string Slot { get; set; }
    public required int Neutral { get; set; }
    public required int SlopePpmPerUnit { get; set; }
    public required int MinPpm { get; set; }
    public required int MaxPpm { get; set; }
    public required List<TuningTableEntryDto>? Table { get; set; }
}

internal sealed class TuningTableEntryDto
{
    public required string Key { get; set; }
    public required int FactorPpm { get; set; }
}

internal sealed class TuningScenarioDto
{
    public required string Id { get; set; }
    public required string Description { get; set; }
    public required string Stage { get; set; }
    public required string Condition { get; set; }
    public required ulong Seed { get; set; }
    public required string ContextKey { get; set; }
    public required int Samples { get; set; }
    public required string ToleranceRule { get; set; }
}

internal sealed class TuningTargetDto
{
    public required string Id { get; set; }
    public required string Description { get; set; }
    public required long LowPpm { get; set; }
    public required long HighPpm { get; set; }
    public required string Status { get; set; }
}

internal static class TuningProfileJson
{
    public const int FormatVersion = 1;
    public const string ResourceName = "virtual_tuning_v1.json";
    public const string ResourceNameV2 = "virtual_tuning_v2.json";

    public static Result<TuningProfile> Read(string json)
    {
        var draft = ReadDraft(json);
        return draft.IsSuccess ? TuningProfile.Create(draft.Value) : Result<TuningProfile>.Failure(draft.Error!.Value);
    }

    /// <summary>Parses the file into the unvalidated draft (JSON shape errors only). Tests mutate the draft to prove each validation rule.</summary>
    public static Result<TuningProfileDraft> ReadDraft(string json)
    {
        static Result<TuningProfileDraft> Fail(string code, string message) => Result<TuningProfileDraft>.Failure(new Error(code, message));
        try
        {
            var dto = JsonSerializer.Deserialize<TuningFileDto>(json, WorldJson.Options);
            if (dto is null)
                return Fail("Fixture.json_invalid", "JSON 내용이 비어 있습니다.");
            if (dto.FormatVersion != FormatVersion)
                return Fail("Fixture.version_unsupported", $"지원하지 않는 튜닝 파일 형식 버전입니다: {dto.FormatVersion}");
            if (MissingPart(dto) is { } missing)
                return Fail("Fixture.field_missing", $"튜닝 파일에 필수 항목이 비어 있습니다(null): {missing}");
            foreach (var row in dto.Baselines)
                if (row.Outcomes.Count != row.Ppm.Count)
                    return Fail("Fixture.shape_invalid", $"기준 확률표 {row.Stage}/{row.Condition}의 결과 목록과 ppm 목록 길이가 다릅니다.");
            return Result<TuningProfileDraft>.Success(ToDraft(dto));
        }
        catch (JsonException e)
        {
            return Fail("Fixture.json_invalid", $"JSON 형식이 올바르지 않습니다: {e.Message}");
        }
    }

    // System.Text.Json accepts an explicit null for a member declared `required` (it only insists the member is PRESENT), so every required object, array and array element is
    // checked here; a string member that is null is left to TuningProfile.Create, which already rejects it with its own stable code. `table` is optional by schema (null for a
    // linear modifier), its entries are not. Returns the path of the first empty part, or null.
    private static string? MissingPart(TuningFileDto d)
    {
        if (d.Tendency is null)
            return "tendency";
        if (d.Baselines is null) return "baselines";
        for (var i = 0; i < d.Baselines.Count; i++)
        {
            var row = d.Baselines[i];
            if (row is null) return $"baselines[{i}]";
            if (row.Outcomes is null) return $"baselines[{i}].outcomes";
            if (row.Ppm is null) return $"baselines[{i}].ppm";
        }

        if (d.Modifiers is null) return "modifiers";
        for (var i = 0; i < d.Modifiers.Count; i++)
        {
            var m = d.Modifiers[i];
            if (m is null) return $"modifiers[{i}]";
            if (m.Table is not null)
                for (var j = 0; j < m.Table.Count; j++)
                    if (m.Table[j] is null) return $"modifiers[{i}].table[{j}]";
        }

        if (d.Scenarios is null) return "scenarios";
        for (var i = 0; i < d.Scenarios.Count; i++)
            if (d.Scenarios[i] is null) return $"scenarios[{i}]";
        if (d.Targets is null) return "targets";
        for (var i = 0; i < d.Targets.Count; i++)
            if (d.Targets[i] is null) return $"targets[{i}]";
        return null;
    }

    /// <summary>The embedded virtual tuning set shipped with this build (<c>tuning-virtual-proto-v1</c>).</summary>
    public static Result<TuningProfile> LoadVirtualV1() => Read(ScriptedGameSource.ReadResource(ResourceName));

    /// <summary>The embedded virtual tuning set of <c>prob-model-v2</c> (<c>tuning-virtual-proto-v2</c>, Phase 3-D): v1 plus the three active park slots. v1 is a separate, unchanged file.</summary>
    public static Result<TuningProfile> LoadVirtualV2() => Read(ScriptedGameSource.ReadResource(ResourceNameV2));

    internal static TuningProfileDraft ToDraft(TuningFileDto d) => new(
        d.ModelVersion, d.TuningVersion, d.Status, d.Provenance, d.ZoneModel, d.ReferenceDistributionId, d.NormalizationPolicy, d.RoundingPolicy,
        d.Denominator, d.MinPositiveBaselinePpm, d.CombinedMinPpm, d.CombinedMaxPpm,
        new TendencyDraft(d.Tendency.ContactCapPpm, d.Tendency.QualityCapPpm, d.Tendency.MinMassPpm),
        d.Baselines.Select(b => new BaselineRowDraft(b.Stage, b.Condition, b.Outcomes.Select((o, i) => new OutcomeDraft(o, b.Ppm[i])).ToList())).ToList(),
        d.Modifiers.Select(m => new ModifierDraft(m.Slot, m.Neutral, m.SlopePpmPerUnit, m.MinPpm, m.MaxPpm,
            m.Table?.Select(t => new TableEntryDraft(t.Key, t.FactorPpm)).ToList())).ToList(),
        d.Scenarios.Select(s => new ScenarioDraft(s.Id, s.Description, s.Stage, s.Condition, s.Seed, s.ContextKey, s.Samples, s.ToleranceRule)).ToList(),
        d.Targets.Select(t => new TargetDraft(t.Id, t.Description, t.LowPpm, t.HighPpm, t.Status)).ToList());

}
