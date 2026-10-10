using StarPlayer.Application.Queries;
using StarPlayer.Application.Sessions;

namespace StarPlayer.Infrastructure.Fixtures;

/// <summary>
/// The only public way to obtain the Phase 1 player query service. It binds the trusted source and returns the safe
/// <see cref="PlayerInfoService"/> (public views only). Who "my player" is comes from the fixture's own binding
/// (<c>controlled_player_id</c> in the pack, standing in for the Phase 4 career binding), never from a parameter here.
/// </summary>
public static class PlayerInfoComposition
{
    public static PlayerInfoService CreateFictionalPhase1(PlayerSessionOptions? options = null) =>
        new(new FixturePlayerWorldSource(), options);
}
