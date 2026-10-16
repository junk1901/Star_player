---
type: project-dashboard
project: Star Player
current_phase: "03"
phase_status: 3-C3-C 기술 검증 완료(테스트 63건·Mutation 36건 34 KILLED/2 SURVIVED·xUnit 1,804 PASS·verify 2회 PASS·증거 보존) 및 Closeout 문서 작성·최종 정합화 완료(2026-10-15, 문서만) — Codex 독립 리뷰 CODEX_C3C_REVIEW_PASS_WITH_NOTES(BLOCKER 0/MAJOR 0/MINOR 1 정정 완료), 모델 예외 MODEL_DEVIATION_OWNER_ACCEPTED, 로컬 Commit 소유자 결정 대기, PHASE_3_GATE_INCOMPLETE, Push 금지; 이전 기록 — 3-C3-B 구현 완료·PASS WITH NOTES(로컬 커밋 8ed806f, 푸시 안 함), C3-C OD-1~OD-4 소유자 승인(수행 아님), C3-C-0 문서 Closeout 반영(2026-10-14, 문서만; C3-C 통합 테스트·변이 재실행·Codex 리뷰 NOT STARTED / NOT AUTHORIZED, Phase 3 Gate INCOMPLETE, Push는 Phase 3 최종 Gate 후 별도 승인), 이전 기록 — 3-C3-B 세부 계약 B-01~B-07 승인 반영(2026-10-13, 문서만 `OWNER APPROVED`; C3-A 구현 완료·GPT Chat 기술 검토 PASS WITH NOTES·로컬 커밋 18ed60e, C3-B 프로덕션·C3-C NOT STARTED / NOT AUTHORIZED, Phase 3 Gate INCOMPLETE, Push는 Phase 3 최종 Gate 후 별도 승인), 이전 기록 — 3-C3-A 구현 완료·검토 대기(2026-10-13, 자체 판정 C3A_IMPLEMENTATION_READY_FOR_REVIEW; GPT Chat 기술 검토 GPT_CHAT_TECHNICAL_PASS_WITH_NOTES(소유자 전달, C3-A 한정), Codex 미실시, C3-B·C3-C·Phase 4 NOT STARTED / NOT AUTHORIZED, Phase 3 Gate INCOMPLETE), 이전 기록 — 3-C3-A 세부 계약 O-1~O-9 승인 반영(2026-10-13, 문서만 `OWNER APPROVED`; C3-A 프로덕션·C3-B·C3-C NOT STARTED / NOT AUTHORIZED, Phase 3 Gate INCOMPLETE), 이전 기록 — 3-C3 설계 승인 반영(2026-10-13, D-1~D-8·계약 A~F `OWNER APPROVED` 설계만; C3-A/B/C·C3 구현 NOT STARTED / NOT AUTHORIZED, Phase 3 Gate INCOMPLETE), 이전 기록 — 3-C2B-3(`advancement` 해결기) 구현 완료·검토 대기(2026-10-12, 자체 판정 PASS WITH NOTES; 승인 아님, GPT Chat·Codex 미실시, 3-C3 미착수, Phase 3 Gate INCOMPLETE), 이전 기록 — 3-C2B-2(기본 인플레이 계획·끝내기 공식 기록·PendingAdvancement 격리·인플레이 후보) 최종 승인 완료(2026-10-12, Codex Medium PASS / GPT Chat 최종 PASS; 3-C2B-2만, Push 미승인), 이하 기존 기록 — Phase 0–2 승인 완료 (3/30), Phase 3은 3-A(RNG)·3-B1(구역·규칙 데이터 계약) 승인(PASS WITH NOTES), 3-B2(확률·튜닝 계약) 승인(PASS WITH NOTES, Codex 최종 PASS), 3-C1(이벤트 위치·ef2·JSON v2) 승인(PASS WITH NOTES, Codex 최종 PASS WITH NOTES), 3-C2A(단일 투구 확률 분기·game/target@1) 승인(PASS WITH NOTES, Codex 최종 집중 재검토 PASS WITH NOTES; 3-C2A만), 3-C2B-1(준비된 투구·비인플레이 이벤트 후보) 승인(PASS WITH NOTES, Codex PASS WITH NOTES; 3-C2B-1만), 3-C2B-2-P(D1–D5 정책 확정·템플릿 실현성 검증; 구현 아님) 검증 완료·GPT Chat PASS WITH NOTES(검증 한정; Codex 미실시), Phase 3 전체 Gate INCOMPLETE, 3-C2B-3·3-C3 미착수
last_updated: 2026-10-16
---

# Star Player 개발 대시보드

> [!info] 문서 역할
> 이 Vault는 Phase 리뷰와 개발 히스토리를 관리합니다. 공식 계획과 현재 상태는 각각 [ROADMAP](../ROADMAP.md), [CURRENT_STATE](../CURRENT_STATE.md)가 기준이며, 이 대시보드는 이를 대체하지 않습니다.

## 프로젝트 개요

