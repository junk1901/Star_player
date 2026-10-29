# B1 G1·G2 수용 기준 조건부 변경 결정안 (OWNER 결정 부록 초안)

- 작성: Claude Code, 2026-10-29. 근거: OWNER 지시 "G1/G2 조건부 정책 선택 및 G2/G3 공식 실행 준비"(2026-10-29) 3·4항
- **성격: 결정안(초안)입니다.** OWNER 승인 전에는 효력이 없습니다. Codex READ-ONLY 검수 대상입니다([검수 요청서](B1_G1_G2_POLICY_CODEX_REVIEW_REQUEST.md)).
  - 승인 B1-T00 계약, D3 보호 파일, `.gitattributes`, 제품·검증기·시험 코드, Golden·Hash·RNG·Replay는 바꾸지 않습니다.
  - 기존 G1·G2 조건을 조용히 재해석하지 않습니다. 아래는 **기존 조건을 명시적으로 바꾸자는 제안**이며, 바뀌는 부분과 빠지는 보장을 적었습니다.
- 근거 자료: [G2/G3 진단 보고서](../reports/B1_G2G3_DIAGNOSTIC_REPORT.md), [선행 문제 보고서](../reports/B1_G2G3_PREREQ_REPORT.md), [정책 준비 보고서](../reports/B1_G1G2_POLICY_PREP_REPORT.md)
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
- **개정 1 (2026-10-30):** Codex 판정(FIX_REQUIRED, G2 E-1 ACCEPTABLE, G1-a NEEDS_CHANGE, G1-POLICY-01 MINOR)에 따라 B.3-3의 정적 검사를 강화했습니다(B.7). A절(G2-E1)은 바꾸지 않았습니다. B.3·B.4·B.6 원문은 이력으로 남깁니다.

---

## A. G2 — E-1 통제된 Checkout 조건부 수용 기준

### A.1 기존 G2 수용 조건 (원문, [운영 Gate 계획](B1_OPERATIONAL_GATE_PLAN.md) 2절)

> 작업: CRLF/LF 정책과 운영 빌드 입력 고정. 실측: `content/tuning/virtual_tuning_v4.json` 작업 트리 CRLF(system `core.autocrlf=true`)·blob LF. `TASK.md`·`docs/tasks/2026-10-25_…`는 CRLF이며 `-text` 규칙 없음
>
> 수용 기준: 빌드 입력이 체크아웃 설정과 무관하게 같은 바이트(`-text` 또는 `eol=` 고정)이고, 내장 리소스 SHA = 고정 바이트, 보호 파일이 커밋·체크아웃 왕복 후 SHA 유지(D3)
>
> OWNER 결정: LF·CRLF 중 무엇으로 고정할지. 바이트가 바뀌면 튜닝 의미 Hash는 같아도 내장 리소스 Raw SHA·SimHarness 바이너리 SHA가 바뀜

### A.2 E-1 진단 결과 (2026-10-29, 개발 PC, `c77ed1a`, 진단)

| 항목 | `core.autocrlf=true`(E-1) | `core.autocrlf=false` |
|---|---|---|
| D3 보호 파일 5개 | 일치 | 일치 |
| `verify.ps1`(3,061) | PASS | FAIL(U4 1건, 튜닝 v4 CRLF 저장·v1~v3 LF) |
| Golden 4/4 · Dry Pilot 9/9 · PCK | 모두 같음 | 모두 같음 |
| 빌드 바이트 재현(다른 경로 Clone 3개) | 같음(Coverage 대응표 외 0 차이, B-4 후보 441행 동일) | 1개 Clone만 측정 |
| VERIFIER_BINARY | `33e694ee…` | 같음(이미 `eol=lf`) |
| PDB 체크섬 = GitHub Raw | 71/372, 나머지 301은 Raw의 LF→CRLF 변환과 정확히 일치(설명 불가 0) | 372/372 |

- 문구 그대로의 G2("체크아웃 설정과 무관")는 **E-1로 충족되지 않습니다.** CRLF와 LF에서 StarPlayer 빌드 바이트가 다릅니다(B-4 후보 24행).
- 단순 `eol=` 속성 추가는 기존 바이트와 같지 않고 작업 트리 63건을 변경 상태로 만듭니다(재정규화 Commit 필요).

### A.3 제안하는 통제된 Checkout 수용 조건 (G2-E1)

G2를 다음 조건이 **모두** 참일 때 충족된 것으로 봅니다.

