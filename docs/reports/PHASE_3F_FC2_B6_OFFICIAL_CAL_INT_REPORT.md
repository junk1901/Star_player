# Star Player Phase 3-F F-C2 B-6 Report — `fc3-cal`·`fc3-int` 공식 실행 경로 연결

- 상태: **`IMPLEMENTED_WITH_NOTES` · `TESTED` · `REVIEW_PENDING`** (2026-10-19, 소유자 `TASK.md` "Phase 3-F / F-C2 B-6 Official Calibration & Integration Wiring"; 독립 리뷰 `미실시`).
- 기준: Branch `main`, HEAD `1b386d3503465ce0ba66d9721936b3f667aece1a`(F-C2 PZL·Dry Pilot 로컬 Closeout Commit) 불변. Staging·Commit·Push·Reset·Clean·Rebase·Amend 없음.
- **실제 Official Execution 미실시 · Final Execution Freeze `PENDING` · Official Execution `NOT_READY` · F-C2 전체 / Phase 3-F / Phase 3 Gate `INCOMPLETE`.** 공개 공식 명령(`official-cal`·`official-int`·`replay-cal`·`replay-int`)은 현재 승인값으로 항상 거부된다(Exit 2, 게임 0회, 출력 0).
- 정책 문서 [ADR-008](../decisions/ADR-008.md)·[ADR-009](../decisions/ADR-009.md) 본문, `ROADMAP.md`, `docs/SYSTEM_CONTRACTS.md`는 수정하지 않았다. B-1~B-8·P-1~P-6은 확정하지 않았다(§8).
- **B-6 구현 상태와 실제 공식 실행 가능 상태는 다르다.** 코드 경로(Preflight → 실행 → 저장 → Replay)는 합성 비공식 Fixture로만 검증되었고, 실제 실행은 Freeze 승인값·빌드 출처·integration 코호트 선언 없이는 시작될 수 없다.

## 1. 승인 계약 확인 — 두 Family의 구별

| 항목 | `fc3-cal` (Calibration) | `fc3-int` (Integration) | 근거 |
|---|---|---|---|
| 단위 | 4층 L1~L4 = 코호트 × 호출 모드(`neutral-mixed-v1`/`virtual-league-v1` × Automated/NonAutomated), 층마다 독립 계획 | 호출 모드 2개(Automated / NonAutomated), 모드마다 정확히 100경기 | FC-D7 표·FC-D7.2, 리더 `Seed.integration_size_invalid` |
| 코호트 | FC-D1/FC-D2로 확정(`OfficialProfile.FamilyCohorts["fc3-cal"]`) | **ADR-008이 정하지 않음**(제안서 §7.2의 `virtual-league-v1`은 후보, 승인 아님) | FC-D7.2, `Official.family_cohorts_not_declared` |
| 정책 | `normal-balanced-v1`만 | `normal-balanced-v1`만 | FC-D3 |
| Seed 계획 | 외부 Pin된 파일 전체가 계획(`games` = 전체), `pa_target ≥ 100,000`, `max_games == Seed 수`, 완전 블록 | 외부 Pin된 파일 전체, `pa_target` 없음, `max_games == Seed 수 == 100` | SR-D1.1, SR-D6, FC-D7.1, FC-D7.2 |
| 중단 규칙 | 블록 경계에서만: 누적 유효 PA ≥ 목표 → `TARGET_REACHED`, 아니면 Seed 소진 → `SEEDS_EXHAUSTED`. 진행 중 블록은 완료. 정지 뒤 Seed는 미실행(제외 아님) | **없음**(전체 실행). 정지 기록·`run-result-v2` 금지 | SR-D1.2~1.4, SR-D5 |
| 유효 PA | Completed 경기 PA만. 실패 경기는 실행 수에 포함, PA 미포함, 교체 없음 | (해당 없음) | SR-D3.2·3.3 |
| 결과 해시 | `run-result-v2`(정지 기록 결속), 취소·중단은 결과 아님 | `run-result-v1` | SR-D7.3, SR-D7.5 |
| 평가 우선순위 | 비공식·미승인·취소/중단 → `NOT_EVALUATED`; 실패 경기 → `FAIL`; 지표 계산 오류 → `NOT_EVALUATED`(SR-D9.2); Seed 소진 → `INSUFFICIENT_SAMPLE`; 층 PA 하한; 공식 지표 판정(FC-D6). 층은 합산하지 않고 다른 층으로 보상하지 않음 | 정확히 100경기·취소/미시작 없음 → 실패 경기 `FAIL` → **지표 계산 실패 `FAIL`**(FC-D9.1) → 아니면 `PASS`(개별 지표 판정은 진단) | SR-D9.2, FC-D7.3, FC-D9.1, 기존 `StratifiedEvaluator.Integration` |
| Replay | 정지 파라미터·계획은 Pin된 파일에서만, 정지점 재도출, 기록값은 비교만, 앞쪽 `executed_games`만 재실행 | 전체 목록 정확 비교, 전체 재실행 | SR-D8, SR-D5 |
| 승인 요구 | FC-D10(Strict CLI·Hash·Dirty·Attestation), Final Execution Freeze(Seed·Pin·InputsHash·Git SHA·실행 정책·빌드 출처) | 동일 + integration 코호트 선언(미결) | FC-D10, ADR-008 "Two freezes" |

`fc2-dry` 진단 결과는 공식 Calibration 증거가 아니다(SR-D4.3). 이번 경로는 `fc2-dry` 파일을 단위로 받지 않는다(Family로 걸러짐, 공식 Preflight는 `Official.family_not_official`).

## 2. B-6 이전 상태와 빠진 연결

- 이미 있던 것: 단일 Manifest 공식 Preflight(`OfficialPreflight`, 모든 계열), Seed 리더(SR-D6·FC-D7.1·FC-D7.2), 정지 규칙 실행기 `StopRule.Execute`/`SimRunner.RunStopAware`, `run-result-v2`·`StopReplay`, 공식 envelope 검증 `ReplayCheck.Validate`, 층화 평가 `StratifiedEvaluator`(`Evaluate`·`GateOfFailedEvaluation`·`CombineStrata`·`Integration`), 48셀용 `PinnedDirectory` 출력.
- 빠진 것: `official`은 Preflight 전용이었고 `fc3-cal`/`fc3-int` 파일이 Ready여도 `Official.execution_not_implemented`로 끝났다. Family 단위 계획 도출(4층·2모드), 실행 진입점, 단위별 저장, Family 색인, Family 평가 결합, 독립 Replay 명령이 없었다. `fc3-int`는 코호트가 선언되지 않아 단일 Preflight조차 `NOT_READY`였다.

## 3. 구현

### 3-1. 파일

| 파일 | 내용 |
|---|---|
| `tools/StarPlayer.SimHarness/FamilyOfficial.cs` (신규) | `OfficialFamilyPlan`(Calibration=4층 정지 인식, Integration=2모드 전체 실행), `FamilyPreflight`, `FamilyOfficial.Execute`/`Evaluate`/문서, `FamilyOfficialReplay.Verify` |
| `Official.cs` | `OfficialApprovals.IntegrationCohortIds`(선택 Freeze 슬롯, `Current`에서 `null`) 추가; 코호트 조회를 `OfficialProfile.CohortsOf(family, approvals)` 한 곳으로(`fc3-cal`/`fc3-dom`은 기존 `FamilyCohorts` 그대로, `fc3-int`만 슬롯). `OfficialProfile.Hash()`·`FamilyCohorts` 불변 |
| `DominanceOfficial.cs` | 동작 보존 추출: 승인 Pin 전체 읽기(`OfficialPins.ReadAll`, B-7 Fail-Closed 한 곳)와 실행 파일 3종 쓰기(`OfficialPins.WriteRun`). 48셀 로직·코드·메시지 불변 |
| `SimRunner.cs` | `RunSpecs`에 선택 시험 Seam `play`(기본 `GameExecutor`; `RunStopAware`와 같은 형태) |
| `Cli.cs`·`Program.cs` | `official-cal`·`replay-cal`·`official-int`·`replay-int`; 48셀과 같은 화이트리스트 생성 함수(`official-dom` 메시지 동일). `official`에 `fc3-cal`/`fc3-int` 파일이 오면 `Official.family_plan_required`로 안내(기존처럼 Exit 2, 게임 미실행) |

