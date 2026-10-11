using System.Collections.Immutable;
using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Baseball.Probability;

// Phase 3-B2 (ADR-006): integer probability contract. Pure arithmetic; no floating point, no randomness, no clock. Nothing here draws from the RNG.

/// <summary>
/// The fixed-denominator probability scale of <c>prob-model-v1</c>: a probability is a whole number of parts per million (ppm) of <see cref="Denominator"/>.
/// <para>
/// <b>Why 1,000,000.</b> It resolves the smallest modelled effect (a 0.1% modifier on a 1% outcome is 10 ppm, still 10 whole units), fits a <c>uint</c> bound
/// for <c>RngStream.NextBounded</c> (the roll contract is <see cref="RollBound"/>), and sits beside the existing integer scales (C46 permille 1000, tendency
/// -100..+100, skills 0..100) without conversion loss: every one of them divides it.
/// </para>
/// <para>
/// <b>Normalization (<see cref="NormalizationPolicy"/>).</b> Non-negative integer weights are scaled so they sum to exactly the requested total
/// (default <see cref="Denominator"/>): each outcome gets <c>floor(weight * total / sum)</c>, and the leftover units (always fewer than the number of outcomes with
/// a non-zero remainder) go one each to the outcomes with the largest remainder <c>(weight * total) mod sum</c>, ties broken by the LOWEST index. A zero weight is
/// always exactly zero. All intermediate products use 128-bit integers, so no overflow is possible inside the validated ranges.
/// </para>
/// <para>
/// <b>Rounding of products (<see cref="RoundingPolicy"/>).</b> <c>round(value * numerator / denominator)</c> rounds half up on non-negative operands.
/// </para>
/// </summary>
public static class ProbabilityScale
{
    public const int Denominator = 1_000_000;

    /// <summary>The bound a caller passes to <c>RngStream.NextBounded</c> for one categorical decision: exactly one <c>NextBounded</c> call per decision, then <see cref="Distribution.Select"/>.</summary>
    public const uint RollBound = 1_000_000u;

    public const int MaxOutcomes = 64;

    /// <summary>Largest accepted weight: 2^56 times <see cref="MaxOutcomes"/> still fits 62 bits, and times the denominator fits 128 bits with room to spare.</summary>
    public const long MaxWeight = 1L << 56;

    public const string NormalizationPolicy = "largest-remainder-lowest-index-v1";
    public const string RoundingPolicy = "half-up-v1";

    /// <summary>Scales <paramref name="weights"/> to non-negative integers that sum to exactly <paramref name="total"/> (1..<see cref="Denominator"/>). See the type remarks for the rule.</summary>
    public static Result<int[]> Normalize(IReadOnlyList<long>? weights, int total = Denominator)
    {
        static Result<int[]> Fail(string code, string message) => Result<int[]>.Failure(new Error(code, message));

        if (weights is null || weights.Count == 0 || weights.Count > MaxOutcomes)
            return Fail("Probability.weights_count_invalid", $"확률 가중치는 1~{MaxOutcomes}개여야 합니다.");
        if (total < 1 || total > Denominator)
            return Fail("Probability.total_invalid", $"정규화 합계는 1~{Denominator} 범위여야 합니다.");
        Int128 sum = 0;
        foreach (var weight in weights)
        {
            if (weight < 0 || weight > MaxWeight)
                return Fail("Probability.weight_out_of_range", $"확률 가중치는 0~{MaxWeight} 범위여야 합니다.");
            sum += weight;
        }

        if (sum == 0)
            return Fail("Probability.weights_all_zero", "확률 가중치의 합이 0이어서 분포를 만들 수 없습니다.");

        var result = new int[weights.Count];
        var remainder = new Int128[weights.Count];
        var given = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            var scaled = (Int128)weights[i] * total;
            result[i] = (int)(scaled / sum);
            remainder[i] = scaled % sum;
            given += result[i];
        }

        var leftover = total - given;
        var order = Enumerable.Range(0, weights.Count).Where(i => remainder[i] > 0).OrderByDescending(i => remainder[i]).ThenBy(i => i).ToList();
        if (leftover < 0 || leftover > order.Count)
            return Fail("Probability.normalize_invariant", "정규화 잔여값 배분이 불변식을 어겼습니다(도달할 수 없는 상태).");
        for (var k = 0; k < leftover; k++)
            result[order[k]]++;
        return Result<int[]>.Success(result);
    }

    /// <summary><c>round-half-up(value * numerator / denominator)</c> for non-negative operands, computed in 128 bits and range-checked to a <c>long</c>.</summary>
    public static Result<long> MultiplyRatio(long value, long numerator, long denominator)
    {
        if (value < 0 || numerator < 0 || denominator <= 0)
            return Result<long>.Failure(new Error("Probability.ratio_operand_invalid", "비율 곱셈의 피연산자는 0 이상이고 분모는 양수여야 합니다."));
        var rounded = ((Int128)value * numerator + denominator / 2) / denominator;
        return rounded > long.MaxValue
            ? Result<long>.Failure(new Error("Probability.ratio_overflow", "비율 곱셈 결과가 허용 범위를 넘었습니다."))
            : Result<long>.Success((long)rounded);
    }
}

