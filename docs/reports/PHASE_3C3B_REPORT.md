# Phase 3-C3-B Closeout Report (사후 작성, C3-C-0)

> [!warning] 이 문서는 과거 보고서의 복원본이 아니다
> 이 파일은 **C3-C-0 단계(문서 Closeout, 2026-10-14)에서 새로 작성한 사후 Closeout 보고서**다. C3-B 구현 당시 작성됐어야 할 `PHASE_3C3B_REPORT.md` 원본은 저장소에 없었고, 이 문서는 그 원본을 재현하지 않는다. 과거 채팅 보고의 성공 요약을 raw 로그로 바꾸어 보존하지 않았다. 증거마다 근거 유형(§8)을 표시한다.

## 1. 목적 및 출처

- **목적:** C3-B 구현(로컬 Commit `8ed806f`)의 공식 완료 기록과 증거 가용성 정리. C3-C 진입 전 기록 공백을 닫되, 없는 증거를 있는 것처럼 만들지 않는다.
- **작성 단계:** Phase 3-C3-C-0 (문서·기존 증거 정리). 코드·테스트·검증 스크립트·Mutation 실행 없음. `dotnet build/test`, `verify.ps1`, Codex 호출 없음.
- **기준 Commit:** `8ed806f572d15a5b57e52fbcabded3bfe2316ee6` (직접 부모 `654b5d9e8053bf42305e500ac45d2537da7094f1`), 메시지 `feat: integrate phase 3c3b pitch candidate commit gate`, 작성 시각 2026-10-14 00:03:41 +09:00, 브랜치 `main`, 푸시 안 함.
- **결과의 출처:** (a) 이전 Claude Code 구현·테스트 보고(채팅; 저장소 파일 아님), (b) 아래 §8의 보존된 verify 원본, (c) 이번 단계의 읽기 전용 확인(Git, 해시 재계산). 이 세 가지를 §8에서 구분한다.
- **구현 파일(Git 직접 확인, 11개 = 신규 9 + 수정 2):**
  - 프로덕션 4: `CommitEvidence.cs`(A), `PitchCommitGate.cs`(A), `PreviewComparison.cs`(A), `CommitGate.cs`(M; +37/−10, 공통 코어에 증거 경로 연결).
  - 테스트 7: `PitchCommitFixture.cs`, `PitchCommitGateTests.cs`, `PitchCommitGateFailureTests.cs`, `PitchCommitGateConcurrencyTests.cs`, `PreviewComparisonTests.cs`(Infrastructure.Tests, 모두 A), `Authority/PitchCommitBoundaryTests.cs`(Simulation.Tests, A), `HiddenTendencyBoundaryTests.cs`(Application.Tests, M).
  - 경로는 `git show --name-status 8ed806f` 결과이며 `src/StarPlayer.Simulation/Baseball/`, `tests/StarPlayer.{Infrastructure,Simulation,Application}.Tests/` 아래에 있다. 총 +2,309 / −11 줄.

## 2. 승인 계약 (본문은 공식 문서가 기준, 여기서 재서술하지 않음)

| 계약 | 기준 문서 |
|---|---|
| D-1~D-8, A~F (C3 설계) | [game-engine §27.1–27.15](../game-engine.md), [ADR-007 "Decisions (3-C3 design approval)"](../decisions/ADR-007.md) |
| O-1~O-9 (C3-A 세부) | [game-engine §27.16](../game-engine.md) |
| B-01~B-07 (C3-B 세부) | [game-engine §27.18](../game-engine.md), [ADR-007 "Decisions (3-C3-B detailed contract approval)"](../decisions/ADR-007.md) |
| OD-1~OD-4 (C3-C 검증 설계) | 소유자 승인 완료. 차기 계약으로만 기록: [game-engine §27.20](../game-engine.md), [ADR-007](../decisions/ADR-007.md) |

이번 단계는 위 본문을 바꾸지 않았다.

## 3. 구현 결과 (Commit 내용 기준)

