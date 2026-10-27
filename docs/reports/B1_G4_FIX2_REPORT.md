# B1 G4 잔여 MINOR(G4-FIX-01·02) 최종 표적 수정 보고서

- 작성: Claude Code, 2026-10-28. 근거: OWNER 지시 "G4 잔여 MINOR 2건 최종 표적 수정"(2026-10-27)
- **성격:**
  - Claude Code의 수정·자체 시험 기록입니다. Codex READ-ONLY 재검수는 **미실시**입니다([요청서](../tasks/B1_G4_FIX2_CODEX_REVIEW_REQUEST.md)).
  - Codex PASS가 나와도 공식 운영 G4 PASS, G5 PASS, 보안 수용, 운영 승인이 아닙니다.
  - 승인 계약·닫힌 Schema·OWNER 원문·Codex 원문·B1 Tag·Golden·Hash·RNG·Replay·루트 `TASK.md`는 바꾸지 않았습니다. 기존 시험 기대값도 바꾸지 않았습니다.
  - 이미 RESOLVED인 G4-01·03·04의 코드(`ExecutionBinding`·`InstallConfig`·`Certification`·`OfficialEntry`)는 이번 diff에 없습니다. G4-04의 TRX 핸들·Pinned Tree 경로도 그대로입니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준·Commit·원문

| 항목 | 값 |
|---|---|
| Worktree·브랜치 | `E:\Madegame\Star_player_g4`, `phase3/g4-official-entry`(로컬 전용) |
| 검수된 기준 | 검증 소스 `6110d5fcfde0a1d89b8aab1a6e5f84213d598d84`, 문서 HEAD `75e272503e6b199292c6bac7b95083b8c7ac4d75` |
| **수정·검증 소스 Commit** | **`a980ea5a73ff2e3dfb032d2c3956d2d51c239711`** |
| 수정 diff | `git diff 75e2725 a980ea5`: 4개 파일, +239 / −18. 저장소 밖 사본 `E:\Madegame\g3x\g4fix2_75e2725_a980ea5.diff`(359행, `b894e194…`) |
| Codex 원문(로컬 보존, Git 밖) | `E:\Madegame\g3x\codex-originals-g4\20261027T134630Z_verdict-g4-fix-targeted-fix-required.txt`(10,610 B, `1a377b22…`). Codex 세션 기록 13845행에서 바이트 그대로 추출, `manifest.json`에 항목 추가(기존 두 원문은 그대로). NOT_INDEPENDENTLY_VERIFIED |

| 파일 | blob(`a980ea5`) | 변경 |
|---|---|---|
| `tools/StarPlayer.EvidenceVerifier/TestProfiles.cs` | `287a51077b06` | G4-FIX-01(`RunSummary`, `TrxRoot`), G4-FIX-02(한 시간 예산, `Stop`) |
| `tests/StarPlayer.EvidenceVerifier.Tests/G4ChildProgram.cs` | `3c84dfa78044` | 가짜 `dotnet test`가 VSTest 형태 TRX(ResultSummary·Counters·RunInfos)를 씀, 반례 시나리오, 파이프 보유 손자 프로세스(`g4-hold`), 대량 출력 |
| `tests/StarPlayer.EvidenceVerifier.Tests/G4OfficialEntryTests.cs` | `361748952b00` | 시험 16 → 20 |
| `docs/tasks/B1_G5_A1_OPERATIONAL_PREP_DRAFT.md` | `8e22e2a38945` | 문서 NOTE 정정(4절) |

## 2. Finding별 수정

### G4-FIX-01 (MINOR) TRX 실행 수준 오류

- **[관찰: 개발 PC]** VSTest 17.11.1이 이 PC에서 쓴 실제 TRX(통과 1건, 필터 불일치 1건)로 형태를 확인했습니다. 정상 실행은 `ResultSummary outcome="Completed"`와 `Counters`만 있고 `RunInfos`가 없습니다. 필터 불일치는 `RunInfo outcome="Warning"`을 씁니다.
- `Run`은 Case 목록(`Trx`)과 별도로 `RunSummary`로 실행 전체를 검사합니다.

