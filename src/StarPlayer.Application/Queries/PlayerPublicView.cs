namespace StarPlayer.Application.Queries;

// Public read models for screens. They are plain strings/ints/bools and deliberately reference no Domain type, so a
// presenter can neither reach hidden state through them nor mutate anything. The hidden development profile
// (true potential, growth type, prime length, decline onset, per-skill ceilings) has NO counterpart in this file.

/// <summary>What a screen sends to ask about one player. It carries an id and nothing else: no viewer, role or permission field.</summary>
public sealed record PlayerQuery(string PlayerId);

/// <summary>One line of a player's abilities. Exactly one of the two shapes is filled: <see cref="Exact"/> for the viewer's own player, or grade + range + confidence for everyone else.</summary>
public sealed record AbilityLine(
    string Key,
    string Label,
    string Group,
    int? Exact,
    string? Grade,
    int? RangeLow,
    int? RangeHigh,
    string? Confidence,
    string Display);

public sealed record PitchLine(string Key, string Label, int? Exact, string? Grade, int? RangeLow, int? RangeHigh, string? Confidence, string Display);

/// <summary>Public, viewer-specific facts about one player. Built only by <c>PlayerVisibilityPolicy</c>.</summary>
public sealed record PlayerPublicView(
    string PlayerId,
    string DisplayName,
    bool IsOwnPlayer,
    string ViewKind,
    int Age,
    string BirthDate,
    string Kind,
    string Handedness,
    int HeightCm,
    int WeightKg,
    IReadOnlyList<string> Personality,
    string Biography,
    string? ClubId,
    string? ClubName,
    string? CurrentJerseyNumber,
    string? JerseySince,
    string Origin,
    string EntryPath,
    string Registration,
    string League,
    string CareerStatus,
    string PreferredPosition,
    IReadOnlyList<AbilityLine> BaseAbilities,
    IReadOnlyList<PitchLine> Pitches,
    IReadOnlyList<AbilityLine> CurrentState,
    IReadOnlyList<AbilityLine> Proficiencies);

/// <summary>One row of the player picker.</summary>
public sealed record PlayerListItem(string PlayerId, string DisplayName, string ClubName, string Kind, string PositionLabel, bool IsOwnPlayer);

/// <summary>Header facts for the diagnostic screen.</summary>
public sealed record WorldSummaryView(string Label, string AsOfDate, string OwnPlayerName, bool CheatMode, int PlayerCount, int ClubCount, string ObservationModel);

/// <summary>Outcome of a player query: exactly one of <see cref="View"/> or the error pair is set.</summary>
public sealed record PlayerQueryResult(PlayerPublicView? View, string? ErrorCode, string? ErrorMessage)
{
    public bool IsSuccess => View is not null;

    public static PlayerQueryResult Success(PlayerPublicView view) => new(view, null, null);

    public static PlayerQueryResult Failure(string code, string message) => new(null, code, message);
}
