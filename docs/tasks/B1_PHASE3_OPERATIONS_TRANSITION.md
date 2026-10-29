# B1 Phase 3 운영 단계 전환 계획 (DRAFT, 운영 실행 없음)

- 작성: Claude Code, 2026-10-29. 근거: OWNER 지시 "G4/G5 검수 수용 및 기존 GitHub 기반 Phase 3 운영 단계 전환"(2026-10-29)
- **성격:** 운영 단계로 넘어가기 위한 결정표·절차·순서입니다.
  - Push, 운영 ACL 적용, Trust Anchor·Pin 등록, 공식 G1~G9, Build Provenance 등록은 **하지 않았습니다**.
  - OWNER 승인값을 만들거나 채우지 않았습니다. 표의 값은 모두 `OWNER_VALUE_PENDING`입니다.
- 관련 문서(각 문서가 자기 범위의 세부 절차를 소유합니다):
  - [A-1 기존 공개 원격 게시 준비](B1_A1_PUBLICATION_PREP.md) — 게시·SourceLink·원격 검증
  - [G5 운영 설계](B1_G5_OPERATIONAL_DESIGN_DRAFT.md) — 보호 Root·계정·ACL 도구·Pin 추적
  - [B-4 빌드 절차 결정 초안](B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md), [B-4 규범 결정 자료](B1_B4_OWNER_NORMATIVE_DECISION_DRAFT.md) — 빌드·Provenance
  - [운영 Gate 준비표](B1_OPERATIONAL_GATE_READINESS.md) — G1~G9
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 0. 코드 검수 단계 종료 기록

| 항목 | 내용 |
|---|---|
| Codex 최종 판정(G5/A-1/B-4 운영 도구) | **REVIEW_READY**. A1-FIX2-01 RESOLVED, A1-R02·G5 Finding 재발 없음, 신규 MAJOR·MINOR 0, 수정 영향 PASS |
| G4 코드 독립 검수 | PASS 유지(2026-10-28 수용) |
| 검증 소스 | `1751fe04aaafbda65462915d2dac89e56536a8ae` (후속 문서 HEAD `5e2c6403223578c3e6531991dbcfd00fc1c504b7`) |
| 증거 | 자체 시험 109/109, xUnit 3,061/0/0, `verify.ps1` GATE PASS, D1 252, D3 5, PCK SHA 불변. Golden·Dry Pilot은 이전 증거 재사용, PCK 5회 재현성은 재실행하지 않음 |
| 뜻 | Codex가 제출 실행 증거를 독립 대조한 **코드 단계** 결과입니다. 공식 G4 운영 PASS나 G5 실제 운영 검증 완료가 아닙니다 |

- 수용된 G4/G5 구현과 `tools/b1-ops/` 도구는 이 전환 작업에서 바꾸지 않았습니다. 다시 처음부터 검수하지 않습니다.
- **Codex NOTE 보존(2026-10-28 판정 원문 4절):** G5-FIX-01의 객체 단위 쓰기는 `SetKernelObjectSecurity`를 씁니다. Microsoft는 파일 시스템 객체에 다른 보안 API를 쓰도록 안내하므로, 개발 PC 실측을 일반적인 지원 보장으로 확대할 수 없습니다. 또 링크 추가 탐지는 사후 복구이며 사전 예방과 같은 보장이 아닙니다. 둘 다 운영 실측 조건(3.2절 M-11, M-6)으로 남깁니다.

## 1. 공식 원격과 로컬 이력 (요약)

세부와 근거는 [A-1 문서](B1_A1_PUBLICATION_PREP.md) 2·3·4절입니다.

