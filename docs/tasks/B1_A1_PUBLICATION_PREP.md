# B1 A-1 신규 비공개 공식 원격 게시 준비 (DRAFT, 원격 생성·Push 없음)

- 작성: Claude Code, 2026-10-28. 근거: OWNER 지시 "G4 구현 수용 및 Phase 3 잔여 작업 자율 진행"(2026-10-28) 3항
- **성격:** 게시 승인 요청에 필요한 정보입니다.
  - 원격 생성, 최초 Push, Tag 생성·게시, 원격 Fetch는 **하지 않았습니다**.
  - 기존 공개 저장소(`junk1901/Star_player`)에는 Push하지 않습니다.
  - 여기의 원격 이름·URL은 모두 **후보**입니다.
- 관련: [B-4 규범 결정 자료](B1_B4_OWNER_NORMATIVE_DECISION_DRAFT.md) 1절, [G5 운영 설계](B1_G5_OPERATIONAL_DESIGN_DRAFT.md), [준비 보고서](../reports/B1_G5_A1_PREP_REPORT.md)
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 신규 저장소 이름 후보

SourceLink URL(`https://raw.githubusercontent.com/<owner>/<repo>/<commit>/*`)이 모든 StarPlayer PDB에 들어갑니다. 그 PDB 식별자 때문에 DLL 바이트도 바뀝니다. 따라서 **이름은 한 번 정하면 바꾸지 않는 값**이어야 합니다(이름을 바꾸면 공식 바이너리 SHA를 다시 측정해야 합니다).

| 후보 | 원격 URL | 장점 | 단점 |
|---|---|---|---|
| **`junk1901/star-player-official`** (추천) | `https://github.com/junk1901/star-player-official.git` | 소문자·하이픈이라 URL 대소문자 차이가 생기지 않음. 용도가 이름에 드러남 | 기존 저장소 이름과 표기 방식이 다름 |
| `junk1901/Star_player_official` | `https://github.com/junk1901/Star_player_official.git` | 기존 저장소 이름과 같은 표기 | 대소문자 혼용. GitHub는 대소문자를 구분하지 않지만 SourceLink 문자열은 clone URL 표기를 그대로 씀 |
| `junk1901/star-player-b1` | `https://github.com/junk1901/star-player-b1.git` | 짧음 | 이후 Phase까지 쓰면 이름이 범위와 맞지 않음 |

- 계정은 현재 Git 원격과 같은 `junk1901`을 후보로 둡니다. 조직 계정을 쓸지는 OWNER가 정합니다.
- **공개 범위:** Private. 공개 전환은 별도 결정입니다(2절 공개 영향 참조).
- **Clone URL 표기 고정:** 모든 공식 빌드 clone은 위 URL을 **정확히 같은 문자열**로 씁니다. `.git` 접미사 유무도 고정합니다. 이렇게 해야 SourceLink 문자열이 같아집니다.

## 2. 게시 대상 Git Commit·Branch·Tag

### 2.1 현재 로컬 상태 (2026-10-28, [사실])

| 항목 | 값 |
|---|---|
| 작업 브랜치 | `phase3/g4-official-entry` (Worktree `E:\Madegame\Star_player_g4`) |
| G4 코드 수용 소스 Commit | `a980ea5a73ff2e3dfb032d2c3956d2d51c239711` (OWNER 2026-10-28 코드 단계 수용) |
| 수용 당시 문서 Commit | `20ec650524330a9cd06e309d2adaedcfd80003f2` |
| 이번 작업 Commit | 준비 보고서 7절 (운영 도구·문서만, 제품·검증기 코드 변경 없음) |
| 로컬 `main` | `96070c5` — 작업 브랜치의 조상(빨리 감기 가능) |
| 기존 공개 원격 `origin/main` | `7456f44` (마지막 조회값). 게시 후보 이력 중 이 Commit 밖의 것은 `20ec650` 기준 40개 |
| 보존 Tag | `b1-code-accepted-2026-10-26` → 객체 `5e19ddf` → Commit `7bdde7dd091f9a66957bc740c77d470c7732b3ad` (변경 금지) |

### 2.2 게시안 (후보)

