# B1 운영 Gate G1~G9 준비표 (선행 조건·담당·증거·중단 조건)

- 작성: Claude Code, 2026-10-28. 근거: OWNER 지시 "G4 구현 수용 및 Phase 3 잔여 작업 자율 진행"(2026-10-28) 5항
- [운영 Gate 후속 계획](B1_OPERATIONAL_GATE_PLAN.md)(2026-10-25)의 G1~G9 정의를 그대로 씁니다. 이 문서는 각 Gate의 실행 조건을 구체화합니다. Godot `verify.ps1`의 G1~G4·E1~E4 검사와는 다른 번호 체계입니다.
- **공식 Gate PASS를 선언하지 않습니다.** 아래 "현재"는 사실 기록입니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 실행 구분

| 구분 | 뜻 | 누가 | 승인 |
|---|---|---|---|
| **진단** | 격리 환경·개발 PC에서 도구·절차·반례를 확인. 운영 값·신뢰 입력을 만들거나 바꾸지 않음 | Claude Code(자율) | 불필요(보고만) |
| **공식** | 승인 운영 기계·계정·원격에서 Gate 증거를 만듦. 결과가 신뢰 입력(앵커·Pin·Provenance·Profile·Golden)이 됨 | OWNER(관리 세션) 실행, Claude Code 절차·검증 지원 | 실행 전·결과 수용 모두 OWNER |
| **독립 검수** | 제출 증거와 코드를 READ-ONLY로 판정 | Codex | 판정은 승인이 아님 |

- 공통 중단 조건(모든 Gate):
  - 계약 충돌
  - 새로운 보안 MAJOR
  - 새로운 승인 규범이 필요한 경우
  - B1 Tag·계약·OWNER 원문·Golden·Hash·RNG·Replay·루트 `TASK.md` 변경이 필요해지는 경우
  - 어느 경우든 작업을 멈추고 별도 보고합니다.

## 2. Gate별 준비표

### G1 — B-4 공식 출력 파일명·경로

- **현재:** 미결. B-4 Clause와 출력 Root는 운영 값 확인 뒤 확정합니다(OWNER 2026-10-28 4항).
- **선행 조건:** G5 Root 구조 확정([G5 설계](B1_G5_OPERATIONAL_DESIGN_DRAFT.md) 1절), §21 `run/<run_id>/` 내부 배치(결정 3 §6)
- **진단:** 확정 이름 후보로 생산자·검증기·시험이 같은 경로를 쓰는지 TEST_ONLY 시험. 이름은 코드 한 곳에서만 정의되는지 확인
- **공식:** OWNER가 이름·Root를 확정. 코드 변경이 필요하면 별도 TASK·Codex 검수
- **증거:** 확정 이름 표, 변경 Commit, `verify.ps1` 전체 PASS, Hash·Replay 불변 기록
- **중단 조건:** 기존 Hash·Replay가 바뀜. 이름이 두 곳 이상에서 정의됨
- **독립 검수:** 코드 변경 시 Codex
- **OWNER 최종 승인:** 필요

### G2 — CRLF/LF 정책·빌드 입력 고정

- **현재:** 정책 결정됨(OWNER G2 2026-10-26: 검수된 바이트를 `-text`로 그대로 유지, 일괄 정규화 없음, `.gitattributes`). Gate 판정은 선언되지 않았습니다.
- **선행 조건:** 없음
- **진단:** 깨끗한 clone에서 보호 파일·내장 리소스 SHA가 체크아웃 설정과 무관하게 같은지 확인. 현재 `verify.ps1` D3 5개 PASS(G4 Clean Checkout)
- **공식:** A-1 원격에서 받은 clone 2개로 같은 확인(A-1 문서 6절 1~2단계)
- **증거:** D3 결과, 내장 리소스 SHA 표
- **중단 조건:** 원격 clone에서 보호 파일 SHA가 다름
- **독립 검수:** G3와 함께 Codex
- **OWNER 최종 승인:** 필요(결과 수용)

