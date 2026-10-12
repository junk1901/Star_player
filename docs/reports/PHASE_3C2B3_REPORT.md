# Phase 3-C2B-3 Report — `advancement` resolver (extra-base decisions of a PendingAdvancement)

Date: 2026-10-12 · Owner instruction: `TASK.md` "Phase 3-C2B-3 프로덕션 구현" · Base HEAD `ab7147b619b9f342be043269be77d7f0ca700e6b` (`main`, equal to `origin/main`, no staged or tracked changes at start) · Environment: Windows 11, .NET SDK 8.0.425 (pinned), Godot 4.7.2 Mono · Status: **Phase 3-C2B-3 구현 완료 / 기술 승인, Git 반영 전** (self-assessment in section 20; Codex independent review `PASS WITH NOTES` and GPT Chat technical review `PASS WITH NOTES`, recorded separately in section 21; the `미실시` wording in sections 15 and 20 is the report-time state; **3-C3 NOT STARTED / NOT AUTHORIZED; Phase 3 Gate INCOMPLETE**; nothing staged, committed or pushed).

Scope is 3-C2B-3 only: it implements the owner-approved policies P-01 to P-10 (`docs/reports/PHASE_3C2B3_POLICY_DECISION_DRAFT.md`, `docs/game-engine.md` section 25) in C#. No policy was designed or changed. 3-C3 (adopting H2, the id, the receipt and the state together), Phase 4 and any commit/push/stage are **not done**. The owner's explicit start of this implementation was taken from the instruction "TASK.md를 읽고 실행" given in the chat of this session.

## 1. Changed files (actual)

| File | Change |
|---|---|
| `src/StarPlayer.Simulation/Baseball/AdvancementTypes.cs` | **new** — `AdvancementPolicy` (version constants), `AdvancementOutcome`, `AdvancementReason`, public audit records (`AdvancementAudit`, `AdvancementDecisionAudit`, `AdvancementMoveAudit`), internal working types (`AdvancementContext`, `AdvancementStep`, `AdvancementMove`, `AdvancementComposition`) |
| `src/StarPlayer.Simulation/Baseball/AdvancementRules.cs` | **new** — `AdvancementWork`, `AdvancementRules` (possibility rules R1/R2, the pre-check `Check`, `CountedRuns` over the unchanged `ThirdOutRule`) |
| `src/StarPlayer.Simulation/Baseball/AdvancementPlanning.cs` | **new** — `AdvancementPlanner` (`Contexts`, `Compose`) and the public entry point `AdvancementPlanning.Resolve` |
| `src/StarPlayer.Simulation/Baseball/AdvancementEventBuilder.cs` | **new** — `Build` (final `InPlayEvent`) and `Verify` (reducer-derived record against the rules) |
| `src/StarPlayer.Simulation/Baseball/InPlayCandidateTypes.cs` | `InPlayAudit` gets one optional trailing field `AdvancementDetail = null`; `InPlayPitchCandidate` gets a second internal constructor that takes the completed audit. The existing constructor and every earlier construction are unchanged |
| `src/StarPlayer.Simulation/Baseball/InPlayPitchPlanning.cs` | the four checks every in-play candidate shares were extracted **unchanged and in the same order** from `Verify` into `internal VerifyApplied`; `Verify` calls it first |
| `tests/StarPlayer.Simulation.Tests/Advancement/AdvancementPlannerTests.cs` | **new** — 23 tests (pure planner, 5,562 raw vectors) |
| `tests/StarPlayer.Infrastructure.Tests/AdvancementPlanningTests.cs` | **new** — 36 tests (real `PreparedPitch` → `PendingAdvancement` → `Resolve`) |
| `tests/StarPlayer.Application.Tests/HiddenTendencyBoundaryTests.cs` | the three public audit types added to the resolver-internal signature guard |
| `docs/reports/PHASE_3C2B3_REPORT.md` (this), `…_BASELINE_XUNIT_raw.txt`, `…_RED_A_raw.txt`, `…_RED_B_raw.txt`, `…_GREEN_A_raw.txt`, `…_GREEN_B_raw.txt`, `…_FULL_XUNIT_raw.txt`, `…_MUTATION_raw.txt`, `…_MUTATION_raw_round2.txt`, `…_MUTATION_raw_round3.txt`, `…_VERIFY_LOG_run1/2.md`, `…_VERIFY_STDOUT_run1/2.txt` | new evidence |
| `docs/decisions/ADR-007.md` (3-C2B-3 implementation section), `docs/game-engine.md` (§25 history note, new §26), `docs/SYSTEM_CONTRACTS.md` (C08 status line), `CURRENT_STATE.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/00_Dashboard.md`, `Star/02_Decisions/2026-10-12_C2B3_Resolver_Interpretations.md` | documentation |

