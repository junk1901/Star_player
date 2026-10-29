# B1 A-1 기존 공개 공식 원격 게시 준비 (DRAFT, Push 없음)

- 작성: Claude Code, 2026-10-28. **개정 2026-10-29:** OWNER 지시 "G4/G5 검수 수용 및 기존 GitHub 기반 Phase 3 운영 단계 전환"(2026-10-29) 2·3항
- **OWNER 결정(2026-10-29):** 공식 원격은 **기존 공개 저장소** `https://github.com/junk1901/Star_player.git`입니다.
  - Public 상태를 유지합니다. Public이라는 이유만으로 개발·Phase 3를 멈추지 않습니다.
  - 새 비공개 저장소(`star-player-official` 등) 생성 계획은 **취소**되었습니다. 이 문서의 이전 판(2026-10-28)이 권고한 신규 비공개 저장소 후보는 이력으로만 남습니다.
  - [B-4 빌드 절차 결정 초안](B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md) 11.4절 선택지 중 **R-1**(현재 공개 저장소에 승인 Commit과 조상을 Push)에 해당합니다.
- **성격:** OWNER 승인 A(최초 게시)에 필요한 정보입니다.
  - Push, Tag 생성·게시, 원격 설정 변경은 **하지 않았습니다**.
  - 원격에는 읽기 전용 조회(`git ls-remote`)만 했습니다. Fetch도 하지 않았습니다(원격 `main` 객체가 이미 로컬에 있음).
- 이미 독립 검수를 통과한 A-1 검사(`a1-scan.ps1`), Ref 대조(`a1-refcheck.ps1`), 안전한 Push 절차(`--no-follow-tags`, 명시 Ref, 정확한 Tag 객체 ID)는 그대로 씁니다. 바뀐 것은 대상 원격과 "빈 원격" 전제뿐입니다.
- 관련: [Phase 3 운영 전환 계획](B1_PHASE3_OPERATIONS_TRANSITION.md), [B-4 규범 결정 자료](B1_B4_OWNER_NORMATIVE_DECISION_DRAFT.md) 1절, [G5 운영 설계](B1_G5_OPERATIONAL_DESIGN_DRAFT.md)
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 공식 원격과 고정 문자열

| 항목 | 값(대소문자·구두점까지 고정) |
|---|---|
| 저장소 | `junk1901/Star_player` (Public) |
| **Clone·Push URL** | `https://github.com/junk1901/Star_player.git` |
| 저장소 웹 URL | `https://github.com/junk1901/Star_player` |
| **SourceLink 문서 URL** | `https://raw.githubusercontent.com/junk1901/Star_player/<source_git_sha>/*` |
| 기본 브랜치 | `main`(원격 `HEAD` → `refs/heads/main`, 2026-10-29 조회) |

- SourceLink 문자열은 빌드하는 clone의 `remote.origin.url`에서 만들어집니다(5절). 공식 빌드 clone은 위 Clone URL을 **정확히** 씁니다.
- **[실측]** `star_player`(소문자)로 clone하면 SourceLink 문자열과 DLL 바이트가 달라졌습니다. `.git` 접미사 유무는 결과에 영향이 없었습니다(5절). 그래도 표기는 위 값으로 고정합니다.

## 2. 게시 대상 Git Commit·Branch·Tag

### 2.1 현재 상태 (2026-10-29, [사실])

| 항목 | 값 |
|---|---|
| 작업 브랜치·HEAD | `phase3/g4-official-entry` → `5e2c6403223578c3e6531991dbcfd00fc1c504b7` (Worktree `E:\Madegame\Star_player_g4`, 작업 트리 Clean) |
| 최종 검증 소스 Commit | `1751fe04aaafbda65462915d2dac89e56536a8ae` (G4/G5/A-1 도구 Codex REVIEW_READY) |
| G4 코드 수용 소스 | `a980ea5a73ff2e3dfb032d2c3956d2d51c239711` |
| 로컬 `main` | `96070c5` — 원격 `main`보다 32개 앞, 작업 브랜치의 조상 |
| **원격 `refs/heads/main`(서버, 2026-10-29 `ls-remote`)** | `7456f44fd3ecd687afce210eb5be972114f8479c` — 원격의 **유일한 Ref**. 원격 Tag 0개 |
| 로컬 추적 Ref `refs/remotes/origin/main` | `7456f44…` (서버와 같음) |
| 공통 조상(`merge-base HEAD 7456f44`) | `7456f44` 자체 |
| **Fast-forward 가능** | **예.** `7456f44`는 HEAD의 조상입니다. 원격에만 있는 Commit 0개, Divergence 없음 |
| 게시 시 새로 공개되는 Commit | HEAD 기준 **49개**(`18ed60e` 2026-10-13 ~ `5e2c640` 2026-10-29), Merge Commit 0개 |
| HEAD에서 도달 가능한 전체 Commit | 68개(원격 `main` 19개 포함) |
| 로컬 Tag | `b1-code-accepted-2026-10-26` → 객체 `5e19ddf505a2…` → Commit `7bdde7dd091f9a66957bc740c77d470c7732b3ad`. **로컬 전용, 변경 금지**. `7bdde7d`는 원격 `main`에 없고 HEAD의 조상 |
| 로컬 Branch | `main`, `phase3/g4-official-entry` (그 밖 없음) |
| Worktree | `E:\Madegame\Star_player` (`main`, 미추적 파일 3개), `E:\Madegame\Star_player_g4` (작업 브랜치) |

