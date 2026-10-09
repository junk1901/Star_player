# Star Player Development Roadmap

Planning baseline: 2026-10-05 (source review began 2026-10-04). Status: **proposed architecture and implementation sequence; no game code has been implemented**. Primary implementer: Claude Code. Implement one owner-approved phase at a time and return its report for Codex review.

Read this document with [System Contracts](docs/SYSTEM_CONTRACTS.md), [UI Architecture](docs/UI_ARCHITECTURE.md), and [Requirements Traceability](docs/REQUIREMENTS_TRACEABILITY.md). These contain supporting detail, not competing product specifications. [CURRENT_STATE.md](CURRENT_STATE.md) identifies the next task; [AGENTS.md](AGENTS.md) defines the agent handoff rules.

## 1. Architecture Summary

### 1.1 Authority and scope

The direct task is [MASTER_REQUEST.txt](sources/MASTER_REQUEST.txt). The product sources are [Star_Player_game_v1.6.txt](sources/Star_Player_game_v1.6.txt), abbreviated **G**, and [UI_UX_GUIDE_v1.8.md](sources/UI_UX_GUIDE_v1.8.md), abbreviated **U**. Both were read completely. The differently spelled filenames in the master request refer to the supplied game-design file; they are not additional missing documents. U §49 contains old version references; its current v1.8 content and G v1.6 are the supplied baseline.

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

Unresolved product choices are listed at the end. No recommendation there is treated as approved. Phase 0 and Phase 1 can proceed without those answers.

## 2. Repository Structure

The delivered `outputs/` package is documentation, not a scaffold. At implementation kickoff, place its contents at the repository root. The current workspace is `E:/Madegame/Star_player`; its additional portrait-design document is excluded by the owner's explicit 2026-10-05 reply. Create only the directories needed by the approved phase.

```text
StarPlayer.sln
global.json                         # compatible SDK pinned in Phase 0
Directory.Build.props               # shared compiler settings
Directory.Packages.props            # exact package versions
AGENTS.md
CURRENT_STATE.md
ROADMAP.md
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
| Player | Identity, birth date, body, handedness, origin history, personality, career status | Club, contract, injuries, observations, statistics |
| Ratings / DevelopmentProfile | Current 0–100 skill vector; separate hidden per-skill growth envelope, growth/aging parameters | Training/exposure inputs; no UI access to hidden profile |
| PlayerState / Workload | Condition, fatigue, sharpness, stress, satisfaction, recent-form state; dated pitch/PA/field workload | Player and injury modifiers |
| Proficiency | Position and hitter/pitcher-role familiarity, learned pitches, transition history | Base skills; changing role does not erase them |
| Club / Roster | Club policy, managerial evaluation, domestic contract affiliation, dated registrations and assignments, team role | Competition squads, players, manager/staff characters |
| Competition / Season / Schedule | Rule version, stage dates, entries, fixture identity/status, standings source | Clubs, national squads, military squad |
| Game | Lineups, batting order, active defense, pitchers, inning/half/count/outs/bases, score, substitutions, decision boundary, RNG cursor | Players via resolved simulation snapshots |
| ScoredPlay / StatLine | Baseball facts and attributed increments; game-player-team-competition splits | Game and player IDs; counting facts are immutable after finalization |
| InjuryEpisode | Part, side, kind, severity, prognosis, recovery stage, relapse links and medical availability | Player; durable history survives recovery |
| Contract / Offer / FAEligibility | Terms, signing/expiry, incentives, no-trade clause, accrued service, offer state | Player, club, agent agreement |
| Draft / Prospect / AmateurTeam | Eligibility, order/picks, observation estimates, origin enrollment history, school reputation/output history | The same Player identity before and after entry |
| NationalSelection / TournamentEntry | Eligibility and squad membership, international results/medals | Domestic affiliation is preserved |
| MilitaryCareer | Status transitions, application/acceptance, service dates, return affiliation, sports-personnel classification | Eligibility rules, domestic contracts, temporary military squad |
| Character / Relationship | Person identity and player-centered directed relationship edges with category/reason | Player-linked NPCs or non-player family/agent/staff identities |
| Wallet / Ownership / Activity | Integer KRW ledger, owned assets, scheduled lifestyle/training/commercial time | Contract obligations, catalogs, player state effects |
| Story / PendingDecision | Eligibility/cooldowns, confirmed participants, legal choices, expiry, choice effects | Public narrative is a rendering of this fact structure |
| History / Legacy | Career/team/league facts, awards, milestone occurrence, retirement, number-retirement and Hall cases | Source records and immutable IDs |
| NewsItem | Fact references, importance, template text, optional generated presentation | Never authoritative outcomes |

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

### 3.3 Public contracts

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
RenderNarrative(PublicFactBundle, PresentationPolicy) -> NarrativeResult
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

A game reducer owns legal baseball transitions; a probability resolver chooses outcomes. Keeping them separate permits exact scoring tests independent of balance. The primary orchestration unit is a plate appearance; the internal count/pitch decision cadence is D1, not an implicit implementation choice. Non-PA plays such as steals or pickoffs are explicit scored transitions and do not fabricate a completed PA.

Before Phase 3, specify the resolver as ordered, bounded conditional probabilities: plate discipline/HBP/contact branch, batted-ball kind/quality, defense result, and advancement. Combine neutral league baselines with ability, matchup, state, proficiency, park, and selected strategy using documented monotonic modifiers. Normalize conditional distributions; record model and tuning versions. Reuse one resolver for watched, skipped, second-team, and international play. UI playback never consumes authoritative randomness.

Track runner identity, responsible pitcher, earned/unearned attribution, batter/runner advancement, and outs. Store the information needed for wins/losses/saves/holds and later advanced metrics from the first scored-game implementation. Do not calculate ERA from all runs or infer RBI from every team run.

### 4.2 Calendar and decision barriers

```text
Advance request
  -> resolve existing required decisions
  -> dated eligibility/contract/military/registration transitions
  -> planned training/life workload and recovery
  -> squad selection and fixtures in stable competition/game order
  -> player-relevant decisions; pause at a committed boundary
  -> other games using the same engine
  -> stats, trust, reputation, milestones, decisions
  -> end-of-day development / season-stage hooks
  -> stable commit, coalesced autosave, publish date
```

The persisted day cursor records completed stages and games. Advancing to a target date stops at the next unresolved player choice and returns its reason. It never silently accepts an offer, plays through an injury choice, or skips a national/military decision. Long jobs can cancel between committed stages. No single season-long transaction and no date clock that outruns incomplete games.

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

### 4.5 Narrative

```text
Committed domain facts -> importance + visibility filter
  -> ordinary: local template
  -> major: bounded optional Gemini request
  -> exceptional: bounded article/interview/reactions bundle
  -> validate references, numbers, schema, permitted expression
  -> accepted presentation cache OR fallback template
  -> escaped rich text with engine-created PlayerLink/TeamLink tokens
