# B1 — T11 R2 잔여 결함 최종 수정 보고서 (R2-A·R2-B, Codex READ-ONLY R2 표적 재검수 제출용)

- 작성: Claude Code, 2026-10-25
- 근거: [TASK](../tasks/B1_T11_R2_FINAL_FIX_TASK.md), OWNER 채팅 지시 "B1 T11 R2 잔여 결함 최종 수정"
- 직전 라운드: [B1_T11_FIX_R2_REPORT.md](B1_T11_FIX_R2_REPORT.md)(R1~R4). R1·R3·R4는 재설계하지 않았습니다.
- 상태: **구현·자체 시험 완료, Codex READ-ONLY R2 재검수 대기.**
  - B1 PASS·Phase 3 완료를 선언하지 않습니다.
  - Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
  - Commit·Push 없음
- 바꾸지 않은 것:
  - 승인 계약·보존본(`1338a2f7…7d64`), OWNER 승인·결정 1~3 원문
  - 루트 `TASK.md`(`ecbc0859…f825`), PDF 2개, D3 기준 목록
  - GameResultHash·RunResultHash·ConfigHash·Attestation Hash, RNG 소비, 기본 Replay 순서, Golden 기대값
- Codex R2-A·R2-B 원문은 저장소에 없습니다. 지적은 OWNER 지시의 기술을 따랐습니다(TASK 0절).

## 1. R2-A 실제 소비 입력과 신뢰된 도출 근거

Production 실행(`FamilyOfficial`/`DominanceOfficial`/`OfficialPreflight`), Replay(`FamilyOfficialReplay`/`DominanceOfficialReplay`·`RunConfig`), 평가(`EvaluationPolicy`·`DominanceEvaluator`), 검증기(Trust Bootstrap·E2-4/E2-5·§12.3)를 모두 조사했습니다.

| 논리 입력 | 실제 로딩 | 보존 ArtifactRef(TEST_ONLY 생산자) | 결속 |
|---|---|---|---|
| `virtual_tuning_v4.json` | `SimHarness.csproj` EmbeddedResource(`content/tuning/virtual_tuning_v4.json`) → `HarnessData.ReadResource` → `LoadTuningV4`(Preflight `model_tuning`, `FamilyOfficial` Config, `Dominance`) | `input-virtual-tuning-v4`, INPUT_BUNDLE | Raw SHA = 런타임 리소스 스트림 바이트(`HarnessData.ResourceBytes`)의 SHA. 리소스 바이트 = 검증 루트의 원본 파일(`ConsumedInputs.CheckSources`). 의미값 = 승인 `OfficialProfile.TuningHash`(Preflight `model_tuning`) |
| `game_rules_profiles_v2.json` | EmbeddedResource(`content/rules/game_rules_profiles_v2.json`) → `LoadRules`(Preflight `rules`, Config) | `input-game-rules-profiles-v2` | 같음. 의미값 = `OfficialProfile.RulesHashes`(Preflight `rules`) |
| Environment Profile | 기록부(L6) → E2-4·E2-5 | `input-environment-profile` | Raw SHA = 승인 기록 `environment_profile.raw_sha256` = 세션 `Trust.Environment.Sha` |
| PZL Test Profile | 기록부(L6) → §12.3 | `input-pzl-test-profile` | = `pzl_test_profile.raw_sha256` = `Trust.TestProfile.Sha` |
| (전용 역할) 승인 기록·Build Provenance·Seed·Unit Manifest·색인 | — | APPROVAL_RECORD·BUILD_PROVENANCE·SEED_MANIFEST·UNIT_MANIFEST·DIAGNOSTICS | 기존 필수 검사 |
| (입력 파일 아님) 중립 구장·코호트·정책·실행 프로파일·평가 정책 | `HarnessData.NeutralPark`, `NeutralCohort`, `PolicyRegistry`, `OfficialProfile`, `EvaluationPolicy` | — | 코드 상수(바이너리 범위) |
| (입력 데이터 아님) 실행 허가 기록 | 기록부(L7) | — | 영수증 `execution_authorization_id` |

