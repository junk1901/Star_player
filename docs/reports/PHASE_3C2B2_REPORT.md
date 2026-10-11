# Phase 3-C2B-2 Report — Base in-play plan, official walk-off record, pending advancement and in-play event candidate

Date: 2026-10-12 · Owner instruction: `TASK.md` · Base HEAD `34c2028cbcbf002f8d18cf0408cd10d3ce044b06` · Status: **3-C2B-2 최종 승인 완료 (2026-10-12; Codex Medium focused re-review PASS / GPT Chat final PASS; section 18)**. Earlier status (pre-approval, kept): 3-C2B-2 구현 완료 / 검토 대기 (self-assessed PASS WITH NOTES; GPT Chat review `미실시`, Codex review `미실시`; nothing staged, committed or pushed)

Scope is 3-C2B-2 only. 3-C2B-3 (the `advancement` draw on `game/runner@1`, composite contexts), 3-C3 (joint atomicity, automatic plate-appearance progression, last-issued-sequence re-check) and Phase 4 are **not started**; the Phase 3 Gate is **INCOMPLETE**. Nothing below is an approval.

## 1. Changed files (actual)

| File | Change |
|---|---|
| `src/StarPlayer.Simulation/Baseball/InPlayTemplateCatalog.cs` | **new** — `InPlayPolicy` (version constants), the 19 `inplay-template-v1` templates, the 45 approved combinations, `Resolve` |
| `src/StarPlayer.Simulation/Baseball/InPlayBasePlanner.cs` | **new** — internal pure planner (`Plan`, `BuildEvent`): runner table, batter destination, advancement contexts |
| `src/StarPlayer.Simulation/Baseball/InPlayOfficialCredit.cs` | **new** — `walkoff-credit-v1` (internal) |
| `src/StarPlayer.Simulation/Baseball/InPlayCandidateTypes.cs` | **new** — `BaseInPlayPlan`, `InPlayPlanOutcome` → `InPlayPitchCandidate` / `PendingAdvancement`, `InPlayAudit` and its parts |
| `src/StarPlayer.Simulation/Baseball/InPlayPitchPlanning.cs` | **new** — public entry points `PlanBase` / `Build`, post-reducer `Verify` (internal) |
| `src/StarPlayer.Simulation/Baseball/PitchPreparation.cs` | the stale-preparation checks of `BuildNonInPlayCandidate` moved **unchanged and in the same order** into `internal CheckPrepared`; `PitchesOf` made internal. Public signatures, error codes and priority unchanged |
| `src/StarPlayer.Simulation/StarPlayer.Simulation.csproj` | `InternalsVisibleTo` for `StarPlayer.Simulation.Tests` and `StarPlayer.Infrastructure.Tests` |
| `tests/StarPlayer.Simulation.Tests/InPlay/` (5 files) | **new** — 115 tests: catalogue, base planner (hand-derived tables), 5,400-case comparison, verification, type isolation |
| `tests/StarPlayer.Infrastructure.Tests/InPlayPitchPlanningTests.cs` | **new** — 57 tests on real `PitchPreparation.Prepare` results |
| `tests/StarPlayer.Application.Tests/HiddenTendencyBoundaryTests.cs` | five new types added to the resolver-internal signature guard |
| `docs/reports/PHASE_3C2B2_REPORT.md` (this), `…_RED_raw.txt`, `…_MUTATION_raw.txt`, `…_FULL_XUNIT_raw.txt`, `…_VERIFY_LOG_run1/2.md` | new evidence |
| `docs/decisions/ADR-007.md`, `docs/game-engine.md` (§24), `docs/SYSTEM_CONTRACTS.md` (C08), `CURRENT_STATE.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/00_Dashboard.md` | documentation |

Not touched: `GameReducer*`, `GameEvents.cs`, `EventFingerprint`, event JSON, `PitchBranchResolver`, `PitchBranchDraft`, RNG, probability model, `content/tuning/virtual_tuning_v1.json`, rule profiles, golden vectors and the Python oracle, the 3-C2B-2-P test-only planner and its tests, UI, DB. `TASK.md` and the two rule-book PDFs stay untracked and unchanged. No `git add`, commit or push; no Codex review.

## 2. New internal API and type structure (Simulation, resolver-internal)

