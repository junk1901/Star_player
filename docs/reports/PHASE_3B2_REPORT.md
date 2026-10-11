# Phase 3-B2 Report — Integer conditional probability model and virtual tuning contract

> **최종 상태 (2026-10-11): 승인 — GPT Chat `PASS WITH NOTES`, Codex 최종 집중 재검토 `PASS`** (새 Critical/High/Medium 결함 없음; 전체 xUnit 955 PASS; verify 2회 GATE PASS). 아래 "Fix round"/"first version"의 `검토 대기`·"Codex 미실시" 문구와 Codex `CHANGES REQUIRED` 이력은 당시 상태로 보존되며 이 승인으로 대체된다. **Phase 3 전체 Gate INCOMPLETE, 3-C 미착수; 이 승인은 Phase 3 전체 승인이 아니다.** 유지하는 NOTES:
- 3-C: target_zone 선택 방식 및 RNG 스트림 계약
- 3-C: NextBounded 호출 수와 실제 Draws 구분
- 3-C: 실제 투구 결과, 이벤트, ef2 및 공동 원자성
- 3-D: 코스 성향의 실제 확률 연결
- 3-D 이전: 구장 Hits 계수의 단타 전용 매핑에 대한 소유자 결정
- 3-E: Framing 상한 및 실제 계산
- 3-F: 100,000 PA, 100경기, 8개 집계 목표 검증
- Phase 4: RNG 기대 해시 및 경기 상태·영수증의 트랜잭션 저장
- 확률표의 이전 설계 조정 이력(목표 구역 가중치 두 차례 조정)과 독립 검증의 한계 유지

**Fix round (2026-10-11): after the independent Codex review (`CHANGES REQUIRED`: High x2, Medium x1) the three findings were reproduced RED, fixed and verified — see Part G at the end. Part G supersedes the test counts (now 955) and the "Codex `미실시`" wording of the first version below; review status stays `검토 대기`.**

Status (first version): **implemented and self-tested; review status `검토 대기`** (not approved). Scope: internal work package 3-B2 of Phase 3 only (owner instruction `TASK.md`, 2026-10-11): the integer probability scale, the conditional probability structure, a versioned **virtual prototype** tuning set, the modifier/tendency contract and the calculation trace. **No pitch is resolved, no random number is drawn by production code, no event / `ef1` / `ef2` / `GameReducer` / GameRules / RNG stream changed, nothing is persisted, and no statistical calibration (100,000 PA, 100 games) was run.** The Phase 3 overall Gate is INCOMPLETE; 3-C onward, Phase 4 and any commit/push are not started/permitted. An independent Codex review is `미실시` (recommended). Decisions: `docs/decisions/ADR-006.md`; contract: `docs/game-engine.md` §19.

## 1. Probability denominator and normalization rule

- **Denominator `1,000,000` (ppm).** Probabilities are whole numbers `0..1,000,000`; every conditional distribution sums to exactly `1,000,000`. Checked against the existing scales (skills 0-100, tendency -100..+100, C46 permille 1000, KRW integers all divide it) and against `NextBounded`'s `uint` bound. Rationale and rejected alternatives: ADR-006 item 1.
- **Normalization `largest-remainder-lowest-index-v1`.** `floor(w·T/S)` per outcome; leftover units to the largest remainder `(w·T) mod S`, ties to the **lowest index**; zero weights stay exactly zero; `Int128` intermediates; 1-64 outcomes, weights `0..2^56`, total `1..1,000,000`; anything else is an explicit `Probability.*` error. Products round half up (`half-up-v1`). Known limit (pinned by a test): a negligible positive weight can still round to 0 (`[2^56, 1] → [1,000,000, 0]`), hence the positive-baseline floor and the loud "baseline-positive outcome lost" invariant.
- **Roll contract (defined, not consumed).** One categorical decision = one `NextBounded(1,000,000)` call; `Distribution.Select(roll)` gives outcome `i` exactly `Ppm[i]` consecutive rolls (verified over all 1,000,000 rolls for three distributions). `NextBounded(10^6)` skips about 0.0225% of raw outputs (threshold 967,296): a decision is one call but `Draws` can grow by more than one - 3-C counts decisions by calls.
- No `double`/`float`/`decimal`/`Math.*`/`Random`/clock/RNG type appears in `Domain/Baseball/Probability/` (source-scan test).

