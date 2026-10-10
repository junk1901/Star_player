# Star Player Agent Instructions

This package is an architecture proposal. No implementation phase has passed. These instructions govern future implementation after the owner approves a phase; they do not authorize starting production work during roadmap preparation.

## Read before work

1. Read `CURRENT_STATE.md` and the approved phase in `ROADMAP.md`.
2. Read the relevant entries in `docs/SYSTEM_CONTRACTS.md` and UI inventory rows if changing screens.
3. Read the source sections linked by `docs/REQUIREMENTS_TRACEABILITY.md` before changing product behavior.

## Authority

The original `sources/MASTER_REQUEST.txt`, `sources/ROADMAP_REVISION_REQUEST.txt` (R), targeted revision A, final feature revision F, final owner-decision revision O and owner-approved Club Finance Lite revision CF define the assignment; O is latest for D1–D6, CF, the user-approved Broadcast Commentary Lite addendum (BCL, ROADMAP §1.12), Live Match Viewer Lite (LMV-A, ROADMAP §1.13) and Pitch Zone Lite (PZL, ROADMAP §1.14) are bounded Scope Lock exceptions, and all preserve R/A/F. Product/UX authority is `sources/Star_Player_game_v1.6.txt` and `sources/UI_UX_GUIDE_v1.8.md`, plus subsequent explicit owner decisions. `ROADMAP.md` proposes implementation order and architecture; supporting docs do not override source requirements. Source numeric examples are not approved constants unless explicitly fixed. Record any new unresolved substantive conflict using Issue, Reason, Recommended Option, Alternative and Impact; do not reopen an approved D1–D6 policy merely because its detailed phase data is not yet specified.

`Star_Player_Portrait_System_Design.MD` is now an approved separate Source of Truth, superseding the earlier exclusion. Preserve it unchanged and reference it: Seed 2D/profile base in Phase 24, explicit user-selected Gemini single/batch enhancement in Phase 26, final polish in 28–29. Its local phase labels do not renumber ROADMAP. Source documents do not authorize production work during this revision.

## Invariants

