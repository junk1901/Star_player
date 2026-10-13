# Phase 3-C3-A Report — authoritative game and the single Stage/Publish gate (foundation)

Date: 2026-10-13 · Owner instruction: `TASK.md` "Phase 3-C3-A 프로덕션 구현 지시서" · Base HEAD `7456f44fd3ecd687afce210eb5be972114f8479c` (`main`, equal to `origin/main`, no tracked changes and nothing staged at the start) · Environment: Windows 11, .NET SDK 8.0.425 (pinned), Godot 4.7.2 Mono · Status: **Phase 3-C3-A 구현 완료 / 검토 대기, 승인 아님** (self-assessment in section 23). **Technical review record: `GPT_CHAT_TECHNICAL_PASS_WITH_NOTES`** (section 26).

Scope is C3-A only: it implements the owner-approved contracts D-1 to D-8, A to F and O-1 to O-9 (`docs/game-engine.md` 27.1 to 27.16, `docs/decisions/ADR-007.md`) in C#. No contract was changed. **C3-B (pitch-candidate confirmation), C3-C (stability, regression, independent review) and Phase 4 are `NOT STARTED / NOT AUTHORIZED`. The Phase 3 Gate is `INCOMPLETE`.** GPT Chat and Codex review: `미실시`. Nothing is staged, committed or pushed.

## 1. Changed files (actual)

| File | Change |
|---|---|
| `src/StarPlayer.Simulation/Baseball/AuthoritativeGame.cs` | **new** — the immutable aggregate, internal `StartNew`/`Restore`, `IsComplete`, `IsConsistent` |
| `src/StarPlayer.Simulation/Baseball/CommitRecord.cs` | **new** — the minimal immutable record (O-9) |
| `src/StarPlayer.Simulation/Baseball/StagedCommit.cs` | **new** — `Expected`, `Next`, `Record` |
| `src/StarPlayer.Simulation/Baseball/CommitGate.cs` | **new** — pure `Stage` (public) and an internal overload with a stage hook (the replaceable `Finalize` argument was removed afterwards, section 25) |
| `src/StarPlayer.Simulation/Baseball/GameCursor.cs` | **new** — the single current reference, `Publish` by compare-and-swap |
| `tests/StarPlayer.Simulation.Tests/Authority/AuthFixture.cs` | **new** — canonical ids, RNG snapshot with real draws, `CanonScript` (drives the real reducer with canonical ids) |
| `tests/StarPlayer.Simulation.Tests/Authority/AuthoritativeGameTests.cs` | **new** — 17 tests |
| `tests/StarPlayer.Simulation.Tests/Authority/CommitGateStageTests.cs` | **new** — 28 tests |
| `tests/StarPlayer.Simulation.Tests/Authority/GameCursorTests.cs` | **new** — 18 tests |
| `tests/StarPlayer.Simulation.Tests/Authority/AuthorityBoundaryTests.cs` | **new** — 6 tests |
| `tests/StarPlayer.Application.Tests/HiddenTendencyBoundaryTests.cs` | the five public types added to the hidden-type list (no test removed or weakened) |
| `docs/reports/PHASE_3C3A_REPORT.md` (this), `…_AUTHORITY_TESTS_raw.txt`, `…_MUTATION_raw.txt`, `…_FULL_XUNIT_raw.txt`, `…_VERIFY_LOG_run1/2.md`, `…_VERIFY_STDOUT_run1/2.txt` | new evidence |
| `docs/game-engine.md` (new 27.17), `docs/decisions/ADR-007.md` (3-C3-A implementation section), `CURRENT_STATE.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/00_Dashboard.md` | documentation |

Not touched: `GameReducer*`, `GameEventIds` (`GameIdentity.cs`), `EventFingerprint`, `PitchPreparation` (incl. `CheckPrepared`), the RNG, the C2B-3 resolver, `ef1`/`ef2`, event JSON, rule profiles, tuning, ROADMAP, `docs/SYSTEM_CONTRACTS.md`, SQLite, UI. `TASK.md` and the two PDFs under `docs/` are untracked and unchanged.

## 2. AuthoritativeGame (item 3)

`public sealed class AuthoritativeGame`: `State`, `Rng` (the `GameRandomSnapshot`), `LastEventSequence` (`int`), `Revision` (`long`), `LastCommit` (`CommitRecord?`). Get-only; the constructor, `StartNew` and `Restore` are `internal`, so nothing outside Simulation (or the test assembly) can build or edit one (O-7). `GameState` is an immutable record and the aggregate keeps the very references it is given.