- **도출:** 필수 INPUT_BUNDLE의 Raw SHA 다중 집합은 `ConsumedInputs.Expected`입니다. 승인 Pin 2개와 `HarnessData.Inputs` 각 리소스의 런타임 바이트 SHA로 이루어지며, 제출 목록에서는 도출하지 않습니다.
  - Family/Dominance 검증(`RequiredCounts`)은 `archive_invalid`/`Archive.required_original`로 거부합니다.
  - 발급 경계(`RequireOriginals`)는 세션 신뢰값으로 다시 확인하고 `not_certifiable`/`Certification.required_original`로 거부합니다.
  - 다중 집합 비교이므로 누락·중복·치환·추가가 모두 거부됩니다.
- **목록의 완전성:** `HarnessData.Inputs` = `SimHarness.csproj`의 EmbeddedResource(LogicalName·Include) = 어셈블리 `GetManifestResourceNames()`입니다. 각 리소스 바이트 = 저장소 파일 바이트입니다(SimHarness 시험 `R2A_the_input_list_is_exactly…`).
- **실측(Codex 확인 요청):** `content/tuning/virtual_tuning_v4.json`은 작업 트리 CRLF 26,505 B(system `core.autocrlf=true`), Git blob LF 26,346 B입니다. 빌드가 내장한 바이트는 **작업 트리 바이트**이며, 따라서 보존 원본도 그 바이트입니다. 같은 커밋이라도 체크아웃 설정이 다르면 내장 바이트와 SHA가 달라지므로, 운영 빌드 절차(B-4·Build Provenance)에서 고정해야 합니다(10절).
- **Build Provenance:** TEST_ONLY Build Provenance에는 SIMHARNESS_BINARY가 없어, 내장 리소스를 담은 실행 바이너리의 SHA 대조는 이번에도 하지 않았습니다(남은 위험, 운영 Gate).

## 2. R2-B 실제 Preflight 기록과 필요성 판단 근거

- **기존 상태:** 공식 Preflight는 파일을 쓰지 않았습니다.
  - Unit Preflight 결과(`PreflightReport`, 검사 15개)는 메모리에만 있었고, 남는 것은 Attestation(UNIT_MANIFEST 안)뿐이었습니다.
  - Family Preflight(검사 cli·git_integrity·git_sha·model_tuning·output_directory·seed_manifests·(games_per_cell)·unit/cell_preflight·execution_policy·execution_path)는 실행 색인(DIAGNOSTICS)의 `preflight.checks` 요약에만 있었습니다.
  - §21 "PREFLIGHT Family·Unit별 필요 기록, 첫 경기 전"에 따라 **Family 1 + Unit별 1**이 필요하다고 판단했습니다.
- **보존(기존 형식, 새 공개 형식 없음):** 공식 실행 `FamilyOfficial.Execute`·`DominanceOfficial.Execute`가 출력 디렉터리를 고정한 직후, **첫 Unit·첫 경기 전에** `OfficialPins.WritePreflight`로 다음을 새로 만듭니다(덮어쓰지 않음, 실패하면 경기 전에 중단).
  - `preflight.json`: 기존 `FamilyOfficial.PreflightDocument`/`DominanceOfficial.PreflightDocument`. 실행이 진입한 바로 그 READY Report입니다.
  - `preflight/<unit>.json`: 기존 단일 Manifest 결과 `OfficialOutput.Result(PreflightReport)` `simharness-official-result-v1`
  - 생산자 `EvidenceWriter.WriteRun`은 이 파일들을 바이트 그대로 PREFLIGHT(Family: unit_id null, Unit: unit_id)로 보존합니다.
