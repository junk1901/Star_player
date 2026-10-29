# B1 G5-TEMP-01 운영 보호 모델·강제 시작 경계·M-12A~J (DRAFT)

- 작성: Claude Code, 2026-10-30. 근거: OWNER 지시 "Codex FIX_REQUIRED 후속 조치"(2026-10-30) 3·4항
- **성격:** 설계·분석 문서입니다. 운영 ACL 적용, 서비스·작업 등록, Trust Anchor·Pin 등록, 공식 인증 실행을 **하지 않았습니다**. 제품·G4·G5 코드와 `c77ed1a`를 바꾸지 않았습니다.
- 기준: Codex 판정 G5-TEMP-01 = **OPEN_MAJOR**. 이 문서의 모든 운영 항목은 운영 기계 실측 전까지 `미검증`이며, Codex가 운영 경로를 받아들이더라도(`OPERATIONAL_PATH_ACCEPTABLE`) M-12A~J 실측 통과 전에는 **OPEN_MAJOR가 유지**됩니다.
- 관련: [정책 준비 보고서](../reports/B1_G1G2_POLICY_PREP_REPORT.md) 3절(최초 분석), [운영 전환 계획](B1_PHASE3_OPERATIONS_TRANSITION.md) 3.2절(M-1~M-12), [G5 운영 설계](B1_G5_OPERATIONAL_DESIGN_DRAFT.md) 1~3절(Root·역할)
- 표기: **[사실]** 코드·실측으로 확인 · **[문서 근거]** Windows·.NET 공개 동작, 이 저장소에서 실측하지 않음 · **[실측]** 운영 기계 측정 필요 · **NOT_IMPLEMENTED** 필요한 절차·도구가 아직 없음
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 보안 목표와 공격 경로

**보안 목표:** 공식 인증 프로세스가 판정 입력으로 쓰는 임시 실행 색인(`family-result.json`·`dominance-result.json`)과 Unit Manifest(`…/manifest.json`)가 작성된 뒤 Replay가 다시 읽을 때까지, 비신뢰 주체가 이를 변경·교체할 수 없어야 합니다. 그래서 최초 Archive Snapshot과 Replay 결과의 결속이 깨지지 않아야 합니다.

**[사실] 코드 경로 (`c77ed1a`, 변경 없음):**

| 단계 | 위치 | 보호 |
|---|---|---|
| 1. 재생 Root 결정 | `Verifier.cs` 245행 `Path.Combine(Path.GetTempPath(), "starplayer-verifier", <guid>)` | 없음. 프로세스 환경(`TMP`→`TEMP`→…)이 위치를 정함 |
| 2. 기록 | `Archive.WriteNew` → `PinnedDirectory.Open(…, createRoot: true)` | 볼륨 Root부터 핸들 고정, Reparse 거부, 파일은 `FILE_CREATE`(덮어쓰기 없음), 다시 읽어 바이트 대조. **쓰기가 끝나면 핸들을 닫음** |
| 3. 재읽기 | `FamilyOfficialReplay.Verify`·`DominanceOfficialReplay.Verify`가 `File.Exists`·`File.ReadAllBytes`로 경로를 다시 엶 | 경로 기반(링크를 따라감). 색인은 Snapshot SHA와 대조하지 않음. Manifest는 **TEMP에서 읽은 색인**의 `manifest_sha256`과만 대조 |

- 2와 3 사이에는 핸들이 없습니다. 이 구간에 디렉터리·파일을 바꿀 수 있는 주체는 색인과 Manifest를 **함께** 교체해 3의 대조를 통과시킬 수 있습니다.
- `starplayer-verifier` 상위 디렉터리는 `FileOpenIf`로 열립니다. 다른 주체가 **미리 만들어 둔** 일반 디렉터리(Reparse 아님)도 그대로 쓰며, 새 `<guid>` 디렉터리는 그 디렉터리의 상속 ACL을 받습니다.
- **[사실] C0/C1/C2 환경 digest는 보호를 증명하지 않습니다.** `ExecutionEnvironment.Capture`의 digest는 모든 환경 변수(TMP 포함)를 담지만, Profile 대조(`ExecutionEnvironment.Mismatch`)는 OS·Runtime·`ucrtbase`·Runtime 파일만 비교합니다. digest는 세션 안의 C0 = C1 = C2 비교에만 쓰입니다. 즉 TMP **값**이 승인값인지도, 그 디렉터리의 **ACL**도 검증기가 확인하지 않습니다.

