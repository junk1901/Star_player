using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-C2B-1 (ADR-007, docs/game-engine.md section 22): couples a 3-C2A PitchBranchDraft to the real game state and turns the five NON-in-play branches into a legal,
// reducer-checked but UNCONFIRMED event candidate. Nothing here adopts a state, an RNG position or an event id: committing them together is 3-C3, persistence is Phase 4.
// Resolver-internal like the draft itself: no Application or Infrastructure signature may name these types (a test pins it).

/// <summary>
/// One in-memory preparation of one pitch. It is created only by <see cref="PitchPreparation.Prepare"/>, which really calls <see cref="PitchBranchResolver.Resolve"/> with the
/// batter and pitcher taken from the trusted sheets; nobody can construct one or attach a draft to it. It is not saved or restored: after any state, RNG, rule or tuning change
/// (and after a save/load round trip) it is refused. <see cref="State"/> identity is only a short-lived in-memory check, not persistence integrity.
/// </summary>
public sealed class PreparedPitch
{
    internal PreparedPitch(GameState state, string gameId, string startRngStateHash, PlayerId batterId, PlayerId pitcherId, BattingSide batterDeclaredSide,
        ThrowingHand pitcherThrows, PitchBranchDraft draft, string rulesVersion, string rulesHash, string probabilityModelVersion, string tuningVersion, string tuningHash)
    {
        State = state; GameId = gameId; StartRngStateHash = startRngStateHash; BatterId = batterId; PitcherId = pitcherId; BatterDeclaredSide = batterDeclaredSide;
        PitcherThrows = pitcherThrows; Draft = draft; RulesVersion = rulesVersion; RulesHash = rulesHash; ProbabilityModelVersion = probabilityModelVersion;
        TuningVersion = tuningVersion; TuningHash = tuningHash;
    }

    public GameState State { get; }
    public string GameId { get; }
    public string StartRngStateHash { get; }
    public PlayerId BatterId { get; }
    public PlayerId PitcherId { get; }
    public BattingSide BatterDeclaredSide { get; }
    public ThrowingHand PitcherThrows { get; }
    public PitchBranchDraft Draft { get; }
    public string RulesVersion { get; }
    public string RulesHash { get; }
    public string ProbabilityModelVersion { get; }
    public string TuningVersion { get; }
    public string TuningHash { get; }

    public override string ToString() => $"PreparedPitch({GameId} {Draft.Kind})";
}

/// <summary>
/// A validated, still UNCONFIRMED event candidate. <see cref="UnconfirmedStep"/> is what <see cref="GameReducer.Apply"/> returned for <see cref="Event"/> on the original state: a
/// preview for validation only. Nothing was published; <see cref="NextRng"/> equals the draft's position (no further random number was drawn) and <see cref="EventId"/> was
/// issued from a private working counter, so the caller's id cursor is untouched.
/// </summary>
public sealed class NonInPlayPitchCandidate
{
    internal NonInPlayPitchCandidate(GameEvent gameEvent, GameStep unconfirmedStep, GameRandomSnapshot nextRng, PitchBranchKind kind, int nextBoundedCalls)
    {
        Event = gameEvent; UnconfirmedStep = unconfirmedStep; NextRng = nextRng; Kind = kind; NextBoundedCalls = nextBoundedCalls;
    }

    public GameEvent Event { get; }
    public string EventId => Event.Id;
    public GameStep UnconfirmedStep { get; }
    public GameRandomSnapshot NextRng { get; }
    public string ContractVersion => PitchPreparation.CandidateContractVersion;
    public PitchBranchKind Kind { get; }
    public int NextBoundedCalls { get; }

    public override string ToString() => $"NonInPlayPitchCandidate({EventId} {Kind})";
}

public static class PitchPreparation
{
    public const string CandidateContractVersion = "pitch-candidate-v1";

