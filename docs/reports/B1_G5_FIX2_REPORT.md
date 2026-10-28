# B1 G5·A-1 잔여 Finding 3건 표적 수정 보고서

- 작성: Claude Code, 2026-10-28. 근거: OWNER 지시 "G5/A-1 잔여 Finding 3건 표적 수정"(2026-10-28)
- **이번 Codex 판정:** FIX_REQUIRED. 기존 8건 중 6건은 RESOLVED였고, 남은 수정 대상은 3건입니다.
  - G5-FIX-01(MAJOR): 하드링크를 통한 ACL 변경 범위 이탈
  - G5-R05(MINOR): 프로세스 생성 시각 확인과 모듈 열거 사이의 PID 재사용 경쟁
  - A1-R02(MINOR): 정상 `HEAD`·Annotated Tag 때문에 생기는 게시 후 Ref 비교 오류
  - 이번 판정의 **원문은 저장소 밖 원문 폴더(`E:\Madegame\g3x\codex-originals-g4`)에 아직 없습니다.** 이 보고서는 OWNER 지시에 옮겨진 요약을 근거로 합니다. 원문 보존은 OWNER 조치 사항입니다.
- **기준:**
  - 이전 검수 HEAD `fb1e33838000a86534a18de3210e53635ddd3286`
  - 이전 수정 검증 소스 `306c402`, G4 수용 소스 `a980ea5`
- **개발·검증 기준 Commit:** **`4a3d61b7f4b30db5cc0c5061ad0adcbf90e340df`**(도구·`docs/tasks` 두 문서, 로컬 전용, Push 없음)
  - 이 보고서와 요청서, `CURRENT_STATE.md`·Dashboard 갱신은 그 뒤의 **후속 문서 Commit**입니다. 검증 대상이 아닙니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
- Codex 재검수는 `미실시`입니다. 요청서는 [B1_G5_FIX2_CODEX_REVIEW_REQUEST.md](../tasks/B1_G5_FIX2_CODEX_REVIEW_REQUEST.md)입니다.

## 1. Finding별 수정

| Finding | 수정 | 위치 |
|---|---|---|
| **G5-FIX-01** MAJOR | 아래 1.1 | `g5-acl.ps1` `Get-ObjectKey`·`Invoke-Preflight`·Apply·`Restore-Snapshot`, `B1OpsNative.cs` `SetSddlExact`·`FileSystem` |
| **G5-R05** MINOR | 아래 1.2 | `B1OpsNative.cs` `IProcessApi`·`ModuleReader`·`Win32ProcessApi`, `g5-pins.ps1` Trace |
| **A1-R02** MINOR | 아래 1.3 | 새 `a1-refcheck.ps1`, [A-1 문서](../tasks/B1_A1_PUBLICATION_PREP.md) 2.2절 6단계·7절 |

### 1.1 G5-FIX-01 — 하드링크 ACL 범위 보호

**원인:** 보안 설명자는 경로가 아니라 파일 객체에 붙습니다. 이전 Apply는 두 경로로 다른 위치의 객체를 바꿀 수 있었습니다.

- 하위 항목마다 `D:AI`(상속만)로 다시 썼습니다. 그 항목이 R-ARCHIVE 파일의 하드링크이면 R-ARCHIVE 원본이 바뀌었습니다.
- Root에 `SetSecurityInfo`를 썼습니다. 이 함수는 하위를 **이름으로 순회하며** 상속 ACE를 전파하므로, 검사하지 않은 객체에도 닿았습니다.

**수정:**