**공격 경로(비신뢰 주체 기준):**

| # | 경로 | 차단 조건 |
|---|---|---|
| A1 | TEMP 디렉터리에 다른 주체의 Write·Delete·DeleteChild 권한(개발 PC 사용자 Temp 실측과 같은 상속 Modify) | 보호 Root 하위 + 보호 DACL(2.1) |
| A2 | `starplayer-verifier`를 미리 만들어 소유(Owner 권한으로 DACL 변경) | 세션마다 새로 만든 전용 TEMP(2.2) |
| A3 | 상위 경로의 Junction·Symlink·Mount Point, 상위 디렉터리 이름 변경·교체 | 상위 체인 Reparse·DeleteChild 검사(2.1, M-12A·B·C) |
| A4 | 보호 적용 전부터 열려 있던 쓰기·`WRITE_DAC` 핸들 | 세션 디렉터리를 보호 적용 **뒤** 새 객체로 생성(2.2), 기존 핸들 조사(M-12D) |
| A5 | 같은 검증기 SID로 실행되는 다른 프로그램(ACL은 같은 SID를 구분하지 못함) | SID 전용·동시 실행 차단(2.3) |
| A6 | 보호 절차를 거치지 않고 검증기 SID로 직접 실행(예: `runas`, 다른 TMP 값) | 강제 시작 경계(3절) |
| A7 | 실행 중 ACL·Owner 변경 | 변경 권한은 TCB와 검증기 SID에만 있음(2.1·2.3). 탐지는 사후 대조(M-12G) |

- TCB(SYSTEM·Administrators·TrustedInstaller)는 신뢰 경계 안입니다. TCB가 바꾸는 경우는 이 모델이 막지 않습니다(기존 G5 설계와 같음).

## 2. 코드 변경 없는 운영 보호 모델 (기존 검증기 바이너리 유지)

### 2.1 보호 Root

```text
<R-WORK>\                         G5 Apply 대상(TCB 소유, 보호 DACL)
└─ verifier-tmp\                  TCB: Full · VERIFIER: 하위 폴더·파일 Modify(이 폴더 자체의 DELETE·WRITE_DAC·WRITE_OWNER 없음)
   └─ <session-id>\               시작 절차가 세션마다 새로 만듦(상속 ACL만, 소유자 = VERIFIER) → TMP=TEMP=이 경로
      └─ starplayer-verifier\<guid>\   검증기가 만듦(코드 변경 없음)
```

- `verifier-tmp`와 그 상위(R-WORK·R-BASE·드라이브 Root)는 비TCB·비VERIFIER 주체에게 `WRITE_DATA`·`APPEND_DATA`·`DELETE`·`DELETE_CHILD`·`WRITE_DAC`·`WRITE_OWNER`를 주지 않습니다. 기존 G5 상위 경로 규칙(NSC-4)·`g5-acl.ps1 Verify`·`g5-probe.ps1`이 이 조건을 검사합니다.
- 세션 디렉터리의 소유자는 VERIFIER입니다. 소유자의 암묵 권한(`READ_CONTROL`·`WRITE_DAC`)은 VERIFIER SID에만 생깁니다. 선택적 강화: `OWNER RIGHTS`(S-1-3-4) ACE로 암묵 권한 제한(**[실측]** 검증기 동작 영향 확인 필요).
- 같은 Root를 TRX에 쓰는 기존 G4 경로(`Certification.RunTestProfile(…, config.WorkRoot)`)와 같은 보호 수준입니다.

### 2.2 세션별 새 TEMP

