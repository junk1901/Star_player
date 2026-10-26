# B1 G4 공식 인증 진입점·실행 결속 구현 보고서

- 작성: Claude Code, 2026-10-27. 근거: OWNER 지시 "Phase 3 신속 완료를 위한 Claude Opus 5.5 자율 개발 위임"(2026-10-27) 1·3·6항
- **성격:**
  - Claude Code의 구현·자체 시험 기록입니다. 공식 보안 수용, G4·G5 PASS, 운영 승인이 아닙니다. Codex 독립 검수는 **미실시**입니다([요청서](../tasks/B1_G4_CODEX_READONLY_REVIEW_REQUEST.md)).
  - 승인 계약(B1-T00)·닫힌 Schema·B1 Code Tag·Golden·Hash·RNG·Replay·루트 `TASK.md`·원문은 바꾸지 않았습니다.
  - 공식 승인값(앵커·SID·Pin·설치 설정 값·승인 기록)은 하나도 만들지 않았습니다. 그래서 공식 인증 명령은 이 PC에서 항상 거부됩니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 작업 위치와 Commit

| 항목 | 값 |
|---|---|
| Worktree·브랜치 | `E:\Madegame\Star_player_g4`, `phase3/g4-official-entry`(로컬 전용, Push 없음) |
| 기준 | `96070c5fa1571b079df9c204597215db9da974ac` |
| 문서 Commit | `2c90a88` — 검수된 B-4 초안(`62ae952e…`)·요청서(`49f6d73c…`)·규범 결정 자료(`4ccc865c…`) 바이트 그대로 |
| G4 구현 Commit | 이 보고서를 포함한 Commit(SHA는 `git log`) |

## 2. 구현 범위 (규범 결정 자료 4절 G4-T1~T8)

| TASK | 해결 대상 | 구현 | 파일 |
|---|---|---|---|
| T1 공식 인증 CLI·설치 설정 | F-13 | `certify-family`·`certify-phase` 명령. 앵커·Root·Pin은 고정 위치 `%ProgramData%\StarPlayer\EvidenceVerifier\install-config.json`에서만 읽고 CLI 인자로 받지 않음. 순서: 설치 설정 → 결속 → 앵커 → Registry → `Certification.Open` → 기존 API로 검증·발급 | `OfficialEntry.cs`, `InstallConfig.cs`, `Program.cs` |
| T2 자식 실행 파일·작업 디렉터리 | F-01, F-02, F-09 | 자식 `dotnet test`는 절대경로·고정 작업 디렉터리(build root). 비공식 경로는 이 프로세스를 실행하는 런타임의 `dotnet.exe` 절대경로. 공식 세션은 Pin된 `dotnet`·`git` | `TestProfiles.cs`, `Certification.cs`, `GitIntegrity.cs`(실행 파일 인자만 추가) |
| T3 검증기 실행 이미지 결속 | F-07, F-14 일부 | 실행 중 DLL 위치 = Provenance VERIFIER_BINARY 경로, SHA와 일치하는 `verifiers[]` 항목이 정확히 1개, 영수증은 그 항목을 씀. 프로세스 이미지는 Pin된 `dotnet.exe`(Apphost 거부) | `ExecutionBinding.cs`, `Certification.cs` |
| T4 SimHarness 사본 결속 | F-06 | V-1: 검증기 폴더 사본 DEPENDENCY = SIMHARNESS_BINARY 바이트. V-2: 로드된 어셈블리·모듈이 Pin·Provenance 또는 `%windir%`·OWNER 허용 Root 안에 있어야 함. 이후 로드는 이벤트로 감시하고, 발급 직전과 E2-7에서 다시 검사 | `ExecutionBinding.cs` |
| T5 교체 방지(TOCTOU) | F-03, F-04, F-12 | Pin 파일을 쓰기·삭제 공유 없이 열어 그 핸들로 해시하고 계속 유지. 검증기 폴더와 각 시험 폴더는 **모든 파일**이 Pin이거나 OWNER가 나열한 비Pin 출력이어야 함. 시험 폴더는 자식 실행이 끝날 때까지 유지 | `ExecutionBinding.HoldTree`, `Certification.RunTestProfile` |
| T6 VSTest·Test Host | F-04, F-05 | 시험 폴더의 `testhost.exe`·xUnit runner는 폴더 전체 Pin으로 덮음. SDK 쪽 VSTest는 `execution_pins`로 Pin(목록은 OWNER·로드 추적 후) | 같음 |
| T7 TRX 보호 | F-17 | 실행마다 새 결과 디렉터리(Reparse·기존 존재 거부), 기대한 TRX 하나만 있어야 하며 한 번 읽음 | `TestProfiles.Run` |
| T8 시작 환경 통제 | F-08 | E2-1은 그대로 두고 OWNER 목록(`startup_forbidden_prefixes`)의 접두사 변수가 있으면 증거를 읽기 전에 거부. 자식 환경에서도 제거 | `ExecutionBinding.Establish`, `TestRunner` |

