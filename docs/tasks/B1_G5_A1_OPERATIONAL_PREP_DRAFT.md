# B1 G5 신뢰·권한 경계와 A-1 비공개 원격 준비 (DRAFT, 실행 없음)

- 작성: Claude Code, 2026-10-27. 근거: OWNER 지시 "Phase 3 신속 완료를 위한 Claude Opus 5.5 자율 개발 위임"(2026-10-27) 4·5항
- **성격:** 기술 준비 자료입니다. 운영 OS의 ACL 변경, 신뢰 앵커·설치 설정 설치, 계정·권한 부여, 원격 생성·Push·Tag 게시는 **하지 않았고**, 모두 OWNER 승인 지점입니다.
- **OWNER 결정 반영:** N-1 = **A-1 신규 비공개 공식 원격**. 원격 생성과 최초 Push는 별도 승인 지점입니다.
- 관련: [G4 구현 보고서](../reports/B1_G4_IMPLEMENTATION_REPORT.md), [B-4 규범 결정 자료](B1_B4_OWNER_NORMATIVE_DECISION_DRAFT.md)
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. G5 보호 Root

G4 구현은 아래 Root를 설치 설정(`starplayer-verifier-install-config-v1`)에서만 읽습니다. 값은 모두 **OWNER_VALUE_PENDING**입니다.

| Root | 설치 설정 필드 | 내용 | 쓰기 허용(후보) | G4가 실행 중 확인하는 것 |
|---|---|---|---|---|
| 설치 설정 | 고정 위치 `%ProgramData%\StarPlayer\EvidenceVerifier\install-config.json` | 앵커 위치·Root·Pin·시작 정책 | TCB(SYSTEM·Administrators·TrustedInstaller)만 | NSC-4 B 경로 ACL, Reparse 없음, 관리자 권한 아님, 한 번 읽기 |
| R-TRUST | `anchor_path` | 신뢰 앵커(R03-1) | TCB만 | 기존 T1/T2 검사 |
| R-BUILD | `build_root` | 승인 Commit의 깨끗한 체크아웃과 빌드 출력. Build Provenance `relative_path`의 루트 | OWNER 배포 계정(설치 후 TCB 소유로 전환 후보) | 검증기 폴더·시험 폴더의 모든 파일이 Provenance Pin과 같은 바이트, 실행 중 핸들 유지 |
| R-ARCHIVE | `archive_root` | 증거 Archive | 계약(§17·NSC-4)대로 | `Certification.Open`의 archive root와 같음 |
| R-WORK | `work_root` | Test Profile 결과 디렉터리 | VERIFIER_SERVICE만 | 새 하위 디렉터리, Reparse 없음, TRX 하나 |
| R-DOTNET | `dotnet`, `execution_pins` | `dotnet.exe`, `host\fxr`, `shared`, `sdk` | TCB만 | 프로세스 이미지 = 승인 dotnet, Pin 파일 SHA, 핸들 유지 |
| R-GIT | `git` | 승인 `git.exe` | TCB만 | SHA, 핸들 유지 |

- **[관찰: 개발 PC]** `C:\ProgramData`는 `BUILTIN\Users:(CI)(WD,AD,WEA,WA)`와 `CREATOR OWNER:(OI)(CI)(IO)(F)`를 가집니다. 일반 사용자가 만든 하위 폴더는 그 사용자가 소유하게 되어 NSC-4 B를 통과하지 못합니다. 따라서 `StarPlayer\EvidenceVerifier`는 **TCB 계정이 만들고 상속을 끊은(보호된) DACL**로 두어야 합니다.

## 2. 실행 계정과 SID

| 역할 | 앵커·설정의 자리 | 값 | 제약 |
|---|---|---|---|
| OWNER | `owner_principal_sids` | OWNER_VALUE_PENDING | 다른 역할과 겹치지 않음(R03-1) |
| 검증기 서비스 | `verifier_principal_sid` | OWNER_VALUE_PENDING | 관리자 권한(Elevated) 아님(NSC-4 6) |
| Registry 계정·서비스 | `registry_account_sid`, `registry_service_sid` | OWNER_VALUE_PENDING | §17 R03-2 |
| 증거 작성자(생산자) | — | OWNER_VALUE_PENDING | 비신뢰 표준 사용자. R-TRUST·R-BUILD·R-DOTNET·R-GIT·R-WORK 쓰기 금지 |
| 증거 열람자 | `evidence_reader_sids` | OWNER_VALUE_PENDING | 검증기를 시작하지 않음(R03-4 1단계) |

## 3. ACL 적용 절차 (초안, 실행하지 않음)

