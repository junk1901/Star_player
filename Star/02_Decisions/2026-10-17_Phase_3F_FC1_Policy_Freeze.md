---
type: decision
date: "2026-10-17"
status: Phase 3-F F-C1 정책 계약 소유자 승인·문서 동결(FC1_POLICY_CONTRACT_FROZEN_COMMITTED, 로컬 Commit 97045829; 동결 시점에는 UNCOMMITTED) — 실행 데이터 미동결, F-C2-A 구현·검토 대기, F-C2-B/C·F-C3 미착수, Phase 3 전체 Gate INCOMPLETE
related_phase: "03"
tags:
  - star-player
  - decision
  - phase-3-f
---

# 2026-10-17 — Phase 3-F F-C1 검증 정책 계약 동결

> [!warning] 정책 동결 ≠ Gate 통과
> 소유자가 FC-D1~FC-D12 권장 정책을 승인했다(`OWNER_APPROVED`). 실행 데이터(Seed Manifest·셀별 표본 수 등)는 `FINAL_EXECUTION_FREEZE_PENDING`이고, 구현·실행은 하지 않았다. 3-F와 Phase 3 전체 Gate는 `INCOMPLETE`다.

## 출처
- 소유자 `TASK.md` "Star Player Phase 3-F F-C1 Policy Contract Freeze"(기준 HEAD `5492e80`).
- 권위 문서: [ADR-008](../../docs/decisions/ADR-008.md). 해설: [PHASE_3F_FC1_POLICY_PROPOSAL](../../docs/reports/PHASE_3F_FC1_POLICY_PROPOSAL.md) 개정 2. 보고: [PHASE_3F_FC1_POLICY_FREEZE_REPORT](../../docs/reports/PHASE_3F_FC1_POLICY_FREEZE_REPORT.md).

## 결정 요약
| 항목 | 결정 |
|---|---|
| 코호트 | F1·F6·F7·F8 = `neutral-mixed-v1`(F1 코호트 충돌 해소), F2~F5 = `virtual-league-v1`(값 35·40·45·50·50·50·55·60·65) |
| 손잡이 | F1·F8은 same 3/7·opposite 4/7 사전 가중 표준화(자기 분모, 중간 반올림 없음). 계수 평균 1.0 ≠ 결과 중립 |
| 최소 분모 | k 0.25·DEFF 2(검증 안 된 가정)·z 1.959964, 8,158 / 15,297 / 38,239 / 29,964 / 40,906 / 3,043 / 4,795 / 6,229, F1·F8 층별 최소 |
| 실행 규모 | L1~L4 각 ≥ 100,000 PA(층 합산 금지) + 모드별 100경기. ROADMAP 최소보다 엄격 |
| 지배 전략 | 48셀, δ 0.05 runs/half-inning, α 0.05, 검정력 0.8, Holm 비우월성·IUT 우월성, paired 금지, 4상태. A-1 가정은 F-C2 필수 검증 |
| PZL | 12a 필수, 12b `DEFERRED`(BCL Phase 5·6·28, LMV-A Phase 5·28) — Phase 3 Gate 비차단 예외, PASS 기록 금지 |
| 실패 | 1건 = Gate FAIL, 제외 재집계 금지, 변경 시 전체 재실행, v4 직접 수정 금지 |
| Official Mode | Strict CLI·Git Dirty·승인 Hash·Replay — F-C2 구현 계약 |
| 기각/유예 | 실제 Seed 목록·셀별 표본 수·최종 입력 Hash는 Dry Run 이후 F-C3 이전 별도 동결 |
