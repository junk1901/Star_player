using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Domain.Teams;

namespace StarPlayer.Application.Ports;

/// <summary>
/// Everything a fixture/datapack supplies for the Phase 1 player model, including the trusted binding of which player is
/// "mine" (<see cref="ControlledPlayer"/>; Phase 4 replaces it with the saved career binding). INTERNAL: the hidden
/// <see cref="DevelopmentProfile"/> list is world content that no public API may return, and the query service never
/// reads it. Lists keep source order.
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
    IReadOnlyList<BatteryChemistry> Batteries);

/// <summary>Internal port for loading the Phase 1 world. Implementations return a typed failure instead of throwing for bad content.</summary>
internal interface IPlayerWorldSource
{
    Result<PlayerWorld> Load();
}