| 검사 | 실패 시 |
|---|---|
| XML 파싱(DTD 금지), 뿌리 = VSTest 2010 `TestRun` | `TestProfile.trx` |
| `UnitTestResult`마다 `testName`·`outcome` 존재 | `TestProfile.trx` |
| 뿌리 아래 `ResultSummary` 정확히 1개, 그 아래 `Counters` 정확히 1개, `@outcome` 존재 | `TestProfile.trx` |
| 필요한 Counter가 0 이상의 정수 | `TestProfile.trx` |
| `RunInfo`가 하나라도 있음(Error·Warning 모두) | **`TestProfile.run_error`** |
| `error`·`timeout`·`aborted`·`passedButRunAborted`·`notRunnable`·`disconnected` Counter ≠ 0 | **`TestProfile.run_error`** |
| `ResultSummary/@outcome`이 `Completed`·`Failed`가 아님(`Aborted` 등) | **`TestProfile.run_error`** |
| `Counters` total·passed·failed ≠ `UnitTestResult` 수 | `TestProfile.trx` |
| 종료 코드 0 + Failed Case | `TestProfile.exit_inconsistent`(기존) |
| 종료 코드 1 + Failed Case 없음 | `TestProfile.exit_code`(기존) |
| 종료 코드 0 ↔ `Completed`, 1 ↔ `Failed` 불일치 | `TestProfile.exit_inconsistent`(기존 원인 재사용) |

- **순서:** 실행 수준 검사가 종료 코드·Case 검사보다 먼저입니다. 그래서 Failed Case와 실행 오류가 함께 있으면 FAIL 결과가 아니라 인프라 실패입니다.
- **정상 Case 실패는 불변:** 종료 코드 1 + Failed Case + 깨끗한 실행(`Failed` outcome, RunInfo 없음, 오류 Counter 0)은 그대로 결과이고, Evidence에서 FAIL Case로 남습니다.
- **오류 코드:** 상위 코드는 기존 `Evidence.test_profile_mismatch`입니다. 하위 원인 문자열 `TestProfile.run_error` 하나를 추가했습니다(G4-02 수정 때 `exit_inconsistent`·`timeout`·`cancelled`를 추가한 것과 같은 수준의 진단값). 새 Schema·인증 판정 상태는 없습니다.
- **보수적 선택(Codex 판단 요청):** VSTest가 Warning으로 분류하는 RunInfo도 거부합니다. 근거: 정상 실행은 RunInfo를 쓰지 않고, 검증기는 어떤 경고가 무해한지 판정할 규범이 없습니다. 대안: Warning만 허용하고 Error만 거부. 영향: 지금 선택은 무해한 경고가 생기면 인증이 인프라 실패가 되는 Fail-Closed 방향입니다.
- `notExecuted`·`inconclusive` Counter는 여기서 막지 않습니다. 그런 Case는 기존 `Evidence`가 `TestProfile.case_not_executed`로 거부합니다.

### G4-FIX-02 (MINOR) 출력 파이프 시간 제한

- 종료 대기와 stdout·stderr 끝 읽기를 하나의 `CancellationTokenSource`(호출자 취소에 연결, `CancelAfter(Timeout)`)로 묶었습니다.
  - `Task.WhenAll(WaitForExitAsync(t), StandardOutput.ReadToEndAsync(t), StandardError.ReadToEndAsync(t)).WaitAsync(t)`
  - `WaitAsync(t)`이 있으므로, 파이프 읽기가 취소에 응하지 않아도 제한 시간에 반환합니다.
  - `.Result` 무제한 대기는 없앴습니다.
- 끝나지 못하면(시간 초과·취소·그 밖의 예외) `Stop`이 `Kill(entireProcessTree: true)`를 시도하고 최대 10초 기다립니다. 그다음 두 Reader를 정리하고, 기존 원인 `TestProfile.timeout`·`TestProfile.cancelled`로 실패합니다.
- 출력이 끝나기 전에는 종료 코드·TRX를 보지 않습니다. 그래서 정상 TRX와 종료 코드 0이 있어도 파이프가 열려 있으면 성공이 아닙니다.
- **[관찰: 개발 PC]** 자식이 이미 끝난 뒤에도 `Kill(entireProcessTree: true)`가 그 자식이 시작한 손자 프로세스를 종료했습니다(시험 3·5의 고아 0).
- **한계:**
  - 프로세스 트리 밖으로 빠져나간 프로세스(다른 부모로 다시 시작, Job Breakaway 등)는 종료하지 못할 수 있습니다. 그때도 검증기는 제한 시간에 실패로 반환하지만, 그 프로세스와 묶인 읽기 작업은 파이프가 닫힐 때까지 남을 수 있습니다.
  - Job Object로 트리를 묶는 방식은 만들지 않았습니다. 공식 운영 경로의 자식 실행 체인 추적(미완료 항목)과 함께 정할 일입니다.

