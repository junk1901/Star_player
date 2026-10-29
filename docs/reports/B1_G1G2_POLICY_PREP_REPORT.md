# B1 G1·G2 조건부 정책·공식 G2/G3 준비 보고서

- 작성: Claude Code, 2026-10-29. 근거: OWNER 지시 "G1/G2 조건부 정책 선택 및 G2/G3 공식 실행 준비"(2026-10-29)
- **성격:** 정책 결정안·비운영 진단입니다. 공식 Gate PASS, 공식 소스 동결, Provenance 등록이 아닙니다. Push·Tag·계약·`.gitattributes`·제품·G4·G5 코드 변경은 없습니다. 새 도구 1개(`tools/b1-ops/g1-names-check.ps1`, 읽기 전용 정적 검사)를 추가했습니다.
- 기준: 원격 `main` = `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f`. 로컬 기록(미게시) `e8f8f72`·`200f448`·`92297cb`·이 보고서 Commit.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 제출물

| # | 제출물 | 위치 |
|---|---|---|
| 1 | G2 E-1 변경 결정안 | [조건부 결정안](../tasks/B1_G1_G2_CONDITIONAL_DECISION_PROPOSAL.md) A절 |
| 2 | G1 G1-a 변경 결정안 | 같은 문서 B절, [B-4 빌드 절차 초안](../tasks/B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md) 6.1절 규범 표, `tools/b1-ops/g1-names-check.ps1` |
| 3 | Codex 검수 요청서 | [정책 검수 요청서](../tasks/B1_G1_G2_POLICY_CODEX_REVIEW_REQUEST.md) |
| 4 | SourceLink 체크섬 영향 | 2절, 결정안 A.6 |
| 5 | G5 TEMP 신뢰 경계 분석 | 3절, [운영 전환 계획](../tasks/B1_PHASE3_OPERATIONS_TRANSITION.md) 3.2절 M-12 |
| 6 | `c77ed1a` 공식 소스 동결 가능 여부 | 4절 |
| 7 | 공식 G2/G3 실행 전 최소 조건 | 5절 |

## 2. SourceLink 체크섬 영향 (301개 파일)

- E-1(`core.autocrlf=true`) 빌드의 원본 추적 문서 372개를 하나씩 대조했습니다(`sourcelink_reconcile.ps1`).
  - Git Blob = GitHub Raw: 372/372(이전 진단에서 실제 조회로 확인)
  - PDB 체크섬 = Raw: **71**
  - PDB 체크섬 = Raw에 LF→CRLF 변환을 적용한 바이트: **301**
  - 설명할 수 없는 것: **0**
- 301개의 구성: 게임 코어 `src/` 110(Application 22, Domain 32, Infrastructure 8, Simulation 48), 게임 클라이언트 7, SimHarness 16, 시험 168.
- PDB = Raw인 71개는 `.gitattributes`가 `eol=lf`로 고정한 경로입니다(검증기 15, 검증기 시험 17, SimHarness 22 등).
- **판정:**
  - 승인 B1-T00 계약에는 SourceLink·PDB 체크섬 요구가 없습니다(계약 전문 검색).
  - B-4 SourceLink 출처 검증(규범 결정 자료 1.3절)은 처음부터 "Raw는 Blob과, PDB 체크섬은 체크아웃 파일과 비교"로 정해져 있고, 둘 다 통과합니다.
  - 검증기는 SourceLink를 판정에 쓰지 않습니다.
  - 따라서 **충돌은 없고**, 남는 것은 디버거에서 301개 원문의 체크섬 경고입니다. E-1 공식 PASS를 막는 조건이 아닙니다.

## 3. G5 TEMP 사용의 신뢰 경계 분석

**코드에서 TEMP를 쓰는 곳(생산 코드 전체 검색):**

| 위치 | 경로 | 공식 검증 경로인가 |
|---|---|---|
| `Verifier.cs` 245행 | `Path.GetTempPath()\starplayer-verifier\<guid>`(Replay 재료) | **예** |
| `SimHarness/Program.cs` 87행 | 저장소 Root를 못 찾을 때 `run` 출력의 대체 경로 | 아니오(생산자 비공식 실행) |
| `Infrastructure/SqliteStorageDiagnostics.cs` 18행 | Godot 저장소 진단 | 아니오 |

**검증기 Replay 디렉터리:**

