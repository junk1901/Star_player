using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// The official state of one game as a single immutable value (C3-A, game-engine 27.3 and 27.16): the <see cref="GameState"/>, the authoritative RNG position,
/// the last issued event sequence, a monotonic <see cref="Revision"/> and the last <see cref="CommitRecord"/>. Nothing outside Simulation can build or change one:
/// the constructor and the factories are <c>internal</c>, and a new value is made only by <see cref="CommitGate.Stage"/> and made current by <see cref="GameCursor.Publish"/>.
/// <para>
/// <b>Limit of a restored history:</b> a receipt holds the id, kind and fingerprint but not the event body, so <see cref="Restore"/> verifies id form, game
/// membership, continuity and receipt form (and the explicit event references); it cannot verify that a stored fingerprint matches an event body that was never stored.
/// </para>
/// </summary>
public sealed class AuthoritativeGame
{
    internal AuthoritativeGame(GameState state, GameRandomSnapshot rng, int lastEventSequence, long revision, CommitRecord? lastCommit)
    {
        State = state;
        Rng = rng;
        LastEventSequence = lastEventSequence;
        Revision = revision;
        LastCommit = lastCommit;
    }

    public GameState State { get; }

    /// <summary>The authoritative RNG position. An event that draws no random number leaves this very reference in place.</summary>
    public GameRandomSnapshot Rng { get; }

    /// <summary>The highest event sequence issued in this game (0 = none). Independent of <see cref="Revision"/>.</summary>
    public int LastEventSequence { get; }

    /// <summary>+1 for every successful Publish; never forced equal to <see cref="LastEventSequence"/> (a restored game keeps the stored value).</summary>
    public long Revision { get; }

    /// <summary>The commit that produced this value; <c>null</c> for a new or restored game.</summary>
    public CommitRecord? LastCommit { get; }

    /// <summary>A new game: empty receipt history, sequence 0, revision 0, no last commit.</summary>
    internal static Result<AuthoritativeGame> StartNew(GameState? state, GameRandomSnapshot? rng)
    {
        if (state is null)
            return Fail("Authoritative.state_missing", "경기 상태가 없습니다.");
        if (rng is null)
            return Fail("Authoritative.rng_missing", "난수 스냅샷이 없습니다.");
        if (!state.AppliedEvents.IsEmpty)
            return Fail("Authoritative.initial_state_not_empty", "새 경기의 사건 영수증 이력은 비어 있어야 합니다.");
        return Validated(state, rng, 0, 0);
    }

    /// <summary>A stored game. A corrupted or incomplete history is refused; nothing is renumbered, removed or repaired.</summary>
    internal static Result<AuthoritativeGame> Restore(GameState? state, GameRandomSnapshot? rng, int lastEventSequence, long revision)
    {
        if (state is null)
            return Fail("Authoritative.state_missing", "경기 상태가 없습니다.");
        if (rng is null)
            return Fail("Authoritative.rng_missing", "난수 스냅샷이 없습니다.");
        return Validated(state, rng, lastEventSequence, revision);
    }

