# B1 — T11 2차 FIX_REQUIRED(R1~R4) 수정 보고서 (Codex 표적 재검수 제출용)

- 작성: Claude Code, 2026-10-25
- 근거:
  - [수정 TASK](../tasks/B1_T11_FIX_R2_TASK.md)
  - OWNER 채팅 지시 "B1 T11 2차 FIX_REQUIRED 수정 지시"
  - R2 도출 방식에 대한 OWNER 선택 "승인 Pin 기준"(채팅 응답)
  - 재개 지시 "계속 진행해"
- 상태: **구현·자체 시험 완료, Codex READ-ONLY 표적 재검수 대기.**
  - B1 완료 아님
  - Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
  - Commit·Push 하지 않음
- 바꾸지 않은 것:
  - 승인 계약·보존본(`1338a2f7…7d64`)
  - OWNER 승인·결정 1~3 원문
  - 루트 `TASK.md`(`ecbc0859…f825`), PDF 2개, D3 기준 목록(`a9865a2f…9430`)
  - GameResultHash·RunResultHash·ConfigHash·Attestation Hash 계산식
  - RNG 소비, Replay 순서
- Codex 2차 보고서 원문은 받지 못했습니다. 지적·재현 경로는 OWNER 지시의 요구를 현재 코드로 다시 추적한 것입니다(TASK 0절).

## 1. 수정 내용과 코드 위치

