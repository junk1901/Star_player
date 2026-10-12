# Phase 3-C2B-3-P 정책 검증 보고서 — 추가 진루(`advancement`) 정책 실현성

날짜: 2026-10-12 · 소유자 지시: `TASK.md` · 기준 HEAD `f669589c48f3b7deb72ce828f13d2a186bae2a7a` (`main`, origin 대비 ahead 1 = C2B-2 커밋, 미푸시) · 상태: **정책 검증 수행 → 정책 P-01~P-10 소유자 승인 완료(2026-10-12, §20) / 구현 `NOT AUTHORIZED`** (최초 제출 시 상태는 `PROPOSED`/승인 대기였으며 이 기록은 보존) · 자체 판정: **`POLICY_READY_FOR_REVIEW`**(소유자 검토 가능). GPT Chat·Codex 검토 `미실시`. 이 보고서는 승인이 아니다. **FIX2(2026-10-12):** P-05 소유자 승인 기록 정정과 P-06 이벤트 입력/파생 기록 계약 정정은 본문 §19에 기록했다(§1–§18의 당시 서술은 이력으로 보존하며, P-05·`SacrificeFly` 관련 현행 상태는 §19가 우선한다). **Codex 리뷰 수정 반영(2026-10-12):** Codex Medium 2건(후보 생성 입력·최신 상태 검증 계약 누락 / P-05 소유자 승인·의미 정의 미완료)에 대한 문서·테스트 보완은 §18에 기록했다. §1–§17의 수치와 증거는 최초 제출 결과로 보존했고 바꾸지 않았다.

범위는 **정책 검증**뿐이다. `src/`(프로덕션)는 한 줄도 바꾸지 않았다. 3-C2B-3(실제 `game/runner@1` 추첨 엔진), 3-C3, Phase 4는 미착수이며 Phase 3 Gate는 **INCOMPLETE**이다. 결정 초안은 [정책 결정 초안](PHASE_3C2B3_POLICY_DECISION_DRAFT.md), 구현 인수인계 초안은 [인수인계 초안](PHASE_3C2B3_IMPLEMENTATION_HANDOFF_DRAFT.md)을 본다.

## 1. 한눈에 보기

| 질문 | 측정 결과 |
|---|---|
| 보류(Pending) 1,314건은 모두 재구성되었나 | 예. 일반 300 + 끝내기 1,014 = 1,314, 복합 864 (210 + 654). C2B-2 기준선과 정확히 일치 |
| 복합 864건이 모두 "주자 간 상호작용"인가 | **아니다.** 열린 결정이 2개인 사례는 **270건**뿐이고 나머지 594건은 결정이 1개(다른 주자의 기본 플레이는 이미 완결)다. F-1의 "864건" 표현은 상호작용 범위를 과대 표시한다 |
| 독립 추첨을 그대로 합성하면 | 2결정 사례 평균 **15.63 %**의 추첨이 야구상 불가능한 플레이(대표 사례 11.4 % + 3아웃 이후 이동). 5,562개 원시 벡터 중 840개 불법, **Reducer는 360개만 거부하고 480개는 수락** |
| D3 정책 A(재정규화) vs B(Hold 귀속) | 불가능성을 "시도 결합"으로 정의하면 **1,314건 전수에서 두 정책의 정확 분포가 동일**. 차이는 일부 결과만 제외하는 (비권장) 변형에서만 생김(아웃 4.11 % vs 3.00 %) |
| 추첨 순서 | 선행 주자 우선 순차 = 후행 우선 + 사후 보정(분포 동일, 1,314건 전수). 후행 우선 + 사전 가능성 판정은 후행 주자의 추가 진루가 **0 %** |
| 제3아웃·득점 순서 | 득점+제3아웃이 공존하는 합법 벡터 **318개**. 사건 순서가 선행 우선이면 득점 인정, 후행 우선이면 **318/318 득점 무효** → 순서는 반드시 정책으로 고정해야 한다 |
| 끝내기 | 합법 끝내기 벡터 240개. 인정 루수 변경 0건(현 4개 맥락), 결승 득점자는 모두 기존 주자, 기본 계획의 `WinningRunner`는 전부 null이라 **재계산 필수** |
| RNG | 고정 소비(맥락당 롤 1회)에서 H2는 H1과 맥락 수 K로 결정. 실패·취소 시 H1 불변, 재시도는 같은 롤 |
| 선수 능력 | 공급 경로 **없음**(`MISSING_INPUT`): 주자 주루, 송구 수비수, 피로·컨디션 슬롯 |
| 전체 xUnit | **1,451 통과**(기준 1,387 + 신규 64) |

## 2. 환경과 시작 상태

| 항목 | 값 |
|---|---|
| 저장소 | `E:\Madegame\Star_player`, 브랜치 `main`, HEAD `f669589` (`feat: add phase 3c2b2 in-play candidate planning`) |
| 원격 대비 | ahead 1 / behind 0 (C2B-2 커밋 미푸시, 이번 작업에서도 푸시하지 않음) |
| 시작 시 작업 트리 | 추적 파일 변경 없음. 미추적: `TASK.md`, `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf` (읽기만 함, 변경·스테이징 없음) |
| `git diff --check` | 출력 없음(종료 코드 0) |
| 도구 | .NET SDK(`global.json` 고정), xUnit, PowerShell 5.1/Git Bash. Godot는 `verify.ps1`에서만 사용 |
| 기준선 | 변경 전 전체 xUnit 1,387 통과 ([기준선 로그](PHASE_3C2B3P_BASELINE_XUNIT_raw.txt): Domain 263, Application 117, Simulation 537, Infrastructure 470) |

## 3. 실제로 분석한 코드와 문서

코드: `InPlayBasePlanner.cs`, `InPlayCandidateTypes.cs`, `InPlayPitchPlanning.cs`, `InPlayOfficialCredit.cs`, `InPlayTemplateCatalog.cs`, `PitchPreparation.cs`, `PitchBranchResolver.cs`, `GameReducer.Plays.cs`, `GameReducer.Order.cs`, `GameReducer.Finish.cs`, `ThirdOutRule.cs`, `GameRandom.cs`, `RngStreamId.cs`, `Pcg32.cs`, `ProbabilityModel.cs`, `StageProbability.cs`, `ProbabilityScale.cs`, `PlayerSheet.cs`, `content/tuning/virtual_tuning_v1.json`(`advancement` 4행·modifier 표), 기존 테스트 하네스(`InPlayStates`, `InPlayProductionHarness`, `FeasibilityData`).

문서: `AGENTS.md`, `CURRENT_STATE.md`, `docs/game-engine.md`(§4 "Outs and runs on the play that ends the inning", §6, §23, §24), [ADR-007](../decisions/ADR-007.md), `docs/SYSTEM_CONTRACTS.md` C08, [C2B-2 보고서](PHASE_3C2B2_REPORT.md), `Star/01_Phase_Reviews/Phase_03_Review.md`, `docs/2026-official-baseball-rules.pdf`(OBR 2026, 5.08(a)의 승인 판정 사례만 확인).

`PHASE_3C2B3_POLICY_DRAFT.md`, `TASK_NEXT_PHASE_3C2B3_POLICY.md`, 정책 분석 CSV는 **저장소에 없다**(Glob 확인). 존재를 가정하지 않았고, 승인된 C2B-2 계약과 기존 오라클에서 검증 범위를 재구성했다(§5).

**문서와 코드의 충돌: 없음.** 용어 정밀화 1건만 있다: C2B-2 보고서·`game-engine.md` §23.5의 "복합(composite) = 주자 2명 이상"은 코드(`keys.Length > 0 && runners.Length > 1`)와 일치하지만, 이것이 곧 "열린 결정 2개"를 뜻하지 않는다(§5).

## 4. 증거의 세 범주 (섞어 보고하지 않는다)

| 범주 | 뜻 | 위치 |
|---|---|---|
| `STATIC_POLICY_CHECK` | 독립 판정기(`AdvJudge`)가 규칙·조합을 판정. 엔진 호출 없음 | 테스트 `Static_*`, 판정기 `AdvancementPolicyJudge.cs` |
| `TEST_HARNESS_CHECK` | 이 작업에서 만든 테스트 전용 정책 구현(`AdvModel`: 리졸버, 정확 분포, 실제 `GameRandom` 스트림 소비) | 테스트 `Harness_*` |
| `ACTUAL_REDUCER_CHECK` | 이 작업에서 만든 이벤트를 **변경하지 않은** 프로덕션 `GameReducer.Apply`에 적용 | 테스트 `Reducer_*` (일부 `Harness_*`가 reducer를 증인으로 사용) |

세 범주 모두 "실제 엔진 통합 테스트"가 **아니다.** 3-C2B-3 엔진은 존재하지 않는다. `tests/.../AdvancementPolicy/`의 모든 코드는 테스트 전용이며 프로덕션 코드로 쓰이지 않고, 새 public API도 없다.

신규 테스트 64건: Simulation 60(`AdvancementPolicyTests` 59 + `AdvancementPolicyEvidence` 1), Infrastructure 4(`AdvancementPolicyHandoffTests`, 실제 `PreparedPitch` 사용). 이름 목록과 건별 결과: [신규 테스트 로그](PHASE_3C2B3P_NEW_TESTS_raw.txt).