- 공식 원격: `https://github.com/junk1901/Star_player.git`(Public). 새 비공개 저장소 계획 취소.
- 서버 상태(2026-10-29 `ls-remote`): `refs/heads/main` = `7456f44fd3ecd687afce210eb5be972114f8479c` 하나뿐, Tag 0개.
- 로컬 HEAD `5e2c640`은 `7456f44`의 자손입니다. **Fast-forward 가능**, Divergence 없음, 새로 공개될 Commit 49개(이 전환 작업 Commit 제외).
- SECRET 패턴 0건. 새로 공개되는 개인정보·자료: 개발 PC 실제 SID 1개 파일, 로컬 경로 문자열, 원문 35개, 규칙 PDF 2개 → OWNER 판단(A1-5).
- Push는 승인 A 전까지 하지 않습니다.

## 2. SourceLink·B-4 계획 변경

### 2.1 SourceLink

- 공식 SourceLink 문자열: `https://raw.githubusercontent.com/junk1901/Star_player/<source_git_sha>/*`. 만드는 방법·확인 규칙은 A-1 문서 5절입니다.
- **저장소 설정 변경 없음:** URL은 clone의 `origin`에서 만들어집니다. 수용된 코드·계약·`Directory.Build.props`를 바꿀 필요가 없습니다.
- **[실측]** 대소문자가 다른 URL(`star_player`)은 SourceLink와 DLL 바이트를 바꿨습니다. `.git` 접미사는 영향이 없었습니다. 공식 clone URL을 `-ceq`로 확인하는 단계를 절차에 넣었습니다.
- 이전 신규 비공개 저장소 후보 URL은 공식 설정에서 제거했습니다. 그 URL과 기존 진단 빌드의 SHA는 공식 값으로 쓰지 않습니다.

### 2.2 B-4 Artifact 후보 재산출 (진단, 공식 아님)

- **명령:** `b4-candidates.ps1 -BuildRoot E:\Madegame\g3x\g4clean3 -SourceCommit 1751fe04… -Previous <a980ea5 후보>`
  - 대상은 `1751fe0` Clean Checkout의 `verify.ps1` 빌드입니다(CI 빌드 아님, 로컬 경로 포함).
  - 출력: `E:\Madegame\g3x\phase3ops\b4_candidates_1751fe0_devbuild.json`(`5bbe1547…`)
- **결과:** `CANDIDATE_INCOMPLETE`, 441행, 비Pin 출력 파일 31개
  - 역할: SIMHARNESS_BINARY 1, VERIFIER_BINARY 1, TEST_ASSEMBLY 4, RUNTIME_CONFIG 24, DEPENDENCY 411
  - `a980ea5` 후보 대비: 추가 0, 삭제 0, **변경 27**(모두 `STARPLAYER` 분류), 불변 414
- **해석:** 제품 코드는 `a980ea5`와 같습니다. 그런데도 StarPlayer DLL 27개가 바뀐 것은 PDB 식별값(SourceLink Commit 포함)이 DLL에 들어가기 때문입니다. 제3자 DLL은 그대로였습니다.
  - 따라서 **공식 Artifact SHA는 승인 소스 Commit·공식 원격 clone·CI 빌드에서만** 측정합니다. 기존 441행·31개 결과와 이번 결과는 공식 Provenance로 승격하지 않습니다.
- 승인 계약의 닫힌 역할 집합·JSON Schema(§18.2·§18.3·§21)는 바꾸지 않습니다.

### 2.3 최종 Build Provenance 준비표

