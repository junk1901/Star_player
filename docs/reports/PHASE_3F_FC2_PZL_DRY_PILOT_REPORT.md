# Star Player Phase 3-F F-C2 PZL 잔여 검사 · 비공식 Dry Pilot 보고서

- 상태: **`IMPLEMENTED_WITH_NOTES` · `TESTED` · `REVIEW_PENDING`** (2026-10-18, 소유자 `TASK.md` "Phase 3-F / F-C2 PZL & Dry Pilot"; 독립 리뷰 `미실시`).
- 기준: Branch `main`, HEAD `5d7264a5e26a6378f0fd899ab35529ae50cde5b6`(F-C2 Official Integration DRAFT 로컬 Closeout Commit) 불변. Staging·Commit·Push·Reset·Clean·Rebase·Amend 없음.
- **Official Execution `NOT_READY` 유지 · Final Execution Freeze `PENDING` · 실제 공식 실행 미실시 · F-C2 전체(B-6 포함) 미완료 · Phase 3-F / Phase 3 Gate `INCOMPLETE`.**
- 정책 문서 [ADR-008](../decisions/ADR-008.md)·[ADR-009](../decisions/ADR-009.md) 본문, `ROADMAP.md`, `docs/SYSTEM_CONTRACTS.md`는 수정하지 않았다. 공식 Seed·Pin·표본 수·스키마·Hash를 만들거나 확정하지 않았다.
- Dry Pilot 결과는 전부 **비공식 진단 증거**(ADR-009 SR-D4.3)이며 Official Evidence가 아니다.
- **2026-10-19 Closeout 준비 Addendum(§11):** Codex High 독립 리뷰 BLOCKER 0 / MAJOR 0(소유자 전달, READ-ONLY, 전체 시험 미재실행)을 반영해 상태를 `READY_FOR_OWNER_CLOSEOUT_REVIEW`로 올렸다. 위의 `REVIEW_PENDING`·`독립 리뷰 미실시`는 작성 당시 기록으로 보존하며 §11이 최신이다. 로컬 Closeout Commit 미실시, 문서만 갱신(코드·시험 변경·재실행 없음).

## 1. 원본 계약 확인

### 1-1. 근거 문서

| 항목 | 원본 위치 | 권위 |
|---|---|---|
| PZL 12개 절 문구 | `ROADMAP.md` 3-F Acceptance Criteria (제안서 §10 표의 "ROADMAP 원문") | 원문 |
| PZL 판정 정책 | [ADR-008](../decisions/ADR-008.md) FC-D8(1~4), Consequences("PZL #5, #6, #10 and #12a checks") | 정규 계약 |
| 절별 F-C 추가 검사·PASS 조건 | [제안서](PHASE_3F_FC1_POLICY_PROPOSAL.md) §10 표 #5·#6·#10·#12, §10.1(12a/12b 분리), §15 "PZL 보강" 행 | ADR-008이 승인한 권고안의 도출·해설 |
| Dry run 허용 범위 | ADR-008 Consequences("a `fc2-dry` dry run on non-official seeds"), N-1, N-2, FC-D4.7, FC-D5.4; [ADR-009](../decisions/ADR-009.md) SR-D4, SR-D5, SR-D8.6 | 정규 계약 |
| Dry run 규모·Seed 후보 | 제안서 §6.5(σ_g 파일럿 규칙), §7.2(fc2-dry 행), §8.1(batch means DEFF) | **후보(미승인)** — ADR-008 "a proposal value that this ADR does not list as approved stays a candidate" |

보호된 공식 야구 규칙 PDF는 이번 검사에 필요하지 않아 읽지 않았다(야구 규칙·좌표·확률·경계값을 새로 정하지 않음).

### 1-2. PZL 4개 항목 정의와 대응

| # | ROADMAP 원문 | 검증 목적 | 입력·사전 조건 | 기대 결과(제안서 §10 PASS 조건) | 기존 구현·테스트 | 기존 미검증 경로 | 완료를 증명하는 테스트(이번) |
|---|---|---|---|---|---|---|---|
| 5 | "new `ef2` events are identified and collision-checked" | 위치 있는 사건이 `ef2`로 식별되고 EventId 충돌이 없음 | 실제 엔진 실행의 확정 사건 | **공식 실행 전체** 충돌 0, 위치 있는 사건 모두 `ef2`, 없는 사건 모두 `ef1` | `EventFingerprint.VersionOf`, `AuthoritativeGame`; `EventJsonV2Tests.Receipts_of_both_fingerprint_versions…`, `AuthoritativeGameTests.A_bad_fingerprint_version…`, `GameIdentityTests.Different_parts_never_collide` | 실행 수준 전체 EventId 유일성·동일 Id 다른 fingerprint 0 검사(제안서: `ADDITIONAL_CHECK_REQUIRED`) | `PzlCheckTests.Pzl5_*` 3개 |
| 6 | "legacy and new JSON formats both replay" | ef2 JSON 왕복 후 Replay가 같은 receipt·최종 상태를 냄 | Harness는 Infrastructure를 참조할 수 없음(경계) → Infrastructure.Tests에서 **사전 선언한 공식 GameId 표본**(예: `fc3-int` 각 모드 앞 10경기) | 표본 전부 같은 receipt·최종 상태, legacy 코퍼스 replay 유지 | `ScriptedGameJson` format 2 / legacy; `EventJsonV2Tests`, `ReplayContractTests`, `Ef1CorpusPinTests` | 실제 엔진 경기 표본의 ef2 왕복 Replay(제안서: `ADDITIONAL_CHECK_REQUIRED`) | `PzlJsonRoundTripReplayTests` 3개(Theory 2 + Fact 1) |
| 10 | "ABS mode gives zero Framing effect" | Automated(ABS)에서 Framing 영향 0 | 실제 엔진 실행의 확정 투구와 그 called_pitch 계산 흔적 | 기존 테스트 PASS + **Automated 층 framing 비중립 0**(계수 1,000,000) | `GameRules` CalledPitchMode, `FrozenCatcherInput.FramingRequired`, `CommitEvidence.LinkCalledPitch`; `GameRulesCalledPitchModeTests`, `FramingInputTests` | 실행 수준 확인(제안서: `EXISTING_EVIDENCE` + `ADDITIONAL_CHECK_REQUIRED`) | `PzlCheckTests.Pzl10_*` 2개 |
| 12a | "gameplay RNG is not consumed by BCL/LMV" — 12a 현재 경계 보존 | 등록 Stream이 `game/*`뿐이고 presentation Stream이 없으며 Harness 결과 Hash가 presentation과 무관 | `RngStreamId.cs` 등록 집합, 실제 실행의 소비 Stream | 기존 테스트 + F-C2 추가("등록 Stream 집합 고정", "presentation namespace Stream 없음") PASS, F-C3 SHA에서 재통과 | `SimulationDeterminismTests`, `ArchitectureTests` | 등록 집합 고정·presentation 부재의 명시 검사 | `PzlCheckTests.Pzl12a_*` 3개 |
| 12b | 같은 절 — 실제 BCL/LMV 기능 | — | 기능 미구현 | **`DEFERRED`**(BCL Phase 5·6·28, LMV-A 5·28). Phase 3에서 PASS로 기록하지 않음(FC-D8.2) | — | — | 항상 `DEFERRED`임을 단언 |