Not touched: `GameReducer*`, `GameEvents.cs`, `EventFingerprint`, event JSON, `PitchBranchResolver`, `PitchBranchDraft`, `PitchPreparation.cs` (incl. `CheckPrepared`), RNG (`GameRandom`, `Pcg32`, stream ids), probability model, `content/tuning/virtual_tuning_v1.json`, rule profiles, golden vectors and the Python oracles, the 3-C2B-3-P test-only model/judge and their tests, UI, DB. `TASK.md` and the two rule-book PDFs stay untracked and unchanged. No `git add`, commit or push; no Codex run.

## 2. New internal API and type structure

- `AdvancementPlanning.Resolve(PendingAdvancement? pending, GameState? state, GameRandomSnapshot? rng /* H0 */, TuningProfile? tuning, PlayerSheet? batter, PlayerSheet? pitcher, int lastIssuedEventSequence, CancellationToken ct = default) -> Result<InPlayPitchCandidate>` — public, like `InPlayPitchPlanning.Build`; it accepts a `PendingAdvancement` only (no draft-injection API, no test-only public API). An internal overload adds a stage hook (tests cancel at a chosen stage).
- `AdvancementPlanner.Contexts(facts) -> Result<ImmutableArray<AdvancementContext>>` (lead-runner-first) and `Compose(facts, state, drawn) -> Result<AdvancementComposition>` (internal, pure: the drawn results are an explicit input, no random number).
- `AdvancementRules` (internal): `Start`, `IsEnded`, `TargetOpen`, `Apply`, `Check(facts, state, contexts, actions)` (names every forbidden situation of a vector applied exactly as given), `CountedRuns` (delegates to `ThirdOutRule.Evaluate`).
- `AdvancementEventBuilder` (internal): `Build(facts, composition, id, location)`, `Verify(facts, original, composition, event, applied, out record)`.
- Public result records: `AdvancementAudit` / `AdvancementDecisionAudit` / `AdvancementMoveAudit` (reachable through `InPlayPitchCandidate.Audit.AdvancementDetail`); pinned Simulation-internal by `HiddenTendencyBoundaryTests`.
- Reused, not copied: `PitchPreparation.CheckPrepared`, `InPlayBasePlanner.BuildEvent`, `InPlayPitchPlanning.VerifyApplied`, `StageProbability.Compute`, `Distribution.SelectIndex`, `GameRandom.Restore/Atomically`, `GameEventIds`, `ThirdOutRule`, `GameReducer.Apply`, `InPlayWinningRunner`. The test-only policy model/judge of 3-C2B-3-P is **not** referenced by production code; it is used only as an independent oracle in tests.

## 3. How `CheckPrepared` is reused

`Resolve` calls `PitchPreparation.CheckPrepared(pending?.Plan.Prepared, state, rng, tuning, batter, pitcher)` as its **first** act after the first stage hook, before any RNG restore, id or draw. The method, its codes and its priority are unchanged (this phase did not edit `PitchPreparation.cs`): inputs present (`prepared_missing` also covers a null pending) → draft contract → same game → restorable snapshot of that game → **H0 equals the prepared start** (`rng_state_changed`) → current batter/pitcher ids → same batter/pitcher/handedness → rules, tuning, probability model → the very same state object (`state_stale`, last). A test passes 17 stale or missing inputs and checks that the hook saw only the `start` stage, i.e. that no restore, id or draw happened (section 12/14).

