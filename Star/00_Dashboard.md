---
type: project-dashboard
project: Star Player
current_phase: "03"
phase_status: Phase 0–2 승인 완료 (3/30), Phase 3은 3-A(RNG) 승인(PASS WITH NOTES), 3-B1(구역·규칙 데이터 계약) 승인(PASS WITH NOTES), 3-B2 이후 미착수
last_updated: 2026-10-11
---

# Star Player 개발 대시보드

> [!info] 문서 역할
> 이 Vault는 Phase 리뷰와 개발 히스토리를 관리합니다. 공식 계획과 현재 상태는 각각 [ROADMAP](../ROADMAP.md), [CURRENT_STATE](../CURRENT_STATE.md)가 기준이며, 이 대시보드는 이를 대체하지 않습니다.

## 프로젝트 개요

- PC 싱글 플레이 KBO 선수 커리어 시뮬레이션
- 계획 범위: Phase 0–29, 총 30개 Phase
- 현재 상태: **Phase 0 · Phase 1 · Phase 2 완료·승인 (3/30)**. Phase 2는 2026-10-11 `PASS WITH NOTES` / 승인 완료(상세는 아래 "완료된 Phase"와 [Phase_02_Review](01_Phase_Reviews/Phase_02_Review.md)). 후속 NOTES: C33, C38, C42, C44, D2 정책, Phase 4 엔진 규칙 버전·영속화.
- 현재 개발 중인 Phase: **Phase 3 — 3-A(RNG) PASS WITH NOTES / GPT Chat 승인 (2026-10-11), Codex 최종 검토 PASS WITH NOTES; 3-B1(Pitch Zone Lite 데이터 계약·호출 판정 모드) PASS WITH NOTES / GPT Chat 승인 (2026-10-11), Codex 집중 재검토 PASS WITH NOTES** (3-B2~3-F, Phase 4 미착수; Phase 3 전체 Gate INCOMPLETE)
- 다음 후보: **소유자의 별도 지시로 3-B2 착수 (현재 미착수)**

## 진행 요약

| 구분 | 수량 | 내용 |
|---|---:|---|
| 승인 완료 | 3 | Phase 0, Phase 1, Phase 2 |
| 검토 대기 | 0 | 없음 (Phase 3-A·3-B1은 승인, Phase 3 전체는 미완료) |
| 구현/기록 중 | 1 | Phase 3 (3-A·3-B1 승인 완료, 3-B2~3-F 미착수) |
| 미착수 | 26 | Phase 4–29 |

## Phase 0–29 현황

리뷰 문서는 실제 구현 또는 기록이 시작될 때 `01_Phase_Reviews/Phase_XX_Review.md` 형식으로 생성합니다. 미착수 Phase에는 빈 리뷰 문서를 만들지 않습니다.

| Phase | 이름 | 상태 | 리뷰 |
|---:|---|---|---|
| 0 | Reproducible desktop foundation | 승인 완료 | [Phase_00_Review](01_Phase_Reviews/Phase_00_Review.md) |
| 1 | Player identity, visibility, and baseball vocabulary | 승인 완료 (PASS WITH NOTES) | [Phase_01_Review](01_Phase_Reviews/Phase_01_Review.md) |
| 2 | Baseball state transitions and scoring | 승인 완료 (PASS WITH NOTES, 2026-10-11) | [Phase_02_Review](01_Phase_Reviews/Phase_02_Review.md) |
| 3 | Seeded game simulation / Prototype 1 | 3-A(RNG)·3-B1 PASS WITH NOTES / 승인 — Phase 3 전체 Gate INCOMPLETE, 3-B2 이후 미착수 | [Phase_03_Review](01_Phase_Reviews/Phase_03_Review.md) |
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

