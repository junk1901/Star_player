---
type: phase-review
phase: "03"
phase_name: "Seeded game simulation / Prototype 1 — 작업 패키지 3-A (RNG), 3-B1 (구역·규칙 데이터 계약), 3-B2 (확률·튜닝 계약), 3-C1 (이벤트 위치·ef2·JSON v2), 3-C2A (단일 투구 확률 분기·game/target@1)"
status: 검토 대기
started: "2026-10-11"
completed: ""
developer_ai: Claude Code
reviewed_by: ""
branch: "main"
commit: "3-C2A: 'feat: add phase 3c2a deterministic pitch branch resolver' (기준 HEAD d3570b76e93246076253681568b709d3ea1d5a3b 다음 커밋; SHA는 git log 참조 — 문서가 자기 커밋 SHA를 담을 수 없음; 3-C1은 d3570b7)"
roadmap_anchor: "phase-3--seeded-game-simulation--prototype-1"
last_updated: "2026-10-11"
tags:
  - star-player
  - phase-review
---

# Phase 03 Review — Seeded game simulation / Prototype 1 (3-A RNG만 착수)

> [!warning] 상태 규칙
> 허용 상태는 `초안`, `검토 대기`, `승인 완료`, `보완 필요`입니다. 구현 AI는 외부 검토 없이 `승인 완료`로 변경하지 않습니다. 실행하지 않은 검증은 `미검증`, 수행하지 않은 독립 리뷰는 `미실시`로 기록합니다.

> [!success] 3-C2B-1 최종 승인 (2026-10-11, 소유자 `TASK.md`)
> 작업 패키지 3-C2B-1은 **GPT Chat `PASS WITH NOTES` / 최종 승인**, **독립 Codex 리뷰 `PASS WITH NOTES`**(신규 Critical/High/Medium 결함 없음)를 받았다. 승인 범위는 **3-C2B-1 비인플레이 후보 생성에만 한정**되며 3-C2B-2/3·3-C2B 전체·Phase 3 전체 완료가 아니다(3-C2B-2/3·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE). 이 문서의 최상위 status는 규칙에 따라 `검토 대기`로 유지한다.
> - 근거(소유자 기록 인용, 이번 문서 작업에서 재실행하지 않음): 전체 xUnit 1,176 PASS(Claude 실행), Codex 직접 실행 Release 63/63 PASS, `verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, Codex 로그 확인), 3-C2A Golden Vector 유지, 기존 RNG·`ef1`/`ef2`·`GameReducer` 계약 유지.
> - 유지 NOTES: `PlayerSheet`가 권위 있는 공급원에서 왔는지 확인하는 책임은 호출 경계; 마지막 발급 EventId 순번은 3-C3에서 최종 재검사; `GameState`/RNG/ID/영수증 공동 원자성은 3-C3; M16 변형 검사 생존(선행 ID 충돌 검사로 도달 불가능한 `WasDuplicate` 방어 분기); 변경된 `HiddenTendencyBoundaryTests`의 Codex 독립 신규 빌드는 NuGet 접근 문제로 `미실시`; 인플레이 후보 생성·주자 이동·수비 템플릿·`advancement` 미구현; 끝내기 장타 공식 기록 정책 미확정; Phase 3-F 통계 보정 `미실시`.
> - 문서: [보고서 §16](../../docs/reports/PHASE_3C2B1_REPORT.md), [ADR-007](../../docs/decisions/ADR-007.md) 3-C2B-1 절.
> - 아래 3-C2B-1 "구현 완료 — 검토 대기" 기록의 "검토 대기"·"승인 아님"·"미실시" 표현은 승인 이전 시점의 기록이며 자체 판정과 증거 한계는 그대로 보존한다.

> [!note] 3-C2B-1 구현 완료 — 검토 대기 (2026-10-11, `TASK.md`)
> 작업 패키지 3-C2B-1(준비된 투구 + 비인플레이 이벤트 후보)은 구현·자체 검증을 마쳤고 **`검토 대기`**입니다(승인 아님). GPT Chat 검토 `미실시`, Codex 독립 검토 `미실시`(자동 진행 안 함). 3-C2B-2/3(설계 미확정)·3-C3·Phase 4는 미착수, Phase 3 전체 Gate는 INCOMPLETE. 이 문서의 최상위 status는 `검토 대기`로 유지한다. 3-C2A 최종 승인 기록은 아래에 그대로 보존한다.
> - **구현:** `src/StarPlayer.Simulation/Baseball/PitchPreparation.cs`(신규). `Prepare`는 현재 타자·투수 `PlayerSheet`의 ID를 상태와 대조하고 `Player.Bats`/`Player.Throws`로 기존 `PitchBranchResolver.Resolve`를 실제 호출, 외부에서 생성·변경할 수 없는 `PreparedPitch`를 만든다. `BuildNonInPlayCandidate`는 경기 ID·RNG 맥락·시작 `StateHash`·타자/투수·투타 방향·규칙/튜닝/확률 모델·동일 `GameState` 객체를 재확인하고 다섯 분기를 `PitchEvent`/`TakeEvent`(`ef2`)로 바꾸며 별도 작업용 `GameEventIds`로 후보 ID를 발급, 기존 `GameReducer.Apply`로 원본 상태에서 검증한다. 인플레이는 `in_play_not_supported_yet`, 추가 RNG 0, 상태·RNG·ID 커서 불변.
> - **변경 파일:** `PitchPreparation.cs`(신규), `PitchPreparationTests.cs`(신규), `HiddenTendencyBoundaryTests.cs`(신규 타입 2개를 시그니처 가드에 추가), 문서(보고서·ADR-007·game-engine §22·SYSTEM_CONTRACTS C08·CURRENT_STATE·이 리뷰·대시보드). `GameReducer`·`PitchBranchResolver`·이벤트/JSON/RNG/튜닝/Golden은 변경 없음.
> - **테스트(실제 실행):** 신규 63 테스트; 스텁 대비 RED 60 실패/3 통과; 변형 검사 유효 18건 중 17건 검출, 1건 생존(M16: 사전 ID 충돌 검사 때문에 도달 불가한 `WasDuplicate` 방어 분기); 전체 xUnit 1,176 통과(Domain 263, Simulation 383, Application 117, Infrastructure 413; 기준 1,113에서 +63), 실패 0; `tools/verify.ps1` 연속 2회 `GATE: PASS`; 3-C2A 기존 테스트·Golden 불변. 원시 로그와 상세는 [보고서](../../docs/reports/PHASE_3C2B1_REPORT.md) §11~12.
> - **NOTES:** 위 M16 생존; `PreparedPitch`의 상태 객체 동일성은 단기 메모리 가드일 뿐 Phase 4 무결성·3-C3 공동 원자성을 대신하지 않음; 신뢰 가능한 `PlayerSheet` 공급은 호출자 책임; 3-C3는 후보의 ID를 바꾸지 않고 채택해야 함; 인플레이·`advancement`·주자 이동·수비 기록·타석 자동 진행 미구현(3-C2B-2/3, 3-C3); 통계 보정 3-F.
> - 문서: [ADR-007](../../docs/decisions/ADR-007.md) 3-C2B-1 절, [game-engine §22](../../docs/game-engine.md), [보고서](../../docs/reports/PHASE_3C2B1_REPORT.md).

> [!success] 3-C2A 최종 승인 (2026-10-11, 소유자 `TASK.md`)
> 작업 패키지 3-C2A는 **GPT Chat `PASS WITH NOTES` / 최종 승인**, **Codex 최종 집중 재검토 `PASS WITH NOTES`**를 받았다. 기존 Codex `CHANGES REQUIRED`(Medium 1)는 해결·재검증됐고 새 Critical/High/Medium 결함은 없다(아래 판정과 수정 이력 보존). 근거(소유자 기록 인용): 전체 xUnit 1,113 PASS(Claude 실행), Codex 직접 실행 Release 92/92 PASS, `verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행·Codex 로그 확인), 독립 Python 골든 검증 PASS, 3-A 골든 바이트 불변, `game/target@1` 독립성·정상 분기 RNG 호출 수·원본 GameState/RNG 스냅샷 불변성 확인. **승인 범위는 3-C2A뿐**이며 3-C2 전체·Phase 3 전체 완료가 아니다: 3-C2B·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE. 이 문서의 최상위 status는 규칙에 따라 `검토 대기`로 유지한다.
> - **비차단 NOTES(유지):** 수정 전 RED 9 실패/75 통과는 원시 로그 없이 요약만 확인됨; 변형 검사 15건 원시 출력의 독립 검토 한계; 분기 중간 오류·늦은 취소 추가 검증; 실제 타자·투수와 요청 입력 연결(3-C2B); 초안→합법 `GameEvent` 변환(3-C2B); `advancement`·주자 이동 검증(3-C2B); 이벤트 ID·영수증·RNG·`GameState` 공동 원자성(3-C3); DB 트랜잭션·RNG 기대 해시(Phase 4); 100,000 PA / 100경기 통계 보정(3-F).
> - 문서: [보고서 §15](../../docs/reports/PHASE_3C2A_REPORT.md), [ADR-007](../../docs/decisions/ADR-007.md).
> - 아래 "검토 대기"·"미실시"·"Commit/Push 안 함" 표현은 승인 이전 시점의 기록이다.

