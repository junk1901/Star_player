using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// The internal, immutable evidence that lets the common <see cref="CommitGate"/> core confirm ONE pitch candidate (C3-B, game-engine 27.18 B-04). <see cref="PitchCommitGate"/>
/// makes it, once per Stage call, from exactly the inputs of that call; it is never returned, stored in a <see cref="StagedCommit"/> or <see cref="CommitRecord"/>, cached or
/// reused. It is not a permission token: it holds the base aggregate, the target event, the prepared pitch with its candidate and the RNG position the candidate would adopt,
/// and it performs the checks of O-2 steps 7a, 7b, 9 (the pitch part), 10a and 10b ITSELF when the core asks, always against the core's own inputs. It carries no callback
/// and no flag a caller could set, so an event cannot become confirmable merely because evidence exists; the core still refuses evidence whose binding differs from its inputs.
/// Constructing it runs no verification (the core's checks 0 to 6 come first). Everything it reads is snapshots and value objects: it never draws and never changes the aggregate.
/// </summary>
internal sealed class CommitEvidence
{
    private const string LinkCode = "Commit.candidate_link_mismatch";
    private const string RngCode = "Commit.rng_evidence_mismatch";

    private readonly AuthoritativeGame? _base;
    private readonly PreparedPitch? _prepared;
    private readonly NonInPlayPitchCandidate? _nonInPlay;
    private readonly InPlayPitchCandidate? _inPlay;
    private readonly TuningProfile? _tuning;
    private readonly PlayerSheet? _batter;
    private readonly PlayerSheet? _pitcher;
    private readonly PlayerInputSet? _inputs;

    private CommitEvidence(AuthoritativeGame? aggregate, PreparedPitch? prepared, NonInPlayPitchCandidate? nonInPlay, InPlayPitchCandidate? inPlay,
        TuningProfile? tuning, PlayerSheet? batter, PlayerSheet? pitcher, PlayerInputSet? inputs)
    {
        _inputs = inputs;
        _base = aggregate;
        _prepared = prepared;
        _nonInPlay = nonInPlay;
        _inPlay = inPlay;
        _tuning = tuning;
        _batter = batter;
        _pitcher = pitcher;
    }

    /// <summary>Evidence for a non-in-play candidate (pitch or explicit take) and the <see cref="PreparedPitch"/> it was built from.</summary>
    internal static CommitEvidence ForNonInPlay(AuthoritativeGame? aggregate, PreparedPitch? prepared, NonInPlayPitchCandidate? candidate, TuningProfile? tuning,
        PlayerSheet? batter, PlayerSheet? pitcher, PlayerInputSet? inputs = null) => new(aggregate, prepared, candidate, null, tuning, batter, pitcher, inputs);

    /// <summary>Evidence for an in-play candidate (base or resolved); its source is <c>candidate.Plan.Prepared</c>.</summary>
    internal static CommitEvidence ForInPlay(AuthoritativeGame? aggregate, InPlayPitchCandidate? candidate, TuningProfile? tuning, PlayerSheet? batter, PlayerSheet? pitcher,
        PlayerInputSet? inputs = null) =>
        new(aggregate, candidate?.Plan?.Prepared, null, candidate, tuning, batter, pitcher, inputs);

    /// <summary>True when the entry received no candidate at all (O-2 step 1).</summary>
    internal bool CandidateMissing => _nonInPlay is null && _inPlay is null;

    /// <summary>The event of the candidate: the only event this evidence can be used for.</summary>
    internal GameEvent? Event => (GameEvent?)_nonInPlay?.Event ?? _inPlay?.Event;

    /// <summary>The RNG position the confirmed pitch adopts (H1, or H2 for a resolved in-play candidate): the candidate's own snapshot, never a new draw.</summary>
    internal GameRandomSnapshot? Final => _nonInPlay?.NextRng ?? _inPlay?.NextRng;

    private GameStep? Preview => _nonInPlay?.UnconfirmedStep ?? _inPlay?.UnconfirmedStep;

    /// <summary>O-2 step 2 for the evidence path: a non-in-play candidate carries a pitch or take, an in-play one an in-play event; nothing else is confirmable through it.</summary>
    internal bool Covers(GameEvent gameEvent) => _nonInPlay is not null ? gameEvent is PitchEvent or TakeEvent : _inPlay is not null && gameEvent is InPlayEvent;

    /// <summary>O-2 step 7a: the unchanged <see cref="PitchPreparation.CheckPrepared"/> against the core's aggregate (its codes and order).</summary>
    internal Error? CheckPrepared(AuthoritativeGame expected) =>
        PitchPreparation.CheckPrepared(_prepared, expected.State, expected.Rng, _tuning, _batter, _pitcher, _inputs);

    /// <summary>
    /// O-2 step 7b (B-01): the binding of the evidence to the core's inputs, then the link of the candidate to its source. Returns <c>Commit.candidate_link_mismatch</c> or null.
    /// Nothing is re-drawn: the event is rebuilt from recorded facts with the existing deterministic builders and compared as a receipt.
    /// </summary>
    internal Error? CheckLink(AuthoritativeGame expected, GameEvent gameEvent)
    {
        if (!ReferenceEquals(_base, expected))
            return Link("증거가 이 확정의 경기 상태가 아닌 다른 상태에 결속되어 있습니다.");
        if (!ReferenceEquals(Event, gameEvent))
            return Link("증거가 이 확정의 사건이 아닌 다른 사건에 결속되어 있습니다.");
        return _nonInPlay is { } nonInPlay ? LinkNonInPlay(expected, nonInPlay) : LinkInPlay(expected, _inPlay!);
    }

