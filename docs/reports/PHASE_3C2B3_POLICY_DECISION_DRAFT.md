# Phase 3-C2B-3-P 정책 결정 초안 (P-01 ~ P-10)

날짜: 2026-10-12 (Codex 리뷰 수정 반영 판) · 기준 HEAD `f669589` · 항목 상태: **P-01~P-10 전부 `OWNER APPROVED`**(2026-10-12 프로젝트 소유자: P-05 `Prototype Sequence Policy v1`은 먼저 별도 승인, P-01~P-04·P-06~P-10은 이후 일괄 승인). **C2B-3-P: 정책 소유자 승인 완료, 구현 전 문서 정합성 점검.** 이 승인은 3-C2B-3 프로덕션 구현의 승인이 아니다 — 3-C2B-3 프로덕션 `NOT STARTED / NOT AUTHORIZED`, 3-C3 `NOT STARTED`, Phase 3 Gate `INCOMPLETE`. 소유자 정책 승인, GPT Chat의 FIX2 보고서 기반 `PASS WITH NOTES`(사용자 전달 보고), Codex 독립 검토는 서로 다른 사실이며 Codex가 FIX2 이후 독립 PASS를 수행했다는 기록은 없다. **FIX2 수정 이력(2026-10-12):** 재검토 Codex가 (1) P-05 소유자 승인이 문서에 반영되지 않음, (2) P-06이 존재하지 않는 `InPlayResult.SacrificeFly`를 요구함을 Medium으로 지적해 P-05 승인 상태를 정정하고 P-06을 '이벤트 입력 `InPlayResult` vs Reducer 파생 `BatterResult`' 계약으로 고쳤다(정책 내용·수치·증거는 불변). **수정 이력(보존):** Codex Medium 지적 `P-05 사건 순서 정책의 소유자 승인 및 의미 정의 미완료`에 따라 P-05를 "프로토타입 순서 정책 v1"로 의미를 재정의하고 P-03·P-06·P-07·P-09·P-10 문구를 정정했다(숫자와 증거는 바꾸지 않았다). 입력 계약은 [인수인계 초안 §6](PHASE_3C2B3_IMPLEMENTATION_HANDOFF_DRAFT.md). **최종 승인 반영 이력(2026-10-12):** 아래 "최종 승인 기록" 절과 표·각 정책의 `소유자 승인` 항목을 `OWNER APPROVED`로 갱신했다. 각 정책 본문("권장 정책" 등 원 제안 문구)은 승인된 정책으로 읽으며 숫자·증거는 바꾸지 않았다. 과거 `PROPOSED`·`OWNER_APPROVAL_REQUIRED` 기록과 Codex `CHANGES REQUIRED`·FIX1/FIX2 이력은 보존한다. Codex 재검토는 `미실시`. 근거 수치는 [정책 검증 보고서](PHASE_3C2B3_POLICY_VALIDATION_REPORT.md)와 `PHASE_3C2B3P_*` 증거 파일이다. [ADR-007](../decisions/ADR-007.md) "3-C2B-3-P policy approval", [game-engine §25](../game-engine.md), SYSTEM_CONTRACTS C08에 승인된 정책 계약을 반영했다(구현 아님).

전제(변경하지 않음): `prob-model-v1`의 `advancement` 4개 맥락과 확률표(`single_runner_on_second` 59/38/3, `single_runner_on_first` 27/70/3, `double_runner_on_first` 56/40/4, `fly_out_runner_on_third` 70/28/2 %), 스트림 `game/runner@1`, C2B-2 기본 계획·`PendingAdvancement` 격리, `GameReducer` 계약.

## 최종 승인 기록 (2026-10-12, 프로젝트 소유자)

승인 범위는 **정책**이다. 3-C2B-3 프로덕션 구현·3-C3·Phase 3 완료의 승인이 아니다. 승인 시점의 소유자 지시(`TASK.md`)가 정한 정책 문구는 다음과 같으며, 이 문서의 각 정책 본문과 일치함을 확인했다(충돌 없음).

| ID | 승인된 정책 (요지) | 상태 |
|---|---|---|
| P-01 | 선행 주자 우선: 3루 → 2루 → 1루 | `OWNER APPROVED` |
| P-02 | 순차 조건부 합성, 베이스 점유와 앞선 주자 행동 고려. 플레이 종료 후 남은 맥락의 적용 결과는 `Void`이며 P-07의 논리적 RNG 소비는 보존 | `OWNER APPROVED` |
| P-03 | 현재 네 개 맥락에서 점유 충돌로 `Extra`·`Out`이 모두 불가능하면 원본 분포의 해당 행동을 `Hold`로 귀속(정책 B). 재정규화 정책 A와의 분포 동등성은 이 조건에서만 인정하고 미래 맥락으로 일반화하지 않음 | `OWNER APPROVED` |
| P-04 | 정책 계층이 베이스 충돌·주자 추월·불법 아웃·플레이 종료 후 행동을 사전 차단하고 완성 이벤트는 기존 `GameReducer`로 재검증. Reducer 수락을 실제 야구 규칙의 완전한 합법성 증명으로 보지 않음 | `OWNER APPROVED` |
| P-05 | `Prototype Sequence Policy v1` — 기존 승인 유지(2026-10-12 별도 승인). 네 개 맥락 한정, 선행 주자 우선 명시 `Sequence`, 제3아웃·득점 인정은 `Sequence`와 기존 제3아웃 규칙, 실제 시간 관측·KBO 2026 인증 아님, 시간 기반 주루 모델로 교체 가능 | `OWNER APPROVED` |
| P-06 | 최종 진루·아웃·`Sequence` 확정 후 **실제 존재하는 `InPlayResult`**로 `InPlayEvent` 구성(뜬공 아웃은 `Out`), `Apply` 뒤 `ScoredPlay`의 파생 공식 기록을 검증. 희생플라이는 `Result = Out` 입력이 조건을 만족할 때 Reducer가 `BatterResult.SacrificeFly`로 파생하는 기록이며 `InPlayResult.SacrificeFly`는 없음. `Apply` 뒤 이벤트 수정 금지 | `OWNER APPROVED` |
| P-07 | 현재 권위 RNG **H0** 검증 → Pending의 **H1**에서 개인 사본 복원 → 맥락당 논리적 추첨 1회(Void 맥락도 고정 소비) → 재추첨 금지 → 성공 후보 `NextRng = H2` → 권위 RNG의 실제 채택은 C3. 논리적 호출과 raw `Draws`를 구별 | `OWNER APPROVED` |
| P-08 | 첫 C2B-3 구현은 선수별 주루·송구 능력 보정을 적용하지 않음(기존 승인 `advancement` 확률·기본 보정 계약 사용). **중립은 선수의 실제 능력이 0이라는 뜻이 아님.** 선수별 공급·보정은 향후 별도 계약·버전. 새 `PlayerSheet` 입력·가상 능력치를 만들지 않음 | `OWNER APPROVED` |
| P-09 | 감사: 정책·버전, 맥락별 처리 순서, 원본 RNG 추첨 결과, 최종 적용 결과·`Hold`/`Void`, 최종 사건 `Sequence`, H1/H2 RNG 해시, **최종 이벤트와 Reducer 파생 기록의 구분**, 후보 검증 결과. 기존 `ef1`/`ef2`·이벤트 JSON 계약은 변경하지 않음 | `OWNER APPROVED` |
| P-10 | `Resolve(pending, state, rng(H0), tuning, batter, pitcher, lastIssuedEventSequence, cancellation)`; 기존 `CheckPrepared` 재사용·최종 후보 전 재검증; 실제 `GameState`·RNG·EventId 커서 불변; 개인 작업용 ID와 영수증 충돌 검사; 기존 `InPlayPitchCandidate` 재사용(`NextRng = H2`); C3가 확정 직전 실제 커서·상태 재검사 | `OWNER APPROVED` |

