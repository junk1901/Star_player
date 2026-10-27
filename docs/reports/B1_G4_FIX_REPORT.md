# B1 G4 Codex FIX_REQUIRED(G4-01~04) 표적 수정 보고서

- 작성: Claude Code, 2026-10-27. 근거: OWNER 지시 "G4 Codex FIX_REQUIRED 표적 수정 및 재검증"(2026-10-27)
- **성격:**
  - Claude Code의 수정·자체 시험 기록입니다. Codex 표적 재검수는 **미실시**입니다([요청서](../tasks/B1_G4_FIX_CODEX_REVIEW_REQUEST.md)).
  - 공식 G4·G5 PASS, 보안 수용, 운영 승인이 아닙니다.
  - 승인 계약·닫힌 Schema·B1 Tag·Golden·Hash·RNG·Replay·루트 `TASK.md`·원문은 바꾸지 않았습니다. 기존 시험의 기대값도 바꾸지 않았습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준·Commit·원문

| 항목 | 값 |
|---|---|
| Worktree·브랜치 | `E:\Madegame\Star_player_g4`, `phase3/g4-official-entry`(로컬 전용) |
| 검수된 G4 | 구현 `430e306f5cfec3eff5cc6ffe7eb62e08e46eded9`, 문서 HEAD `01d1626bbdfcfc28a59b154128db1e0ecc1ab821` |
| 수정 코드 Commit | `ca312267df14b0bea6bf25d5bd69df0e363b5114`(G4-01~04, NOTE) |
| **검증 소스 Commit** | **`6110d5fcfde0a1d89b8aab1a6e5f84213d598d84`** — `ca31226` + 시험 격리 수정(3.2절)과 이 보고서 이전 문서. `tools`·`src`·`game` 트리는 `ca31226`과 같음 |
| 수정 diff | `git diff 01d1626 ca31226`: 9개 파일, +628 / −135. 저장소 밖 사본 `E:\Madegame\g3x\g4fix_01d1626_ca31226.diff`(1,131행, `git diff` 출력 SHA `8875a04e…`) |
| Codex 원문(로컬 보존, Git 밖) | `E:\Madegame\g3x\codex-originals-g4\`: 요청 13487행(`f050bc35…`), 판정 13689행 FIX_REQUIRED(`2ac5cafe…`), `manifest.json`(`14904b02…`). 모두 NOT_INDEPENDENTLY_VERIFIED. 요청 본문은 Codex 첨부 파일로, 이번에는 복사하지 않음 |

| 파일 | blob(`ca31226`) | 변경 |
|---|---|---|
| `tools/StarPlayer.EvidenceVerifier/ExecutionBinding.cs` | `f922e065b3c1` | G4-01, HoldTree 역방향 검사 |
| `tools/StarPlayer.EvidenceVerifier/InstallConfig.cs` | `7410a7844c51` | `allowed_module_roots` 제거, `allowed_dynamic_assemblies` 추가 |
| `tools/StarPlayer.EvidenceVerifier/TestProfiles.cs` | `abe3d0cbb3d9` | G4-02, G4-04 |
| `tools/StarPlayer.EvidenceVerifier/Certification.cs` | `d4fcc923083d` | G4-03(`InitialCapture`, `Open`의 `initial`), 공식 세션 R-WORK 보호 요구 |
| `tools/StarPlayer.EvidenceVerifier/OfficialEntry.cs` | `1836da7af71a` | G4-03 순서 |
| `tools/StarPlayer.SimHarness/PathSafety.cs` | `4a0cf79f48aa` | `PinnedDirectory` `internal` → `public`(동작 불변) |
| `tests/StarPlayer.EvidenceVerifier.Tests/G4ChildProgram.cs` | `f1a7cb17a4d5` | 새 파일: 격리 자식 프로세스 진입점, 공용 TEST_ONLY 값 |
| `tests/StarPlayer.EvidenceVerifier.Tests/G4OfficialEntryTests.cs` | `0e474dc5f060` | 시험 12 → 16 |
| `tests/StarPlayer.EvidenceVerifier.Tests/StarPlayer.EvidenceVerifier.Tests.csproj` | `b1ca985a5c69` | `GenerateProgramFile=false`(자체 `Main`) |

## 2. Finding별 해결

### G4-01 (MAJOR) 비Pin 모듈과 실제 로드 결속

| 원칙(OWNER 2항) | 구현 |
|---|---|
| 1. Windows 하위 경로라는 이유로 신뢰하지 않음 | `%windir%` 허용 분기 삭제 |
| 2. 허용 Root라는 이유로 신뢰하지 않음 | 설치 설정의 `allowed_module_roots` 필드 삭제. 그 필드가 있는 설정은 `schema_invalid`로 거부 |
| 3. StarPlayer 어셈블리 위치·SHA 결속 | `StarPlayer.*`는 검증기 폴더 안이어야 하고, 그 폴더의 모든 파일은 Provenance Pin과 같은 바이트로 핸들 유지 |
| 4. SimHarness 사본과 실제 사용 어셈블리 구분 | V-1(사본 바이트 = SIMHARNESS_BINARY)과 별도로, 실제로 쓰는 `typeof(Archive).Assembly`의 위치가 그 사본이고 기본 로드 컨텍스트인지 확인(`Execution.simharness_load`) |
| 5. 같은 이름 중복·별도 ALC 검출 | 모든 컨텍스트에서 같은 이름이 2개 이상이면 거부, 기본이 아닌 `AssemblyLoadContext`면 거부 |
| 6. `Assembly.Load(byte[])`·위치 없는 비동적 로드 거부 | 거부. byte 로드는 별도 컨텍스트(`Assembly.Load(byte[], ...)`)로 잡힘 |
| 7. `IsDynamic` 예외의 한계 | 기본 허용을 없앰. OWNER가 나열한 이름(`allowed_dynamic_assemblies`, 기본 빈 목록)만, 기본 컨텍스트에서만 허용. 한계: 동적 어셈블리는 이미 실행 중인 코드만 만들 수 있으므로 디스크의 비Pin 코드 경로는 아니지만, 그 내용은 검사하지 않음 |
| 8. 예외가 없으면 Fail-Closed | 모든 로드 파일이 Pin(Provenance 또는 설치 설정 `execution_pins`)이 아니면 거부. 현재 OWNER 예외 집합이 없으므로 실제 PC에서는 Pin 없이는 항상 거부 |

- **OWNER_DECISION_REQUIRED:** OS·공유 런타임 모듈의 예외 정책. 실측값은 6절입니다. 임의 허용 목록·서명 신뢰·Runtime 자동 신뢰는 만들지 않았습니다.
- **한계:** 두 검사 사이에 로드됐다가 해제된 Native 모듈은 보이지 않습니다(표본 검사). 이미 실행된 악성 코드를 복구하지 못하며, 예방은 G5 ACL과 승인 시작 환경입니다.

### G4-02 (MAJOR) Test Profile 종료 코드

- 종료 코드와 TRX를 함께 판정합니다.

| 종료 코드 | TRX | 결과 |
|---|---|---|
| 0 | 있음, 실패 Case 없음 | 결과로 인정 |
| 1 | 있음, 실패 Case 있음 | 결과로 인정. 실패는 FAIL Case로 남음(숨기지 않음) |
| 0 | 실패 Case 있음 | `TestProfile.exit_inconsistent` |
| 1 | 실패 Case 없음(전부 Passed) | `TestProfile.exit_code` |
| 0·1 | 없음·2개 이상·잠김 | `TestProfile.trx` |
| 그 밖 | 무관 | `TestProfile.exit_code`(실행 인프라 실패) |
| 시작 실패 | — | `TestProfile.start_failed` |
| 시간 초과 | — | `TestProfile.timeout`(프로세스 트리 종료) |
| 취소 | — | `TestProfile.cancelled`(새 `CancellationToken` 인자) |

- 상위 코드는 기존 `Evidence.test_profile_mismatch`입니다. 새 Schema·인증 상태는 없습니다.

### G4-03 (MINOR) E2-1 순서

- 공식 진입점의 첫 단계가 `Certification.InitialCapture`입니다. `DOTNET_`·`COMPlus_` 검사와 C0 수집을 설치 설정·앵커·Registry보다 먼저 합니다.
- 그 C0를 `Certification.Open(..., initial)`에 넘기고, `Open`은 C0를 다시 수집하지 않습니다. C1·C2는 그 C0와 비교합니다(조용한 재기준화 없음).
- 결속 세션에 `initial`이 없으면 `Execution.initial_capture_missing`입니다. 결속 없는 TEST_ONLY는 기존과 같습니다.
- 추가 시작 정책(`startup_forbidden_prefixes`)은 E2-1 뒤에 검사하며, 정식 목록은 OWNER 미확정(N-7)입니다.

### G4-04 (MINOR) TRX TOCTOU

- 작업 Root와 새 결과 디렉터리를 `PinnedDirectory`(볼륨 루트부터 상대 핸들, 링크 거부, FILE_SHARE_DELETE 없음, 결과 디렉터리는 원자적 새로 만들기)로 고정하고, 자식 실행과 TRX 읽기가 끝날 때까지 유지합니다. 그동안 디렉터리·상위 경로는 이름 변경·삭제·교체할 수 없습니다.
- TRX는 쓰기·삭제 공유 없이 **한 번** 엽니다. 그 핸들로 다음을 확인합니다.
  - 실제 경로(`GetFinalPathNameByHandle`) = 기대 경로. Reparse·경로 이탈은 거부됩니다.
  - 공식 세션이면 소유자·쓰기 주체
  - 결과 디렉터리의 TRX가 정확히 1개
- 확인한 바로 그 핸들에서 바이트를 읽어 파싱합니다. 같은 경로를 다시 열지 않습니다.
- **R-WORK 쓰기 주체 검사:** 공식 세션(`RequireProtectedWorkTree`)은 작업 Root·결과 디렉터리·TRX의 소유자와 쓰기 가능 주체가 NSC-4 TCB(SYSTEM·Administrators·TrustedInstaller)와 이 계정(+CREATOR OWNER)뿐이어야 합니다(`TestProfile.results_acl`).
- **[관찰: 개발 PC]** 이 PC의 `%TEMP%`는 `CodexSandboxUsers` 그룹(Modify)과 AppContainer 능력 SID(Full Control)에 쓰기를 줍니다. 그래서 `%TEMP%` 아래는 공식 R-WORK가 될 수 없고, 검사는 이를 거부합니다. 비공식(TEST_ONLY·verify) 경로는 핸들 고정·한 번 읽기만 적용하고 이 ACL 규칙은 적용하지 않습니다.
- **G5 의존:** 운영 R-WORK의 실제 위치·계정·ACL은 실측하지 않았습니다. 디렉터리 핸들은 그 안의 파일 내용 불변을 보장하지 않으며, 파일 내용 보호는 TRX 핸들(쓰기 공유 없음)과 R-WORK ACL에 의존합니다.

### Codex NOTE — HoldTree 누락 파일

- Provenance가 그 폴더 아래에 선언한 역할별 Artifact가 디스크에 하나라도 없으면 `Execution.artifact_missing`으로 거부합니다.

## 3. 시험

### 3.1 G4 시험 16개 (`G4OfficialEntryTests`)

| 시험 | 정상 경로 | 실패 반례 |
|---|---|---|
| 설치 설정 | 정규 Root, SHA, 빈 동적 목록 | 부재, Elevated, ACL, 알 수 없는 필드, 정렬, 상대경로, Root 안 앵커, **`allowed_module_roots`가 있는 설정** |
| 호스트·Pin 유지 | 결속 중 쓰기 불가, 해제 후 가능 | — |
| 시작 환경·호스트·Pin | — | `CORECLR_` 변수, Apphost, Pin SHA |
| 검증기·SimHarness·폴더(프로세스 안) | — | VERIFIER_BINARY 경로, 사본 SHA, **실제 사용 SimHarness가 다른 어셈블리**, 일치 항목 없음, 비Pin 폴더 파일, **비Pin 공유 런타임 로드** |
| **격리 자식 프로세스** | 로드된 전부를 Pin한 결속이 통과 | **비Pin Windows 모듈(`msimg32.dll`)**, **비Pin 런타임 모듈(`Microsoft.DiaSymReader.Native.amd64.dll`)**, **`Assembly.Load(byte[])`**, **별도 ALC**, **같은 이름 중복** |
| 폴더 유지 | 나열된 비Pin 출력 | 비Pin 추가, 바뀐 바이트, **선언됐지만 없는 파일** |
| **E2-1·C0** | 진입점 C0 = 세션 C0 | **Bootstrap 중 환경 변경(C1 ≠ C0)**, 결속 + C0 없음, Archive Root, VERIFIER_BINARY 경로 |
| 자식 실행 파일 | 절대 `dotnet` | 이름 `dotnet`, **시작 실패** |
| **종료 코드·TRX** | 0 + Passed, 1 + Failed | 1 + 전부 Passed, 1 + 없음, 0 + 없음, 0 + Failed, 3 + Passed, TRX 2개 |
| **시간 초과·취소** | — | 2초 제한, 2초 뒤 취소 |
| 실제 자식 실행 | 폴더 유지 중 실제 `dotnet test` 통과, DLL 쓰기 불가 | — |
| **TRX 교체** | 연 뒤 쓰기·이동·삭제가 모두 실패하고 원래 바이트로 결과 | **실행 중 결과 디렉터리·상위 Root 이동 실패** |
| **R-WORK** | 보호 DACL 디렉터리에서 정상 단일 TRX | **Junction 작업 Root**, **이 PC `%TEMP%`(다른 주체 쓰기)**, **Everyone 쓰기 Root**, **Everyone 쓰기 TRX(다른 계정 수정 가능)** |
| CLI 인자 | 모든 인자 1회 | `--anchor`, 중복, 누락, family, usage |
| **CLI 순서** | — | **금지 변수 + 설정 부재**, **금지 변수 + 앵커 부재 → E2-1이 먼저**, 설정 부재, 시작 정책, 앵커 부재. 모두 Registry 미도달 |

- 격리 자식은 이 시험 어셈블리를 `dotnet exec`로 실행합니다(`G4ChildProgram.Main`). 결속을 위한 Pin은 그 프로세스가 로드한 파일로 만든 TEST_ONLY 대역이며 OWNER 값이 아닙니다.
- 종료 코드·TRX 시험은 같은 진입점이 `dotnet test`를 흉내 내어 정해진 종료 코드와 TRX를 씁니다(`TestRunner.LeadingArguments`, 시험 전용).
- **구현 중 실패와 수정(실제 출력):**
  - 1차: `%TEMP%` ACL 때문에 R-WORK 검사가 5개 시험을 거부 → 6절의 관찰. 공식 세션 전용 규칙으로 범위를 정함
  - 자식 기준 실행이 늦게 로드된 `System.Threading.dll`로 거부 → 대역 Pin을 로드가 수렴할 때까지 다시 수집하도록 시험 쪽을 고침
  - 자식 출력 인코딩(cp949) → UTF-8 고정
  - 중복 시나리오가 기본 컨텍스트에 로드되지 않은 Domain을 썼음 → SimHarness로 바꿈. 중복 검사를 감시 결과보다 먼저 보도록 순서를 정함

### 3.2 전체 회귀 (실제 명령 출력)

| 실행 | 기준·위치 | 결과 | 증거 |
|---|---|---|---|
| G4 시험 16개 | Worktree | 16/16 PASS(9초) | `dotnet test … --filter FullyQualifiedName~G4OfficialEntryTests` |
| `verify.ps1` | Clean clone `E:\Madegame\g3x\g4clean`, `ca31226` | **GATE: FAIL** — U6 85/86. G4 E2-1 시험이 `c1_differs` 대신 `forbidden_variable`을 받음 | [B1_G4FIX_VERIFY_LOG_clean_FAIL.md](B1_G4FIX_VERIFY_LOG_clean_FAIL.md)(`d57b6e0e…`) |
| 원인 | — | 병렬로 실행되는 `T11CertificationTests`가 정적 `Certification.VariablesSeam`을 시험 도중 null로 되돌림. 제품 결함이 아니라 시험 격리 결함(Worktree에서는 시점 차이로 통과) | `g4clean\build\verify\U6.log` |
| 수정 | `6110d5f` | 정적 Seam을 쓰는 두 시험 클래스를 같은 xUnit Collection으로 직렬화(속성만, 기대값 변경 없음) | — |
| U6 전체 | Worktree | 86/86 PASS(21분 11초, verify 한도 30분 안) | — |
| `verify.ps1` | Clean clone `E:\Madegame\g3x\g4clean2`, `6110d5f`(clean, D2 0건) | **GATE: PASS**, **3,057 PASS / 0 FAIL / 0 SKIP** | [B1_G4FIX_VERIFY_LOG_clean2.md](B1_G4FIX_VERIFY_LOG_clean2.md)(`3b2d5348…`) |
| Golden | 같은 clone | `Evidence.GoldenTests` **4/4 PASS** | `E:\Madegame\g3x\g4fix_golden_clean2.txt`(`f8e54bdd…`) |
| Dry Pilot 9 | 같은 clone(보관 manifest 130개 복사, 이전 절차와 같음) | **`ALL_9_IDENTICAL=True`**: config_hash·run_result_hash·manifest 9행 모두 같음 | `E:\Madegame\g3x\g4fix_drypilot_clean2.txt`(`eee1232f…`) |

- verify 세부: U1 327, U2 741, U3 128(U3a 14·U3b 48), U4 1,237, U5 538, U6 86. B1~B3·G1~G4·E1~E4·D1(235개)·D3(5개) PASS
- 기준선 3,041에서 늘어난 16개는 모두 G4 시험입니다. 기존 시험 기대값과 Golden은 바꾸지 않았습니다.
- **게임 Hash·RNG·Replay 불변:** Dry Pilot 9 Run 동일, Golden 4/4, U2·U4·U5 PASS. 게임 계산 코드(`src`·`game`)는 G4 전체에서 바뀌지 않았습니다.
- **Godot Export:** E1~E4 PASS, `StarPlayer.pck` = `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799`(변경 없음). 5회 재현성 검사는 다시 하지 않음(**미검증**)

## 4. 기존 반례의 재현·정적 차단 근거

| Codex 반례 | 이전 동작 | 지금 |
|---|---|---|
| G4-01: 비Pin Windows 하위 경로 모듈 | 허용(194행) | 격리 자식에서 거부 재현(`msimg32.dll`) |
| G4-01: 허용 Root의 비Pin 모듈 | 허용 | 필드 삭제 + 격리 자식에서 런타임 Root 모듈 거부 재현 |
| G4-01: 별도 ALC에 Pin 파일 재로드 | 경로만 보고 허용 | 거부 재현(`g4-other`) |
| G4-01: 다른 SimHarness 실제 로드 | 사본 바이트만 확인 | `Execution.simharness_load` 재현 |
| G4-02: 비정상 종료 + 전부 Passed TRX | 결과로 반환 | `TestProfile.exit_code` 재현 |
| G4-03: `CORECLR_`만 추가 목록, `DOTNET_NOLOGO` 존재 + 앵커 오류 | 앵커 오류가 먼저 | `Environment.forbidden_variable`이 먼저, Registry 미도달 재현 |
| G4-04: 검사 후 TRX 교체 | 별도 열기 사이 교체 가능 | 연 핸들에서 쓰기·이동·삭제 실패 재현, 같은 바이트로 파싱 |

## 5. G5 ACL에 의존하는 부분

- R-WORK·R-BUILD·R-DOTNET·R-GIT·R-TRUST의 실제 운영 ACL·계정·SID: 미실측
- 다른 계정이 TRX를 **열기 전에** 바꾸는 경우: R-WORK ACL로만 막음(공식 세션은 그 ACL을 검사)
- 자식 프로세스(`vstest`·`testhost`·`git`)가 로드하는 Pin 밖 파일: 부모가 보지 못함
- 프로세스 시작 전·검사 사이의 로드·해제

## 6. OWNER 미확정 규범과 실측 자료

- **OS·공유 런타임 예외 집합(G4-01, OWNER_DECISION_REQUIRED):**
  - **[관찰: 개발 PC]** 시험 어셈블리의 격리 자식(검증기 결속 코드만 실행)이 로드한 폴더 밖 파일은 40개입니다. 공식 인증 경로는 SQLite·Pipe·XML 등을 더 로드하므로 이것은 **하한**입니다.
    - dotnet 설치 19개: `hostfxr`·`hostpolicy`·`coreclr`·`clrjit`·`System.Private.CoreLib` + 프레임워크 14개
    - Windows 21개: `ntdll`·`KERNEL32`·`KERNELBASE`·`ucrtbase`·`BCrypt`·`icu` 등
  - 목록(경로·크기·SHA): `E:\Madegame\g3x\g4_loaded_pins_devpc.tsv`(`0bf5b81b…`). 임시 진단 시나리오로 만들었고 그 코드는 Commit하지 않았습니다(`E:\Madegame\g3x\g4_pins_scenario_temp.diff`, `91a6376b…`).
  - **대안(OWNER 선택):**
    - (a) 운영 기계에서 실측한 파일 전부를 `execution_pins`로 바이트 Pin. 가장 강하지만 Windows·.NET 업데이트마다 갱신이 필요합니다.
    - (b) 공유 런타임은 바이트 Pin, Windows 모듈은 OWNER가 승인한 정확한 경로 목록. 새 규범이 필요합니다.
    - (c) 서명 기반 신뢰. 이번 지시로 금지되어 만들지 않았습니다.
- **동적 어셈블리 이름 목록:** `allowed_dynamic_assemblies`(기본 빈 목록)
- **그 밖:** N-5·N-6·N-7·N-9·N-10·N-11(변경 없음)

## 7. Git·SourceLink

- **Clean Checkout:** `git clone --no-hardlinks E:\Madegame\Star_player_g4 E:\Madegame\g3x\g4clean2` → `checkout --detach 6110d5f` → 작업 트리 0건
- **원격 URL:** clone의 `origin`을 `https://github.com/junk1901/Star_player.git`로 **설정만** 했습니다(조회·Push 없음). 이전 진단과 같은 **실험 통제값**이며 A-1 공식 원격이 아닙니다. A-1 URL이 정해지면 SourceLink와 DLL·PDB SHA가 모두 바뀝니다.
- **CI 빌드:** `dotnet build StarPlayer.sln -c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false -p:ContinuousIntegrationBuild=true`, exit 0(`E:\Madegame\g3x\g4fix_ci_build_clean2.txt`, `a7b62173…`)