## 5. 보류 1,314건의 범위 (재구성 방법)

원본 오라클 CSV가 없으므로 C2B-2와 같은 방식으로 재구성했다: 기존 테스트 하네스 `InPlayStates.Make`(실제 `GameReducer.Apply`로만 만든 상태)에서 양의 확률 45개 조합 × 8 주자 점유 × 3 아웃(× 끝내기 4개 점수차)에 **프로덕션 `InPlayBasePlanner.Plan`**을 실행하고 `AdvancementRequired`인 사례만 취했다. 결과는 C2B-2 기준선과 정확히 같다(`Static_the_1314_pending_cases_...`): 일반 300 / 끝내기 1,014 / 복합 864(210 + 654).

| 열린 결정 구조 (`ContextKeys`) | 일반 | 끝내기 | 합계 |
|---|---:|---:|---:|
| `single_runner_on_first` (1루 주자 1명의 결정) | 60 | 210 | 270 |
| `single_runner_on_second` (2루 주자) | 60 | 210 | 270 |
| `single_runner_on_first` + `single_runner_on_second` (**상호작용**) | 60 | 210 | **270** |
| `double_runner_on_first` | 96 | 288 | 384 |
| `fly_out_runner_on_third` | 24 | 96 | 120 |
| 합계 | 300 | 1,014 | 1,314 |

- 결정 1개 1,044건, 결정 2개 270건(손 유도: 공식 단타 10개 조합 × 점유 {1·2루, 만루} × 3아웃 = 일반 60; 끝내기는 점유 3에서 4개 점수차, 점유 7에서 3개 점수차 → 10 × 3 × 7 = 210).
- 복합 864건 중 결정 2개는 270건, **594건은 결정 1개**(예: 단타에서 1·3루: 3루 주자는 기본 계획에서 득점 완결, 1루 주자만 결정).
- 추가 주루사 **한 건만으로** 제3아웃이 되는 사례 458건(단타·2루타 2아웃, 뜬공 1아웃). 결정 2개 사례는 1아웃에서도 두 주자의 아웃이 합쳐 제3아웃이 된다.
- 타자주자는 승인된 4개 맥락 어디에서도 열린 결정이 없다(`Static_every_decision_attempts...`: 모든 결정이 기존 주자, `StartBase` 1–3).
- 시도 대상 베이스가 점유될 수 있는 결정은 `single_runner_on_first`(대상 3루) 하나뿐이다. 홈은 막히지 않는다.

원시 행동 벡터(각 결정 {Extra, Hold, Out}): 1,044 × 3 + 270 × 9 = **5,562**. 전체 표: [`PHASE_3C2B3P_CASE_VECTORS.csv`](PHASE_3C2B3P_CASE_VECTORS.csv), 요약: [`PHASE_3C2B3P_POLICY_SUMMARY.json`](PHASE_3C2B3P_POLICY_SUMMARY.json).

## 6. 핵심 검증 A — 복수 주자 추가 진루

### 6.1 주자 판정 순서 (A-1)

방식 B("기존 `ContextKeys` 순서")를 코드에서 확인했다: `InPlayBasePlanner.ContextKeys`는 베이스 순서로 키를 추가하므로 단타는 `single_runner_on_first` → `single_runner_on_second`, 즉 **후행(트레일링) 주자가 먼저**다(`Static_the_production_context_key_order_is_trailing_runner_first...`).

| 비교 항목 | 방식 A: 선행 주자 우선 순차 (3루→2루→1루) | 방식 B: 키 순서(후행 우선) |
|---|---|---|
| 후행 주자와의 충돌 | 후행 주자 차례에 선행 주자의 결과가 확정되어 실제 점유로 즉시 판정 | **사전 판정**: 선행 주자가 아직 3루에 서 있어 후행 주자의 추가 진루가 항상 불가 → 2결정 270건 전부에서 후행 주자 Extra **0 %**, Hold 100 % (`A/B-T-pre`). **사후 보정**: 독립 추첨 후 선행 우선으로 재생해 충돌 해소 |
| 베이스 점유 유효성 | 도달 가능 4,722벡터 전부 합법·Reducer 수락 | 사전 판정 3,942벡터 합법(Extra 소실), 사후 보정 4,722벡터 합법 |
| 주자 추월 | 0건 | 0건 |
| 득점 순서 | 추첨 순서 = 사건 순서(선행 먼저) | 추첨 순서 ≠ 사건 순서 → 사건 순서를 별도로 고정해야 함 |
| RNG 호출 순서 | 선행 → 후행 | 후행 → 선행 (동일 시드에서 롤 배정이 달라져 일부 시드에서 다른 플레이. 2,000시드 중 일부만 다름: `Harness_draw_order_changes_which_roll...`) |
| 결과 재현성 | 동일 시드 동일 결과 | 동일 |
| **정확한 최종 분포** | 기준 | **사후 보정은 방식 A와 1,314건 전수에서 정확히 같다** (`Harness_post_hoc_repair_...`). 증명: 독립 추첨이므로 선행 주자가 Hold일 때 후행 주자의 Extra/Out이 Hold로 바뀌는 것과 선행 주자가 Hold일 때 후행 주자에게 롤 없이 Hold를 주는 것은 같은 분포 |

선행 우선이 직관적이라서 채택하지 않는다. 채택 근거는 (i) 사건 순서의 Reducer 기본값과 일치, (ii) 추첨 시점에 실제 작업 상태로 가능성을 판정(사후 보정 불필요), (iii) 사전 판정이 후행 우선에서는 성립하지 않는다는 측정 결과다.

### 6.2 독립 추첨과 합성 (A-2)

`IND-L`(맥락별 독립 추첨, 보정 없음)이 도달하는 5,562벡터 중 **840개가 불법**(15.1 %), 2결정 사례의 평균 불법 확률 **156,333 ppm(15.63 %)**, 전체 1,314건 평균 32,123 ppm. 대표 사례(일반, 0아웃, 1·2루, 단타) 정확 값: 선행 주자 Hold 38 % × 후행 주자 {Extra 27 % + Out 3 %} = **11.4 %**(Extra 10.26 %, Out 1.14 %).

판정기와 실제 Reducer를 5,562벡터 전수에서 대조한 결과(`Reducer_*`):

| 분류 | 건수 | 의미 |
|---|---:|---|
| 규칙 합법 + Reducer 수락 + 기록(득점 수·아웃·종료·득점자) 일치 | **4,722** | `LEGAL_MISMATCH` 0건 |
| 불법, Reducer 거부 `Game.base_occupied_twice` | 270 | 선행 주자 Hold + 후행 주자 Extra (3루 이중 점유) |
| 불법, Reducer 거부 `Game.too_many_outs` | 90 | 2아웃에서 두 주자 모두 아웃 |
| **불법, Reducer 수락** — 점유된 3루에서 후행 주자가 태그아웃 | **270** | 선행 주자가 3루에 있는데 "3루에서 아웃" |
| **불법, Reducer 수락** — 제3아웃/결승 득점 이후의 이동 | **210** | 2아웃에서 선행 주자가 3번째 아웃을 당한 뒤 후행 주자 진루(90) + 끝내기 득점 뒤의 이동(120) |

결정 1개 사례 3,132벡터는 전부 합법이다. **"Reducer가 받아들이는 이벤트 = 합법"이 아님**이 840 중 480건으로 실증되었다. 야구상 합법성은 정책 계층의 책임이며, 최종 이벤트는 Reducer로 다시 검증해야 한다(P-04).

한 주자가 정지하고 후행 주자가 진루하려는 경우, 선행 주자가 아웃되는 경우, 추가 진루로 베이스가 비워지는 경우(후행 주자는 가능), 두 주자가 같은 베이스를 목표로 하는 경우(승인된 맥락에서는 목표 베이스가 서로 달라 `Static_every_decision...` 확인), 강제 진루와 선택적 진루가 섞이는 경우(승인된 맥락에서 `out_on_bases`는 모두 비포스 태그 아웃이며 강제 진루는 기본 계획에서 이미 완결)는 모두 위 벡터 표에 포함되어 판정되었다.

## 7. 핵심 검증 B — D3 확률 정책

### 7.1 정의

불가능한 결과를 두 방식으로 처리한다. "불가능"의 정의는 **시도 결합(attempt-coupled)**: 시도할 베이스가 점유되어 있으면 추가 진루(Extra)와 그 시도 중 아웃(Out)이 모두 불가능하다(둘 다 그 베이스로의 시도). 닫힌 플레이(제3아웃·결승 득점 이후)에는 결정이 없다(Void, 베이스 계획 위치에 동결).

- 정책 A: 가능한 결과만 남기고 `ProbabilityScale.Normalize`로 재정규화 후 추첨.
- 정책 B: 승인된 행 그대로 추첨하고 불가능하면 Hold로 귀속. 원래 추첨 결과는 감사에 남긴다.
- 정책 C(제3 대안): 불가능하면 가능한 결과가 나올 때까지 재추첨(최대 64회).

비교 대상은 소비 방식(고정: 맥락마다 1회 항상 / 생략: 결과가 이미 강제되면 추첨 안 함)과 불가능성 정의(시도 결합 / 추가 진루만 제외)로 모두 12개 정책 변형이다(`PolicySpec.All`).

