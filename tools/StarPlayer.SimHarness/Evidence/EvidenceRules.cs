using System.Globalization;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.SimHarness.Evidence;

/// <summary>
/// T01 entry point: strict parse → closed shape → contract combination → the cross-field rules of the contract that need no outside source (§2.1 S1/S2, §2.2 R-12, §2.4 R-14 axes, §2.5, §3.4-§3.8
/// state tables R-3/R-5, §4 R-7, §5 / §7 R-9 and the coverage rules that a document can show about itself, §6). Everything compared with an approval, a seed file or a re-execution is T06b.
/// </summary>
public static class EvidenceRules
{
    public static JObject Validate(EvidenceKind kind, ReadOnlySpan<byte> raw)
    {
        var doc = StrictJson.Parse(raw, Stage);
        EvidenceShapes.Of(kind)(doc, "");
        var o = (JObject)doc;
        Header(kind, o);
        switch (kind)
        {
            case EvidenceKind.Family: FamilyRules(o); break;
            case EvidenceKind.Dominance: DominanceRules(o); break;
            case EvidenceKind.UnitPzl: UnitPzlRules(o); break;
            default: Pzl6Rules(o); break;
        }

        return o;
    }

    public static EvidenceError? Check(EvidenceKind kind, ReadOnlySpan<byte> raw)
    {
        try
        {
            Validate(kind, raw);
            return null;
        }
        catch (EvidenceException e)
        {
            return e.Error;
        }
    }

    private static string Family(JObject doc) => Text(Obj(doc["execution"])["family"]);

    // ---------------------------------------------------------------- common header (S1, S2, contracts, R-12, §2.5)

    private static void Header(EvidenceKind kind, JObject doc)
    {
        var schema = Obj(doc["schema"]);
        if (Text(schema["id"]) != EvidenceShapes.SchemaId(kind))
            throw Bad("/schema/id", "Schema.id", $"schema.id는 '{EvidenceShapes.SchemaId(kind)}'여야 합니다(DRAFT·다른 ID 거부, S1).");
        var contracts = Obj(schema["contracts"]);
        if (Text(schema["version"]) != Text(contracts[EvidenceShapes.RoleKey(kind)]))
            throw Bad("/schema/version", "Schema.version_role", "schema.version이 schema.contracts의 역할 키 값과 다릅니다(S2).");
        foreach (var key in EvidenceShapes.ContractKeys)
            if (Text(contracts[key]) != EvidenceShapes.SupportedContracts[key])
                throw Bad($"/schema/contracts/{key}", "Schema.contract_combination", "이 검증기가 지원하지 않는 계약 조합입니다(v1은 모두 \"1\").", EvidenceCodes.ContractNotApproved);

        var family = Family(doc);
        var allowed = kind switch { EvidenceKind.Family => family is "fc3-cal" or "fc3-int", EvidenceKind.Dominance => family == "fc3-dom", EvidenceKind.Pzl6 => family == "fc3-int", _ => true };
        if (!allowed)
            throw Bad("/execution/family", "Schema.family", $"{kind} 문서의 family로 '{family}'는 허용되지 않습니다.");

        var exec = Obj(doc["execution"]);
        var vs = Text(exec["validation_set_id"]);
        if (vs.Length > 64)
            throw Bad("/execution/validation_set_id", "Schema.format", "validation_set_id는 64자 이하입니다.");
        var expected = kind switch
        {
            EvidenceKind.UnitPzl => EvidenceIds.UnitPzl(vs, family, Text(Obj(doc["plan"])["unit_id"])),
            EvidenceKind.Pzl6 => EvidenceIds.Pzl6(vs),
            _ => EvidenceIds.Result(vs, family),
        };
        if (Text(exec["evidence_id"]) != expected)
            throw Bad("/execution/evidence_id", "Evidence.evidence_id_mismatch", $"evidence_id는 도출값 '{expected}'여야 합니다(R-12).", EvidenceCodes.ReferenceInvalid);

        var add = Obj(doc["additional_evidence"]);
        var artifacts = Items(add["artifacts"]).Select(Obj).ToList();
        for (var i = 1; i < artifacts.Count; i++)
            if (string.CompareOrdinal(Text(artifacts[i - 1]["artifact_id"]), Text(artifacts[i]["artifact_id"])) >= 0)
                throw Bad($"/additional_evidence/artifacts/{i}", "Schema.order", "artifacts는 artifact_id ordinal 오름차순·중복 금지입니다.");
        for (var i = 0; i < artifacts.Count; i++)
            if (Text(artifacts[i]["role"]) == "GAME_EVENTS" && (IsNull(artifacts[i]["unit_id"]) || !IsNull(artifacts[i]["game_id"])))
                throw Bad($"/additional_evidence/artifacts/{i}", "Schema.artifact", "GAME_EVENTS artifact는 unit_id non-null·game_id null입니다(§21).");

        var pzlIds = Items(add["pzl_unit_ids"]).Select(Text).ToList();
        if (kind is EvidenceKind.UnitPzl or EvidenceKind.Pzl6)
        {
            if (pzlIds.Count != 0 || !IsNull(add["pzl6_id"]))
                throw Bad("/additional_evidence", "Schema.additional_evidence", "Unit PZL·#6 문서의 pzl_unit_ids는 []·pzl6_id는 null입니다.");
        }
        else
        {
            var units = UnitIds(kind, doc);
            if (!pzlIds.SequenceEqual(units.Select(u => EvidenceIds.UnitPzl(vs, family, u)), StringComparer.Ordinal))
                throw Bad("/additional_evidence/pzl_unit_ids", "Evidence.pzl_unit_ids_mismatch", "pzl_unit_ids는 부모 Unit 순서로 Unit마다 정확히 하나의 도출 ID입니다.", EvidenceCodes.ReferenceInvalid);
            var pzl6 = add["pzl6_id"];
            if (family == "fc3-int" ? pzl6 is not JString { Value: var v } || v != EvidenceIds.Pzl6(vs) : !IsNull(pzl6))
                throw Bad("/additional_evidence/pzl6_id", "Evidence.pzl6_id_mismatch", "pzl6_id는 Integration에서만 도출 ID이고 그 밖에는 null입니다.", EvidenceCodes.ReferenceInvalid);
        }
    }

