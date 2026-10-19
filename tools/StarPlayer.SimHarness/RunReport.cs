using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StarPlayer.SimHarness;

/// <summary>Facts about the machine and the moment of a run. They are RECORDED in the manifest but belong to no hash: the same configuration and seeds give the same result hashes anywhere, any time.</summary>
public sealed record EnvironmentInfo(string? StartedUtc, string? FinishedUtc, double? ElapsedMilliseconds, string? OsDescription, string? RuntimeVersion, string? GitCommit, bool? GitDirty, long? PeakWorkingSetBytes = null)
{
    public static EnvironmentInfo Capture(DateTimeOffset started, DateTimeOffset finished, string? gitCommit, bool? gitDirty) =>
        new(started.ToString("O", CultureInfo.InvariantCulture), finished.ToString("O", CultureInfo.InvariantCulture), (finished - started).TotalMilliseconds,
            System.Runtime.InteropServices.RuntimeInformation.OSDescription, System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, gitCommit, gitDirty,
            System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64);
}

/// <summary>The manifest (what was run, pinned, so it can be run again), the summary (what happened) and the short text.</summary>
public static class RunReport
{
    public const string ManifestVersion = "simharness-manifest-v1";
    public const string SummaryVersion = "simharness-summary-v1";

    public const string StatusCompleted = "COMPLETED";
    public const string StatusGamesFailed = "GAMES_FAILED";
    public const string StatusMetricsError = "METRICS_ERROR";

    /// <summary>ADR-009 SR-D3.5 / SR-D3.6: a cancelled stop-aware run (no stop record, not a result).</summary>
    public const string StatusCancelled = "CANCELLED";

    /// <summary>ADR-009 SR-D3.4 / SR-D3.6: a stop-aware run aborted because trust in its inputs broke (no stop record, not a result).</summary>
    public const string StatusAborted = "ABORTED_INTEGRITY";

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The manifest. A legacy / full-execution run keeps the F-A/B shape unchanged. A stop-aware run (ADR-009) adds <c>seed_manifest</c> (the seed file it was planned from) and, in <c>results</c>,
    /// <c>run_status</c>, <c>run_result_version</c> (<c>run-result-v2</c> only when it terminated by the rule), <c>stop</c> (SR-D7.1, only then) and <c>games_unexecuted_after_stop</c>; a cancelled or
    /// aborted run records <c>null</c> for the version and the result hash, since it is not a result (SR-D7.2). The recorded values are evidence for comparison; a replay re-derives them.
    /// </summary>
    public static object Manifest(RunResult run, EnvironmentInfo? environment, string? tuningFileSha256 = null)
    {
        var legacy = LegacyManifest(run, environment, tuningFileSha256);
        if (run.Plan is not { } plan)
            return legacy;
        var node = JsonSerializer.SerializeToNode(legacy, Json)!.AsObject();
        var seed = new JsonObject
        {
            ["path"] = plan.SeedManifestPath, ["sha256"] = plan.SeedManifestSha256, ["family"] = plan.Family, ["block_size_games"] = plan.BlockSize, ["pa_target"] = plan.PaTarget,
            ["planned_games"] = plan.Games.Count,
        };
        var withSeed = new JsonObject();
        foreach (var (name, value) in node.ToList())
        {
            node.Remove(name);
            withSeed[name] = value;
            if (name == "games")
                withSeed["seed_manifest"] = seed;
        }

        var results = withSeed["results"]!.AsObject();
        results["run_status"] = RunStatus(run);
        results["run_result_version"] = run.ResultVersion;
        results["games_unexecuted_after_stop"] = run.UnexecutedAfterStop;
        if (run.Stop is { } stop)
            results["stop"] = new JsonObject { ["executed_games"] = stop.ExecutedGames, ["stop_reason"] = stop.ReasonText, ["cumulative_pa"] = stop.CumulativePa, ["block_size"] = stop.BlockSize };
        else
            results["run_result_hash"] = null;
        if (run.Abort is { } abort)
            results["abort_code"] = abort.Code;
        return withSeed;
    }