- **검증(`PreflightRecords.Verify`, Replay 전):**
  - 개수·경로: Family 1·Unit별 정확히 1이며, 그 밖의 PREFLIGHT는 없어야 합니다(`Archive.required_original`/`run_file_missing`).
  - 비교 기준: 검증기가 **같은 승인값으로 Family/Dominance Preflight를 직접 다시 실행**합니다(경기 없음, 쓰기 없음, 새 출력 이름).
  - 비교 내용:
    - 기존 형식 멤버 집합·순서 동일
    - 모든 기록 검사 PASS, 검사 ID 순서 = 재실행
    - AMPV 고정 분류표의 평가 대상 검사(Unit 9개, Family 4개)의 code·message = 재실행
    - Unit 기록의 git·실행 정책·코호트·Seed·모델·튜닝·규칙·config·attestation 값 = 그 Unit의 보존 UNIT_MANIFEST Attestation
    - Family 기록의 run_id·family·`PREFLIGHT_READY`·Unit 목록(ID·Seed 경로·ready) = 승인 계획
  - 위반은 `approval_mismatch`/`Preflight.record_differs`입니다.
  - 실행 전용 검사(cli·git_integrity·git_sha·output_directory·execution_path·attestation)는 환경·경로에 따라 달라 값을 비교하지 않고 PASS만 요구합니다.
  - Attestation이나 DIAGNOSTICS만으로는 통과하지 않습니다.
- **판단이 필요한 사항(Codex·OWNER 확인 요청, 새 규범은 추가하지 않음):**
  1. 파일명 `preflight.json`·`preflight/<unit>.json`은 B-4가 정할 사항입니다. OWNER 결정 3 §6의 `run/<run_id>/` 내부 배치와 같은 성격으로 두었고, B-4 공식 경로를 주장하지 않습니다.
  2. Unit 기록 형식 `simharness-official-result-v1`은 단일 Manifest CLI 형식이라, 준비된 Preflight에 `run_status=NOT_STARTED`·`error_code=Official.execution_not_implemented`가 들어갑니다. 기존 형식을 바꾸지 않으려고 그대로 두었고, 검증기는 이 고정값을 요구합니다. 형식 의미를 바꿀지는 `OWNER_DECISION_REQUIRED` 후보입니다.
  3. 공식 실행 출력에 파일 2종을 더했습니다. Manifest·색인·Hash는 바뀌지 않았고, Replay는 디렉터리를 열거하지 않습니다.

## 3. 수정 파일·함수·라인

| 대상 | 위치 |
|---|---|
| 입력 목록·원본 바이트 | `tools/StarPlayer.SimHarness/HarnessData.cs:49`(`Inputs`)·`:53`(`ResourceBytes`) |
| Preflight 기록 | `tools/StarPlayer.SimHarness/DominanceOfficial.cs:181`(`OfficialPins.WritePreflight`)·`:266`(Dominance 호출), `FamilyOfficial.cs:209`(호출), `Official.cs:363`·`:365`(파일명) |
| 생산자 | `tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs:62`·`:69`(PREFLIGHT 보존) |
| 검증기 | `tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs`(신규: `ConsumedInputs` `:11`·`Expected` `:14`·`CheckSources` `:21`, `PreflightRecords` `:40`·`Verify` `:48`), `Verifier.cs:240`·`:241`(호출)·`:372`(INPUT_BUNDLE)·`:379`(PREFLIGHT 개수), `Certification.cs:149`(`RequireOriginals`, Family 목록)·`:166` |
| 시험 | `tests/StarPlayer.EvidenceVerifier.Tests/T11R2FinalTests.cs`(신규 5), `T11FixR2Tests.cs`(R2 기대값 갱신), `tests/StarPlayer.SimHarness.Tests/Evidence/R2InputsAndPreflightTests.cs`(신규 2), `tests/StarPlayer.Infrastructure.Tests/OfficialPzl6VerificationTests.cs`(Phase 단정 추가) |
| Fixture | `TestOnlyWorld`(세계 루트에 content 원본 복사), `Production.Trust`(내장 입력 2개 보존) |

## 4. 수정 전·후 반례

Red 원시 출력은 [B1_T11_R2_FINAL_RED.log](B1_T11_R2_FINAL_RED.log)(SHA-256 `c266c5a7ac608a19aa71ed00f846b67e68c8c5593241060829938a13d9c70474`)입니다.

