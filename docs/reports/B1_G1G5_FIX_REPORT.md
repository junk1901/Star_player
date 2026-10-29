# B1 Codex FIX_REQUIRED 후속 보고서 — G1-POLICY-01 수정·G5-TEMP-01 운영 보호 모델

- 작성: Claude Code, 2026-10-30. 근거: OWNER 지시 "Codex FIX_REQUIRED 후속 조치"(2026-10-30)
- Codex 판정(대상 `7bae96e`): 최종 **FIX_REQUIRED** · G2 E-1 **ACCEPTABLE** · G1 G1-a **NEEDS_CHANGE** · G1-POLICY-01 **MINOR** · G5-TEMP-01 **OPEN_MAJOR**
- **성격:** 감사 도구 수정, 문서, 비운영 진단입니다. Push·Tag·계약·`.gitattributes`·제품·G4/G5 코드·Golden·Hash·RNG·Replay 변경, 운영 ACL 적용, Trust Anchor·Pin 등록, 공식 인증, 공식 Gate PASS 선언, Build Freeze는 **없습니다**.
- 기준: 공식 원격 `main` = `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f`(수정하지 않음). 로컬(미게시) `e8f8f72`·`200f448`·`92297cb`·`7bae96e`·`c4a04bb`(도구)·이 보고서 Commit
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 제출물 (지시 6항)

| # | 제출물 | 위치 |
|---|---|---|
| 1 | G1-POLICY-01 수정 전후 RED/GREEN | 2.3절 |
| 2 | 검사 도구 새 SHA | 2.2절 |
| 3 | 기존 6개·신규 반례 결과 | 2.3·2.4절 |
| 4 | PowerShell 5.1/7 결과 | 2.4절(7은 **미검증**) |
| 5 | G5-TEMP-01 강화 운영 보호 모델 | [보호 모델](../tasks/B1_G5_TEMP01_PROTECTION_MODEL.md) 1·2절 |
| 6 | 강제 실행 차단 절차 | 같은 문서 3절 |
| 7 | M-12A~J 실측 계획 | 같은 문서 4절 |
| 8 | 코드 변경 없이 `c77ed1a` 유지 조건 | 같은 문서 6절 |
| 9 | Codex 표적 검수 요청서 | [요청서](../tasks/B1_G1G5_FIX_CODEX_REVIEW_REQUEST.md) |
| 10 | 공식 G1/G2/G3 승인에 남은 사항 | 5절 |

## 2. G1-POLICY-01 수정

### 2.1 원인과 변경

- **원인:** 첫 도구는 이름 문자열의 **파일별 등장 횟수**만 비교했습니다. 경로 구성식이 바뀌어도 개수가 같으면 PASS였습니다. Codex 반례 두 개(`run/{runId}/` → `run/wrong/{runId}/`, `UnitPreflightName`의 `.json` → `.txt`)가 그대로 통과했습니다(2.3절 RED).
- **변경(`tools/b1-ops/g1-names-check.ps1`, Commit `c4a04bb`):** 기존 검사 1에 검사 2~4를 더했습니다. 모든 기준값은 `c77ed1a`에서 고정했습니다.

| # | 검사 | 내용 | 막는 것 |
|---|---|---|---|
| 1 | names(기존) | 이름 리터럴 9종의 파일별 개수 | 이름 변경·추가·삭제 |
| 2 | definitions | B-4 6.1 각 행의 경로 생성 정의 13줄이 고정 문장 그대로 정확히 한 번 있음(`RunPath`, `RootRelative`, `PreflightFile`·`PreflightDirectory`, `UnitPreflightName`(확장자 포함), `UnitDirectory`, Archive `units/`, `CellDirectory`, 실행 색인 2개, 생산자·검증기·Family·Dominance의 `…/manifest.json` 경로) | 경로 구성식 변경(같은 개수 포함) |
| 3 | lines | 주석이 아닌 줄 중 이름 리터럴 또는 경로 기호(`RunPath`, `UnitPreflightName`, `PreflightFile`, `PreflightDirectory`, `RootRelative`, `ResultFile`, `UnitDirectory`, `CellDirectory`)를 가진 줄 전부(50줄, 8개 파일)의 SHA-256 다중집합 | 생산자·검증기의 사용처 변경, 목록 밖 파일의 새 사용 |
| 4 | files | 위 8개 파일의 SHA-256(CRLF→LF 정규화). 8개 모두 `git show c77ed1a:<파일>`의 SHA-256과 같음(대조함) | 그 파일 안에서 경로로 흘러가는 값의 변경(해당 줄에 이름·기호가 없어도) |