구분 기록: ① **소유자 정책 승인**(위 표; 이 세션에서 사용자가 채팅으로 기록을 확인) ② **GPT Chat의 FIX2 보고서 기반 `PASS WITH NOTES`**(`TASK.md`에 적힌 사용자 전달 보고; 이 세션이 독립 확인하지 않음) ③ **Codex 독립 검토** — 최초 `CHANGES REQUIRED`(보존)와 재검토 지적(FIX2로 정정) 이후 Codex가 새 독립 PASS를 수행했다는 기록은 없음(`미실시`). 세 가지는 서로 대체하지 않는다.

## 0. 정책 요약 (원 제안 문구 보존; 전 항목 `OWNER APPROVED`)

| ID | 한 줄 권장 | 소유자 승인 |
|---|---|---|
| P-01 | 추첨 순서 = 선행 주자 우선(시작 베이스 3→2→1) | **`OWNER APPROVED`**(2026-10-12) |
| P-02 | 순차 조건부 합성 + 시도 결합 가능성 규칙(점유된 베이스로의 시도는 Extra·Out 모두 불가), 종료 후 결정은 Void | **`OWNER APPROVED`**(2026-10-12) |
| P-03 | D3 = 승인 행 그대로 추첨, 불가능 결과는 Hold로 귀속(정책 B), 원 추첨 결과를 감사에 기록. **현 4개 맥락의 점유 충돌(Extra·Out 동시 차단)에서만** A와 결과 분포 동일 | **`OWNER APPROVED`**(2026-10-12) |
| P-04 | 정책 계층이 중복 점유·종료 후 이동을 사전 차단 + 최종 이벤트는 Reducer 재검증 + 독립 판정 규칙을 테스트로 고정 | **`OWNER APPROVED`**(2026-10-12) |
| P-05 | **Prototype Sequence Policy v1**: 타자 아웃 → 선행 주자 우선, 득점·아웃 공존 시 항상 명시 `Sequence`. 실제 야구의 관측 순서가 아니라 **게임 프로토타입이 정한 결정적 가정**이며 KBO 인증이 아님 | **`OWNER APPROVED`**(2026-10-12; 결과를 뒤집는 정책임을 알고 승인) |
| P-06 | 추첨 → 합법성·`Sequence`·최종 주자 이동 확정 → **실제 존재하는 `InPlayResult`(예: 뜬공 아웃은 `Out`)로 최종 `InPlayEvent` 생성 → `GameReducer.Apply` → Reducer가 파생한 `ScoredPlay.Result`(`BatterResult`, 희생플라이 포함)·타수·타점·득점·아웃·LOB를 기대값과 대조·감사**. 적용 뒤 이벤트를 수정하지 않는다 | **`OWNER APPROVED`**(2026-10-12) |
| P-07 | 맥락당 롤 정확히 1회(고정 소비, Void·강제 Hold 포함), H1에서 시작, 재추첨 금지, 실패·취소 시 H1 유지, 재시도는 같은 롤. 호출 수와 raw `Draws`는 구별 | **`OWNER APPROVED`**(2026-10-12) |
| P-08 | 첫 구현은 중립(C2A와 동일). 능력 연결의 최소 계약은 별도 승인 후 | **`OWNER APPROVED`**(2026-10-12) |
| P-09 | 추가 진루 감사 레코드 추가(확률표·원 추첨·적용 결과·사유·위치) | **`OWNER APPROVED`**(2026-10-12) |
| P-10 | **최신 입력 재검증(기존 `CheckPrepared`) 선행** 후 `PendingAdvancement` + 추가 진루 → 기존 `InPlayPitchCandidate`(NextRng = H2). 채택·원자성은 C3 | **`OWNER APPROVED`**(2026-10-12) |

---

## P-01 복수 주자 추첨 순서

1. **현재 코드 지원**: 추첨 코드 없음. `InPlayBasePlanner.ContextKeys`가 키를 후행 주자 우선(`single_runner_on_first` → `single_runner_on_second`)으로 나열할 뿐 추첨 순서를 규정하지 않는다.
2. **기존 승인 계약**: 복합 상황의 맥락·순서·상호작용은 없다(U-1/F-1). 각 `advancement` 조건은 자기 분모의 독립 조건부 분포다.
3. **발견한 문제**: 후행 우선 + 사전 가능성 판정이면 선행 주자가 아직 3루에 있어 후행 주자의 Extra가 270/270 사례에서 0 %가 된다(`A/B-T-pre`). 후행 우선 + 사후 보정은 선행 우선과 분포가 같지만(1,314건 전수 정확 일치) 추첨 순서와 사건 순서가 달라진다.
4. **대안**: (a) 선행 주자 우선, 시작 베이스 내림차순 — 권장. (b) 키 순서(후행 우선) + 독립 추첨 후 사후 보정. (c) 키 순서 + 사전 판정 — 기각(후행 Extra 0 %).
5. **테스트 근거**: `Static_the_production_context_key_order_is_trailing_runner_first...`, `Harness_sequential_feasibility_in_the_production_key_order_makes_the_trailing_runners_extra_base_impossible`, `Harness_post_hoc_repair_in_the_production_key_order_equals_lead_first...`, `Harness_draw_order_changes_which_roll_belongs_to_which_runner...`.
6. **권장 정책**: (a). 이유는 직관이 아니라 (i) Reducer의 기본 사건 순서(`OrderByDescending(From)`)와 같다, (ii) 추첨 시점에 실제 작업 상태로 가능성을 판정해 사후 보정이 필요 없다, (iii) (c)의 붕괴를 피한다. 롤 순서: 선행 주자의 롤이 먼저.
7. **미해결 위험**: 선행 주자 아웃이 후행 주자의 진루를 쉽게 만드는 현실적 상관은 모델에 없다(R-5, 3-F 보정). (b)를 택해도 분포는 같으므로 순서는 의미 선택이 아니라 구현·감사 편의 선택이다.
8. **소유자 승인**: **`OWNER APPROVED` (2026-10-12).** 선행 주자 우선(3루 → 2루 → 1루). (과거 기록: 이 줄은 이전 판에서 "필요"였다.)

