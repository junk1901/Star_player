using System.Globalization;

namespace StarPlayer.SimHarness.Evidence;

/// <summary>
/// The approval references every official document carries (§2.2-§2.3) and the artifact references written after the lower documents (§21 DAG). They come from the trusted approval record (T06a)
/// of the producer, never from the run itself.
/// </summary>
public sealed record EvidenceContext(
    string ValidationSetId, string ApprovalRecordId, string ApprovalRecordSha256, string FreezeId, string BuildProvenanceId, string? CombinationPolicyClauseId = null)
{
    public JObject Approvals() => J.O(("approval_record_id", J.S(ApprovalRecordId)), ("approval_record_sha256", J.S(ApprovalRecordSha256)), ("freeze_id", J.S(FreezeId)));
}

/// <summary>
/// T03: the closed Family (§3) and Dominance (§4) documents projected from a COMPLETE official result. Nothing is recomputed differently: the family verdicts are <see cref="FamilyOfficial.Evaluate"/>'s,
/// a calibration unit's metrics are <see cref="StratifiedEvaluator.Evaluate"/>'s, the dominance evaluation is the result's own <see cref="DominanceEvaluation"/>, the run status is
/// <see cref="RunReport.RunStatus"/> and every hash is the existing one. An incomplete result has no official document (R-4, N04): the DRAFT index stays its only record.
/// </summary>
public static class OfficialDocuments
{
    public static JObject SchemaHeader(EvidenceKind kind) => J.O(("id", J.S(EvidenceShapes.SchemaId(kind))), ("version", J.S("1")),
        ("contracts", J.O(EvidenceShapes.ContractKeys.Select(k => (k, (JNode)J.S(EvidenceShapes.SupportedContracts[k]))).ToArray())));

    public static JObject Execution(string evidenceId, EvidenceContext ctx, string runId, string family, string gitSha) =>
        J.O(("evidence_id", J.S(evidenceId)), ("validation_set_id", J.S(ctx.ValidationSetId)), ("run_id", J.S(runId)), ("family", J.S(family)), ("git_commit_sha", J.S(gitSha)),
            ("build_provenance_id", J.S(ctx.BuildProvenanceId)));

    public static JObject Family(FamilyOfficialResult r, EvidenceContext ctx, IReadOnlyList<JObject> artifacts)
    {
        if (!r.Complete)
            throw EvidenceCodes.Fail(EvidenceCodes.NotCertifiable, "Evidence.run_not_complete", "", "projection", $"완료되지 않은 실행({r.RunStatus})은 공식 문서를 만들지 않습니다(R-4).");
        return Family(r.Preflight.Plan, r.RunId, r.Units.Select(u => (u.Run, u.Plan.Report.Attestation!)).ToList(), r.Evaluation!, ctx, artifacts);
    }

    /// <summary>The same projection from runs in plan order and their attestations (the producer's complete result, or the verifier's re-execution of it, T06b).</summary>
    public static JObject Family(OfficialFamilyPlan plan, string runId, IReadOnlyList<(RunResult Run, OfficialAttestation Attestation)> units, FamilyEvaluation evaluation, EvidenceContext ctx,
        IReadOnlyList<JObject> artifacts)
    {
        var family = plan.Family;
        var vs = ctx.ValidationSetId;
        var rows = units.Select((u, i) => J.O(("unit_id", J.S(plan.Units[i].Id)), ("inputs", Inputs(u.Run, u.Attestation)), ("technical", Technical(u.Run, dominance: false)),
            ("evaluation", UnitEvaluation(plan, u.Run, evaluation.Units[i])))).ToList();
        return J.O(
            ("schema", SchemaHeader(EvidenceKind.Family)),
            ("execution", Execution(EvidenceIds.Result(vs, family), ctx, runId, family, units[0].Attestation.GitCommitSha)),
            ("approvals", ctx.Approvals()),
            ("plan", J.O(("unit_ids", J.A(plan.Units.Select(u => (JNode)J.S(u.Id)))), ("combination_policy_clause_id", J.S(ctx.CombinationPolicyClauseId ?? throw new ArgumentException("결합 정책 조항 ID가 필요합니다(B-6c/B-6d).", nameof(ctx)))))),
            ("units", J.A(rows)),
            ("aggregate_evaluation", Gate(GateText.Of(evaluation.Gate), evaluation.Reason)),
            ("additional_evidence", Additional(vs, family, plan.Units.Select(u => u.Id), family == "fc3-int" ? EvidenceIds.Pzl6(vs) : null, artifacts)));
    }