- PC 싱글 플레이 KBO 선수 커리어 시뮬레이션
- 계획 범위: Phase 0–29, 총 30개 Phase
- 현재 상태: **Phase 0 · Phase 1 · Phase 2 완료·승인 (3/30)**. Phase 2는 2026-10-11 `PASS WITH NOTES` / 승인 완료(상세는 아래 "완료된 Phase"와 [Phase_02_Review](01_Phase_Reviews/Phase_02_Review.md)). 후속 NOTES: C33, C38, C42, C44, D2 정책, Phase 4 엔진 규칙 버전·영속화.
- 현재 개발 중인 Phase: **Phase 3 — 3-A(RNG) PASS WITH NOTES / GPT Chat 승인 (2026-10-11), Codex 최종 검토 PASS WITH NOTES; 3-B1(Pitch Zone Lite 데이터 계약·호출 판정 모드) PASS WITH NOTES / GPT Chat 승인 (2026-10-11), Codex 집중 재검토 PASS WITH NOTES** (3-B2 정수 확률·가상 튜닝 계약은 2026-10-11 구현·자체 검증 완료, 검토 대기 — 승인 아님; 3-C~3-F, Phase 4 미착수; Phase 3 전체 Gate INCOMPLETE)
- 다음 후보: **3-C2A(단일 투구 확률 분기·`game/target@1`)는 2026-10-11 GPT Chat `PASS WITH NOTES` / 최종 승인, Codex 최종 집중 재검토 `PASS WITH NOTES`(기존 Codex `CHANGES REQUIRED` Medium 1: GameId↔RNG ContextKey 불일치는 해결·재검증, 이력 보존; 전체 xUnit 1,113 PASS, verify 2회 GATE PASS — 소유자 기록 인용). 승인 범위는 3-C2A뿐이다. 3-C2B·3-C3는 소유자의 별도 지시로만 착수 (현재 미착수); 3-C1 승인 사실과 유지 NOTES는 CURRENT_STATE 참조**

## 진행 요약

| 구분 | 수량 | 내용 |
|---|---:|---|
| 승인 완료 | 3 | Phase 0, Phase 1, Phase 2 |
| 검토 대기 | 0 | 상위 Phase 단위로는 없음 (Phase 3-A·3-B1·3-B2·3-C1 작업 패키지는 승인, 3-C2A 작업 패키지도 승인(2026-10-11), 3-C2B-1 작업 패키지도 승인(2026-10-11), 3-C2B-2 작업 패키지도 승인(2026-10-12), Phase 3 전체는 미완료; 상위 status는 `검토 대기` 유지) |
| 구현/기록 중 | 1 | Phase 3 (3-A·3-B1·3-B2·3-C1·3-C2A 승인 완료, 3-C2B-1 승인 완료, 3-C2B-2 승인 완료, 3-C2B-3 구현 완료·검토 대기(승인 아님), 3-C3-A·3-C3-B 구현 완료·PASS WITH NOTES, 3-C3-C 기술 검증 완료·Closeout 문서 작성·정합화 완료·Codex 독립 리뷰 PASS WITH NOTES(2026-10-15), 로컬 Commit 소유자 결정 대기, 3-D~3-F 미착수) |
| 미착수 | 26 | Phase 4–29 |

## Phase 0–29 현황

리뷰 문서는 실제 구현 또는 기록이 시작될 때 `01_Phase_Reviews/Phase_XX_Review.md` 형식으로 생성합니다. 미착수 Phase에는 빈 리뷰 문서를 만들지 않습니다.

| Phase | 이름 | 상태 | 리뷰 |
|---:|---|---|---|
| 0 | Reproducible desktop foundation | 승인 완료 | [Phase_00_Review](01_Phase_Reviews/Phase_00_Review.md) |
| 1 | Player identity, visibility, and baseball vocabulary | 승인 완료 (PASS WITH NOTES) | [Phase_01_Review](01_Phase_Reviews/Phase_01_Review.md) |
| 2 | Baseball state transitions and scoring | 승인 완료 (PASS WITH NOTES, 2026-10-11) | [Phase_02_Review](01_Phase_Reviews/Phase_02_Review.md) |
| 3 | Seeded game simulation / Prototype 1 | 3-A(RNG)·3-B1 PASS WITH NOTES / 승인, 3-B2 PASS WITH NOTES / 승인, 3-C1 PASS WITH NOTES / 승인, 3-C2A PASS WITH NOTES / 승인, 3-C2B-1 PASS WITH NOTES / 승인(3-C2B-1만) ; 3-C2B-2 승인 완료(Codex Medium PASS / GPT Chat 최종 PASS, 3-C2B-2만, 2026-10-12) ; 3-C2B-3(`advancement` 해결기) 구현 완료·검토 대기(2026-10-12, 승인 아님, 자체 판정 PASS WITH NOTES) ; 3-C3 설계(D-1~D-8)·3-C3-A 세부 계약(O-1~O-9) 승인, 3-C3-A 구현 완료·GPT Chat 기술 검토 PASS WITH NOTES(로컬 커밋 `18ed60e`), 3-C3-B 세부 계약 B-01~B-07 승인 후 3-C3-B 구현 완료·PASS WITH NOTES(로컬 커밋 `8ed806f`, 2026-10-14 기록, [보고서](../docs/reports/PHASE_3C3B_REPORT.md)), C3-C OD-1~OD-4 승인(구현 NOT AUTHORIZED) ; 2026-10-15 현재: 3-C3-C 기술 검증 완료(테스트 63건, Mutation 34 KILLED/2 SURVIVED, xUnit 1,804 PASS, verify 2회 PASS)·Closeout 문서 작성·정합화 완료, Codex 독립 리뷰 `CODEX_C3C_REVIEW_PASS_WITH_NOTES`, 로컬 Commit 소유자 결정 대기([보고서](../docs/reports/PHASE_3C3C_REPORT.md)) — Phase 3 전체 Gate INCOMPLETE, 3-D 이후 미착수 | [Phase_03_Review](01_Phase_Reviews/Phase_03_Review.md) |
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

