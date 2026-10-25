**T11 최종 재검수 판정: `FIX_REQUIRED`**

R1·R3·R4는 해결됐습니다. R2는 승인·Build 원본 및 Regression Proof 결속을 보완했지만, **INPUT_BUNDLE을 프로필 2개로 제한하고 PREFLIGHT를 전부 거부하는 규칙이 승인 계약 §21과 일치하지 않습니다.** 따라서 B1 코드 구현의 최종 수용은 아직 불가합니다.

**1. 기준 상태와 검수 범위**

- HEAD: `257403ddae557a54c4013c17705370caeac7a2e0` — 일치
- 계약 원본·승인 보존본 SHA: `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64` — 일치
- D3 보호 파일 5개 — 기준 목록과 모두 일치
- 작업 트리: 추적 수정 14개, 미추적 150개, Staging 0
- 신규 미추적 Production·테스트·보고서·재현 스크립트도 읽었습니다.
- 파일 수정, 빌드·테스트·재현 실행, Git 변경은 하지 않았습니다.

**2. R1~R4 개별 판정**

| 항목 | 판정 | 확인 결과 |
|---|---|---|
| R1 검증 결과 스냅샷 | **RESOLVED** | 내부 사본 보관, 공개 결과와 발급 직전 대조, 발급에는 내부 스냅샷에서 복원한 사본 사용 |
| R2 필수 ORIGINAL 완전성 | **NOT_RESOLVED** | 승인·Build·프로필 누락 및 Phase 증명 누락은 차단하지만, 필수 입력·PREFLIGHT 범위를 축소함 |
| R3 Phase 원본 합집합 | **RESOLVED** | 충돌을 조용히 제거하지 않고 거부하며, 정상 세 Family→Phase 발급 시험 존재 |
| R4 Archive 루트 생성 | **RESOLVED** | 경로 기반 선행 생성 제거, 검증·고정한 부모 핸들 기준 생성으로 변경 |

R1은 [Verifier.cs:127](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Verifier.cs:127)의 `Frozen`과 [Snapshot:142](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Verifier.cs:142)에 연결돼 있습니다. Canonical 배열이 바뀌면 `Certification.result_mutated`로 거부하며, 정상 발급은 보관한 Canonical·A+·문서·SourceFiles로 진행합니다. [StrictJson.cs:68](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/Evidence/StrictJson.cs:68)의 배열·객체 컬렉션도 복사 후 읽기 전용으로 보관합니다.

R3은 [EvidenceWriter.cs:66](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs:66)에서 문서 ID를 역할 이름 대신 경로로 구분합니다. [Certification.cs:252](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Certification.cs:252)는 동일 ID의 다른 경로·SHA, 동일 경로의 다른 ID를 거부합니다. 공유 원본 병합은 지정된 검증 집합 역할과 동일 참조에 한정됩니다. [OfficialPzl6VerificationTests.cs:80](E:/Madegame/Star_player/tests/StarPlayer.Infrastructure.Tests/OfficialPzl6VerificationTests.cs:80)에는 Family 영수증 3개, Regression Proof, Phase 영수증·Inventory까지 검증하는 양성 경로가 있습니다. 다만 이 시험이 사용하는 원본 집합 자체의 완전성은 R2 문제의 영향을 받습니다.

R4는 [PathSafety.cs:123](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/PathSafety.cs:123)에서 기존 상위 경로를 핸들로 고정한 뒤 누락된 구성요소를 생성합니다. 생성 전에 Junction을 통과하는 기존 문제가 제거됐습니다. [ArchiveTests.cs:192](E:/Madegame/Star_player/tests/StarPlayer.SimHarness.Tests/Evidence/ArchiveTests.cs:192)는 거부뿐 아니라 **외부 디렉터리에 아무것도 생성되지 않았음**과 정상 신규 루트의 생성·읽기를 단언합니다.

**3. 남은 MAJOR — R2의 필수 원본 범위 축소**

**R2-A: INPUT_BUNDLE을 실제 소비 입력이 아닌 프로필 2개로 고정**

- 계약: [승인 계약 §21:1697](E:/Madegame/Star_player/B1-T00_FINAL_CONTRACT_DRAFT.md:1697)은 INPUT_BUNDLE을 **“소비 입력 파일별”**로 보존하도록 정합니다.
- 구현: [Verifier.cs:361](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Verifier.cs:361), [Certification.cs:151](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Certification.cs:151)는 Environment Profile과 PZL Test Profile의 SHA 두 개만 허용합니다.
- 실제 실행은 [SimHarness.csproj:13](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/StarPlayer.SimHarness.csproj:13)의 튜닝·규칙 JSON을 내장하고, `HarnessData.LoadTuningV4`와 `LoadRules`로 소비합니다. 이 원본들은 현재 필수 INPUT_BUNDLE 집합에 포함되지 않습니다.