    public static JObject Dominance(DominanceOfficialResult r, EvidenceContext ctx, IReadOnlyList<JObject> artifacts)
    {
        if (!r.Complete)
            throw EvidenceCodes.Fail(EvidenceCodes.NotCertifiable, "Evidence.run_not_complete", "", "projection", $"완료되지 않은 실행({r.RunStatus})은 공식 문서를 만들지 않습니다(R-4).");
        var byId = r.Cells.ToDictionary(c => c.Plan.Pinned.Cell.Id, StringComparer.Ordinal);
        return Dominance(r.RunId, DominanceMatrix.Cells.Select(c => (byId[c.Id].Run, byId[c.Id].Plan.Report.Attestation!)).ToList(), r.Evaluation!, ctx, artifacts);
    }

    /// <summary>The same projection from the 48 cell runs in matrix order and their attestations.</summary>
    public static JObject Dominance(string runId, IReadOnlyList<(RunResult Run, OfficialAttestation Attestation)> cells, DominanceEvaluation e, EvidenceContext ctx, IReadOnlyList<JObject> artifacts)
    {
        var frozen = e.FrozenGamesPerCell ?? throw EvidenceCodes.Fail(EvidenceCodes.NotCertifiable, "Evidence.games_per_cell_not_frozen", "/plan/games_per_cell", "projection",
            "셀당 경기 수가 동결되지 않은 평가는 공식 문서가 아닙니다(R-7).");
        var vs = ctx.ValidationSetId;
        var units = DominanceMatrix.Cells.Select((c, i) => J.O(("cell", Cell(c)), ("inputs", Inputs(cells[i].Run, cells[i].Attestation)),
            ("technical", Technical(cells[i].Run, dominance: true)))).ToList();
        return J.O(
            ("schema", SchemaHeader(EvidenceKind.Dominance)),
            ("execution", Execution(EvidenceIds.Result(vs, DominanceMatrix.Family), ctx, runId, DominanceMatrix.Family, cells[0].Attestation.GitCommitSha)),
            ("approvals", ctx.Approvals()),
            ("plan", J.O(("cell_ids", J.A(DominanceMatrix.Cells.Select(c => (JNode)J.S(c.Id)))), ("games_per_cell", J.N(frozen)))),
            ("units", J.A(units)),
            ("aggregate_evaluation", DominanceAggregate(e, frozen)),
            ("additional_evidence", Additional(vs, DominanceMatrix.Family, DominanceMatrix.Cells.Select(c => c.Id), null, artifacts)));
    }

    private static JObject Additional(string vs, string family, IEnumerable<string> unitIds, string? pzl6, IReadOnlyList<JObject> artifacts) =>
        J.O(("pzl_unit_ids", J.A(unitIds.Select(u => (JNode)J.S(EvidenceIds.UnitPzl(vs, family, u))))), ("pzl6_id", J.Sn(pzl6)),
            ("artifacts", J.A(artifacts.OrderBy(a => Schema.Text(a["artifact_id"]), StringComparer.Ordinal))));

    public static JObject Gate(string status, string reason) => J.O(("status", J.S(status)), ("reason", J.S(reason)));

    // ---------------------------------------------------------------- shared unit parts (§3.2-§3.6)