- 이전 판의 "62개 Commit·43개 신규" 같은 수치는 재사용하지 않습니다. 위 값은 2026-10-29 실측입니다. 게시 직전에 다시 산출합니다(게시 순서 1단계).
- **기준선 Ref 대조(읽기 전용, 2026-10-29):** `a1-refcheck.ps1 -Remote https://github.com/junk1901/Star_player.git -Expect "refs/heads/main=7456f44fd3ecd687afce210eb5be972114f8479c"` → `RESULT=PASS remote_refs=1 expected=1 bad=0`
  - 기록: `E:\Madegame\g3x\phase3ops\refcheck_prepush_baseline.txt`

### 2.2 게시안

| 안 | 원격 Ref | 내용 | 평가 |
|---|---|---|---|
| **P-1** (추천) | `refs/heads/main` = 승인 게시 Commit(Fast-forward) | 승인 게시 Commit(이번 전환 작업의 마지막 문서 Commit 또는 OWNER가 정한 Commit)과 조상 전체. 기존 `7456f44` 위에 그대로 이어짐 | 이력 재작성·강제 Push 없음. 브랜치 하나. `1751fe0`·`a980ea5`·`7bdde7d`가 조상이므로 SourceLink Commit 조회 가능 |
| P-2 | `refs/heads/main` = `96070c5`, `refs/heads/phase3/g4-official-entry` = 승인 게시 Commit | 로컬 구조 그대로 | 브랜치 2개 보호. `-Expect`가 `main`·Tag만 받으므로 게시 후 대조 도구가 두 번째 브랜치를 `UNEXPECTED`로 거부함(도구 변경 필요 → 비권고) |
| P-3 | `refs/heads/main` = `1751fe0` | 최종 검증 소스까지만 | 그 뒤 문서 Commit(검수 결과·전환 계획)이 빠짐. 추가 Push 필요 |

- **SourceLink 결속:** 공식 빌드의 `source_git_sha`는 게시된 `main`에서 도달 가능해야 합니다. P-1은 이를 만족합니다.

**Tag 정책(OWNER 2026-10-29 2-3항):**

- 역사적 B1 Tag `b1-code-accepted-2026-10-26`은 로컬에서 기존 Commit에 그대로 둡니다. 이동·삭제·재생성하지 않습니다.
- 신규 G4 Tag는 만들지 않습니다.
- 최초 Push는 승인된 `main` Ref 하나만 대상입니다. `--no-follow-tags`를 씁니다.
- Tag를 따로 게시하려면 OWNER가 게시 목록(정확한 이름)을 승인해야 합니다. 그 경우 아래 4·5단계의 정확한 Tag 객체 ID 절차를 씁니다.

**게시 순서 (OWNER 승인 A 뒤에만):**

1. **SHA 재확인:** 승인 게시 Commit의 전체 SHA, 원격 `main`, Fast-forward 여부(`git merge-base --is-ancestor 7456f44fd3ecd687afce210eb5be972114f8479c <승인 Commit>`의 종료 코드 0), 새 Commit 수를 다시 산출합니다.
2. **검사 재실행:** 4절 검사를 승인 게시 Commit에서 다시 실행하고 결과를 보존합니다. SECRET이 하나라도 있으면 중단합니다.
3. **게시 직전 원격 기준선 대조:** `a1-refcheck.ps1 -Remote https://github.com/junk1901/Star_player.git -Expect "refs/heads/main=<1단계에서 확인한 원격 main SHA>" -Git <승인 git.exe>`가 PASS여야 합니다.
   - 원격 `main`이 `7456f44`에서 바뀌었거나 다른 Ref·Tag가 생겼으면 중단하고 OWNER에 보고합니다.
   - 이때 Merge·Rebase·강제 Push로 맞추지 않습니다.
   - (이전 판의 "원격이 비어 있는지 확인"을 대신합니다. 기존 원격은 비어 있지 않으므로, 기록된 상태와 **정확히 같은지**를 봅니다.)