    private Error? LinkNonInPlay(AuthoritativeGame expected, NonInPlayPitchCandidate candidate)
    {
        var draft = _prepared!.Draft;                                  // CheckPrepared proved the preparation exists
        if (draft.Kind == PitchBranchKind.InPlay)
            return Link("인플레이 투구는 비인플레이 후보로 확정할 수 없습니다.");
        if (candidate.Kind != draft.Kind)
            return Link("후보의 분기 종류가 준비한 투구의 분기와 다릅니다.");
        if (candidate.NextBoundedCalls != draft.NextBoundedCalls)
            return Link("후보의 난수 호출 수가 준비한 투구와 다릅니다.");
        if (draft.ExplicitTake && draft.Kind is PitchBranchKind.SwingingStrike or PitchBranchKind.Foul)
            return Link("Take로 지켜본 투구는 헛스윙이나 파울일 수 없습니다.");
        if (!ReferenceEquals(candidate.NextRng, draft.NextRng))
            return Link("후보의 난수 위치가 준비한 투구의 난수 위치(H1)와 같은 인스턴스가 아닙니다.");
        if (GameEventLocation.Of(candidate.Event) is null)
            return Link("후보 사건에 투구 위치가 없습니다.");
        if (LinkActualLocation(expected, draft) is { } actualLocation)
            return actualLocation;
        if (LinkCalledPitch(expected, draft) is { } calledPitch)
            return calledPitch;

        // the event as the builder makes it from the recorded draft (no draw): the same type, outcome, location and every other field, compared as a receipt
        var outcome = draft.Kind switch
        {
            PitchBranchKind.HitByPitch => PitchOutcome.HitByPitch,
            PitchBranchKind.CalledBall => PitchOutcome.Ball,
            PitchBranchKind.CalledStrike => PitchOutcome.CalledStrike,
            PitchBranchKind.SwingingStrike => PitchOutcome.SwingingStrike,
            _ => PitchOutcome.Foul,
        };
        var spec = new PitchSpec(outcome, Location: draft.Location);
        GameEvent rebuilt = draft.ExplicitTake ? new TakeEvent(candidate.Event.Id, spec) : new PitchEvent(candidate.Event.Id, spec);
        return EventFingerprint.Receipt(rebuilt) == EventFingerprint.Receipt(candidate.Event)
            ? null
            : Link("후보 사건이 준비한 투구의 종류·결과·위치로 만든 사건과 다릅니다(Take와 Pitch의 뒤바뀜 포함).");
    }

