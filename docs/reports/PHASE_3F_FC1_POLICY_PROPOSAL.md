# Phase 3-F F-C1 Policy Proposal

> **이후 상태 (2026-10-17):** 정책 동결은 `97045829d5836ff6dec8b40109c84d016d92c340`로 로컬 Commit됨 → `FC1_POLICY_CONTRACT_FROZEN_COMMITTED`(미푸시). F-C2-A 구현(검토 대기, `PHASE_3F_FC2A_IMPLEMENTATION_REPORT.md`), F-C2-B/C·F-C3 `NOT_STARTED`. 아래 개정 2 문구는 그 시점 기록으로 보존한다.
>
> **문서 상태 (2026-10-17 개정 2): `OWNER_APPROVED` — `FC1_POLICY_CONTRACT_FROZEN_UNCOMMITTED`.** 소유자가 FC-D1~FC-D12 권장 정책을 승인했다. **권위 문서는 [ADR-008](../decisions/ADR-008.md)**이고, 이 문서는 근거·해설용 보고서다. 둘이 다르면 ADR-008이 우선하며, ADR-008이 승인값으로 적지 않은 이 문서의 수치(Seed 범위·상한·블록 크기·셀별 경기 수 등)는 후보로 남는다(`FINAL_EXECUTION_FREEZE_PENDING`). F-C2·F-C3 `NOT_STARTED`. 아래 원래 상태 문구는 승인 이전 기록으로 보존한다.
>
> (승인 이전 기록) **문서 상태: `DRAFT — OWNER APPROVAL REQUIRED`**
> 이 문서는 F-C1 정책 동결을 위한 **제안서**다. 여기의 어떤 정책·수치도 `POLICY_FROZEN` 또는 `OWNER_APPROVED`가 아니다. 공식 Seed Manifest는 활성화되지 않았고, 코드·테스트·Tuning·Model은 변경되지 않았으며, 100,000 PA·100경기·전략 행렬은 실행되지 않았다. **Phase 3-F Gate와 Phase 3 Gate는 `INCOMPLETE`다.**
>
> 상태 표기: `CONFIRMED` = 기존 ROADMAP·ADR·Tuning·커밋된 코드가 이미 정한 사항 · `PROPOSED` = 이 문서의 승인 후보 · `NOT_SPECIFIED` = 어떤 원천에도 근거가 없음.