| 항목 | 구현 사실 |
|---|---|
| `PitchCommitGate` | 별도 public static 진입점. 비인플레이 `(expected, PreparedPitch, NonInPlayPitchCandidate, tuning, batter, pitcher, cancellation)`, 인플레이 `(expected, InPlayPitchCandidate, tuning, batter, pitcher, cancellation)`. 직접 공식 단계를 수행하지 않고 `CommitEvidence`를 만들어 공통 `CommitGate`에 넘긴다. `PendingAdvancement`용 진입점은 없다. |
| `CommitEvidence` | internal 불변. 호출마다 생성, 기반 `AuthoritativeGame`·대상 이벤트·검증된 `PreparedPitch`/후보·최종 RNG 스냅샷(H1/H2)에 결속. 반환·보관·재사용 안 함. |
| `PreviewComparison` | `GameState`/`GameStep` 구조 비교(내용·순서, 첫 차이 경로를 메시지에). 미리보기는 비교만 하고 채택하지 않는다. |
| `CommitGate` 공통 코어 | 시그니처 `Stage(expected, GameEvent, CommitEvidence?, checkpoint, cancellation)` 추가. 증거 없이 호출하면 C3-A 동작 그대로(`Stage(expected, GameEvent)`는 여전히 투구 이벤트 거절). 증거 경로는 7a/7b, 9의 투구 수, 10a, 10b를 추가하고 12단계에서 검증된 RNG 위치를 채택. |
| H0/H1/H2 | H0=`expected.Rng`; 비인플레이·기본 인플레이는 H1, 해결 인플레이는 H2를 채택. 코드에서 추첨하지 않음. |
| 스트림별 `RawDraws` | 스트림별 증분 검증(10b), 호출 수와 `RawDraws` 구분, 실패 시 `Commit.rng_evidence_mismatch`. |
| 신규 오류 코드 5 | `Commit.candidate_missing`, `candidate_link_mismatch`, `preview_mismatch`, `rng_evidence_mismatch`, `pitch_count_invalid` (`src`에서 5개 모두 확인). `Commit.receipt_mismatch` 재사용. |
| Finalize·CAS | 종료 이벤트는 같은 Stage에서 `Finalize`, 공개는 기존 `GameCursor.Publish`의 단일 CAS 하나뿐. |
| 경계 테스트 | C3-A 경계 테스트 6건 무변경, `PitchCommitBoundaryTests` 신규(선언된 [Fact]/[Theory] 10), `HiddenTendencyBoundaryTests`는 새 진입 타입 `PitchCommitGate`만 추가(+3/−1). |

B-01~B-07 대응: B-01 후보 연결(`CheckLink`), B-02 RNG 증거(`CheckRng`), B-03 구조 비교(`PreviewComparison`), B-04 대안 A(위 표), B-05 오류 5개, B-06 테스트 매트릭스(Infrastructure 신규 5개 파일 + Authority 경계), B-07 Simulation 내부(Application/Infrastructure 공개 시그니처에 숨김 타입 없음 — 경계 테스트 대상). 위는 Commit 내용·파일 구성에서 직접 읽은 구현 사실이며, 각 항목이 완전히 충족됐는지의 판정은 독립 검토(§7)의 몫이다.

## 4. 테스트 결과 (과거 보고 기준 + 근거 유형)

| 항목 | 값 | 근거 유형 |
|---|---|---|
| 전체 xUnit | 1,741 PASS / FAIL 0 / SKIP 0 | **부분 RAW_AVAILABLE**: [verify Run 1](PHASE_3C3B_PRIORITY_VERIFY_LOG_run1.md)·[Run 2](PHASE_3C3B_PRIORITY_VERIFY_LOG_run2.md)의 U1~U4 행이 프로젝트별 passed/failed/skipped를 기록(Domain 263 + Simulation 697 + Application 117 + Infrastructure 664 = 1,741, 모두 failed=0 skipped=0). **전체 xUnit 단독 실행 원본(`PHASE_3C3B_FULL_XUNIT_raw.txt` 류)은 MISSING.** |
| C3-A 기존 Authority 67 PASS | 67 | **SUMMARY_ONLY** |
| Authority 네임스페이스 전체 77 PASS | 77 | **SUMMARY_ONLY** |
| `verify.ps1` Run 1 / Run 2 | GATE PASS / exit 0 | **RAW_AVAILABLE** (보존: 로그·stdout) |
| 소스·테스트 229개 SHA-256 매니페스트 | `E33195260ED6EFF007417677B96C1B7E539C07CFC268729E678C9E72EAA6241A` | **RAW_AVAILABLE** (매니페스트 파일 보존) + **DERIVED_VERIFICATION** (§6) |
| C3-B Mutation | 13건 중 12건 검출, M7 생존 | **SUMMARY_ONLY**. 변이 정의·패치·실행 결과 원본 MISSING. |