정책 공백(판정 기준 신설 없이 보고만): 아래 §5 P-1~P-6.

## 2. 구현

엔진·Model·Tuning·RNG·Commit·CAS·판정 책임 변경 없음. 확정된(`GameCursor.Publish`가 돌려준) 사건만 관찰하며, 관찰 결과는 **어떤 Hash에도 들어가지 않는다**(`game-result-v1`·`run-result-v1/v2`·ConfigHash·Attestation v1·receipt 불변).

| 파일 | 변경 |
|---|---|
| `tools/StarPlayer.SimHarness/PzlChecks.cs` (신규) | `PzlRecorder`(경기당 관찰), `PzlGameObservation`(스칼라 + 정규 텍스트), `PzlChecks.Evaluate`(실행 수준 판정) |
| `tools/StarPlayer.SimHarness/GameExecutor.cs` | 매 확정 commit과 그 투구 준비(`PreparedPitch`)를 `PzlRecorder`에 전달, 경기 종료 시 최종 receipt 로그와 RNG 소비 Stream으로 마감. `Play`에 읽기 전용 관찰 seam(`observe`, #6 시험용) 추가 |
| `tools/StarPlayer.SimHarness/RunModel.cs` | `GameRunResult.Pzl`(init, Hash 밖 — 기존 `Regulation`과 같은 방식) |
| `tools/StarPlayer.SimHarness/RunReport.cs` | `summary.json`에 `pzl_checks`, 정지 인식 실행에 `regulation_runs_diagnostic`(σ_g 측정값), `summary.txt`에 PZL 한 줄 |
| `tools/StarPlayer.SimHarness/Program.cs` | PZL `FAIL`을 stderr에도 출력(`PZL CHECK FAIL …`). Exit code 의미 불변(Exit 0은 Gate 판정이 아님, SR-D9) |
| `tools/StarPlayer.SimHarness/README.md` | 새 출력 설명 |

판정 규칙(원문의 "0" 외 임계값 없음):

- **#5** — 각 확정 사건마다: receipt를 `EventFingerprint.Receipt`로 재계산해 일치, 버전이 "위치 있음 ⇔ `ef2`", EventId가 그 경기의 정규 ID(`GameEventIds.ParseSequence`)이고 commit 순번과 같음, 같은 EventId 재등장 시 같은 fingerprint는 중복·다른 fingerprint는 충돌로 계수, 최종 `AppliedEvents`가 commit receipt 목록과 정확히 같음. 실행 수준: 위 계수 모두 0 **그리고** 실행 안 GameId 고유. **실행 전체 EventId 유일성**은 (경기 안 유일) ∧ (모든 EventId가 자기 GameId의 정규형) ∧ (GameId 고유)로 정확히 성립한다(GameId 구성 요소에 ':'가 없어 `{GameId}:e{seq:D6}`가 단사). 사건이 0개면 `NOT_EVALUATED`.
- **#10** — 확정 투구 계산 흔적의 모든 `catcher.framing` 항목과 그 `FactorPpm`. Automated 실행: 1,000,000이 아닌 계수가 하나라도 있으면 `FAIL`, called_pitch 단계가 0이면 `NOT_EVALUATED`(공허한 PASS 방지), 그 외 `PASS`. 다른 모드는 `NOT_APPLICABLE`(수치는 진단으로 보고).
- **#12a** — 경기 최종 RNG 상태와 확정 draft가 소비한 모든 Stream이 고정 집합(`game/ai@1, game/contact@1, game/field@1, game/location@1, game/pitch@1, game/runner@1, game/target@1`) 안. 고정 집합 = Simulation 등록 집합임은 시험이 Reflection으로 확인한다(Harness 소스는 RNG 형식 이름을 쓰지 않는다는 기존 경계 시험 `EndToEndAndBoundaryTests`를 지키기 위해). 12b는 항상 `DEFERRED`.
- **실행 수준 결과 ≠ 공식 절 PASS.** `pzl_checks`는 "이 실행 하나"의 결과이며 공식 절 PASS는 F-C3 공식 실행 증거가 필요하다(FC-D8.4, FC-D12(12)). 48셀 공식 경로의 셀 `summary.json`에도 같은 항목이 생기지만 Hash·색인·Attestation에는 들어가지 않는다(B-1 참고).

## 3. 시험 · 수정 전후 재현(Sabotage)

### 3-1. 신규 시험 13개

| 시험 | 내용 |
|---|---|
| `Pzl5_every_committed_event_of_real_games_is_identified_by_its_location_and_no_event_id_collides` | 실제 엔진(virtual-league, 두 모드) — 사건 수 = CommittedEvents, located = LocatedPitches = ef2, ef1 > 0, 위반 계수 7종 0 |
| `Pzl5_the_recorder_counts_…` | 손으로 만든 위반: ef1로 표시된 위치 사건, ef2로 표시된 비위치 사건, 위조 fingerprint, 다른 경기 Id, 순번 불일치, 중복, 충돌, 최종 로그 누락·교체 — 각각 정확히 계수 |
| `Pzl5_any_violation_or_a_repeated_game_id_fails_…` | 위반 6종·중복 GameId → `FAIL`, 사건 0 → `NOT_EVALUATED` |
| `Pzl10_an_automated_run_has_no_non_neutral_framing_factor_while_the_non_automated_control_shows_the_counter_working` | Automated: virtual-league와 **시험 전용 Framing 95 코호트** 모두 PASS·Framing 입력/항목 0. NonAutomated 공식 코호트(Framing 50): 항목은 있으나 비중립 0. NonAutomated Framing 95(음성 대조): 비중립 > 0 |
| `Pzl10_a_non_neutral_factor_fails_…` | 주입 비중립 → `FAIL`, called_pitch 0 → `NOT_EVALUATED`, 비ABS 주입 → `NOT_APPLICABLE` |
| `Pzl12a_the_registered_streams_are_exactly_the_pinned_game_set_…` | Reflection 등록 집합 = 고정 7개, 모두 `game/`, presentation/bcl/lmv 없음; 실제 실행 소비 Stream ⊆ 고정 집합, target·location·pitch 포함 |
| `Pzl12a_a_presentation_stream_…_and_12b_is_always_deferred_never_pass` | presentation Stream 허용 집합·소비, 버전 다른 Stream, 고정 집합 축소 → `FAIL`; 소비 0 → `NOT_EVALUATED`; 12b `DEFERRED` |
| `Pzl12a_the_harness_and_its_result_hashes_depend_on_no_presentation_or_infrastructure_assembly` | Harness 참조 어셈블리 = Domain·Simulation뿐; 재실행 시 모든 Hash와 관찰 동일 |
| `The_summary_carries_every_check_…` | `summary.json`·`summary.txt` 출력 형식 |
| `A_dry_run_summary_reports_sigma_g_…` | σ_g 진단값 = `CellSample` 재계산값, 표본 수 미도출, dry Gate `NOT_EVALUATED` |
| `PzlJsonRoundTripReplayTests.Ten_engine_games_of_each_mode_…` ×2 | 실제 엔진 20경기(각 모드 10) → ef2 JSON 쓰기·읽기 → 사건·위치·receipt 동일, 재쓰기 바이트 동일 → `GameReducer` Replay(+`Finalize`) → 원본·왕복 상태(receipt, Stats, 점수, 이닝, 라인, Final) 동일, Harness `event_hash`·점수·이닝 동일, receipt JSON 왕복 동일 |
| `PzlJsonRoundTripReplayTests.A_located_engine_game_cannot_be_written_in_the_legacy_format_…` | 위치 있는 실제 경기는 legacy 쓰기 거부(`Fixture.location_requires_v2`), v2 JSON은 legacy 읽기 거부 |

시험 개발 중 실패(보존 필요 없는 작업 중간 결과, 사실만 기록): #6 Replay에서 `Finalize`를 호출하지 않아 `Final`이 null → CommitGate와 같은 방식으로 `GameReducer.Finalize` 호출해 해결. #10 음성 대조를 공식 코호트로 잡았으나 모든 공식 코호트 포수 Framing이 50이라 비중립이 생기지 않음 → 시험 전용 Framing 95 코호트로 교체(§5 P-3).

### 3-2. 표적 Sabotage (원복 후 SHA-256 동일 확인)

최종 코드 기준 `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_sabotage_final/` (SUMMARY SHA-256 `d989103aac9dd2ff91e472b9a219c36890b9461d91fe28c414f531bb3ab0027f`):

| # | 변형 | 결과 |
|---|---|---|
| S1 | #5 버전 불일치 계수 제거 | 검출 |
| S2 | #5 중복 GameId 무시 | 검출 |
| S3 | #5 receipt 재계산 제거 | 검출 |
| S4 | #10 Framing 계수 미판독 | 검출(음성 대조) |
| S5 | #12a 고정 집합 밖 소비 무시 | 검출 |
| S6 | #12a 고정 집합에 `presentation/bcl@1` 추가 | 검출(3개 시험) |
| S7 | #6 Reader가 투구 spec의 주자 이동을 버림 | **표본에서 생존**(엔진 표본에 해당 값이 없음 = 표본상 동등) → 넓은 JSON·Replay 묶음에서 21개 실패로 검출(S7w) |
| S8 | #6 Reader가 인플레이 `BatterSequence`를 버림 | **표본에서 생존**(표본 값 모두 null) → S8w 17개 실패로 검출 |
| S9 | #6 Reader가 인플레이 주자 이동을 버림 | 검출 |
| S10 | #6 Reader가 타자 측을 Right로 고정 | **표본에서 생존**(virtual-league 타자 전원 우타) → S10w 2개 실패로 검출 |

S7·S8·S10 생존은 #6 엔진 표본이 해당 필드를 만들지 않기 때문이며(검사 누락이 아니라 표본 범위), 기존 손 작성 JSON 시험이 그 필드를 덮는다. 이전 시도 `…_sabotage_try1`(콘솔 인코딩 깨짐), `…_sabotage_try2`·`…_sabotage`(S1·S3이 시험이 아니라 컴파일 경고-오류로 "검출"됨, 또 이후 경계 수정 전 코드), `…_sabotage_final_try1_partial`(S5 패턴 불일치로 중단)은 별도 보존했다.

## 4. 비공식 Dry Pilot

### 4-1. 범위 구별

| 구분 | 이번 처리 |
|---|---|
| `fc2-dry` 비공식 진단 실행 | **수행** — 기존 `dry` 명령(ADR-009 SR-D4, 정지 인식, Envelope 없음, Gate 항상 `NOT_EVALUATED`)과 `replay --seed-manifest --seed-sha256`(운영자 Pin, SR-D8.6) |
| F-C2 공식 통합의 합성 Fixture 테스트 | 기존 시험 재사용(`DominanceOfficialTests*`, `StopReplayTests`, `OfficialAndReplayTests` 등 — 시험 전용 TEMP Pin·시험용 승인값). 이번에 새 승인 객체를 만들지 않음 |
| `fc3-cal`·`fc3-int`·`fc3-dom` 공식 실행 | **미실시** — 현재 승인값(`OfficialApprovals.Current`)으로 공식 명령이 거부됨을 확인만 함 |
| Final Freeze 이후 Official Evidence | 해당 없음(Freeze 없음) |

**`fc2-dry`는 48셀 통합 검증과 같지 않다.** Dry Pilot의 행렬 부분은 48셀 중 4셀의 비공식 σ_g 측정이며, 48셀 공식 Preflight·실행·색인·Replay를 대신하지 않는다. `fc3-int` 경로는 존재하지 않는다(B-6).

### 4-2. 규모·Seed 선택 (진단용, 동결 아님)

ADR-008은 Dry run의 규모·통과 기준을 승인하지 않았다(규모·σ_g 규칙은 Final Execution Freeze 후보). 그래서 유일한 명세 수치인 **제안서 §7.2 후보**를 그대로 진단 규모로 썼고, 통과 기준은 정하지 않았다(Dry 결과는 판정이 아님).

| Seed 파일(Competition Key) | 코호트 / 정책 / 규칙 | Seed | 경기 | 블록 | pa_target | 목적 |
|---|---|---|---:|---:|---:|---|
| `fc2-dry-cal-na` | neutral-mixed-v1 / normal-balanced-v1 / automated | 900001–900050 | 50 | 10 | 99,999 | calibration 파일럿(§7.2 "층당 50, 전부 실행") |
| `fc2-dry-cal-nn` | neutral-mixed-v1 / … / non-automated | 900051–900100 | 50 | 10 | 99,999 | 〃 |
| `fc2-dry-cal-va` | virtual-league-v1 / … / automated | 900101–900150 | 50 | 10 | 99,999 | 〃 |
| `fc2-dry-cal-vn` | virtual-league-v1 / … / non-automated | 900151–900200 | 50 | 10 | 99,999 | 〃 |
| `fc2-dry-dom-sa-nb` / `-ak` / `-pc` / `-tb` | neutral-v1 / `dom-normal-balanced-v1`·`dom-aggressive-attack-v1`·`dom-patient-careful-v1`·`dom-takefirst-balanced-v1` / automated | 910001–910800(셀당 200) | 200×4 | 50 | 99,999 | 행렬 파일럿(§7.2 "NB, AK, PC, TB; same/Auto 등", 셀당 200) — same/Automated로 사전 지정 |
| `fc2-dry-stop-na` | neutral-mixed-v1 / … / automated | 900001–900050 | 50 | 10 | 1,500 | 작은 목표의 `TARGET_REACHED` end-to-end(SR-D4.1) |
| `fc2-dry-abort-va` | virtual-league-v1 / … / automated | 920001–920400 | 400 | 10 | 99,999 | 실행 중 Seed 파일 변경 → 무결성 중단 |

- `pa_target` 99,999(`DryMaxPa`, 공식 하한 100,000 미만)는 "전부 실행"을 정지 인식 실행으로 표현하기 위한 값이다(모든 실행이 목표 전 `SEEDS_EXHAUSTED`).
- Seed 파일은 Git 무시 경로 `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_dry_run1/seeds/`에만 있고 공식 Pin 표·승인값에 넣지 않았다. 파일별 SHA-256은 `pilot.json`에 기록.

### 4-3. 실행 방법

- 바이너리: Release `tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.dll` SHA-256 `714af674cbf96669564bbfd7ac74a84b80494a5b04033fa970d98d9b5db03d6b`(최종 소스 빌드; Verify run2 이후에도 같은 값).
- 명령 형식(각 Seed 파일마다): `dotnet …SimHarness.dll dry --seed-manifest <rel> --seed-sha256 <sha> --run-id <key>-a --out <dir>-a`, 같은 명령을 `-b`로 한 번 더(별도 프로세스 결정성), `replay --manifest <dir>-a/manifest.json --seed-manifest <rel> --seed-sha256 <sha>`. 50개 명령 전체·Exit·stdout/stderr는 `logs/`와 `pilot.json`(SHA-256 `91e537b8a9ae72b1992bd5f643066b44d37dbe0f3b9f3b08b5d473083b21570d`)에 있다.
- 구동 스크립트는 세션 임시 경로에서 실행했으며 저장소에 넣지 않았다(명령은 위 로그로 재현 가능).

### 4-4. 결과 (최종 바이너리, `…_dry_run1`)

| 실행 | 상태 / 정지 | 경기 | 유효 PA | run_result_hash(앞 12) | a=b·교차 시도 | Replay | PZL #5/#10/#12a/#12b | σ_g |
|---|---|---:|---:|---|---|---|---|---:|
| cal-na | COMPLETED / SEEDS_EXHAUSTED | 50 | 3,909 | `669f8dc9f884` | 같음 | MATCH | PASS/PASS/PASS/DEFERRED | 0.3429 |
| cal-nn | COMPLETED / SEEDS_EXHAUSTED | 50 | 3,788 | `400ffc581d2b` | 같음 | MATCH | PASS/N_A/PASS/DEFERRED | 0.2090 |
| cal-va | COMPLETED / SEEDS_EXHAUSTED | 50 | 3,808 | `fa268e705e92` | 같음 | MATCH | PASS/PASS/PASS/DEFERRED | 0.2239 |
| cal-vn | COMPLETED / SEEDS_EXHAUSTED | 50 | 3,743 | `8ad13f6f5929` | 같음 | MATCH | PASS/N_A/PASS/DEFERRED | 0.2530 |
| dom-sa-nb | COMPLETED / SEEDS_EXHAUSTED | 200 | 15,062 | `d83a37ea357e` | 같음 | MATCH | PASS/PASS/PASS/DEFERRED | 0.2398 |
| dom-sa-ak | COMPLETED / SEEDS_EXHAUSTED | 200 | 14,897 | `9a6d3f63159f` | 같음 | MATCH | PASS/PASS/PASS/DEFERRED | 0.2306 |
| dom-sa-pc | COMPLETED / SEEDS_EXHAUSTED | 200 | 15,360 | `77852d9d6fd7` | 같음 | MATCH | PASS/PASS/PASS/DEFERRED | 0.2282 |
| dom-sa-tb | COMPLETED / SEEDS_EXHAUSTED | 200 | 14,884 | `6e00ac59d364` | 같음 | MATCH | PASS/PASS/PASS/DEFERRED | 0.2248 |
| stop-na | COMPLETED / **TARGET_REACHED** (30/50, 블록 10) | 30 | 2,164 | `0ebd5c3660e1` | 같음 | MATCH | PASS/PASS/PASS/DEFERRED | 0.2217 |

- 총 1,030경기(진단 실행 a) × 2 + Replay 1,030경기; 실패·취소 경기 0, cross-check 불일치 0, `calibration_evaluation` Gate 전부 `NOT_EVALUATED`(fc2-dry 진단).
- **결정성:** 같은 Seed 파일의 두 실행(a/b)은 config·run_result Hash, 매니페스트(run_id·환경 제외), PZL·진단값이 모두 같다. 이전 소스로 실행한 `…_dry_try1`(구동 스크립트 출력 인코딩 오류로 음성 경로 전 중단)·`…_dry_try2`(완료)와도 9개 실행 Hash가 모두 같다 — PZL 관찰 추가·리팩터링이 결과 Hash를 바꾸지 않았음을 실행으로 확인.
- **#5 실행 수준:** 9개 실행 합계 사건 314,453 · 위치 사건 = `ef2` 236,855 · 위반 0. **#10:** Automated 7개 실행 비중립 0(Framing 항목 자체 0), NonAutomated 2개는 항목 10,802·비중립 0(포수 Framing 50). **#12a:** 소비 Stream 6개(`game/ai@1` 미사용), 고정 집합 밖 0.
- **σ_g (진단 측정값):** 행렬 4셀 최댓값 0.2398 (NB). 셀당 표본 수(제안서 공식·×1.2·50경기 올림)는 Final Execution Freeze 후보라 계산하지 않았다.
- **DEFF (N-2 진단):** 게임 군집 DEFF 범위 0.558–1.538. batch-means DEFF(20경기 묶음)는 50경기 층에서 묶음 2개뿐이라 값이 0.000–2.888로 크게 흔들리고, F1·F8(표준화)은 층별 묶음이 2개 미만이라 null이다. **`fc2-dry-cal-vn` F4의 batch-means DEFF 2.888 > 2** — N-2에 따라 자동 변경 없이 보고하며 소유자가 결정한다(묶음 2개라 통계적 정보는 매우 약함, §5 P-4).

### 4-5. 실패·거부 경로 (모두 기대대로, 실제 CLI)

| 경로 | 결과 |
|---|---|
| 없는 Seed 파일 | Exit 2 `Seed.file_missing` |
| 변조 Seed 파일 + 원래 Pin | Exit 2 `Seed.file_hash_mismatch` |
| 잘못된 SHA Pin(다른 파일의 SHA) | Exit 2 `Seed.file_hash_mismatch` |
| 형식이 틀린 Pin(대문자) | Exit 2 `Seed.pin_invalid` |
| 같은 출력 디렉터리(Run 충돌) | Exit 2 `Dry.output_exists`(기존 결과 불변) |
| dry에 공식 고정 옵션(`--seeds`) | Exit 2 `Cli.option_forbidden` |
| 불완전 결과(정지 기록 제거) | Exit 2 `Replay.result_version_downgrade` |
| 잘린 매니페스트 파일 | Exit 2(JSON 처리 불가) |
| 변조 run_result_hash | Exit 1 `REPLAY DIFFERS Replay.run_result_hash_mismatch` |
| 변조 경기 result_hash | Exit 1 `REPLAY DIFFERS Replay.result_games_mismatch` |
| 변조 plate_appearances | Exit 2 `Replay.plate_appearances_mismatch` |
| 계획 Seed 변경 / 계획 경기 삭제 | Exit 2 `Replay.seed_games_mismatch` |
| 공식 Key로 재라벨 | Exit 2 `Replay.official_envelope_missing` |
| Pin 없는 Replay | Exit 2 `Replay.stop_parameters_untrusted` |
| 다른 파일 Pin으로 Replay | Exit 2 `Replay.competition_key_mismatch` |
| 실행 중 Seed 파일 변경(무결성 중단) | Exit 2, `ABORTED_INTEGRITY` / `Seed.file_hash_mismatch`, 10/400경기 후 중단, 정지 기록·v2 Hash 없음; 그 Replay는 Exit 2 `Replay.run_not_terminated` |
| `official --seed-manifest <dry 파일>` | Exit 2, 게임 미실행(`Git.dirty` FAIL + `Official.git_sha_not_approved`·`seed_manifest_not_frozen`·`execution_policy_not_frozen`·`not_ready` 등 NOT_READY) |
| `official-dom` | Exit 2, 게임 미실행(`Official.dominance_seed_manifests_not_frozen`·`dominance_games_per_cell_not_frozen` 등) |
| `official-dom --out ../outside` | Exit 2, 위에 더해 `Official.output_invalid` |
| `replay-dom --result <dry 디렉터리>` | Exit 2 `DomOfficial.result_missing` |
| 실제 저장소 `build/simharness/official/` | 생성 0 |

- **평가 정보 변조:** `summary.json`의 Gate를 `PASS`로, PZL을 `FAIL`로 바꿔도 Replay는 **MATCH**다. `summary.json`은 Replay 입력도 무결성 보호 대상도 아닌 파생 데이터이며, 평가는 Replay 재실행에서 다시 계산해야 한다(dry 계약에 평가 결속 없음; 공식 48셀은 색인의 평가 비교로 검증 — 기존 계약). 이것은 거부 경로가 아니라 "평가 파일은 권위가 아님"을 확인한 것이다(§5 P-5).
- **실행 취소:** CLI에 취소 경로가 없어 기존 시험(`StopReplayTests.Cancellation_and_broken_integrity_end_the_run_without_a_stop_record`, `…outcome_table…`)으로 대체 — 이번 전체 시험에서 PASS.
- **승인되지 않은 출력 위치:** dry 명령에는 출력 위치 계약이 없다(공식 출력만 `build/simharness/official/` 아래). 공식 쪽 거부는 위 표와 기존 `PinnedDirectoryTests`·`DominanceOfficialTests.Fix1`로 확인.

### 4-6. BLOCKED

| 항목 | 이유 |
|---|---|
| #6 공식 GameId 표본 왕복 | 공식 Seed(예: `fc3-int` 앞 10경기)가 미동결이고 `fc3-int` 공식 경로가 없다(B-6). 비공식 대체 표본(`fc2-dry-cal-va/vn` 앞 10경기)으로만 수행 |
| 모든 PZL 절의 공식 PASS | F-C3 공식 실행 증거 필요(FC-D8.4) |
| 공식 Seed로 하는 Dry Pilot·σ_g 동결 | Final Execution Freeze 대상 |

## 5. 정책 공백 · 소유자 결정 필요 (판정 기준을 만들지 않고 보고)

각 항목: Issue / Reason / Recommended / Alternative / Impact.

- **P-1 #6 공식 표본 선언.** Reason: 제안서는 "사전 선언한 공식 GameId 표본(예: fc3-int 각 모드 앞 10경기)"이라고만 한다. Recommended: Final Execution Freeze에서 표본(Key·index 범위)을 명시하고, `fc3-int` 경로(B-6) 구현 뒤 같은 시험을 그 표본으로 재실행. Alternative: 표본을 calibration L3/L4 앞 경기로 지정. Impact: 그 전까지 #6은 비공식 대체 표본 증거뿐.
- **P-2 Dry Seed 범위가 `fc3-dom` 후보 범위와 겹침.** Reason: 제안서 §7.2의 dry 후보(900001~, 910001~)는 `fc3-dom` 셀 c의 후보 `600001 + 10000×(c−1)`에서 셀 31(900001~910000)·32(910001~920000) 범위 안이다(이번 무결성 중단 시험 920001~920400은 셀 33 범위). GameId가 달라 RNG는 다르지만 SR-D4.4 수치 분리는 미결(B-8). Recommended: Freeze에서 dry·공식 수치 범위를 분리 확정. Alternative: GameId 차이만으로 충분하다고 명시. Impact: 기록 위생 문제이며 통계 독립성에는 영향 없음.
- **P-3 공식 코호트의 #10 실행 증거는 구조상 자명.** Reason: 모든 공식 코호트(FC-D1/FC-D2)의 포수 Framing이 50이라 NonAutomated에서도 계수가 중립이다. Automated의 "비중립 0"은 Framing이 입력조차 되지 않음으로 성립한다. Recommended: 현 PASS 조건 유지(조건 자체는 충족) + 극단 Framing 시험 코호트(이번 단위 시험)를 보조 증거로 인정할지 결정. Impact: 없음(판정 변경 아님).
- **P-4 Dry Pilot 규모와 DEFF 진단력.** Reason: 후보 규모 50경기/층은 20경기 batch-means에 묶음 2개만 주며 F1·F8은 null. `cal-vn` F4에서 2.888 > 2가 관찰됨(N-2 보고 대상). Recommended: DEFF 진단이 필요하면 별도 승인된 dry 규모(예: 층당 묶음 수 기준)로 재측정, 그 전까지 N-2 판단 보류. Alternative: 게임 군집 DEFF(최대 1.538)를 참고. Impact: FC-D6 DEFF 2 가정의 경험적 확인은 아직 없음.
- **P-5 PZL·평가 결과의 공식 증거 형식.** Reason: FC-D11은 "PZL results"를 버전 관리 대상으로 정하지만 형식·Hash가 없다. 현재 `pzl_checks`는 Hash 밖 파생 데이터. Recommended: B-1(공식 결과 스키마)에서 함께 결정. Impact: 공식 PZL 결과를 F-C3 증거로 묶을 수 없음.
- **P-6 σ_g 사용.** 측정값만 보고했다. 파일럿 규칙(×1.2, 50경기 올림)과 셀당 n은 FC-D4.7/N-1 Freeze 대상이며 이번에 계산·동결하지 않았다.

## 6. 빌드 · 전체 시험 · Verify

| 명령 | 결과 |
|---|---|
| `dotnet build StarPlayer.sln -c Release` | 경고 0 · 오류 0 |
| `dotnet test tests/StarPlayer.SimHarness.Tests -c Release` | 353 PASS / 0 FAIL / 0 SKIP (343 + 신규 10) |
| `dotnet test … --filter PzlJsonRoundTripReplayTests` (Infrastructure) | 3 PASS |
| `verify.ps1 -LogName PHASE_3F_FC2_PZL_DRY_PILOT_VERIFY_LOG.md` **try1** | **`GATE: FAIL`** — SimHarness 352/1 FAIL: 기존 경계 시험 `The_harness_sources_compute_no_probability_outcome_or_random_number_and_read_no_test_helper`가 `PzlChecks.cs`의 `ProbabilityScale`·`GameRandom(Snapshot)`·`RngStream(Id)`·`GameRngStreams` 언급을 거부. **시험을 약화하지 않고** 코드를 고침(중립 계수 상수화, Stream은 정규 문자열로 전달, 등록 집합 Reflection을 시험으로 이동). [로그](PHASE_3F_FC2_PZL_DRY_PILOT_VERIFY_LOG_try1_FAIL.md) SHA-256 `fb1ea68a49abb0857b0b3ebafe6059deea8b65b5855711b5ef1552e95c3e398b`, 원시 `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_try1_FAIL/`(`verify-result.json` `44de1f0cb4e591b38b8522e9f1250a73c34f0a829e5bf9224d399cd00b6a1e6d`) |
| 같은 명령 **run2 (최종 트리)** | **`GATE: PASS`, exit 0** — Release 11개 경고 0; Domain 327 · Simulation 741 · Application 127 · Infrastructure 1,236 · SimHarness 353 = **2,784 PASS / 0 FAIL / 0 SKIP**(아키텍처 14·결정성 48은 위 묶음의 부분집합); Godot·Export·문서 링크(159개 파일) PASS. [로그](PHASE_3F_FC2_PZL_DRY_PILOT_VERIFY_LOG.md) SHA-256 `bd69e9e7ef03fcae07f0959b2a69fbfe3c3d512cd4ce2bb1776706835352a231`, 원시 `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_run1/`(29개 파일, `verify-result.json` `46c7aa024e08dc3162f5ba89f692295a7e35d9d561ab81aa841605c39dd3041b`) |

- 이전 기록 2,771 PASS는 `58692e4`/`5d7264a` 트리 기준이며, 이번 변경 후 실제 전체 수는 2,784(+13)이다.
- Dry Pilot `…_dry_run1`과 Sabotage `…_sabotage_final`은 run2와 같은 최종 소스로 수행했다(그 사이 소스 변경 없음; 이 보고서·상태 문서만 run2 뒤에 작성).
- 미실시: 독립 리뷰(Codex·GPT Chat), 전체 Mutation, 실제 공식 실행·공식 승인 입력 Replay(금지·자료 없음).

## 7. 증거 보존

| 경로(Git 무시 로컬 증거) | 내용 |
|---|---|
| `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_run1/` | Verify run2(최종) 원시 + `verify-stdout.txt` |
| `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_try1_FAIL/` | Verify try1 FAIL 원시 + `verify-stdout.txt` |
| `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_dry_run1/` | 최종 Dry Pilot: `seeds/`, `runs/`, `logs/`(50개 명령), `pilot.json` |
| `…_dry_try1/`, `…_dry_try2/` | 이전 소스 Dry 시도(try1 부분 — 구동 스크립트 인코딩 오류, try2 완료) |
| `…_sabotage_final/` | 최종 Sabotage 13건 |
| `…_sabotage_try1/`, `…_sabotage_try2/`, `…_sabotage/`, `…_sabotage_final_try1_partial/` | 이전 Sabotage 시도 |

- 기존 증거: 작업 시작 시 `docs/reports/`·`build/verify-archive/`의 **492개 파일 SHA-256을 기록했고 종료 시 모두 동일**(F-C2-A/B/C·Official Integration·FIX1 포함). Verify 전 `build/verify` = Official Integration FIX1 run2 Archive와 동일함을 `diff -rq`로 확인했다(Verify가 `build/verify`를 다시 쓰기 전).
- 실제 저장소 `build/simharness/`는 생성되지 않았다(dry 출력은 증거 디렉터리 아래로만).

## 8. 변경 파일 Manifest와 Git 상태

| 구분 | 파일 | SHA-256 (최종) |
|---|---|---|
| 신규(개발) | `tools/StarPlayer.SimHarness/PzlChecks.cs` | `929a51287c8de71120f7d2dd6dce0bf7b190188f9468d391d595bcf0b9d29151` |
| 수정(개발) | `tools/StarPlayer.SimHarness/GameExecutor.cs` | `87f1045d6f4d7768db296af8d2c88a891037b3c12ae86112eeb4422f16757656` |
| 수정(개발) | `tools/StarPlayer.SimHarness/RunModel.cs` | `cd7f02c1f1225c6d0a5cc1a5c7e106732225b4165ffd50630efc4217a74dcf53` |
| 수정(개발) | `tools/StarPlayer.SimHarness/RunReport.cs` | `1e507fc7ef7bae6cadd050b33653733974416025ec648732d68e778bc476f984` |
| 수정(개발) | `tools/StarPlayer.SimHarness/Program.cs` | `e5809175d76861360e40407d6a79aee9e069f498e83a0f6d1ca46690d22924fe` |
| 수정(문서) | `tools/StarPlayer.SimHarness/README.md` | `9b952e3a12522b6c98f363a0dc4049388d649af48c2cad863d9186836c9e6ddb` |
| 신규(시험) | `tests/StarPlayer.SimHarness.Tests/PzlCheckTests.cs` | `4901544d76c9861816d81e10a672d6b643d67709ee89ca98ecb4b1290b1e8584` |
| 신규(시험) | `tests/StarPlayer.Infrastructure.Tests/PzlJsonRoundTripReplayTests.cs` | `144636ba98994e24b3c8f2bc7e333fa93cf4a11cddcee52411d7a1f2220376c6` |
| 신규(기록) | 이 보고서, `PHASE_3F_FC2_PZL_DRY_PILOT_VERIFY_LOG.md`(run2), `PHASE_3F_FC2_PZL_DRY_PILOT_VERIFY_LOG_try1_FAIL.md` | 로그 SHA는 §6 |
| 수정(상태) | `CURRENT_STATE.md`, `docs/game-engine.md` §40(신규 절), `Star/01_Phase_Reviews/Phase_03_Review.md`(부록), `Star/00_Dashboard.md`(한 줄) | (상태 문서 — 자기 참조라 미기록) |

- Git: Branch `main`, HEAD `5d7264a5e26a6378f0fd899ab35529ae50cde5b6` 불변(origin/main보다 20 Commit 앞섬 — 이번 작업에서 변화 없음). Staging 0. Stage·Commit·Push·Reset·Clean·Rebase·Amend 없음.
- 보호 파일(시작·종료 SHA-256 동일, 쓰기 없음): `TASK.md` `b0d1c4a77c71273c0656d8ba71722c80d7e6306a41deeae5b2749bd0de075bea`, `docs/2023recordRule.pdf` `9742caaf3a596b03d37fc6c57b9f866e989214e074aa5af1a2ab821ec5f212b0`, `docs/2026-official-baseball-rules.pdf` `f85860f3b754dfb5e07269e9d3a86c77c89b856376f6e3ba8345af36db997859`.

## 9. B-1~B-8과 F-C2 잔여 책임 (변화 없음, 임의 확정하지 않음)

[공식 통합 보고서 §9·§13-6](PHASE_3F_FC2_OFFICIAL_INTEGRATION_REPORT.md)의 항목을 그대로 유지한다.

- **B-1** 공식 결과 스키마·전체 Hash(이번 P-5 PZL 결과 형식 포함) · **B-2** 셀–Seed 승인 결속 표현 · **B-3** 상한·INCONCLUSIVE 확장 규칙 · **B-4** 빌드 출처·승인 출력 경로(이번에도 공식 Preflight가 `Git.dirty`·`git_sha_not_approved`로 거부) · **B-5** 코호트 InputsHash 동결 · **B-6** `fc3-cal`/`fc3-int` 공식 실행 경로 · **B-7** Family 간 Pin 집합 결합 · **B-8** 공식·dry Seed 수치 분리(이번 P-2), F-C2-C D-2·D-3.
- **B-6은 F-C2 전체 완료 전에 해결할 잔여 구현이다.** PZL·Dry Pilot이 통과해도 F-C2 전체 완료가 아니다(이번 작업은 48셀 공식 경로나 `fc3-cal`/`fc3-int` 공식 실행을 연결하지 않았다).
- 공식 통합 Closeout NOTE-1~3(NTSTATUS 이름, 일반 I/O 예외 범위, TEMP 잔여)은 변화 없음.

## 10. 상태

**PZL #5·#6·#10·#12a 검사 구현·자체 시험 완료(실행 수준 결과는 비공식), Dry Pilot 비공식 수행 완료 — `REVIEW_PENDING`(독립 리뷰 대기).** #12b `DEFERRED`. 공식 절 PASS 미판정(F-C3 필요). Official Execution `NOT_READY` · Final Execution Freeze `PENDING` · F-C2 전체(B-6 포함)·Phase 3-F / Phase 3 Gate `INCOMPLETE`.

## 11. Closeout 준비 Addendum (2026-10-19)

- 상태: **`READY_FOR_OWNER_CLOSEOUT_REVIEW`**. 이 Addendum은 문서 정리와 Commit Manifest 점검뿐이다. 코드·시험 수정, 빌드·시험·Verify·Dry Pilot 재실행, Stage/Commit/Push/Reset/Clean/Rebase/Amend, Official Execution, Final Execution Freeze 없음. HEAD `5d7264a5e26a6378f0fd899ab35529ae50cde5b6` 불변. 위 §1~§10 본문은 작성 당시 기록으로 수정하지 않았다.

### 11-1. 독립 리뷰 결과 (소유자 전달)

- Codex High 독립 리뷰: PZL #5·#6·#10·#12a 및 비공식 Dry Pilot에 **Closeout 차단 결함(BLOCKER / MAJOR) 0**. 이 기록은 소유자가 전달한 결과 요약이며 Codex 원문은 이 세션에서 읽지 않았다.
- 리뷰는 **READ-ONLY**였고 Codex가 전체 시험을 **재실행하지 않았다.** 2,784 PASS는 아래 §11-4의 Claude Code 자체 `verify.ps1` 결과이지 Codex 재현 결과가 아니다.
- GPT Chat 검토와 소유자 Closeout 승인은 아직 없다(`승인 완료` 아님).

### 11-2. 최종 상태

| 항목 | 상태 |
|---|---|
| PZL #5 | 실행 수준 검사 구현·자체 시험 완료(비공식 실행 결과, 공식 절 PASS 아님) |
| PZL #6 | Infrastructure 시험 구현·완료 — **비공식 대체 표본 20경기** 한정. 공식 표본은 `BLOCKED`(미동결, B-6, P-1) |
| PZL #10 | 실행 수준 검사 구현·자체 시험 완료(Automated 한정, 공식 코호트는 구조상 자명 — P-3) |
| PZL #12a | 실행 수준 검사 구현·자체 시험 완료(등록 Stream 집합 고정) |
| PZL #12b | **`DEFERRED`** — #12a 구현 완료와 별개이며 BCL/LMV 기능 구현 Phase(5·6·28)까지 PASS로 기록하지 않음 |
| 비공식 Dry Pilot | 9 Runs / 1,030 Games, 별도 프로세스 재실행 Hash 동일, Replay `MATCH` 9/9, 관찰 사건 314,453(위치 사건 = `ef2` 236,855), 기록된 PZL 위반 0, 공식 명령 전부 거부·공식 출력 0 |
| 시험 / Verify | 2,784 PASS / 0 FAIL / 0 SKIP, `GATE: PASS`(최초 시도 try1은 `GATE: FAIL`, 보존) |
| 로컬 Closeout Commit | 미실시 |
| F-C2 전체 / Phase 3-F / Phase 3 Gate | `INCOMPLETE` (B-6 잔여). Official Execution `NOT_READY`, Final Execution Freeze `PENDING` |

Dry Pilot 결과를 Official Evidence나 Phase 3-F Gate PASS로 승격하지 않는다(ADR-009 SR-D4.3).

### 11-3. 비차단 NOTE (코드 변경 없음)

**NOTE-1 — 경기별 PZL 관찰 누락 가능성.** `PzlChecks.Evaluate`(`PzlChecks.cs` 136행)는 경기의 `Pzl`이 `null`이면 `PzlGameObservation.None`(전부 0)으로 대체한다. 그 경기는 FAIL이 아니라 사건 0건으로 합산된다. 모든 경기가 누락되면 #5·#10·#12a가 `NOT_EVALUATED`가 되지만, 일부 경기만 누락된 혼합 RunResult는 위반이 없으면 `PASS`가 될 수 있고 누락은 보이지 않는다. 또한 `Evaluate` 자체는 경기별 확정 사건 수와 관찰 수를 대조하지 않는다(그 대조는 시험 `Pzl5_every_committed_event_…`가 실제 엔진 실행에서 수행). 현재 실제 `GameExecutor`는 모든 경기에 관찰을 붙이며 Dry Pilot 집계(314,453)와 일치하므로 이번 Closeout의 차단 결함이 아니다. **후속 과제(공식 PZL 증거 처리 시):** (a) 경기별 관찰 존재 확인, (b) 경기별 확정 사건 수 = 관찰 수, (c) 관찰이 누락된 경기를 PASS로 처리하지 않음(`FAIL` 또는 `NOT_EVALUATED`). B-1·P-5와 함께 결정한다.

**NOTE-2 — `summary.json`의 신뢰 경계.** `summary.json`(`pzl_checks`, Gate 표시 포함)은 **비인증 파생 진단 결과**이며 Replay 입력도 무결성 보호 대상도 아니다. 위조한 summary(Gate `PASS`, PZL `FAIL` 표시)로도 Replay는 `MATCH`다(Dry Pilot `neg_forged_summary_replay.txt`에서 `REPLAY MATCH` 확인). 따라서 summary의 Gate·PZL 표시를 공식적으로 인증된 결과라고 주장하지 않는다. 공식 결과·PZL 증거의 결속은 P-5 / B-1에 남는다(§5).

**NOTE-3 — PZL #6 변형(Sabotage) 검출 범위.** 결함 주입 10건(S1~S10) 중 #6 Reader 변형은 S7·S8·S9·S10이다. S9는 신규 엔진 20경기 표본에서 직접 검출됐다. **S7(투구 spec 주자 이동 폐기)·S8(인플레이 `BatterSequence` 폐기)·S10(타자 측 Right 고정)은 신규 엔진 20경기 표본에서는 검출되지 않았고**(표본 엔진이 그 값을 만들지 않음 = 표본상 동등), 기존의 넓은 JSON/Replay 시험 묶음에서 각각 21 / 17 / 2개 실패로 검출됐다(S7w / S8w / S10w). 즉 "모든 변형이 신규 20경기 표본에서 직접 검출됐다"고 표현하지 않는다. 공식 PZL #6 표본 검증은 계속 `BLOCKED`다(P-1, B-6, Final Execution Freeze).

**NOTE-4 — PZL #12b.** #12a 구현 완료와 구별하여 `DEFERRED` 유지. Phase 3에서 PASS로 기록하지 않는다(ADR-008 FC-D8.2).

### 11-4. 검증 증거 정합성 (읽기 전용 재확인)

| 항목 | 값 / 확인 |
|---|---|
| 최종 Verify 로그 | `PHASE_3F_FC2_PZL_DRY_PILOT_VERIFY_LOG.md` SHA-256 `bd69e9e7ef03fcae07f0959b2a69fbfe3c3d512cd4ce2bb1776706835352a231`(§6과 일치), `Run at 2026-10-18 18:28:32`, `Git: 5d7264a (dirty working tree)`, `Gate result: PASS`, Domain 327 · Simulation 741 · Application 127 · Infrastructure 1,236 · SimHarness 353 = 2,784 |
| 최종 `verify-result.json` | `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_run1/` SHA-256 `46c7aa024e08dc3162f5ba89f692295a7e35d9d561ab81aa841605c39dd3041b`(일치), Archive 29개 파일 |
| 최초 실패 시도 | `…_VERIFY_LOG_try1_FAIL.md` SHA-256 `fb1ea68a…e398b`, `Run at 18:14:45`, `Gate result: FAIL`; `…_try1_FAIL/verify-result.json` `44de1f0c…a1e6d`(둘 다 일치) |
| `_run1` 이름 | 폴더 이름 `…_run1`은 try1 FAIL 폴더와 별개의 Archive 이름일 뿐이며 최종 트리의 검증 기록이다: try1 로그 18:14:45 FAIL → 코드 수정 → 최종 로그 18:28:32 PASS(같은 `Git: 5d7264a`). 이름이 아니라 로그 시각·결과 SHA로 판단한다 |
| 최종 트리 일치 | 개발 파일 8개의 현재 SHA-256이 §8 표와 모두 일치. 가장 늦은 소스 수정 `PzlChecks.cs` 18:18:39 < 최종 Verify 18:28:32이므로 최종 Verify 이후 소스 변경 없음 |
| Dry Pilot | `…_dry_run1/pilot.json` SHA-256 `91e537b8…570d`(§4-3과 일치, `all_as_expected` 키 존재); 9개 Run 디렉터리 쌍(a/b), 9개 `replay_*.txt` 전부 `REPLAY MATCH`; `summary.json` 9개의 `pzl-5` 사건 합 314,453 · `ef2` 236,855 재합산 일치 |
| Sabotage | `…_sabotage_final/SUMMARY.txt` SHA-256 `d989103a…b0027f`(§3-2와 일치) |
| 보존 | 시도·부분 실행 폴더(`dry_try1/try2`, `sabotage`, `sabotage_try1/try2`, `sabotage_final_try1_partial`)와 `try1_FAIL` 모두 존재. 원본 로그·Archive 수정 없음. 실제 저장소 `build/simharness/` 없음 |
| 기존 증거 492개 | §7의 "492개 파일 SHA-256 시작·종료 동일"은 해당 구현 세션이 기록한 대조 결과다. 이번 Closeout에서는 별도 과거 해시 목록으로 **재검증하지 않았다(`미검증`)**. 위 표의 항목만 이번에 직접 재확인했다 |

문서 링크는 최종 Verify의 D1(159개 파일, PASS)이 이 Addendum 이전 트리 기준이다. 이 Addendum의 링크는 기존 형식의 상대 링크만 쓰며 `verify.ps1`로 재확인하지 않았다(`미검증`, 재실행 금지).

### 11-5. 15개 Commit 후보 Manifest (`git status --porcelain=v1 -uall` 대조)

수정된 추적 파일 9개:

1. `CURRENT_STATE.md`
2. `Star/00_Dashboard.md`
3. `Star/01_Phase_Reviews/Phase_03_Review.md`
4. `docs/game-engine.md`
5. `tools/StarPlayer.SimHarness/GameExecutor.cs`
6. `tools/StarPlayer.SimHarness/Program.cs`
7. `tools/StarPlayer.SimHarness/README.md`
8. `tools/StarPlayer.SimHarness/RunModel.cs`
9. `tools/StarPlayer.SimHarness/RunReport.cs`

신규 파일 6개:

1. `docs/reports/PHASE_3F_FC2_PZL_DRY_PILOT_REPORT.md` (이 보고서)
2. `docs/reports/PHASE_3F_FC2_PZL_DRY_PILOT_VERIFY_LOG.md`
3. `docs/reports/PHASE_3F_FC2_PZL_DRY_PILOT_VERIFY_LOG_try1_FAIL.md`
4. `tests/StarPlayer.Infrastructure.Tests/PzlJsonRoundTripReplayTests.cs`
5. `tests/StarPlayer.SimHarness.Tests/PzlCheckTests.cs`
6. `tools/StarPlayer.SimHarness/PzlChecks.cs`

보호 미추적 3개(제외): `TASK.md`, `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf`. 전체 상태 항목 18개(수정 9 + 신규 6 + 보호 3), Staging 0. 실제 경로·수량은 예상과 일치했다. 코드·시험 파일 8개는 이번 Addendum에서 수정하지 않았다.

### 11-6. 보호 파일 SHA-256 (작업 시작·종료 동일, 쓰기 없음)

- `TASK.md` `09be15fae9a5a7eb198b2ec2c0f87ebfe43baac3a41bb56821523c3460a63eae` — 소유자가 교체한 현재 파일의 이 작업 시작 시점 값이다. §8의 `b0d1c4a7…5bea`는 이전 TASK.md의 값이며 교체로 달라진 것이다.
- `docs/2023recordRule.pdf` `9742caaf3a596b03d37fc6c57b9f866e989214e074aa5af1a2ab821ec5f212b0`
- `docs/2026-official-baseball-rules.pdf` `f85860f3b754dfb5e07269e9d3a86c77c89b856376f6e3ba8345af36db997859`

### 11-7. 남은 차단 결함과 F-C2 전체 완료 이전 잔여 작업

- 알려진 Closeout 차단 결함: **없음**(Codex High 기준 BLOCKER 0 / MAJOR 0; 위 NOTE-1~4는 비차단).
- 미결 유지(임의 확정하지 않음): P-1~P-6, B-1~B-8. 특히 공식 PZL #6 표본 미확정(P-1), 비공식 Dry Seed와 공식 후보 Seed 범위 중복(P-2, B-8), Framing 중립 코호트의 관측 한계(P-3), `cal-vn` F4 DEFF 2.887893709… 는 진단값이며 표본 수·정책 근거가 아님(P-4), 공식 PZL 결과 증거 형식 미확정(P-5), σ_g 측정값을 공식 표본 수로 변환하지 않음(P-6).
- **B-6 `fc3-cal` / `fc3-int` 공식 실행 연결은 F-C2 잔여 구현**으로 유지한다. 그 뒤 Final Execution Freeze 승인값 확정, 공식 PZL #6 표본 검증, NOTE-1 후속 확인이 필요하다. Official Execution·Final Execution Freeze·F-C3·Phase 4는 소유자의 별도 지시 전까지 착수하지 않는다.
