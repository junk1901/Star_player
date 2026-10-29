# B1 Phase 3 운영 단계 전환 보고서 (G4/G5 검수 수용, 기존 공개 GitHub 사용)

- 작성: Claude Code, 2026-10-29. 근거: OWNER 지시 "G4/G5 검수 수용 및 기존 GitHub 기반 Phase 3 운영 단계 전환"(2026-10-29)
- **수행하지 않은 것:** Push(Dry Run 포함), 원격 생성·설정 변경, Tag 생성·변경, 운영 ACL 적용, Trust Anchor·Pin 등록, 공식 G1~G9, Build Provenance 등록, 이력 재작성, 수용된 제품·검증기·G4/G5 도구 코드 변경
- 기준: 작업 시작 HEAD `5e2c6403223578c3e6531991dbcfd00fc1c504b7`(작업 트리 Clean), 검증 소스 `1751fe04aaafbda65462915d2dac89e56536a8ae`
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기존 공개 GitHub와 로컬 HEAD의 관계 ([사실], 2026-10-29)

| 항목 | 값 | 확인 방법 |
|---|---|---|
| `origin` URL | `https://github.com/junk1901/Star_player.git` (fetch·push 같음) | `git remote -v` |
| 서버 Ref | `refs/heads/main` = `7456f44fd3ecd687afce210eb5be972114f8479c` **하나**, Tag 0개, `HEAD` → `refs/heads/main` | `git ls-remote --refs origin`, `git ls-remote --symref origin HEAD` |
| 로컬 추적 Ref | `refs/remotes/origin/main` = `7456f44…`(서버와 같음, Fetch 불필요) | `for-each-ref` |
| 공통 조상 | `7456f44` | `git merge-base HEAD 7456f44…` |
| Fast-forward | **가능**(원격 `main`이 HEAD의 조상, 원격에만 있는 Commit 0개) | `merge-base --is-ancestor`, `rev-list --count` |
| 새로 공개될 Commit | 49개(`5e2c640` 기준, Merge 0개). 이 보고서의 문서 Commit을 더하면 50개 | `rev-list --count 7456f44..HEAD` |
| 전체 Commit | 68개(`5e2c640` 기준) | `rev-list --count HEAD` |
| 로컬 Branch | `main`(`96070c5`, 원격보다 32개 앞), `phase3/g4-official-entry`(HEAD) | `for-each-ref` |
| 로컬 Tag | `b1-code-accepted-2026-10-26` → `5e19ddf…` → `7bdde7dd…`(원격에 없음, 변경 없음) | `for-each-ref` |
| 충돌·Divergence | 없음 | 위 |
| 기준선 Ref 대조 | `a1-refcheck.ps1 -Expect refs/heads/main=7456f44…` → **PASS**(`remote_refs=1`) | `phase3ops\refcheck_prepush_baseline.txt` |

- Push Dry Run은 GitHub HTTPS에서 쓰기 자격 증명을 쓰므로 실행하지 않았습니다(승인 A 뒤 OWNER 세션).

## 2. 변경한 계획 (문서만)

| 문서 | 변경 |
|---|---|
| [A-1 게시 준비](../tasks/B1_A1_PUBLICATION_PREP.md) | 전면 개정: 기존 공개 원격, 고정 URL·SourceLink 문자열, 2026-10-29 실측 상태, 게시 전 "빈 원격" 확인 → **기록된 원격 상태와 정확히 같은지** 대조(`a1-refcheck`), 비강제 Fast-forward Push, 공개 범위 재산출, A1-1 결정 반영. 검수된 검사·대조·Push 규칙은 유지 |
| [Phase 3 운영 전환 계획](../tasks/B1_PHASE3_OPERATIONS_TRANSITION.md) | 신규: 검수 종료 기록, SourceLink·B-4 계획, Build Provenance 13항목, G5 결정값 B-1~B-14, 운영 실측 M-1~M-11, Pin 절차, 의존성·순서, 승인 A/B/C |
| [운영 Gate 준비표](../tasks/B1_OPERATIONAL_GATE_READINESS.md) | Gate별 실행 계정·승인값·명령 표 추가, G3·G5 현재 상태, 승인 지점 A/B/C |
| [B-4 빌드 절차 초안](../tasks/B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md) 11.4절 | OWNER 결정 R-1 기록 |
| [G5·A-1 준비 초안](../tasks/B1_G5_A1_OPERATIONAL_PREP_DRAFT.md) | 신규 비공개 원격 계획 취소 기록 |
| [G5 운영 설계](../tasks/B1_G5_OPERATIONAL_DESIGN_DRAFT.md) 9절 | 전환 계획 연결, Codex NOTE 보존 |