## 4. P-01 to P-10 against the code

| Policy | Implementation | Tests (excerpt) |
|---|---|---|
| P-01 lead runner first | `AdvancementPlanner.Contexts` orders by start base descending; `AdvancementKeys` stored order untouched | `Contexts_are_in_lead_runner_first_order…` (270 two-decision cases), `Every_declared_context_appears_exactly_once…`, `A_composite_play_is_resolved_lead_runner_first…` |
| P-02 sequential conditional composition, Void | `Compose` applies each drawn result to the play as it stands; `IsEnded` (third out or winning run) → `Void`/`PlayOver` | `Hold_and_Void_are_different_facts…`, `The_lead_runners_result_is_applied_before…`, `A_decision_after_the_third_out_is_Void_not_Hold…` |
| P-03 Hold attribution (policy B) | a drawn Extra/Out onto an occupied target base → `Hold`/`OccupiedBase`; nothing is re-drawn or renormalised; drawn and applied are both kept | `A_blocked_extra_base_or_out_is_attributed_to_Hold…` |
| P-04 legality pre-check | `AdvancementRules.Check`; `Compose` re-checks its own result; the finished event is re-validated by the unchanged reducer | `The_rules_pre_check_flags_exactly_the_840_vectors…`, `The_rules_check_names_each_forbidden_situation`, `The_rules_catch_a_passed_runner…`, `A_run_after_the_winning_run_is_flagged…` |
| P-05 Prototype Sequence Policy v1 | explicit `Sequence` 1..n, descending start base, on every run and out when the play has any | `Every_out_and_run_of_a_play_carries_an_explicit_sequence…`, `The_318_legal_vectors…`, `A_run_before_a_non_force_third_out_counts…` |
| P-06 event input vs. derived record | real `InPlayResult` (caught fly = `Out`); `Verify` compares the reducer's `ScoredPlay`/stat entries with the rules and never edits the event | `A_fly_out_with_the_third_base_runner_scoring…` (Simulation and Infrastructure), `A_sacrifice_fly_can_win_the_game…` |
| P-07 H0→H1→H2, fixed consumption | H0 by `CheckPrepared`; `Restore(H1, H1.StateHash)` into a private copy; one `NextBounded(1,000,000)` per context on `game/runner@1` inside `Atomically` | `A_real_pending_advancement_becomes_one…_H2`, `Every_resolution_draws_exactly_one_roll_per_declared_context…` |
| P-08 neutral ability | no rating read, no modifier supplied; the ppm are the approved baseline rows | `The_drawn_results_are_the_approved_rows…`, `The_resolver_reads_no_player_ability…` |
| P-09 audit | `AdvancementAudit` (section 5) with event input and reducer-derived record as separate fields | asserted in the Infrastructure tests above |
| P-10 candidate generation | `Resolve` signature, `CheckPrepared` first, then the id boundary; the existing `InPlayPitchCandidate` with `NextRng` = H2; sibling types stay unconvertible | `A_pending_made_for_other_inputs_is_refused…`, `The_event_id_boundary_is_checked_after_validation…`, `A_pending_cannot_be_mistaken_for_a_candidate…` |

## 5. Pending → final candidate

