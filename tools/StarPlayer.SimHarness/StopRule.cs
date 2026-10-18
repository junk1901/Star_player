using System.Globalization;
using System.Text;
using System.Text.Json;
using StarPlayer.Domain.Common;

namespace StarPlayer.SimHarness;

/// <summary>Why a stop-aware run ended (ADR-009 SR-D1.2). It is never a gate verdict (SR-D9.3).</summary>
public enum StopReason { TargetReached, SeedsExhausted }

/// <summary>How a stop-aware run ended: by the stop rule (it has a <see cref="StopRecord"/>), cancelled (SR-D3.5) or aborted because trust in its inputs broke (SR-D3.4). Only <see cref="Rule"/> is a termination.</summary>
public enum RunTermination { Rule, Cancelled, AbortedIntegrity }

/// <summary>The <c>results.stop</c> record of ADR-009 SR-D7.1, written exactly when the run terminated by the rule.</summary>
public sealed record StopRecord(int ExecutedGames, StopReason Reason, long CumulativePa, int BlockSize)
{
    public const string TargetReachedText = "TARGET_REACHED";
    public const string SeedsExhaustedText = "SEEDS_EXHAUSTED";

    public string ReasonText => Reason == StopReason.TargetReached ? TargetReachedText : SeedsExhaustedText;

    /// <summary>Strict read of a recorded stop record: exactly the four members, integer values, a known reason. Anything else is <c>Replay.stop_record_invalid</c> (it is compared, never trusted).</summary>
    public static Result<StopRecord> FromJson(JsonElement e)
    {
        static Result<StopRecord> Bad(string why) => Result<StopRecord>.Failure(new Error("Replay.stop_record_invalid", $"results.stop가 올바르지 않습니다: {why}"));
        if (e.ValueKind != JsonValueKind.Object)
            return Bad("객체가 아닙니다.");
        string[] names = ["executed_games", "stop_reason", "cumulative_pa", "block_size"];
        var members = e.EnumerateObject().Select(p => p.Name).ToList();
        if (members.Count != names.Length || names.Any(n => !members.Contains(n)))
            return Bad($"항목은 정확히 {string.Join(", ", names)}여야 합니다.");
        if (e.GetProperty("executed_games").ValueKind != JsonValueKind.Number || !e.GetProperty("executed_games").TryGetInt32(out var executed)
            || e.GetProperty("cumulative_pa").ValueKind != JsonValueKind.Number || !e.GetProperty("cumulative_pa").TryGetInt64(out var pa)
            || e.GetProperty("block_size").ValueKind != JsonValueKind.Number || !e.GetProperty("block_size").TryGetInt32(out var block))
            return Bad("executed_games·cumulative_pa·block_size는 정수여야 합니다.");
        var reason = e.GetProperty("stop_reason").ValueKind == JsonValueKind.String ? e.GetProperty("stop_reason").GetString() : null;
        if (reason is not (TargetReachedText or SeedsExhaustedText))
            return Bad($"stop_reason은 {TargetReachedText} 또는 {SeedsExhaustedText}만 허용됩니다.");
        if (executed < 1 || block < 1 || pa < 0 || executed % block != 0)
            return Bad("executed_games는 block_size의 양의 배수이고 cumulative_pa는 0 이상이어야 합니다.");
        return Result<StopRecord>.Success(new StopRecord(executed, reason == TargetReachedText ? StopReason.TargetReached : StopReason.SeedsExhausted, pa, block));
    }
}

/// <summary>
/// The TRUSTED parameters of a stop-aware run (SR-D1.5, SR-D8.2): the whole approved plan in file order, its block size and PA target, and the seed file they were read from. It is built only from a
/// seed file read against an externally pinned hash (<see cref="FromSeedManifest"/>), never from a recorded stop record.
/// </summary>
public sealed record StopPlan(string Family, int BlockSize, long PaTarget, IReadOnlyList<GameSpec> Games, string SeedManifestPath, string SeedManifestSha256)
{
    /// <summary>SR-D5: the stop rule is a <c>fc3-cal</c> rule; <c>fc2-dry</c> is the single non-official exception path (SR-D4).</summary>
    public static bool FamilyIsStopAware(string family) => family is "fc3-cal" or "fc2-dry";

