# Phase 3-C2B-1 Report — Prepared pitch and non-in-play event candidate

Date: 2026-10-11 · Owner instruction: `TASK.md` · Base HEAD `00144323a2a9cce4f7a44a611da24ccb47dc4016` · Status: **3-C2B-1 최종 승인 (2026-10-11, 3-C2B-1 비인플레이 후보 생성에만 한정; section 16)** — the original `구현 완료 / 검토 대기` wording below is the pre-approval record

Scope is 3-C2B-1 only. (Pre-approval record: GPT Chat review `미실시`, Codex review `미실시`; superseded by section 16.) 3-C2B-2/3 (in-play events, `advancement`, runner movement), 3-C3 (joint atomicity) and Phase 4 are **not started**; the Phase 3 Gate and 3-C2B as a whole remain INCOMPLETE. GPT Chat review `미실시`, Codex review `미실시` (not started automatically). Nothing was committed or pushed.

## 1. Changed files (actual)

| File | Change |
|---|---|
| `src/StarPlayer.Simulation/Baseball/PitchPreparation.cs` | **new** — `PreparedPitch`, `NonInPlayPitchCandidate`, `PitchPreparation.Prepare` / `BuildNonInPlayCandidate` |
| `tests/StarPlayer.Infrastructure.Tests/PitchPreparationTests.cs` | **new** — 63 test cases |
| `tests/StarPlayer.Application.Tests/HiddenTendencyBoundaryTests.cs` | the two new types added to the resolver-internal signature guard (+3 lines) |
| `docs/reports/PHASE_3C2B1_REPORT.md`, `..._RED_raw.txt`, `..._GREEN_full_xunit_raw.txt`, `..._MUTATION_raw.txt`, `..._VERIFY_LOG_run1/2.md` | new evidence |
| `docs/decisions/ADR-007.md`, `docs/game-engine.md` (§22), `docs/SYSTEM_CONTRACTS.md` (C08), `CURRENT_STATE.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/00_Dashboard.md` | documentation (3-C2A status wording aligned with the recorded approval; the earlier review history is kept) |

Not touched: `PitchBranchResolver`, `PitchBranchDraft`, `GameReducer*`, `GameEvents.cs`, `EventFingerprint`, event JSON, `GameRandom*`, `GameIdentity.cs`, tuning/rule files, golden vectors, the Python oracle, UI. `TASK.md` and the two rule-book PDFs stay untracked and unchanged.

## 2. New API and type structure (Simulation, resolver-internal)

