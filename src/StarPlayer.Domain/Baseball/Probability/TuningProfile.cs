using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Baseball.Probability;

// Phase 3-B2 (ADR-006): a versioned, VIRTUAL PROTOTYPE tuning data set. Its numbers are design choices for a fictional league, NOT statistics of any real league
// (not KBO, not MLB). Every value is validated here; the JSON reader (Infrastructure) only builds a TuningProfileDraft and never decides validity.

public sealed record OutcomeDraft(string? Id, int Ppm);

public sealed record BaselineRowDraft(string? Stage, string? Condition, IReadOnlyList<OutcomeDraft>? Outcomes);

public sealed record TableEntryDraft(string? Key, int FactorPpm);

public sealed record ModifierDraft(string? Slot, int Neutral, int SlopePpmPerUnit, int MinPpm, int MaxPpm, IReadOnlyList<TableEntryDraft>? Table);

public sealed record TendencyDraft(int ContactCapPpm, int QualityCapPpm, int MinMassPpm);

public sealed record ScenarioDraft(string? Id, string? Description, string? Stage, string? Condition, ulong Seed, string? ContextKey, int Samples, string? ToleranceRule);

public sealed record TargetDraft(string? Id, string? Description, long LowPpm, long HighPpm, string? Status);

/// <summary>The unvalidated content of a tuning file. <see cref="TuningProfile.Create"/> is the only way to turn it into a usable profile.</summary>
public sealed record TuningProfileDraft(
    string? ModelVersion, string? TuningVersion, string? Status, string? Provenance, string? ZoneModel, string? ReferenceDistributionId,
    string? NormalizationPolicy, string? RoundingPolicy, int Denominator, int MinPositiveBaselinePpm, int CombinedMinPpm, int CombinedMaxPpm,
    TendencyDraft? Tendency, IReadOnlyList<BaselineRowDraft>? Baselines, IReadOnlyList<ModifierDraft>? Modifiers,
    IReadOnlyList<ScenarioDraft>? Scenarios, IReadOnlyList<TargetDraft>? Targets);

/// <summary>Validated tuning of one modifier slot. A linear slot: <c>factor = 1,000,000 + (input - Neutral) * SlopePpmPerUnit</c>. A table slot: <see cref="Table"/> in the slot's fixed order.</summary>
public sealed record ModifierTuning(string Slot, int Neutral, int SlopePpmPerUnit, int MinPpm, int MaxPpm, IReadOnlyList<KeyValuePair<string, int>> Table);

/// <summary>A declared statistical check on one baseline distribution, fixed BEFORE it is ever run: seed, context, sample count and tolerance rule.</summary>
public sealed record ScenarioSpec(string Id, string Description, string Stage, string Condition, ulong Seed, string ContextKey, int Samples, string ToleranceRule);

/// <summary>A declared aggregate calibration target (fixed before Phase 3-F runs it). Declared only; Phase 3-B2 evaluates none of them.</summary>
public sealed record TargetSpec(string Id, string Description, long LowPpm, long HighPpm, string Status);

/// <summary>
/// One validated tuning profile of <c>prob-model-v1</c> or <c>prob-model-v2</c> (each tuning version belongs to exactly one model version). Identity: <see cref="ContentHash"/>, the SHA-256 of <see cref="CanonicalText"/>, a deterministic text built
/// from the validated values in a fixed order (JSON key order, whitespace and the free-text descriptions do not matter; every number, id and the provenance do).
/// Any change of a number is therefore a new hash and, by policy, a new <see cref="TuningVersion"/>.
/// </summary>
public sealed class TuningProfile
{
    public const string VirtualStatus = "virtual_prototype";
    public const string ToleranceRuleBinomial = "binomial-4.5-sigma-ceiling-v1";
    public const string TargetStatusDeclared = "declared_for_3f_not_evaluated";
    public const int MaxCapPpm = 200_000;

    private readonly Dictionary<string, Distribution> _baselines;
    private readonly Dictionary<string, ModifierTuning> _modifiers;