## P-02 독립 추첨 결과의 합성 규칙

1. **현재 코드 지원**: 없음.
2. **기존 승인 계약**: `prob-model-v1`은 각 단계를 자기 분모의 조건부 분포로만 정의하고 단계끼리 섞지 않는다.
3. **발견한 문제**: 독립 합성(`IND-L`)은 5,562벡터 중 840개(15.1 %)가 불법에 도달한다. 2결정 사례 평균 불법 확률 15.63 %, 대표 사례 11.4 %. Reducer는 그중 360개만 거부하고 480개를 수락한다.
4. **대안**: (a) 순차 조건부 합성 + 시도 결합 규칙 — 권장. (b) 독립 추첨 + 사후 보정(분포 같음). (c) 불법 결과 시 전체 재추첨(기각: 가변 소비, P-07).
5. **테스트 근거**: `Harness_independent_per_context_draws_without_repair_reach_illegal_plays`, `Reducer_acceptance_is_not_baseball_legality_...`, `Harness_exact_joint_distribution_of_first_and_second_occupied_with_nobody_out_matches_the_hand_derivation`(손 유도: E/E = 59 % × 27 %, H/H = 38 %, 불가능 합성 E/H·O/H = 11.4 %).
6. **권장 정책**: 규칙 두 가지만. (R1) **시도 결합**: 시도할 베이스(2루·3루)가 점유되어 있으면 Extra와 Out 모두 불가능하다(둘 다 그 베이스로의 시도이므로). 홈은 막히지 않는다. (R2) **종료 후 Void**: 제3아웃 또는 결승 득점이 확정된 뒤의 결정은 Void이며 베이스 계획 위치에 동결한다(타자주자도 이동하지 않는다).
7. **미해결 위험**: 승인된 4개 맥락 밖(타자주자 진루, 3루 주자 태그업 외)은 해당 없음. 새 맥락이 생기면 시도 대상과 가능성 규칙을 다시 정해야 한다.
8. **소유자 승인**: **`OWNER APPROVED` (2026-10-12).** 순차 조건부 합성 + 시도 결합(R1)·종료 후 `Void`(R2). `Void` 맥락도 P-07의 고정 논리 소비를 지킨다. (과거 기록: 이 줄은 이전 판에서 "필요"였다.)

## P-03 불가능한 행동의 확률 처리 (D3)

1. **현재 코드 지원**: 없음. `StageProbability.Compute`는 조건 전체 결과를 대상으로 하며 부분 집합 재정규화 API가 없다(재정규화는 `ProbabilityScale.Normalize`를 호출하는 새 코드가 필요).
2. **기존 승인 계약**: 확률표 불변. `Distribution`은 합이 정확히 1,000,000이어야 하며 확률 0 결과는 선택되지 않는다.
3. **발견한 문제**: 현재 승인된 4개 `advancement` 맥락에서, 점유 충돌이 Extra와 Out을 **함께** 막는 시도 결합 규칙(P-02 R1)을 쓰면 A와 B의 최종 분포가 **1,314건 전수 정확히 같다**(가능한 결과가 Hold 하나뿐이라 재정규화와 Hold 귀속이 같은 점으로 수렴). 이 동등성은 **그 4개 맥락과 그 규칙에 한정된 측정 결과**이며, Hold 귀속이 모든 미래 맥락에서 재정규화와 같다는 일반 주장이 아니다(가능한 결과가 둘 이상 남는 부분 제외에서는 다르다). 부분 제외(Extra만 제외) 변형은 점유된 베이스에서의 아웃을 만들고 Reducer가 수락한다. 부분 제외에서 A는 `out` 3.00 %→4.11 %(958,904/41,096 ppm, `Normalize` 반올림 의존), B는 3.00 % 유지.
4. **대안**: A 재정규화, B Hold 귀속, C 재추첨. 비교 표는 보고서 §7.2.
5. **테스트 근거**: `Harness_with_attempt_coupling_policies_A_and_B_give_the_same_exact_distribution_in_all_1314_cases`, `Harness_blocked_runner_is_thrown_out_conditional_distribution_A_versus_B...`, `Harness_excluding_only_the_extra_base_still_lets_a_runner_be_thrown_out_on_an_occupied_base`, `Harness_resolver_frequencies_on_the_real_stream_match_the_exact_distribution...`.
6. **권장 정책**: **B** — 승인된 행 그대로 `NextBounded(1,000,000)` 한 번으로 추첨하고, 결과가 P-02의 규칙으로 불가능하면 Hold로 귀속, 감사에 `drawn_outcome`과 `applied_outcome`, 사유(`blocked_base`/`play_over`)를 둘 다 기록한다. 이유: 새 분포·새 반올림 규칙이 필요 없고, 확률표를 건드리지 않으며, 롤 1회가 고정되고, **현 4개 맥락에서는** 결과 분포가 A와 같다. 정책의 범위: 점유 충돌은 Extra와 Out을 동시에 차단하는 경우에 한해 적용하며, 새 맥락이 부분 제외(예: Extra만 불가)를 만들면 A/B 동등성을 가정하지 말고 그 맥락의 정책을 새로 승인한다.
7. **미해결 위험**: Hold 귀속은 "시도하지 않기로 했다"로 읽히며 후행 주자 Extra가 명목 27 %에서 평균 12.9 %로 낮아진다(R-5). 소유자가 A를 택해도 현 4개 맥락의 시도 결합이면 결과는 같다. 의미 선택은 A/B가 아니라 **시도 결합 정의(P-02 R1)** 다. 단 "Hold 귀속 = 재정규화"는 미래 맥락에 대한 주장이 아니다.
8. **소유자 승인**: **`OWNER APPROVED` (2026-10-12), 정책 B.** 분포 동등성은 현재 네 개 맥락의 점유 충돌에 한하며 미래 맥락으로 일반화하지 않는다. (과거 기록: 이 줄은 이전 판에서 "필요"였다.)

