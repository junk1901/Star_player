using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Baseball.Probability;

// Phase 3-B2 (ADR-006): computes ONE stage's conditional distribution from a tuning profile. It is a pure function of its arguments: no random source, no clock, no
// game state, no event, no reducer. It is the contract the later resolver (3-C/3-D) will call; Phase 3-B2 wires it to no game.

/// <summary>
/// The caller's value for one modifier slot. A slot that is not listed is neutral. Linear slots read <see cref="Value"/> (a 0-100 skill, or a permille), table slots read
/// <see cref="Key"/>. The value is only ever an INPUT: the model never writes it back, so a player's stored ratings cannot change.
/// </summary>
public sealed record ModifierInput(string Slot, int Value = 0, string? Key = null)
{
    public static ModifierInput Linear(string slot, int value) => new(slot, value);

    public static ModifierInput Choice(string slot, string key) => new(slot, 0, key);
}

/// <summary>
/// The pitch's zone and the batter's hidden tendency, for the two stages a tendency may shift (<c>swing_result</c>, <c>batted_ball</c>). Supplying a context turns on the
/// tendency-safe window guard even for <see cref="BatterZoneTendency.Neutral"/>, so "no tendency" and "all-zero tendency" are the same computation. Resolver-internal:
/// this type never appears in an Application or Infrastructure signature (a test pins it).
/// </summary>
public sealed record TendencyContext(PitchZoneId Zone, BatterZoneTendency Tendency);

/// <summary>What one applied (or listed but not applicable) modifier contributed, for the trace.</summary>
public sealed record TraceModifier(string Slot, string Input, int FactorPpm, bool Applied, IReadOnlyList<string> Targets);

/// <summary>
/// The calculation record of one stage distribution (resolver-internal; never a UI, Gemini, news or saved-read-model payload). Lists, in order: versions and the zone
/// model/reference, the baseline, every modifier with its factor, the per-outcome combined factor (and whether the combined cap clipped it), the distribution after
/// modifiers and normalization, the tendency-window guard shift and tendency shift (signed ppm of favourable mass), and the final distribution.
/// </summary>
public sealed record ProbabilityTrace(
    string ModelVersion, string TuningVersion, string TuningHash, string ZoneModel, string ReferenceId, string NormalizationPolicy,
    string Stage, string Condition, IReadOnlyList<string> Outcomes, IReadOnlyList<int> Baseline, IReadOnlyList<TraceModifier> Modifiers,
    IReadOnlyList<int> CombinedFactorPpm, IReadOnlyList<bool> CombinedCapped, IReadOnlyList<int> AfterModifiers,
    int GuardShiftPpm, int TendencyShiftPpm, IReadOnlyList<int> Final)
{
    public override string ToString() => $"ProbabilityTrace({Stage}/{Condition} {ModelVersion} {TuningVersion})";
}

public sealed record StageResult(Distribution Distribution, ProbabilityTrace Trace);

public static class StageProbability
{
    private const int Neutral = ProbabilityScale.Denominator;

