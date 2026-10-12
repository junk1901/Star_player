# Phase 3-C2B-3 구현 인수인계 초안

날짜: 2026-10-12 (Codex 리뷰 수정 반영 판) · 기준 HEAD `f669589` · 상태: **정책 `OWNER APPROVED`(P-01~P-10, 2026-10-12) / 구현 `NOT AUTHORIZED`인 구현 전 인수인계 초안**. **실제 구현 지시서가 아니다.** 소유자가 [정책 결정 초안](PHASE_3C2B3_POLICY_DECISION_DRAFT.md)의 P-01~P-10은 승인됐으나 소유자의 별도 구현 지시 전에는 3-C2B-3을 시작하지 않는다. 2026-10-12 소유자가 **P-01~P-10 전부를 승인**했다(P-05 `Prototype Sequence Policy v1`은 먼저 별도 승인). C2B-3-P는 정책 소유자 승인 완료·구현 전 문서 정합성 점검 상태이며, **정책 승인은 구현 착수 승인이 아니다**: 3-C2B-3 프로덕션 `NOT STARTED / NOT AUTHORIZED`는 소유자의 별도 구현 지시가 있을 때까지 유지한다. 승인된 정책이 이 초안과 다르면 이 초안이 틀린 것이다. 3-C2B-3, 3-C3, Phase 4는 미착수이고 Phase 3 Gate는 INCOMPLETE다.

**최종 정책 승인 반영(2026-10-12):** P-01~P-10 `OWNER APPROVED`를 반영했다(아래 FIX2 문단의 "다른 정책은 `PROPOSED` 유지"는 그 시점의 기록이며 이 승인으로 대체된다). 승인과 별개로 GPT Chat의 FIX2 보고서 기반 `PASS WITH NOTES`(사용자 전달)와 Codex 독립 검토(FIX2 이후 `미실시`)는 구분한다. 구체 변경: §2 `AdvancementInputs`는 첫 구현에서 만들지 않음(P-08), §3 중립의 의미 명시, §9 구현 착수 조건.

**FIX2 수정 이력(2026-10-12):** 재검토 Codex 지적 두 건을 정정했다. (1) P-05 승인 상태: 소유자가 2026-10-12 승인했으나 이 초안이 `OWNER_APPROVAL_REQUIRED`로 남아 있었다 → P-05만 `OWNER APPROVED`로 정정(다른 정책은 `PROPOSED` 유지). (2) 이벤트 계약: 정책 초안이 `InPlayResult.Out → InPlayResult.SacrificeFly`를 요구했으나 `SacrificeFly`는 `InPlayResult`에 없고 Reducer가 파생하는 `BatterResult` 값이다 → 신규 §6.7로 이벤트 입력과 파생 기록을 구분했다. 위 §6의 Resolver 입력·H0/H1/H2·EventId 계약은 바꾸지 않았다.

**수정 이력(보존):** Codex Medium 지적 `후보 생성 입력 및 최신 상태 검증 계약 누락`에 따라 §2의 `Resolve` 개념 서명, 신규 §6(입력·최신 상태 검증 계약), §7 구현 순서, §8 수락 기준을 보완했다. 이전 판의 개념 서명은 `Resolve(PendingAdvancement, TuningProfile, CancellationToken)`였고 최신 상태·권위 RNG·현재 선수·EventId 경계를 검증할 수 없어 **틀렸다**(아래 §6.1 전·후 비교).

## 1. 승인 후 구현의 목표 (한 문장)

`PendingAdvancement` 하나와 **그 순간의 권위 있는 입력(GameState, H0 RNG 스냅샷, 현재 타자·투수 시트, 튜닝, 마지막 발급 EventId 순번)** 을 받아, 입력이 아직 그 Pending이 만들어진 때와 같은지 기존 계약으로 먼저 재검증한 뒤, 승인된 `advancement` 행(`game/runner@1`)으로 열린 결정을 선행 주자 순서로 해결하고, 합법인 `InPlayEvent`를 만들어 **변경하지 않은** `GameReducer.Apply`로 검증하여 기존 `InPlayPitchCandidate`(`NextRng = H2`)로 돌려준다. 상태·ID·영수증·RNG의 채택은 C3다.

## 2. 필요한 최소 타입 (Simulation, resolver-internal; 승인 시 확정)

