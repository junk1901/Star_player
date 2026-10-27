---
type: decision
date: 2026-10-28
phase: "3-F B-1"
status: 결정됨
tags:
  - star-player
  - decision
---

# B-1 G4 코드 단계 수용과 운영 정책 방향(후보)

정본: OWNER 지시 "G4 구현 수용 및 Phase 3 잔여 작업 자율 진행"(2026-10-28). 원문은 저장소 밖 `E:\Madegame\g3x\owner-originals-post-o19\P19-14_…`(`c993976f…`)에 보존했으며, 이 노트는 요약입니다. 저장소 편입은 OWNER 결정 A1-4입니다.

- **수용:** G4 구현을 **코드 단계**에서 수용합니다.
  - 검증 소스 `a980ea5`, 문서 `20ec650`
  - Codex 최종 READ-ONLY PASS(원문 `codex-originals-g4/20261027T164628Z_…`, `2f149042…`)
  - G4-FIX-01·02 RESOLVED, Clean Checkout 3,061 PASS, Golden 4/4, Dry Pilot 9/9
- **승인하지 않은 것:** 공식 운영 G4 PASS, 인증 성공, Build Freeze, Phase 3 완료
- **운영 정책 방향(규범 후보, 실측값 미확정):**
  - TRX `RunInfo` Warning을 인프라 실패로 거부
  - Windows·공유 런타임 파일을 정확한 경로+SHA로 Pin
  - 경로·Root만으로 비Pin 코드 허용 금지
  - 동적 어셈블리 허용 목록은 기본 빈 목록
  - R-WORK는 TEMP가 아닌 별도 보호 Root
  - 외부 Pin·보호 설치 설정은 제출자가 변경 불가
- **반영 문서:** [G5 운영 설계](../../docs/tasks/B1_G5_OPERATIONAL_DESIGN_DRAFT.md), [A-1 게시 준비](../../docs/tasks/B1_A1_PUBLICATION_PREP.md), [운영 Gate 준비표](../../docs/tasks/B1_OPERATIONAL_GATE_READINESS.md), [준비 보고서](../../docs/reports/B1_G5_A1_PREP_REPORT.md)
- 상태: Official Execution = NOT_READY · Final Execution Freeze = PENDING · Phase 3 = INCOMPLETE