| 안 | 원격 ref | 내용 | 평가 |
|---|---|---|---|
| **P-1** (추천) | `refs/heads/main` = 승인 게시 Commit | 승인 게시 Commit(이번 작업 마지막 Commit 또는 OWNER가 정한 Commit)과 조상 전체. 이력 재작성 없음 | 기본 브랜치 하나라 보호 규칙이 단순함. `a980ea5`가 조상이므로 SourceLink Commit 조회 가능 |
| P-2 | `refs/heads/main` = `96070c5`, `refs/heads/phase3/g4-official-entry` = 승인 게시 Commit | 로컬 구조 그대로 | 브랜치 2개를 보호해야 함 |
| P-3 | `refs/heads/main` = `a980ea5` | 코드 수용 Commit까지만 | 수용 뒤 문서·도구 이력이 빠짐. 이후 추가 Push 필요 |

**Tag:**

- 보존 Tag `b1-code-accepted-2026-10-26`은 원격에 **게시할지만** OWNER가 정합니다. 내용은 바꾸지 않습니다.
- G4 수용 Tag는 후보 이름만 제시합니다: `b1-g4-code-accepted-2026-10-28` → `a980ea5`(Annotated). **만들지 않았습니다.**
- Tag는 원격에서 삭제·이동하지 않도록 보호 규칙 대상에 넣습니다.

**게시 순서 (승인 후, 후보):**

1. 2.1 표의 Commit SHA를 다시 확인합니다.
2. 3절 검사를 게시 대상 Commit에서 다시 실행하고 결과를 보존합니다.
3. 빈 비공개 저장소를 만듭니다(README·License 자동 생성 없음).
4. **게시 전 Ref 대조(A1-R02):**
   - **승인 Ref 목록 작성:** 게시 직전에 승인 Ref 목록 파일을 만듭니다. 기본은 `refs/heads/main <승인 Commit SHA>` 한 줄입니다. 승인된 Tag가 있으면 `refs/tags/<tag> <Tag 객체 SHA>`를 추가합니다.
   - **원격이 비어 있는지 확인:** `git ls-remote <A-1 URL>`의 출력이 비어 있어야 합니다(새 저장소).
   - **Dry Run으로 전송 Ref 확인:** 다음 명령의 `*` 줄이 승인 목록의 `refs/heads/main` **한 줄뿐**이어야 합니다. 다른 Ref가 보이면 중단합니다.
     `git -c push.followTags=false push --no-follow-tags --porcelain --dry-run <A-1 URL> <승인 Commit SHA>:refs/heads/main`
5. **최초 Push:** 강제 Push는 하지 않습니다.
   - `git -c push.followTags=false push --no-follow-tags --porcelain <A-1 URL> <승인 Commit SHA>:refs/heads/main`
   - **승인된 Tag만**, Tag마다 별도 명령으로 보냅니다.
     `git -c push.followTags=false push --no-follow-tags --porcelain <A-1 URL> refs/tags/<tag>:refs/tags/<tag>`
   - **명령 형식의 규칙:**
     - 원격 이름이 아니라 **URL을 직접** 씁니다. 저장된 원격의 `remote.<name>.push`·`mirror` 설정이 끼어들지 않게 하기 위해서입니다.
     - 대상은 전체 Ref 이름으로 씁니다.
     - `--tags`·`--all`·`--mirror`·`--follow-tags`는 쓰지 않습니다.
   - **[시험: 로컬 bare 저장소, 2026-10-28]** 설정 `push.followTags=true`와 도달 가능한 미승인 Annotated Tag가 있는 상태에서 비교했습니다.
     - 위 형식(`--no-follow-tags`)은 `refs/heads/main`만 전송했습니다.
     - 같은 명령에서 `--no-follow-tags`를 빼면 미승인 Tag도 함께 전송되었습니다.
     - 승인 Tag는 별도의 명시 Ref Push로만 전송되었습니다.
     - 같은 비교는 `tools/b1-ops/selftest.ps1`의 `a1.push_no_follow_tags` 사례로 반복됩니다.
6. **게시 후 Ref 대조:** `git ls-remote <A-1 URL>`의 출력이 승인 목록과 **정확히 같아야** 합니다.
   - Annotated Tag는 `refs/tags/<tag>`와 `refs/tags/<tag>^{}` 두 줄로 나타납니다.
   - 목록에 없는 Ref가 있으면 게시 미완료로 보고 OWNER에 보고합니다. 원격 Ref를 임의로 삭제하지 않습니다.
7. 기본 브랜치 보호(강제 Push·삭제 금지)와 Tag 보호를 설정합니다.
8. 7절 원격 조회 검증을 실행합니다.