| # | 항목 | 출처·방법 | 현재 | 확정 주체·시점 |
|---|---|---|---|---|
| 1 | 공식 Git 원격 | A-1 문서 1절 | **결정됨**: `https://github.com/junk1901/Star_player.git` | OWNER(2026-10-29) |
| 2 | 승인 소스 Commit(`source_git_sha` = `ApprovalRecord.git_commit_sha`) | 게시 Commit의 조상이어야 함 | 후보 `1751fe0` 또는 그 뒤 문서 Commit. 미확정 | OWNER 승인 A 후(A1-6) |
| 3 | 공식 SourceLink 문자열 | A-1 문서 5절 | 형식 확정, Commit 미확정 | 2번 확정 후 |
| 4 | 승인 SDK·Runtime | `global.json` SDK `8.0.425`, §18.3 `dotnet_sdk` | 개발 PC 값만 있음 | 운영 기계 G8 Profile |
| 5 | Release Build | A-1 문서 6절 3단계(`ContinuousIntegrationBuild=true`) | 진단만 | 승인 A 후 공식 clone 2개 |
| 6 | SimHarness Binary | Artifact 규칙 v2 `SIMHARNESS_BINARY` | 후보 1행 | 5번 결과 |
| 7 | Verifier Binary | `VERIFIER_BINARY` | 후보 1행 | 5번 결과 |
| 8 | Test Assembly | `TEST_ASSEMBLY` | 후보 4행 | 5번 결과 |
| 9 | Dependency | `DEPENDENCY` | 후보 411행, EXE 행 해석 보류(`exe_rows_interpretation_pending`) | 5번 결과 + OWNER |
| 10 | Runtime Config | `RUNTIME_CONFIG` | 후보 24행 | 5번 결과 |
| 11 | Trust Config | `TRUST_CONFIG`(`relative_path` 루트 [충돌], B-4 초안 4절) | 미결 | OWNER 규범 결정 |
| 12 | Artifact SHA·파일 크기 | `b4-candidates.ps1`, 두 clone 일치 | 진단 SHA만(공식 아님) | 5번 결과 |
| 13 | 외부 실행 체인 Pin | 3.3절, `execution_pins` | 개발 PC 후보 191개(공식 아님) | 운영 기계 ETW 후 승인 B |

- **결속 조건 유지:** §18.3 `source_git_sha`, `ApprovalRecord.git_commit_sha`, SourceLink Commit, 실제 빌드 HEAD가 모두 같아야 합니다. 완화하지 않습니다.
- **공식 원격 변경의 바이너리 영향:** URL은 기존 진단 통제 URL과 같으므로 URL 때문에 바뀌는 값은 없습니다. 바뀌는 것은 **Commit**입니다. 승인 소스 Commit의 StarPlayer DLL(후보 27행)과 PDB, Godot Export 산출물(별도 PCK 정책)은 새로 측정해야 합니다. 제3자 DLL·Runtime Config는 같은 SDK·복원 결과라면 그대로일 것으로 예상하지만, 공식 빌드에서 확인 전까지는 예상일 뿐입니다.

## 3. G5 운영 환경 준비

### 3.1 OWNER 결정값 (승인 B 대상)

