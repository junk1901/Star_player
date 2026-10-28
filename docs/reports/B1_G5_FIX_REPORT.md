# B1 G5·A-1 Codex FIX_REQUIRED 표적 수정 보고서

- 작성: Claude Code, 2026-10-28. 근거: OWNER 지시 "G5/A-1 Codex FIX_REQUIRED 표적 수정"(2026-10-28)
- **Codex 판정 원문:** `E:\Madegame\g3x\codex-originals-g4\20261028T113717Z_verdict-g5-a1-fix-required.txt`(`6f126dbb…`). 요청 원문은 `20261028T112154Z_request-g5-a1-readonly-review.txt`(`a3d10456…`)이며, 둘 다 저장소 밖 보존(manifest `5ffde47b…`, NOT_INDEPENDENTLY_VERIFIED)입니다.
- **기준:**
  - 검수 기준 HEAD `2e588093097baf0d7450248e224d2f51155ce6b6`
  - 이전 Clean Checkout 소스 `1207767`, G4 수용 소스 `a980ea5`
- **수정 Commit:** `306c402cbd0543d05dd9b63b075f608740e2b992`(로컬 전용, Push 없음)
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
- Codex 재검수는 `미실시`입니다. 요청서는 [B1_G5_FIX_CODEX_REVIEW_REQUEST.md](../tasks/B1_G5_FIX_CODEX_REVIEW_REQUEST.md)입니다.

## 1. Finding별 수정

| Finding | 수정 | 위치 |
|---|---|---|
| **G5-R01** MAJOR | Apply는 스냅샷·생성·변경보다 먼저 **사전 점검**을 수행하고, 실패하면 `REFUSED`로 끝냅니다(종료 코드 1, 스냅샷 없음). 아래 표에 단계별로 정리했습니다. | `g5-acl.ps1` `Invoke-Preflight`, `B1OpsNative.cs` |
| **G5-R02** MAJOR | Apply Root 아래의 `apply: false` Root는 **이미 존재하고 보호 DACL**일 때만 진행합니다. 상속을 허용하거나 없으면 Apply 전에 거부합니다. 보호된 경우에도 하위 트리 SDDL 전후를 비교합니다(탐지용). R-ARCHIVE 권한 값은 도구가 정하지 않습니다. | `Invoke-Preflight`, Apply 사후 비교 |
| **G5-R03** MAJOR | 아래 표에 정리했습니다. **자동 복구는 트랜잭션이 아닙니다**(문서·도구 설명에 명시). | `Restore-Snapshot`, Apply `catch`, 스크립트 `trap` |
| **G5-R04** MINOR | 관리자 그룹 직접 구성원 조회가 실패하면 principals **FAIL**입니다(이전에는 설명만 남기고 PASS 가능). 중첩 그룹 미해석은 실패가 아닌 별도 안내(note)로 표시합니다. | `Invoke-Verify` |
| **G5-R05** MINOR | 아래 표에 정리했습니다. | `g5-pins.ps1` `Step-Tree`·`Adopt` |
| **A1-R01** MINOR | 각 조상 Commit 객체를 원시 바이트(`git cat-file commit`)로 읽어 첫 빈 줄에서 나눕니다. 메시지 안 구분 문자가 범위를 자르지 않습니다. 검사 필드는 아래와 같습니다. | `a1-scan.ps1` |
| **A1-R02** MINOR | 게시 절차를 바꿨습니다. 내용은 아래와 같습니다. | [A-1 문서](../tasks/B1_A1_PUBLICATION_PREP.md) 2.2절 |
| **B4-R01** MINOR | 아래 표에 정리했습니다. 상태를 `CANDIDATE_INCOMPLETE`로 바꿨습니다. | `b4-candidates.ps1` |

**G5-R01 사전 점검 단계:**

| 단계 | 내용 |
|---|---|
| 1 | 드라이브 Root부터 각 Apply Root까지 존재하는 모든 구성요소를 **삭제 공유 없이, 데이터 접근을 포함해, Reparse를 따르지 않는 핸들**로 엽니다. Apply가 끝날 때까지 유지합니다. |
| 2 | 핸들로 Reparse 여부와 **최종 경로 = 기대 경로**를 확인합니다. 문자열 접두사 대신 실제 경로로 격리를 판정합니다. |
| 3 | Apply Root가 아닌 기존 상위는 상위 규칙을 만족해야 합니다(신뢰된 상위). |
| 4 | Apply Root 아래 Reparse나 읽을 수 없는 항목이 있으면 거부합니다(이전에는 건너뜀). |
| 5 | 운영 모드에서는 레이아웃 주체 프로세스가 없어야 합니다(유지보수 조건). |