    public static Result<StopPlan> FromSeedManifest(SeedManifest m, string path)
    {
        if (!FamilyIsStopAware(m.Family))
            return Result<StopPlan>.Failure(new Error("Stop.family_not_stop_aware", $"family '{m.Family}'는 정지 규칙을 쓰지 않습니다(전체 실행 계약, SR-D5)."));
        if (m.PaTarget is not { } target)
            return Result<StopPlan>.Failure(new Error("Stop.pa_target_missing", $"Seed Manifest '{path}'에 pa_target이 없어 정지 인식 실행을 할 수 없습니다."));
        return Result<StopPlan>.Success(new StopPlan(m.Family, m.BlockSizeGames, target, m.Seeds.Select(s => new GameSpec(s.Index, s.GameId, s.Seed)).ToList(), path, m.FileSha256));
    }
}

/// <summary>
/// ADR-009 N-04: the ONE implementation of the calibration stop rule (SR-D1.4). <see cref="Decide"/> is the rule (the committed <see cref="SeedManifest.AfterGames"/> delegates to it);
/// <see cref="Execute"/> is the only stop-aware executor, used by a run and by its replay, so a replay derives the stop point by running the same code on the same trusted plan.
/// </summary>
public static class StopRule
{
    /// <summary>
    /// After <paramref name="gamesPlayed"/> games (a failed game counts as played) with <paramref name="validPa"/> valid plate appearances: only at a block boundary may a run stop; the target is tested
    /// first (inclusive), then exhaustion of the declared seeds. No seed is ever added (FC-D5.3, SR-D1.2).
    /// </summary>
    public static BlockDecision Decide(int blockSize, long? paTarget, int seedCount, int gamesPlayed, long validPa)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(validPa);
        ArgumentOutOfRangeException.ThrowIfLessThan(blockSize, 1);
        if (gamesPlayed < 0 || gamesPlayed > seedCount)
            throw new ArgumentOutOfRangeException(nameof(gamesPlayed), gamesPlayed, $"0~{seedCount}이어야 합니다.");
        if (gamesPlayed % blockSize != 0)
            return BlockDecision.Continue;
        if (paTarget is { } target && validPa >= target)
            return BlockDecision.StopTargetReached;
        return gamesPlayed == seedCount ? BlockDecision.StopSeedsExhausted : BlockDecision.Continue;
    }

    /// <summary>SR-D3.4: a game that failed in the configuration stage failed the harness' own pin check of the run record, so the run's inputs are not trusted any more.</summary>
    public static bool IsIntegrityFailure(GameRunResult game) => game.Status == GameRunStatus.Failed && game.Failure?.Stage == "config";

    public sealed record Outcome(IReadOnlyList<GameRunResult> Games, RunTermination Termination, StopRecord? Stop, Error? Abort);

    /// <summary>
    /// Plays the plan in file order, one block at a time, and ends at the first legal stop point (SR-D1.2). Valid PA is the PA of COMPLETED games only (a completed game passed the BoxScore cross-check,
    /// <see cref="GameExecutor"/>); a failed game counts as played and the run goes on by the plan (SR-D3.2, SR-D3.3). A cancelled game ends the run without a stop record (SR-D3.5); a broken
    /// integrity check — <paramref name="integrity"/> before every block, or a game whose pin check failed — aborts it without one (SR-D3.4). Games after the stop are never played (SR-D1.3).
    /// </summary>
    public static Outcome Execute(StopPlan plan, Func<GameSpec, GameRunResult> play, Func<Error?>? integrity = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(play);
        if (plan.Games.Count == 0 || plan.BlockSize < 1 || plan.Games.Count % plan.BlockSize != 0 || plan.PaTarget < 1)
            throw new ArgumentException("정지 계획은 완전한 블록으로 된 비어 있지 않은 경기 목록과 양의 PA 목표가 필요합니다(검증된 Seed Manifest에서만 만듭니다).", nameof(plan));
        var games = new List<GameRunResult>();
        long pa = 0;
        for (var i = 0; i < plan.Games.Count; i++)
        {
            if (i % plan.BlockSize == 0 && integrity?.Invoke() is { } broken)
                return new Outcome(games, RunTermination.AbortedIntegrity, null, broken);
            var game = play(plan.Games[i]);
            games.Add(game);
            if (game.Status == GameRunStatus.Cancelled)
                return new Outcome(games, RunTermination.Cancelled, null, null);
            if (IsIntegrityFailure(game))
                return new Outcome(games, RunTermination.AbortedIntegrity, null, new Error(game.Failure!.Code, game.Failure.Message));
            if (game.Completed)
                pa = checked(pa + game.Counters.PlateAppearances);
            var decision = Decide(plan.BlockSize, plan.PaTarget, plan.Games.Count, i + 1, pa);
            if (decision != BlockDecision.Continue)
                return new Outcome(games, RunTermination.Rule, new StopRecord(i + 1, decision == BlockDecision.StopTargetReached ? StopReason.TargetReached : StopReason.SeedsExhausted, pa, plan.BlockSize), null);
        }

        throw new InvalidOperationException("완전한 블록의 계획은 마지막 경기에서 반드시 정지합니다.");   // unreachable: the last game is a block boundary and exhausts the seeds
    }
}

