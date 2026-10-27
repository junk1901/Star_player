# B1 G5 운영 준비·A-1 게시 준비 보고서

- 작성: Claude Code, 2026-10-28. 근거: OWNER 지시 "G4 구현 수용 및 Phase 3 잔여 작업 자율 진행"(2026-10-28)
- 기준:
  - Worktree `E:\Madegame\Star_player_g4`, 브랜치 `phase3/g4-official-entry`
  - G4 코드 수용 소스 `a980ea5a73ff2e3dfb032d2c3956d2d51c239711`, 문서 `20ec650524330a9cd06e309d2adaedcfd80003f2`
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
- **이 보고서는 G5·A-1 준비의 구현·시험 증거입니다.** 공식 Gate PASS, 운영 승인, Codex 검수 결과가 아닙니다. 독립 검수는 `미실시`입니다.

## 0. OWNER 승인 요청 요약 (우선 제출)

### 0.1 A-1 비공개 원격 생성·최초 게시

상세는 [A-1 게시 준비](../tasks/B1_A1_PUBLICATION_PREP.md) 8절입니다.

| # | 결정 | 권고 값 |
|---|---|---|
| A1-1 | 저장소·URL | `https://github.com/junk1901/star-player-official.git`, Private. 이 문자열을 모든 공식 clone에서 그대로 사용 |
| A1-2 | 게시 Commit·ref | `refs/heads/main` = 이 보고서를 담은 Commit(7절). 조상에 `a980ea5` 포함, 이력 재작성·강제 Push 없음 |
| A1-3 | Tag | `b1-code-accepted-2026-10-26`(→ `7bdde7dd…`) 게시 여부. 새 `b1-g4-code-accepted-2026-10-28`(→ `a980ea5`) 생성 여부(만들지 않음) |
| A1-4 | 저장소 밖 원문 편입 | OWNER 원문 14건(O19 이후), Codex B-4 5건, G4 5건. 편입하면 A1-2 Commit이 바뀜 |
| A1-5 | 개인정보 항목 수용 | 개발 PC 계정 SID 1건, 로컬 경로(`C:\Users\user`, `E:\Madegame`). 비공개 조건으로 수용 권고 |
| A1-6 | 공식 빌드 기준 Commit | 제품 코드가 `a980ea5`와 같은 최신 승인 Commit. 확정 후 A-1 문서 6절 재검증 |
| A1-7 | 읽기 전용 검증 자격 증명 | 저장소·로그 밖, 검증 후 폐기 |

- 비밀정보 패턴 검사(SECRET 7종): `20ec650` 59개 Commit **0건**. 이번 Commit의 결과는 6절에 있습니다.

### 0.2 실제 운영 ACL·Trust Anchor·Pin 설치 (다음 승인 묶음)

- 준비 완료: [G5 운영 설계](../tasks/B1_G5_OPERATIONAL_DESIGN_DRAFT.md) 1~8절과 도구 4종(`g5-acl`, `g5-probe`, `g5-pins`, `selftest`), 격리 자체 시험 36/36 PASS
- 승인 시 필요한 OWNER 값:
  - 운영 기계
  - 역할 계정·SID 6종
  - R-ARCHIVE 계약 권한
  - R-GIT `git` 경로 선택(런처 또는 `mingw64\bin\git.exe`)
  - Windows·런타임 Pin 승인(운영 기계 ETW 추적 후)

### 0.3 최종 운영 Gate 결과·Build Freeze

- G1~G9의 선행 조건·담당·증거·중단 조건은 [운영 Gate 준비표](../tasks/B1_OPERATIONAL_GATE_READINESS.md)에 있습니다.
- 공식 Gate PASS는 선언하지 않았습니다.

## 1. 수행 범위와 변경 파일