    private TuningProfile(
        ProbabilityModel model, TuningVersion tuning, ZoneModelVersion zone, ZoneReferenceDistribution reference, string provenance,
        int minPositive, int combinedMin, int combinedMax, TendencyDraft tendency,
        Dictionary<string, Distribution> baselines, Dictionary<string, ModifierTuning> modifiers, List<ScenarioSpec> scenarios, List<TargetSpec> targets)
    {
        Model = model;
        Tuning = tuning;
        ZoneModel = zone;
        Reference = reference;
        Provenance = provenance;
        MinPositiveBaselinePpm = minPositive;
        CombinedMinPpm = combinedMin;
        CombinedMaxPpm = combinedMax;
        ContactCapPpm = tendency.ContactCapPpm;
        QualityCapPpm = tendency.QualityCapPpm;
        TendencyMinMassPpm = tendency.MinMassPpm;
        _baselines = baselines;
        // Frozen copies: a modifier's table is re-created as an ImmutableArray here, whatever list the validator built, so Modifier(...) can never hand out a writable list
        // and the ContentHash computed below always describes exactly the values every later calculation reads (ADR-006 item 13).
        _modifiers = modifiers.ToDictionary(m => m.Key, m => m.Value with { Table = m.Value.Table.ToImmutableArray() }, StringComparer.Ordinal);
        Scenarios = scenarios.ToImmutableArray();
        Targets = targets.ToImmutableArray();
        CanonicalText = BuildCanonical();
        ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText))).ToLowerInvariant();
    }

    public ProbabilityModel Model { get; }
    public TuningVersion Tuning { get; }
    public ZoneModelVersion ZoneModel { get; }

    /// <summary>The registered reference distribution the tendency vectors are balanced against (id and weights are one immutable definition, ADR-005 item 9).</summary>
    public ZoneReferenceDistribution Reference { get; }

    public string Provenance { get; }
    public int MinPositiveBaselinePpm { get; }

    /// <summary>The stage-wide bounds on the PRODUCT of all factors that act on one outcome (the combined cap).</summary>
    public int CombinedMinPpm { get; }

    public int CombinedMaxPpm { get; }

    /// <summary>What a tendency value of +100 means for the contact (swing_result) stage: ppm of favourable mass.</summary>
    public int ContactCapPpm { get; }

    /// <summary>What a tendency value of +100 means for the batted-ball quality stage: ppm of hard-contact mass.</summary>
    public int QualityCapPpm { get; }

    /// <summary>Smallest mass the favourable or unfavourable side of a tendency-shifted distribution may keep.</summary>
    public int TendencyMinMassPpm { get; }

    public IReadOnlyList<ScenarioSpec> Scenarios { get; }
    public IReadOnlyList<TargetSpec> Targets { get; }
    public string CanonicalText { get; }
    public string ContentHash { get; }

    public Result<Distribution> Baseline(string stage, string condition) =>
        _baselines.TryGetValue(Key(stage, condition), out var found)
            ? Result<Distribution>.Success(found)
            : Result<Distribution>.Failure(new Error("Tuning.baseline_unknown", $"이 튜닝에 없는 기준 확률표입니다: {stage} / {condition}"));

    public Result<ModifierTuning> Modifier(string slot) =>
        _modifiers.TryGetValue(slot, out var found)
            ? Result<ModifierTuning>.Success(found)
            : Result<ModifierTuning>.Failure(new Error("Tuning.modifier_unknown", $"이 튜닝에 없는 보정 슬롯입니다: {slot}"));

    public int CapFor(TendencyStage stage) => stage.CapIsContact ? ContactCapPpm : QualityCapPpm;

    private static string Key(string stage, string condition) => stage + "|" + condition;

    // ---- validation ----

    public static Result<TuningProfile> Create(TuningProfileDraft? draft)
    {
        static Result<TuningProfile> Fail(string code, string message) => Result<TuningProfile>.Failure(new Error(code, message));

        if (draft is null)
            return Fail("Tuning.missing", "튜닝 데이터가 없습니다.");
        var modelVersion = ProbabilityModelVersion.Create(draft.ModelVersion);
        if (!modelVersion.IsSuccess)
            return Result<TuningProfile>.Failure(modelVersion.Error!.Value);
        var model = ProbabilityModel.Resolve(modelVersion.Value).Value;
        var tuning = TuningVersion.Create(draft.TuningVersion);
        if (!tuning.IsSuccess)
            return Result<TuningProfile>.Failure(tuning.Error!.Value);
        // a model and a tuning belong together (v1 with v1, v2 with v2): a version is never converted to its neighbour
        if ((modelVersion.Value.Value == ProbabilityModelVersion.V2Text) != (tuning.Value.Value == TuningVersion.VirtualProtoV2Text))
            return Fail("Tuning.model_tuning_mismatch", $"튜닝 버전 '{tuning.Value}'은(는) 확률 모델 '{modelVersion.Value}'의 튜닝이 아닙니다.");
        var zone = ZoneModelVersion.Create(draft.ZoneModel);
        if (!zone.IsSuccess)
            return Result<TuningProfile>.Failure(zone.Error!.Value);
        var reference = ZoneReferenceDistribution.Resolve(draft.ReferenceDistributionId);
        if (!reference.IsSuccess)
            return Result<TuningProfile>.Failure(reference.Error!.Value);

        if (draft.Status != VirtualStatus)
            return Fail("Tuning.status_invalid", $"이 튜닝 버전은 '{VirtualStatus}' 상태만 허용합니다(실제 통계라고 주장할 수 없습니다).");
        if (string.IsNullOrWhiteSpace(draft.Provenance) || draft.Provenance.Length > 600 || draft.Provenance != draft.Provenance.Trim() || draft.Provenance.Contains('\n') || draft.Provenance.Contains('|'))
            return Fail("Tuning.provenance_invalid", "출처 설명은 앞뒤 공백·줄바꿈·'|' 없이 1~600자여야 합니다.");
        if (draft.NormalizationPolicy != ProbabilityScale.NormalizationPolicy)
            return Fail("Tuning.normalization_unsupported", $"지원하지 않는 정규화 정책입니다: '{draft.NormalizationPolicy}'");
        if (draft.RoundingPolicy != ProbabilityScale.RoundingPolicy)
            return Fail("Tuning.rounding_unsupported", $"지원하지 않는 반올림 정책입니다: '{draft.RoundingPolicy}'");
        if (draft.Denominator != ProbabilityScale.Denominator)
            return Fail("Tuning.denominator_unsupported", $"확률 분모는 {ProbabilityScale.Denominator}이어야 합니다.");

        if (draft.MinPositiveBaselinePpm is < 1 or > 10_000)
            return Fail("Tuning.min_positive_invalid", "양수 기준 확률의 최솟값은 1~10000 ppm이어야 합니다.");
        if (draft.CombinedMinPpm < 1 || draft.CombinedMinPpm > ProbabilityScale.Denominator || draft.CombinedMaxPpm < ProbabilityScale.Denominator || draft.CombinedMaxPpm > 2 * ProbabilityScale.Denominator)
            return Fail("Tuning.combined_cap_invalid", "결합 상한은 1 ≤ 하한 ≤ 1,000,000 ≤ 상한 ≤ 2,000,000 이어야 합니다.");
        if ((long)draft.CombinedMinPpm * 2 < draft.CombinedMaxPpm)
            return Fail("Tuning.combined_cap_too_wide", "결합 상한/하한의 비가 2배를 넘으면 양수 기준 확률이 0으로 사라질 수 있습니다.");

        var tendency = draft.Tendency;
        if (tendency is null)
            return Fail("Tuning.tendency_missing", "코스 성향 상한이 없습니다.");
        if (!ValidCap(tendency.ContactCapPpm) || !ValidCap(tendency.QualityCapPpm))
            return Fail("Tuning.tendency_cap_invalid", $"코스 성향 상한은 100~{MaxCapPpm}이고 100의 배수여야 합니다(성향 ±100을 정수 ppm으로 정확히 나누기 위함).");
        if (tendency.MinMassPpm < draft.MinPositiveBaselinePpm || tendency.MinMassPpm > 50_000)
            return Fail("Tuning.tendency_min_mass_invalid", "성향 적용 후 남겨야 하는 최소 질량은 양수 기준 최솟값 이상 50000 이하여야 합니다.");

        var baselines = BuildBaselines(model, draft);
        if (!baselines.IsSuccess)
            return Result<TuningProfile>.Failure(baselines.Error!.Value);
        var rules = CheckBaselineRules(baselines.Value, draft, tendency);
        if (rules is { } ruleError)
            return Result<TuningProfile>.Failure(ruleError);

        var modifiers = BuildModifiers(model, draft);
        if (!modifiers.IsSuccess)
            return Result<TuningProfile>.Failure(modifiers.Error!.Value);

        var scenarios = BuildScenarios(draft, baselines.Value);
        if (!scenarios.IsSuccess)
            return Result<TuningProfile>.Failure(scenarios.Error!.Value);
        var targets = BuildTargets(draft);
        if (!targets.IsSuccess)
            return Result<TuningProfile>.Failure(targets.Error!.Value);

        return Result<TuningProfile>.Success(new TuningProfile(
            model, tuning.Value, zone.Value, reference.Value, draft.Provenance, draft.MinPositiveBaselinePpm, draft.CombinedMinPpm, draft.CombinedMaxPpm, tendency,
            baselines.Value, modifiers.Value, scenarios.Value, targets.Value));
    }

    private static bool ValidCap(int cap) => cap is >= 100 and <= MaxCapPpm && cap % 100 == 0;

    private static Result<Dictionary<string, Distribution>> BuildBaselines(ProbabilityModel model, TuningProfileDraft draft)
    {
        static Result<Dictionary<string, Distribution>> Fail(string code, string message) => Result<Dictionary<string, Distribution>>.Failure(new Error(code, message));

        if (draft.Baselines is null)
            return Fail("Tuning.baselines_missing", "기준 확률표가 없습니다.");
        var rows = new Dictionary<string, Distribution>(StringComparer.Ordinal);
        foreach (var row in draft.Baselines)
        {
            if (row is null || row.Stage is null || row.Condition is null || row.Outcomes is null)
                return Fail("Tuning.baseline_row_invalid", "기준 확률표 행에 단계·조건·결과가 모두 있어야 합니다.");
            var stage = model.Stage(row.Stage);
            if (stage is null)
                return Fail("Tuning.baseline_stage_unknown", $"알 수 없는 확률 단계입니다: '{row.Stage}'");
            var condition = stage.Find(row.Condition);
            if (condition is null)
                return Fail("Tuning.baseline_condition_unknown", $"단계 '{row.Stage}'에 없는 조건입니다: '{row.Condition}'");
            if (!rows.TryAdd(Key(row.Stage, row.Condition), null!))
                return Fail("Tuning.baseline_duplicate", $"같은 기준 확률표 행이 두 번 있습니다: {row.Stage} / {row.Condition}");
            if (row.Outcomes.Any(o => o is null) || !row.Outcomes.Select(o => o.Id).SequenceEqual(condition.Outcomes, StringComparer.Ordinal))
                return Fail("Tuning.baseline_outcomes_mismatch", $"기준 확률표 {row.Stage} / {row.Condition}의 결과 ID와 순서는 모델이 정한 {string.Join(",", condition.Outcomes)}와 같아야 합니다.");
            var made = Distribution.Create(condition.Outcomes, row.Outcomes.Select(o => o.Ppm).ToList());
            if (!made.IsSuccess)
                return Fail(made.Error!.Value.Code, $"{row.Stage} / {row.Condition}: {made.Error.Value.Message}");
            if (made.Value.Ppm.Any(p => p > 0 && p < draft.MinPositiveBaselinePpm))
                return Fail("Tuning.baseline_positive_too_small", $"{row.Stage} / {row.Condition}: 양수 확률은 {draft.MinPositiveBaselinePpm} ppm 이상이어야 합니다(0이면 아예 불가능한 결과로 선언).");
            rows[Key(row.Stage, row.Condition)] = made.Value;
        }

        var missing = model.Stages.SelectMany(s => s.Conditions.Select(c => Key(s.Id, c.Key))).FirstOrDefault(k => !rows.ContainsKey(k));
        return missing is not null
            ? Fail("Tuning.baseline_missing", $"기준 확률표에 없는 행이 있습니다: {missing}")
            : Result<Dictionary<string, Distribution>>.Success(rows);
    }

    private static Error? CheckBaselineRules(Dictionary<string, Distribution> baselines, TuningProfileDraft draft, TendencyDraft tendency)
    {
        var zones = PitchZoneModel.V1.Zones;
        const int certain = 950_000;
        const int notDegenerate = 50_000;
        const int denominator = ProbabilityScale.Denominator;

        foreach (var zone in zones)
        {
            var decision = baselines[Key(StageIds.BatterDecision, zone.Id.Value)];
            var hbp = decision.Of("hit_by_pitch").Value;
            var mayHitBatter = zone.Region == PitchZoneRegion.Outer && zone.Lateral == ZoneLateral.Inside;
            if (hbp > 100_000 || (!mayHitBatter && hbp != 0))
                return new Error("Tuning.hbp_rule", $"몸에 맞는 공은 타자 쪽 바깥 구역에서만, 10% 이하로 허용됩니다: {zone.Id}");

            foreach (var mode in new[] { ConditionKeys.Automated, ConditionKeys.NonAutomated })
            {
                var call = baselines[Key(StageIds.CalledPitch, $"{mode}:{zone.Id.Value}")];
                var strike = call.Of("strike").Value;
                var ball = call.Of("ball").Value;
                var inner = zone.Region == PitchZoneRegion.Inner;
                if (mode == ConditionKeys.Automated)
                {
                    if (strike != (inner ? denominator : 0))
                        return new Error("Tuning.called_pitch_automated_rule", $"자동 판정에서는 스트라이크존 안 구역은 반드시 스트라이크, 밖은 반드시 볼이어야 합니다: {zone.Id}");
                    continue;
                }

                if (zone.Boundary == BoundaryClass.Clear)
                {
                    if ((inner ? strike : ball) < certain)
                        return new Error("Tuning.called_pitch_clear_rule", $"Clear 구역의 판정은 한쪽이 95% 이상 확실해야 합니다: {zone.Id}");
                }
                else if (strike < notDegenerate || ball < notDegenerate || (inner ? strike < ball : ball < strike))
                    return new Error("Tuning.called_pitch_borderline_rule", $"Borderline 구역은 두 판정 모두 5% 이상이고, 스트라이크존 안은 스트라이크가, 밖은 볼이 더 많아야 합니다: {zone.Id}");
            }

            if (zone.Region != PitchZoneRegion.Inner)
                continue;

            // Tendency window: with the cap of +-100 the favourable mass must stay inside [cap + minMass, D - cap - minMass] for every inner zone, so the shift is never clipped.
            var contact = baselines[Key(StageIds.SwingResult, zone.Id.Value)];
            var contactMass = contact.Of("foul").Value + contact.Of("in_play").Value;
            if (!InWindow(contactMass, tendency.ContactCapPpm, tendency.MinMassPpm))
                return new Error("Tuning.tendency_window_contact", $"{zone.Id}의 접촉(파울+인플레이) 기준 확률이 성향 상한을 담을 만큼 중간에 있지 않습니다.");
            var group = ConditionKeys.BattedBallGroup(zone.Id).Value;
            var batted = baselines[Key(StageIds.BattedBall, group)];
            var hardMass = batted.Of("ground_hard").Value + batted.Of("line_hard").Value + batted.Of("fly_hard").Value;
            if (!InWindow(hardMass, tendency.QualityCapPpm, tendency.MinMassPpm))
                return new Error("Tuning.tendency_window_quality", $"{zone.Id}의 강한 타구 기준 확률이 성향 상한을 담을 만큼 중간에 있지 않습니다.");
        }

        return null;
    }

    internal static bool InWindow(int mass, int cap, int minMass) => mass >= cap + minMass && mass <= ProbabilityScale.Denominator - cap - minMass;

    private static Result<Dictionary<string, ModifierTuning>> BuildModifiers(ProbabilityModel model, TuningProfileDraft draft)
    {
        static Result<Dictionary<string, ModifierTuning>> Fail(string code, string message) => Result<Dictionary<string, ModifierTuning>>.Failure(new Error(code, message));

        if (draft.Modifiers is null)
            return Fail("Tuning.modifiers_missing", "보정 계수 목록이 없습니다.");
        var result = new Dictionary<string, ModifierTuning>(StringComparer.Ordinal);
        const int neutral = ProbabilityScale.Denominator;
        foreach (var item in draft.Modifiers)
        {
            if (item is null || item.Slot is null)
                return Fail("Tuning.modifier_invalid", "보정 계수 항목에 슬롯 ID가 없습니다.");
            var slot = model.Slot(item.Slot);
            if (slot is null)
                return Fail("Tuning.modifier_slot_unknown", $"알 수 없는 보정 슬롯입니다: '{item.Slot}'");
            if (result.ContainsKey(item.Slot))
                return Fail("Tuning.modifier_duplicate", $"같은 보정 슬롯이 두 번 있습니다: '{item.Slot}'");
            var where = $"보정 슬롯 '{item.Slot}'";

            if (slot.Reserved)
            {
                if (item.Table is { Count: > 0 } || item.SlopePpmPerUnit != 0 || item.MinPpm != neutral || item.MaxPpm != neutral)
                    return Fail("Tuning.modifier_reserved_authority", $"{where}은 후속 패키지 소유라 수치 권한이 없습니다(기울기 0, 상·하한 {neutral}).");
                if (item.Neutral < slot.InputMin || item.Neutral > slot.InputMax)
                    return Fail("Tuning.modifier_neutral_invalid", $"{where}의 중립 입력은 {slot.InputMin}~{slot.InputMax} 범위여야 합니다.");
                result[item.Slot] = new ModifierTuning(item.Slot, item.Neutral, 0, neutral, neutral, ImmutableArray<KeyValuePair<string, int>>.Empty);
                continue;
            }

            if (item.MinPpm < draft.CombinedMinPpm || item.MinPpm > neutral || item.MaxPpm < neutral || item.MaxPpm > draft.CombinedMaxPpm)
                return Fail("Tuning.modifier_cap_invalid", $"{where}의 상·하한은 결합 상한 {draft.CombinedMinPpm}~{draft.CombinedMaxPpm} 안에서 {neutral}를 포함해야 합니다.");

            if (slot.Kind == ModifierKind.Linear)
            {
                if (item.Table is { Count: > 0 })
                    return Fail("Tuning.modifier_shape_invalid", $"{where}은 선형 슬롯이라 표를 가질 수 없습니다.");
                if (item.Neutral < slot.InputMin || item.Neutral > slot.InputMax)
                    return Fail("Tuning.modifier_neutral_invalid", $"{where}의 중립 입력은 {slot.InputMin}~{slot.InputMax} 범위여야 합니다.");
                if (slot.HigherInputRaisesFactor ? item.SlopePpmPerUnit < 0 : item.SlopePpmPerUnit > 0)
                    return Fail("Tuning.modifier_direction_invalid", $"{where}의 기울기 부호가 모델이 정한 영향 방향({(slot.HigherInputRaisesFactor ? "입력이 클수록 증가" : "입력이 클수록 감소")})과 다릅니다.");
                foreach (var edge in new[] { slot.InputMin, slot.InputMax })
                {
                    var factor = neutral + (long)(edge - item.Neutral) * item.SlopePpmPerUnit;
                    if (factor < item.MinPpm || factor > item.MaxPpm)
                        return Fail("Tuning.modifier_range_exceeds_cap", $"{where}: 입력 {edge}의 계수 {factor}가 자기 상·하한 {item.MinPpm}~{item.MaxPpm}을 벗어납니다.");
                }

                result[item.Slot] = new ModifierTuning(item.Slot, item.Neutral, item.SlopePpmPerUnit, item.MinPpm, item.MaxPpm, ImmutableArray<KeyValuePair<string, int>>.Empty);
                continue;
            }

            if (item.Neutral != 0 || item.SlopePpmPerUnit != 0)
                return Fail("Tuning.modifier_shape_invalid", $"{where}은 표 슬롯이라 중립 입력·기울기를 가질 수 없습니다.");
            var table = item.Table;
            if (table is null || table.Any(t => t is null) || !table.Select(t => t.Key).SequenceEqual(slot.TableOrder, StringComparer.Ordinal))
                return Fail("Tuning.modifier_table_keys", $"{where}의 표 키는 모델이 정한 순서 {string.Join(",", slot.TableOrder)}와 같아야 합니다.");
            var previous = int.MinValue;
            foreach (var entry in table)
            {
                if (entry.FactorPpm < item.MinPpm || entry.FactorPpm > item.MaxPpm)
                    return Fail("Tuning.modifier_range_exceeds_cap", $"{where}: '{entry.Key}'의 계수 {entry.FactorPpm}가 상·하한 {item.MinPpm}~{item.MaxPpm}을 벗어납니다.");
                if (entry.FactorPpm < previous)
                    return Fail("Tuning.modifier_table_order", $"{where}: 계수는 {string.Join(" ≤ ", slot.TableOrder)} 순서로 줄어들 수 없습니다.");
                previous = entry.FactorPpm;
            }

            if (table.Single(t => t.Key == "neutral").FactorPpm != neutral)
                return Fail("Tuning.modifier_neutral_not_identity", $"{where}: 'neutral'의 계수는 정확히 {neutral}이어야 합니다.");
            result[item.Slot] = new ModifierTuning(item.Slot, 0, 0, item.MinPpm, item.MaxPpm, table.Select(t => new KeyValuePair<string, int>(t.Key!, t.FactorPpm)).ToList());
        }

        var absent = model.Slots.FirstOrDefault(s => !result.ContainsKey(s.Id));
        return absent is not null
            ? Fail("Tuning.modifier_missing", $"튜닝에 없는 보정 슬롯이 있습니다: '{absent.Id}'")
            : Result<Dictionary<string, ModifierTuning>>.Success(result);
    }

    private static bool IsContextKey(string? text) =>
        text is { Length: > 0 and <= 64 } && text.All(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-' or '.' or ':');

    private static bool IsPlainId(string? text) =>
        text is { Length: > 0 and <= 64 } && text.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-' or '.');

    private static Result<List<ScenarioSpec>> BuildScenarios(TuningProfileDraft draft, Dictionary<string, Distribution> baselines)
    {
        static Result<List<ScenarioSpec>> Fail(string code, string message) => Result<List<ScenarioSpec>>.Failure(new Error(code, message));

        if (draft.Scenarios is null || draft.Scenarios.Count == 0)
            return Fail("Tuning.scenarios_missing", "사전 고정된 검증 시나리오가 하나 이상 있어야 합니다.");
        var list = new List<ScenarioSpec>();
        foreach (var s in draft.Scenarios)
        {
            if (s is null || !IsPlainId(s.Id) || s.Stage is null || s.Condition is null || s.Description is null)
                return Fail("Tuning.scenario_invalid", "검증 시나리오에는 ID·설명·단계·조건이 필요합니다.");
            if (list.Any(x => x.Id == s.Id))
                return Fail("Tuning.scenario_duplicate", $"검증 시나리오 ID가 중복되었습니다: '{s.Id}'");
            if (!baselines.ContainsKey(Key(s.Stage, s.Condition)))
                return Fail("Tuning.scenario_target_unknown", $"검증 시나리오 '{s.Id}'가 가리키는 기준 확률표가 없습니다.");
            if (!IsContextKey(s.ContextKey))
                return Fail("Tuning.scenario_context_invalid", $"검증 시나리오 '{s.Id}'의 컨텍스트 키가 올바르지 않습니다.");
            if (s.Samples is < 1_000 or > 2_000_000)
                return Fail("Tuning.scenario_samples_invalid", $"검증 시나리오 '{s.Id}'의 표본 수는 1000~2000000이어야 합니다.");
            if (s.ToleranceRule != ToleranceRuleBinomial)
                return Fail("Tuning.scenario_tolerance_unsupported", $"검증 시나리오 '{s.Id}'의 허용 규칙을 알 수 없습니다: '{s.ToleranceRule}'");
            list.Add(new ScenarioSpec(s.Id!, s.Description, s.Stage, s.Condition, s.Seed, s.ContextKey!, s.Samples, s.ToleranceRule));
        }

        return Result<List<ScenarioSpec>>.Success(list.OrderBy(s => s.Id, StringComparer.Ordinal).ToList());
    }

    private static Result<List<TargetSpec>> BuildTargets(TuningProfileDraft draft)
    {
        static Result<List<TargetSpec>> Fail(string code, string message) => Result<List<TargetSpec>>.Failure(new Error(code, message));

        if (draft.Targets is null)
            return Fail("Tuning.targets_missing", "사전 선언 통계 목표 목록이 없습니다.");
        var list = new List<TargetSpec>();
        foreach (var t in draft.Targets)
        {
            if (t is null || !IsPlainId(t.Id) || t.Description is null)
                return Fail("Tuning.target_invalid", "통계 목표에는 ID와 설명이 필요합니다.");
            if (list.Any(x => x.Id == t.Id))
                return Fail("Tuning.target_duplicate", $"통계 목표 ID가 중복되었습니다: '{t.Id}'");
            if (t.LowPpm < 0 || t.HighPpm > ProbabilityScale.Denominator * 100L || t.LowPpm > t.HighPpm)
                return Fail("Tuning.target_range_invalid", $"통계 목표 '{t.Id}'의 허용 범위가 올바르지 않습니다.");
            if (t.Status != TargetStatusDeclared)
                return Fail("Tuning.target_status_invalid", $"통계 목표 '{t.Id}'는 '{TargetStatusDeclared}' 상태여야 합니다(3-B2는 평가하지 않습니다).");
            list.Add(new TargetSpec(t.Id!, t.Description, t.LowPpm, t.HighPpm, t.Status));
        }

        return Result<List<TargetSpec>>.Success(list.OrderBy(t => t.Id, StringComparer.Ordinal).ToList());
    }

    // ---- identity ----

    private string BuildCanonical()
    {
        static string N(long value) => value.ToString(CultureInfo.InvariantCulture);
        var lines = new List<string>
        {
            "tuning-profile-canonical-v1",
            $"model={Model.Version}",
            $"tuning={Tuning}",
            $"status={VirtualStatus}",
            $"zone_model={ZoneModel}",
            $"reference={Reference.Id}|{string.Join(",", Reference.Weights.Select(w => N(w)))}",
            $"normalization={ProbabilityScale.NormalizationPolicy}",
            $"rounding={ProbabilityScale.RoundingPolicy}",
            $"denominator={N(ProbabilityScale.Denominator)}",
            $"min_positive_baseline_ppm={N(MinPositiveBaselinePpm)}",
            $"combined_ppm={N(CombinedMinPpm)},{N(CombinedMaxPpm)}",
            $"tendency_caps_ppm=contact:{N(ContactCapPpm)}|quality:{N(QualityCapPpm)}|min_mass:{N(TendencyMinMassPpm)}",
            $"provenance={Provenance}",
        };
        foreach (var stage in Model.Stages)
            foreach (var condition in stage.Conditions)
            {
                var d = _baselines[Key(stage.Id, condition.Key)];
                lines.Add($"baseline|{stage.Id}|{condition.Key}|{string.Join(",", d.Outcomes.Select((o, i) => $"{o}={N(d.Ppm[i])}"))}");
            }

        foreach (var slot in Model.Slots)
        {
            var m = _modifiers[slot.Id];
            lines.Add(slot.Kind == ModifierKind.Linear
                ? $"modifier|{slot.Id}|linear|neutral={N(m.Neutral)}|slope={N(m.SlopePpmPerUnit)}|min={N(m.MinPpm)}|max={N(m.MaxPpm)}"
                : $"modifier|{slot.Id}|table|min={N(m.MinPpm)}|max={N(m.MaxPpm)}|{string.Join(",", m.Table.Select(t => $"{t.Key}={N(t.Value)}"))}");
        }

        foreach (var s in Scenarios)
            lines.Add($"scenario|{s.Id}|{s.Stage}|{s.Condition}|seed={s.Seed.ToString(CultureInfo.InvariantCulture)}|context={s.ContextKey}|samples={N(s.Samples)}|rule={s.ToleranceRule}");
        foreach (var t in Targets)
            lines.Add($"target|{t.Id}|{N(t.LowPpm)}|{N(t.HighPpm)}|{t.Status}");
        return string.Join("\n", lines);
    }
}
