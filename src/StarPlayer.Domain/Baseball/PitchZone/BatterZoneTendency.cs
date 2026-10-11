using System.Collections.Immutable;
using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Baseball.PitchZone;

/// <summary>
/// How often each of the nine inner cells is assumed to be pitched to in the league-average case: the reference for average-effect neutrality.
/// <para>
/// <b>A reference id and its weights are one inseparable, immutable definition.</b> Only definitions registered in this class exist; <see cref="Create"/> accepts
/// an id only together with exactly its registered weights, and <see cref="Resolve"/> looks one up by id, so the same <c>ReferenceId</c> stored with a tendency
/// can never have judged it under different weights. <see cref="UniformV1"/> (<c>pzl-ref-uniform-v1</c>) is nine weights of 1 and nothing else; it is the interim
/// reference of Phase 3-B1. A different reference (for example a tuned one from Phase 3-B2) must be added here under a NEW id with its fixed weights, never by
/// changing an existing one or by passing other weights under an old id. The instances are immutable (private constructor, read-only weights copied on creation).
/// </para>
/// </summary>
public sealed class ZoneReferenceDistribution
{
    public const int MaxWeight = 1_000_000;

    public static ZoneReferenceDistribution UniformV1 { get; } = new("pzl-ref-uniform-v1", [1, 1, 1, 1, 1, 1, 1, 1, 1]);

    private static readonly IReadOnlyDictionary<string, ZoneReferenceDistribution> Registered =
        new Dictionary<string, ZoneReferenceDistribution>(StringComparer.Ordinal) { [UniformV1.Id] = UniformV1 };

    public string Id { get; }

    /// <summary>Non-negative weights of the nine inner cells in row-major order. Read-only.</summary>
    public IReadOnlyList<int> Weights { get; }

    private ZoneReferenceDistribution(string id, IEnumerable<int> weights)
    {
        Id = id;
        Weights = weights.ToImmutableArray();
    }

    /// <summary>The registered reference with this id, or an error for an unknown id (never a default).</summary>
    public static Result<ZoneReferenceDistribution> Resolve(string? id) =>
        id is not null && Registered.TryGetValue(id, out var found)
            ? Result<ZoneReferenceDistribution>.Success(found)
            : Result<ZoneReferenceDistribution>.Failure(new Error("ZoneReferenceDistribution.id_unknown", $"알 수 없는 기준 분포 ID입니다: '{id}'"));

    /// <summary>
    /// Confirms that <paramref name="id"/> is a registered reference and that <paramref name="weights"/> are exactly its weights (for reading stored data). An unknown id is
    /// <c>id_unknown</c>; a known id with other weights is <c>weights_mismatch</c>. Returns the registered instance.
    /// </summary>
    public static Result<ZoneReferenceDistribution> Create(string? id, IEnumerable<int>? weights)
    {
        var shaped = Validate(id, weights);
        if (!shaped.IsSuccess)
            return Result<ZoneReferenceDistribution>.Failure(shaped.Error!.Value);
        var registered = Resolve(id);
        if (!registered.IsSuccess)
            return registered;
        return registered.Value.Weights.SequenceEqual(shaped.Value)
            ? registered
            : Result<ZoneReferenceDistribution>.Failure(new Error("ZoneReferenceDistribution.weights_mismatch", $"기준 분포 '{id}'의 가중치는 등록된 값과 다를 수 없습니다."));
    }

    /// <summary>Tests only (Domain.Tests is the sole <c>InternalsVisibleTo</c>): builds a well-shaped but UNREGISTERED reference to exercise the weighted-sum logic.</summary>
    internal static Result<ZoneReferenceDistribution> CreateUnregisteredForTest(string? id, IEnumerable<int>? weights)
    {
        var shaped = Validate(id, weights);
        return shaped.IsSuccess
            ? Result<ZoneReferenceDistribution>.Success(new ZoneReferenceDistribution(id!, shaped.Value))
            : Result<ZoneReferenceDistribution>.Failure(shaped.Error!.Value);
    }

    private static Result<List<int>> Validate(string? id, IEnumerable<int>? weights)
    {
        if (IdText.Validate(id, "ZoneReferenceDistribution.id") is { } idError)
            return Result<List<int>>.Failure(new Error("ZoneReferenceDistribution.id_invalid", idError.Message));
        var list = weights?.ToList();
        if (list is null || list.Count != BatterZoneTendency.ZoneCount)
            return Result<List<int>>.Failure(new Error("ZoneReferenceDistribution.length_invalid", $"기준 분포는 {BatterZoneTendency.ZoneCount}개 값이어야 합니다."));
        if (list.Any(w => w is < 0 or > MaxWeight))
            return Result<List<int>>.Failure(new Error("ZoneReferenceDistribution.weight_invalid", $"기준 분포 값은 0~{MaxWeight} 범위여야 합니다."));
        if (list.Sum(w => (long)w) == 0)
            return Result<List<int>>.Failure(new Error("ZoneReferenceDistribution.empty", "기준 분포의 합이 0입니다."));
        return Result<List<int>>.Success(list);
    }
}

