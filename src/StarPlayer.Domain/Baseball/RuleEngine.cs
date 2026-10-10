using System.Collections.ObjectModel;
using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Baseball;

/// <summary>
/// The typed KBO Rule Engine areas of C40. Each area has exactly one versioned owner; later phases add the area's
/// content (Phase 2 GameRules, Phase 6 RegularSeasonRules, ...). Phase 1 defines only the area/version boundary and
/// contains no 2026 constants.
/// </summary>
public enum RuleArea { Game, RegularSeason, Roster, Registration, Postseason, Draft, Contract, FreeAgency, ForeignPlayer, Award }

public readonly record struct RuleVersionId : IStringId<RuleVersionId>
{
    public string Value { get; private init; }

    /// <summary><c>default(RuleVersionId)</c> carries no text and is never a valid version; it is rejected wherever a version is required.</summary>
    public bool IsDefault => Value is null;

    public static Result<RuleVersionId> Create(string? value) =>
        IdText.Validate(value, nameof(RuleVersionId)) is { } error
            ? Result<RuleVersionId>.Failure(error)
            : Result<RuleVersionId>.Success(new RuleVersionId { Value = value! });

    public override string ToString() => Value ?? "";
}

/// <summary>A reference from a profile to one area version.</summary>
public readonly record struct RuleAreaRef(RuleArea Area, RuleVersionId Version);

/// <summary>
/// A documented simplification (D2) of a rule area version: a stable id within its definition and a required description.
/// Immutable; only <see cref="Create"/> builds one.
/// </summary>
public sealed class RuleSimplification
{
    public const int MaxDescriptionLength = 200;

    public string Id { get; }
    public string Description { get; }

    private RuleSimplification(string id, string description)
    {
        Id = id;
        Description = description;
    }

    public static Result<RuleSimplification> Create(string? id, string? description)
    {
        if (IdText.Validate(id, "RuleSimplification.id") is { } error)
            return Result<RuleSimplification>.Failure(new Error("RuleSimplification.id_invalid", error.Message));
        if (string.IsNullOrWhiteSpace(description) || description != description.Trim() || description.Length > MaxDescriptionLength || description.Any(char.IsControl))
            return Result<RuleSimplification>.Failure(new Error("RuleSimplification.description_invalid", $"단순화 설명은 앞뒤 공백 없이 1~{MaxDescriptionLength}자여야 합니다."));
        return Result<RuleSimplification>.Success(new RuleSimplification(id!, description));
    }
}

/// <summary>
/// One registered area version: effective window, provenance and its documented simplifications. Immutable and built only
/// through <see cref="Create"/>, so a definition in a catalog or profile is always valid and cannot be edited afterwards.
/// </summary>
public sealed class RuleAreaDefinition
{
    public RuleArea Area { get; }
    public RuleVersionId Version { get; }
    public DateOnly EffectiveFrom { get; }
    public DateOnly? EffectiveTo { get; }
    public string Provenance { get; }
    public IReadOnlyList<RuleSimplification> Simplifications { get; }

    private RuleAreaDefinition(RuleArea area, RuleVersionId version, DateOnly from, DateOnly? to, string provenance, IReadOnlyList<RuleSimplification> simplifications)
    {
        Area = area;
        Version = version;
        EffectiveFrom = from;
        EffectiveTo = to;
        Provenance = provenance;
        Simplifications = simplifications;
    }