| 구분 | 파일 | 목적 |
|---|---|---|
| 도구 | `tools/b1-ops/g5-acl.ps1` | 보호 Root ACL Plan·Verify·Apply·Restore, 자동 복구 |
| 도구 | `tools/b1-ops/g5-probe.ps1` | 계정별 반례 시험(실행 파일 교체·TRX 변조·상위 경로 변경), 비파괴 |
| 도구 | `tools/b1-ops/g5-pins.ps1` | 로드 추적(진단)·Pin 후보 수집·Pin 재검증 |
| 도구 | `tools/b1-ops/a1-scan.ps1` | A-1 게시 전 비밀정보·개인정보 검사 |
| 도구 | `tools/b1-ops/b4-candidates.ps1` | B-4 Artifact 후보 규칙 v2 재산출·분류·이전 목록과 비교 |
| 시험 | `tools/b1-ops/selftest.ps1` | 위 도구의 격리 자체 시험 36건 |
| 설정 | `.gitattributes` | 새 도구·결정 노트에 `text eol=lf` 명시(G2 정책, 다른 B1 도구와 같음). 기존 규칙 변경 없음 |
| 문서 | `docs/tasks/B1_G5_OPERATIONAL_DESIGN_DRAFT.md` | G5 운영 설계 |
| 문서 | `docs/tasks/B1_A1_PUBLICATION_PREP.md` | A-1 게시 준비 |
| 문서 | `docs/tasks/B1_OPERATIONAL_GATE_READINESS.md` | G1~G9 준비표 |
| 문서 | `docs/tasks/B1_G5_A1_OPERATIONAL_PREP_DRAFT.md` | 후속 문서 링크 한 줄 추가 |
| 문서 | `Star/02_Decisions/2026-10-28_B1_G4_Code_Acceptance_And_Ops_Direction.md` | OWNER 결정 요약 |
| 문서 | 이 보고서, `CURRENT_STATE.md`, `Star/00_Dashboard.md` | 상태 기록 |

- **변경 없음:**
  - `src`, `game`, `tests`, `tools/StarPlayer.*`(검증기·SimHarness 코드)
  - 기존 B1 Tag, 승인 계약·OWNER 원문, Golden·Hash·RNG·Replay, 루트 `TASK.md`
  - 7절 diff로 확인했습니다.
- **코드 변경 목적:** 운영 준비 도구(PowerShell 스크립트)만 추가했습니다. 제품·검증기 바이너리에 들어가지 않으며 빌드 대상이 아닙니다.

## 2. 도구 시험

### 2.1 격리 자체 시험 (`selftest.ps1`)

- **환경:** 개발 PC, 비승격 개발 사용자(`elevated=False`). 새 Scratch Root 하나를 `-DiagnosticRoot`로 썼고 시험 후 삭제했습니다.
- Scratch 밖은 다음 두 가지만 읽기 전용으로 접근했습니다.
  - `C:\Program Files\dotnet`·`Git`에 대한 핸들 열기 시도(쓰기 없음)
  - `dotnet.exe` 해시
- **결과: 36 PASS / 0 FAIL** (TBD_SELFTEST_FINAL)

| 영역 | 사례 |
|---|---|
| g5-acl (18) | Plan 무변경 · 상속 트리 거부 · Apply PASS · 하위 상속 전용 · Root 보호 DACL · writer Modify · Apply 후 Verify PASS · 생산자 쓰기 ACE 탐지 · 비보호 Root 탐지 · 상위 DELETE 탐지 · Reparse 탐지 · 원복 후 PASS · 스냅샷 덮어쓰기 거부 · 비승격·미승인 Apply 거부 · 진단 Root 밖 거부 · 알 수 없는 주체 거부 · Restore 왕복(명시 ACE 복구·새 Root 제거, 차이 0) · 실패한 Apply 자동 복구(차이 0) |
| g5-probe (7) | 데이터 권한 DENIED · 부모 FILE_DELETE_CHILD로 Root 삭제 가능 탐지 · 열린 Root의 TRX 쓰기 탐지 · writer 데이터 권한 허용 · 소유자 Root WRITE_DAC 탐지 · 종료 코드 · 개발 PC dotnet·Git 교체 거부(각 240 DENIED) |
| g5-pins (6) | 후보 수집 · 재검증 PASS · 교체·삭제 탐지 · 중복 탐지 · Reparse 경로 거부 · 추적 모듈 관찰 |
| a1-scan (3) | HEAD에서 지운 비밀정보도 이력에서 탐지 · 개인정보 분류 · 깨끗한 저장소 PASS |
| b4-candidates (2) | 규칙 v2 역할 수(24/2) · 분류·관찰 상태 |

- 운영 값(실제 SID·운영 경로)은 쓰지 않았습니다. 다음은 **미검증**입니다.
  - 소유자를 다른 계정으로 바꾸는 Apply·Restore(승격 필요)
  - 승격 세션 거부 분기

### 2.2 개발 PC 읽기 전용 관찰 (운영 증거 아님)