| 구분 | 내용 |
|---|---|
| 1. 생성 파일 | 검증 스냅샷의 실행 색인(`family-result.json`·`dominance-result.json`)과 Unit별 `manifest.json`. `Archive.WriteNew`로 새 GUID 디렉터리에 쓰고(덮어쓰기 금지, 쓴 바이트 재확인), 검증 끝에 디렉터리 전체 삭제 |
| 2. 판정 입력 여부 | **예.** 기존 Replay(`FamilyOfficialReplay.Verify`·`DominanceOfficialReplay.Verify`)가 이 파일들을 **경로로 다시 읽습니다.** Manifest는 색인에 적힌 `manifest_sha256`과 비교하지만, 색인 자체는 다시 읽을 때 스냅샷 SHA와 대조하지 않습니다(`FamilyOfficial.cs` 457~494행). 최종 판정은 Archive 문서와 재실행 결과(승인 Seed·Pin에서 도출)를 비교하지만, "Manifest 기록값 = 재실행" 대조는 TEMP의 사본으로 합니다 |
| 3. 실행 파일·모듈 | **아니오.** TEMP에서 코드를 로드하지 않습니다. 검증기에 Pin되지 않은 모듈이 로드되면 G4 V-2가 거부합니다 |
| 4. Writer·Owner | 검증기 서비스 계정의 `TMP`/`TEMP`(.NET은 `TMP` → `TEMP` → 사용자 프로필 순). 계정 형태에 따라 사용자 프로필 Temp 또는 `C:\Windows\Temp`. **운영 미확정.** [실측: 개발 PC] 개발 계정의 `C:\Users\user\AppData\Local\Temp`는 소유자·SYSTEM·Administrators 외에 `CodexSandboxUsers` 그룹과 다른 SID 하나에 **Modify**(`(OI)(CI)(M)`)를 줍니다. 즉 프로필 Temp가 기본으로 비공개라고 가정할 수 없습니다. `C:\Windows\Temp`는 비승격 세션에서 ACL을 읽지 못해 **미검증**입니다 |
| 5. R-WORK와의 관계 | R-WORK(G4 `work_root`)는 Test Profile 결과(TRX)에만 쓰입니다. Replay 디렉터리는 R-WORK 밖입니다 |
| 6. G4 보장 영향 | 재생 디렉터리를 다른 주체가 **수정**할 수 있으면, 쓰기와 다시 읽기 사이에 색인과 Manifest를 함께 바꿔 "Archive Manifest 기록값 ≠ 재실행"인 상태를 숨길 수 있습니다. 위 개발 PC 예처럼 다른 주체에게 Modify를 상속하는 TEMP라면 이 조건이 성립할 수 있습니다. 추가 생성만 가능한 경우는 `WriteNew`의 기존 경로 거부로 실패(서비스 거부)입니다. 즉 보장은 **TEMP ACL이라는 운영 조건**에 의존합니다 |

- **분류:** 새 조건부 보안 Finding **G5-TEMP-01**. 판정 입력이 G5가 통제하지 않는 위치를 거칩니다. 제 판단으로는 TEMP가 보호되지 않은 운영 배치에서는 MAJOR, M-12가 충족되면 해소입니다. Codex 판정을 요청합니다. 이 실측 전에는 아래대로 해당 운영 절차(공식 인증)를 중단합니다.
- **제품 코드 변경 없는 해결:** 검증기 서비스의 `TMP`·`TEMP`를 검증기 전용 보호 디렉터리(예: `R-WORK\verifier-tmp`)로 지정합니다(서비스 환경 설정). 이를 G5 운영 실측 **M-12**로 추가했습니다.
  - 확인 항목: `GetTempPath()` 실측, ACL(`g5-acl Verify`·`g5-probe`), TEST_ONLY 인증 1회로 재생 디렉터리 위치·삭제 확인, E2-1 환경 블록 digest·Profile 반영.
  - `TMP`는 금지 접두사(`DOTNET_`·`COMPlus_`)가 아니므로 E2-1에 걸리지 않습니다. 값은 환경 블록 digest에 들어가므로 Environment Profile(G8)과 함께 정합니다.
- **코드 강화(선택, 지금 하지 않음):** 재생 직전 색인·Manifest를 다시 읽을 때 스냅샷 SHA와 대조하거나, 재생에 메모리 바이트를 넘기는 방식. 수용된 G4 코드 변경이므로 OWNER가 원할 때만 별도 TASK·Codex 검수로 다룹니다.
- **운영 절차 중단 여부:** M-12로 보호 위치를 확인하기 전에는 공식 인증 실행(G4 공식 이후)을 하지 않습니다. 이번 G2/G3 빌드 재현성 절차는 검증기 Replay를 쓰지 않으므로 이 Finding의 영향을 받지 않습니다.

