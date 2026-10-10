using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Baseball;

/// <summary>Display-only stadium facts (C46). Changing any of them never changes <see cref="Stadium.Id"/> or a factor.</summary>
public sealed record StadiumPresentation(string DisplayName, string City, int? Capacity);

/// <summary>Stable venue identity plus presentation metadata. Simulation coefficients live in <see cref="ParkFactorProfile"/>, not here.</summary>
public sealed record Stadium
{
    public StadiumId Id { get; }
    public StadiumPresentation Presentation { get; }

    private Stadium(StadiumId id, StadiumPresentation presentation)
    {
        Id = id;
        Presentation = presentation;
    }

    public static Result<Stadium> Create(StadiumId id, StadiumPresentation presentation)
    {
        if (id == default)
            return Result<Stadium>.Failure(new Error("Stadium.id_missing", "구장 ID가 없습니다."));
        if (string.IsNullOrWhiteSpace(presentation.DisplayName) || presentation.DisplayName != presentation.DisplayName.Trim() || presentation.DisplayName.Length > 40)
            return Result<Stadium>.Failure(new Error("Stadium.name_invalid", "구장 이름은 앞뒤 공백 없이 1~40자여야 합니다."));
        if (presentation.Capacity is <= 0)
            return Result<Stadium>.Failure(new Error("Stadium.capacity_invalid", "수용 인원은 양수여야 합니다."));
        return Result<Stadium>.Success(new Stadium(id, presentation));
    }

    /// <summary>A rename (or any presentation change) keeps the id, so every fixture and past game still points at this venue.</summary>
    public Result<Stadium> WithPresentation(StadiumPresentation presentation) => Create(Id, presentation);
}

/// <summary>A dated club usage of a stadium: a reference, not stadium ownership and not a player-rating modifier.</summary>
public sealed record StadiumUsage(StadiumId Stadium, ClubId Club, DateOnly From, DateOnly? To);

/// <summary>A park coefficient in permille where 1000 is neutral (1100 = +10%). Whole numbers keep it exact and serializable.</summary>
public readonly record struct ParkFactor
{
    public const int Neutral = 1000;

    public int Permille { get; private init; }

    internal static ParkFactor Of(int permille) => new() { Permille = permille };
}

/// <summary>
/// The approved cap for park factors. The numbers are rule/tuning data supplied by the pack or rule profile (Phase 3 and
/// the C40 owner decide them); Phase 1 only fixes that every factor must lie inside a validated cap around neutral.
/// </summary>
public readonly record struct ParkFactorBounds
{
    public const int AbsoluteMaxPermille = 3000;

    public int MinPermille { get; private init; }
    public int MaxPermille { get; private init; }

    public static Result<ParkFactorBounds> Create(int minPermille, int maxPermille) =>
        minPermille <= 0 || minPermille > ParkFactor.Neutral || maxPermille < ParkFactor.Neutral || maxPermille > AbsoluteMaxPermille
            ? Result<ParkFactorBounds>.Failure(new Error("ParkFactorBounds.invalid", $"구장 계수 상한/하한은 0 < 하한 ≤ {ParkFactor.Neutral} ≤ 상한 ≤ {AbsoluteMaxPermille}이어야 합니다."))
            : Result<ParkFactorBounds>.Success(new ParkFactorBounds { MinPermille = minPermille, MaxPermille = maxPermille });

    public Result<ParkFactor> Check(int permille, string name) =>
        permille < MinPermille || permille > MaxPermille
            ? Result<ParkFactor>.Failure(new Error("ParkFactor.out_of_bounds", $"{name} 계수 {permille}‰는 허용 범위 {MinPermille}~{MaxPermille}‰ 밖입니다."))
            : Result<ParkFactor>.Success(ParkFactor.Of(permille));
}

/// <summary>The resolved, immutable factor set a game keeps (C46). It never edits player ratings; consumers read it as game context.</summary>
public sealed record ParkFactorSnapshot(StadiumId Stadium, ParkFactorProfileId Profile, int Version, ParkFactor HomeRuns, ParkFactor Hits, ParkFactor ExtraBaseHits);

/// <summary>A versioned set of HR / hit / extra-base-hit coefficients for one stadium, valid from a date. Old versions stay readable.</summary>
public sealed record ParkFactorProfile
{
    public ParkFactorProfileId Id { get; }
    public StadiumId Stadium { get; }
    public int Version { get; }
    public DateOnly EffectiveFrom { get; }
    public ParkFactor HomeRuns { get; }
    public ParkFactor Hits { get; }
    public ParkFactor ExtraBaseHits { get; }

    private ParkFactorProfile(ParkFactorProfileId id, StadiumId stadium, int version, DateOnly effectiveFrom, ParkFactor hr, ParkFactor hits, ParkFactor xbh)
    {
        Id = id;
        Stadium = stadium;
        Version = version;
        EffectiveFrom = effectiveFrom;
        HomeRuns = hr;
        Hits = hits;
        ExtraBaseHits = xbh;
    }

    public static Result<ParkFactorProfile> Create(ParkFactorProfileId id, StadiumId stadium, int version, DateOnly effectiveFrom,
        int homeRunPermille, int hitPermille, int extraBaseHitPermille, ParkFactorBounds bounds)
    {
        if (id == default || stadium == default)
            return Result<ParkFactorProfile>.Failure(new Error("ParkFactorProfile.identity_missing", "구장 계수 프로필 ID와 구장 ID가 필요합니다."));
        if (version < 1)
            return Result<ParkFactorProfile>.Failure(new Error("ParkFactorProfile.version_invalid", "구장 계수 버전은 1 이상이어야 합니다."));
        if (bounds.MinPermille == 0)
            return Result<ParkFactorProfile>.Failure(new Error("ParkFactorProfile.bounds_missing", "구장 계수 허용 범위가 지정되지 않았습니다."));

        var hr = bounds.Check(homeRunPermille, "홈런");
        if (!hr.IsSuccess) return Result<ParkFactorProfile>.Failure(hr.Error!.Value);
        var hit = bounds.Check(hitPermille, "안타");
        if (!hit.IsSuccess) return Result<ParkFactorProfile>.Failure(hit.Error!.Value);
        var xbh = bounds.Check(extraBaseHitPermille, "장타");
        if (!xbh.IsSuccess) return Result<ParkFactorProfile>.Failure(xbh.Error!.Value);

        return Result<ParkFactorProfile>.Success(new ParkFactorProfile(id, stadium, version, effectiveFrom, hr.Value, hit.Value, xbh.Value));
    }

    public ParkFactorSnapshot ToSnapshot() => new(Stadium, Id, Version, HomeRuns, Hits, ExtraBaseHits);
}