- Phase 2 — Baseball state transitions and scoring은 **`PASS WITH NOTES` / 승인 완료 (2026-10-11)**입니다. **최종 승인 (2026-10-11): Phase 2 = `PASS WITH NOTES` / `승인 완료`.** 근거(GPT Chat 최종 검토 결과를 사용자가 전달): xUnit 598개 PASS(Domain 145 · Simulation 260 · Application 114 · Infrastructure 79), 스크립트 경기 130/130(100경기 + C33 30경기), 박스스코어 검산 200/200, `verify.ps1` 연속 2회 `GATE: PASS`, Codex 최종 `PASS WITH NOTES`, 사용자 Windows Export 수동 UI 검증 완료(사용자 보고, 세부 기록 없음), 기존 NOTES와 승인된 GameRules 정책 유지. 승인 주체는 GPT Chat/사용자의 기록된 검토이며 Claude Code가 승인한 것이 아니다. 기존 테스트 결과와 Codex 리뷰 이력은 변경하지 않았다. Phase 3은 **미착수**이며 소유자의 별도 지시 전에는 시작하지 않는다. 커밋·푸시는 별도 허가 전까지 하지 않는다. 후속 NOTES(보존): ① **C33** — 슬롯 장부 설계의 기록된 한계(OBR 9.16(g) 예시 7 동등 시나리오, 모호한 복합 야수선택 `Game.ambiguous_responsibility` 거부는 승인된 지원 범위 제한); ② **C38** — 희생플라이 변형(9.08(d)) 미지원, Gate 비필수 후속 이월, Phase 3 재평가; ③ **C42** — `ReachedOnError` 직접 원인 판정 `NOT_MODELLED`; ④ **C44** — 1루 주자가 있는 안타의 설명 불가 실책 및 실책 2개 이상 거부(입력 표현 한계에 따른 승인된 Phase 2 지원 범위 제한, GPT Chat 설계 판단 승인 · 소유자 직접 승인 별도 기록 없음, KBO/OBR 규칙·인증 아님); ⑤ **D2 정책** — 운영 `GameRules` 부분 집합은 KBO 규정집·기록규칙 미인증 상태로 유지(연장 11회 한도·R02 운영 근거만 확인), 커리어 사용 전 증거 기반 프로필로 교체; ⑥ **Phase 4 과제** — 엔진 규칙 버전(프로필 해시가 규칙 동작 변경을 드러내지 못함)과 `game_states`·`scored_plays`·규칙 프로필 해시 영속화. FHD 확인은 별도 보고가 없어 `미검증`으로 남는다.아래는 검토 이력이며, 이력의 `검토 대기`·Gate 미선언 표기는 당시 상태로 이 승인에 의해 대체됩니다. 이력: **(Codex 최종 리뷰 `PASS WITH NOTES`, 기능 결함 0건 — 주석·RED 증거 추적 요약 `PHASE_2_RED_EVIDENCE.md` 2건 정리, 사용자 수동 UI 검증 전까지 `검토 대기` 유지) (직전 2026-10-11 Codex Medium 보완: 1루 주자가 있는 안타에서 실책 2개 이상은 원인 이동 개수와 무관하게 `Game.hit_error_cause_unclear`로 거부 — 입력 모델 한계에 따른 C44 지원 범위 제한이며 KBO/OBR 규칙이 아님, RED 로그 보존, xUnit 598, verify 1·2차 `GATE: FAIL`(증거 필드명) 후 수정, 연속 2회 PASS, ADR-003 항목 25 C29 대체 표시, Gate INCOMPLETE 유지)** 이전 이력: 1차 자체 검증(414개, verify 연속 2회 PASS) → 독립 Codex 리뷰 **`CHANGES REQUIRED`**(High 3, Medium 1, 끝내기 잔루) → 보완(아웃·순서·포스 상태 도출, 이벤트 영수증/지문, 팀별 DH 상태, 볼 데드 검증, 끝내기 잔루 프로필 규칙) → xUnit 476개, 100경기 100/100 일치, 변이 점검 13건 모두 검출, verify 연속 2회 PASS → 재검토 `CHANGES REQUIRED`(타자주자 안타 후 주루사 High, 어필 Medium) → **최종 보완(2026-10-11)**: 타자주자 도달 전/후 제3아웃 구분 수정, 어필은 사용자 승인으로 범위 제외, KBO 우선·2026 OBR 보완 정책과 규칙 증거 파일(상태 4종) 도입 → xUnit **500개**, 100경기 100/100, verify 연속 2회 PASS. → **R02·OBR 원문 라운드(2026-10-11)**: R02는 소유자 적용 승인(공식 발표+2026 실제 11회 무승부 기록, 규정 조문 미확인)으로 해소, 2026 OBR 원문 PDF를 읽고 38행 대조(일치 16 · 승인된 단순화 9 · 확인 대기 3 · 미해결 편차 6), DH 야수→투수 전환은 OBR 5.11(a)(14) 원문 조항으로 정정 → **OBR 편차 수정 라운드(2026-10-11)**: 편차 6건(C12·C14·C19·C24·C32·C34)을 수정 전 실패 재현 테스트 후 수정, C29 자동 배치 제거(DH Slot 명시 요청만), C33·C38 보고만(승인 보류), 규칙 출처 차단과 구현 결함 분리 관리 → xUnit **540개**, 박스 증명 200/200, 100경기 재생성 100/100 → **C33 최종 기록 책임 라운드(2026-10-11, `TASK.md`)**: OBR 9.16(g) 공식 주석 예시 1~7을 원문 수치로 먼저 작성(옛 엔진에서 17개 중 8개 실패, 로그 보존) 후 책임 슬롯 장부로 수정, 100경기에는 슬롯 이동 득점이 0건이라 이동이 있는 Python 채점 경기 30개를 추가(장부를 끄면 99건 불일치), C38은 소유자 승인으로 Gate 비필수 후속 이월(구현 아님) → xUnit **561개** → **C33 마지막 High 수정(Codex 집중 재검토)**: 서로 다른 투수의 주자 둘 이상이 한 야수선택에서 아웃되면 `Game.ambiguous_responsibility`로 상태 변경 전에 거부(소유자 승인 Phase 2 지원 범위 제한, OBR/KBO 규칙 아님), 수정 전 재현 테스트 실패 확인 → xUnit **568개** → **안타·포스아웃 High 수정(Codex 최종 집중 리뷰)**: 포스아웃이 있는 타구의 안타를 `Game.hit_with_force_out`으로 상태 변경 전 거부(OBR 9.05(b)(1) 첫 절, KBO 인증 아님), 수정 전 재현 테스트 6개 실패 확인, 둘째 절(C43)은 재현만 하고 보고, `ReachedOnError` 한계 명시(C42) → xUnit **578개** → **C43 최종 보완**: `ErrorSavedOut` 주자가 상태상 포스였을 안타를 `Game.hit_with_force_out_saved_by_error`로 거부하고(C43 MATCH), 1루 주자가 있는 안타에 설명되지 않은 실책은 `Game.hit_error_cause_unclear`로 미지원 거부(**C44, Phase 2 승인 지원 범위 제한 — 2026-10-11 GPT Chat 설계 판단으로 승인(사용자 전달, 소유자 직접 승인 별도 기록 없음), KBO 인증·OBR 완전 구현 아님**), 수정 전 재현 실패 로그 보존, C42 유지 → xUnit **590개**, verify 연속 2회 PASS. Gate는 선언하지 않음. 최종 보완 후 Codex 재검토는 미실시. [리뷰](01_Phase_Reviews/Phase_02_Review.md), [보고서](../docs/reports/PHASE_2_REPORT.md), [계약](../docs/game-engine.md), [ADR-003](../docs/decisions/ADR-003.md). 커밋·푸시 없음.
- Phase 1 — Player identity, visibility, and baseball vocabulary, **승인 완료 (PASS WITH NOTES, 2026-10-10)**: 자체 검증 → Codex `CHANGES REQUIRED` → 보완 → Codex 최종 재검토 `PASS WITH NOTES` → GPT Chat 승인(사용자 전달). xUnit 289개, verify 연속 2회 PASS, 수정 후 수동 UI 검증은 사용자 완료 전달(세부 없음). [리뷰](01_Phase_Reviews/Phase_01_Review.md), [보고서](../docs/reports/PHASE_1_REPORT.md), [ADR-002](../docs/decisions/ADR-002.md). 커밋 `64167f7`로 푸시 완료(승인 후 별도 지시).
- Phase 0 — Reproducible desktop foundation, 승인 완료 (2026-10-10, GPT Chat 최종 검토 결과를 사용자가 전달). [리뷰](01_Phase_Reviews/Phase_00_Review.md), [보고서](../docs/reports/PHASE_0_REPORT.md), [검증 로그](../docs/reports/PHASE_0_VERIFY_LOG.md), [1차 검증 로그(보존)](../docs/reports/PHASE_0_VERIFY_LOG_initial.md). Codex 지적 5건 수정 후 `verify.ps1` PASS, Codex 재검토는 미실시.

