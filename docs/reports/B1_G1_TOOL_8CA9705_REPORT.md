# B1 G1-a 감사 도구 재결속 보고서 — `g1-names-check-8ca9705.ps1`

- 작성: Claude Code, 2026-10-30. 근거: OWNER 지시 "G1-a 감사 도구 재결속 지시"(2026-10-30)
- 입력 판정: C-3 Codex 검수 **REVIEW_READY** 수용(G5-TEMP-01 RESOLVED_IN_CODE, G5-MODEL-01 RESOLVED, 신규 MAJOR/MINOR 0/0)
- **성격:** 별도 감사 도구 버전 생성과 격리 시험입니다. Push·Tag·계약·`TASK.md`·Golden·RNG·Hash·Replay 변경, 운영 ACL·계정·Pin·Trust Anchor·Registry 적용, 공식 Gate PASS·Freeze 선언은 **없습니다**. 기존 승인 도구·C-3 보고서·Codex 판정 문서·기존 증거는 바꾸지 않았습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 식별 (도구와 제품 소스는 별개)

| 구분 | 값 |
|---|---|
| **제품 소스 후보(검증 대상)** | Commit **`8ca9705539bc941919f5290de149c4678f5147dd`**, Tree **`fd87de669127228dd15cbe9cbc85272326a65ff7`** |
| **새 감사 도구** | `tools/b1-ops/g1-names-check-8ca9705.ps1`, SHA-256 **`4d086a5cf2613f22d532ade52c8df42df59700c88b8cd5a3306098c7ee43f96b`** |
| 새 도구를 담은 Commit | `98724888f4ca7f6a59bfb2f29297e037c1aae7d6`(Branch `phase3/g5-temp01-c3`, 부모 `c9e378b`). 제품 소스 후보가 아닙니다 |
| 기존 승인 도구(보존, 변경 없음) | `tools/b1-ops/g1-names-check.ps1`, SHA-256 `d3945b5ed51f0c49e39b2689bd73a8b14240ff9d13564745aca12f22eeb56ba0`(Commit `c4a04bb`, 고정값 `c77ed1a`). 같은 바이트가 `98724888`에도 그대로 있음 |

- 문서 HEAD `c9e378b`와 도구 Commit `98724888`은 검증 대상이 아닙니다. 검증은 `8ca9705`의 Clean Checkout으로만 했습니다.

## 2. 새 도구의 생성 방법

- 생성 스크립트 `E:\Madegame\g3x\phase3ops\g1v2\make_v2.py`가 승인 도구 바이트(SHA `d3945b5e` 확인 후)에서 아래 값만 바꿨습니다. 각 치환은 정확히 1회 일치해야 하며 아니면 중단합니다.

| 항목 | 이전(`c77ed1a`) | 새(`8ca9705`) |
|---|---|---|
| 지배 줄 `DominanceOfficial.cs`(색인 읽기 줄) | `eb32a86c…ffd0db` | `4a408464…7284b` |
| 지배 줄 `FamilyOfficial.cs`(색인 읽기 줄) | `6f5c308b…f70948` | `329b017a…5c1bb7` |
| 파일 `Verifier.cs` | `83a82130…44088e` | `eb6ca5d6…173ec8e` |
| 파일 `DominanceOfficial.cs` | `5421d563…bd2421f` | `36e22202…331f981d8` |
| 파일 `FamilyOfficial.cs` | `e41ac7cb…6ef56e9f` | `4dcddd1d…cc651a7a9` |
| 주석 3곳의 기준 Commit 표기, 설명 4줄 추가 | `c77ed1a` | `8ca9705`(+ 버전·출처·"다른 Commit 승인 아님" 문구) |

- **그대로인 것(보호 수준 유지):** 이름 9종과 정규식·개수, 정의 13줄, 지배 토큰 정규식, 지배 줄 50개 중 48개와 6개 파일 SHA, 검사 논리, `-SelfTest` 17개 사례, 출력 형식, 종료 코드. 승인 도구와의 차이는 `git diff --no-index` 기준 +12/−8줄이며 모두 위 표의 값과 주석입니다.

## 3. 기준값의 독립 재산출

