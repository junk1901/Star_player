# Star Player Phase 3-F F-C1 Policy Contract Freeze Report

- 작성: Claude Code, 2026-10-17, 소유자 `TASK.md` "Star Player Phase 3-F F-C1 Policy Contract Freeze"
- 기준 HEAD: `5492e80eba7d347f7deabe5dbdb98931f92db0f3` (불변)
- 최종 판정: **`PHASE3_F_FC1_POLICY_CONTRACT_FROZEN_UNCOMMITTED`** (작성 시점)
- 이후 상태: `97045829d5836ff6dec8b40109c84d016d92c340`(`docs: freeze phase 3-F F-C1 validation policies`)로 로컬 Commit → **`FC1_POLICY_CONTRACT_FROZEN_COMMITTED`**(미푸시). 아래 본문은 작성 시점 기록이다.
- 실제 Seed Manifest와 전략 표본 수는 동결하지 않았다(`FINAL_EXECUTION_FREEZE_PENDING`).

## A. 소유자 승인 범위

FC-D1~FC-D12 권장 정책 승인(`OWNER_APPROVED`, 2026-10-17). 권한은 문서 수정뿐이다. Staging·Commit·Push, F-C2/F-C3 구현, 공식 Seed 생성·활성화, 대규모 실행, Tuning 변경, Codex 호출은 승인 범위 밖이며 하지 않았다.

## B. FC-D1~FC-D12 동결 결과

12개 모두 `OWNER_APPROVED`로 [ADR-008](../decisions/ADR-008.md)에 기록했다. 항목별 요지와 제안서 대비 차이는 [제안서 §20](PHASE_3F_FC1_POLICY_PROPOSAL.md) 표에 있다.

## C. 권위 문서와 변경 이력

| 문서 | 역할 | 변경 |
|---|---|---|
| `docs/decisions/ADR-008.md` | **권위 정책 계약** | 신규 |
| `docs/reports/PHASE_3F_FC1_POLICY_PROPOSAL.md` | 근거·해설(대안·산술·48셀 표·후보 범위) | 개정 2: 상태 헤더, §0 승인 열(이전 값 보존), §16·§18 주석, §20 승인 기록. 개정 1까지 내용 삭제 없음 |
| `ROADMAP.md` 3-F | Gate 영향 | 승인된 Gate 변경 상태 노트 1개(4개 층·모드별 100경기, PZL 12a/12b) |
| `docs/game-engine.md` §35 | 엔진 측 계약 요약 | 신규 절 |
| `docs/SYSTEM_CONTRACTS.md` | C08/C11 상태 | 항목 1개 추가 |
| `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md` | 현재 상태·대시보드·리뷰 부록 | 갱신 (리뷰 상태 `검토 대기` 유지) |
| `Star/02_Decisions/2026-10-17_Phase_3F_FC1_Policy_Freeze.md` | 결정 노트 | 신규 |
| 이 보고서 | 동결 보고 | 신규 |

기존 ADR-001~007의 기록은 수정하지 않았다. 기존 미커밋 변경(CURRENT_STATE·Dashboard·Phase_03_Review의 Closeout/제안서 기록)은 그대로 두고 그 위에 추가했다.

## D. Neutral / VirtualLeague 계약

- `neutral-mixed-v1`: Rating 50, Normal/Balanced, Framing·GameCalling·PitcherControl 50, 전원 우타, 3 RHP : 4 LHP 선발 로테이션. F1·F6·F7·F8 판정. **F1 코호트 충돌(C-1)은 소유자 결정으로 해소**: Tuning 원문과 기존 Harness `RecommendedCohort = VirtualLeague` 사실은 보존, Tuning JSON 불변, Harness 정의 변경은 F-C2.
- F1_std = 3/7·SO_S/PA_S + 4/7·SO_O/PA_O, F8_std = 3/7·SwStr_S/Sw_S + 4/7·SwStr_O/Sw_O, 정수 ppm 한 번에 half-up. 실제 경기·PA·투구·스윙 비율은 3/7과 다를 수 있고, 계수 평균 1.0은 결과 중립이 아니다. 층화 분산은 층별·경기 군집으로 추정하고 합산 Wilson은 쓰지 않는다.
- `virtual-league-v1`: 값 35, 40, 45, 50, 50, 50, 55, 60, 65 (평균 50, 대칭), 제안서 §4 설계. F2~F5 판정. 실제 인스턴스·Hash는 실행 동결.

