---
type: decision
date: "2026-10-17"
status: Phase 3-F F-A/B (Headless 실행기 + 지표 수집) 구현·자체 테스트 완료 — 검토 대기(승인 아님), Phase 3 전체 Gate INCOMPLETE
related_phase: "03"
tags:
  - star-player
  - decision
  - phase-3-f
---

# 2026-10-17 — Phase 3-F F-A/B 하네스·지표 수집 구현 결정

> [!warning] 구현 완료 ≠ 승인
> 구현자(Claude Code)의 자체 판정은 `PHASE3_F_FA_FB_IMPLEMENTED_WITH_NOTES`이지만 GPT Chat/Codex 검토는 `미실시`이며 이 노트는 승인이 아니다. 100,000 PA·100경기·공식 판정(F-C)은 하지 않았고, 3-F와 Phase 3 전체 Gate는 `INCOMPLETE`다.

## 출처
- 소유자 `TASK.md` "Star Player Phase 3-F F-A/B Integrated Implementation"(기준 HEAD `430f42d`).
- 공식 기록: [PHASE_3F_FA_FB_IMPLEMENTATION_REPORT](../../docs/reports/PHASE_3F_FA_FB_IMPLEMENTATION_REPORT.md) · [game-engine §34](../../docs/game-engine.md) · [ADR-007 "Addendum 2026-10-17 - Phase 3-F F-A/B"](../../docs/decisions/ADR-007.md) · [SimHarness README](../../tools/StarPlayer.SimHarness/README.md)

## 결정 요약
| 항목 | 결정 |
|---|---|
| 구조 | `tools/StarPlayer.SimHarness`(콘솔). Domain + Simulation만 참조, Production `InternalsVisibleTo` 없음. 행동만 고르고 확정은 `PitchCommitGate`/`CommitGate`/`GameCursor.Publish`. 확률·결과·진루·득점·종료 계산, 난수 추첨, 상태 직접 생성 없음(소스 스캔 테스트로 고정) |
| Production 추가 | `GameStarter.Begin(GameStart, ulong)` **하나**. 엔진에 경기를 시작하는 공개 경로가 없었다. `GameState`가 아니라 `GameStart`를 받아 실제 `GameReducer.Start`로 상태를 만들고 RNG 맥락 키를 GameId로 묶으므로 새 경기만 만들 수 있다(복원·진행 중 상태 위조 불가) |
| 데이터 읽기 | Infrastructure 읽기 코드는 internal이고 아키텍처 테스트가 Infrastructure 공개 표면과 `InternalsVisibleTo` 목록을 고정한다 → 약화하지 않고 도구가 두 콘텐츠 파일(`virtual_tuning_v4.json`, `game_rules_profiles_v2.json`)을 직접 읽는다(Domain의 `TuningProfile.Create`/`GameRuleSnapshot.Create`가 검증). 두 읽기 코드가 같은 해시를 내도록 양쪽에서 고정(`SimHarnessInputPinTests` + 하네스 테스트) |
| 고정 입력 | `prob-model-v4`/`tuning-virtual-proto-v4`, `player-input-set-v3`, `gamerules-v2` 프로필(`fictional_test`), `advancement-policy-v2`(기본), 중립 구장, Event Budget 3,000. `GameExecution`은 이 객체들로 만들고 경기마다 `CheckPins`로 재검증 |
| 결정성 | `GameId = game:sim:{cohort}-{index:D6}`, 명시적 Seed, RNG 맥락 = GameId. 같은 입력 = 같은 Event/RNG/Result Hash. 시각·환경·git은 `environment_not_hashed`로 기록만 하고 Hash 제외 |
| 정책 | `normal-balanced-v1`: Normal·Balanced, Take·교체 없음. 합법성 증명용이며 지배 전략 부재의 근거가 아님. 교체는 필요하지도 만들지도 않음 |
| 종료·실패 | 엔진의 최종 상태가 있어야 `Completed`. Budget 초과·엔진 오류·예외·정체·위치 누락·교차 검증 불일치는 `Failed`(GameId·Seed·코드·마지막 영수증 12건 보존), 취소는 확정된 사건만 남김. 연장 한도 무승부는 정상 종료 |
| 지표 | F1~F8을 확정 커밋의 이벤트·`PlateAppearanceCompleted`만으로 집계. 목표는 튜닝 파일에서 읽음. 반올림 half-up, 점추정 + 경계 포함, Wilson은 진단용. 상태 `PASS`/`FAIL`/`INSUFFICIENT_SAMPLE`/`NOT_EVALUATED` |
| 판정 | 최소 표본·평가 코호트가 미승인이므로 모든 공식 판정 `NOT_EVALUATED`(`EvaluationPolicy.Unapproved`). 임의의 최소 표본(1건, 100건 등)을 고르지 않음 |
| 코호트 | 중립 = 모든 능력치 50, 우투우타(손잡이 무관 코호트는 `matchup.handedness`에 중립 키가 없어 불가능). Virtual League = 확장 지점만(능력치 분포·전략 혼합비 미정). 중립 결과는 F1~F5의 근거가 아님 |
| 교차 검증 | `BoxScoreBuilder`와 15항목. 한계: 두 경로 모두 Reducer의 타석 결과 분류에서 출발 |
| 기각한 대안 | `InternalsVisibleTo` 추가 · Infrastructure 공개 합성 타입(Domain 타입 노출) · Infrastructure JSON 소스 링크 · `Gx.Play` 복사 · 상태를 받는 공개 팩토리 |

## 남은 NOTE
- F-C 선행 결정(소유자): 대표 능력치 분포·손잡이 구성, 전략 혼합비, 공식 Seed 목록, Metric별 최소 분모, 지배 전략 검사 행렬("declared test matrix"가 어디에도 선언되어 있지 않음), Pitch Zone Lite 점검 목록(ROADMAP 문장은 12개 절 — 이전 "11개"는 2026-10-17 Codex 수정에서 정정), 공식 실행의 진루 정책·규칙 프로필, 결과 파일 보관. (`tools/verify.ps1`의 새 테스트 프로젝트 `U5`는 Codex F-03 수정으로 추가됨.)
- 규칙 프로필은 `fictional_test`; 2026 KBO 공식 RuleProfile 외부 근거 미검증. Phase 4 저장 미구현(Manifest는 실행 기록이지 저장이 아님).
- Codex·전체 Mutation·`verify.ps1`·100,000 PA·100경기 `미실시`. 추가 Codex 리뷰 권장: `GameStarter`(저장 무결성·권위 경계)와 F-C 코호트·기준 확정 전.

## 추가 기록 2026-10-17 — Codex F-01~F-04 수정

Cohort는 불변 스냅샷(`ImmutableSortedDictionary` + `ImmutableArray`)이고 `InputsHash`는 그 스냅샷으로 계산한다. Harness JSON 로더는 Infrastructure 입력 계약(필수 속성·null 요소·중복 ID·정확한 snake_case Enum)을 따르며 83건 대조 행렬로 검증한다. `verify.ps1`에 `U5` 추가(미실행). 종료 코드는 0(완료+지표 계산 성공) / 1(경기 실패) / 2(입력 오류) / 3(경기 완료·지표 계산 실패). PZL 목록은 12개 절. 근거와 수치: 보고서 부록, ADR-007 부록.