## 3. StartNew / Restore (item 4)

`StartNew(state, rng)`: `state_missing`, `rng_missing`, a non-empty `AppliedEvents` is `initial_state_not_empty`, then the same validation as `Restore` with sequence 0 and revision 0 (so a new game also cannot start with a bad GameId, a foreign RNG context or an inconsistent final state). `LastCommit` is `null`.

`Restore(state, rng, lastEventSequence, revision)` checks, in this order: inputs; the canonical GameId (`GameEventIds.Create`, `Id.game_invalid`); the snapshot restores (`GameRandom.Restore`, `Rng.*` pass through); `rng.ContextKey == GameId` (Ordinal, `Authoritative.rng_context_mismatch`); `lastEventSequence` in 0 to 999999 (`Id.sequence_invalid`); every receipt (`history_event_id_invalid`, then `history_sequence_gap` for a number above its position and `history_sequence_duplicate` for a number already used, then `history_receipt_invalid` for kind, fingerprint version `ef1`/`ef2` and a 64-character lower-case hexadecimal fingerprint); `last_sequence_mismatch`; the final-state completeness rule (`final_state_invalid`; `Status == Final && Final == null` is refused, never finalized implicitly); `revision >= 0` (`revision_invalid`); and the explicit event references `ScoredPlay.EventId` and `SubstitutionRecord.EventId` against the receipt ids (`evidence_without_receipt`). `StatEntry.SourceId` and `ScoredPlay.Id` are not treated as event ids. `Revision == LastEventSequence` is not enforced; `LastCommit` is `null`. A refusal builds nothing and changes none of its inputs.

**Stated limit:** a receipt holds no event body, so the restore verifies id, game membership, continuity and form; it cannot verify that a stored fingerprint matches an event body that was never stored. Code comments, tests and this report call it "id and continuity", never authenticity.

## 4. EventId continuity (item 5)

The official id is `{GameId}:e{six digits}`, 1 to 999999, parsed with the unchanged `GameEventIds.ParseSequence` for this game. `Stage` requires exactly `LastEventSequence + 1`. A seventh digit, `000000`, a diagnostic id (`e1`), whitespace or a lower/upper-case slip is `event_id_invalid`; a canonical id of another game (valid context key + `:e` + six digits 1 to 999999) is `event_game_mismatch`. 999999 receipts restore (test) and the next id `…:e1000000` is `event_id_invalid`. Revision and sequence stay separate values (a test restores revision 10 with sequence 102 and stages 11/103).

## 5. CommitGate.Stage (item 6)