## P-04 베이스 중복 점유 및 추월 방지

1. **현재 코드 지원**: Reducer `ValidatePlans`가 최종 위치의 이중 점유(`Game.base_occupied_twice`), 추월(`Game.runner_passed_runner`), 4아웃 이상(`Game.too_many_outs`), 중복 이동(`Game.runner_moved_twice`)을 거부한다.
2. **기존 승인 계약**: C2B-2 기본 계획은 항상 합법(Reducer가 5,400건 수락).
3. **발견한 문제**: 위 검사는 최종 위치만 본다. **점유된 베이스에서의 아웃**(270)과 **제3아웃/결승 득점 이후의 이동**(210)은 Reducer가 수락한다. 추월 위반은 5,562벡터 중 0건(4개 맥락에서 후행 주자는 한 베이스 이내).
4. **대안**: (a) 정책 계층이 사전 차단 + Reducer 재검증 — 권장. (b) Reducer를 강화 — 금지(이번 단계에서 `GameReducer` 수정 불가, 기존 계약 변경).
5. **테스트 근거**: `Reducer_a_runner_tagged_out_on_a_base_a_teammate_occupies_is_accepted_as_a_legal_event`, `Reducer_a_move_after_the_third_out_is_accepted_and_two_outs_after_two_are_refused`, `Harness_and_reducer_a_coupled_policy_reaches_only_legal_plays...`(8개 정책), `Reducer_the_same_runner_cannot_move_twice_...`.
6. **권장 정책(승인됨)**: P-02의 R1·R2로 사전 차단하고(베이스 충돌·불법 아웃·플레이 종료 후 행동), 주자 추월도 정책 계층의 사전 검사 항목에 포함한다(현 4개 맥락에서는 5,562벡터 중 0건이지만 승인된 검사 목록에 둔다). 후보 확정 전 항상 Reducer로 재검증한다. 구현은 "Reducer가 수락했으므로 합법"으로 판단하지 않으며, 독립 판정 규칙(이번에 만든 `AdvJudge` 수준)을 프로덕션 테스트에 포함한다.
7. **미해결 위험**: 판정 규칙을 프로덕션 코드와 테스트가 같은 작성자로 공유하면 독립성이 약하다(R-8).
8. **소유자 승인**: **`OWNER APPROVED` (2026-10-12).** Reducer 수락을 실제 야구 규칙의 완전한 합법성 증명으로 보지 않는다. `GameReducer`는 변경하지 않는다. (과거 기록: 이 줄은 이전 판에서 "필요"였다.)

## P-05 주루사와 제3아웃의 판정 순서 — Prototype Sequence Policy v1 (`OWNER APPROVED`, 2026-10-12)

**승인 상태: `OWNER APPROVED` (2026-10-12, 프로젝트 소유자 명시 승인).** 정책명 `Prototype Sequence Policy v1`. 승인 범위는 아래로 한정된다.

- 대상: 기존 네 개 Advancement 맥락(`single_runner_on_first`, `single_runner_on_second`, `double_runner_on_first`, `fly_out_runner_on_third`)에 한정.
- 선행 주자 우선 추가 진루 판단과 명시적 `Sequence` 사용. 제3아웃과 득점 인정은 승인된 가상 사건 순서와 기존 Reducer 규칙으로 처리한다.
- 실제 야구에서 관측된 시간 순서라고 주장하지 않으며, KBO 2026 공식 규칙 인증으로 간주하지 않는다.
- 향후 시간 기반 주루 모델(구분 ④)로 교체할 수 있다.

이 승인은 **P-05 한 항목에만** 해당한다. P-01~P-04, P-06~P-10은 계속 `PROPOSED`이고 C2B-3-P 전체는 최종 승인 전이며, 3-C2B-3 프로덕션 구현·3-C3은 미승인·미착수이고 Phase 3 Gate는 INCOMPLETE다. 소유자 승인은 Codex의 기술적 PASS를 뜻하지 않는다. ADR-007·game-engine·SYSTEM_CONTRACTS는 C2B-3-P 전체가 승인되기 전이므로 이번에도 고치지 않는다. (과거 기록: 이 항목은 이전 판에서 `OWNER_APPROVAL_REQUIRED`(미승인)로 적혀 있었고, 재검토 Codex가 승인 기록 누락을 지적해 정정했다.)

### 5.1 네 가지를 구분한다 (섞어 쓰지 않는다)

| 구분 | 내용 | 이 정책의 입장 |
|---|---|---|
| **① 실제 야구 규칙에 관한 주장** | 임시 근거 2026 OBR 5.08(a)의 승인 판정 사례: 제3아웃이 타자주자의 1루 도달 전 아웃이거나 포스 아웃이면 그 플레이의 득점은 인정되지 않고, 그 외(비포스) 제3아웃이면 아웃보다 **먼저 홈을 밟은** 득점만 인정된다. 핵심은 "득점 시점이 제3아웃 시점보다 앞서는가"라는 **시간 관계**다 | 인용만 한다. **KBO 2026 조항은 인증되지 않았다.** 이 문서가 KBO 규칙을 새로 주장하지 않는다 |
| **② 현 `GameReducer`가 구현한 검증 규칙** | `ThirdOutRule`/`ResolveOrder`: 아웃·득점 `Sequence`가 모두 있거나 모두 없어야 함(`Game.play_order_incomplete`); 3아웃과 득점이 한 플레이에 있으면 명시 순서 필수(`Game.play_order_required`); 타자주자 아웃·포스 아웃이 제3아웃이면 순서와 무관하게 득점 불인정; 비포스 제3아웃이면 더 작은 `Sequence`의 득점만 인정; 주자 아웃은 `OutKind` 포스/태그 필수, 어필 플레이 미지원(`Game.appeal_not_supported`). 순서를 생략하면 Reducer 기본 순서는 타자 → 선행 주자 | 변경하지 않는다. Reducer는 **어떤 순서를 사실로 만들어 주지 않고**, 주어진 순서를 규칙에 따라 판정할 뿐이다 |
| **③ 게임 프로토타입이 임의로 정의한 사건 순서** | 아래 5.2의 S1–S8. 시간 플레이를 모델링하지 않기 때문에 "누가 먼저 홈을 밟았나"를 관측할 수 없어 **결정적 규약으로 정한 가정**이다 | 이 정책의 본체. 승인 대상 |
| **④ 향후 정교한 주루·송구 시간 시뮬레이션이 필요한 영역** | 송구 거리·주자 속도에 따른 실제 득점/아웃 선후, 어필 플레이, 협살, 주자 추월 상세, 포스 상태 변화의 시간적 전개 | 범위 밖. 미래 단계(주루 시간 모델)에서 ③을 대체할 수 있으며 그때 이 정책을 개정한다 |

