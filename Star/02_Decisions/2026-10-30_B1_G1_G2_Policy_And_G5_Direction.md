---
type: decision
date: 2026-10-30
phase: "3-F B-1"
status: 결정됨
tags:
  - star-player
  - decision
---

# B-1 G1/G2 정책 채택과 G5-TEMP-01 운영 방향

정본: OWNER 지시 "G1/G2 정책 확정 및 G5-MODEL-01/02 후속 조치"(2026-10-30, 채팅 지시). 이 노트는 요약입니다.

- **Codex 판정(입력):** 최종 FIX_REQUIRED · G2 E-1 ACCEPTABLE · G1-POLICY-01 RESOLVED · G5-TEMP-01 OPEN_MAJOR · G5-MODEL-01 MAJOR · G5-MODEL-02 MINOR
- **결정:**
  1. G2 = **E-1** 채택. "Checkout 설정과 무관한 같은 바이트"는 보장하지 않음, 301개 SourceLink 체크섬 차이는 알려진 제한
  2. G1 = **G1-a** 채택. 감사 도구 SHA `d3945b5e…6ba0`, 대상 `c77ed1a`·Tree `a7b9b937…`. 도구는 다시 수정하지 않음
  3. 공식 제품 소스 후보 `c77ed1a` 유지
  4. G5 운영 해결은 **로컬 S4U 전용 계정**을 우선 검토
  5. **gMSA 제외**(이번 운영 경로)
  6. C-3은 보완 설계로 보존, **구현하지 않음**
- **승인하지 않은 것:** 운영 G1~G9 PASS, 최종 Build Freeze, G5-TEMP-01 해소, 계정·작업·ACL 운영 적용
- **반영 문서:** [G1·G2 결정안 C절](../../docs/tasks/B1_G1_G2_CONDITIONAL_DECISION_PROPOSAL.md), [G5-TEMP-01 보호 모델](../../docs/tasks/B1_G5_TEMP01_PROTECTION_MODEL.md), [S4U 실행기 구성안](../../docs/tasks/B1_G5_S4U_LAUNCHER_PLAN.md), [보고서](../../docs/reports/B1_G5_MODEL_FIX_REPORT.md)
- 상태: Official Execution = NOT_READY · Final Execution Freeze = PENDING · Phase 3 = INCOMPLETE