- `InPlayPitchPlanning.PlanBase(PreparedPitch, GameState, GameRandomSnapshot, TuningProfile, PlayerSheet batter, PlayerSheet pitcher, ct) -> Result<BaseInPlayPlan>` — shared stale check, then the deterministic base plan. No RNG, no id, no reducer call.
- `InPlayPitchPlanning.Build(…same…, int lastIssuedEventSequence, ct) -> Result<InPlayPlanOutcome>` — `InPlayPitchCandidate` or `PendingAdvancement`. An internal overload takes a stage hook (tests cancel at a chosen stage).
- `BaseInPlayPlan` (internal ctor, get-only): the real `PreparedPitch`, original state, batted ball / class / intensity, original `defense_result`, drawn and official result and template ids, batter final base, every runner's move (key, player, from, to, cause; lead runner first), credits, errors, runs, half-inning end, walk-off context / runs needed / effect / winning runner, `AdvancementRequired`, all applicable context keys, composite marker, record-change reason, policy versions, `InPlayAudit`.
- `InPlayPlanOutcome` (abstract, closed: internal ctor) with **sibling** sealed classes. `InPlayPitchCandidate`: `Event` (`InPlayEvent`), `EventId`, `UnconfirmedStep`, `NextRng`, `Audit`. `PendingAdvancement`: `ContextKeys`, `IsComposite`, `RngAfterPitchDraw`, and the plan — **no** event, id, receipt, step or adoptable RNG member.
- Internal: `InPlayBasePlanner.Plan(state, battedBall, defenseResult)` (pure: this is the entry the 5,400-case tests use; it can never produce a candidate), `BuildEvent`, `InPlayOfficialCredit`, `InPlayTemplateCatalog`, `Verify`.
- New error codes (stable, declared): `InPlayPlan.not_in_play | batted_ball_unknown | defense_result_unknown | template_unsupported | combination_not_approved | state_missing | state_invalid`; `InPlayCandidate.event_id_in_use | event_duplicate | receipt_invalid | pitch_count_invalid | official_record_invalid`. Everything else passes through: `PitchCandidate.*` / `PitchPrepare.*` / `Rng.*` (the shared check), `Id.*`, and the reducer's own `Game.*` error.
- Evidence versions: `inplay-mapping-v1`, `walkoff-credit-v1` (`not_applicable` outside a walk-off context), `inplay-template-v1`, candidate contract `inplay-candidate-v1`. No new package, interface, manager or repository.

## 3. How C2B-1 is reused

`BuildNonInPlayCandidate`'s checks (inputs exist, draft contract, game, restorable snapshot whose context equals the game id, start `StateHash`, sheets and ids, handedness, rules / tuning / model identity, and last the very same `GameState` object) became `PitchPreparation.CheckPrepared`, with the same codes and the same order; the non-in-play builder calls it, so do `PlanBase` and `Build`. The 63 C2B-1 tests pass unchanged (and a mutation removing the RNG-position check is caught). `BuildNonInPlayCandidate` still answers an in-play draft with `PitchCandidate.in_play_not_supported_yet`.

## 4. The 19 templates

A hand-written literal table in `InPlayCatalogTests` (not derived from the catalogue): ground out SS assist + 1B putout; line out SS; fly out CF; pop out 2B; reached on error E6 (ground, line), E8 (fly, pop); single / double (ground), single / double / triple / home run (line, fly), single (pop) with no putout, assist or error. Rules checked for every template: exactly one putout per out; an assist only on the ground out; a hit or an error has no credit; no fielder's choice, double play, sacrifice or compound error. `popup/single` and `popup/reached_on_error` resolve and carry the approved fact `pop_beyond_infielder_ordinary_effort`; the error stays the outfielder's (never moved to an infielder to avoid the Infield Fly rule). The 45 positive combinations are an explicit table in the catalogue and a test reads the tuning JSON, so a tuning change that makes a zero combination positive fails a test; the 15 zero combinations are refused (`template_unsupported` where the class has no template for the result, `combination_not_approved` where it has one).

## 5. 5,400 cases against the recorded oracle

`InPlayOracleComparisonTests` runs the **production** planner over the same real states (built only through `GameReducer.Apply`; the test-only 3-C2B-2-P planner is not called) and compares every field with the independent Python oracle, the recorded raw feasibility JSON and the real reducer.