OWNER가 운영 기계에서 관리자 권한으로 수행할 절차의 초안입니다. `<…>`는 OWNER 값입니다. Claude는 이 명령을 실행하지 않았습니다.

1. **사전 기록:** 대상 Root마다 `icacls "<Root>"`와 소유자(`Get-Acl`) 출력을 저장합니다.
2. **설치 설정 폴더(TCB 전용):**
   - `New-Item -ItemType Directory "C:\ProgramData\StarPlayer\EvidenceVerifier"`(관리자)
   - `icacls "C:\ProgramData\StarPlayer" /setowner "BUILTIN\Administrators" /T`
   - `icacls "C:\ProgramData\StarPlayer" /inheritance:r /grant:r "SYSTEM:(OI)(CI)F" "BUILTIN\Administrators:(OI)(CI)F" "<VERIFIER_SERVICE>:(OI)(CI)RX"`
3. **R-TRUST:** 2와 같은 형태. 앵커 파일은 TCB가 쓰고 검증기 서비스는 읽기만 합니다.
4. **R-BUILD:** 배포 후 소유자를 TCB로 바꾸고 `/inheritance:r`, 생산자·검증기 서비스에는 `RX`만 줍니다.
5. **R-WORK:** `<VERIFIER_SERVICE>:(OI)(CI)M`, 생산자 권한 없음.
6. **R-DOTNET·R-GIT:** 기본 설치 ACL(TrustedInstaller·SYSTEM·Administrators 쓰기, Users 읽기·실행)을 유지하는지 확인만 합니다.
7. **사후 검증(읽기 전용):**
   - `icacls` 재기록
   - 생산자 계정으로 각 Root에 쓰기·삭제·이름 변경을 시도해 모두 거부되는지 확인(반례 기록)
   - 검증기 `environment` 명령과 `certify-*`의 거부 원인을 기록
8. **보존:** 1·7의 출력 원본을 Archive 밖 감사 디렉터리에 바이트 그대로 둡니다(§5).

## 4. 외부 보호 Pin (설치 설정 `execution_pins`)

- **G4 동작:** `dotnet`·`git`·`execution_pins`의 각 파일을 쓰기·삭제 공유 없이 열어 SHA를 대조하고 프로세스가 끝날 때까지 핸들을 유지합니다. 검증기 프로세스에 로드된 어셈블리·모듈 중 Pin도 Build Provenance도 아니고 `%windir%`·`allowed_module_roots` 밖인 것이 있으면 인증을 거부합니다(V-2).
- **Pin 목록을 만드는 방법(후보):** 승인 운영 기계에서 동적 로드 추적(규범 결정 자료 6절)으로 검증기와 Test Profile 자식이 여는 R-DOTNET 파일을 모은 뒤 OWNER가 승인합니다. 추적 전에는 목록을 확정하지 않습니다.
- **대안(개정 2026-10-27):** 초안에 있던 `allowed_module_roots`(Root 전체 허용)는 Codex G4-01(MAJOR)로 **삭제**되었습니다. 경로·Root만으로 신뢰하는 방식은 쓰지 않습니다. OS·공유 런타임 예외 정책은 OWNER 결정 대상이며, 실측과 대안은 [G4 수정 보고서](../reports/B1_G4_FIX_REPORT.md) 6절에 있습니다.
- **한계:** 자식 프로세스(`vstest.console`·`testhost`·`git`)가 로드하는 파일은 부모가 검사하지 못합니다. Pin된 파일은 핸들로 교체를 막지만, Pin 밖 파일은 ACL에만 의존합니다.

## 5. 실행 파일 교체 차단과 감사·증거 보존

| 위협 | G4 차단 | G5 차단 |
|---|---|---|
| 검사 후 실행 전 DLL·EXE 교체(F-03) | Pin 파일 핸들 유지(쓰기·삭제·이름 변경 불가) | R-BUILD·R-DOTNET ACL |
| 검증기 폴더 SimHarness 사본 교체(F-06) | V-1 동일 바이트, 폴더 전체 Pin과 핸들 | R-BUILD ACL |
| 다른 `dotnet`·Apphost로 시작(F-01·F-14) | 프로세스 이미지 = 승인 dotnet | R-DOTNET ACL |
| 작업 디렉터리 `dotnet.exe`·`git.exe` 심기(F-01·F-09) | 자식은 절대경로·고정 작업 디렉터리 | R-BUILD ACL |
| TRX 바꿔치기(F-17) | 새 결과 디렉터리, TRX 하나, 한 번 읽기 | R-WORK ACL |
| 프로파일러·VSTest 변수 주입(F-08) | `startup_forbidden_prefixes` 시작 거부, 자식 환경에서도 제거 | 서비스 계정 환경 블록 |