- **주의:** 기본 Worktree(`E:\Madegame\Star_player`)에는 미추적 파일 3개(`docs/tasks/B1_B4_*_DRAFT.md`)가 있습니다. 같은 경로의 파일이 작업 브랜치에서는 추적되므로, 그 Worktree에서 `main`을 빨리 감기하면 Git이 덮어쓰기를 거부할 수 있습니다. P-1은 Worktree를 바꾸지 않고 Commit SHA로 Push하므로 영향이 없습니다.

## 3. 포함될 OWNER·Codex 원문과 Git 이력

### 3.1 저장소 안(게시되면 함께 올라감)

| 위치 | 내용 | 수 |
|---|---|---|
| `docs/contracts/b1/originals/owner/` | OWNER 원문 O01~O19 | 19 |
| `docs/contracts/b1/originals/codex/` | Codex 요청·판정 C01~C12, P01·P02 | 14 |
| `docs/contracts/b1/originals/INDEX.md`, `index.json` | 색인 | 2 |
| `docs/contracts/b1/` 그 밖 | 승인 계약, OWNER 결정 1~3, 코드 수용 결정 등 | — |

- **[관찰]** `index.json`은 28건(O01~O16, P01·P02, C01~C10)만 나열합니다. `INDEX.md`에는 O17~O19와 C11·C12가 없습니다.
  - 파일은 저장소에 있지만 색인에서 빠져 있습니다.
  - 원문·계약 기록 폴더이므로 이번 작업에서 고치지 않았습니다. 색인 보완 여부는 OWNER가 정합니다.

### 3.2 저장소 밖(현재 게시되지 않음)

