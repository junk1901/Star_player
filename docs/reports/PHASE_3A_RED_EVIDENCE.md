# Phase 3-A final fix round — RED / GREEN evidence

The repository-wide `*.log` ignore hides raw logs from Git, so this tracked summary records them. The originals stay in this folder locally:
`PHASE_3A_RED_BCDE.log` (RED, before the fix) and `PHASE_3A_GREEN_AFTER_FIX.log` (GREEN, after the fix). Instruction: `TASK.md` (Phase 3-A final fix).

## RED run (unfixed code, `dotnet test tests\StarPlayer.Simulation.Tests --filter "FullyQualifiedName~RngHardeningTests"`)

Result: **24 tests, 16 failed, 8 passed**. The unfixed sources were the Phase 3-A code exactly as reviewed (no source file changed between the review and this run).

| Defect | Failing tests (RED) | Observed on the unfixed code |
|---|---|---|
| B (M1) looking at a stream changes the hash | `Touching_a_stream_without_drawing_leaves_the_state_hash_equal_to_never_touching_it`, `Touching_other_streams_does_not_change_the_hash_of_a_game_that_has_drawn`, `Touching_a_stream_inside_an_atomic_scope_does_not_change_the_hash_either`, `A_stream_enters_the_snapshot_with_its_exact_state_after_its_first_draw` | hashes differ; the snapshot held a zero-draw `game/location@1` entry |
| C nested scope | `Nested_scope_on_the_same_instance_is_rejected_and_nothing_is_published`, `A_scope_started_on_the_working_copy_is_rejected_too`, `After_a_rejected_nested_call_an_ordinary_scope_runs_again_and_equals_direct_drawing` | no exception thrown |
| C direct / pre-acquired access, escaped streams | `Drawing_from_the_original_while_a_scope_runs_is_blocked_before_any_state_change`, `A_stream_that_escapes_the_scope_cannot_draw_afterwards` | no exception thrown; the original advanced during the scope |
| D draw limit | `NextUInt32_at_the_draw_limit_throws_before_changing_anything`, `The_last_allowed_draw_succeeds_and_equals_the_unlimited_output`, `NextBounded_that_needs_a_draw_beyond_the_limit_leaves_the_generator_exactly_as_it_was`, `Advance_validates_the_input_and_the_remaining_limit_before_changing_anything`, `A_stream_restored_at_the_draw_limit_refuses_further_draws_and_keeps_its_snapshot` | `Draws++` wrapped to a negative number instead of refusing; `Advance` threw the wrong exception type for "over the remaining allowance" |
| E non-canonical snapshots | `A_snapshot_whose_streams_are_not_in_canonical_order_is_rejected_not_silently_reordered`, `A_snapshot_entry_for_a_stream_without_draws_is_rejected_as_non_canonical` | restore succeeded and silently re-sorted / accepted the unused entry |

The 8 tests that passed on the unfixed code are intentional: 4 culture tests (`Canonical_texts_and_ids_do_not_depend_on_the_current_culture`, ar-SA/fa-IR/th-TH/tr-TR - the output was already culture-invariant for positive integers, so no RED is possible; the explicit `InvariantCulture` call is a defensive change) and 4 regression guards (`Work_that_swallows_the_misuse_error_still_publishes_only_the_copy`, `Working_copy_and_original_share_no_mutable_object`, `The_scope_lock_is_released_after_a_failure_an_exception_and_a_cancellation`, `NextBounded_that_fits_the_limit_exactly_still_succeeds`).

## A (omitted consumed stream) - how it is evidenced

The expectation contract is a new API (`GameRandom.Restore(snapshot, expectedStateHash)`), so a RED run against the old code can only be a compile error (no such overload); no fake RED was constructed. The hazard itself is reproduced on the structure-only overload and kept as a permanent test, `Limit_without_an_expectation_a_deleted_consumed_stream_restores_and_silently_restarts`: it passes before and after the fix and documents that a snapshot from which a consumed stream was removed restores successfully with that stream back at its first value. The same fact was reproduced in the earlier independent review round (probe, outside the repository). GREEN: the 16 tests of `SnapshotExpectationTests`, including rejection of every removed/added/advanced stream against the committed hash and acceptance of the Python-computed hash of the golden scenario.

## GREEN run (after the fix)

`dotnet test tests\StarPlayer.Simulation.Tests --filter "FullyQualifiedName~Randomness"`: **111 passed, 0 failed** (71 original + 24 `RngHardeningTests` + 16 `SnapshotExpectationTests`). A first GREEN attempt had 1 failure that was a mistake in the new test (`Advance(ulong.MaxValue)` is by definition an invalid argument, not "over the allowance"); the test was corrected, the implementation was not changed for it.

## Mutation check of the new guards

18 deliberate source mutations, each applied alone and the Randomness tests run; all 18 were detected: snapshot includes unused streams; original lock not checked; closed copy not checked; nesting on the original allowed; nesting on the working copy allowed; lock never set; lock not released; copy not closed; copy shares the generator; order check removed; unused-entry check removed; expected hash ignored; expected hash compared with the input snapshot's own hash; hash format check loosened; `NextUInt32` limit check removed; `NextBounded` rollback removed; `Advance` remaining-allowance check removed; `Advance` input check removed. Three of them first failed to compile (warnings are errors) and were redone in a compilable form. Sources were restored and compared byte-for-byte with a copy taken before the run.
