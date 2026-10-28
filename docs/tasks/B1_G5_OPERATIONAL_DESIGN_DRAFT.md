# B1 G5 운영 보호 설계 확정안 (DRAFT, 운영 시스템 변경 없음)

- 작성: Claude Code, 2026-10-28. 근거: OWNER 지시 "G4 구현 수용 및 Phase 3 잔여 작업 자율 진행"(2026-10-28) 1·2항
- **성격:** G5 운영 준비 설계와 도구입니다. 운영 시스템의 보호 ACL 변경, 신뢰 앵커 설치, 공식 Pin 등록, 계정 생성은 **하지 않았습니다.** 모두 OWNER 승인 지점입니다.
- 이 문서는 [G5·A-1 준비 초안](B1_G5_A1_OPERATIONAL_PREP_DRAFT.md) 1~5절을 구체화합니다. 두 문서가 다르면 이 문서가 최신 제안입니다. A-1은 [A-1 게시 준비](B1_A1_PUBLICATION_PREP.md), Gate는 [운영 Gate 준비표](B1_OPERATIONAL_GATE_READINESS.md)에 있습니다.
- 도구: `tools/b1-ops/` (스크립트 6개). 시험과 증거는 [G5·A-1 준비 보고서](../reports/B1_G5_A1_PREP_REPORT.md)에 있습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 0. OWNER 운영 정책 방향 (2026-10-28, 규범 후보)

OWNER가 운영 규범 **후보**로 채택한 방향입니다. 확정 규범이나 승인값이 아닙니다.

| # | 방향 | 이 설계에서 반영한 곳 |
|---|---|---|
| D-1 | 공식 인증에서 TRX `RunInfo` Warning은 인프라 실패로 거부 | 현재 G4 코드 동작 그대로(`TestProfile.run_error`) |
| D-2 | Windows·공유 런타임 파일은 승인 운영 환경에서 정확한 경로와 SHA로 Pin | 7절 Pin 수집·검증 |
| D-3 | Windows 경로·설치 Root라는 이유만으로 비Pin 코드를 허용하지 않음 | G4 코드 동작(V-2). 7절 |
| D-4 | 동적 어셈블리 허용 목록은 기본 빈 목록 | 설치 설정 `allowed_dynamic_assemblies = []` 권고 |
| D-5 | R-WORK는 일반 TEMP가 아닌 별도 보호 Root | 1·3절 |
| D-6 | 외부 Pin과 보호 설치 설정은 제출자가 변경 불가 | 1·3절 ACL, 6절 반례 |

- 실제 파일 SHA, SID, ACL 값은 아직 확정하지 않습니다.

## 1. 보호 Root 디렉터리 구조 확정안

**권고 구조** (경로는 후보이며 OWNER가 바꿀 수 있습니다):

```text
C:\                                   드라이브 Root (TrustedInstaller 소유, 기본 ACL 유지)
├─ ProgramData\StarPlayer\            R-CONFIG  보호 DACL, TCB 전용
│   └─ EvidenceVerifier\install-config.json      고정 위치(G4-T1)
├─ StarPlayer\                        R-BASE    보호 DACL, TCB 전용
│   ├─ trust\anchor.json              R-TRUST   TCB 쓰기, 검증기 읽기
│   ├─ build\<source_git_sha>\        R-BUILD   깨끗한 체크아웃 + 빌드 출력 (Build Provenance relative_path의 Root)
│   ├─ work\                          R-WORK    검증기 서비스만 쓰기 (세션마다 새 하위 디렉터리)
│   ├─ archive\                       R-ARCHIVE 계약(§17·NSC-4)의 계정별 권한
│   └─ audit\                         R-AUDIT   ACL 스냅샷·icacls 기록·반례 결과 (TCB 쓰기, Archive 밖)
├─ Program Files\dotnet\              R-DOTNET  설치 ACL 유지·확인만
└─ Program Files\Git\                 R-GIT     설치 ACL 유지·확인만(4절 관찰 참고)
```

**배치 근거:**

