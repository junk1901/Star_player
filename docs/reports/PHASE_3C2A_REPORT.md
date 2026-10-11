# Phase 3-C2A Report — Single-pitch probability branch and the stream `game/target@1`

Status: **approved 2026-10-11 for 3-C2A only (see §15)**; the rest of this paragraph and sections 1–13 are the original record written when the package was `검토 대기` and are kept unchanged (their "not approved", "`미실시`" and "nothing committed" statements describe that earlier point in time). Scope: internal work package 3-C2A of Phase 3 only (owner instruction `TASK.md`, 2026-10-11, base commit `d3570b76e93246076253681568b709d3ea1d5a3b`). 3-C2B, 3-C3 and Phase 4 are not started. Phase 3 Gate: INCOMPLETE. GPT Chat review `미실시`; independent Codex review `미실시` (not started automatically, as instructed). Nothing was committed or pushed (`TASK.md` forbids it).

## 1. What was built

`PitchBranchResolver.Resolve(PitchBranchRequest, CancellationToken)` (`src/StarPlayer.Simulation/Baseball/`) connects the 3-B2 probability model (`StageProbability.Compute`, `Distribution.Select`, the virtual tuning) to the real seeded streams for ONE pitch and returns a `PitchBranchDraft`. It is a draft, not a `GameEvent`: nothing is given to `GameReducer`, no `GameState` changes, no event id or fingerprint exists. Contract text: `docs/game-engine.md` section 21; decisions: ADR-007 "Decisions (3-C2A)" and ADR-004 item 20.

## 2. `game/target@1`

`GameRngStreams.Target` = `game/target@1`: exactly one `NextBounded(1,000,000)` per actual pitch, drawn before `game/location@1`. The six earlier streams keep their purpose; `pcg32-xsh-rr-v1`, `kd1`, `rngset1`, snapshot ordering/restore and every Phase 3-A golden vector are unchanged (the Phase 3-A golden file and its tests were not edited and pass). New independent vectors (Python oracle, own PCG32 + `kd1`): key derivation and first draws for five (seed, context) pairs, `NextBounded(1,000,000)` values with cumulative raw draws, the first outputs of the six earlier streams (independence), and a rejection vector (40 calls, 41 raw draws; the call at index 26 consumed 2). Result: all 8 `TargetStreamTests` pass (the stream contract had no RED run of its own because it only needed a constant, see the RED summary).

## 3. Single-pitch branch API

Input `PitchBranchRequest(State, Tuning, Intent, ExplicitTake, BatterDeclaredSide, PitcherThrows, Rng)`; output `Result<PitchBranchDraft>`. The state is read only (rules, status, plate-appearance stage). The intent (`Balanced` / `Attack` / `Careful`) is an explicit input; nothing chooses it from hidden values. The draft holds the target zone, the `PitchLocation` (built with `PitchLocation.FromZone`), the applied side, the kind (`HitByPitch`, `CalledBall`, `CalledStrike`, `SwingingStrike`, `Foul`, `InPlay`), the outcome texts of the reached stages, the ordered steps (stage, condition, outcome, stream, exact roll, raw draws, `ProbabilityTrace`), call and draw counts, the next RNG snapshot, and the probability-model / tuning / rules identities. The stage inputs are neutral (no modifier, no tendency), so each distribution is the baseline row; no Phase 3-D / 3-E effect is active.

## 4. Stage → stream → calls

| Stage | Stream | Calls |
|---|---|---|
| `target_zone` | `game/target@1` | 1 per pitch |
| `actual_location` | `game/location@1` | 1 per pitch |
| `batter_decision` | `game/pitch@1` | 1 per pitch |
| `called_pitch` | `game/pitch@1` | 1 if the batter took (or an explicit Take that was not a hit by pitch) |
| `swing_result` | `game/contact@1` | 1 if the batter swung (never in an explicit Take) |
| `batted_ball` | `game/contact@1` | 1 if in play |
| `defense_result` | `game/field@1` | 1 if there is a batted ball |
| `advancement` | `game/runner@1` | 0 (3-C2B) |

