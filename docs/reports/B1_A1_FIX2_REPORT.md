# B1 A-1 A1-FIX2-01 단일 MINOR 수정 보고서 (Ref 이름 정확 비교)

- 작성: Claude Code, 2026-10-29. 근거: OWNER 지시 "A1-FIX2-01 단일 MINOR 수정 및 최종 표적 검수"(2026-10-29)
- **이번 Codex 판정(FIX_REQUIRED):**
  - G5-FIX-01 RESOLVED(유지보수 격리 전제), G5-R05 RESOLVED, 기존 RESOLVED 6건 재발 없음, G4 PASS 유지
  - 신규 MAJOR 0, 잔여 MINOR **A1-FIX2-01** 1건
  - 원문은 4절대로 보존했습니다(`NOT_INDEPENDENTLY_VERIFIED`).
- **기준:**
  - 작업 시작 HEAD `a89f0d5fde45e69e6eda7f02860feb7d1e1b498e`, 작업 트리 Clean(직접 확인)
  - 이전 검증 소스 `4a3d61b7f4b30db5cc0c5061ad0adcbf90e340df`, G4 수용 소스 `a980ea5`
- **소스(개발·검증 기준) Commit:** **`1751fe04aaafbda65462915d2dac89e56536a8ae`**(로컬 전용, Push 없음)
  - 변경 파일: `tools/b1-ops/a1-refcheck.ps1`, `tools/b1-ops/selftest.ps1`, `docs/tasks/B1_A1_PUBLICATION_PREP.md`
  - 이 보고서·요청서·`CURRENT_STATE.md`·Dashboard·verify 로그 사본은 그 뒤의 **후속 문서 Commit**입니다. 검증 대상이 아닙니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
- Codex 재검수는 `미실시`입니다. 요청서는 [B1_A1_FIX2_CODEX_REVIEW_REQUEST.md](../tasks/B1_A1_FIX2_CODEX_REVIEW_REQUEST.md)입니다.

## 1. 수정

**원인:** `a1-refcheck.ps1`은 기대 목록과 조회 목록을 `[ordered]@{}`에 담았습니다. 이 사전의 키 비교는 대소문자를 무시합니다.

- 승인된 `refs/tags/V-approved`만 있을 때, 원격의 미승인 `refs/tags/v-approved`가 같은 키로 조회되어 통과했습니다(객체 ID가 같으면 PASS).
- 원격에 두 변형이 함께 있으면 하나의 키로 합쳐져, 한쪽이 보이지 않았습니다.
- 같은 이유로 `-match`·`-notmatch`도 대소문자를 무시했습니다.
  - 기대값 `refs/heads/Main`이 `main`으로 통과했습니다.
  - 대문자 16진수 ID가 기대값·조회 결과 모두에서 통과했습니다.

**수정(`a1-refcheck.ps1`만):**

| 요구 | 구현 |
|---|---|
| 1·3·9 Ordinal, 같은 정책, 암묵적 비구분 연산 없음 | 기대·조회 목록 모두 `Dictionary[string,string]`(`StringComparer.Ordinal`). 조회는 `ContainsKey`만, 출력 순서는 `Array.Sort(..., StringComparer.Ordinal)`. 패턴은 모두 `-cmatch`/`-cnotmatch`. `-eq`·`-contains`·`-in`·`Compare-Object`·`Sort-Object -Unique`·`ToLower`/`ToUpper` 정규화는 Ref 이름에 쓰지 않습니다. |
| 2·5 대소문자 정확, 변형은 독립 Ref | 위 사전으로 `V-approved`와 `v-approved`가 각각 키가 됩니다. |
| 4 중복 거부 | 조회 결과에 같은 이름이 두 번 나오면 덮어쓰지 않고 `DUPLICATE`(FAIL). `-Expect` 중복은 사용 오류. |
| 6·7·8 미승인·누락·SHA 불일치 거부 | `UNEXPECTED`·`MISSING`·`MISMATCH`, 모두 FAIL(종료 코드 1) |
| SHA 비교 분리 | ID는 이름과 따로, 소문자 16진수(`-cmatch`) 확인 뒤 `String.Equals(..., Ordinal)`로 비교 |
| 10 Fail-Closed | 조회 실패·형식 오류(대문자 ID 포함)는 `ERROR`(종료 코드 2), PASS 아님 |
| 기존 정책 유지 | `git ls-remote --refs`(HEAD·Peeled 줄 제외), Tag 객체 ID 비교, `--no-follow-tags` 게시는 그대로 |
| 추가 | `-Expect`는 마지막 `=`에서 나눕니다. Ref 이름에는 `=`가 들어갈 수 있지만 객체 ID에는 없기 때문입니다. `-Remote`의 `-` 시작 검사도 Ordinal입니다. |