- Phase 3 **3-E Framing v3 Package A+B 통합 구현 완료 · 검토 대기** (2026-10-16, 소유자 `TASK.md`, 기준 HEAD `b64d3e9`, 미커밋·미스테이징, 승인 아님): 소유자 정책 E1~E7 구현(Codex 판정 `CODEX_PHASE3_E_ARCH_PASS_WITH_NOTES`는 지시서에 명시되었으나 검토 원문은 저장소에 없어 인용하지 않음). `prob-model-v3` + `tuning-virtual-proto-v3`(해시 `682a5f06…e389a`; v1·v2와 해시 불변), `ModifierConditionScope.NonAutomatedBorderline`(NonAutomated + Borderline 12구역에서만 Framing 적용, Automated·Clear는 `Applied=false`·계수 1,000,000), Model/Tuning 명시적 대응표, `PlayerInputSet` 콘텐츠 v1(ID 불변)/v2(`ReceivingFraming` 포함), `FrozenCatcherInput`, `CheckPrepared` 6c, `CommitEvidence.LinkCalledPitch`(실제 상태·규칙·Set에서 called_pitch 재계산). 새 Stage·Stream·Draw 없음. 전체 xUnit Release **2,174 PASS**(신규 134, 의도적 기존 테스트 변경 6건), 독립 Oracle(238건), Sabotage 18건 중 17건 검출(미검출 1건은 `CheckPrepared`가 먼저 막는 동등 변이, game-engine §32). Game Calling·battery.chemistry 미구현, 3-E 전체 Gate `INCOMPLETE`, 3-F·Phase 4 영속화·실제 Runner 미착수, KBO 공식 RuleProfile 외부 근거 미검증, Codex·Mutation·`verify.ps1` `미실시`. Phase 3 Gate `INCOMPLETE`. 상세: [Phase_03_Review](01_Phase_Reviews/Phase_03_Review.md), [game-engine §32](../docs/game-engine.md).
- Phase 3 **3-D Park / Fielding / Advancement Policy v2 통합 구현 완료 · 검토 대기** (2026-10-16, 소유자 `TASK.md`, 기준 HEAD `054704d`, 이후 로컬 커밋 `b64d3e9`(푸시 안 함), 승인 아님): 소유자 결정 P1~P4·R1~R5·S1~S3 구현(Codex 판정 `CODEX_PHASE3_D_ARCH_REVISIONS_REQUIRED`는 지시서에 명시되었으나 검토 원문은 저장소에 없어 인용하지 않음). `prob-model-v2` + `tuning-virtual-proto-v2`(해시 `7f02cb4f…dd70`, v1·해시 `5b70dca7…3ea5` 불변), 구장 3개 슬롯 활성(800~1250‰, 단타 전용 `park.hits`), `GameExecution`(구장 스냅샷·버전·정책·선수 입력 집합 식별)을 `GameStart`→`GameState`→`FinalizedGame`에 보존, 대표 수비수 Fielding(불변 `PlayerInputSet`·`CheckPrepared` 6b), `advancement-policy-v2`(주자 Speed + 보살 수비수 Arm을 1회 `Compute`). 새 Stage·Stream·Draw 없음. 전체 xUnit Release **2,036 PASS**(신규 129, 의도적 기존 테스트 변경 6건), 독립 Oracle, 6개 조합 전체 경기, Sabotage 15/15 검출. Codex·Mutation·`verify.ps1`·대규모 시뮬레이션 `미실시`. Phase 4 영속화·실제 경기 Runner·3-E·3-F 미착수. Phase 3 Gate `INCOMPLETE`. 상세: [Phase_03_Review](01_Phase_Reviews/Phase_03_Review.md), [game-engine §31](../docs/game-engine.md).
- Phase 3 **3-D BatterTendency v2 통합 구현 완료 · 검토 대기** (2026-10-15, 소유자 `TASK.md`, 기준 HEAD `ee9756a`, 승인 아님): DataPack v2(선택 `batter_zone_tendencies[]`, v1 호환·형태 보존)·불변 `BatterZoneProfiles`·`PlayerWorld` 내부 보관·`PrepareFromProfiles`·HiddenTendencyBoundary/SourceScan 강화·RNG 계약 정정. 신규 테스트 64건, 전체 xUnit 1,907 PASS(Release, 종료 코드 0). 실제 경기 Runner·Phase 4 영속화·구장/수비/주자 보정·3-E·3-F 미착수, Codex·Mutation·verify `미실시`. Phase 3 Gate `INCOMPLETE`. 상세: [Phase_03_Review](01_Phase_Reviews/Phase_03_Review.md), [game-engine §30](../docs/game-engine.md).
- Phase 3 **3-C3-C 검증 완료 · Closeout 문서 통합** (2026-10-15, 소유자 `TASK.md`, 기준 HEAD `52cab03`): **기술 검증 완료**(C3-C-1/2 신규 테스트 63건, E4 Race 11건 × 5회 PASS, Mutation 36건 실행 34 KILLED / 2 SURVIVED, 격리 Clone 전체 xUnit 1,804 PASS·`verify.ps1` 2회 `GATE: PASS`, D5·E4·3B·3C 증거 보호 보관)과 **Closeout 문서 작성 완료**(문서만, 미커밋)는 별개다. B-M7(`SURVIVED / LIKELY_REDUNDANT_GUARD / NOT_FORMALLY_PROVEN`)·A-M25(`SURVIVED / UNRESOLVED`) 생존은 OD-3 잠정 비차단 NOTE(해결 아님), TC-12/13 `UNDEFINED — NOT SCORED`, 모델 변경은 소유자 수용(`MODEL_DEVIATION_OWNER_ACCEPTED`). **Codex 독립 리뷰 `CODEX_C3C_REVIEW_PASS_WITH_NOTES`(BLOCKER 0 / MAJOR 0 / MINOR 1, 정정 완료; 비공개 증거 `REPORTED_AND_ATTESTED_NOT_INDEPENDENTLY_READ`), 최종 문서 정리 단계(`C3C_CLOSEOUT_READY_FOR_OWNER_COMMIT_DECISION_WITH_NOTES`), 로컬 Commit 소유자 결정 대기(미실시), `PHASE_3_GATE_INCOMPLETE`, GitHub Push 금지.** [PHASE_3C3C_REPORT](../docs/reports/PHASE_3C3C_REPORT.md), [결정 기록](02_Decisions/2026-10-15_C3C_Closeout.md), [game-engine §27.21](../docs/game-engine.md), [ADR-007](../docs/decisions/ADR-007.md).
- (이전 기록) Phase 3 **3-C3-C-0 문서 Closeout 반영** (2026-10-14, 소유자 `TASK.md`, 기준 HEAD `8ed806f`): **문서만** — 코드·테스트·verify·Mutation 재실행 없음. **C3-B 구현 완료 / PASS WITH NOTES / 로컬 커밋 `8ed806f`**(푸시 안 함), 사후 Closeout 보고서 [PHASE_3C3B_REPORT](../docs/reports/PHASE_3C3B_REPORT.md)(과거 원본의 재현이 아님). Priority verify Run 1/2·트리 매니페스트 원본 보존(Commit 이전 `654b5d9` dirty 트리에서 실행, Commit 이후 verify 없음); 전체 xUnit 단독 원본·Authority 67/77·C3-B 변이 원본(12/13 검출, M7 생존)은 `SUMMARY_ONLY`/`MISSING`. C3-C OD-1~OD-4 소유자 승인(수행 아님). **C3-C 통합 테스트·변이 재실행 `NOT STARTED / NOT AUTHORIZED`, Codex Medium 리뷰 `NOT AUTHORIZED`, Phase 3 Gate `INCOMPLETE`, Push는 Phase 3 최종 Gate 후 별도 승인.** [결정 기록](02_Decisions/2026-10-13_C3_Design_Approval.md), [game-engine §27.19–27.20](../docs/game-engine.md), [ADR-007](../docs/decisions/ADR-007.md).
- (이전 기록) Phase 3 설계 **3-C3-B 세부 계약 B-01~B-07 승인 반영** (2026-10-13, 소유자 `TASK.md`, 기준 HEAD `18ed60e`): **문서만 `OWNER APPROVED`** — 대안 A(`PitchCommitGate` → `CommitEvidence` → 공통 `CommitGate` 코어 → `GameCursor.Publish`), O-2 순서 불변, 스트림별 `RawDraws` 증거, 전 필드 구조적 미리보기 비교, 신규 오류 5개. **C3-B 프로덕션·C3-C `NOT STARTED / NOT AUTHORIZED`, Phase 3 Gate `INCOMPLETE`.** C3-A는 구현 완료·GPT Chat 기술 검토 `PASS WITH NOTES`·로컬 커밋 `18ed60e`. [결정 기록](02_Decisions/2026-10-13_C3_Design_Approval.md), [game-engine §27.18](../docs/game-engine.md), [ADR-007](../docs/decisions/ADR-007.md).
- Phase 3 설계 **3-C3 설계 승인 반영** (2026-10-13, 소유자 `TASK.md`, 기준 HEAD `a8cb10c`): **D-1~D-8과 추가 계약 A~F `OWNER APPROVED`(설계만)** — `AuthoritativeGame`(`LastEventSequence`·`Revision`)·단일 Stage/Publish·`Apply` 재실행 결과 채택·`Finalize` 동일 Publish·참조 CAS·RNG 증분 검증·`(PreparedPitch, Candidate)` 쌍·불변 `CommitRecord`·영속화는 Phase 4 인수인계·PA 구동기는 3-F 하네스. **C3-A / C3-B / C3-C 및 C3 프로덕션 구현 `NOT STARTED / NOT AUTHORIZED`, Phase 3 Gate `INCOMPLETE`.** 문서만 변경(`src/`·테스트·ROADMAP 불변, 커밋·푸시 없음, Codex 검토 `미실시`). 승인 날짜 2026-10-13은 프로젝트 소유자의 채팅 승인에 근거한다(`TASK.md` 수정 시각은 승인 증거가 아님). [결정 기록](02_Decisions/2026-10-13_C3_Design_Approval.md), [game-engine §27](../docs/game-engine.md), [ADR-007](../docs/decisions/ADR-007.md).
- Phase 3 작업 패키지 **3-C2B-3** (`advancement` 해결기, 2026-10-12, 소유자 `TASK.md`, 기준 HEAD `ab7147b`): **구현 완료 / 기술 승인(Codex 독립 리뷰 `PASS WITH NOTES`, GPT Chat 기술 검토 `PASS WITH NOTES` — 별개 판정, 사용자 전달), Git 반영 전; 3-C3 NOT STARTED / NOT AUTHORIZED** — 승인된 P-01~P-10을 코드로 구현(정책 불변): `AdvancementPlanning.Resolve`가 `CheckPrepared`를 먼저 재사용하고 H1 개인 사본에서 맥락마다 `game/runner@1` 1회(종료 후 `Void`·점유 `Hold` 포함)를 추첨, 선행 주자 우선 순차 합성·명시 `Sequence`·실제 `InPlayResult` 이벤트·변경 없는 `GameReducer` 검증·Reducer 파생 공식 기록 대조로 기존 `InPlayPitchCandidate`(`NextRng` = H2)를 반환. 신규 59 테스트(단계 A 23, 단계 B 36), 원시 행동 벡터 5,562건(합법 4,722 / 불법 840 / 불법 중 Reducer 수락 480) 전수, 득점+제3아웃 318건은 선행 우선 순서에서만 인정, 전체 xUnit 1,513 통과, 결함 주입 변이 25건 + 대조군 1건(행동 수준 검출 24건, M15는 오류 메시지 검증으로만 검출, 대조군 의도대로 생존), `verify.ps1` 연속 2회 `GATE: PASS`(로그 `PHASE_3C2B3_VERIFY_LOG_run1/2.md`, Claude 실행, 최종 트리). 자체 판정 `PASS WITH NOTES`; 최종 검토 후속으로 M15 분류·ADR-007 테스트 수(1,513) 문서 정정([보고서 §21](../docs/reports/PHASE_3C2B3_REPORT.md)); 3-C3·Phase 4 미착수; Phase 3 Gate INCOMPLETE; 커밋·푸시 안 함. [보고서](../docs/reports/PHASE_3C2B3_REPORT.md), [ADR-007](../docs/decisions/ADR-007.md), [game-engine §26](../docs/game-engine.md), [Phase_03_Review](01_Phase_Reviews/Phase_03_Review.md).
- Phase 3 정책 검증 **3-C2B-3-P** (추가 진루 `advancement` 정책, 2026-10-12, 기준 HEAD `f669589`): **정책 검증 수행 / 승인 대기, 구현 아님** — `src/` 불변, 정책 P-01~P-10 최초 제출 시 `PROPOSED`(현재 P-05만 `OWNER APPROVED`, 78행 FIX2), 자체 판정 `POLICY_READY_FOR_REVIEW`(승인 아님), GPT Chat·Codex 검토 `미실시`, 3-C2B-3·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE, 전체 xUnit 1,451 통과(신규 64). 문서: [검증 보고서](../docs/reports/PHASE_3C2B3_POLICY_VALIDATION_REPORT.md), [결정 초안](../docs/reports/PHASE_3C2B3_POLICY_DECISION_DRAFT.md), [인수인계 초안](../docs/reports/PHASE_3C2B3_IMPLEMENTATION_HANDOFF_DRAFT.md).
- Phase 3 정책 검증 **3-C2B-3-P Codex 리뷰 수정** (2026-10-12): Codex Medium 2건(입력·최신 상태 검증 계약 / P-05) 문서·테스트 설계 보완 — `FIX_READY_FOR_REVIEW`(정책 승인 아님), **P-05 `OWNER_APPROVAL_REQUIRED`(이 시점 기록; FIX2에서 `OWNER APPROVED`로 정정)**, `src/` 불변, 전체 xUnit 1,454 통과(직접 실행), Codex 재검토 `미실시`, Phase 3 Gate INCOMPLETE. [검증 보고서 §18](../docs/reports/PHASE_3C2B3_POLICY_VALIDATION_REPORT.md).
- Phase 3 정책 검증 **3-C2B-3-P FIX2** (2026-10-12): 재검토 Codex Medium 2건 정정(문서 계약만) — **P-05 `Prototype Sequence Policy v1` `OWNER APPROVED`(기존 네 개 맥락 한정, 관측 순서·KBO 인증 아님)**, P-01~P-04·P-06~P-10 `PROPOSED` 유지, C2B-3-P 전체 최종 승인 전, P-06은 이벤트 `InPlayResult`(`Out`)와 Reducer 파생 `BatterResult.SacrificeFly` 구분, `FIX2_READY_FOR_REVIEW`(승인 아님), `src/` 불변, 전체 xUnit 1,454 통과(직접 실행), Codex 재검토 `미실시`, 3-C2B-3·3-C3 미착수, Phase 3 Gate INCOMPLETE. [검증 보고서 §19](../docs/reports/PHASE_3C2B3_POLICY_VALIDATION_REPORT.md).
- Phase 3 정책 검증 **3-C2B-3-P 최종 정책 승인** (2026-10-12): **P-01~P-10 전부 `OWNER APPROVED`**(P-05는 먼저 별도 승인·보존) — 정책 승인 문서 반영과 구현 전 계약 정합성 점검만 수행(`POLICY_DOCUMENTATION_READY`, 승인 아님). **3-C2B-3 프로덕션 `NOT STARTED / NOT AUTHORIZED`, 3-C3 미착수, Phase 3 Gate INCOMPLETE.** 소유자 정책 승인 ≠ GPT Chat FIX2 `PASS WITH NOTES`(사용자 전달) ≠ Codex 독립 검토(FIX2 이후 `미실시`, `CHANGES REQUIRED` 이력 보존). 이전 줄들의 "P-05만 승인/나머지 `PROPOSED`"는 그 시점 기록이다. `src/` 불변. [검증 보고서 §20](../docs/reports/PHASE_3C2B3_POLICY_VALIDATION_REPORT.md).
- Phase 3 작업 패키지 **3-C2B-2 최종 승인 완료** (2026-10-12): Codex Medium 집중 재검토 `PASS`(사용자 전달 독립 리뷰 보고; Codex 직접 실행 Simulation 87 + Infrastructure 57 = 144건 PASS) / GPT Chat 최종 `PASS`(Commit 승인 포함, Push 미승인). 승인 범위는 3-C2B-2뿐, Phase 3 전체 Gate INCOMPLETE. 전체 xUnit 1,387 PASS와 verify 연속 2회 GATE PASS는 Claude Code 실행 결과(Codex·GPT Chat의 로컬 전체 독립 실행 아님). 과거 Codex `CHANGES REQUIRED`는 수정 이력으로 보존. 미해결: F-1, F-2, U-2, C3 원자적 확정·EventId 재검사, KBO 2026 인증, 원정팀 승리 제3아웃 `WinningRunner` null 전용 테스트(비차단). 아래 "구현 완료 / 검토 대기, 승인 아님" 표현은 승인 이전 기록이다.
- Phase 3 작업 패키지 **3-C2B-2** (기본 인플레이 계획·끝내기 공식 기록·`PendingAdvancement` 격리·인플레이 후보, 2026-10-12, 소유자 `TASK.md`, 기준 HEAD `34c2028`): **구현 완료 / 검토 대기, 승인 아님** — 실제 `PreparedPitch`만 받는 `InPlayPitchPlanning.PlanBase`/`Build`가 19개 템플릿과 모든 기존 주자의 결정적 기본 이동, `walkoff-credit-v1` 공식 기록으로 `BaseInPlayPlan`을 만들고, 추가 진루 판단이 필요하면 이벤트·ID·영수증이 없는 `PendingAdvancement`, 아니면 `GameReducer.Apply`로 검증한 미확정 `InPlayPitchCandidate`(`ef2`)를 반환. C2A 이후 난수 소비 0, EventId 커서·GameState·영수증 불변. 5,400건 독립 오라클·원시 JSON·리듀서와 필드 단위 일치(일반 780/300, 끝내기 3,114/1,014/192), 신규 172 테스트, 주입 결함 32/32 검출, 전체 xUnit 1,377 통과(기준 1,205 +172), `verify.ps1` 연속 2회 `GATE: PASS`. GPT Chat·Codex 검토 `미실시`, 3-C2B-3(F-1 복수 주자 맥락·F-2)·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE, 커밋·푸시 안 함. [보고서](../docs/reports/PHASE_3C2B2_REPORT.md) · [Phase_03_Review](01_Phase_Reviews/Phase_03_Review.md). **Codex 리뷰 수정(2026-10-12):** 타자주자 결승 득점자 구분(`InPlayWinningRunner`)과 RNG H0/H1/H2 문서 정정만 반영, 전체 xUnit 1,387 통과·verify 2회 PASS, 상태 '재검토 대기'(Codex·GPT Chat 재검토 `미실시`, 승인 아님).
- Phase 3 작업 패키지 **3-C2B-2-P** (인플레이 정책 확정·템플릿 실현성 검증, 2026-10-12, 소유자 `TASK.md`): D1–D5 소유자 정책을 ADR-007·game-engine §23에 기록하고, 양의 확률 45개 조합(확률 0 15개 별도)·템플릿 19개·일반 1,080건·끝내기 4,320건을 테스트 전용 도구로 실제 `GameReducer`에서 검증(거부 0, 결함 0). **프로덕션 코드 변경 없음, 3-C2B-2 구현 미착수, 구현 승인 아님**; GPT Chat 최종 검토 `PASS WITH NOTES`(3-C2B-2-P 정책·템플릿 실현성 검증에 한정; 수치는 Claude Code 제출 실행 결과이며 GPT Chat·Codex가 로컬 신규 테스트 전체를 독립 실행한 것이 아님), Codex 독립 리뷰 `미실시`; 3-C2B-3·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE. 승인 구분: 정책 D1–D5 = 소유자 승인; 템플릿 실현성 검증 = GPT Chat `PASS WITH NOTES`; 3-C2B-2 구현 = 미승인·미착수. 기본 이벤트 5,400건 중 LEGAL_BASE 3,894 / PENDING_ADVANCEMENT 1,314. [보고서](../docs/reports/PHASE_3C2B2_POLICY_REPORT.md), [매트릭스](../docs/reports/PHASE_3C2B2_TEMPLATE_MATRIX.md), [리뷰](01_Phase_Reviews/Phase_03_Review.md).
- Phase 3 작업 패키지 **3-C2B-1 최종 승인** (2026-10-11): GPT Chat `PASS WITH NOTES` / 최종 승인, 독립 Codex 리뷰 `PASS WITH NOTES`(신규 Critical/High/Medium 없음); 승인 범위는 비인플레이 후보 생성만, 3-C2B-2/3·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE; 유지 NOTES는 [보고서 §16](../docs/reports/PHASE_3C2B1_REPORT.md) 참조(소유자 기록 인용: xUnit 1,176 PASS, Codex Release 63/63 PASS, verify 2회 GATE PASS; Commit/Push는 사용자 승인 대기). 아래는 승인 이전 구현 기록: 구현·자체 검증 완료, 신규 63 테스트, 스텁 대비 RED 60 실패/3 통과, 변형 검사 유효 18건 중 17건 검출(1건 생존: 도달 불가 `WasDuplicate` 방어 분기), 전체 xUnit 1,176 통과(기준 1,113에서 +63), `verify.ps1` 2회 GATE PASS, **`검토 대기` — 승인 아님**(승인 이전 기록), GPT Chat 검토 `미실시`, Codex 검토 `미실시`. 3-C2B-2/3·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE, Commit/Push 미허가. [리뷰](01_Phase_Reviews/Phase_03_Review.md), [보고서](../docs/reports/PHASE_3C2B1_REPORT.md), [ADR-007](../docs/decisions/ADR-007.md) 3-C2B-1 절.
- Phase 3 작업 패키지 **3-C2A 후속 수정** (2026-10-11): Codex `CHANGES REQUIRED` Medium 1건(해결기가 `GameState.GameId`와 RNG `ContextKey` 일치를 검사하지 않음)을 `PitchBranch.game_context_mismatch`로 수정, Fixture는 경기 ID 명시, Golden 불변, RED 9 실패/75 통과 → GREEN 84, 전체 xUnit 1113 통과(+11), `verify.ps1` 2회 GATE PASS. 이후 같은 날 GPT Chat `PASS WITH NOTES` / 최종 승인, Codex 최종 집중 재검토 `PASS WITH NOTES`로 **3-C2A 승인**(범위: 3-C2A만; 3-C2B·3-C3 미착수, Phase 3 Gate INCOMPLETE; 상위 리뷰 status는 `검토 대기` 유지). [보고서 §14](../docs/reports/PHASE_3C2A_REPORT.md)·[§15](../docs/reports/PHASE_3C2A_REPORT.md).
- Phase 3 작업 패키지 **3-C2A** (단일 투구 확률 분기·`game/target@1`, 2026-10-11): 구현·자체 검증 완료, 전체 xUnit 1102 통과(기준 1021에서 +81), 독립 Python 오라클 대비 28 시나리오·55 투구 일치, 스텁 대비 RED 69 실패/12 통과, 변형 검사 15건 모두 검출, `verify.ps1` 2회 GATE PASS, **`검토 대기` — 승인 아님**, GPT Chat 검토 `미실시`, Codex 검토 `미실시`. Phase 3 전체가 아니며 Gate INCOMPLETE, 3-C2B·3-C3 미착수, Commit/Push 미허가. [리뷰](01_Phase_Reviews/Phase_03_Review.md), [보고서](../docs/reports/PHASE_3C2A_REPORT.md), [ADR-007](../docs/decisions/ADR-007.md) 3-C2A 절.
- Phase 3 작업 패키지 **3-C1** (이벤트 위치 payload·`ef2`·이벤트 JSON 버전 2)는 2026-10-11 GPT Chat `PASS WITH NOTES` 최종 승인, Codex 최종 집중 재검토 `PASS WITH NOTES` (전체 xUnit 1,021 PASS, Codex 직접 실행 83 PASS, `verify.ps1` 2회 GATE PASS, `ef1` 영수증 30,974건·레거시 writer 바이트 유지). 이전 `CHANGES REQUIRED`(Codex Medium 1, GPT Chat 중첩 null) 수정 이력은 [보고서](../docs/reports/PHASE_3C1_REPORT.md) Part G·H·I에 보존. Phase 3 전체는 아님, Gate INCOMPLETE, 3-C2·3-C3 미착수. 유지 NOTES는 CURRENT_STATE 참조.
- Phase 3 작업 패키지 3-B2(정수 확률 모델·가상 튜닝 계약)는 2026-10-11 GPT Chat `PASS WITH NOTES` 승인, Codex 최종 집중 재검토 `PASS`(전체 xUnit 955 PASS, `verify.ps1` 2회 GATE PASS). 이전 Codex `CHANGES REQUIRED` 수정 이력은 [보고서](../docs/reports/PHASE_3B2_REPORT.md)에 보존. 유지 NOTES는 CURRENT_STATE 참조.
- 없음 — Phase 3 작업 패키지 3-B1은 2026-10-11 GPT Chat `PASS WITH NOTES` 승인, Codex 집중 재검토 PASS WITH NOTES (전체 xUnit 813 PASS(Claude 실행), 관련 xUnit 104 PASS(Codex 직접 실행), `verify.ps1` 2회 GATE PASS, 새 필수 코드 수정 없음; Phase 3 전체는 아님, Gate INCOMPLETE, 3-B2 미착수). 이력: 3-B1 (Pitch Zone Lite 데이터 계약·호출 판정 모드) — 2026-10-11 구현·자체 검증 완료 후 Codex 리뷰(CHANGES REQUIRED: High 1, Low 1)를 반영한 보완 라운드 완료(전체 xUnit 813 통과, 기준 분포 ID와 가중치를 불변 대응으로 고정, 기존 규칙 프로필 해시 7개 불변; Codex 재검토 미실시). Phase 3-B 전체가 아니며 Gate는 평가하지 않음. 커밋·푸시 없음. [리뷰](01_Phase_Reviews/Phase_03_Review.md), [보고서](../docs/reports/PHASE_3B1_REPORT.md), [ADR-005](../docs/decisions/ADR-005.md).
- Phase 3 작업 패키지 3-A(RNG)는 2026-10-11 GPT Chat `PASS WITH NOTES` 승인, Codex 최종 검토 PASS WITH NOTES (전체 xUnit 709 PASS(Claude 실행), Randomness 111 PASS(Codex 독립 재실행), `verify.ps1` 2회 GATE PASS). Phase 3 전체는 아님, Gate INCOMPLETE. [리뷰](01_Phase_Reviews/Phase_03_Review.md), [보고서](../docs/reports/PHASE_3A_REPORT.md), [ADR-004](../docs/decisions/ADR-004.md).
- Phase 2는 2026-10-11 `승인 완료`(PASS WITH NOTES)로 이 목록에서 제외되었습니다.