4. **게시 Ref 목록:**
   - **승인 Ref 목록 작성:** 게시 직전에 승인 Ref 목록 파일을 만듭니다. 기본은 `refs/heads/main <승인 Commit SHA>` 한 줄입니다. 승인된 Tag가 있으면 `refs/tags/<tag> <Tag 객체 SHA>`를 추가합니다.
   - **Tag 객체 SHA는 정확한 이름으로 읽습니다(A1-FIX2-01):** `git for-each-ref --format="%(objectname) %(refname)" refs/tags/<승인 Tag>`를 씁니다.
     - 출력의 Ref 이름은 OWNER가 승인한 이름과 **대소문자까지 바이트 단위로** 같아야 합니다. 출력이 비어 있거나 이름이 다르면 중단합니다.
     - `git rev-parse`·`git show-ref --verify`는 쓰지 않습니다. Windows의 일반(loose ref) 저장소는 대소문자를 구분하지 않습니다. 그래서 `refs/tags/v-approved`(틀린 대소문자)를 물어도 `V-approved`의 ID를 돌려주고, `show-ref`는 틀린 이름을 그대로 출력합니다.
     - 정확한 결과를 낸 것은 `for-each-ref`뿐이었습니다(빈 출력). 실측: git 2.45.1.windows.1, 보고서 `docs/reports/B1_A1_FIX2_REPORT.md`.
     - 같은 이유로 Windows의 일반 로컬 저장소에는 대소문자만 다른 Tag 두 개가 함께 있을 수 없습니다. 원격 서버(대소문자 구분)에는 있을 수 있으므로 게시 후 대조는 대소문자를 구분합니다(6단계).
   - **Dry Run으로 전송 Ref 확인:** 다음 명령의 `*`(또는 Fast-forward) 줄이 `refs/heads/main` **한 줄뿐**이고, 그 줄이 `7456f44…..<승인 Commit>` 형태의 Fast-forward여야 합니다. 다른 Ref가 보이거나 `+`(강제) 표시가 있으면 중단합니다.
     `git -c push.followTags=false push --no-follow-tags --porcelain --dry-run https://github.com/junk1901/Star_player.git <승인 Commit SHA>:refs/heads/main`
     - GitHub의 HTTPS Push는 Dry Run도 쓰기 자격 증명으로 연결합니다. 그래서 이번 준비 단계에서는 실행하지 않았고, 승인 뒤 OWNER 세션에서 실행합니다.
5. **최초 Push:** 강제 Push는 하지 않습니다.
   - `git -c push.followTags=false push --no-follow-tags --porcelain https://github.com/junk1901/Star_player.git <승인 Commit SHA>:refs/heads/main`
   - Refspec 앞에 `+`를 붙이지 않고 `--force`·`--force-with-lease`도 쓰지 않습니다. 그러면 원격 `main`이 그 사이 바뀌어 Fast-forward가 아닐 때 Git이 거부합니다. 거부되면 3단계부터 다시 하고 OWNER에 보고합니다.
   - **승인된 Tag만**, Tag마다 별도 명령으로 보냅니다. 원본은 이름이 아니라 4단계에서 읽은 **Tag 객체 SHA**로 씁니다. 그래야 대소문자를 구분하지 않는 로컬 이름 해석이 끼어들지 않습니다.
     `git -c push.followTags=false push --no-follow-tags --porcelain https://github.com/junk1901/Star_player.git <Tag 객체 SHA>:refs/tags/<승인 Tag 정확한 이름>`
   - **명령 형식의 규칙:**
     - 원격 이름이 아니라 **URL을 직접** 씁니다. 저장된 원격의 `remote.<name>.push`·`mirror` 설정이 끼어들지 않게 하기 위해서입니다.
     - 대상은 전체 Ref 이름으로 씁니다.
     - `--tags`·`--all`·`--mirror`·`--follow-tags`는 쓰지 않습니다.
   - **[시험: 로컬 bare 저장소, 2026-10-28]** 설정 `push.followTags=true`와 도달 가능한 미승인 Annotated Tag가 있는 상태에서 비교했습니다.
     - 위 형식(`--no-follow-tags`)은 `refs/heads/main`만 전송했습니다.
     - 같은 명령에서 `--no-follow-tags`를 빼면 미승인 Tag도 함께 전송되었습니다.
     - 승인 Tag는 별도의 명시 Ref Push로만 전송되었습니다.
     - 같은 비교는 `tools/b1-ops/selftest.ps1`의 `a1.push_no_follow_tags` 사례로 반복됩니다.
   - URL로 Push하면 로컬 추적 Ref `refs/remotes/origin/main`은 자동으로 바뀌지 않습니다. 게시 후 `git fetch --no-tags origin`으로 맞춥니다(`--no-tags`: 원격 Tag가 로컬 Tag를 만들지 않게 함).
