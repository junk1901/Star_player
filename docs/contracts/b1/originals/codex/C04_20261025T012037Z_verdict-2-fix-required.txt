**T11 최종 재검수 판정: `FIX_REQUIRED`**

수정된 인증 세션, 자식 영수증 결속, GAME_RESULTS·PZL6 검증, 환경 프로필 검사, Reader 클라이언트와 Pipe 실패 처리를 확인했습니다. 그러나 **검증 후 데이터 변경, 필수 원본 증거 누락, Phase 원본 목록 손실**이 남아 B1 코드 구현을 최종 수용할 수 없습니다.

이번 검수에서는 파일 변경이나 빌드·테스트·Verify·시뮬레이션을 실행하지 않았습니다. 아래 재현 경로는 실제 코드 추적에 근거하며, 공격 시험을 직접 실행했다는 뜻은 아닙니다.

**1. 기준 상태와 증거 확인**

| 항목 | 직접 확인 결과 |
|---|---|
| HEAD | `257403ddae557a54c4013c17705370caeac7a2e0` |
| 브랜치 / Staging | `main` / 0건 |
| 작업 트리 | 추적 수정 13개, 미추적 파일 145개 |
| 계약 크기 | 175,154바이트 |
| 계약 SHA-256 | `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64` |
| D3 보호 기준 | 계약·승인 보존본·TASK.md·PDF 2개 모두 기준 SHA와 일치 |
| 검수 종료 시 | HEAD·Staging·D3 기준 유지 |

실제 파일 단위 변경 항목은 158개입니다. 보고서의 과거 집계와 현재 집계를 구분했으며, 일반 `git diff`에 나타나지 않는 신규 구현·시험 파일도 검토했습니다.

**2. T11-01~08 개별 판정**

| Finding | 판정 | 확인 결과 |
|---|---|---|
| T11-01 인증 경로 결속 | **NOT_RESOLVED** | 세션·환경·Replay·PZL 적격성은 연결됐지만, 검증 결과 내부의 가변 데이터가 발급 시 그대로 사용됩니다. 아래 R1 |
| T11-02 Phase 자식 결속 | **RESOLVED — 해당 검사 범위** | 서로 다른 3개 ID, 역할·순서·검증 집합·승인·RunId·EvidenceId·A+·TEST_ONLY를 실제 발급 경계에서 검사합니다. 다만 이후 원본 목록 합성에 새 결함 R3가 있습니다 |
| T11-03 Archive 전체 검증 | **NOT_RESOLVED** | 하위 참조 재귀 검사와 Replay 스냅샷 사용은 개선됐습니다. §21 필수 원본 역할 검사는 여전히 불완전합니다. 아래 R2 |
| T11-04 경로 경쟁 | **NOT_RESOLVED — 잔여 MINOR** | 파일 생성·읽기·재시도는 핸들 기반으로 개선됐습니다. 루트 디렉터리 생성만 고정 전에 수행됩니다. 아래 R4 |
| T11-05 환경 Pin·Golden | **RESOLVED** | 런타임 파일 정확한 3개와 divisor 1~36의 순서·중복·누락을 Reader와 실제 비교 양쪽에서 검사 |
| T11-06 Reader 전용 클라이언트 | **RESOLVED** | 같은 서버 인증을 사용하며 Get/GetByScope만 공개합니다. `IEvidenceRegistry`가 아니고 Register·인증 발급 경로가 없습니다 |
| T11-07 Revert 처리 | **RESOLVED** | ImpersonateSelf 호출 여부로 Revert 실행을 결정하며, 실패 반환 경로도 포함 |
| T11-08 인스턴스 생성 실패 | **RESOLVED — 코드·제출 시험 범위** | 포화와 생성 실패를 구분하고, 추가 생성 3회 실패 후 오류 기록·중지합니다. 동기 I/O 취소와 스레드 종료 시험도 추가됐습니다 |

