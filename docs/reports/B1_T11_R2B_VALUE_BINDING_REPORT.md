# B1 — T11 R2-B 마지막 MINOR 수정 보고서 (Preflight 결정적 값 결속, Codex READ-ONLY 표적 재검수 제출용)

- 작성: Claude Code, 2026-10-25
- 근거: [TASK](../tasks/B1_T11_R2B_VALUE_BINDING_TASK.md), OWNER 채팅 지시 "B1 T11 R2-B 마지막 MINOR 수정"
- 직전 라운드 [B1_T11_R2_FINAL_FIX_REPORT.md](B1_T11_R2_FINAL_FIX_REPORT.md)는 소급해 고치지 않았습니다. R1·R2-A·R3·R4는 재설계하지 않았습니다.
- 상태: **구현·자체 시험 완료, Codex READ-ONLY 표적 재검수 대기.**
  - B1 완료를 선언하지 않습니다.
  - Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
  - Commit·Push 없음
- Codex 최신 Finding 원문은 저장소에 없습니다. OWNER 지시의 기술을 따랐습니다.
  - Finding: `SameMembers`가 이름·순서만 보고 값을 보지 않아 `games_per_cell`·`schema_status`·`official_evidence`·`complete`·`family_gate`·`dominance_gate` 등의 변조를 거부하지 못함

## 1. 수정 코드와 함수·행 번호

| 파일 | 위치 | 내용 |
|---|---|---|
| `tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs` | `PreflightRecords` `:45` | Preflight 비교 클래스. `SameMembers`·부분 필드 비교(고정 목록·평가 대상 검사만)는 삭제 |
| 〃 | `MachineDependentCheck` `:48` | 값 비교에서 빼는 유일한 검사 `output_directory` |
| 〃 | `Verify` `:50` | 재실행 → READY 기대 Report → 기존 형식 직렬화 → 전체 비교. 검증기 Attestation = 보존 Manifest Attestation 확인 `:87` |
| 〃 | `Ready` `:94` | 재실행 검사 중 "검증기 승인 인스턴스가 ExecutionPathReady=false라서 NOT_READY인 검사"만 READY로 대체. 그 밖의 검사가 PASS가 아니면 거부 |
| 〃 | `ReadyUnit` `:103` | Unit 기대 Report. Attestation은 `OfficialPreflight.BuildAttestation`(AMPV와 같은 경로)으로 승인 입력에서 만듦 |
| 〃 | `Compare` `:121`, `FirstDifference` `:143` | 전체 JSON 트리 비교: 객체 멤버 이름·순서, 배열 길이·순서, 원시값 타입·값 |
| `tools/StarPlayer.SimHarness/Official.cs` | `OfficialPreflight.ExecutionPathReadyMessage` `:309` | READY 메시지 단일 정의(바이트 동일, 기존 리터럴 이동) |
| `tools/StarPlayer.SimHarness/FamilyOfficial.cs` | `FamilyPreflight.AllUnitsReady` `:54` | 〃 |
| `tools/StarPlayer.SimHarness/DominanceOfficial.cs` | `DominancePreflight.AllCellsReady` `:40` | 〃 |
| `tests/StarPlayer.EvidenceVerifier.Tests/T11R2BValueTests.cs` | 신규 `:98`·`:127` | RED/GREEN 반례 |

## 2. 결정적 필드 전체와 비교 정책

**기대값의 출처:**

- 검증기 자신이 같은 승인값으로 다시 실행한 Family/Dominance Preflight를 씁니다. Git은 승인 SHA의 Clean 평가이며, 새 출력 이름을 쓰고, 경기와 쓰기는 없습니다.
- 기대값을 만드는 데 제출 기록의 값은 하나도 쓰지 않습니다.
- READY로 대체하는 것은 다음 검사뿐입니다. 메시지는 SimHarness의 단일 정의입니다.
  - `execution_path` → (PASS, null, `ExecutionPathReadyMessage`)
  - Unit `attestation` → (PASS, null, 검증기가 만든 Attestation Hash)
  - `unit_preflight` → `AllUnitsReady(family, n)`
  - `cell_preflight` → `AllCellsReady`

**비교 정책:**

- 이름·순서: 객체마다 멤버 이름 순서가 같아야 하므로 누락·추가·중복·재정렬이 모두 거부됩니다.
- 타입: ValueKind(string·number·true/false·null·object·array)가 같아야 합니다.
- 값:
  - 문자열은 디코딩한 값이 같아야 합니다.
  - 숫자는 원문 토큰이 같아야 합니다(`2` ≠ `2.0` ≠ `"2"`).
  - 중첩 객체·배열은 재귀로 비교합니다.

