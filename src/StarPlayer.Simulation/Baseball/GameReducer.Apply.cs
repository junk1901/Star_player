using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

public static partial class GameReducer
{
    private static readonly ImmutableList<GameFact> NoFacts = ImmutableList<GameFact>.Empty;

    /// <summary>
    /// Applies one event. The same event id with the same content is a no-op that returns the same state (idempotent replay). Any illegal event
    /// returns an explicit error; the caller's state value is never modified, so there is no partial mutation to undo.
    /// </summary>
    public static Result<GameStep> Apply(GameState? state, GameEvent? gameEvent)
    {
        if (state is null || gameEvent is null || string.IsNullOrWhiteSpace(gameEvent.Id) || gameEvent.Id != gameEvent.Id.Trim())
            return Fail<GameStep>("Game.event_invalid", "경기 상태 또는 사건 ID가 올바르지 않습니다.");
        var receipt = EventFingerprint.Receipt(gameEvent);
        if (state.AppliedEvents.FirstOrDefault(r => r.EventId == gameEvent.Id) is { } known)
        {
            if (known.FingerprintVersion != receipt.FingerprintVersion)
                return Fail<GameStep>("Game.event_fingerprint_version_mismatch", "이 사건 ID의 영수증은 다른 버전의 지문 규칙으로 만들어져 같은 내용인지 비교할 수 없습니다.");
            if (known.Kind != receipt.Kind || known.Fingerprint != receipt.Fingerprint)
                return Fail<GameStep>("Game.event_id_conflict", $"사건 ID '{gameEvent.Id}'는 이미 다른 내용의 사건({known.Kind})에 쓰였습니다. 같은 ID는 같은 내용으로만 다시 보낼 수 있습니다.");
            return Result<GameStep>.Success(new GameStep(state, GameStatDelta.Empty(gameEvent.Id), ImmutableList<ScoredPlay>.Empty, NoFacts, true));
        }

        if (state.Status == GameStatus.Final)
            return Fail<GameStep>("Game.already_over", "이미 끝난 경기에는 사건을 적용할 수 없습니다.");
        if (gameEvent is not SubstitutionEvent && !DefenseComplete(state.Fielding))
            return Fail<GameStep>("Game.defense_incomplete", "수비 구성이 완성되지 않았습니다. 먼저 선수 교체로 수비를 맞춰야 합니다.");

        var result = gameEvent switch
        {
            ApproachEvent approach => ApplyApproach(state, approach),
            PitchEvent pitch => ApplyPitch(state, pitch.Id, pitch.Spec, isTake: false),
            TakeEvent take => ApplyPitch(state, take.Id, take.Spec, isTake: true),
            InPlayEvent inPlay => ApplyInPlay(state, inPlay),
            RunnerPlayEvent runnerPlay => ApplyRunnerPlay(state, runnerPlay),
            SubstitutionEvent substitution => ApplySubstitution(state, substitution),
            _ => Fail<GameStep>("Game.event_unknown", "알 수 없는 종류의 사건입니다."),
        };
        if (!result.IsSuccess)
            return result;

        var step = result.Value;
        return Result<GameStep>.Success(step with { State = step.State with { AppliedEvents = step.State.AppliedEvents.Add(receipt) } });
    }

    private static Result<GameStep> ApplyApproach(GameState s, ApproachEvent e)
    {
        if (s.Stage == PaStage.InSequence)
            return Fail<GameStep>("Game.approach_not_legal", "이미 접근 방식이 정해진 타석입니다.");
        if (!Enum.IsDefined(e.Approach))
            return Fail<GameStep>("Game.approach_invalid", "접근 방식 값이 올바르지 않습니다.");
        return Result<GameStep>.Success(new GameStep(s with { Stage = PaStage.InSequence, Approach = e.Approach }, GameStatDelta.Empty(e.Id), ImmutableList<ScoredPlay>.Empty, NoFacts, false));
    }

    // ------------------------------------------------------------------ finalize

    /// <summary>
    /// Finishes a finished game exactly once: builds the box score, decisions and the one-time catcher/battery exposure. Calling it
    /// again returns the same result with <c>WasNew == false</c>; nothing is recounted.
    /// </summary>
    public static Result<FinalizeResult> Finalize(GameState? state)
    {
        if (state is null)
            return Fail<FinalizeResult>("Game.state_missing", "경기 상태가 없습니다.");
        if (state.Final is { } existing)
            return Result<FinalizeResult>.Success(new FinalizeResult(state, existing, false));
        if (state.Status != GameStatus.Final || state.Outcome is not { } outcome)
            return Fail<FinalizeResult>("Game.not_over", "아직 끝나지 않은 경기는 확정할 수 없습니다.");

        var box = BoxScoreBuilder.Build(state);
        var final = new FinalizedGame(state.GameId, outcome, state.AwayRuns, state.HomeRuns, state.Rules.ProfileId, state.Rules.ContentHash, box, state.Execution);
        return Result<FinalizeResult>.Success(new FinalizeResult(state with { Final = final }, final, true));
    }

    /// <summary>The typed fact announcing the finished game; the caller emits it once, when <see cref="FinalizeResult.WasNew"/> is true.</summary>
    public static GameCompleted CompletedFact(FinalizedGame game) => new(game.GameId, game.Outcome, game.AwayRuns, game.HomeRuns);
}
