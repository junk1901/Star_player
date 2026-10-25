# B1 Git 기준본 고정 — 결과 보고

- **3차(최종, 10절) 결론:**
  - 네 번째 로컬 Commit `7bdde7dd091f9a66957bc740c77d470c7732b3ad`에서 Red 원시 로그 3개를 보존했습니다.
  - 깨끗한 체크아웃 검증 결과는 **`GATE: PASS`**이고, D1 전체 오류는 **0개**입니다. 12개 조건을 모두 충족했습니다.
  - Annotated Tag **`b1-code-accepted-2026-10-26`**를 만들었습니다. 대상 Commit은 검증한 Commit과 같습니다.
  - 이 Tag는 B1 코드 구현 기준본의 수용만 뜻합니다. 운영 Build Freeze, Official Execution 허가, Phase 3 완료는 아닙니다.
- **2차(OWNER 후속 결정 처리, 6~9절) 결론(당시 기록):**
  - 세 번째 로컬 Commit `ff368a9dc7a12cccebd0d8732d19579aa9d6fad8`을 만들었습니다.
  - 깨끗한 체크아웃에서 U6은 **70/70 PASS**(11분 39초)이고, Codex 원문 링크 122개(1차 보고의 116개는 잘못 센 값, 8절)의 D1 실패는 해소됐습니다.
  - 그러나 D1이 **다른 원인으로 FAIL**입니다. B1 보고서 3개가 링크한 Red 원시 로그 3개(`*.log`)가 `.gitignore`의 `*.log` 규칙 때문에 커밋되지 않았습니다. 그래서 `GATE: FAIL`이고, 결정 5에 따라 **Tag를 만들지 않았습니다**.
  - 1차 실행에서는 D1 상세가 처음 8개만 표시되어(전부 Codex 링크) 이 6개 링크가 가려져 있었습니다.
- 1~5절은 1차 기록이며 그대로 둡니다.

- 작성: Claude Code, 2026-10-26. 근거: OWNER 결정 [O16](../contracts/b1/originals/owner/O16_20261025T151044Z_code-acceptance-and-git-baseline-decision.txt) 5항(조건부 Commit·Tag 허가)
- **1차 결론: 로컬 Commit 2개를 만들었습니다. 깨끗한 체크아웃의 `tools/verify.ps1`이 `GATE: FAIL`(D1·U6)이라 O16 5항의 "모든 조건 충족 후 Tag" 조건을 충족하지 못해 annotated Tag는 만들지 않았습니다. 중단 후 보고합니다.**
- Remote Push 없음. Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
- 이 보고서는 Commit 이후에 작성했으며 **커밋되지 않았습니다**(작업 트리 변경).

## 1. Commit

| Commit | 내용 |
|---|---|
| `b2635ab201003ccaa42d86866646946f216773af` | G2 개행 정책(`.gitattributes` 규칙 추가) + `content/tuning/virtual_tuning_v4.json`을 검수 바이트(CRLF)로 저장. 개행 변경만이며, `--ignore-cr-at-eol` 기준 내용 차이 0 |
| `c15360c814f45629fd659abb7d7b34663b629966` | B1 코드 기준본: 구현 목록 210개 + 목록 파일 2개 = 211경로(추가 197, 수정 14) |

- 부모: `257403ddae557a54c4013c17705370caeac7a2e0`

**Stage 전 조건(O16 5항) 점검:**

| 조건 | 결과 |
|---|---|
| 1 G2 적용 | 완료. `git status` 변화 없음, Index blob 변화는 내용 수정 파일과 튜닝 JSON뿐 |
| 2 목록 전체 검토 | 210개, 분류·상태·SHA(목록 문서) |
| 3 Stage = 실제 범위 | 허용 목록(`--pathspec-from-file`)과 Stage 목록이 정확히 같음. `git add -A` 미사용, 남은 변경 0 |
| 4 비밀정보·임시·산출물 제외 | 비밀정보 패턴 0건, bin/obj/build 경로 0건 |
| 5 D3 SHA 5개 | 일치 |
| 6 보호 파일 blob = 승인 바이트 | D3 5개 blob SHA 일치. Stage blob 209개가 작업 트리와 바이트 동일, `eol=crlf` 문서 2개는 설정대로 LF 정규화, 예상 밖 0 |
| 7 diff·신규 목록 검토 | 수정 14개(numstat 기록), 추가 197개 |
| 8 계약·OWNER 결정 원문 불변 | 계약 `1338a2f7…`, 승인 기록 `cb8dd03b…`, 결정 1 `af7142e5…`, 결정 2 `fb469705…`, 결정 3 `bcb7bee7…` 그대로 |

## 2. 깨끗한 체크아웃 재검증 (`git clone --no-hardlinks` → `E:\Madegame\Star_player_b1_verify`, `c15360c`, system `core.autocrlf=true`)

