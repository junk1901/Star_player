# Phase 3-A Report — Seeded RNG foundation

**Final approval (2026-10-11): Phase 3-A RNG `PASS WITH NOTES` / approved by GPT Chat; Codex final focused re-review `PASS WITH NOTES`. Full xUnit 709 PASS (run by Claude); Randomness 111 PASS (independently re-run by Codex); `tools/verify.ps1` two consecutive `GATE: PASS` (run by Claude, logs checked by Codex). Phase 3 overall Gate: INCOMPLETE. Phase 3-B: not started. The last non-blocking note (a stale `GameRandomSnapshot.cs` comment on which streams a snapshot contains) was fixed as a comment only; no RNG behaviour or format changed. Committed as `feat: implement deterministic phase 3a rng`.** The text below is the history of the earlier rounds and is kept.

Status (at the time of the earlier rounds): **implemented and self-verified; review status `검토 대기`** (not approved). Scope: work package 3-A of Phase 3 only (ROADMAP Phase 3, "Internal work packages"). **The Phase 3 Gate is not evaluated and not claimed**: 3-B to 3-F and Phase 4 are not started. Nothing is committed or pushed (base commit `b1e6b73ecf299147ab1b6b4dc2697ff463c66a1b`, working tree dirty). Instruction: `TASK.md`.

## Part F — Final fix round (2026-10-11, `TASK.md`: consolidated fix after two independent Codex reviews) — current state

Status: **implemented and self-verified; review status stays `검토 대기`** (not approved). Phase 3 Gate: **INCOMPLETE** (3-B to 3-F and Phase 4 not started). Nothing is committed or pushed. Scope: Phase 3-A RNG correctness and reproducibility only; no ID restructuring, probability model, persistence, Resolver, ABS, PZL probability, BCL or LMV was added. Parts of Part 1 below that Part F contradicts are superseded (Part 1 text is kept as history). Details and decisions: `docs/decisions/ADR-004.md` items 13-19; RED/GREEN evidence: `docs/reports/PHASE_3A_RED_EVIDENCE.md`.

**F.1 Were the earlier fix instructions already applied?** Checked first: no. Between the two reviews and this round no source file in `Randomness/` had changed (the first Codex review requested `Snapshot()` changes and the like; none was present), so all defects below were reproduced on the unfixed code before being fixed. Nothing was fixed twice.

**F.2 Defects, cause and fix**

| # | Defect (cause) | Fix | Evidence |
|---|---|---|---|
| A | A consumed stream deleted from a stored snapshot restores silently and restarts from its first value: `Restore` derives absent streams from the seed, and a hash computed from the truncated snapshot agrees with itself. | New loading contract `GameRandom.Restore(snapshot, expectedStateHash)`: the hash of the **live** state recorded at commit time is compared with the hash of the state actually rebuilt (`Rng.state_hash_mismatch`; malformed value `Rng.expected_hash_invalid`). The structure-only overload stays, documented as unable to detect omissions. The `rngset1` text and golden vectors are unchanged. The hash detects corruption and lost/substituted parts; it is **not** authentication. Phase 4 must write the hash in the same transaction as game state and receipts. | `SnapshotExpectationTests` (16); the hazard is kept as a permanent limit test; the Python-computed golden hash is accepted as an expectation |
| B (M1) | `Stream()` created a snapshot entry without a draw, so a read-only peek changed `StateHash`. | `Snapshot()` omits streams with zero draws (a stream is at its derived start until it draws); lookup never changes the hash; after the first draw the exact state is in the snapshot. | 4 RED tests, GREEN; location-peek hash equals untouched hash |
| C | `Atomically` could be nested, the original (or a stream obtained before the scope) could be drawn from during a scope, and a stream/copy escaping the scope could still draw. | While a scope runs the original is locked: draws on it or on pre-acquired streams, `Stream()`, and any nested scope (on the original or on the copy) throw `InvalidOperationException` before anything changes; `Snapshot()` and `Draws` stay readable (last committed state). The copy shares no mutable object with the original (cloned generators) and is closed when the scope ends, so escaped streams cannot draw. The lock is released in `finally` after success, failure, exception and cancellation. | 5 RED tests + guards (lock release after failure/exception/cancellation, ordinary re-run after a rejected nesting, no shared objects) |
| D | `Draws++` wrapped to a negative number at `long.MaxValue`; `Advance` reported a limit overrun as an argument error. | `NextUInt32` checks the limit before the state changes; `NextBounded` restores state and counter if the limit is reached while skipping rejected outputs; `Advance` checks input (`> long.MaxValue` is an argument error) and remaining allowance (`InvalidOperationException`) first. PCG outputs unchanged. | 5 RED tests incl. a rejection that crosses the limit and a stream restored at the limit |
| E | Non-canonical snapshots were silently re-sorted; unused entries accepted; number formatting relied on the default culture; report wording; thread claim. | Restore rejects wrong order (`Rng.stream_order_noncanonical`) and unused entries (`Rng.stream_unused`); `RngStreamId.Canonical` uses `InvariantCulture` (tested under ar-SA, fa-IR, th-TH, tr-TR; the output was already invariant, no RED possible); the rejection-heavy wording in Part 1 §3 is corrected; the classes document a **single serialized writer** and claim no multi-thread support. | 2 RED tests + 4 culture tests |