모든 값은 과거 실행 결과이며 이번 단계에서 재실행하지 않았다.

## 5. 우선순위 테스트 보완

`Step_8_event_id_conflict_beats_step_10a_forged_preview` — `PitchCommitGateFailureTests.cs`(Commit `8ed806f`에 포함, 저장소에서 확인). O-2 순서에서 8단계(`event_id_in_use`)가 10a(`preview_mismatch`)보다 먼저임을 고정한다. 시나리오 세 가지(코드의 주석·단언 기준):

| 시나리오 | 입력 | 기대 결과 |
|---|---|---|
| A | 새 ID + 위조 미리보기 | `Commit.preview_mismatch` (메시지에 `Step.State.Count`) |
| B | 이미 사용된 ID + 다른 내용 + 진짜 미리보기 | `Commit.event_id_in_use` |
| C | 사용된 ID + 다른 내용 + 위조 미리보기 | `Commit.event_id_in_use` (`preview_mismatch` 아님), 집합체·커서 상태 불변 |

앞서 위조 없는 후보가 성공함을 단언해 위 변경만이 실패 원인이 되게 했다. 이 테스트의 PASS는 §4의 1,741에 포함된 과거 결과로만 알려져 있으며 이번 단계에서 실행하지 않았다.

## 6. 검증 시점 구분 (숨기지 않음)

| 시점 | 내용 | 근거 |
|---|---|---|
| 1. 최종 구현 트리에서의 verify | Run 1 2026-10-13 23:45:48, Run 2 23:48:44. 두 로그의 `Git:` 줄은 **`654b5d9 (dirty working tree)`** — Commit 이전, 작업 트리 미커밋 상태에서 실행 | 보존된 로그 |
| 2. 트리 매니페스트 | 23:43:01(`snap0`), 23:45:57(`snap1`), 23:49:03(`snap2`), 00:03:25(`snap3`) 모두 SHA-256 동일(`E3319526…241A`, 229개 파일). 즉 verify 전후와 Commit 직전(00:03:25, Commit 00:03:41의 16초 전)까지 소스·테스트 트리가 같았다 | 매니페스트 4개(2개 보존, 2개는 원본 임시 경로에서 해시만 확인) |
| 3. 로컬 Commit | `8ed806f` 2026-10-14 00:03:41 | Git |
| 4. Commit 이후 verify | **실행 기록 없음.** 접근 가능한 증거상 마지막 실행은 Run 2(`build/verify/verify-result.json` 최종 수정 23:48:44; 해당 파일은 `.gitignore` 대상이며 이후 verify가 덮어쓴다). Commit 이후 재실행되지 않은 verify를 Commit 이후 실행으로 표시하지 않는다. | `build/verify/` 읽기 전용 확인 |

**DERIVED_VERIFICATION (이번 단계, 읽기 전용):** 보존된 `snap.ps1`을 그대로 현재 작업 트리(HEAD `8ed806f`, 추적 파일 변경 없음)에 실행해 임시 위치에 만든 매니페스트가 `snap0`과 **경로+해시 줄 단위로 차이 0건**, 매니페스트 SHA-256도 동일(`E33195260ED6EFF007417677B96C1B7E539C07CFC268729E678C9E72EAA6241A`)함을 확인했다. 이는 "현재 트리 = verify 당시 트리"의 근거이며, 과거 실행 원본이 아니다. 이전 분석의 "해시 집합은 같지만 경로 결합 비교는 하지 않았다"는 한계는 이 비교로 해소됐다(DERIVED).

매니페스트 한계: `snap.ps1`은 경로를 `Substring(25)`로 잘라 첫 글자가 빠진다(`game\…` → `ame\…`). 모든 줄이 같은 규칙이라 고유성·비교에는 영향이 없다. 파일 선택은 `src`·`tests`·`game` 아래 `.cs .csproj .json .tscn .gd .props .targets`이며 `bin/obj/.godot` 제외 — `tools/verify.ps1`의 게이트와는 별개의 임시 도구다.

