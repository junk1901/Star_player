# B1 G5-TEMP-01 운영 보호 모델·강제 시작 경계·M-12A~J (DRAFT)

- 작성: Claude Code, 2026-10-30. 근거: OWNER 지시 "Codex FIX_REQUIRED 후속 조치"(2026-10-30) 3·4항
- **개정 1 (2026-10-30):** OWNER 지시 "G1/G2 정책 확정 및 G5-MODEL-01/02 후속 조치"와 Codex 판정 G5-MODEL-01(MAJOR)·G5-MODEL-02(MINOR)를 반영했습니다.
  - 운영 경로는 **로컬 S4U 전용 계정만** 검토합니다. gMSA는 이번 운영 선택에서 **제외**하고, 외부 신뢰 범위를 8절에 따로 적었습니다(G5-MODEL-02).
  - C-3 설명을 폐기하고 Snapshot 색인·전체 Unit Manifest 결속 설계로 다시 썼습니다(5절, G5-MODEL-01). **구현하지 않았습니다.**
  - 새 사실: 검증기는 Registry 등록을 실행 안에서 합니다. 시작 절차의 사후 대조(S8)는 항상 등록 **뒤**이므로 등록을 막지 못합니다(3.4절).
  - 바뀐 문장과 이전 문장은 9절 개정 이력에 있습니다. 구성 상세·구현량 비교·승인 B 값은 [로컬 S4U 실행기 구성안](B1_G5_S4U_LAUNCHER_PLAN.md)에 있습니다.
- **성격:** 설계·분석 문서입니다. 운영 계정 생성, 로그온 권한 변경, 예약 작업 등록, 운영 ACL 적용, Trust Anchor·Pin 등록, 공식 인증 실행을 **하지 않았습니다**. 제품·G4·G5 코드와 `c77ed1a`를 바꾸지 않았습니다.
- 기준: Codex 판정 G5-TEMP-01 = **OPEN_MAJOR**. 이 문서의 모든 운영 항목은 운영 기계 실측 전까지 `미검증`입니다. 시작 절차와 M-12A~J가 실제로 구현·실측되기 전에는 RESOLVED로 바꾸지 않습니다.
  - TMP/TEMP를 보호 Root로 지정한 것만으로는 인증을 허용하지 않습니다.
  - S8 사후 대조는 예방 통제를 대신하지 않습니다.
  - 세션 동안 모든 보호 조건을 강제하지 못하면 운영 해결을 수용하지 않습니다.
- 관련: [정책 준비 보고서](../reports/B1_G1G2_POLICY_PREP_REPORT.md) 3절(최초 분석), [운영 전환 계획](B1_PHASE3_OPERATIONS_TRANSITION.md) 3.2절(M-1~M-12), [G5 운영 설계](B1_G5_OPERATIONAL_DESIGN_DRAFT.md) 1~3절(Root·역할)
- 표기: **[사실]** 코드·실측으로 확인 · **[문서 근거]** Windows·.NET 공개 동작, 이 저장소에서 실측하지 않음 · **[실측]** 운영 기계 측정 필요 · **NOT_IMPLEMENTED** 필요한 절차·도구가 아직 없음
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 보안 목표와 공격 경로

**보안 목표:** 공식 인증 프로세스가 판정 입력으로 쓰는 임시 실행 색인(`family-result.json`·`dominance-result.json`)과 Unit Manifest(`…/manifest.json`)가 작성된 뒤 Replay가 다시 읽을 때까지, 비신뢰 주체가 이를 변경·교체할 수 없어야 합니다. 그래서 최초 Archive Snapshot과 Replay 결과의 결속이 깨지지 않아야 합니다.

**[사실] 코드 경로 (`c77ed1a`, 변경 없음):**

| 단계 | 위치 | 보호 |
|---|---|---|
| 0. Snapshot | `Verifier.cs` 168~190행 `Archive.Snapshot`: 제출 문서 참조와 그 문서의 모든 ArtifactRef 파일을 핸들 고정 경로로 **한 번** 읽고 `byte_length`·`raw_sha256`을 참조와 대조 | 있음(이 바이트는 결속됨) |
| 1. 재생 Root 결정 | `Verifier.cs` 245행 `Path.Combine(Path.GetTempPath(), "starplayer-verifier", <guid>)` | 없음. 프로세스 환경(`TMP`→`TEMP`→…)이 위치를 정함 |
| 2. 기록 | Snapshot의 색인·Manifest 바이트를 `Archive.WriteNew`로 기록 → `PinnedDirectory.Open(…, createRoot: true)` | 볼륨 Root부터 핸들 고정, Reparse 거부, 파일은 `FILE_CREATE`, 다시 읽어 대조. **쓰기가 끝나면 핸들을 닫음** |
| 3. 재읽기 | `FamilyOfficialReplay.Verify`·`DominanceOfficialReplay.Verify`가 `File.Exists`·`File.ReadAllBytes`로 경로를 다시 엶 | 경로 기반(링크를 따라감). 색인은 Snapshot SHA와 대조하지 않음. Manifest는 **TEMP에서 읽은 색인**의 `manifest_sha256`과만 대조 |
| 4. 등록 | `Certification.IssueFamily`·`IssuePhase` → `Receipts.Issue` → `registry.Register("VERIFICATION_RECEIPT", …)`(`Receipts.cs` 166행) | 검증기 프로세스 **안에서** 등록. Registry는 INSERT만 허용 |

