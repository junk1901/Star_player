# B-1 필드 단위 Traceability (생성 문서)

- 생성: `python -I tools/b1-golden/fields.py > docs/contracts/b1/B1_FIELD_TRACEABILITY.md` (입력: `tests/Fixtures/b1-golden/`의 TEST_ONLY 실제 문서). 손으로 고치지 않습니다.
- 경로의 `*`는 배열 원소입니다. '관찰 타입'은 Golden 문서에 실제 나타난 JSON 타입이며, 계약상 허용 타입·Null 조건의 정본은 계약 본문과 `EvidenceShapes`입니다(관찰되지 않은 타입은 표에 없을 수 있음).
- 시험 약어는 [B1_TRACEABILITY.md](B1_TRACEABILITY.md)와 같고, `CX` = `CounterexampleTests`(하네스), `VX` = `VerifierCounterexampleTests`(검증기), `BP`·`PR` = `BootstrapAndProfileTests`·`PipeRegistryTests`입니다.

## FAMILY

| JSON 경로 | 관찰 타입 | 계약 조항 | 검사 위치 | 시험 |
|---|---|---|---|---|
| `/additional_evidence` | object | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/additional_evidence/artifacts` | array | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*` | object | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/artifact_id` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/byte_length` | number | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/game_id` | null | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/raw_sha256` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/relative_path` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/role` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/unit_id` | null, string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/pzl6_id` | null, string | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/additional_evidence/pzl_unit_ids` | array | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/additional_evidence/pzl_unit_ids/*` | string | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/aggregate_evaluation` | object | §3.7·§3.10 | `EvidenceRules.Evaluation`, 재평가 `Verifier.Compare` | OD, VT (A05) |
| `/aggregate_evaluation/reason` | string | §3.7·§3.10 | `EvidenceRules.Evaluation`, 재평가 `Verifier.Compare` | OD, VT (A05) |
| `/aggregate_evaluation/status` | string | §3.7·§3.10 | `EvidenceRules.Evaluation`, 재평가 `Verifier.Compare` | OD, VT (A05) |
| `/approvals` | object | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/approvals/approval_record_id` | string | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/approvals/approval_record_sha256` | string | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/approvals/freeze_id` | string | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/execution` | object | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/build_provenance_id` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/evidence_id` | string | §2.2 R-12 | `EvidenceIds`, `EvidenceRules.Header` | OD (N08) |
| `/execution/family` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/git_commit_sha` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/run_id` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/validation_set_id` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/plan` | object | §3.1 | `EvidenceRules.Validate` (Family Root), `Verifier.BindUnits` | OD, VX (A04) |
| `/plan/combination_policy_clause_id` | string | §3.1 | `EvidenceRules.Validate` (Family Root), `Verifier.BindUnits` | OD, VX (A04) |
| `/plan/unit_ids` | array | §3.1 | `EvidenceRules.Validate` (Family Root), `Verifier.BindUnits` | OD, VX (A04) |
| `/plan/unit_ids/*` | string | §3.1 | `EvidenceRules.Validate` (Family Root), `Verifier.BindUnits` | OD, VX (A04) |
| `/schema` | object | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts` | object | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/dominance` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/family` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/hash` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/pzl6` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/pzl_unit` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/id` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/version` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/units` | array | §3.1 (Unit 순서 = plan.unit_ids) | `EvidenceRules.Validate`, `Verifier.BindUnits` | OD |
| `/units/*` | object | §3.1 (Unit 순서 = plan.unit_ids) | `EvidenceRules.Validate`, `Verifier.BindUnits` | OD |
| `/units/*/evaluation` | object | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/error_code` | null | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/evaluation_computed` | bool | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/gate` | object | §3.10 | `EvidenceRules.Evaluation` | OD, VT (A05) |
| `/units/*/evaluation/gate/reason` | string | §3.10 | `EvidenceRules.Evaluation` | OD, VT (A05) |
| `/units/*/evaluation/gate/status` | string | §3.10 | `EvidenceRules.Evaluation` | OD, VT (A05) |
| `/units/*/evaluation/kind` | string | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/metrics` | array | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*` | object | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/denominator` | number | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/denominator_opposite` | number | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/denominator_same` | number | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/f_id` | string | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/gate` | object | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/gate/reason` | string | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/gate/status` | string | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/measured_ppm` | number | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/method` | string | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/min_denominator` | number | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/min_opposite` | null, number | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/min_same` | null, number | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/numerator` | number | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/numerator_opposite` | number | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/numerator_same` | number | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/target_high_ppm` | number | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/target_id` | string | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/metrics/*/target_low_ppm` | number | §3.8 | `EvidenceRules.Evaluation`, `OfficialDocuments.UnitEvaluation` | OD, GD (Calibration) |
| `/units/*/evaluation/policy_hash` | null, string | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/policy_id` | null, string | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/shares` | null, object | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/shares/mixed_games` | number | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/shares/opposite_games` | number | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/shares/opposite_pa` | number | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/shares/opposite_pitches` | number | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/shares/opposite_swings` | number | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/shares/same_games` | number | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/shares/same_pa` | number | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/shares/same_pitches` | number | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/evaluation/shares/same_swings` | number | §3.7 R-5 | `EvidenceRules.Evaluation` | OD |
| `/units/*/inputs` | object | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/attestation` | object | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/advancement_policy_version` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/cohort_id` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/cohort_inputs_hash` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/competition_key` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/config_hash` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/event_budget` | number | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/execution_policy_hash` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/execution_policy_id` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/game_id_rule` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/git_commit_sha` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/model_version` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/park_snapshot` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/policy_id` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/rules_hash` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/rules_profile_id` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/schema_version` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/seed_family` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/seed_manifest_path` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/seed_manifest_sha256` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/tuning_hash` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/tuning_version` | string | §3.3 | `EvidenceShapes.Attestation`, `OfficialDocuments.Inputs`, 재투영 `Verifier.Compare` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation_hash` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/block_size_games` | null, number | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/called_pitch_mode` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/cohort_kind` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/games` | array | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/games/*` | object | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/games/*/game_id` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/games/*/index` | number | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/games/*/seed` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/mode` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/pa_target` | null, number | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/planned_games` | number | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/player_input_set_content_version` | number | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/player_input_set_id` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/rules_status` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/rules_version` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/unit_run_id` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/technical` | object | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/committed_events` | number | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/counters` | object | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/at_bats` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/called_balls` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/called_strikes` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/doubles` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/duplicates_ignored` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/event_hits` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/event_home_runs` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/events` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/fouls` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/hit_by_pitch` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/hit_by_pitch_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/hits` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/home_runs` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/in_play_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/located_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/plate_appearances` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/sacrifice_bunts` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/sacrifice_flies` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/singles` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/strikeouts` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/swinging_strikes` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/swings` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/takes` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/triples` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/unlocated_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/walks` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/counters/zone_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD (A22) |
| `/units/*/technical/end_kinds` | array | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/end_kinds/*` | object | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/end_kinds/*/count` | number | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/end_kinds/*/end_kind` | string | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/error_codes` | array | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/games` | array | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*` | object | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/away_runs` | number | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/box_score_cross_check` | object | §3.6 | `EvidenceRules.GameRow` (CrossCheck 15항목) | OD (N03) |
| `/units/*/technical/games/*/box_score_cross_check/items` | array | §3.6 | `EvidenceRules.GameRow` (CrossCheck 15항목) | OD (N03) |
| `/units/*/technical/games/*/box_score_cross_check/items/*` | object | §3.6 | `EvidenceRules.GameRow` (CrossCheck 15항목) | OD (N03) |
| `/units/*/technical/games/*/box_score_cross_check/items/*/from_box_score` | number | §3.6 | `EvidenceRules.GameRow` (CrossCheck 15항목) | OD (N03) |
| `/units/*/technical/games/*/box_score_cross_check/items/*/from_events` | number | §3.6 | `EvidenceRules.GameRow` (CrossCheck 15항목) | OD (N03) |
| `/units/*/technical/games/*/box_score_cross_check/items/*/name` | string | §3.6 | `EvidenceRules.GameRow` (CrossCheck 15항목) | OD (N03) |
| `/units/*/technical/games/*/box_score_cross_check/passed` | bool | §3.6 | `EvidenceRules.GameRow` (CrossCheck 15항목) | OD (N03) |
| `/units/*/technical/games/*/committed_events` | number | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/counters` | object | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/at_bats` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/called_balls` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/called_strikes` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/doubles` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/duplicates_ignored` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/event_hits` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/event_home_runs` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/events` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/fouls` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/hit_by_pitch` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/hit_by_pitch_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/hits` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/home_runs` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/in_play_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/located_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/plate_appearances` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/sacrifice_bunts` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/sacrifice_flies` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/singles` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/strikeouts` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/swinging_strikes` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/swings` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/takes` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/triples` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/unlocated_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/walks` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/zone_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/end_kind` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/error_code` | null | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/event_hash` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/failure` | null | §3.6 B~D행 | `EvidenceRules.GameRow` | OD (N03), CX (N33·N34) |
| `/units/*/technical/games/*/final_half` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/final_inning` | number | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/game_id` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/home_runs` | number | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/index` | number | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/outcome` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/regulation_error_code` | null | §3.6 A행 | `EvidenceRules.GameRow` | OD, CX (N02) |
| `/units/*/technical/games/*/regulation_half_innings` | null | §3.6 A행 | `EvidenceRules.GameRow` | OD, CX (N02) |
| `/units/*/technical/games/*/result_hash` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/rng_hash` | string | §3.6 D행·§10.7 | `EvidenceRules.GameRow`, `OfficialDocuments.Game` | OD, CX (N01·N33·N34) |
| `/units/*/technical/games/*/seed` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games/*/status` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | OD (A06), VT (A06), CX (N01) |
| `/units/*/technical/games_cancelled` | number | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/games_completed` | number | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/games_executed` | number | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/games_failed` | number | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/games_not_started` | number | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/games_requested` | number | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/games_unexecuted_after_stop` | number | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/metrics_computed` | bool | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/metrics_error_code` | null | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/pitches` | number | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/plate_appearances` | number | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/run_result_hash` | string | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/run_result_version` | string | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/run_status` | string | §3.4 | `EvidenceRules.UnitResult`, `OfficialDocuments.Technical` | OD (A06), VT (A06) |
| `/units/*/technical/stop` | null, object | §3.4 | `EvidenceRules.UnitResult` | OD, VT (Calibration SEEDS_EXHAUSTED) |
| `/units/*/technical/stop/block_size` | number | §3.4 | `EvidenceRules.UnitResult` | OD, VT (Calibration SEEDS_EXHAUSTED) |
| `/units/*/technical/stop/cumulative_pa` | number | §3.4 | `EvidenceRules.UnitResult` | OD, VT (Calibration SEEDS_EXHAUSTED) |
| `/units/*/technical/stop/executed_games` | number | §3.4 | `EvidenceRules.UnitResult` | OD, VT (Calibration SEEDS_EXHAUSTED) |
| `/units/*/technical/stop/stop_reason` | string | §3.4 | `EvidenceRules.UnitResult` | OD, VT (Calibration SEEDS_EXHAUSTED) |
| `/units/*/unit_id` | string | §3.1 | `EvidenceRules.Validate` | OD |