| 단계 | 내용 |
|---|---|
| 사전 점검 | Apply가 바꿀 **모든 객체**(기존 Apply Root와 그 아래 모든 항목)를 각자 핸들로 열어 Apply가 끝날 때까지 유지합니다. 삭제 공유가 없으므로 이름 변경·삭제·교체가 불가합니다. |
| | 그 핸들로 확인합니다: 볼륨 파일 시스템 NTFS, 링크 수 **정확히 1**, 파일 ID가 0이 아니고 이번 Apply의 다른 객체와 겹치지 않음. |
| | 읽을 수 없거나 NTFS가 아니면 링크 수·파일 ID를 신뢰하지 않고 거부합니다(Fail-Closed). 하나라도 어긋나면 `REFUSED`(종료 코드 1)이며 스냅샷·디렉터리 생성·ACL 변경이 없습니다. |
| 첫 변경 직전 | 유지한 핸들마다 링크 수 1과 같은 파일 ID를 다시 확인합니다. 다르면 `REFUSED`이고 스냅샷도 쓰지 않습니다. |
| 변경 | `SetKernelObjectSecurity`로 **그 핸들의 객체 하나에만** 씁니다. 전파·이름 순회가 없습니다. |
| | 하위 항목의 설명자는 소유자와 Root ACE의 상속 사본(디렉터리 `OICIID`, 파일 `ID`, 같은 순서)입니다. Windows가 계산하는 값과 같은지 시험했습니다(3절). |
| | 쓰기 직전마다 같은 핸들로 링크 수·파일 ID를 확인합니다. 사전 점검 뒤 새로 생긴 항목도 쓰기 전에 같은 방법으로 확인하며, 다른 경로와 링크된 항목은 쓰지 않고 실패시킵니다. |
| 마지막 재확인 | 모든 변경 뒤 유지한 모든 핸들을 다시 확인합니다. 그 사이 링크가 추가되었으면 실패이며, 그 객체를 **같은 핸들로** 원래 설명자로 되돌립니다. |
| 복구 | 스냅샷 형식을 `starplayer-g5-acl-snapshot-v3`로 바꿨습니다. 항목마다 객체 식별값(`object` = 볼륨 일련번호:파일 ID)을 담습니다. |
| | Apply 중 복구는 유지한 핸들로 씁니다. `-Mode Restore`는 새 핸들의 파일 ID가 스냅샷과 같을 때만 씁니다. 복구도 전파 없이 씁니다. |
| | 사전 점검 뒤 생겨 변경된 객체는 원래 설명자가 스냅샷에 없으므로 `residue`로 보고하고 `ROLLBACK_FAILED`(3)입니다. |

- **요구사항 대응:**
  1. 모든 변경 대상 파일을 변경 전에 검사합니다.
  2. 다중 링크 파일은 Apply 전에 거부합니다.
  3. 파일 ID 중복과 같은 객체의 여러 경로를 확인합니다.
  4. 링크 수·파일 ID를 신뢰할 수 없으면 Fail-Closed입니다.
  5. 사전 점검은 첫 변경보다 먼저 끝납니다. 이제 상속 전파 자체가 없습니다.
  6. TOCTOU는 아래 5절에 분석했습니다.
  7. 비적용 Root(R-ARCHIVE 등) 객체의 설명자는 쓰지 않습니다.
  8. 정상 단일 링크 파일의 Apply는 유지됩니다.
- **문구 해석(이전 보고서와 같음):** OWNER 지시 7항 "기존 R-ARCHIVE 등 apply Root의 파일 보안 설명자를 변경하지 않는다"의 "apply Root"는 비적용(`apply: false`) Root로 읽었습니다. Apply 대상 Root는 레이아웃이 변경을 승인한 Root이기 때문입니다.

### 1.2 G5-R05 — 동일 프로세스 핸들 결속

**원인:** 이전 판은 `[Diagnostics.Process]::GetProcessById(pid)` 뒤에 `StartTime`과 `Modules`를 읽었습니다. 이 둘은 각자 PID로 프로세스를 **다시 열었으므로**, 그 사이 PID가 재사용되면 다른 인스턴스의 모듈을 읽을 수 있었습니다.

**수정:** `ModuleReader.Read`는 `OpenProcess`를 **한 번** 호출하고, 그 핸들로만 다음을 처리합니다.