주요 근거: [Certification.cs:190](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Certification.cs:190), [ApprovalRecord.cs:71](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/Evidence/ApprovalRecord.cs:71), [ExecutionEnvironment.cs:68](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/ExecutionEnvironment.cs:68), [PipeRegistry.cs:159](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/PipeRegistry.cs:159), [PipeRegistry.cs:318](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/PipeRegistry.cs:318), [PipeRegistry.cs:515](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/PipeRegistry.cs:515).

**3. 남은 Findings**

**R1 — MAJOR: 같은 검증 완료 객체의 내부 변경은 발급 경계가 검출하지 못합니다.**

- [EvidenceHash.cs:8](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/Evidence/EvidenceHash.cs:8): `EvidencePayload.Canonical`은 공개 `byte[]`입니다.
- [Verifier.cs:15](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Verifier.cs:15): 검증 결과가 이 Payload와 컬렉션을 공개합니다.
- [Certification.cs:112](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Certification.cs:112): `Bound`는 객체 동일성과 세션만 확인합니다.
- [Certification.cs:134](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Certification.cs:134): 공개 배열을 Canonical Payload 파일로 저장하면서, target에는 기존 `Payload.Hash`를 사용합니다.

재현 경로는 정상적으로 검증된 적격 결과에서 다음과 같습니다.

```csharp
v.Payload.Canonical[0] ^= 1;
Certification.IssueFamily(session, v);
```

`v` 자체는 동일하므로 세션 검사를 통과합니다. 변경된 바이트의 Raw SHA는 새로 계산되지만 **그 바이트의 A+와 기존 `Payload.Hash`가 같은지는 재검사하지 않습니다.** 결과적으로 영수증 target A+와 보존된 Canonical Payload가 서로 다른 상태로 등록될 수 있습니다.

`SourceFiles`도 실제 `List`로 반환되며, JSON 배열·객체에도 원본 컬렉션을 그대로 보관하는 경로가 있습니다. `with`로 만든 새 객체를 거부하는 시험만으로는 이 변경을 막지 못합니다.

**최소 수정:** 인증 경계가 검증 당시의 변경 불가능한 내부 스냅샷을 소유하도록 하고, 발급할 Canonical 바이트·A+·원본 목록의 일치를 보장해야 합니다. 같은 객체의 배열·컬렉션을 변경하는 반례가 필요합니다.

이는 외부 사용자가 현재 CLI로 인증을 탈취했다는 판정이 아니라, **T11-01이 요구한 도구 내부의 검증→발급 결속이 아직 완성되지 않았다는 판정**입니다.

---

**R2 — MAJOR: §21 필수 원본 역할을 누락한 Archive도 발급 대상이 됩니다.**

[Verifier.cs:313](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Verifier.cs:313)의 `RequiredCounts`는 다음을 검사합니다.

- Unit별 UNIT_PZL·GAME_EVENTS·GAME_RESULTS·SEED_MANIFEST·UNIT_MANIFEST
- Integration의 PZL6 문서 1개와 JSON 20개

그러나 [승인 계약 §21:1695](E:/Madegame/Star_player/B1-T00_FINAL_CONTRACT_DRAFT.md:1695)의 **APPROVAL_RECORD와 BUILD_PROVENANCE 필수 보존·개수**는 검사하지 않습니다. INPUT_BUNDLE·필요 PREFLIGHT·REGRESSION_PROOF의 완전성도 이 검사에 포함되지 않습니다.

실제 시험 생산 경로도 [TestOnlyWorld.cs:179](E:/Madegame/Star_player/tests/StarPlayer.EvidenceVerifier.Tests/TestOnlyWorld.cs:179)에서 Unit 증거·Seed·실행 파일만 묶고, 해당 승인 원본들을 Archive에 넣지 않습니다. 이 세계에서 Family 영수증 발급 성공을 시험합니다.

