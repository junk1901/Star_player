# Phase 3-C1 Report — Pitch location on events, `ef2` and event JSON format 2

**Approval update (2026-10-11, owner `TASK.md`): the 3-C1 package is approved — GPT Chat `PASS WITH NOTES` / final approval, Codex final focused re-review `PASS WITH NOTES` (see Part I). Everything below is the history of the rounds; where it says `검토 대기`, "not approved", "Codex review `미실시`" or "nothing committed", that was the status at that round and is superseded by Part I. Phase 3 Gate stays INCOMPLETE.**

Status (original, superseded): **implemented and self-tested; review status `검토 대기`** (not approved). Scope: internal work package 3-C1 of Phase 3 only (owner instruction `TASK.md`, 2026-10-11, base commit `ddbc999c4c8640dd22b458062e98c0bf7fbe46f6`). 3-C2, 3-C3 and Phase 4 are not started. Phase 3 Gate: INCOMPLETE. GPT Chat review `미실시`; independent Codex review of the 3-C1 code `미실시` (Codex reviewed the 3-C *architecture*: `READY WITH NOTES`). Nothing was committed or pushed (`TASK.md` forbids it).

## 1. Event location payload

- `PitchSpec` and `InPlayEvent` end with an optional `PitchLocation? Location = null` ([GameEvents.cs](../../src/StarPlayer.Domain/Baseball/Game/GameEvents.cs)). Existing parameters, order and defaults are untouched; every existing call site compiles unchanged.
- `PitchEvent` and `TakeEvent` carry it through `PitchSpec`; `InPlayEvent` carries it as its own single pitch (no `PitchEvent` precedes it; reducer counting unchanged — test).
- `PitchLocation` (3-B1) is reused: its factory already checks model version, zone id, defined side and that the boundary class is the zone's class. It has no public constructor and no init setters. `TargetZone` is a different type.
- `GameEventLocation.Of(event)` (Domain) is the single accessor for the location of any event (null for non-pitch events).
- The reducer does not demand a location in 3-C1: Phase 2 events have none and must replay unchanged. "Every new actual pitch has a location" is enforced where new data enters: the format-2 reader/writer (below) and the 3-C2 resolver.

## 2. `ef1` / `ef2` selection and canonical rule

- `EventFingerprint.Version` is still `"ef1"`; new `VersionV2 = "ef2"`; `VersionOf(event)` = `ef2` iff the event carries a location (`PitchEvent`, `TakeEvent`, `InPlayEvent`), otherwise `ef1` (legacy pitches, `ApproachEvent`, `RunnerPlayEvent`, `SubstitutionEvent`).
- `ef2|kind|<ef1 field list unchanged>|zone_model=..|zone=..|side=..|boundary=..`; same escaping, enum names, set rules; hashed with the same SHA-256. Example (literal in tests): `ef2|pitch|spec={Ball;None;[];None;~}|zone_model=pzl-zones-v1|zone=in.r2c2|side=Right|boundary=Clear` → `f917cb4e…93a4f5e8`; the in-play literal hash `2442ffd0…f387a`. Both SHA-256 values were computed with Python, independently of the C# code.
- `ef1` text, hash, receipts, duplicate/conflict behaviour and golden vectors are unchanged (section 5).

## 3. Receipt comparison

`GameReducer.Apply` was **not modified**: it already compares the receipt version, then kind and fingerprint. Tested (`PitchLocationEventTests`):

| Scenario | Result |
|---|---|
| `ef1` event re-delivered unchanged | duplicate (same state object) |
| same id, `ef1` pitch / in-play re-delivered with a location added | `Game.event_fingerprint_version_mismatch`, state unchanged |
| `ef2` event re-delivered unchanged (pitch, take, in-play) | duplicate, same state object |
| `ef2` with another zone / other side / other zone + same side (all three kinds) | `Game.event_id_conflict` |
| `ef2` re-delivered with its location removed (pitch, in-play) | `Game.event_fingerprint_version_mismatch` |
| same id, other pitch outcome / mishap / other event kind, equal location | `Game.event_id_conflict` |
| boundary class or model version inconsistent with the zone | cannot be constructed: `PitchLocation.boundary_mismatch`, `ZoneModelVersion.unsupported`, `PitchZoneId.unknown`, `…side_invalid`, `…boundary_invalid` |
| ef1 and ef2 receipts in one game | kept in order; replay gives equal receipts; every event is then a duplicate |