    /// <summary>The integer of a content version text such as <c>player-input-set-v3</c> (§3.2 <c>P</c>).</summary>
    internal static int ContentVersionNumber(string text) =>
        text.LastIndexOf("-v", StringComparison.Ordinal) is var at and >= 0 && int.TryParse(text.AsSpan(at + 2), NumberStyles.None, CultureInfo.InvariantCulture, out var v) && v > 0
            ? v : throw new ArgumentException($"콘텐츠 버전 '{text}'에서 번호를 읽을 수 없습니다.", nameof(text));

    public static JObject Inputs(RunResult run, OfficialAttestation att)
    {
        var c = run.Config;
        return J.O(
            ("unit_run_id", J.S(c.RunId)), ("mode", J.S(c.Mode)), ("cohort_kind", J.S(c.Cohort.Kind.ToString())), ("rules_version", J.S(c.Rules.Version)), ("rules_status", J.S(c.Rules.Status.ToString())),
            ("called_pitch_mode", J.S(c.Rules.CalledPitchMode?.ToString() ?? throw new ArgumentException("판정 모드가 없는 규칙입니다.", nameof(run)))),
            ("player_input_set_content_version", J.N(ContentVersionNumber(c.Cohort.InputSet.ContentVersion))), ("player_input_set_id", J.S(c.Cohort.InputSet.Id)),
            ("planned_games", J.N(run.Requested)), ("block_size_games", J.Nn(run.Plan?.BlockSize)), ("pa_target", J.Nn(run.Plan?.PaTarget)),
            ("games", J.A(run.Specs.Select(s => (JNode)J.O(("index", J.N(s.GameIndex)), ("game_id", J.S(s.GameId)), ("seed", J.S(EvidenceScalars.U64(s.Seed))))))),
            ("attestation", Attestation(att)), ("attestation_hash", J.S(att.Hash())));
    }

    public static JObject Attestation(OfficialAttestation a) => J.O(
        ("schema_version", J.S(a.SchemaVersion)), ("git_commit_sha", J.S(a.GitCommitSha)), ("execution_policy_id", J.S(a.ExecutionPolicyId)), ("execution_policy_hash", J.S(a.ExecutionPolicyHash)),
        ("seed_manifest_path", J.S(a.SeedManifestPath)), ("seed_manifest_sha256", J.S(a.SeedManifestSha256)), ("seed_family", J.S(a.SeedFamily)), ("competition_key", J.S(a.CompetitionKey)),
        ("game_id_rule", J.S(a.GameIdRule)), ("cohort_id", J.S(a.CohortId)), ("cohort_inputs_hash", J.S(a.CohortInputsHash)), ("policy_id", J.S(a.PolicyId)), ("model_version", J.S(a.ModelVersion)),
        ("tuning_version", J.S(a.TuningVersion)), ("tuning_hash", J.S(a.TuningHash)), ("rules_profile_id", J.S(a.RulesProfileId)), ("rules_hash", J.S(a.RulesHash)),
        ("advancement_policy_version", J.S(a.AdvancementPolicyVersion)), ("park_snapshot", J.S(a.ParkSnapshot)), ("event_budget", J.N(a.EventBudget)), ("config_hash", J.S(a.ConfigHash)));