6. **게시 후 Ref 대조(A1-R02 2차 보완):** `tools/b1-ops/a1-refcheck.ps1`로 대조합니다.
   - 명령: `a1-refcheck.ps1 -Remote https://github.com/junk1901/Star_player.git -Expect "refs/heads/main=<승인 Commit SHA>|refs/tags/<승인 Tag>=<Tag 객체 SHA>" -Git <승인 git.exe>`
   - 도구는 `git ls-remote --refs <URL>`만 읽습니다. `--refs`는 실제 Ref만 나열합니다.
     - 저장소가 항상 광고하는 `HEAD`는 나오지 않습니다.
     - Annotated Tag의 Peeled 줄(`refs/tags/<tag>^{}`)도 나오지 않습니다.
     - 따라서 정상 게시와 승인 Annotated Tag가 "목록 외 Ref"로 오판되지 않습니다.
     - 이전 판(`fb1e338`)은 `--refs` 없는 출력을 승인 목록과 그대로 비교했습니다. 그래서 정상 게시도 `HEAD`·`^{}` 줄 때문에 실패로 판정되었습니다. 로컬 bare 재현 기록은 G5/A-1 2차 수정 보고서(`docs/reports/B1_G5_FIX2_REPORT.md`)에 둡니다.
   - **기대값:** 4단계 승인 Ref 목록의 값, 즉 Ref 자신의 객체 ID입니다(Tag는 `for-each-ref`로 정확한 이름에서 읽은 값).
     - `main`과 Lightweight Tag는 Commit SHA입니다.
     - Annotated Tag는 **Tag 객체 SHA**입니다. Peeled Commit SHA를 쓰면 `MISMATCH`가 납니다.
     - `-Expect`에는 `refs/heads/main`이 정확히 한 번 있어야 합니다. Tag는 **OWNER가 승인한 것만** 넣습니다.
   - **판정:**
     - `PASS`(종료 코드 0): 원격의 모든 `refs/heads/main`·`refs/tags/*`가 기대값과 같습니다.
     - `FAIL`(종료 코드 1): 기대 밖 Ref(다른 Branch·미승인 Tag 등, `UNEXPECTED`), SHA 불일치(`MISMATCH`), 누락(`MISSING`), 같은 Ref가 두 번 나온 출력(`DUPLICATE`, 덮어쓰지 않음) 중 하나라도 있습니다.
     - `ERROR`(종료 코드 2): 조회 실패나 해석할 수 없는 출력(소문자 16진수가 아닌 ID 포함)입니다. **PASS로 취급하지 않습니다.**
   - **Ref 이름은 대소문자까지 정확히 비교합니다(A1-FIX2-01):**
     - 기대 목록과 조회 목록은 모두 `StringComparer.Ordinal` 사전에 담습니다. 패턴은 `-cmatch`만 씁니다.
     - `refs/tags/V-approved` 승인은 `refs/tags/v-approved`를 승인하지 않습니다.
     - 대소문자만 다른 두 Ref는 각각 독립된 Ref입니다. 둘 다 승인하려면 둘 다 정확한 이름과 ID로 `-Expect`에 넣습니다.
     - 이전 판(`4a3d61b`)은 PowerShell `[ordered]@{}`(대소문자 무시)를 썼습니다. 그래서 대소문자 변형 Ref가 승인 Ref로 통과하거나 두 Ref가 하나로 합쳐졌습니다.
     - `-Expect`의 `refs/heads/Main`이나 대문자 16진수 ID는 사용 오류입니다.
   - FAIL·ERROR이면 게시 미완료로 보고 OWNER에 보고합니다. 원격 Ref를 임의로 삭제하지 않습니다.
   - **한계:** 서버가 숨기는 Ref(`uploadpack.hideRefs`, GitHub의 `refs/pull/*` 등)는 `ls-remote --refs`에 나타나지 않을 수 있어 이 대조로 확인할 수 없습니다.
   - **[시험: 로컬 bare 저장소]** `selftest.ps1`의 `a1.r02_*` 12개 사례가 다음을 반복합니다.
     - 정상 main+HEAD, 승인 Annotated·Lightweight Tag는 PASS입니다.
     - 미승인 Tag, `push.followTags=true`에서 `--no-follow-tags` 없는 Push, SHA 불일치, 기대 밖 Branch, 기대 Tag 누락은 FAIL입니다.
     - `push.followTags=true`라도 `--no-follow-tags`가 있으면 PASS입니다.
     - 조회 오류는 ERROR입니다.
     - Peeled 이름이나 `main` 없는 기대값은 사용 오류입니다.
   - **[시험: reftable 로컬 bare 저장소]** `a1.fix2_*` 9개 사례가 대소문자 정확 비교를 반복합니다.
     - reftable 형식은 NTFS에서도 대소문자만 다른 Ref를 따로 보관하므로, 대소문자를 구분하는 서버와 같은 상태를 만들 수 있습니다.
     - 대상: 대소문자 변형만 있음, 두 변형 중 하나만 승인, 두 변형 모두 승인·ID 교차, 출력 중복, 대문자 ID 출력, `Main`·대문자 ID 기대값
