# B1 G5 로컬 S4U 실행기 구성안·구현 경로 비교·승인 B 값 (DRAFT)

- 작성: Claude Code, 2026-10-30. 근거: OWNER 지시 "G1/G2 정책 확정 및 G5-MODEL-01/02 후속 조치"(2026-10-30) 5·6항, 10항 4·5·7
- **성격:** 구성안·계획입니다. **NOT_IMPLEMENTED.** 운영 계정 생성, 로그온 권한 변경, 예약 작업 등록, ACL 적용, 실행기 작성을 하지 않았습니다. 실제 운영 계정·기계가 정해지기 전에는 구현 가능 여부를 확정하지 않습니다.
- 상위 문서: [G5-TEMP-01 보호 모델](B1_G5_TEMP01_PROTECTION_MODEL.md)(보안 목표·S0~S9·M-12A~J·C-3). 이 문서는 그 3절을 운영 구성으로 구체화합니다.
- OWNER 우선 후보는 **A(보호된 로컬 S4U 실행기)** 입니다. A의 강제 경계가 성립하지 않으면 **B(보완 C-3)** 전환을 제안합니다(4절).
- 표기: **[사실]** 코드·실측 · **[문서 근거]** Windows 공개 동작, 미실측 · **[실측]** 운영 기계 측정 필요 · `OWNER_VALUE_PENDING`
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · G5-TEMP-01 = **OPEN_MAJOR**

## 1. 역할과 계정

| 역할 | 계정 | 용도 | 금지 |
|---|---|---|---|
| OWNER 관리 세션(TCB) | 운영 기계 관리자, 승격 세션 | 계정 생성, 로그온 권한, 작업 등록·보안 설명자, ACL Apply, 요청 배치, 작업 실행 | 검증기·생산자 실행 |
| **VERIFIER** | **검증기 전용 로컬 표준 사용자**(S4U 작업의 주체) | 시작 스크립트와 검증기 실행만 | 대화형·원격·네트워크·서비스 로그온, Git Clone, CI 빌드, 다른 프로그램 실행, 관리자 그룹 |
| DEPLOYER | 별도 로컬 표준 사용자(대화형 가능) | 공식 원격 Clone, CI 빌드, 스테이징(네트워크 필요) | R-BUILD 봉인 후 쓰기, 검증기 실행 |
| PRODUCER·READER·REGISTRY | 기존 G5 설계 2절 | 기존 | 기존 |

- VERIFIER와 DEPLOYER는 다른 SID입니다. 개발 계정(현재 관리자 그룹 구성원)은 어느 역할에도 쓰지 않습니다.
- **S4U 제약 반영 [문서 근거]:** S4U 로그온 토큰에는 네트워크 자격 증명이 없어 원격 자원(SMB 공유, 인증 프록시 등)에 접근하지 못하고, 사용자 암호로 보호되는 비밀(EFS 암호화 파일, 사용자 DPAPI 비밀)도 쓸 수 없습니다.
  - 공식 Git Clone·CI 빌드(네트워크 필요)는 DEPLOYER가 합니다. VERIFIER로 하지 않습니다.
  - 검증기 경로는 로컬만 씁니다. **[사실]** Test Profile은 빌드된 DLL에 `dotnet test <assembly>`를 실행하므로(`TestProfiles.cs` 273행) 복원(NuGet)이 없습니다. Registry는 로컬 Named Pipe입니다.
  - 보호 Root는 EFS 암호화를 쓰지 않습니다(M-12B `cipher /c`).
  - 남는 확인 **[실측]**: 사용자 프로필 로드 여부, `dotnet` 첫 실행 처리, 원격 측정(Telemetry) 시도(`DOTNET_` 변수는 E2-1이 금지하므로 끄는 변수를 쓸 수 없음), 검증기가 끝까지 동작하는지 — M-12A

## 2. 계정·권한·작업 구성

### 2.1 계정 생성·관리 (OWNER 관리 세션, 승인 B 뒤)

