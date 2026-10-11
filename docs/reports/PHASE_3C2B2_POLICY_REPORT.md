# Phase 3-C2B-2-P Report — Policy lock and template feasibility verification

Date: 2026-10-12 · Owner instruction: `TASK.md` · Base HEAD `eca11bb222f19604e4c63ad5605ad622e93d7306` · Status: **policy D1–D5 Owner Accepted; detailed templates verified by test-only tooling, GPT Chat final review `PASS WITH NOTES` (feasibility verification only; figures are Claude Code's submitted run results; Codex review `미실시`); 3-C2B-2 implementation NOT started; no approval of any implementation**

This package is the preparation step before 3-C2B-2: it records the owner's prototype policies D1–D5 and proves, with the real `GameReducer.Apply`, that every positive-probability in-play combination can be written as a legal virtual play. **No production code was written or changed** (`src/` is untouched; `git status` below). 3-C2B-2, 3-C2B-3 (the `advancement` draw), 3-C3 (joint atomicity) and Phase 4 are not started; the Phase 3 Gate is INCOMPLETE. GPT Chat review `미실시`, Codex review `미실시` (not started automatically). Nothing was committed or pushed.

## 1. Changed files (actual)

| File | Change |
|---|---|
| `docs/decisions/ADR-007.md` | status line + split-table row + new section "Decisions (3-C2B-2 prototype policy)" (12 items); earlier 3-C1 / 3-C2A / 3-C2B-1 records untouched |
| `docs/game-engine.md` | new section 23 (policy contract, templates, base runner table, official record, advancement rule, Infield Fly, type boundary, audit contract) |
| `docs/SYSTEM_CONTRACTS.md` | C08: one status bullet |
| `CURRENT_STATE.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/00_Dashboard.md` | status, findings and next task (3-C2B-1 approval records preserved) |
| `tests/StarPlayer.Simulation.Tests/InPlayFeasibility/InPlayTemplates.cs`, `InPlayStates.cs`, `InPlayHarness.cs`, `InPlayFeasibilityTests.cs` | **new, test-only**: template catalogue + planner (a candidate reading of the policy, NOT the production generator), real-state builder, harness, 29 tests |
| `tests/Fixtures/generator/generate_inplay_feasibility_oracle.py`, `tests/Fixtures/phase3c2b2p_inplay_oracle.json` | **new**: independent Python oracle and its expected values (5,400 cases, 19 templates, 60 combinations) |
| `tests/Fixtures/generator/render_inplay_feasibility_matrix.py` | **new**: renders the matrix from the raw results |
| `docs/reports/PHASE_3C2B2_POLICY_REPORT.md` (this), `PHASE_3C2B2_TEMPLATE_MATRIX.md`, `PHASE_3C2B2_FEASIBILITY_RAW.json`, `PHASE_3C2B2P_FEASIBILITY_TEST_raw.txt`, `PHASE_3C2B2P_FULL_XUNIT_raw.txt`, `PHASE_3C2B2P_VERIFY_LOG_run1/2.md` | new evidence |

Not touched: everything under `src/`, `GameReducer*`, `GameEvents.cs`, `EventFingerprint`, event JSON, `PitchBranchResolver`, `PitchPreparation`, RNG, probability model, `content/tuning/virtual_tuning_v1.json`, rule profiles, golden vectors, UI, DB. `TASK.md` and the two rule-book PDFs stay untracked and unchanged; no `git add`, commit or push was run.

## 2. D1–D5 recorded in the ADR

ADR-007 "Decisions (3-C2B-2 prototype policy) — Owner Accepted, Implementation Pending", twelve items in the order requested: (1) approval and scope; (2) D1 meaning of a drawn long hit as the approved interpretation of `inplay-mapping-v1` (the 3-C2A draw and `pitch-branch-v1` unchanged); (3) versions and the walk-off rule; (4) relation to the 3-C2A meaning; (5) fact generation vs. official record; (6) D3 batter destination limit (a generation limit, not a baseball claim); (7) D2 out-of-the-park home run; (8) D4 temporary 2026 OBR basis with the explicit statement that the KBO clauses were **not** certified; (9) D5 audit and replay contract; (10) 3-C2B-2 / 3-C2B-3 / 3-C3 / Phase 4 boundary; (11) unresolved technical matters U-1..U-7; (12) policy approval distinct from implementation approval. No sentence says that 3-C2B-2 code exists. The instruction does not state the date of the owner's approval; the ADR is dated by the day it was written (2026-10-12) and says so.

## 3. Policy versions and contracts

`inplay-mapping-v1` (generation), `walkoff-credit-v1` (official record of a game-ending hit), `inplay-template-v1` (19 templates). Contract text: `docs/game-engine.md` section 23. The rule basis is the 2026 OBR original text read for this package (9.06(a), 9.06(b) and its comment, 9.06(f) and its comment, 9.06(g), 7.01(e)(3) and its exception, the Infield Fly definition and comment). **The 2026 KBO clauses were not found or certified**; the prototype simplifications are not presented as KBO certification anywhere.

## 4. The 45 positive combinations (check)

Read from `content/tuning/virtual_tuning_v1.json` by the test itself (not copied): 10 batted balls x 6 defence results = **60** nominal combinations; **45** positive and **15** exactly 0 ppm. Per batted ball (hand-counted from the ppm rows, asserted): ground weak 3, ground medium 4, ground hard 4, line weak 4, line medium 5, line hard 6, fly weak 4, fly medium 6, fly hard 6, popup 3 = 45. The 15 zero combinations are the hand-derived list in the matrix (asserted as an exact set). `popup/single` (5,000 ppm) and `popup/reached_on_error` (20,000 ppm) are positive and have templates. The defence-result distribution never yields a fielder's choice. All 60 are listed in [the matrix](PHASE_3C2B2_TEMPLATE_MATRIX.md) with the twelve requested columns; the 15 zero ones are `UNREACHABLE_ZERO_PPM`, not executed and not counted as passed.

## 5. Base runner table

The 8 situations x 6 results table (moves per runner, explicit holds, causes `Play`/`Forced`) is in game-engine section 23 and, as actually produced by the real run, in matrix section 3. It was also written **by hand first** as literals in the test (`The_base_running_table_matches_the_hand_derived_table_for_all_eight_situations`) and agrees. Key choices: a ground out with fewer than two outs moves the forced runners one base, other outs hold everyone, a third-out play holds everyone (so no out/run order is ever needed), hits move each runner the bases of the hit, a reached-on-error pushes only forced runners (`MoveCause.Forced`) and gives the batter no separate move.

## 6. Fielding and error templates

19 templates (matrix section 2): ground out SS assist + 1B putout; line out SS; fly out CF; pop out 2B; ROE E6 (ground, line), E8 (fly, pop); hits without any credit. Checked on every executed case: exactly one putout per out, assists only where a throw exists, an error entry for every error, no credit on a hit, every credited position has a defender in the current defence, the weak/medium/hard intensity kept as audit data. **Bias of fixed fielders** (matrix section 7): 1B takes every ground-out putout, SS every ground-out assist, 2B only pop-out putouts; 3B, LF, RF and C never receive a putout or assist on a ball in play; no double play, fielder's choice or sacrifice; every error is E6 or E8.

## 7. The 1,080 ordinary cases

45 combinations x 8 runner situations x 3 outs, top of the 3rd inning, 0-0, approach chosen. Real states built only through `GameReducer.Apply` (walks, steals, fly outs).

| Result | Cases |
|---|---:|
| nominal cases reviewed | 1,080 |
| executed | 1,080 |
| `LEGAL_BASE` (legal base plan, no extra draw needed) | 780 |
| `PENDING_ADVANCEMENT` (base plan verified legal, 3-C2B-3 required) | 300 |
| `NEEDS_WALKOFF_POLICY` | 0 |
| `UNSUPPORTED_TEMPLATE` | 0 |
| `DEFECT_OR_UNRESOLVED` | 0 |
| reducer refused | 0 |
| independent oracle and policy checks agree | 1,080 |

Pending = exactly the four declared `advancement` contexts of `prob-model-v1` and a game that the base plan does not end: 180 singles (runner on 1B or 2B), 96 doubles (runner on 1B), 24 fly outs (runner on 3B, fewer than two outs), all hand-counted in the test. The reducer accepting a pending base plan never changed its status. Of the 300, 90 are a single named runner and **210 are composite** (F-1). Mirror runs with identical statuses and identical per-case results: bottom of the 3rd (home batting), a game without a designated hitter, a 3-2 count (3 x 1,080 more executed rows).

## 8. The walk-off matrix

Bottom of the 9th, away team ahead by 0, 1, 2 or 3: 45 x 8 x 3 x 4 = **4,320** nominal combinations; all are reachable game states, **none was excluded** as impossible, all executed.

| Result | Cases |
|---|---:|
| `LEGAL_BASE` (includes the games that end with an unchanged official result) | 3,114 |
| `PENDING_ADVANCEMENT` | 1,014 (654 composite) |
| `NEEDS_WALKOFF_POLICY` (legal only through `walkoff-credit-v1`) | 192 |
| `UNSUPPORTED_TEMPLATE` / `DEFECT_OR_UNRESOLVED` | 0 / 0 |
| reducer refused / oracle disagrees | 0 / 0 |

The 192: drawn double -> single (96), triple -> double (48), triple -> single (48). Every non-home-run game-ending play scores exactly the needed runs; a home run scores everyone (`Game.run_after_winning_run` would refuse otherwise). A walk-off base plan is never also pending (the winning run makes later advancement moot). The first extra inning (bottom of the 10th, tied) gives per-case results identical to the tied ninth. When the last half ends without a lead, the game is lost for the home team if it is behind and goes on if tied (checked on every third-out row). Grids for every drawn result are in matrix section 5.1.

## 9. The ten representative cases (hand-derived expectation vs. actual)

The expectations were derived by hand before the first run and are asserted as literals; they are not generated by the oracle or by the planner.

| # | Situation | Hand-derived expectation | Actual (reducer) | Match |
|---|---|---|---|---|
| 1 | tied, 3B, drawn double | official single, 1 run, game over, credit reduced | Single, 1 run, over, `credit_reduced` | yes |
| 2 | tied, loaded, 1 out, drawn double | official single, 1 run, moves 3>4 2>3 1>2, runners 1B/2B/3B left | Single, 1 run, same moves, LOB 3 | yes |
| 3 | tied, 2B, drawn triple | official double (batter reaches second naturally), 1 run | Double, 1 run, `credit_reduced` | yes |
| 4 | tied, 1B, 2 outs, drawn triple | triple unchanged (winner advanced 3), 1 run | Triple, 1 run, `game_ends` | yes |
| 5 | tied, loaded, out-of-the-park home run | 4 runs, home run | HomeRun, 4 runs, 4 RBI | yes |
| 6 | behind 1, 2B+3B, double | 2 runs, double stays | Double, 2 runs, over | yes |
| 7 | behind 2, loaded, 2 outs, triple | 3 runs, triple stays | Triple, 3 runs, over | yes |
| 8 | tied, 2B, single | base plan moves him to 3B, no final score invented -> `PENDING_ADVANCEMENT` | Single, 0 runs, not over, `PENDING_ADVANCEMENT` | yes |
| 9 | tied, loaded, 2 outs, single reached-on-error | forced run scores, `ReachedOnError`, E8, no hit, no RBI | same; run cause `Forced` | yes |
| 10 | tied, nobody on, double | game continues, batter on 2B | Double, 0 runs, not over | yes |

Each case also asserts the reducer accepted the event and the reducer's own checks (all moves, RBI, left on base, bases after). Case 8 is classified `PENDING_ADVANCEMENT` and **no** `InPlayPitchCandidate` is produced for it.

## 10. Popup and the Infield Fly rule

The reducer has no Infield Fly concept, so the templates separate the physical premises: a *pop out* is caught by 2B with ordinary effort (with first and second occupied or bases loaded and fewer than two outs this is an Infield Fly; the batter is out either way, the same record); *pop single* and *pop reached-on-error* are balls beyond any infielder's ordinary effort (shallow CF) so no Infield Fly applies by premise, and the ROE error is the outfielder's (E8) on a ball he could ordinarily catch. Checked for all 72 popup cases (3 results x 8 situations x 3 outs), including first-and-second and bases-loaded with 0 and 1 outs: outs credited to 2B only, runners hold, single and ROE have no infielder credit or error; an injected infielder error is detected. The reducer cannot enforce the premise; that limit is F-5.

## 11. Audit and replay contract

Defined (not implemented) in ADR-007 item 9 and game-engine section 23.8: policy/walk-off/catalogue versions, original batted ball and intensity, original defence result, official result, drawn and official template ids, pre-play state, batter final base, ordered runner moves, winning-run scorer, record-change reason, `GameId` and candidate `EventId`, rules/tuning/probability identities, start and final RNG `StateHash`, advancement state. It is Simulation-internal evidence outside `ef1`/`ef2`; 3-C3 settles it together with event, receipt, state, RNG and ids; Phase 4 stores it. Neither a joint commit nor any DB work was done.

## 12. 3-C2B-2 / 3-C2B-3 boundary

`BaseInPlayPlan` (no RNG; template, moves, official record, flag), `PendingAdvancement` (not a candidate: no event id, no receipt, not adoptable, RNG position unchanged), `InPlayPitchCandidate` (every decision made, reducer-validated on the original state, `ef2` receipt, unconfirmed step, `NextRng`). 3-C2B-2 delivers plans and candidates for complete plans and `PendingAdvancement` for the rest; 3-C2B-3 draws `advancement` on `game/runner@1` and recomputes the record and the third-out order; 3-C3 adopts state, RNG, ids and receipts together. Details: game-engine section 23.7.

## 13. Tests actually run

- New: `InPlayFeasibilityTests` — 29 tests, all passed (`PHASE_3C2B2P_FEASIBILITY_TEST_raw.txt`). Rows executed per run of the suite: 1,080 (ordinary) + 3 x 1,080 (mirrors) + 4,320 (walk-off) + 1,080 (first extra inning) = 9,720, plus 12 x 5,400 = 64,800 rows run with deliberately injected defects (counted as detections, not as passes).
- 12 injected defects (quiet downgrade of long hits, no forced ground-out advance, missing putout, double putout, phantom assist, ROE without error, no walk-off credit, infielder pop error, forced runner not pushed, triple as double, fly-out runner scoring from third, pending treated as legal): **all 12 detected**, the unmodified planner produced 0 disagreements. Reducer refusals and oracle disagreements are both counted (raw JSON `mutant_*`).
- Counterexamples with exact reducer codes: excess runs `Game.run_after_winning_run`; an unreduced tied 3B double **accepted by the reducer but wrong** (caught by the policy check); batter-on-third claim refused `Game.batter_move_invalid`; `Game.runner_move_missing`, `runner_moved_twice`, `runner_passed_runner`, `base_occupied_twice`, `runner_moved_backward`, `too_many_outs`, `play_order_required`, `out_credit_invalid`, `error_missing`; a refused event never changes the state it was applied to (digest compared on every row).
- Reproducibility: every row is planned and applied twice and the event fingerprint and resulting state digest are compared; the planner and harness take no RNG, clock or id counter (asserted).
- Regression: `dotnet test StarPlayer.sln -c Release` **1,205 passed, 0 failed** (Domain 263, Simulation 412, Application 117, Infrastructure 413; baseline 1,176, +29 new), raw output `PHASE_3C2B2P_FULL_XUNIT_raw.txt`.
- `tools/verify.ps1`: two consecutive runs on the final tree, both `GATE: PASS`, exit 0 (`PHASE_3C2B2P_VERIFY_LOG_run1.md`, `PHASE_3C2B2P_VERIFY_LOG_run2.md`; run by Claude; the Godot export/headless checks of the gate also passed; only these result sentences of this report and the status files were edited after the runs).
- `git diff --check`: no output, exit 0. Markdown links: verify check D1 PASS (96 files checked in run 1).

Not run / not applicable to a test-only package: a production 3-C2B-2 test suite (does not exist), RNG consumption of production code (the planner has no RNG; the "zero extra RNG" and "event id cursor unchanged" gates are therefore **requirements for the implementation**, not facts proven here), cancellation of an in-play candidate (no candidate API exists), Codex and GPT Chat review.

## 14. New findings

| ID | Severity | Finding |
|---|---|---|
| F-1 | Medium | `prob-model-v1` declares only four single-runner `advancement` contexts. 210 of 300 pending ordinary cases and 654 of 1,014 pending walk-off cases are composite (another runner on base); the context, order and interaction are undefined. Does not block 3-C2B-2 (the flag and context keys are enough) but blocks a complete in-play loop and must be designed in 3-C2B-3. |
| F-2 | Medium (planning) | 300 of 1,080 nominal ordinary cases (27.8 %) are `PENDING_ADVANCEMENT` (a share of cases, not of probability mass). 3-C2B-2 alone cannot produce an adoptable in-play candidate for them; 3-C2B-3 is mandatory before 3-C3 can adopt in-play. |
| F-3 | Low | Fixed fielders: biased defensive statistics, no double play, fielder's choice or sacrifice, and a bases-loaded ground out can score the winning run with no play at home (6 walk-off cases). Needs 3-F calibration before such statistics are shown. |
| F-4 | Low | The status name `NEEDS_WALKOFF_POLICY` is overloaded in the instruction (policy needed vs. policy applied). Defined in the matrix as "legal only through `walkoff-credit-v1`, verified legal under it". |
| F-5 | Low | The Infield Fly rule is not modelled by the reducer; only a template premise separates the pop templates. |
| F-6 | Low | A third-out play holds every runner (no run on such a play is ever generated). Conservative; the left-on-base count is unaffected. |
| F-7 | Low (evidence limit) | The Python oracle and the C# planner share one author and one reading of the policy; the hand tables and the ten hand-derived cases are the only parts not derived from either. |
| F-8 | Low | The instruction states the owner approval but not its date; the ADR records the writing date (2026-10-12). |

No Critical or High finding. **No defect was found in `GameReducer`** by this package (it refused every malformed plan with the expected code and accepted all 5,400 policy-conforming cases).

## 15. Remaining implementation blockers

None for starting 3-C2B-2 as specified (BaseInPlayPlan, candidates for complete plans, PendingAdvancement for the rest). Open before a complete in-play loop: U-1 composite advancement design, U-2 record recomputation after advancement, U-7 last issued event sequence (3-C3), KBO certification of any rule used (D4), 3-F calibration (fielder choice, extra-base rates).

## 16. Self-assessment

**PASS WITH NOTES** (self-assessed, then confirmed by GPT Chat `PASS WITH NOTES` for the policy/template feasibility verification only; Codex review `미실시`). Reasons for the notes: F-1 and F-2 (3-C2B-3 is required for a playable loop), the evidence limits F-7 and "no production code exists", and the KBO non-certification.

## 17. Can 3-C2B-2 production implementation start?

Technically feasible with no known repeatable-failure path for any positive-probability combination (0 refused, 0 defects, 0 unsupported among 5,400 policy cases). The owner's start gate is separate and comes after the GPT Chat review of this report (done: `PASS WITH NOTES`); this package did **not** start 3-C2B-2, 3-C2B-3 or 3-C3.

## 18. Git working tree at the end of the work

`git status --short` (HEAD `eca11bb`, branch `main`): modified `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `docs/SYSTEM_CONTRACTS.md`, `docs/decisions/ADR-007.md`, `docs/game-engine.md`; new (untracked) the files of section 1; unchanged untracked `TASK.md`, `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf`. **No file under `src/` is modified.** Nothing was staged, committed or pushed; `git add -A` was not used; no Codex review was started.

## 19. Staging re-verification (2026-10-12, owner instruction `TASK.md`, same session as staging)

Re-run in this session: the 29 new tests PASS; full Release xUnit 1,205 PASS (263 + 117 + 412 + 413), 0 failed; `tools/verify.ps1` `GATE: PASS` (log kept as `PHASE_3C2B2P_VERIFY_LOG_run3.md`; markdown link check 98 files, 0 errors; the earlier run1/run2 logs say 96/97 files because the report files did not exist yet). `PHASE_3C2B2_FEASIBILITY_RAW.json` regenerated into a temporary directory is byte-identical to the stored file; the Python oracle JSON and the template matrix regenerate byte-identically. **Whitespace-only edits made so that `git diff --cached --check` passes (no measured value changed):** the three `PHASE_3C2B2P_VERIFY_LOG_run*.md` files had CRLF line endings and two stray CR characters inside the G3/G4 detail cells (script output); they were converted to LF and the stray CRs removed. The generated matrix ended with one extra blank line; `render_inplay_feasibility_matrix.py` now writes a single final newline and the matrix was regenerated (content otherwise identical). The raw `*_raw.txt` files were not changed. Nothing was committed or pushed.