    /// <summary>
    /// Game Calling v4 (GC-B, GC-S1 and GC-S2): the <c>target_zone</c> and <c>actual_location</c> part of the recorded draft against a pure recomputation from the REAL inputs, and the authenticity of every recorded roll.
    /// <para>
    /// <b>Inputs (GC-S1).</b> The pitcher on the mound is read from the aggregate's actual state and his Control from the player input set the GAME pinned (<see cref="GameExecution.PlayerInputsId"/>, content version 3); the
    /// catcher and his GameCalling are read the same way. The pitcher, catcher, Control and GameCalling written in the prepared pitch, in the recorded steps or in the trace are never used as the expectation.
    /// The aimed zone is not read from the record either: it is the outcome of the replayed <c>target_zone</c> draw.
    /// </para>
    /// <para>
    /// <b>Replay (GC-S2).</b> A <see cref="PitchRollReplay"/> is restored from H0 (the aggregate's snapshot, which <see cref="CheckPrepared"/> proved is the position the pitch started from) and the draft's draws are
    /// replayed on its private copy with the existing stream, bound and rejection sampling (<c>NextBounded(1,000,000)</c> on the stream each step names, in step order). The copy shares nothing mutable with the aggregate, the
    /// cursor or the candidate; running it is a verification, not a game draw, and it is never published. For the two location stages the expected condition, distribution, modifier entries, roll, raw draws and
    /// outcome are recomputed from the trusted inputs; for every later step the roll and raw draws are replayed; and the copy must end exactly at the draft's H1. Re-evaluating a recorded roll against a
    /// table is NOT enough: a roll changed inside the same outcome interval keeps the outcome and the H1 hash but is not what the generator produced, and only this replay sees it.
    /// </para>
    /// Applies only when the model pins Control in the input set (<c>prob-model-v4</c>); earlier models keep their previous verification unchanged. Returns a link problem or null.
    /// </summary>
    private Error? LinkActualLocation(AuthoritativeGame expected, PitchBranchDraft draft)
    {
        var tuning = _tuning!;                                          // CheckPrepared proved the tuning is the prepared one
        if (!FrozenPitcherInput.Required(tuning.Model))
            return null;
        var state = expected.State;
        if (_inputs is null)
            return Link("Game Calling 모델의 투구를 검증할 선수 입력 집합이 없습니다.");
        if (state.Execution is not { PlayerInputsId: { } pinned } || pinned != _inputs.Id)
            return Link("검증에 전달된 선수 입력 집합이 경기가 고정한 집합이 아닙니다.");
        var pitcher = FrozenPitcherInput.Build(state, _inputs, "Commit");
        if (!pitcher.IsSuccess)
            return Link($"실제 상태와 선수 입력 집합에서 투수 Control을 다시 읽을 수 없습니다: {pitcher.Error!.Value.Code}");
        var catcher = FrozenCatcherInput.Build(state, _inputs, tuning.Model, "Commit");
        if (!catcher.IsSuccess)
            return Link($"실제 상태와 선수 입력 집합에서 포수 Game Calling을 다시 읽을 수 없습니다: {catcher.Error!.Value.Code}");

        // F-01 (Codex final review): the structure is fixed by the branch BEFORE any recorded step is replayed; the recorded list is never the source of what is allowed
        if (StructureProblem(draft) is { } structure)
            return Link(structure);
        var steps = draft.Steps;
        var contract = StageContract(draft.Kind);

        // H0 -> a private replay (a copy restored from the immutable snapshot; see PitchRollReplay)
        var started = PitchRollReplay.From(expected.Rng);
        if (!started.IsSuccess)
            return started.Error!.Value;
        var replay = started.Value;

        // the stage-by-stage expectation; each tuple is (expected stream, expected condition, the recomputed result)
        var intent = draft.Intent switch { PitchIntent.Balanced => "balanced", PitchIntent.Attack => "attack", PitchIntent.Careful => "careful", _ => null };
        if (intent is null)
            return Link("투구 분기 기록의 투수 의도 값이 올바르지 않습니다.");
        var targetRow = StageProbability.Compute(tuning, StageIds.TargetZone, intent);
        if (!targetRow.IsSuccess)
            return Link($"target_zone 확률을 다시 계산할 수 없습니다: {targetRow.Error!.Value.Code}");
        if (TraceProblem(StageIds.TargetZone, GameRngStreams.Target, intent, targetRow.Value, steps[0]) is { } targetProblem)
            return Link(targetProblem);
        var (targetRoll, targetRaw) = replay.Next(GameRngStreams.Target);
        if (targetRoll != steps[0].Roll || targetRaw != steps[0].RawDraws)
            return Rng($"target_zone의 기록된 주사위 값·원시 소비({steps[0].Roll}/{steps[0].RawDraws})가 H0에서 다시 재생한 값({targetRoll}/{targetRaw})과 다릅니다.");
        var targetOutcome = targetRow.Value.Distribution.Select(targetRoll);
        if (!targetOutcome.IsSuccess || targetOutcome.Value != steps[0].Outcome)
            return Link("target_zone의 기록된 결과가 재생한 주사위 값에서 나오는 결과와 다릅니다.");
        var aimed = PitchZoneId.Create(targetOutcome.Value);
        if (!aimed.IsSuccess)
            return Link("재생한 목표 구역이 올바른 구역이 아닙니다.");

        // the location row from the REAL pitcher Control and catcher GameCalling, for the REPLAYED aimed zone
        var inputs = FrozenCatcherInput.Extend(PitchModifierInputs.ForActualLocation(pitcher.Value.Control), StageIds.ActualLocation, catcher.Value);
        var locationRow = StageProbability.Compute(tuning, StageIds.ActualLocation, ConditionKeys.Zone(aimed.Value), inputs);
        if (!locationRow.IsSuccess)
            return Link($"actual_location 확률을 실제 투수·포수 입력으로 다시 계산할 수 없습니다: {locationRow.Error!.Value.Code}");
        if (TraceProblem(StageIds.ActualLocation, GameRngStreams.Location, ConditionKeys.Zone(aimed.Value), locationRow.Value, steps[1]) is { } locationProblem)
            return Link(locationProblem);
        var (locationRoll, locationRaw) = replay.Next(GameRngStreams.Location);
        if (locationRoll != steps[1].Roll || locationRaw != steps[1].RawDraws)
            return Rng($"actual_location의 기록된 주사위 값·원시 소비({steps[1].Roll}/{steps[1].RawDraws})가 H0에서 다시 재생한 값({locationRoll}/{locationRaw})과 다릅니다.");
        var landed = locationRow.Value.Distribution.Select(locationRoll);
        if (!landed.IsSuccess || landed.Value != steps[1].Outcome)
            return Link("actual_location의 기록된 결과가 재생한 주사위 값에서 나오는 결과와 다릅니다.");

        // the recorded target, location and boundary are exactly what the replay produced
        var target = TargetZone.Create(tuning.ZoneModel, aimed.Value);
        var landedZone = PitchZoneId.Create(landed.Value);
        if (!target.IsSuccess || !landedZone.IsSuccess)
            return Link("재생한 구역으로 투구 위치를 만들 수 없습니다.");
        var location = PitchLocation.FromZone(tuning.ZoneModel, landedZone.Value, draft.Location.Side);
        if (!location.IsSuccess || location.Value != draft.Location)
            return Link("기록된 투구 위치(구역·타석 방향·경계 분류)가 재생한 실제 위치와 다릅니다.");
        if (target.Value != draft.Target)
            return Link("기록된 목표 구역이 재생한 목표 구역과 다릅니다.");

        // every later step: the roll and the raw draws are what the same stream produces next; then the copy must stand exactly where the draft says H1 is
        for (var i = 2; i < steps.Length; i++)
        {
            var (roll, raw) = replay.Next(contract[i].Stream);           // the stream the contract names, equal to the recorded one (StructureProblem)
            if (roll != steps[i].Roll || raw != steps[i].RawDraws)
                return Rng($"{i + 1}번째 단계({steps[i].Stage})의 기록된 주사위 값·원시 소비({steps[i].Roll}/{steps[i].RawDraws})가 H0에서 다시 재생한 값({roll}/{raw})과 다릅니다.");
        }

        return replay.StateHash == draft.NextRng.StateHash
            ? null
            : Rng("H0에서 기록된 단계들을 재생한 난수 위치가 투구 뒤 난수(H1)와 다릅니다.");
    }

    // the two batter_decision outcomes the resolver branches on (PitchBranchResolver keeps them private); an honest hit-by-pitch or called pitch fails the contract below if either is wrong
    private const string HitByPitchOutcome = "hit_by_pitch";
    private const string TakeOutcome = "take";

    /// <summary>
    /// The stages a pitch branch may contain, in order, each with its fixed stream: exactly what <see cref="PitchBranchResolver"/> draws. Hit by pitch = target, location, batter_decision; a called ball or
    /// strike adds called_pitch; a swinging strike or foul adds swing_result; in play adds swing_result, batted_ball and defense_result. No other stage or stream belongs to a pitch.
    /// </summary>
    private static (string Stage, RngStreamId Stream)[] StageContract(PitchBranchKind kind)
    {
        (string, RngStreamId)[] head = [(StageIds.TargetZone, GameRngStreams.Target), (StageIds.ActualLocation, GameRngStreams.Location), (StageIds.BatterDecision, GameRngStreams.Pitch)];
        return kind switch
        {
            PitchBranchKind.HitByPitch => head,
            PitchBranchKind.CalledBall or PitchBranchKind.CalledStrike => [.. head, (StageIds.CalledPitch, GameRngStreams.Pitch)],
            PitchBranchKind.SwingingStrike or PitchBranchKind.Foul => [.. head, (StageIds.SwingResult, GameRngStreams.Contact)],
            _ => [.. head, (StageIds.SwingResult, GameRngStreams.Contact), (StageIds.BattedBall, GameRngStreams.Contact), (StageIds.DefenseResult, GameRngStreams.Field)],
        };
    }