### G3 — 운영 Build Provenance·바이너리 SHA

- **현재:** 진단 완료분은 재현성 조사, Scene Migration(`0009173`), Artifact 후보 규칙 v2입니다. 공식 SHA는 A-1 URL과 승인 소스 Commit이 정해져야 측정할 수 있습니다.
- **선행 조건:** G1, G2, A-1 원격 게시, 승인 소스 Commit
- **진단:** 이번 작업에서 수행했습니다. 결과는 [준비 보고서](../reports/B1_G5_A1_PREP_REPORT.md) 4·5절입니다.
  - `b4-candidates.ps1`로 `a980ea5` 빌드의 441행 재산출
  - 이전 후보와의 차이 확인
  - 후보 URL SourceLink 확인
- **공식:** [A-1 문서](B1_A1_PUBLICATION_PREP.md) 6절 절차
  - 두 경로 clone, 전체 검증, CI 빌드
  - 441행 SHA 일치
  - SourceLink = A-1 + 승인 Commit
  - PCK 5회
- **증거:**
  - 두 clone의 Artifact JSON과 SHA
  - verify·Golden·Dry Pilot 로그
  - PDB SourceLink 덤프
  - PCK 비교 결과
- **중단 조건:**
  - 두 clone 간 SHA 불일치
  - SourceLink Commit·URL 불일치
  - PDB·Coverage·PCK를 닫힌 Build Provenance 역할에 넣어야 하는 상황(OWNER 4항: 임의 추가 금지)
- **독립 검수:** Codex(재현성·역할 배정)
- **OWNER 최종 승인:** 필요(Build Provenance 값 채택, B-4 Clause)

### G4 — 검증기 바이너리·Test Profile 실행 경로 신뢰

- **현재:** **코드 단계 수용**(OWNER 2026-10-28)입니다.
  - 기준: 검증 소스 `a980ea5`, Codex 최종 READ-ONLY PASS
  - 운영 G4 PASS는 승인되지 않았습니다.
- **선행 조건:** G3(공식 VERIFIER_BINARY SHA), G5(R-BUILD·R-DOTNET·R-GIT·R-WORK ACL), 승인 `execution_pins`
- **진단:**
  - 완료: G4 단위·반례 시험 90개
  - 이번 작업: Pin 수집 도구와 개발 PC 로드 추적
- **공식:**
  - 운영 기계 ETW 추적([G5 설계](B1_G5_OPERATIONAL_DESIGN_DRAFT.md) 8절)으로 `execution_pins` 확정
  - 설치 설정 설치
  - 승인 환경에서 거부 경로를 확인합니다: 앵커 불일치, 비Pin 모듈, 다른 dotnet
- **증거:** 추적 원시 자료, Pin 후보·승인 JSON, 설치 설정 SHA(`InstallConfig.RawSha256`), 거부 출력
- **중단 조건:**
  - 비Pin 모듈이 필수 실행 경로에 있음
  - 보안 제품이 주입한 DLL처럼 Pin할 수 없는 모듈이 있음
  - 동적 어셈블리 허용이 필요해짐(D-4: 기본 빈 목록)
- **독립 검수:** Codex(Pin 범위)
- **OWNER 최종 승인:** 필요

### G5 — 보호 Root·계정 분리·ACL

- **현재:** 준비 완료(이 작업). 운영 적용·실측은 하지 않았습니다.
- **선행 조건:** 운영 기계, 역할 계정·SID, R-ARCHIVE 계약 값
- **진단:** 이번 작업에서 수행했습니다.
  - 완료: 도구 4종(`g5-acl`·`g5-probe`·`g5-pins`·`selftest`)과 격리 자체 시험 36/36
  - 개발 PC 관찰: R-DOTNET PASS. R-GIT은 사용자 소유 파일 5개로 FAIL. 개발 계정은 관리자 그룹 구성원