| 시험 | Red(수정 전) | Green |
|---|---|---|
| `R2A_the_two_profiles_without_the_consumed_tuning_and_rules_inputs_are_refused` | FAIL "No exception was thrown"(Profile 2개 + 튜닝·규칙 누락 문서가 발급됨) | PASS `Archive.required_original` |
| `R2B_a_family_without_its_preflight_records_is_refused` | FAIL "No exception was thrown"(PREFLIGHT 0개 발급) | PASS `Archive.required_original` |
| `R2A_every_consumed_input_is_required…` | 수정 후 작성 | 정상 4개 양성(문서·영수증·Inventory). 하나 누락 → `required_original`, Raw SHA 변경 → `Archive.raw_mismatch`, 같은 ID 다른 바이트(LF blob) → `required_original`, 승인 집합 밖 추가 → `required_original`, 같은 입력 두 ID → `required_original` |
| `R2A_an_embedded_input_must_be_its_repository_original…` | 수정 후 작성 | 정상 통과. 리소스 변경 → `Input.embedded_differs`, 원본 파일 변경(CRLF→LF) → `Input.embedded_differs`, 원본 없음 → `Input.embedded_differs` |
| `R2B_the_family_and_unit_preflight_records_are_kept_bound…` | 수정 후 작성 | 양성(문서 5개 = Family 1 + Unit 4, 영수증 ORIGINAL 5, Inventory, 내용 비교 통과). 특정 Unit 누락·Attestation만·DIAGNOSTICS만(역할 변경) → `required_original`, SHA 변조 → `raw_mismatch`, 다른 Unit 기록 대체(참조) → `run_file_missing`, 다른 Unit 바이트·평가 검사 message 변조·검사 NOT_READY·attestation 변경·멤버 추가·검사 누락·Family 상태/Unit 목록/run_id 변경 → `Preflight.record_differs` |
| `T11FixR2Tests.R2_the_approval_and_build…`(직전 라운드) | — | 기대값 갱신: INPUT_BUNDLE 2 → 4, `RequireOriginals` Family 인자. 추가 반례: 소비 입력 하나 누락, PREFLIGHT 없음, 한 Unit 기록 누락, 다른 Family 기록 누락 → `Certification.required_original` |
| `T11FixR2Tests.R3_two_families…`(직전 라운드) | — | 공유 원본 수 4 → 6(검증 집합 원본 = 승인·Build·입력 4개). 정확한 개수를 그대로 단정하며, 조건(7필드 동일·검증 집합 역할)은 바꾸지 않음 |
| SimHarness `R2A_the_input_list…`, `R2B_an_official_execution_records…` | 수정 후 작성 | 입력 목록 = csproj = 어셈블리 리소스, 리소스 = 원본 파일. 첫 셀 전에 중단된 실행에도 Family·48 Unit 기록이 기존 형식 직렬화와 바이트 동일하게 있고 셀 출력은 없음 |

## 5. 정상 Family·Phase 영수증 양성 시험

`OfficialPzl6VerificationTests`(Infrastructure, 실제 세션·Test Profile 자식 `dotnet test`)는 Family 영수증 3개를 발급하고(Calibration INSUFFICIENT_SAMPLE·허용, Integration PASS, Dominance PASS), 검증된 REGRESSION_PROOF로 **Phase 영수증을 발급**합니다(`TestOnlyRegistered`, 기록부 바이트 동일). 단정 내용은 다음과 같습니다.

- 세 Family SourceFiles 전부 ⊂ Phase ORIGINAL, ID 고유
- APPROVAL_RECORD 1·BUILD_PROVENANCE 1·**INPUT_BUNDLE 4**(공유 병합 1회)
- **PREFLIGHT 4+2+48+3 = 57**, REGRESSION_PROOF 1, FAMILY/DOMINANCE_RESULT 3
- `Receipts.CheckInventory` 통과
- Phase 경계 `RequireOriginals` 정상 통과. 다음은 모두 `Certification.required_original`로 거부합니다.
  - 내장 입력 누락
  - Integration Unit 기록 누락
  - Dominance Family 기록 누락
  - Family 목록 불일치