### 3-2. CLI

```
StarPlayer.SimHarness official-cal|official-int --run-id ID [--out build/simharness/official/DIR]
StarPlayer.SimHarness replay-cal|replay-int --result OFFICIAL_FAMILY_RESULT_DIR
```

- 명령별 Family 고정(`official-cal` = `fc3-cal`만). `--seed-manifest`·`--seed-sha256`·`--seeds`·`--tuning`·`--rules`·`--advancement`·`--event-budget`는 `Cli.option_forbidden`, `--pa-target`·`--block-size`·`--max-games`·`--games`·`--family`·`--cohort` 등은 `Cli.option_unknown`. 중복·값 누락은 기존 코드. 승인 Seed·Pin·표본 조건은 명령줄로 주입할 수 없다.
- Exit Code(기존 의미만): 0 완료, 1 경기 실패·취소 또는 재실행 후 불일치, 2 인수·Preflight 거부·무결성 중단·게임 전 Replay 거부, 3 지표 계산 오류. 0은 Gate 판정이 아니다.

### 3-3. Family Preflight (`FamilyPreflight.Run`, 모든 검사 실행·보고, 쓰기·게임 없음)

| 검사 | 내용 | 미충족 |
|---|---|---|
| `cli` | 엄격 파서 통과 | (파서가 Exit 2) |
| `git_integrity`·`git_sha`·`model_tuning` | 기존 공유 검사 | Dirty `FAIL`, bin/obj `NOT_READY`, 미동결 `NOT_READY`, 불일치 `FAIL` |
| `output_directory` | 기존 `OfficialOutput.Resolve`(새 디렉터리, 공식 루트 내부, 링크 거부) | `FAIL` |
| `seed_manifests` | `PinnedUnits`: 승인 Pin **전체**를 Pin으로 읽음(하나라도 실패 → `FAIL`, B-7) → 해당 Family 파일만 → Competition Key 고유 → 파일마다 정확히 한 단위에 결속(층: 코호트+규칙, 모드: 규칙) → calibration 파일은 정지 계획 생성 가능 → 단위마다 정확히 하나·누락 없음 | 0개 `NOT_READY`(`Official.family_seed_manifests_not_frozen`); `Official.family_unit_unbound` / `family_unit_duplicate` / `family_units_missing`, `Seed.*` `FAIL` |
| `unit_preflight` | 단위마다 기존 단일 Manifest `OfficialPreflight.Run` → 단위별 **Attestation v1**(전체 계획 `config_hash`) | 첫 미충족 검사 코드(FAIL 우선) |
| `execution_policy`·`execution_path` | 기존 공유 검사 | 미동결 `NOT_READY`, `ExecutionPathReady=false` `NOT_READY` |

- `Ready`는 모든 Family 검사 PASS **그리고** 모든 단위 Preflight Ready일 때만. 거부 문서는 `run_status: PREFLIGHT_REFUSED`, `family_gate: NOT_EVALUATED`(통계 판정 아님), `official_evidence: false`.
- `FamilyOfficial.Execute`는 Ready가 아닌 보고서를 받으면 `InvalidOperationException`(라이브러리 이중 차단). 시험은 이때 게임 Seam 호출 0회를 확인한다.

### 3-4. 실행 (`FamilyOfficial.Execute`)

1. 출력은 OI-01 `PinnedDirectory`로만: 볼륨 루트부터 핸들 고정, 링크 거부, 실행 디렉터리 배타 생성(같은 Run ID 동시 실행은 두 번째가 `Official.output_exists`로 중단, 아무것도 쓰지 않음), `units/` 아래 단위 디렉터리·파일 모두 새로 생성.
2. 단위마다(계획 순서): 무결성 재검증(모든 단위 Seed 파일 = Attestation의 SHA) → `FamilyOfficial.Config`(Pin된 파일의 코호트·정책·규칙 + 고정 공식 프로파일; Preflight Attestation과 같은 구성) → GameId·전체 계획 `config_hash` = Attestation 확인 → 실행:
   - **calibration**: `SimRunner.RunStopAware`(유일한 정지 규칙 실행기, SR-D1.4) + **매 블록 전 무결성 재검증**(SR-D3.4).
   - **integration**: `SimRunner.RunSpecs`로 전체 100경기(정지 규칙 미적용).
3. 단위가 끝날 때마다 `units/<id>/manifest.json`(기존 `simharness-manifest-v1` + 기존 `simharness-official-v1` envelope·Attestation v1; calibration은 기존 정지 인식 형태 `results.stop`·`run-result-v2`), `summary.json`, `summary.txt`를 기록(이후 실패에도 증거 보존).
4. 취소 → `CANCELLED`, 무결성 중단(사전·블록 전·중단된 정지 인식 실행) → `ABORTED_INTEGRITY`: `complete=false`, 평가 없음, 실행된 단위 증거는 남김. 마지막 무결성 검사 뒤 `COMPLETED`/`GAMES_FAILED`/`METRICS_ERROR`.
5. Family 색인 `family-result.json`을 마지막에 기록(아래).

### 3-5. Family 평가 (`FamilyOfficial.Evaluate`, 기존 평가기만 사용)

- **calibration**: 층마다 `StratifiedEvaluator.Evaluate(run, FcEvaluationV1)`; 지표 계산 자체가 실패하면 `GateOfFailedEvaluation`(실패 경기 있으면 `FAIL`, 없으면 `NOT_EVALUATED`, SR-D9.2). 결합은 `CombineStrata`. 단, 평가가 만들어지지 못한 `FAIL` 층이 `CombineStrata`에서 "평가 없는 층"(→ `NOT_EVALUATED`)으로 가려지지 않도록, 그런 층이 있으면 Family Gate를 `FAIL`로 둔다(`CombineStrata`의 "알려진 FAIL은 누락으로 가려지지 않는다" 규칙을 같은 뜻으로 적용; §8 B-6c).
- **integration**: 모드마다 `StratifiedEvaluator.Integration(run)`(지표 계산 실패 = `FAIL`, FC-D9.1). 결합: 한 모드라도 `FAIL`이면 `FAIL`(보상 없음), 두 모드 모두 `PASS`일 때만 `PASS`, 그 밖은 `NOT_EVALUATED`(§8 B-6d).
- 실행 상태: 실패가 지표 오류보다 우선(기존 규칙). 평가 비교 텍스트 `family-evaluation-text-draft`(해시 아님): 단위별 Gate·사유·오류 코드와 calibration 공식 지표 줄(상태·점추정·층별 분자/분모).

### 3-6. 저장 형식 (DRAFT, 공식 증거 아님)

```
<out>/units/<l1|l2|l3|l4|automated|non-automated>/manifest.json   기존 계약 그대로
<out>/units/<id>/summary.json, summary.txt
<out>/family-result.json   schema simharness-family-result-draft, schema_status DRAFT_NOT_APPROVED, official_evidence false
```

