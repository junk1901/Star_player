# Source Manifest

Reviewed completely for the architecture proposal prepared on 2026-10-04–10. Files here and the independent root Portrait document are unmodified copies of the supplied inputs; their original language is preserved. The generated roadmap/supporting documents are in English. The owner's direct targeted revisions A, F, O and CF are recorded as conversation authority rather than copied into `sources`.

| Key | File | Role | SHA-256 |
|---|---|---|---|
| M | [MASTER_REQUEST.txt](MASTER_REQUEST.txt) | Direct user request pasted as an attachment; governs this planning task | `813452BA3B325CBBBC558304F4E6008CB7A915615B8FF17A758B7A88FD406C76` |
| G | [Star_Player_game_v1.6.txt](Star_Player_game_v1.6.txt) | Product requirements | `87EADC3684B368FFBF9CB8A48646794E1E37E2E74006FD749D36CCC0978491DA` |
| U | [UI_UX_GUIDE_v1.8.md](UI_UX_GUIDE_v1.8.md) | UI/UX requirements | `45B98A38116CBA56C5E53B66E688A737DFD0764BEFD74B7FAEF13037D68C8DBB` |

Original G path: `C:/Users/user/Downloads/Star_Player_game_v1.6.txt`. Original U path: `C:/Users/user/Downloads/UI_UX_GUIDE_v1.8.md`. Original M path: `C:/Users/user/.codex/attachments/394af5b1-cf5d-402b-a51e-20442bd6e091/붙여넣은 텍스트.txt`.

M's alternate game-design spellings refer to G as supplied; no extra document was invented. U §49's old v1.0/v1.4 references are stale cross-references, not a different planning baseline. Descriptive examples are distinguished from mandatory rules in ROADMAP §1.

The workspace is `E:/Madegame/Star_player`. The earlier two-document-only decision is superseded by R11: the Portrait specification is now approved and included unchanged as a separate root reference. G/U/M remain unchanged historical inputs; R identifies the first explicitly revised requirements. A adds Stadium/ParkFactor, annual non-FA salary, staff lifecycle, captaincy, TeamAtmosphere, All-Star voting, lightweight weather and discipline deferral. F then adds jersey-number lifecycle, imported historical draft data, imported-rookie Existing Player Start and the final gameplay scope lock while preserving R/A. O approves D1–D6 without adding gameplay scope. CF is one later owner-approved bounded exception for Club Finance Lite and reactivates the Scope Lock after integration. No source file has been silently edited.

## Revision authority and independent Portrait source

| Key | File | Role | SHA-256 |
|---|---|---|---|
| R | [ROADMAP_REVISION_REQUEST.txt](ROADMAP_REVISION_REQUEST.txt) | Prior direct owner revision; overrides conflicting earlier choices, including the earlier portrait exclusion and universal AI fallback | `6027C298C8510991A933DE8654027C7CDAE07794389CBFAB92A8F49977AC0876` |
| P | [Star_Player_Portrait_System_Design.MD](../Star_Player_Portrait_System_Design.MD) | Approved independent portrait Source of Truth; preserved unchanged, referenced rather than copied into ROADMAP | `D53DF3D253CEC20A62BF89F5B558AD5C737F4B6ED24E2AB0A342BB0E0EB94486` |

Original R path: `C:/Users/user/.codex/attachments/e69f6bf2-a508-4775-8ea3-dce34891a5c3/붙여넣은 텍스트.txt`. Original P path: `C:/Users/user/Downloads/Star_Player_Portrait_System_Design.MD`, explicitly reattached by the owner for this revision. R/P were read completely, alongside the entire existing roadmap and available supporting planning documents. P's internal Phase 1–5 are local portrait steps mapped into global Phases 24/26/28–29, not new global phase numbers.

## Current direct revision A

A is the owner's 2026-10-05 conversation request headed “STAR PLAYER ROADMAP REVISION — ADDITIONAL SYSTEMS.” It remains authority for its targeted additions and requires R's previously approved features to remain intact. A was read with the complete current roadmap, contracts, UI architecture, traceability, current state, agent instructions, G, U and P. It adds no source-file hash because no separate local source copy was created or edited.

## Final direct feature revision F

F is the owner's conversation request headed “FINAL FEATURE ADDITIONS — JERSEY NUMBERS + IMPORTED HISTORICAL ROOKIES.” It is latest for those additions, preserves R/A and closes committed gameplay scope after integrating C47 jersey assignments, data-driven imported/generated draft history and imported-rookie Existing Player Start. F adds no source-file hash because no separate local source copy was created or edited.

## Final owner-decision revision O

O is the owner's 2026-10-06 conversation request headed “STAR PLAYER — FINAL OWNER DECISIONS D1 / D2 / D3 / D4 / D6.” It marks D1–D6 approved, preserves the D5 AI policy and every R/A/F feature, and adds no gameplay scope or top-level phase. It fixes the PA interaction cadence, KBO-oriented rule/version policy, immediate-preseason personal 2026 snapshot, Draft / Entry created-player policy and preserve-first historical retention. O adds no source-file hash because no separate local source copy was created or edited.

## Owner-approved Club Finance Lite revision CF

CF is the owner's 2026-10-10 conversation request headed “STAR PLAYER — CLUB FINANCE LITE / Owner-Approved Final Scope Extension.” It is one explicit bounded exception to F's Scope Lock and adds contract-derived club payroll, versioned KBO salary-cap assessment, performance-based club sponsorship settlement/renewal, rule-based Club AI financial inputs and read-only financial/history views. It preserves D1–D6, every R/A/F feature, Phase 0–29 and the existing architecture, then reactivates the Scope Lock. CF adds no source-file hash because no separate local source copy was created or edited.

## Technical references

These official references were consulted to check feasibility constraints, not to override game requirements. Pin and recheck the chosen release/API at the relevant implementation phase.

- [.NET-enabled Godot and C# prerequisites](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html): Phase 0 toolchain/export probe.
- [Godot thread-safe APIs](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html): main-thread scene updates.
- [SQLite Online Backup](https://www.sqlite.org/backup.html): consistent snapshot creation.
- [SQLite WAL](https://www.sqlite.org/wal.html): sidecar/checkpoint considerations if WAL is introduced.
- [SQLite Foreign Keys](https://www.sqlite.org/foreignkeys.html): connection-level activation and enforcement.
- [Gemini Structured Output](https://ai.google.dev/gemini-api/docs/structured-output): schema constraints do not by themselves establish semantic fact accuracy.

No current real-world KBO/military rulebook, player dataset, Gemini image API or pricing claim has been substituted for owner-approved game requirements.