| # | 결정값 | 쓰이는 곳 | 준비된 것 | 상태 |
|---|---|---|---|---|
| B-1 | 운영 Windows 기계·OS 빌드·볼륨(NTFS) | 모든 Root, G8 Profile | 권고 구조(G5 설계 1절), NTFS 외 거부 | `OWNER_VALUE_PENDING` |
| B-2 | 보호 Root 실제 경로 | 레이아웃 `starplayer-g5-layout-v1`, 설치 설정 | 레이아웃 예(G5 설계 4.2절) | `OWNER_VALUE_PENDING` |
| B-3 | TCB 계정·OWNER 관리 세션 | Apply·Restore·설치 | 역할 정의(G5 설계 2절) | `OWNER_VALUE_PENDING` |
| B-4 | OWNER 서명 주체 SID(`owner_principal_sids`) | Registry 등록 | 분리 검사 | `OWNER_VALUE_PENDING` |
| B-5 | 검증기 실행 계정 SID와 형태(표준 사용자·가상 계정·관리 서비스 계정) | `verifier_principal_sid`, R-WORK writer | 분리 검사, 관리자 그룹 직접 구성원 거부 | `OWNER_VALUE_PENDING` |
| B-6 | Registry 계정·서비스 SID | §17 R03-2 | — | `OWNER_VALUE_PENDING` |
| B-7 | 생산자(Producer)·시험 실행 계정 SID | R-ARCHIVE writer 후보 | 분리 검사 | `OWNER_VALUE_PENDING` |
| B-8 | 배포 계정·열람자 SID | R-BUILD 스테이징, 증거 읽기 | — | `OWNER_VALUE_PENDING` |
| B-9 | **R-ARCHIVE 계약상 권한**(§17·NSC-4) | 레이아웃 R-ARCHIVE writers·readers | 도구는 값을 강제할 뿐 만들지 않음. R-BASE 아래면 먼저 보호 DACL로 존재해야 함 | `OWNER_VALUE_PENDING`. Claude가 정하지 않음 |
| B-10 | Git 실행 파일 체인(`cmd\git.exe` 런처 또는 `mingw64\bin\git.exe` 직접) | 설치 설정 `git`, R-GIT | 체인 관찰(G5 설계 8.1절) | `OWNER_VALUE_PENDING` |
| B-11 | 외부 실행 모듈 Pin(`execution_pins`, `dotnet`, `git`) | 설치 설정 | 수집·검증 도구(`g5-pins.ps1`) | 운영 ETW 뒤 `OWNER_VALUE_PENDING` |
| B-12 | 운영 ETW 결과 채택 | B-11 근거 | 추적 계획(G5 설계 8절) | 미실행 |
| B-13 | Trust Anchor 설치값(R03-1 앵커) | R-TRUST | 앵커 검사(기존 G4 코드) | `OWNER_VALUE_PENDING` |
| B-14 | 설치 설정 `starplayer-verifier-install-config-v1` 값 | R-CONFIG 고정 위치 | 형식·검사(G4) | `OWNER_VALUE_PENDING` |
| B-15~B-22 | G5-TEMP-01 로컬 S4U 실행기(로그온 권한, 작업·보안 설명자, 시작 스크립트·설정 SHA, 요청 디렉터리, `verifier-tmp` SDDL, 호스트 `powershell.exe`, 실행기 로그·S8 절차)와 G1 공식 감사 PowerShell(2026-10-30 추가) | S0~S9, G1 감사 | [S4U 구성안](B1_G5_S4U_LAUNCHER_PLAN.md) 6절 | `OWNER_VALUE_PENDING`. B-5는 로컬 S4U 전용 표준 사용자로 한정(gMSA 제외) |

**준비 완료(코드 단계 수용됨):** `g5-acl.ps1`(Plan·Verify·Apply·Restore), `g5-probe.ps1`, `g5-pins.ps1`(Trace·Adopt·Collect·Verify), `b4-candidates.ps1`, `a1-scan.ps1`, `a1-refcheck.ps1`, `selftest.ps1` 109건, G5 설계 문서.

**보호 Root 정책(OWNER 2026-10-29 4-1항):**

- 기존 G5 설계의 Root 분리를 유지합니다. R-WORK는 일반 TEMP를 쓰지 않습니다.
- `apply: true` Root는 레이아웃이 승인한 변경 대상입니다.
- `apply: false` Root(R-ARCHIVE·R-DOTNET·R-GIT 등)는 **직접·간접 변경 모두 금지**입니다. 부모 ACL 상속, 하드링크를 통한 변경도 포함합니다. 안전하지 않은 구성은 변경 전에 거부합니다(`REFUSED`).
  - **문구 해석:** 지시 원문의 이 단락은 "비적용 Root에 대한 정책" 아래에 "apply Root는 직접·간접 변경 모두 금지"라고 적혀 있습니다. 앞 문장("apply Root는 승인된 변경 대상이다")과 함께 읽어, 금지 대상을 **비적용 Root**로 해석했습니다. Codex 2차 판정(2026-10-28 13:37Z, G5-R02 행)도 이 해석을 "OWNER가 확정한 비적용 Root 해석"으로 적었습니다.

### 3.2 운영 실측 절차 (승인 B 뒤, 운영 기계)