> [!note] 3-C2A 구현 완료 — 검토 대기 (2026-10-11, `TASK.md`)
> 작업 패키지 3-C2A(단일 투구 확률 분기 + `game/target@1`)는 구현·자체 검증을 마쳤고 **`검토 대기`**입니다(승인 아님). GPT Chat 검토 `미실시`, Codex 독립 검토 `미실시`(자동 진행 안 함). 3-C2B·3-C3·Phase 4는 미착수, Phase 3 전체 Gate는 INCOMPLETE. 이 문서의 최상위 status는 `검토 대기`로 유지한다.
> - **구현:** 일곱째 스트림 `game/target@1`(투구마다 `NextBounded(1,000,000)` 1회, `game/location@1`보다 먼저; 알고리즘·`kd1`·`rngset1`·3-A Golden Vector 불변)과 `PitchBranchResolver.Resolve`: 목표 구역 → 실제 위치(`PitchLocation.FromZone`) → 타자 결정 → 콜 또는 스윙 결과 → 타구 → 수비 결과를 각각 계약된 스트림에서 정확히 1회 추첨(도달하지 않은 단계는 추첨 없음, 확률 100% 단계도 추첨, `advancement`는 3-C2B). 스위치 타자는 투수 투구 손의 반대쪽(투구마다, RNG 없음), 명시적 Take는 일반 타자 결정(HBP 질량 유지) 뒤 콜만(스윙·타구·수비 없음). 결과는 `PitchBranchDraft`이며 `GameEvent`·`GameState`가 아니다.
> - **테스트(실제 실행):** 전체 xUnit 1102 통과(Domain 263, Simulation 383, Application 117, Infrastructure 339; 기준 1021에서 +81), 실패 0. 기대값은 독립 Python 오라클(28 시나리오·55 투구, 거부 샘플링 벡터). 스텁 대비 RED 69 실패/12 통과, 변형 검사 15건 모두 검출. `tools/verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, 로그 `PHASE_3C2A_VERIFY_LOG_run1/2.md`).
> - **NOTES:** 스트림 계약은 자체 RED 실행이 없음, 변형 검사 원시 출력 미보존, 분기 중간 실패 주입·늦은 취소 미테스트, 오라클은 C#과 작성자·계약 해석을 공유, 해결기는 호출자가 준 타석 방향·투구 손을 상태의 타자·투수와 대조하지 않음, Take/투구 단계 규칙은 리듀서와 코드 공유 없이 재진술, 통계 보정 미실시(3-F).
> - **3-C2B/3-C3 인계:** 합법 `GameEvent` 변환(수비 기록·진루·아웃 순서·제3아웃·끝내기·실책 귀속), `advancement` 추첨, 이벤트 ID·`ef2` 영수증, 새 데이터의 위치 필수화, 타석 방향·투구 손의 라인업 연결, 타석 자동 진행, 상태+RNG+ID+영수증 공동 원자성, Phase 4 트랜잭션·RNG 기대 해시.
> - 문서: [ADR-007](../../docs/decisions/ADR-007.md) 3-C2A 절, [ADR-004](../../docs/decisions/ADR-004.md) 항목 20, [game-engine §21](../../docs/game-engine.md), [보고서](../../docs/reports/PHASE_3C2A_REPORT.md), [RED·변형 요약](../../docs/reports/PHASE_3C2A_RED_EVIDENCE.md).

> [!warning] 3-C2A Codex 지적 수정 — 검토 대기 유지 (2026-10-11, `TASK.md`)
> 3-C2A에 대해 GPT Chat 1차 검토는 `PASS WITH NOTES`, Codex 독립 검토는 **`CHANGES REQUIRED` — Medium 1건**이었다(위 3-C2A 기록과 NOTES는 보존). 최종 승인은 보류 상태이며 이번 수정 후에도 **`검토 대기`**다(`승인 완료` 아님).
> - **결함:** `PitchBranchResolver`가 `GameState.GameId`와 RNG 스냅샷 `ContextKey`를 비교하지 않아 `g001` + `game:3c2a:probe`처럼 서로 다른 경기의 입력도 성공했다. 테스트 Fixture가 GameId를 `g001`로 고정해 이를 가려 왔다.
> - **수정:** 스냅샷 복원 성공 후·첫 추첨 전에 `string.Equals(GameId, ContextKey, Ordinal)` 검사, 불일치 시 `PitchBranch.game_context_mismatch`(정규화·ID 변경·기본값 대체 없음). 정상 입력의 확률·RNG 소비·다음 스냅샷은 그대로(HBP 3 / 볼·루킹 4 / 헛스윙·파울 4 / 인플레이 6회). 기존 `Rng.*` 오류 코드 유지.
> - **Fixture·Golden:** 상태 생성 helper가 경기 ID를 명시적으로 받는다(임의 덮어쓰기 helper 없음). Phase 3-A Golden Vector와 3-C2A Golden JSON·Python 오라클은 변경하지 않았다(골든 `context`를 GameId로 사용).
> - **테스트(실제 실행):** RED 9 실패/75 통과(84), GREEN 84 통과. 전체 xUnit **1113 통과**(Domain 263, Simulation 383, Application 117, Infrastructure 350; 기준 1102에서 +11), 실패 0. `tools/verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, 로그 `PHASE_3C2A_FIX_VERIFY_LOG_run1/2.md`). `git diff --check` 오류 없음.
> - **미실시:** Codex 재검토 `미실시`(자동 진행 안 함), GPT Chat의 수정 검토 `미실시`. 3-C2B·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE, Commit/Push 안 함.
> - 문서: [보고서 §14](../../docs/reports/PHASE_3C2A_REPORT.md), [RED 증거](../../docs/reports/PHASE_3C2A_RED_EVIDENCE.md), [ADR-007](../../docs/decisions/ADR-007.md) 항목 13.

