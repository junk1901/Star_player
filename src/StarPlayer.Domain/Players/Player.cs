using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Players;

public enum ThrowingHand { Right, Left }

public enum BattingSide { Right, Left, Switch }

/// <summary>Structured personality choices listed in source v1.6. In Phase 1 they are identity data only and have no effect.</summary>
public enum PersonalityTrait { Diligent, Ambitious, Competitive, Sociable, Introverted, Flashy, Frugal, BaseballObsessed, FreeSpirited }

/// <summary>Height/weight are descriptive identity facts; bounds are physical plausibility only, not balance values.</summary>
public readonly record struct BodyMeasurements
{
    public const int MinHeightCm = 140, MaxHeightCm = 230, MinWeightKg = 40, MaxWeightKg = 200;

    public int HeightCm { get; private init; }
    public int WeightKg { get; private init; }

    public static Result<BodyMeasurements> Create(int heightCm, int weightKg)
    {
        if (heightCm is < MinHeightCm or > MaxHeightCm)
            return Result<BodyMeasurements>.Failure(new Error("Body.height_out_of_range", $"신장은 {MinHeightCm}~{MaxHeightCm}cm 범위여야 합니다."));
        if (weightKg is < MinWeightKg or > MaxWeightKg)
            return Result<BodyMeasurements>.Failure(new Error("Body.weight_out_of_range", $"체중은 {MinWeightKg}~{MaxWeightKg}kg 범위여야 합니다."));
        return Result<BodyMeasurements>.Success(new BodyMeasurements { HeightCm = heightCm, WeightKg = weightKg });
    }
}

/// <summary>Completed-years age from the authoritative birth date (ROADMAP §3.2). A Feb-29 birthday falls on Mar 1 in common years.</summary>
public static class AgeCalculator
{
    public static Result<int> AgeOn(DateOnly birthDate, DateOnly onDate)
    {
        if (onDate < birthDate)
            return Result<int>.Failure(new Error("Age.before_birth", "기준일이 생년월일보다 빠릅니다."));
        var age = onDate.Year - birthDate.Year;
        var birthdayThisYear = BirthdayIn(birthDate, onDate.Year);
        return Result<int>.Success(onDate < birthdayThisYear ? age - 1 : age);
    }

    private static DateOnly BirthdayIn(DateOnly birthDate, int year) =>
        birthDate.Month == 2 && birthDate.Day == 29 && !DateTime.IsLeapYear(year)
            ? new DateOnly(year, 3, 1)
            : new DateOnly(year, birthDate.Month, birthDate.Day);
}

/// <summary>
/// Identity and descriptive facts only (C01). Ratings, state, hidden development, proficiency, club, contract and jersey
/// number are separate owners; there is deliberately no current-number or club field here.
/// </summary>
public sealed record Player
{
    public const int MaxNameLength = 30, MaxBiographyLength = 500;

    public PlayerId Id { get; }
    public string DisplayName { get; }
    public DateOnly BirthDate { get; }
    public ThrowingHand Throws { get; }
    public BattingSide Bats { get; }
    public BodyMeasurements Body { get; }
    public IReadOnlyList<PersonalityTrait> Personality { get; }

    /// <summary>Free descriptive text (D4). It carries no mechanics.</summary>
    public string Biography { get; }

    private Player(PlayerId id, string displayName, DateOnly birthDate, ThrowingHand throws, BattingSide bats, BodyMeasurements body, IReadOnlyList<PersonalityTrait> personality, string biography)
    {
        Id = id;
        DisplayName = displayName;
        BirthDate = birthDate;
        Throws = throws;
        Bats = bats;
        Body = body;
        Personality = personality;
        Biography = biography;
    }

    public static Result<Player> Create(PlayerId id, string? displayName, DateOnly birthDate, ThrowingHand throws, BattingSide bats,
        BodyMeasurements body, IEnumerable<PersonalityTrait> personality, string? biography)
    {
        if (id == default)
            return Result<Player>.Failure(new Error("Player.id_missing", "선수 ID가 없습니다."));
        if (string.IsNullOrWhiteSpace(displayName) || displayName != displayName.Trim() || displayName.Length > MaxNameLength || displayName.Any(char.IsControl))
            return Result<Player>.Failure(new Error("Player.name_invalid", $"선수 이름은 앞뒤 공백 없이 1~{MaxNameLength}자여야 합니다."));
        if (birthDate.Year < 1900)
            return Result<Player>.Failure(new Error("Player.birth_date_invalid", "생년월일이 너무 과거입니다."));
        if (!Enum.IsDefined(throws) || !Enum.IsDefined(bats))
            return Result<Player>.Failure(new Error("Player.handedness_invalid", "투타 방향 값이 올바르지 않습니다."));

        var traits = personality.OrderBy(t => t).Distinct().ToList();
        if (traits.Any(t => !Enum.IsDefined(t)))
            return Result<Player>.Failure(new Error("Player.personality_invalid", "성격 값이 올바르지 않습니다."));
        var text = biography ?? "";
        if (text.Length > MaxBiographyLength)
            return Result<Player>.Failure(new Error("Player.biography_too_long", $"소개 글은 {MaxBiographyLength}자 이하여야 합니다."));

        return Result<Player>.Success(new Player(id, displayName, birthDate, throws, bats, body, traits, text));
    }

    public Result<int> AgeOn(DateOnly date) => AgeCalculator.AgeOn(BirthDate, date);

    /// <summary>A display-name change never touches identity: the id stays the join key.</summary>
    public Result<Player> Rename(string? newName) =>
        Create(Id, newName, BirthDate, Throws, Bats, Body, Personality, Biography);
}