    /// <summary>
    /// The recorded steps against the structure the branch allows: the same count, the same stages in the same order, the same stream per stage. Whether the branch label itself matches what the steps decided is
    /// checked from their outcomes (the batter's decision and the swing result choose the branch). Returns the problem text or null.
    /// </summary>
    private static string? StructureProblem(PitchBranchDraft draft)
    {
        var steps = draft.Steps;
        if (steps.IsDefault)
            return "투구 분기 기록에 단계 목록이 없습니다.";
        var contract = StageContract(draft.Kind);
        if (steps.Length != contract.Length)
            return $"{draft.Kind} 투구의 단계 수는 {contract.Length}개여야 하는데 {steps.Length}개가 기록되어 있습니다(허용되지 않은 추가·누락 단계).";
        for (var i = 0; i < contract.Length; i++)
            if (steps[i].Stage != contract[i].Stage || steps[i].Stream != contract[i].Stream)
                return $"{draft.Kind} 투구의 {i + 1}번째 단계는 {contract[i].Stage} / {contract[i].Stream.Canonical}이어야 하는데 {steps[i].Stage} / {steps[i].Stream.Canonical}이 기록되어 있습니다.";

        var decision = steps[2].Outcome;
        var consistent = draft.Kind switch
        {
            PitchBranchKind.HitByPitch => decision == HitByPitchOutcome,
            PitchBranchKind.CalledBall or PitchBranchKind.CalledStrike => decision != HitByPitchOutcome && (decision == TakeOutcome || draft.ExplicitTake),
            _ => decision != HitByPitchOutcome && decision != TakeOutcome && !draft.ExplicitTake
                && draft.Kind == (steps[3].Outcome switch { "miss" => PitchBranchKind.SwingingStrike, "foul" => PitchBranchKind.Foul, _ => PitchBranchKind.InPlay }),
        };
        return consistent ? null : $"투구 분기 종류 {draft.Kind}가 기록된 타자 결정 '{decision}'·스윙 결과의 의미와 맞지 않습니다.";
    }

    /// <summary>
    /// A recorded step against the recomputed row of its stage: the stage, stream and condition, the final distribution, the trace (baseline, combined factors and cap flags, versions, hash) and the modifier entries (slot,
    /// input, factor, applied, targets). Returns the problem text or null. The roll and the outcome are compared by the caller, against the replay.
    /// </summary>
    private static string? TraceProblem(string stage, RngStreamId stream, string condition, StageResult row, PitchBranchStep recorded)
    {
        if (recorded.Stage != stage || recorded.Stream != stream)
            return $"기록된 {recorded.Stage} 단계의 스트림 {recorded.Stream.Canonical}이 기대한 {stage} / {stream.Canonical}과 다릅니다.";
        if (recorded.Condition != condition || recorded.Trace.Condition != condition)
            return $"기록된 {stage} 조건 '{recorded.Condition}'이 실제 입력에서 나오는 '{condition}'과 다릅니다.";
        var (expected, trace) = (row.Distribution, row.Trace);
        if (!expected.Ppm.SequenceEqual(recorded.Trace.Final))
            return $"기록된 {stage} 확률 분포가 실제 투수·포수·규칙 입력으로 다시 계산한 값과 다릅니다.";
        if (!trace.Baseline.SequenceEqual(recorded.Trace.Baseline) || !trace.CombinedFactorPpm.SequenceEqual(recorded.Trace.CombinedFactorPpm) || !trace.CombinedCapped.SequenceEqual(recorded.Trace.CombinedCapped)
            || !trace.AfterModifiers.SequenceEqual(recorded.Trace.AfterModifiers) || trace.TuningHash != recorded.Trace.TuningHash || trace.ModelVersion != recorded.Trace.ModelVersion
            || trace.TuningVersion != recorded.Trace.TuningVersion || !trace.Outcomes.SequenceEqual(recorded.Trace.Outcomes))
            return $"기록된 {stage} 계산 흔적(기준 확률·결합 계수·버전)이 실제 입력으로 다시 계산한 값과 다릅니다.";
        if (trace.Modifiers.Count != recorded.Trace.Modifiers.Count
            || trace.Modifiers.Zip(recorded.Trace.Modifiers).Any(p => p.First.Slot != p.Second.Slot || p.First.Input != p.Second.Input || p.First.FactorPpm != p.Second.FactorPpm
                                                                    || p.First.Applied != p.Second.Applied || !p.First.Targets.SequenceEqual(p.Second.Targets)))
            return $"기록된 {stage} 보정 항목(투수 Control·포수 Game Calling 입력, 적용 여부, 계수)이 실제 입력으로 다시 계산한 값과 다릅니다.";
        return null;
    }

