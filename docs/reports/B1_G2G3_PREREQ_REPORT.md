# B1 공식 G2/G3 선행 문제 분석 보고서 (줄바꿈·G1·Archive·Producer)

- 작성: Claude Code, 2026-10-29. 근거: OWNER 지시 "G2/G3 진단 수용 및 공식 실행 선행 문제 해결"(2026-10-29)
- **성격: 진단·분석입니다.** 공식 G1~G3 PASS, 공식 소스 Commit 확정, Build Provenance 등록이 아닙니다. Push·Tag·계약·`.gitattributes`·제품 코드 변경은 없습니다.
- 기준: 공식 원격 `main` = `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f`(게시됨). 로컬 기록 `e8f8f72`·`200f448`과 이 보고서 Commit은 미게시입니다.
- 증거(저장소 밖): `E:\Madegame\g3x\phase3ops\eol\`(줄바꿈 비교), `E:\Madegame\g3x\phase3ops\prodsep\`(Producer 분리), PCK `E:\Madegame\g3x\pck-out\EOLCRLF_P1·P2`, `EOLLF_P1·P2`. 해시는 8절.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. CRLF/LF 체크섬 비교와 공식 빌드 정책

### 1.1 조건

| 항목 | A안(CRLF) | B안(LF) |
|---|---|---|
| Clone | `git clone -c core.autocrlf=true --no-hardlinks https://github.com/junk1901/Star_player.git E:\Madegame\g3x\eol-crlf` | `… -c core.autocrlf=false … E:\Madegame\g3x\eol-lf` |
| 설정 적용 시점 | Clone 명령의 `-c`(첫 Checkout 전, 저장소 local 설정에 기록 확인) | 같음 |
| Commit·URL·SDK·CI 옵션 | `c77ed1a`, 공식 URL, SDK 8.0.425, 같은 CI 명령·환경 변수 | 같음 |
| `.gitattributes` | 저장소 그대로(`1cdbb0e0…`), 추적 파일 수정 없음 | 같음 |

### 1.2 결과

| # | 검증 항목 | A안 CRLF | B안 LF |
|---|---|---|---|
| 1 | D3 보호 파일 5개 | 모두 일치 | 모두 일치(같은 SHA) |
| 2 | `verify.ps1` | **GATE: PASS**, 3,061/0/0 | **GATE: FAIL**: U4 1,236/1 실패 |
| 3 | Golden | 4/4 | 4/4 |
| 4 | Dry Pilot | 9/9 동일 | 9/9 동일(SimHarness DLL은 다르지만 결과 Hash는 같음) |
| 5 | DLL/PDB | 이전 G2/G3 진단 Clone A·B와 동일(B-4 후보 441행 변경 0, 다른 파일은 경로가 든 Coverage 대응표 6개뿐) | CRLF 대비 출력 106개 다름. B-4 후보 24행(SIMHARNESS_BINARY 1, TEST_ASSEMBLY 4, DEPENDENCY 19 — 모두 StarPlayer 빌드물). **VERIFIER_BINARY는 같음**(`33e694ee…`) |
| 6 | PDB 원본 문서 체크섬 | 체크아웃과 372/372 같음 | 체크아웃과 372/372 같음 |
| 7 | GitHub Raw 실제 SHA | 372/372 HTTP 200, Raw = Git Blob 372/372 | 같음 |
| — | **PDB 체크섬 = Raw(같은 파일)** | **71/372** | **372/372** |
| 8 | SourceLink URL·Commit | 55개 어셈블리 모두 `https://raw.githubusercontent.com/junk1901/Star_player/c77ed1a…/*` | 같음 |
| 9 | Godot PCK | 2회 `3a187e19…`, verify Export도 같음 | 2회 `3a187e19…`, verify Export도 같음 |
| 10 | Git 작업 트리 | Checkout·빌드 뒤 변경 0(verify 로그만 바뀌었다가 되돌림) | 같음 |