### 7.2 결과

| 비교 항목 | 정책 A (재정규화) | 정책 B (Hold 귀속) |
|---|---|---|
| **합법성** | 시도 결합: 도달 4,722벡터 모두 합법·Reducer 수락·기록 일치(불법 0, 거부 0). 추가 진루만 제외 변형: 270벡터가 점유된 베이스에서의 아웃에 도달, **Reducer는 전부 수락**(평균 불법 확률 A 3,209 ppm) | 시도 결합: A와 같음(0/0). 추가 진루만 제외 변형: 270벡터 불법, 수락(평균 2,343 ppm) |
| **결정성** | 12개 정책 × 대표 사례 × 시드 50개 전부 동일 시드 동일 결과·호출 수·최종 위치(`Harness_the_same_seed...`) | 동일 |
| **RNG 소비** | 고정: 맥락 수 K회(평균 1.50). 생략: 1–2회(평균 1.306) | 고정: K회(평균 1.50). 생략: 1–2회(평균 1.306). 정책 C(추가 진루만 제외): 1–9회(평균 1.565) |
| **원래 확률 의미** | 불가능 결과를 조건으로 놓은 조건부 분포. 일부 결과만 제외하면 `out`이 3.00 %→4.11 %(×1.37), `hold` 97 %→95.89 %(`Normalize` 반올림 규칙 의존: 958,904 / 41,096 ppm) | 승인 행을 그대로 쓰고 제외된 질량 전부가 Hold로 간다: `hold` 97.00 %, `out` 3.00 % 유지. 시도 결합이면 Hold 100 % |
| **시도 결합 시 두 정책의 분포** | **1,314건 전수에서 정확히 동일** (`Harness_with_attempt_coupling_policies_A_and_B_give_the_same_exact_distribution...`). 가능한 결과가 {Hold}뿐이라 두 방식 모두 Hold 100 % | 동일 |
| 구현 복잡도 | 가능 집합 계산 + 재정규화 + 새 `Distribution` 생성(반올림 규칙 의존) | 가능 집합 계산 + 매핑. 새 분포 없음 |
| 장기 확장성 | 새 맥락에서 부분 제외가 생기면 재정규화 규칙이 의미를 정해야 함 | 부분 제외도 "원 추첨 + 귀속 사유"로 감사 가능. 확률표 불변 |
| 권장 여부 | 동등(결과 분포가 같다). 새 반올림 규칙이 필요해 열세 | **권장**(P-03) |

두 정책의 결과가 다른 이유: 시도 결합에서는 가능한 결과가 하나뿐이라 재정규화와 귀속이 같은 점으로 수렴한다. 차이는 **일부 결과만 남는 경우**(예: Extra만 제외, `hold`와 `out` 둘 다 가능)에서만 생기며, 그때 A는 조건부 비율을 보존하고 B는 `out`의 주변 확률을 보존한다. 그러나 이 변형은 "선행 주자가 3루에 서 있는데 후행 주자가 3루에서 태그아웃"을 만들며 Reducer가 이를 수락한다(표의 불법 270). 즉 의미상 올바른 불가능성 정의는 시도 결합이고, 그 아래에서는 A/B 선택이 결과를 바꾸지 않는다.

**재추첨(정책 C)**: 시도 결합에서는 재추첨이 발생하지 않고(가능한 결과가 하나면 추첨 생략) 분포는 A와 같다. 추가 진루만 제외하는 변형에서만 재추첨이 일어나며, 호출 수가 최대 9회로 가변이다(`Harness_skip_consumption...`). 필요성: 없음. 위험: 최종 RNG 위치가 결과에 의존하고 상한 처리가 필요하다.

**Hold 증가·편향**: 후행 주자(1루, 2결정 사례 270건 평균): Extra 270,000 → 129,300 ppm(−52 %), Hold 700,000 → 715,222(+2.2 %), Out 30,000 → 14,367, 닫힌 플레이 Void 141,111(14.1 %). 감소의 대부분은 선행 주자 Hold(38 %)와 플레이 종료이며 Hold 자체의 증가는 작다. 주자 순서에 따른 왜곡: 후행 우선 + 사전 판정만 0 %로 붕괴(§6.1). 모든 수치는 `PHASE_3C2B3P_POLICY_SUMMARY.json` `policies[*].marginals_two_decision_cases`.

**최종 D3 정책은 이 작업에서 자동 승인하지 않았다.** 권장안은 [결정 초안](PHASE_3C2B3_POLICY_DECISION_DRAFT.md) P-03이다. **범위 한정(리뷰 수정):** A/B 분포 동등성은 현재 승인된 4개 `advancement` 맥락에서 점유 충돌이 Extra와 Out을 **함께** 막을 때의 측정 결과이며, Hold 귀속이 모든 미래 맥락에서 재정규화와 같다는 일반 주장이 아니다.

## 8. 핵심 검증 C — 주루사와 제3아웃

기존 `GameReducer`의 계약(`ThirdOutRule`, `ResolveOrder`)과 `docs/game-engine.md` §4를 코드로 확인했다: 제3아웃이 타자주자 아웃 또는 포스 아웃이면 순서와 무관하게 득점 불인정, 그 외 아웃이면 그보다 **먼저**(작은 `Sequence`) 홈을 밟은 득점만 인정, 득점+제3아웃이 한 플레이에 있으면 명시 순서 필수(`Game.play_order_required`). 근거는 임시로 2026 OBR 5.08(a)의 승인 판정 사례(3아웃 전에 홈을 밟은 득점은 비포스 아웃이면 인정)이며 **KBO 2026은 인증되지 않았다.**

| 필수 검증 | 결과 (테스트) | 범주 |
|---|---|---|
| 0아웃 추가 주루사 | 1루 주자 3루 아웃: 아웃 1, `OutKind.Tag`, `NonForce`, `OutAt=3`, 3B 자살·CF 보살 (`Reducer_nobody_out_...`) | REDUCER |
| 1아웃 추가 주루사 | 2루 주자 홈 아웃: 아웃 2, 포수 자살 | REDUCER |
| 2아웃 추가 주루사 | 홈 아웃 = 제3아웃, 득점 0, 타자주자 1루 잔루 LOB 1, 안타 기록 유지 (`Reducer_two_outs_...`) | REDUCER |
| 타자 아웃 + 추가 주루사 | 뜬공 아웃 + 3루 주자 홈 아웃: 아웃 2(0아웃에서) / 3(1아웃에서, 이닝 종료). 3루 주자 득점 시 이벤트 `Result`는 `Out` 그대로이고 Reducer가 `BatterResult.SacrificeFly`를 파생(AB 소멸, RBI 1) (`Reducer_a_fly_out_...`) | REDUCER |
| 복수 주자의 추가 주루사 | 만루 단타에서 2루 주자 홈 아웃 + 1루 주자 3루 아웃: 0아웃→아웃 2, 1아웃→아웃 3·이닝 종료. 3루 주자 득점은 제3아웃(비포스) 이전이라 **인정**(득점 1, 무효 0) | REDUCER |
| 3아웃 초과 방지 | 2아웃 두 주자 모두 아웃 → `Game.too_many_outs`. 정책은 제3아웃 뒤의 결정을 Void로 둔다(`Harness_when_the_play_is_over...`) | REDUCER + HARNESS |
| 동일 주자 중복 이동/아웃 | 같은 주자의 이동 두 개 → `Game.runner_moved_twice` | REDUCER |
| 득점이 제3아웃보다 **먼저** | `Lead` 순서: 득점 인정(득점 1, 무효 0) | REDUCER |
| 득점이 제3아웃보다 **늦게** | `Trail` 순서(같은 사실): 득점 **무효**(득점 0, 무효 1), LOB에 무효 득점 주자 포함. 암묵 순서는 `Game.play_order_required`로 거부 | REDUCER |
| 제3아웃이 **포스 아웃** | 만루 2아웃 야수선택에서 1루 주자 2루 포스 아웃(`Sequence 1`)과 3루 주자 득점(`Sequence 2`): 득점 무효 — 순서와 무관 (`Reducer_a_force_out_or_a_batter_runner_out...`) | REDUCER |
| 제3아웃이 **비포스 태그** | 위 "2루 주자 홈 아웃" 사례 | REDUCER |
| 제3아웃이 **타자주자 1루 도달 전 아웃** | 만루 2아웃 땅볼 아웃, 3루 주자 득점을 먼저 배열해도 득점 무효 | REDUCER |
| 득점 인정이 순서·종류에 따라 달라지는 경우 | 합법 벡터 618개가 제3아웃으로 끝나고 그중 **318개가 인정되는 득점을 포함**. 후행 우선 사건 순서에서는 **318/318이 무효**, 암묵 순서에서는 318/318이 거부 | HARNESS + REDUCER |