### 5.2 Prototype Sequence Policy v1 (소유자 승인, 2026-10-12)

- **S1 적용 범위:** C2B-2의 기본 계획이 열어 둔 현재 승인된 `advancement` 4개 맥락(`single_runner_on_first`, `single_runner_on_second`, `double_runner_on_first`, `fly_out_runner_on_third`)과 그 복합 상황에만 적용한다. 다른 맥락·타자주자 추가 진루·병살·야수선택은 별도 승인.
- **S2 판정 순서:** 복수 주자의 판정은 선행 주자(시작 베이스가 큰 쪽)부터 처리한다(P-01과 같은 순서).
- **S3 타자:** 포구된 뜬공의 타자 아웃은 항상 첫 사건이다(Reducer의 기존 규칙 `Game.batter_out_order_invalid`).
- **S4 배치:** 득점이나 아웃이 한 플레이에 공존하면 모든 득점·아웃에 명시 `Sequence`를 부여한다. **선행 주자의 득점 사건은 후행 주자의 비포스 주루사 사건보다 앞에 둔다**(Sequence가 더 작다).
- **S5 득점 인정:** 후행 주자의 비포스 태그 아웃이 제3아웃이면, 그보다 앞선 `Sequence`의 득점은 Reducer의 기존 규칙으로 인정된다.
- **S6 무효 규칙:** 제3아웃이 포스 아웃이거나 타자주자의 1루 도달 전 아웃이면 순서와 무관하게 모든 득점이 무효다(Reducer 기존 규칙, 이 정책이 새로 만들지 않음). 승인된 4개 맥락의 `out_on_bases`는 모두 비포스 태그 아웃이라 이 경우는 현 맥락에서 생성되지 않는다.
- **S7 성격:** S4의 선행 우선은 **실제 플레이 시간의 관측값이 아니라 결정적 시뮬레이션 가정**이다. 같은 입력은 항상 같은 순서를 만든다.
- **S8 비인증:** KBO 2026 공식 규칙 인증으로 표현하지 않는다. 결과·보고서·UI 문구에서도 "공식 판정"이라 쓰지 않는다.

### 5.3 민감도의 정직한 서술

합법 도달 벡터 중 **618개가 제3아웃으로 끝나고, 그중 318개가 인정되는 득점을 포함**한다. 이 318개에서 선행 주자의 득점을 후행 주자의 주루사보다 **먼저** 두면 득점이 인정되고, **나중에** 두면 318/318이 무효가 된다(`order_sensitivity` 318/318/318). 따라서 위 318개 벡터의 득점 결과는 **순서 가정의 함수**다. 우리는 이 민감도를 그대로 유지하고(숨기거나 평균내지 않는다), 선행 우선 결과를 **"실제 경기의 정답"으로 해석하지 않는다.** 사례: 1점 뒤짐·2/3루·2아웃 단타에서 2루 주자가 홈에서 아웃되면, 3루 주자의 득점을 먼저 두면 동점·연장 계속, 뒤에 두면 득점 무효로 원정 승리. 같은 하나의 사실이 가정에 따라 경기 결과를 뒤집는다.

### 5.4 대안과 선택지

| 선택 | 내용 | 평가 |
|---|---|---|
| **A (소유자 승인: `Prototype Sequence Policy v1`)** | S1–S8의 선행 득점 우선 | 구현 단순, Reducer 기본 순서와 일치, 공격 유리 가정임을 명시 |
| **B** | 후행 주자 사건 우선(득점을 뒤에 둠) | 318/318 득점 무효. 추가 진루 실패 시 선행 득점이 항상 무효가 되어 승인 확률표가 뜻하는 기대 결과와 멀어진다(추론). 비권장 |
| **C** | 시간 플레이 모델로 득점/아웃 선후를 추첨·계산 | 정확하나 미승인 신규 확률·상태 필요. 이번 단계 범위 밖(Scope Lock) — 미래 항목으로 기록 |
| **D** | 승인 보류 | 3-C2B-3은 득점+제3아웃 공존 플레이를 구현할 수 없다. 해당 맥락은 `Pending` 유지 |

### 5.5 요약 항목

1. **현재 코드 지원**: Reducer가 위 ②를 지원한다. 추가 진루 사건 생성은 없다.
2. **기존 승인 계약**: `docs/game-engine.md` §4 "Outs and runs on the play that ends the inning". 근거는 임시 OBR 5.08(a); KBO 2026 미인증.
3. **발견한 문제**: 순서가 정책 결과를 뒤집는다(5.3).
4. **대안**: 5.4.
5. **테스트 근거**: `Reducer_a_run_made_before_a_non_force_third_out_counts_and_after_it_is_voided_and_the_order_must_be_explicit`, `Reducer_walkoff_5_...`, `Reducer_a_force_out_or_a_batter_runner_out_as_the_third_out_voids_every_run_whatever_the_order`, 증거 `order_sensitivity`(318/318/318).
6. **승인된 정책**: 5.2의 A. 구현 시 승인된 정책 이름·버전(`prototype-sequence-v1`)을 감사에 기록(P-09).
7. **미해결 위험**: "선행 주자의 득점이 후행 주자의 아웃보다 먼저"는 공격 유리 가정이다. 시간 플레이를 모델링하면 무효 득점 빈도가 늘 수 있다. KBO 인증은 별도 과제로 남는다.
8. **소유자 승인**: **`OWNER APPROVED` (2026-10-12).** 득점 인정과 경기 결과를 바꾸는 정책임을 전제로 한 승인이며, 위 승인 범위(네 개 맥락·가상 순서·비인증·교체 가능)를 넘지 않는다.

## P-06 추가 진루 끝내기 공식 기록