- 출력 첫 줄에 도구 경로·SHA-256·PowerShell 버전, 둘째 줄에 대상 Root·`HEAD`를 기록합니다(감사 증거 묶음).
- `-SelfTest`는 대상의 생산 파일을 임시 디렉터리에 복사해 변형하며, 대상(`-Root`)은 건드리지 않습니다. 일반 실행은 읽기만 합니다.

### 2.2 도구 SHA (새로 산출, 이전 값 재사용 없음)

| 도구 | Git | SHA-256 |
|---|---|---|
| 이전(검수 대상) | `7bae96e:tools/b1-ops/g1-names-check.ps1` | `28f07574c480fd7588f63f5ce3ce46eebac4f6d87520237449adabab669d8fcd` |
| **수정** | `c4a04bb:tools/b1-ops/g1-names-check.ps1` | **`d3945b5ed51f0c49e39b2689bd73a8b14240ff9d13564745aca12f22eeb56ba0`** |

- 도구는 제품 빌드에 포함되지 않습니다(`tools/b1-ops/`, `.gitattributes` `eol=lf`, `StarPlayer.sln` 밖).

### 2.3 RED/GREEN (같은 변형 사본에 이전·수정 도구를 각각 별도 프로세스로 실행)

드라이버 `E:\Madegame\g3x\phase3ops\g1fix\redgreen.ps1`(도구의 `-SelfTest`와 독립된 두 번째 구현). 대상 = `c77ed1a` CRLF Clone의 생산 파일 + `StarPlayer.sln` 사본. 종료 코드 0 = PASS, 1 = FAIL.

| 반례 | 구분 | 이전 도구 | 수정 도구 | 기대(수정) |
|---|---|---|---|---|
| baseline_copy | 대조 | PASS 0 | PASS 0 | PASS |
| verifier_renames_manifest | 기존 1 | FAIL 1 | FAIL 1 | FAIL |
| producer_renames_manifest | 기존 2 | FAIL 1 | FAIL 1 | FAIL |
| verifier_hardcodes_run_prefix | 기존 3 | FAIL 1 | FAIL 1 | FAIL |
| preflight_renamed | 기존 4 | FAIL 1 | FAIL 1 | FAIL |
| result_root_moved | 기존 5 | FAIL 1 | FAIL 1 | FAIL |
| second_dominance_index_literal | 기존 6 | FAIL 1 | FAIL 1 | FAIL |
| **runpath_wrong_segment** | Codex 반례 1 | **PASS 0 (RED)** | FAIL 1 | FAIL |
| **unit_preflight_extension_txt** | Codex 반례 2 | **PASS 0 (RED)** | FAIL 1 | FAIL |
| verifier_manifest_subdir_same_count | 같은 개수·다른 경로 | **PASS 0 (RED)** | FAIL 1 | FAIL |
| producer_archive_manifest_moved_same_count | 같은 개수·다른 경로 | **PASS 0 (RED)** | FAIL 1 | FAIL |
| unit_preflight_dir_swapped_same_count | 같은 개수·순서 바뀐 경로 | **PASS 0 (RED)** | FAIL 1 | FAIL |
| new_file_composes_run_path | 새 파일의 `RunPath` 사용 | **PASS 0 (RED)** | FAIL 1 | FAIL |
| ungoverned_dataflow_in_governed_file | 이름·기호 없는 줄로 경로 값 변경(`runId + "-x"`) | **PASS 0 (RED)** | FAIL 1 | FAIL |
| line_endings_flipped | 오탐 대조(8개 파일 CRLF↔LF) | PASS 0 | PASS 0 | PASS |
| unrelated_file_edit | 오탐 대조(무관한 새 파일) | PASS 0 | PASS 0 | PASS |
| assembled_name_in_new_file | **알려진 한계**(새 파일에서 `"ru" + "n/"`·`"/manifest" + ".json"` 조립) | PASS 0 | PASS 0 | 탐지 못함(LIMIT) |