- **드라이브:** NSC-4 상위 경로 규칙은 드라이브 Root까지 검사합니다.
  - 개발 PC의 `C:\`는 TCB 밖 주체에게 DELETE·WRITE_DAC 등을 주지 않습니다. `Authenticated Users`의 `SDGXGWGR`는 상속 전용입니다.
  - 개발 PC의 `E:\`는 `Authenticated Users`에게 Modify(DELETE 포함)를 직접 줍니다. 상위 경로 규칙에서 거부되므로 보호 Root를 두지 않습니다.
- **상속 차단 필수:**
  - `C:\` 아래에 새로 만든 폴더는 `Authenticated Users:(OI)(CI)(IO)` Delete+Write를 상속합니다.
  - `C:\ProgramData` 아래 폴더는 `Users`의 쓰기 권한과 `CREATOR OWNER` 전체 권한을 상속합니다.
  - 따라서 R-CONFIG·R-BASE와 그 아래 Root는 모두 **보호 DACL**(상속 끊음)이어야 합니다. 이는 `g5-acl.ps1 Apply`가 강제하고 `Verify`가 확인합니다.
- **R-BASE 자체 보호:** R-BASE가 없거나 상속 DACL이면 `C:\StarPlayer`를 일반 사용자가 이름 변경·삭제해 하위 Root 전체를 바꿔치기할 수 있습니다(6절 반례 "상위 경로 변경").
- **설치 설정·앵커 위치:** 설치 설정과 앵커는 `build_root`·`archive_root`·`work_root` 밖이어야 합니다(현재 코드 검사). 위 구조에서 R-TRUST는 R-BUILD·R-WORK·R-ARCHIVE의 형제이므로 조건을 만족합니다.
- **R-WORK와 TEMP:** `%TEMP%`·`%TMP%`는 사용자별이며 다른 프로세스가 쓸 수 있습니다. 개발 PC 관찰에서도 사용자 TEMP에 다른 로컬 계정(`…-1002`)의 Modify 상속 ACE가 있었습니다. 따라서 R-WORK는 R-BASE 아래 별도 보호 Root로 둡니다.
- **R-BUILD 하위 이름:** 승인 Commit SHA를 폴더 이름으로 쓰면 빌드 간 혼동을 막습니다. Build Provenance `relative_path`는 이 폴더를 Root로 합니다.

## 2. 실행 계정과 TCB 계정의 역할 분리

| 역할 | 성격 | 권한 요약 | 금지 |
|---|---|---|---|
| **TCB**(SYSTEM, BUILTIN\Administrators, NT SERVICE\TrustedInstaller) | 운영체제·설치 관리 | 모든 보호 Root 소유·변경 | 일상 실행에 쓰지 않음 |
| **OWNER 관리 세션** | TCB 구성원으로 승격한 세션 | ACL 적용, 앵커·설치 설정 설치, Pin 등록 | 검증기·생산자 실행 금지 |
| **OWNER 서명 주체**(`owner_principal_sids`) | 승인 기록 등록 주체 | Registry 등록(R03) | 다른 역할과 같은 SID 금지(R03-1) |
| **배포 계정**(DEPLOYER) | 표준 사용자 | 스테이징 폴더에서 clone·빌드. 봉인 후 R-BUILD 쓰기 없음 | 관리자 그룹 금지 |
| **검증기 서비스**(VERIFIER_SERVICE, `verifier_principal_sid`) | 표준 사용자 또는 서비스 계정 | R-CONFIG·R-TRUST·R-BUILD·R-DOTNET·R-GIT 읽기·실행, R-WORK 수정, R-ARCHIVE 읽기 | 관리자 권한(Elevated) 실행 금지(NSC-4 6), 관리자 그룹 금지 |
| **Registry 계정·서비스**(`registry_account_sid`, `registry_service_sid`) | §17 R03-2 | 계약대로 | 다른 역할과 같은 SID 금지 |
| **생산자**(PRODUCER) | 비신뢰 표준 사용자 | 계약상 R-ARCHIVE 쓰기만 | R-CONFIG·R-TRUST·R-BUILD·R-WORK·R-DOTNET·R-GIT 쓰기 금지 |
| **열람자**(READER, `evidence_reader_sids`) | 표준 사용자 | 증거 읽기 | 검증기 시작 금지(R03-4 1단계) |

- **분리 조건(도구 검사):** 역할 SID는 모두 서로 다르고, TCB SID가 아니며, `BUILTIN\Administrators`의 직접 구성원이 아니어야 합니다.
  - 중첩 그룹은 도구가 해석하지 않습니다. OWNER가 `whoami /groups`로 각 계정을 확인해 증거로 남깁니다.
- **[관찰: 개발 PC]** 개발 사용자 계정은 `BUILTIN\Administrators`의 직접 구성원입니다(UAC 필터 토큰). 따라서 이 계정은 운영의 검증기·생산자 역할에 쓸 수 없습니다(`g5-acl.ps1 Verify` principals FAIL로 확인).
- **서비스 계정 형태:** 로컬 표준 사용자, 가상 계정(`NT SERVICE\<이름>`), 관리 서비스 계정 중 하나를 OWNER가 정합니다. NSC-5 Pipe 소유자 규칙(AT-4/P-1)과 함께 G6에서 실측합니다.

## 3. R-TRUST·R-BUILD·R-DOTNET·R-GIT·R-WORK 분리와 권한 행렬

`RX` 읽기·실행, `M` 수정(삭제 포함, ACL·소유자 변경 제외), `—` 권한 없음. TCB는 모든 Root에서 전체 권한입니다.

| Root | 설치 설정 필드 | DEPLOYER | VERIFIER | PRODUCER | READER | 적용 방식 |
|---|---|---|---|---|---|---|
| R-CONFIG | (고정 위치) | — | RX | — | — | Apply |
| R-BASE | — | — | RX | RX | RX | Apply |
| R-TRUST | `anchor_path` | — | RX | — | — | Apply |
| R-BUILD | `build_root` | 봉인 전 스테이징만 | RX | RX | — | Apply(봉인 후) |
| R-WORK | `work_root` | — | **M** | — | — | Apply |
| R-ARCHIVE | `archive_root` | — | 계약대로 | 계약대로 | 계약대로 | 계약 값 확정 후 |
| R-AUDIT | — | — | — | — | RX | Apply |
| R-DOTNET | `dotnet`, `execution_pins` | — | RX(Users 상속) | RX(Users 상속) | — | Verify만 |
| R-GIT | `git` | — | RX(Users 상속) | — | — | Verify만 |

- **R-BUILD 봉인 절차(후보):**
  1. DEPLOYER가 별도 스테이징 폴더에서 승인 Commit을 clone·빌드합니다.
  2. OWNER 관리 세션이 스테이징 폴더를 `R-BUILD\<sha>`로 옮깁니다.
  3. `g5-acl.ps1 Apply`가 소유자를 TCB로, 모든 하위 항목을 상속 전용으로 바꿉니다.
  4. 봉인 후 R-BUILD에는 TCB 밖 쓰기 권한이 없습니다.
- **R-ARCHIVE:** 계약 §17·NSC-4의 계정별 권한을 그대로 따릅니다. 이 도구는 레이아웃의 `writers`·`readers` 값만 강제하며 정책을 새로 만들지 않습니다. 계약 값이 확정될 때까지 `apply: false`(확인만)로 둡니다.
  - **중첩 제약(G5-R02):** 보호되지 않은 기존 자식은 부모의 상속 ACE를 따릅니다. Windows 전파를 쓰면 바로 바뀌고, 쓰지 않으면 부모의 옛 상속 ACE가 남아 실제 부모와 어긋납니다(Apply는 G5-FIX-01 뒤 자동 전파를 쓰지 않습니다). 따라서 `apply: false` Root가 Apply Root 아래에 있으면, 그 Root가 **이미 보호 DACL로 존재할 때만** Apply가 진행됩니다. 위 구조에서는 R-BASE 아래의 R-ARCHIVE가 해당합니다.
  - 상속을 허용하거나 없으면 Apply 전에 거부합니다(REFUSED, 아무것도 바뀌지 않음).
  - 보호된 경우에도 적용 전후의 하위 트리 SDDL을 비교합니다(탐지용).
  - R-ARCHIVE의 보호 DACL 값은 도구가 정하지 않습니다. 계약 값으로 OWNER가 먼저 설정하거나, R-ARCHIVE를 R-BASE 밖에 둡니다.
- **R-WORK 세션 디렉터리:** 검증기가 만든 하위 디렉터리의 소유자는 검증기 서비스입니다. 도구는 R-WORK에서 소유자로 TCB 외에 writers를 허용합니다. G4 코드의 `PinnedDirectory`(새 디렉터리·Reparse 거부·TRX 한 번 읽기)가 같은 경로를 보호합니다.

## 4. Windows ACL 설정·검증 스크립트

### 4.1 도구 (`tools/b1-ops/g5-acl.ps1`)

| 모드 | 변경 | 내용 |
|---|---|---|
| `Plan` | 없음 | 각 Root에 적용할 소유자·DACL(SDDL)과 하위 처리 출력 |
| `Verify` | 없음 | 아래 규칙 판정. 종료 코드 0 PASS, 1 위반 |
| `Apply` | ACL 변경 | **사전 점검**(아래, 실패 시 아무것도 쓰지 않음) → 스냅샷 → 핸들 결속 적용 → 전체 Verify·중첩 Root 비교 → 실패 시 항목별 복구. 운영에서는 승격 세션 + `-OwnerApproved`만 허용 |
| `Restore` | ACL 변경 | 스냅샷 파일로 소유자·DACL을 항목별로 복구하고, 생성 일지(`<snapshot>.created`)에 있는 디렉터리만 비어 있으면 제거 |

**Verify 규칙** (TCB = `AnchorAcl.Tcb`와 같은 3개 SID):

1. 드라이브 Root부터 Root까지 모든 경로 구성요소가 Reparse Point가 아님
2. **상위 경로:** 소유자가 TCB이고, 그 구성요소에 적용되는 Allow ACE가 TCB 밖 주체에게 DELETE·FILE_DELETE_CHILD·WRITE_DAC·WRITE_OWNER를 주지 않음(NSC-4 상위 규칙과 같음)
3. **Root와 모든 하위 항목:** Reparse Point는 따라가지 않고 위반으로 보고합니다.
   - 소유자는 TCB 또는 그 Root의 writers입니다.
   - DACL이 있고 ACE 형식은 Allow·Deny만 허용합니다.
   - 상속·상속 전용 ACE를 포함해, TCB+writers 밖 주체에게 쓰기 계열 권한을 주는 Allow ACE가 없습니다. 쓰기 계열 권한은 데이터 쓰기, 추가, EA, 속성, 삭제, 하위 삭제, WRITE_DAC, WRITE_OWNER입니다.
   - `CREATOR OWNER`·`OWNER RIGHTS`는 소유자로 해석합니다. `CREATOR GROUP` 쓰기 ACE는 거부합니다.
4. Apply 대상 Root는 보호 DACL이어야 합니다. readers에게는 RX, writers에게는 파일·하위 디렉터리 추가 권한이 있어야 합니다.
5. 역할 SID는 2절 분리 조건을 만족해야 합니다.
6. 중첩된 Root(예: R-BASE 아래 R-WORK)는 자기 항목에서만 판정합니다.
7. 관리자 그룹 조회가 실패하면 principals는 FAIL입니다(G5-R04). 조회 실패를 PASS로 바꾸지 않습니다.
   - 중첩 그룹은 해석하지 않습니다. 이 한계는 실패가 아니라 별도 안내(note)로 표시합니다.

**Apply 사전 점검(G5-R01·R02)** — 스냅샷·디렉터리 생성·ACL 변경보다 먼저 수행합니다. 하나라도 실패하면 `REFUSED`(종료 코드 1)이며 스냅샷도 쓰지 않습니다.

1. **경로 결속:**
   - 드라이브 Root부터 각 Apply Root까지 존재하는 모든 구성요소를 핸들로 엽니다. 이 핸들은 데이터 접근을 포함하고 삭제 공유는 없으며, Reparse를 따라가지 않습니다.
   - 핸들은 Apply가 끝날 때까지 유지합니다. 그동안 그 구성요소는 이름 변경·삭제·교체가 불가합니다.
   - 핸들로 다음 두 가지를 확인합니다.
     - Reparse Point가 아닌지
     - 최종 경로(`GetFinalPathNameByHandle`)가 기대 경로와 같은지. Junction·마운트 지점·짧은 이름은 거부합니다. 따라서 진단 Root 안의 Junction을 통한 외부 접근도 문자열 접두사가 아닌 실제 경로로 거부됩니다.
   - **[시험]** 메타데이터 권한만 가진 핸들은 공유 검사에 참여하지 않아 이름 변경을 막지 못했습니다. 그래서 데이터 접근을 함께 요청합니다. 이 사실은 이번 자체 시험의 교체 반례에서 실측했습니다.
2. **신뢰된 상위:** Apply Root가 아닌 기존 상위 구성요소는 이미 상위 규칙(TCB만 이름 변경·삭제·재권한 가능)을 만족해야 합니다.
3. **하위 상태:** 기존 Apply Root 아래에 Reparse Point나 읽을 수 없는 항목이 없어야 합니다. 이전에는 건너뛰었지만, 지금은 거부합니다.
4. **중첩된 비적용 Root(G5-R02):** 이미 존재하고 보호 DACL이어야 합니다. 그 하위 트리 SDDL을 기준값으로 기록합니다.
5. **유지보수 조건:** 레이아웃 주체가 소유한 실행 중 프로세스가 없어야 합니다. 운영 모드에서 강제합니다.
   - 소유자를 확인할 수 없는 프로세스 수는 보고만 합니다.
   - OWNER는 역할 계정의 로그아웃·비활성화를 함께 수행합니다.
6. **하드링크·객체 식별(G5-FIX-01):** 보안 설명자는 경로가 아니라 파일 객체에 붙습니다. 그래서 다른 경로에 하드링크가 있는 파일의 설명자를 바꾸면 그 경로(예: R-ARCHIVE의 원본)도 함께 바뀝니다.
   - Apply가 바꿀 모든 객체(기존 Apply Root와 그 아래 모든 항목)를 각자의 핸들로 열어 끝까지 유지합니다.
   - 그 핸들로 다음을 확인합니다.
     - 볼륨 파일 시스템이 NTFS입니다. 그 밖의 파일 시스템은 링크 수와 64비트 파일 ID를 신뢰하지 않고 거부합니다.
     - 링크 수가 정확히 1입니다.
     - 파일 ID가 0이 아니고, 이번 Apply의 다른 객체와 겹치지 않습니다.
   - 링크 수·파일 ID를 읽지 못하면 거부합니다(Fail-Closed).
   - 하드링크 위치가 R-ARCHIVE, 레이아웃 밖 디렉터리, 같은 Root 안 어디든 똑같이 거부합니다.

**변경 방식:**

- 없는 디렉터리(중간 디렉터리 포함)는 최종 보호 보안 설명자를 가진 채로 생성합니다. 상속 DACL로 노출되는 순간이 없습니다.
  - 생성한 경로는 즉시 생성 일지에 추가합니다.
  - 사전 점검 뒤 같은 이름이 먼저 생기면 거부합니다.
- **첫 변경 직전 재확인(G5-FIX-01):** 유지한 핸들마다 링크 수 1과 같은 파일 ID를 다시 확인합니다. 다르면 아무것도 쓰지 않고 스냅샷도 없이 `REFUSED`입니다.
- **전파 없는 객체 단위 변경(G5-FIX-01):**
  - 모든 변경은 `SetKernelObjectSecurity`로 그 핸들의 객체 하나에만 씁니다. 이전 판의 `SetSecurityInfo`는 하위 항목을 **이름으로 순회하며** 상속 ACE를 전파했습니다. 그 때문에 다른 경로에 링크가 있는 객체까지 닿을 수 있었습니다.
  - Root에는 유지한 핸들로 소유자와 보호 DACL을 씁니다.
  - 하위 항목마다 소유자와 Root ACE의 상속 사본을 씁니다. 디렉터리는 `OICIID`, 파일은 `ID`이고, 순서는 Root와 같습니다.
    - 이는 Windows가 이 ACE들에 대해 계산하는 값과 같습니다.
    - **[시험]** `acl.fix01_exact_descriptors_equal_windows_inheritance`: Apply 뒤 운영체제가 새로 만든 파일·디렉터리의 SDDL과 Apply가 쓴 SDDL이 같았습니다.
  - 각 쓰기 직전에는 같은 핸들로 링크 수·파일 ID를 다시 확인합니다.
  - 사전 점검 뒤 새로 생긴 항목도 쓰기 전에 같은 방법으로 확인합니다. 다른 경로와 링크된 항목은 쓰지 않고 Apply를 실패시킵니다.
- **마지막 재확인:** 모든 변경 뒤 유지한 모든 핸들을 한 번 더 확인합니다. Apply 중 링크가 추가되었으면 실패로 처리하고, 그 객체를 같은 핸들로 원래 설명자로 되돌립니다.
- **남는 한계(운영 전제 조건):**
  - 핸들 유지는 점검한 객체의 이름 변경·삭제·교체를 막습니다. 또 모든 쓰기는 확인한 핸들에 결속됩니다.
  - 그러나 쓰기 권한이 남은 주체가 Apply 중 새 항목을 만들거나, Root 파일에 대한 링크를 만드는 일 자체는 막지 못합니다.
    - 확인과 쓰기 사이의 극히 짧은 구간에 생긴 링크는 마지막 재확인에서 탐지되어 원복됩니다.
    - 사전 점검만으로 이후의 링크 추가를 완전히 막았다고 주장하지 않습니다.
  - 이것이 유지보수 조건(주체 프로세스 없음, 역할 계정 로그아웃·비활성화)이 필요한 이유입니다.
  - PowerShell·Win32 API만으로 이 경쟁을 완전히 없앴다고 주장하지 않습니다.

### 4.2 레이아웃 파일 (`starplayer-g5-layout-v1`, 도구 입력이며 계약 Schema 아님)

```json
{
  "format": "starplayer-g5-layout-v1",
  "owner_sid": "S-1-5-32-544",
  "principals": { "verifier": "<OWNER_VALUE>", "producer": "<OWNER_VALUE>", "reader": "<OWNER_VALUE>", "deployer": "<OWNER_VALUE>" },
  "roots": [
    { "id": "R-CONFIG",  "path": "C:\\ProgramData\\StarPlayer",     "apply": true,  "writers": [],           "readers": ["verifier"] },
    { "id": "R-BASE",    "path": "C:\\StarPlayer",                  "apply": true,  "writers": [],           "readers": ["verifier", "producer", "reader"] },
    { "id": "R-TRUST",   "path": "C:\\StarPlayer\\trust",           "apply": true,  "writers": [],           "readers": ["verifier"] },
    { "id": "R-BUILD",   "path": "C:\\StarPlayer\\build",           "apply": true,  "writers": [],           "readers": ["verifier", "producer"] },
    { "id": "R-WORK",    "path": "C:\\StarPlayer\\work",            "apply": true,  "writers": ["verifier"], "readers": [] },
    { "id": "R-AUDIT",   "path": "C:\\StarPlayer\\audit",           "apply": true,  "writers": [],           "readers": ["reader"] },
    { "id": "R-ARCHIVE", "path": "C:\\StarPlayer\\archive",         "apply": false, "writers": ["producer"], "readers": ["verifier", "reader"] },
    { "id": "R-DOTNET",  "path": "C:\\Program Files\\dotnet",       "apply": false, "writers": [],           "readers": [] },
    { "id": "R-GIT",     "path": "C:\\Program Files\\Git",          "apply": false, "writers": [],           "readers": [] }
  ]
}
```

- `<OWNER_VALUE>`는 SID 형식이 아니므로 도구가 사용 오류(종료 코드 2)로 거부합니다. 실제 값은 OWNER가 넣습니다.
- R-ARCHIVE의 writers·readers는 계약 값 확인 전의 자리표시입니다.

### 4.3 운영 적용 절차 (OWNER, 실행하지 않음)

1. **사전 기록(읽기 전용):**
   - `g5-acl.ps1 -Mode Plan`, `-Mode Verify -Report <R-AUDIT>\pre-verify.json`
   - Root마다 `icacls "<Root>" /save "<R-AUDIT>\pre-<id>.acl" /T /C`(보조 백업)
2. **적용:** 승격 PowerShell에서 실행합니다.
   - `g5-acl.ps1 -Mode Apply -Layout <layout> -Snapshot <R-AUDIT>\snapshot-<시각>.json -Report <R-AUDIT>\apply.json -OwnerApproved`
3. **사후 확인:**
   - `-Mode Verify` 재실행
   - 각 역할 계정의 비승격 세션에서 `g5-probe.ps1` 실행(6절)
4. **보존:** 레이아웃·스냅샷·보고서·`icacls` 출력의 SHA를 기록하고 덮어쓰지 않습니다. 영수증 Schema는 바꾸지 않습니다.

## 5. 권한 변경 실패 시 복구 절차

| 상황 | 동작 | 종료 코드 | OWNER 조치 |
|---|---|---|---|
| 사전 점검 실패 | 아무것도 바꾸지 않음, 스냅샷 없음 | 1 `REFUSED` | 출력의 원인을 해소한 뒤 재실행 |
| Apply 중 오류, 링크 추가 탐지, 사후 Verify·중첩 Root 비교 위반 | 스냅샷 항목을 상위부터 **각각** 복구(한 항목 실패가 나머지를 멈추지 않음)하고, 다시 읽어 비교하고, 생성 일지의 디렉터리를 깊은 것부터 비어 있으면 제거. 복구는 Apply가 유지한 그 객체의 핸들로, 전파 없이 씀 | 1 `ROLLED_BACK`: 차이 0, 남은 생성 경로 없음, 스냅샷 밖 객체 변경 없음 | 원인(출력 `cause=`)을 고친 뒤 새 스냅샷 경로로 재시도 |
| 복구 불완전 | 항목별 실패·차이·남은 생성 경로, 사전 점검 뒤 생겨 변경된 객체(원래 설명자가 스냅샷에 없음)를 `residue` 행에 모두 출력. 예외도 이 경로로 모임 | 3 `ROLLBACK_FAILED` | 아래 수동 복구 |
| 운영 중 의도적 원복 | `-Mode Restore -Snapshot <file> -OwnerApproved` | 0 `RESTORED` / 3 | — |

- **수동 복구 순서:**
  1. `icacls "<상위>" /restore "<R-AUDIT>\pre-<id>.acl"`로 DACL을 복구합니다(1단계 보조 백업).
  2. 스냅샷 JSON의 각 항목 소유자를 `icacls "<path>" /setowner *<SID>`로 맞춥니다.
  3. `g5-acl.ps1 -Mode Verify`와 스냅샷의 SDDL을 비교해 기록합니다.
- **스냅샷 규칙:**
  - 스냅샷 파일은 Apply 시작 전에 쓰고, 기존 파일은 덮어쓰지 않습니다(종료 코드 2).
  - 스냅샷은 소유자·DACL만 담습니다. SACL은 다루지 않습니다.
  - 형식은 `starplayer-g5-acl-snapshot-v3`입니다(G5-FIX-01). 항목마다 객체 식별값(`object` = 볼륨 일련번호:파일 ID)을 담습니다.
  - `-Mode Restore`는 경로를 새 핸들로 열고, 그 객체의 파일 ID가 스냅샷과 같을 때만 복구합니다. 그 사이 경로가 다른 객체(예: 다른 파일에 대한 링크)로 바뀌었으면 쓰지 않고 `residue`로 보고합니다.
  - 이전 형식 v2 스냅샷은 받지 않습니다. 운영 Apply가 실행된 적이 없어 v2 운영 스냅샷은 존재하지 않습니다.
- **한계:**
  - 자동 복구는 **보상 절차이며 트랜잭션이 아닙니다.** 다른 사용자 소유 항목의 소유자를 원래 계정으로 되돌리려면 SeRestorePrivilege가 필요합니다. 도구가 승격 세션에서 이를 켭니다.
  - Apply 중 정전·강제 종료가 나면 자동 복구가 실행되지 않습니다. 이때는 남은 스냅샷과 생성 일지로 `-Mode Restore`를 실행하거나 위 수동 복구를 씁니다.
  - 생성 일지는 디렉터리를 만든 직후에 씁니다. 그 사이 강제 종료가 나면 마지막 하나가 일지에 없을 수 있습니다.
  - 일지에 없는 기존 경로는 절대 삭제하지 않습니다.
- **[시험: 격리 진단]** 아래를 `selftest.ps1`에서 확인했습니다(G5 수정 보고서 3절). 소유자를 다른 계정으로 바꾸는 복구는 비승격 환경이라 **미검증**입니다.
  - 전체 복구: Apply 중 실패 → 모든 항목 SDDL 원복, 새 Root 제거, `ROLLED_BACK`
  - 첫 복구 항목 실패, 중간 복구 항목 실패: 나머지 Root는 원복되고 `ROLLBACK_FAILED`·종료 코드 3
  - 깊은 새 Root(`newA\newB\root`): 중간 디렉터리까지 일지에 3개가 기록되고 모두 제거됨
  - 생성 경로에 다른 쓰기가 생긴 경우: 남은 생성 경로가 `residue`로 보고되고 `ROLLBACK_FAILED`, 기존 경로는 그대로
  - 수동 Restore 왕복: 기존 명시 ACE가 정확히 복구되고 생성 Root가 제거됨

## 6. 반례 시험 (실행 파일 교체·TRX 변조·상위 경로 변경)

### 6.1 G5 ACL 반례 (`tools/b1-ops/g5-probe.ps1`)

시험할 계정의 비승격 세션에서 실행합니다. 권리 하나씩 핸들만 열고 즉시 닫으므로 아무것도 쓰거나 바꾸지 않습니다.

| 반례 | 대상 | 시도하는 권한 | 기대 |
|---|---|---|---|
| 실행 파일 교체 | R-BUILD·R-DOTNET·R-GIT의 파일(`dotnet.exe`, `hostfxr.dll`, 검증기 DLL, `testhost.exe` 등 `-Target`) | 데이터 쓰기·추가·삭제(이름 변경 포함)·속성·WRITE_DAC·WRITE_OWNER, 디렉터리 파일 추가 | 모든 역할 DENIED |
| TRX 변조 | R-WORK의 기존 TRX, R-WORK 디렉터리 | 쓰기·삭제·파일 추가 | 생산자·열람자 DENIED, 검증기는 writer로 허용 |
| 상위 경로 변경 | Root의 모든 상위 디렉터리(드라이브 Root 포함) | DELETE·하위 삭제·WRITE_DAC·WRITE_OWNER | 모든 역할 DENIED |
| Root 재권한 | 각 Root 디렉터리 | WRITE_DAC·WRITE_OWNER·DELETE | writer 포함 모든 역할 DENIED |

- 판정 코드: Win32 5 = DENIED. 0 또는 32(공유 위반, 접근 검사는 통과)는 ALLOWED입니다. 그 밖의 코드는 INCONCLUSIVE이며 실패로 봅니다.
- 승격 세션에서는 백업 권한 때문에 결과가 무의미하므로 실행을 거부합니다.
- **[관찰: 개발 PC, 비승격 개발 사용자]** `C:\Program Files\dotnet`과 `C:\Program Files\Git`에서 각 240개 시도가 모두 DENIED였습니다(`dotnet.exe`·`git.exe`와 표본 32개 파일, 상위 경로 포함).
  - 운영 계정에서의 결과가 아닙니다. 운영 증거는 G5 실측에서 각 역할 계정으로 다시 얻습니다.
- **[시험: 격리 진단에서 확인된 함정]** Root에 자기 DELETE 거부 ACE가 있어도, **부모가 FILE_DELETE_CHILD를 주면 Root를 삭제·이름 변경할 수 있습니다.**
  - 도구는 이를 `UNEXPECTED_ALLOWED`로 잡았습니다.
  - 그래서 Root 보호만으로는 부족하고, 상위 경로 규칙과 R-BASE 보호가 함께 필요합니다.

### 6.2 검증기 코드 수준 반례 (이미 G4에 있는 시험, 변경 없음)

| 반례 | G4 시험·동작 |
|---|---|
| 검사 후 실행 전 DLL·EXE 교체(F-03) | Pin 파일을 쓰기·삭제 공유 없이 열어 프로세스가 끝날 때까지 유지 |
| TRX 바꿔치기(F-17, G4-04) | 새 결과 디렉터리(`PinnedDirectory`), TRX 하나, 한 번 읽기. 실행 수준 오류는 `TestProfile.run_error`(G4-FIX-01) |
| 상위 경로·Reparse(G4-04) | 결과 디렉터리와 상위 Reparse 거부 |
| 다른 `dotnet`·Apphost로 시작(F-01·F-14) | `Execution.host_unpinned` |
| 출력 파이프 유지(G4-FIX-02) | 하나의 제한 시간과 취소, 프로세스 트리 종료 |

- G5는 위 코드 검사의 **예방층**입니다. 코드는 탐지·결속, ACL은 교체 자체를 막습니다(F-20).

## 7. 운영 모듈 Pin 수집·검증 도구 (`tools/b1-ops/g5-pins.ps1`)

| 모드 | 내용 |
|---|---|
| `Trace` | 명령 하나를 시작하고 프로세스 트리를 표본 추출합니다. 프로세스 이미지는 `PROCESS_STARTED`, 모듈은 `IMAGE_LOADED`로 기록합니다. **진단 전용**입니다(아래 한계). |
| `Adopt` | 추적의 원시 Snapshot(`<out>.raw.tsv`)에 같은 소속 규칙을 다시 적용해 ADOPTED·UNVERIFIED_PARENT를 출력합니다. 자체 시험과 사후 독립 대조에 씁니다. |
| `Collect` | 관찰 경로(Trace TSV 또는 경로 목록)를 Pin 후보로 만듭니다. 정규 경로, 경로 전체 Reparse 없음, SHA-256 계산 후 R-BUILD 아래(Build Provenance가 Pin) 파일은 제외합니다. `dotnet`·`git`·`execution_pins`는 설치 설정과 같은 모양(`path`, `raw_sha256`)이며 `status = CANDIDATE_NOT_APPROVED`입니다. |
| `Verify` | 후보 파일이나 설치 설정의 모든 Pin을 쓰기·삭제 공유 없는 핸들로 다시 해시합니다. MISMATCH·MISSING·REPARSE·DUPLICATE는 실패입니다. |

- **OWNER 정책 D-2·D-3 반영:** 수집 대상에는 공유 런타임과 Windows(System32) 모듈이 모두 포함됩니다. 경로·Root에 따른 자동 허용은 없습니다.
- **프로세스 소속 규칙(G5-R05):**
  - 프로세스 인스턴스는 (PID, 정확한 생성 시각)으로 식별합니다. 생성 시각은 마이크로초 단위 UTC이며, CIM과 `Process.StartTime`이 개발 PC 235개 프로세스에서 이 단위로 정확히 일치했습니다. 시간 허용 범위는 두지 않습니다.
  - 같은 Snapshot에서 부모 PID를 가진 살아 있는 인스턴스가 이미 추적 트리의 인스턴스와 같고, 자식이 그 뒤에 생성된 경우에만 편입(ADOPTED)합니다.
  - 부모 PID가 과거 트리 PID였지만 그 인스턴스를 입증할 수 없는 자식은 `UNVERIFIED_PARENT`로 기록만 합니다. 모듈을 읽지 않고 후보에도 넣지 않습니다. 부모가 끝났거나 다른 인스턴스가 PID를 재사용한 경우가 여기에 해당합니다.
  - 트리와 무관한 부모의 자식은 무시합니다. 관찰되지 않은 짧은 중간 프로세스의 자손은 누락될 수는 있지만, 잘못 포함되지는 않습니다.
  - **모듈 읽기의 핸들 결속(G5-R05 2차 보완):** 모듈은 `B1OpsNative.cs`의 `ModuleReader`로 **프로세스 핸들 하나**에서 읽습니다.
    - `OpenProcess`를 한 번만 호출합니다. 그 핸들로 생성 시각(`GetProcessTimes`)을 읽어 기록과 비교합니다.
    - 같은 핸들로 모듈 목록(`EnumProcessModulesEx`, `GetModuleFileNameExW`)을 읽습니다.
    - 읽은 뒤에도 같은 핸들에서 프로세스가 살아 있고 생성 시각이 같아야 합니다.
    - 핸들은 항상 닫습니다.
    - 이전 판은 `Process.StartTime`과 `Process.Modules`가 PID로 각자 프로세스를 다시 열었습니다. 그래서 그 사이 PID가 재사용되면 다른 인스턴스의 모듈을 읽을 수 있었습니다.
    - 열린 핸들은 프로세스 객체를 붙잡으므로, 읽는 동안 그 PID는 재사용되지 않습니다.
  - **결과 상태:**
    - 모듈이 기록되는 것은 `OK`뿐입니다.
    - `OTHER_INSTANCE`(다른 인스턴스), `NOT_RUNNING`(PID 없음), `EXITED_BEFORE_READ`, `EXITED_DURING_READ`(읽은 목록을 버림)에서는 모듈을 기록하지 않습니다.
    - `OPEN_FAILED`(핸들 획득 실패), `TIME_UNREADABLE`, `READ_FAILED`, `INCOMPLETE`(일부만 수집)는 `module_reads_failed`로 셉니다.
    - 모든 상태는 인스턴스별로 `module_read_statuses`에 남습니다.
  - **[시험]** 결정적 경쟁 반례는 `IProcessApi`를 대신하는 스크립트형 시험 대역으로 재현합니다. 실제 PID 재사용을 기다리지 않습니다.
    - 대상: 시각 확인 뒤 PID 재사용, 열거 전·중 종료, 핸들 획득 실패, 열거 실패·일부 수집, 다른 인스턴스
    - 모든 사례에서 열기는 1회이고, 연 핸들은 모두 닫혔습니다.
    - 이전 호출 형태(열기 2회)를 같은 대역에 적용한 모형은 재사용된 PID의 모듈을 읽었습니다.
    - 실제 자식 프로세스 추적도 같은 경로로 수행합니다(`pins.r05_*`).
  - 출력에는 PID·생성 시각·확인된 부모 인스턴스가 남습니다. `Collect` 후보의 각 파일에는 관찰된 추적 파일(`observed_in`)이 남습니다.
- **Trace의 한계:**
  - 비승격 표본 추출이라 두 표본 사이에 로드·해제된 모듈과 짧게 사는 자식 프로세스를 놓칠 수 있습니다. 예: 짧은 `git ls-tree` 실행에서는 `git.exe` 런처가 띄우는 `mingw64\bin\git.exe`가 관찰되지 않았습니다(긴 `git log` 실행에서는 관찰).
  - 파일 열기만 하는 입력(`.deps.json`·`.runtimeconfig.json`·TRX)은 기록되지 않습니다.
  - 운영체제 로더를 거치지 않고 매핑된 IL 어셈블리도 보이지 않을 수 있습니다.
  - 따라서 관찰되지 않은 파일은 `NOT_OBSERVED_IN_RUN`이며 "미사용"이 아닙니다.
- **진단 결과(개발 PC, `a980ea5` 빌드):** 보고서 3절에 있습니다. 공식 Pin 목록이 아닙니다.

## 8. 공식 실행 체인 동적 로드 추적 계획 (운영, 실행하지 않음)

[B-4 규범 결정 자료](B1_B4_OWNER_NORMATIVE_DECISION_DRAFT.md) 6절 원칙(관찰 상태, 반복, 확정 분류 금지)을 그대로 따릅니다. 이번에 확인한 사실로 대상과 도구를 구체화했습니다.

**8.1 추적 대상 체인** (개발 PC 진단에서 관찰한 실제 체인):

```text
dotnet.exe <verifier>.dll certify-phase ...                 검증기(모드 A: dotnet + DLL)
 ├─ git.exe -C <build_root> ls-tree / cat-file ...          고정 git (cmd\git.exe 런처 → mingw64\bin\git.exe)
 └─ dotnet.exe test <Test>.dll --filter ... --logger trx    Test Profile 실행 (4개 프로젝트)
     └─ dotnet.exe exec sdk\8.0.425\vstest.console.dll ...  VSTest 콘솔
         ├─ conhost.exe
         └─ <test folder>\testhost.exe --runtimeconfig ...  시험 출력 폴더의 Apphost (PROCESS_STARTED 관찰)
