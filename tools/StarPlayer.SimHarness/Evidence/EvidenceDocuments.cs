using System.Globalization;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.SimHarness.Evidence;

/// <summary>The four official evidence kinds (B1-T00 §2.1) and the role key of each in <c>schema.contracts</c>.</summary>
public enum EvidenceKind { Family, Dominance, UnitPzl, Pzl6 }

/// <summary>
/// The closed shapes of B1-T00 §2-§7 and §10.2 (T01). Field lists are the contract's, in its order; <see cref="EvidenceRules"/> adds the cross-field rules (state tables R-3/R-5/R-7/R-9, R-12, R-14).
/// </summary>
public static class EvidenceShapes
{
    public const string HashContract = "simharness-evidence-hash-v1";

    public static string SchemaId(EvidenceKind k) => k switch
    {
        EvidenceKind.Family => "simharness-family-result", EvidenceKind.Dominance => "simharness-dominance-result",
        EvidenceKind.UnitPzl => "simharness-pzl-unit-evidence", _ => "simharness-pzl6-roundtrip-evidence",
    };

    public static string RoleKey(EvidenceKind k) => k switch
    {
        EvidenceKind.Family => "family", EvidenceKind.Dominance => "dominance", EvidenceKind.UnitPzl => "pzl_unit", _ => "pzl6",
    };

    public static readonly string[] ContractKeys = ["family", "dominance", "pzl_unit", "pzl6", "hash"];

    /// <summary>The only contract combination this verifier supports (v1: all <c>"1"</c>, §2.1 R-1).</summary>
    public static IReadOnlyDictionary<string, string> SupportedContracts { get; } = ContractKeys.ToDictionary(k => k, _ => "1", StringComparer.Ordinal);

    public static readonly Shape Contracts = Obj(ContractKeys.Select(k => (k, Ver)).ToArray());

    public static readonly Shape SchemaHeader = Obj(("id", Exact), ("version", Ver), ("contracts", Contracts));

    public static readonly Shape Execution = Obj(("evidence_id", Id), ("validation_set_id", Id), ("run_id", Run), ("family", OneOf("fc3-cal", "fc3-int", "fc3-dom")), ("git_commit_sha", Git),
        ("build_provenance_id", Id));

    public static readonly Shape Approvals = Obj(("approval_record_id", Id), ("approval_record_sha256", H), ("freeze_id", Id));

    public static readonly string[] ArtifactRoles =
    [
        "APPROVAL_RECORD", "BUILD_PROVENANCE", "INPUT_BUNDLE", "SEED_MANIFEST", "PREFLIGHT", "UNIT_MANIFEST", "ATTESTATION", "GAME_RESULTS", "GAME_EVENTS", "FAMILY_RESULT",
        "DOMINANCE_RESULT", "UNIT_PZL", "PZL6", "REGRESSION_PROOF", "DIAGNOSTICS", "CANONICAL_PAYLOAD", "RAW_INVENTORY", "VERIFICATION_RECEIPT", "REGISTRY_RECORD",
    ];

    public static readonly Shape ArtifactRef = Obj(("artifact_id", Id), ("role", OneOf(ArtifactRoles)), ("unit_id", Opt(Exact)), ("game_id", Opt(Exact)), ("relative_path", RelPath),
        ("raw_sha256", H), ("byte_length", N));

    public static readonly Shape AdditionalEvidence = Obj(("pzl_unit_ids", Arr(Id)), ("pzl6_id", Opt(Id)), ("artifacts", Arr(ArtifactRef)));

    public static Shape GateOf(bool dominance) => Obj(("status", dominance ? OneOf("PASS", "FAIL", "INSUFFICIENT_SAMPLE", "NOT_EVALUATED", "INCOMPLETE") : OneOf("PASS", "FAIL", "INSUFFICIENT_SAMPLE", "NOT_EVALUATED")),
        ("reason", Text1));