기존 `StarPlayer.Simulation.Baseball` 관례를 따른다(내부 접근 제한, 형제 타입 격리, 새 패키지·인터페이스·매니저 없음).

| 타입 | 용도 | 비고 |
|---|---|---|
| `AdvancementOutcome` (enum `Extra`, `Hold`, `Out`, `Void`) | 해결된 결과 | `Void` = 플레이 종료 뒤 결정 없음 |
| `AdvancementDecision` (record) | 열린 결정 1개: `ContextKey`, `RunnerKey`, `Player`, `StartBase`, `PlanTo`, `TargetTo`, 보살 수비 위치, 베이스 수비 위치, 승인 확률(ppm 3개) | 기본 계획 `InPlayPlanFacts.Moves`/`AdvancementKeys`에서 파생(이번 검증의 `AdvDecision`과 같은 구조) |
| `AdvancementDraw` (record) | 추첨 1회: 순번, 롤, `Drawn`(원 추첨), `Applied`(적용), 사유(`none`, `blocked_base`, `play_over`) | P-03/P-09 |
| `AdvancementResolution` (record) | 결정별 결과, 최종 이동 목록, 사건 `Sequence`, H1·H2 | 후보 생성 전 중간 결과 |
| `AdvancementAudit` (record) | P-09 감사 필드 | `InPlayAudit`에 선택 필드로 연결 |
| `AdvancementInputs` (record, **첫 구현에서 만들지 않음 — P-08 승인: 중립**) | (향후 별도 계약·버전) 결정별 `runner.speed`, `defense.arm`의 `ModifierInput` | 새 `PlayerSheet` 입력·가상 능력치를 이번에 만들지 않는다 |
| `AdvancementPlanning` (static class) | **개념 서명(타입·순서는 `InPlayPitchPlanning.Build`/`PitchPreparation.CheckPrepared`에 맞춘다; 구현 시 실제 서명과 대조해 확정):** `Resolve(PendingAdvancement? pending, GameState? state, GameRandomSnapshot? rng, TuningProfile? tuning, PlayerSheet? batter, PlayerSheet? pitcher, int lastIssuedEventSequence, CancellationToken cancellation = default) -> Result<InPlayPitchCandidate>` | 새 진입점. `rng`는 **현재 권위 스냅샷 = H0**이다(H1이 아니다, §6.3). 규칙(`GameState.Rules`)·확률 모델은 별도 인수 없이 `state`/`tuning`에서 읽는 기존 방식을 유지한다. 최소 입력 우회 오버로드를 만들지 않는다. `InPlayPitchPlanning.Build`는 변경하지 않는다 |

내부 구성(작은 기능별 서비스, 거대한 관리자 없음): `AdvancementRules`(P-02 가능성 규칙 R1·R2), `AdvancementEventBuilder`(P-05 승인된 사건 순서·`OutAt`·`OutKind.Tag`·수비 기록; 이벤트 `Result`는 실제 `InPlayResult`만 사용), `AdvancementVerify`(`InPlayPitchPlanning.Verify`의 추가 진루 판).

## 3. 선수 능력 공급 (P-08, `OWNER APPROVED`: 첫 구현은 중립) — 향후 별도 계약 시의 최소 계약 변경안(첫 구현에 포함하지 않음)

- 첫 구현은 **중립**이다(`StageProbability.Compute(tuning, "advancement", key)`에 `inputs` 없음; C2A와 동일). **중립은 선수의 실제 주루·송구 능력이 0이라는 뜻이 아니라** 기존 승인된 `advancement` 확률·기본 보정 계약을 선수 구분 없이 쓴다는 뜻이다. 새 `PlayerSheet` 입력이나 가상의 능력치를 만들지 않으며 승인된 튜닝 확률을 바꾸지 않는다.
- 연결을 승인하면 프로덕션 도메인 필드는 추가하지 않고 호출 경계에서 `AdvancementInputs`를 공급한다: 결정별 `ModifierInput.Linear("runner.speed", 주자 Speed)`, `ModifierInput.Linear("defense.arm", 보살 수비수 Arm)`. 시트 조회는 호출자(Application/Infrastructure)가 `PlayerId → PlayerSheet`로 수행하며 Simulation은 `PlayerSheet`의 두 값만 읽는다. **주자·수비수 시트가 추가 입력이 되므로, 그 시점에 §6의 최신 입력 검증에 "주자·수비수 시트 ID가 현재 상태의 해당 주자·수비수와 같다"를 더해야 한다**(승인 전에는 해당 없음).
- 필요한 정보 경로: 주자 `BaseRunner.Player` → 시트, 보살 수비수 `GameState.DefenderAt(fieldingSide, retrieverPosition)` → 시트. 피로·컨디션은 `advancement`에 슬롯이 없으므로 이번 범위가 아니다(슬롯 신설은 확률 모델 변경 = 별도 승인).
- 효과 한계: 선형, 기울기 2000 ppm/단위, 계수 0.9–1.1.

