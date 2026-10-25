**최종 T11 판정: `FIX_REQUIRED`**

R2-A는 해결됐습니다. R2-B도 **첫 경기 전 기록, 57개 원본 보존, 독립 Preflight 재실행**까지 연결됐습니다. 다만 Family/Dominance Preflight의 일부 값은 비교하지 않아, **내용을 변조하고 Raw SHA를 갱신한 기록이 검증을 통과하는 MINOR 1건**이 남았습니다.

**1. 기준 파일·증거 확인**

- HEAD: `257403ddae557a54c4013c17705370caeac7a2e0` — 일치
- 계약 원본·승인 보존본 SHA: `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64` — 일치
- D3 보호 파일 5개 — 모두 기준 SHA와 일치
- 작업 트리: **추적 수정 15개, 미추적 156개, Staging 0**
- 신규 `RequiredOriginals.cs`, 테스트 2개 파일, 보고서·RED 로그까지 확인했습니다.
- 파일 수정, 빌드·테스트·Dry Pilot 실행, Git 변경은 하지 않았습니다.

**2. R2-A — `RESOLVED`**

현재 공식 실행·Replay가 사용하는 게임 데이터 파일은 튜닝·규칙 JSON 두 개입니다. Environment Profile·PZL Test Profile을 합친 INPUT_BUNDLE 4개와, 별도 역할로 보존하는 승인·Build·Seed·Manifest가 구분돼 있습니다.

[RequiredOriginals.cs:14](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs:14)의 필수 SHA 다중집합은 제출 목록이 아니라 **승인 프로필 Pin과 런타임 리소스 바이트**에서 도출됩니다. [Verifier.cs:372](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Verifier.cs:372)와 [Certification.cs:149](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Certification.cs:149)에서 검증·발급 경계에 각각 적용됩니다.

누락·추가·중복·치환을 거부하고, Archive의 바이트 길이·Raw SHA 검사 및 R1 스냅샷을 거쳐 Inventory와 Receipt SourceFiles에 결속됩니다.

현재 Release DLL을 **실행하지 않고 PE 메타데이터와 내장 리소스를 읽어** 직접 확인했습니다.

| 입력 | 내장 바이트 | 작업 트리와 일치 | SHA-256 |
|---|---:|---|---|
| `virtual_tuning_v4.json` | 26,505 | 일치 | `992d7e24…e3d88c1c` |
| `game_rules_profiles_v2.json` | 2,671 | 일치 | `b016b37b…c6bcab45` |

[입력 목록 시험](E:/Madegame/Star_player/tests/StarPlayer.SimHarness.Tests/Evidence/R2InputsAndPreflightTests.cs:22)은 `HarnessData.Inputs`, csproj의 EmbeddedResource, 실제 어셈블리 리소스 목록을 서로 대조합니다. 따라서 **새 EmbeddedResource를 추가하면서 목록을 빠뜨리면 시험에서 검출**됩니다. 향후 외부 파일 로더를 새로 추가하는 경우까지 자동 발견하는 구조는 아니므로, 그 변경에서는 소비 입력 목록을 함께 갱신해야 합니다. 현재 확인된 누락은 없습니다.

**3. R2-B — `NOT_RESOLVED`: 보존 경로는 해결, 내용 검사 일부 미완료**

해결된 부분은 다음과 같습니다.

- [FamilyOfficial.cs:209](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/FamilyOfficial.cs:209)와 [DominanceOfficial.cs:266](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/DominanceOfficial.cs:266)에서 첫 Unit·경기 실행 전에 기록합니다.
- 쓰기 실패 시 경기 실행 전에 중단합니다.
- [EvidenceWriter.cs:56](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs:56)가 실제 실행의 기록 바이트를 PREFLIGHT로 보존합니다.
- Family 3개와 Unit 54개, 총 57개를 요구합니다.
- 누락, 잘못된 Unit·경로, 중복, SHA 불일치와 Attestation·DIAGNOSTICS만 남긴 경우를 거부합니다.
- [Verifier.cs:240](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Verifier.cs:240)에서 경기 Replay 전에 Preflight를 독립 재실행합니다.
- [Phase 양성 시험](E:/Madegame/Star_player/tests/StarPlayer.Infrastructure.Tests/OfficialPzl6VerificationTests.cs:105)은 INPUT_BUNDLE 4개·PREFLIGHT 57개를 포함한 영수증과 Inventory를 확인합니다.

