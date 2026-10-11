# Star Player System Contracts

Companion to [ROADMAP.md](../ROADMAP.md). Product authority includes G/U, the original master request, [owner revision R](../sources/ROADMAP_REVISION_REQUEST.txt), targeted revision A, final feature revision F, final owner-decision revision O and the owner-approved Club Finance Lite revision CF; O approves D1–D6, CF is one bounded Scope Lock exception, and all earlier requirements remain preserved. [Portrait specification P](../Star_Player_Portrait_System_Design.MD) remains its own unchanged Source of Truth. These are proposed architectural contracts, not implemented classes or SQL. Interface notation fixes responsibilities and typed inputs/outputs; local helper names may change without altering ownership.

## Shared conventions

All mutating use cases are called through the Application command queue with a command ID and expected revision. Domain/Simulation functions receive explicit snapshots/rules/RNG and return a validated delta plus events; only the application unit of work persists it. No domain service writes another service's rows. Cross-owner changes are one coordinated transaction, not a chain of unrelated commits. Read queries have viewer context and pagination where applicable. Rejected operations return stable error codes and do not mutate state. Money uses checked integer KRW and explicit rounding; a derived projection never becomes a second authoritative obligation ledger.

In this file, `Result<T>` means a successful typed value or typed validation/conflict/failure outcome. `Delta<T>` means a proposed new state plus factual events, not a publicly editable patch. Deterministic operations include their simulation/rule/tuning/RNG versions. Every durable owner adds migrations, round-trip tests and failure tests in its phase. Internal events may carry private simulation data only inside the core; presentation projections always use explicit allowlists. Required history/stat facts commit with their causes; expressive prose is asynchronous. Club/Manager AI means rule-based C# policies. C43 is the sole validated inbound Gemini semantic path; generated prose/images never become commands. Recorded accepted interpretations are external command inputs for deterministic replay, not fresh LLM calls. All actual player-facing text is Korean.

## C01 — Player identity and career start (Phases 1, 5, 13)

- **Responsibility:** Maintain one identity across amateur, professional, released and retired life; create or select the single controlled player. A newly created production career routes through Draft / Entry, while Existing Player Start binds an existing imported identity.
- **Inputs:** Validated profile/start specification, date, source identity, descriptive biography/free text, separately defined structured personality choice where approved, or an existing imported-player query/selection.
- **Outputs:** Player identity/profile and career binding; `PlayerPublicView` through visibility policy; an automatically derived skill-based archetype label without a second bonus. Existing imported rookies expose permitted C14 draft and C47 number summaries.
- **State ownership:** Birth/profile/handedness/body/personality, ordered origins, career status; no ratings/contract/draft/jersey ownership.
- **Dependencies:** ID allocator, rule/start policy, Datapacks; C14/C34 imported history and C47 jersey projection; registration/contract coordinated separately.
- **Public interfaces:** `CreatePlayer(ProfileSpec) -> Result<Player>`; `SearchExistingPlayers(ExistingPlayerQuery) -> PlayerPage`; `StartCareer(CareerStartSpec) -> Result<CareerBinding>`; `GetPlayer(PlayerId, ViewerContext) -> PlayerPublicView`.
- **Events:** `PlayerCreated`, `CareerStarted`, `PlayerProfileChanged`.
- **Persistence:** `players`, `player_origins`, career controlled ID; imported source ID/provenance preserved and referenced, never copied for career binding.
- **Failure cases:** Duplicate ID, invalid date, incompatible source/profile, direct-club new-player start outside an explicitly approved future policy, arbitrary biography text converted into mechanics, selection of an unplayable status, changing controlled player midcareer, imported rookie copied or historical draft rerun.
- **Testing:** Same-name players, age boundary, production new-player route reaches Draft / Entry, Prototype 2 shortcut is labeled post-entry, biography text has no uncontrolled effect, existing-start preservation except personality, rookie/year/club/position/round filters, imported rookie keeps one PlayerId/draft/entry/contract/registration/initial-number facts, and retained identity after draft/transfer/retirement.

## C02 — Ratings and effective ability (Phases 1, 3, 7)

- **Responsibility:** Maintain current base skills and calculate temporary effective performance without confusing them. `CatcherRatings` holds Receiving/Framing, Blocking, Pop Time and Game Calling independently of C proficiency; shared Fielding/Arm/Endurance remain separate inputs.
- **Inputs:** Current rating vector, state modifiers, matchup, proficiency, C46 ParkFactorSnapshot and strategy context. Catcher tools, C proficiency and C41 pair context use documented bounded effective modifiers. Pop Time is a 0–100 higher-is-better skill; raw observed seconds have a separate unit.
- **Outputs:** Effective skill snapshot and separately approved base-rating deltas; privacy-safe current evaluation. Self catcher values may be exact; NPC tools and C proficiency use approved observation projections, never hidden truth.
- **State ownership:** Current 0–100 skills and change provenance; hidden developmental parameters belong to C04. No composite CatcherRating/Caught Stealing Ability, and chemistry/Game Calling cannot permanently boost pitcher Stuff or other base ratings.
- **Dependencies:** Player, PlayerState, Proficiency, approved tuning; no statistics-to-rating overwrite.
- **Public interfaces:** `EvaluateEffectiveAbility(AbilityContext) -> EffectiveAbility`; `ApplyBaseChange(AuthorizedAbilityDelta) -> Result<Ratings>`.
- **Events:** `PlayerAbilityChanged` with reason/version; public projection excludes hidden causal inputs.
- **Persistence:** `ratings`, skill-change history; transient effective values are recomputed.
- **Failure cases:** Out-of-range/NaN inputs, unknown skill, stale deltas, changing base ability for a temporary injury.
- **Testing:** Bounds, independent modifier effects, state changes leave base skills intact, cheat current ratings remain 0–100, no hidden UI values. Excellent catcher tools/low C familiarity and average tools/high C familiarity; modifier caps, pitch-profile framing disabled where required, unchanged base ratings under chemistry/fatigue.

## C03 — Player state and workload (Phases 2, 5, 7–10, 19–22)

- **Responsibility:** Evolve condition, fatigue, sharpness, stress, satisfaction and recent form; account for dated exposure.
- **Inputs:** Pitch counts, PA, fielding innings, travel, training/life load, rest, availability and date. Actual catcher defensive exposure, repeated starts and rest from C08; no workload from a nominal C position alone.
- **Outputs:** State delta, workload summary and risk inputs; self precise/NPC observed projection.
- **State ownership:** Current transient state and dated workload, not base skill or medical episode. Catcher load is dated biological workload shared across all squads and roles; receiving work cannot be counted twice as separate fielding bodies.
- **Dependencies:** Player, schedule, typed effects from injury/training/life.
- **Public interfaces:** `ApplyExposure(WorkloadExposure) -> Result<StateDelta>`; `Recover(RecoveryContext) -> StateDelta`.
- **Events:** `PlayerStateChanged`, `WorkloadRecorded`.
- **Persistence:** `player_states`, `workload_days`, unique exposure receipt; retain rolling windows and lifetime inputs actually required.
- **Failure cases:** Duplicate exposure, negative duration, two-way load omission, treating two competitions as two independent bodies.
- **Testing:** Workload additivity, exactly-once recovery, pitching+batting+fielding combination, save/day-cursor continuity. Catching generally exceeds ordinary fielding load under declared tuning; repeated starts cause temporary fatigue, rest restores effective performance, base ratings remain unchanged.

## C04 — Development and aging (Phase 7)

- **Responsibility:** Change base skills slowly with uncertain careers and individualized aging.
- **Inputs:** Age, current skills, hidden growth envelope/type/speed, actual training/exposure, injury effects, seeded variation.
- **Outputs:** Reasoned ability deltas and observation opportunities; never a guaranteed future result.
- **State ownership:** `DevelopmentProfile`, per-skill hidden parameters and last processed interval.
- **Dependencies:** Ratings, State, Proficiency, Training, Injury; isolated development RNG.
- **Public interfaces:** `Develop(DevelopmentContext, RngState) -> DevelopmentResult`; `Age(AgingContext, RngState) -> DevelopmentResult`.
- **Events:** `PlayerDeveloped`, `PlayerAged`; Ratings applies resulting explicit base change.
- **Persistence:** `development_profiles`, versioned tuning snapshot, update cursor and reason history.
- **Failure cases:** Duplicate interval, impossible age, hidden potential exposed, imported estimate incorrectly treated as true profile.
- **Testing:** Early/late/bust/breakout/decline cohorts, high-potential failure, deterministic replay, no rating reaction to one stat line. Catcher skills grow independently from C proficiency and pair familiarity.

## C05 — Position/role proficiency and training (Phase 7)

- **Responsibility:** Support freely attempted positions/two-way roles and finite training allocation with real unfamiliarity penalties.
- **Inputs:** Requested role/position, existing skills/proficiency, focus/intensity, learned repertoire, training/game exposure. Catcher drill/game exposure separately trains the four catcher skills and C proficiency within the same finite time budget.
- **Outputs:** Training plan, proficiency/role preference changes, effective-ability penalties and pitch-learning progress.
- **State ownership:** Proficiencies, preferred roles, training allocation and repertoire learning state; AI owns actual selection.
- **Dependencies:** Player, Ratings, State, Development, date/exposure.
- **Public interfaces:** `SetTraining(TrainingPlan) -> Result<TrainingPlan>`; `RequestRole(RoleIntent) -> Result<RoleIntent>`; `ApplyPractice(PracticeExposure) -> ProficiencyDelta`.
- **Events:** `TrainingPlanChanged`, `PlayerChangedPosition`, `PlayerRoleChanged`, `PitchLearned`.
- **Persistence:** `proficiencies`, `training_plans`, `pitch_repertoire`, dated role changes.
- **Failure cases:** Allocation >100%, invalid position, reinitializing a known skill on return, promising a starting role from a preference.
- **Testing:** SS→CF→SS retention, batter↔pitcher, shared two-way budget/time, learning by camp/first/second team, no artificial two-way ceiling. C→another position→C preserves prior C proficiency; actual represented camp/bullpen exposure can be forwarded to C41 once.

## C06 — Club, staff and team context (Phases 1, 9, 11, 21)

- **Responsibility:** Stable franchise identity, club policies, staff identity/assignment/tenure, team-role expectations, manager evaluation, captain assignment, bounded TeamAtmosphere and bounded financial decision preferences for C48 consumers.
- **Inputs:** Player performance/state/proficiency, team needs, coaching policy, dated affiliation/tenure, staff lifecycle context, captain candidate factors and documented atmosphere causes. Persistent ManagerProfile and ClubPhilosophy weights drive bounded rule-based scores, not generative-AI decisions.
- **Outputs:** Club/staff context, trust/role evaluations, selection priorities, appointment/departure/captain/atmosphere deltas, bounded spending/star-retention/cap-approach/caution preferences and public explanation categories.
- **State ownership:** Club identity/presentation metadata; staff identities/roles/effective assignments/tenures; policy, current manager evaluation/trust context, captain assignment, bounded atmosphere and team-role history. Personal relationship history remains C23. Staff-bound ManagerProfile: veteran/prospect preference, offensive aggression, defense/catcher-defense preference, starter leash, bullpen aggression, trust sensitivity, development patience. Club-bound ClubPhilosophy: development/win-now focus, FA/trade aggression, prospect protection, roster stability, spending aggressiveness, star retention, salary-cap approach and long-term caution. C48 owns budgets, sponsorships and cap assessments; C06 owns no money ledger. Save policy/version and effective staff assignment.
- **Dependencies:** Players, statistics, observations, roster query; rule snapshots.
- **Public interfaces:** `EvaluatePlayer(TeamEvaluationContext) -> ManagerEvaluation`; `ResolveStaffTransition(StaffTransitionContext) -> Result<StaffTransition>`; `SelectCaptain(CaptainContext) -> Result<CaptainAssignment>`; `UpdateTeamAtmosphere(AtmosphereContext) -> AtmosphereDelta`; `GetClubContext(TeamId) -> ClubContext`.
- **Events:** `ManagerEvaluationChanged`, `TeamRoleChanged`, `ManagerAppointed`, `ManagerDeparted`, `CoachAssigned`, `CoachDeparted`, `CaptainDesignated`, `CaptaincyEnded`, `TeamAtmosphereChanged`.
- **Persistence:** `clubs`, `club_policies`, `staff`, `staff_assignments`, `manager_evaluations`, `manager_profiles`, `club_philosophies`, captain assignments and only authoritative atmosphere state/cause cursor. Identity survives name/logo changes. Prior choices, relationships and tenure facts retain their staff/policy evidence.
- **Failure cases:** Club-name joins, overlapping incompatible staff/captain assignments, inactive franchise treated as active participant, replacement erasing prior relationship/tenure, one manager retained forever without a lifecycle outcome, friendship overwriting professional evaluation, captaincy/atmosphere used as a large rating buff, or player hire/fire/captain commands.
- **Testing:** Same name/different IDs, rename continuity, role transitions, separate trust/relationship combinations. Different profile weights affect curated decisions within caps; identical state/seed/policy matches; manager continuation/departure/replacement preserves prior history and creates a new evaluation context. Multi-factor captain selection rejects unavailable candidates. Atmosphere cause/recovery/caps prevent unsupported permanent extremes. Market/financial preferences activate only through C18/C19/C48 and cannot create budget or cap state by themselves.