## DOMINANCE

| JSON 경로 | 관찰 타입 | 계약 조항 | 검사 위치 | 시험 |
|---|---|---|---|---|
| `/additional_evidence` | object | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/additional_evidence/artifacts` | array | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*` | object | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/artifact_id` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/byte_length` | number | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/game_id` | null | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/raw_sha256` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/relative_path` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/role` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/unit_id` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/pzl6_id` | null | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/additional_evidence/pzl_unit_ids` | array | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/additional_evidence/pzl_unit_ids/*` | string | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/aggregate_evaluation` | object | §4 | `OfficialDocuments.DominanceAggregate`, 재평가 `Verifier.Compare` | VT (A05) |
| `/aggregate_evaluation/batter` | object | §4 SideResult·R-7 | `EvidenceRules.Side` | OD, VT |
| `/aggregate_evaluation/batter/reason` | string | §4 SideResult·R-7 | `EvidenceRules.Side` | OD, VT |
| `/aggregate_evaluation/batter/side` | string | §4 SideResult·R-7 | `EvidenceRules.Side` | OD, VT |
| `/aggregate_evaluation/batter/strategies` | array | §4 SideResult·R-7 | `EvidenceRules.Side` | OD, VT |
| `/aggregate_evaluation/batter/verdict` | string | §4 SideResult·R-7 | `EvidenceRules.Side` | OD, VT |
| `/aggregate_evaluation/cells` | array | §4 | `EvidenceRules.DominanceRules` | OD |
| `/aggregate_evaluation/cells/*` | object | §4 | `EvidenceRules.DominanceRules` | OD |
| `/aggregate_evaluation/cells/*/cell_id` | string | §4 | `EvidenceRules.DominanceRules` | OD |
| `/aggregate_evaluation/cells/*/config_hash` | string | §4 | `EvidenceRules.DominanceRules` | OD |
| `/aggregate_evaluation/cells/*/failed_games` | number | §4 | `EvidenceRules.DominanceRules` | OD |
| `/aggregate_evaluation/cells/*/metrics_failures` | number | §4 | `EvidenceRules.DominanceRules` | OD |
| `/aggregate_evaluation/cells/*/planned_games` | number | §4 | `EvidenceRules.DominanceRules` | OD |
| `/aggregate_evaluation/cells/*/reason` | string | §4 | `EvidenceRules.DominanceRules` | OD |
| `/aggregate_evaluation/cells/*/run_result_hash` | string | §4 | `EvidenceRules.DominanceRules` | OD |
| `/aggregate_evaluation/cells/*/sample` | object | §4·§8.4 (F64) | `EvidenceRules.Side`, `EvidenceScalars.F64` | OD (A24) |
| `/aggregate_evaluation/cells/*/sample/games` | number | §4·§8.4 (F64) | `EvidenceRules.Side`, `EvidenceScalars.F64` | OD (A24) |
| `/aggregate_evaluation/cells/*/sample/mean` | object | §4·§8.4 (F64) | `EvidenceRules.Side`, `EvidenceScalars.F64` | OD (A24) |
| `/aggregate_evaluation/cells/*/sample/mean/reason` | null | §4·§8.4 (F64) | `EvidenceRules.Side`, `EvidenceScalars.F64` | OD (A24) |
| `/aggregate_evaluation/cells/*/sample/mean/state` | string | §4·§8.4 (F64) | `EvidenceRules.Side`, `EvidenceScalars.F64` | OD (A24) |
| `/aggregate_evaluation/cells/*/sample/mean/value` | string | §4·§8.4 (F64) | `EvidenceRules.Side`, `EvidenceScalars.F64` | OD (A24) |
| `/aggregate_evaluation/cells/*/sample/sum_of_squares` | number | §4·§8.4 (F64) | `EvidenceRules.Side`, `EvidenceScalars.F64` | OD (A24) |
| `/aggregate_evaluation/cells/*/sample/total_runs` | number | §4·§8.4 (F64) | `EvidenceRules.Side`, `EvidenceScalars.F64` | OD (A24) |
| `/aggregate_evaluation/cells/*/sample/variance` | object | §4·§8.4 (F64) | `EvidenceRules.Side`, `EvidenceScalars.F64` | OD (A24) |
| `/aggregate_evaluation/cells/*/sample/variance/reason` | null | §4·§8.4 (F64) | `EvidenceRules.Side`, `EvidenceScalars.F64` | OD (A24) |
| `/aggregate_evaluation/cells/*/sample/variance/state` | string | §4·§8.4 (F64) | `EvidenceRules.Side`, `EvidenceScalars.F64` | OD (A24) |
| `/aggregate_evaluation/cells/*/sample/variance/value` | string | §4·§8.4 (F64) | `EvidenceRules.Side`, `EvidenceScalars.F64` | OD (A24) |
| `/aggregate_evaluation/cells/*/state` | string | §4 | `EvidenceRules.DominanceRules` | OD |
| `/aggregate_evaluation/frozen_games_per_cell` | number | §4 R-7 | `EvidenceRules.DominanceRules` | OD (Schema.r7) |
| `/aggregate_evaluation/gate` | object | §4 | `OfficialDocuments.DominanceAggregate`, 재평가 `Verifier.Compare` | VT (A05) |
| `/aggregate_evaluation/gate/reason` | string | §4 | `OfficialDocuments.DominanceAggregate`, 재평가 `Verifier.Compare` | VT (A05) |
| `/aggregate_evaluation/gate/status` | string | §4 | `OfficialDocuments.DominanceAggregate`, 재평가 `Verifier.Compare` | VT (A05) |
| `/aggregate_evaluation/pitcher` | object | §4 SideResult·R-7 | `EvidenceRules.Side` | OD, VT |
| `/aggregate_evaluation/pitcher/reason` | string | §4 SideResult·R-7 | `EvidenceRules.Side` | OD, VT |
| `/aggregate_evaluation/pitcher/side` | string | §4 SideResult·R-7 | `EvidenceRules.Side` | OD, VT |
| `/aggregate_evaluation/pitcher/strategies` | array | §4 SideResult·R-7 | `EvidenceRules.Side` | OD, VT |
| `/aggregate_evaluation/pitcher/verdict` | string | §4 SideResult·R-7 | `EvidenceRules.Side` | OD, VT |
| `/aggregate_evaluation/policy_id` | string | §4 | `OfficialDocuments.DominanceAggregate`, 재평가 `Verifier.Compare` | VT (A05) |
| `/aggregate_evaluation/set_problems` | array | §4 | `OfficialDocuments.DominanceAggregate`, 재평가 `Verifier.Compare` | VT (A05) |
| `/approvals` | object | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/approvals/approval_record_id` | string | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/approvals/approval_record_sha256` | string | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/approvals/freeze_id` | string | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/execution` | object | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/build_provenance_id` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/evidence_id` | string | §2.2 R-12 | `EvidenceIds`, `EvidenceRules.Header` | OD (N08) |
| `/execution/family` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/git_commit_sha` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/run_id` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/validation_set_id` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/plan` | object | §4 | `EvidenceRules.DominanceRules`, `Verifier.BindUnits` | OD, VX (A04) |
| `/plan/cell_ids` | array | §4 | `EvidenceRules.DominanceRules`, `Verifier.BindUnits` | OD, VX (A04) |
| `/plan/cell_ids/*` | string | §4 | `EvidenceRules.DominanceRules`, `Verifier.BindUnits` | OD, VX (A04) |
| `/plan/games_per_cell` | number | §4 | `EvidenceRules.DominanceRules`, `Verifier.BindUnits` | OD, VX (A04) |
| `/schema` | object | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts` | object | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/dominance` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/family` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/hash` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/pzl6` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/pzl_unit` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/id` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/version` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/units` | array | §4 (Cell 순서 = plan.cell_ids) | `EvidenceRules.DominanceRules`, `Verifier.BindUnits` | OD |
| `/units/*` | object | §4 (Cell 순서 = plan.cell_ids) | `EvidenceRules.DominanceRules`, `Verifier.BindUnits` | OD |
| `/units/*/cell` | object | §4 (카탈로그) | `EvidenceRules.DominanceRules` | OD (카탈로그) |
| `/units/*/cell/batter_strategy` | string | §4 (카탈로그) | `EvidenceRules.DominanceRules` | OD (카탈로그) |
| `/units/*/cell/called_pitch_mode` | string | §4 (카탈로그) | `EvidenceRules.DominanceRules` | OD (카탈로그) |
| `/units/*/cell/cohort_id` | string | §4 (카탈로그) | `EvidenceRules.DominanceRules` | OD (카탈로그) |
| `/units/*/cell/competition_key` | string | §4 (카탈로그) | `EvidenceRules.DominanceRules` | OD (카탈로그) |
| `/units/*/cell/hand` | string | §4 (카탈로그) | `EvidenceRules.DominanceRules` | OD (카탈로그) |
| `/units/*/cell/id` | string | §4 (카탈로그) | `EvidenceRules.DominanceRules` | OD (카탈로그) |
| `/units/*/cell/pitcher_strategy` | string | §4 (카탈로그) | `EvidenceRules.DominanceRules` | OD (카탈로그) |
| `/units/*/cell/policy_id` | string | §4 (카탈로그) | `EvidenceRules.DominanceRules` | OD (카탈로그) |
| `/units/*/inputs` | object | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/attestation` | object | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/advancement_policy_version` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/cohort_id` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/cohort_inputs_hash` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/competition_key` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/config_hash` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/event_budget` | number | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/execution_policy_hash` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/execution_policy_id` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/game_id_rule` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/git_commit_sha` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/model_version` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/park_snapshot` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/policy_id` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/rules_hash` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/rules_profile_id` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/schema_version` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/seed_family` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/seed_manifest_path` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/seed_manifest_sha256` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/tuning_hash` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation/tuning_version` | string | §3.3 | `EvidenceShapes.Attestation` | OD, VT, VX (A04) |
| `/units/*/inputs/attestation_hash` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/block_size_games` | null | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/called_pitch_mode` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/cohort_kind` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/games` | array | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/games/*` | object | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/games/*/game_id` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/games/*/index` | number | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/games/*/seed` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/mode` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/pa_target` | null | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/planned_games` | number | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/player_input_set_content_version` | number | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/player_input_set_id` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/rules_status` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/rules_version` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/inputs/unit_run_id` | string | §3.2 | `EvidenceShapes.UnitInputs`, `Verifier.BindUnits` | OD, VX (A04) |
| `/units/*/technical` | object | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/committed_events` | number | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/counters` | object | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/at_bats` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/called_balls` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/called_strikes` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/doubles` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/duplicates_ignored` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/event_hits` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/event_home_runs` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/events` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/fouls` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/hit_by_pitch` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/hit_by_pitch_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/hits` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/home_runs` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/in_play_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/located_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/plate_appearances` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/sacrifice_bunts` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/sacrifice_flies` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/singles` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/strikeouts` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/swinging_strikes` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/swings` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/takes` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/triples` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/unlocated_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/walks` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/counters/zone_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD |
| `/units/*/technical/end_kinds` | array | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/end_kinds/*` | object | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/end_kinds/*/count` | number | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/end_kinds/*/end_kind` | string | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/error_codes` | array | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/games` | array | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*` | object | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/away_runs` | number | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/box_score_cross_check` | object | §3.6 | `EvidenceRules.GameRow` | OD (N03) |
| `/units/*/technical/games/*/box_score_cross_check/items` | array | §3.6 | `EvidenceRules.GameRow` | OD (N03) |
| `/units/*/technical/games/*/box_score_cross_check/items/*` | object | §3.6 | `EvidenceRules.GameRow` | OD (N03) |
| `/units/*/technical/games/*/box_score_cross_check/items/*/from_box_score` | number | §3.6 | `EvidenceRules.GameRow` | OD (N03) |
| `/units/*/technical/games/*/box_score_cross_check/items/*/from_events` | number | §3.6 | `EvidenceRules.GameRow` | OD (N03) |
| `/units/*/technical/games/*/box_score_cross_check/items/*/name` | string | §3.6 | `EvidenceRules.GameRow` | OD (N03) |
| `/units/*/technical/games/*/box_score_cross_check/passed` | bool | §3.6 | `EvidenceRules.GameRow` | OD (N03) |
| `/units/*/technical/games/*/committed_events` | number | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/counters` | object | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/at_bats` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/called_balls` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/called_strikes` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/doubles` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/duplicates_ignored` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/event_hits` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/event_home_runs` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/events` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/fouls` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/hit_by_pitch` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/hit_by_pitch_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/hits` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/home_runs` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/in_play_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/located_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/plate_appearances` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/sacrifice_bunts` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/sacrifice_flies` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/singles` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/strikeouts` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/swinging_strikes` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/swings` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/takes` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/triples` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/unlocated_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/walks` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/counters/zone_pitches` | number | §3.5 | `EvidenceShapes.CounterFields` | OD, CX (N01) |
| `/units/*/technical/games/*/end_kind` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/error_code` | null | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/event_hash` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/failure` | null | §3.6 B~D행 | `EvidenceRules.GameRow` | OD (N03), CX |
| `/units/*/technical/games/*/final_half` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/final_inning` | number | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/game_id` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/home_runs` | number | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/index` | number | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/outcome` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/regulation_error_code` | null | §3.6 A행·§4 | `EvidenceRules.GameRow` (dominance) | CX (N02) |
| `/units/*/technical/games/*/regulation_half_innings` | array | §3.6 A행·§4 | `EvidenceRules.GameRow` (dominance) | CX (N02) |
| `/units/*/technical/games/*/regulation_half_innings/*` | number | §3.6 A행·§4 | `EvidenceRules.GameRow` (dominance) | CX (N02) |
| `/units/*/technical/games/*/result_hash` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/rng_hash` | string | §3.6 D행·§10.7 | `EvidenceRules.GameRow` | CX (N01·N33·N34) |
| `/units/*/technical/games/*/seed` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games/*/status` | string | §3.6 | `EvidenceRules.GameRow`, 재실행 비교 | VT (A06) |
| `/units/*/technical/games_cancelled` | number | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/games_completed` | number | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/games_executed` | number | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/games_failed` | number | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/games_not_started` | number | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/games_requested` | number | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/games_unexecuted_after_stop` | number | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/metrics_computed` | bool | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/metrics_error_code` | null | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/pitches` | number | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/plate_appearances` | number | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/run_result_hash` | string | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/run_result_version` | string | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/run_status` | string | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |
| `/units/*/technical/stop` | null | §3.4 | `EvidenceRules.UnitResult` | VT (A06) |

## UNIT_PZL

| JSON 경로 | 관찰 타입 | 계약 조항 | 검사 위치 | 시험 |
|---|---|---|---|---|
| `/additional_evidence` | object | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/additional_evidence/artifacts` | array | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*` | object | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/artifact_id` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/byte_length` | number | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/game_id` | null | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/raw_sha256` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/relative_path` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/role` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/artifacts/*/unit_id` | string | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/pzl6_id` | null | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/additional_evidence/pzl_unit_ids` | array | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/approvals` | object | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/approvals/approval_record_id` | string | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/approvals/approval_record_sha256` | string | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/approvals/freeze_id` | string | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/checks` | array | §7·§11.1 | `UnitPzlEvidence.Checks` (`PzlChecks.Evaluate`) | UP, EH |
| `/checks/*` | object | §7·§11.1 | `UnitPzlEvidence.Checks` (`PzlChecks.Evaluate`) | UP, EH |
| `/checks/*/coverage` | object | §7 coverage (NSC-6) | `UnitPzlEvidence.Coverage`, 재투영 `Verifier` (unit_pzl_differs) | UP (N05-a·b·d), CX (N05-c·N05-e) |
| `/checks/*/coverage/called_pitch_steps` | number | §7 coverage (NSC-6) | `UnitPzlEvidence.Coverage`, 재투영 `Verifier` (unit_pzl_differs) | UP (N05-a·b·d), CX (N05-c·N05-e) |
| `/checks/*/coverage/completed_games` | number | §7 coverage (NSC-6) | `UnitPzlEvidence.Coverage`, 재투영 `Verifier` (unit_pzl_differs) | UP (N05-a·b·d), CX (N05-c·N05-e) |
| `/checks/*/coverage/events` | number | §7 coverage (NSC-6) | `UnitPzlEvidence.Coverage`, 재투영 `Verifier` (unit_pzl_differs) | UP (N05-a·b·d), CX (N05-c·N05-e) |
| `/checks/*/coverage/pitches` | number | §7 coverage (NSC-6) | `UnitPzlEvidence.Coverage`, 재투영 `Verifier` (unit_pzl_differs) | UP (N05-a·b·d), CX (N05-c·N05-e) |
| `/checks/*/deferral` | null, object | §7 Deferral | `UnitPzlEvidence.Deferral` | UP |
| `/checks/*/deferral/bcl_phases` | array | §7 Deferral | `UnitPzlEvidence.Deferral` | UP |
| `/checks/*/deferral/bcl_phases/*` | number | §7 Deferral | `UnitPzlEvidence.Deferral` | UP |
| `/checks/*/deferral/code` | string | §7 Deferral | `UnitPzlEvidence.Deferral` | UP |
| `/checks/*/deferral/lmv_a_phases` | array | §7 Deferral | `UnitPzlEvidence.Deferral` | UP |
| `/checks/*/deferral/lmv_a_phases/*` | number | §7 Deferral | `UnitPzlEvidence.Deferral` | UP |
| `/checks/*/id` | string | §7·§11.1 | `UnitPzlEvidence.Checks` (`PzlChecks.Evaluate`) | UP, EH |
| `/checks/*/reason` | string | §7·§11.1 | `UnitPzlEvidence.Checks` (`PzlChecks.Evaluate`) | UP, EH |
| `/checks/*/status` | string | §7·§11.1 | `UnitPzlEvidence.Checks` (`PzlChecks.Evaluate`) | UP, EH |
| `/checks/*/test_evidence` | null | §7 TestEvidence | `EvidenceRules.UnitPzlRules` | EH (A25-a·b) |
| `/checks/*/violations` | array | §7·§11.1 | `UnitPzlEvidence.Checks` (`PzlChecks.Evaluate`) | UP, EH |
| `/execution` | object | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/build_provenance_id` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/evidence_id` | string | §2.2 R-12 | `EvidenceIds`, `EvidenceRules.Header` | OD (N08) |
| `/execution/family` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/git_commit_sha` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/run_id` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/validation_set_id` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/games` | array | §5.1 | `UnitPzlEvidence.CompareGame` | UP (A07·A08·A09·N17) |
| `/games/*` | object | §5.1 | `UnitPzlEvidence.CompareGame` | UP (A07·A08·A09·N17) |
| `/games/*/committed_events` | number | §5.1 | `UnitPzlEvidence.CompareGame` | UP (A07·A08·A09·N17) |
| `/games/*/event_hash` | string | §5.1 | `UnitPzlEvidence.CompareGame` | UP (A07·A08·A09·N17) |
| `/games/*/events_digest` | string | §10.5 | `UnitPzlEvidence.CompareGame` | UP (N14) |
| `/games/*/final_rng_canonical` | string | §10.4 | `EvidenceRules.UnitPzlRules` (rng_boundary) | UP, CX (N35) |
| `/games/*/game_id` | string | §5.1 | `UnitPzlEvidence.CompareGame` | UP (A07·A08·A09·N17) |
| `/games/*/game_result_hash` | string | §5.1 | `UnitPzlEvidence.CompareGame` | UP (A07·A08·A09·N17) |
| `/games/*/index` | number | §5.1 | `UnitPzlEvidence.CompareGame` | UP (A07·A08·A09·N17) |
| `/games/*/observation_present` | bool | §5.1 | `UnitPzlEvidence.CompareGame` | UP (A07·A08·A09·N17) |
| `/games/*/rng_hash` | string | §10.4 | `EvidenceRules.UnitPzlRules` (rng_boundary) | UP, CX (N33·N34) |
| `/games/*/seed` | string | §5.1 | `UnitPzlEvidence.CompareGame` | UP (A07·A08·A09·N17) |
| `/games/*/status` | string | §5.1 | `UnitPzlEvidence.CompareGame` | UP (A07·A08·A09·N17) |
| `/games/*/summary` | object | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/called_pitch_steps` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/drawn_streams` | array | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/drawn_streams/*` | string | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/duplicate_event_ids` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/ef2_receipts` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/event_id_collisions` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/events` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/fingerprint_mismatches` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/foreign_event_ids` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/framing_entries` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/framing_inputs` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/framing_non_neutral` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/located_events` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/receipt_log_mismatches` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/games/*/summary/version_mismatches` | number | §5.2 | `UnitPzlEvidence.PzlGame` | UP |
| `/plan` | object | §5 | `EvidenceRules.UnitPzlRules`, `Verifier.BindBundleHeader` | UP |
| `/plan/config_hash` | string | §5 | `EvidenceRules.UnitPzlRules`, `Verifier.BindBundleHeader` | UP |
| `/plan/run_result_hash` | string | §5 | `EvidenceRules.UnitPzlRules`, `Verifier.BindBundleHeader` | UP |
| `/plan/run_result_version` | string | §5 | `EvidenceRules.UnitPzlRules`, `Verifier.BindBundleHeader` | UP |
| `/plan/unit_id` | string | §5 | `EvidenceRules.UnitPzlRules`, `Verifier.BindBundleHeader` | UP |
| `/registered_streams` | array | §5 | `EvidenceRules.UnitPzlRules` | UP |
| `/registered_streams/*` | string | §5 | `EvidenceRules.UnitPzlRules` | UP |
| `/schema` | object | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts` | object | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/dominance` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/family` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/hash` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/pzl6` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/pzl_unit` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/id` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/version` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |

## PZL6

| JSON 경로 | 관찰 타입 | 계약 조항 | 검사 위치 | 시험 |
|---|---|---|---|---|
| `/additional_evidence` | object | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/additional_evidence/artifacts` | array | §2.5·§21 | `Archive.CheckReferences` | AR (A18~A20), EH (A12) |
| `/additional_evidence/pzl6_id` | null | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/additional_evidence/pzl_unit_ids` | array | §2.5 | `EvidenceRules.Header` | OD, EH (A10) |
| `/approvals` | object | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/approvals/approval_record_id` | string | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/approvals/approval_record_sha256` | string | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/approvals/freeze_id` | string | §2.3 | `Verifier.BindApproval` | VT (A03), EH (A01) |
| `/check` | object | §6·§7 | `Pzl6Verifier.Check`, `EvidenceRules.Pzl6Rules` | P6, EH |
| `/check/coverage` | object | §7 coverage | `Pzl6Verifier.Check` | EH (A10-a) |
| `/check/coverage/called_pitch_steps` | number | §7 coverage | `Pzl6Verifier.Check` | EH (A10-a) |
| `/check/coverage/completed_games` | number | §7 coverage | `Pzl6Verifier.Check` | EH (A10-a) |
| `/check/coverage/events` | number | §7 coverage | `Pzl6Verifier.Check` | EH (A10-a) |
| `/check/coverage/pitches` | number | §7 coverage | `Pzl6Verifier.Check` | EH (A10-a) |
| `/check/deferral` | null | §6·§7 | `Pzl6Verifier.Check`, `EvidenceRules.Pzl6Rules` | P6, EH |
| `/check/id` | string | §6·§7 | `Pzl6Verifier.Check`, `EvidenceRules.Pzl6Rules` | P6, EH |
| `/check/reason` | string | §6·§7 | `Pzl6Verifier.Check`, `EvidenceRules.Pzl6Rules` | P6, EH |
| `/check/status` | string | §6·§7 | `Pzl6Verifier.Check`, `EvidenceRules.Pzl6Rules` | P6, EH |
| `/check/test_evidence` | null | §6·§7 | `Pzl6Verifier.Check`, `EvidenceRules.Pzl6Rules` | P6, EH |
| `/check/violations` | array | §6·§7 | `Pzl6Verifier.Check`, `EvidenceRules.Pzl6Rules` | P6, EH |
| `/execution` | object | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/build_provenance_id` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/evidence_id` | string | §2.2 R-12 | `EvidenceIds`, `EvidenceRules.Header` | OD (N08) |
| `/execution/family` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/git_commit_sha` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/run_id` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/execution/validation_set_id` | string | §2.2·§2.4 | `EvidenceRules.Header`, `Verifier.BindApproval` | OD (R-14), VT (A03) |
| `/plan` | object | §6 | `EvidenceRules.Pzl6Rules` | P6, EH (A10) |
| `/plan/integration_evidence_id` | string | §6 | `EvidenceRules.Pzl6Rules` | P6, EH (A10) |
| `/plan/sample_count_per_mode` | number | §6 | `EvidenceRules.Pzl6Rules` | P6, EH (A10) |
| `/plan/selection_rule` | string | §6 | `EvidenceRules.Pzl6Rules` | P6, EH (A10) |
| `/samples` | array | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*` | object | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/ef1_receipts` | number | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/ef2_receipts` | number | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/event_count` | number | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/event_format_version` | number | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/event_hash` | string | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/event_hash_equal` | bool | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/event_replay_match` | bool | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/event_sequence_equal` | bool | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/final_state_equal` | bool | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/game_id` | string | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/game_result_hash` | string | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/index` | number | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/located_event_count` | number | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/locations_equal` | bool | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/receipts_equal` | bool | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/rewrite_bytes_equal` | bool | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/rng_hash` | string | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/seed` | string | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/seed_replay_match` | bool | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/samples/*/unit_id` | string | §6 (고정 20경기) | `Pzl6Verifier.Check` | P6 |
| `/schema` | object | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts` | object | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/dominance` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/family` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/hash` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/pzl6` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/contracts/pzl_unit` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/id` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
| `/schema/version` | string | §2.1 | `EvidenceRules.Header`, `EvidenceShapes.Contracts` | OD (N06·N23·A11) |