**F.3 Guarantee scope (what is and is not promised).** Snapshot: restores exactly what `Snapshot()` writes; the structure-only restore validates versions, canonical order, no unused/duplicate entries, increment, draw count and state/draw consistency; completeness is only checkable through the expected hash. `Atomically`: in-memory RNG state of one `GameRandom` only - it does **not** cover the event-id counter, `GameReducer`/`GameState`, receipts or external effects, and it publishes when the work returns, **not after a durable commit**. Threads: single serialized writer, concurrent use unsupported and undetected.

**F.4 Golden Vector / compatibility.** `generate_rng_golden.py` and `phase3a_rng_golden.json` are unchanged (no file touched); all 71 first-round tests - including every golden-vector test, `set_vector.canonical` and `state_hash` - pass unmodified. `rngset1`, `pcg32-xsh-rr-v1`, `kd1` unchanged. API: one addition (`Restore(snapshot, expectedStateHash)`); behaviour changes listed in ADR-004 item 19. No persisted rngset1 data exists.

**F.5 Tests and results**

| Command | Result |
|---|---|
| RED: `dotnet test tests\StarPlayer.Simulation.Tests --filter "FullyQualifiedName~RngHardeningTests"` on the unfixed code | 24 tests, **16 failed**, 8 passed (see RED evidence) |
| GREEN: `dotnet test tests\StarPlayer.Simulation.Tests --filter "FullyQualifiedName~Randomness"` | **111 passed**, 0 failed (71 first round + 24 `RngHardeningTests` + 16 `SnapshotExpectationTests`) |
| `dotnet test StarPlayer.sln -c Release` | Domain 145, Simulation 371, Application 114, Infrastructure 79 = **709 passed**, 0 failed, 0 skipped (Phase 2 baseline 598 unchanged and passing) |
| Mutation check of the new guards | 18 of 18 mutations detected (see RED evidence) |
| `git diff --check` | exit 0 (tracked changes only; the new files are untracked, see below) |
| `tools\verify.ps1` | see F.6 |

**F.6 `tools\verify.ps1`.** Two consecutive runs after all code and documents were final: `powershell -NoProfile -ExecutionPolicy Bypass -File toolserify.ps1 -LogName PHASE_3A_VERIFY_LOG_final1.md` and `... _final2.md` - both **`GATE: PASS`, exit 0** (2 min 44 s and 2 min 28 s): Release build of 9 projects with 0 warnings, Domain 145, Simulation 371, Application 114, architecture 14, determinism detector 48, Infrastructure 79, Godot headless checks, Windows export and cold-copy runs, relative doc links (59 and 60 files). Logs: `docs/reports/PHASE_3A_VERIFY_LOG_final1.md`, `docs/reports/PHASE_3A_VERIFY_LOG_final2.md`. The first-round logs (`PHASE_3A_VERIFY_LOG.md`, `PHASE_3A_VERIFY_LOG_run2.md`) are kept unchanged. Not executed: manual Godot UI clicks and FHD (no UI change), performance measurement, any Phase 3 Gate item (100,000 PA, 100 games, calibration), independent re-review of this round (`미실시`).

