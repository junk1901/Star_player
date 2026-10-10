# Current State

## Current Phase

No phase is in progress. **Phases 0 and 1 are complete and approved.** Phase 0 — approved 2026-10-10. Phase 1 — Player identity, visibility, and baseball vocabulary — **`승인 완료` (PASS WITH NOTES), 2026-10-10**: GPT Chat final review relayed by the owner after the Codex `CHANGES REQUIRED` (2 High, 2 Medium) was fixed and the Codex final re-review returned `PASS WITH NOTES`; 289/289 xUnit, two consecutive full `tools/verify.ps1` PASS, fresh export with SQLite 8/8, and the owner's post-fix manual UI check completed (no per-item details supplied). **Phase 2 is not started** and needs a separate owner instruction. All Phase 1 work is **uncommitted** on `main` (HEAD `e28cb8d`); commit/push only on a separate instruction.

## Last Completed Phase

Phase 1 — Player identity, visibility, and baseball vocabulary (approved 2026-10-10; `docs/reports/PHASE_1_REPORT.md`, `Star/01_Phase_Reviews/Phase_01_Review.md`), after Phase 0 (`docs/reports/PHASE_0_REPORT.md`).

## Working Systems

Phase 0 foundation plus Phase 1 data model (no game simulation): Domain player model (identity, 0–100 ratings incl. four catcher tools, transient state, hidden `DevelopmentProfile` kept out of `PlayerSheet`, position/role proficiency, separate origin/entry/registration/league/affiliation axes), `Money`, `InningsOuts`, `BatteryPairId`, KBO Rule Engine area/version boundary (no rule content), Stadium/ParkFactor vocabulary, C47 `JerseyNumberLedger`; Application `PlayerVisibilityPolicy` (own player exact, others `observation-v0` grade/range/confidence, session-owned `ViewerContext`, cheat mode hides potential too), `PlayerInfoService`, self-check; Infrastructure JSON pack reader/writer (fictional 2-club/10-player pack embedded); Godot `선수 정보 진단` screen (QHD). Visibility is structural: raw world data, the world port, `ViewerContext` and the service constructor are `internal` (Application) and the raw fixture source/JSON reader are `internal` (Infrastructure); `InternalsVisibleTo` excludes the Godot client; the client's only entry point is `PlayerInfoComposition.CreateFictionalPhase1`; "my player" comes from the fixture binding, not from any option. Verified: Release build 0 warnings, **289 xUnit tests passing** (Domain 138, Simulation 1, Application 112, Infrastructure 38; was 254 before the Codex fixes, Phase 0's 82 included), Godot headless player-card self-check 7/7 with 0 hidden-identifier leaks, fresh Windows export and cold-copy run (SQLite 8/8, player card at 2560×1440). Pins unchanged: .NET SDK 8.0.425, Godot 4.7.2 Mono, Microsoft.Data.Sqlite 8.0.31 (`docs/toolchain.md`, `docs/decisions/ADR-001.md`; Phase 1 decisions in `docs/decisions/ADR-002.md`, data dictionary in `docs/data-model.md`).

## Current Blockers

No owner decision remains pending. Implementation gates still require each phase's detailed D2 RuleProfile subset before use, including evidence-backed salary-cap scope/calculation/exemptions/assessment/consequences before Phase 14. Phase 27 still needs the personal KBO 2026 exact `as_of_date`, evidence cutoff, supplied datasets and provenance, plus any real club finance inputs that are to be certified rather than labeled fictional/game-model estimates. Those are rule-specification/content-certification dependencies under approved policy. Phase 0 and Phase 1 were each authorized separately (Phase 0 approved 2026-10-10; Phase 1 awaiting review); Phase 2 and every later phase still require separate owner authorization. Phase 2 will additionally need its detailed `GameRules` (D2) subset specified first.

## Important Architecture Decisions

