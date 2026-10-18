# Star Player Phase 3-F F-C2 Official Integration Report — 48셀 공식 실행·검증·재현 경로

- 상태: **`IMPLEMENTED_WITH_NOTES` · `TESTED` · `REVIEW_PENDING`** (2026-10-18, 소유자 `TASK.md` "Phase 3-F / F-C2 Official Integration"; 독립 리뷰 `미실시`).
- 기준: Branch `main`, HEAD `58692e4faadc9a58653680e7d05fe9103b5ceef8`(F-C2-C 라이브러리 로컬 Closeout Commit) 불변. Staging·Commit·Push·Reset·Clean·Rebase·Amend 없음.
- **Official Execution `NOT_READY` 유지 · Final Execution Freeze `PENDING` · 실제 공식 실행 미실시 · Phase 3-F / Phase 3 Gate `INCOMPLETE`.** PZL 잔여 검사·Dry Pilot은 착수하지 않았다.
- 정책 문서 [ADR-008](../decisions/ADR-008.md)·[ADR-009](../decisions/ADR-009.md) 본문, `ROADMAP.md`, `docs/SYSTEM_CONTRACTS.md`는 수정하지 않았다.
- **최신 상태(2026-10-18, Closeout 준비): [§13](#13-closeout-준비-addendum--codex-high-집중-재검토-반영-2026-10-18) — `READY_FOR_OWNER_CLOSEOUT_REVIEW`.** 위 상태 줄과 §1~§12는 구현·FIX1 당시 기록으로 보존한다.

## 1. 승인 계약과 통합 구현의 대응

| 승인 계약 | 출처 | 이번 통합 |
|---|---|---|
| 48셀 = 4 Batter × 3 Pitcher × 2 Hand × 2 Mode, 셀별 고유 Competition Key, 셀마다 독립 전체 실행 | FC-D4.1, 제안서 §6.2·§6.6 | 기존 `DominanceMatrix.Cells`(생성식)만 사용. 셀 결속은 기존 `DominanceMatrix.CellOf` |
| Seed 파일 SHA-256 외부 고정, 실패 Seed 교체·제외 금지, Manifest replay 가능, 공식 Git SHA 사전 고정 | FC-D5.2 | 승인값(`OfficialApprovals.SeedManifestPins`)에서만 Seed 파일을 찾고 Pin으로 읽음. 실행 중 매 셀 전후 Pin 재검증 |
| `fc3-dom`은 전체 실행 계약(정지 규칙·`pa_target`·정지 기록 없음), 공식 파일 `max_games == Seed 수` | ADR-009 SR-D5·SR-D6 | `SimRunner.RunSpecs`(전체 실행)만 사용. `StopAware` 결과는 셀 증거가 아님(기존 평가기). `pa_target` 파일은 `CellOf`가 거부 |
| 셀당 공식 표본 수·실행 상한·Seed Manifest는 Final Execution Freeze 값 | FC-D4.7, ADR-008 "Two freezes" | `DominanceGamesPerCell == null`이면 Preflight `NOT_READY`; 동결값과 각 Seed 파일 Seed 수가 다르면 `FAIL` |
| Official Mode 필수 항목(미등록·중복 옵션 거부, `--tuning` 거부, 모델·튜닝·Seed·입력 Hash 검증, Dirty 거부, 공식 replay, 기술 Exit Code와 통계 Gate 분리, 실패 시 재현 증거 보존) | FC-D10 | 기존 엄격 CLI·단일 Manifest Preflight·Git 무결성·Attestation v1을 셀마다 그대로 재사용. 새 명령 `official-dom`·`replay-dom` |
| 실패·재실행: 엔진·지표 실패 → 그 Gate FAIL, 제외 재집계 금지, 미실행·계약 미충족 → `NOT_EVALUATED` | FC-D9 | 기존 `DominanceEvaluator` Gate 그대로. 불완전 실행(취소·무결성 중단·일부 셀)에는 평가 자체를 만들지 않음 |
| 무결성 손상 → 시작 전 거부 또는 실행 중 중단(정지 기록 없음), 취소 ≠ 종료, PASS 불가, 증거 보존 | ADR-009 SR-D3.4·SR-D3.5(정신을 `fc3-dom`에 적용) | `ABORTED_INTEGRITY`·`CANCELLED`는 `complete=false`, Gate `NOT_EVALUATED`, replay 대상 아님. 실행된 셀 산출물은 남김 |
| 기술 결과와 통계 Gate 분리, Exit 0 ≠ PASS | FC-D10, FC-D12, SR-D9.1 | Exit Code는 기존 의미(0/1/2/3)만. Preflight 거부 문서의 `dominance_gate`는 항상 `NOT_EVALUATED`이며 "통계 판정 아님"을 명시 |
| 증거 보관: 원시 출력은 `build/simharness/<run>/`(Git 무시), 정책·Seed·Hash·결과를 버전 관리 | FC-D11 | 출력은 기존 `build/simharness/official/<run>/` 규칙(`OfficialOutput.Resolve`, 새 디렉터리만). 48셀 색인 형식은 **승인된 공식 스키마가 없어 DRAFT**(§5, §9) |

## 2. Seed Manifest와 외부 Pin의 신뢰 경계

- **유일한 신뢰 출처:** `OfficialApprovals`(코드에 고정되는 Final Execution Freeze 값). 현재 `OfficialApprovals.Current`는 모든 값이 비어 있다(Pin 0개, Git SHA·실행 정책·코호트 Hash·셀당 표본 수 `null`, `ExecutionPathReady=false`).
- **명령줄·결과 파일은 신뢰하지 않는다.** `official-dom`은 `--seed-manifest`·`--seed-sha256`·`--seeds`·`--tuning`·`--rules`·`--advancement`·`--event-budget`을 `Cli.option_forbidden`으로, 그 밖의 옵션(`--family`, `--games-per-cell` 등)을 `Cli.option_unknown`으로 거부한다. 숨은 기본값으로 승인 조건을 대신하지 않는다.
- **셀 ↔ Seed 파일 결속 도출(`DominancePreflight.PinnedCells`):**
  1. 승인된 모든 Pin 파일을 Pin으로 읽는다(기존 `SeedManifestReader.Read`: 저장소 상대 경로·reparse point 거부·원본 바이트 SHA-256 = Pin·엄격 JSON·GameId 규칙·SR-D6). 하나라도 읽지 못하면 승인 집합의 무결성 손상으로 `FAIL`.
  2. `family == fc3-dom`인 파일만 행렬 후보. 0개면 `NOT_READY`(`Official.dominance_seed_manifests_not_frozen`).
  3. Competition Key 고유(`Seed.competition_key_reused`), 각 파일이 정확히 한 셀에 결속(`Dominance.cell_unknown` / `cell_binding_mismatch` / `stop_rule_not_allowed`), 48셀 모두 포함(`Official.dominance_cells_missing` — 일부 셀만 공식 실행 불가).
  4. 결과는 행렬 순서의 48개 `PinnedCell`.
- **변조 검출 지점:** 파일 변경 → `Seed.file_hash_mismatch`(Preflight·실행 중 재검증·Replay 모두); 다른 Pin으로 교체 → 기록된 Attestation과 승인 Pin 불일치(`Replay.seed_manifest_hash_mismatch`); 누락 파일 → `Seed.file_missing`; 다른 셀용 파일 → `Dominance.cell_binding_mismatch`.
- **승인값과 시험 Fixture의 분리:** 실제 공식 Seed 파일·Pin·표본 수는 만들지 않았다. 시험은 임시 디렉터리의 합성 `fc3-dom` 파일 48개(가상 Seed 990001·990002, 셀당 2경기)와 그것을 고정한 **시험 전용** `OfficialApprovals` 인스턴스만 쓴다. 시험은 `OfficialApprovals.Current`가 여전히 아무것도 동결하지 않았음을 함께 단언한다.

## 3. 48셀 Official Preflight 조건

`DominancePreflight.Run`은 모든 검사를 실행·보고한다(Fail-Closed, 단락 통과 없음). 게임을 시작하지 않고 아무것도 쓰지 않는다.

| 검사 ID | 내용 | 미충족 시 |
|---|---|---|
| `cli` | 엄격 파서 통과 | (파서가 먼저 Exit 2) |
| `git_integrity`, `git_sha`, `model_tuning` | 기존 F-C2-A 검사 공유(`OfficialPreflight.EnvironmentChecks`로 추출, 동작 불변) | Dirty `FAIL`, bin/obj `NOT_READY`, SHA 미동결 `NOT_READY`, 불일치 `FAIL` |
| `output_directory` | 기존 `OfficialOutput.Resolve`(새 디렉터리만, 공식 루트 내부) | `FAIL` |
| `seed_manifests` | §2의 Pin 도출·48셀 결속 | §2 |
| `games_per_cell` | `DominanceGamesPerCell` 동결 여부, ≥ 2, 모든 셀 Seed 수 = 동결값(SR-D6로 셀 상한과 같음) | 미동결 `NOT_READY`, 1 이하·불일치 `FAIL` |
| `cell_preflight` | 48셀 각각 기존 단일 Manifest `OfficialPreflight.Run`(코호트 승인·코호트 InputsHash 동결·규칙 Hash·정책·구장·실행 정책) → 셀별 **Attestation v1**(불변 계약) | 거부된 첫 셀의 첫 미충족 검사 코드(FAIL 우선) |
| `execution_policy`, `execution_path` | 기존 검사 공유(`OfficialPreflight.ExecutionChecks`) | 미동결 `NOT_READY`, 불일치 `FAIL`, `ExecutionPathReady=false` `NOT_READY` |

- `Ready`는 모든 행렬 검사 PASS **그리고** 48셀 Preflight 모두 Ready일 때만 참이다.
- **Preflight 결과 ≠ 통계 Gate.** 거부 문서는 `run_status: PREFLIGHT_REFUSED`, `dominance_gate: NOT_EVALUATED`, `dominance_gate_reason: "게임을 실행하지 않았습니다. Preflight 결과는 지배 전략 통계 판정이 아닙니다."`를 쓴다. 승인값 누락은 실험의 PASS/FAIL이 아니다.
- 현재 승인값(`Current`)으로는 `seed_manifests`·`games_per_cell`·`git_sha`·`execution_policy`·`execution_path`가 `NOT_READY`이고 `cell_preflight`는 입력 없음으로 `NOT_READY`다. 실제 저장소에서는 `git_integrity`도 bin/obj와 `build/` 출력 때문에 통과하지 않는다(§9 B-4).

### Family별 호환성

| Family | 상태 |
|---|---|
| `fc3-dom` | 승인된 48개 Seed 계획 **전체** 실행(정지 규칙 미적용, 셀마다 `max_games == n`). 단일 `official --seed-manifest <fc3-dom 파일>`은 행렬의 한 셀만 다루므로 실행하지 않고 `Official.dominance_matrix_required`로 `official-dom`을 안내(게임 미실행) |
| `fc3-int` | 기존 전체 실행 계약·리더 규칙(정확히 100경기) 불변. 공식 실행 경로는 이번 범위 밖(`official`은 여전히 Preflight 전용, `Official.execution_not_implemented`) |
| `fc3-cal` | ADR-009 정지 규칙·`run-result-v2`·Stop Replay 불변(관련 시험 전부 PASS). 공식 실행 경로는 이번 범위 밖 |
| `fc2-dry` | 기존 비공식 `dry` 명령·dry replay 불변. 공식 Preflight는 `Official.family_not_official`로 거부 |

## 4. CLI 진입점과 실행 차단

```
StarPlayer.SimHarness official-dom --run-id ID [--out build/simharness/official/DIR]
StarPlayer.SimHarness replay-dom --result OFFICIAL_48_CELL_RESULT_DIR
```

- `official-dom` 흐름: ① 엄격 파싱 → ② 저장소 루트 → ③ `OfficialApprovals.Current`와 실제 `GitIntegrity.Check` → ④ `DominancePreflight.Run`(승인 Manifest·Pin 조회, 48셀 계획, 셀별 Attestation) → ⑤ **Ready일 때만** `DominanceOfficial.Execute` → ⑥ 셀마다 결과 저장 → ⑦ 48셀 평가와 색인 저장.
- 거부 시: Preflight 문서를 표준 출력에, 미충족 검사를 표준 오류에 쓰고 **Exit 2, 게임 미실행, 출력 디렉터리 미생성**. `DominanceOfficial.Execute`는 Ready가 아닌 보고서를 받으면 `InvalidOperationException`을 던져 실행 경로에 들어가지 않는다(라이브러리 수준 이중 차단).
- Exit Code(기존 의미만): 0 완료, 1 경기 실패·취소 또는 replay 불일치, 2 인수·Preflight 거부·무결성 중단, 3 지표 실패. 0은 Gate 판정이 아니다.
- **현재 실제 실행 결과:** `official-dom`은 항상 Exit 2로 거부된다(시험으로 자식 프로세스 실행·출력 디렉터리 미생성 확인). `replay-dom`도 현재 승인값으로는 어떤 결과도 수용하지 않는다(Exit 2).

## 5. 저장 결과와 Replay 검증 경로

### 저장(실행 경로, 시험 Fixture로만 실행됨)

```
<out>/cells/<cell-id>/manifest.json   기존 simharness-manifest-v1 + 기존 official envelope(simharness-official-v1, Attestation v1)
<out>/cells/<cell-id>/summary.json    기존 simharness-summary-v1
<out>/cells/<cell-id>/summary.txt
<out>/dominance-result.json           48셀 색인 (DRAFT: simharness-dominance-result-draft, schema_status DRAFT_NOT_APPROVED)
```

- 셀 산출물은 **셀이 끝날 때마다** 기록한다(이후 실패에도 재현 증거 보존). 색인은 마지막에 기록한다. 실행 시작 직전에 출력 경로가 이미 있으면 아무것도 쓰지 않고 중단한다(덮어쓰기 금지).
- 셀 Manifest는 기존 계약 그대로이므로 **기존 `ReplayCheck.Validate`(공식 envelope·Attestation·승인값·실제 Pin Seed 파일·정확한 경기 목록)를 그대로 통과**한다. `run_result_hash`는 `run-result-v1`, `config_hash`는 불변 `ConfigHash`.
- 색인 필드: 실행 식별(run id, family, run type), `complete`, `run_status`(`COMPLETED`/`GAMES_FAILED`/`METRICS_ERROR`/`CANCELLED`/`ABORTED_INTEGRITY`), 중단 코드, 승인 입력(Git SHA·실행 정책·셀당 표본 수·평가 정책 ID), Preflight 검사, 48셀(번호·ID·Key·Seed 파일 경로·SHA-256·셀 Manifest 경로·파일 SHA-256·`config_hash`·`run_result_hash`·`attestation_hash`·경기 수·셀 상태·사유), Gate·사유·측별 판정, 평가 비교 텍스트(`dominance-evaluation-text-v1`, 기존 `Canonical()` — 해시 아님).
- 색인은 **새 해시를 만들지 않는다**. `official_evidence: false`를 항상 쓰고 그 이유(형식 미승인 + Final Execution Freeze 없음)를 함께 쓴다. 저장 결과가 있다는 사실만으로 Official Approval이 되지 않는다.
- 불완전 결과(취소·무결성 중단)는 `complete: false`, Gate `NOT_EVALUATED`, `evaluation_canonical: null`이며 실행된 셀만 남는다.

### Replay(`DominanceOfficialReplay.Verify`)

게임 실행 전 거부(순서대로):
1. 색인 존재·중복 JSON 항목 없음·스키마 = DRAFT 버전(자기 승격 거부, `DomOfficial.result_schema_unsupported`).
2. `complete == true`이고 `run_status ∈ {COMPLETED, GAMES_FAILED, METRICS_ERROR}`(취소·중단 결과 거부, `DomOfficial.run_not_complete`). 실패 경기가 있는 완료 실행은 재현 증거로 재실행된다(SR-D8.5 취지).
3. 승인 셀당 표본 수 동결 필수, 기록값 = 승인값. 기록된 Git SHA·실행 정책·평가 정책 = 승인값.
4. **승인값에서 신뢰 계획 재도출**(`PinnedCells`, 결과 파일 무시).
5. 48셀 정확히·행렬 순서(누락·순서 변경·교체 거부). 셀 Manifest 경로는 셀 ID로 고정(기록된 경로를 따르지 않음), 파일 SHA-256 = 기록값.
6. 셀 Manifest마다 기존 `ReplayCheck.Validate`(공식 replay 전체 검사) → Attestation의 Seed 파일이 승인값이 그 셀에 결속한 파일과 같음, 실행 ID가 모두 같음(서로 다른 실행 혼용 거부), 색인의 해시·Seed 기록 = 셀 Manifest, 셀 정의로 다시 만든 설정의 GameId·`config_hash` 일치.

재실행 후 비교:
7. 48셀 전체 재실행(셀 정의에서 설정 재구성): 경기별 GameId·Seed·상태·결과/이벤트/RNG 해시·실패 코드, `run_result_hash`.
8. 재실행 결과를 **승인 셀당 표본 수**로 다시 평가: 셀별 상태·사유·경기 수, 측별 판정, Gate, Gate 사유, 실행 상태, 평가 비교 텍스트가 기록과 정확히 같아야 일치. 해시에 없는 Metrics 상태(1~8회 반이닝 득점 관측·셀 상태)는 이 재계산으로 검증된다.

불일치는 경고가 아니라 거부다(게임 전 거부는 Exit 2, 재실행 후 불일치는 Exit 1).

## 6. 기존 A/B/C 계약과의 호환성

- **불변:** `RunConfig.ConfigHash`, `game-result-v1`, `run-result-v1/v2`, `OfficialAttestation` v1(`official-attestation-hash-v1`, envelope `simharness-official-v1`), `OfficialProfile.Hash()`, 기존 `SeedManifestReader` 검증 규칙, Legacy Manifest Fixture 2종과 그 Hash, ADR-009 Stop/Replay, `DominanceStatistics`·`DominanceEvaluator`·`DominanceReplay`, `ef1/ef2`, 엔진·확률 모델·Tuning·Rules·RNG.
- **기존 코드 변경(동작 보존):**
  - `Official.cs`: `OfficialPreflight`의 환경 검사(`git_integrity`·`git_sha`·`model_tuning`)와 실행 검사(`execution_policy`·`execution_path`)를 내부 함수 두 개로 추출해 48셀 Preflight와 공유. 검사 순서·코드·상태 동일. `execution_path` 미준비 메시지 문구만 "F-C2-B/C 없음" → "Final Execution Freeze 없음(`ExecutionPathReady = false`)"으로 갱신(코드 `Official.not_ready` 동일).
  - `Dominance.cs`: `DominanceMatrix.Config`에 선택 인자 `mode`(기본 `"dominance"`) 추가. 모드 문자열은 `ConfigHash` 밖이며 공식 실행은 기존 replay가 요구하는 `"official"`을 쓴다.
  - `Program.cs`·`Cli.cs`: 새 명령 2개. `official` 명령은 `fc3-dom` 파일일 때 마지막 안내 메시지만 `Official.dominance_matrix_required`로 바뀐다(기존처럼 항상 Exit 2, 게임 미실행).
- 기존 시험: `StrictCliTests.Each_command_has_one_authoritative_whitelist` 한 곳만 갱신했다 — 명령 목록에 `official-dom`·`replay-dom`을 추가하고 두 명령의 옵션·필수 목록 단언을 **추가**(약화 없음). 이 시험은 첫 Verify(try1)에서 실제로 실패해 갱신 필요를 드러냈다(§7-3). 그 밖의 기존 시험은 변경 없이 통과.

## 7. 시험 및 검증 결과

모든 결과는 실제 실행 출력이다. 실행하지 않은 것은 마지막 줄에 `미검증`/`미실시`로 적었다.

### 7-1. 신규 시험 `tests/StarPlayer.SimHarness.Tests/DominanceOfficialTests.cs` (12개)

공유 Fixture(`DominanceOfficialWorld`): 임시 루트·합성 Seed 48개·시험 전용 승인값으로 48셀을 **한 번** 실행(96경기). 변조 시험은 결과 디렉터리를 복사한 뒤 수정한다.

| 범주 | 시험 | 확인 내용 |
|---|---|---|
| Preflight | `With_the_real_approvals_the_48_cell_preflight_is_blocked_and_nothing_can_execute` | 실제 승인값 → NOT_READY 검사 6종, 셀 0, `Execute` 진입 시 예외, 출력 미생성, `Current` 무동결 |
| Preflight | `Fully_pinned_test_approvals_make_the_preflight_ready_…` | 48셀 행렬 순서, 셀별 Attestation이 Pin 경로·SHA·Key·코호트·정책·규칙과 결속, 48개 서로 다른 Attestation |
| Preflight | `Each_unmet_48_cell_condition_fails_closed_on_its_own_check` | 표본 수 미동결/1/불일치, 셀 누락(47셀), Pin 불일치, 파일 없음, 행렬 밖 파일, 같은 셀 파일 2개, 셀 결속 위반, 코호트 Hash 미동결/불일치, Dirty, bin/obj, Git SHA 불일치, 실행 정책 불일치, 실행 경로 미준비, 기존 출력 존재; 다른 계열 승인 파일은 행렬에 섞이지 않음 |
| 실행·저장 | `A_ready_preflight_runs_all_48_cells_as_full_runs_…` | 48셀 전체 실행(`StopAware=false`, 계획 = 실행), 평가 = 독립 재평가, Exit Code 일관성, 셀 Manifest가 기존 `ReplayCheck.Validate` 통과, 파일 SHA 일치, 색인 DRAFT·`official_evidence=false` |
| Replay | `The_stored_result_replays_to_a_match_from_trusted_inputs` | 정상 합성 결과 저장·읽기·재현 일치 |
| Replay | `Replay_refuses_changes_to_the_index_before_any_game` | `complete`·취소 상태·스키마 승격·표본 수·Git SHA 기록·셀 누락·순서 교환·경로·파일 해시·결과 해시·필드 누락·색인 없음·색인 미갱신 Manifest 수정 → 게임 전 거부 |
| Replay | `Replay_refuses_untrusted_or_mixed_approval_inputs` | 표본 수 미동결, 실제 승인값, 외부 Pin 불일치, 다른 승인 Git SHA, 실행 경로 미준비, 승인 Seed 파일 교체(재고정) |
| Replay | `Replay_refuses_tampered_cell_manifests` | 다른 실행 ID 셀 혼용, 기록 Seed 변경, 한 경기 결과 해시 변조(재실행 후 검출) |
| Replay | `Replay_recomputes_the_metrics_state_and_the_gate_…` | 셀 상태(Metrics 상태) 편집, Gate·평가 텍스트 일관 편집 → 재계산으로 검출 |
| 실행 실패 | `An_integrity_break_or_a_cancellation_never_yields_a_complete_or_replayable_result` | 4번째 셀 전 무결성 중단(3셀 증거 보존, Exit 2, `complete=false`, Gate `NOT_EVALUATED`, replay 거부), 취소(Exit 1, replay 거부), Preflight 후 Seed 파일 변경 → 첫 게임 전 중단 |
| CLI | `The_48_cell_commands_accept_only_their_whitelisted_options` | 금지 옵션 7종, 미등록 옵션(`--family`, `--games-per-cell`), 필수·중복 |
| CLI | `The_official_48_cell_command_refuses_with_the_real_approvals_and_starts_no_game` | 자식 프로세스 `official-dom` → Exit 2, DRAFT 거부 문서(`PREFLIGHT_REFUSED`, Gate `NOT_EVALUATED`, `official_evidence=false`), 출력 미생성; `replay-dom`이 Fixture 결과를 실제 승인값으로 거부 |

### 7-2. Sabotage(표적 변형 → 시험 실패 확인 → 원복)

`DominanceOfficial.cs`의 10개 지점을 하나씩 변형하고 `DominanceOfficialTests`를 실행한 뒤 원복(원복 후 SHA-256 동일 확인).

| 변형 | 결과 | 검출 시험 |
|---|---|---|
| 48셀 누락 검사 제거 | 검출 | `Each_unmet_48_cell_condition_fails_closed_on_its_own_check` |
| Replay 평가 텍스트·Gate 비교 무력화 | 검출 | `Replay_recomputes_the_metrics_state_and_the_gate_…` |
| 실행 중 무결성 재검증 무력화 | 검출 | `An_integrity_break_or_a_cancellation_…` |
| Replay 경기별 비교 무력화 | 검출 | `Replay_refuses_tampered_cell_manifests` |
| Replay 셀 상태·사유·경기 수 비교 무력화 | 검출 | `Replay_recomputes_the_metrics_state_and_the_gate_…` |
| 셀당 Seed 수 = 동결값 검사 무력화 | 검출 | `Each_unmet_48_cell_condition_fails_closed_on_its_own_check` |
| Replay `complete`/상태 검사 무력화 | 검출 | `Replay_refuses_changes_to_the_index_before_any_game` |
| Replay 실행 ID 혼용 검사 무력화 | 검출(첫 변형 `if (false)`는 컴파일 오류 CS0162로만 실패해 무효 처리, 컴파일되는 변형으로 재실행해 시험 검출) | `Replay_refuses_tampered_cell_manifests` |
| `Ready`의 `Cells.All(Ready)` 제거 | 생존 — **동등 변형**: `cell_preflight` 검사가 이미 같은 조건에서 PASS가 아니므로 결과가 같다(이중 방어로 유지) | — |
| Replay의 "Attestation Seed 경로 = 승인 결속 경로" 검사 제거 | 생존 — **동등 변형**: 기존 `VerifyAgainstSeedFile`이 Attestation의 경로를 승인 Pin으로 읽어 Competition Key를 대조하고, `PinnedCells`가 Key 고유를 강제하므로 다른 경로가 통과할 방법이 없다(이중 방어로 유지) | — |

결과: 유효 변형 8/8 검출, 동등 변형 2개 생존(근거 위 표).

### 7-3. 빌드·전체 시험·Verify

환경: Windows 11 Home 10.0.26200, .NET SDK 8.0.425, Godot 4.7.2.stable.mono. HEAD `58692e4`(작업 트리 변경 있음 — 이번 변경 자체).

| 명령 | 결과 |
|---|---|
| `dotnet build tools/StarPlayer.SimHarness -c Release` | 경고 0, 오류 0 |
| `dotnet test tests/StarPlayer.SimHarness.Tests -c Release --filter "FullyQualifiedName~DominanceOfficialTests"` | 12 PASS / 0 FAIL / 0 SKIP |
| `dotnet test tests/StarPlayer.SimHarness.Tests -c Release` (whitelist 시험 갱신 후) | 330 PASS / 0 FAIL / 0 SKIP |
| `verify.ps1 -LogName PHASE_3F_FC2_OFFICIAL_INTEGRATION_VERIFY_LOG.md` **try1** | **`GATE: FAIL`** — U5 SimHarness 329 PASS / 1 FAIL(`StrictCliTests.Each_command_has_one_authoritative_whitelist`: 새 명령 미반영). 다른 모든 항목 PASS. 증거 보존: [try1 로그](PHASE_3F_FC2_OFFICIAL_INTEGRATION_VERIFY_LOG_try1_FAIL.md) SHA-256 `fdf6347eeac0123b923c038cc3bc0d6b3824c014a7a8d498cc84409dfdd6c1f8`, 원시 `build/verify-archive/PHASE_3F_FC2_OFFICIAL_INTEGRATION_try1_FAIL/`(`verify-result.json` `f2903e13bfa2411c9b2095668e16798c748d5e9f5cdb73ffd01cf876bee44e7c`) |
| `verify.ps1 -LogName PHASE_3F_FC2_OFFICIAL_INTEGRATION_VERIFY_LOG.md` **run1**(시험 갱신 후) | **`GATE: PASS`, exit 0** |

run1 세부: Release 빌드 11개 프로젝트 경고 0; Domain 327 · Simulation 741 · Application 127(Architecture 14, Determinism 48 포함) · Infrastructure 1,233 · **SimHarness 330**(318 + 12) = **2,758 PASS / 0 FAIL / 0 SKIP**; Godot headless 4종, Windows export·신선도, Exported program 3종, 문서 링크 155개 파일 PASS.

- 로그: [`PHASE_3F_FC2_OFFICIAL_INTEGRATION_VERIFY_LOG.md`](PHASE_3F_FC2_OFFICIAL_INTEGRATION_VERIFY_LOG.md) SHA-256 `1ae51610184dd7b631e5f9c54ef772c51eb2da4bb4797c884e41ca2475e10906`.
- 원시 증거: `build/verify-archive/PHASE_3F_FC2_OFFICIAL_INTEGRATION_run1/`(28개 파일, `diff -rq build/verify` 동일; `verify-result.json` `79ea176088be7132647d46953d7886c9cbea9c4272351ddc6264ef49d2ad090f`). Git 무시 로컬 자료.
- **기존 증거 보존:** 실행 전 `build/verify`가 `PHASE_3F_FC2C_FIX1_run1`과 동일함을 확인했고, 기존 `build/verify-archive/` 전체와 기존 `*VERIFY_LOG*.md`·`PHASE_3F_FC2A_VERIFY_EVIDENCE.md`(277개 파일)의 SHA-256이 실행 전후 동일(`sha256sum -c` 전부 OK).
- 이 보고서의 §7~§10과 상태 문서 갱신은 run1 뒤에 작성했다(이 보고서 자체는 run1 전에 만들어져 링크 검사 대상에 포함됨; 이후 추가한 링크는 기존 파일만 가리킨다).
- **미검증/미실시:** Codex·GPT Chat 독립 리뷰 `미실시`; 전체 Mutation `미실시`(위 표적 Sabotage만); 실제 공식 48셀·calibration·integration 실행 `미실시`(금지); 실제 승인 입력에 대한 Replay `미검증`(승인 자료 없음 — 시험 Fixture까지만); 대규모 표본에서의 실행 시간·저장 용량 `미검증`.

## 8. Official Execution 차단 확인

- `OfficialApprovals.Current`: Git SHA·실행 정책 ID/Hash·코호트 InputsHash·셀당 표본 수 모두 미동결, Pin 0개, `ExecutionPathReady=false`(시험 단언).
- `official-dom` 실제 실행 → Preflight 거부, Exit 2, 게임 미실행, 출력 미생성(자식 프로세스 시험).
- 실제 저장소에서는 bin/obj 빌드 출력이 있는 한 `git_integrity`가 `Git.build_output_unverified`(NOT_READY)이고, 승인되지 않은 ignored 출력(`build/` 등)이 있으면 `Git.dirty`다. 즉 Final Execution Freeze 값이 들어와도 빌드 출처 계약이 승인되기 전에는 공식 실행이 시작되지 않는다(§9 B-4).
- 이번 작업에서 공식 Seed 파일·Pin·표본 수·실행 상한·Git SHA를 만들거나 기록하지 않았고, 실제 공식 48셀·calibration·integration 실행을 하지 않았다.

## 9. 공식 계약 공백·소유자 결정 필요 (BLOCKED 항목)

각 항목: Issue / Reason / Recommended Option / Alternative / Impact.

- **B-1 48셀 공식 결과 스키마 미승인.** Reason: FC-D11은 보존할 증거만 정하고 48셀 결과의 정규 형식·버전·Hash는 정하지 않았다. Recommended: 현재 DRAFT 색인 구조(셀별 기존 Manifest+Attestation v1 + 색인)를 검토해 `simharness-dominance-result-v1`로 승인하고, 필요하면 48개 `attestation_hash`·`run_result_hash`·평가 비교 텍스트를 묶는 행렬 수준 해시를 별도 버전으로 승인. Alternative: 색인 없이 셀 Manifest 48개 + 보고서만 증거로 사용. Impact: 승인 전에는 `official_evidence=false`로만 기록되고 F-C3 증거가 될 수 없다.
- **B-2 셀 ↔ Seed 파일 결속의 Freeze 표현.** Reason: 승인값에는 경로 → Pin 표만 있다. 현재는 "고정된 `fc3-dom` 파일 전체가 48셀을 하나씩 덮어야 함"으로 도출한다. Recommended: Final Execution Freeze에서 이 도출 규칙을 확인(또는 셀 ID → 경로 표를 명시). Alternative: 경로 명명 규칙 고정. Impact: 규칙이 바뀌면 `PinnedCells`만 수정.
- **B-3 실행 상한(FC-D4.7) 의미 미정의.** Reason: SR-D6로 셀당 `max_games == Seed 수 == 동결 n`이 되어 셀 상한은 결정되지만, 행렬 전체 상한·INCONCLUSIVE 확장 규칙(N-1 후보)은 승인되지 않았다. Recommended: Freeze에서 "셀당 n이 곧 상한, 확장 없음" 또는 확장 규칙을 명시. Alternative: 별도 총 상한 슬롯 추가. Impact: 확장 규칙이 승인되면 새 Seed 블록·재평가 경로가 필요.
- **B-4 빌드 출처(Build provenance)·승인 출력 경로.** Reason: FC-D10/제안서 §12.1은 "실행 직전 해당 SHA에서 빌드하고 기록"을 요구하지만 증명 방식이 승인되지 않아 F-C2-A 정책이 bin/obj를 `NOT_READY`로 둔다. 또한 `build/` 아래 기존 출력(이번 공식 출력 포함)은 ignored-미승인으로 Dirty가 된다. Recommended: 빌드 출처 기록 형식과 허용 출력 경로를 승인. Alternative: 깨끗한 별도 체크아웃에서 실행하는 운영 절차. Impact: 승인 전에는 Freeze 값이 있어도 공식 실행 불가(현재 의도된 차단).
- **B-5 코호트 InputsHash 동결.** `neutral-v1`·`neutral-opp-v1`(행렬), `neutral-mixed-v1`·`virtual-league-v1`(calibration·integration). Final Execution Freeze 항목.
- **B-6 `fc3-cal`·`fc3-int` 공식 실행 경로.** Reason: 이번 TASK의 실행 CLI 범위는 48셀이었고, 두 계열의 공식 실행·저장 형식은 별도 결정이 필요하다(특히 `fc3-int` 코호트는 ADR-008이 이름 짓지 않아 `Official.family_cohorts_not_declared`). Recommended: 별도 후속 지시. Impact: 현재 `official`은 계속 Preflight 전용.
- **B-7 승인 Pin 집합의 결합.** 현재 어느 계열이든 승인 Pin 파일 하나를 읽지 못하면 48셀 Preflight도 `FAIL`이다(Fail-Closed). 확인 요청.
- **B-8 (기존 미결 유지)** 공식·dry Seed 수치 분리(SR-D4.4), F-C2-C 보고서 §J의 D-2(분산 0 비교 = NOT_EVALUATED)·D-3(`fc3-dom` 지표 실패 = FAIL) 확인.

## 10. 변경 파일 Manifest와 Git 상태

| 구분 | 파일 | SHA-256 (최종) |
|---|---|---|
| 신규(개발) | `tools/StarPlayer.SimHarness/DominanceOfficial.cs` | `8726c2762035c35d9f04e88082515adeb4c97eb389168bdd89f51e869169d17b` |
| 수정(개발) | `tools/StarPlayer.SimHarness/Official.cs` (검사 함수 추출·메시지 문구) | `6376998d7b50580ba4045e443344ad1005d3559c534d24de62ff311781bf31d2` |
| 수정(개발) | `tools/StarPlayer.SimHarness/Dominance.cs` (`Config`의 선택 인자 `mode`) | `6883fd7d7ed8e4995403d1524042eb58f1572a1a8668ff47c9588a9d6c88557a` |
| 수정(개발) | `tools/StarPlayer.SimHarness/Program.cs` (`official-dom`·`replay-dom`, `official`의 `fc3-dom` 안내) | `753d3909c14ed14e39c5d32a8c4d1a4fede9852fbf6269dc9ed792808d0c5beb` |
| 수정(개발) | `tools/StarPlayer.SimHarness/Cli.cs` (두 명령 화이트리스트) | `372b181b042c9f5d5626b2f52bbc84ca5b85ee4e515cced07403882949c5cdaa` |
| 신규(시험) | `tests/StarPlayer.SimHarness.Tests/DominanceOfficialTests.cs` | `51faa280c6092aeab8312abbee1cd9bbb24d071ea3a8b15d4f6110c11fe88da4` |
| 수정(시험) | `tests/StarPlayer.SimHarness.Tests/StrictCliTests.cs` (명령 목록 단언 확장) | `96454cf016ee13b7deb114ea6a2761cda4ab2ada8cb69d4f904189c4378c3351` |
| 신규(기록) | 이 보고서, `PHASE_3F_FC2_OFFICIAL_INTEGRATION_VERIFY_LOG.md`, `PHASE_3F_FC2_OFFICIAL_INTEGRATION_VERIFY_LOG_try1_FAIL.md`; `build/verify-archive/PHASE_3F_FC2_OFFICIAL_INTEGRATION_run1/`·`…_try1_FAIL/`(Git 무시) | — |
| 수정(상태) | `CURRENT_STATE.md`, `docs/game-engine.md` §39(신규 절), `Star/01_Phase_Reviews/Phase_03_Review.md`(부록), `Star/00_Dashboard.md`(한 줄) | — |

위 소스 SHA-256은 Verify run1 시점과 같다(run1 뒤 소스 변경 없음).

- Git: Branch `main`, HEAD `58692e4faadc9a58653680e7d05fe9103b5ceef8` 불변(origin/main보다 19 Commit 앞섬 — 이번 작업에서 변화 없음). Staging·Commit·Push·Reset·Clean·Rebase·Amend 없음.
- 보호 파일 SHA-256(시작·종료 동일, 쓰기 없음): `TASK.md` `46d115c88f6621db7838c4eb2d4ee40ffadafe3200c78b6eb597109d8a3b8da1`, `docs/2023recordRule.pdf` `9742caaf3a596b03d37fc6c57b9f866e989214e074aa5af1a2ab821ec5f212b0`, `docs/2026-official-baseball-rules.pdf` `f85860f3b754dfb5e07269e9d3a86c77c89b856376f6e3ba8345af36db997859`.
- 실제 저장소의 `build/simharness/official/`에는 아무것도 생성되지 않았다(시험은 임시 디렉터리만 사용).

## 11. 후속 작업 (자동 착수하지 않음)

1. 독립 리뷰(Codex 권장: 공식 실행·무결성·저장 경계).
2. §9 B-1~B-8 소유자 결정.
3. PZL 잔여 검사(#5·#6·#10·#12a)와 `fc2-dry` Dry Pilot(σ_g·DEFF) — 이번 단계 미착수.
4. Final Execution Freeze(Seed 파일·Pin·셀당 표본 수·실행 상한·코호트 Hash·Git SHA·빌드 출처).
5. 그 뒤에만 F-C3 공식 실행.

## 12. FIX1 Addendum — Codex 리뷰 OI-01(MAJOR)·OI-02(MINOR) 수정 (2026-10-18)

- 지시: 소유자 `TASK.md` "Phase 3-F F-C2 Official Integration FIX1". 기준 HEAD `58692e4` 불변, 기존 미커밋 변경 유지. 독립 재검토 `미실시`(두 건은 **해결 후보**).
- 범위: 출력 생성·쓰기 경로(OI-01)와 Replay 승인 계획 검사(OI-02)만. ConfigHash·game-result-v1·run-result-v1/v2·Attestation v1·Stop/Replay·Dominance 통계·Metrics 실패 우선순위·Legacy Fixture·DRAFT 색인(`official_evidence: false`)·정책 B-1~B-7 불변. 공식 루트·표본 수·스키마·빌드 출처를 확정하지 않았다.

### 12-1. OI-01 원인

`DominanceOfficial.Execute`가 `Directory.Exists` 확인 뒤 `Directory.CreateDirectory`·`File.WriteAllBytes`로 **경로 문자열**을 매번 다시 해석했다. 그래서 Preflight 이후(또는 실행 중) 상위 디렉터리를 Junction으로 바꾸면 결과가 루트 밖에 기록되고, 같은 Run ID 두 실행이 모두 존재 확인을 통과해 한 디렉터리에 섞여 쓸 수 있었다.

### 12-2. 수정 — OS 핸들로 고정한 출력(`PathSafety.cs`의 `PinnedDirectory`)

| 보장 | 방법 |
|---|---|
| 경로를 두 번 해석하지 않음 | 볼륨 루트(`\??\X:\`)부터 각 구성 요소를 **부모 핸들 기준 상대 경로**로 연다(`NtCreateFile`, `OBJECT_ATTRIBUTES.RootDirectory`). 이후 디렉터리·파일 생성도 모두 부모 핸들 기준이다 |
| 링크를 따라가지 않음 | 모든 열기에 `FILE_OPEN_REPARSE_POINT` — Junction/Symlink/Mount point는 그 자체로 열려 거부된다(`Official.output_reparse_point`) |
| 경로 고정(교체·이동 방지) | 체인의 모든 디렉터리 핸들을 실행 끝까지 **`FILE_SHARE_DELETE` 없이** 보유 → 그동안 누구도 그 디렉터리를 삭제 권한으로 열 수 없어 이름 변경·이동·삭제·링크 교체가 불가능. (자식이 열려 있는 디렉터리는 NTFS가 이름 변경을 별도로 거부한다.) 이미 삭제 권한으로 열려 있으면 고정 자체가 실패(Fail-Closed) |
| 신규·배타 생성 | 실행 디렉터리, `cells`, 셀 디렉터리, 모든 파일을 `FILE_CREATE`로 생성 — 존재 확인과 생성이 하나의 원자 연산. 기존 이름은 재사용·덮어쓰기 안 함(`Official.output_exists`). 같은 Run ID 두 실행 중 정확히 하나만 디렉터리를 얻는다 |
| 쓰기 전 확인 | 열거나 만든 객체마다 그 **핸들로** 속성(reparse 아님·종류 일치)과 최종 경로(`GetFinalPathNameByHandleW`, 고정된 부모 최종 경로의 직접 하위)를 확인한 뒤에만 쓴다. 볼륨 루트는 실제 경로가 `X:\`여야 한다(SUBST 등 거부). 데이터는 생성 핸들로만 쓴다(`FileStream(SafeFileHandle)`, `Flush(true)`) |
| Fail-Closed | 확인할 수 없는 모든 경우(비 Windows, 드라이브 문자 아닌 루트, 8.3/별칭으로 실제 경로가 요청과 다름, NTSTATUS 실패)는 `Official.output_unpinnable`로 거부 |

`Execute`: 출력 생성 실패 → `ABORTED_INTEGRITY`, 셀 0, 아무것도 쓰지 않음. 생성 후 셀·색인 쓰기 실패 → 중단, 결과는 `complete=false`·Gate `NOT_EVALUATED`(색인 쓰기도 실패하면 반환 결과가 `ABORTED_INTEGRITY`). 핸들은 `finally`에서 해제. Preflight의 `OfficialOutput.Resolve`는 조기 거부용으로 그대로 두었다(권위 있는 보장은 실행 시점의 핸들 고정). **[Closeout 정정 NOTE-2 → §13-3: 모든 쓰기·Flush I/O 예외가 typed `ABORTED_INTEGRITY`가 되는 것은 아니다.]**

### 12-3. 공격 재현과 실제 결과 (Windows 11, NTFS, 테스트 전용 임시 경로)

| 시험 | 수정 전(RED) | 수정 후 |
|---|---|---|
| Preflight 이후 `build`를 루트 밖 Junction으로 교체 | **결과 파일이 루트 밖에 생성** | `Official.output_reparse_point`, 셀 0, 외부 0개 |
| Preflight 이후 `build/simharness/official`을 Junction으로 교체 | **결과 파일이 루트 밖에 생성** | 같음 |
| 실행 중(쓰기 직전) `build`·`official`·실행 디렉터리 이동 후 Junction 설치, 셀 1개 뒤 열린 자식 없는 `cells`를 루트 밖으로 이동 | **이동 성공**(접근 거부 외 다수 통과) | 4회 모두 차단, 외부 0개, 증거는 Preflight 경로에 그대로 |
| 보유 중인 **빈** 실행·`cells` 디렉터리를 Mount point로 변환(`FSCTL_SET_REPARSE_POINT`, 쓰기 속성 권한만 사용 — 공유 모드로 막을 수 없음) | (수정 전 경로에는 해당 고정 없음) | **변환 자체는 성공함을 확인.** 이후 상대 생성이 `STATUS_REPARSE_POINT_ENCOUNTERED`(0xC0000280) **[Closeout 정정 NOTE-1 → §13-3: 0xC0000280의 이름은 `STATUS_REPARSE_POINT_NOT_RESOLVED`]**로 실패 → `Official.output_reparse_point`, 외부 0개, 실행은 불완전 기록으로 중단(라이브러리·`Execute` 양쪽 시험) |
| 같은 Run ID 동시 실행(Barrier로 동시 시작) | **두 실행 모두 `output_exists` 없이 같은 디렉터리에 기록**(이번 RED 실행에서 실제 재현) | 정확히 하나가 `Official.output_exists`·셀 0, 색인·셀 디렉터리는 승자 것만 |
| Preflight 이후 같은 경로에 기존(부분) 결과 존재 | 거부(존재 확인) | `Official.output_exists`, 기존 파일 내용 불변, 다른 파일 생성 없음 |
| 출력 체인을 만들 수 없음(`build`가 파일) | **IOException으로 예외 종료** | `Official.output_unpinnable`, Exit 2, 셀 0, 기존 파일 불변 |
| 정상 안전 경로 저장·읽기·Replay | — | 기존 48셀 시험 전부 통과(저장·기존 Replay 검사·Replay MATCH) |

RED 원시 출력: `docs/reports/PHASE_3F_FC2_OFFICIAL_INTEGRATION_FIX1_RED.log`(Git 무시 `*.log`, SHA-256 `4feac565676068319a66a60bb7d474c138c02abf76485c7bbc7dff060246349c`; 22개 중 신규 8개 실패, 기존 14개 통과).

### 12-4. OI-02 — 공통 승인 계획 검사

- 원인: Replay는 색인의 `games_per_cell`이 승인값과 같은지만 보고, 승인된 48개 Seed 파일 각각의 경기 수를 다시 확인하지 않았다. 수정 전 RED에서 승인값 3·실제 계획 2경기에 대해 **일관되게 위조한 색인이 `REPLAY MATCH`**가 되었다.
- 수정: Preflight의 `games_per_cell` 규칙을 `DominancePreflight.PlanSizeCheck(approvals, pinScan)` 하나로 추출해 **Preflight와 Replay가 같은 함수**를 호출한다(복제 없음). Replay는 결과 파일을 읽기 전에 `PinnedCells`(외부 Pin·48셀·고유성·셀 결속) → `PlanSizeCheck`(동결·≥2·48개 파일 모두 동결값과 같은 경기 수)를 적용하고, 하나라도 어긋나면 **재실행 전에** 거부한다. 거부 코드는 Preflight의 해당 검사 코드와 같다.
- 시험(재실행 호출 수를 `Verify`의 관찰용 seam으로 계측): 승인 3·실제 2(일관 위조 색인 포함) → `Official.dominance_plan_size_mismatch`, 재실행 0; 48셀 중 1개만 1경기 → 같은 코드, 재실행 0; 표본 미동결·1·3·셀 누락·Pin 불일치·Pin 없음 → Preflight와 같은 코드, 재실행 0; 정상 → MATCH, 셀 재실행 정확히 48회.

### 12-5. 표적 Sabotage (원복 후 SHA-256 동일 확인)

| 변형 | 결과 |
|---|---|
| `FILE_OPEN_REPARSE_POINT` 제거(링크 추종) | 검출(Junction 시험 3개) |
| 체인 핸들에 `FILE_SHARE_DELETE` 허용 | 처음엔 생존 — 시험한 디렉터리가 모두 열린 자식을 가져 NTFS가 따로 이동을 막았기 때문. 열린 자식 없는 `cells` 이동 공격을 시험에 추가한 뒤 **검출** |
| 실행 디렉터리 `FILE_CREATE` → `FILE_OPEN_IF` | 검출(배타 생성·동시 실행·부분 결과) |
| 자식 핸들의 속성 확인 제거 | 생존 — **동등 변형**: 커널이 같은 조건을 먼저 강제한다(링크는 `FILE_OPEN_REPARSE_POINT`로 그 자체로 열린 뒤 그 아래 상대 생성이 0xC0000280으로 실패, 종류 불일치는 `STATUS_NOT_A_DIRECTORY`). 이중 방어로 유지 |
| Replay가 `PlanSizeCheck` 결과 무시 | 검출(OI-02 시험 4개) |

### 12-6. 빌드·전체 시험·Verify

| 명령 | 결과 |
|---|---|
| `dotnet test tests/StarPlayer.SimHarness.Tests -c Release` | 343 PASS / 0 FAIL / 0 SKIP (330 + 신규 13) |
| `verify.ps1 -LogName PHASE_3F_FC2_OFFICIAL_INTEGRATION_FIX1_VERIFY_LOG.md` run1 | `GATE: PASS`, 2,771 PASS. 이후 Sabotage로 `cells` 이동 시험을 추가해 최종 트리와 달라졌으므로 증거로만 보존: [run1 로그](PHASE_3F_FC2_OFFICIAL_INTEGRATION_FIX1_VERIFY_LOG_run1.md) SHA-256 `dcc9897f6ef6d40f844ad49e5c0d2337621dc6161a155459e3f4c902deb37f62`, 원시 `build/verify-archive/PHASE_3F_FC2_OFFICIAL_INTEGRATION_FIX1_run1/`(`verify-result.json` `b381feaf0c118530d4e267fc70f5f54f0936c3f2012ca8b4a750899c5d31dd32`) |
| 같은 명령 **run2(최종 트리)** | **`GATE: PASS`, exit 0** — Release 빌드 11개 경고 0; Domain 327 · Simulation 741 · Application 127 · Infrastructure 1,233 · SimHarness 343 = **2,771 PASS / 0 FAIL / 0 SKIP**; Godot·Export·문서 링크(157개 파일) PASS. [로그](PHASE_3F_FC2_OFFICIAL_INTEGRATION_FIX1_VERIFY_LOG.md) SHA-256 `c0255c2249de7f24631461f90a76aa2deb02a0051c15108fe690b559730697d9`, 원시 `build/verify-archive/PHASE_3F_FC2_OFFICIAL_INTEGRATION_FIX1_run2/`(28개 파일, `verify-result.json` `93aefd3819b4dce18f96a19d07ce5bcb6cb646cc12d042c14f4aab81781c79da`) |

- 기존 증거 보존: 실행 전 `build/verify` = `PHASE_3F_FC2_OFFICIAL_INTEGRATION_run1` 확인. Official Integration try1 FAIL·run1 PASS와 F-C2-A/B/C 등 기존 로그·Archive **335개 파일의 SHA-256이 전후 동일**.
- 테스트 잔여 리소스: 임시 루트·Junction·Mount point는 시험 안에서 링크만 제거(대상 미추적)한 뒤 정리, 실행 후 `%TEMP%\simharness-tests`에 잔여 0. **[Closeout 정정 NOTE-3 → §13-3: 이번 FIX1 시험이 만든 자원에 한정한 결과이며 TEMP 전체의 잔여 0 주장이 아니다.]** 실제 저장소의 `build/simharness/official/` 생성 0.
- 이 Addendum과 상태 문서는 run2 뒤에 작성했다(새 링크는 기존 파일만 가리킴).
- 미실시: 독립 재검토, 전체 Mutation, 실제 공식 실행·실제 승인 입력 Replay(금지·자료 없음).

### 12-7. 변경 파일 (FIX1 분, 최종 SHA-256)

| 구분 | 파일 | SHA-256 |
|---|---|---|
| 수정 | `tools/StarPlayer.SimHarness/PathSafety.cs` (`PinnedDirectory` 추가, 기존 `FirstReparsePoint` 동작 불변) | `86b74126df9a813078c218732ee53ddcfc49afc8ce2dceeaebabd6f6646df4c7` |
| 수정 | `tools/StarPlayer.SimHarness/DominanceOfficial.cs` (핸들 고정 쓰기, `PlanSizeCheck` 공유, Replay 관찰 seam) | `111bce7cb21c343903b6cf1582f11a71e989d69cbf4fdfa53d6242e06b3eedbd` |
| 수정 | `tests/StarPlayer.SimHarness.Tests/DominanceOfficialTests.cs` (`partial`, 고유 임시 루트, 정리 재시도, 실패 메시지) | `f01697573034f8ca1cfa2cd7ac2b5570704952a028e9b98d492db52d3b8aaae3` |
| 신규 | `tests/StarPlayer.SimHarness.Tests/DominanceOfficialTests.Fix1.cs` (OI-01 7개 사례·OI-02 4개) | `fe4d8c5613d05a46cd41002f3391c5facc094e9e55b4e9f5322d9be46f5a0b48` |
| 신규 | `tests/StarPlayer.SimHarness.Tests/PinnedDirectoryTests.cs` (Mount point 변환·배타 생성·체인 링크) | `715298dcfda2440edbcf5b6844d188e9f849046b7f63215ae1fdb022196b16f4` |
| 신규(기록) | `PHASE_3F_FC2_OFFICIAL_INTEGRATION_FIX1_VERIFY_LOG.md`(run2), `…_FIX1_VERIFY_LOG_run1.md`, `…_FIX1_RED.log`(Git 무시), Archive `…_FIX1_run1/`·`…_FIX1_run2/`(Git 무시) | — |
| 수정(상태) | 이 보고서 §12, `CURRENT_STATE.md`, `docs/game-engine.md` §39, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/00_Dashboard.md` | — |

Git: HEAD `58692e4faadc9a58653680e7d05fe9103b5ceef8` 불변, Staging·Commit·Push·Reset·Clean·Rebase·Amend 없음. 보호 파일(이번 작업 시작·종료 동일): `TASK.md` `f149c46bf7f98591e678e14f74e71faf1a176b76fbf5cd89c5ff1dba00453979`, `docs/2023recordRule.pdf` `9742caaf…12b0`, `docs/2026-official-baseball-rules.pdf` `f85860f3…7859`.

### 12-8. 남은 기술적 제약 (숨기지 않음)

1. **빈 보유 디렉터리의 Mount point 변환**은 공유 모드로 막을 수 없다(쓰기 속성 권한만 필요). 실측 결과 외부 기록은 일어나지 않고 실행이 Fail-Closed로 중단된다 — 탈출이 아니라 서비스 거부만 가능.
2. **같은 사용자 권한의 다른 프로세스**는 우리가 닫은 뒤의 결과 파일을 고치거나 실행 디렉터리에 파일을 추가할 수 있다(ACL 범위). 우리 파일을 미리 만들거나 덮어쓸 수는 없다(`FILE_CREATE`). 내용 무결성은 파일 시스템이 아니라 Replay(파일 SHA·Attestation·재실행·재평가)가 검증한다.
3. **지원 범위**: Windows 드라이브 문자 루트만. 비 Windows·UNC·네트워크 경로, 상위에 정당한 Junction/Mount point가 있는 저장소, 8.3/별칭 경로는 거부(오거부 가능, 보안상 안전 쪽).
4. **다른 프로세스가 상위 디렉터리를 삭제 권한으로 열고 있으면**(백신 등) 고정이 실패해 실행이 거부된다(일시적 오거부 가능).
5. **읽기 경로**(Seed 파일·Replay 입력)는 기존 "검사 후 읽기"를 유지한다. 읽은 바이트의 SHA-256을 외부 Pin·기록값과 대조하므로 경로가 바뀌어도 다른 내용이 받아들여지지 않는다.
6. 승인 출력 루트·빌드 출처(B-4)는 여전히 미승인이며, 실제 저장소의 bin/obj·`build/` 출력 때문에 `git_integrity`가 통과하지 않는다. `official-dom`은 현재 승인값으로 계속 Exit 2 거부.
7. 정책 결정 항목 B-1~B-7 변화 없음.

### 12-9. 상태

OI-01: 해결 후보 · OI-02: 해결 후보 · F-C2 Official Integration: 독립 재검토 대기 · Official Execution `NOT_READY` · Final Execution Freeze `PENDING` · Phase 3-F / Phase 3 Gate `INCOMPLETE`.

## 13. Closeout 준비 Addendum — Codex High 집중 재검토 반영 (2026-10-18)

- 지시: 소유자 `TASK.md` "Phase 3-F F-C2 Official Integration Closeout Preparation". **문서·Manifest 정리만 수행했다.** 코드·시험 변경, 빌드·시험·`verify.ps1` 재실행, Stage/Commit/Push/Reset/Clean/Rebase/Amend, Official Execution, Final Execution Freeze, PZL·Dry Pilot 착수는 없다.
- §1~§12는 구현·FIX1 당시 기록이므로 삭제·소급 변경하지 않았다. 정정은 아래 §13-3과 해당 위치의 짧은 표식(NOTE-1/2/3)으로만 연결한다. §7-3·§12의 "독립 리뷰 `미실시`"와 §12-9의 "해결 후보"는 작성 시점 상태이며, 현재 상태는 §13-7이다.

### 13-1. 독립 재검토 결과 (소유자 보고 기준)

| 항목 | 결과 |
|---|---|
| 재검토 | Codex High 집중 재검토. **READ-ONLY** — Codex는 빌드·시험을 직접 재실행하지 않았다(코드·문서·해시·링크 정독 기반). |
| OI-01 (MAJOR) | **해결** — Windows 핸들 기반 경로 고정·배타적 생성·직접 쓰기(§12-2). |
| OI-02 (MINOR) | **해결** — Preflight/Replay 공통 `PlanSizeCheck`(§12-4). |
| 신규 지적 | BLOCKER 0 · MAJOR 0 · MINOR 0. 비차단 NOTE 3건(§13-3). |
| 문서 링크 | 기존 Codex 링크 검토: 354개 상대 경로, 미해결 0건. 이번 Addendum이 추가한 링크는 모두 기존 파일을 가리키며 읽기 전용으로 확인했다. |

- 구분: **2,771 PASS는 Claude Code가 FIX1 run2에서 실행한 결과**이며 Codex의 재실행 결과가 아니다. Codex 재검토는 그 결과와 코드의 일치를 읽기 전용으로 검토한 것이다.
- GPT Chat/소유자의 최종 Closeout 검토는 아직 기록되지 않았다(`승인 완료` 아님).

### 13-2. 최종 검증 증거 — 변경 없음, 재실행 없음

최종 검증의 기준은 **FIX1 run2(최종 트리)** 이다. 아래 기록은 서로 대체하지 않고 각각 보존한다.

| 기록 | 결과 | 용도 | 로그 SHA-256 |
|---|---|---|---|
| Integration **try1** | `GATE: FAIL` (whitelist 시험 미갱신) | 실패 증거 | `fdf6347eeac0123b923c038cc3bc0d6b3824c014a7a8d498cc84409dfdd6c1f8` |
| Integration **run1** | `GATE: PASS` 2,758 PASS | 최초 통합 구현 증거 | `1ae51610184dd7b631e5f9c54ef772c51eb2da4bb4797c884e41ca2475e10906` |
| FIX1 **run1** | `GATE: PASS` 2,771 PASS | 증거로만 보존 — 이후 시험이 추가되어 **최종 트리 검증 근거로 쓰지 않는다** | `dcc9897f6ef6d40f844ad49e5c0d2337621dc6161a155459e3f4c902deb37f62` |
| FIX1 **run2 (최종)** | **`GATE: PASS`, 2,771 PASS / 0 FAIL / 0 SKIP** | **최종 트리 검증 근거** | `c0255c2249de7f24631461f90a76aa2deb02a0051c15108fe690b559730697d9` |

- 최종 로그: [`PHASE_3F_FC2_OFFICIAL_INTEGRATION_FIX1_VERIFY_LOG.md`](PHASE_3F_FC2_OFFICIAL_INTEGRATION_FIX1_VERIFY_LOG.md). Result JSON(`verify-result.json`) SHA-256 `93aefd3819b4dce18f96a19d07ce5bcb6cb646cc12d042c14f4aab81781c79da`. 원시 Archive `build/verify-archive/PHASE_3F_FC2_OFFICIAL_INTEGRATION_FIX1_run2/`(Git 무시 로컬 증거, Commit 제외).
- 이번 작업에서 읽기 전용으로 확인: 위 로그 4개와 run2 `verify-result.json`의 SHA-256 재계산 값이 기록과 일치, `build/verify`가 run2 Archive와 동일(`diff -rq`), Archive 디렉터리 11개(F-C2-A/B/C·Integration·FIX1 포함) 존재, 기존 추적 로그에 수정 표시 없음. 로그·Archive에는 쓰기를 하지 않았다.
- 결과(2,771): Domain 327 · Simulation 741 · Application 127 · Infrastructure 1,233 · SimHarness 343; Release 빌드 11개 경고 0; Godot·Export·문서 링크 PASS.

### 13-3. 비차단 NOTE 3건 (코드·시험·동작 변경 없음)

**NOTE-1 — NTSTATUS 명칭.** `PathSafety.cs`의 `0xC0000280`은 `STATUS_REPARSE_POINT_NOT_RESOLVED`다. `STATUS_REPARSE_POINT_ENCOUNTERED`는 다른 상태 코드 `0xC000050B`다. §12-3 표와 `PathSafety.cs`·`PinnedDirectoryTests.cs` 주석/상수 이름(`StatusReparsePointEncountered`)의 "ENCOUNTERED" 표기는 이름 오표기다. **동작은 정확하다:** 코드는 수치 `0xC0000280`만 비교하며, 시험이 실측한 Mount point 시나리오에서 실제로 이 값이 관찰되어 `Official.output_reparse_point`로 거부된다. 다른 NTSTATUS(`0xC000050B` 포함)는 이 분기에 걸리지 않고 일반 거부 `Official.output_unpinnable`로 떨어지므로 어느 쪽이든 Fail-Closed다. 이번 단계에서 상수 이름·주석·메시지는 바꾸지 않았다. **후속 비기능적 코드 정리 항목**(상수/주석 명칭 정정)으로 기록한다.

**NOTE-2 — 일반 I/O 예외.** 쓰기·Flush에서 발생하는 모든 I/O 예외가 typed `ABORTED_INTEGRITY` 결과로 반환된다고 서술하지 않는다. 현재 코드 정독으로 확인한 범위는 다음 두 가지이며 서로 다르다.
1. **Native 경로 오류의 명시적 처리:** `PinnedDirectory`의 핸들 기준 열기·생성이 NTSTATUS 실패(이미 존재 → `Official.output_exists`, 링크/Mount point → `Official.output_reparse_point`, 그 밖 → `Official.output_unpinnable`)와 `CheckChild`의 속성·최종 경로 불일치를 typed `Result` 오류로 돌려주고, `Execute`는 이를 `ABORTED_INTEGRITY`로 바꾼다.
2. **일반 I/O 예외의 상위 CLI 거부:** 핸들을 연 뒤 `WriteNewFile`의 `stream.Write`/`Flush(true)`(디스크 가득 참, 장치 오류, 백신 간섭 등)가 던지는 `IOException`은 `WriteNewFile`·`Execute`에서 잡히지 않는다. `Execute`를 호출한 CLI의 상위 처리(`Program`의 예외 처리)가 입력 처리 불가로 Exit 2 거부한다. 이 경우 typed `ABORTED_INTEGRITY` 결과나 **완전한 중단 색인(`complete=false` 색인)이 항상 생성된다고 보장하지 않으며**, 이미 기록된 셀 산출물만 남을 수 있다. 어떤 경우에도 Gate PASS로 이어지지 않는다(색인이 없으면 Replay 대상이 아님).

이번에 이 일반 I/O 실패 경로를 시험으로 재현하지 않았다(`미검증`). 동작 변경 여부는 소유자 결정 사항이며 이번 Closeout에서 변경하지 않았다. 따라서 §12-2 마지막 단락의 "생성 후 셀·색인 쓰기 실패 → 중단" 서술은 위 1번(Native 경로 오류)에 한해 정확하다.

**NOTE-3 — TEMP 잔여물 범위.** §12-6의 "실행 후 `%TEMP%\simharness-tests`에 잔여 0"은 **이번 FIX1 시험이 만든 임시 루트·Junction·Mount point가 시험 안에서 정리되었다**는 결과이며, TEMP 전체 또는 `%TEMP%\simharness-tests` 전체에 다른 시점의 자료가 없다는 주장이 아니다. Codex가 이전 날짜의 잔여 디렉터리 1개를 관찰했다. 이번 작업의 읽기 전용 목록 확인에서도 `%TEMP%\simharness-tests\`에 2026-10-17 12:16 수정 시각의 `red-<32자리 16진수>` 디렉터리 1개가 있었다(생성 출처 미확인). **이 디렉터리를 삭제·변경하지 않았다.** 정리 여부는 소유자 결정이다.

### 13-4. 로컬 Closeout Commit 후보 Manifest — 19개 (수정 10 + 신규 9)

`git status --short -uall`과 파일 단위로 대조했다: 상태 항목 22개 = 후보 19개 + 보호 미추적 3개, Staging 0. 소스·시험의 SHA-256은 §10·§12-7 기록과 모두 일치한다.

| # | 구분 | 파일 | SHA-256 |
|---|---|---|---|
| 1 | 수정 | `CURRENT_STATE.md` | (상태 문서 — 이 Closeout 편집 포함, 자기 참조라 미기록) |
| 2 | 수정 | `Star/00_Dashboard.md` | (상동) |
| 3 | 수정 | `Star/01_Phase_Reviews/Phase_03_Review.md` | (상동) |
| 4 | 수정 | `docs/game-engine.md` | `23f769939dd7540be4107a9b25a8f8f7bc17e87f34417d4fa405bd7abe2ef696` |
| 5 | 수정 | `tests/StarPlayer.SimHarness.Tests/StrictCliTests.cs` | `96454cf016ee13b7deb114ea6a2761cda4ab2ada8cb69d4f904189c4378c3351` |
| 6 | 수정 | `tools/StarPlayer.SimHarness/Cli.cs` | `372b181b042c9f5d5626b2f52bbc84ca5b85ee4e515cced07403882949c5cdaa` |
| 7 | 수정 | `tools/StarPlayer.SimHarness/Dominance.cs` | `6883fd7d7ed8e4995403d1524042eb58f1572a1a8668ff47c9588a9d6c88557a` |
| 8 | 수정 | `tools/StarPlayer.SimHarness/Official.cs` | `6376998d7b50580ba4045e443344ad1005d3559c534d24de62ff311781bf31d2` |
| 9 | 수정 | `tools/StarPlayer.SimHarness/PathSafety.cs` | `86b74126df9a813078c218732ee53ddcfc49afc8ce2dceeaebabd6f6646df4c7` |
| 10 | 수정 | `tools/StarPlayer.SimHarness/Program.cs` | `753d3909c14ed14e39c5d32a8c4d1a4fede9852fbf6269dc9ed792808d0c5beb` |
| 11 | 신규 | `docs/reports/PHASE_3F_FC2_OFFICIAL_INTEGRATION_REPORT.md` | (이 보고서 — 자기 참조라 미기록) |
| 12 | 신규 | `docs/reports/PHASE_3F_FC2_OFFICIAL_INTEGRATION_VERIFY_LOG.md` | `1ae51610184dd7b631e5f9c54ef772c51eb2da4bb4797c884e41ca2475e10906` |
| 13 | 신규 | `docs/reports/PHASE_3F_FC2_OFFICIAL_INTEGRATION_VERIFY_LOG_try1_FAIL.md` | `fdf6347eeac0123b923c038cc3bc0d6b3824c014a7a8d498cc84409dfdd6c1f8` |
| 14 | 신규 | `docs/reports/PHASE_3F_FC2_OFFICIAL_INTEGRATION_FIX1_VERIFY_LOG.md` | `c0255c2249de7f24631461f90a76aa2deb02a0051c15108fe690b559730697d9` |
| 15 | 신규 | `docs/reports/PHASE_3F_FC2_OFFICIAL_INTEGRATION_FIX1_VERIFY_LOG_run1.md` | `dcc9897f6ef6d40f844ad49e5c0d2337621dc6161a155459e3f4c902deb37f62` |
| 16 | 신규 | `tests/StarPlayer.SimHarness.Tests/DominanceOfficialTests.cs` | `f01697573034f8ca1cfa2cd7ac2b5570704952a028e9b98d492db52d3b8aaae3` |
| 17 | 신규 | `tests/StarPlayer.SimHarness.Tests/DominanceOfficialTests.Fix1.cs` | `fe4d8c5613d05a46cd41002f3391c5facc094e9e55b4e9f5322d9be46f5a0b48` |
| 18 | 신규 | `tests/StarPlayer.SimHarness.Tests/PinnedDirectoryTests.cs` | `715298dcfda2440edbcf5b6844d188e9f849046b7f63215ae1fdb022196b16f4` |
| 19 | 신규 | `tools/StarPlayer.SimHarness/DominanceOfficial.cs` | `111bce7cb21c343903b6cf1582f11a71e989d69cbf4fdfa53d6242e06b3eedbd` |

### 13-5. 보호 파일과 Commit 제외 대상

| 파일 | SHA-256 (이번 작업 시작 시점 = 종료 시점) |
|---|---|
| `TASK.md` | `8e1b16b1f4e80a03778e32d2ae5c5c368cf4948dcd9d389df77890637909a0b3` (소유자가 이번 지시로 교체한 현재 파일. §10·§12-7에 적힌 이전 TASK.md 해시와 다른 것은 정상) |
| `docs/2023recordRule.pdf` | `9742caaf3a596b03d37fc6c57b9f866e989214e074aa5af1a2ab821ec5f212b0` |
| `docs/2026-official-baseball-rules.pdf` | `f85860f3b754dfb5e07269e9d3a86c77c89b856376f6e3ba8345af36db997859` |

- 위 3개와 `build/verify-archive/`·`build/verify/`·`…_FIX1_RED.log`(Git 무시 로컬 증거)는 Commit 후보가 아니다.
- Git: Branch `main`, HEAD `58692e4faadc9a58653680e7d05fe9103b5ceef8` 불변, Staging 0, 로컬 Closeout Commit **미실시**.

### 13-6. 미완료 정책과 F-C2 잔여 책임 (임의 확정하지 않음)

- **B-1** 공식 결과 스키마와 전체 Hash 채택 · **B-2** 셀–Seed 파일 승인 결속 표현 · **B-3** 실행 상한과 INCONCLUSIVE 확장 정책 · **B-4** 빌드 출처와 승인 출력 경로 · **B-5** 코호트 InputsHash 동결 · **B-6** `fc3-cal`/`fc3-int` 공식 실행 경로 연결 · **B-7** Family 간 Pin 집합의 결합 범위 — 모두 §9 그대로 미결. (B-8 기존 미결도 유지.)
- **B-6은 F-C2 전체 완료 전에 해결해야 하는 잔여 구현이며 자동으로 F-C3에 넘기지 않는다.** 이번 통합은 48셀(`fc3-dom`)만 연결했다.
- 미착수: PZL #5·#6·#10·#12a, `fc2-dry` Dry Pilot, Final Execution Freeze, 실제 Official Execution.
- 후속 비기능/소유자 결정 후보: NOTE-1 상수·주석 명칭 정정, NOTE-2 일반 I/O 예외 처리 방침, NOTE-3 TEMP 잔여 디렉터리 정리 여부.

### 13-7. 상태

**`READY_FOR_OWNER_CLOSEOUT_REVIEW`** — OI-01 해결 · OI-02 해결 · 신규 BLOCKER/MAJOR/MINOR 0 · 로컬 Closeout Commit 미실시 · 공식 결과 색인은 DRAFT이며 `official_evidence: false` · Official Execution `NOT_READY` · Final Execution Freeze `PENDING` · F-C2 전체(B-6 포함)·Phase 3-F / Phase 3 Gate `INCOMPLETE`.