1. **현재 코드 지원**: `walkoff-credit-v1`은 기본 계획에서 끝내기를 판정한다. 추가 진루로 비로소 끝나는 경우에는 `WinningRunner = null`이고 보류 상태다.
2. **기존 승인 계약**: `inplay-mapping-v1`, `walkoff-credit-v1`, `inplay-template-v1`, `InPlayWinningRunner`(D5).
3. **발견한 문제**: 추가 진루 뒤 달라지는 값: 결승 득점자(240개 끝내기 벡터에서 항상 기존 주자), Reducer가 파생하는 공식 `BatterResult`(이벤트 `Result = Out`인 뜬공 아웃에서 3루 주자가 득점하면 `BatterResult.SacrificeFly` 120벡터: AB 소멸, RBI), 타점, 득점, 아웃, LOB(무효 득점 주자 포함), 경기·이닝 종료. 인정 루수 변경은 현 맥락에서 0건.
4. **대안**: (a) 최종 Reducer 결과에서만 읽고 재계산 — 권장. (b) 기본 계획 값을 갱신해서 재사용 — 기각(오기록 위험).
5. **테스트 근거**: `Reducer_walkoff_1..8_*`, `Static_walkoff_7_no_extra_base_changes_the_credited_base...`, `Static_walkoff_8_every_extra_base_winner_is_an_existing_runner...`, `Reducer_advancement_changes_the_official_record_so_it_must_be_recomputed...`.
6. **권장 정책 — 계약 순서(앞 단계가 끝나기 전에 뒤 단계를 하지 않는다):**
   1. 추가 진루 추첨(P-07; 선행 주자 순).
   2. 합법성(P-02 R1·R2)과 사건 `Sequence`(P-05) 확정. 끝내기 이후 남은 맥락은 Void.
   3. **최종 이벤트 입력 확정.** 추가 진루로 득점·아웃이 바뀌면 최종 주자 이동·`Sequence`·수비 기록과 `walkoff-credit-v1` 인정 루수를 **이벤트를 만들기 전에** 이 단계에서 정한다. 이벤트의 `Result`는 **`GameEvents.cs`에 실제로 있는 `InPlayResult`**(`Single`, `Double`, `Triple`, `HomeRun`, `Out`, `FieldersChoice`, `ReachedOnError`)만 쓴다. 뜬공 아웃은 추가 진루가 있어도 `Out`이며, `SacrificeFly`는 `InPlayResult` 값이 아니므로 이벤트에 넣지 않는다. 현 4개 맥락에서는 인정 루수가 변하지 않음을 테스트로 유지한다.
   4. **최종 `InPlayEvent` 생성**(위 `InPlayResult`·이동·`Sequence`·수비 기록 포함). 승자는 최종 득점 순서의 `need`번째 득점자로 재계산(`InPlayWinningRunner`, 종류 `ExistingRunner`).
   5. `GameReducer.Apply`(원 상태에 적용, 미확정 미리보기).
   6. **Reducer가 파생한 최종 `ScoredPlay`/`GameStep`을 기대값과 대조·감사**: 공식 `BatterResult`(희생플라이 포함), 타수·타점·득점·아웃·LOB·결승 득점자(차이는 오류, 새 값을 만들지 않음). 감사는 **이벤트 입력(`InPlayResult`)과 Reducer 파생 기록(`BatterResult`)을 별도 필드로 기록**한다.
   - **Reducer 적용 뒤 이벤트의 `Result`나 이동을 수정하지 않는다.** 결과가 어긋나면 후보를 만들지 않고 실패로 돌려준다(Reducer의 공식 숫자가 정본).
   - **이벤트 입력 결과와 파생 공식 기록의 계약(코드 근거 `GameReducer.Plays.cs`의 `InPlayResult → BatterResult` 매핑과 희생플라이 분기):** `BatterResult.SacrificeFly`는 이벤트 `Result == Out`, `Batted == Fly`, `Sacrifice == false`, 해당 플레이의 아웃이 정확히 1개(타자), 플레이 시작 시 아웃이 `OutsPerHalfInning - 1` 미만(2아웃 미만), 오류가 아닌 득점이 1점 이상일 때 Reducer가 **파생**한다. 따라서 희생플라이에 필요한 조건(타구 유형 `Fly`, 최종 주자 이동과 사건 순서)은 4단계 이벤트를 만들기 전에 완성돼 있어야 하고, 5단계 이후 `Result`를 바꾸는 흐름은 없다.

   | 상황 | 이벤트 `Result` | 예상 `ScoredPlay` 기록 |
   |---|---|---|
   | 일반 뜬공 아웃, 득점 없음 | `Out` | `BatterResult.Out` |
   | 뜬공 아웃, 3루 주자 정상 득점(2아웃 미만, 아웃 1개) | `Out` | `BatterResult.SacrificeFly`(AB 소멸, RBI) |
   | 뜬공 아웃, 3루 주자 득점이 제3아웃 등으로 무효 | `Out` | 실제 Reducer 판정에 따름(플레이 아웃이 2개 이상이거나 시작 시 2아웃이면 SF 아님) |
   | 안타 뒤 추가 진루 | 실제 안타 `Single`/`Double`/`Triple` | 희생플라이 아님(안타 `BatterResult` 그대로) |
   | 추가 진루 실패로 득점 없음 | 실제 기본 `Result` | 최종 결과에 따름 |
7. **미해결 위험**: 승인 맥락이 늘어나 3루 주자가 추가 진루로 승자가 되면 인정 루수 축소가 필요할 수 있다.
8. **소유자 승인**: **`OWNER APPROVED` (2026-10-12).** 이벤트 입력은 실제 `InPlayResult`, 공식 기록은 Reducer가 파생한 `BatterResult`(희생플라이 포함). (과거 기록: 이 줄은 이전 판에서 "필요"였다.)

## P-07 RNG 소비·실패·재시도