| Result | Ordinary (1,080) | Walk-off (4,320) |
|---|---:|---:|
| executed / reducer accepted / mismatches | 1,080 / 1,080 / 0 | 4,320 / 4,320 / 0 |
| `LEGAL_BASE` | 780 | 3,114 |
| `PENDING_ADVANCEMENT` | 300 (210 composite) | 1,014 (654 composite) |
| legal only through `walkoff-credit-v1` | 0 | 192 |
| unsupported / defect | 0 / 0 | 0 / 0 |

45 combinations, 19 distinct drawn templates. The counts were not targeted: each case is compared per field (drawn/official result and template, runner moves, walk-off effect, context presence, pending flag, status, runs, credits, error, and after the reducer: outs, half over, game over, runners left, LOB, RBI, hits, at-bats, one pitch, `ef2`). The recorded raw rows (`PHASE_3C2B2_FEASIBILITY_RAW.json`, `general` and `walkoff`) match field by field, including the winning runner's base. Mirror contexts (bottom of the 3rd, no-DH game, 3-2 count, tied bottom of the 10th) give identical plans. The "needs walk-off policy" label stays "legal only through the policy, verified legal under it".

## 6. PendingAdvancement classification and isolation

Declared contexts (kept as the keys of `prob-model-v1`): `single_runner_on_first`, `single_runner_on_second`, `double_runner_on_first`, `fly_out_runner_on_third` (fly-ball out, fewer than two outs). A plan is `AdvancementRequired` only if a key applies and it neither ends the game nor is a third-out play; a game-ending walk-off keeps the keys but is not pending. A composite situation (more than one runner) keeps **every** applicable key and the composite marker. `PendingAdvancement` is a sibling of `InPlayPitchCandidate`: tests prove it is not assignable to or from it, has no `GameEvent`/`GameStep`/`EventReceipt` member and no `Event`, `EventId`, `UnconfirmedStep`, `NextRng` or `Receipt` property, that `Build` stops before any id or reducer work for it (stage hook), that it ignores the cursor (no id is issued: a cursor of 0, `MaxSequence` and −1 all give the same pending), and that it holds the position **after** the 3-C2A draw (`RngAfterPitchDraw` = `Draft.NextRng`, never the pitch start). Nothing is drawn or advanced. Whether a pending advancement should reject a nonsensical cursor is left as a note (section 12).

## 7. Walk-off long hits (192)

Exactly double→single 96, triple→double 48, triple→single 48; each ends the game, scores exactly the needed runs, records the reason `walkoff_credit_reduced`, a winning runner (the need-th scorer, lead runner first) and the batter's final base equal to the credited base. Hand-derived examples all pass: tied + runner on 3B + drawn double → single; tied + loaded + double → single (3>4,2>3,1>2); tied + runner on 2B + triple → double; tied + runner on 1B + triple → triple; two behind + loaded + triple → triple, 3 runs; singles, outs and reached-on-error are never reduced; an out-of-the-park home run keeps everyone scoring and is never converted. The original draw is never touched.

## 8. Real PreparedPitch integration

57 tests use only the real `Prepare` (seeded scans; no injected draft, only the reflection `Forge` of the 3-C2B-1 tests to reach refusals). Real in-play pitches become `InPlayEvent`s whose `Location` is the very draft object (`Assert.Same`), `ef2` receipt equal to the event's fingerprint, one receipt added, one pitch counted for the original fielding team's pitcher, ids exact (`e000042` from cursor 41) and repeatable, plus all 28 golden scenarios: in-play pitches become the expected candidate and the whole chain ends on the Python oracle's final RNG text and hash.

## 9. Zero additional RNG draws (evidence)

The three new source files that build plans contain no `NextBounded`, stream, `GameRandom` call, clock or I/O (source-scan test). `candidate.NextRng` equals `Draft.NextRng` (canonical text and hash); the in-play draft has 6 calls and no `game/runner@1` use; a second build gives the same position; the golden chains end exactly on the oracle's positions after in-play pitches (so any extra draw would have shown).

## 10. EventId cursor, GameState and receipts unchanged

The working id comes from a private `GameEventIds` seeded with the read-only sequence; a `GameEventIds` object kept by the test stays at 5 after building id 6; the state digest, snapshot canonical text, `AppliedEvents` count and start `StateHash` are equal before and after every success, refusal and cancellation tested; the original state holds no receipt of the candidate (only the unconfirmed step does). Collision with an existing receipt, `Id.sequence_exhausted` and `Id.sequence_invalid` are refused without wrapping.

## 11. Cancellation and failure