- `PitchPreparation.Prepare(GameState, GameRandomSnapshot, TuningProfile, PitchIntent, bool explicitTake, PlayerSheet batter, PlayerSheet pitcher, CancellationToken) -> Result<PreparedPitch>`
- `PitchPreparation.BuildNonInPlayCandidate(PreparedPitch, GameState, GameRandomSnapshot, TuningProfile, PlayerSheet batter, PlayerSheet pitcher, int lastIssuedEventSequence, CancellationToken) -> Result<NonInPlayPitchCandidate>`
- `PreparedPitch` (sealed class, internal constructor, get-only): state reference, `GameId`, start RNG `StateHash`, batter/pitcher `PlayerId`, declared batter side, pitcher throwing hand, the resolver's `PitchBranchDraft`, rules version/hash, probability-model version, tuning version/hash. In-memory only; never saved or restored.
- `NonInPlayPitchCandidate` (sealed class, internal constructor, get-only): validated `GameEvent`, `EventId`, `UnconfirmedStep` (the reducer's `GameStep`, a validation preview that is published nowhere), `NextRng`, `ContractVersion` `pitch-candidate-v1`, `Kind`, `NextBoundedCalls`.
- No new package, interface or manager. The two types are added to `HiddenTendencyBoundaryTests` so they cannot appear in an Application/Infrastructure signature.

## 3. How Prepare calls the real resolver

`Prepare` builds a `PitchBranchRequest` itself and calls `PitchBranchResolver.Resolve` (unchanged). There is no API that accepts a caller-made draft or attaches evidence later. A test compares `prepared.Draft` with a direct `Resolve` call (same summary), and the 28-scenario golden test proves the draft matches the independent oracle.

## 4. Current batter / pitcher verification

`Prepare` and `Build` require `PlayerSheet.Id == state.Batting.CurrentBatterSlot.Player` and `== state.Fielding.Pitcher` (ids only; no extra eligibility rule, no ratings/role requirement). Errors: `PitchPrepare.batter_sheet_missing | pitcher_sheet_missing | batter_mismatch | pitcher_mismatch`. The declared side is `Player.Bats` and the throwing hand `Player.Throws` from the sheets; the caller cannot declare them. Switch hitters keep the 3-C2A policy (right-handed pitcher → left side, left-handed → right side; tested for all six combinations). Supplying trusted sheets is the caller's responsibility (there is no player store yet).

## 5. Start RNG and `GameState` coupling

`Build` checks, each with its own code: `draft_contract_unsupported`, `game_mismatch`, the snapshot restore (existing `Rng.*` codes pass through), `rng_context_mismatch`, `rng_state_changed` (current `StateHash` ≠ start `StateHash`), `batter_changed`, `pitcher_changed`, `batter_side_changed`, `pitcher_hand_changed`, `rules_changed`, `tuning_changed`, `probability_model_changed`, and last `state_stale` (`ReferenceEquals` with the state used by `Prepare`). The identity check is last so a specific reason wins; it is a short in-memory life-cycle guard only. Count change, batter/pitcher change, approach change, an applied event, another RNG draw and a rebuilt (replayed/loaded) state object each make the old `PreparedPitch` unusable (tests). `StateHash` comparison is not Phase 4 persistence integrity and not 3-C3 joint atomicity.

## 6. Working event id and collision protection

The authoritative last sequence is passed in read-only. `GameEventIds.Create(gameId, last)` is a private working object; `Next()` gives the candidate id (exact next sequence, deterministic for equal input; `Id.sequence_invalid` / `Id.sequence_exhausted` pass through at the limits; the maximum `e999999` works, no wrap). The id must not already be in `AppliedEvents` (`PitchCandidate.event_id_in_use`, also for a same-content duplicate). The receipt count is never used. The caller's cursor is never touched (a test keeps a real `GameEventIds` at 5 and parses the candidate as 6). Finalising and moving the cursor is 3-C3.

## 7. The five non-in-play mappings

| `PitchBranchKind` | normal pitch | explicit Take |
|---|---|---|
| `HitByPitch` | `PitchEvent(HitByPitch)` | `TakeEvent(HitByPitch)` |
| `CalledBall` | `PitchEvent(Ball)` | `TakeEvent(Ball)` |
| `CalledStrike` | `PitchEvent(CalledStrike)` | `TakeEvent(CalledStrike)` |
| `SwingingStrike` | `PitchEvent(SwingingStrike)` | `PitchCandidate.take_cannot_swing` |
| `Foul` | `PitchEvent(Foul)` | `PitchCandidate.take_cannot_swing` |

`Location` = the draft's location unchanged; `Mishap`, `DroppedThirdStrike` None; no `Moves`; `StrikeoutCredits` null. Walks, hit-by-pitch advances and strikeouts are the unchanged reducer's work (tests: 3-0 ball → walk, 0-2 strike → out, HBP → batter on first, 2-strike foul keeps the count).

## 8. In play (explicit limit)

`PitchBranchKind.InPlay` → `PitchCandidate.in_play_not_supported_yet` (all 12 in-play pitches of the golden corpus). No redraw, no kind change, no event, no id issued, caller RNG/state untouched. `Prepare` itself succeeds (it only ran the resolver's private copy).

## 9. Reducer validation and `ef2`

The candidate goes through the unchanged `GameReducer.Apply` on the **original** state. Failure → the reducer's error is returned as the failure (test: `Game.defense_incomplete`). `WasDuplicate` → failure `event_duplicate`; wrong event kind/receipt version/event id → `receipt_invalid`; the pitcher's actual pitch count must rise by exactly 1 → `pitch_count_invalid`. All 43 non-in-play golden pitches produced an `ef2` receipt with the candidate id.

## 10. Immutability

Prepare/Build success, every failure and cancellation leave the `GameState` (text, lists), the caller's `GameRandomSnapshot` (canonical text) and the id cursor unchanged (tests). `OperationCanceledException` is thrown for a cancelled token, as in 3-C2A.

## 11. Tests and RED/GREEN evidence

63 new xUnit cases (`PitchPreparationTests`; theory rows count as cases): Prepare 13 (6 are the side/hand theory rows), stale 8, event id 4, golden 28 + 1 corpus check, reducer consequences 5, side effects / boundary 4.

- **RED** (stub with the final signatures, every method returns `stub.not_implemented`): 60 failed / 3 passed / 63; the 3 passing cases test the pre-existing structure (corpus coverage, no public constructor, unchanged identities). Raw log: [PHASE_3C2B1_RED_raw.txt](PHASE_3C2B1_RED_raw.txt).
- **GREEN**: 63 / 63 passed on the first implementation run.
- **Mutation checks**: 18 effective injected defects (state-identity, RNG-hash, id-collision, in-play limit, tuning/rules/batter checks, reducer-error, take-swing, context, draft contract, foul mapping, dropped location, take→pitch event, wrong next RNG, wrong id, caller-chosen side). **17 detected, 1 survived**: M16 removes the post-reducer `WasDuplicate` guard; it is unreachable because the earlier `event_id_in_use` check already refuses any existing id (defence in depth, see NOTES). A first batch whose `if (false)` mutants failed to compile (CS0162, warnings as errors) is **not** counted as detection; those were redone with a runtime-false condition. Raw: [PHASE_3C2B1_MUTATION_raw.txt](PHASE_3C2B1_MUTATION_raw.txt).
- Expected branch / location / RNG values are from the independent oracle (`phase3c2a_pitch_branch_golden.json`: kind, zone, side, boundary, call counts, final canonical RNG text and state hash per scenario); event ids and error codes are literals. No expected value was produced by the code under test.

## 12. Full suite and `verify.ps1`

- `dotnet test StarPlayer.sln -c Release`: Domain 263, Simulation 383, Application 117, Infrastructure 413 = **1,176 passed**, 0 failed (baseline 1,113; +63). Raw: [PHASE_3C2B1_GREEN_full_xunit_raw.txt](PHASE_3C2B1_GREEN_full_xunit_raw.txt).
- `tools/verify.ps1` twice in a row: run 1 `GATE: PASS` ([log](PHASE_3C2B1_VERIFY_LOG_run1.md)), run 2 `GATE: PASS` ([log](PHASE_3C2B1_VERIFY_LOG_run2.md)); see section 15 for the exact sequence.
- `git diff --check`: exit 0 (only the repository's LF/CRLF notices). 3-A / 3-C2A golden vectors, the Python oracle, `ef1`/`ef2`, event JSON and 3-C2A tests are unchanged (the existing 3-C2A tests still pass).

## 13. Notes (open)

1. The post-reducer `WasDuplicate` guard is defence in depth and unreachable behind the id pre-check (mutation M16 survives); same-content duplicates are still refused, as `event_id_in_use`.
2. `PreparedPitch.State` identity cannot survive a save/load by design; the rebuild-then-prepare-again flow is the contract. It does not replace Phase 4 integrity checks or 3-C3 atomicity.
3. Trusted `PlayerSheet` supply is a caller responsibility until a player store exists; only ids and handedness are checked.
4. The candidate's `UnconfirmedStep` carries the reducer preview; a later committer must still re-apply or deliberately adopt it (3-C3), and the id must not be replaced.
5. In-play, `advancement`, runner movement, fielding credits and automatic plate-appearance progression are unimplemented (3-C2B-2/3, 3-C3); statistical calibration is 3-F.
6. The 63 tests live in `StarPlayer.Infrastructure.Tests` (where the scripted-game fixtures are), like the 3-C2A tests; the reflection helper `Forge` is test-only and used for four negative cases.

## 14. Self-assessment

**PASS WITH NOTES** for 3-C2B-1: every requested behaviour is implemented and tested, existing contracts are unchanged, all executed checks pass. Not an approval, not a Phase 3 Gate result.

## 15. Final verification sequence

1. New tests: 63 / 63 passed (after RED 60 failed / 3 passed against the stub).
2. `dotnet test StarPlayer.sln -c Release`: 1,176 passed, 0 failed.
3. `tools/verify.ps1` run 1 (before the documentation edits): `GATE: PASS` ([log](PHASE_3C2B1_VERIFY_LOG_run1.md)).
4. Documentation written (report, ADR-007, game-engine section 22, SYSTEM_CONTRACTS C08, CURRENT_STATE, Phase 03 review, dashboard).
5. `tools/verify.ps1` run 2 (after the documentation edits; relative links 92 files OK, Infrastructure 413 passed): `GATE: PASS` ([log](PHASE_3C2B1_VERIFY_LOG_run2.md)).
6. `git diff --check`: exit 0. `git status`: 7 modified tracked files (6 documents + `HiddenTendencyBoundaryTests.cs`), new untracked: `PitchPreparation.cs`, `PitchPreparationTests.cs`, this report and its five evidence files; `TASK.md` and the two rule-book PDFs were already untracked. No `src` file other than `PitchPreparation.cs` changed. Nothing committed or pushed.

## 16. Final approval (2026-10-11, owner `TASK.md`)

**3-C2B-1 is approved for the non-in-play candidate work package only: GPT Chat `PASS WITH NOTES` / final approval; independent Codex review `PASS WITH NOTES`** (no new Critical / High / Medium defect). The self-assessment (section 14) and every earlier statement are kept as the pre-review record. This is not an approval of 3-C2B-2/3, of 3-C2B as a whole, or of Phase 3: those stay INCOMPLETE and are not started.

Evidence cited from the owner record (not re-run for this documentation step): full xUnit 1,176 PASS (Claude run, section 12); Codex direct run Release 63/63 PASS; `tools/verify.ps1` twice `GATE: PASS` (Claude run; Codex checked the logs); 3-C2A golden vectors kept; the existing RNG, `ef1`/`ef2` and `GameReducer` contracts kept.

**Notes that stay open (none is closed by the approval):**
1. Verifying that a `PlayerSheet` comes from an authoritative source is the responsibility of the call boundary.
2. The last issued event sequence needs a final re-check in 3-C3.
3. Joint atomicity of `GameState`, RNG, ids and receipts is 3-C3.
4. Mutation M16 survived: the post-reducer `WasDuplicate` guard is unreachable behind the earlier id-collision check (defence in depth).
5. The Codex independent fresh build of the changed `HiddenTendencyBoundaryTests` was `미실시` because of a NuGet access problem.
6. In-play candidates, runner movement, fielding templates and `advancement` are not implemented.
7. The official-record policy for a walk-off extra-base hit is undecided.
8. Phase 3-F statistical calibration is `미실시`.

**Approval-record verification (this documentation step, code unchanged):** `tools/verify.ps1` was run once more after the approval edits: `GATE: PASS`, relative Markdown links 93 files OK, and its test steps passed again (Domain 263, Simulation 383, Application 117, Infrastructure 413 = 1,176) ([log](PHASE_3C2B1_APPROVAL_VERIFY_LOG.md)). `dotnet test StarPlayer.sln` was not run separately in this step. Git staging prepared; no commit or push (awaiting the user's explicit approval); planned message `feat: add phase 3c2b1 safe non-inplay pitch candidates`.

Evidence-file note: to make `git diff --cached --check` pass, the three generated `PHASE_3C2B1_*VERIFY_LOG*.md` files were converted from CR/CRLF to LF and the empty mutation results in `PHASE_3C2B1_MUTATION_raw.txt` were annotated; no result value changed.
