---
type: phase-review
phase: "00"
phase_name: ""
status: 초안
started: ""
completed: ""
developer_ai: Claude Code
reviewed_by: ""
branch: ""
commit: ""
roadmap_anchor: ""
last_updated: "{{date:YYYY-MM-DD}}"
tags:
  - star-player
  - phase-review
---

# Phase 00 Review — Phase 이름

> [!warning] 상태 규칙
> 허용 상태는 `초안`, `검토 대기`, `승인 완료`, `보완 필요`입니다. 구현 AI는 외부 검토 없이 `승인 완료`로 변경하지 않습니다. 실행하지 않은 검증은 `미검증`, 수행하지 않은 독립 리뷰는 `미실시`로 기록합니다.

## A. 기본 정보

| 항목 | 내용 |
|---|---|
| Phase | Phase 00 — Phase 이름 |
| 작업 시작일 |  |
| 작업 완료일 |  |
| 개발 담당 AI | Claude Code |
| 리뷰 상태 | 초안 |
| 관련 ROADMAP | [Phase 항목](../../outputs/ROADMAP.md) |
| 관련 Git 브랜치 |  |
| 관련 커밋 |  |

- 대시보드: [Star Player 개발 대시보드](../00_Dashboard.md)
- 공식 현재 상태: [CURRENT_STATE.md](../../outputs/CURRENT_STATE.md)

## B. 개발 목표

### 개발 목적


### 구현 범위

- 

### 완료 기준

- [ ] ROADMAP Acceptance Criteria 1
- [ ] Common Gate G 적용 항목

### 관련 요구사항 및 시스템 계약

- 요구사항: [REQUIREMENTS_TRACEABILITY.md](../../outputs/docs/REQUIREMENTS_TRACEABILITY.md)
- 시스템 계약: [SYSTEM_CONTRACTS.md](../../outputs/docs/SYSTEM_CONTRACTS.md)
- 관련 계약/요구사항 ID:

## C. 실제 구현 내역

### 구현한 기능

- 

### 주요 변경 파일

| 파일 | 변경 내용 |
|---|---|
|  |  |

### 새 모듈과 기존 모듈 변경

- 새 모듈:
- 수정한 기존 모듈:

### 계획 대비 달라진 내용

- 없음 / 내용과 이유 기록

## D. 테스트 및 검증

### 실행 환경

| 항목 | 값 |
|---|---|
| OS |  |
| Godot |  |
| .NET SDK |  |
| 빌드 구성 |  |

### 실제 실행 결과

| 명령 | 기대 결과 | 실제 결과 | 상태 | 로그·결과 파일 |
|---|---|---|---|---|
|  |  |  | 미검증 |  |

### 완료 기준별 충족 여부

| 완료 기준 | 상태 | 근거 |
|---|---|---|
|  | 미검증 |  |

### 테스트하지 못한 항목

- 없음 / 미검증 항목과 이유 기록

## E. 발견된 문제

### 발생한 오류

- 없음

### 해결한 문제

- 없음

### 미해결 문제

- 없음

### 기술 부채 및 향후 영향

- 없음

관련 이슈가 있으면 `../03_Issues/` 문서를 연결합니다.

## F. 설계 변경

| 변경 내용 | 변경 이유 | 영향 모듈 | 결정 문서 | Codex 추가 검토 |
|---|---|---|---|---|
| 없음 |  |  |  | 미실시 |

중요 설계 변경은 `../02_Decisions/`에 별도 기록하고 공식 설계 문서를 먼저 갱신합니다.

## G. AI 리뷰

### GPT Chat 리뷰

| 항목 | 결과 |
|---|---|
| 기능명세 충족 여부 | 미실시 |
| 누락된 요구사항 | 미실시 |
| 개선 권장사항 | 미실시 |
| 최종 검토 의견 | 미실시 |

### Codex 리뷰 (선택적)

| 항목 | 결과 |
|---|---|
| 아키텍처 적합성 | 미실시 |
| 모듈 간 의존성 | 미실시 |
| 중요 로직 검증 | 미실시 |
| 구조적 위험 요소 | 미실시 |

## H. 다음 Phase 인수인계

### 시작 전 확인 사항

- 

### 의존 관계

- 

### 주의할 문제 및 후속 작업

- 

### 추가 검증 필요 사항

- 

## I. 최종 결과

| 항목 | 결과 |
|---|---|
| 완료 기준 충족 여부 | 미검증 |
| 최종 리뷰 상태 | 초안 |
| 남은 문제 |  |
| 승인자 또는 승인 근거 | 미승인 |
| 최종 수정 날짜 | {{date:YYYY-MM-DD}} |

### Phase 종료 체크

- [ ] ROADMAP 완료 기준과 Common Gate G 확인
- [ ] 실제 구현·변경 파일과 Git 변경사항 기록
- [ ] 실행한 테스트의 명령·환경·실제 결과 기록
- [ ] 미검증 항목, 미해결 문제와 설계 변경 식별
- [ ] 중요한 설계 변경의 결정 문서 연결
- [ ] 리뷰 상태를 `검토 대기`로 변경
- [ ] [CURRENT_STATE.md](../../outputs/CURRENT_STATE.md)와 실제 상태 일치 확인
- [ ] GPT Chat 전달용 간략 요약 작성
- [ ] 다음 Phase를 자동 시작하지 않음

