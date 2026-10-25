# Star Player B1 — T11 2차 FIX_REQUIRED 수정 TASK (R1~R4)

- 작성: Claude Code, 2026-10-25. 근거: OWNER 채팅 지시 "Star Player — B1 T11 2차 FIX_REQUIRED 수정 지시"(2026-10-25 수신)와 R2 도출 방식 OWNER 선택("승인 Pin 기준", 같은 날 채팅 응답).
- 범위: Codex 두 번째 T11 표적 재검수의 남은 Findings **R1~R4만**. 새 설계·승인 계약 변경 없음.
- 루트 `TASK.md`는 NSC-6 OWNER 증거(D3 보호)이며 이 문서가 대신하지 않습니다. 진행 상태는 [B1_IMPLEMENTATION_LOG.md](../reports/B1_IMPLEMENTATION_LOG.md)와 별도 보고서 `docs/reports/B1_T11_FIX_R2_REPORT.md`에 남깁니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 0. 기준 상태 (작성 시점 실측)

| 항목 | 값 |
|---|---|
| Git HEAD | `257403ddae557a54c4013c17705370caeac7a2e0` (스테이징 0, Commit·Push 없음, porcelain 38행) |
| 승인 계약 원본·보존본 | `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64` |
| 루트 `TASK.md` | `ecbc0859c809c68151c93f92058c31a7ab0b84ce8d8cc9a4a25512b7dd06f825` |
| 기준본 목록 `B1-T00_BASELINE.sha256` | `a9865a2f7db4bfe49ceea1e711528fb186d786750f4dde1b8ce74e948d7a9430` (계약 2·TASK.md·PDF 2 = 5개) |
| OWNER 결정 1 / 2 / 3 | `af7142e5…5dcc` / `fb469705…8d51` / `bcb7bee7…35e0` (변경 금지) |
| 직전 전체 verify | 2026-10-25 08:37:27, `GATE: PASS`, 3,026 PASS — **이번 결과로 재사용하지 않음** |

**Codex 원문:** 두 번째 재검수 보고서 원문은 저장소에 없고 OWNER 지시에는 R1~R4 제목과 요구만 있습니다. 아래 "지적·재현 경로"는 그 요구와 현재 코드를 대조해 Claude가 다시 추적한 것입니다. Codex 원문을 받으면 `docs/reports/`에 그대로 보존하고 대조합니다.

## 1. R1 MAJOR — 검증 완료 객체 내부 가변성