- 새 디렉터리는 최종 보호 보안 설명자를 가진 채 생성합니다.
- 변경은 모두 핸들로 적용합니다.

**G5-R03 복구 방식:**

- 스냅샷 항목을 **각각** 복구합니다. 한 항목의 실패는 기록하고 나머지를 계속합니다.
- 복구 뒤 실제 상태를 다시 읽어 비교합니다.
- 새로 만든 디렉터리는 **중간 디렉터리까지** 생성 직후 일지(`<snapshot>.created`)에 기록합니다. 복구 때는 깊은 것부터 비어 있으면 제거합니다. 일지에 없는 기존 경로는 삭제하지 않습니다.
- 다음 경우는 모두 `residue`로 나열하고 `ROLLBACK_FAILED`·종료 코드 3입니다. Apply가 변경을 시작한 뒤의 예기치 않은 예외도 종료 코드 3입니다.
  - 차이·실패가 하나라도 있음
  - 제거하지 못한 생성 경로가 있음
- `ROLLED_BACK`(1)은 차이 0이고 생성 경로가 남지 않았을 때만 나옵니다.

**G5-R05 프로세스 소속 규칙:**

- 인스턴스는 (PID, **정확한** 생성 시각)으로 식별합니다. 생성 시각은 마이크로초 UTC이며 허용 범위는 없습니다.
  - [실측] 개발 PC 235개 프로세스에서 CIM `CreationDate`와 `Process.StartTime`이 이 단위로 모두 일치했습니다.
- 부모 PID를 가진 살아 있는 인스턴스가 트리 인스턴스와 같고, 자식이 그 뒤에 생성된 경우에만 편입합니다.
- 부모 인스턴스를 입증할 수 없으면 `UNVERIFIED_PARENT`로 기록만 하고, 모듈도 읽지 않습니다.
- 모듈 읽기 실패는 인스턴스별로 셉니다.
- 다음을 출력합니다.
  - `processes.tsv`: PID, 생성 시각, 확인된 부모 인스턴스
  - 원시 Snapshot 기록(`raw.tsv`)과, 이를 다시 판정하는 `-Mode Adopt`
  - `Collect` 후보의 각 파일에 `observed_in`

**A1-R01 검사 필드:**

- 작성자 이름·이메일
- Committer 이름·이메일
- 그 밖의 머리글
- 메시지 전체
- 결과 표기는 `NO_SECRET_PATTERN_FOUND (pattern-based; not proof that no secret exists)`입니다.

**A1-R02 게시 절차:**

- URL을 직접 지정하고 `main` Ref만 명시해 `--no-follow-tags`로 Push합니다.
- 승인 Tag는 별도의 명시 Ref로 보냅니다.
- 게시 전에 원격이 비어 있는지와 `--dry-run --porcelain` 전송 목록을 대조합니다.
- 게시 후 `ls-remote`가 승인 목록과 같은지 대조합니다.
- `push.followTags=true`에서의 안전성은 로컬 bare 저장소로 실측했습니다(3절 `a1.push_no_follow_tags`).

**B4-R01 입력 검증:**

- Build Root, 상위 경로 전체, 출력 폴더와 모든 하위 항목을 **핸들로 열어 유지한 채** 검사·해시합니다. 이 핸들은 삭제 공유가 없고 데이터 접근을 포함합니다.
- 다음 경우는 목록 전체를 거부합니다(종료 코드 1).
  - Reparse·최종 경로 불일치(루트 이탈)
  - 하드 링크(한 파일 두 이름)
  - 중복 상대 경로
  - 크기 ≠ 읽은 바이트
  - 출력 폴더 누락
  - 주 바이너리 누락·중복. 6개를 SimHarness 1, Verifier 1, Test Assembly 4로 구분합니다.
- EXE 행은 `exe_rows_interpretation_pending`으로 따로 표시합니다. `roles_note`에는 category·observed가 Provenance 필드가 아니라는 점을 밝혔습니다.