    /// <summary>
    /// Checks that the two sheets are the CURRENT batter and pitcher of <paramref name="state"/> (ids only: no extra eligibility rule), takes the batter's declared side
    /// (<see cref="Player.Bats"/>) and the pitcher's throwing hand (<see cref="Player.Throws"/>) from them and calls the real <see cref="PitchBranchResolver.Resolve"/>.
    /// Supplying trusted sheets is the caller's responsibility (there is no player store yet). Changes nothing of the caller's.
    /// </summary>
    public static Result<PreparedPitch> Prepare(GameState? state, GameRandomSnapshot? rng, TuningProfile? tuning, PitchIntent intent, bool explicitTake,
        PlayerSheet? batter, PlayerSheet? pitcher, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (state is null)
            return Prep.Fail("PitchPrepare.state_missing", "경기 상태가 없어 투구를 준비할 수 없습니다.");
        if (rng is null)
            return Prep.Fail("PitchPrepare.rng_missing", "난수 스냅샷이 없어 투구를 준비할 수 없습니다.");
        if (tuning is null)
            return Prep.Fail("PitchPrepare.tuning_missing", "튜닝 프로필이 없어 투구를 준비할 수 없습니다.");
        if (state.Status != GameStatus.InProgress)
            return Prep.Fail("PitchBranch.game_not_in_progress", "끝난 경기에서는 투구를 진행할 수 없습니다."); // the resolver's own code; checked first because the current batter is undefined
        if (CheckSheets(state, batter, pitcher, "PitchPrepare") is { } wrong)
            return Result<PreparedPitch>.Failure(wrong);

        var drafted = PitchBranchResolver.Resolve(new PitchBranchRequest(state, tuning, intent, explicitTake, batter!.Player.Bats, pitcher!.Player.Throws, rng), cancellation);
        if (!drafted.IsSuccess)
            return Result<PreparedPitch>.Failure(drafted.Error!.Value);

        var draft = drafted.Value;
        return Result<PreparedPitch>.Success(new PreparedPitch(state, state.GameId, rng.StateHash, batter.Id, pitcher.Id, batter.Player.Bats, pitcher.Player.Throws, draft,
            state.Rules.Version, state.Rules.ContentHash, tuning.Model.Version.Value, tuning.Tuning.Value, tuning.ContentHash));
    }