| 대상 | 도구 | 결과 |
|---|---|---|
| `C:\Program Files\dotnet` (6,807 항목) | `g5-acl Verify` | **PASS** — TCB 밖 쓰기 계열 권한·비TCB 소유자·Reparse 없음 |
| `C:\Program Files\Git` (7,087 항목) | `g5-acl Verify` | **FAIL** — `etc\hosts`·`mtab`·`networks`·`protocols`·`services` 5개의 소유자가 개발 사용자 |
| 역할 분리 | `g5-acl Verify` | **FAIL** — 개발 사용자가 `BUILTIN\Administrators` 직접 구성원 |
| dotnet·Git 파일 교체·상위 경로 | `g5-probe` | 480개 시도 모두 DENIED |

- **함의:**
  - 개발 PC 사용자는 운영 역할 계정으로 쓸 수 없습니다.
  - 운영 기계의 Git 설치는 사용자 소유 파일이 없도록 정리하거나 확인해야 합니다. 소유자는 WRITE_DAC를 암묵적으로 가지므로 ACL을 바꿔 파일을 교체할 수 있습니다.

## 3. 로드 추적과 Pin 후보 (진단, 공식 Pin 아님)

- **대상:** `a980ea5` Clean Checkout(`E:\Madegame\g3x\g4clean3`, 통제 URL CI 빌드)
  - Test Profile 4개 시험 어셈블리 전체를 `dotnet test <dll> --logger trx …`로 실행했습니다. 작업 디렉터리는 clone Root(`global.json` SDK 8.0.425)입니다.
  - 공식 실행은 필수 Case 필터를 쓰므로 이 실행은 그 상위 집합입니다.
  - 각 2회 실행했고, 고정 git으로 `git -C <clone> log -p --all` 1회를 추가했습니다.
  - 모든 실행이 exit 0이었고 시험은 모두 통과했습니다.
- **도구:** `g5-pins.ps1 -Mode Trace`(표본 추출, 비승격), 이어서 `-Mode Collect -ExcludeRoot <clone>`, `-Mode Verify`
- **관찰된 실행 체인(PROCESS_STARTED):**
  - `dotnet.exe` 8회, `conhost.exe` 8회
  - 시험 폴더 `testhost.exe` 4개 프로젝트 모두
  - `Git\cmd\git.exe` 3회, `Git\mingw64\bin\git.exe` 3회
  - 체인: `dotnet test` → `dotnet exec sdk\8.0.425\vstest.console.dll` → `<시험 폴더>\testhost.exe`
- **Pin 후보:** `g5prep\pin_candidates_devpc_a980ea5.json`, 191개 파일. 재검증 `Verify` PASS(191/191). 상태는 `CANDIDATE_NOT_APPROVED`입니다.

| 분류 | 수 | 내용 |
|---|---:|---|
| DOTNET_HOST | 1 | `dotnet.exe` → 설치 설정 `dotnet` |
| DOTNET_HOSTFXR | 1 | `host\fxr\8.0.31\hostfxr.dll` |
| DOTNET_SHARED_RUNTIME | 87 | `shared\Microsoft.NETCore.App\8.0.31\*`(`hostpolicy`·`coreclr`·`clrjit`·`System.Private.CoreLib` 포함) |
| DOTNET_SDK | 39 | `sdk\8.0.425\`의 `dotnet.dll`·`vstest.console.dll`·TestPlatform·TRX Logger·`ko` 리소스 등 |
| WINDOWS | 56 | `ntdll`·`KERNEL32`·`KERNELBASE`·`ucrtbase`·`bcrypt` 등 System32 |
| GIT | 7 | `cmd\git.exe`(→ 설치 설정 `git` 후보), `mingw64\bin\git.exe`와 DLL 5개 |

- **G4 하한과의 관계:** G4 수정 때 검증기 결속 코드 프로세스에서 얻은 폴더 밖 파일 40개(`g4_loaded_pins_devpc.tsv`)는 이번 자식 체인 관찰에도 모두 들어 있습니다.
  - 다만 공식 인증 경로의 검증기 프로세스(SQLite·Pipe·XML 사용)는 이번에 추적하지 않았습니다. 이것은 공식 경로가 실행 금지 상태이기 때문입니다.
- **함의:**
  - 운영 `execution_pins`는 최소한 공유 런타임·SDK VSTest·Windows 모듈을 모두 포함해야 합니다(OWNER 방향 D-2).
  - `.deps.json`·`.runtimeconfig.json`·TRX 같은 열기 전용 파일과 짧은 수명의 모듈은 이 방식으로 보이지 않습니다. 따라서 운영 확정은 ETW 추적으로 합니다([G5 설계](../tasks/B1_G5_OPERATIONAL_DESIGN_DRAFT.md) 8절).

## 4. SourceLink 후보 URL 진단

- **방법:**
  - 작업 브랜치를 Scratch에 `git clone --no-hardlinks`한 뒤 `checkout --detach a980ea5`로 맞췄습니다.
  - `git remote set-url origin https://github.com/junk1901/star-player-official.git`으로 **설정만** 바꿨습니다. 원격 조회는 없습니다.
  - 검증기 프로젝트를 `-c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false -p:ContinuousIntegrationBuild=true`로 빌드했습니다(exit 0, 경고 0, 오류 0).
  - PDB는 기존 G3 진단 도구 `pedump`로 읽었습니다.