- 2와 3 사이에는 핸들이 없습니다. 이 구간에 디렉터리·파일을 바꿀 수 있는 주체는 색인과 Manifest를 **함께** 교체해 3의 대조를 통과시킬 수 있습니다.
- `starplayer-verifier` 상위 디렉터리가 이미 있으면 그대로 씁니다. 다른 주체가 **미리 만들어 둔** 일반 디렉터리(Reparse 아님)도 쓰며, 새 `<guid>` 디렉터리는 그 상속 ACL을 받습니다.
- **[사실] C0/C1/C2 환경 digest는 보호를 증명하지 않습니다.** digest는 모든 환경 변수(TMP 포함)를 담지만, Profile 대조(`ExecutionEnvironment.Mismatch`)는 OS·Runtime·`ucrtbase`·Runtime 파일만 비교합니다. digest는 세션 안의 C0 = C1 = C2 비교에만 쓰입니다. TMP **값**이 승인값인지도, 그 디렉터리의 **ACL**도 검증기가 확인하지 않습니다.

**공격 경로(비신뢰 주체 기준):**

| # | 경로 | 차단 조건(운영 경로) |
|---|---|---|
| A1 | TEMP 디렉터리에 다른 주체의 Write·Delete·DeleteChild 권한(개발 PC 사용자 Temp 실측과 같은 상속 Modify) | 보호 Root 하위 + 보호 DACL(2.1) |
| A2 | `starplayer-verifier`를 미리 만들어 소유(Owner 권한으로 DACL 변경) | 세션마다 새로 만든 전용 TEMP(2.2) |
| A3 | 상위 경로의 Junction·Symlink·Mount Point, 상위 디렉터리 이름 변경·교체 | 상위 체인 Reparse·DeleteChild 검사(2.1, M-12A·B·C) |
| A4 | 보호 적용 전부터 열려 있던 쓰기·`WRITE_DAC` 핸들 | 세션 디렉터리를 보호 적용 **뒤** 새 객체로 생성(2.2), 기존 핸들 조사(M-12D) |
| A5 | 같은 검증기 SID로 실행되는 다른 프로그램(ACL은 같은 SID를 구분하지 못함) | SID 전용·동시 실행 차단(2.3) |
| A6 | 보호 절차를 거치지 않고 검증기 SID로 직접 실행(예: `runas`, 다른 TMP 값) | 강제 시작 경계(3절) |
| A7 | 실행 중 ACL·Owner 변경 | 변경 권한은 TCB와 검증기 SID에만 있음(2.1·2.3). 시작 절차는 실행 중 예방을 더하지 못함(3.4) |

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
- 자식 프로세스 환경에 `TMP`·`TEMP`를 같은 **절대경로**로 넣습니다. 계정 프로필의 사용자 환경 변수(`HKCU\Environment`)에 의존하지 않습니다.
- **[문서 근거]** .NET 8 `Path.GetTempPath()`는 Windows에서 `GetTempPath2W`를 쓰며(설치된 `System.Private.CoreLib.dll` 8.0.31에 이 이름이 있음, 개발 PC 확인), SYSTEM이 아닌 프로세스에서는 `TMP` → `TEMP` → `USERPROFILE` → Windows 디렉터리 순입니다. 실제 값은 M-12A에서 프로세스 기준으로 실측합니다.

### 2.3 SID 전용·동시 실행 차단

- 검증기 SID(`verifier_principal_sid`, B-5)는 검증기와 그 승인 실행 체인(`dotnet`, Test Profile의 `testhost`, Git 체인 — G4 `execution_pins`)과 시작 스크립트만 씁니다. 다른 프로그램·예약 작업·서비스와 공유하지 않습니다.
- 한 번에 한 세션만: 시작 절차가 `R-WORK\verifier-tmp\.lock`을 `FileShare.None`으로 열어 세션 끝까지 유지하고, 같은 SID의 다른 프로세스가 있으면 시작하지 않습니다. 작업의 동시 인스턴스 정책도 "새 인스턴스 시작 안 함"(`IgnoreNew`)으로 둡니다.

## 3. 강제 실행 경계 (OWNER 통제 시작 절차, 로컬 S4U 전용)

### 3.1 경계의 핵심

- Registry는 연결한 클라이언트 토큰의 SID가 `verifier_principal_sid`이면 검증기 역할로 받습니다(`PipeRegistry.cs` 427·492행 **[사실]**). 따라서 **검증기 SID 토큰을 가진 프로세스를 만들 수 있는 모든 방법**이 공식 인증 시작 경로입니다.
- 운영 통제가 충분하려면, 비TCB 주체가 시작 절차를 거치지 않고 검증기 SID 프로세스를 만들 수 없어야 합니다(3.3).

### 3.2 시작 절차 S0~S9 (NOT_IMPLEMENTED, 로컬 S4U 설계)

시작 절차는 **보호된 예약 작업 하나**와 그 작업이 실행하는 **보호된 시작 스크립트·설정**입니다. 둘 다 아직 없습니다. 실제 운영 계정·기계가 정해지기 전에는 구현 가능 여부를 확정하지 않습니다. 구성 상세(계정·권한·작업 보안·파일 SHA)는 [S4U 실행기 구성안](B1_G5_S4U_LAUNCHER_PLAN.md) 1~3절입니다.