- **G5-R02 해석:** OWNER 지시 3항의 "R-ARCHIVE: apply"와 "apply Root는 직접 수정하지 않는다"는 Codex 원문(“`apply:false` 중첩 Root”)에 따라 **비적용(`apply: false`) Root**를 뜻하는 것으로 해석했습니다. 적용 Root는 레이아웃이 변경을 승인한 대상입니다. 다른 뜻이라면 OWNER 확인이 필요합니다.
- **변경 없음:** G5 Probe(`g5-probe.ps1`)는 PASS 판정 그대로이며 바이트 변경이 없습니다.
- **함께 정정한 것:**
  - 구현 중 핸들 결속의 함정을 실측했습니다. 메타데이터 권한만 가진 핸들은 공유 검사에 참여하지 않아 이름 변경을 막지 못합니다.
  - 그래서 모든 핸들에 데이터 접근을 넣었습니다. 반례 시험 `acl.r01_swap_after_check_blocked_by_held_handle`이 이 결함을 잡았습니다.

## 2. 수정 전 반례 실행 (FAIL 증거)

`2e58809`의 `g5-acl.ps1`·`a1-scan.ps1`·`b4-candidates.ps1`을 Scratch에 꺼내 같은 반례를 실행했습니다. 출력은 `E:\Madegame\g3x\g5fix\prefix_FAIL.txt`(`e4147697…`), 드라이버는 `prefix_demo.ps1`(`1d9ee343…`)입니다.

| Finding | 수정 전 결과 | 수정 후 결과(3절) |
|---|---|---|
| G5-R01 상위 Junction | 스냅샷이 쓰였고 Junction 대상의 ACL을 바꾼 뒤 사후 Verify가 잡아 `ROLLED_BACK`(사후 검증 의존) | `REFUSED`, 스냅샷 없음, 대상 트리 불변 |
| G5-R02 상속 허용 비적용 R-ARCHIVE | **`PASS`(종료 0)인데 R-ARCHIVE 트리가 바뀜** | `REFUSED`(G5-R02), 불변 |
| G5-R03 깊은 새 Root + 실패 | **`ROLLED_BACK`(1)인데 `newA`·`newB`가 남음** | 일지 3개, 모두 제거. 남으면 `ROLLBACK_FAILED`(3) |
| G5-R04 그룹 조회 실패 | **principals `PASS`** | principals `FAIL`("could not be verified") |
| A1-R01 작성자 이름·Committer 이메일·메시지 후반 | **`NO_SECRET_PATTERN`, SECRET 0건** | `SECRET_FOUND`, 세 필드 각각 탐지 |
| B4-R01 Junction 경유 Build Root, 주 DLL 없음 | **목록 작성(종료 0)** | `REFUSED`(종료 1) |
| G5-R05 | 정적 근거(1초 허용, 부모 부재 시 편입)이며 실행 재현은 하지 않았습니다. 이전 코드는 이 규칙만 분리해 실행할 수 없습니다. | 재현 기록으로 5개 경우 판정(3절) |

## 3. 시험

### 3.1 격리 자체 시험 `tools/b1-ops/selftest.ps1`

- **환경:** 개발 PC, 비승격. Scratch Root 하나에서 실행하고 시험 뒤 삭제했습니다.
- **결과: 68 PASS / 0 FAIL**
  - Worktree 사전 실행: `g5fix\selftest_worktree_306c402pre.json`(`ca7570d7…`)
  - Clean Checkout 실행: 4절
- 이전 36건은 유지했습니다. 다만 다음은 새 동작에 맞춰 바꿨습니다.
  - 이전 "Junction으로 사후 Verify 실패 → 자동 복구" 사례는 이제 사전 거부 사례(`acl.r01_reparse_under_apply_root_refused_before_change`)입니다.
  - 자동 복구는 주입 실패로 따로 시험합니다.

