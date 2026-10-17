# Star Player Phase 3-F F-A/B Implementation Report

## A. Overview

- Date: 2026-10-17 · Executor: Claude Code (Sonnet 5.5) · Branch `main` · Base HEAD `430f42d0dbd61c51fbde5cb10fbf2e98d1eecc02` (Phase 3-E Game Calling v4 closeout, local, not pushed). HEAD is unchanged at the end: **no staging, no commit, no push**.
- Authority: owner instruction `TASK.md` "Star Player Phase 3-F F-A/B Integrated Implementation" (SHA-256 of that file, `99F36C71...540BDA`, unchanged at the end). Scope: **F-A** deterministic execution harness and **F-B** metrics and data collection, plus a small end-to-end smoke. Not in scope and **not done**: the 100,000-PA run, the 100-game run, any official verdict on the eight targets, tuning changes, Phase 4 save, Codex, full Mutation, `tools/verify.ps1`.
- Result in one line: a console tool plays complete, legal, repeatable games through the unchanged production engine, counts the eight targeted measures from confirmed commits, cross-checks them against `BoxScoreBuilder`, and writes a manifest and a summary; every official verdict stays `NOT_EVALUATED`.
- Verdict: **`PHASE3_F_FA_FB_IMPLEMENTED_WITH_NOTES`**; **`PHASE3_F_INFRASTRUCTURE_READY`** (with the notes in sections F and G); **`PHASE3_F_VALIDATION_PENDING`**. Phase 3-F gate and **Phase 3 gate: `INCOMPLETE`**. Review status: `검토 대기` (not approved; independent review `미실시`).

### Implementation Brief (the ten items the instruction asked for before coding)

