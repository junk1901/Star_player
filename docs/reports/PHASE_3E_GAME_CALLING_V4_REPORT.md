# Star Player Phase 3-E Game Calling v4 GC-A/B/C Implementation Report

## A. Overview

- Date: 2026-10-17 · Executor: Claude Code (Sonnet 5.5) · Branch `main` · Base HEAD `131dcac87cefdbf68a19a5032652c1e67f000fb8` (the locally committed Framing v3; not pushed). HEAD is unchanged at the end: **no staging, no commit, no push**.
- Authority: owner instruction `TASK.md` "Star Player Phase 3-E Game Calling v4 Integrated Implementation"; owner policies GC1-GC10; the additional contracts **GC-S1** (pitcher Control authority and freshness) and **GC-S2** (roll authenticity) that accept the Codex architecture review `CODEX_PHASE3_E_GC_ARCH_REVISIONS_REQUIRED`. **The review text is not stored in this repository**, so findings GC-F01 / GC-F02 (both MAJOR) are recorded as owner-approved contracts and are not quoted.
- Scope delivered: GC-A (model / tuning v4), GC-B (input set v3, pitcher / catcher inputs, resolver, commit evidence, roll replay), GC-C (integration, regression, oracle, sabotage, documents). v4 is an ADDITION; v1-v3 models, tuning files, hashes, goldens and `ef1` / `ef2` receipts are untouched.
- Contract conflicts: none that blocked the work. Two points were resolved inside the approved contracts (see ADR-007 "Decisions (Game Calling v4...)", items 11-13): `pitcher.control` keeps its single connection (the resolver therefore requires the abilities under v4 instead of a second creation site), and the commit replay applies to v4 only (GC-S1 forbids a retroactive change of v1-v3).
- Verdict: **`PHASE3_E_GC_V4_ABC_IMPLEMENTED_WITH_NOTES`**. Phase 3-E implementation gate (implementer's own judgement, independent review `미실시`): **`PHASE3_E_IMPLEMENTATION_GATE_PASS_PENDING_COMMIT`**. **Phase 3 Gate: `INCOMPLETE`** (Phase 3-F not done). Review status: `검토 대기` (no GPT Chat / Codex review has taken place).

## B. GC-A Model / Tuning

| Item | Result |
|---|---|
| `BorderlineTarget` | New value of the closed `ModifierConditionScope` (`All = 0`, `NonAutomatedBorderline = 1`, `BorderlineTarget = 2`). Enforced in `StageProbability.AppliesTo`, i.e. inside `Compute`: each non-open scope is defined for exactly one stage, any other pairing or an unknown value is `Modifier.condition_scope_unsupported`, a non-zone condition is an error. Applies iff the AIMED zone (the condition key of `actual_location`) is `Borderline`. |
| Model v4 | `prob-model-v4` = v3 with `catcher.game_calling` active (Linear, `actual_location`, `@condition`, positive, 0-100, `BorderlineTarget`). Every other slot / stage is equal to v3 (tests compare each slot incl. owner note). `battery.chemistry` still the only reserved slot. |
| Tuning v4 | `content/tuning/virtual_tuning_v4.json` (embedded resource, `TuningProfileJson.LoadVirtualV4`). Differs from v3 in exactly four places. Neutral 50, 1,000 ppm per point, factor 950,000-1,050,000. **Canonical content hash `5011f8acaae4848481db6bc60f636d7594b2a30fcdf594aeab375f0aa98b8fd8`** (computed first by `gen_virtual_tuning_v4.py --hash`, matched by C#); raw file SHA-256 `992D7E2487CC53CA0AB2D8F64CE4EC66F76B2430B834D276F47378C2E3D88C1C`. v1 `5b70dca7...3ea5`, v2 `7f02cb4f...dd70`, v3 `682a5f06...e389a` unchanged. |
| Version pairing | Explicit table v1/v1, v2/v2, v3/v3, v4/v4 (`ProbabilityModel.TuningFor` / `Pairs`); `TuningProfile.Create` rejects the other twelve (both directions, tested); `GameExecutionRules` accepts the v4 pair, keeps the park range 800-1250 permille and requires `PlayerInputsId` in every mode (`Game.execution_inputs_required_game_calling`); legacy / v2 / v3 are never promoted. |
| Control x Game Calling | Multiplicative on the same outcome, existing product / half-up rounding / combined cap 800,000-1,250,000 / one normalisation, no extra roll. Borderline aim, Control / GC: 0/0 = **878,750**, 0/100 = **971,250**, 50/50 = **1,000,000**, 100/0 = **1,021,250**, 100/100 = **1,128,750**; the cap is never reached. **The overlap is NOT removed**; it is bounded by the cap. |
| Scope matrix | 17 aimed zones x 2 modes x GC {0, 50, 100} x Control {0, 50, 100} = **306** combinations (+ 408 further ratings = **714** oracle cases): 12 Borderline applied, 5 Clear `Applied=false` with factor 1,000,000 and a distribution identical to a call without GC; modes give identical rows; GC input to any other stage is `Modifier.slot_stage_mismatch`; before v4 any non-neutral GC is `Modifier.reserved_not_active`. |
| Independent oracle | `generate_phase3e_game_calling_oracle.py` -> `phase3e_game_calling_oracle.json`. Reads only the v4 JSON; DERIVES the Clear / Borderline split from the geometric rule (not from the C# table or a copied list); own integer arithmetic and largest-remainder normalisation; asserts the five approved combined factors itself. Production = oracle for all 714 cases. |

## C. GC-B PlayerInputSet

- **Content v3** (`player-input-set-v3`, `PlayerInputSet.CreateForGameCalling`): line `<id>|fielding|speed|arm|framing|game_calling|control`, null token `-`, ordinal PlayerId order, invariant digits; own prefix line. Three literals (base, GC+1, Control+1) computed independently in Python are pinned. Role-nullable: a role rating the player does not have is `null`, refused only when the game needs it for the player standing there (never 50).
- **v1 / v2 preserved**: identities are the pinned literals; `Create` / `CreateForFraming` never produce v3 and never capture the new fields (changing only those ratings does not move a v1 / v2 id); the entry type holds only a PlayerId and nullable integers; changing GC, Control or Framing changes the v3 id.
- **FrozenCatcherInput** now has nullable `ReceivingFraming` / `GameCalling`; `FramingRequired` (Framing slot active AND non_automated) and `GameCallingRequired` (v4, every mode) are separate; v4 Automated freezes GameCalling only (the set's framing is never read; changing it changes no pitch); v4 NonAutomated both. Typed errors `catcher_game_calling_missing`, `catcher_ratings_missing`, `catcher_input_format`, `catcher_input_unexpected`, ...
- **FrozenPitcherInput** (new): game, set id, pitcher PlayerId, Control; exists exactly when the model pins Control (v4).
- **Prepare / CheckPrepared**: `Prepare` reads the current catcher and pitcher from the `GameState` and the pinned set before the first draw and refuses a sheet whose Control differs (`PitchPrepare.pitcher_control_mismatch`); `CheckPrepared` 6c compares the catcher and both ratings (`game_calling_changed`), new 6d re-derives pitcher and Control from the real state and the set the GAME pinned (`pitcher_input_mismatch`, `pitcher_game_mismatch`, `player_inputs_changed`, `pitcher_changed`, `pitcher_control_changed`). The resolver validates both inputs before drawing (`PitchBranch.pitcher_*`, `catcher_framing_mismatch`, `catcher_game_calling_mismatch`, `abilities_missing`).

| Model / mode | GameCalling | ReceivingFraming | PitcherControl |
|---|---|---|---|
| v1 / v2 | existing | existing | existing (sheet) |
| v3 automated | not needed | not needed | existing (sheet) |
| v3 non_automated | not needed | required | existing (sheet) |
| v4 automated | required | not read | required (pinned set) |
| v4 non_automated | required | required | required (pinned set) |

## D. ActualLocation / Commit

- Authority (GC-S1): `state.Fielding.Pitcher` + the content-v3 set pinned by `GameExecution.PlayerInputsId`; the Control in a `PreparedPitch`, audit or trace is never the expectation; a new base Control is never adopted mid-game.
- `CommitEvidence.LinkActualLocation` (step 7b, before the reducer re-run and the CAS): re-derives pitcher / Control / catcher / GameCalling from the real state and set; requires one `target_zone` then one `actual_location` step; recomputes both rows (condition, distribution, trace, combined factors and cap flags, modifier entries with input / factor / applied / targets) with the SAME `PitchModifierInputs` / `FrozenCatcherInput.Extend` code the resolver uses; checks target, landing zone, boundary class and side. Step order / count, stream and intent are checked. Codes: existing `Commit.candidate_link_mismatch` / `Commit.rng_evidence_mismatch`; no new gate code.
- All forged parts below are refused with the aggregate, revision, event sequence, RNG snapshot and last commit unchanged (asserted with a before / after snapshot, `Same` on the instances): target zone, condition key, outcome zone, modifier input, applied flag, control / game-calling / combined factor, cap flag, distribution, baseline, modifier list emptied or extended, stream, tuning hash in the trace, steps swapped / missing / duplicated, intent.
- GC-F01 counterexamples (each its own test): same pitcher id and hand with Control 80 on the sheet; a set that pins 80; the frozen record rewritten to 80; Control and the recorded distribution forged TOGETHER (refused by the recomputation even though `CheckPrepared` passes); the same for GameCalling; a real catcher swap; another catcher id; a real pitching change; another game's / another set's frozen input; a set the game did not pin.

## E. RNG verification (GC-S2)

- `PitchRollReplay` (new file): built only from a `GameRandomSnapshot` through `GameRandom.Restore`; the private copy shares nothing mutable with the aggregate, cursor, candidate or any other replay; never returned or published; used only by `CommitEvidence`. Streams are the existing ones (`game/target@1`, `game/location@1` = the actual-location stream, then `game/pitch@1`, `game/contact@1`, `game/field@1` as the steps name them); the bound is `NextBounded(1,000,000)` with the existing rejection sampling; no stream, stage or draw was added or moved.
- Compared per step: roll and raw draws; for the two location stages also condition, distribution, outcome (from the REPLAYED target, not a recorded value); finally the replay position vs the draft's H1. The adopted position is still the candidate's own snapshot (tested: adopted streams grew by exactly the recorded raw draws, same instance, no new stream; H0 unchanged).
- **GC-F02 counterexample** (explicit test): a roll changed to another number inside the SAME outcome interval - the forged roll selects the same outcome from the recorded table, H1 / `NextBoundedCalls` / usage / outcome are identical, the honest candidate is accepted - is refused with `Commit.rng_evidence_mismatch` for target, location and later stages, base and resolved in-play, both modes. A pure table re-evaluation cannot detect it.
- Cross-checks independent of the replay's own code: `generate_phase3e_game_calling_rng_reference.py` (independent PCG32 + `kd1`, self-checked against the published pcg-random.org demo output) -> 402 start positions incl. 6 genuine rejected first draws on the target / location streams and 6 seeds with different raw-draw counts on the shared pitch stream; the rolls and raw draws the real resolver drew equal it for all 402 (corrected 2026-10-17, F-02: the COMMIT-path test runs only the 270 positions with `prior_target_calls <= 1`, all 270 of which stage; the other 132 are not run through the commit path - see section L), and a RawDraws swap between two steps of one stream (invisible to the per-stream growth check) is refused. Direct `CommitEvidence.CheckLink` tests isolate the roll guard, the raw-draw guard and the position guard from the later step 10b.
- Existing guard "the evidence runs no draw" (`PitchCommitBoundaryTests`) is unchanged; a new guard pins the replay file's vocabulary and that only the evidence uses it.

## F. Integration regression

Fifteen scenarios of the instruction run as complete games through the real pipeline (prepare, plan, advancement, staging with evidence and replay, publication, finalization; sums, caps, allowed slots per stage, streams, retained execution checked on every pitch): legacy v1; model v2 with park / fielding / advancement v2; v3 automated and non_automated; v4 automated GC 0 / 50 / 100; v4 non_automated Framing 0 / GC 50, 50 / 50, 100 / 100; Control x GC corners (0/0, 0/100, 100/0, 100/100, both modes); everything active at once (park, varied fielders / runners / arms, advancement v2, Framing 80, GC 75, Control 65). Neutral v4 (GC 50, Control 50, Framing 50) = the v3 game = the v2 game receipt for receipt (semantic events); only version metadata differs (model / tuning / hash / input-set id). Full receipt bytes are NOT compared. At pitch level a neutral v4 pitch equals the v3 pitch in every step, roll, raw draw, probability, outcome, position H1 and stream usage for Control 30 / 50 / 90 in both modes. A single compare-and-swap among eight contenders on a v4 base: exactly one wins. Cancellation at start / validated / applied leaves everything unchanged and a later commit still works. Unsupported inputs (no set, v2 / v1 set, another v3 set, a sheet Control that differs from the pin) stop the game at the first pitch with a typed error. Existing Park / Fielding / Advancement / Framing suites unchanged and green.

## G. Tests

Environment: Windows 11, .NET SDK 8.0.425, `dotnet build StarPlayer.sln -c Release --no-incremental` (0 warnings, 0 errors), `dotnet test StarPlayer.sln -c Release --no-build` (exit code 0).

| Project | PASS | FAIL | SKIP | before |
|---|---:|---:|---:|---:|
| Domain | 327 | 0 | 0 | 316 |
| Application | 127 | 0 | 0 | 127 |
| Simulation | 738 | 0 | 0 | 728 |
| Infrastructure | 1,141 | 0 | 0 | 1,003 |
| **Total** | **2,333** | 0 | 0 | 2,174 (+159) |

New tests: Domain `ProbabilityModelV4Tests` 11; Simulation `GameCallingExecutionTests` 9 and one replay pin in `PitchCommitBoundaryTests`; Infrastructure `TuningV4Tests` 16, `GameCallingScopeMatrixTests` 21, `GameCallingInputSetTests` 11, `GameCallingPrepareTests` 23, `GameCallingCommitTests` 22, `GameCallingResolverTests` 13, `GameCallingIntegrationTests` 22, `GameCallingLinkTests` 10 (theory rows counted individually). New fixtures: `phase3e_game_calling_oracle.json`, `phase3e_game_calling_rng_reference.json` + their generators, `virtual_tuning_v4.json` + `gen_virtual_tuning_v4.py`.

Existing tests changed on purpose (none weakened, no assertion removed, no skip): six methods (nine lines) that used v4 as the "unknown version" example now use v5; the Framing wiring pin became a catcher-slots pin (stricter); `FramingResolverTests` passes the model to `FrozenCatcherInput.Build`; helpers carry the pitcher input / sheet provider (list in ADR-007).

### Sabotage (local, one production area each; original bytes restored and SHA-256 verified by the script)

| # | Mutation | Result | Caught by |
|---|---|---|---|
| S1 | `BorderlineTarget` scope removed (open scope) | DETECTED | 14 tests |
| S2 | Clear aim also applies GC | DETECTED | 10 tests |
| S3 | Control input not bound to the pinned set (resolver + 6d) | DETECTED | 2 tests |
| S4 | `CheckPrepared` stops comparing the pitcher Control | DETECTED | 4 tests |
| S5 | prepared pitch's catcher trusted (6c and the commit link) | DETECTED | 11 tests |
| S6 | GameCalling left out of the v3 canonical text | DETECTED | 5 tests |
| S7 | recorded `actual_location` row not compared | DETECTED | 7 tests |
| S8 | roll authenticity check removed (raw draws and H1 kept) | DETECTED | 6 tests incl. the GC-F02 same-interval tests |
| S9 | raw-draw comparison removed | DETECTED | 2 tests (incl. the same-stream swap) |
| S10 | replayed H1 comparison removed | DETECTED | 1 test (`CheckLink` position guard) |

10 run, 10 detected, 0 compile failures, 0 not detected, 0 `NOT_RUN`. Unlike the Framing round, "audit catcher trusted" is detected even when only the commit link is changed, because `CheckLink` is now tested directly. The raw-draw and H1 guards overlap with the later step 10b by design; they were therefore tested at `CheckLink` level so that removing them is visible. Full Mutation was not run (forbidden).

## H. Documents / Git

- Modified documents: `CURRENT_STATE.md`, `ROADMAP.md` (status note only, no plan change), `docs/game-engine.md` (new section 33), `docs/data-model.md`, `docs/SYSTEM_CONTRACTS.md`, `docs/decisions/ADR-005.md` / `ADR-006.md` / `ADR-007.md` (dated addenda; ADR-006 reconciles the "Phase 7" wording of Game Calling), `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`. New: this report, `Star/02_Decisions/2026-10-17_Game_Calling_V4.md`. Correction made: Framing v3 is committed locally as `131dcac` (earlier text said "not committed").
- New / modified source files (all untracked or unstaged): see `git status`; production: `ProbabilityModel.cs`, `StageProbability.cs`, `TuningProfileJson.cs`, `StarPlayer.Infrastructure.csproj`, `PlayerInputs.cs`, `FrozenCatcherInput.cs`, `FrozenPitcherInput.cs` (new), `PitchRollReplay.cs` (new), `PitchPreparation.cs`, `PitchBranchResolver.cs`, `PitchBranchDraft.cs`, `PitchModifierInputs.cs`, `CommitEvidence.cs`, `GameExecutionRules.cs`, `content/tuning/virtual_tuning_v4.json` (new).
- Protected files SHA-256 before = after: `TASK.md` `7062C8C0...3992`, `docs/2023recordRule.pdf` `9742CAAF...12B0`, `docs/2026-official-baseball-rules.pdf` `F85860F3...7859`. 30 pre-existing fixture / tuning / rule / content files (all goldens, `ef1` / `ef2` corpora, tuning v1-v3, rule packs) byte-identical to their start hashes.
- Git: branch `main`, HEAD `131dcac87cefdbf68a19a5032652c1e67f000fb8` unchanged, 0 staged, 0 commits, no push / fetch / pull / reset / rebase; `git diff --check` exit 0 (only the repository's LF / CRLF notices). Not run: Codex, full Mutation, `tools/verify.ps1`, 100,000-PA / 100-game validation, Phase 3-F.

## I. Phase 3-E gate (ROADMAP 3-E and the instruction's section 29)

| Item | Result | Evidence |
|---|---|---|
| CalledPitchMode: automated, non_automated, fixed contract, undeclared refused | PASS | existing 3-C/3-E suites + v4 scenarios in both modes |
| Framing: NonAutomated Borderline only, Automated 0, Clear none, cap, real catcher rating | PASS | unchanged v3 oracle / matrix; v4 keeps Framing rows equal to v3 (all 34 keys x 3 ratings); automated v4 never lists or reads Framing |
| Game Calling: Borderline target scope, current catcher rating, approved slope / cap, Control combination, input freshness, ActualLocation commit verification | PASS | sections B-E |
| Integration: no double application, RNG contract, CAS atomicity, v1-v3 compatibility, goldens, independent oracle, full Release | PASS | slot-once pins, per-trace slot checks over 250 pitches x 2 modes, sections F-G |
| ROADMAP 3-E "existing catcher ability and battery effects are not double counted" | PASS (limited) | `catcher.framing` (called_pitch) and `catcher.game_calling` (actual_location) are each connected once; `battery.chemistry` never appears in any trace; Blocking, Pop Time, catcher Arm, WP / PB and caught-stealing are not wired into the Phase 3 pitch resolver at all, so there is nothing to double count and nothing to test yet (recorded as a NOTE, not as PASS of those features) |
| Overall | **`PHASE3_E_IMPLEMENTATION_GATE_PASS_PENDING_COMMIT`** (implementer's judgement; independent review `미실시`) | The reviewer should confirm the "limited" row. |
| Phase 3 Gate | **`INCOMPLETE`** | Phase 3-F (calibration, 100,000 PA, 100 games) not run; production gameplay runner and Phase 4 persistence do not exist |

## J. Open NOTES

1. **Phase 3-F** not started: all numbers are virtual prototype values; balance and the overlap of Control and Game Calling are judged there.
2. **Phase 4 Save**: `GameExecution`, the PlayerInputSet content VERSION + content (now including `GameCalling` and `PitcherControl`) + identity, and the world format must be stored together; nothing was added.
3. **Gameplay Runner**: none exists; the test harness only chooses an approach for each plate appearance.
4. **BatteryChemistry**: reserved (Phase 7); a joint-effect budget with Control / Game Calling is a separate future decision.
5. **KBO official RuleProfile**: no external verification of the official 2026 KBO rules was made; the test profiles are not presented as official; a production profile (source, edition, effective date, simplifications) is separate work.
6. **Dynamic Control revision**: the pinned Control is the base value fixed at game start; fatigue / condition (D-ABILITY-1) need their own revision contract.
7. **Replay scope**: the roll replay and actual-location recomputation apply to v4 only (v1-v3 keep their evidence; extending them is an owner decision). The other stages' distributions are still re-derived only for `called_pitch` and `advancement`; re-deriving batter decision / swing / batted ball / defence rows from real inputs is not part of this work.
8. **Additional Codex review**: recommended for the commit-evidence replay and the pitcher-input authority (save-data integrity / core simulation rules); not called.
9. The Codex review text of `CODEX_PHASE3_E_GC_ARCH_REVISIONS_REQUIRED` is not in the repository.

## K. Final verdict

`PHASE3_E_GC_V4_ABC_IMPLEMENTED_WITH_NOTES` - Phase 3-E implementation gate `PHASE3_E_IMPLEMENTATION_GATE_PASS_PENDING_COMMIT` (implementer only, review pending); Phase 3 Gate `INCOMPLETE`; review status `검토 대기`; stopped for review (no next Phase started).

## L. Addendum 2026-10-17 — Codex final review `CODEX_PHASE3_E_GC_V4_FINAL_REVISIONS_REQUIRED` (F-01 MAJOR fixed, F-02 MINOR corrected)

Owner instruction: `TASK.md` "Phase 3-E GC v4 Final Review F-01 Fix" (base HEAD `131dcac`; included in the Phase 3-E closeout local commit, not pushed). The Codex review text is not stored in the repository; its two findings are recorded here from the instruction.

**F-01 (MAJOR) - cause.** `CommitEvidence.LinkActualLocation` checked only that the first two recorded steps were `target_zone` / `actual_location`, then replayed every later step on the stream THE CANDIDATE NAMED. A candidate with one extra step (for example a real bounded `game/ai@1` draw appended last), whose roll, raw draws, `StreamUsage`, `NextBoundedCalls` and `NextRng` (H1) were all made consistent with that real draw, therefore passed the replay, `DraftInconsistency` (an internal sum check) and the cursor swap. A pitch branch never draws on `game/ai@1`, so the committed H1 / stream counter contained a draw no game rule made.

**Red (reproduced before the fix).** `GameCallingStageStructureTests.F01_an_extra_real_ai_draw_...` built the forgery from a REAL replay (not constants) for every branch (HBP, called ball, called strike, swinging strike, foul, base in play, explicit-take called) in both call modes and called the real `PitchCommitGate.Stage`: the first case (`automated/hbp`) was STAGED (and publishable); the 'pitch-stream step appended' variant too. After the fix both are refused with `Commit.candidate_link_mismatch`.

**Fix (v4 path only; `CommitEvidence.cs`).** Before any step is replayed, `StructureProblem` derives the allowed structure from the actual branch and compares the recorded list to it - the recorded list is never the source of what is allowed. The table was re-derived from `PitchBranchResolver.Resolve`: HBP = `target_zone`(game/target@1), `actual_location`(game/location@1), `batter_decision`(game/pitch@1); called ball / strike adds `called_pitch`(game/pitch@1); swinging strike / foul adds `swing_result`(game/contact@1); in play adds `swing_result`(game/contact@1), `batted_ball`(game/contact@1), `defense_result`(game/field@1). Count, stage order and the stream of each stage must equal it exactly; additionally the branch label must agree with the recorded decision (`hit_by_pitch` <=> HBP; `take` or an explicit Take <=> called; otherwise a swing whose `swing_result` outcome maps to the label). The replay of the steps from index 2 now uses the contract's stream (equal to the recorded one by the check). Nothing else changed: no new draw, stream, RNG algorithm, model, tuning, golden, `ef1`/`ef2`, `PlayerInputSet`, CAS or GC-S1 / GC-S2 behavior; v1-v3 commits are untouched. The check runs inside `LinkActualLocation`, i.e. in `CheckLink` (step 7b), before the RNG evidence (10b), the preview and the cursor swap, for non-in-play and in-play (base and resolved) candidates alike.

**Tests.** New `GameCallingStageStructureTests` (4): honest structure of every branch pinned as literals and staged / published; the extra real `game/ai` draw forged consistently (roll, raw draws, usage, calls, H1 recomputed by a real replay) is refused for every branch with `Commit.candidate_link_mismatch`; 12 further structural forgeries x 14 candidates, each rebuilt consistently from a real replay (extra pitch / contact / target-stream step, ai step before the target and after the location, last step removed or duplicated, decision and next step swapped, a stage on another stream, ai stream in place of the stage's own, a wrong stage kind, a stage of another branch) are refused with the same code (an `Rng` code alone would not satisfy the test); a relabelled branch is refused. Every refusal leaves aggregate, revision, event sequence, RNG, last commit and receipts unchanged.

**Sabotage (local; original SHA-256 `b159a4ca...5cc7` restored after each, verified).** S1 structure check disabled: 2 of 4 new tests fail. S3 structure ignored and the recorded stream replayed: 2 of 4 fail. S2 stream comparison only removed: first run NOT detected (a wrong recorded stream was still refused, but as an `Rng` mismatch by the replay on the contract stream, so the stream check was masked); the structural cases were therefore pinned to `Commit.candidate_link_mismatch`, and S2 is then detected (1 failure). Full Mutation, `tools/verify.ps1` and Codex: not run.

**F-02 (MINOR) - correction.** The independent PCG32 reference holds **402** start positions. The resolver's target / location roll and raw draws are compared with all 402. The COMMIT path is a narrower claim: that test keeps only `prior_target_calls <= 1` (**270** eligible) and was previously described as if all 402 staged. Measured now (counters added to the test, asserted to add up): records 402, eligible 270, staged 270, skipped because no candidate could be built 0. So the commit replay accepted **270** positions, not 402; the earlier sentence "every position stages" is withdrawn (section E above is corrected). The other 132 positions were not run through the commit path (option A, documentation correction; no test was widened).

**Results.** Full xUnit Release: Domain 327, Application 127, Simulation 738, Infrastructure 1,145 (1,141 + 4 new) = **2,337 PASS**, 0 FAIL, 0 SKIP, exit code 0. Phase 3-E implementation gate remains `PASS_PENDING_COMMIT` (implementer only); Phase 3 Gate `INCOMPLETE`; review status `검토 대기`; final verdict of this addendum `PHASE3_E_GC_V4_FINAL_F01_FIXED_WITH_NOTES` (notes: S2 masking found and closed by a stricter test; other-stage row re-derivation, dynamic Control, Phase 3-F, Phase 4 Save, runner, BatteryChemistry, official KBO RuleProfile remain open as in section J).