    /// <summary>§8.4 F64: <c>FINITE</c> with the canonical ES string and a null reason; <c>NOT_APPLICABLE</c> with a null value and a reason of the closed v1 list; <c>UNDEFINED</c> reserved and refused in v1.</summary>
    public static readonly Shape F64 = (n, p) =>
    {
        Obj(("state", OneOf("FINITE", "UNDEFINED", "NOT_APPLICABLE")), ("value", Opt(Exact)), ("reason", Opt(Exact)))(n, p);
        var o = (JObject)n;
        switch (Text(o["state"]))
        {
            case "FINITE":
                if (o["value"] is not JString { Value: var v } || o["reason"] is not JNull)
                    throw Bad(p, "Schema.f64", "FINITE는 value 문자열과 null reason이어야 합니다.");
                if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || !double.IsFinite(d) || Jcs.Es(d) != v)
                    throw Bad($"{p}/value", "Schema.f64_noncanonical", $"'{v}'는 정규 binary64 문자열이 아닙니다(NaN·Infinity·1E+21·-0 등 거부).");
                break;
            case "NOT_APPLICABLE":
                if (o["value"] is not JNull || o["reason"] is not JString { Value: "HOLM_STOPPED" })
                    throw Bad(p, "Schema.f64", "NOT_APPLICABLE은 null value와 v1 이유 코드 HOLM_STOPPED만 허용합니다.");
                break;
            default:
                throw Bad($"{p}/state", "Schema.f64_undefined", "UNDEFINED 상태는 v1에서 예약만 되어 있고 허용하지 않습니다.");
        }
    };

    public static readonly Shape FiniteF64 = (n, p) =>
    {
        F64(n, p);
        if (Text(((JObject)n)["state"]) != "FINITE")
            throw Bad(p, "Schema.f64", "이 값은 FINITE만 허용합니다.");
    };

    public static readonly string[] CounterFields =
    [
        "plate_appearances", "at_bats", "hits", "singles", "doubles", "triples", "home_runs", "walks", "hit_by_pitch", "strikeouts", "sacrifice_flies", "sacrifice_bunts",
        "pitches", "located_pitches", "unlocated_pitches", "zone_pitches", "hit_by_pitch_pitches", "swings", "swinging_strikes", "fouls", "in_play_pitches", "called_balls", "called_strikes", "takes",
        "event_home_runs", "event_hits", "events", "duplicates_ignored",
    ];

    public static readonly Shape Counters = Obj(CounterFields.Select(f => (f, N)).ToArray());

    public static readonly string[] CrossCheckNames = ["PA", "AB", "H", "1B", "2B", "3B", "HR", "BB", "HBP", "SO", "SF", "Pitches", "HR(in-play events)", "H(in-play events)", "HBP(pitch events)"];

    public static readonly Shape CrossCheck = Obj(("passed", Bool), ("items", ArrExact(Obj(("name", Text1), ("from_events", N), ("from_box_score", N)), 15)));

    public static readonly Shape ReceiptRef = Obj(("event_id", Exact), ("kind", Exact), ("fingerprint", H));

    public static readonly Shape Failure = Obj(("code", Code), ("stage", Text1), ("event_sequence", I), ("revision", N), ("rng_hash", Opt(H)), ("last_receipts", Arr(ReceiptRef, 0, 12)));

    public static readonly Shape GameResult = Obj(
        ("index", Int(1, 999_999)), ("game_id", Exact), ("seed", U64), ("status", OneOf("Completed", "Failed")), ("end_kind", OneOf("None", "Regulation", "ExtraInnings", "TieAtExtraInningsLimit")),
        ("outcome", Opt(OneOf("AwayWin", "HomeWin", "Tie"))), ("away_runs", I), ("home_runs", I), ("final_inning", I), ("final_half", OneOf("Top", "Bottom")), ("committed_events", Int(0, 999_999)),
        ("event_hash", H), ("rng_hash", Opt(H)), ("result_hash", H), ("error_code", Opt(Code)), ("failure", Opt(Failure)), ("counters", Counters), ("box_score_cross_check", Opt(CrossCheck)),
        ("regulation_half_innings", Opt(ArrExact(I, 16))), ("regulation_error_code", Opt(OneOf("Dominance.regulation_incomplete", "Dominance.line_score_total_mismatch", "Dominance.line_score_mismatch"))));

    public static readonly Shape Stop = Obj(("executed_games", P), ("stop_reason", OneOf("TARGET_REACHED", "SEEDS_EXHAUSTED")), ("cumulative_pa", N), ("block_size", P));

    public static readonly Shape Technical = Obj(
        ("run_status", OneOf("COMPLETED", "GAMES_FAILED", "METRICS_ERROR")), ("run_result_version", OneOf("run-result-v1", "run-result-v2")), ("run_result_hash", H), ("games_requested", I),
        ("games_executed", I), ("games_completed", I), ("games_failed", I), ("games_cancelled", IntConst(0)), ("games_not_started", IntConst(0)), ("games_unexecuted_after_stop", I),
        ("plate_appearances", N), ("pitches", N), ("committed_events", N),
        ("end_kinds", Arr(Obj(("end_kind", OneOf("ExtraInnings", "Regulation", "TieAtExtraInningsLimit")), ("count", Int(1, int.MaxValue))))), ("error_codes", Arr(Code)),
        ("metrics_computed", Bool), ("metrics_error_code", Opt(Code)), ("stop", Opt(Stop)), ("counters", Counters), ("games", Arr(GameResult)));

    public static readonly Shape GameSpec = Obj(("index", Int(1, 999_999)), ("game_id", Exact), ("seed", U64));

    public static readonly string[] AttestationFields =
    [
        "schema_version", "git_commit_sha", "execution_policy_id", "execution_policy_hash", "seed_manifest_path", "seed_manifest_sha256", "seed_family", "competition_key", "game_id_rule",
        "cohort_id", "cohort_inputs_hash", "policy_id", "model_version", "tuning_version", "tuning_hash", "rules_profile_id", "rules_hash", "advancement_policy_version", "park_snapshot",
        "event_budget", "config_hash",
    ];

    public static readonly Shape Attestation = Obj(
        ("schema_version", Const(OfficialAttestation.Version)), ("git_commit_sha", Git), ("execution_policy_id", Exact), ("execution_policy_hash", H), ("seed_manifest_path", Text1),
        ("seed_manifest_sha256", H), ("seed_family", OneOf("fc3-cal", "fc3-int", "fc3-dom")), ("competition_key", Text1), ("game_id_rule", Text1), ("cohort_id", Text1), ("cohort_inputs_hash", H),
        ("policy_id", Text1), ("model_version", Text1), ("tuning_version", Text1), ("tuning_hash", H), ("rules_profile_id", Text1), ("rules_hash", H), ("advancement_policy_version", Text1),
        ("park_snapshot", Text1), ("event_budget", Int(1, 999_999)), ("config_hash", H));

    public static readonly Shape UnitInputs = Obj(
        ("unit_run_id", Run), ("mode", Exact), ("cohort_kind", OneOf("Neutral", "VirtualLeague")), ("rules_version", Exact), ("rules_status", Exact), ("called_pitch_mode", OneOf("Automated", "NonAutomated")),
        ("player_input_set_content_version", P), ("player_input_set_id", Exact), ("planned_games", Int(1, 999_999)), ("block_size_games", Opt(P)), ("pa_target", Opt(N)),
        ("games", Arr(GameSpec)), ("attestation", Attestation), ("attestation_hash", H));

    public static readonly Shape Shares = Obj(("same_games", I), ("opposite_games", I), ("mixed_games", I), ("same_pa", N), ("opposite_pa", N), ("same_pitches", N), ("opposite_pitches", N),
        ("same_swings", N), ("opposite_swings", N));

    public static readonly Shape OfficialMetric = Obj(
        ("f_id", OneOf("F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8")), ("target_id", Exact), ("method", OneOf("standardized_3_7_4_7", "observed_aggregate")), ("numerator", N), ("denominator", N),
        ("numerator_same", N), ("denominator_same", N), ("numerator_opposite", N), ("denominator_opposite", N), ("measured_ppm", Opt(Ppm)), ("target_low_ppm", Ppm), ("target_high_ppm", Ppm),
        ("min_denominator", N), ("min_same", Opt(N)), ("min_opposite", Opt(N)), ("gate", GateOf(false)));

    public static readonly Shape FamilyEvaluation = Obj(("kind", OneOf("CALIBRATION", "INTEGRATION")), ("policy_id", Opt(Exact)), ("policy_hash", Opt(H)), ("evaluation_computed", Bool),
        ("error_code", Opt(Code)), ("gate", GateOf(false)), ("shares", Opt(Shares)), ("metrics", Arr(OfficialMetric)));

    public static readonly Shape Family = Obj(
        ("schema", SchemaHeader), ("execution", Execution), ("approvals", Approvals),
        ("plan", Obj(("unit_ids", Arr(Exact)), ("combination_policy_clause_id", Exact))),
        ("units", Arr(Obj(("unit_id", Exact), ("inputs", UnitInputs), ("technical", Technical), ("evaluation", FamilyEvaluation)))),
        ("aggregate_evaluation", GateOf(false)), ("additional_evidence", AdditionalEvidence));

    public static readonly Shape Cell = Obj(("id", Exact), ("batter_strategy", OneOf("Normal", "Patient", "Aggressive", "TakeFirst")), ("pitcher_strategy", OneOf("Balanced", "Attack", "Careful")),
        ("hand", OneOf("Same", "Opposite")), ("called_pitch_mode", OneOf("Automated", "NonAutomated")), ("cohort_id", Exact), ("policy_id", Exact), ("competition_key", Exact));

    public static readonly Shape CellEvaluation = Obj(
        ("cell_id", Exact), ("state", OneOf("Ok", "Missing", "IntegrityViolation", "Incomplete", "EngineFailure", "MetricsFailure", "InsufficientSample")), ("reason", Text1), ("config_hash", H),
        ("run_result_hash", H), ("planned_games", I), ("failed_games", I), ("metrics_failures", I),
        ("sample", Opt(Obj(("games", I), ("total_runs", N), ("sum_of_squares", N), ("mean", FiniteF64), ("variance", FiniteF64)))));

    public static readonly Shape Witness = Obj(("rival", Exact), ("column", Exact), ("strategy_cell", Exact), ("rival_cell", Exact), ("difference", FiniteF64), ("standard_error", FiniteF64),
        ("lower_bound", FiniteF64), ("superior", Bool), ("non_superiority_z", FiniteF64), ("holm_critical", F64), ("holm_rejected", Bool));

    public static readonly Shape SideResult = Obj(("side", OneOf("Batter", "Pitcher")), ("verdict", OneOf("NOT_EVALUATED", "DOMINANCE_DETECTED", "NO_DOMINANCE_SUPPORTED", "INCONCLUSIVE")),
        ("reason", Text1), ("strategies", Arr(Obj(("strategy", Exact), ("dominant", Bool), ("not_dominant", Bool), ("label", OneOf("DOMINANT", "NOT_DOMINANT", "UNDETERMINED")), ("witnesses", Arr(Witness))))));

    public static readonly Shape Dominance = Obj(
        ("schema", SchemaHeader), ("execution", Execution), ("approvals", Approvals),
        ("plan", Obj(("cell_ids", ArrExact(Exact, 48)), ("games_per_cell", P))),
        ("units", ArrExact(Obj(("cell", Cell), ("inputs", UnitInputs), ("technical", Technical)), 48)),
        ("aggregate_evaluation", Obj(("policy_id", Exact), ("frozen_games_per_cell", P), ("cells", ArrExact(CellEvaluation, 48)), ("set_problems", Arr(Text1)), ("batter", SideResult),
            ("pitcher", SideResult), ("gate", GateOf(true)))),
        ("additional_evidence", AdditionalEvidence));

    public static readonly string[] CoverageFields = ["completed_games", "events", "pitches", "called_pitch_steps"];

    public static readonly Shape Coverage = Obj(("completed_games", N), ("events", N), ("pitches", N), ("called_pitch_steps", N));

    public static readonly Shape Check = Obj(
        ("id", OneOf("pzl-1", "pzl-2", "pzl-3", "pzl-4", "pzl-5", "pzl-6", "pzl-7", "pzl-8", "pzl-9", "pzl-10", "pzl-11", "pzl-12a", "pzl-12b")),
        ("status", OneOf("PASS", "FAIL", "NOT_EVALUATED", "NOT_APPLICABLE", "DEFERRED")), ("reason", Text1), ("coverage", Coverage),
        ("violations", Arr(Obj(("code", Text1), ("game_id", Opt(Text1)), ("event_id", Opt(Text1)), ("field", Opt(Text1))))),
        ("test_evidence", Opt(Obj(("profile_id", Id), ("profile_sha256", H), ("source_git_sha", Git),
            ("cases", Arr(Obj(("id", Str(v => IsText1(v) && v.Contains('|'), "CASE_ID")), ("status", OneOf("PASS", "FAIL")))))))),
        ("deferral", Opt(Obj(("code", Const("DEFERRED_TO_PHASE_5_6_28")), ("bcl_phases", Exact3(5, 6, 28)), ("lmv_a_phases", Exact2(5, 28))))));

    private static Shape Exact3(int a, int b, int c) => (n, p) =>
    {
        ArrExact(I, 3)(n, p);
        if (!Items(n).Select(Long).SequenceEqual([a, b, c]))
            throw Bad(p, "Schema.format", $"[{a},{b},{c}]이어야 합니다.");
    };

    private static Shape Exact2(int a, int b) => (n, p) =>
    {
        ArrExact(I, 2)(n, p);
        if (!Items(n).Select(Long).SequenceEqual([a, b]))
            throw Bad(p, "Schema.format", $"[{a},{b}]이어야 합니다.");
    };

    public static readonly string[] SummaryCounts =
    [
        "events", "located_events", "ef2_receipts", "version_mismatches", "fingerprint_mismatches", "foreign_event_ids", "duplicate_event_ids", "event_id_collisions", "receipt_log_mismatches",
        "called_pitch_steps", "framing_inputs", "framing_entries", "framing_non_neutral",
    ];

    public static readonly Shape PzlSummary = Obj([.. SummaryCounts.Select(f => (f, N)), ("drawn_streams", Arr(StreamId))]);

    public static readonly Shape PzlGame = Obj(("index", Int(1, 999_999)), ("game_id", Exact), ("seed", U64), ("status", OneOf("Completed", "Failed")), ("game_result_hash", H), ("event_hash", H),
        ("rng_hash", Opt(H)), ("committed_events", Int(0, 999_999)), ("summary", PzlSummary), ("events_digest", AH), ("final_rng_canonical", Opt(RngSet1)), ("observation_present", True));

    public static IReadOnlyList<string> RegisteredStreams => PzlChecks.PinnedStreams;

    public static readonly Shape UnitPzl = Obj(
        ("schema", SchemaHeader), ("execution", Execution), ("approvals", Approvals),
        ("plan", Obj(("unit_id", Exact), ("config_hash", H), ("run_result_version", Opt(OneOf("run-result-v1", "run-result-v2"))), ("run_result_hash", Opt(H)))),
        ("registered_streams", ArrExact(StreamId, 7)), ("games", Arr(PzlGame)), ("checks", ArrExact(Check, 4)), ("additional_evidence", AdditionalEvidence));

    public static readonly Shape RoundTripSample = Obj(
        ("unit_id", OneOf("automated", "non-automated")), ("index", Int(1, 999_999)), ("game_id", Exact), ("seed", U64), ("game_result_hash", H), ("event_hash", H), ("rng_hash", H),
        ("event_format_version", IntConst(2)), ("event_count", I), ("located_event_count", I), ("ef1_receipts", I), ("ef2_receipts", I),
        ("event_sequence_equal", Bool), ("locations_equal", Bool), ("receipts_equal", Bool), ("rewrite_bytes_equal", Bool), ("final_state_equal", Bool), ("event_hash_equal", Bool),
        ("event_replay_match", Bool), ("seed_replay_match", Bool));

    public static readonly Shape Pzl6 = Obj(
        ("schema", SchemaHeader), ("execution", Execution), ("approvals", Approvals),
        ("plan", Obj(("integration_evidence_id", Id), ("selection_rule", Const("FIRST_10_PER_MODE")), ("sample_count_per_mode", IntConst(10)))),
        ("samples", ArrExact(RoundTripSample, 20)), ("check", Check), ("additional_evidence", AdditionalEvidence));

    public static Shape Of(EvidenceKind k) => k switch
    {
        EvidenceKind.Family => Family, EvidenceKind.Dominance => Dominance, EvidenceKind.UnitPzl => UnitPzl, _ => Pzl6,
    };

    // ---------------------------------------------------------------- GAME_EVENTS records (§10.2) and the event row (§5.3)

    public static readonly Shape Location = Obj(("zone_model_version", Exact), ("pitch_zone_id", Exact), ("applied_batter_side", Exact), ("boundary_class", OneOf("Clear", "Borderline")));

    public static readonly Shape Prepared = Obj(("batter_id", Exact), ("pitcher_id", Exact), ("catcher_id", Exact), ("player_input_set_id", Exact), ("input_receiving_framing", Int(0, 100)),
        ("frozen_receiving_framing", Opt(Int(0, 100))), ("rules_profile_id", Exact), ("rules_version", Exact), ("rules_hash", H), ("probability_model_version", Exact), ("tuning_version", Exact),
        ("tuning_hash", H));

    public static readonly Shape BranchStep = Obj(("stage", Exact), ("condition", Exact), ("outcome", Exact), ("stream", Exact), ("roll", Int(0, 999_999)), ("raw_draws", D64Positive),
        ("framing_entries", Arr(Obj(("slot", Const("catcher.framing")), ("factor_ppm", N), ("applied", Bool)))));

    public static readonly Shape PitchObservation = Obj(
        ("branch_contract_version", Exact), ("branch_kind", Exact), ("explicit_take", Bool), ("intent", Exact), ("target_zone", Exact), ("location", Location), ("prepared", Prepared),
        ("h1_rng_canonical", RngSet1), ("steps", Arr(BranchStep)), ("stream_usage", Arr(Obj(("stream", Exact), ("calls", I), ("raw_draws", D64)))), ("next_bounded_calls", I),
        ("runner_calls", I), ("runner_raw_draws", D64));

    public static readonly Shape EventObservation = Obj(
        ("sequence", Int(1, 999_999)), ("event_id", Exact), ("kind", OneOf("approach", "pitch", "take", "in_play", "runner_play", "substitution")), ("canonical_event", EvCanon),
        ("receipt", Obj(("event_id", Exact), ("kind", Exact), ("fingerprint_version", OneOf("ef1", "ef2")), ("fingerprint", H))),
        ("rng_before", RngSet1), ("rng_after", RngSet1), ("pitch", Opt(PitchObservation)));

    public static readonly Shape BundleHeader = Obj(("bundle", Const("simharness-game-events-v1")), ("contracts", Contracts), ("family", OneOf("fc3-cal", "fc3-int", "fc3-dom")), ("run_id", Run),
        ("unit_evidence_id", Id), ("unit_id", Exact), ("validation_set_id", Id));

    public static readonly Shape BundleGame = Obj(("committed_events", I), ("events_digest", AH), ("game_id", Exact), ("index", P), ("record", Const("game")), ("seed", U64),
        ("status", OneOf("Completed", "Failed")));

    public static readonly Shape BundleEnd = Obj(("executed_games", I), ("record", Const("end")), ("total_events", N));

    // ---------------------------------------------------------------- GAME_RESULTS bundle (OWNER decision 3 §5)

    public static readonly Shape GameResultsHeader = Obj(("bundle", Const("simharness-game-results-v1")), ("contracts", Contracts), ("family", OneOf("fc3-cal", "fc3-int", "fc3-dom")), ("run_id", Run),
        ("unit_evidence_id", Id), ("unit_id", Exact), ("validation_set_id", Id));

    public static readonly Shape GameResultsGame = Obj(("game", GameResult), ("record", Const("game")));

    public static readonly Shape GameResultsEnd = Obj(("completed_games", I), ("executed_games", I), ("failed_games", I), ("record", Const("end")));
}