색인 항목: 실행 식별·Family·`run_status`·`complete`·중단 코드/메시지, Git SHA, `approvals`(실행 정책 ID/Hash, calibration 평가 정책 ID/Hash, integration 선언 코호트), Preflight 검사, 단위마다 {코호트·규칙·Key·Seed 파일 경로/SHA·Manifest 경로/SHA·`config_hash`·결과 버전/`run_result_hash`·`attestation_hash`·실행 상태·계획/실행/완료/실패/취소 경기 수·정지 후 미실행 수·정지 기록·유효 PA·지표 오류 코드·Gate·사유}, Family Gate·사유, 평가 비교 텍스트. **새 해시를 만들지 않으며** 승인되지 않은 공식 결과 Schema·Hash를 도입하지 않았다(B-1). ConfigHash·`game-result-v1`·`run-result-v1/v2`·Attestation v1·48셀 DRAFT·Legacy Fixture 불변.

### 3-7. Replay (`FamilyOfficialReplay.Verify`, 결과 파일을 신뢰하지 않음)

게임 전 거부(하나라도 어긋나면 어떤 게임도 재실행하지 않음):
1. 색인 존재·중복 JSON 항목 없음·JSON 객체·스키마 = DRAFT(자기 승격 거부)·Family/`run_type` = 명령의 Family.
2. `complete == true`, `run_status ∈ {COMPLETED, GAMES_FAILED, METRICS_ERROR}`(취소·중단 거부).
3. **승인값에서 계획 재도출**(`PinnedUnits`, Preflight와 같은 함수) — 외부 Pin·전체 계획 완전성·단위 결속.
4. 기록된 Git SHA·`approvals` = 승인값.
5. 단위 수·순서 = 계획, Manifest 경로는 단위 ID로 고정, 파일 SHA-256 = 기록값.
6. 단위 Manifest마다 기존 `ReplayCheck.Validate`(공식 envelope·Attestation·승인값·**실제 Pin된 Seed 파일**·정확한 전체 경기 목록; calibration은 Pin된 Family가 `run-result-v2`·정지 기록을 요구하고 SR-D8.4 사전 검사) → 정지 계약 = Family(cal 정지 인식, int 아님) → Attestation의 Seed 파일·SHA = 승인값이 그 단위에 결속한 파일 → 실행 ID 단일 → 색인의 해시·Seed 기록·**정지 기록·결과 버전·경기 수** = Manifest → 설정 재구성·`CheckAgainst` → integration은 계획 100경기 전부 기록·취소/미시작 0(부분 실행 위장 거부).

재실행 후 비교:
7. 같은 실행 함수로 재실행(calibration은 Pin된 계획으로 정지점 재도출 + 블록마다 Pin 재검증, `StopReplay.Compare`로 정지 기록·PA·경기별 증거·집계·상태·`run-result-v2` 비교; integration은 경기별 상태·해시·실패 코드와 `run-result-v1`).
8. 재평가 후 단위 색인 항목 전체(JSON 정확 일치), 평가 비교 텍스트, Family Gate·사유, 실행 상태가 기록과 같아야 일치. 불일치는 경고가 아니라 거부(게임 전 Exit 2, 재실행 후 Exit 1).

## 4. 신뢰 경계와 Fail-Closed

- 유일한 승인 출처는 `OfficialApprovals`. `Current`는 여전히 Pin 0개, Git SHA·실행 정책·코호트 Hash `null`, `ExecutionPathReady=false`, `IntegrationCohortIds=null`이다(시험 단언). 내부 기본값·Dry Pilot Fixture로 공식 실행을 시작하는 경로는 없다.
- CLI·결과 파일의 Seed·Pin·정지값 자기 주장은 쓰지 않는다. 계획은 언제나 승인 Pin과 실제 파일 바이트(SHA-256)에서 다시 만든다.
- **B-7 유지:** 어느 Family든 승인 Pin 파일 하나를 읽지 못하면 calibration·integration Preflight·Replay도 `FAIL`이다(48셀과 같은 함수 `OfficialPins.ReadAll`). 다른 Family의 정상 Pin 파일은 계획에 섞이지 않는다.
- `IntegrationCohortIds`는 **값이 아니라 빈 슬롯**이다. 시험 전용 승인 객체에만 가상 선언(`neutral-v1`, 제안서 후보와 무관하게 Fixture로 고른 값)을 넣었다. 이 슬롯은 `OfficialProfile.Hash()`에 들어가지 않으며, 결속은 Attestation의 `cohort_id`·`cohort_inputs_hash`(승인값과 대조)와 색인의 `approvals.integration_cohort_ids`(Replay 대조)로 한다(§8 B-6a).
- 시험 Seam `play`·`integrity`는 라이브러리 인자일 뿐 CLI에서 쓰지 않는다(공식 명령은 항상 `null`).

## 5. 시험

### 5-1. 신규 `tests/StarPlayer.SimHarness.Tests/FamilyOfficialTests.cs` (17개)

시험 세계: TEMP 루트, 합성 `fc3-cal` 4개(가상 Seed 990001~990012, 블록 2, 목표 100,000) + `fc3-int` 2개(990001~990100), 시험 전용 승인 객체. calibration은 합성 경기(경기당 PA 10,000의 Seam; 100,000 PA 층 실경기는 단위 시험이 아님), integration Fixture는 **실제 엔진 200경기**와 실제 엔진 Replay.

| 시험 | 확인 |
|---|---|
| `With_the_real_approvals_both_family_preflights_are_blocked_and_no_game_can_start` | 두 Family 모두 `NOT_READY`, Execute 예외, 게임 0회, 출력 없음, Replay 게임 0회; `Current` 미동결·`IntegrationCohortIds` null·`FamilyCohorts` 불변 |
| `The_family_commands_refuse_with_the_real_approvals_and_start_no_game` | 자식 프로세스 `official-cal`/`official-int` Exit 2·거부 JSON·출력 미생성; `replay-cal`/`replay-int` Exit 2; 금지 옵션 Exit 2 |
| `The_family_commands_accept_only_their_whitelisted_options` | 허용·금지·미등록·필수·중복 옵션 |
| `A_fully_pinned_calibration_plan_is_ready_with_one_attestation_per_stratum_in_order` | L1~L4 순서, Attestation 결속, 전체 계획 `config_hash`; integration 미선언이 calibration을 막지 않음 |
| `The_calibration_plan_comes_only_from_the_pins_and_every_gap_or_conflict_is_refused` | 누락·중복·Key 재사용·미결속 층·B-7(다른 Family 파일 누락)·Pin 불일치·파일 변조·코호트 Hash 미동결·Dirty·SHA·실행 정책·경로 미준비·기존 출력 |
| `Each_stratum_stops_at_the_first_block_boundary_reaching_the_target_and_is_stored_under_existing_contracts` | 층마다 `TARGET_REACHED`@10(블록 2), 실행 접두 1..10만 실행(11·12 미실행), `run-result-v2`, 기존 `ReplayCheck` 수용, Gate PASS(합성), DRAFT 색인 |
| `A_target_reached_mid_block_completes_the_block_and_exhaustion_is_seeds_exhausted_and_insufficient` | 1경기에서 목표 도달 → 블록 끝(2경기)에서만 정지; Seed 소진 → `SEEDS_EXHAUSTED`·Exit 0·`INSUFFICIENT_SAMPLE`; 두 경우 Replay 일치 |
| `A_failed_game_keeps_its_seed_and_fails_the_gate_and_a_metrics_error_is_not_evaluated_with_the_existing_priority` | 실패 경기 유지·교체 없음·PA 제외·`FAIL`·Exit 1·Replay 재현; 지표 오류만 → 층 `NOT_EVALUATED`·Exit 3·Family PASS 아님; 지표 오류+실패 경기 → `FAIL`(가려지지 않음) |
| `Cancellation_and_broken_integrity_leave_an_incomplete_result_that_replay_refuses` | 취소·블록 전 무결성 중단·실행 중 Seed 파일 변경 → 정지 기록 없음·`complete=false`·Exit 1/2·증거 보존, Replay 게임 전 거부, 완료 위장 거부 |
| `The_calibration_result_replays_by_re_deriving_every_stop_from_the_pinned_plan` | Replay 일치(재실행 40경기 = 도출된 접두만), 다른 실행 → `Replay.stop_too_early` |
| `A_calibration_replay_refuses_every_tampered_or_untrusted_record_before_any_game` | Stop Reason 위조·이른/늦은 정지·v1 강등·정지 기록 삭제·계획 축소·색인만 위조·해시·순서·누락·경로·파일 해시·다른 층 Manifest 대체·불완전·스키마 승격·승인 기록·색인 없음/형식·실행 정책·Seed 파일 변조·Family 혼용 — 모두 게임 0회 |
| `A_calibration_replay_rejects_a_forged_evaluation_after_re_execution` | Gate·사유·평가 텍스트·실행 상태·단위 Gate·유효 PA 위조 → 재실행 후 거부 |
| `The_integration_preflight_needs_a_declared_cohort_and_exactly_one_plan_per_mode` | 선언 없음 `NOT_READY`(`family_cohorts_not_declared`), 미승인 코호트 `FAIL`, 모드 중복·누락, 98경기 파일 거부 |
| `Integration_runs_the_whole_plan_of_each_mode_on_the_real_engine_and_replays_independently` | 실제 엔진: 모드마다 100경기 전체·정지 없음·`run-result-v1`·기존 Replay 수용, Gate = 기존 `Integration`, 실제 엔진 Replay 일치 |
| `Integration_failures_and_metrics_errors_fail_the_gate_and_a_partial_run_is_never_a_complete_one` | 실패 경기 `FAIL`(계획대로 계속)·Replay 재현; 한 모드만 실패해도 Family `FAIL`; 지표 오류 → `FAIL`·Exit 3; 부분 실행 위장·정지 기록 주입·계획 축소·해시·경기 수·승인 기록·Family 위조 게임 전 거부; 경기 해시 위조 재실행 후 거부; Replay 시 선언 없음 거부; 취소 → 불완전 |
| `Two_ready_runs_with_one_run_id_never_share_an_output_and_existing_evidence_is_never_overwritten` | 같은 Run ID 두 번째 실행 `Official.output_exists`, 게임 0회, 기존 파일 SHA 불변 |
| `A_parent_swapped_for_a_junction_after_the_preflight_never_redirects_the_family_output` | Preflight 뒤 `build/simharness/official`을 Junction으로 교체 → `Official.output_reparse_point`, 루트 밖 0파일, 게임 0회 |