    /// <summary>
    /// Phase 3-E (Package B, F-01 principle): the <c>called_pitch</c> part of the recorded draft against a pure recomputation from the REAL inputs. The expected condition key is built from the aggregate's
    /// actual rules mode and the pitch location; the expected Framing input is re-derived from the aggregate's actual state (the current catcher) and the player input set held now - the catcher id, the rating
    /// and the applied flag written in the prepared pitch or in the recorded steps are never used as the source of the expectation. The row is recomputed with <see cref="StageProbability.Compute"/> (a pure
    /// function: no draw) and the recorded distribution, modifier entries, roll and outcome must equal it. A pitch that has no called pitch (hit by pitch, swing, in play) must have no such step. Returns a
    /// link problem or null.
    /// </summary>
    private Error? LinkCalledPitch(AuthoritativeGame expected, PitchBranchDraft draft)
    {
        var steps = draft.Steps.Where(s => s.Stage == StageIds.CalledPitch).ToArray();
        var isCall = draft.Kind is PitchBranchKind.CalledBall or PitchBranchKind.CalledStrike;
        if (steps.Length != (isCall ? 1 : 0))
            return Link(isCall ? "볼·스트라이크 콜 투구에 called_pitch 단계가 정확히 하나 있지 않습니다." : "콜이 없는 투구에 called_pitch 단계가 기록되어 있습니다.");
        if (!isCall)
            return draft.CalledPitch is null ? null : Link("콜이 없는 투구에 called_pitch 결과가 기록되어 있습니다.");

        var step = steps[0];
        var state = expected.State;
        var tuning = _tuning!;                                         // CheckPrepared proved the tuning is the prepared one
        var zoneStep = draft.Steps.FirstOrDefault(s => s.Stage == StageIds.ActualLocation);
        if (zoneStep is null || zoneStep.Outcome != draft.Location.Zone.Value)
            return Link("기록된 실제 위치 단계의 결과가 투구 위치의 구역과 다릅니다.");
        var key = ConditionKeys.CalledPitch(state.Rules.CalledPitchMode, draft.Location.Zone);
        if (!key.IsSuccess)
            return Link($"실제 규칙의 판정 방식과 투구 위치로 called_pitch 조건을 만들 수 없습니다: {key.Error!.Value.Code}");
        if (step.Condition != key.Value || step.Trace.Condition != key.Value)
            return Link($"기록된 called_pitch 조건 '{step.Condition}'이 실제 규칙 모드와 투구 위치에서 나오는 '{key.Value}'와 다릅니다.");
        if (step.Stream != GameRngStreams.Pitch)
            return Link("called_pitch 추첨 스트림이 game/pitch가 아닙니다.");

        IReadOnlyList<ModifierInput>? inputs = null;
        if (FrozenCatcherInput.Required(tuning.Model, state.Rules))
        {
            if (_inputs is null)
                return Link("수동 판정 경기의 called_pitch를 검증할 선수 입력 집합이 없습니다.");
            var catcher = FrozenCatcherInput.Build(state, _inputs, tuning.Model, "Commit");
            if (!catcher.IsSuccess)
                return Link($"실제 상태와 선수 입력 집합에서 포수 Framing을 다시 읽을 수 없습니다: {catcher.Error!.Value.Code}");
            inputs = FrozenCatcherInput.Extend(null, StageIds.CalledPitch, catcher.Value);
        }

        var computed = StageProbability.Compute(tuning, StageIds.CalledPitch, key.Value, inputs);
        if (!computed.IsSuccess)
            return Link($"called_pitch 확률을 실제 입력으로 다시 계산할 수 없습니다: {computed.Error!.Value.Code}");
        var (distribution, trace) = (computed.Value.Distribution, computed.Value.Trace);
        if (!distribution.Ppm.SequenceEqual(step.Trace.Final))
            return Link("기록된 called_pitch 확률 분포가 실제 포수·규칙 입력으로 다시 계산한 값과 다릅니다.");
        if (!trace.Baseline.SequenceEqual(step.Trace.Baseline) || !trace.CombinedFactorPpm.SequenceEqual(step.Trace.CombinedFactorPpm) || !trace.AfterModifiers.SequenceEqual(step.Trace.AfterModifiers)
            || trace.TuningHash != step.Trace.TuningHash || trace.ModelVersion != step.Trace.ModelVersion || trace.TuningVersion != step.Trace.TuningVersion)
            return Link("기록된 called_pitch 계산 흔적(기준 확률·결합 계수·버전)이 실제 입력으로 다시 계산한 값과 다릅니다.");
        if (trace.Modifiers.Count != step.Trace.Modifiers.Count
            || trace.Modifiers.Zip(step.Trace.Modifiers).Any(p => p.First.Slot != p.Second.Slot || p.First.Input != p.Second.Input || p.First.FactorPpm != p.Second.FactorPpm
                                                                 || p.First.Applied != p.Second.Applied || !p.First.Targets.SequenceEqual(p.Second.Targets)))
            return Link("기록된 called_pitch 보정 항목(포수 Framing 입력·적용 여부·계수)이 실제 입력으로 다시 계산한 값과 다릅니다.");
        if (distribution.SelectIndex(step.Roll) is not { IsSuccess: true } picked || distribution.Outcomes[picked.Value] != step.Outcome)
            return Link("기록된 called_pitch 결과가 기록된 주사위 값과 다시 계산한 확률표에서 나오는 결과와 다릅니다.");
        if (draft.CalledPitch != step.Outcome || draft.Kind != (step.Outcome == "ball" ? PitchBranchKind.CalledBall : PitchBranchKind.CalledStrike))
            return Link("기록된 투구 분기 종류·결과가 called_pitch 단계의 결과와 다릅니다.");
        return null;
    }

