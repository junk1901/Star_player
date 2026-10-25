**T11 최종 판정: PASS — B1 코드 구현 검수 범위에서 수용 가능합니다.**  
R2-B는 **RESOLVED**입니다. 기존 해결 항목을 침해하는 회귀나 새로운 MAJOR·MINOR 결함은 발견하지 못했습니다. OWNER 최종 승인과 운영 보안 Gate는 별도입니다.

**1. 기준 파일·Git 확인**

| 항목 | 직접 확인 결과 |
|---|---|
| HEAD | `257403ddae557a54c4013c17705370caeac7a2e0` |
| 브랜치 | `main` |
| 승인 계약 | 175,154바이트, SHA `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64` |
| 루트 TASK.md | SHA `ecbc0859c809c68151c93f92058c31a7ab0b84ce8d8cc9a4a25512b7dd06f825` |
| D3 보호 파일 | 기준본·보존본·TASK·PDF 2개 모두 기준 SHA 일치 |
| 작업 트리 | 추적 수정 15개, 미추적 160개, Staging 0 |

미추적 상태인 `RequiredOriginals.cs`, 신규 시험 등도 실제 내용을 확인했습니다. 검수 중 파일이나 Git 상태를 변경하지 않았습니다.

**2. R2-B: RESOLVED**

수정은 필드 이름 비교에 그치지 않고 **독립 재생성한 PREFLIGHT 전체 트리 비교**로 연결돼 있습니다.

- 기대 기록은 제출 PREFLIGHT에서 가져오지 않습니다. 승인 기록에서 만든 입력, 승인 Git SHA, Pin된 Seed 계획으로 기존 Preflight를 다시 수행합니다.
- 준비 상태 보정은 `execution_path`, Family의 `unit_preflight` 또는 Dominance의 `cell_preflight`, Unit의 `attestation`으로 제한됩니다.
- 모든 Unit이 독립 검사와 Attestation 재생성을 통과한 뒤 집계 상태를 보정합니다. 그 밖의 실패 검사는 그대로 거부합니다.
- 재생성한 Attestation Hash를 실제 보존 UNIT_MANIFEST의 Attestation Hash와 대조합니다.

근거: [RequiredOriginals.cs:50](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs:50), [ReadyUnit:103](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs:103).

전체 비교는 객체 멤버 이름·순서, 배열 길이·순서, 타입, 값, 중첩 구조를 검사합니다. 따라서 추가·누락·중복 멤버와 숫자/문자열/boolean 타입 변조도 거부합니다. 숫자는 원문 토큰까지 비교하므로 `2`, `2.0`, `"2"`를 동일하게 취급하지 않습니다.

근거: [FirstDifference:143](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs:143).

이 검사는 `VerifyFamily`의 Replay 이전에 실행됩니다. 검증 실패 시 검증 완료 객체가 반환되지 않으며, Certification 발급 경로로 진행하지 못합니다. 원본 SHA·길이·참조를 모두 다시 맞춰도 내용 차이는 `Preflight.record_differs`로 거부됩니다.

근거: [Verifier.cs:239](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Verifier.cs:239), [Certification.cs:98](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Certification.cs:98).

**3. 비교 제외 정책: 적합**

유일한 값 비교 제외는 **`output_directory` 검사의 `message` 문자열**입니다.

- 제외 위치는 제출 기록이 아니라 **기대 기록의 검사 ID**로 결정합니다.
- `id`, `status`, `code`와 `message`의 문자열 타입은 계속 검사합니다.
- `cli`, `git_integrity`, `git_sha`, `execution_path`, `attestation`에는 제외가 없습니다.
- 제출된 출력 경로 메시지는 후속 실행 경로나 인증 판정 입력으로 사용되지 않습니다.

근거: [RequiredOriginals.cs:134](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs:134).

공유 상수로 옮긴 세 메시지는 HEAD의 기존 리터럴과 동일합니다. 공유 Writer만 믿고 판단하지 않고 기존 공식 형식도 대조했으며, `DRAFT`, `official_evidence=false`, `complete=false`, 통계 미판정 표현을 변경한 흔적은 없습니다.

**4. RED/GREEN 및 정상 발급 경로**

RED 로그가 직접 입증하는 반례는 다음 **두 건**입니다.

1. Calibration `schema_status` 변조 후 영수증 발급.
2. Dominance `games_per_cell` 2→3 변조 후 영수증 발급.

각 시험이 첫 실패에서 중단되므로 나머지 변조를 수정 전에도 실행했다고 간주하지 않았습니다. [RED 로그:10](E:/Madegame/Star_player/docs/reports/B1_T11_R2B_VALUE_RED.log:10)

현재 시험은 파일을 변조한 다음 Raw SHA·길이·상위 참조를 갱신하고, 실제 `Certification.VerifyFamily → IssueFamily`를 호출합니다. 수정 후 시험에는 요청한 값·타입 변조, 중첩 값 변경, 멤버 추가·삭제가 포함됩니다. 정상 사본의 발급 성공도 확인하도록 구성돼 있습니다.

근거: [T11R2BValueTests.cs:82](E:/Madegame/Star_player/tests/StarPlayer.EvidenceVerifier.Tests/T11R2BValueTests.cs:82), [변조 목록:98](E:/Madegame/Star_player/tests/StarPlayer.EvidenceVerifier.Tests/T11R2BValueTests.cs:98).

