using StarPlayer.Domain.Common;

namespace StarPlayer.SimHarness;

/// <summary>
/// The result of a run: every requested game appears (completed, failed or cancelled; nothing is dropped or hidden), the aggregate counters sum the COMPLETED games whose two statistic derivations
/// agreed (a failed game's counters stay on its own result), and <see cref="RunResultHash"/> identifies the whole outcome deterministically (no time, no machine). The metric report is evaluated
/// with the supplied policy; <see cref="EvaluationPolicy.Unapproved"/> keeps every official verdict <see cref="MetricStatus.NotEvaluated"/>.
/// <para>
/// A STOP-AWARE result (ADR-009, <see cref="Plan"/> set) keeps the whole approved plan in <see cref="Specs"/> (so <see cref="ConfigHash"/> is the whole-plan hash) and the executed leading games in
/// <see cref="Games"/>; it terminated by the rule only when <see cref="Stop"/> is set, and only then is <see cref="RunResultHash"/> a <c>run-result-v2</c> result hash (<see cref="ResultVersion"/>).
/// </para>
/// </summary>
public sealed record RunResult(
    RunConfig Config, IReadOnlyList<GameSpec> Specs, IReadOnlyList<GameRunResult> Games, MetricCounters Aggregate, Result<MetricsReport> Metrics, EvaluationPolicy Policy, string ConfigHash, string RunResultHash)
{
    /// <summary>The trusted stop parameters of a stop-aware run; null for a legacy / full-execution run.</summary>
    public StopPlan? Plan { get; init; }

    /// <summary>The <c>results.stop</c> record; set exactly when a stop-aware run terminated by the rule (SR-D7.2).</summary>
    public StopRecord? Stop { get; init; }

    /// <summary>How a stop-aware run ended (meaningless for a legacy run, which keeps its F-A/B semantics).</summary>
    public RunTermination Termination { get; init; } = RunTermination.Rule;

    /// <summary>The broken integrity check of an aborted stop-aware run (SR-D3.4).</summary>
    public Error? Abort { get; init; }

    public bool StopAware => Plan is not null;

    /// <summary><c>run-result-v1</c> for a legacy run, <c>run-result-v2</c> for a stop-aware run that terminated by the rule, and null for a cancelled or aborted stop-aware run (not a result, SR-D7.2).</summary>
    public string? ResultVersion => !StopAware ? RunResultText.V1 : Stop is not null ? RunResultText.V2 : null;

    public int Requested => Specs.Count;

    public int CompletedCount => Games.Count(g => g.Status == GameRunStatus.Completed);

    public int FailedCount => Games.Count(g => g.Status == GameRunStatus.Failed);

    public int CancelledCount => Games.Count(g => g.Status == GameRunStatus.Cancelled);

    /// <summary>Requested games the run never started (a cancelled run stops at the first cancelled game). Plan games after a rule stop are not "not started" but <see cref="UnexecutedAfterStop"/>.</summary>
    public int NotStartedCount => Stop is null ? Requested - Games.Count : 0;

    /// <summary>Plan games the stop rule did not require (never played, never observed; SR-D1.3).</summary>
    public int UnexecutedAfterStop => Stop is null ? 0 : Requested - Stop.ExecutedGames;

    /// <summary>
    /// Legacy: every requested game completed. Stop-aware (SR-D9.4): the run terminated by the rule and every game the rule required was run and completed.
    /// </summary>
    public bool AllCompleted => StopAware ? Termination == RunTermination.Rule && Stop is not null && CompletedCount == Stop.ExecutedGames : CompletedCount == Requested;

    /// <summary>
    /// True when the metric computation itself worked. It is NOT about the verdicts: <see cref="MetricStatus.NotEvaluated"/> and <see cref="MetricStatus.InsufficientSample"/> (and an official
    /// <see cref="MetricStatus.Fail"/>) are results of a working computation. False means an error such as <c>Metrics.target_missing</c> or <c>Metrics.counts_inconsistent</c>.
    /// </summary>
    public bool MetricsComputed => Metrics.IsSuccess;

    /// <summary>Every required game completed AND the metrics could be computed: the only state a command line reports as success.</summary>
    public bool Succeeded => AllCompleted && MetricsComputed;
}