| Finding | 추가된 반례(모두 PASS) |
|---|---|
| G5-R01 | `r01_reparse_under_apply_root_refused_before_change`, `r01_upper_junction_refused`, `r01_junction_inside_diagnostic_root_to_outside_refused`, `r01_swap_after_check_blocked_by_held_handle`(검사 후 교체 시도가 유지 핸들로 차단), `r01_maintenance_condition_refuses_running_principal`, 정상: `apply_pass`·`apply_created_root_born_protected` |
| G5-R02 | `r02_inheriting_nonapplied_nested_root_refused`(변경 전후 SDDL 동일), `r02_protected_nonapplied_nested_root_unchanged`, `r02_missing_nonapplied_nested_root_refused` |
| G5-R03 | `r03_first_restore_item_fails_others_restored`(종료 3, 다른 Root 원복), `r03_middle_restore_item_fails_others_restored`, `r03_deep_new_root_and_intermediates_removed`(일지 3, 전부 제거), `r03_failed_apply_full_rollback`, `r03_created_paths_left_behind_are_residue`(종료 3, 기존 경로 유지), `r03_manual_restore_round_trip` |
| G5-R04 | `r04_admin_lookup_failure_fails_verify`, `r04_nested_group_limit_stated_separately`, `injection_requires_diagnostic_root` |
| G5-R05 | `r05_trace_records_instance_identity`, `r05_normal_parent_child_adopted`(같은 Snapshot에서 자식이 먼저 나와도), `r05_pid_reuse_not_adopted`, `r05_parent_exited_then_pid_reused_child_unverified`, `r05_fast_reuse_sub_second_is_another_instance`(1µs 차이), `r05_unobserved_parent_not_attributed` |
| A1-R01 | `a1r01_author_name_scanned`, `a1r01_committer_email_scanned`, `a1r01_message_after_separators_scanned`(0x1E·0x1F·빈 줄 뒤), `a1r01_author_email_privacy_reported`, `clean_repo_passes_without_claiming_absence` |
| A1-R02 | `a1.push_no_follow_tags`: `push.followTags=true`에서 명시 Push는 원격 Ref 1개, 플래그 없는 명령은 미승인 Tag까지 3줄 |
| B4-R01 | `r01_upper_junction_refused`, `r01_inner_junction_refused`, `r01_root_escape_refused`, `r01_missing_primary_refused`, `r01_duplicate_candidate_refused`(하드 링크), `r01_normal_output_after_cleanup`, `rule_v2_roles`(CANDIDATE_INCOMPLETE, EXE 4행 표시) |

- **고장 주입:** 결정적 재현을 위해 `g5-acl.ps1 -DiagnosticInject`(apply-fail, restore-fail, drop-file, swap-after-check, admin-lookup-fail, maintenance-check)를 썼습니다.
  - `-DiagnosticRoot` 없이는 사용 오류로 거부합니다(시험 `injection_requires_diagnostic_root`).
  - 결과에는 DIAGNOSTIC 표시가 붙습니다.
  - 실제 접근 거부로 복구 실패를 만들 수 없는 비승격 환경이기 때문입니다. 비승격 사용자는 자기 소유 객체의 WRITE_DAC를 항상 가집니다.
- **실제 빌드 트리 확인:** 수정된 `b4-candidates.ps1`를 `g4clean3`(`1207767` 빌드)에 실행했습니다. 441행, 비Pin 31, 종료 0으로 거짓 거부가 없었습니다(`g5fix\b4_realtree_check.json`, `f1240b13…`).

### 3.2 A-1 검사

- `306c402`, 63 Commit(공개 원격 밖 44), Commit 필드 315개 검사, SECRET 0건(`g5fix\a1scan_306c402\summary.json`, `03dd3030…`)
- 새로 보이는 PRIVACY 값은 Commit 작성자 이메일(이미 공개된 Commit에도 있음)과 시험용 `@example.invalid`입니다.

## 4. Clean Checkout 회귀 검증

- **Clone:** 기존 `E:\Madegame\g3x\g4clean3`를 재사용했습니다(새 Clone 없음).
  - verify 로그 복원 → 작업 브랜치에서 로컬 fetch → `checkout --detach 306c402` → 작업 트리 0건
  - `origin`은 실험 통제값 그대로입니다(조회·Push 없음).
- **제품 불변:** `git diff --stat a980ea5 306c402`의 결과가 0줄입니다. 대상 경로는 다음과 같습니다.
  - `src`, `game`, `tests`, `tools/StarPlayer.EvidenceVerifier`, `tools/StarPlayer.SimHarness`
  - `tools/verify.ps1`, `TASK.md`, `docs/contracts`, `content`
  - `2e58809 → 306c402`의 변경은 `tools/b1-ops`(새 `B1OpsNative.cs` 포함)와 `docs/tasks` 두 문서뿐입니다.
- **`tools\verify.ps1`:** exit 0, **`GATE: PASS`**