구분(리뷰 수정 — [결정 초안 P-05 §5.1](PHASE_3C2B3_POLICY_DECISION_DRAFT.md)의 네 범주): ① **실제 야구 규칙 주장** = 임시 OBR 5.08(a) 인용만(KBO 2026 미인증), ② **현 `GameReducer`가 구현한 검증 규칙** = 비포스 태그/포스/타자주자 아웃 판정, 명시 `Sequence` 필수, 어필 플레이 미지원(`Game.appeal_not_supported`), ③ **게임 프로토타입이 임의로 정의한 사건 순서** = 선행 우선 규약(결정적 가정, 실제 관측 순서가 아님; **소유자 승인 전**), ④ **향후 시간 플레이 시뮬레이션이 필요한 영역** = 송구·주루 시간에 따른 득점/아웃 선후. 위 318개 벡터의 득점 결과는 ③ 가정의 함수이며 실제 경기의 정답이 아니다. 승인된 4개 맥락에는 포스 아웃과 타자주자 아웃이 생기지 않는다(C2B-2의 제3아웃 플레이는 모든 주자를 정지시킨다, U-6). 위 두 항목은 Reducer 계약의 기록용이다.

## 9. 핵심 검증 D — 끝내기

8개 필수 사례를 손으로 유도한 값으로 실제 Reducer에 적용했다.

| # | 사례 | 결과 |
|---|---|---|
| 1 | 9회말 동점, 2루 주자, 단타 → 2루 주자 추가 진루 득점 | 경기 종료 `HomeWin`, 득점 1, RBI 1, 단타 유지, 타자주자 1루 잔루 LOB 1. 기본 계획 `WinningRunner = null` → **재사용 불가, 재계산 필요** |
| 2 | 동점, 1루 주자, 2루타 → 1루 주자 홈 | 끝내기, 득점 1. 결승 득점자 = 1루 주자(시작 베이스 1). 인정 루수 `min(2, 4−1)=2` → 2루타 유지 |
| 3 | 1점 뒤짐, 2·3루, 단타 → 3루 주자가 먼저 득점(동점), 2루 주자 추가 진루 득점 | 득점 2, RBI 2, 결승 득점자 = 2루 주자(두 번째 득점). Hold/Out이면 경기 계속(득점 1, 동점) |
| 4 | 2점 뒤짐, 만루, 2루타 → 3·2루 주자 득점(2점), 1루 주자 추가 진루 득점 | 득점 3, RBI 3, 결승 득점자 = 1루 주자, 2루타 유지. 1루 주자 Hold면 경기 계속 |
| 5 | 2아웃에서 주루사와 결승 득점이 함께 | 1점 뒤짐·2/3루·2아웃 단타: 2루 주자 Extra → 끝내기(득점 2). Out이면: 3루 주자 득점이 먼저(`Lead`)면 인정되어 동점·연장 계속, 같은 사실을 `Trail` 순서로 쓰면 득점 무효 → 원정 승리(0:1)로 경기 종료. **같은 하나의 사실이 순서에 따라 경기 결과를 뒤집는다** |
| 6 | 추가 진루 실패로 끝내기 불발 | 동점 2루 단타 Hold/Out, 1루 2루타 Hold/Out, 뜬공 3루 주자 Hold/Out: 경기 계속 (`Reducer_walkoff_6_...`) |
| 7 | 추가 진루로 공식 인정 루수가 달라지는 경우 | 합법 끝내기 벡터 240개에서 **0건**(`Static_walkoff_7_...`). 4개 맥락에서는 승자가 항상 1·2루 주자이고, `min(drawn bases, 4−시작 베이스)`가 drawn 루수와 같다. 미래 맥락이 승자를 3루 주자로 만들 수 있으면 추가 진루 뒤에 `walkoff-credit-v1`을 다시 적용해야 한다 |
| 8 | 타자주자 또는 기존 주자가 결승 득점자 | 240개 전부 **기존 주자**, 타자주자 0건 (`Static_walkoff_8_...`). `InPlayWinningRunner(Kind=ExistingRunner)` 재사용 가능 |

재계산이 필요한 항목(근거 `Reducer_advancement_changes_the_official_record...`): 공식 `BatterResult`(이벤트 `Out` 뜬공 → Reducer 파생 `SacrificeFly`: AB 0, SF, RBI), 타점, 득점(최종 `ScoredPlay.Runs`), 아웃 수, LOB(무효 득점 주자 포함), 경기·이닝 종료 상태, 결승 득점자(기본 계획에는 없음), 수비 기록(추가 아웃의 자살·보살). 새 통계 키는 필요 없다(주루사는 `RunCS`/`RunPicked`가 아니라 `ScoredPlay.Moves`와 수비 PO/A로 기록). 기존 `inplay-mapping-v1`·`walkoff-credit-v1`·`inplay-template-v1`과 **충돌하지 않는다**: 기본 계획은 불변이며 추가 진루는 그 위에 얹힌다.

## 10. 핵심 검증 E — RNG와 재현성

위치: H0(투구 추첨 전) → C2A 추첨 → H1 = `Draft.NextRng` → C2B-2(소비 0) → H1 → 추가 진루(향후 C2B-3) → H2 → C3.

| 확인 항목 | 결과 | 범주 |
|---|---|---|
| C2B-2의 H1 보존 | 실제 `PreparedPitch`에서 `PendingAdvancement.RngAfterPitchDraw == Draft.NextRng`, H0와 다름, `game/runner@1` 스트림은 C2A·C2B-2가 쓰지 않음 | REDUCER 측 프로덕션 + HARNESS |
| 정책별 필요한 호출 수 | 고정 소비: 정확히 K(맥락 수)회. 생략: 1–2회(평균 1.306). 재추첨(추가 진루만 제외): 1–9회(평균 1.565). 거절 표집으로 raw 출력이 호출 수보다 많을 수 있다(약 0.0225 %/롤, 측정 12,000회 중 3회). 호출 수는 고정이어도 `Draws`는 가변 → 감사는 호출 수가 아니라 스냅샷 해시로 | HARNESS |
| 복수 주자 처리 순서에 따른 소비 차이 | 고정 소비에서는 순서와 무관하게 K. 순서는 어느 롤이 어느 주자의 것인지만 바꾼다 | HARNESS |
| 동일 입력·시드 재현 | 12개 정책 × 3개 대표 사례 × 50시드 동일 | HARNESS |
| 불가능한 행동 시 추가 RNG | 고정 소비: 없음(Void도 롤 소비). 생략: 소비 없음(결과 의존). 재추첨: 추가 소비 있음 | HARNESS |
| 재추첨 필요성·위험 | 시도 결합에서는 불필요. 위험: 가변 호출, 상한 필요, H2가 결과에 의존 | HARNESS |
| 실패·취소 시 원본 RNG 불변 | `GameRandom.Atomically`: 실패 결과·취소 후에도 H1 해시 불변, 성공한 범위만 H2 공개. 재시도는 같은 롤 재생(숨은 재추첨 없음) | 프로덕션 기존 기능 (Infrastructure 테스트) |
| 기존 C2A 추첨 불변 | 같은 입력으로 `Prepare` 재실행 → 같은 batted ball·defense result·`NextRng` 해시. 기존 골든 벡터 전부 통과 | 프로덕션 |
| 새 소비 스트림 | 정확히 `game/runner@1` 하나만 추가되고 다른 스트림의 상태·`Draws` 불변 | HARNESS + 프로덕션 |

가상 추첨 결과(테스트 전용 `AdvModel.Resolve`)와 프로덕션 동작(`PreparedPitch`, `GameRandom`, 스트림 ID)의 증거는 위 "범주" 열로 분리했다.

## 11. 핵심 검증 F — 선수 능력 데이터 (`MISSING_INPUT`)

| 정보 | 존재 여부 | 공급 경로 |
|---|---|---|
| 주자의 주루 능력 | `Ratings.Hitter.Speed` 존재(`PlayerSheet`) | **없음.** `GameState`의 `BaseRunner`는 `PlayerId`만 보유. `PlanBase`/`Build`는 타자·투수 시트만 받는다 — `MISSING_INPUT` |
| 수비수 송구 능력 | `Hitter.Arm` 존재 | **없음.** 수비수 `PlayerId`는 `GameState.DefenderAt`로 얻지만 시트 조회 경로가 없다 — `MISSING_INPUT` |
| 피로도·컨디션 | `PlayerState` 존재 | `advancement`에 해당 modifier 슬롯 **없음**(`runner.speed`, `defense.arm`뿐) — `NO_SLOT` |
| 수비 위치·타구 종류 | 템플릿 `Retriever`, `Class` | 있음(C2B-2 내부) |
| 경기 상황 | `GameState` | 있음 |

C2A도 어떤 modifier도 공급하지 않고 모든 단계를 중립으로 추첨한다(`PitchBranchResolver`가 `StageProbability.Compute(tuning, stage, condition)`만 호출; 소스 검사 테스트). 따라서 C2B-3의 첫 구현을 중립으로 시작하는 것은 일관된다. 능력 연결의 최소 계약 변경안은 [인수인계 초안](PHASE_3C2B3_IMPLEMENTATION_HANDOFF_DRAFT.md) §3. 이번 단계에서 프로덕션 필드는 추가하지 않았다. 효과 크기 한계: `runner.speed`, `defense.arm` 모두 기울기 2000 ppm/단위, 범위 0.9–1.1(최대 ±10 %).

## 12. 기존 C2B-2 기준선 유지 확인

