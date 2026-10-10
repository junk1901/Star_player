using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Baseball;

/// <summary>
/// C41 pair identity, ordered (catcher, pitcher). One catcher with three pitchers is three different pairs; a pair of a
/// player with themselves is invalid. Persisted key text is <c>catcher_id/pitcher_id</c> (ids cannot contain '/').
/// </summary>
public readonly record struct BatteryPairId
{
    public PlayerId Catcher { get; private init; }
    public PlayerId Pitcher { get; private init; }

    public static Result<BatteryPairId> Create(PlayerId catcher, PlayerId pitcher)
    {
        if (catcher == default || pitcher == default)
            return Result<BatteryPairId>.Failure(new Error("BatteryPairId.identity_missing", "배터리 조합에는 포수와 투수 ID가 모두 필요합니다."));
        if (catcher == pitcher)
            return Result<BatteryPairId>.Failure(new Error("BatteryPairId.same_player", "포수와 투수는 서로 다른 선수여야 합니다."));
        return Result<BatteryPairId>.Success(new BatteryPairId { Catcher = catcher, Pitcher = pitcher });
    }

    public string Key => $"{Catcher.Value}/{Pitcher.Value}";

    public override string ToString() => Key;
}

/// <summary>
/// C41 default context for a pair: neutral and unknown. It is separate from friendship, trust, C proficiency and base
/// skills. Familiarity scale, growth and decay are Phase 7 tuning; Phase 1 only defines the pair and its neutral default,
/// and never invents past appearances.
/// </summary>
public sealed record BatteryChemistry
{
    public BatteryPairId Pair { get; }
    public int SharedAppearances { get; }
    public DateOnly? LastBatteryDate { get; }

    private BatteryChemistry(BatteryPairId pair, int sharedAppearances, DateOnly? lastBatteryDate)
    {
        Pair = pair;
        SharedAppearances = sharedAppearances;
        LastBatteryDate = lastBatteryDate;
    }

    public bool IsNeutral => SharedAppearances == 0;

    public static BatteryChemistry Neutral(BatteryPairId pair) => new(pair, 0, null);

    public static Result<BatteryChemistry> Create(BatteryPairId pair, int sharedAppearances, DateOnly? lastBatteryDate)
    {
        if (sharedAppearances < 0)
            return Result<BatteryChemistry>.Failure(new Error("BatteryChemistry.negative_appearances", "배터리 공동 출전 수는 0 이상이어야 합니다."));
        if ((sharedAppearances == 0) != (lastBatteryDate is null))
            return Result<BatteryChemistry>.Failure(new Error("BatteryChemistry.date_mismatch", "공동 출전이 있을 때만 마지막 배터리 날짜가 있어야 합니다."));
        return Result<BatteryChemistry>.Success(new BatteryChemistry(pair, sharedAppearances, lastBatteryDate));
    }
}