7. 기본 브랜치 보호(강제 Push·삭제 금지)와 Tag 보호를 설정합니다(OWNER, GitHub 설정).
8. 7절 원격 조회 검증을 실행합니다.

- **주의:** 기본 Worktree(`E:\Madegame\Star_player`)에는 미추적 파일 3개(`docs/tasks/B1_B4_*_DRAFT.md`)가 있습니다. 같은 경로의 파일이 작업 브랜치에서는 추적되므로, 그 Worktree에서 `main`을 빨리 감기하면 Git이 덮어쓰기를 거부할 수 있습니다. P-1은 Worktree를 바꾸지 않고 Commit SHA로 Push하므로 영향이 없습니다. 로컬 `main`을 맞추는 일은 게시와 별개로 OWNER가 정합니다.

## 3. 포함될 OWNER·Codex 원문과 Git 이력

### 3.1 저장소 안(게시되면 공개됨)

| 위치 | 내용 | 수 | 원격 `main`(`7456f44`)에 이미 있음 |
|---|---|---|---|
| `docs/contracts/b1/originals/owner/` | OWNER 원문 O01~O19 | 19 | 0 |
| `docs/contracts/b1/originals/codex/` | Codex 요청·판정 C01~C12, P01·P02 | 14 | 0 |
| `docs/contracts/b1/originals/INDEX.md`, `index.json` | 색인 | 2 | 0 |
| `docs/contracts/b1/` 그 밖 | 승인 계약, OWNER 결정 1~3, 코드 수용 결정 등 | — | 일부 |
| `docs/reports/`, `docs/tasks/`, `Star/` | B1~G5·A-1 보고서, 작업 지시, Codex 요청서, 검증 로그 사본, Vault 기록 | — | 7456f44 이전 것만 |

- 원문 35개(`docs/contracts/b1/originals/**`)는 모두 이번 게시로 **처음 공개**됩니다(A-1 Scan `originals=35`).
- **[관찰, 변경 없음]** `index.json`은 28건(O01~O16, P01·P02, C01~C10)만 나열합니다. `INDEX.md`에는 O17~O19와 C11·C12가 없습니다. 원문·계약 기록 폴더이므로 고치지 않았습니다. 색인 보완 여부는 OWNER가 정합니다.

### 3.2 저장소 밖(게시되지 않음)