| 검사 | 결과 |
|---|---|
| 구현 목록 SHA(`sha256sum -c`) | **210/210 일치** |
| D3 SHA | **5개 일치** |
| 빌드 입력 바이트 | 튜닝 `992d7e24…`(검수 CRLF 바이트), 규칙 `b016b37b…` |
| 추적 파일 변경·개행 | clone 직후 `git status` 0행. verify 후에는 새 로그 1개(미추적)뿐이며 추적 파일 변경 0. `git ls-files --eol`은 규칙대로(TASK·튜닝 `-text` CRLF, `eol=crlf` 문서, `eol=lf` 소스) |
| `tools/verify.ps1 -LogName B1_CLEAN_CHECKOUT_VERIFY_LOG.md`(2026-10-26 00:45:43, Git `c15360c (clean)`) | **`GATE: FAIL`** — B1 빌드(경고 0), U1 327·U2 741·U3 128(U3a 14·U3b 48)·U4 1,237·U5 538(Golden 4 포함), G1~G4, E1~E4, D3 **PASS**. **D1 FAIL, U6 FAIL** |
| U6 원인 | verify의 명령별 제한 600초 초과(`TIMED OUT after 600 s; process tree killed`). 같은 clone에서 제한 없이 실행하면 **70/70 PASS**, 17분 53초 |
| D1 원인 | 보존한 Codex 원문 `.md`의 링크 116개(**오기** — 실제 122개, 10.3절)가 Codex가 쓴 절대 경로·행 표기(`E:/Madegame/Star_player/…/Verifier.cs:120` 등)라 상대 링크 검사에서 실패. 원본 작업 트리에서도 같습니다(원문 추출 후 D1을 다시 실행하지 않은 Claude의 확인 누락). 코드·시험 문제는 아님 |
| Dry Pilot 9 Run(clone 빌드) | **`ALL_9_IDENTICAL=True`**. ConfigHash·RunResultHash·환경 외 Manifest가 보관본과 같음. clone SimHarness DLL SHA `44f31cab…495a`(원본 작업 트리 `31110bf5…117b`와 다름 — 빌드 경로가 바이너리에 들어가는 것으로 보이며, 운영 Gate G3 재현성 대상) |

## 3. 차단 항목과 OWNER 선택지

| 차단 | 권장 | 대안 | 영향 |
|---|---|---|---|
| **D1** | Codex 원문 12개의 확장자를 `.md`에서 `.txt`로 바꾸는 Commit 1개(바이트·SHA 그대로, 색인·목록의 파일명 갱신). D1은 `.md`만 검사하므로 원문을 고치지 않고 통과합니다. | `verify.ps1` D1이 `docs/contracts/b1/originals/`를 제외(검사 범위 축소이므로 OWNER 결정 필요) | 원문 바이트 불변. 경로만 바뀌고, 이미 만든 Commit은 고치지 않습니다(새 Commit). |
| **U6** | `verify.ps1`의 U6 시험 제한 시간을 늘림(예: 1,800초). 시험 내용은 그대로 | 제한을 유지하고 U6을 따로 제한 없이 실행한 결과를 증거로 인정 | 시험 약화 없음(시간 한도만 변경). verify 스크립트 변경이므로 OWNER 허가가 필요합니다. |

두 차단을 해소하면 같은 clone 절차로 전체 재검증한 뒤 Tag(예: `b1-code-accepted-2026-10-26`)를 만듭니다. 현재 Tag는 **없습니다**.

## 4. 원문 보존

- [원문 색인](../contracts/b1/originals/INDEX.md): OWNER 16개(O01~O16), Codex 12개(판정 P01·P02·C02·C04·C06·C08·C10, 요청 C01·C03·C05·C07·C09). 로컬 세션 기록에서 바이트 그대로 추출했습니다.
- 상태: NOT_INDEPENDENTLY_VERIFIED(로컬 기록). 미확보: S00~S07 자료집 UNAVAILABLE.

## 5. 운영 Gate

G1~G9 모두 미승인·미실시입니다([계획](../tasks/B1_OPERATIONAL_GATE_PLAN.md)). 계정·SID·Pin·SHA·Golden 값은 만들지 않았습니다.

## 6. OWNER 후속 결정 처리 — 세 번째 Commit (2026-10-26)

근거: OWNER 후속 결정(2026-10-26, "B1 Git 기준본 검증 실패 후속") 결정 1~6. 기존 Commit `b2635ab`·`c15360c`는 그대로 두었습니다.

- 이 결정문은 아직 원문 파일(`originals/owner/O17_…`)로 보존하지 않았습니다. 결정 3의 Commit 범위 밖이기 때문입니다.

| Commit | 부모 | 내용 |
|---|---|---|
| `ff368a9dc7a12cccebd0d8732d19579aa9d6fad8` | `c15360c814f45629fd659abb7d7b34663b629966` | D1·U6 수정. 19경로 = 이름 변경 12(R100) + 수정 7 |

**Stage 대상(명시적 허용 목록, `git add -A` 미사용):**