- 시작 절차가 인증 직전에 `verifier-tmp\<session-id>`를 **새로** 만듭니다(이미 있으면 거부). 따라서 A2·A4의 미리 만든 객체·기존 핸들이 새 세션 디렉터리에 닿지 않습니다.
- 자식 프로세스 환경에 `TMP`·`TEMP`를 같은 **절대경로**로 넣습니다. 서비스 계정 프로필의 사용자 환경 변수(`HKCU\Environment`)에 의존하지 않습니다.
- **[문서 근거]** .NET 8 `Path.GetTempPath()`는 Windows에서 `GetTempPath2W`를 쓰며(설치된 `System.Private.CoreLib.dll` 8.0.31에 이 이름이 있음, 개발 PC 확인), SYSTEM이 아닌 프로세스에서는 `TMP` → `TEMP` → `USERPROFILE` → Windows 디렉터리 순입니다. 실제 값은 M-12A에서 프로세스 기준으로 실측합니다.

### 2.3 SID 전용·동시 실행 차단

- 검증기 SID(`verifier_principal_sid`, B-5)는 검증기와 그 승인 실행 체인(`dotnet`, Test Profile의 `testhost`, Git 체인 — G4 `execution_pins`)만 씁니다. 다른 프로그램·예약 작업·서비스와 공유하지 않습니다.
- 한 번에 한 세션만: 시작 절차가 `R-WORK\verifier-tmp\.lock`을 `FileShare.None`으로 열어 세션 끝까지 유지하고, 같은 SID의 다른 `dotnet`·`powershell`·검증기 프로세스가 있으면 시작하지 않습니다. 작업 설정의 동시 인스턴스 정책도 "새 인스턴스 시작 안 함"으로 둡니다.

## 3. 강제 실행 경계 (OWNER 통제 시작 절차)

### 3.1 경계의 핵심

- Registry는 연결한 클라이언트 토큰의 SID가 `verifier_principal_sid`이면 검증기 역할로 받습니다(`PipeRegistry.cs` 427·492행 **[사실]**). 따라서 **검증기 SID 토큰을 가진 프로세스를 만들 수 있는 모든 방법**이 공식 인증 시작 경로입니다.
- 운영 통제가 충분하려면, 비TCB 주체가 시작 절차를 거치지 않고 검증기 SID 프로세스를 만들 수 없어야 합니다. 이것이 3.3의 계정 형태 조건입니다.

### 3.2 시작 절차 (NOT_IMPLEMENTED, 설계)

시작 절차는 **보호된 예약 작업 하나**와 그 작업이 실행하는 **보호된 시작 스크립트 하나**입니다. 둘 다 아직 없습니다. 만들면 새 운영 도구이므로 별도 TASK·자체 시험·Codex 검수 뒤 승인 B에서 설치합니다.

| 단계 | 내용 | 실패 시 |
|---|---|---|
| S0 | 작업: 주체 = 검증기 계정, 로그온 형식 S4U(암호 저장 없음) 또는 gMSA. 동작 = `powershell.exe -NoProfile -NonInteractive -File <R-CONFIG>\launcher\start-verifier.ps1`. 작업 보안 설명자: 읽기·실행·수정 = TCB·OWNER 관리 세션만 | 작업 등록 안 함 |
| S1 | 요청 읽기: `<R-CONFIG>\requests\<id>.json`(TCB만 쓰기)에서 명령·인자. 형식 밖이면 거부 | 실행 거부, 로그 |
| S2 | 실행 계정: `whoami /user` SID = 승인 `verifier_principal_sid`, `whoami /groups`에 Administrators 활성 없음, 비승격 | 실행 거부 |
| S3 | 승인 바이너리·설정: 검증기 DLL·`dotnet.exe` SHA = 승인 Pin, `install-config.json` SHA = 승인값(`InstallConfig.RawSha256`) | 실행 거부 |
| S4 | 보호 확인: `verifier-tmp`와 상위 전체 — 객체 종류(디렉터리, Reparse 아님), 소유자, DACL을 승인 SDDL과 정확히 비교, 비TCB·비VERIFIER의 Write·Delete·DeleteChild·WRITE_DAC·WRITE_OWNER 0 | 실행 거부 |
| S5 | 동시 실행 차단: 잠금 파일 단독 획득, 같은 SID의 다른 프로세스 0 | 실행 거부 |
| S6 | 세션 TEMP: `verifier-tmp\<session-id>` 새로 생성 → 실제 SDDL이 상속 기대값과 같음, Reparse 아님 | 생성 디렉터리 삭제 후 거부 |
| S7 | 실행: 자식 환경 = 승인 환경 + `TMP`=`TEMP`=세션 경로, 금지 접두사(`DOTNET_`·`COMPlus_`) 없음. 검증기를 승인 절대경로 `dotnet.exe`로 시작하고 종료 코드·출력 보존 | — |
| S8 | 사후 대조: 세션 디렉터리와 상위의 소유자·SDDL이 S4·S6 값과 같은지, 남은 항목 목록. 다르면 **해당 결과를 공식으로 쓰지 않음**(OWNER 보고) | 결과 무효 |
| S9 | 정리: 세션 디렉터리 삭제, 잠금 해제, 로그(R-WORK\launcher-logs, 덮어쓰기 없음) | — |