- 이전 도구가 놓친 7건(RED)을 수정 도구가 모두 FAIL로 잡았습니다. 기존 6건은 두 도구 모두 FAIL, 오탐 대조 2건은 두 도구 모두 PASS입니다.

### 2.4 실행 결과

| 실행 | PowerShell | 대상 | 결과 | 종료 코드 |
|---|---|---|---|---|
| 수정 도구 일반 실행 | 5.1.26100.9711 Desktop | `c77ed1a` Clone(`core.autocrlf=true`) | `RESULT=PASS` 생산 파일 164, 정의 13, 지배 줄 50(8개 파일), 고정 파일 8, 불일치 0 | 0 |
| 수정 도구 일반 실행 | 5.1 | `c77ed1a` Clone(`core.autocrlf=false`) | 같음 | 0 |
| 수정 도구 `-SelfTest` | 5.1 | 두 Clone 각각 | `SELFTEST cases=17 wrong=0`(두 로그 바이트 동일) | 0 |
| 이전 도구 일반·`-SelfTest` | 5.1 | CRLF Clone | PASS, `cases=7 failed=0`(이전 보고서의 로그 SHA `b6805117`·`08e584f5`와 같은 바이트) | 0 |
| RED/GREEN 드라이버 | 5.1 | CRLF Clone | 2.3절 표, 수정 도구 판정 17/17 OK | 0 |
| 수정 도구 일반 실행 | 5.1 | 작업 트리 `E:\Madegame\Star_player_g4`(제품 경로 `c77ed1a`와 차이 0) | PASS | 0 |
| **수정 도구 전체** | **PowerShell 7** | — | **미검증** — 이 개발 PC에 PowerShell 7(`pwsh`)이 설치되어 있지 않습니다. 설치(다운로드)는 OWNER 허락이 필요합니다 | — |

**조건 기록:**