## 2. Conditional probability structure (`prob-model-v1`, ROADMAP §4.1 order)

Eight stages, each its own denominator ("given the condition"), never mixed; a path probability is the product of its stage probabilities. Target zone (3 intents) → actual location (17 targets) → batter decision (`hit_by_pitch`/`take`/`swing`, 17 zones) → called pitch (take; 2 modes × 17 zones = 34) → swing result (`miss`/`foul`/`in_play`, 17 zones) → batted ball (10 outcomes by `heart`/`edge`/`outside`) → defence result (6 outcomes by the 10 batted balls) → advancement (3 outcomes, 4 runner situations). 105 baseline rows in total. A `BigInteger` test sums every mutually exclusive path through decision → call / swing result → batted ball → defence result for each of the 17 zones × 2 modes and gets exactly `D⁴`. Table: ADR-006 item 5.

## 3. Virtual baselines and caps

- **Status `virtual_prototype`**, provenance "VIRTUAL PROTOTYPE values chosen by the Phase 3-B2 author for a fictional league; not derived from KBO, MLB or any real statistics" - enforced by validation (any other status is rejected). Rationale and the numbers the reviewer should judge first: ADR-006 item 7.
- **Disclosure of one design iteration.** An exact-arithmetic check of the first draft of the target-zone weights gave an in-zone pitch share of 0.738 (intended 0.45-0.65); the weights were moved toward the outer zones twice before the profile was frozen and before any sampled run (final: balanced 0.640 / attack 0.703 / careful 0.605; swing share 0.508 / 0.537 / 0.487; whiff per swing 0.230 / 0.222 / 0.237). That is table design, not calibration against results; the pinned hash covers the final tables only.
- **Modifier caps (live slots).** Per-slot factor ranges at the ends of the input range: Stuff ±6%, Control / Eye / Contact / Power ±7.5%, Speed / Arm ±10%, Velocity ±4%, Fielding ±4%, approach 0.88-1.10, count pressure 0.88-1.10, handedness 0.97-1.04. **Combined cap `[800,000, 1,250,000]`** on the product of all factors acting on one outcome (clipping is recorded in the trace); `combinedMin × 2 ≥ combinedMax` is required so no positive baseline can vanish. Fixed application order = the model's slot order; neutral input = exact baseline.
- **Reserved slots** (neutral input only, no numeric authority, tuning forced to slope 0 and min = max = 1,000,000): `catcher.framing` (3-E), `catcher.game_calling`, `battery.chemistry` (Phase 7), `park.home_runs`, `park.hits`, `park.extra_base_hits` (3-D; disjoint outcome ownership; the C46 `Hits` = singles-only mapping needs an owner confirmation before activation).
- **No double counting:** each ability input feeds one slot, a slot twice is `Modifier.duplicate_input`, Power acts only on hard-contact share, `defense.fielding` is one effective value, Eye only on outer zones (trace shows "not applied" inside). Player ratings are unreachable: no type of the probability namespace names a `StarPlayer.Domain.Players` type (reflection test).

## 4. Pitch Zone Lite course tendency and reference contract

