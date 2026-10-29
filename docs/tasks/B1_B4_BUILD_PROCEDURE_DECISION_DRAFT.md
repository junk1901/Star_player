# B-4 공식 빌드·출력·출처 정책 — OWNER 결정서 초안 (부분 수용 반영, 확정 아님)

- 작성: Claude Code, 2026-10-26. 근거: OWNER 지시 "B-4 공식 빌드·출력·출처 정책 결정 준비"(2026-10-26)
- **개정 2 (2026-10-27):** Codex READ-ONLY 검수 FIX_REQUIRED(B4-R01·B4-R02, 모두 MINOR)의 최소 정정입니다. EXE 일괄 제외 안을 철회하고(8.4·7.7절), 원격 Ref 해석을 정정했습니다(2·11절). Codex 판정 반영과 정정 전후 문장은 18절에 있습니다. A1~A11 부분 수용 범위는 바꾸지 않았습니다.
- **개정 1 (2026-10-26):** OWNER 지시 "B-4 부분 정책 수용 및 미해결 신뢰 경계 검토"를 반영했습니다. 0절에 수용 범위를 따로 기록하고, 실행 신뢰 경계 조사(8·9절), Profile·TRUST_CONFIG 대안(10절), 원격·공개 적합성(11절), 결과 분류(16절)를 추가했습니다. 개정 1에서 정정한 기존 기재는 17.2절에 적었습니다.
- **성격:**
  - OWNER 검토용 결정서 초안입니다. 일부 정책 **방향**만 부분 수용되었습니다(0절).
  - B-4 동결, 공식 Build Provenance 등록, G3 PASS, 운영 승인, G1~G9 실행이 **아닙니다**.
  - 승인 계약의 닫힌 Schema(§18.2·§18.3·§21)는 바꾸지 않습니다.
  - **미커밋 문서입니다.** Stage·Commit·Push·Tag는 하지 않았고, 별도 승인 대상입니다.
- **기준:**
  - Scene Migration 검증 소스 Commit `00091736d5386aec3f5c67cf159291976e7133a2`
  - Post-Commit 증거 Commit `96070c5fa1571b079df9c204597215db9da974ac`(문서 전용, 검증 소스가 아님)
  - 역사적 B1 Tag `b1-code-accepted-2026-10-26` → `7bdde7dd…`(유지)
- **근거 자료:**
  - 승인 계약 §15~§18·§20·§21·§23
  - [운영 Gate 계획](B1_OPERATIONAL_GATE_PLAN.md), [전체 빌드 진단](../reports/B1_G3_FULL_BUILD_DIAGNOSTIC_REPORT.md), [PCK 진단](../reports/B1_G3_GODOT_PCK_REPRO_REPORT.md)
  - [Commit 이후 검증](../reports/B1_G3_SCENE_MIGRATION_POSTCOMMIT_REPORT.md), [증거 색인](../reports/B1_SCENE_MIGRATION_EVIDENCE_INDEX.md)
  - 이 문서 작성 중 추가로 측정·조사한 결과(7·8·11·17절)
  - Codex 검수 요청: [B1_B4_CODEX_READONLY_REVIEW_REQUEST.md](B1_B4_CODEX_READONLY_REVIEW_REQUEST.md)
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · Artifact 집합 = **CANDIDATE_INCOMPLETE**

**분류 표기(각 항목에 붙임):**

| 표기 | 뜻 |
|---|---|
| **[사실]** | 실험·코드·계약으로 확인됨 |
| **[관찰: 개발 PC]** | 이 개발 PC에서 읽기 전용으로 본 값. 운영 기계 값이 아님 |
| **[문서 근거]** | Windows·.NET 공개 동작에 근거한 판단. 이 저장소에서 실측하지 않음 |
| **[수용]** | OWNER가 2026-10-26 정책 **방향**으로 부분 수용함. 동결·등록·PASS 아님 |
| **[후보]** | OWNER가 아직 정하지 않은 정책 후보 |
| **[실측]** | 운영 실측이나 추가 검증이 필요한 값 |
| **[충돌]** | 기존 계약과 충돌하거나 계약이 정하지 않아 임의로 결정할 수 없는 항목 |
| **NOT_IMPLEMENTED** | 필요한 코드·명령이 없음. 이번 작업에서 구현하지 않음 |
| **OWNER_VALUE_PENDING** | 운영 값이 아직 없음 |

## 0. OWNER 부분 수용 결정 (2026-10-26)

이 절은 OWNER 결정만 적습니다. 근거가 되는 사실은 1절 이후에 따로 둡니다.

**부분 수용된 정책 방향 [수용]:**

| # | 정책 방향 | 이 초안의 위치 |
|---:|---|---|
| A1 | 승인 소스의 Clean Checkout 빌드 | 1절 2, 4절 1단계 |
| A2 | .NET SDK `8.0.425` | 4절 3단계 |
| A3 | Release Configuration | 4절 6단계 |
| A4 | Deterministic Build | 4절 7단계 |
| A5 | `ContinuousIntegrationBuild=true`를 빌드 명령에서 전달 | 1절 1, 4절 6·8단계 |
| A6 | SourceLink 유지, 실제 `source_git_sha`와 대조 | 1절 3, 4절 9단계 |
| A7 | 로컬 경로 Git Remote를 공식 SourceLink 출처로 인정하지 않음 | 1절 4 |
| A8 | PDB를 별도 진단 증거로 보존 | 1절 5 |
| A9 | Coverage Mapping 파일 6개를 공식 Artifact 후보에서 제외 | 1절 6 |
| A10 | Godot PCK 전체 바이트 재현성 요구 | 1절 7, 5절 |
| A11 | 기존 `source_git_sha`와 승인 기록을 소급 변경하지 않음 | 1절 8 |

**이 수용으로 생기지 않는 것:** 공식 B-4 동결, G3 PASS, Build Provenance 등록, 운영 승인, Clause ID 등록, Remote Push 승인.

**수용 범위 밖으로 남은 것(이 초안에서 계속 [후보]·[충돌]·OWNER_VALUE_PENDING):**

- `PathMap` 대안(1절 1의 대안 부분), NuGet 출처 정책
- 공식 SourceLink 원격의 확정과 원격 Commit 검증(11절), Push
- 공식 실행 방식(8절, OWNER 우선 후보만 지정됨), Apphost 포함 여부
- Artifact 집합(109개 목록 미확정, CANDIDATE_INCOMPLETE, 7절)
- Verifier의 SimHarness DLL 사본 결속(9절)
- Profile Archive 역할, TRUST_CONFIG `relative_path` 루트(10절)
- B-4 파일명·상대경로 표(6절, 후보 유지), 운영 출력 Root·계정·SID·ACL
- Clause ID `starplayer.b4.dotnet-build.v1`·`starplayer.b4.godot-export.v1`(12절, 후보 유지·등록 보류)

**OWNER가 지정한 공식 실행 방식 우선 후보 [후보]:** 승인된 절대경로의 `dotnet.exe`와 승인된 DLL 경로로 실행합니다. PATH에서 고른 `dotnet.exe`는 그대로 신뢰하지 않습니다(8절).

## 1. 정책 후보와 부분 수용 상태

| # | 정책 | 근거 | 분류 |
|---:|---|---|---|
| 1 | .NET 빌드는 `ContinuousIntegrationBuild=true`(명령줄 전달)를 쓴다. `Directory.Build.props`·프로젝트 파일은 바꾸지 않는다. `PathMap`은 대안으로만 남긴다 | [사실] 13개 프로젝트, 경로 4개(`7bdde7d`)와 경로 2개(`00091736`)에서 DLL·PDB 동일 | CI 속성 [수용] A5 / PathMap [후보] |
| 2 | 빌드는 승인 Commit의 깨끗한 체크아웃에서만 하고, 미커밋 작업 트리 빌드는 공식 출처로 쓰지 않는다 | [사실] 원본 작업 트리 빌드의 SourceLink가 미커밋 소스와 다른 Commit(`257403d`)을 가리킨 사례 | [수용] A1 |
| 3 | SourceLink는 유지하고, SourceLink Commit = 실제 빌드 Commit = `source_git_sha`를 빌드 후 검사한다 | [사실] 55개 어셈블리 모두 실제 Commit 기록. Commit이 다르면 PDB·DLL 식별자만 다름(출처 차이) | [수용] A6. 원격 검증은 미완료(11절) |
| 4 | 로컬 파일 경로 원격은 정식 원격으로 인정하지 않는다 | [사실] 로컬 경로 원격이면 SourceLink 자체가 생성되지 않음 | [수용] A7 |
| 5 | PDB는 §18.3 역할에 넣지 않고, 별도 진단 증거로 바이트·SHA를 보존한다 | 닫힌 Schema 유지 | [수용] A8. 보존 위치·형식은 [후보] |
| 6 | Coverage 대응표(`.msCoverageSourceRootsMapping_*`) 6개는 Artifact 후보에서 제외하고, 목록·원인·SHA를 기록한다 | [사실] 빌드 절대 경로와 NuGet 캐시 경로를 담아 경로마다 다름(어셈블리 아님) | [수용] A9 |
| 7 | Godot PCK는 같은 승인 입력·통제된 Export 환경에서 PCK 전체 바이트 동일을 요구하고, `node_ids` 제외 비교와 사후 패치를 금지한다. Build Provenance에 새 Artifact 역할을 추가하지 않는 별도 Export 정책으로 둔다 | [사실] Scene Migration 후 5회 동일(`3a187e19…`) | 전체 바이트 요구 [수용] A10 / 별도 조항 [후보] |
| 8 | 기존 승인 기록의 `source_git_sha`는 소급 변경하지 않는다. 새 공식 빌드는 실제 빌드 Commit으로 새로 결속한다 | §18.3 결속 사슬, OWNER 지시 | [수용] A11 |
| 9 | `.tscn`은 LF(`*.tscn text eol=lf`)로 둔다 | Scene Migration Commit에 반영·검증됨 | [사실](적용 완료) |

## 2. 확정되지 않은 정책과 이유

| 항목 | 이유 | 분류 |
|---|---|---|
| 공식 SourceLink URL·원격 Commit 검증 | 광고된 원격 ref는 `main` → `7456f44` 하나이고, `00091736`·`96070c5`·`7bdde7d`를 가리키는 광고된 ref는 확인되지 않음. Commit 객체·Raw URL 조회 가능성은 미검증(11.2절). 원격 URL은 PDB·DLL 바이트에 영향 | OWNER_VALUE_PENDING |
| 공개 저장소 게시 적합성 | 공개 원격에 Push하면 OWNER·Codex 원문 35개가 새로 공개되고, 승인 SHA 때문에 이력에서 지울 수 없음(11.3절) | OWNER 판단 필요 |
| 공식 Archive 루트(Windows 절대 경로)·보안 계정·SID·ACL | 운영 기계·계정 미정(G5, §17) | [실측], OWNER_VALUE_PENDING |
| 공식 실행 명령(SimHarness·Verifier) | Verifier CLI는 `environment`만 있고 인증 명령이 없음. SimHarness `official*`는 NOT_READY로 거부(8.1절) | NOT_IMPLEMENTED, [충돌] |
| 실행할 `dotnet.exe`의 승인 경로·SHA를 어디에 고정할지 | §18.2는 `runtime_files` 3개만 정함. `dotnet.exe`·`hostfxr`·`hostpolicy`는 닫힌 Schema 밖(8.2절) | [충돌] |
| Apphost 포함 여부 | 실행 명령이 없어 판단 근거가 없음. 닫힌 역할에서 apphost를 어디에 둘지 계약이 정하지 않음(8.4절) | [충돌] |
| Artifact 집합 | `deps.json` 밖의 파일(xunit 어댑터, `testhost.exe`, SDK 쪽 VSTest 파일 등)이 실행에 쓰일 가능성. 로드 추적 미실시(7절) | CANDIDATE_INCOMPLETE, [실측] |
| Verifier 폴더 SimHarness 사본 결속 | 검증기가 재생·Schema·내장 입력을 이 사본에서 실행하지만 실행 중 바이트 결속이 없음(9절) | [후보], NOT_IMPLEMENTED |
| TRUST_CONFIG `relative_path` 루트 | §18.3은 RELPATH만 정하고 루트를 정하지 않음. 코드는 SHA만 대조(10.2절) | [충돌] |
| ENVIRONMENT_PROFILE·PZL_TEST_PROFILE의 Archive 역할 | 생산 검증기(R2-A)는 두 원문 SHA를 INPUT_BUNDLE ORIGINAL로 요구함. 이 해석을 공식으로 할지 미정(10.1절) | 계약 해석 필요 |
| Environment Profile·N12 Golden | 승인 실행 환경에서 실측해야 함(13절) | [실측] |
| `build_procedure_clause_id` | 위 항목이 정해져야 조항 내용이 고정됨(12절) | 후보 유지, 등록 보류 |