    public static Result<RuleAreaDefinition> Create(RuleArea area, RuleVersionId version, DateOnly effectiveFrom, DateOnly? effectiveTo,
        string? provenance, IEnumerable<RuleSimplification>? simplifications)
    {
        if (!Enum.IsDefined(area))
            return Result<RuleAreaDefinition>.Failure(new Error("RuleCatalog.area_unknown", "알 수 없는 규칙 영역입니다."));
        if (version.IsDefault)
            return Result<RuleAreaDefinition>.Failure(new Error("RuleVersionId.invalid", $"{area} 규칙 정의에 규칙 버전 ID가 없습니다."));
        if (effectiveTo is { } to && to <= effectiveFrom)
            return Result<RuleAreaDefinition>.Failure(new Error("RuleCatalog.window_invalid", $"{area}/{version} 규칙의 유효 기간이 올바르지 않습니다."));
        if (string.IsNullOrWhiteSpace(provenance))
            return Result<RuleAreaDefinition>.Failure(new Error("RuleCatalog.provenance_missing", $"{area}/{version} 규칙에 출처 설명이 없습니다."));
        if (simplifications is null)
            return Result<RuleAreaDefinition>.Failure(new Error("RuleCatalog.simplifications_missing", $"{area}/{version} 규칙의 단순화 목록이 없습니다. 없으면 빈 목록을 주세요."));

        var copy = simplifications.ToList(); // defensive copy: later edits to the caller's list cannot reach the definition
        if (copy.Any(s => s is null))
            return Result<RuleAreaDefinition>.Failure(new Error("RuleSimplification.missing", $"{area}/{version} 규칙의 단순화 항목에 빈 값이 있습니다."));
        if (copy.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != copy.Count)
            return Result<RuleAreaDefinition>.Failure(new Error("RuleSimplification.duplicate_id", $"{area}/{version} 규칙에 같은 단순화 ID가 중복되었습니다."));

        return Result<RuleAreaDefinition>.Success(new RuleAreaDefinition(area, version, effectiveFrom, effectiveTo, provenance, copy.OrderBy(s => s.Id, StringComparer.Ordinal).ToList().AsReadOnly()));
    }

    public bool IsEffectiveOn(DateOnly date) => date >= EffectiveFrom && (EffectiveTo is null || date < EffectiveTo);
}

/// <summary>
/// Lookup of registered (area, version) pairs. Unknown or default area/version is an explicit error, never a default.
/// Versions of one area form a timeline: their effective windows may not overlap, so a date selects at most one.
/// </summary>
public sealed class RuleCatalog
{
    private readonly Dictionary<(RuleArea, string), RuleAreaDefinition> _byKey;

    public IReadOnlyList<RuleAreaDefinition> Definitions { get; }

    private RuleCatalog(List<RuleAreaDefinition> definitions)
    {
        Definitions = definitions.AsReadOnly();
        _byKey = definitions.ToDictionary(d => (d.Area, d.Version.Value));
    }

    public static Result<RuleCatalog> Create(IEnumerable<RuleAreaDefinition>? definitions)
    {
        if (definitions is null)
            return Result<RuleCatalog>.Failure(new Error("RuleCatalog.definitions_missing", "규칙 정의 목록이 없습니다."));
        var list = definitions.ToList(); // defensive copy
        if (list.Any(d => d is null))
            return Result<RuleCatalog>.Failure(new Error("RuleCatalog.definition_missing", "규칙 정의 목록에 빈 항목이 있습니다."));
        list = list.OrderBy(d => d.Area).ThenBy(d => d.EffectiveFrom).ThenBy(d => d.Version.Value, StringComparer.Ordinal).ToList();

        if (list.GroupBy(d => (d.Area, d.Version.Value)).Any(g => g.Count() > 1))
            return Result<RuleCatalog>.Failure(new Error("RuleCatalog.duplicate_version", "같은 영역에 같은 규칙 버전이 두 번 등록되었습니다."));
        foreach (var area in list.GroupBy(d => d.Area))
        {
            var timeline = area.ToList();
            for (var i = 0; i + 1 < timeline.Count; i++)
                if (timeline[i].EffectiveTo is null || timeline[i].EffectiveTo > timeline[i + 1].EffectiveFrom)
                    return Result<RuleCatalog>.Failure(new Error("RuleCatalog.overlapping_versions",
                        $"{area.Key} 영역의 '{timeline[i].Version}'와 '{timeline[i + 1].Version}' 유효 기간이 겹칩니다."));
        }

        return Result<RuleCatalog>.Success(new RuleCatalog(list));
    }