Per pitch: hit by pitch 3 calls; called ball/strike 4; swinging strike / foul 4; in play 6. Every call is `NextBounded(1,000,000)`; raw draws are reported separately and can be higher (rejection sampling: the rejection scenario shows 1 call = 2 raw draws; the pitch's total raw draws then exceed its calls). A reached stage with a 0%/100% distribution still draws (checked on the `automated:` rows). A stage that is not reached draws nothing (checked for every branch kind of the 55 golden pitches).

## 5. Switch hitters

Right → Right, Left → Left, Switch → the side opposite the pitcher's throwing hand; recomputed per pitch, so a pitching change flips the next pitch's side and the same pitcher keeps the policy. `AppliedBatterSideRules.Validate` is called on the result; the side decision uses no RNG (proved: a switch hitter vs either hand and a right-handed batter give identical rolls and the identical next position). A missing / undefined batter side or pitcher hand is an error (`BattingSide?`, `ThrowingHand?`); the hand is required for every batter.

## 6. Explicit Take and hit by pitch

The decision is drawn once with the ordinary `batter_decision` distribution (so the baseline hit-by-pitch mass is kept; no Take-only table, no zeroed HBP). `hit_by_pitch` → HitByPitch; `take` or `swing` → watching → `called_pitch` once; `swing_result`, `batted_ball`, `defense_result` never drawn. The 55 golden pitches include a Take that ends in a hit by pitch, Takes that met a `swing` decision and ended as a called ball / strike, and normal pitches of every kind. A Take is legal at `Start` / `Decision`, a normal pitch at `InSequence`; the opposite stage fails with `PitchBranch.take_not_legal` / `PitchBranch.pitch_not_legal` before any draw.

## 7. Original state and RNG untouched

The caller's `GameRandomSnapshot` is immutable and is restored into a private `GameRandom` that continues from that position; failure or cancellation drops it. The `GameState` is only read. Tested: after a successful draft the caller's live `GameRandom` (hash and per-stream draws), the snapshot, the state's lists and its text are unchanged; the draft carries a different next position; the same input again gives an identical draft; the pitch continues from an advanced snapshot (the first roll equals the stream's fourth call, computed independently) and does not reseed; every one of the 19 invalid-input cases fails with its own code and leaves the given snapshot unchanged; a pre-cancelled request throws `OperationCanceledException`, changes nothing and works afterwards. Limits: there is no way to inject a failure after the first draw, so "failure in the middle of a branch" is covered by construction (nothing external is touched) rather than by a test; cancellation after the start of the draws is not tested.

## 8. Tests (actual results)

New: `TargetStreamTests` 8, `PitchBranchResolverTests` 73 (the golden-scenario theory has 28 cases). Changed existing test: `HiddenTendencyBoundaryTests` (`HiddenTypes` gains `PitchBranchDraft`, `PitchBranchStep`). `dotnet test StarPlayer.sln -c Release`: Domain 263, Simulation 383, Application 117, Infrastructure 339 = **1102 passed** (base 1021, +81), 0 failed, 0 skipped. The pre-existing tests of the six streams, the Phase 3-A golden vectors, the probability / tuning tests (including the tuning content hash `5b70dca7…3ea5`), PitchZone / GameRules tests, `ef1` / `ef2` / JSON tests all ran unchanged and passed. RED/GREEN and the 15 mutation checks: [PHASE_3C2A_RED_EVIDENCE.md](PHASE_3C2A_RED_EVIDENCE.md) (all 15 detected; the raw output of the mutation runs was not saved).

## 9. `tools/verify.ps1`