- 도구와 별개인 Python 구현(`govlines.py`, `emit.py`, 이전 G1 작업과 같은 스크립트)으로 `8ca9705` Clean Clone 2개(CRLF·LF)에서 지배 줄과 파일 SHA를 다시 산출했습니다.
  - 두 Clone의 결과가 바이트 동일(`CRLF_LF_SAME`)
  - 지배 줄 50개, 8개 파일, 파일별 개수 = `c77ed1a`와 같음. 다른 줄은 두 Replay의 색인 읽기 줄 2개뿐
  - 8개 파일 SHA(CRLF→LF) = `git show 8ca9705:<파일>`의 SHA-256(8/8)
- 이 값으로 만든 인벤토리(`logs\inventory_8ca9705.txt`)와 새 도구의 고정값이 같고, PowerShell 구현이 같은 값으로 PASS해 두 구현이 일치합니다.

## 4. 대상 확인 (도구 외부)

도구가 출력하는 HEAD에 의존하지 않고 Git으로 직접 확인했습니다(`logs\clones_external_check.txt`, `clones_after.txt`).

| Clone | 설정 | HEAD | Tree | 실행 전·후 상태 | 추적 `.cs` | 파일 집합 |
|---|---|---|---|---|---|---|
| `clones\p-crlf` | `core.autocrlf=true` | `8ca9705…` | `fd87de66…` | 0건 · 0건(`--ignored` 포함) | 164 | 추적 목록 SHA = 파일 시스템 목록 SHA(`6e2a4fbe…`) |
| `clones\p-lf` | `core.autocrlf=false` | 같음 | 같음 | 0건 · 0건 | 164 | 같음 |

- 두 Clone의 `git ls-tree -r HEAD` SHA 동일(`8dc726e1…`). 대상에는 새 도구 파일이 없습니다(도구와 대상 출처 분리). Clone은 로컬 Worktree에서 만들었습니다(`8ca9705`는 아직 공식 원격에 없음).

## 5. 실행 결과 (PowerShell 5.1.26100.9711 · 7.4.22)

| 실행 | 5.1 | 7.4.22 |
|---|---|---|
| 새 도구, `8ca9705` CRLF | **PASS**(불일치 0), 종료 0 | **PASS**, 종료 0 |
| 새 도구, `8ca9705` LF | **PASS**, 종료 0 | **PASS**, 종료 0 |
| 새 도구 `-SelfTest`(CRLF·LF) | **17/17**(`wrong=0`), 종료 0 | **17/17**, 종료 0(로그 바이트가 5.1과 같음) |
| 승인 도구(`d3945b5e`), `8ca9705` | FAIL 불일치 7(지배 줄 2쌍, 파일 3), 종료 1 | 같음 |
| 새 도구, `c77ed1a` Clone | FAIL 불일치 7(대칭), 종료 1 | 같음 |

- 마지막 두 줄은 두 도구가 각자 자기 기준 Commit에만 결속되어 있음을 보여 줍니다. 새 도구는 `c77ed1a`를 받아들이지 않습니다.

**반례(RED/GREEN 드라이버 `redgreen.ps1`, 이전 G1 작업과 같은 바이트 `da9bb636…`, 각 사례 별도 프로세스, 대상 `8ca9705` 사본):**

| 구분 | 사례 | 새 도구 |
|---|---|---|
| 정상 | baseline_copy | PASS |
| 기존 6 | verifier_renames_manifest, producer_renames_manifest, verifier_hardcodes_run_prefix, preflight_renamed, result_root_moved, second_dominance_index_literal | 모두 FAIL(검출) |
| 신규 7 | runpath_wrong_segment, unit_preflight_extension_txt, verifier_manifest_subdir_same_count, producer_archive_manifest_moved_same_count, unit_preflight_dir_swapped_same_count, new_file_composes_run_path, ungoverned_dataflow_in_governed_file | 모두 FAIL(검출) |
| 오탐 대조 | line_endings_flipped, unrelated_file_edit | 모두 PASS |
| **알려진 LIMIT** | assembled_name_in_new_file(새 파일에서 `"ru" + "n/"`·`"/manifest" + ".json"` 조립) | **PASS = 탐지 못함.** 탐지 성공이 아니며, 이 한계는 종단 시험·코드 검수가 다룹니다 |

- 이 드라이버에서 승인 도구는 `8ca9705` 사본에서 모든 사례가 FAIL입니다(고정값이 `c77ed1a`라 정상 사본도 FAIL). 정보용이며 판정은 새 도구 열로만 합니다.
- 5.1과 7.4.22의 드라이버 출력 표는 바이트가 같습니다.