| C2B-2 계약 | 확인 |
|---|---|
| 새 RNG 소비 없음 | C2B-2 소스 스캔 테스트 기존 통과. 이번 작업은 `src/` 불변 |
| Pending은 이벤트·ID·영수증·채택 가능 RNG 없음 | 기존 `InPlayVerifyAndIsolationTests` 전부 통과 |
| H1 보존 | 위 §10 |
| GameState·EventId 커서 불변 | 기존 테스트 + 이번 `Reducer_building_and_applying_every_vector_leaves_the_state...` |
| 5,400건 오라클 비교 | 기존 `InPlayOracleComparisonTests` 통과(전체 xUnit) |

## 13. 실행한 테스트

| 실행 | 결과 |
|---|---|
| 신규 Simulation 테스트 `--filter FullyQualifiedName~AdvancementPolicy` | 60/60 통과 |
| 신규 Infrastructure 테스트 `--filter FullyQualifiedName~AdvancementPolicyHandoffTests` | 4/4 통과 ([건별 로그](PHASE_3C2B3P_NEW_TESTS_raw.txt)) |
| `dotnet test StarPlayer.sln -c Release` | **1,451 통과, 0 실패**(Domain 263, Application 117, Simulation 597, Infrastructure 474). 기준 1,387 + 신규 64 ([로그](PHASE_3C2B3P_FULL_XUNIT_raw.txt)) |
| 변이 검사(테스트 전용 하네스·판정기·이벤트 빌더에 결함 주입) | 최종 9/9 검출. 첫 라운드에서 **2건 생존**(M2: 등가 변이, M6: 리졸버 결과의 합법성을 직접 검사하는 테스트 부재 = 실제 공백)과 2건 빌드 불가 → 테스트 보강 후 재실행 ([기록](PHASE_3C2B3P_MUTATION_raw.txt)) |
| `tools/verify.ps1` 연속 2회, `GATE`, 링크 검사, `git diff --check` | §14 |

새 테스트가 처음부터 모두 통과한 이유: 검증 대상이 이미 존재하는 프로덕션 Reducer이고, 새 코드는 테스트 전용 모델·판정기뿐이어서 "먼저 실패하는" 프로덕션 변경이 없다. 그래서 RED 증거 대신 변이 검사로 테스트의 민감도를 확인했다.

## 14. `verify.ps1`과 링크 검사

`tools/verify.ps1 -LogName …`를 최종 트리에서 **연속 2회** 실행했고 둘 다 `GATE: PASS`(종료 코드 0)였다(Claude Code 실행; 기존 Phase 2 로그는 덮어쓰지 않음). 로그: [run1 표](PHASE_3C2B3P_VERIFY_LOG_run1.md), [run2 표](PHASE_3C2B3P_VERIFY_LOG_run2.md), 표준출력 [run1](PHASE_3C2B3P_VERIFY_STDOUT_run1.txt), [run2](PHASE_3C2B3P_VERIFY_STDOUT_run2.txt). 테스트 합계: Domain 263, Simulation 597, Application 117, Architecture 14, 결정성 감지기 48, Infrastructure 474 모두 실패 0. Markdown 상대 링크 검사 110개 파일 통과. `git diff --check`는 출력 없음(종료 코드 0). verify가 추적 파일을 바꾸지 않았다(`git status`의 추적 변경은 의도한 문서 3건뿐). 이후 이 §14 문장만 추가했고 링크 대상은 모두 존재한다.

## 15. 문제·위험 분류

| ID | 등급 | 내용 |
|---|---|---|
| R-1 | **High (설계 위험, 결함 아님)** | Reducer는 야구상 불가능한 플레이를 480/840건 수락한다(점유된 베이스에서의 아웃 270, 제3아웃/결승 득점 이후 이동 210). C2B-3가 "Reducer 수락 = 합법"을 가정하면 불법 플레이가 확정된다. 정책 계층 규칙(P-02/P-04)과 독립 판정 테스트가 필수 |
| R-2 | **High (결정 필요, `OWNER_APPROVAL_REQUIRED`)** | 득점+제3아웃 공존 시 사건 순서가 득점 인정 여부와 경기 결과를 뒤집는다(318/318, 사례 D-5). 순서를 승인 없이 구현하면 안 된다(P-05). 소유자 승인은 확인되지 않았고 기록하지 않았다 (이 문장은 최초 제출 당시 기록이다. **FIX2 정정: 2026-10-12 소유자가 P-05를 승인했다 — §19**) |
| R-3 | Medium | 기본 계획의 `WinningRunner`/공식 `BatterResult`/타점/AB는 추가 진루 뒤 달라진다(SF 120벡터, 승자 240벡터). 기본 계획 값을 재사용하면 오기록(P-06) |
| R-4 | Medium | 선수 능력 공급 경로 없음(`MISSING_INPUT`). 첫 구현은 중립으로 시작해야 하며, 능력 연결은 별도 소유자 결정(P-08) |
| R-5 | Medium | 주자 간 상관(선행 주자가 아웃되면 후행 주자 진루 쉬움)이 모델에 없다. 2결정 사례에서 후행 주자 Extra가 명목 27 %→평균 12.9 %로 낮아진다. 승인된 모델이 상호작용을 선언하지 않으므로(U-1) 위반은 아니나 3-F 보정 항목 |
| R-6 | Low | F-1의 "복합 864건"은 상호작용 범위를 과대 표시(실제 270건) — 문서 표현 정밀화 권고 |
| R-7 | Low | 거절 표집으로 raw `Draws`가 호출 수보다 클 수 있어 감사에 호출 수만 쓰면 불충분 |
| R-8 | Low | 독립 판정기와 정책 모델이 같은 작성자(검증 독립성 한계, F-7과 동일). Python 오라클 같은 독립 구현은 만들지 않았다 |
| R-9 | Low | KBO 2026 인증 미완료(임시 OBR 근거), 3-F 보정 `미실시`, 타자주자 추가 진루·병살·야수선택 없음(승인된 단순화) |

Critical 없음.

## 16. 한계 (이 작업이 아닌 것)

실제 추가 진루 엔진·`game/runner@1` 추첨 구현 없음. GameEvent 확정·영속화 없음(C3). 새 확률값 없음(승인된 4개 `advancement` 행을 그대로 읽었다). 독립 리뷰(GPT Chat, Codex) `미실시`. 위 모든 정책은 `PROPOSED`다.

## 17. 재현 명령

```powershell
dotnet test StarPlayer.sln -c Release
dotnet test tests\StarPlayer.Simulation.Tests -c Release --filter "FullyQualifiedName~AdvancementPolicy"
dotnet test tests\StarPlayer.Infrastructure.Tests -c Release --filter "FullyQualifiedName~AdvancementPolicyHandoffTests"
$env:STAR_ADV_OUT = "docs\reports"; dotnet test tests\StarPlayer.Simulation.Tests -c Release --filter "FullyQualifiedName~AdvancementPolicyEvidence"
```

마지막 명령이 [`PHASE_3C2B3P_CASE_VECTORS.csv`](PHASE_3C2B3P_CASE_VECTORS.csv)와 [`PHASE_3C2B3P_POLICY_SUMMARY.json`](PHASE_3C2B3P_POLICY_SUMMARY.json)을 다시 만든다.

## 18. Codex 리뷰 수정 (FIX-1 입력 계약 / FIX-2 P-05 정의) — 2026-10-12, 소유자 `TASK.md`

기준 HEAD 변경 없음(`f669589`). Codex Medium 2건에 대한 **문서·테스트 설계 보완**이다. `src/`(프로덕션)는 변경하지 않았고, `GameReducer`·확률·RNG·튜닝·기존 오라클/골든은 건드리지 않았다. 스테이징·커밋·푸시 안 함, Codex 자동 실행 안 함. P-05 소유자 승인은 확인되지 않았고 **승인으로 기록하지 않았다.** 자체 판정은 §18.8.

### 18.1 FIX-1 — Resolver 입력·최신 상태 검증 계약

| 항목 | 수정 전 | 수정 후 ([인수인계 §6](PHASE_3C2B3_IMPLEMENTATION_HANDOFF_DRAFT.md)) |
|---|---|---|
| 개념 서명 | `Resolve(PendingAdvancement, TuningProfile, CancellationToken)` | `Resolve(pending, state, rng(H0), tuning, batter, pitcher, lastIssuedEventSequence, cancellation)`; 타입·순서는 `InPlayPitchPlanning.Build`/`CheckPrepared`의 실제 서명에 맞춰 구현 시 확정(별도 최소 입력 API 금지) |
| 최신 상태 검증 | 없음 | 첫 단계로 기존 `PitchPreparation.CheckPrepared(pending?.Plan.Prepared, …)`를 코드·우선순위 불변으로 재사용 |
| 기존 `CheckPrepared` 재사용 가능성 | 미기술 | **가능.** `PendingAdvancement.Plan.Prepared`가 실제 `PreparedPitch`를 보관하고 현재 H0 스냅샷이 곧 `rng` 인수이므로 서명 변경 없이 호출된다 |
| EventId | "개인 작업 카운터"만 | 유효성 통과 뒤 `GameEventIds.Create`/`Next`/영수증 충돌 검사(`Build`와 같은 코드): `Id.sequence_invalid`, `Id.sequence_exhausted`, `InPlayCandidate.event_id_in_use`. 현재 `Build`는 Pending에서 커서를 보지 않는다는 사실을 테스트로 고정 |