- **LF 실패 원인(U4):** `TuningV4Tests.The_v4_file_differs_from_the_v3_file_only_in_the_versions_the_provenance_and_the_game_calling_modifier`(`TuningV4Tests.cs` 78행).
  - `content/tuning/virtual_tuning_v4.json`은 저장소에 **CRLF로 저장**되어 있고 `-text`입니다. v1~v3는 LF로 저장되어 있어 CRLF Checkout에서만 CRLF가 됩니다.
  - 시험은 두 내장 리소스의 JSON 원문 텍스트(`GetRawText`)를 비교하므로, LF Checkout에서는 여러 줄 멤버가 모두 다르게 나옵니다.
  - 즉 튜닝 파일이 **내장 리소스 바이트로 빌드 입력**에 들어가며, 그 바이트가 Checkout 설정에 따라 달라집니다(운영 Gate 계획 G2가 이미 지적한 `virtual_tuning_v4.json`·CRLF 문제와 같은 원인).
  - 상세 재실행 로그 `LF\u4_rerun_detail.log`의 두 번째 실패(`OfficialPzl6VerificationTests…end_to_end`, `TestOnlyWorld` 생성자 `DirectoryNotFoundException`)는 제가 이 프로젝트만 다시 빌드해 다른 시험 어셈블리가 없어서 생긴 **재실행 부산물**입니다. `verify.ps1` 본 실행의 실패는 1건입니다.
- **VERIFIER_BINARY가 두 설정에서 같은 이유[사실]:** 저장소 `.gitattributes`가 이미 `tools/StarPlayer.EvidenceVerifier/** text eol=lf`, `tools/StarPlayer.SimHarness/Evidence/**`, `tests/StarPlayer.EvidenceVerifier.Tests/**`, `*.tscn`, 일부 SimHarness 파일 등을 `eol=lf`로 고정합니다. 그래서 검증기 원본은 어느 설정에서도 LF이고, CRLF에서도 검증기 PDB 체크섬 = Raw 15/15입니다. PCK가 같은 것도 `*.tscn text eol=lf` 덕분입니다. 고정되지 않은 것은 `src/`의 게임 코어, 대부분의 `tests/`, SimHarness 일부, `content/tuning/` 등입니다.
- **`eol=` 규칙 추가 효과 사전 확인(추적 파일 변경 없음):** `core.autocrlf=false` Clone에 저장소 local `.git/info/attributes`(추적되지 않음)로 `* eol=crlf`를 Checkout 전에 넣어 봤습니다.
  - D3 5개는 그대로였지만 작업 트리가 **63건 변경 상태**가 되었습니다(저장소에 `i/lf` 717, `i/crlf` 15, `i/mixed` 4, `-text` 95, `i/none` 63이 섞여 있음).
  - CI 빌드는 CRLF 빌드와 42개(Coverage 외) 다르고, LF 빌드와도 110개 다릅니다.
  - 즉 일괄 `eol=` 규칙은 기존 바이트와 같지 않으며, 저장소 재정규화 Commit이 필요합니다. 변경 상태는 생산자 `GitIntegrity`(Git.dirty)도 실패시킵니다.

### 1.3 판단과 추천

- OWNER 판정 규칙에 따르면, LF는 **모든 요구사항을 만족하지 못했습니다**(`verify.ps1` FAIL). 기존 불변 기준(3,061 PASS, Golden, Dry Pilot, PCK)을 만족한 것은 CRLF뿐입니다. 따라서 **차이 분석과 OWNER 결정 요청** 경우입니다.

