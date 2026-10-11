# Phase 3-C1 fix round — RED/GREEN evidence (summary)

Defect (Codex review `CHANGES REQUIRED`, Medium 1): `ScriptedGameJson.ReadEventsV2` threw on an explicit JSON `null` for the `events` member or for an element of it.
`System.Text.Json` treats a `required` member as satisfied by an explicit `null`, so the null reached `dto.Events.Select(...)` and `ToEventV2(e, ...)`.

## RED (regression tests written first, run against the unfixed `ReadEventsV2`)

Command: `dotnet test tests/StarPlayer.Infrastructure.Tests -c Release --filter EventJsonV2Tests`, 2026-10-11, .NET SDK 8.0.425.
Result: **failed 5, passed 23, total 28** (the 16 existing `EventJsonV2Tests` and the 7 regression cases that already held passed).

| Input | RED result (unfixed code) |
|---|---|
| `{"format_version":2,"events":null}` | `System.ArgumentNullException` (parameter `source`) |
| `{"format_version":2,"events":[null]}` | `System.NullReferenceException` |
| `events: [null, <valid>]` | `System.NullReferenceException` |
| `events: [<valid>, null]` | `System.NullReferenceException` |
| `events: [<valid>, <valid>, null, <valid>]` | `System.NullReferenceException` |

Already green before the fix (kept as regression guards): `events` member missing, `events` of the wrong JSON type (`5`, `"x"`, `{}`), element of the wrong type (`5`, `"x"`), a valid array, an empty array.

The raw console output of the RED run was not saved to a file; this table was written from that run's output. The original 3-C1 work package has no RED log (unchanged note).

## GREEN

After the fix: `EventJsonV2Tests` 28/28, Infrastructure.Tests 234/234, full `dotnet test StarPlayer.sln -c Release` 997 passed (Domain 263, Simulation 383, Application 117, Infrastructure 234), 0 failed, 0 skipped.