| 단계 | 내용 | 실패 시(Fail-Closed) |
|---|---|---|
| S0 | 작업: 주체 = 검증기 전용 **로컬** 표준 사용자, 로그온 형식 **S4U**(암호 저장 없음, 로컬 자원만). 트리거 없음(요청 시 실행만). 동작 = 승인 경로의 `powershell.exe -NoProfile -NonInteractive -File <R-CONFIG>\launcher\start-verifier.ps1`. 작업 보안 설명자: 등록·수정·삭제·DACL 변경·실행 = TCB·OWNER 관리 세션만 | 작업 등록 안 함 |
| S1 | 요청 읽기: `<R-CONFIG>\requests\<id>.json`(TCB만 쓰기)에서 명령·인자. 닫힌 형식 밖이면 거부 | 검증기 미실행, 로그 |
| S2 | 실행 계정: 토큰 SID = 승인 `verifier_principal_sid`, 로그온 형식 = 배치(S4U), Administrators 활성 그룹 없음, 비승격 | 검증기 미실행 |
| S3 | 승인 바이너리·설정: 시작 스크립트 설정·검증기 DLL·`dotnet.exe` SHA = 승인값, `install-config.json` SHA = 승인값(`InstallConfig.RawSha256`) | 검증기 미실행 |
| S4 | 보호 확인: `verifier-tmp`와 상위 전체 — 객체 종류(디렉터리, Reparse 아님), 소유자, DACL을 승인 SDDL과 정확히 비교, 비TCB·비VERIFIER의 Write·Delete·DeleteChild·WRITE_DAC·WRITE_OWNER 0 | 검증기 미실행 |
| S5 | 동시 실행 차단: 잠금 파일 단독 획득, 같은 SID의 다른 프로세스 0(시작 스크립트 자신 제외) | 검증기 미실행 |
| S6 | 세션 TEMP: `verifier-tmp\<session-id>` 새로 생성 → 실제 SDDL이 상속 기대값과 같음, Reparse 아님 | 생성 디렉터리 삭제 후 미실행 |
| S7 | 실행: 자식 환경 = 승인 환경 + `TMP`=`TEMP`=세션 경로, 금지 접두사(`DOTNET_`·`COMPlus_`) 없음. 승인 절대경로 `dotnet.exe`로 검증기 시작, 종료 코드·출력 보존 | — |
| S8 | 사후 대조(**탐지 전용**): 세션 디렉터리와 상위의 소유자·SDDL이 S4·S6 값과 같은지, 남은 항목 목록. 다르면 OWNER 보고(3.4의 처리 한계) | 보고 |
| S9 | 정리: 세션 디렉터리 삭제, 잠금 해제, 로그(R-WORK\launcher-logs, 덮어쓰기 없음) | — |

- 확인할 수 없는 값, 예외, 시간 초과, 설정 누락은 모두 **실패**로 처리합니다(Fail-Closed). 실패 시 검증기를 시작하지 않습니다.

**지시의 각 강제 항목에 대한 판단:**

| 항목 | 시작 절차로 강제 가능? | 근거·한계 |
|---|---|---|
| TEMP 보호 검사 | 예(S4·S6) | 시작 시점 검사 |
| 실행 계정 검사 | 예(S2) | 작업 주체가 고정. 검증기도 Registry 역할로 다시 확인됨 |
| 승인 Verifier 바이너리 검사 | 예(S3) | G4 V-2(비Pin 모듈 거부)·앵커 검사와 중복 확인 |
| 승인 설정 검사 | 예(S3) | 설정 위치는 고정(`%ProgramData%\…\install-config.json`) |
| 다른 Writer·동시 실행 차단 | 시작 시점 예(S5). 세션 중은 작업 정책 `IgnoreNew`와 3.3의 실행 경로 차단 | 같은 SID의 **다른 프로그램 부재**는 운영 규칙 + M-12E 실측 |
| 검사 성공 후 실행·실패 시 거부 | 예(S1~S7 순서, Fail-Closed) | 스크립트 무결성은 R-CONFIG ACL(G5 M-10) |
| 실행 중 보호 유지 | **시작 절차로는 불가.** OS 접근 검사가 2.1 ACL과 2.3 SID 전용을 세션 내내 적용하는 것에만 의존 | 3.4. M-12G·H·I로 비TCB 주체에게 변경 권한 자체가 없음을 실측 |
| 등록 전 검증 | **시작 시점 검사(S1~S6)만 가능.** 실행 중 상태를 등록 직전에 확인할 수 없음 | 3.4 |

### 3.3 우회 가능성과 계정 형태 조건 (로컬 S4U)

