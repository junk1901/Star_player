# Star Player Phase 3-F F-C2-C Implementation Report — 48-Cell Strategy Dominance Check

- 작성: Claude Code, 2026-10-18, 소유자 `TASK.md` "Star Player Phase 3-F F-C2-C Implementation"
- 기준 HEAD: `f2e6eb396e445cea40d7e531052e4cda756a7ca8` (`feat: implement phase 3-F F-C2-B stop replay cohorts and evaluation`) — 작업 중 불변
- 권위 계약: [ADR-008](../decisions/ADR-008.md) FC-D4(48셀)·FC-D5·FC-D7.5·FC-D9, [ADR-009](../decisions/ADR-009.md) SR-D5(`fc3-dom` 전체 실행). 해설: [정책 제안서 개정 2](PHASE_3F_FC1_POLICY_PROPOSAL.md) §6.1~§6.7(ADR-008 FC-D4.1·FC-D4.3이 §6.6 표와 §6.4/§6.7 규칙을 지정). 두 ADR 본문은 변경하지 않았다.
- 최종 판정: **`PHASE3_F_FC2C_IMPLEMENTED_WITH_NOTES`** · `TESTED` · `REVIEW_PENDING` · `OFFICIAL_EXECUTION_NOT_READY` · `FINAL_EXECUTION_FREEZE_PENDING`
- 검토 상태: `검토 대기` (구현자 자체 시험만; GPT Chat·Codex 독립 리뷰 `미실시`)
- **Phase 3-F Gate: `INCOMPLETE`. Phase 3 Gate: `INCOMPLETE`.** 공식 48셀 실행은 하지 않았고 공식 지배력 데이터는 없다.

## A. 기준 상태와 범위

- 시작 상태는 `TASK.md`의 기대와 일치: Branch `main`, HEAD `f2e6eb3…`, 추적 변경 0, 미추적은 보호 파일 3개뿐.
- 범위: `tools/StarPlayer.SimHarness`와 그 시험. **엔진·확률 모델·Tuning·Rules 콘텐츠·RNG·Commit/CAS·`RunConfig.ConfigHash`·`game-result-v1`·`run-result-v1/v2`·`OfficialAttestation` v1·`OfficialProfile.Hash()`·`ef1/ef2`·Legacy Fixture 불변.**
- 하지 않은 것: Official Execution 활성화(`ExecutionPathReady=false`), 공식 Seed 파일·Pin, 셀당 표본 수 등 Final Execution Freeze 값, 공식 48셀 실행, F-C3, Phase 4, Staging·Commit·Push.

## B. 승인 계약 추출 (참조 문서·정책 ID)

| 항목 | 승인 내용 | 출처 |
|---|---|---|
| 축 | Batter {Normal, Patient, Aggressive, TakeFirst} × Pitcher {Balanced, Attack, Careful} × Hand {same, opposite} × `CalledPitchMode` {Automated, NonAutomated} = 48 | ADR-008 FC-D4.1 |
| 셀 정의 | 셀 ID·코호트(`neutral-v1`/`neutral-opp-v1`)·규칙(`test-pzl-automated`/`-non-automated`)·정책(`dom-<b>-<p>-v1`)·Competition Key(`fc3-dom-<hm>-<bp>`)·비교 열(B-col `<P><H><M>`, P-col `<B><H><M>`) | FC-D4.1 → 제안서 §6.6 표 |
| 전략 정의 | Normal/Patient/Aggressive = 매 결정 지점 그 Approach; TakeFirst = 0-0이면 `Take`, 아니면 Normal; Pitcher = 모든 투구 그 Intent; 양 팀 같은 정책 | 제안서 §6.1·§6.2 |
| `neutral-opp-v1` | 타자 우타, 선발·구원 좌투, 평가값 50 | 제안서 §6.2, ADR-008 Consequences |
| 지표 | 1~8회 half-inning 득점; 경기 = 군집, 경기 관측값 = 16개 half-inning 평균 | FC-D4.2, §6.3, §6.7 #1 |
| 비교 | D_B = R̄(σ) − R̄(τ), D_P = R̄(τ) − R̄(σ); 비대응 Welch z; paired 금지 | FC-D4.2, §6.7 #2·#9 |
| 지배 | IUT: W_σ 모든 (τ, 열)에서 단측 (1−α) 하한 > 0 | FC-D4.2, §6.7 #5a·#6 |
| 비지배 | H0 "D ≥ δ", Z = (δ − D̂)/SE, Holm(|W_σ| = 36 타자 / 32 투수, α) 기각 ≥ 1 | FC-D4.2, §6.7 #5b·#7 |
| 상수 | δ = 0.05 runs/half-inning, α = 0.05 단측, 목표 검정력 0.8 | FC-D4.2 |
| 우선순위 | 지배·비지배 동시 성립 시 지배 우선; 7개 후보 합집합 미보정 | FC-D4.3, §6.4-3, §6.7 #5d |
| 측별 판정 | `DOMINANCE_DETECTED` / `NO_DOMINANCE_SUPPORTED` / `INCONCLUSIVE` / `NOT_EVALUATED`; 증거 부족은 PASS 아님; 양 측 `NO_DOMINANCE_SUPPORTED`일 때만 PASS | FC-D4.4, §6.4-4·5 |
| INCONCLUSIVE | Gate는 PASS가 아니라 `INCOMPLETE`, 소유자 결정 | §6.5, §6.7 #8 |
| 실패 | 엔진·지표 실패 ≥ 1 → 그 검증 Gate FAIL; 제외 재집계 금지; 표본 부족 `INSUFFICIENT_SAMPLE`; 미실행·계약 미충족 `NOT_EVALUATED` | FC-D9.1·9.2·9.4 |
| 실행 계약 | `fc3-dom`은 전체 실행(정지 규칙·`pa_target`·정지 기록 없음, 정확한 전체 목록), 공식 Seed 파일 `max_games == Seed 수` | ADR-009 SR-D5·SR-D6 |
| 가정 A-1 | 정책 무상태·점수/이닝 무관·half-inning 간 이월 없음·교체 없음을 F-C2에서 검증 | FC-D4.5, §6.7 |
| 미동결 | 셀당 공식 표본 수, 실행 상한, Seed Manifest, 검정력 근거(σ_g 파일럿·INCONCLUSIVE 확장 규칙은 후보) | FC-D4.7 |