## 4. `c77ed1a` 공식 소스 동결 가능 여부

- **조건부로 가능합니다.**
  - 결정안 A(E-1)와 B(G1-a)가 Codex 검수 뒤 OWNER 승인되면, 코드·`.gitattributes` 변경 없이 `c77ed1a`를 공식 빌드 소스로 쓸 수 있습니다.
  - G5-TEMP-01을 운영 설정(M-12)으로 해결하면 소스 변경이 필요 없습니다.
  - Archive 조립은 B1에서 구현하지 않습니다(계약 §14.2·§14.4·§22, OWNER 지시 5항).
- **아직 동결을 선언하지 않습니다.** OWNER 지시 1항대로, G1/G2 조건부 결정이 승인되기 전에는 공식 소스 동결 완료가 아닙니다.
- 다음 중 하나라도 택해지면 새 소스 Commit이 필요하고, G2/G3 진단을 그 Commit으로 다시 합니다(기존 SHA 재사용 금지): E-3, G1-b, G5-TEMP-01 코드 강화.
- Build Provenance의 `source_git_sha`는 반드시 실제 빌드 Commit과 같아야 합니다. 제품 코드가 같다는 이유로 다른 Commit의 바이너리 SHA를 쓰지 않습니다.

## 5. 공식 G2/G3 실행 전 남은 최소 조건

1. Codex READ-ONLY 검수: 결정안 A·B와 G5-TEMP-01
2. OWNER 결정: G2-E1 수용 기준, G1-a 수용 기준(또는 E-3·G1-b)
3. OWNER 결정: A1-6 공식 소스 Commit(`c77ed1a` 후보) 동결
4. 승인 B: 운영 기계·R-BUILD·DEPLOYER 계정(공식 빌드 장소). G4 공식 인증 단계 전에는 M-12도 필요
5. 공식 실행: 운영 기계에서 결정안 A.3·A.5 절차, `g1-names-check.ps1` PASS, PCK 5회 → Codex 독립 검수 → OWNER 수용

## 6. 작업 기록과 한계

- **이번 작업의 진단 실행:**
  - `sourcelink_reconcile.ps1`: 첫 세 번은 제 스크립트 결함으로 실패했습니다.
    - `$sha`와 `$Sha`가 같은 변수였습니다(PowerShell 변수 이름은 대소문자를 구분하지 않음).
    - 함수 이름 `H`가 내장 별칭 `Get-History`에 가려졌습니다.
    - 주석이 함수 본문을 삼켰습니다.
    - 네 번째 실행이 성공했습니다. 실패 로그도 `console.log`·`run2`·`run3`으로 보존했고, 성공은 `run4`입니다.
  - `g1-names-check.ps1`:
    - 첫 실행은 PS 5.1의 매개변수 기본값에서 `$PSScriptRoot`가 비어 실패했습니다.
    - 두 번째 실행의 자체 시험에서 정규식 2개가 반례를 놓친 것을 찾아 고쳤습니다.
    - 세 번째 실행에서 반례 6건이 모두 잡혔습니다. 모든 회차의 로그를 보존했습니다.
- 기존 증거(`phase3ops\g23`·`eol`·`prodsep`, `pck-out\G23*`·`EOL*`)는 그대로 보존했습니다.

## 7. 증거 해시 (SHA-256 앞 8자리, 저장소 밖 `E:\Madegame\g3x\phase3ops\policy\`)

| 파일 | SHA-256 |
|---|---|
| `sourcelink_reconcile.ps1` · `sourcelink_reconcile.tsv` | `427b03ee` · `e5b68703` |
| `sourcelink_reconcile_console.log` · `run2` · `run3` · `run4`(성공) | `50a522a9` · `7b7fa423` · `ad0f3999` · `7c0150bc` |
| `g1_names_check_worktree.log` · `run2` · `run3` | `6f9d129a` · `7795006c` · `b6805117` |
| `g1_names_check_c77ed1a.log` · `run2` · `run3` | `7795006c` · `7795006c` · `b6805117` |
| `g1_names_check_selftest.log` · `run2` · `run3` | `75e60fec` · `c0c77954` · `08e584f5` |
| 저장소 `tools/b1-ops/g1-names-check.ps1` | `28f07574c480fd7588f63f5ce3ce46eebac4f6d87520237449adabab669d8fcd` |