| Finding | 수정 | 위치 |
|---|---|---|
| R1 MAJOR 검증 후 가변 데이터 | JSON 트리를 불변으로 바꿨습니다. `JArray.Items`·`JObject.Members`는 생성 시 복사한 읽기 전용 배열이라, 원래 목록이나 형변환으로 바꿀 수 없습니다. `VerifiedFamily`의 `PzlUnits`·`SourceFiles`와 `TestProfileRun.Evidence`도 읽기 전용입니다. 검증 종료 시점 스냅샷 `Frozen`(Canonical 사본·A+·문서·Unit PZL·#6·Payload·SourceFiles의 JCS)을 결과에 묶어 둡니다. 발급 경계 `Bound`는 공개 객체가 스냅샷과 같은지 확인하고, 다르면 `not_certifiable`/`Certification.result_mutated`로 거부합니다. 같으면 스냅샷 바이트를 새로 파싱한 사본만 반환하며, 이후 Target·A+·Canonical·SourceFiles·Inventory는 모두 이 사본에서 만듭니다. | `tools/StarPlayer.SimHarness/Evidence/StrictJson.cs:68`·`:79`, `tools/StarPlayer.EvidenceVerifier/Verifier.cs:127`(Frozen)·`:142`(Snapshot)·`:317`, `Certification.cs:35`·`:128`(Bound) |
| R2 MAJOR 필수 ORIGINAL | 필수 원본은 신뢰된 승인 기록에서 도출하고, 제출 목록에서는 도출하지 않습니다(OWNER 선택 "승인 Pin 기준"). ① Family/Dominance 검증(`RequiredCounts`)이 요구하는 것: APPROVAL_RECORD 1(승인 Raw SHA), BUILD_PROVENANCE 1(승인 Pin), INPUT_BUNDLE 정확히 2(Environment Profile·PZL Test Profile Pin), 모두 Unit·경기 없음. PREFLIGHT와 REGRESSION_PROOF는 0개여야 합니다. Preflight 결과는 UNIT_MANIFEST 안 Attestation이고(§21 "Manifest 내부 허용"), REGRESSION_PROOF는 Phase 입력입니다. 위반은 `archive_invalid`/`Archive.required_original`입니다. ② 발급 경계 `RequireOriginals`는 영수증 ORIGINAL을 **세션 신뢰값**(`Trust.Approval.RawSha256`·`Trust.Build.Sha`·`Trust.Environment.Sha`·`Trust.TestProfile.Sha`)과 다시 대조합니다. Phase에서는 검증된 REGRESSION_PROOF SHA도 대조하며, 위반은 `not_certifiable`/`Certification.required_original`입니다. ③ 신규 `simharness-regression-proof-v1`(`TestProfiles.RegressionProof`)은 승인 Profile 8개 Check의 TestEvidence를 Profile 순서로 담은 JCS입니다. `Certification.VerifyRegressionProof`는 제출본을 Archive 스냅샷으로 읽어 검증기 자신의 Test Profile 실행 결과와 바이트 비교하고(§12.3 2), 다르면 `TestProfile.regression_proof_differs`로 거부합니다. `IssuePhase`는 `VerifiedRegressionProof`를 받습니다. | `Verifier.cs:339`·`:369`, `Certification.cs:47`(VerifiedRegressionProof)·`:148`(RequireOriginals)·`:196`(VerifyRegressionProof)·`:222`(IssuePhase), `TestProfiles.cs:225` |
| R3 MAJOR Phase ID 충돌 | `EvidenceWriter.WriteDocument`의 artifact_id를 역할 이름에서 **Archive 경로(확장자 제외)**로 바꿨습니다. 그래서 Calibration·Integration 문서가 `family-result` 하나를 공유하지 않습니다. `GroupBy(artifact_id).First()`를 없애고 `PhaseOriginals`로 바꿨습니다. 같은 ID는 7필드+origin이 완전히 같고 역할이 검증 집합 범위(APPROVAL_RECORD·BUILD_PROVENANCE·INPUT_BUNDLE)일 때만 합칩니다. 다른 경로·SHA·길이·역할, Family 고유 파일의 재사용, 한 경로를 가리키는 두 ID는 `archive_invalid`(`Phase.artifact_id_conflict`/`Phase.path_conflict`)입니다. | `tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs:68`, `Certification.cs:245`·`:252` |
| R4 MINOR 루트 생성 경계 | `Archive.WriteNew`의 `Directory.CreateDirectory(root)`를 없앴습니다. `PinnedDirectory.Open(…, createRoot: true)`는 가장 깊은 기존 상위부터 볼륨 루트 핸들 사슬로 고정하고, 없는 루트 구성 요소를 고정 부모 기준으로 상대 생성합니다(링크 거부, FILE_SHARE_DELETE 없음). 존재 확인은 생성 시작 지점을 고를 때만 쓰고, 모든 구성 요소는 핸들로 열어 검사합니다. 상위의 Junction은 무엇이든 만들기 전에 `Archive.reparse_point`로 거부됩니다. | `tools/StarPlayer.SimHarness/PathSafety.cs:119`·`:123`, `Archive.cs:175`·`:196` |

## 2. 수정 전(Red)·후(Green) 시험

Red 원시 출력: [B1_T11_FIX_R2_RED.log](B1_T11_FIX_R2_RED.log) (SHA-256 `81a470b0b3878c2080b21e45db99787b9d77306ba90f19e7e0ccfcc6f7cb4368`).

| Finding | 시험 | Red(수정 전 실제 실행) | Green |
|---|---|---|---|
| R4 | `ArchiveTests.R4_a_new_archive_root_below_a_junction_is_refused_without_creating_anything_outside` | FAIL at `ArchiveTests.cs:198`: Junction 대상에 디렉터리가 먼저 생김 | PASS. 거부와 외부 0항목, 정상 새 루트 생성·기존 루트 사용 |
| R4 유지 | `ArchiveTests.T11_04_*` 3개(파일 생성 후 부모 교체, 읽기 중 교체, 영수증 재시도 읽기) | — | PASS |
| R1 | `T11CertificationTests.R1_a_verified_result_cannot_be_changed_in_place_and_issuance_uses_exactly_the_verification_snapshot` | FAIL "SourceFiles add": 검증 결과 목록에 Add 성공. 첫 단정에서 멈춰 Canonical 경로는 따로 Red 출력이 없음 | PASS. ① SourceFiles 추가·제거, Raw SHA·중첩 JObject/JArray 변경이 모두 `NotSupportedException`이고 내용 불변 ② `Canonical[0]` 변경은 `Certification.result_mutated` ③ 바꾸지 않은 결과의 영수증 CANONICAL_PAYLOAD 바이트·A+·ORIGINAL source_files = 검증 스냅샷 |
| R2 | `R2_the_approval_and_build_originals_the_session_used_are_required_even_when_removed_from_the_document_and_the_inventory_together` | FAIL "No exception was thrown": 승인 원본 없이 Calibration 영수증이 발급됨 | PASS. ① 승인·Build·INPUT_BUNDLE을 문서·스냅샷·Inventory에서 동시에 빼면 `Archive.required_original` ② INPUT_BUNDLE을 다른 파일로 바꾸거나 PREFLIGHT·REGRESSION_PROOF를 추가해도 같은 원인 ③ 정상 ORIGINAL 전체 양성(세션 SHA 일치) ④ 발급 경계 `RequireOriginals`는 승인·Build 누락, Build SHA 변경, Environment 누락, Phase 증명 누락 → `Certification.required_original` |
| R2 | `R2_a_regression_proof_must_be_the_verifier_s_own_test_profile_run_byte_for_byte` | 수정 후 작성(신규 형식, Red 실행 없음) | PASS. 정상 통과·source=ORIGINAL 참조, 다른 실행 결과 → `regression_proof_differs`, 역할 위조 → `required_original`, SHA 위조 → `Archive.raw_mismatch`, 다른 세션 → `Certification.test_profile` |
| R3 | `R3_two_families_of_one_archive_never_name_different_originals_with_one_artifact_id` | FAIL: 같은 ID의 두 원본(byte_length 119,924 ≠ 403,556 = Calibration·Integration 문서). 옛 ID 규칙을 임시로 되돌려 실행한 뒤 수정본으로 복원 | PASS. 공유 ID는 검증 집합 원본 4개뿐이며 7필드가 같음 |
| R3 | `R3_the_phase_union_keeps_every_family_s_originals_and_refuses_one_id_for_two_originals` | 수정 후 작성(새 함수, Red 실행 없음) | PASS. 세 Family 합집합, Codex 경로(Cal/Int 같은 ID), 같은 ID 다른 SHA·다른 경로, Family 고유 파일 재사용 → `artifact_id_conflict`, 두 ID 한 경로 → `path_conflict` |

## 3. 정상 Certification·Phase 영수증 양성 경로

`tests/StarPlayer.Infrastructure.Tests/OfficialPzl6VerificationTests.cs`의 `The_fixed_twenty_game_sample_round_trips_and_the_integration_evidence_verifies_end_to_end`는 실제 세션에서 끝까지 진행합니다.

1. Trust Bootstrap → E2-1/E2-4/E2-5
2. 세 Family 검증 → 검증기 자신의 Test Profile 실행(자식 `dotnet test`)
3. Family 영수증 3개(Calibration INSUFFICIENT_SAMPLE·허용, Integration PASS, Dominance PASS)
4. 제출 REGRESSION_PROOF 검증(위조본 거부)
5. A28 거부 3건 → **Phase 영수증 발급**

발급된 Phase 영수증은 다음을 단정합니다.

- 상태와 등록: `TestOnlyRegistered`, 기록부 바이트 동일
- targets·자식: targets CAL→INT→DOM→PHASE, Phase A+ = 검증 Payload, child 3개 순서
- 원본 목록:
  - 세 Family SourceFiles 전부가 Phase ORIGINAL에 있음(ID 충돌로 유실 없음)
  - ID 고유
  - REGRESSION_PROOF 1, APPROVAL_RECORD 1·BUILD_PROVENANCE 1·INPUT_BUNDLE 2
  - FAMILY/DOMINANCE_RESULT 3
- 결속: `Receipts.CheckInventory`(RAW_INVENTORY = ORIGINAL) 통과

결과: 단독 실행 1/1 PASS, 전체 verify에서도 PASS(5절).

**양성 경로를 위한 시험 Fixture 변경(Codex 확인 요청):**

- TEST_ONLY 세계의 48셀 Seed 기준값을 990000에서 **991000**으로 바꿨습니다(`TestOnlyWorld.DominanceSeedBase`, 셀당 2경기, 시험 전용 가상 Seed).
  - 기존 값의 Dominance Gate는 `NOT_EVALUATED`(분산 0 셀 비교)라 §19상 발급할 수 없었습니다.
  - 991000은 임시 탐색으로 찾았습니다(990000 NOT_EVALUATED, 991000 PASS). 탐색 코드는 삭제했습니다.
  - 엔진·평가식·Hash는 바꾸지 않았고, SimHarness의 `DominanceOfficialWorld` 기본값(990000)도 그대로입니다.
- 세계 기본 승인 기록이 실제 Build Provenance·Environment Profile·Test Profile 바이트를 Pin합니다(`TestOnlyTrust.Files`/`Repoint`).
- 생산자(`Production`)는 네 원본(`trust/…`)을 한 번 보존하고 모든 Family 문서가 참조합니다. `ProducerFor(TestOnlyTrust)`는 그 기록의 원본을 보존합니다.
- **검사를 약화하지 않았습니다.** 누락이 생기던 Fixture를 계약에 맞게 고쳤습니다.

## 4. 기대값이 바뀐 기존 시험 (완화 여부 판단 요청)

1. `T11CertificationTests.T11_02_*` 마지막 단정: `Certification.statistical` → `Phase.child_not_registered`.
   - Dominance가 이제 발급 가능해져 세 번째 자리의 미등록 ID가 먼저 거부됩니다.
   - Phase 발급 자체는 3절 양성 경로가 다룹니다.
2. `VerifierTests.The_dominance_evidence_verifies_end_to_end_…`: `(NOT_EVALUATED, NotCertifiable)` → `(PASS, null)`.
   - Seed 기준값 변경 때문입니다.
   - NOT_EVALUATED·INCOMPLETE 거부는 `CertificationAndEnvironmentTests`의 §19 표 시험이 계속 단정합니다.
3. `OfficialPzl6VerificationTests`: Calibration·Dominance `NotCertifiable` 단정 → 세 영수증 발급과 Phase 양성 경로.
   - 승인 기록에 INSUFFICIENT_SAMPLE 허용을 켭니다.
   - 허용 없는 Calibration 거부는 `T11_01_the_session_enforces…`의 `calStrict`가 계속 단정합니다.
   - 마지막 `Certification.statistical` 단정은 `Phase.child_role`(역할 순서 위반)로 바꿨습니다.
4. `T11_02`·`Decision3VerifierTests`: `IssuePhase` 인자를 `TestProfileRun`에서 `VerifiedRegressionProof`(내부 생성자)로 감쌌습니다. 두 시험 모두 자식 결속 단계에서 거부되므로 증명 내용에는 도달하지 않습니다.

## 5. 전체 시험·Golden·verify

`powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1 -LogName B1_T11_FIX_R2_VERIFY_LOG.md`:

- 실행: 2026-10-25 15:49:34 +09:00, exit 0, **`GATE: PASS`**
- 로그: [B1_T11_FIX_R2_VERIFY_LOG.md](B1_T11_FIX_R2_VERIFY_LOG.md), SHA-256 `2c6de56f216d980865434bbedb64a8a9298423b75169d6d5961eca481abf031d`
- 원시 결과: `build/verify/verify-result.json`(git-ignored), SHA-256 `31e341edb5101b0a297f0a98e62c7361148eba51935b4d136494e945ccd8c847`
- 직전 기준 3,026은 재사용하지 않았습니다.

| 항목 | 결과 |
|---|---|
| B1 Release 빌드(13개 프로젝트) | PASS, 경고 0 |
| U1 Domain | 327 PASS |
| U2 Simulation | 741 PASS |
| U3 Application(U3a 아키텍처 14·U3b 결정성 48은 U3의 부분 재실행, 합계에 미포함) | 128 PASS |
| U4 Infrastructure(3절 Phase 양성 경로 포함) | 1,237 PASS |
| U5 SimHarness(R4 1개 추가, `GoldenTests` 4개 포함) | 536 PASS |
| U6 EvidenceVerifier(R1~R3 5개 추가) | 63 PASS |
| **합계** | **3,032 PASS / 0 FAIL / 0 SKIP** |
| G1~G4 Godot, E1~E4 Export·cold copy | PASS |
| D1 문서 링크 | PASS(190개 파일) |
| D3 기준 SHA | PASS(5개 일치) |

단계별 확인(같은 트리, verify 전):

- SimHarness 536/536
- EvidenceVerifier 63/63
- Infrastructure `OfficialPzl6VerificationTests` 1/1
- Dry Pilot 재현(6절)

T11-01~08 회귀 시험(`T11CertificationTests`·`T11FixTests`·`ArchiveTests.T11_04_*`)과 OWNER 결정 7건 회귀 시험(`Decision3EvidenceTests`·`Decision3VerifierTests`·`UnitPzlEvidenceTests`·`EvidenceHashTests`·`OfficialPzl6VerificationTests`)은 위 합계 안에서 모두 통과했습니다.

이 보고서와 기록 문서는 verify 뒤에 작성했습니다. 그래서 문서 링크(같은 규칙의 스크립트)와 `sha256sum -c docs/contracts/b1/B1-T00_BASELINE.sha256`을 다시 실행했고, 결과는 10절에 있습니다.

## 6. 기존 Hash·RNG·Replay 불변성

- **Dry Pilot 9 Run 재현:** 이번 수정 후 Release SimHarness DLL SHA-256은 `96baca7e3d8cb6848470604a1cf77539afa1990a15157e7a7329f081d1300902`이고, 보관 Seed는 그대로 썼습니다.
  - 결과 `ALL_9_IDENTICAL=True`. 9개 모두 Exit 0이고, `config_hash`·`run_result_hash`·환경 외 Manifest 전체가 `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_dry_run1/runs/*-a`와 같습니다.
  - 예: cal-na `669f8dc9…78b4ff`, dom-sa-ak `9a6d3f63…32966a`, stop-na `0ebd5c36…9bed05`.
  - 재현 스크립트를 이번에 저장소에 넣었습니다: [`tools/b1-drypilot-repro.ps1`](../../tools/b1-drypilot-repro.ps1). 직전 라운드의 근거 공백을 해소합니다.
- **실행 코드 변경 범위:** 게임 실행 코드(`GameExecutor`·`SimRunner`·`Official`·Replay)는 바꾸지 않았습니다.
  - `PathSafety.Pin`은 바꾸지 않았고, `Open`에 `createRoot` 경로만 추가했습니다. 공식 실행 출력은 `CreateNew`를 씁니다.
  - JSON 트리 불변화는 값이 같은 복사여서 JCS 바이트를 바꾸지 않습니다.
  - artifact_id는 M(D)에서 제외되므로 A+에 영향이 없습니다.
- **Golden:** 고정 입력·기대값은 바꾸지 않았고 `GoldenTests` 4개가 통과했습니다(5절).

## 7. Git 상태

- HEAD `257403ddae557a54c4013c17705370caeac7a2e0`(변경 없음), **스테이징 0, Commit·Push 없음**
- `git status --porcelain` 41행(추적 수정 14), `--untracked-files=all` 163행(verify 직후 측정)
- 이번 라운드에서 바꾼 파일:
  - 제품·도구 코드:
    - `tools/StarPlayer.SimHarness/Evidence/StrictJson.cs`·`Archive.cs`·`EvidenceWriter.cs`
    - `tools/StarPlayer.SimHarness/PathSafety.cs`
    - `tools/StarPlayer.EvidenceVerifier/Verifier.cs`·`Certification.cs`·`TestProfiles.cs`
  - 시험:
    - `tests/StarPlayer.EvidenceVerifier.Tests/T11FixR2Tests.cs`(신규)
    - `tests/StarPlayer.EvidenceVerifier.Tests/T11CertificationTests.cs`·`Decision3VerifierTests.cs`·`VerifierTests.cs`·`TestOnlyWorld.cs`·`TestOnlyTrust.cs`
    - `tests/StarPlayer.SimHarness.Tests/Evidence/ArchiveTests.cs`·`DominanceOfficialTests.cs`(`SeedFile`의 선택 인자만)
    - `tests/StarPlayer.Infrastructure.Tests/OfficialPzl6VerificationTests.cs`
  - 도구: `tools/b1-drypilot-repro.ps1`(신규)
  - 문서:
    - `docs/tasks/B1_T11_FIX_R2_TASK.md`, `docs/tasks/README.md`(색인 1행)
    - `docs/reports/B1_T11_FIX_R2_REPORT.md`·`B1_T11_FIX_R2_RED.log`·`B1_T11_FIX_R2_VERIFY_LOG.md`
    - `docs/reports/B1_IMPLEMENTATION_LOG.md`, `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03F_B1_Review.md`

## 8. 운영 Gate와 승인 출처 확인 잔여 사항 (변동 없음 + 추가 1)

- **운영 Gate(미실시·미검증, PASS 아님):** 보호 앵커 디렉터리 설치·ACL 실측, AT-1~AT-14r, AT-4/P-1 정식 측정, N27-d·e·f, 운영 Environment Profile 승인, N12 비트 Golden·witness_digest 승인값.
- **승인 출처:** OWNER 결정 3과 이번 R2 도출 선택("승인 Pin 기준")은 채팅으로 받은 지시이며, 원문 사본은 저장소에 없습니다. OWNER 확인 대상입니다.
- **Codex 원문:** 2차 재검수 보고서 원문은 저장소에 없습니다. 받으면 보존·대조합니다.
- **남은 위험(확인 요청):**
  - 스냅샷 결속은 프로세스 내부(`ConditionalWeakTable`)입니다. Reflection은 범위 밖입니다.
  - REGRESSION_PROOF는 "검증기 자신의 실행과 바이트 동일"을 요구하므로, 제출자는 같은 승인 환경 결과를 내야 합니다. 이것이 §12.3 2의 해석입니다.
  - INPUT_BUNDLE의 범위를 두 Profile로 한정했습니다. 실행 허가 기록은 승인 Pin이 없어 넣지 않았습니다.

## 9. Codex 표적 재검수 범위

1. R1: `StrictJson` JArray/JObject, `Verifier.Frozen/Snapshot`, `Certification.Bound` 이후의 모든 사용처
2. R2: `Verifier.RequiredCounts`의 필수 원본 규칙, `Certification.RequireOriginals`, `VerifyRegressionProof`, `TestProfiles.RegressionProof`, 시험 Fixture의 원본 보존
3. R3: `EvidenceWriter.WriteDocument` ID, `Certification.PhaseOriginals`, 3절 양성 경로
4. R4: `PinnedDirectory.Open(createRoot)`, `Archive.WriteNew`
5. 4절 기대값 변경 4건, 3절 Fixture 변경(Seed 기준값)
6. 기존 RESOLVED 항목(T11-02·05·06·07·08)과 OWNER 결정 1~7 회귀 없음

## 10. 보고서 작성 후 재확인

- verify 이후 이 보고서, 구현 로그, `CURRENT_STATE.md`, Dashboard, 리뷰 초안, `docs/tasks/README.md`를 고쳤습니다. 코드와 시험은 바꾸지 않았습니다.
- 그 뒤 다시 실행한 확인:
  - `verify.ps1` D1과 같은 규칙의 문서 링크 검사(루트·`docs/`·`Star/` `*.md`, 코드 블록 제외): 191개 파일, 깨진 링크 0
  - `sha256sum -c docs/contracts/b1/B1-T00_BASELINE.sha256`: 5개 OK
  - 스테이징 0
- 코드·시험 결과는 5절 verify가 근거이며, 문서만 바뀐 뒤 전체 verify를 다시 실행하지는 않았습니다.