정책 공백으로 STOP할 항목은 없었다. 아래 §D의 해석·기술 선택은 승인 규칙을 바꾸지 않으며, 소유자 확인이 필요한 항목은 §J에 따로 적었다.

## C. 구현 구조

- **`Dominance.cs`(신규):**
  - `DominancePolicy` — 12개 행렬 정책(생성자 private, 인스턴스 12개뿐). 공개 볼카운트만 읽음.
  - `DominanceCell` / `DominanceMatrix` — 승인 축의 중첩 반복(hand → mode → batter → pitcher)으로 48셀을 **생성**한다(손으로 쓴 목록이 아니므로 누락·중복·오표기 불가). `Config(cell)`은 등록부(코호트·정책)와 고정 공식 실행 프로필(FC-D7.5)만으로 설정을 만들고, `Run(cell, seeds)`는 전체 실행, `CellOf(SeedManifest)`는 Pin으로 읽은 `fc3-dom` Seed 파일을 정확히 한 셀에 결속(family·key·코호트·정책·규칙 일치, `pa_target` 거부).
  - `RegulationRuns` — 완료 경기의 1~8회 16개 half-inning 득점. 엔진 Line Score를 읽고 ScoredPlay 득점과 반이닝별로 교차 확인, Line 합계 = 최종 점수 확인. 불일치는 오류 코드(보정 없음).
  - `CellSample` / `DominanceStatistics` — 정수 n·ΣT·ΣT²에서 R̄과 경기 군집 분산(분자 128비트 정수). Welch SE, 단측 하한, 비우월 Z, Holm, IUT, 측별 판정.
  - `DominanceEvaluator` — 셀 결과 수용(무결성)·셀 상태·Gate 결합. `DominanceEvaluation.Canonical()`은 두 판정의 정확 비교용 텍스트(공식 해시 아님).
  - `DominanceReplay` — 셀 정의에서 설정을 다시 만들어 기록된 계획을 전부 재실행하고 비교.
- **기존 파일 변경(최소):** `Cohorts.cs`(`NeutralCohort`를 투수 손 매개변수로 일반화, `CreateOpposite()`; `neutral-v1` 입력·해시 동일), `Replay.cs`(등록부에 `neutral-opp-v1`과 12개 정책), `RunModel.cs`·`GameExecutor.cs`(`GameRunResult.Regulation` init 속성; 결과 해시 텍스트 밖), `Official.cs`(`OfficialApprovals`에 선택 인자 `DominanceGamesPerCell = null` 추가 — 미동결).
- F-C2-B의 `CombineStrata`·`StratifiedEvaluator`는 재사용하지 않았다(calibration 층 규칙이라 근거 없이 전용하지 않음). `RunResult`·`SimRunner`·`RunResultText.V1Text`·`OfficialProfile`·`CohortRegistry`·`PolicyRegistry`·`HarnessData`는 그대로 재사용.

## D. 판정·오류 처리

**셀 상태**(셀마다 하나, 사유 문자열 포함): `Ok` · `Missing`(실행 없음) · `IntegrityViolation` · `Incomplete`(계획보다 결과가 적거나 취소) · `EngineFailure`(실패 경기) · `MetricsFailure`(완료 경기의 1~8회 관측 실패) · `InsufficientSample`.

**무결성 검사**(하나라도 어긋나면 그 셀은 증거로 쓰지 않음): 정지 인식 실행(SR-D5); Competition Key·코호트·규칙·정책 결속(정책은 해당 `DominancePolicy` 인스턴스여야 함 — 같은 ID의 다른 객체·같은 동작의 `normal-balanced-v1` 거부); 규칙의 `CalledPitchMode`; 등록 코호트와 같은 `InputsHash`; 모델·튜닝·규칙 해시·진루·구장·이벤트 예산 = 고정 프로필; 계획 index 1..n과 GameId 규칙; `config_hash` 재계산; 결과가 계획의 앞쪽과 순서까지 일치; 집계와 `run-result-v1` 재계산; 모든 경기의 손잡이 층 = 셀의 Hand. 같은 셀 실행 2개, 행렬 밖 실행, null은 집합 문제.

