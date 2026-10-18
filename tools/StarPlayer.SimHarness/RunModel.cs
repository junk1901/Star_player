using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.SimHarness;

/// <summary>
/// The choice a plate appearance starts with (decision D1): one approach for the whole plate appearance, or an explicit one-pitch <c>Take</c>. The policy sees only the public game state,
/// never a rating, a hidden tendency or a probability.
/// </summary>
public sealed record PaDecision(bool Take, ApproachKind Approach)
{
    public static PaDecision Choose(ApproachKind approach) => new(false, approach);

    public static PaDecision TakeOne { get; } = new(true, ApproachKind.Normal);
}

/// <summary>Chooses the next player action. It must be a pure function of the state it is given, so a replay needs no recorded decisions.</summary>
public interface IGamePolicy
{
    /// <summary>A stable id recorded in the manifest and part of the run configuration hash.</summary>
    string Id { get; }

    /// <summary>Called when a plate appearance is new or back at a decision point after a <c>Take</c>.</summary>
    PaDecision AtPlateAppearance(GameState state);

    PitchIntent IntentFor(GameState state);
}

/// <summary>
/// The default policy: <see cref="ApproachKind.Normal"/> for every plate appearance and <see cref="PitchIntent.Balanced"/> for every pitch, no <c>Take</c> and no substitution. It exists to prove the
/// engine legal, not to win: it reads no rating, does not adapt to the score, and nothing in it is tuned toward a target. That no single policy is dominant is NOT shown by it.
/// </summary>
public sealed class NormalBalancedPolicy : IGamePolicy
{
    public string Id => "normal-balanced-v1";

    public PaDecision AtPlateAppearance(GameState state) => PaDecision.Choose(ApproachKind.Normal);

    public PitchIntent IntentFor(GameState state) => PitchIntent.Balanced;
}

public enum GameRunStatus { Completed, Failed, Cancelled }

/// <summary>How a COMPLETED game ended, from its final state and its rules (never a budget or a failure).</summary>
public enum GameEndKind { None, Regulation, ExtraInnings, TieAtExtraInningsLimit }

public sealed record ReceiptRef(string EventId, string Kind, string Fingerprint);

/// <summary>A failed or cancelled game's reproduction record: where it stopped and the last receipts, so the game can be replayed from its GameId, Seed and the run's pinned inputs.</summary>
public sealed record RunFailure(string Code, string Message, string Stage, int EventSequence, long Revision, string RngHash, IReadOnlyList<ReceiptRef> LastReceipts);

public sealed record GameSpec(int GameIndex, string GameId, ulong Seed);

public sealed record GameRunResult(
    GameSpec Game, GameRunStatus Status, RunFailure? Failure, GameOutcome? Outcome, GameEndKind EndKind, int AwayRuns, int HomeRuns, int FinalInning, InningHalf FinalHalf,
    int CommittedEvents, MetricCounters Counters, CrossCheckResult? CrossCheck, string EventHash, string RngHash, string ResultHash, double ElapsedMilliseconds)
{
    public bool Completed => Status == GameRunStatus.Completed;

    /// <summary>
    /// The strategy-matrix observation of an engine-final game (ADR-008 FC-D4.2): the runs of the 16 half-innings of innings 1-8, or the error of its line-score cross-check; null for a game that never
    /// reached a final state. It is a pure function of the committed events, so it is NOT part of <see cref="ResultHash"/> (<c>game-result-v1</c> is unchanged) and a replay compares it by re-execution.
    /// </summary>
    public RegulationRuns? Regulation { get; init; }
}