기존 시험 변경: `StrictCliTests.Each_command_has_one_authoritative_whitelist`에 신규 명령 4개를 **추가** 단언(약화 없음). 그 밖의 기존 시험 변경 없음.

### 5-2. 표적 Sabotage (원복 후 SHA-256 동일 확인)

`build/verify-archive/PHASE_3F_FC2_B6_sabotage/SUMMARY.txt`(FamilyOfficialTests + DominanceOfficialTests 40개 대상):

| 변형 | 결과 |
|---|---|
| S1 색인 정지 기록 사전 검사 제거 | 1 FAIL(검출) |
| S2 가려진 FAIL 층 규칙 제거 | 1 FAIL |
| S3 calibration을 정지 규칙 없이 실행 | 7 FAIL |
| S4 integration 계획 완료 검사 제거 | 1 FAIL |
| S5 승인 Pin 읽기 실패 무시(B-7 약화) | 6 FAIL(48셀 시험 포함) |
| S6 integration 모드 보상(전부 FAIL일 때만 FAIL) | 1 FAIL |
| S7 Replay가 기록된 Seed 계획을 신뢰 | 1 FAIL |

`restored_identical=True`(`FamilyOfficial.cs` `9e572d6d…9021`, `DominanceOfficial.cs` `f077d4e8…3dc2`). 최초 시도 `…_sabotage_try1/`은 S5·S7 변형이 컴파일되지 않았고(도달 불가 코드 경고) S6이 검출되지 않아 시험 공백(한 모드만 실패하는 경우)을 드러냈다. 시험을 추가하고 변형을 고친 뒤 재실행했다. 두 폴더 모두 보존.

## 6. 빌드 · 전체 시험 · Verify

모든 결과는 실제 실행 출력이다(Windows 11, .NET SDK 8.0.425, Godot 4.7.2 mono).

| 명령 | 결과 |
|---|---|
| `dotnet build tools/StarPlayer.SimHarness -c Release --no-incremental` | 경고 0 · 오류 0 |
| `dotnet test tests/StarPlayer.SimHarness.Tests -c Release --filter FamilyOfficialTests\|StrictCliTests` (첫 실행) | 62 PASS / **1 FAIL** — 시험 쪽 기대값 오류(기록 정지 10 < 재도출 12이므로 올바른 코드는 `Replay.stop_too_early`). 기대값만 고침, 구현 변경 없음 |
| `dotnet test tests/StarPlayer.SimHarness.Tests -c Release` | 370 PASS / 0 FAIL / 0 SKIP |
| Sabotage(§5-2) | try1: S5·S7 컴파일 실패, S6 미검출 → 시험 추가·변형 수정 → 최종 7/7 검출, 원복 SHA 동일 |
| `powershell -File tools/verify.ps1 -LogName PHASE_3F_FC2_B6_VERIFY_LOG.md` **run1 (최종 트리)** | **`GATE: PASS`, exit 0** — Release 11개 경고 0; Domain 327 · Simulation 741 · Application 127 · Infrastructure 1,236 · SimHarness 370 = **2,801 PASS / 0 FAIL / 0 SKIP**(아키텍처 14·결정성 48은 부분집합); Godot 빌드·Headless·Export·Export 실행 PASS; 문서 링크 162개 파일 PASS. `Run at 2026-10-19 20:18:26 +09:00`, `Git: 1b386d3 (dirty working tree)` |

- 로그 [PHASE_3F_FC2_B6_VERIFY_LOG.md](PHASE_3F_FC2_B6_VERIFY_LOG.md) SHA-256 `5374c65e14d112dc5da4c545033e8b8d2943c516b7b05931bb5bf534130ae3b8`.
- 원시 증거 `build/verify-archive/PHASE_3F_FC2_B6_run1/`(29개 파일 + `verify-stdout.txt`), `verify-result.json` SHA-256 `b311d5a643ac66d5d7c183fdc518b6a4eb072ff12dcc54e825157fe44524ec2e`.
- Sabotage: `…_B6_sabotage/SUMMARY.txt` `873a389f1c16ccbad58123b91d8614250a1a1a3429038830fb1ea43390d30d4e`, `…_B6_sabotage_try1/SUMMARY.txt` `5a761a61bc3d7e272308cf0d9a2f824bc6fbcde4151df995f53817a26eeb3e5f`(실패 시도, 보존).
- Verify 뒤 소스 변경 없음(§9 SHA-256이 Verify 시점 값). Verify 뒤에는 이 보고서의 §6 본문(숫자·해시)만 채웠고 새 링크는 이 절의 같은 디렉터리 로그 링크 하나다(그 파일은 존재; D1 재실행은 하지 않음 — `미검증`).
- 보호 파일 SHA-256: 시작·종료 동일(§9 값), 쓰기 없음.
- 기존 증거: `build/verify-archive/`(B-6 폴더 제외)와 `docs/reports/`(B-6 파일 제외) 956개 파일의 SHA-256이 문서 작성 직전 스냅샷과 종료 시점에 동일. 작업 시작 직후 스냅샷은 만들지 않았으나 그 사이 이 세션은 해당 경로에 쓰지 않았다.
- 실제 저장소 `build/simharness/` 없음(공식·비공식 실행 출력 0).
- 미실시: 독립 리뷰(GPT Chat·Codex), 실제 Official Execution, Final Execution Freeze, 전체 Mutation.