공통: OWNER 관리 세션(승격)에서 실행하고, 결과는 R-AUDIT와 저장소 밖 진단 디렉터리에 덮어쓰기 없이 보존합니다. 각 항목은 운영 기계에서 실제로 확인하기 전까지 `미검증`입니다. 실측 대상과 관리자 권한을 쓰지 않는 개발 PC 진단은 섞지 않습니다.

| ID | 실측 | 방법 | 기대 | 보존 증거 |
|---|---|---|---|---|
| M-1 | 관리자 권한 Apply·Restore | `g5-acl.ps1 -Mode Plan` → `Verify` → `Apply -OwnerApproved -Snapshot <R-AUDIT>\…` → `Verify` → (시험 Root에서) `Restore` | Apply PASS, 사후 Verify PASS, Restore 후 스냅샷과 SDDL 일치 | 레이아웃, 스냅샷, 일지, 보고서 JSON, `icacls /save` |
| M-2 | 다른 계정 소유자 ACL 복원 | 소유자가 다른 역할 계정인 시험 항목을 포함해 Apply 실패 주입 없이 Restore | SeRestorePrivilege로 원래 소유자 복원, 차이 0 | 전후 SDDL |
| M-3 | NTFS 외 파일 시스템 거부 | 시험용 FAT32·exFAT 볼륨(또는 ReFS)에 Root를 둔 레이아웃으로 Apply | `REFUSED`, 원인 "link count / 64-bit file ID not trusted", 변경 없음 | 출력, 볼륨 정보 |
| M-4 | 파일 교체·삭제·이름 변경 공격 | 각 역할 계정의 비승격 세션에서 `g5-probe.ps1 -Role <role>` | 모든 금지 권한 `DENIED`, `UNEXPECTED_ALLOWED` 0 | 역할별 Probe 보고서 |
| M-5 | 부모 DELETE_CHILD | Probe의 Root 삭제·이름 변경 시도와 상위 경로 규칙 Verify | 상위가 FILE_DELETE_CHILD를 주지 않음, Root 삭제 `DENIED` | Probe, Verify |
| M-6 | 하드링크 유입 | 시험 Root에 다른 Root 파일의 하드링크를 두고 Apply. 역할 계정으로 R-BUILD 파일을 R-ARCHIVE에 링크 생성 시도 | Apply `REFUSED`. 링크 생성 권한 여부를 기록(개발 PC 미검증 항목) | 출력, 시도 결과 |
| M-7 | 유지보수 중 기존 열린 쓰기 핸들 | Apply 전 Root 아래에 열린 핸들 조사. Windows 기본 도구 `openfiles /local on`(재부팅 필요) 뒤 `openfiles /query /v` | Root 아래 쓰기 핸들 0. 있으면 Apply 중단 | 조사 출력 |
| M-8 | 역할 계정 로그아웃·프로세스 부재 | `quser`, 역할 계정 비활성화 기록, `g5-acl` 유지보수 검사(운영 모드 강제) | 주체 프로세스 0, 소유자 미확인 프로세스 수 기록 | 출력 |
| M-9 | 복구 실패 시 `ROLLBACK_FAILED` | 운영 기계의 별도 시험 Root(운영 Root 아님)에서 실패 상황 재현 | 종료 코드 3, `residue` 목록, 기존 경로 삭제 없음 | 출력 |
| M-10 | 다른 계정으로 실제 접근 차단 | 생산자·열람자·배포 계정으로 R-TRUST·R-BUILD·R-DOTNET·R-GIT·R-WORK 쓰기·삭제·이름 변경 실제 시도 | 모두 거부 | 시도별 명령·출력·계정·시각 |
| M-11 | Windows 파일 시스템 보안 API 동작 | M-1 결과의 하위 항목 SDDL을 운영체제가 새로 만든 파일·디렉터리 SDDL과 비교(자체 시험 `acl.fix01_exact_descriptors_equal_windows_inheritance`와 같은 방법). `AI`·`P` 플래그와 상속 ACE 순서 확인 | 같음. 다르면 Apply 결과를 공식으로 쓰지 않고 보고 | SDDL 비교 |
| M-12 | 검증기 서비스 계정 TEMP(G5-TEMP-01, 2026-10-29 추가) | 검증기 서비스에 `TMP`·`TEMP`를 검증기 전용 보호 디렉터리(예: `R-WORK\verifier-tmp`, TCB+VERIFIER만 쓰기)로 지정(서비스 환경 설정, 코드 변경 없음) → 서비스 프로세스의 `Path.GetTempPath()` 실측 → 그 디렉터리 ACL을 `g5-acl Verify`·역할별 `g5-probe`로 확인 → TEST_ONLY 인증 1회로 재생 디렉터리 `starplayer-verifier\<guid>`가 그 아래 생기고 끝나면 지워지는지 확인 → E2-1 환경 블록 digest·Profile에 그 값이 들어가는지 확인 | 다른 역할의 쓰기·삭제·생성 `DENIED`. 재생 디렉터리가 보호 위치에만 생김 | 서비스 설정, `GetTempPath` 출력, ACL·Probe 보고서, 인증 로그 |