```

Backend interview choices and relationship effects exist before requesting prose. No generated text is parsed into a command. D5 addresses how strict fact-safe generated prose should be; schema compliance alone cannot guarantee semantic truth, as the [Gemini structured-output documentation](https://ai.google.dev/gemini-api/docs/structured-output) explains.

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
| `clubs`, `squads`, `registrations`, `assignments`, `manager_evaluations` | Dated affiliation, availability and role | Club/level/status; player/effective dates; no overlapping incompatible active assignments |
| `competitions`, `seasons`, `fixtures`, `game_states`, `game_players`, `scored_plays` | Fixtures and resumable game facts | Competition/season/date/status; game/play sequence; player/game |
| `game_batting`, `game_pitching`, `game_fielding`, `season_stats`, `career_stat_baselines` | Integer counting facts and imported baseline coverage | Player/competition/season/phase/team/stint/stat role; no double-counted total rows |
| `injuries`, `rehab_steps`, `workload_days`, `training_plans` | Episodes, exposure, plans | Player/date; active injury status |
| `contracts`, `contract_terms`, `offers`, `service_credit`, `trades`, `agent_agreements` | Terms, offers, accrual and transactions | Player/effective dates; team/expiry; unique accepted offer and obligation |
| `amateur_teams`, `prospect_enrollments`, `observations`, `drafts`, `draft_picks`, `entry_history` | Generated cohorts, scouting, outcomes | Cohort/year; team/year; unique draft/pick and draft/player |
| `national_selections`, `tournament_entries`, `medals`, `military_careers` | Eligibility and temporary participation | Tournament/player; player/status; dated service transitions |
| `characters`, `relationships`, `stories`, `pending_decisions` | Relevant people and choice state | Player/character/type; due date/status; unique applied choice |
| `ledger_entries`, `owned_assets`, `activities`, `commercial_contracts` | Money, ownership, schedule and commercial obligations | Player/date; unique source obligation/payment period; overlap queries |
| `history_facts`, `team_seasons`, `awards`, `milestones`, `retirements`, `retired_numbers`, `hall_cases` | Durable history and legacy outcomes | Subject/date; team/season/competition; unique award/season/category; team/number constraint |
| `domain_events`, `projection_receipts`, `news_items`, `news_links` | Integration facts and optional presentation | Event sequence; unique consumer receipt; subject/date/importance; fact fingerprint |

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

Datapack history imports carry source, coverage interval, scope, metric method, and completeness. Year-by-year history is preferred. A supplied career total becomes an opening baseline, not extra season rows counted again. Midseason imports require a cutoff and remaining schedule. Imported six championships plus two simulated championships must display eight; absent per-year evidence is flagged incomplete rather than fabricated. D3 fixes the 2026 snapshot boundary.

### 5.5 Long-save retention

Retain identities, season/team-stint counting stats, game results/box scores, injuries, contracts, drafts, awards, major events, schools/origins, and legacy outcomes for the entire career. Rebuildable aggregates are caches with a source revision and metric version.

Proposed retention of raw pitch traces and ordinary generated prose is D6. Until approved, do not delete authoritative history or advertise retention that has not been measured. A diagnostic harness may suppress optional pitch traces using a clearly marked fixture policy. Full PA/scoring evidence remains available through the validations that need it; seasonal aggregates alone cannot rebuild earned-run or WAR inputs after those facts are lost.

## 6. Testing Strategy

### 6.1 Evidence by layer

| Test class | Required evidence |
|---|---|
| Domain unit / transition | Boundary values, illegal operations, explicit rule fixtures and accounting identities |
| Deterministic simulation | Same version + state + seed + commands produces identical canonical facts/RNG cursors; UI mode and query order do not alter results |
| Statistical calibration | Seeded multi-game samples compared with declared fictional or approved league targets; confidence intervals and model version recorded |
| Application integration | Decision barriers, stale commands, idempotency, rollback-memory consistency, task cancellation and date ordering |
| Persistence | Real temporary SQLite DB; FK/check constraints, fault injection, snapshot recovery, migration and round-trip state hashes |
| Long-save | Cohort supply, league strength distributions, roster solvency, history consistency, size/query/runtime measurements over declared season counts |
| UI smoke / usability | Actual Godot scenes, both supported resolutions from Prototype 2; navigation restore, focus, loading/error/empty, hidden-data scans |
| Export | Cold-start desktop export without editor or user real-world pack; bundled SQLite and local saves work |

Tests use synthetic packs with known expected results, not private real-player data. Seed suites are fixed in version control. Replay checks compare sorted canonical business facts; exclude file timestamps, branch/session bookkeeping, image cache paths, and narrative text. Compare event type/payload/logical sequence rather than its branch-qualified transport identity. Deterministic entity IDs and RNG cursor allocation are part of the hash. Loading a save must not reseed simulation from its newly allocated branch ID.

Partition RNG streams by `(subsystem, season/game/player/cohort context)` using a fixed documented PRNG algorithm and stable key derivation. Persist state/draw counters. Never use shared `System.Random`, randomized string hashes, unordered dictionary iteration, wall time, thread scheduling, or Gemini output as simulation inputs. An unrelated UI query or extra news item must not change injuries, draft picks, or a game.

### 6.2 Mandatory game oracles

Fixture outcomes cover strikeout, walk, HBP, single/double/triple/HR, ground/fly/line outs, errors, double plays, sacrifices, force/tag outs, steal/caught stealing/pickoff, foul/count handling if D1 selects pitch progression, inning change, walk-off, home-team skip of final half, ties/extra innings by rule profile, legal substitutions/DH/two-way cases, inherited runners, earned runs, and pitcher decisions. Exact supported rule variants are locked in D2.

For every scored game, player/team runs reconcile, hits and errors reconcile, outs agree with defensive innings, PA/AB classifications reconcile, and pitching totals use outs. Two-way game counting cannot double count the same team's game while separate batting and pitching appearances remain correct.

### 6.3 Performance protocol

Record CPU, RAM, OS, storage, build mode, fixture size, retained-log policy, and seed. Run Release builds, one warm-up and five measured runs. Provisional engineering budgets: ordinary screen query p95 <=250 ms on the recorded developer PC; cold long-save list <=2 s; one background full domestic day <=2 s; foreground input acknowledgment <=100 ms. Exceeding a budget requires a report and targeted optimization before the relevant performance gate, not changing gameplay rules. These are project benchmark targets, not claims about untested hardware.

Use 100,000 PAs for resolver calibration, 100 complete games for reducer integration, ten seeds of one full season for Prototype 3, five seeds of 20 seasons for initial world turnover, and at least three seeds of 50 seasons for final soak. Long-world runs use an explicit harness policy that supplies player choices and permits world-only continuation after controlled-player retirement; this is a test tool, not post-retirement gameplay.

### 6.4 Common Gate G

Every phase's Acceptance Criteria invokes **G**. PASS requires all applicable items plus its own criteria:

1. Listed automated tests pass with commands, versions, seed/fixture IDs, counts, and exit codes recorded; a skipped required test means INCOMPLETE.
2. No regression in earlier phase tests; deterministic facts match where simulation is involved.
3. Changed durable state round-trips and migrates; fault cases preserve the last valid save. Before Phase 4, storage tests are explicitly not applicable.
4. Listed user workflows pass in actual scenes. Prototype 1 uses QHD; Phase 5 onward uses QHD and FHD. Hidden fields never appear in visible DTOs or serialized UI/narrative payloads.
5. Each acceptance row has PASS evidence. Required owner decisions are approved and recorded; a pending affected decision means INCOMPLETE.
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
  -> 25 Retirement/legacy -> 26 Gemini presentation
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
| 26 | 21, 25 | AI expresses already implemented facts and choices |
| 27 | 26 | Final schemas exist before complete import/content certification |
| 28 | 27 | Final visual system after feature and navigation stability |
| 29 | 28 | Polish and export/soak on the complete product |

Prototype 3 spans Phases 6–11: minimal player and team databases arrive in Phase 6, before its gate. The 10-club and second-team expansion is delayed until the two-club engine and player loop have passed. History facts are collected from Phase 2/4; Phase 23 adds richer aggregation, not the first record of history.

## 8. Development Phases

Each phase below specifies G plus phase-specific checks. UI tests from Phase 5 onward run at **2560 × 1440 and 1920 × 1080**. No phase may redefine a source requirement through a test fixture. A provisional balance/rule fixture is labeled as such and cannot certify real KBO compliance.

## Phase 0 — Reproducible desktop foundation

### Goal
Prove the fixed stack builds, tests outside Godot, and exports with working SQLite on the development PC.

### Dependencies
None. Read §§1–6 and contract conventions. Owner decisions D1–D6 do not block this phase.

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
Typed identities, player/base-rating/state/development/proficiency separation, club/squad references, baseball value types, explicit rule-profile contract, and public projections.

### Explicit Non-Goals
No growth rolls, live games, complete pack importer, database writes, or fixed creation-budget balance.

### Architecture / Systems
`Domain/Players`, `Domain/Baseball`, `Domain/Teams`; `Application/Queries/PlayerVisibilityPolicy`; contracts C01–C06.

### Domain Models
`Player`, `Ratings`, `DevelopmentProfile`, `PlayerState`, `RoleProficiency`, `OriginRecord`, `RegistrationStatus`, `LeagueAssignment`, `Money`, `InningsOuts`, `RuleProfile`, `PlayerPublicView`.

### Implementation Tasks
1. Implement the value objects/invariants from §3 with IDs distinct from names and registration distinct from level.
2. Define hitter six-skill and pitcher four-skill plus repertoire structures, each 0–100. Preserve birth date, handedness, body, and personality independently.
3. Implement session-bound visibility projections for self/NPC and observed estimates; exclude hidden profile fields by construction.
4. Build two fictional-club fixtures with legal batting/fielding roles, duplicate names, and one two-way player; keep all world content outside code.

### UI Work
Diagnostic player cards show self exact ratings, NPC estimated ratings, and separate state/proficiency fields at QHD. True potential never appears.

### Persistence Work
Define logical mapping names only. JSON test fixtures are versioned; no persistent career DB yet.

### Events
Define `PlayerCreated` and `PlayerChangedPosition` payload schemas; no simulated growth/event loop yet.

### Tests
Ratings reject <0/>100; age/date boundary tests; innings `112 -> 37.1`; same-name references remain distinct; self/NPC/cheat projection serialization contains no hidden fields. Returning to a known position preserves its proficiency.

### Acceptance Criteria
**Gate: G +** all four player scenarios serialize to legal public DTOs; independent fields round-trip in fixture serialization; an NPC cannot gain exact ratings by changing a query flag; no names used as join keys. Gameplay persistence clauses remain not applicable.

### Deliverables
Domain value types, visibility service, two-club fixtures, player-card probe and `PHASE_1_REPORT.md`.

### Risks
Single overloaded player-status enum; sorting by hidden values; accidentally clamping cheat ratings to a budget rather than the 0–100 skill scale.

### Documentation Updates
Contracts C01–C06; data dictionary notes in `docs/data-model.md`; current state and report.

### Claude Code Handoff
> Implement Phase 1 only using the Phase 0 boundaries. Keep every player dimension separate and make hidden-data exclusion testable. Do not simulate games, create SQL migrations, or select unresolved gameplay rules.

## Phase 2 — Baseball state transitions and scoring

### Goal
Prove a scripted sequence of baseball outcomes produces legal game state and reconciled statistics.

### Dependencies
Phase 1. D2 approval is required for the production competition rule profile; synthetic reducer fixtures may be developed independently.

### Scope
Lineups, batting order, innings/counts/outs/bases, substitutions, runner attribution, scored outcomes, rule-profile variants, official counting classifications.

### Explicit Non-Goals
No probability balance, full season, user career, dynamic club AI, or final game visuals.

### Architecture / Systems
`Simulation/Baseball/GameReducer`, `Domain/Baseball/ScoredPlay`, `Application/Projections/GameStats`; C07/C08/C11.

### Domain Models
`GameState`, `RunnerState`, `LineupSlot`, `PitcherUsage`, `ScoredPlay`, `GameStatDelta`, `GameRuleSnapshot`, `LegalActionSet`.

### Implementation Tasks
1. Specify reducer inputs/outputs and table-driven scoring oracles in `docs/game-engine.md`.
2. Implement all mandatory baseball transitions from §6.2, including non-PA plays, earned-run responsibility, legal roster/substitution constraints and two-way/DH rule cases.
3. Finalize game/box-score facts once; build stat deltas with source play IDs and inning outs.
4. Add a scripted-game runner and diagnostic field/scoreboard displaying only committed state.

### UI Work
QHD test-game selector, scoreboard, bases/outs, scripted Next Outcome, and box score. No free manager controls in the career UI; test runner actions are diagnostic only.

### Persistence Work
Define the resumable game-state and scored-play contract; fixture serialization only. No migration yet.

### Events
`PlateAppearanceCompleted`, `RunnerPlayCompleted`, `SubstitutionMade`, `GameCompleted`; typed scoring facts precede narrative text.

### Tests
Exact expected outcomes for every §6.2 case; zero/loaded-base combinations; invalid play rejected without partial mutation; game finalization repeated twice yields one result. Imported examples are not scoring oracles.

### Acceptance Criteria
**Gate: G +** 100 scripted complete games reconcile runs/hits/errors/outs and batting/pitching totals; no negative outs or duplicate occupied bases; each supported rule variant has a named fixture; D2 production profile recorded. No stochastic claim is made.

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
Phase 2; D1 interaction cadence and D2 rule profile approved before the affected engine contract is finalized.

### Scope
PA resolver, documented seeded RNG, probability inputs, simple legal AI strategy/substitution policy, complete games and basic player records.

### Explicit Non-Goals
No ten-club pack, long career, training growth, life systems, Gemini, or final design.

### Architecture / Systems
`Simulation/Randomness`, `Simulation/Baseball`, `tools/StarPlayer.SimHarness`; C08 and shared determinism contract.

### Domain Models
`SimulationVersion`, `RngStreamState`, `MatchupSnapshot`, `StrategyChoice`, `ProbabilityTrace`, `GameStep`.

### Implementation Tasks
1. Implement fixed PRNG/key derivation and golden vectors; isolate gameplay from presentation draws.
2. Implement documented conditional outcomes and bounded modifiers from §4.1; every input has a traceable effect.
3. Feed results into the existing reducer; auto-resolve AI situations with the same code path. Expose documented batter/pitcher choices.
4. Add the headless harness and calibration report against declared fixture targets, not arbitrary real-player statistics.

### UI Work
Temporary QHD game, choices, current mode, result, box score and individual summary. A compact field uses names/positions and visible runner evaluation without photos.

### Persistence Work
Versioned tuning JSON, seed/command replay fixtures, game-state serialization contract. No career save promise yet.

### Events
Existing scored-game events; `GameDecisionRequested`. Do not emit a “pitch event” for an invented display count.

### Tests
100,000 seeded PAs; 100 complete games; monotonic ability/strategy influence fixtures; identical-seed canonical replay; all outcome probabilities finite and normalized; neutral-strategy watched vs instant results identical.

### Acceptance Criteria
**Gate: G +** all games terminate legally; repeat runs match event and RNG hashes; aggregate rates fall within predeclared calibration intervals; no strategy dominates every relevant matchup in the declared test matrix. Prototype 1 proves simulation, not final KBO balance.

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
Minimal pack validator/import, first normalized save schema, transactional unit of work, game checkpoint, manual/multiple slots, autosave policy interface, Continue, migrations and recovery.

### Explicit Non-Goals
No full data editor, final real KBO pack, large career schema upfront, cloud saving, or automatic pack updates to saves.

### Architecture / Systems
Infrastructure Datapacks/Saves/Sqlite; Application Sessions/Ports; C33/C34.

### Domain Models
`PackManifest`, `ValidationIssue`, `SaveHeader`, `SaveSlot`, `SnapshotGeneration`, `CommandReceipt`, `MigrationRecord`, `DayCursor`.

### Implementation Tasks
1. Validate fixture packs and initialize a new temp career transaction; freeze rule/content provenance.
2. Implement explicit row mapping and §5 commit/snapshot protocol, command/event receipts, RNG state and allocator persistence.
3. Implement multiple careers/manual slots, rolling autosave generations, Continue scan and explicit Load.
4. Add sequential copy-before-migrate support and a sample old-schema fixture; simulate disk/commit/publication failure points.

### UI Work
Main menu, pack picker/errors, save-slot cards, busy indicator, failure/recovery notice. Save a paused test game and restore its decision screen.

### Persistence Work
Schema v1 plus one exercised upgrade path; manifest/schema JSON. Tables for current implemented state only; catalog rebuild and snapshot generations as §5.

### Events
`CareerCreated`; save lifecycle uses application notifications (`SaveCompleted`, `SaveFailed`), not gameplay events that alter world history.

### Tests
Round trip at pregame/midgame/postgame; fault injection before/after commit and publication; invalid IDs/references/dates/enums/ranges; missing optional asset fallback; latest-corrupt Continue; future-version rejection; old-save branch isolation.

### Acceptance Criteria
**Gate: G +** restored state and next 20 resolved actions match uninterrupted execution; invalid packs create no published career; every injected failure retains a loadable previous snapshot; two careers and three manual slots remain independent; modifying/deleting original pack does not change saved facts.

### Deliverables
Repositories, migration runner, validator, save/load scenes and fixtures, `PHASE_4_REPORT.md`.

### Risks
Memory/SQL divergence, copying live DB files, catalog becoming sole truth, duplicate command application after recovery.

### Documentation Updates
`docs/save-system.md`, data dictionary, C33/C34, save-format ADR, current state and report.

### Claude Code Handoff
> Implement the transactional save spine exactly as §5. Do not claim safe saves until fault-injection and midgame replay pass. Every added table has an explicit owner and migration path.

## Phase 5 — One-player career loop / Prototype 2

### Goal
Prove a player can understand and complete the home → choice → game → result → news → home loop, including saving and returning.

### Dependencies
Phase 4; D4 approved for the new-career starting path. Draft-based starts remain routed to Phase 13, not simulated by a fake assignment.

### Scope
New/existing player starts, fixed controlled identity, creation flow, personality, recommended-budget display, training intent, condition/fatigue, own batting/pitching and instant modes, template events.

### Explicit Non-Goals
No developed life economy, real growth curves, full draft, major league season, advanced news, or final visual theme.

### Architecture / Systems
Application career/game commands, public queries, UI shell/navigation; C01–C03/C08/C29.

### Domain Models
`CareerStartSpec`, `CreationBudgetGuide`, `ControlledPlayerContext`, `TrainingIntent`, `PendingDecision`, `PublicGameView`.

### Implementation Tasks
1. Implement six-step creation and existing-player search; include DOB/body/origin/handedness, pitcher form/repertoire and the D4-approved characteristic policy. Derive the hitter/pitcher archetype label from the allocated skill mix; it is not an extra ability bonus. Existing starts retain all imported facts except owner-editable personality.
2. Apply approved start placement; normal points are advisory, shared across two-way skills, while cheat has no over-budget warning and never reveals potential. Body size may provide only the bounded tendencies allowed by the source, never a large automatic skill bonus or one universally optimal physique.
3. Add minimal exposure/recovery state updates and training-direction selection; long-term growth is Phase 7.
4. Implement own-PA/own-pitching/instant presentation using one engine, bench progression, persisted decisions and small template news.

### UI Work
Home, My Player, Training, Game, Result, basic News/Event, menu shell and minimal overlay player card. Build compact QHD/FHD variants now; preserve active game when inspecting a name.

### Persistence Work
Add creation settings, controlled player, personality, plans, pending choices and navigation preferences. Autosave postgame and pre/post major career decisions; migration fixture required.

### Events
`CareerStarted`, `TrainingPlanChanged`, `PlayerStateChanged`, `DecisionResolved`; existing game events.

### Tests
Both start modes; hitter/pitcher/two-way/bench flows; budget above recommendation allowed; no potential in cheat; repeated choice rejected; save-resume at a decision; offline template completion.

### Acceptance Criteria
**Gate: G +** five consecutive games and an off-day complete for each role at both resolutions; switching mode changes no outcomes with identical actions; an observer identifies inning/score/outs/runners in <=3 seconds for 9 of 10 prepared situations; next action is reachable without losing game state. Log usability misses as failed criteria to correct.

### Deliverables
Playable Prototype 2, creation/existing selection, shell/scenes, paired screenshots and workflow evidence, `PHASE_5_REPORT.md`.

### Risks
Prototype shortcuts becoming permanent roster rules; UI inventing growth or automatically choosing important actions.

### Documentation Updates
UI routes and state contract, creation policy/config, current state and report.

### Claude Code Handoff
> Deliver the one-player loop at QHD and FHD. Keep missing later systems explicitly unavailable, and keep early training intent distinct from implemented growth. Do not hard-cap normal creation or modify imported existing-player facts.

## Phase 6 — Calendar, regular season, and basic discovery

### Goal
Prove a repeatable sequence of games/rest days progresses all participating clubs and provides trustworthy current-season information.

### Dependencies
Phase 5.

### Scope
World clock/stage cursor, schedule import/generation, date advance barriers, current standings, minimal Player DB/common profiles and Team Profile.

### Explicit Non-Goals
No draft, postseason results, long-world stability claim, full historical profile tabs, or life calendar simulation.

### Architecture / Systems
Application date coordinator; Simulation World; schedule/season/query systems C09–C11/C35/C36.

### Domain Models
`WorldDate`, `Fixture`, `FixtureStatus`, `SeasonStage`, `AdvanceRequest`, `AdvanceStopReason`, `StandingsRow`, `ProfileRouteState`.

### Implementation Tasks
1. Implement stable fixture order and persisted stage cursor; enforce no duplicate participation and valid home/away totals.
2. Advance through all games, stop at player decisions, resume after cancellation/reload without duplicate results.
3. Project standings and separated season/career summaries from scored facts.
4. Add minimal database search/filter/profile and team current-roster/rank screens; preserve filter/sort/scroll/season state.

### UI Work
Schedule, Team, League standings, Player DB and common Player/Team Profiles, status-bar next activity. Missing future tabs show truthful empty/unavailable states.

### Persistence Work
Competition/season/fixture/stage tables and indexed queries; preserve played fixtures when loading; migration from Prototype 2 starts a declared new fixture schedule without rewriting past games.

### Events
`DateAdvanced`, `FixtureScheduled`, `SeasonStageChanged`; existing game events feed standings/history.

### Tests
30-day fixture with off-days, competing same-day tasks, cancelled advance, stale query and load replacement; schedule constraints and standings recomputation; name duplicates/search escaping; UI back-stack restore.

### Acceptance Criteria
**Gate: G +** ten seeds of the 30-day fixture finish with all scheduled games accounted for; loaded continuation matches uninterrupted facts; required decisions stop advancement; minimal profiles are reachable from standings, roster, games and records at both resolutions.

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

### UI Work
Training focus/intensity and predicted fatigue/risk categories, position/proficiency tabs, both role summaries and growth history.

### Persistence Work
Hidden profiles, ability/proficiency changes, learned pitches and exposure checkpoints; migration initializes missing profiles with versioned deterministic seeds, never from public estimate as if it were truth.

### Events
`PlayerDeveloped`, `PlayerAged`, `PlayerChangedPosition`, `PlayerRoleChanged`, `PitchLearned`, `TrainingPlanChanged`.

### Tests
Fixed-seed cohort curves; same history reproducible; high-potential failure possible; small sample hot streak does not directly boost base ratings; two-way time sums <=100%; returning position preserved; extreme allowed ratings remain bounded.

### Acceptance Criteria
**Gate: G +** 1,000 synthetic careers include every configured growth class; no single-game rating jump outside the documented update policy; 1,000 extra UI queries do not change growth hashes; role changes and learned pitches survive save/load.

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

### UI Work
Priority injury popup, episode details, recovery stages, active injury on Home, injury-history profile tab.

### Persistence Work
Episode/stage/history tables, persisted pending choices and recovery cursors; closed injuries remain queryable.

### Events
`PlayerInjured`, `InjuryWorsened`, `RehabilitationStarted`, `PlayerClearedForRehab`, `PlayerReturnedFromInjury`.

### Tests
All severity transitions, stacked injuries, repeated same-part episodes, unavailable player excluded, minor injury base ratings unchanged, serious-injury effect recorded once, restore before a play/rest choice.

### Acceptance Criteria
**Gate: G +** each severity has a completed fixture; rest vs play-through changes configured risk in a seeded cohort; no repeated permanent penalty after reload; “cleared for rehab” cannot be displayed as “returned to first team.”

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
Lineup/rotation/bullpen policies, role evaluation, managerial trust, playing opportunity, position competition and promotion/demotion proposals.

### Explicit Non-Goals
No playable team operations, FA/trade AI, friend-based guaranteed selection, or automatic call-up based on one stat threshold.

### Architecture / Systems
Simulation ClubAI, Roster invariants C06/C07; observations/public reasons are separate from internal scores.

### Domain Models
`ClubPolicy`, `SelectionContext`, `RosterProposal`, `ManagerEvaluation`, `TeamRole`, `RotationPlan`, `SelectionReason`.

### Implementation Tasks
1. Implement availability filtering, positional coverage and eligibility before candidate ranking.
2. Rank using ability, proficiency, recent form, health, workload, trust, team need and development policy; deterministic tie-break by stable ID or named RNG stream.
3. Select legal lineups/rotation/bullpen and issue assignment proposals; retain final domain validation.
4. Publish human-readable role/competition reasons without exposing hidden evaluation numbers.

### UI Work
Team roster, position competition, expected role, rotation, trust category and selection/demotion notices.

### Persistence Work
Club policy, trust/role history, rotation/workload decisions; registrations remain dated and independently validated.

### Events
`TeamRoleChanged`, `ManagerEvaluationChanged`, `LineupSelected`, `RosterMoveProposed`.

### Tests
Injured/ineligible candidates, all-catcher/all-pitcher shortages, tired bullpen, two-way conflicts, same skill with different proficiency, high second-team performance with blocked first-team position.

### Acceptance Criteria
**Gate: G +** 1,000 seeded selection contexts yield a legal selection or a typed actionable shortage, never an illegal lineup; private friendship cannot overwrite trust; identical context yields the same decision/reasons; no user can submit another player's lineup command.

### Deliverables
Club policies, roster evaluator, team screen, scenario scorecards and `PHASE_9_REPORT.md`.

### Risks
Hidden AI score leaks, permanently overworked pitchers, and fallback logic that conjures players instead of reporting a supply defect.

### Documentation Updates
`docs/roster-system.md`, club AI decision specification, C06/C07 and current state/report.

### Claude Code Handoff
> Implement explainable legal selection and role evaluation, not a club-management mode. Test shortages explicitly; do not satisfy roster tests by creating untracked emergency players.

## Phase 10 — Ten-club world and real second-team play

### Goal
Prove first and second teams run actual games with shared players, separate records, and coherent call-up, demotion, and rehabilitation.

### Dependencies
Phase 9; approved D2 registration/second-team rules.

### Scope
Ten fictional KBO-style clubs, affiliated first/second squads, schedules, AI usage, developmental registration, roster moves and rehab games.

### Explicit Non-Goals
No foreign professional leagues, draft generation, military squad yet, or final ten-club real-data pack.

### Architecture / Systems
Competition/squad model plus C07/C17. Use the same game, development and injury services for both levels.

### Domain Models
`SquadEntry`, `AssignmentInterval`, `CallUpDecision`, `DemotionDecision`, `RehabAppearance`, `RegistrationConversion`.

### Implementation Tasks
1. Expand fictional pack with adequate legal depth and configured affiliation relationships, not a hardcoded assumption of identical competition sizes.
2. Run both competitions from one date coordinator with mutually compatible assignments.
3. Commit call-ups/demotions and registration changes atomically; enforce wait periods/quotas from the approved rule profile.
4. Execute rehab appearances and medical reassessment; prevent a good rehab line automatically triggering promotion.

### UI Work
Level/status badge everywhere; shared game/records screens; separate first/second stat tabs; roster filters for registered/developmental/injured; call-up/demotion/return notices.

### Persistence Work
Competition-level/squad indexes; assignment intervals, rehab appearances and first-call-up/debut history; migrate prior first-team stats with explicit competition IDs.

### Events
`PlayerCalledUp`, `PlayerDemoted`, `PlayerRegistered`, `RehabAppearanceCompleted`, `PlayerMadeLeagueDebut`.

### Tests
60-day ten-club fixture with all level moves, developmental first-team prohibition, two-way workload, injury return, concurrent fixtures and separate stat totals; one player cannot play incompatible same-time games.

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
Phase 10; D2 season/tiebreak/postseason/award eligibility definitions.

### Scope
Offseason, camp, exhibition, regular season halves, All-Star, postseason/Korean Series, awards and rollover; season UX stabilization.

### Explicit Non-Goals
No claim of sustainable multi-decade rosters before feeder/contracts, no fabricated future draft/FA results, or final visual theme.

### Architecture / Systems
C09/C10/C11/C27/C28; season stage handlers invoke existing systems, not a second simulator.

### Domain Models
`PostseasonBracket`, `SeriesResult`, `AwardDefinition`, `AwardResult`, `SeasonClosure`, `TeamSeasonRecord`.

### Implementation Tasks
1. Implement configured stage transitions and schedule rules, including standings ties and postponed/cancelled fixture handling as specified by the profile.
2. Select/play All-Star and postseason games with separate statistical scope.
3. Compute approved awards/titles and finalize team-season facts once.
4. Close the season transactionally and create next-season metadata; expose unimplemented offseason systems as pending phase scope, not fake outcomes.
5. Complete important-situations and entire-game presentation modes using the shared engine; mode changes alter pause/display policy only.

### UI Work
Season calendar/stage, All-Star and awards, bracket/series, standings, season/career summaries and basic historical team seasons. All five game modes are available and labeled. Core menu arrangement stabilizes.

### Persistence Work
Season/series/awards/team-season tables; durable championship, runner-up, regular first-place and postseason participation facts; autosave stage boundaries.

### Events
`AllStarSelected`, `TeamClinchedPostseason`, `SeriesCompleted`, `TeamWonChampionship`, `PlayerWonAward`, `SeasonClosed`, `SeasonOpened`.

### Tests
Ten seeds of one full season; tiebreak and series fixtures; advance/load at each stage; second-team season closure; championship/award idempotency; team win/loss accounting; all five modes reproduce the same facts with identical strategies.

### Acceptance Criteria
**Gate: G +** each stage reachable at both resolutions; every fixture/award/history row reconciles; imported six titles plus two synthetic completed title seasons displays eight in a targeted fixture; no hidden ratings exposed in award competition screens. Prototype 3 passes only after Phases 6–11 all pass.

### Deliverables
Season lifecycle, bracket/award screens, full-season reports, `PHASE_11_REPORT.md`.

### Risks
Double counting postseason stats; offseason loops silently skipping unavailable market systems; sample-only awards becoming a final unapproved rule set.

### Documentation Updates
League/history contracts, season testing baseline, UI navigation freeze notes, current state/report.

### Claude Code Handoff
> Complete the season lifecycle and Prototype 3 UX. Record completed seasons and awards once. Do not claim long-world readiness until player intake, contracts, and exits are implemented.

## Phase 12 — Feeder cohorts and uncertain scouting

### Goal
Prove each future year can supply distinct prospects with persistent origins and imperfect public evaluations.

### Dependencies
Phase 11.

### Scope
High schools/universities/overseas backgrounds/other paths, fictional future cohorts, school reputation and output history, observation-based scouting and prospect rankings.

### Explicit Non-Goals
No full amateur or foreign-league seasons, school management, actual draft picks, or guaranteed potential by school.

### Architecture / Systems
C15/C16; generated prospects use existing Player, growth, state and history models.

### Domain Models
`AmateurTeam`, `ProspectCohort`, `EnrollmentHistory`, `ScoutingObservation`, `EvaluatorProfile`, `ProspectEstimate`, `DraftProjection`.

### Implementation Tasks
1. Import seed amateur/background organizations into the save and define versioned cohort distributions.
2. Generate IDs, background, age/body/role/current skills, hidden development profile and synthetic amateur summaries with independent streams.
3. Produce evaluator-specific uncertain grades/ranges; build public rankings from estimates/observations, not a sorted true-potential list.
4. Record school output and slowly update reputation with bounded per-year changes; do not rerun previous cohorts on load.

### UI Work
League → Newcomers/Draft pool, origin filter, prospect version of common profile, basic school panel and linked origin chain.

### Persistence Work
Amateur organizations, cohort marker, observations, enrollment history and school-output facts; prospective initialization for older saves without rewriting known origins.

### Events
`AmateurPlayerGenerated`, `ScoutingReportUpdated`, `ProspectCohortPublished`, `SchoolReputationChanged`.

### Tests
20 generated cohorts across fixed seeds; duplicate-ID prevention; unknown-school elite and famous-school ordinary fixtures; evaluator disagreement and inaccurate ranges; save after cohort generation and repeat command.

### Acceptance Criteria
**Gate: G +** every cohort player has a unique ID and valid origin; cohort replay hashes match; all approved origin paths occur in the declared sample; no school hard-locks potential; changing installed school data does not alter existing save output history.

### Deliverables
Feeder/scouting services, fictional amateur data, pool/profile UI and cohort diagnostics, `PHASE_12_REPORT.md`.

### Risks
Unbounded generation, demographics drift, high-school and university identities duplicated instead of preserving a player's life path.

### Documentation Updates
`docs/draft-system.md`, feeder distributions, C15/C16 and current state/report.

### Claude Code Handoff
> Generate fictional future prospects on existing Player identities and saved origin histories. Implement imperfect scouting. Do not build amateur leagues or equate school reputation with true potential.

## Phase 13 — Draft and professional entry paths

### Goal
Prove prospects enter actual club rosters, or retain coherent undrafted paths, without losing identity or origin.

### Dependencies
Phase 12; D2 draft/entry rules and D4 career-start choice approved.

### Scope
Eligibility/order/picks, AI prospect evaluation, controlled new-player draft flow, fixed rookie entry terms and acceptance, undrafted university/developmental/other/end paths, conversion/release history.

### Explicit Non-Goals
No player-selected draft outcome, full contract negotiation engine, separate developmental-player database, or external league simulation.

### Architecture / Systems
C14–C17; minimal contract activation owned by C13 is introduced here for entry; Phase 14 extends it to negotiation/payment.

### Domain Models
`DraftEligibility`, `DraftOrder`, `DraftPick`, `EntryOffer`, `EntryDecision`, `RegistrationConversion`, `ExternalCareerState`.

### Implementation Tasks
1. Compute draft order from approved saved rules and standings; validate no duplicate eligible players or picks.
2. Evaluate club need/observations/allowed internal factors and commit picks in stable order.
3. Accept fixed rule-defined rookie entry terms, preserve player authority where a signature is needed, and activate real affiliation/roster rows.
4. Resolve undrafted paths; university return preserves player ID and prevents duplicate cohort generation. Support developmental offers, later registration conversion and release.

### UI Work
Draft preview/results, pending own entry choices, original origin plus entry/status/level, conversion events and linked timeline. Prototype 2 new-career fixture placement is replaced by the approved full flow.

### Persistence Work
Draft/order/pick/entry-contract/decision tables; unique draft/player/pick; conversion/release dates; atomic roster+contract+history updates.

### Events
`PlayerEnteredDraft`, `PlayerDrafted`, `PlayerWentUndrafted`, `PlayerSignedAsDevelopmental`, `PlayerRegistered`, `PlayerEnteredUniversity`, `PlayerLeftBaseball`.

### Tests
Complete draft, depleted pool, pick retry, undrafted branches, own-player decision stop, university re-entry, developmental conversion, high-potential undrafted fixture and failed-signing rollback.

### Acceptance Criteria
**Gate: G +** all drafted/signing players appear in the correct actual roster; no player drafted twice; every undrafted player has one valid next status; all branch paths are exercised; origin and entry remain separately visible after save/load.

### Deliverables
Draft/entry services and screens, updated new-career flow, branch tests, `PHASE_13_REPORT.md`.

### Risks
Implicit guaranteed signing, annual duplication of university prospects, and conversion accidentally changing true potential.

### Documentation Updates
Draft/roster/contracts, approved starting flow, C14–C17, current state/report.

### Claude Code Handoff
> Implement actual draft-to-roster entry and all specified undrafted outcomes. Use minimal fixed rookie terms through the Contract owner; leave general negotiation to Phase 14. Never assign draft success directly from a UI choice.

## Phase 14 — Contracts, agents, and earnings ledger

### Goal
Prove player-approved domestic contracts create enforceable terms and exactly-once career income.

### Dependencies
Phase 13; D2 contract/service-credit/incentive rules.

### Scope
Salary/length/signing bonus/options, offers/counteroffers/expiry, agent types/fees/advice, service accrual, basic career earnings ledger.

### Explicit Non-Goals
No FA competition yet, purchases/lifestyle store, commercial contracts, or agent deciding for the controlled player.

### Architecture / Systems
C13/C25/C37; one contract service owns activation; ledger entries consume validated obligations.

### Domain Models
`ContractTerms`, `ContractOffer`, `NegotiationRound`, `Incentive`, `ServiceCredit`, `AgentAgreement`, `PaymentObligation`, `LedgerEntry`.

### Implementation Tasks
1. Extend entry contracts with explicit effective/expiry dates, guarantees/options/no-trade clause and documented currency rounding.
2. Implement bounded negotiation rounds, expiry and final user acceptance; agents propose comparisons and advice.
3. Accrue service under rule data and schedule salary/bonus/incentive/commission payments with unique obligation IDs.
4. Expose current terms and earnings summaries; correctly migrate existing entry/imported contracts.

### UI Work
Contract and offer comparison, agent selection/type/fees, final confirmation, service-to-FA status, basic income tab.

### Persistence Work
Contract terms, offers, negotiation state, service ledger, agent agreement and payment ledger. Store rule version used for accrual and award/incentive evidence.

### Events
`ContractOfferReceived`, `PlayerSignedContract`, `ContractExpired`, `AgentHired`, `AgentAgreementEnded`, `IncomePaid`, `CommissionPaid`.

### Tests
Expired/stale offers, repeated acceptance, multi-year salary, team changes, incentive boundaries, fee rounding, insufficient club policy budget, save on either side of a payment.

### Acceptance Criteria
**Gate: G +** yearly payment totals equal terms less explicit fees; replaying a payment causes no second credit; overlapping prohibited club contracts are rejected; the agent cannot finalize the user's contract; comparison data matches signed terms.

### Deliverables
Contract/agent/payment services, contract screens, ledger fixtures, `PHASE_14_REPORT.md`.

### Risks
Confusing offer total with annual pay, service accrued twice, and money rounding drift over decades.

### Documentation Updates
`docs/contract-system.md`, ledger ownership, C13/C25/C37, current state/report.

### Claude Code Handoff
> Implement domestic negotiation, agent advice and auditable earnings. Final choices belong to the player. Every payment and commission needs a unique obligation key and a replay-safe test.

## Phase 15 — FA eligibility and multi-club offers

### Goal
Prove eligible players can enter a domestic FA market and select meaningful offers without the game choosing for them.

### Dependencies
Phase 14; D2 FA eligibility/compensation/market rules.

### Scope
FA declaration/window, AI offers, role/opportunity/team competitiveness/loyalty preferences, own-player final selection and NPC market resolution.

### Explicit Non-Goals
No foreign FA market, posting, trade deals, or detailed club finance simulator.

### Architecture / Systems
C18 with Contract/Roster services; team budgets are bounded policy inputs, not a management game.

### Domain Models
`FAEligibilitySnapshot`, `FADeclaration`, `MarketWindow`, `FAOfferComparison`, `ClubDemand`, `CompensationObligation` where the approved rules require it.

### Implementation Tasks
1. Compute eligibility from saved service and rule versions; expose reasons and remaining service.
2. Generate legal offers with documented club demand/budget/role policies.
3. Pause for controlled-player decisions; resolve NPC preferences with saved seeds and deterministic tie-breaks.
4. Activate chosen contract and all required roster/compensation effects in one unit of work.

### UI Work
FA eligibility/declaration and comparison cards showing money, length, role, competition, loyalty and agent advice; pending offer deadline indicator.

### Persistence Work
Market window/declaration/offer and compensation rows; keep declined/expired facts needed for career history; migration preserves prior service totals.

### Events
`PlayerDeclaredFA`, `FAOfferReceived`, `FAOfferDeclined`, `FAAgreementReached`; Contract emits actual signing.

### Tests
Ineligible declaration, exact service threshold, multiple simultaneous offers, deadline, no-offer result, older offer after another signing, compensation failure rollback, NPC deterministic market.

### Acceptance Criteria
**Gate: G +** one player cannot accept two offers; career/roster/contract show the same chosen team after reload; legal no-offer/unresolved status is supported; preference advice never locks the controlled player's team choice.

### Deliverables
FA policy/market/UI, threshold and market fixtures, `PHASE_15_REPORT.md`.

### Risks
Scope creep into GM simulation, fabricated guarantees, incorrectly treating friendship as a forced destination.

### Documentation Updates
Contract/FA rule profile, C18, UI comparison, current state/report.

### Claude Code Handoff
> Implement the domestic FA process with explicit eligibility and user authority. Support no-offer outcomes and expired offers. Do not create foreign-team operations or make advisory preferences mandatory.

## Phase 16 — Transfers, releases, foreign-player intake, and NPC exits

### Goal
Prove the league can change personnel and replace departures without invalid contracts, affiliations or history.

### Dependencies
Phase 15; D2 trading/foreign-player/roster rules.

### Scope
AI club trades, no-trade consent, releases, domestic re-signing, foreign-player candidate pool/contracts, routine NPC retirement and historical identity preservation.

### Explicit Non-Goals
No foreign league simulation; no player-executed trade; optional trade-request gameplay remains deferred because G explicitly calls it a later consideration. Full retirement ceremonies/legacy are Phase 25.

### Architecture / Systems
C19/C07/C13/C30. Foreign recruits are background-origin Player records, not members of simulated MLB/NPB leagues.

### Domain Models
`TradeProposal`, `TransferPlan`, `NoTradeConsent`, `ReleaseDecision`, `ForeignCandidatePool`, `RetirementDecision`.

### Implementation Tasks
1. Evaluate finite AI proposals with roster need, value, salary and eligibility; validate both clubs before committing.
2. Require controlled-player consent only where contract rules grant it; otherwise expose confirmed AI trade outcome.
3. Implement release/re-sign and quota-limited foreign candidate intake; retain foreign background histories without foreign standings.
4. Introduce routine NPC retirement based on versioned age/ability/opportunity policy. Controlled-player retirement becomes a pending career-end request until Phase 25's full ending, never an unnoticed deletion.

### UI Work
Trade/no-trade dialog, transfer/release notices, contract and team links, released/retired database filters and basic retirement notice.

### Persistence Work
Atomic contract/affiliation/assignment transfer, dated team stints, trade payload, intake origin, released/retired status. Do not cascade-delete history.

### Events
`PlayerTraded`, `PlayerReleased`, `ForeignPlayerSigned`, `PlayerRetired`, `RetirementDecisionRequested`.

### Tests
Two-club rollback, injury/registration restrictions, no-trade rejection, no destination, foreign quota, retire/re-sign legality, retired player stat links and saved pending consent.

### Acceptance Criteria
**Gate: G +** no active incompatible duplicate affiliation; every transfer reconciles both rosters/contracts; rejected transfer leaves both unchanged; foreign intake works offline with fictional data; NPC retirement preserves all historical references.

### Deliverables
Transaction/intake/exit policies, notices/dialogs, invariant suite, `PHASE_16_REPORT.md`.

### Risks
Multiple systems editing membership, infinite trade churn, league population inflation, and erasing retired players to reduce database size.

### Documentation Updates
Roster/contract/career docs, C19/C30 and current state/report.

### Claude Code Handoff
> Add atomic AI personnel changes and sustainable player exits/intake. Preserve player identity and history. Do not expand into foreign leagues, automatic trade-request gameplay, or a player management mode.

## Phase 17 — Twenty-season world stability

### Goal
Prove the domestic world remains populated, legal, and explainable after the original roster has substantially turned over.

### Dependencies
Phase 16.

### Scope
Integrated annual cadence, draft/FA/contracts/development/rosters/retirement, population and balance diagnostics, save migration chain and measured performance.

### Explicit Non-Goals
No new life/international features or silent removal of failing rules to accelerate tests.

### Architecture / Systems
SimHarness drives Application commands and explicit harness decision policies; no alternative simulation shortcut.

### Domain Models
`WorldHealthReport`, `PopulationLedger`, `AnnualAudit`, `BenchmarkRun`; diagnostic models only.

### Implementation Tasks
1. Stitch annual phase windows without circular date dependencies or unresolved NPC market locks.
2. Run five fixed seeds for 20 seasons; checkpoint annually and compare selected resumed segments with uninterrupted runs.
3. Inspect roster shortages, age/current-skill distributions, elite tails, unsigned/released/retired counts, salary policy and school outputs.
4. Fix defects in their owning modules; record tuning changes/version bumps separately from behavior changes requiring owner approval.

### UI Work
Existing screens must open late-save profiles, season tables, retired players and draft histories; expose long-operation progress/cancel at safe boundaries.

### Persistence Work
Measure database growth by family and indexed query plans. Exercise every prior schema fixture and rebuildable projection. No unapproved retention pruning.

### Events
No new product events; diagnostics verify event/projection uniqueness.

### Tests
Five ×20-season runs, periodic reloads, controlled-decision stop tests, population accounting and historical reconciliation, §6.3 benchmarks.

### Acceptance Criteria
**Gate: G +** zero illegal lineups/duplicate payments/draft picks/orphan references; every active-player count equals prior population plus entries minus exits; no unexplained yearly monotonic strength/population drift outside declared tuning bounds; measured budgets pass on recorded hardware or targeted fixes bring them within budget.

### Deliverables
Seeded soak outputs, balance charts/tables, query plans, migration evidence, `PHASE_17_REPORT.md`.

### Risks
Passing a single “lucky” seed, suppressing shortages, or equating deterministic behavior with believable balance.

### Documentation Updates
`docs/testing/long-save.md`, tuning baseline and performance measurements, current state/report.

### Claude Code Handoff
> Validate the real domestic command path over five 20-season runs. Report population and rule failures explicitly. This phase is a gate on existing systems, not permission to add new scope or weaken rules.

## Phase 18 — National selection and international competitions

### Goal
Prove international participation coexists with domestic affiliation and uses the same game engine with separate records.

### Dependencies
Phase 17; D2 tournament schedules, entry/selection eligibility and format profiles.

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
Phase 18; D2 versioned military eligibility/duration/contract-service-credit policy.

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
Manager/coach/teammate/competitor/rival/family/partner/agent relationships, dating-marriage-family events, personality/image/reputation, conditional stories and interview intent choices.

### Explicit Non-Goals
No league-wide social graph, full dating simulator, hidden numbers exposed, invented relationship facts from prose, or forced FA destination.

### Architecture / Systems
C23/C24/C26/C29. Story eligibility/choice effects are domain data; template rendering is presentation.

### Domain Models
`CharacterId`, `RelationshipEdge`, `RelationshipCategory`, `FamilyState`, `StoryDefinition`, `StoryInstance`, `ChoiceEffect`, `InterviewIntent`, `ReputationProfile`.

### Implementation Tasks
1. Create player-centered relevant character edges; keep managerial trust separate from personal relationship.
2. Implement predicates, participant binding, cooldowns, expiry and legal predefined effects for story templates.
3. Add family progression and lightweight rivalry/team-adaptation stories; family/location preferences advise rather than constrain choices.
4. Persist selected intent and effects before rendering an interview or story; escape user-entered feature descriptions as data. Any gameplay effect of free-text traits requires a defined rule, not arbitrary AI interpretation.

### UI Work
Relationship categories/reasons, separate trust, family/life status, priority choice popups, template interview and timeline links.

### Persistence Work
Characters/edges/family/story instances/cooldowns/choice receipts/image-fame facts. No retroactive romance inferred during migration.

### Events
`RelationshipChanged`, `RivalryEstablished`, `FamilyStateChanged`, `StoryTriggered`, `StoryChoiceResolved`, `InterviewIntentSelected`, `ReputationChanged`.

### Tests
Eligibility contradictions, retired/released participants, repeated choice, missing participant, cooldown/reload, friendly competitor, low trust with close relationship, family preference overruled by player, non-baseball effects.

### Acceptance Criteria
**Gate: G +** at least one complete fixture for each named relationship class and family stage; story facts/effects match before and after reload; no NPC-to-NPC full-network expansion; UI categories hide exact relationship values; backend resolves stories with networking disabled.

### Deliverables
Relationship/story services, initial varied templates, Relationships/events UI, `PHASE_21_REPORT.md`.

### Risks
Stories reapplying on screen open, coercive preferences, free-text traits becoming an unbounded gameplay language.

### Documentation Updates
Life/story contracts, visibility policy and story authoring guide, current state/report.

### Claude Code Handoff
> Implement structured, replay-safe story choices and relevant relationships. Separate friendship from trust. Prose must describe already persisted facts; neither user trait text nor generated text can directly become game rules.

## Phase 22 — Advertising, sponsors, and agent marketing

### Goal
Prove fame can generate commercial opportunity with money, equipment, time and fatigue trade-offs.

### Dependencies
Phases 14, 18 and 21.

### Scope
Fictional brand categories, agent marketing, advertisement/sponsor proposals, terms/image requirements, appearance schedules, equipment obligations and player final decision.

### Explicit Non-Goals
No compulsory real brands, unbounded stat bonuses, automatic acceptance, or separate marketing management game.

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
Unified imported/generated history, season/team-stint/career/competition records, milestones, advanced stats (WAR, wRC+, OPS+, FIP, ERA+, BABIP), awards and representative events.

### Explicit Non-Goals
No claim that approximate game-model WAR equals an external provider's formula; no fabricated historical inputs; legacy selection decisions wait for Phase 25.

### Architecture / Systems
C11/C27/C28; explicit metric definitions and versions, rebuildable read projections over retained counting inputs.

### Domain Models
`HistoricalCoverage`, `MetricDefinition`, `MetricValue`, `TeamStint`, `MilestoneOccurrence`, `CareerSummary`, `FranchiseLeaderboard`.

### Implementation Tasks
1. Define exact denominators, league/park baselines, replacement/position treatment, two-way aggregation and minimum-sample policies for each metric in `docs/statistics.md`.
2. Implement simple ratios and comparative metrics first, then a documented simulation-specific WAR model from retained batting/pitching/fielding inputs; mark method and unavailable values.
3. Join imported opening baselines with generated intervals without overlaps/double counting.
4. Build team-season summaries, franchise leaderboards, milestones, role/position/entry/military/commercial timelines and historical news links.

### UI Work
Records tabs for season/career/second-team/national/postseason/rankings; Career timeline and team history. Explain advanced metrics in tooltips and label unavailable history rather than displaying zero.

### Persistence Work
Metric/version/baseline/coverage metadata and indexed projections; preserve source counts. Recalculation creates a versioned projection, not a rewrite of historical awards already decided under earlier versions.

### Events
`CareerMilestoneReached`, `FranchiseRecordSet`, `LeagueRecordSet`; metric rebuilds are diagnostic/projection notifications.

### Tests
Known small stat fixtures, zero denominators, transferred players/team stints, two-way contributions, missing imported fields, overlapping baseline rejection, 6+2 titles, ties in record rankings and repeated milestone projection.

### Acceptance Criteria
**Gate: G +** season/team-stint sums reconcile to supported career totals; each metric has a reproducible formula fixture; unavailable inputs never become a false exact WAR; historical projection rebuild leaves authoritative outcomes unchanged; long-save queries meet §6.3.

### Deliverables
Metric/history projectors, query indexes, record/timeline UI, reconciliation report, `PHASE_23_REPORT.md`.

### Risks
Mixing league levels or metric methods, double counting traded players, preserving totals but losing franchise-specific contributions.

### Documentation Updates
Statistics/history/data dictionary and C11/C27/C28, current state/report.

### Claude Code Handoff
> Complete historical read models and explicitly defined advanced metrics. Keep provenance, coverage and missing-data semantics. Never add imported career totals on top of the seasons they already contain.

## Phase 24 — Complete player, team, and amateur discovery

### Goal
Prove users can navigate the whole saved world and return to their prior task without losing filters or live-game state.

### Dependencies
Phases 12 and 23.

### Scope
All required Player DB filters/profile tabs, retired players, complete Team Profile, school output search/panels, draft histories/projections and global links.

### Explicit Non-Goals
No hidden-stat disclosure, school/club management controls, portrait generation unless separately approved, or loading all rows into Control nodes.

### Architecture / Systems
C35/C36 and UI route/query contracts. One common profile per identity with context-dependent tab visibility.

### Domain Models
`PlayerSearchQuery`, `TeamHistoryQuery`, `SchoolAlumniQuery`, `PageCursor`, `NavigationEntry`, `ProfileTabAvailability` (read models).

### Implementation Tasks
1. Add indexed pagination, stable sorting, combined origin/entry/registration/level/status filters and explicit missing-value ordering.
2. Complete Player Profile tabs and Team Profile overview/history/season/roster/records/awards/retired-number slots/news links.
3. Complete school panels, active/retired alumni and recent draft output, draft projections and results.
4. Audit every named-player/team surface for reusable links; preserve nested navigation and active-game decision ownership.

### UI Work
All listed screens at QHD/FHD, fixed headers, numeric alignment, filter restore, wide-table details, long names/text, loading/empty/error/missing-image/retry states.

### Persistence Work
Read indexes/projections as required, migrated explicitly; route preferences outside authoritative simulation. No copying hidden values into generic table data.

### Events
No new authoritative events; screen lifecycle/query notifications only.

### Tests
50-season-sized synthetic dataset, combination filters, duplicate names, multi-stint years, school alumni identities, nested player→team→player back navigation, stale async responses, every link source in UI inventory.

### Acceptance Criteria
**Gate: G +** all required filters/tabs have a working data source; ten nested navigation steps restore state; reopening a profile does not alter game hash; no hidden field in table sorting/export/tooltip; all hidden FHD columns remain accessible through details.

### Deliverables
Completed discovery screens/components, indexed query suite, UI inventory evidence, `PHASE_24_REPORT.md`.

### Risks
N+1 SQL queries, UI data leakage via sort order, destroyed game scene when opening profiles, missed new-player origin filters.

### Documentation Updates
UI inventory, query contracts C35/C36, current state/report.

### Claude Code Handoff
> Finish the existing common profiles and discovery routes rather than creating one-off profile screens. Verify global links and restored state at both resolutions with a long-save dataset.

## Phase 25 — Retirement, franchise legacy, and Hall of Fame

### Goal
Prove a career ends with an accurate summary and distinct team/league legacy outcomes for both controlled and NPC players.

### Dependencies
Phase 24; approved legacy eligibility/waiting-period/tuning profile under D2.

### Scope
Retirement decision/farewell, complete career summary, delayed team-specific number retirement, independent Hall cases, NPC candidates and bounded future epilogue.

### Explicit Non-Goals
No post-retirement management/life gameplay, revealed raw scores, guaranteed honor from one stat, or one-team-only number retirement restriction.

### Architecture / Systems
C30–C32 consume retained historical facts; timeline scheduler handles delayed announcements. Extends routine NPC exit facts from Phase 16.

### Domain Models
`RetirementPlan`, `FarewellEvent`, `FranchiseLegacyCase`, `NumberRetirementDecision`, `HallEligibility`, `HallCase`, `EpilogueFact`.

### Implementation Tasks
1. Resolve final season/game or immediate career-end paths and lock playable career commands while preserving history exploration.
2. Build summary from all recorded domains including income, commercial, military, relationships and role changes.
3. Evaluate each franchise independently using G's approximate 25/20/20/15/10/5/5 factor emphasis as the starting tuning profile, not a raw score exposed to users.
4. Evaluate league Hall eligibility/selection independently for all retired players. Advance only bounded epilogue milestones, using committed outcomes and a documented cutoff; do not silently simulate an endless new game mode.

### UI Work
Farewell and final summary; Hall listing/profile; team retired-number cards; epilogue navigation. Preserve exploration access to retired players and supporting history.

### Persistence Work
Retirement/legacy/Hall/announcement/epilogue rows with evaluated input/model versions; unique team-number policy; keep pending delayed honors in saves.

### Events
`PlayerRetired`, `FarewellHeld`, `NumberRetirementConsidered`, `NumberRetired`, `HallCandidateAnnounced`, `PlayerInductedIntoHallOfFame`, `EpilogueCompleted`.

### Tests
Quiet/injury/star retirement, no honor, one/multiple-team honor, league legend without franchise honor, NPC Hall entry, delayed announcement save/load, repeated eligibility processing, retired-number reuse rules.

### Acceptance Criteria
**Gate: G +** each outcome path has fixture evidence; no active gameplay command proceeds after career end; exact legacy/Hall scores stay private; multiple-team honors are possible; summary totals reconcile and delayed facts survive reload without duplicate awards.

### Deliverables
Retirement/legacy services and screens, bounded epilogue specification/tests, `PHASE_25_REPORT.md`.

### Risks
Honors becoming one-stat thresholds, delayed awards lost on closure, epilogue scope expanding into a new game.

### Documentation Updates
`docs/retirement-legacy.md`, C30–C32, final-summary UI, current state/report.

### Claude Code Handoff
> Complete career closure and distinct franchise/Hall evaluation. Preserve NPC eligibility, delayed results and rare multiple-team number retirement. Never reveal raw scores or extend into a playable post-retirement mode.

## Phase 26 — Optional Gemini narrative presentation

### Goal
Prove generated expression enriches confirmed events while gameplay remains correct, responsive and usable offline.

### Dependencies
Phases 21 and 25; D5 approved content-integrity policy.

### Scope
Importance routing, local ordinary templates, short major-event content, exceptional article/interview/fan/SNS bundles, fictional outlets, bounded queue/cache/cost controls and fallback.

### Explicit Non-Goals
No Gemini world decisions, new relationship/injury/contract facts, full-save uploads, hardcoded permanent model pricing, image generation unless separately approved, or online dependency for progression.

### Architecture / Systems
Application public fact builder + Infrastructure narrative adapter; C29. Adapter has no domain command port or mutable entity references.

### Domain Models
`PublicFactBundle`, `ContentImportance`, `NarrativeRequest`, `NarrativePolicy`, `NarrativeResult`, `FactFingerprint`, `PresentationCacheEntry`.

### Implementation Tasks
1. Implement importance/outlet/content selection using confirmed facts; ordinary news makes zero API calls.
2. Pin a supported Gemini model/API contract at implementation time and store prompt/model metadata; keep keys in separate local user configuration, never saves/logs/git.
3. Implement bounded requests, timeout/cancel, rate-limit backoff, duplicate suppression, content budgets and session/branch-aware callbacks.
4. Validate result schema and approved fact references/numeric placeholders. Apply D5's allowed expression rule; reject uncertain/unsupported factual assertions to template fallback rather than claiming a regex guarantees prose truth.

### UI Work
News categories, outlet/card/detail/bundle views, backend-defined interview choices, generation indicator with immediate fallback text, AI unavailable state and settings/test connection.

### Persistence Work
Facts remain in career DB; optional narrative cache keyed by branch/event/facts/model/prompt/locale. Secrets are outside save. Failure/retry metadata cannot rerun domain events.

### Events
No generated domain facts. `NarrativeReady`/`NarrativeFailed` are presentation notifications only.

### Tests
Offline/no key, timeout, quota failure, malformed JSON, injected false injury/romance/score, duplicate result, late callback after loading another career, escaped names/markup and fact privacy audit.

### Acceptance Criteria
**Gate: G +** network-disabled season progression matches online canonical domain hash under identical actions; ordinary events issue zero calls; failed major events show fallback without blocking; repeated opening/reload does not regenerate; adversarial facts are rejected under the approved policy.

### Deliverables
Gemini adapter/public payloads, templates/outlets/cache, narrative UI, failure matrix and `PHASE_26_REPORT.md`.

### Risks
Unsupported claims in otherwise valid prose, runaway costs, prompt injection via imported text, stale results attaching to another save.

### Documentation Updates
`docs/narrative-system.md`, approved D5 policy, C29, API configuration and current state/report.

### Claude Code Handoff
> Implement optional presentation only. Test adversarial output and every network failure path. Do not attach domain-writing capabilities to Gemini or claim structured JSON alone enforces factual truth.

## Phase 27 — Complete datapack tools and content certification

### Goal
Prove the complete game runs with fictional content and imports supported personal data without affecting existing careers.

### Dependencies
Phase 26; D3 defines the 2026 personal pack's as-of cutoff and data inputs.

### Scope
Complete JSON schemas, individual-file/pack import, image overrides, draft classes/selected rules, documented normalization/sample correction/manual overrides, final fictional pack and separate personal 2026 pack workflow.

### Explicit Non-Goals
No executable mods, full in-game data editor (future candidate), scraping unspecified datasets, mandatory personal real-data distribution, or live-save rating/roster replacement.

### Architecture / Systems
C34 plus Assets; `tools/StarPlayer.PackTool` validates/prepares effective packs offline. Runtime imports finished ratings; it does not recompute old careers from new reality.

### Domain Models
`PackSchemaVersion`, `ImportProvenance`, `RatingEvidence`, `ManualOverride`, `HistoryCoverage`, `AssetReference`, `EffectivePackManifest`.

### Implementation Tasks
1. Complete schemas for all implemented families, optional defaults, clear unknown-field/version policies, asset paths and overlay replacement declarations.
2. Implement pack-tool rating preparation using league-relative normalization, sample-size weighting and explicit scouting/manual correction; retain provenance and cutoff.
3. Validate the complete fictional pack and user's separately supplied 2026 data against IDs, dates, roster/contract/rule consistency and historical coverage.
4. Add data-management import/error UI and safe image fallback; prove original pack deletion does not corrupt an active career.

### UI Work
Settings → Data Management → folder/file select → validation report → install; new-career pack choice; file/entity/field-level errors, optional asset warnings and fallback previews.

### Persistence Work
Freeze effective world content and history at creation; schema migration tests for pack upgrades; immutable asset identity/hash and saved references. A later cosmetic override is an explicit presentation operation, never a world re-import.

### Events
`DatapackInstalled` is application-level; `CareerCreated` records chosen provenance. Import failures emit no world mutation.

### Tests
Every schema family, duplicate IDs, broken references, bad enums/dates/ratings, traversal/symlinks outside allowed pack, oversized/corrupt images, malformed JSON, missing assets, same-named entities, differing old/new pack versions and cutoff overlap.

### Acceptance Criteria
**Gate: G +** fictional pack supports new/existing starts offline; validation errors block publication and identify source fields; ratings preparation distinguishes low/high sample reliability; all supplied 2026 rows pass or have explicit rejected-row reports. The 2026 content subgate remains INCOMPLETE until required user data/cutoff are available; this does not invalidate the separately tested fictional-game gate.

### Deliverables
Complete schemas/validator/tool, fictional content, personal-pack build recipe and certification report, `PHASE_27_REPORT.md`.

### Risks
Silently inventing real-world data, importing future evidence into an earlier snapshot, inconsistent mod precedence, requiring external photos to load a save.

### Documentation Updates
`docs/datapacks.md`, rating methodology, schemas/source provenance, C34 and current state/report.

### Claude Code Handoff
> Certify the full fictional pack and supported import workflow. Keep personal 2026 data separate and source its evidence explicitly. Report missing supplied data as an incomplete content gate; never fabricate a real roster or rewrite a running career.

## Phase 28 — Alpha visual system and complete UX

### Goal
Prove all implemented features form one understandable, consistent UI at the two supported resolutions.

### Dependencies
Phase 27 technical/fictional-content gate; track the personal-data subgate explicitly if still unavailable. All required gameplay feature gates must already pass.

### Scope
Final palette/typography/tokens, reusable card/table/button/tab/status components, all navigation/routes, settings/window modes, accessibility cues and complete error/empty/loading behavior.

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

### Persistence Work
Versioned display/preferences file, migrations only if presentation metadata changes. No save fact rewrite for a theme change.

### Events
Presentation notifications only; no simulated RNG consumption.

### Tests
Every inventory route at QHD/FHD, window/borderless/fullscreen as supported in the selected desktop export, keyboard focus, text overflow, removed images, failed saves/DB/API and nested profiles during live games.

### Acceptance Criteria
**Gate: G +** all inventory rows have screenshots and workflow evidence at both resolutions; no clipped required action or unreachable information; all error/loading/empty states are usable; §6.3 query/input budgets pass; simulation hashes match the unthemed build for identical inputs.

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
Phase 28; D6 retention policy resolved before any automated pruning. Outstanding required content gates remain visible.

### Scope
Transitions/sound/celebrations, final UI QA, three 50-season seeds, complete career-ending flows, upgrade/recovery matrix, exported desktop build and handoff documentation.

### Explicit Non-Goals
No Steam/Cloud/achievements, extra resolutions/platforms, multiplayer, foreign career, code mods, or unapproved portrait expansion.

### Architecture / Systems
Existing modules only; measured optimization in owning systems and optional retention maintenance as approved.

### Domain Models
No new gameplay model; final compatibility manifest and diagnostic reports.

### Implementation Tasks
1. Add nonblocking/skippable transitions, sound feedback and major-event celebrations; keep simulation independent of frame rate/animation.
2. Apply approved retention only to permitted reconstructible/presentation data with explicit audit and integrity checks.
3. Run three 50-season world soaks plus controlled-player full-career stories, all schema upgrades, corrupt/newer-save handling and fault-injection matrix.
4. Cold-launch exported build with fictional data and no network/editor, verify every important flow, and record known limitations without calling unresolved gates complete.

### UI Work
Final QHD/FHD audit of every inventory screen; long text/names/numbers, dense late-save tables, loading/retry, record/MVP/title/medal/farewell/legacy sequences and sound preferences.

### Persistence Work
Final migration compatibility matrix, backup/recovery documentation, no destructive cleanup of authoritative history; secrets absent from exported content and snapshots.

### Events
Existing facts trigger presentations; replaying animations cannot duplicate events or awards.

### Tests
Three ×50-season seeds, full-career save/resume to epilogue, §6.3 performance, memory/database growth, all prior migrations, exported offline play, publication crash points and API failures.

### Acceptance Criteria
**Gate: G +** all soak invariants and performance budgets pass; retained history reconciles before/after maintenance; exported game loads and saves offline; zero unresolved critical save corruption/progression/privacy defects; every required source requirement has passing evidence or an owner-approved scope decision. Personal 2026 data absence cannot be mislabeled complete.

### Deliverables
Personal-play desktop build, final QA/soak/compatibility reports, final known-issues list, `PHASE_29_REPORT.md` and concise current state.

### Risks
Animation blocking decisions, late tuning invalidating earlier calibration, untested exported native dependencies, declaring completion despite incomplete content.

### Documentation Updates
Testing/compatibility/recovery docs, traceability with gate evidence, current state and final phase report.

### Claude Code Handoff
> Polish and certify the implemented scope. Run the full exported-build, recovery and long-save gates. Do not label the project complete while required tests, source coverage, or approved content gates are missing.

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
| Gemini dependency, inaccurate prose or cost | High | High | Optional adapter, limited public facts, fact-safe rendering policy, budgets/cache/fallback, adversarial tests | 21, 26 |
| Lost/corrupt save publication | Medium | Critical | Single-writer transactions, backup API, immutable generations, fault injection and recoverable catalog | 4, 17, 29 |
| Hidden-data leakage through UI/news | Medium | High | Public DTO allowlists; exclude private sort keys; self/NPC/cheat serialization tests | 1, 5, 12, 24, 26 |
| Rule/version ambiguity or data cutoff leakage | High | High | Owner-approved profiles, explicit simulated vs historical coverage, no live-rule refresh | 2, 6, 13–19, 27 |
| Missing real-data inputs | Medium | Medium | Fictional runtime baseline, independent personal-pack subgate, source/cutoff provenance | 0, 27 |
| Scope creep | High | High | Phase non-goals, source mapping, explicit exclusion list and approved change records | Every phase |

## Decisions Requiring Owner Approval

These are product-policy choices, not requests to select libraries, table names, folder structure or test frameworks. Until answered, affected behavior is **proposed**, not confirmed. Phase 0 and Phase 1 are independently implementable. Questions do not block completion of this roadmap document.

### D1 — Decision frequency inside a plate appearance

**Issue:** G Part 3 says the base simulation unit is one plate appearance, while hitter/pitcher examples and U §8 show live ball-strike counts and choices such as taking a pitch. Neither source fixes how often the user acts.

**Why it matters:** It changes pacing, save checkpoints, strategic meaning and engine complexity. A decorative invented count would misrepresent the simulated state.

**Recommended choice:** Keep PA as the orchestration unit, resolve a real internal pitch/count sequence, and offer a strategy at PA start with optional decision updates only at explicitly approved count boundaries. “Take” applies to the next real pitch. Approve the default prompt cadence before Phase 3.

**Alternative:** Require a decision on every pitch; or approve an abstract PA-only game and revised count/“take” UX. The latter changes a displayed gameplay promise and cannot be silently adopted.

**Impact:** Phase 2 can build generic count/scoring types, but Phase 3's resolver and Phase 5's final game-input UX depend on this answer.

### D2 — Target competition-rule fidelity and baseline editions

**Issue:** Required systems are named, but exact season lengths, ties/extra innings, DH/two-way rules, registration limits, draft order/rounds, FA service/compensation, foreign quotas, awards, international cycles/eligibility, military dates and legacy waiting periods are not fully specified.

**Why it matters:** These alter gameplay and long-term career outcomes; silently picking plausible values would establish product policy without approval. Source examples are not a complete rulebook.

**Recommended choice:** Use a versioned 2026 KBO-oriented rule pack, with current source-backed real rules at the chosen cutoff and a short explicit list of approved game simplifications. International/military rules get edition/effective-date profiles. Approve the baseball subset before Phase 2, then each later subsystem's rule specification before its phase. Honor the source's Asian Games gold/Olympic top-three conditions plus individual eligibility.

**Alternative:** Approve a fictional KBO-style rule set throughout, or pursue fuller real-world fidelity with a larger rule/data validation burden.

**Impact:** Gates 2, 6, 10–11, 13–19 and 25 require the corresponding approved subset. This does not mean implementing or researching every later rule during Phase 0. Balance weights can then be engineered within approved policy and measured targets.

### D3 — Personal 2026 pack's snapshot date and evidence

**Issue:** G uses “2026 actual statistics” for initial ratings and also describes a 2026 starting world/current-season stats. It does not specify preseason vs midseason vs end-of-season, the evidence cutoff, or the supplied data source.

**Why it matters:** Using completed-season evidence at preseason leaks future information. Midseason starts need already-played results, remaining fixtures, contracts and cumulative baselines with compatible coverage.

**Recommended choice:** Declare one `as_of_date`, evidence cutoff and supplied dataset for the initial personal pack. Prefer a preseason world based only on information available then; if the intended ratings use all 2026 results, use an explicitly end-of-2026/start-of-next-season snapshot instead.

**Alternative:** Fully supported midseason snapshot with current records and remaining schedule; or intentionally retrospective ratings with that choice clearly labeled.

**Impact:** Phase 27 personal-pack certification cannot pass until supplied data and cutoff are agreed. Fictional prototypes and normal save architecture are unaffected.

### D4 — New-player entry and free-text characteristics

**Issue:** Draft is described as one possible new-player start, but the default start date/team-assignment options are unspecified. G also permits free-text characteristics that may affect popularity without defining their mechanical interpretation.

**Why it matters:** Guaranteed team selection would bypass draft uncertainty; arbitrary free text cannot safely become unbounded game effects.

**Recommended choice:** Default full new careers to the draft/entry flow from the selected background, with undrafted branches intact. Use clearly labeled post-entry fictional scenarios for Prototype 2 until Phase 13. Treat free text as descriptive biography; expose any mechanical starting traits as separately defined choices with bounded, visible effects.

**Alternative:** Approve a direct-team start alongside draft start, including placement/entry-contract rules; or approve a finite reviewed mapping from characteristic choices to starting effects.

**Impact:** Approve the prototype/start-policy boundary before Phase 5; complete the selected production route in Phase 13. Personality remains editable for existing-player starts as G explicitly requires. Free-text mechanical effects are not silently implemented.

### D5 — Strict factual integrity of generated narrative

**Issue:** Sources require AI to express only established facts. Unrestricted prose generation cannot guarantee this merely through a prompt or JSON schema.

**Why it matters:** Even non-authoritative text could tell the player that an injury, relationship or contract exists when it does not.

**Recommended choice:** Use engine-authored factual clauses with immutable fact/entity/numeric slots. Gemini supplies bounded stylistic expression/approved phrase choices and optional fictional reactions under a restricted schema; uncertain claims fall back to templates. Prototype this restriction before committing to the final content UX.

**Alternative:** Approve freer draft prose with automated checks and residual hallucination risk, or keep all factual articles templated and use Gemini only for non-factual flavor. Both involve an explicit product trade-off.

**Impact:** Phase 26 cannot claim the source's factual-integrity gate using unrestricted prose plus heuristic checks. Gameplay remains fully offline-capable under every option.

### D6 — Historical detail retention

**Issue:** Sources require decades of records and representative news but do not promise permanent every-pitch transcripts or every ordinary generated article.

**Why it matters:** Pruning affects what users can revisit and which metrics can be reconstructed, while unbounded detail affects storage and load time.

**Recommended choice:** Permanently retain box scores, required metric/scoring inputs, season/team-stint totals, identity/contract/injury/draft/award/history facts and major career news. Keep detailed raw pitch traces for the current and previous season and make longer retention an explicit option; retain user-pinned articles. Measure actual storage before adopting this policy.

**Alternative:** Retain every transcript/article for the entire career and accept measured storage/query costs, or require explicit user-triggered archive maintenance.

**Impact:** Approve before Phase 29 implements deletion. Earlier phases can benchmark with clearly labeled diagnostic retention settings but cannot silently erase player-accessible history.
