# Star Player Phase 3-F F-C2-B Implementation Report — N-04 Stop/Replay, Calibration Cohorts, Stratified Evaluation

- 작성: Claude Code, 2026-10-18, 소유자 `TASK.md` "Phase 3-F F-C2-B Implementation"
- 기준 HEAD: `e9209b33ee3c55b609749779ab3c79d3a04eed94` (`docs: freeze N-04 calibration stop replay contract in ADR-009`) — 작업 중 불변
- 권위 계약: [ADR-008](../decisions/ADR-008.md) (FC-D1~FC-D12), [ADR-009](../decisions/ADR-009.md) (SR-D1~SR-D9). 두 ADR의 정책 본문은 변경하지 않았다(ADR-009 머리말 Status의 구현 상태 문구만 갱신).
- 최종 판정: **`PHASE3_F_FC2B_IMPLEMENTED_WITH_NOTES`** · `OFFICIAL_EXECUTION_NOT_READY` · `FINAL_EXECUTION_FREEZE_PENDING`
- 검토 상태: `검토 대기` (구현자 자체 시험만; GPT Chat·Codex 독립 리뷰 `미실시`)
- **Phase 3-F Gate: `INCOMPLETE`. Phase 3 Gate: `INCOMPLETE`.** F-C2-C `NOT_STARTED`.

## A. 기준 상태와 범위

- 시작 상태는 `TASK.md`의 기대와 일치했다: Branch `main`, HEAD `e9209b3…`, 추적 변경 0, 미추적은 보호 파일 3개뿐.
- 범위: `tools/StarPlayer.SimHarness`(Domain + Simulation만 참조)와 그 시험. **Production `src/`, GameEngine, 확률 모델·Tuning·Rules 콘텐츠, RNG, Commit/CAS, `RunConfig.ConfigHash`, `run-result-v1`, `OfficialAttestation` v1, `ef1/ef2`, Legacy Fixture는 변경하지 않았다.**
- 하지 않은 것: Official Execution 활성화(`OfficialApprovals.Current` 불변, `ExecutionPathReady=false`), 공식 Seed 파일·Pin 생성, Final Execution Freeze 값 확정, 100,000 PA·100경기·48셀 실행, F-C2-C(전략 행렬·PZL 검사·A-1·`neutral-opp-v1`), Staging·Commit·Push.

## B. B-1 — ADR-009 N-04 Stop/Replay 구현

신규 `StopRule.cs`가 정지 규칙의 **단일 구현**이다(SR-D1.4). 실행과 Replay가 같은 실행기를 같은 신뢰 계획에 대해 호출하므로 Replay는 정지점을 기록에서 읽지 않고 다시 도출한다.

| 계약 | 구현 | 대표 시험 (`StopReplayTests`) |
|---|---|---|
| SR-D1.1 전체 계획 | `StopPlan`(Family, BlockSize, PaTarget, 전체 `GameSpec` 목록, Seed 파일 경로·SHA)은 외부 Pin으로 읽은 `SeedManifest`에서만 생성(`StopPlan.FromSeedManifest`). Stop-aware `RunResult.Specs` = 전체 계획 → `ConfigHash` = 전체 계획 Hash = `attestation.config_hash`. 매니페스트 `games`는 전체 목록 | `The_outcome_table…`(Requested 14, ConfigHash = 전체 계획), `A_consistent_stopped_calibration…`(plan.Games 21) |
| SR-D1.2 첫 합법 정지점 | `StopRule.Decide(block, target, n, played, validPa)` — 블록 경계에서만, 목표(포함) 먼저, 그다음 소진. `SeedManifest.AfterGames`는 이 함수에 위임(기존 동작 동일) | `The_run_ends_at_the_first_block_boundary…`, `A_target_reached_mid_block…`, `Exhaustion_is_reported_only…`, `The_seed_manifest_rule_and_the_executor_rule_are_one_function` |
| SR-D1.3 정지 후 Seed 미실행 | `StopRule.Execute`가 정지 즉시 반환. 실행된 경기 수를 Counting Seam으로 확인 | 위 시험의 `Played` 단언 |
| SR-D1.5·SR-D8.2 파라미터 출처 | `block_size`·`pa_target`·계획은 Pin된 Seed 파일에서만. `results.stop`은 비교 대상일 뿐 | `StopReplay.Precheck/Compare` 경로 전체 |
| SR-D3.2 유효 PA | Completed 경기(=BoxScore 교차검증 통과) PA만 누적. 실패 경기는 진행 경기 수에는 포함 | `A_failed_game_counts_as_played…` |
| SR-D3.3 실패 경기 | 실행 계속, 대체 없음, 결과에 보존(GameId·Seed·오류 코드·Hash). 규칙 정지 + 실패 = `GAMES_FAILED`(Exit 1), Gate `FAIL` | 위 시험, `A_failed_game_reproduces_under_replay…` |
| SR-D3.4 무결성 중단 | `integrity` 검사(블록 시작마다)가 실패하거나 경기가 `config` 단계 Pin 검사로 실패하면 `RunTermination.AbortedIntegrity`, 정지 기록 없음, `ABORTED_INTEGRITY`, Exit 2. `dry` 명령과 dry Replay는 블록마다 Seed 파일 Pin을 재검증 | `Cancellation_and_broken_integrity…`, `The_outcome_table…` |
| SR-D3.5 취소 | `RunTermination.Cancelled`, 정지 기록·결과 Hash 없음, `CANCELLED`, Exit 1. 매니페스트에 `run_result_version`/`run_result_hash` = null | 같은 시험, Replay `run_not_terminated` |
| SR-D4 dry | Reader: `fc2-dry` `pa_target`은 없거나 1~99,999(`DryMaxPa`). `dry` CLI 명령(비공식, Envelope 없음, Gate 항상 `NOT_EVALUATED`) | `Official_files_must_declare_max_games…`, `A_small_target_dry_run…`, CLI E2E |
| SR-D5 Family 범위 | Stop-aware는 `fc3-cal`·`fc2-dry`만(`StopPlan.FamilyIsStopAware`). 다른 계열·Legacy Key의 v2/정지 기록은 `Replay.stop_not_allowed` | `A_stop_record_or_v2_result_is_refused_outside_calibration_and_dry` |
| SR-D6 `max_games` | 공식 계열(`fc3-*`)은 `max_games == Seed 수`, 아니면 `Seed.max_games_mismatch`. `fc2-dry`는 여유 허용 | `Official_files_must_declare_max_games…` |
| SR-D7.1~7.3 정지 기록·v2 | `results.stop`(`executed_games`·`stop_reason`·`cumulative_pa`·`block_size`), `results.run_result_version`, `run-result-v2` 텍스트(§C.3) + Golden Vector | `Run_result_v2_has_a_fixed_byte_layout_and_golden_hash`, `The_v2_hash_binds…` |
| SR-D7.4 다운그레이드 금지 | 필요한 버전은 신뢰 입력(Pin된 Seed 파일 family)에서 결정. `fc3-cal`인데 v1/버전 없음 → `Replay.result_version_downgrade`, 정지 기록 없음 → `Replay.stop_record_missing` | `A_v1_downgrade_a_missing_stop…` |
| SR-D7.5·7.6 Legacy·Attestation | v1 경로·Legacy Fixture·`OfficialAttestation` v1 무변경, Attestation `config_hash`는 전체 계획 | Legacy 회귀(§G) |
| SR-D8 Replay 무결성 | `ReplayCheck.Validate`가 신뢰 계획 + 기록 정지값 구조·일관성(`StopReplay.Precheck`)을 실행 전에 검사 → `replay`가 신뢰 계획을 `SimRunner.RunStopAware`로 재실행 → `StopReplay.Compare`가 도출값 전부와 재계산 `run-result-v2`를 기록값과 대조 | §D 표 |
| SR-D9 기술 결과 vs Gate | Exit Code/`run_status`와 `GateStatus`(`NOT_EVALUATED`·`INSUFFICIENT_SAMPLE`·`PASS`·`FAIL`) 분리. `AllCompleted`(stop-aware) = 규칙 정지 ∧ 요구된 경기 전부 Completed | `The_outcome_table…`, `StratifiedEvaluationTests` |