**H0/H1/H2 경계:** H0 = 호출자의 `rng` = 권위 위치, `StartRngStateHash`와 대조(`rng_state_changed`). H1 = `Pending.RngAfterPitchDraw`는 호출자의 `rng`와 **직접 비교하지 않고**(권위 RNG가 H0인 한 H1은 미채택 위치) `Restore(H1, hash)`한 개인 사본에서 후속 추첨을 시작. H2 = 성공한 `Atomically`의 결과로만 `NextRng`. 호출자가 H1을 현재 RNG로 넘기면 거부된다(테스트됨). 한계: 영수증 목록의 최대 순번보다 큰 커서(번호 건너뜀)는 이 계층에서 감지할 수 없고 C3가 막는다(문서에 명시). 확률 모델 버전 변경(`probability_model_changed`)은 기존 테스트가 없어 구현 시 위조 `PreparedPitch`로 확인해야 한다(미확인으로 표기).

### 18.2 오래된 Pending 재사용 반례 (현재 확인 / 구현 시 확인)

전체 표는 [인수인계 §6.5](PHASE_3C2B3_IMPLEMENTATION_HANDOFF_DRAFT.md). **현재 확인**(실제 composite `PendingAdvancement`의 `Plan.Prepared`를 기존 `PlanBase`/`Build`에 넣어 실행): 같은 입력 수락·같은 H1, `state_stale`(GameState 교체), `game_mismatch`, `rng_context_mismatch`, `rng_state_changed`(H0 변경 / H1을 현재 RNG로 전달), `PitchPrepare.batter_mismatch`/`pitcher_mismatch`, `rules_changed`, `tuning_changed`, `prepared_missing`, 모든 거부 뒤 입력 불변, Pending 빌드가 EventId 커서를 무시함. **구현 시 확인**(존재하지 않는 `Resolve`가 필요하므로 이번에 작성·보고하지 않음): EventId 충돌·범위·한도, 취소 단계별 무부작용, 후보·H2 재현성, `probability_model_changed`.

### 18.3 FIX-2 — P-05 사건 순서 정책 (`OWNER_APPROVAL_REQUIRED`)

[결정 초안 P-05](PHASE_3C2B3_POLICY_DECISION_DRAFT.md)를 "Prototype Sequence Policy v1"으로 재정의했다: ① 실제 야구 규칙 주장(임시 OBR, KBO 미인증), ② 현 Reducer 구현 규칙, ③ 프로토타입 임의 사건 순서(S1–S8), ④ 향후 시간 플레이 시뮬레이션 영역을 분리했다. 318개 벡터의 득점 민감도는 유지하되 실제 경기 결과의 정답이 아니라 ③ 가정의 함수로 서술했다. 대안 A/B/C/D와 승인 방법을 기록했다. **소유자 승인: 미확인 → `OWNER_APPROVAL_REQUIRED` 유지.** (최초 제출 당시 기록; FIX2에서 2026-10-12 승인으로 정정 — §19.1) "기술적 준비 완료"와 "소유자 승인"을 구분해 적었다. ADR-007·game-engine·SYSTEM_CONTRACTS는 갱신하지 않았다.

### 18.4 연계 정책 문구

P-03: 분포 동등성을 현 4개 맥락의 점유 충돌(Extra·Out 동시 차단)에 한정하고 Hold 귀속 = 재정규화의 일반 주장을 삭제. P-06: 추첨 → 합법성·Sequence → 최종 공식 결과 → 최종 이벤트 → `Apply` → 최종 `ScoredPlay` 검증·감사의 6단계, 적용 뒤 이벤트 수정 금지, `SacrificeFly`도 이벤트 전 결정. (**FIX2-2 정정:** 이 문장은 틀렸다. `SacrificeFly`는 `InPlayResult` 값이 아니라 Reducer가 파생하는 `BatterResult`이며, 이벤트 전에 완성할 것은 그 파생에 필요한 최종 이동·`Sequence`·`Out` 결과다 — §19.2.) P-07: H1 시작, 선언 맥락별 1회, Void 맥락도 소비, 재추첨 금지, 호출 수 vs raw `Draws`, H1/H2 재현성, 확정은 C3. P-09: 감사 최소 항목(처리 순서·원본 추첨·적용 결과·원본 행동과 최종 Sequence·H0/H1/H2 해시·정책 버전·후보 검증 성공/실패). P-10: 최신 입력 재검증이 후보 생성에 선행.

### 18.5 신규·수정 테스트와 실행 결과

신규 3건(`tests/StarPlayer.Infrastructure.Tests/AdvancementPolicyHandoffTests.cs`에 추가; 헬퍼 `FirstAndSecond`에 규칙·GameId 선택 인수만 추가): `A_real_pendings_prepared_pitch_is_accepted_again_…`, `A_real_pendings_prepared_pitch_is_refused_by_the_shared_stale_check_for_every_changed_input_…`, `Building_a_pending_does_not_look_at_the_event_id_cursor_…`. 모두 **이미 존재하는 프로덕션 동작**의 문서화·고정이므로 처음부터 통과했다(RED 증거 없음; 이 테스트는 새 프로덕션 기능을 검증하지 않는다). 변이 검사는 이번 보완에서 새로 실행하지 않았다(`미검증`).

| 실행 (이번 보완에서 직접 실행) | 결과 |
|---|---|
| `dotnet test tests\StarPlayer.Infrastructure.Tests -c Release --filter "FullyQualifiedName~AdvancementPolicyHandoffTests"` | 7/7 통과(기존 4 + 신규 3) — [로그](PHASE_3C2B3P_FIX_NEW_TESTS_raw.txt) |
| `dotnet test StarPlayer.sln -c Release` | **1,454 통과, 0 실패**(Domain 263, Application 117, Simulation 597, Infrastructure 477) = 이전 1,451 + 신규 3 — [로그](PHASE_3C2B3P_FIX_FULL_XUNIT_raw.txt) |
| `tools/verify.ps1` 연속 2회(Claude Code 실행, 최종 문서 트리 직전) | 둘 다 `GATE: PASS`(종료 코드 0), Markdown 상대 링크 112개 파일 통과, 테스트 합계 Domain 263 / Simulation 597 / Application 117 / Architecture 14 / 결정성 48 / Infrastructure 477 실패 0 — [run1 표](PHASE_3C2B3P_FIX_VERIFY_LOG_run1.md), [run2 표](PHASE_3C2B3P_FIX_VERIFY_LOG_run2.md), [run1 출력](PHASE_3C2B3P_FIX_VERIFY_STDOUT_run1.txt), [run2 출력](PHASE_3C2B3P_FIX_VERIFY_STDOUT_run2.txt). 이후 이 표 행만 추가했다 |
| `git diff --check` | 출력 없음(종료 코드 0; `Star/00_Dashboard.md`의 LF→CRLF 경고만 기존 파일 속성) |

### 18.6 이전 검증 결과의 보존

아래는 **이전 제출 결과의 인용**이며 이번에 다시 실행해 얻은 값이 아니다(검증 로직·`src/` 불변이라 원시 증거를 보존했다): 보류 1,314건, 원시 벡터 5,562, 합법 4,722, 불법 840(Reducer 수락 480), 제3아웃·득점 공존 618 중 인정 득점 318, 변이 검사 9/9, 이전 `verify.ps1` 연속 2회 PASS(§14). 이전 증거 파일(`PHASE_3C2B3P_*_raw.txt`, `*_VERIFY_*`, CSV, JSON)은 덮어쓰지 않았고 이번 로그는 `PHASE_3C2B3P_FIX_*`로 별도 저장했다. 전체 xUnit 1,451은 이전 인용, **1,454는 이번 직접 실행**이다.

### 18.7 남은 차단 사항

1. **(FIX 당시 기록 — FIX2에서 해소: P-05는 2026-10-12 소유자 승인, §19.1)** **P-05 소유자 승인(차단):** 승인 전에는 득점+제3아웃 공존 플레이를 구현할 수 없다(승인 선택지 A/B/C/D, 결정 초안 §5.4).
2. P-01~P-04, P-06~P-10도 `PROPOSED`(소유자 승인 전), P-08 능력 연결 범위는 별도 결정.
3. 3-C2B-3 구현, 3-C3, Phase 4 미착수. 구현 시 확인 항목(§18.2)은 미검증. Phase 3 Gate **INCOMPLETE**.
4. GPT Chat·Codex 재검토 `미실시`.

### 18.8 자체 판정

`FIX_READY_FOR_REVIEW` — **정책 최종 승인이 아니다.** 두 Medium의 문서 보완과 테스트 설계 구분이 끝나 Codex 집중 재검토를 받을 수 있는 상태(재검토 자체는 `미실시`, 자동 실행하지 않음)이며, P-05 소유자 승인 부재가 구현 착수의 차단 항목으로 남는다.

## 19. FIX2 — P-05 승인 기록과 P-06 이벤트 계약 정정

날짜 2026-10-12 · 소유자 `TASK.md` · 기준 HEAD 변경 없음(`f669589`). 재검토 Codex가 Medium 2건을 지적했고, **문서 계약과 승인 기록만** 최소 수정했다. `src/`(`GameEvents.cs`·`InPlayResult`·`GameReducer` 포함), 확률·RNG·튜닝, 기존 테스트·오라클·골든·원시 증거는 변경하지 않았다. 스테이징·커밋·푸시·Codex 재실행 안 함. FIX-1(Resolver 최신 입력 검증)과 P-05의 기술적 순서 정책은 Codex가 적합하다고 판단했으므로 재설계하지 않았다.