/// <summary>The result hash texts. <c>run-result-v1</c> is the unchanged F-A/B text; <c>run-result-v2</c> (SR-D7.3) is the stop-aware one.</summary>
public static class RunResultText
{
    public const string V1 = "run-result-v1";
    public const string V2 = "run-result-v2";

    public static string V1Text(string configHash, IEnumerable<string> gameHashes, string aggregate)
    {
        var text = new StringBuilder(V1).Append('\n').Append(configHash).Append('\n');
        foreach (var hash in gameHashes)
            text.Append(hash).Append('\n');
        return text.Append(aggregate).ToString();
    }

    /// <summary>
    /// <c>run-result-v2</c>, fixed byte layout (UTF-8, <c>\n</c> line ends, invariant integers), covered by a golden vector:
    /// <code>
    /// run-result-v2
    /// {config_hash over the WHOLE plan}
    /// plan|{planned games}|{pa_target}
    /// stop|{executed_games}|{stop_reason}|{cumulative_pa}|{block_size}
    /// {game result hash}            (one line per EXECUTED game, plan order)
    /// {aggregate counters, MetricCounters.Canonical}
    /// </code>
    /// </summary>
    public static string V2Text(string configHash, int plannedGames, long paTarget, StopRecord stop, IEnumerable<string> gameHashes, string aggregate)
    {
        var i = CultureInfo.InvariantCulture;
        var text = new StringBuilder(V2).Append('\n').Append(configHash).Append('\n')
            .Append("plan|").Append(plannedGames.ToString(i)).Append('|').Append(paTarget.ToString(i)).Append('\n')
            .Append("stop|").Append(stop.ExecutedGames.ToString(i)).Append('|').Append(stop.ReasonText).Append('|').Append(stop.CumulativePa.ToString(i)).Append('|').Append(stop.BlockSize.ToString(i)).Append('\n');
        foreach (var hash in gameHashes)
            text.Append(hash).Append('\n');
        return text.Append(aggregate).ToString();
    }
}

/// <summary>One recorded game of a stop-aware manifest: identity, status and every execution-evidence field the re-execution reproduces (FC2B-02).</summary>
public sealed record RecordedGame(string GameId, ulong Seed, string Status, string ResultHash, string EventHash, string RngHash, string? ErrorCode)
{
    public static RecordedGame Of(GameRunResult g) => new(g.Game.GameId, g.Game.Seed, g.Status.ToString(), g.ResultHash, g.EventHash, g.RngHash, g.Failure?.Code);
}

/// <summary>
/// What a stop-aware manifest CLAIMS (SR-D8.3: compared with the re-derivation, never trusted): the stop record, the plate appearances, the games and the run-level execution evidence
/// (<c>games_requested</c> / <c>games_completed</c> / <c>games_failed</c>, <c>pitches</c>, <c>run_status</c>), all of which <see cref="StopReplay.Compare"/> checks against the re-execution.
/// </summary>
public sealed record RecordedStopResult(StopRecord Stop, long PlateAppearances, IReadOnlyList<RecordedGame> Games, int Requested, int Completed, int Failed, long Pitches, string RunStatus)
{
    /// <summary>What an honest manifest of <paramref name="run"/> records.</summary>
    public static RecordedStopResult Of(RunResult run) =>
        new(run.Stop!, run.Aggregate.PlateAppearances, run.Games.Select(RecordedGame.Of).ToList(), run.Requested, run.CompletedCount, run.FailedCount, run.Aggregate.Pitches, RunReport.RunStatus(run));
}