- 관리자 그룹 중첩 구성원은 도구가 해석하지 않으므로 각 계정의 `whoami /groups`를 증거로 남깁니다.
- **M-12 개정 (2026-10-30, Codex G5-TEMP-01 OPEN_MAJOR):** 위 M-12 단일 행은 이력으로 남기고, 판정은 [G5-TEMP-01 보호 모델](B1_G5_TEMP01_PROTECTION_MODEL.md) 4절의 **M-12A~J** 전부로 합니다. TEMP를 R-WORK 아래로 옮기는 것만으로는 해소가 아닙니다. 강제 시작 절차(같은 문서 3절, NOT_IMPLEMENTED)와 M-12A~J 통과 전에는 공식 인증을 시작하지 않습니다.
- **M-12 개정 1 (2026-10-30, Codex G5-MODEL-01/02):** M-12A~J에 로컬 S4U 동작·EFS·세션 중 예방 확인·우회 시도를 더했습니다([보호 모델](B1_G5_TEMP01_PROTECTION_MODEL.md) 4절).
- **M-12 개정 2 (2026-10-30, C-3 구현):** 검증기 Replay가 TEMP를 쓰지 않게 되어 Replay 사본 항목은 해당 없음이 됩니다. 운영 ETW로 재생용 파일 미생성 확인, Test Profile VSTest 임시 파일 검토를 별도로 남깁니다. S4U 항목은 보류입니다([C-3 보고서](../reports/B1_G5T01_C3_REPORT.md) 12절).
- 실패하면 해당 Gate(G5) 중단 조건입니다([운영 Gate 준비표](B1_OPERATIONAL_GATE_READINESS.md) G5).

### 3.3 실행 체인 추적·Pin 후보 (승인 B의 일부)

- **대상 체인:** `dotnet.exe` → `hostfxr`·`hostpolicy` → .NET Runtime 모듈 → VSTest(`vstest.console.dll`) → 시험 폴더 `testhost.exe` → SimHarness·EvidenceVerifier → Git 실행 체인(B-10) → Windows Native DLL → 실제 동적 로드 파일
- **방법:** G5 설계 8절(ETW: Kernel-Process·Kernel-File·DotNETRuntime Loader, `tracerpt`) → 경로 목록 → `g5-pins.ps1 -Mode Collect` → `CANDIDATE_NOT_APPROVED` 후보 → OWNER 승인.
  - 회차·조건: 대상마다 2회 이상, `ko-KR`·`en-US`, Pass·Fail Case, 공식 인증 거부 경로.
  - 표본 추출 진단 `g5-pins.ps1 -Mode Trace`는 보조 자료입니다. ETW 결과를 대신하지 않습니다.