### 19.1 처리 이력 (순서대로, 이전 판정 보존)

| 순서 | 사건 | 상태 |
|---|---|---|
| 1 | 최초 Codex 리뷰 | `CHANGES REQUIRED`(Medium 2: 후보 입력·최신 상태 검증 계약 누락 / P-05 의미·승인) — 삭제하지 않음 |
| 2 | FIX-1 기술 계약 보완 | 완료(인수인계 §6, §18). 승인 아님 |
| 3 | **P-05 소유자 승인 (2026-10-12)** | **`OWNER APPROVED`** — `Prototype Sequence Policy v1`, 기존 네 개 맥락 한정, 선행 주자 우선·명시 `Sequence`, 실제 관측 순서·KBO 인증 아님, 시간 기반 모델로 교체 가능 |
| 4 | 재검토 Codex | 문서 상태 오류(승인 미반영) + P-06 이벤트 타입 오류(`InPlayResult.SacrificeFly`) 지적 |
| 5 | **FIX2-1** | P-05 승인 기록 정정 (결정 초안·인수인계 초안·이 보고서·`CURRENT_STATE.md`·Phase 03 리뷰·대시보드) |
| 6 | **FIX2-2** | P-06을 "이벤트 입력 `InPlayResult` vs Reducer 파생 `BatterResult`"로 정정 |
| 7 | C2B-3-P 전체 기술 최종 판정 | **아직 대기**(Codex 재검토 `미실시`, 자동 실행 안 함) |
| 8 | C2B-3 프로덕션 구현 / C3 / Phase 4 | **미승인·미착수**, Phase 3 Gate **INCOMPLETE** |

정책별 상태(**FIX2 시점 기록 — §20 최종 승인으로 대체**): P-05 `OWNER APPROVED`, P-01~P-04·P-06~P-10 `PROPOSED`, C2B-3-P 전체 최종 승인 전. 소유자 승인과 Codex의 기술적 PASS는 별개이며 P-05 승인은 다른 정책이나 구현 착수의 승인이 아니다. ADR-007·game-engine·SYSTEM_CONTRACTS는 승인 전 범위를 벗어나므로 갱신하지 않았다.

### 19.2 FIX2-2 수정 전·후 계약

| | 수정 전 (틀림) | 수정 후 |
|---|---|---|
| 흐름 | 추가 진루 → "최종 공식 `InPlayResult` 결정"(뜬공 `Out` → `SacrificeFly`) → 이벤트 | 추가 진루 → 최종 이동·`Sequence` 확정 → **실제 `InPlayResult`(뜬공 아웃은 `Out`)로 이벤트** → `Apply` → Reducer가 파생한 `BatterResult`를 대조 |
| 희생플라이 | 이벤트 `Result`로 요구 | 이벤트에는 없음. Reducer가 `ScoredPlay.Result = BatterResult.SacrificeFly`로 파생 |
| 감사 | 단일 "공식 결과" | 이벤트 입력(`InPlayResult`)과 Reducer 파생 기록(`BatterResult`·타수·타점·득점·아웃·LOB·결승 득점자)을 별도 필드로 기록 |

코드 근거(이번 세션에서 직접 확인):

- `src/StarPlayer.Domain/Baseball/Game/GameEvents.cs:26` — `public enum InPlayResult { Single, Double, Triple, HomeRun, Out, FieldersChoice, ReachedOnError }` (**`SacrificeFly` 없음**).
- `src/StarPlayer.Domain/Baseball/Game/GameRecords.cs:9–12` — `BatterResult`에 `SacrificeFly`, `SacrificeBunt` 존재.
- `src/StarPlayer.Simulation/Baseball/GameReducer.Plays.cs:362–385` — `InPlayResult → BatterResult` 매핑(`Out`은 기본 `BatterResult.Out`) 후, `e.Result == Out`일 때 희생번트(`e.Sacrifice`) 또는 **희생플라이**(`e.Batted == Fly && outsOnPlay == 1 && s.Outs < OutsPerHalfInning - 1 && cleanRuns > 0`)로 `BatterResult`를 바꾼다. 따라서 SF에 필요한 타구 유형·최종 주자 이동·사건 순서는 이벤트 생성 전에 완성돼야 하고, `Apply` 뒤에는 이벤트를 바꾸지 않는다.
- 기존 테스트 `Reducer_a_fly_out_with_a_runner_out_at_home_counts_both_outs_and_a_runner_scoring_makes_it_a_sacrifice_fly`(`AdvancementPolicyTests.cs:529`)가 `Out`(아웃 2, `BatterResult.Out`)·`SacrificeFly`(득점 시, RBI 1)·1아웃에서 제3아웃(`Out`)을 실제 Reducer로 고정한다.

최종 `ScoredPlay` 검증 순서: ① C2B-2 기본 계획 ② 추가 진루·주루사 결정 ③ P-05 `Sequence` ④ 불가능 플레이 차단(P-02 R1·R2) ⑤ 실제 `InPlayResult`로 `InPlayEvent` ⑥ 원 상태에 `GameReducer.Apply` ⑦ 파생 `BatterResult`·타수·타점·득점·아웃·LOB·결승 득점자를 기대값과 대조(희생플라이 요건 충족 시 `BatterResult.SacrificeFly` 기대) ⑧ 감사에 이벤트 입력과 Reducer 결과를 구분 기록. 필수 사례 5건의 표는 [정책 결정 초안 P-06](PHASE_3C2B3_POLICY_DECISION_DRAFT.md)과 [인수인계 §6.7](PHASE_3C2B3_IMPLEMENTATION_HANDOFF_DRAFT.md)에 있다.

### 19.3 불변 확인

Resolver 입력 `Resolve(pending, state, rng(H0), tuning, batter, pitcher, lastIssuedEventSequence, cancellation)`, `CheckPrepared` 재사용·우선순위, H0/H1/H2 경계, EventId 개인 작업용 ID·영수증 충돌 검사·권위 순번 재검사는 C3, P-03 Hold 귀속 범위, P-07 고정 논리 추첨·Void 소비, P-08 중립 능력(제안 상태), P-09 감사, P-10 Pending/완성 후보 분리는 모두 바꾸지 않았다.

### 19.4 이번 FIX2에서 직접 실행한 검증

| 실행 | 결과 |
|---|---|
| `dotnet test tests\StarPlayer.Infrastructure.Tests -c Release --filter "FullyQualifiedName~AdvancementPolicyHandoffTests"` | 7/7 통과 — [로그](PHASE_3C2B3P_FIX2_HANDOFF_TESTS_raw.txt) |
| `dotnet test tests\StarPlayer.Simulation.Tests -c Release --filter "FullyQualifiedName~AdvancementPolicy"` (뜬공·희생플라이·제3아웃/득점 순서 Reducer 테스트 포함) | 60/60 통과 — [로그](PHASE_3C2B3P_FIX2_REDUCER_POLICY_TESTS_raw.txt) |
| `dotnet test StarPlayer.sln -c Release` | **1,454 통과, 0 실패**(Domain 263, Application 117, Simulation 597, Infrastructure 477) = 이전 기준 1,454와 같음(신규 테스트 없음) — [로그](PHASE_3C2B3P_FIX2_FULL_XUNIT_raw.txt) |
| `tools/verify.ps1 -LogName PHASE_3C2B3P_FIX2_VERIFY_LOG_run1.md` (1회, Claude Code 실행; 연속 2회는 요청되지 않아 실시 안 함) | `GATE: PASS`(종료 코드 0), Markdown 상대 링크 114개 파일 통과(앵커 검증은 이 스크립트 범위 밖), 테스트 합계 Domain 263 / Simulation 597 / Application 117 / Architecture 14 / 결정성 48 / Infrastructure 477 실패 0 — [출력](PHASE_3C2B3P_FIX2_VERIFY_STDOUT_run1.txt), [로그](PHASE_3C2B3P_FIX2_VERIFY_LOG_run1.md) |
| `git diff --check` | 아래 최종 확인 참조 |

새 테스트는 추가하지 않았다: 기존 테스트가 이미 `Out`→`SacrificeFly` 파생과 제3아웃/득점 순서를 실제 Reducer로 고정하고 있어 문서 정정에 필요한 증거가 충분하다(부족하지 않음). 이전 라운드의 로그·오라클·골든·CSV·JSON은 덮어쓰지 않았고 이번 로그는 `PHASE_3C2B3P_FIX2_*`로 별도 저장했다. 이전 제출 로그(`*_FIX_*`, `*_VERIFY_*`)를 이번 실행 결과로 인용하지 않는다.

### 19.5 남은 차단 사항

1. (FIX2 시점 기록 — §20에서 해소) P-01~P-04, P-06~P-10 소유자 승인(P-08 능력 연결 범위는 별도 결정) 및 C2B-3-P 전체 최종 승인.
2. Codex·GPT Chat 재검토 `미실시`. 3-C2B-3 구현, 3-C3, Phase 4 미착수. §18.2의 "구현 시 확인" 항목은 미검증. Phase 3 Gate **INCOMPLETE**.

