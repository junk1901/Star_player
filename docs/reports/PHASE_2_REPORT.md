# Phase 2 Report — Baseball state transitions and scoring

> **Twelve rounds are recorded here.** Part A11 is the final approval record (current state). Part A10 records the Codex final `PASS WITH NOTES` and the two closing notes. Part A9 is the C44 multi-error fix and C29 ADR note. Part A8 is the C43 final completion. Part A7 is the hit-with-force-out High fix. Part A6 is the C33 High fix (ambiguous fielder's choice refused). Part A5 is the C33 final recording-responsibility round of 2026-10-11. Part A4 is the OBR difference fix round of 2026-10-11. Part A3 is the R02 / OBR original-text round of the same day. Part A2 is the earlier final round of that day. Part A is the first Codex-review remediation of 2026-10-10. Part B is the
> first-round report as submitted before the independent Codex review returned `CHANGES REQUIRED`; Parts A and B are preserved except where a later part replaces a statement (marked "superseded").

Status: **`PASS WITH NOTES` / approved (`승인 완료`, 2026-10-11)** - see Part A11. The older status words in the parts below (`검토 대기`, "not approved", Gate "INCOMPLETE / not declared passed") are the history of each round and are superseded. Phase 3 has not been started.
Automatic verification (`tools/verify.ps1`) is a separate fact from the Gate: it is PASS, the Gate is not declared passed. Nothing is committed or pushed
(base commit `64167f788dc6a77fe4725bb13e06d946fd122ada`, working tree dirty). Phase 3 has **not** been started.

## Part A11 — Final approval record (2026-10-11): `PASS WITH NOTES` / approved

**A11.1 Decision.** Phase 2 is recorded as **`PASS WITH NOTES` / approved (`승인 완료`)** on 2026-10-11. Basis, as relayed by the user from the final GPT Chat review: xUnit 598 passing (Domain 145, Simulation 260, Application 114, Infrastructure 79); scripted games 130/130 (100 + 30 C33); box-score proofs 200/200; `tools/verify.ps1` two consecutive `GATE: PASS` ([run 1](PHASE_2_VERIFY_LOG_c44multi_run1.md), [run 2](PHASE_2_VERIFY_LOG_c44multi.md)); Codex final `PASS WITH NOTES` (Part A10); the user's manual Windows Export UI verification completed (user-reported; no detail was recorded here); existing NOTES and the approved GameRules policy unchanged. The approval comes from the recorded GPT Chat/user review; Claude Code did not approve it. Statements above that say `검토 대기`, "not approved", "INCOMPLETE" or "not declared passed" describe the status at that round and are superseded by this record; no earlier test result or Codex review entry was changed.

**A11.2 Follow-up NOTES (kept).** (1) **C33** - recorded limits of the run-responsibility slot ledger (OBR 9.16(g) example 7 equivalent; the ambiguous multi-out fielder's choice stays refused as an approved support limit). (2) **C38** - sacrifice-fly variants (9.08(d)) unsupported, Gate non-required carry-over, re-evaluate in Phase 3. (3) **C42** - `ReachedOnError` direct cause `NOT_MODELLED`. (4) **C44** - a hit with a runner on first and an unexplained error, or two or more errors, is refused: an input-model support limit approved by GPT Chat's design judgement (no direct owner approval recorded), not a KBO/OBR rule or certification. (5) **D2 policy** - the operating `GameRules` subset is not certified against the KBO rule book and scoring rules (only the 11-inning limit and the R02 operating basis are confirmed); replace it with an evidence-based profile before career use. (6) **Phase 4** - an engine rule version (the profile hash does not reveal rule-behaviour changes) and persistence of `game_states`, `scored_plays` and the rule-profile hash. FHD has no reported check and remains unverified.

**A11.3 Scope.** Documentation only: no code, test or GameRules change. Phase 3 is **not started**. Nothing is committed or pushed; that needs separate permission.

## Part A10 — Codex final review `PASS WITH NOTES` and closing notes (2026-10-11)

**A10.1 Result.** The independent Codex final review of the C44 multi-error fix ended **`PASS WITH NOTES`** (result relayed by the user in `TASK.md`; this report records it and did not run or invent it). Functional defects: **0**. No further code review is planned. Two notes were handled:

1. *Stale comment* at `GameReducer.Finish.cs` line 41 (it still said "every error explained by an error-caused move, nothing is doubted"). The comment now states the actual C44 policy: an error-caused move is not proof that every error is explained; a hit with a runner on first and an unmarked error, or two or more errors, is refused as cause-unclear; one error with a marking move is the scorer's statement, not a verified link. Comment only; no code, rule or test changed.
2. *`.gitignore` `*.log` hid the RED evidence from Git.* The repository-wide `.log` ignore is **kept**. The five original RED logs stay unchanged in the local `docs/reports/` folder. A tracked summary, [PHASE_2_RED_EVIDENCE.md](PHASE_2_RED_EVIDENCE.md), copies from each log its line count, SHA-256, the failing test names, the summary line and the failure messages, plus the C44 GREEN run; the report, review, ADR-003, game-engine, CURRENT_STATE and evidence-file references now point to it. Links were checked to resolve.

**A10.2 Status.** No test result and no GameRules policy changed. The Phase 2 Gate stays **INCOMPLETE** and the review status stays **`검토 대기`** until the user's manual UI verification; nothing is committed or pushed, Phase 3 is not started.

## Part A9 — C44 multi-error gap (2026-10-11, Codex focused review `CHANGES REQUIRED`, Medium; `TASK.md`) and C29 ADR history

**A9.1 Finding.** `FinishPlay` treated every recorded error as explained as soon as *one* move carried an error cause. The event lists the error positions and gives moves a cause, but nothing links an error to the move it caused, so with two errors one `ErrorExtraBase` move says nothing about whether the other error prevented a force out.

**A9.2 Fix** (`GameReducer.Finish.cs`, no new input model, no engine redesign, no reclassification). A `Single`/`Double`/`Triple` with a runner on first at the start of the play is refused with the existing `Game.hit_error_cause_unclear` when errors are recorded and **(a)** no move carries an error cause, or **(b)** two or more errors are recorded - whatever number of error-caused moves exist. Checked in `FinishPlay` before any state, statistic, responsibility slot or receipt. *Order and codes:* `Game.hit_with_force_out` -> `Game.hit_with_force_out_saved_by_error` (confirmed C43) -> `Game.hit_error_cause_unclear` (C44). One error with one matching error move still keeps its hit; this is the scorer's statement of what that error did, **not** a verified cause-by-cause link, so documents no longer say "every error is explained".

**A9.3 Nature of the refusal.** A play with several errors can be legal in baseball. Refusing it is a **game support limit caused by the input model** (evidence row C44, `NOT_MODELLED`), not a KBO/OBR rule and not a certification. Approval record: C44 is an approved Phase 2 support limit by **GPT Chat's design judgement** relayed by the user; no separate direct owner approval is recorded and Claude Code did not approve it. The multi-error extension is the Codex Medium fix under that limit; no new GameRules were approved.

**A9.4 RED -> GREEN** (`HitMultiErrorTests`, 8 cases with hand-written expectations; RED log [PHASE_2_RED_EVIDENCE.md (원본 `PHASE_2_C44_MULTI_ERROR_RED.log` 로컬 보존)](PHASE_2_RED_EVIDENCE.md): 8 cases, 5 failed with "the multi-error hit was accepted", before the fix):

| Case | Input | Before | After |
|---|---|---|---|
| A | runner on first, Single, 2 errors, 1 `ErrorExtraBase` | accepted (RED) | `Game.hit_error_cause_unclear` |
| B | runner on first, Double and Triple, 2 errors, 1 `ErrorExtraBase` | accepted (RED, 2 cases) | `Game.hit_error_cause_unclear` |
| C | runner on first, 2 errors, 2 error moves, no link | accepted (RED) | `Game.hit_error_cause_unclear` |
| D | runner on first, 1 error, matching `ErrorExtraBase` | accepted | accepted (unchanged) |
| E | no runner on first (empty / runner on second), several errors; no error at all | accepted | accepted (unchanged) |
| F | state after refusal | - | same `GameState` instance, no hit/AB/error/out/run, bases and responsibility slots untouched, no receipt |
| G | same event delivered twice | - | identical error both times; accepted event is a harmless duplicate |
| H | JSON round trip (`HitErrorReplayTests`) | - | accepted game keeps fingerprints, receipts and replay; the multi-error event survives JSON, keeps its fingerprint and is refused after the round trip; no receipt |
| priority | `ErrorSavedOut` on a forced runner plus 2 errors | - | `Game.hit_with_force_out_saved_by_error` (C43 first) |

No existing test expectation was changed. **Not expressible and therefore not tested:** a normal hit followed by an extra base of the batter-runner (the batter's own extra base is not a move of the event model; example 7 limit, unchanged).

**A9.5 C29 ADR history.** ADR-003 item 25 is kept word for word; a "Superseded in part" note now states that "C29, awaiting owner confirmation" described that moment and that the current policy is: no automatic DH placement, an explicit Slot for a legal substitution, unsupported substitutions refused with an error. It points to item 30 and the C29 row (`MATCH`) of the evidence file.

**A9.6 Verification.** xUnit **598** (Domain 145, Simulation 260, Application 114, Infrastructure 79; previous 590 = +8 new Simulation cases; the replay test gained assertions, no new test). Explicit re-runs: C41/C43/C44 tests, C33 ambiguous fielder's choice, C14/C19 (`BoxProofTests`: 200 team games), the 100 games and the 30 C33 games (`ScriptedGameSuiteTests`, `ReplayContractTests`, `ResponsibilityScriptedGamesTests`), event receipts and JSON replay, Phase 0-1 suites: all pass. `tools/verify.ps1`: the first two attempts were **`GATE: FAIL`** (exit 1; Infrastructure `RuleEvidenceTests` failed because the new evidence field `fixed_on` on the `NOT_MODELLED` row C44 is reserved for fixed `MATCH` rows; fields renamed `limit_extended_on` / `before_extension`; logs kept: [try 1](PHASE_2_VERIFY_LOG_c44multi_try1_FAIL_evidence_key.md), [try 2](PHASE_2_VERIFY_LOG_c44multi_try2_FAIL_evidence_key.md)). After the fix two consecutive runs: both **`GATE: PASS`, exit code 0** ([run 1](PHASE_2_VERIFY_LOG_c44multi_run1.md), [run 2](PHASE_2_VERIFY_LOG_c44multi.md)): Release build 0 warnings, Godot headless build, SQLite 8/8, 6 game diagnostics, fresh Windows export and cold-copy runs at QHD with screenshots, relative links (50 and 51 files). Not run: manual UI click-through, FHD.

**A9.7 Remaining support limits and Gate self-assessment.** Unchanged: C44 (now including 2+ errors with a runner on first), the ambiguous multi-out fielder's choice, example 7 and batter-runner interference outs, C42, C38 carry-over, other hit-denial rules (9.05(a)(6), (b)(2)-(5)), no KBO 2026 rule-book certification. Gate-required implementation defects: none open; open OBR differences: none; the Phase 2 Gate stays **INCOMPLETE / not declared passed**, the review status stays `검토 대기`, and nothing is committed. GPT Chat review is requested; Phase 3 is not started.

## Part A8 — C43 final completion (2026-10-11, `TASK.md`): errors that prevent a force out

Instruction: `TASK.md` (C43 final completion), minimal record-integrity fix. Status stays **`검토 대기`**; nothing is committed or pushed; Phase 3 is not started. This part supersedes the "C43 open difference" statements of A7.4 and A7.7.

**A8.1 Counter-example before and after.** Reproduced first: a `Single` with the runner on first moved to second as `ErrorSavedOut` (the scorer's statement that the error saved him from an out) with a recorded shortstop error was accepted and credited a hit and an at-bat (the same input as a plain forced advance with an unexplained error was accepted too). The failing run is preserved: [PHASE_2_RED_EVIDENCE.md (원본 `PHASE_2_C43_RED.log` 로컬 보존)](PHASE_2_RED_EVIDENCE.md) (6 of the new tests failed, each because no error was returned; the over-rejection guards passed on the old engine). After: `Game.hit_with_force_out_saved_by_error` / `Game.hit_error_cause_unclear` before any state change; the state instance is unchanged; no hit, at-bat, error credit, slot, score or receipt; a re-delivered event gets the same error; no result is relabelled.
Rule: 2026 OBR 9.05(b)(1) second clause ("or would have been forced out except for a fielding error"), through the KBO-first / OBR-fallback policy (not a KBO certification).

**A8.2 Exact rejection conditions** (`FinishPlay`, after plan validation and order resolution; `docs/game-engine.md` section 16). For `Single`, `Double`, `Triple`: (1) a move with cause `ErrorSavedOut` whose would-be out is a state-derived force out (`OutClassification.Force`; a force removed by an earlier real out makes it `NonForce` and the hit stands) -> `Game.hit_with_force_out_saved_by_error`; (2) a runner on first, at least one recorded error and no move at all with an error cause (`ErrorExtraBase` / `ErrorSavedOut`) -> `Game.hit_error_cause_unclear`. Causality is verifiable only through the scorer's explicit `ErrorSavedOut` cause plus the derived force status; where the input says nothing about what an error did and a forced runner exists, the correct hit ruling cannot be determined and the play is refused as unsupported instead of being credited or guessed.

**A8.3 Effect on legal plays.** Unchanged and tested: a single with no runner on first whatever error is recorded (empty bases, runner on second only); a single with no error; a single whose error is explained by an error-caused move (a runner scores on the error while the first-base runner is forced up normally); a runner saved by an error from a non-force out (first base empty); a force removed by an earlier real out (hit stands) versus the same two moves in the force-keeping order (refused); fielder's choice, reach on error, outs, walks. **Cases the input cannot tell apart and that are now refused (legal plays that are restricted):** a hit with a runner on first plus an error whose effect the model cannot express (for example a batter's own extra base on a throw) and that no move explains.

**A8.4 Approval status of the new limit.** Rule (1) is the OBR text under the approved policy (evidence row C43 -> `MATCH`). **Rule (2) is the approved Phase 2 support limit C44** (evidence row C44, `NOT_MODELLED`, `support_limits_approved_by_gpt_chat_design_judgement`). *Approval record (2026-10-11):* approved by **GPT Chat's design judgement**, relayed by the user in `TASK.md`; no separate direct owner approval is recorded and Claude Code did not approve it. *Decision:* an unexplained error on a hit with a runner on first is refused explicitly; `ErrorSavedOut` plus a verified force out stays refused by the existing C43 check; supported events such as `ErrorExtraBase` on a normal hit are kept; not every hit with an error is rejected; an error of unconfirmed cause is never reclassified as another event. It is not KBO official certification and not a complete OBR implementation. The actual refusal conditions, the boundary with accepted plays and the related tests (A8.5) are unchanged by this documentation-only decision. A per-error cause input would remove it and belongs to the Phase 3 batted-ball and fielding model. C42 (`ReachedOnError` direct cause) stays `NOT_MODELLED`: valid reaches on error are neither certified nor refused, and the confirmed C43 defect was not left behind because of it.

**A8.5 Tests.** `HitErrorForceTests` (11 cases, hand-written expectations) and `HitErrorReplayTests` (Infrastructure: JSON event round trip of a game containing the accepted explained single, identical fingerprints, receipts and replay; the two refused combinations leave no receipt). The C41 hit/force-out tests, the C33 slot and ambiguous-fielder's-choice tests remain unchanged and pass.

**A8.6 Regression.** xUnit **590 passing, 0 failed, 0 skipped** (Domain 145, Simulation 252, Application 114, Infrastructure 79; previous 578): C41, C43, C33 slots and the ambiguous refusal, C14 earned runs, C19 left on base / box proof 200/200, receipts and JSON replay, the 100 games and the 30 C33 games (the generators never emit the refused combinations; no generated file changed), Phase 0 and 1 regressions.

**A8.7 Gate verification.** `tools/verify.ps1` twice in a row on the final code: both **`GATE: PASS`, exit code 0** ([run 1](PHASE_2_VERIFY_LOG_c43_run1.md), [run 2](PHASE_2_VERIFY_LOG_c43.md)). Each: Release build 0 warnings, Godot headless build, SQLite 8/8, game diagnostics 6 games, fresh Windows export and cold-copy runs at QHD with screenshots (E2-E4), relative documentation links (46 and 47 files).

**A8.8 Remaining limits and self-assessment.** Support limits: C44 (approved Phase 2 support limit, GPT Chat design judgement 2026-10-11; not an official certification), the ambiguous multi-out fielder's choice (owner-approved earlier), example 7 and batter-runner interference outs, C42, other hit-denial rules needing judgement (9.05(a)(6), (b)(2)-(5)), profile hash not showing rule-behaviour changes (Phase 4 needs an engine rule version), shared author of Python scorer and engine. Not done: Codex re-check of the last fixes, human click-through, FHD, KBO 2026 rule-book certification. Known Gate-required implementation defects: **none open**; open OBR differences: **none**; the Phase 2 Gate is still **INCOMPLETE / not declared passed** and the review status is `검토 대기`. Final review is possible; an independent Codex review of the C44 documentation can be requested. The Phase 2 final approval status is unchanged.

## Part A7 — Hit with a force out (2026-10-11, Codex final focused review `CHANGES REQUIRED`, High): OBR 9.05(b)(1)

Instruction: `TASK.md` (hit/force-out consistency). Minimal fix; no engine redesign, no Phase 3, no commit or push; status stays **`검토 대기`**.

**A7.1 Defect, before and after.** Before: `Single` was accepted together with the force out of a preceding runner on the same batted ball, so the play that part A6 refuses as a fielder's choice (Peter's runner on second, Roger's on first, Sierra pitching, first-base runner forced out at second, second-base runner tagged out at home) was recorded as a single with a hit and an at-bat when relabelled: the C33 refusal was bypassed by the result label. After: `Game.hit_with_force_out`; the state instance is returned unchanged; no hit, at-bat, out, run, slot or receipt; the same event gives the same error; the engine never turns a refused hit into a fielder's choice or a reach on error.
Rule: 2026 OBR 9.05(b)(1) ("shall not credit a base hit when a runner is forced out by a batted ball, or would have been forced out except for a fielding error"; `docs/2026-official-baseball-rules.pdf`, p.116-117), applied through the approved KBO-first / 2026-OBR-fallback policy. **Not a KBO certification.**

**A7.2 Where the cross-check is.** `GameReducer.Finish.cs`, `FinishPlay`, directly after `ResolveOrder` succeeded (so after plan validation, third-out and order checks) and before the C33 slot hand-over, the scoring of runs and any statistic or receipt: a result of `Single`, `Double`, `Triple` or `HomeRun` with an out whose `Resolution.Class` is `OutClassification.Force`. The classification is the one derived from the state and the verified order, not the result label and not the way the out was made: a tag on a forced runner at the base he was forced to is a force out; a force removed by an earlier out of the runner behind is respected (the later out is a tag out); a batter put out after a safe hit is not a force out.
Case B: the same play entered as a fielder's choice keeps `Game.ambiguous_responsibility`.

**A7.3 New independent tests (`HitWithForceOutTests`, 10 cases incl. a 3-case theory, hand-written; 6 failed on the unchanged engine with "no error returned", [PHASE_2_RED_EVIDENCE.md (원본 `PHASE_2_HIT_FORCE_RED.log` 로컬 보존)](PHASE_2_RED_EVIDENCE.md)).** A: the Codex reproduction as a single (error, same state instance, no hit or at-bat, slots unchanged, same error twice, no receipt); single, double and triple with a forced runner; B: fielder's choice route still `Game.ambiguous_responsibility`; C: a forced runner put out by a tag; D: force removed by an earlier out (the same two outs in the order that keeps the force are refused, in the order that removes it the hit stands and the out is classified `NonForce`);
E: legal hits keep hit, at-bat, RBI and a runner or batter thrown out going for more; F: an accepted hit is a harmless duplicate. Existing tests were not changed.

**A7.4 `ReachedOnError` and the second clause (documented, not extended).** `ReachedOnError` checks that an error is recorded, not that it directly caused the batter to reach: it is neither certified legal nor refused wholesale (reproduced: a reach on error with a runner forced out on the same ball is accepted); evidence row **C42 `NOT_MODELLED`**, `docs/game-engine.md` section 15. The "enter it as another result" hint of the C33 error message and of section 14 now says it is valid only for the official classification of the real play. **Further reproduced difference, reported only:** 9.05(b)(1) second clause - a single that carries a runner saved from a force out by an error (a runner on first moved to second as `ErrorSavedOut`) is still credited as a hit; evidence row **C43 `OPEN_DEVIATION`** (issue, reason, recommended option, alternative, impact and Gate effect recorded; Gate relevance is for the owner, nothing was approved or simplified). Impact: hit counts in a rare error-saved-force play; runs, RBI, outs and earned-run reconstruction are unaffected.

**A7.5 Regression.** xUnit **578 passing, 0 failed, 0 skipped** (Domain 145, Simulation 241, Application 114, Infrastructure 78; previous 568). OBR 9.16(g) examples 1-6 and the example 7 equivalent, the ambiguous fielder's-choice refusal, third-out and force-removal logic, hit followed by a batter-runner out, C14 earned runs, C19 left on base / box proof 200/200, fingerprints, receipts and JSON replay, the 100 games and the 30 C33 games all pass unchanged (the generators never emit a hit with a force out and were not changed).

**A7.6 Gate verification.** `tools/verify.ps1` twice in a row on the final code: both **`GATE: PASS`, exit code 0** ([run 1](PHASE_2_VERIFY_LOG_hit_force_run1.md), [run 2](PHASE_2_VERIFY_LOG_hit_force.md)). Each: Release build 0 warnings, Godot headless build, SQLite 8/8, game diagnostics 6 games, fresh Windows export and cold-copy runs at QHD with screenshots (E2-E4), relative documentation links (44 and 45 files).

**A7.7 Remaining risks and self-assessment.** C43 (open difference, Gate relevance undecided), C42 (reach-on-error cause not decidable), the ambiguous multi-out fielder's choice support limit (A6), example 7 and batter-runner interference outs, other hit-denial rules that need judgement (9.05(a)(6), (b)(2)-(5)), profile hash not showing rule-behaviour changes (Phase 4 needs an engine rule version), shared author of Python scorer and engine, no further Codex re-check, no click-through, no FHD, no KBO 2026 rule-book certification. The Phase 2 Gate is **INCOMPLETE / not declared passed** (one reproduced open difference exists and is not self-approved); review status `검토 대기`.

## Part A6 — C33 last High fix (2026-10-11, Codex focused re-review `CHANGES REQUIRED`): ambiguous fielder's choice refused

Instruction: the C33 slot ledger and OBR examples 1-6 were confirmed correct; the remaining High defect was an arbitrary slot choice when one fielder's choice puts out runners of different pitchers. Scope: refusal only; no new baseball rule, no engine redesign. Status stays **`검토 대기`**; nothing is committed or pushed; Phase 3 is not started. This part supersedes the "oldest slot" statements of A5.9 and A5.10.

**A6.1 How it is fixed.** `HandOverFieldersChoiceSlot` (`GameReducer.Responsibility.cs`) now returns a result and is called in `FinishPlay` after plan validation and order resolution succeeded and before any outs, runs, RBI, statistics, slots or receipt are produced (nothing is produced and cancelled afterwards). Refusal condition: result `FieldersChoice`, batter reaches, two or more runners put out, and the slots of those runners belong to more than one pitcher -> `Game.ambiguous_responsibility`. Same-pitcher multiple outs, a single out (examples 2, 3, 5, 6) and every non-fielder's-choice result are unchanged. The input cannot say which out was the fielder's choice (slot passes on) and which an independent out on a runner's own advance (slot disappears); order of the outs does not identify it.
It is recorded as a **Phase 2 support limit by owner decision**, not as an OBR or KBO rule (`docs/game-engine.md` section 14, ADR-003 item 34, evidence row C33 `unsupported_scope`). C38, appeal exclusion and KBO-first / OBR-fallback are unchanged.

**A6.2 Before and after (Codex reproduction).** Peter's runner on second, Roger's on first, Sierra pitching, batter reaches on a fielder's choice, first-base runner forced out at second, second-base runner tagged out at home going for more. Before: the event was **accepted** and the batter received an arbitrarily chosen slot (3 new tests failed with "no error returned", [PHASE_2_RED_EVIDENCE.md (원본 `PHASE_2_C33_AMBIGUOUS_RED.log` 로컬 보존)](PHASE_2_RED_EVIDENCE.md)). After: `Game.ambiguous_responsibility`, state instance identical, outs 0, runs 0, no slot handed to the batter (Roger and Peter slots unchanged on first and second), no receipt, the same error on re-delivery.

**A6.3 New independent tests (hand-written, `ResponsibilityLedgerTests`; 7 new, total 22).**
- A: the Codex reproduction (error, same state, slots unchanged, no receipt, same error twice).
- B: both orders of the two outs (explicit `Sequence` both ways) are refused alike.
- Replacement: after the refusal the scorer enters the play as a single with a runner thrown out; the game continues with the old charges.
- Same-pitcher: two runners of one pitcher put out on a fielder's choice are accepted and that pitcher's slot is charged first.
- E (bypass): the same physical play entered as a single or a reach on error is a different classification, not a fielder's choice; both slots disappear (documented, tested). No other path passes a slot to a batter (section 14).
- D: pickoff and a runner thrown out going for more still end the charge as before.
- F: an accepted fielder's choice delivered again is a harmless duplicate; a refused one never becomes one.
The 8 pre-existing tests of this file that passed before are untouched; the replay and JSON round trip of normal events (100 games, 30 C33 games, fingerprints, receipts) pass unchanged.

**A6.4 Regression.** xUnit **568 passing, 0 failed, 0 skipped** (Domain 145, Simulation 231, Application 114, Infrastructure 78; previous 561): OBR 9.16(g) examples 1-6 as printed, the example 7 equivalent, the 30 C33 games and 100 games against the Python scorer (the generator never emits an ambiguous play and was not changed; the files regenerate byte-identical), box proof 200/200, C14 earned runs and C19 left on base, 9.17(d) loss test, fingerprint and JSON replay.

**A6.5 Gate verification.** `tools/verify.ps1` twice in a row on the final code: both **`GATE: PASS`, exit code 0** ([run 1](PHASE_2_VERIFY_LOG_c33_amb_run1.md), [run 2](PHASE_2_VERIFY_LOG_c33_amb.md)). Each: Release build 0 warnings, Godot headless build, SQLite diagnostics 8/8, game diagnostics 6 games, fresh Windows export and cold-copy runs at QHD with screenshots (E2-E4), relative documentation links (42 and 43 files). Earlier logs are preserved.

**A6.6 Remaining technical risks.** Plays of this kind cannot be recorded as a fielder's choice until the event model can name the out that is the fielder's choice (an input extension, not part of Phase 2); example 7's batter advance on the throw and batter-runner interference outs are still outside the model; run charging changed for an existing rules profile without a profile hash change (Phase 4 must store an engine rule version); the Python scorer shares its author with the engine; no Codex re-review after this fix, no human click-through, no FHD, no KBO 2026 rule-book certification (D2).

**A6.7 Gate self-assessment.** Known Gate-required implementation defects: **none open**; rule-source blockers: none; every executed check passes. The Phase 2 Gate is still **INCOMPLETE / not declared passed**; review status `검토 대기`. Approval is possible if GPT Chat accepts the refusal limit, the C38 carry-over and the uncertified D2 baseline; a Codex re-check of this fix is recommended. Nothing was approved by this work.

## Part A5 — C33 final recording responsibility (2026-10-11, `TASK.md`): C33 fixed, C38 carried over

Instruction: GPT Chat review result (`TASK.md`): C33 must be fixed before final approval; C38 is a Gate non-required carry-over. Scope: run responsibility between pitchers only. Status stays **`검토 대기`**; nothing is committed or pushed; Phase 3 is not started.

**A5.1 Before and after.** Before: `BaseRunner.ResponsiblePitcher` was the runner's own pitcher and the run was charged to it, so OBR 9.16(g) Comment examples 2, 3, 5 and 6 charged the reliever (e.g. example 5: Peter 0 / Roger 0 / Sierra 3 instead of 1 / 1 / 1; example 6: Peter 0 / Roger 2 instead of 1 / 1) and, through 9.17(d), the loser could be the wrong pitcher. After: the field is a responsibility *slot* and `RunResponsibility` / `GameReducer.Responsibility.cs` charges runs by the ledger rules of `docs/game-engine.md` section 4b (a fielder's-choice batter takes the slot of the runner put out; other outs remove the slot; each run takes the oldest slot among scorers and runners left on base). No new event or persisted field; event payloads, fingerprints and receipts are unchanged.

**A5.2 OBR 9.16(g) Comment examples 1-7 (`ResponsibilityLedgerTests`, expectations from the rule text, written before the fix).**

| Example | Expected (OBR) | Old engine | New engine |
|---|---|---|---|
| 1 | Abel's run: Peter | pass | pass |
| 2 | forced-out Abel, Baker scores: Peter | **fail** (Roger) | pass |
| 3 | Abel out at home on a fielder's choice, Baker scores: Peter | **fail** | pass |
| 4 | Abel picked off, Baker scores: Roger | pass | pass |
| 5 | one run each to Peter, Roger, Sierra | **fail** (0/0/3) | pass |
| 6 | one run to Peter, one to Roger | **fail** (0/2) | pass |
| 7 | Abel out going for an extra base, Baker scores: Roger | pass (equivalent) | pass (equivalent) |

Example 7 is **not expressible as printed**: Baker "advances to second on the throw" on his single, and a batter's own extra-base advance is not an event of the Phase 2 model (batter moves are limited to reach-on-error and out-after-hit). The test moves Baker to second by a stolen base before the scoring single; the responsibility structure is identical. Reported, not skipped; **no Gate effect**; supporting that advance would be an input-model extension and is not done here. Also unsupported as before: a batter-runner called out for interference before first base (the "other" out kind is refused).

**A5.3 How R and ER are computed.** R: one `PitchR` per run, to the pitcher whose slot the run took (`ScoredRun.ResponsiblePitcher`); the pitchers' R of a team always add up to the opponent's credited runs and a runner key is never charged twice (asserted after every event of the 30-game set and in each hand test). ER: unchanged C14 machinery, `EarnedRuns.Reconstruct(half, limit, startIndex)` from the *charged* pitcher's entry; `TeamER` unchanged. Test `Runs_charged_by_the_ledger_get_their_earned_verdict_from_each_pitchers_own_reconstruction`: Peter 1 R / 1 ER, Roger 2 R / 1 ER (one unearned run after an error), team 2 ER. C34 (the 9.16(h) walk charge) is unchanged: the walked runner's slot goes to the previous pitcher and then moves by the same rules (`When_the_relief_pitcher_walks_the_first_batter_on_a_decided_count_...`).

**A5.4 Loser and records.** 9.17(d): the loser is the pitcher charged with the lead-taking run (`BoxScoreBuilder.Decisions`, unchanged). `The_loss_goes_to_Peter_when_the_go_ahead_run_is_the_one_Peters_charge_was_carried_to_by_a_fielders_choice`: a 1-0 game whose only run is scored by Baker (Roger's pitching change in between) gives the loss to Peter (old engine: Roger), the win to the starter, R 1/0. Existing decision tests are unchanged and pass.

**A5.5 Tests.** Red first: 17 new hand tests, **8 failed on the unchanged engine** ([PHASE_2_RED_EVIDENCE.md (원본 `PHASE_2_C33_RED.log` 로컬 보존)](PHASE_2_RED_EVIDENCE.md)); after the fix all pass. Added: boundaries (caught stealing, pickoff of the batter who inherited a slot, fielder's choice with nobody out, pinch runner), a rejected event leaving state untouched (`Game.out_kind_mismatch`, `Game.out_credit_invalid`, same state instance), deterministic replay, and in `ResponsibilityScriptedGamesTests` the JSON round trip of all events with identical fingerprints and identical charges. The engine outputs are never used as expected values.

**A5.6 100 games and the 30-game C33 set.** The Python scorer got the same clauses (written from `docs/game-engine.md`); the 100 games / 30,974 events regenerate **byte-identical**, because they contain **no run charged through a moved slot** (measured: 8,266 charges, 31 fielder's-choice outs, 0 differences) — so the 100/100 match proves "no regression" and nothing more for C33. A second set of 30 games (`tests/Fixtures/phase2_c33_responsibility_games_30.json`, plus the sidecar `.meta.json`) was therefore drawn by the generator: many pitching changes with runners on, fielder's choices that retire the lead runner, and only candidates with at least one moved-slot run (33 runs). The engine matches the independent scorer on every number (score, line, hits, LOB, per-player stats incl. R/ER, decisions, appearances, batteries). **Negative control:** with the ledger switched off the same comparison fails with 99 mismatches (then restored). The Python scorer shares its author with the engine; the hand oracles are the independent check.

**A5.7 Full regression and Gate verification.** xUnit **561 passing, 0 failed, 0 skipped** (Domain 145, Simulation 224, Application 114, Infrastructure 78; previous 540). Box proof exact in 200/200 team-games (`BoxProofTests`). `tools/verify.ps1` was run twice in a row on the final code: both **`GATE: PASS`, exit code 0** ([run 1](PHASE_2_VERIFY_LOG_c33_run1.md), [run 2](PHASE_2_VERIFY_LOG_c33.md)). Each: Release build 0 warnings, Godot headless build, SQLite diagnostics 8/8, player diagnostics, game diagnostics 6 games, fresh Windows export, cold-copy export runs at QHD (E2-E4 with screenshots), relative documentation links resolve (40 files in run 1, 41 in run 2; the 41st is the run-1 log). The earlier logs are preserved.

**A5.8 C38.** Owner-approved Gate non-required carry-over, recorded in `content/rules/game_rules_evidence.json` (`APPROVED_SIMPLIFICATION`, `gate_required = false`), `docs/game-engine.md` section 6 and ADR-003 item 33: the basic sacrifice fly (caught fly ball, one out on the play, fewer than two outs before, a clean run) is kept; line drives, dropped flies (errors), combined outs and the outfielder-handled test are **unsupported** and are not claimed as implemented; they are to be re-evaluated in Phase 3 with the batted-ball and fielding event model. No sacrifice-fly system was added.

**A5.9 Unresolved and risks.**
- Example 7's batter advance on the throw (above) and batter-runner interference outs are outside the model; a fielder's choice that retires two runners gave the batter the oldest slot (superseded by A6: now refused).
- Engine behaviour for an existing rules profile changed while the profile `ContentHash` (rule parameters only) did not; no game is persisted before Phase 4, which must store an engine rule version.
- The earned verdict of a run charged through a moved slot follows the scoring runner's identity in the charged pitcher's reconstruction (documented, section 4b).
- Not done: Codex re-review of the scoring rules (recommended for C12, C14, C19, C33), human click-through, FHD, KBO 2026 rule book / record-rule certification (D2). The Python scorer is not independent of the engine's author.

**A5.10 Self-assessment.** Known Gate-required implementation defects: **none open**; rule-source blockers: none; every executed check passes. The Phase 2 Gate is still **INCOMPLETE / not declared passed** and the review status stays `검토 대기`. Approval is **possible** if GPT Chat accepts the ledger design with the recorded limits (example 7 equivalent, two-out fielder's choice), the C38 carry-over and the uncertified D2 baseline; a Codex re-review of the scoring rules is recommended before approval. This work did not approve anything.

## Part A4 — OBR difference fix round (2026-10-11): C12, C14, C19, C24, C32, C34 fixed, C29 explicit, C33 / C38 reported

Instruction: GPT Chat, "OBR 편차 처리 최종 결정" (delivered in chat; policy: KBO first, unconfirmed general rules from the 2026 OBR, appeals excluded, only approved simplifications). Scope: Phase 2 scoring accuracy and substitution legality only.
Status stays **`검토 대기`**; nothing is committed or pushed; Phase 3 is not started; R02 is untouched (`GAME_SIMPLIFICATION_APPROVED`, `clause_certified = false`, kept apart from the Futures rule R28).

**A4.1 Results.** All six differences were fixed in the engine (no engine redesign, no appeal play, no new approved simplification):

| Row | Clause | Result | Counter-example before -> after |
|---|---|---|---|
| C12 | 9.02(a)(17), 9.04(b)(1), 9.11 | GIDP and the withheld RBI only for a grounded-into force / reverse-force double play; the defensive DP credit stays separate | runner on third thrown out at the plate first, batter out at first: GIDP 1 -> **0**, DP credit kept; runner on third scores while the runner on second is thrown out at third and the batter is out: RBI 0 -> **1** |
| C14 | 9.16(i) | a pitcher's earned runs are judged from his own entry; new team figure `TeamER` | official example 1 (two out, walk, error, relief home run): reliever ER 0 -> **1** (Peter 0 of 2, team 0); example 3 team ER (missing) -> **2** |
| C19 | 9.03(c) | a runner whose run was voided by the third out is left on base once, derived from his final state; the reconciler checks the proof | batter-runner third out with a runner on second and a voided run from third: left on base 1 -> **2**; box proof 5 vs 4 -> **5 = 5**; 100 games: proof short in 70 of 200 team-games -> **0** |
| C24 | 5.11(a)(2) | the starting DH cannot be replaced before his first plate appearance unless the opponent changed pitchers | pinch hit for the DH due first: accepted -> **`Game.dh_must_bat`**, state untouched |
| C32 | 5.11(b), (a)(12) | an ordinary or replacement DH who pitches ends the DH role (or is refused where the profile forbids it); the exception is the starting pitcher listed as DH | ordinary DH to the mound with the DH kept (`DhActive` true) -> **false** (forfeit profile) / **refused** (others) |
| C34 | 9.16(h) | 2-0, 2-1, 3-0, 3-1, 3-2 charge a following walk to the previous pitcher | 3-2 and 2-0 / 2-1: walk charged to the reliever -> **to the previous pitcher** |

**A4.2 Reproduction first.** `tests/StarPlayer.Simulation.Tests/ObrDeviationTests.cs` was written before any fix and run on the unchanged engine: **15 failing tests** (14 caught the difference; one failed only because of an arithmetic slip in the expectation of a walk-off test,
corrected to 29 = 24 earlier at-bats + 5, which is not evidence of a defect), 17 passing (guards that already held, such as the reverse-force double play and Examples 2 and 3 of 9.16(i)). After the fixes all 32 pass. The 70-of-200 box-proof failure was
measured on the previous 100 games before the fix. The old 100-game file cannot be replayed on the new engine (it contained a DH going to the mound with the role kept and replacements of an unbatted DH), so the 70 cases are covered by the hand cases and by the regenerated games, which contain
the same situation in 71 team-games.

**A4.3 C29.** The engine no longer places the DH into a vacated position by itself: `PitchingChange` of a lineup fielder needs `Slot` = the DH's batting-order number (`Game.dh_vacated_position_unspecified`, `Game.dh_vacated_position_invalid` otherwise, state untouched). The only supported arrangement is the legal OBR 5.11(a)(5)/(14)
one the event names (the DH takes the position, the replaced pitcher leaves, the new pitcher bats in his slot, `DhActive` ends); every other arrangement and a double switch is refused (rows C39, NOT_MODELLED). Not claimed as an approved simplification.

**A4.4 C33 and C38 (reported only, unchanged, not approved).**
- **C33, OBR 9.16(g) and comment.** Original rule: a reliever is not charged with runs of runners on base when he entered, nor of runners who reached on a fielder's choice that put out a runner left by the predecessor; each pitcher answers for the *number of runners he put on base*, so the runs scored after a left-on-base runner was put out *by the batter's action* are the predecessor's up to that
number (examples 3 and 7), unless the runner vanished without batter action (caught stealing, picked off, interference: example 4). Engine: a per-runner `ResponsiblePitcher`; identical in examples 1, 2, 4 and 5-6, different in 3 and 7. Impact: `PitchR`, `PitchER` and the losing pitcher in those situations. Recommendation: a per-pitcher count of inherited
runners with the exclusion above (medium change, new hand oracles and generator update) after your decision, or an explicit recorded acceptance. **Gate-required: yes** (rule R17), so it is tracked as a possible implementation defect.
- **C38, OBR 9.08(d) and comment.** Original rule: sacrifice fly with fewer than two out when a fly ball handled by an outfielder (or an infielder running into the outfield) is caught and a runner scores after the catch, or is dropped (error) and the scorer judges the runner would have scored; still a sacrifice fly when another runner is forced out. Engine: Fly only, exactly one out on the play,
a clean run; line drives, plays with a second out and dropped-ball judgements are not sacrifice flies, and the fielder is not checked. Impact: sacrifice-fly and at-bat counts in rare plays, no effect on runs or RBI (a fly-ball double play with a run now gives the RBI but stays an at-bat). Recommendation: keep as an explicit unsupported simplification until Phase 3 models who
handles the ball, then implement 9.08(d) whole. **Gate-required: no** (supplementary statistic, no effect on score, RBI or box proof).

**A4.5 New and changed tests.** New: `ObrDeviationTests` (33 cases incl. theories), `BoxProofTests` (exact proof, 200/200, 71 voided-run team-games), reconciler test for the 9.03(c) proof, replay/JSON test for the explicit DH slot, `RuleEvidenceTests` +1 (9 in total).
Changed with re-derived hand expectations (none weakened): left on base +1 after a voided run in `ThirdOutAndOrderTests` (3 expectations), `CountAndPlateAppearanceTests` (1), `BatterRunnerAfterHitTests` (1); the 3-2 walk theory; the DH to the mound test (renamed, now ends or refuses); `Slot` for fielder-to-mound; an opposing pitching change before a DH replacement.
Python generator (independent scorer) updated with the same clauses and the 100 games / 30,974 events regenerated (54 away wins, 40 home wins, 6 ties): 100/100 match. It is an integration check and not an independent oracle; the hand oracles are.

**A4.6 Full regression.** xUnit **540 passing, 0 failed, 0 skipped** (Domain 145, Simulation 207, Application 114, Infrastructure 74; previous 504). Third-out and batter-runner-after-hit regressions, inherited-runner responsibility, DH / two-way transitions, event fingerprints and JSON replay, and Phase 0-1 suites all pass.

**A4.7 Box proof (OBR 9.03(c)).** Exact in all 200 team-games of the 100 games (no adjustment term), including 71 team-games with voided runs and the walk-off games; hand cases cover batter-runner third out, force third out and a walk-off.

**A4.8 Windows export and Gate verification.** `tools/verify.ps1` was run twice in a row on the final code: both **`GATE: PASS`, exit code 0** ([run 1](PHASE_2_VERIFY_LOG_obr_fix_run1.md), [run 2](PHASE_2_VERIFY_LOG_obr_fix.md)). Each: Release build 0 warnings; xUnit Domain 145 / Simulation 207 / Application 114 (architecture 14, determinism 48) / Infrastructure 74, 0 failed, 0 skipped; Godot headless build, SQLite 8/8, player card 7/7 with 0 hidden-field leaks, 6 game scenarios finished and matching; fresh `ExportRelease` Windows export; cold-copy run of the exported program (SQLite 8/8, player card and game screen at QHD with screenshots); relative markdown links resolve (38 / 39 files). Earlier logs are preserved unchanged. This is a technical result and not an approval.

**A4.9 Remaining technical risks and self-assessment.**
- C33 (Gate-required R17) awaits the owner; C38 on hold; the KBO 2026 rule book and record rules are still unread, so the D2 profile is policy (KBO first / OBR fallback), not a KBO certification; R02 is an application approval without a certified clause.
- Not modelled and refused: errors between putouts (C40), double switch and other vacated-position arrangements (C39), pitcher batting for the DH (5.11(a)(10)), replacing a pitching two-way DH.
- The Python scorer and the engine share an author and the reading of the clauses; the hand oracles (official 9.16(i) examples, definitions of terms) are the independent check. No human click-through, no FHD, no Codex re-review of this round (recommended for the scoring-rule changes C12, C14, C19).
- Self-assessment: known implementation defects against the read OBR clauses: **none open except the pending C33 (and C38, not Gate-required)**; rule-source blockers: **none**. Phase 2 approval is **possible once you decide C33 and accept the uncertified D2 baseline**; this report does not approve the Phase and the Obsidian state stays `검토 대기`.

**A4.10 Review needed.** GPT Chat: C33 fix-or-accept, C38 keep-unsupported, C29 explicit-request design, the vocabulary separating rule-source blockers from implementation defects. Codex (recommended: core scoring rules and substitution legality): `GroundedIntoForceDoublePlay`, `EarnedRuns` start index and `TeamER`, the voided-runner left-on-base derivation, `StartingDhMustStillBat`, the DH branches of `ApplyPitchingChange`.

---

## Part A3 — R02 decision and OBR original-text comparison (2026-10-11)

Instruction: `TASK.md` (GPT Chat review result). Status stays **`검토 대기`**; nothing is committed or pushed; Phase 3 is not started. Details: `docs/game-engine.md` §11, `docs/decisions/ADR-003.md` items 23-26, `content/rules/game_rules_evidence.json`.

**A3.1 R02 applied.** Owner application approval: a regular-season game level after the 11th inning is a tie. Evidence: KBO board announcement 2025-01-22 (limit 11) and the official KBO news item of the 2026-06-03 Hanwha–Doosan game
(3-3 after 11 innings); the 2026-10-04 KT–Lotte 2-2 game (11 innings) was read in a press report as extra support (two more 2026 tie games were seen only as search snippets and are not relied on). The KBO league-regulation clause was **not** found:
`clause_certified = false`, status `GAME_SIMPLIFICATION_APPROVED`. R02 is no longer `UNRESOLVED`; `gate_blockers` is empty. **R02 resolution (an application approval backed by operating evidence) and D2 rule certification (KBO rule book / record rules unread) are separate facts and
are reported separately.** Risk found and kept out of the first-team evidence: notice 11795's "승부치기 10~12회, 이후 무승부" sentence is inside the ▲퓨처스리그 item (new `UNRESOLVED`, non-Gate rule R28 for a future second-team profile).

**A3.2 OBR original text.** `docs/2026-official-baseball-rules.pdf` (SHA-256 `f85860f3b754dfb5e07269e9d3a86c77c89b856376f6e3ba8345af36db997859`, 192 pages) was extracted with `pdftotext -layout` and read. The requested clauses were compared: 5.08(a), 5.11(a)/(b), 9.03(c), 9.04, 9.16, plus 5.05, 5.06(c)(1),
5.09(a), 5.10, 7.01, 9.02, 9.08, 9.17, 9.19 and the definitions FORCE PLAY / DOUBLE PLAY: **38 comparison rows**, `MATCH` 16, `KBO_PRECEDENCE` 1, `APPROVED_SIMPLIFICATION` 9, `PROPOSED_SIMPLIFICATION` 3, `NOT_MODELLED` 3, `OPEN_DEVIATION` 6. Clauses not compared are listed (`obr_not_compared`).
The MLB glossary readings are no longer counted as clause confirmation; the old "OBR clause numbers unread" statements are superseded (A2.4, A2.5, A2.10, A.7 first bullet).

**A3.3 Open differences (not fixed in that round; all six were fixed in Part A4).** Each has Issue / Reason / Recommended Option / Alternative / Impact in the evidence file.
C12 any 2-out non-hit play is treated as a double play (OBR: force / reverse-force only); C14 earned-run reconstruction ignores pitching changes (9.16(i)); **C19 the 9.03(c) box proof fails in 70 of 200 team-games of the 100 games because runs voided by the third out are not left on base (measured;
adding the voided runs balances 200/200, walk-off games included)**; C24 DH must bat once (5.11(a)(2), not Gate-required); C32 two-way DH entry is wider than 5.11(b); C34 relief walk-count table is a 2-count subset of 9.16(h). C12, C14, C24 and C32, C34 are known by code reading only (`미검증` as failing reproductions);
C19 is measured by `BoxProofTests`. Three engine simplifications are first recorded now and wait for owner confirmation (C29 who fills the vacated position, C33 9.16(g) inherited-runner counting, C38 sacrifice-fly derivation); they are **not** called approved.

**A3.4 DH classification.** OBR 5.11(a)(14) ("a player on defense goes to the mound (i.e., replaces the pitcher)" ends the DH) is an original clause, so `fielder_to_mound_forfeits_dh` was wrongly labelled invented/fictional. It is now rule R27 (`OBR_FALLBACK_APPROVED`, not a KBO certification) and the flag is **on in the KBO-oriented profile**
(the only runtime behaviour change of this round: a lineup fielder going to the mound while a DH is active is now legal and ends the DH instead of being refused; profile content hash changes; no persisted game uses it). 5.11(b) was read: leaving the mound keeps the two-way DH (matches); the engine's entry path is wider (C32). Lineup mechanics for the vacated position are an engine simplification (C29).

**A3.5 Files changed in this round.** Data: `content/rules/game_rules_evidence.json` (28 rules, 38 comparison rows, policy/PDF record), `content/rules/game_rules_profiles.json` (KBO profile texts and one flag; fictional profile wording). Code (comments only): `GameRules.cs`, `GameEvents.cs`, `GameReducer.Substitutions.cs`. Tests: `RuleEvidenceTests` (rewritten:
OBR-read policy, clause coverage, comparison-table discipline, R02 wording, empty blockers, profile flags), `BoxProofTests` (new, 9.03(c) measurement), `DhStateTests` (comment and one test renamed: `..._keeps_the_mound_transition_off`). Docs: `docs/game-engine.md` §2.1/§4/§5/§6/§8/§11, `docs/decisions/ADR-003.md` items 23-26, this report, `Star/01_Phase_Reviews/Phase_02_Review.md`, `CURRENT_STATE.md`, dashboard, `docs/SYSTEM_CONTRACTS.md` note.
No engine behaviour other than the profile flag changed; no expectation of an existing test was changed; no SQLite change; no Phase 3 work.

**A3.6 Tests.** xUnit **504 passing, 0 failed, 0 skipped** (Domain 145, Simulation 174, Application 114, Infrastructure 71; previous 500: Infrastructure 67 → 71: `RuleEvidenceTests` 5 → 8, `BoxProofTests` +1). Full `tools/verify.ps1` result: see A3.8. Manual human click-through, FHD: `미검증` (unchanged).

**A3.7 Phase 2 Gate: INCOMPLETE (kept; the state of that round, see A4.9 for the current one).** Met: R02 resolved; every executed check passes; requested OBR clauses compared; classifications explicit. Not met / open: six `OPEN_DEVIATION` rows (five in Gate-required areas) await the owner's choice between fixing and accepting as recorded simplifications; the KBO 2026 rule book and record rules are unread
(D2 certification incomplete); three proposed simplifications are unconfirmed. This report does not mark the Gate passed; the verdict belongs to GPT Chat / the owner.

**A3.8 Verification log.** `tools/verify.ps1 -LogName PHASE_2_VERIFY_LOG_r02_obr.md` was run **once** on the final code: `GATE: PASS`, exit code 0 ([log](PHASE_2_VERIFY_LOG_r02_obr.md)). Release build 0 warnings; xUnit Domain 145 / Simulation 174 / Application 114 / Infrastructure 71 (architecture 14, determinism 48), 0 failed, 0 skipped; Godot headless build, SQLite 8/8, player card 7/7 with 0 hidden-field leaks, 6 game scenarios finished and matching; fresh `ExportRelease` export; cold-copy run of the exported program (SQLite, player card and game screen at QHD with screenshots); 37 markdown files with all relative links resolving. Earlier logs are preserved unchanged. This is a technical result and not an approval; the previous rounds ran the script twice, this round once.

**A3.9 Review needed.** GPT Chat: confirm the R02 wording (application approval, clause not certified) and the classification vocabulary; decide each open difference C12 / C14 / C19 / C24 / C32 / C34 (fix vs accept) and confirm C29 / C33 / C38. Codex (recommended: core scoring rules): C19 box proof / left-on-base change, C12 double-play definition, C14 earned-run pitcher awareness.

---

## Part A2 — final round (2026-10-11): batter-runner third out, appeals excluded, rule-source policy

> Superseded by Part A3 where it says: R02 `UNRESOLVED` (A2.10, A2.11), "2026 OBR text unread" (A2.4, A2.5, A2.10), the fielder-to-mound transition being invented / fictional-only (A2.3), test counts (500 → 504).

Instruction of that round replaced the earlier remediation requests of the day. Status stays **`검토 대기`**; Phase 2 Gate **INCOMPLETE**; nothing is committed or pushed; Phase 3 not started.

**A2.1 Batter-runner out before / after reaching first (Codex re-review High).** A hit may carry the batter move (`From 0, To 0`): safe on the hit, then put out going
for more. The proof is the out base, not a flag: `OutAt` must be beyond the base the hit gave him; kind must be `Tag`; the order is data (`Sequence`/`BatterSequence`). Out before first base
(result `Out`) voids the play's runs; out after a hit is a NonForce tag out, so earlier runs count. One plate appearance, one hit, one at-bat, one out, one fact; a hit with two outs is
no double play (RBI kept, no GIDP). Earned-run replay and the Python generator implement the same split. Invalid claims (no `OutAt`, `OutAt` at/before the hit base, force kind, wrong destination,
a batter move on HR/Out, disagreeing order numbers) are refused with the state unchanged.

**A2.2 Appeals excluded (owner-approved scope exclusion).** Not implemented: missed base, failed retouch, appeal third out and its run rule, fourth-out appeal. Refused as `Game.appeal_not_supported`
(`RunnerPlayKind.Appeal`, `OutKind.Other` for runners, retouch out after a caught ball, base-touch out of a non-forced runner). Never converted into a tag out. Codex Medium = `승인된 범위 제외`.
It is a game simplification, not a statement about KBO/MLB rules.

**A2.3 KBO-first / 2026-OBR-fallback policy and profile changes.** `content/rules/game_rules_evidence.json` (26 rules, status per rule, sources read, tests); `GameRuleSnapshot.RulePolicyId`;
profile fields `rule_policy`, `fielder_to_mound_forfeits_dh`; the KBO-oriented profile enables only the OBR-style "DH plays defense ends the DH" transition; the invented fielder-to-mound one is fictional-only
(`test-dh-forfeit`). `RuleEvidenceTests` guards the bookkeeping.

**A2.4 Official KBO evidence confirmed.** Only R01 (regular-season extra-innings limit 11, KBO press release 2025-01-22) is `KBO_VERIFIED`; 2026 continuation is not read. The 2026 KBO rule book and the
record rules could not be read (rule-book notice has no text; the record-rule PDF link triggers a download that was not retrieved; the e-book is images). KBO notice 11814 only echoes the force/timing principle.

**A2.5 OBR fallback items.** `OBR_FALLBACK_APPROVED` (comparison source: MLB glossary pages actually read; the 2026 OBR PDF was a download prompt and was **not read**, so no OBR clause number is cited as verified): counts/innings/home skips the last half
(R04), DH exists (R05), DH plays defense ends the DH (R06), two-way DH after leaving the mound (R08), third out and runs (R11), batter-runner out after reaching (R12), force play (R13), hit by pitch dead ball (R14), RBI (R16), earned run (R17), win/loss (R19),
save (R20), team left on base (R21), substitutions (R23). **None of these is KBO certification.**

**A2.6 Game simplifications.** `GAME_SIMPLIFICATION_APPROVED`: no MLB automatic extra-inning runner (R03), appeals excluded (R15), earned-run reconstruction (R18), walk-off left on base (R22), no pitch-clock/ABS logic (R24).

**A2.7 New hand-oracle tests (all expectations counted by hand).** `BatterRunnerAfterHitTests` (9): Case 1 out before first = 0 runs; Case 2 hit, then tag out at second as third out with the run before it = 1 run, 1 hit, 1 at-bat, 1 out,
RBI 1, runs allowed 1, earned 1, box score runs 1 / hits 2 / LOB 0; Case 3 order swapped = 0 runs but hit and at-bat stand; Case 4 double/triple stretches, one PA, order advances once, no double count; two-outs-on-a-hit is not a DP; Case 5 seven invalid claims refused,
state unchanged; no order given = refused; Case 6 order swap changes result and fingerprint, same id other order = conflict. `AppealExclusionTests` (6), `DhStateTests` (+2), `GameFlowTests` (+1: no automatic runner),
`RuleEvidenceTests` (5), `ReplayContractTests` (+1, Case 7: JSON round trip of the hit-then-out event gives identical results and fingerprints; Case 8 consistency is asserted in Case 2 and the 100 games).
Two earlier tests of `ThirdOutAndOrderTests` that used a doubled-off runner (a retouch appeal) were rewritten with a tag-up out; no test was deleted; no coverage was reduced.
Manual mutation check of the new code: 8 mistakes (batter out after hit classed as batter-runner out, no evidence of reaching required, a two-out hit counted as a DP, other/appeal outs accepted,
appeal request accepted, retouch appeal accepted, earned-run replay class, mound transition flag) — the first run left the two-out-hit mistake undetected, a test was added, **all 8 are now detected**.

**A2.8 Tests and 100 games.** xUnit **500 passing, 0 failed, 0 skipped** (Domain 145, Simulation 174, Application 114, Infrastructure 67; previous 476). 100 games / 31,075 events regenerated by the Python scorer (which implements the same rules
separately) match **100/100**; they contain 116 batter-out-after-hit plays (50 with a third out and an ordered run: counted and voided cases). The Python generator is an **integration check, not an independent rule oracle** (same author,
same reading); the hand oracles above are separate. No existing expectation was weakened.

**A2.9 Windows export and verification script.** `tools/verify.ps1` run twice on the final code, both **PASS**, exit code 0 (`docs/reports/PHASE_2_VERIFY_LOG_run1.md` 00:18:29 and `docs/reports/PHASE_2_VERIFY_LOG.md` 00:21:57, +09:00): Release build 0 warnings; xUnit counts above; Godot headless
build, SQLite 8/8, player card 7/7 with 0 hidden-field leaks, 6 game scenarios finished and matching; fresh ExportRelease export; cold-copy run of the exported program (SQLite 8/8, player card and game screen at 2560x1440 with screenshots); 37 markdown
files with all relative links resolving. Earlier logs are preserved (`_initial*`, `_remediation1*`, `_remediation_try1_FAIL_doc_links`). Manual human click-through remains `미검증`; FHD is Phase 5+.

**A2.10 Unresolved risks.** R02 (tie when the 11-inning limit is reached) is required and unresolved; KBO 2026 rule book / record rules / 2026 OBR text unread (fallbacks are policy, not certification); scorer-supplied order of outs and runs is trusted for physical truth
(the engine checks consistency); a batter's touching of the base the hit gave him is assumed; OBR DH triggers beyond "DH plays defense" and replacing a pitching two-way DH are not implemented; appeals, interference, infield fly, suspended/rain games out of scope; Python oracle independence limit; no human click-through.

**A2.11 Phase 2 Gate self-assessment: INCOMPLETE.** Met: High defect fixed, appeals excluded by owner decision, rule sources classified with KBO/OBR clearly separated, tests and regression green, supported statistics reconcile, Common Gate G evidence collected.
Not met: one required rule (R02) is `UNRESOLVED`. It resolves when the owner approves "level after the limit inning = tie" as a Star Player simplification (or supplies the KBO text); then the Gate can be reported as met. `tools/verify.ps1` PASS is a technical result and does not approve the Phase.

**A2.12 Review needed.** GPT Chat: R02 decision, the evidence classification, the appeal exclusion wording. Codex re-review: batter-runner classification in `GameReducer.Order`/`EarnedRuns`, `ThirdOutRule` reuse, the appeal refusals (no hidden conversion to tag outs), DH switch split.

---

## Part A — Codex review remediation

Codex independent review of Phase 2: **`CHANGES REQUIRED`** — High 1 (third-out and run decision), High 2 (event id idempotency collision), High 3 (DH
kept/lost state), Medium 4 (hit by pitch with wild pitch / passed ball), plus left on base at a walk-off. This verdict is kept; the fixes follow.

### A.1 Result per finding (pre-fix reproduction vs after)

Pre-fix reproductions were written first as `CodexReproTests` (they assert the correct behaviour and are kept as regression tests). The Codex
reproduction programs themselves were not available to this work, so the reproductions are this work's own.

| Finding | Pre-fix reproduction | After the fix |
|---|---|---|
| High 1 — third out | Repro 2 (a runner out with no declared kind): **failed pre-fix** (accepted). Repro 1 (forced runner declared "tag" on the third out so a run counts): the first pre-fix run failed during its own set-up (a wrong runner move in my script), so it is **not valid evidence**; the pre-fix behaviour is known from the code, which trusted the declared kind and a scorer flag (`blocked` looked only at declared `Force`, `TimingPlayRuns` let the run count). The corrected repro was only run after the fix | order, force derivation and third-out rule implemented; Repro 1 and 2 pass; 16 hand-oracle tests in `ThirdOutAndOrderTests` |
| High 2 — event id | Repro 3 (same id, other kind): **failed pre-fix** (returned a duplicate success) | `Game.event_id_conflict`; Repro 3 passes; `EventIdentityTests` (10) and `ReplayContractTests` (5) |
| High 3 — DH state | no equivalent reproduction: the engine could not express a DH ending at all (`rules.DesignatedHitter` was read everywhere), so this is a missing capability, not a wrong result | `DhActive` per team, forfeiture transitions in fictional profiles only, 13 tests in `DhStateTests` |
| Medium 4 — HBP | Repro 4 (hit by pitch + wild pitch with a runner move): **failed pre-fix** (accepted) | `Game.hbp_with_mishap` / `Game.hbp_extra_advance_invalid`; Repro 4 passes; `HitByPitchTests` (8) |
| Walk-off LOB | known from the code (`halfOver && !walkOff`: always 0) | profile rule, 8 tests in `WalkOffLeftOnBaseTests` |

**High 1.** An out now carries a required kind, an order and the base where it was made. The reducer derives whether a runner was forced at that moment
(batter-runner still alive, every base behind him occupied by a runner not yet out, out made at the base he was forced to) and rejects a declared kind that
disagrees; a forced runner tagged out is a force out. The third out decides the runs: batter-runner or force out voids every run of the play, any other out
counts only the runs that crossed the plate before it, runs after it never count. The same rule object (`ThirdOutRule`) is used by the earned-run replay
with the stored order and classes. A voided run is in no statistic (run, RBI, runs allowed, earned run, box score, left on base) and stays visible as
`RunVoided`. `TimingPlayRuns` no longer exists. Rejected plays change nothing.

**High 2.** A processed event leaves a receipt (id, kind, `ef1` SHA-256 of a canonical, order-independent payload text). Same id and content is a
duplicate, same id with other kind or content is a conflict, a receipt of another version is refused, a rejected event leaves no receipt. The fingerprint
format, null/empty/ordering rules and the version policy are in `docs/game-engine.md` §3a with a literal-text and literal-hash test; the events and the
receipts have a JSON form (the resume contract, no SQLite before Phase 4).

**High 3.** `GameRules.DesignatedHitter` (profile) is separate from `TeamGameState.DhActive` (this team now). `DhMayBeForfeited` is false in the KBO-oriented
profile (the official conditions are unverified, nothing was guessed) and true only in fictional profiles, which define two invented transitions
(`DhToField`, a lineup fielder going to the mound). After the DH ends no event can create one; the other team is never touched; two-way players have
explicit allowed and forbidden transitions; every rejection leaves the state unchanged; the DH state is rebuilt by replaying the JSON events.

**Medium 4.** A hit by pitch is a dead ball: no wild pitch, passed ball, dropped third strike or runner move on it; only the forced advance (with its forced
run and RBI) is produced by the reducer. Ball four may still carry a wild pitch. A strikeout credit list is only valid on a real strikeout. The general
baseball principle is the comparison; KBO certification is separate (A.7).

### A.2 Left on base after a walk-off

Old behaviour: always 0 at a walk-off. New: team LOB = runners standing on base when the game ends, counted under the profile rule
`CountWalkOffRunnersAsLeftOnBase` (true in every shipped profile, false only in a test variant). Completed half-innings (three outs, including the
top half after which the home team does not bat) count their stranded runners as before; the unfinished walk-off inning counts the runners left after the
winning run (walk-off single: the batter and any runner who stays; walk-off home run: none; bases-loaded walk or hit by pitch: three). Only a team
figure exists, no individual LOB (`StatKey` has no such key; tested). Expected values are counted by hand from the final base picture. External
definition used for comparison: MLB glossary (team LOB = runners remaining at the end of an inning); how KBO counts an unfinished walk-off inning is
**unverified**, so this is recorded as a simplification in the profile open items and `docs/game-engine.md`.

### A.3 Main changed files (this round)

Code: `src/StarPlayer.Domain/Baseball/Game/{GameEvents,GameModel,GameRecords,GameRules}.cs`; `src/StarPlayer.Simulation/Baseball/{ThirdOutRule,GameReducer.Order,EventFingerprint}.cs` (new), `GameReducer.{Apply,Plays,Finish,Start,Substitutions}.cs`, `EarnedRuns.cs`;
`src/StarPlayer.Application/Projections/GameText.cs`; `src/StarPlayer.Infrastructure/Fixtures/ScriptedGameJson.cs`.
Data: `content/rules/game_rules_profiles.json` (new fields, `test-dh-forfeit`, KBO evidence text), `content/fictional/phase2_diagnostic_games.json` and
`tests/Fixtures/phase2_scripted_games_100.json` (regenerated), `tests/Fixtures/generator/generate_scripted_games.py`.
Tests (new): `ThirdOutAndOrderTests`, `EventIdentityTests`, `DhStateTests`, `HitByPitchTests`, `WalkOffLeftOnBaseTests`, `CodexReproTests` (Simulation), `ReplayContractTests` (Infrastructure);
edited: `Script.cs`, `CountAndPlateAppearanceTests`, `GameFlowTests`, `RunnerPlayAndEarnedRunTests`, `GameRulesProfileTests`. Docs: `docs/game-engine.md`, `docs/decisions/ADR-003.md`, this report, the Obsidian review, `CURRENT_STATE.md`, dashboard. `tools/verify.ps1` log title.
No SQLite schema or migration, no new package, no Phase 3 code.

### A.4 Independent tests added and their results

All expectations are worked out by hand from the rules (the test names and the comments state the arithmetic); none is read from the engine.
Simulation 156 passing (was 99), Infrastructure 61 passing (was 56): +62 tests net (+64 new, 2 removed).

- Third out (16): run before a non-force third out counts / the same play in the other order voids it / no order given is rejected / force third out voids in
  both orders / a forced runner tagged is still a force out, declared "tag" rejected / out beyond the forced base is not a force out / an earlier out removes the force
  and the earlier run counts / the opposite order keeps the force and voids it / batter-runner third out voids in both orders / a caught ball makes the doubled-off
  runner a non-force out / double steal in both orders / order-number and out-detail validation / earned run only if it crossed before the third out the error
  prevented (both orders).
- Event identity (10), replay contract (5): see A.1; every constructor parameter of every event changes the fingerprint; fingerprint stable across threads and culture; after the game is over duplicates change nothing and finalization stays once; round trip of all 31,016 events of the 100 games keeps every fingerprint.
- DH (13), hit by pitch (8), walk-off left on base (8): see A.1/A.2.
- The 100 games now also contain plays with a third out and a run in both orders; `ReplayContractTests.The_hundred_games_exercise_...` asserts that the suite really contains voids by force out, by batter-runner out, after a non-force third out, runs counted before a non-force third out, and walk-off games with left-on-base runners.
- **Mutation check** (manual, one-off, script not kept): 13 deliberate engine mistakes (non-force third out treated as force, order ignored, force never removed, out base ignored, batter-runner out counted as non-force, receipt compares kind only, walk-off LOB forced to zero, hit by pitch with mishap allowed, DH state not cleared, fingerprint ignoring the move sequence, declared kind trusted, missing kind accepted, DH re-creatable) — **all 13 caught** (Simulation 1-7 failures each, several also by the 100 games).
- Independence limit kept: the 100-game expectations come from a Python scorer written by the same author from the same document, so they share its reading of the rules; the hand oracles above are the separate check.

### A.5 Regression of the earlier 414 tests

All four earlier suites run: Domain 145, Simulation 156, Application 114, Infrastructure 61 = **476 passing, 0 failed, 0 skipped**. Of the first-round 414 tests,
408 are unchanged and pass; **2 were removed** because the scorer flag they tested no longer exists (`A_run_that_scores_before_a_non_force_third_out...` and
`A_force_out_for_the_third_out_cancels_runs_even_with_the_timing_flag`; both cases are covered by the new model); **4 were edited by design**: the batter-third-out
sacrifice-fly test (a rejected play became a voided run), the walk-off single LOB (0 → 1), the profile count (6 → 7) and the applied-event-list assertion (ids → receipts). No other expectation changed.

### A.6 100-game verification

100 games / 31,016 events (the regenerated set): every game replays to the independently generated expectation (score, hits, errors, line score, LOB,
per-player batting/pitching/fielding, catcher and battery exposure, decisions, earned/unearned): **100/100 match, 0 reconciliation problems**; per-event
invariants (ScoredPlay behind every run, no negative outs, no shared base) hold; ScoredPlay/StatDelta/BoxScore reconcile; each event's fingerprint survives a
JSON round trip; finalization runs once. 6 diagnostic games embedded in the exe are the first of the set and pass in the game screen.

### A.7 GameRules certification status

Official KBO pages read (all five listed by the owner, details in `docs/game-engine.md` §5): **the extra-innings limit of 11 now has an official source**
(press release of 2025-01-22, regular-season extras reduced from 12 to 11 innings from 2025). Everything else remains **unverified**:

- the 2026 rule book (published, notice 11896) and the record rules are not readable here (the record-rule PDF link answers with a file download that this work did not retrieve — the in-app browser showed a save prompt for it once; the e-book is images);
- tie at the limit, 2026 continuation of the limit, DH and two-way rule and when a DH ends, the third-out/force principle (only echoed by notice 11814), hit by pitch dead ball, RBI/earned/decision/left-on-base definitions, left on base at a walk-off.

Rules with no official basis are either marked unverified in the KBO-oriented profile or live only in clearly named fictional profiles
(`test-*`, status `fictional_test`, `DhMayBeForfeited` only there). MLB/OBR material is a comparison only.

**Phase 2 Gate: INCOMPLETE.** `tools/verify.ps1` is a separate fact: PASS.

### A.8 Windows export and UI verification

`tools/verify.ps1` was run on the final code: see `docs/reports/PHASE_2_VERIFY_LOG_remediation1_run1.md` and `docs/reports/PHASE_2_VERIFY_LOG_remediation1.md` (two consecutive runs,
both `Gate result: PASS`): Release build 0 warnings; xUnit Domain 145 / Simulation 156 / Application 114 (architecture 14, determinism 48) / Infrastructure 61;
Godot headless build, SQLite diagnostics 8/8, Phase 1 player card 7/7 with 0 hidden-field leaks, Phase 2 game diagnostics (6 games stepped to the end, all match, 7/7);
fresh `ExportRelease` Windows export with `e_sqlite3.dll`; cold-copy run of the exported program: SQLite 8/8, player card and game screen at 2560×1440 with screenshots;
all relative markdown links resolve. The first remediation attempt of the script failed only on the doc-link check (D1) because I renamed the first-round
logs while it ran; that log is kept as `PHASE_2_VERIFY_LOG_remediation_try1_FAIL_doc_links.md`. No human click-through and no FHD run were done (`미검증`).

### A.9 Unresolved issues

1. D2 production subset not certified (A.7) — needs the owner to supply the official 2026 rule book / record rules, or an equivalent official document.
2. The scorer-supplied order of outs and runs is trusted for physical truth (the engine checks consistency, not reality); appeal outs are modelled as non-force outs only.
3. The forfeiture transitions are fictional; they must not be reused for KBO without a source.
4. Left on base at a walk-off and stolen-base credit with a voided run are recorded simplifications.
5. Python oracle independence limit; manual mutation script not kept in the repo.
6. No human click-through of the diagnostic screen, no FHD, no second Codex review.

### A.10 Gate verdict

Executed checks: all PASS (476 tests, 100 games, export, UI, links). **Phase 2 Gate: INCOMPLETE** until the D2 certification items in A.7 are supplied
and reviewed. Review status stays `검토 대기`. GPT Chat review of this remediation is requested; Phase 3 is not started.

---

## Part B — first-round report (as submitted before the Codex review)

Items of Part B that Part A supersedes: the test counts (414 → 476), the 100-game event count and mix, the GameRules evidence statement (Part B said no official
source was found; the extra-innings limit now has one), `TimingPlayRuns` and the "the scorer flags timing plays" simplification, the walk-off LOB 0 rule, and the
HBP/DH statements. The verification logs of the first round are `PHASE_2_VERIFY_LOG_initial*.md`.

Status: implementation and self-verification complete; **review status `검토 대기`** (not approved). Gate verdict: **INCOMPLETE** — every executed Phase 2 check passes, but the D2 GameRules *production* subset could not be certified against an official KBO source (see §8). Nothing in this phase is committed or pushed (base commit `64167f788dc6a77fe4725bb13e06d946fd122ada`, working tree dirty). Phase 3 has **not** been started.

Environment: Windows 11 Home 10.0.26200, .NET SDK 8.0.425 (pinned), Godot 4.7.2 Mono, Release build. Final verification logs: `docs/reports/PHASE_2_VERIFY_LOG_initial_run1.md` (19:31:12 +09:00) and `docs/reports/PHASE_2_VERIFY_LOG_initial.md` (19:33:50 +09:00), both `Gate result: PASS`; raw output under git-ignored `build/verify/`.

## 1. Phase 2-A ~ 2-D implementation

**2-A Phase 1 Medium debt.** `Ratings.cs`, `Proficiency.cs`, `PlayerStatus.cs`, `DevelopmentProfile.cs`, `Player.cs`: inputs are defensively copied, exposed lists are `ReadOnlyCollection` (`AsReadOnly()`), so an `IList` cast `Add/Clear/RemoveAt/set` throws `NotSupportedException`; elements are immutable; changes only through the existing validated factories. Phase 1 JSON format and visibility policy are unchanged (all 138 earlier Domain tests still pass). `tests/StarPlayer.Domain.Tests/CollectionImmutabilityTests.cs` (7 tests: frozen exposure for each type, input copies, element immutability by reflection). No generic immutable-collection framework was added.

**2-B Contract.** `docs/game-engine.md` (inputs, outputs, invariants, scoring conventions, GameRules scope/provenance/open items, simplifications, oracle design and its limits, test criteria). Models in `src/StarPlayer.Domain/Baseball/Game/`: `GameState`, `PitchCountState`, `RunnerState`/`BaseRunner`, `LineupSlot`, `PitcherUsage`, `ScoredPlay`, `GameStatDelta`/`StatEntry`, `GameRuleSnapshot`, `LegalActionSet`, `CatcherExposure`, `BatteryExposure`, plus events/facts. Decisions in `docs/decisions/ADR-003.md`; `docs/SYSTEM_CONTRACTS.md` C07/C08/C11 got implementation-status notes.

**2-C Reducer + scorer.** Pure, input-driven `GameReducer` (`Start`, `Apply`, `LegalActions`, `Finalize`), `EarnedRuns`, `BoxScoreBuilder` in `src/StarPlayer.Simulation/Baseball/`. Covers balls/strikes/fouls/K/BB/HBP, dropped third strike, 1B–HR, ground/fly/line/bunt outs, sacrifices, errors, FC, DP/TP, force/tag, steal/CS/pickoff/balk, WP and PB kept separate, runner cause tracking, inning change, walk-off, home-skips-last-half, extra innings/ties per profile, legal substitutions with DH/no-DH/two-way, inherited runners and per-pitcher responsibility, earned-run reconstruction, W/L/S, catcher participation and battery exposure. D1: one approach per PA; `Take` consumes exactly one real pitch and resumes at the exact count/runners (`PaStage.Decision`). Invalid plays return a stable-coded error and never change the input state; duplicate event IDs are no-ops; finalize runs once.

**2-D Records, scripted runner, UI.** Official records are projections of `StatEntry` rows (`BoxScoreBuilder`, `BoxScoreReconciler` with ≈25 identities). Scripted-game runner = `GameDiagnosticsService` (public views only, internal constructor). Godot QHD screen `경기 진단`: game list, scoreboard grid, inning/top-bottom, balls/strikes/outs, base diamond, runners, current batter/pitcher, "다음 사건 진행"/restart/run-to-end buttons, game-over result + box score, self-check panel. No manager/club features.

## 2. Main changed files (git working tree vs `64167f7`; nothing committed)

- Modified (15): `src/StarPlayer.Domain/Players/{Ratings,Proficiency,PlayerStatus,DevelopmentProfile,Player}.cs`, `src/StarPlayer.Infrastructure/StarPlayer.Infrastructure.csproj`, `game/Bootstrap/{CompositionRoot,GameRoot,LaunchOptions}.cs`, `game/UI/MainMenu/MainMenu.{cs,tscn}`, `tests/StarPlayer.Application.Tests/{SourceScan,TrustedBoundaryTests}.cs`, `docs/SYSTEM_CONTRACTS.md`, `tools/verify.ps1`.
- New: `src/StarPlayer.Domain/Baseball/Game/` (4 files), `src/StarPlayer.Simulation/Baseball/` (7 files), `src/StarPlayer.Application/Projections/` (7 files) + `Ports/IScriptedGameSource.cs`, `src/StarPlayer.Infrastructure/Fixtures/{ScriptedGameJson,ScriptedGameSource}.cs`, `content/rules/game_rules_profiles.json`, `content/fictional/phase2_diagnostic_games.json`, `game/Components/BaseDiamond.cs`, `game/UI/GameDiagnostics/`, `tests/Fixtures/` (100-game JSON + generator), five Simulation test files, `ScriptedGameSuiteTests.cs`, `GameRulesProfileTests.cs`, `CollectionImmutabilityTests.cs`, `docs/game-engine.md`, `docs/decisions/ADR-003.md`, this report, `docs/reports/PHASE_2_VERIFY_LOG*.md`, `Star/01_Phase_Reviews/Phase_02_Review.md`.
- Also touched (docs only): `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_01_Review.md` (commit field corrected to the pushed SHA).
- No SQLite schema/migration, no new NuGet package, no change to `ROADMAP.md`.

## 3. GameRules applied and their basis

Immutable `GameRuleSnapshot` (SHA-256 content hash, recorded on each finished game). Items used: outs per half (3), balls for walk (4), strikes for strikeout (3), regulation innings (9), extra-innings limit/tie handling, designated hitter, two-way player rule, starter-win minimum outs (15), save lead/outs values.

| Profile | Status | Notes |
|---|---|---|
| `kbo-oriented-unverified-v0` | KBO-oriented, **unverified** | DH, two-way, extra-inning limit 11 then tie |
| `test-dh-cap11`, `test-no-dh-cap11`, `test-dh-unlimited`, `test-dh-cap10`, `test-dh-no-two-way` | **fictional test** | each isolates one rule-profile behaviour; labelled fictional in file, snapshot and UI |

Basis/provenance: no official KBO regulation text was supplied or located. The only evidence is press reports of a 2025-01-22 KBO board decision limiting regular-season extra innings to 11 (some headlines tie it to 2026). It is recorded as provenance, not certified fact. Open items are listed in the JSON profile and `docs/game-engine.md` §5. No unapproved value is claimed as an official 2026 KBO rule.

## 4. Independent scoring-oracle results

- Hand-computed oracles (expected values written by hand in the test, never read from the engine): Simulation tests, 99 passing — count and foul boundaries, dropped third strike, Take-then-resume, base states, all hit types, outs and credits, SF/SH, DP/TP, force/tag, FC, ROE, steal/CS/pickoff/balk, WP vs PB incl. earned-run effect, timing plays, third-out cancellation, walk-off variants, home-skip, extra innings/ties for each profile, substitutions/DH/two-way/no-DH, mid-inning catcher change, battery exposure once, inherited runners, earned/unearned reconstruction, decisions, duplicate event, double finalization, rejected-event non-mutation, determinism.
- Independent scorer: `tests/Fixtures/generator/generate_scripted_games.py` (Python, stdlib, fixed seeds) produces the games and their expected results.
- **Mutation check (manual, one-off; script not kept in the repo).** Thirteen deliberate engine mistakes (M1–M13; M5 and M10/M11 were first tried as constant-false edits that did not compile and were redone) were applied one at a time and the Simulation and Infrastructure suites run; each was reverted afterwards (final suites re-ran green):

| Mutation | Caught by |
|---|---|
| RBI credited on a double play (M1) | **initially not caught** → added hand oracle `A_run_that_scores_on_a_ground_ball_double_play…`; now caught (Simulation) |
| Foul with two strikes adds a strike (M3) | Simulation 1, Infrastructure 1 |
| Passed-ball runner advances in earned-run replay (M4) | Simulation 1, Infrastructure 2 |
| Runs allowed after the winning run (M5) | Simulation 1 (100-game suite does not exercise it) |
| Batter-out third out still lets runs score (M9) | Simulation 1 (100-game suite does not exercise it) |
| Duplicate event ID no longer idempotent (M10) | Simulation 1, Infrastructure 1 |
| Finalize recounts every call (M11) | Simulation 2, Infrastructure 1 |
| Errors ignored in earned-run replay (M12) | Simulation 3, Infrastructure 2 |
| Catcher outs not counted (M7) | Simulation 2, Infrastructure 2 |
| Wild pitch charged to the wrong stat (M8) | Simulation 3, Infrastructure 2 |
| Battery exposure counted per event (M13) | Simulation 2, Infrastructure 2 |

  Eleven were caught (table). Two (M2, M6) survived because the guarded code is redundant by analysis (not proven by a test), not because tests are missing: M2 (sacrifice-fly "fewer than two outs" check — a two-out fly out is the third out, already rejected by the third-out rule) and M6 (save "lead held" check — a lead lost after the finisher entered and retaken makes the finisher the winning pitcher, so he is excluded earlier). Both guards are kept as defence in depth.
- **Limit (honest):** generator and engine share an author and a reading of the spec; agreement shows consistent implementation of the documented conventions, not equality with official KBO scoring. The 100-game suite does not contain every rare case (M1/M5/M9 show this); those are covered by hand oracles.

## 5. 100-game results

100 complete scripted games (31,582 events: 16,135 pitches, 7,636 approaches, 5,566 balls in play, 970 takes, 641 runner plays, 634 substitutions) over six profiles (DH cap11 ×40, no-DH ×15, unlimited ×15, cap10 ×10, no-two-way ×10, KBO-oriented unverified ×10). Outcomes: 53 away wins, 41 home wins, 6 ties; 17 games reached extra innings. Every game replays through the real reducer and is compared with the generated expectation on final score, hits/errors, line score, LOB, per-player batting/pitching/fielding keys, outs/innings, PA/AB, catcher and battery exposure, decisions and pitcher earned/unearned: **100/100 match, 0 reconciliation problems** (`ScriptedGameSuiteTests`, 8 tests, including tamper tests that prove the comparison fails when an expected value is changed, per-event invariants (no negative outs, no duplicate base occupancy, every run has a ScoredPlay) and finalize-once). This is a scripted-fixture integration result; no stochastic claim and no 100,000-PA calibration (Phase 3).

## 6. Full test and regression results

`dotnet test … -c Release --no-build` inside `tools/verify.ps1`, both final runs identical: Domain **145**, Simulation **99**, Application **114** (incl. architecture 14, determinism 48), Infrastructure **56** = **414 passed, 0 failed, 0 skipped** (Phase 1 baseline 289: 138/1/112/38; +125 Phase 2 tests). Release build 0 warnings/0 errors. All Phase 0–1 checks (T1–T3, B1–B3, G1–G3, E1–E3) remain PASS. Exit code of both runs 0.

## 7. Windows export and UI verification

- G4 (headless `--game-diagnostics`): all 6 embedded games stepped event-by-event to the end through the screen presenter, each box score equals its independent expected result, self-check 7/7, marker `STARPLAYER_GAME_DIAGNOSTICS=PASS`.
- E1/E1b: fresh `ExportRelease` Windows export, DLL hash identical to publish output and different from Debug. E4: cold copy of the exported program at 2560×1440 runs `--game-diagnostics` PASS (7/7) and writes the mid-game and finished-game screenshots (`build/verify/exported-game-screen*.png`, not committed). The finished-game screenshot of the repo run (`build/tmp/game-screen-end.png`) was inspected: scoreboard (11 innings, R/H/E), diamond, count/outs, result, box-score summary and self-check list all readable at QHD.
- Only QHD was verified; FHD is Phase 5+. The automated launch modes do not click like a human; **no manual human click-through of the new screen has been done** (`미검증`, owner check suggested).

## 8. Unresolved rule and technical issues

1. **D2 GameRules production subset not certified (blocks the Gate item → INCOMPLETE).** Needs an official source and effective date for: regular-season extra-innings limit 11 and tie handling; KBO use of DH and any two-way rule; official scoring rules for win/save/RBI/earned runs/timing plays/relief walk charging. Provided material did not contain them. Profile stays `kbo-oriented-unverified-v0`.
2. Documented simplifications (RBI, earned-run replay convention, W/L/S, scorer-flag judgements) are not official rules; certification belongs with item 1.
3. Not modelled: pitch clock, check-swing review, infield fly, interference, appeals, rain-shortened/suspended games, discipline.
4. `ApproachKind` is recorded and has no effect until Phase 3.
5. Persistence of `game_states`/`scored_plays` is a contract only (fixture JSON); no SQLite mapping/migration until Phase 4.
6. Known test limit: the 100-game suite does not hit every rare rule (M1/M5/M9); hand oracles cover them.
7. Diagnostic screen is not final UX; Korean text relies on system-font fallback (unchanged from Phase 0).

## 9. Phase 2 Gate verdict

Common Gate G items 1–4 and 6: PASS with evidence above (tests, regression, no storage change so G3 not applicable, QHD scene, reports/state docs). Phase 2 acceptance rows: 100 scripted games reconcile — PASS; no negative outs/duplicate bases — PASS; real count and one-pitch continuation fixtures — PASS; each supported rule variant has a named fixture — PASS; catcher/WP/PB/battery rows — PASS; **"D2 GameRules subset/simplifications are recorded" — PASS as recorded; "production subset certified" — INCOMPLETE** (item 1 above). Overall: **INCOMPLETE pending owner/GPT Chat decision** on whether the recorded-and-labelled unverified profile satisfies the gate or an evidence-backed KBO source must be supplied first. We do not mark it PASS.

## 10. Parts needing GPT Chat and Codex review

- GPT Chat: whether the labelled-unverified D2 profile is acceptable for the gate; the scoring conventions in `docs/game-engine.md` §4/§6 (RBI, earned-run replay, decisions) against the source documents; scope check that nothing beyond Phase 2 was added.
- Codex (recommended — core simulation rules and broad multi-module change): `EarnedRuns` and `FinishPlay` rule logic (third-out/timing/walk-off interplay), `ValidatePlans` legality, substitution/two-way handling, immutability of `GameState`, determinism/idempotency claims, the internal-visibility boundary for the new Application/Infrastructure types, and independence of the Python oracle.

Phase 2 final approval is requested. Phase 3 is not started and is not authorized by this report.
