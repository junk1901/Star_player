using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;

namespace StarPlayer.SimHarness;

/// <summary>
/// The two authoritative content files a harness run is pinned to: <c>content/tuning/virtual_tuning_v4.json</c> (tuning-virtual-proto-v4, model prob-model-v4) and
/// <c>content/rules/game_rules_profiles_v2.json</c> (gamerules-v2). They are embedded UNCHANGED, so the numbers live in the content file only. The Infrastructure readers of the same files
/// are internal to the trusted boundary and the harness must not see them, so this is a small second reader; every validity decision is still the Domain's
/// (<see cref="TuningProfile.Create"/>, <see cref="GameRuleSnapshot.Create"/>), and a test pins that both readers give the same content hash.
/// <para>
/// The two readers share ONE input contract (Codex F-02): the DTOs here mirror the Infrastructure DTOs member for member, with the same <c>required</c> members (a missing member is refused, never read as 0),
/// the same explicit-null checks of required objects, arrays and array elements, the same duplicate-profile refusal and the same exact snake_case enum text (no case folding, no numbers, no aliases).
/// A conformance matrix in the Infrastructure tests runs both readers over the same defective files. The harness reader is narrower in exactly one way: it reads only rules format 2, the format the pinned
/// profiles have.
/// </para>
/// </summary>
public static class HarnessData
{
    public const string TuningResource = "virtual_tuning_v4.json";
    public const string RulesResource = "game_rules_profiles_v2.json";
    public const string NonAutomatedProfile = "test-pzl-non-automated";
    public const string AutomatedProfile = "test-pzl-automated";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    public static string ReadResource(string name)
    {
        using var stream = typeof(HarnessData).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"내장 데이터 '{name}'이(가) 어셈블리에 없습니다.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>SHA-256 of the embedded file's UTF-8 text (line endings as embedded): the provenance of the exact content a run read.</summary>
    public static string ResourceHash(string name) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ReadResource(name)))).ToLowerInvariant();

    public static Result<TuningProfile> LoadTuningV4() => ParseTuning(ReadResource(TuningResource));

    /// <summary>The neutral park every harness run uses (<c>pf-sim-neutral</c> v1, all factors 1000).</summary>
    public static Result<ParkFactorSnapshot> NeutralPark()
    {
        var park = ParkFactorProfile.Create(ParkFactorProfileId.Create("pf-sim-neutral").Value, StadiumId.Create("stadium-sim-neutral").Value, 1, new DateOnly(2030, 1, 1),
            ParkFactor.Neutral, ParkFactor.Neutral, ParkFactor.Neutral, ParkFactorBounds.Create(1, ParkFactorBounds.AbsoluteMaxPermille).Value);
        return park.IsSuccess ? Result<ParkFactorSnapshot>.Success(park.Value.ToSnapshot()) : Result<ParkFactorSnapshot>.Failure(park.Error!.Value);
    }

    public static Result<GameRuleSnapshot> LoadRules(string profileId)
    {
        var all = ParseRules(ReadResource(RulesResource));
        if (!all.IsSuccess)
            return Result<GameRuleSnapshot>.Failure(all.Error!.Value);
        var found = all.Value.FirstOrDefault(r => r.ProfileId == profileId);
        return found is null
            ? Result<GameRuleSnapshot>.Failure(new Error("Harness.rules_profile_unknown", $"규칙 프로필 '{profileId}'이(가) 없습니다."))
            : Result<GameRuleSnapshot>.Success(found);
    }

    public static Result<TuningProfile> ParseTuning(string json)
    {
        try
        {
            var d = JsonSerializer.Deserialize<TuningFileDto>(json, Options);
            if (d is null)
                return Result<TuningProfile>.Failure(new Error("Harness.tuning_shape_invalid", "튜닝 파일 내용이 비어 있습니다."));
            if (d.FormatVersion != 1)
                return Result<TuningProfile>.Failure(new Error("Harness.tuning_shape_invalid", $"지원하지 않는 튜닝 파일 형식 버전입니다: {d.FormatVersion}"));
            if (MissingPart(d) is { } missing)
                return Result<TuningProfile>.Failure(new Error("Harness.tuning_shape_invalid", $"튜닝 파일에 필수 항목이 비어 있습니다(null): {missing}"));
            foreach (var row in d.Baselines)
                if (row.Outcomes.Count != row.Ppm.Count)
                    return Result<TuningProfile>.Failure(new Error("Harness.tuning_shape_invalid", $"기준 확률표 {row.Stage}/{row.Condition}의 결과 목록과 ppm 목록 길이가 다릅니다."));
            return TuningProfile.Create(new TuningProfileDraft(d.ModelVersion, d.TuningVersion, d.Status, d.Provenance, d.ZoneModel, d.ReferenceDistributionId, d.NormalizationPolicy, d.RoundingPolicy,
                d.Denominator, d.MinPositiveBaselinePpm, d.CombinedMinPpm, d.CombinedMaxPpm, new TendencyDraft(d.Tendency.ContactCapPpm, d.Tendency.QualityCapPpm, d.Tendency.MinMassPpm),
                d.Baselines.Select(b => new BaselineRowDraft(b.Stage, b.Condition, b.Outcomes.Select((o, i) => new OutcomeDraft(o, b.Ppm[i])).ToList())).ToList(),
                d.Modifiers.Select(m => new ModifierDraft(m.Slot, m.Neutral, m.SlopePpmPerUnit, m.MinPpm, m.MaxPpm, m.Table?.Select(t => new TableEntryDraft(t.Key, t.FactorPpm)).ToList())).ToList(),
                d.Scenarios.Select(s => new ScenarioDraft(s.Id, s.Description, s.Stage, s.Condition, s.Seed, s.ContextKey, s.Samples, s.ToleranceRule)).ToList(),
                d.Targets.Select(t => new TargetDraft(t.Id, t.Description, t.LowPpm, t.HighPpm, t.Status)).ToList()));
        }
        catch (JsonException e)
        {
            return Result<TuningProfile>.Failure(new Error("Harness.tuning_json_invalid", $"튜닝 JSON 형식이 올바르지 않습니다: {e.Message}"));
        }
    }

    // System.Text.Json accepts an explicit null for a member declared `required` (it only insists the member is PRESENT), so every required object, array and array element is checked here, exactly as the
    // Infrastructure reader does; a string member that is null is left to TuningProfile.Create. `table` is optional (null for a linear modifier), its entries are not. Returns the path of the first empty part.
    private static string? MissingPart(TuningFileDto d)
    {
        if (d.Tendency is null) return "tendency";
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

    public static Result<IReadOnlyList<GameRuleSnapshot>> ParseRules(string json)
    {
        static Result<IReadOnlyList<GameRuleSnapshot>> Fail(string code, string message) => Result<IReadOnlyList<GameRuleSnapshot>>.Failure(new Error(code, message));
        try
        {
            var d = JsonSerializer.Deserialize<RulesFileDto>(json, Options);
            if (d is null || d.FormatVersion != 2 || d.Profiles is null || d.Profiles.Any(p => p is null))
                return Fail("Harness.rules_shape_invalid", "규칙 프로필 파일은 format_version 2이고 프로필 목록(빈 항목 없음)이 있어야 합니다.");
            var list = new List<GameRuleSnapshot>();
            foreach (var p in d.Profiles)
            {
                if (!TryEnum<GameRulesStatus>(p.Status, out var status) || !TryEnum<CalledPitchMode>(p.CalledPitchMode, out var mode))
                    return Fail("Harness.rules_enum_invalid", $"프로필 '{p.ProfileId}'의 status 또는 called_pitch_mode 값이 올바르지 않습니다.");
                var zone = ZoneModelVersion.Create(p.ZoneModel);
                if (!zone.IsSuccess)
                    return Result<IReadOnlyList<GameRuleSnapshot>>.Failure(zone.Error!.Value);
                var made = GameRuleSnapshot.Create(p.ProfileId, p.Version, status, p.Provenance, p.OpenItems, p.OutsPerHalfInning, p.BallsForWalk, p.StrikesForStrikeout, p.RegulationInnings,
                    p.ExtraInningsLimit, p.TieAllowed, p.DesignatedHitter, p.TwoWayPlayerRule, p.StarterWinMinOuts, p.SaveMaxLead, p.SaveMinOuts, p.SaveLongOuts, p.DhMayBeForfeited,
                    p.CountWalkOffRunnersAsLeftOnBase, p.FielderToMoundForfeitsDh, p.RulePolicy, mode, zone.Value);
                if (!made.IsSuccess)
                    return Result<IReadOnlyList<GameRuleSnapshot>>.Failure(made.Error!.Value);
                list.Add(made.Value);
            }

            if (list.Select(r => r.ProfileId).Distinct().Count() != list.Count)
                return Fail("Harness.rules_duplicate_id", "규칙 프로필 ID가 중복되었습니다(첫 항목을 임의로 고르지 않음).");
            return Result<IReadOnlyList<GameRuleSnapshot>>.Success(list);
        }
        catch (JsonException e)
        {
            return Fail("Harness.rules_json_invalid", $"규칙 JSON 형식이 올바르지 않습니다: {e.Message}");
        }
    }

    /// <summary>The exact snake_case spelling of an enum member (<c>fictional_test</c>), as the Infrastructure reader requires: no case folding, no numeric text, no alias.</summary>
    private static bool TryEnum<T>(string? text, out T value) where T : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<T>())
            if (SnakeCase.From(candidate.ToString()) == text)
            {
                value = candidate;
                return true;
            }

        value = default;
        return false;
    }

    // Mirrors of the Infrastructure DTOs (TuningProfileJson.cs, ScriptedGameJson.cs): the same members, the same `required` set.
    private sealed class TuningFileDto
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
        public required TendencyDto Tendency { get; set; }
        public required List<BaselineDto> Baselines { get; set; }
        public required List<ModifierDto> Modifiers { get; set; }
        public required List<ScenarioDto> Scenarios { get; set; }
        public required List<TargetDto> Targets { get; set; }
    }

    private sealed class TendencyDto
    {
        public required int ContactCapPpm { get; set; }
        public required int QualityCapPpm { get; set; }
        public required int MinMassPpm { get; set; }
    }

    private sealed class BaselineDto
    {
        public required string Stage { get; set; }
        public required string Condition { get; set; }
        public required List<string> Outcomes { get; set; }
        public required List<int> Ppm { get; set; }
    }

    private sealed class ModifierDto
    {
        public required string Slot { get; set; }
        public required int Neutral { get; set; }
        public required int SlopePpmPerUnit { get; set; }
        public required int MinPpm { get; set; }
        public required int MaxPpm { get; set; }
        public required List<TableEntryDto>? Table { get; set; }
    }

    private sealed class TableEntryDto
    {
        public required string Key { get; set; }
        public required int FactorPpm { get; set; }
    }

    private sealed class ScenarioDto
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

    private sealed class TargetDto
    {
        public required string Id { get; set; }
        public required string Description { get; set; }
        public required long LowPpm { get; set; }
        public required long HighPpm { get; set; }
        public required string Status { get; set; }
    }

    private sealed class RulesFileDto
    {
        public required int FormatVersion { get; set; }
        public required string Note { get; set; }
        public required List<RuleDto> Profiles { get; set; }
    }

    private sealed class RuleDto
    {
        public required string ProfileId { get; set; }
        public required string Version { get; set; }
        public required string Status { get; set; }
        public required string Provenance { get; set; }
        public required List<string> OpenItems { get; set; }
        public required int OutsPerHalfInning { get; set; }
        public required int BallsForWalk { get; set; }
        public required int StrikesForStrikeout { get; set; }
        public required int RegulationInnings { get; set; }
        public required int? ExtraInningsLimit { get; set; }
        public required bool TieAllowed { get; set; }
        public required bool DesignatedHitter { get; set; }
        public required bool TwoWayPlayerRule { get; set; }
        public required int StarterWinMinOuts { get; set; }
        public required int SaveMaxLead { get; set; }
        public required int SaveMinOuts { get; set; }
        public required int SaveLongOuts { get; set; }
        public required bool DhMayBeForfeited { get; set; }
        public required bool CountWalkOffRunnersAsLeftOnBase { get; set; }
        public required bool FielderToMoundForfeitsDh { get; set; }
        public required string RulePolicy { get; set; }
        public required string CalledPitchMode { get; set; }
        public required string ZoneModel { get; set; }
    }
}