    private static Result<AuthoritativeGame> Validated(GameState state, GameRandomSnapshot rng, int lastEventSequence, long revision)
    {
        var ids = GameEventIds.Create(state.GameId);
        if (!ids.IsSuccess)
            return Result<AuthoritativeGame>.Failure(ids.Error!.Value);
        if (GameExecutionRules.Validate(state.Execution) is { } badExecution)
            return Result<AuthoritativeGame>.Failure(badExecution); // a restored game's execution contract (park snapshot, pins) is validated again, never trusted
        var restored = GameRandom.Restore(rng);
        if (!restored.IsSuccess)
            return Result<AuthoritativeGame>.Failure(restored.Error!.Value);
        if (!string.Equals(rng.ContextKey, state.GameId, StringComparison.Ordinal))
            return Fail("Authoritative.rng_context_mismatch", "난수 맥락 키가 경기 ID와 다릅니다.");
        var range = GameEventIds.Create(state.GameId, lastEventSequence);
        if (!range.IsSuccess)
            return Result<AuthoritativeGame>.Failure(range.Error!.Value);

        var known = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < state.AppliedEvents.Count; i++)
        {
            var receipt = state.AppliedEvents[i];
            if (receipt is null || !ids.Value.ParseSequence(receipt.EventId).IsSuccess)
                return Fail("Authoritative.history_event_id_invalid", $"{i + 1}번째 영수증의 사건 ID가 이 경기의 정규 ID가 아닙니다.");
            var sequence = ids.Value.ParseSequence(receipt.EventId).Value;
            if (sequence > i + 1)
                return Fail("Authoritative.history_sequence_gap", $"{i + 1}번째 영수증의 순번이 {sequence}입니다(건너뜀 또는 순서 뒤바뀜).");
            if (sequence < i + 1)
                return Fail("Authoritative.history_sequence_duplicate", $"{i + 1}번째 영수증의 순번 {sequence}이 이미 쓰였습니다.");
            if (!IsReceiptForm(receipt))
                return Fail("Authoritative.history_receipt_invalid", $"{i + 1}번째 영수증의 종류·지문 버전·지문 형식이 올바르지 않습니다.");
            known.Add(receipt.EventId);
        }

        if (lastEventSequence != state.AppliedEvents.Count)
            return Fail("Authoritative.last_sequence_mismatch", "마지막 사건 순번이 영수증 수와 다릅니다.");
        if (!IsComplete(state))
            return Fail("Authoritative.final_state_invalid", "경기 종료 정보가 불완전하거나 서로 맞지 않습니다.");
        if (revision < 0)
            return Fail("Authoritative.revision_invalid", "Revision은 0 이상이어야 합니다.");
        if (state.Plays.Any(p => !known.Contains(p.EventId)) || state.Substitutions.Any(s => !known.Contains(s.EventId)))
            return Fail("Authoritative.evidence_without_receipt", "득점 기록 또는 교체 기록이 영수증 없는 사건 ID를 가리킵니다.");
        return Result<AuthoritativeGame>.Success(new AuthoritativeGame(state, rng, lastEventSequence, revision, null));
    }

    /// <summary>
    /// A finished game carries its <c>Final</c> record (same game, outcome and score); a game in progress has neither. <c>Status == Final</c> with
    /// <c>Final == null</c> is never complete, so such a state can neither be restored nor published.
    /// </summary>
    internal static bool IsComplete(GameState state) => state.Status switch
    {
        GameStatus.Final => state.Outcome is { } outcome && state.Final is { } final && final.GameId == state.GameId && final.Outcome == outcome
            && final.AwayRuns == state.AwayRuns && final.HomeRuns == state.HomeRuns && Equals(final.Execution, state.Execution),
        GameStatus.InProgress => state.Outcome is null && state.Final is null,
        _ => false,
    };

    /// <summary>True when the last receipt agrees with <see cref="LastEventSequence"/>; the factories make a failure unreachable (a defence for <see cref="CommitGate"/>).</summary>
    internal bool IsConsistent()
    {
        var receipts = State.AppliedEvents;
        if (LastEventSequence != receipts.Count)
            return false;
        if (receipts.Count == 0)
            return true;
        var ids = GameEventIds.Create(State.GameId);
        return ids.IsSuccess && ids.Value.ParseSequence(receipts[^1].EventId) is { IsSuccess: true } last && last.Value == LastEventSequence;
    }

    private static bool IsReceiptForm(EventReceipt receipt) =>
        !string.IsNullOrEmpty(receipt.Kind)
        && receipt.FingerprintVersion is EventFingerprint.Version or EventFingerprint.VersionV2
        && receipt.Fingerprint is { Length: 64 } fingerprint && fingerprint.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static Result<AuthoritativeGame> Fail(string code, string message) => Result<AuthoritativeGame>.Failure(new Error(code, message));
}