**남은 Finding — MINOR: Preflight의 결정적 필드 값이 검증에서 빠짐**

위치:

- [RequiredOriginals.cs:74](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs:74)
- [RequiredOriginals.cs:127](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs:127)

`SameMembers`는 **필드 이름과 순서만** 비교합니다. 이후 값 검사는 일부 필드에만 적용됩니다.

예를 들어 다음 값이 검증되지 않습니다.

- Dominance의 `preflight.games_per_cell`
- `schema_status`, `official_evidence`, `complete`
- `family_gate` 또는 `dominance_gate`

원래 생성기는 [DominanceOfficial.cs:338](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/DominanceOfficial.cs:338)에서 승인된 표본 수, `official_evidence=false`, `complete=false`, `NOT_EVALUATED`를 기록합니다.

**재현 경로 — 코드 추적으로 확인, 실행 재현은 하지 않음**

1. 정상 Dominance PREFLIGHT의 `preflight.games_per_cell`을 승인값과 다른 값으로 변경합니다.
2. 필드 이름·순서, 검사 목록, Unit 목록은 유지합니다.
3. 해당 파일의 ArtifactRef Raw SHA·길이와 부모 문서 참조를 갱신합니다.
4. 개수·경로·SHA 검사는 통과합니다.
5. `PreflightRecords.Verify`는 변경한 필드의 값을 읽어 비교하지 않으므로 이를 거부하지 않습니다.

`official_evidence=true` 또는 `dominance_gate="PASS"`로 바꾸는 경우에도 같은 검사 공백이 있습니다.

**영향:** 실제 경기 수·재계산된 통계 Gate가 이 필드로 변경되는 것은 아닙니다. 그러나 잘못된 Preflight 기록이 검증된 ORIGINAL로 영수증에 포함될 수 있습니다. 따라서 게임 결과 결함이 아닌 **증거 내용 검증 결함, MINOR**로 분류합니다.

**최소 수정 요구:** 이미 재계산한 Preflight 문서와 결정적 필드의 값·타입을 비교해야 합니다. 기존에 명시적으로 제외한 실행 환경·출력 경로 등의 비교 정책은 유지할 수 있습니다. 위 변조 후 Raw SHA까지 갱신한 입력을 인증 경로에서 거부하는 시험을 추가하면 됩니다. 새 OWNER 정책이나 형식 설계는 필요하지 않습니다.

**4. Preflight 상태 표현 판정**

`run_status=NOT_STARTED`, `error_code=Official.execution_not_implemented`는 [Official.cs:367](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/Official.cs:367)의 기존 출력입니다.

이번 기록은 **첫 경기 전 Preflight 스냅샷**이므로 `NOT_STARTED`는 적절합니다. 오류 문자열은 기존 단일 명령의 준비 상태 표현을 재사용한 것으로, 현재 코드에서는 실제 Family 실행 실패나 최종 Gate로 전파하지 않습니다.

판정은 **ACCEPTABLE_IMPLEMENTATION / NOTE**입니다. 표현이 혼동을 줄 수는 있지만, 현재 구현 수용을 위해 반드시 새 OWNER 결정을 받아야 하는 계약 충돌로 보지는 않습니다. B-4의 공식 파일명·출력 표현 동결은 별도입니다.

**5. 개행·Build 재현성**

직접 확인한 상태:

- `core.autocrlf=true`
- 튜닝 작업 트리: CRLF, 26,505바이트
- 튜닝 Git blob: LF, 26,346바이트
- 규칙 파일: 2,671바이트
- 두 파일 모두 명시적 `text`·`eol` 속성 없음
- 현재 DLL의 내장 바이트는 현재 작업 트리와 일치