| 경로 | 변경 | 결정 |
|---|---|---|
| `docs/contracts/b1/originals/codex/*.md` → `*.txt` (12) | `git mv`, 유사도 100%, blob·SHA-256 그대로 | 1 |
| `docs/contracts/b1/originals/INDEX.md` | 링크 12개 `.txt`로 갱신, 4절 "파일명·경로 변경 이력"(파일별 이전·현재 경로·SHA·blob) 추가 | 1 |
| `docs/contracts/b1/originals/index.json` | `file` 12개 갱신, `previous_file` 추가 | 1 |
| `docs/reports/B1_IMPLEMENTATION_MANIFEST.md`·`.sha256` | 경로 12개와 SHA 5개 갱신, 9절 "기준본 Commit 이후 변경"(변경 전후 바이트·SHA) 추가 | 1 |
| `tools/verify.ps1` | U6 제한만 600 → 1,800초. 로그 머리말의 제한 표기도 함께 수정 | 2 |
| `docs/contracts/b1/B1_CODE_ACCEPTANCE_OWNER_DECISION.md` | C10 원문 링크의 대상 경로만 `.md` → `.txt` | 1(아래 범위 메모) |
| `Star/01_Phase_Reviews/Phase_03F_B1_Review.md` | C10 원문 링크의 대상 경로만 `.md` → `.txt` | 1(아래 범위 메모) |

- **Stage 점검:**
  - 19개 blob이 작업 트리 바이트와 같습니다(`git hash-object --no-filters`).
  - 비밀정보 패턴 0건입니다.
  - 계약(`B1-T00_FINAL_CONTRACT*`), OWNER 원문(`originals/owner/`), `TASK.md`, PDF, B1 구현 코드·시험은 Stage 대상이 아닙니다.
  - `docs/reports/B1_IMPLEMENTATION_LOG.md`의 기존 미커밋 기록(4행)과 이 보고서는 Commit하지 않았습니다.
- **범위 메모(OWNER 확인 요청):**
  - 결정 3이 나열한 것은 문서 색인·Manifest·`.gitattributes`입니다.
  - 그런데 C10 원문을 링크한 Claude 작성 기록 2개가 더 있었습니다. 이 링크를 그대로 두면 D1이 실패합니다.
  - 그래서 결정 1의 "경로 갱신"으로 보고 링크 대상만 바꿨습니다. 두 파일 모두 OWNER 원문이 아닙니다. 결정 기록 문서는 스스로 "Claude가 쓴 기록이며 원문이 아닙니다"라고 적고 있습니다.
  - 이 처리가 허용 범위 밖이면 되돌릴 수 있습니다(Commit 3만 수정 대상).
- **`.gitattributes`: 변경 없음.**
  - 기존 `docs/contracts/b1/** -text` 규칙이 `.txt`에 적용됩니다(`git check-attr`: `text: unset`, `git ls-files --eol`: 12개 모두 `attr/-text`, `i/lf w/lf`).
- Codex 원문의 절대 경로 링크는 수정·삭제하지 않았습니다. D1 검사 범위·링크 규칙도 바꾸지 않았습니다.

## 7. 깨끗한 체크아웃 재검증 (`ff368a9`)

- 위치: `git clone --no-hardlinks` → `E:\Madegame\Star_player_b1_verify2`
- 상태: HEAD `ff368a9dc7a1…`, clone 직후 `git status` 0행, system `core.autocrlf=true`
- Dry Pilot 비교 기준 `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_dry_run1/`(git-ignored): 원본 작업 트리에서 복사(130개 파일, SHA 전부 일치)

| # | 검사 | 결과 |
|---:|---|---|
| 1 | 전체 구현 Manifest SHA(`sha256sum -c`) | **210/210 일치** |
| 2 | D3 보호 파일 5개 | **5/5 일치** |
| 3 | Codex 원본 12개 내용 SHA | **12/12 일치**(`index.json` 28/28, 바이트 수 포함). Git blob 12개가 `c15360c`와 같음 |
| 4 | Release Build | **PASS**, 13개 프로젝트, 경고 0 |
| 5 | `tools/verify.ps1 -LogName B1_CLEAN_CHECKOUT_VERIFY_LOG.md` | **`GATE: FAIL`**(exit 1, 01:21:52~01:44:29 KST, 22분 37초). 실패는 **D1 하나**입니다 |
| 6 | 전체 시험 | **3,041 PASS / 0 FAIL / 0 SKIP**: U1 327·U2 741·U3 128(U3a 14·U3b 48)·U4 1,237·U5 538·U6 70 |
| 7 | 기존 Golden 4개 | **4/4 PASS**. U5에 포함되며, `--filter Evidence.GoldenTests`로 따로도 실행했습니다 |
| 8 | Godot·Export·Cold-copy | **PASS**: G1~G4, E1~E4 |
| 9 | D1 문서 링크 | **FAIL**: Codex 원문 링크 122개는 해소. 남은 깨진 링크 6개(8절) |
| 10 | Dry Pilot 9 Run(clone 빌드) | **`ALL_9_IDENTICAL=True`**. ConfigHash·RunResultHash·환경 외 Manifest 9개 모두 보관본과 같음 |
| 11 | Git 작업 트리 불변성 | 추적 파일 변경 **0**(`git diff HEAD` 없음). 미추적은 verify가 쓰는 로그 1개(`docs/reports/B1_CLEAN_CHECKOUT_VERIFY_LOG.md`)뿐이며 1차와 같은 설계상 동작입니다. `build/`는 무시 대상 |

