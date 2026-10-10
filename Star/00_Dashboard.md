---
type: project-dashboard
project: Star Player
current_phase: none
phase_status: Phase 0–1 승인 완료 (2/30), Phase 2 미착수
last_updated: 2026-10-10
---

# Star Player 개발 대시보드

> [!info] 문서 역할
> 이 Vault는 Phase 리뷰와 개발 히스토리를 관리합니다. 공식 계획과 현재 상태는 각각 [ROADMAP](../ROADMAP.md), [CURRENT_STATE](../CURRENT_STATE.md)가 기준이며, 이 대시보드는 이를 대체하지 않습니다.

## 프로젝트 개요

- PC 싱글 플레이 KBO 선수 커리어 시뮬레이션
- 계획 범위: Phase 0–29, 총 30개 Phase
- 현재 상태: **Phase 0 · Phase 1 완료·승인 (2/30, 2026-10-10), Phase 2 미착수**
- 현재 개발 중인 Phase: **없음**
- 다음 후보: **Phase 2 — 소유자의 별도 지시 후 시작 (컬렉션 가변성 후속 조치 선행)**

## 진행 요약

| 구분 | 수량 | 내용 |
|---|---:|---|
| 승인 완료 | 2 | Phase 0, Phase 1 |
| 검토 대기 | 0 | 외부 검토 대기 중인 Phase 없음 |
| 구현/기록 중 | 0 | 현재 진행 중인 Phase 없음 |
| 미착수 | 28 | Phase 2–29 |

## Phase 0–29 현황

리뷰 문서는 실제 구현 또는 기록이 시작될 때 `01_Phase_Reviews/Phase_XX_Review.md` 형식으로 생성합니다. 미착수 Phase에는 빈 리뷰 문서를 만들지 않습니다.

| Phase | 이름 | 상태 | 리뷰 |
|---:|---|---|---|
| 0 | Reproducible desktop foundation | 승인 완료 | [Phase_00_Review](01_Phase_Reviews/Phase_00_Review.md) |
| 1 | Player identity, visibility, and baseball vocabulary | 승인 완료 (PASS WITH NOTES) | [Phase_01_Review](01_Phase_Reviews/Phase_01_Review.md) |
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

- Phase 1 — Player identity, visibility, and baseball vocabulary, **승인 완료 (PASS WITH NOTES, 2026-10-10)**: 자체 검증 → Codex `CHANGES REQUIRED` → 보완 → Codex 최종 재검토 `PASS WITH NOTES` → GPT Chat 승인(사용자 전달). xUnit 289개, verify 연속 2회 PASS, 수정 후 수동 UI 검증은 사용자 완료 전달(세부 없음). [리뷰](01_Phase_Reviews/Phase_01_Review.md), [보고서](../docs/reports/PHASE_1_REPORT.md), [ADR-002](../docs/decisions/ADR-002.md). 커밋·푸시 없음.
- Phase 0 — Reproducible desktop foundation, 승인 완료 (2026-10-10, GPT Chat 최종 검토 결과를 사용자가 전달). [리뷰](01_Phase_Reviews/Phase_00_Review.md), [보고서](../docs/reports/PHASE_0_REPORT.md), [검증 로그](../docs/reports/PHASE_0_VERIFY_LOG.md), [1차 검증 로그(보존)](../docs/reports/PHASE_0_VERIFY_LOG_initial.md). Codex 지적 5건 수정 후 `verify.ps1` PASS, Codex 재검토는 미실시.

## 검토 대기 중인 Phase

없음.

## 추적 중인 기술 부채

- Domain 컬렉션 가변성(Codex Medium, Notes): `Ratings.cs`(`PitchRepertoire.Pitches`), `Proficiency.cs`, `PlayerStatus.cs`, `DevelopmentProfile.cs`, `Player.cs`가 `List` 기반 `IReadOnlyList`를 그대로 노출. **Phase 2에서 이 컬렉션을 경기 상태·결정론적 처리에 쓰기 전에** 방어 복사, 읽기 전용(`AsReadOnly`) 노출, 캐스팅 변이 테스트(`IList` 캐스트 후 변경 시 예외)를 적용한다. 현재 승인된 Phase 1 코드는 수정하지 않는다.

## 주요 선행 조건과 미해결 항목