- **결속 필수성:** 공식(ResolvedOfficial) 승인 기록은 결속 없이 세션을 열지 않습니다(`Execution.unbound`). 결속 없는 TEST_ONLY 세션은 기존과 같습니다. 결속이 있으면 TEST_ONLY에도 같은 검사가 적용됩니다.
- **원인 코드:** 새 실패는 기존 17개 상위 코드 아래 새 원인 문자열을 씁니다(상위 코드 추가 없음).
  - `approval_mismatch`: `Trust.install_config_*`, `Execution.*`
  - `environment_mismatch`: `Environment.startup_policy`
  - `test_profile_mismatch`: `TestProfile.runner`·`results_directory`·`trx`·`roots`·`assembly_unknown`

## 3. 계약과의 관계

- **바꾸지 않은 것:** §13 승인 기록, §17 R03-1 앵커, §18.2 Environment Profile, §18.3 Build Provenance, §20.1 영수증, §21 역할표, E2-1 금지 접두사, Hash·A+·Replay·Golden
- **새로 둔 것:** 설치 설정 `starplayer-verifier-install-config-v1`. §16 T1 "OWNER가 관리하는 설치·운영 설정"의 구현 후보입니다. B1-T00 계약 Schema가 아니며, 형식·위치·Pin 방식은 OWNER 규범 결정(N-5·N-6·N-7) 전에는 공식으로 쓰지 않습니다.
- **해석으로 둔 것:** §21 "Hash한 뒤 같은 경로를 다시 열어 다른 파일을 파싱하지 않음"을 실행 파일에 적용하는 방식(T5 핸들 유지)은 Codex 1차 판정 6절의 "G4 수용 조건으로 명시" 방향을 따랐습니다(N-9 확정 필요).

## 4. 구현 중 확인한 사실

- **[사실] 동적 어셈블리:** 시험 호스트에는 파일 위치가 없는 런타임 내부 어셈블리 `Anonymously Hosted DynamicMethods Assembly`가 로드되어 있었습니다(첫 실행에서 V-2가 거부). 이미 로드된(Pin된) 코드가 메모리에서 만든 것이므로 `IsDynamic`은 허용하고, 위치 없는 **비동적** 어셈블리(`Assembly.Load(byte[])`, 실제 우회 경로)는 계속 거부하도록 고쳤습니다.
- **[사실] 핸들 유지와 자식 실행 양립:** Domain.Tests 폴더 전체를 핸들로 잡은 채 실제 자식 `dotnet test`가 통과했고, 그동안 시험 DLL 쓰기 열기는 `IOException`으로 거부됐습니다.
- **[관찰: 개발 PC]** `C:\ProgramData`는 일반 사용자에게 하위 폴더 생성을 허용합니다. 설치 설정 폴더는 TCB가 만들고 상속을 끊어야 합니다([G5 준비](../tasks/B1_G5_A1_OPERATIONAL_PREP_DRAFT.md) 1절).
- **[사실] 실제 CLI(이 PC):** `certify-family`는 설치 설정이 없어 `Evidence.approval_mismatch Trust.install_config_missing`, exit 1. `--anchor` 인자는 usage, exit 2. `environment`는 exit 0.

## 5. B-4 Artifact 후보 규칙 v2

- **규칙:** SIMHARNESS_BINARY + SimHarness·검증기·Test Profile 시험 4개 폴더의 **모든 파일** − PDB(A8)·Coverage 대응표(A9). 비Pin 파일은 설치 설정 `unpinned_output_files`에 정확한 RELPATH로 나열합니다(와일드카드 없음).
- **`00091736` N_S1 적용 결과[사실]:**
  - 441행: DEPENDENCY 411, RUNTIME_CONFIG 24, TEST_ASSEMBLY 4, VERIFIER_BINARY 1, SIMHARNESS_BINARY 1
  - 비Pin 31개: PDB 27, Coverage 대응표 4
  - 기존 109개는 모두 포함됩니다. 새로 들어간 332개: 위성 리소스 260, native 38, xUnit runner 12, json 12, Apphost 6, `testhost.exe` 4
  - 목록: `E:\Madegame\g3x\artifact_candidates_v2_00091736.json`(`4ff8a7254399f703c161580779502b5811b7cb4cabc088057a6f1d77cc59f402`, 저장소 밖)
- **의미:** 실제로 로드되는지와 무관하게 폴더에 있는 실행 가능 파일을 모두 고정하므로, B4-R01의 "확인 전 EXE 일괄 제외 금지"를 지키고 F-19의 보수적 포함 허용과도 맞습니다. 미사용이라고 분류하지 않습니다.
- **여전히 CANDIDATE_INCOMPLETE인 이유:**
  - SDK·공유 런타임 쪽 실행 Pin(`execution_pins`) 목록은 로드 추적 뒤 OWNER가 정합니다.
  - EXE를 DEPENDENCY로 두는 해석은 F-14·N-11로 확정이 필요합니다.
  - 새 소스 Commit과 A-1 원격 URL로 다시 빌드하면 SHA가 모두 바뀝니다.