> [!success] 3-C1 승인 (2026-10-11)
> 작업 패키지 3-C1(이벤트 위치 payload·`ef2`·이벤트 JSON 버전 2)은 **PASS WITH NOTES / GPT Chat 최종 승인**, Codex 최종 집중 재검토 **PASS WITH NOTES**. 기존 Medium(`ReadEventsV2` 명시적 null)과 추가 중첩 컬렉션 null 오류는 해결·재검증(이전 `CHANGES REQUIRED` 판정과 수정 이력은 아래 보존). 전체 xUnit 1,021 PASS(Claude 실행), Codex 직접 실행 83 PASS(Infrastructure 61, Simulation 22), `verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, Codex 로그 확인), 30,974개 `ef1` 영수증 다이제스트·레거시 writer 바이트 유지, 신규 `ef2` 지문·충돌·JSON 호환성 확인. **이 승인은 3-C1 작업 패키지에만 적용되며 Phase 3 전체 Gate는 INCOMPLETE, 3-C2/3-C3 미착수다. 이 문서의 최상위 status는 `검토 대기`로 유지한다.** ADR-007은 3-C1 한정 Accepted. 유지 NOTES:
> - 최초 3-C1 구현의 RED 로그 부재
> - 최초 및 이후 변형 검사 원시 출력의 독립 검증 한계
> - 이벤트 외부 JSON 입력 검증의 별도 안정화
> - 3-C2: `game/target@1` 및 목표 구역 선택
> - 3-C2: 실제 투구 위치 필수화와 타자 방향 검증
> - 3-C2: `NextBounded` 호출 수와 `Draws` 구분
> - 3-C2: 명시적 `Take` 및 스위치 타자 정책
> - 3-C3: GameState·RNG·이벤트 ID·영수증 공동 원자성
> - Phase 4: DB 트랜잭션 및 복구

> [!success] 3-B2 승인 (2026-10-11)
> 작업 패키지 3-B2(확률·튜닝 계약)는 **PASS WITH NOTES / GPT Chat 승인**, Codex 최종 집중 재검토 **PASS**. 이전 Codex `CHANGES REQUIRED`(High 2, Medium 1)는 해결·재검증(이력은 아래 보존). 전체 xUnit 955 PASS(Claude 실행), Codex 직접 실행 71 PASS(Domain 31, Infrastructure 40), `verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, Codex 로그 확인). **Phase 3 전체는 승인되지 않았다 — 이 문서의 최상위 상태는 `검토 대기`로 유지, Phase 3 Gate INCOMPLETE, 3-C 미착수.** 유지하는 NOTES:
> - 3-C: target_zone 선택 방식 및 RNG 스트림 계약
> - 3-C: NextBounded 호출 수와 실제 Draws 구분
> - 3-C: 실제 투구 결과, 이벤트, ef2 및 공동 원자성
> - 3-D: 코스 성향의 실제 확률 연결
> - 3-D 이전: 구장 Hits 계수의 단타 전용 매핑에 대한 소유자 결정
> - 3-E: Framing 상한 및 실제 계산
> - 3-F: 100,000 PA, 100경기, 8개 집계 목표 검증
> - Phase 4: RNG 기대 해시 및 경기 상태·영수증의 트랜잭션 저장
> - 확률표의 이전 설계 조정 이력(목표 구역 가중치 두 차례 조정)과 독립 검증의 한계 유지

> [!success] 3-A 승인 (2026-10-11)
> 작업 패키지 3-A(RNG)는 **PASS WITH NOTES / GPT Chat 승인**, Codex 최종 집중 재검토 **PASS WITH NOTES**. 전체 xUnit 709 PASS(Claude 실행), Randomness 111 PASS(Codex 독립 재실행), `verify.ps1` 2회 `GATE: PASS`(Claude 실행, Codex 로그 확인). 마지막 비차단 노트(`GameRandomSnapshot.cs`의 오래된 주석)는 주석만 수정했고 RNG 동작·알고리즘·직렬화는 변경 없음. **Phase 3 전체 Gate는 INCOMPLETE, Phase 3-B 미착수.** 아래 `검토 대기`·`미승인`·`미실시` 표기는 승인 이전 라운드의 기록이며 3-A에 한해 이 블록이 최신이다. 승인 근거: GPT Chat 최종 판정(소유자 전달, `TASK.md`).

> [!important] 범위
> 소유자는 **Phase 3-A RNG 구현만** 승인했다(`TASK.md`). 3-B~3-F와 Phase 4는 미승인·미착수다. 이 문서는 **Phase 3 전체 Gate를 PASS로 기록하지 않는다.** Phase 3 Gate(100,000 PA, 100경기, 보정 구간, Pitch Zone Lite 검사 등)는 전부 `미검증`/INCOMPLETE다.

## 3-C1 진행 상황 (2026-10-11, `TASK.md` — 이벤트 위치 payload·`ef2`·이벤트 JSON 버전 2)

