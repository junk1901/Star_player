using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Players;

// Event payload schemas only (ROADMAP Phase 1 "Events"): no event loop or publication yet. Payloads carry public facts
// and ids; they never carry DevelopmentProfile data. JSON names are documented in docs/data-model.md.

/// <summary>C01 <c>PlayerCreated</c>. <paramref name="Kind"/> is the derived hitter/pitcher/two-way label at creation.</summary>
public sealed record PlayerCreated(PlayerId PlayerId, DateOnly BirthDate, PlayerKind Kind, FieldPosition PreferredPosition, DateOnly CreatedOn);

/// <summary>C05 <c>PlayerChangedPosition</c>. Proficiency values are unchanged by the move itself.</summary>
public sealed record PlayerChangedPosition(PlayerId PlayerId, FieldPosition From, FieldPosition To, DateOnly EffectiveOn);

public static class PlayerEventFactory
{
    public static PlayerCreated Created(PlayerSheet sheet, DateOnly createdOn) =>
        new(sheet.Id, sheet.Player.BirthDate, sheet.Kind, sheet.Proficiency.PreferredPosition, createdOn);
}