1. **현재 코드 지원**: `GameRandom.Atomically`/`Restore`, 스트림 `game/runner@1`. 추가 진루 추첨 코드는 없다.
2. **기존 승인 계약**: C2B-2는 H1에서 시작해 소비 0회(H1→H1), 추가 진루 추첨은 H1에서 이어 H2로.
3. **발견한 문제**: 고정 소비는 K회(평균 1.50), 생략은 1–2회(1.306), 재추첨(부분 제외)은 1–9회. 호출 수가 같아도 거절 표집으로 raw 출력 수는 달라질 수 있다.
4. **대안**: (a) 맥락당 정확히 1회, 항상 소비 — 권장. (b) 결과가 강제되면 생략 — 소비가 결과에 의존. (c) 재추첨 — 가변·상한 필요.
5. **테스트 근거**: `Harness_fixed_consumption_uses_exactly_one_roll_per_declared_context_whatever_the_outcome`, `Harness_skip_consumption_makes_the_final_rng_position_depend_on_the_outcome...`, `Harness_rejection_sampling_may_use_extra_raw_draws...`, `Harness_the_advancement_draw_touches_only_the_runner_stream...`, Infrastructure `A_real_pending_advancement_exposes_H1_not_H0...`, `The_future_draw_from_H1_is_deterministic...`, `A_failed_or_cancelled_future_draw_leaves_the_position_unchanged...`.
6. **권장 정책**:
   - **시작점:** `PendingAdvancement.RngAfterPitchDraw`(H1)에서 `GameRandom.Restore`한 개인 사본에서 시작한다. 호출자의 권위 RNG는 H0이며(인수인계 §6.3) H1과 직접 비교하지 않는다. H0 일치는 최신 입력 재검증(P-10)이 먼저 확인한다.
   - **맥락별 논리적 추첨 규칙(원래 선언된 맥락 기준):** 기본 계획의 선언된 `AdvancementKeys` 맥락 수를 K라 한다. 선행 주자 순서로 **각 선언 맥락에서 정확히 한 번** `game/runner@1`의 `NextBounded(1,000,000)`을 호출한다. 맥락의 롤은 그 시점의 플레이 상태와 무관하게 항상 소비한다.
   - **Void 맥락의 소비:** 앞선 결과로 플레이가 이미 끝난(제3아웃·결승 득점) 뒤의 맥락도 롤을 **소비하고 버린다**(결과는 Void/Hold 귀속). 강제 Hold(점유 충돌)도 같다. 소비를 결과에 의존시키지 않는다.
   - **재추첨 금지**, 불가능하거나 불법인 결과는 다시 뽑지 않고 P-03·P-02 규칙으로 귀속한다.
   - **논리적 호출 수와 raw `Draws`는 다르다:** 호출 수는 K로 고정이지만 거절 표집으로 raw 출력 수(`RngStream.Draws`)는 더 클 수 있다. 감사는 호출 수가 아니라 스냅샷 해시로 재현성을 증명한다.
   - **재현성:** 같은 H1과 같은 입력이면 항상 같은 롤·같은 H2. 실패·취소 시 H1 불변(스냅샷은 값 객체, `Atomically`는 성공했을 때만 H2를 공개), 재시도는 H1에서 같은 롤을 다시 얻는다. 테스트는 H1→H2 재현과 실패/취소 뒤 H1 동일을 모두 확인한다(현재는 스탠드인 추첨으로 확인, 실제 Resolve로는 구현 시).
   - **최종 확정은 C3 책임:** H2의 채택은 C3가 상태·ID·영수증과 함께 하며 Resolve는 어떤 것도 채택하지 않는다.
7. **미해결 위험**: 고정 소비는 필요 없는 롤을 소비하지만 비용은 평균 0.19회/플레이 수준(생략 대비).
8. **소유자 승인**: **`OWNER APPROVED` (2026-10-12).** H0 검증 → H1 사본 → 맥락당 1회 → H2, 채택은 C3. (과거 기록: 이 줄은 이전 판에서 "필요"였다.)

## P-08 선수별 주루·수비 능력 공급 계약

1. **현재 코드 지원**: `HitterRatings.Speed`/`Arm`은 `PlayerSheet`에 있으나 `GameState`·`PlanBase`·`Build`로 흐르지 않는다. 모델에는 `runner.speed`(Extra), `defense.arm`(Out) 슬롯이 선언되어 있다.
2. **기존 승인 계약**: C2A는 어떤 modifier도 공급하지 않는 중립 추첨(소스 검사 테스트). 두 슬롯은 선형, 중립 50, 기울기 2000 ppm/단위, 범위 0.9–1.1.
3. **발견한 문제**: 주자 능력·송구 수비수 시트·피로/컨디션 슬롯이 없다(`MISSING_INPUT`/`NO_SLOT`).
4. **대안**: (a) 첫 구현은 중립 — 권장. (b) 능력 연결을 같은 단계에 포함 — 범위 확대.
5. **테스트 근거**: `Static_missing_input_the_game_state_holds_runners_and_defenders_as_ids_only...`, `Static_missing_input_the_planning_entry_points_receive_only_the_batter_and_pitcher_sheets`, `Static_missing_input_the_model_declares_the_speed_and_arm_slots_but_the_pitch_resolver_supplies_no_modifier_at_all`.
6. **권장 정책**: 첫 구현은 중립으로 추첨하고, 능력 연결은 별도 승인 단계로 둔다. 필요한 최소 계약 변경은 인수인계 초안 §3(`AdvancementInputs`: 결정별 주자 속도·수비수 송구 `ModifierInput`, 호출 경계에서 시트 공급, 프로덕션 필드 추가 없이 입력 DTO).
7. **미해결 위험**: 결과 확률의 정직성: 능력이 연결되지 않으면 모든 주자가 같은 확률(27 %/59 % 등)로 진루한다.
8. **소유자 승인**: **`OWNER APPROVED` (2026-10-12), 첫 구현은 중립.** 중립은 능력치 0이 아니며 새 `PlayerSheet` 입력·가상 능력치·`AdvancementInputs`를 이번 구현에서 만들지 않는다. 능력 연결은 향후 별도 계약·버전. (과거 기록: 이 줄은 이전 판에서 "필요"였다.)

## P-09 추가 진루 이후 감사 정보

1. **현재 코드 지원**: `InPlayAudit.Advancement` 문자열(`pending|not_required`)과 `AdvancementContextKeys`만 있다.
2. **기존 승인 계약**: D5 감사 레코드는 `ef1`/`ef2`·이벤트 JSON·해시에 들어가지 않는 Simulation 내부 증거다(영속화는 C3/Phase 4).
3. **발견한 문제**: 원 추첨과 적용 결과가 다른 경우(Hold 귀속)와 Void를 재현하려면 기록이 필요하다.
4. **대안**: (a) 맥락별 레코드 — 권장. (b) 최종 결과만 — 재현 불가.
5. **테스트 근거**: 해당 정책의 결과 키(`E/H/O/V`), 정확 분포 열거, Infrastructure 해시 테스트.
6. **권장 정책 — 감사 데이터 최소 항목:**
   - **맥락별 처리 순서:** 처리 순번(선행 주자 우선의 몇 번째인지), `context_key`, `runner_key`/`player`, `start_base`/`plan_to`/`target_to`.
   - **원본 추첨 결과:** 스트림 ID·호출 순번, `roll`, 사용한 확률(ppm 3개), `drawn_outcome`.
   - **실제 적용 결과:** `applied_outcome`(Extra/Hold/Out/Void)와 `reason`(`none|blocked_base|play_over`).
   - **원본 행동과 최종 사건 Sequence:** 맥락별 원 행동(예: 시도·정지·아웃), 최종 이벤트에서 그 행동이 가진 `Sequence`(부여되지 않으면 null).
   - **H1/H2 RNG 해시:** `start`=H0(이 투구 전체 범위), `after_pitch_draw`=H1, `final`=H2.
   - **적용 정책과 버전:** P-01~P-10 중 쓰인 정책 식별자와 버전(예: `prototype-sequence-v1`, D3 정책 `B`, 시도 결합 규칙), `prob-model`/tuning 버전·해시.
   - **후보 검증의 성공·실패 구분:** 최신 입력 검증 통과 여부와 실패 시 오류 코드(`PitchCandidate.*` 등), EventId 경계 검증 결과, Reducer 검증 결과(성공/거부 사유)와 `Verify` 결과. 실패한 시도는 후보를 만들지 않으므로 이 기록은 오류 반환/로그 증거로만 남는다(영속은 C3/Phase 4).
   - 그 외: 재계산한 `WinningRunner`, 공식 결과 라벨 변경 여부. `advancement` 필드는 `resolved(...)`.
