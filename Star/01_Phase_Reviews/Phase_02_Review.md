---
type: phase-review
phase: "02"
phase_name: "Baseball state transitions and scoring"
status: 승인 완료
started: "2026-10-10"
completed: "2026-10-10"
developer_ai: Claude Code
reviewed_by: "GPT Chat 최종 검토 PASS WITH NOTES (사용자 전달, 2026-10-11)"
branch: "main"
commit: "미커밋 (기준 커밋 64167f788dc6a77fe4725bb13e06d946fd122ada 위의 작업 트리에만 존재)"
roadmap_anchor: "phase-2--baseball-state-transitions-and-scoring"
last_updated: "2026-10-11"
codex_review: "1차 CHANGES REQUIRED(High 3, Medium 1) → 보완 → 재검토 CHANGES REQUIRED(High 1, Medium 1) → 최종 보완(Medium은 승인된 범위 제외) → 최종 보완 후 재검토 미실시 → C44 다중 실책 Medium 보완 → Codex 최종 PASS WITH NOTES (2026-10-11, 사용자 전달)"
tags:
  - star-player
  - phase-review
---

# Phase 02 Review — Baseball state transitions and scoring

> [!warning] 상태 규칙
> 허용 상태는 `초안`, `검토 대기`, `승인 완료`, `보완 필요`입니다. 구현 AI는 외부 검토 없이 `승인 완료`로 변경하지 않습니다. 실행하지 않은 검증은 `미검증`, 수행하지 않은 독립 리뷰는 `미실시`로 기록합니다.

## 최종 승인 기록 (2026-10-11)

> [!success] Phase 2 — `PASS WITH NOTES` / `승인 완료`
> **최종 승인 (2026-10-11): Phase 2 = `PASS WITH NOTES` / `승인 완료`.** 근거(GPT Chat 최종 검토 결과를 사용자가 전달): xUnit 598개 PASS(Domain 145 · Simulation 260 · Application 114 · Infrastructure 79), 스크립트 경기 130/130(100경기 + C33 30경기), 박스스코어 검산 200/200, `verify.ps1` 연속 2회 `GATE: PASS`, Codex 최종 `PASS WITH NOTES`, 사용자 Windows Export 수동 UI 검증 완료(사용자 보고, 세부 기록 없음), 기존 NOTES와 승인된 GameRules 정책 유지. 승인 주체는 GPT Chat/사용자의 기록된 검토이며 Claude Code가 승인한 것이 아니다. 기존 테스트 결과와 Codex 리뷰 이력은 변경하지 않았다. Phase 3은 **미착수**이며 소유자의 별도 지시 전에는 시작하지 않는다. 커밋·푸시는 별도 허가 전까지 하지 않는다. 후속 NOTES(보존): ① **C33** — 슬롯 장부 설계의 기록된 한계(OBR 9.16(g) 예시 7 동등 시나리오, 모호한 복합 야수선택 `Game.ambiguous_responsibility` 거부는 승인된 지원 범위 제한); ② **C38** — 희생플라이 변형(9.08(d)) 미지원, Gate 비필수 후속 이월, Phase 3 재평가; ③ **C42** — `ReachedOnError` 직접 원인 판정 `NOT_MODELLED`; ④ **C44** — 1루 주자가 있는 안타의 설명 불가 실책 및 실책 2개 이상 거부(입력 표현 한계에 따른 승인된 Phase 2 지원 범위 제한, GPT Chat 설계 판단 승인 · 소유자 직접 승인 별도 기록 없음, KBO/OBR 규칙·인증 아님); ⑤ **D2 정책** — 운영 `GameRules` 부분 집합은 KBO 규정집·기록규칙 미인증 상태로 유지(연장 11회 한도·R02 운영 근거만 확인), 커리어 사용 전 증거 기반 프로필로 교체; ⑥ **Phase 4 과제** — 엔진 규칙 버전(프로필 해시가 규칙 동작 변경을 드러내지 못함)과 `game_states`·`scored_plays`·규칙 프로필 해시 영속화. FHD 확인은 별도 보고가 없어 `미검증`으로 남는다.
>
> 이 문서의 아래 내용은 검토 이력이다. 본문의 "검토 대기", "미승인", "Gate INCOMPLETE", "재검토 미실시" 표기는 **당시 상태**이며 이 최종 승인 기록과 아래 "I. 최종 결과"가 최신이다.

## A. 기본 정보