정상 통합 시험에는 세 Family 영수증과 Phase 영수증 발급, INPUT_BUNDLE 4개, PREFLIGHT 57개, Regression Proof 및 RAW_INVENTORY·SourceFiles 대조가 포함됩니다. Seed `991000`도 유지됩니다.

근거: [OfficialPzl6VerificationTests.cs:80](E:/Madegame/Star_player/tests/StarPlayer.Infrastructure.Tests/OfficialPzl6VerificationTests.cs:80).

`NOT_STARTED`와 `Official.execution_not_implemented`는 기존 `OfficialOutput.Result`의 **실행 전 점검 기록 표현**으로 유지됩니다. 최종 실행 성공이나 통계 PASS로 승격되지 않습니다. 이번 수정에서 새로운 OWNER 정책이 필요한 변경은 확인하지 못했습니다. [Official.cs:388](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/Official.cs:388)

**5. 기존 Finding 회귀 및 수용 판정**

| 항목 | 판정 | 확인한 경계 |
|---|---|---|
| R1 | RESOLVED 유지 | 발급 시 Frozen Snapshot 대조 및 스냅샷에서 복원 |
| R2-A | RESOLVED 유지 | 승인 프로필·실제 내장 입력 바이트에서 필수 집합 도출 |
| R3 | RESOLVED 유지 | 공통 원본만 중복 제거, 동일 ID의 다른 원본은 거부 |
| R4 | RESOLVED 유지 | 루트 생성부터 PinnedDirectory 경로 사용 |
| PREFLIGHT 계약 | 준수 | 첫 경기 전 기록, 전체 값 검증, 발급 경계 결속 |
| T10 코드·자동화 수용 범위 | 충족 | 기존 Golden·회귀 및 이번 변조/양성 시험 근거 확인 |
| B1 코드 구현 | 최종 수용 가능 | 이번 표적 검수에서 차단 결함 없음 |

근거: [Frozen Snapshot](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Verifier.cs:142), [필수 원본 검사](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Certification.cs:149), [Phase 원본 합집합](E:/Madegame/Star_player/tools/StarPlayer.EvidenceVerifier/Certification.cs:263), [Archive 생성](E:/Madegame/Star_player/tools/StarPlayer.SimHarness/Evidence/Archive.cs:170).

이번 변경 범위에서 기존 게임 계산식·Hash·RNG 소비·기본 Replay 순서를 바꾸는 변경은 확인하지 못했습니다. 다만 **운영 실측까지 포함한 T10 전체 검증 완료**를 선언하는 것은 아닙니다.

**6. 제출 증거와 독립 확인의 구분**

- 보존된 U1~U6 원시 로그 합계는 **3,041 PASS / 0 FAIL / 0 SKIP**으로 보고서와 일치합니다. 부분 재실행 U3a·U3b는 중복 합산하지 않았습니다.
- Verify 로그 SHA `565d6737…46f92e`, 원시 결과 SHA `99aeb2bd…ae873c`가 보고값과 일치합니다.
- Golden 4개 시험의 구성과 전체 SimHarness 시험 통과 로그를 확인했습니다. Codex가 Golden을 재실행한 것은 아닙니다.
- 보존된 Dry Pilot 9개 재현 결과를 기존 Archive와 직접 비교했습니다. **ConfigHash·RunResultHash·환경 정보 제외 Manifest가 모두 일치**합니다. 재현 실행 자체는 Claude의 제출 증거입니다.
- 현재 SimHarness DLL SHA도 보고된 `31110bf5…c117b`와 일치합니다.
- Verify 원시 결과의 문서 링크 검사는 **196개**입니다. **197개·깨진 링크 0개**는 문서 수정 후 별도 검사 보고이며, 같은 Verify 실행 결과로 합치지 않았습니다.

근거: [최종 Verify 로그](E:/Madegame/Star_player/docs/reports/B1_T11_R2B_VALUE_VERIFY_LOG.md), [추가 검사 보고:160](E:/Madegame/Star_player/docs/reports/B1_T11_R2B_VALUE_BINDING_REPORT.md:160), [재현 스크립트](E:/Madegame/Star_player/tools/b1-drypilot-repro.ps1).

**7. 남은 NOTE와 운영 Gate**

새로운 MAJOR·MINOR Finding은 없습니다. 다음 사항은 그대로 남습니다.

- OWNER 결정 3과 R2 지시의 저장소 기록은 확인했지만 **원본 채팅 사본과의 일치는 독립 확인하지 못했습니다.**
- B-4 공식 출력 배치, 입력 CRLF/LF 정책과 운영 빌드 재현성 동결.
- 운영 Build Provenance, 검증기 바이너리 및 Test Profile 실행 경로의 신뢰 확인.
- 보호 앵커 디렉터리 설치·ACL 실측.
- AT-1~AT-14r, 정식 AT-4/P-1, N27-d/e/f.
- 승인 Environment Profile, N12 비트 Golden 및 `witness_digest`.

이 항목들은 코드 검수 PASS로 대체되지 않습니다. **T11 코드 검수는 PASS로 종결하며, OWNER 최종 수용과 운영 허가는 이번 판정에 포함하지 않습니다.**

**Official Execution = NOT_READY · Final Execution Freeze = PENDING · Phase 3 = INCOMPLETE**

빌드·테스트·Dry Pilot·Windows 운영 실측은 재실행하지 않았습니다. 파일·Git 변경은 없으며, 승인 출처와 미실시 운영 Gate는 별도 확인이 필요합니다.