using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
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
        ThrowingHand pitcherThrows, PitchBranchDraft draft, string rulesVersion, string rulesHash, string probabilityModelVersion, string tuningVersion, string tuningHash,
        PitchFieldingInputs? fielding = null, FrozenCatcherInput? catcher = null)
    {
        State = state; GameId = gameId; StartRngStateHash = startRngStateHash; BatterId = batterId; PitcherId = pitcherId; BatterDeclaredSide = batterDeclaredSide;
        PitcherThrows = pitcherThrows; Draft = draft; RulesVersion = rulesVersion; RulesHash = rulesHash; ProbabilityModelVersion = probabilityModelVersion;
        TuningVersion = tuningVersion; TuningHash = tuningHash; Fielding = fielding; Catcher = catcher;
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

    /// <summary>
    /// The immutable fielding inputs this pitch was drawn with (Phase 3-D v2, S1): three fielders, their base fielding ratings and the identity of the player input set they came from, or
    /// <c>null</c> when the pitch was prepared with no player input set (the fielding slot was neutral).
    /// </summary>
    public PitchFieldingInputs? Fielding { get; }

    /// <summary>
    /// The catcher and his base ReceivingFraming frozen for this pitch (Phase 3-E), or <c>null</c> when the model/rules need none (<c>prob-model-v1/v2</c>, or automated calling). It is a record of what was
    /// used, NOT a source of truth: whoever verifies the pitch re-derives the catcher from the real state and the real player input set.
    /// </summary>
    public FrozenCatcherInput? Catcher { get; }

    /// <summary>The identity of the player input set the pitch was prepared with (null = none). Later stages must be given the very same set.</summary>
    public string? PlayerInputsId => Fielding?.InputSetId;

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
    /// <paramref name="batterTendency"/> (Phase 3-D-1) is the batter's hidden zone tendency for the resolver only; null is neutral. The stored source is the world's per-player set, reached through <see cref="PrepareFromProfiles"/>.
    /// The two sheets' current base ratings become the ability modifier inputs (Phase 3-D, <see cref="PitchAbilities"/>); a sheet without the needed hitting or pitching ratings is refused.
    /// <paramref name="playerInputs"/> (Phase 3-D v2, S1) is the game's immutable player input set: the three representative fielders are read from the state, their base fielding ratings from the
    /// set, and both are frozen into the result (<see cref="PreparedPitch.Fielding"/>) together with the set's identity. A game whose execution contract pins an input set REQUIRES it
    /// (and exactly that set); with none the fielding slot is neutral, as before.
    /// </summary>
    public static Result<PreparedPitch> Prepare(GameState? state, GameRandomSnapshot? rng, TuningProfile? tuning, PitchIntent intent, bool explicitTake,
        PlayerSheet? batter, PlayerSheet? pitcher, CancellationToken cancellation = default, BatterZoneTendency? batterTendency = null, PlayerInputSet? playerInputs = null)
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

        var abilities = PitchAbilities.From(batter, pitcher);
        if (!abilities.IsSuccess)
            return Result<PreparedPitch>.Failure(abilities.Error!.Value);

        PitchFieldingInputs? fielding = null;
        if (playerInputs is not null)
        {
            var frozen = PitchFieldingInputs.Build(state, playerInputs, "PitchPrepare");
            if (!frozen.IsSuccess)
                return Result<PreparedPitch>.Failure(frozen.Error!.Value);
            fielding = frozen.Value;
        }
        else if (state.Execution is { PlayerInputsId: not null })
            return Prep.Fail("PitchPrepare.player_inputs_missing", "경기 실행 계약이 선수 입력 집합을 고정했으므로 투구를 준비하려면 그 집합이 필요합니다.");

        // Phase 3-E: the current catcher, when the model's Framing is active and the rules call pitches by hand. Read from the state and the content-version-2 set BEFORE the first random number.
        FrozenCatcherInput? catcher = null;
        if (FrozenCatcherInput.Required(tuning.Model, state.Rules))
        {
            if (playerInputs is null)
                return Prep.Fail("PitchPrepare.catcher_inputs_missing", "수동 판정 경기에서 확률 모델 v3는 포수 Framing을 읽을 선수 입력 집합이 필요합니다.");
            var builtCatcher = FrozenCatcherInput.Build(state, playerInputs, "PitchPrepare");
            if (!builtCatcher.IsSuccess)
                return Result<PreparedPitch>.Failure(builtCatcher.Error!.Value);
            catcher = builtCatcher.Value;
        }

        var drafted = PitchBranchResolver.Resolve(new PitchBranchRequest(state, tuning, intent, explicitTake, batter!.Player.Bats, pitcher!.Player.Throws, rng, batterTendency, abilities.Value, fielding, catcher), cancellation);
        if (!drafted.IsSuccess)
            return Result<PreparedPitch>.Failure(drafted.Error!.Value);

        var draft = drafted.Value;
        return Result<PreparedPitch>.Success(new PreparedPitch(state, state.GameId, rng.StateHash, batter.Id, pitcher.Id, batter.Player.Bats, pitcher.Player.Throws, draft,
            state.Rules.Version, state.Rules.ContentHash, tuning.Model.Version.Value, tuning.Tuning.Value, tuning.ContentHash, fielding, catcher));
    }

    /// <summary>
    /// Phase 3-D (BatterTendency v2, D-T3): <see cref="Prepare"/> with the CURRENT batter's hidden zone tendency looked up in a fixed, immutable <see cref="BatterZoneProfiles"/> set. It is a
    /// separate entry point on purpose: it takes the set and no explicit vector (the two can never be passed together), and it adds no calculation. Order: the state must be in progress and
    /// the two sheets must be the state's current batter and pitcher (the same <c>CheckSheets</c> contract as <see cref="Prepare"/>); the batter's PlayerId is then taken from the
    /// <paramref name="state"/> (never from a name) and looked up; a batter with no entry runs the unchanged no-tendency path; a tendency balanced against another zone model or reference than
    /// <paramref name="tuning"/> is refused up front (otherwise the resolver would fail only on pitches that reach a tendency stage). The result is <see cref="Prepare"/>'s, so a substitute
    /// batter gets his own entry (or none), never the previous batter's. A null set is refused: an EMPTY set is the way to say "nobody has a tendency".
    /// </summary>
    public static Result<PreparedPitch> PrepareFromProfiles(GameState? state, GameRandomSnapshot? rng, TuningProfile? tuning, PitchIntent intent, bool explicitTake,
        PlayerSheet? batter, PlayerSheet? pitcher, BatterZoneProfiles? profiles, CancellationToken cancellation = default, PlayerInputSet? playerInputs = null)
    {
        cancellation.ThrowIfCancellationRequested();
        if (profiles is null)
            return Prep.Fail("PitchPrepare.profiles_missing", "타자 코스 성향 집합이 없습니다(성향이 없으면 빈 집합을 전달하세요).");

        BatterZoneTendency? tendency = null;
        if (state is not null && state.Status == GameStatus.InProgress && CheckSheets(state, batter, pitcher, "PitchPrepare") is null)
            tendency = profiles.Find(state.Batting.CurrentBatterSlot.Player);
        if (tendency is not null && rng is not null && tuning is not null && (tendency.ReferenceId != tuning.Reference.Id || tendency.Model != tuning.ZoneModel))
            return Prep.Fail("PitchPrepare.tendency_reference_mismatch", "타자 코스 성향의 구역 모델 또는 기준 분포가 이 튜닝과 다릅니다.");
        return Prepare(state, rng, tuning, intent, explicitTake, batter, pitcher, cancellation, tendency, playerInputs); // every other error is Prepare's own
    }

    /// <summary>
    /// Re-checks that nothing changed since <see cref="Prepare"/> (game, RNG position, current batter/pitcher and their handedness, rules, tuning, probability model, the very
    /// same state object), maps the branch to a <see cref="PitchEvent"/>/<see cref="TakeEvent"/>, issues the id on a PRIVATE counter from the read-only
    /// <paramref name="lastIssuedEventSequence"/>, and lets <see cref="GameReducer.Apply"/> judge it on the original state. No random number is drawn. In play is an explicit
    /// limit of C2B-1. The result is not adopted anywhere.
    /// </summary>
    public static Result<NonInPlayPitchCandidate> BuildNonInPlayCandidate(PreparedPitch? prepared, GameState? state, GameRandomSnapshot? rng, TuningProfile? tuning,
        PlayerSheet? batter, PlayerSheet? pitcher, int lastIssuedEventSequence, CancellationToken cancellation = default, PlayerInputSet? playerInputs = null)
    {
        cancellation.ThrowIfCancellationRequested();
        if (CheckPrepared(prepared, state, rng, tuning, batter, pitcher, playerInputs) is { } stale)
            return Result<NonInPlayPitchCandidate>.Failure(stale);
        var draft = prepared!.Draft;                   // CheckPrepared proved that prepared and state exist (the null guard below only informs the compiler)
        ArgumentNullException.ThrowIfNull(state);

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

    /// <summary>
    /// The shared "is this preparation still the one the caller is looking at" contract of 3-C2B-1, in its original order: the inputs exist, the draft contract is supported, the same
    /// game, a snapshot that belongs to it and sits where the draft started, the same current batter/pitcher and handedness, the same rules / tuning / probability model, then (Phase 3-D v2,
    /// step 6b) the game's pinned tuning and the player inputs - the set identity and every frozen fielder - and last of all the very same state object. Returns the first problem or null.
    /// Used by the non-in-play builder, the in-play planner, the advancement resolver and the commit evidence. It runs BEFORE the compare-and-swap; nothing is checked after it.
    /// </summary>
    internal static Error? CheckPrepared(PreparedPitch? prepared, GameState? state, GameRandomSnapshot? rng, TuningProfile? tuning, PlayerSheet? batter, PlayerSheet? pitcher,
        PlayerInputSet? playerInputs = null)
    {
        if (prepared is null)
            return Fail("PitchCandidate.prepared_missing", "준비된 투구가 없습니다.");
        if (state is null)
            return Fail("PitchCandidate.state_missing", "현재 경기 상태가 없습니다.");
        if (rng is null)
            return Fail("PitchCandidate.rng_missing", "현재 난수 스냅샷이 없습니다.");
        if (tuning is null)
            return Fail("PitchCandidate.tuning_missing", "현재 튜닝 프로필이 없습니다.");
        if (batter is null)
            return Fail("PitchCandidate.batter_sheet_missing", "현재 타자의 선수 정보가 없습니다.");
        if (pitcher is null)
            return Fail("PitchCandidate.pitcher_sheet_missing", "현재 투수의 선수 정보가 없습니다.");
        var draft = prepared.Draft;
        if (draft.ContractVersion != PitchBranchResolver.ContractVersion)
            return Fail("PitchCandidate.draft_contract_unsupported", $"지원하지 않는 투구 분기 계약 버전입니다: '{draft.ContractVersion}'");

        // 1-2: the same game, and a stream position that belongs to it
        if (!string.Equals(state.GameId, prepared.GameId, StringComparison.Ordinal))
            return Fail("PitchCandidate.game_mismatch", "준비한 투구의 경기와 현재 경기가 다릅니다.");
        var restored = GameRandom.Restore(rng); // a structurally broken snapshot keeps the existing restore error
        if (!restored.IsSuccess)
            return restored.Error!.Value;
        if (!string.Equals(restored.Value.ContextKey, state.GameId, StringComparison.Ordinal))
            return Fail("PitchCandidate.rng_context_mismatch", "난수 스냅샷의 경기 맥락이 현재 경기와 다릅니다.");
        // 3: the stream position is the one the draft started from (any other draw since then makes it stale)
        if (!string.Equals(rng.StateHash, prepared.StartRngStateHash, StringComparison.Ordinal))
            return Fail("PitchCandidate.rng_state_changed", "준비 이후 난수 위치가 바뀌어 준비한 투구를 쓸 수 없습니다.");

        // 5: the current batter and pitcher (and their handedness) are still the prepared ones
        if (CheckSheets(state, batter, pitcher, "PitchPrepare") is { } wrong)
            return wrong;
        if (batter.Id != prepared.BatterId)
            return Fail("PitchCandidate.batter_changed", "준비 이후 타자가 바뀌었습니다.");
        if (pitcher.Id != prepared.PitcherId)
            return Fail("PitchCandidate.pitcher_changed", "준비 이후 투수가 바뀌었습니다.");
        if (batter.Player.Bats != prepared.BatterDeclaredSide)
            return Fail("PitchCandidate.batter_side_changed", "준비 이후 타자의 선언 타석 방향이 바뀌었습니다.");
        if (pitcher.Player.Throws != prepared.PitcherThrows)
            return Fail("PitchCandidate.pitcher_hand_changed", "준비 이후 투수의 투구 손이 바뀌었습니다.");

        // 6: rules, tuning and probability model
        if (state.Rules.Version != prepared.RulesVersion || state.Rules.ContentHash != prepared.RulesHash)
            return Fail("PitchCandidate.rules_changed", "준비 이후 규칙 버전 또는 해시가 바뀌었습니다.");
        if (tuning.Tuning.Value != prepared.TuningVersion || tuning.ContentHash != prepared.TuningHash)
            return Fail("PitchCandidate.tuning_changed", "준비 이후 튜닝 버전 또는 해시가 바뀌었습니다.");
        if (tuning.Model.Version.Value != prepared.ProbabilityModelVersion)
            return Fail("PitchCandidate.probability_model_changed", "준비 이후 확률 모델 버전이 바뀌었습니다.");

        // 6b (Phase 3-D v2, S1): the player inputs. The set the caller holds NOW must be the very set the pitch was prepared with, and the frozen fielders (position, player, base rating) must
        // still be the current defenders and ratings. PlayerId alone is never enough: the same id with another rating is another input. A defensive substitution or a changed rating is
        // reported here with its own reason, before the generic stale-state reason below.
        if (state.Execution is { } execution && (tuning.Model.Version.Value != execution.ProbabilityModelVersion || tuning.Tuning.Value != execution.TuningVersion || tuning.ContentHash != execution.TuningHash))
            return Fail("PitchCandidate.execution_mismatch", "경기가 고정한 확률 모델·튜닝 버전 또는 해시와 현재 튜닝이 다릅니다.");
        if (prepared.PlayerInputsId != playerInputs?.Id)
            return Fail("PitchCandidate.player_inputs_changed", "준비 이후 선수 입력 집합이 달라졌거나, 준비할 때와 다른 집합(또는 없음)이 전달되었습니다.");
        if (prepared.Fielding is { } frozen)
        {
            var current = PitchFieldingInputs.Build(state, playerInputs!, "PitchCandidate");
            if (!current.IsSuccess)
                return current.Error!.Value;
            for (var i = 0; i < frozen.Entries.Length; i++)
            {
                var then = frozen.Entries[i];
                var now = current.Value.Entries[i];
                if (then.Player != now.Player)
                    return Fail("PitchCandidate.fielder_changed", $"준비 이후 {then.Position} 수비수가 {then.Player}에서 {now.Player}(으)로 바뀌었습니다.");
                if (then.Fielding != now.Fielding)
                    return Fail("PitchCandidate.fielding_changed", $"준비 이후 {then.Position} 수비수 {then.Player}의 기본 수비 능력치가 바뀌었습니다.");
            }
        }

        // 6c (Phase 3-E): the catcher input. Whether one is needed is decided by the CURRENT model and rules, never by what the preparation says; when needed, the catcher and his rating are re-derived from
        // the real state and the real (content-version-2) set held now and compared as values. A catcher change, the same catcher with another rating, another game's input or another set are each reported here.
        var catcherRequired = FrozenCatcherInput.Required(tuning.Model, state.Rules);
        if (catcherRequired != (prepared.Catcher is not null))
            return Fail("PitchCandidate.catcher_input_mismatch", catcherRequired ? "수동 판정 경기의 준비된 투구에 포수 입력이 없습니다." : "포수 입력이 필요 없는 준비된 투구에 포수 입력이 붙어 있습니다.");
        if (prepared.Catcher is { } frozenCatcher)
        {
            if (!string.Equals(frozenCatcher.GameId, state.GameId, StringComparison.Ordinal) || !string.Equals(frozenCatcher.GameId, prepared.GameId, StringComparison.Ordinal))
                return Fail("PitchCandidate.catcher_game_mismatch", "준비된 포수 입력이 이 경기의 것이 아닙니다.");
            if (playerInputs is null || frozenCatcher.InputSetId != playerInputs.Id)
                return Fail("PitchCandidate.player_inputs_changed", "준비한 포수 입력의 선수 입력 집합이 지금 전달된 집합과 다릅니다.");
            var currentCatcher = FrozenCatcherInput.Build(state, playerInputs, "PitchCandidate");
            if (!currentCatcher.IsSuccess)
                return currentCatcher.Error!.Value;
            if (frozenCatcher.Catcher != currentCatcher.Value.Catcher)
                return Fail("PitchCandidate.catcher_changed", $"준비 이후 포수가 {frozenCatcher.Catcher}에서 {currentCatcher.Value.Catcher}(으)로 바뀌었습니다.");
            if (frozenCatcher.ReceivingFraming != currentCatcher.Value.ReceivingFraming)
                return Fail("PitchCandidate.framing_changed", $"준비 이후 포수 {frozenCatcher.Catcher}의 기본 Framing 능력치가 바뀌었습니다.");
        }

        // 1 (last, so the specific reasons above win): count, outs, bases, approach, applied events... the very same state object
        if (!ReferenceEquals(state, prepared.State))
            return Fail("PitchCandidate.state_stale", "준비 이후 경기 상태가 바뀌었거나 다시 만들어져 준비한 투구를 쓸 수 없습니다.");
        return null;
    }

    private static Error Fail(string code, string message) => new(code, message);

    internal static int PitchesOf(GameState s, GameState original) =>
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