**관련 결함 수정(게시 절차, `B1_A1_PUBLICATION_PREP.md` 2.2절):**

- 실측: Windows 일반(loose ref) 저장소에서 `git rev-parse refs/tags/v-approved`와 `git show-ref --verify refs/tags/v-approved`(틀린 대소문자)가 `V-approved`의 ID를 돌려주었습니다. `show-ref`는 틀린 이름을 그대로 출력했습니다. 정확한 결과를 낸 것은 `git for-each-ref`뿐이었습니다(빈 출력).
  - 증거: `g5fix3\git_wrongcase_lookup.txt`(`88c76a9d…`), git 2.45.1.windows.1
- 이전 절차는 기대 ID를 `git rev-parse`로 얻고 Tag를 이름(`refs/tags/<tag>:refs/tags/<tag>`)으로 Push하게 했습니다. 이름을 틀린 대소문자로 쓰면, 로컬은 다른 이름의 Tag를 찾아 그 이름으로 원격에 만들 수 있었습니다.
- 수정:
  - Tag 객체 ID는 `git for-each-ref --format="%(objectname) %(refname)" refs/tags/<승인 Tag>`로 읽습니다. 출력 이름이 승인 이름과 바이트 단위로 같아야 합니다.
  - Tag Push 원본은 그 객체 ID입니다(`<Tag 객체 SHA>:refs/tags/<승인 Tag 정확한 이름>`).
- 같은 이유로 Windows 일반 로컬 저장소에는 대소문자만 다른 Tag 두 개가 함께 있을 수 없습니다(`git tag`가 "already exists"로 거부). 대소문자를 구분하는 원격에는 있을 수 있으므로 게시 후 대조는 정확 비교가 필요합니다.

## 2. RED / GREEN 반례

반례 실행기 `g5fix3\a1fix2_cases.ps1`(`2bb69d4e…`)는 같은 19개 사례를 지정한 도구에 실행합니다. 사례마다 기대 판정, 실제 종료 코드, `RESULT` 줄, 일치 여부를 기록합니다.

- 원격은 격리된 로컬 bare 저장소입니다.
- 원본·원격 모두 **reftable** Ref 형식(git 2.45+)입니다. NTFS에서도 대소문자만 다른 Ref를 따로 보관하므로, 대소문자를 구분하는 서버와 같은 상태를 만듭니다.
- 출력 중복·대문자 ID 출력은 `ls-remote` 출력을 고정 파일로 내는 시험용 `git` 대역(`.cmd`)으로 만듭니다.