### 문서 NOTE

- [G5 준비 문서](../tasks/B1_G5_A1_OPERATIONAL_PREP_DRAFT.md) 4절 "G4 동작"의 `%windir%`·`allowed_module_roots` 자동 허용 문장을 현재 코드에 맞게 고쳤습니다. 이제 모든 로드 파일이 Pin이어야 하고, 동적 어셈블리는 `allowed_dynamic_assemblies` 이름만 허용합니다.
- 정정 표기를 남겼습니다. Codex 원문과 이전 보고서의 당시 기록은 고치지 않았습니다.

## 3. 시험

### 3.1 신규 시험 4개 (`G4OfficialEntryTests`, 16 → 20)

| 시험 | 지시 시험 번호 | 반례·확인 |
|---|---|---|
| `A_run_level_error_in_the_trx_is_an_infrastructure_failure_even_with_failed_cases_and_a_plain_case_failure_stays_a_fail_result` | FIX-01 1~8 | 정상 Failed(결과 + Evidence FAIL), Failed + 오류(`run_error`), Passed + 오류·종료 1(`run_error`), 종료 0 + 오류(`run_error`), Warning RunInfo·`error` Counter·`Aborted`(`run_error`), 종료 0 + `Failed` outcome(`exit_inconsistent`), ResultSummary 없음·2개, Counters 없음, 정수 아닌 Counter, 합계 불일치, outcome 없음, 2006 Namespace, XML 오류, Case outcome 없음(모두 `trx`) |
| `A_process_left_holding_stdout_or_stderr_after_the_child_exits_is_a_timeout_and_is_killed_with_the_tree` | FIX-02 3·4·5·8 | 자식은 정상 TRX와 종료 코드 0으로 끝나고, 손자가 두 파이프 / stdout만 / stderr만 보유 → `timeout`(제한 5초), 60초 안 반환, 손자 종료 확인 |
| `A_cancellation_while_a_pipe_is_held_is_a_cancellation_and_kills_the_holder` | FIX-02 6 | 파이프 보유 중 5초 뒤 취소 → `cancelled`, 손자 종료 확인 |
| `Large_interleaved_stdout_and_stderr_are_drained_without_blocking_the_child` | FIX-02 7 | stdout·stderr 각 약 8 MB 교차 출력 → 정상 결과 |

- 기존 시험이 덮는 지시 항목:
  - FIX-02 1(정상 종료) — `pass`
  - FIX-02 2(자식이 끝나지 않음) — `sleep` 시간 초과
  - FIX-02 6(취소) — `sleep` 취소
  - FIX-01 1·2·8(정상 Passed, 정상 Failed, 기존 종료 코드 조합) — `Only_exit_0_with_a_passing_trx_or_exit_1_with_failed_cases…`
  - 실제 `dotnet test` 자식 — `A_real_child_test_run_passes…`(실제 VSTest TRX가 새 검사를 통과)
- **격리:** 파이프 보유 반례는 가짜 자식(`dotnet exec … g4-fake-dotnet`)이 시작한 별도 손자(`g4-hold`)입니다.
  - 손자가 물려받는 것은 그 자식의 파이프뿐입니다. stdout만·stderr만 시나리오는 자식이 다른 표준 핸들의 상속 플래그를 끕니다.
  - 손자는 PID를 결과 디렉터리에 쓰고 최대 120초만 살아 있습니다. 시험은 반환 뒤 그 PID가 끝났는지 확인하고, 남아 있으면 직접 종료한 뒤 실패로 기록합니다.
- **고아 확인:** 모든 실행 뒤 `Win32_Process` 명령줄에 `g4-hold`·`g4-fake-dotnet`이 있는 프로세스는 0개였습니다.

### 3.2 수정 전 코드로 반례 실행 (FAIL 증거)

- 방법: Worktree에서 `TestProfiles.cs`만 `75e2725` 판으로 되돌리고 신규 시험 4개를 실행했습니다. 그 뒤 수정본으로 복구해 다시 빌드했습니다.
- 결과: **실패 3 / 통과 1** — 원시 로그 `E:\Madegame\g3x\g4fix2_prefix_FAIL.txt`(SHA는 5절)

