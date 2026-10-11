# Phase 3-C1 second fix round — nested null elements: RED/GREEN evidence (summary)

Defect (GPT Chat review `CHANGES REQUIRED`, found by the probe of the first fix round): an explicit JSON `null` as an **element** of a list inside an event (`moves`, `strikeout_credits`, `batter_credits`, `errors`, a move's `credits`) and as an element of an `events` list reached `.Select(...)` / member access and ended in `NullReferenceException` (or, for `errors`, in a misleading `Fixture.enum_invalid`). `required` / `List<T>` in `System.Text.Json` do not reject a null element.

## RED (regression tests written first, run against the unfixed converters)

Command: `dotnet test tests/StarPlayer.Infrastructure.Tests -c Release --filter EventJsonNestedNullTests --nologo` (2026-10-11, .NET SDK 8.0.425). Raw console output saved to `docs/reports/PHASE_3C1_FIX2_RED.log` (git-ignored by the existing `*.log` policy, kept locally; this file is the tracked summary).
Result: **failed 19, passed 5, total 24.** The 5 passing tests are the compatibility guards (optional lists null / absent / empty on the three readers, `strikeout_credits` null-vs-empty meaning, a well-formed nested event on the three readers).

Directly reproduced on **all three readers** (legacy `ReadEvents`, `ReadEventsV2`, scripted-game `ScriptedGameJson.Read`), each by running the input, not by reading the code:

| Input | RED result (unfixed) |
|---|---|
| `moves: [null]` (pitch, in_play, runner_play) | `System.NullReferenceException` ×3 readers |
| `strikeout_credits: [null]` | `System.NullReferenceException` ×3 |
| `batter_credits: [null]` | `System.NullReferenceException` ×3 |
| `moves[1].credits: [null]` | `System.NullReferenceException` ×3 |
| null in the middle of `moves` / `batter_credits` / `errors` | `System.NullReferenceException` ×3 (the first sub-case `moves` aborts the test) |
| `errors: [null]` (in_play, runner_play) | not a crash: `Fixture.enum_invalid` instead of the required `Fixture.field_missing` with the path, ×3 |
| scripted game `events: [null, ...]`, `events: null`; legacy array `[valid, null]` | `System.NullReferenceException` |

Not probed (outside the stated scope): null members of `away` / `home` / lineup / bench / `expected`, GameRules, player packs, tuning loader.

## GREEN

After the fix: `EventJsonNestedNullTests` 24/24; Infrastructure.Tests 258/258; full `dotnet test StarPlayer.sln -c Release` 1021 passed (Domain 263, Simulation 383, Application 117, Infrastructure 258), 0 failed, 0 skipped.

## Mutation checks of this fix (6, each applied to `ScriptedGameJson.cs`, the 3 relevant test classes run, then reverted)

| Mutation | Failing tests |
|---|---|
| null `errors` element not checked | 6 |
| null credit inside a move not checked | 12 |
| null move not checked | 6 |
| null event element not checked | 5 |
| null elements silently dropped from `events` (partial success) | 5 |
| null `strikeout_credits` turned into an empty list | 7 |

All 6 detected. This is a new check set; the raw logs of the earlier 7 mutation checks remain un-reviewed, and the original 3-C1 package still has no RED log.