| 우회 방법 | 막는 조건 | 막지 못하면 |
|---|---|---|
| 암호로 로그온(`runas`, 대화형, RDP, 네트워크, 서비스 등록, `CreateProcessWithLogonW`) | 생성 시 무작위 암호를 설정하고 기록하지 않음. `SeDenyInteractiveLogonRight`·`SeDenyRemoteInteractiveLogonRight`·`SeDenyNetworkLogonRight`·`SeDenyServiceLogonRight` 부여, 배치 로그온(`SeBatchLogonRight`)만 허용 **[문서 근거]** | 운영 통제 **불충분** |
| S4U로 토큰 획득 | 비TCB 호출자는 S4U로 Identification 수준 토큰만 얻고 프로세스를 만들 수 없음(프로세스용 토큰은 `SeTcbPrivilege` 필요) **[문서 근거]** | — |
| 이 계정 주체의 다른 예약 작업 등록 | 다른 계정 주체 작업(S4U 포함) 등록은 관리자 권한 필요. 이 계정으로 실행 중인 프로세스는 승인 코드뿐(2.3) **[문서 근거]** | 운영 통제 불충분 |
| 기존 작업 수정·임의 실행 | 작업 보안 설명자 = TCB·OWNER만(S0) | 운영 통제 불충분 |
| 실행 중인 검증기 프로세스에 주입 | 같은 SID 또는 `SeDebugPrivilege`(TCB) 필요. 같은 SID는 2.3으로 배제 | — |

- **결론(설계 판단, 미실측):** 로컬 S4U 전용 계정과 위 조건이 모두 실측되면, 시작 절차를 우회하려면 TCB가 필요합니다. 따라서 **시작 경계** 자체는 운영으로 성립할 수 있다고 판단합니다. 남는 문제는 3.4입니다.
  - 암호를 아는 로컬 계정이면 `runas`로 다른 `TMP`를 준 검증기를 시작할 수 있으므로 **불충분**합니다.
  - 가상 계정(`NT SERVICE\<이름>`)은 서비스 호스트 코드가 필요해 코드 변경 없는 경로에 들지 않습니다.
  - gMSA는 이번 운영 선택에서 **제외**합니다(8절). S4U와 gMSA는 같은 보안 보장이 아닙니다.

### 3.4 실행 중 보호와 등록 시점의 한계 (개정 1에서 추가)

- **[사실]** 검증기는 Replay·대조가 끝나면 같은 프로세스 안에서 영수증을 Registry에 등록합니다(`Receipts.Issue`, E2-7 재확인 뒤). Registry는 INSERT만 허용합니다(B-4 6절 표 `REGISTRY_RECORD`).
- 시작 절차는 검증기 프로세스 밖에 있습니다. 따라서
  - 검사할 수 있는 시점은 **시작 전(S1~S6)** 과 **종료 후(S8)** 뿐입니다.
  - S8은 언제나 등록 **뒤**입니다. S8이 위반을 찾아도 이미 등록된 영수증을 막을 수 없습니다.
  - 승인 계약에는 등록된 영수증을 무효화하는 절차가 없습니다(아래 충돌 기록).
- 그러므로 운영 경로가 받아들여지려면 **세션 중 보호가 전부 예방 통제로 성립**해야 합니다. 예방 통제는 OS 접근 검사이며 조건은 다음 셋입니다.
  1. 세션 동안 비TCB·비VERIFIER 주체에게 2.1 체인의 쓰기·삭제·DAC·Owner 권한이 없음(ACL, M-12B·C·G)
  2. 세션 동안 VERIFIER SID로 실행되는 프로세스가 승인 체인과 시작 스크립트뿐(M-12E·F)
  3. TCB가 세션 중 이를 바꾸지 않음(신뢰 경계 정의)
- 1·2를 실측으로 보이지 못하면 OWNER 지시 7항대로 운영 해결을 수용하지 않고 C-3(5절)으로 전환을 제안합니다.

**충돌 기록 — 사후 탐지된 위반과 등록된 영수증:**

| 구분 | 내용 |
|---|---|
| Issue | S8이 세션 중 보호 위반을 탐지해도, 그 세션의 영수증은 이미 INSERT-only Registry에 등록되어 있습니다. 승인 계약은 등록 영수증의 무효화·철회 절차를 정하지 않습니다. |
| Reason | 등록은 검증기 내부(E2-7 뒤)에서 일어나고, 운영 시작 절차는 그 사이에 끼어들 수 없습니다. 코드 변경 없이는 등록 직전 확인을 넣을 수 없습니다. |
| Recommended Option | 운영 경로의 수용 조건을 "세션 중 모든 보호가 예방 통제로 성립함을 M-12로 실측"으로 둡니다. S8 위반은 사고로 OWNER에 보고하고 해당 기계의 공식 인증을 중단합니다. 예방 통제를 실측하지 못하면 C-3으로 전환합니다. |
| Alternative | (a) C-3: TEMP를 판정 경로에서 없애 이 문제 자체를 제거. (b) 계약에 영수증 철회 절차 추가 — 승인 계약 변경이라 이번 범위 밖이며 OWNER 결정 필요. |
| Impact | 운영 경로에서는 S8이 보조 증거일 뿐이며, 탐지된 위반의 영수증을 되돌릴 수 없습니다. 운영 경로 수용 여부 판단에 직접 영향을 줍니다. |

## 4. M-12 세부 실측 (M-12A~J)

- 공통: 운영 기계, 승인 B 뒤. OWNER 관리 세션은 승격, 역할 계정은 비승격 세션. 결과는 R-AUDIT와 저장소 밖 진단 디렉터리에 덮어쓰기 없이 보존합니다. 하나라도 실패하거나 `미검증`이면 **공식 인증을 시작하지 않습니다**.
- 개발 PC 결과는 운영 PASS로 승격하지 않습니다. 기존 M-12 단일 행(전환 계획 3.2절)은 이력으로 남기고, 판정은 M-12A~J 전부로 합니다.
- 개정 1: 로컬 S4U 조건과 3.4의 예방 통제 확인을 각 항목에 넣었습니다(굵은 글씨).