1. **Existing loop.** `Gx.Play` (`tests/StarPlayer.Infrastructure.Tests/ExecutionFixture.cs`): restore a cursor, then until the game is final or N events: if the plate appearance is not in sequence, stage an `ApproachEvent(Normal)`; otherwise `PitchPreparation.Prepare` → for a non-in-play branch `BuildNonInPlayCandidate` + `PitchCommitGate.Stage`; for in-play `InPlayPitchPlanning.Build` (+ `AdvancementPlanning.Resolve` for a `PendingAdvancement`) + `PitchCommitGate.Stage`; publish with `GameCursor.Publish`. The rest of that helper checks invariants for tests.
2. **Reused.** All of it, unchanged: reducer, resolver, planning, `PitchCommitGate` / `CommitGate`, CAS, `BoxScoreBuilder`, `PlayerInputSet` v3, `GameExecution` validation, RNG.
3. **Test-only dependencies found.** `GameCursor`'s constructor and `AuthoritativeGame.Restore` (internal, reached through `InternalsVisibleTo` by the test assemblies), the Infrastructure JSON readers for the tuning and rules files (internal), `Gx` / `Pc` / `PitchBranchFixture` (test classes). None of them is used by the harness.
4. **Public API.** `GameStarter.Begin(GameStart, seed)` (the one production addition), `RunConfig.Create`, `GameExecutor.Play`, `SimRunner.Run`, `CohortInputs.Create` / `NeutralCohort.Create`, `MetricsRecorder`, `MetricsEvaluator.Evaluate`, `BoxScoreCrossCheck.Compare`, `RunReport.Manifest/Summary/Text/Write`, and the `run` / `replay` command line.
5. **Seed and GameId.** Seeds are explicit inputs. `GameId = game:{competition key}:{cohort id}-{index:D6}` (a pure function; `GameIds.Create`); the RNG context key is the game id (the engine's existing binding), so the same seed with another GameId is another game. No clock, OS random or counter feeds any result.
6. **End and error contract.** A game is `Completed` only when the engine's final state exists (`Final` with the start `GameExecution`); a legal tie at the extra-innings limit is a completion. Budget exhaustion, an engine error code, an exception, a missing location, a cross-check mismatch and a stall are `Failed`; a cancellation is `Cancelled`. A failure keeps GameId, Seed, the engine's code, the stage and the last 12 receipts.
7. **Event sources of F1-F8.** PA-level counts: the `PlateAppearanceCompleted` facts of confirmed commits. Pitch-level counts: `PitchEvent`, `TakeEvent`, `InPlayEvent` (an in-play event is one pitch and one swing) and their `PitchLocation`.
8. **Independent aggregation.** Path A (`MetricsRecorder`) never reads a stat entry; path B is `BoxScoreBuilder` on the final state. Eleven plate-appearance items plus the pitch total are compared, and three more checks read the in-play and pitch events a second way.
9. **Files.** Section H.
10. **Open policy.** Section G. Nothing there was decided by the implementer.

(The brief was finalised after the investigation and before the first harness code was written; it is recorded here, not as a separate document.)

### Contract conflicts and decisions that needed a stated reason

| # | Finding | Handling |
|---|---|---|
| 1 | The engine has no public way to begin a game: `GameCursor` has an internal constructor and `AuthoritativeGame` only internal factories, and the harness may not use `InternalsVisibleTo`. | One minimal production addition, `GameStarter.Begin(GameStart, ulong)` (new file). It takes a `GameStart` and a seed, builds the state with the real `GameReducer.Start`, binds the RNG context key to the game id and calls `AuthoritativeGame.StartNew`. A caller cannot supply a state or a restored history, so nothing can be forged. No existing production file changed; the Commit/CAS structure, RNG and `ef1`/`ef2` are untouched. New pins: `GameStarterBoundaryTests` (3). |
| 2 | The Infrastructure readers of the tuning and rules files are internal, `ArchitectureTests` pins the Infrastructure public surface to three names with no Domain type, and pins the `InternalsVisibleTo` lists. | Not weakened. The harness has its own ~100-line reader of the two embedded content files (`HarnessData`); every validity decision is still the Domain's (`TuningProfile.Create`, `GameRuleSnapshot.Create`). Equivalence is pinned on **both** readers: `SimHarnessInputPinTests` (Infrastructure reader) and `ReportAndReplayTests` (harness reader) assert the same literals (tuning `5011f8ac...b8fd8`, rules `2d3f7557...dce2d` and `7ad97767...f4fa7`). |
| 3 | `targets[]` carries no cohort field and its descriptions disagree: F1 says "neutral inputs", F2-F5 "league average". | Not reinterpreted. The cohort recommendation is the instruction's (F1-F5 virtual league, F6-F8 neutral), recorded in the report, and `MetricsEvaluator` keeps it as data. Reported for the owner. |
| 4 | `matchup.handedness` has no neutral key for a real batter and pitcher (`same_hand` 1,040,000 ppm, `opposite_hand` 970,000 ppm). | A handedness-free "neutral" cohort cannot exist. The neutral cohort is all right-handed, like every earlier fixture, so every pitch gets the `same_hand` factor. Recorded; the hand mix of any official cohort is a G decision. |
| 5 | The ROADMAP's Pitch Zone Lite acceptance sentence lists **12** required checks; an earlier instruction text said 11. | Reported as found (section G). No clause was removed. **Corrected in the 2026-10-17 addendum: the list has 12 clauses; the figure 11 is superseded.** |
| 6 | "No strategy dominates every relevant matchup in the declared test matrix": no such matrix is declared anywhere. | `NOT_SPECIFIED` (section G). |

## B. F-A: simulation harness

- **Project.** `tools/StarPlayer.SimHarness` (console, net8.0, `TreatWarningsAsErrors`), added to `StarPlayer.sln` in a `tools` folder. References `StarPlayer.Domain` and `StarPlayer.Simulation` only; its one `InternalsVisibleTo` is its own test assembly. Tests pin all of this (`The_harness_references_only_domain_and_simulation_and_nothing_with_a_back_door`).
- **Production call path.** `GameStarter.Begin` → per action: `GameReducer.LegalActions` → `CommitGate.Stage(ApproachEvent)` or `PitchPreparation.Prepare` → `BuildNonInPlayCandidate` / `InPlayPitchPlanning.Build` (+ `AdvancementPlanning.Resolve`) → `PitchCommitGate.Stage` → `GameCursor.Publish`. Source scans pin that the executor draws nothing, applies no event itself, builds no state with `with`, and constructs no authority type.
- **Fixed inputs (`RunConfig.Create`).** `prob-model-v4` + `tuning-virtual-proto-v4` (a non-v4 profile is refused), `player-input-set-v3` (any other content version refused), a `gamerules-v2` profile (default `test-pzl-non-automated`, `test-pzl-automated` selectable; both `fictional_test`), `advancement-policy-v2` by default (v1 selectable), one neutral park snapshot (1000/1000/1000 permille), `normal-balanced-v1` policy, event budget 3,000. The `GameExecution` is built from those objects (not typed twice) and `CheckPins` re-verifies it before every game.
- **Seed / GameId.** As in the brief. Determinism is tested: same inputs (even with another run id and a freshly built cohort) give identical event hash, RNG hash, result hash and counters; another seed or another GameId gives a different event hash.
- **Player inputs.** `CohortInputs` = both `TeamSetup`s, every `PlayerSheet`, the `PlayerInputSet` v3 and `InputsHash`, the harness' own identity of everything the run reads (the engine's input-set id covers only six ratings; a contact rating change moves `InputsHash` and not the id: tested). `NeutralCohort`: two clubs, DH lineup, every rating 50, all right-handed, starter does not bat, two-player bench.
- **AI policy.** `NormalBalancedPolicy`: `ApproachKind.Normal` for every plate appearance, `PitchIntent.Balanced`, no `Take`, no substitution. The interface sees only `GameState`. A scripted `Take`-first policy is used in a test to prove an explicit `Take` is legal, consumes one real pitch and returns to a decision point. **Substitutions:** none are needed or made (a game without them is the supported normal path; the DH rules never require one). `Harness.substitution_required` is a defensive typed failure for a state the engine itself says needs one; it is unreachable through legal events, so it has no test.
- **End of game.** `Regulation`, `ExtraInnings` (one of the ten observed games, section E) and `TieAtExtraInningsLimit` (tested with a fictional one-inning rule set, seeds 1-40 scanned deterministically; the real 11-inning cap was not reached by the three regression seeds). The `Final` record must carry the start execution.
- **Safety.** Event budget checked before every stage (exhausted ⇒ `Failed`, never a completion; a budget exactly enough completes); stall detection; cancellation (before the first event and in the middle: exactly the committed prefix remains); exceptions of any step, including the post-game checks, become `Harness.unhandled_exception` with the game kept in the report and later games still run.
- **Failure data.** `RunFailure`: code, message, stage, last event sequence, revision, RNG hash, last 12 receipts; the game's GameId and Seed are in its result and in every report.

## C. F-B: metrics collector

| ID | Metric | Numerator | Denominator | Source |
|---|---|---|---|---|
| F1 | `pa_strikeout_rate` | SO (`Strikeout`, `StrikeoutReachedBase`) | PA | `PlateAppearanceCompleted` facts |
| F2 | `pa_walk_rate` | BB | PA | same |
| F3 | `pa_hit_by_pitch_rate` | HBP | PA | same |
| F4 | `pa_home_run_rate` | HR | PA | same |
| F5 | `babip` | H - HR | AB - SO - HR + SF | same; AB = PA - BB - HBP - SF - SH |
| F6 | `pitch_zone_rate` | pitches whose **actual** `PitchLocation` is in the nine inner cells | pitches with a confirmed location | `PitchEvent` / `TakeEvent` / `InPlayEvent` |
| F7 | `pitch_swing_rate` | swings (swinging strike, foul, foul bunt, in play) | pitches - hit-by-pitch pitches | same |
| F8 | `swing_miss_rate` | swinging strikes | swings | same |