For a real pending: `CheckPrepared` → H1 restored → contexts and approved rows → working id (`GameEventIds.Create(gameId, last).Next()`, receipt collision) → K rolls → results via `Distribution.SelectIndex` → `Compose` → `Build` (the base plan's batted ball, real `InPlayResult`, credits, location and base moves with each decided runner's final move; a thrown-out runner is a tag out at the target base credited to the retriever (assist) and the base fielder (putout)) → `GameReducer.Apply(original, event)` → `Verify` (shared `VerifyApplied`: refusal passed through, no duplicate, the candidate's own `ef2` receipt, exactly one pitch counted for the original pitcher; then the official-record comparison of section 11) → `InPlayPitchCandidate(plan, event, step, H2, audit)`.

Verified on a real example (seed 12 of the scenario "first and second, nobody out, a single", cursor 12): one `InPlayEvent` `game:3c2b3:probe:e000013`, `Single`, the prepared pitch's location, `ef2` receipt, one scored play, one more receipt than the original state, the original pitcher's pitch count + 1, `NextRng` ≠ H1 ≠ H0, the original `GameState`, H0, the pending's H1 and the cursor unchanged. Audit record fields: policy `advancement-policy-v1`, sequence policy `prototype-sequence-v1`, hold policy `B`, coupling `attempt-coupled-v1`, per decision (order, context, runner, start/plan/target base, exact roll, raw outputs, approved ppm, drawn, applied, reason, final base, sequence), H0/H1/H2 hashes, logical and raw draw counts, event input, reducer-derived record, winning scorer, record changes, verification result `reducer_verified`.

## 6. H0 → H1 → H2 evidence

- `PendingAdvancement.RngAfterPitchDraw` (H1) differs from H0; the audit's start hash is H0, `after_pitch` is H1, `final` is H2 (= the candidate's `NextRng.StateHash`); H2 equals the position that the test derives independently with the existing primitives (`GameRandom.Restore(H1)`, K × `NextBounded(1,000,000)` on `game/runner@1`, `Snapshot`) — snapshot canonical text equal, the exact rolls equal the audit's rolls, the raw outputs equal the audit's.
- Fixed consumption: for the first 150 resolutions of each of three scenarios (K = 2 with a blocked or voided second decision, K = 2 with two outs, K = 1) H2 equals the K-roll derivation **whatever was applied**, only the runner stream is added to H1 and every other stream is unchanged. Both an `OccupiedBase` Hold and a `Void` occur in the scanned plays, so the fixed consumption was exercised for them.
- Raw `Draws` can exceed the call count; the audit keeps both (`LogicalDraws` = K, `RawDraws` ≥ K); no test assumes a fixed raw count.
- Passing H1 as the "current" RNG is refused (`rng_state_changed`); a failed or cancelled call changes neither H0 nor H1; a retry draws the identical rolls (sections 12, 14).

## 7. Hold and Void

