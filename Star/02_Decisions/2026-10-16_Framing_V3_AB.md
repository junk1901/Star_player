---
type: decision
date: "2026-10-16"
status: Framing v3 Package A+B 구현·자체 테스트 완료 — 검토 대기(승인 아님), Game Calling 미구현으로 3-E 전체 Gate INCOMPLETE
related_phase: "03"
tags:
  - star-player
  - decision
  - phase-3-e
---

# 2026-10-16 — Phase 3-E Framing v3 (Package A + B) 구현 결정

> [!warning] 구현 완료 ≠ Phase 3-E 완료
> 이 노트는 Framing만 다룬다. Game Calling·battery.chemistry는 구현하지 않았고 Phase 3-E 전체 Gate와 Phase 3 Gate는 `INCOMPLETE`이다. GPT Chat/Codex 검토는 `미실시`.

## 출처
- 소유자 `TASK.md` "Star Player Phase 3-E Framing v3 Integrated Implementation"(기준 HEAD `b64d3e9`), 정책 E1~E7, Codex 아키텍처 판정 `CODEX_PHASE3_E_ARCH_PASS_WITH_NOTES`(검토 원문은 저장소에 없음 — 인용하지 않음).
- 공식 기록: [PHASE_3E_FRAMING_AB_REPORT](../../docs/reports/PHASE_3E_FRAMING_AB_REPORT.md) · [game-engine §32](../../docs/game-engine.md) · [ADR-007 "Decisions (Framing v3, Phase 3-E A+B)"](../../docs/decisions/ADR-007.md) · [ADR-006 addendum](../../docs/decisions/ADR-006.md) · [ADR-005 addendum](../../docs/decisions/ADR-005.md).

## 결정 요약
| 항목 | 결정 |
|---|---|
| 적용 범위 | NonAutomated + Borderline(12구역)에서만 적용. Automated·Clear(5구역)는 `Applied=false`, 계수 1,000,000. 범위는 모델의 `ModifierConditionScope`가 소유(리졸버가 걸러내지 않음) |
| 수치 | 중립 50, 1,000ppm/점, 계수 950,000~1,050,000, `called_pitch`의 `strike`만. 가상 프로토타입 값(3-F에서 균형 판단) |
| 버전 | `prob-model-v3` + `tuning-virtual-proto-v3`(해시 `682a5f06…e389a`). v1·v2 불변. Model/Tuning 대응은 명시적 표 |
| 입력 집합 | 콘텐츠 v1(ID 불변) / v2(`ReceivingFraming`). 버전은 타입이 보유. v1에 Hash 제외 필드를 넣는 방식은 기각 |
| 포수 | `Prepare`에서 실제 포수 ID·Rating 동결(`FrozenCatcherInput`), 누락은 Typed Error(50 대입 없음). Automated·v1/v2는 요구하지 않음 |
| 검증 | `CheckPrepared` 6c와 `CommitEvidence.LinkCalledPitch`가 실제 상태·규칙·Set에서 재도출·재계산. 감사/Prepared에 적힌 포수 ID·Rating은 기대값의 출처가 아님(F-01) |

## 남은 NOTE
- Game Calling, KBO 공식 RuleProfile 외부 근거(미검증, 테스트 프로필을 공식으로 표기하지 않음), Phase 4 저장(콘텐츠 버전+내용+ID), 실제 Runner, Phase 3-F.
- 동등 변이 1건(감사 포수 신뢰, `CommitEvidence`만): `CheckPrepared`가 먼저 같은 값을 재도출하므로 중복 방어선.