- **결과 [사실]:**

| 파일 | 후보 URL 빌드 DLL / PDB SHA-256 | 통제 URL 빌드(`g4clean3`, G4-FIX2 보고서 4절) DLL |
|---|---|---|
| `StarPlayer.EvidenceVerifier.dll` | `aa8eebd660eb7cd3fecf76b54e934b49f4723a9a6a5258a14a8320d379efbdd4` / `ea3b892a1857b0a08711cfaf6fd8672fbee8fbacbc9dd301447fbf3453e22990` | `2e9cd6ab…` |
| `StarPlayer.SimHarness.dll`(SimHarness 폴더와 검증기 폴더 사본 동일) | `09efe491e1f577e565b5865ac7f737cf7dda027684f67fba636e853812ecc4df` / `f1fb437bb3d61f6af1b636f2751aec44715fd91a11ddebb9f901ed88f76360a6` | `a82e6fc8…` |

- **SourceLink JSON**(세 파일 모두): `{"documents":{"/_/*":"https://raw.githubusercontent.com/junk1901/star-player-official/a980ea5a73ff2e3dfb032d2c3956d2d51c239711/*"}}`
- **차이의 범위:** 각 DLL의 길이는 통제 빌드와 같고 72바이트만 다릅니다. PDB는 URL 길이만큼 길어졌습니다.
  - 72바이트는 PDB 식별자 영역으로 **추정**합니다. 영역별 분류는 실행하지 않았습니다(G3 진단에서 같은 유형 확인).
- **결론:** URL은 `origin`을 그대로 따릅니다. A-1 이름이 정해지면 그 원격에서 clone한 빌드로 공식 SHA를 새로 측정해야 합니다.
  - 위 SHA는 **진단값**이며 공식 기대값이 아닙니다.
  - 원격 조회 가능성([A-1 문서](../tasks/B1_A1_PUBLICATION_PREP.md) 7절 절차)은 검증되지 않았습니다.
- Scratch clone은 시험 후 삭제했습니다. 빌드 출력 기록은 `E:\Madegame\g3x\g5prep\sourcelink_candidate_build.txt`입니다.

## 5. B-4 Artifact 후보 재산출 (`a980ea5`, 진단)

- **명령:** `b4-candidates.ps1 -BuildRoot E:\Madegame\g3x\g4clean3 -SourceCommit a980ea5… -Trace <3절 시험 추적 8개> -Previous E:\Madegame\g3x\artifact_candidates_v2_00091736.json`
- **결과 파일:** `g5prep\artifact_candidates_v2_a980ea5.json`, `CANDIDATE_NOT_APPROVED`
  - HEAD = `a980ea5` 일치
  - 추적 변경 1건은 verify가 덮어쓰는 `PHASE_2_VERIFY_LOG.md`입니다.
- **규칙 v2 결과:** 441행(DEPENDENCY 411, RUNTIME_CONFIG 24, TEST_ASSEMBLY 4, VERIFIER_BINARY 1, SIMHARNESS_BINARY 1), 비Pin 31개(PDB 27, Coverage 대응표 4)
- **`00091736` 후보와의 차이:**
  - 경로 추가 0, 삭제 0
  - 바이트가 바뀐 행 27개: 모두 StarPlayer 어셈블리(Commit·SourceLink 차이)
  - 같은 행 414개
