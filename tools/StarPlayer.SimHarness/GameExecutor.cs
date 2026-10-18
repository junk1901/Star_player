using System.Diagnostics;
using System.Text;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.SimHarness;

/// <summary>
/// Plays ONE game through the production engine and nothing else: <see cref="GameStarter"/> begins it, <see cref="PitchPreparation"/>, <see cref="InPlayPitchPlanning"/> and
/// <see cref="AdvancementPlanning"/> build what happens, <see cref="PitchCommitGate"/> / <see cref="CommitGate"/> stage it and <see cref="GameCursor.Publish"/> makes it official. This class chooses an
/// action (through the <see cref="IGamePolicy"/>) and confirms what the engine built; it computes no probability, outcome, base running, scoring, inning change or ending itself, and it never
/// edits a state. A failure of any engine step ends the game as <see cref="GameRunStatus.Failed"/> with the engine's own error code, and a game that does not reach a final state is never reported as finished.
/// </summary>
public static class GameExecutor
{
    private const int TailReceipts = 12;

    public static GameRunResult Play(RunConfig run, GameSpec game, CancellationToken cancellation = default)
    {
        var timer = Stopwatch.StartNew();
        var recorder = new MetricsRecorder();

        GameRunResult Stopped(GameRunStatus status, GameCursor? cursor, string code, string message, string stage, CrossCheckResult? cross = null)
        {
            var current = cursor?.Current;
            var receipts = current?.State.AppliedEvents ?? [];
            var failure = new RunFailure(code, message, stage, current?.LastEventSequence ?? 0, current?.Revision ?? 0, current?.Rng.StateHash ?? "-",
                receipts.Skip(Math.Max(0, receipts.Count - TailReceipts)).Select(r => new ReceiptRef(r.EventId, r.Kind, r.Fingerprint)).ToList());
            return Build(run, game, status, failure, current, recorder.Counters, cross, timer.Elapsed.TotalMilliseconds);
        }

        if (run.CheckPins() is { } pins)
            return Stopped(GameRunStatus.Failed, null, pins.Code, pins.Message, "config");

        var (away, home) = run.Cohort.SetupFor(game.GameIndex);                                 // the cohort's own per-game rotation; a non-rotating cohort returns its two setups unchanged
        var begun = GameStarter.Begin(new GameStart(game.GameId, run.Rules, away, home, run.Execution), game.Seed);
        if (!begun.IsSuccess)
            return Stopped(GameRunStatus.Failed, null, begun.Error!.Value.Code, begun.Error.Value.Message, "start");
        var cursor = begun.Value;

        try
        {
            while (cursor.Current.State.Status == GameStatus.InProgress)
            {
                cancellation.ThrowIfCancellationRequested();
                var before = cursor.Current;
                if (before.LastEventSequence >= run.EventBudget)
                    return Stopped(GameRunStatus.Failed, cursor, "Harness.event_budget_exceeded", $"경기가 이벤트 예산 {run.EventBudget}건 안에 끝나지 않았습니다(종료로 취급하지 않음).", "budget");

                var step = Advance(run, cursor, cancellation);
                if (!step.IsSuccess)
                    return Stopped(GameRunStatus.Failed, cursor, step.Error!.Value.Code, step.Error.Value.Message, "advance");
                var seen = recorder.Observe(step.Value);
                if (seen == ObserveOutcome.SequenceGap)
                    return Stopped(GameRunStatus.Failed, cursor, "Harness.event_sequence_gap", "확정된 사건 순번이 건너뛰었습니다.", "observe");
                if (cursor.Current.LastEventSequence <= before.LastEventSequence)
                    return Stopped(GameRunStatus.Failed, cursor, "Harness.stalled", "확정 후에도 사건 순번이 늘지 않았습니다.", "progress");
            }

            // a finished game: the final record must carry the execution contract the game started with, and the two derivations of its statistics must agree
            var last = cursor.Current;
            var final = last.State.Final;
            if (final is null || !Equals(final.Execution, run.Execution))
                return Stopped(GameRunStatus.Failed, cursor, "Harness.final_state_invalid", "끝난 경기의 기록이 없거나 시작 때의 실행 계약을 담고 있지 않습니다.", "final");
            if (recorder.Counters.UnlocatedPitches > 0)
                return Stopped(GameRunStatus.Failed, cursor, "Harness.pitch_location_missing", $"실제 위치가 없는 투구가 {recorder.Counters.UnlocatedPitches}개 있습니다(투구 해결기는 항상 위치를 확정해야 합니다).", "metrics");
            var cross = BoxScoreCrossCheck.Compare(recorder.Counters, last.State);
            if (!cross.Passed)
            {
                var text = string.Join(", ", cross.Mismatches.Select(m => $"{m.Name}: 이벤트 {m.FromEvents} / 박스스코어 {m.FromBoxScore}"));
                return Stopped(GameRunStatus.Failed, cursor, "Harness.crosscheck_mismatch", $"이벤트 집계와 BoxScore가 다릅니다: {text}", "crosscheck", cross);
            }

            return Build(run, game, GameRunStatus.Completed, null, last, recorder.Counters, cross, timer.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return Stopped(GameRunStatus.Cancelled, cursor, "Harness.cancelled", "경기가 취소되었습니다(확정된 사건만 남음).", "cancelled");
        }
        catch (Exception e)
        {
            return Stopped(GameRunStatus.Failed, cursor, "Harness.unhandled_exception", $"{e.GetType().Name}: {e.Message}", "exception");
        }
    }

    // ---------------------------------------------------------------- one action

    private static Result<CommitRecord> Advance(RunConfig run, GameCursor cursor, CancellationToken ct)
    {
        var current = cursor.Current;
        var state = current.State;
        var legal = GameReducer.LegalActions(state);
        if (!legal.DefenseComplete)
            return Fail("Harness.substitution_required", "이 상태는 교체가 있어야 진행되지만, 하네스에는 교체 정책이 없습니다(임의 교체는 만들지 않음).");

        PitchIntent intent;
        bool take;
        if (state.Stage == PaStage.InSequence)
        {
            if (!legal.Allows(GameActionKind.Pitch))
                return Fail("Harness.pitch_not_legal", "접근 방식이 정해진 타석인데 투구가 합법 행동이 아닙니다.");
            (intent, take) = (run.Policy.IntentFor(state), false);
        }
        else
        {
            var decision = run.Policy.AtPlateAppearance(state);
            if (!legal.Allows(decision.Take ? GameActionKind.Take : GameActionKind.Approach))
                return Fail("Harness.action_not_legal", "정책이 고른 행동이 지금 합법이 아닙니다.");
            if (!decision.Take)
            {
                var id = GameEventIds.Create(state.GameId, current.LastEventSequence).Value.Next();
                if (!id.IsSuccess)
                    return Result<CommitRecord>.Failure(id.Error!.Value);
                return Publish(cursor, CommitGate.Stage(current, new ApproachEvent(id.Value, decision.Approach), ct), ct);
            }

            (intent, take) = (run.Policy.IntentFor(state), true);
        }

        if (!Sheets(run, state, out var batter, out var pitcher))
            return Fail("Harness.sheet_missing", "현재 타자 또는 투수의 능력치 정보가 코호트에 없습니다.");
        var tuning = run.Tuning;
        var inputs = run.Cohort.InputSet;

        var prepared = PitchPreparation.Prepare(state, current.Rng, tuning, intent, take, batter, pitcher, ct, null, inputs);
        if (!prepared.IsSuccess)
            return Result<CommitRecord>.Failure(prepared.Error!.Value);

        if (prepared.Value.Draft.Kind != PitchBranchKind.InPlay)
        {
            var candidate = PitchPreparation.BuildNonInPlayCandidate(prepared.Value, state, current.Rng, tuning, batter, pitcher, current.LastEventSequence, ct, inputs);
            return candidate.IsSuccess
                ? Publish(cursor, PitchCommitGate.Stage(current, prepared.Value, candidate.Value, tuning, batter, pitcher, ct, inputs), ct)
                : Result<CommitRecord>.Failure(candidate.Error!.Value);
        }

        if (take)
            return Fail("Harness.take_in_play", "Take로 준비한 투구가 인플레이로 해결되었습니다(엔진 계약 위반).");
        var plan = InPlayPitchPlanning.Build(prepared.Value, state, current.Rng, tuning, batter, pitcher, current.LastEventSequence, ct, inputs);
        if (!plan.IsSuccess)
            return Result<CommitRecord>.Failure(plan.Error!.Value);
        InPlayPitchCandidate finished;
        if (plan.Value is PendingAdvancement open)
        {
            var resolved = AdvancementPlanning.Resolve(open, state, current.Rng, tuning, batter, pitcher, current.LastEventSequence, ct, inputs);
            if (!resolved.IsSuccess)
                return Result<CommitRecord>.Failure(resolved.Error!.Value);
            finished = resolved.Value;
        }
        else
            finished = (InPlayPitchCandidate)plan.Value;
        return Publish(cursor, PitchCommitGate.Stage(current, finished, tuning, batter, pitcher, ct, inputs), ct);
    }

    private static Result<CommitRecord> Publish(GameCursor cursor, Result<StagedCommit> staged, CancellationToken ct) =>
        staged.IsSuccess ? cursor.Publish(staged.Value, ct) : Result<CommitRecord>.Failure(staged.Error!.Value);

    private static bool Sheets(RunConfig run, GameState state, out PlayerSheet batter, out PlayerSheet pitcher)
    {
        batter = pitcher = null!;
        return run.Cohort.Sheets.TryGetValue(state.Batting.CurrentBatterSlot.Player, out batter!) && run.Cohort.Sheets.TryGetValue(state.Fielding.Pitcher, out pitcher!);
    }

    private static Result<CommitRecord> Fail(string code, string message) => Result<CommitRecord>.Failure(new Error(code, message));

    // ---------------------------------------------------------------- the result and its hashes

    private static GameRunResult Build(RunConfig run, GameSpec game, GameRunStatus status, RunFailure? failure, AuthoritativeGame? last, MetricCounters counters, CrossCheckResult? cross, double elapsed)
    {
        var state = last?.State;
        var final = state?.Final;                                   // an engine-final game keeps its outcome even when the harness then rejects its data
        var done = final is not null;
        var eventHash = Hashing.Sha256(string.Join('\n', (state?.AppliedEvents ?? []).Select(r => $"{r.EventId}|{r.Kind}|{r.FingerprintVersion}|{r.Fingerprint}")));
        var rngHash = last?.Rng.StateHash ?? "-";
        var kind = !done ? GameEndKind.None
            : final!.Outcome == GameOutcome.Tie ? GameEndKind.TieAtExtraInningsLimit
            : state!.Inning > run.Rules.RegulationInnings ? GameEndKind.ExtraInnings : GameEndKind.Regulation;
        var inning = state?.Inning ?? 0;
        var half = state?.Half ?? InningHalf.Top;
        var text = new StringBuilder("game-result-v1\n")
            .Append(game.GameId).Append('|').Append(game.Seed).Append('|').Append(status).Append('|').Append(final?.Outcome.ToString() ?? "-").Append('|')
            .Append(state?.AwayRuns ?? 0).Append('-').Append(state?.HomeRuns ?? 0).Append('|').Append(inning).Append(half).Append('|').Append(kind).Append('|')
            .Append(last?.LastEventSequence ?? 0).Append('|').Append(eventHash).Append('|').Append(rngHash).Append('|').Append(failure?.Code ?? "-").Append('|')
            .Append(counters.Canonical());
        return new GameRunResult(game, status, failure, final?.Outcome, kind, state?.AwayRuns ?? 0, state?.HomeRuns ?? 0, inning, half, last?.LastEventSequence ?? 0, counters, cross,
            eventHash, rngHash, Hashing.Sha256(text.ToString()), elapsed);
    }
}