- `PhaseOriginals`는 같은 ID에 다른 SHA인 입력을 `Phase.artifact_id_conflict`로 거부합니다(병합은 동일성 조건에서만).

## 6. RAW_INVENTORY·SourceFiles 전체 ORIGINAL 결속

- RAW_INVENTORY는 영수증 ORIGINAL source_files와 바이트 동일합니다(`Archive.Inventory`·`Receipts.CheckInventory`, 발급 경계와 Phase 자식에서 필수).
- ORIGINAL 목록은 R1 스냅샷에서 나오며, R2-A·R2-B 필수 집합은 Verifier(승인 기록·실행 경로)와 발급 경계(세션 신뢰)에서 두 번 확인합니다.
- 양성 증거:
  - Calibration 영수증: INPUT_BUNDLE 4·PREFLIGHT 5 ORIGINAL과 Inventory 통과(`R2A_every…`·`R2B_the_family…`)
  - Phase 영수증: 5절
- 문서와 Inventory에서 동시에 제거해도 거부됩니다(입력·Preflight·승인·Build 모두).

## 7. 전체 시험·verify

`powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1 -LogName B1_T11_R2_FINAL_VERIFY_LOG.md`:

- 실행: 2026-10-25 18:41:01 +09:00, exit 0, **`GATE: PASS`**
- 로그: [B1_T11_R2_FINAL_VERIFY_LOG.md](B1_T11_R2_FINAL_VERIFY_LOG.md), SHA-256 `101ffcbe1a375a3f1b924d46170a91b00c34b019a18ee80ef201ae37c88dc249`
- 원시 결과: `build/verify/verify-result.json`(git-ignored), SHA-256 `064a169713b315633a11667eab58c5f7d552149e1c0d2bbdbe1917d68a9fc8b1`
- 직전 기준 3,032는 참고만 했습니다.

| 항목 | 결과 |
|---|---|
| B1 Release 빌드(13개 프로젝트) | PASS, 경고 0 |
| U1 Domain / U2 Simulation / U3 Application(U3a·U3b는 부분 재실행) | 327 / 741 / 128 PASS |
| U4 Infrastructure(Phase 양성 경로 포함) | 1,237 PASS |
| U5 SimHarness(신규 2, `GoldenTests` 4) | 538 PASS |
| U6 EvidenceVerifier(신규 5) | 68 PASS |
| **합계** | **3,039 PASS / 0 FAIL / 0 SKIP** |
| G1~G4 Godot, E1~E4 Export·cold copy | PASS |
| D1 문서 링크 | PASS(193개 파일) |
| D3 기준 SHA | PASS(5개) |

단계별 확인(verify 전, 같은 코드):

- SimHarness 538/538
- EvidenceVerifier 67/68
  - 1 FAIL: `R3_two_families…`의 공유 원본 수 4 → 6. 소비 입력 2개가 늘어서 생긴 정확한 개수 변화라 기대값을 갱신했고, 재실행 1/1 PASS입니다. verify에서 68/68.
- Infrastructure `OfficialPzl6VerificationTests` 1/1
- Dry Pilot(8절)

R1~R4 회귀(`T11FixR2Tests`·`ArchiveTests.R4_`·`T11_04_*`), T11-01~08(`T11CertificationTests`·`T11FixTests`), OWNER 결정 7건(`Decision3*`·`UnitPzlEvidenceTests`·`EvidenceHashTests`·`OfficialPzl6VerificationTests`)은 위 합계 안에서 통과했습니다.

보고서·기록 문서는 verify 뒤에 고쳤고, 그 뒤 링크와 D3를 다시 확인했습니다(11절).

## 8. Hash·RNG·Replay 불변성

- **Dry Pilot 9 Run:** 이번 수정 후 SimHarness DLL SHA-256은 `2d017ffa62ef529e4bfe1af4877adc9e3fd72b53b58e7b1bfb01c755deeceec0`이고, `tools/b1-drypilot-repro.ps1` 결과 `ALL_9_IDENTICAL=True`입니다.
  - 9개 모두 Exit 0이고, `config_hash`·`run_result_hash`·환경 외 Manifest가 보관본과 같습니다.
  - 예: cal-na `669f8dc9…78b4ff`, dom-sa-ak `9a6d3f63…32966a`, stop-na `0ebd5c36…9bed05`.