/// <summary>
/// SR-D8 replay of a stop-aware result. <see cref="Precheck"/> runs before any game (structure and consistency of the claim against the trusted plan); <see cref="Compare"/> runs after the replay
/// re-executed the trusted plan with <see cref="StopRule.Execute"/> and matches only if every derived value equals the recorded one AND the recomputed <c>run-result-v2</c> equals the recorded hash.
/// </summary>
public static class StopReplay
{
    public static Error? Precheck(StopPlan trusted, RecordedStopResult recorded)
    {
        static Error E(string code, string message) => new(code, message);
        var s = recorded.Stop;
        if (s.BlockSize != trusted.BlockSize)
            return E("Replay.stop_block_size_mismatch", $"기록된 block_size {s.BlockSize}이(가) 고정된 Seed 파일의 {trusted.BlockSize}와 다릅니다.");
        if (s.ExecutedGames > trusted.Games.Count)
            return E("Replay.stop_too_late", $"기록된 executed_games {s.ExecutedGames}이(가) 계획 {trusted.Games.Count}경기를 넘습니다.");
        if (recorded.Games.Count != s.ExecutedGames || recorded.Games.Where((g, i) => g.GameId != trusted.Games[i].GameId || g.Seed != trusted.Games[i].Seed).Any())
            return E("Replay.result_games_not_prefix", "results.games가 계획(games)의 앞쪽 executed_games개 경기와 순서·GameId·Seed까지 같지 않습니다.");
        if (recorded.PlateAppearances != s.CumulativePa)
            return E("Replay.plate_appearances_mismatch", $"기록된 plate_appearances {recorded.PlateAppearances}이(가) 기록된 cumulative_pa {s.CumulativePa}와 다릅니다.");
        var claimed = StopRule.Decide(trusted.BlockSize, trusted.PaTarget, trusted.Games.Count, s.ExecutedGames, s.CumulativePa);
        var expected = s.Reason == StopReason.TargetReached ? BlockDecision.StopTargetReached : BlockDecision.StopSeedsExhausted;
        return claimed == expected ? null : E("Replay.stop_record_inconsistent", $"기록된 정지({s.ReasonText}, {s.ExecutedGames}경기, PA {s.CumulativePa})는 고정된 정지 규칙의 결정({claimed})과 맞지 않습니다.");
    }

    public static Error? Compare(RecordedStopResult recorded, string recordedHash, RunResult derived)
    {
        static Error E(string code, string message) => new(code, message);
        if (derived.Termination != RunTermination.Rule || derived.Stop is not { } d)
            return E("Replay.run_not_terminated", "replay 실행이 정지 규칙으로 끝나지 않았습니다(취소 또는 무결성 중단).");
        var r = recorded.Stop;
        if (r.ExecutedGames < d.ExecutedGames)
            return E("Replay.stop_too_early", $"기록된 정지 {r.ExecutedGames}경기가 규칙이 정하는 첫 정지점 {d.ExecutedGames}경기보다 이릅니다.");
        if (r.ExecutedGames > d.ExecutedGames)
            return E("Replay.stop_too_late", $"기록된 정지 {r.ExecutedGames}경기가 규칙이 정하는 첫 정지점 {d.ExecutedGames}경기보다 늦습니다.");
        if (r.Reason != d.Reason)
            return E("Replay.stop_reason_mismatch", $"기록된 stop_reason {r.ReasonText}이(가) 재계산 {d.ReasonText}와 다릅니다.");
        if (r.CumulativePa != d.CumulativePa)
            return E("Replay.stop_cumulative_pa_mismatch", $"기록된 cumulative_pa {r.CumulativePa}이(가) 재계산 {d.CumulativePa}와 다릅니다.");
        if (r.BlockSize != d.BlockSize)
            return E("Replay.stop_block_size_mismatch", $"기록된 block_size {r.BlockSize}이(가) 재계산 {d.BlockSize}와 다릅니다.");
        if (recorded.PlateAppearances != derived.Aggregate.PlateAppearances)
            return E("Replay.plate_appearances_mismatch", $"기록된 plate_appearances {recorded.PlateAppearances}이(가) 재계산 {derived.Aggregate.PlateAppearances}와 다릅니다.");
        if (!recorded.Games.SequenceEqual(derived.Games.Select(RecordedGame.Of)))
            return E("Replay.result_games_mismatch", "기록된 경기별 상태·결과 해시·event_hash·rng_hash·error_code가 재실행 결과와 다릅니다.");
        if ((recorded.Requested, recorded.Completed, recorded.Failed, recorded.Pitches) != (derived.Requested, derived.CompletedCount, derived.FailedCount, derived.Aggregate.Pitches))
            return E("Replay.result_counts_mismatch", $"기록된 games_requested/completed/failed/pitches ({recorded.Requested}/{recorded.Completed}/{recorded.Failed}/{recorded.Pitches})가 재계산 "
                + $"({derived.Requested}/{derived.CompletedCount}/{derived.FailedCount}/{derived.Aggregate.Pitches})와 다릅니다.");
        if (recorded.RunStatus != RunReport.RunStatus(derived))
            return E("Replay.run_status_mismatch", $"기록된 run_status '{recorded.RunStatus}'가 재계산 '{RunReport.RunStatus(derived)}'와 다릅니다.");
        return recordedHash == derived.RunResultHash ? null : E("Replay.run_result_hash_mismatch", $"기록된 run_result_hash {recordedHash}이(가) 재계산 {derived.RunResultHash}(run-result-v2)와 다릅니다.");
    }
}