    /// <summary>
    /// Order of work: (1) the baseline row; (2) each supplied modifier's factor, range-checked; (3) per outcome, the factors of the slots that target it are multiplied in the
    /// model's slot order with half-up rounding and clipped to the profile's combined cap; (4) weights = baseline x combined factor, normalized to exactly the denominator
    /// by the profile's policy (a zero baseline stays exactly zero); (5) only when a <see cref="TendencyContext"/> is given: the favourable mass is first guarded into the
    /// tendency-safe window, then shifted by <c>tendency value x cap / 100</c> ppm, taken from or given to the other outcomes in proportion to their probabilities, with
    /// both sides keeping their exact totals; (6) the result is re-validated (sum, range, no baseline-positive outcome lost).
    /// </summary>
    public static Result<StageResult> Compute(
        TuningProfile profile, string stage, string condition, IReadOnlyList<ModifierInput>? inputs = null, TendencyContext? tendency = null)
    {
        static Result<StageResult> Fail(Error error) => Result<StageResult>.Failure(error);
        static Result<StageResult> FailText(string code, string message) => Result<StageResult>.Failure(new Error(code, message));

        if (profile is null)
            return FailText("Tuning.missing", "튜닝 프로필이 없습니다.");
        var model = profile.Model;
        var stageDef = model.Stage(stage);
        if (stageDef is null)
            return FailText("Probability.stage_unknown", $"알 수 없는 확률 단계입니다: '{stage}'");
        var conditionDef = stageDef.Find(condition);
        if (conditionDef is null)
            return FailText("Probability.condition_unknown", $"단계 '{stage}'에 없는 조건입니다: '{condition}'");
        var baselineResult = profile.Baseline(stage, condition);
        if (!baselineResult.IsSuccess)
            return Fail(baselineResult.Error!.Value);
        var baseline = baselineResult.Value;
        var outcomes = conditionDef.Outcomes;

        // (2) validate and evaluate the supplied inputs
        var factors = new Dictionary<string, (ModifierSlot Slot, ModifierInput Input, int Factor, bool Applied)>(StringComparer.Ordinal);
        foreach (var input in inputs ?? [])
        {
            if (input is null || input.Slot is null)
                return FailText("Modifier.input_invalid", "보정 입력에 슬롯 ID가 없습니다.");
            var slot = model.Slot(input.Slot);
            if (slot is null)
                return FailText("Modifier.slot_unknown", $"알 수 없는 보정 슬롯입니다: '{input.Slot}'");
            if (slot.Stage != stage)
                return FailText("Modifier.slot_stage_mismatch", $"보정 슬롯 '{slot.Id}'는 '{stage}' 단계에 쓸 수 없습니다(소속 단계: {slot.Stage}).");
            if (factors.ContainsKey(slot.Id))
                return FailText("Modifier.duplicate_input", $"같은 보정 슬롯 '{slot.Id}'가 두 번 입력되어 같은 효과가 중복 적용될 수 있습니다.");
            var tuning = profile.Modifier(slot.Id).Value;
            var evaluated = Evaluate(slot, tuning, input);
            if (!evaluated.IsSuccess)
                return Fail(evaluated.Error!.Value);
            var applies = AppliesTo(slot, stageDef, condition);
            factors[slot.Id] = (slot, input, applies ? evaluated.Value : Neutral, applies);
        }

        // (3) per-outcome combined factor, fixed slot order
        var combined = new int[outcomes.Count];
        var capped = new bool[outcomes.Count];
        for (var i = 0; i < outcomes.Count; i++)
        {
            long running = Neutral;
            foreach (var slot in model.Slots)
            {
                if (!factors.TryGetValue(slot.Id, out var entry) || entry.Factor == Neutral || !Targets(slot, outcomes[i], condition))
                    continue;
                var next = ProbabilityScale.MultiplyRatio(running, entry.Factor, Neutral);
                if (!next.IsSuccess)
                    return Fail(next.Error!.Value);
                running = next.Value;
            }

            var clipped = Math.Clamp(running, profile.CombinedMinPpm, profile.CombinedMaxPpm);
            capped[i] = clipped != running;
            combined[i] = (int)clipped;
        }

        // (4) weights and normalization
        var weights = new long[outcomes.Count];
        for (var i = 0; i < weights.Length; i++)
            weights[i] = (long)baseline.Ppm[i] * combined[i];
        var normalized = ProbabilityScale.Normalize(weights);
        if (!normalized.IsSuccess)
            return Fail(normalized.Error!.Value);
        var afterModifiers = normalized.Value;
        var current = afterModifiers;

        // (5) tendency window guard and shift
        var guardShift = 0;
        var tendencyShift = 0;
        if (tendency is not null)
        {
            var applied = ApplyTendency(profile, stage, condition, outcomes, current, tendency);
            if (!applied.IsSuccess)
                return Fail(applied.Error!.Value);
            (current, guardShift, tendencyShift) = applied.Value;
        }

        // (6) final invariants
        var distribution = Distribution.Create(outcomes, current);
        if (!distribution.IsSuccess)
            return Fail(distribution.Error!.Value);
        for (var i = 0; i < outcomes.Count; i++)
            if (baseline.Ppm[i] > 0 && current[i] <= 0)
                return FailText("Probability.positive_outcome_lost", $"기준 확률이 양수인 결과 '{outcomes[i]}'가 보정 뒤 0이 되었습니다(유효 확률 불변식 위반).");

        var trace = new ProbabilityTrace(
            model.Version.Value, profile.Tuning.Value, profile.ContentHash, profile.ZoneModel.Value, profile.Reference.Id, ProbabilityScale.NormalizationPolicy,
            stage, condition, outcomes.ToImmutableArray(), baseline.Ppm.ToImmutableArray(),
            model.Slots.Where(s => factors.ContainsKey(s.Id)).Select(s => Describe(factors[s.Id])).ToImmutableArray(),
            combined.ToImmutableArray(), capped.ToImmutableArray(), afterModifiers.ToImmutableArray(), guardShift, tendencyShift, current.ToImmutableArray());
        return Result<StageResult>.Success(new StageResult(distribution.Value, trace));
    }

