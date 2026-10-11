using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Baseball.PitchZone;

// Pitch Zone Lite (ROADMAP 1.14), model pzl-zones-v1. Categorical and virtual: a zone is a named region, never a measured coordinate and never
// real ABS data. Renaming, adding or re-meaning a zone, or changing the boundary table, needs a new ZoneModelVersion - never an edit in place.

/// <summary>The version of the zone set and its boundary table. Only versions this build knows can be created; an unknown one is an error, never a default.</summary>
public readonly record struct ZoneModelVersion : IStringId<ZoneModelVersion>
{
    public const string Pzl1Text = "pzl-zones-v1";

    public static ZoneModelVersion Pzl1 { get; } = new() { Value = Pzl1Text };

    public string Value { get; private init; }

    public static Result<ZoneModelVersion> Create(string? value)
    {
        if (value is null)
            return Result<ZoneModelVersion>.Failure(new Error("ZoneModelVersion.empty", "ZoneModelVersion 값이 비어 있습니다."));
        return string.Equals(value, Pzl1Text, StringComparison.Ordinal)
            ? Result<ZoneModelVersion>.Success(Pzl1)
            : Result<ZoneModelVersion>.Failure(new Error("ZoneModelVersion.unsupported", $"지원하지 않는 구역 모델 버전입니다: '{value}'"));
    }

    public override string ToString() => Value ?? "";
}

/// <summary>A stable zone identifier (persisted text such as <c>in.r2c1</c>). Meaning depends on the <see cref="ZoneModelVersion"/> it is used with.</summary>
public readonly record struct PitchZoneId : IStringId<PitchZoneId>
{
    public string Value { get; private init; }

    public static Result<PitchZoneId> Create(string? value) =>
        value is not null && PitchZoneModel.V1.TryFind(value, out var info)
            ? Result<PitchZoneId>.Success(info.Id)
            : Result<PitchZoneId>.Failure(new Error("PitchZoneId.unknown", $"알 수 없는 구역 ID입니다: '{value}'"));

    internal static PitchZoneId Unchecked(string value) => new() { Value = value };

    public override string ToString() => Value ?? "";
}

/// <summary>Inner = one of the nine strike-zone cells; Outer = one of the eight directions just outside the zone.</summary>
public enum PitchZoneRegion { Inner, Outer }

/// <summary>Batter-relative horizontal position: Inside is toward the batter whichever side he stands on.</summary>
public enum ZoneLateral { Inside, Middle, Outside }

public enum ZoneVertical { High, Middle, Low }

/// <summary>
/// The minimal boundary class used by call-mode and framing rules. Fixed per zone by the model version's table (docs/game-engine.md section 18);
/// it is a property of the virtual region, not a measurement of the pitch.
/// </summary>
public enum BoundaryClass { Clear, Borderline }

/// <summary>The side the batter actually stood on for one pitch. A switch hitter's declared <c>Switch</c> is never a pitch fact; this is.</summary>
public enum AppliedBatterSide { Right, Left }

/// <summary>Horizontal position as the catcher sees it (looking at the pitcher).</summary>
public enum CatcherHorizontal { Left, Center, Right }

/// <summary>An absolute (catcher's-view) position of a zone; reconstructable from a batter-relative zone and the applied side.</summary>
public readonly record struct CatcherViewPosition(PitchZoneRegion Region, CatcherHorizontal Horizontal, ZoneVertical Vertical);

/// <summary>One zone of a model: where it is (batter-relative) and its boundary class. <see cref="InnerIndex"/> is the row-major 0..8 index of an inner cell, -1 for an outer zone.</summary>
public sealed record PitchZoneInfo(PitchZoneId Id, PitchZoneRegion Region, ZoneLateral Lateral, ZoneVertical Vertical, BoundaryClass Boundary, int InnerIndex);

/// <summary>The immutable zone set of one <see cref="ZoneModelVersion"/>. The only model today is <see cref="V1"/>.</summary>
public sealed class PitchZoneModel
{
    /// <summary>
    /// pzl-zones-v1. Inner cells <c>in.r{row}c{col}</c>: row 1 = high, 3 = low; column 1 = inside, 3 = outside (batter-relative). Outer zones, in canonical order:
    /// high, high_in, in, low_in, low, low_out, out, high_out. Boundary table: a region that shares an edge with the strike-zone outline is
    /// <see cref="BoundaryClass.Borderline"/> (the eight non-centre inner cells and the four outer zones that border an edge); the centre cell and the four
    /// diagonal outer zones, which touch the outline at most at a corner point, are <see cref="BoundaryClass.Clear"/>.
    /// </summary>
    public static PitchZoneModel V1 { get; } = BuildV1();

    public ZoneModelVersion Version { get; }

    public IReadOnlyList<PitchZoneInfo> Zones { get; }

    private readonly Dictionary<string, PitchZoneInfo> _byText;

    private PitchZoneModel(ZoneModelVersion version, List<PitchZoneInfo> zones)
    {
        Version = version;
        Zones = zones.AsReadOnly();
        _byText = zones.ToDictionary(z => z.Id.Value, StringComparer.Ordinal);
    }