    private Error? LinkInPlay(AuthoritativeGame expected, InPlayPitchCandidate candidate)
    {
        var plan = candidate.Plan;
        var prepared = plan.Prepared;                                  // the very object CheckPrepared accepted
        var draft = prepared.Draft;
        var audit = candidate.Audit;
        if (draft.Kind != PitchBranchKind.InPlay)
            return Link("인플레이가 아닌 투구는 인플레이 후보로 확정할 수 없습니다.");
        if (candidate.Event.Location is null)
            return Link("후보 사건에 투구 위치가 없습니다.");
        if (audit.StartRngStateHash != prepared.StartRngStateHash)
            return Link("후보 감사 기록의 시작 난수 해시(H0)가 준비한 투구의 것과 다릅니다.");
        if (LinkActualLocation(expected, draft) is { } actualLocation)
            return actualLocation;
        if (LinkCalledPitch(expected, draft) is { } calledPitch)
            return calledPitch;                                        // an in-play pitch has no called_pitch step: a recorded one is a forgery

        // an advancement decision is open exactly when a resolved detail exists: a plan that needs one but has none is a forged unresolved candidate
        var detail = audit.AdvancementDetail;
        if (plan.AdvancementRequired != (detail is not null))
            return Link(plan.AdvancementRequired ? "추가 진루 결정이 필요한데 해결 기록이 없습니다(미해결 후보)." : "추가 진루가 필요 없는 후보에 해결 기록이 붙어 있습니다.");

        GameEvent rebuilt;
        if (detail is null)
        {
            // base in-play: no second draw, the candidate adopts the draft's own position (H1)
            if (audit.Advancement != "not_required")
                return Link("기본 인플레이 후보의 추가 진루 상태가 not_required가 아닙니다.");
            if (!ReferenceEquals(candidate.NextRng, draft.NextRng))
                return Link("후보의 난수 위치가 준비한 투구의 난수 위치(H1)와 같은 인스턴스가 아닙니다.");
            if (audit.FinalRngStateHash != draft.NextRng.StateHash)
                return Link("후보 감사 기록의 마지막 난수 해시가 H1과 다릅니다.");
            rebuilt = InPlayBasePlanner.BuildEvent(plan.Facts, candidate.Event.Id, draft.Location);
        }
        else
        {
            // resolved in-play: H2 is its own snapshot, so the link rests on the audit
            if (audit.Advancement != "resolved")
                return Link("해결된 인플레이 후보의 추가 진루 상태가 resolved가 아닙니다.");
            if (detail.StartRngStateHash != prepared.StartRngStateHash)
                return Link("추가 진루 기록의 시작 난수 해시가 H0와 다릅니다.");
            if (detail.AfterPitchRngStateHash != draft.NextRng.StateHash)
                return Link("추가 진루 기록의 투구 후 난수 해시가 H1과 다릅니다.");
            if (detail.FinalRngStateHash != candidate.NextRng.StateHash || audit.FinalRngStateHash != candidate.NextRng.StateHash)
                return Link("추가 진루 기록의 마지막 난수 해시가 후보의 난수 위치(H2)와 다릅니다.");
            if (detail.DrawStream != GameRngStreams.Runner.Canonical)
                return Link("추가 진루 추첨 스트림이 game/runner가 아닙니다.");
            if (detail.LogicalDraws != detail.Decisions.Length || detail.LogicalDraws != plan.AdvancementContextKeys.Length || detail.LogicalDraws < 1)
                return Link("추가 진루의 논리 추첨 수가 결정 수 또는 맥락 수와 다릅니다.");
            if (detail.RawDraws != detail.Decisions.Sum(d => d.RawDraws) || detail.Decisions.Any(d => d.RawDraws < 1))
                return Link("추가 진루의 원시 추첨 수가 결정별 원시 추첨의 합과 다릅니다.");
            if (!detail.Decisions.Select(d => d.ContextKey).Order(StringComparer.Ordinal).SequenceEqual(plan.AdvancementContextKeys.Order(StringComparer.Ordinal)))
                return Link("추가 진루 결정의 맥락 키가 기본 계획의 맥락 키와 다릅니다.");
            if (detail.EventResult != candidate.Event.Result || detail.EventBatted != candidate.Event.Batted)
                return Link("추가 진루 기록의 사건 입력이 후보 사건과 다릅니다.");
            if (LinkAbilities(candidate, detail) is { } ability)
                return ability;

            var composed = AdvancementPlanner.Compose(plan.Facts, prepared.State, detail.Decisions.Select(d => d.Drawn).ToArray());
            if (!composed.IsSuccess)
                return Link($"기록된 뽑기 결과로 추가 진루를 다시 합성할 수 없습니다: {composed.Error!.Value.Code}");
            rebuilt = AdvancementEventBuilder.Build(plan.Facts, composed.Value, candidate.Event.Id, draft.Location);
        }

        return EventFingerprint.Receipt(rebuilt) == EventFingerprint.Receipt(candidate.Event)
            ? null
            : Link("후보 사건이 기록된 계획으로 결정적으로 다시 만든 사건과 다릅니다.");
    }

