# Star Player Development Roadmap

Planning baseline: 2026-10-10; targeted owner revisions R1–R5 applied (source review began 2026-10-04). Status: **proposed architecture and implementation sequence; no game code has been implemented**. Primary implementer: Claude Code. Implement one owner-approved phase at a time and return its report for GPT Chat/user review; request additional Codex review for important architecture, save-integrity, core-simulation, security or broad cross-module changes.

Read this document with [System Contracts](docs/SYSTEM_CONTRACTS.md), [UI Architecture](docs/UI_ARCHITECTURE.md), and [Requirements Traceability](docs/REQUIREMENTS_TRACEABILITY.md). These contain supporting detail, not competing product specifications. [CURRENT_STATE.md](CURRENT_STATE.md) identifies the next task; [AGENTS.md](AGENTS.md) defines the agent handoff rules.

## 1. Architecture Summary

### 1.1 Authority and scope

The original task is [MASTER_REQUEST.txt](sources/MASTER_REQUEST.txt); the prior [owner revision request](sources/ROADMAP_REVISION_REQUEST.txt), abbreviated **R**, supersedes conflicting earlier instructions, including the earlier portrait exclusion. The direct 2026-10-05 additional-systems request, abbreviated **A**, adds stadium/park factors, annual non-FA salary negotiation, staff turnover, captaincy, TeamAtmosphere, All-Star voting and lightweight weather while preserving R. The final feature request, abbreviated **F**, adds dated club-specific jersey numbers, data-driven imported historical drafts and Existing Player Start for already-drafted imported rookies while preserving R/A. The 2026-10-06 final owner-decision request, abbreviated **O**, approves D1–D6 without adding gameplay scope and is latest for those policies. The 2026-10-10 owner-approved Club Finance Lite request, abbreviated **CF**, is one bounded exception to F's Scope Lock and adds club payroll, KBO salary-cap assessment, performance-based club sponsorship revenue, rule-based Club AI integration and read-only financial views. The Scope Lock is active again after CF. The product sources are [Star_Player_game_v1.6.txt](sources/Star_Player_game_v1.6.txt), abbreviated **G**, and [UI_UX_GUIDE_v1.8.md](sources/UI_UX_GUIDE_v1.8.md), abbreviated **U**. Both were read completely. [Star_Player_Portrait_System_Design.MD](Star_Player_Portrait_System_Design.MD), abbreviated **P**, remains an approved, separate Source of Truth for portraits; preserve it unchanged and consult it rather than duplicating its specification here. The current roadmap and all available planning companions were reviewed for this revision. The differently spelled filenames in the master request refer to the supplied game-design file; they are not additional missing documents. U §49 contains old version references; its current v1.8 content and G v1.6 are the supplied baseline.

The source documents describe the product. Instructions embedded in them do not authorize implementation, publication, installations, or replacing this planning task. The direct request explicitly authorizes architecture and roadmap documents only.

Required product: PC single-player, one controlled KBO player, new or existing career, autonomous clubs, first and second teams, and a continuing domestic baseball world. Retirement ends the playable career. Keep the fixed Godot 4.x stable / C# / Control / SQLite / JSON / Gemini stack. No required Steam work, online multiplayer, manager/GM career, foreign professional league simulation, posting, executable mods, or post-retirement management mode.

Numbers marked as examples in G/U remain examples. In particular, a 100-point creation budget, agent percentages, sample contracts, and sample stat lines are not automatically balance constants. Ratings **0–100**, hidden true potential for everyone including cheat mode, the two supported resolutions, and the separation of developmental registration from origin are explicit requirements.

### 1.2 Selected design

Use a **single desktop process with a modular C# core**. A small composition root constructs application services and infrastructure adapters. There is no service bus server, microservice deployment, reflection-based plugin framework, or universal repository abstraction.

| Layer / project | Owns | Allowed compile-time dependencies | Forbidden dependencies / behavior |
|---|---|---|---|
| Domain | Entities, value objects, invariants, domain events, rule evaluations | .NET base libraries | Godot, SQL, HTTP, filesystem, UI DTOs |
| Simulation | Baseball resolution, daily evolution, seeded AI policies | Domain | Godot, database access, wall-clock randomness |
| Application | Commands, queries, session coordination, unit-of-work and service ports, visibility projections | Domain, Simulation | Concrete infrastructure, Godot scene objects |
| Infrastructure | SQLite mapping, datapack import, assets, save files, Gemini adapter | Application, Domain | Autonomous gameplay decisions |
| Godot client | Control scenes, presenters, navigation, input, composition root | Application; Infrastructure only at composition root | Domain mutation or direct SQL from scenes |
| Test / simulation host | Headless scenarios, regression and long-save diagnostics | Core and selected infrastructure | Production UI dependency for domain tests |

Calls move from UI through Application to core and adapters. Compile-time dependency inversion means the Domain does **not** reference persistence even though persistence follows computation at runtime. Screen code receives explicit read models, not mutable entities.

Three alternatives considered: (1) a Godot-node-centered model is convenient initially but undermines headless tests and save ownership; (2) a separate simulation service creates unnecessary deployment and synchronization work; (3) this modular in-process design supplies isolation without those costs. This is an engineering choice within the requested stack.

### 1.3 Mutation and concurrency

One `WorldSession` owns the loaded career and one serialized command queue. It is an application coordinator, not a class containing every rule. Domain services own their specific rules. Commands contain `command_id`, target IDs, and `expected_revision`; stale or repeated inputs cannot apply a choice twice.

Simulation runs on one worker initially. Query requests return immutable snapshots tagged with revision and save-session token. Godot controls update on the main thread. A late result from an old save, closed screen, or cancelled query is discarded. The active scene tree is not thread-safe; this boundary follows the [Godot threading documentation](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html).

A command computes changes in a private working scope, validates invariants, commits changes plus mandatory projections and events, and only then publishes its new revision. On failure, discard the scope and keep the prior committed revision. Do not mutate the displayed authoritative object graph and hope that SQL rollback also rolls back memory.

### 1.4 Authority, visibility, and events

- A datapack seeds a new world once. The career's SQLite state then owns facts, rules, generated players, and history. No automatic live refresh from the original pack.
- `controlled_player_id` is immutable for a playable career. UI commands can choose that player's permitted actions; team lineups, trades, drafts, and selections remain AI decisions.
- Player current ratings, transient state, hidden development profile, role proficiency, team role, managerial trust, and personal relationships have distinct owners/fields.
- Exact current ratings are available only for the controlled player. NPC projections contain observed grades/ranges and confidence. True potential, hidden growth type, raw legacy and Hall scores never enter UI DTOs, tooltips, search sort keys, logs shown to users, or Gemini payloads. This is an application access rule; local save files are not claimed to be tamper-proof.
- Typed domain events carry facts. Required statistical/history consequences are committed synchronously; news text is an optional post-commit projection. This is **not full event sourcing**: normalized state plus selected immutable facts remains load authority.
- Event identity is `(branch_id, sequence)` plus an `event_id`; events include type, payload version, world date, subject IDs, competition/season/game context, causation command, and world revision. Projection consumption is idempotent by `(event_id, projection_name, projection_version)`.

### 1.5 Decisions already resolved by explicit authority

| Source tension | Resolution in this proposal | Effect |
|---|---|---|
| U §8 shows exact NPC runner/defender values; G ability rules, U §26, and master §13 hide NPC internals | Apply the explicit visibility rule to field labels and tooltips: self `SPD 78`; NPC `SPD B+` or observed range with confidence | Required baserunning information remains available; no information leak |
| U §8 mentions pinch runners as a use for information; player is not a manager | Diagram conveys tactical context; AI owns substitutions. No roster-control buttons are implied | Preserves one-player scope |
| Early developmental `roster_status` example mixes registration and level | Use separate registration, assignment, availability, and origin concepts, as later G text and master explicitly require | No special developmental-player database |
| Master priority lists databases late; U §38/52 require early minimal profiles | Minimal Player/Team profiles in Prototype 3; richer historical exploration later | Earlier UI requirements are not postponed |
| Advanced stats are initially “later,” but WAR feeds confirmed history and legacy systems | Implement counting-stat inputs early and the versioned advanced metrics before legacy evaluation | No missing data reconstruction after decades |

R approves the save-recovery architecture, catcher/Battery Chemistry additions, early UX infrastructure, discovery QoL, portrait reintegration and the revised generative-AI policy. A additionally approves the bounded systems summarized in §1.9. F adds the two systems summarized in §1.10 without changing R/A. O approves D1–D6 while preserving every prior requirement. CF adds only the bounded system summarized in §1.11 and then re-locks gameplay scope. Detailed RuleProfile subsets and personal-pack source certification remain implementation/content tasks, not unresolved product-policy decisions. No decision authorizes production implementation by itself.

### 1.6 KBO Rule Engine

The **KBO Rule Engine** is the explicit data-driven, versioned rules subsystem within the existing Domain/Simulation boundaries, not another project or one giant service. A validated `RuleProfile` groups typed rule areas; a career freezes their versions/content hashes and effective dates in `rule_snapshots`. `GameRuleSnapshot` is the resolved game-specific subset. Services evaluate rules through the relevant area contract (C40) rather than scatter 2026 constants or competing eligibility checks across scenes/services.

| Rule area | Owning integration phases | Consumers |
|---|---|---|
| GameRules | 1 vocabulary, 2 legality, 3 resolution | Game/reducer/scorer |
| RegularSeasonRules | 6, 11 | Schedule/standings, venue/weather interruption, rescheduling and configured All-Star selection procedure |
| RosterRules / RegistrationRules | 9–10 | Selection, level moves and eligibility |
| PostseasonRules / AwardRules | 11 | Brackets, series and awards |
| DraftRules | 13 | Eligibility/order/entry |
| ContractRules / SalaryCapRules | 14 (fixed rookie subset 13) | Contracts, annual non-FA salary renewal, service/payment obligations and salary-cap assessment; Phase 15–16 consume projections |
| FARules | 15 | Eligibility/windows/compensation |
| ForeignPlayerRules | 16 | Domestic foreign-player intake/quotas |

D2 approves a versioned 2026 KBO-oriented baseline plus explicit documented simplifications. Each consuming phase specifies and tests its detailed RuleProfile subset before implementation; this does not reopen D2. Missing/unsupported profiles fail explicitly, with no guessed production values. International/military/legacy profiles remain versioned under their existing owners. Save/load, migrations and pack certification retain and validate the same career-frozen rule snapshots.

### 1.7 Rule-based AI and generative-AI authority

**Club AI / Manager AI means deterministic or seeded rule-based C# decision scoring, never an LLM.** Persistent `ManagerProfile` and `ClubPhilosophy` supply bounded policy weights to legal decisions, including spending aggressiveness, star retention, willingness to approach a saved salary-cap threshold and long-term caution after Phase 14. They do not grant the user manager/GM control.

Use Gemini actively where it adds substantial immersion, personality and expressive variety: free-text interviews, contextual fan/SNS/community reactions and selected high-value articles. Routine low-value information remains procedural/template-based. Generative AI never owns game results, simulation, growth/aging/injuries, lineups, team operations, draft/contract/FA/trade outcomes, generated ratings, league progression, authoritative relationship values or facts.

The one inbound semantic path is a strictly validated **structured interview interpretation**. Gemini suggests whitelisted meaning; C# Game Core rules determine and atomically commit consequences. Generated narrative prose is never parsed as a domain command. AI-native interpretation and rich fan/SNS reactions do not require template equivalents: failures retain pending/retry state without fabricated answers or partial effects. Routine systems and already-committed facts remain safe. P's Seed 2D image fallback is a separate, retained requirement.

### 1.8 Player language and incremental UX

All actual player-facing labels, messages, help, settings, reporter questions and generated presentation are **Korean**. English route/type names and roadmap prose are technical notation only. Early UX includes `의사결정 대기함`, contextual help and settings for capabilities that actually exist; final presentation/accessibility polish remains Phase 28.

### 1.9 Additional long-career systems

The following additions reuse existing owners and phases rather than creating a new top-level phase:

- `StadiumId`, `Stadium` and versioned bounded `ParkFactorProfile` form the C46 data-driven venue subsystem. Presentation assets and simulation factors are separate. Games retain the resolved stadium/profile/version context used when played; changing a club's home venue or a profile never rewrites old outcomes or player ratings.
- C13 distinguishes annual domestic salary renewal from the FA market. Club proposal, agent advice, bounded counteroffer, player final decision, NPC resolution, deadline and salary history use ContractRules; the detailed 2026 KBO-oriented ContractRules subset and any simplification must be specified before Phase 14.
- C06 owns lightweight staff identity/assignment/tenure and bounded lifecycle policy. Manager replacement creates a new evaluation context without deleting the prior relationship or tenure history. The player never hires or dismisses staff.
- Captaincy and `TeamAtmosphere` are lightweight club context under C06/C23/C26: captain selection is AI/rule-based and multi-factor; effects are chiefly leadership, relationship, stress, adaptation and story opportunities, not large rating buffs or a league-wide social graph.
- C10 owns an observable All-Star voting/selection race whose configured procedure remains in the KBO Rule Engine. C09 owns minimal weather state and isolated deterministic weather RNG; weather mainly produces rule-driven delay/postponement/cancellation/rescheduling and downstream workload.
- Detailed ejections, disciplinary hearings, suspension/appeal and bench-clearing simulation are deferred expansion candidates. Fact-backed conflict may appear in stories, but this revision adds no discipline subsystem.

### 1.10 Final jersey/draft additions and scope lock

- C47 owns `JerseyNumberAssignment` as a dated PlayerId–ClubId relationship. It supports imported/rookie/generated assignment, same-club change, transfer/FA/release closure, deterministic NPC fallback, controlled-player choice from legal alternatives, permanent history and club-specific retired-number enforcement. A jersey number is never a timeless Player field.
- C14 marks each draft edition/class with data-driven `DraftClassSource` (`Imported` or `Generated`, or an equivalent extensible value). Imported completed results are locked starting facts and never enter Club AI selection. If no imported class exists when a future draft is reached, the normal seeded feeder/scouting/draft pipeline generates it once per save and persists its identities, pool and results.
- C34 may optionally import completed draft facts, entry/signing/registration facts and an initial C47 number. No Game Core branch checks `year == 2026`; the default KBO 2026 pack policy comes from pack data and may instead describe other imported years or fictional completed classes.
- The personal KBO 2026 pack represents the world immediately before the 2026 regular season, with opening day as the simulation divergence point. Its default configuration marks the already-completed 2026 rookie draft/history as `Imported`/completed and locked; with no imported 2027+ classes, 2027 onward uses the normal generated pipeline. This is pack data, not a Game Core conditional. Phase 27 still records and certifies the exact `as_of_date`, evidence cutoff, source rows and provenance.
- C01 Existing Player Start may select an imported already-drafted rookie by the same stable PlayerId. Career binding never copies the Player, reruns the draft or changes the historical club/entry facts. Facts before the configured start remain imported history; events after it are simulation-owned and may diverge completely from reality.
- F closed the committed gameplay feature scope. CF is an owner-approved bounded exception described in §1.11, Broadcast Commentary Lite (BCL, §1.12) and Live Match Viewer Lite (LMV-A, §1.13) are user-approved (2026-10-11) presentation-only exceptions with unchanged, independent scopes, Pitch Zone Lite (PZL, §1.14) is a user-approved (2026-10-11, Codex design review `READY WITH NOTES`) bounded game-simulation exception that is not presentation-only, and LMV-B is only a recorded future-extension possibility, not an approved feature; after their integration, the Scope Lock remains active. Future ideas are deferred unless an implementation dependency is genuinely missing, an existing requirement is contradictory, or the owner explicitly approves another feature.
- Pitch Zone Lite (§1.14) is a fourth bounded exception that leaves the CF, BCL and LMV-A approvals unchanged and approves **only**: a 3×3 (nine-zone) strike zone; eight adjacent outside-zone directions; the actual batter side applied to each pitch; a minimal `Clear`/`Borderline` boundary classification; distinct pitcher target zone versus actual pitch location; a bounded nine-zone tendency vector per batter with a neutral default; location-conditioned pitch and contact probabilities; a simple pitcher target-zone selection policy; confirmed pitch location recorded in events and replay; and a data-extension boundary for later zone analytics and weakness-exploiting AI. It does **not** approve advanced opponent-weakness AI, batter-tendency growth, precision ball physics, reproduction of real ABS coordinates or a finished heat-map UI; those remain deferred ideas. After this addition the Scope Lock is active again.

### 1.11 Club Finance Lite approved exception and renewed scope lock

Club Finance Lite is a lightweight autonomous-club subsystem, not a playable front-office mode. C48 derives total contracted payroll and future commitments from C13's authoritative player contracts, evaluates separate club player budgets and C40 salary-cap chargeable payroll, settles abstract club sponsorship revenue from confirmed C10 season facts, and supplies explainable projections to annual salary, FA and trade decisions. It does not duplicate contracts, salary payments or the player's C38 commercial sponsorships.

`Total Player Payroll`, `Salary-Cap Payroll` and `Club Player Budget` are distinct values. SalaryCapRules determine included players/components, exemptions, assessment dates, threshold and whether an excess is prohibited, permitted with a penalty or has another consequence. Sponsorship income never changes that legal threshold automatically; only a bounded ClubFinancePolicy may allocate part of realized revenue to a later player budget. Existing binding salary obligations remain enforceable even when future spending is constrained.

Club sponsorship uses abstract/versioned agreements with annual base revenue and bonuses tied only to finalized regular-season, postseason and championship facts. Unique settlement evidence makes base/bonus realization idempotent across reload, repeated closure and projection rebuild. Renewal uses deterministic bounded annual changes, multi-season smoothing, baseline support and minimum/maximum policy limits so success cannot create unlimited wealth and struggling clubs can recover. Real club sponsorship amounts or confidential budgets are never presented as verified facts without certified evidence; fictional assumptions and game-model estimates carry explicit provenance and player-facing labels.

The player may inspect Korean read-only financial summaries and reasons relevant to contracts, but cannot allocate club budgets, negotiate club sponsorships, hire staff or act as owner/GM. Gemini has no financial decision authority. Ticketing, stadium operations, broadcast rights, ownership/investment, staff payroll, debt/bankruptcy, accounting statements and other franchise-finance features remain excluded. CF authorizes no further gameplay system, no new top-level phase and no production implementation in this revision. (The "broadcast rights" exclusion concerns revenue; the presentation-only BCL of §1.12 is a separate approved feature.)

### 1.12 Broadcast Commentary Lite — owner-approved presentation addition

**Status:** user-directed addition of 2026-10-11, planning approved; implementation not started. It is a bounded exception to the Scope Lock (§1.10), adds no new top-level phase and keeps Phases 0–29 and their numbering/completion status unchanged (Phases 0–2 stay approved as recorded in CURRENT_STATE.md).

Add an offline, text-only, rule-based in-game broadcast chat. It reuses authoritative scored-game events and visible, confirmed context; commentary has no authority over game outcomes, strategy selection, scoring, player ratings, season facts or saves. It does **not** implement broadcast rights, television revenue, club finance, authentic broadcaster likenesses, Gemini, external AI/API calls, TTS or required networking.

Ship **exactly two fictional broadcaster crews** initially, each with a caster and an analyst/commentator. Crew A uses restrained play-by-play and tactical analysis; Crew B uses energetic reactions, light banter and grounded tactical explanations. Names and portraits are provisional presentation assets, not game-logic identifiers. Use one reusable commentary service with two independently authored Korean dialogue packs, not two engines.

Select one crew at game start by a **documented, deterministic A/B rotation across the controlled player's consecutive games**, and keep it for the whole game. Persist the assignment against a stable game/fixture identity; save/reload, watched/instant modes, weather postponements/rescheduling and revisiting archived games cannot silently reroll it. Phase 5 prototypes the assignment against its stable test-game identity (no season calendar exists yet); Phase 6 binds it to the canonical persisted fixture/reschedule chain and keeps every already-assigned game's crew unchanged. The crew is committed once, when the game or fixture chain first starts: a fixture postponed or cancelled before it starts consumes no rotation slot, and a started game that later resumes, replays or is rescheduled keeps its crew.

Messages derive only from committed game events and permissible read-model facts (inning, outs, score, bases, scoring plays, verified player records, measured recent-game context where available). Never invent pitch sequences, quotations, injuries, skills, psychology, records or tactical causes, and never disclose hidden NPC ratings/potential. Routine actions receive sparse lines; game-changing events may produce a short two-speaker exchange. Korean text is skippable, scrollable and nonblocking; controls include off / highlights / fuller commentary, limited by screen and available event granularity. Skipped and instant games never require replaying thousands of lines.

Dialogue variation uses a **separate deterministic presentation selection key** derived from stable event/game identity, crew ID and dialogue-pack version, never authoritative gameplay RNG. Deduplicate by event ID + presentation version; replay and reopening cannot duplicate or reorder messages. Record the frozen pack/version and relevant immutable presentation references (or a stable transcript) where needed for historic re-display, without treating dialogue as a core game fact. Renderer failures, missing dialogue content, disabled settings or changed layout leave the scored game, RNG/event hashes and pending gameplay decisions unchanged. Gemini/generative commentary, TTS or further crews need a later explicit design decision. Phase placement: 3 (boundary only), 5 (offline chat), 6 (fixture-bound assignment), 28 (final UX audit). BCL's Crew A/B are broadcaster crews and are unrelated to the LMV-A/LMV-B visualization modes of §1.13.

### 1.13 Live Match Viewer Lite — owner-approved presentation addition

**Status:** user-directed addition of 2026-10-11, design only; implementation not started. The user chose **LMV-A** (2D icon-based live field) first and **LMV-B** (SD-character motion) as a later extension. LMV-A is a bounded, presentation-only Scope Lock exception (§1.10) that adds no top-level phase, leaves Phases 0–29, their numbering and Gates unchanged, and does not alter CF (§1.11) or BCL (§1.12). **LMV-B is not approved for implementation**: it is a design-preserved future extension and no part of any current Gate. "LMV-A/B" names visualization modes only; BCL's "Crew A/B" names broadcaster crews. The two are independent.

**Flow.** (1) Game Core resolves and commits every outcome, out, runner movement, run and record. (2) A read-only Presentation Adapter in the Application layer (Godot-free, unit-testable) converts committed game events into presentation cues. (3) The LMV-A Godot renderer plays icon-based field animation from those cues. (4) A later LMV-B renderer may consume the same cues with separate SD sprite/animation assets. Visual physics never decide a result; no engine rebuild is implied, and no giant interface or general animation engine is built ahead of need.

**Presentation contract.** A `PlayPresentationCue` (or equivalent DTO) may carry only what the real event model can supply: GameId / EventId / event sequence, play kind, runner/out/run state before and after the event, batted-ball kind or direction where recorded, handling fielder or defensive position where recorded, the presentation effect kind and display order, and a presentation version. Exact ball coordinates, speed, catch point or throw order that the event model does not record are never presented as official fact. A generic presentation path may be drawn, kept visibly separate from the official record and never asserting an unrecorded defensive detail; an event with too little information degrades to a short ball movement, a generic state change or a static result display. Field coordinates are presentation-only and are never used in rules, fielding success or scoring.

**Separation of responsibilities (minimum).** Committed-event query · event→cue conversion · playback sequencing · LMV-A icon rendering · a single future attachment point for an LMV-B renderer. Keep these as small feature-focused pieces.