Two consecutive runs after all code, tests and the ADR / game-engine / contract / report documents were final: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify.ps1 -LogName PHASE_3C2A_VERIFY_LOG_run1.md` (log time 18:35:55 +09:00) and `..._run2.md` (18:38:38 +09:00): both **`GATE: PASS`**, exit 0 ([run 1](PHASE_3C2A_VERIFY_LOG_run1.md), [run 2](PHASE_3C2A_VERIFY_LOG_run2.md)). Release unit tests inside the script: Domain 263, Simulation 383, Application 117 (architecture 14, determinism detector 48), Infrastructure 339; Godot headless checks, Windows export, exported-program QHD checks and the markdown link check (86 / 87 files) PASS. Both runs were executed in this session. (A first run made before the documents were final was discarded and its log deleted.) The state documents (`CURRENT_STATE.md`, the Obsidian review and dashboard) were filled in with these results afterwards; no code or link changed. The two new logs have the same CRLF + lone-CR property as earlier verify logs (git classifies them `-text`; `git diff --cached --check` will flag every line once staged: 37 lines per log). `git diff --check` on the tracked changes is clean; the other new files have no trailing spaces or tabs.

## 10. Files

Modified: `src/StarPlayer.Simulation/Randomness/RngStreamId.cs` (stream constant and comment), `tests/StarPlayer.Application.Tests/HiddenTendencyBoundaryTests.cs` (two types added to the list), `docs/decisions/ADR-004.md`, `docs/decisions/ADR-007.md`, `docs/game-engine.md`, `docs/SYSTEM_CONTRACTS.md`, `CURRENT_STATE.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/00_Dashboard.md`. New: `src/StarPlayer.Simulation/Baseball/PitchBranchDraft.cs`, `PitchBranchResolver.cs`; `tests/StarPlayer.Infrastructure.Tests/PitchBranchFixture.cs`, `PitchBranchResolverTests.cs`, `TargetStreamTests.cs`; `tests/Fixtures/generator/generate_pitch_branch_golden.py`, `tests/Fixtures/phase3c2a_pitch_branch_golden.json`; `docs/reports/PHASE_3C2A_REPORT.md`, `PHASE_3C2A_RED_EVIDENCE.md`, the verify logs (and the git-ignored `PHASE_3C2A_RED.log`). Not touched: `GameEvents.cs`, `GameReducer*`, `EventFingerprint.cs`, `ScriptedGameJson.cs`, `ProbabilityModel` / `StageProbability` / `TuningProfile`, `content/tuning/virtual_tuning_v1.json`, `content/rules`, `phase3a_rng_golden.json`, `Pcg32`, `StreamKeyDerivation`, `GameRandom`, `ROADMAP.md`. `TASK.md` and the two rule-book PDFs stay untracked and untouched. Nothing committed or pushed.

## 11. Corrections to earlier records

ADR-007's 3-C1 table row said "implemented here, `검토 대기`"; it now says Accepted (approved 2026-10-11), as `TASK.md` §13 asked. The approved 3-C1 contract itself is unchanged. The stale `Proposed` header of ADR-004 (noted since 3-B2) is still not edited.

## 12. Open points and handoff

3-C2B: map a draft to legal `GameEvent`s (defensive credits, force/tag categories, every runner's movement, outs order, third out, walk-off, the fielder of `reached_on_error`), draw `advancement` on `game/runner@1` against the real runner situation, event ids and `ef2` receipts, mandatory location for new data, and wire the batter side / pitcher hand from the current lineup (the resolver trusts the caller's two inputs and does not check them against the batter and pitcher in the state). 3-C3: joint atomicity of state, RNG, ids and receipts; automatic plate-appearance progression; Phase 4 transaction and expected RNG hash. 3-D: modifier inputs and the tendency vector (the resolver accepts none today, by design). Others unchanged from ADR-005/006/007 (park `Hits` coefficient, Framing cap, 100,000 PA / 100 game / 8 target validation).

Limits worth the reviewer's attention: (a) the Python oracle shares its author and its reading of the stage contract with the C# code, so it proves the arithmetic, order and stream use against an independent implementation but not that the contract itself is right; (b) the Take / pitch stage rules are re-stated in the resolver (same codes' meaning as the reducer, not shared code), so a future reducer change would not automatically follow; (c) the work package changed no probability and ran no statistical calibration: distribution quality is Phase 3-F; (d) no UI, Gemini, BCL or LMV code reads a draft.

## 13. Self-assessment

**PASS WITH NOTES** for the 3-C2A work package: every requested stream, branch, side, Take, validation and no-side-effect behaviour is implemented and tested against an independent oracle, existing contracts are unchanged, and all executed checks pass. Notes: the stream contract had no RED run of its own, the mutation-run raw output was not saved, mid-branch failure injection and late cancellation are not tested, the oracle's independence is limited as stated above. Not a Phase 3 Gate result and not an approval.

## 14. Codex follow-up: game id / RNG context mismatch fix (2026-10-11, `TASK.md`)

**Review record.** GPT Chat first review: PASS WITH NOTES. Independent Codex review: **CHANGES REQUIRED, Medium 1** (`PitchBranchResolver.cs` ~line 47). The sections above are the original report and are kept unchanged; the numbers in section 8 / 9 / 13 are the pre-fix results. Final 3-C2A approval is on hold; Codex re-review `미실시` (not started automatically); 3-C2B / 3-C3 not started; Phase 3 Gate INCOMPLETE.

**Defect.** `Resolve` restored the caller's `GameRandomSnapshot` but never compared its `ContextKey` with `GameState.GameId`. `GameState.GameId = "g001"` with `ContextKey = "game:3c2a:probe"` succeeded, so a pitch of one game could be drawn from the random position of another. The cause was also in the test fixture: it fixed the state's game id to `g001` while the golden scenarios used other context keys, so the tests could not notice.

**Fix** (`PitchBranchResolver.cs`, 4 lines). After `GameRandom.Restore` succeeded (so null / malformed / tampered snapshots keep their existing `Rng.*` codes) and before the first draw, `string.Equals(state.GameId, restoredContextKey, StringComparison.Ordinal)` must hold, otherwise `Result.Failure` with the new resolver-only code **`PitchBranch.game_context_mismatch`** and a Korean message that names both values. No normalization, no trimming, no substitute context, no id rewriting; case differences are a mismatch. Matching input is not touched: probabilities, draw order, call counts and the returned next snapshot are as before.

**Fixture.** `PitchBranchFixture.StartState` / `InSequenceState` / `DecisionState` / `StateFor` now take the game id explicitly (no default, no helper that overwrites an id). Positive tests pass the golden `context` (or the test's own context constant) to both the state and the snapshot; mismatches exist only as deliberate failure tests.

**Golden vectors: unchanged.** `phase3c2a_pitch_branch_golden.json`, the Python oracle and every Phase 3-A golden vector were not touched; the golden contexts are simply used as the game ids. No expected value was regenerated, and no production code produced one.

**Tests** (+11, all in `PitchBranchResolverTests`): 5 mismatch cases (A/B, B/A, case-only, `g001` vs `game:3c2a:probe`, trailing difference) each check failure, the exact code, a non-empty message, no draft, the caller's snapshot canonical text, the live `GameRandom` hash and per-stream draws, the `GameState` text / lists unchanged, and that the same snapshot still works for its own game; 2 cases cover Take and normal pitch; one test pins that null / format / algorithm / tampered snapshots keep their `Rng.*` codes with a different game id (and that an empty game id is a mismatch); 2 success cases (`game:3c2a:a`, `game:3c2a:b`: same snapshot retried gives an identical draft, a restored snapshot continues like the draft's own next position); one test pins that the code is defined once, in the resolver only. RED: 9 failed / 75 passed / 84; GREEN: 84 passed (see [PHASE_3C2A_RED_EVIDENCE.md](PHASE_3C2A_RED_EVIDENCE.md)).

**Branch call counts (unchanged, re-checked by the existing branch tests):** hit by pitch 3, called ball / strike 4, swinging strike / foul 4, in play 6 `NextBounded` calls; raw draws are reported separately.

**Verification after the fix.** `dotnet test StarPlayer.sln -c Release`: Domain 263, Simulation 383, Application 117, Infrastructure 350 = **1113 passed** (base 1102, +11), 0 failed, 0 skipped. `tools/verify.ps1` twice in a row (`-LogName PHASE_3C2A_FIX_VERIFY_LOG_run1.md` / `..._run2.md`): both `GATE: PASS`, exit 0 ([run 1](PHASE_3C2A_FIX_VERIFY_LOG_run1.md), [run 2](PHASE_3C2A_FIX_VERIFY_LOG_run2.md)). `git diff --check`: no whitespace error (only the repository's LF/CRLF notices). Not touched: `GameReducer`, `ef1` / `ef2`, event JSON, RNG algorithm and streams, probability tables, tuning numbers, 3-C2B / 3-C3 / Phase 4 / UI; `TASK.md` and the two rule-book PDFs remain untracked and unchanged. Nothing committed or pushed.

**Status.** 3-C2A stays `검토 대기` (final approval pending GPT Chat review of this fix; Codex re-review `미실시`). Phase 3 Gate INCOMPLETE. All earlier NOTES remain open.

## 15. Final approval (2026-10-11, owner `TASK.md`)

**3-C2A is approved for this work package only: GPT Chat `PASS WITH NOTES` / final approval; Codex final focused re-review `PASS WITH NOTES`.** The earlier Codex `CHANGES REQUIRED` (Medium 1) and its fix (§14) are preserved; the Medium defect is resolved and no new Critical / High / Medium defect was found. This is not an approval of 3-C2 as a whole or of Phase 3: 3-C2B and 3-C3 are not started and the Phase 3 Gate is INCOMPLETE.

Evidence cited from the owner record (not re-run for this documentation and Git step): full xUnit 1,113 PASS (Claude run, §14), Codex direct run Release 92/92 PASS, `tools/verify.ps1` twice `GATE: PASS` ([run 1](PHASE_3C2A_FIX_VERIFY_LOG_run1.md), [run 2](PHASE_3C2A_FIX_VERIFY_LOG_run2.md); Codex checked the logs), independent Python golden verification PASS, Phase 3-A goldens byte-identical, `game/target@1` independence, branch call counts and the caller's `GameState` / RNG snapshot immutability confirmed.

**Non-blocking notes that stay open:** the pre-fix RED (9 failed / 75 passed) is known only from a summary without a raw log; the raw output of the 15 mutation checks cannot be independently reviewed; mid-branch errors and late cancellation need more verification; connecting the real batter / pitcher in the state to the request inputs (3-C2B); converting a draft into legal `GameEvent`s (3-C2B); `advancement` and runner-movement validation (3-C2B); joint atomicity of event ids, receipts, RNG and `GameState` (3-C3); DB transaction and expected RNG hash (Phase 4); 100,000 PA / 100 game statistical calibration (Phase 3-F).