**재현 경로:** 현재 양성 Fixture처럼 두 프로필만 보존하면 영수증까지 도달합니다. 반대로 실제 소비한 튜닝·규칙 원본을 INPUT_BUNDLE로 추가하면 SHA 집합이 두 개와 달라져 `Archive.required_original`로 거부됩니다.

이는 단순 누락 검사가 아니라 **계약에 필요한 추가 원본도 금지하는 제한**입니다. 입력의 의미 Hash나 Build Provenance 참조가 있다는 사실만으로 원본 바이트 보존 의무가 대체되지는 않습니다.

**최소 수정 요구:** 실제 검증·실행에서 소비한 입력과 보존 역할을 대조해 필수 원본 집합을 도출하고, 두 프로필로 고정한 검사를 수정해야 합니다. 범위를 축소하려는 정책이라면 기존 계약과의 차이를 명시한 OWNER 결정이 필요합니다.

**R2-B: Attestation 내부 보존 허용을 PREFLIGHT 면제로 확대**

- 계약 [§21:1699](E:/Madegame/Star_player/B1-T00_FINAL_CONTRACT_DRAFT.md:1699)는 Family·Unit별 필요한 PREFLIGHT 기록을 요구합니다.
- [§21:1716](E:/Madegame/Star_player/B1-T00_FINAL_CONTRACT_DRAFT.md:1716)의 Manifest 내부 허용은 **Attestation**에 관한 규정입니다.
- 현재 코드는 `PREFLIGHT` 기대 집합을 빈 값으로 고정해 모든 PREFLIGHT 참조를 거부합니다.

Family/Dominance 색인에는 일부 Preflight 집계가 DIAGNOSTICS로 보존됩니다. 따라서 Preflight 정보가 전혀 없다는 뜻은 아닙니다. 그러나 **Unit별 검사 기록과 §21의 역할·범위를 충족했다는 검증 없이 Attestation으로 대체**하고 있습니다. Attestation과 `PreflightReport.Checks`는 동일한 자료가 아닙니다.

[신규 테스트:90](E:/Madegame/Star_player/tests/StarPlayer.EvidenceVerifier.Tests/T11FixR2Tests.cs:90)도 PREFLIGHT 추가 거부를 기대하므로, 이 부분의 PASS는 계약 충족을 입증하지 못합니다.

**최소 수정 요구:** 필요한 Family·Unit Preflight 기록과 보존 위치를 명시적으로 결속해야 합니다. 기존 파일 안에 포함하는 경우에도 해당 기록의 존재·범위·원본 결속을 검증해야 하며, 현재의 일괄 0개 규칙은 제거해야 합니다.

두 항목은 **R2 수정 과정에서 생긴 동일한 완전성 문제의 잔여 MAJOR**로 분류합니다. 별도의 신규 MINOR 결함은 확인하지 못했습니다.

**4. 신규 형식·Seed·기대값 변경 판정**

| 대상 | 판정 | 근거 |
|---|---|---|
| `simharness-regression-proof-v1` | **COMPLIANT** | §12.3의 TestEvidence를 감싸는 저장 형식이며, 승인 프로파일·현재 세션의 독립 실행 결과와 바이트 대조함 |
| INPUT_BUNDLE 프로필 2개 제한 | **NON_COMPLIANT** | §21의 소비 입력 파일별 보존 범위를 충족하지 않음 |
| TEST_ONLY Seed base `991000` | **COMPLIANT** | 인증 양성 경로 확보용 Fixture 변경. 계산식·공식 Seed·기존 기본 Fixture를 변경하지 않음 |

Regression Proof는 [Certification.cs:196](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Certification.cs:196)에서 세션 동일성, 역할, Raw SHA, 검증기 실행 결과와의 바이트 동일성을 검사합니다. 이 저장용 외피 자체를 별도 OWNER 정책 변경으로 볼 근거는 발견하지 못했습니다.

Seed 변경은 기존 분산 0으로 인한 `NOT_EVALUATED`를 숨겨 공식 통계 결과를 만드는 경로가 아닙니다. TEST_ONLY 양성 시험의 입력을 바꿨으며, 공식 실행 준비 상태는 유지됩니다. 다만 이것을 공식 표본 수나 통계 적정성의 증거로 사용하면 안 됩니다.