/// <summary>
/// A batter's HIDDEN nine-element inner-zone tendency (ROADMAP 1.14): an integer per inner cell in row-major order, <c>-100..+100</c>, where +100 means
/// the full per-zone effect cap of the later probability model in the batter's favour and -100 the full cap against him. The cap itself, and every
/// link to a swing or contact probability, belong to Phase 3-B2/3-D; this type only holds and validates the data.
/// <list type="bullet">
/// <item>Default is all zeros (<see cref="Neutral"/>) - no effect.</item>
/// <item>Average-effect neutrality: the reference-weighted sum of the values must be exactly zero, so a tendency moves strength between zones and
/// never raises or lowers a batter's league-average output.</item>
/// <item>Outer zones are not adjusted (<see cref="AdjustmentFor"/> returns 0).</item>
/// <item>Separate from base skills: nothing here references <c>Ratings</c> or Contact, and it does not grow or change in this version.</item>
/// <item>Hidden: only the game resolver may read the values. It must never reach a UI, tooltip, news/Gemini payload, save-visible read model or the
/// opponent pitching AI; a future weakness-exploiting AI may use only observable reports (ROADMAP 1.14). <see cref="ToString"/> never prints the values.</item>
/// </list>
/// </summary>
public sealed class BatterZoneTendency : IEquatable<BatterZoneTendency>
{
    public const int ZoneCount = 9;
    public const int MaxMagnitude = 100;

    public static BatterZoneTendency Neutral { get; } = new(ZoneReferenceDistribution.UniformV1.Id, new int[ZoneCount]);

    public ZoneModelVersion Model => ZoneModelVersion.Pzl1;

    /// <summary>The <see cref="ZoneReferenceDistribution.Id"/> this vector was balanced against.</summary>
    public string ReferenceId { get; }

    /// <summary>The nine values, row-major (<c>in.r1c1</c> first). Read-only; resolver use only (see the type remarks).</summary>
    public IReadOnlyList<int> Values { get; }

    public bool IsNeutral => Values.All(v => v == 0);

    private BatterZoneTendency(string referenceId, IEnumerable<int> values)
    {
        ReferenceId = referenceId;
        Values = values.ToImmutableArray();
    }

    public static Result<BatterZoneTendency> Create(IEnumerable<int>? values, ZoneReferenceDistribution? reference)
    {
        static Result<BatterZoneTendency> Fail(string code, string message) => Result<BatterZoneTendency>.Failure(new Error(code, message));

        if (values is null)
            return Fail("BatterZoneTendency.values_missing", "타자 코스 성향 값이 없습니다.");
        if (reference is null)
            return Fail("BatterZoneTendency.reference_missing", "타자 코스 성향의 기준 분포가 없습니다.");
        var list = values.ToList();
        if (list.Count != ZoneCount)
            return Fail("BatterZoneTendency.length_invalid", $"타자 코스 성향은 {ZoneCount}개 값이어야 합니다.");
        if (list.Any(v => v is < -MaxMagnitude or > MaxMagnitude))
            return Fail("BatterZoneTendency.value_out_of_range", $"타자 코스 성향 값은 {-MaxMagnitude}~{MaxMagnitude} 범위여야 합니다.");
        // Overflow-safe: every value is range-checked above (|v| <= 100) and every weight is <= 1_000_000, so nine terms stay below 1e9, far inside a long.
        long weighted = 0;
        for (var i = 0; i < ZoneCount; i++)
            weighted += (long)list[i] * reference.Weights[i];
        if (weighted != 0)
            return Fail("BatterZoneTendency.not_average_neutral", "타자 코스 성향은 기준 분포에서 평균 효과가 0이어야 합니다.");
        return Result<BatterZoneTendency>.Success(new BatterZoneTendency(reference.Id, list));
    }

    /// <summary>The adjustment at a zone: an inner cell's value; 0 for an outer zone (not adjusted by default); an error for an unknown zone.</summary>
    public Result<int> AdjustmentFor(PitchZoneId zone)
    {
        var info = PitchZoneModel.V1.Info(zone);
        if (!info.IsSuccess)
            return Result<int>.Failure(info.Error!.Value);
        return Result<int>.Success(info.Value.Region == PitchZoneRegion.Inner ? Values[info.Value.InnerIndex] : 0);
    }

    public bool Equals(BatterZoneTendency? other) =>
        other is not null && ReferenceId == other.ReferenceId && Values.SequenceEqual(other.Values);

    public override bool Equals(object? obj) => Equals(obj as BatterZoneTendency);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ReferenceId, StringComparer.Ordinal);
        foreach (var value in Values)
            hash.Add(value);
        return hash.ToHashCode();
    }

    public override string ToString() => "BatterZoneTendency(hidden)";
}