| 위치 | 내용 | 파일 | manifest SHA-256 |
|---|---|---|---|
| `E:\Madegame\g3x\codex-originals-prep\` | C11·C12 사본(저장소에도 있음) | 2 | (manifest 없음) |
| `E:\Madegame\g3x\codex-originals-b4\` | B-4 Codex 요청·판정 4건 + 첨부 1 | 7(manifest 2 포함) | `manifest.json` `723139d9a59c1891…`, `manifest-attachments.json` `06dbcce3eb8df2c1…` |
| `E:\Madegame\g3x\codex-originals-g4\` | G4 Codex 요청·판정 7건(최종 PASS 포함) + G5/A-1 2차·3차 요청·판정 4건(2026-10-29 추가) | 13(manifest 2 포함) | `manifest.json` `5ffde47be94650ae…`, `manifest-20261029-g5fix2.json` `6fcd295aa2b5d676…` |
| `E:\Madegame\g3x\owner-originals-post-o19\` | O19 이후 OWNER 지시 14건(마지막 2026-10-27T17:02Z) | 15(manifest 포함) | `6d97f887be069eba…` |

- 모두 `NOT_INDEPENDENTLY_VERIFIED`입니다(로컬 세션 기록에서 바이트 그대로 추출).
- **[미보존]** 2026-10-28 이후 OWNER 지시(G5/A-1 FIX_REQUIRED 표적 수정, 잔여 3건 수정, A1-FIX2-01 수정, 이번 운영 전환 지시)는 저장소 밖 원문 폴더에 아직 추출되지 않았습니다. OWNER가 원하면 같은 방식으로 추출합니다.
- **OWNER 결정 필요(A1-4):** 3.2의 원문을 게시 전에 저장소에 넣을지 정해야 합니다.
  - 넣는다면 3.1과 같은 위치·형식(`O20~`, `C13~`)으로 새 Commit에 추가합니다. 기존 원문과 색인은 바꾸지 않습니다. 이 경우 게시 Commit SHA가 바뀌고, 그 원문도 공개됩니다.
  - 넣지 않으면 저장소 밖 보존이 유일한 사본입니다.

### 3.3 Git 이력

- **범위:** 게시 Commit과 그 조상 전체. 이력 재작성은 없습니다.
- **수량(HEAD `5e2c640` 기준, 2026-10-29):** 전체 68개, 원격 `main`에 새로 추가되는 Commit **49개**, Merge 0개. 이 전환 작업의 문서 Commit이 더해지면 그 수만큼 늘어납니다(보고서에 최종값).
- **목록 생성(읽기 전용):** `git log --reverse --format="%H %ad %s" --date=iso-strict 7456f44fd3ecd687afce210eb5be972114f8479c..<게시 Commit>`
- **작성자 정보:** 모든 Commit의 작성자·Committer는 같은 이름·메일 1개(`junk1901`)이며, 원격 `main`의 기존 Commit에도 같은 값이 있어 새 노출이 아닙니다.
- **변경 파일:** 원격 `main` 대비 515개 파일 변경(465개 추가).
- **이진 파일(새로 공개):** `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf` 2개입니다. 원격 `main`에 **없습니다**. 규칙 문서 원본이므로 **공개 게시 전에 저작권·재배포 가능 여부를 OWNER가 판단**해야 합니다(A1-5).
  - 두 PDF는 B1-T00 승인 기준선(`docs/contracts/b1/B1-T00_BASELINE.sha256`)의 **D3 보호 파일 5개 중 2개**입니다(2026-10-29 SHA 일치 확인). 따라서 빼려면 이력 재작성만이 아니라 승인 기준선 변경도 필요합니다. 이 문서는 제외 방법을 권하지 않습니다. 공개가 불가하다고 판단되면 게시 자체를 보류하는 것이 유일한 비파괴 선택입니다.

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
- **SECRET 클래스**(하나라도 있으면 게시 중단, 종료 코드 1): 개인 키 블록, GitHub·Google·AWS·Slack·LLM 키 형태, 따옴표로 둘러싼 자격 증명 대입
- **PRIVACY 클래스**(OWNER 판단): 이메일, Windows 계정 SID, 사용자 프로필 경로, 로컬 프로젝트 경로, Windows 기계 이름

**결과 — `5e2c640` 기준(68 Commit, 공개 Ref `refs/remotes/origin/main` = 서버 `7456f44` 밖 49개, Commit 필드 340개):**

| 분류 | HEAD 결과 | 원격 `main`에 이미 있음 | 게시로 새로 공개 |
|---|---|---|---|
| SECRET 7종 | **0건** (`NO_SECRET_PATTERN_FOUND`, 패턴 기반) | — | — |
| 이메일 | 4개 값: 시험용 `t@example.invalid`·`selftest@example.invalid`, Commit 메시지의 `Co-Authored-By` 주소(`noreply@anthropic.com`), 작성자 메일 | 작성자 메일 있음 | 시험용 값과 `Co-Authored-By` 주소 |
| 계정 SID | 22건. 21건은 시험용 가짜 SID(`S-1-5-21-1-2-3-*`, `S-1-5-21-9-9-9-9`). **개발 PC 실제 사용자 SID는 1건, 1개 파일**(`docs/reports/B1_INTEGRATED_REPORT.md`) | 실제 SID 0개 파일 | **실제 SID 1개 파일** |
| 사용자 프로필 경로(`C:\Users\user…`) | 119개 파일 | 68개 파일 | 약 51개 파일 증가 |
| 로컬 프로젝트 경로(`E:\Madegame…`) | 192개 파일 | 110개 파일 | 약 82개 파일 증가 |
| 기계 이름 | 0건 | — | — |

- 기록: `E:\Madegame\g3x\g5fix3\a1scan_5e2c640\`(`findings.tsv`에 전체 값. 저장소 밖에 둠)
- **판단:** Public 상태 자체는 차단 조건이 아닙니다(OWNER 2026-10-29). 실제 자격 증명·비밀값은 발견되지 않았습니다.
- **OWNER 판단 항목(A1-5):**
  - 개발 PC 계정 SID 1건, 로컬 경로, 2개 PDF가 **공개**되는 것을 수용하는지 정해야 합니다.
  - SID는 로컬 계정 식별자이며 자격 증명이 아닙니다. 원문·보고서 바이트를 바꾸지 않는 원칙상 제거하려면 이력 재작성이 필요하고, 그러면 `7bdde7d`·`a980ea5`·`1751fe0` 결속이 깨집니다. 따라서 **수용**을 권고합니다(PDF는 저작권 판단이 별도로 필요).
- **운영 값 주의:** 앞으로 운영 SID·Pipe 이름·설치 경로를 문서에 넣으면 그 Commit도 **공개** 게시 범위입니다. 운영 값은 R-AUDIT·설치 설정 등 저장소 밖에만 두는 것을 권고합니다([운영 전환 계획](B1_PHASE3_OPERATIONS_TRANSITION.md) 3절).
- **자격 증명:** Push 자격 증명(토큰)은 저장소·세이브·로그·증거·설치 설정에 넣지 않습니다.
- **한계:** 정규식 검사이므로 "없음"을 증명하지 않습니다. Text가 아닌 파일은 경로 목록만 확인합니다.
  - 결과 표기는 `NO_SECRET_PATTERN_FOUND (pattern-based; not proof that no secret exists)`입니다. 패턴 0건은 기밀정보 부재와 다릅니다.

## 5. SourceLink 공식 URL

- **방식:** .NET 8 SDK 내장 SourceLink(별도 패키지 없음)는 빌드하는 clone의 `remote.origin.url`과 HEAD Commit에서 URL을 만듭니다. 저장소 설정 파일(`Directory.Build.props` 등)은 **바꾸지 않습니다**. 바꿀 필요도 없습니다.
  - `https://github.com/junk1901/Star_player.git` → `https://raw.githubusercontent.com/junk1901/Star_player/<commit>/*`