## 7. Official Execution 차단 확인

- `OfficialApprovals.Current`: Pin 0, Git SHA·실행 정책·코호트 Hash 미동결, `ExecutionPathReady=false`, `IntegrationCohortIds=null`(시험 단언).
- `official-cal`·`official-int` 실제 자식 프로세스 → Preflight 거부, Exit 2, 게임 0회, 출력 미생성. `replay-cal`·`replay-int`는 현재 승인값으로 어떤 결과도 받지 않는다(Exit 2).
- 실제 저장소에서는 bin/obj와 `build/` 출력 때문에 `git_integrity`도 통과하지 않는다(B-4). 승인값이 들어와도 빌드 출처 계약 전에는 시작되지 않는다(의도된 차단).
- 이번 작업에서 공식 Seed 파일·Pin·표본 수·실행 상한·Git SHA·코호트 선언을 만들거나 기록하지 않았고, 실제 calibration·integration·48셀 공식 실행을 하지 않았다. 실제 저장소 `build/simharness/`는 생성되지 않았다(시험은 TEMP만 사용).

## 8. BLOCKED · 정책 공백 · 소유자 결정 필요

각 항목: Issue / Reason / Recommended Option / Alternative / Impact. 아무것도 확정하지 않았다.

- **B-6a `fc3-int` 코호트 선언.** Reason: ADR-008 FC-D7.2는 모드당 100경기만 정하고 코호트를 정하지 않았다(제안서 §7.2의 `virtual-league-v1`은 후보). 실행 경로를 시험하려면 선언 위치가 필요해 `OfficialApprovals.IntegrationCohortIds` 빈 슬롯을 두었다(48셀의 `DominanceGamesPerCell` 슬롯과 같은 방식). Recommended: 소유자가 코호트를 결정하고, ADR 보충과 함께 `OfficialProfile.FamilyCohorts`에 넣어 실행 정책 Hash에 결속(그 경우 슬롯은 제거 가능). Alternative: Final Execution Freeze 승인값(슬롯)으로 선언. Impact: 선언 전 integration은 단위 Preflight에서 `NOT_READY`; 선언 위치에 따라 `OfficialProfile.Hash()`가 바뀔 수 있다.
- **B-6b 단위 ↔ Seed 파일 결속 표현(B-2의 확장).** Reason: 승인값은 경로 → Pin 표뿐이다. 현재 "Pin된 그 Family 파일 전체가 각 층(코호트+규칙)·모드(규칙)를 정확히 하나씩 덮어야 함"으로 도출한다. Recommended: Freeze에서 이 규칙 확인 또는 단위 ID → 경로 표 명시. Alternative: 경로·Competition Key 명명 규칙 고정(제안서 후보 `fc3-cal-na` 등). Impact: 규칙이 바뀌면 `PinnedUnits`만 수정.
- **B-6c calibration Family Gate에서 지표 계산이 실패한 `FAIL` 층.** Reason: `CombineStrata`는 평가 객체가 있는 층만 받으므로, 실패 경기+지표 오류 층은 "평가 없는 층"이 되어 `NOT_EVALUATED`로 보일 수 있다. 기존 "알려진 FAIL은 누락으로 가려지지 않는다" 규칙을 같은 뜻으로 적용해 `FAIL`로 두었다. Recommended: 이 해석 확인. Alternative: `CombineStrata` 입력을 층 Gate 목록으로 일반화. Impact: 판정 방향만(PASS가 되는 경로 없음).
- **B-6d integration 두 모드의 결합 Gate.** Reason: FC-D7.2/FC-D12(5)는 모드당 100경기 완료를 요구하지만 결합 규칙 문장은 없다. 보수적으로 "하나라도 FAIL이면 FAIL, 둘 다 PASS일 때만 PASS, 그 밖은 NOT_EVALUATED"로 두었다. Recommended: 확인. Impact: 판정 방향만.
- **B-1 / P-5 공식 결과 Schema·Hash.** Family 색인은 `simharness-family-result-draft`, `official_evidence: false`. 승인 전에는 F-C3 증거가 될 수 없다. `summary.json` 진단(PZL 포함)은 여전히 비인증 파생 결과다(PZL Closeout NOTE-2).
- **B-3 실행 상한.** calibration 층 상한은 SR-D6로 `max_games == Seed 수`이며 그 이상의 총 상한·확장 규칙은 미승인. 이번 경로는 Seed 파일 이외의 상한을 만들지 않았다.
- **B-4 빌드 출처·승인 출력 경로**, **B-5 코호트 InputsHash 동결**, **B-8 공식·dry Seed 수치 분리(P-2)**: 변화 없음.
- **B-7 Family 간 Pin 결합**: 보수적 Fail-Closed 유지(§4). 소유자 확인 대기.
- **P-1 PZL #6 공식 표본**: `fc3-int` 실행 경로는 생겼지만 공식 Seed가 미동결이므로 공식 표본 검증은 계속 `BLOCKED`.
- P-3·P-4·P-6: 변화 없음.

## 9. 변경 파일 Manifest와 Git 상태

| 구분 | 파일 | SHA-256 (최종) |
|---|---|---|
| 신규(개발) | `tools/StarPlayer.SimHarness/FamilyOfficial.cs` | `9e572d6df5443c6cff40377758ab5ea8ae47418508069de2f7b9dfb3ad4b9021` |
| 수정(개발) | `tools/StarPlayer.SimHarness/Official.cs` | `732b2d9e1a22eb0b27b7f27d80e1315d38285fab5de6904a8deb789fba98bfcc` |
| 수정(개발) | `tools/StarPlayer.SimHarness/DominanceOfficial.cs` | `f077d4e84b8a22faa398f7380142cdc10779c101200c0d1a708bdce417c23dc2` |
| 수정(개발) | `tools/StarPlayer.SimHarness/SimRunner.cs` | `696869faf156002dcec2ec2de606699b1ace73c3caedfed7c789d8e14b2d421d` |
| 수정(개발) | `tools/StarPlayer.SimHarness/Cli.cs` | `96953a158adce7b9640ea9938baf2c8f1f9a7cc375d2483d06328dcb59ebe3e7` |
| 수정(개발) | `tools/StarPlayer.SimHarness/Program.cs` | `46add7f42169e65a1d1a42b2799f11238c8a96a78c850021969ae43c7f0776cd` |
| 신규(시험) | `tests/StarPlayer.SimHarness.Tests/FamilyOfficialTests.cs` | `8984e58d4b3374c70d11cad22639f2ea2a4bb57488660d350c76e130604b5ed3` |
| 수정(시험) | `tests/StarPlayer.SimHarness.Tests/StrictCliTests.cs` | `c92afd3131fa18d21fd8de02b49a5b443c78557c76f363975ca15ca53ce7bfbd` |
| 신규(기록) | 이 보고서, `PHASE_3F_FC2_B6_VERIFY_LOG.md`; `build/verify-archive/PHASE_3F_FC2_B6_*`(Git 무시) | — |
| 수정(상태) | `CURRENT_STATE.md`, `docs/game-engine.md` §41, `tools/StarPlayer.SimHarness/README.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`(부록), `Star/00_Dashboard.md`(한 줄) | — |

- Git: Branch `main`, HEAD `1b386d3503465ce0ba66d9721936b3f667aece1a` 불변(origin/main보다 21 Commit 앞섬). Staging·Commit·Push·Reset·Clean·Rebase·Amend 없음.
- 보호 파일 SHA-256(시작 값; 종료 값은 §6): `TASK.md` `7885871fc5aefaf8d797a155a45929f6248df960998c9cf9de6dff6a268e2e2c`, `docs/2023recordRule.pdf` `9742caaf3a596b03d37fc6c57b9f866e989214e074aa5af1a2ab821ec5f212b0`, `docs/2026-official-baseball-rules.pdf` `f85860f3b754dfb5e07269e9d3a86c77c89b856376f6e3ba8345af36db997859`.