**F.7 Remaining notes.** (1) `kd1`/`rngset1` still await an independent format review (their text is this project's own definition). (2) The expected-hash contract exists only as an API: nothing stores or passes the hash until Phase 4. (3) Event-id counter atomicity, the one success unit of state/receipts/RNG, publishing only after a durable commit, the real `game/location` consumption order and probability scale, and `ef2`/replay are recorded as the Phase 3-C/4 handoff (ADR-004), not implemented. (4) The first Codex review's lower findings that were not requested (lone-surrogate behaviour of `Derive`, case sensitivity and the 72-character event id, a missing canonical-text parser) stay open as in the review. (5) The Phase 3 Gate, Pitch Zone Lite checks, 100,000-PA and 100-game criteria remain INCOMPLETE.

**F.8 Self-assessment: PASS WITH NOTES** for work package 3-A after the fix round (all requested defects reproduced and fixed, all executed checks pass; notes above). Not a Phase 3 Gate result and not an approval.


# Part 1 — First round (2026-10-11, superseded where Part F says so)

## 1. What was built (`src/StarPlayer.Simulation/Randomness/`)

| File | Content |
|---|---|
| `Pcg32.cs` | PCG32 XSH-RR 64/32 (`pcg32-xsh-rr-v1`): reference seeding, wrapping `unchecked` arithmetic, rotate-right output, `NextBounded` rejection sampling, `Draws` counter, `Advance` (jump-ahead), `FromState` (rejects even increment / negative draws) |
| `RngVersions.cs` | `Algorithm = pcg32-xsh-rr-v1`, `KeyDerivation = kd1`, `SnapshotFormat = rngset1` |
| `RngStreamId.cs` | `RngStreamId` (`name@version`, validated) and `GameRngStreams`: `game/pitch`, `game/contact`, `game/field`, `game/runner`, `game/location`, `game/ai` (all `@1`) with the per-stream purpose and the `game/location` consumption rule |
| `StreamKeyDerivation.cs` | `kd1`: SHA-256 over tag + big-endian seed + length-prefixed UTF-8 context and stream text; big-endian `initstate`/`initseq` |
| `RngStream.cs`, `GameRandom.cs` | the lazily created, mutually independent streams of one context; `Snapshot`, `Restore`, `Atomically(work, token)` |
| `GameRandomSnapshot.cs` | `rngset1` snapshot, canonical text, `StateHash` |
| `GameIdentity.cs` | `GameIds.Create` (`game:{competition}:{fixture}`) and `GameEventIds` (`{game id}:e{000001..999999}`) |

Main API: `GameRandom.Create(seed, contextKey)`, `.Stream(GameRngStreams.X).NextBounded(n)`, `.Snapshot()` / `GameRandom.Restore(snapshot)`, `.Atomically(work, token)`; `GameEventIds.Next()`. Decisions and exact formats: `docs/decisions/ADR-004.md`; contract summary `docs/game-engine.md` §17; `SYSTEM_CONTRACTS` C08 status line. Everything is public in the Simulation assembly (no new project references; it still depends on Domain only).

Not built (by instruction): probabilities, location drawing or model, `ef1`/`ef2`, reducer changes, ABS/Framing, pitcher AI, LMV/BCL, persistence, any Phase 3-B+ item.

## 2. Files

Created: the 8 files above; `tests/StarPlayer.Simulation.Tests/Randomness/{RngFixture,Pcg32Tests,GameRandomTests,GameIdentityTests,RngSourceScanTests}.cs`; `tests/Fixtures/generator/generate_rng_golden.py`; `tests/Fixtures/phase3a_rng_golden.json`; `docs/decisions/ADR-004.md`; `docs/reports/PHASE_3A_REPORT.md`, `docs/reports/PHASE_3A_VERIFY_LOG*.md`; `Star/01_Phase_Reviews/Phase_03_Review.md`. Modified: `docs/game-engine.md` (§17), `docs/SYSTEM_CONTRACTS.md` (C08 status line), `CURRENT_STATE.md`, `Star/00_Dashboard.md`. No existing source, test or fixture file was modified. The untracked `TASK.md` and the two PDFs under `docs/` were preserved.

## 3. Golden vectors (independent references)

- **Published reference (author's own output).** From pcg-random.org "Using PCG (C Basic)", pcg32-demo round 1, seed 42 / sequence 54: the six 32-bit values `a15c02b7 7b47f409 ba1d3330 83d2f293 bfa4784b cbed606e` and the 33 bounded dice rolls (`bounded(6)+1`) that follow 71 draws (6 words + 65 coin flips). They are typed by hand into `Pcg32Tests` and also asserted by the Python generator before it writes anything. The reference C source (`pcg_basic.c`: `pcg32_srandom_r`, `pcg32_random_r`, `pcg32_boundedrand_r`) was fetched and the C# follows it.
- **Independent Python implementation** (`generate_rng_golden.py`, standard `hashlib` SHA-256, no shared code): seed vectors for 7 seed pairs including `0`, `2^64-1` and mixed extremes (8 outputs each), bounded sequences for 11 bounds (`0x80000001` rejects about 50 % of raw outputs and `0xC0000001` about 25 %, so both exercise the rejection loop; `0xFFFFFFFF` rejects only the single output 0 and is an edge-of-range value, not a rejection-heavy one) with values and cumulative draw counts, jump-ahead vectors (8 deltas), `kd1` keys (7 combinations including a non-ASCII context), and one multi-stream `rngset1` scenario (canonical text and SHA-256).
- **Result:** all of them pass in C#. Golden values were not produced by the C# code.
- **Limits:** only the PCG parts and SHA-256 are externally anchored; the `kd1`/`rngset1` text formats are this project's own and the Python script and the C# code share the same author's reading of ADR-004. No C compiler was available, so the upstream C demo itself was not executed, only its published output compared.

## 4. Independence and snapshot checks (all passing)

Same seed/context/stream = identical output; different seed, context, stream or stream version = different sequence; the six streams are pairwise different; reverse creation order plus interleaved draws leaves every stream identical to drawing it alone; extra draws on `game/location`/`game/ai` between `game/contact` draws change nothing. Snapshot: versions recorded, ordinal order, draw counts; restore continues every stream identically and a never-created stream still derives from the stored seed; the Python set vector's canonical text and hash are reproduced. Rejected on restore: missing snapshot, wrong format/algorithm/key-derivation version, even increment, negative draws, wrong derived increment, state not consistent with the draw count (forged/corrupt), duplicate or invalid stream, invalid context, different master seed/context. Atomic scope: success publishes (equal to drawing directly, existing stream objects keep identity); a failure result, an exception, cancellation before the work and cancellation after the work all leave the original byte-identical; a retry after failure gives the same result. Ids: pure and injective game ids, strictly increasing unique event ids, resume without reissue, exhaustion fails instead of wrapping, canonical-only parsing, and interleaved id generation provably changes neither draws nor snapshot hash. A source scan forbids `System.Random`, time, `GetHashCode`, `Guid.NewGuid`, threads/tasks and mutable static state in `Randomness/`.

## 5. Tests and results

| Command | Result |
|---|---|
| `dotnet test tests\StarPlayer.Simulation.Tests --filter "FullyQualifiedName~Randomness"` (Debug) | 71 passed, 0 failed (Pcg32Tests 14, GameRandomTests 39, GameIdentityTests 16, RngSourceScanTests 2) |
| `dotnet test StarPlayer.sln -c Release` | Domain 145, Simulation 331, Application 114, Infrastructure 79 = **669 passed, 0 failed, 0 skipped** (Phase 2 baseline 598 + 71 new; no Phase 2 test changed) |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1 -LogName PHASE_3A_VERIFY_LOG.md` | `GATE: PASS`, exit 0, 2 min 40 s: Release build 9 projects 0 warnings, all xUnit suites, determinism detector 48, Godot headless checks, Windows export and cold-copy runs, doc links 54 files ([log](PHASE_3A_VERIFY_LOG.md)). A second run after the documents were written (`-LogName PHASE_3A_VERIFY_LOG_run2.md`): `GATE: PASS`, exit 0, doc links 57 files ([log](PHASE_3A_VERIFY_LOG_run2.md)) |
| `python -I tests/Fixtures/generator/generate_rng_golden.py` (Python 3.14.7) | published-output self-checks pass; fixture written |

Environment: Windows 11 Home 10.0.26200, .NET SDK 8.0.425, Godot 4.7.2 Mono, Release for the solution run.

**Mutation check** (the tests were shown to be able to fail): 22 deliberate source mutations were each applied alone and the Randomness tests run; all 22 were detected: rotation direction, shift constant, LCG multiplier (non-terminating bounded loop, caught by the hang timeout and by 5 failures), even increment, rejection removed, threshold wrong, draw counter off, little-endian key bytes, stream version ignored, context ignored, commit on failure, cancellation not honoured, restore skipping the state check / algorithm check / increment check, working copy sharing live streams, unsorted snapshot, unpadded event ids, id wrap-around, and three forbidden-source patterns (`new Random()`, `DateTime.UtcNow`, mutable static). Process notes, stated for honesty: the first mutation run produced garbled output because an earlier loop was still running (both were stopped and the sources restored and re-verified by hash before a clean sequential rerun); one pass showed a real test defect (the source-scan regex stripped the rest of a file after the first `//`), which was fixed and re-proved by the three forbidden-source mutations; four mutations that first failed to compile (unreachable-code warning as error) were redone with a compilable form. Final sources equal the pre-mutation copies (hash-verified).

Not run: Godot/UI clicks (no UI change), FHD (no UI change), independent Codex or GPT Chat review (`미실시`), any 100,000-PA, 100-game or calibration run (3-F), performance measurement.

## 6. Open issues, debt and notes

1. `kd1`/`rngset1` are first-time project formats checked against one author's Python re-implementation; an independent reviewer should confirm ADR-004 items 4 and 9 before 3-B builds on them.
2. The task cited a "Codex Phase 3 RNG design decision"; no such document exists in the repository. The contract was derived from ROADMAP §4.1/§6.1/§1.14 and C08 and is open for correction.
3. No unit-interval (`double`) draw exists. 3-B should fix a fixed-denominator integer probability scale used with `NextBounded` (avoids platform floating-point differences); this is a 3-B design decision, not made here.
4. `game/location` draw count per pitch is undefined until the location model (3-B/3-C); ADR-004 item 7 fixes only name, version, order relative to outcome draws and the rule that a count/order change needs `@2`.
5. `GameEventIds` format is not yet adopted by the resolver; the Phase 2 reducer accepts any non-empty event id and `ef1` is untouched. Adoption belongs to 3-C.
6. Snapshot persistence (tables, migration, commit with game state and receipts) is Phase 4; the snapshot has a canonical text but no JSON/SQL mapping.
7. `Atomically` is synchronous and single-writer; no cross-thread protection by design.

## 7. Handoff to 3-B

Use `GameRandom` per game (context key = game id from `GameIds`), draw only from the stream named for the purpose, run each fallible game step inside `Atomically` together with `GameReducer.Apply`, and commit `Snapshot()` with the state. Decide the probability scale, the zone/boundary/call-mode model contracts and tuning versions (ROADMAP §1.14, work package 3-B) before any calibration. Phase 3 Gate G, the Pitch Zone Lite checks and the 100,000-PA / 100-game criteria remain **INCOMPLETE** for the whole of Phase 3.

## 8. Self-assessment

**PASS WITH NOTES** for work package 3-A: every 3-A requirement in `TASK.md` is implemented and tested, all executed checks pass, nothing outside 3-A was touched. Notes: items 1, 2 and 6 above (independent review of the new formats, absent Codex document, persistence deferred). This is not a Phase 3 Gate result and not an approval.