## 3. 추천 정책과 대안

| 주제 | 추천 | 대안 | 추천 이유 |
|---|---|---|---|
| 경로 정규화 | `-p:ContinuousIntegrationBuild=true` ([수용]) | `-p:PathMap=<checkout root>\=/_/` | 결과 바이트가 같으면서 SourceLink 키까지 `/_/*`로 정규화됨. PathMap은 루트 경로를 정확히 넣어야 함 |
| 속성 전달 | 공식 빌드 스크립트의 명령줄 ([수용]) | `Directory.Build.props`에 영구 설정 | 프로젝트 파일 변경 없이 B-4 조항으로 고정 가능. 영구 설정은 소스 변경과 새 Commit이 필요 |
| SourceLink 원격 | 11.4절 선택지 중 OWNER 결정. 원격 Commit 조회 확인 뒤에만 "원격 출처 확인 완료" | 원격 게시 없이 SourceLink Commit = `source_git_sha`만 검사 | 원격 조회가 안 되면 원격 출처 확인 완료로 볼 수 없음 |
| 실행 방식 | **승인 절대경로 `dotnet.exe` + 승인 DLL 경로**(OWNER 우선 후보, 8절) | Apphost `.exe` | DLL Pin으로 실행 코드를 정할 수 있고 닫힌 역할과 충돌이 없음. Apphost는 DLL 바이트를 결속하지 않음(8.4절) |
| 자식 프로세스 실행 파일 | 부모와 같은 승인 `dotnet.exe` 절대경로, 고정 작업 디렉터리 | 현재 코드(`ProcessStartInfo("dotnet")`) | 현재 코드는 Windows 프로세스 검색 순서로 실행 파일을 고름(8.2절) |
| Artifact 범위 | 실행·검증에서 실제로 여는 파일(로드 추적으로 확정) | `deps.json` 기준 109개 그대로 | `deps.json`이 xunit 어댑터 등을 빠뜨림(7.3·7.4절) |
| Verifier SimHarness 사본 | 정적 동일성 규칙 + 실행 중 로드 목록 결속 + ACL(9절 V-1~V-3 병행) | 정적 규칙만 | 정적 규칙만으로는 실제 로드된 파일을 증명하지 못함 |
| Godot PCK | 별도 Export 조항(Provenance 밖, 새 Artifact 역할 없음) | DEPENDENCY에 포함 | OWNER 지시. PCK는 SimHarness 증거 경로가 아님 |

## 4. 공식 .NET 빌드 절차 초안 (B-4 후보)

- **상태:** A1~A6·A11 방향은 [수용]입니다. 아래 단계표 자체는 동결되지 않은 [후보]입니다.

