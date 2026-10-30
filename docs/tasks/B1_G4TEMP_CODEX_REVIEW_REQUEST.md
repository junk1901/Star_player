# Codex READ-ONLY 표적 검수 요청 — G4-TEMP (Test Profile 자식 TEMP 보호, C단계 A안)

- 요청자: Claude Code, 2026-10-31. 근거: OWNER "G4 Test Profile TEMP 보호 수정 (C단계)" 지시, B단계 추적 보고(`E:\Madegame\g3x\phase3ops\g4_temp_trace\run_20261030T140030Z\REPORT.md`, POTENTIAL_DECISION_DEPENDENCY)
- **READ-ONLY:** 코드·시험·계약·문서 수정, Commit, Push, Tag, 운영 적용을 하지 않습니다. 판정만 요청합니다.
- 대상: 로컬 Branch `phase3/g4-temp-protect`, 코드 Commit **`262f4402880cbaa933eb0a0488aa20a55cd9497f`**(Tree `a5d60b8198013f0f435e8bd650d107c9f4f882bc`, 부모 = 게시 HEAD `292fa18d6090b0efa2405142d9e72e1853b1d6e1`). 원격에 없습니다(Push 미승인).
- 비교 기준: 공식 제품 소스 `8ca9705539bc941919f5290de149c4678f5147dd`(`292fa18`과 제품 코드 동일)
- **판정 결과 (2026-10-31, OWNER 전달 요약, 7절):** REVIEW_READY · G4-TEMP = **RESOLVED_IN_CODE** · G4-ENV-01 = **OPEN**(설정 누락 시 MAJOR) · G5-TEMP-01 = **RESOLVED_IN_CODE** · 신규 코드 MAJOR/MINOR 0/0 · 공식 운영 G1~G9 미실행. 아래 요청 본문은 요청 당시 그대로 둔다.
- 요청 당시 상태: G4-TEMP = **OPEN (Codex 판정 전, RESOLVED_IN_CODE 아님)** · Official Execution = NOT_READY · Final Execution Freeze = PENDING · Phase 3 = INCOMPLETE · Artifact Set = CANDIDATE_INCOMPLETE

## 1. 변경 요약

`git diff 292fa18 262f440` (공백 무시 +198/−2):

| 파일 | 내용 |
|---|---|
| `tools/StarPlayer.EvidenceVerifier/TestProfiles.cs` (+8/−1) | `Run`: `BeforeChild` 다음에 `results.CreateSubdirectory("temp")`를 `Pinned(…)`로 고정한다(실패 = `TestProfile.results_directory`). `RequireProtectedWorkTree`이면 `Writers(temp)` 검사(실패 = `TestProfile.results_acl`). 환경 정리(E4·OWNER 접두사) **뒤에** `start.Environment["TEMP"]`·`["TMP"]` = `temp.FullPath`. 문서 주석 2곳 |
| `tests/…/G4ChildProgram.cs` (+38/−1) | 가짜 자식 시나리오 `temp-env`(받은 TEMP·TMP·`GetTempPath`, 심은 폴더 보임 여부, 임시 파일 왕복을 결과 폴더에 기록), `trx-in-temp`. 격리 프로세스 시나리오 `g4-temp-isolation`. `Child(environment, …)` 오버로드 |
| `tests/…/G4TestProfileTempTests.cs` (신규 152줄) | 시험 6건(아래 4절) |

바꾸지 않은 것: `src`, `game`, `content`, `docs/contracts`, `TASK.md`, `.gitattributes`, `global.json`, `Directory.*.props`, `tools/b1-ops`, 다른 검증기·SimHarness 파일, 기존 시험과 기대값, Golden, Hash, RNG, Replay.

## 2. 확인 요청 항목