## E. 8개 지표 및 최소 표본

k 0.25, DEFF 2, z 1.959964, `ceil(DEFF·z²·p(1-p)/(k·h)²)`, 올림 1회. `virtual_tuning_v4.json`에서 재계산한 값이 승인값과 모두 일치한다.

| 지표 | 판정 코호트 | 방식 | 최소 분모 | 층별 최소 |
|---|---|---|---:|---|
| F1 | neutral-mixed | 표준화 | 8,158 | PA_S 3,497 / PA_O 4,662 |
| F2 | virtual-league | 관측 | 15,297 | — |
| F3 | virtual-league | 관측 | 38,239 | — |
| F4 | virtual-league | 관측 | 29,964 | — |
| F5 | virtual-league | 관측 | 40,906 | — |
| F6 | neutral-mixed | 관측 | 3,043 | — |
| F7 | neutral-mixed | 관측 | 4,795 | — |
| F8 | neutral-mixed | 표준화 | 6,229 | Sw_S 2,670 / Sw_O 3,560 |

DEFF 2는 검증되지 않은 설계 가정이며, 이 임계값만으로 오류율을 보장한다고 주장하지 않는다.

## F. 48셀 지배 전략 판정

4 타자 × 3 투수 × 2 손잡이 × 2 CalledPitchMode = 48셀. δ = 0.05 runs/half-inning, α = 0.05, 목표 검정력 0.8, 경기 단위 분산, Welch, Holm 비우월성, IUT 우월성, paired 금지. 판정 `DOMINANCE_DETECTED` / `NO_DOMINANCE_SUPPORTED` / `INCONCLUSIVE` / `NOT_EVALUATED`; 증거 부족은 PASS가 아니다. **가정 A-1은 F-C2 필수 검증**(불성립 시 공식 행렬 전에 구조 수정·소유자 보고). 같은 Seed라도 GameId가 다르면 RNG 경로가 다르며, 이는 독립성의 수학적 증명이 아니다.

## G. 공식 실행 4개 층 및 모드별 100경기

L1 neutral × Automated, L2 neutral × NonAutomated (F1, F6, F7, F8), L3 VL × Automated, L4 VL × NonAutomated (F2~F5). 층마다 ≥ 100,000 PA, 총 ≥ 400,000 PA, 층 합산·보정 금지. 통합 검증은 모드별 100 Complete Games. ROADMAP 최소보다 엄격한 소유자 승인 Gate다.

## H. PZL #12 유예 계약

12개 절 유지. 12a(현행 경기 RNG 경계 보존)는 Phase 3 필수 검증. 12b(BCL/LMV 실제 기능의 RNG 비소비)는 `DEFERRED` — BCL Phase 5·6·28, LMV-A Phase 5·28(해당 Phase의 기존 ROADMAP 테스트가 이미 Hash 불변을 요구). 12b는 PASS로 기록하지 않는다. 12a 실제 PASS와 유예 기록을 조건으로 12b 미구현은 Phase 3 최종 Gate의 별도 차단 조건이 아니다. 나머지 11개 절은 실제 증거가 필요하다.

## I. Official Mode 요구사항 (F-C2 구현 계약)