| ID | 실측 | 실행 계정 | 명령·방법 | 기대 결과 | 증거 | 실패 시 중단 |
|---|---|---|---|---|---|---|
| M-12A | 실제 TEMP 경로와 전체 부모 경로, **S4U에서의 전체 동작** | 검증기(시작 절차 경유) | TEST_ONLY 인증 1회를 시작 절차로 실행. ETW Kernel-File(G5 설계 8절 세션)로 `starplayer-verifier\` 생성 경로 수집. 시작 절차 로그의 자식 환경 `TMP`·`TEMP`. 경로의 모든 상위 구성 요소 나열. **S4U 제약(네트워크 자격 증명 없음, 암호화 파일·사용자 DPAPI 비밀 접근 불가, 프로필 로드 여부)에서 검증기·`dotnet test`·Git·Registry Pipe가 끝까지 동작하는지와 네트워크 시도 유무를 ETW Network·출력으로 기록** | 생성 경로가 `<R-WORK>\verifier-tmp\<session-id>\starplayer-verifier\<guid>` 하나뿐. `TMP`=`TEMP`=세션 절대경로. **TEST_ONLY 인증 완료, 실패가 네트워크·DPAPI·EFS 때문이 아님** | ETW 원본·`tracerpt` 출력, 시작 절차 로그, 경로 목록 | 다른 위치에 생성, 경로 미확정, **S4U에서 검증기가 끝까지 동작하지 않음** |
| M-12B | NTFS 객체·Reparse·상속 ACL | OWNER 관리 세션 | 드라이브 Root부터 세션 디렉터리까지 각 구성 요소: `fsutil fsinfo volumeinfo`(NTFS), `fsutil reparsepoint query`, `icacls`, `Get-Acl … .Sddl`, **`cipher /c`(EFS 아님)**. `g5-acl.ps1 -Mode Verify`(R-WORK 포함 레이아웃). 세션 디렉터리와 그 안의 시험 파일 SDDL을 운영체제가 만든 상속값과 비교(M-11 방법) | NTFS, Reparse 0, **EFS 0**, `verifier-tmp` 보호 DACL(`P`), 세션·파일은 상속 ACE만, 비TCB·비VERIFIER 쓰기 계열 ACE 0 | 구성 요소별 SDDL·`icacls /save`, Verify 보고서 | 하나라도 다름 |
| M-12C | DeleteChild·Rename·Replace 권한 | 생산자·열람자·**배포** 계정 각각(비승격) | `g5-probe.ps1 -Role <role>`에 `verifier-tmp`·세션 디렉터리 대상 추가 실행. 추가 수동 시도: 세션 디렉터리와 상위의 삭제·이름 변경, 같은 이름 파일 생성, 기존 파일 `MoveFileEx(REPLACE_EXISTING)` 교체, 하드링크 생성 | 모든 시도 `ACCESS_DENIED`, `UNEXPECTED_ALLOWED` 0 | 역할별 Probe 보고서, 시도별 명령·출력·계정·시각 | 허용된 시도 1건 이상 |
| M-12D | 기존 열린 쓰기 핸들 | OWNER 관리 세션 | 보호 적용·세션 생성 전 `openfiles /local on`(재부팅) 뒤 `openfiles /query /v`, R-WORK 아래 핸들 목록. 시작 절차 S6이 새 디렉터리를 만드는지(이미 있으면 거부) 확인 | R-WORK 아래 비TCB·비VERIFIER 핸들 0. 세션 디렉터리는 항상 새 객체 | 조사 출력, 시작 절차 로그 | 기존 핸들 존재 또는 세션 디렉터리 재사용 |
| M-12E | 계정과 프로세스 격리(**로컬 S4U**) | OWNER 관리 세션 | `whoami /all`(시작 절차 로그), `secedit /export /areas USER_RIGHTS`의 배치 허용·거부 4종, `Get-LocalUser`·`Get-LocalGroupMember`(Administrators·Remote Desktop Users 등 비구성원), 이 계정 주체 예약 작업·서비스 검색(`schtasks /query /xml`, `Get-CimInstance Win32_Service`), **세션 중 1초 간격** 같은 SID 프로세스 목록 | 계정은 로컬 표준 사용자, 거부 권한 4종, 배치 로그온만, 그 계정 주체 작업은 시작 작업 하나, 서비스 0, **세션 내내** 같은 SID 프로세스 = 승인 체인·시작 스크립트뿐 | 출력 원본, 시계열 | 다른 작업·서비스·프로세스가 같은 SID 사용 |
| M-12F | 인증 시작 차단 절차 | OWNER 관리 세션(요청 배치), 검증기(시작 절차), 비TCB 역할 계정(우회 시도) | 실패 주입 각 1회: S1 형식 위반, S2 다른 계정, S3 다른 DLL·설정 SHA, S4 `verifier-tmp`에 시험 ACE 추가, S5 잠금 선점·같은 SID 프로세스, S6 세션 디렉터리 선생성. 우회 시도: 비TCB 계정의 `runas /user:<검증기>`, `schtasks /run`·`/change`·`/delete`, 작업 XML 읽기, `Register-ScheduledTask`로 검증기 주체 작업 등록, 세션 중 두 번째 `schtasks /run` | 각 주입에서 검증기 미실행·Registry 연결 0. 우회 시도 전부 거부. 두 번째 실행은 `IgnoreNew`로 무시 | 시작 절차 로그, Registry 로그, 시도 출력 | 어느 주입에서든 검증기가 실행되거나 우회 성공 |
| M-12G | 세션 중 보호 유지(**예방**) | OWNER 관리 세션(관찰), 각 역할 계정 | TEST_ONLY 인증 중 1초 간격으로 세션 디렉터리·상위 SDDL·소유자 기록, 같은 시간에 역할 계정의 `WRITE_DAC`·`WRITE_OWNER`·삭제·생성 시도, S8 사후 대조 | 시도 전부 거부(예방 확인), 기록된 SDDL·소유자 변화 0, S8 일치 | 시계열 기록, 시도 로그, S8 출력 | 허용된 시도 또는 변화. **S8 일치만으로는 통과 아님** |
| M-12H | 다른 계정의 파일 변조 반례 | 생산자·열람자·배포 계정 각각 | TEST_ONLY 인증 중 세션 디렉터리 아래 모든 파일에 대해 반복 시도: 쓰기 열기, 삭제, 이름 변경, 같은 이름 새 파일 생성, 디렉터리 Junction 생성 | 모든 시도 거부, 인증 결과가 무간섭 실행과 같음 | 시도 로그(성공·실패 개수), 두 실행의 결과 비교 | 시도 1건이라도 성공 |
| M-12I | TEMP 색인과 Manifest의 동시 변조 반례 | 생산자 계정(공격), 검증기 | TEST_ONLY 인증 중 색인과 해당 Unit Manifest를 함께 바꾸는 교체 스크립트를 경쟁 실행(Manifest를 다른 정상 실행의 것으로, 색인의 `manifest_sha256`을 그에 맞게). **대조 실험(진단 전용, 비공식 기계):** 같은 스크립트를 보호하지 않은 TEMP에서 돌려 교체가 실제로 일어남을 먼저 확인 | 운영 기계: 교체 시도 전부 거부, 결과 불변. 대조 실험: 교체 성공(시험 장치가 유효함을 확인) | 스크립트·SHA, 두 환경의 시도 로그, 결과 | 운영 기계에서 교체 1건이라도 성공. 대조 실험에서 교체가 안 되면 장치 무효로 M-12I 미검증 |
| M-12J | 재시작·설정 변경 후 재검증 | OWNER 관리 세션 | 운영 기계 재부팅, 작업 정의·보안 설명자·시작 스크립트·설정·`install-config`·R-WORK ACL·**로그온 권한** 변경(되돌림 포함) 각각 뒤 M-12A·B·E·F를 다시 실행. 작업 정의 XML·스크립트·설정 SHA를 승인값과 대조 | 재부팅 뒤 같은 결과. 승인값과 다른 설정에서는 S3·S4가 거부 | 회차별 출력 | 재시작·변경 뒤 결과가 다름 |

## 5. 코드 대안 C-3 (보완 설계, 구현하지 않음)

- **이 설계는 제품에 구현되지 않았습니다.** G5-TEMP-01을 해결했다고 주장하지 않습니다. 운영 경로(A)의 강제 경계가 성립하지 않을 때의 대체 설계로만 보존합니다.
- 이전 C-3 설명("색인 SHA 결속만으로 모든 Manifest가 자동 결속됨")은 Codex G5-MODEL-01에 따라 **폐기**했습니다(9절에 원문).

### 5.1 결속 사슬 (기존 코드에서 이미 성립하는 부분 [사실])

```text
제출 문서 ArtifactRef(raw_sha256)  ──Archive.Snapshot(핸들 고정 1회 읽기, 길이·SHA 대조)──►  문서 바이트
문서 additional_evidence.artifacts[*] ──Archive.Snapshot──►  각 파일 바이트(길이·SHA = 해당 ArtifactRef)
   ├─ DIAGNOSTICS  run/<run_id>/<ResultFile>             = Snapshot 색인 바이트
   └─ UNIT_MANIFEST run/<run_id>/<unit dir>/manifest.json = Snapshot Manifest 바이트(Unit마다 1개, RunFile이 정확히 1개 확인)