- `pzl-zones-v1` (17 zones, 5 `Clear` / 12 `Borderline`) and `pzl-ref-uniform-v1` (`[1]×9`) are **unchanged**; no new reference was needed (a mix-weighted one would need a new registered id and is deferred to 3-F).
- Tendency = exact mass shift on two stages after normalization: `swing_result` (favourable `foul`+`in_play`, cap 60,000 ppm) and `batted_ball` (favourable `ground_hard`+`line_hard`+`fly_hard`, cap 40,000 ppm); shift `value × cap / 100`, both sides keep exact totals; outer zones unadjusted; a window guard (`[cap+minMass, D−cap−minMass]`, minMass 1000) is applied whenever a `TendencyContext` is supplied, independent of the vector, so the shift is never clipped, both sides stay positive and "no tendency" ≡ "all-zero tendency".
- **Neutrality, exactly stated.** For every vector accepted against the uniform reference, the sum over the nine inner zones of the favourable mass with the vector equals the sum without it (exact integers; 8 vectors incl. ±100 extremes; also under extreme ability inputs). **Not claimed:** unchanged league-average output - the real pitch mix is not uniform and the stage is reached with different probabilities; a test documents the gap for the shipped mix (non-zero, bounded by the cap).
- Baselines distinguish zone, boundary class and `CalledPitchMode` (`automated` = hard line; `non_automated`: `Clear` ≥95% certain, `Borderline` both sides ≥5%, leaning to the zone's side); an undeclared mode is `CalledPitchMode.undeclared`. `pitcher.control` raises the on-target outcome of `actual_location`; the actual location is drawn (future 3-C) before any outcome stage.
- `game/location` future draw contract (handoff, nothing changed): one `NextBounded(1,000,000)` on `game/location@1` per actual pitch using `actual_location | target`, before the outcome draws; stage 1 is a deterministic policy or one more sampled decision whose stream 3-C must choose (proposal `game/ai@1`; if `game/location`, ADR-004 item 7's per-pitch count text must be amended first).

## 5. Tests and regression (actual results)

Environment: Windows 11 Home 10.0.26200, .NET SDK 8.0.425, Debug configuration for the development runs below (the Release runs are inside `tools/verify.ps1`, section 5.1).

| Project | Before (3-B1 approved) | After | New |
|---|---:|---:|---:|
| Domain.Tests | 232 | 253 | +21 (`ProbabilityScaleTests`) |
| Simulation.Tests | 371 | 371 | 0 (unchanged; includes the 111 Randomness tests and the golden vectors) |
| Application.Tests | 117 | 117 | 0 (the hidden-boundary guard now also covers `TendencyContext`, `ProbabilityTrace`, `StageResult`) |
| Infrastructure.Tests | 93 | 168 | +75 (`TuningProfileTests` 20, `StageProbabilityTests` 38, `TuningScenarioTests` 13, `Phase3B2CompatibilityTests` 4) |
| **Total** | **813** | **909** | **+96**, 0 failed, 0 skipped |

Coverage of the ordered test list in `TASK.md` §8: (1) exact sums - `Distribution`, `Normalize`, every one of the 105 rows, random-input property test, BigInteger path sum; (2) normal / boundary / invalid probabilities - `ProbabilityScaleTests`, `Tuning*` mutation tests; (3) overflow - extreme weights, `MultiplyRatio` overflow; (4) determinism - repeated runs, input-order independence, scenario reproduction; (5) before/after traceability - `ProbabilityTrace` re-derivation test; (6) neutral input - all rows, all slots; (7) per-factor bounds - both ends of every slot; (8) monotonicity and combined cap - every slot × every condition × every input value (exact, no ±1 slack), table order, combined clipping both ways; (9) mean neutrality - 8 vectors × 2 stages, extreme inputs, guard case; (10) actual-location distribution validity - all 17 rows, 0 / D rows; (11) unknown model / tuning / zone / reference version rejection; (12) GameRules v1 hashes (seven, existing tests) and v2 hashes (pinned in `Phase3B2CompatibilityTests` from the unchanged approved state); (13) RNG golden vectors and constants (existing tests plus pinned versions and stream names); (14) all 813 earlier tests pass.
Counter-examples / mutation checks: a highest-index tie-break and plain rounding produce different results from the policy (tests); **8 deliberate defects were injected into the implementation and the suite was run**: tie-break reversed, no combined clamp, no window guard, tendency delta off by one unit, duplicate input allowed, reserved slot accepted, a hit-by-pitch rule removed → all detected (13 failing tests for the delta defect alone); "an extra target on `batter.power`" initially **survived**, so the pinned slot-catalog test was added and now catches it. Sources were restored after each run (the suite is green again).

Pre-declared scenarios (profile data, fixed before the first run; seeds `2026101101`-`2026101104`, 200,000 samples each, tolerance `binomial-4.5-sigma-ceiling-v1`, exact for p ∈ {0,1}): all four **passed on the first run**; seeds and tolerance were not adjusted. They use a throw-away generator and a test-only stream name; production code consumed no randomness (test: `GameRandom` snapshot unchanged and all six `game/*` streams at 0 draws after computing every stage).

## 6. Pre-declared seeds, scenarios and statistical tolerances

| Scenario id | Distribution | Seed | Samples | Rule |
|---|---|---:|---:|---|
| `s2_location_on_target_center` | `actual_location` given `in.r2c2` | 2026101101 | 200,000 | binomial 4.5σ ceiling |
| `s3_decision_inner_corner` | `batter_decision` given `in.r1c1` | 2026101102 | 200,000 | same |
| `s4_called_pitch_nonauto_border` | `called_pitch` given `non_automated:in.r1c2` | 2026101103 | 200,000 | same |
| `s7_defense_fly_hard` | `defense_result` given `fly_hard` | 2026101104 | 200,000 | same |

Declared aggregate targets for 3-F (status `declared_for_3f_not_evaluated`, ppm of the stated denominator): strikeout rate per PA 160,000-260,000; walk rate 60,000-110,000; hit-by-pitch rate 4,000-15,000; home-run rate 15,000-35,000; BABIP 270,000-320,000; in-zone pitch share 450,000-650,000; swing share 400,000-560,000; whiff per swing 180,000-300,000. They are virtual-league targets for the prototype, not claims about real leagues, are covered by the profile hash (a change is a new tuning version), and **none was evaluated in 3-B2**. The three pitch-level ones are consistent with the frozen tables by the exact-arithmetic note in section 3 (disclosed there); the PA-level ones need the 3-C plate-appearance loop. Phase 3-F is **not** claimed.

### 5.1 `tools/verify.ps1` (two consecutive runs, after all code and documents were final)

`powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify.ps1 -LogName PHASE_3B2_VERIFY_LOG_run1.md` (run 12:15 +09:00) and `... _run2.md` (12:18 +09:00): both **`GATE: PASS`**, exit 0. Release unit tests inside the script: Domain 253, Simulation 371, Application 117 (architecture 14, determinism 48), Infrastructure 168 = 909 passed, 0 failed; Godot headless diagnostics, the Windows export and the exported-program checks at QHD passed unchanged; Markdown link check 69 files. Logs: `docs/reports/PHASE_3B2_VERIFY_LOG_run1.md`, `docs/reports/PHASE_3B2_VERIFY_LOG_run2.md` (the script's heading text is the generic Phase 2 title; the content is this run). Run by Claude Code; no independent re-run.

## 7. Compatibility with GameRules and RNG

`git diff` against the approved 3-B1 commit shows **no change** in `src/StarPlayer.Simulation/`, `src/StarPlayer.Domain/Baseball/Game/`, `content/rules/`, `tests/Fixtures/` (RNG golden vector, legacy GameRules hashes, scripted games) or `tests/StarPlayer.Simulation.Tests/`. The seven legacy GameRules hashes and two v2 hashes, `pcg32-xsh-rr-v1` / `kd1` / `rngset1` and the six `game/*@1` stream names are pinned by passing tests. `ef1` / `ef2` and the reducer were not touched. The only edits to existing files: the Infrastructure `.csproj` (one embedded resource), `HiddenTendencyBoundaryTests` (stronger guard), `docs/` and `CURRENT_STATE.md`, `Star/` records, and ADR-005's status line (instruction in `TASK.md`).

## 8. Handoff to 3-C / 3-D / 3-E / 3-F

- **3-C:** realise `target_zone`; one `NextBounded(1,000,000)` per decision on the named stream; location draw before outcomes on `game/location@1`; count decisions by calls (raw-draw overhead 0.0225%); switch hitter's per-pitch side rule (enters only via `matchup.handedness`); `ef2` payload, replay and persistence of the tuning hash / versions.
- **3-D:** supply a `TendencyContext` for every inner-zone pitch; activate park slots after fixing caps and the C46 `Hits` meaning (owner decision); decide the pitcher repertoire / Velocity / Endurance use (only `pitcher.stuff`, `pitcher.velocity`, `pitcher.control` are mapped here; per-pitch-type effects are not).
- **3-E:** Framing cap on `Borderline` zones in `non_automated` mode (zero in `automated`); production `CalledPitchMode` evidence (still unsettled).
- **3-F:** replace / confirm the virtual baselines against the declared targets and the 100,000-PA / 100-game Gates; consider a mix-weighted tendency reference; a changed number is a new `TuningVersion`.

## 9. Git state

Branch `main`, base commit `7756442` (3-B1). **Nothing is committed or pushed** (not permitted by `TASK.md`). Working tree: modified - `src/StarPlayer.Infrastructure/StarPlayer.Infrastructure.csproj`, `tests/StarPlayer.Application.Tests/HiddenTendencyBoundaryTests.cs`, `docs/decisions/ADR-005.md`, `docs/game-engine.md`, `docs/SYSTEM_CONTRACTS.md`, `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`; new - `src/StarPlayer.Domain/Baseball/Probability/` (4 files), `src/StarPlayer.Infrastructure/Fixtures/TuningProfileJson.cs`, `content/tuning/virtual_tuning_v1.json`, `tests/Fixtures/generator/gen_virtual_tuning_v1.py`, 5 test files (`ProbabilityScaleTests.cs`, `TuningProfileTests.cs`, `StageProbabilityTests.cs`, `TuningScenarioTests.cs`, `Phase3B2CompatibilityTests.cs`), `docs/decisions/ADR-006.md`, this report, the `verify.ps1` logs. Untracked files that pre-existed and are unrelated: `TASK.md`, `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf`.

## 10. Known limits, unsolved issues and self-assessment

- The numbers are virtual. The least defensible are the swing / call / whiff rows, the 10 batted-ball and defence rows and the 4 advancement rows (ADR-006 item 7).
- Stage coverage is deliberately partial: no steals, wild pitch / passed ball, double plays, sacrifice-fly variants (C38), per-pitch-type effects, fatigue or count-dependent baselines; the count enters only as a modifier choice.
- Exact tendency neutrality is reference-relative; league-average neutrality is not claimed (section 4).
- The single `ZoneReferenceDistribution` mismatch branch (`Tendency.reference_mismatch`) is defensive and untested: with only one registered reference no mismatching profile can be built.
- The tolerance rule uses `Math.Sqrt` inside the TEST code only; production code has no floating point.
- `ADR-004` still shows its original "Proposed" header (not edited; only ADR-005's was updated as instructed).
- Not run: sampled calibration (out of scope), manual UI / FHD (no UI change), independent Codex review (`미실시`), performance measurement.

**Self-assessment: PASS WITH NOTES** for the 3-B2 contract work package - every requested contract is implemented and exercised, all executed checks pass, regression is clean, and the notes above (virtual numbers, partial stage coverage, reference-relative neutrality, no independent review) are open. This is not a Phase 3 Gate result and not an approval; stop for GPT Chat (and recommended Codex) review.

---

# Part G — Fix round after the independent Codex review (2026-10-11, `TASK.md`)

Status: **fixed and self-tested; review status stays `검토 대기`** (not approved; no Codex re-review has happened: `미실시`; Phase 3 Gate INCOMPLETE). This part supersedes the test counts and the "Codex `미실시`" wording above. The probability model, normalization and tie rule, caps, tuning numbers, scenarios and targets were **not** touched; the code changes are storage and input checking only.

**G.1 High 1 — `Distribution` handed out its arrays.** Cause: `string[]`/`int[]` fields returned as `IReadOnlyList`; a cast reached the array, so the public table could differ from the private cumulative table `Select` uses (Codex: `[400000, 600000]` → public sum 600000, `Select(0)` unchanged). Fix: `ImmutableArray` copies at creation for outcomes, probabilities and the cumulative table (all from the same values at the same moment). Not done: wrapping the old array in another interface.

**G.2 High 2 — the shared model and the tuning could be changed from outside.** Causes: (a) `ProbabilityModel.V1` published arrays / `List`s of a shared static instance (one consumer's write changed every later consumer; the next ordinary profile load then failed with `Tuning.baseline_outcomes_mismatch … tampered`); (b) `TuningProfile.Modifier(...).Table` was a writable `List` and `ContentHash` was computed once, so a changed factor moved a result (swing 320388 → 348837 ppm on `out.low`; Codex 568966 → 600000) with an unchanged hash. Fix: the model and profile constructors re-create every nested list as `ImmutableArray` (stages, conditions, outcome ids, slots, targets, table orders, tendency stages, modifier tables, scenarios, targets); `ProbabilityTrace` / `TraceModifier` lists too. Chosen over copy-on-read: one rule in one place, no per-call allocation in the resolver's hot path, no reliance on every accessor copying (ADR-006 item 13). Public property types are unchanged (`IReadOnlyList<T>`).

**G.3 Medium — `"baselines": null`.** Cause: `System.Text.Json` accepts an explicit `null` for a `required` member (it only checks presence). Fix: `TuningProfileJson.ReadDraft` checks every required object / array / element after deserialization and returns `Fixture.field_missing` with the path (ADR-006 item 14). Missing members and wrong JSON types stay `Fixture.json_invalid`; the optional `table` is unchanged; null strings keep their existing `Tuning.*` codes; no exception is caught or swallowed.

**G.4 RED evidence (before the fix).** Tests written first, run on the unfixed code; details in `docs/reports/PHASE_3B2_FIX_RED_EVIDENCE.md`. `ProbabilityImmutabilityTests`: 1 pass / 9 fail (Codex counterexample: `Actual int[] [0, 600000]`). `TuningImmutabilityTests`: 2 pass / 6 fail (swing 320388 → 348837 with an unchanged hash; the shared model corrupted for the next load). `TuningJsonNullTests`: 15 pass / 12 fail (`NullReferenceException at TuningProfileJson.ReadDraft`). Tests that already passed before the fix (normal-input selection, copying of creation inputs, missing members, wrong types, shipped values) are kept as regression tests and are not claimed as RED.

**G.5 GREEN.** New tests: `ProbabilityImmutabilityTests` 10 (Domain), `TuningImmutabilityTests` 9, `TuningJsonNullTests` 27 (Infrastructure) = **46**, plus the linked helper `ImmutabilityAudit` (reflection walk of the whole public graph of `Distribution`, `ProbabilityModel.V1`, `TuningProfile` with every `Baseline` / `Modifier`, and a `StageResult`; fails on any array or writable collection). They cover: input array changed after creation; writes to `Ppm` / `Outcomes` through array, `IList` and `List` casts; sum and cumulative selection consistent (`Select(0)`, maximum roll, every interval boundary); nested `Outcomes` of the shared model; the shared `V1` instance seen by two consumers; `Modifier` table writes; creation-time input collections changed afterwards; identical calculation results, tuning content and `ContentHash` after tampering; an independent re-derivation of the hash from the public accessors equals the stored hash; trace / result lists cannot be used to change later results; and the golden values. Full `dotnet test StarPlayer.sln -c Release` (run by Claude): Domain 263, Simulation 371, Application 117, Infrastructure 204 = **955 passed**, 0 failed, 0 skipped (909 before this round + 46); the Phase 2, 3-A, 3-B1 and earlier 3-B2 tests pass unchanged.

**G.6 Before / after.** Pinned by `TuningImmutabilityTests.Normal_calculations_give_exactly_the_results_they_gave_before_the_fix`: SHA-256 `2ffa361ac49bc56cd7133d10916d85320c362dbfd7b54abb71df7028ef2b9988` over the full results (distribution and combined factors) of all 105 conditions under neutral, low-extreme and high-extreme inputs plus the guard / tendency shifts of a mean-neutral vector in every inner zone. The value was produced by the **unfixed** code and is identical after the fix. Tuning `ContentHash` `5b70dca79ec3a3c815f741b57c4c4490e93c5bcdca19900ad452732c66d73ea5` is unchanged (the independent Python `gen_virtual_tuning_v1.py --hash` run after the fix prints the same value); `content/tuning/virtual_tuning_v1.json` was not edited; scenario seeds 2026101101-04, 200,000 samples, the 4.5σ rule, the 8 declared targets, caps 60,000 / 40,000, the GameRules v1 / v2 hashes and the RNG constants / golden vectors are unchanged and their tests pass.

**G.7 Other checks.** `git diff --check`: exit 0 (CRLF conversion warnings only); no trailing whitespace in the new untracked files. Scope of the round — production: `ProbabilityScale.cs`, `ProbabilityModel.cs`, `TuningProfile.cs`, `StageProbability.cs` (trace lists only), `TuningProfileJson.cs`; tests: `ImmutabilityAudit.cs`, `ProbabilityImmutabilityTests.cs`, `TuningImmutabilityTests.cs`, `TuningJsonNullTests.cs`, one linked-file line in the Infrastructure test `.csproj`; documents: ADR-006, this report, `PHASE_3B2_FIX_RED_EVIDENCE.md`, `CURRENT_STATE.md`, `Star/`. No change in `Simulation`, `GameReducer`, `GameEvents`, rule profiles, RNG, `content/` or any 3-C+ feature.

**G.8 `tools/verify.ps1`.** Two consecutive runs after all code and documents were final: `... -LogName PHASE_3B2_FIX_VERIFY_LOG_run1.md` and `..._run2.md` (12:36 and 12:39 +09:00): both **`GATE: PASS`**, exit 0. Release unit tests inside the script: Domain 263, Simulation 371, Application 117, Infrastructure 204 = 955 passed, 0 failed; Godot headless diagnostics, Windows export and the exported-program QHD checks passed unchanged; Markdown link check 73 files. Logs: `docs/reports/PHASE_3B2_FIX_VERIFY_LOG_run1.md`, `docs/reports/PHASE_3B2_FIX_VERIFY_LOG_run2.md`. Run by Claude Code; no independent re-run.

**G.9 Remaining notes.** (1) `PitchZoneModel.Zones` (3-B1, approved) still wraps a `List` in a `ReadOnlyCollection`; its list is not reachable by a cast and the package is outside this fix. (2) A positional record that a consumer constructs itself is the consumer's own object; only the instances the model and profile publish are frozen. (3) Reflection can always write private fields; not a supported route. (4) The Phase 3-C+ handoff (section 8) is unchanged. (5) Codex re-review `미실시`; GPT Chat review pending; nothing is committed.

**Self-assessment (fix round): PASS WITH NOTES** — all three findings were reproduced RED, fixed and GREEN; normal results and the tuning hash are provably unchanged. Not a Phase 3 Gate result and not an approval.

## Git hygiene note (commit step, 2026-10-11)

`git diff --cached --check` reports trailing whitespace only in the four generated verify logs (`PHASE_3B2_VERIFY_LOG_run1/2.md`, `PHASE_3B2_FIX_VERIFY_LOG_run1/2.md`; 37 lines each, 148 total) and nowhere else (the 28 other staged files are clean). Cause: `tools/verify.ps1` writes CRLF line endings and each log contains 2 lone CR bytes, so git classifies the blob as `-text` (no CRLF normalization) and `--check` counts the CR of every line as whitespace. Same pre-existing pattern as the committed Phase 3-B1 logs (`i/-text`). The logs are unmodified generated evidence and were committed as-is; it is not a code, test or content defect.