미등록·중복 옵션 거부, 공식 실행의 `--tuning FILE` 거부, 승인 Model/Tuning Hash·Cohort/Policy·Seed Manifest Hash·입력 파일 Hash 검증, Git Dirty 검증, 모든 공식 Cohort/Policy Replay, 공식 실행 Manifest, Exit Code와 통계 Gate 분리, 실패 재현 증거 보존. Dirty: 추적 변경·Staging·보호 파일 외 미추적·미승인 ignored는 거부, 정확한 경로의 보호 파일 3개와 승인 빌드 출력만 허용. 보호 파일은 공식 입력이 아니며, 빌드 출력은 승인 SHA·입력 Hash에 결속된다.

## J. Final Execution Freeze 미정 항목 (`FINAL_EXECUTION_FREEZE_PENDING`)

1. 공식 Seed Manifest 4계열과 파일 SHA-256, 정확한 Seed 범위
2. Calibration 블록 크기(제안서 후보 7 / 63경기)와 상한
3. 48셀 셀별 공식 표본 수, 실행 상한, 검정력 추정 근거(σ_g 파일럿 규칙·INCONCLUSIVE 확장 규칙은 후보)
4. 최종 Cohort 인스턴스(선수 ID 배치·손잡이 구성)와 `InputsHash`·입력 파일 Hash
5. 대규모 실행 직전 Git SHA
6. fc2-dry 측정 DEFF가 2를 넘을 때의 처리(자동 변경 없음, 소유자 결정)

## K. F-C2 선행 구현 목록 (미승인·미착수)

Strict CLI, Official Mode(Dirty 분류·입력 Hash·공식 Manifest·Gate 결과 분리), Cohort Registry(`neutral-v1`·`neutral-opp-v1`·`neutral-mixed-v1`·`virtual-league-v1`, 경기 index별 로테이션, `InputsHash` 결속), Policy Registry(`normal-balanced-v1` + 행렬 정책 12개), Seed Manifest Reader(Hash 검증·블록 정지 규칙·competition key 고유성), 승인 `EvaluationPolicy`(최소 분모, 코호트, F1/F8 층화와 층별 최소, F1 코호트 정정, 층화·경기 군집 분산과 batch-means DEFF 진단), 전략 행렬 실행·판정기, A-1 검증, PZL #5·#6·#10·#12a 보강, 해당 테스트, 비공식 Seed Dry Run. 엔진·Model·Tuning·RNG·Commit·CAS 변경 없음.

## L. Git / 보호 파일 상태

| 항목 | 시작 | 종료 |
|---|---|---|
| Branch | main | main |
| HEAD | `5492e80` | `5492e80` |
| Staging | 0 | 0 |
| Commit / Push | — | 0 / 0 |
| `TASK.md` SHA-256 | `647f6bb6…5109` | 동일 |
| `docs/2023recordRule.pdf` | `9742caaf…12b0` | 동일 |
| `docs/2026-official-baseball-rules.pdf` | `f85860f3…7859` | 동일 |
| src / tools / tests / content 변경 | 0 | 0 |
| `git diff --check` | — | PASS |

종료 시 확인 명령과 결과는 이 작업의 최종 채팅 보고에 그대로 기록했다. 전체 xUnit·`verify.ps1`은 재실행하지 않았다(`미검증`; 2,528 PASS는 `5492e80`의 과거 결과).

## M. Phase 3-F / Phase 3 전체 Gate

- F-C1 정책 계약: `FC1_POLICY_CONTRACT_FROZEN_UNCOMMITTED` (작성 시점; 이후 `97045829` Commit으로 `FC1_POLICY_CONTRACT_FROZEN_COMMITTED`)
- 실행 데이터: `FINAL_EXECUTION_FREEZE_PENDING`
- F-C2: `NOT_STARTED`, F-C3: `NOT_STARTED`
- **Phase 3-F Gate: `INCOMPLETE`. Phase 3 Gate: `INCOMPLETE`.**
- 새로 발견된 미해결 계약 충돌 없음(`BLOCKED` / `OWNER_DECISION_REQUIRED` 없음). 해석 NOTE 4건은 ADR-008 "Notes".