## 4. JSON: legacy format 1 and event format 2

Implemented in [ScriptedGameJson.cs](../../src/StarPlayer.Infrastructure/Fixtures/ScriptedGameJson.cs); table in `docs/game-engine.md` §20.

- Format 2 envelope `{"format_version": 2, "events": [...]}` through `ReadEventsV2` / `WriteEventsV2` and its own DTO (`EventV2Dto` extends the legacy `EventDto` — unsealed for this — with `location`). Required on `pitch`/`take`/`in_play` (`Fixture.location_missing` for absent or `null`), refused elsewhere (`Fixture.location_unexpected`); bad model / zone / side / boundary text / class → typed error, never corrected; missing or unknown location members → `Fixture.json_invalid`.
- Legacy reader: a `location` member is still an unknown member; a version-2 envelope is `Fixture.version_unsupported`; it never creates a location. `format_version: 1` scripted games are untouched.
- Legacy writer: refuses a located event (`Fixture.location_requires_v2`, naming the event) rather than dropping the location; `WriteEventsV2` refuses a pitch-bearing event without a location.
- `EventReceiptJson` already stores a fingerprint version per receipt; a mixed ef1/ef2 list round-trips (test).
- Tests (`EventJsonV2Tests`, 16): round trip with equal receipts and an identical second write; a v2 game replays to the same state and every event is a duplicate afterwards; every rejection above; legacy reader/writer behaviour.

## 5. Existing contracts unchanged — evidence

- Two literals in `Ef1CorpusPinTests` were computed on the **unchanged commit `ddbc999`** (a git worktree of HEAD, the test file copied in, run once, the values pasted into the repository version): SHA-256 over the (game, event id, kind, version, fingerprint) of all **30,974** events of the 100 Phase 2 games = `417a91f0…cc375`, and SHA-256 over the bytes of `WriteEvents` of those games = `e0840016…f19ffe`. The same tests pass on the working tree.
- `EventIdentityTests` literal `ef1` text and hash (`c2693b1f…`, `862f90f3…`) pass unchanged; the 111 Randomness tests and RNG golden vectors, `GameRules` v1/v2 hashes (Phase 3-B1) and the 3-B2 tuning tests all pass.
- Not touched: `GameReducer*`, `GameEventIds`, `Randomness/`, `GameRules*`, rule/tuning JSON.

## 6. Tests (actual results)

Environment: Windows 11 Home 10.0.26200, .NET SDK 8.0.425, `dotnet test StarPlayer.sln -c Release`.

| Project | Before (3-B2) | After | New |
|---|---:|---:|---:|
| Domain.Tests | 263 | 263 | 0 |
| Simulation.Tests | 371 | 383 | +12 (`PitchLocationEventTests`) |
| Application.Tests | 117 | 117 | 0 |
| Infrastructure.Tests | 204 | 222 | +18 (`EventJsonV2Tests` 16, `Ef1CorpusPinTests` 2) |
| **Total** | **955** | **985** | **+30**, 0 failed, 0 skipped |

One existing test was extended, no expectation of an `ef1` value changed: `EventIdentityTests.Every_constructor_parameter_of_every_event_changes_the_fingerprint` gained a `Location` variant for `PitchEvent`, `TakeEvent` and `InPlayEvent`, and its guard on the number of payload parameters of `InPlayEvent` went from 7 to 8 (the guard exists to fail when a parameter is added without being covered).

### Mutation checks (7, each applied to the source, the relevant tests run, then reverted)

| Mutation | Detected by |
|---|---|
| `ef2` canonical omits the applied side | 4 Simulation tests |
| version choice ignores the location (always `ef1`) | 7 Simulation + 3 Infrastructure tests |
| legacy writer no longer refuses a located event | 1 Infrastructure test |
| v2 reader accepts a missing location | 7 Infrastructure tests |
| v2 reader rebuilds the boundary instead of checking it | 1 Infrastructure test |
| `TakeEvent` location ignored by `GameEventLocation.Of` | 8 Simulation + 14 Infrastructure tests |
| `InPlayEvent` location ignored by `GameEventLocation.Of` | 11 Simulation + 14 Infrastructure tests |