Pure; reads the aggregate, changes nothing shared. Order (O-2 alternative C, as implemented): 0 stage hook + cancellation → 1 aggregate/event exist → 2 kind allowed → 3 `IsConsistent` → 4 id form and game → 5 `already_committed` (existing receipt equals `EventFingerprint.Receipt(event)` in id, kind, version and fingerprint) → 6 `event_sequence_gap` → (7 C3-B insertion point, a comment) → 8 `event_id_in_use` → hook `validated` → 9 `GameReducer.Apply` on `expected.State` (a reducer refusal passes through as its own `Game.*` code; a duplicate answer, a receipt count other than +1 or a last receipt that is not the event's own is `receipt_mismatch`) → hook `applied` → (10 C3-B insertion point, a comment) → 11 `Finalize` in the same stage if the re-run state is final → 12 `Revision == long.MaxValue` is `revision_exhausted`, last cancellation check, build. Step 5 runs before step 9 because the reducer answers a re-sent identical id with a success even on a finished game; the tests pin that (`already_committed`, not success).

## 6. CommitRecord and StagedCommit (item 7)

`CommitRecord` (`public sealed`, internal constructor, get-only): `Event`, `Receipt`, `EventSequence`, `RevisionBefore`, `RevisionAfter`, `RngStateHashBefore`, `RngStateHashAfter`, `Facts` (reducer facts, plus one `GameCompleted` when this commit finalized) and `Finalized`. It names no `PreparedPitch`, candidate, draft, audit or `AdvancementDetail`, and has no `GameStatDelta` (a test pins the exact member list). It is built before the swap from the same values as the next aggregate; `Next.LastCommit` and `StagedCommit.Record` are one instance (tested with `Assert.Same`, also against the record `Publish` returns). `StagedCommit` holds `Expected` (the reference Stage read), `Next` and `Record`; it changes nothing by existing and can be published at most once.

## 7. GameCursor and the CAS (item 8)

`GameCursor` holds one `AuthoritativeGame` (internal constructor, no static or global store); `Current` is `Volatile.Read`. `Publish(staged, ct)`: `staged_missing` → `Expected` is not the current reference: `revision_stale`, no swap → stage hook `before_swap` and the last cancellation check → `Interlocked.CompareExchange(ref _current, Next, Expected)`; the answer is the staged record if the returned reference is `Expected`, else `cas_lost`. Nothing after the swap can throw, check the token or call a hook.

## 8. Revision (item 9)

`Next.Revision = Expected.Revision + 1`, computed in Stage; Publish only makes it current. A loser cannot raise it (`revision_stale` / `cas_lost`, tests). `long.MaxValue` is `revision_exhausted` (and a reducer refusal wins over it because step 9 precedes step 12, test). Successive publishes give 0→1→2→3 with continuous `RevisionBefore`/`After`.

## 9. Duplicate commits (item 10)

Same event again: `already_committed` (Stage, also against an old `Expected`). Same id, other content or kind: `event_id_in_use`. Same `StagedCommit` twice: the second Publish is `revision_stale`, state and revision unchanged. Two threads publishing the same staged commit together (held at the swap by a barrier): one success, one `cas_lost`.

## 10. Finalize in one Publish (item 11)

For a game-ending event Stage runs `Apply`, then `GameReducer.Finalize` on the result, then checks: `WasNew`, `Final != null`, `Final` is the returned game, GameId/outcome/score agree (`IsComplete`). Test: a tied bottom of the ninth, the home batter on third, a balk scores: one stage, `Status == Final`, `Final` set, `Finalized == true`, exactly one `GameCompleted`, exactly one new receipt (none for the finalization), `Revision` +1 (5→6), the RNG reference unchanged, the input aggregate still unfinished. A failing or incomplete `Finalize` produces no `StagedCommit` (so there is nothing to publish) and leaves the aggregate unchanged. A stage that does not end the game does not finalize (`Finalized == false`, no `GameCompleted`; asserted in the Approach tests). *Section 25: the replacement-based failure/incomplete tests were removed.* No aggregate with `Status == Final && Final == null` can be restored or staged.

## 11. Cancellation and races (item 12)

Cancellation is `OperationCanceledException` and is checked at hooks `start`, `validated`, `applied`, `finalized` (Stage) and `before_swap` (Publish), and once more before building/swapping. Tests: a token cancelled before the call (even with null inputs) throws first; cancelling at each stage throws and the aggregate is unchanged; cancelling after finalization throws; a token cancelled before the swap leaves the cursor unchanged and the staged commit still publishable; a cancellation after a successful Publish does not change the result; a hook that throws before the swap publishes nothing. Because no hook exists after the swap, "no failure after the swap" is shown by structure plus a source scan (no `ThrowIfCancellationRequested`, `IsCancellationRequested`, `cancellation`, `checkpoint`, `.Invoke(`, `throw `, `await ` after `Interlocked.CompareExchange`). Races use `System.Threading.Barrier` (20 s timeout) so that both publishers pass the freshness check and meet at the swap: exactly one succeeds, the loser gets `cas_lost`, `Current` is the winner's aggregate, one receipt, revision 1, no reused id. No `Sleep`.

## 12. RNG invariance (item 13)

`Next.Rng` is the same reference as `Expected.Rng` for all three allowed events (`Assert.Same`), and the record's before/after hashes are equal and equal to the aggregate's hash. The fixture snapshot has a stream that really drew twice, so the assertion is not about an empty set. No code in the five files creates a `GameRandom`, a stream or a draw (source scan in `AuthorityBoundaryTests`).

## 13. HiddenTendencyBoundaryTests (item 14)

`AuthoritativeGame`, `GameCursor`, `StagedCommit`, `CommitRecord` and `CommitGate` were added to its hidden-type list; the three existing tests still pass, i.e. no Application or Infrastructure member signature names them. Additionally `AuthorityBoundaryTests` check in Simulation: all five are `public sealed` (static class for the gate) with no public constructor; `AuthoritativeGame` has no public static factory; no member of the five names `PreparedPitch`, a candidate, `PendingAdvancement`, `BaseInPlayPlan`, an audit or a draft type; `CommitRecord` has exactly the approved members; the five sources contain none of `PreparedPitch`, `PitchCandidate`, `PendingAdvancement`, `PitchPreparation`, `InPlayPitchPlanning`, `AdvancementPlanning`, `PitchBranch`, `GameRandom.Create`, `.Stream(`, `NextBounded`, `Atomically`; the gate names `PitchEvent`/`TakeEvent`/`InPlayEvent` nowhere outside comments.

## 14. Tests added

69 new tests when first delivered (67 after the section 25 removal of two Finalize-replacement tests), all in `tests/StarPlayer.Simulation.Tests/Authority/` (17 + 28 + 18 + 6), plus the extended list in `HiddenTendencyBoundaryTests`. They cover every listed area A to G of the instruction: creation/restoration incl. the 999999/1000000 boundary and negative last sequence, bad fingerprint version/shape, RNG snapshot and context errors, negative revision, incomplete final state, evidence references; Stage for the three events, the three refused kinds, sequence +1/gap, same id same/different receipt, other game, reducer error, receipt +1, RNG reference, revision +1, failure invariance; the error priority (game mismatch vs gap, gap vs reducer error, finished-game duplicate vs new vs changed, same id identical vs different, pitch event in C3-A); CAS, stale, other cursor, race, record/`LastCommit` identity, revision chain, overflow; cancellation; Finalize.

## 15. Fault injection

`docs/reports/PHASE_3C3A_MUTATION_raw.txt`: 26 single-line mutants of the production code (CAS replaced by an assignment, stale check removed, revision +2, finalize skipped, duplicate check off, gap off by one, restore checks removed one by one, `GameCompleted` twice, post-swap cancellation check, copied RNG snapshot, pitch kinds allowed, …): **26 of 26 detected** when first delivered; after the section 25 hook removal only the Finalize-related ones were re-run (see the ledger below). One first-draft mutant was semantically equivalent (it passed) and was replaced by two real ones; five first drafts did not compile (`if (false)`) and were redone. The files were restored byte-for-byte (compared against backups). This is test-strength evidence only, not a proof.

**Mutant ledger (ID list of `PHASE_3C3A_MUTATION_raw.txt`; 26 original mutants = M1–M25 with M7 split into M7a/M7b; IDs M7 as first drafted, and the five `if (false)` first drafts, are not counted).**

| Group | IDs | Count | Result |
|---|---|---|---|
| Original, run once on the **first-delivery tree (69 tests)**, **not re-run** after the hook removal | M1, M2, M3, M5, M6, M8, M9, M10, M11, M12, M13, M14, M15, M16, M17, M18, M20, M21, M22, M23, M24 | 21 | detected then (FAIL n each); these are prior-run results, not re-confirmed on the final tree. The two removed tests were Finalize-replacement tests only, so a change in their detection is not expected but was **not** checked |
| Original, **re-run on the final tree (67 tests)** | M4, M7a, M7b, M19 | 4 | detected (FAIL 3 / 1 / 1 / 1) |
| Original, **re-run on the final tree (67 tests)** | M25 (finalize completeness check removed in `CommitGate`) | 1 | **survives** (67 passed): an unreachable defence branch whose only detector was a removed test |
| **New**, added after the hook removal (not one of the original 26) | M26 (Finalize result discarded, Apply state adopted) | 1 | detected (FAIL 1) on the final tree |

Totals: 26 original = 21 not re-run + 5 re-run (4 detected, 1 survivor). Detected on record: 21 (prior tree) + 4 (final tree) = 25 of 26; survivor 1 (M25); plus the new M26 (detected). Removed from the set: none (the earlier replaced equivalent mutant and the five non-compiling drafts were already excluded in the first delivery). The earlier "26/26" is valid only for the first-delivery tree.


## 16. Results

| Check | Result |
|---|---|
| New Authority tests (`PHASE_3C3A_AUTHORITY_TESTS_raw.txt`) | **67 run, 67 passed, 0 failed, 0 skipped** (was 69 before section 25) |
| Full xUnit (`dotnet test StarPlayer.sln`, `PHASE_3C3A_FULL_XUNIT_raw.txt`) | **1,582 run, 1,582 passed, 0 failed, 0 skipped** (Domain 263, Application 117, Simulation 689, Infrastructure 513) *as first delivered; superseded by section 25: 1,580*; baseline 1,513 + 69 new = 1,582 |
| `tools/verify.ps1` run 1 / run 2 (`-LogName PHASE_3C3A_VERIFY_LOG_run1.md` / `run2.md`) | **`GATE: PASS` both times as first delivered; re-run in section 25** (exit 0; Domain 263, Simulation 689, Application 117, architecture 14, determinism detector 48, Infrastructure 513; Godot headless checks, Release export and cold-copy checks PASS; relative markdown links: 123 files, 0 broken). Run on the final tree (documentation included); Claude Code executed both |
| `git diff --check` | exit 0 (no whitespace errors) |

Baseline before this work: 1,513 passed (Domain 263, Application 117, Simulation 620, Infrastructure 513, from the 3-C2B-3 record); observed after: 1,513 + 69 = 1,582, which matches.

## 17. Test commands

```
dotnet test tests/StarPlayer.Simulation.Tests --filter "FullyQualifiedName~Authority" --nologo --logger "console;verbosity=normal"
dotnet test StarPlayer.sln --nologo
powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1 -LogName PHASE_3C3A_VERIFY_LOG_run1.md
```

## 18. Regression

C2B-1, C2B-2 and C2B-3 tests, the golden vectors, `ef1`/`ef2`, the RNG tests and the determinism detector ran unchanged and passed in the full run (see section 16). No existing test was edited except the list in `HiddenTendencyBoundaryTests`, which only grew.

## 19. Design mismatches and interpretations found

- **No contract conflict found.** Interpretations inside the approved text: a "canonical id of another game" needs a valid context key as the game part; the restore check order (section 3) follows the 27.16 list with the evidence references last; `StartNew` runs the full restore validation after its own two checks.
- **`Commit.receipt_mismatch`, `Commit.authority_inconsistent`, `Commit.finalize_failed`, `Commit.final_state_incomplete` are defences** that the real reducer and the factories make unreachable. `receipt_mismatch`/`authority_inconsistent` are tested through a crafted aggregate (internal constructor). The replaceable `Finalize` seam that used to reach `finalize_failed`/`final_state_incomplete` was **removed by owner decision (section 25)**; those two branches are now untested defences.
- `GameCursor`'s constructor is `internal` (O-7), so no production caller creates a cursor yet; C3-B/Phase 4 will own that.

## 20. Unresolved defects and risks

None found. Known limits: restoration cannot verify fingerprints against bodies (section 3); the 999999-receipt test builds a 1,000,000-element list (about a second, tens of MB) and could be slow on a very small machine; the race tests need two free thread-pool threads for the barrier (20 s timeout fails loudly rather than hanging); the stage hook is checked per stage, not per statement, so a cancellation between two stages is seen at the next one; the structural-comparison fixtures (C3-B), the Phase 4 persistence list of `CommitRecord` and the ROADMAP 3-F label remain open.

## 21. Git state

Recorded at the end of the work in section 24.

## 22. Hand-off to C3-B

**C3-C final-review check items:** (1) coverage limit of the M25 defence branch: `Commit.finalize_failed` and the post-Finalize `Commit.final_state_incomplete` are unreachable with the real `GameReducer.Finalize` and untested since the replacement hook was removed (M25 survives); decide whether that is acceptable or needs a reachable, non-bypass test; (2) the 21 original mutants not re-run after the hook removal should be re-confirmed on the final tree.

Steps 7 and 10 of `CommitGate.Stage` are comment-only insertion points. A pitch-carrying proposal needs its own entry that (a) links the `(PreparedPitch, candidate)` pair, (b) runs the unchanged `PitchPreparation.CheckPrepared` against the current aggregate, (c) after the reducer re-run compares the preview structurally and checks H0→H1/H2 and the per-stream raw draws, and (d) builds `Next.Rng` from the verified `NextRng` instead of keeping the reference. `CommitRecord` would then carry the changed hashes (already separate fields). The ids must come from `LastEventSequence + 1`; a candidate numbered from a stale sequence is already `event_id_in_use`/`already_committed` here.

## 23. Self-assessment

**`C3A_IMPLEMENTATION_READY_FOR_REVIEW`** — this is Claude Code's own judgement, not an approval; GPT Chat and Codex reviews are `미실시` (a Codex review is recommended: concurrency, save-data-adjacent state and a core simulation boundary). The five specific conditions:

1. No more than one commit succeeds in a CAS race — shown (barrier tests, section 11).
2. A failure or a cancellation changes no official state — shown (unchanged-aggregate assertions after every Stage failure and cancellation; Publish failures leave `Current` the same reference).
3. Finalization is part of the single Publish — shown (section 10).
4. C3-A has no path that confirms a pitch event — shown (the kind is refused at step 2 before anything else with an id; boundary scans).
5. Nothing fails or throws after a successful swap — by structure (nothing follows the swap but the answer) plus the source scan; it cannot be exercised by a runtime hook because none exists after the swap.

## 24. Final Git state

`git status -sb`: branch `main`, HEAD `7456f44fd3ecd687afce210eb5be972114f8479c` (unchanged), equal to `origin/main` (no ahead/behind). Nothing staged. No commit, no push, no amend/rebase/reset.

Modified tracked files (6): `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `docs/decisions/ADR-007.md`, `docs/game-engine.md`, `tests/StarPlayer.Application.Tests/HiddenTendencyBoundaryTests.cs`.
New untracked files: five production files in `src/StarPlayer.Simulation/Baseball/`, the folder `tests/StarPlayer.Simulation.Tests/Authority/` (five files), and the evidence files `docs/reports/PHASE_3C3A_*` (this report, `AUTHORITY_TESTS_raw`, `FULL_XUNIT_raw`, `MUTATION_raw`, `VERIFY_LOG_run1/2`, `VERIFY_STDOUT_run1/2`).
Preserved untracked files (unchanged): `TASK.md`, `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf`.
`git diff --check`: exit 0.

## 25. Finalize replacement hook removed (fix task `TASK.md` "Finalize 훅 제거 및 최종 재검증", 2026-10-13, base HEAD `7456f44`)

Status stays `READY_FOR_REVIEW`; Phase 3 Gate stays `INCOMPLETE`. No staging, commit, push or Codex call.

1. **Risk found.** The internal `CommitGate.Stage(expected, event, checkpoint, Func<GameState?, Result<FinalizeResult>>? finalize, cancellation)` overload let a caller replace `GameReducer.Finalize` (Medium: a second Finalize implementation could decide the final record that is then published).
2. **Decision.** GPT Chat: REMOVE the replacement hook.
3. **Removed.** The `finalize` parameter and the `(finalize ?? GameReducer.Finalize)(next)` call. The internal overload is now `Stage(expected, event, Action<string>? checkpoint, CancellationToken)`; the public `Stage(expected, event, ct)` forwards to it. The only callers of the old five-argument form were tests (the cancellation-at-stage tests now call `(…, hook, cts.Token)`). No interface, service, global or DI point was added.
4. **Before / after.** Before: step 11 called an injectable delegate whose default was the real reducer. After: step 11 calls `GameReducer.Finalize(next)` directly. Unchanged: the result checks (`WasNew`, `Final != null`, same reference, `IsComplete`), `Commit.finalize_failed`, `Commit.final_state_incomplete`, the checkpoint stage names, the revision step and the whole CAS path (`GameCursor.cs` not touched).
5. **Tests removed (2), in `CommitGateStageTests.cs`:** `A_failed_finalize_or_an_incomplete_finalize_answer_stages_nothing` (injected a failing / `WasNew=false` / no-`Final` / wrong-score answer) and `A_finalize_that_is_not_the_reducers_is_never_asked_for_an_event_that_does_not_end_the_game` (injected a throwing delegate). Neither can be reproduced through a public path: with the real reducer `Finalize` never fails or answers incompletely for a `Final` state, and "a non-ending event does not finalize" is already asserted by `Finalized == false` / no `GameCompleted` in the Approach tests. Three cancellation tests lost only their `null` argument. All tests the task lists as must-keep (Final creation, one `GameCompleted`, one-stage Apply+Finalize, one CAS Publish, revision +1, no new RNG, no extra EventId, bad Final StartNew/Restore refused, duplicate refused, cancellation leaves state, CAS race one winner) are unchanged and pass.
6. **Defence branches kept:** `Commit.finalize_failed`, and `Commit.final_state_incomplete` (after Finalize, and for an unfinished in-progress state). With the real reducer the first one and the post-Finalize check are **unreachable** and are now untested defences; no new bypass was created to test them. `Authoritative.final_state_invalid` (Restore) still covers the incomplete-final rule through the public factory path.
7. **Results (actual, final tree).** Authority: **67 run, 67 passed, 0 failed, 0 skipped** (69 minus the 2 removed tests). Full xUnit (`dotnet test StarPlayer.sln`, `PHASE_3C3A_FULL_XUNIT_raw.txt`): **1,580 run, 1,580 passed, 0 failed, 0 skipped** (Domain 263, Application 117, Simulation 687, Infrastructure 513; baseline 1,513 + 67 new).
8. **verify.ps1:** `tools/verify.ps1` run twice on the final tree (logs/stdout `PHASE_3C3A_VERIFY_*_run1/2` overwritten): **`GATE: PASS` both times** (exit 0; Simulation 687, Domain 263, Application 117, Infrastructure 513, 0 failed/skipped; relative markdown links 124 files OK). Claude Code executed both.
9. **Fault injection after the change** (`PHASE_3C3A_MUTATION_raw.txt`, "Round 3"): only mutants touching Finalize or the final-state rule were re-run against the 67 tests: M4 (Finalize skipped) FAIL 3, M7a (score not compared) FAIL 1, M7b (GameId not compared) FAIL 1, M19 (`GameCompleted` twice) FAIL 1, new M26 (Finalize result discarded) FAIL 1 – all detected; **M25 (finalize completeness check removed) now survives (67 passed)**: its only detector was a removed injection test and the branch is unreachable with the real reducer. The other **21** original mutants (ledger in section 15) do not involve Finalize and were not re-run; their detection is the first-delivery result. The earlier "26/26" is not carried over: 25 of the original 26 are detected on record (21 from the prior tree + 4 re-run), 1 survivor (M25, an unreachable defence), plus 1 new mutant (M26, not one of the original 26) detected.
10. **Remaining risks.** The unreachable defence branches above are untested; section 20 is otherwise unchanged. No replacement or bypass path for Finalize remains: `CommitGate.cs` has no `finalize` parameter, and the only internal `Stage` overload takes an observer `Action<string>` that cannot change state.

## 26. Technical review record and record clean-up (2026-10-13, base HEAD `7456f44`)

- **Final technical review status: `GPT_CHAT_TECHNICAL_PASS_WITH_NOTES`** — GPT Chat's verdict as relayed by the owner in `TASK.md` "C3-A 기술 승인 후 기록 정리": the C3-A production implementation is technically approved with notes. Claude Code did not produce this verdict; a Codex review remains `미실시`. This is a technical approval of C3-A only: C3-B, C3-C and Phase 4 stay `NOT STARTED / NOT AUTHORIZED` and the Phase 3 Gate stays `INCOMPLETE`.
- **Notes carried forward:** (a) M25 / unreachable defence branches have no test (sections 19, 25 and the C3-C check items in section 22); (b) the 21 mutants not re-run after the hook removal; (c) the Finalize replacement hook removal and the deletion of its two tests stay as recorded in section 25 (reasons unchanged).
- **This clean-up changed documents only**: no source, no test, no new Finalize hook. xUnit (1,580) and `verify.ps1` (2 x PASS) were last run before this clean-up and were not repeated because only Markdown changed; `git diff --check` and the relative-link check were re-run after the last edit (section 24 / the task report).

## 27. Commit whitespace-check exception (2026-10-13, GPT Chat decision relayed by the owner)

- `git diff --cached --check` on the 24 staged files: **FAIL** (it did not pass; this report does not claim a full-check PASS).
- Cause: only the four automatically generated verification evidence files `PHASE_3C3A_VERIFY_LOG_run1.md`, `PHASE_3C3A_VERIFY_LOG_run2.md`, `PHASE_3C3A_VERIFY_STDOUT_run1.txt`, `PHASE_3C3A_VERIFY_STDOUT_run2.txt`.
  - The two `VERIFY_LOG` files: CRLF line endings written by `tools/verify.ps1` (37 lines each reported as trailing whitespace; earlier committed `PHASE_*_VERIFY_LOG*.md` files have the same form).
  - The two `VERIFY_STDOUT` files: real trailing spaces in the original tool output at lines 5 and 13 of each file.
- With `git -c core.whitespace=cr-at-eol diff --cached --check` (temporary, no configuration changed) the CRLF reports disappear and only the four real trailing-space lines (STDOUT lines 5 and 13) remain.
- Exception approved for exactly these four files, to keep the original verification evidence byte-for-byte; the files were not normalized (SHA-256 identical before and after).
- The C# sources, the tests, the other documents and the remaining evidence files (20 of the 24 staged files) have no whitespace error. No code or test behaviour changed by this exception.