1. 생성 시각(`GetProcessTimes`)을 읽어 기록된 인스턴스(마이크로초 UTC)와 비교합니다.
2. 실행 중인지 확인합니다(`WaitForSingleObject(h, 0)`).
3. 모듈 목록을 읽습니다(`EnumProcessModulesEx` `LIST_MODULES_ALL`, `GetModuleFileNameExW`).
   - 목록이 늘어나면 처음부터 다시 읽습니다(최대 5회).
   - 이름 하나라도 읽지 못하면 일부 수집(`INCOMPLETE`)입니다.
4. 읽은 뒤에도 실행 중이고 생성 시각이 같아야 합니다.
5. 핸들은 `finally`에서 항상 닫습니다.

- 열린 핸들은 프로세스 객체를 붙잡으므로, 읽는 동안 그 PID는 재사용되지 않습니다. PID만으로 다시 여는 경로는 없습니다.
- **결과 상태:**
  - 모듈이 기록되는 것은 `OK`뿐입니다.
  - `OTHER_INSTANCE`·`NOT_RUNNING`·`EXITED_BEFORE_READ`·`EXITED_DURING_READ`(읽은 목록 폐기)에서는 모듈을 기록하지 않습니다.
  - `OPEN_FAILED`·`TIME_UNREADABLE`·`READ_FAILED`·`INCOMPLETE`는 `module_reads_failed`로 셉니다.
- **기록:** `processes.tsv`에 인스턴스별 `module_read_statuses` 열을 추가했습니다. 인스턴스 식별 근거는 PID와, 모듈을 읽은 바로 그 핸들에서 읽은 생성 시각입니다.
- **시험 대역:** Native 호출 경계를 `IProcessApi`로 분리했습니다. 자체 시험은 스크립트형 대역(`FakeProcessApi`)으로 경쟁을 결정적으로 재현합니다. 실제 PID 재사용을 기다리지 않습니다.
- **기존 191개 후보:** 운영 Pin으로 승인하지 않았습니다. 고친 추적기로 다시 만들지도 않았습니다(**미실시**).
- Process 객체의 수명 연장만으로 해결했다고 주장하지 않습니다. 실제 수정은 같은 핸들에서 확인과 열거를 함께 하는 것입니다.

### 1.3 A1-R02 — 원격 Ref 비교

**원인:** 이전 6단계는 `git ls-remote <URL>` 출력을 승인 목록과 그대로 비교했습니다. 새 저장소는 항상 `HEAD`를 광고하고, Annotated Tag는 Peeled 줄(`^{}`)을 추가합니다. 그래서 정상 게시도 실패로 판정되었습니다.

**수정:** 새 `a1-refcheck.ps1`을 6단계 절차로 넣었습니다.

- `git ls-remote --refs`만 읽습니다. 이 옵션은 `HEAD`·Peeled 줄을 제외한 실제 Ref만 나열합니다.
- 실제 `refs/heads/main`과 `refs/tags/*`를 `-Expect`의 정확한 객체 ID와 비교합니다.
  - Annotated Tag의 기대값은 Tag 객체 ID입니다. Peeled Commit ID를 쓰면 `MISMATCH`입니다.
  - `-Expect`에는 `refs/heads/main`이 정확히 한 번 있어야 합니다. Tag는 OWNER가 승인한 것만 넣습니다. Peeled 이름이나 다른 Branch는 사용 오류입니다.
- 기대 밖 Ref(다른 Branch, 미승인 Tag, 그 밖의 `refs/*`)는 `UNEXPECTED`, SHA 불일치는 `MISMATCH`, 누락은 `MISSING`이며 모두 FAIL입니다.
- 조회 실패와 해석할 수 없는 출력은 `ERROR`(종료 코드 2)이며 PASS로 취급하지 않습니다.
- Push 명령은 그대로입니다: 명시적 `<SHA>:refs/heads/main`, `--no-follow-tags`, 승인 Tag는 Tag마다 별도 명시 Ref.
- **한계:** 서버가 숨기는 Ref는 `ls-remote`에 나타나지 않으므로 이 대조로 확인할 수 없습니다.

## 2. 수정 전 반례 FAIL (수정 후 PASS와 분리 보존)

