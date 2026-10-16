---
type: decision
date: "2026-10-17"
status: Game Calling v4 (GC-A/B/C) 구현·자체 테스트 완료 — 검토 대기(승인 아님), Phase 3 전체 Gate INCOMPLETE
related_phase: "03"
tags:
  - star-player
  - decision
  - phase-3-e
---

# 2026-10-17 — Phase 3-E Game Calling v4 (GC-A / GC-B / GC-C) 구현 결정

> [!warning] 구현 완료 ≠ 승인
> 구현자(Claude Code)의 자체 판정은 `PHASE3_E_IMPLEMENTATION_GATE_PASS_PENDING_COMMIT`이지만 GPT Chat/Codex 검토는 `미실시`이며 이 노트는 승인이 아니다. Phase 3 전체 Gate는 3-F 미완료로 `INCOMPLETE`다.

## 출처
- 소유자 `TASK.md` "Star Player Phase 3-E Game Calling v4 Integrated Implementation"(기준 HEAD `131dcac` = 커밋된 Framing v3), 정책 GC1~GC10, 추가 계약 GC-S1·GC-S2(Codex `CODEX_PHASE3_E_GC_ARCH_REVISIONS_REQUIRED` 수용; 검토 원문은 저장소에 없어 인용하지 않음, 지적 GC-F01·GC-F02는 소유자 승인 계약으로 기록).
- 공식 기록: [PHASE_3E_GAME_CALLING_V4_REPORT](../../docs/reports/PHASE_3E_GAME_CALLING_V4_REPORT.md) · [game-engine §33](../../docs/game-engine.md) · [ADR-007 "Decisions (Game Calling v4, Phase 3-E GC-A/B/C)"](../../docs/decisions/ADR-007.md) · [ADR-006 addendum](../../docs/decisions/ADR-006.md) · [ADR-005 addendum](../../docs/decisions/ADR-005.md).

## 결정 요약
| 항목 | 결정 |
|---|---|
| 의미·적용 | 포수의 Borderline 목표 구역 공략 보조. `actual_location`에서 `pitcher.control`과 같은 결과(목표 구역)에 곱셈 결합, 두 판정 방식 모두. 전술 AI·약점 분석 제외 |
| 범위 | **목표 구역**이 Borderline(12)일 때만 적용, Clear(5)는 `Applied=false`·계수 1,000,000. 도착 구역은 무관. 범위는 모델의 `ModifierConditionScope.BorderlineTarget`이 소유하고 `StageProbability.Compute` 안에서 강제(직접 호출 우회 불가, 미정의 조합은 Fail-closed) |
| 수치 | 중립 50, 1,000ppm/점, 계수 950,000~1,050,000. Control×GC: 878,750 / 971,250 / 1,000,000 / 1,021,250 / 1,128,750. **중복(overlap)은 제거된 것이 아니라 Combined Cap(800,000~1,250,000)으로 제한됨** |
| 버전 | `prob-model-v4` + `tuning-virtual-proto-v4`(해시 `5011f8ac…b8fd8`, 독립 Python 계산) + `player-input-set-v3`. v1~v3 불변, Model/Tuning 대응은 v1/v1~v4/v4 명시표 |
| 입력 (GC-S1) | PlayerInputSet 콘텐츠 v3에 `GameCalling`·`PitcherControl`. v4 Control의 권위 = `GameState`의 현재 투수 + 경기에 고정된 콘텐츠 v3. `Prepare`가 동결(`FrozenPitcherInput`)하고 시트 Control 불일치를 RNG 이전에 거부, `CheckPrepared` 6d와 Commit이 재도출. Prepared/Audit에 적힌 Control은 기대값이 아님. 경기 중 새 Control 자동 채택 없음 |
| 검증 (GC-S2) | Commit이 H0의 비공개 복사본에서 기록된 추첨을 **재생**해 Roll·RawDraws·Outcome·최종 H1을 대조(같은 Outcome 구간 안의 Roll 변조도 거부). 새 Stage/Stream/Draw 없음, 재생은 게임 Draw가 아님(`PitchRollReplay`, 별도 파일·스냅샷으로만 생성) |
| 기각한 대안 | 리졸버에서 Scope 필터링(Compute 직접 호출로 우회 가능) · 일반 Predicate 엔진 · 검증용 별도 RNG 스트림(새 Stream) · 권위 RNG에서 직접 추첨 · 기록된 Roll을 확률표에만 재대입 · `pitcher.control`의 두 번째 연결 지점(이중 적용 위험) |

## 남은 NOTE
- `battery.chemistry`(Phase 7), 약점 공략 AI, 동적 유효 Control(피로·컨디션) 미구현. v1~v3 커밋 증거에는 Roll 재생을 적용하지 않음(소급 변경 금지, 확장은 소유자 결정 필요).
- KBO 공식 RuleProfile 외부 근거 미검증(테스트 프로필을 공식으로 표기하지 않음), Phase 4 저장(콘텐츠 버전+내용+ID), 실제 Runner, Phase 3-F.
- Codex·전체 Mutation·`verify.ps1`·대규모 시뮬레이션 `미실시`. 추가 Codex 리뷰 권장: 저장 무결성·핵심 시뮬레이션 규칙(Commit 증거와 RNG 재생).