Stage hook names in order: `start`, `validated`, `planning`, `planned`, `before_reducer`, `after_reducer`, `verified`. A test cancels at each, requires the exception, that the stage is the last one seen, and that state, RNG, cursor and receipts are unchanged and the same input then succeeds with the same id. A token cancelled before the call throws at once. Failure cases checked for no side effect: not in play, unknown / unapproved draw, stale preparation (state replaced, count changed, RNG moved or from another game, other game id, rules, tuning, sheets), id collision, reducer rejection (`Game.defense_incomplete` passed through, no candidate), `Verify` branches driven with deliberately wrong reducer verdicts (rejection, duplicate, non-`ef2` or foreign receipt, no pitch counted, wrong official result, walk-off without a final home win, a lost runner move).

## 12. Tests actually run

| Run | Result |
|---|---|
| RED against the stub API (`PHASE_3C2B2_RED_raw.txt`) | Simulation InPlay 106 failed / 8 passed (114); Infrastructure 55 failed / 2 passed (57). The 10 that passed against stubs are shape or constant checks (version names, type structure, and templates assertions that are vacuous on an empty catalogue); they became meaningful with the catalogue |
| GREEN | new tests 172 pass (Simulation 115 incl. one test added after mutation M32, Infrastructure 57) |
| `dotnet test StarPlayer.sln -c Release` | **1,377 passed, 0 failed** (Domain 263, Simulation 527, Application 117, Infrastructure 470; baseline 1,205, +172) — `PHASE_3C2B2_FULL_XUNIT_raw.txt` |
| Mutation checks (`PHASE_3C2B2_MUTATION_raw.txt`) | 32 defects injected one at a time (runner moves, forced advance, credit rule, winner, templates, approved set, contexts, composite, pending, location, next RNG, id, collision, duplicate, receipt, pitch count, official record, shared check): **32 of 32 detected**. First run: 23 detected, 8 not buildable (unreachable-code compile errors, re-expressed and re-run), 1 survived (M32, missing-move verification) → test added → detected |
| `tools/verify.ps1` (`-LogName`, so the Phase 2 log is not overwritten) | two consecutive runs on the final tree, both `GATE: PASS`, exit 0 ([run 1](PHASE_3C2B2_VERIFY_LOG_run1.md), [run 2](PHASE_3C2B2_VERIFY_LOG_run2.md); run by Claude; Godot headless and export checks and the markdown link check (99 files) also passed; only result sentences of the status documents were edited afterwards) |
| Markdown links / `git diff --check` | see section 15 |

Not run: Codex and GPT Chat reviews; a production calibration of any frequency (3-F); persistence (none built).

## 13. Golden vectors, `ef1`/`ef2`, JSON

Phase 3-A and 3-C2A golden vectors, the C2B-1 63 tests, `Ef1CorpusPinTests`, event JSON v1/v2 tests, reducer and score tests, `HiddenTendencyBoundaryTests` (extended with the five new types) all pass in the full run; no `ef1`/`ef2` text, hash, JSON member or RNG constant changed. The audit is not in the canonical event (a hand-built event with the same facts has the same `ef2` receipt; the canonical text contains neither `inplay` nor `ipt1`).

## 14. Problems and notes

No Critical, High or Medium defect found.

| ID | Sev. | Note |
|---|---|---|
| N-1 | Low | `PendingAdvancement` ignores the event-sequence cursor (no id is issued), so a garbage cursor is first noticed by the candidate path. |
| N-2 | Low | (Superseded: fixed in section 17.) `WinningRunner` was null when the batter scored the winning run, which could not be told from "no winning scorer". |
| N-3 | Low | The approved 45 combinations are duplicated in the catalogue; a test reads the tuning JSON to keep them in step. |
| N-4 | Low | `InternalsVisibleTo` gives two test assemblies access to the internal planner core and `Verify`; the public entry points still accept only a real `PreparedPitch`. |
| N-5 | Low | `Verify` compares counts of credits, errors and moves with the plan and trusts the reducer for the official numbers by design (no duplicated scoring). |
| N-6 | Low (process) | The mutation script restored files with their old timestamps, which left a stale mutated assembly for one build; found when a new test failed unexpectedly; fixed (`utime` after restore) and everything was rebuilt with `--no-incremental` before the final runs. |
| N-7 | Low | Inherited and unchanged: oracle and C# share one author (F-7), fixed fielders bias defence statistics and give no double play / fielder's choice / sacrifice (F-3), the Infield Fly rule is a template premise only (F-5), a third-out play holds every runner (F-6), 2026 KBO clauses not certified (OBR as temporary basis), 3-F calibration `미실시`. |