1. **보호 경계:** `temp`가 이미 고정된 `results` 핸들 기준으로 `FILE_CREATE`, 링크 거부로 만들어지고, 실행 끝까지 삭제 공유 없이 유지되는가. `using var` 순서(temp → results → root 해제)가 맞는가. 이름 선점, Junction, 생성 권한 없음, 상속된 외부 쓰기 주체가 모두 자식 시작 **전에** 거부되는가.
2. **환경 덮어쓰기:** `ProcessStartInfo.Environment`(Windows 대소문자 무시)에서 정리 후 설정하므로 어떤 접두사 제거로도 빠지지 않는가. 부모의 `Temp`·`tmp` 같은 다른 대소문자 이름이 남지 않는가.
3. **`GetTempPath` 의존 범위:** 자식 체인(dotnet test, vstest.console, testhost, xUnit)이 TEMP 결정에 TMP/TEMP만 쓰는가. Windows `GetTempPath2`는 **SYSTEM 계정 프로세스에서 TMP/TEMP를 무시하고 `C:\Windows\SystemTemp`를 쓴다.** 검증기를 SYSTEM으로 실행하는 운영 구성이 있다면 이 수정이 효과가 없는지(아래 6절 R-4).
4. **판정 보존:** TRX 단일성(`AllDirectories`라서 temp 안의 `.trx`도 셈), 종료 코드 0/1 규칙, RunSummary, Timeout, 프로세스 트리 종료, 취소가 그대로인가. 시험 Seam `BeforeChild` 호출 시점이 temp 생성 **앞**으로 정해진 것이 기존 시험 의미를 바꾸지 않는가.
5. **Registry 경계:** `TestProfiles.Run`은 Registry를 참조하지 않는다. `Certification.RunTestProfile`이 던지면 `TestProfileRun`(생성자 internal, `RunTestProfile`만 생성)이 만들어지지 않아 `IssuePhase`에 도달하지 못한다. `OfficialEntry.cs` 94~97행 순서상 **Family 영수증(94행)은 Test Profile 실행(95행) 전에 발행**된다. 이것은 이번 변경 전부터 있던 순서이며, Family 판정은 Test Profile 결과와 무관하다. 이 순서가 G4-TEMP 관점에서 문제가 없는지 확인을 요청한다(이번에 바꾸지 않음).
6. **SDK 분석의 정확성:** `E:\Madegame\g3x\phase3ops\g4temp\SDK_POSTPROCESS_ANALYSIS.md`의 [IL] 항목과 IL 목록(`ildump\*.txt`). 특히 `exit = vstest | postprocess`(비트 OR), 후처리 입력(`runsettings.xml`, `executionComplete.json`), `InvokedDataCollector.FilePath` 기반 확장 로드.

## 3. 직접 재현 RED/GREEN (실제 SDK 8.0.425, 개발 PC)

부모 TEMP = Everyone이 쓸 수 있는 새 폴더. 감시자가 수집 모드 vstest.console의 PEB 명령줄에서 상관 ID를 읽어 부모 TEMP에 `<ID>` 폴더를 만든다(실행 코드 없음).

| 실행 | 빌드 | 공격 | 후처리 시작 | 판정 |
|---|---|---|---|---|
| red-none | 8ca9705 | 없음 | 아니오 | RESULT 4×Passed |
| red-empty | 8ca9705 | 빈 폴더 | **예** | RESULT. SDK가 폴더 삭제 |
| red-garbage | 8ca9705 | 깨진 `executionComplete.json` | **예** | **REFUSED `TestProfile.exit_code`** (PASS가 인프라 실패로 바뀜) |
| green-none / -empty / -garbage | 262f440 | 같음 | **아니오** | RESULT 4×Passed. 심은 폴더 그대로(조회 안 됨) |

GREEN에서 dotnet test, vstest.console, testhost의 TEMP=TMP=`<results>\temp`(PEB 실측). 실행 뒤 temp는 비어 있었다. B단계 추적은 공격 성공을 관찰하지 않았으므로 RED로 소급하지 않았다. 위 RED는 이번에 새로 실행한 결과다.

## 4. xUnit 시험 RED/GREEN

