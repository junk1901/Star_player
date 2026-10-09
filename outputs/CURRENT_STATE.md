# Current State

## Current Phase

Targeted owner roadmap revisions R1–R5 complete as documentation. D1–D6 are approved, Club Finance Lite is recorded as one bounded owner-approved exception to the existing Scope Lock, the lock is active again, and production Phase 0 has not started or been approved for implementation in this chat.

## Last Completed Phase

None. Documentation completion does not count as a passed game-development phase.

## Working Systems

No game systems implemented or runtime-tested. The package contains the proposed 30-phase roadmap (0–29), 48 system contracts, UI route/layout specification, source traceability, and unchanged G/U/master/R/Portrait source copies. P remains independent; the 0–29 numbering, order and dependency edges are preserved.

## Current Blockers

No owner decision remains pending. Implementation gates still require each phase's detailed D2 RuleProfile subset before use, including evidence-backed salary-cap scope/calculation/exemptions/assessment/consequences before Phase 14. Phase 27 still needs the personal KBO 2026 exact `as_of_date`, evidence cutoff, supplied datasets and provenance, plus any real club finance inputs that are to be certified rather than labeled fictional/game-model estimates. Those are rule-specification/content-certification dependencies under approved policy. Phase 0 still requires separate owner authorization.

## Important Architecture Decisions

Modular in-process C# core, one application command queue, shared seeded game engine, normalized SQLite state, immutable save generations with migration-on-copy, explicit public projections, the explicit KBO Rule Engine, independent catcher/C proficiency and pair chemistry, rule-based manager/club policies, and Gemini expression with one validated AI-native interview bridge. R2 adds bounded Stadium/ParkFactor, weather/rescheduling, annual salary, staff, captain, atmosphere and All-Star systems. R3 adds C47 dated club-specific jersey assignments plus data-driven imported/generated draft source, locked completed imports, persistent generated classes and Existing Player Start for imported rookies with stable identity. R4 approves D1–D6 policies. R5 adds C48 Club Finance Lite: C13 remains the sole contract-obligation authority; total payroll, cap payroll and player budget stay distinct; C40 owns frozen SalaryCapRules; finalized C10 facts drive idempotent club sponsor settlements; rule-based Club AI consumes bounded finance projections; UI is read-only. Seed/profile base is Phase 24; optional selected Gemini portraits are Phase 26. These are planning decisions to follow only when the owning phase is approved.

## Known Issues

No codebase has been built or tested. Toolchain versions and desktop export feasibility must be demonstrated in Phase 0. Current real 2026 data and historical draft file are not supplied, and the exact preseason `as_of_date`/evidence cutoff/source provenance remain Phase 27 content-certification work; the approved preseason policy itself is settled. Detailed 2026 KBO RuleProfile values and documented simplifications, including salary-cap editions/calculation/penalties, remain to be specified incrementally before their consuming phases. No verified real club sponsorship amounts or confidential budgets were supplied; fictional/game-model finance assumptions must be labeled unless later certified. Portrait design remains approved and preserved unchanged. Gemini text/image/Batch capability versions must be verified in Phase 26; no present API/model/pricing capability is claimed. No revision required a new top-level phase. Detailed discipline, comprehensive franchise finance and all further unapproved gameplay ideas remain deferred under the renewed scope lock.

## Next Task

Review the revised documentation package. After separate authorization to implement, give Claude Code Phase 0 using the ready-to-copy handoff in ROADMAP §9.1. This revision does not begin Phase 0. Implement only foundation/export/SQLite feasibility and test boundaries, then report its actual gate result.

## Relevant Documents

- `AGENTS.md`
- `ROADMAP.md` §§1–6, Phase 0, §9
- `docs/SYSTEM_CONTRACTS.md` shared conventions and C33/C34 for future boundary context
- `docs/UI_ARCHITECTURE.md` shell boundary
- `docs/REQUIREMENTS_TRACEABILITY.md`
- `sources/SOURCE_MANIFEST.md`
- `sources/ROADMAP_REVISION_REQUEST.txt`
- `Star_Player_Portrait_System_Design.MD`