| 선택지 | 내용 | 기존 불변 기준 | G2 수용 기준("Checkout 설정과 무관하게 같은 바이트") | SourceLink 체크섬 | 소스 Commit 영향 |
|---|---|---|---|---|---|
| E-1 | 공식 절차에서 `git clone -c core.autocrlf=true` 명시 고정 | 충족 | 문구 그대로는 **불충족**(설정 의존). 절차 고정을 "빌드 입력 고정"으로 인정할지 OWNER 해석 필요 | 검증기 15/15, 전체 71/372 | 없음(`c77ed1a` 유지) |
| E-2 | 공식 절차에서 `core.autocrlf=false` | **불충족**(U4 1건 FAIL) | 불충족(설정 의존) | 372/372 | 없음, 그러나 Gate 불가 |
| E-3 | 남은 빌드 입력에 경로별 `eol=` 규칙 확대 + 저장소 재정규화 + 튜닝 v1~v4 줄바꿈 정합(시험 또는 데이터) | 재측정 필요 | 충족 | 고정값에 따라 최대 372/372 | **새 Commit**. `.gitattributes`·재정규화(시험/데이터 변경 가능). 별도 TASK·Codex 검수 |

- **추천:**
  1. 단기·현행 소스로는 **E-1**이 유일하게 모든 기존 불변 기준을 충족합니다. 신뢰 사슬이 직접 Pin하는 VERIFIER_BINARY는 이미 설정과 무관하고, B-4 7절 원격 출처 검증(Raw = Blob, PDB 체크섬 = 체크아웃)도 통과합니다. 부족한 점은 디버거 SourceLink 체크섬(검증기 외 301개)과 G2 문구입니다.
  2. G2 수용 기준을 문구 그대로 지켜야 한다면 **E-3**이 필요합니다. 이미 B1이 검증기·증거 경로에 쓴 방식(`text eol=lf`)을 남은 빌드 입력으로 넓히는 것이므로 방향은 기존 정책과 같습니다. 다만 새 소스 Commit·재측정·Codex 검수가 따르고, 튜닝 v4(CRLF 저장)와 v1~v3의 관계를 시험 또는 데이터에서 정리해야 하며 그 영향(Hash·Golden)을 먼저 확인해야 합니다.
  3. LF 공식 빌드(E-2)는 현행 소스로는 Gate를 통과하지 못하므로 추천하지 않습니다.
- `.gitattributes`와 계약은 바꾸지 않았습니다. 빌드 방식이 바뀌면 기존 후보 SHA는 재사용하지 않습니다.

## 2. G1 파일명·경로 확정 제안 (D-4·D-5)

**G1 범위(운영 Gate 계획):** 공식 결과 루트, `run/<run_id>/` 내부 배치, Preflight 기록 `preflight.json`·`preflight/<unit>.json`의 최종 이름. 수용 기준: 생산자·검증기·시험이 같은 경로, Hash·Replay 불변, 문서와 코드에서 이름 정의 한 곳.

| 이름·경로 | 현재 코드(생산) | TEST_ONLY | 계약 요구 | 제안 |
|---|---|---|---|---|
| 생산자 결과 루트 `build/simharness/official/<run_id>` | `OfficialOutput.RootRelative`(한 곳). 저장소 밖 거부 | 사용 | §21 "출력 루트는 B-4" | 그대로 확정. 단 Producer 전용 Checkout에서 실행(4절) |
| 공식 Archive 루트 | 없음(운영 절대경로) | 임시 경로 | §21·§23 B-4 | 승인 B의 R-ARCHIVE 값 |
| `run/<run_id>/` | `EvidenceWriter.RunPath`(한 곳), 검증기도 같은 함수 사용 | 사용 | 결정 3 §6 | **그대로 확정** |
| Family Preflight `run/<run_id>/preflight.json` | `OfficialOutput.PreflightFile`(한 곳) | 사용 | R2-B | **그대로 확정** |
| Unit Preflight `run/<run_id>/preflight/<unit 소문자>.json` | `PreflightDirectory`·`UnitPreflightName`(한 곳) | 사용 | R2-B | **그대로 확정** |
| Unit Manifest `run/<run_id>/units/<unit>/manifest.json`(Family), `…/cells/<cell>/manifest.json`(Dominance) | 디렉터리 이름은 `OfficialFamilyPlan.UnitDirectory`·`DominancePreflight.CellDirectory` 한 곳. 파일명 `manifest.json`은 **문자열 8곳**(`RunReport` 313, `DominanceOfficial` 167·392·473, `FamilyOfficial` 391·488, `EvidenceWriter` 66, `Verifier` 237) | 사용 | §21 UNIT_MANIFEST | 이름 유지(아래) |
| 실행 색인 `run/<run_id>/family-result.json`·`dominance-result.json` | `FamilyOfficial.ResultFile`·`DominanceOfficial.ResultFile`(한 곳) | 사용 | §21 DIAGNOSTICS | 그대로 |