| 사례 | 내용 | 기대 | RED(`4a3d61b` 도구) | GREEN(`1751fe0`) |
|---|---|---|---|---|
| C01 | 정상 main만 | PASS | PASS | PASS |
| C02 | 정확히 승인된 Tag `V-approved` | PASS | PASS | PASS |
| C03 | `V-approved` 승인, 원격에는 `v-approved`만(같은 객체) | FAIL | **PASS(오판)** | FAIL: `UNEXPECTED v-approved`, `MISSING V-approved` |
| C04a | 두 변형 공존(같은 객체), `V`만 승인 | FAIL | **PASS(오판)** | FAIL: `UNEXPECTED v-approved` |
| C04b | 두 변형 공존(다른 객체), `v`만 승인 | FAIL | **PASS(오판)** | FAIL: `UNEXPECTED V-approved` |
| C05a | 두 변형 모두 각자 ID로 승인 | PASS | **USAGE(승인 불가)** | PASS, `remote_refs=3` |
| C05b | 두 변형 모두 승인, ID 교차 | FAIL | **USAGE** | FAIL: `MISMATCH` 2건 |
| C06a | `ls-remote` 출력에 같은 Ref 두 번 | FAIL | **PASS(덮어씀)** | FAIL: `DUPLICATE` |
| C06b | `-Expect`에 같은 Ref 두 번 | USAGE | USAGE | USAGE |
| C07 | 승인 Tag 누락 | FAIL | FAIL | FAIL: `MISSING` |
| C08 | 예상 밖 Branch | FAIL | FAIL | FAIL: `UNEXPECTED refs/heads/feature` |
| C09 | Tag 객체 대신 Peeled Commit ID 기대 | FAIL | FAIL | FAIL: `MISMATCH` |
| C10 | 원격 조회 실패 | ERROR | ERROR | ERROR(종료 코드 2) |
| C11 | Annotated Tag Peeled 줄 제외(`--refs` 없는 출력에 HEAD 1·Peeled 1줄 확인) | PASS | PASS | PASS |
| C12a | `push.followTags=true`, `--no-follow-tags` 없음 | FAIL | FAIL(`remote_refs=3`, **두 변형이 합쳐져 하나 누락**) | FAIL(`remote_refs=4`, 미승인 Tag 3개 모두 `UNEXPECTED`) |
| C12b | `push.followTags=true`, `--no-follow-tags` 있음 | PASS | PASS | PASS |
| C13 | 기대값 `refs/heads/Main` | USAGE | **PASS(오판)** | USAGE |
| C14 | 기대값 대문자 ID | USAGE | **PASS** | USAGE |
| C15 | 출력에 대문자 ID | ERROR | **PASS** | ERROR |

- **RED:** 19건 중 9건이 기대와 다름. `g5fix3\RED_a1-refcheck_4a3d61b.txt`(`805db178…`).
  - 대상 도구 `g5fix3\a1-refcheck_4a3d61b.ps1`(`8f5a5a5e…`)는 `git show 4a3d61b:tools/b1-ops/a1-refcheck.ps1`와 Blob ID가 같음을 확인했습니다.
- **GREEN:** 0건 다름.
  - 작업 트리: `GREEN_a1-refcheck_worktree.txt`(`4984ed5e…`)
  - Clean Checkout `1751fe0` 사본: `GREEN_a1-refcheck_clean_1751fe0.txt`(`9feb66ab…`). 도구 SHA-256 `e7dbb4db…`는 작업 트리와 같습니다.
- 실행 환경: Windows PowerShell 5.1.26100.9711(Desktop), git 2.45.1.windows.1, 비승격.
- **실행기 결함 기록:** 첫 실행기는 두 Tag ID를 `$tV`·`$tv`에 담았습니다. PowerShell 변수 이름은 대소문자를 무시하므로 같은 변수였고, 두 "다른" Tag가 사실상 하나가 되어 C05b가 GREEN에서 오판으로 나왔습니다.
  - 원본 저장소도 처음에는 loose ref라 두 변형을 만들 수 없었습니다.
  - 변수 이름을 `$tagUpper`·`$tagLower`로 바꾸고, 원본을 reftable로, Push 원본을 객체 ID로 바꾸고, 두 ID가 달라야 실행되도록 단언을 넣었습니다.
  - 위 RED·GREEN 기록은 모두 고친 실행기로 다시 만든 것입니다. 첫 실행 출력은 덮어써져 보존되지 않았습니다.
  - 이 경험이 5절 자체 점검의 근거입니다.

