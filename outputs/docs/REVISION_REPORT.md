# Star Player Club Finance Lite Revision Report

Status: planning revision R5 complete. Club Finance Lite is integrated as one owner-approved bounded Scope Lock exception. The Scope Lock is active again. No production code, scene, SQL migration, datapack build or Phase implementation was created.

## Files changed

- [ROADMAP.md](../ROADMAP.md): C48 ownership, phase integrations, persistence, tests, risks and renewed Scope Lock.
- [SYSTEM_CONTRACTS.md](SYSTEM_CONTRACTS.md): added C48 and reconciled C06/C10/C13/C18/C19/C25/C27/C34/C36/C38/C40.
- [UI_ARCHITECTURE.md](UI_ARCHITECTURE.md): Korean read-only finance summaries in existing Team/Contract/League routes.
- [REQUIREMENTS_TRACEABILITY.md](REQUIREMENTS_TRACEABILITY.md): added R101–R107.
- [AGENTS.md](../AGENTS.md), [CURRENT_STATE.md](../CURRENT_STATE.md), [SOURCE_MANIFEST.md](../sources/SOURCE_MANIFEST.md): updated authority, invariants, state and remaining rule/data dependencies.
- Obsidian dashboard and decision note: recorded the approved exception and renewed lock.
- Planning validators: updated for 48 contracts, R101–R107 and Club Finance Lite invariants.

## Domain and ownership decisions

C48 owns versioned club financial policy, annual player-budget allocations, abstract club sponsorship agreements/settlements and historically consequential salary-cap assessments. C13 remains the sole authority for contracts and salary obligations; C25 credits the player's personal ledger from the same unique obligation. C48 derives payroll and never creates a second contract or payment database.

Total Player Payroll, Salary-Cap Payroll and Club Player Budget are separate. C40 SalaryCapRules own scope, included components, exemptions, assessment dates, threshold and excess consequence. Sponsorship revenue cannot change the legal cap threshold automatically.

## Sponsorship and Club AI

Annual base revenue and performance/postseason/championship bonuses settle only from finalized C10 facts and unique evidence receipts. Renewal uses deterministic smoothing, baseline support and bounded annual/minimum/maximum policies. ClubPhilosophy supplies bounded spending and caution preferences; C13/C18/C19 consume C48 assessments for annual salary, FA and trades. Gemini has no finance authority.

## Phase changes

- Phase 9: financial preference inputs only.
- Phase 11: durable achievement evidence with no Phase 14/C48 dependency.
- Phase 14: C48 core, payroll, budget, cap, sponsorship and renewal.
- Phase 15: FA offer financial projections.
- Phase 16: trade/release incoming and outgoing obligations.
- Phase 17: five 20-season stability/recovery audits.
- Phase 23: retained annual finance history.
- Phase 24: complete read-only finance UI.
- Phase 27: optional baseline/provenance and SalaryCapRules validation.
- Phase 29: three 50-season migration, stability and reconciliation audits.

Phase 22 now explicitly keeps individual player sponsorship separate from club sponsorship. No direct dependency edge changed.

## UI, persistence and tests

Existing routes may show `구단 연봉 총액`, `샐러리캡 산정액`, `샐러리캡 기준액`, `샐러리캡 초과 현황`, `선수 영입 예산`, `예상 연봉 부담`, `스폰서 수익`, `시즌 성과 보너스` and `구단 재정 현황`. They provide no budget, sponsor or owner controls.

Planned persistence retains policies/provenance, annual budgets, agreement periods, unique settlements and saved assessments without duplicating contracts. Planned tests cover exact payroll reconciliation, cap inclusion/exclusion/consequences, settlement timing/idempotency, transactional rollback, save/recovery/migration, bounded renewal, sustainable AI decisions and 20/50-season wealth/recovery distributions.

## Remaining implementation-specific dependencies

Before Phase 14, reliable evidence must specify the applicable SalaryCapRules edition, included players/components, exemptions, calculation dates, threshold, assessment method and consequences, with any game simplification documented. Phase 27 must label finance inputs as fictional/game-model estimates unless real values and provenance are supplied and certified. No official KBO amount or procedure was invented in this revision.

## Final confirmation

D1–D6 remain approved and unchanged. The roadmap still contains exactly **30 phases numbered 0–29** with the same order and direct dependencies. The Scope Lock remains active after this approved exception. No playable GM/owner mode, comprehensive finance simulator, production code or Phase 0 work was implemented.