    private static TraceModifier Describe((ModifierSlot Slot, ModifierInput Input, int Factor, bool Applied) e) =>
        new(e.Slot.Id, e.Slot.Kind == ModifierKind.Table ? e.Input.Key ?? "" : e.Input.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            e.Factor, e.Applied, e.Slot.Targets.ToImmutableArray());

    private static bool Targets(ModifierSlot slot, string outcome, string condition) =>
        slot.Targets.Any(t => t == ProbabilityModel.CondTarget ? outcome == condition : t == outcome);

    private static bool AppliesTo(ModifierSlot slot, StageDef stage, string condition)
    {
        if (slot.Scope == ZoneScope.All)
            return true;
        var zone = PitchZoneId.Create(condition);
        if (!zone.IsSuccess)
            return true; // a scope is defined only for zone-keyed stages (the model never gives a scope to another stage)
        var inner = PitchZoneModel.V1.Info(zone.Value).Value.Region == PitchZoneRegion.Inner;
        return slot.Scope == ZoneScope.Inner ? inner : !inner;
    }

    private static Result<int> Evaluate(ModifierSlot slot, ModifierTuning tuning, ModifierInput input)
    {
        static Result<int> Fail(string code, string message) => Result<int>.Failure(new Error(code, message));

        if (slot.Kind == ModifierKind.Table)
        {
            if (input.Key is null)
                return Fail("Modifier.key_missing", $"보정 슬롯 '{slot.Id}'는 선택 키가 필요합니다.");
            if (slot.Reserved && input.Key != "neutral")
                return Fail("Modifier.reserved_not_active", $"보정 슬롯 '{slot.Id}'는 아직 구현되지 않아 중립 입력만 받습니다.");
            foreach (var entry in tuning.Table)
                if (entry.Key == input.Key)
                    return Result<int>.Success(entry.Value);
            return Fail("Modifier.key_unknown", $"보정 슬롯 '{slot.Id}'에 없는 선택 키입니다: '{input.Key}'");
        }

        if (input.Key is not null)
            return Fail("Modifier.key_unexpected", $"보정 슬롯 '{slot.Id}'는 숫자 입력만 받습니다.");
        if (input.Value < slot.InputMin || input.Value > slot.InputMax)
            return Fail("Modifier.input_out_of_range", $"보정 슬롯 '{slot.Id}'의 입력은 {slot.InputMin}~{slot.InputMax} 범위여야 합니다. (입력값 {input.Value})");
        if (slot.Reserved)
            return input.Value == tuning.Neutral
                ? Result<int>.Success(Neutral)
                : Fail("Modifier.reserved_not_active", $"보정 슬롯 '{slot.Id}'는 아직 구현되지 않아 중립 입력({tuning.Neutral})만 받습니다.");
        var factor = Neutral + (long)(input.Value - tuning.Neutral) * tuning.SlopePpmPerUnit;
        return Result<int>.Success((int)Math.Clamp(factor, tuning.MinPpm, tuning.MaxPpm));
    }