## 6. 유지되는 한계 (변경 없음)

- 고정 8개 파일 **밖**의 새 파일에서 이름을 조각으로 조립·복제하는 코드는 잡지 못합니다(LIMIT 사례).
- 고정 파일 밖에서 이름·기호 없는 식별자로 들어오는 값의 흐름은 추적하지 않습니다.
- 시험 코드의 이름은 개수만 보고합니다(정보용).
- **도구 PASS만으로 Commit을 승인하지 않습니다.** 고정 8개 파일과 지배 줄이 같은 다른 Commit도 PASS할 수 있으므로, 공식 감사는 대상 Commit·Tree를 도구 밖에서 먼저 확인합니다(이번에도 그렇게 했습니다).

## 7. 증거 (저장소 밖 `E:\Madegame\g3x\phase3ops\g1v2\`, 전체 목록 `SHA256SUMS_g1v2.txt` 99개 파일)

| 파일 | SHA-256 |
|---|---|
| `tool_new\g1-names-check.ps1`(= `98724888:tools/b1-ops/g1-names-check-8ca9705.ps1`) | `4d086a5cf2613f22d532ade52c8df42df59700c88b8cd5a3306098c7ee43f96b` |
| `tool_old\g1-names-check.ps1`(= 승인 도구) | `d3945b5ed51f0c49e39b2689bd73a8b14240ff9d13564745aca12f22eeb56ba0` |
| `make_v2.py` · `govlines.py` · `emit.py` | `10afe0003e88ff64b058a4405e9114a98bd46d04832dc228cb082f77deaf0d53` · `f976b45a09f21fb243e3eddbce973fe2249ae22ceb980500e45d03ca762ae603` · `1862aa42c97461f4d7d3d8de3df2fb413f6f7059cdc612cee89b3e55c2ce3db8` |
| `redgreen.ps1` | `da9bb636dacc97825a93108a64302fd4d863474c6c4193f6e9a6cf5145fd5646` |
| `logs\clones_external_check.txt` · `inventory_8ca9705.txt` · `shells.txt` | `2972eef76bda98f33972d8c5d4fc4da7193399520243b5880b9143756b90b03d` · `f886726a7c843a990418b4d3856d704260bf6c180ea9f17efdd02979bdc78b5e` · `b562406d2e97d994ac2764fb58c3162819c2d3eeecad1fd7b3fb34d517981597` |
| `logs\ps51\new_check_8ca9705-crlf.log` · `ps7\…` | `1aee4f2c3f343acdcc3767b107addbec0603e40c518da71caacd9a6c149da7d6` · `b390620b5ec98933775b5500b2c167a981596b854e4bfea31c67fa3be3c028a6` |
| `logs\ps51\new_selftest_8ca9705-crlf.log` = `ps7\…` | `4557d797cfa207b88c436f848fe0817de080262a539e7a16224f813a557eab33` |
| `logs\ps51\redgreen_8ca9705-crlf.log` = `ps7\…` | `bce9267f440bc9a4d0b59646740b2f342e59f6bd2ecf5a6ed5e2e6eff3071320` |
| `logs\ps51\commands.tsv` · `ps7\commands.tsv`(명령·종료 코드·시각) | `efbd70f42ccbb859fda06736c12f9c2fe4cee29449a4c09614b015890e4af39c` · `33393eee1c1171a3055da3f0b6cb78140b07ab2408a058f9385cec3bf35d4c21` |

- 실행 호스트: `powershell.exe` 5.1(`60c9b298…410f`), `pwsh.exe` 7.4.22(`d83e66c6…c803`). 개발 PC 진단이며 운영 기계 공식 G1 증거가 아닙니다(공식 감사용 PowerShell은 승인 B-22).

## 8. 남은 사항

1. Codex READ-ONLY 표적 재검수: [요청서](../tasks/B1_G1_TOOL_8CA9705_CODEX_REVIEW_REQUEST.md)
2. OWNER: 새 도구 채택, 공식 소스 후보 `8ca9705` 확정(A1-6), 게시 승인
3. 공식 G1: 운영 기계에서 공식 원격 Clone(E-1)의 Commit·Tree 외부 확인 → 새 도구(SHA `4d086a5c…`) 실행, 종단 시험, 결과 Codex 검수 → OWNER 수용