    /// <summary>
    /// Phase 3-D v2 (R2-R4, S1/S3): the policy of a resolved advancement is the one the GAME pinned; under <see cref="AdvancementPolicy.VersionV2"/> every decision names the runner and the
    /// Retriever whose Speed and Arm produced its row, those ratings are re-read from the set the caller holds NOW (the identity was already proved equal to the prepared pitch's by
    /// <see cref="CheckPrepared"/>), the row and the drawn result are recomputed from the recorded roll with one joint computation, and the throw-out assists are credited to the Arm
    /// player. Under v1 nothing of this exists. Returns a link problem or null.
    /// </summary>
    private Error? LinkAbilities(InPlayPitchCandidate candidate, AdvancementAudit detail)
    {
        var prepared = candidate.Plan.Prepared;
        var state = prepared.State;
        var policy = state.Execution?.AdvancementPolicyVersion ?? AdvancementPolicy.Version;
        if (detail.PolicyVersion != policy)
            return Link($"추가 진루 기록의 정책 '{detail.PolicyVersion}'이 경기가 고정한 정책 '{policy}'과 다릅니다.");
        // F-01: the decisions are bound to the contexts the game's own verified facts declare (the same pure derivation the resolver used), never to what the audit says about itself
        var made = AdvancementPlanner.Contexts(candidate.Plan.Facts);
        if (!made.IsSuccess)
            return Link($"기본 계획에서 추가 진루 맥락을 다시 도출할 수 없습니다: {made.Error!.Value.Code}");
        var contexts = made.Value;
        if (detail.Decisions.Length != contexts.Length)
            return Link("추가 진루 결정의 수가 기본 계획에서 도출한 맥락의 수와 다릅니다.");
        for (var i = 0; i < contexts.Length; i++)
        {
            var (c, d) = (contexts[i], detail.Decisions[i]);
            if (d.Order != c.Order || d.ContextKey != c.Key || d.RunnerKey != c.RunnerKey || d.Runner != c.Runner || d.StartBase != c.StartBase || d.PlanTo != c.PlanTo || d.TargetTo != c.TargetTo)
                return Link($"'{d.ContextKey}' 결정이 기본 계획에서 도출한 {i + 1}번째 맥락(순서·키·주자·시작 베이스·목표)과 다릅니다.");
        }

        if (policy != AdvancementPolicy.VersionV2)
            return detail.PlayerInputsId is not null || detail.Decisions.Any(d => d.Abilities is not null)
                ? Link("중립 추가 진루 정책의 기록에 선수 입력이 붙어 있습니다.")
                : null;

        if (_inputs is null || detail.PlayerInputsId != _inputs.Id || prepared.PlayerInputsId != _inputs.Id)
            return Link("추가 진루 기록의 선수 입력 집합이 준비한 투구·현재 집합과 다릅니다.");
        for (var i = 0; i < contexts.Length; i++)
        {
            var d = detail.Decisions[i];
            if (d.Abilities is null)
                return Link($"'{d.ContextKey}' 결정에 선수 입력 기록이 없습니다.");

            // the expected runner, Retriever and ratings come from the REAL context, the state and the set held now; the record must equal them exactly
            var read = AdvancementSlots.For(contexts[i], state, _inputs);
            if (!read.IsSuccess)
                return Link($"'{d.ContextKey}' 결정의 선수 입력을 실제 맥락에서 다시 읽을 수 없습니다: {read.Error!.Value.Code}");
            if (d.Abilities != read.Value.Audit)
                return Link($"'{d.ContextKey}' 결정의 주자·송구 수비수·능력치 기록이 실제 맥락과 현재 선수 입력 집합에서 읽은 값과 다릅니다.");

            var row = StageProbability.Compute(_tuning!, "advancement", d.ContextKey, read.Value.Modifiers);
            if (!row.IsSuccess || !row.Value.Distribution.Ppm.SequenceEqual(d.Ppm))
                return Link($"'{d.ContextKey}' 결정의 확률표가 실제 입력으로 다시 계산한 값과 다릅니다.");
            if (row.Value.Distribution.SelectIndex(d.Roll) is not { IsSuccess: true } picked || (AdvancementOutcome)picked.Value != d.Drawn)
                return Link($"'{d.ContextKey}' 결정의 뽑은 결과가 기록된 주사위 값과 확률표에서 나오는 결과와 다릅니다.");
        }

        if (candidate.UnconfirmedStep.NewPlays.Count != 1)
            return Link("추가 진루 후보의 미리보기에 단일 득점 플레이가 없어 보살 기록을 확인할 수 없습니다.");
        return AdvancementEventBuilder.ThrowOutCreditProblem(detail.Decisions, candidate.UnconfirmedStep.NewPlays[0], state) is { } armProblem
            ? Link(armProblem.Message)
            : null;
    }

    /// <summary>
    /// O-2 step 9 (the pitch part): the receipt of a located pitch must be an <c>ef2</c> receipt of an event with a location (<c>Commit.receipt_mismatch</c>), and the re-run must
    /// have counted exactly one actual pitch for the pitcher who was fielding before the event (<c>Commit.pitch_count_invalid</c>).
    /// </summary>
    internal Error? CheckApplied(GameState original, GameStep step)
    {
        var receipt = step.State.AppliedEvents[^1];
        if (receipt.FingerprintVersion != EventFingerprint.VersionV2 || Event is null || GameEventLocation.Of(Event) is null)
            return new Error("Commit.receipt_mismatch", "투구 사건의 영수증이 위치를 담은 ef2 형식이 아닙니다.");
        if (PitchPreparation.PitchesOf(step.State, original) != PitchPreparation.PitchesOf(original, original) + 1)
            return new Error("Commit.pitch_count_invalid", "재실행이 실제 투구를 정확히 한 번 세지 않았습니다.");
        return null;
    }

    /// <summary>O-2 step 10a (B-03): the candidate's preview against the re-run. The preview is only compared, never adopted.</summary>
    internal Error? ComparePreview(GameStep official)
    {
        var difference = PreviewComparison.FirstDifference(Preview, official);
        return difference is null ? null : new Error("Commit.preview_mismatch", $"후보의 미리보기가 리듀서 재실행 결과와 다릅니다: {difference}");
    }