**`manifest.json` 분석(D-5):**

- **이름 일치:** 8곳 모두 같은 문자열이며, 생산자(쓰기)·검증기(읽기)·시험이 같은 경로를 씁니다.
- **경로 생성 규칙:** 디렉터리는 공유 함수, 파일명만 문자열 반복입니다.
- **이름 변경 영향:** `manifest_path`가 Family·Dominance 실행 색인(DRAFT) 본문에 기록됩니다. 계약 §22는 DRAFT 색인 바이트 변화 0을 요구하므로, 이름을 바꾸면 안 됩니다.
- **자동 탐지:** TEST_ONLY 종단 시험(생산자 → Archive → 검증기)이 같은 경로를 쓰므로, 한쪽만 바뀌면 시험이 실패합니다.
- **공유 상수화(문자 그대로의 "한 곳 정의"):** 이름 바이트·DRAFT·Hash·Replay는 그대로지만, SimHarness·검증기 어셈블리 바이트가 바뀝니다(SIMHARNESS_BINARY·VERIFIER_BINARY 변경 → 새 소스 Commit, Codex 검수). 시험을 추가해도 TEST_ASSEMBLY가 바뀝니다.
- **제안:** 이름 유지. G1 승인 조건은 완화하지 않고 OWNER가 둘 중 하나를 정합니다.
  - G1-a: 이 보고서의 **단일 규범 표**(위 표)를 공식 정의로 채택하고, 코드의 문자열 반복은 종단 시험으로 일치를 보장한다고 해석
  - G1-b: 공유 상수로 코드 정리(별도 TASK, 소스 Commit 변경, Hash·Replay 불변 확인)

**그 밖의 이름(D-8 연계, 4번째 열 참고):** 아래 표.

| 역할 | 파일명·상대경로(현재) | 해석 Root | 작성 주체 | 검증 주체·방식 | 상태 |
|---|---|---|---|---|---|
| Approval Record | TEST_ONLY `trust/approval-record.json` | Archive Root | OWNER가 Registry에 등록(원본). Archive 사본은 생산자 | 검증기: Registry에서 ID로 조회(L2)한 원문이 신뢰 출처, Archive 사본은 SHA 대조용 ORIGINAL | 이름은 B-4 후보. 공식 생산 코드 없음 |
| Build Provenance | TEST_ONLY `trust/build-provenance.json` | Archive Root | OWNER 등록, 사본은 생산자 | 검증기: Registry 원문, T5·T6 | 같음 |
| Environment Profile | TEST_ONLY `trust/environment-profile.json`(INPUT_BUNDLE) | Archive Root | OWNER 등록, 사본은 생산자 | 검증기: Registry 원문·E2-4, 영수증 INPUT_BUNDLE SHA 집합(R2-A) | B-4 P-A 추천 유지 |
| PZL Test Profile | TEST_ONLY `trust/pzl-test-profile.json`(INPUT_BUNDLE) | Archive Root | OWNER 등록, 사본은 생산자 | 같음 | 같음 |
| Regression Proof | TEST_ONLY `trust/regression-proof-<guid>.json` | Archive Root | 생산자(시험 결과) | 검증기: §12.3 TestEvidence 독립 확인(Test Profile 직접 실행) | B-4 후보 `proof/<pzl_test_profile_id>.json`, OWNER 결정 |
| Family Result | 생산 고정 이름 없음(TEST_ONLY 호출자 지정, 예 `integration-result.json`) | Archive Root | 생산자 | 검증기 전체 재검증 | B-4 후보 `calibration-result.json`·`integration-result.json` |
| Dominance Result | TEST_ONLY `dominance-result.json` | Archive Root | 생산자 | 같음 | 실행 색인과 이름이 같지만 경로가 다름(`run/<run_id>/dominance-result.json`). 그대로 두되 문서에 구분 명시 |
| PZL6 | TEST_ONLY `pzl6-<stem>.json`, `pzl6/<stem>/<unit>-NN.json`. 생산 `Archive`가 Artifact ID 형태(`pzl6-json-<mode>-NN`)를 검사 | Archive Root | 생산자 | 검증기 `Pzl6Verifier` | B-4 후보 그대로 |
| TRUST_CONFIG | Build Provenance artifact 행(`relative_path`·`raw_sha256`) | **해석하지 않음**(현재 G4) | OWNER(Provenance 작성) | 검증기 T6: TRUST_CONFIG 행이 정확히 1개이고 `raw_sha256` = 앵커 SHA인지만 확인(`TrustBootstrap.cs` 59~60행) | 현재 동작 유지. `relative_path` Root는 B-4 10.2절 [충돌] 그대로 OWNER 결정 |
| 실제 Trust Anchor | 설치 설정 `anchor_path`(R-TRUST) | 운영 절대경로(설치 설정 `%ProgramData%\StarPlayer\EvidenceVerifier\install-config.json`) | OWNER 관리 세션 | 검증기 T1·T2(경로·ACL·Reparse) | 승인 B |