**자체 시험(`selftest.ps1`):** 109 PASS / 0 FAIL(기존 100건 + 신규 `a1.fix2_*` 9건), 비승격.

- 신규 사례: 두 변형 Tag 준비 단언, 변형만 존재, 두 변형 공존·하나 승인, 다른 변형 미승인, 두 변형 모두 승인, ID 교차, 출력 중복, 출력 대문자 ID, `Main`·대문자 ID 기대값
- 작업 트리: `g5fix3\selftest_worktree_pre1751fe0.json`(`6c542bfa…`)
- Clean Checkout `1751fe0` 사본: `g5fix3\selftest_clean_1751fe0.json`(`0da2743c…`). `verify.ps1`과 동시에 실행했습니다.

## 3. Clean Checkout 회귀

- **Clone:** 기존 `E:\Madegame\g3x\g4clean3`를 재사용했습니다(새 검증 폴더 없음).
  - 작업 트리는 Clean이었습니다. 작업 브랜치에서 로컬 fetch → `checkout --detach 1751fe0` → `git clean -fdx`(이전 실행의 빌드 산출물만 삭제, 증거 아님) → 작업 트리 0건·무시 파일 0건
  - `origin`은 실험 통제값 그대로입니다(조회·Push 없음).
- **`tools\verify.ps1`:** exit 0, **`GATE: PASS`**

| 단위 | 결과 |
|---|---|
| T1~T3 SDK·Godot·Export Template | PASS |
| B1~B3 Release 빌드(경고 0) | PASS |
| U1 Domain | 327 / 0 / 0 |
| U2 Simulation | 741 / 0 / 0 |
| U3 Application(U3a 14, U3b 48 포함) | 128 / 0 / 0 |
| U4 Infrastructure | 1,237 / 0 / 0 |
| U5 SimHarness | 538 / 0 / 0 |
| U6 EvidenceVerifier | 90 / 0 / 0 |
| **합계** | **3,061 PASS / 0 FAIL / 0 SKIP** (기존과 같음) |
| Godot G1~G4, Export E1·E1b·E2~E4 | PASS |
| D1 문서 링크 | PASS, 252개 파일(`a89f0d5`의 문서 3개 추가분) |
| D3 승인 계약·보호 파일 | PASS, 5개 일치 |
| PCK SHA-256 | `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799` — 이전과 같음(이번 Export 1회의 SHA 확인) |

- **기록:**
  - verify 로그 사본 [B1_A1FIX2_VERIFY_LOG_clean.md](B1_A1FIX2_VERIFY_LOG_clean.md)(`6dd6ebd1…`), 저장소 밖 사본 `g5fix3\PHASE_2_VERIFY_LOG_1751fe0.md`
  - stdout `g5fix3\verify_1751fe0_stdout.txt`(`1ccabb80…`)
  - 실행 뒤 Clone의 verify 로그 변경을 되돌렸습니다(작업 트리 0건).
- **자체 시험(Clean Checkout 사본):** 109 PASS / 0 FAIL, GREEN 반례 0/19(2절)
- **A-1 Secret·Privacy 검사:** `1751fe0`, 67개 Commit(공개 Ref 밖 48개), Commit 필드 335개 → **SECRET 0건**, `NO_SECRET_PATTERN_FOUND (pattern-based; not proof that no secret exists)`. 출력 `g5fix3\a1scan_1751fe0`
  - 후속 문서 Commit은 Commit 뒤 같은 검사를 실행하며, 결과는 `g5fix3\a1scan_<문서 Commit>`과 OWNER 전달 요약에 남깁니다.