## 추적 중인 기술 부채

- Phase 2 후속 NOTES(2026-10-11 승인 시 보존): C33 기록된 한계(예시 7 동등 시나리오, 모호한 복합 야수선택 거부), C38 희생플라이 변형 미지원 이월(Phase 3 재평가), C42 `ReachedOnError` 원인 `NOT_MODELLED`, C44 지원 범위 제한(승인된 Phase 2 지원 범위 제한, KBO/OBR 규칙 아님), D2 운영 `GameRules` 비인증, Phase 4 엔진 규칙 버전·`game_states`/`scored_plays`/규칙 프로필 해시 영속화, FHD 미검증.
- ~~Domain 컬렉션 가변성~~ — Phase 2-A에서 해결(방어 복사, `AsReadOnly()`, 변이 테스트 7개).
- Phase 2: 문서화된 득점 단순화(RBI, 자책 재구성 관례, 승/패/세이브, 끝내기 잔루)는 공식 KBO 규칙과 대조되지 않음; 아웃·득점 순서는 기록원 입력(엔진은 일관성만 검사); DH 소멸 전환(야수의 투수 전환)은 OBR 5.11(a)(14) 원문 조항으로 정정(KBO 미인증), 어필 플레이는 승인된 범위 제외, R02는 적용 승인(조문 미확인), OBR 편차 6건과 C33 승계 주자 책임은 수정 완료(C38 희생플라이 변형은 Gate 비필수 후속 이월, 구현 아님); Python 오라클은 동일 작성자·동일 명세 해석이며 100경기 스위트가 드문 규칙을 모두 다루지는 않음(손 오라클이 보완); `ApproachKind`는 Phase 3 전까지 효과 없음; 사람 수동 클릭 검증·FHD·보완 후 Codex 재검토 미실시.