- **분류와 관찰 상태(진단):**

| 분류 | 행 | IMAGE_LOADED | PROCESS_STARTED | NOT_OBSERVED_IN_RUN | NOT_TRACED(검증기·SimHarness 폴더) |
|---|---:|---:|---:|---:|---:|
| STARPLAYER(빌드 어셈블리) | 27 | 18 | — | — | 9 |
| TEST_HOST(`testhost.exe`·`.dll`) | 8 | 4 | 4 | — | — |
| VSTEST_IN_FOLDER(TestPlatform) | 32 | 24 | — | 8 | — |
| TEST_ADAPTER(xUnit runner) | 12 | 12 | — | — | — |
| TEST_FRAMEWORK(xUnit) | 16 | 16 | — | — | — |
| THIRD_PARTY_MANAGED | 12 | 8 | — | — | 4 |
| NATIVE(SQLite 등, RID별) | 42 | 1 (`win-x64\native\e_sqlite3.dll`) | — | 20 | 21 |
| SATELLITE_RESOURCE | 260 | 4 | — | 256 | — |
| APPHOST(StarPlayer `.exe`) | 6 | — | — | 3 | 3 |
| RUNTIME_CONFIG(`.deps.json`·`.runtimeconfig.json`) | 24 | — | — | 18 | 6 |
| OTHER(`e_sqlite3.a`) | 2 | — | — | 1 | 1 |

- **해석 원칙:** `NOT_OBSERVED_IN_RUN`은 미사용 판정이 아닙니다.
  - 예: 설정 JSON은 열기만 하므로 이 방식으로는 보이지 않습니다.
  - 규칙 v2는 폴더의 모든 파일을 고정하므로 관찰 여부와 무관하게 441행이 그대로 후보입니다.
- **구분 요약(OWNER 4항):**

| 구분 | 고정 수단 | 이번 근거 |
|---|---|---|
| 실제 사용 파일(시험 폴더) | Build Provenance 441행 | 91행 관찰(IMAGE_LOADED 87 + PROCESS_STARTED 4) |
| Runtime(공유 런타임·hostfxr) | 외부 `execution_pins` | 3절 88개 |
| Test Host | 시험 폴더 `testhost.exe`(DEPENDENCY, 실제 실행 확인) | 4개 프로젝트 모두 PROCESS_STARTED |
| VSTest | SDK 쪽은 `execution_pins`, 시험 폴더 쪽은 DEPENDENCY | SDK 39개, 폴더 32행 |
| 외부 실행 파일 | `dotnet`·`git` Pin(+ `mingw64\bin\git.exe` 체인) | 3절 |
| PDB·Coverage·Godot PCK | Build Provenance 역할에 추가하지 않음(OWNER 4항) | 비Pin 31개, PCK는 B-4 5절 별도 |

- **확정 시점:** A-1 원격과 승인 소스 Commit이 정해진 뒤 [A-1 문서](../tasks/B1_A1_PUBLICATION_PREP.md) 6절에서 다시 산출합니다. 이번 SHA는 공식 값이 아닙니다.

## 6. A-1 검사 결과

**명령:** `tools\b1-ops\a1-scan.ps1 -Repo E:\Madegame\Star_player_g4 -Commit <c> -Out <dir> -PublicRef refs/remotes/origin/main`

| Commit | Commit 수 / 공개 원격 밖 | SECRET 7종 | 이메일(값) | 계정 SID(값) | 사용자 경로(파일) | 로컬 경로(파일) | 결과 |
|---|---|---|---|---|---|---|---|
| `20ec650` | 59 / 40 | 0 | 2 | 8 | 113 | 180 | `NO_SECRET_PATTERN` |
| `0ee5162`(교체됨, 아래) | 60 / 41 | **2** | 3 | 8 | 115 | 182 | `SECRET_FOUND` |
| **`73b286c`** | 60 / 41 | **0** | 3 | 8 | 114 | 182 | `NO_SECRET_PATTERN` |