Still open for 3-C2B-3: **F-1** (no context, order or interaction for composite runner situations: 210 of 300 ordinary and 654 of 1,014 walk-off pending cases) and **F-2** (27.8 % of nominal ordinary in-play situations are pending, so 3-C2B-2 alone gives no complete in-play loop). U-2 (recompute the official record and third-out order after an advancement result) and U-7 / 3-C3 items (last issued sequence re-check, joint atomicity, plate-appearance progression, audit persistence) are unchanged.

## 15. Documents and Git

Updated: ADR-007 (new "Decisions (3-C2B-2 implementation)"), `docs/game-engine.md` §24, SYSTEM_CONTRACTS C08, `CURRENT_STATE.md`, `Phase_03_Review.md`, `Star/00_Dashboard.md`. The status everywhere is "3-C2B-2 구현 완료 / 검토 대기"; no GPT Chat / Codex approval is claimed; Phase 3 Gate INCOMPLETE. `git status`: modified `PitchPreparation.cs`, `StarPlayer.Simulation.csproj`, `HiddenTendencyBoundaryTests.cs` and the documents above; new the files of section 1; unchanged untracked `TASK.md` and the two PDFs. Nothing staged, committed or pushed; `git add -A` not used.

## 16. Self-assessment

**PASS WITH NOTES.** Everything required by `TASK.md` section 2–12 was built and tested with evidence; the notes are N-1…N-7, the unresolved C2B-3 items F-1 / F-2 and the missing independent reviews. This is not an approval.

## 17. Codex review fix round (2026-10-12, owner `TASK.md`; Codex Medium review `CHANGES REQUIRED`: Medium 1, Low 1)

The review status stays "3-C2B-2 구현 완료 / 재검토 대기"; no GPT Chat or Codex approval is claimed; the Phase 3 Gate is INCOMPLETE. Only the two findings were changed; the 19 templates, D1-D5, the 5,400 oracle cases and the C2B-1 contract are untouched. Nothing was staged, committed or pushed.

**Medium - winning scorer of a walk-off.** Defect: with a tied game in the bottom of the 9th, nobody on and a home run, the planner searched only the existing runners, so `WinningRunner` was null although the reducer correctly scores the batter-runner; "no winning scorer" and "the batter-runner won" were both null. Fix: `WinningRunner` is now an `InPlayWinningRunner(Kind, RunnerKey, Player, StartingBase)` (new small type and `InPlayScorerKind { ExistingRunner, BatterRunner }` in `InPlayCandidateTypes.cs`; used by `InPlayPlanFacts`, `BaseInPlayPlan.WinningRunner` and `InPlayAudit.WinningRunner`). `InPlayBasePlanner.Plan` builds the scorer list in the reducer's order (existing runners lead-first, then for an official home run the batter-runner) and takes the need-th entry when the play ends the game. The batter-runner uses `state.NextRunnerKey` and `state.Batting.CurrentBatterSlot.Player`, exactly what `GameReducer.ApplyInPlay` constructs (`new BaseRunner(slot.Player, pitcher, s.NextRunnerKey, slot.Order)`); no id is invented and no existing key is reused. Null now means only "the play does not end the game". No RNG, event id cursor, reducer, `ef1`/`ef2` or persistence change; the base plan, runs and credits are unchanged.

New tests (`InPlayWinningRunnerTests.cs`, 10), each judged against the real `GameReducer.Apply` `ScoredPlay.Runs`: tied/nobody on/home run, 1 behind + runner on first + home run, 2 behind + first and second + home run, 3 behind + bases loaded + grand slam (all `BatterRunner`, key = `NextRunnerKey`, player = the batter, last in `Runs`); tied + runner on third + home run and tied + runner on third + drawn double (`ExistingRunner`, key preserved); 1 behind + nobody on + home run (null, game tied and continues); ordinary-inning home run (null); null versus `BatterRunner` are different data; reducer order (existing runners lead-first, batter last). Existing `WinningRunner == null` expectations for the batter were corrected (`A_walk_off_home_run_with_nobody_on_is_won_by_the_batter`, the oracle-comparison winner test); `.From` became `.StartingBase`; no test was deleted or disabled.