> [!warning] 2차 결함 수정 라운드 — 중첩 이벤트 목록의 null 요소 (2026-10-11)
> GPT Chat 추가 검토 **`CHANGES REQUIRED`**. 1차 수정의 탐색에서 발견된 결함: 이벤트 내부 목록(`moves`, `strikeout_credits`, `batter_credits`, 이동의 `credits`)과 시나리오 `events`의 **null 요소**가 `NullReferenceException`(`errors: [null]`은 경로 없는 `Fixture.enum_invalid`)을 일으킨다. **세 reader(레거시 `ReadEvents`, `ReadEventsV2`, 경기 시나리오 reader) 모두에서 직접 실행으로 재현**(추정 아님). 회귀 테스트 24건을 먼저 작성해 **RED 19 실패 / 5 통과**(5건은 호환성 보호) → 공통 변환 함수(`Item`·`Credits`·`Moves`·`Errors`·`Events`) 수정 → **GREEN 24/24**, 신규 변형 검사 6건 모두 검출. 오류는 `Fixture.field_missing` + 정확한 경로(`events[0].moves[1].credits[0]`, 시나리오는 `g001.events[2]` 접두사 유지), 전체 입력 실패. 목록 자체가 null인 선택 항목은 그대로 허용(`strikeout_credits: null`은 기존 의미 유지, 빈 목록으로 바꾸지 않음). 전체 xUnit 1021 통과(Domain 263, Simulation 383, Application 117, Infrastructure 258; 997에서 +24). `ef1`/`ef2`·30,974 다이제스트·Golden Vector·`GameEvents`·`GameReducer`·RNG 불변(고정 테스트 통과). `tools/verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, 로그 `PHASE_3C1_FIX2_VERIFY_LOG_run1/2.md`). **Codex 재검토 `미실시`, 3-C1 최종 승인 대기, 상태는 `검토 대기` 유지.** 유지 노트: 최초 3-C1 RED 로그 부재, 최초 변형 검사 7건 원시 로그 미검토, 이벤트 밖 null 멤버(라인업·벤치·expected, 다른 변환기) 미조사, 신규 verify 로그의 CRLF·단독 CR 후행 공백. 근거: [보고서 Part H](../../docs/reports/PHASE_3C1_REPORT.md), [RED 요약](../../docs/reports/PHASE_3C1_FIX2_RED_EVIDENCE.md). 아래 1차 수정 라운드의 "수정하지 않은 관련 노트"는 이 라운드로 해결됨.

> [!warning] Codex 결함 수정 라운드 (2026-10-11)
> 3-C1 코드에 대한 Codex 독립 리뷰 결과 **`CHANGES REQUIRED`** (Medium 1). `ReadEventsV2`가 `{"format_version":2,"events":null}`에서 `ArgumentNullException`, `"events":[null]`에서 `NullReferenceException`을 던졌다(`required`는 명시적 JSON null을 허용). 회귀 테스트를 먼저 작성해 **RED(5건 실패 / 23건 통과)** 를 확인하고 `ReadEventsV2`만 수정 → **GREEN 28/28**. null 목록·null 요소는 `Fixture.field_missing`(경로 `events` / `events[i]`)으로 전체 읽기 실패, 부분 성공·기본값 대체 없음. 전체 xUnit 997 통과(Domain 263, Simulation 383, Application 117, Infrastructure 234; 기준 985에서 +12). `ef1`/`ef2`·Golden Vector·`Location` 계약·`GameReducer`·레거시 reader/writer 불변(고정 테스트 통과). `tools/verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, 로그 `PHASE_3C1_FIX_VERIFY_LOG_run1/2.md`). **Codex 재검토 `미실시`, 3-C1 최종 승인 대기, 상태는 `검토 대기` 유지.** 수정하지 않은 관련 노트: 이벤트 내부 목록의 명시적 null(`moves: [null]`, `strikeout_credits: [null]` 등)은 `ReadEventsV2`·레거시 `ReadEvents`·시나리오 reader에서 여전히 예외(별도 지시 필요). 최초 3-C1 RED 로그 부재와 변형 검사 7건 원시 로그 미검토 노트는 그대로 유지. 근거: [보고서 Part G](../../docs/reports/PHASE_3C1_REPORT.md), [RED 요약](../../docs/reports/PHASE_3C1_FIX_RED_EVIDENCE.md).

> [!note] 상태
> 3-C1은 구현·자체 검증을 마쳤고 **`검토 대기`**입니다(승인 아님). GPT Chat 검토 `미실시`, 3-C1 코드에 대한 Codex 독립 검토 `미실시`(Codex는 3-C *아키텍처*만 검토: `READY WITH NOTES`). 이 문서의 최상위 상태는 `검토 대기`이며 Phase 3 전체 Gate는 INCOMPLETE, 3-C2·3-C3·Phase 4는 미착수다.

