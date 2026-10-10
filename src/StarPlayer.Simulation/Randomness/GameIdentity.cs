using System.Globalization;
using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Randomness;

/// <summary>
/// Stable game ids. <c>game:{competition key}:{fixture key}</c> is a pure function of its two parts (no clock, no counter, no RNG), so the same
/// fixture always yields the same id and different (competition, fixture) pairs never collide: parts may not contain ':', which makes the
/// join injective. Each part is 1+ characters of letters, digits, <c>_ - .</c>; the whole id is at most 64 characters (an id-like text,
/// usable as a <see cref="GameRandom.ContextKey"/>).
/// </summary>
public static class GameIds
{
    public static Result<string> Create(string? competitionKey, string? fixtureKey)
    {
        if (!IsPart(competitionKey) || !IsPart(fixtureKey))
            return Result<string>.Failure(new Error("Id.game_part_invalid", "경기 ID 구성 요소는 영문·숫자·'_'·'-'·'.'로 된 1자 이상이어야 합니다."));
        string id = $"game:{competitionKey}:{fixtureKey}";
        return id.Length > 64
            ? Result<string>.Failure(new Error("Id.game_too_long", "경기 ID는 64자 이하여야 합니다."))
            : Result<string>.Success(id);
    }

    private static bool IsPart(string? s) =>
        !string.IsNullOrEmpty(s) && s.All(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-' or '.');
}

/// <summary>
/// Event ids of one game: <c>{game id}:e{sequence, at least 6 digits}</c> with sequence 1, 2, 3... assigned in event order. The sequence is the
/// only state; ids never depend on randomness, time or content, so generating ids cannot consume any game RNG and replays reproduce them.
/// Resuming a saved game passes the last sequence already issued, so an id is never issued twice. Not thread-safe (one serialized writer).
/// </summary>
public sealed class GameEventIds
{
    /// <summary>Largest sequence; keeps the fixed 6-digit text ordered and a game far larger than any real one (~2,000 events) legal.</summary>
    public const int MaxSequence = 999_999;

    private GameEventIds(string gameId, int lastIssued)
    {
        GameId = gameId;
        LastIssued = lastIssued;
    }

    public string GameId { get; }

    /// <summary>Highest sequence issued so far (0 = none yet); persist it with the game state.</summary>
    public int LastIssued { get; private set; }

    public static Result<GameEventIds> Create(string? gameId, int lastIssued = 0)
    {
        if (StreamKeyDerivation.ValidateContext(gameId) is not null)
            return Result<GameEventIds>.Failure(new Error("Id.game_invalid", "경기 ID가 올바르지 않습니다."));
        if (lastIssued < 0 || lastIssued > MaxSequence)
            return Result<GameEventIds>.Failure(new Error("Id.sequence_invalid", "사건 순번이 범위를 벗어났습니다."));
        return Result<GameEventIds>.Success(new GameEventIds(gameId!, lastIssued));
    }

    /// <summary>The next id. At <see cref="MaxSequence"/> it fails instead of wrapping, so an id can never repeat.</summary>
    public Result<string> Next()
    {
        if (LastIssued >= MaxSequence)
            return Result<string>.Failure(new Error("Id.sequence_exhausted", "사건 순번이 한도에 도달했습니다."));
        LastIssued++;
        return Result<string>.Success(Format(GameId, LastIssued));
    }

    /// <summary>The sequence of an event id of this game; any other game, non-canonical text or out-of-range sequence is rejected.</summary>
    public Result<int> ParseSequence(string? eventId)
    {
        string prefix = $"{GameId}:e";
        if (eventId is null || !eventId.StartsWith(prefix, StringComparison.Ordinal)
            || !int.TryParse(eventId.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int sequence)
            || sequence < 1 || sequence > MaxSequence || Format(GameId, sequence) != eventId)
            return Result<int>.Failure(new Error("Id.event_id_invalid", "이 경기의 사건 ID가 아닙니다."));
        return Result<int>.Success(sequence);
    }

    private static string Format(string gameId, int sequence) => $"{gameId}:e{sequence.ToString("D6", CultureInfo.InvariantCulture)}";
}
