# Current State

## Current Phase

No phase is in progress. Phase 0 — Reproducible desktop foundation — is **complete and approved** (2026-10-10): the independent Codex architecture review returned `CHANGES REQUIRED` (5 findings), they were fixed and re-verified the same day, and the GPT Chat final review result `승인 완료` was relayed by the owner (Obsidian review state `승인 완료`; Codex re-review of the fixes `미실시`). Gate after remediation recorded by `tools/verify.ps1`: PASS (`docs/reports/PHASE_0_VERIFY_LOG.md`; the pre-Codex run is preserved in `PHASE_0_VERIFY_LOG_initial.md`). **Phase 1 is not started** and needs a separate owner instruction.

## Last Completed Phase

Phase 0 — Reproducible desktop foundation (approved 2026-10-10; `docs/reports/PHASE_0_REPORT.md`, `Star/01_Phase_Reviews/Phase_00_Review.md`).

## Working Systems

Phase 0 only (no game systems): solution with five boundary projects plus four test projects and a Godot client; typed IDs, `WorldRevision` and `Result` conventions; Application port `IStorageDiagnostics` with a SQLite adapter that proves commit/rollback/two-connection FK/backup/reopen; a Korean diagnostic shell (`MainMenu`); architecture-dependency tests; `tools/verify.ps1`. Verified after the Codex fixes: Release build 0 warnings with the client really built as Release, 82 xUnit tests passing without Godot (architecture boundary checks incl. the compiled client assembly, 48 determinism-detector tests, SQLite integration), Godot headless diagnostics, a fresh ExportRelease Windows export and a cold-copy run of it with 8/8 SQLite steps. The Godot client no longer references Domain/Simulation; presenters see Application DTOs only. Pins: .NET SDK 8.0.425, Godot 4.7.2 Mono, Microsoft.Data.Sqlite 8.0.31 (`docs/toolchain.md`, `docs/decisions/ADR-001.md`). The planning package (30-phase roadmap, 48 contracts, UI architecture, traceability, unchanged G/U/master/R/Portrait sources) now lives at the repository root instead of `outputs/`.

## Current Blockers

No owner decision remains pending. Implementation gates still require each phase's detailed D2 RuleProfile subset before use, including evidence-backed salary-cap scope/calculation/exemptions/assessment/consequences before Phase 14. Phase 27 still needs the personal KBO 2026 exact `as_of_date`, evidence cutoff, supplied datasets and provenance, plus any real club finance inputs that are to be certified rather than labeled fictional/game-model estimates. Those are rule-specification/content-certification dependencies under approved policy. Phase 0 was authorized, implemented and approved on 2026-10-10; Phase 1 and every later phase still require separate owner authorization.

## Important Architecture Decisions

Modular in-process C# core, one application command queue, shared seeded game engine, normalized SQLite state, immutable save generations with migration-on-copy, explicit public projections, the explicit KBO Rule Engine, independent catcher/C proficiency and pair chemistry, rule-based manager/club policies, and Gemini expression with one validated AI-native interview bridge. R2 adds bounded Stadium/ParkFactor, weather/rescheduling, annual salary, staff, captain, atmosphere and All-Star systems. R3 adds C47 dated club-specific jersey assignments plus data-driven imported/generated draft source, locked completed imports, persistent generated classes and Existing Player Start for imported rookies with stable identity. R4 approves D1–D6 policies. R5 adds C48 Club Finance Lite: C13 remains the sole contract-obligation authority; total payroll, cap payroll and player budget stay distinct; C40 owns frozen SalaryCapRules; finalized C10 facts drive idempotent club sponsor settlements; rule-based Club AI consumes bounded finance projections; UI is read-only. Seed/profile base is Phase 24; optional selected Gemini portraits are Phase 26. These are planning decisions to follow only when the owning phase is approved.

## Known Issues

The owner reported a passing manual UI test of the pre-Codex-fix EXE (launch, QHD, Korean text, both button clicks, 8/8 result, normal exit; no screenshot supplied) and later stated that the manual click test of the rebuilt export was completed (no per-item details supplied — `docs/reports/PHASE_0_MANUAL_TEST.md`). `verify.ps1` itself does not click buttons. Why the reviewer's machine lacked the Godot export templates is unknown (they were present here; `tools/install-export-templates.ps1` restores them). The exported exe has no custom icon/version resource (rcedit not used), Korean text relies on system-font fallback, and the export is a folder (exe + pck + runtime data), not a single file. Only QHD was verified; FHD is Phase 5+ work. Current real 2026 data and historical draft file are not supplied, and the exact preseason `as_of_date`/evidence cutoff/source provenance remain Phase 27 content-certification work; the approved preseason policy itself is settled. Detailed 2026 KBO RuleProfile values and documented simplifications, including salary-cap editions/calculation/penalties, remain to be specified incrementally before their consuming phases. No verified real club sponsorship amounts or confidential budgets were supplied; fictional/game-model finance assumptions must be labeled unless later certified. Portrait design remains approved and preserved unchanged. Gemini text/image/Batch capability versions must be verified in Phase 26; no present API/model/pricing capability is claimed. Detailed discipline, comprehensive franchise finance and all further unapproved gameplay ideas remain deferred under the renewed scope lock.

## Next Task

Wait for the owner's Phase 1 instruction (Phase 1 will need its own handoff from ROADMAP §9). Do not start Phase 1 on its own. Optional follow-ups noted by the review: Codex re-review of the Phase 0 fixes, FHD layout (Phase 5+), export on another PC.

## Relevant Documents

- `AGENTS.md`
- `docs/toolchain.md`, `docs/decisions/ADR-001.md`, `docs/reports/PHASE_0_REPORT.md`
- `ROADMAP.md` §§1–6, Phase 0, §9
- `docs/SYSTEM_CONTRACTS.md` shared conventions and C33/C34 for future boundary context
- `docs/UI_ARCHITECTURE.md` shell boundary
- `docs/REQUIREMENTS_TRACEABILITY.md`
- `sources/SOURCE_MANIFEST.md`
- `sources/ROADMAP_REVISION_REQUEST.txt`
- `Star_Player_Portrait_System_Design.MD`