| 시험 | 수정 전 동작 |
|---|---|
| 실행 수준 오류 | `Assert.Throws() Failure: No exception was thrown` — 실행 오류가 있는 TRX를 결과로 받아들임 |
| 파이프 보유 | 약 2분 대기(손자의 120초 수명) 뒤 정상 TRX를 결과로 받아들임 |
| 파이프 보유 중 취소 | 같음. 취소가 출력 대기에 닿지 않음 |
| 대량 출력 | 통과. 수정 전에도 두 파이프를 동시에 읽었으므로 회귀 방지 시험입니다 |

### 3.3 전체 회귀 (Clean Checkout, 실제 명령 출력)

| 실행 | 기준·위치 | 결과 | 증거 |
|---|---|---|---|
| G4 + TRX 파서 시험 | Worktree | 24/24 PASS | `dotnet test … --filter "FullyQualifiedName~G4OfficialEntryTests\|FullyQualifiedName~BootstrapAndProfileTests"` |
| `verify.ps1` | Clean clone `E:\Madegame\g3x\g4clean3`, `a980ea5`(clean, D2 0건) | **GATE: PASS**, **3,061 PASS / 0 FAIL / 0 SKIP** | [B1_G4FIX2_VERIFY_LOG_clean3.md](B1_G4FIX2_VERIFY_LOG_clean3.md) |
| Golden | 같은 clone | `GoldenTests` **4/4 PASS** | `E:\Madegame\g3x\g4fix2_golden_clean3.txt` |
| Dry Pilot 9 | 같은 clone(보관 Archive 130개 파일 복사, 이전 절차와 같은 스크립트를 이 clone에 맞춤: `E:\Madegame\g3x\g4fix2_drypilot.ps1`) | **`ALL_9_IDENTICAL=True`**: config_hash·run_result_hash·manifest 9행 모두 같음 | `E:\Madegame\g3x\g4fix2_drypilot_clean3.txt` |

- **verify 세부:**
  - U1 327, U2 741, U3 128(U3a 14·U3b 48), U4 1,237, U5 538, U6 90(12분 21초)
  - B1~B3·G1~G4·E1~E4·D1(237개)·D3(5개) PASS
- **시험 수:** 이전 3,057에서 늘어난 4개는 모두 신규 G4 시험입니다(U6 86 → 90). 기존 3,057개는 모두 PASS이고, 기대값과 Golden은 바꾸지 않았습니다.
- **게임 Hash·RNG·Replay 불변:**
  - Dry Pilot 9 Run 동일, Golden 4/4, U2·U4·U5 PASS
  - `src`·`game`·`tools/StarPlayer.SimHarness`는 이번 diff에 없습니다(`git diff --quiet 75e2725 a980ea5 -- src game tools/StarPlayer.SimHarness …` 성공).
- **Dry Pilot 스크립트 DLL:** SHA `44937744…`는 clone 경로에 따라 달라지는 기본 빌드 값이며 Pin이 아닙니다.
- **Godot Export:** E1~E4 PASS, `StarPlayer.pck` = `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799`(변경 없음). 5회 재현성 검사는 다시 하지 않았습니다(**미검증**).
- **이전 로그 보존:** [B1_G4FIX_VERIFY_LOG_clean_FAIL.md](B1_G4FIX_VERIFY_LOG_clean_FAIL.md)(`ca31226`, U6 1 FAIL), [B1_G4FIX_VERIFY_LOG_clean2.md](B1_G4FIX_VERIFY_LOG_clean2.md)(`6110d5f`, PASS)는 그대로 둡니다.

## 4. Git·SourceLink

- **Clean Checkout:**
  - `git clone --no-hardlinks E:\Madegame\Star_player_g4 E:\Madegame\g3x\g4clean3` → `checkout --detach a980ea5` → 작업 트리 0건
  - verify 뒤 변경은 verify가 덮어쓰는 `docs/reports/PHASE_2_VERIFY_LOG.md` 1건뿐입니다.
- **원격 URL:** clone의 `origin`을 `https://github.com/junk1901/Star_player.git`로 **설정만** 했습니다(조회·Push 없음). 이전과 같은 **실험 통제값**이며 A-1 공식 원격이 아닙니다. A-1 URL이 정해지면 SourceLink와 DLL·PDB SHA가 모두 바뀝니다.
- **CI 빌드:** verify·Golden·Dry Pilot 뒤 같은 clone에서 `dotnet build StarPlayer.sln -c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false -p:ContinuousIntegrationBuild=true` 실행, exit 0, 오류 0(`E:\Madegame\g3x\g4fix2_ci_build_clean3.txt`)