- 새 Schema 필드나 승인 규범은 제안하지 않았습니다.

## 3. 정식 Evidence Archive 조립 방식 (D-6)

### 3.1 핵심 사실

- **계약이 공식 실행 경로를 B-1 밖에 둡니다.**
  - §14.2: "승인 기록이 유효해도 `OfficialApprovals.Current.ExecutionPathReady=false`는 바뀌지 않습니다. 공식 실행 경로 연결은 B-1 범위 밖입니다."
  - §14.4: "B-1의 어떤 산출물도 공식 실행 경로를 켜지 않습니다."
  - §22: "`OfficialApprovals.Current`·CLI 변경 없음, NOT_READY 유지"
- 따라서 공식 Archive의 내용(공식 Calibration·Integration·Dominance 실행 결과)이 **현재 계약에서는 만들어질 수 없습니다.** 조립기 유무와 무관합니다. 4절 실험에서도 `official-cal`은 `Official.not_ready`로 거부되었습니다.
- **운영 Gate G1~G9의 정의에는 공식 Archive 생성이 들어 있지 않습니다.** 운영 Gate 계획은 Final Execution Freeze 준비 뒤의 "Official Execution 착수"를 이 계획 범위 밖으로 둡니다. Archive 조립은 그 단계의 요구사항입니다.
- TEST_ONLY 조립은 `TestOnlyWorld.Production`이 생산 API만으로 합니다(`EvidenceWriter.Observe/WriteUnits/WriteSeeds/WriteRun/WriteDocument`, `OfficialDocuments.Family/Dominance`, `Pzl6Verifier.Bundle`, `TestProfiles.RegressionProof`, `Archive.WriteNew`). 다만 실행 결과를 **같은 프로세스 메모리의 `FamilyOfficialResult`**에서 받습니다. 결과 폴더에서 다시 읽어 조립하는 생산 API는 없습니다.
- §21 작성 순서: 번들 → Unit PZL → PZL6 → Family/Dominance → RAW_INVENTORY → CANONICAL_PAYLOAD → 영수증. 앞의 넷은 생산자, 뒤의 셋은 검증기(`Certification`·`Receipts`, 이미 생산 코드)입니다.

### 3.2 세 대안 비교

