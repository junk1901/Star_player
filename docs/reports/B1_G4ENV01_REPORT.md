# B1 G4-ENV-01 구현 보고서 — 설치 설정의 `VSTEST_` 필수 접두사 Fail-Closed

- 작성: Claude Code, 2026-10-31. 근거: OWNER "G4-ENV-01 필수 설정 Fail-Closed 구현" 지시(B안 최소 코드 보완 승인: 보안 코드 수정, 음성·양성 시험, 전체 회귀, 로컬 코드·시험 Commit 1개, Codex 독립 검수 요청)
- **성격:** Push, Tag, 운영 설치 설정 변경, 운영 ACL·Trust Anchor·Registry·Pin, 공식 G1~G9, Approval B/C, Final Freeze는 **하지 않았습니다.** 문서 4개만 별도 로컬 Commit했습니다(OWNER 2026-10-31 승인, 제품 소스 Commit 아님).
- 상태 (2026-10-31 Codex 판정 반영, 9·10절): G4-ENV-01 = **RESOLVED_IN_CODE**(현재 공식 진입점 한정) · Codex **REVIEW_READY** · 신규 MAJOR / MINOR / NOTE = **0 / 0 / 1** · G4-TEMP = RESOLVED_IN_CODE · G5-TEMP-01 = RESOLVED_IN_CODE · 신규 시험 20건 · 전체 xUnit 3,097 PASS / 0 FAIL / 0 SKIP · 공식 G1~G9 = **미실행** · Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · Artifact Set = **CANDIDATE_INCOMPLETE**
- 이전 상태 기록(Codex 판정 전 작성 시점): G4-ENV-01 = **OPEN (Codex 판정 전, RESOLVED_IN_CODE 아님)** · G4-TEMP = RESOLVED_IN_CODE · G5-TEMP-01 = RESOLVED_IN_CODE · 신규 시험 20건 · 전체 xUnit 3,097 PASS / 0 FAIL / 0 SKIP · 공식 G1~G9 = **미실행** · Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · Artifact Set = **CANDIDATE_INCOMPLETE**
- 증거: 저장소 밖 `E:\Madegame\g3x\phase3ops\g4env\`

## 1. 작업 위치와 Commit

| 항목 | 값 |
|---|---|
| Worktree / Branch | `E:\Madegame\Star_player_g4temp` / `phase3/g4-temp-protect`. 기존 `Star_player`와 C-3 Worktree(`Star_player_c3`)는 손대지 않음 |
| 부모 Commit | `3fa60b6684b7cf3d52aebb1a3e4a192d35ed280f`(G4-TEMP 문서 HEAD. 그 아래 G4-TEMP 제품 Commit `262f440`, 공개 GitHub HEAD `292fa18`) |
| **코드·시험 Commit** | **`1437bd3230ef5a73d73df4f8a214c27c4b538197`**, Tree **`b7574b931850420d192a1b9a0e895fbd9b16ab64`** |
| 변경 파일 | `tools/StarPlayer.EvidenceVerifier/InstallConfig.cs`(+11/−2), `tests/StarPlayer.EvidenceVerifier.Tests/G4OfficialEntryTests.cs`(+99), `tests/StarPlayer.EvidenceVerifier.Tests/G4ChildProgram.cs`(+19) |
| 바꾸지 않은 것 | `install-config-v1` 필드·Shape, `ExecutionBinding`, `ExecutionEnvironment`, `TestProfiles`(G4-TEMP), `OfficialEntry`, `Certification`, `src`, `game`, `content`, `docs/contracts`, `TASK.md`, `.gitattributes`, `global.json`, `tools/b1-ops`, Golden·Hash·RNG·Replay, 기존 시험과 기대값 |
| 문서 Commit | 이 보고서, `docs/tasks/B1_G4ENV01_CODEX_REVIEW_REQUEST.md`, `CURRENT_STATE.md`, `Star/00_Dashboard.md`만 담은 별도 문서 전용 Commit(OWNER 2026-10-31 승인, `1437bd3` 위). **제품 소스 Commit이 아니다**(제품·시험 파일 변경 없음) |

## 2. 보안 경계 — 구현 전후

**이전(`262f440`/`3fa60b6`):** `InstallConfigReader.Read`는 `startup_forbidden_prefixes`의 형식(`^[A-Za-z][A-Za-z0-9_]{0,63}$`), Ordinal 정렬, 중복만 검사했다. `VSTEST_`가 없는 설정도 받아들였다. 그러면 시작 검사(`ExecutionBinding.StartupViolations`)가 `VSTEST_CONSOLE_PATH`를 거부하지 않는다. Test Profile 자식 환경 제거(`TestProfiles.Run`)도 그 변수를 지우지 않는다. 이 경우 `dotnet test`가 다른 vstest.console.dll을 실행할 수 있다(G4-ENV-01).

**이후(`1437bd3`):**

```text
OfficialEntry.Run
  E2-1 (DOTNET_/COMPlus_)  →  InstallConfigReader.Read
                                ├ 파일 존재·정규 경로·비Elevated·ACL(TCB)   (기존)
                                ├ Schema (형식·정렬·중복)                  (기존, SchemaInvalid)
                                ├ ★ VSTEST_ 정확 표기 필수, 대소문자 변형 거부 → approval_mismatch / Trust.install_config_policy
                                └ 경로 정규화·루트 검사                     (기존)
                         →  ExecutionBinding.Establish (시작 환경: 설정 접두사 OrdinalIgnoreCase, 기존)
                         →  TrustAnchorReader.Read → Registry → 인증 → Test Profile 자식 (자식 환경: 같은 접두사 제거 + TEMP/TMP 보호, 기존)