- **구현:** `PitchSpec`(→ `PitchEvent`/`TakeEvent`)와 `InPlayEvent`의 맨 끝에 선택적 `PitchLocation? Location = null` 추가(기존 인자 순서·기본값 불변, `InPlayEvent`는 자체가 투구 1회). 위치가 있는 이벤트는 `ef2`(`ef2|kind|<ef1 필드 그대로>|zone_model|zone|side|boundary`), 그 밖의 모든 이벤트는 `ef1` 그대로. `GameReducer.Apply`는 수정하지 않았다(영수증 버전 → 종류·지문 순서 비교가 이미 있음). 이벤트 JSON `format_version: 2`(`ReadEventsV2`/`WriteEventsV2`, 투구 이벤트에 위치 필수·엄격 검증, 비투구 이벤트에는 위치 불허)와 레거시 배열 형식 분리, 레거시 writer는 위치가 있는 이벤트를 조용히 버리지 않고 거부.
- **결정/계약:** [ADR-007](../../docs/decisions/ADR-007.md), [game-engine.md §20](../../docs/game-engine.md), [보고서](../../docs/reports/PHASE_3C1_REPORT.md). ROADMAP §1.14·C08의 "ef1 영수증에서 위치 제거" 부정확 문장을 계약 변경 없이 정정(ef1 영수증은 위치를 가진 적이 없음: ef1+위치 추가 / ef2−위치 제거 = 버전 불일치, 위치 변경 = 내용 충돌).
- **테스트(실제 실행):** `dotnet test StarPlayer.sln -c Release` 985 통과(Domain 263, Simulation 383, Application 117, Infrastructure 222; 기준 955에서 +30), 실패·건너뜀 0. `ddbc999`(3-B2)에서 계산한 두 리터럴(Phase 2 100경기 30,974개 영수증 SHA-256, 레거시 `WriteEvents` 바이트 SHA-256)이 현재 트리에서도 동일. 변형 검사 7건 모두 검출. RED 로그는 없음(구현 후 테스트 작성; 변형 검사와 HEAD 계산 해시가 대체 증거). `tools/verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, 로그 `PHASE_3C1_VERIFY_LOG_run1/2.md`).
- **기존 테스트 변경:** `EventIdentityTests`의 "모든 생성자 매개변수가 지문을 바꾼다" 테스트에 `Location` 변형을 추가하고 `InPlayEvent` 매개변수 개수 가드를 7→8로 갱신(기존 `ef1` 기대값은 변경 없음).
- **미검증/미실시:** Phase 3 Gate 전부, 3-C 해결기 흐름(3-C2), 공동 원자성(3-C3), Codex 코드 검토, GPT Chat 검토, 수동 UI·FHD(UI 변경 없음).
- **이월/주의:** 3-C2 — `game/target@1`(권장, ADR-004 개정+골든 벡터), `target_zone` 실현, `NextBounded` 호출 수와 실제 Draws 구분, 명시적 Take의 HBP 정책, 스위치 타자 타석 방향 정책, 이벤트 방향의 타자 적합성 검증. 3-C3 — 이벤트 ID·리듀서 결과·영수증·RNG 커서 공동 원자성, Phase 4 트랜잭션·RNG 기대 해시. 리듀서는 위치 없는 새 투구를 거부하지 않는다(새 데이터의 위치 필수는 v2 reader/writer와 3-C2 해결기가 담당).
- **승인자 또는 승인 근거:** 없음 — 3-C1은 미승인.

## 3-B2 진행 상황 (2026-10-11, `TASK.md` — 정수 확률·가상 튜닝 계약)

> [!warning] Codex 결함 수정 라운드 (2026-10-11)
> Codex 독립 리뷰 결과 **`CHANGES REQUIRED`** (High 2, Medium 1). ① `Distribution`이 내부 배열을 그대로 노출(형변환으로 공개 표만 바꿔 `Select`와 불일치) ② 공유 `ProbabilityModel.V1`의 중첩 목록과 `TuningProfile.Modifier().Table`이 외부에서 변경 가능(계산값이 바뀌어도 `ContentHash` 불변) ③ `"baselines": null` 입력에서 `NullReferenceException`. 세 건 모두 수정 전 코드에서 **RED 재현**(분포 `int[] [0, 600000]`, swing 320388 → 348837 ppm·해시 불변, NRE; [증거](../../docs/reports/PHASE_3B2_FIX_RED_EVIDENCE.md))한 뒤 수정했다: 공개 컬렉션 전부를 생성 시 복사한 `ImmutableArray`로 바꾸고(모델·튜닝·추적 포함, 반사 기반 감사 테스트), 리더는 필수 항목 null을 `Fixture.field_missing`으로 반환한다. 확률 모델·정규화·상한·튜닝 수치·시나리오·목표·튜닝 해시 `5b70dca7…3ea5`는 **불변**이며, 수정 전 코드에서 만든 105개 조건 전체 결과의 골든 다이제스트가 수정 후에도 동일하다. 신규 테스트 46개, Release 전체 xUnit 955 통과(909 + 46, Claude 실행). 자세한 내용은 [보고서 Part G](../../docs/reports/PHASE_3B2_REPORT.md), [ADR-006 항목 13–15](../../docs/decisions/ADR-006.md). **여전히 `검토 대기`(승인 아님), Codex 재검토 `미실시`, Phase 3 전체 Gate INCOMPLETE, Commit/Push·3-C 미착수.** 아래 본문의 테스트 수(909)와 "Codex `미실시`"는 수정 이전 기록이다.

> [!note] 상태
> 3-B2는 구현·자체 검증을 마쳤고 **`검토 대기`**입니다(승인 아님). GPT Chat 검토 `미실시`, Codex 독립 검토 `미실시`(권장: 확률 산술과 튜닝 식별은 이후 모든 리졸버 패키지의 기반). Phase 3 전체 Gate는 INCOMPLETE이며 3-C 이후·Phase 4·Git Commit/Push는 미착수/미허가입니다. 실제 투구·타격 판정, RNG 소비, 이벤트·`ef1`/`ef2`·리듀서·GameRules·RNG 스트림 변경, 영속화, 10만 PA/100경기 보정은 **하지 않았습니다.**

- **구현(계약만):** `src/StarPlayer.Domain/Baseball/Probability/` — 정수 확률 척도(ppm, 분모 1,000,000, 최대 잔여값 정규화·낮은 인덱스 우선·반올림 half-up·128비트 중간 연산, 부동소수점 없음), `prob-model-v1` 8개 단계(ROADMAP §4.1 순서, 단계별 독립 분모), 고정 순서·슬롯별 상한·결합 상한 `[0.8, 1.25]`의 보정 슬롯, Pitch Zone Lite 코스 성향(정확한 질량 이동, 상한 60,000/40,000 ppm, 창 보호, 기준 분포 가중 정확 중립 — 리그 평균 중립은 주장하지 않음), `ProbabilityTrace`, 버전형 **가상 프로토타입** 튜닝 `tuning-virtual-proto-v1`(`content/tuning/virtual_tuning_v1.json`, 해시 `5b70dca7…3ea5`, 독립 Python 스크립트로도 계산해 일치). 포수·배터리·구장 효과는 중립 입력만 받는 예약 슬롯(수치 권한 없음).
- **결정/계약:** [ADR-006](../../docs/decisions/ADR-006.md), [game-engine.md §19](../../docs/game-engine.md), [보고서](../../docs/reports/PHASE_3B2_REPORT.md). ADR-005 상태 줄만 승인 이력에 맞게 정리(결정 내용 불변).
- **테스트(실제 실행):** `dotnet test StarPlayer.sln` — 909 통과(Domain 253, Simulation 371, Application 117, Infrastructure 168; 기준 813에서 +96), 실패·건너뜀 0. 변형 검사 8건: 7건 즉시 검출, 1건("`batter.power` 타깃 추가") 처음 생존 → 슬롯 카탈로그 고정 테스트 추가 후 검출. 사전 선언 시나리오 4건(고정 시드 2026101101–04, 200,000 표본, 이항 4.5σ)은 첫 실행에 통과, 시드·허용치 조정 없음. `tools/verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, 2026-10-11 12:15·12:18; Release 909 통과, Godot 진단·Windows Export·QHD 확인 통과, 링크 검사 69개 파일; 로그 `docs/reports/PHASE_3B2_VERIFY_LOG_run1.md`·`_run2.md`, 보고서 §5.1). 독립 재실행은 `미실시`.
- **공개해야 할 설계 이력:** 목표 구역 가중치 1안은 정확 산술 점검에서 존 안 투구 비율 0.738(의도 0.45–0.65)이어서, 동결(해시 확정)·표본 실행 이전에 바깥쪽으로 두 차례 조정(최종 균형 0.640). 보정이 아니라 표 설계이며, 고정 해시는 최종 표만 포함.
- **미검증/미실시:** Phase 3 Gate 항목 전부, 10만 PA·100경기, 3-F 통계 목표 평가(8개 목표는 선언만, `declared_for_3f_not_evaluated`), 수동 UI·FHD(UI 변경 없음), Codex 독립 검토, GPT Chat 검토, 성능 측정.
- **이월/주의:** ① 3-C: `target_zone` 실현 방식과 RNG 스트림(제안 `game/ai@1`, `game/location@1`을 쓰면 ADR-004 항목 7 문구 수정 필요), 결정 수는 `NextBounded` 호출 수로 센다(원시 소모는 약 0.0225% 더 많을 수 있음) ② 3-D: 성향 문맥 연동, 구장 슬롯 활성화 전 C46 `Hits`의 의미(현재 단타 한정 매핑) 소유자 확인 ③ 3-E: Framing 상한 ④ 3-F: 가상 수치 교체·보정, 분포 가중 성향 기준 ⑤ ADR-004 헤더는 여전히 `Proposed`(미수정).
- **승인자 또는 승인 근거:** (작성 당시 "없음 — 미승인") → 2026-10-11 GPT Chat `PASS WITH NOTES` 승인 + Codex 최종 집중 재검토 `PASS`(`TASK.md`). 위 "3-B2 승인" 콜아웃 참조.

## 3-B1 진행 상황 (2026-10-11, `TASK.md` — Pitch Zone Lite 위치·규칙 데이터 계약)