## 6. 시험

### 6.1 새 시험 (`tests/StarPlayer.EvidenceVerifier.Tests/G4OfficialEntryTests.cs`)

| 시험 | 정상 경로 | 실패 반례 |
|---|---|---|
| 설치 설정 | 정규 Root, 바이트 SHA, 정렬된 접두사 | 부재, Elevated, ACL 위반, 알 수 없는 필드, 정렬 위반, 상대경로, build_root 안의 앵커 |
| 호스트·Pin·시작 환경 | Pin 파일이 결속 중 쓰기 불가, 해제 후 쓰기 가능 | `CORECLR_` 변수(대소문자 무시), Apphost 이미지, Pin SHA 불일치 |
| 실행 중 검증기·폴더·로드 | 폴더 전체 Provenance와 일치, 일치 항목 선택, `Inspect()` = null | 다른 VERIFIER_BINARY 경로, 다른 SimHarness 사본, 일치 항목 없음, 폴더의 비Pin 파일(`testhost.exe`), 허용 Root 없는 공유 런타임 로드 |
| 폴더 유지 | 나열된 비Pin 출력 허용, 유지 중 쓰기 불가 | 비Pin 추가 파일, 바뀐 바이트 |
| 결속 세션 | — | TEST_ONLY 승인 + 결속: VERIFIER_BINARY 경로 불일치, Archive Root 불일치 |
| 자식 실행 | 실제 자식 `dotnet test`가 폴더 유지 중 통과 | 이름 `dotnet`(절대경로 아님), TRX를 쓰지 않는 실행 파일 |
| CLI | 모든 인자 1회 | `--anchor`, 중복, 누락, 잘못된 family, usage exit 2; 설치 설정 부재 → 시작 환경 위반 → 앵커 부재 순서로 거부, Registry에 도달하지 않음 |

### 6.2 실행 결과

(verify 실행 후 기록)

## 7. 남은 위험과 미해결

| 항목 | 상태 |
|---|---|
| 자식 프로세스(`vstest.console`·`testhost`·`git`)가 로드하는 Pin 밖 파일 | 부모가 검사하지 못함. ACL(G5)에 의존 |
| 비공식(TEST_ONLY·verify) 경로의 `git` | 이름 검색 그대로. 공식 세션만 Pin 사용 |
| 시험 폴더 결속을 포함한 공식 `RunTestProfile` 전체 경로 | 공식 승인 기록이 없어 끝까지 실행할 수 없음. 구성요소 단위 시험(폴더 유지 + 실제 자식 실행)으로 대신함 |
| `Assembly.Load(byte[])` 반례 | 시험 프로세스를 오염시켜 다른 시험이 순서에 따라 실패할 수 있어 자동 시험에 넣지 않음(규칙은 코드 2줄) |
| 설치 설정 형식·위치, 시작 정책 값, Pin 방식 | OWNER 규범 결정 전 공식 사용 금지 |
| `Execution.*` 원인 문자열 | 계약 §1.3 상위 코드 안에서의 추가. Codex 확인 필요 |

## 8. Codex Finding 추적 (F-01~F-20)

| ID | 이번 처리 | 남은 조건 |
|---|---|---|
| F-01 | T2·T3: 절대경로 자식, 프로세스 이미지 Pin | 운영 R-DOTNET ACL |
| F-02 | T2: 작업 디렉터리 = build root | — |
| F-03 | T5: 핸들 유지 | N-9 해석 확정, ACL |
| F-04 | T5·T6: 폴더 전체 Pin | 후보 v2 확정(N-11) |
| F-05 | T6: `execution_pins` 구조 | Pin 목록(로드 추적), N-5 |
| F-06 | T4: V-1·V-2 | V-3 ACL |
| F-07 | T3 | — |
| F-08 | T8 | 접두사 값 N-7 |
| F-09 | T2: 공식 세션 Pin `git` | 비공식 경로 이름 검색 |
| F-10 | 변경 없음(T-A 동작 유지) | N-6 |
| F-11 | 변경 없음 | — |
| F-12 | T1: build root = 설치 설정 값 | N-6 |
| F-13 | T1 | 운영 값 |
| F-14 | Apphost 이미지 거부(방식 A 강제) | 공식 명령 승인 후 Apphost Artifact 결정 |
| F-15·F-16 | 문서 준비(A-1) | 원격 생성·Push |
| F-17 | T7 | R-WORK ACL |
| F-18 | T3·T6·T8 구조 | Pin 목록, N-5 |
| F-19 | 규칙 v2에서 보수적 포함 | — |
| F-20 | G5 준비 문서 | 운영 실측 |

## 9. 변경 파일

(Commit 후 기록)