- verify 로그 사본: [B1_CLEAN_CHECKOUT_VERIFY_LOG_ff368a9.md](B1_CLEAN_CHECKOUT_VERIFY_LOG_ff368a9.md)(SHA `ff1d183b…6f66`, 미커밋)
- clone의 원시 결과: `build/verify/verify-result.json` `7d278c07…cd4c6`, `U6.log` `cd34d477…f48a`
- Golden·D3·Manifest·Dry Pilot이 모두 통과했어도 Gate 판정은 `FAIL`입니다. D1 실패를 무시하지 않았습니다.

### U6 제한 변경과 실제 실행 시간

| 측정 | 조건 | 결과 |
|---|---|---|
| 1차 clone(`c15360c`), verify 안 | 제한 600초 | 600초 초과로 강제 종료 → FAIL |
| 1차 clone(`c15360c`), 제한 없이 단독 실행 | — | 70/70 PASS, **17분 53초** |
| 이번 clone(`ff368a9`), verify 안 | 제한 1,800초 | **70/70 PASS**, 시험 Duration **11분 39초**. 벽시계는 U5 로그 종료 01:29:55 → U6 로그 종료 01:41:37 = 11분 42초 |
| 별도 clone `E:\Madegame\Star_player_b1_u6time2`(`ff368a9`), 단계 분리 | verify와 같은 빌드 서버 설정, Dry Pilot 보관본 복사 | 아래 표. U6 **70/70 PASS**, 추적 파일 변경 0 |

| 단계 | 명령 | exit | 시간 |
|---|---|---:|---:|
| Restore | `dotnet restore StarPlayer.sln --disable-build-servers` | 0 | 1.6초 |
| Build | `dotnet build StarPlayer.sln -c Release --no-restore --disable-build-servers -nr:false` | 0 | 18.7초(경고 0) |
| Test | `dotnet test tests\StarPlayer.EvidenceVerifier.Tests -c Release --no-build` | 0 | 10분 55.4초(시험 Duration 10분 52초) |

- Restore가 짧은 것은 이 PC의 NuGet 전역 캐시가 이미 채워져 있기 때문으로 보입니다(확인하지 않음).
- **측정 시도 1회 무효:** 처음에는 별도 clone `E:\Madegame\Star_player_b1_u6time`에서 U6 프로젝트만 Restore·Build했습니다(1.5초·13.4초). 이후 Test가 45 FAIL / 25 PASS로 20초 만에 끝났습니다.
  - 원인: U6 Fixture `TestOnlyWorld`가 다른 프로젝트의 Release 출력(`tests\StarPlayer.Domain.Tests\bin\Release\net8.0\StarPlayer.Domain.Tests.dll`)을 읽습니다. 그런데 프로젝트 하나만 빌드해서 그 파일이 없었습니다(`DirectoryNotFoundException`).
  - 측정 절차의 오류이며 제품·시험 결함이 아닙니다. 위 표는 verify처럼 솔루션 전체를 빌드한 측정입니다.
  - U6은 솔루션 전체 빌드에 의존합니다. verify의 U6도 `--no-build`이며 B1 솔루션 빌드 뒤에 실행됩니다.

- **조정 근거:**
  - 1차 단독 실행이 17분 53초로 600초를 넘었습니다.
  - 1,800초는 그 측정값보다 약 12분 여유가 있습니다.
  - 제한은 유지되므로 초과하면 프로세스 트리를 종료합니다. 그 경우 시험 요약이 없으므로 `FAIL`입니다(판정 코드 변경 없음).
  - 다른 단계의 제한(빌드·Export 900초, 다른 시험 600초, Godot 300초)은 그대로입니다.
  - 시험 내용·기대값·SKIP·제외는 바꾸지 않았습니다.
- 17분 53초와 11분 39초의 차이 원인은 확인하지 않았습니다(측정 조건이 다름).

## 8. 새 차단 항목 — D1: 커밋되지 않은 Red 원시 로그 3개

| 깨진 링크(문서 → 대상) | 대상 파일(원본 작업 트리) | 보고서에 기록된 SHA와 비교 |
|---|---|---|
| `B1_T11_FIX_R2_REPORT.md`, `B1_IMPLEMENTATION_LOG.md` → `B1_T11_FIX_R2_RED.log` | 존재, 8,060 B, `81a470b0…4368` | 일치(`B1_T11_FIX_R2_REPORT.md` 32행) |
| `B1_T11_R2_FINAL_FIX_REPORT.md`, `B1_IMPLEMENTATION_LOG.md` → `B1_T11_R2_FINAL_RED.log` | 존재, 3,361 B, `c266c5a7…0474` | 일치(78행) |
| `B1_T11_R2B_VALUE_BINDING_REPORT.md`, `B1_IMPLEMENTATION_LOG.md` → `B1_T11_R2B_VALUE_RED.log` | 존재, 2,934 B, `20439359…f420` | 일치(69행) |

- **원인:**
  - `.gitignore` 41행 `*.log`가 이 3개를 무시합니다(`git check-ignore -v`).
  - 구현 Manifest는 `git status --untracked-files=all`(무시 규칙 적용)로 만들어서 이 파일들이 목록과 Commit에서 빠졌습니다.
  - 원본 작업 트리에는 파일이 있어 로컬 D1은 통과했습니다. clone에서만 실패합니다.