기대값 4건의 변경은 새 양성 Fixture에서 실제로 도달하는 거부 지점과 맞습니다. `NOT_EVALUATED`·`INCOMPLETE` 인증 거부는 [CertificationAndEnvironmentTests.cs:39](E:/Madegame/Star_player/tests/StarPlayer.EvidenceVerifier.Tests/CertificationAndEnvironmentTests.cs:39)에, 표본 부족 허용 없는 발급 거부는 `T11CertificationTests`에 남아 있습니다. 변경 자체를 검증 완화로 판정하지 않습니다.

**5. 검증 증거와 독립 확인의 한계**

직접 확인한 것은 **기존 파일·코드·로그·Hash 및 산출물 비교**입니다. 시험을 직접 실행한 것은 아닙니다.

- U1~U6 원시 로그 합계: **3,032 PASS / 0 FAIL / 0 SKIP**
- Verify 로그 SHA: `2c6de56f…031d` — 보고서와 일치
- `verify-result.json` SHA: `31e341ed…c847` — 일치
- RED 로그 SHA: `81a470b0…4368` — 일치
- 현재 Release SimHarness DLL SHA: `96baca7e…0902` — 보고서와 일치
- 보존된 Dry Pilot 재현 산출물 9개: 기존 Archive와 ConfigHash·RunResultHash·환경 제외 Manifest 모두 일치

[재현 스크립트](E:/Madegame/Star_player/tools/b1-drypilot-repro.ps1)의 입력 파일, DLL 경로, `dry` 명령과 비교 대상을 확인했습니다. **스크립트는 실행하지 않았습니다.** 산출물 일치는 독립 확인했지만, 이번 Codex가 그 DLL로 재실행한 결과는 아닙니다.

RED 증거의 범위도 구분합니다.

- R1: SourceFiles 변경에서 먼저 실패했으므로 Canonical 변경의 별도 RED 실행까지 증명하지 않습니다.
- R2: 승인 원본 없이 인증됐던 실패를 확인했습니다.
- R3: 옛 ID 규칙을 되돌린 결함 주입 결과입니다.
- R4: 외부 디렉터리 생성 반례의 실패를 확인했습니다.
- 신규 합집합·Regression Proof 세부 시험은 수정 후 작성됐습니다.

수정 영향 범위에서 기존 Hash 계산식·RNG 소비·기본 Replay 정책을 바꾼 정황은 발견하지 못했습니다. 다만 미커밋 신규 파일의 과거 전체 스냅샷이 없으므로, **“코드 변경 없이 보고서만 마지막에 수정됐다”와 Golden의 이전 바이트 불변성 전체를 독립적으로 증명한 것은 아닙니다.**

**6. 승인 출처·운영 Gate·최종 수용**

OWNER 결정 3과 R2의 “승인 Pin 기준” 지시 원문은 저장소에 없고, Claude가 작성한 기록만 확인됩니다. 이를 실제 OWNER 원문과 독립 대조했다고 주장하지 않습니다. 특히 **Pin 기준 도출 승인과 입력 범위 축소 승인은 동일하지 않습니다.**

다음 운영 Gate는 계속 미실시·미검증입니다.

- 보호 앵커 디렉터리 설치·ACL
- AT-1~AT-14r 및 정식 AT-4/P-1
- N27-d/e/f 계정별 실측
- 운영 Environment Profile
- N12 비트 Golden·witness_digest
- 검증기 바이너리 자체 SHA와 Test Profile 실행 경로의 잔여 신뢰 문제

| 최종 항목 | 판정 |
|---|---|
| T10 수용 기준 | **부분 충족** — 제출 시험 증거는 확인했지만 필수 원본 완전성은 미충족 |
| B1 코드 구현 최종 수용 | **불가 — R2 수정 필요** |
| T11 최종 재검수 | **FIX_REQUIRED** |
| OWNER 추가 확인 | 승인 원문과 저장소 기록의 일치, 원본 범위 축소를 의도했다면 명시적 계약 결정 |

Claude가 우선 처리할 사항은 **INPUT_BUNDLE 전체 소비 입력 결속과 PREFLIGHT 보존·검증 범위 정정**입니다. 이미 해결된 R1·R3·R4를 다시 설계할 필요는 없습니다.

Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

미실행: 빌드·테스트·Dry Pilot·Windows 운영 시험. 파일·Git 변경 없이 종료합니다.  
잔여 위험: 현재 시험 PASS만으로 §21 원본 완전성이나 운영 인증 준비가 보장되지는 않습니다.