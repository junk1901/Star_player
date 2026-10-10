using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Players;

/// <summary>
/// One current base-skill reading on the fixed 0-100 skill scale. This is the skill scale, not a creation-point
/// budget: cheat creation may use any value in range, and nothing outside 0-100 is representable.
/// </summary>
public readonly record struct SkillValue : IComparable<SkillValue>
{
    public const int Min = 0;
    public const int Max = 100;

    public int Value { get; private init; }

    public static Result<SkillValue> Create(int value, string fieldName = "skill") =>
        value is < Min or > Max
            ? Result<SkillValue>.Failure(new Error("SkillValue.out_of_range", $"{fieldName} 능력치는 {Min}~{Max} 범위여야 합니다. (입력값 {value})"))
            : Result<SkillValue>.Success(new SkillValue { Value = value });

    public int CompareTo(SkillValue other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Stable identity of every named skill (hitter six, pitcher four, catcher four).</summary>
public enum SkillKey
{
    Contact, Power, Eye, Speed, Fielding, Arm,
    Stuff, Control, Velocity, Endurance,
    ReceivingFraming, Blocking, PopTime, GameCalling,
}