- **1차에서 보이지 않은 이유:** verify의 D1 상세는 처음 8개만 표시합니다. 1차 clone(`c15360c`)에서 같은 D1 코드로 다시 세면 깨진 링크는 128개(Codex 원문 122 + 이 6개)이고, 표시된 앞 8개는 모두 Codex 링크였습니다.
- **정정:** 1차 보고서 2절의 "링크 116개"는 잘못 센 값이며, 실측은 Codex 원문 122개입니다. 1차 문구에는 숫자를 지우지 않고 오기 표시만 덧붙였습니다(3차, 10.3절).
- 다른 `PHASE_*.log` 11개도 같은 규칙으로 무시되지만, 링크된 곳이 없어 D1에는 영향이 없습니다.

| Issue | Reason | Recommended Option | Alternative | Impact |
|---|---|---|---|---|
| B1 검수 기록이 링크한 Red 원시 로그 3개가 저장소에 없음 | `.gitignore` `*.log`. Manifest 생성 방법이 무시 파일을 보지 못함 | 3개 파일만 Commit 4로 추가. `.gitignore`에 이 3개 경로의 예외(`!docs/reports/B1_T11_*_RED.log` 또는 경로 3개)를 두거나 `git add -f`로 추가. 바이트는 기존 `docs/reports/B1_* -text` 규칙으로 보존. Manifest에 3개 항목 추가(SHA는 위 표, 보고서 기록값과 일치). D1 규칙·범위 불변 | 보고서 3개와 구현 기록의 링크를 일반 텍스트로 바꿈 — B1 검수 기록 바이트가 바뀌고 원시 증거가 저장소 밖에 남아 권장하지 않음 | OWNER 허가 필요(결정 3의 범위 밖 파일 추가). 이후 깨끗한 체크아웃 전체 재검증을 다시 한 뒤 Tag |

## 9. Tag·DLL SHA·잔여 사항

- **Annotated Tag: 만들지 않았습니다.**
  - 결정 5 조건 "모든 검증 통과"를 충족하지 못했습니다(검사 5·9 FAIL).
  - `b1-*` Tag는 저장소에 없습니다(`git tag -l "b1-*"` 0개).
  - 검증에 쓴 Commit은 `ff368a9dc7a12cccebd0d8732d19579aa9d6fad8`입니다.
- **SimHarness DLL SHA(결정 6, G3 운영 Build Provenance 미결 사항):**

| 빌드 위치 | Commit | `StarPlayer.SimHarness.dll` SHA-256 |
|---|---|---|
| 원본 작업 트리 `E:\Madegame\Star_player` | (검수 작업 트리) | `31110bf5…117b` |
| 1차 clone `E:\Madegame\Star_player_b1_verify` | `c15360c` | `44f31cab…495a` |
| 이번 clone `E:\Madegame\Star_player_b1_verify2` | `ff368a9` | `e5c0fd534d62a827abfbfe428e32c6fa2cf285ca850bfb575f96bac859f74e9e` |

  - `c15360c`와 `ff368a9` 사이 코드 변경은 `tools/verify.ps1`뿐입니다(`git diff --name-only`). SimHarness 소스와 내장 입력은 같은데도 두 clone의 DLL SHA가 다릅니다.
  - 빌드 경로가 바이너리에 들어가는 것, 비결정적 컴파일 출력 등은 **가능성일 뿐이며 원인으로 확정하지 않았습니다**.
  - Game Hash·ConfigHash·RunResultHash·Manifest는 세 빌드 모두 같습니다. 이것은 계산 결과의 불변성이며, 바이너리 바이트의 재현성과는 별개입니다.
  - 게임 계산식은 바꾸지 않았습니다. 운영 Build Freeze 전에 원인을 조사하고 빌드 입력·출력 바이트 정책을 정해야 합니다.
- **운영 Gate G1~G9:** 모두 미승인·미실시 그대로입니다([계획](../tasks/B1_OPERATIONAL_GATE_PLAN.md)). 계정·SID·Pin·SHA·Golden 값은 만들지 않았습니다.
- **원문 출처:** `NOT_INDEPENDENTLY_VERIFIED` 그대로입니다(색인 3절 불변).
- Remote Push 없음. Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
- 남겨 둔 검증용 clone: `E:\Madegame\Star_player_b1_verify`(1차), `E:\Madegame\Star_player_b1_verify2`(이번), `E:\Madegame\Star_player_b1_u6time`·`E:\Madegame\Star_player_b1_u6time2`(U6 시간 측정용). 삭제하지 않았습니다.

## 10. D1 잔여 오류 수정 — 네 번째 Commit과 최종 검증 (2026-10-26)

근거: OWNER 결정(2026-10-26, "B1 Git Baseline D1 잔여 오류 수정"). 세 번째 Commit `ff368a9`는 유지했습니다.

- C10 링크 대상을 정정한 두 파일(`B1_CODE_ACCEPTANCE_OWNER_DECISION.md`, `Phase_03F_B1_Review.md`)은 OWNER가 허용했습니다.
- 이 변경은 OWNER 원문 변경이 아니라 **참조 경로 정정**으로 기록합니다. 두 파일 모두 Claude가 쓴 기록이며, 바뀐 것은 실제 Codex 원본 경로(`.md` → `.txt`)뿐입니다.