| 단위 | 결과 |
|---|---|
| U1 Domain | 327 / 0 / 0 |
| U2 Simulation | 741 / 0 / 0 |
| U3 Application(U3a 14, U3b 48 포함) | 128 / 0 / 0 |
| U4 Infrastructure | 1,237 / 0 / 0 |
| U5 SimHarness | 538 / 0 / 0 |
| U6 EvidenceVerifier | 90 / 0 / 0 |
| **합계** | **3,061 PASS / 0 FAIL / 0 SKIP** (기존과 같음) |
| Godot G1~G4, Export E1·E1b·E2~E4 | PASS |
| D1 문서 링크 | PASS, 246개 파일 |
| D3 승인 계약·보호 파일 | PASS, 5개 일치 |
| PCK SHA-256 | `3a187e19…` 불변 |

- **`tools\b1-ops\selftest.ps1`(Clean Checkout 사본):** 68 PASS / 0 FAIL, 비승격. Scratch Root는 삭제했습니다(`g5fix\selftest_clean_306c402.json`, `3e4e8aa4…`).
- **기록:**
  - verify 로그 사본 [B1_G5FIX_VERIFY_LOG_clean.md](B1_G5FIX_VERIFY_LOG_clean.md)(`167488fe…`)
  - stdout `g5fix\verify_306c402_stdout.txt`(`166aa994…`)
- **재실행하지 않은 것:** 제품·게임·검증기 코드 변경이 없어 아래는 이전 증거(`a980ea5` G4-FIX2 보고서)를 재사용합니다.
  - Golden 4/4
  - Dry Pilot 9회
  - PCK 5회 재현성 시험(**미검증** 유지)
- **B1 Tag·TASK.md:** `b1-code-accepted-2026-10-26` → `7bdde7dd091f9a66957bc740c77d470c7732b3ad` 불변. 루트 `TASK.md`는 D3로 확인했습니다.

## 5. 남은 한계와 OWNER 미확정 조건

- **경쟁 조건(G5-R01):**
  - 핸들 유지는 점검한 경로 구성요소의 교체를 막습니다.
  - 그러나 Apply 중 Root 안에 쓰기 권한이 있는 주체가 **새 항목을 만드는 것**은 막지 못합니다.
  - 유지보수 조건(주체 프로세스 없음)을 운영 전제로 강제하고, 계정 로그아웃·비활성화는 OWNER 절차로 둡니다.
  - 이 검사는 운영(비진단) 모드에서만 기본 실행되며, 비승격 개발 PC에서 운영 모드로는 **미검증**입니다.
- **자동 복구:** 트랜잭션이 아닙니다. 강제 종료 시 남은 스냅샷·일지로 `Restore`하거나 수동 복구합니다. 일지 기록 직전의 강제 종료로 마지막 생성 경로 하나가 일지에서 빠질 수 있습니다.
- **미검증(비승격 환경):**
  - 다른 계정으로의 소유자 복원
  - 승격 세션 전용 분기(SeRestore·SeTakeOwnership 활성화)
  - 운영 계정별 실제 반례
- **Pin 추적:** 표본 추출 진단입니다. 운영 Pin은 ETW 추적으로만 확정합니다. 기존 191개 후보는 공식 Pin이 아니며, 수정된 추적기로 다시 산출하지 않았습니다(이전 산출물의 원시 기록에는 생성 시각이 없음).
- **B-4:** 후보 441행은 `CANDIDATE_INCOMPLETE`입니다. EXE 해석(F-14·N-11), 외부 실행 Pin, TRUST_CONFIG는 미확정입니다.
- **OWNER 결정(변경 없음):**
  - 운영 기계·SID
  - R-ARCHIVE 계약 권한과 보호 DACL 값
  - R-GIT 체인
  - 실제 ACL·Anchor·Pin 설치
  - A-1 게시 결정 A1-1~A1-7
  - G5-R02 해석 확인(1절)

## 6. 하지 않은 것

- 운영 ACL 적용, 원격 생성·Push, Tag 생성·변경, Trust Anchor·공식 Pin 설치, 운영 G1~G9, 공식 승인값·Build Provenance 등록
- 기존 계약·OWNER 원문·Golden·Hash·RNG·Replay·루트 `TASK.md`·B1 Tag 변경, 기존 Commit 이력 재작성
- R-ARCHIVE 권한 결정
- 기존 검증 Clone 추가 생성. `g4clean3`를 재사용했습니다.
