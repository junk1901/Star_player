using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Players;

/// <summary>Hitter base skills: 컨택, 파워, 선구안, 주루, 수비, 송구 (source v1.6 PART 4). Fielding/Arm are the shared fielding inputs.</summary>
public sealed record HitterRatings(SkillValue Contact, SkillValue Power, SkillValue Eye, SkillValue Speed, SkillValue Fielding, SkillValue Arm);

public enum PitchType { Fastball, Slider, Curve, Changeup, Forkball, Sinker }

/// <summary>Per-pitch ability. A pitcher needs at least one pitch; a pitch type appears at most once.</summary>
public sealed record PitchRepertoire
{
    public IReadOnlyList<PitchRating> Pitches { get; }

    private PitchRepertoire(IReadOnlyList<PitchRating> pitches) => Pitches = pitches;

    public static Result<PitchRepertoire> Create(IEnumerable<PitchRating>? pitches)
    {
        var list = (pitches ?? []).OrderBy(p => p.Type).ToList();
        if (list.Count == 0)
            return Result<PitchRepertoire>.Failure(new Error("PitchRepertoire.empty", "투수는 구종을 하나 이상 가져야 합니다."));
        if (list.Select(p => p.Type).Distinct().Count() != list.Count)
            return Result<PitchRepertoire>.Failure(new Error("PitchRepertoire.duplicate", "같은 구종을 두 번 등록할 수 없습니다."));
        return Result<PitchRepertoire>.Success(new PitchRepertoire(list.AsReadOnly())); // the validated copy is wrapped read-only: IList casts cannot change it
    }
}

public readonly record struct PitchRating(PitchType Type, SkillValue Grade);

/// <summary>Pitcher base skills: 구위, 제구, 구속, 체력 plus the repertoire (구종별 능력). Endurance is the shared physical-capacity input.</summary>
public sealed record PitcherRatings(SkillValue Stuff, SkillValue Control, SkillValue Velocity, SkillValue Endurance, PitchRepertoire Repertoire);

/// <summary>
/// Catcher tools (C02/§3.3), each an independent 0-100 value. They are <b>not</b> C position proficiency, and there is
/// no composite CatcherRating or Caught-Stealing Ability. <see cref="PopTime"/> is a higher-is-better skill; an observed
/// time in seconds is a different quantity, see <see cref="PopTimeObservation"/>.
/// </summary>
public sealed record CatcherRatings(SkillValue ReceivingFraming, SkillValue Blocking, SkillValue PopTime, SkillValue GameCalling);

/// <summary>
/// A raw measured catcher pop time. Lower is faster, the unit is milliseconds, and it is deliberately a different type
/// from the 0-100 <see cref="CatcherRatings.PopTime"/> skill so the two cannot be mixed.
/// </summary>
public readonly record struct PopTimeObservation
{
    public const int MinMilliseconds = 500;
    public const int MaxMilliseconds = 6000;

    public int Milliseconds { get; private init; }

    public static Result<PopTimeObservation> FromMilliseconds(int milliseconds) =>
        milliseconds is < MinMilliseconds or > MaxMilliseconds
            ? Result<PopTimeObservation>.Failure(new Error("PopTimeObservation.out_of_range", $"포구~송구 시간은 {MinMilliseconds}~{MaxMilliseconds}ms 범위여야 합니다."))
            : Result<PopTimeObservation>.Success(new PopTimeObservation { Milliseconds = milliseconds });
}

/// <summary>
/// Current base ratings. A set is null when the player has no such role; a two-way player has both
/// <see cref="Hitter"/> and <see cref="Pitcher"/>. Catcher tools exist only for a player who also has hitter ratings.
/// </summary>
public sealed record Ratings
{
    public HitterRatings? Hitter { get; }
    public PitcherRatings? Pitcher { get; }
    public CatcherRatings? Catcher { get; }

    private Ratings(HitterRatings? hitter, PitcherRatings? pitcher, CatcherRatings? catcher)
    {
        Hitter = hitter;
        Pitcher = pitcher;
        Catcher = catcher;
    }

    public static Result<Ratings> Create(HitterRatings? hitter, PitcherRatings? pitcher, CatcherRatings? catcher)
    {
        if (hitter is null && pitcher is null)
            return Result<Ratings>.Failure(new Error("Ratings.no_role", "타자 능력치와 투수 능력치 중 하나는 있어야 합니다."));
        if (catcher is not null && hitter is null)
            return Result<Ratings>.Failure(new Error("Ratings.catcher_without_hitter", "포수 세부 능력치는 타자 능력치와 함께만 가질 수 있습니다."));
        return Result<Ratings>.Success(new Ratings(hitter, pitcher, catcher));
    }

    public PlayerKind Kind => (Hitter, Pitcher) switch
    {
        (not null, not null) => PlayerKind.TwoWay,
        (null, not null) => PlayerKind.Pitcher,
        _ => PlayerKind.Hitter,
    };

    /// <summary>Every present skill in a fixed order, so serializers and projections never depend on dictionary order.</summary>
    public IReadOnlyList<(SkillKey Key, SkillValue Value)> Skills()
    {
        var list = new List<(SkillKey, SkillValue)>();
        if (Hitter is { } h)
            list.AddRange([(SkillKey.Contact, h.Contact), (SkillKey.Power, h.Power), (SkillKey.Eye, h.Eye), (SkillKey.Speed, h.Speed), (SkillKey.Fielding, h.Fielding), (SkillKey.Arm, h.Arm)]);
        if (Pitcher is { } p)
            list.AddRange([(SkillKey.Stuff, p.Stuff), (SkillKey.Control, p.Control), (SkillKey.Velocity, p.Velocity), (SkillKey.Endurance, p.Endurance)]);
        if (Catcher is { } c)
            list.AddRange([(SkillKey.ReceivingFraming, c.ReceivingFraming), (SkillKey.Blocking, c.Blocking), (SkillKey.PopTime, c.PopTime), (SkillKey.GameCalling, c.GameCalling)]);
        return list;
    }
}

/// <summary>타자 / 투수 / 투타 겸업, derived from which rating sets exist (never stored separately).</summary>
public enum PlayerKind { Hitter, Pitcher, TwoWay }