**SR-D9.2 결과표의 실제 매핑**

| 상황 | `run_status` | Exit | Gate(§F) |
|---|---|---|---|
| `TARGET_REACHED`, 실패 없음, 지표 계산 | `COMPLETED` | 0 | FC-D6 판정(PASS/FAIL/INSUFFICIENT_SAMPLE) |
| `SEEDS_EXHAUSTED`, 실패 없음 | `COMPLETED` | 0 | `INSUFFICIENT_SAMPLE` |
| 규칙 정지 + 실패 경기 ≥ 1 | `GAMES_FAILED` | 1 | `FAIL` |
| 규칙 정지 + 지표 계산 불가 | `METRICS_ERROR` | 3 | `NOT_EVALUATED` |
| 취소 | `CANCELLED`(신규 라벨) | 1 | `NOT_EVALUATED` |
| 무결성 중단 | `ABORTED_INTEGRITY`(신규 라벨) | 2 | `NOT_EVALUATED` |

새 Exit Code는 만들지 않았다(SR-D3.6 "No new process exit code may mean success").

## C. ADR-009 Open 항목의 처리 (정책 변경 없는 구현 세부)

1. **Dry Replay의 신뢰 정지 파라미터 출처 (SR-D8.6) → 운영자 Pin.** `replay --manifest F --seed-manifest REL --seed-sha256 HEX`(쌍으로만). 이 Pin은 매니페스트 밖에서 오며 `fc2-dry` Competition Key에만 허용되고, 공식 Replay에는 거부(`Replay.dry_seed_manifest_unexpected`; 공식은 `OfficialApprovals`만 신뢰 — F-C2-A 계약 유지). Pin된 dry 파일은 family `fc2-dry`, Key·코호트·정책·규칙, 전체 경기 목록, 매니페스트가 기록한 Seed 파일 경로·SHA까지 결속된다. Pin 없이 v2 dry 매니페스트는 `Replay.stop_parameters_untrusted`. **근거:** 매니페스트가 스스로 지정한 Pin(자기 선언)은 SR-D8.1 "recomputed, not trusted"에 어긋나고, 코드 내 dry Pin 표는 시험·파일럿마다 코드 변경이 필요하다. 운영자 Pin은 공식 검사를 약화하지 않고 SR-D4.3(진단 전용)을 유지한다.
2. **결과 버전 식별 (SR-D7.4) → `results.run_result_version`.** 값은 `run-result-v1` 또는 `run-result-v2`, 없으면 v1(F-C2-B 이전의 모든 매니페스트). 그 외 값은 `Replay.result_version_unknown`. 이 필드는 *선언*일 뿐이고, 요구 버전은 신뢰 계획에서 정해진다(§B SR-D7.4).
3. **`run-result-v2` 결정적 직렬화·Hash 결속 (SR-D7.3).** UTF-8, `\n`, 불변 정수:
   ```
   run-result-v2
   {전체 계획 config_hash}
   plan|{계획 경기 수}|{pa_target}
   stop|{executed_games}|{TARGET_REACHED|SEEDS_EXHAUSTED}|{cumulative_pa}|{block_size}
   {실행된 경기 result_hash}   (계획 순서, 한 줄씩)
   {MetricCounters.Canonical 집계}
   ```
   SHA-256 소문자 hex. Golden Vector(리터럴 전문 + Python `hashlib`으로 독립 계산한 `6d3c35ec…84f5`)를 시험에 고정했다. ADR의 최소 결속(ConfigHash·정지 4필드·경기 Hash·집계)에 계획 길이와 PA 목표를 더했다("at least"). v1 텍스트는 `RunResultText.V1Text`로 옮겼으나 바이트 동일(기본 스모크·Legacy Hash 일치로 확인).
4. **상태 라벨·오류 코드 (SR-D3.6).** 라벨 `CANCELLED`, `ABORTED_INTEGRITY` 추가(기존 `COMPLETED`·`GAMES_FAILED`·`METRICS_ERROR` 유지). 코드: `Replay.result_version_unknown`, `Replay.result_version_downgrade`, `Replay.stop_record_missing`, `Replay.stop_record_invalid`, `Replay.stop_record_inconsistent`, `Replay.stop_block_size_mismatch`, `Replay.stop_too_early`, `Replay.stop_too_late`, `Replay.stop_reason_mismatch`, `Replay.stop_cumulative_pa_mismatch`, `Replay.plate_appearances_mismatch`, `Replay.result_games_not_prefix`, `Replay.result_games_mismatch`, `Replay.run_result_hash_mismatch`, `Replay.run_not_terminated`, `Replay.stop_not_allowed`, `Replay.stop_parameters_untrusted`, `Replay.dry_seed_manifest_unexpected`, `Replay.dry_seed_manifest_unavailable`, `Replay.dry_seed_family_invalid`, `Seed.max_games_mismatch`, `Stop.family_not_stop_aware`, `Stop.pa_target_missing`, `Dry.repository_unknown`, `Dry.family_not_dry`, `Dry.rules_unknown`, `Dry.output_exists`. 실행 전 구조 거부는 Exit 2, 재실행 후 불일치(`REPLAY DIFFERS`)는 Exit 1(기존 의미).
5. **Dry 소형 목표의 상한 → `1 ≤ pa_target ≤ 99,999`.** "small"을 공식 하한(100,000) 미만으로 정의해 dry 실행이 공식 층 규모로 보일 수 없게 했다. 경기 수 상한은 따로 두지 않았다(파일의 선언 그대로; F-C2-C의 파일럿 규모는 그 지시가 정한다).

새 정책 선택이 필요한 충돌은 없었다. ADR-009 "Open (Final Execution Freeze)" 항목(Seed 수치 분리, 블록·상한, Seed 목록·Pin)은 손대지 않았다.

## D. B-1 검증 결과 (B-2 착수 전 확인)

- `StopReplayTests` 20개 PASS: 순수 규칙(최초 경계, 포함 목표, 블록 중간 목표, 소진, 실패 PA 제외), 실행 결과표(Exit 0/0/1/1/2/3), Golden Vector·결속, Reader(SR-D4/SR-D6), 공식 `fc3-cal` 세계(TEMP Pin + 시험용 승인값 + 합성 경기; 100,000 PA 실제 실행은 단위 시험이 아님)의 재도출 일치와 변조 거부, 실패 경기 재현, 비정지 계열 거부, 실제 엔진 `fc2-dry` 소형 실행·결정성·Replay, CLI 하위 프로세스 E2E(TEMP 저장소 루트).
- 변조 → 코드: 이른 정지(일관되게 위조) → 재도출에서 `stop_too_early`; 늦은 정지 → `stop_too_late`(계획 초과는 실행 전); 규칙과 모순된 기록 → `stop_record_inconsistent`; 위조 PA → `stop_cumulative_pa_mismatch`/`plate_appearances_mismatch`; 위조 블록 → `stop_block_size_mismatch`; 경기 Hash → `result_games_mismatch`; 결과 Hash → `run_result_hash_mismatch`; 형식 오류 9종 → `stop_record_invalid`; 결과 경기 삭제·교체·순서 변경·추가 → `result_games_not_prefix`; 계획 Prefix·Seed 변경·추가 → `seed_games_mismatch`, 순서 변경 → `game_index_mismatch`; v1/버전 없음 → `result_version_downgrade`; 정지 기록 삭제 → `stop_record_missing`; `CANCELLED`/`ABORTED_INTEGRITY`·취소 경기 포함 → `run_not_terminated`; 실제 승인값 → `Official.seed_manifest_not_frozen`.
- **표적 Sabotage 7/7 검출**(전체 Mutation 아님; 각 원본 SHA-256 복구 확인): S1 실패 경기 PA 누적 → 2 FAIL; S2 블록 중간 정지 허용 → 9 FAIL; S3 v1 다운그레이드 허용 → 2 FAIL; S4 기록된 정지점 신뢰(이른 정지 검사 제거) → 1 FAIL; S5 결과 경기 Prefix 검사 제거 → 1 FAIL; S6 dry Pin과 기록 결속 제거 → 1 FAIL; S7 취소를 정지로 처리 → 2 FAIL(첫 시도 `if (false)`는 CS0162로 컴파일 실패 → 비상수 조건으로 재실행).
- 무결성 결함·정책 충돌 없음 → B-2/B-3 진행.

