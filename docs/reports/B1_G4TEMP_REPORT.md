# B1 G4-TEMP 구현 보고서 — Test Profile 자식 프로세스의 보호된 TEMP (C단계 A안)

- 작성: Claude Code, 2026-10-31. 근거: OWNER "G4 Test Profile TEMP 보호 수정 (C단계)" 지시. B단계 추적 결과 POTENTIAL_DECISION_DEPENDENCY(`E:\Madegame\g3x\phase3ops\g4_temp_trace\run_20261030T140030Z\REPORT.md`)를 OWNER가 수용함
- **성격:** 승인 범위인 최소 코드 수정, 보안 시험, 전체 회귀, 로컬 Commit만 했습니다. Push, Tag, 이력 재작성, 계약·Hash·RNG·Replay·Golden 변경, 운영 ACL·Trust Anchor·Registry·Pin, 공식 Gate·인증, Provenance 등록은 **없습니다.** 공식 Approval C가 아닙니다.
- 상태 (2026-10-31 Codex 판정 반영, 11절): G4-TEMP = **RESOLVED_IN_CODE** · G4-ENV-01 = **OPEN**(보호 설치 설정에서 누락되면 MAJOR) · G5-TEMP-01 = **RESOLVED_IN_CODE** · 신규 코드 MAJOR/MINOR = **0/0** · 공식 운영 G1~G9 = **미실행** · Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · Artifact Set = **CANDIDATE_INCOMPLETE**
- 이전 상태 기록(Codex 판정 전 작성 시점): G4-TEMP = OPEN (Codex 판정 전, RESOLVED_IN_CODE 아님)
- 증거: 저장소 밖 `E:\Madegame\g3x\phase3ops\g4temp\`(목록 `SHA256SUMS_g4temp.txt`), PCK `E:\Madegame\g3x\pck-out\G4TEMPA_P1\`

## 1. 작업 위치와 Commit

| 항목 | 값 |
|---|---|
| Worktree / Branch | `E:\Madegame\Star_player_g4temp` / `phase3/g4-temp-protect` (신규, 기존 `Star_player`·`Star_player_c3`·`Star_player_g4`는 손대지 않음) |
| 출발 Commit | `292fa18d6090b0efa2405142d9e72e1853b1d6e1`(게시 GitHub HEAD. 제품 코드는 `8ca9705`와 같고 이후는 문서와 G1 도구뿐) |
| **코드·시험 Commit** | **`262f4402880cbaa933eb0a0488aa20a55cd9497f`**, Tree **`a5d60b8198013f0f435e8bd650d107c9f4f882bc`** |
| Commit 직후 상태 | 작업 트리 0건(clean) |
| 변경 파일 | `tools/StarPlayer.EvidenceVerifier/TestProfiles.cs`(공백 무시 +8/−1), `tests/StarPlayer.EvidenceVerifier.Tests/G4ChildProgram.cs`(+38/−1), `tests/StarPlayer.EvidenceVerifier.Tests/G4TestProfileTempTests.cs`(신규 152줄) |
| 바꾸지 않은 것 | `src`, `game`, `content`, `docs/contracts`, `TASK.md`, `.gitattributes`, `global.json`, `Directory.*.props`, `tools/b1-ops`, 다른 제품 파일, 기존 시험 기대값 |
| 문서 Commit | 이 보고서, `docs/tasks/B1_G4TEMP_CODEX_REVIEW_REQUEST.md`, `CURRENT_STATE.md`, `Star/00_Dashboard.md`만 담은 별도 문서 전용 Commit(OWNER 2026-10-31 승인, 코드 Commit `262f440` 위). C단계 작성 시점에는 승인 범위 밖이라 작업 트리에만 두었다. **공식 빌드의 제품 소스는 문서 Commit이 아니라 `262f440`이다**(문서 Commit은 제품·시험 파일을 바꾸지 않음) |

## 2. 보안 경계 — 구현 전후

**이전(`8ca9705`):** 자식 `dotnet test`는 검증기의 TEMP/TMP를 그대로 받았다(E4는 `DOTNET_`·`COMPlus_`·OWNER 접두사만 제거). SDK 8.0.425의 `dotnet test`는 vstest.console이 끝난 뒤, 검증기가 종료 코드와 TRX를 읽기 **전에** `%TEMP%\<자신의 PID>_<GUID>`가 있는지 보고, 있으면 그 폴더를 입력으로 VSTest 후처리를 실행한다(6절). 그 ID는 vstest.console 명령줄에 노출된다. 이 개발 PC의 `%TEMP%`는 `CodexSandboxUsers`와 미해석 SID에 Modify를 준다.

**이후(`262f440`):**

```text
R-WORK (root, 핸들 고정, 공식: Writers 검사)
 └ StarPlayer.Domain.Tests-<GUID>/   (results, CreateSubdirectory = FILE_CREATE·링크 거부·삭제 공유 없음, 공식: Writers)
     ├ temp/                          ← 신규: results 핸들 기준 CreateSubdirectory("temp"), 같은 고정·검사, 실행 끝까지 유지
     └ StarPlayer.Domain.Tests.trx