**지시 3.3의 각 강제 항목에 대한 판단:**

| 항목 | 시작 절차로 강제 가능? | 근거·한계 |
|---|---|---|
| TEMP 보호 검사 | 예(S4·S6) | 시작 시점 검사. 실행 중 유지는 아래 행 |
| 실행 계정 검사 | 예(S2) | 작업 주체가 고정. 검증기 자체도 Registry 역할로 다시 확인됨 |
| 승인 Verifier 바이너리 검사 | 예(S3) | G4 V-2(비Pin 모듈 거부)·앵커 검사와 중복 확인 |
| 승인 설정 검사 | 예(S3) | 설정 위치는 고정(`%ProgramData%\…\install-config.json`) |
| 다른 Writer·동시 실행 차단 | 예(S5, 2.3) | 같은 SID의 **다른 프로그램 부재**는 운영 규칙 + M-12E 실측 |
| 검사 성공 후 실행·실패 시 거부 | 예(S1~S7 순서) | 스크립트 논리. 스크립트 무결성은 R-CONFIG ACL(G5 M-10) |
| 실행 중 보호 유지 | **예방은 ACL 구조로만**, 시작 절차는 **사후 탐지**(S8) | 실행 중 변경 권한은 TCB·VERIFIER SID뿐(2.1·2.3). 변경 후 원복은 S8이 못 볼 수 있음 → M-12G·H·I로 권한 자체가 없음을 실측 |

### 3.3 우회 가능성과 계정 형태 조건

| 우회 방법 | 막는 조건 | 막지 못하면 |
|---|---|---|
| 암호로 로그온(`runas`, 대화형, RDP, 네트워크, 서비스 등록) | 계정 암호를 무작위로 만들고 기록하지 않음. `SeDenyInteractiveLogonRight`·`SeDenyRemoteInteractiveLogonRight`·`SeDenyNetworkLogonRight`·`SeDenyServiceLogonRight` 부여, `SeBatchLogonRight`만 허용 **[문서 근거]** | 운영 통제 **불충분** |
| S4U로 토큰 획득 | 비TCB 호출자는 S4U로 Identification 수준 토큰만 얻고 프로세스를 만들 수 없음(프로세스용 토큰은 `SeTcbPrivilege` 필요) **[문서 근거]** | — |
| 같은 계정으로 다른 예약 작업 등록 | 다른 계정 주체 작업 등록에는 그 계정 암호 또는 관리자 권한 필요 **[문서 근거]** | 운영 통제 불충분 |
| 기존 작업 수정·임의 실행 | 작업 보안 설명자 = TCB·OWNER만(S0) | 운영 통제 불충분 |
| 실행 중인 검증기 프로세스에 주입 | 같은 SID 또는 `SeDebugPrivilege`(TCB) 필요. 같은 SID는 2.3으로 배제 | — |

- **결론(설계 판단, 미실측):**
  - B-5 계정 형태가 **S4U 전용 로컬 표준 사용자** 또는 **gMSA**이고 위 조건이 모두 실측되면, 우회에는 TCB가 필요하므로 운영 경계로 충분하다고 판단합니다.
  - B-5가 **암호를 아는 로컬 표준 사용자**이면 `runas`로 다른 `TMP`를 준 검증기를 시작할 수 있으므로 **불충분**합니다.
  - **가상 계정(`NT SERVICE\<이름>`)** 은 SCM이 시작하는 서비스 프로세스에만 토큰이 생깁니다. 현재 검증기는 콘솔 프로그램이고 Windows에는 범용 서비스 호스트가 기본 포함되지 않습니다. 이 형태는 **새 서비스 호스트 코드**가 필요하므로 코드 변경 없는 경로에 들지 않습니다(예약 작업이 가상 계정 주체를 지원하는지는 확인하지 않았습니다 — 미확인).