- Godot 4.x stable .NET, C#, Control, SQLite, JSON and Gemini narrative/AI-native interaction plus optional selected portraits. No replacement stack.
- Domain and Simulation are headless and independent of Godot/SQL/HTTP. UI sends commands and displays public read models. Bootstrap alone binds infrastructure.
- One controlled player. Club/Manager AI is bounded deterministic/seeded rule-based C#, never an LLM. Save ManagerProfile/ClubPhilosophy and versioned KBO Rule Engine profiles. Stable IDs, not names, join records.
- Base skills, transient state, hidden development profile, proficiency, team role, manager trust and personal relationships are separate. Catcher tools differ from C proficiency; BatteryChemistry belongs to each pitcher-catcher pair, affects effective performance only and is not friendship.
- Self current skills may be exact. NPC current ability uses observed grades/ranges/confidence. Never expose true potential, hidden growth type or raw legacy/Hall scores, including cheat mode and tooltips/news payloads.
- Creation points are recommendations, not hard caps. Two-way normal creation shares resources. Cheat does not reveal hidden future state.
- Second team is a real competition. Registration, league assignment, origin and entry path are distinct. No separate developmental-player database.
- Use one seeded baseball engine across display modes, levels and national competitions. Determinism includes stable ordering/IDs and versioned RNG.
- Under D1, default player input is one strategy per Plate Appearance while Game Core resolves a real internal pitch/count sequence. Display only real count state. An explicit `Take` consumes one actual pitch and resumes at a legal decision point if the PA continues; do not require every-pitch input by default.
- Under D2, use a career-frozen versioned 2026 KBO-oriented baseline with explicit documented simplifications. Specify the detailed owning RuleProfile subset before each consuming phase, never guess or scatter KBO constants, and never mutate an existing career when real rules change.
- Under D3, the personal KBO 2026 pack is immediately preseason and opening day is the simulation divergence point. Record exact `as_of_date`, evidence cutoff, supplied sources and provenance in Phase 27; never use post-start 2026 evidence for preseason ratings.
- Under D4, a newly created production career uses Draft / Entry; Phase 5 may only use labeled fictional post-entry fixtures. Existing Player Start remains separate. Free biography text is descriptive unless a separately defined structured, visible, bounded and tested trait owns an effect.
- StadiumId and versioned bounded ParkFactor context are explicit game inputs, never player-rating changes. Completed games retain the venue/profile context actually used even if a club moves or a profile changes.
- Manager/coach identities, assignments and tenures may change; the player never hires/fires staff or selects the captain. Preserve former-staff relationship history and start a replacement manager's trust/evaluation in a separate context.
- Annual domestic salary renewal is separate from FA. The agent may advise/negotiate, but the controlled player's final decision remains explicit; exact legal deadlines and procedures come from the approved ContractRules profile.
- C13 contracts and obligations are the only authoritative source for player salary commitments. C48 Club Finance Lite derives total payroll and future commitments, while salary-cap chargeable payroll and internal player budget remain separate. C48 cannot duplicate contracts or C25 player payments.
- SalaryCapRules remain career-frozen C40 data and decide scope, components, exemptions, dates, threshold and excess consequence. Do not assume a hard cap or let sponsorship revenue change the legal threshold. Binding salary guarantees survive financial trouble.
- Club sponsorship is separate from C38 individual player sponsorship. Settle base/earned revenue once from confirmed C10 facts, renew within saved bounds and provenance, and keep all Club AI financial decisions deterministic/rule-based. No Gemini finance authority, player budget allocation, sponsor negotiation or GM/owner mode.
- Pitch Zone Lite (PZL, ROADMAP §1.14) is an owner-approved, bounded simulation exception: 17 categorical pitch zones plus the actual batter side, a minimal `Clear`/`Borderline` class, distinct target zone and actual location, a bounded neutral-default nine-zone batter tendency vector, and location-conditioned probabilities. The actual pitch location is drawn **before** the outcome (never assigned afterward); `GameReducer` keeps sole authority over official records and calls. Existing `ef1` fingerprints, golden vectors and receipts are immutable; events carrying a location use the versioned `ef2` contract. Legacy `GameRuleSnapshot` hashes are never silently changed (new fields = a new profile version), and no real KBO ABS certification is claimed. Advanced weakness-exploiting AI (observable reports only, never the hidden tendency vector), tendency growth, precision physics, real ABS coordinates and a finished heat-map UI are deferred. BCL/LMV may read confirmed locations but never consume gameplay RNG.
- Live Match Viewer Lite (LMV-A) is an owner-approved, presentation-only 2D icon-based game visualization. The authoritative Game Core resolves every outcome before visualization; presentation cues and renderers must not determine baseball results, modify scoring or player ratings, or consume gameplay RNG. Watch, skip, animation speed and display changes preserve identical authoritative game/event hashes. Future LMV-B SD-character animation is a deferred extension, not a current phase requirement. Keep LMV-A/B visualization modes distinct from BCL broadcaster Crew A/B. LMV and BCL presentation selection keys use separate namespaces and never share a stream with each other or with gameplay RNG. (ROADMAP §1.13; Domain / Simulation / Application / Godot boundaries unchanged.)
- Broadcast Commentary Lite (ROADMAP §1.12) is an offline, rule-based, text-only presentation projection of committed events: no authority over game facts, no Gemini/TTS/network, one crew fixed per game (A/B rotation, assignment persisted and never rerolled), variation from a presentation key separate from gameplay RNG, and no effect on game/RNG/event hashes with commentary on, off or at any verbosity.
- Captaincy and TeamAtmosphere are bounded social/narrative/team-context systems, not large direct ability modifiers. Captain choice uses multiple factors and remains club-owned.
- All-Star voting and weather interruption/makeup behavior are KBO Rule Engine-owned. Weather uses an isolated deterministic RNG stream and cannot perturb baseball/development randomness.
- A jersey number is a C47 effective-dated PlayerId–ClubId assignment, never a permanent Player field. Validate active club uniqueness and C31 club-specific retired restrictions atomically; preserve every historical interval across changes, movement and retirement.
- C14 draft source/completion is data-driven. Imported completed editions are locked history and never run through Club AI. Missing future imported classes generate once through C15/C16, persist IDs/results, and never reroll on load. Existing Player Start binds an imported rookie's existing PlayerId without copying the player or changing draft/club/entry facts.
- Datapacks seed a world once; saves then own facts/rules/history. Do not refresh a running world from changed pack files.
- Under D6, preserve authoritative history and initially retain raw pitch traces and ordinary narrative. Measure working DB plus all snapshot generations and performance in Phases 17/29 before proposing targeted pruning. Never delete authoritative statistical/archive/legacy evidence or weaken recovery guarantees for storage convenience; add no retention setting without measured need.
- One serialized writer; commit state, exact stats/history, RNG/counters and event receipts together. Apply a private working scope only after commit. Save snapshots follow ROADMAP §5.
- Gemini receives privacy-safe confirmed facts/context; raw prose never becomes commands. C43 alone validates finite interview meaning before core rules compute and atomically apply consequences once. Preserve raw answers and accepted interpretations; deterministic replay uses those records, never a new model reply. AI-native interview/fan/SNS features may remain pending/retryable without template equivalents; failures apply no partial effects. Keys stay outside saves/git/logs.
- Portraits retain Seed/neutral fallback and local cache; only explicit user generate/regenerate actions may call the image API. Missing portraits cannot prevent core simulation/load.
- Keep Pending Decisions visible across navigation/load/advance; contextual help is suppressible and reopenable. Settings appear only with implemented capabilities and cannot weaken mandatory recovery snapshots or the three-generation floor.
- Official layouts: 2560×1440 and 1920×1080 only. All actual player-facing UI/help/messages/generated text remain Korean; English roadmap/type names are technical notation. Preserve functionality at FHD; no blanket text shrink.