## E. B-2 — Calibration Cohort

- `CohortInputs`에 결정적 경기별 로테이션 `CohortRotation(Id, AwayStarters, HomeStarters, RotateBattingOrder)` 추가. 경기 g: 선발 `Starters[(g−1) mod 7]`, 나머지 선발은 벤치, 타순 로테이션 시 이동 `(g−1) mod 9`(포지션은 선수에 고정). `GameExecutor`는 `Cohort.SetupFor(GameIndex)`로 경기를 시작한다. 로테이션이 없는 코호트(`neutral-v1`)는 기존 두 팀 구성을 그대로 반환하므로 결과·Hash가 같다.
- `CohortInputs.StratumOf(g)`: 두 선발의 투구 손과 상대 라인업 타석 방향으로 경기 층(Same/Opposite/Mixed)을 정한다(FC-D1.3 "a property of the game").
- `InputsHash`: 로테이션이 있을 때만 `rotation=…|batting_order=…`와 선발 순서 줄을 덧붙인다. `neutral-v1` Hash `9343ad41…cb70` 불변(시험 고정).
- **`neutral-mixed-v1`**(FC-D1, 제안서 §3.3): 미러 팀 2개, 모든 능력치 50, 전원 우타, DH 라인업, 선발 7명(슬롯 0~2 우완, 3~6 좌완), 구원 1명·벤치 타자 1명(미사용), 로테이션 `sp7-3r4l-v1`. 7경기마다 Same 3 : Opposite 4.
- **`virtual-league-v1`**(FC-D2, 제안서 §4): 값 집합 `35,40,45,50,50,50,55,60,65`, 능력별 순환 이동(Contact 0, Power 2, Eye 4, Speed 1, Fielding 3, Arm 5), 선발 (Stuff, Control, Velocity) = (40,50,60)(50,60,40)(60,40,50) / (35,45,55)(45,55,65)(55,65,35)(65,35,45), 손잡이 그룹별 평균 50, 포수 도구·구원·Endurance 50, 전원 우타, 로테이션 `sp7-3r4l-bo9-v1`(63경기 주기). 승인 값 집합 밖의 능력치 없음(시험).
- Registry: 두 코호트 구현. `neutral-opp-v1`은 F-C2-C(전략 행렬)라 계속 `Registry.cohort_not_implemented`.
- Strata `CalibrationStrata.All` = L1 `neutral-mixed-v1`×Automated, L2 ×NonAutomated, L3 `virtual-league-v1`×Automated, L4 ×NonAutomated(FC-D7). 분리: GameId에 코호트 ID 포함 → 다른 RNG 문맥, ConfigHash 상이, 다른 코호트의 계획은 `RunStopAware`가 거부, Replay는 코호트 InputsHash로 거부, 층 결합은 합산 없이 `CombineStrata`만.
- `CalibrationCohortTests` 6개: 구조·손잡이·층, VL 값 집합·이동·상관(|r| < 0.358)·선발, 63경기 타순·선발 주기(각 타자 각 타순 7회), InputsHash 결속·결정성·로테이션 검증 오류, 실제 엔진 합법 완료·결정성·**회전된 선발이 실제로 쓰임**(같은 GameId·Seed에서 2번 선발 능력만 바꾸면 1경기 Hash 동일, 2경기 Hash 상이), 코호트 분리.
- 최종 공식 Seed 목록·Pin·코호트 InputsHash 동결·실행 규모는 정하지 않았다(Final Execution Freeze).

## F. B-3 — Metrics 및 EvaluationPolicy

- `EvaluationPolicy`에 승인 정책 `FcEvaluationV1`(`fc-evaluation-policy-v1`) 추가: FC-D6.3 최소 분모 8개(8,158 / 15,297 / 38,239 / 29,964 / 40,906 / 3,043 / 4,795 / 6,229), FC-D6.4 층별 최소(F1 3,497/4,662, F8 2,670/3,560), FC-D1/D2 판정 코호트(F1·F6·F7·F8 → `neutral-mixed-v1`, F2~F5 → `virtual-league-v1`), 표준화 지표 F1·F8(3/7·4/7), FC-D7.1 층 하한 100,000 유효 PA. `Hash()`는 정책 정규 텍스트의 SHA-256(추적용, 승인 근거 아님). 시험이 Tuning 목표에서 최소 분모를 독립 재계산해 값이 일치함을 확인한다.
- `StratifiedEvaluator.Evaluate(RunResult, policy)`: Completed 경기만 층별 집계 → F1·F8 = 정수 표준화 ppm `floor((2·10⁶·(3·N_S·D_O + 4·N_O·D_S) + 7·D_S·D_O)/(14·D_S·D_O))`(checked Int128, 중간 반올림 없음), 나머지는 관측 집계. 지표 상태: 비공식(층·코호트 불일치) → `NOT_EVALUATED`(진단값은 보고), 최소 미달(총계 또는 층) → `INSUFFICIENT_SAMPLE`, 그 외 점추정이 구간 안(경계 포함)이면 `PASS`, 밖이면 `FAIL`.
- 층 Gate 순서(FC-D9·SR-D9): 정지 인식 아님/dry/비 calibration → `NOT_EVALUATED`; 미승인 정책 → `NOT_EVALUATED`; 취소·중단 → `NOT_EVALUATED`; 실패 경기 ≥ 1 → `FAIL`; 지표 오류 → `NOT_EVALUATED`; L1~L4 아님 → `NOT_EVALUATED`; `SEEDS_EXHAUSTED` → `INSUFFICIENT_SAMPLE`; 유효 PA < 100,000 → `INSUFFICIENT_SAMPLE`; 공식 지표 중 FAIL 있으면 `FAIL`, 아니면 부족 → `INSUFFICIENT_SAMPLE`, 미판정 → `NOT_EVALUATED`, 전부 PASS → `PASS`. 지표 결합 우선순위(FAIL > INSUFFICIENT > NOT_EVALUATED)는 "층은 보상되지 않음"(FC-D7.3)·"목표 미달은 FAIL"(FC-D9.3)에서 따른 구현 세부다.
- `CombineStrata`: L1~L4가 각각 정확히 한 번, 서로 다른 실행일 때만 결합하고 합산하지 않는다(FAIL 층은 보상되지 않음). `Integration`: 정지 규칙 없이 정확히 100경기 전부 완료(지표 진단) — 공식 `fc3-int` 코호트는 ADR-008에 없으므로(F-C2-A NOTE-2) 코호트 무관 완료 조건만 구현.
- 진단(부동소수, Hash·판정 무관): 관측 집계 ppm, 경기 군집 선형화 비율 추정량 분산 `n/(n−1)·Σ(y−p̂x)²/(Σx)²`(F1·F8은 층별 결합 `w_S²V_S + w_O²V_O`, FC-D1.7), 군집 DEFF, 20경기 Batch-means DEFF(제안서 §8.1의 보고 파라미터, 승인 임계값 아님 — ADR-008 N-2대로 자동 변경 없음), 실제 층별 경기·PA·투구·스윙 수(FC-D1.5).
- F1의 `RecommendedCohort`를 `Neutral`로 정정(FC-D1.8 C-1 해소). F-A/B 평가기는 표준화 지표(F1·F8)에 승인 정책이 와도 공식 판정을 내지 않는다(관측 합산은 진단).
- Stop-aware 실행의 `summary.json`에 `calibration_evaluation`(정책 ID·Hash, 층, 코호트 InputsHash, ConfigHash, run_result_hash, Gate·사유, 층 비율, 지표별 값·최소·상태·진단)을 기록한다. 결과 Hash에는 들어가지 않는다.
- `StratifiedEvaluationTests` 11개: 정책 값과 독립 재계산, 정수 공식(손계산 예, 중간 반올림 금지 반례, 2,000개 난수 대조, 128비트 범위), PASS·경계 포함·FAIL, 표준화 ≠ 관측 합산(비율 3/7 아님), 총계 충족·층 미달 → `INSUFFICIENT_SAMPLE`(S·O 양쪽), 실패·취소·중단·소진(하한 이상 포함)·하한 미만·지표 오류, dry·미승인·비 L 코호트·전체 실행, 4층 결합(보상 금지·누락·중복·dry 혼입), integration 조건, 군집 분산 공식·진단 불변, 실제 엔진 dry 실행의 층 보고.
- **표적 Sabotage 9/9 검출**(B-2·B-3): T1 실행기가 로테이션 무시 → 1 FAIL; T2 모든 경기 Same → 10 FAIL; T3 가중치 3↔4 → 2 FAIL; T4 실패 경기 Gate 무시 → 1 FAIL; T5 층 최소 미검사 → 1 FAIL; T6 InputsHash가 로테이션 무시 → 1 FAIL(첫 시도는 컴파일 실패, 재실행); T7 소진 → INSUFFICIENT 분기 제거 → **처음에는 미검출**(시험의 소진 사례가 PA 하한에도 걸려 가려짐) → 목표 200,000·유효 PA 140,000 사례를 추가해 1 FAIL로 검출; T8 FAIL 층 보상 → 1 FAIL; T9 타순 미회전 → 1 FAIL. 모두 원본 SHA-256 복구.