| 항목 | A. SimHarness에 조립 추가 | B. 별도 버전 관리 Assembler 도구 | C. 현재 기능 조합만 |
|---|---|---|---|
| 가능 여부(현 계약) | 공식 실행 경로가 꺼져 있어 공식 Archive 불가 | 같음 | **불가**: 조립 CLI가 없고 TEST_ONLY만 조립 가능 |
| 코드 변경 | SimHarness(공식 실행 뒤 조립 단계, 결과 폴더 재읽기 또는 실행 직후 조립) | 새 도구(SimHarness·검증기 어셈블리를 라이브러리로 참조, 결과 폴더 재생 후 조립) | 없음 |
| 계약 영향 | §14.4·§22(`Execute` 경로·CLI)와 맞물림. 공식 실행을 켜는 변경은 계약 변경 | 도구 자체는 계약 역할 밖. 닫힌 Provenance 역할에 Assembler가 없으므로 **검증기가 신뢰하지 않는 도구**여야 함(검증기는 Archive 모든 바이트를 Raw SHA·승인 Pin·Replay로 재검증하므로 성립 가능) | 없음 |
| 공식 소스 Commit 영향 | SIMHARNESS_BINARY 변경 → 새 Commit | SimHarness·검증기 불변 가능. 도구는 별도 저장소 경로·별도 검수 | 없음 |
| 검수 범위 | SimHarness 공식 경로 전체(크게) | 도구 + 검증기가 도구를 신뢰하지 않는다는 반례 | — |
| 필요한 회귀 | 기존 3,061 + Hash·Replay·Golden 불변 + 조립 종단 시험 | 기존 회귀 불변 + 도구 종단 시험(조립 → 검증기 TEST_ONLY 인증) + 변조 반례(누락·중복·추가 파일, 바이트 교체, 순서 위반, 덮어쓰기) | — |

- **추천:**
  1. B1(운영 G1~G9)의 선행 조건으로는 **구현하지 않습니다.** 계약상 공식 실행이 B-1 밖이고, G1~G9는 공식 Archive를 요구하지 않습니다. 이 결론이면 Archive 조립은 **최종 소스 Commit을 바꾸지 않습니다.**
  2. Official Execution 단계에서 필요해지면 **B(별도 Assembler)**를 추천합니다. 변경 범위가 가장 작고 SIMHARNESS_BINARY·VERIFIER_BINARY를 건드리지 않을 수 있습니다. 단 공식 실행 경로를 켜는 일(OfficialApprovals 출처, CLI)은 별도로 계약·OWNER 결정이 필요하며, 그때 SimHarness 변경이 생깁니다.
  3. 그 시점의 TASK 초안: 범위(결과 폴더 재생 → §21 순서 조립 → `Archive.WriteNew` 무덮어쓰기), 신뢰 경계(검증기가 Assembler를 신뢰하지 않음), 반례 목록(위 표), Codex 독립 검수.
- 이번에는 격리 프로토타입을 만들지 않았습니다. 계약상 공식 입력이 생기지 않아 TEST_ONLY 조립(이미 `TestOnlyWorld`가 함)과 차이가 없기 때문입니다.

## 4. Producer 분리 가능 여부 (D-7)

**실험(진단, 제품 변경 없음):** 공식 원격의 새 Clone(빌드 산출물 없음)을 Producer Checkout으로 쓰고, 현재 디렉터리를 그 Checkout으로 둔 채 R-BUILD 대역(G2/G3 진단 Clone B)의 CI 빌드 SimHarness(`b22d46a1…`)로 `official-cal`을 실행했습니다.