Evidence: RED (fix line disabled, tests otherwise as final) `PHASE_3C2B2_FIX1_RED_raw.txt` - 8 failed / 146 passed of the 154 filtered `InPlay` Simulation tests; GREEN `PHASE_3C2B2_FIX1_GREEN_raw.txt` - 154/154. The earlier RED/GREEN evidence of section 12 is kept unchanged. The 5,400-case production-versus-oracle comparison, the 63 C2B-1 tests and the real-`PreparedPitch` integration tests pass unchanged; the frozen oracle records a winning base only on its double/triple credit-reduction path, so that comparison was not widened.

**Low - RNG documentation.** `docs/game-engine.md` section 23.8 (the `start_rng_state_hash`/`final_rng_state_hash` row), the `PendingAdvancement` description (section 23) and a new "RNG positions (H0 / H1 / H2)" paragraph in section 24, plus ADR-007 (3-C2B-2 item 3), now say: H0 = position given to `Prepare`; H1 = `Draft.NextRng` after the 3-C2A draw; H2 = after a future 3-C2B-3 advancement draw. C2B-2 starts at H1 and consumes nothing (H1 -> H1); 3-C2B-3 continues from H1; H0 and H1 differ in general; the two audit fields describe the whole pitch (H0 -> H1) whereas the C2B-2 sub-step alone is H1 -> H1; only 3-C3 fixes the final position with the authoritative state. The code and every stored meaning were unchanged: `StartRngStateHash` was already H0 and `FinalRngStateHash` H1.

Tests actually run after the fix: `dotnet test StarPlayer.sln -c Release` **1,387 passed, 0 failed** (Domain 263, Simulation 537, Application 117, Infrastructure 470; previous 1,377, +10) - `PHASE_3C2B2_FIX1_FULL_XUNIT_raw.txt`; `tools/verify.ps1` twice consecutively, both `GATE: PASS` - `PHASE_3C2B2_FIX1_VERIFY_LOG_run1.md`, `..._run2.md` (stdout) and `PHASE_3C2B2_FIX1_VERIFY_LOG_run2_table.md` (the script's own table; it writes `PHASE_2_VERIFY_LOG.md` by default, which was restored with `git checkout` afterwards); the script includes the Markdown link check; `git diff --check` reports no whitespace error in the tracked files. The earlier mutation run (section 12) was not repeated; the changed lines are covered by the new RED/GREEN pair.

Self-assessment after the fix: **PASS WITH NOTES**, re-review pending (`미실시`).

## 18. Final approval record (2026-10-12)

**3-C2B-2: 최종 승인 완료 (scope: the 3-C2B-2 work package only).** Codex Medium focused re-review `PASS`; GPT Chat final `PASS`. The earlier Codex `CHANGES REQUIRED` (Medium 1, Low 1) is not deleted: it is the fix history of section 17. Phase 3 overall Gate: **INCOMPLETE**; 3-C2B-3, 3-C3 and Phase 4 are not started. Commit was approved; the actual Commit/Push result is whatever the Git history shows; Push is not approved.

Sources, kept apart: the Codex `PASS` is an independent review report the owner relayed in the ChatGPT conversation (Codex ran 87 Simulation + 57 Infrastructure = 144 related tests, all PASS); the GPT Chat `PASS` is GPT Chat's own final judgement (final approval of this work package and of the Git Commit, confirmed by the owner in chat on 2026-10-12); the full xUnit run and `tools/verify.ps1` are Claude Code results. Neither GPT Chat nor Codex is recorded as having run the full local suite.

Basis: 19 in-play templates; 45 positive-probability combinations; 1,080 ordinary and 4,320 walk-off cases; 5,400-case oracle comparison with 0 mismatches; 1,314 `PendingAdvancement` cases kept separate; 192 walk-off credit reductions; D5 winning-scorer audit contract completed (`InPlayWinningRunner`); H0/H1/H2 RNG contract corrected; full xUnit 1,387 PASS and `tools/verify.ps1` twice `GATE: PASS` (Claude Code).

Still open: F-1 (C2B-3 composite-runner advancement contract); F-2 (follow-up decision for `PendingAdvancement`); U-2 (official-record recomputation after an extra advance); C3 atomic commit and the EventId re-check; KBO 2026 official-rule certification; a dedicated named test for the `WinningRunner` null of an away-team win on a third out (non-blocking NOTE).
