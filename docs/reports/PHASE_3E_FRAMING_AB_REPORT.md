# Phase 3-E Framing v3 (Packages A + B) — Implementation Report

- Date: 2026-10-16 · Executor: Claude Code (Sonnet 5.5) · Branch `main` · Base HEAD `b64d3e9b07c931527d018d012f6c5ef5ef8939b8`
- Authority: owner instruction `TASK.md` "Star Player Phase 3-E Framing v3 Integrated Implementation"; owner policies E1-E7; Codex architecture verdict `CODEX_PHASE3_E_ARCH_PASS_WITH_NOTES` (named in the instruction; **the review text is not stored in the repository**, so the notes are recorded as the owner-approved contract and are not quoted).
- Verdict: `PHASE3_E_FRAMING_V3_AB_IMPLEMENTED_WITH_NOTES` — Packages A and B only. **The Phase 3-E gate as a whole is `INCOMPLETE` (Game Calling is not implemented); the Phase 3 Gate stays `INCOMPLETE`.** Review status: `REVIEW_PENDING` (no GPT Chat / Codex review has taken place).

## Scope delivered

| Package | Delivered |
|---|---|
| A | `ModifierConditionScope` (`All`, `NonAutomatedBorderline`); strict `ConditionKeys.ParseCalledPitch`; `prob-model-v3`; `tuning-virtual-proto-v3` (`content/tuning/virtual_tuning_v3.json`, hash `682a5f06b46ae607ef73c7d66236f7a8bceebc69b15cffe98b005d94f33e389a`); explicit model/tuning pair table (`ProbabilityModel.TuningFor` / `Pairs`); `GameExecutionRules` for the v2/v2 and v3/v3 pairs with the selected model's park range |
| B | `PlayerInputSet` content v1 (identity unchanged) and v2 (`CreateForFraming`, `ReceivingFraming`); `FrozenCatcherInput`; `Prepare` / `PitchBranchRequest` / resolver wiring; `CheckPrepared` step 6c; `CommitEvidence.LinkCalledPitch` |
| Docs | `CURRENT_STATE.md`, `ROADMAP.md` (status note, no plan change), `docs/game-engine.md` §32, `docs/data-model.md`, `docs/SYSTEM_CONTRACTS.md`, ADR-005/006/007 addenda, `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/02_Decisions/2026-10-16_Framing_V3_AB.md` |

Not implemented (by instruction): Game Calling, `battery.chemistry`, Phase 3-F, Phase 4 persistence, a production gameplay runner. No new stage, RNG stream or draw.

## Gate results