```

- 규칙: 목록에 `VSTEST_`가 Ordinal로 **정확히** 있어야 한다. 대소문자만 다른 항목(`vstest_`, `VsTest_`)이 있으면 함께 있어도 거부한다. 개별 변수 이름(`VSTEST_CONSOLE_PATH`)이나 밑줄 없는 `VSTEST`는 대신하지 못한다. `VS` 같은 더 넓은 접두사는 형식상 허용하지만 `VSTEST_`가 함께 있어야 통과한다.
- 목록은 **읽은 그대로** 반환한다. 자동 추가·정렬·수정은 없다.
- 오류 체계: 기존 설치 설정 오류와 같은 `Evidence.approval_mismatch`(`Untrusted`)이고, 원인 이름은 `Trust.install_config_policy`다. Schema 위반은 계속 `SchemaInvalid`로, `VSTEST_` 검사보다 먼저 판정된다.
- 위치: Schema 검사 직후, 경로 정규화·Anchor·Registry·자식 실행 전이다. `OfficialEntry`에서 `Read`가 `Establish`·Anchor·Registry보다 앞서므로, 거부하면 그 뒤로 진행하지 않는다.
- `DOTNET_`·`COMPlus_`(E4)와 OWNER 추가 접두사 정책은 그대로다. 시작 검사와 자식 제거의 대소문자 무시 비교도 그대로다.
- `ExecutionBinding.Establish` 중복 검토: 운영 코드에서 `InstallConfig`를 만드는 곳은 `Read` 한 곳뿐이다(`new InstallConfig(` 정적 검색: 운영 1곳, 시험 고정 장치 1곳). 공식 `TestRunner`는 `b.Config.StartupForbiddenPrefixes`를 받는다(`Certification.cs` 146행). 그래서 `Establish`에 같은 검사를 두지 않았다(구조 변경 없음). `InstallConfig` Record 생성자는 public이다. 이 판단은 Codex 확인 항목으로 넘겼다.

## 3. 추가 시험과 RED/GREEN

신규 6개 메서드, **20 Case** (`G4OfficialEntryTests`, 격리 시나리오 `G4ChildProgram`).

| 시험 | Case | RED (`3fa60b6` 제품 + 새 시험) | GREEN (`1437bd3`) |
|---|---|---|---|
| `A_config_naming_the_exact_vstest_prefix_is_read_with_its_prefixes_unchanged` | `VSTEST_` / `CORECLR_,VSTEST_` / `CORECLR_,OWNER_EXTRA_,VSTEST_` → 읽음, 목록 불변 | 통과 3 | 통과 3 |
| `A_config_without_the_exact_vstest_prefix_is_refused_as_an_approval_mismatch` | 빈 목록, `CORECLR_`, `CORECLR_,VSTEST_CONSOLE_PATH`, `CORECLR_,vstest_`, `VSTEST`, `VSTEST_,vstest_`, `VSTEST_,VsTest_` | **실패 7**(모두 예외 없이 수용) | 통과 7 (`approval_mismatch` / `Trust.install_config_policy`) |
| `The_prefix_list_keeps_its_schema_order_and_duplicate_rules_before_the_vstest_rule` | 중복 `VSTEST_,VSTEST_`, 역순 `VSTEST_,CORECLR_`, 형식 위반 `CORECLR-,VSTEST_` → `SchemaInvalid` | 통과 3 | 통과 3 |
| `A_vstest_variable_in_any_case_refuses_the_start_of_a_binding_built_from_the_read_config` | 설정 파일 → `Read` → `Establish`. 시작 환경 `VSTEST_CONSOLE_PATH`, `vStEsT_ConSoLe_PaTh`, `VSTEST_HOST_DEBUG`, `VSTEST_DISABLE_ARTIFACTS_POSTPROCESSING` → `startup_policy`. 금지 변수 없으면 Binding 성립 | 통과 4 | 통과 4 |
| `A_config_missing_vstest_or_a_vstest_start_variable_stops_the_cli_before_the_anchor_registry_and_any_test_profile_child` | `certify-family`·`certify-phase` CLI. Registry 연결 Seam은 호출되면 예외. 누락 설정 → `install_config_policy`(시작 환경에 `VSTEST_CONSOLE_PATH`가 있어도 설정 거부가 먼저), 올바른 설정 + `vStEsT_ConSoLe_PaTh` → `startup_policy`, 대조군 → `anchor_missing`. 두 설정 모두 Work Root 결과 폴더 미생성 | **실패 2**(누락 설정이 통과해 `Trust.anchor_missing`까지 진행 = Anchor 조회 단계 도달) | 통과 2 |
| `The_parents_vstest_variables_never_reach_the_test_profile_child_when_the_config_names_vstest` | 격리 프로세스 환경 `vStEsT_ConSoLe_PaTh`, `VSTEST_HOST_DEBUG`, `VsTest_Diag`, `G4_ENV_KEEP`. 제거 목록 `CORECLR_,VSTEST_` → 자식 환경에 `VSTEST_*` 0, `G4_ENV_KEEP` 있음, TEMP = `<results>\temp`, PASS 유지. 대조군 `CORECLR_`만 → 세 변수 모두 자식에 전달됨 | 통과 1 | 통과 1 |

- 구분:
  - **직접 실행한 RED/GREEN:** 위 표의 실패 9 → 통과. RED 실행은 제품 diff 0을 확인한 뒤 시험만 추가한 상태에서 했다(`logs\red_g4env.console.log`·`.trx`: 필터상 기존 1건 포함 21건 중 실패 9). GREEN은 G4 전체 46/46이다(기존 G4 26 + 신규 20, `logs\green_g4.console.log`·`.trx`).
  - **기존 동작을 직접 확인한 시험(RED에서도 통과):** 시작 환경 대소문자 무시 거부, 자식 환경 제거, Schema 규칙. 이번 변경 전부터 있던 기능이며, 이제는 실제 설정 파일을 거쳐 확인한다.
  - **정적 분석:** `Read`가 `Establish`·Anchor·Registry보다 먼저 호출된다. 운영 코드의 `InstallConfig` 생성처는 1곳이다. 공식 `TestRunner`가 설정 접두사를 받는다.
  - **기존 증거 재검토:** G4-TEMP 단계의 SDK 분석(`VSTestForwardingApp::GetVSTestExePath`, `vstest_env_names.txt`)이 위험 근거다. 다시 실행하지 않았다.
- **보호 정책 위반 시 자식 실행 0회:** CLI 시험의 Registry Seam이 호출되지 않았다. Test Profile 자식은 Registry 연결 뒤에만 실행되므로 자식도 실행되지 않았다. Work Root 결과 폴더가 생기지 않은 것도 확인했다.
- **기존 시험 입력 갱신:** 없음. 설정 파일을 만드는 시험(`WriteConfig`)은 이미 `CORECLR_`·`VSTEST_`를 담고 있었다. Record 생성자로 직접 만드는 `G4Fixtures.Config` 기본값(`CORECLR_`)은 `Read`를 거치지 않는 Binding·자식 시험용이라 바꾸지 않았다. 기존 시험의 삭제·완화는 없다.
- **유지 확인:** G4-TEMP 6건, 실제 `dotnet test` 자식, Exit Code·TRX·Timeout·트리 종료 시험은 G4 46/46과 전체 verify에서 모두 통과했다. 해당 코드도 바꾸지 않았다.

## 4. 전체 회귀

| 실행 | 위치 | 결과 |
|---|---|---|
| `verify.ps1` | Worktree(Commit 직전, 같은 바이트) | **GATE: PASS**. U1 327, U2 741, U3 128(U3a 14, U3b 48), U4 1,237, U5 540, U6 **124** = **3,097 PASS / 0 FAIL / 0 SKIP**(3,077 + 신규 20). B1 경고 0, G1~G4, E1~E4, D1(280), D3(5) PASS. 끝난 뒤 verify가 갱신한 `docs/reports/PHASE_2_VERIFY_LOG.md`는 증거 폴더에 복사하고 되돌렸다 |
| `verify.ps1` | **로컬 Clean Clone** `E:\Madegame\g3x\g4envA`(원본 = 로컬 Worktree, `checkout --detach 1437bd3`, `core.autocrlf=true`, HEAD·Tree 일치, 작업 트리 0) | **GATE: PASS**(1,582초), 3,097 PASS, D1 278, D3 5, 끝난 뒤 작업 트리 0 |
| Golden | 같은 Clone | **4/4 PASS** |
| Dry Pilot 9 | 같은 Clone | **9/9 동일** |
| D3 보호 파일 | 같은 Clone | **5/5 SHA 일치** |
| CI Release Build | 같은 Clone | 종료 0, 경고 0, 오류 0, 출력 793개 |
| G1-a 감사 도구 v2 | 같은 Clone, 도구 `98724888`(SHA `4d086a5c…f96b` 일치) | PS 5.1·PS 7 모두 **RESULT=PASS production_files=164 names=9 mismatches=0**. `InstallConfig.cs`는 고정 파일 8개에 들지 않는다 |
| verify 내 export PCK | 같은 Clone | 13,452 B, `3a187e19…9e799` |
| Godot PCK(별도 실행기) | 같은 Clone, 기존 G3 실행기 `run_pck.ps1`(clean) | `StarPlayer.pck` 13,452 B, SHA-256 `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799` = **기존과 같음**. Export 파일 198개 모두 `262f440` 실행(`G4TEMPA_P1`)과 같음. `pck-out\G4ENVA_P1\meta.json` SHA `98eb73bb…30b1` |
| B-4 후보 | 같은 Clone, 기존 `b4-candidates.ps1` | 441행. `262f440` 로컬 Clone 대비 변경 4행(`EvidenceVerifier.dll` 2곳, `EvidenceVerifier.Tests.dll`, `Infrastructure.Tests.dll`), 추가·삭제 0. `CANDIDATE_INCOMPLETE` 유지 |

- **바이너리 SHA:** 같은 출처 유형(로컬)의 `262f440` Clone과 CI 출력 793개를 비교하면 18개가 다르다.
  - `StarPlayer.EvidenceVerifier.dll/.pdb`(`5e6efaa5…` → `b203605a…`, 3곳)
  - `StarPlayer.EvidenceVerifier.Tests.dll/.pdb`(2곳)
  - `StarPlayer.Infrastructure.Tests.dll/.pdb`
  - Clone 경로를 담는 `.msCoverageSourceRootsMapping_*` 6개(이미 Artifact 규칙 밖)

  `src`·`game`·SimHarness 출력은 같다. `Infrastructure.Tests`는 소스가 그대로인데 바뀌었다. 이 프로젝트는 `EvidenceVerifier.Tests`를 참조하고, `EvidenceVerifier`의 public 표면이 바뀌었다(새 public 상수). 이로써 참조 어셈블리가 달라진 것이 원인으로 보인다(**[가능성] 코드 수준 설명, 직접 확인하지 않음**). **기존 바이너리 SHA를 공식 값으로 재사용하지 않는다.**
- **SourceLink:** 로컬 출처 Clone이라 PDB 55개 모두 SourceLink가 기대값과 다르다(`sourcelink_all_expected=false`). 공식 원격 검증 결과가 아니다. 새 Commit은 원격에 없다.

## 5. Codex 독립 검수 요청서

[B1_G4ENV01_CODEX_REVIEW_REQUEST.md](../tasks/B1_G4ENV01_CODEX_REVIEW_REQUEST.md)

## 6. G4-ENV-01 상태와 Approval B 조건(`B1_G4TEMP_REPORT.md` 12절) 대응

| 조건 | 이번 코드 이후 |
|---|---|
| ENV-1 보호 설치 설정에 `VSTEST_` 포함 | 코드가 강제한다(누락 설정 거부). 실제 설치 설정 바이트·SHA 기록은 Approval B에서 한다 |
| ENV-2 혼합 대소문자 거부 | 시작 환경: `vStEsT_ConSoLe_PaTh` 거부를 xUnit으로 직접 확인했다. 설정값: 대소문자 변형을 거부한다. 운영 계정 실측은 남았다 |
| ENV-3 `VSTEST_CONSOLE_PATH` 거부 | xUnit으로 직접 확인했다(`Establish`와 CLI) |
| ENV-4 누락 설정 운영 전 차단 | 코드 기본 조건으로 해결했다(B안). Codex 판정 전이다 |
| ENV-5 운영 계정 자식 환경 상속 실측 | 개발 PC의 격리 자식 시험만 했다. 운영 실측은 남았다 |
| ENV-6 검수 전 OPEN 유지 | **OPEN 유지**(Codex 판정 전) |

## 7. 범위 밖 잔여 위험 (이번에 해결하지 않음)

| 항목 | 내용 |
|---|---|
| VSLANG 고정 정책 | `VSLANG`은 `VSTEST_` 접두사가 아니라 여전히 자식에 전달된다(TRX 지역화 바이트. outcome·Counters 판정은 같음) |
| CLI Telemetry | `DOTNET_CLI_TELEMETRY_OPTOUT`이 E4로 제거되어 자식이 `%USERPROFILE%\.dotnet\TelemetryStorageService`에 쓴다(판정 입력 아님) |
| SYSTEM 계정 TEMP | `GetTempPath2`가 SYSTEM에서 TEMP/TMP를 무시한다(G5 운영 계정 설계에서 확인) |
| `temp` 자동 삭제 | 결과 폴더와 같은 수명이다 |
| B-4 Artifact 441행 확정 | `CANDIDATE_INCOMPLETE` |
| 운영 계정·SID·ACL·Root | Approval B |
| 운영 설치 설정 실제 생성 | 만들지 않았다. 기존 문서상 설정 예시가 `VSTEST_`를 빠뜨리지 않는지는 운영 설정 작성 때 확인 |

## 8. 새 공식 소스 후보에 필요한 사항

1. Codex가 G4-ENV-01을 판정한 뒤 OWNER가 새 제품 소스 후보(`1437bd3`)를 확정한다. 그 전에는 직전 후보 `262f440`이다.
2. 문서 Commit(이 보고서·Codex 요청서·CURRENT_STATE·대시보드)은 OWNER 승인(2026-10-31)으로 수행했다. Codex 판정 뒤 A-1 읽기 전용 사전검사는 그 문서 Commit HEAD를 대상으로 한다.
3. 게시 승인 뒤, 공식 원격 독립 Clone 2개에서 verify, Golden, Dry Pilot, CI 빌드, B-4 SHA, SourceLink, PCK를 다시 측정한다. A-1 사전검사도 새 HEAD로 다시 한다(`3fa60b6` 기준 결과는 이 Commit을 포함하지 않음).
4. G1-a 도구는 영향이 없다(PASS, 고정 파일 8개 불변).

2026-10-31 갱신: 1은 Codex REVIEW_READY 뒤 OWNER가 문서 동기화와 조건부 게시를 승인했다. 새 제품 소스의 공식 결속·운영 채택은 공식 원격 Clone 재측정(G2/G3) 뒤 별도 OWNER 승인 대상이다. 3의 재측정은 아직 실행하지 않았다.

## 9. Codex 판정 (2026-10-31, OWNER 전달 요약)

Codex 판정 원문은 이 저장소에 보관하지 않았다. 아래는 OWNER가 작업 지시로 전달한 판정 요약이며, 원문을 새로 쓴 것이 아니다.

| 항목 | 판정 |
|---|---|
| Verdict | **REVIEW_READY** |
| G4-ENV-01 | **RESOLVED_IN_CODE** — 코드 수용은 **현재 공식 진입점**(`OfficialEntry.Run` → `InstallConfigReader.Read`)에 한정 |
| G4-TEMP / G5-TEMP-01 | RESOLVED_IN_CODE 유지 |
| 신규 MAJOR / MINOR / NOTE | **0 / 0 / 1** |
| 기존 공식 실행 경로의 우회 | 발견되지 않음 |
| 공식 운영 G1~G9 | 미실행 |

NOTE 1의 원문 문장은 전달되지 않았다. 10절의 한계는 OWNER 지시에 적힌 표현을 따른다.

## 10. 수용 범위와 남은 한계

| 항목 | 상태 |
|---|---|
| 수용 범위 | 현재 공식 진입점(`certify-family`·`certify-phase` → `InstallConfigReader.Read` → `ExecutionBinding.Establish` → Anchor → Registry → 인증 → Test Profile 자식)에 한정된 코드 수용 |
| 직접 생성한 `InstallConfig` | Record 생성자가 public이라, `Read`를 거치지 않고 만든 설정은 **일반적으로 검증을 우회할 수 있다**(한계로 보존). 기존 공식 실행 경로에서는 우회가 발견되지 않았다(운영 코드의 생성처는 `Read` 한 곳). 새 진입점을 추가하면 같은 검사가 필요하다 |
| `Infrastructure.Tests.dll` SHA 변경 원인 | **INFERRED**(신규 public 상수 → 참조 어셈블리 표면 변화로 추정, 직접 확인 안 함). 공식 원격 Clone 재측정에서 다시 확인한다 |
| 운영 검증 | 운영 설치 설정, 서비스 계정·SID, ACL, Root, 실제 G5, 실행 Pin은 **미완료**. Approval B 조건 ENV-1·2·5의 운영 실측도 남아 있다 |
| 잔여 위험(7절) | VSLANG, CLI Telemetry, SYSTEM 계정 TEMP, `temp` 자동 정리, B-4 최종 목록. 이번 수정으로 해결되지 않았다 |
| 6절 ENV-6 "OPEN 유지" | 판정 전 기록이다. 판정 뒤 G4-ENV-01은 코드 수준에서 RESOLVED_IN_CODE이고, 운영 조건(ENV-1·2·5)은 Approval B에 남는다 |