### 19.6 자체 판정

`FIX2_READY_FOR_REVIEW` — 소유자 또는 Codex의 승인이 아니다.

## 20. 최종 정책 승인 반영과 구현 전 계약 정합성 점검 (2026-10-12)

소유자 지시 `TASK.md`, 기준 HEAD `f669589c48f3b7deb72ce828f13d2a186bae2a7a` 불변. 작업 유형: 정책 승인 기록과 문서 계약 정합성 검증. **`src/`(프로덕션)·테스트·확률·RNG·튜닝·기존 Golden/Oracle/CSV/JSON/RAW 증거는 변경하지 않았고** Resolver(3-C2B-3)·C3 구현, 스테이징·커밋·푸시, Codex 실행은 하지 않았다.

### 20.1 승인 상태와 구분

| 항목 | 상태 |
|---|---|
| P-01 ~ P-10 | **전부 `OWNER APPROVED`** — P-05 `Prototype Sequence Policy v1`은 2026-10-12 먼저 별도 승인(보존), P-01~P-04·P-06~P-10은 같은 날 일괄 승인. 사용자가 이 세션 채팅에서 기록 확인 |
| C2B-3-P | 정책 소유자 승인 완료 / 구현 전 문서 정합성 점검 |
| 3-C2B-3 프로덕션 | **`NOT STARTED / NOT AUTHORIZED`**(소유자의 별도 구현 지시 필요) |
| 3-C3 | `NOT STARTED` |
| Phase 3 Gate | **`INCOMPLETE`** |

세 가지를 구분한다: ① 소유자 정책 승인 ② GPT Chat의 FIX2 보고서 기반 `PASS WITH NOTES`(`TASK.md`에 적힌 사용자 전달 보고, 이 세션이 독립 확인하지 않음) ③ Codex 독립 검토 — 최초 `CHANGES REQUIRED`(§1–§18)와 FIX1/FIX2 수정 이력(§18–§19)을 삭제하지 않았고, Codex가 FIX2 이후 독립 PASS를 새로 수행했다는 기록은 없다(`미실시`). 소유자 승인은 어느 쪽의 PASS도 대체하지 않는다.

### 20.2 갱신한 문서

[정책 결정 초안](PHASE_3C2B3_POLICY_DECISION_DRAFT.md)(최종 승인 기록 절·요약표·각 정책 8항), [인수인계 초안](PHASE_3C2B3_IMPLEMENTATION_HANDOFF_DRAFT.md)(상태·§2 `AdvancementInputs`·§3·§9), 이 보고서(헤더·§19 시점 표기·§20), [ADR-007](../decisions/ADR-007.md)("3-C2B-3-P policy approval" 절과 Status 항목), [game-engine §25](../game-engine.md)(승인된 정책 계약 신설), [SYSTEM_CONTRACTS C08](../SYSTEM_CONTRACTS.md)(상태 항목), `CURRENT_STATE.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/00_Dashboard.md`. `ROADMAP.md`는 3-C2B 단계 상태 표시가 없어(검색 0건) 변경하지 않았다. 문서명 변경·승인 이력 삭제 없음.

### 20.3 구현 전 계약 점검 (TASK §4)

| # | 점검 | 결과 |
|---|---|---|
| 1 | 승인 상태가 문서 간 일치 | 일치: 위 문서 모두 P-01~P-10 `OWNER APPROVED`, 구현 `NOT AUTHORIZED`, Gate `INCOMPLETE`. 이전 "P-05만 승인/나머지 `PROPOSED`" 문구는 "당시/시점 기록" 표지를 달아 이력으로 보존 |
| 2 | 존재하지 않는 이벤트 타입 참조 없음 | `InPlayResult.SacrificeFly`·`Result = SacrificeFly`는 전 문서에서 부정문·이력으로만 남음(검색 확인). `GameEvents.cs:26`의 `InPlayResult`는 `Single, Double, Triple, HomeRun, Out, FieldersChoice, ReachedOnError` |
| 3 | Resolver 최신 입력 계약 유지 | `Resolve(pending, state, rng(H0), tuning, batter, pitcher, lastIssuedEventSequence, cancellation)`와 `CheckPrepared` 재사용·코드/우선순위 불변이 결정 초안 P-10·인수인계 §6·ADR-007·game-engine §25에 동일하게 기록됨 |
| 4 | H0/H1/H2 구별 | 구별 유지: H0 = 권위 RNG(Prepare 입력), H1 = Pending 보유(C2A 이후), H2 = 후보 `NextRng`, 채택은 C3 |
| 5 | Hold/Void 적용과 RNG 소비 의미 | 일치: Hold = 점유 충돌로 불가능한 `Extra`/`Out`의 귀속(정책 B), Void = 플레이 종료 후 맥락의 적용 결과, 둘 다 맥락당 고정 논리 1회 소비 |
| 6 | P-05 Sequence가 프로토타입 가정으로 표시 | 표시됨(실제 관측 순서 아님, KBO 인증 아님, 교체 가능) |
| 7 | 이벤트 입력과 파생 기록 구분 | 구분됨(결정 초안 P-06 표, 인수인계 §6.7, game-engine §25, SYSTEM_CONTRACTS C08) |
| 8 | 미확정 후보와 C3 권위 확정의 경계 | 보존: `PendingAdvancement`는 후보가 아님, 후보 `NextRng`/ID는 작업용, 실제 GameState·RNG·EventId 커서 불변, C3가 확정 직전 재검사 |
| 9 | P-08 중립 능력이 튜닝 확률을 바꾸지 않음 | 바꾸지 않음: 새 modifier·`PlayerSheet` 입력·`AdvancementInputs` 없음, `content/tuning/virtual_tuning_v1.json` 불변(git 상 변경 없음) |
| 10 | 이전 검증 증거 불변 | 불변: 기존 `PHASE_3C2B3P_*` 파일 덮어쓰지 않음, 이번 로그는 `PHASE_3C2B3P_APPROVAL_*`로 별도 저장 |

### 20.4 신규 발견 문서·계약 충돌

중대한 충돌은 발견하지 못했다. 정리한 비충돌 불일치: (a) 인수인계 §4의 "game-engine §25 작성 예정" → 이번에 §25를 정책 계약으로 만들었으므로 구현 시에는 같은 절을 구현 결과로 갱신한다고 고침; (b) 인수인계 §2의 `AdvancementInputs` 표기("후속 승인 시")를 P-08 승인(첫 구현에서 만들지 않음)에 맞게 고침; (c) 주자 추월 사전 차단이 P-04 승인 문구에는 있었으나 결정 초안 §6에는 R1·R2만 적혀 있어 사전 검사 항목에 추가(현 4개 맥락에서 추월은 5,562벡터 중 0건, 정책 의미 변경 아님). 소유자 승인 정책과 모순되는 기존 문구는 없었다.

### 20.5 이번에 직접 실행한 검증

| 실행 | 결과 |
|---|---|
| `dotnet test StarPlayer.sln -c Release` | **1,454 통과, 0 실패**(Domain 263, Application 117, Simulation 597, Infrastructure 477) = 기준 1,454와 동일(신규 테스트 없음) — [로그](PHASE_3C2B3P_APPROVAL_FULL_XUNIT_raw.txt) |
| `dotnet test tests\StarPlayer.Infrastructure.Tests -c Release --filter "FullyQualifiedName~AdvancementPolicyHandoffTests"` | 7/7 통과 — [로그](PHASE_3C2B3P_APPROVAL_HANDOFF_TESTS_raw.txt) |
| `tools/verify.ps1 -LogName PHASE_3C2B3P_APPROVAL_VERIFY_LOG_run1.md` (1회, §20 작성 직후 최종 문서 트리) | `GATE: PASS`(종료 코드 0), Markdown 상대 링크 115개 파일 통과, 테스트 합계 Domain 263 / Simulation 597 / Application 117 / Architecture 14 / 결정성 48 / Infrastructure 477 실패 0 — [출력](PHASE_3C2B3P_APPROVAL_VERIFY_STDOUT_run1.txt), [로그](PHASE_3C2B3P_APPROVAL_VERIFY_LOG_run1.md). 이후 이 표 행만 추가 |
| `git diff --check` | 출력 없음, 종료 코드 0 |
| 승인 상태·계약 키워드 정합성 검색(`Grep`) | 20.3 #1·#2 근거 |

정책 전수 검증(5,562벡터 등)은 문서 변경 작업이라 재실행하지 않았고, 그 수치는 이전 제출의 인용이다.

### 20.6 남은 NOTE (비차단)

1. 인수인계 §6.5의 "구현 시 확인" 항목은 `Resolve` 구현 전까지 미검증.
2. Codex의 FIX2 이후 독립 재검토 `미실시`(소유자 판단에 따름).
3. P-05는 프로토타입 가정이며 KBO 2026 인증이 아니다(U-4 유지). 318개 벡터의 득점 결과는 순서 가정의 함수로 남는다.
4. P-03 분포 동등성은 현재 네 개 맥락 한정; 새 맥락은 별도 정책 필요. P-08 중립으로 모든 주자가 같은 확률을 가진다(3-F/별도 계약에서 선수별 보정).
5. 판정기(`AdvJudge`)와 프로덕션 코드의 작성자 독립성 한계(R-8).