현재 구현은 개행을 정규화해 차이를 숨기지 않습니다. 다른 바이트의 원본과 기존 DLL을 섞으면 `Input.embedded_differs`로 거부합니다.

따라서 **현재 B1 코드 수용 결함으로 분류하지 않습니다.** 다만 같은 Git SHA만으로 동일 바이너리·Raw Input SHA를 보장할 수 없으므로, 공식 B-4 Build Freeze 전에 빌드 입력 바이트 정책과 실제 바이너리·입력 SHA를 고정해야 합니다. 운영 Build Provenance와 검증기 바이너리 신뢰 검증은 아직 완료되지 않았습니다.

**6. 회귀·테스트·증거 판정**

- R1 스냅샷, R3 합집합 충돌 거부, R4 PinnedDirectory의 **수정 영향 범위에서 회귀를 발견하지 못했습니다.**
- 공유 ORIGINAL 기대값 **4→6**은 승인·Build 2개와 INPUT_BUNDLE 4개의 합으로 정당합니다.
- 기존 Hash 계산식·RNG 소비·기본 Replay 순서의 변경은 발견하지 못했습니다.
- Golden 4개 시험의 코드가 유지돼 있습니다. 다만 이전 미커밋 전체 스냅샷이 없어 모든 과거 파일의 바이트 불변성을 독립 증명한 것은 아닙니다.

보존된 원시 로그에서 확인한 Claude 실행 결과:

| 항목 | 결과 |
|---|---|
| Release Build | 경고 0, 성공 |
| U1~U6 | **3,039 PASS / 0 FAIL / 0 SKIP** |
| 최종 Verify | **GATE PASS** |
| RED | R2-A·R2-B 모두 `No exception was thrown`으로 실패 |
| D3 | 5개 일치 |

최종 Verify 로그 SHA `101ffcbe…8dc249`, 원시 결과 SHA `064a1697…9fc8b1`, RED SHA `c266c5a7…c70474`는 보고서와 일치합니다.

Dry Pilot 보존 산출물 9개를 기존 Archive와 직접 비교했고, 모두 ConfigHash·RunResultHash·환경 제외 Manifest가 일치했습니다. **이는 파일 비교 결과이며 Codex 재실행 PASS가 아닙니다.** 재현 스크립트도 읽었지만 실행하지 않았습니다.

새 시험은 주요 누락·치환·변조를 검사하지만, 위에서 지적한 Preflight 미비교 필드의 반례는 포함하지 않습니다.

**7. 최종 수용과 잔여 Gate**

| 항목 | 판정 |
|---|---|
| R2-A | **RESOLVED** |
| R2-B | **NOT_RESOLVED — 내용 비교 MINOR 1건** |
| R1·R3·R4 | 확인 범위에서 회귀 없음 |
| INPUT_BUNDLE 계약 | 현재 입력 범위 준수 |
| PREFLIGHT 계약 | 생성·보존·개수 충족, 내용 검증 일부 미충족 |
| T10 수용 조건 | 부분 충족 |
| B1 코드 구현 최종 수용 | 잔여 MINOR 수정 후 재판정 |
| T11 | **FIX_REQUIRED** |

OWNER 지시 원문과 저장소 승인 기록의 일치 여부는 독립 확인하지 못했습니다. 보호 앵커 설치, AT-1~AT-14r·정식 AT-4, N27-d/e/f, 운영 Environment Profile, N12 Golden·witness_digest, 운영 Build Provenance·검증기 바이너리 신뢰 경계도 계속 미검증입니다. 이번 MINOR 수정과 별개로 유지해야 합니다.

Official Execution = **NOT_READY**  
Final Execution Freeze = **PENDING**  
Phase 3 = **INCOMPLETE**

미실행: 빌드·테스트·Dry Pilot·운영 보안 시험. 파일·Git 변경 없이 종료합니다.  
잔여 위험: 현재 검증기는 일부 변조된 Preflight 값을 ORIGINAL 증거로 받아들일 수 있습니다.