1. 로컬 표준 사용자 생성: 이름 `OWNER_VALUE_PENDING`. 생성 순간 32자 이상 무작위 암호를 설정하고 기록·표시·저장하지 않습니다. 암호 만료 없음, 사용자 암호 변경 불가.
2. 그룹: 기본 `Users`만. Administrators, Remote Desktop Users, Backup Operators 등 비구성원(`Get-LocalGroupMember`로 증거).
3. 계정 관리(암호 재설정·비활성화·삭제)는 TCB만 할 수 있습니다. 암호 재설정은 TCB 행위이며, 재설정 사실은 M-12J 재검증 조건입니다.

### 2.2 로그온 권한 (로컬 보안 정책)

| 권한 | 설정 | 이유 |
|---|---|---|
| `SeBatchLogonRight`(일괄 작업으로 로그온) | VERIFIER 허용 | S4U 작업 실행에 필요 **[문서 근거]** |
| `SeDenyInteractiveLogonRight` | VERIFIER 거부 | `runas`·콘솔 |
| `SeDenyRemoteInteractiveLogonRight` | VERIFIER 거부 | RDP |
| `SeDenyNetworkLogonRight` | VERIFIER 거부 | SMB·원격 Pipe·WinRM 등 |
| `SeDenyServiceLogonRight` | VERIFIER 거부 | 서비스 등록 경로 |

- 변경 주체: TCB만. 적용 방법(`secedit /configure` 또는 `ntrights` 대체 스크립트)과 결과(`secedit /export /areas USER_RIGHTS`)는 승인 B 증거입니다.

### 2.3 S4U 예약 작업

- **등록 자격 [문서 근거]:** 다른 계정을 주체로 하는 S4U 작업은 관리자(TCB)가 등록합니다. VERIFIER 암호는 필요하지 않습니다(`New-ScheduledTaskPrincipal -UserId <VERIFIER> -LogonType S4U`). 작업 정의는 `%SystemRoot%\System32\Tasks` 아래에 저장되며 운영체제 ACL로 보호됩니다.
- **작업 설정:** 트리거 없음(요청 시 실행만), 동시 인스턴스 `IgnoreNew`, 실행 시간 제한 `OWNER_VALUE_PENDING`, 네트워크 조건 없음, 전원 조건 없음, 최고 권한 실행 안 함(`RunLevel Limited`).
- **작업 보안 설명자(SDDL, `OWNER_VALUE_PENDING`):** 읽기·실행·수정·삭제·DACL 변경 = SYSTEM·Administrators(OWNER 관리 세션)만. 등록 시 COM `ITaskFolder.RegisterTaskDefinition`의 `sddl` 인자로 지정하고 `schtasks /query /xml`과 작업 보안 조회로 확인합니다 **[문서 근거, 실측]**.
- **동작:** `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File <R-CONFIG>\launcher\start-verifier.ps1 -Request <id>`
  - 호스트는 OS 구성 요소인 Windows PowerShell 5.1입니다(추가 설치 런타임을 TCB에 넣지 않음). 경로·버전·SHA를 승인 B에 기록합니다.
  - 실행 정책 우회는 TCB가 정한 작업 동작에만 있습니다. 스크립트 무결성은 R-CONFIG ACL(TCB만 쓰기)과 S3의 SHA 대조로 확인합니다.
  - 시작 스크립트는 자동 로드 모듈에 의존하지 않도록 .NET API와 내장 명령만 씁니다. 사용자 모듈 경로(`Documents\WindowsPowerShell\Modules`)는 VERIFIER만 쓸 수 있지만, 이 경로에서 로드하지 않도록 첫 줄에서 `PSModulePath`를 시스템 경로로 고정합니다(설계).

### 2.4 보호 실행기 파일 (NOT_IMPLEMENTED)