    /// <summary>
    /// Re-checks that nothing changed since <see cref="Prepare"/> (game, RNG position, current batter/pitcher and their handedness, rules, tuning, probability model, the very
    /// same state object), maps the branch to a <see cref="PitchEvent"/>/<see cref="TakeEvent"/>, issues the id on a PRIVATE counter from the read-only
    /// <paramref name="lastIssuedEventSequence"/>, and lets <see cref="GameReducer.Apply"/> judge it on the original state. No random number is drawn. In play is an explicit
    /// limit of C2B-1. The result is not adopted anywhere.
    /// </summary>
    public static Result<NonInPlayPitchCandidate> BuildNonInPlayCandidate(PreparedPitch? prepared, GameState? state, GameRandomSnapshot? rng, TuningProfile? tuning,
        PlayerSheet? batter, PlayerSheet? pitcher, int lastIssuedEventSequence, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (prepared is null)
            return Cand.Fail("PitchCandidate.prepared_missing", "준비된 투구가 없습니다.");
        if (state is null)
            return Cand.Fail("PitchCandidate.state_missing", "현재 경기 상태가 없습니다.");
        if (rng is null)
            return Cand.Fail("PitchCandidate.rng_missing", "현재 난수 스냅샷이 없습니다.");
        if (tuning is null)
            return Cand.Fail("PitchCandidate.tuning_missing", "현재 튜닝 프로필이 없습니다.");
        if (batter is null)
            return Cand.Fail("PitchCandidate.batter_sheet_missing", "현재 타자의 선수 정보가 없습니다.");
        if (pitcher is null)
            return Cand.Fail("PitchCandidate.pitcher_sheet_missing", "현재 투수의 선수 정보가 없습니다.");
        var draft = prepared.Draft;
        if (draft.ContractVersion != PitchBranchResolver.ContractVersion)
            return Cand.Fail("PitchCandidate.draft_contract_unsupported", $"지원하지 않는 투구 분기 계약 버전입니다: '{draft.ContractVersion}'");

        // 1-2: the same game, and a stream position that belongs to it
        if (!string.Equals(state.GameId, prepared.GameId, StringComparison.Ordinal))
            return Cand.Fail("PitchCandidate.game_mismatch", "준비한 투구의 경기와 현재 경기가 다릅니다.");
        var restored = GameRandom.Restore(rng); // a structurally broken snapshot keeps the existing restore error
        if (!restored.IsSuccess)
            return Result<NonInPlayPitchCandidate>.Failure(restored.Error!.Value);
        if (!string.Equals(restored.Value.ContextKey, state.GameId, StringComparison.Ordinal))
            return Cand.Fail("PitchCandidate.rng_context_mismatch", "난수 스냅샷의 경기 맥락이 현재 경기와 다릅니다.");
        // 3: the stream position is the one the draft started from (any other draw since then makes it stale)
        if (!string.Equals(rng.StateHash, prepared.StartRngStateHash, StringComparison.Ordinal))
            return Cand.Fail("PitchCandidate.rng_state_changed", "준비 이후 난수 위치가 바뀌어 준비한 투구를 쓸 수 없습니다.");

        // 5: the current batter and pitcher (and their handedness) are still the prepared ones
        if (CheckSheets(state, batter, pitcher, "PitchPrepare") is { } wrong)
            return Result<NonInPlayPitchCandidate>.Failure(wrong);
        if (batter.Id != prepared.BatterId)
            return Cand.Fail("PitchCandidate.batter_changed", "준비 이후 타자가 바뀌었습니다.");
        if (pitcher.Id != prepared.PitcherId)
            return Cand.Fail("PitchCandidate.pitcher_changed", "준비 이후 투수가 바뀌었습니다.");
        if (batter.Player.Bats != prepared.BatterDeclaredSide)
            return Cand.Fail("PitchCandidate.batter_side_changed", "준비 이후 타자의 선언 타석 방향이 바뀌었습니다.");
        if (pitcher.Player.Throws != prepared.PitcherThrows)
            return Cand.Fail("PitchCandidate.pitcher_hand_changed", "준비 이후 투수의 투구 손이 바뀌었습니다.");

        // 6: rules, tuning and probability model
        if (state.Rules.Version != prepared.RulesVersion || state.Rules.ContentHash != prepared.RulesHash)
            return Cand.Fail("PitchCandidate.rules_changed", "준비 이후 규칙 버전 또는 해시가 바뀌었습니다.");
        if (tuning.Tuning.Value != prepared.TuningVersion || tuning.ContentHash != prepared.TuningHash)
            return Cand.Fail("PitchCandidate.tuning_changed", "준비 이후 튜닝 버전 또는 해시가 바뀌었습니다.");
        if (tuning.Model.Version.Value != prepared.ProbabilityModelVersion)
            return Cand.Fail("PitchCandidate.probability_model_changed", "준비 이후 확률 모델 버전이 바뀌었습니다.");

        // 1 (last, so the specific reasons above win): count, outs, bases, approach, applied events... the very same state object
        if (!ReferenceEquals(state, prepared.State))
            return Cand.Fail("PitchCandidate.state_stale", "준비 이후 경기 상태가 바뀌었거나 다시 만들어져 준비한 투구를 쓸 수 없습니다.");

        // the limits of this phase
        if (draft.Kind == PitchBranchKind.InPlay)
            return Cand.Fail("PitchCandidate.in_play_not_supported_yet", "인플레이 투구는 아직 이벤트 후보로 만들 수 없습니다(Phase 3-C2B-2/3).");
        if (draft.ExplicitTake && draft.Kind is PitchBranchKind.SwingingStrike or PitchBranchKind.Foul)
            return Cand.Fail("PitchCandidate.take_cannot_swing", "Take로 지켜본 투구는 헛스윙이나 파울이 될 수 없습니다.");

        // 7: the id on a private counter; the caller's cursor is only read
        var ids = GameEventIds.Create(state.GameId, lastIssuedEventSequence);
        if (!ids.IsSuccess)
            return Result<NonInPlayPitchCandidate>.Failure(ids.Error!.Value);
        var id = ids.Value.Next();
        if (!id.IsSuccess)
            return Result<NonInPlayPitchCandidate>.Failure(id.Error!.Value);
        if (state.AppliedEvents.Any(r => r.EventId == id.Value))
            return Cand.Fail("PitchCandidate.event_id_in_use", $"후보 사건 ID '{id.Value}'는 이미 이 경기에 쓰였습니다. 마지막 발급 순번이 오래됐을 수 있습니다.");

        var outcome = draft.Kind switch
        {
            PitchBranchKind.HitByPitch => PitchOutcome.HitByPitch,
            PitchBranchKind.CalledBall => PitchOutcome.Ball,
            PitchBranchKind.CalledStrike => PitchOutcome.CalledStrike,
            PitchBranchKind.SwingingStrike => PitchOutcome.SwingingStrike,
            _ => PitchOutcome.Foul,
        };
        var spec = new PitchSpec(outcome, Location: draft.Location); // no mishap, dropped third strike, runner moves or strikeout credits: the reducer applies forced advances itself
        GameEvent candidate = draft.ExplicitTake ? new TakeEvent(id.Value, spec) : new PitchEvent(id.Value, spec);

        // 8: the reducer judges the candidate on the ORIGINAL state; its result stays an unconfirmed preview
        cancellation.ThrowIfCancellationRequested();
        var applied = GameReducer.Apply(state, candidate);
        if (!applied.IsSuccess)
            return Result<NonInPlayPitchCandidate>.Failure(applied.Error!.Value);
        var step = applied.Value;
        if (step.WasDuplicate)
            return Cand.Fail("PitchCandidate.event_duplicate", "이미 적용된 사건과 같은 내용이라 새 후보로 인정할 수 없습니다.");
        var receipt = step.State.AppliedEvents.Last();
        if (receipt.EventId != id.Value || receipt.FingerprintVersion != EventFingerprint.VersionV2 || GameEventLocation.Of(candidate) is null)
            return Cand.Fail("PitchCandidate.receipt_invalid", "후보의 영수증이 위치를 담은 ef2 형식이 아닙니다.");
        if (PitchesOf(step.State, state) != PitchesOf(state, state) + 1)
            return Cand.Fail("PitchCandidate.pitch_count_invalid", "후보가 실제 투구를 정확히 한 번 세지 않았습니다.");

        cancellation.ThrowIfCancellationRequested();
        return Result<NonInPlayPitchCandidate>.Success(new NonInPlayPitchCandidate(candidate, step, draft.NextRng, draft.Kind, draft.NextBoundedCalls));
    }