> [!success] 3-B1 최종 승인 (2026-10-11)
> 내부 작업 패키지 3-B1은 **PASS WITH NOTES / GPT Chat 승인**, Codex 집중 재검토 **PASS WITH NOTES**. 기존 High 결함(기준 분포 ID와 가중치 대응)은 해결되었고 Codex 독립 재검토를 통과했으며, 새로 필요한 코드 수정은 없다. 전체 xUnit 813 PASS(Claude 실행), 관련 xUnit 104 PASS(Codex 직접 실행), `verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, Codex 로그 검토). **이 승인은 3-B1에만 해당한다 — Phase 3 전체 Gate는 INCOMPLETE, 3-B2 미착수, 이 문서의 최상위 상태(`status`)는 `검토 대기` 유지.** 아래의 `검토 대기`·`미승인`·`미실시` 표기와 Codex `CHANGES REQUIRED` 판정은 승인 이전 라운드의 이력이며 3-B1에 한해 이 블록이 최신이다.
> 유지하는 후속 NOTES: ① `UniformV1`은 ID·가중치 의미가 고정된 기준이며 다른 분포는 새 ID로 등록 ② 3-B2: 확률 분모·기준 확률·상한·보정 시드 확정 ③ 3-C: v2 규칙 팩의 경기 연동, 투구별 타석 방향 ④ 3-E: Borderline/Framing 영향 상한 ⑤ Application 조회 DTO 연결 시 숨겨진 성향 값의 직렬화 노출 검사 ⑥ 운영용 KBO `CalledPitchMode`의 공식 근거 미확정.

> [!note] 상태
> 3-B1은 구현·자체 검증을 마쳤고 **`검토 대기`**입니다(승인 아님). GPT Chat 검토 `미실시`, Codex 검토 `미실시`. Phase 3-B 전체·3-B2 이후·Phase 4는 미착수이며 Phase 3 전체 Gate는 INCOMPLETE입니다. 이 구간에서 위치 추첨, 이벤트·`ef1`/`ef2`·리듀서·RNG·확률 변경은 없습니다.

- **구현:** `src/StarPlayer.Domain/Baseball/PitchZone/` — `ZoneModelVersion` `pzl-zones-v1`, 17개 `PitchZoneId`와 버전형 Clear/Borderline 표(경계선과 변을 공유하면 Borderline: 12개, 나머지 5개 Clear), `PitchLocation`(구역·실제 타석 방향·경계 분류, 허용되지 않는 조합 거부), `TargetZone`, `AppliedBatterSideRules`(스위치 타자), 숨김·유계(`-100..+100`)·평균 중립 `BatterZoneTendency`와 `ZoneReferenceDistribution`. C40 `CalledPitchMode`(`Automated`/`NonAutomated`)와 구역 모델을 짝으로 요구하는 `gamerules-v2`(JSON `format_version` 2, 가상 프로필 2개). 구형 프로필은 판정 모드를 선언하지 않은 사실을 유지하고 7개 `ContentHash`가 변경 전 독립 계산값과 같음.
- **Codex 독립 리뷰와 보완 라운드:** Codex 결과 `CHANGES REQUIRED`(High 1, Low 1). High — `ZoneReferenceDistribution`이 같은 ID(`pzl-ref-uniform-v1`)에 다른 가중치를 허용해 저장된 `ReferenceId`의 의미가 둘일 수 있었음(반례: 가중치 `[3,1,1,...]`에서 `[1,-3,0,...]`가 중립으로 통과, 정식 UniformV1에서는 합 -2). 수정 — ID와 가중치를 한 쌍의 불변 등록 정의로 고정(`pzl-ref-uniform-v1` = `[1]*9`만, 미등록 ID·다른 가중치는 생성 단계에서 거부, 미등록 기준을 만드는 공개 경로 없음, 입력 배열 복사). 새 기준은 새 ID와 고정 가중치로 등록([ADR-005](../../docs/decisions/ADR-005.md) 결정 9). Low — `HiddenTendencyBoundaryTests`의 이름·주석을 실제 보장 범위(Application/Infrastructure 시그니처의 타입 노출)로 정리하고, 실제 직렬화 출력 검사는 Phase 3-D/5 DTO 연결 시점으로 인계. RED 7/232 실패(Codex 반례 포함) → GREEN. `dotnet test StarPlayer.sln -c Release` 813 통과(Domain 232, Simulation 371, Application 117, Infrastructure 93). Codex 재검토 `미실시`, 상태는 계속 `검토 대기`. 상세: [보고서](../../docs/reports/PHASE_3B1_REPORT.md) Part F.
- **변경 파일:** [PHASE_3B1_REPORT.md](../../docs/reports/PHASE_3B1_REPORT.md) §5. 결정: [ADR-005](../../docs/decisions/ADR-005.md). 계약: [game-engine.md §18](../../docs/game-engine.md).
- **테스트(실제 실행):** `dotnet test StarPlayer.sln -c Release` — 797 통과(Domain 216, Simulation 371, Application 117, Infrastructure 93; 기준 709에서 +88), 실패·건너뜀 0. 변형 검사 15건 모두 검출(처음 1건 생존 → 상·하한 각각 검증하는 테스트 추가 후 검출). `tools/verify.ps1` 결과는 보고서 Part 10. RED 로그는 없음(테스트가 타입 부재로 컴파일되지 않아 별도 RED 실행을 남기지 못함; 변형 검사로 대체).
- **미검증/미실시:** Phase 3 Gate 항목(100,000 PA, 100경기, 보정, 나머지 Pitch Zone Lite 검사), 수동 UI·FHD(UI 변경 없음), 데이터 계약의 독립 형식 검토(Codex 권장), 성능 측정.
- **이월/주의:** 12/17이 Borderline이라 분류가 거칠다 — Framing 크기는 3-E 상한이 담당하고 더 세밀한 표는 새 모델 버전(ADR-005 결정 2). `UniformV1`은 임시 기준 분포. 3-B2는 확률 척도·성향 상한·기준 분포·기준 확률을 보정 전에 확정해야 하며, 운영 KBO 프로필이 선언할 `CalledPitchMode`는 근거와 소유자 결정이 필요(구형 프로필은 미선언 유지).
- **승인자 또는 승인 근거:** (승인 이전 기록: 3-B1 미승인.) 최종: GPT Chat PASS WITH NOTES 승인 + Codex 집중 재검토 PASS WITH NOTES (2026-10-11, 소유자 전달 `TASK.md`).

## 최종 보완 라운드 (2026-10-11, `TASK.md` — Codex 독립 리뷰 2회 통합 수정)

> [!note] 상태
> 리뷰 상태는 계속 **`검토 대기`**입니다(승인 아님). Phase 3 전체 Gate는 **INCOMPLETE**. 아래 본문(A~I)의 수치·서술 중 이 블록과 다른 것은 1차 구현 당시 기록이며, 이 블록과 [PHASE_3A_REPORT.md Part F](../../docs/reports/PHASE_3A_REPORT.md)가 최신입니다.

- **이전 수정 지시 반영 여부 확인:** 반영된 것이 없음을 먼저 확인(리뷰 이후 `Randomness/` 소스 무변경)하고 결함 전부를 수정 전 코드에서 재현했습니다. 중복 수정 없음.
- **A. 누락 스트림 복원:** 소비된 스트림을 지운 스냅샷이 구조 검증을 통과해 처음 값부터 조용히 재시작하는 문제 → `Restore(snapshot, expectedStateHash)` 계약(커밋 시 라이브 상태 해시와 복원 결과 대조, `Rng.state_hash_mismatch`). 해시는 손상 검출용이며 인증 수단이 아님. Phase 4에서 게임 상태·영수증과 같은 저장 트랜잭션으로 확정. `rngset1` 정규 형식·Golden Vector 불변.
- **B. M1:** 사용 전(0회 소비) 스트림은 스냅샷에서 제외 → 조회만으로 `StateHash` 불변(`game/location` 조회 vs 미접근 일치 테스트).
- **C. `Atomically`:** 진행 중 원본 잠금(원본·사전 획득 스트림 draw, `Stream()`, 중첩 범위 거부), 복사본 객체 비공유·범위 종료 후 폐쇄, `finally` 잠금 해제. 보장 범위는 메모리 RNG 상태뿐(사건 ID·리듀서·영수증·영속 커밋 제외) — 문서화.
- **D. Draws 한도:** `NextUInt32` 상태 변경 전 검사, `NextBounded` 롤백, `Advance` 사전 검증.
- **E. 기타:** 비정규 정렬·미사용 항목 거부, `InvariantCulture` 명시, 보고서 rejection 설명 정정, 단일 작성자 계약 명시.
- **근거:** RED 24개 중 16개 실패(수정 전) → GREEN. Randomness 111개 통과, 전체 **709개**(Domain 145 · Simulation 371 · Application 114 · Infrastructure 79) 통과, 신규 방어 로직 변형 18/18 검출. [RED/GREEN 증거](../../docs/reports/PHASE_3A_RED_EVIDENCE.md).
- **Phase 3-C/4 인계(기록만, 미구현):** 사건 ID 순번과 리듀서 결과의 공동 원자성, 게임 상태·영수증·RNG의 성공 단위, 영속 커밋 성공 후에만 메모리 발행, `game/location` 소비 순서·확률 척도, `ef2`/리플레이 — [ADR-004](../../docs/decisions/ADR-004.md) 참조.

## A. 기본 정보

| 항목 | 내용 |
|---|---|
| Phase | Phase 03 — Seeded game simulation / Prototype 1 (작업 패키지 3-A) |
| 작업 시작일 | 2026-10-11 |
| 작업 완료일 | 3-A 구현·자체 검증 2026-10-11 (Phase 3 전체는 미완료) |
| 개발 담당 AI | Claude Code |
| 리뷰 상태 | **검토 대기** (3-A 한정; 최종 보완 라운드 후에도 동일) |
| 관련 ROADMAP | [Phase 3](../../ROADMAP.md#phase-3--seeded-game-simulation--prototype-1) |
| 관련 Git 브랜치 | main |
| 관련 커밋 | 없음 (기준 `b1e6b73`; 커밋·푸시는 별도 허가 전까지 하지 않음) |

- 대시보드: [Star Player 개발 대시보드](../00_Dashboard.md)
- 공식 현재 상태: [CURRENT_STATE.md](../../CURRENT_STATE.md)
- 보고서: [PHASE_3A_REPORT.md](../../docs/reports/PHASE_3A_REPORT.md) · 검증 로그: [PHASE_3A_VERIFY_LOG.md](../../docs/reports/PHASE_3A_VERIFY_LOG.md) · 결정: [ADR-004](../../docs/decisions/ADR-004.md) · 계약: [game-engine.md §17](../../docs/game-engine.md)

## B. 개발 목표

### 개발 목적

이후 확률 해석기(3-B~3-F)가 사용할 버전 고정 난수 기반을 만든다: PCG32, 스트림 키 유도, 스트림 독립성, 스냅샷, 원자적 작업 범위, 결정적 ID. 확률·위치 추첨·이벤트 변경은 하지 않는다.

### 구현 범위

- PCG32 XSH-RR(`pcg32-xsh-rr-v1`), 편향 없는 bounded 정수(rejection sampling), 소비 횟수
- SHA-256 기반 `kd1` 스트림 키 유도, 스트림 `game/pitch`·`contact`·`field`·`runner`·`location`·`ai`(모두 `@1`), `game/location` 규약 확정
- `rngset1` 스냅샷 생성·복원(버전·상태 검증), `Atomically` 작업 범위
- 결정적 GameId·사건 순번 ID

### 완료 기준 (TASK.md §2~§6 기준; Phase 3 Gate 아님)

- [x] PCG32 Golden Vector(독립 참조) — 공식 공개 출력 + Python 독립 구현
- [x] 스트림 독립성·재현성
- [x] Snapshot 생성·복원, 잘못된 버전·상태 거부
- [x] Rejection sampling 경계·거부 경로, 잘못된 입력 거부
- [x] 결정적 ID·중복 방지, ID 생성 시 RNG 비소비
- [x] 원자적 작업의 실패·취소 보존
- [x] Phase 2 전체 테스트 회귀 (598개 변경 없이 통과)
- [ ] **Phase 3 Common Gate G / Phase 3 Acceptance Criteria — 해당 없음(미평가, INCOMPLETE)**

### 관련 요구사항 및 시스템 계약

- 시스템 계약: [SYSTEM_CONTRACTS.md C08](../../docs/SYSTEM_CONTRACTS.md), ROADMAP §4.1 · §6.1 · §1.14 (Pitch Zone Lite 스트림 규약)

## C. 실제 구현 내역

### 구현한 기능

PCG32, 스트림 ID/키 유도, `GameRandom`(스트림 집합·스냅샷·복원·`Atomically`), `GameIds`/`GameEventIds`. 상세는 보고서 §1.

### 주요 변경 파일

| 파일 | 변경 내용 |
|---|---|
| `src/StarPlayer.Simulation/Randomness/*.cs` (8개) | 신규: 난수 기반 구현 |
| `tests/StarPlayer.Simulation.Tests/Randomness/*.cs` (5개) | 신규: 71개 테스트 |
| `tests/Fixtures/generator/generate_rng_golden.py`, `tests/Fixtures/phase3a_rng_golden.json` | 신규: 독립 Python 참조와 Golden Vector |
| `docs/decisions/ADR-004.md` | 신규: RNG 결정 |
| `docs/game-engine.md` §17, `docs/SYSTEM_CONTRACTS.md` C08 | RNG 요약·상태 문장 추가 |
| `docs/reports/PHASE_3A_REPORT.md`, `PHASE_3A_VERIFY_LOG*.md` | 신규 보고·검증 로그 |
| `CURRENT_STATE.md`, `Star/00_Dashboard.md` | 3-A 실제 상태 반영 |

### 새 모듈과 기존 모듈 변경

- 새 모듈: `StarPlayer.Simulation.Randomness` 네임스페이스(새 프로젝트 없음)
- 수정한 기존 모듈: 없음 (기존 소스·테스트·픽스처 무수정). 추적 외 파일 `TASK.md`와 PDF 2개 보존.

### 계획 대비 달라진 내용

- 지시서가 인용한 "Codex Phase 3 RNG 설계 결정" 문서는 저장소에 없다. ROADMAP·SYSTEM_CONTRACTS 규약에서 설계를 도출했다(ADR-004 머리말, 보고서 §6-2).

## D. 테스트 및 검증

### 실행 환경

| 항목 | 값 |
|---|---|
| OS | Windows 11 Home 10.0.26200 |
| Godot | 4.7.2 Mono |
| .NET SDK | 8.0.425 |
| 빌드 구성 | Release(솔루션·verify), Debug(필터 실행) |

### 실제 실행 결과

| 명령 | 기대 결과 | 실제 결과 | 상태 | 로그·결과 파일 |
|---|---|---|---|---|
| `dotnet test tests\StarPlayer.Simulation.Tests --filter "FullyQualifiedName~Randomness"` | 신규 테스트 통과 | 71 통과 / 0 실패 | PASS | 콘솔 |
| `dotnet test StarPlayer.sln -c Release` | Phase 2 회귀 포함 전부 통과 | Domain 145 · Simulation 331 · Application 114 · Infrastructure 79 = 669 통과 / 0 실패 / 0 건너뜀 | PASS | 콘솔 |
| `tools\verify.ps1 -LogName PHASE_3A_VERIFY_LOG.md` | `GATE: PASS` | `GATE: PASS`, exit 0 (2분 40초) | PASS | [로그](../../docs/reports/PHASE_3A_VERIFY_LOG.md) |
| `python -I tests/Fixtures/generator/generate_rng_golden.py` | 공개 출력 자체검증 후 fixture 생성 | 통과 | PASS | `tests/Fixtures/phase3a_rng_golden.json` |
| 의도적 소스 변형 22건 | 전부 테스트 실패로 검출 | 22/22 검출 (보고서 §5) | PASS | 보고서 §5 |

### 완료 기준별 충족 여부

| 완료 기준 | 상태 | 근거 |
|---|---|---|
| Golden Vector(독립 참조 비교) | 충족 | 공식 공개 출력(42/54 6값·주사위 33개) + Python 독립 구현 벡터. 한계: kd1/rngset1은 같은 작성자 해석 |
| 스트림 독립성 | 충족 | 역순 생성·교차 draw·타 스트림 추가 draw 테스트 |
| Snapshot | 충족 | 복원 후 출력 일치, 버전·상태 위조 거부 코드별 테스트 |
| Rejection sampling | 충족 | 거부 경로 벡터 포함, bound 0 거부, bound 1 소비 1회 |
| 결정적 ID | 충족 | 순수·단사·단조 증가·재개·소진 실패 |
| 원자성 | 충족 | 실패 결과·예외·취소(전/후) 시 원본 불변 |
| Phase 2 회귀 | 충족 | 598개 변경 없이 통과 |
| Phase 3 전체 Gate | **해당 없음 / INCOMPLETE** | 3-B 이후 미착수 |

### 테스트하지 못한 항목

- Phase 3 Gate 전 항목(100,000 PA, 100경기, 보정, Pitch Zone Lite 검사, UI) — 미착수 (`미검증`)
- Godot UI 수동 확인·FHD — 3-A에 UI 변경 없음 (`미검증`)
- 업스트림 C 데모 직접 실행 — C 컴파일러 없음 (공개 출력 비교로 대체)
- 성능 측정 — 미실시

## E. 발견된 문제

### 발생한 오류

- 소스 스캔 테스트의 주석 제거 정규식이 첫 `//` 이후 파일 전체를 지움 → 변형 점검에서 발견, 수정 후 금지 패턴 변형 3건으로 재검증.
- 변형 점검 스크립트 동시 실행으로 첫 출력이 섞임 → 중단·해시로 복원 확인·순차 재실행.

### 해결한 문제

- 위 두 건.

### 미해결 문제

- 없음(결함). 아래 기술 부채·주의 사항 참조.

### 기술 부채 및 향후 영향

- `kd1`/`rngset1` 형식은 독립 검토 전(보고서 §6-1). 확률 척도(정수 분모)는 3-B에서 결정. `game/location` draw 수는 3-B/3-C에서 확정. 스냅샷 영속화는 Phase 4. `GameEventIds` 채택은 3-C.

## F. 설계 변경

| 변경 내용 | 변경 이유 | 영향 모듈 | 결정 문서 | Codex 추가 검토 |
|---|---|---|---|---|
| RNG 기반 신규 설계(승인 범위 내) | Phase 3-A 구현 | Simulation | [ADR-004](../../docs/decisions/ADR-004.md) | 미실시 (저장 데이터 무결성·핵심 시뮬레이션 규칙이므로 권장) |

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

- 3-B는 소유자의 별도 승인 후에만 착수. `GameRandom` 사용 규칙은 [game-engine.md §17](../../docs/game-engine.md) 참조.

### 의존 관계

- 3-B(모델·튜닝 계약)는 ADR-004의 스트림·`game/location` 규약과 정수 확률 척도 결정에 의존한다.

### 주의할 문제 및 후속 작업

- 해석기는 `GameReducer.Apply`를 우회하지 않는다. 실패 가능한 단계는 `Atomically` 안에서 리듀서 호출과 함께 실행. Phase 2 후속 NOTES(C33·C38·C42·C44·D2·Phase 4 영속화)는 그대로 이월.

### 추가 검증 필요 사항

- `kd1`/`rngset1` 독립 검토(Codex 권장), 3-F의 100,000 PA·100경기·보정.

## I. 최종 결과

| 항목 | 결과 |
|---|---|
| 완료 기준 충족 여부 | 3-A 요구사항 충족(자체 검증). Phase 3 전체 Gate는 미평가 |
| 최종 리뷰 상태 | 검토 대기 |
| 남은 문제 | 기술 부채 목록(위 E) |
| 승인자 또는 승인 근거 | 3-A 한정: GPT Chat 최종 판정 PASS WITH NOTES (2026-10-11, 소유자 전달); Phase 3 전체는 미승인 |
| 최종 수정 날짜 | 2026-10-11 |

### Phase 종료 체크

- [x] ROADMAP 완료 기준과 Common Gate G 확인 (3-A 범위 외 항목은 INCOMPLETE로 명시)
- [x] 실제 구현·변경 파일과 Git 변경사항 기록
- [x] 실행한 테스트의 명령·환경·실제 결과 기록
- [x] 미검증 항목, 미해결 문제와 설계 변경 식별
- [x] 중요한 설계 변경의 결정 문서 연결 (ADR-004)
- [x] 리뷰 상태를 `검토 대기`로 변경
- [x] [CURRENT_STATE.md](../../CURRENT_STATE.md)와 실제 상태 일치 확인
- [x] GPT Chat 전달용 간략 요약 작성
- [x] 다음 Phase를 자동 시작하지 않음