    private static object LegacyManifest(RunResult run, EnvironmentInfo? environment, string? tuningFileSha256)
    {
        var c = run.Config;
        return new
        {
            manifest_version = ManifestVersion,
            run = new { run_id = c.RunId, mode = c.Mode, cohort_id = c.Cohort.CohortId, cohort_kind = c.Cohort.Kind.ToString(), input_schema_version = RunConfig.InputSchemaVersion, config_hash = run.ConfigHash },
            engine = new
            {
                probability_model_version = c.Execution.ProbabilityModelVersion,
                tuning_version = c.Execution.TuningVersion,
                tuning_hash = c.Execution.TuningHash,
                embedded_tuning_file_sha256 = HarnessData.ResourceHash(HarnessData.TuningResource),
                tuning_source = tuningFileSha256 is null ? "embedded" : "file",
                tuning_file_sha256 = tuningFileSha256,
                rules_profile_id = c.Rules.ProfileId,
                rules_version = c.Rules.Version,
                rules_status = c.Rules.Status.ToString(),
                rules_hash = c.Rules.ContentHash,
                embedded_rules_file_sha256 = HarnessData.ResourceHash(HarnessData.RulesResource),
                called_pitch_mode = c.Rules.CalledPitchMode?.ToString(),
                advancement_policy_version = c.AdvancementPolicyVersion,
                park = new { stadium = c.Park.Stadium.Value, profile = c.Park.Profile.Value, version = c.Park.Version, home_runs_permille = c.Park.HomeRuns.Permille, hits_permille = c.Park.Hits.Permille, extra_base_hits_permille = c.Park.ExtraBaseHits.Permille },
                player_input_set_content_version = c.Cohort.InputSet.ContentVersion,
                player_input_set_id = c.Cohort.InputSet.Id,
                cohort_inputs_hash = c.Cohort.InputsHash,
                policy_id = c.Policy.Id,
                event_budget = c.EventBudget,
                competition_key = c.CompetitionKey,
            },
            games = run.Specs.Select(s => new { index = s.GameIndex, game_id = s.GameId, seed = s.Seed }).ToList(),
            results = new
            {
                games_requested = run.Requested,
                games_completed = run.CompletedCount,
                games_failed = run.FailedCount,
                games_cancelled = run.CancelledCount,
                games_not_started = run.NotStartedCount,
                plate_appearances = run.Aggregate.PlateAppearances,
                pitches = run.Aggregate.Pitches,
                committed_events = run.Games.Sum(g => (long)g.CommittedEvents),
                end_kinds = run.Games.Where(g => g.EndKind != GameEndKind.None).GroupBy(g => g.EndKind.ToString()).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()),
                error_codes = run.Games.Where(g => g.Failure is not null).Select(g => g.Failure!.Code).Distinct().Order(StringComparer.Ordinal).ToList(),
                run_result_hash = run.RunResultHash,
                games = run.Games.Select(g => new
                {
                    game_id = g.Game.GameId, seed = g.Game.Seed, status = g.Status.ToString(), end_kind = g.EndKind.ToString(), event_hash = g.EventHash, rng_hash = g.RngHash, result_hash = g.ResultHash,
                    error_code = g.Failure?.Code,
                }).ToList(),
            },
            environment_not_hashed = environment,
        };
    }

    /// <summary>
    /// COMPLETED, GAMES_FAILED (a game did not complete) or METRICS_ERROR (the games completed, the metrics could not be computed). A verdict of NOT_EVALUATED is none of the errors. A stop-aware run that
    /// was cancelled is CANCELLED and one aborted for integrity is ABORTED_INTEGRITY (never COMPLETED, never presented as a rule stop); a rule stop with a failed game is GAMES_FAILED (SR-D9.2).
    /// </summary>
    public static string RunStatus(RunResult run) =>
        run.StopAware && run.Termination == RunTermination.AbortedIntegrity ? StatusAborted
        : run.StopAware && run.Termination == RunTermination.Cancelled ? StatusCancelled
        : !run.AllCompleted ? StatusGamesFailed : run.MetricsComputed ? StatusCompleted : StatusMetricsError;

    public static object Summary(RunResult run) => new
    {
        summary_version = SummaryVersion,
        run_status = RunStatus(run),
        run_id = run.Config.RunId,
        config_hash = run.ConfigHash,
        run_result_hash = run.ResultVersion is null ? null : run.RunResultHash,
        run_result_version = run.ResultVersion,
        stop = run.Stop is { } stop ? new { executed_games = stop.ExecutedGames, stop_reason = stop.ReasonText, cumulative_pa = stop.CumulativePa, block_size = stop.BlockSize } : null,
        official_verdict = "NOT_EVALUATED",
        official_verdict_note = "F-A/B: 최소 표본 기준과 Virtual League 코호트가 승인되지 않아 공식 지표 판정은 하지 않습니다. 100,000 PA와 100경기 Gate는 실행하지 않았습니다.",
        games = run.Games.Select(g => new
        {
            game_id = g.Game.GameId,
            seed = g.Game.Seed,
            status = g.Status.ToString(),
            outcome = g.Outcome?.ToString(),
            away_runs = g.AwayRuns,
            home_runs = g.HomeRuns,
            final_inning = g.FinalInning,
            final_half = g.FinalHalf.ToString(),
            end_kind = g.EndKind.ToString(),
            plate_appearances = g.Counters.PlateAppearances,
            pitches = g.Counters.Pitches,
            committed_events = g.CommittedEvents,
            event_hash = g.EventHash,
            rng_hash = g.RngHash,
            result_hash = g.ResultHash,
            error_code = g.Failure?.Code,
            elapsed_ms = Math.Round(g.ElapsedMilliseconds, 1),
        }).ToList(),
        metrics = run.Metrics.IsSuccess
            ? new
            {
                cohort_id = run.Metrics.Value.CohortId,
                cohort_kind = run.Metrics.Value.CohortKind,
                error_code = (string?)null,
                error = (string?)null,
                items = run.Metrics.Value.Metrics.Select(m => new
                {
                    f_id = m.FId, metric_id = m.TargetId, numerator = m.Numerator, denominator = m.Denominator, numerator_count = m.NumeratorCount, denominator_count = m.DenominatorCount,
                    measured_ppm = m.MeasuredPpm, target_low_ppm = m.TargetLowPpm, target_high_ppm = m.TargetHighPpm, deviation_ppm = m.DeviationPpm,
                    recommended_cohort = m.RecommendedCohort, evaluated_cohort = m.EvaluatedCohortId, sample_rule = m.SampleRule, status = m.Status.ToString(), reason = m.Reason,
                    wilson_95_diagnostic_ppm = m.Wilson is { } w ? new[] { w.LowPpm, w.HighPpm } : null,
                }).ToList<object>(),
            }
            : new { cohort_id = run.Config.Cohort.CohortId, cohort_kind = run.Config.Cohort.Kind.ToString(), error_code = (string?)run.Metrics.Error!.Value.Code, error = (string?)$"{run.Metrics.Error.Value.Code}: {run.Metrics.Error.Value.Message}", items = new List<object>() },
        cross_check = run.Games.Select(g => new
        {
            game_id = g.Game.GameId, seed = g.Game.Seed, passed = g.CrossCheck?.Passed, mismatches = g.CrossCheck?.Mismatches.Select(m => new { m.Name, from_events = m.FromEvents, from_box_score = m.FromBoxScore }).ToList(),
        }).ToList(),
        failures = run.Games.Where(g => g.Failure is not null).Select(g => new
        {
            game_id = g.Game.GameId, seed = g.Game.Seed, status = g.Status.ToString(), code = g.Failure!.Code, message = g.Failure.Message, stage = g.Failure.Stage, event_sequence = g.Failure.EventSequence,
            revision = g.Failure.Revision, rng_hash = g.Failure.RngHash, last_receipts = g.Failure.LastReceipts,
        }).ToList(),
        calibration_evaluation = run.StopAware ? Evaluation(run) : null,
        regulation_runs_diagnostic = run.StopAware ? RegulationDiagnostic(run) : null,
        pzl_checks = new
        {
            note = "이 실행 하나의 Pitch Zone Lite 검사 결과(파생 데이터, 어떤 Hash에도 없음). 공식 절 PASS는 F-C3 공식 실행 증거가 필요합니다(ADR-008 FC-D8·FC-D12). #6은 Infrastructure.Tests의 JSON 왕복 시험입니다.",
            checks = PzlChecks.Evaluate(run).Select(c => new { id = c.Id, clause = c.Clause, status = c.Status, reason = c.Reason, counts = c.Counts }).ToList(),
        },
    };

    /// <summary>
    /// The fc2-dry σ_g measurement (proposal §6.5): the game observation of FC-D4.2 (innings 1-8 runs / 16) over the completed games whose line score cross-checked. A measurement only: the σ_g pilot rule
    /// (maximum × 1.2, rounding to 50 games) and the per-cell sample size are Final Execution Freeze candidates (ADR-008 FC-D4.7, N-1) and are not computed here. Derived data: in no hash.
    /// </summary>
    private static object RegulationDiagnostic(RunResult run)
    {
        var completed = run.Games.Where(g => g.Completed).ToList();
        var sample = CellSample.Of(completed.Where(g => g.Regulation is { Ok: true }).Select(g => g.Regulation!.Total));
        return new
        {
            note = "진단 측정값(판정 아님). σ_g 파일럿 규칙(최댓값×1.2, 50경기 올림)과 셀당 표본 수는 Final Execution Freeze 후보이며 계산하지 않습니다.",
            games = sample.Games, regulation_errors = completed.Count(g => g.Regulation is not { Ok: true }), total_runs_innings_1_8 = sample.TotalRuns, sum_of_squares = sample.SumOfSquares,
            mean_runs_per_half_inning = sample.Games > 0 ? sample.Mean : (double?)null, sigma_g_game_observation_sd = sample.Variance is { } v ? Math.Sqrt(v) : (double?)null,
        };
    }

    /// <summary>The ADR-008 stratified evaluation of a stop-aware run under <see cref="EvaluationPolicy.FcEvaluationV1"/> (a dry run's gate is always NOT_EVALUATED). Derived data: in no hash.</summary>
    private static object Evaluation(RunResult run)
    {
        var evaluated = StratifiedEvaluator.Evaluate(run, EvaluationPolicy.FcEvaluationV1);
        if (!evaluated.IsSuccess)
        {
            var (gate, reason) = StratifiedEvaluator.GateOfFailedEvaluation(run, EvaluationPolicy.FcEvaluationV1, evaluated.Error!.Value);
            return new { error_code = evaluated.Error.Value.Code, error = evaluated.Error.Value.Message, gate_status = GateText.Of(gate), gate_reason = reason };
        }

        var e = evaluated.Value;
        return new
        {
            policy_id = e.PolicyId, policy_hash = e.PolicyHash, stratum = e.StratumId, family = e.Family, official = e.Official, cohort_id = e.CohortId, cohort_inputs_hash = e.CohortInputsHash,
            rules_profile_id = e.RulesProfileId, config_hash = e.ConfigHash, run_result_hash = e.RunResultHash, completed_games = e.CompletedGames, failed_games = e.FailedGames, valid_pa = e.ValidPa,
            gate_status = GateText.Of(e.Gate), gate_reason = e.GateReason,
            shares = new
            {
                same_games = e.Shares.SameGames, opposite_games = e.Shares.OppositeGames, mixed_games = e.Shares.MixedGames, same_pa = e.Shares.SamePa, opposite_pa = e.Shares.OppositePa,
                same_pitches = e.Shares.SamePitches, opposite_pitches = e.Shares.OppositePitches, same_swings = e.Shares.SameSwings, opposite_swings = e.Shares.OppositeSwings,
            },
            metrics = e.Metrics.Select(m => new
            {
                f_id = m.FId, metric_id = m.TargetId, method = m.Method, official = m.Official, numerator = m.Numerator, denominator = m.Denominator, numerator_same = m.NumeratorSame,
                denominator_same = m.DenominatorSame, numerator_opposite = m.NumeratorOpposite, denominator_opposite = m.DenominatorOpposite, measured_ppm = m.MeasuredPpm,
                target_low_ppm = m.TargetLowPpm, target_high_ppm = m.TargetHighPpm, min_denominator = m.MinDenominator, min_same = m.MinSame, min_opposite = m.MinOpposite,
                status = GateText.Of(m.Status), reason = m.Reason,
                diagnostics_not_verdicts = new
                {
                    observed_ppm = m.Diagnostics.ObservedPpm, standard_error_ppm_game_cluster = m.Diagnostics.StandardErrorPpm, deff_game_cluster = m.Diagnostics.DeffGameCluster,
                    deff_batch_means = m.Diagnostics.DeffBatchMeans, batch_games = m.Diagnostics.BatchGames,
                },
            }).ToList(),
        };
    }

    public static string Text(RunResult run)
    {
        var t = new StringBuilder();
        var c = run.Config;
        t.AppendLine($"Star Player SimHarness - run {c.RunId} (mode {c.Mode}, cohort {c.Cohort.CohortId}/{c.Cohort.Kind}, {c.Execution.ProbabilityModelVersion}/{c.Execution.TuningVersion}, rules {c.Rules.ProfileId})");
        t.AppendLine($"games: {run.Requested} requested, {run.CompletedCount} completed, {run.FailedCount} failed, {run.CancelledCount} cancelled, {run.NotStartedCount} not started");
        if (run.Plan is { } plan)
            t.AppendLine(run.Stop is { } stop
                ? $"stop: {stop.ReasonText} after {stop.ExecutedGames}/{plan.Games.Count} games (block {stop.BlockSize}, valid PA {stop.CumulativePa} / target {plan.PaTarget}), {run.UnexecutedAfterStop} plan games not executed  status {RunStatus(run)}  {run.ResultVersion}"
                : $"stop: none - {RunStatus(run)}{(run.Abort is { } a ? $" ({a.Code})" : "")}; not a result (no stop record, no run-result-v2)");
        t.AppendLine($"PA {run.Aggregate.PlateAppearances}  pitches {run.Aggregate.Pitches}  events {run.Games.Sum(g => (long)g.CommittedEvents)}  config {Short(run.ConfigHash)}  result {Short(run.RunResultHash)}");
        foreach (var g in run.Games)
        {
            t.Append($"  {g.Game.GameIndex:D3} {g.Game.GameId} seed={g.Game.Seed} {g.Status}");
            t.AppendLine(g.Status == GameRunStatus.Completed || g.Outcome is not null
                ? $" away {g.AwayRuns} - home {g.HomeRuns} ({g.FinalInning}{(g.FinalHalf == StarPlayer.Domain.Baseball.Game.InningHalf.Top ? "T" : "B")}) {g.EndKind} {g.Outcome} PA={g.Counters.PlateAppearances} pitches={g.Counters.Pitches} events={g.CommittedEvents} event={Short(g.EventHash)} rng={Short(g.RngHash)}"
                : "");
            if (g.Failure is { } f)
                t.AppendLine($"      FAILURE {f.Code} @{f.Stage} seq={f.EventSequence}: {f.Message}  (replay with GameId {g.Game.GameId}, seed {g.Game.Seed})");
        }

        t.AppendLine("metrics (official verdicts are NOT evaluated in F-A/B; the numbers are measurements, not results):");
        if (!run.Metrics.IsSuccess)
            t.AppendLine($"  ERROR {run.Metrics.Error!.Value.Code}: {run.Metrics.Error.Value.Message}");
        else
            foreach (var m in run.Metrics.Value.Metrics)
                t.AppendLine($"  {m.FId} {m.TargetId,-22} {m.NumeratorCount}/{m.DenominatorCount} = {(m.MeasuredPpm is { } v ? v.ToString(CultureInfo.InvariantCulture) : "n/a")} ppm  target {m.TargetLowPpm}..{m.TargetHighPpm}  dev {(m.DeviationPpm is { } d ? d.ToString(CultureInfo.InvariantCulture) : "n/a")}  {m.Status}  [{m.RecommendedCohort} recommended]");
        if (run.StopAware && StratifiedEvaluator.Evaluate(run, EvaluationPolicy.FcEvaluationV1) is { } evaluation)
        {
            if (evaluation.IsSuccess)
                t.AppendLine($"calibration evaluation ({evaluation.Value.PolicyId}, stratum {evaluation.Value.StratumId ?? "-"}, official {evaluation.Value.Official}): gate {GateText.Of(evaluation.Value.Gate)} - {evaluation.Value.GateReason}");
            else
            {
                var (gate, reason) = StratifiedEvaluator.GateOfFailedEvaluation(run, EvaluationPolicy.FcEvaluationV1, evaluation.Error!.Value);
                t.AppendLine($"calibration evaluation ({EvaluationPolicy.FcEvaluationV1.Id}): gate {GateText.Of(gate)} - {reason}");
            }
        }
        var bad = run.Games.Where(g => g.CrossCheck is { Passed: false }).ToList();
        t.AppendLine(bad.Count == 0 ? "cross-check (events vs BoxScoreBuilder): all completed games match" : $"cross-check MISMATCH in {string.Join(", ", bad.Select(g => $"{g.Game.GameId} seed {g.Game.Seed}"))}");
        t.AppendLine(PzlChecks.Text(PzlChecks.Evaluate(run)));
        return t.ToString();
    }

    public static IReadOnlyList<string> Write(RunResult run, EnvironmentInfo? environment, string directory, string? tuningFileSha256 = null)
    {
        Directory.CreateDirectory(directory);
        var files = new List<string>();
        void Put(string name, string content)
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            files.Add(path);
        }

        Put("manifest.json", JsonSerializer.Serialize(Manifest(run, environment, tuningFileSha256), Json));
        Put("summary.json", JsonSerializer.Serialize(Summary(run), Json));
        Put("summary.txt", Text(run));
        return files;
    }

    private static string Short(string hash) => hash.Length > 12 ? hash[..12] : hash;
}