### 10.1 네 번째 Commit

| Commit | 부모 | 변경(6경로) |
|---|---|---|
| `7bdde7dd091f9a66957bc740c77d470c7732b3ad` | `ff368a9dc7a12cccebd0d8732d19579aa9d6fad8` | Red 로그 3개 추가, `.gitignore` 예외 4행, Manifest `.md`·`.sha256` |

- **Stage 점검:**
  - Stage 목록이 위 6경로와 같았습니다.
  - 6개 blob이 작업 트리 바이트와 같습니다(`git hash-object --no-filters`).
  - 추가된 행에서 비밀정보 패턴은 0건입니다.
  - B1 코드·시험·Golden·승인 계약·OWNER 원문·`TASK.md`는 변경하지 않았습니다.
- **`.gitattributes`:** 변경하지 않았습니다. 기존 `docs/reports/B1_* -text`가 세 로그에 적용됩니다(`git check-attr`: `text: unset`).

### 10.2 Red 원시 로그 3개 보존

| 경로 | 바이트 | SHA-256 | 보고서 기록값 | 개행(원래 바이트 그대로) |
|---|---:|---|---|---|
| `docs/reports/B1_T11_FIX_R2_RED.log` | 8,060 | `81a470b0b3878c2080b21e45db99787b9d77306ba90f19e7e0ccfcc6f7cb4368` | 일치(`B1_T11_FIX_R2_REPORT.md` 32행) | CRLF 75 + LF 7 |
| `docs/reports/B1_T11_R2_FINAL_RED.log` | 3,361 | `c266c5a7ac608a19aa71ed00f846b67e68c8c5593241060829938a13d9c70474` | 일치(`B1_T11_R2_FINAL_FIX_REPORT.md` 78행) | CRLF 29 + LF 3 |
| `docs/reports/B1_T11_R2B_VALUE_RED.log` | 2,934 | `20439359442817f9002adce0d217f8720024bdb54829dae42fa2c61c7502f420` | 일치(`B1_T11_R2B_VALUE_BINDING_REPORT.md` 69행) | CRLF 25 + LF 4 |

- **실제 경로:** 저장소 안에서 같은 이름의 파일은 `docs/reports/`의 이 3개뿐입니다.
- **비밀정보·민감정보 검사:**
  - 대상 패턴: API Key·Token·비밀번호·Bearer·개인 키·이메일
  - 결과: 검출 0건. `CancellationTokenSource`(.NET 형식 이름) 1건은 오탐입니다.
  - 들어 있는 것은 이 PC의 빌드·시험 경로뿐입니다(`E:\Madegame\Star_player\tests\…`, `C:\Users\user\AppData\Local\Temp\simharness-…`, 계정명은 일반값 `user`). 이메일·실명·키는 없습니다.
- 내용과 개행은 수정하거나 재구성하지 않았습니다. 이 PC 작업 트리, 깨끗한 체크아웃, Git blob의 바이트가 모두 같습니다.
- **`.gitignore`:**
  - `*.log`(41행)는 유지했습니다.
  - 바로 아래에 정확한 경로 예외 3개(`!/docs/reports/B1_T11_FIX_R2_RED.log` 등)와 주석 1행을 추가했습니다.
  - 확인 결과 다른 `PHASE_*.log` 11개와 새 로그(예: `docs/reports/x.log`)는 계속 무시됩니다.
  - 새 체크아웃에서도 같습니다. `.gitignore`는 clone에서 `core.autocrlf` 때문에 CRLF가 되지만, 같은 규칙으로 동작했습니다.

### 10.3 Manifest와 D1 집계 정정

- Manifest는 210개에서 **213개**가 됐습니다(검수·실행 기록 14 → 17). 누락 원인, 처리, D1 집계는 Manifest 9절 끝에 적었습니다.
- **D1 이전 실패 개수**(전체 개수, 같은 D1 코드로 다시 셈):
  - 최초 깨끗한 체크아웃 `c15360c`: **128개** = Codex 원문 관련 **122개** + Red 로그 누락 관련 **6개**
  - 두 번째 `ff368a9`: 6개(Red 로그 관련만)
  - 세 번째 `7bdde7d`: **0개**
- **116개 오기:** 1차 보고서 2절과 구현 기록(미커밋 메모)의 "116개"에 **오기** 표시를 덧붙였습니다. 숫자는 지우지 않았고, 당시 원시 증거(verify 로그·결과 파일)도 고치지 않았습니다.
- **참조 연결:** 깨끗한 체크아웃에서 확인했습니다.
  - 원문 색인 `index.json`의 28개 경로가 모두 Git 추적 파일입니다.
  - D1 전체 검사로 `.md` 문서의 상대 링크도 모두 실제 파일에 연결됩니다(205개 파일, 0개 오류). 여기에는 원문 색인 `INDEX.md`와 Red 로그를 링크한 보고서 3개가 포함됩니다.