```

- **함의 1:** `testhost.exe`는 시험 출력 폴더의 Apphost로 **실제 실행됩니다**. Artifact 규칙 v2(DEPENDENCY)에 이미 포함되어 있습니다. 이 Apphost가 로드하는 `hostfxr`·`hostpolicy`·공유 런타임도 Pin 대상입니다.
- **함의 2:** 승인 `git`을 `cmd\git.exe`(런처)로 두면, 런처가 다른 실행 파일(`mingw64\bin\git.exe`)과 MSYS DLL을 시작합니다. 이것들은 검증기가 핸들로 붙잡지 못합니다.
  - 후보: 설치 설정 `git`을 `mingw64\bin\git.exe`로 직접 지정하고, 그 체인을 R-GIT ACL과 추적으로 보호합니다.
  - 이 선택은 OWNER 결정입니다. `GitIntegrity`가 어느 경로든 실행할 수 있는지는 운영 실측으로 확인합니다.

**8.2 도구** (모두 Windows 기본 제공, 다운로드 없음):

1. `wpr.exe` 또는 `logman`으로 아래 Provider의 ETW 세션을 만듭니다. 세션 시작에는 관리자 권한이 필요하며, 추적 대상은 검증기 서비스 계정으로 실행합니다.
   - `Microsoft-Windows-Kernel-Process`: 프로세스 시작·Image Load
   - `Microsoft-Windows-Kernel-File`: Create·Read. 설정 JSON·TRX처럼 열기만 하는 파일
   - `Microsoft-Windows-DotNETRuntime`: Loader 키워드. 어셈블리 로드 경로·로드 컨텍스트
2. `tracerpt`로 XML/CSV를 만든 뒤, 경로별로 상태(PROCESS_STARTED / IMAGE_LOADED / OPENED)와 PID를 정리합니다.
3. 결과 경로 목록을 `g5-pins.ps1 -Mode Collect`에 넣어 후보를 만들고, OWNER 승인 전까지 `CANDIDATE_NOT_APPROVED`로 둡니다.

**8.3 회차와 조건:**

- 각 대상 최소 2회
- UI 문화권 2가지(`ko-KR`·`en-US`)
- Pass·Fail Case가 있는 경로
- 공식 인증 거부 경로(설치 설정 없음, 앵커 불일치)

**8.4 금지·기록:**

- `DOTNET_`·`COMPlus_` 변수를 쓰지 않습니다. 원시 ETL·명령·환경 digest·파일별 SHA는 R-AUDIT 밖 진단 디렉터리에 보존합니다.
- 공식 인증 **성공** 경로의 추적은 G5 이후, OWNER 승인과 Registry·앵커가 준비된 뒤에만 합니다(현재 금지).

## 9. 남은 OWNER 결정 (G5 범위)

| 항목 | 결정 내용 |
|---|---|
| 운영 기계·드라이브 | 1절 구조 채택 여부, 보호 Root 실제 경로 |
| 계정 | 2절 역할별 계정·SID, 서비스 계정 형태 |
| R-ARCHIVE | 계약 §17 값에 따른 writers·readers |
| R-GIT | 설치 설정 `git`을 런처와 `mingw64\bin\git.exe` 중 무엇으로 할지. 개발 PC의 `Git\etc` 사용자 소유 파일 5개 같은 상태는 운영 기계에서 TCB 소유로 정리 |
| Pin | 8절 추적 결과에서 만든 `execution_pins`의 승인. Windows 업데이트 때 재승인 절차 |
| 적용 | 4.3절 적용과 6절 계정별 반례 실측 실행 승인 |