`CheckInventory`는 **주어진 ORIGINAL 목록과 Inventory의 일치**를 확인하지만, 양쪽에서 동시에 빠진 필수 원본은 발견하지 못합니다. Registry에서 승인 기록을 조회했다는 사실과, 그 원본을 영수증의 검증 스냅샷에 보존했다는 사실은 다릅니다.

**최소 수정:** §21의 필수 역할을 신뢰된 승인·검증 입력에서 도출하여 완전성을 검사하고, 실제 사용한 원본을 `source_files`와 Inventory에 포함해야 합니다. 최소한 APPROVAL_RECORD·BUILD_PROVENANCE 누락은 발급 전에 거부해야 합니다.

---

**R3 — MAJOR: Phase 원본 합성에서 다른 Family 파일이 조용히 삭제됩니다.**

- [EvidenceWriter.cs:66](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs:66)는 Artifact ID를 역할 문자열에서만 만듭니다.
- 따라서 Calibration과 Integration의 공식 문서는 모두 `artifact_id = "family-result"`입니다.
- [Certification.cs:185](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Certification.cs:185)는 세 Family의 원본을 합칠 때 다음 방식으로 처리합니다.

```csharp
GroupBy(artifact_id).Select(g => g.First())
```

정상적인 Calibration·Integration 두 문서도 경로와 Raw SHA가 다른데 같은 ID로 묶입니다. 이후 Phase Inventory와 `source_files`에서 두 번째 문서가 빠집니다. 두 목록을 동일하게 축소하므로 `CheckInventory`도 이를 검출하지 못합니다.

현재 Phase 시험은 적격하지 않은 자식 때문에 이 지점에 도달하기 전에 거부됩니다. 따라서 제출 PASS는 이 정상 발급 분기를 입증하지 않습니다.

**최소 수정:** Phase 범위에서 서로 다른 원본이 고유하게 식별되도록 하고, 동일 Artifact ID의 참조가 다르면 조용히 선택하지 말고 거부해야 합니다. 정상 세 Family의 원본 합집합이 빠짐없이 남는 시험이 필요합니다.

이 문제는 [OWNER 결정 3 §2](E:/Madegame/Star_player/docs/contracts/b1/B1-T00_OWNER_DECISION_3.md:13)의 “필수 ORIGINAL 원본을 빠짐없이 포함”과 충돌합니다.

---

**R4 — MINOR: 경로 고정 전에 루트 디렉터리를 생성합니다.**

[Archive.cs:175](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/Evidence/Archive.cs:175)는 다음 순서입니다.

1. `Directory.CreateDirectory(root)`
2. `PinnedDirectory`로 경로 검증·고정

예를 들어 `root = C:\allowed\link\new-archive`이고 `link`가 외부 디렉터리로 향하는 Junction이면, 첫 단계가 외부에 `new-archive`를 만든 뒤 두 번째 단계에서 거부될 수 있습니다.

**최종 증거 파일의 외부 생성은 핸들 기반 경로에서 차단됩니다.** 남은 영향은 거부 전에 발생하는 외부 디렉터리 생성입니다.

**최소 수정:** 루트 준비도 검증된 부모 핸들 아래에서 수행하거나, 이미 안전하게 준비된 루트만 받도록 해야 합니다. 현재 시험은 고정 이후의 교체를 다루므로 이 선행 생성 경로는 별도 반례가 필요합니다.

**4. OWNER 결정 7건의 구현 적합성**

아래 판정은 **저장소 결정 기록을 규범으로 삼았을 때의 구현 적합성**입니다. 원본 채팅의 승인 여부까지 인증한 것은 아닙니다.

