namespace StarPlayer.Domain.Common;

/// <summary>Monotonic counter of committed world state. Starts at zero and only moves forward.</summary>
public readonly record struct WorldRevision
{
    public long Value { get; private init; }

    public static WorldRevision Initial => default;

    public static Result<WorldRevision> Create(long value) =>
        value < 0
            ? Result<WorldRevision>.Failure(new Error("WorldRevision.negative", "WorldRevision은 0 이상이어야 합니다."))
            : Result<WorldRevision>.Success(new WorldRevision { Value = value });

    public WorldRevision Next() => new() { Value = checked(Value + 1) };

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
