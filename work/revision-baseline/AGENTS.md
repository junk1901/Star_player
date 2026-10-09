# Star Player Agent Instructions

This package is an architecture proposal. No implementation phase has passed. These instructions govern future implementation after the owner approves a phase; they do not authorize starting production work during roadmap preparation.

## Read before work

1. Read `CURRENT_STATE.md` and the approved phase in `ROADMAP.md`.
2. Read the relevant entries in `docs/SYSTEM_CONTRACTS.md` and UI inventory rows if changing screens.
3. Read the source sections linked by `docs/REQUIREMENTS_TRACEABILITY.md` before changing product behavior.

## Authority

The direct request in `sources/MASTER_REQUEST.txt` defines the assignment. Product/UX authority is `sources/Star_Player_game_v1.6.txt` and `sources/UI_UX_GUIDE_v1.8.md`, plus subsequent explicit owner decisions. `ROADMAP.md` proposes implementation order and architecture; supporting docs do not override source requirements. Source numeric examples are not approved constants unless explicitly fixed. Record unresolved substantive conflicts using Issue, Reason, Recommended Option, Alternative and Impact; wait for approval of the affected change while continuing independent authorized work.

The owner explicitly excluded `Star_Player_Portrait_System_Design.MD` from this roadmap on 2026-10-05. Its presence in the workspace does not authorize Seed 2D or Gemini image/batch work. Do not revise the supplied original files silently.

## Invariants

- Godot 4.x stable .NET, C#, Control, SQLite, JSON and optional Gemini text presentation. No replacement stack.
- Domain and Simulation are headless and independent of Godot/SQL/HTTP. UI sends commands and displays public read models. Bootstrap alone binds infrastructure.
- One controlled player. Team operations belong to AI. Stable IDs, not display names, join all records.
- Base skills, transient state, hidden development profile, proficiency, team role, manager trust and personal relationships are separate.
- Self current skills may be exact. NPC current ability uses observed grades/ranges/confidence. Never expose true potential, hidden growth type or raw legacy/Hall scores, including cheat mode and tooltips/news payloads.
- Creation points are recommendations, not hard caps. Two-way normal creation shares resources. Cheat does not reveal hidden future state.
- Second team is a real competition. Registration, league assignment, origin and entry path are distinct. No separate developmental-player database.
- Use one seeded baseball engine across display modes, levels and national competitions. Determinism includes stable ordering/IDs and versioned RNG.
- Datapacks seed a world once; saves then own facts/rules/history. Do not refresh a running world from changed pack files.
- One serialized writer; commit state, exact stats/history, RNG/counters and event receipts together. Apply a private working scope only after commit. Save snapshots follow ROADMAP §5.
- AI receives only public confirmed facts, cannot issue gameplay commands, and always has local fallback. Keys are excluded from saves/git/logs.
- Official layouts: 2560×1440 and 1920×1080 only. Preserve functionality at FHD; no blanket text shrink.

## Implementation conventions

Use typed IDs, explicit DTOs/results and small feature-focused services. Use standard .NET before adding packages. No giant manager, unnecessary inheritance, generic object-graph persistence, service-bus server or executable mod framework. C# uses PascalCase for types/members, normal local naming, nullable annotations and clear cancellation/revision handling. SQLite/JSON use documented stable snake_case field names. Monetary values are checked integer KRW; innings are outs; dates and save timestamps are distinct.

Each new durable owner needs an explicit table mapping, migration and historical-default policy. Do not backfill invented past events. Do not silently replace a PRNG or metric formula without versioning and compatibility notes. Keep exact backend values out of general-purpose UI dictionaries.

## Scope, tests and reporting

Implement one approved phase. Ordinary local implementation decisions are allowed within these boundaries; product changes and the roadmap's affected owner decisions require explicit resolution. Do not expand into foreign leagues/posting, Steam, manager/GM modes, post-retirement gameplay, multiplayer, code mods, or excluded portraits.

Run the phase's tests and Common Gate G, including prior regressions, save/migration checks and required UI workflows. Actual command output is evidence; unrun tests remain INCOMPLETE. No fake passing reports. Update `docs/reports/PHASE_<N>_REPORT.md` and `CURRENT_STATE.md` with concise facts, gate status, artifacts, decisions, blockers and the next approved task. Stop for phase review instead of automatically implementing the next phase.

Keep requirements in the sources, sequence/gates in ROADMAP, contracts in SYSTEM_CONTRACTS and routes in UI_ARCHITECTURE. Add subsystem docs only when they materially help the active phase. Do not turn CURRENT_STATE into a diary.