## 검토 대기 중인 Phase

- 없음 — Phase 3 작업 패키지 3-B1은 2026-10-11 GPT Chat `PASS WITH NOTES` 승인, Codex 집중 재검토 PASS WITH NOTES (전체 xUnit 813 PASS(Claude 실행), 관련 xUnit 104 PASS(Codex 직접 실행), `verify.ps1` 2회 GATE PASS, 새 필수 코드 수정 없음; Phase 3 전체는 아님, Gate INCOMPLETE, 3-B2 미착수). 이력: 3-B1 (Pitch Zone Lite 데이터 계약·호출 판정 모드) — 2026-10-11 구현·자체 검증 완료 후 Codex 리뷰(CHANGES REQUIRED: High 1, Low 1)를 반영한 보완 라운드 완료(전체 xUnit 813 통과, 기준 분포 ID와 가중치를 불변 대응으로 고정, 기존 규칙 프로필 해시 7개 불변; Codex 재검토 미실시). Phase 3-B 전체가 아니며 Gate는 평가하지 않음. 커밋·푸시 없음. [리뷰](01_Phase_Reviews/Phase_03_Review.md), [보고서](../docs/reports/PHASE_3B1_REPORT.md), [ADR-005](../docs/decisions/ADR-005.md).
- Phase 3 작업 패키지 3-A(RNG)는 2026-10-11 GPT Chat `PASS WITH NOTES` 승인, Codex 최종 검토 PASS WITH NOTES (전체 xUnit 709 PASS(Claude 실행), Randomness 111 PASS(Codex 독립 재실행), `verify.ps1` 2회 GATE PASS). Phase 3 전체는 아님, Gate INCOMPLETE. [리뷰](01_Phase_Reviews/Phase_03_Review.md), [보고서](../docs/reports/PHASE_3A_REPORT.md), [ADR-004](../docs/decisions/ADR-004.md).
- Phase 2는 2026-10-11 `승인 완료`(PASS WITH NOTES)로 이 목록에서 제외되었습니다.