| Gate | Result | Evidence |
|---|---|---|
| G1 start Git state | PASS | branch `main`, HEAD `b64d3e9...`, 0 tracked changes, 0 staged, 3 untracked user files |
| G2 v1/v2 protected | PASS | v1 `5b70dca7...3ea5` and v2 `7f02cb4f...dd70` still pinned and green; v1/v2 JSON and models not edited; v2 framing still reserved |
| G3 v3 separate | PASS | own model, tuning file, version texts, hash (independent Python = C#) |
| G4 explicit pairs | PASS | `TuningV3Tests` (six crossed pairs, both directions), `ProbabilityModelV3Tests`, `GameExecution` cases |
| G5 NonAutomated + Borderline | PASS | oracle matrix, 12 zones x ratings |
| G6 Automated / Clear not applied | PASS | `Applied=false`, factor 1,000,000, distribution = baseline (34 rows) |
| G7 invalid condition fail-closed | PASS | 25 malformed keys, 11 unregistered keys, unknown stage / slot, scope on non-called-pitch keys |
| G8 slope / cap | PASS | 950,000 / 1,000,000 / 1,050,000 and 4 further ratings; combined cap never reached |
| G9 content v1 ID unchanged | PASS | literal `a2240615...7987` computed independently |
| G10 content v2 ID deterministic | PASS | literal `a3ab8c93...146f`, fixed null token, order-free |
| G11 catcher input frozen at Prepare | PASS | `FramingInputTests` |
| G12 catcher change / rating change detected | PASS | `catcher_changed`, `framing_changed`, `player_inputs_changed`, other game / set |
| G13 commit bound to the real catcher | PASS | re-derived from real state and set; forged consistent catcher id refused |
| G14 forged distribution / result refused | PASS | 16 `FramingCommitTests`, each refusal leaves aggregate, cursor, RNG, last commit unchanged |
| G15 RNG stage / stream / draw unchanged | PASS | identical steps, rolls, draws and H1 for ratings 0 / 50 / 100; neutral = model-v2 pitch for pitch |
| G16 CAS / commit / finalize atomicity | PASS | existing 3-C3 suites unchanged and green; single-use publish re-tested for v3 |
| G17 goldens / ef1 / ef2 | PASS | no golden file, hash or receipt changed |
| G18 independent oracle | PASS | `generate_phase3e_framing_oracle.py` (238 cases), no shared code |
| G19 new tests | PASS | +134 tests |
| G20 full xUnit Release | PASS | see below, exit code 0 |
| G21 self review | PASS | one unused member removed; production diff re-read |
| G22 docs | PASS | list above |
| G23 user files preserved | PASS | SHA-256 of `TASK.md` and both PDFs identical before and after |
| G24 no staging / commit / push | PASS | `git diff --cached` empty; no commit, no push, no fetch / pull / reset / rebase |
| G25 Game Calling reserved | PASS | `catcher.game_calling` and `battery.chemistry` still reserved, neutral-only |
| G26 Phase 3-E not complete | PASS (stated) | Game Calling open |
| G27 Phase 3 Gate | `INCOMPLETE` | unchanged |

## Tests

| Project | PASS | FAIL | SKIP |
|---|---:|---:|---:|
| Domain | 316 | 0 | 0 |
| Application | 127 | 0 | 0 |
| Simulation | 728 | 0 | 0 |
| Infrastructure | 1,003 | 0 | 0 |
| **Total** | **2,174** | 0 | 0 |

`dotnet build StarPlayer.sln -c Release --no-incremental` 0 warnings, 0 errors; `dotnet test StarPlayer.sln -c Release --no-build` exit code 0 (before: 2,040). Not run: Codex, full Mutation, `tools/verify.ps1`, 100,000-PA / 100-game validation (`NOT_RUN`).

Six existing test methods were changed on purpose (none weakened): four used v3 as the "unknown version" example (now v4), one pin test lists the new `ModifierInput` creator, one forged draft was made coherent (see ADR-007).

## Sabotage (local; every file restored, SHA-256 verified)

18 runs, 17 DETECTED, 1 NOT_DETECTED. The undetected run (`CommitEvidence` trusts the prepared catcher while `CheckPrepared` still re-derives it) is behaviourally equivalent: step 7a runs first on the same state and set. Trusting the audit catcher in both places is detected (6 tests). Six first attempts did not compile (`if (false)` -> CS0162 as error) and were re-run in a compiling form. Full detail: `docs/game-engine.md` §32.

## NOTES (open)

1. Game Calling and `battery.chemistry` — not implemented (Phase 3-E gate open).
2. KBO RuleProfile — the Codex review did not verify the official 2026 KBO rules; the test profiles in `game_rules_profiles_v2.json` are not relabelled as official and no real-ABS certification is claimed. A production profile (source, edition, effective date, simplifications) is separate work, not a precondition of this implementation.
3. Phase 4 — persist `GameExecution`, the PlayerInputSet content VERSION, content and identity together, and the world format; none exists.
4. A production gameplay runner does not exist (the test harness only chooses approaches).
5. Phase 3-F — balance of the virtual Framing numbers and all calibration is not run.
6. Phase 3-D commit state correction: the Park / Fielding / Advancement v2 work is committed locally as `b64d3e9` (BatterTendency v2 `054704d`); neither is pushed. The earlier "not committed" wording in the 3-D paragraphs is superseded.