| 파일 | DLL SHA-256 | PDB SHA-256 | SourceLink |
|---|---|---|---|
| `tools/StarPlayer.EvidenceVerifier/…/StarPlayer.EvidenceVerifier.dll` | `2e9cd6abfa6406fb728a604ea1c329b6e83cb3637c302912cdec0605b7fbee64` | `301dfa78ceaa1fbf98c70f33e06b8065a530292f41f722a4bd1a3b5d5cde48d1` | `/_/*` → `https://raw.githubusercontent.com/junk1901/Star_player/a980ea5a73ff2e3dfb032d2c3956d2d51c239711/*` |
| `tools/StarPlayer.SimHarness/…/StarPlayer.SimHarness.dll` | `a82e6fc83a0a8b4d3cb42d3c074530df0b20df6905edd1cf8d667a0fbd6cc881` | `5f4e72060c952c1e74e1ce47a787c4087d8883352f573bf8145edcc8161badd9` | 같음 |
| 검증기 폴더의 `StarPlayer.SimHarness.dll` 사본 | `a82e6fc8…`(SIMHARNESS_BINARY와 같음) | `5f4e7206…` | 같음 |

- SourceLink Commit은 실제 빌드 Commit `a980ea5`입니다. 이 SHA들은 진단값이며 Build Provenance로 등록하지 않았습니다.
- 브랜치 Commit(로컬 전용): `75e2725` → `a980ea5`(수정) → 이 보고서 Commit
- **D1·D3·계약:** D1 237개 PASS, D3 5개 PASS(승인 계약·`TASK.md` 포함). B1 Tag `b1-code-accepted-2026-10-26` → `7bdde7dd091f9a66957bc740c77d470c7732b3ad` 불변

## 5. 증거 SHA-256

| 파일 | SHA-256 |
|---|---|
| [B1_G4FIX2_VERIFY_LOG_clean3.md](B1_G4FIX2_VERIFY_LOG_clean3.md) | `c45db6851b2f74bdd864a642891ed3fe2b4681e30d911e41e4fd59293f5db40f` |
| `g4clean3\build\verify\U6.log` | `15d014cf709c9886ebfa6fe2a2f4749c436df242ef09cabddf853d81358af84d` |
| `E:\Madegame\g3x\g4fix2_verify_clean3_stdout.txt` | `e706cacb13ce2f476cc88a1846135c25e08a02f0e5b1bc370a382eec19e7d0ba` |
| `E:\Madegame\g3x\g4fix2_golden_clean3.txt` | `91cab776433126638b9c13da98475182bab8855ffad8e58a055c6b4c63526632` |
| `E:\Madegame\g3x\g4fix2_drypilot_clean3.txt` | `c2984e95d4d7011a2ecd306236dc6cc54824c4b358211b506c7ab43ca0979b01` |
| `E:\Madegame\g3x\g4fix2_ci_build_clean3.txt` | `655e4309c4c44eaa0c258065a90c4daa522f79a8c4a2ce07fa98100c43e445d1` |
| `E:\Madegame\g3x\g4fix2_prefix_FAIL.txt` | `e7756f7efda134ccaad27e6fef5bc981ac3f4a9574cf55f866fa6c6a01f3f349` |
| `E:\Madegame\g3x\codex-originals-g4\manifest.json`(항목 추가 후) | `6192c88a0397a3ac4e0b826d374277be557336d1b3995a3c52723391c84613b6` |

## 6. 미완료로 유지하는 사항 (이번에 승인·실행하지 않음)

- N-5·N-6·N-7·N-9·N-10·N-11
- OS·공유 런타임 및 동적 어셈블리 승인 Pin
- G5 운영 계정·ACL
- 공식 인증 성공 경로
- 자식 실행 체인(`vstest`·`testhost`·`git`) 파일 로드 추적과 트리 밖 프로세스 종료(2절 한계)
- A-1 신규 비공개 원격
- B-4 최종 Artifact 및 Build Provenance
- PCK 5회 재현성 재검사(**미검증**)
- Codex READ-ONLY 재검수(**미실시**)
