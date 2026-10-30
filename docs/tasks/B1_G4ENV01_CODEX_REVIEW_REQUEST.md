# Codex READ-ONLY 독립 검수 요청 — G4-ENV-01 (`VSTEST_` 필수 설치 설정 Fail-Closed)

- 요청자: Claude Code, 2026-10-31. 근거: OWNER "G4-ENV-01 필수 설정 Fail-Closed 구현" 지시(B안 최소 코드 보완 승인)
- **READ-ONLY:** 코드·시험·계약·문서 수정, Commit, Push, Tag, 운영 적용을 하지 않습니다. 판정만 요청합니다.
- 대상: 로컬 Branch `phase3/g4-temp-protect`, 코드 Commit **`1437bd3230ef5a73d73df4f8a214c27c4b538197`**(Tree `b7574b931850420d192a1b9a0e895fbd9b16ab64`, 부모 = 문서 HEAD `3fa60b6684b7cf3d52aebb1a3e4a192d35ed280f`). 원격에 없습니다(Push 미승인).
- 비교 기준: G4-TEMP 제품 Commit `262f4402880cbaa933eb0a0488aa20a55cd9497f`(`3fa60b6`과 제품 코드 동일)
- **판정 결과 (2026-10-31, OWNER 전달 요약, 7절):** REVIEW_READY · G4-ENV-01 = **RESOLVED_IN_CODE**(현재 공식 진입점 한정) · 신규 MAJOR / MINOR / NOTE = 0 / 0 / 1. 아래 요청 본문은 요청 당시 그대로 둔다.
- 요청 당시 상태: G4-ENV-01 = **OPEN (Codex 판정 전, RESOLVED_IN_CODE 아님)** · G4-TEMP = RESOLVED_IN_CODE · G5-TEMP-01 = RESOLVED_IN_CODE · 신규 시험 20건 · 전체 xUnit 3,097 PASS / 0 FAIL / 0 SKIP · 공식 G1~G9 = **미실행** · Official Execution = NOT_READY · Final Execution Freeze = PENDING · Phase 3 = INCOMPLETE · Artifact Set = CANDIDATE_INCOMPLETE
- 보고서: `docs/reports/B1_G4ENV01_REPORT.md`(이 요청서와 함께 `1437bd3` 위 문서 전용 Commit에 포함, 제품 소스 아님). 증거: 저장소 밖 `E:\Madegame\g3x\phase3ops\g4env\`

## 1. 변경 요약

| 파일 | 내용 |
|---|---|
| `tools/StarPlayer.EvidenceVerifier/InstallConfig.cs` (+11/−2) | 상수 `RequiredStartupPrefix = "VSTEST_"`. `Read`: Schema 검사(`ApprovalFormats.Read`) 직후, 경로 정규화·루트 검사 전에 `startup_forbidden_prefixes`에 `VSTEST_`가 Ordinal로 없거나, 대소문자만 다른 항목(`vstest_`, `VsTest_` 등)이 있으면 `Evidence.approval_mismatch` / `Trust.install_config_policy`로 거부. 읽은 목록을 그대로 반환(추가·수정 없음). 문서 주석 2곳 |
| `tests/…/G4OfficialEntryTests.cs` (+99) | 신규 시험 6개 메서드, 20 Case(아래 3절) |
| `tests/…/G4ChildProgram.cs` (+19) | 격리 프로세스 시나리오 `g4-env-isolation <work> <prefixes>`, 가짜 자식 시나리오 `env-names`(받은 환경 변수 이름·TEMP 기록) |

바꾸지 않은 것: `install-config-v1` 필드 구조와 Schema(형식·정렬·중복 규칙), `ExecutionBinding`, `ExecutionEnvironment`(E4 `DOTNET_`·`COMPlus_`), `TestProfiles`(G4-TEMP 포함), `OfficialEntry` 순서, `src`, `game`, `content`, `docs/contracts`, Golden, Hash, RNG, Replay, 기존 시험과 기대값. 기존 시험 입력 갱신은 **없었다**(시험 설정 `WriteConfig`는 이미 `CORECLR_`·`VSTEST_`를 담고 있었다. Record 생성자로 직접 만드는 `G4Fixtures.Config`의 기본값 `["CORECLR_"]`은 `Read`를 거치지 않으므로 그대로 둠).

## 2. 확인 요청 항목 (OWNER 검수 범위 A~F, 2026-10-31)

Codex가 **직접 실행한 시험**과 **Claude가 만든 증거를 검토한 결과**를 구분해 주십시오.

### A. 설정 검증 우회 가능성

1. `OfficialEntry.Run`(`certify-family`·`certify-phase`, `Program.cs`에서 호출)의 순서: E2-1(`Certification.InitialCapture`) → `InstallConfigReader.Read` → `ExecutionBinding.Establish` → `TrustAnchorReader.Read` → Registry 연결 → 인증 → (`certify-phase`만) `Certification.RunTestProfile` 자식. `Read`가 이 공식 진입점에서 반드시 실행되는가.
2. `InstallConfig` Record 생성자는 public이다. 정적 검색(`new InstallConfig(`) 결과 운영 코드에서는 `InstallConfig.cs`의 `Read` 한 곳, 시험 쪽에서는 `G4Fixtures.Config` 한 곳뿐이다. 직접 만든 설정이 공식 실행을 우회할 수 있는가.
3. `ExecutionBinding.Establish`에 같은 검사를 두지 않아도 신뢰 경계가 유지되는가. 공식 `TestRunner`는 `b.Config.StartupForbiddenPrefixes`를 받는다(`Certification.cs` 146행).
4. 시험 Seam(`OfficialSeams`: `ConfigPath`·`Variables`·`Acl`·`Elevated`·`Connect`·`ProcessPath`, `TestRunner`의 internal `LeadingArguments`·`BeforeChild`)과 운영 기본값(고정 설정 경로 `%ProgramData%\StarPlayer\EvidenceVerifier\install-config.json`, 실제 ACL·Elevated 검사, 실제 Registry Pipe)을 구분해 주십시오.
5. 설치 설정 오류가 Anchor·Registry·자식 실행 전에 거부되는가. 시험은 Registry 연결 Seam이 호출되면 예외를 던지고, Work Root에 결과 폴더가 생기지 않음을 확인한다.

### B. 필수 접두사 규칙

1. 목록에 `VSTEST_`가 Ordinal로 정확히 있어야 한다.
2. 누락, 대소문자 변형(`vstest_`, `VsTest_`. 정확한 표기와 함께 있어도 거부), 부분 문자열(`VSTEST`), 개별 변수 이름(`VSTEST_CONSOLE_PATH`)이 거부되는가. `VS` 같은 더 넓은 접두사는 형식상 허용되지만 `VSTEST_`가 함께 있어야만 통과한다.
3. 배열 형식·Ordinal 정렬·중복 금지(`SchemaInvalid`)가 그대로이며 `VSTEST_` 검사보다 먼저 판정되는가.
4. 위반 오류가 `Evidence.approval_mismatch` / `Trust.install_config_policy`인가.
5. 배열을 자동으로 수정·보완하지 않는가(읽은 목록을 그대로 반환).

### C. 시작·자식 환경

1. `VSTEST_CONSOLE_PATH`가 공식 시작 경계(`Establish` → `Environment.startup_policy`)에서 거부되는가.
2. 혼합 대소문자(`vStEsT_ConSoLe_PaTh`)가 거부되는가. `StartupViolations`와 자식 제거는 기존대로 `OrdinalIgnoreCase`다. Windows 환경 블록은 이름의 대소문자를 구분하지 않으므로 두 표기가 동시에 있을 수 없다.
3. 자식 `dotnet test`에서 `VSTEST_*`가 제거되는가. 격리 자식 시험에 대조군(`VSTEST_` 없는 목록)이 있다.
4. 기존 `DOTNET_`·`COMPlus_`(E4) 정책이 유지되는가.
5. G4-TEMP의 보호된 TEMP/TMP 동작이 유지되는가(자식 환경 시험이 TEMP = `<results>\temp`도 확인).
6. 설정이 잘못됐을 때 자식 시작과 Registry 연결이 차단되는가.

### D. RED/GREEN 및 회귀

RED 9건이 실제로 기존 코드에서 실패했는지 확인해 주십시오. 나머지 11건은 기존 동작을 확인하는 시험입니다. GREEN 46/46, 전체 xUnit 3,097/0/0, Golden 4/4, Dry Pilot 9/9, D3 5/5, G1-a v2 PASS, PCK·Export 198개 불변, 이전 승인 계약·Hash·RNG·Replay 불변을 확인해 주십시오(3절, 보고서 3·4절, 증거 `E:\Madegame\g3x\phase3ops\g4env\`, `SHA256SUMS_g4env.txt`).

### E. B-4 Artifact 영향

1. 441행 후보의 역할·경로가 바뀌었는가. `b4_candidates.log` 기준으로 `262f440` 로컬 Clone 대비 추가 0, 삭제 0, 변경 4다.
2. SHA가 바뀐 4행의 실제 원인. 4행은 다음과 같다.
   - `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.EvidenceVerifier.dll`
   - `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/` 아래 `StarPlayer.EvidenceVerifier.dll`
   - 같은 폴더의 `StarPlayer.EvidenceVerifier.Tests.dll`
   - 같은 폴더의 `StarPlayer.Infrastructure.Tests.dll`
3. `Infrastructure.Tests.dll`은 소스가 그대로인데 바뀌었다. 신규 public 상수 `RequiredStartupPrefix` 때문에 참조 어셈블리 표면이 바뀐 것과 관련이 있는가. Claude는 **직접 확인하지 않은 가능성**으로만 기록했다.
4. 기존 PDB·Coverage Mapping 예외 범위가 유지되는가. CI 출력 793개 중 다른 18개에는 Clone 경로를 담는 `.msCoverageSourceRootsMapping_*` 6개가 포함된다.
5. 새로 로드 가능한 파일이 `unpinned_output_files`에 들어가지 않았는가. 이 목록은 OWNER 설정값이고 코드 변경은 없다.
6. 새 Commit의 DLL/PDB SHA가 기존 공식 값과 섞이지 않는가.

### F. 잔여 위험

4절 항목을 별도로 기록해 주십시오. 이번 수정으로 해결되었다고 표시하지 않습니다.

## 3. RED/GREEN

| 구분 | 결과 |
|---|---|
| RED (`3fa60b6` 제품 + 새 시험, 제품 diff 0) | 신규 20 Case 중 **실패 9**: `VSTEST_` 없는 설정 7종 모두 수용, CLI 2종은 누락 설정으로 Anchor 조회(`Trust.anchor_missing`)까지 진행. 나머지 11은 기존 기능(시작 환경 대소문자 무시 거부, 자식 제거, Schema 규칙)이라 통과 |
| GREEN (`1437bd3`) | G4 전체 **46/46**(기존 26 + 신규 20) |
| Worktree `verify.ps1` | GATE PASS, **3,097 PASS / 0 FAIL / 0 SKIP**(U6 124) |
| 로컬 Clean Clone(원본 = 로컬 Worktree) | verify GATE PASS 3,097, Golden 4/4, Dry Pilot 9/9, D3 5/5, CI 빌드 경고 0·오류 0, G1-a v2 PASS(PS 5.1·7), PCK `3a187e19…`·Export 198개 불변, B-4 441행(변경 4행 = EvidenceVerifier 계열과 Infrastructure.Tests) |

로컬 Clone이라 SourceLink는 공식 값이 아니고, DLL/PDB SHA는 새 값이다(보고서 4절).

## 4. 범위 밖 잔여 위험 (이번에 고치지 않음)

VSLANG(자식에 계속 전달), .NET CLI Telemetry, SYSTEM 계정 TEMP 동작(`GetTempPath2`), 실행별 `temp` 디렉터리 자동 정리 미구현, B-4 Artifact 최종 목록(441행 `CANDIDATE_INCOMPLETE`), 운영 설치 설정과 실제 계정·SID·ACL·Root, 실제 G5 및 실행 Pin 검증. 보고서 7절 참조.

## 5. 제출 요청 형식

- Verdict: `REVIEW_READY` / `FIX_REQUIRED` / `INSUFFICIENT_EVIDENCE`
- 신규 MAJOR / MINOR / NOTE
- G4-ENV-01 코드 수준 해결 여부
- 직접 생성한 `InstallConfig`로 우회할 수 있는지
- 기존 G4-TEMP와 G5-TEMP-01에 대한 영향
- 신규 제품 소스 후보(`1437bd3`)로 채택할 수 있는지
- 재검증이 필요한 B-4 Artifact
- 공식 운영에서 남은 필수 조건

## 6. 판정 이후 (OWNER 지시 6항)

`REVIEW_READY`이고 차단 Finding이 없으면, Claude는 다음을 진행한다.

1. 제품 소스 `1437bd3` 채택을 OWNER에게 보고한다.
2. 문서 Commit HEAD를 대상으로 A-1 읽기 전용 사전검사를 한다(신규 Commit, 개인정보, SECRET, Fast-forward).
3. 공식 원격 Push 승인을 OWNER에게 별도로 요청한다.
4. 게시 후 새 제품 소스 기준 G2/G3 재측정 계획을 작성한다.

판정 전에는 공식 제품 소스를 갱신하지 않는다.

## 7. 판정 결과 기록 (2026-10-31, 요청 뒤 추가)

Codex 판정 원문은 이 저장소에 보관하지 않았다. 아래는 OWNER가 전달한 요약이며, 원문을 새로 쓴 것이 아니다.

| 항목 | 판정 |
|---|---|
| Verdict | **REVIEW_READY** |
| G4-ENV-01 | **RESOLVED_IN_CODE** — 코드 수용은 **현재 공식 진입점**(`OfficialEntry.Run` → `InstallConfigReader.Read`)에 한정 |
| G4-TEMP / G5-TEMP-01 | RESOLVED_IN_CODE 유지 |
| 신규 MAJOR / MINOR / NOTE | **0 / 0 / 1** |
| 기존 공식 실행 경로의 우회 | 발견되지 않음 |
| 공식 운영 G1~G9 | 미실행 |

- 남은 한계(보고서 10절): 직접 생성한 `InstallConfig`는 일반적으로 검증을 우회할 수 있다(기존 공식 실행 경로에서는 발견 안 됨). `Infrastructure.Tests.dll` 변경 원인은 INFERRED다. 운영 설정·SID·ACL·실행 Pin 검증은 미완료다.
- NOTE 1의 원문 문장은 전달되지 않았다.