```

- 끊기는 곳은 Snapshot 바이트를 TEMP에 쓰고 Replay가 **경로로 다시 읽는** 1절 2~3단계뿐입니다.

### 5.2 보완 설계 (OWNER 조건 1~9 대응)

| # | OWNER 조건 | 설계 |
|---|---|---|
| 1 | Snapshot 실행 색인 고정 | Replay 입력 = `RunFile(snapshot, "DIAGNOSTICS", …)`의 `Bytes` 버퍼 그 자체. 파일로 쓰지 않음 |
| 2 | 모든 Unit Manifest 바이트 고정 | Replay 입력 = 계획 순서의 `RunFile(snapshot, "UNIT_MANIFEST", unit, …)` 버퍼 목록. 개수 = 계획 Unit 수 = Snapshot의 이 실행 UNIT_MANIFEST 수(누락·추가 거부) |
| 3 | 색인 `manifest_sha256` ↔ Snapshot Manifest SHA 직접 비교 | 각 Unit에 대해 `index.units[i].manifest_sha256` = SHA-256(Snapshot Manifest 버퍼), `index.units[i].manifest_path` = 그 ArtifactRef `relative_path`에서 `run/<run_id>/`를 뗀 값 |
| 4 | Manifest ArtifactRef SHA ↔ Snapshot 원본 SHA | Replay 진입에서 SHA-256(버퍼) = ArtifactRef `raw_sha256`, 길이 = `byte_length`를 다시 확인(Snapshot 검사의 명시적 재확인) |
| 5 | Snapshot 색인 자체를 승인 Archive 원본에 결속 | 색인 버퍼에도 4와 같은 재확인. 색인 ArtifactRef는 제출 문서의 `artifacts`에 있는 것만(역할 DIAGNOSTICS, 경로 `run/<승인 run_id>/<ResultFile>`). 문서 자체는 제출 ArtifactRef SHA와 최종 `Compare(doc, expected)`로 결속. 색인의 `run_id`·`family`·`git_commit_sha`·`approvals`는 기존 Replay 검사로 승인값과 대조 |
| 6 | 검증과 파싱에 같은 읽기 버퍼 | SHA 계산·JSON 중복 항목 검사·파싱을 모두 같은 `byte[]`로. 검사 뒤 버퍼를 복사·재생성하지 않음 |
| 7 | 검증 후 변경 가능한 경로를 다시 열지 않음 | Replay에 버퍼를 받는 진입점 추가. 검증기는 그 진입점만 호출하고 `Path.GetTempPath()`·`Archive.WriteNew`(재생 디렉터리)를 쓰지 않음. 기존 경로 진입점은 SimHarness CLI용으로 남기되, 각 파일을 **한 번** 읽어 같은 버퍼 진입점을 호출하는 얇은 래퍼로 바꿈 |
| 8 | SHA 검사 직후 교체 가능한 TOCTOU 경로 없음 | 판정 입력이 프로세스 메모리 버퍼뿐이므로 파일 교체 경로가 없음. Snapshot 읽기 자체는 기존 핸들 고정 1회 읽기 |
| 9 | 불일치 시 인증 거부 | 3~5 불일치는 기존 오류 체계(`archive_invalid`·`replay_mismatch`)로 거부, 영수증 발행·등록 없음 |

- **범위 밖(이 설계가 바꾸지 않는 것):** Replay가 저장소 Root(R-BUILD)에서 읽는 Seed Manifest 등 승인 입력. 이는 G5 R-BUILD 봉인 ACL과 기존 Seed SHA 대조(`Archive.cs` 114행, attestation Pin)가 다룹니다.
- **불변 조건:** Golden·Hash·RNG·Replay 결과·DRAFT 색인 바이트는 같아야 합니다. 거부 사유만 추가됩니다.
- 수정 파일·시험·재검증 범위는 [S4U 실행기 구성안](B1_G5_S4U_LAUNCHER_PLAN.md) 4절 비교표에 있습니다. C-1(재생 Root를 R-WORK로)·C-2(핸들 유지)는 TEMP 경로를 남기므로 C-3의 대안이 아니라 부분 완화로만 남깁니다. 이전 C-4(메모리 Replay)는 보완 C-3에 포함되었습니다.

## 6. 코드 변경 없이 `c77ed1a`를 유지할 수 있는 조건 (개정 1)

다음이 **모두** 참이어야 합니다. 하나라도 거짓이면 보완 C-3(새 소스 Commit)이 필요합니다.

1. Codex가 로컬 S4U 운영 경로(2·3절)를 받아들입니다(`OPERATIONAL_PATH_ACCEPTABLE`). 이 판정은 운영 PASS가 아닙니다.
2. OWNER가 B-5를 **로컬 S4U 전용 표준 사용자**로 정하고, 3.3 조건(무작위 미기록 암호, 거부 권한 4종, 배치 로그온만, 작업 보안 설명자)을 승인 B에 넣습니다. gMSA·암호를 아는 계정·가상 계정은 해당하지 않습니다.
3. 시작 절차(예약 작업 + 시작 스크립트·설정, 3.2절)를 별도 TASK로 구현하고 자체 시험·Codex 검수를 거쳐 승인 B에서 설치합니다. 운영 도구이며 제품 코드가 아닙니다.
4. 운영 기계에서 M-12A~J가 모두 통과하고, 특히 3.4의 예방 조건 1·2가 세션 내내 성립함을 보입니다(M-12E·G·H·I).
5. 3.4 충돌 기록의 권고안을 OWNER가 받아들입니다(S8 위반 = 사고 보고·중단, 등록 영수증 처리 방법 포함).
6. 공식 인증은 시작 절차로만 실행합니다. 절차 밖 실행 결과는 공식 증거로 쓰지 않습니다.
7. 조건이 바뀌면(재부팅, 설정·계정·권한·ACL 변경) M-12J로 다시 확인하기 전까지 공식 인증을 멈춥니다.

## 7. 공식 G2/G3와의 관계

- G5-TEMP-01은 **공식 인증(G4 공식 이후) 진입**을 막습니다. 검증기 Replay를 쓰지 않는 공식 G2/G3 빌드 재현성 절차와는 별개입니다.
- G1/G2 정책은 OWNER가 채택했습니다(2026-10-30, [결정안](B1_G1_G2_CONDITIONAL_DECISION_PROPOSAL.md) C절). 공식 소스 Commit 확정과 운영 빌드 환경이 준비되면 G2/G3는 G5-TEMP-01과 무관하게 진행할 수 있습니다.
- 보완 C-3으로 전환하면 공식 빌드 소스 Commit이 바뀌므로 G2/G3는 **새 Commit으로** 다시 합니다. G5-TEMP-01 해결 방식을 G2/G3 공식 실행 전에 정하는 것이 재작업을 줄입니다.
- G5-TEMP-01이 해결되기 전에는 공식 인증·G4 신뢰 경계 검증을 통과 처리하지 않습니다.

## 8. gMSA 대안 — 이번 운영 선택에서 제외 (G5-MODEL-02)

- **OWNER 결정(2026-10-30):** 이번 운영 경로에서 gMSA를 **제외**합니다. 로컬 S4U와 gMSA를 같은 보안 보장으로 표현하지 않습니다. gMSA 관련 AD 설정·계정 생성은 하지 않았습니다.
- gMSA를 쓰면 검증기 SID의 자격 증명이 운영 기계 밖의 신뢰에 의존합니다 **[문서 근거]**:

| 항목 | 신뢰 범위 |
|---|---|
| AD·KDS 신뢰 범위 | gMSA 암호는 도메인의 KDS Root Key와 계정 속성에서 도메인 컨트롤러가 계산합니다. 모든 도메인 컨트롤러, KDS Root Key 객체를 읽을 수 있는 주체, 그 백업(DC 시스템 상태 백업 포함)이 신뢰 범위에 들어갑니다. |
| 관리 암호 회수 허용 주체 | `PrincipalsAllowedToRetrieveManagedPassword`(`msDS-GroupMSAMembership`)에 든 모든 컴퓨터·그룹과 그 그룹의 중첩 구성원 |
| 관리 암호를 회수할 수 있는 모든 호스트 | 위 목록의 모든 컴퓨터 계정. 그 호스트의 SYSTEM·로컬 관리자는 암호를 얻어 같은 SID로 로그온할 수 있습니다. |
| 관리 권한·위임 권한 | Domain Admins·Enterprise Admins, gMSA 객체나 그 OU에 쓰기·`msDS-GroupMSAMembership` 변경 권한을 위임받은 주체(자신을 회수 허용 목록에 넣을 수 있음), 계정 연산자 등 도메인 위임 구조 |
| 다른 호스트 통제의 영향 | 회수 허용 호스트 하나라도 장악되면 운영 기계와 **같은 SID**의 자격 증명이 생깁니다. Registry는 토큰 SID로 검증기 역할을 정하므로(3.1), 그 자격 증명으로 운영 기계에 로그온하거나 연결할 수 있으면 시작 절차 밖 인증 경로가 됩니다. 로그온 거부 권한은 운영 기계에서만 적용됩니다. |
| 운영 기계 외부 신뢰 경계 | AD 도메인 전체(DC, KDS, 복제, 위임, 회수 허용 호스트와 그 관리자). 기존 G5 TCB(운영 기계의 SYSTEM·Administrators·TrustedInstaller)보다 넓습니다. |

- **사용 조건:** 위 신뢰 범위 전체를 OWNER가 명시적으로 승인하지 않으면 gMSA는 사용할 수 없습니다. 승인하더라도 회수 허용 목록 = 운영 기계 하나, 위임 목록·DC 접근 통제의 실측이 추가로 필요합니다. 이번 작업에서는 다루지 않습니다.

## 9. 개정 이력

| 개정 | 위치 | 이전 | 이후 |
|---|---|---|---|
| 1 | 3.2 S0 | "주체 = 검증기 계정, 로그온 형식 S4U(암호 저장 없음) 또는 gMSA" | 로컬 표준 사용자·S4U만 |
| 1 | 3.3 결론 | "B-5 계정 형태가 S4U 전용 로컬 표준 사용자 또는 gMSA이고 … 충분하다고 판단" | 로컬 S4U만. gMSA는 8절로 분리·제외 |
| 1 | 3.2 S8·표 | "다르면 해당 결과를 공식으로 쓰지 않음" | 탐지 전용. 등록 뒤라 막지 못함(3.4 신설) |
| 1 | 5절 C-3 | "Replay가 색인 바이트를 한 번 읽고 그 SHA를 Snapshot 색인 SHA와 대조(기대 SHA 인자 추가). Manifest는 이미 그 색인의 `manifest_sha256`과 대조하므로 사슬이 Snapshot에 고정됨" | **폐기**(Codex G5-MODEL-01). 이유: 경로 재읽기를 남김(조건 7·8), Snapshot Manifest 바이트·ArtifactRef와 직접 대조하지 않음(조건 2~4), 색인의 Archive 원본 결속과 같은 버퍼 원칙을 명시하지 않음(조건 5·6). 5.2절 설계로 대체 |
| 1 | 5절 C-1·C-2·C-4 | 별도 대안 | C-1·C-2는 부분 완화로만, C-4는 보완 C-3에 포함 |
| 1 | 6절 2 | "S4U 전용 로컬 표준 사용자 또는 gMSA" | 로컬 S4U만, 조건 5(3.4 충돌) 추가 |
| 1 | M-12A·B·C·E·F·G·J | — | S4U 동작, EFS, 배포 계정, 세션 중 프로세스 시계열, 우회 시도, 예방 확인, 로그온 권한 변경 추가 |