로그 표기 한계: 두 verify 로그의 제목은 `Phase 2 verification log after the Codex remediation…`이다. 이는 `tools/verify.ps1`이 쓰는 고정 문구이며 C3-B 로그라는 사실은 파일 이름과 `Run at`/`Git:` 줄로 판단한다(원본 무수정 원칙에 따라 고치지 않음). Run 1/2의 차이는 `Run at`과 E1b 행의 pck 시각뿐이다(DERIVED).

## 7. Mutation 결과와 C3-C 인계

- **C3-B 변이:** 총 13건, 12건 검출, **M7 생존**(과거 보고값). M7을 검출된 것으로 기록하지 않는다. 변이 정의·Patch·원본 결과는 접근 가능한 곳에 없다(MISSING). 저장소 전체와 임시 폴더 조사에서 M7의 내용 설명도 찾지 못했다 — 따라서 M7이 어떤 방어를 대상으로 했는지 이 문서는 말하지 못한다.
- 구현 세션 임시 폴더(`e29cf47b…`)에 `CommitEvidence.cs.bak`·`CommitGate.cs.bak`·`PreviewComparison.cs.bak`가 남아 있다. 내용이 Commit `8ed806f`의 해당 파일과 같다는 것(CommitGate는 줄바꿈만 다름)은 확인했지만, 이는 변이 실행 전 원본 백업으로 보이는 흔적일 뿐(추정) **변이 결과의 증거가 아니다.** 보존하지 않았다.
- **C3-C 인계 (차기 계약 OD-3/OD-4; 이번 단계에서 구현·실행하지 않음):**
  1. M7 — 비차단 NOTE(잠정). 전제 고정 테스트와 Codex 독립 리뷰 전까지 무조건 PASS가 아니다.
  2. M25 — 비차단 NOTE(잠정, C3-A의 `Finalize` 완전성 방어 분기; [PHASE_3C3A_REPORT](PHASE_3C3A_REPORT.md) §19·§22·§25). 도달 가능한 실제 위험이 확인되면 재분류.
  3. C3-A 초기 변이 21건 — 최종 트리에서 미재실행. 전부 재검증.
  4. M9/M10/M13 — 현재 코드에 맞춰 재정의.
  5. C3-B 변이 원본 부재 — 신규 정의로 재실행하고 원본과 신규 정의를 구별. 실제 Patch Diff 보존.
  6. Codex Medium 독립 리뷰 필요(현재 `NOT AUTHORIZED`).

## 8. 증거 목록

분류: **RAW_AVAILABLE**(원본 존재·읽힘) / **SUMMARY_ONLY**(보고에만 결과 있음) / **MISSING** / **DERIVED_VERIFICATION**(이번 단계의 읽기 전용 확인).

