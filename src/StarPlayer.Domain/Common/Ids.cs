namespace StarPlayer.Domain.Common;

// Identifiers are opaque canonical strings (ROADMAP §2); names are never keys.
// default(XId) is not a valid id: only Create() output may be persisted or compared.

internal static class IdText
{
    public static Error? Validate(string? value, string idName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new Error($"{idName}.empty", $"{idName} 값이 비어 있습니다.");
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            return new Error($"{idName}.not_canonical", $"{idName} 값의 앞뒤에 공백이 있습니다.");
        return null;
    }
}

public readonly record struct PlayerId
{
    public string Value { get; private init; }

    public static Result<PlayerId> Create(string? value) =>
        IdText.Validate(value, nameof(PlayerId)) is { } error
            ? Result<PlayerId>.Failure(error)
            : Result<PlayerId>.Success(new PlayerId { Value = value! });

    public override string ToString() => Value;
}

public readonly record struct TeamId
{
    public string Value { get; private init; }

    public static Result<TeamId> Create(string? value) =>
        IdText.Validate(value, nameof(TeamId)) is { } error
            ? Result<TeamId>.Failure(error)
            : Result<TeamId>.Success(new TeamId { Value = value! });

    public override string ToString() => Value;
}

public readonly record struct CareerId
{
    public string Value { get; private init; }

    public static Result<CareerId> Create(string? value) =>
        IdText.Validate(value, nameof(CareerId)) is { } error
            ? Result<CareerId>.Failure(error)
            : Result<CareerId>.Success(new CareerId { Value = value! });

    public override string ToString() => Value;
}