| 위치 | 내용 | 수 | manifest SHA-256 |
|---|---|---|---|
| `E:\Madegame\g3x\codex-originals-prep\` | C11·C12 사본(저장소에도 있음) | 2 | (manifest 없음) |
| `E:\Madegame\g3x\codex-originals-b4\` | B-4 Codex 요청·판정 4건 + 첨부 1 | 5 | `723139d9a59c1891…` |
| `E:\Madegame\g3x\codex-originals-g4\` | G4 Codex 요청·판정 5건(최종 PASS 포함) | 5 | `075384a493145abc…` |
| `E:\Madegame\g3x\owner-originals-post-o19\` | O19 이후 OWNER 지시 14건(이번 작업에서 세션 기록에서 바이트 그대로 추출) | 14 | `6d97f887be069eba…` |

- **OWNER 결정 필요:** 3.2의 원문을 게시 전에 저장소에 넣을지 정해야 합니다.
  - 넣는다면 3.1과 같은 위치·형식(`O20~`, `C13~`)으로 새 Commit에 추가합니다. 기존 원문과 색인은 바꾸지 않습니다.
  - 넣지 않으면 저장소 밖 보존이 유일한 사본입니다.
- 3.2의 OWNER 원문 14건은 Claude 세션 기록에서 `pasted_content`와 `OWNER`가 함께 있는 사용자 메시지로 찾았습니다. 이 형식이 아닌 짧은 지시는 빠졌을 수 있습니다.

### 3.3 Git 이력

- **범위:** 게시 Commit과 그 조상 전체. 이력 재작성은 없습니다.
- **수량:**
  - `20ec650` 기준 59개 Commit이며, 그중 40개가 기존 공개 `origin/main`(`7456f44`)에 없습니다.
  - 이번 작업 Commit 수는 보고서 7절에 있습니다.
- **목록 생성(읽기 전용):** `git log --reverse --format="%H %ad %s" --date=iso-strict <게시 Commit>`
- **작성자 정보:** 모든 Commit의 작성자·Commit 작성자는 같은 이름·메일 1개입니다. 이미 공개된 Commit에도 같은 값이 있어 새 노출은 아닙니다.
- **이진 파일:** `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf` 2개입니다. 둘 다 공개 `origin/main`에 **없습니다**. 규칙 문서 원본이므로 공개 전환 때는 저작권 검토가 필요합니다. 비공개 게시에는 영향이 없습니다.

## 4. 비밀정보·개인정보 검사

**도구:** `tools/b1-ops/a1-scan.ps1`. 읽기 전용이며 게시 Commit과 조상 전체를 검사합니다.

- **검사 범위(A1-R01 보완):**
  - 조상 Commit 전체 트리에 `git grep -P`를 실행해 모든 도달 가능 Text blob을 검사합니다.
  - 각 조상 Commit 객체를 원시 바이트(`git cat-file commit`)로 읽어 다음 필드에 같은 패턴을 적용합니다.
    - 작성자 이름·이메일
    - Committer 이름·이메일
    - 그 밖의 머리글(encoding·gpgsig 등)
    - **메시지 전체**
  - 메시지는 첫 빈 줄 뒤 전부이므로, 메시지 안의 구분 문자·제어 문자·빈 줄이 범위를 자르지 않습니다.
  - 이전 판은 작성자·Committer를 통계에만 썼고, 메시지를 구분 문자로 잘랐습니다.
- **SECRET 클래스**(하나라도 있으면 게시 중단, 종료 코드 1): 개인 키 블록, GitHub·Google·AWS·Slack·LLM 키 형태, 따옴표로 둘러싼 자격 증명 대입
- **PRIVACY 클래스**(OWNER 판단): 이메일, Windows 계정 SID, 사용자 프로필 경로, 로컬 프로젝트 경로, Windows 기계 이름

**결과 — `20ec650` 기준(59 Commit, 이번 작업 Commit은 보고서 6절):**

| 분류 | 결과 | 내용 |
|---|---|---|
| SECRET 7종 | **0건** | — |
| 이메일 | 2개 값 | 시험용 `t@example.invalid`, Commit 메시지의 `Co-Authored-By` 주소 |
| 계정 SID | 8개 값 | 7개는 시험용 가짜 SID(`S-1-5-21-1-2-3-*`, `S-1-5-21-9-9-9-9`). **1개는 개발 PC 실제 사용자 SID**이며 `docs/reports/B1_INTEGRATED_REPORT.md` 58행에 있고 공개 원격에는 없음 |
| 사용자 프로필 경로 | 113개 파일 | `C:\Users\user…`(Windows 사용자 이름 `user`) |
| 로컬 프로젝트 경로 | 180개 파일 | `E:\Madegame…` |
| 기계 이름 | 0건 | — |

- **OWNER 판단 항목:** 개발 PC 계정 SID 1건과 로컬 경로가 비공개 원격에 올라가는 것을 수용하는지 정해야 합니다.
  - 원문·보고서 바이트를 바꾸지 않는 원칙상 제거하려면 이력 재작성이 필요합니다. 따라서 **수용(비공개 유지)**을 권고합니다.
- **운영 값 주의:** 앞으로 운영 SID·Pipe 이름·설치 경로를 문서에 넣으면 그 Commit도 게시 범위입니다. 운영 값은 R-AUDIT·설치 설정에만 두는 것을 권고합니다.
- **자격 증명:** 원격 접근 토큰은 저장소·세이브·로그·증거·설치 설정에 넣지 않습니다.
- **한계:** 정규식 검사이므로 "없음"을 증명하지 않습니다. Text가 아닌 파일은 경로 목록만 확인합니다.
  - 결과 표기는 `NO_SECRET_PATTERN_FOUND (pattern-based; not proof that no secret exists)`입니다. 패턴 0건은 기밀정보 부재와 다릅니다.

## 5. SourceLink 공식 URL 설정 방법

- **방식:** .NET 8 SDK 내장 SourceLink(별도 패키지 없음)는 빌드하는 clone의 `remote.origin.url`과 HEAD Commit에서 URL을 만듭니다. 저장소 설정 파일은 바꾸지 않습니다.
  - `https://github.com/<owner>/<repo>.git` → `https://raw.githubusercontent.com/<owner>/<repo>/<commit>/*`
- **공식 빌드:** A-1 원격에서 직접 clone하면 `origin`이 자동으로 공식 URL이 됩니다. 로컬 사본에서 clone하거나 `remote set-url`로 바꾸는 방식은 **진단 전용**입니다.
- **확인:** 빌드 뒤 모든 StarPlayer PDB의 SourceLink JSON이 `{"documents":{"/_/*":"<raw URL>/<승인 Commit>/*"}}` 형태(`ContinuousIntegrationBuild=true`가 경로를 `/_/`로 바꿈)이고, 승인 Commit과 같은지 확인합니다.
  - 확인 도구는 기존 G3 진단 도구(`pedump`, 저장소 밖) 또는 System.Reflection.Metadata 기반 동등 도구를 씁니다.