| 기록 | 결정적 필드(모두 값 비교) |
|---|---|
| Family Preflight (`FamilyOfficial.PreflightDocument`) | `schema_version`, `schema_status`, `official_evidence`, `official_evidence_note`, `run_id`, `run_type`, `family`, `run_status`, `complete`, `preflight.ready`, `preflight.error_code`, `preflight.checks[]`(Family 검사 9개: cli·git_integrity·git_sha·model_tuning·output_directory·seed_manifests·unit_preflight·execution_policy·execution_path, 각 `id`·`status`·`code`·`message`), `preflight.units[]`(`unit_id`·`seed_manifest_path`·`ready`·`error_code`), `family_gate`, `family_gate_reason` |
| Dominance Preflight (`DominanceOfficial.PreflightDocument`) | 위와 같고(검사 10개: games_per_cell 추가, unit_preflight 대신 cell_preflight) `preflight.games_per_cell`, `preflight.cells[]`(`cell_id`·`seed_manifest_path`·`ready`·`error_code`), `dominance_gate`, `dominance_gate_reason` |
| Unit Preflight (`OfficialOutput.Result`) | `schema_version`, `run_type`, `official`, `git_commit_sha`, `execution_policy_id`, `execution_policy_hash`, `cohort_id`, `cohort_hash`, `seed_manifest_hash`, `model_version`, `tuning_version`, `tuning_hash`, `rule_snapshot_hash`, `config_hash`, `attestation_version`, `attestation_hash`, `run_status`(=`NOT_STARTED`, 의미 무변경), `gate_status`, `error_code`(=`Official.execution_not_implemented`, 의미 무변경), `checks[]`(15개 각 `id`·`status`·`code`·`message`) |

A+ 제외 여부와 무관하게 위 필드를 모두 비교합니다.

## 3. 비교 제외 필드와 근거

- **제외는 1개입니다:** `output_directory` 검사의 `message`(Family `preflight.checks[i].message`, Unit `checks[i].message`).
  - 근거: 생산 기계의 출력 절대 경로라 환경에 의존합니다. 직전 라운드에서 명시한 환경·출력 경로 제외 정책을 유지한 것입니다.
  - 이 값도 문자열이어야 하며, 그 검사의 `id`·`status`(PASS)·`code`(null)는 비교합니다.
- **좁힌 것(넓히지 않음):** 직전 라운드에서 값 비교를 제외했던 cli·git_integrity·git_sha·execution_path·attestation은 결정적 값이 확인되어 이번에 모두 비교합니다.
  - git_integrity: Clean 고정 메시지 "보호 파일 외 변경 없음"
  - git_sha: 승인 SHA 메시지
  - execution_path·attestation: 위 READY 정의

## 4. RED/GREEN 결과

Red 원시 출력은 [B1_T11_R2B_VALUE_RED.log](B1_T11_R2B_VALUE_RED.log)(SHA-256 `20439359442817f9002adce0d217f8720024bdb54829dae42fa2c61c7502f420`)입니다.

| 시험 | RED(수정 전 실제 실행) | GREEN |
|---|---|---|
| `R2B_value_a_changed_deterministic_calibration_preflight_field…` | FAIL: 변조 없는 복사본은 통과(`Assert.Null`). `schema_status` 변경 복사본은 **영수증까지 발급**(원인 null). 첫 실패에서 멈춰 이후 항목의 RED 출력은 없음 | PASS: 복사본 정상 발급. 다음 11개 Family 변조가 모두 `Preflight.record_differs`: schema_status, official_evidence true, official_evidence 문자열 "false", official_evidence_note, complete true, complete 숫자 0, family_gate, family_gate_reason, 중첩 `preflight.units[0].seed_manifest_path`, 멤버 추가, 멤버 삭제. Unit 기록 중첩 `checks[2].message`(git_sha)도 `Preflight.record_differs` |
| `R2B_value_a_changed_dominance_games_per_cell_or_gate…` | FAIL: `games_per_cell` 2→3 복사본이 **영수증 발급**(원인 null) | PASS: games_per_cell 3, games_per_cell 문자열 "2", dominance_gate NOT_EVALUATED→PASS, dominance_gate_reason이 모두 `Preflight.record_differs` |
| 기존 `R2B_the_family_and_unit_preflight_records…`, `R2B_a_family_without…` | — | 반례 변경 없이 PASS |