    public static JObject Technical(RunResult run, bool dominance)
    {
        var games = run.Games;
        var endKinds = games.Where(g => g.EndKind != GameEndKind.None).GroupBy(g => g.EndKind.ToString(), StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (JNode)J.O(("end_kind", J.S(g.Key)), ("count", J.N(g.Count())))).ToList();
        var codes = games.Where(g => g.Failure is not null).Select(g => g.Failure!.Code).Distinct().Order(StringComparer.Ordinal).Select(c => (JNode)J.S(c)).ToList();
        return J.O(
            ("run_status", J.S(RunReport.RunStatus(run))), ("run_result_version", J.S(run.ResultVersion ?? throw new ArgumentException("결과 버전이 없는 실행(취소·중단)입니다.", nameof(run)))),
            ("run_result_hash", J.S(run.RunResultHash)), ("games_requested", J.N(run.Requested)), ("games_executed", J.N(games.Count)), ("games_completed", J.N(run.CompletedCount)),
            ("games_failed", J.N(run.FailedCount)), ("games_cancelled", J.N(run.CancelledCount)), ("games_not_started", J.N(run.NotStartedCount)),
            ("games_unexecuted_after_stop", J.N(run.UnexecutedAfterStop)), ("plate_appearances", J.N(run.Aggregate.PlateAppearances)), ("pitches", J.N(run.Aggregate.Pitches)),
            ("committed_events", J.N(games.Sum(g => (long)g.CommittedEvents))), ("end_kinds", J.A(endKinds)), ("error_codes", J.A(codes)),
            ("metrics_computed", J.B(run.MetricsComputed)), ("metrics_error_code", J.Sn(run.MetricsComputed ? null : run.Metrics.Error!.Value.Code)),
            ("stop", run.Stop is { } s
                ? J.O(("executed_games", J.N(s.ExecutedGames)), ("stop_reason", J.S(s.ReasonText)), ("cumulative_pa", J.N(s.CumulativePa)), ("block_size", J.N(s.BlockSize)))
                : JNode.Null),
            ("counters", Counters(run.Aggregate)), ("games", J.A(games.Select(g => (JNode)Game(g, dominance)))));
    }

    public static long[] CounterValues(MetricCounters c) =>
    [
        c.PlateAppearances, c.AtBats, c.Hits, c.Singles, c.Doubles, c.Triples, c.HomeRuns, c.Walks, c.HitByPitch, c.Strikeouts, c.SacrificeFlies, c.SacrificeBunts,
        c.Pitches, c.LocatedPitches, c.UnlocatedPitches, c.ZonePitches, c.HitByPitchPitches, c.Swings, c.SwingingStrikes, c.Fouls, c.InPlayPitches, c.CalledBalls, c.CalledStrikes, c.Takes,
        c.EventHomeRuns, c.EventHits, c.Events, c.DuplicatesIgnored,
    ];

    public static JObject Counters(MetricCounters c) => J.O(EvidenceShapes.CounterFields.Zip(CounterValues(c)).Select(p => (p.First, (JNode)J.N(p.Second))).ToArray());

    /// <summary>§3.6: a game result exactly as the existing executor recorded it; the only null restoration is the "-" of an absent RNG hash (§10.7).</summary>
    public static JObject Game(GameRunResult g, bool dominance)
    {
        var f = g.Failure;
        var regulation = dominance ? g.Regulation : null;
        return J.O(
            ("index", J.N(g.Game.GameIndex)), ("game_id", J.S(g.Game.GameId)), ("seed", J.S(EvidenceScalars.U64(g.Game.Seed))), ("status", J.S(g.Status.ToString())), ("end_kind", J.S(g.EndKind.ToString())),
            ("outcome", J.Sn(g.Outcome?.ToString())), ("away_runs", J.N(g.AwayRuns)), ("home_runs", J.N(g.HomeRuns)), ("final_inning", J.N(g.FinalInning)), ("final_half", J.S(g.FinalHalf.ToString())),
            ("committed_events", J.N(g.CommittedEvents)), ("event_hash", J.S(g.EventHash)), ("rng_hash", J.Sn(g.RngHash == "-" ? null : g.RngHash)), ("result_hash", J.S(g.ResultHash)),
            ("error_code", J.Sn(f?.Code)),
            ("failure", f is null ? JNode.Null : J.O(("code", J.S(f.Code)), ("stage", J.S(f.Stage)), ("event_sequence", J.N(f.EventSequence)), ("revision", J.N(f.Revision)),
                ("rng_hash", J.Sn(f.RngHash == "-" ? null : f.RngHash)),
                ("last_receipts", J.A(f.LastReceipts.Select(r => (JNode)J.O(("event_id", J.S(r.EventId)), ("kind", J.S(r.Kind)), ("fingerprint", J.S(r.Fingerprint)))))))),
            ("counters", Counters(g.Counters)),
            ("box_score_cross_check", g.CrossCheck is { } x
                ? J.O(("passed", J.B(x.Passed)), ("items", J.A(x.Items.Select(i => (JNode)J.O(("name", J.S(i.Name)), ("from_events", J.N(i.FromEvents)), ("from_box_score", J.N(i.FromBoxScore)))))))
                : JNode.Null),
            ("regulation_half_innings", regulation is { Ok: true } r ? J.A(r.HalfInnings.Select(v => (JNode)J.N(v))) : JNode.Null),
            ("regulation_error_code", J.Sn(regulation is { Ok: false } bad ? bad.ErrorCode : null)));
    }