| 확인 | 결과 |
|---|---|
| 1. Producer Checkout의 Commit 결속 | Preflight `git_integrity` PASS(빌드 산출물 없는 Clean Checkout). Attestation의 `git_commit_sha`는 이 Checkout의 HEAD(`git rev-parse`)에서 옵니다(`FamilyOfficial.cs` 414행, `DominanceOfficial.cs` 378행) |
| 2. 실행 위치·Repository Root 인식 | Root는 **현재 디렉터리에서 위로 `StarPlayer.sln`을 찾아** 정합니다(`Program.cs` 315~321행). 바이너리 위치와 무관합니다 |
| 3. 출력 경로 제한 | `output_directory` PASS: `E:\Madegame\g3x\prodsep-producer\build\simharness\official\prodsep-cal-1`. Producer Checkout 안입니다 |
| 공식 실행 | `Official.not_ready`(`git_sha_not_approved`, `family_seed_manifests_not_frozen`, `execution_policy_not_frozen`, `ExecutionPathReady=false`)로 거부. 게임 미실행, 파일 미생성 |
| 반례: Producer Checkout에 빌드 산출물 | `Git.build_output_unverified`로 거부("F-C2-A 미구현") |
| 7. R-BUILD 쓰기 | R-BUILD 대역 2,355개 파일 변경 0, HEAD·상태 불변 |
| 6. 검증기가 Producer 작업 Root를 신뢰하는지 | **신뢰하지 않습니다**[코드]. 검증기는 Archive 스냅샷만 읽고(`EvidenceWriter.WriteRun` 주석: "the verifier … never reads the result directory itself"), 재생은 `%TEMP%\starplayer-verifier\<guid>`에 Archive 바이트를 복사해 합니다(`Verifier.cs` 245행). 승인 입력은 검증기 자신의 Repository Root(R-BUILD)에서 읽습니다 |
| 4·5. 결과를 R-ARCHIVE로 옮기기, 원본·보존 바이트 SHA | 생산 명령 없음(3절). `Archive.WriteNew`가 무덮어쓰기·쓴 바이트 재확인을 하므로, 조립기가 생기면 이 함수로 옮기는 것이 계약 §21 보존 규칙과 맞습니다 |

- **결론: 현재 코드로 분리 가능합니다.**
  - R-BUILD(승인 바이너리, 봉인·읽기 전용) — Producer는 실행 권한만
  - Producer 전용 Checkout(빌드 산출물 없음, 승인 Commit의 Clean 상태) — Producer 쓰기
  - R-ARCHIVE(계약 권한) — 조립 단계에서 기록
  - R-WORK — 검증기 전용
- **정확한 제약:**
  - Producer Checkout은 **빌드 산출물이 전혀 없어야** 하고(`bin`·`obj` → `build_output_unverified`), 그 밖의 무시 파일도 없어야 합니다(이전 결과의 `build/` → `Git.dirty`). 따라서 공식 실행 1회마다 새 Checkout 또는 결과 이동 후 정리가 필요합니다.
  - Producer는 자기 빌드가 아니라 R-BUILD의 승인 바이너리를 실행해야 합니다(빌드 산출물 결속 F-C2-A가 미구현이기 때문).
  - 공식 실행 자체는 계약상 NOT_READY입니다(3절).
- **G5 관찰(코드 변경 제안 아님):** 검증기 재생 디렉터리가 R-WORK가 아니라 검증기 계정의 `%TEMP%`입니다. G5 방향 D-5("R-WORK는 TEMP가 아님")는 Test Profile 결과 폴더에 적용되어 있습니다. 운영에서 검증기 서비스 계정의 TEMP가 다른 주체에게 쓰기 불가인지 승인 B 실측 항목(M-10)에 추가할 것을 권합니다.

## 5. 최종 소스 Commit에 미치는 영향

| 결정 | 코드 변경 없이 가능한 선택 | 소스 Commit을 바꾸는 선택 |
|---|---|---|
| D-3 줄바꿈 | E-1(절차에서 `core.autocrlf=true` 고정) | E-3(`eol=` 확대·재정규화, 튜닝 정합) |
| D-4/D-5 G1 | G1-a(규범 표 채택) | G1-b(공유 상수) — VERIFIER_BINARY도 바뀜 |
| D-6 Archive | B1 범위 밖으로 둠 | Official Execution 단계의 별도 결정 |
| D-7 Producer | 운영 구조로 분리(4절) | — |
| D-8 이름 | 문서 확정 | — |