## G. 회귀·호환성

- 불변 확인: `RunConfig.ConfigHash` 공식 코드 무변경; 기본 스모크(`run`) `config_hash b9bbac72…195a`·`run_result_hash 28e0536c…2ec6`(기존 시험); Legacy Fixture 2개 실제 CLI `REPLAY MATCH`(`28e0536c644d6a38…552ec6`, `9e991e896d1bb476…a8903`, Exit 0); `neutral-v1` InputsHash `9343ad41…cb70`; `OfficialAttestation` v1 필드·Hash 텍스트 무변경; F-C2-A Replay/Attestation/Downgrade/Reparse 시험 전부 PASS; A4~A7 정확한 전체 목록 비교는 비정지 계열에서 그대로(축약 목록 거부 시험 추가).
- 실제 `official` 명령: Exit 2, `PREFLIGHT_REFUSED`, Gate `NOT_EVALUATED`, 이 작업 트리에서 `Git.dirty`, 출력 디렉터리 미생성.
- **계약에 따른 기존 시험 수정 4개 파일(기대값만 갱신, 삭제·약화 없음):**
  - `StrictCliTests.Each_command_has_one_authoritative_whitelist`: `dry` 명령과 `replay`의 dry Pin 옵션 추가 반영(+ `dry` 옵션·필수 목록 단언 추가).
  - `OfficialAndReplayTests`: `neutral-mixed-v1`·`virtual-league-v1`이 이제 구현됨(Resolve 성공), `neutral-opp-v1`은 계속 미구현; Replay에서 코호트 ID를 `neutral-mixed-v1`로 바꾼 변조는 이제 `Replay.cohort_hash_mismatch`(미구현 거부 단언은 `neutral-opp-v1`로 유지).
  - `EvaluationTests` 3개: FC-D1.8에 따라 F1 권장 코호트가 Neutral. 기존 "중립 코호트의 F1은 판정 없음" 단언은 반대로 바뀌었고(이제 FAIL 판정 가능), 같은 자리에 "가상 리그 코호트의 F1은 판정 없음" 단언을 추가했다.
  - 기존 시험 중 SR-D6 여유(`max_games > seeds`)에 의존한 공식 계열 시험은 없었다(`fc2-dry` 사례만 존재, 그대로 통과).

## H. 전체 검증

| 명령 | 환경 | 결과 |
|---|---|---|
| `dotnet build StarPlayer.sln -c Release --no-incremental` | Windows 11, .NET SDK 8.0.425 | exit 0, 경고 0, 오류 0 |
| `dotnet test StarPlayer.sln -c Release --no-build` | 동일 | **2,715 PASS / 0 FAIL / 0 SKIP** — Domain 327, Application 127, Simulation 741, Infrastructure 1,233(모두 기준과 동일), SimHarness **287**(기준 250 + 신규 37: StopReplay 20, CalibrationCohort 6, StratifiedEvaluation 11) |
| 실제 CLI(`StarPlayer.SimHarness.dll`) | 동일 | Legacy replay 2개 MATCH Exit 0, `official` Exit 2 `PREFLIGHT_REFUSED` |
| `tools/verify.ps1 -LogName PHASE_3F_FC2B_VERIFY_LOG.md` | 동일 | **`GATE: PASS`**, exit 0 (§H-1) |

- Sabotage 실행 후 남은 바이너리가 변조 소스로 빌드됐을 수 있어, 최종 증거는 `--no-incremental` 재빌드 후 실행한 결과다.
- **§H-1 `verify.ps1`:** 실제 실행 결과 `GATE: PASS`(exit 0). Toolchain·Release 빌드(11개 프로젝트, 경고 0)·Domain 327·Simulation 741·Application 127·Architecture 14·Determinism 48·Infrastructure 1,233·SimHarness 287(모두 0 FAIL·0 SKIP)·Godot headless 3종·Windows export·Exported program 3종·문서 링크(146개 파일) 전부 PASS, INFO 2건(변경 31개 항목, 잔존 Build-server 없음). 로그 `docs/reports/PHASE_3F_FC2B_VERIFY_LOG.md`(SHA-256 `b2f8b7ff…`), 원시 로그는 `build/verify-archive/PHASE_3F_FC2B_run1/`에 복사(`diff -rq` 동일, `verify-result.json` `0b6ecbdd…`). `PHASE_2_VERIFY_LOG.md`·`PHASE_3F_FC2A_VERIFY_LOG.md`는 변경되지 않았다(git status 확인), `build/verify-archive/PHASE_3F_FC2A_run1/`(28개 파일) 보존. 출력 분리: `-LogName`으로 추적 로그를 새 파일 `docs/reports/PHASE_3F_FC2B_VERIFY_LOG.md`에 쓰고 `PHASE_2_VERIFY_LOG.md`·`PHASE_3F_FC2A_VERIFY_LOG.md`는 건드리지 않는다. 스크립트가 지우는 `build/verify/`는 실행 전 `build/verify-archive/PHASE_3F_FC2A_run1/`과 `diff -rq` 동일(보존본 존재)했고, `build/export/`는 재생성되는 빌드 산출물이다. `build/verify-archive/` 자체는 스크립트가 건드리지 않는다.

## I. 남은 문제·위험·F-C2-C 연계