- **감사 보존(후보):** 설치 설정 원문 바이트와 SHA(`InstallConfig.RawSha256`), 앵커 SHA, Pin 목록, `icacls` 전후 기록, 반례 시도 결과, 검증기 거부 출력을 Archive 밖 감사 디렉터리에 덮어쓰기 없이 보존합니다. 영수증 Schema는 바꾸지 않습니다(§20.1 닫힘).

## 6. A-1 신규 비공개 원격 준비

### 6.1 게시할 Git 이력

- 게시 대상은 **최종 승인 소스 Commit과 그 조상 전체**입니다. 이력 재작성·강제 Push는 하지 않습니다.
- 현재 후보 브랜치는 `phase3/g4-official-entry`입니다. 모든 Commit은 로컬 전용입니다.
- 목록 생성 명령(읽기 전용): `git rev-list --reverse <승인 Commit>`
- 보존 Tag `b1-code-accepted-2026-10-26`(→ `7bdde7dd…`)는 게시 여부를 OWNER가 정합니다(현재 원격 Tag 0개).

### 6.2 비밀정보·공개 범위 검사

- 기준 검사([규범 결정 자료](B1_B4_OWNER_NORMATIVE_DECISION_DRAFT.md) 1.2절): `00091736`·`96070c5`와 조상의 도달 가능한 모든 blob에서 비밀정보 패턴 0건
- 비공개 원격이라도 게시 직전 같은 검사를 승인 Commit 기준으로 다시 합니다.
- 운영 값(SID·Pipe 이름·설치 경로)이 문서에 들어가면 그 Commit도 게시 범위에 포함된다는 점을 확인합니다.
- **자격 증명:** 원격 접근 토큰은 저장소·세이브·로그·증거·설치 설정에 넣지 않습니다(AGENTS 규칙).

### 6.3 SourceLink URL 변경 영향

- 새 원격 URL이 정해지면 모든 StarPlayer PDB의 SourceLink JSON이 바뀌고, PDB 식별자가 들어간 13개 프로젝트 StarPlayer DLL의 바이트도 바뀝니다.
- NuGet 패키지 DLL은 바뀌지 않습니다.
- 따라서 N_S1·N_S2(`00091736`, 기존 URL) 진단 SHA는 공식 기대값으로 쓰지 않습니다.

### 6.4 승인 소스 Commit 식별

- `00091736d5386aec3f5c67cf159291976e7133a2`는 Scene Migration 검증 소스로 남습니다. 소급 변경하지 않습니다.
- G4 코드 변경 때문에 새 소스 Commit이 필요합니다. 후보는 이 브랜치의 G4 구현 Commit이며, Codex 검수와 수정이 끝난 뒤의 Commit을 OWNER가 승인 소스로 확정합니다.
- 확정 후 그 Commit의 깨끗한 체크아웃에서 `verify.ps1` 전체를 다시 실행합니다.

### 6.5 바이너리 SHA 재검증 계획

1. 새 원격에서 경로 길이가 다른 두 폴더로 clone, 승인 Commit으로 `checkout --detach`
2. B-4 4절 1~11단계 빌드(`ContinuousIntegrationBuild=true`)
3. Artifact 후보 규칙 v2(검증기·SimHarness·Test Profile 시험 폴더 전체 − PDB·Coverage 대응표, [G4 보고서](../reports/B1_G4_IMPLEMENTATION_REPORT.md) 5절)로 목록을 만들고 두 폴더의 SHA가 모두 같은지 비교
4. SourceLink URL = 새 원격 raw 기준, Commit = 승인 Commit
5. Godot PCK 5회 전체 바이트 비교(B-4 5절)
6. 결과를 저장소 밖 진단 디렉터리에 보존하고 OWNER·Codex에 제출

### 6.6 원격 Commit·Raw Source 조회 검증

- 규범 결정 자료 1.3절 절차를 그대로 씁니다. 비공개이므로 읽기 전용 자격 증명으로 수행합니다.
- 광고된 ref 확인, 객체 fetch, PDB의 SourceLink 확인, 문서별 raw ↔ blob 비교, PDB 체크섬 ↔ 체크아웃 비교 순서입니다.

## 7. OWNER 승인이 필요한 다음 지점

| 지점 | 내용 |
|---|---|
| 원격 | A-1 비공개 저장소 생성, URL 확정, 최초 Push 대상 Commit·Tag |
| 규범 | 설치 설정 형식·위치(N-5·N-6), 시작 정책 접두사(N-7), OS·공유 런타임 예외 정책(G4 수정 보고서 6절) |
| 운영 | 운영 기계·계정·SID, 3절 ACL 적용, 앵커·설치 설정 설치 |
| 진단 | 운영 기계 로드 추적과 추적 도구 |