1. **Checkout 설정 고정:** 공식 빌드 Clone은 `git clone -c core.autocrlf=true --no-hardlinks https://github.com/junk1901/Star_player.git <dir>`로 만듭니다. 설정은 첫 Checkout 전에 적용되어야 하며, Clone 직후 `git config --local --get core.autocrlf`가 `true`여야 합니다.
2. **원격·Commit:** `remote.origin.url`이 위 문자열과 대소문자까지 같고(`-ceq`), `git checkout --detach <source_git_sha>` 뒤 HEAD = `source_git_sha`, Tree = 기록값.
3. **Clean:** Checkout 직후 `git status --porcelain --untracked-files=all` 결과 0건, 무시 파일 0건(새 Clone).
4. **입력 바이트 확인:** D3 보호 파일 5개 SHA 일치. 추가로 **승인 입력 바이트 목록**(아래 A.5의 기준 Manifest: 이 설정에서 Checkout된 빌드 입력 파일의 상대경로·SHA-256 전체 목록)과 Clone의 실제 바이트가 같아야 합니다. 다르면 빌드하지 않고 중단합니다.
5. **SDK·빌드 옵션:** .NET SDK 8.0.425, `dotnet build StarPlayer.sln -c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false -p:ContinuousIntegrationBuild=true`, 환경 변수 `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `UseSharedCompilation=false`.
6. **내장 리소스:** 내장 리소스(튜닝 등)의 바이트는 4의 입력 바이트 목록으로 고정됩니다(Checkout 설정이 1로 고정되므로 고정 바이트).
7. **불일치 시 중단:** 1~6 중 하나라도 다르면 공식 빌드·Gate 판정을 하지 않고 OWNER에 보고합니다.

### A.4 기존 조건에서 빠지는 보장

| 기존 G2가 보장하던 것 | E-1에서 |
|---|---|
| 어떤 `core.autocrlf`(또는 Git 전역 설정)로 Clone해도 같은 빌드 입력 바이트 | **보장하지 않음.** 다른 설정의 Clone은 다른 바이트를 만들 수 있고, 그 빌드는 공식이 아님(A.3-1·4에서 거부) |
| 저장소 바이트(Blob)만으로 빌드 입력이 정해짐 | **보장하지 않음.** 입력 = Blob + 문서화된 CRLF 변환(A.3-1) |
| 디버거 SourceLink가 받은 원문과 PDB 체크섬의 일치 | **301개 파일에서 보장하지 않음**(A.6) |

- 유지되는 보장: D3 보호 파일 왕복 SHA, 같은 설정·같은 Commit에서의 바이트 재현성(경로 무관), VERIFIER_BINARY의 설정 독립성(기존 `eol=lf`), Golden·Dry Pilot·PCK.

### A.5 재현성 검증 방법 (공식 G2/G3에서)

1. 서로 다른 경로의 공식 Clone 2개를 A.3대로 만듭니다(운영 기계, 승인 B 뒤).
2. 각 Clone에서 빌드 입력 바이트 목록을 만들어 서로 같고, 개발 PC 진단의 목록과 같은지 비교합니다.
   - 기준값: 이번 진단 Clone(`eol-crlf`, `c77ed1a`)의 목록. 공식 기준값은 공식 Clone에서 다시 만들어 OWNER가 채택합니다.
   - 목록 범위: 추적 파일 전체의 Checkout 바이트(상대경로·SHA-256). 진단 단계에서는 정의만 하고 기준값을 등록하지 않았습니다.
3. `verify.ps1`, Golden, Dry Pilot, CI 빌드, B-4 후보, SourceLink, PCK 5회를 실행하고 두 Clone이 같은지, 개발 PC 진단값과 같은지 비교합니다.
4. 다른 값이 하나라도 있으면 원인 분석 전에는 Gate 판정을 하지 않습니다.

### A.6 SourceLink·PDB 체크섬 제한 (301개 파일)

- **사실:**
  - PDB 문서 체크섬은 Checkout 바이트의 SHA-256입니다. GitHub Raw는 Blob 바이트를 줍니다.
  - E-1에서 원본 추적 문서 372개 중 71개(검증기 15, 검증기 시험 17, SimHarness 증거·일부 파일 등 `eol=lf` 경로)는 PDB = Raw입니다.
  - 나머지 **301개**(게임 코어 `src/` 110, 게임 클라이언트 7, SimHarness 16, 시험 168)는 PDB ≠ Raw이며, **301개 모두 Raw 바이트에 LF→CRLF 변환을 적용하면 PDB 체크섬과 정확히 같습니다**(설명 불가 0). 기록: `phase3ops\policy\sourcelink_reconcile.tsv`.
- **영향:**
  - 디버거가 SourceLink로 받은 원문은 이 301개에서 체크섬이 맞지 않아 "원본이 다름" 경고를 냅니다.
  - **신뢰 판정에는 영향이 없습니다.** 승인 계약(B1-T00)에는 SourceLink·PDB 체크섬 요구가 없습니다. B-4 원격 출처 검증 절차(규범 결정 자료 1.3절)는 처음부터 "Raw는 Blob과, PDB 체크섬은 체크아웃 파일과 비교"하도록 되어 있으며 두 비교 모두 통과합니다(372/372). 검증기는 SourceLink를 판정에 쓰지 않습니다.
  - 신뢰 사슬이 직접 Pin하는 VERIFIER_BINARY는 체크섬까지 Raw와 같습니다.
- **대조 방법(감사용):** Raw 바이트 → (해당 파일이 `-text`·`eol=`가 아니면) LF→CRLF 변환 → SHA-256 = PDB 체크섬. 이 방법으로 372개 전부 설명됩니다.
- **충돌 여부:** 승인 계약 또는 B-4가 정한 SourceLink 신뢰 요구와 **충돌하지 않습니다**. 충돌이 확인되면 E-1을 공식 PASS 근거로 쓰지 않고 중단합니다(OWNER 지시).

### A.7 E-3를 선택할 경우 필요한 변경

- `.gitattributes`에 남은 빌드 입력 경로의 `text eol=…` 규칙 확대(검증기·증거 경로에 이미 쓴 방식)
- 저장소 재정규화(`git add --renormalize`) Commit: 혼합 저장(`i/crlf` 15, `i/mixed` 4, `i/none` 63 등) 정리
- 튜닝 v1~v4 줄바꿈 정합: v4(`-text`, CRLF 저장)와 v1~v3(LF)의 관계를 데이터 또는 시험(`TuningV4Tests` 78행 원문 비교)에서 정리. 튜닝 의미 Hash·Golden 영향을 먼저 측정
- 결과: 새 소스 Commit, SIMHARNESS_BINARY·TEST_ASSEMBLY·DEPENDENCY 일부 바이트 변경(VERIFIER_BINARY는 이미 `eol=lf`라 변경 없을 가능성이 큼, 측정 필요), G2/G3 진단 전체 재실행, 별도 TASK와 Codex 검수

### A.8 Codex 독립 검수 필요성

- 이 변경은 기존 Gate 수용 기준을 완화하는 정책 변경이므로 Codex READ-ONLY 검수를 거친 뒤 OWNER가 결정합니다. 검수 결과는 승인이 아닙니다.

---

## B. G1 — G1-a 단일 규범 표 조건부 수용 기준

### B.1 기존 G1 수용 조건 (원문, 운영 Gate 계획 2절)

> 작업: B-4 공식 출력 파일명·경로: 공식 결과 루트, `run/<run_id>/` 내부 배치(결정 3 §6), Preflight 기록 `preflight.json`·`preflight/<unit>.json`(R2-B 내부 배치)의 최종 이름
>
> 수용 기준: 확정 이름으로 생산자·검증기·시험이 같은 경로를 쓰고, 기존 Hash·Replay가 불변이며, 문서와 코드에 이름이 한 곳에서만 정의됨

### B.2 사실

- `run/<run_id>/`(`EvidenceWriter.RunPath`), `preflight.json`·`preflight`(`OfficialOutput` 상수), 결과 루트(`OfficialOutput.RootRelative`), 실행 색인 두 개, `units/`·`cells/` 디렉터리는 **코드 한 곳**에서 정의되고 검증기도 같은 정의를 씁니다.
- `manifest.json`만 생산 코드 **5개 파일 9곳**에 문자열로 있습니다(`Verifier.cs` 1, `DominanceOfficial.cs` 3, `EvidenceWriter.cs` 2, `FamilyOfficial.cs` 2, `RunReport.cs` 1). 따라서 기존 G1의 "코드에 한 곳에서만 정의" 조건은 **문자 그대로 충족되지 않습니다.**
- `manifest_path`는 실행 색인(DRAFT) 본문에 기록되므로 이름을 바꾸면 계약 §22(DRAFT 색인 바이트 변화 0)에 어긋납니다. 이름은 바꾸지 않습니다.

### B.3 제안하는 수용 조건 (G1-a)

다음이 **모두** 참이면 G1을 충족한 것으로 봅니다.

1. **단일 규범:** [B-4 빌드 절차 초안](B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md) 6.1절의 이름·경로 표가 공식 이름의 유일한 정의입니다(현재 코드의 이름 그대로).
2. **코드 내 참조 전부 추적:** 표의 각 이름에 대해 생산 코드의 모든 문자열 위치(파일·개수)를 표에 고정합니다.
3. **정적 일치성 검사로 차단:** `tools/b1-ops/g1-names-check.ps1`이 생산 코드(`src`, `tools/StarPlayer.SimHarness`, `tools/StarPlayer.EvidenceVerifier`)에서 고정 목록과 다른 위치·개수·이름을 하나라도 찾으면 FAIL합니다. 공식 G1·G3 증거에 그 PASS 출력을 포함합니다.
4. **동적 일치:** 생산자 → Archive → 검증기 TEST_ONLY 종단 시험(기존 `verify.ps1` U6 등)이 PASS입니다.
5. **불변:** 기존 Hash·Replay·DRAFT 색인 바이트·Golden이 바뀌지 않습니다(이름 변경 없음).

### B.4 기존 조건에서 바뀌는 점과 남는 한계

- 바뀌는 점: "코드에 이름이 한 곳에서만 정의"를 "규범 표 한 곳 + 코드의 모든 반복 위치를 정적 검사로 고정"으로 바꿉니다.
- **남는 한계:**
  - 정적 검사는 **문자열 리터럴**을 다룹니다. 이름을 조각으로 이어 붙이는 코드(예 `"manifest" + ".json"`)는 잡지 못합니다. 현재 생산 코드에는 그런 형태가 없습니다(검사 PASS, 진단 확인). 이를 막는 것은 4의 종단 시험과 코드 검수입니다.
  - 시험 코드의 이름은 개수만 보고합니다(정보용). 시험의 일치는 4가 보장합니다.
- **완전한 일치성을 코드 변경 없이 확인할 수 없는 부분:** 위 리터럴 조립 형태의 미래 변경. 현재 소스에는 해당 없음.

### B.5 G1-b로 전환할 경우(보존용 영향 분석)

- `manifest.json`을 공유 상수(예: `RunReport.ManifestFile`)로 바꾸고 9곳이 이를 참조하게 합니다.
- 이름 바이트·DRAFT·Hash·Replay는 불변이지만, SimHarness·검증기 어셈블리에 새 상수 필드가 생겨 **SIMHARNESS_BINARY·VERIFIER_BINARY가 바뀝니다** → 새 소스 Commit, G2/G3 진단 재실행, Codex 검수.
- Codex가 G1-a를 불충분하다고 판단하면 이 경로로 바꿉니다.

### B.6 검증 결과 (진단)

- `g1-names-check.ps1`: 작업 트리와 `c77ed1a` Clone 모두 PASS(생산 파일 164개, 이름 9개, 불일치 0).
- `-SelfTest`: 반례 6건(검증기·생산자의 `manifest.json` 변경, 검증기의 `run/` 하드코딩, Preflight 이름 변경, 결과 루트 이동, 두 번째 색인 문자열) 모두 FAIL로 잡음. 첫 구현의 정규식 2개는 반례 2건(접두어가 붙은 이름, 꼬리가 붙은 경로)을 놓쳐 고쳤습니다(로그 `run2`·`run3` 분리 보존).

## B.7 개정 1 — G1-POLICY-01 정정 (2026-10-30)

- **Codex Finding G1-POLICY-01(MINOR):** 첫 도구는 이름 문자열의 파일별 개수만 비교해, 개수가 같은 경로 변경(`RunPath`의 `run/wrong/{runId}/`, `UnitPreflightName`의 `.txt`)을 PASS로 남겼습니다. 수정 전 도구로 실제 PASS임을 확인했습니다([후속 보고서](../reports/B1_G1G5_FIX_REPORT.md) 2.3절 RED).
- **B.3-3 정정:** `tools/b1-ops/g1-names-check.ps1`(Commit `c4a04bb`, SHA-256 `d3945b5e…6ba0`)은 다음 네 검사가 모두 맞을 때만 PASS합니다. 기준값은 모두 `c77ed1a`에서 고정했습니다.
  1. 이름 리터럴 9종의 파일별 개수(기존)
  2. B-4 6.1 각 행의 경로 생성 정의 13줄이 고정 문장 그대로 정확히 한 번
  3. 이름 리터럴 또는 경로 기호를 가진 생산 코드 줄 전부(50줄, 8개 파일)의 SHA-256 다중집합. 목록 밖 파일의 새 줄도 FAIL
  4. 그 8개 파일의 SHA-256(CRLF→LF, `c77ed1a` Blob과 같음)
- **B.4 한계 정정:** 남는 한계는 "고정 8개 파일 **밖**의 새 파일에서 이름을 조각으로 조립·복제하는 코드"와 "고정 파일 밖에서 이름·기호 없는 식별자로 들어오는 값 흐름"입니다(반례 `assembled_name_in_new_file`로 한계 확인). 고정 파일 안의 변경은 모두 FAIL합니다. 이 한계는 B.3-4 종단 시험과 코드 검수가 다룹니다.
- **B.6 결과 정정:** `c77ed1a` Clone 2개(CRLF·LF) PASS, `-SelfTest` 17건 모두 기대대로(기존 6, Codex 반례 2, 같은 개수 반례 3, 새 파일 1, 값 흐름 1, 오탐 대조 2, 한계 1). PowerShell 5.1만 실행했고 PowerShell 7은 **미검증**입니다.
- 이름·코드·Hash·Replay·DRAFT 색인 바이트는 바뀌지 않습니다(B.3-5 그대로).