- **NOTE-B1 (Official 경로 미활성):** 공식 `fc3-cal` 실행 명령은 여전히 Preflight만 한다(`Official.execution_not_implemented`/NOT_READY). 정지 인식 실행기·v2·Replay 재도출은 공식 Replay에 연결됐지만, 공식 실행의 활성화·출력 쓰기는 Final Execution Freeze 이후 별도 지시 대상이다. 공식 출력 쓰기 단계에서 F-C2-A NOTE(TOCTOU, 출력 디렉터리의 Git Dirty 분류)를 다시 판단해야 한다.
- **NOTE-B2 (dry Pin은 운영자 신뢰):** dry Replay의 신뢰는 명령줄 Pin을 준 운영자에게 있다(SR-D4.3에 따라 진단 전용). 공식 검사에는 영향이 없다.
- **NOTE-B3 (평가 결합 규칙):** 층 Gate의 지표 결합 우선순위와 `CombineStrata`·`Integration`은 ADR 문구에서 도출한 구현 세부다. 독립 리뷰에서 해석 확인이 필요하다.
- **NOTE-B4 (Batch 크기):** Batch-means DEFF의 20경기는 제안서 §8.1의 후보값을 진단 파라미터로만 쓴다. 판정·자동 변경 없음.
- **NOTE-B5 (`fc3-int` 코호트):** ADR-008에 공식 코호트가 없어(F-C2-A NOTE-2) integration 평가는 완료 조건만 구현했다.
- **Residual risk (ADR-009):** 운영자가 비공개로 여러 번 실행하고 하나만 공개하는 위험은 Replay로 제거되지 않는다(프로세스 통제, Final Execution Freeze에 명시 필요).
- **F-C2-C로 넘김:** `neutral-opp-v1`, 12개 행렬 정책, 48셀 실행기·판정기, A-1 검증, PZL #5·#6·#10·#12a, 실제 `fc2-dry` 파일럿(σ_g·DEFF 측정). F-C2-C는 이번 `dry` 명령·`StratifiedEvaluator` 진단을 그대로 쓸 수 있다.
- **Final Execution Freeze 전 필요:** 블록 크기(7/63 후보)·상한, 공식 Seed 목록·Pin, 코호트 InputsHash 동결(현재 `neutral-mixed-v1`·`virtual-league-v1` 값은 시험이 결정성만 확인), 실행 정책 승인, 공식 Git SHA, 빌드 산출물-소스 결속 증명, 정지 인식 dry run의 end-to-end Replay와 독립 리뷰.
- 신규 미해결 계약 충돌: 없음.

## J. 변경 Manifest

| 구분 | 파일 | 내용 |
|---|---|---|
| 신규 | `tools/StarPlayer.SimHarness/StopRule.cs` | 정지 규칙·실행기·정지 기록·`run-result-v2`·Replay 대조 |
| 신규 | `tools/StarPlayer.SimHarness/CalibrationCohorts.cs` | `neutral-mixed-v1`, `virtual-league-v1` |
| 신규 | `tools/StarPlayer.SimHarness/Evaluation.cs` | Strata L1~L4, `StratifiedEvaluator`, Gate, 결합, integration, 진단 |
| 수정 | `tools/StarPlayer.SimHarness/SimRunner.cs` | `RunResult` 정지 인식 속성·의미, `RunStopAware` |
| 수정 | `tools/StarPlayer.SimHarness/SeedManifest.cs` | `AfterGames` 위임, SR-D4.1 dry 목표, SR-D6 |
| 수정 | `tools/StarPlayer.SimHarness/Cohorts.cs` | `HandednessStratum`, `CohortRotation`, `SetupFor`, `StratumOf`, InputsHash 로테이션 결속, 공용 Sheet 생성기 |
| 수정 | `tools/StarPlayer.SimHarness/GameExecutor.cs` | 경기별 팀 구성 사용 |
| 수정 | `tools/StarPlayer.SimHarness/Replay.cs` | 코호트 Registry, `DryStopPin`, 결과 버전·정지 기록 검증 |
| 수정 | `tools/StarPlayer.SimHarness/Official.cs` | `VerifyAgainstSeedFile`가 검증된 Seed 파일 반환(기존 오버로드 유지) |
| 수정 | `tools/StarPlayer.SimHarness/Program.cs` | `dry` 명령, v2 Replay, Exit Code(중단 = 2) |
| 수정 | `tools/StarPlayer.SimHarness/Cli.cs` | `dry` 화이트리스트, `replay` dry Pin 옵션 |
| 수정 | `tools/StarPlayer.SimHarness/RunReport.cs` | v2 매니페스트 필드, 상태 라벨, 요약의 `calibration_evaluation` |
| 수정 | `tools/StarPlayer.SimHarness/MetricsEvaluator.cs` | `FcEvaluationV1`, 정책 확장, F1 코호트 정정, 표준화 지표 보호 |
| 수정 | `tools/StarPlayer.SimHarness/README.md` | 사용법 |
| 신규 | `tests/StarPlayer.SimHarness.Tests/StopReplayTests.cs` | B-1 시험 20 |
| 신규 | `tests/StarPlayer.SimHarness.Tests/CalibrationCohortTests.cs` | B-2 시험 6 |
| 신규 | `tests/StarPlayer.SimHarness.Tests/StratifiedEvaluationTests.cs` | B-3 시험 11 |
| 수정 | `tests/…/StrictCliTests.cs`, `OfficialAndReplayTests.cs`, `EvaluationTests.cs` | §G의 계약 반영 |
| 신규 | 이 보고서, `docs/reports/PHASE_3F_FC2B_VERIFY_LOG.md`(verify.ps1 생성), `Star/02_Decisions/2026-10-18_Phase_3F_FC2B_Stop_Replay_Evaluation.md` | 기록 |
| 수정 | `CURRENT_STATE.md`, `docs/game-engine.md`(§37), `docs/SYSTEM_CONTRACTS.md`(항목 1줄), `docs/decisions/ADR-009.md`(Status 구현 상태 문구만), `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md` | 상태 |

## K. Git 상태와 보호 파일

- Branch `main`, HEAD `e9209b33ee3c55b609749779ab3c79d3a04eed94` 불변, Staging 0, Commit 0, Push 0, Rebase/Reset/Clean 없음.
- 보호 파일 SHA-256(작업 중 쓰기 없음): `TASK.md` `8fc9a9ed…3a92`(이번 지시서), `docs/2023recordRule.pdf` `9742caaf…12b0`, `docs/2026-official-baseball-rules.pdf` `f85860f3…7859`(F-C2-A 기록과 동일). `build/verify-archive/PHASE_3F_FC2A_run1/` 보존.
- 최종 `git status`는 채팅 Handoff에 실제 출력으로 기록한다.

## L. Addendum 1 — Codex High 독립 리뷰 지적 수정 (FC2B-01 MAJOR · FC2B-02 MINOR)

범위: `TASK.md` "Phase 3-F F-C2-B Review Findings Remediation". Harness만 변경(엔진·RNG/CAS·`ConfigHash`·`run-result-v1/v2` 바이트 계약·Attestation v1·ADR-008/009 본문 불변). 상태: FC2B-01 / FC2B-02 `RESOLVED 후보`, F-C2-B는 **독립 재검토 대기**(최종 Closeout 아님), F-C2-C `NOT_STARTED`, Official Execution `NOT_READY`, Phase 3-F / Phase 3 Gate `INCOMPLETE`.

### L-1. FC2B-01 — Integration Gate가 지표 계산 실패에도 PASS
- **원인:** `StratifiedEvaluator.Integration`이 완료 경기 수·실패/취소만 보고 `run.MetricsComputed`(= `Metrics.IsSuccess`)를 보지 않았다(ADR-008 FC-D9.1 "엔진 또는 지표 실패는 공식 Gate FAIL").
- **수정:** `Evaluation.cs`의 `Integration` 한 곳. 미완료·취소 → `NOT_EVALUATED`(불변), 실패 경기 → `FAIL`(불변), **지표 계산 실패(`Metrics.target_missing` / `Metrics.counts_inconsistent` 등) → `FAIL`**, 그 외 → `PASS`. 개별 지표의 `NOT_EVALUATED` / `INSUFFICIENT_SAMPLE` 판정은 *성공한 계산의 결과*이므로 Gate를 막지 않는다(`MetricsComputed` 의미 그대로, `RunReport.RunStatus`의 `METRICS_ERROR`와 같은 구분).
- **수정 전/후:** 100경기 완료 + 지표 계산 오류 → 전 `PASS` / 후 `FAIL`(사유에 오류 코드). 100경기 완료 + 정상 계산(Unapproved 정책의 NOT_EVALUATED 지표) → `PASS` 유지. 99경기·취소 경기는 지표 상태와 무관하게 `NOT_EVALUATED`, 실패 경기는 `FAIL` 유지.
- **회귀 테스트:** `StratifiedEvaluationTests.The_integration_gate_is_never_pass_when_the_metric_computation_itself_failed`(수정 전 RED 실제 확인: `Expected: Fail / Actual: Pass`).
- **알려진 인접 사항(범위 밖, 미변경):** 층(stratum) Gate의 같은 상황(`Gate()`의 `!run.MetricsComputed`)은 `NOT_EVALUATED`를 반환하며 FC-D9.1의 문구(FAIL)와 다를 수 있다. 이번 TASK는 Integration에 한정되므로 바꾸지 않았고 재검토 시 판단이 필요하다(새 위험 §L-5).