## Obsidian Phase review workflow

`Star/` is the Obsidian development-history and Phase-review Vault. It supplements the official documents and never replaces them: `ROADMAP.md` owns the plan and gates, `CURRENT_STATE.md` owns current development status, and this file owns AI working rules. If a review conflicts with an official document, report the conflict and reconcile the official source before treating the review as authoritative. Do not move, duplicate or delete existing planning documents merely to make them Vault-local.

Use `Star/99_Templates/Phase_Review_Template.md` and create `Star/01_Phase_Reviews/Phase_XX_Review.md` only when that Phase's implementation or record actually begins. Do not pre-create reviews for unstarted Phases or infer past work. Allowed review states are `초안`, `검토 대기`, `승인 완료`, and `보완 필요`. Claude Code may set `초안` and, after implementation plus its actual self-tests, `검토 대기`; only a recorded GPT Chat/user review may set `승인 완료`. Mark unrun checks `미검증` and independent reviews that did not occur `미실시`.

### Review roles

- **GPT Chat:** define/refine requirements, review Phase completion reports, check specification coverage, recommend improvements, issue the next-Phase instruction, and request deeper Codex review when warranted.
- **Codex:** review initial architecture, important design changes, complex cross-module dependencies, major code/architecture changes and structural risks. Codex review is optional for ordinary implementation or simple UI changes; recommend it for security, save-data integrity, core simulation rules and broad multi-module changes.
- **Claude Code:** implement the approved Phase, run unit/integration tests, fix errors, record changed files, write the Phase completion report, create/update the Obsidian review draft, and update `CURRENT_STATE.md`. Claude Code records implementation/test evidence only and never invents GPT Chat/Codex review results.

### Required Phase closeout

After implementation and testing, Claude Code must:

1. Re-read that Phase's ROADMAP acceptance criteria and Common Gate G.
2. Record the actual implementation, changed files and Git working-tree facts.
3. Record each test command, environment and actual pass/fail result; mark anything not run as `미검증`.
4. Identify solved/unsolved issues, technical debt and deviations from the plan.
5. Reconcile any design change in the official owning document first; create a linked note in `Star/02_Decisions/` for an important decision.
6. Create or update `Star/01_Phase_Reviews/Phase_XX_Review.md` from the template. Put unresolved bugs/debt in `Star/03_Issues/` and reusable lessons in `Star/04_Dev_Notes/` only when there is real content.
7. Set the review to `검토 대기`, never `승인 완료`.
8. Update `CURRENT_STATE.md` and the dashboard summary so they match actual evidence. Change ROADMAP planning text only when an approved design/plan change requires it; never mark a gate passed merely by editing prose.
9. Return a concise GPT Chat handoff with implemented scope, tests, failures, open issues and review-document path, then stop. Creating a review never authorizes the next Phase.

Preserve prior approved review content. Append or revise with clear evidence; do not erase historical decisions to make a current report cleaner. Keep full evidence in Markdown and chat output short. Do not add Dataview or another plugin dependency for this workflow.

## Implementation conventions

Use typed IDs, explicit DTOs/results and small feature-focused services. Use standard .NET before adding packages. No giant manager, unnecessary inheritance, generic object-graph persistence, service-bus server or executable mod framework. C# uses PascalCase for types/members, normal local naming, nullable annotations and clear cancellation/revision handling. SQLite/JSON use documented stable snake_case field names. Monetary values are checked integer KRW; innings are outs; dates and save timestamps are distinct.

Each new durable owner needs an explicit table mapping, migration and historical-default policy. Do not backfill invented past events. Do not silently replace a PRNG or metric formula without versioning and compatibility notes. Keep exact backend values out of general-purpose UI dictionaries.

## Scope, tests and reporting

Implement one owner-approved phase. Ordinary local implementation decisions are allowed within these boundaries; product changes outside approved policy still require explicit resolution. D1–D6 are all approved, but this documentation revision does not authorize Phase 0 or any production work. F closed committed gameplay feature scope; CF, the user-approved presentation-only Broadcast Commentary Lite (BCL, ROADMAP §1.12: offline two-crew text commentary, Phases 5/6/28) and Live Match Viewer Lite (LMV-A, ROADMAP §1.13: 2D icon-based field visualization, Phases 5/28) and Pitch Zone Lite (PZL, ROADMAP §1.14: bounded pitch-location simulation, Phases 3/4) are the only approved bounded exceptions and the Scope Lock is active again after their integration. Do not add another gameplay system unless an implementation dependency is genuinely missing, a requirement is contradictory, or the owner explicitly approves it; otherwise record the idea as deferred. Do not expand into foreign leagues/posting, Gemini/TTS/external-API commentary, further broadcast crews, LMV-B SD-character animation or art, advanced weakness-exploiting pitching AI, batter tendency growth, precision ball physics, real ABS coordinate reproduction, a finished heat-map UI, Steam, manager/GM/owner modes, comprehensive franchise finance, ticket/stadium/broadcast-rights/staff-payroll/debt/bankruptcy/accounting systems, post-retirement gameplay, multiplayer, code mods, portrait features outside the approved P specification, or detailed ejection/discipline/hearing/appeal/bench-clearing simulation.

Run the phase's tests and Common Gate G, including prior regressions, save/migration checks and required UI workflows. Actual command output is evidence; unrun tests remain INCOMPLETE. No fake passing reports. Update `docs/reports/PHASE_<N>_REPORT.md` and `CURRENT_STATE.md` with concise facts, gate status, artifacts, decisions, blockers and the next approved task. Stop for phase review instead of automatically implementing the next phase.

Keep requirements in the sources, sequence/gates in ROADMAP, contracts in SYSTEM_CONTRACTS and routes in UI_ARCHITECTURE. Add subsystem docs only when they materially help the active phase. Do not turn CURRENT_STATE into a diary.