자식 환경: E4·OWNER 접두사 제거 → TEMP = TMP = <results>\temp (마지막에 덮어씀)
준비 실패(이름 선점·Junction·생성 권한 없음·비TCB 쓰기 주체) → 자식 시작 0회
```

- `TestProfiles.cs` 변경 위치: `Run`의 `BeforeChild` 호출 다음 3줄(`Pinned(results.CreateSubdirectory("temp"), …)`, `RequireProtectedWorkTree`일 때 `Writers(temp…)`), 환경 정리 다음 2줄(`start.Environment["TEMP"]`·`["TMP"]`), 문서 주석 2곳.
- 재사용: `PinnedDirectory.CreateSubdirectory`(기존 G4-04 결과 폴더와 같은 함수), `Pinned`(실패 원인 `TestProfile.results_directory`), `Writers`/`WorkTree.WriterProblem`(실패 원인 `TestProfile.results_acl`). 새 검사 코드는 없다.
- 핸들 수명·공유: `temp` 핸들은 Read|Write 공유, 삭제 공유 없음. 자식이 그 **안에서** 파일을 만들고 지우는 것은 막지 않는다(시험의 임시 파일 왕복, GREEN 재현에서 확인). `temp` 자체는 이동·삭제·링크 교체가 불가능하다. 해제 순서는 temp → results → root.
- 유지: TRX 단일성(`AllDirectories`, temp 안의 `.trx`도 둘째 TRX로 거부), 종료 코드 0/1 규칙, RunSummary, Timeout·트리 종료·취소, Registry 경계(7절).

## 3. 추가 시험과 결과

| 시험 (`G4TestProfileTempTests`) | 확인 내용 | RED (8ca9705 제품 + 새 시험) | GREEN (262f440) |
|---|---|---|---|
| `The_child_temp_and_tmp_are_a_new_protected_folder_inside_each_runs_results_directory_and_never_the_parents_writable_temp` | 격리 프로세스의 부모 TEMP = Everyone 쓰기 가능 폴더(사전 조건: `WriterProblem` ≠ null) + 후처리 모양 폴더를 심음. 2회 실행. 자식이 받은 TEMP·TMP·`GetTempPath` = `<results>\temp`, 심은 폴더가 보이지 않음, 임시 파일 왕복 정상, temp 비어 있음, temp 쓰기 주체 정상, 두 실행의 temp가 다름, PASS 결과 유지 | 실패(자식 TEMP = 부모 TEMP) | 통과 |
| `Preparing_the_child_temp_folder_fails_closed_before_any_child_starts(existing-folder)` | `temp` 이름 선점 | 실패(예외 없음, 자식 실행) | 통과 `results_directory`, 자식 0 |
| `…(junction)` | `temp`를 외부 폴더 Junction으로 | 실패(예외 없음) | 통과 `results_directory`, 대상 폴더 무변경 |
| `…(no-add-subdirectory)` | results에 하위 폴더 생성 거부 ACE(권한 오류) | 실패(예외 없음) | 통과 `results_directory` |
| `…(inherited-writer)` | 공식 모드. results 검사 통과 뒤 Everyone 쓰기 상속 → temp가 상속 | 실패: **자식이 이미 실행됨**(TRX ACL로 사후 거부) | 통과 `results_acl`, 자식 시작 전 |
| `A_trx_the_child_drops_into_its_temp_folder_is_a_second_trx_and_refused` | 자식이 자기 TEMP에 TRX를 하나 더 씀 | 실패(예외 없음. TRX가 부모 TEMP로 감) | 통과 `TestProfile.trx` |

- 자식 시작 0회 증거: 가짜 자식은 시작하면 가장 먼저 `temp-env.txt`를 쓴다. 준비 실패 4사례에서 이 파일과 TRX가 없음을 확인했다(`BeforeChild` 호출은 확인함). temp 안의 TRX 사례는 자식이 실행된 뒤 거부된다(7절 정정).
- RED 실행이 남긴 `%TEMP%\g4-stray-39824.trx`(499 B, SHA-256 `aefb9a79…0997`) 1개를 확인 후 삭제했다.

| 실행 | 결과 | 로그 |
|---|---|---|
| RED (`292fa18` 제품 + 새 시험, 제품 diff 0 확인) | 6건 중 실패 6 | `logs\red_g4temp.console.log`·`.trx` |
| GREEN (새 시험 + 기존 G4 20건) | **26/26 통과**(실제 `dotnet test` 자식 시험 포함) | `logs\green_g4temp.console.log`·`.trx` |

첫 GREEN 시도는 솔루션 전체를 빌드하지 않아 `TestOnlyWorld` 고정 장치가 `StarPlayer.Domain.Tests.dll`을 찾지 못해 실패했다(환경 원인). 전체 빌드 뒤 다시 실행한 결과가 위 26/26이다.

## 4. 직접 재현 RED/GREEN (실제 SDK, 저장소 밖 진단 드라이버)

부모 TEMP = Everyone 쓰기 폴더. 감시자가 수집 모드 vstest.console의 PEB 명령줄에서 상관 ID를 읽어 부모 TEMP에 `<ID>` 폴더를 만든다(실행 코드 없음). 드라이버는 해당 빌드의 `TestProfiles.Run`을 수정 없이 부른다(`exp\driver-src`).

| 실행 | 빌드 | 심은 것 | 후처리 실행 | 판정 |
|---|---|---|---|---|
| red-none / red-empty / red-garbage | 8ca9705 | 없음 / 빈 폴더 / 깨진 `executionComplete.json` | 아니오 / **예** / **예** | RESULT / RESULT / **REFUSED `TestProfile.exit_code`** |
| green-none / -empty / -garbage | 262f440 | 같음 | 아니오 / **아니오** / **아니오** | RESULT ×3(각 4×Passed). 심은 폴더 그대로 |

- RED-garbage는 **부모 TEMP에 심은 폴더가 판정 입력(종료 코드)을 바꾼 것**을 직접 재현했다(PASS → 인프라 실패, fail-closed 방향). B단계 추적은 소급하지 않았다.
- GREEN에서 `dotnet test`, vstest.console, testhost의 TEMP=TMP=`<results>\temp`(PEB 실측). 실행 뒤 각 temp는 비어 있었다.

## 5. 전체 회귀

| 실행 | 위치 | 결과 |
|---|---|---|
| `verify.ps1` | Worktree(Commit 직전, 같은 바이트) | **GATE: PASS**(1,933초). U1 327, U2 741, U3 128(U3a 14, U3b 48), U4 1,237, U5 540, U6 **104** = **3,077 PASS / 0 FAIL / 0 SKIP**(기존 3,071 + 신규 6). B1 경고 0, G1~G4, E1~E4, D1(276), D3(5) PASS, Z1 잔여 프로세스 없음 |
| `verify.ps1` | **로컬 Clean Clone** `E:\Madegame\g3x\g4tempA`(원본 = 로컬 Worktree, `checkout --detach 262f440`, `core.autocrlf=true`, HEAD·Tree 일치, 작업 트리 0) | **GATE: PASS**(1,567초), 3,077 PASS, B1·U1~U6·D1·D3 PASS, 끝난 뒤 작업 트리 0 |
| Golden | 같은 Clone | **4/4 PASS** |
| Dry Pilot 9 | 같은 Clone(보관 Archive 130개 파일) | **9/9 동일**(config_hash·run_result_hash·환경 외 Manifest) |
| D3 보호 파일 5개 | 같은 Clone | **5/5 SHA 일치** |
| CI Release Build | 같은 Clone | 종료 0, 경고 0, 오류 0. 출력 793개 |
| Godot PCK | 같은 Clone, 기존 G3 실행기 `run_pck.ps1`(clean) | `StarPlayer.pck` 13,452 B, SHA-256 `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799` = **8ca9705 기준과 같음**. Export 파일 198개 **모두 같음**. verify 내 export PCK도 같은 값 |
| G1-a 감사 도구 v2 | 같은 Clone. 도구는 Commit `98724888`에서 꺼냄(SHA `4d086a5c…f96b` 일치) | PS 5.1·PS 7 모두 **RESULT=PASS, production_files=164, names=9, mismatches=0** |

- **G1 도구 영향:** 없음. `TestProfiles.cs`는 G1 도구의 고정 파일·이름·정의·줄 대상이 아니다. 도구를 수정하지 않았다.
- **바이너리 SHA:** 새 Commit의 DLL/PDB SHA는 새 값으로 취급한다. 같은 출처 유형의 두 로컬 Clone(8ca9705와 262f440)을 비교하면, 코드가 바뀐 `StarPlayer.EvidenceVerifier.dll`만 다르다(`0f13d279…` → `5e6efaa5…`). `StarPlayer.SimHarness.dll`·`StarPlayer.Domain.Tests.dll`·`StarPlayer.Domain.dll`은 바이트가 같다. 공식 원격 Clone인 `g23c3` 기준과 비교하면 출력 793개 중 116개가 다르다. 원인은 Clone 출처다. 이번 Clone은 로컬 경로가 origin이라 **SourceLink가 비어 있고**(PDB 55개 모두 SourceLink 없음, 기대값 불일치), 그 차이가 PDB와 DLL 디버그 디렉터리에 반영된다. **공식 SourceLink와 공식 바이너리 SHA는 이번에 산출하지 않았다.** 새 Commit은 원격에 없으며, 공식 GitHub Clone을 수행한 것이 아니다.

## 6. SDK 후처리 경로 분석

상세: `E:\Madegame\g3x\phase3ops\g4temp\SDK_POSTPROCESS_ANALYSIS.md`(IL 목록 근거). 요점:

- [IL] `dotnet test <dll>`는 항상 VSTest 경로를 탄다. 상관 ID = `{dotnet test PID}_{새 GUID}`. 수집 모드 인수 `--artifactsProcessingMode-collect --testSessionCorrelationId:<ID>`가 붙는다.
- [IL] vstest 종료 뒤 `Path.Combine(Path.GetTempPath(), ID)`가 있으면 `--artifactsProcessingMode-postprocess`로 두 번째 vstest.console을 실행하고, `finally`에서 그 폴더를 지운다. **종료 코드 = vstest | 후처리(비트 OR)**.
- [IL] 후처리 입력은 `runsettings.xml`과 `executionComplete.json`이다. 후자의 `InvokedDataCollectors[].FilePath`로 확장을 로드해 첨부 처리기를 만든다 → **[가능성] 심은 파일이 지정한 어셈블리를 검증기 계정으로 TRX 읽기 전에 로드.** 실행하지 않았다.
- [IL] 후처리에는 TRX logger 재생성이 보이지 않는다. 검증기의 OR·TRX 일치 검사 때문에 TRX만 바꾼 FAIL→PASS는 거부된다. 코드 로드는 그 밖의 문제다.
- [재현] 4절.
- MTP: 모든 시험 프로젝트가 xUnit v2 + VSTest 어댑터이며, `.dll` 인수 경로는 MTP를 쓰지 않는다.

## 7. Fail-Closed와 Registry 경계

> **정정 (2026-10-31, Codex NOTE-01):** 처음 작성한 이 절의 제목("Registry 0건")과 첫 항목("준비 실패 4종과 temp 안의 TRX는 … 자식 시작 0회를 확인")은 범위를 넓게 썼다. 아래가 정확한 표현이다. 원래 문장은 이 정정 아래 이력으로 남긴다.
>
> - **준비 실패 4사례**(이름 선점·Junction·생성 권한 없음·상속된 외부 쓰기 주체): 자식 시작 **0회**(xUnit, 3절).
> - **temp 안의 TRX 사례:** 자식은 **실행된 뒤** 결과 수집 단계에서 `TestProfile.trx`로 거부된다. 자식 시작 0회 사례가 아니다.
> - **Test Profile이 실패하면 Phase 영수증은 발급되지 않는다**(아래 정적 근거).
> - **그보다 먼저 등록된 Family 영수증은 존재할 수 있다**(`OfficialEntry.cs` 94행에서 Family 영수증이 95행 Test Profile 실행 전에 발행됨). 따라서 "모든 Registry 등록이 0건"이라고 말할 수 없다.
>
> 원래 문장(이력): "준비 실패 4종과 temp 안의 TRX는 xUnit으로 거부와 자식 시작 0회를 확인했다(3절)."

- Registry: `TestProfiles.Run`은 Registry를 참조하지 않는다(정적). `Certification.RunTestProfile`(`Certification.cs` 141~178행)이 던지면 `TestProfileRun`(생성자 internal, 이 함수만 생성)이 없으므로 `VerifyRegressionProof`·`IssuePhase`·`registry.Register`에 도달하지 못한다. 이번 변경은 이 경로의 **앞**에서 더 일찍 거부할 뿐, 새 발행 경로를 만들지 않는다. 이 항목은 **정적 근거**이며, Registry 건수를 세는 새 시험은 추가하지 않았다(Test Profile 실패를 Certification 수준에서 유도하는 시험은 실제 4개 시험 어셈블리 실행이 필요해 범위를 넘음, **미검증**).
- 관찰(이번 변경 전부터): `OfficialEntry.cs` 94행에서 Family 영수증이 95행 Test Profile 실행 **전에** 발행된다. Family 판정은 Test Profile과 무관하다. Codex 확인 항목으로 넘겼다.

## 8. 잔여 위험 (이번에 고치지 않음, OWNER 지시 4항)

| ID | 내용 | 비고 |
|---|---|---|
| **G4-ENV-01 (신규 독립 Finding, OPEN)** | [IL] `VSTEST_CONSOLE_PATH`가 있으면 `dotnet test`가 그 vstest.console.dll을 실행한다. E4 정리 대상이 아니다 | OWNER 설정 `startup_forbidden_prefixes`에 `VSTEST_`를 넣으면 시작 환경 거부와 자식 제거가 함께 적용된다(기존 기능). 2026-10-31: OWNER 정책 = 보호 설치 설정에 `VSTEST_` 필수, 누락 시 MAJOR(Codex). 운영 조건은 12절 |
| R-2 | 그 밖의 `VSTEST_*`(디버그 대기, 시간 제한, 진단 로그 경로, `VSTEST_DISABLE_ARTIFACTS_POSTPROCESSING` 등)와 `VSLANG`(TRX 지역화 바이트) | 목록 `vstest_env_names.txt`(112행). 의미 일부는 **미검증** |
| R-3 | CLI 원격분석 `%USERPROFILE%\.dotnet\TelemetryStorageService` 쓰기(`DOTNET_CLI_TELEMETRY_OPTOUT`가 E4로 제거됨) | 판정 입력 아님 |
| R-4 | 검증기를 SYSTEM으로 실행하면 Windows `GetTempPath2`가 TEMP/TMP를 무시한다(`C:\Windows\SystemTemp`) | 운영 계정 설계(G5)와 함께 확인 |
| R-5 | `temp`를 실행 후 지우지 않는다(결과 폴더와 같은 수명) | GREEN 실행 뒤 비어 있음 |

## 9. Codex READ-ONLY 표적 검수 요청서

[B1_G4TEMP_CODEX_REVIEW_REQUEST.md](../tasks/B1_G4TEMP_CODEX_REVIEW_REQUEST.md)

## 10. 새 공식 소스 후보 재선정에 필요한 사항

1. Codex 판정(G4-TEMP)이 나온 뒤 OWNER가 새 후보(`262f440` 또는 그 위 문서 Commit)를 확정한다. 그 전에는 `8ca9705`가 공식 제품 소스다.
2. 문서 Commit(이 보고서·Codex 요청서·CURRENT_STATE·대시보드)은 별도 승인이 필요하다.
3. Push 승인 뒤 공식 원격 독립 Clone 2개에서 verify, Golden, Dry Pilot, CI Build, B-4 Artifact SHA를 다시 측정한다(`EVIDENCE_VERIFIER`·시험 어셈블리 SHA 변경 예상). SourceLink는 원격 Raw와 대조하고, PCK 재현성도 다시 확인한다. 이번 로컬 결과는 그 증거가 아니다.
4. G1 도구는 이번 변경에 영향을 받지 않았다(5절). 새 후보 Commit 기준의 도구 재고정이 필요한지는 Codex·OWNER가 판단한다.
5. G4-ENV-01의 처리 방식(OWNER 설정인지 코드 기본값인지)을 정해야 한다.

2026-10-31 갱신: 1은 Codex `RESOLVED_IN_CODE` 뒤 신규 게시 후보로 준비 중이다(공식 빌드 제품 소스 = `262f440`, 게시 HEAD = 그 위 문서 Commit 안; 게시는 별도 OWNER 승인). 2는 승인되어 수행했다. 3은 그대로 남는다. 5는 OWNER 정책(설정에 `VSTEST_` 필수)으로 정해졌고 운영 조건이 12절에 있다.

## 11. Codex 판정 (2026-10-31, OWNER 전달 요약)

원문은 이 저장소에 보관하지 않았다. 아래는 OWNER가 작업 지시로 전달한 판정 요약이다.

| 항목 | 판정 |
|---|---|
| G4-TEMP 보완(`262f440`) | **REVIEW_READY**, **RESOLVED_IN_CODE** |
| G4-ENV-01 | **OPEN**. 보호 설치 설정에 `VSTEST_`가 빠지면 **MAJOR** |
| G5-TEMP-01 | **RESOLVED_IN_CODE** |
| 신규 코드 MAJOR / MINOR | **0 / 0** |
| NOTE-01 | 7절 Registry·자식 시작 표현 과대 → 7절에서 정정 |
| 공식 운영 G1~G9 | **미실행** |

Codex 판정은 코드 단계 판정이며, 공식 G4 PASS·인증 성공·Build Freeze가 아니다.

## 12. Approval B 준비표 — G4-ENV-01 운영 조건 (OWNER 정책 2026-10-31)

OWNER 정책: 공식 보호 설치 설정 `install-config.json`의 `startup_forbidden_prefixes`에 `VSTEST_`를 **반드시** 포함한다. **현재 코드에는 이 설정 누락을 강제로 거부하는 기본 조건이 없다**(`InstallConfig.cs`는 접두사 형식 `^[A-Za-z][A-Za-z0-9_]{0,63}$`와 정렬·중복만 검사하고 필수 항목은 없다. E4 기본 제거는 `DOTNET_`·`COMPlus_`뿐). 이번 작업에서 운영 설정을 만들거나 설치하지 않았다.

| # | Approval B 조건 | 현재 근거 | 운영 확인 |
|---|---|---|---|
| ENV-1 | 보호 설치 설정 `startup_forbidden_prefixes`에 `VSTEST_` 포함 | 설정 기능 존재(G4-T8). 시험 고정값에 `VSTEST_` 포함(`G4OfficialEntryTests` 61행) | 실제 설치 설정 바이트·SHA를 Approval B 값으로 기록 |
| ENV-2 | 혼합 대소문자 환경 변수 거부(`vstest_console_path`, `VsTest_Console_Path` 등) | [정적] 시작 검사 `StartupViolations`와 자식 제거 모두 `OrdinalIgnoreCase`. 혼합 대소문자 시험은 `CORECLR_` 접두사(`CoreClr_Enable_Profiling`)로만 있음. `VSTEST_` 혼합 대소문자 시험은 없음(**미검증**) | 운영 계정에서 혼합 대소문자 변수로 시작 거부 실측 |
| ENV-3 | `VSTEST_CONSOLE_PATH`에 의한 실행 경로 변경 거부 | [IL] `VSTestForwardingApp::GetVSTestExePath`. `VSTEST_`가 설정에 있으면 시작 단계에서 `Environment.startup_policy`로 거부(코드상). 실제 변수로 거부한 시험 없음(CLI 시험은 `VSTEST_HOST_DEBUG`) | 운영 설정으로 `VSTEST_CONSOLE_PATH` 거부 실측 |
| ENV-4 | 누락된 설정을 운영 시작 전에 차단할 수 있는지 | **코드상 차단 없음.** 가능한 수단: (a) Approval B 설치 절차의 설정 바이트 검수(설정 SHA 고정), (b) 별도 승인 시 코드 기본값으로 `VSTEST_` 추가 또는 설정 필수 항목화(제품 코드 변경 → 새 소스 후보·재검수 필요) | OWNER가 (a)/(b) 선택. 결정 전까지 설정 누락 = MAJOR |
| ENV-5 | 실제 운영 계정에서 자식 환경 상속 실측 | 개발 PC GREEN 재현에서만 PEB 실측(`dotnet test`·vstest.console·testhost의 TEMP/TMP, 부모 TEMP 무관) | 운영 계정·운영 설정으로 `VSTEST_*` 부재, TEMP/TMP = `<results>\temp` 실측. SYSTEM 실행이면 R-4 확인 |
| ENV-6 | 검수 전까지 G4-ENV-01 **OPEN** 유지 | — | ENV-1~5 운영 증거와 Codex 검수 뒤에만 상태 변경 |

## 13. A-1 신규 게시 사전검사

문서 Commit 뒤 읽기 전용으로 수행했다. 결과는 저장소 밖 `E:\Madegame\g3x\phase3ops\g4temp\a1_precheck\`에 있다(문서 Commit 안에는 넣을 수 없음). 게시·Push Dry-run·Tag 변경은 하지 않았다.