| 파일 | 위치 | 쓰기 | SHA |
|---|---|---|---|
| 시작 스크립트 `start-verifier.ps1` | `<R-CONFIG>\launcher\` | TCB만 | `OWNER_VALUE_PENDING`(구현·검수 후) |
| 실행기 설정 `launcher-config.json`(승인 SID, 검증기·`dotnet` 경로·SHA, `install-config` SHA, 기대 SDDL, 세션 Root, 시간 제한) | `<R-CONFIG>\launcher\` | TCB만 | `OWNER_VALUE_PENDING` |
| 요청 `<id>.json`(명령·인자의 닫힌 형식) | `<R-CONFIG>\requests\` | TCB만 | 요청마다 기록 |
| 작업 정의 XML | `%SystemRoot%\System32\Tasks\<경로>` | OS·TCB | `OWNER_VALUE_PENDING` |
| 실행기 로그 | `<R-WORK>\launcher-logs\` | VERIFIER(덮어쓰기 없음) | 세션마다 |

- 저장소 원본 위치(구현 시): `tools/b1-ops/`(제품 빌드 밖, `eol=lf`). 저장소 원본 SHA = 설치본 SHA를 S3와 M-12J가 확인합니다.

## 3. 보안 조건별 구성

| 조건 | 구성 | 강제 주체 | 확인 |
|---|---|---|---|
| 검증기 SID로 프로세스를 만드는 경로 | 유일한 경로 = 2.3 작업. 나머지(암호 로그온, 서비스, 다른 작업, S4U 직접 호출, 주입)는 [보호 모델](B1_G5_TEMP01_PROTECTION_MODEL.md) 3.3으로 차단 | OS(LSA, 작업 스케줄러) | M-12E·F |
| 같은 SID의 다른 프로세스 없음 | 대화형 로그온 거부로 Run 키·시작 폴더 경로 없음. 그 주체의 작업·서비스 0. `IgnoreNew`. S5에서 시작 시 확인 | OS + 시작 시점 검사 | M-12E(세션 중 시계열) |
| 보호 TEMP/TMP | `<R-WORK>\verifier-tmp\<session-id>`를 세션마다 새로 만들고 자식 환경에 절대경로로 지정 | 시작 스크립트(S6·S7) | M-12A·D |
| 부모 경로·하위 파일 ACL | 드라이브 Root ~ `verifier-tmp`: 비TCB·비VERIFIER 쓰기 계열 0, `verifier-tmp` 보호 DACL, 세션·파일 상속 ACE만 | OS 접근 검사(G5 Apply) | M-12B·C |
| Reparse·DeleteChild·Rename·Replace 차단 | 위 ACL로 비TCB·비VERIFIER는 생성·삭제·이름 변경 불가. S4·S6이 Reparse 0 확인. 검증기 `PinnedDirectory`는 기록 시점에 Reparse 거부 | OS + 시작 시점 검사 | M-12B·C·H |
| 기존 열린 쓰기 핸들·동시 Writer | 세션 디렉터리는 항상 새 객체. 잠금 파일 단독. 같은 SID 프로세스 없음 | 시작 스크립트 + OS | M-12D·E |
| 실행 중 보안 조건 유지 | **예방은 OS 접근 검사뿐**(ACL + SID 전용). 시작 스크립트는 실행 중 예방을 더하지 못함 | OS | M-12G·H·I |
| 인증 결과 등록 전 검증 | **시작 전 검사(S1~S6)만 가능.** 등록은 검증기 내부(`Receipts.Issue`)라 그 직전에 끼어들 수 없음. S8은 등록 뒤 탐지 | — | 보호 모델 3.4 충돌 기록 |
| Fail-Closed | 확인 불가·예외·시간 초과·설정 누락·SHA 불일치 = 검증기 미실행. 부분 생성 디렉터리 정리. 종료 코드 ≠ 0 | 시작 스크립트 | M-12F |

## 4. 구현 경로 비교 (A: 보호된 로컬 S4U 실행기 / B: 보완 C-3)

| 비교 항목 | A. 로컬 S4U 실행기 | B. 보완 C-3 코드 수정 |
|---|---|---|
| 실제 수정 파일 | 제품 0. 새 운영 파일: `tools/b1-ops/start-verifier.ps1`, 실행기 설정 형식, 작업 등록 절차 스크립트(작업 XML·SDDL), `tools/b1-ops/selftest.ps1` 사례 추가. G5 레이아웃 데이터에 `verifier-tmp`·`launcher`·`requests` 추가(도구 코드 불변 예상, 확인 필요). 문서 | `tools/StarPlayer.SimHarness/FamilyOfficial.cs`(`FamilyOfficialReplay`), `tools/StarPlayer.SimHarness/DominanceOfficial.cs`(`DominanceOfficialReplay`), `tools/StarPlayer.EvidenceVerifier/Verifier.cs`(TEMP 제거·버퍼 진입점 호출·3~5 대조), 필요 시 SimHarness에 Replay 입력 형식 1개. 시험: `tests/StarPlayer.SimHarness.Tests/FamilyOfficialTests.cs`·`DominanceOfficialTests.cs`·`DominanceOfficialTests.Fix1.cs`, `tests/StarPlayer.EvidenceVerifier.Tests/VerifierTests.cs`. `tools/b1-ops/g1-names-check.ps1` 고정값 재산출(세 파일이 G1 고정 파일) |
| 구현 난이도 | 중. 스크립트 분량은 작지만 보안 판단이 많음: SDDL 정확 비교, SID별 프로세스 열거, 작업 보안 설명자, Fail-Closed 경로. 실제 역할 계정 없이 개발 PC에서 충분히 시험하기 어려움 | 중. Replay 진입점 추가와 배선, 대조 3~5. 기존 Replay 판정 논리는 그대로. 개발 PC에서 전부 시험 가능 |
| 운영 복잡도 | 높음: 계정·로그온 권한·작업·작업 보안·실행기 설정·요청 배치·세션 관리, 변경마다 M-12J | 낮음: 새 운영 구성요소 없음. 재생 디렉터리가 없어짐 |
| 추가 신뢰 구성요소 | 작업 스케줄러 서비스, LSA S4U, 로컬 보안 정책, `powershell.exe` 5.1과 그 구성, 시작 스크립트·설정, R-CONFIG ACL, "같은 SID 다른 프로그램 없음" 운영 규칙 | 없음(TEMP를 신뢰 경로에서 제거). 기존 Snapshot 핸들 고정 읽기에 의존 |
| 시험 범위 | 실행기 격리 자체 시험(개발 PC), 운영 기계 M-12A~J(역할 계정 3종 포함, 공격·대조 실험), Codex 실행기 검수 | 새 단위 시험(버퍼 변조 거부, 경로 진입점과 결과 동일, TEMP 미생성), `verify.ps1` 전체(xUnit 3,061+), Golden 4/4, Dry Pilot 9/9, G1 도구 재고정, Codex 코드 검수 |
| 잔여 위험 | 세션 중 예방이 ACL·SID 전용에만 의존. S8은 등록 뒤 탐지(보호 모델 3.4 충돌). TCB 설정 오류. PowerShell 호스트 표면. S4U 동작 미확인(M-12A) | Replay가 저장소 Root에서 읽는 승인 입력은 기존대로(G5 R-BUILD ACL). 코드 변경 자체의 회귀 위험(시험으로 관리) |
| `c77ed1a` 유지 | **유지** | **불가.** 새 제품 Commit → SIMHARNESS_BINARY·VERIFIER_BINARY 변경, B-4 후보 재산출, 공식 G2/G3 새 Commit으로 재실행, G1 도구 새 SHA, 공식 소스 후보(A1-6) 재결정, 게시(Push) 별도 승인 |
| G5-TEMP-01 해소 방식 | 운영 실측(M-12A~J) 통과 시. 운영 기계마다, 설정 변경마다 재확인 | 코드로 판정 경로에서 TEMP 제거. 계정 형태와 무관 |

- **판단:** OWNER 우선 후보 A로 진행합니다. 다음 중 하나가 확인되면 B 전환을 제안합니다.
  1. 운영 기계에서 3.3 우회 차단(M-12F) 또는 세션 중 예방 조건(M-12E·G·H·I)이 성립하지 않음
  2. S4U에서 검증기가 끝까지 동작하지 않음(M-12A)
  3. Codex 또는 OWNER가 "등록 전 검증"을 필수로 판단함(보호 모델 3.4) — A는 코드 변경 없이 이를 만족할 수 없습니다

## 5. `c77ed1a` 유지 가능 조건

[보호 모델](B1_G5_TEMP01_PROTECTION_MODEL.md) 6절의 7개 조건이 모두 참일 때만 유지합니다. 요약: Codex 운영 경로 수용, B-5 = 로컬 S4U(3.3 조건), 실행기 구현·검수·설치, M-12A~J 통과(예방 조건 포함), 3.4 충돌 권고 수용, 시작 절차로만 인증, 변경 시 M-12J 재확인.

## 6. 승인 B에 필요한 실제 값

기존 [운영 전환 계획](B1_PHASE3_OPERATIONS_TRANSITION.md) 3.1절 B-1~B-14에 G5-TEMP-01·G1 공식 감사용 값 B-15~B-22를 더합니다. 모두 `OWNER_VALUE_PENDING`이며 Claude가 정하지 않습니다.

| # | 값 | 쓰이는 곳 |
|---|---|---|
| B-1 | 운영 Windows 기계 이름·OS 빌드·볼륨(NTFS, EFS 미사용) | 모든 Root, G8 Profile, M-12B |
| B-2 | 보호 Root 실제 경로: R-CONFIG, R-BASE, R-TRUST, R-BUILD, R-WORK, R-ARCHIVE, R-AUDIT, R-DOTNET, R-GIT | 레이아웃, 설치 설정 |
| B-3 | TCB 계정·OWNER 관리 세션 | 계정·권한·작업·ACL 작업 |
| B-4 | OWNER 서명 주체 SID | Registry |
| **B-5** | **VERIFIER = 로컬 S4U 전용 표준 사용자**: 계정 이름·SID | `verifier_principal_sid`, 작업 주체, R-WORK writer |
| B-6 | Registry 계정·서비스 SID | §17 |
| B-7 | PRODUCER·시험 실행 계정 SID | R-ARCHIVE |
| B-8 | DEPLOYER(네트워크 Clone·CI 빌드)·READER SID | R-BUILD 스테이징, 증거 읽기, 공식 G1·G2·G3 실행 |
| B-9 | R-ARCHIVE 계약상 권한 | 레이아웃 |
| B-10~B-14 | Git 체인, 외부 실행 모듈 Pin, ETW 결과 채택, Trust Anchor, 설치 설정 값 | G4 |
| B-15 | 로컬 보안 정책: VERIFIER 배치 로그온 허용과 거부 권한 4종(2.2절) | 3.3 우회 차단 |
| B-16 | 예약 작업 경로·이름, 정의 XML SHA, 작업 보안 설명자 SDDL, 실행 시간 제한 | S0, M-12F·J |
| B-17 | 시작 스크립트·실행기 설정의 경로와 SHA(구현·검수 후) | S3, M-12J |
| B-18 | 요청 디렉터리 `<R-CONFIG>\requests` 경로·SDDL | S1 |
| B-19 | `verifier-tmp` 경로, 부모·세션 디렉터리·파일의 기대 SDDL, `OWNER RIGHTS` 사용 여부 | S4·S6, M-12B |
| B-20 | 실행기 호스트 `powershell.exe` 경로·버전·SHA | S0, M-12J |
| B-21 | 실행기 로그 위치 `<R-WORK>\launcher-logs`, S8 위반 시 보고·중단 절차(보호 모델 3.4) | S8·S9 |
| B-22 | 공식 G1 감사: 실행 계정(DEPLOYER 권고), PowerShell 실제 경로·버전·SHA, 감사 도구 위치와 SHA(`d3945b5e…6ba0`), 대상 Commit·Tree, 로그 보존 위치 | G1 공식 감사 |