모두 저장소 밖 `E:\Madegame\g3x\g5fix2\`에 있습니다. 수정 전 도구는 `fb1e338`의 작업 트리 도구입니다.

| Finding | 수정 전 결과 | 기록 |
|---|---|---|
| G5-FIX-01 | 진단 Root에서 R-ARCHIVE(비적용, 보호, NS 읽기 ACE 추가) 파일의 하드링크를 R-BUILD에 두고 Apply했습니다. | `prefix_hardlink_demo.ps1`(`188649d4…`) |
| | 형제 Root·다른 디렉터리: **`RESULT=PASS`이면서 R-ARCHIVE 원본 파일의 DACL이 바뀌었습니다**(NS ACE가 사라지고 R-BUILD의 상속 ACE로 바뀜). | `prefix_hardlink_FAIL.txt`(`69d2e809…`) |
| | 중첩 Root: 기준값 비교가 탐지해 `ROLLBACK_FAILED`였지만, 복구가 `SetSecurityInfo`로 상위(임시 폴더)의 상속 ACE를 원본 파일에 써서 원본이 다른 값으로 남았습니다. | 같은 파일 |
| G5-R05 | 실제 PID 재사용은 재현하지 않았습니다. 같은 시험 대역에 **이전 호출 형태**(시각 확인용 열기·닫기 후 모듈용으로 다시 열기)를 적용한 모형이 재사용된 PID의 모듈(`C:\evil\evil.dll`)을 반환했습니다. | 자체 시험 `pins.r05_prefix_call_pattern_model_reads_other_instance`(모형임을 명시) |
| | 이전 코드의 정적 근거: `g5-pins.ps1`(`fb1e338`) 165~169행. `GetProcessById` 뒤 `StartTime`과 `Modules`를 따로 읽습니다. | Git 이력 |
| A1-R02 | 로컬 bare 저장소에서 이전 6단계(`--refs` 없는 출력 = 승인 목록)를 적용했습니다. 정상 `main` 게시와 승인 Annotated Tag 게시가 모두 **FAIL**(오판)이었습니다. `HEAD`와 `refs/tags/v-approved^{}` 줄 때문입니다. 같은 저장소에서 `--refs` 비교는 PASS였습니다. | `prefix_a1r02_demo.ps1`(`ec04caae…`), `prefix_a1r02_FAIL.txt`(`1a0bd84f…`) |

**수정 후:** 같은 하드링크 재현 스크립트를 수정 도구에 실행했습니다. 세 구성 모두 `REFUSED`(종료 코드 1)였고, 원본 SDDL 불변, 스냅샷 없음이었습니다(`postfix_hardlink_PASS.txt`, `74886f62…`).

**Native 동작 실측**(`exact_probe.ps1` `64817098…`, 출력 `exact_probe_out.txt` `6b294672…`):

- `SetKernelObjectSecurity`는 자식을 바꾸지 않았습니다.
- 계산한 상속 사본은 운영체제가 새 자식에 주는 SDDL과 같았습니다.
- `SE_DACL_AUTO_INHERITED`(`AI`)는 `SE_DACL_AUTO_INHERIT_REQ`를 함께 설정해야 보존되었습니다. 첫 실측에서 이 플래그가 빠지는 것을 보고 고쳤습니다.
- 원래 설명자로 정확히 되돌아갔습니다.
- `ModuleReader`는 실제 프로세스에서 `OK`, 다른 생성 시각에서 `OTHER_INSTANCE`, 종료 뒤 `EXITED_BEFORE_READ`였습니다.

## 3. 시험

`tools/b1-ops/selftest.ps1`: **100 PASS / 0 FAIL**(기존 68건 + 신규 32건), 비승격.

- 작업 트리: Commit 전 동일 도구(`selftest_worktree_pre4a3d61b.json`, `269c9f9f…`)
- Clean Checkout `4a3d61b` 사본: 4절(`selftest_clean_4a3d61b.json`, `3744f219…`). `verify.ps1`과 동시에 실행했습니다.

**G5-FIX-01(11건, `acl.fix01_*`):** 거부 사례는 모두 다음을 함께 확인합니다.

- 종료 코드 1, `REFUSED`, 원인 문구 일치
- 스냅샷 없음, 새 디렉터리(`R-WORK`) 생성 없음
- R-BUILD·R-ARCHIVE·레이아웃 밖 디렉터리의 트리 전체 SDDL 불변(원본과 링크 모두)

| 사례 | 결과 |
|---|---|
| `archive_file_linked_into_build_refused` | R-ARCHIVE 원본 + R-BUILD 안 하드링크 → 거부 |
| `link_in_other_directory_refused` | R-BUILD 파일의 링크가 레이아웃 밖 디렉터리에 있음 → 거부 |
| `two_links_in_same_root_refused` | 같은 Root 안 두 링크 → 거부 |
| `same_file_id_cross_check_refused` | 링크 수 검사를 진단 주입으로 끄면 파일 ID 중복 교차 검사가 거부 |
| `link_count_unreadable_refused` | 링크 수 조회 실패(주입) → 거부 |
| `link_added_after_check_refused_before_change` | 사전 점검 뒤 R-ARCHIVE에 R-BUILD 파일의 링크 추가 → 첫 변경 직전 재확인에서 거부. 스냅샷·변경 없음 |
| `link_into_apply_root_after_check_never_written` | 사전 점검 뒤 R-BUILD 안에 R-ARCHIVE 파일의 링크 추가 → 쓰기 전 확인에서 실패, `ROLLED_BACK`. 원본은 한 번도 쓰이지 않아 불변 |
| `link_added_after_change_rolled_back_through_handle` | 모든 변경 뒤 링크 추가 → 마지막 재확인에서 실패, 같은 핸들로 복구. 두 경로 모두 원래 SDDL, `ROLLED_BACK` |
| `single_link_apply_pass` | 정상 단일 링크 → `PASS`, R-ARCHIVE 불변 |
| `exact_descriptors_equal_windows_inheritance` | Apply 뒤 운영체제가 만든 새 파일·디렉터리 SDDL = Apply가 쓴 SDDL |
| `restore_refuses_a_replaced_object` | Restore 왕복 PASS 뒤 파일을 다른 객체로 바꾸면 Restore가 쓰지 않고 `residue`(종료 코드 3) |

**G5-R05(9건, `pins.r05_*` 신규):**

| 사례 | 결과 |
|---|---|
| `normal_read_one_handle` | `OK`, 열기 1회·닫기 1회, 생성 시각은 핸들에서 읽음 |
| `pid_reused_after_time_check_never_reads_other_instance` | 실행 확인 직후 인스턴스 종료·PID 재사용 → `READ_FAILED`, 다른 인스턴스 모듈 0, 열기 1회 |
| `prefix_call_pattern_model_reads_other_instance` | (수정 전 모형) 열기 2회, 재사용 PID의 모듈을 읽음 |
| `exit_before_read_no_modules` | 열거 직전 종료 → `EXITED_BEFORE_READ` |
| `exit_during_read_list_discarded` | 열거 도중 종료 → `EXITED_DURING_READ`, 목록 폐기 |
| `open_failure_no_handle_no_modules` | 접근 거부 → `OPEN_FAILED`(닫을 핸들 없음). PID 없음 → `NOT_RUNNING` |
| `partial_or_failed_list_not_used` | 일부 수집 → `INCOMPLETE`, 열거 실패 → `READ_FAILED`, 모듈 0 |
| `other_instance_no_modules` | 다른 생성 시각 → `OTHER_INSTANCE` |
| `real_child_adopted_and_read_through_handle` | 실제 `cmd.exe` → `powershell.exe` 자식이 `ADOPTED`로 편입되고 `OK` 읽기로 모듈 기록 |

- 부모 인스턴스 미확인(`UNVERIFIED_PARENT`)은 기존 `Adopt` 재판정 사례 5건이 유지합니다. Trace는 편입된 인스턴스의 모듈만 읽습니다.

**A1-R02(12건, `a1.r02_*`):** 사례마다 새 로컬 bare 저장소를 씁니다.

| 사례 | 결과 |
|---|---|
| 정상 main + `HEAD` | PASS. `--refs` 없는 출력에는 `HEAD` 줄이 있음을 함께 확인 |
| 승인 Annotated Tag | PASS. `--refs` 없는 출력에는 `^{}` 줄이 있음을 함께 확인 |
| Annotated Tag 기대값을 Peeled Commit으로 지정 | FAIL `MISMATCH` |
| 승인 Lightweight Tag | PASS |
| 미승인 Tag 존재 | FAIL `UNEXPECTED` |
| `push.followTags=true`, `--no-follow-tags` 없음 | FAIL `UNEXPECTED refs/tags/…` |
| `push.followTags=true`, `--no-follow-tags` 있음 | PASS |
| main SHA 불일치 | FAIL `MISMATCH` |
| 기대 밖 Branch | FAIL `UNEXPECTED refs/heads/feature` |
| 기대 Tag 누락 | FAIL `MISSING` |
| 조회 오류(없는 원격) | `ERROR`, 종료 코드 2 |
| `main` 없는 기대값, Peeled 이름 | 사용 오류, 종료 코드 2 |

기존 `a1.push_no_follow_tags` 사례도 유지됩니다.

## 4. Clean Checkout 회귀

- **Clone:** 기존 `E:\Madegame\g3x\g4clean3`를 재사용했습니다(새 검증 폴더 없음).
  - 이전 verify 로그 변경을 되돌렸습니다. 그 내용은 `B1_G5FIX_VERIFY_LOG_clean.md`(`167488fe…`)와 같음을 확인했습니다.
  - 작업 브랜치에서 로컬 fetch → `checkout --detach 4a3d61b` → 작업 트리 0건
  - 이전 실행의 빌드 산출물(`bin`·`obj`·`build`·`game/.godot`, 증거 아님)은 `git clean -fdx`로 지워, 소스에서 다시 빌드했습니다.
  - `origin`은 실험 통제값 그대로입니다(조회·Push 없음).
- **`tools\verify.ps1`:** exit 0, **`GATE: PASS`**

| 단위 | 결과 |
|---|---|
| T1~T3 SDK·Godot·Export Template | PASS |
| B1~B3 Release 빌드(13개 프로젝트, 경고 0) | PASS |
| U1 Domain | 327 / 0 / 0 |
| U2 Simulation | 741 / 0 / 0 |
| U3 Application(U3a 14, U3b 48 포함) | 128 / 0 / 0 |
| U4 Infrastructure | 1,237 / 0 / 0 |
| U5 SimHarness | 538 / 0 / 0 |
| U6 EvidenceVerifier | 90 / 0 / 0 |
| **합계** | **3,061 PASS / 0 FAIL / 0 SKIP** (기존과 같음) |
| Godot G1~G4, Export E1·E1b·E2~E4 | PASS |
| D1 문서 링크 | PASS, 249개 파일(`fb1e338`의 문서 3개 추가분) |
| D3 승인 계약·보호 파일 | PASS, 5개 일치 |
| PCK SHA-256 | `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799` — 이전 기록 `3a187e19…`과 같음 |

- **기록:**
  - verify 로그 사본 [B1_G5FIX2_VERIFY_LOG_clean.md](B1_G5FIX2_VERIFY_LOG_clean.md)(`00cfcbf6…`)
  - stdout `g5fix2\verify_4a3d61b_stdout.txt`(`ca571771…`)
- **`tools\b1-ops\selftest.ps1`(Clean Checkout 사본):** 100 PASS / 0 FAIL, 비승격. `verify.ps1`과 동시에 실행했습니다. Scratch Root는 삭제했습니다.
- **A-1 Secret·Privacy 검사:**
  - `4a3d61b`: 65개 Commit, 공개 Ref 밖 46개, Commit 필드 325개 → **SECRET 0건**, `NO_SECRET_PATTERN_FOUND (pattern-based; not proof that no secret exists)`. 출력 `g5fix2\a1scan_4a3d61b`
  - PRIVACY는 보고용입니다(email 8, account_sid 22, user_profile_path 119, project_local_path 188). 게시 여부는 OWNER 결정 A1-x 대상입니다.
  - 후속 문서 Commit은 Commit 뒤 같은 검사를 실행합니다. 결과는 이 보고서에 담을 수 없으므로 저장소 밖 `g5fix2\a1scan_<문서 Commit>`과 OWNER 전달 요약에 남깁니다.
- **재실행하지 않은 것:** 제품·게임·검증기 코드 변경이 없어 아래는 이전 증거(`a980ea5` G4-FIX2 보고서)를 **재사용**합니다.
  - Golden 4/4
  - Dry Pilot 9회
  - PCK 5회 재현성 시험(**미검증** 유지). 이번에는 단일 Export의 SHA만 확인했습니다.
- **G4 수용 코드·계약 불변:** 7절.

## 5. Codex 검수 전 자체 점검 (공격자 관점)

| 질문 | 답 |
|---|---|
| Apply 이전 검사와 실제 변경 대상이 같은 파일 객체인가? | **같습니다.** 사전 점검에서 연 핸들(삭제 공유 없음)을 끝까지 유지하고, 모든 쓰기를 그 핸들로 합니다. 이름 변경·삭제·교체가 불가하므로 경로가 다른 객체를 가리킬 수 없습니다. 쓰기 직전마다 같은 핸들로 파일 ID·링크 수를 다시 확인합니다. 사전 점검 뒤 새로 생긴 항목은 열 때 확인하고, 그 핸들로만 씁니다. |
| 하드링크를 통해 비적용 Root가 간접 변경될 수 있는가? | **수정 범위에서는 막았습니다.** 쓰는 객체는 쓰는 순간 같은 핸들에서 링크 수 1이 확인된 객체뿐이고, 쓰기는 전파되지 않습니다. 기존 비적용 Root 파일이 Apply Root에 링크되어 있으면 링크 수가 2 이상이므로 쓰지 않습니다. |
| | 남는 구간: 확인과 쓰기 사이(같은 핸들, 극히 짧음)에 누군가 그 Apply Root 객체의 링크를 다른 곳에 만들 수 있습니다. 이때 바뀌는 것은 원래 Apply Root 객체이고, 마지막 재확인이 탐지해 같은 핸들로 되돌립니다(`link_added_after_change_*`). |
| | 마지막 재확인 전에 그 링크를 다시 지우면 탐지되지 않습니다. 그러나 그때 다른 경로에 남는 것은 없습니다. 링크 생성에는 대상 디렉터리 쓰기 권한이 필요합니다. 이는 유지보수 조건으로 막는 운영 전제입니다(사전 점검만으로 완전히 제거했다고 주장하지 않음). |
| 프로세스 생성 시각 확인과 모듈 수집 사이에 PID만 쓰는 단계가 남았는가? | **없습니다.** `ModuleReader`는 PID로 한 번 열고, 그 뒤 모든 호출이 같은 핸들을 씁니다. |
| | PID·부모 PID·생성 시각만 쓰는 단계는 소속 판정용 CIM Snapshot뿐입니다. 이 단계는 모듈을 수집하지 않습니다. 모듈 수집 전 핸들의 생성 시각으로 그 인스턴스를 다시 확인합니다. |
| | Root 인스턴스의 생성 시각은 `Start-Process`가 만든 프로세스 핸들(`Process` 객체가 유지)에서 읽습니다. |
| 정상 최초 Git Push가 Ref 비교에서 거부되는가? | **아닙니다.** `HEAD`와 Peeled 줄은 `--refs`가 제외합니다. main만, main + 승인 Annotated/Lightweight Tag 모두 PASS였습니다(3절). |
| 승인되지 않은 실제 Ref가 통과하는가? | **아닙니다.** `-Expect`에 없는 `refs/*`는 모두 `UNEXPECTED`입니다. `-Expect`는 main과 Tag만 받으므로 다른 Branch를 승인 목록에 넣을 수도 없습니다. 조회 실패는 `ERROR`입니다. |

**점검 중 추가로 확인·수정한 것(이번 범위 안):**

- `SetKernelObjectSecurity`가 `AI` 플래그를 버리는 문제를 실측으로 찾아 `SE_DACL_AUTO_INHERIT_REQ`로 고쳤습니다(2절).
- `Restore` 모드는 경로만 믿지 않고 스냅샷의 파일 ID와 같은 객체에만 씁니다(v3).
- 복구도 전파 없는 쓰기로 바꿨습니다. 이전에는 수정 전 반례에서 본 것처럼 복구가 상위의 상속 ACE를 원본에 쓸 수 있었습니다.

**관찰(수정하지 않음, 판단 요청):**

- 대소문자 구분이 켜진 디렉터리(`fsutil setCaseSensitiveInfo`)에서 이름이 대소문자만 다른 두 항목은 사전 점검에서 "같은 파일 ID"로 거부됩니다. Fail-Closed이지만 원인 문구가 정확하지 않습니다.
- 생산자가 Apply 이후 언제든 R-BUILD 파일의 링크를 R-ARCHIVE에 만들 수 있는지는 NTFS 링크 생성 권한(원본 파일 속성 쓰기 권한 필요 여부)에 달려 있습니다. 다른 계정이 필요해 **미검증**입니다. Apply의 범위 문제가 아니라 R-ARCHIVE 무결성 문제이며, R-ARCHIVE 권한은 OWNER 미확정 값입니다.

## 6. 남은 한계와 운영 G5 실측 조건

- **G5-FIX-01:** Apply 중 쓰기 권한 주체의 새 항목·링크 생성은 탐지·복구할 뿐 예방하지 않습니다. 유지보수 조건(주체 프로세스 없음, 역할 계정 로그아웃·비활성화)이 운영 전제입니다. 운영 모드의 프로세스 검사는 비승격 개발 PC에서 **미검증**입니다.
- 자동 복구는 보상 절차이며 트랜잭션이 아닙니다. 다른 계정 소유자로의 복구와 승격 전용 경로는 **미검증**입니다.
- NTFS 외 파일 시스템은 거부합니다. 이 분기는 시험할 볼륨이 없어 코드 검토로만 확인했습니다(**미검증**).
- **G5-R05:** 표본 추출 한계(짧은 프로세스·모듈 누락)는 그대로입니다. 운영 Pin은 ETW 추적 계획(G5 설계 8절)을 따릅니다. 기존 191개 후보는 승인되지 않았고 재추적하지 않았습니다.
- **A1-R02:** 숨겨진 Ref는 확인할 수 없습니다. 실제 GitHub 원격 생성·Push는 하지 않았습니다.
- OWNER 미확정 값은 그대로입니다: 운영 기계·SID, R-ARCHIVE 권한, R-GIT 체인, ETW·Pin, 실제 ACL·Anchor, A-1 결정 A1-1~A1-7.

## 7. 불변 확인

- `git diff --stat a980ea5 4a3d61b -- src game tests tools/StarPlayer.EvidenceVerifier tools/StarPlayer.SimHarness tools/verify.ps1 TASK.md docs/contracts content` → **0줄**입니다.
- `fb1e338 → 4a3d61b`의 변경 파일은 다음 7개뿐입니다.
  - `tools/b1-ops/`: `B1OpsNative.cs`, `g5-acl.ps1`, `g5-pins.ps1`, `selftest.ps1`, 새 `a1-refcheck.ps1`
  - `docs/tasks/`: `B1_A1_PUBLICATION_PREP.md`, `B1_G5_OPERATIONAL_DESIGN_DRAFT.md`
- B1 Tag `b1-code-accepted-2026-10-26` → `7bdde7dd091f9a66957bc740c77d470c7732b3ad` 불변입니다.
- 승인 계약, OWNER 원문, D3 보호 파일, 루트 `TASK.md`, Golden, Hash, RNG, Replay는 바꾸지 않았습니다.
- 운영 ACL 적용, Trust Anchor·Pin 등록, 원격 생성·Push, Tag 생성·변경, 운영 G1~G9, Build Provenance 등록, Git 이력 재작성은 하지 않았습니다.