**RED라고 주장하지 않는 항목:** 수정 전 시험이 각 시험의 첫 실패에서 멈췄으므로, Calibration `schema_status`와 Dominance `games_per_cell` 2→3 외의 변조는 수정 전에 실행되지 않았습니다. 그중 최상위 멤버 추가·삭제는 옛 `SameMembers`도 거부하던 항목입니다.

## 5. Raw SHA를 다시 맞춘 변조 반례

`T11R2BValueTests.Copy`의 절차:

1. 세션 Archive의 정상 Calibration·Dominance 증거(문서와 그 아래 모든 파일)를 **새 Archive**로 복사합니다.
2. 대상 PREFLIGHT 한 개만 JSON에서 값 하나를 바꿔 생산자와 같은 직렬화(`RunReport.Json`)로 다시 씁니다.
3. 그 바이트로 Raw SHA·byte_length·ArtifactRef를 다시 만들고(`Archive.WriteNew`), Family 문서를 그 참조로 다시 써 새 문서 참조를 만듭니다.
4. 그 Archive를 루트로 연 새 세션에서 `Certification.VerifyFamily` → `IssueFamily`를 실행합니다. 발급되면 RAW_INVENTORY·source_files는 그 스냅샷에서 일관되게 만들어집니다.

결과: 원인은 `Archive.raw_mismatch`가 아니라 **`Preflight.record_differs`**(내용 비교)이며, JSON Pointer가 함께 보고됩니다.

## 6. 정상 Family·Phase 발급

`OfficialPzl6VerificationTests`(Infrastructure, 실제 세션·Test Profile 자식 실행) 단독 실행 1/1 PASS, 전체 verify에서도 PASS입니다.

- Calibration·Integration·Dominance 영수증과 **Phase 영수증**을 발급합니다.
- INPUT_BUNDLE 4, **PREFLIGHT 57**, REGRESSION_PROOF 1, APPROVAL_RECORD·BUILD_PROVENANCE 각 1을 보존합니다.
- `Receipts.CheckInventory`(RAW_INVENTORY = ORIGINAL source_files)와 Phase 경계 `RequireOriginals` 반례가 통과합니다.
- 복사 Archive의 변조 없는 Calibration(5절)도 발급됩니다.
- TEST_ONLY Seed 991000 유지.

## 7. 전체 시험·Golden·Dry Pilot·Verify

`powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1 -LogName B1_T11_R2B_VALUE_VERIFY_LOG.md`:

- 실행: 2026-10-25 19:40:30 +09:00, exit 0, **`GATE: PASS`**
- 로그: [B1_T11_R2B_VALUE_VERIFY_LOG.md](B1_T11_R2B_VALUE_VERIFY_LOG.md), SHA-256 `565d6737fabfc3eba10705c71e9041baf6e1dc03f7a330de6d4007dfe746f92e`
- 원시 결과: `build/verify/verify-result.json`(git-ignored), SHA-256 `99aeb2bd79d8f719f5f2769f560d528bdf5508d6905cc783df7010f250ae873c`
- 이전 3,039는 참고만 했습니다.

| 항목 | 결과 |
|---|---|
| B1 Release 빌드(13개 프로젝트) | PASS, 경고 0 |
| U1 Domain / U2 Simulation / U3 Application(U3a 14·U3b 48은 부분 재실행) | 327 / 741 / 128 PASS |
| U4 Infrastructure(Phase 영수증 양성 경로 포함) | 1,237 PASS |
| U5 SimHarness(`GoldenTests` 4 포함) | 538 PASS |
| U6 EvidenceVerifier(신규 2) | 70 PASS |
| **합계** | **3,041 PASS / 0 FAIL / 0 SKIP** |
| G1~G4 Godot, E1~E4 Export·cold copy | PASS |
| D1 문서 링크 | PASS(196개 파일) |
| D3 기준 SHA | PASS(5개) |

단계별 확인(verify 전, 같은 코드):

- R2B 4/4
- EvidenceVerifier 70/70
- SimHarness 538/538
- Infrastructure `OfficialPzl6VerificationTests` 1/1

R1~R4 회귀(`T11FixR2Tests`·`T11R2FinalTests`·`ArchiveTests.R4_`·`T11_04_*`), T11-01~08, OWNER 결정 7건(`Decision3*`·`UnitPzlEvidenceTests`·`EvidenceHashTests`·`OfficialPzl6VerificationTests`)은 위 합계 안에서 통과했습니다.