## 4. 예상 수정 파일 (승인 후)

신규:

- `src/StarPlayer.Simulation/Baseball/AdvancementTypes.cs`
- `src/StarPlayer.Simulation/Baseball/AdvancementRules.cs`
- `src/StarPlayer.Simulation/Baseball/AdvancementEventBuilder.cs`
- `src/StarPlayer.Simulation/Baseball/AdvancementPlanning.cs`
- 테스트: `tests/StarPlayer.Simulation.Tests/Advancement/`(규칙·빌더·판정기·정확 분포·소비), `tests/StarPlayer.Infrastructure.Tests/AdvancementPlanningTests.cs`(실제 `PreparedPitch`; §6.5의 "구현 시 확인" 반례 전부)

수정(최소):

- `InPlayCandidateTypes.cs`: `InPlayAudit`에 선택 필드 `AdvancementAudit? AdvancementDetail`(기본 null) 추가. `PendingAdvancement` 형제 격리는 유지.
- `InPlayPitchPlanning.cs`: `Verify`를 추가 진루 해결 결과로 비교하도록 일반화한 오버로드 또는 `AdvancementVerify`(기존 `Verify`는 불변).
- `tests/StarPlayer.Application.Tests/HiddenTendencyBoundaryTests.cs`: 새 resolver-internal 타입의 서명 격리 목록.
- 문서: `docs/game-engine.md` §25(승인된 정책 계약은 이미 기록됨; 구현 결과 갱신), ADR-007, SYSTEM_CONTRACTS C08, `CURRENT_STATE.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, 보고서.

**수정하면 안 되는 파일**: `GameReducer*`, `GameEvents.cs`, `EventFingerprint.cs`, RNG(`GameRandom`, `Pcg32`, 스트림 ID), `PitchBranchResolver.cs`, **`PitchPreparation.cs`의 `CheckPrepared`(재사용만, 서명·코드·우선순위 불변)**, `content/tuning/virtual_tuning_v1.json`, 규칙 프로파일, 골든 벡터·Python 오라클, SQLite 스키마.

## 5. 기존 타입 재사용

| 필요 | 재사용 |
|---|---|
| 기본 계획·열린 맥락 | `BaseInPlayPlan.Facts`(`InPlayPlanFacts`: `Moves`, `AdvancementKeys`, `WalkOff*`, `Runs`, `Official`) — 단, 이는 `pending.Plan`이 이미 보관한 값이며 호출자가 따로 주입하지 않는다 |
| **최신 입력 재검증** | **`PitchPreparation.CheckPrepared(pending.Plan.Prepared, state, rng, tuning, batter, pitcher)` 그대로(§6.2)** |
| 이벤트 ID 경계 | `GameEventIds.Create(state.GameId, last)` → `Next()` → `state.AppliedEvents` 충돌 검사(`InPlayPitchPlanning.Build`의 7–8행 로직과 같은 코드·메시지, §6.4) |
| 이벤트 뼈대 | `InPlayBasePlanner.BuildEvent`로 기본 이벤트를 만들고 결정 주자의 이동만 교체(Extra: `To = TargetTo`; Out: `To = 0`, `OutKind.Tag`, `OutAt = TargetTo`, `Method = Tag`, 자살+보살; Hold/Void: 불변) |
| 승인 확률 | `StageProbability.Compute(tuning, "advancement", contextKey, inputs?)` — 새 JSON 읽기 금지 |
| 롤→결과 | `Distribution.SelectIndex` (한 번의 `NextBounded(1_000_000)`) |
| RNG | `GameRandom.Restore(pending.RngAfterPitchDraw, hash)`, `GameRngStreams.Runner`, `GameRandom.Atomically`(실패·취소 시 불변) |
| 승자 | `InPlayWinningRunner`(`ExistingRunner`) — 최종 득점 순서에서 재계산 |
| 후보 | `InPlayPitchCandidate`(`Event`, `EventId`, `UnconfirmedStep`, `NextRng = H2`, `Audit`) |
| 검증 | 변경하지 않은 `GameReducer.Apply` (원 상태에 적용, 결과는 미확정 미리보기) |

## 6. 입력과 최신 상태 검증 계약 (FIX-1)

### 6.1 수정 전·후 비교

| | 수정 전 (틀림) | 수정 후 |
|---|---|---|
| 개념 서명 | `Resolve(PendingAdvancement, TuningProfile, CancellationToken)` | `Resolve(pending, state, rng(H0), tuning, batter, pitcher, lastIssuedEventSequence, cancellation)` (§2) |
| 최신 게임 상태 | 없음. Pending 안의 `Plan.State`(만들어진 시점의 객체)를 그대로 믿음 | `state`를 받아 `ReferenceEquals(state, pending.Plan.Prepared.State)`를 포함한 기존 검사로 대조 |
| 권위 RNG | 없음. 호출자가 지금 어느 위치인지 확인 불가 | H0 스냅샷을 받아 `StartRngStateHash`와 대조 |
| 현재 선수 | 없음 | 현재 타자·투수 시트의 ID·투타 방향 대조 |
| 규칙·튜닝·모델 | 튜닝만(규칙은 Pending 안의 값에 의존) | `state.Rules` 버전·해시 + 튜닝 버전·해시 + 확률 모델 버전 대조 |
| EventId | "개인 작업 카운터에서 받는다"고만 적고 입력이 없음 | 마지막 발급 순번을 읽기 전용으로 받아 범위·소진·영수증 충돌 검사 |
| 취소/실패 시 불변 | 문서 서술뿐 | 입력 전부가 값 객체/불변 + 개인 복사본 작업이라는 근거 명시(§6.6) |

### 6.2 기존 `CheckPrepared` 재사용 가능성 — **가능, 새 최소 입력 API 금지**

`PreparedPitch`는 내부 생성자만 있는 클래스이고 `PendingAdvancement.Plan.Prepared`가 그 인스턴스를 보관한다. `Resolve`는 첫 검증으로

`PitchPreparation.CheckPrepared(pending?.Plan.Prepared, state, rng, tuning, batter, pitcher)`

를 **서명·코드·우선순위 변경 없이** 호출한다. 실제 서명과 순서(코드 `PitchCandidate.*`/`PitchPrepare.*`):

1. 입력 존재: `prepared_missing`(= `pending`이 null이어도 이 코드가 나온다, 새 오류 코드 불필요) → `state_missing` → `rng_missing` → `tuning_missing` → `batter_sheet_missing` → `pitcher_sheet_missing`
2. 투구 분기 계약 버전: `draft_contract_unsupported`
3. 같은 경기: `game_mismatch`
4. 스냅샷이 복원 가능하고 그 경기 맥락에 속함: 기존 복원 오류 코드, `rng_context_mismatch`
5. **H0 일치**: `rng_state_changed`
6. 현재 타자·투수(ID만): `PitchPrepare.batter_mismatch`, `PitchPrepare.pitcher_mismatch` → 준비 시점과 같은 타자·투수·투타: `batter_changed`, `pitcher_changed`, `batter_side_changed`, `pitcher_hand_changed`
7. 규칙·튜닝·확률 모델: `rules_changed`, `tuning_changed`, `probability_model_changed`
8. **마지막에** 같은 상태 객체: `state_stale`

이 순서가 곧 C2B-1의 순서이며(C2B-2 `PlanBase`/`Build`도 같은 호출), `Resolve`는 이를 앞지르거나 대체하는 사전 검사를 두지 않는다. 기존 검사의 한계(그대로 승계): 규칙·튜닝은 버전과 해시로, 타자·투수는 ID와 선언 투타로만 비교하며 그 외 적격성은 만들지 않는다. `state_stale`의 참조 동일성은 단기 메모리 검사이지 영속 무결성이 아니다(저장·복원 후 Pending 재사용 불가, 이는 의도).

### 6.3 H0 / H1 / H2의 정확한 검증 경계

| 기호 | 정체 | 어디서 검증/사용 |
|---|---|---|
| **H0** | `Prepare`에 들어간 스냅샷 = **현재 권위 RNG** | `Resolve`의 `rng` 인수. `CheckPrepared` 5단계가 `rng.StateHash == prepared.StartRngStateHash`와 ContextKey = GameId를 요구. 호출자가 H1을 "현재 RNG"로 넘기면 `rng_state_changed`로 거부(테스트됨, §6.5) |
| **H1** | C2A 추첨 직후 = `pending.RngAfterPitchDraw` = `prepared.Draft.NextRng` | **호출자의 `rng`와 직접 비교하지 않는다.** 현재 권위 RNG가 H0인 한 H1은 아직 채택되지 않은 위치이므로 동일성을 요구하면 정상 Pending이 거부된다. H0가 일치하면 `PreparedPitch`가 내부 생성자만 가진다는 점으로 H1의 출처가 보장되고, `Resolve`는 `GameRandom.Restore(H1, H1.StateHash)`로 구조·해시 일치를 확인한 개인 사본에서 후속 추첨을 이어간다 |
| **H2** | 추가 진루 추첨 완료 후 위치 | `Atomically`가 성공했을 때만 개인 사본의 스냅샷을 만들고 `InPlayPitchCandidate.NextRng`로 돌려준다. 채택은 C3 |

H1이 권위 위치가 되는 상태는 설계에 없다(C2A 뒤 부분 채택 금지; 채택은 상태·ID·영수증·H2를 한꺼번에). 따라서 "권위 RNG = H1"인 호출은 오래된·잘못된 호출로 보고 거부하는 것이 맞다.

**C3가 다시 검증할 것(Resolve 검증의 대체가 아님):** 채택 시점에도 권위 RNG가 여전히 H0인지, 상태 객체·마지막 발급 순번이 그대로인지 다시 확인한다.

### 6.4 EventId·영수증 충돌 검사 계약

`CheckPrepared`는 EventId를 보지 않는다(그리고 현재 `InPlayPitchPlanning.Build`는 `PendingAdvancement`를 만들 때 커서를 **전혀 보지 않는다**: 어떤 순번과 어떤 영수증 목록에서도 `Pending`이 나온다 — 현재 테스트로 확인). 따라서 Resolver가 자기 안에서 다음을 `Build`와 **같은 코드·같은 순서**로 수행해야 한다:

1. 유효성 검증(§6.2, 필요하면 §6.3의 H1 복원)을 모두 통과한 **뒤** 개인 카운터를 만든다: `GameEventIds.Create(state.GameId, lastIssuedEventSequence)` → 범위 밖이면 `Id.sequence_invalid`.
2. `Next()` → 한도에 도달했으면 `Id.sequence_exhausted`(순번이 되돌아 감기지 않는다).
3. 얻은 ID가 `state.AppliedEvents`의 어느 영수증과 같으면 `InPlayCandidate.event_id_in_use`(메시지: 마지막 발급 순번이 오래됐을 수 있음).
4. 호출자의 커서·`GameState`·영수증 목록은 바꾸지 않는다. 실제 ID 발급이 아니라 개인 작업 카운터이며 후보의 `EventId`는 미확정이다.
5. 후보는 하나의 `InPlayEvent`이므로 ID는 하나다. `Pending`은 계속 ID·`GameEvent`·`GameStep`을 직접 갖지 않는다.

**감지 가능한 것과 불가능한 것(정직한 한계):** 커서가 너무 작아 다음 ID가 기존 영수증과 겹치면 거부된다. 커서가 너무 커서 번호가 건너뛰어지는 경우는 이 계층에서는 알 수 없다(영수증 목록의 최댓값보다 크기만 하면 충돌이 없음) — 이는 C3가 실제 EventId 커서와 비교해 막는다. 영수증 목록의 최대 순번보다 작은 커서를 일률적으로 거부하는 더 엄격한 규칙은 `Build`와 일관되지 않아 이 초안에 넣지 않았다(소유자/Codex 판단 사항으로만 기록).

### 6.5 오래된 Pending 재사용 반례 (확인 상태 구분)

"현재 확인" = 프로덕션 API가 이미 있어서 기존 코드를 대상으로 실행한 테스트(`tests/StarPlayer.Infrastructure.Tests/AdvancementPolicyHandoffTests.cs`, 실제 composite `PendingAdvancement`의 `Plan.Prepared`를 기존 `PlanBase`/`Build`에 넣음). "구현 시 확인" = `Resolve`가 존재해야 쓸 수 있는 테스트이며 **지금은 작성·보고하지 않았다**(존재하지 않는 API를 대상으로 하는 실패 테스트를 통과로 보고하지 않는다).

| 상황 | 기대 결과 | 확인 |
|---|---|---|
| 같은 Pending, 유효한 원본 상태 | `CheckPrepared` 통과, 같은 Pending·같은 H1 | **현재 확인**(`A_real_pendings_prepared_pitch_is_accepted_again_...`). `Resolve` 성공 자체는 구현 시 |
| GameState 인스턴스 교체(저장·복원과 동치) | `PitchCandidate.state_stale` | **현재 확인** |
| 다른 GameId (상태) / 다른 GameId (RNG 맥락) | `game_mismatch` / `rng_context_mismatch` | **현재 확인** |
| 권위 RNG H0 변경 | `rng_state_changed` | **현재 확인** |
| 권위 RNG로 H1을 넘김 | `rng_state_changed`(H1은 채택 위치가 아님) | **현재 확인** |
| 현재 타자·투수가 아닌 시트 | `PitchPrepare.batter_mismatch` / `pitcher_mismatch` | **현재 확인** |
| 준비 시점과 다른 타자·투수·투타 | `batter_changed` 등 4종 | 기존 C2B-1 테스트(`PitchPreparationTests`)가 확인. Pending 경로 재확인은 구현 시 |
| 규칙 해시 변경 / 튜닝 해시 변경 | `rules_changed` / `tuning_changed` | **현재 확인** |
| 확률 모델 버전 변경 | `probability_model_changed` | **미확인**(기존 테스트 없음; 구현 시 위조 `PreparedPitch`로 확인) |
| `pending`·입력 null | `prepared_missing` 등 | **현재 확인**(`prepared_missing`); 나머지 null은 기존 C2B-2 테스트 |
| EventId 충돌 | `InPlayCandidate.event_id_in_use` | **구현 시 확인** — 현재 `Build`는 Pending에서 커서를 보지 않음(위 §6.4, 테스트됨) |
| 순번 범위 밖 / 한도 도달 | `Id.sequence_invalid` / `Id.sequence_exhausted` | **구현 시 확인** |
| 너무 큰 커서(번호 건너뜀) | 이 계층은 감지 불가 → C3 | **구현 시 확인**(감지되지 않음을 명세로 고정) |
| 취소 발생(단계별) | `OperationCanceledException`, 부작용 없음 | **구현 시 확인**(C2B-2의 7단계 훅 방식) |
| 동일 입력 재시도 | 같은 후보·같은 H2 | H1에서 같은 롤 재생은 **현재 확인**(스탠드인 추첨); 후보·H2 동일성은 구현 시 |
| 실패(Reducer 거부) | 실패 결과, H1·상태·커서 불변 | H1 불변은 **현재 확인**(`GameRandom.Atomically`); Resolve 경로는 구현 시 |

### 6.6 실패·취소 시 원본 불변의 근거

`GameState`는 불변 레코드이고 `GameRandomSnapshot`은 값 객체이며 `PendingAdvancement`/`PreparedPitch`는 읽기 전용이다. 추첨은 `Restore(H1, hash)`로 만든 **개인 `GameRandom`** 위에서 `Atomically`로 실행되어 실패·취소 시 개인 사본은 버려진다. ID는 개인 카운터이고 영수증 목록은 읽기만 한다. 취소는 `Build`와 같이 예외(`OperationCanceledException`)로 알린다. 따라서 호출자의 `GameState`, `rng`(H0), `pending`(H1), 커서, `AppliedEvents`는 어떤 단계에서도 바뀌지 않는다.

### 6.7 최종 이벤트 입력과 Reducer 파생 공식 기록의 구분 (FIX2-2)

| 구분 | 타입 | 값 | 누가 정하는가 |
|---|---|---|---|
| 이벤트 입력 결과 | `InPlayResult`(`GameEvents.cs`) | `Single`, `Double`, `Triple`, `HomeRun`, `Out`, `FieldersChoice`, `ReachedOnError` — **`SacrificeFly` 없음** | Resolver가 `Apply` **이전**에 확정 |
| 공식 기록 | `BatterResult`(`GameRecords.cs`) → `ScoredPlay.Result` | `Out`, `SacrificeFly`, `SacrificeBunt` 등 | `GameReducer.Plays.cs`가 이벤트와 최종 이동에서 **파생** |

희생플라이는 이벤트 `Result == Out`, `Batted == Fly`, `Sacrifice == false`, 플레이 아웃 정확히 1개, 시작 시 아웃 2개 미만, 오류 아닌 득점 1점 이상일 때 Reducer가 `BatterResult.SacrificeFly`로 파생한다.

Resolver 순서(앞 단계가 끝나기 전에 뒤 단계를 하지 않는다): ① C2B-2 기본 계획 → ② C2B-3 추가 진루·주루사 결정 → ③ P-05 `Sequence` 확정 → ④ 불가능한 플레이를 정책 계층(P-02 R1·R2)에서 차단 → ⑤ 실제 `InPlayResult`와 최종 이동으로 `InPlayEvent` 생성(뜬공 아웃은 `Out`) → ⑥ 원본 상태에 `GameReducer.Apply` → ⑦ 최종 `ScoredPlay`에서 Reducer가 파생한 `BatterResult`·타수·타점·득점·아웃·LOB·결승 득점자를 기대값과 대조 → ⑧ 감사에 **이벤트 입력과 Reducer 결과를 별도 필드로** 기록. `Apply` 뒤 이벤트의 `Result`·이동을 바꾸지 않으며, 희생플라이에 필요한 최종 이동과 순서는 ⑤ 전에 완성한다.

| 상황 | 이벤트 `Result` | 예상 기록 |
|---|---|---|
| 일반 뜬공 아웃, 득점 없음 | `Out` | `BatterResult.Out` |
| 뜬공 아웃, 3루 주자 정상 득점 | `Out` | 요건 충족 시 `BatterResult.SacrificeFly` |
| 뜬공 아웃, 제3아웃으로 득점 무효 | `Out` | 실제 Reducer 판정 |
| 안타 뒤 추가 진루 | 실제 안타 `Result` | 희생플라이 아님 |
| 추가 진루 실패로 득점 없음 | 실제 기본 `Result` | 최종 결과에 따름 |

## 7. C2B-2와의 연결, C3와의 경계

- 호출 순서: `InPlayPitchPlanning.Build(...)` → `PendingAdvancement`이면 호출자가 **같은 `state`·`rng`(H0)·튜닝·타자/투수 시트와 읽기 전용 마지막 발급 순번**으로 `AdvancementPlanning.Resolve(pending, state, rng, tuning, batter, pitcher, last, ct)`를 호출한다. `Build`와 C2B-2 계약(RNG 0회, ID 미발급, 영수증 없음)은 그대로다.
- `Resolve`는 H1에서 시작해 H2를 반환값 `NextRng`로만 준다. 입력 `PendingAdvancement`, `GameState`, `rng`, ID 커서, 영수증은 바꾸지 않는다.
- C3가 맡는 것: 상태·RNG(H2)·ID·영수증의 원자적 채택, **권위 RNG가 아직 H0인지·마지막 발급 순번이 그대로인지의 재검사**, 타석 진행, 감사 영속화. Phase 4: 내구 저장. C3의 재검사는 `Resolve` 검증의 대체가 아니다.
- **(2026-10-13 주석, 이 초안은 이력으로 보존)** 위 "C3가 맡는 것"의 확정 설계는 소유자가 승인했다(설계만, 구현 `NOT STARTED / NOT AUTHORIZED`): [game-engine §27](../game-engine.md)·[ADR-007](../decisions/ADR-007.md) "Decisions (3-C3 design approval)". 특히 "타석 진행"은 D-4로 C3에서 제외되어 3-F 하네스 책임이고, 재검사는 마지막 확정 순번 + 1 정확 일치와 `Apply` 재실행 결과 채택으로 구체화됐다.

## 8. 구현 순서 (승인 후, TDD)

1. **판정 규칙을 먼저 고정**: 이번 검증의 `AdvJudge`·사례 열거를 수락 데이터로 승격(독립성 한계 R-8을 문서화). 가능성 규칙 R1·R2 단위 테스트 RED→GREEN.
2. **오래된 입력 검증 테스트를 먼저**: §6.5의 "구현 시 확인" 행 전부를 `Resolve` 스텁에 대해 RED로 작성하고, `CheckPrepared` 호출 + EventId 경계를 GREEN으로.
3. **이벤트 빌더**: 5,562 원시 벡터 중 합법 4,722개를 실제 Reducer로 수락 + 기록 일치(RED: 빌더 없이 실패).
4. **리졸버·RNG**: 고정 소비 K회, 정확 분포(손 유도 값), 빈도 5σ, 동일 시드 재현, 거절 표집 raw 수 허용.
5. **감사(P-09)** 와 `Verify` 추가 진루 판.
6. **통합**: 실제 `PreparedPitch`(Infrastructure) — H0 대조·H1 시작 보존, 취소 단계별 테스트, 상태·커서 불변.
7. 변형 검사(규칙·소비·순서·귀속·Void·**H0/H1 혼동·ID 경계**), 전체 xUnit, `tools/verify.ps1` 연속 2회, 문서 정합.

## 9. 테스트 수락 기준 (초안)

- §6.5의 모든 행이 해당 코드·결과로 통과하고 원본 상태·H0·H1·커서·영수증이 불변이다.
- 1,314 보류 사례 전수에서 해결 가능한 모든 도달 벡터가 합법이고 Reducer가 수락하며 판정기의 기록(득점 수·아웃·종료·득점자)과 일치한다. 불법 0, 거부 0, `LEGAL_MISMATCH` 0.
- 손 유도 분포와 정확 일치(예: 1·2루 0아웃 `E/E = 59 % × 27 %`, `H/H = 38 %`).
- 추첨은 선언된 맥락 수 K회 `NextBounded(1_000_000)`, 다른 스트림 불변, H1 불변, 실패·취소·재시도 불변/재현.
- 득점+제3아웃 플레이는 **소유자가 승인한 P-05 `Prototype Sequence Policy v1`**(기존 네 개 맥락, 선행 주자 우선, 명시 `Sequence`)로 구성되고, 순서를 뒤집은 회귀 데이터는 "정답이 아니라 민감도 증거"로 보존한다. 이 정책은 실제 관측 순서도 KBO 인증도 아니며 향후 시간 기반 주루 모델로 교체될 수 있다. 정책 P-01~P-10은 모두 승인됐지만 구현은 소유자의 별도 구현 지시가 있어야만 시작한다.
- 끝내기: 결승 득점자 재계산, Reducer가 파생한 `BatterResult`(`SacrificeFly` 포함)·AB·RBI·LOB를 최종 `ScoredPlay`와 기대값으로 대조, 끝내기 뒤 Void.
- 이벤트 `Result`는 실제 `InPlayResult` 값만 쓰며(`SacrificeFly` 이벤트 값 없음), `Apply` 뒤 이벤트를 수정하지 않는다(§6.7).
- 기존 5,400건 오라클 비교, 3-C2A 골든 벡터, `ef1`/`ef2`, C2B-1 63건, C2B-2 172건 불변.
- 실제 C2B-3 코드의 변형 검사 전수 검출, 전체 xUnit PASS, `verify.ps1` 연속 2회 `GATE: PASS`.

## 10. 아직 구현하면 안 되는 것

- 소유자의 구현 지시 전 3-C2B-3 코드 일체, `game/runner@1` 실제 추첨.
- 선수 능력 연결(P-08)과 새 modifier 슬롯(확률 모델 변경).
- 타자주자 추가 진루, 병살·야수선택·희생번트, 포스 아웃을 만드는 새 맥락, 인필드 플라이 모델링.
- 주자 간 상관 보정(3-F).
- 상태·ID·영수증·RNG 채택, 감사 영속화(C3/Phase 4).
- Reducer·`GameEvents`·RNG·확률값·골든 벡터·SQLite 스키마 변경.
- KBO 2026 인증 주장(임시 OBR 근거 유지).