Measured on the fixed-seed scenarios of the Infrastructure tests (pending plays with a real single; a diagnostic run, not part of the suite): first and second, nobody out, 500 plays → applied vectors lead/trailing `EE` 70, `EH` 210, `EO` 6, `HH` 200 (of which 42 were a drawn Extra and 6 a drawn Out blocked by the lead runner staying on third), `OE` 5, `OH` 9; first and second, two out, 900 plays → `EE` 134, `EH` 372, `EO` 16, `HH` 354, `OV` 24 (the lead runner's out is the third out, the trailing decision is Void: 8 drawn Extra, 16 drawn Hold). The seven reachable combinations are `EE EH EO HH OE OH OO`; `HE`/`HO` never occur (they are `HH`). Void keeps the runner on the base plan's base, consumes its roll and records the drawn result; Hold-by-occupancy records reason `OccupiedBase`; a drawn Hold on an occupied base has reason `None`; a drawn Hold after the play ended is **Void**, not Hold (interpretation, `Star/02_Decisions/2026-10-12_C2B3_Resolver_Interpretations.md`).

## 8. Composite plays (two runners)

A single with first and second occupied is the only two-decision situation (270 of the 1,314 pending cases). The lead runner (second base) is decided first; the trailing runner (first base) is then judged on the play as it stands: lead scores → third is free (extra base, hold, or thrown out at third); lead holds → third is occupied (hold only); lead thrown out → third is free again and, with two outs, the play is over (Void). Unit tests cover each branch; the 5,562-vector sweep covers all of them for all 1,314 cases (section 9).

## 9. Illegal action vectors (the 840)

All 5,562 raw vectors (3^K per case, 1,314 cases) were applied to production code:

| Measure | Result |
|---|---:|
| raw vectors | 5,562 |
| legal by the independent test-only judge | 4,722 |
| illegal | 840 |
| illegal and **accepted by the unchanged reducer** when built as given | 480 |
| `AdvancementRules.Check` flags exactly the judge's illegal set, none of the legal ones | 5,562 / 5,562 agree |
| legal vector composed with no moving result changed | 4,722 / 4,722 (a drawn Hold after the end is recorded Hold→Void; no runner moves differently) |
| illegal vector composed into a different (legal) play | 840 / 840 |
| composed play legal by the judge, by `Check`, accepted by the reducer, runs/outs/half end/walk-off/winner equal to the judge | 5,562 / 5,562 |
| composed play whose outs exceed three | 0 |

"The reducer accepted it" is therefore not used as the test of legality: 480 illegal vectors are accepted by the reducer and refused by the policy layer.

## 10. The 318 sequence-sensitive plays

Among the legal vectors, those that end the half-inning and contain counted runs number **318** (equal to the policy validation's figure). In production they count their run(s) because the lead runner's run precedes the trailing runner's non-force out; the same facts rebuilt with the trailing runner's out first make the reducer count **0** runs for all 318. The verification refuses that reducer result for the production composition and names the counted runs. The `Sequence` is therefore an official-record input, not metadata. Force-out and batter-runner third outs (which void every run whatever the order) cannot be produced by the four contexts; the rule is tested on synthetic terminals and against the unchanged reducer with two hand-built events (`The_counted_run_rule_agrees_with_the_unchanged_reducer…`).

## 11. Walk-off and the official record

- The winner is recomputed from the final plan: the need-th run in sequence order (`InPlayWinningRunner`, `ExistingRunner`); the base plan's winner is null for a pending. Verified for every walk-off vector (equal to the judge's winner; exactly `need` runs; no run after the winning run; the credited base cannot shrink: `min(bases, 4 − winner's start base) = bases` in all of them, so no record change was needed).
- Extra base scores the winning run → game ends (`Final`, `HomeWin`), trailing decision Void, `WalkOffEffect = GameEnds`, record change `walkoff_game_ends`; failed attempt → game continues, no winner; sacrifice-fly walk-off (fly out, third-base runner scores) → `Out` event, reducer-derived `BatterResult.SacrificeFly`, no at-bat, one RBI, winner = the runner from third; a third out with an earlier counted run that does not win → the inning ends, the game does not (tied → extra inning).
- Sacrifice fly: event `InPlayResult.Out`, `Batted = Fly`; the reducer derives `BatterResult.SacrificeFly` only when the batter is the play's only out, fewer than two outs before the play and a run scored. `Verify` computes that expectation independently and compares at-bat (none), RBI, runs, outs, half end and left on base with the reducer's `ScoredPlay` and stat entries. Fly out with the runner thrown out (two outs on the play) is an ordinary `Out`; with one out before the play the runner's out is the third out (inning over).
- No `InPlayResult.SacrificeFly` was added; the event is never changed after `Apply`.

## 12. Real `PreparedPitch` integration

All 36 Infrastructure tests start from `PitchPreparation.Prepare` (real 3-C2A resolver on real seeded streams) and `InPlayPitchPlanning.Build`; the pendings come from scanning fixed seeds in real, reducer-built game states (first and second with 0/2 outs, second only with 0/1/2 outs, third with 0/1 outs (fly), first only (double), and bottom-of-the-9th tied states). Forged objects, each named in its test: a rebuilt state object, other game ids/RNG contexts, a changed H0, H1 passed as current, other sheets and handedness, other rules/tuning, a forged `PreparedPitch` with another probability model or draft contract version, a pending with an unknown or duplicated context or a phantom runner.
Checks: state reference identity (`state_stale`), game id, H0/H1/H2, current batter/pitcher, outcome of each extra base, `ef2`, the unchanged reducer, deterministic retry (same event canonical text, same id, same H2, same vector), the id derived from the cursor only, 5σ frequency of the drawn results for a lone second-base runner (300 plays: 59 / 38 / 3 %).

## 13. EventId, receipts, GameState

The working id comes from `GameEventIds.Create(gameId, last).Next()` on a private counter; the caller's cursor is an `int` passed by value and never changes. Boundary: `last < 0` or `> 999,999` → `Id.sequence_invalid`; `last = 999,999` → `Id.sequence_exhausted`; a next id already in the state's receipts → `InPlayCandidate.event_id_in_use` (tested with a state whose receipts start at `e000001` and a cursor 0); these are reached only after validation and the H1 restore (hook saw `start, validated, restored`). A cursor far above the receipts leaves a numbering gap that this layer cannot see and accepts (`e500001`); 3-C3 must compare it with the real cursor. The caller's state (digest, applied-event count), H0, the pending's H1 and the pending are unchanged after every success, failure and cancellation (asserted in each test). A successful candidate has exactly one new receipt, `ef2`, in the **preview** state only.

## 14. Cancellation and failure

Stages, in order: `start`, `validated`, `restored`, `id_issued`, `drawing` (once per decision), `drawn`, `composed`, `event_built`, `before_reducer`, `after_reducer`, `verified`. A theory cancels at each stage (and at the second `drawing` for a two-decision pending): `OperationCanceledException`, nothing ran past the cancelled stage, state/H0/H1/cursor/receipts untouched, no partial candidate, and a retry gives the same id, event text and H2. A pre-cancelled token stops at `start`. Failures after validation (unknown/duplicated context, phantom runner refused by the reducer with its own `Game.move_source_invalid`, id errors) leave every input untouched and the untouched inputs still resolve. `CheckPrepared` itself has no cancellation point; it is bracketed by `start` and `validated`.

## 15. Tests actually run and raw evidence

All in Release on the final tree unless noted; the run commands are `dotnet test <project> -c Release --filter …` and `dotnet test StarPlayer.sln -c Release`.

| Evidence | Result | File |
|---|---|---|
| Baseline before any change | 1,454 pass (Domain 263, Application 117, Simulation 597, Infrastructure 477) | `PHASE_3C2B3_BASELINE_XUNIT_raw.txt` |
| Stage A RED (stubs throwing `NotImplementedException`) | 20 failed / 0 passed | `PHASE_3C2B3_RED_A_raw.txt` |
| Stage A GREEN | 23 / 23 | `PHASE_3C2B3_GREEN_A_raw.txt` |
| Stage B RED (stub `Resolve`) | 35 failed / 1 passed | `PHASE_3C2B3_RED_B_raw.txt` |
| Stage B GREEN | 36 / 36 | `PHASE_3C2B3_GREEN_B_raw.txt` |
| Full xUnit, final tree | **1,513 pass, 0 fail** (Domain 263, Application 117, Simulation 620, Infrastructure 513); 1,454 + 59 new | `PHASE_3C2B3_FULL_XUNIT_raw.txt` |
| Defect injection | 25 mutants + 1 no-op control (26 ids): 24 mutants detected at behaviour level; **M15 detected only at message level** (survived its first effective run; see section 21); the control survived as designed | `PHASE_3C2B3_MUTATION_raw*.txt` |
| `tools/verify.ps1` run 1 | `GATE: PASS` (all checks PASS: build 0 warnings, Domain 263 / Simulation 620 / Application 117 / Infrastructure 513, Godot headless G1–G4, export E1–E4, docs links 118 files; run by Claude on the final tree, 2026-10-12 22:57) | `PHASE_3C2B3_VERIFY_LOG_run1.md` |
| `tools/verify.ps1` run 2 | `GATE: PASS` (the same checks, same counts, links 119 files because run 1 added its own log; run by Claude, 2026-10-12 23:00, consecutive with run 1) | `PHASE_3C2B3_VERIFY_LOG_run2.md` |

RED/GREEN honesty: three tests added after the first GREEN (`The_counted_run_rule_agrees_with_the_unchanged_reducer…`, `A_run_after_the_winning_run_is_flagged…`, `A_scoring_play_that_also_makes_the_third_out…`) and some assertions added to existing tests tested existing reducer/rule behaviour and passed on their first run, so they have no RED of their own (stage A had 20 tests at RED time and has 23 now).

Defect injection (raw files; three rounds, restored byte-identically after every mutation, verified by hash): round 1 detected M01–M07, M11, M12, M16, M18–M20, M25, M26; nine mutations were reported "pattern missing" because the script assumed CRLF while the four new source files have LF, and two (M09, M15) did not compile (unreachable-code warnings are errors) — a test-infrastructure defect, fixed. Round 2 re-ran those: M08, M09, M10, M13, M14, M17, M21, M22, M23 detected; **M15 survived** (removing the comparison of the reducer's counted-run number: the same fact is also checked through the per-runner `RunVoided` flags and the RBI count; no input of the verified scope made a game-result difference visible, only the refusal message differs). Round 3, after the verification test was strengthened to require the message that names the counted runs: M15 detected - **by that error-message assertion, not by a new game-result error** (classification and counts in section 21). The control M24 (a mutation that changes nothing) survived as designed, which shows that survivors are reported. The mutants: lead/trail order, Void→Hold, uncoupled out, lost reason, winning run not ending the play, third out not ending the play, occupancy ignored, reversed or missing sequence, one roll fewer, H0 instead of H1, wrong stream, removed stale check, removed id collision check, removed counted-run comparison, wrong out base, no sacrifice fly, first run as winner, off-by-one id, H2 = H1, removed stage hook, an out not counted, overtaking unflagged, swapped outcome index, audit walk-off effect.

## 16. Existing regressions

Unchanged and passing in the full run: C2B-1 63 tests and its check contract, the 5,400-case C2B-2 oracle comparison, the 19 templates, 3-C2A and 3-A golden vectors, `ef1`/`ef2` pins and the 30,974 receipt digests, event JSON, the official-record tests, `HiddenTendencyBoundaryTests` (extended by three types), the 67 C2B-3-P policy tests (60 Simulation + 7 Infrastructure hand-off). `git status` shows no change to `tests/Fixtures/`, `content/` or any earlier evidence file; the tuning content hash is pinned by a test (`5b70dca7…3ea5`).

## 17. New findings

- Critical / High / Medium: none found.
- Low: (a) the survived mutation M15 (redundant verification checks) — message-level assertion added, no new behaviour-level detection (section 21); (b) the mutation script's CRLF assumption — script defect only; (c) `AdvancementRules.Check` and `Compose` rely on every base-plan scorer starting on a higher base than every decided runner (true for the four contexts; stated in code comments and ADR-007) — a new context must re-approve it; (d) the four new source files have LF line endings in the working tree while the existing files are CRLF (`core.autocrlf=true`; Git normalises on add, no effect on content).
- Observations kept from the policy (not new): Hold attribution lowers the trailing runner's extra-base rate in composite plays (the policy's R-5); the composite situation is the only source of `OccupiedBase`.

## 18. Remaining notes

1. 3-C3: adopt H2, the id, the receipt and the state together; re-check that the authoritative RNG is still H0 and that the real last issued sequence is unchanged, including a numbering gap that `Resolve` cannot see.
2. KBO 2026 certification of the rules used (temporary OBR basis) and of the prototype sequence assumption; replacement by a time-based running model stays possible.
3. Per-player running/throwing ability (P-08 neutral): a separate versioned contract; composite-context and extra-base calibration are 3-F.
4. The audit is Simulation-internal and not persisted (Phase 4).
5. The expected values for the sweeps come from the 3-C2B-3-P test-only judge written by the same author as the policy; the unchanged reducer is the second, independent check.
6. Codex review of this implementation is recommended (core simulation rules, multi-module): `미실시`.

## 19. Git

`git status -sb` at the end: branch `main`, equal to `origin/main`, modified tracked: `docs/SYSTEM_CONTRACTS.md`, `docs/decisions/ADR-007.md`, `docs/game-engine.md`, `src/StarPlayer.Simulation/Baseball/InPlayCandidateTypes.cs`, `src/StarPlayer.Simulation/Baseball/InPlayPitchPlanning.cs`, `tests/StarPlayer.Application.Tests/HiddenTendencyBoundaryTests.cs`, `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`; new untracked: the four `Advancement*.cs`, the two new test files, `Star/02_Decisions/2026-10-12_C2B3_Resolver_Interpretations.md` and the evidence files above; pre-existing untracked and untouched: `TASK.md`, `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf`. Nothing is staged, committed or pushed. `git diff --check` is clean (only the line-ending notice for one LF file).

## 20. Self-assessment

**PASS WITH NOTES** (Claude Code's own judgement; not an external approval). All acceptance items of the instruction were implemented and tested with actual runs; the notes are sections 17 and 18. GPT Chat review `미실시`, Codex review `미실시`. Phase 3 Gate **INCOMPLETE**; 3-C3 and Phase 4 not started; nothing committed or pushed.

## 21. Final review follow-up (2026-10-12, owner `TASK.md` "Final Review Follow-up"; documentation correction only)

**Review results (recorded separately; relayed by the owner, not re-run here).** Codex Medium independent focused review of base HEAD `ab7147b`: **`PASS WITH NOTES`**, Critical / High / Medium 0. GPT Chat technical review: **`PASS WITH NOTES`** (technical approval of 3-C2B-3; two Low documentation mismatches to fix before commit - this section and ADR-007). GPT Chat did not run the full test suite itself; the 1,513-test figure is Claude Code's run (`PHASE_3C2B3_FULL_XUNIT_raw.txt`). Status: 3-C2B-3 implemented and technically approved, **Git not yet done** (no staging, commit or push); **3-C3 NOT STARTED / NOT AUTHORIZED**; **Phase 3 Gate INCOMPLETE**. Sections 1-20 are kept as written at report time.

**Defect-injection classification (raw files unchanged).** 26 ids = 25 mutants (M01-M26 without M24) + the no-op control M24. Round 1 applied 15 ids effectively; 9 ids were `NOT-APPLIED` (the CRLF pattern defect; M24 among them) and M09/M15 were build errors, so those 11 ids were first effectively applied in round 2.

| Classification | Count | Ids |
|---|---|---|
| Mutants applied effectively | 25 | M01-M23, M25, M26 |
| Detected at behaviour level (a game-result, state, RNG, ID or contract difference) | 24 | all of the above except M15 |
| Survived its first effective run, then detected **only by an error-message assertion** | 1 | M15 (round 2 `SURVIVED failures=0` -> round 3 `DETECTED failures=1`) |
| Survived control (as designed) | 1 | M24 (round 2 `SURVIVED failures=0`) |

M15 removes the comparison between the reducer's counted-run number and the composed one. Other checks (per-runner `RunVoided` flags, RBI) watch the same outcome, so within the verified scope no result difference appeared. The round-3 detection comes from the added assertion that the refusal message names the counted runs (`AdvancementPlannerTests.cs`, `Assert.Contains("counted runs", ...)`), so it is a message-level detection and does not prove that a new game-result error is caught. This is *not* a claim that M15 is mathematically equivalent; it is the recorded fact that redundant defences hid it in the current scope. Wherever "all 25 detected" appears in the status documents, read: 24 behaviour-level + 1 message-level (M15) + 1 control survivor. Detection rate: behaviour-level 24/25; including the message-level detection 25/25.

**Test count.** The final full xUnit run is **1,513 pass / 0 fail** (1,454 + 59 new = stage A 23 + stage B 36). `ADR-007` had `1,511` and `Stage A 21`; both are corrected to 1,513 and 23. Earlier dated runs (1,451 / 1,454 baselines, earlier packages) are unchanged.

**Not re-run in this follow-up** (documentation only): full xUnit and `tools/verify.ps1` - cited from the existing evidence (`PHASE_3C2B3_FULL_XUNIT_raw.txt`, `PHASE_3C2B3_VERIFY_LOG_run1/2.md`). Checked in this step: production/test files and all raw evidence unchanged (hashes), Markdown links, `git diff --check`.