## 10. 상태

**B-6 코드 경로 구현·자체 검증 완료 후보 — `REVIEW_PENDING`(독립 리뷰 `미실시`; 공식 실행·무결성·저장 경계라 Codex 리뷰 권장).** 실제 공식 실행 가능 상태는 아니다: Official Execution `NOT_READY`, Final Execution Freeze `PENDING`, B-6a(integration 코호트)·B-1~B-8·P-1~P-6 미결, F-C2 전체·Phase 3-F·Phase 3 Gate `INCOMPLETE`. 다음 Phase·F-C3는 착수하지 않았다.


---

## FIX1 Addendum — Codex High MINOR 2건 (B6-01 · B6-02) 수정

작업: 2026-10-19, 소유자 `TASK.md` "Phase 3-F F-C2 B-6 FIX1". Base HEAD `1b386d3503465ce0ba66d9721936b3f667aece1a` 불변, B-6 미커밋 변경 위에 적용. Staging·Commit·Push·Reset·Clean·Rebase·Amend 없음. 이 수정은 **해결 후보**이며 독립 재검토는 `미실시`다.

### F-1. B6-01 — Calibration Family 결합 우선순위

- **수정 전**: `FamilyOfficial.Evaluate`가 Metrics 계산에 실패한 층(`StratifiedEvaluator.Evaluate` 실패)을 `CombineStrata` 입력에서 빼서 "평가 없는 층"으로 처리했다. L1 Metrics 오류(NOT_EVALUATED) + L2 Seed 소진(INSUFFICIENT_SAMPLE) + L3/L4 PASS의 Family가 `NOT_EVALUATED`였다(RED 확인: Expected `InsufficientSample`, Actual `NotEvaluated`).
- **수정 후**: 실패한 층도 자신의 Gate(`GateOfFailedEvaluation`, 변경 없음)와 층 식별자·Config/RunResult Hash·정지 기록을 담은 지표 없는 `CalibrationEvaluation`(`FamilyOfficial.Unmeasured`)으로 `CombineStrata`에 들어간다. 결과: 위 사례의 Family = `INSUFFICIENT_SAMPLE`, L1은 `NOT_EVALUATED`와 `Metrics.counts_inconsistent` 유지, 사유 문구에 L1·L2가 모두 명시된다. 실행되지 않은(평가 객체가 없는) 층만 계속 "누락"이다.
- 이전의 `hiddenFail` 사후 보정은 같은 목적(실패 경기+Metrics 오류 층이 누락으로 가려지지 않음)을 `CombineStrata`의 기존 FAIL 우선 분기가 직접 하므로 삭제했다. FAIL 우선 결과는 동일하고 사유 문구만 `CombineStrata`의 표준 문구가 된다(문구 단언 시험 없음, 기존 FAIL 단언 유지).
- **변경 없음**: `Evaluation.cs`(SR-D9.2 개별 층 판정, `CombineStrata`, `GateOfFailedEvaluation`), 새 상태값·통계 정책·Hash 없음. 층끼리 합산·병합 없음. 오류를 FAIL로 승격하지 않음(FAIL은 실패 경기가 있을 때만, 기존 규칙).

### F-2. B6-02 — Integration Replay 집계값 검증

- **수정 전**: 전체 실행(비 정지 인식) 단위의 Replay는 경기별 상태·Hash와 `run_result_hash`/`config_hash`만 재실행과 비교했다. Manifest `results.pitches`·`results.plate_appearances`를 바꾸고 색인의 `manifest_sha256`을 맞추면 MATCH했다(RED 확인: 가짜 경기 시험과 **실제 엔진** 시험 모두 `Error == null`). Calibration은 이미 `StopReplay.Compare`가 두 값을 비교하므로 해당 없음.
- **수정 후**: 사전 검사(`ReplayCheck.Validate`, 승인 계획·Pin·전체 게임 목록)는 그대로, 재실행 직후 단위마다 Manifest 기록값을 **재실행 `RunResult.Aggregate`** 의 PA·투구 수와 비교한다(기록값을 기대값으로 쓰지 않음). 불일치는 `FamOfficial.result_counts_mismatch`(`GamesExecuted=true`)로 거부한다. 한 값·한 모드만 틀려도 거부. 필드를 읽지 못하면 기존대로 게임 전에 `result_invalid`. 새 Schema·Hash·Attestation 변경 없음.
- 순서: 경기별 비교 → 집계값 비교 → `run_result_hash` 비교. 경기 Hash가 같고 집계만 바뀐 변조를 집계 단계가 잡는다.

### F-3. 변경 코드 (최소)

- `tools/StarPlayer.SimHarness/FamilyOfficial.cs`: `Evaluate`(실패 층을 결합에 유지, `hiddenFail` 삭제), `Unmeasured`(신규 private), Replay의 단위 튜플에 기록 PA·Pitches 추가와 재실행 후 비교 1개.
- `tests/StarPlayer.SimHarness.Tests/FamilyOfficialTests.cs`: 신규 시험 4건(기존 단언 약화·삭제 없음).

### F-4. 신규 회귀 시험 (RED → GREEN)

| 시험 | 수정 전 | 수정 후 |
|---|---|---|
| `A_metrics_error_stratum_keeps_its_state_in_the_family_combination_and_never_outranks_a_sample_shortage` — 실패 층을 L1~L4 각각에 두고 인접 층을 표본 부족으로: Family `INSUFFICIENT_SAMPLE`, 실패 층 `NOT_EVALUATED`+오류 코드, 사유에 둘 다 표시, 층 순서 불변, Metrics 오류만이면 `NOT_EVALUATED`, 저장 Family 결과 = Replay 재계산(canonical 동일) | FAIL | PASS |
| `A_fail_stratum_still_outranks_a_metrics_error_and_a_sample_shortage_in_the_family_combination` — FAIL+Metrics 오류 층 / FAIL 층 / 표본 부족 공존 → FAIL, "평가가 없습니다" 문구 없음, 실패 경기+오류 층 단독 → FAIL | PASS(회귀 방어; 기존 동작 보존) | PASS |
| `Integration_replay_compares_the_recorded_pitches_and_plate_appearances_of_every_mode_with_the_re_execution` — D 정상 MATCH(200경기), A pitches / B plate_appearances / C 두 번째 모드만(각 모드 × 각 필드, 색인 `manifest_sha256` 동기화), 두 값 동시 변조, 재실행이 먼저 수행됨(`plays.Count == (모드+1)×100`) | FAIL | PASS |
| `Integration_replay_on_the_real_engine_also_rejects_a_tampered_aggregate` — 실제 엔진 결과의 두 번째 모드 pitches·plate_appearances | FAIL (MATCH) | PASS |

RED 원본: `build/verify-archive/PHASE_3F_FC2_B6_FIX1_run1/red_and_focused/RED_run.log`(첫 시도: 시험 쪽 산술 기대값 오류 1건 포함), `RED_run2.log`(기대값 수정 후: 3 FAIL / 1 PASS). 별도 Sabotage 재실행은 하지 않았다(RED가 수정 전 코드에 대한 검출 증거).

### F-5. 검증 (실제 실행)