- 시작 절차 자체(예약 작업 + 시작 스크립트)가 아직 없습니다(NOT_IMPLEMENTED). 이 절차가 없거나 우회 가능한 상태에서는 공식 인증을 시작하지 않습니다.

## 4. M-12 세부 실측 (M-12A~J)

- 공통: 운영 기계, 승인 B 뒤. OWNER 관리 세션은 승격, 역할 계정은 비승격 세션. 결과는 R-AUDIT와 저장소 밖 진단 디렉터리에 덮어쓰기 없이 보존합니다. 하나라도 실패하거나 `미검증`이면 **공식 인증을 시작하지 않습니다**.
- 개발 PC 결과는 운영 PASS로 승격하지 않습니다. 기존 M-12 단일 행(전환 계획 3.2절)은 이력으로 남기고, 판정은 M-12A~J 전부로 합니다.

| ID | 실측 | 실행 계정 | 명령·방법 | 기대 결과 | 증거 | 실패 시 중단 |
|---|---|---|---|---|---|---|
| M-12A | 실제 TEMP 경로와 전체 부모 경로 | 검증기(시작 절차 경유) | TEST_ONLY 인증 1회를 시작 절차로 실행. ETW Kernel-File(G5 설계 8절 세션)로 `starplayer-verifier\` 생성 경로 수집. 시작 절차 로그의 자식 환경 `TMP`·`TEMP`. 경로의 모든 상위 구성 요소 나열 | 생성 경로가 `<R-WORK>\verifier-tmp\<session-id>\starplayer-verifier\<guid>` 하나뿐. `TMP`=`TEMP`=세션 절대경로 | ETW 원본·`tracerpt` 출력, 시작 절차 로그, 경로 목록 | 다른 위치에 생성되거나 경로를 확정할 수 없음 |
| M-12B | NTFS 객체·Reparse·상속 ACL | OWNER 관리 세션 | 드라이브 Root부터 세션 디렉터리까지 각 구성 요소: `fsutil fsinfo volumeinfo`(NTFS), `fsutil reparsepoint query`, `icacls`, `Get-Acl … .Sddl`. `g5-acl.ps1 -Mode Verify`(R-WORK 포함 레이아웃). 세션 디렉터리와 그 안의 시험 파일 SDDL을 운영체제가 만든 상속값과 비교(M-11 방법) | NTFS, Reparse 0, `verifier-tmp` 보호 DACL(`P`), 세션·파일은 상속 ACE만, 비TCB·비VERIFIER 쓰기 계열 ACE 0 | 구성 요소별 SDDL·`icacls /save`, Verify 보고서 | 하나라도 다름 |
| M-12C | DeleteChild·Rename·Replace 권한 | 생산자·열람자·배포 계정 각각(비승격) | `g5-probe.ps1 -Role <role>`에 `verifier-tmp`·세션 디렉터리 대상 추가 실행. 추가 수동 시도: 세션 디렉터리와 상위의 삭제·이름 변경, 같은 이름 파일 생성, 기존 파일 `MoveFileEx(REPLACE_EXISTING)` 교체, 하드링크 생성 | 모든 시도 `ACCESS_DENIED`, `UNEXPECTED_ALLOWED` 0 | 역할별 Probe 보고서, 시도별 명령·출력·계정·시각 | 허용된 시도 1건 이상 |
| M-12D | 기존 열린 쓰기 핸들 | OWNER 관리 세션 | 보호 적용·세션 생성 전 `openfiles /local on`(재부팅) 뒤 `openfiles /query /v`, R-WORK 아래 핸들 목록. 시작 절차 S6이 새 디렉터리를 만드는지(이미 있으면 거부) 확인 | R-WORK 아래 비TCB·비VERIFIER 핸들 0. 세션 디렉터리는 항상 새 객체 | 조사 출력, 시작 절차 로그 | 기존 핸들 존재 또는 세션 디렉터리 재사용 |
| M-12E | 서비스 계정과 프로세스 격리 | OWNER 관리 세션 | `whoami /all`(검증기 계정, 시작 절차 로그), 로컬 보안 정책의 로그온 권한(`secedit /export` 해당 항목), 예약 작업·서비스 목록에서 같은 계정 주체 검색(`schtasks /query /v /fo csv`, `Get-CimInstance Win32_Service`), 세션 중 같은 SID 프로세스 목록 | 계정은 3.3 조건 충족(거부 권한 4종, 배치 로그온만), 그 계정 주체 작업은 시작 작업 하나, 서비스 0, 세션 중 같은 SID 프로세스 = 승인 실행 체인뿐 | 출력 원본 | 다른 작업·서비스·프로세스가 같은 SID 사용 |
| M-12F | 인증 시작 차단 절차 | OWNER 관리 세션(요청 배치), 검증기(시작 절차) | 실패 주입 각 1회: S2 다른 계정, S3 다른 DLL SHA·설정 SHA, S4 `verifier-tmp`에 시험 ACE 추가, S5 잠금 선점·같은 SID 프로세스, S6 세션 디렉터리 선생성. 추가로 비TCB 계정이 `runas`·작업 실행·작업 수정으로 검증기 SID 실행을 시도 | 각 주입에서 검증기 미실행·Registry 연결 0. 우회 시도 전부 거부 | 시작 절차 로그, Registry 로그, 시도 출력 | 어느 주입에서든 검증기가 실행되거나 우회 성공 |
| M-12G | 세션 중 보호 유지 | OWNER 관리 세션(관찰), 각 역할 계정 | TEST_ONLY 인증 중 1초 간격으로 세션 디렉터리·상위 SDDL·소유자 기록, S8 사후 대조, 같은 시간에 역할 계정의 `WRITE_DAC`·`WRITE_OWNER` 시도 | 기록된 SDDL·소유자 변화 0, 시도 전부 거부, S8 일치 | 시계열 기록, S8 출력 | 변화 또는 허용 |
| M-12H | 다른 계정의 파일 변조 반례 | 생산자·열람자·배포 계정 각각 | TEST_ONLY 인증 중 세션 디렉터리 아래 모든 파일에 대해 반복 시도: 쓰기 열기, 삭제, 이름 변경, 같은 이름 새 파일 생성, 디렉터리 Junction 생성 | 모든 시도 거부, 인증 결과가 무간섭 실행과 같음 | 시도 로그(성공·실패 개수), 두 실행의 결과 비교 | 시도 1건이라도 성공 |
| M-12I | TEMP 색인과 Manifest의 동시 변조 반례 | 생산자 계정(공격), 검증기 | TEST_ONLY 인증 중 색인과 해당 Unit Manifest를 함께 바꾸는 교체 스크립트를 경쟁 실행(Manifest를 다른 정상 실행의 것으로, 색인의 `manifest_sha256`을 그에 맞게). **대조 실험(진단 전용, 비공식 기계):** 같은 스크립트를 보호하지 않은 TEMP에서 돌려 교체가 실제로 일어남을 먼저 확인 | 운영 기계: 교체 시도 전부 거부, 결과 불변. 대조 실험: 교체 성공(시험 장치가 유효함을 확인) | 스크립트·SHA, 두 환경의 시도 로그, 결과 | 운영 기계에서 교체 1건이라도 성공. 대조 실험에서 교체가 안 되면 장치 무효로 M-12I 미검증 |
| M-12J | 재시작·설정 변경 후 재검증 | OWNER 관리 세션 | 운영 기계 재부팅, 작업·스크립트·`install-config`·R-WORK ACL 변경(되돌림 포함) 각각 뒤 M-12A·B·E·F를 다시 실행. 변경 감지: 작업 정의 XML·스크립트·설정 SHA를 승인값과 대조 | 재부팅 뒤 같은 결과. 승인값과 다른 설정에서는 S3·S4가 거부 | 회차별 출력 | 재시작·변경 뒤 결과가 다름 |

## 5. 운영 경로로 해결하지 못하는 경우의 코드 대안 (지금 하지 않음)

제품 코드 변경은 새 TASK, 별도 Commit, 회귀 시험, Codex 독립 검수, OWNER 승인이 필요합니다. 모두 Golden·Hash·RNG·Replay 출력이 바뀌지 않아야 하고(거부 사유만 추가), 바뀌는 바이너리는 공식 G2/G3를 새 Commit으로 다시 측정합니다.

| # | 변경 | 막는 경로 | 바뀌는 바이너리 | 비고 |
|---|---|---|---|---|
| C-1 | 재생 Root를 `GetTempPath()` 대신 설치 설정 `work_root`(R-WORK) 아래 세션 디렉터리로(검증기만) | 환경 변수 의존(A6의 TMP 조작) | VERIFIER_BINARY | ACL·SID 조건은 남음 |
| C-2 | 기록한 디렉터리 체인과 각 파일 핸들을 Replay가 끝날 때까지 유지(공유 = 읽기만, `FILE_SHARE_DELETE` 없음)(검증기만) | 기록 후 교체·삭제·이름 변경(A1·A3·A5·A7) | VERIFIER_BINARY | Replay 코드 불변. 같은 SID 프로세스도 쓰기·교체 불가 **[문서 근거, 실측 필요]** |
| C-3 | Replay가 색인 바이트를 한 번 읽고 그 SHA를 Snapshot 색인 SHA와 대조(기대 SHA 인자 추가). Manifest는 이미 그 색인의 `manifest_sha256`과 대조하므로 사슬이 Snapshot에 고정됨 | 색인·Manifest 동시 교체(M-12I 공격) 전부 | SIMHARNESS_BINARY, VERIFIER_BINARY | 위치·ACL과 무관하게 결속. 가장 작은 신뢰 표면 |
| C-4 | 메모리 바이트로 Replay(경로 재읽기 제거) | 파일 경로 전부 | 둘 다 | 변경 범위가 가장 큼 |

- **권고(설계 판단):** 코드 경로를 택한다면 C-3(필요하면 C-1 추가)입니다. TEMP 위치·ACL·계정 형태와 무관하게 Snapshot과 Replay 입력을 결속합니다.

## 6. 코드 변경 없이 `c77ed1a`를 유지할 수 있는 조건

다음이 **모두** 참이어야 합니다. 하나라도 거짓이면 5절 코드 대안(새 소스 Commit)이 필요합니다.

1. Codex가 운영 경로(2·3절)를 `OPERATIONAL_PATH_ACCEPTABLE`로 판정합니다.
2. OWNER가 B-5 계정 형태를 3.3 조건을 충족하는 형태(S4U 전용 로컬 표준 사용자 또는 gMSA)로 정합니다. 암호를 아는 계정·가상 계정은 해당하지 않습니다.
3. 시작 절차(예약 작업 + 시작 스크립트, 3.2절)를 별도 TASK로 구현하고 자체 시험·Codex 검수를 거쳐 승인 B에서 설치합니다. 이 절차는 운영 도구이며 제품 코드가 아닙니다.
4. 운영 기계에서 M-12A~J가 모두 통과합니다.
5. 공식 인증은 시작 절차로만 실행합니다. 절차 밖 실행 결과는 공식 증거로 쓰지 않습니다.
6. 이후 조건이 바뀌면(재부팅, 설정·계정·ACL 변경) M-12J로 다시 확인하기 전까지 공식 인증을 멈춥니다.

## 7. 공식 G2/G3와의 관계

- G5-TEMP-01은 **공식 인증(G4 공식 이후) 진입**을 막습니다. 검증기 Replay를 쓰지 않는 공식 G2/G3 빌드 재현성 절차와는 별개입니다.
- G1/G2 정책 수용, 공식 소스 Commit 확정, 운영 빌드 환경이 준비되면 G2/G3는 G5-TEMP-01과 무관하게 진행할 수 있습니다.
- 단, 5절 코드 대안을 택하면 공식 빌드 소스 Commit이 바뀌므로 G2/G3는 **새 Commit으로** 다시 합니다. 순서상 G5-TEMP-01 해결 방식(운영 또는 코드)을 G2/G3 공식 실행 전에 정하는 것이 재작업을 줄입니다.
- G5-TEMP-01이 해결되기 전에는 공식 인증·G4 신뢰 경계 검증을 통과 처리하지 않습니다.