    /// <summary>
    /// O-2 step 10b (B-02): the RNG evidence, from snapshots only (<see cref="GameRandom.Restore(GameRandomSnapshot?)"/> works on a private copy; nothing is drawn and nothing
    /// shared changes). H0 is the aggregate's position, H1 the draft's, H2 the candidate's when it differs from H1. A structural error keeps its <c>Rng.*</c> code; everything
    /// else is <c>Commit.rng_evidence_mismatch</c>. Calls (logical <c>NextBounded</c> decisions) and raw draws (raw 32-bit outputs, rejected ones included) are never confused.
    /// </summary>
    internal Error? CheckRng(AuthoritativeGame expected)
    {
        var h0 = expected.Rng;
        var draft = _prepared!.Draft;
        var h1 = draft.NextRng;
        var final = Final!;

        foreach (var snapshot in ReferenceEquals(final, h1) ? new[] { h1 } : new[] { h1, final })
        {
            var restored = GameRandom.Restore(snapshot);
            if (!restored.IsSuccess)
                return restored.Error!.Value;
            if (snapshot.FormatVersion != h0.FormatVersion || snapshot.AlgorithmVersion != h0.AlgorithmVersion || snapshot.KeyDerivationVersion != h0.KeyDerivationVersion)
                return Rng("난수 형식·알고리즘·키 유도 버전이 현재 권위 난수(H0)와 다릅니다.");
            if (snapshot.MasterSeed != h0.MasterSeed)
                return Rng("난수 마스터 시드가 현재 권위 난수(H0)와 다릅니다.");
            if (!string.Equals(snapshot.ContextKey, h0.ContextKey, StringComparison.Ordinal) || !string.Equals(snapshot.ContextKey, expected.State.GameId, StringComparison.Ordinal))
                return Rng("난수 맥락 키가 현재 권위 난수 또는 경기 ID와 다릅니다.");
        }

        if (DraftInconsistency(draft) is { } inconsistent)
            return Rng($"투구 분기 기록의 난수 사용량이 서로 맞지 않습니다: {inconsistent}");

        // H0 to H1: every stream of H0 is still there and never goes backwards; each stream grew by exactly the draft's recorded raw draws; a stream the record does not name did not move
        var used = draft.StreamUsage.ToDictionary(u => u.Stream.Canonical, u => u.RawDraws, StringComparer.Ordinal);
        var before = h0.Streams.ToDictionary(s => s.Stream.Canonical, s => s.Draws, StringComparer.Ordinal);
        var after = h1.Streams.ToDictionary(s => s.Stream.Canonical, s => s.Draws, StringComparer.Ordinal);
        foreach (var (stream, draws) in before)
        {
            if (!after.TryGetValue(stream, out var now))
                return Rng($"현재 난수(H0)의 스트림 {stream}이 투구 뒤 난수(H1)에서 사라졌습니다.");
            if (now < draws)
                return Rng($"스트림 {stream}의 소비 횟수가 뒤로 갔습니다({draws} -> {now}).");
        }

        foreach (var stream in after.Keys.Concat(used.Keys).Concat(before.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var found = after.GetValueOrDefault(stream) - before.GetValueOrDefault(stream);
            var recorded = used.GetValueOrDefault(stream);
            if (found == recorded)
                continue;
            return Rng(!used.ContainsKey(stream)
                ? $"기록에 없는 스트림 {stream}의 소비가 {found}만큼 늘었습니다."
                : $"스트림 {stream}의 원시 소비 증가량이 기록 {recorded}과 다릅니다(실제 {found}).");
        }

        // H1 to H2: only a resolved in-play candidate has a separate H2; then only game/runner moved, by exactly the advancement record's raw draws
        if (ReferenceEquals(final, h1))
            return _inPlay?.Audit.AdvancementDetail is not null ? Rng("추가 진루를 해결한 후보가 H1을 그대로 채택하려 합니다.") : null;
        if (_inPlay?.Audit.AdvancementDetail is not { } detail)
            return Rng("H1과 다른 난수 위치를 채택하려는데 추가 진루 해결 기록이 없습니다.");

        var runner = GameRngStreams.Runner.Canonical;
        var finalDraws = final.Streams.ToDictionary(s => s.Stream.Canonical, s => s.Draws, StringComparer.Ordinal);
        foreach (var (stream, draws) in after)
            if (!finalDraws.TryGetValue(stream, out var now) || now < draws)
                return Rng($"H1의 스트림 {stream}이 H2에서 사라졌거나 뒤로 갔습니다.");
        foreach (var stream in finalDraws.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var found = finalDraws.GetValueOrDefault(stream) - after.GetValueOrDefault(stream);
            var recorded = stream == runner ? detail.RawDraws : 0;
            if (found != recorded)
                return Rng(stream == runner
                    ? $"스트림 {stream}의 H1에서 H2까지 원시 소비 증가량이 추가 진루 기록 {recorded}과 다릅니다(실제 {found})."
                    : $"추가 진루와 무관한 스트림 {stream}의 소비가 H1에서 H2 사이에 {found}만큼 늘었습니다.");
        }

        return null;
    }

    /// <summary>Whether the draft's own usage record agrees with its steps: calls versus raw draws are different numbers and are checked separately. Returns the first problem or null.</summary>
    private static string? DraftInconsistency(PitchBranchDraft draft)
    {
        if (draft.Steps.IsDefault || draft.StreamUsage.IsDefault)
            return "단계 또는 사용량 목록이 없습니다.";
        if (draft.Steps.Length != draft.NextBoundedCalls)
            return $"단계 수 {draft.Steps.Length}와 NextBoundedCalls {draft.NextBoundedCalls}가 다릅니다.";
        if (draft.StreamUsage.Sum(u => u.Calls) != draft.NextBoundedCalls)
            return "스트림별 호출 수의 합이 NextBoundedCalls와 다릅니다.";
        if (draft.StreamUsage.Select(u => u.Stream).Distinct().Count() != draft.StreamUsage.Length)
            return "같은 스트림이 사용량에 두 번 있습니다.";
        foreach (var use in draft.StreamUsage)
        {
            var steps = draft.Steps.Where(s => s.Stream == use.Stream).ToArray();
            if (steps.Length != use.Calls)
                return $"스트림 {use.Stream.Canonical}의 호출 수 {use.Calls}가 단계 수 {steps.Length}와 다릅니다.";
            if (steps.Sum(s => s.RawDraws) != use.RawDraws)
                return $"스트림 {use.Stream.Canonical}의 원시 소비 {use.RawDraws}가 단계별 원시 소비의 합과 다릅니다.";
            if (use.Calls < 1 || use.RawDraws < use.Calls)
                return $"스트림 {use.Stream.Canonical}의 원시 소비 {use.RawDraws}가 호출 수 {use.Calls}보다 적거나 호출이 없습니다.";
        }

        return draft.Steps.Any(s => draft.StreamUsage.All(u => u.Stream != s.Stream)) ? "사용량에 없는 스트림을 쓰는 단계가 있습니다." : null;
    }

    private static Error Link(string message) => new(LinkCode, message);

    private static Error Rng(string message) => new(RngCode, message);
}