| 명령 | 결과 |
|---|---|
| 집중: `dotnet test tests/StarPlayer.SimHarness.Tests -c Release --filter FullyQualifiedName~FamilyOfficialTests` | 21 PASS / 0 FAIL |
| `dotnet test tests/StarPlayer.SimHarness.Tests -c Release --no-build` (Dominance·Stop/Replay·PZL·Legacy·StrictCli 포함) | **374 PASS** / 0 FAIL / 0 SKIP (370 → 374, 신규 4건) |
| `powershell -File tools/verify.ps1 -LogName PHASE_3F_FC2_B6_FIX1_VERIFY_LOG.md` | **`GATE: PASS`, exit 0** — Release 11개 빌드 경고 0; Domain 327 · Simulation 741 · Application 127 · Infrastructure 1,236 · SimHarness 374 = **2,805 PASS / 0 FAIL / 0 SKIP**; Godot 빌드·Headless·Export·Export 실행 PASS; 문서 링크 163개 파일 PASS(이 Addendum 작성 전 실행, 이후 D1 재실행은 `미검증`). `Run at 2026-10-19 21:43:02 +09:00`, `Git: 1b386d3 (dirty working tree)` |

이전 2,801 기록은 재사용하지 않았다(새 실행 값).

- 로그 [PHASE_3F_FC2_B6_FIX1_VERIFY_LOG.md](PHASE_3F_FC2_B6_FIX1_VERIFY_LOG.md) SHA-256 `6317522e1e56a5287ecab617422fc9362c120de181c23ec683d64d21b8e2c23e`.
- 원시 증거 `build/verify-archive/PHASE_3F_FC2_B6_FIX1_run1/`(Verify 원시 로그 + `verify-stdout.txt` + `red_and_focused/`), `verify-result.json` SHA-256 `e306964df561a7c4e9602f6003c7db58b836ba0384138c5a0fb6b94a090147b0`; `RED_run.log` `fb6fe0bf…2acf`, `RED_run2.log` `3f06a2ef…a11d`, `focused_family.log` `e1ed313f…48c5`, `simharness_full.log` `7573df82…82af`(전체 해시는 파일 자체에서 재계산 가능).
- 기존 증거 보존: 작업 중 시험 실행 전 `docs/reports/`+`build/verify-archive/` 1,003개 파일의 SHA-256 스냅샷(`build/fix1/evidence_before.sha`)을 모두 재검증해 **불일치 0건**(기존 B-6 로그·Archive 포함 무변경).
- 보호 파일 SHA-256 시작·종료 동일: `TASK.md` `8d3128925c5b6b9df9cc587132658085462fb66ca1e5f911d15dc50063dc183f`, `docs/2023recordRule.pdf` `9742caaf3a596b03d37fc6c57b9f866e989214e074aa5af1a2ab821ec5f212b0`, `docs/2026-official-baseball-rules.pdf` `f85860f3b754dfb5e07269e9d3a86c77c89b856376f6e3ba8345af36db997859`.

### F-6. 불변 확인

- 불변: OfficialApprovals 신뢰 경계, 외부 SHA-256 Pin, Calibration Stop/Replay, SR-D9.2(Calibration Metrics 오류 = NOT_EVALUATED), FC-D9.1(Integration Metrics 오류 = FAIL), 전체 Seed 실행, ConfigHash, game-result-v1, run-result-v1/v2, Attestation v1, Dominance 통계, PinnedDirectory, PZL(ef1/ef2), Legacy Fixture — 해당 코드·Hash 계약을 변경하지 않았고 위 374/2,805 시험이 통과.
- 공식 실행 차단: `With_the_real_approvals_both_family_preflights_are_blocked…`, `The_family_commands_refuse_with_the_real_approvals…`(자식 프로세스 Exit 2, 게임 0회, 출력 0건) PASS. `OfficialApprovals.Current` 무변경. 실제 `build/simharness/` 없음. Official Execution·Final Execution Freeze·F-C3·Phase 4 미착수.
- Family DRAFT 색인 `official_evidence:false`·`DRAFT_NOT_APPROVED` 유지.

### F-7. 변경 파일 Manifest (FIX1)

| 구분 | 파일 | SHA-256 (Verify 시점 최종) |
|---|---|---|
| 수정(개발) | `tools/StarPlayer.SimHarness/FamilyOfficial.cs` | `89571f0983db0eeae9c360e39985abbedfbe4a5bbacb370642c01cb143c8841a` |
| 수정(시험) | `tests/StarPlayer.SimHarness.Tests/FamilyOfficialTests.cs` | `0f743b963d93bf9b30ec48fbd5402c800ec7f26000d8b6a7c1965b22265ed542` |
| 신규(기록) | `docs/reports/PHASE_3F_FC2_B6_FIX1_VERIFY_LOG.md`, `build/verify-archive/PHASE_3F_FC2_B6_FIX1_run1/`(Git 무시) | 위 참조 |
| 수정(기록) | 이 보고서(Addendum 추가), `CURRENT_STATE.md`(상태 문단 1개) | — |

Verify 뒤 소스 변경 없음. 다른 B-6 변경 파일은 이번에 건드리지 않았다.

### F-8. 정책 공백 · NOTE (확정하지 않음)

- B-6a·B-6b·B-6c·B-6d·B-1~B-8·P-1~P-6 미결 유지. **B-6c 갱신 설명**: §8의 B-6c 서술("평가 없는 층이 되어")은 이 수정으로 메커니즘이 바뀌었다 — 지표 계산이 실패한 층은 이제 자신의 Gate로 결합에 들어간다(FAIL 층은 FAIL, 그 밖은 NOT_EVALUATED). 결합 우선순위(`FAIL > 누락 > INSUFFICIENT_SAMPLE > NOT_EVALUATED > PASS`)는 `CombineStrata`의 기존 승인 순서이며 B-6c의 명시적 소유자 확인은 여전히 필요하다.
- Codex NOTE(Replay가 `schema_status`·`official_evidence` 표시를 비교하지 않음)는 별도 정책·증거 계약 검토 항목으로 남긴다. 이번 수정에서 공식 인증 필드로 승격하지 않았고 비교를 추가하지 않았다.
- Integration의 `valid_pa`(색인 단위 기록)는 기존대로 재실행 `UnitEntry`와 비교된다. Manifest `committed_events` 등 이번 범위 밖 필드는 변경하지 않았다 — 필요 여부는 독립 재검토 판단 사항.

### F-9. 상태

**B6-01·B6-02 해결 후보 — 독립 재검토 `미실시`.** B-6 상태는 여전히 `REVIEW_PENDING`; Official Execution `NOT_READY`, Final Execution Freeze `PENDING`, F-C2 / Phase 3-F / Phase 3 Gate `INCOMPLETE`. STOP.


---

## Closeout Addendum — B-6 DRAFT Closeout 준비 (2026-10-19, 문서 정리만)

위 본문과 FIX1 Addendum(§F-1~F-9)은 작성 시점의 기록이며 수정·삭제하지 않았다. 이 Addendum이 **최신 상태**다. 코드·테스트 변경, 빌드·테스트·Verify·Sabotage·실제 엔진 실행은 하지 않았고, 아래 확인은 읽기 전용이다. Base HEAD `1b386d3503465ce0ba66d9721936b3f667aece1a` 불변, Staging 0, Commit·Push 없음.

### C-1. 상태

**`READY_FOR_OWNER_CLOSEOUT_REVIEW`** (F-C2 B-6 `fc3-cal`·`fc3-int` DRAFT 경로). 소유자 전달 요약 기준 Codex High 집중 재검토에서 B6-01·B6-02 해결, 신규 BLOCKER / MAJOR / MINOR 0건(Codex 원문은 이 세션에서 읽지 않음). 그 리뷰는 READ-ONLY였고 빌드·테스트를 직접 재실행하지 않았다. 본문 §10의 "독립 리뷰 `미실시`"와 FIX1 Addendum의 "독립 재검토 `미실시`"는 작성 당시 기록이며 이 Addendum이 최신이다. **로컬 Closeout Commit은 아직 미실시**다. 이는 공식 실행 준비 완료와 별개다: Official Execution `NOT_READY`, Final Execution Freeze `PENDING`, F-C2 전체·Phase 3-F·Phase 3 Gate `INCOMPLETE`. 이 Closeout은 Final Freeze나 F-C3 공식 실행의 승인이 아니다.

