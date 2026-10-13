using System.Globalization;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// The pure first half of confirming an official event (C3-A, game-engine 27.6 and 27.16 O-2 "alternative C"): from the current <see cref="AuthoritativeGame"/> and one
/// proposed event it computes the next aggregate and the <see cref="CommitRecord"/>, touching nothing shared. A failure or a cancellation changes no state, RNG, id,
/// revision or last commit. C3-A confirms only events that draw no random number (<see cref="ApproachEvent"/>, <see cref="RunnerPlayEvent"/>,
/// <see cref="SubstitutionEvent"/>); a pitch event is refused so that no path confirms a pitch without its RNG (C3-B).
/// </summary>
public static class CommitGate
{
    /// <summary>Stage names seen by the test hook; the cancellation token is checked right after the hook at each of them.</summary>
    internal static class StageName
    {
        public const string Start = "start";
        public const string Validated = "validated";
        public const string Applied = "applied";
        public const string Finalized = "finalized";
    }

    public static Result<StagedCommit> Stage(AuthoritativeGame? expected, GameEvent? gameEvent, CancellationToken cancellation = default) =>
        Stage(expected, gameEvent, null, cancellation);

    /// <summary>
    /// The same, with a hook that sees each stage name before the cancellation token is checked at it (tests cancel at a chosen stage). Finalize is always the real
    /// <see cref="GameReducer.Finalize"/>; <c>Commit.finalize_failed</c> and <c>Commit.final_state_incomplete</c> are defences the real reducer does not reach.
    /// </summary>
    internal static Result<StagedCommit> Stage(AuthoritativeGame? expected, GameEvent? gameEvent, Action<string>? checkpoint, CancellationToken cancellation)
    {
        void At(string name)
        {
            checkpoint?.Invoke(name);
            cancellation.ThrowIfCancellationRequested();
        }

        // 0 cancellation
        At(StageName.Start);

        // 1 the aggregate and the event exist
        if (expected is null)
            return Fail("Commit.authority_missing", "권위 있는 경기 상태가 없습니다.");
        if (gameEvent is null)
            return Fail("Commit.event_missing", "확정할 사건이 없습니다.");

        // 2 the kind is allowed (C3-A: only events without a random draw)
        if (gameEvent is not (ApproachEvent or RunnerPlayEvent or SubstitutionEvent))
            return Fail("Commit.event_kind_not_supported", "이 단계에서는 투구·테이크·인플레이 사건을 확정할 수 없습니다.");

        // 3 the aggregate is consistent
        if (!expected.IsConsistent())
            return Fail("Commit.authority_inconsistent", "경기 상태의 마지막 영수증이 마지막 사건 순번과 맞지 않습니다.");

        // 4 event id form and game
        var gameId = expected.State.GameId;
        var own = GameEventIds.Create(gameId).Value;
        var parsed = own.ParseSequence(gameEvent.Id);
        if (!parsed.IsSuccess)
            return IsCanonicalIdOfOtherGame(gameEvent.Id, gameId)
                ? Fail("Commit.event_game_mismatch", "사건 ID가 다른 경기의 것입니다.")
                : Fail("Commit.event_id_invalid", "사건 ID가 정규 형식({경기 ID}:e{6자리 순번}, 1~999999)이 아닙니다.");
        var sequence = parsed.Value;

        // 5 an identical event already committed
        var receipt = EventFingerprint.Receipt(gameEvent);
        EventReceipt? existing = sequence <= expected.LastEventSequence ? expected.State.AppliedEvents[sequence - 1] : null;
        if (existing is not null && existing == receipt)
            return Fail("Commit.already_committed", "이미 확정된 사건입니다.");

        // 6 the sequence skips a number
        if (sequence > expected.LastEventSequence + 1)
            return Fail("Commit.event_sequence_gap", $"사건 순번이 {expected.LastEventSequence + 1}이어야 하는데 {sequence}입니다.");

        // 7 C3-B insertion point: candidate linkage and the unchanged PitchPreparation.CheckPrepared (not implemented in C3-A)

        // 8 the same id with a different event
        if (existing is not null)
            return Fail("Commit.event_id_in_use", "이 사건 ID는 이미 다른 내용의 사건에 쓰였습니다.");
        At(StageName.Validated);

        // 9 the reducer again on the current authoritative state; its result, not any preview, is what is adopted
        var applied = GameReducer.Apply(expected.State, gameEvent);
        if (!applied.IsSuccess)
            return Result<StagedCommit>.Failure(applied.Error!.Value);
        var step = applied.Value;
        var receipts = step.State.AppliedEvents;
        if (step.WasDuplicate || receipts.Count != expected.State.AppliedEvents.Count + 1 || receipts[^1] != receipt)
            return Fail("Commit.receipt_mismatch", "리듀서가 이 사건의 영수증을 정확히 하나 추가하지 않았습니다.");
        At(StageName.Applied);

        // 10 C3-B insertion point: structural preview comparison and the RNG checks (not implemented in C3-A; the three allowed events draw nothing)

        // 11 a game-ending event is finalized in the same stage
        var next = step.State;
        var facts = step.Facts;
        var finalized = false;
        if (next.Status == GameStatus.Final)
        {
            var done = GameReducer.Finalize(next);
            if (!done.IsSuccess)
                return Fail("Commit.finalize_failed", $"경기 종료 확정에 실패했습니다: {done.Error!.Value.Code}");
            var result = done.Value;
            if (!result.WasNew || result.State.Final is null || !ReferenceEquals(result.State.Final, result.Game) || !AuthoritativeGame.IsComplete(result.State))
                return Fail("Commit.final_state_incomplete", "경기 종료 기록이 새로 만들어지지 않았거나 불완전합니다.");
            next = result.State;
            facts = facts.Add(GameReducer.CompletedFact(result.Game));
            finalized = true;
            At(StageName.Finalized);
        }
        else if (!AuthoritativeGame.IsComplete(next))
            return Fail("Commit.final_state_incomplete", "진행 중인 경기의 상태가 불완전합니다.");

        // 12 revision overflow, the last cancellation check, building the staged commit
        if (expected.Revision == long.MaxValue)
            return Fail("Commit.revision_exhausted", "Revision이 한도에 도달했습니다.");
        cancellation.ThrowIfCancellationRequested();

        var revision = expected.Revision + 1;
        var rngHash = expected.Rng.StateHash;
        var record = new CommitRecord(gameEvent, receipt, sequence, expected.Revision, revision, rngHash, rngHash, facts, finalized);
        var published = new AuthoritativeGame(next, expected.Rng, sequence, revision, record);
        return Result<StagedCommit>.Success(new StagedCommit(expected, published, record));
    }

    /// <summary>True for the canonical shape <c>{game id}:e{six digits, 1-999999}</c> of a game other than <paramref name="gameId"/>.</summary>
    private static bool IsCanonicalIdOfOtherGame(string? eventId, string gameId)
    {
        if (eventId is null)
            return false;
        var cut = eventId.LastIndexOf(":e", StringComparison.Ordinal);
        if (cut <= 0 || eventId.Length - cut - 2 != 6)
            return false;
        var digits = eventId.AsSpan(cut + 2);
        foreach (var c in digits)
            if (!char.IsAsciiDigit(c))
                return false;
        var game = eventId[..cut];
        return int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture) >= 1
            && StreamKeyDerivation.ValidateContext(game) is null
            && !string.Equals(game, gameId, StringComparison.Ordinal);
    }

    private static Result<StagedCommit> Fail(string code, string message) => Result<StagedCommit>.Failure(new Error(code, message));
}