- 작성: Claude Code, 2026-10-17, 소유자 `TASK.md` "Star Player Phase 3-F F-C1 Policy Freeze Preparation"
- 기준 HEAD: `5492e80eba7d347f7deabe5dbdb98931f92db0f3` (F-A/B 로컬 Commit, Parent `430f42d`, 미푸시)
- 이전 분석: F-A/B Closeout 최종 보고(채팅)의 F-C 정책 검토를 재검증해 사용했다. 코드와 다르거나 부정확했던 부분은 §17에 정정으로 기록했다.
- **개정 1 (2026-10-17, 소유자 `TASK.md` "Phase 3-F F-C1 Final Policy Clarification", 기준 HEAD `5492e80` 불변):** GPT 검토 보완 사항을 반영했다. 새 절 §3.5(손잡이 비율·표준화), §6.7(행렬 비교 규칙), §7.5(Seed/GameId 해석·4개 층), §8.5(최소 표본 검산), §10.1(PZL #12), §12.1(Dirty 정책), §19(최종 승인 요청표)를 추가했다. 기존 분석은 지우지 않았고, 고친 서술에는 "(개정 1)"을 붙였다. 상태는 여전히 `DRAFT — OWNER APPROVAL REQUIRED`다.
- **개정 2 (2026-10-17, 소유자 `TASK.md` "Star Player Phase 3-F F-C1 Policy Contract Freeze", 기준 HEAD `5492e80` 불변):** 소유자 승인 기록을 §20에 추가하고 §0 표의 승인 여부를 갱신했다. 개정 1까지의 분석·수치·대안은 지우거나 바꾸지 않았다. 공식 계약은 [ADR-008](../decisions/ADR-008.md)에 기록했다.

## 0. 소유자 결정 요약

| ID | 정책 | 추천안 | 기존 확정 부분 (`CONFIRMED`) | 승인 여부 |
|---|---|---|---|---|
| FC-D1 | Neutral 정의 | `neutral-mixed-v1` (Rating 50, 우타 전원, 선발 7인 로테이션 3 RHP : 4 LHP, 7경기 블록 단위). F1·F8은 손잡이 층별 집계 후 사전 가중치 3/7·4/7로 **표준화**해 판정(개정 1, §3.5). | Tuning 목표 문구, handedness 계수(same 1,040,000 / opposite 970,000, neutral 키 미생성) | **OWNER_APPROVED** 2026-10-17 (이전: PENDING) |
| FC-D2 | VirtualLeague 분포 | `virtual-league-v1` (평균 50 대칭 분포, 순환 이동 배치, 미러 팀, 타순·선발 로테이션). F2~F5 판정 코호트. | 없음 (분포 근거 `NOT_SPECIFIED`) | **OWNER_APPROVED** 2026-10-17 (이전: PENDING) |
| FC-D3 | Calibration 전략 | `normal-balanced-v1` (Approach Normal, Intent Balanced, Take 없음) | 정책 구현·기본값 (F-A/B Commit) | **OWNER_APPROVED** 2026-10-17 (이전: PENDING (공식 사용 여부만)) |
| FC-D4 | 지배 전략 | 48셀(셀별 독립 실행, 비대응 Welch), 경기 군집 SE, 우월성 IUT·비우월성 Holm·Gate IUT, 4상태 (개정 1, §6.7) | Gate 문구 "no strategy dominates every relevant matchup in the declared test matrix" | **OWNER_APPROVED** 2026-10-17 (이전: PENDING) |
| FC-D5 | 공식 Seed | 계열별 사전 선언 범위 Manifest, Hash 고정, 실행 전 Commit | ROADMAP 3-B "seed sets … fixed before any calibration run" | **OWNER_APPROVED** 2026-10-17 (이전: PENDING (목록·형식)) |
| FC-D6 | 최소 분모 | k = 0.25, DEFF = 2, z = 1.959964, 목표 구간 중앙값 | 판정 로직(PASS/FAIL/INSUFFICIENT_SAMPLE/NOT_EVALUATED) 구현 | **OWNER_APPROVED** 2026-10-17 (이전: PENDING (수치)) |
| FC-D7 | 공식 Profile | prob-model-v4 / tuning-virtual-proto-v4 / player-input-set-v3 / advancement-policy-v2 / gamerules-v2 + 모드 분리 | 위 버전·Hash, `fictional_test` 상태 | **OWNER_APPROVED** 2026-10-17 (이전: PENDING (모드 분리)) |
| FC-D8 | PZL | 12개 절 각각 증거 기반 판정. #12는 12a 경계 보존(Phase 3) / 12b 기능 RNG 비소비(Phase 5·6·28) 분리. 유예는 자동 적용하지 않음 (개정 1, §10.1) | 12개 절 문구 (ROADMAP Acceptance Criteria) | **OWNER_APPROVED** 2026-10-17 (이전: PENDING (PASS 조건, #12 처리)) |
| FC-D9 | 실패·재실행 | 실패 은폐 금지, 전체 재실행, Holdout 별도 | "failing results are reported, not hidden"; 수치 변경 = 새 TuningVersion | **OWNER_APPROVED** 2026-10-17 (이전: PENDING (세부)) |
| FC-D10 | CLI/Replay | official Fail-Closed (12개 항목). Dirty 판정은 정확한 보호 파일 3개 경로만 허용하고 나머지 미추적은 거부 (개정 1, §12.1) | Exit Code 0/1/2/3 의미, `--tuning` 시험용·replay 거부 | **OWNER_APPROVED** 2026-10-17 (이전: PENDING) |
| FC-D11 | 증거 보관 | 원본 `build/simharness/` (Git 밖), 집계·Hash·Manifest만 버전 관리 | `build/` git-ignore, 매니페스트 형식 v1 | **OWNER_APPROVED** 2026-10-17 (이전: PENDING) |
| FC-D12 | 최종 Gate | §15의 15개 조건 모두 (항목별 최종 표는 §19) | Phase 3 Gate 문구, Common Gate G | **OWNER_APPROVED** 2026-10-17 (이전: PENDING) |

## 1. 현재 프로젝트 상태

| 항목 | 상태 | 근거 |
|---|---|---|
| Phase 3-E Framing v3 / Game Calling v4 | 로컬 Commit `131dcac` / `430f42d`, `REVIEW_PENDING` (독립 최종 검토 대기) | `CURRENT_STATE.md`, `git log` |
| Phase 3-F F-A (Harness) · F-B (Metrics) | 로컬 Commit `5492e80` (미푸시), 리뷰 `검토 대기` | `git log -1` |
| F-A/B Closeout 검증 | Release build exit 0·경고 0, Release xUnit **2,528 PASS** / 0 FAIL / 0 SKIP, 프로세스 간 3회 `run_result_hash` 동일·replay 일치 | Closeout 실행 기록 |
| Codex F-01~F-04 | 수정 확인 (구현자 자체 확인). 독립 재검토 `미실시` | 같은 기록 |
| `tools/verify.ps1` 전체 | `미실시` (U5 등록만 정적 확인) | 같은 기록 |
| Phase 3-F F-C | 미착수. 이 문서는 F-C1 승인 준비 초안 | — |
| Phase 3-F Gate / Phase 3 Gate | `INCOMPLETE` | — |

## 2. F-C 기존 확정 계약 (`CONFIRMED`)

### 2.1 ROADMAP 원문

| 원문 위치 | 원문 요지 (영문 그대로 인용) | 의미 |
|---|---|---|
| §6.3 Performance protocol | "Use 100,000 PAs for resolver calibration, 100 complete games for reducer integration" | 두 실행은 목적이 다르다: calibration(통계) / integration(합법·재현·집계) |
| Phase 3 Tests | "100,000 seeded PAs; 100 complete games; … identical-seed canonical replay; all outcome probabilities finite and normalized …" | 규모와 Replay |
| 3-B | "Virtual baseline probabilities, cap values, seed sets and model versions are fixed before any calibration run." | **Seed 집합은 calibration 전에 고정** |
| 3-F | "Keep the 100,000-PA and 100-complete-game Gates and add the Pitch Zone Lite tests below. Statistical and model changes are judged against pre-declared criteria and failing results are reported, not hidden." | 사전 선언 기준, 실패 공개 |
| Acceptance Criteria | "all games terminate legally; repeat runs match event and RNG hashes; aggregate rates fall within predeclared calibration intervals; no strategy dominates every relevant matchup in the declared test matrix; default interaction requests one PA approach rather than every-pitch input, while `Take` resolves exactly one pitch." | 3-F 핵심 Gate 문구 |
| Acceptance Criteria (PZL) | 12개 절 (§13 표에 원문 인용) | PZL Gate |
| §1.14, AGENTS.md | PZL은 실제 위치가 결과 **전에** 뽑힌다, `GameReducer`가 공식 기록 권위, ef1 불변, 실제 KBO ABS 인증 주장 금지 | PZL 불변식 |
| AGENTS.md | BCL/LMV는 경기 RNG를 소비하지 않는다(별도 namespace) | PZL #12 근거 |

**표현 차이 (변경하지 않음):** 3-F 항목은 PZL tests가 "below"(3-F 아래)에 있다고 하지만 실제 12개 절은 Acceptance Criteria 문단에 있다. 이 문서는 Acceptance Criteria 문단을 원문으로 쓴다.

### 2.2 ADR

- **ADR-006 item 12:** 목표(targets)와 시나리오는 "fixed before any run".
- **ADR-006 "For 3-F":** "Replace or confirm the virtual baselines against the declared targets and the 100,000-PA / 100-game Gates; consider a mix-weighted tendency reference; a changed number needs a new `TuningVersion`." → 수치를 바꾸면 새 TuningVersion이 필요하다(`CONFIRMED`). mix-weighted tendency reference는 검토 대상일 뿐이고 결정되지 않았다(`NOT_SPECIFIED`). 현재 값은 `pzl-ref-uniform-v1`.
- **ADR-007 D-4:** PA 드라이버와 다음 행동 선택은 3-F Harness가 소유한다(F-A가 구현).
- **ADR-007 addendum (F-A/B):** Harness 경계(Domain + Simulation만 참조, 난수·확률 계산 없음, `GameStarter.Begin`이 유일한 Production 진입점).

### 2.3 Tuning v4 목표 (실제 JSON 대조)

`content/tuning/virtual_tuning_v4.json` `targets[]`. 모든 status는 `declared_for_3f_not_evaluated`이다. **TASK.md 표의 값과 JSON 값은 8개 모두 일치한다(차이 없음).** 이 값은 가상 Tuning 목표이고 KBO 실측값이 아니다.

| ID | target id | low ppm | high ppm | 원문 description (그대로) | 코호트 표기 |
|---|---|---:|---:|---|---|
| F1 | `pa_strikeout_rate` | 160000 | 260000 | strikeouts per plate appearance, league average, neutral inputs (needs the 3-C plate-appearance loop) | "league average" + "neutral inputs" |
| F2 | `pa_walk_rate` | 60000 | 110000 | walks per plate appearance, league average | "league average" |
| F3 | `pa_hit_by_pitch_rate` | 4000 | 15000 | hit-by-pitch per plate appearance | **없음** (`NOT_SPECIFIED`) |
| F4 | `pa_home_run_rate` | 15000 | 35000 | home runs per plate appearance | **없음** (`NOT_SPECIFIED`) |
| F5 | `babip` | 270000 | 320000 | batting average on balls in play | **없음** (`NOT_SPECIFIED`) |
| F6 | `pitch_zone_rate` | 450000 | 650000 | share of actual pitches located in the nine inner cells, balanced intent, neutral inputs | "balanced intent, neutral inputs" |
| F7 | `pitch_swing_rate` | 400000 | 560000 | share of actual pitches swung at (excluding hit-by-pitch), neutral inputs | "neutral inputs" |
| F8 | `swing_miss_rate` | 180000 | 300000 | share of swings that miss, neutral inputs | "neutral inputs" |

Harness 계산식 (`tools/StarPlayer.SimHarness/MetricsEvaluator.cs`, `CONFIRMED` 구현):

| ID | 분자 | 분모 | 코드의 `RecommendedCohort` |
|---|---|---|---|
| F1 | SO | PA | VirtualLeague |
| F2 | BB | PA | VirtualLeague |
| F3 | HBP | PA | VirtualLeague |
| F4 | HR | PA | VirtualLeague |
| F5 | H − HR | AB − SO − HR + SF | VirtualLeague |
| F6 | 실제 위치가 안쪽 9칸인 투구 | 실제 위치가 확정된 투구 | Neutral |
| F7 | Swing (헛스윙, 파울, 인플레이) | 투구 − HBP 투구 | Neutral |
| F8 | 헛스윙 | Swing | Neutral |

**불일치 C-1:** F1의 원문은 "neutral inputs"를 포함하는데 코드의 추천 코호트는 VirtualLeague다(§17).

### 2.4 버전·Hash (실제 코드·Commit된 Harness 출력에서 확인)

| 항목 | 값 | 출처 |
|---|---|---|
| Probability model | `prob-model-v4` | `ProbabilityModelVersion.V4Text` |
| Tuning | `tuning-virtual-proto-v4`, ContentHash `5011f8acaae4848481db6bc60f636d7594b2a30fcdf594aeab375f0aa98b8fd8` | `TuningV4Tests.GoldenHashV4` |
| 내장 tuning 파일 SHA-256 | `992d7e2487cc53ca0ab2d8f64ce4ec66f76b2430b834d276f47378c2e3d88c1c` | Harness manifest `embedded_tuning_file_sha256` |
| PlayerInputSet | `player-input-set-v3` (`PlayerInputSet.ContentV3`) | `PlayerInputs.cs` |
| Advancement | `advancement-policy-v2` (`AdvancementPolicy.VersionV2`; v1 = `advancement-policy-v1`) | `AdvancementTypes.cs` |
| Rules (gamerules-v2, format 2) | `test-pzl-non-automated` `test-v2` Hash `2d3f7557750c65d920ee79ec0ff57121ae8b8990f13368ae4daa7520492dce2d`; `test-pzl-automated` `test-v2` Hash `7ad9776729b4a71d60af352d620df561d02f699e036fd6f4a4870b82635f4fa7` | `SimHarnessInputPinTests` |
| 내장 rules 파일 SHA-256 | `b016b37bd94e1fcf7594525cfe3d6b590853619b4a9e13de5683a7bfc6bcab45` | Harness manifest |
| Rules 상태 | `fictional_test` (DH, 9이닝, 연장 11회 한도, 무승부 허용, `rule_policy` `fictional-test-v1`, `zone_model` `pzl-zones-v1`). **2026 KBO 규칙이 아님** | `game_rules_profiles_v2.json` note |
| Park | `pf-sim-neutral` / `stadium-sim-neutral` v1, HR·H·XBH 각 1000‰ | `Program.Build` |
| Reference distribution / 정규화 | `pzl-ref-uniform-v1`, `largest-remainder-lowest-index-v1`, `half-up-v1`, 결합 Cap [800,000, 1,250,000] ppm | Tuning v4 |
| Harness 입력 스키마 | `simharness-input-v1`, 매니페스트 `simharness-manifest-v1`, summary `simharness-summary-v1` | `RunModel.cs`, `RunReport.cs` |
| 기본 정책 / 이벤트 예산 | `normal-balanced-v1` / 3,000 (최대 999,999) | `RunModel.cs` |
| GameId | `game:{competition_key}:{cohort_id}-{index:D6}`, 64자 이하, 구성 문자 `[A-Za-z0-9_.-]`. RNG Context Key = GameId | `GameIds.Create`, `GameStarter.Begin` |
| CLI 종료 코드 | 0 완료+지표 계산 성공 / 1 경기 실패·취소·replay 불일치 / 2 입력 오류 / 3 지표 계산 실패 | `Program.ExitCodeOf` |
| 스모크 상한 | `SmokeMaxGames = 10` | `Program.cs` |

### 2.5 엔진의 실제 전략 선택지 (`CONFIRMED`, 코드 확인)

- `ApproachKind { Normal, Patient, Aggressive }` (`GameEvents.cs`). `strategy.approach` 표 키는 `patient` 880,000 / `neutral` 1,000,000 / `aggressive` 1,100,000이고 `batter_decision` 단계에 적용된다. Normal이나 접근 미선택(PA 시작, 명시적 Take)은 `neutral`로 처리된다.
- `GameActionKind.Take`: D1에 따라 실제 투구 1개를 처리하고, PA가 계속되면 결정 지점으로 돌아간다. 한번 접근(`ApproachEvent`)을 고르면 그 PA는 끝날 때까지 `InSequence`다. Take 투구가 인플레이가 되면 `Harness.take_in_play`로 엔진 계약 위반이 된다.
- `PitchIntent { Balanced, Attack, Careful }` (`PitchBranchDraft.cs`). `target_zone` 단계의 baseline 행(`balanced`/`attack`/`careful`)을 고른다. 투구마다 `IGamePolicy.IntentFor`가 정한다.
- `matchup.handedness`: `swing_result` 단계의 `miss` 결과에만 적용된다(`ProbabilityModel.cs`). 타석 방향이 투수 손과 같으면 `same_hand`, 다르면 `opposite_hand`이고, 스위치 타자는 항상 opposite다. **`neutral` 키는 실제 대결에서 생성되지 않는다**(`PitchModifierInputs.cs`).
- 정책은 공개 `GameState`만 읽는다(평가값·숨겨진 성향 읽기 금지는 기존 `EndToEndAndBoundaryTests`가 고정).

## 3. FC-D1 — Neutral 정의 · F1 해석 · F8 손잡이 (`PROPOSED / OWNER_APPROVAL_REQUIRED`)

### 3.1 "중립"의 구분

| 구분 | 의미 | 달성 가능 여부 |
|---|---|---|
| Neutral Rating | 모든 선형 슬롯 입력 = neutral 50 → 계수 1,000,000 | 가능 (`neutral-v1`이 이미 그렇다) |
| Neutral Modifier | 모든 슬롯 계수 = 1,000,000 | **불가능**: handedness는 neutral 키를 만들지 않는다. count_pressure도 볼카운트에 따라 정해진다(입력이 아니라 상황) |
| Neutral Handedness | 계수 1.0 | 대결 단위로는 불가능. **코호트 평균 계수 1.0**은 비율 설계로만 가능하다 |
| Neutral Strategy | Approach Normal(`neutral`), Intent Balanced | 가능 |
| Neutral League Environment | 중립 Park, 중립 tendency, catcher tools 50 | 가능 |

### 3.2 혼합 비율 산술

- 3/7 same + 4/7 opposite: (3 × 1.04 + 4 × 0.97) / 7 = **1.000** (산술 평균).
- 50:50 혼합: (1.04 + 0.97) / 2 = 1.005.
- **주의:** 계수는 `miss` 가중치에 곱해진 뒤 정규화되므로 결과 확률은 계수에 비선형이다. 예시로 baseline `miss` b = 0.25일 때 p(1.04) = 0.257426, p(0.97) = 0.244332이고, 3/7 혼합은 0.249944다. 단계 수준에서 −56 ppm(2차 효과)이 남으며, PA 수준 결과는 더 복잡하다. **3/7은 산술 평균만 1.0으로 만드는 후보이고 결과의 완전한 중립을 보장하지 않는다.**

### 3.3 `neutral-mixed-v1` 명세 후보

| 항목 | 값 |
|---|---|
| 팀 | 2개 미러 팀(`club-sim-a`, `club-sim-h`), ID만 다르고 구조 동일 |
| 타자 | 팀당 라인업 9명 + 벤치 타자 1명. 모든 Hitter 평가값 50, **모두 우타(Right)**. DH 라인업(`neutral-v1`과 같은 포지션 배열) |
| 포수 | 라인업 포수 1명. Catcher tools(ReceivingFraming, Blocking, PopTime, GameCalling) 모두 50 |
| 투수 | 팀당 선발 7명 + 구원 1명(사용 안 함). Pitcher 평가값(Stuff, Control, Velocity, Endurance) 50, Repertoire Fastball 50. **선발 슬롯 0~2 = 우투, 3~6 = 좌투** |
| 로테이션 | 경기 index g(1부터)에서 두 팀 모두 슬롯 r = (g − 1) mod 7 선발. r ≤ 2 → 모든 PA가 same_hand, r ≥ 3 → 모든 PA가 opposite. 경기 수 기준 3/7 **(개정 1: 완전한 7경기 블록 단위로 끝날 때만 성립. PA·투구·스윙 비율은 보장하지 않음, §3.5)** |
| 전략 | `normal-balanced-v1` (FC-D3) |
| 모드 | Automated / NonAutomated를 각각 별도 층(stratum)으로 실행 (FC-D7) |
| 입력 Hash | 기존 `InputsHash` 구성 + 로테이션 규칙 버전(`rotation=sp7-3r4l-v1`). F-C2 구현 필요 |
| 보고 | same_hand 층·opposite_hand 층·전체 혼합 각각의 F1~F8 값과 **실제 손잡이별 PA·Pitch·Swing 분모** (PA 가중 실제 비율은 3/7과 다를 수 있다. opposite 경기에서 출루가 늘어 PA가 조금 더 많아질 수 있음) |

**설계 이유:** 타자를 좌우로 섞으면 실제 손잡이 비율이 타순별 PA 가중에 따라 달라져 설계로 고정할 수 없다. 타자를 전원 우타로 두고 투수 손만 경기 단위로 돌리면 비율이 경기 수로 정확히 정해지고 결정적이다.

**대안:**
- A. `neutral-v1` 그대로 (100% same_hand): 이미 구현돼 있지만 F8·F1에 체계적인 +4% miss 편향이 생긴다. **진단용으로 유지**하는 것을 추천.
- B1. 타자 4R/5L + RHP: 구현은 간단하지만 비율이 PA 가중으로만 정해진다.
- B2. 50:50: 평균 계수가 1.005다.
- C. 테스트 전용 확률 Baseline: Tuning·Production 외 경로가 필요하므로 **금지 사항에 해당해 기각**.

### 3.4 F1 "league average, neutral inputs" 해석

원문은 그대로 보존한다(삭제·수정 없음).

| 안 | 해석 | 판정 코호트 |
|---|---|---|
| A | 중립 능력치 집합 전체 평균 | `neutral-mixed-v1` |
| B | 가상 리그 대표 분포의 평균 | `virtual-league-v1` |
| C | 판정은 A로 하고, B와 손잡이 층별 값을 함께 보고 | 판정 `neutral-mixed-v1`, 진단 `virtual-league-v1` |

**추천: C.** 원문에서 더 구체적인 한정어가 "neutral inputs"다. `virtual-league-v1`은 평균 50으로 만들었으므로 "league average = neutral inputs"라는 읽기와 충돌하지 않는다. 두 코호트의 판정이 다르면 NOTE로 보고하고 Gate는 판정 코호트로만 정한다. **영향:** 코드의 `RecommendedCohort`(F1 = VirtualLeague)와 충돌한다(C-1). 승인되면 F-C2에서 F1 정의를 Neutral로 바꾸는 코드 변경이 필요하다.

**F6~F8 판정 코호트 (추천):** `neutral-mixed-v1`. F6(`actual_location`)은 handedness의 영향을 받지 않는다. F7(`batter_decision`)은 볼카운트 분포를 통해서만 간접적으로 영향을 받는다. F8(`swing_result` miss)은 **직접** 영향을 받는다. `neutral-v1` 값은 진단용으로 보고한다.

### 3.5 손잡이 비율과 F1·F8 판정 방식 (개정 1, `PROPOSED`)

**(1) 비율의 구분.** 3:4 선발 로테이션이 설계로 보장하는 것과 관측으로만 알 수 있는 것을 나눈다.

| # | 비율 | 3/7 보장 여부 | 이유 |
|---:|---|---|---|
| 1 | 경기 수 (same 경기 / 전체) | **실행이 완전한 7경기 블록 단위로 끝날 때만** 정확히 3/7 | §7.3의 원래 정지 규칙(누적 PA 도달 후 현재 경기까지)은 블록 중간에서 끝날 수 있다. 개정 정지 규칙은 §7.5 |
| 2 | 투수-타자 매치업 구성 (서로 다른 쌍의 수) | 구성상 3/7: same 쌍 3 × 9 × 2팀 = 54, opposite 4 × 9 × 2 = 72 | 전원 우타, 교체 없음. 한 경기 안의 모든 PA가 같은 유형 |
| 3 | PA 기준 same/opposite | **보장 안 됨** (관측값) | 경기당 PA는 내생적이다. opposite 경기는 miss 계수 0.97로 삼진이 줄고 출루가 늘어 PA가 많아질 수 있다 |
| 4 | 투구 기준 | **보장 안 됨** (관측값) | PA당 투구 수가 유형마다 다를 수 있다 |
| 5 | 스윙 기준 | **보장 안 됨** (관측값) | 투구당 스윙 비율이 볼카운트 분포에 따라 달라진다. F8의 분모가 스윙이다 |

**(2) 계수 평균 1.0의 한계.** (3 × 1.04 + 4 × 0.97) / 7 = 1.0은 **계수의 산술 평균**일 뿐이다. 계수가 실제로 적용되는 단위는 스윙(`swing_result`의 miss)이다. 스윙 비율이 정확히 3/7이어도 정규화 비선형성(§3.2 예시 −56 ppm)과 PA 단위 누적 효과 때문에 **결과 중립을 보장하지 않는다**.

**(3) 판정 방식 비교.** 한 경기의 모든 PA·투구·스윙이 같은 손잡이 유형이므로 층(S = same, O = opposite)은 경기 속성(경기 index g의 (g − 1) mod 7)으로 정해진다. 기존 Collector의 경기별 counters로 층별 집계를 계산할 수 있고 수집 방식은 바꿀 필요가 없다.

| 방식 | 정의 | 장점 | 단점 |
|---|---|---|---|
| A. 관측 통계 | F = Σ분자 / Σ분모 (전체 경기 합산) | 기존 평가기 그대로, "실현된 리그" 해석 | 실효 가중치가 관측 분모 비율이라 손잡이 효과 자체에 따라 변하는 **내생적** 값이고 사전 선언되지 않음 |
| **B. 사전 가중 표준화** | 층별 비율을 고정 가중치 w_S = 3/7, w_O = 4/7로 결합 | 혼합비가 **사전 고정**되어 실현 PA·스윙 비율과 무관. 계수 평균 1.0의 전제(가중치 3/7)를 지표의 분모 단위에서 그대로 구현 | 층별 최소 분모 필요, 평가기 변경(F-C2) |

**(4) 방식 B의 식.** 가중치는 각 지표의 **자기 분모** 단위에 적용한다.

- **F1 (분모 PA):**
  - p1_S = SO_S / PA_S
  - p1_O = SO_O / PA_O
  - **F1_std = (3/7) · p1_S + (4/7) · p1_O**
- **F8 (분모 Swing):**
  - p8_S = SwStr_S / Sw_S
  - p8_O = SwStr_O / Sw_O
  - **F8_std = (3/7) · p8_S + (4/7) · p8_O**
  - 분모는 PA가 아니라 층별 스윙 수다.
- **정수 ppm (중간 반올림 없이 한 번에 half-up):**
  - ppm = ⌊(2 · 10⁶ · (3 · N_S · D_O + 4 · N_O · D_S) + 7 · D_S · D_O) / (2 · 7 · D_S · D_O)⌋
  - N은 층 분자, D는 층 분모다.
  - checked 64비트 정수로 계산한다. 층 분모가 수십만 이하이면 범위 안이다.
- **진단 분산:** Var = w_S² · p_S(1 − p_S) / D_S + w_O² · p_O(1 − p_O) / D_O (× DEFF). 상태를 바꾸지 않는다.
- **층별 최소 분모:** D_S ≥ ⌈(3/7) · n_min⌉, D_O ≥ ⌈(4/7) · n_min⌉.
  - D_S = w_S · N, D_O = w_O · N이면 Var = p(1 − p) / N이 되므로, 위 조건은 합산 n_min과 같은 정밀도를 준다(층별 p가 같다는 근사).
  - F1: PA_S ≥ 3,497, PA_O ≥ 4,662
  - F8: Sw_S ≥ 2,670, Sw_O ≥ 3,560
  - 둘 중 하나라도 미달이면 `INSUFFICIENT_SAMPLE`.
- **고정 원칙:** 가중치 3/7·4/7과 층 정의는 공식 실행 전에 동결한다. **측정값을 본 뒤 혼합비를 바꾸지 않는다.**

**(5) 추천.**
- F1·F8 공식 판정은 **B(표준화)**로 한다.
- A(관측 합산), 층별 값, 실제 PA·투구·스윙 비율은 모두 진단으로 보고한다.
- F6·F7은 handedness가 그 단계에 직접 작용하지 않으므로(F7은 볼카운트를 통한 간접 영향만) **A(관측)**로 판정하고 층별 값은 진단으로 보고한다. 같은 B 적용은 대안(소유자 선택)이다.
- **대안 B′:** 로테이션 없이 순수 층 두 개(`neutral-v1` = same, `neutral-opp-v1` = opposite)를 각각 실행해 B로 결합한다. 행렬 코호트를 재사용하므로 neutral 층에는 로테이션 구현이 필요 없다. `virtual-league-v1`에는 어차피 로테이션이 필요하다. 층 구성이 바뀌므로(§7.5) 소유자가 선택한다.

**(6) F1 판정 코호트 충돌과 승인 요청.**
- Tuning 원문은 "league average, neutral inputs"이고 삭제·수정하지 않는다.
- Harness 코드의 F1 `RecommendedCohort`는 `VirtualLeague`다(C-1).
- 다음 둘 중 하나를 승인받아야 한다.
  - **(i) 추천:** `neutral-mixed-v1` + 방식 B
  - **(ii)** `virtual-league-v1` + 방식 A (코드 추천 유지)
- (i)이 승인되면 F-C2에서 F1 정의의 코호트를 바꾼다. **승인 전에는 어느 쪽도 공식 판정이 아니다.**

## 4. FC-D2 — `virtual-league-v1` (`PROPOSED`, 손잡이·팀 구성은 소유자 승인 대기)

실제 KBO 선수 분포를 추측하지 않는다. 아래는 **가상 입력 집합 선언**이다.

### 4.1 타자 (팀당 9명)

- 값 집합 v = [35, 40, 45, 50, 50, 50, 55, 60, 65] (평균 50, 50 중심 대칭).
- 능력 k의 순환 이동 s_k: 타자 i(0~8)의 값 = v[(i + s_k) mod 9]. 능력마다 9명 전원이 값을 하나씩 가지므로 **팀 평균은 정확히 50**이다.
- 이동 후보: Contact 0, Power 2, Eye 4, Speed 1, Fielding 3, Arm 5.
- **산술 검토 (Pearson r, 9명 기준):** 이동 차 d=1·8 → 0.250, d=2·7 → −0.214, d=3·6 → −0.357, d=4·5 → −0.179. 타격 3능력(Contact/Power/Eye, 이동 0/2/4)의 최대 |r|는 0.214, 6능력 전체의 최대 |r|는 0.357이다(6개 이동을 어떻게 골라도 최대 0.357). **완전 상관(|r| = 1)은 없다.** 값이 9개뿐이라 상관을 0으로 만들 수는 없다.
- **Latin-square 호환성:** 9×9 순환 Latin 배치는 팀당 9명과 정확히 맞는다. 라인업 포수의 catcher tools는 50으로 고정한다(팀당 포수가 1명이라 분포를 표현할 수 없음).
- **타순 로테이션:** 경기 g에서 타순을 (g − 1) mod 9만큼 순환 이동한다(포지션은 선수에 고정). 9경기 주기마다 각 타자가 모든 타순을 같은 횟수로 맡으므로 PA 가중 평균이 기대값 기준 50이 된다. 실제 PA 가중 평균은 진단으로 보고한다.

### 4.2 투수 (팀당 선발 7명 + 구원 1명)

- 손잡이는 `neutral-mixed-v1`과 같은 3 RHP / 4 LHP 로테이션이다(handedness 평균 계수 1.0을 유지하고 FC-D1과 같은 구조).
- 손잡이와 실력이 섞이지 않도록 손잡이 그룹마다 평균 50이 되게 배치한다.
  - RHP (슬롯 0~2): Stuff (40, 50, 60), Control (50, 60, 40), Velocity (60, 40, 50). 같은 3개 값의 순환 배치라 능력 간 r = −0.5가 불가피하다.
  - LHP (슬롯 3~6): Stuff (35, 45, 55, 65), Control (45, 55, 65, 35), Velocity (55, 65, 35, 45). 인접 이동 r = −0.2.
  - Endurance 50 (피로 입력 미구현: D-ABILITY-1, 모델에 피로 없음). Repertoire Fastball 50.
- 투수 교체: 없음. Harness에 교체 정책이 없고 필요도 없다(DH 규칙상 강제 교체 없음).

### 4.3 팀·홈/원정·구장

- **미러 팀:** 두 팀은 같은 평가값 구조를 쓰고 ID만 다르다. 실력 차가 없으므로 홈/원정을 서로 바꿔 실행할 필요가 없다. 홈 이점은 엔진에 입력이 없으므로 말 공격 여부만 다르다. 대안: 서로 다른 순환 배치 + 홈/원정 교차 쌍. 교차 쌍은 코호트가 2배가 되므로 추천하지 않는다.
- 전체 주기: LCM(7, 9) = 63경기. 공식 실행이 63의 배수가 아니면 마지막 주기가 불완전해지고, 실제 분포를 보고한다.
- 구장: `pf-sim-neutral` 1개. 구장 효과는 기존 3-D 고정 테스트가 담당한다.

### 4.4 승인 대기 항목

타자 손잡이(추천은 전원 우타, 대안은 좌·스위치 혼합), 값 집합, 이동 값, 로테이션 크기(7), 미러 팀 여부.

### 4.5 구현 영향 (F-C2)

현재 `CohortInputs`는 실행당 하나의 고정 `TeamSetup`만 가진다. **경기 index별 TeamSetup(선발·타순 로테이션)을 결정적으로 만드는 기능**이 필요하다. 로스터에 없는 선수의 시트는 `Harness.cohort_sheet_unused`로 거부되므로, 로테이션 선발 7명 전원이 코호트 로스터에 있어야 한다. `InputsHash`는 로테이션 규칙도 포함해야 한다.

## 5. FC-D3 — Calibration 전략 (`PROPOSED`, 기존 구현 기반)

- 공식 F1~F8 측정에는 `normal-balanced-v1`을 쓴다: Approach Normal(표 키 `neutral`), Intent Balanced, Take 없음, 교체 없음. 이 정책은 이미 구현돼 있고 기본값이다(`CONFIRMED`).
- 전략 선택용 RNG는 추가하지 않는다(Harness 난수 금지 계약).
- 결과를 본 뒤 전략을 바꾸지 않는다. 공식 통계 측정(FC-D6)과 전략 우위 측정(FC-D4)은 분리한다.
- 엔진 명칭과 대조한 결과는 §2.5와 같다.

## 6. FC-D4 — 지배 전략 행렬과 판정 (`PROPOSED`)

### 6.1 합법성 검토

| 전략 | 정의 | 합법성 근거 |
|---|---|---|
| Normal / Patient / Aggressive | 매 PA 시작 결정 지점에서 해당 `ApproachKind`를 고른다 | `GameActionKind.Approach`. D1 기본 상호작용 |
| **TakeFirst** | 결정 지점에서 볼카운트가 0-0이면 `Take`, 아니면 Approach Normal | `GameActionKind.Take` 합법(기존 테스트 `An_explicit_take_each_plate_appearance_is_legal…`). Take 투구는 볼·스트라이크·HBP로 끝나고 파울·인플레이는 불가하므로 0-0 결정 지점은 PA당 한 번뿐이다. 공개 상태만 쓰는 순수 함수 |
| Balanced / Attack / Careful | 모든 투구에 해당 `PitchIntent` | `IntentFor`. 합법 |

숨겨진 정보는 쓰지 않는다. 기존 경계 테스트가 정책이 공개 `GameState`만 읽도록 고정한다.

### 6.2 셀 설계

- **셀 = (Batter 전략, Pitcher 전략, Hand, Mode).** 4 × 3 × 2 × 2 = **48셀**.
- 한 셀의 모든 경기에서 **양 팀 모두** 같은 (Batter, Pitcher) 전략을 쓴다. 그러면 매 half-inning이 "Batter 전략 vs Pitcher 전략" 관측 1개가 된다. 이렇게 하면 기존 단일 `IGamePolicy` 인터페이스로 충분하고 공수별 Composite Policy가 필요 없다.
  - (개정 1) 현재 `IGamePolicy`에는 메서드가 두 개 있다: `AtPlateAppearance`(타석 결정: Approach 또는 Take)와 `IntentFor`(투구 의도). Harness는 공격 팀 결정에는 앞의 것을, 수비 팀 투구에는 뒤의 것을 호출한다. 따라서 **타자 전략과 투수 전략은 한 정책 객체 안의 독립 구성요소로 따로 지정할 수 있다**. 다만 정책 객체 하나가 양 팀에 함께 쓰이므로, 팀마다 다른 전략을 주려면 공개 상태(`InningHalf`)로 분기해야 한다. 이 기능은 현재 구현되어 있지 않고, 아래 설계에는 필요 없다. 대칭 적용이 일방적 우위 검정과 어떻게 다른지는 §6.7에 정리했다.
- Hand 조건:
  - same = 기존 `neutral-v1`(전원 우투우타, 평가값 50) 재사용
  - opposite = 신규 `neutral-opp-v1`(타자 우타, 선발·구원 좌투, 평가값 50)
- Mode 조건: `test-pzl-automated` / `test-pzl-non-automated`.
- 평가값을 50으로 둬서 전략 효과만 분리한다. 평가값 차이까지 포함하는 확장 행렬(예: 고/저 타자 × 고/저 투수 = 192셀)은 **대안**이다. 추천하지 않으며, 셀을 늘릴수록 "모든 대결에서 우세"가 성립하기 어려워진다는 점을 소유자가 판단해야 한다.
- **RNG/GameId 결속:** 셀마다 고유한 `competition_key`(`fc3-dom-<hand><mode>-<batter><pitcher>`)를 써서 GameId와 RNG Context가 셀마다 다르다. 셀끼리는 **독립 표본**이다. (개정 1) RNG 독립성은 GameId 차이에서 나온다. Seed 차이에서 나오는 것이 아니다(§7.5).
  - 대안: 공통 GameId(공통 난수)로 짝지은 비교. 첫 전략 차이 이후 Stream이 갈라져 짝짓기 이득이 작고, 같은 GameId가 서로 다른 결과로 나타나 증거 해석을 흐리므로 추천하지 않는다.

### 6.3 측정 단위와 지표

- **주 지표:** 1~8회의 half-inning 득점. 1~8회는 양 팀 모두 항상 끝까지 진행되므로 경기당 16개다. 9회 말 미진행·끝내기·연장으로 생기는 잘림/선택 편향을 피한다.
- 셀 추정량: R̄ = 총 득점 / (16 × 경기 수).
- **표준오차:** 경기를 군집 단위로 쓴다. 경기별 평균(득점/16)의 경기 간 분산으로 SE를 계산하므로 경기 내 half-inning 상관을 반영한다. 결정적으로 계산하며 RNG를 쓰지 않는다.
- 차이:
  - 타자 측 D_B(σ, τ, 열) = R̄(σ) − R̄(τ)
  - 투수 측 D_P(σ, τ, 열) = R̄(τ) − R̄(σ) (실점이 적을수록 좋다)
  - Welch 근사 z.
- 비교 열:
  - 타자 측: 열 = (Pitcher, Hand, Mode) 12개. 열마다 4개 Batter 셀 비교.
  - 투수 측: 열 = (Batter, Hand, Mode) 16개. 열마다 3개 Pitcher 셀 비교.
- **보조 진단 (판정 미사용):** OBP, SO%, BB%, HR%, PA당 투구 수, PA 종료 볼카운트 분포와 볼카운트별 결과율.

### 6.4 판정 상태 (전략 σ별 → 측별 → Gate)

기호: 유의수준 α, 효과 크기 δ(runs per half-inning), witness 집합 W_σ = {(τ, 열)} (타자 측 3 × 12 = 36, 투수 측 2 × 16 = 32).

1. **σ에 대한 DOMINANCE_DETECTED:** 모든 (τ, 열)에서 단측 (1 − α) 하한 LCB(D) > 0. 교집합-합집합 검정(IUT)이라 다중 비교 보정이 필요 없다.
2. **σ에 대한 NOT_DOMINANT_SUPPORTED:** W_σ 안에서 귀무가설 H0: D ≥ δ에 대한 단측 검정(Z = (δ − D̂)/SE)이 Holm 보정으로 하나 이상 기각된다. 즉 "σ가 δ 이상 우세하지 않은 대결이 적어도 하나 있다"는 증거다.
3. 1과 2가 동시에 성립하면(모든 곳에서 유의하게 우세하지만 그 차이가 δ보다 작은 곳이 있음) **1이 우선한다.** 보수적 선택이다. 소유자가 "δ 이상일 때만 지배"라는 실무 기준을 고를 수 있다(승인 항목).
4. **측별 상태:**
   - `DOMINANCE_DETECTED`: 어떤 σ가 1을 만족
   - `NO_DOMINANCE_SUPPORTED`: 모든 σ가 2를 만족하고 1인 σ가 없음
   - `INCONCLUSIVE`: 나머지(검정력 부족 포함)
   - `NOT_EVALUATED`: 정책 미승인, 셀 미실행, 셀 안의 Engine/Metrics 실패
5. **Gate:** 타자·투수 양 측이 모두 `NO_DOMINANCE_SUPPORTED`일 때만 PASS다. 유의하지 않다는 것만으로 PASS 처리하지 않는다. "모든 σ가 지배 전략이 아님"은 σ별 IUT이므로 σ 간 추가 보정은 필요 없다.

### 6.5 수치 후보 (모두 `PROPOSED`, 미승인)

- α = 0.05 (단측), 검정력 1 − β = 0.80.
- δ 후보: 0.03 / **0.05 (추천)** / 0.10 runs per half-inning. 9이닝 환산으로 팀당 약 0.27 / 0.45 / 0.9점이다. 0.05는 경기당 거의 반 점 차이라 게임 디자인상 의미 있는 우위의 하한 후보로 제안한다.
- 셀당 경기 수: n_games = ⌈2 σ_g² (z_{1−α/m} + z_{1−β})² / δ²⌉, m = 36(Holm의 최악 경우 Bonferroni), σ_g = 경기별 평균의 SD.
- **σ_g는 현재 `NOT_SPECIFIED`다.** fc2-dry에서 공식 Seed와 겹치지 않는 Seed로 파일럿 4셀 × 200경기를 실행해 추정하고, 측정 최댓값 × 1.2를 쓰고, 50경기 단위로 올려 **fc3 Seed 파일에 기록한 뒤** 동결한다. 공식 결과를 본 뒤에는 바꾸지 않는다.
- 예시 계산 (half-inning SD σ_h 가정값, DEFF = 경기 내 군집 보정 가정값, m = 36, z = 2.991 + 0.842; **가정값이지 측정값이 아님**):

| δ | 셀당 경기 (σ_h 0.8) | 셀당 경기 (σ_h 1.0) | 셀당 경기 (σ_h 1.2) | 셀당 경기 (σ_h 1.0, DEFF 1.5) | 48셀 총 경기 (σ_h 1.0, DEFF 1.5) |
|---:|---:|---:|---:|---:|---:|
| 0.03 | 1,306 | 2,041 | 2,939 | 3,061 | 146,928 |
| 0.05 | 471 | 735 | 1,058 | 1,102 | 52,896 |
| 0.10 | 118 | 184 | 265 | 276 | 13,248 |

스모크 관찰(10경기 1.58 s, 약 0.158 s/경기, 벤치마크 아님) 기준으로 δ 0.05 예시는 약 2.3시간, δ 0.10 예시는 약 35분이다.

- `INCONCLUSIVE` 처리: Gate는 `INCOMPLETE`로 남고 소유자가 결정한다. 확장 실행은 새 Seed 블록 1회만 허용하고 사전 선언하며, 확장 판정은 α/2로 한다(순차 검정의 α 팽창 방지).

### 6.6 48셀 표

비교 목적: 타자 측 열은 "Batter 4전략 중 이 (Pitcher, Hand, Mode)에서 지배적인 것이 있는가", 투수 측 열은 "Pitcher 3전략 중 이 (Batter, Hand, Mode)에서 지배적인 것이 있는가"를 본다.

모든 셀에 공통으로 적용:
- **단위:** 1~8회 half-inning 득점(경기당 16개), 경기 군집 SE
- **표본:** §6.5 공식에 따른 n_games(셀마다 같음, fc2-dry 후 동결)
- **결정성:** 셀 고유 `competition_key` → 고유 GameId = RNG Context. Seed 범위는 §7.2. replay 시 `config_hash`·`run_result_hash`가 일치해야 한다.

열 표기: B-col `<Pitcher><Hand><Mode>`, P-col `<Batter><Hand><Mode>`. 약어는 N Normal, P Patient, A Aggressive, T TakeFirst / B Balanced, K Attack, C Careful / S same, O opposite / A Automated, N NonAutomated.

| # | Cell ID | Batter | Pitcher | Hand | Mode | Cohort | Rules profile | Policy ID | competition_key | 타자 측 비교 열 | 투수 측 비교 열 |
|---:|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `DOM-SA-NB` | Normal | Balanced | same | Automated | `neutral-v1` | `test-pzl-automated` | `dom-normal-balanced-v1` | `fc3-dom-sa-nb` | B-col BSA | P-col NSA |
| 2 | `DOM-SA-NK` | Normal | Attack | same | Automated | `neutral-v1` | `test-pzl-automated` | `dom-normal-attack-v1` | `fc3-dom-sa-nk` | B-col KSA | P-col NSA |
| 3 | `DOM-SA-NC` | Normal | Careful | same | Automated | `neutral-v1` | `test-pzl-automated` | `dom-normal-careful-v1` | `fc3-dom-sa-nc` | B-col CSA | P-col NSA |
| 4 | `DOM-SA-PB` | Patient | Balanced | same | Automated | `neutral-v1` | `test-pzl-automated` | `dom-patient-balanced-v1` | `fc3-dom-sa-pb` | B-col BSA | P-col PSA |
| 5 | `DOM-SA-PK` | Patient | Attack | same | Automated | `neutral-v1` | `test-pzl-automated` | `dom-patient-attack-v1` | `fc3-dom-sa-pk` | B-col KSA | P-col PSA |
| 6 | `DOM-SA-PC` | Patient | Careful | same | Automated | `neutral-v1` | `test-pzl-automated` | `dom-patient-careful-v1` | `fc3-dom-sa-pc` | B-col CSA | P-col PSA |
| 7 | `DOM-SA-AB` | Aggressive | Balanced | same | Automated | `neutral-v1` | `test-pzl-automated` | `dom-aggressive-balanced-v1` | `fc3-dom-sa-ab` | B-col BSA | P-col ASA |
| 8 | `DOM-SA-AK` | Aggressive | Attack | same | Automated | `neutral-v1` | `test-pzl-automated` | `dom-aggressive-attack-v1` | `fc3-dom-sa-ak` | B-col KSA | P-col ASA |
| 9 | `DOM-SA-AC` | Aggressive | Careful | same | Automated | `neutral-v1` | `test-pzl-automated` | `dom-aggressive-careful-v1` | `fc3-dom-sa-ac` | B-col CSA | P-col ASA |
| 10 | `DOM-SA-TB` | TakeFirst | Balanced | same | Automated | `neutral-v1` | `test-pzl-automated` | `dom-takefirst-balanced-v1` | `fc3-dom-sa-tb` | B-col BSA | P-col TSA |
| 11 | `DOM-SA-TK` | TakeFirst | Attack | same | Automated | `neutral-v1` | `test-pzl-automated` | `dom-takefirst-attack-v1` | `fc3-dom-sa-tk` | B-col KSA | P-col TSA |
| 12 | `DOM-SA-TC` | TakeFirst | Careful | same | Automated | `neutral-v1` | `test-pzl-automated` | `dom-takefirst-careful-v1` | `fc3-dom-sa-tc` | B-col CSA | P-col TSA |
| 13 | `DOM-SN-NB` | Normal | Balanced | same | NonAutomated | `neutral-v1` | `test-pzl-non-automated` | `dom-normal-balanced-v1` | `fc3-dom-sn-nb` | B-col BSN | P-col NSN |
| 14 | `DOM-SN-NK` | Normal | Attack | same | NonAutomated | `neutral-v1` | `test-pzl-non-automated` | `dom-normal-attack-v1` | `fc3-dom-sn-nk` | B-col KSN | P-col NSN |
| 15 | `DOM-SN-NC` | Normal | Careful | same | NonAutomated | `neutral-v1` | `test-pzl-non-automated` | `dom-normal-careful-v1` | `fc3-dom-sn-nc` | B-col CSN | P-col NSN |
| 16 | `DOM-SN-PB` | Patient | Balanced | same | NonAutomated | `neutral-v1` | `test-pzl-non-automated` | `dom-patient-balanced-v1` | `fc3-dom-sn-pb` | B-col BSN | P-col PSN |
| 17 | `DOM-SN-PK` | Patient | Attack | same | NonAutomated | `neutral-v1` | `test-pzl-non-automated` | `dom-patient-attack-v1` | `fc3-dom-sn-pk` | B-col KSN | P-col PSN |
| 18 | `DOM-SN-PC` | Patient | Careful | same | NonAutomated | `neutral-v1` | `test-pzl-non-automated` | `dom-patient-careful-v1` | `fc3-dom-sn-pc` | B-col CSN | P-col PSN |
| 19 | `DOM-SN-AB` | Aggressive | Balanced | same | NonAutomated | `neutral-v1` | `test-pzl-non-automated` | `dom-aggressive-balanced-v1` | `fc3-dom-sn-ab` | B-col BSN | P-col ASN |
| 20 | `DOM-SN-AK` | Aggressive | Attack | same | NonAutomated | `neutral-v1` | `test-pzl-non-automated` | `dom-aggressive-attack-v1` | `fc3-dom-sn-ak` | B-col KSN | P-col ASN |
| 21 | `DOM-SN-AC` | Aggressive | Careful | same | NonAutomated | `neutral-v1` | `test-pzl-non-automated` | `dom-aggressive-careful-v1` | `fc3-dom-sn-ac` | B-col CSN | P-col ASN |
| 22 | `DOM-SN-TB` | TakeFirst | Balanced | same | NonAutomated | `neutral-v1` | `test-pzl-non-automated` | `dom-takefirst-balanced-v1` | `fc3-dom-sn-tb` | B-col BSN | P-col TSN |
| 23 | `DOM-SN-TK` | TakeFirst | Attack | same | NonAutomated | `neutral-v1` | `test-pzl-non-automated` | `dom-takefirst-attack-v1` | `fc3-dom-sn-tk` | B-col KSN | P-col TSN |
| 24 | `DOM-SN-TC` | TakeFirst | Careful | same | NonAutomated | `neutral-v1` | `test-pzl-non-automated` | `dom-takefirst-careful-v1` | `fc3-dom-sn-tc` | B-col CSN | P-col TSN |
| 25 | `DOM-OA-NB` | Normal | Balanced | opposite | Automated | `neutral-opp-v1` | `test-pzl-automated` | `dom-normal-balanced-v1` | `fc3-dom-oa-nb` | B-col BOA | P-col NOA |
| 26 | `DOM-OA-NK` | Normal | Attack | opposite | Automated | `neutral-opp-v1` | `test-pzl-automated` | `dom-normal-attack-v1` | `fc3-dom-oa-nk` | B-col KOA | P-col NOA |
| 27 | `DOM-OA-NC` | Normal | Careful | opposite | Automated | `neutral-opp-v1` | `test-pzl-automated` | `dom-normal-careful-v1` | `fc3-dom-oa-nc` | B-col COA | P-col NOA |
| 28 | `DOM-OA-PB` | Patient | Balanced | opposite | Automated | `neutral-opp-v1` | `test-pzl-automated` | `dom-patient-balanced-v1` | `fc3-dom-oa-pb` | B-col BOA | P-col POA |
| 29 | `DOM-OA-PK` | Patient | Attack | opposite | Automated | `neutral-opp-v1` | `test-pzl-automated` | `dom-patient-attack-v1` | `fc3-dom-oa-pk` | B-col KOA | P-col POA |
| 30 | `DOM-OA-PC` | Patient | Careful | opposite | Automated | `neutral-opp-v1` | `test-pzl-automated` | `dom-patient-careful-v1` | `fc3-dom-oa-pc` | B-col COA | P-col POA |
| 31 | `DOM-OA-AB` | Aggressive | Balanced | opposite | Automated | `neutral-opp-v1` | `test-pzl-automated` | `dom-aggressive-balanced-v1` | `fc3-dom-oa-ab` | B-col BOA | P-col AOA |
| 32 | `DOM-OA-AK` | Aggressive | Attack | opposite | Automated | `neutral-opp-v1` | `test-pzl-automated` | `dom-aggressive-attack-v1` | `fc3-dom-oa-ak` | B-col KOA | P-col AOA |
| 33 | `DOM-OA-AC` | Aggressive | Careful | opposite | Automated | `neutral-opp-v1` | `test-pzl-automated` | `dom-aggressive-careful-v1` | `fc3-dom-oa-ac` | B-col COA | P-col AOA |
| 34 | `DOM-OA-TB` | TakeFirst | Balanced | opposite | Automated | `neutral-opp-v1` | `test-pzl-automated` | `dom-takefirst-balanced-v1` | `fc3-dom-oa-tb` | B-col BOA | P-col TOA |
| 35 | `DOM-OA-TK` | TakeFirst | Attack | opposite | Automated | `neutral-opp-v1` | `test-pzl-automated` | `dom-takefirst-attack-v1` | `fc3-dom-oa-tk` | B-col KOA | P-col TOA |
| 36 | `DOM-OA-TC` | TakeFirst | Careful | opposite | Automated | `neutral-opp-v1` | `test-pzl-automated` | `dom-takefirst-careful-v1` | `fc3-dom-oa-tc` | B-col COA | P-col TOA |
| 37 | `DOM-ON-NB` | Normal | Balanced | opposite | NonAutomated | `neutral-opp-v1` | `test-pzl-non-automated` | `dom-normal-balanced-v1` | `fc3-dom-on-nb` | B-col BON | P-col NON |
| 38 | `DOM-ON-NK` | Normal | Attack | opposite | NonAutomated | `neutral-opp-v1` | `test-pzl-non-automated` | `dom-normal-attack-v1` | `fc3-dom-on-nk` | B-col KON | P-col NON |
| 39 | `DOM-ON-NC` | Normal | Careful | opposite | NonAutomated | `neutral-opp-v1` | `test-pzl-non-automated` | `dom-normal-careful-v1` | `fc3-dom-on-nc` | B-col CON | P-col NON |
| 40 | `DOM-ON-PB` | Patient | Balanced | opposite | NonAutomated | `neutral-opp-v1` | `test-pzl-non-automated` | `dom-patient-balanced-v1` | `fc3-dom-on-pb` | B-col BON | P-col PON |
| 41 | `DOM-ON-PK` | Patient | Attack | opposite | NonAutomated | `neutral-opp-v1` | `test-pzl-non-automated` | `dom-patient-attack-v1` | `fc3-dom-on-pk` | B-col KON | P-col PON |
| 42 | `DOM-ON-PC` | Patient | Careful | opposite | NonAutomated | `neutral-opp-v1` | `test-pzl-non-automated` | `dom-patient-careful-v1` | `fc3-dom-on-pc` | B-col CON | P-col PON |
| 43 | `DOM-ON-AB` | Aggressive | Balanced | opposite | NonAutomated | `neutral-opp-v1` | `test-pzl-non-automated` | `dom-aggressive-balanced-v1` | `fc3-dom-on-ab` | B-col BON | P-col AON |
| 44 | `DOM-ON-AK` | Aggressive | Attack | opposite | NonAutomated | `neutral-opp-v1` | `test-pzl-non-automated` | `dom-aggressive-attack-v1` | `fc3-dom-on-ak` | B-col KON | P-col AON |
| 45 | `DOM-ON-AC` | Aggressive | Careful | opposite | NonAutomated | `neutral-opp-v1` | `test-pzl-non-automated` | `dom-aggressive-careful-v1` | `fc3-dom-on-ac` | B-col CON | P-col AON |
| 46 | `DOM-ON-TB` | TakeFirst | Balanced | opposite | NonAutomated | `neutral-opp-v1` | `test-pzl-non-automated` | `dom-takefirst-balanced-v1` | `fc3-dom-on-tb` | B-col BON | P-col TON |
| 47 | `DOM-ON-TK` | TakeFirst | Attack | opposite | NonAutomated | `neutral-opp-v1` | `test-pzl-non-automated` | `dom-takefirst-attack-v1` | `fc3-dom-on-tk` | B-col KON | P-col TON |
| 48 | `DOM-ON-TC` | TakeFirst | Careful | opposite | NonAutomated | `neutral-opp-v1` | `test-pzl-non-automated` | `dom-takefirst-careful-v1` | `fc3-dom-on-tc` | B-col CON | P-col TON |


### 6.7 비교 규칙 (개정 1, 모든 수치 `PROPOSED`)

**실행 구조.** 48셀 각각을 별도 실행으로 돌린다(고유 `competition_key`와 Seed 파일). 셀 안에서는 양 팀 모두 Batter 전략 B로 타격하고 Pitcher 전략 P로 투구한다.

**대칭 적용과 일방적 우위 검정의 구분.**
- 대칭 셀의 **경기 승패는 정보가 없다.** 양 팀이 같은 전략이므로 기대 승률은 대칭으로 50%다.
- 대신 **half-inning 득점**은 (공격 팀의 B, 수비 팀의 P)에만 의존한다. 다음 가정 A-1이 성립하기 때문이다.
  - **가정 A-1:** 정책은 점수나 이닝에 반응하지 않는다(무상태). 엔진에는 점수·피로 입력이 없다. 베이스·아웃 상태는 매 half-inning 초기화된다. 교체가 없다.
- 그러면 셀 (B, P)의 half-inning 득점은 "B로 타격, P를 상대"하는 보수 u(B, P)를 추정한다. 일방적 이탈 비교는 다음과 같다.
  - 타자: 같은 P에서 u(B_i, P)와 u(B_τ, P)를 비교한다.
  - 투수: 같은 B에서 u(B, P_j)와 u(B, P_k)를 비교한다.
- A-1은 F-C2에서 코드 검토와 테스트로 확인한다(점수 의존 확률 없음, half-inning 간 이월 없음).
- A-1이 깨지면(예: 미래의 점수 의존 AI) 대칭 설계는 무효이고 비대칭 셀(팀별 다른 전략)이 필요하다.

| # | 항목 | 규칙 |
|---:|---|---|
| 1 | 비교 단위 | **경기**가 표본 단위(군집)다. 경기 관측값 = 1~8회 half-inning 16개의 평균 득점. 셀 추정량 R̄ = 경기 관측값의 평균 |
| 2 | Payoff 방향 | 공격: u_B = half-inning당 득점 (클수록 좋음). 수비: u_P = −(half-inning당 실점) (실점이 적을수록 좋음). D_B(σ, τ \| 열) = R̄(σ, 열) − R̄(τ, 열). D_P(σ, τ \| 열) = R̄(τ, 열) − R̄(σ, 열). 둘 다 σ가 τ보다 좋으면 양수 |
| 3 | δ 단위 | **runs per half-inning** (1~8회, 셀 평균의 차이). 경기당 득점이나 승률이 아니다. 0.05는 9이닝 환산 팀당 약 0.45점 |
| 4 | α | 단측 0.05, 검정력 0.80 (`PROPOSED`) |
| 5 | IUT·다중 비교 적용 범위 | (a) σ의 **우월성(지배)**: H0 "어떤 (τ, 열)에서 D ≤ 0". W_σ의 모든 단측 검정이 α에서 기각될 때만 H0를 기각한다. IUT이므로 크기 α, **σ 내부 보정 없음**.<br>(b) σ의 **비우월성(지배 아님)**: H0 "모든 (τ, 열)에서 D ≥ δ". W_σ 안의 witness 검정(Z = (δ − D̂) / SE) 중 하나라도 **Holm(\|W_σ\| = 36 타자 / 32 투수, α)**으로 기각되면 H0를 기각한다. σ별 FWER는 α.<br>(c) **Gate PASS 주장** "모든 σ(타자 4 + 투수 3 = 7개)가 지배 전략이 아님"은 σ 사이의 IUT다. 추가 보정 없이 잘못된 PASS의 크기가 α 이하로 통제된다.<br>(d) **잘못된 지배 검출**(7개 후보의 합집합)은 최악의 경우 7α까지 커질 수 있다. 오류 방향이 Gate FAIL(보수적)이므로 보정하지 않는 것을 추천한다. 대안은 Bonferroni α/7 |
| 6 | Dominance 증거 | σ의 W_σ 모든 쌍에서 단측 (1 − α) 하한 LCB(D) > 0 |
| 7 | Non-Dominance 증거 | W_σ에서 Holm으로 기각된 witness가 하나 이상: 그 (τ, 열)에서 σ의 우위가 δ 미만이라는 증거 |
| 8 | 검정력 부족 | 6도 7도 아니면 `INCONCLUSIVE`. Gate는 PASS가 아니라 `INCOMPLETE`다. 6과 7이 동시에 성립하면 6이 우선한다(§6.4-3). 확장 규칙은 §6.5 |
| 9 | 표본·상관 | 셀끼리 독립이다(GameId가 달라 RNG Context가 다름). 셀 안의 경기끼리도 독립이다(GameId가 다름). 경기 안의 half-inning 상관은 경기 단위 군집으로 흡수한다(SE = 경기 관측값의 표본 표준편차 / √n). **비대응 Welch z**를 쓴다. **대응(paired) 검정은 쓰지 않는다.** 셀마다 GameId가 다르므로 공통 난수가 없고, GameId를 일부러 같게 맞춰도 첫 결정이 달라진 뒤 Stream 소비가 갈라져 대응 상관을 지지할 근거가 없다 |
| 10 | 해석 한계 | 평균 득점만 본다(분산·분포 차이, 예: 고분산 전략은 보지 않음). 점수·이닝 상황 전략은 범위 밖이다(정책 무상태). 승률은 대칭 셀에서 측정할 수 없다. 승률 기준 지배를 보려면 비대칭 셀 설계가 필요하며 제안하지 않는다. 9회 이후는 제외한다. 평가값 50 조건만 본다. 결론은 **선언된 48셀 행렬 안에서만** 유효하다(ROADMAP "declared test matrix") |

볼카운트별 분석(PA 종료 볼카운트 분포, 볼카운트별 결과율)은 진단용이다. 판정에는 쓰지 않는다.
## 7. FC-D5 — 공식 Seed Manifest (`PROPOSED`; Seed 집합 사전 고정 원칙은 `CONFIRMED`)

### 7.1 필수 정책

1. Seed 목록은 사전에 선언한다(ROADMAP 3-B: calibration 전 고정, `CONFIRMED`).
2. GameId는 `game:{competition_key}:{cohort_id}-{index:D6}`로 결정적으로 만든다(`CONFIRMED` 구현).
3. GameId = RNG Context Key (`GameStarter.Begin`, `CONFIRMED`).
4. 실행 순서는 index 오름차순으로 고정한다.
5. Seed 파일의 SHA-256을 실행 Manifest에 기록한다.
6. 실패한 Seed는 교체하지 않는다.
7. 결과가 마음에 들지 않는 Seed를 제외하지 않는다.
8. Manifest 전체를 replay할 수 있어야 한다.
9. 공식 실행 전에 Seed 파일을 Commit한다.
10. 공식 실행 후에는 목록을 변경하지 않는다. 변경이 필요하면 새 파일 버전과 전체 재실행으로 처리한다.

### 7.2 계열·Seed 범위 후보 (활성화 안 함)

테스트·스모크에서 이미 쓴 Seed(1~10, 3, 11, 29, 2026101101~2026101104)는 공식 범위에서 제외한다. (개정 1) 이 제외와 계열별 비중복 범위는 **기록 위생**(이미 본 결과와의 혼동 방지)을 위한 것이다. 통계적 독립은 GameId 차이가 보장한다(§7.5).

| 계열 | 층 / 셀 | competition_key | cohort_id | rules | 첫 Seed | 수 | 정지 규칙 |
|---|---|---|---|---|---:|---:|---|
| fc2-dry | calibration 파일럿 4층 | `fc2-dry-cal-<na/nn/va/vn>` | 해당 층 | 해당 모드 | 900001 | 층당 50 | 전부 실행 |
| fc2-dry | 행렬 파일럿 4셀 (NB, AK, PC, TB; same/Auto 등 사전 지정) | `fc2-dry-dom-<cell>` | 해당 | 해당 | 910001 | 셀당 200 | 전부 실행 |
| fc3-cal | neutral-mixed × Automated | `fc3-cal-na` | `neutral-mixed-v1` | `test-pzl-automated` | 100001 | 2,000 (상한) → (개정 1) 2,002 | 완료 경기 누적 PA ≥ 100,000 → (개정 1) 블록 정지 규칙, §7.5 |
| fc3-cal | neutral-mixed × NonAutomated | `fc3-cal-nn` | `neutral-mixed-v1` | `test-pzl-non-automated` | 110001 | 2,000 → (개정 1) 2,002 | 동일 (개정 1: §7.5) |
| fc3-cal | virtual-league × Automated | `fc3-cal-va` | `virtual-league-v1` | `test-pzl-automated` | 120001 | 2,000 → (개정 1) 2,016 | 동일 (개정 1: §7.5) |
| fc3-cal | virtual-league × NonAutomated | `fc3-cal-vn` | `virtual-league-v1` | `test-pzl-non-automated` | 130001 | 2,000 → (개정 1) 2,016 | 동일 (개정 1: §7.5) |
| fc3-int | Automated | `fc3-int-a` | `virtual-league-v1` | `test-pzl-automated` | 500001 | 100 | 전부 실행 |
| fc3-int | NonAutomated | `fc3-int-n` | `virtual-league-v1` | `test-pzl-non-automated` | 510001 | 100 | 전부 실행 |
| fc3-dom | 셀 #c (1~48) | §6.6 | §6.6 | §6.6 | 600001 + 10000 × (c − 1) | n_games (≤ 10,000) | 전부 실행 |
| fc3-hold | 재보정 후 확인용 예비 (FC-D9) | `fc3-hold-*` | — | — | 700001~ | 미정 | 소유자 승인 시에만 |

GameId 길이는 최장 예시 `game:fc3-cal-va:virtual-league-v1-000001`이 40자로 64자 이내다.

### 7.3 Seed 파일 형식 후보 (`simharness-seed-manifest-v1`)

```json
{
  "seed_manifest_version": "simharness-seed-manifest-v1",
  "family": "fc3-cal",
  "stratum": "neutral-mixed-v1/automated",
  "status": "proposed",
  "competition_key": "fc3-cal-na",
  "cohort_id": "neutral-mixed-v1",
  "rules_profile_id": "test-pzl-automated",
  "advancement_policy_version": "advancement-policy-v2",
  "policy_id": "normal-balanced-v1",
  "event_budget": 3000,
  "seeds": { "kind": "range", "first": 100001, "count": 2000 },
  "stop_rule": { "kind": "cumulative_completed_pa_block", "threshold": 100000, "block_games": 7 }
}
```

- index i(1부터)의 Seed = first + i − 1. 실행 Manifest(`manifest.json`)에는 실제로 실행한 (index, GameId, Seed) 목록이 그대로 남는다(기존 형식).
- (개정 1) 위 JSON의 정지 규칙은 블록 단위 `cumulative_completed_pa_block`으로 바꿨다(§7.5). 원래 규칙은 다음과 같았다. 정지 규칙 `cumulative_completed_pa`: 순서대로 실행하다가 완료 경기의 누적 PA가 임계 이상이 되면 진행 중이던 경기를 끝까지 마치고 멈춘다. 지표 값과 무관하고 결정적이다. 상한(count)을 모두 써도 미달이면 해당 층은 `INSUFFICIENT_SAMPLE`이다. 실패 경기의 PA는 누적하지 않지만 실패 자체는 FC-D9에 따라 Gate FAIL이다.
- 위치 후보: `tools/StarPlayer.SimHarness/manifests/*.json`. 내장 리소스로 넣고 Hash를 코드에 고정한다. 이번 작업에서는 **만들지 않는다**.

### 7.4 모드·코호트 분리 비교

| 안 | 구성 | 총 PA | 추정 경기·시간 (스모크 75 PA/경기, 0.158 s/경기; 추정) | ROADMAP 관계 |
|---|---|---:|---|---|
| A | 전체 합산 100,000 PA (1층) | 100,000 | 약 1,334경기, 약 3.5분 | **ROADMAP 최소 요구** |
| B | Automated / NonAutomated 각 100,000 | 200,000 | 약 2,668경기, 약 7분 | 최소 요구 초과 |
| **C (추천)** | {neutral-mixed, virtual-league} × {Automated, NonAutomated} 각 100,000 | 400,000 | 약 5,336경기, 약 14분 | 최소 요구 초과. **추가 Gate로 소유자 승인 필요** |

C를 추천하는 이유: F1·F6~F8과 F2~F5는 판정 코호트가 다르고(FC-D1/D2), 모드별 결과를 합치면 Framing(NonAutomated 전용) 효과가 숨겨진다. 실행 비용은 몇 분 수준이다. 100경기 integration은 calibration과 별도의 고정 목록(fc3-int)을 쓰고 모드당 100경기다(추천. ROADMAP 최소는 100경기 1세트).


### 7.5 Seed/GameId 해석과 4개 층 범위 (개정 1, `PROPOSED`)

**Seed와 GameId (기존 계약 그대로, 변경 없음).**
- `GameStarter.Begin`은 `GameRandom.Create(masterSeed, contextKey = GameId)`를 호출한다.
- 각 Stream의 초기 상태는 `StreamKeyDerivation.Derive(masterSeed, contextKey, stream)`이다. 즉 (Seed, GameId, Stream ID)의 함수다.
- GameId = `game:{competition_key}:{cohort_id}-{index:D6}`.
- 따라서:
  - **같은 Seed라도 competition_key, cohort_id, index 중 하나가 다르면 다른 난수 경로다.**
  - Seed와 GameId가 모두 같아야 같은 경로다(replay).
- 결과:
  - (a) 셀·층·계열 사이의 독립은 GameId 차이에서 나온다.
  - (b) 같은 Seed 목록을 여러 셀에 써도 공통 난수가 아니다.
  - (c) 공식 계열·층·셀마다 `competition_key`가 고유해야 한다. 같은 (key, cohort, index, Seed)가 두 공식 계열에 나타나면 경로가 중복된다. F-C2 Registry에서 검사한다.
- RNG·GameId 계약은 바꾸지 않는다.

**정지 규칙 개정.** `cumulative_completed_pa_block`: index 순서로 실행해서 완료 경기의 누적 PA가 100,000 이상이 되면 진행 중이던 경기를 끝까지 마치고, **현재 블록이 끝날 때까지** 계속한다.
- 블록 크기: `neutral-mixed-v1`은 7경기, `virtual-league-v1`은 63경기(선발 7 × 타순 9의 최소공배수).
- 상한도 블록의 배수로 둔다: neutral 2,002 = 7 × 286, VL 2,016 = 63 × 32.
- 상한을 모두 써도 미달이면 `INSUFFICIENT_SAMPLE`.

**4개 층.**

| 층 | competition_key | 공식 판정 지표 | 진단 지표 | Seed 범위 (첫 값, 상한) | 경기 완료 기준 | 층별 최소 분모 |
|---|---|---|---|---|---|---|
| L1 Neutral × Automated | `fc3-cal-na` | F1 (B 표준화), F8 (B), F6 (A), F7 (A) | F2~F5, F1·F8의 A 합산, 손잡이 층별 값, 실제 PA·투구·스윙 비율 | 100001, 2,002 | 모든 경기 합법 완료(`Completed`, 합법적 무승부 포함) + 블록 정지 규칙 충족 | F1: PA_S ≥ 3,497 & PA_O ≥ 4,662. F8: Sw_S ≥ 2,670 & Sw_O ≥ 3,560. F6 3,043. F7 4,795 |
| L2 Neutral × NonAutomated | `fc3-cal-nn` | L1과 같음 | L1과 같음 | 110001, 2,002 | L1과 같음 | L1과 같음 |
| L3 VirtualLeague × Automated | `fc3-cal-va` | F2, F3, F4, F5 (A 관측) | F1, F6~F8, 손잡이 층별 값, PA 가중 평가값 평균 | 120001, 2,016 | 모든 경기 합법 완료 + 블록(63) 정지 규칙 | F2 15,297. F3 38,239. F4 29,964. F5 40,906 |
| L4 VirtualLeague × NonAutomated | `fc3-cal-vn` | L3과 같음 | L3과 같음 | 130001, 2,016 | L3과 같음 | L3과 같음 |

- **층 사이 합산 금지:** 판정은 층마다 따로 한다. 분자·분모를 층끼리 더한 값은 진단으로만 쓴다.
- **추가 Gate 여부:** ROADMAP 최소 요구는 **calibration 100,000 PA 1회 + 100 complete games 1회**다(§6.3, Phase 3 Tests).

| 범위 | 층 | 총 PA | ROADMAP 대비 | 비고 |
|---|---|---:|---|---|
| Min-1 | 1층 | 100,000 | **최소 요구 그대로** | 8개 지표 모두 한 코호트로 판정해야 하므로 FC-D1/D2의 코호트 분리와 맞지 않음 |
| Min-2 | L1 + L3 (한 모드) | 200,000 | 초과 (코호트 분리 때문) | 판정 모드 1개(FC-D7 대안) |
| **Full (추천)** | L1~L4 | 400,000 | 초과 (코호트 분리 + 모드 분리) | L2·L4는 **추가 Gate**이므로 소유자 승인 필요 |

- **실패 시 재실행 범위:**
  - 어떤 층에서든 Engine/Metrics 실패가 있으면 그 층은 FAIL이다.
  - 코드를 바꿔 고쳤다면 **새 SHA로 모든 공식 계열(L1~L4, fc3-int, fc3-dom)을 다시 실행**한다. 공식 증거는 하나의 SHA에서 나와야 한다.
  - 코드 변경 없는 환경 문제라면 같은 층 Manifest 전체를 그대로 재실행해 재현을 확인한다.
  - 일부 Seed만 다시 돌리거나 교체하지 않는다.
- **CLI 종료 코드와 Gate:** 층 실행의 Exit 0은 "모든 경기 완료 + 지표 계산 성공"이라는 기술적 성공일 뿐이다. 층 판정은 지표 상태로, Gate는 §15.1로 정한다. Exit 0인데 층이 FAIL·INSUFFICIENT_SAMPLE·NOT_EVALUATED일 수 있다.
- 100경기 integration(fc3-int)은 층이 아니다. 모드당 100경기(`virtual-league-v1`, 63경기 블록과 무관하게 정확히 100경기)이고 지표는 진단만 한다.
## 8. FC-D6 — 최소 분모 (`PROPOSED`, 판정 로직은 `CONFIRMED` 구현)

### 8.1 산식

- n_min = ⌈DEFF × z² × p(1 − p) / (k × h)²⌉
  - p = 목표 구간 중앙값
  - h = 목표 구간 반폭
  - k = 0.25 (95% 반폭 ≤ 목표 반폭의 25%)
  - z = 1.959964 (정확한 0.975 분위수)
  - DEFF = 2
- **의미:** 가설 검정이 아니라 **정밀도 요건**이다. 측정 불확실성이 목표 구간에 비해 충분히 작을 때만 점추정으로 PASS/FAIL을 정한다.
- 중앙값을 쓰는 이유: 판정 기준점이다. p(1 − p)는 0.5에 가까울수록 크다. 구간 안에서 분산이 가장 큰 p("보수")를 쓰면 값이 커진다. 아래 표에 두 값을 모두 제시한다.
- Wilson과 정규근사: 이 n에서는 두 구간이 사실상 같다. 산식은 정규근사이고, Wilson은 기존 구현대로 **진단만** 한다(상태 불변).
- DEFF = 2는 **가정**이다. 경기 내 PA 종속, PA 내 투구 종속(볼카운트), `virtual-league-v1` 선발 로테이션에 따른 경기 간 이질성이 이유다. fc2-dry에서 batch means(경기 20묶음)로 측정 DEFF를 보고하고, 측정 DEFF가 2보다 크면 **자동 변경 없이 중단하고 소유자가 결정**한다.

### 8.2 재계산 값

| ID | 분모 | 중앙값 p | 반폭 h | **n_min (추천 후보)** | 이전 보고 (z = 1.96) | 차이 | 보수 p | 보수 n_min | 하한 기준 기대 사건 수 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| F1 | PA | 0.2100 | 0.0500 | 8,158 | 8,158 | 0 | 0.260 | 9,461 | 1,305 |
| F2 | PA | 0.0850 | 0.0250 | 15,297 | 15,298 | −1 | 0.110 | 19,256 | 918 |
| F3 | PA | 0.0095 | 0.0055 | 38,239 | 38,240 | −1 | 0.015 | 60,042 | 153 |
| F4 | PA | 0.0250 | 0.0100 | 29,964 | 29,966 | −2 | 0.035 | 41,519 | 449 |
| F5 | AB − SO − HR + SF | 0.2950 | 0.0250 | 40,906 | 40,908 | −2 | 0.320 | 42,799 | 11,045 |
| F6 | LocatedPitches | 0.5500 | 0.1000 | 3,043 | 3,044 | −1 | 0.500 | 3,074 | 1,369 |
| F7 | Pitches − HBP 투구 | 0.4800 | 0.0800 | 4,795 | 4,796 | −1 | 0.500 | 4,802 | 1,918 |
| F8 | Swings | 0.2400 | 0.0600 | 6,229 | 6,230 | −1 | 0.300 | 7,171 | 1,121 |

이전 값과의 차이(−2~0)는 z를 1.96으로 반올림했기 때문이다. 공식 기준을 동결할 때 z 표기를 함께 고정해야 한다(승인 항목).

### 8.3 100,000 PA와의 일관성

스모크 비율(BIP/PA 0.72, 투구/PA 2.98, Swing/투구 0.52)로 추정한 층당 분모는 PA 100,000, BIP 약 72,000, 투구 약 298,000, Swing 약 155,000이다. 보수 값을 포함한 모든 후보를 충족한다. 따라서 공식 calibration 층에서 `INSUFFICIENT_SAMPLE`은 사실상 안전장치다. 스모크 비율은 3경기 관찰값이고 `virtual-league-v1`에서는 달라질 수 있다. 100경기 integration(모드당 약 7,500 PA, BIP 약 5,400)은 F3~F5 후보에 못 미친다. 그래서 **통계 판정은 fc3-cal에서만** 하고 fc3-int의 지표는 진단(`NOT_EVALUATED`)으로 둔다.

### 8.4 판정과 집계

- 판정 규칙(구현과 동일):
  - 분모 0 → `INSUFFICIENT_SAMPLE`
  - 승인된 최소 분모 없음 → `NOT_EVALUATED`
  - 최소 미달 → `INSUFFICIENT_SAMPLE`
  - 승인 코호트가 아님 → `NOT_EVALUATED`
  - 그 외 점추정이 [low, high](경계 포함)이면 `PASS`, 벗어나면 `FAIL`
- **집계:** 판정은 층(코호트 × 모드)별로 한다. **층끼리 분모를 합친 판정은 하지 않는다.** 합산값은 진단으로만 쓴다. 최소 분모는 층마다 적용한다.
- 판정 코호트 (추천): F1·F6·F7·F8은 `neutral-mixed-v1`, F2~F5는 `virtual-league-v1`. 나머지 조합은 진단이다. F3~F5의 코호트는 원문에 없으므로(`NOT_SPECIFIED`) F-A/B 지시의 VirtualLeague 추천을 따르고 승인을 받는다.
- 경계 근접: 점추정이 구간 안이지만 Wilson 구간이 경계를 넘으면 `boundary_proximity` NOTE를 단다. 상태는 바뀌지 않는다.
- 표본 부족 시: 상한 Manifest를 다 쓰고도 미달이면 해당 층은 `INSUFFICIENT_SAMPLE`이고 Gate는 미충족이다. 확장은 새 Seed 파일 버전과 전체 재실행으로 하고 소유자가 승인한다.


### 8.5 최소 표본 검산과 전제 (개정 1)

- **산식:** n_min = ⌈ DEFF · z² · p(1 − p) / (k · h)² ⌉
  - k = 0.25, DEFF = 2, z = 1.959964
  - p = (low + high) / 2 / 10⁶ (목표 구간 중앙값)
  - h = (high − low) / 2 / 10⁶ (목표 구간 반폭)
- **반올림 규칙:** 배정밀도로 계산한 실수에 DEFF를 곱한 **뒤** 올림을 **한 번만** 적용한다. 정확한 분위수 1.959963984540054를 써도 올림 결과는 같다.
- **올림 전 실수값:** F1 8,157.415 / F2 15,296.996 / F3 38,238.326 / F4 29,963.379 / F5 40,905.083 / F6 3,042.435 / F7 4,794.141 / F8 6,228.285.
  - 올림 결과는 기존 제안값 8개와 **모두 같다**.
  - F2는 정수와 0.004 차이라 반올림 규칙에 민감하다. 올림 대신 내림을 쓰면 15,296이 되므로 규칙 자체를 동결해야 한다.
- **p 선택:** 목표 구간의 중앙값, 즉 설계 중심을 쓴다. 구간 안 최악 분산(보수 p, §8.2)이 아니다. 보수 값은 대안으로만 남기고 공식 값을 바꾸지 않는다.
- **전제와 한계:**
  - (1) 독립 Bernoulli 시행에 분산 팽창 하나(DEFF)를 곱하는 근사다. **DEFF = 2는 설계상 가정이다.** 경기 내 PA 상관, PA 내 투구 상관, 로테이션에 따른 경기 간 이질성에서 추정되거나 검증된 값이 **아니다**.
  - (2) 정규근사다. 하한 기준 기대 사건 수가 153개 이상이라(F3) 적절하다고 본다.
  - (3) **정밀도 기준이지 오류율 통제가 아니다.** 전제가 성립하면 95% 반폭은 0.25h 이하다. 그러면 참값이 경계에서 0.25h 이상 안쪽일 때 점추정이 그 쪽 경계 밖으로 나갈 확률은 약 2.5% 이하다. 경계 근처 참값의 오판, 그리고 8지표 × 층의 다중성은 통제하지 않는다.
  - (4) F5의 분모는 PA가 아니라 BIP다.
  - (5) 방식 B를 쓰는 F1·F8은 §3.5의 층별 최소 분모를 적용한다.
- **추가 보수 기준 (대안, 공식 값 대체 아님):**
  - (a) 보수 p(§8.2)
  - (b) fc2-dry batch means로 DEFF를 측정하고, 측정값이 2를 넘으면 **중단하고 소유자에게 보고**(자동 교체 없음)
  - (c) 층별 최소 분모의 상향
- **설계 여유:** 층 규모(100,000 PA, BIP 약 72,000, 스윙 약 155,000, 스모크 비율 추정)는 위 후보와 보수 대안을 모두 넉넉히 넘는다.
## 9. FC-D7 — 공식 실행 Profile (`PROPOSED`, 버전은 `CONFIRMED`)

| 항목 | 추천 | 상태 |
|---|---|---|
| Model / Tuning | `prob-model-v4` / `tuning-virtual-proto-v4` (Hash §2.4) | `CONFIRMED` (Harness가 받아들이는 유일한 모델) |
| PlayerInputSet | `player-input-set-v3` | `CONFIRMED` |
| Advancement | `advancement-policy-v2`만 사용(v1은 기존 replay 테스트가 담당) | `PROPOSED` |
| Rules | `gamerules-v2`의 `test-pzl-automated` / `test-pzl-non-automated` (`fictional_test`). **2026 KBO 공식 규칙이라고 표시하지 않는다.** KBO 2026 RuleProfile subset은 D2에 따라 별도 확정 대상 | `CONFIRMED` (가상) |
| CalledPitchMode | **모드 분리 실행(안 2)**. 대안 1 Automated만 / 3 50:50 혼합 | `PROPOSED` |
| DH · 이닝 · 연장 · 무승부 | 프로필 값 그대로(DH, 9이닝, 연장 11회 한도, 무승부 허용). 합법적 무승부는 완료 경기(`TieAtExtraInningsLimit`) | `CONFIRMED` (프로필) |
| Park | `pf-sim-neutral` v1 | `PROPOSED` |
| 이벤트 예산 | 3,000 (스모크 최대 경기 약 300 이벤트 대비 충분). 초과는 Engine Failure | `PROPOSED` |
| 팀 구성 | FC-D1/D2/D4 코호트 | `PROPOSED` |

**목표 구간과 모드:** Tuning 목표에는 모드 구분이 없다(`NOT_SPECIFIED`). 같은 구간을 두 모드에 각각 적용하는 것은 **ROADMAP 최소 요구를 넘는 추가 Gate**이므로 소유자 승인이 필요하다. 승인하지 않으면 대안은 Automated를 판정 모드로 하고 NonAutomated를 진단으로 두는 것이다. F-A/B의 현재 Harness 기본값(non-automated, neutral-v1, seeds 3/11/29)을 공식 입력으로 자동 승격하지 않는다.

## 10. FC-D8 — PZL 12개 절 (`PROPOSED`; 절 문구는 `CONFIRMED`)

12개 절은 모두 포함하고 삭제·통합하지 않는다. **어떤 절도 F-C3 증거 없이 자동 PASS하지 않는다.**

상태 표현:
- `EXISTING_EVIDENCE`: 기존 테스트가 있고 F-C3 SHA에서 재통과가 필요함
- `ADDITIONAL_CHECK_REQUIRED`: F-C2에서 검사를 추가해야 함
- `DEFERRED`: 대상 기능이 아직 없음

| # | ROADMAP 원문 | 코드 위치 | 기존 테스트 | F-C 추가 | 공식 PASS 조건 | 미구현 관련 | 상태 |
|---:|---|---|---|---|---|---|---|
| 1 | "the same seed reproduces locations, events and RNG state" | `GameStarter.Begin`, `GameExecutor` 결과 Hash(`eventHash` = receipt의 EventId·Kind·FingerprintVersion·Fingerprint, ef2 fingerprint에 zone·side·boundary 포함; `rngHash`) | `ExecutionTests` 동일 입력 Hash, `EndToEndAndBoundaryTests` replay, CLI replay. Closeout 때 프로세스 간 3회 일치 | 공식 Manifest 전체 replay | 모든 fc3 계열에서 replay `config_hash`·`run_result_hash` 일치 | 없음 | `EXISTING_EVIDENCE` + F-C3 실행 |
| 2 | "every actual pitch has a location" | `GameExecutor` `Harness.pitch_location_missing`, `MetricCounters.LocatedPitches` | Harness 실행 테스트 | 공식 실행 집계 대조 | 위치 누락 실패 0, 모든 층에서 LocatedPitches = Pitches | 없음 | `EXISTING_EVIDENCE` + F-C3 집계 |
| 3 | "an in-play pitch is never counted twice" | `MetricCounters`(인플레이 = 투구 1·Swing 1), `BoxScoreCrossCheck` 총 투구 | `Every_event_of_a_finished_game_was_counted_once…`, cross-check 테스트 | 공식 실행 cross-check | 모든 공식 경기 `Harness.crosscheck_mismatch` 0 | 없음 | `EXISTING_EVIDENCE` + F-C3 집계 |
| 4 | "legacy `ef1` golden vectors are byte-identical" | `EventFingerprint.cs` (`ef1`) | `Ef1CorpusPinTests` (Phase 2 100경기 byte 비교 2건) | 없음 | F-C3 SHA에서 전체 xUnit PASS | 없음 | `EXISTING_EVIDENCE` |
| 5 | "new `ef2` events are identified and collision-checked" | `EventFingerprint.VersionOf`, `AuthoritativeGame` | `EventJsonV2Tests.Receipts_of_both_fingerprint_versions…`, `AuthoritativeGameTests.A_bad_fingerprint_version…`, `GameIdentityTests.Different_parts_never_collide` | **공식 실행 전체 EventId 유일성 + 같은 EventId에 다른 fingerprint 0 검사** | 공식 실행 전체 충돌 0, 위치가 있는 이벤트는 모두 `ef2`, 없는 이벤트는 모두 `ef1` | 없음 | `ADDITIONAL_CHECK_REQUIRED` |
| 6 | "legacy and new JSON formats both replay" | Infrastructure `ScriptedGameJson.cs` (format 2 envelope / legacy bare array) | `EventJsonV2Tests` (round trip, replay), `ReplayContractTests` | **Harness는 Infrastructure를 참조할 수 없다(경계).** Infrastructure.Tests에서 사전 선언한 공식 GameId 표본(예: fc3-int 각 모드 앞 10경기)을 ef2 JSON으로 왕복한 뒤 replay | 표본 전부 같은 receipt·최종 상태. legacy 코퍼스 replay 유지 | 없음 | `ADDITIONAL_CHECK_REQUIRED` |
| 7 | "a new-format pitch with a missing location is rejected" | `ScriptedGameJson` v2 reader | `EventJsonV2Tests` 138/147/165/211 | 없음 | F-C3 SHA에서 PASS | 없음 | `EXISTING_EVIDENCE` |
| 8 | "a neutral tendency vector has no effect" | `BatterZoneTendency.cs`, `StageProbability` 5단계 | `PitchBranchTendencyTests` 51/63 | 없음(단계 수준 정확 등식). 공식 코호트는 중립 tendency만 씀 | F-C3 SHA에서 PASS, 공식 코호트 tendency = neutral 확인 | 없음 | `EXISTING_EVIDENCE` |
| 9 | "a non-neutral vector is monotonic and within its cap" | 같은 위치 (swing_result cap 60,000, batted_ball cap 40,000) | `PitchBranchTendencyTests` 79/142 | 경기 수준은 진단(선택) | F-C3 SHA에서 PASS | 성향 성장은 범위 밖 | `EXISTING_EVIDENCE` |
| 10 | "ABS mode gives zero Framing effect" | `GameRules.cs` CalledPitchMode, `PitchModifierInputs`, `CommitEvidence` called_pitch 재계산 | `GameRulesCalledPitchModeTests` 87, `FramingInputTests` 168 | Automated 공식 층에서 framing 계수가 중립(1,000,000)인지 실행 수준 확인 | 테스트 PASS + Automated 층 framing 비중립 0 | 실제 KBO ABS 인증 주장 금지 | `EXISTING_EVIDENCE` + `ADDITIONAL_CHECK_REQUIRED`(실행 수준) |
| 11 | "the legacy `GameRuleSnapshot` hash is unchanged" | `GameRules.cs` ContentHash | `Phase3B2CompatibilityTests` 14/28, `SimHarnessInputPinTests` | 없음 | F-C3 SHA에서 PASS | 없음 | `EXISTING_EVIDENCE` |
| 12 | "gameplay RNG is not consumed by BCL/LMV" | `RngStreamId.cs` (등록 Stream이 모두 `game/*`: pitch, contact, field, runner, location, ai, target) | Application `SimulationDeterminismTests`, `ArchitectureTests`. **BCL/LMV 코드는 없다**(Phase 5/6/28) | 12a 경계 보존: presentation Stream 미등록, Harness 결과 Hash가 presentation과 무관함 확인 | 12a PASS 가능 | **BCL/LMV 미구현** | **12a 경계 보존:** `EXISTING_EVIDENCE` / **12b 기능 RNG 비소비:** `DEFERRED` (기능 미구현 상태에서 PASS할 수 없음. 후보 `NOT_APPLICABLE_IN_PHASE_3`) |

**#12 결정 항목:** Phase 3 최종 Gate에서 12a(경계 보존)만으로 이 절을 충족한 것으로 볼지, 12b를 Phase 5/28 Gate로 넘기고 Phase 3에 유예 항목으로 기록할지는 **소유자 결정**이다. 이 문서는 자동 PASS로 처리하지 않는다.

### 10.1 PZL #12 분리 (개정 1)

| 하위 항목 | 내용 | 지금 검증 가능 여부 | 구현·검증 Phase | PASS 조건 |
|---|---|---|---|---|
| **12a 현재 RNG 경계 보존** | 등록된 경기 RNG Stream이 `game/*`뿐이다(`RngStreamId.cs`: pitch, contact, field, runner, location, ai, target). Simulation은 presentation을 참조하지 않는다(Application `ArchitectureTests`, `SimulationDeterminismTests`). Harness 결과 Hash는 presentation 입력 없이 만들어진다 | **가능** (Phase 3, F-C2에서 "등록 Stream 집합 고정"과 "presentation namespace Stream 없음" 확인을 추가) | Phase 3 | 기존 테스트 + F-C2 추가 검사 PASS, F-C3 SHA에서 재통과 |
| **12b BCL/LMV 실제 기능의 RNG 비소비** | 해설 켬/끔/모든 상세도, LMV 보기/건너뛰기/속도 변경에서 경기·이벤트·RNG Hash가 같아야 한다. presentation 선택 키는 별도 namespace를 쓴다(AGENTS.md BCL·LMV 불변식) | **불가능** (기능 미구현) | BCL: Phase 5·6·28 / LMV-A: Phase 5·28 | 해당 Phase의 기능 테스트 PASS |

**12a의 PASS는 12b의 PASS를 의미하지 않는다.** 현재 표기는 "#12 = 12a `EXISTING_EVIDENCE`(F-C2 보강 후 판정) / 12b `DEFERRED` 후보"다.

| 정책 | 내용 | 영향 |
|---|---|---|
| (i) 유예 인정 (소유자 승인 시에만) | Phase 3 Gate에 "#12: 12a PASS, 12b `DEFERRED_TO_PHASE_5_6_28`"로 기록하고, 12b를 BCL·LMV 구현 Phase Gate의 **필수 조건**으로 넘긴다 | Phase 3 Gate를 닫을 수 있다. 유예 기록과 이관 의무가 남는다 |
| (ii) 유예 불인정 | 12b가 검증될 때까지 Phase 3 Gate는 `INCOMPLETE` | ROADMAP 의존 관계상 Phase 4는 Phase 3에, Phase 5는 Phase 4에 의존한다. 12b는 Phase 5 이후에야 존재할 수 있으므로 **Phase 3 Gate를 사실상 닫을 수 없는 순환**이 생긴다 |

추천은 (i)이다. 하지만 **자동으로 적용하지 않는다**(`PENDING`). 승인 전에는 #12를 PASS로 쓰지 않는다.

## 11. FC-D9 — 실패·재실행 (`PROPOSED`; 실패 공개와 새 TuningVersion 원칙은 `CONFIRMED`)

| 유형 | 정의 | 예 | 공식 처리 |
|---|---|---|---|
| `ENGINE_FAILURE` | 경기가 합법적으로 끝나지 않음 | 엔진 오류 코드, 불법 상태, 비정상 종료, `Harness.event_budget_exceeded`, `Harness.stalled`, `Harness.event_sequence_gap`, `Harness.final_state_invalid`, RNG/Commit 불일치, `Harness.unhandled_exception` | 1건이라도 있으면 해당 Gate FAIL |
| `METRICS_FAILURE` | 집계가 틀림 | `Metrics.target_missing`, `Metrics.counts_inconsistent`, `Harness.crosscheck_mismatch` | 1건이라도 있으면 해당 Gate FAIL (CLI Exit 3 / 1) |
| `STATISTICAL_FAILURE` | 정상 실행, 목표 구간 밖 | 측정 ppm 구간 밖 | FAIL을 그대로 보고 |
| `INSUFFICIENT_SAMPLE` | 실행량 또는 분모 부족 | 상한 Manifest 소진 | Gate 미충족 |
| `NOT_EVALUATED` | 정책 미승인·코호트 없음·미실행 | 현재 모든 지표 | Gate 미충족 |

- 실패한 GameId·Seed·마지막 receipt·오류 코드를 보존한다(기존 `RunFailure`).
- 실패 경기를 뺀 선택적 집계는 하지 않는다. 기존 Harness도 집계에서 빼고 실패로 보고한다.
- 같은 Manifest 재실행은 재현 확인용으로만 한다(같은 실패가 재현돼야 함).
- 코드를 바꾼 뒤에는 **새 Git SHA로 해당 계열 전체를 다시 실행**한다. 실패 Seed만 다시 돌리지 않는다.
- 통계 FAIL은 실제 FAIL이다. 수치를 바꾸면 `tuning-virtual-proto-v5`라는 새 버전 제안으로 분리한다(ADR-006). v4는 바꾸지 않는다.
- **재보정 후 이미 본 Seed에서 좋아졌다는 이유로 최종 PASS하지 않는다.** 재보정 버전의 판정은 새로 사전 동결한 Holdout Manifest(fc3-hold)로 한다. 승인이 필요하다.
- CLI Exit Code와 Gate 판정은 분리한다. Exit 0이어도 Gate는 FAIL일 수 있다.

## 12. FC-D10 — Official CLI / Replay (`PROPOSED`, F-C2 필수 선행)

**확인된 결함 (Closeout 재현):** `run --seedz 1`을 실행하면 미등록 옵션이 무시되고 기본 Seed 3·11·29로 실행되며 Exit 0이 나온다. `Program.Parse`는 `--키 값` 쌍만 확인하고 키 이름은 검사하지 않는다.

| # | 항목 | 분류 | 비고 |
|---:|---|---|---|
| 1 | 미등록 옵션 거부 (run/replay/official 모두, Exit 2) | **필수** | 위 결함 |
| 2 | 중복 옵션 거부 | **필수** | 현재는 마지막 값이 덮어씀 |
| 3 | official에서 `--tuning FILE` 거부 | **필수** | `--tuning`은 개발용으로 유지. replay 거부는 `CONFIRMED` 구현 |
| 4 | 공식 Tuning version/Hash 검증 (§2.4 고정값) | **필수** | |
| 5 | 공식 Seed 파일 Hash 검증 (승인 목록) | **필수** | |
| 6 | 미승인 Cohort ID 거부 | **필수** | Cohort Registry |
| 7 | 미승인 Policy ID 거부 | **필수** | Policy Registry (calibration 1 + 행렬 12) |
| 8 | Dirty Git Tree에서 official 거부 | **필수** | **주의:** 현재 `Program.Git()`은 `git status --porcelain`을 쓰므로 미추적 사용자 보호 파일(TASK.md, PDF 2개) 때문에 항상 dirty가 된다. official 기준은 **추적 파일 변경**(`--untracked-files=no`)으로 정의할 것을 제안한다. **(개정 1: 이 정의는 모든 미추적 파일을 허용하게 되므로 철회한다. 대체안은 §12.1)** |
| 9 | Manifest에 `official: true`와 정책 버전(예: `fc1-policy-v1`) 기록 | **필수** | |
| 10 | 모든 공식 Cohort/Policy replay | **필수** | 현재 replay는 `neutral-v1`과 기본 정책만 지원 |
| 11 | 기술 Exit Code와 통계 Gate 분리 (Gate 판정은 별도 `gate.json`/보고서, Exit Code 의미 불변) | **필수** | |
| 12 | 공식 실행 실패 시 재현 정보 보존 | **필수** (대부분 구현됨) | `RunFailure` |
| — | official 출력 디렉터리는 새 디렉터리만 허용(덮어쓰기 금지) | 권장 | |
| — | official에서만 `SmokeMaxGames` 해제 | **필수** | |
| — | `GameRulesJson` `profiles[n] = null` NRE 수정 | 권장 (F-C 비선행) | Harness 경로는 자체 reader가 거부하므로 공식 입력 경로가 아님 |


### 12.1 Official Mode Dirty 정책 (개정 1, `PROPOSED`)

| 범주 | 감지 (예) | 처리 | 이유 |
|---|---|---|---|
| 추적 파일 변경 (worktree) | `git status --porcelain=v1 --untracked-files=all`의 Y열 변경(M, D, R, C, T, U 등) | **거부** | Commit된 SHA와 다른 코드·데이터 |
| Staging 변경 | X열 변경 / `git diff --cached --quiet` 실패 | **거부** | 같은 이유 |
| 허용 보호 미추적 파일 | 정확한 경로 3개만: `TASK.md`, `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf` (대소문자 일치, 와일드카드 없음) | **허용.** Manifest에 `allowed_untracked` 경로를 기록한다. 내용 Hash는 참고로만 기록하고 판정에는 쓰지 않는다(`TASK.md`는 작업마다 바뀜) | 사용자 보호 파일이 공식 실행을 불필요하게 막지 않도록. **공식 입력으로 읽지 않는다.** Harness는 내장 리소스와 승인된 Seed 파일만 읽고, F-C2 테스트로 이를 고정한다 |
| 그 밖의 미추적 파일 | 허용 목록 밖의 `??` | **거부** | SDK 스타일 csproj는 프로젝트 폴더의 모든 `*.cs`를 컴파일하므로 미추적 소스가 빌드에 들어갈 수 있다. 미추적 설정·입력 파일도 결과에 영향을 줄 수 있다. **모든 미추적 파일 허용은 제안하지 않는다** |
| ignored 빌드 출력 | `git status --ignored`의 `!!` 중 승인된 출력 경로(`**/bin/`, `**/obj/`, `build/`, `game/.godot/`) | **허용** (판정 무관). 단, official 절차는 실행 직전에 해당 SHA에서 `dotnet build -c Release`를 실행하고 그 결과를 기록한다 | 이전 빌드 산출물로 실행되는 것을 막는다 |
| 승인 출력 경로 밖의 ignored 파일 | 위 패턴 밖의 `!!` | **거부** | ignore된 소스·설정이 빌드나 입력에 섞이는 것을 막는다 |

허용 목록은 코드에 고정한 승인 목록이다. CLI 옵션으로 늘릴 수 없다.

**실제로 소비되는 입력의 Hash 검증** (불일치하면 Exit 2로 거부):

| 입력 | 검증 |
|---|---|
| Model/Tuning | 내장 파일 SHA-256 `992d7e24…`과 TuningHash `5011f8ac…` (§2.4) |
| Rules | 내장 파일 SHA-256 `b016b37b…`과 프로필별 ContentHash (`2d3f7557…`, `7ad97767…`) |
| Cohort | Registry ID + 코호트별로 승인된 `InputsHash` |
| Policy | Registry ID와 버전. 코드는 기록된 SHA에 결속 |
| Seed 파일 | 승인 목록의 경로와 SHA-256 |
| 기타 | Advancement 버전, Park 스냅샷 값, HEAD SHA 기록 |

현재 `Program.Git()`은 `git status --porcelain`을 허용 목록 없이 쓰기 때문에 이 저장소에서는 항상 dirty로 판정된다(C-4). F-C2에서 바꿀 대상이다.
## 13. FC-D11 — 증거 보관 (`PROPOSED`)

- 원본 실행 결과는 `build/simharness/<run>/`에 둔다(`manifest.json`, `summary.json`, `summary.txt`). 기존 `.gitignore`의 `build/` 규칙을 유지한다(`CONFIRMED`).
- **버전 관리할 증거:**
  - 공식 Seed 파일과 Hash (실행 전 Commit)
  - 정책 버전 ID
  - Model/Tuning/Rules/Cohort/InputSet Hash
  - 층별 F1~F8 분자·분모·ppm·판정·Wilson
  - Engine/Metrics 실패 목록
  - 계열별 `config_hash`·`run_result_hash`와 replay 결과
  - PZL 12개 절 결과
  - 48셀 행렬 결과(셀별 R̄·SE·경기 수와 측별 상태)
  - 이 모든 것을 담은 `docs/reports/PHASE_3F_FC3_REPORT.md`
- 경기별 원본(행렬은 수만 경기)은 Git에 넣지 않는다. Git 밖 보존 기간은 **소유자 승인 항목**이다.
- Phase 4 Save와 겹치는 영속화(SQLite 테이블 등)는 만들지 않는다. 매니페스트는 Save가 아니다(`CONFIRMED`, `docs/data-model.md`).

## 14. F-C2 선행 구현 목록 (승인 후에만; 이번 작업에서 구현 안 함)

| 영역 | 내용 |
|---|---|
| Strict CLI | §12 #1·#2. 모든 명령의 옵션 화이트리스트 |
| Official Mode | `official --seed-manifest FILE` 명령: §12 #3~#9, #11, 출력 디렉터리 규칙 |
| Cohort Registry | `neutral-v1`(기존), `neutral-opp-v1`, `neutral-mixed-v1`, `virtual-league-v1`. 경기 index별 TeamSetup(선발·타순 로테이션), 로테이션 규칙을 포함한 `InputsHash` |
| Policy Registry | `normal-balanced-v1` + 행렬 정책 12개(TakeFirst 포함), 정책 ID로 replay |
| Seed Reader | `simharness-seed-manifest-v1` 읽기, 범위 전개, Hash 검증, `cumulative_completed_pa` 정지 규칙 |
| Evaluation | 승인된 `EvaluationPolicy`(최소 분모, 판정 코호트). F1 `RecommendedCohort` 정정(FC-D1 승인 시). 층별 판정, 손잡이 층 분모 보고, batch means DEFF, `boundary_proximity` |
| 전략 행렬 | 셀 실행기, 1~8회 half-inning 득점 수집, 경기 군집 SE, IUT/Holm 판정, 4상태 |
| PZL 보강 | #5 공식 실행 EventId/fingerprint 유일성, #6 Infrastructure.Tests JSON 왕복 표본, #10 Automated 층 framing 중립 확인, #12a presentation Stream 미등록 확인 |
| 개정 1 추가 | 손잡이 층별 집계와 방식 B 표준화 평가(경기 index 기준 층, 정수 ppm 식), 층별 최소 분모. 블록 정지 규칙 `cumulative_completed_pa_block`. `competition_key` 고유성 검사. Dirty 분류(§12.1)와 입력 Hash 검증. 가정 A-1(half-inning 간 이월 없음) 확인. PZL 12a 등록 Stream 집합 고정 |
| 테스트 | Strict parse·official 거부 경로 Red/Green, 신규 Cohort Hash 결정성·불변성, 로테이션 비율(3/7), TakeFirst 합법성·PA당 Take 1회, Seed Reader 위조 Hash 거부, replay(신규 Cohort/Policy), 판정 로직 고정, fc2-dry 소규모 실행(공식 Seed와 겹치지 않음) |
| 금지 유지 | 엔진·Model·Tuning·RNG·Commit·CAS 변경 없음 |

## 15. F-C3 공식 실행 절차 (승인 후에만)

1. 사전 조건: F-C1 승인 기록, F-C2 Commit, 전체 xUnit PASS, 전체 `tools/verify.ps1` PASS, Seed 파일 Commit, 깨끗한 추적 트리.
2. fc2-dry 결과로 σ_g와 DEFF를 기록하고 셀당 n_games를 확정한 뒤 fc3-dom Seed 파일에 기록·Commit한다(공식 결과를 보기 전).
3. fc3-cal 4층(각 PA ≥ 100,000) → 층별 F1~F8 판정.
4. fc3-int 2모드 × 100경기 → 합법 종료, cross-check, replay.
5. fc3-dom 48셀 → 측별 상태.
6. 모든 계열 replay(같은 SHA, 별도 프로세스) → Hash 일치.
7. PZL 12개 절 증거 정리.
8. `PHASE_3F_FC3_REPORT.md`, Phase 리뷰 `검토 대기`, CURRENT_STATE 갱신 후 중단.

### 15.1 Phase 3-F 최종 Gate 후보 (FC-D12, `PROPOSED`; † 표시는 ROADMAP 원문으로 `CONFIRMED`)

1. 공식 정책 사전 동결 (소유자 승인 기록)
2. 승인된 Seed Manifest 사용
3. 전체 `verify.ps1` PASS (Common Gate G †)
4. 100,000 PA 요구 충족 † (층 구성은 FC-D5)
5. 100경기 요구 충족 †
6. 전 경기 합법 종료 †
7. Engine/Metrics 실패 0
8. BoxScore 교차 검증 PASS
9. 같은 입력의 결정성 replay PASS ("repeat runs match event and RNG hashes" †)
10. F1~F8 모두 승인 기준 충족 ("aggregate rates fall within predeclared calibration intervals" †)
11. 지배 전략: 양 측 `NO_DOMINANCE_SUPPORTED` ("no strategy dominates…" †)
12. PZL 12개 절 공식 판정 완료 †
13. 미충족·유예 항목을 정확히 기록
14. 공식 결과 보고서
15. 독립 검토와 소유자 최종 승인

- 유예 항목(예: PZL #12b)이 있을 때 Gate를 닫을 수 있는지는 **별도 승인 계약**으로 기록한다.
- CLI Exit 0이나 F1~F8 PASS만으로 Gate를 닫지 않는다.
- **범위 주의:** Phase 3 전체 Gate에는 3-F 밖 항목도 있다. 예: Tests 목록의 "neutral-strategy watched vs instant results identical", "displayed counts equal authoritative state", UI Work, catcher·park fixture, D1 기본 상호작용. 3-F Gate를 통과해도 Phase 3 Gate가 자동으로 닫히지 않는다.

## 16. 소유자 승인 체크리스트

- [ ] FC-D1: `neutral-mixed-v1`(3 RHP : 4 LHP 로테이션, 전원 우타), F1 해석 안 C, F6~F8 판정 코호트, F1 `RecommendedCohort` 정정 허용
- [ ] FC-D2: `virtual-league-v1` 값 집합·이동·타순 로테이션·미러 팀·타자 손잡이
- [ ] FC-D3: 공식 calibration 정책 `normal-balanced-v1`
- [ ] FC-D4: 48셀 구성, `neutral-opp-v1`, TakeFirst 정의, 주 지표(1~8회 half-inning 득점), 독립 표본, α = 0.05, δ(추천 0.05), 검정력 0.8, IUT/Holm, 우선순위 규칙(3), σ_g 파일럿 규칙, INCONCLUSIVE 확장 규칙
- [ ] FC-D5: 계열·Seed 범위·Seed 파일 형식·정지 규칙·위치, 안 C(4층)
- [ ] FC-D6: k = 0.25, DEFF = 2, z = 1.959964, 중앙값 p(또는 보수 p), 층별 판정·합산 금지, F3~F5 판정 코호트
- [ ] FC-D7: 모드 분리 판정(추가 Gate 승인), advancement-v2만, 중립 Park, 이벤트 예산 3,000
- [ ] FC-D8: 12개 절 PASS 조건, #12 처리(12a만 인정 / 12b 유예 기록)
- [ ] FC-D9: 실패 유형, 전체 재실행, Holdout(fc3-hold)
- [ ] FC-D10: 필수 12개 항목과 dirty 정의(추적 파일 기준)
- [ ] FC-D11: 버전 관리 증거 범위, Git 밖 원본 보존 기간
- [ ] FC-D12: 15개 Gate 조건과 유예 시 Gate 종결 가능 여부

**(개정 2)** 위 체크리스트의 권장안은 2026-10-17 소유자가 모두 승인했다. 체크 표시는 승인 이전 기록으로 그대로 둔다. 승인 범위와 아직 동결되지 않은 실행 항목은 §20과 ADR-008을 따른다.

## 17. 계약 충돌·미확정·정정

| ID | Issue | Reason | Recommended Option | Alternative | Impact |
|---|---|---|---|---|---|
| C-1 | F1 판정 코호트 | Tuning 원문은 "league average, neutral inputs", 코드 `RecommendedCohort`는 VirtualLeague | 안 C: 판정 `neutral-mixed-v1`, VL은 진단 | 안 B: VL 판정 (코드 유지) | 승인 시 F-C2에서 정의 1줄 변경 |
| C-2 | F3~F5 코호트 | 원문에 코호트 표기 없음 | VirtualLeague (F-A/B 지시 추천 유지) | Neutral | F-C2 EvaluationPolicy |
| C-3 | "neutral inputs"의 handedness | `neutral` 키가 실제 대결에서 생성되지 않음 | 3/7 혼합 코호트(산술 중립) + 층별 보고 | 100% same(`neutral-v1`) 인정 | F-C2 Cohort 로테이션 |
| C-4 | official dirty 판정 | 현재 Git 검사는 미추적 보호 파일 때문에 항상 dirty | (개정 1) 추적·Staging 변경 거부 + 정확한 보호 파일 3개 경로만 허용 + 그 밖의 미추적 거부 + ignored는 승인 출력 경로만 (§12.1). 이전 "추적 파일 기준" 안은 철회 | 보호 파일을 저장소 밖으로 이동(소유자 작업) | F-C2 `Program.Git` |
| C-5 | PZL 목록 위치 | 3-F는 "below"라고 하지만 실제 목록은 Acceptance Criteria에 있음 | Acceptance Criteria 12개 사용 (ROADMAP 무변경) | — | 없음 |
| C-6 | 모드별 목표 | 목표에 모드 구분이 없음 | 같은 구간을 모드별 적용 (추가 Gate 승인) | Automated만 판정 | FC-D7 |
| C-7 | mix-weighted tendency reference | ADR-006에서 "consider" 상태 | F-C에서는 `pzl-ref-uniform-v1` 유지, 결정하지 않음 | 3-F에서 결정 | 바꾸면 새 TuningVersion |

**이전 Closeout 보고 대비 정정:**
- (a) 최소 분모 값 일부가 z 반올림 때문에 1~2 차이 난다(§8.2).
- (b) 이전 FC-D4 안의 "공수별 Composite Policy 필요"는 셀 설계(양 팀이 같은 전략)로 **불필요**하다(§6.2).
- (c) 이전 FC-D10 안의 "Dirty tree 거부"는 그대로 적용하면 항상 거부되므로 정의를 보완했다(C-4).
- (d) 이전 FC-D2 안의 "팀당 선발 5명"은 handedness 3/7 구조에 맞춰 7명으로 바꿨다.
- (e) (개정 1) "경기 수 기준 정확히 3/7"은 완전한 7경기 블록에서만 성립한다. PA·투구·스윙 비율은 보장하지 않는다(§3.5). 정지 규칙을 블록 단위로 바꿨다(§7.5).
- (f) (개정 1) Dirty "추적 파일 기준" 안은 모든 미추적 파일을 허용하게 되므로 철회했다(§12.1).
- (g) (개정 1) Seed 비중복 범위의 근거는 기록 위생이다. 독립성은 GameId 차이가 보장한다(§7.5). 같은 Seed가 같은 난수 경로를 뜻한다는 가정은 쓰지 않는다.
- (h) (개정 1) IUT·Holm의 적용 범위를 우월성, 비우월성, Gate 주장, 지배 검출 오류로 나눠 명시했다(§6.7). 대응 검정은 쓰지 않는다.

## 18. Phase 3-F Gate

**Phase 3-F Gate: `INCOMPLETE`. Phase 3 Gate: `INCOMPLETE`.** 이 문서는 정책 제안서(`DRAFT — OWNER APPROVAL REQUIRED`)이며 `POLICY_FROZEN`이 아니다. **(개정 2: 2026-10-17 소유자 승인으로 정책 계약은 동결됐다(§20, ADR-008). Gate 상태는 그대로 `INCOMPLETE`다.)** F-C2·F-C3는 미착수이고, 공식 Seed는 비활성 상태이며, 코드·테스트·Tuning 변경과 대규모 실행은 없다.

## 19. 소유자 최종 승인 요청표 (개정 1)

모든 항목은 `PENDING`이다. 숫자와 예외는 승인된 것으로 가정하지 않는다.

| ID | 확정된 기존 계약 (`CONFIRMED`) | 현재 제안 | GPT 보완 사항 (개정 1 반영) | 최종 추천 | 추가 승인 필요 | F-C2 영향 | F-C3 영향 |
|---|---|---|---|---|---|---|---|
| FC-D1 | Tuning 목표 문구, handedness 계수 1.04/0.97, neutral 키 미생성 | `neutral-mixed-v1` (3 RHP : 4 LHP, 전원 우타) | 3/7은 경기 수(완전 블록)만 보장. PA·투구·스윙은 관측값. 계수 평균 1.0 ≠ 결과 중립. 방식 A/B 비교 | F1·F8은 B(3/7·4/7 표준화, 자기 분모, 층별 최소 분모), F6·F7은 A. F1 코호트 (i) neutral-mixed | 예: 판정 방식, 가중치, F1 코호트 (i)/(ii), 대안 B′ | 층별 평가, 정수 ppm 식, F1 `RecommendedCohort` 정정, 로테이션 | L1·L2의 F1·F8 판정 |
| FC-D2 | 없음 (분포 `NOT_SPECIFIED`) | `virtual-league-v1` (순환 Latin, 미러 팀, 7선발·9타순 로테이션) | 블록 63경기와 정지 규칙 정합 | 제안 유지, F2~F5는 A(관측) | 예: 값 집합, 이동, 손잡이, 미러 팀 | 경기 index별 TeamSetup, 로테이션 Hash | L3·L4의 F2~F5 판정 |
| FC-D3 | `normal-balanced-v1` 구현·기본값 | 공식 calibration 정책으로 사용 | 변경 없음 | 유지 | 예: 공식 사용 | Policy Registry 등록 | 모든 cal 층 |
| FC-D4 | Gate 문구 "no strategy dominates every relevant matchup in the declared test matrix" | 48셀, IUT/Holm, 4상태 | 정책 독립성 확인, 대칭 적용 vs 일방 우위(가정 A-1), Payoff 방향, δ 단위, IUT·Holm 범위, 비대응 Welch, 해석 한계 | §6.7 규칙. α 0.05, 검정력 0.8, δ 0.05 runs/half-inning (모두 PROPOSED) | 예: α, δ, 검정력, 우선순위, 지배 검출 보정 여부, 확장 규칙 | 행렬 정책 12개, 셀 실행기, 군집 SE, 판정기, A-1 검사 | fc2-dry 파일럿 → n 동결 → fc3-dom 48셀 |
| FC-D5 | 3-B "seed sets … fixed before any calibration run" | 계열별 범위, Seed 파일 형식 | 같은 Seed ≠ 같은 경로(GameId = Context). 비중복 범위는 위생. 블록 정지 규칙 | `cumulative_completed_pa_block`, 블록 배수 상한, `competition_key` 고유 | 예: 범위, 형식, 위치, 정지 규칙 | Seed Reader, 고유성 검사 | Seed 파일을 Commit한 뒤 실행 |
| FC-D6 | 판정 로직(PASS/FAIL/INSUFFICIENT_SAMPLE/NOT_EVALUATED) 구현 | k 0.25, DEFF 2, z 1.959964, 중앙값 p | 산식·반올림 규칙(DEFF 곱한 뒤 올림 1회) 명시, DEFF는 검증되지 않은 가정, 정밀도 기준임을 명시 | 8개 값 유지(재검산 결과 동일), 층별 적용, F1·F8 층별 최소 분모 | 예: 상수, 반올림 규칙, 보수 대안 채택 여부 | 승인된 `EvaluationPolicy`, batch means DEFF 진단 | 층별 판정 |
| FC-D7 | v4/v4/v3/v2 버전·Hash, `fictional_test` 프로필 | 모드 분리 | 4개 층과 ROADMAP 최소 요구 구분(Min-1/Min-2/Full) | Full(L1~L4), L2·L4는 추가 Gate | 예: 추가 Gate 승인 또는 Min-2 | 층 설정 | 실행 규모 400,000 PA (추천) |
| FC-D8 | 12개 절 문구 | 증거 기반 판정, #12 분리 | 12a/12b 검증 가능성과 Phase 구분, 12a PASS ≠ 12b PASS, 유예 정책 (i)/(ii) 비교 | 12a 판정, 12b 유예 (i) 추천(자동 적용 안 함) | 예: 12b 처리 | #5·#6·#10·#12a 보강 | 12개 절 증거 보고서 |
| FC-D9 | "failing results are reported, not hidden", 수치 변경 = 새 TuningVersion | 실패 유형 5종, 전체 재실행, Holdout | 재실행 범위: 코드 변경 시 모든 공식 계열 | 유지 | 예: Holdout 사용 | 실패 분류 보고 | 단일 SHA 증거 |
| FC-D10 | Exit Code 0/1/2/3 의미, `--tuning` 시험용·replay 거부 | official Fail-Closed 12개 항목 | Dirty 범주 분리(정확한 보호 파일 3개만 허용, 그 밖의 미추적 거부, ignored는 승인 경로만), 입력 Hash 검증 | §12·§12.1 | 예: 허용 목록, ignored 패턴, 직전 빌드 절차 | Strict CLI, official 모드, Dirty 분류기 | official 실행 전제 |
| FC-D11 | `build/` git-ignore, 매니페스트 v1, Phase 4 Save와 분리 | 원본은 Git 밖, 집계·Hash만 버전 관리 | 변경 없음 | 유지 | 예: 원본 보존 기간 | 보고서 생성 | FC3 보고서 |
| FC-D12 | Phase 3 Gate 문구, Common Gate G | 15개 조건 | CLI Exit 0 ≠ Gate. 유예 시 종결 가능 여부는 별도 계약 | §15.1 유지 + #12b 정책 연동 | 예: 유예 종결 계약 | — | 최종 판정 |

**Phase 3-F Gate: `INCOMPLETE`. Phase 3 Gate: `INCOMPLETE`.** 이 개정도 `POLICY_FROZEN`이 아니다.

(개정 2) 위 문장은 개정 1 시점의 기록이다. 현재 상태는 §20.

## 20. 소유자 승인 기록 (개정 2, 2026-10-17)

- **승인:** 프로젝트 소유자가 FC-D1~FC-D12 권장 정책을 승인했다(`OWNER_APPROVED`, 2026-10-17, 소유자 `TASK.md` "Star Player Phase 3-F F-C1 Policy Contract Freeze", 기준 HEAD `5492e80eba7d347f7deabe5dbdb98931f92db0f3`).
- **권위 문서:** [ADR-008](../decisions/ADR-008.md). 이 문서는 근거·대안·산술·48셀 표·후보 Seed 범위를 담은 해설 보고서다.
- **문서 상태:** `FC1_POLICY_CONTRACT_FROZEN_UNCOMMITTED` (Staging·Commit 안 함) — 작성 시점 기록. 이후 `97045829d5836ff6dec8b40109c84d016d92c340`로 로컬 Commit되어 `FC1_POLICY_CONTRACT_FROZEN_COMMITTED`.
- **실행 데이터:** `FINAL_EXECUTION_FREEZE_PENDING`. F-C2 `NOT_STARTED`, F-C3 `NOT_STARTED`. Phase 3-F Gate·Phase 3 Gate `INCOMPLETE`.

| ID | 승인 내용 (요지, 정확한 문구는 ADR-008) | 이 문서 대비 차이·유예 |
|---|---|---|
| FC-D1 | `neutral-mixed-v1`. 손잡이 가중치 same 3/7 · opposite 4/7. F1·F8 = 층별 비율의 사전 가중 표준화(자기 분모, 중간 반올림 없음), F6·F7 = 관측 집계. 층화 분산은 층별·경기 군집 기준, 합산 Wilson 미적용. **C-1 해소: F1 판정 코호트 = `neutral-mixed-v1`** | Tuning JSON 불변. Harness `RecommendedCohort` 정정은 F-C2. 실제 인스턴스·`InputsHash`는 실행 동결 |
| FC-D2 | `virtual-league-v1`, 값 `35, 40, 45, 50, 50, 50, 55, 60, 65`, §4 설계 기준. F2~F5 판정 코호트 | 선수 ID 배치·손잡이 구성·입력 Hash는 F-C2 검증 후 F-C3 전 동결. 다른 분포 도입 금지 |
| FC-D3 | `normal-balanced-v1`. 전략 RNG 금지, 측정 중 전략 변경 금지, 지배 전략 검증과 분리 | 없음 |
| FC-D4 | 48셀. δ = 0.05 runs/half-inning, α = 0.05, 목표 검정력 0.8, 경기 단위 분산, Holm 비우월성, IUT 우월성, 비대응 비교에 paired 금지, 4상태, 증거 부족 ≠ PASS. **A-1 가정은 F-C2 필수 검증** | 셀별 표본 수·실행 상한·Seed Manifest·검정력 근거(§6.5 σ_g 파일럿 규칙, INCONCLUSIVE 확장 규칙 포함)는 실행 동결 |
| FC-D5 | 계열 `fc2-dry`/`fc3-cal`/`fc3-int`/`fc3-dom`, 고유 Competition Key, 결정적 GameId, 사전 순서, SHA-256 고정, 실패·결과 기반 교체 금지, 블록 단위 진행(100,000 PA 도달 후 진행 중 블록 완료) | §7.2·§7.5의 Seed 범위·상한·블록 크기(7/63)는 후보. F-C3 직전 확정. 공식 Seed 파일 미생성 |
| FC-D6 | k 0.25, DEFF 2, z 1.959964, 올림 1회. 최소 분모 8개(§8.2 값과 동일)와 F1·F8 층별 최소 분모 | DEFF 2는 검증되지 않은 설계 가정. fc2-dry 측정 DEFF가 2를 넘으면 자동 변경 없이 보고·소유자 결정 |
| FC-D7 | L1~L4 각 ≥ 100,000 PA(총 ≥ 400,000), 모드별 100 Complete Games, 층 합산·보정 금지. ROADMAP 최소보다 엄격한 승인 Gate. §9 실행 Profile | 없음 |
| FC-D8 | PZL 12개 절 유지. 12a 현재 경계 보존 = Phase 3 필수, 12b = `DEFERRED`(BCL Phase 5·6·28, LMV-A Phase 5·28). 12b 미구현은 Phase 3 최종 Gate의 별도 차단 조건이 아님(12a PASS와 유예 기록이 조건) | 12b를 PASS로 기록하지 않음 |
| FC-D9 | 실패 1건 이상 = 해당 Gate FAIL, 제외 재집계 금지, 통계 미달 = FAIL, 표본 부족 = `INSUFFICIENT_SAMPLE`, 미실행·계약 미충족 = `NOT_EVALUATED`, 코드·Tuning 변경 시 전체 재실행, v4 직접 수정 금지, 재보정은 새 TuningVersion + Holdout | 없음 |
| FC-D10 | Official Mode Fail-Closed 12개 요구와 Git Dirty 정책(§12.1과 같음) | F-C2 구현 계약. 이번 작업은 코드 변경 없음 |
| FC-D11 | 원본은 `build/simharness/<run>/`, 집계·Hash·Manifest·결과·보고서는 Git 관리, 대형 로그 제외 | 원본 보존 기간은 별도 운영 결정. 자동 삭제 없음 |
| FC-D12 | 최종 Gate 15개 조건(ADR-008), CLI Exit 0만으로 PASS 금지 | §15.1 후보 목록 대신 ADR-008 목록이 공식 |

**검산 (개정 2):** `virtual_tuning_v4.json`의 8개 구간으로 최소 분모를 다시 계산했다. 결과 8,158 / 15,297 / 38,239 / 29,964 / 40,906 / 3,043 / 4,795 / 6,229, 층별 3,497 / 4,662 / 2,670 / 3,560으로 모두 같다. §8.5의 F2 올림 전 값 15,296.996은 z = 1.959964에서 15,296.997이다. 올림 결과는 같다.