public static class SimRunner
{
    /// <summary>
    /// Plays one game per seed, in order, with ids <c>game:{competition}:{cohort}-{index:D6}</c> (index from 1). The seeds are the caller's: this phase defines no official seed list, and no seed is
    /// chosen, skipped or replaced here. A failed game does not stop the run (its GameId and Seed are in the result); cancellation does.
    /// </summary>
    public static RunResult Run(RunConfig config, IReadOnlyList<ulong> seeds, EvaluationPolicy? policy = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(seeds);
        return RunSpecs(config, seeds.Select((seed, i) => config.Spec(i + 1, seed)).ToList(), policy, cancellation);
    }

    /// <summary>
    /// Plays exactly the given games (a replay's RECORDED index / game id / seed, already checked by <see cref="ReplayPlan.CheckAgainst"/>); nothing is regenerated. <paramref name="play"/> is a test seam
    /// (default: the production <see cref="GameExecutor"/>), as in <see cref="RunStopAware"/>.
    /// </summary>
    public static RunResult RunSpecs(RunConfig config, IReadOnlyList<GameSpec> specs, EvaluationPolicy? policy = null, CancellationToken cancellation = default, Func<GameSpec, GameRunResult>? play = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(specs);
        var results = new List<GameRunResult>();
        foreach (var spec in specs)
        {
            var result = play is null ? GameExecutor.Play(config, spec, cancellation) : play(spec);
            results.Add(result);
            if (result.Status == GameRunStatus.Cancelled)
                break;
        }

        var (aggregate, used, metrics, configHash) = Summarize(config, specs, results, policy);
        return new RunResult(config, specs, results, aggregate, metrics, used, configHash, Hashing.Sha256(RunResultText.V1Text(configHash, results.Select(g => g.ResultHash), aggregate.Canonical())));
    }

    /// <summary>
    /// ADR-009 stop-aware run of a trusted <paramref name="plan"/> (a run and its replay call exactly this): <see cref="StopRule.Execute"/> decides which leading games run. Every plan game must be exactly
    /// what the configuration's own rule makes of its index and seed. A cancelled or aborted run keeps its executed games as evidence and has no stop record; its <see cref="RunResult.RunResultHash"/> is
    /// only the <c>run-result-v1</c> fingerprint of that evidence, never a v2 result. <paramref name="play"/> is a test seam (default: the production <see cref="GameExecutor"/>).
    /// </summary>
    public static RunResult RunStopAware(RunConfig config, StopPlan plan, EvaluationPolicy? policy = null, CancellationToken cancellation = default, Func<Error?>? integrity = null,
        Func<GameSpec, GameRunResult>? play = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Games.FirstOrDefault(g => config.Spec(g.GameIndex, g.Seed) != g) is { } foreign)
            throw new ArgumentException($"계획의 경기 {foreign.GameIndex} '{foreign.GameId}'가 실행 설정이 만드는 GameId와 다릅니다.", nameof(plan));
        var outcome = StopRule.Execute(plan, play ?? (spec => GameExecutor.Play(config, spec, cancellation)), integrity);
        var (aggregate, used, metrics, configHash) = Summarize(config, plan.Games, outcome.Games, policy);
        var hashes = outcome.Games.Select(g => g.ResultHash);
        var hash = outcome.Stop is { } stop
            ? Hashing.Sha256(RunResultText.V2Text(configHash, plan.Games.Count, plan.PaTarget, stop, hashes, aggregate.Canonical()))
            : Hashing.Sha256(RunResultText.V1Text(configHash, hashes, aggregate.Canonical()));
        return new RunResult(config, plan.Games, outcome.Games, aggregate, metrics, used, configHash, hash)
        {
            Plan = plan, Stop = outcome.Stop, Termination = outcome.Termination, Abort = outcome.Abort,
        };
    }

    private static (MetricCounters Aggregate, EvaluationPolicy Used, Result<MetricsReport> Metrics, string ConfigHash) Summarize(RunConfig config, IReadOnlyList<GameSpec> specs,
        IReadOnlyList<GameRunResult> results, EvaluationPolicy? policy)
    {
        var aggregate = new MetricCounters();
        foreach (var game in results.Where(g => g.Completed))
            aggregate.Add(game.Counters);
        var used = policy ?? EvaluationPolicy.Unapproved;
        return (aggregate, used, MetricsEvaluator.Evaluate(aggregate, config.Cohort, config.Tuning, used), config.ConfigHash(specs));
    }
}