| 결정 | 판정 | 근거 |
|---|---|---|
| 1. violations 생성 단위 | **COMPLIANT** | 경기·원인별 생성, 4필드, null 우선 Ordinal 정렬·중복 제거 및 재관찰 대조 확인. [UnitPzlEvidence.cs:127](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/Evidence/UnitPzlEvidence.cs:127) |
| 2. RAW_INVENTORY | **NON_COMPLIANT — 전체 보존 조건** | 닫힌 Reader·목록 대조는 구현됐으나 필수 원본 누락과 Phase 합성 손실이 남음. R2·R3 |
| 3. witness_digest | **COMPLIANT** | 네 수치와 Holm critical의 BITS64 처리, 240행·생성 순서 확인. [ExecutionEnvironment.cs:87](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/ExecutionEnvironment.cs:87) |
| 4. Production 상태·Fact 비교 | **COMPLIANT** | 실제 최종 GameState를 관찰하고 왕복 Replay와 구조 비교. Production 커밋 Fact·완료 Fact와 비교하며 누락 상태 거부. [Pzl6Verifier.cs:72](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Pzl6Verifier.cs:72) |
| 5. GAME_RESULTS | **COMPLIANT** | 닫힌 Header/Game/End, 실패 경기 포함, 순서·건수·부모 행·재실행 투영·ResultHash 대조 확인. [GameResultsEvidence.cs:40](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/Evidence/GameResultsEvidence.cs:40) |
| 6. 내부 출력 배치 | **COMPLIANT** | `run/<run_id>/…`를 내부 배치로 사용하며 공식 B-4 동결을 대체하지 않음 |
| 7. PZL6 역할·개수 | **COMPLIANT** | 문서 1개＋표본 JSON 20개, GAME_RESULTS와 분리, 표본 ID·Unit·Game·바이트 대조 확인. [Verifier.cs:408](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Verifier.cs:408) |

결정 4의 시험은 Production의 `Stats`·`Final`만 다른 경우, Production 상태 누락, Fact 누락을 실제 비교 함수에 넣어 거부를 확인합니다. 단순히 같은 기대값을 복사해 비교하는 시험만 있는 구조는 아닙니다.

**5. 변경된 시험 기대값과 검증 충분성**

세 변경의 목적 자체는 정당합니다.

1. **UNIT_MANIFEST 누락 원인 코드 변경:** 더 앞선 개수 검사에서 거부되며 상위 오류는 계속 `archive_invalid`입니다. 검증 완화가 아닙니다.
2. **공개 API Reflection 단정 조정:** 검사 전용 `CheckInventory`를 허용하는 것은 타당합니다. 다만 이 시험은 내부 가변성까지 검사하지 않으므로 **R1을 놓칩니다.**
3. **GAME_RESULTS 필수화에 따른 ArchiveTests 변경:** 승인된 결정 5를 반영한 강화입니다.

추가로 필요한 검증은 다음입니다.

- 같은 `VerifiedFamily` 내부 배열·원본 목록 변경 후 발급 거부
- 필수 승인·Build 원본을 양쪽 목록에서 함께 제거한 경우 거부
- 정상 세 Family의 Phase 원본 합집합 보존 및 ID 충돌 거부
- 루트 경로 고정 전 Junction을 통한 디렉터리 생성 방지

현재 [Phase 통합 시험](E:/Madegame/Star_player/tests/StarPlayer.Infrastructure.Tests/OfficialPzl6VerificationTests.cs:65)은 Test Profile 실행과 Phase Payload 검증을 수행하지만, **정상 Phase 영수증 발급 완료는 입증하지 않습니다.** 자식 부적격·누락·중복 거부 시험과 구분해야 합니다.

따라서 **T10 전체 수용 조건은 아직 충족됐다고 판정할 수 없습니다.**

**6. 제출 증거와 독립 확인 범위**

보존 로그를 직접 읽어 다음 합계를 확인했습니다.

| 프로젝트 | PASS |
|---|---:|
| Domain | 327 |
| Simulation | 741 |
| Application | 128 |
| Infrastructure | 1,237 |
| SimHarness | 535 |
| EvidenceVerifier | 58 |
| **합계** | **3,026** |