## 주요 선행 조건과 미해결 항목

- Phase 3-C2B 이후와 Phase 4 이상은 소유자의 별도 구현 지시가 필요합니다 (Phase 0–2 승인 완료; Phase 3은 3-A·3-B1·3-B2·3-C1·3-C2A 승인). D2 `GameRules` 운영 부분 집합은 KBO 규정집·기록규칙 확보 후 인증해야 합니다(현재 비인증 프로필; 연장 11회 한도와 R02 운영 근거만 확인).
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
- Phase 3-C3-C Closeout 기록(2026-10-15, Codex 독립 리뷰 PASS WITH NOTES·모델 변경 소유자 수용 반영): [결정 기록](02_Decisions/2026-10-15_C3C_Closeout.md) · [ADR-007](../docs/decisions/ADR-007.md) "Decisions (C3-C closeout)" — 변이 원본 복구·재정의 분리, A-M9/A-M10 적용 불가, B-M7/A-M25 잠정 NOTE, 공식 Git 증거 최소화, 모델 예외 `MODEL_DEVIATION_OWNER_ACCEPTED`(이전 `PENDING_OWNER_DECISION`은 역사적 상태).
- Phase 3-C2B-2 구현 결정(최종 승인 완료 2026-10-12, 3-C2B-2만): [ADR-007](../docs/decisions/ADR-007.md) 3-C2B-2 구현 절 · [game-engine §24](../docs/game-engine.md) — 실제 `PreparedPitch`만 입력, C2B-1 공유 준비 상태 검사, 형제 타입 `InPlayPitchCandidate`/`PendingAdvancement`, 승인 45 조합 명시, 끝내기 공식 기록, 사후 `Verify`, 내부 감사 기록.
- Phase 3-C2B-2 프로토타입 정책 D1–D5(소유자 승인, 구현 미착수): [ADR-007](../docs/decisions/ADR-007.md) 3-C2B-2 절 · [game-engine §23](../docs/game-engine.md) — `inplay-mapping-v1`(추첨된 장타는 잠재적 안타로 해석, 일반 상황은 원본 유지, 끝내기만 `walkoff-credit-v1`로 공식 루수 판정), 장외 홈런만 지원, 타자 실제 도달 루수 = 공식 인정 루수인 템플릿만 생성, 2026 OBR 임시 근거(KBO 미인증), 감사 기록은 Simulation 내부 증거(`ef1/ef2` 불변).
- Phase 3-C2B-1 구현 결정(승인 2026-10-11, 3-C2B-1만): [ADR-007](../docs/decisions/ADR-007.md) 3-C2B-1 절 · [game-engine §22](../docs/game-engine.md) — `PreparedPitch`(외부 생성 불가, 기존 해결기 실제 호출), 현재 타자·투수 `PlayerSheet` ID 대조, 시작 RNG `StateHash`·동일 상태 객체 결합, 별도 작업용 `GameEventIds`, 다섯 비인플레이 분기 → `PitchEvent`/`TakeEvent`(`ef2`), 인플레이 명시적 미지원.
- Phase 3-C2A 구현 결정: [ADR-007](../docs/decisions/ADR-007.md) 3-C2A 절 · [ADR-004](../docs/decisions/ADR-004.md) 항목 20 — 일곱째 스트림 `game/target@1`(투구당 목표 구역 추첨 1회, 위치 추첨보다 먼저), 목표→위치→타자 결정→콜/스윙→타구→수비 결과의 스트림별 `NextBounded(1,000,000)` 1회 계약, 스위치 타자는 투수 투구 손 반대쪽, 명시적 Take는 HBP 질량 유지, 결과는 `GameEvent`가 아닌 초안 (검토 대기).
- Phase 3-C 설계 계약·3-C1 구현 결정: [ADR-007](../docs/decisions/ADR-007.md) — 3-C1~3-C3 구분, 위치 payload의 선택적 후미 필드, `ef1`/`ef2` 공존(위치가 있는 이벤트만 `ef2`), 이벤트 JSON `format_version: 2`의 엄격한 위치 검증과 레거시 분리, 3-C2 `game/target@1` 권장안, 3-C3 공동 원자성·Phase 4 트랜잭션 인계 (3-C1 승인).
- Phase 3-B2 구현 결정: [ADR-006](../docs/decisions/ADR-006.md) — 확률 분모 1,000,000(ppm)과 최대 잔여값 정규화 규칙, `prob-model-v1` 8단계 조건부 구조, 보정 슬롯의 고정 순서·상한·결합 상한, 코스 성향의 정확한 질량 이동(기준 분포 가중 정확 중립, 리그 평균 중립 미주장), 해시로 식별되는 **가상 프로토타입** 튜닝 `tuning-virtual-proto-v1`과 사전 선언 시나리오·목표 (검토 요청; Codex 검토 권장).
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