- **공식:** [G5 설계](B1_G5_OPERATIONAL_DESIGN_DRAFT.md) 4.3절 적용, 각 역할 계정에서 `g5-probe.ps1`, NSC-4 앵커 판정(검증기 기존 검사)
- **증거:** 레이아웃 JSON, 사전·사후 Verify 보고서, 스냅샷, `icacls /save`, 역할별 Probe 보고서, `whoami /groups`
- **중단 조건:**
  - 사후 Verify 위반(자동 복구 후 보고)
  - `ROLLBACK_FAILED`
  - 어느 역할 Probe의 `UNEXPECTED_ALLOWED`
  - 관리자 그룹에 속한 역할 계정
- **독립 검수:** Codex(ACL 규칙·증거)
- **OWNER 최종 승인:** 필요(실행 전과 결과)

### G6 — AT-1~AT-14r·정식 AT-4/P-1

- **현재:** 미착수
- **선행 조건:** G5 공식 완료
- **진단:** 기존 단일 계정 Seam 시험만 있음(변경 없음)
- **공식:** 계약 3.5 표의 각 기대 결과를 운영 계정 배치에서 실측합니다.
  - AT-4는 표준 사용자가 `registry_account_sid` 소유 Pipe를 만들 때 `ERROR_INVALID_OWNER`가 나는지 확인합니다.
  - 그 결과가 확인될 때만 PATCH-03을 운영에서 씁니다.
- **증거:** 시험별 명령·출력·계정·시각
- **중단 조건:** 기대와 다른 결과. AT-4 실패 미확인
- **독립 검수:** Codex
- **OWNER 최종 승인:** 필요(결과 수용)

### G7 — N27-d/e/f 실측

- **현재:** 미착수
- **선행 조건:** G5, G6
- **공식:** 각 반례를 계정별로 실측합니다.
  - 재연결 서버 바뀜 → 재인증 전 사용 불가
  - 서비스 권한 없는 SQLite 직접 수정 → ACL 거부
  - Impersonation 실패 차단, Revert 실패 FailFast
- **증거:** 반례별 출력
- **중단 조건:** 어느 반례든 거부되지 않음
- **독립 검수:** Codex
- **OWNER 최종 승인:** 필요

### G8 — 운영 Environment Profile 승인

- **현재:** 미착수
- **선행 조건:** 운영 기계 확정
- **진단:** 검증기 `environment` 명령으로 C0 출력 형식 확인(기존)
- **공식:** 운영 기계 C0를 정리합니다. 대상은 OS·런타임·`ucrtbase`·runtime_files 3개·forbidden prefixes·`math_golden`입니다. 이를 Profile로 승인하고, 변경 시 거부를 실측합니다(E2-4).
- **증거:** C0 원문, Profile JSON·SHA
- **중단 조건:** 운영 기계가 Profile과 다름
- **독립 검수:** Codex
- **OWNER 최종 승인:** 필요

### G9 — N12 비트 Golden·`witness_digest` 승인값

- **현재:** 미착수
- **선행 조건:** G8
- **공식:** 승인 환경에서 다음 값을 생성해 Profile `math_golden`과 대조합니다(E2-5).
  - `UpperQuantile` j=1..36
  - `witness_digest`(BITS64)
  - Holm 동률 Fixture 고정
- **증거:** 생성 명령·출력·값
- **중단 조건:** 값 불일치
- **독립 검수:** Codex
- **OWNER 최종 승인:** 필요(승인값 채택)

## 3. 다음에 묶어서 받을 OWNER 승인 지점

1. **A-1 비공개 원격 생성·최초 게시:** [A-1 문서](B1_A1_PUBLICATION_PREP.md) 8절 결정 A1-1~A1-7
2. **실제 운영 ACL·Trust Anchor·Pin 설치:** G5 공식 적용, 설치 설정·앵커 설치, `execution_pins` 승인
3. **최종 운영 Gate 결과와 Build Freeze:** G1~G9 결과 수용 → Final Execution Freeze 준비 → Official Execution 검토

- 권장 순서: 1 → (G2·G3 공식) → 2 → (G4·G5 공식) → G6·G7 → G8·G9 → 3