All 7 were detected (the helper script printed "SURVIVED" for each because it parsed the Korean console summary wrongly; the failing-test counts above come from the same output and the source was restored afterwards — the full suite above ran on the restored source). **RED/GREEN note:** the implementation was written before most of the new tests, so there is no RED log for them; the mutation checks and the HEAD-computed digests are the substitute evidence.

## 7. `tools/verify.ps1`

Two consecutive runs after all code and documents were final: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify.ps1 -LogName PHASE_3C1_VERIFY_LOG_run1.md` (16:58 +09:00) and `... _run2.md` (17:01 +09:00): both **`GATE: PASS`**, exit 0 ([run 1](PHASE_3C1_VERIFY_LOG_run1.md), [run 2](PHASE_3C1_VERIFY_LOG_run2.md)). Release unit tests inside the script: Domain 263, Simulation 383, Application 117 (architecture 14, determinism detector 48), Infrastructure 222; Godot headless checks, Windows export, exported-program QHD checks and the markdown link check (76 files) all PASS. (State documents were filled in with these results after the runs; no link or code changed afterwards.)

**Trailing whitespace.** `git diff --check` on the tracked changes: clean (exit 0). The two new verify logs have the same CRLF + lone-CR property as the earlier logs (git classifies them `-text`, so `git diff --cached --check` will count the CR of each line as trailing whitespace once they are staged); this is generated evidence, not a content defect, and is the same pattern as the committed 3-B1/3-B2 logs. All other new files were checked for real trailing spaces/tabs: none.

## 8. Changed files and Git state

Base commit `ddbc999c4c8640dd22b458062e98c0bf7fbe46f6` (3-B2), branch `main`. **Nothing is committed or pushed** (`TASK.md`). Untouched untracked files kept: `TASK.md`, `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf`.

Modified (10): `src/StarPlayer.Domain/Baseball/Game/GameEvents.cs` (Location parameters, `GameEventLocation`), `src/StarPlayer.Simulation/Baseball/EventFingerprint.cs` (`ef2`), `src/StarPlayer.Infrastructure/Fixtures/ScriptedGameJson.cs` (format 2 reader/writer, legacy writer guard, `EventDto` unsealed), `tests/StarPlayer.Simulation.Tests/EventIdentityTests.cs` (Location variants, parameter-count guard), `ROADMAP.md` (one sentence corrected, §1.14), `docs/SYSTEM_CONTRACTS.md` (C08 sentence corrected, 3-C1 status line), `docs/game-engine.md` (§3a paragraph, new §20), `CURRENT_STATE.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/00_Dashboard.md`.

New (5 files + 2 verify logs): `docs/decisions/ADR-007.md`, `docs/reports/PHASE_3C1_REPORT.md`, `docs/reports/PHASE_3C1_VERIFY_LOG_run1.md`, `..._run2.md`, `tests/StarPlayer.Simulation.Tests/PitchLocationEventTests.cs`, `tests/StarPlayer.Infrastructure.Tests/EventJsonV2Tests.cs`, `tests/StarPlayer.Infrastructure.Tests/Ef1CorpusPinTests.cs`.

Not touched: `GameReducer*`, `GameEventIds`, `Randomness/`, `GameRules*`, `content/`, project files, the 3-B2 probability code, existing ADR-004…006.

## 9. Handoff to 3-C2 / 3-C3 and open points

Recorded in ADR-007 and `docs/game-engine.md` §20; nothing is resolved here.

- 3-C2: `target_zone` realisation (recommended own stream `game/target@1`, an ADR-004 amendment with a golden vector), the `game/location@1` draw before the outcome, `NextBounded` call count vs raw draws, explicit-`Take` hit-by-pitch policy, switch-hitter side rule, validating an event's side against the batter on the card.
- 3-C3: event-id sequence + reducer result + receipts + RNG cursors as one success unit, commit before publication; Phase 4: SQLite transaction and expected RNG hash.
- The reducer accepts a located or unlocated pitch alike; a *new* game that omits locations is caught only by the format-2 writer/reader and (later) the resolver. A reducer-level "game uses format 2" mode was considered and deferred to 3-C2/3-C3, where the resolver exists.
- Older open NOTES (3-D tendency wiring, park `Hits` decision, 3-E Framing, 3-F statistics, probability-table adjustment history, limits of independent verification) are unchanged.

## 10. Self-assessment

**PASS WITH NOTES** for the 3-C1 work package: every requested payload, fingerprint, receipt, JSON and writer behaviour is implemented and tested, legacy bytes are pinned to the pre-change commit, and all executed checks pass. Notes: no RED log, no independent Codex review of the code, the reducer-level requirement of a location for new data is deferred (above). Not a Phase 3 Gate result and not an approval.

---

## Part G — Fix round after the independent Codex review (2026-10-11, `TASK.md`)

**Codex verdict on the 3-C1 code: `CHANGES REQUIRED` (Medium 1).** This part supersedes the "Codex review of the code `미실시`" wording above and the test counts (now 997). Review status stays `검토 대기`; final 3-C1 approval is pending; a Codex re-review of this fix is `미실시`. The earlier parts (including the missing RED log and the un-reviewed raw mutation logs) are kept as written.

**G.1 Defect and cause.** `ReadEventsV2` threw instead of returning an error for `{"format_version":2,"events":null}` (`ArgumentNullException`) and `{"format_version":2,"events":[null]}` (`NullReferenceException`). Cause: `required` on a DTO member only demands presence; an explicit JSON `null` satisfies it, and the reader then dereferenced the null list / element. The catch block only handles `GameJsonException` and `JsonException`, so the exceptions escaped to the caller.

**G.2 Fix** ([ScriptedGameJson.cs](../../src/StarPlayer.Infrastructure/Fixtures/ScriptedGameJson.cs), `ReadEventsV2` only): after deserialisation the list is checked for null, then the events are built in an indexed loop that checks each element for null before `ToEventV2`. Either null throws the existing internal `GameJsonException("Fixture.field_missing", ...)` that the existing catch turns into a `Result.Failure`, with the path in the message (`events` / `events[i]`, the exact index). No broad `catch`, no null-to-empty replacement, no `Where(x => x != null)`, no partial result (the list is only returned after every element was built), no schema change. `Fixture.field_missing` is the code `TuningProfileJson` already uses for the same situation.

**G.3 Final error codes.**

| Input | Result |
|---|---|
| `events: null` | `Fixture.field_missing`, message names `events` |
| `events: [null]`, `[null, valid]`, `[valid, null]`, `[valid, valid, null, valid]` | `Fixture.field_missing`, message names `events[0]` / `events[0]` / `events[1]` / `events[2]`; whole read fails |
| `events` member missing | `Fixture.json_invalid` (unchanged) |
| `events` of wrong type (`5`, `"x"`, `{}`), element of wrong type (`5`, `"x"`) | `Fixture.json_invalid` (unchanged) |
| valid array / empty array | success, as before |

**G.4 RED/GREEN.** Regression tests (+12 test cases in `EventJsonV2Tests`, 4 test methods) were written first and run against the unfixed code: **RED = 5 failed / 23 passed** (the five null cases above threw `ArgumentNullException` / `NullReferenceException`); after the fix **GREEN = 28/28**. Summary: [PHASE_3C1_FIX_RED_EVIDENCE.md](PHASE_3C1_FIX_RED_EVIDENCE.md) (the raw console output was not saved).

**G.5 Compatibility (executed).** `Ef1CorpusPinTests` (30,974-receipt digest and legacy `WriteEvents` bytes, computed on `ddbc999`), `EventIdentityTests` (`ef1` literal), `PitchLocationEventTests` (`ef2` literals, receipt conflicts), `ReplayContractTests`, and the rest of `EventJsonV2Tests` all pass: `ef1`/`ef2` text and hashes, `Location` contract, `GameReducer`, receipt checks, legacy `ReadEvents`/`WriteEvents`, the legacy writer's refusal of a located event, the v2 location checks and the RNG / `GameRules` golden vectors are unchanged. Valid inputs read exactly as before.

**G.6 Tests (actual).** `dotnet test StarPlayer.sln -c Release`: Domain 263, Simulation 383, Application 117, Infrastructure 234 = **997 passed** (base 985, +12), 0 failed, 0 skipped.

**G.7 `tools/verify.ps1`.** Two consecutive runs after all code and documents were final: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify.ps1 -LogName PHASE_3C1_FIX_VERIFY_LOG_run1.md` (17:29 +09:00) and `..._run2.md` (17:32 +09:00): both **`GATE: PASS`**, exit 0 ([run 1](PHASE_3C1_FIX_VERIFY_LOG_run1.md), [run 2](PHASE_3C1_FIX_VERIFY_LOG_run2.md)). Release unit tests inside the script: Domain 263, Simulation 383, Application 117 (architecture 14, determinism detector 48), Infrastructure 234; Godot headless checks, Windows export, exported-program QHD checks and the markdown link check (80 files) PASS. These two runs were executed in this session (not taken from earlier logs). The state documents were filled in with these results afterwards; no code or link changed. The two new logs carry the same CRLF + lone-CR property as earlier verify logs (git `-text`; `git diff --cached --check` will flag every line once staged).