FAIL·SKIP은 모두 0입니다. U3a·U3b는 Application 부분 재실행이므로 합계에 중복하지 않았습니다.

직접 계산한 SHA도 보고와 일치합니다.

- [최종 Verify 로그](E:/Madegame/Star_player/docs/reports/B1_VERIFY_LOG.md):  
  `725a250ee6f9d8b1700f2389d529e1aa32ea4052ac3e5b78f03f640b67d89244`
- [원시 verify-result.json](E:/Madegame/Star_player/build/verify/verify-result.json):  
  `22f15eddde313261213877858da8f8eb9a3b417dbec85059be3c9618a379d657`
- SimHarness DLL:  
  `13198f07ea87ea4e01d8004cc46812d22382fa62c1bc6d9221fa4692645a2d39`

**이는 Claude 실행 증거의 일치 확인이며 Codex 직접 실행 PASS가 아닙니다.**

Dry Pilot은 기존 9개 Manifest와 재현 폴더의 9개 Manifest를 읽어 `environment_not_hashed`를 제외한 JSON 내용이 모두 같은 것을 확인했습니다. 재현 스크립트는 저장소에서 확인되지 않으므로, 해당 DLL과 명령으로 실제 재생성됐다는 실행 과정 전체를 독립 인증하지는 않습니다.

기존 Hash 계산식·RNG/CAS 소스의 변경은 발견하지 않았습니다. Replay 변경은 승인된 authority 전달·기존 검사 추출·재실행 결과 제공 범위이며, 현재 확인한 diff에서 기본 오류 순서를 바꾸는 추가 변경은 발견하지 않았습니다.

**7. 승인 출처와 남은 운영 Gate**

OWNER 승인·결정 기록은 존재하지만, 이를 작성하는 근거가 된 **원본 채팅 사본은 저장소에 없습니다.** OWNER가 승인 기록·NSC 결정·결정 2·결정 3이 자신의 지시와 일치함을 확인해야 합니다. 이 출처 문제와 위 코드 결함은 별개입니다.

다음은 여전히 미완료입니다.

- 보호 앵커 디렉터리 설치·ACL 실측
- 별도 계정 기반 AT-1~AT-14r
- AT-4/P-1 정식 측정
- N27-d·e·f 계정별 실측
- 운영 Environment Profile 승인
- N12 운영 비트 Golden·witness_digest 승인값

Win32 1307 사전 측정을 AT-4 전체 PASS로 인정하지 않았습니다.

보고서가 이미 밝힌 검증기 바이너리 자기 SHA 대조 부재와 Test Profile의 SHA 확인 후 경로 실행 문제도 **해결 완료로 간주하지 않았습니다.** 이 잔여 사항은 운영 준비도 판단에 계속 남겨야 합니다.

**8. 최종 수용 판단**

- **남은 결함:** MAJOR 3건, MINOR 1건
- **T10 전체 수용:** 아직 불가
- **B1 코드 구현 최종 수용:** 아직 불가
- **T11 최종 판정:** **`FIX_REQUIRED`**
- **OWNER 추가 확인:** 승인 기록의 원문 일치 확인. 발견된 코드 결함을 정책 변경으로 면제할 필요는 없으며, 기존 계약대로 수정하면 됩니다.

우선 R1의 검증 스냅샷 보호, R2의 필수 원본 완전성, R3의 Phase 합집합 보존을 수정하고, R4의 루트 생성 경계를 정리해야 합니다.

Official Execution = **NOT_READY**  
Final Execution Freeze = **PENDING**  
Phase 3 = **INCOMPLETE**

빌드·테스트·공식 실행·Windows 운영 실측은 재실행하지 않았으며, 파일·Git 변경도 하지 않았습니다. 제출된 3,026 PASS만으로 위 인증 무결성 공백이 해소됐다고 볼 수 없습니다.