### L-2. FC2B-02 — v2 Replay가 일부 실행 기록 필드를 대조하지 않음
- **원인:** `StopReplay.Compare`가 정지 기록·PA·경기별 `(game_id, seed, status, result_hash)`·`run_result_hash`만 재도출과 대조했다. `results.games_requested/completed/failed`, `pitches`, `run_status`, 경기별 `event_hash` / `rng_hash` / `error_code`는 파싱조차 하지 않아 변조해도 `REPLAY MATCH`였다.
- **수정(기존 구조 확장, 중복 구현 없음):** `StopRule.cs`에 `RecordedGame`(+ `Of(GameRunResult)`)을 도입하고 `RecordedStopResult`에 `Requested/Completed/Failed/Pitches/RunStatus`를 추가(`RecordedStopResult.Of(RunResult)` = 정직한 기록). `Replay.cs`의 `WithStop`이 해당 필드를 **필수**로 읽는다(없거나 형식이 틀리면 `Replay.manifest_invalid`, 기본값 없음). `Compare`는 재실행(`derived`)에서 같은 값을 만들어 비교한다(기록값은 신뢰하지 않음). 기존 검사 순서·코드는 그대로이고 새 검사는 기존 경기 검사 뒤·`run_result_hash` 앞에 놓였다.
- **오류 코드(기존 `Replay.*` 분류 규약, 새 정책 없음):** 경기별 `status / result_hash / event_hash / rng_hash / error_code` 불일치 → 기존 `Replay.result_games_mismatch`(메시지만 확장); `games_requested/completed/failed/pitches` → 신규 `Replay.result_counts_mismatch`; `run_status` → 신규 `Replay.run_status_mismatch`. 취소·중단 실행(`CANCELLED` / `ABORTED_INTEGRITY`)은 종전대로 `Replay.run_not_terminated`로 명시 거부. **실패 경기가 있는 규칙 정지 실행은 기존 계약(SR-D9.2)대로 replay 가능**하며 실패 관련 필드(`games_failed`, 경기 `status`, `error_code`, `run_status = GAMES_FAILED`)까지 대조한다.
- **필드별 변조 검출(정상·실패 두 월드에서 각각, 스키마 파싱 오류가 아닌 재실행 대조로):** `games_completed` · `games_failed` · `games_requested` · `pitches` → `result_counts_mismatch`; 경기 `event_hash` · `rng_hash` · `error_code`(주입/삭제) → `result_games_mismatch`; `run_status`(정상 종료 상태 간 `COMPLETED` ↔ `GAMES_FAILED`, `METRICS_ERROR`) → `run_status_mismatch`; 위 필드 중 하나라도 누락 → `manifest_invalid`. 수정 전에는 모두 `REPLAY MATCH`(RED: `a forged execution-evidence field was accepted as REPLAY MATCH`).
- **회귀 테스트:** `StopReplayTests.Every_recorded_execution_field_of_a_v2_result_is_compared_with_the_re_execution`. 기존 2개 테스트의 *설정 코드*만 새 생성자에 맞게 `RecordedStopResult.Of(run)`으로 바꿨고(단언 불변), `Cal()` 픽스처에 실패 경기 주입 인자를 추가했다.

### L-3. 호환성
- v1 Hash·`run-result-v1` 텍스트·`run-result-v2` 골든 벡터·`ConfigHash`·Attestation v1은 바이트 단위로 변경하지 않았다(해당 기존 시험 모두 PASS). 비정지(legacy / full-execution) replay 경로는 변경 없음 — 레거시 manifest 픽스처 replay MATCH 유지. 정지 인식 v2 replay는 이제 위 필드를 필수로 요구하므로, 이 필드를 기록하지 않은 *가상의* v2 manifest는 `manifest_invalid`가 된다(실제로 F-C2-B 이전·이후 어떤 v2 manifest도 이 필드를 항상 기록했고 Official 실행은 아직 없음). 다운그레이드(`v1` 선언·`stop` 제거)는 종전대로 `Replay.result_version_downgrade` / `stop_record_missing`으로 거부된다.

### L-4. 검증 (실제 실행)
- 선행: 신규 2개 테스트 RED 확인 → 수정 → GREEN. `dotnet build` Release 0 warnings / 0 errors.
- `tools/verify.ps1 -LogName PHASE_3F_FC2B_FIX1_VERIFY_LOG.md` → **`GATE: PASS`**: Domain 327 · Simulation 741 · Application 127(Architecture 14, Determinism 48) · Infrastructure 1,233 · **SimHarness 289**(287 + 2) 모두 0 FAIL / 0 SKIP(합계 2,717), Godot headless 4종·Windows export·Exported program 3종·문서 링크(147개 파일) PASS.
- 로그: `docs/reports/PHASE_3F_FC2B_FIX1_VERIFY_LOG.md`(SHA-256 `20cfa8c2…1f0`); 원시 증거 `build/verify-archive/PHASE_3F_FC2B_FIX1_run1/`(`diff -rq build/verify` 동일, `verify-result.json` `e0117222…7f2c`). **기존 증거 보존:** `PHASE_3F_FC2B_VERIFY_LOG.md`(`b2f8b7ff…`), `PHASE_3F_FC2A_VERIFY_LOG.md`, `PHASE_2_VERIFY_LOG.md`, `build/verify-archive/PHASE_3F_FC2A_run1/`·`PHASE_3F_FC2B_run1/`는 실행 전후 SHA-256 동일 / 미변경.
- Official Execution 비활성: `SeedManifestPins.Official` 비어 있음·`OfficialApprovals.Current.ExecutionPathReady == false`·승인 없는 official 거부(`Official.seed_manifest_not_frozen`) 단언이 전체 시험에 포함되어 PASS.

### L-5. 남은 문제·새 위험
- 층 Gate의 지표 계산 실패 처리(`NOT_EVALUATED`)와 FC-D9.1(FAIL) 정합 여부는 이번 범위 밖 — 독립 재검토에서 결정 필요(정책 문구 해석이므로 임의로 확정하지 않음).
- `Replay.result_counts_mismatch` / `Replay.run_status_mismatch`는 신규 코드명이다(ADR-009 SR-D3.6의 코드 목록에 열거되지 않은 구현 세부; 정책 변경 없음).
- 독립 재검토(Codex) `미실시`. F-C2-B를 최종 Closeout으로 표기하지 않는다.

### L-6. 변경 Manifest (이번 수정)
| 구분 | 파일 |
|---|---|
| 수정(개발) | `tools/StarPlayer.SimHarness/Evaluation.cs`(Integration), `tools/StarPlayer.SimHarness/StopRule.cs`(`RecordedGame`, `RecordedStopResult`, `Compare`), `tools/StarPlayer.SimHarness/Replay.cs`(`WithStop` 파싱) |
| 수정(시험) | `tests/StarPlayer.SimHarness.Tests/StratifiedEvaluationTests.cs`(+1), `tests/StarPlayer.SimHarness.Tests/StopReplayTests.cs`(+1, 설정 2곳·`Cal` 인자) |
| 신규(기록) | `docs/reports/PHASE_3F_FC2B_FIX1_VERIFY_LOG.md`, `build/verify-archive/PHASE_3F_FC2B_FIX1_run1/`(git 무시) |
| 수정(상태) | 이 보고서, `CURRENT_STATE.md` |

Git: Branch `main`, HEAD `e9209b33ee3c55b609749779ab3c79d3a04eed94` 불변, Staging 0, Commit 0, Push 0. 보호 파일(`TASK.md` `a9dd84b2…2dfc`, `docs/2023recordRule.pdf` `9742caaf…12b0`, `docs/2026-official-baseball-rules.pdf` `f85860f3…7859`) 작업 전후 SHA-256 동일.

## M. Addendum 2 — FC2B-03 (층 Gate의 Metrics 계산 실패 처리) — `REVIEW_PENDING`