| 증거 | 보존 위치 | 출처 | 무결성 확인 | 상태 |
|---|---|---|---|---|
| verify Run 1 로그 | [PHASE_3C3B_PRIORITY_VERIFY_LOG_run1.md](PHASE_3C3B_PRIORITY_VERIFY_LOG_run1.md) | 세션 `32c263a9…` scratchpad 무변형 복사 | 원본=복사본 5,647 B, SHA-256 `4A5E9E13…1886` | RAW_AVAILABLE |
| verify Run 2 로그 | [PHASE_3C3B_PRIORITY_VERIFY_LOG_run2.md](PHASE_3C3B_PRIORITY_VERIFY_LOG_run2.md) | 동일 | 5,647 B, `3F88950F…C531` | RAW_AVAILABLE |
| verify Run 1 stdout | [PHASE_3C3B_PRIORITY_VERIFY_STDOUT_run1.txt](PHASE_3C3B_PRIORITY_VERIFY_STDOUT_run1.txt) | 동일(`verify_stdout_run1.txt`) | 2,846 B, `53F7E5EB…2EFE` | RAW_AVAILABLE |
| verify Run 2 stdout | [PHASE_3C3B_PRIORITY_VERIFY_STDOUT_run2.txt](PHASE_3C3B_PRIORITY_VERIFY_STDOUT_run2.txt) | 동일(`verify_stdout_run2.txt`) | 2,846 B, `82DBE328…C55C` | RAW_AVAILABLE |
| 트리 매니페스트 (Run 1 직전, 23:43:01) | [PHASE_3C3B_TREE_MANIFEST_snap0_before_run1.txt](PHASE_3C3B_TREE_MANIFEST_snap0_before_run1.txt) | 동일(`snap0.txt`) | 27,932 B, `E3319526…241A` | RAW_AVAILABLE |
| 트리 매니페스트 (Commit 직전, 00:03:25) | [PHASE_3C3B_TREE_MANIFEST_snap3_before_commit.txt](PHASE_3C3B_TREE_MANIFEST_snap3_before_commit.txt) | 동일(`snap3.txt`) | 27,932 B, `E3319526…241A` | RAW_AVAILABLE |
| 매니페스트 생성 스크립트 | [PHASE_3C3B_TREE_MANIFEST_snap.ps1.txt](PHASE_3C3B_TREE_MANIFEST_snap.ps1.txt) | 동일(`snap.ps1`, 실행되지 않도록 `.txt` 접미사 추가) | 539 B, `522050D2…8851F` | RAW_AVAILABLE |
| 매니페스트 `snap1`(23:45:57)·`snap2`(23:49:03) | 보존 안 함(원본: 세션 `32c263a9…` scratchpad) | 동일 | SHA-256이 `snap0`과 동일함을 확인 | RAW_AVAILABLE(미복사, 해시만 기록) |
| 매니페스트 해시 `E33195…241A` | 이 문서·위 두 매니페스트 | 과거 보고값과 보존 파일·재계산 모두 일치 | 재계산 일치 | RAW_AVAILABLE + DERIVED_VERIFICATION |
| 현재 트리 대 `snap0` 비교 | 보존 안 함(임시 위치에 계산, 결과 0건 차이) | 이번 단계 | 경로+해시 줄 단위 비교 | DERIVED_VERIFICATION |
| 전체 xUnit 1,741 PASS | verify 로그 U1~U4 행 | 과거 보고 + 로그 | 합계 263+697+117+664 | 부분 RAW(프로젝트별 수) / 단독 xUnit 실행 원본 MISSING |
| Authority 67 / 77 PASS | — | 과거 보고 | — | SUMMARY_ONLY |
| C3-B Mutation 12/13, M7 생존 | — | 과거 보고 | — | SUMMARY_ONLY (원본 MISSING) |
| 구현 세션 verify 로그 2건 (C3-B, 우선순위 테스트 추가 전: Infrastructure 663, Git `654b5d9` dirty, 23:04:54/23:07:51) | 보존 안 함(원본: 세션 `e29cf47b…` scratchpad) | 구현 세션 | 열람만 함(SHA-256 `954e90ee…907c`, `5d14bd38…2b74`) | RAW_AVAILABLE(미복사). 우선순위 테스트 1건이 추가되어 664가 되는 선행 상태이며 최종 증거는 위 Priority Run 1/2 |
| 구현 세션 트리 해시 (`B3A9D85C…D3D1`, 217 files, 변경 없음) | 보존 안 함(원본: 위 세션) | 구현 세션 | 열람만 함 | RAW_AVAILABLE(미복사). 파일 집합·방식이 달라 229개 매니페스트와 직접 비교 불가 |
| C3-B 구현 완료 보고서 원본 | — | — | — | MISSING (이 문서가 사후 대체; 원본 재현 아님) |

## 9. 최종 판정

**`C3B_IMPLEMENTATION_COMPLETE_WITH_NOTES`**

이 판정은 **C3-B 구현과 로컬 Commit `8ed806f` 완료**에 관한 것이다. 전체 원본 증거가 복원됐다는 뜻이 아니다. 남아 있는 증거 공백:

1. 전체 xUnit 단독 실행 원본, Authority 테스트 실행 원본: MISSING / SUMMARY_ONLY.
2. C3-B Mutation 원본과 M7의 내용: MISSING / SUMMARY_ONLY.
3. 보존된 verify는 `654b5d9` dirty tree에서 실행됐고 Commit 이후 재실행되지 않았다. 트리 동일성은 매니페스트로만 뒷받침된다.
4. Codex 독립 리뷰 `미실시`. GPT Chat 기술 검토: 이 문서 제출 후 소유자 전달.

상태: Phase 3 Gate `INCOMPLETE` · C3-C 구현·변이 재실행·Codex 리뷰 `NOT STARTED / NOT AUTHORIZED` · GitHub Push는 Phase 3 최종 Gate 이후 별도 승인 · 이 문서와 증거 파일은 Git 스테이징·커밋하지 않았다.