| # | 단계 | 명령·옵션 | 사전 조건 | 실패 시 중단 조건 | 검증 방법 |
|---:|---|---|---|---|---|
| 1 | 승인 Commit의 깨끗한 체크아웃 | `git clone --no-hardlinks <승인 원격>` → `git checkout --detach <source_git_sha>` | 승인 기록의 `git_commit_sha` 확정, 원격 URL 승인(11절, 미정) | HEAD ≠ 승인 SHA, `git status --porcelain --untracked-files=all` ≠ 0행 | 두 명령 출력 기록 |
| 2 | 소스·입력 바이트 | `.gitattributes` 규칙(G2)대로 체크아웃. 입력: `content/tuning/virtual_tuning_v4.json`·`content/rules/game_rules_profiles_v2.json`·`Directory.Build.props`·`global.json`·Scene 4개 | G2 정책 확정 | 입력 SHA ≠ 승인값, `git ls-files --eol` 불일치 | SHA 목록 대조 |
| 3 | SDK | 승인 절대경로 `dotnet.exe`로 `--version` = `8.0.425`(`global.json`, `rollForward: disable`) | 승인 기계에 SDK 설치, 실행 경로 승인(8절) | 버전 불일치 | 출력 기록 |
| 4 | Restore 의존성 | `dotnet restore StarPlayer.sln --disable-build-servers`(또는 build의 암묵 restore) | NuGet 출처 정책(OWNER_VALUE_PENDING) | 패키지 버전·해시 불일치 | `project.assets.json` 라이브러리 목록과 버전 대조(현재 패키지 22개, [PCK 진단](../reports/B1_G3_GODOT_PCK_REPRO_REPORT.md) 13절) |
| 5 | `bin/obj` 초기화 | 모든 `bin/`·`obj/` 삭제(Godot은 `game/.godot/`도) | — | 삭제 실패 | 삭제 후 존재 0 확인 |
| 6 | Release 빌드 | `dotnet build StarPlayer.sln -c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false -p:ContinuousIntegrationBuild=true` | 환경 변수 `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `UseSharedCompilation=false` | exit ≠ 0, 경고 > 0(`TreatWarningsAsErrors`) | 로그 보존 |
| 7 | `Deterministic=true` | 기존 `Directory.Build.props`(변경 없음) | — | 출력 DLL에 `Reproducible` Debug 항목 없음 | PE Debug Directory 검사 |
| 8 | `ContinuousIntegrationBuild=true` | 6단계 명령에 포함 | — | 6단계 명령 불일치 | 명령 기록 |
| 9 | SourceLink URL·Commit | 모든 StarPlayer PDB의 SourceLink JSON | 1단계 원격 = 공식 URL | 키 ≠ `/_/*`, URL ≠ 공식, Commit ≠ `source_git_sha`, SourceLink 없음 | PDB CustomDebugInformation 판독 |
| 10 | 경로 정규화 | 모든 StarPlayer DLL CodeView 경로와 PDB 문서 경로 | — | `/_/`로 시작하지 않는 경로가 있음 | PE·PDB 판독 |
| 11 | 두 독립 폴더 비교 | 경로가 다른 두 번째 깨끗한 체크아웃에서 1~10 반복 | — | Artifact 파일 하나라도 SHA·길이 다름, 누락·추가 | 파일별 SHA 비교(coverage 대응표는 비교 대상 밖이지만 목록은 기록) |
| 12 | 실제 사용 파일의 Raw SHA·길이 | 확정된 Artifact 목록(7절)의 `raw_sha256`·`byte_length` | Artifact 목록 확정(현재 CANDIDATE_INCOMPLETE) | 목록 파일 누락 | `relative_path` ordinal 정렬·중복 없음 확인 |
| 13 | Build Provenance·승인 기록 결속 | §18.3 문서 작성 → OWNER Registry 등록(BUILD_PROVENANCE) → 승인 기록 결속 | 12단계, Environment Profile 등록 | `ApprovalRecord.verifiers[].binary_sha256` ∉ VERIFIER_BINARY, `source_git_sha` ≠ 승인 기록 | 검증기 E2-3·T5 |

- **중단 원칙:** 어느 단계든 실패하면 산출물을 공식 출처로 쓰지 않습니다(§23 B-4 Fail-Closed).
- **원격 선행 조건 [사실]:** SourceLink URL은 PDB에 기록되고, PDB 식별자는 DLL의 CodeView 항목에 들어갑니다. 원격 URL을 바꾸면 PDB·DLL 바이트가 바뀝니다. 그래서 공식 원격(11절)은 공식 빌드 전에 정해야 합니다.
- **이번 진단과의 차이:** 진단은 SimHarness·전체 솔루션 빌드를 직접 실행해 1~11단계에 해당하는 사실을 확인했습니다. 12~13단계(공식 목록 확정·등록)는 하지 않았습니다.

## 5. Godot Export 절차 초안 (Provenance 밖, 별도 정책 후보)

| 항목 | 초안 |
|---|---|
| 정책 성격 | 기존 Build Provenance Schema에 새 Artifact 역할을 추가하지 않는 별도 Godot Export 정책. 조항 ID `starplayer.b4.godot-export.v1`은 후보, 등록 보류 |
| 도구 | Godot `4.7.2.stable.mono.official.ed1daf0bf`, Export Template 5개(verify T3 SHA) |
| 명령 | `godot --headless --path game --build-solutions --quit` 후 `godot --headless --path game --export-release "Windows Desktop" ../build/export/StarPlayer.exe`, 환경 변수 `ContinuousIntegrationBuild=true` |
| 사전 조건 | 깨끗한 체크아웃, `game/.godot/`·`build/export/` 없음 |
| 수용 기준 | 같은 경로 2회·다른 경로 2회·Import Cache 유지 1회의 `StarPlayer.pck` 전체 바이트 동일([수용] A10), 나머지 Export 파일 개수·SHA 동일, verify G2~G4·E2~E4 PASS |
| 금지 | `node_ids` 제외 비교, 사후 패치 |
| 상태 | [사실] `00091736`에서 충족(`3a187e19…`). 운영 배포 산출물 동결은 아님 |

## 6. B-4 파일명·경로 전체 표 (§21 역할, 후보 유지)

- **상태:** 이 표의 파일명·상대경로는 모두 **[후보]**입니다. §21은 "파일명과 실제 출력 루트는 B-4에서 정합니다"라고 하므로 B-4가 정할 대상이지만, 아직 정하지 않았습니다.
- **OWNER_VALUE_PENDING:** 운영 Windows 출력 Root(Archive 루트 절대 경로), 생산자·검증기 계정, SID, ACL.

**경로 층의 구분([사실], 코드):**

| 층 | 무엇 | 현재 | 공식 여부 |
|---|---|---|---|
| 공식 출력 Root | Archive 루트의 Windows 절대 경로 | 없음 | OWNER_VALUE_PENDING |
| Archive 내부 상대 경로 | 아래 표의 `RELPATH` | 코드·TEST_ONLY 시험의 배치 | [후보] |
| `run/<run_id>/…` | OWNER 결정 3 §6의 **내부 Archive 배치** | 생산자·검증기·시험이 함께 씀 | 내부 구조. 공식 출력 Root와 별개이며 공식 동결 아님 |
| 생산자 결과 폴더 | `build/simharness/official/<run_id>`(저장소 상대, git-ignored, 새 폴더만) | SimHarness CLI 기본값 | Archive와 별개. 공식 여부 미정 |

**"현재 구현" 경로의 출처:**

- `EvidenceWriter`·`OfficialOutput`·`Certification`·`Receipts`의 실제 코드입니다.
- Family·Dominance·PZL6 문서와 `trust/…` 경로는 **생산 코드에 고정된 이름이 없고**, TEST_ONLY 시험이 쓰는 이름입니다.
- 모든 경로는 **Archive 루트 기준 `RELPATH`**입니다.

**공통 규칙([사실], 코드):**

- `Archive.WriteNew`는 기존 파일을 덮어쓰지 않습니다(`Archive.exists`).
- `..`·reparse point를 거부하고, 쓴 바이트를 다시 읽어 확인합니다.
- `artifact_id`는 경로에서 확장자를 뺀 값입니다.
- Registry는 갱신·삭제를 막습니다(§15 트리거).

**공통 열 값:**

- **신뢰 루트:** Trust Anchor(T1) → Registry(T3) → 승인 기록·외부 Pin. Archive 파일은 상위 문서 ArtifactRef의 Raw SHA를 거쳐 영수증 `source_files`와 Registry까지 이어집니다.
- **쓰기 권한:** Archive 디렉터리 ACL은 G5·NSC-4 범위이며 **OWNER_VALUE_PENDING**입니다. Registry 쓰기는 서비스 Pipe를 통해서만 가능하고, 역할별 Register 권한은 R03-5-3입니다.
- **검증 주체:** 모든 행에서 `OfficialExecution` 모드 EvidenceVerifier입니다(E2-6). Registry 재조회는 §20.2 7단계입니다.
- **작성 주체:** 생산자(증거 작성자, 비신뢰 표준 사용자)는 SimHarness 증거 Writer를 씁니다. 검증기는 VERIFIER_SERVICE 계정입니다.

| 역할(§21) | 공식 역할 | 파일명 후보 | 상대경로 후보(현재 구현 → 제안) | 작성 주체 | Raw SHA 검증 시점 | 불변·재작성 금지 | 선행 Artifact |
|---|---|---|---|---|---|---|---|
| 승인 자료 `APPROVAL_RECORD` | 검증 집합의 승인 버전 1 | `approval-record.json` | 생산 코드 없음(TEST_ONLY `trust/approval-record.json`) → 제안 `trust/approval-record.json`(Registry 원문의 사본) | OWNER(Registry `APPROVAL_RECORD` 등록) | 실행 전·검증 시작(외부 Pin 필수) | Registry 불변, Archive 사본 덮어쓰기 금지 | Trust Anchor, Registry |
| `BUILD_PROVENANCE` | 승인 build ID별 1세트 | `build-provenance.json` | TEST_ONLY `trust/build-provenance.json` → 제안 같음 | OWNER(Registry `BUILD_PROVENANCE`) | 실행 전·독립 검증(T5) | 같음 | 4절 빌드 출력, Environment Profile |
| `INPUT_BUNDLE` | 소비 입력 파일별 | 저장소 경로 그대로 | `inputs/<저장소 상대 경로>`. Environment Profile·PZL Test Profile 원문은 생산 검증기가 SHA를 요구하고(R2-A), 파일명은 TEST_ONLY `trust/environment-profile.json`·`trust/pzl-test-profile.json`(10.1절) | 생산자 | build/run 전·검증 | 덮어쓰기 금지, 입력 바이트 = 승인 입력 | Commit 입력 파일 |
| `SEED_MANIFEST` | 승인 Unit별 정확히 1 | 승인 Seed 경로 그대로 | `inputs/<seed_manifest_path>`(생산 코드 `WriteSeeds`) | 생산자(승인 Seed 바이트 복사) | 실행 전·블록 전·독립 검증. `raw_sha256` = `attestation.seed_manifest_sha256` = 승인 기록 Pin | 같음 | 승인 기록 Pin |
| Family Preflight `PREFLIGHT`(unit_id null) | Family 기록 | `preflight.json` | `run/<run_id>/preflight.json`(R2-B 내부 배치) | 생산자 | 첫 경기 전 | 같음 | 승인 기록, 입력 |
| Unit Preflight `PREFLIGHT`(unit_id) | Unit별 기록 | `<unit_id 소문자>.json` | `run/<run_id>/preflight/<unit>.json` | 생산자 | 첫 경기 전 | 같음 | Family Preflight |
| `UNIT_MANIFEST`(+`ATTESTATION` 내부) | Unit별 1 | `manifest.json` | `run/<run_id>/<units\|cells>/<id>/manifest.json` | 생산자 | Unit 종료 | 같음, Replay·Attestation·영수증이 같은 스냅샷 바이트 | Preflight, Seed |
| 실행 색인 `DIAGNOSTICS` | 실행 DRAFT 색인 1 | 결과 파일명(`family-result.json`·`dominance-result.json`) | `run/<run_id>/<결과 파일>` | 생산자 | 실행 시 | 같음 | Unit Manifest |
| `GAME_RESULTS` | Unit별 경기 bundle 1 | `game-results.jsonl` | `units/<unit 소문자>/game-results.jsonl` | 생산자 | Unit 종료 | 같음 | Unit 실행 |
| `GAME_EVENTS` | Unit PZL마다 정확히 1 | `game-events.jsonl` | `units/<unit 소문자>/game-events.jsonl` | 생산자 | Commit 후 수집·종료 후 봉인 | 같음 | Unit 실행 |
| `UNIT_PZL` | Unit별 1 | `unit-pzl.json` | `units/<unit 소문자>/unit-pzl.json` | 생산자 | 관찰 후 | 같음 | GAME_EVENTS·GAME_RESULTS(R-13) |
| `PZL6` | Integration 1 + 20경기 JSON | 문서 `pzl6-<stem>.json`, 표본 `<unit>-NN.json` | 생산 코드 없음(TEST_ONLY `pzl6-<stem>.json`·`pzl6/<stem>/<unit>-NN.json`) → 제안 같음 | 생산자 | 고정 표본 검사 후 | 같음 | Unit PZL·번들 |
| `FAMILY_RESULT` | Calibration·Integration 각 1 | 제안 `calibration-result.json`·`integration-result.json` | 생산 코드 없음(TEST_ONLY `integration-result.json` 등, 이름은 호출자 지정) → 제안 Archive 루트 바로 아래 | 생산자 | 전체 Unit 후 | 같음 | Unit PZL·PZL6(R-13) |
| `DOMINANCE_RESULT` | 1 | `dominance-result.json` | TEST_ONLY `dominance-result.json` → 제안 같음 | 생산자 | 48 Unit 후 | 같음 | Unit PZL |
| `REGRESSION_PROOF` | 승인 검사 프로파일별 1세트 | 제안 `regression-proof.json` | TEST_ONLY `trust/regression-proof-<guid>.json` → 제안 `proof/<pzl_test_profile_id>.json` | 생산자(시험 결과) | 필수 검사 시 | 같음 | TEST_ASSEMBLY 실행 |
| `RAW_INVENTORY` | 검증 스냅샷별 1 | `raw-inventory.json` | `verifier/<receipt_id>/raw-inventory.json`(생산 코드 `Certification`) | 검증기 | 원본 고정 후(자기 자신 제외) | 같음, 자기 참조 금지 | 모든 원본 |
| `CANONICAL_PAYLOAD` | 발행 Family별 1·Phase 1 | `canonical-payload.json` | `verifier/<receipt_id>/canonical-payload.json` | 검증기 | 검증 성공 후 | 같음 | Family·Dominance, Inventory |
| `VERIFICATION_RECEIPT` | 발행별 1 | `<receipt_id>.json` | `receipts/<receipt_id>.json`(생산 코드 `Receipts.Issue`) | 검증기(VERIFIER_SERVICE) | 등록 전(E2-7 후 영속 저장, 재읽기·SHA) | 같은 ID는 같은 바이트만(다르면 conflict) | Canonical Payload, Inventory |
| `REGISTRY_RECORD` | 영수증별 1 | 파일 아님(Registry 행) | §15 `registry_entry`(kind `VERIFICATION_RECEIPT`) | Registry 서비스(VERIFIER_SERVICE 등록) | 원자적 등록(§20.2 6·7) | INSERT만, UPDATE·DELETE 트리거 거부 | 영수증 |
| `PREPARED_OBSERVATIONS` | RESERVED / UNUSED | — | — | — | — | — | — |

- **`run/<run_id>` 호환성[사실]:**
  - 위 `run/<run_id>/…` 행은 현재 생산자·검증기·시험이 함께 쓰는 배치입니다.
  - 공식 경로를 바꾸면 `EvidenceWriter.RunPath`·`OfficialOutput`·검증기 `RunFile`·시험이 같은 정의를 써야 합니다(G1 수용 기준: 이름 정의 한 곳, 기존 Hash·Replay 불변).

## 7. Artifact 109개 후보 검증 결과 (`00091736` CI 빌드 2개) — CANDIDATE_INCOMPLETE

- **상태:** 109개 파일 목록은 **확정하지 않습니다**(OWNER 지시). 실제 로드 여부를 확인하기 전에는 파일 수를 정하지 않습니다.
- **측정:**
  - clone `E:\Madegame\g3x\nci`(N_S1)와 `E:\Madegame\g3x\nci2long`(N_S2, 경로 길이 다름)에서 같은 Commit을 CI 빌드했습니다(원격 실험 통제값 GitHub URL).
  - 두 빌드 793개 파일 중 787개가 같고, 다른 6개는 coverage 대응표입니다.
  - 후보 생성·검사 스크립트 `E:\Madegame\g3x\tools\roles_check_n.py`(`7360813f…`), 결과 `E:\Madegame\g3x\roles_candidates_00091736.json`(`1abb0741…`)

### 7.1 검사 결과

| 검사 | 결과 |
|---|---|
| 역할별 수 | SIMHARNESS_BINARY 1, VERIFIER_BINARY 1, TEST_ASSEMBLY 4, DEPENDENCY 91, RUNTIME_CONFIG 12(= 109) [사실, `deps.json` 기준] |
| 빌드 출력에 없음 | 0 [사실] |
| N_S1 = N_S2 SHA | 109/109 [사실] |
| `RELPATH` 문법(`/` 구분, `..`·`:` 없음) | 정상. 루트는 체크아웃 루트(`Certification.RunTestProfile`이 `buildRoot`와 결합) [사실] |
| 경로 중복 | 0. 경로당 역할 1개 [사실] |
| ordinal 오름차순 | 결과 파일에 정렬해 기록 [사실] |

### 7.2 역할별 실제 기능

| 역할 | 파일(`00091736`) | 실행·검증에서 하는 일 |
|---|---|---|
| SIMHARNESS_BINARY | `tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.dll`(906,752 B, `a085d613…`) | 생산자의 공식 실행 엔진(실행 명령 미정) |
| VERIFIER_BINARY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.EvidenceVerifier.dll`(218,112 B, `04a2c71c…`) | 공식 인증 검증 프로세스. `ApprovalRecord.verifiers[].binary_sha256`과 결속 |
| TEST_ASSEMBLY | `tests/StarPlayer.{Domain,Simulation,Infrastructure,SimHarness}.Tests/bin/Release/net8.0/…Tests.dll`(`3197fb82…`·`65972b1b…`·`e8a006e3…`·`926dbf44…`) | 검증기가 SHA 확인 후 자식 `dotnet test <dll>`로 Test Profile Case 실행(§12.3) |
| DEPENDENCY | 위 6개 소비자 폴더의 `deps.json` runtime·native(win) 파일 91개 | StarPlayer 라이브러리 사본, SQLite(검증기·Infra 시험), xunit·TestPlatform·Newtonsoft.Json(시험) |
| RUNTIME_CONFIG | 6개 소비자의 `*.runtimeconfig.json`·`*.deps.json` 12개 | 런타임 프레임워크 선택·의존성 해석 |

**같은 바이트가 여러 경로에 있는 파일[사실]:**

| 파일 | 경로 수 | 역할 | 비고 |
|---|---:|---|---|
| `StarPlayer.SimHarness.dll` | 4 | SIMHARNESS_BINARY 1 + DEPENDENCY 3 | 검증기 폴더 사본은 9절 |
| `StarPlayer.EvidenceVerifier.dll` | 2 | VERIFIER_BINARY 1 + Infra 시험 폴더 DEPENDENCY 1 | |
| `StarPlayer.SimHarness.Tests.dll` | 2 | TEST_ASSEMBLY 1 + Infra 시험 폴더 DEPENDENCY 1 | |
| `StarPlayer.Domain.dll` | 6 | DEPENDENCY | |
| `StarPlayer.Simulation.dll` | 5 | DEPENDENCY | |
| runtimeconfig 2종 | 4 / 2 | RUNTIME_CONFIG | |

### 7.3 `deps.json` 밖의 파일 (폐쇄성 검토, [실측])

| 소비자 폴더 | 폴더 파일 | 후보 | 후보 밖 | 후보 밖 내용 | 판정 |
|---|---:|---:|---:|---|---|
| SimHarness | 9 | 5 | 4 | PDB 3, `StarPlayer.SimHarness.exe` | PDB는 진단 증거, exe는 8.4절 |
| EvidenceVerifier | 44 | 15 | 29 | PDB 6, exe 2(`EvidenceVerifier.exe`·`SimHarness.exe`), `StarPlayer.SimHarness.deps.json`·`.runtimeconfig.json`(참조 Exe 프로젝트 사본), 비Windows SQLite native 19 | 검증기 실행에는 자기 deps·runtimeconfig만 쓰는 것으로 보이나 로드 추적 필요 |
| 시험 4개 폴더(각각) | 90~140 | 18~32 | 72~108 | **`xunit.runner.visualstudio.testadapter.dll`·`xunit.runner.utility.netcoreapp10.dll`·`xunit.runner.reporters.netcoreapp10.dll`**, `testhost.exe`, 위성 리소스 65개(`<culture>/…resources.dll`), coverage 대응표 1, PDB, (Infra) 참조 프로젝트 사본·비Windows native | **누락 후보:** xunit 어댑터는 `dotnet test`가 시험 발견에 쓰는 파일이라 실제로 로드될 가능성이 큼(폴더당 3개, 총 12). `testhost.exe`와 위성 리소스(UI 문화권에 따라 로드)도 사용 여부 실측 필요 |

- **개정 1 확인[사실]:** `tests/StarPlayer.Domain.Tests/bin/Release/net8.0`(N_S1) 37개 항목 중 후보 18개에는 `testhost.dll`이 있고, `testhost.exe`·xunit runner 3개·PDB 2개·문화권 폴더 13개·coverage 대응표는 후보 밖입니다.
- **결론:**
  - 109개 후보는 `deps.json` 기준으로는 정확합니다.
  - 그러나 **실제 소비 파일의 완전한 집합이라고 확정할 수 없습니다**. 최소한 xunit 어댑터 12개가 빠졌을 가능성이 큽니다.
- **과다 포함 후보:** 검증기·Infra 폴더의 `runtimes/win-arm64`·`win-x86` `e_sqlite3.dll`(win-x64 프로세스에서는 로드되지 않을 것으로 보이나 로드 추적 전이라 미확인. Codex F-19 NOT_CONFIRMED). 포함 여부는 OWNER 결정입니다.

### 7.4 `deps.json` 분석과 실제 로드 추적의 차이

- **[사실]** 실제 파일 로드 추적은 **실시하지 않았습니다**. 아래 "로드 여부" 열은 모두 추정이며 확인된 값이 아닙니다.

| 분류 | 예 | `deps.json` 분석 | 실제 로드 여부(추정) | 109개 후보 |
|---|---|---|---|---|
| ① 기록되고 로드될 파일 | StarPlayer DLL, xunit core, SQLite win-x64 native, runtimeconfig·deps | 있음 | 높음. 단 SQLite는 Registry·DB 경로를 탈 때만 로드되는 지연 로드 | 포함 |
| ② 기록됐지만 로드되지 않을 수 있는 파일 | win-arm64·win-x86 native, `Microsoft.VisualStudio.CodeCoverage.Shim.dll`, 쓰지 않는 패키지 어셈블리 | 있음 | 불확실(아키텍처·코드 경로 의존) | 포함 |
| ③ 기록되지 않았지만 로드될 수 있는 빌드 출력 파일 | xunit 어댑터 3종 × 4, `testhost.exe`, 위성 리소스 | 없음 | xunit 어댑터는 높음. `testhost.exe`는 VSTest가 고르는 호스트 방식에 따름. 리소스는 UI 문화권·예외 메시지 경로에 따름 | 제외 |
| ④ 빌드 출력 밖에서 로드될 파일 | `dotnet.exe`, `host/fxr/<ver>/hostfxr.dll`, `hostpolicy.dll`, 공유 런타임, SDK의 `vstest.console.dll`·`testhost.dll`·TRX Logger, System32 DLL | 해당 없음 | 높음 | 대상 아님(§18.3 역할 밖). §18.2는 `runtime_files` 3개만 |
| ⑤ 작업 중 생성·소비 파일 | TRX 결과 파일, 임시 파일 | 해당 없음 | TRX는 코드상 부모가 읽음(판정 입력) | 대상 아님 |

- **분류 상태(개정 2):** "로드 여부(추정)" 열은 판단 근거일 뿐 분류가 아닙니다. 모든 행의 분류 상태는 PENDING이며, 실제 로드 추적 없이 LOAD_CONFIRMED·UNUSED로 분류하지 않습니다.
- **차이의 의미:** ③이 Pin되지 않으면 실행 파일 집합이 승인되지 않은 채 남고, ②가 포함되면 쓰지 않는 파일까지 고정합니다. ④·⑤는 Build Provenance가 아닌 Environment Profile·실행 절차·ACL로 다뤄야 하는지 계약 해석이 필요합니다(8.5·16.3절).

### 7.5 진단용 로드 추적 절차 제안 (개발 PC, 비공식, 미실시)

OWNER 승인 전에는 실행하지 않습니다. 결과는 진단 증거이며 Artifact 집합을 확정하지 않습니다.

1. **빌드:** 4절 1~11단계로 별도 진단 폴더에 깨끗한 CI 빌드를 만듭니다. 빌드 Commit·폴더·SHA를 기록합니다.
2. **도구:** 추적 도구는 Sysinternals Process Monitor(파일·이미지 로드·프로세스 트리 필터) 또는 Windows 기본 `wpr.exe`(FileIO·Loader 프로파일) 중 OWNER가 고릅니다. Process Monitor는 외부 다운로드이므로 파일명·출처·크기를 밝혀 별도 승인을 받습니다. 두 도구 모두 관리자 권한이 필요하며, 관리자 권한은 추적 도구에만 쓰고 대상 프로세스는 일반 권한으로 실행합니다(NSC-4).
3. **금지:** `DOTNET_`·`COMPlus_` 변수를 쓰지 않습니다. `COREHOST_TRACE`(호스트 탐색 로그)는 진단 보조로 별도 회차에만 쓰고, 기본 추적 회차와 섞지 않습니다.
4. **대상 실행:**
   - SimHarness: 승인 후보 절대경로 `dotnet.exe` + SimHarness DLL로 `official-cal`(NOT_READY 거부까지의 로드만 보임)과 비공식 `dry` 명령
   - Verifier: `environment` 명령. 인증 명령은 NOT_IMPLEMENTED라 추적할 수 없습니다
   - Test Profile 자식: `TestProfiles.Run`과 같은 인자(`test <dll> --filter … --logger trx;LogFileName=… --results-directory …`)로 시험 어셈블리 4개를 각각 실행
5. **기록:** 프로세스 트리(`dotnet` → `vstest.console` → `testhost`), 로드 이미지, 읽기 열기 파일의 경로·SHA, 7.4절 분류
6. **비교:** 109개 후보와 차집합(누락·과다)을 만들고 원본 추적 파일을 보존합니다.
7. **한계:** 지연 로드 때문에 한 번의 추적은 하한일 뿐입니다. TEST_ONLY 인증 경로를 xUnit으로 돌리면 실행 이미지가 testhost가 되어 공식 경로를 대표하지 않습니다.

### 7.6 운영 최종 실측 절차 제안 (운영 기계, 미실시)

1. **선행 조건:** G5 계정·ACL 적용, 공식 실행 명령 구현과 승인(8절), 운영 기계 Environment Profile 실측(13절)
2. **실행 계정:** VERIFIER_SERVICE 계정과 생산자 계정으로 각각 실제 공식 명령을 실행하고 로드 파일을 추적합니다.
3. **문화권 고정:** 서비스 계정의 UI 문화권을 기록합니다. 위성 리소스 로드가 문화권에 따라 달라지기 때문입니다.
4. **재현:** 같은 기계에서 2회, 가능하면 독립 기계에서 1회 반복해 로드 집합이 같은지 확인합니다.
5. **확정 순서:** 실측 집합 → OWNER 검토 → Build Provenance 초안 → Codex 검수 → OWNER 등록. 실측만으로 등록하지 않습니다.

### 7.7 EXE 14개 분류 상태 (개정 2, B4-R01)

| 구분 | 파일(SHA 앞 16자, 크기) | 위치 | 개수 | Test Profile 대상 폴더 | 109개 후보 | 분류 상태 |
|---|---|---|---:|---|---|---|
| SimHarness Apphost | `StarPlayer.SimHarness.exe`(`56b5efb2d34b155c`, 151,552 B) | `tools/StarPlayer.SimHarness`, `tools/StarPlayer.EvidenceVerifier`, `tests/StarPlayer.{EvidenceVerifier,Infrastructure,SimHarness}.Tests` | 5 | Infrastructure·SimHarness 시험 폴더에 각 1 | 밖 | PENDING(공식 실행 명령 확정 전 결정 안 함) |
| Verifier Apphost | `StarPlayer.EvidenceVerifier.exe`(`d7f17165c95250f7`, 152,064 B) | `tools/StarPlayer.EvidenceVerifier`, `tests/StarPlayer.{EvidenceVerifier,Infrastructure}.Tests` | 3 | Infrastructure 시험 폴더에 1 | 밖 | PENDING(같음) |
| VSTest 시험 호스트 | `testhost.exe`(`fb63eb314f25584a`, 150,576 B) | `tests/StarPlayer.{Application,Domain,EvidenceVerifier,Infrastructure,SimHarness,Simulation}.Tests` | 6 | 4개(Domain·Simulation·Infrastructure·SimHarness) | 밖 | PENDING(실제 사용 확인 전) |

- **[사실]** N_S1·N_S2에서 EXE 14개 모두 바이트가 같습니다(경로 길이와 무관). 이 재현성 사실은 Artifact 포함 여부 결정과 별개로 보존합니다.
- **[사실]** 실제 실행·로드 추적은 하지 않았습니다. 어떤 EXE도 LOAD_CONFIRMED·UNUSED로 분류하지 않았습니다.
- 109개 후보는 계속 CANDIDATE_INCOMPLETE입니다. 이 표는 후보 수를 바꾸지 않습니다.

## 8. 공식 실행 방식 조사 (OWNER 우선 후보: 승인 절대경로 `dotnet.exe` + 승인 DLL)

- 코드 변경과 공식 실행은 하지 않았습니다. 이 절은 코드 읽기와 개발 PC의 읽기 전용 조회 결과입니다.

### 8.1 실행 경로별 현재 상태

| 경로 | 현재 코드 | 공식 실행 가능 경로 | 상태 |
|---|---|---|---|
| SimHarness 공식 실행 | `official`·`official-dom`·`official-cal`·`official-int` 명령이 있음. `OfficialApprovals.Current`가 동결되지 않아 Preflight에서 NOT_READY로 거부(`Program.cs` 133~134행) | 승인 `dotnet.exe` + `tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.dll` + 명령 | 명령은 있으나 실행 승인 경로 없음 → NOT_READY. 실행 이미지 고정 절차 NOT_IMPLEMENTED |
| SimHarness 저장소 루트 | 현재 작업 디렉터리부터 위로 `StarPlayer.sln`을 찾음(`Program.cs` 315~321행) | 작업 디렉터리를 승인 체크아웃 루트로 고정해야 함 | [사실] 작업 디렉터리 의존 |
| Verifier 공식 인증 | CLI는 `environment`만 있음(`Program.cs`). 인증은 `Certification.Open` 등 API만 있고 CLI 연결 없음 | 승인 `dotnet.exe` + `StarPlayer.EvidenceVerifier.dll` + (미정) 인증 명령 | **NOT_IMPLEMENTED**. T1 앵커 위치를 읽는 "OWNER 관리 설치·운영 설정" 출처도 NOT_IMPLEMENTED |
| Test Profile 자식 | `new ProcessStartInfo("dotnet")`, `WorkingDirectory` 지정 없음, 인자 `test <dll> --filter … --logger trx… --results-directory <work>`(`TestProfiles.cs` 156~159행) | 부모와 같은 승인 `dotnet.exe` 절대경로, 고정 작업 디렉터리 | [사실] 실행 파일·SDK 선택이 고정되지 않음. 고정 구현 NOT_IMPLEMENTED |
| `git` 자식 | `new ProcessStartInfo("git")`(`GitIntegrity.cs` 169행, `Program.cs` 329행). 검증기 `TestProfiles.Fixtures`가 이 출력으로 Fixture blob SHA를 계산 | 승인 `git.exe` 절대경로 | [사실] PATH 해석. 고정 구현 NOT_IMPLEMENTED |

### 8.2 `dotnet.exe` 해석과 검증 가능성

- **[관찰: 개발 PC]**
  - `where dotnet` → `C:\Program Files\dotnet\dotnet.exe`
  - SDK는 `8.0.425` 하나
  - 공유 런타임 `Microsoft.NETCore.App` 3.1.32·6.0.11·8.0.31
  - `host\fxr`에 3.1.32·6.0.11·8.0.31
  - `C:\Program Files\dotnet` ACL: TrustedInstaller·SYSTEM·Administrators 모든 권한, Users 읽기·실행
  - 운영 기계 값은 다를 수 있습니다.
- **[문서 근거] 현재 자식 실행의 실제 검색 순서:** .NET은 `UseShellExecute=false`이면 실행 파일 이름을 그대로 `CreateProcess`에 넘깁니다. 그러면 Windows가 다음 순서로 `dotnet.exe`를 찾습니다.
  1. 부모 실행 이미지가 있는 디렉터리
  2. 부모의 현재 디렉터리
  3. System32 등 시스템 디렉터리
  4. `PATH`
  즉 "PATH의 dotnet"이 아니라 그보다 앞선 위치가 먼저 쓰일 수 있습니다. 부모가 Apphost(`StarPlayer.EvidenceVerifier.exe`)라면 1번 위치는 검증기 `bin` 폴더입니다.
- **[문서 근거] SDK 선택:** `dotnet test`는 작업 디렉터리부터 위로 `global.json`을 찾아 SDK를 고릅니다. 자식의 작업 디렉터리를 정하지 않으면 SDK 선택이 검증기의 현재 디렉터리에 따라 달라집니다. 개발 PC는 SDK가 하나라 차이가 드러나지 않습니다.
- **[문서 근거] hostfxr 선택:** muxer(`dotnet.exe`)는 `host\fxr`의 가장 높은 버전을 씁니다. 설치 디렉터리의 내용이 실행 체인을 정합니다.
- **[사실] Pin 범위:** §18.2 Environment Profile은 `System.Private.CoreLib.dll`·`clrjit.dll`·`coreclr.dll`의 SHA만 고정합니다. `dotnet.exe`·`hostfxr.dll`·`hostpolicy.dll`·SDK 파일은 어떤 승인 문서에도 고정되지 않습니다.
- **검증 가능성:** 승인 경로의 `dotnet.exe`를 쓰려면 (a) 경로를 어디에 기록할지, (b) 그 SHA를 어디에 고정할지, (c) 실행 직전 누가 대조할지가 정해져야 합니다. §18.2·R03-1 앵커·§18.3이 모두 닫힌 Schema라 (a)·(b)를 새 필드로 넣을 수 없습니다. 후보는 B-4 조항 본문, 운영 설치 설정(T1과 같은 보호 위치), `DIAGNOSTICS` 기록입니다. **[충돌]** OWNER 규범 결정이 필요합니다(15절 D-6).

### 8.3 testhost·VSTest 구성요소

| 위치 | 파일 | 역할 | 현재 Pin |
|---|---|---|---|
| SDK `sdk\8.0.425\` [관찰: 개발 PC] | `vstest.console.dll`·`.deps.json`·`.runtimeconfig.json`, `testhost.dll`, `testhost-*.runtimeconfig.json`, `datacollector.dll`, `Microsoft.TestPlatform.*.dll` | `dotnet test <dll>`가 쓰는 시험 실행기 | 없음 |
| SDK `sdk\8.0.425\Extensions\` [관찰: 개발 PC] | `Microsoft.VisualStudio.TestPlatform.Extensions.Trx.TestLogger.dll`, `Microsoft.TestPlatform.TestHostRuntimeProvider.dll`, Blame·EventLog 수집기 등 | TRX 작성(검증기 판정 입력), 호스트 선택 | 없음 |
| 시험 출력 폴더 | `testhost.dll`·`Microsoft.TestPlatform.*.dll` | 시험 호스트 | 109개 후보(DEPENDENCY) |
| 시험 출력 폴더 | `testhost.exe`, xunit runner 3종, 위성 리소스 | 시험 호스트 실행 파일, 시험 발견·실행 | 후보 밖. 포함 여부 PENDING(7.7절) |

- **[문서 근거]** VSTest는 시험 폴더의 `testhost.exe`를 쓰거나 `dotnet exec … testhost.dll`로 호스트를 띄웁니다. 어느 쪽인지는 VSTest 버전·설정에 따르므로 7.5절 추적으로 확인합니다([실측]).
- **[사실]** TRX 파일은 자식이 `--results-directory`에 쓰고 부모가 다시 읽어 판정합니다(`TestProfiles.cs` 174행). 이 디렉터리를 생산자가 쓸 수 있으면 TRX를 바꿔 넣을 수 있으므로 ACL 대상입니다(8.5절).

### 8.4 실행 파일과 DLL의 바이트 Pin 관계

| 항목 | A. 승인 `dotnet.exe` + DLL (OWNER 우선 후보) | B. `<이름>.exe` Apphost |
|---|---|---|
| 실행 이미지 | 설치된 `dotnet.exe`(muxer) + `hostfxr`·`hostpolicy`(공유 설치) + DLL | 빌드 출력 exe(SDK의 apphost 템플릿에 DLL 이름을 기록한 파일) + `hostfxr`·`hostpolicy` + DLL |
| exe → DLL 결속 | 명령줄의 DLL 절대경로 | **[문서 근거]** Apphost는 DLL 이름만 담고 DLL 바이트의 Hash는 담지 않음. exe를 Pin해도 DLL은 결속되지 않음 |
| 빌드 출력 Pin | DLL·deps·runtimeconfig(109개 후보로 표현 가능) | exe도 Pin 필요. 닫힌 역할 중 어디에 둘지 계약에 없음 |
| `verifiers[].binary_sha256` | VERIFIER_BINARY(DLL)와 1:1 | exe와 DLL 중 무엇을 `binary_sha256`으로 할지 모호(검증기는 binary_sha256 ∈ VERIFIER_BINARY만 검사, `ApprovalRecord.cs` 227~230행) |
| 자식 `dotnet` 검색 1순위 위치 | `dotnet.exe` 설치 디렉터리 | 검증기 `bin` 폴더(8.2절) |
| 재현성 | DLL 동일(경로 4개) | exe도 경로와 무관하게 동일(`StarPlayer.SimHarness.exe` `56b5efb2…`, `StarPlayer.EvidenceVerifier.exe` `d7f17165…`. 빌드 출력 EXE 14개 = Apphost 8개 + VSTest `testhost.exe` 6개 모두 [사실], 7.7절) |
| 공유 런타임 경계 | §18.2 `runtime_files`(CoreLib·clrjit·coreclr)만 Profile에 있고 `dotnet.exe`·`hostfxr`는 없음 | 같음 |
| 현재 코드 | Test Profile 자식은 A 형식(`dotnet test <dll>`), 단 실행 파일은 이름 검색 | 사용하는 코드 없음 |
| 자기 SHA 대조(G4) | 실행 중인 검증기 바이너리 자기 SHA 대조가 없음(남은 위험) | 같음 |

- **[충돌]** 공식 SimHarness·Verifier 실행 명령이 코드·계약에 없습니다. 그래서 Apphost 포함·제외를 지금 결정하지 않습니다.
- **EXE 일괄 제외 안 철회(개정 2, B4-R01):** 빌드 출력의 EXE 14개는 SimHarness·Verifier Apphost 8개와 VSTest `testhost.exe` 6개로 나뉩니다(7.7절). 방식 A를 채택해도 `dotnet test`가 `testhost.exe`를 쓰지 않는다고 볼 근거가 없습니다. 실제 실행·로드 추적 전에는 어떤 EXE도 일괄 제외하지 않습니다.
  - SimHarness·Verifier Apphost: 공식 실행 명령이 확정되기 전에는 최종 포함·제외를 정하지 않습니다(PENDING).
  - `testhost.exe`와 관련 VSTest 구성요소: 실제 사용 확인 전까지 Artifact 포함 여부는 PENDING입니다.

### 8.5 Windows 계정·ACL·모듈 로딩 신뢰 경계

| 위치 | 실행 체인에서 하는 일 | 쓰기를 막아야 할 주체 | 현재 |
|---|---|---|---|
| `dotnet.exe` 설치 디렉터리(`host\fxr`·`shared`·`sdk` 포함) | muxer·hostfxr·공유 런타임·VSTest | 생산자, VERIFIER_SERVICE, 증거 작성자 | [관찰: 개발 PC] Users는 읽기·실행만. 운영 OWNER_VALUE_PENDING |
| 검증기 `bin` 폴더 | VERIFIER_BINARY, SimHarness·Domain·Simulation·Infrastructure 사본, SQLite native, runtimeconfig·deps | 생산자, VERIFIER_SERVICE 자신 | OWNER_VALUE_PENDING |
| 시험 출력 폴더 4개 | TEST_ASSEMBLY와 의존성, `testhost` | 생산자 | OWNER_VALUE_PENDING |
| 검증기·자식 작업 디렉터리 | `CreateProcess` 검색 2순위, `global.json` 탐색 시작점 | 생산자 | 지정 없음 |
| TRX 결과 디렉터리 | 판정 입력 파일 | 생산자 | OWNER_VALUE_PENDING |
| `PATH`의 각 디렉터리 | 자식 `dotnet`·`git` 검색 마지막 순위 | 생산자 | 운영 계정 PATH 미정 |
| 앵커 디렉터리 | T1·T2 | TCB 외 모두 | 코드로 검사(NSC-4 B) |

- **[문서 근거] 환경 변수 경계:** 계약 E2-1·E4의 금지 접두사는 `DOTNET_`·`COMPlus_`뿐입니다. 접두사 밖에도 로더에 영향을 주는 변수가 있습니다.
  - `CORECLR_ENABLE_PROFILING`·`CORECLR_PROFILER`·`CORECLR_PROFILER_PATH*`: 네이티브 프로파일러 DLL을 프로세스에 로드
  - `VSTEST_*`: VSTest 동작 변경
  - 이 변수들은 C0 digest에 포함되지만 Environment Profile은 digest 값을 고정하지 않습니다(C0 = C1 = C2만 비교).
  - 닫힌 E2-1 규칙을 바꾸지 않고 다룰지, 계약 해석으로 다룰지 OWNER 결정이 필요합니다([충돌], 15절 D-7).
- **모듈 로딩 한계:** 프로세스 안의 자기 검사는 이미 로드된 악성 모듈을 막지 못합니다. 예방은 ACL(G5)이 맡고, 자기 검사(9절)는 승인 밖 파일의 로드를 감지하고 결속하는 역할입니다.

## 9. Verifier의 SimHarness DLL 사본 — 결속 설계 제안 (구현 없음)

**사실:**

- [사실] 검증기는 SimHarness를 프로젝트 참조로 씁니다(`StarPlayer.EvidenceVerifier.csproj`, `deps.json`의 `StarPlayer.SimHarness/0.0.0-phase0` type `project`). .NET 호스트는 이 참조를 검증기 폴더의 `StarPlayer.SimHarness.dll`로 해석합니다(deps.json 해석 기준, 로드 추적 미실시).
- [사실] 검증기는 이 사본의 코드로 Replay(`FamilyOfficialReplay`·`DominanceOfficialReplay`), Schema·JCS·Archive, Dominance 통계, 내장 입력(`HarnessData.ResourceBytes`, INPUT_BUNDLE 기대 SHA 계산)을 실행합니다.
- [사실] `00091736` N_S1에서 검증기 폴더 사본과 SIMHARNESS_BINARY는 같은 바이트입니다(`a085d613…`). 같아야 한다는 규칙과 실행 중 검사는 없습니다.
- [사실] 검증기는 자기 DLL(VERIFIER_BINARY)과 로드한 DLL의 SHA를 실행 중에 대조하지 않습니다. `TestProfiles.CheckAssembly`는 TEST_ASSEMBLY만 대조합니다.
- 현재 구현을 승인된 안전 상태로 보지 않습니다.

**제안 (병행 적용, 모두 NOT_IMPLEMENTED, 코드 수정은 별도 OWNER 허가 필요):**

| ID | 방식 | 막는 것 | 한계 |
|---|---|---|---|
| V-1 정적 동일성 규칙 | Build Provenance에 검증기 폴더 사본 DEPENDENCY 행이 정확히 1개 있고, 그 `raw_sha256` = SIMHARNESS_BINARY `raw_sha256`이어야 함. 검증기가 T5에서 검사 | 다른 바이트의 사본이 승인 문서에 들어가는 것 | 실제로 로드된 파일을 증명하지 않음 |
| V-2 실행 중 로드 목록 결속 | E2-3 직후(Build Provenance 확보 뒤, E2-6 전)와 E2-7 직전에 로드된 관리 어셈블리(`Assembly.Location`)와 프로세스 모듈 목록을 수집. 각 파일을 쓰기 공유 없이 열어 SHA를 계산하고, 세션 동안 핸들을 유지. 승인 집합과 대조 | 승인 밖 DLL이나 다른 바이트의 DLL이 로드된 상태의 인증 | 프로세스 안 검사이므로 악성 코드가 이미 로드된 경우는 못 막음(8.5절) |
| V-3 ACL | 검증기 `bin` 폴더는 OWNER 배포 계정만 쓰기 가능. VERIFIER_SERVICE·생산자는 읽기·실행만 | 사본 교체 자체 | 운영 값 OWNER_VALUE_PENDING(G5) |

**Fail-Closed 조건 (V-2 후보, 하나라도 해당하면 `Evidence.approval_mismatch`류로 인증 거부 — 원인 코드는 계약 해석 필요):**

1. 로드된 StarPlayer 어셈블리의 경로가 검증기 `bin` 폴더 밖이거나, 그 경로가 Build Provenance(VERIFIER_BINARY·DEPENDENCY)에 없음
2. 경로는 있으나 SHA·길이가 다름
3. `Assembly.Location`이 비어 있음(바이트에서 로드됨)
4. 같은 어셈블리 이름이 두 경로에서 로드됨, 또는 기본 `AssemblyLoadContext` 밖에서 로드됨
5. 검증기 폴더 SimHarness 사본 SHA ≠ SIMHARNESS_BINARY SHA(V-1)
6. 검사 이후 승인 밖 어셈블리가 로드됨(`AppDomain.AssemblyLoad` 감시 → 세션 무효 표시 → 영수증 저장 거부)
7. E2-7 직전 모듈 목록 ⊄ (승인 빌드 출력 ∪ 승인 공유 런타임·OS 모듈 집합)
8. 대조용으로 연 파일 핸들을 세션 중 유지하지 못함

- **열린 해석:** 7번의 "승인 공유 런타임·OS 모듈 집합"은 §18.2에 없습니다(8.2절과 같은 [충돌]). 로드 목록을 어디에 남길지(`DIAGNOSTICS` VERIFIER_DERIVED 후보)도 계약 해석이 필요합니다.
- **같은 문제의 다른 경로:** Test Profile 자식은 TEST_ASSEMBLY SHA를 읽어 확인한 뒤 같은 경로를 `dotnet test`가 다시 엽니다(`Certification.cs` 119~121행). 시험 폴더의 DEPENDENCY·RUNTIME_CONFIG는 실행 시 대조하지 않습니다. 후보 대응은 (a) 확인 후 실행 종료까지 쓰기 공유 없이 핸들 유지, (b) Pin된 바이트를 검증기 전용 디렉터리에 복사해 거기서 실행, (c) ACL입니다. §21 "Hash한 뒤 같은 경로를 다시 열어 다른 파일을 파싱하지 않습니다"가 실행 파일에도 적용되는지는 계약 해석 대상입니다.

## 10. Profile Archive·TRUST_CONFIG 대안

### 10.1 Environment Profile·PZL Test Profile 원문의 Archive 보존

**사실:**

- [사실] 두 Profile은 OWNER가 Registry에 등록하는 원문이고, 검증기는 T5(L6)에서 Registry로 조회합니다(§16).
- [사실] 생산 검증기의 `ConsumedInputs.Expected`(B1 T11 R2-A, Codex C10 PASS 범위)는 영수증 ORIGINAL의 INPUT_BUNDLE SHA 집합이 {Environment Profile SHA, PZL Test Profile SHA, 내장 입력 SHA들}과 정확히 같기를 요구합니다(`RequiredOriginals.cs` 14~15행, `Certification.cs` 154행). TEST_ONLY는 파일명만 `trust/environment-profile.json`·`trust/pzl-test-profile.json`으로 둡니다.
- [사실] §21 INPUT_BUNDLE은 "소비 입력 파일별"이고 전용 Profile 역할은 없습니다. 새 역할 추가는 Schema 변경입니다.

**역할 구분(제안):** Registry 원문이 신뢰 출처(Pin)이고, Archive 사본은 독립 재검증용 보존본입니다. Archive 사본은 신뢰 출처로 쓰지 않으며, SHA는 Registry `raw_sha256`과 같아야 합니다. APPROVAL_RECORD·BUILD_PROVENANCE 사본과 같은 관계입니다.

| 안 | 내용 | §18.3·§21 Schema 변경 | 기존 코드와 관계 | 판단 |
|---|---|---|---|---|
| P-A | 두 원문을 INPUT_BUNDLE ORIGINAL로 보존. "소비 입력"에 검증기가 소비한 Registry 원문이 포함된다고 해석 | 없음 | 현재 생산 검증기 그대로 | **추천**. OWNER 해석 확인 필요 |
| P-B | Archive에 보존하지 않고 Registry 참조만 | 없음 | R2-A 요구와 충돌 → 코드 변경 필요 | 비추천 |
| P-C | `DIAGNOSTICS`로 보존 | 없음 | R2-A와 충돌, DIAGNOSTICS는 판정 근거가 아님 | 비추천 |
| P-D | 새 역할(예: `PROFILE`) | **있음** | — | 금지(닫힌 Schema) |

- 파일명 후보(P-A): `trust/environment-profile.json`·`trust/pzl-test-profile.json`(6절 후보 표와 같이 B-4가 정함).

### 10.2 TRUST_CONFIG `relative_path` 해석 Root

**사실:**

- [사실] 계약 T6은 Build Provenance TRUST_CONFIG의 `raw_sha256` = T1 앵커 바이트 SHA인지**만** 확인하고, 앵커를 얻는 데 쓰지 않습니다. 앵커는 저장소·증거 루트 밖에 있어야 합니다(T2).
- [사실] 코드(`TrustBootstrap.cs` 59~62행)는 TRUST_CONFIG 행이 정확히 1개이고 SHA가 같은지만 보며 `relative_path`를 해석하지 않습니다. TEST_ONLY 값은 `config/trust-anchor.json`입니다.
- [사실] 다른 역할의 `relative_path`는 체크아웃 루트(`buildRoot`)와 결합됩니다(`Certification.cs` 119행). 같은 RELPATH라도 역할마다 루트가 다릅니다.

| 안 | 해석 Root | 실제 보호 Trust Root | Schema 변경 | 코드 변경 | 판단 |
|---|---|---|---|---|---|
| T-A | 없음. `relative_path`는 식별 이름. B-4 조항이 고정 문자열(예: `trust/trust-anchor.json`)을 정하고 해석하지 않는다고 명시 | 앵커 디렉터리(NSC-4 B, 운영 설치 설정이 지정) | 없음 | 없음 | **추천**(현재 동작과 같음) |
| T-B | 앵커 디렉터리. `relative_path` = 앵커 디렉터리 기준 파일 이름이고, 검증기가 일치를 추가 확인 | 같음 | 없음 | 필요(대조 추가) | 대안 |
| T-C | 빌드 체크아웃 루트(다른 역할과 같은 루트) | 체크아웃 안 | 없음 | — | **거부**: T2 "앵커는 저장소 루트 밖"과 충돌 |
| T-D | 역할별 Root 표를 B-4 조항에 둠(빌드 Artifact = 체크아웃 루트, TRUST_CONFIG = 앵커 디렉터리 또는 해석 안 함) | 같음 | 없음 | T-A면 없음 | T-A·T-B를 감싸는 표기 방식 |

- 추천은 T-D 표기 + T-A 의미입니다. 어느 안도 §18.3·§21의 닫힌 Schema를 바꾸지 않습니다.
- 운영 값(앵커 위치, `registry_instance_id`·`registry_pipe_name`·`registry_account_sid`·`registry_service_sid`, `owner_principal_sids`·`verifier_principal_sid`·`evidence_reader_sids`)은 G5 **OWNER_VALUE_PENDING**입니다.

## 11. SourceLink 원격 — 원격 Commit 미검증 상태와 공개 적합성

### 11.1 공식 후보와 현재 상태

- 공식 후보 URL: `https://github.com/junk1901/Star_player.git`(OWNER 지정, 저장소 존재 확인은 OWNER).
- **원격 Commit 검증: 미완료.** 원격 Commit이 확인되기 전에는 공식 Provenance 출처 검증 완료로 표시하지 않습니다.
- [사실] 진단 CI 빌드의 SourceLink URL은 `https://raw.githubusercontent.com/junk1901/Star_player/<commit>/*`이고, Commit은 실제 빌드 Commit(`00091736…`)입니다. 로컬 HEAD(`96070c5`)로 대체하지 않았습니다.

### 11.2 원격 ref 읽기 조회 (개정 1, 2026-10-26)

- [사실] `git ls-remote https://github.com/junk1901/Star_player.git` 결과는 `HEAD`·`refs/heads/main` → `7456f44fd3ecd687afce210eb5be972114f8479c` 한 개뿐입니다. Tag는 없습니다.
- [사실] `7456f44`(docs: approve phase 3c3a detailed contract)는 로컬 이력의 조상이며 로컬 51개 Commit 중 19번째입니다. 그 뒤 로컬 Commit 32개(`7bdde7d`·`00091736`·`96070c5` 포함)를 가리키거나 조상으로 포함하는 광고된 ref는 이 조회에서 확인되지 않았습니다.
- 이 조회는 광고된 ref만 읽은 것입니다. Fetch·Push는 하지 않았습니다.
- **해석(개정 2, B4-R02):** 승인 후보 Commit을 가리키는 광고된 Ref는 확인되지 않았습니다. 해당 Commit 객체 및 SourceLink Raw 콘텐츠의 조회 가능성은 별도 미검증이며, 공식 원격 출처 검증은 완료되지 않았습니다.
  - 광고된 ref가 없다는 사실만으로 Commit 객체가 원격에 없다거나 조회가 불가능하다고 단정하지 않습니다.

| 검증 대상 | 상태 |
|---|---|
| 광고된 ref(`git ls-remote`) | [사실] `HEAD`·`refs/heads/main` → `7456f44…` 하나, Tag 0개. 승인 후보 Commit을 가리키는 ref 미확인 |
| 승인 후보 Commit 객체의 원격 접근 가능성 | 미검증 |
| SourceLink Raw URL(`raw.githubusercontent.com/junk1901/Star_player/<commit>/…`) 조회 | 미검증 |
| 공식 원격 출처 검증 | 미완료 |

### 11.3 공개 저장소 게시 적합성 평가 (Push 승인 아님)

| 항목 | 원격 `7456f44`에 이미 있음 | 로컬 HEAD에 있음 | Push하면 새로 공개됨 |
|---|---|---|---|
| OWNER·Codex 원문(`docs/contracts/b1/originals/**`) | 0개 | 35개 | 35개(Codex 요청·판정, OWNER 지시 원문) |
| 로컬 경로 문자열(`E:\Madegame…`, `C:\Users\user…`)이 든 추적 파일 | 114개 | 180개 | 약 66개 증가(원시 로그·보고서) |
| OWNER 결정·Vault 결정 기록(`docs/contracts/b1/*OWNER*`, `Star/02_Decisions/*`) | 일부 | 전부 | 7456f44 이후 결정 기록 |
| Commit 작성자 메일 주소(메타데이터) | 있음 | 있음 | 새 노출 아님 |
| 비밀정보 패턴(키·토큰·비밀번호 형식의 단순 정규식) | — | 0건 | 단순 검사이며 완전한 검사가 아님 |

- **핵심 제약[사실]:** `00091736`·`7bdde7d`는 승인 기록과 Tag가 결속된 SHA입니다. 공개 전에 원문·경로를 지우려고 이력을 재작성하면 SHA가 바뀌어 기존 결속이 깨집니다(A11과 충돌). 따라서 이 이력은 **있는 그대로 공개하거나, 공개하지 않거나** 둘 중 하나입니다.
- 공개 적합성의 최종 판단은 OWNER 몫입니다. 이 평가는 사실 정리입니다.

### 11.4 원격 선택지 (OWNER 결정 자료)

- **OWNER 결정(2026-10-29): R-1.** 기존 공개 저장소 `https://github.com/junk1901/Star_player.git`를 공식 원격으로 쓰고 Public을 유지합니다. 별도 비공개 원격(R-3)은 취소되었습니다. 실제 Push는 OWNER 승인 A 뒤에만 합니다. 게시 절차·공개 범위 재산출은 [A-1 게시 준비](B1_A1_PUBLICATION_PREP.md)(2026-10-29 개정)에 있습니다. 아래 표는 결정 전 자료로 그대로 둡니다.

| 안 | 내용 | SourceLink URL | 장점 | 위험·비용 |
|---|---|---|---|---|
| R-1 | 현재 공개 저장소에 승인 Commit과 조상을 Push | 현재 후보 그대로 | 누구나 raw URL로 원격 검증 가능 | 11.3절 원문 35개·경로 공개, 되돌릴 수 없음 |
| R-2 | 현재 저장소를 비공개로 전환 후 Push | 같은 URL(PDB·DLL 바이트 불변) | URL 유지, 공개 범위 통제 | raw 조회에 인증 필요 → 검증자는 접근 권한이 있어야 함. 기존 공개 Commit은 이미 복제됐을 수 있음 |
| R-3 | 별도 비공개 공식 원격(새 URL) | 새 URL → **PDB·DLL 바이트가 바뀜**(4절) | 기존 공개 저장소와 분리 | 공식 빌드 SHA가 새 URL 기준이 됨. 진단 빌드 SHA와 다름 |
| R-4 | 원격 게시 없이 로컬 검사만 | 후보 URL 기록, 원격 조회 가능성 미검증 | Push 없음 | "원격 출처 확인 완료"를 쓸 수 없음. A7 취지상 공식 출처 검증 불충분 |

**Push 승인을 요청할 때 정리할 항목(이번에 Push하지 않음):**

| 항목 | 정리 내용 |
|---|---|
| Push 대상 Commit | 최소 `00091736`(검증 소스)과 그 조상 전체. 문서 Commit `cd5f413`·`1978863`·`03eb906`·`b17742a`·`96070c5` 등을 포함할지 |
| 브랜치 | 기존 `main`에 바로 올릴지, 보존 전용 브랜치(예: `b1/g3-scene-migration`)를 쓸지 |
| Tag | `b1-code-accepted-2026-10-26` Push 여부(현재 로컬만, 원격 Tag 0개) |
| 보존 정책 | 강제 Push·브랜치 삭제·이력 재작성 금지. 원격 보호 규칙 설정 |
| 공개 범위 | 11.3절 평가와 R-1~R-4 선택 |
| 확인 방법 | Push 후 원격에서 Commit 조회(읽기)로 존재를 확인하고, SourceLink URL의 raw 경로로 파일을 조회 |

## 12. `build_procedure_clause_id` 후보 (후보 유지, 등록 보류)

- **계약[사실]:** §18.3 `build_procedure_clause_id: EXACT (B-4 승인 조항)`
  - `EXACT` = "기존 등록 ID·버전·조건 문자열을 변경 없이 보존"(§1.2)
  - 구현 Reader는 `Exact`(문자열)로 받습니다(`ApprovalRecord.cs` 66행). 시험값은 `b4-test`입니다.
- **제안 형식:** 버전을 포함하고 `ID` 문법(`[A-Za-z0-9][A-Za-z0-9._-]{0,127}`)도 만족하는 문자열. EXACT보다 엄격하지만 계약과 충돌하지 않습니다.

| 후보 | 범위 | 상태 |
|---|---|---|
| **`starplayer.b4.dotnet-build.v1`** | 4절 1~13단계(.NET 빌드, CI 속성, SourceLink·경로 검사, 두 폴더 비교, Artifact 목록 기록) | 후보 유지, 등록 보류 |
| `starplayer.b4.dotnet-build.v1+artifacts.v1` | 위 + 7절 Artifact 집합 규칙을 같은 조항에 고정할 때 | 보조 후보 |
| `starplayer.b4.godot-export.v1` | 5절 Godot Export. §18.3에 쓰지 않고, 기존 Build Provenance Schema에 새 Artifact 역할을 추가하지 않는 별도 Godot Export 정책 | 후보 유지, 등록 보류 |

- 조항 본문이 바뀌면 `v2`로 새 ID를 씁니다. 등록된 ID의 의미는 바꾸지 않습니다.
- **등록하지 않은 이유:** 공식 실행 명령, 출력 Root·파일명, Artifact 실제 집합, SourceLink 원격 검증, 신뢰·권한 경계, 검증 실패 정책이 확정되지 않았습니다.

## 13. 운영 Environment Profile (§18.2)과 빌드 도구 환경의 구분

| 구분 | 대상 | 근거 |
|---|---|---|
| 빌드 도구 환경(B-4·G3) | SDK 8.0.425, MSBuild 17.11.48, Roslyn 4.11.0-3.25569.22, NuGet 패키지 22개, Godot·Template, 빌드 명령·환경 변수 | 이 결정서 4·5절, [PCK 진단](../reports/B1_G3_GODOT_PCK_REPRO_REPORT.md) 13절 |
| 운영 계산 환경(§18.2, G8·G9) | OS 버전(`Environment.OSVersion.Version`)·X64, 프로세스 X64, `FrameworkDescription`·`Microsoft.NETCore.App` 버전·RID, `ucrtbase.dll` 파일 버전·SHA, `System.Private.CoreLib.dll`·`clrjit.dll`·`coreclr.dll` SHA, 금지 접두사 정책, UpperQuantile 36개 BITS64, `witness_digest` | 승인 실행 기계에서 실측(E2-1 C0 수집) 후 N12와 함께 별도 승인 |
| 실행 체인(§18.2 밖) | `dotnet.exe`·`hostfxr`·`hostpolicy`·SDK VSTest·`git.exe` | 8.2·8.3절. 고정 위치 미정([충돌]) |

- 현재 진단·시험 값으로 운영 Profile을 만들지 않았습니다. 빌드 PC의 값은 운영 기계 값이 아닙니다.

## 14. 운영 Gate 선행 조건

| Gate | B-4와의 관계 | 선행 조건 |
|---|---|---|
| G1 | 6절 표의 공식 이름·경로 확정 | OWNER 결정(15절 D-3~D-5) |
| G2 | 4절 2단계 입력 바이트 정책. `.tscn` LF는 반영됨, 나머지 입력·보호 파일 정책은 기존 G2 결정 | OWNER 결정(기존 G2 범위) |
| G3 | 4절 절차로 공식 빌드 후 SHA 기록, 재현성 확인 | G1·G2, 승인 Commit, **SourceLink 원격 결정·원격 Commit 확인**, Artifact 집합 확정 |
| G4 | 검증기 자기 SHA, 로드 목록 결속(9절), Test Profile 실행 경로(8.1~8.3절) | G3, 공식 실행 명령 구현 |
| G5~G7 | TRUST_CONFIG·Archive ACL·Registry 계정, 실행 체인 ACL(8.5절) | 운영 기계·계정 |
| G8·G9 | §18.2 Profile, N12 | 운영 기계 |

- **의존 순서:** 원격 결정 → 공식 빌드(G3) → 실행 명령 구현·로드 실측(G4) → 운영 ACL(G5~G7) → Profile(G8·G9). 9절 V-3과 8.5절 ACL은 G5에 묶여 있어 G4의 Fail-Closed 설계가 G5 없이는 완결되지 않습니다.

## 15. OWNER가 다음에 내려야 할 결정 목록

| ID | 결정 | 관련 절 | 성격 |
|---|---|---|---|
| D-1 | SourceLink 원격 선택(R-1~R-4)과 공개 적합성 판단. Push 승인 요청을 따로 받을지 | 11절 | 정책 |
| D-2 | 4절 단계표를 B-4 조항 본문 후보로 채택할지, NuGet 출처 정책 | 4절 | 정책 |
| D-3 | Archive 상대 경로 체계, `run/<run_id>/…` 내부 배치 유지, Family·Dominance·PZL6·REGRESSION_PROOF 파일명 | 6절 | 정책 |
| D-4 | 운영 출력 Root, 생산자 결과 폴더의 공식 여부, 계정·SID·ACL | 6·8.5절 | 운영 값(G5) |
| D-5 | Profile Archive 역할: P-A(INPUT_BUNDLE 해석) 채택 여부 | 10.1절 | **규범 해석** |
| D-6 | 승인 `dotnet.exe`·`git.exe` 경로·SHA를 고정할 위치(B-4 조항 / 운영 설치 설정 / DIAGNOSTICS). §18.2 밖 실행 체인(hostfxr·hostpolicy·SDK VSTest)을 어디까지 승인 대상으로 볼지 | 8.2·8.3절 | **규범 해석** |
| D-7 | 금지 접두사 밖 로더 영향 변수(`CORECLR_*` 등)의 처리 | 8.5절 | **규범 해석** |
| D-8 | 공식 실행 명령(SimHarness·Verifier)의 형태와 구현 허가, Apphost 포함·제외 | 8.1·8.4절 | 정책 + 코드 허가 |
| D-9 | Verifier SimHarness 사본 결속(V-1~V-3) 채택, 실패 원인 코드, 로드 목록 보존 위치 | 9절 | 정책 + 코드 허가 + 규범 해석 |
| D-10 | Test Profile 자식의 재오픈 문제에 §21 재오픈 금지 원칙을 적용할지, 대응 방식 | 9절 | **규범 해석** |
| D-11 | 진단용 로드 추적(7.5절) 승인과 추적 도구 선택(외부 다운로드면 별도 승인) | 7.5절 | 실행 허가 |
| D-12 | Artifact 집합 규칙: xunit 어댑터·`testhost.exe`·위성 리소스·비x64 native 포함 여부 | 7절 | 정책(실측 후) |
| D-13 | TRUST_CONFIG `relative_path`: T-A/T-B, 역할별 Root 표(T-D) | 10.2절 | **규범 해석** |
| D-14 | PDB 진단 증거의 보존 위치·형식·Archive 연계 | 1절 5 | 정책 |
| D-15 | Godot Export 조항 채택, Clause ID 등록 시점 | 5·12절 | 정책(나중) |
| D-16 | Environment Profile 등록 시점, G8·G9 실측 계획 | 13절 | 운영 |

## 16. 결과 보고 분류

### 16.1 부분 수용된 정책

- A1~A11(0절). 모두 정책 방향이며 동결·등록·PASS가 아닙니다.

### 16.2 미확정 정책

- PathMap 대안, NuGet 출처, 공식 SourceLink 원격 선택, 공식 실행 방식(우선 후보만 있음), Apphost, Artifact 집합(CANDIDATE_INCOMPLETE), B-4 파일명·경로 표, 운영 출력 Root, PDB 보존 위치, Godot Export 조항·Clause ID 등록(D-1~D-4, D-8, D-12, D-14, D-15)

### 16.3 계약 해석이 필요한 항목

- Profile 원문을 INPUT_BUNDLE "소비 입력"으로 볼지(D-5)
- §18.2 밖 실행 체인의 승인 위치(D-6)
- 금지 접두사 밖 로더 영향 변수(D-7)
- 로드 목록 결속 실패의 원인 코드와 보존 역할(D-9)
- §21 재오픈 금지 원칙의 실행 파일 적용(D-10)
- TRUST_CONFIG `relative_path` 의미·역할별 Root(D-13)
- Apphost를 닫힌 역할 중 어디에 둘지, `verifiers[].binary_sha256` 대상(8.4절)

### 16.4 추가 코드 구현이 필요한 항목 (모두 NOT_IMPLEMENTED, 별도 OWNER 허가 전 구현 금지)

1. Verifier 공식 인증 CLI 명령과 T1 앵커 위치를 읽는 운영 설정
2. Test Profile 자식을 승인 `dotnet.exe` 절대경로로 실행하고 작업 디렉터리를 고정
3. `git` 자식을 승인 경로로 실행
4. TEST_ASSEMBLY와 시험 폴더 의존성의 실행 시 결속(핸들 유지 또는 전용 복사본)
5. 검증기 로드 목록 수집·대조와 V-1 정적 규칙(9절)
6. 검증기 자기 SHA 대조(G4 남은 위험)
7. (T-B를 고르면) TRUST_CONFIG `relative_path` 대조
8. SimHarness 공식 실행의 저장소 루트를 작업 디렉터리 대신 명시 인자로 받기(선택)

### 16.5 운영 실측이 필요한 항목

- 진단·운영 로드 추적(7.5·7.6절), `testhost.exe` 사용 여부, 서비스 계정 UI 문화권과 위성 리소스 로드
- 운영 기계의 `dotnet.exe`·`hostfxr`·SDK 경로·버전·SHA, 설치 디렉터리 ACL
- 계정·SID·Archive Root·ACL(G5~G7), TRX 결과 디렉터리 ACL
- Environment Profile·N12(G8·G9)
- 원격 게시 후 원격 Commit·raw URL 조회

### 16.6 Codex 독립 검수에 넘길 Finding 후보

- [B1_B4_CODEX_READONLY_REVIEW_REQUEST.md](B1_B4_CODEX_READONLY_REVIEW_REQUEST.md)의 F-01~F-20. 1차 Codex 판정 중 OWNER가 전달한 부분은 18.3절에 반영했습니다.

## 17. 이번 작업에서 한 측정·확인

### 17.1 초안 작성 시 (2026-10-26)

- `00091736` CI 빌드를 경로 길이가 다른 두 번째 clone(`E:\Madegame\g3x\nci2long`, N_S2)에서 실행했습니다. N_S1과 787/793 동일하고, 다른 6개는 coverage 대응표입니다(`full_compare_n.json` `1858c81b…`).
- Artifact 후보 109개를 `00091736` 기준으로 다시 생성·검사했습니다(7절).
- 코드 조사: 출력 경로(`EvidenceWriter`·`OfficialOutput`·`Certification`·`Receipts`), 실행·Pin(`EvidenceVerifier/Program.cs`, `TestProfiles.cs` 156행, `ApprovalRecord.cs` 227~230행)
- 계약 조사: §15·§16·§17(R03-1·R03-2·R03-5·R03-6)·§18·§20·§21·§23

### 17.2 개정 1 (2026-10-26, 부분 수용 반영)

- **읽기 전용 조회만 했습니다.** 빌드·시험·SimHarness·Verifier 실행, 파일 로드 추적은 하지 않았습니다.
- 코드 읽기: `TestProfiles.Run`(실행 파일·작업 디렉터리·인자), `Certification.RunTestProfile`(SHA 확인 후 경로 재사용), `TrustBootstrap.Resolve` T6, `RequiredOriginals`·`Certification.RequireOriginals`(R2-A), `ExecutionEnvironment`, SimHarness `Program`(명령·저장소 루트), `GitIntegrity`, 두 도구의 `.csproj`
- 개발 PC 조회: `where dotnet`·`where git`, `dotnet --list-sdks`·`--list-runtimes`, `host\fxr`·`sdk\8.0.425` 목록, `icacls "C:\Program Files\dotnet"`
- 빌드 출력 조회(N_S1): 시험 폴더 목록, SimHarness 사본 SHA 비교, 검증기 `deps.json` 프로젝트 항목
- 원격·이력: `git ls-remote`(ref 읽기만), 로컬 이력의 원문·경로·비밀정보 패턴 검사(11.2·11.3절)
- **기존 기재 정정:** 초안은 Profile 원문의 INPUT_BUNDLE 보관을 "TEST_ONLY 구현"으로만 적었습니다. 실제로는 생산 검증기(R2-A)가 두 원문의 SHA를 요구하고, 파일명만 TEST_ONLY입니다(2·6·10.1절 반영).
- 코드·Scene·시험·Golden·`Directory.Build.props`·계약·OWNER 원문·루트 `TASK.md`·Tag는 수정하지 않았습니다. Stage·Commit·Push 없음.

## 18. Codex B-4 READ-ONLY 검수 반영 (개정 2, 2026-10-27)

- **출처:** OWNER 지시 "B-4 Codex FIX_REQUIRED 최소 정정"(2026-10-27)이 전달한 Codex 판정입니다. Codex 검수 원문 파일은 이 저장소에 보관되어 있지 않으며, 이 절은 원문을 대신하지 않습니다.
- **판정:** FIX_REQUIRED. 초안 자체의 미해결 Finding은 B4-R01(MINOR)·B4-R02(MINOR)입니다.
- **유지되는 것:** A1~A11 부분 수용 정책은 모두 CONTRACT_COMPATIBLE입니다. 기존 B1 코드 수용과 Scene Migration PASS는 유지됩니다.

### 18.1 B4-R01 — EXE 일괄 제외 정정

| | 문장 |
|---|---|
| 정정 전(8.4절) | "A를 채택하면 apphost 14개는 Artifact 후보에서 빼고 목록·SHA만 진단 기록으로 남기는 안이 일관됩니다([후보])." |
| 정정 후(8.4절) | "방식 A를 채택해도 `dotnet test`가 `testhost.exe`를 쓰지 않는다고 볼 근거가 없습니다. 실제 실행·로드 추적 전에는 어떤 EXE도 일괄 제외하지 않습니다." SimHarness·Verifier Apphost는 공식 실행 명령 확정 전 PENDING, `testhost.exe`·관련 VSTest 구성요소는 실제 사용 확인 전 PENDING |
| 정정 전(8.4절 표) | "14개 apphost 모두 [사실]" |
| 정정 후(8.4절 표) | "빌드 출력 EXE 14개 = Apphost 8개 + VSTest `testhost.exe` 6개 모두 [사실], 7.7절" |
| 정정 전(7.3절) | "(win-x64 프로세스에서는 로드되지 않음)" |
| 정정 후(7.3절) | "(win-x64 프로세스에서는 로드되지 않을 것으로 보이나 로드 추적 전이라 미확인. Codex F-19 NOT_CONFIRMED)" |

- 추가: 7.7절 EXE 14개 분류 표, 7.4절 분류 상태 문장(모든 행 PENDING), 8.3절 `testhost.exe` 행 PENDING.
- 코드·빌드 설정·Test Profile 실행 방식은 바꾸지 않았습니다.

### 18.2 B4-R02 — 원격 Ref 해석 정정

| | 문장 |
|---|---|
| 정정 전(11.2절) | "그 뒤 로컬 Commit 32개(…)는 어떤 원격 ref에서도 도달할 수 없습니다." |
| 정정 후(11.2절) | "그 뒤 로컬 Commit 32개(…)를 가리키거나 조상으로 포함하는 광고된 ref는 이 조회에서 확인되지 않았습니다." |
| 정정 전(11.2절) | "원격 ref에 없다는 것은 SourceLink Commit을 원격에서 조회할 수 없다는 뜻이므로 원격 출처 검증은 **미완료**입니다." |
| 정정 후(11.2절) | "승인 후보 Commit을 가리키는 광고된 Ref는 확인되지 않았습니다. 해당 Commit 객체 및 SourceLink Raw 콘텐츠의 조회 가능성은 별도 미검증이며, 공식 원격 출처 검증은 완료되지 않았습니다." + 검증 대상별 상태표 |
| 정정 전(2절 표) | "`00091736`·`96070c5`·`7bdde7d`를 가리키는 원격 ref가 없음" |
| 정정 후(2절 표) | "`00091736`·`96070c5`·`7bdde7d`를 가리키는 광고된 ref는 확인되지 않음. Commit 객체·Raw URL 조회 가능성은 미검증" |
| 정정 전(11.4절 R-4) | "원격 조회 불가" |
| 정정 후(11.4절 R-4) | "원격 조회 가능성 미검증" |

- 조회 결과와 실제 Ref 값(`7456f44fd3ecd687afce210eb5be972114f8479c`)은 바꾸지 않았습니다. 추가 원격 조회와 Push는 하지 않았습니다.

### 18.2a D1 링크 검사 정정 (B4-R01·R02와 별개)

- 개정 1의 1절 표에 있던 "[수용]" 표기 바로 뒤에 괄호로 A 번호를 붙인 형식 8곳이 Markdown 링크로 해석되어 D1이 FAIL했습니다. 표기를 `[수용] A1`로 바꿨습니다. 뜻과 A1~A11 범위는 같습니다.

### 18.3 F-01~F-20 판정 반영

- Codex의 MAJOR는 **공식 운영 전 차단 중요도**입니다. 현재 B1 코드 수용 판정을 취소한 것이 아닙니다.
- "구분" 열은 운영 의존성과 코드 구현 결함을 나눈 Claude Code의 정리이며, Codex 판정이 아닙니다.
- OWNER 전달문에 개별 판정이 없는 항목은 "전달되지 않음"으로 두고 임의로 채우지 않았습니다.

| ID | Codex 판정(OWNER 전달) | 구분(Claude 정리) |
|---|---|---|
| F-01 | MAJOR(운영 전 신뢰 경계) | 코드 구현(실행 파일 절대경로) + 운영(ACL·PATH) |
| F-02 | MAJOR | 코드 구현(작업 디렉터리 고정) |
| F-03 | MAJOR | 코드 구현 + 계약 해석(§21 재오픈 원칙) |
| F-04 | MAJOR | 코드 구현(실행 시 결속) + 운영 실측(Artifact 집합) |
| F-05 | MAJOR | 계약 해석(승인 범위) + 운영(설치 ACL) |
| F-06 | MAJOR | 코드 구현(V-1·V-2) + 운영(V-3 ACL) |
| F-07 | MAJOR | 코드 구현(자기 SHA 대조) |
| F-08 | MAJOR | 계약 해석(E2-1 접두사 밖 변수) + 운영 |
| F-09 | MAJOR | 코드 구현(`git` 절대경로) + 운영 |
| F-10 | 전달되지 않음 | — |
| F-11 | NOT_CONFIRMED / 새로운 결함 아님 | — |
| F-12 | 전달되지 않음 | — |
| F-13 | 공식 인증 진입점 미구현, 운영 전 차단 | 코드 구현(NOT_IMPLEMENTED) |
| F-14 | 전달되지 않음(B4-R01이 관련 문장 정정) | — |
| F-15 | 전달되지 않음(B4-R02가 관련 문장 정정) | — |
| F-16 | 전달되지 않음 | — |
| F-17 | MAJOR | 운영(TRX 디렉터리 ACL) |
| F-18 | MAJOR | 계약 해석(§18.2 밖 실행 체인) + 운영 |
| F-19 | NOT_CONFIRMED / 새로운 결함 아님 | — |
| F-20 | 전달되지 않음 | — |

### 18.4 계속 미확정인 항목 (변경 없음)

- 공식 SourceLink 원격 결정, Artifact 완전 집합, 실제 Apphost·Test Host 사용 여부, TRUST_CONFIG Root, 공식 실행 명령, 인증 실행 체인 Pin, G5 계정·ACL, 공식 출력 Root, `build_procedure_clause_id` 등록, 운영 Environment Profile, N12 Golden, Build Provenance 등록
- B-4 전체 승인이나 G3 PASS로 승격하지 않습니다.

### 18.5 신규 OWNER 결정이 필요한 항목

| ID | 결정 | 이유 |
|---|---|---|
| D-17 | 이번 Codex B-4 검수 원문(요청·판정)을 `docs/contracts/b1/originals/codex/`에 보존할지 | 원문이 저장소에 없어 F-10·F-12·F-14·F-15·F-16·F-20의 개별 판정을 기록할 수 없음. 이번 지시는 originals 수정 권한을 주지 않음 |
| D-18 | 승인 후보 Commit 객체와 SourceLink Raw URL의 읽기 전용 원격 조회를 수행할지, 수행한다면 방법(예: Raw URL GET, 원격 Commit API) | B4-R02 정정 후 두 항목은 미검증으로 남음. Push 없이도 조회 가능성 확인은 가능할 수 있음 |
| D-11(기존, 우선순위 상향) | 진단용 로드 추적 승인 | 7.7절 EXE 14개와 7.4절 모든 행이 이 추적 없이는 PENDING에서 벗어날 수 없음 |