| 항목 | 내용 |
|---|---|
| Phase | Phase 02 — Baseball state transitions and scoring |
| 작업 시작일 | 2026-10-10 |
| 작업 완료일 | 2026-10-10 (자체 검증 종료); R02·OBR 원문 대조 보완 라운드 2026-10-11 |
| 개발 담당 AI | Claude Code |
| 리뷰 상태 | **승인 완료 — PASS WITH NOTES (2026-10-11)** (이전 상태 `검토 대기`는 아래 이력에 보존) |
| 관련 ROADMAP | [Phase 2](../../ROADMAP.md#phase-2--baseball-state-transitions-and-scoring) |
| 관련 Git 브랜치 | main |
| 관련 커밋 | 없음 (기준: `64167f7`; 승인 전 커밋·푸시하지 않음) |
| 독립 Codex 리뷰 | 1차 `CHANGES REQUIRED` (High 3, Medium 1, 끝내기 잔루) → 보완 → **재검토 `CHANGES REQUIRED` (High 1, Medium 1)** → 최종 보완 완료, 최종 보완 후 재검토 `미실시` — 아래 "Codex 리뷰 이력과 보완" 참조 |

- 대시보드: [Star Player 개발 대시보드](../00_Dashboard.md)
- 공식 현재 상태: [CURRENT_STATE.md](../../CURRENT_STATE.md)
- 보고서: [PHASE_2_REPORT.md](../../docs/reports/PHASE_2_REPORT.md) (A: Codex 보완, B: 1차 보고) · Codex 1차 보완 후 검증 로그: [remediation1](../../docs/reports/PHASE_2_VERIFY_LOG_remediation1.md), [remediation1_run1](../../docs/reports/PHASE_2_VERIFY_LOG_remediation1_run1.md) · 최종 보완(2026-10-11) 후 검증 로그: [PHASE_2_VERIFY_LOG.md](../../docs/reports/PHASE_2_VERIFY_LOG.md), [run1](../../docs/reports/PHASE_2_VERIFY_LOG_run1.md) · 1차 검증 로그(보존): [initial](../../docs/reports/PHASE_2_VERIFY_LOG_initial.md), [initial_run1](../../docs/reports/PHASE_2_VERIFY_LOG_initial_run1.md) · 보완 1차 시도(문서 링크로 FAIL, 보존): [try1](../../docs/reports/PHASE_2_VERIFY_LOG_remediation_try1_FAIL_doc_links.md) · 계약: [game-engine.md](../../docs/game-engine.md) · 결정: [ADR-003](../../docs/decisions/ADR-003.md)

## Codex 리뷰 이력과 보완 (CHANGES REQUIRED 보존)

> [!warning] 원래 판정 보존
> 1차 자체 검증(xUnit 414개 통과, verify 연속 2회 PASS) 이후 독립 Codex 리뷰가 **`CHANGES REQUIRED`**를 반환했습니다. 이 판정은 삭제하지 않으며,
> 아래 보완 후에도 보완 후 Codex 재검토는 **미실시**입니다. 승인 상태는 `검토 대기`입니다.

| # | 심각도 | 지적 | 보완 | 재검증 |
|---|---|---|---|---|
| 1 | High | 제3아웃·득점 판정이 선언된 `OutKind`와 스코어러 플래그(`TimingPlayRuns`)에 의존, 아웃/득점 순서·실제 포스 여부가 없음 | 아웃에 종류(필수)·순서·아웃 베이스를 부여하고 포스 여부를 상태에서 도출(선언 불일치 거부), 제3아웃 분류로 득점 판정(타자주자/포스아웃은 전부 무효, 그 밖은 앞선 득점만 인정), 자책 재구성이 같은 규칙 재사용, 무효 득점은 기록에 미반영 | `ThirdOutAndOrderTests` 16, `CodexReproTests` 1-2, 100경기(양방향 순서 포함) |
| 2 | High | 이벤트 ID가 같으면 내용이 달라도 중복으로 처리 | 영수증(ID+종류+`ef1` SHA-256 지문), 같은 ID·같은 내용=중복, 다른 내용=`Game.event_id_conflict`, 직렬화 계약(이벤트·영수증 JSON) | `EventIdentityTests` 10, `ReplayContractTests` 5, `CodexReproTests` 3 |
| 3 | High | 팀별 지명타자 유지/소멸 상태 부재(규칙의 DH 유무만 사용) | `TeamGameState.DhActive`, `DhMayBeForfeited`(KBO 지향 프로필은 끔, 가상 프로필만 켬), 허용 전환 2종·재활성화 거부·팀 독립·투타 겸업 전환 | `DhStateTests` 13, `ReplayContractTests` |
| 4 | Medium | 몸에 맞는 공에 폭투/포일 결합 허용 | 볼 데드 검증과 전용 오류 코드, 허용 진루는 강제 진루뿐 | `HitByPitchTests` 8, `CodexReproTests` 4 |
| 5 | 추가 | 끝내기 시 잔루를 무조건 0으로 기록 | 프로필 규칙 `CountWalkOffRunnersAsLeftOnBase`(종료 시점 누상 주자를 팀 잔루로), 개인 잔루 미기록, 비인증 단순화로 명시 | `WalkOffLeftOnBaseTests` 8 |

보완 후 확인: 기존 414개 중 408개는 수정 없이 통과, 2개는 폐지된 플래그 때문에 삭제, 4개는 설계 변경에 맞춰 수정(상세: 보고서 A.5). 전체 xUnit **476개**
(Domain 145 · Simulation 156 · Application 114 · Infrastructure 61). 엔진 변이 점검 13건 모두 검출. KBO 공식 페이지 5곳 확인: **연장 11회 한도만 공식 출처
확보**(KBO 보도자료 2025-01-22), 나머지는 미인증 유지 → **Gate는 계속 INCOMPLETE**.

### 재검토 `CHANGES REQUIRED` (2026-10-11) 와 최종 보완 — 이전 판정 보존

| # | 심각도 | 지적 | 처리 | 재검증 |
|---|---|---|---|---|
| 6 | High | 타자주자가 안타로 1루에 도달한 뒤 추가 진루 중 아웃된 상황을 표현하지 못함(도달 전/후 제3아웃 구분) | **수정**: 안타 결과에 타자 이동(To=0) 허용, 도달 증거는 `OutAt`(안타가 준 베이스보다 뒤), 태그(비포스) 아웃, 도달 전 아웃은 득점 무효·도달 후 아웃은 앞선 득점 인정, 한 타석 한 번 집계, 안타+아웃 2개는 병살 아님, 자책 재구성·Python 생성기 동일 반영 | `BatterRunnerAfterHitTests` 9, `ReplayContractTests`, 100경기(후 도달 주루사 116건, 제3아웃+순서 50건), 변이 점검 8건 모두 검출 |
| 7 | Medium | 선행 주자 어필 제3아웃의 득점 무효 처리 부족 | **승인된 범위 제외**(사용자 결정 2026-10-11): 어필 플레이 미구현, 입력되면 `Game.appeal_not_supported`로 거부, 일반 태그아웃으로 바꾸지 않음. 구현이 아니라 게임 단순화임을 문서화 | `AppealExclusionTests` 6 |
| 8 | 정책 | KBO 규칙 우선·2026 OBR 보완 정책 확정 | `content/rules/game_rules_evidence.json`(26개 규칙, 상태 4종), `RulePolicyId`, 프로필 필드 분리(`DhMayBeForfeited`=OBR식, `FielderToMoundForfeitsDh`=가상 전용), `RuleEvidenceTests` | `RuleEvidenceTests` 5 |

결과(당시): xUnit **500개**(Domain 145 · Simulation 174 · Application 114 · Infrastructure 67), verify 연속 2회 PASS. Gate: **필수 규칙 R02(연장 11회 한도 도달 시 무승부 처리)가 `UNRESOLVED`** → INCOMPLETE 유지.
OBR 2026 원문 PDF와 KBO 기록규칙 PDF는 파일 다운로드로 응답해 읽지 못했고, MLB 용어집 페이지에서 읽은 내용만 근거로 썼으며 OBR 조항 번호는 인용하지 않았습니다. *(아래 2026-10-11 보완 라운드가 R02와 OBR 원문 부분을 대체합니다. 이 문장은 이력으로 보존.)*

### R02 결정과 OBR 원문 대조 보완 (2026-10-11, GPT Chat 검토 결과 `TASK.md` 반영)

> [!info] 승인 판정이 아님
> 아래는 GPT Chat이 전달한 보완 지시(R02 적용 승인, OBR 원문 대조, DH 분류 재평가)의 이행 기록입니다. 이 라운드에도 최종 승인 판정은 없으며 상태는 `검토 대기`입니다.

| # | 항목 | 처리 | 근거·검증 |
|---|---|---|---|
| 9 | R02 적용 승인 | `UNRESOLVED` → `GAME_SIMPLIFICATION_APPROVED`(`clause_certified=false`). 근거: KBO 이사회 발표 2025-01-22 + KBO 공식 뉴스 2026-06-03 한화-두산 3-3 11회 무승부(+보도 2026-10-04 KT-롯데 2-2). **규정 조문은 미확인**임을 별도 필드로 명시. `gate_blockers` 비움 | `RuleEvidenceTests` 8 (R02 문구 테스트 포함) |
| 10 | OBR 원문 대조 | `docs/2026-official-baseball-rules.pdf`(SHA-256 `f85860f3…`, 192쪽)를 읽고 5.08(a), 5.11(a)/(b), 9.03(c), 9.04, 9.16 등 38행 대조: 일치 16 · KBO 우선 1 · 승인된 단순화 9 · 확인 대기 단순화 3 · 미구현/거부 3 · **실제 미해결 편차 6**(C12·C14·C19·C24·C32·C34) | 증거 파일 `obr_comparison`; C19는 `BoxProofTests`로 측정(200팀경기 중 70건 박스 증명 불성립, 무효 득점 수를 더하면 0건) |
| 11 | DH 분류 재평가 | `fielder_to_mound_forfeits_dh`는 OBR **5.11(a)(14)** 원문 조항 → "가상/임의"는 오분류였음. 규칙 R27(`OBR_FALLBACK_APPROVED`)로 정정, **KBO 지향 프로필에서 켬**(런타임 변경은 이 플래그 한 개). 5.11(b) 투타 겸업 예외도 대조(진입 경로가 원문보다 넓음: C32). 빈 수비 위치를 DH가 채우는 방식은 엔진 단순화 C29(소유자 확인 대기) | `RuleEvidenceTests` 프로필 플래그 테스트, `DhStateTests` 15 |
| 12 | 퓨처스 승부치기 | 공지 11795의 "승부치기 10~12회, 이후 무승부"는 ▲퓨처스리그 항목 안의 문장 → 1군 근거로 쓰지 않음. 별도 비필수 규칙 R28 | 증거 파일 R28 |

결과(당시): xUnit **504개**(Domain 145 · Simulation 174 · Application 114 · Infrastructure 71). 전체 `tools/verify.ps1`은 이번 라운드 1회 실행 PASS(아래 "실제 실행 결과" 표의 R02·OBR 라운드 행). **Gate는 `INCOMPLETE` 유지**: R02 블로커는 해소되었으나 필수 규칙 영역의 미승인 편차 6건(5건이 Gate 필수 영역)과 KBO 규정집·기록규칙 미열람(D2 규칙 인증 미완)이 남음. *(편차 6건은 아래 "OBR 편차 6건 수정 라운드"에서 수정됨.)* *R02 해소와 D2 규칙 적용·인증 상태는 별개입니다.*

### OBR 편차 6건 수정 라운드 (2026-10-11, GPT Chat 지시 "OBR 편차 처리 최종 결정")

> [!info] 승인 판정이 아님
> 소유자(GPT Chat 지시)가 R02 유지와 편차 6건 전부 수정, C29 비승인, C33·C38 승인 보류를 지시했고 그 이행 기록입니다. 최종 승인 판정은 없으며 상태는 `검토 대기`입니다. R02는 이번 수정 대상이 아니며 퓨처스 R28과 섞지 않았습니다.

| # | 항목 | 수정 | 수정 전 → 후 반례 | 검증 |
|---|---|---|---|---|
| 13 | C12 병살 | GIDP·타점 불인정은 땅볼 포스/역포스 병살만, 수비 DP 크레딧은 분리 | 포스가 아닌 병살 GIDP 1 → 0, 비포스 병살 뒤 득점 타점 0 → 1 | `ObrDeviationTests` 5 (수정 전 2 실패) |
| 14 | C14 구원 자책 | 투수별 등판 시점 재구성, `TeamER` 신설 | OBR 예시 1 구원 투수 ER 0 → 1, 예시 3 팀 자책 → 2 | 공식 예시 3개 수작업 오라클 (수정 전 2 실패) |
| 15 | C19 박스 증명 | 무효 득점 주자를 러너별 1회 잔루로 도출(무조건 +1 아님), 검산기 추가 | 잔루 1 → 2, 100경기 200팀경기 중 70건 불성립 → 0 | `BoxProofTests` 200/200 정확, 수작업 3 (수정 전 2 실패) |
| 16 | C24 DH 첫 타석 | `Game.dh_must_bat`, `StartingDh` | 첫 타석 전 대타 허용 → 거부(상태 불변) | 3 (수정 전 1 실패) |
| 17 | C32 투타 겸업 | 일반 DH가 투수가 되면 DH 소멸(또는 거부), 예외는 선발 투수=선발 DH | DH 유지한 채 투수 → 소멸/거부 | 6 (수정 전 3 실패) |
| 18 | C34 볼넷 책임 | 2-0, 2-1, 3-0, 3-1, 3-2 | 3-2·2-0·2-1 구원 책임 → 전임 투수 | 이론 테스트 11 (수정 전 3 실패) |
| 19 | C29 | 자동 배치 제거, `Slot` 명시 요청만 처리, 나머지 거부 | 말 없이 DH 자동 배치 → `dh_vacated_position_unspecified/invalid` | 1 + JSON 재생 1 (수정 전 1 실패) |
| 20 | C33·C38 | **변경 없음, 승인 보류**. 원문·차이·영향·권장·Gate 필수 여부를 증거 파일과 보고서 A4.4에 기록 | — | — |

결과: `tools/verify.ps1` 연속 2회 PASS(종료 코드 0·0), xUnit **540개**(Domain 145 · Simulation 207 · Application 114 · Infrastructure 74), 100경기 30,974 사건 재생성 100/100 일치(독립 오라클이 아닌 통합 확인), 박스 증명 200/200. 수정 전 재현 테스트는 수정되지 않은 엔진에서 15개 실패(실질 14개, 1개는 기대값 산술 실수를 고친 것).
`gate_blockers`(규칙 출처 차단)는 비어 있고 구현 결함은 별도 목록으로 관리합니다: **미해결 편차 0건**, 당시 소유자 결정 대기 C33(Gate 필수 규칙 R17)·C38(필수 아님) — 둘 다 아래 C33 라운드에서 결정·이행됨. 따라서 Gate는 선언하지 않습니다.

### C33 최종 기록 책임 보완 라운드 (2026-10-11, GPT Chat 검토 결과 `TASK.md`)

> [!info] 승인 판정이 아님
> GPT Chat이 C33을 최종 승인 전 필수 수정 대상으로, C38을 Gate 비필수 후속 구현으로 결정했고 그 이행 기록입니다. 상태는 `검토 대기`이며 Gate는 선언하지 않습니다. 커밋·푸시·Phase 3는 하지 않았습니다.

| # | 항목 | 수정 | 수정 전 → 후 | 검증 |
|---|---|---|---|---|
| 21 | C33 승계 주자 책임 (OBR 9.16(g)·(h)·(i), 9.17(d)) | 주자 개인 책임 투수 → **책임 슬롯 장부**(`RunResponsibility`, `GameReducer.Responsibility.cs`): 야수선택으로 주자가 아웃되면 타자가 슬롯 승계, 그 밖의 아웃은 슬롯 소멸, 득점은 득점 주자·루상 잔류 주자 중 가장 앞선 투수의 슬롯에 1점씩 청구 | 공식 주석 예시 2·3·5·6이 구원 투수 책임(예시 5: 0/0/3 → 1/1/1, 예시 6: 0/2 → 1/1), 패전투수도 어긋남 | `ResponsibilityLedgerTests` 17개 중 8개가 수정 전 실패 → 전부 통과, [실패 로그 (요약: PHASE_2_RED_EVIDENCE.md, 원본 `PHASE_2_C33_RED.log` 로컬 보존)](../../docs/reports/PHASE_2_RED_EVIDENCE.md) |
| 22 | 예시 7 | 타자의 송구 진루는 사건 모델에 없음 → 도루로 같은 위치를 만든 동등 시나리오로 검증(미지원 범위로 보고, Gate 영향 없음) | — | 테스트 1 |
| 23 | 100경기·추가 세트 | Python 채점기에 같은 규칙 반영, 100경기 바이트 동일 재생성(그러나 슬롯 이동 득점 0건 → 회귀 확인일 뿐), 슬롯 이동이 있는 30경기(33점) 추가 | 장부를 끄면 30경기 비교 99건 불일치 | `ResponsibilityScriptedGamesTests` 4개 |
| 24 | C38 | 소유자 승인 Gate 비필수 이월: 기본 희생플라이만 유지, 변형은 미지원 명시, Phase 3에서 재평가. 구현하지 않음 | — | 증거 파일 `APPROVED_SIMPLIFICATION`, `gate_required=false` |

결과: xUnit **561개**(Domain 145 · Simulation 224 · Application 114 · Infrastructure 78) 실패 0, 박스 증명 200/200, `tools/verify.ps1` 연속 2회 PASS(종료 코드 0·0, 경고 0, SQLite 8/8, 경기 진단 6개, Export 신규, 격리 실행 QHD, 문서 링크 40·41개): [run1](../../docs/reports/PHASE_2_VERIFY_LOG_c33_run1.md), [run2](../../docs/reports/PHASE_2_VERIFY_LOG_c33.md). 구현 결함(Gate 필수) 0건, 규칙 출처 차단 0건. 기록된 한계: 예시 7의 타자 송구 진루·타자주자 방해 아웃 미지원, 야수선택으로 서로 다른 투수의 주자 둘이 아웃되면 거부(`Game.ambiguous_responsibility`, 아래 마지막 High 수정; 이 행의 당시 선택 "가장 앞선 투수의 슬롯 승계"는 폐기), 프로필 해시는 규칙 변수만 포함하므로 득점 청구 방식 변경이 해시에 드러나지 않음(Phase 4 저장 시 엔진 규칙 버전 필요). 설계: [game-engine §4b·§13](../../docs/game-engine.md), [ADR-003 33](../../docs/decisions/ADR-003.md). Codex 재검토는 미실시(C12·C14·C19·C33 권장).

### C33 마지막 High 수정 (2026-10-11, Codex 집중 재검토 `CHANGES REQUIRED`)

> [!info] 승인 판정이 아님
> Codex 집중 재검토가 일반 슬롯 장부와 OBR 예시 1~6은 정상이나 야수선택 한 번에 서로 다른 투수의 주자 둘이 아웃될 때 슬롯을 임의 선택한다는 High를 지적했고, 소유자가 명시적 거부를 지시했습니다. 상태는 `검토 대기`, Gate 미선언, 커밋·푸시·Phase 3 없음.

| # | 항목 | 수정 | 수정 전 → 후 | 검증 |
|---|---|---|---|---|
| 25 | 모호한 복합 야수선택 | `FinishPlay`에서 상태 생성 전(검증·순서 해석 직후)에 `Game.ambiguous_responsibility`로 거부. 조건: 야수선택 + 타자 출루 + 주자 둘 이상 아웃 + 아웃된 주자의 책임 투수가 둘 이상. 같은 투수 주자들·주자 한 명 아웃·다른 결과는 영향 없음. **소유자 승인에 따른 Phase 2 지원 범위 제한이며 OBR/KBO 규칙 진술이 아님** | Codex 재현(Peter 2루·Roger 1루·Sierra 투구, 1루 포스아웃+2루 홈 태그아웃): 수락 후 슬롯 임의 승계 → 오류, 원본 상태 동일 인스턴스, 슬롯·아웃·득점·영수증 불변, 재전달 시 같은 오류 | 신규 7개(재현·순서 두 가지·대체 입력·같은 투수·우회 경로·독립 주루 아웃·멱등성), 수정 전 3개 실패([로그 (요약: PHASE_2_RED_EVIDENCE.md, 원본 `PHASE_2_C33_AMBIGUOUS_RED.log` 로컬 보존)](../../docs/reports/PHASE_2_RED_EVIDENCE.md)) |

결과: xUnit **568개**(Domain 145 · Simulation 231 · Application 114 · Infrastructure 78) 실패 0, 공식 예시 1~6·예시 7 대체·C33 30경기·100경기·박스 증명 200/200 유지, `tools/verify.ps1` 연속 2회 PASS(종료 코드 0·0, 경고 0, SQLite 8/8, 경기 진단 6개, Export 신규, 격리 실행 QHD, 문서 링크 42·43개): [run1](../../docs/reports/PHASE_2_VERIFY_LOG_c33_amb_run1.md), [run2](../../docs/reports/PHASE_2_VERIFY_LOG_c33_amb.md). 우회 경로 점검: 슬롯이 타자에게 넘어가는 경로는 `FieldersChoice` 하나뿐이며, 같은 플레이를 안타·실책 출루로 입력하면 기록원의 다른 분류(야수선택 아님)로 두 슬롯이 모두 소멸합니다. 설계: [game-engine §14](../../docs/game-engine.md), [ADR-003 34](../../docs/decisions/ADR-003.md). 남은 위험: 이런 플레이는 입력 모델이 야수선택에 해당하는 아웃을 지목할 수 있을 때까지 기록 불가, 예시 7 타자 송구 진루·타자주자 방해 아웃 미지원, 프로필 해시 불변(Phase 4 엔진 규칙 버전), Codex 재검토 미실시.

### 안타·포스아웃 정합성 High 수정 (2026-10-11, Codex 최종 집중 리뷰 `CHANGES REQUIRED`)

> [!info] 승인 판정이 아님
> 상태 `검토 대기`, Gate 미선언. 커밋·푸시·Phase 3 없음. 근거: 2026 OBR 9.05(b)(1) 첫 절(KBO 우선·OBR 보완 정책 적용, **KBO 인증 아님**).

| # | 항목 | 수정 | 수정 전 → 후 | 검증 |
|---|---|---|---|---|
| 26 | 포스아웃이 있는 타구의 안타 | `FinishPlay`에서 순서 해석 직후·상태/영수증 생성 전에, 결과가 `Single/Double/Triple/HomeRun`이고 상태에서 도출된 `OutClassification.Force` 아웃이 있으면 `Game.hit_with_force_out`으로 거부. 태그로 잡은 강제 주자도 포스아웃, 앞선 아웃으로 풀린 포스는 존중. 결과 자동 재분류 없음 | Codex 재현을 `Single`로 입력 시 안타·타수 기록(C33 거부 우회) → 오류, 입력 상태 인스턴스 동일, 기록·영수증 불변, 재전달 시 동일 오류 | `HitWithForceOutTests` 10개(theory 포함), 수정 전 6개 실패([로그 (요약: PHASE_2_RED_EVIDENCE.md, 원본 `PHASE_2_HIT_FORCE_RED.log` 로컬 보존)](../../docs/reports/PHASE_2_RED_EVIDENCE.md)) |
| 27 | `ReachedOnError` | 실책이 타자 출루의 직접 원인인지 판정할 정보가 없음을 명시(인증도 일괄 거부도 아님), 증거 행 C42 `NOT_MODELLED`. C33 오류 메시지·엔진 문서의 "다른 결과로 기록" 문구는 실제 플레이의 공식 분류일 때만 가능하다고 정정 | 선행 주자 포스아웃 + `ReachedOnError` 수락(재현) | 문서·증거 행 |
| 28 | 추가 확정 차이 | 9.05(b)(1) 둘째 절("실책이 없었다면 포스아웃"): `ErrorSavedOut` 주자가 있는 `Single`이 안타로 기록됨을 재현. 이 라운드에서는 보고만 했고 **아래 C43 최종 보완 라운드에서 수정** | — | 재현 확인(임시 테스트) |

결과: xUnit **578개**(Domain 145 · Simulation 241 · Application 114 · Infrastructure 78) 실패 0, 공식 예시 1~6·예시 7 대체·모호 야수선택 거부·C14·C19(박스 200/200)·지문/영수증/JSON 재생·100경기+30경기 유지, `tools/verify.ps1` 연속 2회 PASS(종료 코드 0·0, 경고 0, SQLite 8/8, 경기 진단 6개, Export 신규, 격리 실행 QHD, 문서 링크 44·45개): [run1](../../docs/reports/PHASE_2_VERIFY_LOG_hit_force_run1.md), [run2](../../docs/reports/PHASE_2_VERIFY_LOG_hit_force.md). 설계: [game-engine §15](../../docs/game-engine.md), [ADR-003 35](../../docs/decisions/ADR-003.md). 남은 위험: C43 미수정, C42 한계, 안타 부정 규칙 중 판단·어필·방해가 필요한 항목 미판정, 프로필 해시 불변(Phase 4 엔진 규칙 버전), Codex 재확인 미실시.

### C43 최종 보완 (2026-10-11, `TASK.md`)

> [!info] 승인 판정이 아님
> 상태 `검토 대기`, Gate 미선언. 커밋·푸시·Phase 3 없음. 근거: 2026 OBR 9.05(b)(1) 둘째 절(KBO 우선·OBR 보완 정책 적용, **KBO 인증 아님**).

| # | 항목 | 수정 | 수정 전 → 후 | 검증 |
|---|---|---|---|---|
| 29 | C43 실책이 없었다면 포스아웃이었을 주자의 안타 | `FinishPlay`(순서 해석 직후, 상태·영수증 전)에서 `Single/Double/Triple`에 (1) 기록원이 `ErrorSavedOut`으로 명시한 주자의 아웃이 상태에서 도출된 포스였을 것이면 `Game.hit_with_force_out_saved_by_error`, (2) 1루 주자가 있고 실책이 기록됐는데 실책 원인 이동이 하나도 없으면 `Game.hit_error_cause_unclear`로 거부. 앞선 아웃으로 포스가 풀리면 안타 유지. 결과 자동 재분류 없음 | 1루 주자 `ErrorSavedOut` + `Single`이 안타·타수로 기록 → 오류, 상태 인스턴스 동일, 기록·영수증 불변, 재전달 시 동일 오류 | `HitErrorForceTests` 11개 + `HitErrorReplayTests` 1개, 수정 전 6개 실패([로그 (요약: PHASE_2_RED_EVIDENCE.md, 원본 `PHASE_2_C43_RED.log` 로컬 보존)](../../docs/reports/PHASE_2_RED_EVIDENCE.md)) |
| 30 | 정상 안타 보호 | 1루 주자 없음, 실책 없음, 실책이 실책 원인 이동으로 설명됨, 비포스 아웃을 면한 주자, 포스가 풀린 경우는 안타 유지 | — | 같은 테스트 |
| 31 | 규칙 (2)의 승인 상태 | **Phase 2 승인 지원 범위 제한(2026-10-11 GPT Chat 설계 판단으로 승인, 사용자가 TASK.md로 전달; 소유자 직접 승인은 별도 기록 없음, Claude Code 자체 승인 아님)**: 증거 행 C44 `NOT_MODELLED`. 결정: 1루 주자가 있는 안타의 설명 불가 실책은 명시 거부, `ErrorSavedOut`+검증된 포스아웃은 기존 C43 검증이 거부, 정상 안타의 `ErrorExtraBase` 등 지원 사건은 유지, 모든 안타+실책 일괄 거부 아님, 원인 미확인 실책을 다른 사건으로 재분류하지 않음. KBO 공식 인증·OBR 완전 구현 아님, C42는 `NOT_MODELLED` 유지. 구별되지 않아 제한되는 합법 사건: 1루 주자가 있는 안타에서 모델이 표현하지 못하는 효과의 실책(예: 타자의 송구 진루)이 어떤 이동으로도 설명되지 않는 경우 | — | 문서·증거 |

결과: xUnit **590개**(Domain 145 · Simulation 252 · Application 114 · Infrastructure 79) 실패 0, C41·C33·C14·C19(박스 200/200)·영수증/JSON 재생·100경기+30경기·Phase 0~1 유지, `tools/verify.ps1` 연속 2회 PASS(종료 코드 0·0, 경고 0, SQLite 8/8, 경기 진단 6개, Export 신규, 격리 실행 QHD, 문서 링크 46·47개): [run1](../../docs/reports/PHASE_2_VERIFY_LOG_c43_run1.md), [run2](../../docs/reports/PHASE_2_VERIFY_LOG_c43.md). C42(`ReachedOnError` 직접 원인)는 `NOT_MODELLED` 유지. 설계: [game-engine §16](../../docs/game-engine.md), [ADR-003 36](../../docs/decisions/ADR-003.md). 열린 OBR 차이 0건, Gate 필수 구현 결함 0건, Gate는 INCOMPLETE 유지(Codex 재확인·수동 검증·D2 인증·C44는 승인 지원 범위 제한으로 기록됨, Phase 2 최종 승인은 미결).

### Codex 최종 리뷰 `PASS WITH NOTES` (2026-10-11, 사용자 전달) — 마무리 2건

- **판정:** Codex 최종 리뷰 `PASS WITH NOTES`, 기능 결함 0건, 추가 코드 리뷰 없음. 이 문서는 전달된 결과를 기록할 뿐 Claude Code가 판정한 것이 아니다.
- **노트 1:** `GameReducer.Finish.cs` 41행 주석이 "원인 이동으로 설명된 실책은 의심하지 않음"이라는 옛 정책을 말하고 있었다 → 실제 C44 정책(원인 이동 하나로 모든 실책이 설명됐다고 판단하지 않음, 1루 주자가 있는 안타와 복수 실책은 인과 불명확으로 거부, 실책 1개+표시 이동은 기록원 진술)으로 수정. 주석만 변경, 코드·규칙·테스트 변경 없음.
- **노트 2:** `.gitignore`의 `*.log` 때문에 RED 원본 로그가 Git에 올라가지 않음 → 저장소 전체 `.log` 무시 정책은 유지, 원본 로그 5개는 로컬에 그대로 보존, 추적 가능한 요약 [PHASE_2_RED_EVIDENCE.md](../../docs/reports/PHASE_2_RED_EVIDENCE.md)(줄 수, SHA-256, 실패 테스트 이름, 요약 줄, 실패 메시지, C44 GREEN 실행)를 만들고 보고서·리뷰·ADR·엔진 문서·CURRENT_STATE·증거 파일의 참조를 이 문서로 바꿈. 링크 확인 완료.
- **상태:** 기존 테스트 결과와 GameRules 정책은 변경 없음. Phase Gate는 사용자 수동 UI 검증 전까지 **`검토 대기`** 유지(`승인 완료` 아님). 커밋/푸시 없음, Phase 3 미착수.

### C44 다중 실책 보완과 C29 ADR 정리 (2026-10-11, Codex 집중 리뷰 `CHANGES REQUIRED` Medium, `TASK.md`)

- **지적:** `FinishPlay`가 실책을 설명하는 이동이 하나라도 있으면 나머지 실책도 설명된 것으로 취급했다. 사건 모델에는 실책과 그 원인 이동의 1:1 연결 정보가 없다.
- **수정:** 플레이 시작 시 1루 주자가 있는 `Single`/`Double`/`Triple`은 실책이 있고 (a) 실책 원인 이동이 없거나 (b) 실책이 2개 이상이면(원인 이동 개수와 무관) `Game.hit_error_cause_unclear`로 상태·기록·책임 슬롯·영수증 전에 거부한다. 검사 순서: `hit_with_force_out` → `hit_with_force_out_saved_by_error`(C43 확정) → `hit_error_cause_unclear`(C44). 결과를 재분류하지 않는다. 실책 1개 + 표시 이동 1개는 유지(기록원 진술로 받아들일 뿐 실책별 인과 검증이 아님; "모든 실책이 설명됐다" 표현 삭제).
- **성격:** 복수 실책 사건은 실제 야구에서 합법일 수 있다. 이 거부는 입력 표현 한계에 따른 **게임 지원 범위 제한**이며 KBO/OBR 규칙이나 인증이 아니다. C44는 GPT Chat 설계 판단으로 승인된 Phase 2 지원 범위 제한이고(사용자 전달), 소유자 직접 승인은 별도 기록이 없으며 Claude Code가 승인한 것이 아니다. 새 GameRules 승인 없음.
- **RED→GREEN:** `HitMultiErrorTests` 8건(손으로 쓴 기대값). 수정 전 5건 실패(A Single, B Double/Triple 2건, C 연결 정보 없는 원인 이동 2개, G 재전달) — 로그 [PHASE_2_RED_EVIDENCE.md (원본 `PHASE_2_C44_MULTI_ERROR_RED.log` 로컬 보존)](../../docs/reports/PHASE_2_RED_EVIDENCE.md). D·E 정상 입력(실책 1개+`ErrorExtraBase`, 1루 주자 없음)은 전후 동일하게 허용. 거부 후 `GameState` 동일 인스턴스, 안타·타수·실책·아웃·득점·슬롯 불변, 영수증 없음, 재전달 동일 오류. JSON 왕복 후에도 같은 거부(`HitErrorReplayTests`). 기존 테스트 기대값 변경 없음. 타자주자의 추가 진루는 사건 모델에 표현할 수 없어 테스트하지 못함(예시 7 한계).
- **C29:** ADR-003 항목 25 원문 유지, "후속 결정에 의해 일부 대체됨" 표시 추가(자동 배치 없음, 합법 교체는 명시적 Slot, 미지원 교체는 오류 거부; 최신 상태는 항목 30과 C29 증거 행 `MATCH`).
- **검증:** xUnit **598**(Domain 145 · Simulation 260 · Application 114 · Infrastructure 79). 100경기·C33 30경기·200팀 박스 검산·영수증/JSON Replay·Phase 0~1 회귀 통과. `verify.ps1`: 1·2차 시도는 **`GATE: FAIL`**(새 증거 필드 `fixed_on`이 `NOT_MODELLED` 행에 쓸 수 없어 `RuleEvidenceTests` 1건 실패, 필드명 변경 후 재실행; 로그 [1](../../docs/reports/PHASE_2_VERIFY_LOG_c44multi_try1_FAIL_evidence_key.md)·[2](../../docs/reports/PHASE_2_VERIFY_LOG_c44multi_try2_FAIL_evidence_key.md) 보존). 수정 후 연속 2회 **`GATE: PASS` (종료 코드 0)**: [1회](../../docs/reports/PHASE_2_VERIFY_LOG_c44multi_run1.md), [2회](../../docs/reports/PHASE_2_VERIFY_LOG_c44multi.md). 수동 UI·FHD는 미검증.
- **상태:** Phase 2 Gate **INCOMPLETE**, 리뷰 `검토 대기` 유지(자체 최종 승인 없음). 커밋/푸시 없음, Phase 3 미착수. 보고서 [Part A9](../../docs/reports/PHASE_2_REPORT.md).

## B. 개발 목표

### 개발 목적

확률적 경기 시뮬레이션(Phase 3) 이전에, 스크립트된 야구 사건 열이 항상 합법적인 경기 상태와 정확한 공식 기록을 만든다는 것을 증명한다.

### 구현 범위

- 2-A: Phase 1 Medium 부채(Domain 컬렉션 가변성) 선행 수정
- 2-B: `docs/game-engine.md` 계약, 경기 모델, `GameRuleSnapshot`(D2 범위·출처·미확인 항목)
- 2-C: 순수 C# 결정론적 `GameReducer` + 득점/기록 산출 (D1 `Take` 포함)
- 2-D: 공식 기록 투영·정합성 검증기, 스크립트 경기 실행기, 100경기 독립 기대값 비교, Godot QHD 진단 화면
- 제외: 확률 결과, 시즌/커리어, 구단 AI, 실제 선수 데이터, SQLite 마이그레이션, 미승인 규칙 값

### 완료 기준

- [x] 100 스크립트 경기의 득점·안타·실책·아웃·타격/투구 합계 정합 (100/100 일치)
- [x] 음수 아웃/중복 베이스 없음, 실제 카운트·1구 Take 이후 재개 픽스처 통과
- [x] 지원 규칙 변형마다 이름 붙은 픽스처 (6개 프로필)
- [x] 포수 WP/PB 병합 없음, 배터리 노출 1회만 산출
- [ ] **D2 GameRules 운영 부분 집합 인증 — INCOMPLETE** (R02는 2026-10-11 소유자 적용 승인으로 해소, OBR 편차 6건은 수정 완료; KBO 규정집·기록규칙 미열람, C33 소유자 결정 대기)
- [x] Common Gate G 항목 (아래 표)

### 관련 요구사항 및 시스템 계약

- 요구사항: [REQUIREMENTS_TRACEABILITY.md](../../docs/REQUIREMENTS_TRACEABILITY.md)
- 시스템 계약: [SYSTEM_CONTRACTS.md](../../docs/SYSTEM_CONTRACTS.md) — C07 / C08 / C11 (구현 상태 주석 추가)
- ROADMAP §6.2 필수 경기 오라클

## C. 실제 구현 내역

### 구현한 기능

- 불변 `GameState`와 순수 `GameReducer.Start/Apply/LegalActions/Finalize`; 잘못된 사건은 코드가 있는 오류로 거부되고 상태는 변하지 않음; 중복 사건 ID는 멱등 처리; 확정은 1회
- D1: 접근 방식(`ApproachEvent`) 1회로 타석 진행, `Take`는 실제 1구만 소비하고 정확한 카운트·주자 상태로 재개
- 카운트/파울/삼진/볼넷/사구/낫아웃, 안타·홈런·땅볼·뜬공·직선타·번트·희생타·실책·야수선택·병살/삼중살·포스/태그, 도루/도루실패/견제/보크, WP/PB 분리, 득점 원인 추적, 이닝 교대·끝내기·말 공격 생략, 연장/무승부(프로필별), 합법 교체(대타·대주자·수비 교체·위치 교환·투수 교체)와 DH/무DH/투타 겸업, 승계 주자 책임, 자책 재구성, 승/패/세이브
- 포수 수비 참여·실제 배터리 노출(`CatcherExposure`, `BatteryExposure`)
- 공식 기록 투영(`BoxScoreBuilder`)과 정합성 검증기(`BoxScoreReconciler`, 약 25개 항등식)
- 진단 서비스 + Godot QHD `경기 진단` 화면 (공개 DTO만 사용)
- `tools/verify.ps1` 확장: G4/E4(경기 진단 headless/내보내기), D1(문서 상대 링크), D2(변경 파일)

### 주요 변경 파일

| 파일 | 변경 내용 |
|---|---|
| `src/StarPlayer.Domain/Players/{Ratings,Proficiency,PlayerStatus,DevelopmentProfile,Player}.cs` | 방어적 복사 + `AsReadOnly()` (2-A) |
| `src/StarPlayer.Domain/Baseball/Game/*` | 경기 모델·사건·기록·규칙 스냅샷 |
| `src/StarPlayer.Simulation/Baseball/*` | `GameReducer`, `EarnedRuns`, `BoxScoreBuilder` |
| `src/StarPlayer.Application/Projections/*`, `Ports/IScriptedGameSource.cs` | 공개 뷰 DTO, 한글 텍스트, 검증기, 진단 서비스 |
| `src/StarPlayer.Infrastructure/Fixtures/Scripted*.cs` | 규칙·경기 JSON 읽기, 유일한 공개 진입점 |
| `content/rules/game_rules_profiles.json`, `content/fictional/phase2_diagnostic_games.json` | 규칙 프로필 6개, 진단 경기 6개 (내장) |
| `tests/Fixtures/*` | 100경기 + 독립 기대값 + Python 생성기 |
| `game/UI/GameDiagnostics/*`, `game/Components/BaseDiamond.cs`, `game/Bootstrap/*`, `MainMenu.*` | QHD 진단 화면과 실행 모드 |
| `tools/verify.ps1`, `docs/*` | 검증 확장, 계약·보고서·ADR |

### 새 모듈과 기존 모듈 변경

- 새 모듈: 위 Domain/Simulation/Application/Infrastructure 경기 파일, Godot 화면
- 수정한 기존 모듈: Domain 선수 컬렉션(2-A), `CompositionRoot`/`GameRoot`/`LaunchOptions`/`MainMenu`, 아키텍처 테스트(`SourceScan`, `TrustedBoundaryTests`), `verify.ps1`

### 계획 대비 달라진 내용

- 없음(범위 내). 단, Gate 항목 "D2 운영 부분 집합 인증"은 공식 출처가 없어 수행하지 못함 → INCOMPLETE로 기록.

## D. 테스트 및 검증

### 실행 환경

| 항목 | 값 |
|---|---|
| OS | Windows 11 Home 10.0.26200 |
| Godot | 4.7.2.stable.mono.official.ed1daf0bf |
| .NET SDK | 8.0.425 (고정) |
| 빌드 구성 | Release (경고 0), 내보내기 ExportRelease |

### 실제 실행 결과

| 명령 | 기대 결과 | 실제 결과 | 상태 | 로그·결과 파일 |
|---|---|---|---|---|
| `powershell -File tools\verify.ps1` (1차 자체 검증 1·2회차) | Gate PASS | Gate PASS ×2 | 통과 | [initial_run1](../../docs/reports/PHASE_2_VERIFY_LOG_initial_run1.md), [initial](../../docs/reports/PHASE_2_VERIFY_LOG_initial.md) |
| `powershell -File tools\verify.ps1` (Codex 보완 후, 1차 시도) | Gate PASS | **FAIL** — 문서 링크(D1)만 실패(로그 파일명을 바꾸는 중에 실행됨), 코드·테스트·내보내기는 PASS | 실패(보존) | [try1](../../docs/reports/PHASE_2_VERIFY_LOG_remediation_try1_FAIL_doc_links.md) |
| `powershell -File tools\verify.ps1` (Codex 1차 보완 후, 연속 2회) | Gate PASS | Gate PASS ×2 (xUnit 476) | 통과 | [remediation1_run1](../../docs/reports/PHASE_2_VERIFY_LOG_remediation1_run1.md), [remediation1](../../docs/reports/PHASE_2_VERIFY_LOG_remediation1.md) |
| xUnit (verify 내부, 최종 보완 후) | 전부 통과 | Domain 145 · Simulation 174 · Application 114 · Infrastructure 67 = **500** 통과, 실패 0, 건너뜀 0 (1차 414, Codex 1차 보완 후 476) | 통과 | [최종 로그](../../docs/reports/PHASE_2_VERIFY_LOG.md) U1–U4 |
| `powershell -File tools\verify.ps1` (최종 보완 후, 연속 2회, 2026-10-11 00:18 / 00:21) | Gate PASS | Gate PASS ×2, 종료 코드 0, 경고 0, SQLite 8/8, 경기 진단 6개, Export 신규, 격리 실행, QHD, 문서 링크 37개 | 통과 | [run1](../../docs/reports/PHASE_2_VERIFY_LOG_run1.md), [로그](../../docs/reports/PHASE_2_VERIFY_LOG.md) |
| `powershell -File tools\verify.ps1 -LogName PHASE_2_VERIFY_LOG_r02_obr.md` (R02·OBR 라운드, 2026-10-11) | Gate PASS | Gate PASS ×1(이번 라운드는 1회 실행), 종료 코드 0, 경고 0, xUnit 504(Domain 145 · Simulation 174 · Application 114 · Infrastructure 71), SQLite 8/8, 경기 진단 6개, Export 신규, 격리 실행 QHD, 문서 링크 37개 | 통과 | [r02_obr](../../docs/reports/PHASE_2_VERIFY_LOG_r02_obr.md) |
| `powershell -File tools\verify.ps1` (OBR 편차 수정 라운드, 연속 2회, 2026-10-11) | Gate PASS ×2, 종료 코드 0 | Gate PASS ×2, 종료 코드 0·0, 경고 0, xUnit 540(Domain 145 · Simulation 207 · Application 114 · Infrastructure 74), SQLite 8/8, 경기 진단 6개, Export 신규, 격리 실행 QHD, 문서 링크 38·39개 | 통과 | [run1](../../docs/reports/PHASE_2_VERIFY_LOG_obr_fix_run1.md), [로그](../../docs/reports/PHASE_2_VERIFY_LOG_obr_fix.md) |
| `powershell -File tools\verify.ps1` (C33 라운드, 연속 2회, 2026-10-11) | Gate PASS ×2, 종료 코드 0 | Gate PASS ×2, 종료 코드 0·0, 경고 0, xUnit 561(Domain 145 · Simulation 224 · Application 114 · Infrastructure 78), SQLite 8/8, 경기 진단 6개, Export 신규, 격리 실행 QHD, 문서 링크 40·41개 | 통과 | [run1](../../docs/reports/PHASE_2_VERIFY_LOG_c33_run1.md), [로그](../../docs/reports/PHASE_2_VERIFY_LOG_c33.md) |
| `powershell -File tools\verify.ps1` (C33 마지막 High 수정, 연속 2회, 2026-10-11) | Gate PASS ×2, 종료 코드 0 | Gate PASS ×2, 종료 코드 0·0, 경고 0, xUnit 568(Domain 145 · Simulation 231 · Application 114 · Infrastructure 78), SQLite 8/8, 경기 진단 6개, Export 신규, 격리 실행 QHD, 문서 링크 42·43개 | 통과 | [run1](../../docs/reports/PHASE_2_VERIFY_LOG_c33_amb_run1.md), [로그](../../docs/reports/PHASE_2_VERIFY_LOG_c33_amb.md) |
| `powershell -File tools\verify.ps1` (안타·포스아웃 High 수정, 연속 2회, 2026-10-11) | Gate PASS ×2, 종료 코드 0 | Gate PASS ×2, 종료 코드 0·0, 경고 0, xUnit 578(Domain 145 · Simulation 241 · Application 114 · Infrastructure 78), SQLite 8/8, 경기 진단 6개, Export 신규, 격리 실행 QHD, 문서 링크 44·45개 | 통과 | [run1](../../docs/reports/PHASE_2_VERIFY_LOG_hit_force_run1.md), [로그](../../docs/reports/PHASE_2_VERIFY_LOG_hit_force.md) |
| `powershell -File tools\verify.ps1` (C43 최종 보완, 연속 2회, 2026-10-11) | Gate PASS ×2, 종료 코드 0 | Gate PASS ×2, 종료 코드 0·0, 경고 0, xUnit 590(Domain 145 · Simulation 252 · Application 114 · Infrastructure 79), SQLite 8/8, 경기 진단 6개, Export 신규, 격리 실행 QHD, 문서 링크 46·47개 | 통과 | [run1](../../docs/reports/PHASE_2_VERIFY_LOG_c43_run1.md), [로그](../../docs/reports/PHASE_2_VERIFY_LOG_c43.md) |
| Godot headless `--game-diagnostics` (G4) | 6경기 완주·기대값 일치·7/7 | `games=6 finished_with_box_score=6 self_check=PASS` | 통과 | 위 로그 |
| 내보내기 cold copy QHD `--game-diagnostics` (E4) | PASS + 스크린샷 | PASS, 스크린샷 2장 | 통과 | 위 로그 |
| 엔진 변이 점검 (1차 13건, 보완 후 새 규칙 13건; 수동) | 오류 주입 시 테스트 실패 | 1차: 11건 검출, 2건은 중복 가드(분석); 보완 후: **13건 모두 검출** | 통과 | [보고서 §4, A.4](../../docs/reports/PHASE_2_REPORT.md) |

### 완료 기준별 충족 여부

| 완료 기준 | 상태 | 근거 |
|---|---|---|
| 100경기 정합 | 통과 | `ScriptedGameSuiteTests` 100/100 일치 |
| 실제 카운트·Take 재개 | 통과 | `CountAndPlateAppearanceTests` |
| 규칙 변형별 픽스처 | 통과 | 7개 프로필(보완 후 `test-dh-forfeit` 추가), `GameFlowTests`, `GameRulesProfileTests`, `DhStateTests` |
| 포수/배터리·WP/PB 분리 | 통과 | `SubstitutionDecisionAndBatteryTests`, `RunnerPlayAndEarnedRunTests` |
| D2 규칙 부분 집합 **기록** | 통과 | `docs/game-engine.md` §5–6, 프로필 JSON |
| D2 규칙 부분 집합 **인증**(필수 규칙 R02 미해결) | **INCOMPLETE** | 연장 11회 한도만 공식 출처(KBO 보도자료 2025-01-22) 확보, 2026 야구규칙·기록규칙 원문 미확보(보고서 A.7) |
| Common Gate G 3(저장 라운드트립) | 해당 없음 | 저장 변경 없음(Phase 4 이전) |
| Common Gate G 4(실제 장면) | 통과(QHD만) | G4/E4; FHD는 Phase 5+ |

### 테스트하지 못한 항목

- 사람이 직접 새 화면을 클릭하는 수동 검증: `미검증` (자동 실행 모드와 스크린샷만 수행)
- FHD 해상도: `미검증` (Phase 5+ 범위)
- KBO 공식 규칙집 조문 대조: `미검증` (규정집 공지에 본문 없음). OBR 2026 원문은 이번 라운드에 읽고 대조함(38행)
- Codex 보완 후 재검토: `미실시` (1차 Codex 리뷰는 `CHANGES REQUIRED`로 수행됨)
- KBO 야구 기록규칙 PDF: `docs/2023recordRule.pdf`가 저장소에 있으나 텍스트 추출에 한글이 나오지 않아 대조하지 못함(OCR·육안 대조 미실시), e-book은 이미지 형식: `미검증`
- (2026-10-11 수정 라운드에서 C12·C14·C19·C24·C32·C34의 재현 테스트를 작성하고 수정 전 실패를 확인함: 위 표)
- 병살 사이의 오류(C40), 더블 스위치·다른 빈 자리 배치(C39)는 입력 경로가 없어 거부하며 테스트로만 확인: 미구현

## E. 발견된 문제

### 발생한 오류

- 엔진 버그(테스트로 발견): 홈런 `BatHR` 미기록, 팀 간 벤치 중복 미거부, 인플레이 투구 미집계, 땅볼 병살 포스아웃 거부 → 모두 수정
- 변이 점검에서 병살 RBI 규칙이 테스트로 보호되지 않음을 발견 → 오라클 추가
- Codex 지적(High 3, Medium 1, 끝내기 잔루): 위 "Codex 리뷰 이력과 보완" 참조

### 해결한 문제

- 위 항목 및 Phase 1 Medium 부채(컬렉션 가변성)

### 미해결 문제

- D2 GameRules 운영 부분 집합의 KBO 규정 인증 (연장 11회 한도는 공식 발표·경기 기록 확인, R02는 소유자 적용 승인이며 조문 미확인; KBO 지명타자·투타 겸업·제3아웃·기록규칙 조문은 미인증)
- OBR 원문 대조에서 찾은 편차 6건(C12, C14, C19, C24, C32, C34)은 **2026-10-11 수정 완료**(위 수정 라운드). 미해결 편차 0건
- 열린 OBR 차이 0건. C43(9.05(b)(1) 둘째 절)은 수정 완료(C43 최종 보완 라운드). **Phase 2 지원 범위 제한 C44(1루 주자가 있는 안타에서 설명되지 않은 실책 → 거부)는 2026-10-11 GPT Chat 설계 판단으로 승인(사용자 전달, 소유자 직접 승인 별도 기록 없음, KBO 인증·OBR 완전 구현 아님)**. `ReachedOnError`는 실책의 직접 원인 판정 불가(C42 `NOT_MODELLED`).
- C33 승계 주자 책임(9.16(g))은 **2026-10-11 슬롯 장부로 수정 완료**(Gate 필수 구현 결함 0건). C38 희생플라이 변형은 **소유자 승인 Gate 비필수 후속 이월(미지원 명시, 구현 아님, Phase 3 재평가)**. 예시 7의 타자 송구 진루·타자주자 방해 아웃은 모델 밖. C29는 자동 배치를 없애고 명시 요청만 처리(승인된 단순화로 부르지 않음)
- 출력 순서(`Sequence`)는 기록원 입력이며 엔진은 일관성만 검사(물리적 사실 여부는 알 수 없음)
- 문서화된 단순화(RBI, 자책 재구성 관례, 승/패/세이브)는 OBR 원문과 대조했으나(위 편차 표) KBO 기록규칙과는 대조되지 않음

### 기술 부채 및 향후 영향

- 보완 후 100경기 스위트는 제3아웃+득점 순서·무효 득점·끝내기 잔루도 포함(`ReplayContractTests`가 포함 여부를 검사); 그래도 모든 드문 규칙은 아니므로 손 오라클이 병행
- Python 오라클은 동일 작성자가 같은 명세에서 작성했으므로 독립성에 한계가 있음(제3아웃·포스·끝내기 잔루 규칙도 Python에 별도 구현했으나 같은 해석)
- "야수의 투수 전환으로 DH 소멸"은 OBR 5.11(a)(14) 원문 조항이며(2026-10-11 정정: 이전의 "임의 정의" 분류는 오류) KBO 지향 프로필에서 켰음; "DH 수비 투입 시 DH 소멸"(5.11(a)(12))과 함께 OBR 보완(KBO 미인증)
- 어필 플레이는 구현하지 않음(승인된 단순화); 주자가 베이스를 적법하게 밟았다고 전제
- R02(연장 한도 도달 시 무승부)는 2026-10-11 소유자 적용 승인으로 해소(조문 미확인, `clause_certified=false`)
- `game_states`/`scored_plays` 영속화는 Phase 4 몫

관련 이슈 문서: 없음(`../03_Issues/`에 별도 문서 미생성).

## F. 설계 변경

| 변경 내용 | 변경 이유 | 영향 모듈 | 결정 문서 | Codex 추가 검토 |
|---|---|---|---|---|
| 이벤트 구동 리듀서, 비인증 KBO 지향 프로필 표기, 독립 Python 오라클, Phase 1 컬렉션 불변화 | Phase 2 목표·D1·D2 준수 | Domain/Simulation/Application/Infrastructure | [ADR-003](../../docs/decisions/ADR-003.md) | 1차 수행(CHANGES REQUIRED) |
| (최종 보완 2026-10-11) 타자주자 안타 후 주루사 표현, 어필 제외(승인), KBO 우선·2026 OBR 보완 정책과 규칙 증거 파일, DH 스위치 분리 | Codex 재검토 High 1·Medium 1, 사용자 정책 결정 | Domain/Simulation/Infrastructure/content | [ADR-003 보완 17–22](../../docs/decisions/ADR-003.md) | 최종 보완 후 재검토 미실시 |
| (2026-10-11 OBR 편차 수정 라운드) 편차 6건 수정, `TeamER`·`StartingDh` 추가, DH 슬롯 명시 요청, 구현 결함과 규칙 출처 차단의 분리 관리 | GPT Chat 지시 "OBR 편차 처리 최종 결정" | Domain 모델 2필드, Simulation 리듀서(Finish·Substitutions·EarnedRuns), 검산기, 생성기, content | [ADR-003 보완 27–32](../../docs/decisions/ADR-003.md), [game-engine.md §12](../../docs/game-engine.md) | 권장: C12·C14·C19 점수 계산 규칙 |
| (2026-10-11 C33 라운드) 책임 슬롯 장부(`RunResponsibility`)로 득점 청구 변경, C38 후속 이월 | GPT Chat 검토 결과 `TASK.md` | Simulation 리듀서(Finish, 신규 Responsibility 파일), Python 생성기, 증거 파일, 테스트 | [ADR-003 33](../../docs/decisions/ADR-003.md), [game-engine §4b](../../docs/game-engine.md) | 미실시(권장) |
| (2026-10-11 R02·OBR 라운드) R02 적용 승인, OBR 원문 38행 대조표와 6개 상태 분류, DH 전환 분류 정정(R27)과 KBO 프로필 플래그 활성화, 퓨처스 승부치기 분리(R28), 박스 증명 측정 | GPT Chat 보완 지시(`TASK.md`), 원문 PDF 열람 | content(증거·프로필)/tests/docs, 엔진 동작은 프로필 플래그뿐 | [ADR-003 보완 23–26](../../docs/decisions/ADR-003.md), [game-engine.md §11](../../docs/game-engine.md) | 권장(핵심 시뮬레이션 규칙): C19·C12·C14 변경 시 |
| (Codex 보완) 아웃 분류의 상태 도출·순서 데이터, 이벤트 영수증/지문, 팀별 `DhActive`, 볼 데드 검증, 끝내기 잔루 프로필 규칙 | Codex High 1·2·3, Medium 4, 잔루 지적 | Domain/Simulation/Infrastructure | [ADR-003 보완 10–16](../../docs/decisions/ADR-003.md) | 보완 후 재검토 미실시 |

## G. AI 리뷰

### GPT Chat 리뷰

> **최종 기록 (2026-10-11, 사용자 전달):** GPT Chat 최종 검토 `PASS WITH NOTES` → Phase 2 `승인 완료`. 아래 표의 `미실시`는 승인 이전 라운드의 기록이며 변경하지 않았다.

| 항목 | 결과 |
|---|---|
| 기능명세 충족 여부 | 최종 판정 `미실시` |
| 누락된 요구사항 | 최종 판정 `미실시` |
| 개선 권장사항 | 2026-10-11 보완 지시 수령(`TASK.md`): R02 적용 승인, OBR 원문 대조, DH 분류 재평가 — 이행 결과를 위 "R02 결정과 OBR 원문 대조 보완"에 기록 |
| 최종 검토 의견 | 최종 판정 `미실시`(이 라운드 결과에 대한 GPT Chat 재검토 대기) |

### Codex 리뷰 (선택적)

> **최종 기록 (2026-10-11, 사용자 전달):** Codex 최종 `PASS WITH NOTES`(기능 결함 0건). 위 "Codex 최종 리뷰 PASS WITH NOTES" 절과 이력을 참조한다. 아래 표의 `미실시`는 이전 라운드 기록이며 변경하지 않았다.

| 항목 | 결과 |
|---|---|
| 아키텍처 적합성 | 1차: `CHANGES REQUIRED`(High 3, Medium 1) → 보완 후 재검토 `미실시` |
| 모듈 간 의존성 | 보완 후 재검토 `미실시` |
| 중요 로직 검증 | 1차 지적 4건+잔루 보완 완료, 재검토 `미실시` |
| 구조적 위험 요소 | 보완 후 재검토 `미실시` |

> [!tip] Codex 리뷰 권장
> 보완 후 재검토를 권장합니다. 핵심 시뮬레이션 규칙과 다중 모듈 변경이므로 `EarnedRuns`·`FinishPlay`·`ValidatePlans`·교체/투타 겸업 로직과 Python 오라클의 독립성을 중심으로 Codex 검토를 권장합니다.

## H. 다음 Phase 인수인계

### 시작 전 확인 사항

- (2026-10-11 승인으로 해소: Phase 2 승인 여부, C33 수용, C38 미지원 이월 확인. D2 GameRules 인증은 후속 NOTES로 남음)
- Phase 3은 **미착수**이며 소유자의 별도 지시가 있어야 시작

### 의존 관계

- Phase 3: 확률 해결기가 `GameReducer.Apply`를 우회하지 않고 `GameEvent`만 생성
- Phase 4: `game_states`, `scored_plays`, 규칙 프로필 해시의 영속화와 마이그레이션

### 주의할 문제 및 후속 작업

- `ApproachKind`는 기록만 되고 효과가 없음(Phase 3에서 소비)
- 비인증 프로필 값을 운영 커리어에 사용하기 전에 증거 기반 프로필로 교체

### 추가 검증 필요 사항

- 사람 수동 클릭 검증, FHD, Codex 리뷰

## I. 최종 결과

| 항목 | 결과 |
|---|---|
| 완료 기준 충족 여부 | 충족(PASS WITH NOTES) — D2 운영 부분 집합 비인증 등은 후속 NOTES로 보존 |
| 최종 리뷰 상태 | **승인 완료 (PASS WITH NOTES, 2026-10-11)** — 이전: 검토 대기 |
| 남은 문제 | 후속 NOTES: C33 기록된 한계, C38 미지원 이월, C42 NOT_MODELLED, C44 지원 범위 제한, D2 비인증 프로필, Phase 4 엔진 규칙 버전·영속화, FHD 미검증 |
| 승인자 또는 승인 근거 | GPT Chat 최종 검토 결과를 사용자가 전달(2026-10-11): xUnit 598, 스크립트 경기 130/130, 박스스코어 200/200, verify 연속 2회 PASS, Codex 최종 PASS WITH NOTES, 사용자 Windows Export 수동 UI 검증 완료 |
| 최종 수정 날짜 | 2026-10-11 (최종 승인 기록) |

### Phase 종료 체크

- [x] ROADMAP 완료 기준과 Common Gate G 확인
- [x] 실제 구현·변경 파일과 Git 변경사항 기록
- [x] 실행한 테스트의 명령·환경·실제 결과 기록
- [x] 미검증 항목, 미해결 문제와 설계 변경 식별
- [x] 중요한 설계 변경의 결정 문서 연결
- [x] 리뷰 상태를 `검토 대기`로 변경 (이후 2026-10-11 GPT Chat/사용자 최종 검토로 `승인 완료`)
- [x] [CURRENT_STATE.md](../../CURRENT_STATE.md)와 실제 상태 일치 확인
- [x] GPT Chat 전달용 간략 요약 작성
- [x] 다음 Phase를 자동 시작하지 않음
