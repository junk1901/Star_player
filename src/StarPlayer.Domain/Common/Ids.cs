namespace StarPlayer.Domain.Common;

// Identifiers are opaque canonical strings (ROADMAP §2); names are never keys.
// default(XId) is not a valid id: only Create() output may be persisted or compared.

/// <summary>Implemented by every string-backed id so serializers can rebuild ids through validation.</summary>
public interface IStringId<TSelf> where TSelf : struct, IStringId<TSelf>
{
    string Value { get; }

    static abstract Result<TSelf> Create(string? value);
}

internal static class IdText
{
    public const int MaxLength = 64;

    // Imported pack ids ("player_2032_0841", "team_03") and generated ids fit this alphabet; names do not.
    public static Error? Validate(string? value, string idName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new Error($"{idName}.empty", $"{idName} 값이 비어 있습니다.");
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            return new Error($"{idName}.not_canonical", $"{idName} 값의 앞뒤에 공백이 있습니다.");
        if (value.Length > MaxLength)
            return new Error($"{idName}.too_long", $"{idName} 값은 {MaxLength}자 이하여야 합니다.");
        if (!value.All(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-' or '.' or ':'))
            return new Error($"{idName}.invalid_character", $"{idName} 값에는 영문, 숫자, '_', '-', '.', ':'만 쓸 수 있습니다.");
        return null;
    }
}

public readonly record struct PlayerId : IStringId<PlayerId>
{
    public string Value { get; private init; }

    public static Result<PlayerId> Create(string? value) =>
        IdText.Validate(value, nameof(PlayerId)) is { } error
            ? Result<PlayerId>.Failure(error)
            : Result<PlayerId>.Success(new PlayerId { Value = value! });

    public override string ToString() => Value;
}

/// <summary>Competition-context squad key kept from Phase 0; franchise identity is <see cref="ClubId"/>.</summary>
public readonly record struct TeamId : IStringId<TeamId>
{
    public string Value { get; private init; }

    public static Result<TeamId> Create(string? value) =>
        IdText.Validate(value, nameof(TeamId)) is { } error
            ? Result<TeamId>.Failure(error)
            : Result<TeamId>.Success(new TeamId { Value = value! });

    public override string ToString() => Value;
}

public readonly record struct CareerId : IStringId<CareerId>
{
    public string Value { get; private init; }

    public static Result<CareerId> Create(string? value) =>
        IdText.Validate(value, nameof(CareerId)) is { } error
            ? Result<CareerId>.Failure(error)
            : Result<CareerId>.Success(new CareerId { Value = value! });

    public override string ToString() => Value;
}

/// <summary>Stable franchise identity (C06); survives renames. Used by C47 jersey assignments.</summary>
public readonly record struct ClubId : IStringId<ClubId>
{
    public string Value { get; private init; }

    public static Result<ClubId> Create(string? value) =>
        IdText.Validate(value, nameof(ClubId)) is { } error
            ? Result<ClubId>.Failure(error)
            : Result<ClubId>.Success(new ClubId { Value = value! });

    public override string ToString() => Value;
}

/// <summary>Amateur/overseas origin organization (school, university, foreign club); distinct from <see cref="ClubId"/>.</summary>
public readonly record struct AmateurTeamId : IStringId<AmateurTeamId>
{
    public string Value { get; private init; }

    public static Result<AmateurTeamId> Create(string? value) =>
        IdText.Validate(value, nameof(AmateurTeamId)) is { } error
            ? Result<AmateurTeamId>.Failure(error)
            : Result<AmateurTeamId>.Success(new AmateurTeamId { Value = value! });

    public override string ToString() => Value;
}

/// <summary>Stable venue identity (C46). The display name may change; this id may not.</summary>
public readonly record struct StadiumId : IStringId<StadiumId>
{
    public string Value { get; private init; }

    public static Result<StadiumId> Create(string? value) =>
        IdText.Validate(value, nameof(StadiumId)) is { } error
            ? Result<StadiumId>.Failure(error)
            : Result<StadiumId>.Success(new StadiumId { Value = value! });

    public override string ToString() => Value;
}

public readonly record struct ParkFactorProfileId : IStringId<ParkFactorProfileId>
{
    public string Value { get; private init; }

    public static Result<ParkFactorProfileId> Create(string? value) =>
        IdText.Validate(value, nameof(ParkFactorProfileId)) is { } error
            ? Result<ParkFactorProfileId>.Failure(error)
            : Result<ParkFactorProfileId>.Success(new ParkFactorProfileId { Value = value! });

    public override string ToString() => Value;
}

public readonly record struct RuleProfileId : IStringId<RuleProfileId>
{
    public string Value { get; private init; }

    public static Result<RuleProfileId> Create(string? value) =>
        IdText.Validate(value, nameof(RuleProfileId)) is { } error
            ? Result<RuleProfileId>.Failure(error)
            : Result<RuleProfileId>.Success(new RuleProfileId { Value = value! });

    public override string ToString() => Value;
}
