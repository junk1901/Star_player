---
type: project-dashboard
project: Star Player
current_phase: none
phase_status: 구현 시작 전
last_updated: 2026-10-10
---

# Star Player 개발 대시보드

> [!info] 문서 역할
> 이 Vault는 Phase 리뷰와 개발 히스토리를 관리합니다. 공식 계획과 현재 상태는 각각 [ROADMAP](../outputs/ROADMAP.md), [CURRENT_STATE](../outputs/CURRENT_STATE.md)가 기준이며, 이 대시보드는 이를 대체하지 않습니다.

## 프로젝트 개요

- PC 싱글 플레이 KBO 선수 커리어 시뮬레이션
- 계획 범위: Phase 0–29, 총 30개 Phase
- 현재 상태: **개발 Phase 미착수**
- 현재 개발 중인 Phase: **없음**
- 다음 후보: **Phase 0 — 별도 소유자 승인 후 시작**

## 진행 요약

| 구분 | 수량 | 내용 |
|---|---:|---|
| 승인 완료 | 0 | 완료·승인된 개발 Phase 없음 |
| 검토 대기 | 0 | 외부 검토 대기 중인 Phase 없음 |
| 구현/기록 중 | 0 | 현재 진행 중인 Phase 없음 |
| 미착수 | 30 | Phase 0–29 |

## Phase 0–29 현황

리뷰 문서는 실제 구현 또는 기록이 시작될 때 `01_Phase_Reviews/Phase_XX_Review.md` 형식으로 생성합니다. 미착수 Phase에는 빈 리뷰 문서를 만들지 않습니다.

| Phase | 이름 | 상태 | 리뷰 |
|---:|---|---|---|
| 0 | Reproducible desktop foundation | 미착수 | 미생성 |
| 1 | Player identity, visibility, and baseball vocabulary | 미착수 | 미생성 |
| 2 | Baseball state transitions and scoring | 미착수 | 미생성 |
| 3 | Seeded game simulation / Prototype 1 | 미착수 | 미생성 |
| 4 | Datapack initialization and safe save spine | 미착수 | 미생성 |
| 5 | One-player career loop / Prototype 2 | 미착수 | 미생성 |
| 6 | Calendar, regular season, and basic discovery | 미착수 | 미생성 |
| 7 | Development, aging, training, and role proficiency | 미착수 | 미생성 |
| 8 | Injury, recovery, and rehabilitation eligibility | 미착수 | 미생성 |
| 9 | Club AI, trust, and legal selection | 미착수 | 미생성 |
| 10 | Ten-club world and real second-team play | 미착수 | 미생성 |
| 11 | Complete season lifecycle / Prototype 3 | 미착수 | 미생성 |
| 12 | Feeder cohorts and uncertain scouting | 미착수 | 미생성 |
| 13 | Draft and professional entry paths | 미착수 | 미생성 |
| 14 | Contracts, agents, and earnings ledger | 미착수 | 미생성 |
| 15 | FA eligibility and multi-club offers | 미착수 | 미생성 |
| 16 | Transfers, releases, foreign-player intake, and NPC exits | 미착수 | 미생성 |
| 17 | Twenty-season world stability | 미착수 | 미생성 |
| 18 | National selection and international competitions | 미착수 | 미생성 |
| 19 | Military career and sports-team assignment | 미착수 | 미생성 |
| 20 | Assets, spending, and lifestyle time | 미착수 | 미생성 |
| 21 | Relationships, family, and structured stories | 미착수 | 미생성 |
| 22 | Advertising, sponsors, and agent marketing | 미착수 | 미생성 |
| 23 | Historical continuity and advanced metrics | 미착수 | 미생성 |
| 24 | Complete player, team, and amateur discovery | 미착수 | 미생성 |
| 25 | Retirement, franchise legacy, and Hall of Fame | 미착수 | 미생성 |
| 26 | Narrative AI and AI-native interaction | 미착수 | 미생성 |
| 27 | Complete datapack tools and content certification | 미착수 | 미생성 |
| 28 | Alpha visual system and complete UX | 미착수 | 미생성 |
| 29 | Polish, export, recovery, and final long-save validation | 미착수 | 미생성 |

