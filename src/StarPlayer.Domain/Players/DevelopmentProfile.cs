using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Players;

public enum GrowthType { Early, Steady, Late }

/// <summary>
/// HIDDEN development truth (C04): potential, growth type, prime length and decline onset. It is a different type from
/// <see cref="Ratings"/> and is owned and loaded separately; <c>PlayerSheet</c> does not contain it, so the visibility
/// layer never receives it. Nothing here may reach a public projection, tooltip, news payload or cheat view.
/// </summary>
public sealed record DevelopmentProfile
{
    public PlayerId PlayerId { get; }
    public SkillValue TruePotential { get; }
    public GrowthType GrowthType { get; }
    public int PrimeYears { get; }
    public int DeclineOnsetAge { get; }

    /// <summary>Per-skill hidden ceilings (growth envelope), in fixed <see cref="SkillKey"/> order.</summary>
    public IReadOnlyList<(SkillKey Key, SkillValue Ceiling)> SkillCeilings { get; }

    private DevelopmentProfile(PlayerId playerId, SkillValue truePotential, GrowthType growthType, int primeYears, int declineOnsetAge, IReadOnlyList<(SkillKey, SkillValue)> ceilings)
    {
        PlayerId = playerId;
        TruePotential = truePotential;
        GrowthType = growthType;
        PrimeYears = primeYears;
        DeclineOnsetAge = declineOnsetAge;
        SkillCeilings = ceilings;
    }

    public static Result<DevelopmentProfile> Create(PlayerId playerId, SkillValue truePotential, GrowthType growthType, int primeYears, int declineOnsetAge, IEnumerable<(SkillKey Key, SkillValue Ceiling)>? ceilings)
    {
        if (playerId == default)
            return Result<DevelopmentProfile>.Failure(new Error("DevelopmentProfile.player_missing", "성장 프로필의 선수 ID가 없습니다."));
        if (!Enum.IsDefined(growthType))
            return Result<DevelopmentProfile>.Failure(new Error("DevelopmentProfile.growth_type_invalid", "성장 유형 값이 올바르지 않습니다."));
        if (primeYears is < 1 or > 20 || declineOnsetAge is < 25 or > 50)
            return Result<DevelopmentProfile>.Failure(new Error("DevelopmentProfile.aging_out_of_range", "전성기 기간 또는 노화 시작 나이가 허용 범위를 벗어났습니다."));
        var list = (ceilings ?? []).OrderBy(c => c.Key).ToList();
        if (list.Select(c => c.Key).Distinct().Count() != list.Count)
            return Result<DevelopmentProfile>.Failure(new Error("DevelopmentProfile.duplicate_ceiling", "같은 능력치의 한계치가 중복되었습니다."));
        return Result<DevelopmentProfile>.Success(new DevelopmentProfile(playerId, truePotential, growthType, primeYears, declineOnsetAge, list.AsReadOnly()));
    }

    /// <summary>Cross-owner invariant: a hidden ceiling may not sit below the current base value it limits.</summary>
    public Result CheckAgainst(Ratings current)
    {
        var values = current.Skills().ToDictionary(s => s.Key, s => s.Value);
        foreach (var (key, ceiling) in SkillCeilings)
        {
            if (!values.TryGetValue(key, out var now))
                return Result.Failure(new Error("DevelopmentProfile.unknown_skill", $"{key} 능력치가 없는 선수에게 한계치가 지정되었습니다."));
            if (ceiling.Value < now.Value)
                return Result.Failure(new Error("DevelopmentProfile.ceiling_below_current", $"{key} 한계치가 현재 능력치보다 낮습니다."));
        }

        return Result.Success();
    }
}
