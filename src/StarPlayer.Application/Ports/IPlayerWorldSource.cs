using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Domain.Teams;

namespace StarPlayer.Application.Ports;

/// <summary>
/// Everything a fixture/datapack supplies for the Phase 1 player model, including the trusted binding of which player is
/// "mine" (<see cref="ControlledPlayer"/>; Phase 4 replaces it with the saved career binding). INTERNAL: the hidden
/// <see cref="DevelopmentProfile"/> list is world content that no public API may return, and the query service never
/// reads it. Lists keep source order.
/// <para>
/// <see cref="BatterTendencies"/> (Phase 3-D, DataPack v2) is the second hidden world member: an immutable, sparse set of batters' zone tendencies for the resolver only. It is the
/// designated internal holder (HiddenTendencyBoundaryTests names exactly this member); it is never copied into <c>PlayerSheet</c>, a public view or a log (its text form is
/// <c>BatterZoneProfiles(hidden)</c>). <see cref="Format"/> remembers the structure the pack was read from so writing it again does not change it.
/// </para>
/// </summary>
internal sealed record PlayerWorld(
    string Label,
    DateOnly AsOfDate,
    PlayerId ControlledPlayer,
    IReadOnlyList<Club> Clubs,
    IReadOnlyList<Stadium> Stadiums,
    ParkFactorBounds ParkFactorBounds,
    IReadOnlyList<ParkFactorProfile> ParkFactorProfiles,
    IReadOnlyList<StadiumUsage> StadiumUsages,
    RuleCatalog RuleCatalog,
    RuleProfile RuleProfile,
    IReadOnlyList<PlayerSheet> Players,
    IReadOnlyList<DevelopmentProfile> Development,
    JerseyNumberLedger Jerseys,
    IReadOnlyList<BatteryChemistry> Batteries,
    BatterZoneProfiles BatterTendencies,
    WorldFormat Format);

/// <summary>
/// The structure of the pack a world was read from (D-T1): its <c>format_version</c> and whether the optional <c>batter_zone_tendencies</c> collection was present (an empty array is
/// present; omitted is not). Whether a present collection is empty or has entries is the profile count, so v2-omitted, v2-empty and v2-with-entries write back unchanged.
/// </summary>
internal sealed record WorldFormat(int Version, bool BatterTendenciesPresent)
{
    public static WorldFormat V1 { get; } = new(1, false);
}

/// <summary>Internal port for loading the Phase 1 world. Implementations return a typed failure instead of throwing for bad content.</summary>
internal interface IPlayerWorldSource
{
    Result<PlayerWorld> Load();
}