- 위의 왼쪽 열을 모두 택하면 **`c77ed1a`를 최종 A1-6 소스로 유지할 수 있습니다.** 이번 진단 결과(두 Clone, CRLF 재현, PCK, SourceLink)가 그대로 공식 빌드의 교차 검증 기준이 됩니다.
- 오른쪽 열을 하나라도 택하면 새 소스 Commit이 생기며, 그 Commit으로 G2/G3 진단을 다시 합니다(기존 후보 SHA 재사용 금지).

## 6. 공식 G2/G3 승인에 남는 최소 조건

1. **D-3:** E-1 채택과 G2 문구 해석, 또는 E-3 TASK 승인
2. **G1:** 2절 표 채택(G1-a) 또는 G1-b TASK
3. **D-6:** Archive 조립을 B1 범위 밖(Official Execution 단계)으로 두는 것에 대한 OWNER 확인
4. **A1-6:** 위 결정에 따른 최종 소스 Commit(E-1·G1-a면 `c77ed1a`)
5. **승인 B:** 운영 기계·R-BUILD·DEPLOYER 계정(공식 빌드 장소)
6. **공식 실행:** 운영 기계에서 이 절차(두 Clone, verify, Golden, Dry Pilot, CI 빌드, Artifact, SourceLink 체크섬 — 정책에 맞는 기준, PCK 5회) → Codex 독립 검수 → OWNER 수용

## 7. 증거 관리와 기록

- 기존 `phase3ops\g23`, `pck-out\G23*`은 그대로 보존했습니다.
- **정정 기록(이전 보고 유지):** `g4clean3`의 Dry Pilot Archive 사본 삭제, 첫 Dry Pilot 실패 로그 덮어쓰기. 원본 Archive와 남은 사본 2개는 목록 SHA `be6f927d…`로 결속되어 있습니다.
- **이번 작업부터:** 진단 스크립트(`eol_clone_pipeline.ps1`)는 같은 이름의 로그가 있으면 `.runN.log`로 새로 쓰고 덮어쓰지 않습니다.
- **이번 작업의 한계·실수:**
  - `eol_attr_probe.ps1`의 기록 함수가 PowerShell 변수 대소문자 무시 때문에(`$l`과 `$L`) 자체 로그를 쓰지 못했습니다. 측정값은 화면에 출력되었고, 같은 값을 Clone과 보존 Manifest에서 다시 계산해 `ATTR\probe_rederived.log`에 남겼습니다.
  - LF 시험 상세 재실행의 두 번째 실패는 재실행 부산물입니다(1.2절).
- 진단 산출물은 공식 Provenance로 승격하지 않았습니다.

## 8. 증거 해시 (SHA-256, 앞 8자리, 저장소 밖 `E:\Madegame\g3x\phase3ops\`)

| 파일 | SHA-256 |
|---|---|
| `eol\eol_clone_pipeline.ps1` · `eol\eol_run_all.ps1` · `eol\run_all.log` | `8df22b10` · `4e2adf8b` · `c281da22` |
| `eol\CRLF\summary.json` · `eol\LF\summary.json` | `34673f61` · `454e1cbf` |
| `eol\CRLF\ci_manifest.tsv` · `eol\LF\ci_manifest.tsv` | `fc032e4f` · `7b2c096d` |
| `eol\CRLF\pdb_documents.json` · `eol\LF\pdb_documents.json` | `7ed97e72` · `4443e4ee` |
| `eol\LF\u4_rerun_detail.log` | `ac890e0e` |
| `eol\eol_attr_probe.ps1` · `eol\ATTR\ci_manifest.tsv` · `eol\ATTR\probe_rederived.log` | `636a62d9` · `7da5de65` · `8adb0fd3` |
| `prodsep\prodsep_experiment.ps1` · `prodsep\experiment.log` | `d52c09ee` · `3f1b0027` |
| `prodsep\run1_official_cal.log` · `run2_official_cal_again.log` · `run3_with_build_output.log` | `c2b82a66` · `59c83583` · `e119051e` |