- **`0ee5162` 처리:** 이번 작업의 첫 도구 Commit `0ee5162`에서 `selftest.ps1`의 **가짜** 시험 값이 SECRET 패턴에 걸렸습니다. 해당 값은 개인 키 머리글과 AWS 형태 키입니다.
  - 이력 전체를 검사하므로 후속 수정 Commit으로는 해소되지 않습니다.
  - 그래서 몇 분 전 만든 **로컬 전용·미공유** Commit을 `reset --soft`로 되돌렸습니다. 가짜 값을 실행 시 조립하도록 고친 뒤 같은 메시지로 다시 Commit해 `73b286c`가 되었습니다.
  - 다른 Commit·Tag·원격에는 영향이 없습니다. 검사 출력은 `g5prep\a1scan_0ee5162_replaced_fixture_found\`에 보존했습니다.
- **`73b286c`의 새 PRIVACY 값:** 모두 가짜 시험 값이며 실제 값이 아닙니다.
  - 이메일: `selftest@example.invalid`
  - SID·경로: A-1 문서가 인용한 시험 값
- 개발 PC 실제 SID는 계속 `B1_INTEGRATED_REPORT.md` 1건뿐입니다.
- 이 보고서를 담는 이후 문서 Commit은 게시 직전에 다시 검사합니다([A-1 문서](../tasks/B1_A1_PUBLICATION_PREP.md) 2.2절 2단계).

## 7. Git·Clean Checkout 검증

- **Commit(로컬 전용, Push 없음):**
  - `73b286c` — 도구·설계 문서(`0ee5162`를 6절 사유로 교체)
  - 이 보고서 Commit — 보고서·문서 정정
  - 다음 문서 Commit — 아래 검증 결과·`CURRENT_STATE.md`·대시보드
- **절차:**
  1. 기존 `E:\Madegame\g3x\g4clean3`를 재사용합니다. 그 `a980ea5` CI 빌드에서 나온 3·5절 증거는 먼저 `g5prep`에 보존했습니다.
  2. 작업 브랜치에서 이 보고서 Commit을 fetch해 `checkout --detach`한 뒤 다음을 실행합니다.
     - `tools\verify.ps1` 전체
     - `tools\b1-ops\selftest.ps1`
  3. 제품·검증기·계약 파일이 `a980ea5`와 같은지 diff로 확인합니다.
- **결과:** 다음 문서 Commit에서 이 절에 추가합니다(7.1).

## 8. 증거 (저장소 밖, `E:\Madegame\g3x\`)

| 파일 | SHA-256 |
|---|---|
| `g5prep\pin_candidates_devpc_a980ea5.json` | `ddadfd3a1b3f804fe5d55e0f5472300bf4aa4218dfcb74bf57a51c3e11c11b8a` |
| `g5prep\artifact_candidates_v2_a980ea5.json` | `d32ed0ba4a4247616f3d05c4c1c6ab6b2349d067b50bc3d19f69c9e103f12188` |
| `g5prep\trace\`(정정 뒤 추적 9회, 파일 44개, 원시 TSV·stdout·stderr·TRX 결과) | 경로 기록 |
| `g5prep\trace_contaminated_pid_reuse\`(사용하지 않은 첫 추적) | 경로 기록 |
| `g5prep\run_traces.ps1` / `run_traces.log` / `analyze.py` | `e6609bc1…` / `6744bf83…` / `d6bb648d…` |
| `g5prep\a1scan_20ec650\summary.json` / `findings.tsv` | `6e1912c8…` / `e11348c1…` |
| `g5prep\a1scan_73b286c\summary.json` / `findings.tsv` | `dcecdfe6…` / `fcfbdb5f…` |
| `g5prep\acl_verify_devpc.json` / `.txt`, `layout_devpc_observe.json` | `ff429940…` / `f2027357…`, `b5c57275…` |
| `g5prep\selftest_worktree_run.json`(Worktree 실행, 가짜 값 조립 변경 전) | `8172b59d…` |
| `g5prep\sourcelink_candidate_build.txt` | `ed5015b4…` |
| `codex-originals-g4\manifest.json`(+ `20261027T164155Z_request-…` `0c4d8490…`, `20261027T164628Z_verdict-g4-fix2-final-pass.txt` `2f149042…`) | `075384a493145abcebefb9b811ee9d22270228527e0469bf589fab719506818d` |
| `owner-originals-post-o19\manifest.json`(OWNER 원문 14건) | `6d97f887be069ebadf9d9d68f480e3106e1a3567d9e6313e264eb7c1256bd003` |

- `findings.tsv`에는 PRIVACY 원값(경로·SID)이 들어 있으므로 저장소에 넣지 않습니다.

## 9. 해결·미해결·기술 부채·편차

**작업 중 발견하고 고친 문제(도구 자체):**

| 문제 | 원인 | 조치 |
|---|---|---|
| 첫 추적 결과에 무관한 `bash.exe`·`python.exe`·AhnLab V3 DLL이 섞임 | 끝난 추적 프로세스의 PID를 다른 프로세스가 재사용했고, 그 자식이 추적 트리로 들어옴 | 프로세스를 (PID, 생성 시각)으로 식별하도록 수정한 뒤 추적을 다시 실행. 오염된 첫 결과는 `g5prep\trace_contaminated_pid_reuse\`에 보존했으며 사용하지 않음 |
| Satellite 리소스가 VSTest 분류로 잡힘 | 분류 순서 | Satellite 판정을 먼저 수행 |
| PowerShell 5.1 특성(바이트 Enum `-band`, List를 담은 해시표 JSON 직렬화, 단일 값 `.Count`, 네이티브 인자 따옴표, 스크립트 이름과 같은 함수 이름) | 런타임 차이 | 각각 수정하고 자체 시험으로 확인 |

**미해결·한계:**

- **ACL 도구:**
  - 그룹 중첩 해석, Deny 우선 계산, 유효 권한 계산을 하지 않습니다. Allow ACE의 주체를 그대로 판정하므로 보수적입니다.
  - SACL은 다루지 않습니다.
- **Probe:** 핸들 열기로만 판정합니다. 실제 교체 시도는 하지 않으며, 백업 권한이 있는 승격 세션은 거부합니다.
- **Trace:** 표본 추출이라 누락이 있습니다. 예를 들어 짧은 `git ls-tree` 실행에서는 `mingw64\bin\git.exe`를 놓쳤습니다(긴 `git log` 실행에서는 관찰).
  - 정정 뒤 추적에서는 AhnLab V3 모듈이 `dotnet`·`testhost`·`git` 프로세스에서 관찰되지 않았습니다.
  - 운영 기계의 보안 제품 주입 모듈 여부는 ETW로 다시 확인해야 합니다.
  - 운영 Pin은 ETW 추적([G5 설계](../tasks/B1_G5_OPERATIONAL_DESIGN_DRAFT.md) 8절)으로만 확정합니다.
- **a1-scan:** 정규식 검사입니다. 이진 파일 내용은 검사하지 않습니다.
- **원문 색인:** 저장소의 `originals/index.json`·`INDEX.md`에 O17~O19·C11·C12가 빠져 있습니다. 원문 폴더라 고치지 않았습니다(A1-4 결정과 함께 처리 권고).

**설계상 OWNER 결정으로 넘긴 것:**

- R-GIT `git` 경로(런처 또는 실제 실행 파일)
- 개발 PC Git 사용자 소유 파일과 같은 상태의 운영 정리
- R-ARCHIVE 계약 권한
- Windows 업데이트 시 Pin 재승인 절차

**편차:** 없음. OWNER 지시 2~5항 범위 안의 진단·도구·문서만 수행했습니다.

**검증 Clone 정리 후보:** 아래는 `E:\Madegame\g3x`의 이전 진단 clone·출력 폴더입니다. 삭제는 OWNER 승인이 필요하므로 **삭제하지 않았습니다**.

- `c1`, `c2`, `c3long`, `fa`, `fb`, `fclong`, `fv`
- `g4clean`, `g4clean2`, `nci`, `nci2long`, `npa`, `npblonger`, `ntag`, `nv`
- `pa`, `pb`, `pd`, `pcxlong`, `sma`, `smblonger`, `smv`
- `out`, `full-out`, `pck-out`

이번 작업은 기존 `g4clean3`를 재사용했습니다(7절).

## 10. 하지 않은 것

- 원격 생성·Push·Fetch, Tag 생성·이동, 기존 공개 저장소 Push
- 운영 ACL 변경, Trust Anchor·설치 설정 설치, 공식 Pin·승인값 생성·등록, 계정 생성
- 공식 인증 성공 경로 실행, 운영 G1~G9 실행, Build Provenance 등록
- 기존 B1 Tag·승인 계약·OWNER 원문·Golden·Hash·RNG·Replay·루트 `TASK.md` 변경
- 기존 증거 폴더 삭제. 정리 후보는 9절에 있습니다.
- Codex 검수 요청 작성. OWNER 지시 7항이 승인 지점 보고를 우선하므로 별도 요청서는 만들지 않았습니다.