- **[시험: 로컬 진단]** 후보 URL로 `origin`만 바꾼 임시 clone의 결과는 보고서 4절에 있습니다. 원격 조회는 없습니다.
  - SourceLink 문자열이 후보 URL·Commit으로 바뀌는지
  - 그 결과 DLL SHA가 통제 URL 빌드와 다른지

## 6. 새 원격 기준 빌드·SHA 재검증 절차 (승인 후)

1. **두 clone:** A-1 URL에서 경로 길이가 다른 두 빈 폴더로 `git clone --no-hardlinks <A-1 URL> <dir>`을 실행하고 `git checkout --detach <승인 소스 Commit>`으로 맞춥니다.
2. **전체 검증:** 각 clone에서 다음을 실행합니다. 모두 기존 기준과 같아야 합니다.
   - `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1`(GATE PASS)
   - Golden 4/4
   - Dry Pilot 9회 동일
3. **공식 후보 빌드:** `dotnet build StarPlayer.sln -c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false -p:ContinuousIntegrationBuild=true`
4. **Artifact 목록:** 각 clone에서 다음을 실행합니다.
   - `tools\b1-ops\b4-candidates.ps1 -BuildRoot <dir> -SourceCommit <sha> -Out <json> -Previous <이전 후보>`
   - 두 clone의 441행 SHA가 모두 같아야 합니다(경로 독립 재현성).
5. **SourceLink:** 5절 확인. URL = A-1, Commit = 승인 Commit
6. **Godot PCK:** 5회 전체 바이트 비교(B-4 5절). 이번 작업에서는 재실행하지 않았습니다.
7. **보존:** 명령·출력·SHA를 저장소 밖 진단 디렉터리에 보존하고 OWNER·Codex에 제출합니다. 이 값은 진단값이며, Build Provenance 등록은 별도 승인입니다.

## 7. 원격 Commit 및 소스 조회 검증 절차 (승인 후)

[B-4 규범 결정 자료](B1_B4_OWNER_NORMATIVE_DECISION_DRAFT.md) 1.3절을 그대로 씁니다. 비공개이므로 **읽기 전용 자격 증명**으로 수행하고, 자격 증명은 어떤 파일에도 남기지 않습니다.

1. `git ls-remote <A-1 URL>`: 광고된 `refs/heads/main`(과 승인 Tag)이 승인 Commit이거나 그 자손인지 확인합니다.
2. 빈 저장소에서 `git fetch <A-1 URL> <승인 Commit>`: 객체 SHA가 일치하는지 확인합니다.
3. 6절 빌드 PDB의 SourceLink가 A-1 raw URL + 승인 Commit인지 확인합니다.
4. PDB에 기록된 문서마다 비교합니다. 비공개 저장소의 raw 조회는 인증이 필요합니다.
   - raw 바이트 ↔ `git cat-file blob <commit>:<path>` 바이트
   - PDB 문서 체크섬 ↔ 체크아웃 파일
5. 하나라도 불일치·조회 실패·인증 실패면 공식 원격 출처 검증 미완료(Fail-Closed)입니다.

## 8. OWNER 게시 승인에 필요한 정확한 결정 목록

| # | 결정 | 권고 |
|---|---|---|
| A1-1 | 저장소 계정·이름·URL 표기 | `https://github.com/junk1901/star-player-official.git` (Private) |
| A1-2 | 게시 Commit과 ref | P-1: `main` = 이번 작업 마지막 Commit(보고서 7절 SHA). `a980ea5` 포함 |
| A1-3 | Tag 게시 | `b1-code-accepted-2026-10-26` 게시 여부. 새 `b1-g4-code-accepted-2026-10-28`(→ `a980ea5`) 생성 여부 |
| A1-4 | 3.2 저장소 밖 원문의 저장소 편입 | 편입 권고(원문 보존 일원화). 편입 시 새 Commit이 생기므로 A1-2의 SHA도 바뀜 |
| A1-5 | 4절 PRIVACY 항목(개발 PC SID 1건, 로컬 경로) 수용 | 비공개 유지 조건으로 수용 |
| A1-6 | 승인 소스 Commit(공식 빌드 기준) | 제품 코드가 `a980ea5`와 같은 최신 Commit. 확정 뒤 6절 재검증 |
| A1-7 | 읽기 전용 검증 자격 증명의 발급·보관 방식 | 저장소·로그 밖, 검증 후 폐기 |