- **삽입 위치[실측]:**
  - URL과 Commit은 각 StarPlayer PDB의 SourceLink JSON에만 들어갑니다.
  - 어셈블리 특성에는 저장소 URL이나 Commit이 없습니다. `IncludeSourceRevisionInInformationalVersion=false`이므로 `AssemblyInformationalVersion`은 `0.0.0-phase0`입니다.
  - DLL에는 PDB 식별값(CodeView GUID, `PdbChecksum`)이 들어가므로, URL이나 Commit이 바뀌면 **DLL 바이트도 바뀝니다**.
- **[실측: 진단, 2026-10-29]** `1751fe0`의 임시 clone(작업 후 삭제)에서 `StarPlayer.Domain`을 `ContinuousIntegrationBuild=true`로 빌드했습니다. 원격 조회는 없었습니다. 기록: `E:\Madegame\g3x\phase3ops\sourcelink_url_case.txt`.

| `origin` | SourceLink | DLL SHA-256 |
|---|---|---|
| `https://github.com/junk1901/Star_player.git` | `{"documents":{"/_/*":"https://raw.githubusercontent.com/junk1901/Star_player/1751fe04…/*"}}` | `aec9ca30794d0f63…` |
| `https://github.com/junk1901/Star_player`(`.git` 없음) | 위와 같음 | `aec9ca30794d0f63…`(같음) |
| `https://github.com/junk1901/star_player.git`(소문자) | `…/junk1901/star_player/1751fe04…/*` | `afaa68871d3d9c97…`(**다름**) |

  - `ContinuousIntegrationBuild=true`이면 문서 경로와 CodeView PDB 경로가 `/_/…`로 바뀌어 clone 위치가 바이트에 들어가지 않습니다.
  - 이 진단 SHA는 공식 값이 아닙니다(게시 전 Commit, 단일 프로젝트).
- **공식 빌드 규칙:**
  1. 승인 게시 뒤 **공식 원격에서 직접** `git clone --no-hardlinks https://github.com/junk1901/Star_player.git <dir>`로 받습니다. 로컬 사본에서 clone하거나 `remote set-url`로 바꾸는 방식은 진단 전용입니다.
  2. 빌드 전에 `git -C <dir> remote get-url origin`이 위 Clone URL과 **대소문자까지** 같은지(`-ceq`) 확인합니다.
  3. `git checkout --detach <source_git_sha>` 뒤 `git rev-parse HEAD`가 `source_git_sha`와 같은지 확인합니다. SourceLink Commit은 이 HEAD에서 만들어집니다.
  4. 빌드 뒤 모든 StarPlayer PDB의 SourceLink JSON이 `{"documents":{"/_/*":"https://raw.githubusercontent.com/junk1901/Star_player/<source_git_sha>/*"}}`와 정확히 같은지 확인합니다. 키가 `/_/*`가 아니거나, URL·Commit이 다르거나, SourceLink가 없으면 실패입니다([B-4 빌드 절차](B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md) 4절 9단계).
- **폐기·구분:**
  - 이전 판의 신규 비공개 저장소 후보 URL(`https://github.com/junk1901/star-player-official.git` 등)은 공식 설정에서 **제거**합니다. 그 URL로 만든 진단 빌드 SHA는 어디에도 쓰지 않습니다.
  - 지금까지의 모든 빌드 SHA(G3 진단, `a980ea5`·`1751fe0` 개발 빌드 등)는 진단값입니다. 공식 Artifact SHA는 승인 게시와 승인 소스 Commit이 정해진 뒤 공식 clone에서 새로 측정합니다.
  - 실험용 clone의 `origin`(`E:\Madegame\g3x\g4clean3` 등)은 진단 통제값입니다. 공식 clone과 혼동하지 않습니다.
