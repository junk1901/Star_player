using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;

namespace StarPlayer.Infrastructure.Fixtures;

/// <summary>
/// B-1 PZL #6 (OWNER decision 2, 2026-10-24): the ONE public entry point to the existing event-format-2 writer / reader, so the independent evidence verifier round-trips events with the real codec instead of
/// a copy. It only forwards to <see cref="ScriptedGameJson"/>; the format, its bytes and its round-trip meaning are unchanged, and the DTOs and other readers stay internal.
/// </summary>
public static class EventJsonV2Codec
{
    public static string Write(IReadOnlyList<GameEvent> events) => ScriptedGameJson.WriteEventsV2(events);

    public static Result<IReadOnlyList<GameEvent>> Read(string json) => ScriptedGameJson.ReadEventsV2(json);
}