**Determinism.** Frame rate and animation speed never influence gameplay RNG. Watching, skipping, fast-forward and disabling animation leave outcome, score, records and event hashes identical for the same seed and strategy inputs. Any presentation randomness uses a presentation-only key separate from gameplay streams (as in §1.12), and no authoritative fact depends on it. BCL commentary selection and LMV playback keys are derived in distinct, explicitly named namespaces (so neither feature's draws can influence the other) and neither touches a gameplay RNG stream; Phase 5 implements and tests this separation. Interrupting playback or re-entering the screen never re-applies a game event; cue playback is deduplicated by event ID + presentation version.

**Future LMV-B.** Responsibilities are separated so SD sprites, running/catching/throwing motion, per-character facing, richer baserunning and effects can be added later. Detailed motion may need event information the model does not record today; such a need would require a separate, explicitly approved extension design. A drop-in, no-change replacement is not promised, and LMV-B development and final art are outside every current Gate.

### 1.14 Pitch Zone Lite — owner-approved simulation addition

**Status:** user-approved 2026-10-11 after Codex design review `READY WITH NOTES`; design only, no implementation started. It is a bounded Scope Lock exception (§1.10) inside Phase 3 (with a Phase 4 persistence consequence); it adds no top-level phase, leaves Phases 0–29, their numbering and Gates unchanged and does not alter CF, BCL or LMV-A. It makes the already-approved real internal pitch sequence (D1) carry a categorical pitch location; it is **not** a ball-physics model.

**Location model.**
- *Zones.* Seventeen stable zones under `ZoneModelVersion` `pzl-zones-v1`: nine inner cells `in.r{1..3}c{1..3}` (row 1 = high … 3 = low; column 1 = inside, 2 = middle, 3 = outside) and eight adjacent outer directions `out.high`, `out.high_in`, `out.in`, `out.low_in`, `out.low`, `out.low_out`, `out.out`, `out.high_out`. Identifiers are persisted strings; renaming, adding or re-meaning a zone requires a new `ZoneModelVersion`, never an edit in place.
- *Batter-relative.* "Inside"/"outside" are relative to the batter, so a mirrored switch-hitter does not change a zone's meaning. Each recorded pitch preserves the **actual batter side applied to that pitch** (including a switch hitter's chosen side) so an absolute (catcher's-view) mapping stays reconstructable.
- *Categorical, virtual.* A location is a categorical result of this game's simulation. It is not a measured coordinate and never represents real KBO ABS data.
- *Boundary class.* Each pitch also carries `Clear` or `Borderline`, assigned from a versioned table keyed by zone (the minimal class needed for call-mode and framing rules; the exact table is fixed in work package 3-B before calibration).
- *Target versus actual.* The pitcher's **target zone** is a strategy input; the **actual location** is a drawn, committed event fact. The gap comes from command, situation and versioned tuning.
- *Batter tendency.* Each batter has a nine-element **inner-zone** tendency vector (outer zones are not tendency-adjusted by default). The default is all-neutral. Values are bounded, applied with documented caps and average-effect neutrality (a non-neutral vector redistributes strengths and does not raise a batter's league-average output). It is separate from base skills, never exposed as a raw hidden value to the UI or Gemini, and does not grow or change in this version.

**Probability order.** `pitcher target zone chosen → actual location drawn → take/swing/contact observed → batted-ball, defense and advancement result → GameReducer.Apply`. The result is never drawn first with a zone assigned afterward. Location conditions, within the caps, the swing/take, called-strike and contact/quality branches; it combines with ability, matchup, state, C46 park context, catcher and battery modifiers without double counting.

**Events and fingerprint compatibility.** Every actual pitch Phase 3 creates carries a location payload (zone, batter side, boundary class): `PitchEvent` and `TakeEvent` in their `PitchSpec`, and `InPlayEvent` as the one actual pitch it already stands for. An `InPlayEvent` counts as that pitch, so recording its location never creates an extra `PitchEvent`. Phase 2 events have no location; that is normal legacy data and no location is ever inferred or backfilled. Fingerprints: `ef1` text, SHA-256 and receipts are never changed by even one byte; events that carry a location use `ef2`, which includes zone, batter side and boundary class. `ef1` and `ef2` coexist and are verified by the version recorded on each receipt; an incoming event whose id already has an `ef1` receipt but arrives with a location added, or whose id has an `ef2` receipt but arrives with its location removed, is a `Game.event_fingerprint_version_mismatch`, and an `ef2` event with an altered location or other content is a `Game.event_id_conflict`; none is ever accepted as a duplicate (an `ef1` receipt never had a location, so "location removed" can only apply to `ef2`; ADR-007). Events that carry no pitch keep their existing canonical form. A new explicitly versioned JSON event format distinguishes new data: a pitch-bearing event in that format with a missing location is an error, while existing fixtures and bare event arrays remain readable as legacy format.

**Call mode, ABS and Framing.** C40 owns a versioned called-pitch mode. In an automatic (ABS-style) mode, Framing's probability effect is exactly zero. In a non-automatic mode, Framing acts only on predefined `Borderline` pitches and within a cap; an already committed call is never changed. All of this is explicit RuleProfile data with version and provenance. The existing `GameRuleSnapshot.ContentHash` convention is preserved byte-for-byte for the legacy profile; new fields ship as a separately versioned profile, never as a silent change to an existing hash. No claim is made that this reproduces or certifies the real 2026 KBO ABS rules.

**Seeds and streams.** Location drawing uses a documented `game/location` stream (Phase 3-A records its name and versioned consumption order); the PCG32 algorithm and the existing seed/stream architecture are unchanged. BCL and LMV may read a confirmed location as a read-only fact; they never draw from game streams, and no consumer reconstructs or alters a location.

**Future analytics and AI boundary.** Phase 3's simple pitcher policy picks a legal target zone from its base strategy. A later weakness-exploiting AI must not read a batter's hidden true tendency vector; it may use only reports built from observable pitch and batted-ball records. Zone statistics use their proper denominators separately (pitches, swings, contacts, balls in play, hits) and mark insufficient samples. No such AI and no analytics UI is implemented under this exception.

## 2. Repository Structure

The delivered `outputs/` package is documentation, not a scaffold. At implementation kickoff, place its contents at the repository root. The current workspace is `E:/Madegame/Star_player`. Keep `Star_Player_Portrait_System_Design.MD` beside this roadmap as an unchanged independent design reference. Create only the directories needed by the approved phase.

```text
StarPlayer.sln
global.json                         # compatible SDK pinned in Phase 0
Directory.Build.props               # shared compiler settings
Directory.Packages.props            # exact package versions
AGENTS.md
CURRENT_STATE.md
ROADMAP.md
Star_Player_Portrait_System_Design.MD # independent portrait specification
sources/                            # unmodified design inputs + source manifest
docs/
  SYSTEM_CONTRACTS.md
  UI_ARCHITECTURE.md
  REQUIREMENTS_TRACEABILITY.md
  decisions/                        # only consequential ADRs
  testing/                          # scenario, balance, benchmark specifications
  reports/PHASE_<N>_REPORT.md
src/
  StarPlayer.Domain/
    Common/ Players/ Teams/ Baseball/ Competitions/ Careers/ Life/
  StarPlayer.Simulation/
    Randomness/ Baseball/ Development/ ClubAI/ World/
  StarPlayer.Application/
    Commands/ Queries/ Sessions/ Ports/ Projections/
  StarPlayer.Infrastructure/
    Sqlite/ Datapacks/ Saves/ Assets/ Narrative/
game/
  project.godot
  StarPlayer.Client.csproj
  Bootstrap/
  UI/                               # scenes/presenters by screen
  Components/
  Themes/
  TestRunner/                       # deterministic UI smoke scenes
content/
  fictional/                        # starts with 2-team test pack; 10-team pack later
  schemas/
tests/
  StarPlayer.Domain.Tests/
  StarPlayer.Simulation.Tests/
  StarPlayer.Application.Tests/
  StarPlayer.Infrastructure.Tests/
  Fixtures/                         # inputs, not copied production logic
tools/
  StarPlayer.SimHarness/
  StarPlayer.PackTool/               # added only when needed
```

Use ordinary C# records/value types and feature-focused classes. Domain identifiers are distinct types, persisted as opaque canonical strings. Existing imported IDs are retained with pack provenance; generated IDs use a career-scoped namespace and persisted deterministic counter. Forked saves may reuse subsequent entity IDs because their databases are isolated; branches are never merged. Names are never keys. .NET GUIDs can identify careers/slots/branches outside simulation; do not consume simulation RNG for filenames or UI session tokens.

Initial dependencies: standard .NET APIs, `System.Text.Json`, `Microsoft.Data.Sqlite`, and xUnit for core tests. No ORM, mediator framework, Godot SQLite plugin, or extra web UI. Pin package and engine versions after the Phase 0 desktop export probe. Use the .NET-enabled Godot build and compatible SDK; [Godot's C# documentation](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html) documents these prerequisites. Do not pick a preview or assert a moving “latest” version in this roadmap.

Separate the Godot project's source root from core/test roots to avoid default C# globbing accidentally compiling test or library files into the client. Assemblies and native SQLite libraries must be verified in the exported desktop build, not only in the editor.

## 3. Core Domain Model

### 3.1 Aggregates and ownership

| Aggregate / entity | Owns | References, not ownership |
|---|---|---|
| Career | Controlled player, world clock, stage, rule snapshots, deterministic allocator/RNG metadata, revision | All subsystem records by ID; never an eagerly loaded world graph |
| Player | Identity, birth date, body, handedness, origin history, personality, career status | Club, contract, jersey assignments, draft facts, injuries, observations, statistics |
| Ratings / DevelopmentProfile | Current 0–100 skill vector including separate `CatcherRatings`; hidden per-skill growth envelope and aging parameters | Training/exposure inputs; no UI access to hidden profile |
| PlayerState / Workload | Condition, fatigue, sharpness, stress, satisfaction, recent-form state; dated pitch/PA/field/catcher workload | Player and injury modifiers; catcher load is not a base-rating penalty |
| Proficiency | Position and hitter/pitcher-role familiarity, learned pitches, transition history | Base skills; changing role does not erase them |
| Club / Roster | Club policy, `ManagerProfile`, `ClubPhilosophy`, staff assignments/tenures, captain assignment, bounded `TeamAtmosphere`, managerial evaluation, domestic contract affiliation, dated registrations and assignments, team role | Competition squads, players and retained manager/coach characters |
| JerseyNumberAssignment | PlayerId, ClubId, number, effective interval and reason; active club uniqueness and C31 restriction evidence | Player/Club identities; C31 owns retired-number honor, not historical assignments |
| BatteryChemistry | Pitcher-catcher pair familiarity, shared exposure and progression metadata | Separate from friendship, trust, C proficiency and base ratings |
| RuleProfile / GameRuleSnapshot | KBO Rule Engine area versions, resolved rules, provenance and effective dates | Consumers in §1.6; no duplicate scattered rule ownership |
| Stadium / ParkFactorProfile | Stable venue identity, presentation metadata, bounded versioned simulation factors and dated usage | Clubs/fixtures reference StadiumId; played games retain resolved venue/profile context through C46 |
| Competition / Season / Schedule | Rule version, stage dates, entries, fixture identity/status, venue/weather/reschedule links, All-Star voting state and standings source | Clubs, national squads, military squad |
| Game | Lineups, batting order, active defense, pitchers, inning/half/count/outs/bases, score, substitutions, decision boundary, RNG cursor and resolved stadium/park context | Players via resolved simulation snapshots |
| ScoredPlay / StatLine | Baseball facts and attributed increments; game-player-team-competition splits | Game and player IDs; counting facts are immutable after finalization |
| InjuryEpisode | Part, side, kind, severity, prognosis, recovery stage, relapse links and medical availability | Player; durable history survives recovery |
| Contract / Offer / FAEligibility | Terms, signing/expiry, incentives, no-trade clause, accrued service, annual non-FA salary renewal/negotiation and FA offer state | Player, club, agent agreement |
| Club Finance Lite | Club financial policy/version, annual player budget allocation, sponsorship agreements/settlements and saved salary-cap assessments | C13 contracts remain the sole salary-obligation source; C10 supplies finalized achievement facts |
| Draft / Prospect / AmateurTeam | Data-driven imported/generated class source, completed-history lock, eligibility, order/picks, observation estimates, origin enrollment history, school reputation/output history | The same Player identity before/after entry and when selected as controlled player |
| NationalSelection / TournamentEntry | Eligibility and squad membership, international results/medals | Domestic affiliation is preserved |
| MilitaryCareer | Status transitions, application/acceptance, service dates, return affiliation, sports-personnel classification | Eligibility rules, domestic contracts, temporary military squad |
| Character / Relationship | Person identity and player-centered directed relationship edges with category/reason | Player-linked NPCs or non-player family/agent/staff identities |
| Wallet / Ownership / Activity | Integer KRW ledger, owned assets, scheduled lifestyle/training/commercial time | Contract obligations, catalogs, player state effects |
| Story / PendingDecision / Interview | Eligibility/cooldowns, confirmed participants, deadline/barrier state, interview context/raw answer, allowed consequence rules and apply-once receipts | Inbox is a read model; C43 validates semantic interpretation before core consequences; prose remains presentation |
| History / Legacy | Career/team/league facts, draft provenance, jersey-number timeline, awards, milestone occurrence, retirement, number-retirement and Hall cases | Source records and immutable IDs |
| NewsItem / NarrativeBundle | Confirmed fact and stored-interview references, importance, text/reaction cache and request status | Never authoritative outcomes; no invented past quote |
| PortraitRecord | Stable appearance seed/version, display mode, image metadata and local asset references | P owns design; separate from baseball state/RNG |
| UserPreferences / DiscoveryViews | Help suppression/settings/watchlist and scoped read/navigation state | No authoritative simulation state; Game Archive/milestone progress query retained facts |

Player status is orthogonal: `origin_type`, ordered `origin_history`, `entry_path`, `draft_status`, `registration_status`, dated `club_affiliation`, `league_assignment`, `availability`, and `career_status`. National/military appearances do not overwrite the underlying club identity. A club may enter multiple competitions; a national team is a competition squad, not a KBO franchise. `team_id` identifies clubs/squads in typed contexts; amateur organizations have `amateur_team_id` with an explicit import mapping from source examples' `team_id`.

### 3.2 Shared value objects and invariants

- Birth date is authoritative; age is calculated against world date. Use date-only world time plus an explicit daily stage. Save timestamps are separate UTC metadata.
- Money is signed 64-bit integer KRW with checked arithmetic; percentage calculations use explicit decimal rounding rules. No floating-point money.
- Pitching innings are integer outs. Display 112 outs as `37.1`, never treat `.1` as a decimal fraction of an inning.
- A score change must have scored-play evidence. A player can appear as hitter and pitcher in the same game; unique keys must include stat role/split, not assume a single role.
- Roster legality includes registration, competition eligibility, injury/military availability, position coverage, workload, and mutually incompatible assignments. Team role is neither registration nor personal friendship.
- Retain active, released, amateur, and retired identities while history references them. No cascading delete of retired players or extinct/renamed teams.
- Base ability changes only through explicit development/aging/major-injury decisions. Short slumps, money purchases, relationships, and a single good game cannot directly overwrite base ratings.
- School reputation and entry path cannot assign a mandatory potential tier. Normal and elite outcomes remain possible from every supported background.
- A player's ratings never absorb a home-park adjustment. C46 supplies a bounded immutable `ParkFactorSnapshot` to resolution and metric contexts; finalized games retain that snapshot/version even after venue or factor changes.
- Staff identity, active assignment, manager evaluation/trust and personal relationship are separate. A manager change can reset bounded evaluation context while prior tenure/relationship facts remain queryable.
- Captaincy and TeamAtmosphere may influence social/story/state probabilities within declared caps; neither directly grants large permanent baseball-rating changes.
- Jersey numbers are effective-dated club relationships, not Player attributes. At most one active assignment per player/club context and no prohibited overlapping active club number; change/transfer closes the prior interval atomically. Historical assignments remain valid after a later club-specific number retirement.
- Imported completed draft facts retain PlayerId, source/provenance and club/pick/entry facts and are never rerun. Generated classes use a saved marker, deterministic allocator/RNG and persist once; load cannot regenerate or reroll them.
- C13 contract obligations are the only authoritative source for player salary commitments. C48 may derive/cache payroll projections and saved assessments, but cannot create a second contract or payment ledger. Total payroll, salary-cap chargeable payroll and club player budget remain separate checked-integer-KRW values.
- Club sponsorship and C38 individual player sponsorship are separate. A C48 settlement requires confirmed achievement evidence and a unique receipt; projections, screen opens, reloads and repeated season closure cannot realize revenue twice. Sponsorship revenue never edits the legal salary-cap threshold.

### 3.3 Catcher and battery boundaries

`CatcherRatings` contains Receiving/Framing, Blocking, Pop Time and Game Calling as distinct 0–100 skill values. Pop Time is a higher-is-better skill evaluation, not a raw seconds measurement; any observed time retains its unit separately. Shared Fielding, Arm and Endurance/physical workload capacity remain shared. **C position proficiency is independent**: excellent tools/low experience and average tools/high experience are both valid. No single CatcherRating or Caught Stealing Ability replaces these inputs; visibility follows self exact/NPC observed rules.

`BatteryChemistry` is keyed by `(catcher_id, pitcher_id)`, with bounded chemistry, `shared_appearances`, `last_battery_date`, last processed exposure/update, tuning version and required progression receipts. Count actual shared defensive appearances, not two separate player appearances or bench membership. Different pitchers have different pair records with the same catcher. Default missing history is neutral/unknown, never invented past experience. Shared games, represented camp/bullpen sessions, repeat seasons and actual high-pressure exposure can increase familiarity; long inactivity may cause slow bounded decay. Phase 7 defines/tunes progression before Phase 9 selection consumes it. Phase 21 may supply a modest personal-familiarity growth modifier only where an actual relationship is represented; it never requires a league-wide friendship graph.

Chemistry influences effective coordination, receiving/blocking decisions, communication mistakes and use of Game Calling within declared caps. It never grants permanent pitcher Stuff or other base skill. Pitcher delivery/control, runner speed/observed stealing context, catcher Arm/Pop Time/C proficiency and situation remain separate caught-stealing inputs. Do not add an unrelated steal-rating expansion just to implement this contract.

### 3.4 Public contracts

The exact subsystem responsibility/input/output/state/dependency/interface/event/storage/failure/test contracts are in [SYSTEM_CONTRACTS.md](docs/SYSTEM_CONTRACTS.md). The following architectural signatures are notation, not production code:

```text
Execute(CommandEnvelope) -> CommandOutcome(revision, public_changes, pending_decisions)
Query(QuerySpec, ViewerContext, revision) -> PublicReadModel
AdvanceGame(GameState, LegalStrategy, RuleSnapshot, RngCursor) -> GameStep
AdvanceDate(AdvanceRequest) -> AdvanceOutcome(last_committed_stage, stop_reason)
Commit(ChangeSet, Events, ExpectedRevision) -> CommitReceipt
CreateSnapshot(SaveRequest, CommittedRevision) -> SaveReceipt
LoadSnapshot(SlotId) -> ValidatedSession | LoadProblem
ValidatePack(PackSource) -> ValidationReport
CreateWorld(ValidatedPack, CareerStartSpec, Seed) -> WorldInitialization
AssessClubFinance(ClubId, SeasonId, Proposal?) -> ClubFinancialAssessment
RenderNarrative(PublicFactBundle, PresentationPolicy) -> NarrativeResult
InterpretInterview(InterviewRequest) -> UntrustedInterviewInterpretation
ValidateAndApplyInterview(InterviewId, AnswerRevision, InterpretationEnvelope) -> InterviewOutcome
```

`CommandOutcome` has accepted/rejected/stale/failure states and stable error codes. `ChangeSet` is an internal explicit collection of owned entity changes and projection increments, not an untyped object graph or a UI-supplied SQL patch. `ViewerContext` is created by the session, not trusted from a screen asking to become an administrator.

## 4. Data Flow

### 4.1 Game action and scoring

```text
Control input + decision_id + expected_revision
  -> application validates controlled actor and legal action
  -> game engine resolves with immutable inputs + rule version + RNG cursor
  -> scored play + workload + game state + domain events
  -> transactional state/stat/history updates
  -> new committed revision
  -> visibility projection -> main-thread UI update
  -> optional template/Gemini presentation queue
```

A game reducer owns legal baseball transitions; a probability resolver chooses outcomes. Keeping them separate permits exact scoring tests independent of balance. Under approved D1, the primary orchestration unit is a plate appearance: the player normally chooses one PA approach, the core resolves a real internal pitch/count sequence, and the UI displays only that simulated count. An explicit one-pitch action such as `Take` resolves the next actual pitch and, if the PA continues, returns to an appropriate decision point. Default play never requires input on every pitch. Non-PA plays such as steals or pickoffs are explicit scored transitions and do not fabricate a completed PA.

Before Phase 3, specify the resolver as ordered, bounded conditional probabilities: plate discipline/HBP/contact branch, batted-ball kind/quality, defense result, and advancement. Combine neutral league baselines with ability, matchup, state, proficiency, a resolved C46 `ParkFactorSnapshot`, and selected strategy using documented monotonic modifiers. Normalize conditional distributions; record model and tuning versions. Park factors can slightly alter approved HR/hit/XBH branches within caps but never mutate player ratings. Reuse one resolver for watched, skipped, second-team, and international play. UI playback never consumes authoritative randomness. Phase 3 also consumes distinct catcher skills/C proficiency and a bounded battery context; neutral pair values support initial fixtures, with actual progression added in Phase 7. Framing affects only marginal called pitches where the approved pitch/count and umpiring rules permit it (zero in an automated-strike-zone profile); Blocking affects difficult-pitch handling/PB and runner-advance risk; Pop Time combines with Arm, C proficiency, pitcher delivery/runner control and runner speed/stealing context. Game Calling improves bounded selection/execution quality, never the pitcher base vector. Avoid counting the same defense/chemistry/park modifier twice. Pitch Zone Lite (§1.14) places target-zone selection and a drawn actual location ahead of the discipline/contact branch, so every real pitch has a location before its outcome is resolved.

Track runner identity, responsible pitcher, earned/unearned attribution, batter/runner advancement, and outs. Store the information needed for wins/losses/saves/holds and later advanced metrics from the first scored-game implementation. Do not calculate ERA from all runs or infer RBI from every team run.

### 4.2 Calendar and decision barriers

```text
Advance request
  -> resolve existing required decisions
  -> dated eligibility/contract/military/registration transitions
  -> resolve fixture StadiumId and isolated deterministic weather context
  -> planned training/life workload and recovery
  -> squad selection and fixtures in stable competition/game order
  -> player-relevant decisions; pause at a committed boundary
  -> other games using the same engine
  -> stats, trust, reputation, milestones, decisions
  -> end-of-day development / season-stage hooks
  -> stable commit, coalesced autosave, publish date
```

The persisted day cursor records completed stages and games. Advancing to a target date stops at the next unresolved player choice and returns its reason. It never silently accepts an annual salary/contract offer, plays through an injury choice, or skips a national/military decision. Weather uses its own versioned RNG stream and can only reach fixture interruption/rescheduling through C09/C40; an extra weather draw cannot alter game, injury, draft or other streams. Long jobs can cancel between committed stages. No single season-long transaction and no date clock that outruns incomplete games. `PendingDecisionView` (C42) makes these same persisted barriers visible in Home and `의사결정 대기함`: type, subject, due date, status, blocking reason and route to the authoritative action. Opening/dismissing a notice does not resolve it. Explicit expiry is a committed rule outcome with a retained notice, never silent disappearance. AI-native interview interpretation can remain pending at its required barrier; an article/fan/SNS generation failure alone is presentation work and does not block the world.

### 4.3 Datapack initialization

```text
Folder or individual-file selection
  -> staging pack with explicit manifest / replacement IDs
  -> parse + schema + cross-reference + asset checks
  -> complete validation report (errors block, optional assets warn)
  -> normalize approved pack version
  -> one initial-world transaction into new temporary career DB
  -> integrity checks + initial snapshot
  -> publish career and menu entry
```

Individual files and overlay mods are materialized into one validated effective pack before world creation. Replacements must identify the entity they replace; duplicate IDs do not silently become last-file-wins. Installed packs are available for new careers. Import never mutates an open career.

### 4.4 Save and load

```text
Stable revision -> SQLite backup to new generation temp file
  -> close/flush -> validate -> publish generation -> update slot catalog
Load -> inspect header -> backup/copy -> migrate copy if needed
  -> SQL and semantic checks -> build session -> swap active session
```

### 4.5 Narrative and AI-native interviews

```text
Core confirms event/context -> importance routing
  -> routine information: procedural/template Korean presentation
  -> high-value article/fan/SNS bundle: Gemini expressive presentation

Core creates interview request + confirmed facts + allowed semantic vocabulary
  -> reporter question -> player types answer -> persist original answer/revision
  -> Gemini structured interpretation (untrusted)
  -> Application validates schema, tags, referenced facts/targets and request identity
  -> Game Core looks up bounded consequence rules
  -> one transaction: consequences + interview completion + application receipt
  -> Gemini reaction bundle from confirmed outcome + actual stored answer
  -> validate/escape/cache presentation; retain pending/retry on failure
```

Only `target`, `topic`, `tone`, bounded `intensity`, `sentiment` and explicitly approved semantic tags/references may enter the interpretation schema. Freeze the finite vocabulary, type/range limits and rule mapping before enabling the feature. Reject unknown tags/IDs, invented factual references, extra mutation fields or unsupported meanings; the model cannot send arbitrary relationship/reputation deltas. Raw prose never crosses this boundary as executable intent.

Bind each request to career/branch/session, interview ID, answer revision/hash, fact fingerprint and interpretation/rule versions. Reject stale callbacks and changed eligibility before commit. A unique receipt per interview in its branch prevents retries, edits or two concurrent replies applying consequences twice. A loaded/forked snapshot inherits completed interview status and receipts; allocating a new branch never reopens an applied interview. Only restoring a pre-decision snapshot leaves an independently pending decision in that fork. Persist the accepted interpretation and the core-computed effects atomically; deterministic replay consumes this recorded input, never calls Gemini again. Editing an unapplied answer invalidates previous requests. An applied answer and its exact original text become immutable history.

Reporter follow-up, sports media, club/opponent fans and general/critical community voices can form one connected bundle. Opinions, jokes, emotional reactions and labeled speculation may be expressive; they cannot create facts. Supply a past interview only by its actual stored ID/text; never generate an attributed past quote absent from that context. Facts/entity references/quotes receive explicit validation, with unsupported claims rejected or withheld for retry. Schema checks alone do not prove semantic truth; adversarial content tests and the approved D5 distinction remain mandatory.

On timeout/no key/quota/invalid output, preserve the submitted answer and pending interaction, expose Korean retry/status, apply no consequences and keep loading/saving safe. Required interviews remain visible barriers; defer/decline is offered only when core rules allow it. Failures in later reaction generation never roll back or repeat already-committed consequences. No fake AI/template interpretation is inserted. Routine news remains available independently.

### 4.6 Portrait presentation

Follow P §§4–7/31–34 in Phase 24 for Seed 2D, stable face vs club/national/neutral context, mode and image fallback. Phase 26's separate portrait work package follows P §§8–30 after that base passes. AI image requests require explicit user selection/generate/regenerate actions; profile opens, awards, national selection, aging and save loads trigger zero image API calls. Preserve seeds and save-scoped local cached images; missing/corrupt images fall back to Seed 2D, missing kit assets to neutral. Valid supplied real photos have baseline precedence for real datapack players; an explicit AI mode with a valid image follows P §32. Do not merge narrative-interview authority with image generation.

### 4.7 Club Finance Lite

```text
C13 authoritative contract obligations
  -> C48 payroll projection (total / cap-chargeable / future commitments)
  -> C40 saved SalaryCapRules assessment
  -> separate ClubFinancePolicy player-budget assessment
  -> C13/C18/C19 receives one explainable financial decision input

C10 finalized season/postseason/championship facts
  -> C48 verifies sponsorship condition and unique settlement receipt
  -> bounded annual renewal and next-season budget allocation
  -> history/read models
```

Phase 11 owns baseball results and achievement facts and has no dependency on C48 or Phase 14. C48 consumes only committed facts once Phase 14 exists. Offer/signing/trade/renewal commands validate all affected obligations, budget policy and rule-defined cap consequences in one application transaction; failure leaves contracts, rosters, money and finance history unchanged. Financial calculation uses checked integer KRW, saved rules/policies and deterministic ordering, never wall time, Gemini or generated prose.

## 5. Persistence Strategy

### 5.1 Files, versions, and authority

Use one SQLite working database per loaded career session and independent immutable snapshot generations for manual/autosave slots. The working DB is crash-recovery material; it is not automatically displayed as a successful manual save. A small JSON slot catalog is an index that can be reconstructed by scanning snapshot headers.

Each save header records `career_id`, `branch_id`, controlled player, game date/stage, revision, engine build, database `schema_version`, `save_format_version`, required feature versions, rule/tuning snapshot IDs, RNG algorithm/state version, datapack ID/name/version/hash, UTC saved time, and completion marker. UI summary fields are cached metadata and must agree with validated database values.

Loading an older slot starts a new branch with recorded parent snapshot and a separate working DB. Save generation ordering uses saved time plus a monotonic catalog sequence, not the simulated season year. Continue scans newest to oldest compatible valid snapshots; a corrupt newest save produces a recovery notice and does not hide older valid slots. Explicit Load identifies incompatible newer saves and offers compatible alternatives without silently rewriting them.

SQLite schema, datapack schema, event payload schema, rule/tuning version, simulation version, metric version, and UI preferences version are separate concepts. A file schema migration cannot claim to preserve old simulation results after probability code changes. Determinism is guaranteed only for a specified engine/model/rule/RNG version and recorded command sequence.

### 5.2 Logical tables and indexes

These are design-level table families, not executable migrations. Add each family in its owning phase.

| Family | Authoritative contents / keys | Critical indexes / uniqueness |
|---|---|---|
| `save_meta`, `schema_migrations`, `rule_snapshots`, `rng_streams`, `id_counters` | Session and compatibility state | One metadata row; unique migration version; stream key |
| `players`, `player_origins`, `ratings`, `development_profiles`, `player_states`, `proficiencies`, `pitch_repertoire` | Typed player state, isolated hidden data | `player_id`; origin player/order; player/skill; player/position |
| `clubs`, `squads`, `registrations`, `assignments`, `jersey_number_assignments`, `staff`, `staff_assignments`, `manager_profiles`, `club_philosophies`, `manager_evaluations`, `captain_assignments`, optional `team_atmosphere` | Dated affiliation, availability, jersey-number timeline/reason, role, staff tenure, captaincy and bounded policy/context state | Club/level/status; player/club/number effective intervals; staff/club/profile/role effective dates; no overlapping incompatible assignments |
| `battery_chemistry`, `battery_exposures` | Pair-specific familiarity and real shared exposure/progression | Unique catcher/pitcher pair; unique pair/exposure source; last processed date |
| `stadiums`, `park_factor_profiles`, `stadium_usage`, `competitions`, `seasons`, `fixtures`, `game_states`, `game_players`, `scored_plays` | Venue identity/presentation, versioned bounded factors, dated usage, fixtures with weather/reschedule relations, and resumable games retaining resolved stadium/profile context | Stadium/profile/version/effective date; competition/season/date/status; original/makeup fixture relation; game/play sequence; player/game |
| `game_batting`, `game_pitching`, `game_fielding`, `season_stats`, `career_stat_baselines` | Integer counting facts and imported baseline coverage | Player/competition/season/phase/team/stint/stat role; no double-counted total rows |
| `injuries`, `rehab_steps`, `workload_days`, `training_plans` | Episodes, exposure, plans | Player/date; active injury status |
| `contracts`, `contract_terms`, `offers`, `salary_negotiations`, `salary_history`, `service_credit`, `trades`, `agent_agreements` | Terms, annual non-FA renewal rounds/decisions, offers, accrual and transactions | Player/season/effective dates; team/expiry; one authoritative renewal outcome; unique accepted offer and obligation |
| `club_finance_policies`, `club_budget_allocations`, `club_sponsorship_agreements`, `club_sponsorship_settlements`, `salary_cap_assessments` | Versioned financial assumptions, annual player budgets, abstract sponsor terms, realized revenue/evidence and historically consequential cap results; no duplicate contract terms or player payments | Club/season/effective period; unique agreement period and settlement condition/evidence; one assessment per declared rule/date/version |
| `amateur_teams`, `prospect_enrollments`, `observations`, `drafts`, `draft_picks`, `entry_history` | Imported/generated class source/provenance, completed lock, generated cohort marker/seed, scouting, picks and entry facts | Draft/year/source; unique player/pick and draft/player; imported/generated PlayerId collision rejection |
| `national_selections`, `tournament_entries`, `medals`, `military_careers` | Eligibility and temporary participation | Tournament/player; player/status; dated service transitions |
| `characters`, `relationships`, `stories`, `pending_decisions` | Relevant people and choice/barrier state | Player/character/type; due date/status; unique applied choice |
| `interviews`, `interview_answers`, `interview_interpretations`, `interview_receipts` | Context, preserved raw answer, accepted semantic input and authoritative apply-once receipt | Interview/answer revision; unique applied interview per branch; request/fact/rule version |
| `ledger_entries`, `owned_assets`, `activities`, `commercial_contracts` | Money, ownership, schedule and commercial obligations | Player/date; unique source obligation/payment period; overlap queries |
| `history_facts`, `team_seasons`, `all_star_votes`, `awards`, `milestones`, `retirements`, `retired_numbers`, `hall_cases` | Durable history, meaningful voting/selection facts and legacy outcomes | Subject/date; team/season/competition; unique vote/result edition/player/position; award/season/category; team/number constraint |
| `domain_events`, `projection_receipts`, `news_items`, `news_links`, `narrative_bundles` | Integration facts, stored-interview links and expressive presentation/status | Event sequence; unique consumer receipt; subject/date/importance; fact fingerprint |
| `portrait_records`, `portrait_requests`, local immutable image assets | Saved cosmetic seed/version/mode/reference and per-player request status, not gameplay authority | Career/branch/player/context/request; safe opaque asset identity; no keys |
| Separate versioned preferences | Settings/help suppression/watchlist keyed by user and appropriate career/branch/player | Never hidden simulation values or gameplay RNG state |

Keep important searchable/filterable fields in columns. Versioned JSON is acceptable for rule snapshots, typed event payloads, rare event-specific extensions, and presentation cache metadata, not as a replacement for relational player/contract/roster models. Queries project and page the rows needed by the screen; no load-all-history ORM traversal.

Enable foreign keys on **every** connection and test them; SQLite documents their connection-specific activation in [Foreign Key Support](https://www.sqlite.org/foreignkeys.html). Parameterize values; whitelist sortable column identifiers. Prefer restrictive deletion and explicit archival flags. Validate interval overlap and cross-table eligibility in application transactions where SQL constraints alone are insufficient.

### 5.3 Commit and snapshot protocol

1. Single writer serializes commands. Start with rollback-journal mode and `synchronous=FULL` for simplicity. Reads are short and revision-consistent. Benchmark before considering WAL.
2. Compute a private change scope; validate the entire state transition. A transaction writes owned rows, RNG cursor, allocator, command receipt, exact stat increments, mandatory history, event outbox, and new revision together.
3. Commit succeeds before publishing state. Retried `command_id` returns its prior receipt. Crash before commit leaves no partial authoritative change.
4. A save request waits for a stable command boundary. In a paused game, save the full game state, lineup/substitution state, decision ID, RNG cursor, and day cursor. It must resume without rerolling or replaying a completed PA.
5. Write a new snapshot generation using the provider's SQLite backup support. Never copy an open database file as a backup. The [SQLite Online Backup API](https://www.sqlite.org/backup.html) provides the relevant snapshot mechanism.
6. Close and flush the completed destination; run database checks, required-header validation, and semantic revision checks. Publish by same-directory rename to a new immutable filename. Replace the catalog only afterward. A crash between these steps leaves either a valid discoverable generation or an ignored temporary file.
7. Keep the previous valid generation until publication and catalog verification succeed. Manual slots are never silently garbage-collected. Autosave rotation retains at least three completed generations plus pre-decision recovery until the paired post-decision snapshot succeeds; coalesce overlapping triggers, not distinct pre/post states.
8. Disk full, read-only folder, busy timeout, or backup failure leaves the prior save intact and surfaces a retryable error. Do not display “saved.” Failed required autosave pauses further automatic date advancement; expose Retry and an explicit continue-without-this-save choice.

If WAL is introduced later, use the backup API and manage checkpoint lifetime explicitly. A WAL database can depend on its sidecar files; [SQLite WAL documentation](https://www.sqlite.org/wal.html) explains why copying only the main file is unsafe. Filesystem publication and crash recovery still require platform fault-injection tests; do not claim power-loss guarantees from a rename alone.

### 5.4 Migrations and historical continuity

Run sequential forward migrations on a **copy**, never on the only old snapshot. Record applied versions and checksum. On failure, retain the old save and diagnostic; do not publish the candidate. Reject unsupported future versions. No silent destructive “reset save” fallback.

Every persisted phase adds a prior-version fixture, upgrade test, fresh-create test, and upgraded-vs-fresh semantic comparison. Existing worlds gaining a later feature need explicit initialization: absent relationship history stays absent, a new school cohort starts prospectively, and unavailable past WAR is unknown unless sufficient historical inputs exist. Never invent retroactive injuries, contracts, or records to fill a new column.

Datapack history imports carry source, coverage interval, scope, metric method, and completeness. Year-by-year history is preferred. A supplied career total becomes an opening baseline, not extra season rows counted again. A separately supported midseason import would require a cutoff and remaining schedule, but approved D3 fixes the personal KBO 2026 pack immediately before the regular season and forbids post-start evidence in preseason ratings. Imported six championships plus two simulated championships must display eight; absent per-year evidence is flagged incomplete rather than fabricated.

Later feature persistence must preserve §5.3 exactly. Save references for portraits point to immutable local asset generations; stage/validate/publish images before committing their reference, preserve the old image on failure, and let missing assets fall back without failing core load. Snapshot/fork references cannot overwrite an older branch image even when PlayerIds coincide. Deletion is an explicit cosmetic action scoped to references; no snapshot dependency is silently removed. Seeds/renderer-part-set versions remain saved so offline fallback remains reproducible. No asset copy/migration runs on the only valid save.

Interview raw answers, confirmed context, accepted interpretation and consequence receipts travel with the career and branch. Cache/transport retries cannot reapply them. Migration introduces catcher/pair/profile/interview metadata prospectively with documented defaults, never fabricated shared appearances or historical quotes. New stadium/profile, staff/captain, atmosphere, salary-renewal, All-Star vote, weather and club-finance fields use explicit prospective defaults; they never invent past tenures, votes, negotiations, weather, sponsorship settlements, salary-cap assessments or park-adjusted facts. Jersey migration preserves valid imported/current numbers as dated assignments where evidence exists and never fabricates earlier changes. Draft migration preserves existing player/pick/entry facts and adds an explicit source/completed marker without rerunning a draft or duplicating a Player. Help/settings/watchlist preferences are separate from simulation hashes and failures there cannot corrupt a career.

### 5.5 Long-save retention

Approved D6 is **preserve first, measure, then prune only if evidence requires it**. Permanently retain authoritative game results, box scores, lineups, PA outcomes, scoring progression, important game events, season/career/team-stint statistics, contracts and salary history, historically consequential annual payroll/budget/cap/sponsorship facts, draft/entry and full jersey-number history, injuries, awards, milestones, FA/trade/transfer, national/military, major relationship/career/news and legacy/Hall/retired-number facts, user-pinned content, and every authoritative input required for historical reconciliation. This includes the stadium/park/weather context actually used, meaningful staff/captain/All-Star facts, schools/origins, and submitted/applied interview answers with their factual context and receipts. Game Archive reads retained evidence with explicit coverage; it never reruns a game or draft to fill gaps. Rebuildable aggregates remain versioned caches.

Initially retain raw pitch-by-pitch traces and ordinary generated narrative rather than applying an automatic current-plus-previous-season limit. Phase 17 and Phase 29 measure working SQLite size, snapshot size, rolling/manual/pre-decision save multiplication, save/load/backup/migration time, archive/profile/history query performance, growth by table family, and the separate contributions of raw pitch trace and narrative data. Record actual results. If those measurements are acceptable, preserve the full retained history. If they demonstrate a material problem, a later targeted policy may remove or summarize only safely reconstructible caches, old raw pitch traces or low-importance presentation while preserving box scores, required PA/scoring/metric evidence, major career/news history, pinned content and every authoritative fact. Any such policy is explicit, migrated and tested. Do not add a user-facing retention setting unless measurements demonstrate a need, and do not weaken rolling recovery guarantees to reduce storage.

## 6. Testing Strategy

### 6.1 Evidence by layer

| Test class | Required evidence |
|---|---|
| Domain unit / transition | Boundary values, illegal operations, explicit rule fixtures and accounting identities |
| Deterministic simulation | Same version + state + seed + commands (including recorded accepted interview interpretations) produces identical canonical facts/RNG cursors; UI mode and query order do not alter results |
| Statistical calibration | Seeded multi-game samples compared with declared fictional or approved league targets; confidence intervals and model version recorded |
| Application integration | Decision barriers, stale commands, idempotency, rollback-memory consistency, task cancellation and date ordering |
| Persistence | Real temporary SQLite DB; FK/check constraints, fault injection, snapshot recovery, migration and round-trip state hashes |
| Long-save | Cohort supply, league strength distributions, roster solvency, staff turnover, captain/stadium/salary/All-Star/weather integrity, bounded atmosphere, club payroll/cap/sponsorship/budget stability and recovery, history consistency, and D6 database/snapshot multiplication, save/load/backup/migration, archive/profile query and table-family contribution measurements over declared season counts |
| UI smoke / usability | Actual Godot scenes, both supported resolutions from Prototype 2; navigation restore, focus, loading/error/empty, hidden-data scans |
| Export | Cold-start desktop export without editor or user real-world pack; bundled SQLite and local saves work |

Tests use synthetic packs with known expected results, not private real-player data. Seed suites are fixed in version control. Replay checks compare sorted canonical business facts; exclude file timestamps, branch/session bookkeeping, cosmetic portrait seed/mode/cache metadata, user preferences and generated narrative prose. Interview context, submitted-answer identity/hash, accepted interpretation and core consequence receipts remain replay evidence; original answer text is separately verified for exact retention. Compare event type/payload/logical sequence rather than its branch-qualified transport identity. Deterministic gameplay entity IDs and RNG cursor allocation are part of the hash. Loading a save must not reseed simulation from its newly allocated branch ID.

Partition RNG streams by `(subsystem, season/game/player/cohort context)` using a fixed documented PRNG algorithm and stable key derivation. Weather has its own fixture/date keyed stream. Generated draft cohorts/classes have saved generation markers, IDs and stream counters and are created once; reload reads them rather than drawing again. Deterministic jersey fallback uses a stable legal ordering or its own recorded choice and cannot perturb draft/game streams. Club sponsorship renewal/settlement uses saved policy, confirmed facts and deterministic calculations; it cannot consume baseball/development RNG or depend on wall time. Persist state/draw counters. Never use shared `System.Random`, randomized string hashes, unordered dictionary iteration, wall time, thread scheduling, raw Gemini output or generated prose as simulation inputs. The sole semantic-input exception is the C43-validated, persisted interview interpretation treated as a recorded external player-command input; replay applies the saved meaning through the same core rules without network. Online/offline parity compares identical accepted command/interpretation records, not two unrelated LLM responses or an unanswered interview against an answered one. Portrait generation and presentation remain outside gameplay RNG. An unrelated UI query, weather draw, finance screen open or extra news item must not change injuries, draft picks, financial settlements or a game's baseball RNG.

### 6.2 Mandatory game oracles

Fixture outcomes cover strikeout, walk, HBP, single/double/triple/HR, ground/fly/line outs, errors, double plays, sacrifices, force/tag outs, steal/caught stealing/pickoff, real pitch/count progression under approved D1 including one-pitch `Take`, inning change, walk-off, home-team skip of final half, ties/extra innings by rule profile, legal substitutions/DH/two-way cases, inherited runners, earned runs, and pitcher decisions. Add distinct wild-pitch/passed-ball attribution (including earned-run consequences), steal/caught-stealing responsibility, catcher defensive participation and actual catcher/battery workload. Exact supported variants are specified in the owning subset under approved D2.

For every scored game, player/team runs reconcile, hits and errors reconcile, outs agree with defensive innings, PA/AB classifications reconcile, and pitching totals use outs. Two-way game counting cannot double count the same team's game while separate batting and pitching appearances remain correct.

### 6.3 Performance protocol

Record CPU, RAM, OS, storage, build mode, fixture size, retained-log policy, and seed. Run Release builds, one warm-up and five measured runs. Provisional engineering budgets: ordinary screen query p95 <=250 ms on the recorded developer PC; cold long-save list <=2 s; one background full domestic day <=2 s; foreground input acknowledgment <=100 ms. D6 storage runs also record working DB and every retained snapshot family, operation times and table-family contributions before any pruning proposal. Exceeding a budget requires a report and targeted optimization before the relevant performance gate, not changing gameplay rules. These are project benchmark targets, not claims about untested hardware.

Use 100,000 PAs for resolver calibration, 100 complete games for reducer integration, ten seeds of one full season for Prototype 3, five seeds of 20 seasons for initial world turnover, and at least three seeds of 50 seasons for final soak. Long-world runs use an explicit harness policy that supplies player choices and permits world-only continuation after controlled-player retirement; this is a test tool, not post-retirement gameplay.

### 6.4 Common Gate G

Every phase's Acceptance Criteria invokes **G**. PASS requires all applicable items plus its own criteria:

1. Listed automated tests pass with commands, versions, seed/fixture IDs, counts, and exit codes recorded; a skipped required test means INCOMPLETE.
2. No regression in earlier phase tests; deterministic facts match where simulation is involved.
3. Changed durable state round-trips and migrates; fault cases preserve the last valid save. Before Phase 4, storage tests are explicitly not applicable.
4. Listed user workflows pass in actual scenes. Prototype 1 uses QHD; Phase 5 onward uses QHD and FHD. Hidden fields never appear in visible DTOs or serialized UI/narrative payloads.
5. Each acceptance row has PASS evidence. D1–D6 are approved and recorded. A phase remains INCOMPLETE if its required detailed RuleProfile subset, content-certification input or other declared implementation evidence is missing; that does not reopen the approved product policy.
6. `docs/reports/PHASE_<N>_REPORT.md`, `CURRENT_STATE.md`, and listed document updates exist and agree with actual work.

FAIL means an executed criterion failed. INCOMPLETE means missing work, evidence, approval, or environment. Neither is PASS. No next phase starts automatically after a report; the owner supplies the next approved scope.

## 7. Phase Dependency Map

The sequence contains **30 phases (0–29)** derived from persistence, simulation, calendar, and career dependencies. Phases are deliverable boundaries, not assumed single chat turns. Ordered tasks within a phase may take multiple implementation sessions; preserve the phase gate.

```text
0 Foundation -> 1 Player/baseball types -> 2 Scoring reducer -> 3 Simulation [Prototype 1]
  -> 4 Save/import spine -> 5 One-player loop [Prototype 2]
  -> 6 Calendar -> 7 Development -> 8 Injury -> 9 Club AI
  -> 10 Ten clubs + real second team -> 11 Season closure [Prototype 3]
  -> 12 Feeder/scouting -> 13 Draft/entry
  -> 14 Contracts/agent/pay ledger -> 15 FA -> 16 Trades/releases/intake
  -> 17 Twenty-season world validation
  -> 18 International -> 19 Military
  -> 20 Assets/life -> 21 Relationships/stories -> 22 Commercial career
  -> 23 History/advanced metrics -> 24 Complete discovery UI
  -> 25 Retirement/legacy -> 26 Narrative AI + optional Portrait AI
  -> 27 Pack tooling/content audit -> 28 Alpha -> 29 Polish/final soak
```

This is the recommended serial execution order. Direct prerequisites, allowing later reordering only with unchanged contracts, are:

| Phase | Direct prerequisites | Why this boundary exists |
|---|---|---|
| 0 | None | Exportable C# and SQL feasibility before a large codebase |
| 1 | 0 | Shared identities, player separation, privacy and rule vocabulary |
| 2 | 1 | Baseball legality before random probability |
| 3 | 2 | Correct two-club games before career/world expansion |
| 4 | 3 | Stable persisted game state before a large schema |
| 5 | 4 | Playable one-player loop and resumable choices |
| 6 | 5 | Stable world-date progression |
| 7 | 6 | Dated exposure needed for development |
| 8 | 7 | Workload/recovery and rare persistent injury effects |
| 9 | 8 | AI must reason about health, ability, proficiency and trust |
| 10 | 9 | Real second-team play and compatible assignments |
| 11 | 10 | Whole-season standings/postseason/history reconciliation |
| 12 | 11 | Feeder cohorts and observations before draft selection |
| 13 | 12 | Actual draft results and undrafted career branches |
| 14 | 13 | Durable entry facts before negotiations/payments |
| 15 | 14 | FA depends on contract/service-credit history |
| 16 | 15 | Safe transfers require roster, contract and market rules |
| 17 | 16 | New supply and departures both exist before turnover soak |
| 18 | 17 | International scheduling reuses stable leagues and squads |
| 19 | 18 | Military sports path and tournament awards have foundations |
| 20 | 14, 19 | Spend actual earnings with calendar and career availability |
| 21 | 9, 20 | Player-centered relationship/story consequences |
| 22 | 14, 18, 21 | Agent, fame, time, equipment and relationship integration |
| 23 | 11, 17, 19, 22 | Comprehensive domestic, international and life history |
| 24 | 12, 23 | Full databases consume complete historical read models |
| 25 | 24 | Legacy evaluation requires team-specific and career evidence |
| 26 | 21, 25 | Validated interview semantics and expressive narrative; Phase 24 portrait base already passed through 25 |
| 27 | 26 | Final schemas exist before complete import/content certification |
| 28 | 27 | Final visual system after feature and navigation stability |
| 29 | 28 | Polish and export/soak on the complete product |

Prototype 3 spans Phases 6–11: minimal player and team databases arrive in Phase 6, before its gate. The 10-club and second-team expansion is delayed until the two-club engine and player loop have passed. History facts are collected from Phase 2/4; Phase 23 adds richer aggregation, not the first record of history.

## 8. Development Phases

Each phase below specifies G plus phase-specific checks. UI tests from Phase 5 onward run at **2560 × 1440 and 1920 × 1080**. English names in UI Work describe functionality, not shipped labels: actual strings, including the Phase 0 diagnostic shell, remain Korean. No phase may redefine a source requirement through a test fixture. A provisional balance/rule fixture is labeled as such and cannot certify real KBO compliance.

## Phase 0 — Reproducible desktop foundation

### Goal
Prove the fixed stack builds, tests outside Godot, and exports with working SQLite on the development PC.

### Dependencies
None. Read §§1–6 and contract conventions. D1–D6 are approved; this documentation revision does not itself authorize Phase 0 implementation.

### Scope
Five project boundaries, test projects, composition root, empty Control shell, dependency pins, desktop export probe, and reproducible verification commands.

### Explicit Non-Goals
No baseball simulation, player generation, real rosters, career database schema, migrations, AI requests, or final theme.

### Architecture / Systems
Create the repository layout in §2. Application owns ports; Infrastructure implements them. Only Bootstrap knows adapter implementations.

### Domain Models
Only `PlayerId`, `TeamId`, `CareerId`, `WorldRevision`, and success/error result conventions. No speculative complete domain graph.

### Implementation Tasks
1. Inventory installed tooling; select a stable Godot 4.x .NET build and compatible .NET SDK/export templates. Record exact versions in `docs/toolchain.md`, `global.json`, Godot SDK reference, and package pins.
2. Create `StarPlayer.sln`, the five projects, four test projects, and explicit project references. Use nullable checks and warnings-as-errors for authored core code. Exclude generated files from that policy where necessary and document exceptions.
3. Add an architecture test that rejects core references to Godot, SQL, HTTP, or Infrastructure; enforce presenter access through Application contracts.
4. Implement `game/Bootstrap/CompositionRoot.cs`, `game/UI/MainMenu/MainMenu.tscn`, and its thin presenter. Display a static build/version panel.
5. In `tests/StarPlayer.Infrastructure.Tests/SqlitePackagingTests.cs`, verify a temporary DB transaction rollback, FK enforcement on two independently opened connections, and provider backup/reopen. Keep this a test schema, not a save format.
6. Provide `tools/verify.ps1` to run restore/build/core tests and a Godot headless smoke invocation; document exact paths. Export a desktop build and perform the same database probe in a diagnostic launch mode.

### UI Work
One QHD shell with Start Test/Exit and diagnostics result. No gameplay menus falsely marked functional.

### Persistence Work
Temporary test databases only; remove via test cleanup. No career migration version is consumed.

### Events
No domain events. Build/probe logs are diagnostics only.

### Tests
`dotnet build StarPlayer.sln -c Release`; `dotnet test StarPlayer.sln -c Release`; Godot headless shell launch; cold exported executable SQL probe. Report missing tooling as INCOMPLETE rather than fabricating successful output.

### Acceptance Criteria
**Gate: G +** all projects compile; core tests run with Godot closed; forbidden dependency test is green; exported build opens the shell and reports SQL commit/rollback/backup success without the editor; exact reproduction commands are recorded. Storage/replay gameplay clauses of G are not applicable yet.

### Deliverables
Solution/projects, shell scene, meaningful boundary/probe tests, pinned toolchain, verification script, `docs/reports/PHASE_0_REPORT.md`.

### Risks
Incorrect Godot/.NET pairing, duplicate C# compile globs, missing native SQLite export files. Resolve these before adding features.

### Documentation Updates
Update `CURRENT_STATE.md`, create `docs/toolchain.md`, and record ADR-001 confirming the project boundaries/export result.

### Claude Code Handoff
> Implement Phase 0 only. Use the fixed stack and §2 paths. Prove headless core tests and an exported desktop SQLite round trip. Do not create game rules or career tables. Record actual commands and gate evidence, then stop for phase review.

## Phase 1 — Player identity, visibility, and baseball vocabulary

### Goal
Prove the model can represent a hitter, pitcher, two-way player, and same-name players without leaking hidden state.

### Dependencies
Phase 0.

### Scope
Typed identities, player/base-rating/state/development/proficiency separation, club/squad, Stadium and dated jersey-number references, baseball value types, explicit rule-profile contract, and public projections.

Include catcher skills vs C proficiency and the KBO Rule Engine typed area/version boundary. Define pair IDs/default battery context now; progression and personalities follow in Phases 7/9.

### Explicit Non-Goals
No growth rolls, live games, complete pack importer, database writes, or fixed creation-budget balance.

### Architecture / Systems
`Domain/Players`, `Domain/Baseball`, `Domain/Teams`; `Application/Queries/PlayerVisibilityPolicy`; contracts C01–C06/C47.

### Domain Models
`Player`, `Ratings`, `DevelopmentProfile`, `PlayerState`, `RoleProficiency`, `OriginRecord`, `RegistrationStatus`, `LeagueAssignment`, `JerseyNumber`, `JerseyNumberAssignment`, `AssignmentReason`, `Money`, `InningsOuts`, `RuleProfile`, `PlayerPublicView`, `StadiumId`, `Stadium`, `ParkFactorProfile` and `ParkFactorProfileId`.

Add `CatcherRatings`, independent C `PositionProficiency`, shared Fielding/Arm/Endurance context, `BatteryPairId`, and typed KBO Rule Engine area/version references (C40/C41).

### Implementation Tasks
1. Implement the value objects/invariants from §3 with IDs distinct from names and registration distinct from level.
2. Define hitter six-skill and pitcher four-skill plus repertoire structures, each 0–100. Preserve birth date, handedness, body, and personality independently.
3. Implement session-bound visibility projections for self/NPC and observed estimates; exclude hidden profile fields by construction.
4. Build two fictional-club fixtures with legal batting/fielding roles, duplicate names, and one two-way player; keep all world content outside code.

5. Define the four catcher skills from §3.3 independently of C proficiency; add excellent-tools/novice-C and average-tools/expert-C fixtures. Extend exact-self/observed-NPC projections without hidden growth fields.
6. Define/validate KBO Rule Engine profile composition and version lookup; Phase 2 supplies GameRules, Phase 6 RegularSeasonRules and later phases their owned areas. No hardcoded 2026 constants.
7. Define C46 stadium identity, separated presentation metadata and bounded versioned HR/hit/XBH factor vocabulary. A club's current/home usage is a dated reference, not stadium ownership or a player-rating modifier.
8. Define C47 jersey-number value/range, effective-interval and club-specific uniqueness/retired-number restriction vocabulary. A player's identity and club affiliation do not embed a permanent number; imported and generated players use the same PlayerId-linked assignment model.

### UI Work
Diagnostic player cards show self exact ratings, NPC estimated ratings, separate state/proficiency fields and a current-number projection when assigned at QHD. True potential never appears.

### Persistence Work
Define logical mapping names for PlayerId-linked jersey intervals and draft provenance only. JSON test fixtures are versioned; no persistent career DB yet.

### Events
Define `PlayerCreated` and `PlayerChangedPosition` payload schemas; no simulated growth/event loop yet.

### Tests
Ratings reject <0/>100; age/date boundary tests; innings `112 -> 37.1`; same-name references remain distinct; self/NPC/cheat projection serialization contains no hidden fields. Returning to a known position preserves its proficiency.

Catcher skill bounds and Pop Time unit semantics; independent C proficiency; unknown rule area/version rejected; pair identity distinguishes one catcher with three pitchers; serialize no hidden catcher truth. Stadium/profile IDs remain stable across display-name changes; invalid/out-of-cap factor data is rejected. Jersey assignment references stable PlayerId/ClubId, rejects invalid values/overlapping active intervals and never changes Player identity.

### Acceptance Criteria
**Gate: G +** all four player scenarios serialize to legal public DTOs; independent fields round-trip in fixture serialization; an NPC cannot gain exact ratings by changing a query flag; no names used as join keys. Gameplay persistence clauses remain not applicable.

Catcher and C proficiency fixtures remain distinct in public views; typed rules route to one versioned owner and unknown versions fail explicitly. Stadium presentation and simulation data are separate and C46 owns the factor version boundary. C47 proves a jersey number is a club-specific dated assignment rather than a timeless Player property.

### Deliverables
Domain value types, visibility service, two-club fixtures, player-card probe and `PHASE_1_REPORT.md`.

### Risks
Single overloaded player-status enum; sorting by hidden values; accidentally clamping cheat ratings to a budget rather than the 0–100 skill scale.

### Documentation Updates
Contracts C01–C06/C46/C47; data dictionary notes in `docs/data-model.md`; current state and report.

### Claude Code Handoff
> Implement Phase 1 only using the Phase 0 boundaries. Keep every player dimension separate, including C47 dated club-specific jersey assignments, and make hidden-data exclusion testable. Do not simulate games, create SQL migrations, or select unresolved gameplay rules.

## Phase 2 — Baseball state transitions and scoring

### Goal
Prove a scripted sequence of baseball outcomes produces legal game state and reconciled statistics.

### Dependencies
Phase 1. Approved D2 supplies the 2026 KBO-oriented policy; specify the production GameRules legality subset and documented simplifications before this phase gate. Synthetic reducer fixtures may be developed independently.

### Scope
Lineups, batting order, real pitch/count state, innings/outs/bases, substitutions, runner attribution, scored outcomes, rule-profile variants, official counting classifications.

Catcher-relevant scoring, defensive participation/workload and KBO Rule Engine GameRules integration are required.

### Explicit Non-Goals
No probability balance, full season, user career, dynamic club AI, or final game visuals.

### Architecture / Systems
`Simulation/Baseball/GameReducer`, `Domain/Baseball/ScoredPlay`, `Application/Projections/GameStats`; C07/C08/C11.

### Domain Models
`GameState`, `PitchCountState`, `RunnerState`, `LineupSlot`, `PitcherUsage`, `ScoredPlay`, `GameStatDelta`, `GameRuleSnapshot`, `LegalActionSet`.

Add catcher attribution to `ScoredPlay`, `CatcherExposure` and actual `BatteryExposure` identifiers; retain WP and PB as distinct classifications.

### Implementation Tasks
1. Specify reducer inputs/outputs and table-driven scoring oracles in `docs/game-engine.md`.
2. Implement all mandatory baseball transitions from §6.2, including legal ball/strike/foul/count progression, non-PA plays, earned-run responsibility, legal roster/substitution constraints and two-way/DH rule cases. Counts are authoritative simulated state, never presentation decoration.
3. Finalize game/box-score facts once; build stat deltas with source play IDs and inning outs.
4. Add a scripted-game runner and diagnostic field/scoreboard displaying only committed state.

5. Attribute passed balls separately from wild pitches, steal/caught-stealing participants, catcher defensive outs and pitcher-catcher overlap. Preserve scorer evidence and earned/unearned reconstruction; workload records actual catching, not merely a C label. Read legality only from the resolved GameRules snapshot. Preserve enough state to resume inside a PA after an approved one-pitch action without replaying the resolved pitch.

### UI Work
QHD test-game selector, scoreboard, real count, bases/outs, scripted Next Outcome, and box score. No free manager controls in the career UI; test runner actions are diagnostic only.

### Persistence Work
Define the resumable game-state and scored-play contract; fixture serialization only. No migration yet.

### Events
`PlateAppearanceCompleted`, `RunnerPlayCompleted`, `SubstitutionMade`, `GameCompleted`; typed scoring facts precede narrative text.

### Tests
Exact expected outcomes for every §6.2 case; ball/strike/foul count boundaries and one actual-pitch `Take` continuation; zero/loaded-base combinations; invalid play rejected without partial mutation; game finalization repeated twice yields one result. Imported examples are not scoring oracles.

Exact WP vs PB/earned-run fixtures; steal/caught-stealing responsibility; catcher replacement mid-inning; unique shared exposure after replay/finalization.

### Acceptance Criteria
**Gate: G +** 100 scripted complete games reconcile runs/hits/errors/outs and batting/pitching totals; no negative outs or duplicate occupied bases; real count state and one-pitch continuation pass their fixtures; each supported rule variant has a named fixture; the D2 GameRules subset/simplifications are recorded. No stochastic claim is made.

All catcher scoring fixtures reconcile fielding/pitching/runner records without merging WP/PB; actual battery/catching exposure is emitted once.

### Deliverables
Reducer, scorer, scripted fixture suite, diagnostic scenes, game-engine contract and `PHASE_2_REPORT.md`.

### Risks
Earned runs, fielder's choices, walk-offs and two-way substitutions are easy to approximate incorrectly.

### Documentation Updates
`docs/game-engine.md`, C07/C08/C11, approved rule profile, current state and report.

### Claude Code Handoff
> Implement legal transitions and scoring before probabilities. Use exact expected fixture results. Do not hide invalid transitions by editing totals or silently substituting a different KBO rule.

## Phase 3 — Seeded game simulation / Prototype 1

### Goal
Prove two fictional clubs can play complete, repeatable, statistically inspectable games.

### Dependencies
Phase 2. Implement approved D1's default one-strategy-per-PA interaction and real internal pitch/count sequence using the approved D2 policy and specified GameRules subset.

### Scope
PA resolver, documented seeded RNG, probability inputs including bounded C46 park context, simple legal AI strategy/substitution policy, complete games and basic player records.

### Explicit Non-Goals
No ten-club pack, long career, training growth, life systems, Gemini, or final design.

### Architecture / Systems
`Simulation/Randomness`, `Simulation/Baseball`, `tools/StarPlayer.SimHarness`; C08 and shared determinism contract.

### Domain Models
`SimulationVersion`, `RngStreamState`, `MatchupSnapshot`, `StrategyChoice`, `ProbabilityTrace`, `GameStep`, `ParkFactorSnapshot`.

Add `CatcherContext` and pair-specific `BatteryContext` with neutral default and controlled nonneutral fixtures; no global catcher chemistry.

Pitch Zone Lite (§1.14): `ZoneModelVersion`, `PitchZoneId`, `PitchLocation` (zone, applied batter side, boundary class), `TargetZone`, `BatterZoneTendency` (nine bounded inner-zone values, neutral default), `CalledPitchMode`.

### Implementation Tasks
1. Implement fixed PRNG/key derivation and golden vectors; isolate gameplay from presentation draws.
2. Implement documented conditional outcomes and bounded modifiers from §4.1; every input has a traceable effect.
3. Feed results into the existing reducer; auto-resolve AI situations with the same code path. At PA start expose one documented batter/pitcher approach by default, resolve real pitches/counts internally, and emit only valid decision boundaries. An explicit `Take` resolves one real pitch and requests another decision only if the PA remains live.
4. Add the headless harness and calibration report against declared fixture targets, not arbitrary real-player statistics.

5. Implement §4.1 catcher influences and bounded chemistry modifiers. Framing applies only to supported marginal called pitches and is inactive under an automated-zone rule; never alter an already committed call. Blocking, Pop Time, Arm, C proficiency, pitcher runner control and runner context remain traceable inputs. Game Calling changes bounded effective execution/selection, not base pitcher ratings.
6. Resolve a game's C46 ParkFactorSnapshot once from its StadiumId/profile version, apply approved HR/hit/XBH effects at the documented probability branches, cap combined effects, and retain the snapshot in replay/final game facts. Never modify player ratings or recalculate a finalized game after profile changes.

**Internal work packages 3-A to 3-F.** The roadmap did not previously label sub-steps of Phase 3; these labels order the work inside Phase 3 only. They are not phases, add no dependency and leave the Phase 3 title, Goal, Gate and dependencies unchanged. Tasks 1–6 above are distributed among them; the Pitch Zone Lite (§1.14) additions are:
- **3-A (RNG).** PCG32 and the seed/stream architecture are unchanged. Add only the `game/location` stream name and the rule that its versioned, deterministic consumption order is recorded.
- **3-B (model and tuning contracts).** Add the 17-zone model, applied batter side, boundary class, batter zone tendency and call mode to the model inputs and tuning contract. Virtual baseline probabilities, cap values, seed sets and model versions are fixed before any calibration run.
- **3-C (pitch resolver flow).** Select the target zone, then draw the actual location, then resolve the pitch. Implement location on `PitchEvent`, `TakeEvent` and `InPlayEvent`, `ef1`/`ef2` coexistence and exact count replay.
- **3-D (probability links).** Connect actual location and tendency to contact and batted-ball probabilities. The pitcher AI only chooses a basic legal target zone; no observation-report weakness-exploiting policy.
- **3-E (call mode and framing).** Test ABS/non-automatic call modes, borderline framing and Game Calling within their limits, and check that park, existing catcher ability and battery effects are not double counted.
- **3-F (calibration and tests).** Keep the 100,000-PA and 100-complete-game Gates and add the Pitch Zone Lite tests below. Statistical and model changes are judged against pre-declared criteria and failing results are reported, not hidden.

### UI Work
Temporary QHD game, PA-approach choices, authoritative count, current mode, result, box score and individual summary. A compact field uses names/positions and visible runner evaluation without photos. Default play never prompts on every pitch.

### Persistence Work
Versioned tuning JSON, seed/command replay fixtures, game-state serialization contract. No career save promise yet. The contract carries pitch location, applied batter side, boundary class, target-zone strategy input, `ef1`/`ef2` receipts and the explicit event-format version; legacy fixtures and bare event arrays stay readable.

### Events
Existing scored-game events; `GameDecisionRequested`. Do not emit a “pitch event” for an invented display count. These scored-event and replay contracts are the future input for read-only Broadcast Commentary Lite (§1.12); **no dialogue generation or commentary UI belongs to Phase 3**, and presentation never consumes authoritative RNG or fabricates pitch events. The same events and replay contract are the only future data source for read-only Live Match Viewer Lite cues (§1.13): committed results alone can later yield presentation data. Live Match Viewer Lite itself requires no physics/coordinate model, no ball or player animation in Phase 3, and no change to the probability resolver, rules, scoring or official records; Pitch Zone Lite (§1.14) is a separate approved Phase 3 change that adds categorical pitch location and `ef2` events, and BCL/LMV may only read that confirmed location without consuming game RNG.

### Tests
100,000 seeded PAs; 100 complete games; monotonic ability/strategy influence fixtures; identical-seed canonical replay; all outcome probabilities finite and normalized; neutral-strategy watched vs instant results identical; default PA prompt count and one-pitch `Take` resume fixtures; displayed counts equal authoritative state.

Catcher modifier sensitivity/caps; high tools vs high C proficiency; WP/PB separation; caught-stealing multi-input effects; framing-disabled rule profile; neutral vs nonneutral battery replay; no direct Stuff/base-rating change. Neutral/pitcher-friendly/hitter-friendly park fixtures verify monotonic bounded influence, combined caps and identical historical replay after a later profile-version change. Pitch Zone Lite tests are listed under work package 3-F above and apply to this section.

### Acceptance Criteria
**Gate: G +** all games terminate legally; repeat runs match event and RNG hashes; aggregate rates fall within predeclared calibration intervals; no strategy dominates every relevant matchup in the declared test matrix; default interaction requests one PA approach rather than every-pitch input, while `Take` resolves exactly one pitch. Prototype 1 proves simulation, not final KBO balance.

Each catcher factor has a targeted effect fixture and combined-modifier cap test; no universal catcher/chemistry boost or new authoritative RNG stream from UI. Park effects remain traceable, bounded, versioned and separate from ratings; finalized game hashes do not change when current venue data changes.

Pitch Zone Lite adds, without removing or weakening any criterion above, these required checks: the same seed reproduces locations, events and RNG state; every actual pitch has a location; an in-play pitch is never counted twice; legacy `ef1` golden vectors are byte-identical; new `ef2` events are identified and collision-checked; legacy and new JSON formats both replay; a new-format pitch with a missing location is rejected; a neutral tendency vector has no effect; a non-neutral vector is monotonic and within its cap; ABS mode gives zero Framing effect; the legacy `GameRuleSnapshot` hash is unchanged; and gameplay RNG is not consumed by BCL/LMV. These do not complete Phase 3 or Phase 3-A by themselves.

### Deliverables
Resolver/RNG, playable test-game scenes, reproducible harness and calibration data, `PHASE_3_REPORT.md`.

### Risks
Double counting correlated skill modifiers; rendering-induced RNG draws; a hard safety timeout masking an invalid game.

### Documentation Updates
Game engine, `docs/testing/simulation-baseline.md`, RNG/model ADR, current state and report.

### Claude Code Handoff
> Implement Prototype 1 on the existing reducer. Persist replay inputs in fixtures, document model equations and approved cadence, and prove identical simulation under display-mode changes. Do not build career systems yet.

## Phase 4 — Datapack initialization and safe save spine

### Goal
Prove a career can be initialized, saved, resumed, migrated, and recovered without changing its world facts.

### Dependencies
Phase 3.

### Scope
Minimal pack validator/import including the approved D3 preseason-world boundary, optional completed draft facts and initial jersey assignments, first normalized save schema, transactional unit of work, game checkpoint, manual/multiple slots, autosave policy interface, Continue, migrations and recovery.

### Explicit Non-Goals
No full data editor, final real KBO pack, large career schema upfront, cloud saving, or automatic pack updates to saves.

### Architecture / Systems
Infrastructure Datapacks/Saves/Sqlite; Application Sessions/Ports; C01/C14/C33/C34/C47.

### Domain Models
`PackManifest`, `ValidationIssue`, `DraftClassSource`, `ImportedDraftFact`, `JerseyNumberAssignment`, `SaveHeader`, `SaveSlot`, `SnapshotGeneration`, `CommandReceipt`, `MigrationRecord`, `DayCursor`.

### Implementation Tasks
1. Validate fixture packs and initialize a new temp career transaction; freeze rule/content provenance. Personal-pack fixtures identify the preseason cutoff and reject post-start 2026 regular-season facts from the initial world.
2. Implement explicit row mapping and §5 commit/snapshot protocol, command/event receipts, RNG state and allocator persistence.
3. Implement multiple careers/manual slots, rolling autosave generations, Continue scan and explicit Load.
4. Add sequential copy-before-migrate support and a sample old-schema fixture; simulate disk/commit/publication failure points.
5. Materialize optional imported completed draft/player/entry facts and initial club-number assignments in the same all-or-nothing world-creation transaction. Validate IDs/references/picks/dates/origin/club/signing/registration/number conflicts and retired-number restrictions; source values are data, never a hardcoded year branch.

### UI Work
Main menu, pack picker/errors including draft/jersey field paths, save-slot cards, busy indicator, failure/recovery notice. Save a paused test game and restore its decision screen.

### Persistence Work
Schema v1 plus one exercised upgrade path; manifest/schema JSON. Persist imported draft source/completed/provenance and full jersey-number intervals where supplied, not only a current number. Tables remain limited to implemented state; catalog rebuild and snapshot generations follow §5. The persisted game contract (Phase 4 only designs and stores it; the resolver stays Phase 3) adds: each confirmed event's actual pitch location and applied batter side; the `ef1`/`ef2` fingerprint version per receipt; strategy inputs including the target zone; resolver, tuning and rule-profile versions; the location RNG stream state and consumption cursor; and unchanged legacy event and rule hashes for existing games. A legacy game is never recalculated under the location model.

### Events
`CareerCreated`; save lifecycle uses application notifications (`SaveCompleted`, `SaveFailed`), not gameplay events that alter world history.

### Tests
Round trip at pregame/midgame/postgame; fault injection before/after commit and publication; invalid IDs/references/dates/enums/ranges; duplicate draft pick/PlayerId, missing club/origin/player, inconsistent entry facts, initial jersey conflict/retired number and imported-vs-generated ID collision; missing optional draft history/asset fallback; latest-corrupt Continue; future-version rejection; old-save branch isolation; round trip of location, `ef1`/`ef2` receipts, target-zone input, version fields and location-stream cursor with resume reproducing the uninterrupted game; legacy games reload with unchanged hashes.

### Acceptance Criteria
**Gate: G +** restored state and next 20 resolved actions match uninterrupted execution; invalid packs create no published career; imported completed drafts and initial jersey assignments round-trip without rerun/duplication; every injected failure retains a loadable previous snapshot; two careers and three manual slots remain independent; modifying/deleting original pack does not change saved facts.

### Deliverables
Repositories, migration runner, validator, save/load scenes and fixtures, `PHASE_4_REPORT.md`.

### Risks
Memory/SQL divergence, copying live DB files, catalog becoming sole truth, duplicate command application after recovery.

### Documentation Updates
`docs/save-system.md`, data dictionary, C33/C34, save-format ADR, current state and report.

### Claude Code Handoff
> Implement the transactional save spine exactly as §5. Import optional completed draft and initial jersey facts atomically through C14/C34/C47 with no year hardcoding or partial world. Do not claim safe saves until fault-injection and midgame replay pass. Every added table has an explicit owner and migration path.

## Phase 5 — One-player career loop / Prototype 2

### Goal
Prove a player can understand and complete the home → choice → game → result → news → home loop, including saving and returning.

### Dependencies
Phase 4. Approved D4 makes Draft / Entry the normal production route for a newly created player. Until Phase 13, Prototype 2 may use only clearly labeled fictional post-entry fixtures. An imported already-completed rookie remains an Existing Player Start fact and is never rerun.

### Scope
New/existing player starts, including imported already-drafted rookie selection, fixed controlled identity, creation flow, descriptive biography/personality, recommended-budget display, training intent, condition/fatigue, own batting/pitching and instant modes, template events. Game interaction follows approved D1: one PA approach by default, real displayed count, and one-pitch actions only when explicitly selected.

Visible Pending Decisions, first-time contextual help/tooltips/glossary and a basic settings framework (C42) ship with the first career loop.

Add Broadcast Commentary Lite's (§1.12) minimal offline, template-driven caster/analyst side chat for the controlled player's playable games, with Crew A and Crew B having clearly different stable tone and dialogue packs independent of game mechanics. No career-wide broadcast schedule yet (Phase 6). Add Live Match Viewer Lite's LMV-A (§1.13): the 2D icon-based live field for the Game view, driven by read-only presentation cues from committed events.

### Explicit Non-Goals
No developed life economy, real growth curves, full draft, major league season, advanced news, or final visual theme.

### Architecture / Systems
Application career/game commands, public queries, UI shell/navigation; C01–C03/C08/C14/C29/C35/C47.

### Domain Models
`CareerStartSpec`, `ExistingPlayerQuery`, `ImportedDraftSummary`, `CreationBudgetGuide`, `ControlledPlayerContext`, `TrainingIntent`, `PendingDecision`, `PublicGameView`.

Add `PendingDecisionView`, `HelpTopic`, `HelpSeenPreference`, `SettingsCapabilities`, `UserPreferences`; these project domain barriers or store presentation preferences, not new simulation facts.

### Implementation Tasks
1. Implement six-step creation and existing-player search; include DOB/body/origin/handedness, pitcher form/repertoire and approved D4. Free-text biography/characteristics are descriptive only; any mechanically meaningful starting trait must be a separately defined structured choice with visible, bounded, testable effects and an explicit owner. Existing search supports rookie-only, draft-year, club, position and round filters. Derive the hitter/pitcher archetype label from the allocated skill mix; it is not an extra ability bonus. Existing starts retain all imported facts except owner-editable personality, including stable PlayerId, completed draft/club/entry/contract/registration facts and initial C47 number; binding the controlled player creates no duplicate and performs no draft command.
2. Apply approved start placement; normal points are advisory, shared across two-way skills, while cheat has no over-budget warning and never reveals potential. Body size may provide only the bounded tendencies allowed by the source, never a large automatic skill bonus or one universally optimal physique.
3. Add minimal exposure/recovery state updates and training-direction selection; long-term growth is Phase 7.
4. Implement own-PA/own-pitching/instant presentation using one engine, bench progression, persisted decisions and small template news. The normal control requests one PA approach; the shown count comes from core state. If the player chooses `Take`, resolve only the next actual pitch and return to a legal decision point when the PA continues.

5. Add Korean `의사결정 대기함` with count, subject/type/due date/status/block reason and links to existing decision actions. Dismissal/navigation/reload cannot resolve or hide an outstanding decision.
6. Add contextual notices, hover/focus tooltips and reopenable glossary/help; persist do-not-show-again and reset controls. Introduce basic display/window/text scale, existing progression/text speeds, implemented default modes and confirmation/autosave/help preferences. Capabilities hide unavailable audio/Gemini/portrait controls; autosave settings cannot disable mandatory recovery boundaries or lower the three-generation minimum.
7. Implement a read-only, event-driven `CommentaryContext` projection and bounded `CommentaryLine` selector over existing scored-play events (single engine, never a reducer/state-machine change for narration). Build separate versioned, data-driven Korean Crew A/B packs that prevent unsupported factual claims, assign a crew per stable Phase 5 test-game identity by the documented A/B rotation and preserve it on resume.
8. Implement LMV-A (§1.13): a 2D field coordinate system and icon display of the nine defenders, batter and runners; a read-only Presentation Adapter producing versioned `PlayPresentationCue`s from committed events; ball movement after contact; defensive and baserunning movement only where existing events support it; out/safe and run visual effects; skip and speed controls; per-event playback deduplication; and a safe static fallback when information is insufficient. Coordinates are presentation-only and never used for rules or fielding success. No SD characters (LMV-B) and no change to the reducer or scoring.

### UI Work
Home, My Player, Training, Game, Result, basic News/Event, menu shell and minimal overlay player card. Build compact QHD/FHD variants now; preserve active game when inspecting a name. Label Prototype 2's new-player scenario as a fictional post-entry fixture rather than the final direct-club career start.

Game view: a collapsible broadcaster chat beside the game view in QHD and a compact tab/drawer in FHD. Distinguish caster vs analyst by name and role, show brief dialogue bursts, offer off/highlights/fuller verbosity as event granularity allows, and never cover essential PA/count/score/runner/decision controls. Missing commentary content yields no message, never a fabricated play or a blocked game.

LMV-A layout: at QHD the LMV-A field stays central with the BCL chat panel shown beside it; at FHD the field keeps its readability and the chat becomes a tab or collapsible panel. Neither the field nor the chat may cover the PA-approach choice, real count, score, outs, runners or required action buttons, and skip/speed controls stay reachable.

Use Korean labels including 홈, 내 선수, 경기, 훈련, 기존 선수로 시작, 신인만 보기, 드래프트 연도, 지명 순번, 출신 학교, 의사결정 대기함 and 설정. Imported rookie results are read-only historical facts. Inbox is a compact decision list, not an email simulator; contextual help is skippable and reopenable, not a forced tutorial campaign.

### Persistence Work
Add creation settings, controlled PlayerId binding, personality, plans, pending choices and navigation preferences. Preserve imported draft/entry/jersey references rather than copying player rows. Autosave postgame and pre/post major career decisions; migration fixture required.

Version preferences separately outside authoritative simulation; Inbox reads persisted decisions and never stores a second decision truth. Store the commentary verbosity preference with those preferences and the per-game crew ID + dialogue-pack version as presentation metadata against the stable test-game identity (migration fixture); neither is a core game fact.

### Events
`CareerStarted`, `TrainingPlanChanged`, `PlayerStateChanged`, `DecisionResolved`; existing game events.

### Tests
Both start modes; imported rookie appears under filters; selecting one preserves PlayerId/draft/club/entry/contract/registration/initial number and creates no duplicate; career starts after the completed draft while future simulation may diverge; new-player prototype is explicitly post-entry and cannot establish direct-team start as production policy; free text cannot create uncontrolled modifiers; hitter/pitcher/two-way/bench flows; one PA approach by default plus one-pitch `Take`; displayed count equals core state; budget above recommendation allowed; no potential in cheat; repeated choice rejected; save-resume at a decision; offline template completion.

Pending decision survives close/reload/date-advance attempt; help suppress/reset/reopen; preference migration/corruption fallback leaves core save intact; scaling/FHD focus; no unavailable setting and no weakened autosave floor.

Commentary: Crew A/B alternate over five consecutive controlled-player test games and stay fixed within each game and across reload; normal plays, scoring plays, home runs, strikeouts and game-ending situations yield only context-supported lines; disabling/reopening, every verbosity level, watched/instant modes and viewport differences leave authoritative RNG/event/score hashes unchanged and draw nothing from gameplay RNG streams; event replay is idempotent with stable message order; hidden NPC fields are absent; no network/API is required; all controls remain reachable at QHD/FHD.

LMV-A: the same event yields the same cue and playback; chat display and LMV playback are independent of each other; normal speed, fast speed and skip give identical results; animation on/off leaves gameplay RNG and event hashes identical; BCL and LMV presentation keys live in distinct namespaces (changing one feature's output or settings never changes the other's, and neither consumes gameplay RNG); save/load and screen re-entry never re-apply an event or replay a duplicate cue; a composite play with insufficient information falls back safely without asserting unrecorded detail; required UI stays accessible at QHD/FHD.

### Acceptance Criteria
**Gate: G +** five consecutive games and an off-day complete for each role at both resolutions; switching mode changes no outcomes with identical actions; default play does not require every-pitch input; every shown count is real; `Take` advances exactly one pitch; an observer identifies inning/score/outs/runners in <=3 seconds for 9 of 10 prepared situations; next action is reachable without losing game state. Log usability misses as failed criteria to correct.

All live decision barriers are reachable via Inbox; settings/help work at both resolutions and make zero gameplay RNG draws. An imported rookie start binds the existing Player exactly once and never reruns or changes the historical draft.

Broadcast chat passes the commentary tests above with exactly one stable crew per game, no duplicated or reordered messages and no effect on game outcomes or hashes. Phase 5 passes when LMV-A works and meets its tests above; it never requires LMV-B.

### Deliverables
Playable Prototype 2, creation/existing selection, shell/scenes, paired screenshots and workflow evidence, minimal two-pack Korean dialogue dataset with read-only presenter/contract tests, the LMV-A field with presentation-cue tests, `PHASE_5_REPORT.md`.

### Risks
Prototype shortcuts becoming permanent roster rules; UI inventing growth or automatically choosing important actions.

### Documentation Updates
UI routes and state contract, creation policy/config, new `docs/commentary-system.md` (factual boundaries, pack versions, settings, future Phase 6 fixture integration), new `docs/live-match-viewer.md` (presentation-cue contract, factual boundary, LMV-A scope, LMV-B extension boundary), current state and report.

### Claude Code Handoff
> Deliver the one-player loop at QHD and FHD. Reuse Existing Player Start for imported already-drafted rookies with filters and stable identity; do not copy the player or rerun the draft. Keep missing later systems explicitly unavailable, and keep early training intent distinct from implemented growth. Do not hard-cap normal creation or modify imported existing-player facts. Keep the broadcast chat read-only, offline and outside authoritative RNG/hashes. Build LMV-A as a presentation-only icon field over committed events; do not build LMV-B or alter engine results, and keep LMV-A/B distinct from BCL Crew A/B.

## Phase 6 — Calendar, regular season, and basic discovery

### Goal
Prove a repeatable sequence of games/rest days progresses all participating clubs and provides trustworthy current-season information.

### Dependencies
Phase 5; approved D2 policy with the regular-season/calendar RuleProfile subset specified before implementation.

### Scope
World clock/stage cursor, schedule import/generation, fixture StadiumId, lightweight weather/interruption context, date advance barriers, current standings, minimal Player DB/common profiles and Team Profile.

Integrate KBO Rule Engine RegularSeasonRules and complete early Inbox/help/settings routing with the resumable calendar.

Bind the two Broadcast Commentary Lite crews (§1.12) to the canonical fixture schedule: deterministic A/B rotation across the controlled player's consecutive viewed/played fixtures; off-days never advance it; a rescheduled fixture keeps its original crew.

### Explicit Non-Goals
No draft, postseason results, long-world stability claim, full historical profile tabs, or life calendar simulation.

### Architecture / Systems
Application date coordinator; Simulation World; schedule/season/query systems C09–C11/C35/C36.

### Domain Models
`WorldDate`, `Fixture`, `FixtureStatus`, `WeatherState`, `PostponementReason`, `RescheduledFixtureLink`, `SeasonStage`, `AdvanceRequest`, `AdvanceStopReason`, `StandingsRow`, `ProfileRouteState`.

### Implementation Tasks
1. Implement stable fixture order and persisted stage cursor; enforce no duplicate participation and valid home/away totals.
2. Advance through all games, stop at player decisions, resume after cancellation/reload without duplicate results.
3. Project standings and separated season/career summaries from scored facts.
4. Add minimal database search/filter/profile and team current-roster/rank screens; preserve filter/sort/scroll/season state.

5. Show pending deadlines/advance-stop reasons in Home, Schedule and Inbox from one C42 projection. Add each later contract/FA/injury/national/military/interview type as its owner ships. Distinguish required choices from informational notices.
6. Resolve schedule/standings rules through versioned RegularSeasonRules. Add rule-aware glossary links and capability-driven progression/default-mode settings; unseen future systems expose no fake controls.
7. Assign each fixture a C46 StadiumId and resolve minimal Clear/Rain/HeavyRain-equivalent weather through an isolated deterministic stream. RegularSeasonRules alone decides delay/postponement/cancellation and creates a traceable reschedule relation; no major weather ability modifier or unrelated RNG draw.
8. Attach a versioned `BroadcastCrewAssignment` presentation reference to the stable fixture/game ID using committed fixture ordering (not wall-clock or gameplay PRNG); repeat date-advance and save/reload are idempotent, postponed/cancelled fixtures never double-allocate a crew, and loading never swaps the assigned duo. Phase 5 prototype assignments on already-played games are migrated unchanged, never reassigned.

### UI Work
Schedule, Team, League standings, Player DB and common Player/Team Profiles, status-bar next activity. Show Korean venue/weather/status labels such as `우천 취소`, `경기 연기`, `보충 경기`; missing future tabs show truthful empty/unavailable states.

### Persistence Work
Competition/season/fixture/stage tables and indexed queries; fixture rows retain StadiumId, weather/status/reason and original↔rescheduled linkage. Persist crew ID and dialogue-pack version in game/fixture presentation metadata (migration as needed; extends the Phase 5 per-game assignment). Preserve played fixtures when loading; migration from Prototype 2 starts a declared new fixture schedule without rewriting past games.

### Events
`DateAdvanced`, `FixtureScheduled`, `FixturePostponed`, `FixtureRescheduled`, `SeasonStageChanged`; existing game events feed standings/history.

### Tests
30-day fixture with off-days, competing same-day tasks, cancelled advance, stale query and load replacement; schedule constraints and standings recomputation; name duplicates/search escaping; UI back-stack restore.

Inbox count/list/calendar parity, explicit expiry notice and no silent disappearance during advance; reopening help never repeats the gameplay event; settings changes do not alter rules or saved game facts. Same weather seed/profile reproduces fixture status; reload/reschedule creates neither lost nor duplicate games; weather-stream draws leave game/injury/draft hashes unchanged.

Broadcast crews: full-schedule rotation, postponement/reschedule/reload (crew fixed after start, no slot consumed by a pre-start postponement), two simultaneous games, skipped-game modes and offline re-display of older commentary; identical core results/hashes with chat on/off and with either crew.

### Acceptance Criteria
**Gate: G +** ten seeds of the 30-day fixture finish with all scheduled games accounted for; loaded continuation matches uninterrupted facts; required decisions stop advancement; minimal profiles are reachable from standings, roster, games and records at both resolutions.

Every advance barrier reports the same persisted decision in Inbox; help/settings remain usable while a required choice is pending; rule snapshots survive load unchanged. Every fixture has a valid StadiumId and weather interruption either leaves it scheduled or produces one rule-valid linked replacement. Every controlled-player game has exactly one stable crew assignment following the documented A/B rotation, with no duplicated messages, altered outcomes or invented records.

### Deliverables
Calendar/coordinator/read models, five screens, schedule validation suite, `PHASE_6_REPORT.md`.

### Risks
Date advancing ahead of games, duplicate daily work after reload, loading an entire database for each profile.

### Documentation Updates
`docs/league-system.md`, UI architecture, schedule contract and current state/report.

### Claude Code Handoff
> Implement deterministic regular-season progression and the minimal discovery screens required by Prototype 3. Do not fabricate postseason, awards, or future records. Treat date advancement as resumable committed stages.

## Phase 7 — Development, aging, training, and role proficiency

### Goal
Prove training and exposure create gradual, varied careers while short-term form remains separate from base ability.

### Dependencies
Phase 6.

### Scope
Growth/aging profiles, training focus/intensity, pitch learning, position and hitter/pitcher conversion, two-way allocation/workload and coach estimates.

Catcher skill development, separate C proficiency and pair-specific BatteryChemistry progression (C41) use real saved exposure.

### Explicit Non-Goals
No guaranteed potential fulfillment, arbitrary two-way cap, automatic promotion, injuries yet, or money-based training purchases.

### Architecture / Systems
Simulation Development; Domain Players; C02–C05. Scouting projection exposes uncertain coach observations, not hidden limits.

### Domain Models
`GrowthProfile`, `TrainingPlan`, `TrainingAllocation`, `Exposure`, `AbilityChangeReason`, `ProficiencyTransition`, `PitchLearningProgress`.

### Implementation Tasks
1. Define versioned growth/aging curves and bounded increments with fixture targets for early/normal/late/bust/breakout/decline outcomes.
2. Apply scheduled training and actual game exposure once per interval; use independent RNG streams.
3. Add role/position changes, related-skill transfer and new-pitch learning; preserve known proficiency on return.
4. Combine both-role workloads and divide one training time budget; update coach reports without exact future predictions.

5. Train Receiving/Framing, Blocking, Pop Time and Game Calling independently from C proficiency using finite time/exposure. Returning to C retains prior proficiency under the existing policy.
6. Update BatteryChemistry once per actual shared game/camp/bullpen exposure; use bounded high-pressure/repeat-season effects and optional slow inactivity decay. Save pair metadata/receipts; initialize absent history neutrally. Personal-familiarity growth input stays neutral until Phase 21 supplies a real represented relationship.

### UI Work
Training focus/intensity and predicted fatigue/risk categories, position/proficiency tabs, both role summaries and growth history.

### Persistence Work
Hidden profiles, ability/proficiency changes, learned pitches and exposure checkpoints; migration initializes missing profiles with versioned deterministic seeds, never from public estimate as if it were truth.

Add catcher skill growth provenance and `battery_chemistry`/exposure receipts with unique pair keys, counters/dates/tuning version; upgrades invent no prior shared appearances.

### Events
`PlayerDeveloped`, `PlayerAged`, `PlayerChangedPosition`, `PlayerRoleChanged`, `PitchLearned`, `TrainingPlanChanged`.

### Tests
Fixed-seed cohort curves; same history reproducible; high-potential failure possible; small sample hot streak does not directly boost base ratings; two-way time sums <=100%; returning position preserved; extreme allowed ratings remain bounded.

C→other→C retention; distinct skill/proficiency growth; shared game and represented practice update once; three pitchers have different chemistry with one catcher; bounded slow decay; reload and exposure ordering parity.

### Acceptance Criteria
**Gate: G +** 1,000 synthetic careers include every configured growth class; no single-game rating jump outside the documented update policy; 1,000 extra UI queries do not change growth hashes; role changes and learned pitches survive save/load.

Pair progression is deterministic and distinct from base-rating deltas; no friendship requirement or permanent chemistry skill boost; catcher practice respects the shared time budget.

### Deliverables
Development/proficiency services, tuning fixtures, training/My Player extensions, cohort report and `PHASE_7_REPORT.md`.

### Risks
Potential becoming a rigid promised ceiling; accidental resetting of known skills; double workload application.

### Documentation Updates
`docs/player-system.md`, development tuning specification, C02–C05 and current state/report.

### Claude Code Handoff
> Implement gradual development and flexible role changes. Keep true potential internal and test non-guaranteed outcomes. Do not turn performance rankings into direct rating updates or add permanent role restrictions.

## Phase 8 — Injury, recovery, and rehabilitation eligibility

### Goal
Prove injury decisions affect availability and recovery without ordinary injuries destroying base skill.

### Dependencies
Phase 7. Actual second-team rehabilitation games are completed in Phase 10.

### Scope
Body/side/type/severity model, workload-based risk, minor play/rest choices, recovery/reinjury, rare serious long-term effects and rehabilitation eligibility.

### Explicit Non-Goals
No medical simulator, guaranteed permanent penalties, or simulated second-team appearances before Phase 10.

### Architecture / Systems
Injury service C12; PlayerState and Development consume typed temporary/permanent effects; Roster later consumes availability.

### Domain Models
`InjuryEpisode`, `RecoveryStage`, `MedicalAvailability`, `PlayThroughDecision`, `ReinjuryLink`, `RehabPlan`.

### Implementation Tasks
1. Add risk weights for pitcher, fielder, two-way workload and prior same-part episodes.
2. Implement legal play/rest decisions, recovery updates and relapse attribution with independent RNG.
3. Route ordinary effects to transient/effective performance; rare serious changes explicitly enter development history.
4. Expose rehab-ready status and the contract for future second-team rehab games.

5. Weight actual catcher innings/starts/receiving exposure and consecutive-start/rest history in workload, recovery and injury risk. Catching generally costs more physical load than ordinary fielding under declared tuning; fatigue temporarily reduces effective performance without permanent base-rating loss merely for repeated starts.

### UI Work
Priority injury popup, episode details, recovery stages, active injury on Home, injury-history profile tab.

First injury context explains care/recovery choices through C42 help; workload shows Korean catcher/rest cues without leaking NPC true ratings.

### Persistence Work
Episode/stage/history tables, persisted pending choices and recovery cursors; closed injuries remain queryable.

### Events
`PlayerInjured`, `InjuryWorsened`, `RehabilitationStarted`, `PlayerClearedForRehab`, `PlayerReturnedFromInjury`.

### Tests
All severity transitions, stacked injuries, repeated same-part episodes, unavailable player excluded, minor injury base ratings unchanged, serious-injury effect recorded once, restore before a play/rest choice.

Equal-duration catcher vs ordinary-field workload; repeated starts vs rest, temporary fatigue/recovery with unchanged base ratings, shared two-way/catching load once, save/reload risk parity.

### Acceptance Criteria
**Gate: G +** each severity has a completed fixture; rest vs play-through changes configured risk in a seeded cohort; no repeated permanent penalty after reload; “cleared for rehab” cannot be displayed as “returned to first team.”

Catcher load affects fatigue/risk/effective performance through existing state/injury owners; repeated starts alone never cause permanent base changes.

### Deliverables
Injury model/services/UI, fault and recovery fixtures, `PHASE_8_REPORT.md`.

### Risks
Clearing one injury incorrectly clearing all availability restrictions; UI implying medical certainty from an estimated recovery range.

### Documentation Updates
`docs/injury-system.md`, C12, UI injury states, current state and report.

### Claude Code Handoff
> Implement injury episodes and recovery state transitions, preserving history. Ordinary injury changes state first. Leave actual rehab-game execution to Phase 10 through the defined eligibility contract.

## Phase 9 — Club AI, trust, and legal selection

### Goal
Prove autonomous teams select legal lineups and manage workload while the controlled player remains a player rather than a manager.

### Dependencies
Phase 8.

### Scope
Lineup/rotation/bullpen policies, role evaluation, managerial trust, playing opportunity, position competition, active-roster jersey assignment and promotion/demotion proposals; manager/coach identity, active assignments and lightweight lifecycle hooks for later season-boundary turnover.

Persistent ManagerProfile and ClubPhilosophy, pair-aware catcher selection, C47 number availability, bounded rule-based policy differences and lightweight future financial-preference inputs; C06/C07/C40/C41/C47. Phase 9 defines only C48-facing preferences; Phase 14 owns finance calculations and state.

### Explicit Non-Goals
No playable team operations, FA/trade AI, friend-based guaranteed selection, or automatic call-up based on one stat threshold.

### Architecture / Systems
Simulation ClubAI, Roster invariants C06/C07; staff identity/assignment and policy activation remain C06; observations/public reasons are separate from internal scores.

### Domain Models
`ClubPolicy`, `StaffId`, `StaffRole`, `StaffAssignment`, `StaffTenure`, `SelectionContext`, `RosterProposal`, `ManagerEvaluation`, `TeamRole`, `RotationPlan`, `SelectionReason`, `JerseyNumberAvailability`, `CaptainCandidateContext`, lightweight `TeamAtmosphere`, future `ClubFinancialPreference`, and scheduled-pitcher `BatteryChemistry` context from C41.

Add `ManagerProfile` (veteran/prospect preference, offensive aggression, defensive/catcher-defense preference, starter leash, bullpen aggression, trust sensitivity, development patience) and `ClubPhilosophy` (development/win-now focus, FA/trade aggressiveness, prospect protection, roster stability, bounded spending aggressiveness, star retention, cap-limit willingness and long-term financial caution). Save values and tuning versions independently of friendship/trust. These are preferences, not a second financial engine or budget ledger.

### Implementation Tasks
1. Implement availability filtering, positional coverage and eligibility before candidate ranking.
2. Rank using ability, proficiency, recent form, health, workload, trust, team need and development policy; deterministic tie-break by stable ID or named RNG stream.
3. Select legal lineups/rotation/bullpen and issue assignment proposals; retain final domain validation.
4. Publish human-readable role/competition reasons without exposing hidden evaluation numbers.

5. Persist staff-bound manager profiles and club-bound philosophy; use bounded documented scoring weights after eligibility filtering. Manager replacement changes active policy without rewriting prior decisions. FA/trade dimensions become active only in Phases 15–16.
6. Select catchers from defensive tools, batting, C proficiency, health/condition/fatigue/workload, chemistry with the scheduled pitcher, manager profile, club philosophy and opponent/context. Bound chemistry against health/legality and overall utility. Ace-specific catchers, veteran-rookie pairings and long-running batteries emerge from scores; do not make a personal-catcher flag the primary mechanic.
7. Create stable manager/coach identities, role assignments and effective-dated tenure records. Define deterministic appointment/continuation/departure/replacement hooks for Phase 11 without a staff hiring UI or league-wide detailed staff career simulator.
8. Define bounded captain-candidate inputs (experience, club tenure, role, reputation, leadership tendency, relationships, manager evaluation, loyalty and availability) and a team-atmosphere summary that Club AI may read only where documented. Neither becomes a large direct ratings modifier.
9. Integrate C47 availability with roster activation. AI/NPC assignment chooses deterministically from legal club numbers; a controlled-player request is validated and may be rejected with legal alternatives. Assignment is atomic with activation and cannot use a club-retired or overlapping active number.
10. Define bounded ClubPhilosophy financial-preference inputs for Phase 14–16 decisions. Do not calculate payroll, allocate budgets, assess salary caps or settle sponsorship revenue in this phase.

### UI Work
Team roster with `현재 등번호`/`사용 중인 등번호`, position competition, expected role, rotation, current manager/coaching roles, captain label, team-atmosphere category, trust category and selection/demotion notices. No broad number-management, hire/fire or captain-selection controls.

### Persistence Work
Club policy, trust/role history, rotation/workload decisions; registrations and C47 jersey assignments remain dated and independently validated.

Store staff identities/roles, `manager_profiles`, `club_philosophies`, dated staff assignments/tenures, captain assignment and bounded atmosphere state only if needed for authoritative reload; keep policy versions and selection evidence. Persist only the declared finance-preference weights here; C48 later owns budgets, sponsorship and cap assessments. Read pair chemistry from C41, never duplicate it onto the catcher.

### Events
`TeamRoleChanged`, `ManagerEvaluationChanged`, `StaffAssigned`, `LineupSelected`, `RosterMoveProposed`; Phase 11 owns actual season-boundary staff departure/replacement and captain designation events.

### Tests
Injured/ineligible candidates, all-catcher/all-pitcher shortages, tired bullpen, two-way conflicts, same skill with different proficiency, high second-team performance with blocked first-team position.

Different profiles produce bounded persistent differences on curated identical contexts; same state/seed/policy replays identically. Financial preferences stay within declared bounds and create no payroll/budget/sponsor state before Phase 14. Staff identity and active profile are not conflated; a replacement fixture starts a new evaluation context while preserving prior facts. Captain scoring is multi-factor and rejects unavailable candidates. TeamAtmosphere inputs/effects remain bounded. Imported player retains a legal starting number; generated/rookie NPC fallback is deterministic; duplicate/retired requests reject without partial activation and show valid alternatives. Exercise ace-specific, veteran-rookie, established and newly traded pitcher pairs plus exhausted/unavailable preferred catcher. Friendship never guarantees chemistry or selection.

### Acceptance Criteria
**Gate: G +** 1,000 seeded selection contexts yield a legal selection or a typed actionable shortage, never an illegal lineup; private friendship cannot overwrite trust; identical context yields the same decision/reasons; no user can submit another player's lineup command.

Policy differences remain within legal roster/workload constraints; paired selection scenarios emerge without hardcoded personal-catcher assignment; save/load preserves policy and chemistry evidence. Financial preferences are bounded inputs only and grant no user management command or early finance implementation. Active club jersey numbers are legal/unique and C47 remains separate from Player identity. Staff/profile/club philosophy/captain/atmosphere are separate records, and no user management command exists.

### Deliverables
Club policies, roster evaluator, team screen, scenario scorecards and `PHASE_9_REPORT.md`.

### Risks
Hidden AI score leaks, permanently overworked pitchers, and fallback logic that conjures players instead of reporting a supply defect.

Catcher fatigue/chemistry feedback can monopolize starts; cap influence and test rest/development alternatives and sparse pair growth. Staff turnover or atmosphere can become an opaque global buff; keep effects explainable, bounded and player-centered.

### Documentation Updates
`docs/roster-system.md`, club AI decision specification, C06/C07 and current state/report.

### Claude Code Handoff
> Extend rule-based Club AI with persistent manager/club profiles, stable staff identity/assignment hooks, bounded captain/atmosphere context, C41 pair context and deterministic legal C47 number assignment. Define only bounded financial preferences for later C48 use; do not implement payroll, salary-cap or sponsorship state yet. Keep all scoring bounded and seeded; no Gemini lineup or team-operation decisions and no staff/number-management UI.

## Phase 10 — Ten-club world and real second-team play

### Goal
Prove first and second teams run actual games with shared players, separate records, and coherent call-up, demotion, and rehabilitation.

### Dependencies
Phase 9; approved D2 policy with the detailed registration/second-team RuleProfile subset specified before implementation.

### Scope
Ten fictional KBO-style clubs, affiliated first/second squads, schedules, AI usage, developmental registration, club-wide jersey-number continuity, roster moves and rehab games.

### Explicit Non-Goals
No foreign professional leagues, draft generation, military squad yet, or final ten-club real-data pack.

### Architecture / Systems
Competition/squad model plus C07/C17/C47. Use the same game, development and injury services for both levels.

Use C40 KBO Rule Engine RosterRules/RegistrationRules; preserve the career-frozen versions, and test a differing rule-version fixture rather than embedding 2026 values in this subsystem.

### Domain Models
`SquadEntry`, `AssignmentInterval`, `CallUpDecision`, `DemotionDecision`, `RehabAppearance`, `RegistrationConversion`.

### Implementation Tasks
1. Expand fictional pack with adequate legal depth and configured affiliation relationships, not a hardcoded assumption of identical competition sizes.
2. Run both competitions from one date coordinator with mutually compatible assignments.
3. Commit call-ups/demotions and registration changes atomically; enforce wait periods/quotas from the approved rule profile.
4. Execute rehab appearances and medical reassessment; prevent a good rehab line automatically triggering promotion.
5. Treat first/second-team movement inside one club as the same club-number assignment unless an approved club rule requires a change; reject cross-squad duplicate-number states where club rules prohibit them.

### UI Work
Level/status badge and current jersey number everywhere relevant; shared game/records screens; separate first/second stat tabs; roster filters for registered/developmental/injured; call-up/demotion/return notices.

First second-team assignment and developmental registration link to contextual explanations; help dismissal cannot change assignment or decision state.

### Persistence Work
Competition-level/squad indexes; assignment intervals, C47 club-number reference, rehab appearances and first-call-up/debut history; migrate prior first-team stats with explicit competition IDs without opening/duplicating a number interval.

### Events
`PlayerCalledUp`, `PlayerDemoted`, `PlayerRegistered`, `RehabAppearanceCompleted`, `PlayerMadeLeagueDebut`.

### Tests
60-day ten-club fixture with all level moves, club-number continuity/uniqueness, developmental first-team prohibition, two-way workload, injury return, concurrent fixtures and separate stat totals; one player cannot play incompatible same-time games.

### Acceptance Criteria
**Gate: G +** all scheduled games in both leagues occur or have an explicit permitted status; both controlled-player levels are playable; first/second records never mix; every roster move survives rollback/reload; home/profile/roster show matching registration and level.

### Deliverables
Ten-club fictional pack, dual-league simulation/UI, promotion/rehab tests, `PHASE_10_REPORT.md`.

### Risks
Double participation, developmental status mistaken for low potential, and first-team workload omitting second-team appearances.

### Documentation Updates
League/roster/injury docs, C17, pack schema and current state/report.

### Claude Code Handoff
> Make the second team a real competition using the shared engine. Preserve registration, assignment and health as distinct state. Demonstrate controlled-player games and rehabilitation at both levels.

## Phase 11 — Complete season lifecycle / Prototype 3

### Goal
Prove one complete season and its closure produce consistent standings, awards, team history and a resumable next-season boundary.

### Dependencies
Phase 10; approved D2 policy with season/tiebreak/postseason/award, All-Star and weather-scheduling subsets specified before implementation.

### Scope
Offseason, camp, exhibition, regular season halves, an observable All-Star voting/selection race, All-Star games, postseason/Korean Series, awards, weather makeup scheduling, lightweight staff transitions and rollover; season UX stabilization. Finalized standings/postseason/championship facts must be durable enough for later C48 sponsorship qualification.

### Explicit Non-Goals
No claim of sustainable multi-decade rosters before feeder/contracts, no fabricated future draft/FA results, or final visual theme.

### Architecture / Systems
C09/C10/C11/C27/C28; season stage handlers invoke existing systems, not a second simulator. Phase 11 publishes baseball achievement facts only and has no dependency on Phase 14/C48 financial services.

Use C40 KBO Rule Engine RegularSeasonRules/PostseasonRules/AwardRules; the configured All-Star selection procedure and weather/makeup/doubleheader legality live in typed rule data. Preserve career-frozen versions and test differing rule-version fixtures rather than embedding 2026 values in this subsystem.

### Domain Models
`PostseasonBracket`, `SeriesResult`, `AllStarVotingPeriod`, `AllStarVoteStanding`, `AllStarSelectionResult`, `AwardDefinition`, `AwardResult`, `StaffTransition`, `SeasonClosure`, `TeamSeasonRecord`.

### Implementation Tasks
1. Implement configured stage transitions and schedule rules, including standings ties, weather postponements, makeup games and doubleheaders only as permitted by the saved profile. Preserve original venue context unless the rule-valid reschedule explicitly changes it.
2. Run an explicit All-Star voting/race period and selection through the saved rule procedure. Represent only needed fan-vote, position, performance, popularity/fame and manager/league inputs; then play All-Star and postseason games with separate statistical scope.
3. Compute approved awards/titles and finalize team-season facts once.
4. Close the season transactionally and create next-season metadata; expose unimplemented offseason systems as pending phase scope, not fake outcomes.
5. Complete important-situations and entire-game presentation modes using the shared engine; mode changes alter pause/display policy only.
6. At configured offseason/season boundaries, apply bounded manager/coach appointment, continuation, dismissal, resignation/retirement, departure, role-change and replacement outcomes. Activate the new manager profile/evaluation context without deleting former staff relationships or tenure facts; avoid detailed staff labor/market simulation.
7. Commit finalized regular-season finish, postseason qualification and championship evidence once with stable IDs. Do not evaluate sponsor money, settle revenue or renew an agreement here; Phase 14 consumes these facts later.

### UI Work
Season calendar/stage, `올스타 투표` rank/position/trend/period/final result, All-Star and awards, bracket/series, standings, `우천 취소`/`경기 연기`/`보충 경기`, `감독 교체`/`새 감독 부임`, season/career summaries and basic historical team seasons. Home, League/All-Star and News link to the same voting facts. All five game modes are available and labeled. Core menu arrangement stabilizes.

### Persistence Work
Season/series/awards/team-season tables; required All-Star vote snapshots/final results, staff transition/tenure facts and original↔makeup fixture relations; durable championship, runner-up, regular finish and postseason participation facts with stable evidence IDs for later consumers; autosave stage boundaries. Do not retain every vote tick when a smaller reproducible history supports UI and audit. No sponsorship amount or financial settlement row is owned here.

### Events
`AllStarVotingOpened`, `AllStarVoteUpdated`, `AllStarSelected`, `AllStarVotingClosed`, `ManagerAppointed`, `ManagerDeparted`, `CoachAssigned`, `CoachDeparted`, `TeamClinchedPostseason`, `SeriesCompleted`, `TeamWonChampionship`, `PlayerWonAward`, `SeasonClosed`, `SeasonOpened`.

### Tests
Ten seeds of one full season; tiebreak and series fixtures; advance/load at each stage; second-team season closure; championship/award idempotency; team win/loss accounting; all five modes reproduce the same facts with identical strategies. Test stable finalized achievement evidence across repeated closure/reload without any Phase 14 service. Test All-Star rank/trend/final selection and reload idempotency under two rule profiles; rainout→makeup/doubleheader legality with no lost/duplicate game; venue preservation; manager continuation/departure/replacement with retained former relationship/tenure facts.

### Acceptance Criteria
**Gate: G +** each stage reachable at both resolutions; every fixture/vote/selection/award/staff/history row reconciles; imported six titles plus two synthetic completed title seasons displays eight in a targeted fixture; no hidden ratings exposed in award or All-Star competition screens. Final standings/postseason/championship facts are stable, uniquely identified and consumable later without a finance dependency. Weather makeup games remain rule-legal and exactly once; staff changes retain prior history. Prototype 3 passes only after Phases 6–11 all pass.

### Deliverables
Season lifecycle, bracket/award screens, full-season reports, `PHASE_11_REPORT.md`.

### Risks
Double counting postseason stats; duplicate All-Star selection after reload; makeup fixtures losing venue/history; staff turnover becoming a front-office simulator; offseason loops silently skipping unavailable market systems; sample-only awards becoming a final unapproved rule set.

### Documentation Updates
League/history contracts, season testing baseline, UI navigation freeze notes, current state/report.

### Claude Code Handoff
> Complete the season lifecycle and Prototype 3 UX, including rule-driven All-Star voting, weather makeup scheduling and lightweight staff turnover. Record completed seasons, selections, staff transitions, awards and sponsor-qualification achievement facts once. Do not implement or depend on finance settlement here; C48 consumes these facts in Phase 14.

## Phase 12 — Feeder cohorts and uncertain scouting

### Goal
Prove each future year can supply distinct prospects with persistent origins and imperfect public evaluations.

### Dependencies
Phase 11.

### Scope
High schools/universities/overseas backgrounds/other paths, fictional future cohorts for draft years without an imported completed class, school reputation and output history, observation-based scouting and prospect rankings.

### Explicit Non-Goals
No full amateur or foreign-league seasons, school management, actual draft picks, or guaranteed potential by school.

### Architecture / Systems
C15/C16; generated prospects use existing Player, growth, state and history models.

### Domain Models
`AmateurTeam`, `ProspectCohort`, `DraftClassSource`, `CohortGenerationMarker`, `EnrollmentHistory`, `ScoutingObservation`, `EvaluatorProfile`, `ProspectEstimate`, `DraftProjection`.

### Implementation Tasks
1. Import seed amateur/background organizations into the save and define versioned cohort distributions.
2. When C14/C34 reports no imported completed class for the reached draft year, generate IDs, background, age/body/role/current skills, hidden development profile and synthetic amateur summaries once with independent streams. Persist the generation marker and class immediately; reload never rerolls players or pre-draft facts.
3. Produce evaluator-specific uncertain grades/ranges; build public rankings from estimates/observations, not a sorted true-potential list.
4. Record school output and slowly update reputation with bounded per-year changes; do not rerun previous cohorts on load.

5. Include catcher skill/C proficiency diversity in generated cohorts; do not derive all four skills from one catcher scalar. Phase 24 adds cosmetic seeds through a separate deterministic appearance allocator without changing gameplay RNG.

### UI Work
League → Newcomers/Draft pool, origin filter, prospect version of common profile, basic school panel and linked origin chain.

### Persistence Work
Amateur organizations, cohort marker, observations, enrollment history and school-output facts; prospective initialization for older saves without rewriting known origins.

### Events
`AmateurPlayerGenerated`, `ScoutingReportUpdated`, `ProspectCohortPublished`, `SchoolReputationChanged`.

### Tests
20 generated cohorts across fixed seeds; an imported-class year suppresses generation; missing imported class invokes generation; duplicate imported/generated ID prevention; unknown-school elite and famous-school ordinary fixtures; evaluator disagreement and inaccurate ranges; save after cohort generation/reload/repeat command preserves identical IDs and pre-draft facts.

### Acceptance Criteria
**Gate: G +** every generated cohort player has a unique ID and valid origin; cohort replay hashes match; imported completed classes are not regenerated; missing classes generate and persist once; all approved origin paths occur in the declared sample; no school hard-locks potential; changing installed school data does not alter existing save output history.

### Deliverables
Feeder/scouting services, fictional amateur data, pool/profile UI and cohort diagnostics, `PHASE_12_REPORT.md`.

### Risks
Unbounded generation, demographics drift, high-school and university identities duplicated instead of preserving a player's life path.

### Documentation Updates
`docs/draft-system.md`, feeder distributions, C15/C16 and current state/report.

### Claude Code Handoff
> Generate fictional future prospects once only for draft years without imported completed class data, using stable Player identities and saved origin histories. Implement imperfect scouting. Do not reroll on load, build amateur leagues or equate school reputation with true potential.

## Phase 13 — Draft and professional entry paths

### Goal
Prove prospects enter actual club rosters, or retain coherent undrafted paths, without losing identity or origin; this is the normal production start route for a newly created player under D4.

### Dependencies
Phase 12; approved D2 draft/entry subset and approved D4 production career-start policy.

### Scope
Data-driven imported-completed versus generated-live draft distinction, eligibility/order/picks, AI prospect evaluation, controlled new-player Draft / Entry flow from a supported background/origin, fixed rookie entry terms and acceptance, rookie jersey assignment, undrafted university/developmental/other/end paths, conversion/release history. Direct club selection is not a normal new-player start.

### Explicit Non-Goals
No player-selected draft outcome, full contract negotiation engine, separate developmental-player database, or external league simulation.

### Architecture / Systems
C14–C17/C47; minimal contract activation owned by C13 is introduced here for entry; Phase 14 extends it to negotiation/payment. C34 supplies optional imported completed history; only generated live editions enter Club AI.

Use C40 KBO Rule Engine DraftRules and the fixed rookie ContractRules subset; preserve the career-frozen versions, and test a differing rule-version fixture rather than embedding 2026 values in this subsystem.

### Domain Models
`DraftClassSource`, `DraftCompletionState`, `DraftEligibility`, `DraftOrder`, `DraftPick`, `EntryOffer`, `EntryDecision`, `RookieJerseyChoice`, `RegistrationConversion`, `ExternalCareerState`.

### Implementation Tasks
1. Resolve each draft edition's saved data source. Imported completed editions validate/project immutable picks and entry facts and expose no `RunNextPick`; generated live editions compute order from approved saved rules/standings and validate no duplicate eligible players or picks. Never branch on a literal calendar year.
2. For generated live editions only, evaluate club need/observations/allowed internal factors and commit picks in stable order. Save class identities, source, seed/cursor, progress and final results once.
3. Accept fixed rule-defined rookie entry terms, preserve player authority where a signature is needed, and activate real affiliation/roster rows.
4. Resolve undrafted paths; university return preserves player ID and prevents duplicate cohort generation. Support developmental offers, later registration conversion and release.
5. Assign the entering rookie an available C47 club number atomically with affiliation/registration. Imported initial numbers remain historical starting facts; generated/NPC entries use deterministic legal fallback, while controlled-player requests may be rejected with alternatives.

### UI Work
Draft preview/results with imported/generated source and locked-history status, pending own entry choices, `입단 시 등번호`, original origin plus entry/status/level, conversion events and linked timeline. Prototype 2 new-player fixture placement is replaced by the approved full flow; imported rookies continue through Existing Player Start.

### Persistence Work
Draft source/completed/order/pick/generation marker/entry-contract/decision tables plus C47 assignment; unique draft/player/pick and imported/generated IDs; conversion/release dates; atomic roster+contract+number+history updates.

### Events
`PlayerEnteredDraft`, `PlayerDrafted`, `PlayerWentUndrafted`, `PlayerSignedAsDevelopmental`, `PlayerRegistered`, `PlayerEnteredUniversity`, `PlayerLeftBaseball`.

### Tests
Imported historical draft loads but cannot run/retry; source/round/pick/drafting club/PlayerId remain fixed. Generated full draft, depleted pool, pick retry/reload without reroll, undrafted branches, own-player decision stop, rookie number conflict/fallback, university re-entry, developmental conversion, high-potential undrafted fixture and failed-signing rollback.

### Acceptance Criteria
**Gate: G +** imported completed editions never rerun or change facts; generated editions persist once and resume without reroll; all drafted/signing players appear under the same PlayerId in the correct actual roster with one legal initial number; no player is duplicated/drafted twice; every undrafted player has one valid next status; all branch paths are exercised; source, origin and entry remain separately visible after save/load.

### Deliverables
Draft/entry services and screens, updated new-career flow, branch tests, `PHASE_13_REPORT.md`.

### Risks
Implicit guaranteed signing, annual duplication of university prospects, and conversion accidentally changing true potential.

### Documentation Updates
Draft/roster/contracts, approved starting flow, C14–C17, current state/report.

### Claude Code Handoff
> Implement data-driven imported-completed versus generated-live drafts. Never rerun an imported edition or hardcode 2026; persist generated classes/results once. Activate generated rookie contract/roster/C47 number atomically, preserve PlayerId and all undrafted outcomes, and leave general negotiation to Phase 14.

## Phase 14 — Contracts, agents, and earnings ledger

### Goal
Prove player-approved domestic contracts create enforceable terms and exactly-once career income while autonomous clubs maintain coherent payroll, salary-cap assessment, player budgets and idempotent sponsorship revenue.

### Dependencies
Phase 13; approved D2 policy with the contract/service-credit/incentive, annual-renewal and SalaryCapRules subset specified before implementation. Actual cap editions, included components, exemptions, dates and consequences require reliable evidence or an explicitly documented game simplification; do not invent them.

### Scope
Salary/length/signing bonus/options, offers/counteroffers/expiry, explicit annual domestic non-FA salary renewal/negotiation, agent types/fees/advice, service accrual, salary history and basic career earnings ledger. Add C48 Club Finance Lite core: contract-derived club payroll/projections, separate annual player budget, versioned salary-cap assessment/consequences, abstract sponsorship agreements/base and earned bonuses, bounded annual renewal/allocation and retained financial history. Annual Salary Negotiation and the Phase 15 FA Market remain separate career processes.

### Explicit Non-Goals
No FA competition yet, purchases/lifestyle store, C38 individual commercial contracts, playable GM/owner controls, detailed accounting, ticket/stadium/broadcast/staff-payroll/debt/bankruptcy systems, real sponsor-company simulation, guessed KBO legal/financial procedure or fabricated official club amounts, or agent deciding for the controlled player.

### Architecture / Systems
C13/C25/C37/C42/C48; C13 remains the sole authority for playing-contract terms and obligations while C48 derives payroll and owns club financial policy, budgets, sponsorship and saved cap assessments. C25 credits the player from the same unique C13 obligation and is not club accounting. The KBO Rule Engine distinguishes renewal from FA and owns SalaryCapRules. Pending Decisions exposes unresolved player salary choices.

Use C40 KBO Rule Engine ContractRules for renewal eligibility, permitted rounds/deadlines and resulting obligation structure, plus SalaryCapRules for included players/components, exemptions, assessment dates, threshold and rule-defined excess behavior. Preserve career-frozen versions and test differing rule-version fixtures rather than embedding or guessing 2026 legal details. A cap excess is rejected only when the saved rule says it is prohibited; otherwise apply the configured penalty/consequence.

### Domain Models
`ContractTerms`, `ContractOffer`, `NegotiationRound`, `AnnualSalaryNegotiation`, `SalaryProposal`, `SalaryDecision`, `SalaryHistoryEntry`, `Incentive`, `ServiceCredit`, `AgentAgreement`, `PaymentObligation`, `LedgerEntry`, `ClubFinancePolicy`, `ClubPayrollProjection`, `ClubFinancialSummary`, `SalaryCapAssessment`, `ClubSponsorshipAgreement`, `SponsorshipSettlement`.

### Implementation Tasks
1. Extend entry contracts with explicit effective/expiry dates, guarantees/options/no-trade clause and documented currency rounding.
2. Implement bounded negotiation rounds, expiry and final user acceptance; agents propose comparisons and advice.
3. Accrue service under rule data and schedule salary/bonus/incentive/commission payments with unique obligation IDs.
4. Expose current terms and earnings summaries; correctly migrate existing entry/imported contracts.
5. At the rule-defined post-season renewal window, create one annual non-FA negotiation for each eligible player. Include club initial proposal, prior salary comparison, performance/role/service-time context, agent advice, bounded counteroffer/rounds and deadline where the saved profile requires it.
6. Require the controlled player's explicit final accept/counter/reject decision when the rule profile gives that authority; the agent may advise or negotiate only within authorization and cannot silently finalize. Resolve NPC renewals deterministically through the same legal rules and save-safe receipts.
7. Activate the next-season salary exactly once and append salary history without creating an overlapping playing contract or duplicate payment obligation. Keep FA declarations/offers outside this state machine.
8. Derive annual contracted payroll, cap-chargeable payroll, future commitments and proposed-contract impact directly from C13 obligations. Keep those values distinct from the C48 annual player budget; never duplicate contract rows or count one obligation twice because C25 and C48 both reference it.
9. Resolve SalaryCapRules through C40 and save historically consequential assessments with the rule/profile version. Evaluate the legal cap and internal budget independently: sponsorship/resources cannot raise the cap threshold, and cap room cannot create budget.
10. Load or create abstract sponsorship terms, then settle annual base revenue and performance/postseason/championship bonuses only from finalized C10 evidence. Use unique agreement/condition/evidence receipts so reload, repeated closure, event replay or read-model rebuild cannot award revenue twice.
11. Renew sponsorship deterministically between seasons using recent and multi-season performance, championships/postseason appearances, club popularity/reputation, prior sponsorship level, bounded yearly changes, multi-season smoothing, baseline support and declared minimum/maximum policy limits. Allocate only the saved bounded policy share to future player budget, preserving recovery paths and preventing permanent wealth snowballs.
12. Apply budget/cap/projection checks to annual renewal proposals without reducing already binding obligations. Commit contract, obligation, financial assessment and settlement-related changes transactionally; rejection or arithmetic/rule failure mutates none.

### UI Work
Contract and offer comparison, `연봉 협상` workspace with club proposal/previous salary/context/agent advice/counteroffer/deadline/final confirmation, agent selection/type/fees, service-to-FA status and basic income/salary-history tab. Add read-only `구단 재정 현황` context where useful, using `구단 연봉 총액`, `샐러리캡 산정액`, `샐러리캡 기준액`, `샐러리캡 초과 현황`, `선수 영입 예산`, `예상 연봉 부담`, `스폰서 수익` and `시즌 성과 보너스`. The UI labels annual renewal separately from FA and offers no budget allocation or sponsor negotiation controls.

### Persistence Work
Contract terms, offers, annual salary negotiation/round/deadline/final-decision receipts, salary history, service ledger, agent agreement and payment ledger. Add C48 policy/version/provenance, annual budget allocations, sponsorship agreement periods/terms, unique realized base/bonus settlements and historically meaningful salary-cap assessments; derived payroll caches remain rebuildable and version-tagged. Store the ContractRules/SalaryCapRules version used and the C10 achievement evidence IDs. One player-season renewal outcome and one eligible sponsorship condition settlement may become authoritative; pending state survives save/load and migration adds no invented past negotiations, sponsor revenue or cap results.

### Events
`ContractOfferReceived`, `AnnualSalaryProposed`, `AnnualSalaryCountered`, `AnnualSalaryResolved`, `PlayerSignedContract`, `ContractExpired`, `AgentHired`, `AgentAgreementEnded`, `IncomePaid`, `CommissionPaid`, `ClubBudgetAllocated`, `SalaryCapAssessed`, `ClubSponsorshipSettled`, `ClubSponsorshipRenewed`.

### Tests
Expired/stale offers, repeated acceptance, multi-year salary, team changes, incentive boundaries, fee rounding, insufficient club player budget, save on either side of a payment. Test club proposal→agent advice→counteroffer→final decision, direct acceptance, deadline rule, NPC resolution, reload while pending, stale/duplicate round rejection, previous-salary comparison, one salary-history row and no overlapping obligation. Verify correct annual obligation sum, signing/release/trade projection inputs, multi-year commitments, total-vs-cap-vs-budget separation, included/excluded cap components, threshold/excess and rule-defined consequence, frozen rule version, sponsor revenue never changing the legal cap, base/regular/postseason/champion settlement timing and exactly-once receipts, bounded renewal/allocation and deterministic identical-state results. Confirm the agent cannot finalize and an annual renewal cannot enter the FA offer state.

### Acceptance Criteria
**Gate: G +** yearly payment totals equal terms less explicit fees; replaying a payment causes no second credit; overlapping prohibited club contracts are rejected; the agent cannot finalize the user's contract or annual salary; comparison data matches authoritative terms. Every eligible annual renewal has exactly one pending/resolved outcome under the saved ContractRules, survives reload, appends coherent salary history and never duplicates/overlaps obligations or masquerades as FA. C48 payroll reconciles exactly to C13 obligations, budget and cap remain independent, every sponsorship settlement is backed by confirmed evidence and occurs once, renewal stays within declared bounds, historical assessments survive load unchanged, and no GM command or fabricated official KBO value is introduced.

### Deliverables
Contract/annual-salary/agent/payment services, C48 Club Finance Lite core, read-only financial summaries, contract and `연봉 협상` screens, salary-history/ledger/finance fixtures, `PHASE_14_REPORT.md`.

### Risks
Confusing annual renewal with FA, offer total with annual pay, agent advice with player consent, duplicate/overlapping obligations, service accrued twice, payroll cache becoming a second contract truth, cap and budget being conflated, sponsor bonuses settling from projections or twice, and financial snowball/rounding drift over decades.

### Documentation Updates
`docs/contract-system.md`, annual-renewal and Club Finance Lite/SalaryCapRules boundaries, ledger/contract authority, C13/C25/C37/C42/C48, current state/report.

### Claude Code Handoff
> Implement domestic contracts plus the distinct annual non-FA salary renewal loop, agent advice, auditable earnings and bounded C48 Club Finance Lite. Derive payroll from C13, keep cap/budget/payroll distinct, settle sponsorship only from confirmed C10 facts and prove idempotency. Final controlled-player choices belong to the player; add no GM mode and do not guess KBO cap rules or official club money.

## Phase 15 — FA eligibility and multi-club offers

### Goal
Prove eligible players can enter a domestic FA market and select meaningful offers without the game choosing for them.

### Dependencies
Phase 14; approved D2 policy with the FA eligibility/compensation/market subset specified before implementation.

### Scope
FA declaration/window, AI offers, role/opportunity/team competitiveness/loyalty preferences, C48 projected payroll/budget/salary-cap assessment, own-player final selection, NPC market resolution and atomic old/new club jersey-number transition.

### Explicit Non-Goals
No foreign FA market, posting, trade deals, playable finance controls or expansion beyond the approved C48 projections.

### Architecture / Systems
C18 with Contract/Roster/C47/C48 services; C48 supplies authoritative contract-derived projections and policy/rule assessments, not a management game.

Use C40 KBO Rule Engine FARules; preserve the career-frozen versions, and test a differing rule-version fixture rather than embedding 2026 values in this subsystem.

### Domain Models
`FAEligibilitySnapshot`, `FADeclaration`, `MarketWindow`, `FAOfferComparison`, `ClubDemand`, `ProjectedClubFinancialImpact`, `CompensationObligation` where the approved rules require it.

### Implementation Tasks
1. Compute eligibility from saved service and rule versions; expose reasons and remaining service.
2. Before generating or accepting an offer, evaluate existing C13 obligations plus proposed multi-year terms through C48 for projected total payroll, cap charge, internal player budget and the configured excess consequence. Generate offers with documented club demand/role/ClubPhilosophy policy; do not treat sponsorship revenue as extra legal cap room.
3. Pause for controlled-player decisions; resolve NPC preferences with saved seeds and deterministic tie-breaks.
4. Activate chosen contract and all required roster/compensation effects in one unit of work. If the club changes, close the old C47 assignment and select/validate a legal independent new-club number in that same transaction; no requested number is guaranteed.

5. Consume saved ClubPhilosophy FA aggressiveness/win-now/development/roster-stability preferences within offer legality and budget caps; first eligibility opens optional contextual rule help.

### UI Work
FA eligibility/declaration and comparison cards showing money, length, role, competition, loyalty, agent advice and an explainable public budget/cap constraint reason where relevant; pending offer deadline indicator. On signing, show `이적 후 새 등번호`, requested/available alternatives and final assignment without a separate management screen.

### Persistence Work
Market window/declaration/offer and compensation rows plus transactional C47 transition and C48 assessment reference; keep declined/expired facts needed for career history; migration preserves prior service totals and number history without inventing financial assessments.

### Events
`PlayerDeclaredFA`, `FAOfferReceived`, `FAOfferDeclined`, `FAAgreementReached`; Contract emits actual signing.

### Tests
Ineligible declaration, exact service threshold, multiple simultaneous offers, deadline, no-offer result, older offer after another signing, same/new-club number availability, retired/conflicting request rejection, old assignment closure/new assignment atomicity, compensation failure rollback, NPC deterministic market. Verify offers include existing plus proposed obligations, respect player budget and rule-defined cap behavior, retain binding salaries, and show bounded differences between financially cautious/aggressive ClubPhilosophy profiles.

### Acceptance Criteria
**Gate: G +** one player cannot accept two offers; career/roster/contract/C47/C48 show the same chosen team, obligations, financial assessment and legal club-specific number after reload; old-club number closes only on committed movement; legal no-offer/unresolved status is supported; clubs do not repeatedly make financially contradictory offers; preference advice never locks the controlled player's team or number choice.

### Deliverables
FA policy/market/UI, threshold and market fixtures, `PHASE_15_REPORT.md`.

### Risks
Scope creep into GM simulation, fabricated guarantees, incorrectly treating friendship as a forced destination.

### Documentation Updates
Contract/FA rule profile, C18, UI comparison, current state/report.

### Claude Code Handoff
> Implement the domestic FA process with explicit eligibility and user authority. Use C48 to assess existing plus proposed obligations, internal budget and saved cap consequences before offers; keep sponsorship and the legal cap independent. Close/open C47 assignments atomically when clubs change. Support no-offer outcomes and do not add finance-management controls.

## Phase 16 — Transfers, releases, foreign-player intake, and NPC exits

### Goal
Prove the league can change personnel and replace departures without invalid contracts, affiliations or history.

### Dependencies
Phase 15; approved D2 policy with the trading/foreign-player/roster subset specified before implementation.

### Scope
AI club trades, no-trade consent, releases, domestic re-signing, foreign-player candidate pool/contracts, C48 incoming/outgoing/current/future payroll effects, club-specific jersey-number transitions, routine NPC retirement and historical identity preservation.

### Explicit Non-Goals
No foreign league simulation; no player-executed trade; optional trade-request gameplay remains deferred because G explicitly calls it a later consideration. Full retirement ceremonies/legacy are Phase 25.

### Architecture / Systems
C19/C07/C13/C30/C47/C48. Foreign recruits are background-origin Player records, not members of simulated MLB/NPB leagues.

Use C40 KBO Rule Engine ForeignPlayerRules plus existing roster/contract transfer rules; preserve the career-frozen versions, and test a differing rule-version fixture rather than embedding 2026 values in this subsystem.

### Domain Models
`TradeProposal`, `TransferPlan`, `TransferFinancialImpact`, `NoTradeConsent`, `ReleaseDecision`, `ForeignCandidatePool`, `RetirementDecision`.

### Implementation Tasks
1. Evaluate finite AI proposals with roster need, value, salary, remaining terms, current/future C48 payroll, internal budgets, cap implications and eligibility; validate both clubs before committing. Finance influences but cannot override roster/contract legality or baseball value.
2. Require controlled-player consent only where contract rules grant it; otherwise expose confirmed AI trade outcome.
3. Implement release/re-sign and quota-limited foreign candidate intake; retain foreign background histories without foreign standings. Release/transfer closes the old club's active number; a committed destination assigns a separate legal number, and a released number becomes reusable only after the closed interval and under club rules.
4. Introduce routine NPC retirement based on versioned age/ability/opportunity policy. Controlled-player retirement becomes a pending career-end request until Phase 25's full ending, never an unnoticed deletion.

5. Consume saved trade aggressiveness/prospect protection/roster-stability and bounded financial-caution weights in proposal scoring. A traded pitcher keeps prior pair history by identity and builds new catcher chemistry from actual exposure; no global chemistry reset or automatic elite new pairing.

### UI Work
Trade/no-trade dialog, transfer/release notices with old/new number, contract and team links, released/retired database filters and basic retirement notice.

### Persistence Work
Atomic contract/affiliation/assignment/C47 transition and C48 financial-impact assessment, dated team stints and jersey intervals, trade payload, intake origin, released/retired status. Do not cascade-delete history or duplicate contract obligations in finance rows.

### Events
`PlayerTraded`, `PlayerReleased`, `ForeignPlayerSigned`, `PlayerRetired`, `RetirementDecisionRequested`.

### Tests
Two-club rollback, injury/registration restrictions, no-trade rejection, no destination, foreign quota, incoming/outgoing salary and remaining-term projections for both clubs, budget/cap consequence, release/replacement cost, transfer/release closes old number, new club assigns independently, released number reuses only after closure, repeated command creates no duplicate, retire/re-sign legality, retired player stat/number links and saved pending consent.

### Acceptance Criteria
**Gate: G +** no active incompatible duplicate affiliation or prohibited overlapping club number; every transfer reconciles both rosters/contracts/number intervals and C48 projections for incoming/outgoing obligations; rejected transfer leaves both clubs and their financial history unchanged; foreign intake works offline with fictional data; NPC retirement preserves all historical references.

### Deliverables
Transaction/intake/exit policies, notices/dialogs, invariant suite, `PHASE_16_REPORT.md`.

### Risks
Multiple systems editing membership, infinite trade churn, league population inflation, and erasing retired players to reduce database size.

### Documentation Updates
Roster/contract/career docs, C19/C30 and current state/report.

### Claude Code Handoff
> Add atomic AI personnel changes and sustainable player exits/intake. Assess incoming/outgoing obligations through C48 without overriding roster legality or baseball value. Close/open C47 club-number intervals with the same transaction and preserve Player identity plus full number history. Do not add player finance management or expand into foreign leagues.

## Phase 17 — Twenty-season world stability

### Goal
Prove the domestic world remains populated, legal, and explainable after the original roster has substantially turned over.

### Dependencies
Phase 16.

### Scope
Integrated annual cadence, imported/generated draft provenance, FA/contracts including annual salary renewals, Club Finance Lite payroll/cap/budget/sponsorship, jersey-number lifecycle, development/rosters/retirement, staff turnover, captain/atmosphere continuity, stadium/park history, All-Star voting, weather makeup schedules, population and balance diagnostics, save migration chain and measured performance.

### Explicit Non-Goals
No new life/international features or silent removal of failing rules to accelerate tests.

### Architecture / Systems
SimHarness drives Application commands and explicit harness decision policies; no alternative simulation shortcut.

### Domain Models
`WorldHealthReport`, `PopulationLedger`, `AnnualAudit`, `BenchmarkRun`; diagnostic models only.

### Implementation Tasks
1. Stitch annual phase windows without circular date dependencies or unresolved NPC market locks.
2. Run five fixed seeds for 20 seasons; checkpoint annually and compare selected resumed segments with uninterrupted runs.
3. Inspect roster shortages, age/current-skill distributions, elite tails, unsigned/released/retired counts, salary policy, payroll growth, budget continuity, cap assessments/consequences, sponsor revenue distribution and school outputs.
4. Fix defects in their owning modules; record tuning changes/version bumps separately from behavior changes requiring owner approval.
5. Audit that managers do not remain forever without an explainable retained tenure outcome, staff changes preserve relationships, captain history is coherent, atmosphere returns from unsupported extremes, games retain original stadium/park versions, annual salary renewals create one legal obligation set, All-Star selections remain unique after reload, and postponed games produce one legal played/cancelled chain.
6. Audit imported drafts are never rerun, generated classes/picks persist across reload with stable noncolliding PlayerIds, and every active club number is legal/unique while all change/transfer/release/retirement intervals remain coherent and historical assignments stay visible.
7. Under D6, record working SQLite size, each manual/rolling/pre-decision snapshot family and total storage multiplication; measure save, load, backup/snapshot, migration, Game Archive and important profile/history queries; report growth by table family and the separate raw-pitch/narrative contributions. Preserve first and perform no production pruning in this phase.
8. Audit C48 over five 20-season runs: every payroll sum matches C13 obligations; binding salaries survive financial trouble; sponsor base/bonuses settle once from confirmed facts; renewal/allocation remains within policy bounds; cautious/aggressive clubs differ only within declared caps; successful clubs do not accumulate runaway purchasing power; struggling clubs retain baseline support and can recover; cap room and budget room never substitute for each other.

### UI Work
Existing screens must open late-save profiles, season tables, retired players and draft histories; expose long-operation progress/cancel at safe boundaries.

### Persistence Work
Measure database and snapshot growth by family, operation times and indexed query plans. Exercise every prior schema fixture and rebuildable projection. D6 permits no pruning without recorded evidence and an explicit targeted policy that preserves authoritative history and recovery guarantees.

### Events
No new product events; diagnostics verify event/projection uniqueness.

### Tests
Five ×20-season runs, periodic reloads, controlled-decision stop tests, population accounting and historical reconciliation, imported/generated draft source/ID/no-rerun checks, jersey uniqueness/timeline/retired-number checks, staff/captain/stadium/park-version/salary/All-Star/weather/atmosphere invariants, C48 payroll/cap/sponsorship/budget/offer/trade stability and recovery distributions, and the full D6 storage/operation/query/table-family/raw-pitch/narrative measurement matrix in §6.3.

### Acceptance Criteria
**Gate: G +** zero illegal lineups, overlapping salary obligations, prohibited active club-number conflicts, duplicate payments/sponsorship settlements/draft picks/All-Star selections, rerun imported drafts, regenerated saved cohorts, lost/duplicate fixtures or orphan references; every C48 payroll reconciles to C13 and no financial history is rewritten after policy/rule changes; player budgets, sponsor renewal and wealth distribution remain within declared bounds with recovery observed; every active-player count equals prior population plus entries minus exits; PlayerIds, draft provenance, jersey/staff/captain histories and stadium/park contexts remain referentially valid; TeamAtmosphere has no unsupported permanent extreme; no unexplained yearly monotonic strength/population/financial drift outside declared tuning bounds; the complete D6 measurement report records actual values; measured budgets pass on recorded hardware or targeted fixes bring them within budget. No data is pruned merely to pass the gate.

### Deliverables
Seeded soak outputs, balance charts/tables, query plans, migration evidence, `PHASE_17_REPORT.md`.

### Risks
Passing a single “lucky” seed, suppressing shortages, or equating deterministic behavior with believable balance.

### Documentation Updates
`docs/testing/long-save.md`, tuning baseline and performance measurements, current state/report.

### Claude Code Handoff
> Validate the real domestic command path over five 20-season runs, including C48 payroll/cap/sponsorship/budget stability and recovery, imported/generated draft source and stable IDs, C47 timelines, staff turnover, captain/atmosphere continuity, stadium/park versions, annual salary renewal, All-Star idempotency and weather makeup schedules. Report failures explicitly; do not hide financial divergence by changing rules or obligations.

## Phase 18 — National selection and international competitions

### Goal
Prove international participation coexists with domestic affiliation and uses the same game engine with separate records.

### Dependencies
Phase 17; approved D2 policy with tournament schedules, entry/selection eligibility and format profiles specified before implementation.

### Scope
AI selection, tournament squads, Asian Games/Olympics/WBC/Premier12 profiles, fictional opponent squads, matches/medals, domestic leave/return and career effects.

### Explicit Non-Goals
No full foreign club leagues, automatic representative selection for a good player, or military-status award before Phase 19.

### Architecture / Systems
C20/C21; calendar participation reserves availability; Roster keeps domestic affiliation.

### Domain Models
`CompetitionDefinition`, `SelectionPolicy`, `NationalSquad`, `TournamentBracket`, `InternationalStatLine`, `MedalFact`.

### Implementation Tasks
1. Load versioned competition definitions and eligibility rules; use fictional national opponent player pools with persistent IDs.
2. Select squads by role competition, ability, performance, health, fame and tournament policy.
3. Schedule leave/games/return against domestic fixtures; route own appearances through the existing game/decision UI.
4. Persist selection/nonselection, international statistics/medals and bounded reputation/popularity effects.

### UI Work
Selection event, contextual tournament page, shared game screen, international career/stat tabs and return-to-club notice.

### Persistence Work
Selections, entries, fixtures, medals and international stat scope; rule provenance stored with tournament edition.

### Events
`PlayerSelectedForNationalTeam`, `NationalSelectionMissed`, `InternationalCompetitionCompleted`, `PlayerWonMedal`, `PlayerReturnedToClub`.

### Tests
All four tournament definitions instantiate; position competition/ineligibility; date conflict; player injured during tournament; own game and AI opponent; medal ranking and domestic return.

### Acceptance Criteria
**Gate: G +** one complete fixture tournament per supported definition passes; no simultaneous incompatible participation; international stats never enter KBO regular-season totals; selection and medals survive reload without changing club contract.

### Deliverables
Tournament/selection services, rule JSON, contextual screens and fixtures, `PHASE_18_REPORT.md`.

### Risks
Expanding opponent squads into foreign leagues; medal outcome inferred from news text; imported national players colliding with domestic IDs.

### Documentation Updates
`docs/international-military.md`, C20/C21, UI routes, current state/report.

### Claude Code Handoff
> Implement data-driven international competitions on the shared baseball engine. Preserve domestic affiliation, separate records, and AI selection. Emit medal facts for Phase 19 without changing military status here.

## Phase 19 — Military career and sports-team assignment

### Goal
Prove service choices and sports-personnel eligibility produce coherent career interruptions and returns.

### Dependencies
Phase 18; approved D2 policy with the versioned military eligibility/duration/contract-service-credit subset specified before implementation.

### Scope
Unresolved/planned/serving/completed/arts-sports/other-exemption states, ordinary service, competitive sports-team applications, second-team play, saved return affiliation and tournament qualification.

### Explicit Non-Goals
No claim of current legal advice, automatically accepted sports-team applications, automatic large ability collapse, or overwriting club ownership with military squad membership.

### Architecture / Systems
C22; existing roster, calendar, growth, contract and competition ports apply the resulting restrictions/effects.

### Domain Models
`MilitaryStatus`, `EnlistmentPlan`, `SportsTeamApplication`, `ServicePeriod`, `ReturnAffiliation`, `SportsPersonnelEligibility`.

### Implementation Tasks
1. Implement allowed transitions and nationality/eligibility checks from the saved rule profile.
2. Resolve applications using ability/performance/position/age/capacity; create temporary military squad assignments and second-team fixtures.
3. Apply ordinary-service time/training/sharpness changes and approved contract/service-credit treatment.
4. Evaluate Asian Games first place and Olympic top-three plus individual eligibility from confirmed results; distinguish sports-personnel status from other exemption.

### UI Work
Always-visible profile status, decision event, application result, service progress and return event; shared second-team game UI.

### Persistence Work
Military transitions/application/service dates, original affiliation/contract treatment and eligibility evidence. Migration never guesses a real player's legal status.

### Events
`EnlistmentPlanned`, `SportsTeamApplicationResolved`, `MilitaryServiceStarted`, `SportsPersonnelQualified`, `PlayerCompletedMilitaryService`.

### Tests
Ordinary and sports service, rejected application, date interruption/reload, injured return, noneligible medalist, qualifying tournament vs nonqualifying WBC/Premier12, invalid repeated transition.

### Acceptance Criteria
**Gate: G +** every supported state/path has a saved transition fixture; sports-team stats remain second-team scope; return uses valid original/updated affiliation; medal alone does not grant status without individual rule checks; no implicit ordinary-exemption substitution.

### Deliverables
Military state machine/rules/UI, integration fixtures and `PHASE_19_REPORT.md`.

### Risks
Changing real rules corrupting existing saves, inconsistent contract clocks, and military squad players also appearing for parent club.

### Documentation Updates
International/military and contract rule docs, C22, current state/report.

### Claude Code Handoff
> Implement military career state from saved rule data and confirmed tournament facts. Keep ordinary exemption distinct from arts/sports personnel and preserve the original club relationship. Do not fetch changing real-world rules into live saves.

## Phase 20 — Assets, spending, and lifestyle time

### Goal
Prove earned money and finite personal time create meaningful career-supporting choices.

### Dependencies
Phases 14 and 19.

### Scope
Cars, housing, equipment, private coaching, health/rehab support, travel/hobbies/family expenses/savings, overseas personal camps, calendar occupancy and state effects.

### Explicit Non-Goals
No independent life sandbox, financial market simulator, foreign playing career, large car/romance rating bonuses, or commercial offers yet.

### Architecture / Systems
C24–C26; asset catalog is static JSON, ownership/transactions/activities belong to the save; effects go through PlayerState/Development.

### Domain Models
`AssetDefinition`, `OwnedAsset`, `PurchaseDecision`, `ActivityReservation`, `ExpenseObligation`, `LifestyleEffect`, `SavingsAllocation`.

### Implementation Tasks
1. Define catalog prices/effects and transaction policies with explicit affordability and ownership validation.
2. Connect purchases/recurring housing or coaching expenses to the existing ledger; savings are transfers, not new income.
3. Schedule activities against games/service/training; reject incompatible overlaps and apply each effect once.
4. Separate recovery/training support from base-skill changes; overseas camps remain personal investment activities.

### UI Work
Assets summary and categories, ownership/details, purchase confirmation, Life activity choices and expected time/state trade-offs.

### Persistence Work
Ownership/ledger/activity/effect receipts and recurring obligations; old saves begin with known cash/opening balance, never fabricated past expenses.

### Events
`AssetPurchased`, `HousingChanged`, `ActivityScheduled`, `ActivityCompleted`, `ExpensePaid`, `SavingsTransferred`.

### Tests
Insufficient funds, repeated purchases, overflow/rounding, recurring payments after load, savings conservation, schedule overlaps, retirement/service restrictions and cancelled activities.

### Acceptance Criteria
**Gate: G +** closing wealth reconciles opening balances, earned income, expenses and owned-value policy; no double payment/effect; each catalog category has a usable path; car purchases never directly add large baseball-skill bonuses.

### Deliverables
Asset/activity services/catalogs, Assets/Life scenes, ledger/time accounting suite, `PHASE_20_REPORT.md`.

### Risks
Duplicating the contract wallet, unlimited activity stacking, or unbounded recurring costs blocking progress without a decision.

### Documentation Updates
`docs/life-system.md`, catalog/effect schema, C24–C26, current state/report.

### Claude Code Handoff
> Connect existing income to finite spending and time choices. Keep catalogs data-driven and all effects owned by the appropriate state/development service. Do not introduce an unrelated economy game.

## Phase 21 — Relationships, family, and structured stories

### Goal
Prove relevant relationships and player choices create consistent life stories whose facts are decided without an LLM.

### Dependencies
Phases 9 and 20.

### Scope
Manager/coach/teammate/competitor/rival/family/partner/agent relationships across staff tenures, captaincy/leadership, lightweight TeamAtmosphere, dating-marriage-family events, personality/image/reputation, conditional stories, authoritative interview triggers/context and approved consequence categories/rules. Final free-text interpretation is Phase 26.

### Explicit Non-Goals
No league-wide social graph, staff-management game, full dating simulator, captain-selected teamwide ability buff, detailed discipline/ejection/hearing/suspension/appeal/bench-clearing subsystem, hidden numbers exposed, invented relationship facts from prose, or forced FA destination.

### Architecture / Systems
C23/C24/C26/C29/C43. Ordinary story eligibility/choice effects are core data; interview context and allowed consequence rules are prepared here. No arbitrary LLM interpretation before the validated Phase 26 bridge.

### Domain Models
`CharacterId`, `RelationshipEdge`, `RelationshipCategory`, `FamilyState`, `CaptainAssignment`, `LeadershipContext`, `TeamAtmosphere`, `AtmosphereCause`, `StoryDefinition`, `StoryInstance`, `ChoiceEffect`, `InterviewContext`, `InterviewRequest`, `InterviewConsequenceRule`, `ReputationProfile`. An interview has no final predefined answer intent.

### Implementation Tasks
1. Create player-centered relevant character edges; keep managerial trust, personal friendship and BatteryChemistry distinct. A represented friendship may modestly affect future pair-growth rate, never instantly set excellent chemistry.
2. Implement predicates, participant binding, cooldowns, expiry and legal effects for ordinary structured stories.
3. Add family progression and lightweight rivalry/team-adaptation stories; family/location preferences advise rather than constrain choices.
4. Define interview eligibility, confirmed context, reporter/topic intent, allowed targets/semantic categories and versioned core consequence mappings. Save triggered requests; do not simulate final answers with humble/confident/fan choice buttons or guessed text meaning.
5. Until Phase 26 passes, record interview eligibility/context as inactive feature records, not mandatory unresolved gameplay barriers; staged tests can inspect requests without Gemini. Activate player-facing interview obligations only when the validated interaction exists. After activation, temporary provider failure preserves real pending decisions. This is an explicit pre-feature boundary, not a template substitute. Under approved D4, free-text biography/character traits remain descriptive; only separately defined structured traits may have bounded mechanics, and the interview bridge never generalizes into arbitrary free-text game commands.
6. Bind manager/coach relationships to stable staff identities and tenure intervals. A new manager starts a new bounded trust/evaluation context while the previous manager relationship remains in history; coach changes can alter future evaluation/training-story emphasis only within caps.
7. Let Club AI designate a captain where the saved league/game profile supports it, using the Phase 9 multi-factor context rather than one hidden-stat threshold. The player may become captain but never chooses the team's captain. Captaincy primarily unlocks leadership, junior/teammate, media, interview, adaptation and legacy facts.
8. Update player-centered TeamAtmosphere from documented causes such as recent performance, manager transition, role conflict, major personnel events, relevant relationships, captain presence, title race, losing streak or controversy/success. Apply bounded stress/satisfaction/adaptation/story-probability effects and gradual cause-based recovery; never use it as a large direct rating modifier or a full league social simulation.

### UI Work
Relationship categories/reasons, separate current-manager trust and prior staff relationships, `주장`/leadership status, `팀 분위기` category/reasons, family/life status, ordinary priority story choices and timeline links. Use Korean events such as `감독 교체` and `새 감독 부임`. Final interview answers and actionable Inbox interview obligations ship in Phase 26; earlier diagnostic context is explicitly inactive, with no fake answer controls. All player text is Korean.

### Persistence Work
Characters/edges with staff-tenure references, captain assignments/history, bounded atmosphere state/cause/update cursor where authoritative, family/story instances/cooldowns/choice receipts/image-fame facts; interview request/context and consequence-rule version. No retroactive staff relationship, captaincy, atmosphere event, romance, prior interview or quote inferred during migration.

### Events
`RelationshipChanged`, `RivalryEstablished`, `CaptainDesignated`, `CaptaincyEnded`, `TeamAtmosphereChanged`, `FamilyStateChanged`, `StoryTriggered`, `StoryChoiceResolved`, `InterviewRequested`, `ReputationChanged`. No `InterviewIntentSelected` fixed-choice event.

### Tests
Eligibility contradictions, retired/released/missing participants, repeated ordinary choice, cooldown/reload, friendly competitor, low trust with close relationship, manager replacement with retained former relationship/new evaluation context, coach departure, multi-factor captain choice/termination/transfer, captain story without rating buff, atmosphere cause/recovery/cap and long negative/positive streak, overridden family preference; interview context contains only confirmed facts and allowable categories; no answer effects or unavailable-feature advancement lock before Phase 26 activation; friendship/chemistry distinct.

### Acceptance Criteria
**Gate: G +** one complete fixture per named relationship class/family stage plus staff turnover, captaincy and atmosphere paths; ordinary story facts/effects survive reload and run offline; former staff history remains available; captaincy is AI-designated and social/narrative; TeamAtmosphere remains bounded and cause-linked; no full NPC social network or raw public relationship values; interview triggers/context/rules persist with zero premature answer consequences. Final interview interaction is explicitly delivered by Phase 26.

### Deliverables
Relationship/story services, initial varied templates, Relationships/events UI, `PHASE_21_REPORT.md`.

### Risks
Stories reapplying on screen open, staff change erasing history, captaincy becoming a universal buff, TeamAtmosphere drifting to a permanent extreme, coercive preferences, or free-text traits becoming an unbounded gameplay language.

### Documentation Updates
Life/story contracts, visibility policy and story authoring guide, current state/report.

### Claude Code Handoff
> Implement authoritative relationships, staff-tenure continuity, captaincy/leadership, bounded player-centered TeamAtmosphere, ordinary structured stories, and interview triggers/context/consequence rules only. Remove predefined final interview-answer intent choices. Leave free-text interpretation to Phase 26; keep friendship, trust, atmosphere and pair chemistry separate. Do not add staff management or a discipline simulator.

## Phase 22 — Advertising, sponsors, and agent marketing

### Goal
Prove fame can generate commercial opportunity with money, equipment, time and fatigue trade-offs.

### Dependencies
Phases 14, 18 and 21.

### Scope
Fictional individual-player brand categories, agent marketing, advertisement/sponsor proposals, terms/image requirements, appearance schedules, equipment obligations and player final decision. This remains separate from C48 club sponsorship revenue.

### Explicit Non-Goals
No compulsory real brands, unbounded stat bonuses, automatic acceptance, separate marketing management game, or access to C48 club sponsor agreements/revenue.

### Architecture / Systems
C25/C26/C37/C38; commercial terms differ from club playing contracts but reuse ledger/time ports.

### Domain Models
`BrandCategory`, `CommercialOffer`, `CommercialAgreement`, `ImageRequirement`, `AppearanceObligation`, `EquipmentSponsorship`.

### Implementation Tasks
1. Generate bounded offers from form, career, public image, popularity, national-team history and brand criteria.
2. Apply agent negotiation/fees and present terms for final user decision.
3. Schedule shoots/events and equipment-brand conditions; connect payments, reduced personal/training time and fatigue.
4. Handle expiry/conflicting commitments and agreed failure consequences without retroactively deleting completed income.

### UI Work
Contract commercial tab, agent comparison/advice, schedule obligations, sponsor equipment labels and asset income history.

### Persistence Work
Commercial terms/obligations/payment/effect receipts and campaign career facts; migrations create no invented historical endorsements.

### Events
`CommercialOfferReceived`, `AdvertisementSigned`, `SponsorSigned`, `CommercialAppearanceCompleted`, `CommercialAgreementExpired`.

### Tests
Offer criteria, fee/payment replay, competing time reservations, equipment restriction, unsigned offer has no income, offline story rendering, two-way athlete schedule load.

### Acceptance Criteria
**Gate: G +** advertisement and equipment sponsorship complete full lifecycles; cash/time/fatigue changes reconcile to terms; rejection changes no ownership/payments; sponsor benefit remains within documented balance bounds and does not guarantee skill gains.

### Deliverables
Commercial services/catalogs/UI, lifecycle scenarios, `PHASE_22_REPORT.md`.

### Risks
Reusing playing-contract eligibility for commercial terms, double commission, or time costs never reaching training.

### Documentation Updates
Contract/life integration and C38, current state/report.

### Claude Code Handoff
> Implement commercial careers through existing money/time/agent interfaces. Use fictional brands and explicit user acceptance. Test the costs of commercial activity as carefully as its income.

## Phase 23 — Historical continuity and advanced metrics

### Goal
Prove decades of player, team and league history reconcile, including team-specific contributions needed by legacy systems.

### Dependencies
Phases 11, 17, 19 and 22.

### Scope
Unified imported/generated history, imported/generated draft provenance/results, dated club-specific jersey-number history, season/team-stint/career/competition records, milestones, advanced stats (WAR, wRC+, OPS+, FIP, ERA+, BABIP), versioned park-adjusted context where the approved formula requires it, awards and representative events. Retain meaningful stadium usage, manager tenure, captaincy, All-Star voting/selection and annual club payroll/cap/sponsorship facts.

Add read-only Game Archive projections and milestone progress alongside milestone occurrences; C27/C44.

### Explicit Non-Goals
No claim that approximate game-model WAR equals an external provider's formula; no fabricated historical inputs; legacy selection decisions wait for Phase 25.

### Architecture / Systems
C11/C14/C27/C28/C47; explicit metric definitions and versions, rebuildable read projections over retained counting inputs.

### Domain Models
`HistoricalCoverage`, `MetricDefinition`, `MetricValue`, `HistoricalDraftFact`, `JerseyNumberHistory`, `HistoricalStadiumContext`, `StaffTenureFact`, `CaptaincyFact`, `AllStarHistoryFact`, `ClubFinancialHistoryFact`, `TeamStint`, `MilestoneOccurrence`, `CareerSummary`, `FranchiseLeaderboard`.

Add `GameArchiveQuery`, `HistoricalGameView`, `HistoryCoverage`, `MilestoneProgress(current, target, remaining, scope, availability)`; no quest state or new simulation.

### Implementation Tasks
1. Define exact denominators, league/park baselines, replacement/position treatment, two-way aggregation and minimum-sample policies for each metric in `docs/statistics.md`. Park-adjusted metrics use each game's retained C46 stadium/profile version; never today's park definition for an old game.
2. Implement simple ratios and comparative metrics first, then a documented simulation-specific WAR model from retained batting/pitching/fielding inputs; mark method and unavailable values.
3. Join imported opening baselines with generated intervals without overlaps/double counting.
4. Build team-season summaries, franchise leaderboards, milestones, role/position/entry/military/commercial timelines and historical news links.

5. Query retained games by season/date/team/postseason/controlled-player appearance and expose available score, box score, lineup, PA outcomes, scoring progression, own appearances and important events with coverage metadata. Under D6 these authoritative facts are permanent; never rerun historical games, invent missing detail or substitute seasonal aggregates for required evidence.
6. Calculate milestone progress from reconciled career totals (1,873 hits → 127 to 2,000; 487 HR → 13 to 500); handle surpassed targets, incomplete baselines and competition scopes without duplicate occurrence events.
7. Project historically meaningful stadium/home-usage intervals, manager/coach tenures, captain assignments, atmosphere-linked major facts and All-Star race/final selection without retaining every transient social/vote update or inventing missing imported detail.
8. Project draft source/round/pick/drafting club/entry facts and complete C47 club-number intervals into Player/Career/Team history. Later number retirement changes future availability only; it never rewrites a historical assignment.
9. Project C48 annual payroll, saved cap assessment/consequence, player-budget context and realized club sponsorship revenue with policy/rule/provenance labels. Rebuilds may recalculate views but cannot rewrite settled revenue or a historical decision's saved assessment.

### UI Work
Records tabs for season/career/second-team/national/postseason/rankings; Career timeline and team history with draft facts, `등번호 이력`, stadium, manager tenure, captain, All-Star and meaningful club-finance links. Explain advanced and park-adjusted metrics in tooltips and label unavailable history rather than displaying zero.

### Persistence Work
Metric/version/baseline/coverage metadata and indexed projections; preserve draft provenance/results, jersey intervals and source counts plus retained per-game stadium/profile context and C48 historical assessment/settlement references. Recalculation creates a versioned projection, not a rewrite of historical drafts/numbers/outcomes/selections/awards/financial facts already decided under earlier versions. D6 measurement may identify query/index work but cannot authorize deletion of authoritative history.

### Events
`CareerMilestoneReached`, `FranchiseRecordSet`, `LeagueRecordSet`; metric rebuilds are diagnostic/projection notifications.

### Tests
Known small stat fixtures, zero denominators, transferred players/team stints, two-way contributions, missing imported fields, overlapping baseline rejection, 6+2 titles, ties in record rankings and repeated milestone projection.

Archive filters/box-score, retained stadium/weather and lineup identity after transfers; old park-factor version after venue/profile change; staff/captain/All-Star and annual finance history links; later policy/rule changes leave old payroll/cap/sponsor facts unchanged; incomplete retained detail; archive read changes no state/RNG; exact progress examples, surpassed/unknown totals and boundary occurrence idempotency.

### Acceptance Criteria
**Gate: G +** season/team-stint sums reconcile to supported career totals; each metric has a reproducible formula fixture; unavailable inputs never become a false exact WAR; historical projection rebuild leaves authoritative outcomes unchanged; long-save queries meet §6.3.

Archive rows reconcile to existing game/history records, retain original stadium/park context and disclose unavailable detail; imported/generated draft and jersey timelines plus staff/captain/All-Star/finance history are coherent; C48 historical facts retain the original rule/policy/evidence context; progress and occurrence share the same totals and never create RPG tasks.

### Deliverables
Metric/history projectors, query indexes, record/timeline UI, reconciliation report, `PHASE_23_REPORT.md`.

### Risks
Mixing league levels or metric methods, double counting traded players, preserving totals but losing franchise-specific contributions.

### Documentation Updates
Statistics/history/data dictionary and C11/C27/C28, current state/report.

### Claude Code Handoff
> Complete historical read models and explicitly defined advanced metrics, including locked draft provenance and full C47 jersey timelines. Keep provenance, coverage and missing-data semantics. Never add imported career totals on top of the seasons they already contain or rewrite historical numbers after retirement.

## Phase 24 — Complete player, team, and amateur discovery

### Goal
Prove users can navigate the whole saved world and return to their prior task without losing filters or live-game state.

### Dependencies
Phases 12 and 23.

### Scope
All required Player DB filters/profile tabs including imported-rookie/draft filters, retired players, complete Team Profile with C48 read-only financial summary/history, school output search/panels, draft histories/projections, jersey-number history and global links.

Add favorites/watchlist, two-player comparison, Game Archive and milestone UX; implement P Seed 2D/profile base and mode/fallback packages here (C44/C45).

### Explicit Non-Goals
No hidden-stat disclosure, school/club/finance management controls, standalone finance-management route, Gemini image API calls before Phase 26, or loading all rows into Control nodes.

### Architecture / Systems
C14/C35/C36/C47/C48 and UI route/query contracts. One common profile per identity with context-dependent tab visibility.

### Domain Models
`PlayerSearchQuery`, `DraftHistoryQuery`, `JerseyNumberHistoryView`, `TeamHistoryQuery`, `SchoolAlumniQuery`, `PageCursor`, `NavigationEntry`, `ProfileTabAvailability` (read models).

Add `WatchlistEntry`, `PlayerComparisonView`, archive/progress views and presentation `PortraitRecord`/`PortraitContext`; no new gameplay entity for a favorite.

### Implementation Tasks
1. Add indexed pagination, stable sorting, combined origin/entry/registration/level/status plus rookie/draft-year/club/position/round filters and explicit missing-value ordering.
2. Complete Player Profile tabs and Team Profile overview/history/season/roster/records/awards/retired-number slots/news links.
3. Complete school panels, active/retired alumni and recent draft output, draft projections and results.
4. Audit every named-player/team surface for reusable links; preserve nested navigation and active-game decision ownership.

5. Persist user favorites for players/prospects scoped to career/branch IDs outside authoritative simulation; provide two-player comparison using the same C35 visibility projection, with NPC observed estimates and unavailable data explicitly labeled.
6. Bind Phase 23 archive filters/detail and milestone progress to Korean records/career routes; preserve live-game/back navigation.
7. Implement P §§4–7/26/31–34 Seed 2D, mode metadata and profile image resolution after discovery architecture is mature. Stable unique seeds plus saved renderer/part-set version reproduce faces without consuming gameplay RNG. Club/national/neutral variants change kit/context only. Initialize existing generated players once without rewriting baseball facts; subsequent creation/cohorts receive seeds. Keep supplied real-photo precedence and Seed/neutral fallback; no image-generation buttons falsely functional before Phase 26.
8. Link Team/Profile history to current and historical stadium details, dated home usage, manager tenures and captain history where useful. These are read-only identity/history routes; no stadium, staff or captain management controls.
9. Show C14 imported/generated draft source/result and C47 `현재 등번호`/`등번호 이력` in common Player/Team/Career routes. Team rosters indicate `사용 중인 등번호` and Team Profile links `영구결번`; no standalone number-management screen.
10. Add C48 read-only current/annual financial summaries to Team Profile and relevant Contract/League context. Explain total payroll, cap charge, threshold, excess status, acquisition budget, projected burden and realized sponsor revenue without exposing private AI scores or adding allocation/negotiation controls.

### UI Work
All listed screens at QHD/FHD, fixed headers, numeric alignment, filter restore, wide-table details, long names/text, loading/empty/error/missing-image/retry states. Record Game Archive and important profile/history query performance on the retained long-save dataset for D6.

Add 관심 선수, 선수 비교, 경기 기록 보관함, 기록 달성 현황 and shared portrait views within existing routes. Player/Team/Career views may show `현재 등번호`, `등번호 이력`, `드래프트 연도`, `지명 순번`, `출신 학교`, `홈구장`, `역대 구장`, `감독`, `주장`, `영구결번`, All-Star facts and read-only `구단 재정 현황` from retained read models. Seed/kit/profile integration follows P; no mandatory field-diagram portraits.

### Persistence Work
Read indexes/projections for draft provenance/results and C47 current/history as required, migrated explicitly; route preferences outside authoritative simulation. No copying hidden values into generic table data.

Watchlist/help/settings remain versioned preference data. Save cosmetic seeds/part-set version/mode and owned asset references; migrate deterministically with no gameplay RNG draw. Missing cached assets cannot prevent core save loading.

### Events
No new authoritative events; screen lifecycle/query notifications only.

### Tests
50-season-sized synthetic dataset, combination and imported-rookie/draft filters, duplicate names, multi-stint years/numbers, school alumni identities, finance summary/history with large KRW values and unavailable-estimate labels, nested player→team→player back navigation, stale async responses, every link source in UI inventory.

Compare self/NPC and NPC/NPC without hidden sort/tooltip truth; watchlist same-ID separate-career isolation; archive/progress navigation; Seed reproducibility, team transfer/national/neutral same face, missing kit neutral fallback, missing/corrupt AI asset Seed fallback and zero API calls.

### Acceptance Criteria
**Gate: G +** all required filters/tabs have a working data source; ten nested navigation steps restore state; reopening a profile does not alter game hash; no hidden field in table sorting/export/tooltip; all hidden FHD columns remain accessible through details.

All four QoL surfaces work from existing facts/public views; imported draft facts and jersey history appear without duplicate Player records or rewritten intervals; C48 finance views are read-only, reconcile to public assessments and grant no GM authority; every generated player has reproducible Seed data; changing portraits/favorites never changes canonical gameplay hash; P base/mode tests pass before Phase 26 image work.

### Deliverables
Completed discovery screens/components, indexed query suite, UI inventory evidence, `PHASE_24_REPORT.md`.

C44 QoL views/preferences and C45 Seed/profile integration, with P-referenced evidence rather than a duplicate portrait specification.

### Risks
N+1 SQL queries, UI data leakage via sort order, destroyed game scene when opening profiles, missed new-player origin filters.

### Documentation Updates
UI inventory, query contracts C35/C36, current state/report.

### Claude Code Handoff
> Finish the existing common profiles and discovery routes rather than creating one-off profile or jersey-management screens. Add imported-rookie/draft filters plus draft/C47 history views, and verify global links/restored state at both resolutions with a long-save dataset.

## Phase 25 — Retirement, franchise legacy, and Hall of Fame

### Goal
Prove a career ends with an accurate summary and distinct team/league legacy outcomes for both controlled and NPC players.

### Dependencies
Phase 24; approved D2 policy with the legacy eligibility/waiting-period/tuning subset specified before implementation.

### Scope
Retirement decision/farewell, complete career summary including club-specific jersey timeline, delayed team-specific number retirement enforced through C47, independent Hall cases, NPC candidates, captaincy contribution to franchise legacy and bounded future epilogue.

### Explicit Non-Goals
No post-retirement management/life gameplay, revealed raw scores, guaranteed honor from one stat, or one-team-only number retirement restriction.

### Architecture / Systems
C30–C32/C47 consume retained historical facts; timeline scheduler handles delayed announcements. Extends routine NPC exit facts from Phase 16.

### Domain Models
`RetirementPlan`, `FarewellEvent`, `FranchiseLegacyCase`, `NumberRetirementDecision`, `RetiredNumberRestriction`, `HallEligibility`, `HallCase`, `EpilogueFact`.

### Implementation Tasks
1. Resolve final season/game or immediate career-end paths and lock playable career commands while preserving history exploration.
2. Build summary from all recorded domains including income, commercial, military, relationships and role changes.
3. Evaluate each franchise independently using G's approximate 25/20/20/15/10/5/5 factor emphasis as the starting tuning profile, not a raw score exposed to users. Use that club's retained C47 assignments as number evidence. A new club-specific retirement blocks future assignment only; it preserves all past uses and imposes no league-wide restriction. Long, meaningful captaincy may contribute through existing team-role/loyalty/story evidence; it is not an automatic honor threshold or separate large score bonus.
4. Evaluate league Hall eligibility/selection independently for all retired players. Advance only bounded epilogue milestones, using committed outcomes and a documented cutoff; do not silently simulate an endless new game mode.

### UI Work
Farewell and final summary with jersey timeline; Hall listing/profile; team `영구결번` cards; epilogue navigation. Preserve exploration access to retired players and supporting history.

### Persistence Work
Retirement/legacy/Hall/announcement/epilogue rows with evaluated input/model versions; unique club-number policy linked to C47; keep pending delayed honors in saves without rewriting historical assignments.

### Events
`PlayerRetired`, `FarewellHeld`, `NumberRetirementConsidered`, `NumberRetired`, `HallCandidateAnnounced`, `PlayerInductedIntoHallOfFame`, `EpilogueCompleted`.

### Tests
Quiet/injury/star retirement, no honor, one/multiple-team honor, league legend without franchise honor, captain/noncaptain otherwise-similar careers, NPC Hall entry, delayed announcement save/load, repeated eligibility processing, club-specific retired-number rejection, historical assignment remains visible, and other clubs may still use the number.

### Acceptance Criteria
**Gate: G +** each outcome path has fixture evidence; no active gameplay command proceeds after career end; exact legacy/Hall scores stay private; multiple-team honors are possible; C47 rejects future assignment only for the honoring club while retaining prior timelines; summary totals reconcile and delayed facts survive reload without duplicate awards.

### Deliverables
Retirement/legacy services and screens, bounded epilogue specification/tests, `PHASE_25_REPORT.md`.

### Risks
Honors becoming one-stat thresholds, delayed awards lost on closure, epilogue scope expanding into a new game.

### Documentation Updates
`docs/retirement-legacy.md`, C30–C32, final-summary UI, current state/report.

### Claude Code Handoff
> Complete career closure and distinct franchise/Hall evaluation. Enforce club-specific retired numbers through C47 without rewriting historical assignments. Preserve NPC eligibility, delayed results and rare multiple-team number retirement. Never reveal raw scores or extend into a playable post-retirement mode.

## Phase 26 — Narrative AI and AI-native interaction

### Goal
Prove rich free-text interviews and connected expressive narrative work through a validated semantic bridge, while C# remains authoritative and failures preserve state; add optional Portrait AI only after the stable Seed base.

### Dependencies
Phases 21 and 25 (therefore the Phase 24 portrait base already passed). D5 is approved by R; record its implementation policy and evidence, not another request to reduce Gemini to clause rearrangement.

### Scope
Narrative package: Gemini provider, Korean free-text interviews/structured interpretation/consequence bridge, contextual fan and SNS/community voices, major-event articles, privacy-safe fact bundles, cache/version/budgets and retry states. Separate bounded portrait package: explicit user-selected single/batch Gemini images following P §§8–30/34. Both belong to this phase gate; no new top-level phase.

### Explicit Non-Goals
No Gemini baseball/team decisions or arbitrary numeric game-state mutations, no prose-to-command parser, no full-save uploads/secrets, no fake template-equivalent AI-native interaction, no automatic portrait generation or hardcoded permanent API prices.

### Architecture / Systems
Application fact builder, strict schema/whitelist interpretation validator and C43 Game Core consequence bridge; Infrastructure Gemini adapters (no direct domain mutators); C29 narrative and C45 portrait presentation. Treat provider output as untrusted input. Keep image and text capabilities/request queues distinguishable within existing layers.

### Domain Models
`PublicFactBundle`, `NarrativeBundle`, `InterviewAnswer`, `InterviewInterpretationEnvelope`, `ValidatedInterviewInterpretation`, `InterviewApplicationReceipt`, `FactFingerprint`, `PresentationCacheEntry`; P-defined portrait mode/status/seed/path/model/prompt metadata and per-player batch request mapping.

### Implementation Tasks
1. Route confirmed high-value events/interviews to expressive Korean narrative; ordinary low-value information uses templates with zero API calls. Bind actual stored past interviews by ID/text, and label opinion/speculation separately from factual assertions.
2. Pin supported Gemini API/model contracts at implementation time. Isolate secrets in protected local user configuration; persist prompt/model/schema/policy metadata, bounded request sizes/queues, cost/request controls, timeout/cancel/backoff and session-aware duplicate suppression.
3. Implement free-text question/answer input and preserve the exact submitted answer before interpretation. Define a finite schema for allowed target/topic/tone/intensity/sentiment/tags/fact references; reject unsupported fields, entities, invented quotes/references and arbitrary mutation numbers.
4. Through C43, revalidate interview/context/answer revision/eligibility, map validated meaning to core-owned consequence rules, and atomically persist effects + accepted interpretation + unique interview receipt. Enforce stale callback rejection, one final application per interview and deterministic replay from the recorded interpretation without Gemini.
5. Build a connected reporter/article/fan/SNS bundle from confirmed facts, actual player text and committed consequences. Different voices may express opinions, jokes/emotion and grounded speculation; validate facts/quotes/references, escape markup and cache accepted content. Prose cannot issue commands. Preserve answer/pending/retry state on API/validation failure; a failed reaction cannot rerun applied consequences.
6. After the narrative boundary and P base are stable, implement P's single-user-selected portrait request, then multi-select Batch where supported, per-player statuses/result mapping, local WebP caching, explicit regenerate/Seed restore and failed-only retry. Verify the selected Gemini image/Batch capabilities at implementation; unavailable provider support is an explicit incomplete image subgate, never automatic mass single requests. Confirm targets/count/model/estimated cost (or clearly unavailable estimate) before submission, with large-selection confirmation; no award/aging/selection/open/load triggers. Preserve prior valid image and seeds on partial failure; enforce save/branch isolation and safe immutable publication from §5.

### UI Work
Korean reporter question + free-text answer, submit/loading/pending/retry and preserved raw answer; no humble/confident/fan final-choice buttons. Inbox shows important unresolved interviews. News displays linked media/fan/SNS bundles and factual/opinion distinctions. Settings exposes real Narrative AI and Portrait AI capabilities now. Portrait single/batch selection, cost confirmation, per-player results and Seed restore follow P; accessibility/final styling is Phase 28.

### Persistence Work
Persist interview context/answers/accepted interpretation/core receipt with rule/schema versions in the career; cache generated content by career/branch/interview-answer/facts/model/prompt/locale. Store narrative bundle member status independently so retrying one failed member preserves others. Image assets and metadata follow §5/P with local save-scoped cache and immutable references. Keys never enter DB, snapshots, logs or exports.

### Events
Core emits `InterviewAnswerSubmitted`, `InterviewConsequencesApplied`, `InterviewCompleted` and owned consequence events only after validation/commit. `NarrativeReady`/`NarrativeFailed` and portrait progress/results are presentation notifications. Raw generated prose/image never emits authoritative facts.

### Tests
No key/offline/timeout/quota/malformed output causes no partial domain effect; unsupported tags/targets/mutation fields/invented factual references/past quotes rejected; stale callback or edited answer/wrong save rejected; duplicate submit/retry/reload/concurrent interpretation applies once; original Korean answer preserved; secrets and hidden fields absent; reopening cached content makes no unnecessary request; accepted-interpretation replay matches core facts offline. Test failed post-commit reactions independently. Portrait tests: zero automatic calls, explicit single/batch confirmation, partial batch (47 success/3 failure) failed-only retry, same PlayerId in two saves, corrupt/missing image Seed/neutral fallback, immutable prior image on failure and reopen/load without API.

### Acceptance Criteria
**Gate: G +** all narrative/interpretation and portrait subgates pass. At least one complete free-text interview produces validated core consequences and a connected multi-voice bundle; malformed/adversarial output cannot invent game facts or apply effects; one interview applies at most once. Core replay with the same recorded interpretation matches with networking disabled; unavailable AI-native interactions remain visibly pending/retryable without a pretend template result. Routine simulation/loading/saving stay safe. P Seed fallback and explicit-only cached portrait generation pass; unavailable provider support is INCOMPLETE, not silently dropped.

### Deliverables
Gemini adapters, finite interpretation schema/core mapping/receipt bridge, narrative bundles/cache/status UI, P-referenced optional portrait single/batch workflow, failure/authority evidence and `PHASE_26_REPORT.md`.

### Risks
Semantic misclassification, expressive prose containing unsupported facts, invented past quotes, duplicate or wrong-branch consequences, prompt injection through raw answers/imported text, runaway cost and partial image results. Validate strict boundaries; do not claim a schema or prompt proves semantic truth.

### Documentation Updates
`docs/narrative-system.md`, approved D5 implementation record, C29/C43/C45, privacy/secret configuration and tests; reference P without modifying its independent specification; current state/report.

### Claude Code Handoff
> Implement Phase 26 within the existing core/persistence boundaries: free-text interview → untrusted structured meaning → validation → core rule consequences → expressive cached reactions. Never parse prose as commands or invent fallback meanings. Prove idempotency/failure isolation. Deliver the separately bounded, explicitly user-selected Portrait AI package only after Phase 24 Seed tests, following P unchanged.

## Phase 27 — Complete datapack tools and content certification

### Goal
Prove the complete game runs with fictional content and imports supported personal data without affecting existing careers.

### Dependencies
Phase 26. Approved D3 fixes the personal KBO 2026 pack immediately before the regular season; Phase 27 must record the exact `as_of_date`, evidence cutoff, supplied sources and provenance before the personal-content gate can pass.

### Scope
Complete JSON schemas, individual-file/pack import, image overrides, optional imported historical draft classes/results and initial jersey numbers, generated future draft policy, selected rules including SalaryCapRules, optional club finance/sponsorship baseline assumptions, documented normalization/sample correction/manual overrides, final fictional pack and separate personal 2026 pack workflow.

### Explicit Non-Goals
No executable mods, full in-game data editor (future candidate), scraping unspecified datasets, mandatory personal real-data distribution, or live-save rating/roster replacement.

### Architecture / Systems
C14/C34/C47/C48 plus Assets; `tools/StarPlayer.PackTool` validates/prepares effective packs offline. Runtime imports finished ratings and optional finance baselines; it does not recompute old careers from new reality.

### Domain Models
`PackSchemaVersion`, `ImportProvenance`, `DraftClassSource`, `ImportedDraftRecord`, `InitialJerseyAssignment`, `ClubFinanceBaseline`, `SponsorshipPolicyInput`, `SalaryCapRuleDefinition`, `RatingEvidence`, `ManualOverride`, `HistoryCoverage`, `AssetReference`, `EffectivePackManifest`.

### Implementation Tasks
1. Complete schemas for all implemented families, including optional `draft_history`/equivalent and initial jersey assignments, optional defaults, clear unknown-field/version policies, asset paths and overlay replacement declarations. Imported years/classes are data; no Game Core year special case.
2. Implement pack-tool rating preparation using league-relative normalization, sample-size weighting and explicit scouting/manual correction; retain provenance and cutoff. A preseason initial rating may use only evidence available by the certified cutoff, never completed 2026 regular-season performance.
3. Validate the complete fictional pack and user's separately supplied 2026 data against IDs, dates, roster/contract/rule consistency and historical coverage.
4. Add data-management import/error UI and safe image fallback; prove original pack deletion does not corrupt an active career.

5. Certify catcher/profile/rule-area schema and migration defaults, pair-history provenance when supplied, and portrait photo/Seed/AI precedence plus save-owned asset references. Imported content cannot smuggle executable rules, unknown interpretation tags or secrets.
6. Validate historical draft PlayerId/player/club/origin/round/pick/date/signing/registration consistency, prohibited duplicate picks, imported/generated ID collision and initial-number active/retired conflicts. Optional absence is valid and routes the reached future year to normal saved generation; invalid data blocks the entire world publication with file/entity/field diagnostics.
7. Validate optional C48 club baseline/policy/sponsorship parameters and C40 SalaryCapRules references, effective dates, ranges and provenance. Fictional packs label fictional assumptions; unavailable real club budgets/sponsorship amounts use documented game-model estimates rather than fabricated official facts. Invalid finance data blocks world publication atomically.

### UI Work
Settings → Data Management → folder/file select → validation report → install; new-career pack choice; file/entity/field-level draft/jersey/finance/rule errors, optional history/asset/finance warnings, provenance labels and fallback previews.

### Persistence Work
Freeze effective world content, draft source/completed facts, initial C47 assignments, C48 baseline/policy provenance, C40 SalaryCapRules and history at creation; schema migration tests for pack upgrades; immutable asset identity/hash and saved references. A later cosmetic override is an explicit presentation operation, never a world re-import.

### Events
`DatapackInstalled` is application-level; `CareerCreated` records chosen provenance. Import failures emit no world mutation.

### Tests
Every schema family, optional draft-history/finance-baseline absence, imported one/multiple/fictitious completed classes, duplicate player/pick/ID collisions, missing player/drafting club/origin, impossible dates/round/pick, inconsistent signing/club/registration, invalid initial number/retired conflict, invalid finance ranges/effective dates/rule references/provenance, claimed-real unsourced amounts, broken references, bad enums/dates/ratings, traversal/symlinks outside allowed pack, oversized/corrupt images, malformed JSON, missing assets, same-named entities, differing old/new pack versions and cutoff overlap.

### Acceptance Criteria
**Gate: G +** fictional pack supports new/existing starts offline; optional historical drafts can cover arbitrary configured years/classes, completed imports cannot rerun, absent future classes generate normally, and validation errors block publication with source fields; optional finance baselines and SalaryCapRules validate with explicit fictional/estimated/verified provenance and no fabricated official KBO amounts; ratings preparation distinguishes low/high sample reliability and contains no post-cutoff 2026 evidence; all supplied 2026 rows pass or have explicit rejected-row reports. The approved preseason policy is not pending, but the 2026 content subgate remains INCOMPLETE until the exact `as_of_date`, evidence cutoff and supplied source data are recorded and certified; this does not invalidate the separately tested fictional-game gate.

### Deliverables
Complete schemas/validator/tool, fictional content, personal-pack build recipe and certification report, `PHASE_27_REPORT.md`.

### Risks
Silently inventing real-world data, importing future evidence into an earlier snapshot, inconsistent mod precedence, requiring external photos to load a save.

### Documentation Updates
`docs/datapacks.md`, rating methodology, schemas/source provenance, C34 and current state/report.

### Claude Code Handoff
> Certify the full fictional pack and supported import workflow, including optional completed draft history, initial C47 numbers and C48 finance baselines with C40 SalaryCapRules. Validate all references/conflicts atomically; never hardcode 2026, rerun imported drafts, fabricate real club finances or rewrite a running career. Report missing supplied data as an incomplete content gate.

## Phase 28 — Alpha visual system and complete UX

### Goal
Prove all implemented features form one understandable, consistent UI at the two supported resolutions.

### Dependencies
Phase 27 technical/fictional-content gate; track the personal-data subgate explicitly if still unavailable. All required gameplay feature gates must already pass.

### Scope
Final palette/typography/tokens, reusable card/table/button/tab/status components, all navigation/routes, settings/window modes, accessibility cues and complete error/empty/loading behavior.

Finalize the existing Phase 5–6 settings/help framework: capability visibility, readable Korean help/glossary, reset/suppression, accessibility scaling, implemented speed/default-mode/confirmation/autosave settings, and Phase 26 Narrative/Portrait AI configuration. Audio controls appear when Phase 29 audio actually exists.

### Explicit Non-Goals
No new gameplay scope, web UI, extra official resolutions, or missing feature concealed by final artwork.

### Architecture / Systems
Godot Themes, screen presenters and reusable components; UI architecture inventory is the acceptance checklist.

### Domain Models
Presentation-only `ThemeTokens`, `DisplayPreferences`, `NotificationPriority`, `ScreenState`; no new authority over game rules.

### Implementation Tasks
1. Apply a coherent Godot theme with readable type hierarchy, semantic color plus labels/icons, and team accent constraints.
2. Audit every screen/required tab against the inventory; complete navigation/focus/back/confirmation and busy/disabled states.
3. Implement QHD baseline and explicit FHD compact layouts using containers, details and tabs rather than shrinking all text.
4. Run scripted and observed journeys from both career starts through all major decision families, then fix prioritized usability defects.

### UI Work
All screens, long names, large numbers, Korean text, wrapped news, tables, field labels, popups, saves and data management; accessible focus and non-color status meaning.

Broadcast-panel audit (§1.12) at QHD/FHD: long Korean dialogue and player names, caster/analyst accessibility labels, focus/keyboard interaction, highlight-only vs full text volume, mute/off/hide, scrolling, archive revisit, responsive drawer layout and no overlap with required decisions. Styling must not change gameplay hashes.

LMV-A final-quality audit: field and icon readability, naturalness of ball movement, UI scale and QHD/FHD behavior, field/chat space allocation, no excessive animation, overlap or stutter, reachable speed and skip controls, and unchanged game hashes under any visual-effect setting. LMV-B is not a Phase 28 deliverable.

### Persistence Work
Versioned display/preferences file, migrations only if presentation metadata changes. No save fact rewrite for a theme change.

### Events
Presentation notifications only; no simulated RNG consumption.

### Tests
Every inventory route at QHD/FHD, window/borderless/fullscreen as supported in the selected desktop export, keyboard focus, text overflow, removed images, failed saves/DB/API and nested profiles during live games.

Inbox unresolved/expired/pending-AI states; first injury/second-team/FA/developmental and advanced-metric help; do-not-show/reset/reopen; scaled text/focus; no fake settings; mandatory autosave safety unchanged.

### Acceptance Criteria
**Gate: G +** all inventory rows have screenshots and workflow evidence at both resolutions; no clipped required action or unreachable information; all error/loading/empty states are usable; §6.3 query/input budgets pass; simulation hashes match the unthemed build for identical inputs; the broadcast panel and the LMV-A field pass the QHD/FHD audit with no overlap of required decisions.

Every player-facing label/help/reaction surface is Korean; complete settings expose only implemented capabilities and cannot bypass domain barriers or save-recovery requirements.

### Deliverables
Final theme/components, completed UX inventory, evidence set and `PHASE_28_REPORT.md`.

### Risks
FHD hiding gameplay access, giant universal components, excessive scene nodes for tables, and visual refresh introducing rule calculations.

### Documentation Updates
UI architecture/design tokens, screen inventory, current state/report.

### Claude Code Handoff
> Apply the final visual system only after feature gates pass. Use the complete screen inventory and both supported resolutions. Fix missing access and error states; do not add scope or move rules into UI code.

## Phase 29 — Polish, export, recovery, and final long-save validation

### Goal
Prove the complete personal-play build is stable, recoverable, responsive, and coherent across a full career and long league history.

### Dependencies
Phase 28; approved D6 preserve-first policy. Any automated pruning requires the Phase 17/29 measurement record and a separately explicit targeted policy. Outstanding required content gates remain visible.

### Scope
Transitions/sound/celebrations, final UI QA, three 50-season seeds, complete career-ending flows, upgrade/recovery matrix, exported desktop build and handoff documentation.

### Explicit Non-Goals
No Steam/Cloud/achievements, extra resolutions/platforms, multiplayer, foreign career, code mods, or portrait features beyond approved P.

### Architecture / Systems
Existing modules only; measured optimization in owning systems and optional retention maintenance as approved.

### Domain Models
No new gameplay model; final compatibility manifest and diagnostic reports.

### Implementation Tasks
1. Add nonblocking/skippable transitions, sound feedback and major-event celebrations; keep simulation independent of frame rate/animation.
2. Measure first under D6. If 20/50-season evidence demonstrates a material problem, implement only the explicit targeted retention policy for permitted reconstructible/cache/raw-trace/low-value-presentation data with migration, audit and integrity checks. Otherwise retain everything already preserved. Never prune authoritative facts, required statistical/archive evidence, major history, pinned content or recovery generations for convenience.
3. Run three 50-season world soaks plus controlled-player full-career stories, all schema upgrades, corrupt/newer-save handling and fault-injection matrix. Record working DB size, each save/snapshot family and total multiplication; save/load/backup/migration times; archive/profile/history query performance; table-family growth; and raw-pitch/narrative contributions.
4. Cold-launch exported build with fictional data and no network/editor; verify core flows, Seed portraits and safe AI-native pending/retry states. Exercise completed AI-native journeys separately with controlled provider fixtures and the supported integration probe. Record known limitations without calling unresolved gates complete.
5. Re-run final historical-integrity invariants for retained game StadiumId/ParkFactor versions, venue changes, staff/relationship turnover, captain assignments, annual salary obligations/history, All-Star vote/selection uniqueness, weather interruption/makeup chains and bounded TeamAtmosphere across the three 50-season seeds and migration matrix.
6. Re-run imported/generated draft and C47 invariants: completed imports never rerun, generated classes/results never reroll after save/load, PlayerIds never collide/duplicate on Existing Player Start, active club numbers never conflict, retired restrictions remain club-specific, every transition closes/opens once and full number/draft history survives all migrations.
7. Re-run C48 invariants across three 50-season seeds and every migration: payroll equals C13 obligations; no duplicate salary or sponsor settlement; cap/budget/sponsorship remain distinct and use saved versions; confirmed achievements alone trigger bonuses; later policy/rule changes do not rewrite old assessments; Club AI offers/trades stay sustainable; sponsor renewal/wealth concentration remain bounded and financially weak clubs can recover.

### UI Work
Final QHD/FHD audit of every inventory screen; long text/names/numbers, dense late-save tables, loading/retry, record/MVP/title/medal/farewell/legacy sequences and sound preferences.

### Persistence Work
Final migration compatibility matrix including draft source/completed markers and jersey intervals, backup/recovery documentation, D6 measurement record and no destructive cleanup of authoritative history; secrets absent from exported content and snapshots.

### Events
Existing facts trigger presentations; replaying animations cannot duplicate events or awards.

### Tests
Three ×50-season seeds, full-career save/resume to epilogue, §6.3 performance and complete D6 storage/snapshot/operation/query/table-family contribution measurements, all prior migrations, imported/generated draft stable-ID/no-rerun integrity, jersey assignment/retired-number timeline integrity, stadium/staff/captain/salary/All-Star/weather/atmosphere integrity, C48 payroll/cap/budget/sponsorship/AI-decision/historical reconciliation, exported offline play, publication crash points and API failures.

Offline export verifies core simulation/save/load plus visible pending AI-native interactions and Seed portrait fallback; it does not fabricate a completed AI interview or require online/offline prose equality.

### Acceptance Criteria
**Gate: G +** all soak invariants and performance budgets pass; actual D6 measurements are recorded before any pruning decision; retained history reconciles before/after any approved targeted maintenance, including locked draft provenance, stable PlayerIds, full club-number timelines/retired restrictions, original stadium/park context, staff/captain/salary/All-Star/weather chains and C48 financial assessments/settlements; TeamAtmosphere and financial divergence remain bounded; exported game loads and saves offline; zero unresolved critical save corruption/progression/privacy defects; every required source requirement has passing evidence or an owner-approved scope decision. Personal 2026 data absence cannot be mislabeled complete.

### Deliverables
Personal-play desktop build, final QA/soak/compatibility reports, final known-issues list, `PHASE_29_REPORT.md` and concise current state.

### Risks
Animation blocking decisions, late tuning invalidating earlier calibration, untested exported native dependencies, declaring completion despite incomplete content.

### Documentation Updates
Testing/compatibility/recovery docs, traceability with gate evidence, current state and final phase report.

### Claude Code Handoff
> Polish and certify the re-locked gameplay scope. Run the full exported-build, recovery and long-save gates, including C48 financial stability/history/idempotency, draft-source/stable-ID/no-rerun and C47 timeline/retired-number migrations. Do not add gameplay systems or label the project complete while required tests, source coverage, rule evidence or content gates are missing.

## 9. Implementation Handoff and Reporting

### 9.1 Session entry

Read `AGENTS.md`, `CURRENT_STATE.md`, the approved phase, and its referenced contracts. Read source sections when changing behavior, not all 9,000 source lines for every local edit. Work only within the approved phase; a local technical decision is allowed, a product change requires the issue/reason/options/impact record.

For Phase 0, the owner can hand Claude Code this instruction:

> Read AGENTS.md, CURRENT_STATE.md, ROADMAP.md §§1–6 and Phase 0. Implement Phase 0 only. Create the stated Godot 4.x .NET/C# solution boundaries, core test harness, empty Control shell, SQLite packaging/export probe, dependency pins and repeatable verification commands. Do not implement gameplay or career schemas. Run the Phase 0 gate, write docs/reports/PHASE_0_REPORT.md, update CURRENT_STATE.md, and stop for review.

### 9.2 Phase report format

```text
# Phase N Report
## Goal
## Completed
## Files Changed
## Tests
Command | tool/build version | fixture/seed | expected | actual | exit code
## Acceptance Criteria
Criterion | PASS / FAIL / INCOMPLETE | evidence path
## Design Decisions
## Known Issues
## Deferred Work
## Technical Debt
## Next Phase Preconditions
```

Include the overall gate and any decision IDs that block it. Deferred work is only permitted when the roadmap already places it later or the owner approves a change; it is not a device for making a failing gate pass. Store artifacts under `docs/testing/results/phase-N/` or a documented ignored local results directory when large, retaining a compact summary in the report.

### 9.3 Minimal documentation strategy

Keep this roadmap as the sequence and gate authority. Contracts own interface and state boundaries; UI Architecture owns routes/read models/layout behavior; source files own product requirements. Create subsystem implementation notes only when that subsystem begins, with references to these documents rather than copied requirements. A lightweight ADR records Decision, Context, Options, Chosen Option and Consequences for boundaries, save format, RNG compatibility and narrative integrity. Do not create an ADR for ordinary naming or a local helper.

## 10. Major Project Risks

Likelihood/impact are qualitative planning estimates, not measured failure rates.

| Risk | Likelihood | Impact | Mitigation | Validate in phases |
|---|---|---|---|---|
| Incorrect baseball scoring or termination | High | Critical | Pure reducer, independent rule oracles, accounting identities, inherited-runner/two-way fixtures | 2, 3, 11 |
| Simulation balance and dominant strategy | High | High | Conditional probability traces, seeded calibration, matchup matrix, versioned tuning | 3, 7, 17, 29 |
| Long-save runtime/memory/storage growth | High | High | Measured 20/50-season runs, indexed paging, explicit retention decision, no full-world UI hydration | 17, 23, 24, 29 |
| Save schema evolution and midgame compatibility | High | Critical | Version separation, migrations on copies, archived fixtures, model/RNG compatibility policy | 4 and every persisted phase |
| Player generation strength/population drift | High | High | Cohort/entry/exit accounting, school-independent potential support, multi-seed turnover audit | 12, 13, 16, 17 |
| Poor AI team decisions | High | High | Eligibility before ranking, scenario scorecards, explainable reasons, bounded market loops | 9, 15–17 |
| First/second/national/military assignment conflicts | High | Critical | Dated affiliation vs availability, one participant scheduling authority, shared workload/stat scopes | 10, 18, 19 |
| Cross-system coupling and ordering cycles | High | High | Single command owner, typed service contracts, staged calendar, events with idempotent projections | 4, 6, 14, 22 |
| Historical totals disagree or double count | High | High | Provenance/cutoff/coverage, retained stint counts, canonical team-season facts and rebuild tests | 11, 23, 25 |
| SQLite query volume/N+1 behavior | Medium | High | Bounded read models, batch joins, query plans and long-save page benchmarks | 6, 17, 24 |
| Godot Control complexity / FHD readability | High | High | Early two-resolution UX, containers, bounded table rows, common links/navigation and screen inventory | 5, 11, 24, 28 |
| Catcher/Battery Chemistry overcount or selection lock-in | High | High | Separate tools/C proficiency/pair familiarity; capped effective effects, actual shared exposure, fatigue/rest scenarios and replay | 1–3, 7–9, 17 |
| Venue/profile drift or weather rescheduling rewrites history or loses games | Medium | Critical | Persist resolved game venue/profile version, isolated weather RNG, original↔makeup links and schedule accounting invariants | 3, 6, 11, 17, 23, 29 |
| Staff/captain/atmosphere/All-Star/salary state diverges over long saves | Medium | High | Effective-dated assignments, bounded cause state, idempotent decisions/selections and 20/50-season audits | 9, 11, 14, 17, 21, 23, 25, 29 |
| Imported drafts rerun/duplicate players or generated classes reroll | Medium | Critical | Data-driven source/completed lock, stable IDs, atomic pack validation and persisted generation markers/results | 4, 5, 12, 13, 17, 27, 29 |
| Jersey history conflicts or retired numbers rewrite prior facts | Medium | High | Effective-dated C47 assignments, atomic interval closure, club-specific exclusion and migration/soak invariants | 1, 4, 9–10, 13, 15–17, 23–25, 29 |
| Club payroll/cap/sponsorship divergence or duplicate settlement | High | Critical | C13 single contract authority, C48 derived projections/unique settlement receipts, frozen C40 rules, transactional market checks and 20/50-season distribution/recovery audits | 9, 11, 14–17, 23–24, 27, 29 |
| AI interpretation misclassification or duplicate consequences | High | Critical | Finite semantic schema, core rule mapping, immutable raw answer, request/branch binding and atomic apply-once receipt | 21, 26, 29 |
| Gemini dependency, invented claims/quotes or cost | High | High | Confirmed fact/quote references, expressive-vs-factual boundary, cache/budgets, visible pending/retry and adversarial tests; no fake AI-native fallback | 21, 26 |
| Portrait batch/cross-save asset failure | Medium | High | Explicit target/cost confirmation, per-player mapping, immutable local assets, seed/neutral fallback and zero automatic calls | 24, 26–29 |
| Invisible decisions or unsafe preference overrides | Medium | High | One persisted barrier projected to Inbox; explicit expiry notices; capability settings preserve save minimums | 5–6, 14–19, 26, 28 |
| Lost/corrupt save publication | Medium | Critical | Single-writer transactions, backup API, immutable generations, fault injection and recoverable catalog | 4, 17, 29 |
| Hidden-data leakage through UI/news | Medium | High | Public DTO allowlists; exclude private sort keys; self/NPC/cheat serialization tests | 1, 5, 12, 24, 26 |
| Rule/version ambiguity or data cutoff leakage | High | High | Owner-approved profiles, explicit simulated vs historical coverage, no live-rule refresh | 2, 6, 13–19, 27 |
| Missing real-data inputs | Medium | Medium | Fictional runtime baseline, independent personal-pack subgate, source/cutoff provenance | 0, 27 |
| Scope creep after approved finance exception | High | High | Phase non-goals, source mapping, explicit exclusion list and §1.11 renewed lock; defer ideas unless dependency/contradiction/owner approval applies | Every phase |

## Approved Owner Decisions

D1–D6 are approved product-policy records. Detailed RuleProfile subsets and the exact Phase 27 personal-pack sources/date still require specification or certification in their owning phases, but they do not reopen these decisions. The records remain here as architecture authority and do not authorize production implementation.

### D1 — APPROVED: Plate-appearance interaction cadence

**Status:** APPROVED.

**Decision:** The Plate Appearance remains the primary player-interaction and orchestration unit. By default the player selects one PA approach, the Game Core resolves a real internal pitch/count sequence, and the UI displays that authoritative count. Default play does not require a choice on every pitch. An explicit action such as `Take` applies only to the next real pitch; if the PA continues, the game returns to an appropriate legal decision point.

**Rationale:** This preserves strategic pacing without inventing decorative counts or replacing the underlying baseball sequence with presentation fiction.

**Implementation Boundary:** GameReducer owns legal pitch/count state, the seeded PA resolver owns probability and internal sequencing, and Game Core exposes resumable decision boundaries. D1 creates no separate subsystem. Exact strategy labels and balance remain with the gameplay design and GameRules subset.

**Impact:** Phases 2, 3 and 5 implement and test real count legality, default PA-level input, one-pitch actions, deterministic RNG, resumable state and matching UI.

### D2 — APPROVED: KBO rule fidelity and versioning

**Status:** APPROVED.

**Decision:** Star Player uses a versioned 2026 KBO-oriented rule pack plus explicitly documented gameplay/implementation simplifications. Prefer real KBO rules where practical; never invent plausible rules or scatter constants through gameplay code. A career freezes the rule versions/content it needs, so later real-world changes affect only newly created careers that select a later pack. Unless an explicit RuleProfile exception exists, a club-retired number cannot be newly assigned by that club.

**Rationale:** A real baseline gives the career a stable identity while versioning and declared simplifications keep saves reproducible and honest about deviations.

**Implementation Boundary:** Each consuming phase specifies and tests only the detailed subset it needs before implementation: game legality before Phase 2; regular season before 6; roster/registration before 10; postseason/awards/All-Star/weather before 11; draft before 13; contract/annual renewal and salary-cap scope/calculation/consequences before 14; FA before 15; trade/foreign player before 16; international/military before 18–19; legacy/retired number/Hall before 25. These are RuleProfile specification tasks under approved D2, not new owner decisions unless a genuine product-policy ambiguity appears.

**Impact:** C40 remains the single versioned rule owner, saved careers remain frozen, simplifications are named by Rule Area and tested, and affected phase gates remain incomplete until their detailed subset exists.

### D3 — APPROVED: Personal KBO 2026 preseason snapshot

**Status:** APPROVED.

**Decision:** The personal KBO 2026 datapack represents the world immediately before the 2026 regular season. The regular-season opening is the simulation divergence point. Pre-cutoff clubs, players, roster/contract/history/status facts and already-completed 2026 rookie draft/entry facts may seed the world. Actual 2026 regular-season results are never forced into the career, and preseason initial ratings cannot use future regular-season evidence.

**Rationale:** A clear divergence point prevents future-data leakage and separates imported starting facts from save-specific simulation history.

**Implementation Boundary:** Phase 27 records and certifies the exact `as_of_date`, evidence cutoff, concrete supplied sources and provenance when the personal pack is built. Those are content dependencies, not a pending default-snapshot choice. Imported 2026 rookies remain selectable through Existing Player Start without rerunning or changing their completed draft.

**Impact:** Phases 4, 5, 13, 23 and 27 preserve the imported-before/simulated-after boundary. The fictional pack remains independently testable; the personal-content gate stays incomplete until its exact data and certification evidence exist.

### D4 — APPROVED: New-player entry and free-text characteristics

**Status:** APPROVED.

**Decision:** A normal full career for a newly created player begins through Draft / Entry from a supported background/origin, including valid undrafted or other approved branches. A created player does not normally select a KBO club and instantly become a professional. Existing Player Start remains separate for existing KBO players and imported already-drafted rookies; their completed history is not rerun. Free-text biography/characteristics are descriptive by default.

**Rationale:** The draft route preserves the intended uncertainty and keeps arbitrary prose from becoming hidden, uncontrolled gameplay modifiers.

**Implementation Boundary:** Prototype 2 may use clearly labeled fictional post-entry fixtures until Phase 13 supplies the production flow. Any mechanically meaningful starting personality must be a separately defined structured trait/choice with visible meaning, bounded effects, explicit ownership and tests. Phase 21/26 free-text interpretation never becomes a general gameplay-command language.

**Impact:** Phase 5 labels its shortcut and preserves Existing Player Start; Phase 13 owns the complete production Draft / Entry flow. Direct-team new-player start can return only through a future explicit owner-approved scope change.

### D5 — APPROVED: Generative-AI factual authority and expressive interaction

**Status:** APPROVED.

**Decision:** Game Core supplies confirmed facts and allowed context; Gemini provides rich expression and finite structured interview meaning; Application validates that interpretation; Game Core alone computes and commits authoritative consequences. Narrative prose never creates authoritative events. Preserve raw player answers and accepted meanings, reject unsupported tags/references/quotes, and keep failures safe, pending and retryable without partial mutation. AI-native interviews and fan/SNS content need no fake template equivalents.

**Rationale:** Factual authority and fictional opinion/expression are different. Rich voices may express emotion, humor and grounded speculation without creating injuries, contracts, trades, relationships, results, awards or past quotes.

**Implementation Boundary:** C43 is the sole inbound semantic bridge. Phase 21 owns interview triggers/context/consequence rules; Phase 26 owns free-text interpretation, validation, atomic apply-once receipts and expressive bundles. Deterministic replay consumes the saved validated meaning, never a new model reply. The former engine-clause-only/universal-fallback design remains superseded.

**Impact:** Narrative gates prove factual-reference protection, bounded semantics, stale-request rejection, idempotency, raw-answer retention and safe failure. This decision does not authorize production implementation or broader prose-to-command behavior.

### D6 — APPROVED: Preserve-first historical retention

**Status:** APPROVED.

**Decision:** Preserve history first and measure before pruning. Do not apply an automatic current-plus-previous-season limit to raw pitch traces. Permanently retain authoritative game/box-score/lineup/PA/scoring facts, statistics, team/stint, contract/salary, draft/entry/jersey, injury, award/milestone, FA/trade/transfer, national/military, major relationship/career/news, legacy/Hall/retired-number, pinned-content and reconciliation inputs. Initially retain raw pitch traces and ordinary generated narrative as well.

**Rationale:** Storage policy must follow actual long-save evidence. Premature deletion can break Game Archive, metrics, records, recovery and career history, while snapshot generations multiply the cost beyond the working database alone.

**Implementation Boundary:** Phase 17 and Phase 29 record SQLite/snapshot sizes, manual/rolling/pre-decision multiplication, save/load/backup/migration time, archive/profile/history query performance, table-family growth, and raw-pitch/narrative contributions. If results are acceptable, preserve all retained detail. If evidence shows a material problem, implement only an explicit tested policy for safe reconstructible caches, old raw traces or low-importance presentation while preserving every authoritative fact and required statistical/history input. Do not add retention settings without measured need or weaken save-recovery guarantees.

**Impact:** Phases 17, 23, 24 and 29 measure and validate retained history. Any Phase 29 pruning requires recorded evidence and a targeted migration/integrity gate; no authoritative evidence may be deleted for convenience.
