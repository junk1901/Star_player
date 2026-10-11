# Phase 3-B1 Report — Pitch Zone Lite data contracts and called-pitch mode

**Final approval (2026-10-11): Phase 3-B1 `PASS WITH NOTES` / approved by GPT Chat; Codex focused re-review `PASS WITH NOTES` (the earlier High finding - reference id vs weights - is resolved; Codex re-review passed; no new mandatory code change). Full xUnit 813 PASS (run by Claude); the 104 related xUnit tests run directly by Codex PASS; `tools/verify.ps1` two consecutive `GATE: PASS` (run by Claude, logs reviewed by Codex). This approval covers internal work package 3-B1 only: the Phase 3 overall Gate stays INCOMPLETE, Phase 3-B2 is not started, and the top-level Phase 3 review status stays `검토 대기`.** Earlier review verdicts (Codex `CHANGES REQUIRED` on the first version) are kept as history below.

Status (at the time of the first version): **implemented and self-verified; review status `검토 대기`** (not approved). Scope: internal work package 3-B1 of Phase 3 only (owner instruction `TASK.md`, 2026-10-11): the zone/location/tendency data contracts and the C40 called-pitch mode. **The Phase 3 Gate and Phase 3-B as a whole are not evaluated and not claimed**: 3-B2 onward and Phase 4 are not started. Nothing is committed or pushed (base commit `f2e808f0ff85f9ea414748abb1a2275f7af18a4b`, working tree dirty). Decisions: `docs/decisions/ADR-005.md`; contract text: `docs/game-engine.md` section 18.

## Part F — Fix round after the independent Codex review (2026-10-11, `TASK.md`) — current state

Status (at the time of the fix round; superseded by the final approval at the top): **fixed and self-verified; review status stays `검토 대기`** (not approved; Codex `CHANGES REQUIRED` was the verdict on the first version, no re-review has happened). Scope: one High and one Low finding only. Phase 3-B as a whole, 3-B2 onward and Phase 4 are not started; the Phase 3 Gate is INCOMPLETE. Nothing is committed or pushed. Parts 1-12 below are the first version and are kept as history; where they say 797 tests or describe `ZoneReferenceDistribution.Create` as building any id, Part F supersedes them.

**F.1 High - reference id vs weights.** Cause: `ZoneReferenceDistribution.Create(id, weights)` was public and unchecked, so `pzl-ref-uniform-v1` could be built with `[3,1,1,1,1,1,1,1,1]`; a `BatterZoneTendency` stored with that `ReferenceId` could pass a vector (`[1,-3,0,...]`) that the real UniformV1 rejects (weighted sum -2), i.e. one id with two meanings. Fix (`BatterZoneTendency.cs`): a registry of definitions; `UniformV1` = exactly nine 1s; `Resolve(id)` (unknown -> `id_unknown`); `Create(id, weights)` confirms an id with exactly its registered weights (`weights_mismatch` otherwise) and returns the registered instance; no public constructor and no other public way to build an unregistered reference (an `internal` factory for `Domain.Tests` only); immutable weights copied on creation; weighted sum stays a `long` over range-checked values (<= 9e8, no overflow path); nothing is silently replaced by a neutral default and no id was renamed. Rule for the future: a new reference gets a NEW id with fixed registered weights (ADR-005 item 9). `-100..+100`, `pzl-zones-v1`, `gamerules-v2` and every hash are unchanged.

**F.2 Low - hidden-boundary test wording.** `HiddenTendencyBoundaryTests` now says what it proves: no Application/Infrastructure member signature names the `BatterZoneTendency`/`ZoneReferenceDistribution` types (direct, array, generic) and the type's text form prints no values. It does not detect numbers copied into another DTO/string/JSON. A meaningless serialization assertion was removed. Handoff: when the first Application query/DTO that can touch a batter exists (Phase 3-D/5), add a test that serializes the real output (JSON/text, tooltips, Gemini payloads) and asserts the tendency values are absent. No UI or API was added.