범위: `TASK.md` "F-C2-B Review Fix 2: Per-Stratum Metrics Failure Gate". Harness만 변경. FC2B-01(Integration) / FC2B-02(v2 Replay) 수정과 ADR-008/009 본문·Hash·Golden·Attestation 계약은 불변.

### M-1. 분석: 승인 정책이 요구하는 것
- **ADR-009 SR-D9.2 결과표(승인됨, FC-D9 "kept: SR-D3 / SR-D9")**: 규칙 종료 + 실패 경기 → `FAIL`(FC-D9.1); 규칙 종료 + **지표 계산 불가 → `NOT_EVALUATED`**(기술 결과 exit 3); 취소·무결성 중단 → `NOT_EVALUATED`. 따라서 Addendum 1 §L-5에서 제기한 의문("층 Gate가 `NOT_EVALUATED`를 돌려주는 것이 FC-D9.1과 다를 수 있다")은 **`Gate()`의 해당 분기에 대해서는 근거가 없다**: 승인된 SR-D9 표가 바로 그 `NOT_EVALUATED`를 명시한다. ADR-008 FC-D9.1("엔진 또는 지표 실패는 그 공식 검증 Gate를 실패시킨다")과는, 층 판정이 `NOT_EVALUATED`여도 결합 판정(`CombineStrata`)·최종 Gate가 PASS가 될 수 없다는 점에서 양립한다. **정책 변경 없음**; 이 해석은 `ADR-009`가 "ADR-008과의 실질적 모순 없음"으로 이미 확인한 범위이며, 독립 검토가 다르게 읽는다면(지표 오류 = 층 `FAIL`) ADR-009 SR-D9.2의 해당 행을 바꾸는 *정책 결정*이므로 이 수정에서는 하지 않았다.
- **Integration과의 역할 차이(유지):** Integration(`fc3-int`, 정지 규칙 없음, FC-D7.2)에는 SR-D9 표가 적용되지 않고 FC-D9.1만 적용되므로 FC2B-01대로 지표 계산 실패 = `FAIL`. 층 Gate(정지 인식 `fc3-cal`)는 SR-D9.2대로 `NOT_EVALUATED`. 두 경로의 결과가 다른 것은 의도된 계약 차이다.

### M-2. 실제로 확인된 결함 (정책 위반 2건)
1. **실패 경기가 지표 오류 뒤에 숨는다.** `StratifiedEvaluator.Evaluate`는 지표 오류에서 평가 객체 없이 `Result.Failure`를 반환하여 `Gate()`의 우선순위(실패 경기 `FAIL` → 지표 오류 `NOT_EVALUATED`)에 도달하지 못했고, `summary.json`은 `gate_status = NOT_EVALUATED`만 기록했다. 실패 경기와 지표 오류가 함께 있으면 SR-D3.3 / SR-D9.2(실패 경기는 항상 `FAIL`)가 어겨졌다. (수정 전 RED: `gate_reason` 없음 → 실패 경기 케이스는 `NOT_EVALUATED`.)
2. **`CombineStrata`가 알려진 `FAIL`을 숨긴다.** 누락 층 검사가 `FAIL` 검사보다 앞서, 한 층이 `FAIL`이고 다른 층 평가가 없으면(예: 지표 오류로 평가 객체가 없음) 결합 판정이 `NOT_EVALUATED`였다(FC-D7.3 "실패 층은 다른 층으로 보상되지 않음" 위반 소지; RED: `Expected: Fail / Actual: NotEvaluated`). 또한 `INSUFFICIENT_SAMPLE`/`NOT_EVALUATED` 사유 문구가 다른 층의 상태(예: 지표 오류)를 가렸다.

### M-3. 수정 (최소)
- `Evaluation.cs`: `Gate()`의 지표 이전 분기(비-calibration·dry·미승인 정책 / 취소·중단 / 실패 경기)를 `BeforeMetrics`로 추출하여 공유하고(동작 불변), 평가가 불가능한 실행용 `GateOfFailedEvaluation(run, policy, error)`를 추가했다: 우선순위는 `Gate()`와 동일(실패 경기 → `FAIL`, 이때 사유에 지표 오류 코드를 덧붙임; 취소·중단·비-calibration → `NOT_EVALUATED`; 그 외 지표 오류 → `NOT_EVALUATED`, 오류 코드 명시). 결코 PASS가 되지 않는다.
- `RunReport.cs`: 평가 실패 분기(`summary.json`의 `calibration_evaluation`, `summary.txt`)가 위 함수를 사용하고 `gate_reason`을 기록한다(파생 데이터, Hash 비포함).
- `CombineStrata`: `FAIL`이 누락 층보다 우선(`FAIL`이 가려지지 않음, 누락 층은 사유에 기재); 검증 순서·판정 우선순위(`FAIL` > `INSUFFICIENT_SAMPLE` > `NOT_EVALUATED`)는 불변이며, PASS가 아닌 모든 층을 `층=상태 (사유)`로 결합 사유에 나열한다(지표 오류가 다른 층의 `INSUFFICIENT_SAMPLE`에 묻히지 않음). 누락 층만 있으면 종전대로 `NOT_EVALUATED`.
- 표본 부족과 지표 오류가 함께 있으면: 지표 오류가 우선(`Gate()` 순서 불변: 지표 오류 분기가 Seed 소진·PA 하한 분기보다 앞) — 시험으로 고정.

### M-4. 시험 (수정 전 RED 실제 확인 → GREEN)
- `A_metrics_error_is_not_evaluated_but_never_hides_a_failed_game_and_never_passes`: 지표 오류만 → `NOT_EVALUATED`(오류 코드 포함); 오류 + 실패 경기 → `FAIL`; 오류 + 취소 / 무결성 중단 / dry → `NOT_EVALUATED`; 오류 + Seed 소진 → `NOT_EVALUATED`; 합성 `Metrics` 실패 → `Gate()` 비-PASS.
- `The_combination_never_hides_a_failed_stratum_or_a_stratum_that_could_not_be_evaluated`: `FAIL` + 누락 → `FAIL`; 누락만 → `NOT_EVALUATED`; `INSUFFICIENT_SAMPLE` / `NOT_EVALUATED` 결합 사유에 지표 오류 코드와 층 표시.
- 회귀 유지: FC2B-01 `The_integration_gate_is_never_pass_when_the_metric_computation_itself_failed`, 기존 Integration 시험, FC2B-02 `Every_recorded_execution_field_of_a_v2_result_is_compared_with_the_re_execution`, 기존 `Failure_cancellation_exhaustion_and_metric_errors_take_the_documented_gate_branch`·`The_four_strata_are_judged_together_without_pooling_or_compensation`(단언 불변) 모두 PASS.

### M-5. 신규 Replay 오류 코드의 ADR-009 정합성
`Replay.result_counts_mismatch`, `Replay.run_status_mismatch`: **허용되는 구현 세부사항**으로 판단한다. 근거: ① SR-D3.6 "정확한 라벨과 오류 코드는 F-C2-B에서 기존 어휘(`Replay.*` 등) 위에 정한다; 이 ADR은 의미를 고정하고 철자는 고정하지 않는다"; ② SR-D8.4 "what a replay must refuse (**code spelling in F-C2-B**)"; ③ SR-D8.3 "`results.stop`과 기록된 `plate_appearances`(**및 모든 다른 기록된 카운트**)는 PASS에 충분하지 않다 — 재도출 값과 일치해야 한다"가 카운트·상태 대조를 요구한다; ④ ADR-009에도, `docs/`·`tools/StarPlayer.SimHarness/README.md`에도 `Replay.*` 폐쇄 목록은 없고(결정 노트는 `Replay.stop_*`·`Replay.result_*` 계열 예시만 언급), 새 Exit Code도 없다(replay 불일치는 종전 exit 1). 새 정책 승인 불필요; ADR은 수정하지 않았다.

