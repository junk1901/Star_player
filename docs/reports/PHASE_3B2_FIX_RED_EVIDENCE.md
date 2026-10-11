# Phase 3-B2 fix round — RED evidence (before the fix)

Raw console output is not tracked (`*.log` is ignored and the output lived in a temporary folder); this file keeps the facts. The tests were written first and run against the **unfixed** 3-B2 code (HEAD `7756442` plus the uncommitted 3-B2 work package, 909 passing tests at that point), 2026-10-11, Debug build, `dotnet test <project> --filter ...`.

## Codex findings reproduced

| Finding | Test (new) | Result before the fix |
|---|---|---|
| High 1 — `Distribution.Ppm` / `Outcomes` hand out their backing arrays | `ProbabilityImmutabilityTests.The_Codex_counterexample_cannot_change_the_reported_probabilities` | **FAIL** — distribution `[400000, 600000]`; casting `Ppm` to `int[]` and writing 0 changed the public list: `Expected [400000, 600000]`, `Actual int[] [0, 600000]` (the internal cumulative table still said `Select(0) = a`, so public table and selection disagreed) |
| High 1 | `…The_outcome_ids_cannot_be_changed_through_the_public_collection`, `…Repeated_reads_never_expose…`, `…The_public_probabilities_and_the_selection_always_describe_the_same_creation_time_values`, `…The_public_collections_are_not_backed_by_an_array_or_a_writable_list` | FAIL (all four) |
| High 2A — shared `ProbabilityModel.V1` nested lists are arrays | `…Nested_outcome_lists_of_the_shared_model_cannot_be_changed`, `…The_stage_slot_and_tendency_lists_…`, `…The_whole_public_graph_of_the_shared_model_holds_no_array_and_no_writable_collection`, `…Two_consumers_of_the_shared_model_see_the_same_structure_whatever_the_other_does` | **FAIL** (all four). Writing `"tampered"` into a condition's outcome array, or into a slot's `Targets` array, changed the shared static model for every later reader, and in the same test process the next ordinary `TuningProfile` load then failed with `Tuning.baseline_outcomes_mismatch … tampered,in.r1c2,…` — one consumer broke every other |
| High 2B — `TuningProfile.Modifier(...).Table` is a writable `List` | `TuningImmutabilityTests.The_Codex_modifier_table_tamper_cannot_change_a_calculation_or_the_hash` | **FAIL** — replacing the `aggressive` factor in the returned list changed `batter_decision / out.low` swing from **320388 to 348837 ppm** while `ContentHash` stayed `5b70dca7…3ea5` (the Codex reproduction was 568966 → 600000 on its own input) |
| High 2 | `…No_public_path_of_the_tuning_profile_leads_to_an_array_or_a_writable_collection`, `…After_every_tamper_attempt…`, `…Tampering_with_one_loaded_profile_does_not_affect_another_consumer…`, `…The_trace_and_result_of_a_stage_cannot_be_used_to_change_later_results`, `…The_hash_recomputed_independently…` | FAIL (all five; the last one failed only because an earlier tamper in the same process had already corrupted the shared model) |
| Medium — `"baselines": null` | `TuningJsonNullTests` (12 of 27 cases) | **FAIL** — `System.NullReferenceException … at TuningProfileJson.ReadDraft` (`TuningProfileJson.cs` line 107); also null `modifiers`, `scenarios`, `targets`, `tendency`, null elements of every required array, null baseline `outcomes`/`ppm`, null table entry |

Counts of the RED runs: `ProbabilityImmutabilityTests` 1 pass / 9 fail; `TuningImmutabilityTests` (without the golden digest) 2 pass / 6 fail; `TuningJsonNullTests` 15 pass / 12 fail; the golden-digest test was run alone on the unfixed code, in its own process, with a placeholder and printed the pre-fix value.

## Tests that passed before the fix (kept as regression tests, not RED evidence)

`Normal_inputs_select_exactly_what_they_selected_before_the_fix`, `Changing_the_input_collections_after_creation_…` (the profile already copied its input), `The_shipped_tuning_hash_and_values_are_unchanged`, `The_shipped_tuning_still_loads`, the missing-member cases (STJ already reported them as `Fixture.json_invalid`), wrong-type cases, the optional `table`, empty documents.

## Pre-fix golden value

`TuningImmutabilityTests.Normal_calculations_give_exactly_the_results_they_gave_before_the_fix` pins SHA-256 `2ffa361ac49bc56cd7133d10916d85320c362dbfd7b54abb71df7028ef2b9988` over the full results (distribution and combined factors) of every stage and condition (105 rows) under neutral, low-extreme and high-extreme inputs, plus the guard/tendency shifts of a mean-neutral vector in every inner zone. The value was produced by the **unfixed** code; the same test passing after the fix is the proof that no calculation changed.