    // ---------------------------------------------------------------- Family evaluation (§3.7-§3.8)

    private static JObject UnitEvaluation(OfficialFamilyPlan plan, RunResult run, UnitVerdict verdict)
    {
        if (!plan.StopAware)
            return J.O(("kind", J.S("INTEGRATION")), ("policy_id", JNode.Null), ("policy_hash", JNode.Null), ("evaluation_computed", J.B(run.MetricsComputed)),
                ("error_code", J.Sn(run.MetricsComputed ? null : run.Metrics.Error!.Value.Code)), ("gate", Gate(GateText.Of(verdict.Gate), verdict.Reason)), ("shares", JNode.Null),
                ("metrics", J.A()));
        var policy = EvaluationPolicy.FcEvaluationV1;
        var evaluated = StratifiedEvaluator.Evaluate(run, policy);
        JNode shares = JNode.Null;
        var metrics = new List<JNode>();
        if (evaluated.IsSuccess)
        {
            var s = evaluated.Value.Shares;
            shares = J.O(("same_games", J.N(s.SameGames)), ("opposite_games", J.N(s.OppositeGames)), ("mixed_games", J.N(s.MixedGames)), ("same_pa", J.N(s.SamePa)), ("opposite_pa", J.N(s.OppositePa)),
                ("same_pitches", J.N(s.SamePitches)), ("opposite_pitches", J.N(s.OppositePitches)), ("same_swings", J.N(s.SameSwings)), ("opposite_swings", J.N(s.OppositeSwings)));
            metrics.AddRange(evaluated.Value.Metrics.Where(m => m.Official).OrderBy(m => int.Parse(m.FId.AsSpan(1), CultureInfo.InvariantCulture)).Select(Metric));
        }

        return J.O(("kind", J.S("CALIBRATION")), ("policy_id", J.S(policy.Id)), ("policy_hash", J.S(policy.Hash())), ("evaluation_computed", J.B(evaluated.IsSuccess)),
            ("error_code", J.Sn(verdict.ErrorCode)), ("gate", Gate(GateText.Of(verdict.Gate), verdict.Reason)), ("shares", shares), ("metrics", J.A(metrics)));
    }

    private static JObject Metric(StratifiedMetric m) => J.O(
        ("f_id", J.S(m.FId)), ("target_id", J.S(m.TargetId)), ("method", J.S(m.Method)), ("numerator", J.N(m.Numerator)), ("denominator", J.N(m.Denominator)),
        ("numerator_same", J.N(m.NumeratorSame)), ("denominator_same", J.N(m.DenominatorSame)), ("numerator_opposite", J.N(m.NumeratorOpposite)), ("denominator_opposite", J.N(m.DenominatorOpposite)),
        ("measured_ppm", J.Nn(m.MeasuredPpm)), ("target_low_ppm", J.N(m.TargetLowPpm)), ("target_high_ppm", J.N(m.TargetHighPpm)), ("min_denominator", J.N(m.MinDenominator)),
        ("min_same", J.Nn(m.MinSame)), ("min_opposite", J.Nn(m.MinOpposite)), ("gate", Gate(GateText.Of(m.Status), m.Reason)));