### 10.4 깨끗한 체크아웃 전체 재검증 (`7bdde7d`)

- 위치: `git clone --no-hardlinks` → `E:\Madegame\Star_player_b1_verify3`
- 상태: HEAD `7bdde7dd…`, clone 직후 `git status` 0행, system `core.autocrlf=true`
- Dry Pilot 비교 기준은 원본 작업 트리에서 복사했습니다(130개 파일, SHA 전부 일치).

| # | 조건 | 결과 |
|---:|---|---|
| 1 | 구현 Manifest 전체 SHA | **213/213 일치** |
| 2 | D3 보호 파일 5개 | **5/5 일치** |
| 3 | Codex 원본 12개 | **12/12 일치** |
| 4 | Red 원본 3개 | **3/3 일치**(추적 파일) |
| 5 | 전체 `tools/verify.ps1` | **`GATE: PASS`**(exit 0, 02:54:28~03:12:54 KST, 18분 26초, 로그의 Git `7bdde7d (clean)`) |
| 6 | 전체 시험 | **3,041 PASS / 0 FAIL / 0 SKIP**: U1 327·U2 741·U3 128(U3a 14·U3b 48)·U4 1,237·U5 538·U6 70 |
| 7 | U6 | **70/70 PASS**, 시험 Duration **9분 13초**(벽시계: U5 로그 03:01:05 → U6 로그 03:10:21, 9분 16초). 제한 1,800초 유지 — clone의 `verify.ps1`에 `{ 1800 }`, 로그 머리말에 `except U6 1,800 s` |
| 8 | Golden 4개 | **4/4 PASS**(U5에 포함, `--filter Evidence.GoldenTests`로 따로도 실행) |
| 9 | Godot·Export·Cold-copy | **PASS**: G1~G4, E1~E4 |
| 10 | D1 문서 링크 | **PASS, 전체 오류 0개**. verify 상세 "205 files checked"이며, 같은 D1 코드로 전체 개수를 따로 세어도 0개(처음 8개만 본 것 아님) |
| 11 | Dry Pilot 9 Run | **`ALL_9_IDENTICAL=True`**. ConfigHash·RunResultHash·환경 외 Manifest 9개 모두 보관본과 같음 |
| 12 | 추적 파일 변경 | **0개**(`git status --untracked-files=no` 0행). 미추적은 verify가 쓰는 로그 1개뿐(설계상 동작) |

- verify 로그 사본: [B1_CLEAN_CHECKOUT_VERIFY_LOG_7bdde7d.md](B1_CLEAN_CHECKOUT_VERIFY_LOG_7bdde7d.md)(SHA `a30bc1e3…ee93`, 미커밋)
- clone 원시 결과: `build/verify/verify-result.json` `d56f9bff…fd24`, `U6.log` `b2810280…ce86`

**U6 실제 실행 시간 모음:**

| 실행 | 시간 |
|---|---|
| 1차 clone, 단독, 제한 없음 | 17분 53초 |
| 2차 clone, verify 안 | 11분 39초 |
| 별도 clone, 단계 분리 | Test 10분 55초(Restore 1.6초, Build 18.7초) |
| 3차 clone, verify 안 | **9분 13초** |

모두 1,800초 안입니다. 시간 차이의 원인은 확인하지 않았습니다.

### 10.5 Annotated Tag

| 항목 | 값 |
|---|---|
| Tag | `b1-code-accepted-2026-10-26`(annotated, 객체 `5e19ddf505a2e05d2fc36228707f9b2d06fe8217`) |
| 대상 Commit(`^{commit}`) | `7bdde7dd091f9a66957bc740c77d470c7732b3ad` |
| 깨끗한 체크아웃에서 검증한 Commit | `7bdde7dd091f9a66957bc740c77d470c7732b3ad` — **같음** |
| 기존 같은 이름 Tag | 생성 전 없음(덮어쓰기 없음) |

- **Tag 메시지:**
  - 위 검증 결과를 담았습니다.
  - B1 코드 구현 기준본의 수용만 뜻하며, 운영 Build Freeze·Official Execution 허가·Phase 3 완료가 아니라고 적었습니다.
- Remote Push는 하지 않았습니다. Commit·Tag 모두 로컬에만 있습니다(`origin` 설정은 있으나 push 명령 미실행).

### 10.6 G3 미결 — SimHarness DLL SHA 차이 (결정 6 유지)

| 빌드 위치 | Commit | `StarPlayer.SimHarness.dll` SHA-256 |
|---|---|---|
| 원본 작업 트리 | (검수 작업 트리) | `31110bf5…117b` |
| `E:\Madegame\Star_player_b1_verify` | `c15360c` | `44f31cab…495a` |
| `E:\Madegame\Star_player_b1_verify2` | `ff368a9` | `e5c0fd534d62a827abfbfe428e32c6fa2cf285ca850bfb575f96bac859f74e9e` |
| `E:\Madegame\Star_player_b1_verify3` | `7bdde7d` | `987f2013c830f7a308ea6cfc5ef3365564422c0c3a6daa39a2fba6feaa19aad8` |