## 완료된 Phase

없음.

## 검토 대기 중인 Phase

없음.

## 주요 선행 조건과 미해결 항목

- Phase 0 시작 전 소유자의 별도 구현 승인이 필요합니다.
- 각 소비 Phase는 구현 전에 필요한 KBO RuleProfile 상세 규칙과 명시적 단순화를 확정해야 합니다.
- 개인 KBO 2026 팩은 Phase 27에서 정확한 `as_of_date`, 증거 기준일, 제공 데이터와 출처를 인증해야 합니다.
- 실제 툴체인과 데스크톱 내보내기 가능성은 Phase 0에서 검증해야 합니다.

별도 이슈 문서가 생기면 `03_Issues/`에 기록하고 이 구역에서 링크합니다.

## 최근 기술·제품 결정

- D1–D6 소유자 결정은 모두 승인되었습니다.
- 신규 선수의 정규 커리어는 Draft / Entry 흐름을 사용합니다.
- KBO 규칙은 커리어에 동결되는 버전형 2026 KBO 지향 RuleProfile과 명시적 단순화를 사용합니다.
- 장기 기록은 우선 보존하고 Phase 17/29 실측 후 필요한 경우에만 제한적으로 정리합니다.
- Club Finance Lite가 Scope Lock의 단일 승인 예외로 추가되었습니다: C13 계약 기반 급여, KBO 샐러리캡, 성과형 구단 스폰서 수익, 규칙 기반 Club AI 연동, 읽기 전용 재정 화면.
- 이 예외 반영 후 게임플레이 Scope Lock은 다시 활성화되었으며, 새 기능은 별도 승인 없이 추가하지 않습니다.

상세 근거: [ROADMAP — Approved Owner Decisions](../outputs/ROADMAP.md#approved-owner-decisions)

결정 기록: [Club Finance Lite 승인 및 Scope Lock 재활성화](02_Decisions/2026-10-10_Club_Finance_Lite.md)

## 리뷰 및 기록 탐색

- Phase 리뷰: `01_Phase_Reviews/`
- 기술 결정: `02_Decisions/`
- 미해결 문제·버그·기술 부채: `03_Issues/`
- 개발 노트·시행착오: `04_Dev_Notes/`
- 표준 템플릿: [Phase Review Template](99_Templates/Phase_Review_Template.md)

## 주요 개발 문서

- [ROADMAP.md](../outputs/ROADMAP.md) — 공식 개발 계획과 Phase 완료 기준
- [CURRENT_STATE.md](../outputs/CURRENT_STATE.md) — 공식 현재 상태
- [AGENTS.md](../outputs/AGENTS.md) — AI 작업 및 Phase 종료 규칙
- [SYSTEM_CONTRACTS.md](../outputs/docs/SYSTEM_CONTRACTS.md) — 시스템 소유권과 계약
- [UI_ARCHITECTURE.md](../outputs/docs/UI_ARCHITECTURE.md) — UI 구조와 화면 계약
- [REQUIREMENTS_TRACEABILITY.md](../outputs/docs/REQUIREMENTS_TRACEABILITY.md) — 요구사항 추적성

## 운영 원칙

- 리뷰 상세는 Markdown 파일에 남기고 채팅 보고는 간결하게 유지합니다.
- 실행하지 않은 테스트는 통과로 기록하지 않습니다.
- GPT Chat 또는 사용자 승인 전에는 상태를 `승인 완료`로 바꾸지 않습니다.
- Phase 리뷰 생성만으로 다음 Phase를 자동 시작하지 않습니다.
- 공식 설계가 바뀌면 공식 문서를 먼저 정합하게 수정한 뒤 리뷰에서 변경 이유를 연결합니다.