    public Result<RuleAreaDefinition> Resolve(RuleArea area, RuleVersionId version)
    {
        if (!Enum.IsDefined(area))
            return Result<RuleAreaDefinition>.Failure(new Error("RuleCatalog.area_unknown", "알 수 없는 규칙 영역입니다."));
        if (version.IsDefault)
            return Result<RuleAreaDefinition>.Failure(new Error("RuleVersionId.invalid", $"{area} 영역 조회에 규칙 버전 ID가 없습니다."));
        return _byKey.TryGetValue((area, version.Value), out var found)
            ? Result<RuleAreaDefinition>.Success(found)
            : Result<RuleAreaDefinition>.Failure(new Error("RuleCatalog.version_not_found", $"{area} 영역에 '{version}' 규칙 버전이 없습니다."));
    }
}

/// <summary>
/// A composition of one resolved version per rule area, frozen for a career (C40). Composition is validated against the
/// catalog and an effective date; a missing area is an explicit error for consumers, not a fallback. Because definitions
/// are immutable and the area list is a defensive read-only copy, the whole snapshot is immutable.
/// </summary>
public sealed class RuleProfile
{
    public RuleProfileId Id { get; }
    public string Name { get; }
    public DateOnly EffectiveOn { get; }
    public IReadOnlyList<RuleAreaDefinition> Areas { get; }

    private RuleProfile(RuleProfileId id, string name, DateOnly effectiveOn, IReadOnlyList<RuleAreaDefinition> areas)
    {
        Id = id;
        Name = name;
        EffectiveOn = effectiveOn;
        Areas = areas;
    }

    public static Result<RuleProfile> Create(RuleProfileId id, string? name, DateOnly effectiveOn, IEnumerable<RuleAreaRef>? refs, RuleCatalog? catalog)
    {
        if (id == default)
            return Result<RuleProfile>.Failure(new Error("RuleProfile.id_missing", "규칙 프로필 ID가 없습니다."));
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length > 80)
            return Result<RuleProfile>.Failure(new Error("RuleProfile.name_missing", "규칙 프로필 이름은 앞뒤 공백 없이 1~80자여야 합니다."));
        if (catalog is null)
            return Result<RuleProfile>.Failure(new Error("RuleProfile.catalog_missing", "규칙 카탈로그가 없습니다."));
        var list = refs?.ToList() ?? []; // defensive copy
        if (list.Count == 0)
            return Result<RuleProfile>.Failure(new Error("RuleProfile.empty", "규칙 프로필에는 규칙 영역이 하나 이상 있어야 합니다."));
        if (list.Select(r => r.Area).Distinct().Count() != list.Count)
            return Result<RuleProfile>.Failure(new Error("RuleProfile.conflicting_areas", "한 규칙 영역에 두 개 이상의 버전이 지정되었습니다."));

        var resolved = new List<RuleAreaDefinition>();
        foreach (var reference in list.OrderBy(r => r.Area))
        {
            var found = catalog.Resolve(reference.Area, reference.Version);
            if (!found.IsSuccess)
                return Result<RuleProfile>.Failure(found.Error!.Value);
            if (!found.Value.IsEffectiveOn(effectiveOn))
                return Result<RuleProfile>.Failure(new Error("RuleProfile.version_not_effective", $"{reference.Area}/{reference.Version} 규칙은 {effectiveOn:yyyy-MM-dd}에 유효하지 않습니다."));
            resolved.Add(found.Value);
        }

        return Result<RuleProfile>.Success(new RuleProfile(id, name, effectiveOn, resolved.AsReadOnly()));
    }

    /// <summary>The single versioned definition for an area, or an explicit "area not in profile" error.</summary>
    public Result<RuleAreaDefinition> ForArea(RuleArea area)
    {
        var found = Areas.FirstOrDefault(a => a.Area == area);
        return found is null
            ? Result<RuleAreaDefinition>.Failure(new Error("RuleProfile.area_missing", $"이 규칙 프로필에는 {area} 영역이 없습니다."))
            : Result<RuleAreaDefinition>.Success(found);
    }
}