- `c15360c` 이후 바뀐 코드는 `tools/verify.ps1` 하나입니다. 그 밖의 변경은 문서·로그·`.gitignore`뿐입니다. SimHarness 소스와 내장 입력은 같은데도 빌드 위치마다 DLL SHA가 다릅니다.
- 원인은 **확정하지 않았습니다**. 빌드 경로가 바이너리에 들어가는 것, 비결정적 컴파일 출력 등은 가능성일 뿐입니다.
- 계산 결과는 네 빌드 모두 같습니다(ConfigHash·RunResultHash·Manifest). 바이너리 바이트 재현성은 별개의 미결 사항이며, 운영 Build Freeze 전에 조사하고 빌드 입력·출력 바이트 정책을 정해야 합니다. 게임 계산식은 바꾸지 않았습니다.

### 10.7 커밋하지 않은 파일 (보존 필요성과 승인 범위)

| 파일 | 상태 | 보존 필요성 | 승인 범위 |
|---|---|---|---|
| `docs/reports/B1_GIT_BASELINE_REPORT.md`(이 보고서) | 미추적 | 높음. Commit·검증·Tag 경위의 주 기록 | Commit 허가 없음 |
| `docs/reports/B1_IMPLEMENTATION_LOG.md` 추가분 | 추적 파일의 미커밋 수정(1·2차 메모, 116 오기 표시) | 중간. 이 보고서를 가리키는 요약 | Commit 허가 없음 |
| `docs/reports/B1_CLEAN_CHECKOUT_VERIFY_LOG_ff368a9.md`·`…_7bdde7d.md` | 미추적 | 중간. clone 안의 원본 로그 사본(위 SHA) | Commit 허가 없음 |
| `docs/contracts/b1/originals/owner/O17_20261025T161612Z_d1-u6-follow-up-decision.txt` | 미추적 | 높음. 2차 결정 원문 | 결정 4항: 확보 가능하면 원본 그대로 보존, 신규 자료 임의 Commit 금지 |

- **O17 보존 내용:**
  - 출처: 이 세션 기록 `ebd95d80-533b-4324-b731-d26d936bb2f3.jsonl` 6행 `message.content`(uuid `a699f21b-18a8-4318-a008-dc49ca9da620`, 2026-10-25T16:16:12.236Z = 2026-10-26 01:16:12 KST)
  - 기존 O01~O16과 같은 방법으로 기록된 문자열을 그대로 UTF-8로 저장했습니다.
  - 4,423 B, SHA-256 `5c65b4f93ff441d8a4c30922aa2b2be810a0358bd6643162786ddc3b09f8d984`, 원본 행 SHA-256 `cae4c24990f380f0602cc04f3b8172e4310ae26c1b7b408c73bc3f6213cf46ff`, 비밀정보 0건
  - 원문 색인(`INDEX.md`·`index.json`)에는 아직 추가하지 않았습니다(Commit 범위 밖).
- 이번 결정문(D1 잔여 오류 수정, 3차)은 아직 원문 파일로 보존하지 않았습니다.
- 위 파일들을 Commit하려면 별도 OWNER 허가가 필요합니다.

### 10.8 잔여 사항

- 운영 Gate **G1~G9: 모두 미승인·미실시**([계획](../tasks/B1_OPERATIONAL_GATE_PLAN.md)). 실행하지 않았습니다.
- G3 Build Provenance(10.6) 미결
- 원문 출처는 `NOT_INDEPENDENTLY_VERIFIED` 그대로이며, S00~S07은 UNAVAILABLE입니다.
- 남겨 둔 검증용 clone 5개(삭제하지 않음): `E:\Madegame\Star_player_b1_verify`, `…_b1_verify2`, `…_b1_verify3`, `…_b1_u6time`, `…_b1_u6time2`
- Remote Push 없음. Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 11. Tag 이후 증거 보존 (OWNER 지시 O19, 2026-10-26)

- 10.7의 미커밋 파일은 OWNER 승인에 따라 Tag 이후 문서 전용 Commit으로 보존합니다. 대상과 SHA는 [Post-Tag 증거 색인](B1_POST_TAG_EVIDENCE_INDEX.md)에 있습니다.
- 그 Commit의 SHA는 이 보고서에 적을 수 없습니다(자기 참조). OWNER 보고에서 따로 제출합니다.
- **구현 기록 추가분:**
  - `B1_IMPLEMENTATION_LOG.md`는 Manifest 213개 항목 중 하나입니다.
  - 그래서 그 파일은 Tag 시점 바이트(`2fa62ab1…feea`)로 되돌렸습니다.
  - 추가 8행은 [B1_IMPLEMENTATION_LOG_POST_TAG_ADDENDUM.md](B1_IMPLEMENTATION_LOG_POST_TAG_ADDENDUM.md)로 바이트 그대로 옮겼습니다.
  - 10.3의 "구현 기록(미커밋 메모)의 오기 표시"는 이제 이 Addendum에 있습니다.
- 이 문서 Commit에서는 전체 verify를 다시 실행하지 않습니다. 10.4의 결과는 Tag 대상 Commit `7bdde7d`의 깨끗한 체크아웃 결과입니다.
- Tag `b1-code-accepted-2026-10-26`는 이동·삭제·재생성하지 않습니다.