Modular in-process C# core, one application command queue, shared seeded game engine, normalized SQLite state, immutable save generations with migration-on-copy, explicit public projections, the explicit KBO Rule Engine, independent catcher/C proficiency and pair chemistry, rule-based manager/club policies, and Gemini expression with one validated AI-native interview bridge. R2 adds bounded Stadium/ParkFactor, weather/rescheduling, annual salary, staff, captain, atmosphere and All-Star systems. R3 adds C47 dated club-specific jersey assignments plus data-driven imported/generated draft source, locked completed imports, persistent generated classes and Existing Player Start for imported rookies with stable identity. R4 approves D1–D6 policies. R5 adds C48 Club Finance Lite: C13 remains the sole contract-obligation authority; total payroll, cap payroll and player budget stay distinct; C40 owns frozen SalaryCapRules; finalized C10 facts drive idempotent club sponsor settlements; rule-based Club AI consumes bounded finance projections; UI is read-only. Seed/profile base is Phase 24; optional selected Gemini portraits are Phase 26. These are planning decisions to follow only when the owning phase is approved.

## Known Issues

The owner reported a passing manual UI test of the pre-Codex-fix EXE (launch, QHD, Korean text, both button clicks, 8/8 result, normal exit; no screenshot supplied) and later stated that the manual click test of the rebuilt export was completed (no per-item details supplied — `docs/reports/PHASE_0_MANUAL_TEST.md`). `verify.ps1` itself does not click buttons. Why the reviewer's machine lacked the Godot export templates is unknown (they were present here; `tools/install-export-templates.ps1` restores them). The exported exe has no custom icon/version resource (rcedit not used), Korean text relies on system-font fallback, and the export is a folder (exe + pck + runtime data), not a single file. Only QHD was verified; FHD is Phase 5+ work. Current real 2026 data and historical draft file are not supplied, and the exact preseason `as_of_date`/evidence cutoff/source provenance remain Phase 27 content-certification work; the approved preseason policy itself is settled. Detailed 2026 KBO RuleProfile values and documented simplifications, including salary-cap editions/calculation/penalties, remain to be specified incrementally before their consuming phases. No verified real club sponsorship amounts or confidential budgets were supplied; fictional/game-model finance assumptions must be labeled unless later certified. Portrait design remains approved and preserved unchanged. Gemini text/image/Batch capability versions must be verified in Phase 26; no present API/model/pricing capability is claimed. Detailed discipline, comprehensive franchise finance and all further unapproved gameplay ideas remain deferred under the renewed scope lock. Phase 1 additions: the new screen was verified by automated launch modes and screenshots only for the fixed build (a human button click on the fixed build, FHD and the Codex re-review are open); `observation-v0`, the 850–1150 ‰ park-factor cap, the 0–99 jersey range and the personality-trait list are fixture/vocabulary values, not approved tuning; event payloads have no serializer yet; **tracked debt (Codex Medium Note): `Ratings.cs`, `Proficiency.cs`, `PlayerStatus.cs`, `DevelopmentProfile.cs`, `Player.cs` expose `List`-backed read-only lists — before Phase 2 uses them in game state/deterministic processing, apply defensive copies, read-only exposure and cast-mutation tests (Phase 1 code left unchanged until then)**; `PlayerStatus` rejects an active registered player without a club (revisit for free agents in Phases 15/16).

## Next Task

Wait for the owner's Phase 2 instruction. Before Phase 2 uses the Domain collections listed in Known Issues, apply the tracked mutability follow-up. Phase 2 also needs its detailed `GameRules` (D2) subset. Do not start Phase 2 or commit/push on our own. Optional: FHD (Phase 5+).

## Relevant Documents

- `AGENTS.md`
- `docs/toolchain.md`, `docs/decisions/ADR-001.md`, `docs/decisions/ADR-002.md`, `docs/data-model.md`, `docs/reports/PHASE_0_REPORT.md`, `docs/reports/PHASE_1_REPORT.md`
- `ROADMAP.md` §§1–6, Phase 0, §9
- `docs/SYSTEM_CONTRACTS.md` shared conventions and C33/C34 for future boundary context
- `docs/UI_ARCHITECTURE.md` shell boundary
- `docs/REQUIREMENTS_TRACEABILITY.md`
- `sources/SOURCE_MANIFEST.md`
- `sources/ROADMAP_REVISION_REQUEST.txt`
- `Star_Player_Portrait_System_Design.MD`
