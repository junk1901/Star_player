using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Players;

public enum FieldPosition { Pitcher, Catcher, FirstBase, SecondBase, ThirdBase, Shortstop, LeftField, CenterField, RightField, DesignatedHitter }

/// <summary>Hitter/pitcher role familiarity (C05). Separate from base skills: changing role does not erase them.</summary>
public enum PlayerRole { Batter, StartingPitcher, ReliefPitcher }

/// <summary>
/// Position and role proficiency (C05) plus the player's current preferred position. Independent of base skills: an
/// excellent-tools / novice-C catcher and an average-tools / expert-C catcher are both representable. Position
/// proficiency exists for the eight fielding positions only (pitching is covered by <see cref="PlayerRole"/>, DH by Batter).
/// A position with no entry has no recorded familiarity (value 0, not an invented balance constant).
/// </summary>
public sealed record ProficiencyProfile
{
    public FieldPosition PreferredPosition { get; }
    public IReadOnlyList<(FieldPosition Position, SkillValue Value)> Positions { get; }
    public IReadOnlyList<(PlayerRole Role, SkillValue Value)> Roles { get; }

    private ProficiencyProfile(FieldPosition preferred, IReadOnlyList<(FieldPosition, SkillValue)> positions, IReadOnlyList<(PlayerRole, SkillValue)> roles)
    {
        PreferredPosition = preferred;
        Positions = positions;
        Roles = roles;
    }

    public static Result<ProficiencyProfile> Create(FieldPosition preferred, IEnumerable<(FieldPosition Position, SkillValue Value)>? positions, IEnumerable<(PlayerRole Role, SkillValue Value)>? roles)
    {
        if (!Enum.IsDefined(preferred))
            return Result<ProficiencyProfile>.Failure(new Error("Proficiency.position_invalid", "포지션 값이 올바르지 않습니다."));
        var pos = (positions ?? []).OrderBy(p => p.Position).ToList();
        if (pos.Any(p => !Enum.IsDefined(p.Position) || p.Position is FieldPosition.Pitcher or FieldPosition.DesignatedHitter))
            return Result<ProficiencyProfile>.Failure(new Error("Proficiency.position_not_fielding", "수비 숙련도는 야수 수비 포지션에만 둘 수 있습니다."));
        if (pos.Select(p => p.Position).Distinct().Count() != pos.Count)
            return Result<ProficiencyProfile>.Failure(new Error("Proficiency.duplicate_position", "같은 포지션의 숙련도가 중복되었습니다."));
        var rol = (roles ?? []).OrderBy(r => r.Role).ToList();
        if (rol.Any(r => !Enum.IsDefined(r.Role)) || rol.Select(r => r.Role).Distinct().Count() != rol.Count)
            return Result<ProficiencyProfile>.Failure(new Error("Proficiency.role_invalid", "역할 숙련도 값이 올바르지 않거나 중복되었습니다."));
        return Result<ProficiencyProfile>.Success(new ProficiencyProfile(preferred, pos.AsReadOnly(), rol.AsReadOnly()));
    }

    public SkillValue PositionProficiency(FieldPosition position) =>
        Positions.FirstOrDefault(p => p.Position == position).Value;

    public SkillValue RoleProficiency(PlayerRole role) =>
        Roles.FirstOrDefault(r => r.Role == role).Value;

    /// <summary>
    /// Moves the preferred position. Every recorded proficiency is kept exactly as it was, so returning to a known
    /// position later restores its familiarity instead of reinitializing it.
    /// </summary>
    public Result<(ProficiencyProfile Profile, PlayerChangedPosition Event)> ChangePosition(PlayerId playerId, FieldPosition to, DateOnly effectiveOn)
    {
        if (!Enum.IsDefined(to))
            return Result<(ProficiencyProfile, PlayerChangedPosition)>.Failure(new Error("Proficiency.position_invalid", "포지션 값이 올바르지 않습니다."));
        if (to == PreferredPosition)
            return Result<(ProficiencyProfile, PlayerChangedPosition)>.Failure(new Error("Proficiency.position_unchanged", "이미 같은 포지션입니다."));
        var next = new ProficiencyProfile(to, Positions, Roles);
        return Result<(ProficiencyProfile, PlayerChangedPosition)>.Success((next, new PlayerChangedPosition(playerId, PreferredPosition, to, effectiveOn)));
    }
}