## 추적 중인 기술 부채

- Phase 2 후속 NOTES(2026-10-11 승인 시 보존): C33 기록된 한계(예시 7 동등 시나리오, 모호한 복합 야수선택 거부), C38 희생플라이 변형 미지원 이월(Phase 3 재평가), C42 `ReachedOnError` 원인 `NOT_MODELLED`, C44 지원 범위 제한(승인된 Phase 2 지원 범위 제한, KBO/OBR 규칙 아님), D2 운영 `GameRules` 비인증, Phase 4 엔진 규칙 버전·`game_states`/`scored_plays`/규칙 프로필 해시 영속화, FHD 미검증.
- ~~Domain 컬렉션 가변성~~ — Phase 2-A에서 해결(방어 복사, `AsReadOnly()`, 변이 테스트 7개).
- Phase 2: 문서화된 득점 단순화(RBI, 자책 재구성 관례, 승/패/세이브, 끝내기 잔루)는 공식 KBO 규칙과 대조되지 않음; 아웃·득점 순서는 기록원 입력(엔진은 일관성만 검사); DH 소멸 전환(야수의 투수 전환)은 OBR 5.11(a)(14) 원문 조항으로 정정(KBO 미인증), 어필 플레이는 승인된 범위 제외, R02는 적용 승인(조문 미확인), OBR 편차 6건과 C33 승계 주자 책임은 수정 완료(C38 희생플라이 변형은 Gate 비필수 후속 이월, 구현 아님); Python 오라클은 동일 작성자·동일 명세 해석이며 100경기 스위트가 드문 규칙을 모두 다루지는 않음(손 오라클이 보완); `ApproachKind`는 Phase 3 전까지 효과 없음; 사람 수동 클릭 검증·FHD·보완 후 Codex 재검토 미실시.

## 주요 선행 조건과 미해결 항목

- Phase 3-B2 이후와 Phase 4 이상은 소유자의 별도 구현 지시가 필요합니다 (Phase 0–2 승인 완료; Phase 3은 3-A와 3-B1만 착수). D2 `GameRules` 운영 부분 집합은 KBO 규정집·기록규칙 확보 후 인증해야 합니다(현재 비인증 프로필; 연장 11회 한도와 R02 운영 근거만 확인).
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
- Phase 3-B1 구현 결정: [ADR-005](../docs/decisions/ADR-005.md) — 구역 모델 `pzl-zones-v1`(17구역, 경계 분류 표는 구역의 함수), 타석 방향 규칙, 숨김·유계·평균 중립 타자 성향 벡터, `CalledPitchMode`와 `gamerules-v2`(구형 프로필은 미선언·해시 불변) (검토 요청; Codex 검토 권장).
- Phase 3-A 구현 결정: [ADR-004](../docs/decisions/ADR-004.md) — PCG32 `pcg32-xsh-rr-v1`, SHA-256 `kd1` 스트림 유도, 6개 `game/*` 스트림(`game/location@1` 규약 포함), `rngset1` 스냅샷, 원자적 작업 범위, 결정적 ID (검토 요청; Codex 검토 권장).
- Phase 2 구현 결정: [ADR-003](../docs/decisions/ADR-003.md) (Codex 보완 10–16, 최종 보완 17–22, R02·OBR 원문 라운드 23–26, 편차 수정 라운드 27–32 포함: 타자주자 도달 전/후, 어필 제외, KBO 우선·OBR 보완 정책, 아웃 분류의 상태 도출, 이벤트 영수증, DH 상태 분리, 끝내기 잔루 규칙) — 입력 구동 순수 리듀서, D1 단계를 상태에 포함, 단일 득점 경로(`FinishPlay`)와 이닝 종료 시 자책 재구성, 추가 전용 증거 기반 기록·1회 확정, 불변 규칙 스냅샷과 비인증 KBO 지향 프로필 표기, 독립 Python 오라클 (검토 요청).
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