### C-2. 독립 리뷰 지적과 해결

| 항목 | 최초 지적 (MINOR) | 해결 |
|---|---|---|
| B6-01 | L1 Metrics 오류 + L2 표본 부족 + L3/L4 PASS에서 Calibration Family가 `NOT_EVALUATED`(실행된 층이 "누락"으로 처리) | Metrics 계산에 실패한 층도 `Unmeasured` 평가 객체로 `CombineStrata`에 포함. L1 = `NOT_EVALUATED`(+`Metrics.counts_inconsistent` 보존), L2 = `INSUFFICIENT_SAMPLE`, L3/L4 = `PASS`, **Family = `INSUFFICIENT_SAMPLE`**. 실패 경기가 동반되면 기존 FAIL 우선순위 유지, SR-D9.2(개별 층 판정) 유지, 오류를 FAIL로 승격하지 않음. `hiddenFail` 사후 보정은 `CombineStrata`의 FAIL 분기가 대신하므로 제거했고 Family 사유는 `CombineStrata`의 표준 문구를 쓴다. 저장 Family 결과 = Replay 재계산(canonical 동일). |
| B6-02 | Manifest `results.pitches`·`results.plate_appearances`를 바꾸고 색인 `manifest_sha256`을 맞추면 Integration Replay가 MATCH | 재실행 결과의 PA·투구 수를 다시 계산해 기록값과 비교, 불일치 시 `FamOfficial.result_counts_mismatch`(`GamesExecuted=true`). 두 모드·두 필드 모두 적용, `manifest_sha256` 동기화 변조도 검출, 정상 Replay MATCH 유지, Hash·Attestation 형식 변경 없음. |

구별: (a) 승인 계획·Pin·Seed 파일 오류는 **재실행 전**에 거부된다(`Replay.*`·`FamOfficial.*` 사전 검사). (b) 집계 무결성 불일치는 **재실행 후** 비교에서 거부된다(`result_counts_mismatch`). 또한 B6-01은 현재 DRAFT 결합 판정이 **구현·시험으로 일관됨**을 뜻할 뿐, B-6c·B-6d(결합 규칙의 명시적 소유자 승인)가 확정되었다는 뜻이 아니다.

### C-3. 최종 검증 증거 (FIX1 Verify, 이 세션에서 재실행하지 않음)

- 전체 **2,805 PASS / 0 FAIL / 0 SKIP**, `GATE: PASS` (Domain 327 · Simulation 741 · Application 127 · Infrastructure 1,236 · SimHarness 374).
- 읽기 전용 재확인(현재 파일에서 SHA-256 재계산): [PHASE_3F_FC2_B6_FIX1_VERIFY_LOG.md](PHASE_3F_FC2_B6_FIX1_VERIFY_LOG.md) `6317522e1e56a5287ecab617422fc9362c120de181c23ec683d64d21b8e2c23e` 일치; `build/verify-archive/PHASE_3F_FC2_B6_FIX1_run1/verify-result.json` `e306964df561a7c4e9602f6003c7db58b836ba0384138c5a0fb6b94a090147b0` 일치; `red_and_focused/RED_run2.log` 보존; 최초 B-6 Verify 로그([PHASE_3F_FC2_B6_VERIFY_LOG.md](PHASE_3F_FC2_B6_VERIFY_LOG.md))와 `…_B6_run1/` 보존.
- 코드·시험 Hash가 FIX1 Addendum §F-7과 일치: `FamilyOfficial.cs` `89571f09…841a`, `FamilyOfficialTests.cs` `0f743b96…ed542`.

### C-4. NOTE (비차단)

1. **`schema_status`·`official_evidence`는 아직 공식 Replay 인증 필드가 아니다.** Replay는 이 두 표시를 공식 증거의 근거로 검증하지 않는다. DRAFT 색인은 계속 `official_evidence:false`·`DRAFT_NOT_APPROVED`이며, 이 표시를 공식 결과의 인증 근거로 읽으면 안 된다. 공식 증거 형식과 결속 규칙은 B-1 및 관련 정책 결정에서 정해야 하고, 이번에 새 공식 Hash·Schema를 도입하지 않았다.
2. **기존 증거 스냅샷 표현 정정.** FIX1 Addendum §F-5의 "불일치 0건"은 `docs/reports/`+`build/verify-archive/` 스냅샷(1,003개 파일)을 **Verify 직후** 시점에 대조한 결과이며, 그 직후 이 보고서에 FIX1 Addendum을 붙였다. 현재 대조에서는 **1,002개 동일, 1개 불일치**이고 그 1개는 이 보고서 자체(`PHASE_3F_FC2_B6_OFFICIAL_CAL_INT_REPORT.md`, Addendum 추가가 원인)다. 따라서 "1,003개 전부 현재도 불변"이라고 기록하지 않는다. 원시 Verify 로그와 Archive는 변경 없이 보존되어 있다.
3. **문서 검증 범위.** FIX1 Addendum 작성 이후 전체 `verify.ps1`(D1 문서 링크 검사 포함)은 재실행하지 않았다. Codex의 READ-ONLY 링크 확인(관련 문서의 상대 링크 95개 모두 존재)은 전체 Verify 재실행과 별개이며 그것으로 표시하지 않는다. 이 Closeout에서 새로 넣은 링크는 같은 디렉터리의 기존 파일 두 개와 상태 문서→보고서 링크뿐이며 대상 존재는 읽기 전용으로 확인했다(Verify 재실행 아님).
4. **미확정 정책(코드의 DRAFT 판정이 동작해도 확정이 아님).** B-6a `IntegrationCohortIds` 승인·Hash 결속 범위 · B-6b 단위–Seed 파일 매핑 · B-6c Calibration Family 결합 규칙의 명시적 확인 · B-6d Integration Family 결합 규칙의 명시적 확인 · B-1~B-8 · P-1~P-6.

### C-5. Commit 후보 Manifest (실제 `git status --porcelain=v1 -uall`와 대조)

Git 상태 항목 19개 = 추적 수정 11 + 신규 개발·기록 5 + 보호 미추적 3, Staging 0 — 예상과 정확히 일치(차이 없음).

수정 11: `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `docs/game-engine.md`, `tests/StarPlayer.SimHarness.Tests/StrictCliTests.cs`, `tools/StarPlayer.SimHarness/{Cli,DominanceOfficial,Official,Program,SimRunner}.cs`, `tools/StarPlayer.SimHarness/README.md`.
신규 5: `docs/reports/PHASE_3F_FC2_B6_FIX1_VERIFY_LOG.md`, `docs/reports/PHASE_3F_FC2_B6_OFFICIAL_CAL_INT_REPORT.md`, `docs/reports/PHASE_3F_FC2_B6_VERIFY_LOG.md`, `tests/StarPlayer.SimHarness.Tests/FamilyOfficialTests.cs`, `tools/StarPlayer.SimHarness/FamilyOfficial.cs`.
제외: 보호 파일 `TASK.md`, `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf`; Git이 무시하는 `build/verify-archive/`·RED 로그 등.

### C-6. 선행조건 (Final Execution Freeze 전)

B-1~B-8 정책 결정 · B-6a~d 승인 범위 · P-1~P-6 결정·조치 · 공식 Cohort·Seed·Pin 승인 · 공식 결과 Schema와 증거 결속 · 승인 빌드 출처·출력 경로 · 표본 수·실행 상한 · 공식 PZL #6 표본 검증. 다음 Phase·F-C3는 착수하지 않았다.