/// <summary>
/// A conditional distribution over a fixed, ordered set of mutually exclusive outcomes: every probability is a whole number of ppm in
/// <c>0..<see cref="ProbabilityScale.Denominator"/></c> and they sum to EXACTLY <see cref="ProbabilityScale.Denominator"/>. The denominator of a distribution is the
/// condition it was built for (for example "given the pitch was swung at"), never the whole plate appearance; distributions of different stages are never mixed.
/// </summary>
public sealed class Distribution : IEquatable<Distribution>
{
    // ImmutableArray copies on creation and has no writer, so the public lists below and the cumulative table used by Select can never disagree: all three come from the
    // same creation-time values, and a cast of a public list back to an array or List is impossible (it is not one).
    private readonly ImmutableArray<string> _outcomes;
    private readonly ImmutableArray<int> _ppm;
    private readonly ImmutableArray<int> _cumulative;

    private Distribution(ImmutableArray<string> outcomes, ImmutableArray<int> ppm)
    {
        _outcomes = outcomes;
        _ppm = ppm;
        var cumulative = ImmutableArray.CreateBuilder<int>(ppm.Length);
        var running = 0;
        foreach (var p in ppm)
        {
            running += p;
            cumulative.Add(running);
        }

        _cumulative = cumulative.MoveToImmutable();
    }

    public IReadOnlyList<string> Outcomes => _outcomes;

    public IReadOnlyList<int> Ppm => _ppm;

    public int Count => _ppm.Length;

    public static bool IsOutcomeId(string? id) =>
        id is { Length: > 0 and <= 48 } && id.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '.');

    public static Result<Distribution> Create(IReadOnlyList<string>? outcomes, IReadOnlyList<int>? ppm)
    {
        static Result<Distribution> Fail(string code, string message) => Result<Distribution>.Failure(new Error(code, message));

        if (outcomes is null || ppm is null || outcomes.Count != ppm.Count)
            return Fail("Distribution.shape_invalid", "분포의 결과 목록과 확률 목록의 길이가 같아야 합니다.");
        if (outcomes.Count == 0 || outcomes.Count > ProbabilityScale.MaxOutcomes)
            return Fail("Distribution.count_invalid", $"분포의 결과는 1~{ProbabilityScale.MaxOutcomes}개여야 합니다.");
        if (outcomes.Any(o => !IsOutcomeId(o)))
            return Fail("Distribution.outcome_id_invalid", "분포의 결과 ID는 소문자·숫자·'_'·'.' 48자 이하여야 합니다.");
        if (outcomes.Distinct(StringComparer.Ordinal).Count() != outcomes.Count)
            return Fail("Distribution.outcome_duplicate", "분포에 같은 결과 ID가 두 번 있습니다.");
        if (ppm.Any(p => p < 0 || p > ProbabilityScale.Denominator))
            return Fail("Distribution.probability_out_of_range", $"확률은 0~{ProbabilityScale.Denominator} 범위여야 합니다.");
        if (ppm.Sum(p => (long)p) != ProbabilityScale.Denominator)
            return Fail("Distribution.sum_invalid", $"조건부 분포의 합은 정확히 {ProbabilityScale.Denominator}이어야 합니다.");
        return Result<Distribution>.Success(new Distribution(outcomes.ToImmutableArray(), ppm.ToImmutableArray()));
    }

    public Result<int> Of(string outcome)
    {
        var index = _outcomes.IndexOf(outcome, StringComparer.Ordinal);
        return index >= 0 ? Result<int>.Success(_ppm[index]) : Result<int>.Failure(new Error("Distribution.outcome_unknown", $"분포에 없는 결과입니다: '{outcome}'"));
    }

    /// <summary>
    /// Maps a uniform integer roll in <c>[0, <see cref="ProbabilityScale.RollBound"/>)</c> to the outcome whose cumulative interval contains it:
    /// outcome <c>i</c> owns exactly <c>Ppm[i]</c> consecutive rolls starting at the sum of the earlier outcomes, so an outcome with zero ppm is never selected and
    /// each outcome is selected by exactly its ppm of the rolls. The roll itself comes from ONE <c>RngStream.NextBounded(<see cref="ProbabilityScale.RollBound"/>)</c>
    /// call per decision; this method never touches a random source.
    /// </summary>
    public Result<int> SelectIndex(uint roll)
    {
        if (roll >= ProbabilityScale.RollBound)
            return Result<int>.Failure(new Error("Distribution.roll_out_of_range", $"난수 값은 0~{ProbabilityScale.RollBound - 1} 범위여야 합니다."));
        var index = 0;
        while (roll >= (uint)_cumulative[index])
            index++; // terminates: the last cumulative value is the denominator and roll < denominator
        return Result<int>.Success(index);
    }

    public Result<string> Select(uint roll)
    {
        var index = SelectIndex(roll);
        return index.IsSuccess ? Result<string>.Success(_outcomes[index.Value]) : Result<string>.Failure(index.Error!.Value);
    }

    public bool Equals(Distribution? other) => other is not null && _outcomes.SequenceEqual(other._outcomes) && _ppm.SequenceEqual(other._ppm);

    public override bool Equals(object? obj) => Equals(obj as Distribution);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var outcome in _outcomes)
            hash.Add(outcome, StringComparer.Ordinal);
        foreach (var p in _ppm)
            hash.Add(p);
        return hash.ToHashCode();
    }

    public override string ToString() => string.Join(",", _outcomes.Select((o, i) => $"{o}={_ppm[i]}"));
}