**Gate 결합 순서**(모든 비-Ok 셀과 집합 문제가 사유에 이름으로 남는다):
1. 신뢰 가능한 셀 실행에 실패 경기 또는 지표 실패 → **FAIL**(FC-D9.1; 다른 누락 셀이 이를 가리지 못함).
2. 누락·무결성 위반·불완전 셀 또는 집합 문제(셀 간 계획 n 불일치, 동결 n보다 큰 계획 포함) → **NOT_EVALUATED**.
3. 남은 비-Ok가 표본 부족뿐 → **INSUFFICIENT_SAMPLE**.
4. 48셀 모두 Ok → 통계 계산. 셀당 표본 수 **미동결이면 Gate `NOT_EVALUATED`**(측별 판정은 진단으로만 남김).
5. 동결된 경우: 어느 측이든 `DOMINANCE_DETECTED` → **FAIL**; 양 측 `NO_DOMINANCE_SUPPORTED` → **PASS**; 그 밖(INCONCLUSIVE·통계 미정의) → **INCOMPLETE**(INCONCLUSIVE) 또는 **NOT_EVALUATED**.

이 Gate 어휘(`DominanceGate`)는 calibration의 `GateStatus`와 따로 두었다(§6.7 #8의 `INCOMPLETE` 때문; 기존 `GateStatus`·`OfficialOutput.GateStatuses` 불변).

**구현 결정(정책 변경 아님, 근거 포함):**
- **D-1 셀당 표본 수 미동결 → Gate `NOT_EVALUATED`.** FC-D4.7이 셀당 공식 표본 수를 Final Execution Freeze 값으로 둔다. 실제로 셀당 2경기 실측에서 분산이 우연히 작아 Holm이 기각되어 PASS가 나오는 것을 확인했다(작은 n의 Welch z는 반보수적). 동결 값은 `OfficialApprovals.DominanceGamesPerCell`(현재 `null`)에서만 읽으며, 기록이 스스로 값을 가져올 수 없다(Replay도 승인값 사용). 동결 시: n < 동결값 → `INSUFFICIENT_SAMPLE`(FC-D9.4), n > 동결값 → 동결 계획 아님(`NOT_EVALUATED`).
- **D-2 SE = 0(분산 0인 두 셀의 비교) → 양 측 `NOT_EVALUATED`.** Welch z가 정의되지 않으므로 무한 정밀로 취급하지 않는다(처음 구현한 극한값 처리는 같은 실측에서 PASS를 만들어 폐기). 분산 0 셀이 서로 비교되지 않으면 정의되므로 계산한다.
- **D-3 셀 지표 실패 → Gate FAIL.** `fc3-dom`에는 SR-D9.2(정지 인식 calibration 전용 표)가 아니라 FC-D9.1("엔진 또는 지표 실패는 그 검증 Gate 실패")을 적용했다. 측별 판정은 §6.4-4대로 `NOT_EVALUATED`. F-C2-B 보고서 §M-7(calibration 층 지표 오류의 FAIL/NOT_EVALUATED)과 같은 질문 계열이므로 소유자 확인 대상(§J).
- **D-4** 누락·무결성 문제와 표본 부족이 함께 있으면 `NOT_EVALUATED`(행렬 계약 자체가 미충족). 어느 쪽이든 PASS가 아니며 모든 셀 사유가 남는다.
- **D-5** 제안서 §6.6 "셀마다 같은 n"을 집합 무결성으로 강제.
- **D-6** 정규 분위수는 Acklam 유리 근사(상대 오차 < 1.15e-9). 독립 기준값(Python `statistics.NormalDist`)과 1e-8 이내 일치를 시험으로 고정. Holm 기각은 `Z ≥ z(1 − α/(m − k))`(= p ≤ 임계, 경계 포함), 지배는 하한 `> 0`(엄격).
- **D-7** 1~8회 관측값은 `game-result-v1` 해시 밖이다(해시 계약 불변). 이벤트에서 결정적으로 나오므로 변조는 Replay 재실행 비교로 검출한다. 새 공식 해시는 만들지 않았다.

## E. 가정 A-1 검증

- **코드 검토:** 확률 계산(`PitchPreparation`, `StageProbability`, `ProbabilityModel`)은 점수·이닝을 읽지 않는다(검색 결과 점수 참조는 Reducer의 종료·끝내기 처리와 BoxScore뿐이며 끝내기 판정은 `Inning ≥ RegulationInnings(9)`에서만 작동해 1~8회에 영향 없음). `GameReducer.NextHalf`가 매 half-inning 아웃·볼카운트·주자·Approach를 초기화한다. 하네스는 교체를 하지 않는다(`Harness.substitution_required`). 하네스 코호트에 피로 입력은 없다.
- **시험:** 12개 정책의 결정이 점수·이닝·초/말·아웃이 달라도 같고 볼카운트에만 따름; 인스턴스 필드 모두 읽기 전용, public 생성자 없음; 실제 경기에서 1~8회 16개 half-inning의 첫 결정 지점이 모두 아웃 0·주자 0·0-0이고 교체 0; TakeFirst는 PA마다 정확히 1번(0-0)만 Take. 결론: 현재 엔진·정책에서 A-1은 성립한다(대칭 셀의 half-inning 득점이 (공격 B, 수비 P)만의 보수를 추정).

## F. 결정성·Replay·Hash

- 셀 순서는 생성 규칙으로 고정; 평가는 Competition Key로 셀을 찾으므로 입력 순서와 무관(뒤섞은 입력의 `Canonical()` 동일).
- 같은 셀·Seed 반복 실행 → 같은 `config_hash`·`run_result_hash`·관측값(Run ID는 입력 아님). 다른 셀 → 다른 Key·GameId·RNG Context.
- `DominanceReplay`: 기록된 설정을 신뢰하지 않고 셀 정의로 재구성 → 경기 전에 GameId 규칙·`config_hash` 검사 → 전체 재실행 → 경기별 상태·결과/이벤트/RNG 해시·실패 코드·관측값, `run_result_hash`, 다시 계산한 판정 전체 비교. 오류 코드: `DomReplay.cell_unknown`, `.game_id_mismatch`, `.config_hash_mismatch`, `.result_games_mismatch`, `.run_result_hash_mismatch`, `.evaluation_mismatch`.
- 48셀 × 2경기 전체 Replay가 일치함을 시험으로 확인. 변조: 해시를 일관되게 다시 계산한 위조 실패 경기, 해시 밖 관측값 편집, Seed 교체, GameId 교체, 결과 해시 위조, Gate 편집, 다른 동결 n의 기록, 행렬 밖 실행 — 모두 거부.
- `fc3-dom`에 ADR-009 정지 규칙을 적용하지 않음: 정지 인식 실행은 SR-D5 사유로 거부, `CellOf`는 `pa_target` 거부, `StopPlan.FamilyIsStopAware("fc3-dom") == false`.

## G. 시험

- **신규:** `tests/StarPlayer.SimHarness.Tests/DominanceTests.cs` 21개.
  - 구조: 48셀이 제안서 §6.6 표(문서를 정규식으로 읽은 독립 기준)와 12개 열 모두 행 단위 일치; ID·Key·축 조합 중복 없음; 열 12개×4셀, 16개×3셀; 12개 정책 등록·설정 결속; Seed 파일 결속과 오결속·정지 규칙 거부.
  - A-1: 위 §E의 두 시험.
  - 관측: Line Score 정상·짧음·합계 불일치·플레이 불일치; 실제 96경기 모두 16값·교차 확인 통과.
  - 통계: 분위수 독립 기준값; 하한 = 0 경계(엄격), Holm 임계 경계(포함), Holm 정지; 셀 표본 정확 산술; 동일 셀 → 양 측 비지배 PASS; 타자 Patient·투수 Careful 지배 검출 → FAIL; 한 열만 빠진 우위는 지배 아님; δ 미만이지만 유의한 우위는 지배 우선; 정밀도 부족 → INCONCLUSIVE/INCOMPLETE; 분산 0 비교 → NOT_EVALUATED; 결정성.
  - 실제 48셀(셀당 2경기, 비공식 Seed 11·12): 48셀 모두 Ok, 미동결 Gate `NOT_EVALUATED`, 순서 무관; 누락·중복·행렬 밖·null; 무결성 위반 10종; 실패 경기 → FAIL(누락 셀과 함께여도 FAIL, 사유에 둘 다); 지표 실패 → FAIL; 불완전 → NOT_EVALUATED; 1경기·동결값 미달 → INSUFFICIENT_SAMPLE; 동결값 초과·셀 간 n 불일치 → NOT_EVALUATED; Replay 일치·변조 검출; 반복 실행 동일; Official Execution 비활성(`ExecutionPathReady=false`, 공식 Pin 없음, Git SHA 미동결).
- **기존 시험 변경(약화 아님, F-C2-C 자리표시 갱신):** `OfficialAndReplayTests` — `neutral-opp-v1`이 `Registry.cohort_not_implemented`라는 단언 3곳을 "구현됨·자기 ID·`neutral-v1`과 다른 해시"(등록부), "`Official.cohort_hash_not_frozen`"(Preflight; 다음 검사에서 여전히 거부), "`Replay.cohort_hash_mismatch`"(Replay)로, 정책 목록 단언을 13개로 갱신. `CodexFindingsTests` — 같은 이유로 1곳을 `Replay.cohort_hash_mismatch`로. 모두 여전히 거부를 단언한다.
- **Sabotage(표적 변형 → 시험 실패 확인 → 원복, SHA-256 원복 확인): 12/12 검출.** IUT all→any, Holm `>=`→`>`, 하한 `>`→`>=`(처음엔 미검출 → 판정 경로를 시험된 함수 하나로 모은 뒤 검출), 투수 보수 부호, 실패 Gate 무력화, 미동결 n으로 Gate 결정, 결과 해시 무결성 끔, Replay 관측 비교 끔, 우선순위 반전, 분산 0 허용, 정지 인식 허용(처음엔 다른 검사가 잡아 미분리 → 사유 단언 추가 후 검출), 정책 쌍 결속 끔.

## H. 전체 검증 (실제 실행)

| 명령 | 결과 |
|---|---|
| `dotnet build tools/StarPlayer.SimHarness -c Release` | 경고 0, 오류 0 |
| `dotnet test tests/StarPlayer.SimHarness.Tests -c Release --filter DominanceTests` | 21 PASS / 0 FAIL / 0 SKIP |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1 -LogName PHASE_3F_FC2C_VERIFY_LOG.md` | **`GATE: PASS`, exit 0** |

`verify.ps1` 세부: Release 빌드 11개 프로젝트 경고 0; Domain 327 · Simulation 741 · Application 127(Architecture 14, Determinism 48 포함) · Infrastructure 1,233 · **SimHarness 313**(292 + 21) = **2,741 PASS / 0 FAIL / 0 SKIP**; Godot headless 4종, Windows export, Exported program 3종, 문서 링크 150개 파일 PASS. 환경: Windows 11, .NET SDK 8.0.425, Godot 4.7.2 mono.

- 로그: `docs/reports/PHASE_3F_FC2C_VERIFY_LOG.md` SHA-256 `c013e2bb2ec3b750202eabc2b5e5e4f68af3d84fd457f1307d594b9335303d29`.
- 원시 증거: `build/verify-archive/PHASE_3F_FC2C_run1/`(28개 파일, `diff -rq build/verify` 동일; `verify-result.json` `b15a806fb89a3a588ce07c9194c2744082d3e9520630055621dc3b005a324f14`).
- **기존 증거 보존:** 실행 전 `build/verify`가 `PHASE_3F_FC2B_FIX3_run1`과 동일함을 확인했고, 기존 `build/verify-archive/` 전체와 기존 `*VERIFY_LOG.md`·`PHASE_3F_FC2A_VERIFY_EVIDENCE.md`(152개 파일)의 SHA-256이 실행 전후 동일.
- 이 보고서와 상태 문서는 `verify.ps1` 실행 뒤에 작성했다(그 링크 검사는 이 문서들을 포함하지 않음; §K 참조).
- 실행하지 않음: Codex·GPT Chat 독립 리뷰(`미실시`), 전체 Mutation, 공식·대규모 48셀 실행, σ_g 파일럿 dry run.

## I. F-C2-A/B 호환성

- `neutral-v1` `InputsHash` 불변(기존 단언 `LegacyNeutralInputsHash` PASS), 두 Legacy Manifest Fixture Replay·`run-result-v2` Golden Vector·Attestation 시험 전부 PASS(SimHarness 기존 292 전부 통과).
- `game-result-v1` 텍스트 불변(관측값은 해시 밖 init 속성). `ConfigHash`, `run-result-v1/v2`, `OfficialAttestation` v1, `OfficialProfile.Hash()`(FamilyCohorts 이미 `neutral-opp-v1` 포함) 불변.
- `OfficialApprovals`의 새 선택 인자는 기본 `null`; 기존 위치 인자 생성자 호출은 그대로 컴파일·동작. `OfficialApprovals.Current` 동결값 없음 유지.
- F-C2-B Stop/Replay·층화 평가 시험 전부 PASS.

## J. 공식 실행 전 미확정·남은 문제

**소유자 결정 필요(Final Execution Freeze 또는 확인):**
1. 셀당 공식 표본 수·실행 상한·48개 `fc3-dom` Seed 파일과 Pin·검정력 근거(σ_g 파일럿 규칙과 INCONCLUSIVE 확장 규칙은 후보) — FC-D4.7. `OfficialApprovals.DominanceGamesPerCell`이 `null`인 동안 Gate는 결정되지 않는다.
2. §D-3 해석 확인: `fc3-dom` 셀의 지표 실패 → Gate FAIL(FC-D9.1). calibration 층의 §M-7 질문과 함께 정리 권장.
3. §D-2 확인: 분산 0 비교를 `NOT_EVALUATED`로 둠(대안: FAIL — 대규모 n에서 분산 0은 엔진 이상 징후일 수 있음).
4. `neutral-opp-v1` `InputsHash` 동결(현재 값은 시험으로 `neutral-v1`과 다름만 확인).

**구현 과제(이번 지시 범위 밖, 다음 단계):**
5. 공식 Preflight/Replay에 `DominanceMatrix.CellOf` 결속을 연결하는 일. 현재 `OfficialPreflight`는 `fc3-dom` Seed 파일의 정책을 등록 여부로만 확인한다(F-C2-A 동작 그대로; `ExecutionPathReady=false`라 실행 불가). 연결하면 F-C2-A 시험 Fixture(`fc3-dom-x` + `normal-balanced-v1`)를 실제 셀로 바꿔야 해 이번에는 하지 않았다.
6. 48셀 실행용 CLI·결과 기록 형식(FC-D11 증거 보존)과 공식 실행 경로. 현재 진입점은 라이브러리(`DominanceMatrix.Run`, `DominanceEvaluator`, `DominanceReplay`)뿐이다. 새 결과 형식에 해시가 필요하면 별도 승인 대상.
7. F-C2-B 보고서가 F-C2-C로 넘긴 PZL #5·#6·#10·#12a 검사와 실제 `fc2-dry` 파일럿은 이번 `TASK.md`(48셀 지배력) 범위가 아니어서 하지 않았다.

**기술 부채·위험:** 분위수·Welch 계산은 IEEE 배정밀도(같은 머신에서 결정적; 판정 경계 정확히 위의 값은 플랫폼 간 1 ulp 차이 가능성이 이론상 있음 — 판정은 해시에 넣지 않음). 실측 2경기 셀의 측별 판정은 진단일 뿐이며 증거가 아니다.

## K. 변경 Manifest

| 구분 | 파일 |
|---|---|
| 신규(개발) | `tools/StarPlayer.SimHarness/Dominance.cs` |
| 수정(개발) | `tools/StarPlayer.SimHarness/Cohorts.cs`, `GameExecutor.cs`, `Official.cs`, `Replay.cs`, `RunModel.cs` |
| 신규(시험) | `tests/StarPlayer.SimHarness.Tests/DominanceTests.cs` |
| 수정(시험) | `tests/StarPlayer.SimHarness.Tests/OfficialAndReplayTests.cs`, `CodexFindingsTests.cs` |
| 신규(기록) | `docs/reports/PHASE_3F_FC2C_IMPLEMENTATION_REPORT.md`(이 문서), `docs/reports/PHASE_3F_FC2C_VERIFY_LOG.md`, `build/verify-archive/PHASE_3F_FC2C_run1/`(Git 무시) |
| 수정(상태) | `CURRENT_STATE.md`, `docs/game-engine.md` §38, `Star/01_Phase_Reviews/Phase_03_Review.md`(부록 추가), `Star/00_Dashboard.md`(한 줄) |

최종 SHA-256: `Dominance.cs` `6553988aa5e60c7b4a3a688a2b328cfdafa4509bf13815fe5ed39531c35d3893`, `DominanceTests.cs` `192f7ee35992f8a01b775eb89108a053cf0eb60e65e6335369a551573f7913e4` (검증 실행 시점과 동일).

ADR-008·ADR-009·ROADMAP·SYSTEM_CONTRACTS는 수정하지 않았다.

## L. Git 상태와 보호 파일

- HEAD `f2e6eb396e445cea40d7e531052e4cda756a7ca8` 불변, Branch `main`. Staging·Commit·Push·Reset·Clean·Rebase·Amend 없음.
- 보호 파일 SHA-256(시작·종료 동일, 쓰기 없음): `TASK.md` `8a84e0e9c47605f5f5bcc733c08c05f20fa3eed76c5e6d1b2dd0ce75879c479e`, `docs/2023recordRule.pdf` `9742caaf3a596b03d37fc6c57b9f866e989214e074aa5af1a2ab821ec5f212b0`, `docs/2026-official-baseball-rules.pdf` `f85860f3b754dfb5e07269e9d3a86c77c89b856376f6e3ba8345af36db997859`.
- 상태 유지: Official Execution `NOT_READY`, Final Execution Freeze `PENDING`, Phase 3-F Gate `INCOMPLETE`, Phase 3 Gate `INCOMPLETE`. F-C2-C는 `REVIEW_PENDING`.

## M. 수정 Addendum — Codex 독립 리뷰 FC2C-01 / FC2C-02 (2026-10-18, FIX1)

독립 리뷰(Codex High)가 F-C2-C에서 2건을 지적했고(MAJOR 1 · MINOR 1), 두 결함만 최소 범위로 수정했다. 기준 HEAD `f2e6eb3` 불변. 아래는 Claude Code의 구현·자체 시험 기록이며 **재검토 결과가 아니다**(독립 재검토 `미실시`; 두 건은 "해결 후보").

### M-1. FC2C-01 (MAJOR) — Dominance 셀의 Metrics 계산 실패 누락

- **원인:** `DominanceEvaluator.Cell`이 완료 경기의 1~8회 관측(`Regulation`) 오류만 `MetricsFailures`로 셌고, 실행 단위 `run.MetricsComputed`(= `Metrics.IsSuccess`)를 보지 않았다. 따라서 신뢰 가능한 셀 실행에서 `Metrics.counts_inconsistent` 같은 계산 오류가 나도 셀은 `Ok`, 표본이 있으면 통계에 포함되고, 미동결 상태에서는 `NOT_EVALUATED`(FC-D4.7)로만 보여 FC-D9.1의 FAIL이 사라졌다. (`run-result-v1` 해시는 Metrics를 포함하지 않으므로 무결성 검사로도 걸리지 않는다.)
- **수정(`Dominance.cs` `Cell` 한 곳):** `run.MetricsComputed == false`이면 `MetricsFailures`에 1을 더하고 사유에 `지표 계산 실패(<오류 코드>)`를 남긴다. 상태 우선순위(`Incomplete` > `EngineFailure` > `MetricsFailure` > `InsufficientSample` > `Ok`)와 `Gate` 결합 순서(FAIL > NOT_EVALUATED > INSUFFICIENT_SAMPLE > 측별 판정)는 그대로다. 이미 있는 `failing` 규칙이 이 셀을 잡으므로 표본 수 미동결·표본 부족·다른 셀 누락보다 FAIL이 우선하고, 통계는 계산되지 않는다. 무결성 위반 셀은 종전대로 증거로 쓰지 않는다(Gate FAIL로 승격하지 않음).
- **구분:** "계산 성공 + 개별 지표 `NOT_EVALUATED`"(미승인 정책의 정상 상태)는 `MetricsComputed == true`이므로 실패로 세지 않는다(시험으로 고정). 기존 1~8회 관측 실패의 사유 문구·카운트는 그대로다.
- **ADR 범위:** ADR-008 FC-D9.1 적용. ADR-009 SR-D9.2(calibration 층 지표 오류 = `NOT_EVALUATED`, 실패 경기가 있으면 FAIL)는 코드·정책 모두 변경하지 않았다.
- **재현 시험(수정 전 실패 확인 후 수정):** `A_trusted_cell_run_whose_metric_computation_failed_is_a_metrics_failure_and_fails_the_gate`(정상 셀의 Metrics만 Failure로 교체 → `MetricsFailure`·Gate FAIL·오류 코드 보존·나머지 47셀 Ok·Replay 불일치 검출), `A_metric_computation_failure_takes_precedence_over_an_unfrozen_or_short_sample_and_a_missing_cell`(미동결·표본 부족·1경기 셀·누락 셀과 함께여도 FAIL), `A_working_metric_computation_with_not_evaluated_diagnostics_is_not_a_metrics_failure`(정상 `NOT_EVALUATED` 오판 방지), `A_metric_computation_failure_does_not_displace_game_failures_integrity_or_incomplete_cells`(경기 실패와 동시 → `EngineFailure`·둘 다 계수, 무결성 위반 유지, `Incomplete` 유지, Seed 불일치 거부 유지). 수정 전 4개 중 3개 실패(나머지 1개는 오판 방지 보호 시험이라 수정 전에도 통과), 수정 후 전부 통과.

### M-2. FC2C-02 (MINOR) — 셀 카탈로그의 변경 가능한 내부 컬렉션

- **원인:** `Cells`는 `List<DominanceCell>`, `ByCompetitionKey`는 `Dictionary`를 인터페이스 형으로만 노출해, `(IList<DominanceCell>)DominanceMatrix.Cells`·`(IDictionary<…>)ByCompetitionKey`로 승인된 셀 정의를 바꿀 수 있었다.
- **수정(`Dominance.cs` 선언 2줄):** `Cells = new ReadOnlyCollection<DominanceCell>(Build())`(생성한 `List`는 외부에 노출되지 않음), `ByCompetitionKey = Cells.ToFrozenDictionary(...)`(`System.Collections.Frozen`, 같은 `Cells`에서 한 번만 생성 — 목록과 조회가 같은 인스턴스를 가리킴). 형변환으로 얻은 `IList`/`ICollection`/`IDictionary`는 모든 변경에 `NotSupportedException`을 던진다. `DominanceCell`은 init 전용 record라 요소 자체도 불변이다. 새 패키지·새 타입 없음(표준 .NET 8).
- **시험:** `The_cell_catalogue_and_its_key_lookup_cannot_be_changed_by_a_caller` — 런타임 형식이 `List`/배열/`Dictionary`가 아님, `IList` 요소 교체·Add·Insert·Remove·RemoveAt·Clear, 비제네릭 `IList`, `IDictionary` 추가·교체·삭제·Clear 모두 거부, 시도 뒤에도 48셀·번호 1..48·ID 순서·키 집합 동일, 모든 셀이 조회 결과와 같은 인스턴스. 수정 전 실패 확인.

### M-3. 기존 계약 유지

- 48셀 생성·§6.6 행 단위 대조(`The_matrix_is_exactly_the_48_cells_of_proposal_6_6_row_by_row`), Welch z·IUT·Holm·δ·α, 셀 무결성, 재실행 Replay, `ConfigHash`, `game-result-v1`, `run-result-v1/v2`, Attestation v1, 코호트 `InputsHash`, Legacy Fixture, F-C2-B Stop/Replay: 코드·단언 변경 없음, 전부 PASS. 기존 시험 단언은 약화·삭제하지 않았다(`DominanceTests.cs`에 시험 5개와 `using` 1줄만 추가).
- 정상 셀의 `Canonical()` 텍스트는 이전과 같다(`MetricsFailures`=0, 사유 `평가 가능`). 바뀌는 것은 Metrics 계산 실패 셀뿐이다.

### M-4. 빌드·전체 시험 (실제 실행)

| 명령 | 결과 |
|---|---|
| `dotnet test tests/StarPlayer.SimHarness.Tests -c Release --filter DominanceTests` (수정 전, 신규 시험만 추가한 상태) | 22 PASS / 4 FAIL (FC2C-01 3건·FC2C-02 1건 재현) |
| 같은 명령 (수정 후) | 26 PASS / 0 FAIL / 0 SKIP |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1 -LogName PHASE_3F_FC2C_FIX1_VERIFY_LOG.md` | **`Gate result: PASS`, exit 0** |

`verify.ps1`: Release 빌드 11개 프로젝트 경고 0; Domain 327 · Simulation 741 · Application 127 · Infrastructure 1,233 · **SimHarness 318**(313 + 5) = **2,746 PASS / 0 FAIL / 0 SKIP**; Godot headless 4종, Windows export, Exported program 3종, 문서 링크 152개 파일 PASS. 환경: Windows 11, .NET SDK 8.0.425, Godot 4.7.2 mono. 이전 2,741에서 +5는 신규 시험 5개 때문이다.

### M-5. 검증 로그·원시 증거·SHA-256

- 로그 `docs/reports/PHASE_3F_FC2C_FIX1_VERIFY_LOG.md` SHA-256 `af3e7c193dc7f2f0b5268a0adfe3d8e203681dedf886ab8b03912a2cea6c505a`.
- 원시 증거 `build/verify-archive/PHASE_3F_FC2C_FIX1_run1/`(28개 파일, `diff -rq build/verify` 동일, Git 무시·로컬); `verify-result.json` SHA-256 `a8f4912c7e007d67db84955e189d2fcaed607586fc9a36ff4fa5a1f446d7219a`.
- **기존 증거 보존:** 실행 전후 `build/verify-archive/` 기존 전체와 기존 `docs/reports/*VERIFY_LOG.md`·`PHASE_3F_FC2A_VERIFY_EVIDENCE.md`(합 181개 파일)의 SHA-256 동일. `PHASE_3F_FC2C_VERIFY_LOG.md`·`PHASE_3F_FC2C_run1/`도 그대로(이 보고서 §A~L 본문도 수정하지 않고 이 Addendum만 추가).
- 최종 SHA-256: `Dominance.cs` `6042f5e64ad2df594ca9e74f5f09d34ed329af546d6dcebd90066b4300bf0c3e`, `DominanceTests.cs` `aec12512385ba1e329a83a47dabeceb06e42a5c9f6e5b2edffdba4d7053f2902`(검증 실행 시점과 동일). 이 Addendum과 상태 문서는 `verify.ps1` 실행 뒤에 작성했다.

### M-6. 최종 Git 변경 Manifest (이번 FIX1 한정)

| 구분 | 파일 |
|---|---|
| 수정(개발) | `tools/StarPlayer.SimHarness/Dominance.cs`(F-C2-C 미커밋 신규 파일; 선언 2줄 + `Cell` 내 지표 계산 판정) |
| 수정(시험) | `tests/StarPlayer.SimHarness.Tests/DominanceTests.cs`(F-C2-C 미커밋 신규 파일; 시험 5개 + `using` 1줄) |
| 신규(기록) | `docs/reports/PHASE_3F_FC2C_FIX1_VERIFY_LOG.md`, `build/verify-archive/PHASE_3F_FC2C_FIX1_run1/`(Git 무시) |
| 수정(문서) | 이 보고서(§M 추가), `CURRENT_STATE.md`(단락 1개 추가), `Star/00_Dashboard.md`(한 줄), `Star/01_Phase_Reviews/Phase_03_Review.md`(부록 추가) |

`git status`는 F-C2-C 구현 시점과 같은 파일 집합이다(신규 파일 없음 — FIX1 로그만 추가). ADR-008·ADR-009·ROADMAP·SYSTEM_CONTRACTS·엔진·RNG/CAS·Hash 계약은 수정하지 않았다.

### M-7. 보호 파일·Git

- HEAD `f2e6eb396e445cea40d7e531052e4cda756a7ca8` 불변, Branch `main`. Staging·Commit·Push·Reset·Clean·Rebase·Amend 없음(`git diff --cached` 비어 있음).
- 보호 파일 SHA-256(시작·종료 동일, 쓰기 없음): `TASK.md` `01b8c54c428fb7197e5c259bfec54efc56ac311d6d00a76aab2dee6c0fe108d8`(이번 FIX 지시서), `docs/2023recordRule.pdf` `9742caaf3a596b03d37fc6c57b9f866e989214e074aa5af1a2ab821ec5f212b0`, `docs/2026-official-baseball-rules.pdf` `f85860f3b754dfb5e07269e9d3a86c77c89b856376f6e3ba8345af36db997859`.

### M-8. 후속 공식 통합 작업 (이번 FIX에서 구현하지 않음)

이 FIX는 두 결함의 수정과 회귀 검증뿐이다. 아래는 Codex가 확인한 **F-C2 전체 완료 이전의 미완료 책임**이며, F-C3 실행의 선결 조건이다. 공식 CLI·Preflight·Replay 연결은 F-C3로 자동 이관하지 않는다(ADR-008, F-C1 제안서 §14~§15 기준으로 별도 후속 작업으로 지시·검토받아야 한다).

1. 공식 Seed Manifest(48개 `fc3-dom`)와 외부 SHA Pin의 결속 — 현재 `SeedManifestPins.Official`은 비어 있다.
2. 공식 Preflight/Replay에 `DominanceMatrix.CellOf`·`DominanceReplay` 연결(보고서 §J-5).
3. 48셀 실행용 CLI.
4. 공식 결과 저장 형식(FC-D11 증거 보존; 새 해시가 필요하면 별도 승인).
5. PZL 잔여 검사(#5·#6·#10·#12a)와 `fc2-dry` Dry Pilot.
6. Final Execution Freeze(셀당 표본 수 포함, FC-D4.7).

주의: 셀 단위 재현성(`DominanceReplay`)은 **기록된 셀 실행을 셀 정의로 다시 실행해 비교**할 뿐, 외부 승인 Seed 계획의 진위를 검증하지 않는다. 그 진위는 위 1·2의 외부 Pin 결속이 있어야 확인된다.

### M-9. 상태

FC2C-01: 해결 후보 · FC2C-02: 해결 후보 · F-C2-C: 독립 재검토 대기(`REVIEW_PENDING`) · Official Execution: `NOT_READY` · Final Execution Freeze: `PENDING` · Phase 3-F / Phase 3 Gate: `INCOMPLETE`.

## N. Closeout 준비 (2026-10-18, 문서만)

- **Codex High 재검토:** FC2C-01·FC2C-02 해결 확인, 신규 BLOCKER/MAJOR/MINOR 0. 문서 링크 345개를 Codex가 읽기 전용으로 확인(미해결 경로 0). 이 단계에서 빌드·시험·Verify는 재실행하지 않았다(2,746 PASS / 0 FAIL / 0 SKIP, `GATE: PASS`는 FIX1 시점 결과).
- **Commit 후보 16개:** 수정 11(`CURRENT_STATE.md`, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `docs/game-engine.md`, `CodexFindingsTests.cs`, `OfficialAndReplayTests.cs`, `Cohorts.cs`, `GameExecutor.cs`, `Official.cs`, `Replay.cs`, `RunModel.cs`) + 신규 5(본 보고서, `…_VERIFY_LOG.md`, `…_FIX1_VERIFY_LOG.md`, `DominanceTests.cs`, `Dominance.cs`). 제외: 보호 파일 `TASK.md`, `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf`. 로컬 Closeout Commit 미실시, 스테이징 없음, 기준 HEAD `f2e6eb3` 불변.
- **범위:** F-C2-C 라이브러리 Closeout 준비일 뿐 F-C2 공식 통합 완료가 아니다. §M-8의 1~6(Seed Pin, Preflight·Replay 연결, CLI, 결과 형식, PZL 잔여·Dry Pilot, Final Execution Freeze)은 그대로 미완료다. 셀당 표본 수 미동결의 `NOT_EVALUATED`는 공식 통계 결과가 아니다.
- **상태:** F-C2-C 라이브러리 `READY_FOR_OWNER_CLOSEOUT_REVIEW`(승인 아님) · Official Execution `NOT_READY` · Final Execution Freeze `PENDING` · Phase 3-F / Phase 3 Gate `INCOMPLETE`.