### M-6. 검증 (실제 실행)
- 선행 시험 RED 확인 후 GREEN; SimHarness 단독 291/291 PASS. `dotnet build` Release 0 warnings / 0 errors.
- `tools/verify.ps1 -LogName PHASE_3F_FC2B_FIX2_VERIFY_LOG.md` → **`GATE: PASS`**: Domain 327 · Simulation 741 · Application 127(Architecture 14, Determinism 48 포함) · Infrastructure 1,233 · **SimHarness 291**(289 + 2) = **2,719 PASS / 0 FAIL / 0 SKIP**; Godot headless 4종·Windows export·Exported program 3종·문서 링크 PASS.
- 증거: `docs/reports/PHASE_3F_FC2B_FIX2_VERIFY_LOG.md` SHA-256 `64b6bfe1eb36254b2fa4f0ec09adf0ec5b4470063f6ca2f33a292857326b3577`; 원시 증거 `build/verify-archive/PHASE_3F_FC2B_FIX2_run1/`(`diff -rq build/verify` 동일, `verify-result.json` `20829409a812769b63e843bf76d40e5983e4649a42ac5b8ea54e62627deb6ecb`). 기존 F-C2-A·F-C2-B run1·FIX1 로그와 `build/verify-archive/` 기존 84개 파일은 실행 전후 SHA-256 동일. Official Execution은 계속 `NOT_READY`(관련 시험 PASS).

### M-7. 남은 문제·독립 재검토 필요 사항
- 지표 계산 오류를 층 `NOT_EVALUATED`(ADR-009 SR-D9.2 현행)로 둘지 `FAIL`(ADR-008 FC-D9.1의 문자적 해석)로 바꿀지는 *정책 해석*이다. 이번에는 승인된 SR-D9.2를 따랐고 바꾸지 않았다 — 변경하려면 ADR-009 개정이 선행되어야 한다.
- `CombineStrata`의 `FAIL`-우선 순서 조정은 FC-D7.3(보상 금지)·FC-D9.1을 근거로 한 최소 수정이다. 독립 재검토에서 확인 바람.
- F-C2-B 상태는 독립 재검토 전까지 `REVIEW_PENDING`(최종 Closeout 아님); F-C2-C `NOT_STARTED`; Official Execution `NOT_READY`; Phase 3-F / Phase 3 Gate `INCOMPLETE`.

### M-8. 변경 Manifest (이번 수정)
수정(개발): `tools/StarPlayer.SimHarness/Evaluation.cs`, `tools/StarPlayer.SimHarness/RunReport.cs`. 수정(시험): `tests/StarPlayer.SimHarness.Tests/StratifiedEvaluationTests.cs`(+2). 신규(기록): `docs/reports/PHASE_3F_FC2B_FIX2_VERIFY_LOG.md`, `build/verify-archive/PHASE_3F_FC2B_FIX2_run1/`(git 무시). 수정(상태): 이 보고서, `CURRENT_STATE.md`. Git: HEAD `e9209b33…` 불변, Staging 0, Commit 0, Push 0. 보호 파일(`TASK.md` `3a0248b4…a54c`, 두 PDF) SHA-256 작업 전후 동일.

## N. Addendum 3 — 마지막 비차단 MINOR (`CombineStrata` FAIL 분기의 사유 누락) — `READY_FOR_OWNER_CLOSEOUT_REVIEW`

Codex High 독립 재검토: FC2B-01~04 핵심 결함 해결 확인, **BLOCKER 0 / MAJOR 0 / MINOR 1**. 이 Addendum은 남은 MINOR만 다룬다(`TASK.md` "Phase 3-F F-C2-B Final MINOR Fix"). Harness 평가 문구만 변경; 판정·우선순위·ADR-008/009 본문·Hash·엔진 계약 불변.

- **결함:** `CombineStrata`의 FAIL 분기가 FAIL 층 ID만 출력하여 같은 결합의 다른 비PASS 층(예: L3 `INSUFFICIENT_SAMPLE`, 지표 오류로 인한 `NOT_EVALUATED`)의 상태·사유가 누락됐다.
- **수정(`Evaluation.cs`, 최소):** 이미 있던 비PASS 층 목록(`notPass`, `층=상태 (사유)`)을 FAIL 분기보다 앞에서 만들어 FAIL 분기도 재사용한다(FAIL 층 자신 포함, PASS 층은 나열하지 않음). 출력 순서는 입력 순서와 무관하게 층 ID 순서(Ordinal)로 고정했다. 최종 판정(`FAIL > INSUFFICIENT_SAMPLE > NOT_EVALUATED`, FAIL은 누락 층보다 우선, 네 층 비합산, 새 상태 없음)은 그대로다.
- **회귀 테스트(`StratifiedEvaluationTests.A_combined_fail_still_names_every_other_stratum_that_is_not_pass_in_a_stable_order`):** 수정 전 RED 실제 확인(`Sub-string not found`, 사유 `"FAIL 층: L2(다른 층으로 보상하지 않음)."`) → 수정 후 PASS. 범위: FAIL+`INSUFFICIENT_SAMPLE`·FAIL+`NOT_EVALUATED`(오류 코드 포함)에서 최종 FAIL과 양쪽 사유 표시; FAIL+PASS 층은 PASS 사유 없음; 누락 층이 있어도 FAIL 유지·다른 비PASS 층 표시; FAIL 두 개와 입력 순서 뒤섞기에도 동일 출력; FAIL을 제거하면 종전 판정(`INSUFFICIENT_SAMPLE`, `PASS`) 유지.
- **검증(실제 실행):** `dotnet build` Release 0 warnings. `tools/verify.ps1 -LogName PHASE_3F_FC2B_FIX3_VERIFY_LOG.md` → `GATE: PASS`: Domain 327 · Simulation 741 · Application 127(Architecture 14, Determinism 48 포함) · Infrastructure 1,233 · SimHarness 292(291 + 1) = **2,720 PASS / 0 FAIL / 0 SKIP**; Godot headless·Windows export·Exported program·문서 링크 PASS. FC2B-01(Integration)·FC2B-02(v2 Replay 필드 대조)·FC2B-03·기존 Stop/Replay·Hash·Golden·Legacy replay 시험 모두 단언 변경 없이 PASS.
- **증거:** `docs/reports/PHASE_3F_FC2B_FIX3_VERIFY_LOG.md` SHA-256 `c74b795743c2c5404e13d9f707207dcbf59edca368e4bb0a46c66eb69d3a9fbd`; 원시 증거 `build/verify-archive/PHASE_3F_FC2B_FIX3_run1/`(`diff -rq build/verify` 동일; `verify-result.json` `b5f12674517611e44d56aef9599ea43e7b8e93f9e8aa761c9a13f135040f2d34`). 기존 F-C2-A·F-C2-B run1·FIX1·FIX2 로그와 `build/verify-archive/` 기존 112개 파일은 실행 전후 SHA-256 동일.
- **변경 Manifest(이번 수정):** `tools/StarPlayer.SimHarness/Evaluation.cs`, `tests/StarPlayer.SimHarness.Tests/StratifiedEvaluationTests.cs`(+1), 신규 `docs/reports/PHASE_3F_FC2B_FIX3_VERIFY_LOG.md`, `build/verify-archive/PHASE_3F_FC2B_FIX3_run1/`(git 무시), 상태: 이 보고서·`CURRENT_STATE.md`.
- **Git:** HEAD `e9209b33…` 불변, Staging 0, Commit 0, Push 0. 보호 파일(`TASK.md` `89b6c8c9…300b`, 두 PDF) 작업 전후 SHA-256 동일.
- **Closeout 준비:** 새 결함·정책 충돌 없음 → `READY_FOR_OWNER_CLOSEOUT_REVIEW` 유지. 남은 열린 질문은 §M-7(지표 오류의 층 `NOT_EVALUATED` vs `FAIL` 해석 — ADR-009 SR-D9.2 현행 유지)뿐이며 정책 결정은 소유자 몫이다. F-C2-C `NOT_STARTED`, Official Execution `NOT_READY`, Final Execution Freeze `PENDING`, Phase 3-F / Phase 3 Gate `INCOMPLETE`.