| 시험 | RED(8ca9705 제품 + 새 시험) | GREEN(262f440) |
|---|---|---|
| `The_child_temp_and_tmp_are_a_new_protected_folder_inside_each_runs_results_directory_and_never_the_parents_writable_temp` | 실패: 자식 TEMP = 부모 TEMP | 통과 |
| `Preparing_the_child_temp_folder_fails_closed_before_any_child_starts(existing-folder)` | 실패: 예외 없음(자식 실행) | 통과 `results_directory`, 자식 0 |
| `…(junction)` | 실패: 예외 없음 | 통과 `results_directory`, 대상 폴더 무변경 |
| `…(no-add-subdirectory)` | 실패: 예외 없음 | 통과 `results_directory` |
| `…(inherited-writer)` | 실패: **자식이 이미 실행됨**(사후 TRX ACL로만 거부) | 통과 `results_acl`, 자식 시작 전 |
| `A_trx_the_child_drops_into_its_temp_folder_is_a_second_trx_and_refused` | 실패: 예외 없음(TRX가 부모 TEMP로 감) | 통과 `TestProfile.trx` |

## 5. 회귀

검수 보고서 `docs/reports/B1_G4TEMP_REPORT.md` 5절을 참조한다(worktree verify, 로컬 Clean Clone verify, Golden, Dry Pilot, D3, PCK, G1 도구).

## 6. 이번에 고치지 않은 잔여 위험(독립 Finding, OWNER 지시 4항 범위 밖)

- **R-1 `VSTEST_CONSOLE_PATH`(신규 Finding G4-ENV-01):** [IL] `VSTestForwardingApp::GetVSTestExePath`는 이 변수가 비어 있지 않으면 그 경로의 vstest.console.dll을 실행한다. E4 정리 대상(`DOTNET_`·`COMPlus_`)이 아니다. 검증기 시작 환경에 이 변수가 있으면 시험 실행기 자체가 바뀐다. 현재 막는 수단은 OWNER 설정 `startup_forbidden_prefixes`(시작 환경 거부)와 같은 접두사의 자식 환경 제거뿐이다. 권고: OWNER 설정에 `VSTEST_`를 넣을지 결정하고, 코드 기본값으로 둘지는 Codex가 판단한다.
- R-2 그 밖의 `VSTEST_*`(디버그 대기, 시간 제한, 진단 로그 경로, 후처리 끄기 등)와 `VSLANG`(TRX 지역화 바이트): 목록은 `vstest_env_names.txt`(바이너리 112행).
- R-3 CLI 원격분석: `DOTNET_CLI_TELEMETRY_OPTOUT`은 E4에서 제거되므로 자식은 `%USERPROFILE%\.dotnet\TelemetryStorageService`에 쓴다(B단계 관찰). 판정 입력은 아니다.
- R-4 SYSTEM 계정 실행 시 `GetTempPath2`가 TEMP/TMP를 무시한다(2절 3항). 운영 계정 설계(G5)와 함께 확인해야 한다.
- R-5 결과 폴더 아래 `temp`는 실행 후 지우지 않는다(결과 폴더와 같은 수명). 크기 증가는 측정하지 않았다(GREEN 실행 뒤 비어 있음).

## 7. 판정 결과 기록 (2026-10-31, 요청 뒤 추가)

Codex 판정 원문은 이 저장소에 보관하지 않았다. OWNER가 전달한 요약:

- G4-TEMP 보완 `262f440`: **REVIEW_READY**, **RESOLVED_IN_CODE**. 신규 코드 MAJOR/MINOR **0/0**.
- G4-ENV-01(R-1): **OPEN**. 공식 보호 설치 설정에 `VSTEST_`가 빠지면 **MAJOR**. OWNER 정책 = 설정에 `VSTEST_` 필수. Approval B 조건은 보고서 12절.
- G5-TEMP-01: **RESOLVED_IN_CODE**.
- NOTE-01: 보고서 7절의 "Registry 0건"·"temp 안의 TRX도 자식 시작 0회" 표현이 넓었다. 정정: 준비 실패 4사례는 자식 시작 0회, temp 안의 TRX 사례는 자식 실행 뒤 거부, Test Profile 실패 시 Phase 영수증 미발급, 그 전에 등록된 Family 영수증은 존재할 수 있음(2절 5항의 순서). 보고서 7절에 정정을 기록했다.
- 공식 운영 G1~G9: **미실행**. 이 판정은 코드 단계 판정이며 공식 Gate PASS가 아니다.