    private static IReadOnlyList<string> UnitIds(EvidenceKind kind, JObject doc) => kind == EvidenceKind.Dominance
        ? Items(Obj(doc["plan"])["cell_ids"]).Select(Text).ToList()
        : Items(Obj(doc["plan"])["unit_ids"]).Select(Text).ToList();

    /// <summary>The called-pitch mode of a unit of a family (from the fixed plans, never from the document).</summary>
    public static string? ModeOf(string family, string unitId) => family switch
    {
        "fc3-cal" => CalibrationStrata.All.FirstOrDefault(s => s.Id == unitId) is { } s ? (s.RulesProfileId == HarnessData.AutomatedProfile ? "Automated" : "NonAutomated") : null,
        "fc3-int" => unitId == "automated" ? "Automated" : unitId == "non-automated" ? "NonAutomated" : null,
        "fc3-dom" => DominanceMatrix.Cells.FirstOrDefault(c => c.Id == unitId) is { } c ? c.Mode.ToString() : null,
        _ => null,
    };

    // ---------------------------------------------------------------- Family (§3)

    private static void FamilyRules(JObject doc)
    {
        var family = Family(doc);
        var calibration = family == "fc3-cal";
        string[] plan = calibration ? ["L1", "L2", "L3", "L4"] : ["automated", "non-automated"];
        if (!Items(Obj(doc["plan"])["unit_ids"]).Select(Text).SequenceEqual(plan))
            throw Bad("/plan/unit_ids", "Schema.plan", $"plan.unit_ids는 [{string.Join(",", plan)}]입니다.");
        var units = Items(doc["units"]).Select(Obj).ToList();
        if (units.Count != plan.Length)
            throw Bad("/units", "Schema.array_length", $"units는 계획과 같은 {plan.Length}개입니다.");
        for (var i = 0; i < units.Count; i++)
        {
            var ptr = $"/units/{i}";
            if (Text(units[i]["unit_id"]) != plan[i])
                throw Bad($"{ptr}/unit_id", "Schema.plan", $"{i + 1}번째 Unit은 계획 순서의 {plan[i]}입니다.");
            UnitResult(doc, units[i], ptr, family, plan[i], dominance: false);
            Evaluation(Obj(units[i]["evaluation"]), $"{ptr}/evaluation", calibration, plan[i]);
        }
    }