- **공식 실행 변경:** Preflight 파일 기록은 경기 전 쓰기 2종뿐입니다. Manifest·색인 내용, Attestation, config·결과 Hash, RNG 소비, Replay 순서는 그대로입니다.
  - SimHarness `DominanceOfficialTests`·`FamilyOfficialTests`(Replay·변조 반례 포함)가 통과했습니다.
- **`HarnessData.ResourceBytes`:** 읽기 전용 추가이며 `LoadTuningV4`·`LoadRules`는 바꾸지 않았습니다.
- **Golden:** 4개 통과, 기대값 무변경.

## 9. Git 상태

- HEAD `257403ddae557a54c4013c17705370caeac7a2e0`(변경 없음), **스테이징 0, Commit·Push 없음**
- `git status --porcelain` 44행(추적 수정 15), `--untracked-files=all` 170행(verify 직후)
- 이번 라운드에서 바꾼 파일:
  - 제품·도구 코드:
    - `tools/StarPlayer.SimHarness/HarnessData.cs`·`DominanceOfficial.cs`·`FamilyOfficial.cs`·`Official.cs`·`Evidence/EvidenceWriter.cs`
    - `tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs`(신규)·`Verifier.cs`·`Certification.cs`
  - 시험:
    - `tests/StarPlayer.EvidenceVerifier.Tests/T11R2FinalTests.cs`(신규)·`T11FixR2Tests.cs`·`TestOnlyWorld.cs`
    - `tests/StarPlayer.SimHarness.Tests/Evidence/R2InputsAndPreflightTests.cs`(신규)
    - `tests/StarPlayer.Infrastructure.Tests/OfficialPzl6VerificationTests.cs`
  - 문서:
    - `docs/tasks/B1_T11_R2_FINAL_FIX_TASK.md`, `docs/tasks/README.md`
    - `docs/reports/B1_T11_R2_FINAL_FIX_REPORT.md`·`B1_T11_R2_FINAL_RED.log`·`B1_T11_R2_FINAL_VERIFY_LOG.md`
    - `docs/reports/B1_IMPLEMENTATION_LOG.md`, `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03F_B1_Review.md`

## 10. 남은 OWNER 확인과 Windows 운영 Gate

- **OWNER 확인:**
  - 결정 3과 R2 도출 선택("승인 Pin 기준")은 채팅 기록이며, 원문 사본이 없습니다.
  - Codex R2-A·R2-B 원문이 없습니다.
  - 2절의 판단 사항 1~3(Preflight 파일 배치는 B-4, Unit 기록 형식의 고정값 의미, 공식 출력 파일 추가)
  - 1절 튜닝 파일 CRLF와 체크아웃 의존성(운영 빌드 절차에서 `-text` 또는 빌드 입력 고정 필요)
- **Windows 운영 Gate(미실시·미검증, PASS 아님):**
  - 보호 앵커 디렉터리·ACL 실측
  - AT-1~AT-14r, AT-4/P-1 정식 측정
  - N27-d·e·f
  - 운영 Environment Profile 승인
  - N12 비트 Golden·witness_digest 승인값
  - 운영 Build Provenance(SIMHARNESS_BINARY·VERIFIER_BINARY 실측과 실행 바이너리 자기 SHA 대조)

## 11. 보고서 작성 후 재확인

- verify 이후 이 보고서, 구현 로그, `CURRENT_STATE.md`, Dashboard, 리뷰 초안, `docs/tasks/README.md`를 고쳤습니다. 코드와 시험은 바꾸지 않았습니다.
- 그 뒤 다시 실행한 확인:
  - D1과 같은 규칙의 문서 링크 검사: 194개 파일, 깨진 링크 0
  - `sha256sum -c docs/contracts/b1/B1-T00_BASELINE.sha256`: 5개 OK
  - 스테이징 0
- 문서만 바뀐 뒤 전체 verify를 다시 실행하지는 않았습니다.
