using StarPlayer.Domain.Common;

namespace StarPlayer.Application.Sessions;

/// <summary>
/// Session-level settings supplied by the composition root, never by a screen or by a query. It deliberately has no
/// player identity: who "my player" is comes from the trusted world binding, so no option can impersonate another player.
/// </summary>
public sealed record PlayerSessionOptions(bool CheatMode = false);

/// <summary>
/// Who is looking. Created by the application session from trusted startup data; it has no public constructor, no
/// public setter and is never accepted from a query. A screen asking "show me exact values" has no way to say so:
/// <see cref="IsSelf"/> is decided here from the trusted binding's controlled player and the target's id only.
/// <see cref="CheatMode"/> widens nothing about visibility in Phase 1 (cheat grants rating freedom at creation, not
/// knowledge of hidden state).
/// </summary>
internal sealed class ViewerContext
{
    public PlayerId ControlledPlayer { get; }
    public bool CheatMode { get; }

    internal ViewerContext(PlayerId controlledPlayer, bool cheatMode)
    {
        ControlledPlayer = controlledPlayer;
        CheatMode = cheatMode;
    }

    public bool IsSelf(PlayerId target) => target == ControlledPlayer;
}