**F.3 RED/GREEN.** Tests were written first (`ZoneReferenceDistributionTests`, 16 tests, plus updated `BatterZoneTendencyTests`). RED was run against the unfixed logic with compile-only stubs for the two new members (`Resolve`, the test factory): `dotnet test tests\StarPlayer.Domain.Tests` -> **7 failed, 225 passed (232)**: the Codex counterexample (`The_codex_counterexample_a_reused_uniform_id_with_other_weights_is_rejected_at_creation`), `One_reference_id_always_means_one_set_of_weights_whatever_route_built_it`, `The_public_surface_has_no_way_to_build_an_unregistered_reference`, and four `An_unknown_reference_id_is_rejected...` cases (`pzl-ref-other-v1`, `pzl-ref-uniform-v2`, `PZL-REF-UNIFORM-V1`, `r`). Raw log `docs/reports/PHASE_3B1_FIX_RED.log` (git-ignored by `*.log`, kept locally; this paragraph is the tracked summary). GREEN after the fix: Domain **232 passed, 0 failed**. Requested cases: the counterexample is rejected at creation and, against the real UniformV1, its tendency is rejected (`not_average_neutral`); the normal UniformV1 is created (by `Create` and `Resolve`, same instance); `[1,-1,0,0,0,0,0,0,0]` and nine zeros are accepted; `[1,-3,0,0,0,0,0,0,0]` is rejected; unknown ids are rejected by both entry points; a caller's array changed after creation does not change the stored values (reference weights and tendency values); one `ReferenceId` always resolves to one set of weights.

**F.4 UniformV1 invariance check.** `pzl-ref-uniform-v1` <-> `[1,1,1,1,1,1,1,1,1]` is pinned by tests on every public route (`UniformV1`, `Resolve`, `Create`), forged weights (`[2]*9`, `[1]*8+[0]`, `[0]*8+[1]`, the Codex vector) are refused, and `Weights` cannot be written through the list interface.

**F.5 Mutation check of the fix.** 6 mutations: weight comparison removed, any id resolves to uniform, `Create` trusts any id, neutral default uses another id - all detected; "`Create` returns a new object with the caller's (verified-equal) weights" survives because it is behaviourally equivalent (the weights were just confirmed identical) - not a defect; "weighted sum in `int`" does not compile (the sum is `long` by construction). The earlier 15 mutations were not re-run (their code did not change).

**F.6 Tests.** `dotnet test StarPlayer.sln -c Release`: Domain 232, Simulation 371 (includes the 111 Randomness tests of Phase 3-A), Application 117, Infrastructure 93 = **813 passed**, 0 failed, 0 skipped (797 in the first version, +16). Phase 2 (598) and Phase 3-A (111) tests are unchanged and pass.

**F.7 `toolserify.ps1`.** Two consecutive runs after all code and documents were final: `powershell -NoProfile -ExecutionPolicy Bypass -File toolserify.ps1 -LogName PHASE_3B1_FIX_VERIFY_LOG_run1.md` and `... _run2.md` - both **`GATE: PASS`, exit 0**: Release build of 9 projects with 0 warnings, Domain 232, Simulation 371, Application 117, architecture 14, determinism detector 48, Infrastructure 93, Godot headless checks, Windows export and cold-copy runs, relative doc links (65 and 66 files). Logs: `docs/reports/PHASE_3B1_FIX_VERIFY_LOG_run1.md`, `docs/reports/PHASE_3B1_FIX_VERIFY_LOG_run2.md`; the first-version logs `PHASE_3B1_VERIFY_LOG_run1.md`/`_run2.md` are kept unchanged. Also: `git diff --check` exit 0 for tracked changes; new files scanned for trailing whitespace; `Randomness/`, `GameEvents.cs`, `GameReducer*`, `EventFingerprint` unchanged (empty diff vs `HEAD`).

**F.8 Remaining notes.** The first version's notes remain (coarse boundary table, interim `UniformV1`, no RED log for the original 3-B1 tests, unused v2 pack, data-contract review by Codex after this fix not yet done: `미실시`). Not run: manual UI/FHD (no UI change), Phase 3 Gate items.

**F.9 Self-assessment: PASS WITH NOTES** for the fix round (both findings addressed, RED reproduced and GREEN, all executed checks pass). Not a Phase 3 Gate result and not an approval.

---

# Part 1-12 - First version (kept as history; superseded where Part F says so)

## 1. Data models implemented (`src/StarPlayer.Domain/Baseball/PitchZone/`, namespace `StarPlayer.Domain.Baseball.PitchZone`)