| 파일 | DLL SHA-256 | PDB SHA-256 | SourceLink |
|---|---|---|---|
| `tools/StarPlayer.EvidenceVerifier/…/StarPlayer.EvidenceVerifier.dll` | `80788b2d6e7a71753693d133e6fd9854ada38805efd83ee0d02fe71716f080d5` | `c33fa9b19739d811…` | `/_/*` → `https://raw.githubusercontent.com/junk1901/Star_player/6110d5fcfde0a1d89b8aab1a6e5f84213d598d84/*` |
| `tools/StarPlayer.SimHarness/…/StarPlayer.SimHarness.dll` | `adc823e34526b84bf89979fb97549b993e4e2d9b25c52b8baf46c039c175c506` | `7ac698a3ca9198a7…` | 같음 |
| 검증기 폴더의 `StarPlayer.SimHarness.dll` 사본 | `adc823e3…`(SIMHARNESS_BINARY와 같음) | `7ac698a3…` | 같음 |

- SourceLink Commit = 실제 빌드 Commit `6110d5f`. 이 SHA는 진단값이며 Build Provenance로 등록하지 않았습니다.
- 브랜치 Commit(로컬 전용): `2c90a88` → `430e306` → `01d1626` → `ca31226` → `6110d5f` → 이 보고서 Commit
- **D1·D3·계약:** D1 235개 PASS, D3 5개 PASS(승인 계약 `1338a2f7…`, `TASK.md` `ecbc0859…` 포함). B1 Tag `b1-code-accepted-2026-10-26` → `7bdde7dd…` 불변

## 8. 검증하지 못한 사항

- 공식 승인값을 쓴 인증 성공 경로(불가, 금지)
- 운영 계정·ACL에서의 실제 교체·다른 계정 수정 반례(이 PC의 단일 계정에서 DACL로 흉내 냄)
- 공식 경로에서 검증기 폴더 SimHarness 사본의 동적 로드 추적
- 자식 프로세스의 로드 파일
- PCK 5회 재현성 재검사