- **원칙:**
  - 운영 환경의 ETW 결과와 파일 SHA만 기준입니다. 개발 PC 191개 후보는 참고 자료이며 공식 Pin이 아닙니다.
  - 경로·Root에 따른 자동 허용 목록을 만들지 않습니다(D-3). 동적 어셈블리 허용 목록은 기본 빈 목록입니다(D-4).
  - 공식 인증 **성공** 경로 추적은 Registry·앵커 준비와 OWNER 승인 뒤에만 합니다.

## 4. 운영 G1~G9

각 Gate의 선행 조건·실행 계정·승인값·명령·증거·중단 조건·Codex 검수 지점·OWNER 승인 조건은 [운영 Gate 준비표](B1_OPERATIONAL_GATE_READINESS.md)가 소유합니다(이번에 실행 계정·승인값·명령 표를 추가). 개발용 `verify.ps1`의 GATE PASS는 운영 G1~G9 PASS가 아닙니다.

## 5. Phase 3 완료까지 남은 의존성과 권장 순서

```text
[승인 A] 기존 공식 GitHub 최초 게시 (A1-2~A1-7)
   │  → 게시 후 Ref 대조, 원격 조회 검증(A-1 문서 7절)
   ├─► G2 공식(원격 clone 2개, D3·내장 리소스 SHA)
   └─► G3 공식(승인 소스 Commit, CI 빌드, Artifact·SourceLink·PCK 5회) ─┐
[승인 B] G5 운영 설치 (B-1~B-14)                                       │
   ├─► G5 공식(Apply, M-1~M-11, 역할별 Probe)                           │
   └─► 운영 ETW → Pin 승인 → 설치 설정·앵커 설치 → G4 공식 ◄────────────┘
          └─► G1(출력 이름·Root 확정) → G6(AT-1~AT-14r) → G7(N27-d/e/f)
                 └─► G8(Environment Profile) → G9(Golden·witness_digest)
[승인 C] 최종 Artifact·Build Provenance, G1~G9 증거, 독립 검수 → Build Freeze → Phase 3 완료 판단
```

| 순서 | 작업 | 막는 것 |
|---|---|---|
| 1 | 승인 A와 최초 게시 | 공식 원격 Commit 조회, SourceLink 원격 검증, G2·G3 공식 |
| 2 | 승인 소스 Commit 확정(A1-6) | Build Provenance 결속, 공식 Artifact SHA |
| 3 | G3 공식 측정 | G4(Verifier SHA), Build Provenance |
| 4 | 승인 B: 운영 기계·SID·R-ARCHIVE 권한 | G5 Apply, Pin 추적, G6~G9 |
| 5 | G5 공식 적용·실측 | G4·G6·G7 |
| 6 | 운영 ETW → `execution_pins` 승인 → 설치 설정·앵커 | G4 공식 |
| 7 | G1, G6, G7, G8, G9 | 승인 C |
| 8 | 승인 C | Build Freeze, Phase 3 완료 |

- 각 단계의 공식 결과는 Codex 독립 검수 뒤 OWNER가 수용합니다. 공식 실행 전 조건이 하나라도 빠지면 실행하지 않습니다.

## 6. OWNER 승인 지점

| 지점 | 내용 | 승인 전 금지 |
|---|---|---|
| **승인 A** — 기존 공식 GitHub 최초 게시 | 최종 게시 Commit, `main` Ref, Tag 게시 여부, 실제 공개될 추가 이력(A-1 문서 3·4절), 비강제 Push 절차(A-1 문서 2.2절) | Push |
| **승인 B** — G5 실제 운영 설치 | 운영 기계, 실제 SID·역할, R-ARCHIVE 권한, ACL 적용, Trust Anchor 설치, 외부 Pin 승인(3.1절 B-1~B-14) | 운영 권한 변경·설치 |
| **승인 C** — 공식 Gate 및 Build Freeze | 최종 Artifact·Build Provenance, 공식 G1~G9 증거, 독립 검수 결과, Phase 3 완료 선언 | Build Freeze |
