using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Domain.Baseball.PitchZone;

/// <summary>
/// The confirmed categorical location of one actual pitch: zone, the batter side actually applied to that pitch, and the boundary class the model
/// version's table assigns to the zone. A distinct type from <see cref="TargetZone"/> (what the pitcher aimed at). Phase 3-B1 only defines and
/// validates the value; nothing draws or records one yet (Phase 3-C).
/// </summary>
public sealed record PitchLocation
{
    public ZoneModelVersion Model { get; }
    public PitchZoneId Zone { get; }
    public AppliedBatterSide Side { get; }
    public BoundaryClass Boundary { get; }

    private PitchLocation(ZoneModelVersion model, PitchZoneId zone, AppliedBatterSide side, BoundaryClass boundary)
    {
        Model = model;
        Zone = zone;
        Side = side;
        Boundary = boundary;
    }

    /// <summary>Build a location with its boundary class stated explicitly (for reading stored data); a class that is not the table's class for the zone is rejected.</summary>
    public static Result<PitchLocation> Create(ZoneModelVersion model, PitchZoneId zone, AppliedBatterSide side, BoundaryClass boundary)
    {
        var resolved = PitchZoneModel.Resolve(model);
        if (!resolved.IsSuccess)
            return Result<PitchLocation>.Failure(resolved.Error!.Value);
        var info = resolved.Value.Info(zone);
        if (!info.IsSuccess)
            return Result<PitchLocation>.Failure(info.Error!.Value);
        if (!Enum.IsDefined(side))
            return Result<PitchLocation>.Failure(new Error("PitchLocation.side_invalid", "투구 위치의 타석 방향 값이 올바르지 않습니다."));
        if (!Enum.IsDefined(boundary))
            return Result<PitchLocation>.Failure(new Error("PitchLocation.boundary_invalid", "투구 위치의 경계 분류 값이 올바르지 않습니다."));
        if (info.Value.Boundary != boundary)
            return Result<PitchLocation>.Failure(new Error("PitchLocation.boundary_mismatch", $"구역 '{zone}'의 경계 분류는 {info.Value.Boundary}이어야 합니다(모델 {model})."));
        return Result<PitchLocation>.Success(new PitchLocation(model, zone, side, boundary));
    }

    /// <summary>Build a location whose boundary class is the table's class for the zone.</summary>
    public static Result<PitchLocation> FromZone(ZoneModelVersion model, PitchZoneId zone, AppliedBatterSide side)
    {
        var resolved = PitchZoneModel.Resolve(model);
        if (!resolved.IsSuccess)
            return Result<PitchLocation>.Failure(resolved.Error!.Value);
        var boundary = resolved.Value.Classify(zone);
        return boundary.IsSuccess ? Create(model, zone, side, boundary.Value) : Result<PitchLocation>.Failure(boundary.Error!.Value);
    }
}

/// <summary>The zone a pitcher aims at (a strategy input). It has no side and no boundary class and can never be passed where a <see cref="PitchLocation"/> is expected.</summary>
public sealed record TargetZone
{
    public ZoneModelVersion Model { get; }
    public PitchZoneId Zone { get; }

    private TargetZone(ZoneModelVersion model, PitchZoneId zone)
    {
        Model = model;
        Zone = zone;
    }

    public static Result<TargetZone> Create(ZoneModelVersion model, PitchZoneId zone)
    {
        var resolved = PitchZoneModel.Resolve(model);
        if (!resolved.IsSuccess)
            return Result<TargetZone>.Failure(resolved.Error!.Value);
        var info = resolved.Value.Info(zone);
        return info.IsSuccess ? Result<TargetZone>.Success(new TargetZone(model, zone)) : Result<TargetZone>.Failure(info.Error!.Value);
    }
}

/// <summary>
/// Which side a batter may stand on for a pitch. A right- or left-handed batter has one possible side; a switch hitter has either, and the side
/// actually used for each pitch must be recorded with it. Which side a switch hitter chooses is a game rule of the pitch resolver (Phase 3-C), not decided here.
/// </summary>
public static class AppliedBatterSideRules
{
    public static Result<AppliedBatterSide> Validate(BattingSide declared, AppliedBatterSide applied)
    {
        if (!Enum.IsDefined(declared))
            return Result<AppliedBatterSide>.Failure(new Error("AppliedBatterSide.declared_invalid", "선언된 타석 방향 값이 올바르지 않습니다."));
        if (!Enum.IsDefined(applied))
            return Result<AppliedBatterSide>.Failure(new Error("AppliedBatterSide.invalid", "타석 방향 값이 올바르지 않습니다."));
        var possible = declared switch
        {
            BattingSide.Switch => true,
            BattingSide.Right => applied == AppliedBatterSide.Right,
            _ => applied == AppliedBatterSide.Left,
        };
        return possible
            ? Result<AppliedBatterSide>.Success(applied)
            : Result<AppliedBatterSide>.Failure(new Error("AppliedBatterSide.not_possible", $"{declared} 타자는 {applied} 타석에 설 수 없습니다."));
    }

    /// <summary>The only possible side of a one-sided batter; a switch hitter has none and must choose.</summary>
    public static Result<AppliedBatterSide> OnlySide(BattingSide declared) => declared switch
    {
        BattingSide.Right => Result<AppliedBatterSide>.Success(AppliedBatterSide.Right),
        BattingSide.Left => Result<AppliedBatterSide>.Success(AppliedBatterSide.Left),
        BattingSide.Switch => Result<AppliedBatterSide>.Failure(new Error("AppliedBatterSide.choice_required", "스위치 타자는 투구마다 실제 타석 방향을 정해야 합니다.")),
        _ => Result<AppliedBatterSide>.Failure(new Error("AppliedBatterSide.declared_invalid", "선언된 타석 방향 값이 올바르지 않습니다.")),
    };
}