| Type | Role |
|---|---|
| `ZoneModelVersion` | `pzl-zones-v1`; only known versions can be created |
| `PitchZoneId`, `PitchZoneModel`, `PitchZoneInfo` | the 17 stable zone ids, region, batter-relative lateral/vertical, boundary class, inner row-major index; `V1`, `Resolve`, `Info`, `Classify`, `ToCatcherView`, `FromCatcherView` |
| `AppliedBatterSide`, `AppliedBatterSideRules` | the side used for one pitch; validation against the declared `BattingSide` (switch hitters) |
| `PitchLocation`, `TargetZone` | confirmed location (model, zone, applied side, boundary class) and the pitcher's aim - separate types |
| `BatterZoneTendency`, `ZoneReferenceDistribution` | hidden nine-value tendency (`-100..+100`, neutral default, average-neutral against a named reference) |
| `CalledPitchMode` (in `GameRules.cs`) + `GameRuleSnapshot.CalledPitchMode/ZoneModel/RulesSchema/CalledPitchModeDeclared/FramingMayChangeCalls()` | C40 called-pitch mode, optional pair, `gamerules-v1` / `gamerules-v2` |

Infrastructure: `GameRulesJson.Read` reads `format_version` 1 (legacy, unchanged) and 2 (requires `called_pitch_mode` and `zone_model`); new fictional pack `content/rules/game_rules_profiles_v2.json` (two profiles: `test-pzl-automated`, `test-pzl-non-automated`), embedded in the Infrastructure assembly. Not changed: `GameEvents.cs`, `GameReducer*`, `EventFingerprint` (`ef1`), `Randomness/`, `game_rules_profiles.json`, `game_rules_evidence.json`, `ScriptedGameSource` loading.

## 2. The 17 zones and the boundary classification

Inner `in.r1c1`..`in.r3c3` (row 1 high -> 3 low, column 1 inside -> 3 outside, batter-relative) and outer `out.high`, `out.high_in`, `out.in`, `out.low_in`, `out.low`, `out.low_out`, `out.out`, `out.high_out`. Inside/outside always mean toward/away from the batter; the catcher's view is derived with the applied side (right-handed batter: inside = catcher's left; a model convention). The 17 zones map one-to-one to catcher's-view positions for both sides.

Boundary table (a pure function of the zone, fixed by `pzl-zones-v1`, frozen before any calibration): a region sharing an edge with the strike-zone outline is `Borderline` (12 zones: the eight non-centre inner cells and `out.high`, `out.in`, `out.low`, `out.out`); the centre cell and the four diagonal outer zones are `Clear` (5 zones). `PitchLocation.Create` accepts only the table's class for the zone. The classification is virtual: it is not a measurement and claims nothing about real KBO ABS accuracy. The design trade-off (12 of 17 zones are Borderline, so Framing's cap in 3-E must carry the magnitude; a finer table needs a new model version) is recorded in ADR-005 item 2.

## 3. Tendency vector policy