- **Targets** are read from `tuning.Targets` (the file) on every evaluation; a test edits the file and sees the report move. They are virtual calibration intervals, not KBO values.
- **Confirmed events only.** `MetricsRecorder.Observe` takes the `CommitRecord` a successful `Publish` returned. A rejected, stale or unpublished commit has none (tested with two commits staged from one base). An event sequence already seen is ignored and counted; a skipped one is reported. An in-play pitch is one pitch and one swing and never a swinging strike (tested).
- **Rounding and verdict.** `Ppm` = round-half-up in integers; the verdict compares the reported (rounded) ppm with the inclusive bounds; deviation = 0 inside, negative below the low bound, positive above the high bound. Impossible counts (negative, numerator above denominator, e.g. a BABIP numerator above its denominator) are refused with `Metrics.counts_inconsistent`. Wilson 95% is a diagnostic only and never changes a status (tested: an interval overlapping the target does not turn FAIL into PASS).
- **Statuses.** Zero denominator ⇒ `INSUFFICIENT_SAMPLE`; no approved minimum for the metric ⇒ `NOT_EVALUATED`; below an approved minimum ⇒ `INSUFFICIENT_SAMPLE`; a cohort other than the metric's recommended and approved one ⇒ `NOT_EVALUATED`; otherwise `PASS` / `FAIL`. `EvaluationPolicy.Unapproved` (the only policy the tool ever uses) approves nothing. Tests pass an explicit policy to exercise the logic; that proves logic, not a threshold.
- **Cross-check.** `BoxScoreCrossCheck`: PA, AB, H, 1B, 2B, 3B, HR, BB, HBP, SO, SF and total pitches (the reducer's `PitchPitches` entries) from the events versus `BoxScoreBuilder`; plus HR, hits and HBP read from the in-play / pitch events. A mismatch fails the game (`Harness.crosscheck_mismatch`) and names the items. **Limit:** both paths start from the reducer's classification of a plate appearance (`BatterResult`); the independence is in the counting rules (AB, H, SO, SF) and in the three event-level checks, not in how the reducer decides a strikeout or a walk.
- **Independent oracle.** `MetricsOracleTests`: two hand-written fixtures whose every expected number is a hand tally written beside it (fixture 2 is a real game driven through the public reducer by hand-written events, with no resolver and no RNG). Covered: single, double, triple, home run, walk, hit by pitch, strikeout (looking, swinging, mixed), sacrifice fly, ordinary out, swinging strike, foul, called ball / strike, explicit takes, a strikeout reaching base, a sacrifice hit, a ball on an inner-zone location, an unfinished plate appearance and an unfinished game. The expected values were not copied from the collector; the only fixes during writing were reducer-legality fixes of the scripts.
- **Memory.** A game keeps counters and hashes; its events are not retained. Failed games keep the last 12 receipts only.

## D. Manifest and reproducibility

- `manifest.json` (`simharness-manifest-v1`): run id, mode, cohort id / kind, input schema, config hash; model, tuning version / hash / embedded file SHA-256, rules profile / version / status / hash / file SHA-256, called-pitch mode, advancement policy, park, input-set content version / id, cohort inputs hash, policy id, event budget, competition key; the game list (index, GameId, Seed); results (counts, end kinds, error codes, per-game event / RNG / result hashes, `run_result_hash`); and `environment_not_hashed` (UTC times, OS, runtime, peak working set, git commit and dirty flag).
- `summary.json` (`simharness-summary-v1`) and `summary.txt`: per game score, innings, end kind, PA, pitches, hashes, error code; the eight metrics (numerator, denominator, ppm, target, deviation, sample rule, status, reason, Wilson diagnostic); the cross-check per game; failures with reproduction data.
- **Hashes.** `config_hash` covers everything that can change a result (model, tuning hash, rules hash, advancement, park, input set id **and** `InputsHash`, cohort, policy, budget, the game list); `run_result_hash` covers the config hash and every game's result hash. Nothing time- or machine-dependent is in them (a source scan and a test pin that).
- **Replay.** `replay --manifest FILE` rebuilds a neutral-cohort run from the manifest and compares `config_hash` and `run_result_hash`; a failed run replays to the same failure; an edited result hash is detected. A virtual-league manifest holds only an input hash, so it cannot be rebuilt from the manifest alone.
- **Git fact.** `git_commit` is HEAD and `git_dirty` is true while this work is uncommitted; the manifest therefore names the base commit, not a commit containing the harness.

## E. Tests

| Project | Before | After | Change |
|---|---:|---:|---:|
| Domain.Tests | 327 | 327 | 0 |
| Application.Tests | 127 | 127 | 0 |
| Simulation.Tests | 738 | 741 | +3 (`GameStarterBoundaryTests`) |
| Infrastructure.Tests | 1,145 | 1,146 | +1 (`SimHarnessInputPinTests`) |
| **SimHarness.Tests (new)** | - | **86** | +86 |
| **Total** | 2,337 | **2,427** | +90 |

- Full `dotnet test StarPlayer.sln -c Release`: **0 FAIL, 0 SKIP, exit code 0** (final run after the last change). No existing test was deleted, skipped or weakened; no existing file in `src/`, `tests/Fixtures/` or `content/` was modified.
- New tests by area: execution / determinism / end / budget / cancellation / pins / rating gaps / policy legality / authority (`ExecutionTests`, 23), oracle (11), evaluation and rounding (28), manifest / summary / replay / CLI (14), end-to-end and structure (10) = 86.
- Required test list of the instruction: 1-12 of section 10 are covered (same result, event hash, RNG hash, normal end, legal tie, budget failure, wrong model / tuning, another input set, missing catcher / pitcher rating, failure keeps seed and ids, no external state tampering [a policy cannot change the game; sources build no state], normal Commit / CAS path [stale commit not counted; only `Publish` records are observed]).
- **Spot sabotage (manual, not the Mutation run):** four deliberate formula breaks (F7 denominator with HBP; BABIP without SF; zone from the outer region; foul not a swing) each failed 3-5 tests; the originals were restored byte for byte and the suites passed again. Full Mutation: `미실시`.
- **Observations, not benchmarks.** Seeds 3 / 11 / 29: 3 games, 226 PA, 673 pitches, 899 events, all completed, all cross-checks matched, ~0.9 s including start-up. Seeds 1-10: 10 games, 751 PA, 2,233 pitches, 2,984 events, 9 regulation and 1 extra-innings game, 1.58 s in total (first game 282 ms with JIT, then 112-165 ms), peak working set 60.7 MB, one thread. At about 500 plate appearances per second this machine would need a few minutes for 100,000 PA; this is an order-of-magnitude note, not a measured benchmark.
- Measured values of the three-seed smoke (neutral cohort; **measurements, not results, no verdict**): F1 225,664 · F2 30,973 · F3 0 · F4 22,124 · F5 343,558 · F6 634,473 · F7 515,602 · F8 219,020 ppm. With 226 PA and 673 pitches they say nothing about calibration; F2, F3 and F5 outside their declared intervals at this sample is not a finding.
- **Not run:** `tools/verify.ps1` (forbidden), Codex, full Mutation, the 100,000-PA and 100-game runs, any Godot / client check beyond what the unchanged `ArchitectureTests` already do. `verify.ps1` hard-codes its test projects (U1-U4) and says "all 9 projects": it does not run the new test project until it is edited (not done at the time; **fixed in the 2026-10-17 addendum, `U5`**).

## F. Phase 3-F items still open (none became PASS by this work)

| Item | State |
|---|---|
| 100,000 PA resolver calibration | `NOT_RUN` |
| 100 complete games, reducer integration | `NOT_RUN` (3 + 10 smoke games only) |
| Eight declared targets, official verdict | `NOT_EVALUATED` (no minimum sample, no approved cohort) |
| Minimum sample policy per metric | `NOT_SPECIFIED` |
| Virtual-league cohort (rating distribution, hand mix) | `NOT_SPECIFIED`; the extension point exists, no distribution was invented |
| Strategy mix | `NOT_SPECIFIED` |
| No strategy dominates every relevant matchup | `NOT_RUN`; the declared test matrix is `NOT_SPECIFIED` |
| Pitch Zone Lite required checks | not re-run as a set; see G |
| Official seed manifest | `NOT_SPECIFIED` |
| Seed / execution hash reproduction | shown at smoke scale only (deterministic and replayable); the official run is F-C |
| Existing 2026 KBO rule profile | not verified; the profiles are `fictional_test`; no verification of the official 2026 KBO rules was made |
| Phase 4 career save / persistence | not implemented; a manifest is a run artefact, not a save |
| Git commit / push | not done |

## G. F-C pre-decisions (from the code and the ROADMAP; nothing here is assumed approved)

1. **Representative rating distribution** (F1-F5): none exists. The tuning file says "league average" for F2-F5 and "neutral inputs" for F1; the ROADMAP says "declared fixture targets, not arbitrary real-player statistics". Needed: how ratings are distributed across a virtual league, which positions, the batting-hand mix (decision 4 above), switch hitters (the side is a per-pitch fact), park mix. `CohortInputs.Create(..., CohortKind.VirtualLeague, ...)` accepts any explicit sheets and records their `InputsHash`.
2. **Strategy mix** (approach `Normal / Patient / Aggressive`, intent `Balanced / Attack / Careful`, `Take`): not defined. The default policy is only a legality proof. `strategy.approach` and `situation.count_pressure` are live slots, so the mix changes F1-F8.
3. **Official seed manifest:** not defined. `3 / 11 / 29` are regression seeds of earlier phases and `tuning.scenarios[]` seeds belong to distribution checks. A rule must be fixed before any run; selecting seeds toward the targets is forbidden by the instruction.
4. **Minimum denominator per metric:** not defined. Informational arithmetic only (not a criterion): the binomial standard error of a rate p over n independent trials is sqrt(p(1-p)/n); at the middle of the declared intervals and n = 100,000 PA it is about 1,300 ppm for F1, 900 ppm for F2, 300 ppm for F3, 500 ppm for F4; the intervals are 100,000, 50,000, 11,000 and 20,000 ppm wide. Pitches and plate appearances inside a game are correlated, so the real uncertainty is larger and `tuning.scenarios[]` uses the `binomial-4.5-sigma-ceiling-v1` rule for distribution checks, not for these aggregates.
5. **Dominant-strategy criterion:** "the declared test matrix" is referenced by the ROADMAP gate but declared nowhere. Needed: the strategies, the matchups, the metric of dominance (for example run expectancy per plate appearance), the margin and the sample.
6. **Pitch Zone Lite checks:** the ROADMAP sentence lists 12 clauses (same seed reproduces locations, events and RNG state; every pitch has a location; an in-play pitch is never counted twice; `ef1` golden byte-identical; `ef2` identified and collision-checked; legacy and new JSON formats replay; a new-format pitch with a missing location is rejected; neutral tendency vector has no effect; non-neutral vector monotonic and within its cap; ABS mode gives zero Framing effect; legacy `GameRuleSnapshot` hash unchanged; gameplay RNG not consumed by BCL / LMV). (An earlier instruction text said 11; that figure is superseded - the list has 12 clauses, see the 2026-10-17 addendum.) The owner must still approve which clauses are run and at what scale. For orientation only: most are already pinned by earlier-phase test families (`Ef1CorpusPinTests`, `EventJsonV2Tests`, `PitchBranchTendencyTests`, `StageProbabilityTests`, `GameRulesCalledPitchModeTests`, `FramingCommitTests`, `GameRulesV2ProfileTests`); "every pitch has a location" and "an in-play pitch is never counted twice" now also have runtime evidence at smoke scale (`UnlocatedPitches == 0` is enforced per game, and the pitch total is cross-checked with the box score); the BCL / LMV clause has no code to test yet (Phases 5 / 28).
7. **Large-run failure handling:** the tool continues after a failed game and lists it; whether one failure invalidates a run (and what a rerun may change) is a policy decision. Order of magnitude: a few minutes for 100,000 PA, a megabyte-scale result; per-game results (not events) are kept in memory, so 1,300+ games are fine. The `Program.SmokeMaxGames = 10` guard must be lifted by an explicit change when F-C is approved.
8. **Result files:** default `build/simharness/<run id>/` (git-ignored). Whether official run artefacts are committed (and where) is undecided.
9. **Also open:** the advancement policy for the official run (harness default v2 with runner speed and fielder arm; v1 is neutral), the rules profile (`fictional_test` profiles only; a documented 2026 KBO-oriented profile was not verified), substitution / pitching-change scope for the official games (the model has no fatigue input, so none is needed for legality), and `tools/verify.ps1` coverage of `tests/StarPlayer.SimHarness.Tests`.
10. **Independent review:** recommended for the new production entry `GameStarter` (save-data / authority boundary) and for the choice of the official cohort and thresholds before any run. Not requested or run here.

## H. Git and documents

- Branch `main`, HEAD `430f42d0dbd61c51fbde5cb10fbf2e98d1eecc02` unchanged; staged 0; commits 0; pushes 0.
- **Tracked file modified by tooling:** `StarPlayer.sln` (+16 lines: the two new projects and a `tools` folder). Documents edited (tracked): `CURRENT_STATE.md`, `ROADMAP.md` (dated status note only), `docs/game-engine.md` (section 34), `docs/decisions/ADR-007.md` (dated addendum), `docs/SYSTEM_CONTRACTS.md` (C08 status bullet), `docs/data-model.md` (non-career run manifest note), `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`. New (untracked, kept so): `src/StarPlayer.Simulation/Baseball/GameStarter.cs`; `tools/StarPlayer.SimHarness/` (10 source files, `.csproj`, `README.md`); `tests/StarPlayer.SimHarness.Tests/` (6 files + `.csproj`); `tests/StarPlayer.Simulation.Tests/Authority/GameStarterBoundaryTests.cs`; `tests/StarPlayer.Infrastructure.Tests/SimHarnessInputPinTests.cs`; this report; `Star/02_Decisions/2026-10-17_Phase_3F_FA_FB_Harness.md`.
- Protected user files unchanged (SHA-256 at start and end): `TASK.md` `99F36C7196968E8DCD473FD12E6DAFFFA60C416BE7E3DD4F8F51294BF4540BDA`, `docs/2023recordRule.pdf` `9742CAAF3A596B03D37FC6C57B9F866E989214E074AA5AF1A2AB821EC5F212B0`, `docs/2026-official-baseball-rules.pdf` `F85860F3B754DFB5E07269E9D3A86C77C89B856376F6E3BA8345AF36DB997859`. Models and tunings v1-v4, the goldens, `ef1` / `ef2`, RNG, Commit / CAS: no file changed (git shows no modification under `content/`, `tests/Fixtures/` or any existing `src/` file).
- Scratch output (smoke runs, test log) is under the git-ignored `build/`, or the system temp directory for tests.

## I. Final verdict

- **`PHASE3_F_FA_FB_IMPLEMENTED_WITH_NOTES`** (notes: the `GameStarter` production addition; the harness' own reader of two content files; the neutral cohort cannot be handedness-neutral; the metadata mismatch of the targets; the Pitch Zone Lite list is 12 clauses (an earlier "11" is corrected in the addendum); `verify.ps1` did not yet cover the new test project (fixed by `U5` in the addendum); independence of the two statistic paths is bounded as stated in C).
- **`PHASE3_F_INFRASTRUCTURE_READY`** and **`PHASE3_F_VALIDATION_PENDING`**.
- Phase 3-F gate and Phase 3 gate: **`INCOMPLETE`**. F-C is not started and is not authorized by this report.

### Gate table

| Gate | Result | Evidence |
|---|---|---|
| G1 start Git state | PASS | main, HEAD `430f42d0...`, tracked changes 0, staged 0, three untracked user files, `git diff --check` clean |
| G2 production engine reused | PASS | source scans; no draw / outcome / base-running code in the tool |
| G3 headless SimHarness run | PASS | CLI and 86 tests |
| G4 GameExecution and rules pinned | PASS | `RunConfig.CheckPins`, pin tests, manifest |
| G5 PlayerInputSet content v3 | PASS | other versions refused; id in manifest |
| G6 Seed / GameId determinism | PASS | identical hashes; seed and id each change the result |
| G7 legal game progress | PASS | engine gates only; 13 smoke games completed |
| G8 legal end | PASS | regulation, extra innings (observed), tie (fictional one-inning rules), failure distinct |
| G9 default deterministic policy | PASS (legality only) | `NormalBalancedPolicy`; no dominance claim |
| G10 budget / failure handling | PASS | budget, cancellation, exception, ids and seed kept |
| G11 same-input replay / hash | PASS | `replay` command, manifest tests |
| G12 eight metrics collected | PASS | report C |
| G13 F1-F8 numerators / denominators | PASS | oracle fixtures, hand tally |
| G14 BABIP | PASS | formula, inconsistent counts refused |
| G15 events versus BoxScore | PASS | 15 items, mismatch reporting tested |
| G16 independent oracle | PASS | hand-tallied fixtures; spot sabotage 4/4 caught |
| G17 cohorts distinguished | PASS | cohort kind in hashes, per-metric recommended cohort and approval |
| G18 insufficient / undefined states | PASS | zero denominator, unapproved minimum, cohort mismatch |
| G19 manifest | PASS | fields in D |
| G20 summary / reproduction evidence | PASS | summary, failure records, replay |
| G21 end-to-end smoke | PASS | 3 + 10 games; both call modes; failed-run replay |
| G22 models / tunings unchanged | PASS | no file changed; v1-v4 hash tests pass |
| G23 RNG / CAS / goldens preserved | PASS | no file changed; full suite passes |
| G24 full xUnit Release | PASS | 2,427 passed, 0 failed, 0 skipped, exit 0 |
| G25 official documents | PASS | section H |
| G26 protected files | PASS | SHA-256 equal |
| G27 no staging / commit / push | PASS | git state |
| G28 no 100,000 PA / 100 games | PASS | largest run 10 games; CLI refuses more |
| G29 F-C not started | PASS | stated |
| G30 Phase 3 gate INCOMPLETE | PASS | stated |

---

## Addendum 2026-10-17 - Codex review findings F-01 to F-04 (fix; implementer self-test only)

Review verdict being answered: `CODEX_PHASE3_F_FA_FB_REVIEW_REVISIONS_REQUIRED` (static review; the review text is not stored in the repository, the findings are quoted from the owner instruction `TASK.md` "Phase 3-F F-A/B Codex Findings Fix"). Base HEAD `430f42d0dbd61c51fbde5cb10fbf2e98d1eecc02` unchanged; nothing staged, committed or pushed. Every counter-example was reproduced on the real runtime (Red) before its fix.

### Result

| Finding | Severity | Reproduced (Red) | Fixed | Evidence |
|---|---|---|---|---|
| F-01 cohort mutable after creation, `InputsHash` unchanged | MAJOR | yes: 5 of 8 new tests failed before the fix | yes | `CohortImmutabilityTests` (8) |
| F-02 harness JSON reader looser than the production reader | MAJOR | yes: 20 of 87 matrix tests failed before the fix, all four Codex counter-examples among them | yes | `SimHarnessLoaderConformanceTests` (87: 83 matrix cases + 4) |
| F-03 `tools/verify.ps1` omits the new test project | MINOR | confirmed by reading the script | yes | static checks (below); the script was NOT run |
| F-04 metrics error with exit code 0 | MINOR | yes: process run with `Metrics.target_missing` exited 0 | yes | `CliExitCodeTests` (6), real child processes |

### F-01 - the cohort is an immutable snapshot, the hash describes that snapshot

- **Cause (runtime-confirmed).** `CohortInputs.Sheets` was the very `Dictionary` the constructor had filled, exposed as `IReadOnlyDictionary`: `((IDictionary<PlayerId, PlayerSheet>)cohort.Sheets).Clear()` emptied it (count 24 to 0) while `InputsHash` stayed. `Away` / `Home` kept the caller's `TeamSetup`, whose `Lineup` and `Bench` are `IReadOnlyList` over the caller's own `List`: swapping or removing items in the caller's list changed the lineup and bench the game would use, again with the same hash. `PlayerInputSet` already copies (`ImmutableArray` / `ImmutableDictionary`, sorted by id), so its id could not detect any of this, and its content version 3 does not contain contact, power, eye, stuff or velocity anyway.
- **Already safe (reported, not changed).** The caller's *sheet list* was already copied into the private dictionary at creation (replacing or removing a sheet in the list afterwards had no effect). `PlayerSheet` needs no deep copy: the ratings the engine reads are immutable records of `SkillValue` and the repertoire is a read-only copy (`PitchRepertoire.Create`); the engine reads no other sheet member (`State`, `Proficiency`, `Status` are not read).
- **Fix (`tools/StarPlayer.SimHarness/Cohorts.cs`, harness only).** `CohortInputs.Create` builds `ImmutableSortedDictionary<PlayerId, PlayerSheet>` (ordinal PlayerId order, so enumeration is identical in every process) and new `TeamSetup` values over `ImmutableArray` for both teams; the cohort exposes only those copies, and `InputsHash` is computed from those copies alone. A team setup with a missing lineup/bench list or a null lineup slot is refused as `Harness.cohort_incomplete` (it used to throw `NullReferenceException`). `GameExecutor` already read `run.Cohort.Sheets` and `Away` / `Home`, so the played inputs ARE the hashed inputs; no re-serialisation per game. The hash text, `PlayerInputSet` identity, `RunConfig.ConfigHash` format and all earlier hashes are unchanged (smoke below reproduces the earlier numbers).
- **Note from the fix.** The first version used `ImmutableDictionary`; its enumeration order depends on the per-process string hash seed, and an existing test (`Sheets.Values.First()`) failed intermittently (1 of 100 in one full run). It was found by the full run, reproduced (3 of 6 reruns), and fixed by the sorted dictionary (8 of 8 reruns pass afterwards).
- **Tests.** Red/Green pairs: casting `Sheets` back to a dictionary and mutating it; the caller's lineup list; the caller's bench list; casting the exposed lineup / bench back to `List` / array / `IList`; plus: replaced sheet in the caller's list, hash equal for equal inputs, rebuilding from the exposed snapshot gives the same hash, different contact / lineup order / bench / starter control give a different hash while `PlayerInputSet.Id` stays equal for contact (so only `InputsHash` can see it), a played game is identical after every outside mutation, and the empty-slot refusal. Contact has no effect on the played result in this model/tuning, so the game-difference check uses the starter's control; the hash difference for contact is asserted directly.

### F-02 - one input contract for both readers

- **Differences found (runtime-confirmed).** The harness DTOs were positional records: a missing `slope_ppm_per_unit` (or any number) became 0 and was accepted; `open_items`, `note`, `table`, `rule_policy` and other members could be absent; `profiles` duplicates were accepted and the first won; a `null` table entry threw `NullReferenceException`; enums were read with `Enum.TryParse(ignoreCase)` after stripping `_`, so `FICTIONAL_TEST`, `FictionalTest`, `fictionaltest` and the numeric text `"0"` were accepted; `rule_policy: null` was silently turned into `""`.
- **Fix (`tools/StarPlayer.SimHarness/HarnessData.cs`).** The DTOs now mirror the Infrastructure DTOs member for member with the same `required` set; the explicit-null checks of required objects, lists and list elements are the Infrastructure `MissingPart` checks; duplicate `profile_id` is refused (`Harness.rules_duplicate_id`); enum text is matched exactly against `SnakeCase.From(member)` (the same rule as `EnumText.Parse`); `rule_policy` is passed through as is (the Domain decides). Every validity decision remains the Domain's. No Infrastructure code, no visibility, no `InternalsVisibleTo` was changed. The one deliberate difference is unchanged: the harness reads only rules format 2.
- **Conformance matrix.** `tests/StarPlayer.Infrastructure.Tests/SimHarnessLoaderConformanceTests.cs` (the only test project that can see both readers; it now references `tools/StarPlayer.SimHarness`) runs 83 defective or real files through `TuningProfileJson.Read` / `GameRulesJson.Read` and `HarnessData.ParseTuning` / `ParseRules`. Rule: what production accepts the harness accepts with the same content hash; what production refuses (as an error or by throwing) the harness refuses as an error value, never by throwing. Message texts are not compared.

| Case group | Cases | Production | Harness (after) |
|---|---:|---|---|
| J12 real files | 2 | accept | accept, equal hashes |
| J1 required member missing (slope, neutral, min_ppm, denominator, tendency cap, target low_ppm, scenario seed, table factor, rules ints / bools / note / rule_policy / called_pitch_mode / extra_innings_limit) | 14 | refuse `Fixture.json_invalid` | refuse |
| J2 duplicate ProfileId (same content, different content) | 2 | refuse `Fixture.duplicate_id` | refuse `Harness.rules_duplicate_id` |
| J3 null element (table entry, baseline row, modifier, scenario, target) | 5 | refuse `Fixture.field_missing` | refuse `Harness.tuning_shape_invalid` |
| J3 `profiles[0] = null` | 1 | **throws `NullReferenceException`** (production defect, outside this task) | refuse (error value) |
| J3 `table: null` on a table slot | 1 | refuse (Domain `Tuning.modifier_table_keys`) | same Domain code |
| J7 required object / list null (tendency, outcomes, ppm, mode, model_version, rule_policy) | 6 | refuse (field_missing / enum_invalid / Domain) | refuse |
| J7 `profiles = null` | 1 | **throws `NullReferenceException`** (as above) | refuse |
| J7 `open_items = null` | 1 | accept (Domain accepts) | accept, equal hash |
| J8 required list / member absent (targets, baselines, modifiers, table, profiles, open_items) | 6 | refuse `Fixture.json_invalid` | refuse |
| J9 duplicate modifier slot / table key / target id | 3 | refuse (Domain codes) | same Domain codes |
| J10 number out of type, negative ulong, number as text, fraction, bool as text, Domain-judged values | 10 | refuse | refuse (Domain values: same code) |
| J4 enum case / PascalCase / no underscore (status, called_pitch_mode) | 6 | refuse `Fixture.enum_invalid` | refuse `Harness.rules_enum_invalid` |
| J5 numeric text, JSON number, leading space | 4 | refuse | refuse |
| J6 unknown, empty, comma list, unknown zone model | 4 | refuse | refuse |
| structure: unknown member (root, nested, rule), format version (tuning 2, missing, rules 3), baseline length mismatch, root array / null | 11 | refuse | refuse |
| J11 syntax (truncated, empty, trailing comma, garbage) | 6 | refuse `Fixture.json_invalid` | refuse |

The matrix is not vacuous: it asserts at least 55 production refusals. The two `NullReferenceException` rows are a defect of the PRODUCTION reader (`GameRulesJson` has no null check of `profiles[]`; the production loaders read only the embedded, valid file today): reported, not fixed, because Infrastructure is out of scope here.
- **Open observation.** Unknown command-line options are ignored by `Program.Parse` (existing behaviour, not changed).

### F-03 - `tools/verify.ps1`

`U1`-`U4` (and `U3a` / `U3b`) are untouched. Added `U5` "Phase 3-F SimHarness tests" (`tests\StarPlayer.SimHarness.Tests`) to the same `$tests` table, so it runs through the same loop (Release, `--no-build`, summary parse, fail on failed / skipped / unparsable). The B1 label "all 9 projects" now says "all 11 projects" (the solution has 11 projects since the two harness projects were added; `Get-SolutionMappingErrors` is unchanged). Checks: PowerShell parser reports 0 errors; `U5` appears exactly once; U1-U4 lines identical; the path exists. The script was NOT run (not approved).

### F-04 - exit code and metric errors

- **Cause (runtime-confirmed).** `Program` returned `AllCompleted ? 0 : 1`; `AllCompleted` only counts games. A tuning without the `babip` target gave all games `Completed`, `Metrics` = `Metrics.target_missing` in `summary.json`, and exit code 0.
- **Policy (`SimRunner.cs`, `Program.cs`, `RunReport.cs`).** `RunResult.MetricsComputed` (the computation worked) and `Succeeded`. `Program.ExitCodeOf`: **0** every game completed and the metrics were computed - including every `NOT_EVALUATED`, `INSUFFICIENT_SAMPLE` and official `FAIL` verdict, which are results of a working computation; **1** a game failed or was cancelled (outranks a metrics error); **3** every game completed but the metrics could not be computed (`Metrics.target_missing`, `Metrics.counts_inconsistent`); 2 bad input (unchanged). `summary.json` has `run_status` (`COMPLETED` / `GAMES_FAILED` / `METRICS_ERROR`) and `metrics.error_code`; stderr gets one `METRICS ERROR code: message (run, config hash, inputs hash)` line; the text report, GameIds, seeds, `config_hash`, `cohort_inputs_hash` and the manifest are written as before. The metric formulas, targets, `NOT_EVALUATED` / `INSUFFICIENT_SAMPLE` logic are untouched.
- **Test seam.** `run --tuning FILE` reads a tuning file through the same strict `HarnessData.ParseTuning` (a bad file is exit 2). The manifest records `tuning_source` (`embedded` / `file`) and `tuning_file_sha256`; a manifest of a file-tuning run is refused by `replay` (exit 2) because the manifest holds only the file's hash. `embedded_tuning_file_sha256` stays the hash of the embedded file. The test fixture is written to the temp directory (the babip target renamed); no versioned content file is touched.
- **Tests (`CliExitCodeTests`, 6).** Real child processes (`dotnet StarPlayer.SimHarness.dll ...`): normal run exit 0 with all eight metrics `NotEvaluated`; metrics error exit 3 with `Metrics.target_missing` and the metric name on stdout, `error_code` / `run_status` / games / seeds / hashes in the files; failed game exit 1, with and without a metrics error; the value `Program.Main` returns equals the process exit code (0, 3, 1, 2, 2); `INSUFFICIENT_SAMPLE` (all denominators zero), `NOT_EVALUATED` and an official `FAIL` verdict give 0; the library result distinguishes the error from the verdicts.

### Regression and tests (actual)

| Project | Before (F-A/B report) | After | Change |
|---|---:|---:|---:|
| Domain.Tests | 327 | 327 | 0 |
| Simulation.Tests | 741 | 741 | 0 |
| Application.Tests | 127 | 127 | 0 |
| Infrastructure.Tests | 1,146 | 1,233 | +87 (`SimHarnessLoaderConformanceTests`) |
| SimHarness.Tests | 86 | 100 | +14 (`CohortImmutabilityTests` 8, `CliExitCodeTests` 6) |
| **Total** | 2,427 | **2,528** | +101 |

- Full xUnit, Release, project by project (`dotnet test tests/StarPlayer.<X>.Tests -c Release`): **2,528 passed, 0 failed, 0 skipped**, every exit code 0. No existing test was deleted, skipped or weakened.
- Preserved (all in those suites): F-A determinism and pins (`GameStarter.Begin`, `PitchCommitGate` / `CommitGate`, `GameCursor.Publish`, `GameExecution`, `PlayerInputSet` content v3, `GameRuleSnapshot`, seed / GameId determinism, legal end / tie, event budget, failure records, event / receipt hashes), F-B (F1-F8 formulas, BABIP, HBP-free swing rate, actual-location zone rate, 15-item BoxScore cross-check, independent oracle, manifest / summary, `NOT_EVALUATED` / `INSUFFICIENT_SAMPLE`, neutral / virtual-league distinction), Phase 3-E tests. Smoke on the built tool, seeds 3 / 11 / 29: non-automated 226 PA, 673 pitches, 899 events, F1 225,664 / F2 30,973 / F3 0 / F4 22,124 / F5 343,558 ppm - the same numbers as the F-A/B report; automated 233 PA, 692 pitches, 925 events (one extra-innings game); all six games completed and cross-checked; `replay` of the non-automated manifest matched `run_result_hash` `28e0536c...`; exit 0 for both runs.
- **Sabotage (4 limited, each compiled, restored, SHA-256 verified equal, suite re-run afterwards):** S1 cohort keeps the caller's dictionary and lists: 4 of 8 `CohortImmutabilityTests` fail; S2 `InputsHash` ignores contact and bench: 1 fails (the changed-inputs-differ-in-hash test); S3 `slope_ppm_per_unit` not required: 2 fail (the Codex counter-example and the J1 case); S4 exit code looks only at `AllCompleted`: 3 `CliExitCodeTests` fail. S2 is a "hash does not cover input" mutation; a hash computed lazily from the caller's originals was not separately mutated. Full Mutation: `미실시`.
- **Not run:** `tools/verify.ps1` (forbidden), Codex, full Mutation, 100,000 PA, 100 games, F-C, Phase 4 save, Godot / client checks beyond the existing architecture tests.

### Contracts and documents

- Unchanged by this fix: `prob-model` / tuning v1-v4 and their hashes, `PlayerInputSet` content versions and ids, `GameStarter`, `GameExecutor`, the metric formulas, the RNG, Commit / CAS, `ef1` / `ef2`, goldens, `RunConfig.ConfigHash` text, `content/`. Files changed in tracked paths by this fix: `tools/verify.ps1`, `tests/StarPlayer.Infrastructure.Tests/StarPlayer.Infrastructure.Tests.csproj` (+ the harness project reference), and the documents named in section H.
- **Pitch Zone Lite check list = 12 clauses** (ROADMAP 3-F "tests" paragraph, counted clause by clause; nothing was removed from the ROADMAP): (1) the same seed reproduces locations, events and RNG state; (2) every actual pitch has a location; (3) an in-play pitch is never counted twice; (4) legacy `ef1` golden vectors are byte-identical; (5) new `ef2` events are identified and collision-checked; (6) legacy and new JSON formats both replay; (7) a new-format pitch with a missing location is rejected; (8) a neutral tendency vector has no effect; (9) a non-neutral vector is monotonic and within its cap; (10) ABS mode gives zero Framing effect; (11) the legacy `GameRuleSnapshot` hash is unchanged; (12) gameplay RNG is not consumed by BCL/LMV. Earlier statements of "11" are superseded by this count; which of the 12 are run, and at what scale, is still an F-C decision.
- **F-C policies NOT decided here:** the same-hand 4% correction for the F1 neutral reading, the virtual-league policy for F2-F5, the minimum samples, the dominant-strategy matrix, the representative cohort, the strategy mix, the official seed list, the official advancement policy and rules profile.
- Git: staged 0, commits 0, pushes 0. Protected files `TASK.md` (SHA-256 `1D50005E0083A1B199C7AECD6C31FFDEB1EA6152BDCCD8DC9ED8AE886573886E`, the current instruction), `docs/2023recordRule.pdf` and `docs/2026-official-baseball-rules.pdf` unchanged (start = end).
- Gates: Phase 3-F gate and Phase 3 gate `INCOMPLETE`; F-C not started. Verdict of this fix: `PHASE3_F_FA_FB_CODEX_FINDINGS_FIXED_WITH_NOTES` (notes: the production `GameRulesJson` null-element defect, unknown command-line options ignored, independent re-review `미실시`); `PHASE3_F_INFRASTRUCTURE_READY`; `PHASE3_F_VALIDATION_PENDING`.