**G.8 Files.** Modified: `src/StarPlayer.Infrastructure/Fixtures/ScriptedGameJson.cs` (`ReadEventsV2`), `tests/StarPlayer.Infrastructure.Tests/EventJsonV2Tests.cs` (new null section), `docs/decisions/ADR-007.md` (one note), `docs/reports/PHASE_3C1_REPORT.md`, `CURRENT_STATE.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/00_Dashboard.md`. New: `docs/reports/PHASE_3C1_FIX_RED_EVIDENCE.md`, `PHASE_3C1_FIX_VERIFY_LOG_run1.md`, `_run2.md`. Nothing committed or pushed; `TASK.md` and the two rule-book PDFs remain untracked and untouched.

**G.9 Remaining notes.**
- **(Resolved in Part H.)** ~~Same class of defect, not fixed (out of this fix's scope, found while probing):~~ an explicit `null` *inside* an event's own lists (`"moves": [null]`, `"strikeout_credits": [null]`; probably also `credits`/`errors`/`batter_credits` elements) still ends in a `NullReferenceException` in `ReadEventsV2`, and the same holds for the legacy `ReadEvents` and the scripted-game reader (those paths predate 3-C1). Probe result: both probed inputs throw. Reported to the owner; needs its own instruction.
- Unchanged: no RED log for the original 3-C1 package; the raw logs of the 7 mutation checks were not independently reviewed; no Codex review of the code beyond the `CHANGES REQUIRED` finding; reducer-level location requirement deferred to 3-C2; 3-C2/3-C3 and Phase 4 open.

**G.10 Self-assessment: PASS WITH NOTES** for the fix round: the one Medium finding is reproduced RED, fixed and GREEN, nothing else changed, all executed checks pass. Not a Phase 3 Gate result and not an approval; Codex re-review `미실시`.

---

## Part H — Second fix round: null elements inside event lists (2026-10-11, `TASK.md`, after the GPT Chat review `CHANGES REQUIRED`)

Review status stays `검토 대기`; final 3-C1 approval pending; Codex re-review `미실시`. Part G's earlier `events: null` / `events: [null]` fix stays in force and its note "same class of defect, not fixed" (G.9) is resolved by this part; the probe in G.9 that found it only exercised `moves: [null]` and `strikeout_credits: [null]` on `ReadEventsV2`.

**H.1 Defect and what was directly reproduced.** An explicit JSON `null` as an element of `moves`, `strikeout_credits`, `batter_credits`, a move's `credits`, or as an element of an `events` list threw `NullReferenceException`; `errors: [null]` returned `Fixture.enum_invalid` without a path. Reproduced by running inputs (not inferred) on **all three** event readers — legacy `ReadEvents`, `ReadEventsV2` and the scripted-game reader — for every list listed above, for a null in the middle of a list, for a scenario's `events: null` and `events: [null]`. Not examined (out of scope): lineup/bench/expected and every non-event JSON converter.

**H.2 Fix** (shared converters in [ScriptedGameJson.cs](../../src/StarPlayer.Infrastructure/Fixtures/ScriptedGameJson.cs), no change to `EventFingerprint`, `GameEvents`, `GameReducer`, RNG or the probability model): new `Item<T>(item, path)` rejects a null element with `GameJsonException("Fixture.field_missing", "...(null): <path>")`; `Credits` (now takes the field name), `Moves` (passes `moves[i].credits` to `Credits`), new `Errors`, and a shared `Events(list, path, make)` used by all three readers (it also holds the list-null check that Part G had inline in `ReadEventsV2`). The existing catch blocks turn the exception into `Result.Failure`; no broad `catch`, no filtering, no null-to-empty replacement, no partial result. A list that is itself null is still accepted where it was optional; `strikeout_credits: null` stays `null` (= default catcher putout), `[]` stays empty.

**H.3 Error codes and paths (executed).**

| Input | Code | Path in the message |
|---|---|---|
| `moves: [null]` | `Fixture.field_missing` | `events[0].moves[0]` |
| `strikeout_credits: [null]` | `Fixture.field_missing` | `events[0].strikeout_credits[0]` |
| `batter_credits: [null]` | `Fixture.field_missing` | `events[0].batter_credits[0]` |
| `errors: [null]` | `Fixture.field_missing` (was `Fixture.enum_invalid`) | `events[0].errors[0]` |
| move `credits: [null]` | `Fixture.field_missing` | `events[0].moves[1].credits[0]` |
| null in the middle of a list | `Fixture.field_missing`; the whole input fails | exact index (`moves[1]`, `batter_credits[2]`, `errors[1]`) |
| scripted game | same codes | prefixed with the game id: `g001.events[0].moves[0]` ... |
| scripted game `events: null`; `events: [.., null]` | `Fixture.field_missing` | `g001.events` / `g001.events[i]` |
| legacy array `[valid, null]` | `Fixture.field_missing` | `events[1]` |
| wrong JSON types, missing required members, `events` missing | `Fixture.json_invalid` | unchanged |

**H.4 RED/GREEN.** New `EventJsonNestedNullTests` (24 test cases) written first: **RED = 19 failed / 5 passed** (the 5 are compatibility guards); after the fix **GREEN = 24/24**. Summary [PHASE_3C1_FIX2_RED_EVIDENCE.md](PHASE_3C1_FIX2_RED_EVIDENCE.md); the raw RED console output is in `docs/reports/PHASE_3C1_FIX2_RED.log` (git-ignored by the existing `*.log` policy). 6 new mutation checks all detected (table in the summary). The earlier notes stay: no RED log for the original 3-C1 package; the raw logs of the first 7 mutation checks were not independently reviewed.

**H.5 Compatibility (executed).** `Ef1CorpusPinTests` (30,974-receipt digest, legacy `WriteEvents` bytes), `EventIdentityTests`, `PitchLocationEventTests` (ef2 literals/hashes, conflicts, in-play = one pitch), `ReplayContractTests` (100-game replay, JSON round trips), `EventJsonV2Tests` (location round trip, required location, legacy writer refusing a located event) and the RNG / `GameRules` golden vectors all pass unchanged; valid inputs read exactly as before.

**H.6 Tests (actual).** `dotnet test StarPlayer.sln -c Release`: Domain 263, Simulation 383, Application 117, Infrastructure 258 = **1021 passed** (997 + 24), 0 failed, 0 skipped.

**H.7 `tools/verify.ps1`.** Two consecutive runs after all code and documents were final: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify.ps1 -LogName PHASE_3C1_FIX2_VERIFY_LOG_run1.md` (17:41 +09:00) and `..._run2.md` (17:44 +09:00): both **`GATE: PASS`**, exit 0 ([run 1](PHASE_3C1_FIX2_VERIFY_LOG_run1.md), [run 2](PHASE_3C1_FIX2_VERIFY_LOG_run2.md)). Release unit tests inside the script: Domain 263, Simulation 383, Application 117 (architecture 14, determinism detector 48), Infrastructure 258; Godot headless checks, Windows export, exported-program QHD checks and the markdown link check (83 files) PASS. These two runs were executed in this session. The state documents were filled in with these results afterwards; no code or link changed. The two new logs have the same CRLF + lone-CR property as earlier verify logs (git `-text`; `git diff --cached --check` will flag every line once staged).

**H.8 Files.** Modified: `src/StarPlayer.Infrastructure/Fixtures/ScriptedGameJson.cs`, `docs/decisions/ADR-007.md`, this report, `CURRENT_STATE.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/00_Dashboard.md`. New: `tests/StarPlayer.Infrastructure.Tests/EventJsonNestedNullTests.cs`, `docs/reports/PHASE_3C1_FIX2_RED_EVIDENCE.md`, `PHASE_3C1_FIX2_VERIFY_LOG_run1.md`, `_run2.md` (and the git-ignored `PHASE_3C1_FIX2_RED.log`). Nothing committed or pushed; `TASK.md` and the two rule-book PDFs remain untracked and untouched.

**H.9 Remaining notes.** Null members elsewhere in scenario files (lineup, bench, `expected`) and in other JSON converters (rule profiles, player packs, tuning) were not examined here and may share the pattern (the tuning loader has its own null checks since 3-B2). Reducer-level location requirement still deferred to 3-C2. Codex re-review `미실시`; 3-C2/3-C3 and Phase 4 not started; Phase 3 Gate INCOMPLETE.

**H.10 Self-assessment: PASS WITH NOTES** for this fix round: the reported defect is reproduced RED on all three readers, fixed in the shared converters, GREEN, with no other contract changed and all executed checks passing. Not a Phase 3 Gate result and not an approval.

---

## Part I — Final approval of the 3-C1 package (2026-10-11, `TASK.md`)

**I.1 Verdicts.** GPT Chat: `PASS WITH NOTES` / final approval. Codex final focused re-review: `PASS WITH NOTES`. The existing Medium (`ReadEventsV2` explicit null) and the additional nested-collection null defect are resolved; no new Critical/High/Medium defect. The earlier `CHANGES REQUIRED` verdicts and the fix history (Parts G, H) are kept unchanged. The approval applies to the 3-C1 work package only; it is not a Phase 3 Gate result, and 3-C2/3-C3 are not started. ADR-007 is Accepted for 3-C1 only.

**I.2 Evidence recorded by the reviewers.** Full xUnit 1,021 PASS (run by Claude); Codex ran directly Infrastructure 61 and Simulation 22 = 83 PASS; `tools/verify.ps1` two consecutive `GATE: PASS` (run by Claude, logs checked by Codex); the SHA-256 over all 30,974 `ef1` receipts and the legacy `WriteEvents` bytes unchanged from `ddbc999`; new `ef2` fingerprints, receipt conflicts and JSON v2 compatibility confirmed. This session re-ran no test for the approval bookkeeping; the figures are those of Parts G/H and the reviewers.

**I.3 Compatibility record.** `ef1`: text, hash and receipts unchanged, pinned by `Ef1CorpusPinTests`. `ef2`: only events that carry a location; canonical text = `ef1` field list + `zone_model|zone|side|boundary`. JSON: legacy bare array / format 1 unchanged (its writer refuses a located event); `format_version: 2` requires a valid location on `pitch`/`take`/`in_play`.

**I.4 Notes kept open (nothing is marked done that is not).**
- No RED log for the original 3-C1 package; the raw output of the first mutation checks (and the later ones) cannot be independently verified.
- Validation of JSON input outside events (lineup, bench, `expected`, other converters) is a separate stabilisation task.
- 3-C2: `game/target@1` and target-zone choice; mandatory actual pitch location and batter-side validation; `NextBounded` call count vs `Draws`; explicit `Take` and switch-hitter policy.
- 3-C3: joint atomicity of GameState, RNG, event ids and receipts. Phase 4: DB transaction and recovery.

**I.5 Git.** Committed as one commit (`feat: add phase 3c1 located events and ef2 contracts`) on top of `ddbc999`; the SHA is in `git log` and the owner report. Verify logs are generated evidence with CRLF + lone-CR (git `-text`), so `git diff --cached --check` flags them; this is the known exception, not a content defect. `TASK.md` and the two rule-book PDFs are excluded and stay untracked; raw `*.log` files stay git-ignored.
