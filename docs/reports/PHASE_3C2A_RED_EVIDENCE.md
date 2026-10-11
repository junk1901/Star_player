# Phase 3-C2A — RED/GREEN and mutation evidence (summary)

Scope: the single-pitch probability branch (`PitchBranchResolver`) and the new stream `game/target@1`. Review status `검토 대기` (not approved).

## RED (tests written first, run against a stub resolver)

The 81 new tests (`TargetStreamTests` 8, `PitchBranchResolverTests` 73) and the independent golden file were written before the resolver existed. `PitchBranchResolver.Resolve` was a stub returning the failure `PitchBranch.not_implemented`; the draft types and the stream constant `GameRngStreams.Target` existed (they are declarations, not behaviour).

Command: `dotnet test tests/StarPlayer.Infrastructure.Tests -c Release --filter "FullyQualifiedName~PitchBranch|FullyQualifiedName~TargetStream"` (2026-10-11, .NET SDK 8.0.425). Raw console output: `docs/reports/PHASE_3C2A_RED.log` (git-ignored by the existing `*.log` policy; kept locally only).

Result: **failed 69, passed 12, total 81.**

The 12 that passed against the stub, and why they could (none of them needs resolver behaviour):

| Test | Reason it was green |
|---|---|
| `TargetStreamTests` ×8 (name/version/set of seven, key derivation + first draws vs Python, independence vs Python, drawing never disturbs another stream, rejection vector, snapshot order and restore, lookup without draw, algorithm names) | they exercise `RngStreamId` / `GameRandom` / `Pcg32`, which already existed; only the stream constant was new. So the stream contract has **no** RED run of its own. |
| `A_draft_is_not_a_game_event_and_the_resolver_never_touches_the_reducer` | the stub does not mention the reducer |
| `The_draft_types_are_resolver_internal...` | the draft types are in no signature of the other layers |
| `The_golden_set_covers_every_branch_kind...` | reads the golden file only |
| `The_mismatched_zone_model_helper_really_builds_a_different_zone_model` | test-helper sanity |

The other 69 (every golden scenario, the stage/stream/draw-count contract, Take, switch hitter, versions, validation codes, no-side-effect, cancellation) failed with `PitchBranch.not_implemented`.

One defect found in the test data while preparing RED: the first golden file used the context `game:ünï`, which `GameRandom.Create` rejects (`Rng.context_invalid`: letters, digits, `_ - . :` only; the Phase 3-A vector uses it only through `StreamKeyDerivation.Derive`). The generator was changed to `game:3c2a:max_seed` and the file regenerated (deterministic: two runs are byte-identical) before the resolver was written.

## GREEN

First implementation of the resolver: the 81 new tests all passed against the independent oracle without any expectation being edited. Full `dotnet test StarPlayer.sln -c Release`: Domain 263, Simulation 383, Application 117, Infrastructure 339 = **1102 passed** (1021 + 81), 0 failed, 0 skipped.

## Mutation checks (15; each applied to the source, the 81 tests run, then reverted)

| # | Mutation | Failing tests |
|---|---|---:|
| M1 | `batter_decision` drawn on `game/location` instead of `game/pitch` | 31 |
| M2 | explicit Take may swing (the Take override removed) | 16 |
| M3 | `target_zone` drawn on `game/location` | 32 |
| M4 | a certain (100%) stage skips its `NextBounded` call | 12 |
| M5 | switch hitter side inverted | 10 |
| M6 | explicit Take ignores a `hit_by_pitch` decision | 2 |
| M7 | wrong roll bound (999,999 instead of 1,000,000) | 30 |
| M8 | reseeds instead of continuing the given snapshot | 27 |
| M9 | `NextBoundedCalls` off by one | 31 |
| M10 | an undeclared called-pitch mode accepted | 2 |
| M11 | actual location conditioned on a fixed zone instead of the target | 30 |
| M12 | the stream renamed `game/target@2` | 35 |
| M13 | `advancement` drawn on `game/runner` | 13 |
| M14 | `called_pitch` always uses the `non_automated` key | 13 |
| M15 | `batted_ball` group computed from a fixed zone | 9 |

All 15 were detected. After each run the original file was restored and the tests re-run green (an early restore left an older timestamp, so one stale mutated build was noticed and rebuilt; the table values above come from builds of the mutated source). The raw output of these runs was not saved; only the printed summaries above exist, so they are not independently re-verifiable. M6 and M10 are caught by only two tests each (the Take hit-by-pitch case and the undeclared-mode case).

## Follow-up fix: game id / RNG context mismatch (Codex CHANGES REQUIRED, Medium)

RED (before any resolver change, 2026-10-11, `dotnet test tests/StarPlayer.Infrastructure.Tests -c Release --filter "FullyQualifiedName~PitchBranchResolverTests"`): **9 failed, 75 passed, 84 total.** The fixture had already been changed so that every positive test passes the same text as `GameState.GameId` and `GameRandomSnapshot.ContextKey` (those 73 existing tests still passed). The 9 failures were the new tests, each failing because `Resolve` returned **success** for a snapshot of another game (no `PitchBranch.game_context_mismatch` existed):

- `A_snapshot_of_another_game_is_refused_before_any_draw_and_nothing_of_the_caller_changes` x5 (A/B, B/A, case-only, `g001` vs `game:3c2a:probe`, trailing difference);
- `Both_explicit_take_and_normal_pitch_are_refused_on_a_mismatch` x2;
- `An_invalid_snapshot_keeps_its_own_error_code_whatever_the_game_id_is` (its empty-game-id step succeeded);
- `The_mismatch_error_code_belongs_to_the_resolver_and_is_new` (code not defined).

GREEN (after the check, same command): **84 passed, 0 failed.** The raw RED output was read from the console and not saved to a file.