- 도구·제품·계약 변경: 없음(`git diff --stat 1751fe0 -- src game tests tools content docs/contracts TASK.md` 0줄).

## 3. 이번에 실행한 진단과 결과

| 진단 | 결과 | 기록(저장소 밖 `E:\Madegame\g3x\phase3ops\`) |
|---|---|---|
| 원격 Ref 조회·기준선 대조 | 1절 | `refcheck_prepush_baseline.txt` (`224654ba…`) |
| A-1 Secret·Privacy Scan(`5e2c640`) | SECRET 0. 새 공개: 실제 SID 1개 파일, 사용자 경로 약 51개·프로젝트 경로 약 82개 파일 증가, 원문 35개, PDF 2개(D3 보호 파일) | `E:\Madegame\g3x\g5fix3\a1scan_5e2c640\`(이전 작업 산출). 문서 Commit 검사는 Commit 뒤 실행 |
| SourceLink URL 표기 영향(`1751fe0`, 임시 clone, CI 빌드, 원격 조회 없음) | 정확한 URL과 `.git` 없는 URL은 같은 DLL(`aec9ca30…`). 소문자 `star_player`는 SourceLink·DLL 다름(`afaa6887…`). 임시 clone 삭제 | `sourcelink_url_case.txt` (`b088e6d5…`) |
| SourceLink 삽입 위치(`1751fe0` 개발 빌드) | URL·Commit은 PDB SourceLink JSON에만. 어셈블리 특성에는 없음. DLL에는 PDB 식별값 | (분석 결과, A-1 문서 5절) |
| B-4 후보 재산출(`1751fe0` 개발 빌드) | 441행, 비Pin 31, StarPlayer 27행만 변경(`a980ea5` 대비), 414행 불변, `CANDIDATE_INCOMPLETE` | `b4_candidates_1751fe0_devbuild.json` (`5bbe1547…`) |
| 문서 링크(D1과 같은 규칙, 변경 문서 6개) | 깨진 상대 링크 0 | — |
| D3 보호 파일 5개 | 기준 SHA와 모두 일치 | — |

- **재실행하지 않은 것:** `verify.ps1` 전체(문서만 바뀌어 위 D1·D3 확인으로 대신함), xUnit 3,061건, Golden, Dry Pilot, PCK 5회. 이전 증거(`1751fe0` Clean Checkout)를 재사용합니다.
- 새 진단 폴더는 `phase3ops` 하나만 만들었습니다. 기존 증거 폴더는 삭제하지 않았습니다.

## 4. 새로 확인된 사항

- **계약 충돌·MAJOR 보안 문제:** 없음.
- **판단이 필요한 사실:**
  - 공개될 PDF 2개는 D3 보호 파일입니다. 제외하려면 이력 재작성과 승인 기준선 변경이 모두 필요합니다(A-1 문서 3.3절).
  - 2026-10-28 이후 OWNER 지시 4건은 저장소 밖 원문 폴더에 추출되어 있지 않습니다(A-1 문서 3.2절).
  - `refs/pull/*` 같은 서버 숨김 Ref는 게시 후 대조로 확인할 수 없습니다(A-1 문서 2.2절 6단계 한계).

## 5. OWNER 승인 A 요청 항목

[A-1 문서](../tasks/B1_A1_PUBLICATION_PREP.md) 8절 A1-2~A1-7입니다. 요약:

- **게시 Commit·Ref:** `refs/heads/main` = 이 보고서를 담은 문서 Commit(Fast-forward, 비강제). SHA는 OWNER 전달 요약에 적습니다.
- **Tag:** 최초 Push에서 게시하지 않음(권고)
- **공개될 추가 이력:** 50개 Commit, 원문 35개, 실제 SID 1건, 로컬 경로, PDF 2개
- **절차:** A-1 문서 2.2절 1~8단계
