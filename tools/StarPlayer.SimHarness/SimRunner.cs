using System.Text;
using StarPlayer.Domain.Common;

namespace StarPlayer.SimHarness;

/// <summary>
/// The result of a run: every requested game appears (completed, failed or cancelled; nothing is dropped or hidden), the aggregate counters sum the COMPLETED games whose two statistic derivations
/// agreed (a failed game's counters stay on its own result), and <see cref="RunResultHash"/> identifies the whole outcome deterministically (no time, no machine). The metric report is evaluated
/// with the supplied policy; <see cref="EvaluationPolicy.Unapproved"/> keeps every official verdict <see cref="MetricStatus.NotEvaluated"/>.
/// </summary>
public sealed record RunResult(
    RunConfig Config, IReadOnlyList<GameSpec> Specs, IReadOnlyList<GameRunResult> Games, MetricCounters Aggregate, Result<MetricsReport> Metrics, EvaluationPolicy Policy, string ConfigHash, string RunResultHash)
{
    public int Requested => Specs.Count;

    public int CompletedCount => Games.Count(g => g.Status == GameRunStatus.Completed);

    public int FailedCount => Games.Count(g => g.Status == GameRunStatus.Failed);

    public int CancelledCount => Games.Count(g => g.Status == GameRunStatus.Cancelled);

    /// <summary>Requested games the run never started (a cancelled run stops at the first cancelled game).</summary>
    public int NotStartedCount => Requested - Games.Count;

    public bool AllCompleted => CompletedCount == Requested;

    /// <summary>
    /// True when the metric computation itself worked. It is NOT about the verdicts: <see cref="MetricStatus.NotEvaluated"/> and <see cref="MetricStatus.InsufficientSample"/> (and an official
    /// <see cref="MetricStatus.Fail"/>) are results of a working computation. False means an error such as <c>Metrics.target_missing</c> or <c>Metrics.counts_inconsistent</c>.
    /// </summary>
    public bool MetricsComputed => Metrics.IsSuccess;

    /// <summary>Every requested game completed AND the metrics could be computed: the only state a command line reports as success.</summary>
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
        var specs = seeds.Select((seed, i) => config.Spec(i + 1, seed)).ToList();
        var results = new List<GameRunResult>();
        foreach (var spec in specs)
        {
            var result = GameExecutor.Play(config, spec, cancellation);
            results.Add(result);
            if (result.Status == GameRunStatus.Cancelled)
                break;
        }

        var aggregate = new MetricCounters();
        foreach (var game in results.Where(g => g.Completed))
            aggregate.Add(game.Counters);
        var used = policy ?? EvaluationPolicy.Unapproved;
        var metrics = MetricsEvaluator.Evaluate(aggregate, config.Cohort, config.Tuning, used);
        var configHash = config.ConfigHash(specs);
        var text = new StringBuilder("run-result-v1\n").Append(configHash).Append('\n');
        foreach (var game in results)
            text.Append(game.ResultHash).Append('\n');
        text.Append(aggregate.Canonical());
        return new RunResult(config, specs, results, aggregate, metrics, used, configHash, Hashing.Sha256(text.ToString()));
    }
}