- **확인 도구:** 기존 G3 진단 도구 `pedump`(저장소 밖 `E:\Madegame\g3x\tools\pedump-bin`, System.Reflection.Metadata 기반)를 씁니다.

## 6. 공식 원격 기준 빌드·SHA 재검증 절차 (승인 A 뒤)

1. **두 clone:** 공식 원격에서 경로 길이가 다른 두 빈 폴더로 `git clone --no-hardlinks https://github.com/junk1901/Star_player.git <dir>`을 실행하고 `git checkout --detach <승인 소스 Commit>`으로 맞춥니다. 5절 2~3번 확인을 합니다.
2. **전체 검증:** 각 clone에서 다음을 실행합니다. 모두 기존 기준과 같아야 합니다.
   - `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1`(GATE PASS)
   - Golden 4/4
   - Dry Pilot 9회 동일
3. **공식 후보 빌드:** `dotnet build StarPlayer.sln -c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false -p:ContinuousIntegrationBuild=true`
4. **Artifact 목록:** 각 clone에서 다음을 실행합니다.
   - `tools\b1-ops\b4-candidates.ps1 -BuildRoot <dir> -SourceCommit <sha> -Out <json> -Previous <이전 후보>`
   - 두 clone의 모든 행 SHA가 같아야 합니다(경로 독립 재현성).
5. **SourceLink:** 5절 4번 확인. URL = 공식 원격, Commit = 승인 소스 Commit
6. **Godot PCK:** 5회 전체 바이트 비교(B-4 5절). 지금까지 재실행하지 않았습니다.
7. **보존:** 명령·출력·SHA를 저장소 밖 진단 디렉터리에 보존하고 OWNER·Codex에 제출합니다. Build Provenance 등록은 승인 C입니다.

## 7. 원격 Commit 및 소스 조회 검증 절차 (승인 A 뒤)

[B-4 규범 결정 자료](B1_B4_OWNER_NORMATIVE_DECISION_DRAFT.md) 1.3절을 그대로 씁니다. 공개 저장소이므로 조회에는 자격 증명이 필요 없습니다.

1. `git ls-remote --refs https://github.com/junk1901/Star_player.git`(`HEAD`·Peeled 줄 제외, 2.2절 6단계): 광고된 `refs/heads/main`(과 승인 Tag)이 승인 Commit이거나 그 자손인지 확인합니다.
2. 빈 저장소에서 `git fetch --no-tags https://github.com/junk1901/Star_player.git <승인 Commit>`: 객체 SHA가 일치하는지 확인합니다.
3. 6절 빌드 PDB의 SourceLink가 공식 raw URL + 승인 Commit인지 확인합니다.
4. PDB에 기록된 문서마다 비교합니다.
   - raw 바이트(`https://raw.githubusercontent.com/junk1901/Star_player/<commit>/<path>`) ↔ `git cat-file blob <commit>:<path>` 바이트
   - PDB 문서 체크섬 ↔ 체크아웃 파일
5. 하나라도 불일치·조회 실패면 공식 원격 출처 검증 미완료(Fail-Closed)입니다.

## 8. OWNER 승인 A(기존 공식 GitHub 최초 게시)에 필요한 결정

| # | 결정 | 상태·권고 |
|---|---|---|
| A1-1 | 공식 원격 URL | **결정됨(2026-10-29):** `https://github.com/junk1901/Star_player.git`, Public 유지 |
| A1-2 | 게시 Commit과 Ref | P-1: `refs/heads/main` = 이번 전환 작업의 마지막 문서 Commit(보고서 SHA), Fast-forward, 비강제 |
| A1-3 | Tag 게시 | 최초 Push에서는 **게시하지 않음** 권고. B1 Tag를 게시하려면 정확한 이름을 OWNER가 별도 승인 |
| A1-4 | 3.2 저장소 밖 원문의 저장소 편입 | 편입 시 원문도 공개되고 A1-2 SHA가 바뀜. OWNER 판단 |
| A1-5 | 4절 PRIVACY 항목과 2개 PDF의 공개 | 실제 SID 1건·로컬 경로는 수용 권고. PDF 저작권·재배포는 OWNER 판단 |
| A1-6 | 승인 소스 Commit(공식 빌드 기준) | 제품 코드는 `a980ea5`와 같습니다. 최종 검증 소스 `1751fe0` 또는 그 뒤 문서 Commit 중 OWNER가 정함. 확정 뒤 6절 재검증 |
| A1-7 | Push 자격 증명 | OWNER 세션의 Git 자격 증명 관리자 사용. 토큰을 파일·로그·증거에 남기지 않음 |