**Dry Pilot 9 Run:** 수정 후 SimHarness DLL `31110bf53d530c36aaa016f1c8703e72dc2f44153ab2230d7dd35f8cddec117b`, `tools/b1-drypilot-repro.ps1` 결과 `ALL_9_IDENTICAL=True`입니다.

- 9개 모두 Exit 0이고, ConfigHash·RunResultHash·환경 외 Manifest가 보관본과 같습니다.
- 예: cal-na `669f8dc9…78b4ff`, dom-sa-ak `9a6d3f63…32966a`, stop-na `0ebd5c36…9bed05`.
- SimHarness 변경은 메시지 리터럴을 상수·함수로 옮긴 것뿐이고(같은 바이트), SimHarness 시험 `R2B_an_official_execution_records…`가 기존 형식 직렬화와 바이트 동일함을 계속 단정합니다.
- Hash·RNG·Replay·Golden 기대값은 바뀌지 않았습니다.

## 8. Git HEAD·변경 파일

- HEAD `257403ddae557a54c4013c17705370caeac7a2e0`(변경 없음), **스테이징 0, Commit·Push 없음**
- `git status --porcelain` 46행(추적 수정 15), `--untracked-files=all` 174행(verify 직후)
- 이번 라운드에서 바꾼 파일:
  - 코드: `tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs`(`PreflightRecords` 교체), `tools/StarPlayer.SimHarness/Official.cs`·`FamilyOfficial.cs`·`DominanceOfficial.cs`(메시지 단일 정의)
  - 시험: `tests/StarPlayer.EvidenceVerifier.Tests/T11R2BValueTests.cs`(신규)
  - 문서:
    - `docs/tasks/B1_T11_R2B_VALUE_BINDING_TASK.md`, `docs/tasks/README.md`
    - `docs/reports/B1_T11_R2B_VALUE_BINDING_REPORT.md`·`B1_T11_R2B_VALUE_RED.log`·`B1_T11_R2B_VALUE_VERIFY_LOG.md`
    - `docs/reports/B1_IMPLEMENTATION_LOG.md`, `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03F_B1_Review.md`

## 9. D3 보호 파일 SHA

`sha256sum -c docs/contracts/b1/B1-T00_BASELINE.sha256`: 5개 OK. verify D3 PASS이며, 문서 수정 후에도 다시 확인했습니다(10절).

| 파일 | SHA-256 |
|---|---|
| `B1-T00_FINAL_CONTRACT_DRAFT.md` · 보존본 `docs/contracts/b1/B1-T00_FINAL_CONTRACT.approved.md` | `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64` |
| `TASK.md` | `ecbc0859c809c68151c93f92058c31a7ab0b84ce8d8cc9a4a25512b7dd06f825` |
| `docs/2023recordRule.pdf` | `9742caaf3a596b03d37fc6c57b9f866e989214e074aa5af1a2ab821ec5f212b0` |
| `docs/2026-official-baseball-rules.pdf` | `f85860f3b754dfb5e07269e9d3a86c77c89b856376f6e3ba8345af36db997859` |

## 10. 문서 수정 후 재확인, 남은 운영 Gate와 OWNER 확인

- 문서 수정 후 다음을 다시 확인했습니다: D1과 같은 규칙의 문서 링크 검사 197개 파일·깨진 링크 0, `sha256sum -c` 기준 목록 5개 OK, 스테이징 0. 문서만 바뀐 뒤 전체 verify를 다시 실행하지는 않았습니다.
- **OWNER 승인 출처:** 결정 3과 R2 도출 선택("승인 Pin 기준")은 채팅 기록이며, 원문 사본이 없습니다. Codex 최신 Finding 원문도 저장소에 없습니다.
- **OWNER 확인 후보(직전 보고서와 같음):**
  - Preflight 파일 배치(B-4)
  - Unit 기록 고정값(`NOT_STARTED`·`execution_not_implemented`)의 의미
  - 튜닝 파일 CRLF와 체크아웃 의존성
- **Windows 운영 Gate(미실시·미검증, PASS 아님):**
  - 보호 앵커 디렉터리·ACL 실측
  - AT-1~AT-14r, AT-4/P-1 정식 측정
  - N27-d·e·f
  - 운영 Environment Profile 승인
  - N12 비트 Golden·witness_digest 승인값
  - 운영 Build Provenance(SIMHARNESS_BINARY·VERIFIER_BINARY 실측, 실행 바이너리 자기 SHA 대조)