7. **미해결 위험**: 영속 스키마와 보존(D6)은 C3/Phase 4·17·29에서 측정 후 결정.
8. **소유자 승인**: **`OWNER APPROVED` (2026-10-12).** `ef1`/`ef2`·이벤트 JSON은 변경하지 않는다. (과거 기록: 이 줄은 이전 판에서 "필요"였다.)

## P-10 C3 인수인계 및 최종 후보 계약

1. **현재 코드 지원**: `InPlayPitchCandidate`(이벤트, 임시 ID, 미확정 `GameStep`, `NextRng`, 감사). `PendingAdvancement`는 형제 타입이며 후보가 아니다.
2. **기존 승인 계약**: 형제 타입 격리, 후보만 Reducer 검증, 최종 확정은 C3.
3. **발견한 문제**: (i) 추가 진루 결과가 후보가 되려면 `NextRng`가 H2여야 한다. (ii) Codex 지적: 이전 초안의 입력 `Resolve(PendingAdvancement, TuningProfile, CancellationToken)`는 최신 상태·권위 RNG·현재 선수·EventId 경계를 검증할 수 없었다 → 위 6과 인수인계 §6으로 정정.
4. **대안**: (a) `PendingAdvancement`를 소비해 기존 `InPlayPitchCandidate`를 만든다 — 권장. (b) 새 후보 타입 — 불필요한 타입 증가.
5. **테스트 근거**: 기존 C2B-2 격리 테스트(전체 xUnit 통과), Infrastructure 핸드오프 테스트(`AdvancementPolicyHandoffTests`: H0/H1 구분, 실제 Pending의 준비 투구에 대한 오래된 입력 거부 반례). `Resolve` 자체의 테스트는 구현 후(존재하지 않는 API를 대상으로 하지 않았다).
6. **권장 정책**: 새 내부 함수가 `PendingAdvancement`와 **현재 권위 입력(GameState, H0 RNG 스냅샷, 현재 타자·투수 시트, 튜닝, 읽기 전용 마지막 발급 EventId 순번)** 을 받는다. **가장 먼저** 기존 `PitchPreparation.CheckPrepared`로 입력이 아직 Pending이 만들어진 때와 같은지 재검증하고(인수인계 §6, 오류 코드·우선순위 불변), 이어 EventId 경계를 검사한 뒤에야 (P-01~P-07)대로 해결한다. 최신 입력 재검증을 거치지 않은 후보는 만들지 않으며, C3의 재검사가 이를 대체하지 못한다. 해결 후 Reducer 검증을 거쳐 `InPlayPitchCandidate`(`NextRng = H2`, `Audit.Advancement = resolved(...)`)를 반환한다. `PendingAdvancement` 자체는 계속 후보가 아니며 `GameState`/ID 커서/영수증은 건드리지 않는다. 원자적 확정·마지막 발급 순번 재검사는 C3.
7. **미해결 위험**: C3 설계 전에는 H2 채택 순서(상태·ID·영수증·RNG 동시 커밋)를 검증할 수 없다.
8. **소유자 승인**: **`OWNER APPROVED` (2026-10-12).** 구현 시 개념 서명과 인수인계 §6 검증 계약을 실제 서명과 대조해 확정한다. (과거 기록: 이 줄은 이전 판에서 "필요"였다.)

---

## 정책별 반례 (이 초안이 틀릴 수 있는 지점)

| 정책 | 반례 |
|---|---|
| P-01 선행 우선 | 선행 주자가 아웃당하면 송구가 홈으로 가므로 후행 주자가 더 쉽게 진루한다 — 모델에 없다. 선행 우선이 이를 더 잘 표현하는 것은 아니다 |
| P-02 시도 결합 | "3루 주자가 Hold인데 2루 주자가 3루로 가다 태그아웃"을 막지만, 현실의 협살·더블 스틸 변형은 모델 밖이다 |
| P-03 B | Hold가 사실상 "시도 포기"가 되어 후행 주자의 Extra가 크게 줄어든다(2결정 사례 평균 27.0 % → 12.9 %, 상대 −52 %; Hold는 70.0 % → 71.5 %로 +1.5 %p, 나머지는 플레이 종료 Void 14.1 %). 3-F에서 모델 계수를 다시 정해야 한다 |
| P-05 순서 | 선행 주자의 득점을 항상 먼저로 두는 것은 공격 유리 가정이며 실제 야구의 관측 순서가 아니다(318개 벡터의 득점 결과가 이 가정의 함수). 시간 플레이를 모델링하면 득점이 무효가 되는 빈도가 늘 수 있다. 소유자는 이 가정을 알고 승인했다(2026-10-12); 시간 기반 주루 모델로 교체 가능 |
| P-07 고정 소비 | Void 사례에서 쓰이지 않는 롤을 소비하는 것을 낭비로 볼 수 있다(대신 H2가 결과와 무관해진다) |
| P-08 중립 | 모든 주자가 같은 확률로 진루해 선수 개성이 사라진다 — 의도된 임시 상태여야 한다 |
| P-10 입력 계약 | Pending을 만든 뒤 상태가 바뀌면 오래된 Pending이 재사용될 수 있다 — `CheckPrepared` 재사용으로 막지만, 영수증 목록보다 큰 커서(번호 건너뜀)는 Resolve에서 감지하지 못하고 C3가 막는다 |