## C07 — Roster, lineup and pitcher use (Phases 2, 9–10)

- **Responsibility:** Validate and select eligible assignments, lineups/rotation/bullpen/substitutions and position coverage.
- **Inputs:** Availability, registration, competition rules, workload, club policies, abilities/proficiency, game situation. For catcher selection: batting/defensive tools, C proficiency, health/condition/fatigue, scheduled-pitcher BatteryChemistry, manager profile, club philosophy and opponent/context; legality filters come first via C40.
- **Outputs:** Legal lineup/roster transition or typed shortage, with public reasons. Personal/ace-specific catcher patterns emerge from bounded scoring; no hardcoded personal-catcher flag owns selection.
- **State ownership:** Dated registrations/assignments, current selection and rotation; contract affiliation changes are coordinated with C13/C19.
- **Dependencies:** Player/Club, Injury, State, game rules; later military/national reservation.
- **Public interfaces:** `SelectLineup(SelectionContext) -> Result<Lineup>`; `ProposeRosterMove(RosterContext) -> RosterProposal`; `ValidateAssignment(AssignmentChange) -> ValidationResult`.
- **Events:** `LineupSelected`, `RosterMoveProposed`, `PlayerCalledUp`, `PlayerDemoted`.
- **Persistence:** `registrations`, `assignments`, `rotation_plans`, role/assignment history with effective dates.
- **Failure cases:** Ineligible player, duplicate slot, no catcher/pitcher, exhausted bullpen, incompatible simultaneous appearances.
- **Testing:** Shortages and legal fallback, two-way/DH rules, injury filtering, no automatic promotion from second-team stats. Ace-specific/veteran-rookie/established/newly traded battery scenarios; unhealthy or exhausted preferred catcher loses eligibility/utility; chemistry cannot override rest or create a friendship guarantee.
- **Phase 3-C2B-2-P status (policy lock and template feasibility; owner policies D1-D5 accepted, NO implementation; 3-C2B-2, 3-C2B-3, 3-C3 and Phase 4 not started; Phase 3 Gate INCOMPLETE):** `inplay-mapping-v1` (generation), `walkoff-credit-v1` (official record of a game-ending hit, OBR 9.06(f)/(g)) and the 19-template catalogue `inplay-template-v1` define what a drawn `defense_result` means as a virtual play: a drawn double/triple is a potential base hit kept as drawn in ordinary situations and reduced only to the bases the winning-run scorer advanced in the last half of the final or an extra inning (the facts of the play are those of the official template, the batter's actual final base equals the credited base, D3); a drawn home run is out-of-the-park and scores everyone (D2); all runners get an explicit `RunnerMove` and no base plan puts a runner out or needs an out/run `Sequence`; a base plan whose situation is one of the four declared `advancement` contexts of `prob-model-v1` and does not end the game is `PENDING_ADVANCEMENT` (not a candidate; the `game/runner@1` draw is 3-C2B-3). The 2026 OBR is a temporary prototype basis and the 2026 KBO clauses are NOT certified (D4). The audit record (D5) is Simulation-internal and changes no `ef1`/`ef2` text, event JSON or hash; 3-C3 settles evidence, event, receipt, state, RNG and ids together and Phase 4 stores them. Verified with test-only tooling (45 positive + 15 zero combinations, 1,080 ordinary + 4,320 walk-off cases through the real reducer, independent Python oracle); GPT Chat final review `PASS WITH NOTES` covers this policy/template feasibility verification only (Codex review `미실시`; not an implementation approval); contract `docs/game-engine.md` section 23, decisions `docs/decisions/ADR-007.md`, report `docs/reports/PHASE_3C2B2_POLICY_REPORT.md`.
- **Phase 2 implementation status:** Only the legality part implemented so far: in-game substitutions (pinch hitter/runner, defensive substitution, position swap, pitching change), DH/no-DH lineup validation and the two-way rule, read from the immutable `GameRuleSnapshot` (`docs/game-engine.md` §4–5). Lineup/roster *selection*, registrations and rotation (`SelectLineup`, `registrations`, `rotation_plans`) remain Phases 9–10.

## C08 — Game simulation (Phases 2–3, 5)

- **Responsibility:** Authoritative baseball state transitions and seeded outcomes using one engine for every competition/playback mode. Under D1, a PA normally accepts one approach and resolves a real internal pitch/count sequence; explicit one-pitch actions create only real resumable boundaries.
- **Inputs:** Legal lineups, ratings/state/proficiency snapshots, C46 resolved StadiumId/ParkFactorSnapshot, matchup, PA strategy or explicit one-pitch action, authoritative count/situation, rules and RNG state. Catcher tools/C proficiency, pitcher delivery/runner-control and pair chemistry are independent from runner speed/stealing context; GameRules govern allowed framing effects.
- **Outputs:** `GameStep` with new state, real pitch/count evidence where applicable, scored play/stat evidence, workload and next legal decision/end status. `Take` resolves exactly one actual pitch and requests another decision only if the PA continues. Distinct PB/WP scorer evidence and earned-run effects, steal/caught-stealing attribution, catcher defensive participation and actual battery/catching exposure.
- **State ownership:** Active game, batting order, substitutions, runner responsibility, score/count/outs and cursor.
- **Dependencies:** Baseball reducer/scorer, ability evaluator, roster validation, RNG; no SQL/UI.
- **Public interfaces:** `StartGame(GameStart) -> Result<GameState>`; `AdvanceGame(GameState, LegalStrategy, RuleSnapshot, RngCursor) -> Result<GameStep>`.
- **Events:** `GameDecisionRequested`, `PlateAppearanceCompleted`, `RunnerPlayCompleted`, `SubstitutionMade`, `GameCompleted`.
- **Persistence:** `game_states`, `game_players`, real count/pitch cursor at allowed boundaries, `scored_plays`, final box scores and resolved StadiumId/ParkFactor profile/version used; save at every allowed stable decision boundary. Final games do not recalculate after a venue/profile change.
- **Failure cases:** Stale decision, illegal strategy, decorative/fabricated displayed count, `Take` consuming more than one pitch, impossible reducer outcome, invalid probabilities, nonterminating rule state.
- **Testing:** All §6.2 baseball oracles, real count boundaries, default one-approach PA flow, one-pitch `Take`, resume inside a continuing PA, 100 complete games, 100k PA calibration, seeded replay and mode invariance. Neutral/pitcher/hitter park fixtures prove bounded monotonic branch effects, combined caps, no rating mutation and historical replay after profile changes. Mid-inning catcher change and shared overlap; WP/PB not merged; marginal-call framing only in eligible rule profiles; bounded blocking/Pop Time/Game Calling/chemistry influences with no base-rating inflation.
- **Pitch Zone Lite (Phase 3, ROADMAP §1.14; design only, not implemented):** Inputs add the pitcher's target zone, the batter's bounded nine-zone tendency vector (neutral default) and the C40 called-pitch mode. Outputs add, for every actual pitch (`PitchEvent`/`TakeEvent` via `PitchSpec`, and `InPlayEvent` as its single pitch, never an extra `PitchEvent`), a confirmed location (stable `PitchZoneId` under a `ZoneModelVersion`, the actual batter side applied to that pitch, `Clear`/`Borderline`). Order: target zone → drawn actual location (`game/location` stream, versioned consumption order) → swing/contact → result → `GameReducer.Apply`; the result is never drawn first. `GameReducer` keeps sole authority over official counts, calls and records. Events with a location use fingerprint `ef2`; `ef1` text, hashes and receipts stay byte-identical and coexist by receipt version; a legacy (`ef1`) receipt id re-delivered with a location added, or an `ef2` receipt id re-delivered with its location removed, is a fingerprint-version mismatch, and an altered `ef2` location is a content conflict (ADR-007); the new JSON event format is explicitly versioned and rejects a missing location while legacy formats stay readable. Persistence (Phase 4) stores location, batter side, fingerprint versions, target-zone input, resolver/tuning/rule versions and the location stream cursor; legacy games are never recalculated. BCL/LMV read confirmed locations only and never consume game RNG. Failure cases add: missing location in the new format, location inferred for a legacy event, location drawn after the outcome, ef1 receipt altered, tendency vector exposed or unbounded. Testing: ROADMAP Phase 3 work package 3-F list.
- **Phase 3-A implementation status (RNG foundation only):** `src/StarPlayer.Simulation/Randomness/` provides PCG32 XSH-RR (`pcg32-xsh-rr-v1`), `kd1` SHA-256 stream key derivation, the six `game/*` streams (including `game/location`, versioned `@1`; its consumption rule is recorded in ADR-004), `rngset1` snapshots (only streams that have drawn) with explicit version/state rejection and an expected-state-hash loading contract (the hash is committed with the game state in Phase 4; it is not authentication), a locked atomic private-copy scope for the in-memory RNG state only, and deterministic game/event ids (`docs/decisions/ADR-004.md` items 13-19, `docs/game-engine.md` §17). No probability, location drawing, `ef2` event or persistence is implemented; Phase 3-B onward was not started at that time (see the 3-B1 line below).
- **Phase 3-B1 implementation status (Pitch Zone Lite data contracts only):** `src/StarPlayer.Domain/Baseball/PitchZone/` provides `ZoneModelVersion` `pzl-zones-v1`, the 17 `PitchZoneId`s with a fixed Clear/Borderline table, `PitchLocation` (zone, applied batter side, class), `TargetZone`, `AppliedBatterSide` rules (declared vs applied side, switch hitters) and the hidden, bounded (`-100..+100`), average-neutral `BatterZoneTendency` with a neutral default. C40's `GameRuleSnapshot` gains `CalledPitchMode` (`Automated`/`NonAutomated`) with the zone model as a required pair for `gamerules-v2` (JSON `format_version` 2); legacy profiles declare nothing, keep every `ContentHash` byte-identical and never count as a KBO ABS mode; Automated means Framing influence is exactly zero. No location is drawn and no event, `ef1`/`ef2`, reducer, probability or persistence changed (`docs/game-engine.md` section 18, `docs/decisions/ADR-005.md`); 3-B2 onward is not started.
- **Phase 3-B2 implementation status (probability and tuning contracts only; approved `PASS WITH NOTES` 2026-10-11, Phase 3 Gate still INCOMPLETE):** `src/StarPlayer.Domain/Baseball/Probability/` provides the integer scale (ppm, denominator 1,000,000; `Normalize` = largest remainder, lowest index first; half-up products; 128-bit intermediates; one `NextBounded(1,000,000)` call per categorical decision, mapped by `Distribution.Select`), the `prob-model-v1` stage structure (target zone, actual location, batter decision, called pitch by mode and zone, swing result, batted ball, defence result, advancement - each stage over its own denominator), modifier slots with fixed order, per-slot caps and one combined cap, the Pitch Zone Lite tendency as an exact mass shift (caps 60,000 / 40,000 ppm, window guard, reference-weighted exact neutrality), a `ProbabilityTrace`, and the versioned **virtual prototype** tuning set `tuning-virtual-proto-v1` (`content/tuning/virtual_tuning_v1.json`, identity = SHA-256 of a canonical text). Catcher, battery and park effects are reserved slots that accept only the neutral input. Nothing is drawn, resolved, wired into a game or persisted; no event, `ef1`/`ef2`, reducer, GameRules or RNG change; no statistical calibration was run (`docs/game-engine.md` section 19, `docs/decisions/ADR-006.md`). 3-C onward is not started.
- **Phase 3-C1 implementation status (event location payload, `ef2` and event JSON format 2 only; approved for the 3-C1 package only on 2026-10-11 (GPT Chat `PASS WITH NOTES`, Codex final `PASS WITH NOTES`), Phase 3 Gate INCOMPLETE, 3-C2/3-C3 not started):** `PitchSpec` (used by `PitchEvent`/`TakeEvent`) and `InPlayEvent` gain an optional trailing `PitchLocation? Location = null` (an `InPlayEvent` stays its own single pitch; no `PitchEvent` is added). `EventFingerprint.VersionOf` is `ef2` only for an event that carries a location, with canonical text = `ef1` field list + `zone_model|zone|side|boundary`; every other event and every legacy event keeps `ef1` byte-for-byte (pinned by the unchanged digest of all 30,974 Phase 2 corpus receipts and of the legacy `WriteEvents` bytes). `GameReducer.Apply` is unchanged: it already compares receipt versions first (`Game.event_fingerprint_version_mismatch`), then kind and fingerprint (`Game.event_id_conflict`). Event JSON `format_version: 2` (`ReadEventsV2`/`WriteEventsV2`) requires a valid `location` on `pitch`/`take`/`in_play` and refuses one on other events; the legacy bare-array reader and writer are unchanged except that the legacy writer refuses a located event (`Fixture.location_requires_v2`) instead of dropping its location. Nothing resolves a pitch, draws a number or changes a rule profile, stream, `GameEventIds` or `ef1`. Decisions and the 3-C2/3-C3 handoff: `docs/decisions/ADR-007.md`, `docs/game-engine.md` §20.
- **Phase 3-C2A implementation status (single-pitch probability branch only; approved 2026-10-11 for 3-C2A only: GPT Chat `PASS WITH NOTES`, Codex final focused re-review `PASS WITH NOTES`; Phase 3 Gate INCOMPLETE):** `PitchBranchResolver.Resolve` (Simulation, resolver-internal) turns one `PitchBranchRequest` (read-only `GameState`, `TuningProfile`, explicit `PitchIntent`, explicit Take flag, declared batter side, pitcher throwing hand, caller's `GameRandomSnapshot`) into a `PitchBranchDraft`: target zone on the new stream `game/target@1` (ADR-004 item 20), actual location on `game/location@1` (built with `PitchLocation.FromZone`), batter decision and called pitch on `game/pitch@1`, swing result and batted ball on `game/contact@1`, defence result on `game/field@1`; every decision is one `NextBounded(1,000,000)`, unreached stages draw nothing, `advancement` is not drawn. The draft is not a `GameEvent`, is never applied by `GameReducer`, and does not change `GameState`, `ef1`/`ef2`, event JSON, rule profiles, tuning numbers, the RNG algorithm or any golden vector. The work runs on a private `GameRandom` restored from the caller's snapshot; the draft carries the next snapshot. A profile without a declared called-pitch mode, a zone-model mismatch, a missing batter side / pitcher hand or an illegal Take/pitch stage fails before the first draw. Decisions and handoff: `docs/decisions/ADR-007.md` (3-C2A), `docs/game-engine.md` §21.
- **Phase 3-C2B-1 implementation status (prepared pitch and non-in-play event candidate only; approved 2026-10-11 for 3-C2B-1 only: GPT Chat `PASS WITH NOTES`, Codex `PASS WITH NOTES`; 3-C2B-2/3, 3-C3 and Phase 4 not started; Phase 3 Gate INCOMPLETE):** `PitchPreparation.Prepare` (Simulation, resolver-internal) checks that the supplied `PlayerSheet`s are the state's current batter and pitcher, takes the declared side and throwing hand from them and calls the unchanged `PitchBranchResolver.Resolve`; `BuildNonInPlayCandidate` refuses any preparation whose game, RNG start hash, batter/pitcher, rules, tuning or state object changed, maps the five non-in-play branches to `PitchEvent`/`TakeEvent` (`ef2`, draft location, no moves), issues the id on a private counter from the read-only last sequence and lets the unchanged `GameReducer.Apply` judge it on the original state. The result is an unconfirmed candidate: no state, RNG, id cursor or receipt is adopted (3-C3), in-play is an explicit `in_play_not_supported_yet` limit, no random number is drawn. Decisions: `docs/decisions/ADR-007.md` (3-C2B-1), `docs/game-engine.md` section 22.
- **Phase 2 implementation status:** Implemented as the deterministic input-driven reducer `GameReducer.Start/Apply/LegalActions/Finalize` (`src/StarPlayer.Simulation/Baseball/`; contract in `docs/game-engine.md`). Outcome selection, RNG, `AdvanceGame` with strategy resolution, park factors, game persistence (`game_states`, `scored_plays`) and the 100k-PA calibration are Phase 3/4 and are **not** implemented. Passes the Phase 2 oracle and 100-game reconciliation tests; the D2 KBO production subset is recorded as unverified (profile `kbo-oriented-unverified-v0`, open items in `docs/game-engine.md` §5).

## C09 — Schedule and date progression (Phase 6)

- **Responsibility:** Maintain fixtures, venue assignment, lightweight deterministic weather interruption/rescheduling and deterministic dates/stages; stop at relevant player choices and support safe cancellation.
- **Inputs:** Competition definitions, participating squads, dates, C46 StadiumId/usage, minimal weather profile and isolated RNG, rule calendar, pending choices and availability reservations.
- **Outputs:** Valid fixture plan or conflict report; weather/interruption/reschedule result; advancement receipt with last committed stage/stop reason.
- **State ownership:** Fixture identity/status, assigned StadiumId, weather/status/reason, original↔rescheduled relation, world date and day-stage completion markers.
- **Dependencies:** Season, Game, roster and pending decisions; later activity/tournament/service reservations. C40 RegularSeasonRules; C42 Inbox projects this same barrier rather than duplicating decisions.
- **Public interfaces:** `BuildSchedule(ScheduleSpec) -> Result<Schedule>`; `ResolveFixtureWeather(FixtureWeatherContext, RngState) -> Result<FixtureWeatherOutcome>`; `RescheduleFixture(PostponedFixture, RegularSeasonRules) -> Result<RescheduledFixture>`; `AdvanceDate(AdvanceRequest) -> Result<AdvanceOutcome>`.
- **Events:** `FixtureScheduled`, `FixturePostponed`, `FixtureCancelled`, `FixtureRescheduled`, `DateAdvanced`.
- **Persistence:** `fixtures`, weather/context/reason and original↔makeup links, `day_cursor`, scheduled tasks/reservations; stable order by date/competition/game ID. Played games retain their C46 venue/profile context.
- **Failure cases:** Missing/invalid StadiumId, conflicting fixtures, weather stream affecting another subsystem, unresolved required decision, incomplete earlier stage, unsupported cancellation/makeup/doubleheader rule, lost/duplicated game or broken reschedule link.
- **Testing:** Off-days, resumable multi-game day, clear/rain/heavy-rain equivalents, postponed/makeup/doubleheader rule fixtures, venue preservation/change only by rule, no duplicated/lost completed games, cancellation and reload parity, isolated weather RNG. An unresolved important interview can pause at its required boundary; a pending expressive article cannot. Expiry is an explicit recorded outcome/notice, never silent disappearance.

## C10 — Season and awards (Phases 6, 11)

- **Responsibility:** Camp/exhibition/regular/All-Star voting/selection/game, postseason/Korean Series/awards/offseason stages and season closure.
- **Inputs:** Rules, fixture results, standings, configured All-Star voting/selection inputs, award eligibility/statistics and calendar.
- **Outputs:** Stage transitions, All-Star rank/trend/final selection, brackets, award results and uniquely identified finalized standings/postseason/championship facts that later consumers such as C48 may evaluate.
- **State ownership:** Stage, bounded All-Star voting period/snapshots/final result, bracket/series, award selection and closure marker.
- **Dependencies:** Schedule, Game, Stats, club roster; market/draft later register dated hooks. C40 RegularSeasonRules/PostseasonRules/AwardRules supply versioned definitions, not scene/service constants. C10 has no dependency on C48 or Phase 14; financial services consume committed facts in the forward direction only.
- **Public interfaces:** `AdvanceSeasonStage(StageContext) -> Result<SeasonDelta>`; `UpdateAllStarRace(AllStarContext) -> Result<AllStarVoteSnapshot>`; `FinalizeAllStarSelection(AllStarContext) -> Result<AllStarSelectionResult>`; `EvaluateAwards(AwardContext) -> AwardResults`.
- **Events:** `SeasonStageChanged`, `AllStarVotingOpened`, `AllStarVoteUpdated`, `AllStarSelected`, `AllStarVotingClosed`, `TeamClinchedPostseason`, `TeamWonChampionship`, `PlayerWonAward`, `SeasonClosed`.
- **Persistence:** `seasons`, necessary `all_star_votes`/final results, `series`, `awards`, `team_seasons`, stable achievement evidence and closure receipts; retain only detail needed for UI/history/reproducibility. No sponsorship amount or settlement row belongs here.
- **Failure cases:** Unfinished required fixture, tied rank without rule, duplicate selection after reload, unsupported/guessed voting formula, repeated closure, award without eligible candidate.
- **Testing:** Every stage, tiebreaks, position rank/trend/period/final result under differing saved selection procedures, vote/selection reload idempotency, postseason stat separation, title/award/achievement-evidence idempotency, ten full-season seeds and successful operation without C48 installed.

## C11 — Statistics and metric definitions (Phases 2, 11, 23)

- **Responsibility:** Convert scored facts into exact counts and versioned derived metrics by competition, season, team stint and role.
- **Inputs:** Scored plays, game attribution, historical baseline coverage, league/park/metric definitions. Retained catcher WP/PB/defensive attribution and scorer evidence must support required metrics without inventing missing imported history.
- **Outputs:** Batting/pitching/fielding lines, standings inputs, rankings and advanced metrics with availability/method metadata.
- **State ownership:** Counting facts/lines and metric projections; does not choose game results or ratings.
- **Dependencies:** Game scorer, imported history and competition definitions.
- **Public interfaces:** `ApplyScoredPlay(ScoredPlay) -> StatDelta`; `GetStats(StatsQuery) -> StatPage`; `RebuildMetrics(MetricScope, Version) -> RebuildReport`.
- **Events:** `StatisticsFinalized`; record/milestone owners determine their own once-only events.
- **Persistence:** Game lines, season/team-stint counts, historical baselines, projection receipts and metric version. D6 permanently retains authoritative scoring/PA inputs that cannot be safely reconstructed from aggregates.
- **Failure cases:** Double count, overlapping imported coverage, missing denominator, innings-as-decimal, incomplete WAR inputs.
- **Testing:** Count conservation, zero/unknown distinctions, transfer/two-way splits, formula fixtures, rebuild equivalence and retained-input integrity before/after any evidence-backed maintenance.
- **Phase 2 implementation status:** Single-game official counting only: `StatEntry`/`GameStatDelta` per source event, `BoxScoreBuilder`, `BoxScoreReconciler` identities (`docs/game-engine.md` §7). Season/team-stint aggregation, metric versions, imported baselines and rebuild are Phases 11/23.

## C12 — Injury and rehabilitation (Phases 8, 10)

- **Responsibility:** Decide episodes, prognosis/recovery, play-rest legality, relapse and rare long-term effects.
- **Inputs:** Workload, durability/state, age, position, episode history, medical choice, rehab exposure and seeded roll. Catcher-specific and repeated-start workload from C03, without one-size-fits-all fielding load.
- **Outputs:** Injury/recovery delta, availability, temporary modifiers and explicit rare permanent-change request.
- **State ownership:** Body/side/type/severity/dates/recovery/reinjury episode history and plan.
- **Dependencies:** State, Development, Roster eligibility, actual second-team games for rehabilitation.
- **Public interfaces:** `EvaluateRisk(InjuryContext, RngState) -> InjuryResult`; `ChooseCare(CareChoice) -> Result<RecoveryDelta>`; `ReviewRehab(RehabExposure) -> RecoveryDelta`.
- **Events:** `PlayerInjured`, `InjuryWorsened`, `RehabilitationStarted`, `PlayerClearedForRehab`, `PlayerReturnedFromInjury`.
- **Persistence:** `injuries`, `rehab_steps`, prior episode links, care choices/effect receipts.
- **Failure cases:** Illegal play-through, clearing all injuries when one heals, duplicate permanent effect, rehab marked as first-team return.
- **Testing:** All severities, recurrence, ordinary-base preservation, stacked injuries, actual second-team rehab, save during choice. Catcher fatigue affects effective play and risk; repeated catching alone causes no permanent rating loss; first-care help never submits a care choice.

## C13 — Playing contracts and service credit (Phases 13–15)

- **Responsibility:** Validate/activate domestic playing terms, run annual non-FA salary renewal/negotiation and accrue the sole authoritative player salary/payment obligations. Annual renewal is distinct from C18 FA.
- **Inputs:** Player/club eligibility, season-end evaluation, club salary proposal, prior salary/performance/role/service context, player-authorized counter/final decision, agent advice, rules, game/service evidence, calendar and C48's typed budget/cap projection for the proposal.
- **Outputs:** Active contract/offer state, annual salary negotiation/result/history, service totals, unique ledger obligations and eligibility facts; accepted obligations are the source C48 aggregates.
- **State ownership:** Contract terms/general and annual-renewal negotiation state, salary history and credit ledger, not wallet balance, club assignment rows or FA market state.
- **Dependencies:** Roster, Money, Agent advice; Application coordinates affiliation and payments. C40 owns ContractRules and SalaryCapRules; C48 advises future affordability/cap consequence but cannot edit a binding C13 obligation. Persist evaluated versions.
- **Public interfaces:** `ProposeContract(OfferSpec) -> Result<ContractOffer>`; `OpenAnnualSalaryNegotiation(SalaryRenewalContext) -> Result<AnnualSalaryNegotiation>`; `CounterAnnualSalary(SalaryCounteroffer) -> Result<NegotiationRound>`; `ResolveAnnualSalary(SalaryDecision) -> Result<SalaryRenewalActivation>`; `AcceptOffer(OfferId, PlayerDecision) -> Result<ContractActivation>`; `AccrueService(ServiceEvidence) -> ServiceDelta`.
- **Events:** `ContractOfferReceived`, `AnnualSalaryProposed`, `AnnualSalaryCountered`, `AnnualSalaryResolved`, `PlayerSignedContract`, `ContractExpired`, `ServiceCreditAccrued`.
- **Persistence:** Contracts, term rows, offers, annual renewal rounds/deadline/final receipt, salary history, service evidence and uniquely keyed payment obligations. One authoritative renewal outcome per eligible player-season; migration invents no past negotiation.
- **Failure cases:** Expired/stale offer or renewal, overlapping contract/obligation, invalid money/date, missing controlled-player consent, agent silently finalizing, renewal entering FA state, repeated round/accrual/payment scheduling, or C48 projection treated as authority to erase an existing guarantee.
- **Testing:** Entry activation, multi-year/option terms, no-trade clause, club proposal/advice/counter/final controlled-player choice, deterministic NPC resolution, reload/deadline, previous-salary comparison, exactly-one salary-history/obligation result, FA separation, C48 budget/cap validation, thresholds, money rounding and transaction rollback.

## C14 — Draft (Phases 4, 12–13, 27)

- **Responsibility:** Preserve imported completed draft history or run a generated live draft without player-selected outcomes or calendar-year hardcoding.
- **Inputs:** Data-driven `DraftClassSource`, optional validated imported results, or cohort eligibility/standings/rules/team evaluation and seeded policy.
- **Outputs:** Locked imported summary or generated picks/drafted/undrafted facts and entry-offer requests.
- **State ownership:** Draft edition/source/provenance/completed lock, order/picks/generation marker/seed cursor and selection progress; Player identity is shared.
- **Dependencies:** C34 imported facts, Feeder, Scouting, ClubAI, Registration and fixed entry Contract/C47 activation. C40 KBO Rule Engine owns DraftRules; persist the evaluated rule version.
- **Public interfaces:** `ImportCompletedDraft(ImportedDraftClass) -> Result<DraftSummary>`; `BuildDraftPool(DraftContext) -> Result<DraftPool>`; `RunNextPick(DraftId) -> Result<DraftPick>` only for generated live editions; `CompleteDraft(DraftId) -> DraftSummary`.
- **Events:** `PlayerEnteredDraft`, `PlayerDrafted`, `PlayerWentUndrafted`.
- **Persistence:** Draft edition/source/provenance/completed flag, eligible entries, picks, generated-class marker/current pick and rules/seed; unique player/pick per edition and no imported/generated PlayerId collision.
- **Failure cases:** Rerunning/mutating an imported completed edition, ineligible/duplicate/missing candidate or club/origin, invalid date/round/pick/signing facts, exhausted pool, duplicate pick retry, paused own entry decision.
- **Testing:** Imported draft loads and cannot run; club/round/pick/PlayerId preserved; arbitrary imported years and optional absence; full generated draft, ties/order rules, depleted pool, reload mid-draft/class without reroll, actual roster/C47 activation after signing and future simulation divergence.

## C15 — Amateur feeder (Phase 12)

- **Responsibility:** Supply future fictional players and evolving background organizations without external league simulation.
- **Inputs:** Reached draft year, C14 source/absence result, organization snapshot, distributions, existing university/other-path prospects and seed.
- **Outputs:** New identities/profiles/development inputs, amateur summaries and school-output facts.
- **State ownership:** Amateur organizations, cohort generation markers, enrollment history and school reputation/output.
- **Dependencies:** Player/Development factories, Datapacks, C14 source/completed state and career outcomes.
- **Public interfaces:** `GenerateCohort(CohortSpec, RngState) -> Result<CohortResult>`; `UpdateSchoolReputation(SchoolOutcomeBatch) -> SchoolDelta`.
- **Events:** `AmateurPlayerGenerated`, `ProspectCohortPublished`, `SchoolReputationChanged`.
- **Persistence:** Amateur organization snapshot, cohorts, origins/enrollments and output history inside each save.
- **Failure cases:** Generation despite imported completed class, duplicate yearly generation, reload reroll, imported/generated ID collision, empty eligible organization pool, university prospect recreated under new ID, potential fixed by school.
- **Testing:** 20 cohorts, imported-class suppression, missing-class generation once, reload-stable IDs/pre-draft facts, all origins, real-background/fake-player separation, low-reputation elite outcomes, source-pack independence.

## C16 — Scouting and public prospect evaluation (Phases 1, 12)

- **Responsibility:** Model uncertain evaluator-specific observations, estimates/confidence and media rankings.
- **Inputs:** Observation quantity/quality, evaluator skill/specialty, age/career/injury/form and permitted internal truth for simulation of observation error.
- **Outputs:** Grades/ranges, confidence, narrative facts, projected draft range and public ranking.
- **State ownership:** Observation/evaluation history and evaluator version; never true potential itself.
- **Dependencies:** Player/Stats/Feeder; distinct evaluation RNG.
- **Public interfaces:** `Observe(ObservationContext) -> ScoutingObservation`; `Estimate(EvaluationContext) -> ScoutingEstimate`; `RankProspects(PublicRankingContext) -> ProspectRanking`.
- **Events:** `ScoutingReportUpdated`, `ProspectRankingPublished`.
- **Persistence:** Observation records, estimates, confidence and publication/version; cache only with data revision.
- **Failure cases:** Zero-width range revealing true potential, rankings directly sorted by hidden score, stale estimate represented as current truth.
- **Testing:** Evaluator disagreement, uncertainty changes with information, estimate can be wrong, UI has no truth/hidden ranking key.

## C17 — First/second-team competition integration (Phase 10)

- **Responsibility:** Real lower-level games, movement and rehabilitation with shared player biology and separated stats.
- **Inputs:** Competition schedules/rules, assignment proposals, registration, health and player/team context.
- **Outputs:** Valid level transitions, shared-engine fixtures and level-specific histories.
- **State ownership:** Level movement/application receipts; roster owns dated assignments and competitions own fixtures.
- **Dependencies:** Schedule/Game/Roster/State/Development/Injury; no duplicate second-team player model.
- **Public interfaces:** `ApplyLevelMove(LevelMoveProposal) -> Result<LevelMove>`; `ScheduleRehab(RehabPlan) -> Result<RehabAssignment>`.
- **Events:** `PlayerCalledUp`, `PlayerDemoted`, `RehabAppearanceCompleted`, `PlayerMadeLeagueDebut`.
- **Persistence:** Assignment/debut/rehab histories and competition keys in every stat line.
- **Failure cases:** Unregistered first-team promotion, same-day incompatible play, second-team stat leakage, automatic rehab return.
- **Testing:** Play both levels, call-up/demotion/rehab cycle, separate totals and combined workload, loaded transition continuity.

## C18 — Free agency (Phase 15)

- **Responsibility:** Eligibility, declarations/windows, legal multi-club offers and NPC/player market choices.
- **Inputs:** Contract/service history, rule version, club need, C48 projected total/cap payroll and player budget, preferences and player decision. Saved ClubPhilosophy FA aggression/win-now/development/roster stability/financial caution applies within legal offers and C48 assessments.
- **Outputs:** FA state, offers/comparison, explainable public financial constraint reason and activation/compensation/C47 transition plan with a C48 assessment reference.
- **State ownership:** FA edition/declaration/market status; C13 owns signed contracts.
- **Dependencies:** Contracts, Roster, C47, C48, ClubAI, Agent and advisory relationship/fame facts. C40 owns FARules/SalaryCapRules; persist evaluated versions.
- **Public interfaces:** `CheckFAEligibility(PlayerId, Date) -> EligibilityResult`; `DeclareFA(Declaration) -> Result<FAState>`; `ResolveFAChoice(OfferId) -> Result<ActivationPlan>`.
- **Events:** `PlayerDeclaredFA`, `FAOfferReceived`, `FAOfferDeclined`, `FAAgreementReached`.
- **Persistence:** Declarations, market windows/offer links, decision deadlines, compensation obligations and the assessment ID used for a consequential offer/decision; no copied payroll total as contract truth.
- **Failure cases:** Ineligible declaration, expired window, no offers, concurrent acceptances, illegal compensation, financially contradictory offer, unavailable requested number or partial contract/finance/old-new club-number transition.
- **Testing:** Exact thresholds, multiple offers, choice authority, no-offer path, existing-plus-proposed multi-year obligations, separate cap/budget behavior, saved cap consequence, bounded financial philosophy differences, same/new-club number cases, retired/conflicting request with alternatives and multi-owner rollback.

## C19 — Trades, releases and foreign intake (Phase 16)

- **Responsibility:** AI-initiated legal personnel transfers and sustainable domestic intake/exits.
- **Inputs:** Club needs/value, contracts/remaining terms, C48 incoming/outgoing/current/future payroll and budget/cap assessments, eligibility/quotas, no-trade consent and available candidates. Saved ClubPhilosophy trade aggression/prospect protection/roster stability/financial caution stays bounded.
- **Outputs:** Validated transaction affecting both clubs or rejection; explainable financial impact, intake/release history and atomic C47 close/open plan.
- **State ownership:** Transaction proposal/result, foreign candidate pool; contract/roster mutations remain with their owners.
- **Dependencies:** ClubAI, Contract, Roster, C47, C48, Player, CareerHistory. C40 owns ForeignPlayerRules and applicable roster/contract/SalaryCapRules; persist evaluated versions.
- **Public interfaces:** `EvaluateTrade(TradeContext) -> TradeProposal`; `ValidateTransfer(TransferPlan) -> ValidationResult`; `ExecuteTransfer(ApprovedTransfer) -> Result<TransferReceipt>`.
- **Events:** `PlayerTraded`, `PlayerReleased`, `ForeignPlayerSigned`.
- **Persistence:** Transaction participants/terms/date, C48 assessment references, immutable origin/team-stint/jersey history and unique execution receipt; no duplicate contract or finance obligation.
- **Failure cases:** Missing consent, invalid destination/quota, stale financial or baseball evaluation, one club/contract/number/assessment updated while another fails, or endless proposal loop.
- **Testing:** No-trade consent/rejection, incoming/outgoing obligations and replacement cost, budget/cap impact, bilateral atomic rollback, old number closes/new club assigns independently, released number legal reuse, repeated receipt idempotency, released re-entry and foreign quotas without foreign league simulation. Finance influences but cannot override legality/baseball value. Transferred pitchers retain identity-based prior battery history and build new pair familiarity from actual exposure, not a global reset.

## C20 — National-team selection (Phase 18)

- **Responsibility:** AI chooses eligible national squads and resolves nonselection without user selection authority.
- **Inputs:** Tournament rules, nationality/eligibility, role competition, stats/ability/health/form/fame.
- **Outputs:** Squad selections, public selection/nonselection facts and temporary availability reservations.
- **State ownership:** Selection edition/candidate/result; domestic club affiliation is unchanged.
- **Dependencies:** Player, Roster, Stats, Injury, InternationalCompetition.
- **Public interfaces:** `SelectNationalSquad(SelectionContext) -> Result<NationalSelection>`.
- **Events:** `PlayerSelectedForNationalTeam`, `NationalSelectionMissed`.
- **Persistence:** Selection entries/role/date/policy version and individual eligibility evidence.
- **Failure cases:** Ineligible nationality/age, duplicate player, no positional coverage, injury invalidates selection.
- **Testing:** Position competition, good player not guaranteed, rule editions, saved selection and domestic affiliation preservation.

## C21 — International competition (Phase 18)

- **Responsibility:** Data-driven tournament schedules/formats/games/medals and domestic return.
- **Inputs:** Edition rules, selected squads, persistent opponent IDs, date reservations and game results.
- **Outputs:** Tournament progression, international stat scope, medals and completed-participation facts.
- **State ownership:** Tournament edition/entries/brackets/results, not domestic season or military status.
- **Dependencies:** NationalSelection, Schedule, shared Game/Stats, Roster availability.
- **Public interfaces:** `StartTournament(TournamentSpec) -> Result<Tournament>`; `AdvanceTournament(TournamentId) -> Result<TournamentStep>`.
- **Events:** `InternationalCompetitionCompleted`, `PlayerWonMedal`, `PlayerReturnedToClub`.
- **Persistence:** Competition-scoped fixtures/stats/results/medals and rule snapshots.
- **Failure cases:** Conflicting domestic participation, invalid bracket, missing opponent, medal emitted before final result.
- **Testing:** Asian Games/Olympics/WBC/Premier12 definitions, date conflicts, tournament injuries, shared-engine replay and separate records.

## C22 — Military career (Phase 19)

- **Responsibility:** Service planning/status, sports-team applications/assignment, individual medal eligibility and return.
- **Inputs:** Nationality/age/status, approved rule edition, application competition, confirmed medals, contracts and time.
- **Outputs:** Status/availability delta, temporary squad assignment, contract-credit treatment and career facts.
- **State ownership:** Military application/status/service periods/return affiliation and qualification evidence.
- **Dependencies:** Player, Roster, Contract, second-team competition and international medal facts.
- **Public interfaces:** `PlanService(ServiceChoice) -> Result<MilitaryPlan>`; `ResolveApplication(ApplicationContext) -> ApplicationResult`; `EvaluateSportsPersonnel(MedalEligibilityContext) -> EligibilityResult`.
- **Events:** `EnlistmentPlanned`, `SportsTeamApplicationResolved`, `MilitaryServiceStarted`, `SportsPersonnelQualified`, `PlayerCompletedMilitaryService`.
- **Persistence:** Military transitions/dates/policy versions, temporary squad and return references; no guessed historical status.
- **Failure cases:** Automatic application acceptance, unrelated medals qualifying, exemption conflation, conflicting affiliation on return.
- **Testing:** All statuses, rejected applications, ordinary/sports service, individual eligibility, saved return and second-team stats.

## C23 — Relationships (Phase 21)

- **Responsibility:** Player-centered relationship state with relevant people, including current/former managers/coaches, captains, teammates, rivals and agents.
- **Inputs:** Confirmed interactions/choices, personality, team/staff/captain changes, tenure context and relevant character identities.
- **Outputs:** Relationship deltas and public categories/reasons; leadership/team-adaptation and weak preference inputs for career choices. A modest personal-familiarity growth input may be supplied to C41 only for a represented relationship; absence is neutral and high friendship never guarantees strong battery chemistry.
- **State ownership:** Directed player-to-character relationship edges/history with tenure references; managerial evaluation/trust, captain assignment and TeamAtmosphere remain C06. A manager replacement creates a new C06 evaluation context without deleting the old C23 relationship. Baseball pair familiarity remains C41.
- **Dependencies:** Player/Character registry, Story, calendar and approved effects.
- **Public interfaces:** `ApplyInteraction(RelationshipEffect) -> Result<RelationshipDelta>`; `GetRelationships(PlayerId) -> RelationshipView`.
- **Events:** `RelationshipChanged`, `RivalryEstablished`.
- **Persistence:** `characters`, `relationships`, reason history; player-linked and non-player characters both supported.
- **Failure cases:** Missing/deleted participant, double effect, staff replacement erasing history, captaincy conflated with friendship, full NPC graph expansion, forced FA choice through relationship.
- **Testing:** Friendly rival/competitor, trust separate from friendship, current/new/former manager contexts, coach departure, captain/teammate interaction, transfers, public category masking and saved effects.

## C24 — Lifestyle and family (Phases 20–21)

- **Responsibility:** Lightweight life activities and dating/marriage/family progression supporting the baseball career.
- **Inputs:** Player choices, existing family/relationship facts, money/time availability and story conditions.
- **Outputs:** Activity/family facts, planned spending and state effects without direct large skill bonuses.
- **State ownership:** Family status and activity reservations/completion; wallet and relationship values remain separate owners.
- **Dependencies:** Assets, Relationships, State, Story, calendar.
- **Public interfaces:** `ChooseLifeActivity(ActivityChoice) -> Result<ActivityReservation>`; `ApplyFamilyTransition(FamilyChoice) -> Result<FamilyDelta>`.
- **Events:** `ActivityScheduled`, `ActivityCompleted`, `FamilyStateChanged`.
- **Persistence:** Activity intervals/status, family events/participants, effect receipts.
- **Failure cases:** Time overlap, insufficient funds, invented spouse, marriage granting arbitrary skill, mandatory family destination.
- **Testing:** Every family stage, hobbies/rest/camps, schedules during service/games, no duplicate costs/effects, preference override.

## C25 — Assets and personal ledger (Phases 14, 20, 22)

- **Responsibility:** Account for the controlled player's salary/bonus/incentive/commercial income, commissions, expenses, savings and ownership. This is personal finance, not C48 club accounting.
- **Inputs:** Validated payment obligations, purchase commands, catalog snapshots and scheduled expenses.
- **Outputs:** Ledger entries, wealth/earnings summaries, owned asset records and purchase effects.
- **State ownership:** Integer KRW ledger/opening balances/ownership; playing/commercial terms belong elsewhere.
- **Dependencies:** Money value type, Contracts, Life, C38 individual Commercial, catalog validation. C25 and C48 may reference the same unique C13 obligation for different projections but must never create or pay it twice.
- **Public interfaces:** `PostPayment(PaymentObligation) -> Result<LedgerReceipt>`; `Purchase(PurchaseCommand) -> Result<PurchaseReceipt>`; `GetWealth(PlayerId) -> WealthSummary`.
- **Events:** `IncomePaid`, `CommissionPaid`, `ExpensePaid`, `AssetPurchased`, `SavingsTransferred`.
- **Persistence:** Append-only ledger with unique obligation/period keys, assets/ownership, opening balance provenance.
- **Failure cases:** Overflow, negative affordability, duplicate obligation, club sponsorship posted as player income, savings counted as income, undocumented valuation policy.
- **Testing:** Money conservation, rounding, recurring costs/load, current-vs-lifetime totals, same C13 obligation referenced by C25/C48 without duplicate payment and failed purchase rollback.

## C26 — Structured stories and interview context (Phases 5, 21)

- **Responsibility:** Select eligible ordinary stories, including leadership/staff/TeamAtmosphere stories, apply defined ordinary choices, and prepare authoritative interview requests/context/consequence-rule categories; final interview interpretation belongs to C43 in Phase 26.
- **Inputs:** Confirmed world facts, personality/life/role, staff tenure/captain/atmosphere context, participants, cooldowns, ordinary choices and seeded event selection; reporter/topic intent and permitted interview targets/categories.
- **Outputs:** Pending decisions/legal ordinary options, validated ordinary effect batches and interview context. No predefined final interview-answer intent substitutes for free text.
- **State ownership:** Story/interview request instance, context, eligibility/cooldown/deadline/barrier and ordinary choice receipt; target domain values remain with their owners. Phase 21 interview contexts are inactive records until the Phase 26 interaction exists, so unimplemented obligations cannot deadlock progression; temporary provider failure after activation retains real pending decisions.
- **Dependencies:** Calendar barrier, C42 visibility surface and typed State/Relationships/Contracts/Life effects; C43 later validates free-text semantic input.
- **Public interfaces:** `EvaluateStories(StoryContext) -> StoryCandidates`; `ResolveChoice(DecisionId, ChoiceId) -> Result<ChoiceEffectBatch>` for ordinary stories; `CreateInterview(ConfirmedInterviewContext) -> Result<InterviewRequest>`.
- **Events:** `StoryTriggered`, `StoryChoiceResolved`, `DecisionResolved`, `InterviewRequested`; no fixed-choice InterviewIntentSelected event.
- **Persistence:** Definitions/rules from versioned data; story/interview context, participant IDs, pending barriers and cooldowns in SQLite; no fabricated historical interviews.
- **Failure cases:** Stale ordinary choice, contradictory predicates, absent/former staff misbound as current, captain/atmosphere creating unsupported baseball facts, detailed discipline subsystem creep, expired request, duplicate effects or interpreting arbitrary text before C43 exists.
- **Testing:** Ordinary deterministic/offline story choice; staff/captain/leadership/atmosphere eligibility and bounded effects; fact-backed conflict story with no ejection/hearing/suspension subsystem; decision stop/resume and explicit expiry; interview context/rule-only fixtures before Phase 26; no premature interview consequences.

## C27 — Career and league history (Phases 4, 11, 23)

- **Responsibility:** Preserve player and league facts and present chronological careers/records across imported and simulated history, including draft provenance, complete C47 jersey timeline and meaningful stadium/park, staff-tenure, captaincy, All-Star and C48 annual financial facts.
- **Inputs:** Committed events, C14 draft source/result, C47 assignments, stat/award/vote/staff/captain facts, C46 retained game venue/profile context, C48 payroll/budget/cap/sponsorship facts, validated imported coverage and subject IDs.
- **Outputs:** Career timeline, league/draft/jersey/stadium/staff/captain/All-Star/club-finance history, milestone facts and retirement inputs. Game Archive and milestone progress queries in Phase 23 expose retained evidence/coverage, not a new simulation or quest system (C44).
- **State ownership:** Durable history facts and milestone occurrence; rich views are rebuildable projections.
- **Dependencies:** Stats, Contracts, C48, Injury, C14 Draft, C47, Military, Life, Season and narrative links.
- **Public interfaces:** `RecordHistory(ConfirmedHistoryFact) -> HistoryReceipt`; `QueryCareer(CareerQuery) -> CareerPage`; `DetectMilestones(MilestoneContext) -> MilestoneFacts`.
- **Events:** `CareerMilestoneReached`, `LeagueRecordSet`.
- **Persistence:** History source/coverage/date/IDs, locked draft source/result, full jersey intervals, retained game/box-score/lineup/PA/scoring evidence with StadiumId/ParkFactor version, meaningful staff/captain/All-Star facts, C48 annual payroll/budget/cap/sponsorship facts and evidence/version references, milestones and projection receipt; retained after retirement. Retain original player interview answers/context and accepted interpretation/receipt references, major narrative/career facts and user-pinned content; absent past quotes stay absent. D6 starts with raw pitch traces and ordinary narrative retained until measurement supports a targeted policy.
- **Failure cases:** Duplicate milestone/selection/settlement history, imported draft rewritten/rerun, jersey intervals lost/rebound to current number, old game rebound to a new stadium/profile, historical cap/sponsor facts rewritten by current policy, authoritative PA/scoring/metric evidence pruned, overlapping imported totals, dangling staff/subject, interpreting missing facts as zero or inventing prior tenure/captain/vote/weather/finance history.
- **Testing:** Historical+save continuity, imported/generated draft boundary, jersey change/transfer/retirement timeline, venue/profile change without historical rewrite, staff/captain/All-Star/finance history, later finance policy/rule version leaves old facts unchanged, imported opening baseline, year/team/role/competition scopes, projection rebuild, retired lookup and D6 archive/profile query plus retained-evidence integrity before/after any targeted maintenance.

## C28 — Team history and franchise records (Phases 11, 23)

- **Responsibility:** One continuous franchise history with titles, finishes, yearly results, notable players and club-specific performance.
- **Inputs:** Team-season/series/award facts, player team stints, imported historical evidence and legacy outcomes.
- **Outputs:** Title counts/detail years, season timeline, franchise leaderboards and notable-player evidence.
- **State ownership:** Team-season history/provenance and franchise projections; current team and awards have other owners.
- **Dependencies:** Stable TeamId, Stats, Season, CareerHistory, RetiredNumbers/Hall projections.
- **Public interfaces:** `FinalizeTeamSeason(TeamSeasonFacts) -> HistoryReceipt`; `QueryFranchise(FranchiseQuery) -> FranchiseView`.
- **Events:** `FranchiseRecordSet`; consumes `TeamWonChampionship`, `PlayerWonAward`, `NumberRetired`.
- **Persistence:** Team/season/competition keys, outcome facts, historical coverage and player-stint aggregates.
- **Failure cases:** Summary count not matching detail list, entire player career attributed to one club, pack update overwriting old titles.
- **Testing:** Six imported plus two new titles, transfers/multiple stints, rename continuity, award/team attribution and filtered years.

## C29 — News and expressive Narrative AI (Phases 5, 26)

- **Responsibility:** Render routine information procedurally and use Gemini for high-value articles and rich connected reporter/fan/SNS/community expression; factual authority remains Game Core.
- **Inputs:** Visibility-filtered confirmed facts, actual stored interview answer/quote references and committed consequences, importance, fictional voice/outlet, approved D5 boundary and request budget.
- **Outputs:** Validated/escaped cached Korean narrative bundles, or explicit pending/retry/failure status. AI-native fan/SNS reactions need no template equivalent and prose never returns gameplay commands.
- **State ownership:** Presentation requests/member status/cache and content metadata only; C43 owns validated interpretation processing, C26/domain owners own facts.
- **Dependencies:** Public fact builder, routine template renderer, Gemini adapter, typed link/reference validation, C43 completion facts and source interviews.
- **Public interfaces:** `BuildPublicFacts(EventId, ViewerContext) -> PublicFactBundle`; `RenderNarrative(PublicFactBundle, PresentationPolicy) -> NarrativeResult`; requests include career/branch/session/fingerprint and actual quote IDs.
- **Events:** Presentation `NarrativeReady`/`NarrativeFailed`; generated expression alone emits no authoritative relationship, injury, result or other domain event.
- **Persistence:** Bundle members/text/fact/answer references/model/prompt/policy/locale/status in cache; reopening uses accepted cache. API secrets stay outside saves/git/logs. A failed member retry preserves completed members.
- **Failure cases:** Unsupported facts/quotes, malformed schema, prompt/markup injection, hidden prompt data, timeout/quota, stale branch response or duplicate generation. Withhold rejected content and offer retry; never invent a successful AI result.
- **Testing:** Zero low-value routine calls; rich expression and voice diversity grounded in supplied facts; false injury/contract/relationship/past-quote adversaries; wrong-save callbacks; cache reuse; post-commit failure cannot reapply interview effects.

## C30 — Retirement (Phases 16, 25)

- **Responsibility:** Routine NPC exit and final controlled-player career closure, farewell, summary and bounded epilogue.
- **Inputs:** Career decision/policy, ability/age/opportunity/health, retained career facts and existing legacy results.
- **Outputs:** Retirement fact, closed playable state, farewell/summary and scheduled epilogue facts.
- **State ownership:** Retirement plan/date/status, career closure and epilogue milestones.
- **Dependencies:** Player, Roster/Contract/C47 release policies, History, RetiredNumbers and Hall evaluation.
- **Public interfaces:** `PlanRetirement(RetirementIntent) -> Result<RetirementPlan>`; `FinalizeRetirement(RetirementContext) -> Result<RetirementDelta>`; `AdvanceEpilogue(EpilogueRequest) -> EpilogueStep`.
- **Events:** `RetirementDecisionRequested`, `PlayerRetired`, `FarewellHeld`, `EpilogueCompleted`.
- **Persistence:** Retirement date/cause, closure revision, pending milestones, summarized reference IDs; player row retained.
- **Failure cases:** Active gameplay after retirement, lost deferred honors, deleting history, infinite post-retirement world loop.
- **Testing:** Early/quiet/star retirement, contract/C47 cleanup, full jersey timeline in summary, save-to-epilogue continuation and locked career actions.

## C31 — Retired numbers (Phase 25)

- **Responsibility:** Independently evaluate franchise legacy and delayed honors for each relevant club.
- **Inputs:** Team-specific tenure/role/records/fans/titles/awards/loyalty/story, meaningful captaincy evidence, jersey history and approved tuning.
- **Outputs:** Candidate/declined/announced club-number honor facts with public qualitative reasons and C47 future-assignment restriction.
- **State ownership:** Hidden legacy case/evaluation, scheduled review and club-number honor record.
- **Dependencies:** Retirement, TeamHistory, Stats, Reputation, C47 and calendar.
- **Public interfaces:** `EvaluateFranchiseLegacy(LegacyContext) -> LegacyCase`; `ResolveNumberHonor(LegacyCaseId, Date) -> Result<HonorDecision>`.
- **Events:** `NumberRetirementConsidered`, `NumberRetired`.
- **Persistence:** Team/player/number/date, score/model version internally, pending review and ceremony facts.
- **Failure cases:** Public raw score, career totals replacing club contribution, captaincy acting as automatic honor, league-wide instead of club-specific restriction, historical assignment invalidated, duplicate number conflict or forced one-team restriction.
- **Testing:** Long backup vs franchise star, captain/noncaptain otherwise-similar cases, league legend without club honor, delayed review, historical use retained/future same-club assignment rejected/other-club use legal, rare multiple-team honor and replay-safe announcement.

## C32 — Hall of Fame (Phase 25)

- **Responsibility:** Evaluate league-wide greatness independently from club-number honors, including NPC candidates.
- **Inputs:** Career stats/WAR/method, ranks, awards, peak/titles/postseason/national career, longevity/fame and eligibility dates.
- **Outputs:** Eligibility/candidacy/induction or rejection facts; public career evidence.
- **State ownership:** Hidden Hall case/score, eligibility/review edition and selection result.
- **Dependencies:** Retirement, CareerHistory/Stats, approved selection rules and calendar.
- **Public interfaces:** `CheckHallEligibility(PlayerId, Date) -> EligibilityResult`; `EvaluateHall(HallContext) -> HallCase`; `ResolveHallEdition(EditionId) -> HallResults`.
- **Events:** `HallCandidateAnnounced`, `PlayerInductedIntoHallOfFame`.
- **Persistence:** Candidate/player/edition/method/result and delayed action; NPC and controlled-player rows share schema.
- **Failure cases:** One-stat guaranteed induction, raw score exposure, missing historical metrics silently zeroed, evaluating before wait period.
- **Testing:** Dominant/borderline/unsuccessful careers, NPC equivalence, delayed eligibility, independent retired-number outcome and duplicate prevention.

## C33 — Save/load and compatibility (Phase 4 onward)

- **Responsibility:** Atomic command persistence, durable snapshots, slots/Continue/load/migration/recovery and branch isolation.
- **Inputs:** Explicit owned changes/events/revision, save request, existing slot/header and version policy.
- **Outputs:** Commit/save receipt, validated loaded session, or recoverable typed problem.
- **State ownership:** Save headers/catalog/generations/migration journal/revision; subsystem table semantics remain with their owners.
- **Dependencies:** SQLite adapter, filesystem snapshot publisher, validators, migration registry; no GUI.
- **Public interfaces:** `Commit(ChangeSet, Events, ExpectedRevision) -> CommitReceipt`; `CreateSnapshot(SaveRequest, Revision) -> Result<SaveReceipt>`; `LoadSnapshot(SlotId) -> Result<ValidatedSession>`.
- **Events:** Application save success/failure/progress; `CareerCreated` is a domain initialization fact.
- **Persistence:** §5 protocol, catalog reconstructible from immutable generations, rule/RNG/allocator/decision/day/game state included. Preserve at least three completed rolling autosave generations, paired pre-decision recovery until successful post-decision save, explicit recovery notices and copy-before-migrate; preferences and D6 storage optimization cannot weaken this floor.
- **Failure cases:** Disk full, stale revision, interrupted backup/publication, corrupt/latest/newer save, migration failure, loaded branch callback collision.
- **Testing:** Fault injection at every boundary, exact next-outcome replay, catalog rebuild, prior-version upgrades, multi-career/slot isolation and unsupported-version preservation. D6 measures working DB plus manual/rolling/pre-decision generations and save/load/backup/migration times before pruning is considered. Imported/generated draft source/completed markers, stable PlayerIds and full C47 intervals round-trip without rerun/duplication. Interview apply receipt/answer round-trip and portrait missing-asset fallback do not weaken commit/snapshot fault-injection tests.

## C34 — Datapacks and asset import (Phases 4, 27)

- **Responsibility:** Validate/materialize initial world data, optional completed draft history/initial jersey assignments/club-finance baselines, optional assets and explicit data-only overlays; never refresh active facts automatically. The personal KBO 2026 pack follows approved D3's immediate-preseason snapshot boundary.
- **Inputs:** Folder/individual files, manifest/schema/rules including SalaryCapRules, optional historical draft records, initial numbers, finance/sponsorship policy assumptions, exact `as_of_date`/evidence cutoff/source provenance and asset references.
- **Outputs:** Validation report, immutable effective pack, initial-world specification with data-driven draft source/completed facts and optional C48 baselines, plus presentation asset catalog.
- **State ownership:** Installed/staged pack metadata; after initialization all mutable content belongs to the career save.
- **Dependencies:** JSON parser/schema checks, explicit ID/reference map, C01/C14/C47/C48 validation, allowed asset loader; no executable mod loader. C40 rule-area validation and C45 cosmetic asset mapping; original P remains the portrait design authority.
- **Public interfaces:** `ValidatePack(PackSource) -> ValidationReport`; `MaterializePack(PackSelection) -> Result<ValidatedPack>`; `CreateWorld(ValidatedPack, CareerStartSpec, Seed) -> WorldInitialization`.
- **Events:** Application `DatapackInstalled`; career initialization records provenance.
- **Persistence:** Static JSON/schema/assets and save snapshots of authoritative initial data, draft provenance/completed records, initial C47 intervals and frozen C48 baseline/policy plus C40 rule provenance; source/content hashes and coverage.
- **Failure cases:** Duplicate/colliding PlayerIds or prohibited picks, missing player/drafting club/origin, impossible dates/round/pick, inconsistent signing/registration/club facts, post-cutoff 2026 evidence in the preseason world or ratings, missing cutoff/provenance at personal-pack certification, initial active/retired number conflict, invalid finance effective date/range/reference/provenance, unsourced real amount presented as verified, broken references, invalid enums/ranges, incompatible versions, unsafe paths, corrupt/missing assets or ambiguous overlay precedence.
- **Testing:** Optional draft history/finance baseline absent; arbitrary one/multiple imported or fictional completed classes; D3 preseason cutoff and no-future-evidence fixture; invalid reference/finance/rule conflict blocks all publication with file/entity/field path; imported class never reruns; missing future class routes to generated pipeline; initial number imports. Fictional finance assumptions remain labeled and real/estimated/verified provenance is explicit. Also every prior validator class, missing optional image fallback, invalid import rollback and original-pack deletion after career start; catcher/rule/profile defaults, real-photo precedence, Seed/AI references and missing-image fallback without changing player facts.

## C35 — Player database and common profile (Phases 6, 24)

- **Responsibility:** Search active/second-team/generated/prospect/imported-rookie/released/retired players and expose one privacy-safe profile.
- **Inputs:** Viewer-scoped query, name/team/position/age/role/level/status/origin plus rookie/draft-year/round filters, sort/page and selected PlayerId.
- **Outputs:** Bounded public list/profile tabs with C14 draft and C47 number summaries and consistent link targets. C44 comparisons reuse these exact visibility projections; C45 supplies cosmetic portraits without granting hidden ability data.
- **State ownership:** No authoritative gameplay state; navigation/query cache is UI/session state.
- **Dependencies:** Application visibility policy and read repositories for owned domains, including C14/C27/C47.
- **Public interfaces:** `SearchPlayers(PlayerSearchQuery, ViewerContext) -> PlayerPage`; `GetPlayerProfile(PlayerId, Tab, ViewerContext) -> ProfileView`.
- **Events:** None; query lifecycle notifications only.
- **Persistence:** Indexes and optional projection caches; filter/scroll preferences separate from core state.
- **Failure cases:** Hidden truth in DTO/sort key, missing ID, stale callback, N+1 reads, all rows materialized as controls.
- **Testing:** Combined/rookie/draft filters, imported rookie selection result, duplicate names, retired profiles, draft/jersey history, missing tabs, no private fields and restored navigation at both resolutions. Self-vs-NPC and NPC-vs-NPC comparisons, inaccessible private sort keys, favorites never alter visibility.

## C36 — Team database and common profile (Phases 6, 24)

- **Responsibility:** Discover franchises and display current rosters/rank plus continuous team, jersey, stadium, staff, captain and read-only C48 financial history without management commands.
- **Inputs:** TeamId, season/history/record/roster filter and page state.
- **Outputs:** Overview/history/season/roster/records/awards/retired-number read models, current/historical jersey, stadium/staff/captain links, C48 total payroll/cap/budget/sponsorship summaries and drill-down lists.
- **State ownership:** No authoritative state; caches carry source revision.
- **Dependencies:** Team/History/Stats/Roster/Legacy/C46/C47/C48 read models and PlayerLink/TeamLink/StadiumLink routes.
- **Public interfaces:** `ListTeams(TeamQuery) -> TeamPage`; `GetTeamProfile(TeamId, TeamTabQuery) -> TeamProfileView`.
- **Events:** None; projection availability notifications only.
- **Persistence:** Indexed queries/projections and separate route state.
- **Failure cases:** Title counts disagree with years, renamed team loses history, current number/stadium/staff/finance policy replaces historical context, a retired number is shown league-wide, private AI score exposed, management command offered, or first/second/developmental/military filters are conflated.
- **Testing:** Current/historical separation, 6+2 titles drill-down, active/retired jersey plus stadium/staff/captain/finance history links, C48 read-only summary reconciliation and large KRW labels, roster filters, team→player→back context restore and FHD information parity.

## C37 — Agents (Phases 14, 22)

- **Responsibility:** Support negotiation, marketing, information and career advice while preserving player final authority.
- **Inputs:** Agent type/skills/fees/trust/reputation, valid offers, player priorities and relevant public career facts.
- **Outputs:** Negotiated proposal/comparison/advice and commission obligations after accepted agreements.
- **State ownership:** Agent profile/agreement and service relationship; C23 owns personal relationship edge.
- **Dependencies:** Contracts, FA, Commercial, Assets and Relationships.
- **Public interfaces:** `HireAgent(AgentChoice) -> Result<AgentAgreement>`; `Advise(OfferComparisonContext) -> AgentAdvice`; `Negotiate(AuthorizedNegotiationContext) -> OfferProposal`.
- **Events:** `AgentHired`, `AgentAgreementEnded`; fee owner emits `CommissionPaid`.
- **Persistence:** Agent identities/agreement periods/fee terms and advice reference, no automatic signing permission.
- **Failure cases:** Agent signing for player, applying current fee to old agreement, duplicated commission or advice exposing hidden truth.
- **Testing:** No-agent early career, type differences, hire/end, fee lifecycle, advice cannot force offer choice.

## C38 — Individual player advertising and sponsors (Phase 22)

- **Responsibility:** Create player-approved individual commercial opportunities with explicit image/equipment/time obligations and benefits; separate from C48 club sponsorship revenue.
- **Inputs:** Performance/fame/popularity/image/national career, brand criteria, agent and player decision.
- **Outputs:** Offers, accepted agreements, scheduled appearances, payments and equipment conditions.
- **State ownership:** Commercial terms/status/obligations; actual wallet, calendar and equipment ownership use their owners.
- **Dependencies:** Agent, Assets, Life/calendar, Reputation and narrative fact publisher.
- **Public interfaces:** `GenerateCommercialOffers(CommercialContext) -> OfferBatch`; `AcceptCommercialOffer(OfferId) -> Result<CommercialPlan>`; `CompleteAppearance(ObligationId) -> Result<EffectBatch>`.
- **Events:** `CommercialOfferReceived`, `AdvertisementSigned`, `SponsorSigned`, `CommercialAppearanceCompleted`, `CommercialAgreementExpired`.
- **Persistence:** Commercial agreements, requirement snapshots, scheduled obligations and receipts/history.
- **Failure cases:** Offer creates income before acceptance, conflicting equipment brands/time, duplicated fees, excessive ability bonus, or C48 club sponsorship misposted as player income/control.
- **Testing:** Ad/sponsor lifecycles, refusal, obligation conflicts, money/time/fatigue reconciliation and saved outstanding obligations.

## C39 — Developmental registration and undrafted paths (Phase 13)

- **Responsibility:** Preserve origin while moving undrafted players through university/developmental/other/end paths and professional registration.
- **Inputs:** Draft result, age/origin/eligibility, club demand/offer, player intention, current development and approved rules.
- **Outputs:** Entry/status transition, contract request, conversion/release fact and dated professional history.
- **State ownership:** Entry-path history and transition receipts; Player owns origin, Roster owns current registration/level, Contract owns terms.
- **Dependencies:** Draft, Feeder, Scouting, Roster, Contract and Player; high potential is permitted regardless of entry path.
- **Public interfaces:** `ResolveUndraftedPath(UndraftedContext) -> Result<EntryPathPlan>`; `SignDevelopmental(EntryOfferId) -> Result<EntryActivation>`; `ConvertRegistration(ConversionProposal) -> Result<RegistrationDelta>`.
- **Events:** `PlayerSignedAsDevelopmental`, `PlayerEnteredUniversity`, `PlayerRegistered`, `PlayerReleased`, `PlayerLeftBaseball`.
- **Persistence:** `entry_history`, draft status and conversion/release dates; no separate `development_players.json` or separate player identity.
- **Failure cases:** Every undrafted player auto-signed, university prospect duplicated, developmental status confused with level, potential lowered because of entry.
- **Testing:** All source-defined undrafted paths, developmental→registered→first-team debut, retained original school and same PlayerId, high-potential late success.

## C40 — KBO Rule Engine (Phases 1–3, 6, 9–16)

- **Responsibility:** Single conceptual owner of typed, data-driven/versioned GameRules, RegularSeasonRules, RosterRules, RegistrationRules, PostseasonRules, DraftRules, ContractRules including SalaryCapRules, FARules, ForeignPlayerRules and AwardRules; no monolithic service or extra process. SalaryCapRules own competition/season scope, threshold, included players/components, exemptions, calculation date/period, assessment procedure and prohibited/penalty/other excess consequence. DraftRules govern generated live drafts and jersey legality/approved exceptions belongs to the applicable roster/legacy profile; imported completed draft facts remain C14/C34 content and are never rerun. Weather interruption/makeup and configured All-Star selection are typed subsets of existing season/award areas, not new scattered rule owners.
- **Inputs:** Validated RuleProfile area snapshots, edition/effective dates, relevant typed gameplay/eligibility context; approved D2's 2026 KBO-oriented production baseline and explicit simplification records.
- **Outputs:** Resolved GameRuleSnapshot or area-specific typed evaluation/constraints/reason codes with source version; missing/unsupported versions are explicit errors.
- **State ownership:** Rule definitions/profile composition and immutable career rule snapshots; consumers own resulting state transitions.
- **Dependencies:** Domain invariants, datapack validation/freeze and save compatibility; no UI/HTTP/live-rule refresh.
- **Public interfaces:** `ResolveGameRules(RuleProfileId, GameContext) -> Result<GameRuleSnapshot>`; typed area operations such as `EvaluateRegistration(RegistrationContext, RegistrationRules) -> EligibilityResult` and `AssessSalaryCap(SalaryCapContext, SalaryCapRules) -> Result<SalaryCapAssessment>`; avoid untyped catch-all rule scripting.
- **Events:** Consumers emit factual effects with evaluated rule version; merely reading rules emits no gameplay event.
- **Persistence:** `rule_snapshots` stores area version/content hash/effective dates and documented simplifications; saves freeze rules and migration preserves prior behavior or explicitly versions compatibility. Later real-world changes never mutate existing careers. No hardcoded 2026 values scattered in domain classes.
- **Failure cases:** Conflicting areas, unsupported edition, missing rule subset, undocumented simplification, salary-cap threshold/procedure scattered or guessed, every excess treated as a hard prohibition, sponsorship changing the legal threshold, live pack replacement, duplicated service-side eligibility constants or invented real-world assumptions.
- **Testing:** Area validation/version lookup in Phase 1; legality in 2; differing rule-profile fixtures per consuming phase, including generated draft, jersey range/retired exception, weather/makeup/All-Star procedure, annual salary renewal and SalaryCapRules inclusion/exclusion/threshold/excess consequence; imported completed draft bypasses selection but not validation; save/load and original-pack edit independence; frozen-career behavior after installing a later rule pack; detailed subset/simplification evidence at affected gates.
- **Pitch Zone Lite (Phase 3, ROADMAP §1.14; the data contract `CalledPitchMode` / `gamerules-v2` is implemented in Phase 3-B1, see the C08 status line and `docs/game-engine.md` section 18; Framing behaviour is 3-E and not implemented):** C40 owns a versioned called-pitch mode in GameRules. Automatic (ABS-style) mode: Framing probability effect exactly 0. Non-automatic mode: Framing acts only on predefined `Borderline` pitches within a cap; an already committed call never changes. Each rule carries RuleProfile version and provenance. The legacy `GameRuleSnapshot.ContentHash` convention and the legacy profile's hash stay byte-identical; new fields ship in a separately versioned profile and an existing hash is never silently changed. The `KboOrientedUnverified` status and the absence of KBO/ABS certification are preserved; no real 2026 ABS rule is claimed.
- **Phase 2 implementation status (GameRules only):** `GameRuleSnapshot` (hash, status `KboOrientedUnverified`/`FictionalTest`, provenance, open items, counts, regulation/extra innings, DH, two-way, decision values, `DhMayBeForfeited`, `CountWalkOffRunnersAsLeftOnBase`) loaded from `content/rules/game_rules_profiles.json`. Rule-source policy KBO first / 2026 OBR fallback (`RulePolicyId`) and the per-rule status table (`content/rules/game_rules_evidence.json`, statuses KBO_VERIFIED / OBR_FALLBACK_APPROVED / GAME_SIMPLIFICATION_APPROVED / UNRESOLVED): `docs/game-engine.md` section 5. Only the 11-inning limit is KBO_VERIFIED; the tie at the limit (R02) is applied on the owner's approval (2026-10-11) with operating evidence and no certified clause (`GAME_SIMPLIFICATION_APPROVED`); OBR fallbacks are compared with the original 2026 OBR text (`obr_comparison`; the six differences found were fixed on 2026-10-11, C33 and C38 await the owner) and are not KBO certification. RegularSeasonRules, SalaryCapRules and the other areas are not implemented.

## C41 — Battery Chemistry (Phases 1, 3, 7–9, 21)

- **Responsibility:** Maintain baseball-specific pitcher-catcher familiarity and provide bounded effective coordination modifiers; independent of personal friendship, manager trust, C proficiency and base skills.
- **Inputs:** Role-ordered pitcher/catcher IDs, actual shared game exposure, represented camp/bullpen work, repeated-season/high-pressure evidence, elapsed inactivity and optional modest represented-relationship growth modifier from Phase 21.
- **Outputs:** Pair-specific progression delta and bounded communication/receiving/blocking/Game Calling/execution-stability context; public projection follows observation policy, never private skill disclosure.
- **State ownership:** `catcher_id`, `pitcher_id`, bounded chemistry, `shared_appearances`, `last_battery_date`, update cursor/exposure receipts/tuning version; no global chemistry field on catcher.
- **Dependencies:** Actual C08/C03 exposure, C05 practice, rule/tuning snapshot and date; C23 modifier is optional and neutral until implemented, not a new NPC friendship graph.
- **Public interfaces:** `GetBatteryContext(BatteryPairId) -> BatteryContext`; `ApplyBatteryExposure(BatteryExposure) -> Result<BatteryDelta>`; `UpdateInactivity(BatteryDateContext) -> BatteryDelta` under documented slow-decay tuning.
- **Events:** `BatteryExposureRecorded`, `BatteryChemistryChanged`; historical personal-catcher patterns may be derived later, not commands setting an inviolable pairing.
- **Persistence:** Unique `(catcher_id, pitcher_id)` row and pair/source exposure receipt; retain dated metadata across transfers/retirement where history references it. Missing historical pairs initialize neutral/unknown without invented appearances.
- **Failure cases:** Duplicate/shared game counted twice, same-player pair, global catcher score, excessive decay/churn, friendship automatically setting elite chemistry, or permanent Stuff/base-rating inflation.
- **Testing:** One catcher/three pitchers differ; exact overlap attribution and repeated exposure idempotency; camp/game/pressure/slow-decay caps; save replay; base ratings unchanged; emergent Phase 9 pairings respect health/fatigue.

## C42 — Pending Decisions, contextual help and settings (Phases 5–6; later content)

- **Responsibility:** Expose existing authoritative barriers in a compact Korean Inbox and supply nonintrusive contextual help and capability-driven settings; no email simulator or forced tutorial campaign.
- **Inputs:** Persisted decision/status/deadline/stop reason, confirmed first-time events, metric/rule glossary keys, implemented capability flags and user preferences.
- **Outputs:** `PendingDecisionView` plus count/routes, notices/hover-or-focus tooltips/reopenable glossary, valid settings UI; ordinary notifications remain distinct from unresolved choices.
- **State ownership:** Presentation preferences only: help seen/suppressed/reset, display/text scale/speeds/default modes/confirmation/allowed autosave options. Decision facts and expiry stay in C26/subsystem owners; audio/Gemini/portrait controls exist only with the feature.
- **Dependencies:** Calendar/query/command boundary, authoritative decision owners, versioned rule descriptions and UI routing; C43 supplies AI-interview states later.
- **Public interfaces:** `GetPendingDecisions(ViewerContext) -> PendingDecisionView[]`; `OpenDecision(DecisionId) -> Route`; `GetHelp(HelpTopic, PublicContext) -> HelpView`; `UpdatePreferences(PreferenceChange, SettingsCapabilities) -> Result<UserPreferences>`.
- **Events:** Help/settings/navigation notifications are presentation-only; resolving/expiring a decision still uses its existing domain command and receipt.
- **Persistence:** Versioned local preferences scoped to user/career where appropriate, outside simulation state/hash; Inbox queries durable decisions. Closing UI cannot accept/expire a decision. Preference corruption resets safely without modifying a career.
- **Failure cases:** Silent disappearance on advance, hidden important interview, duplicated Inbox truth, missing reopen/reset, unavailable settings, or autosave controls disabling mandatory snapshots/three-generation minimum.
- **Testing:** Annual salary/contract/FA/care/national/military/interview items as owners ship; deadline/explicit expiry and reload parity; first injury/second-team/FA/developmental notices; metric/park-factor help; suppress/reset/reopen; QHD/FHD Korean accessibility and no RNG/state change.

## C43 — AI-native interview interpretation and core consequences (Phases 21, 26)

- **Responsibility:** Accept player free text, validate untrusted structured Gemini meaning, and coordinate Game Core-owned consequences exactly once; generated narrative prose is not interpreted as commands.
- **Inputs:** C26 confirmed request/context, original Korean answer/revision/hash, finite allowed target/topic/tone/intensity/sentiment/tags/references, provider envelope bound to career/branch/session/interview/fact/schema/rule versions.
- **Outputs:** Validated interpretation or rejected/pending/retry state; core-computed effect batch and atomic application receipt; confirmed context/original answer/committed consequences for C29 expressive reactions.
- **State ownership:** Interview answers, request identity/status, accepted meaning and apply-once receipt; actual relationship/reputation values belong to their existing core owners and cannot be chosen numerically by Gemini.
- **Dependencies:** C26 rules/context, strict Application validator, Infrastructure provider, command queue/unit of work, public fact builder, domain effect owners and C42 Inbox.
- **Public interfaces:** `SubmitInterviewAnswer(InterviewId, AnswerRevision, RawText) -> Result<PendingInterpretation>`; `ValidateAndApplyInterview(InterpretationEnvelope) -> Result<InterviewOutcome>`; `RetryInterview(InterviewId, AnswerRevision) -> Result<RequestStatus>`.
- **Events:** `InterviewAnswerSubmitted`, `InterviewConsequencesApplied`, `InterviewCompleted` plus core-owned effect events after atomic commit; validation/API failure has no authoritative consequence event.
- **Persistence:** Preserve raw submitted text/context; accepted interpretation/effects/receipt commit together. Unique applied interview per career branch forbids repeated effects across answer revisions. Loading/forking a snapshot preserves inherited completion/receipts; a new branch ID never reopens an applied interview. Unapplied edits invalidate old callbacks; applied answers are immutable history. Replay consumes saved meaning without an LLM; secrets are excluded.
- **Failure cases:** Unsupported tags/ranges/targets/factual references or numeric mutation fields, invented past quote, stale answer/fact/session, duplicate/concurrent application, timeout/no key/quota, input prompt injection. Remain visibly pending/retryable as appropriate with no fake template answer or partial mutation.
- **Testing:** Exact raw-answer retention; malformed/unsupported/invented references rejected; changed-save/answer callbacks rejected; retry/reload/race applies once; failed post-commit reaction does not repeat effects; recorded-meaning deterministic replay; secret/privacy audit and no interpretation before Phase 26.

## C44 — Discovery QoL and historical read models (Phases 23–24)

- **Responsibility:** Favorites/watchlist for players/prospects, two-player comparison, Game Archive and milestone progress over retained facts; no additional simulator or RPG quest system.
- **Inputs:** Viewer context, stable career/branch/player IDs, existing public player projections, retained game/stat/history/coverage and milestone definitions.
- **Outputs:** Scoped watchlist, visibility-safe comparison, filtered historical game pages and milestone current/target/remaining/availability views.
- **State ownership:** Watchlist is user preference data, never simulation truth; archive/comparison/progress are rebuildable read models and navigation state.
- **Dependencies:** C11/C27/C35/C36, common routes/links and C42 preferences; no direct hidden-rating or true-potential access.
- **Public interfaces:** `SetFavorite(PlayerId, CareerScope, bool) -> PreferenceReceipt`; `ComparePlayers(PlayerId, PlayerId, ViewerContext) -> PlayerComparisonView`; `QueryGameArchive(GameArchiveQuery) -> GamePage`; `GetMilestoneProgress(PlayerId, Scope) -> MilestoneProgress[]`.
- **Events:** No gameplay events for viewing/bookmarking/comparing; C27 emits actual milestone occurrence once when facts cross a threshold.
- **Persistence:** Favorites in separately versioned preferences scoped by career/branch; query indexes/cache carry source revision. Archive uses existing retained score/box score/lineup/scoring progression/own appearances/important events and marks unavailable detail.
- **Failure cases:** Favorites leaking across same PlayerId in different saves, comparison revealing NPC truth, fabricated historical detail, double-counted career baselines, negative remaining count or unknown history shown as zero.
- **Testing:** Self/NPC and NPC/NPC comparison privacy, bookmark/reload/isolation, season/date/team/postseason/own-appearance filters, archive read-only hash, 1,873→127 hits and 487→13 HR progress, surpassed and incomplete totals.

## C45 — Portrait reference and application boundary (Phases 24, 26–29)

- **Responsibility:** Integrate the independent [P specification](../Star_Player_Portrait_System_Design.MD) into common profiles: offline Seed 2D base first, then optional explicitly user-selected Gemini images. P owns portrait feature detail.
- **Inputs:** PlayerId/save scope, unique cosmetic seed and renderer/part-set version, public appearance descriptors, club/national/neutral context, supplied photo and explicit user target/generate/regenerate choice.
- **Outputs:** Local rendered/cached portrait with Seed/neutral fallback, generation confirmation/status and per-player results; no gameplay facts or hidden-rating payload.
- **State ownership:** Cosmetic seed/mode/path/context metadata and P request/model/prompt/time/status fields; image assets scoped to career/branch; face identity separate from kit/context. Seed data survives AI use/deletion.
- **Dependencies:** C35 profiles/C34 asset validation, separate cosmetic allocator (no gameplay RNG), existing safe asset publication and Phase 26 Gemini image capability; P §34 local steps map to roadmap 24 then 26 then 28–29.
- **Public interfaces:** `ResolvePortrait(PlayerId, PortraitContext, CareerScope) -> PortraitView`; `RequestPortraits(ExplicitPortraitSelection) -> Result<PortraitJob>`; `RetryFailedPortraits(JobSelection) -> Result<PortraitJob>`; `RestoreSeedPortrait(Selection) -> Result<PortraitPreference>`.
- **Events:** Presentation-only status notifications; no automatic calls on games, awards, selection, aging, profile open or save load. Narrative request eligibility never authorizes image generation.
- **Persistence:** Keep unchanged P and save seed/renderer version plus local immutable asset references; WebP cache keyed by safe PlayerId/context and save branch, no names as keys. Publish/validate image before reference commit, preserve old assets and never store API keys in saves/git/logs.
- **Failure cases:** Missing/corrupt AI file→Seed; missing club/national asset→neutral; partial batch maps per player and retries only failures; unsupported Batch/provider is an explicit incomplete subgate, not silent automatic alternative calls.
- **Testing:** P seed/context/mode/fallback cases in 24; explicit single then Batch/cost/large-selection confirmation, 47/3 partial results, no automatic API, restart/cache reuse, same-ID save isolation and prior-image preservation in 26; import precedence and QHD/FHD polish in 27–29.

## C46 — Stadium and Park Factor context (Phases 1, 3, 6, 11, 23–24)

- **Responsibility:** Own stable stadium identity, separated presentation metadata, dated club/fixture usage and bounded versioned ParkFactor profiles while preserving the exact venue context used by historical games.
- **Inputs:** Validated `StadiumId`, name/location/capacity/asset references, dated home/usage relationships, bounded HR/hit/XBH or other approved factor values, profile version/effective dates and fixture/game context.
- **Outputs:** Stadium/public detail views, valid fixture venue assignment and immutable `ParkFactorSnapshot` for game resolution or versioned historical metrics.
- **State ownership:** Stadium identity/presentation, ParkFactorProfile definitions/versions and dated usage. C09 owns fixture status/weather/reschedule links; C08 owns finalized game state/outcome; C11 owns metric formulas. C46 never owns or mutates player ratings.
- **Dependencies:** Stable TeamId/competition/fixture references, datapack validation/freeze, C40 schedule rules, save compatibility and asset fallback; no meteorological or architectural stadium simulator.
- **Public interfaces:** `GetStadium(StadiumId) -> Result<Stadium>`; `ResolveFixtureVenue(FixtureContext) -> Result<StadiumId>`; `ResolveParkFactors(StadiumId, GameDate, ProfileVersion) -> Result<ParkFactorSnapshot>`; `GetStadiumHistory(TeamId) -> StadiumUsageView`.
- **Events:** `StadiumUsageChanged` for meaningful dated club usage; fixture/game owners include resolved StadiumId/profile version in their factual events. Merely reading a stadium emits no event.
- **Persistence:** `stadiums`, `park_factor_profiles`, `stadium_usage`; fixtures store StadiumId and finalized games retain resolved profile/version/content hash needed for replay/history. New definitions never rewrite old results; missing presentation assets use ordinary fallback.
- **Failure cases:** Missing/ambiguous venue, factor outside approved cap, overlapping incompatible usage, club move rebinding old games, current profile used for an old game, park value copied into player ratings or optional image failure blocking simulation.
- **Testing:** Stable ID across rename/asset changes; neutral/pitcher/hitter profile validation and caps; fixture assignment; club venue change with unchanged old-game hash/context; profile-version change without historical recalculation; park-adjusted metric uses retained context; missing image fallback and save/pack independence.
- **Phase 3-B2 probability contract (reserved, not active):** the C08 probability model (`prob-model-v1`, `docs/decisions/ADR-006.md`) declares three **reserved, neutral-input-only** slots for a resolved `ParkFactorSnapshot`: `park.home_runs` (owns the `home_run` outcome of `defense_result`), `park.hits` (owns `single`) and `park.extra_base_hits` (owns `double` and `triple`). The three own disjoint outcomes so no hit type is counted twice, and a park value is an input (permille 1-3000, 1000 neutral) that never touches player ratings. Their tuning has no numeric authority (the caps are set by Phase 3-D under the combined cap) and a non-neutral input is rejected until then. **Open owner point:** this mapping reads the C46 `Hits` coefficient as singles only to keep the three disjoint; whether `Hits` means all hits must be confirmed before the slots are enabled.

## C47 — Jersey number assignments (Phases 1, 4, 9–10, 13, 15–17, 23–25, 29)

- **Responsibility:** Own each jersey number as an effective-dated PlayerId–ClubId assignment, including import, rookie/generated assignment, same-club change, release/transfer/FA closure, historical query and club-specific retired-number enforcement.
- **Inputs:** Stable PlayerId/ClubId, requested or imported number, effective date, assignment reason, roster/affiliation transition, legal number policy, current overlapping assignments and C31 club-retired restrictions.
- **Outputs:** Current assignment/history, available alternatives, or an atomic validated close/open transition. AI/NPC fallback selects deterministically from the legal ordered set; controlled-player requests are never guaranteed.
- **State ownership:** `JerseyNumberAssignment(player_id, club_id, jersey_number, effective_from, effective_to, assignment_reason)` and its command receipt. Player owns no timeless/current-number field; C31 owns honor decisions/restrictions, and roster/contract owners coordinate affiliation changes.
- **Dependencies:** Player/Club stable identities, Roster/Registration, C13/C18/C19 movement, C31 retired numbers, C34 import validation, C27 history and saved rule/profile data.
- **Public interfaces:** `AssignNumber(AssignmentRequest) -> Result<JerseyNumberAssignment>`; `ChangeNumber(NumberChangeRequest) -> Result<NumberTransition>`; `CloseAssignment(PlayerId, ClubId, Date, Reason) -> Result<NumberTransition>`; `GetAvailableNumbers(ClubId, Date) -> NumberAvailability`; `GetNumberHistory(PlayerId) -> JerseyNumberHistory`.
- **Events:** `JerseyNumberAssigned`, `JerseyNumberChanged`, `JerseyNumberReleased`; C31 separately emits `NumberRetired`. First/second-team movement inside the same club emits no change unless an approved rule requires one.
- **Persistence:** `jersey_number_assignments` with stable PlayerId/ClubId, number, inclusive/exclusive effective interval, reason and unique command receipt; interval/index constraints support one active player assignment and prohibit disallowed overlapping club-number use. Historical rows are permanent and remain valid after later retirement.
- **Failure cases:** Invalid range, missing identity, overlapping active duplicate, two open assignments for one player/club context, retired-number assignment without an explicitly approved exception, stale/repeated change, previous interval left open, partial transfer/FA/entry transaction, or migration storing only current number.
- **Testing:** Imported initial number; generated rookie legal fallback; controlled request/alternatives; duplicate and retired rejection without mutation; same-club change closes prior interval; transfer/FA/release closes old and independently assigns new; released number legal reuse; historical use survives retirement; save/load/idempotent retry/migration and 20/50-season timeline coherence.

## C48 — Club Finance Lite (Phases 9, 11, 14–17, 23–24, 27, 29)

- **Responsibility:** Provide lightweight autonomous-club payroll projection, internal player budgets, KBO salary-cap assessment, abstract club sponsorship settlement/renewal and financial decision inputs without becoming a franchise accounting or playable GM system.
- **Inputs:** C13 authoritative current/future contract obligations and components, proposed signing/renewal/trade/release terms, C40 career-frozen SalaryCapRules, C10 finalized regular-season/postseason/championship evidence, recent and multi-season results, club popularity/reputation, validated baseline/provenance, prior agreement/revenue, and bounded C06 ClubPhilosophy/ClubFinancePolicy parameters.
- **Outputs:** `ClubPayrollProjection` separating total contracted payroll, salary-cap chargeable payroll and planned/remaining player budget; rule-defined cap assessment/consequence; exactly-once base/bonus sponsorship settlements; bounded renewal/allocation; `ClubFinancialSummary` and explainable typed inputs for C13/C18/C19 Club AI decisions.
- **State ownership:** Versioned `ClubFinancePolicy`, initial assumptions/provenance, annual player-budget allocations, abstract `ClubSponsorshipAgreement` periods/terms, realized `SponsorshipSettlement` evidence/receipts and historically consequential `SalaryCapAssessment`. C13 alone owns player contracts/obligations; C25 owns the player's ledger; C10 owns achievement facts; C38 owns individual player sponsorship. Derived payroll caches are rebuildable, versioned projections.
- **Dependencies:** C13 contracts, C40 SalaryCapRules, C10 committed achievement facts, C06 bounded policy preferences, save/unit-of-work and History/read models. Phase 11 never calls or depends on C48; after Phase 14, C48 consumes already committed C10 facts. Gemini, generated prose and wall-clock randomness are forbidden decision inputs.
- **Public interfaces:** `ProjectPayroll(ClubId, SeasonId, ProposedObligationSet?) -> Result<ClubPayrollProjection>`; `AssessClubFinance(ClubFinanceContext) -> Result<ClubFinancialAssessment>`; `SettleSponsorship(SponsorshipSettlementContext) -> Result<SponsorshipSettlement>`; `RenewSponsorship(SponsorshipRenewalContext) -> Result<ClubSponsorshipAgreement>`; `GetClubFinancialSummary(ClubId, SeasonId, ViewerContext) -> ClubFinancialSummary`.
- **Events:** `ClubBudgetAllocated`, `SalaryCapAssessed`, `ClubSponsorshipSettled`, `ClubSponsorshipRenewed`; C13/C18/C19 emit contract/market/transfer facts. Projections and screen opens emit no financial event.
- **Persistence:** `club_finance_policies`, `club_budget_allocations`, `club_sponsorship_agreements`, `club_sponsorship_settlements`, `salary_cap_assessments` with club/season/effective period, rule/policy versions, source provenance, checked integer KRW, achievement/obligation references and unique command/condition/evidence receipts. Save/load and migration preserve historical assessments/settlements; later rules, budgets or policies never rewrite them. Do not copy contract terms or salary payments into these tables.
- **Failure cases:** Overflow/negative/impossible obligation, total/cap/budget conflation, duplicated contract expense/payment/revenue settlement, bonus before confirmed condition, repeat closure/reload/rebuild award, sponsorship automatically raising the legal cap, cap room inventing budget, excess always hard-rejected despite saved rule, binding salary erased by financial trouble, stale assessment, partial signing/trade/renewal, fabricated official real-world amount, runaway renewal/wealth or permanent poverty, player/LLM club-finance command.
- **Testing:** Exact annual obligation sum and multi-year projection; signing/release/trade updates; C25/C48 reference one C13 obligation without double payment; component inclusion/exclusion, threshold/excess and configured consequence under frozen rules; sponsor revenue leaves cap threshold unchanged; base/regular/postseason/champion bonuses settle once and only from finalized C10 facts; repeated closure/reload/projection is idempotent; renewal/allocation obeys min/max/change/smoothing bounds; identical state/seed/policy matches; cautious/aggressive philosophies differ within caps; transaction rollback; save/recovery/migration/history stability; five 20-season and three 50-season distributions show bounded payroll/revenue/wealth, sustainable AI commitments and recovery from weak seasons.