    private static void UnitResult(JObject doc, JObject unit, string ptr, string family, string unitId, bool dominance)
    {
        var exec = Obj(doc["execution"]);
        var inputs = Obj(unit["inputs"]);
        var technical = Obj(unit["technical"]);
        var calibration = family == "fc3-cal";
        var att = Obj(inputs["attestation"]);

        // R-14 axes
        if (Text(inputs["unit_run_id"]) != Text(exec["run_id"]))
            throw Bad($"{ptr}/inputs/unit_run_id", "Schema.r14_run_id", "execution.run_id == units[].inputs.unit_run_id (R-14).");
        if (Text(att["git_commit_sha"]) != Text(exec["git_commit_sha"]))
            throw Bad($"{ptr}/inputs/attestation/git_commit_sha", "Schema.r14_git", "execution.git_commit_sha == attestation.git_commit_sha (R-14).");
        if (Text(att["seed_family"]) != family)
            throw Bad($"{ptr}/inputs/attestation/seed_family", "Schema.r14_seed_family", "attestation.seed_family == execution.family (R-14).");
        if (ModeOf(family, unitId) is { } mode && Text(inputs["called_pitch_mode"]) != mode)
            throw Bad($"{ptr}/inputs/called_pitch_mode", "Schema.mode", $"{unitId}의 called_pitch_mode는 {mode}입니다.");

        // calibration-only fields
        if (calibration != !IsNull(inputs["block_size_games"]) || calibration != !IsNull(inputs["pa_target"]))
            throw Bad($"{ptr}/inputs", "Schema.calibration_fields", "block_size_games·pa_target은 Calibration에서만 non-null입니다.");
        if (calibration && Long(inputs["pa_target"]) < 1)
            throw Bad($"{ptr}/inputs/pa_target", "Schema.integer_range", "pa_target은 non-null이면 양수입니다.");

        var specs = Items(inputs["games"]).Select(Obj).ToList();
        if (specs.Count != Long(inputs["planned_games"]))
            throw Bad($"{ptr}/inputs/games", "Schema.plan", "inputs.games는 전체 승인 Seed 계획(planned_games개)입니다.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < specs.Count; i++)
        {
            if (i > 0 && Long(specs[i]["index"]) <= Long(specs[i - 1]["index"]))
                throw Bad($"{ptr}/inputs/games/{i}/index", "Schema.order", "GameSpec index는 승인 Seed 파일 순서(증가)입니다.");
            if (!ids.Add(Text(specs[i]["game_id"])))
                throw Bad($"{ptr}/inputs/games/{i}/game_id", "Schema.duplicate", "GameId가 중복됩니다.");
        }

        // Technical (§3.4) and R-14 counters / games_executed / metrics / block size
        var version = Text(technical["run_result_version"]);
        if (version != (calibration ? "run-result-v2" : "run-result-v1"))
            throw Bad($"{ptr}/technical/run_result_version", "Schema.result_version", "Calibration은 run-result-v2, Integration·Dominance는 run-result-v1입니다.");
        var games = Items(technical["games"]).Select(Obj).ToList();
        if (Long(technical["games_executed"]) != games.Count)
            throw Bad($"{ptr}/technical/games_executed", "Schema.r14_games_executed", "games_executed == games.length (R-14).");
        if (Long(technical["games_requested"]) != specs.Count)
            throw Bad($"{ptr}/technical/games_requested", "Schema.technical", "games_requested는 전체 계획 수입니다.");
        var completed = games.Count(g => Text(g["status"]) == "Completed");
        if (Long(technical["games_completed"]) != completed || Long(technical["games_failed"]) != games.Count - completed)
            throw Bad($"{ptr}/technical", "Schema.technical", "games_completed·games_failed가 games의 상태 수와 다릅니다.");
        if (games.Count > specs.Count)
            throw Bad($"{ptr}/technical/games", "Schema.technical", "실행 경기가 계획보다 많습니다.");
        for (var i = 0; i < games.Count; i++)
            if (Text(games[i]["game_id"]) != Text(specs[i]["game_id"]) || Long(games[i]["index"]) != Long(specs[i]["index"]) || Text(games[i]["seed"]) != Text(specs[i]["seed"]))
                throw Bad($"{ptr}/technical/games/{i}", "Evidence.game_prefix_mismatch", "technical.games는 승인 계획의 실행 Prefix 순서입니다.", EvidenceCodes.ReferenceInvalid);

        var stop = technical["stop"];
        if (calibration == IsNull(stop))
            throw Bad($"{ptr}/technical/stop", "Schema.stop", "Stop은 Calibration에서 항상 non-null, 그 밖에는 null입니다(R-4).");
        var unexecuted = Long(technical["games_unexecuted_after_stop"]);
        if (stop is JObject s)
        {
            if (Long(s["block_size"]) != Long(inputs["block_size_games"]))
                throw Bad($"{ptr}/technical/stop/block_size", "Schema.r14_block_size", "stop.block_size == inputs.block_size_games (R-14).");
            if (Long(s["executed_games"]) != games.Count || unexecuted != specs.Count - games.Count)
                throw Bad($"{ptr}/technical/stop", "Schema.stop", "stop.executed_games·games_unexecuted_after_stop가 실행 경기 수와 맞지 않습니다.");
        }
        else if (unexecuted != 0 || games.Count != specs.Count)
            throw Bad($"{ptr}/technical", "Schema.technical", "전체 실행 Unit은 계획 전체를 실행하며 games_unexecuted_after_stop은 0입니다.");

        var counters = Obj(technical["counters"]);
        if (Long(technical["plate_appearances"]) != Long(counters["plate_appearances"]) || Long(technical["pitches"]) != Long(counters["pitches"]))
            throw Bad($"{ptr}/technical", "Schema.r14_counters", "technical.plate_appearances·pitches == counters 값 (R-14).");
        foreach (var f in EvidenceShapes.CounterFields)
            if (Long(counters[f]) != games.Where(g => Text(g["status"]) == "Completed").Sum(g => Long(Obj(g["counters"])[f])))
                throw Bad($"{ptr}/technical/counters/{f}", "Schema.counters_aggregate", "technical.counters는 완료 경기 counters의 합입니다(§3.4).");
        if (Long(technical["committed_events"]) != games.Sum(g => Long(g["committed_events"])))
            throw Bad($"{ptr}/technical/committed_events", "Schema.technical", "committed_events는 실행 경기의 committed events 합입니다(실패 경기 포함).");
        if (technical["metrics_computed"] is JBool { Value: var mc } && mc == !IsNull(technical["metrics_error_code"]))
            throw Bad($"{ptr}/technical/metrics_error_code", "Schema.r14_metrics", "metrics_computed=false ⇔ metrics_error_code≠null (R-14).");

        var endKinds = games.Select(g => Text(g["end_kind"])).Where(k => k != "None").GroupBy(k => k, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (g.Key, (long)g.Count())).ToList();
        var recorded = Items(technical["end_kinds"]).Select(Obj).Select(e => (Text(e["end_kind"]), Long(e["count"]))).ToList();
        if (!endKinds.SequenceEqual(recorded))
            throw Bad($"{ptr}/technical/end_kinds", "Schema.end_kinds", "end_kinds는 None을 뺀 실행 경기 집계, end_kind ordinal 오름차순입니다.");
        var codes = games.Where(g => g["failure"] is JObject).Select(g => Text(Obj(g["failure"])["code"])).Distinct().Order(StringComparer.Ordinal).ToList();
        if (!Items(technical["error_codes"]).Select(Text).SequenceEqual(codes))
            throw Bad($"{ptr}/technical/error_codes", "Schema.error_codes", "error_codes는 Failure.code 중복 제거·ordinal 정렬입니다.");

        for (var i = 0; i < games.Count; i++)
            GameRow(games[i], $"{ptr}/technical/games/{i}", dominance);
    }

    /// <summary>§3.6 state table (P-06), rows A-D.</summary>
    internal static void GameRow(JObject g, string ptr, bool dominance)
    {
        var status = Text(g["status"]);
        var failure = g["failure"] as JObject;
        var outcome = g["outcome"];
        var endKind = Text(g["end_kind"]);
        var cross = g["box_score_cross_check"];
        var rng = g["rng_hash"];
        var halves = g["regulation_half_innings"];
        var regError = g["regulation_error_code"];
        EvidenceException Row(string row, string why) => Bad(ptr, "Schema.state_table", $"§3.6 {row}행 위반: {why}");

        if (status == "Completed")
        {
            if (failure is not null || !IsNull(g["error_code"]))
                throw Row("A", "Completed 경기는 failure·error_code가 null입니다.");
            if (IsNull(outcome) || endKind == "None" || cross is not JObject c || c["passed"] is not JBool { Value: true } || IsNull(rng))
                throw Row("A", "outcome non-null, end_kind≠None, CrossCheck passed=true, rng_hash H여야 합니다.");
            Regulation(true);
        }
        else
        {
            if (failure is null || g["error_code"] is not JString { Value: var code } || code != Text(failure["code"]))
                throw Row("B/C/D", "Failed 경기는 failure가 있고 error_code == failure.code입니다.");
            var stage = Text(failure["stage"]);
            if (stage is "cancelled")
                throw Row("-", "공식 문서에서는 stage=cancelled를 금지합니다.");
            if (stage is "config" or "start")
            {
                if (!IsNull(outcome) || endKind != "None" || !IsNull(cross) || !IsNull(rng) || !IsNull(failure["rng_hash"]) || Long(g["committed_events"]) != 0 || Long(g["final_inning"]) != 0
                    || Text(g["final_half"]) != "Top" || EvidenceShapes.CounterFields.Any(f => Long(Obj(g["counters"])[f]) != 0))
                    throw Row("D", "시작 전 실패는 outcome·rng null, end_kind None, committed_events·final_inning 0, final_half Top, Counters 모두 0입니다.");
                Regulation(false);
            }
            else if (!IsNull(outcome))
            {
                if (stage is not ("observe" or "progress" or "final" or "metrics" or "crosscheck" or "exception"))
                    throw Row("B", $"엔진 종료 상태의 실패에 stage '{stage}'는 허용되지 않습니다.");
                if (endKind == "None" || IsNull(rng))
                    throw Row("B", "end_kind≠None, rng_hash H여야 합니다.");
                if ((stage == "crosscheck") != cross is JObject || (cross is JObject cc && cc["passed"] is not JBool { Value: false }))
                    throw Row("B", "CrossCheck는 stage=crosscheck일 때만 non-null·passed=false입니다.");
                Regulation(true);
            }
            else
            {
                if (stage is not ("budget" or "advance" or "observe" or "progress" or "final" or "exception"))
                    throw Row("C", $"종료 전 실패에 stage '{stage}'는 허용되지 않습니다.");
                if (endKind != "None" || !IsNull(cross) || IsNull(rng))
                    throw Row("C", "end_kind None, CrossCheck null, rng_hash H여야 합니다.");
                Regulation(false);
            }

            if (Text(failure["rng_hash"] is JString f ? f : J.S("-")) != (rng is JString r ? r.Value : "-"))
                throw Row("B/C/D", "failure.rng_hash는 rng_hash와 같습니다.");
        }

        if (cross is JObject check)
        {
            var items = Items(check["items"]).Select(Obj).ToList();
            if (!items.Select(i => Text(i["name"])).SequenceEqual(EvidenceShapes.CrossCheckNames))
                throw Bad($"{ptr}/box_score_cross_check/items", "Schema.crosscheck_order", "CrossCheck 15항목은 고정 순서입니다.");
            if (((JBool)check["passed"]).Value != items.All(i => Long(i["from_events"]) == Long(i["from_box_score"])))
                throw Bad($"{ptr}/box_score_cross_check/passed", "Schema.crosscheck_passed", "passed는 모든 항목의 일치 여부와 같아야 합니다.");
        }

        void Regulation(bool engineFinal)
        {
            if (!dominance || !engineFinal)
            {
                if (!IsNull(halves) || !IsNull(regError))
                    throw Bad(ptr, "Schema.regulation", dominance ? "엔진 종료 전 경기의 Regulation 두 필드는 null입니다." : "Family 문서는 Regulation 두 필드를 항상 null로 투영합니다.");
                return;
            }

            if (IsNull(halves) == IsNull(regError))
                throw Bad(ptr, "Schema.regulation", "Regulation half_innings·error_code 중 정확히 하나만 non-null입니다.");
        }
    }

    /// <summary>§3.7-§3.8 (R-5).</summary>
    private static void Evaluation(JObject e, string ptr, bool calibration, string unitId)
    {
        if (Text(e["kind"]) != (calibration ? "CALIBRATION" : "INTEGRATION"))
            throw Bad($"{ptr}/kind", "Schema.evaluation_kind", "kind는 Calibration이면 CALIBRATION, Integration이면 INTEGRATION입니다.");
        var computed = ((JBool)e["evaluation_computed"]).Value;
        var metrics = Items(e["metrics"]).Select(Obj).ToList();
        if (!calibration)
        {
            if (!IsNull(e["policy_id"]) || !IsNull(e["policy_hash"]) || !IsNull(e["shares"]) || metrics.Count != 0)
                throw Bad(ptr, "Schema.r5", "INTEGRATION은 policy_id·policy_hash·shares null, metrics []입니다(R-5).");
            return;
        }

        if (e["policy_id"] is not JString { Value: var id } || id != EvaluationPolicy.FcEvaluationV1.Id || e["policy_hash"] is not JString { Value: var hash } || hash != EvaluationPolicy.FcEvaluationV1.Hash())
            throw Bad(ptr, "Schema.r5", "CALIBRATION의 policy_id·policy_hash는 계산 성공 여부와 관계없이 FcEvaluationV1 값입니다(R-5).");
        if (computed == IsNull(e["shares"]))
            throw Bad($"{ptr}/shares", "Schema.r5", "shares는 계산 성공 시에만 non-null입니다.");
        var expected = CalibrationStrata.All.FirstOrDefault(s => s.Id == unitId)?.OfficialMetrics ?? [];
        if (!metrics.Select(m => Text(m["f_id"])).SequenceEqual(computed ? expected : []))
            throw Bad($"{ptr}/metrics", "Schema.r5", $"metrics는 계산 성공 시 공식 지표 [{string.Join(",", expected)}]이고 실패 시 []입니다.");
        for (var i = 0; i < metrics.Count; i++)
        {
            var m = metrics[i];
            var mp = $"{ptr}/metrics/{i}";
            var standardized = Text(m["method"]) == "standardized_3_7_4_7";
            if (standardized != EvaluationPolicy.FcEvaluationV1.StandardizedMetrics.Contains(Text(m["f_id"])))
                throw Bad($"{mp}/method", "Schema.r5", "method=standardized ⇔ 정책의 StandardizedMetrics 포함입니다.");
            if (IsNull(m["min_same"]) != IsNull(m["min_opposite"]) || (!standardized && !IsNull(m["min_same"])))
                throw Bad(mp, "Schema.r5", "min_same·min_opposite는 둘 다 null 또는 둘 다 non-null이고 observed 지표는 null입니다.");
            var zero = standardized ? Long(m["denominator_same"]) == 0 || Long(m["denominator_opposite"]) == 0 : Long(m["denominator"]) == 0;
            if (IsNull(m["measured_ppm"]) != zero)
                throw Bad($"{mp}/measured_ppm", "Schema.r5", "measured_ppm=null ⇔ 해당 분모가 0입니다.");
            if (Long(m["target_high_ppm"]) < Long(m["target_low_ppm"]))
                throw Bad($"{mp}/target_high_ppm", "Schema.r5", "target_high_ppm ≥ target_low_ppm.");
        }
    }

    // ---------------------------------------------------------------- Dominance (§4, R-7)

    private static void DominanceRules(JObject doc)
    {
        var cells = DominanceMatrix.Cells;
        var planIds = Items(Obj(doc["plan"])["cell_ids"]).Select(Text).ToList();
        if (!planIds.SequenceEqual(cells.Select(c => c.Id)))
            throw Bad("/plan/cell_ids", "Schema.plan", "plan.cell_ids는 승인 카탈로그 48개 셀의 실행 순서입니다.");
        var perCell = Long(Obj(doc["plan"])["games_per_cell"]);
        var units = Items(doc["units"]).Select(Obj).ToList();
        for (var i = 0; i < units.Count; i++)
        {
            var c = cells[i];
            var cell = Obj(units[i]["cell"]);
            (string, string)[] expected =
            [
                ("id", c.Id), ("batter_strategy", c.Batter.ToString()), ("pitcher_strategy", c.Pitcher.ToString()), ("hand", c.Hand.ToString()), ("called_pitch_mode", c.Mode.ToString()),
                ("cohort_id", c.CohortId), ("policy_id", c.PolicyId), ("competition_key", c.CompetitionKey),
            ];
            foreach (var (name, value) in expected)
                if (Text(cell[name]) != value)
                    throw Bad($"/units/{i}/cell/{name}", "Evidence.cell_catalog_mismatch", $"{c.Id}의 {name}는 카탈로그 값 '{value}'입니다.", EvidenceCodes.ReferenceInvalid);
            var inputs = Obj(units[i]["inputs"]);
            if (Long(inputs["planned_games"]) != perCell)
                throw Bad($"/units/{i}/inputs/planned_games", "Schema.plan", "셀의 planned_games == plan.games_per_cell입니다.");
            UnitResult(doc, units[i], $"/units/{i}", "fc3-dom", c.Id, dominance: true);
        }

        var e = Obj(doc["aggregate_evaluation"]);
        if (Text(e["policy_id"]) != DominanceEvaluator.PolicyId)
            throw Bad("/aggregate_evaluation/policy_id", "Schema.policy", $"policy_id는 {DominanceEvaluator.PolicyId}입니다.");
        if (Long(e["frozen_games_per_cell"]) != perCell)
            throw Bad("/aggregate_evaluation/frozen_games_per_cell", "Schema.r7", "frozen_games_per_cell == plan.games_per_cell (R-7).");
        var cellEvals = Items(e["cells"]).Select(Obj).ToList();
        for (var i = 0; i < cellEvals.Count; i++)
        {
            if (Text(cellEvals[i]["cell_id"]) != cells[i].Id)
                throw Bad($"/aggregate_evaluation/cells/{i}/cell_id", "Schema.plan", "cells는 계획 순서입니다.");
            if ((Text(cellEvals[i]["state"]) == "Ok") == IsNull(cellEvals[i]["sample"]))
                throw Bad($"/aggregate_evaluation/cells/{i}/sample", "Schema.r7", "sample non-null ⇔ state=Ok (R-7).");
        }

        var batter = Side(Obj(e["batter"]), "/aggregate_evaluation/batter", "Batter", ["Normal", "Patient", "Aggressive", "TakeFirst"], 36);
        var pitcher = Side(Obj(e["pitcher"]), "/aggregate_evaluation/pitcher", "Pitcher", ["Balanced", "Attack", "Careful"], 32);
        var gate = Text(Obj(e["gate"])["status"]);
        var incomplete = batter is not ("DOMINANCE_DETECTED" or "NOT_EVALUATED") && pitcher is not ("DOMINANCE_DETECTED" or "NOT_EVALUATED") && (batter == "INCONCLUSIVE" || pitcher == "INCONCLUSIVE");
        if ((gate == "INCOMPLETE") != incomplete)
            throw Bad("/aggregate_evaluation/gate/status", "Schema.r7", "gate=INCOMPLETE ⇔ 어느 쪽도 DOMINANCE_DETECTED·NOT_EVALUATED가 아니고 한쪽 이상 INCONCLUSIVE (R-7).");
    }

    private static string Side(JObject side, string ptr, string name, string[] order, int comparisons)
    {
        if (Text(side["side"]) != name)
            throw Bad($"{ptr}/side", "Schema.r7", $"side는 {name}입니다.");
        var verdict = Text(side["verdict"]);
        var strategies = Items(side["strategies"]).Select(Obj).ToList();
        if ((verdict == "NOT_EVALUATED") != (strategies.Count == 0))
            throw Bad($"{ptr}/strategies", "Schema.r7", "verdict=NOT_EVALUATED ⇔ strategies=[] (R-7).");
        if (strategies.Count == 0)
            return verdict;
        if (!strategies.Select(s => Text(s["strategy"])).SequenceEqual(order))
            throw Bad($"{ptr}/strategies", "Schema.r7", $"전략 순서는 {string.Join(", ", order)}입니다.");
        for (var i = 0; i < strategies.Count; i++)
        {
            var s = strategies[i];
            bool dominant = ((JBool)s["dominant"]).Value, notDominant = ((JBool)s["not_dominant"]).Value;
            if (Text(s["label"]) != (dominant ? "DOMINANT" : notDominant ? "NOT_DOMINANT" : "UNDETERMINED"))
                throw Bad($"{ptr}/strategies/{i}/label", "Schema.r7", "label은 dominant → DOMINANT, not_dominant → NOT_DOMINANT, 그 밖 UNDETERMINED입니다.");
            var witnesses = Items(s["witnesses"]).Select(Obj).ToList();
            if (witnesses.Count != comparisons)
                throw Bad($"{ptr}/strategies/{i}/witnesses", "Schema.r7", $"비교 수는 {comparisons}개입니다.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var j = 0; j < witnesses.Count; j++)
            {
                var w = witnesses[j];
                if (!keys.Add(Text(w["rival"]) + "|" + Text(w["column"])))
                    throw Bad($"{ptr}/strategies/{i}/witnesses/{j}", "Schema.duplicate", "같은 (rival, column) Witness가 중복됩니다.");
                if (Text(Obj(w["holm_critical"])["state"]) == "NOT_APPLICABLE" && ((JBool)w["holm_rejected"]).Value)
                    throw Bad($"{ptr}/strategies/{i}/witnesses/{j}/holm_rejected", "Schema.r7", "holm_critical이 NOT_APPLICABLE이면 holm_rejected=false입니다.");
            }
        }

        return verdict;
    }

    // ---------------------------------------------------------------- Unit PZL (§5, §7)

    private static void UnitPzlRules(JObject doc)
    {
        var family = Family(doc);
        var unitId = Text(Obj(doc["plan"])["unit_id"]);
        if (ModeOf(family, unitId) is not { } mode)
            throw Bad("/plan/unit_id", "Evidence.unit_unknown", $"'{unitId}'는 {family}의 Unit이 아닙니다.", EvidenceCodes.ReferenceInvalid);
        if (!Items(doc["registered_streams"]).Select(Text).SequenceEqual(EvidenceShapes.RegisteredStreams))
            throw Bad("/registered_streams", "Schema.registered_streams", "registered_streams는 고정 7개 Stream, 고정 순서입니다.");
        var games = Items(doc["games"]).Select(Obj).ToList();
        for (var i = 0; i < games.Count; i++)
        {
            var g = games[i];
            var ptr = $"/games/{i}";
            if (Long(g["index"]) != i + 1)
                throw Bad($"{ptr}/index", "Schema.order", "games는 부모 실행 순서의 연속 index입니다.");
            if (IsNull(g["rng_hash"]) != IsNull(g["final_rng_canonical"]))
                throw Bad(ptr, "Schema.rng_boundary", "rng_hash와 final_rng_canonical의 null 여부는 같습니다(§10.4 A).");
            if (IsNull(g["rng_hash"]) && (Text(g["status"]) != "Failed" || Long(g["committed_events"]) != 0 || Items(Obj(g["summary"])["drawn_streams"]).Count != 0))
                throw Bad(ptr, "Schema.rng_boundary", "rng_hash=null은 시작 전 실패(A 경우: Failed, committed_events 0, drawn_streams [])뿐입니다.");
            var streams = Items(Obj(g["summary"])["drawn_streams"]).Select(Text).ToList();
            for (var j = 1; j < streams.Count; j++)
                if (string.CompareOrdinal(streams[j - 1], streams[j]) >= 0)
                    throw Bad($"{ptr}/summary/drawn_streams/{j}", "Schema.order", "drawn_streams는 ordinal 오름차순·중복 금지입니다.");
            if (Long(Obj(g["summary"])["events"]) != Long(g["committed_events"]))
                throw Bad($"{ptr}/summary/events", "Evidence.pzl_summary_mismatch", "summary.events는 committed_events와 같습니다.", EvidenceCodes.PzlMismatch);
        }

        var checks = Items(doc["checks"]).Select(Obj).ToList();
        string[] ids = ["pzl-5", "pzl-10", "pzl-12a", "pzl-12b"];
        for (var i = 0; i < 4; i++)
        {
            var c = checks[i];
            var ptr = $"/checks/{i}";
            if (Text(c["id"]) != ids[i])
                throw Bad($"{ptr}/id", "Schema.order", $"checks 순서는 {string.Join(", ", ids)}입니다.");
            var status = Text(c["status"]);
            var allowed = ids[i] switch
            {
                "pzl-12b" => status == "DEFERRED",
                "pzl-10" when mode == "NonAutomated" => status == "NOT_APPLICABLE",
                _ => status is "PASS" or "FAIL" or "NOT_EVALUATED",
            };
            if (!allowed)
                throw Bad($"{ptr}/status", "Schema.r9", $"{ids[i]}({mode})에 status '{status}'는 허용되지 않습니다(R-9).");
            if (!IsNull(c["test_evidence"]))
                throw Bad($"{ptr}/test_evidence", "Schema.r9", "Unit PZL Check의 test_evidence는 null입니다(R-9).");
            if ((ids[i] == "pzl-12b") == IsNull(c["deferral"]))
                throw Bad($"{ptr}/deferral", "Schema.r9", "deferral은 pzl-12b에서만 non-null입니다.");
            Violations(c, ptr);
            var cov = Obj(c["coverage"]);
            if (ids[i] == "pzl-12b")
            {
                if (EvidenceShapes.CoverageFields.Any(f => Long(cov[f]) != 0))
                    throw Bad($"{ptr}/coverage", "Evidence.coverage_scope", "pzl-12b의 coverage 네 필드는 모두 0입니다(§7).", EvidenceCodes.PzlMismatch);
                continue;
            }

            // what the document can show about itself: completed games, committed events and called-pitch steps of every executed game (pitches are re-observed in T06b)
            if (Long(cov["completed_games"]) != games.Count(g => Text(g["status"]) == "Completed") || Long(cov["events"]) != games.Sum(g => Long(g["committed_events"]))
                || Long(cov["called_pitch_steps"]) != games.Sum(g => Long(Obj(g["summary"])["called_pitch_steps"])))
                throw Bad($"{ptr}/coverage", "Evidence.coverage_scope", "coverage는 Unit의 실행된 모든 경기 범위입니다(Completed 수·확정 사건 수·called-pitch Step 수, §7).", EvidenceCodes.PzlMismatch);
        }
    }

    internal static void Violations(JObject check, string ptr)
    {
        var v = Items(check["violations"]).Select(Obj).Select(x => (G: x["game_id"] as JString, E: x["event_id"] as JString, F: x["field"] as JString, C: Text(x["code"]))).ToList();
        static int Cmp(JString? a, JString? b) => a is null ? (b is null ? 0 : -1) : b is null ? 1 : string.CompareOrdinal(a.Value, b.Value);
        for (var i = 1; i < v.Count; i++)
        {
            var c = Cmp(v[i - 1].G, v[i].G);
            if (c == 0) c = Cmp(v[i - 1].E, v[i].E);
            if (c == 0) c = Cmp(v[i - 1].F, v[i].F);
            if (c == 0) c = string.CompareOrdinal(v[i - 1].C, v[i].C);
            if (c >= 0)
                throw Bad($"{ptr}/violations/{i}", "Schema.order", "violations는 (game_id, event_id, field, code) ordinal 순서(null 먼저)·중복 금지입니다.");
        }
    }

    // ---------------------------------------------------------------- PZL #6 (§6)

    private static void Pzl6Rules(JObject doc)
    {
        var vs = Text(Obj(doc["execution"])["validation_set_id"]);
        if (Text(Obj(doc["plan"])["integration_evidence_id"]) != EvidenceIds.Result(vs, "fc3-int"))
            throw Bad("/plan/integration_evidence_id", "Evidence.integration_id_mismatch", "integration_evidence_id는 같은 검증 집합의 Integration 증거 ID입니다.", EvidenceCodes.ReferenceInvalid);
        var samples = Items(doc["samples"]).Select(Obj).ToList();
        for (var i = 0; i < 20; i++)
        {
            var unit = i < 10 ? "automated" : "non-automated";
            if (Text(samples[i]["unit_id"]) != unit || Long(samples[i]["index"]) != i % 10 + 1)
                throw Bad($"/samples/{i}", "Evidence.pzl6_sample_fixed", $"{i + 1}번째 표본은 {unit}의 {i % 10 + 1}번째 경기입니다(FIRST_10_PER_MODE, 교체 금지).", EvidenceCodes.PzlMismatch);
        }

        var check = Obj(doc["check"]);
        if (Text(check["id"]) != "pzl-6" || Text(check["status"]) is not ("PASS" or "FAIL") || !IsNull(check["deferral"]) || !IsNull(check["test_evidence"]))
            throw Bad("/check", "Schema.r9", "#6 문서의 check는 id pzl-6, PASS/FAIL, test_evidence·deferral null입니다.");
        Violations(check, "/check");
    }
}

/// <summary>R-12 evidence ids (§2.2).</summary>
public static class EvidenceIds
{
    public static string Result(string vs, string family) => $"{vs}.{family}.result";

    public static string UnitPzl(string vs, string family, string unitId) => $"{vs}.{family}.pzl.{unitId.ToLowerInvariant()}";

    public static string Pzl6(string vs) => $"{vs}.fc3-int.pzl6";

    public static string Phase(string vs) => $"{vs}.phase";
}