- Phase 2 이상은 소유자의 별도 구현 지시가 필요합니다 (Phase 0–1 승인 완료). Phase 2는 구현 전에 `GameRules` 상세 부분 집합(D2)이 필요합니다.
- 각 소비 Phase는 구현 전에 필요한 KBO RuleProfile 상세 규칙과 명시적 단순화를 확정해야 합니다.
- 개인 KBO 2026 팩은 Phase 27에서 정확한 `as_of_date`, 증거 기준일, 제공 데이터와 출처를 인증해야 합니다.
- 툴체인(.NET 8.0.425, Godot 4.7.2 Mono)과 Windows Export 가능성은 Phase 0에서 검증되었습니다 — [toolchain](../docs/toolchain.md).

별도 이슈 문서가 생기면 `03_Issues/`에 기록하고 이 구역에서 링크합니다.

## 최근 기술·제품 결정

- D1–D6 소유자 결정은 모두 승인되었습니다.
- 신규 선수의 정규 커리어는 Draft / Entry 흐름을 사용합니다.
- KBO 규칙은 커리어에 동결되는 버전형 2026 KBO 지향 RuleProfile과 명시적 단순화를 사용합니다.
- 장기 기록은 우선 보존하고 Phase 17/29 실측 후 필요한 경우에만 제한적으로 정리합니다.
- Club Finance Lite가 Scope Lock의 단일 승인 예외로 추가되었습니다: C13 계약 기반 급여, KBO 샐러리캡, 성과형 구단 스폰서 수익, 규칙 기반 Club AI 연동, 읽기 전용 재정 화면.
- Phase 1 구현 결정: [ADR-002](../docs/decisions/ADR-002.md) — 숨김 데이터 구조적 제외, 신뢰된 바인딩이 조작 선수 결정(옵션·쿼리로 재정의 불가), 원본 세계 `internal` 격리(Codex 보완 8–11), 치트 모드도 잠재력 비노출·NPC 현재값 비확대, `observation-v0` 임시 관측 모델, 가상 팩 내장, `ClubId` 추가 (검토 요청).
- Phase 0 구조 결정: [ADR-001](../docs/decisions/ADR-001.md) — 계층 경계(Client는 Domain/Simulation 직접 참조 금지), Godot 클라이언트 전용 `.sln`, 진단 실행 모드, Codex 지적 반영.
- 이 예외 반영 후 게임플레이 Scope Lock은 다시 활성화되었으며, 새 기능은 별도 승인 없이 추가하지 않습니다.

상세 근거: [ROADMAP — Approved Owner Decisions](../ROADMAP.md#approved-owner-decisions)

결정 기록: [Club Finance Lite 승인 및 Scope Lock 재활성화](02_Decisions/2026-10-10_Club_Finance_Lite.md)

## 리뷰 및 기록 탐색

- Phase 리뷰: `01_Phase_Reviews/`
- 기술 결정: `02_Decisions/`
- 미해결 문제·버그·기술 부채: `03_Issues/`
- 개발 노트·시행착오: `04_Dev_Notes/`
- 표준 템플릿: [Phase Review Template](99_Templates/Phase_Review_Template.md)

## 주요 개발 문서

- [ROADMAP.md](../ROADMAP.md) — 공식 개발 계획과 Phase 완료 기준
- [CURRENT_STATE.md](../CURRENT_STATE.md) — 공식 현재 상태
- [AGENTS.md](../AGENTS.md) — AI 작업 및 Phase 종료 규칙
- [SYSTEM_CONTRACTS.md](../docs/SYSTEM_CONTRACTS.md) — 시스템 소유권과 계약
- [UI_ARCHITECTURE.md](../docs/UI_ARCHITECTURE.md) — UI 구조와 화면 계약
- [REQUIREMENTS_TRACEABILITY.md](../docs/REQUIREMENTS_TRACEABILITY.md) — 요구사항 추적성

## 운영 원칙

- 리뷰 상세는 Markdown 파일에 남기고 채팅 보고는 간결하게 유지합니다.
- 실행하지 않은 테스트는 통과로 기록하지 않습니다.
- GPT Chat 또는 사용자 승인 전에는 상태를 `승인 완료`로 바꾸지 않습니다.
- Phase 리뷰 생성만으로 다음 Phase를 자동 시작하지 않습니다.
- 공식 설계가 바뀌면 공식 문서를 먼저 정합하게 수정한 뒤 리뷰에서 변경 이유를 연결합니다.