- **재실행하지 않은 것(이전 증거 재사용):** 제품·게임·검증기 코드 변경이 없습니다.
  - Golden 4/4, Dry Pilot 9회: `a980ea5` G4-FIX2 증거 재사용
  - PCK 5회 재현성 시험: **이번에도 재실행하지 않았습니다(미검증 유지).** 단일 Export SHA만 확인했습니다.

## 4. Codex 원문 보존

- 로컬 Codex 세션 로그 `C:\Users\user\.codex\sessions\2026\10\04\rollout-2026-10-04T21-41-07-01a106ee-aab7-7a10-8f22-d304b84ba8ee.jsonl`에서 찾았습니다(읽기 전용).
- 각 행의 `payload.content[0].text`를 UTF-8 바이트 그대로 `E:\Madegame\g3x\codex-originals-g4\`에 새 파일로 저장했습니다. 기존 파일·`manifest.json`은 바꾸지 않았습니다.
- 새 목록 `manifest-20261029-g5fix2.json`(`6fcd295a…`)에 원본 경로·행·역할·시각·바이트 수·본문 SHA-256·원본 행 SHA-256·비밀정보 패턴 결과를 기록했습니다.

| 행 | 역할 | 시각(UTC) | 파일 | SHA-256 |
|---|---|---|---|---|
| 14101 | user | 2026-10-28T13:26:57.525Z | `20261028T132657Z_request-g5-a1-b4-fix-targeted-readonly-review.txt` | `b361f5f6…` |
| 14268 | assistant | 2026-10-28T13:37:38.292Z | `20261028T133738Z_verdict-g5-a1-fix-required-g5fix01-r05-a1r02.txt` | `f6ab0843…` |
| 14278 | user | 2026-10-28T15:31:38.069Z | `20261028T153138Z_request-g5-a1-residual3-readonly-review.txt` | `a71f0625…` |
| 14382 | assistant | 2026-10-28T15:39:31.137Z | `20261028T153931Z_verdict-g5-a1-fix2-fix-required-a1fix201.txt` | `305dd763…` |

- 비밀정보 패턴 결과는 네 파일 모두 0건입니다.
- **상태: `NOT_INDEPENDENTLY_VERIFIED`.** 로컬 세션 로그에서 복사한 것이며, 그 로그 자체가 독립 인증된 원본은 아닙니다. 대화에서 옮긴 내용이 아닙니다.
- 이로써 2차 보고서(`B1_G5_FIX2_REPORT.md`)가 "원문 미보존"으로 적은 13:37 판정도 보존되었습니다. 그 보고서 본문은 이력으로 그대로 둡니다.

## 5. 자체 점검 (같은 종류의 문제 재발 방지)

| 질문 | 답 |
|---|---|
| 모든 Ref 이름 비교 경로가 대소문자를 구분하는가? | **예.** `a1-refcheck.ps1`에서 Ref 이름은 Ordinal 사전의 `ContainsKey`·Ordinal 정렬·`-cmatch`로만 다룹니다. 수정 뒤 파일 전체를 `-eq`·`-ne`·`-contains`·`-in`·`-match`·`-like`·`Compare-Object`·`Sort-Object`·`ToLower`/`ToUpper`·`.Contains(`·`IndexOf`·`@{}`·`[ordered]`·`HashSet`으로 검색했습니다. 남은 것은 설명 주석과 정수 종료 코드 비교(`$code -ne 0`)뿐입니다. |
| 키 저장은 구분하지만 조회는 구분하지 않는 경로가 남았는가? | **없습니다.** 저장·조회 모두 같은 Ordinal 사전입니다. PowerShell 인덱서(`$have[$ref]`)도 그 사전의 비교자를 따릅니다(C05a·C05b에서 각 변형의 ID가 따로 비교됨). |
| 두 Ref가 하나로 합쳐지는 경로가 남았는가? | **없습니다.** 조회 결과는 `Add`로만 넣고, 이미 있으면 `DUPLICATE`로 거부합니다(C04a `remote_refs=3`, C12a `remote_refs=4`). |
| 정상 Ref가 잘못된 ERROR를 내는가? | **아닙니다.** C01·C02·C05a·C11·C12b PASS. git이 내는 ID는 소문자 16진수이므로 `-cmatch` 형식 검사에 걸리지 않습니다. |
| 미승인 Ref가 PASS할 수 있는가? | **아닙니다.** 기대 목록에 Ordinal로 없는 Ref는 모두 `UNEXPECTED`입니다. 기대 목록은 `refs/heads/main`(정확한 소문자)과 `refs/tags/*`만 받습니다. |
| 실제 Windows PowerShell에서 비교자 동작이 같은가? | **예.** 모든 시험을 실제 사용 환경인 Windows PowerShell 5.1(`powershell.exe`)에서 실행했습니다. .NET Framework `Dictionary`·`StringComparer.Ordinal`은 PowerShell 버전에 따른 차이가 없습니다. |

**범위 밖 관찰(수정하지 않음):**

- `a1-scan.ps1`은 Ref 이름을 비교하지 않습니다(`-PublicRef`는 git에 그대로 전달). 다만 개인정보 요약의 작성자 식별값 집계(`$ids = @{}`)는 대소문자를 무시하므로, 대소문자만 다른 두 식별값이 하나로 세어질 수 있습니다. 검사 자체(필드별 패턴 판정)에는 영향이 없고 Ref 비교도 아니므로 이번 범위에서 고치지 않았습니다.
- 실행기 자체의 변수 충돌(2절)도 같은 종류의 PowerShell 함정입니다. 저장소 시험(`selftest.ps1`)은 처음부터 `$tagUpper`·`$tagLower`를 썼고, 두 ID가 다름을 단언합니다.

## 6. Codex 지적의 운영 조건 (판정 대상 아님, 기록)

- Codex는 `SetKernelObjectSecurity`(G5-FIX-01의 객체 단위 쓰기)에 대해 Microsoft가 파일 시스템 객체에는 다른 보안 API를 쓰도록 안내한다고 지적했습니다. 이번 실측을 일반적인 지원 보장으로 확대할 수 없다는 뜻입니다.
- OWNER 지시("기존 G4 및 G5 수용 코드는 변경하지 않는다")에 따라 코드는 바꾸지 않았습니다. 운영 G5 실측 조건으로 남깁니다. 운영 기계에서 Apply 뒤 Verify와 SDDL 대조로 확인해야 합니다.
- G5-FIX-01의 사후 복구는 사전 예방과 같은 보장이 아니라는 Codex 판단에 동의합니다. 유지보수 격리는 운영 전제입니다.

## 7. 불변 확인

- `git diff --stat a980ea5 1751fe0 -- src game tests tools/StarPlayer.EvidenceVerifier tools/StarPlayer.SimHarness tools/verify.ps1 TASK.md docs/contracts content` → **0줄**
- `git diff --stat 4a3d61b 1751fe0 -- tools/b1-ops/g5-acl.ps1 tools/b1-ops/g5-pins.ps1 tools/b1-ops/B1OpsNative.cs tools/b1-ops/g5-probe.ps1 tools/b1-ops/b4-candidates.ps1 tools/b1-ops/a1-scan.ps1` → **0줄**(G5·B-4·A-1 Scan 수용 도구 불변)
- B1 Tag `b1-code-accepted-2026-10-26` → `7bdde7dd091f9a66957bc740c77d470c7732b3ad` 불변
- 승인 계약, OWNER 원문, D3 보호 파일, 루트 `TASK.md`, Golden, Hash, RNG, Replay는 바꾸지 않았습니다.
- 운영 ACL 적용, Trust Anchor·Pin 등록, A-1 원격 생성·Push, Tag 생성·변경, 공식 G1~G9, Build Provenance 등록, Git 이력 재작성은 하지 않았습니다.