/// <summary>
/// Everything that pins a run. Created by <see cref="Create"/>, which checks the combination and builds the game execution contract from the profiles themselves (nothing is typed twice); the
/// executor re-checks the combination on every game, so a hand-edited record cannot start a game on inputs the contract does not name.
/// </summary>
public sealed record RunConfig(
    string RunId, string Mode, CohortInputs Cohort, GameRuleSnapshot Rules, TuningProfile Tuning, string AdvancementPolicyVersion, ParkFactorSnapshot Park, int EventBudget, IGamePolicy Policy,
    string CompetitionKey, GameExecution Execution)
{
    public const int MaxEventBudget = GameEventIds.MaxSequence;

    public const string InputSchemaVersion = "simharness-input-v1";

    public static Result<RunConfig> Create(string? runId, string? mode, CohortInputs? cohort, GameRuleSnapshot? rules, TuningProfile? tuning, ParkFactorSnapshot? park, string? advancementPolicy = null,
        int eventBudget = 3000, IGamePolicy? policy = null, string competitionKey = "sim")
    {
        static Result<RunConfig> Fail(string code, string message) => Result<RunConfig>.Failure(new Error(code, message));
        if (string.IsNullOrWhiteSpace(runId) || runId.Length > 64 || runId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')))
            return Fail("Harness.run_id_invalid", "실행 ID는 영문·숫자·'_'·'-'·'.'로 된 1~64자여야 합니다.");
        if (string.IsNullOrWhiteSpace(mode))
            return Fail("Harness.mode_invalid", "실행 모드가 비어 있습니다.");
        if (cohort is null || rules is null || tuning is null || park is null)
            return Fail("Harness.config_incomplete", "코호트, 규칙, 튜닝, 구장 스냅샷이 모두 필요합니다.");
        if (tuning.Model.Version.Value != ProbabilityModelVersion.V4Text)
            return Fail("Harness.model_unsupported", $"하네스는 확률 모델 '{ProbabilityModelVersion.V4Text}'만 실행합니다(받은 값: '{tuning.Model.Version.Value}').");
        if (cohort.InputSet.ContentVersion != PlayerInputSet.ContentV3)
            return Fail("Harness.input_set_version", $"선수 입력 집합은 콘텐츠 {PlayerInputSet.ContentV3}여야 합니다(받은 값: {cohort.InputSet.ContentVersion}).");
        if (eventBudget < 1 || eventBudget > MaxEventBudget)
            return Fail("Harness.event_budget_invalid", $"이벤트 예산은 1~{MaxEventBudget}이어야 합니다.");
        var gameIds = GameIds.Create(competitionKey, $"{cohort.CohortId}-{GameEventIds.MaxSequence.ToString("D6", CultureInfo.InvariantCulture)}");   // the longest id this configuration can make
        if (!gameIds.IsSuccess)
            return Fail(gameIds.Error!.Value.Code, gameIds.Error.Value.Message);
        var policyVersion = advancementPolicy ?? AdvancementPolicy.VersionV2;
        var execution = new GameExecution(tuning.Model.Version.Value, tuning.Tuning.Value, tuning.ContentHash, policyVersion, park, cohort.InputSet.Id);
        return Result<RunConfig>.Success(new RunConfig(runId, mode, cohort, rules, tuning, policyVersion, park, eventBudget, policy ?? new NormalBalancedPolicy(), competitionKey, execution));
    }

    /// <summary>The game id of the <paramref name="index"/>-th game of this configuration: a pure function of the competition key, the cohort and the index (no clock, no counter, no RNG).</summary>
    public GameSpec Spec(int index, ulong seed)
    {
        var id = GameIds.Create(CompetitionKey, $"{Cohort.CohortId}-{index.ToString("D6", CultureInfo.InvariantCulture)}");
        return new GameSpec(index, id.IsSuccess ? id.Value : throw new InvalidOperationException($"경기 ID를 만들 수 없습니다: {id.Error!.Value.Code}"), seed);
    }

    /// <summary>The engine-facing check of the pins: the execution contract must name exactly the tuning, advancement policy, park and input set this record carries.</summary>
    public Error? CheckPins()
    {
        if (Execution.ProbabilityModelVersion != Tuning.Model.Version.Value || Execution.TuningVersion != Tuning.Tuning.Value || Execution.TuningHash != Tuning.ContentHash)
            return new Error("Harness.pin_tuning_mismatch", "경기 실행 계약의 모델·튜닝이 실행 설정의 튜닝과 다릅니다.");
        if (Execution.PlayerInputsId != Cohort.InputSet.Id || Cohort.InputSet.ContentVersion != PlayerInputSet.ContentV3)
            return new Error("Harness.pin_input_set_mismatch", "경기 실행 계약의 선수 입력 집합 식별자가 코호트의 집합(콘텐츠 v3)과 다릅니다.");
        if (Execution.AdvancementPolicyVersion != AdvancementPolicyVersion || !Equals(Execution.Park, Park))
            return new Error("Harness.pin_policy_or_park_mismatch", "경기 실행 계약의 진루 정책 또는 구장 스냅샷이 실행 설정과 다릅니다.");
        return null;
    }

    /// <summary>The deterministic identity of the whole configuration (everything that can change a result except the run id and the mode label).</summary>
    public string ConfigHash(IEnumerable<GameSpec> games)
    {
        var text = new StringBuilder();
        text.Append(InputSchemaVersion).Append('\n')
            .Append("model=").Append(Execution.ProbabilityModelVersion).Append("|tuning=").Append(Execution.TuningVersion).Append('|').Append(Execution.TuningHash).Append('\n')
            .Append("rules=").Append(Rules.ProfileId).Append('|').Append(Rules.Version).Append('|').Append(Rules.ContentHash).Append('\n')
            .Append("advancement=").Append(AdvancementPolicyVersion).Append('\n')
            .Append("park=").Append(Park.Stadium.Value).Append('|').Append(Park.Profile.Value).Append('|').Append(Park.Version).Append('|')
            .Append(Park.HomeRuns.Permille).Append('|').Append(Park.Hits.Permille).Append('|').Append(Park.ExtraBaseHits.Permille).Append('\n')
            .Append("inputs=").Append(Cohort.InputSet.ContentVersion).Append('|').Append(Cohort.InputSet.Id).Append('|').Append(Cohort.InputsHash).Append('\n')
            .Append("cohort=").Append(Cohort.Kind).Append('|').Append(Cohort.CohortId).Append('\n')
            .Append("policy=").Append(Policy.Id).Append("|budget=").Append(EventBudget).Append("|competition=").Append(CompetitionKey).Append('\n');
        foreach (var game in games)
            text.Append(game.GameIndex).Append('|').Append(game.GameId).Append('|').Append(game.Seed).Append('\n');
        return Hashing.Sha256(text.ToString());
    }
}

public static class Hashing
{
    public static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