    private static Result<(int[] Distribution, int Guard, int Shift)> ApplyTendency(
        TuningProfile profile, string stage, string condition, IReadOnlyList<string> outcomes, int[] current, TendencyContext context)
    {
        static Result<(int[], int, int)> Fail(string code, string message) => Result<(int[], int, int)>.Failure(new Error(code, message));

        var spec = profile.Model.TendencyFor(stage);
        if (spec is null)
            return Fail("Tendency.stage_unsupported", $"단계 '{stage}'는 코스 성향을 받지 않습니다.");
        if (context.Tendency is null)
            return Fail("Tendency.missing", "코스 성향 값이 없습니다(중립이면 BatterZoneTendency.Neutral을 전달하세요).");
        var info = PitchZoneModel.V1.Info(context.Zone);
        if (!info.IsSuccess)
            return Result<(int[], int, int)>.Failure(info.Error!.Value);
        var expected = stage == StageIds.SwingResult ? context.Zone.Value : ConditionKeys.BattedBallGroup(context.Zone).Value;
        if (condition != expected)
            return Fail("Tendency.context_mismatch", $"성향 문맥의 구역 '{context.Zone}'이(가) 조건 '{condition}'과 맞지 않습니다.");
        if (context.Tendency.ReferenceId != profile.Reference.Id)
            return Fail("Tendency.reference_mismatch", "성향 벡터가 이 튜닝의 기준 분포와 다른 기준으로 균형 잡혀 있습니다.");
        if (info.Value.Region == PitchZoneRegion.Outer)
            return Result<(int[], int, int)>.Success((current, 0, 0)); // outer zones are not tendency-adjusted by default: no guard, no shift

        var cap = profile.CapFor(spec);
        var favoured = outcomes.Select(o => spec.Favoured.Contains(o)).ToArray();
        var mass = current.Where((_, i) => favoured[i]).Sum();
        var low = cap + profile.TendencyMinMassPpm;
        var high = ProbabilityScale.Denominator - cap - profile.TendencyMinMassPpm;
        var guarded = Math.Clamp(mass, low, high);
        var delta = context.Tendency.AdjustmentFor(context.Zone).Value * (cap / 100);
        var target = guarded + delta;

        if (target == mass)
            return Result<(int[], int, int)>.Success((current, 0, 0));
        var shifted = SetFavouredMass(current, favoured, target);
        return shifted.IsSuccess
            ? Result<(int[], int, int)>.Success((shifted.Value, guarded - mass, delta))
            : Result<(int[], int, int)>.Failure(shifted.Error!.Value);
    }

    /// <summary>Moves the favoured group's total to exactly <paramref name="target"/> and the rest to <c>denominator - target</c>, each side scaled in proportion to its current probabilities.</summary>
    internal static Result<int[]> SetFavouredMass(int[] current, bool[] favoured, int target)
    {
        var favouredWeights = current.Select((p, i) => favoured[i] ? (long)p : 0L).ToArray();
        var otherWeights = current.Select((p, i) => favoured[i] ? 0L : (long)p).ToArray();
        if (favouredWeights.Sum() == 0 || otherWeights.Sum() == 0)
            return Result<int[]>.Failure(new Error("Tendency.group_empty", "성향이 옮길 질량이 한쪽에 없어 분포를 이동할 수 없습니다."));
        var up = ProbabilityScale.Normalize(favouredWeights, target);
        if (!up.IsSuccess)
            return up;
        var down = ProbabilityScale.Normalize(otherWeights, ProbabilityScale.Denominator - target);
        if (!down.IsSuccess)
            return down;
        var merged = new int[current.Length];
        for (var i = 0; i < merged.Length; i++)
            merged[i] = up.Value[i] + down.Value[i];
        return Result<int[]>.Success(merged);
    }
}