    public static Result<PitchZoneModel> Resolve(ZoneModelVersion version) =>
        version.Value == ZoneModelVersion.Pzl1Text
            ? Result<PitchZoneModel>.Success(V1)
            : Result<PitchZoneModel>.Failure(new Error("ZoneModelVersion.unsupported", $"지원하지 않는 구역 모델 버전입니다: '{version}'"));

    internal bool TryFind(string text, out PitchZoneInfo info) => _byText.TryGetValue(text, out info!);

    public Result<PitchZoneInfo> Info(PitchZoneId zone) =>
        zone.Value is not null && _byText.TryGetValue(zone.Value, out var found)
            ? Result<PitchZoneInfo>.Success(found)
            : Result<PitchZoneInfo>.Failure(new Error("PitchZoneId.unknown", $"이 모델에 없는 구역입니다: '{zone}'"));

    public Result<BoundaryClass> Classify(PitchZoneId zone)
    {
        var info = Info(zone);
        return info.IsSuccess ? Result<BoundaryClass>.Success(info.Value.Boundary) : Result<BoundaryClass>.Failure(info.Error!.Value);
    }

    /// <summary>
    /// The catcher's-view position of a batter-relative zone for the side the batter actually stood on. Convention of this model (not a measurement):
    /// a right-handed batter stands on the catcher's left, so his inside is the catcher's left; a left-handed batter's inside is the catcher's right.
    /// </summary>
    public Result<CatcherViewPosition> ToCatcherView(PitchZoneId zone, AppliedBatterSide side)
    {
        var info = Info(zone);
        if (!info.IsSuccess)
            return Result<CatcherViewPosition>.Failure(info.Error!.Value);
        if (!Enum.IsDefined(side))
            return Result<CatcherViewPosition>.Failure(new Error("AppliedBatterSide.invalid", "타석 방향 값이 올바르지 않습니다."));
        var z = info.Value;
        var horizontal = z.Lateral switch
        {
            ZoneLateral.Middle => CatcherHorizontal.Center,
            ZoneLateral.Inside => side == AppliedBatterSide.Right ? CatcherHorizontal.Left : CatcherHorizontal.Right,
            _ => side == AppliedBatterSide.Right ? CatcherHorizontal.Right : CatcherHorizontal.Left,
        };
        return Result<CatcherViewPosition>.Success(new CatcherViewPosition(z.Region, horizontal, z.Vertical));
    }

    /// <summary>The batter-relative zone behind a catcher's-view position for the given applied side (inverse of <see cref="ToCatcherView"/>).</summary>
    public Result<PitchZoneId> FromCatcherView(CatcherViewPosition position, AppliedBatterSide side)
    {
        if (!Enum.IsDefined(side))
            return Result<PitchZoneId>.Failure(new Error("AppliedBatterSide.invalid", "타석 방향 값이 올바르지 않습니다."));
        var found = Zones.Where(z => ToCatcherView(z.Id, side).Value == position).ToList();
        return found.Count == 1
            ? Result<PitchZoneId>.Success(found[0].Id)
            : Result<PitchZoneId>.Failure(new Error("PitchZone.catcher_view_invalid", "이 위치에는 해당하는 구역이 없습니다."));
    }

    private static PitchZoneModel BuildV1()
    {
        var zones = new List<PitchZoneInfo>();
        ZoneVertical[] rows = [ZoneVertical.High, ZoneVertical.Middle, ZoneVertical.Low];
        ZoneLateral[] columns = [ZoneLateral.Inside, ZoneLateral.Middle, ZoneLateral.Outside];
        for (var r = 0; r < 3; r++)
            for (var c = 0; c < 3; c++)
                zones.Add(new PitchZoneInfo(PitchZoneId.Unchecked($"in.r{r + 1}c{c + 1}"), PitchZoneRegion.Inner, columns[c], rows[r],
                    r == 1 && c == 1 ? BoundaryClass.Clear : BoundaryClass.Borderline, r * 3 + c));

        void Outer(string name, ZoneLateral lateral, ZoneVertical vertical, BoundaryClass boundary) =>
            zones.Add(new PitchZoneInfo(PitchZoneId.Unchecked($"out.{name}"), PitchZoneRegion.Outer, lateral, vertical, boundary, -1));

        Outer("high", ZoneLateral.Middle, ZoneVertical.High, BoundaryClass.Borderline);
        Outer("high_in", ZoneLateral.Inside, ZoneVertical.High, BoundaryClass.Clear);
        Outer("in", ZoneLateral.Inside, ZoneVertical.Middle, BoundaryClass.Borderline);
        Outer("low_in", ZoneLateral.Inside, ZoneVertical.Low, BoundaryClass.Clear);
        Outer("low", ZoneLateral.Middle, ZoneVertical.Low, BoundaryClass.Borderline);
        Outer("low_out", ZoneLateral.Outside, ZoneVertical.Low, BoundaryClass.Clear);
        Outer("out", ZoneLateral.Outside, ZoneVertical.Middle, BoundaryClass.Borderline);
        Outer("high_out", ZoneLateral.Outside, ZoneVertical.High, BoundaryClass.Clear);

        return new PitchZoneModel(ZoneModelVersion.Pzl1, zones);
    }
}