- Clean Checkout: `git clone -c core.autocrlf={true|false} --no-hardlinks https://github.com/junk1901/Star_player.git` → `checkout --detach c77ed1a…`. 두 Clone 모두 `HEAD` = `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f`, Tree `a7b9b937e2283fcdb0adb5d83114274940f0e71a`, 원격 URL 일치, 실행 전후 `status --porcelain --untracked-files=all --ignored` 0건(읽기 전용 확인, `GIT_OPTIONAL_LOCKS=0`).
- 출처 분리: 도구는 `c4a04bb` Blob을 증거 디렉터리(`g1fix\tool_new\`)로 내보내 실행했습니다. 대상 `c77ed1a`에는 이 도구가 없습니다(`tools/b1-ops/`에 `g1-names-check.ps1` 없음).
- 명령·종료 코드·시각: `g1fix\logs\ps51\commands.tsv`. 실행마다 로그 파일을 따로 두었고, 드라이버는 기존 로그를 덮어쓰지 않도록 거부합니다.
- 이전 증거(`phase3ops\policy\g1_names_check_*`)는 그대로 보존했습니다.

### 2.5 검출 범위와 한계 (정확한 기술)

- **검출함:** 고정 8개 파일의 모든 변경(내용 SHA), 그 밖의 생산 파일에서 이름 리터럴 또는 경로 기호를 가진 줄의 추가(목록 밖 파일), 13개 정의 줄의 변경, 이름 리터럴 개수 변화.
- **검출하지 못함:**
  - 고정 파일 **밖**의 새 파일에서 이름을 조각으로 조립하는 코드(`"manifest" + ".json"` 등) 또는 이름·기호가 없는 새 식별자로 같은 경로를 복제하는 코드(반례 `assembled_name_in_new_file`로 한계 확인).
  - 고정 파일 밖에서 이름·기호 없는 식별자를 거쳐 고정 파일로 들어오는 값의 흐름.
  - 시험 코드는 개수만 보고합니다(정보용).
- **보완:** 위 한계는 생산자 → Archive → 검증기 TEST_ONLY 종단 시험(`verify.ps1` U6 등)과 코드 검수가 다룹니다. 정적 검사 단독으로 전체 일치를 보장한다고 주장하지 않습니다.
- **엄격성의 대가:** 고정 8개 파일은 경로와 무관한 수정에도 FAIL합니다. 그 파일을 바꾸는 Commit은 이 도구의 고정값을 함께 갱신해야 하며, 그 갱신 자체가 검수 대상입니다. 공식 소스 Commit이 바뀌면 고정값을 새 Commit에서 다시 산출합니다.

## 3. G5-TEMP-01

- 상세는 [보호 모델](../tasks/B1_G5_TEMP01_PROTECTION_MODEL.md)입니다. 요약:
  - 운영 보호 모델: 보호 R-WORK 아래 `verifier-tmp`, 세션마다 새로 만든 TEMP, SID 전용·단일 세션(2절)
  - 강제 시작 경계: 보호 예약 작업(S4U 또는 gMSA) + 시작 스크립트의 S0~S9 단계(3절). **NOT_IMPLEMENTED**
  - 우회 분석: 계정 형태가 S4U 전용 로컬 사용자 또는 gMSA이고 조건이 실측되면 우회에 TCB 필요 → 충분. 암호를 아는 계정이면 불충분. 가상 계정은 서비스 호스트 코드 필요
  - [사실] 검증기의 C0/C1/C2 digest는 TMP 값도 ACL도 Profile과 대조하지 않으므로 보호를 증명하지 않습니다.
  - M-12A~J(4절), 코드 대안 C-1~C-4(5절, 권고 C-3), `c77ed1a` 유지 조건 6가지(6절)
- **운영 상태: OPEN_MAJOR 유지.** 운영 기계 M-12A~J 통과 전에는 공식 인증을 하지 않습니다. 개발 PC에서 보안 시험을 실행하지 않았고, 개발 PC 관찰을 운영 PASS로 쓰지 않습니다.
- 개발 PC 확인(진단): 설치된 .NET 8.0.31 `System.Private.CoreLib.dll`에 `GetTempPath2` 이름이 있습니다(문자열 검색). 실제 사용 여부·경로는 M-12A에서 측정합니다.

## 4. G2 E-1 ACCEPTABLE 판정에 대한 영향

- 이번 변경은 `tools/b1-ops/g1-names-check.ps1`과 문서뿐입니다. `c77ed1a..HEAD`에서 `src` `game` `tests` `content` `docs/contracts` `TASK.md` `.gitattributes` `tools/StarPlayer.SimHarness` `tools/StarPlayer.EvidenceVerifier` `global.json` `Directory.Build.props` 차이 = **0줄**.
- E-1 조건(`core.autocrlf=true` 최초 Checkout 전 적용, 승인 Commit·Tree, 정확한 원격 URL, 승인 입력 바이트 전체 SHA 대조, SDK·복원·CI 빌드 옵션, 독립 Clone 2개 비교, 불일치 시 중단)을 바꾸지 않았습니다.
- 기존 Checkout 설정 독립성 요구를 E-1이 충족하지 못한다는 기록, 301개 SourceLink 체크섬 차이와 원인(Raw의 LF→CRLF 변환, 설명 불가 0)을 그대로 둡니다.
- 수정 G1 도구는 Checkout 설정과 무관하게 같은 판정을 냅니다(CRLF·LF Clone 모두 PASS, 반례 `line_endings_flipped` PASS). G2 공식 PASS는 OWNER가 개정 G2 조건을 승인하기 전까지 선언하지 않습니다.

## 5. 공식 G1/G2/G3 승인에 남은 사항

| # | 항목 | 주체 |
|---|---|---|
| 1 | Codex 표적 재검수: G1-POLICY-01, G5-TEMP-01 운영 경로 충분성, G2 E-1 영향 없음 | Codex |
| 2 | PowerShell 7 실행 증거(현재 미검증) | OWNER 설치 허락 → Claude 실행 |
| 3 | OWNER 결정: 개정 G2(E-1) 수용 조건 최종 승인 | OWNER |
| 4 | OWNER 결정: G1-a 수용(수정 도구 기준) 또는 G1-b | OWNER |
| 5 | OWNER 결정: G5-TEMP-01 해결 방식(운영 경로 또는 코드 C-3 등). 코드 경로면 새 소스 Commit → 공식 소스 후보 변경 | OWNER |
| 6 | 공식 소스 Commit 확정(A1-6, `c77ed1a` 후보) | OWNER |
| 7 | 승인 B: 운영 기계·R-BUILD·배포 계정(G2/G3 장소), B-5 계정 형태(G5-TEMP-01 조건) | OWNER |
| 8 | 공식 G2/G3 실행: E-1 절차, 수정 G1 도구 PASS(운영 기계, 출처 분리), PCK 5회 → Codex 독립 검수 → OWNER 수용 | OWNER·Claude |
| 9 | 공식 인증(G4 공식 이후) 전: 시작 절차 구현·검수·설치, M-12A~J 통과 | OWNER·Claude·Codex |

## 6. 작업 기록과 한계

- 도구 수정 후 개발 실행은 첫 회에 PASS, `-SelfTest` 17/17이었습니다. 증거는 Commit `c4a04bb` 뒤 Blob을 내보내 다시 실행한 것입니다.
- 지배 줄 고정값은 Python(`-I -X utf8`)으로 산출했고, PowerShell 5.1 구현이 같은 값으로 PASS해 두 구현이 일치합니다. 첫 Python 산출 두 번은 콘솔 인코딩(cp949) 문제로 실패했습니다(작업 파일만, 증거 아님).
- PowerShell 7 실행은 하지 않았습니다(미설치).

## 7. 증거 해시 (SHA-256, 저장소 밖 `E:\Madegame\g3x\phase3ops\g1fix\`, 전체 목록 `SHA256SUMS_g1fix.txt`)

| 파일 | SHA-256 |
|---|---|
| `tool_new\g1-names-check.ps1`(= `c4a04bb` Blob) | `d3945b5ed51f0c49e39b2689bd73a8b14240ff9d13564745aca12f22eeb56ba0` |
| `tool_old\g1-names-check.ps1`(= `7bae96e` Blob) | `28f07574c480fd7588f63f5ce3ce46eebac4f6d87520237449adabab669d8fcd` |
| `redgreen.ps1` | `da9bb636dacc97825a93108a64302fd4d863474c6c4193f6e9a6cf5145fd5646` |
| `logs\ps51\redgreen_c77-crlf.log` | `3840bfea35c883d068db779cad5a3ee68dbca630f420a09a69756726c1a81d03` |
| `logs\ps51\new_check_c77-crlf.log` · `new_check_c77-lf.log` | `4aa85f74dccb90d1d3c00b4af7b382693ab207e6dc88870fae49a32d1dad57fa` · `f0e66e0d27cfacb00bab511957c29ce3ff83c86823521a5e143a45f881aea21a` |
| `logs\ps51\new_selftest_c77-crlf.log` = `new_selftest_c77-lf.log` | `474790ba751fe25668ec3d15d38bd1df8dbecb3f17cd665e6200715b9708f2cf` |
| `logs\ps51\old_check_c77-crlf.log` · `old_selftest_c77-crlf.log` | `b6805117c10afbf30a2a4fb65a49a57bbc9c50d70d9665be9aa594de28c0294f` · `08e584f5f73d6fd661a46c385d3c3828793d2959196987151674e62f34ff8c11` |
| `logs\ps51\commands.tsv` | `67d0f6cbe7795d0671d8b8e9dfe107e8936d67c2e2c4094c0b00106b7e939e02` |