- **지적·재현 경로:** `VerifiedFamily`는 레코드 동일성(`ConditionalWeakTable`)으로만 결속됩니다. 그 내부는 가변입니다. `EvidencePayload.Canonical`은 `byte[]`, `SourceFiles`·`PzlUnits`는 `List<JObject>`, `JArray.Items`·`JObject.Members`는 생성자에 받은 목록을 그대로 노출합니다. 따라서 `(List<JObject>)v.SourceFiles`, `((KeyValuePair<…>[])o.Members)[i]`, `v.Payload.Canonical[0]` 변경이 같은 인스턴스에 그대로 반영됩니다. `Certification.Issue`는 그 바뀐 바이트·목록에 검증 당시 A+를 붙여 발급합니다.
- **계약:** §20.2 1·3·4(같은 스냅샷 바이트, 미검증 값을 Hash에 넣지 않음, A+·원본 목록·Canonical Payload 결속), §21 보존 규칙.
- **수정 파일·함수:**
  - `tools/StarPlayer.SimHarness/Evidence/StrictJson.cs`: `JArray`·`JObject` 생성자가 입력을 복사해 읽기 전용 배열로 보관합니다. JSON 트리 전체가 불변이 됩니다.
  - `tools/StarPlayer.EvidenceVerifier/Verifier.cs`: `VerifyFamily`가 검증 종료 시점의 **불변 스냅샷**(Canonical 사본·A+·문서·Unit PZL·#6·Payload·SourceFiles의 JCS 바이트)을 내부 표에 보관합니다. `VerifiedFamily`의 목록은 읽기 전용 배열입니다.
  - `tools/StarPlayer.EvidenceVerifier/Certification.cs`: 발급 경계에서 공개 객체와 스냅샷을 대조하고, 다르면 `Evidence.not_certifiable`(`Certification.result_mutated`)로 거부합니다. 같으면 **스냅샷에서 새로 파싱한 사본만** 써서 Canonical·A+·SourceFiles·Inventory를 도출합니다.
- **선행 의존:** 없음. R2·R3이 이 스냅샷 위에서 원본 목록을 만듭니다.
- **Red/Green 시험** (`tests/StarPlayer.EvidenceVerifier.Tests/T11FixR2Tests.cs`):
  - `Canonical[0]` 변경 → 거부
  - `SourceFiles`에 추가·제거 시도 → `NotSupportedException`이고 발급 결과는 검증 당시 목록
  - Raw SHA 변경 시도(`Members` 교체) → 같은 처리
  - 중첩 `JObject`/`JArray` 변경 → 같은 처리
  - 발급된 영수증의 CANONICAL_PAYLOAD 바이트·A+·source_files = 검증 스냅샷
  - 수정 전 Red: 바뀐 바이트가 발급되는 것을 확인합니다.

## 2. R2 MAJOR — Archive 필수 ORIGINAL 완전성

- **지적·재현 경로:** 현재 필수 검사(`Verifier.RequiredCounts`)는 Unit별 UNIT_PZL·GAME_EVENTS·GAME_RESULTS·SEED_MANIFEST·UNIT_MANIFEST와 PZL6만 봅니다. APPROVAL_RECORD·BUILD_PROVENANCE·INPUT_BUNDLE·REGRESSION_PROOF가 없어도 통과합니다. `Receipts.CheckInventory`는 영수증의 ORIGINAL 목록과 RAW_INVENTORY가 **서로** 같은지만 봅니다. 따라서 양쪽에서 동시에 제거하면 통과합니다.
- **계약:** §21 표(APPROVAL_RECORD 승인 버전 1, BUILD_PROVENANCE build ID별 1세트, INPUT_BUNDLE 소비 입력 파일별, PREFLIGHT Family·Unit별 필요 기록, ATTESTATION Manifest 내부 허용, REGRESSION_PROOF 승인 검사 프로파일별 1세트), §20.2 4·7, §12.3 2.
- **도출 규칙 (OWNER 선택 "승인 Pin 기준"):** 필수 원본은 신뢰된 승인 기록과 세션이 실제로 쓴 바이트에서 도출합니다. 제출 목록에서 도출하지 않습니다.

  | 범위 | 역할 | 개수 | 결속 |
  |---|---|---|---|
  | Family/Dominance 문서 | APPROVAL_RECORD | 1 (unit·game null) | raw_sha256 = 세션 승인 기록 Raw SHA |
  | 〃 | BUILD_PROVENANCE | 1 | = 승인 기록 `build_provenance.raw_sha256` (세션이 기록부에서 읽어 SHA 확인한 원문) |
  | 〃 | INPUT_BUNDLE | 정확히 2 | {`environment_profile.raw_sha256`, `pzl_test_profile.raw_sha256`} 집합과 같음 |
  | 〃 | PREFLIGHT | 0 | 기존 Preflight는 파일을 쓰지 않고 결과 Attestation이 UNIT_MANIFEST 안에 있습니다(§21 "Manifest 내부 허용", Unit별 UNIT_MANIFEST 필수가 이를 강제). 별도 PREFLIGHT 참조는 거부합니다. |
  | 〃 | REGRESSION_PROOF | 0 | Phase 입력으로만 받습니다. |
  | Phase | REGRESSION_PROOF | 정확히 1 | 승인 PZL Test Profile의 TestEvidence 8건. 바이트 = 검증기 자신의 Test Profile 실행 결과로 만든 문서(§12.3 2). 형식 `simharness-regression-proof-v1`(신규 1개). |
- **수정 파일·함수:**
  - `Verifier.RequiredCounts`: 위 Family 범위 규칙을 적용합니다(`archive_invalid`, `Archive.required_original`).
  - `Certification.Issue`: 세션 신뢰값(`Trust.Approval.RawSha256`·`Trust.Build.Sha`·`Trust.Environment.Sha`·`Trust.TestProfile.Sha`)으로 영수증 ORIGINAL source_files를 다시 검사합니다(`not_certifiable`, `Certification.required_original`).
  - `Certification.VerifyRegressionProof`(신규, 검증 결과 객체 `VerifiedRegressionProof`): `IssuePhase`가 이 객체를 받습니다.
  - `TestProfiles.RegressionProof`(신규): 형식 생성기입니다.
  - 시험 Fixture `TestOnlyWorld`·`TestOnlyTrust`·`Production`: 세계 기본 승인 기록이 실제 Build·Environment·Test Profile 바이트를 Pin하고, 생산자가 네 원본을 보존합니다. **누락을 허용하도록 검사를 약화하지 않습니다.**
- **선행 의존:** R1(스냅샷), R3(Phase 합집합에서 공유 원본 처리).
- **Red/Green 시험:**
  - APPROVAL_RECORD 누락
  - BUILD_PROVENANCE 누락
  - 승인 원본을 문서 참조와 Inventory에서 동시에 제거
  - Build 원본을 문서 참조와 source_files에서 동시에 제거
  - INPUT_BUNDLE 누락·다른 SHA
  - PREFLIGHT 참조 추가
  - Phase REGRESSION_PROOF 누락·불일치
  - 정상 ORIGINAL 전체 양성

## 3. R3 MAJOR — Phase 원본 합성 시 Artifact ID 충돌

- **지적·재현 경로:** `EvidenceWriter.WriteDocument`가 artifact_id를 역할 이름으로 만듭니다. 그래서 Calibration과 Integration 문서가 모두 `family-result`가 됩니다. `Certification.IssuePhase`는 세 Family의 SourceFiles를 `GroupBy(artifact_id).First()`로 합칩니다. 그 결과 Integration 문서 원본이 Phase source_files·RAW_INVENTORY에서 조용히 빠집니다.
- **계약:** §2.5 ArtifactRef `artifact_id` "목록 내 고유", §20.1 SourceFile 중복 거부, §21 RAW_INVENTORY "검증 스냅샷별 1".
- **수정 파일·함수:**
  - `EvidenceWriter.WriteDocument`: ID를 Archive 내 고유한 파일 이름(확장자 제외)에서 만듭니다.
  - `Certification.IssuePhase`: `GroupBy…First` 대신 `PhaseOriginals`로 합칩니다.
    - 같은 ID는 7필드가 완전히 같고, 역할이 검증 집합 범위(APPROVAL_RECORD·BUILD_PROVENANCE·INPUT_BUNDLE)일 때만 하나로 합칩니다.
    - 그 밖의 같은 ID(다른 경로·SHA·내용, 또는 Family 범위 역할의 재사용)와, 서로 다른 ID의 같은 경로는 `archive_invalid`(`Phase.artifact_id_conflict` / `Phase.path_conflict`)로 거부합니다.
- **선행 의존:** R1.
- **Red/Green 시험:**
  - 정상 세 Family 합집합 보존
  - Calibration/Integration 같은 ID 충돌(수정 전 Red: 원본 1개 유실)
  - 같은 ID·다른 SHA
  - 같은 ID·다른 경로
  - 한 Family 원본 유실 검사(Phase ORIGINAL ⊇ 각 Family SourceFiles)
  - Phase RAW_INVENTORY = Phase ORIGINAL source_files
  - **정상 세 Family로 Phase 영수증 발급 양성 경로**
- **양성 경로 Fixture:** 현재 TEST_ONLY 세계의 Dominance는 Gate가 `NOT_EVALUATED`라 발급 대상이 아닙니다(§19). 양성 Phase 시험에는 Gate가 PASS 또는 FAIL인 셀당 2경기 시험 Seed를 씁니다(시험 전용 Seed 기준값만 바꾸며 엔진·Hash·평가식은 변경 없음).

## 4. R4 MINOR — Archive 루트 생성 전 경로 안전성

- **지적·재현 경로:** `Archive.WriteNew`가 경로 검사(`PinnedDirectory.Open`) 전에 `Directory.CreateDirectory(root)`를 실행합니다. 부모가 Junction이면 경로를 따라가 **외부에 디렉터리를 먼저 만들고** 나서 거부합니다.
- **계약:** §2.5 "실제 접근에는 핸들·Reparse Point 검증", §21 보존 규칙. B1-T11-04 핸들 사슬 규범을 유지합니다.
- **수정 파일·함수:**
  - `tools/StarPlayer.SimHarness/PathSafety.cs`: `PinnedDirectory.Open(root, segments, create, createRoot)`. `createRoot`이면 볼륨 루트부터 고정한 부모 핸들 기준으로 루트 구성 요소도 상대 생성합니다(링크 거부, FILE_SHARE_DELETE 없음).
  - `Archive.cs` `WriteNew`: `Directory.CreateDirectory` 제거 → `createRoot: true`.
- **선행 의존:** 없음.
- **Red/Green 시험** (`ArchiveTests`):
  - 부모 Junction 아래 루트 생성 → `Archive.reparse_point`, Junction 대상에 새 디렉터리 없음(수정 전 Red)
  - 정상 루트 생성·사용
  - 기존 `T11_04_*` 3개(파일 생성 후 부모 교체, 읽기, 영수증 재시도 읽기) 유지

## 5. 유지할 해결 사항

T11-02·05·06·07·08과 OWNER 결정 1~7(GAME_RESULTS, PZL6 20 JSON, Production 상태·Fact 비교 포함)은 회귀가 없어야 합니다. 기존 GameResultHash·RunResultHash·ConfigHash·Attestation Hash·RNG 소비·Replay 순서는 바꾸지 않습니다. artifacts는 A+ 의미 Hash(M(D))에서 제외되므로 ID 변경이 A+를 바꾸지 않습니다.

## 6. 완료 기준

1. R1~R4마다 수정 전 Red와 수정 후 Green을 실제 실행 결과로 기록합니다.
2. 정상 Family 영수증과 정상 Phase 영수증이 실제로 발급됩니다(TEST_ONLY, `TestOnlyRegistered`).
3. 다음을 실행합니다.
   - Release 빌드(경고 0)
   - 모든 시험 프로젝트
   - Golden 4개
   - T11-01~08·OWNER 결정 7건 회귀
   - Dry Pilot 9 Run 재현
   - `tools/verify.ps1` `GATE: PASS`
   - D3 기준 SHA 5개 OK
4. 보고서·링크 문서를 고친 뒤 문서 검사와 SHA를 다시 확인합니다.
5. B1 PASS를 자동 처리하지 않습니다. Codex READ-ONLY 재검수와 OWNER 최종 수용을 기다립니다.

## 7. 작업 제한

- 다음 원본은 변경하지 않습니다: 승인 계약, OWNER 승인·결정 기록, 루트 `TASK.md`, PDF, D3 기준 SHA.
- Commit·Push를 하지 않습니다.
- Official Execution과 Final Execution Freeze를 활성화하지 않습니다.
- 운영 Gate(보호 앵커 디렉터리, AT-1~AT-14r, AT-4/P-1, N27-d·e·f, 운영 Environment Profile, N12 승인값)는 PASS로 처리하지 않습니다.

## 8. 작업 재개 방법

1. `git status`로 기준 상태를 확인하고, 이 문서 0절과 `sha256sum -c docs/contracts/b1/B1-T00_BASELINE.sha256`을 대조합니다.
2. `docs/reports/B1_IMPLEMENTATION_LOG.md`의 "T11 2차 수정" 절에서 마지막 완료 단계를 확인합니다.
3. 순서: R4(독립) → R1 → R3 → R2 → Phase 양성 Fixture → 전체 verify → 보고서.
4. 단계별 확인은 `dotnet test <project> -c Release --filter <class>`로 하고, 최종 판단은 `tools/verify.ps1`만으로 합니다.

## 9. Codex 재검수 제출 자료

`docs/reports/B1_T11_FIX_R2_REPORT.md`에 다음을 모읍니다.

1. R1~R4 수정 내용과 코드 위치
2. 수정 전·후 재현 시험
3. 정상 Certification·Phase 영수증 양성 경로
4. 필수 ORIGINAL 완전성
5. RAW_INVENTORY·SourceFiles 결속
6. 전체 시험·Golden·verify 결과
7. Hash·RNG·Replay 불변성
8. Git HEAD·변경 파일·스테이징
9. 운영 Gate와 OWNER 승인 출처 확인 잔여 사항(결정 3·R2 도출 선택이 채팅 기반이라는 점 포함)