    private static int PitchesOf(GameState s, GameState original) =>
        s.Team(original.FieldingSide).PitcherUsages.Where(u => u.Pitcher == original.Fielding.Pitcher).Sum(u => u.PitchCount);

    /// <summary>The two sheets must be the state's current batter and pitcher. Only ids are compared; no further eligibility is invented.</summary>
    private static Error? CheckSheets(GameState state, PlayerSheet? batter, PlayerSheet? pitcher, string prefix)
    {
        if (batter is null)
            return new Error($"{prefix}.batter_sheet_missing", "현재 타자의 선수 정보가 없습니다.");
        if (pitcher is null)
            return new Error($"{prefix}.pitcher_sheet_missing", "현재 투수의 선수 정보가 없습니다.");
        if (batter.Id != state.Batting.CurrentBatterSlot.Player)
            return new Error($"{prefix}.batter_mismatch", "전달된 타자 정보가 현재 타석의 타자와 다릅니다.");
        if (pitcher.Id != state.Fielding.Pitcher)
            return new Error($"{prefix}.pitcher_mismatch", "전달된 투수 정보가 현재 마운드의 투수와 다릅니다.");
        return null;
    }

    private static class Prep
    {
        public static Result<PreparedPitch> Fail(string code, string message) => Result<PreparedPitch>.Failure(new Error(code, message));
    }

    private static class Cand
    {
        public static Result<NonInPlayPitchCandidate> Fail(string code, string message) => Result<NonInPlayPitchCandidate>.Failure(new Error(code, message));
    }
}