    // ---------------------------------------------------------------- Dominance (§4)

    public static JObject Cell(DominanceCell c) => J.O(("id", J.S(c.Id)), ("batter_strategy", J.S(c.Batter.ToString())), ("pitcher_strategy", J.S(c.Pitcher.ToString())),
        ("hand", J.S(c.Hand.ToString())), ("called_pitch_mode", J.S(c.Mode.ToString())), ("cohort_id", J.S(c.CohortId)), ("policy_id", J.S(c.PolicyId)), ("competition_key", J.S(c.CompetitionKey)));

    public static JObject DominanceAggregate(DominanceEvaluation e, int frozen) => J.O(
        ("policy_id", J.S(e.PolicyId)), ("frozen_games_per_cell", J.N(frozen)),
        ("cells", J.A(e.Cells.Select(c => (JNode)J.O(("cell_id", J.S(c.Cell.Id)), ("state", J.S(c.State.ToString())), ("reason", J.S(c.Reason)),
            ("config_hash", J.S(c.ConfigHash ?? throw new ArgumentException($"{c.Cell.Id}: config_hash가 없는 셀 평가입니다.", nameof(e)))),
            ("run_result_hash", J.S(c.RunResultHash ?? throw new ArgumentException($"{c.Cell.Id}: run_result_hash가 없는 셀 평가입니다.", nameof(e)))),
            ("planned_games", J.N(c.PlannedGames)), ("failed_games", J.N(c.FailedGames)), ("metrics_failures", J.N(c.MetricsFailures)),
            ("sample", c.State == CellState.Ok && c.Sample is { } s
                ? J.O(("games", J.N(s.Games)), ("total_runs", J.N(s.TotalRuns)), ("sum_of_squares", J.N(s.SumOfSquares)), ("mean", EvidenceScalars.F64(s.Mean)),
                    ("variance", EvidenceScalars.F64(s.Variance ?? throw new ArgumentException($"{c.Cell.Id}: 분산이 없는 표본입니다.", nameof(e)))))
                : JNode.Null))))),
        ("set_problems", J.A(e.SetProblems.Select(p => (JNode)J.S(p)))), ("batter", Side(e.Batter)), ("pitcher", Side(e.Pitcher)),
        ("gate", Gate(SimHarness.DominanceEvaluation.Text(e.Gate), e.GateReason)));

    private static JObject Side(SideResult s) => J.O(
        ("side", J.S(s.Side.ToString())), ("verdict", J.S(SideResult.Text(s.Verdict))), ("reason", J.S(s.Reason)),
        ("strategies", J.A(s.Strategies.Select(v => (JNode)J.O(("strategy", J.S(v.Strategy)), ("dominant", J.B(v.Dominant)), ("not_dominant", J.B(v.NotDominant)), ("label", J.S(v.Label)),
            ("witnesses", J.A(v.Witnesses.Select(w => (JNode)J.O(("rival", J.S(w.Rival)), ("column", J.S(w.Column)), ("strategy_cell", J.S(w.StrategyCell)), ("rival_cell", J.S(w.RivalCell)),
                ("difference", EvidenceScalars.F64(w.Difference)), ("standard_error", EvidenceScalars.F64(w.StandardError)), ("lower_bound", EvidenceScalars.F64(w.LowerBound)),
                ("superior", J.B(w.Superior)), ("non_superiority_z", EvidenceScalars.F64(w.NonSuperiorityZ)),
                ("holm_critical", w.HolmCritical is { } h ? EvidenceScalars.F64(h) : EvidenceScalars.F64HolmStopped()), ("holm_rejected", J.B(w.HolmRejected)))))))))));
}