Nine integers, row-major inner cells, `-100..+100`, default all zero; value = fraction of a per-zone cap defined in 3-B2 (+ = batter's favour). Wrong length, a missing vector/reference, a value outside the bound (upper and lower checked separately) or a non-neutral weighted sum are rejected with stable codes. Neutrality is exact against the stored `ZoneReferenceDistribution` (`UniformV1` is interim; a tuned reference is a 3-B2 decision under a new id). Outer zones are not adjusted. Immutable, equal by content, no link to Ratings/Contact, no growth. Hidden: `ToString()` redacts, and `HiddenTendencyBoundaryTests` fails if any Application/Infrastructure member signature carries the type (so no query, DTO, tooltip, news/Gemini payload or opponent-AI input can); the Godot client cannot name Domain. No probability uses it yet.

## 4. GameRules compatibility result

| Check | Result |
|---|---|
| Seven legacy `ContentHash` values | byte-identical to values computed independently by `tests/Fixtures/generator/legacy_gamerules_hashes.py` **before any code change**; pinned in `GameRulesV2ProfileTests` |
| Legacy profiles (incl. `kbo-oriented-unverified-v0`) | `CalledPitchMode == null`, `ZoneModel == null`, schema `gamerules-v1`; "not declared" preserved, never read as Automated, NonAutomated or the real 2026 KBO ABS mode; `FramingMayChangeCalls()` -> `GameRules.called_pitch_mode_undeclared` |
| `game_rules_profiles.json` | untouched; a format-1 file that sneaks in `called_pitch_mode` is rejected (unknown member) |
| New profiles | both fields required together (`pitch_model_incomplete`), unknown mode `Fixture.enum_invalid`, unknown zone model `ZoneModelVersion.unsupported`, other format numbers `Fixture.version_unsupported`, v2 hash appends `\|gamerules-v2\|mode\|zone model` and differs from every legacy hash |
| Automated => Framing influence | contract: exactly zero (`FramingMayChangeCalls() == false`); NonAutomated => may act on Borderline pitches within a cap, never changing a committed call (cap and computation: 3-E) |
| Phase 2 | the six diagnostic games still load and all 598 Phase 2 tests pass unchanged |

## 5. Files changed

New: `src/StarPlayer.Domain/Baseball/PitchZone/{PitchZoneModel,PitchLocation,BatterZoneTendency}.cs`; `content/rules/game_rules_profiles_v2.json`; tests `tests/StarPlayer.Domain.Tests/{PitchZoneModelTests,BatterZoneTendencyTests,GameRulesCalledPitchModeTests}.cs`, `tests/StarPlayer.Infrastructure.Tests/GameRulesV2ProfileTests.cs`, `tests/StarPlayer.Application.Tests/HiddenTendencyBoundaryTests.cs`; `tests/Fixtures/generator/legacy_gamerules_hashes.py`; `docs/decisions/ADR-005.md`; this report. Modified: `src/StarPlayer.Domain/Baseball/Game/GameRules.cs` (additive; legacy canonical text frozen), `src/StarPlayer.Infrastructure/Fixtures/ScriptedGameJson.cs` (v2 reader; v1 path unchanged), `src/StarPlayer.Infrastructure/StarPlayer.Infrastructure.csproj` (embeds the v2 file), `docs/game-engine.md` (section 18), `docs/SYSTEM_CONTRACTS.md` (C08/C40 status), `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`.

## 6. Tests and results (Windows 11, .NET SDK 8.0.425)

Tests were written first against the new API; the 87 new tests were then run against the implementation. A separate pre-implementation failing run was not captured (the tests do not compile without the types), so there is no RED log for this package; the substitute is the mutation check below.

| Command | Result |
|---|---|
| `dotnet test tests\StarPlayer.Domain.Tests` | 216 passed (145 before; +71: `PitchZoneModelTests` 42, `BatterZoneTendencyTests` 20, `GameRulesCalledPitchModeTests` 9, counted per class with a filter) |
| `dotnet test tests\StarPlayer.Infrastructure.Tests` | 93 passed (79 before; +14 `GameRulesV2ProfileTests`) |
| `dotnet test tests\StarPlayer.Application.Tests` | 117 passed (114 before; +3 `HiddenTendencyBoundaryTests`) |
| `dotnet test tests\StarPlayer.Simulation.Tests` | 371 passed, unchanged (includes the 111 Randomness tests) |
| `dotnet test StarPlayer.sln -c Release` | **797 passed**, 0 failed, 0 skipped (Domain 216, Simulation 371, Application 117, Infrastructure 93; previous baseline 709, +88 new) |
| Mutation check of the new logic | 15 mutations applied one at a time to Domain code (boundary table x2, catcher-view mirror, boundary-mismatch check, switch-hitter side, neutrality check, tendency bound, outer-zone adjustment, text redaction, legacy hash suffix, framing in Automated, half-declared profile, legacy-as-non-automated, undefined mode); all detected. One survivor was found at first (the upper bound alone: the earlier test only broke the lower bound), a test was added for each bound separately, then upper-only and lower-only mutations were both detected |

Covered per the instruction list: (1) 17-id uniqueness, (2) 9 inner / 8 outer, (3) left/right catcher's-view conversion, (4) switch-hitter applied side preserved and distinguishable, (5) deterministic classification and allowed combinations, (6) rejected zone / version / side, (7) neutral default, (8) length and bound checks, (9) legacy hash unchanged, (10) new profile mode/version validation, (11) existing Phase 2 profile/fixture compatibility (the six diagnostic games, 598 tests), (12) same input -> same result.

## 7. Regression and Gate verification

Common Gate G item 1 (tests with commands and counts): above. Item 2 (no regression): all earlier tests pass unchanged; no RNG, golden vector, `ef1` or reducer file changed. Items 3-4: not applicable (no durable state, no UI change). Item 5: the Phase 3 acceptance rows (100,000 PA, 100 games, calibration, the other Pitch Zone Lite checks of ROADMAP Phase 3) belong to later packages and are **INCOMPLETE**; this report claims only the 3-B1 data contracts. Item 6: this report, `CURRENT_STATE.md` and the section 18 / C08 / C40 updates.

`tools\verify.ps1`: see Part 10.

## 8. Handoff to 3-B2 (not implemented here)

Fix before any calibration run: the integer probability scale and fixed denominator; the per-zone tendency cap (the unit of the `-100..+100` values); the tuned or confirmed uniform reference distribution; the virtual baseline conditional probabilities by zone / boundary class / call mode; the combination order with ability, matchup, state, C46 park and catcher/battery inputs without double counting; the cap values and seed sets. Inputs the model receives are listed in `docs/game-engine.md` section 18. Open for 3-C: the number and order of `game/location` draws (a zone draw; the class needs none), the switch hitter's per-pitch side rule, the `PitchEvent`/`TakeEvent`/`InPlayEvent` payload and `ef2` (3-C attaches to `PitchZoneModel.V1` and `PitchLocation.FromZone`). Open for 3-E: Framing cap and calculation. Open owner item: which `CalledPitchMode` a production KBO-oriented profile should declare (the legacy one declares none).

## 9. Issues, debt and deviations

- No deviation from the plan. Interpretation choices recorded in ADR-005: class as a function of the zone (not drawn), one version for zones plus table, integer tendency scale, `null` mode for legacy.
- Weak point to review: 12 of 17 zones being `Borderline` makes the class coarse; accepted as a documented simplification, adjustable only through a new model version.
- `UniformV1` is an interim reference, not a statement about real pitch locations.
- Unreferenced data: `game_rules_profiles_v2.json` is embedded and tested but no game loads it until 3-C.
- Independent review of the data contracts: Codex `미실시` (recommended before 3-C because they become event payloads and persisted values). GPT Chat review: pending.

## 10. `tools\verify.ps1`

Two consecutive runs after all code and documents were final: `powershell -NoProfile -ExecutionPolicy Bypass -File toolserify.ps1 -LogName PHASE_3B1_VERIFY_LOG_run1.md` and `... _run2.md` - both **`GATE: PASS`, exit 0**: Release build of 9 projects with 0 warnings, Domain 216, Simulation 371, Application 117, architecture 14, determinism detector 48, Infrastructure 93, Godot headless checks (SQLite diagnostics 8/8, Phase 1 card self-check 7/7 with no hidden-field leak, Phase 2 scripted games 6/6), Windows release export and cold-copy runs (QHD screenshots), relative doc links (63 and 64 files). Logs: `docs/reports/PHASE_3B1_VERIFY_LOG_run1.md`, `docs/reports/PHASE_3B1_VERIFY_LOG_run2.md`. Not executed: manual Godot UI clicks and FHD (no UI change), performance measurement, any Phase 3 Gate item (100,000 PA, 100 games, calibration), independent review of this package (`미실시`). Other checks: `git diff --check` exit 0 for the tracked changes; the new files were scanned for trailing whitespace (none); `git diff` of `GameEvents.cs` and `src/StarPlayer.Simulation` is empty.

## 11. Git state

Base `f2e808f`; all 3-B1 work is uncommitted in the working tree on `main`; nothing staged, committed or pushed. `TASK.md` and the two rule-book PDFs remain untracked as before.

## 12. Self-assessment

**PASS WITH NOTES** for work package 3-B1: every requested data contract is implemented and every executed check passes (797 tests, 15 mutations detected, `verify.ps1` twice `GATE: PASS`); notes are the coarse boundary table, the interim reference distribution, the missing RED log, the unused v2 pack and the pending independent review (Part 9). Not a Phase 3 Gate result and not an approval; Phase 3-B2 is not started.
