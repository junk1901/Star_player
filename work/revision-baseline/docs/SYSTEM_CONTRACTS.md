# Star Player System Contracts

Companion to [ROADMAP.md](../ROADMAP.md). Product authority remains the two source documents and direct master request. These are proposed architectural contracts, not implemented classes or SQL. Interface notation fixes responsibilities and typed inputs/outputs; local helper names may change without altering ownership.

## Shared conventions

All mutating use cases are called through the Application command queue with a command ID and expected revision. Domain/Simulation functions receive explicit snapshots/rules/RNG and return a validated delta plus events; only the application unit of work persists it. No domain service writes another service's rows. Cross-owner changes are one coordinated transaction, not a chain of unrelated commits. Read queries have viewer context and pagination where applicable. Rejected operations return stable error codes and do not mutate state.

In this file, `Result<T>` means a successful typed value or typed validation/conflict/failure outcome. `Delta<T>` means a proposed new state plus factual events, not a publicly editable patch. Deterministic operations include their simulation/rule/tuning/RNG versions. Every durable owner adds migrations, round-trip tests and failure tests in its phase. Internal events may carry private simulation data only inside the core; presentation projections always use explicit allowlists. Required history/stat facts commit with their causes; optional prose is asynchronous.

## C01 — Player identity and career start (Phases 1, 5, 13)

- **Responsibility:** Maintain one identity across amateur, professional, released and retired life; create or select the single controlled player.
- **Inputs:** Validated profile/start specification, date, source identity, allowed personality choice.
- **Outputs:** Player identity/profile and career binding; `PlayerPublicView` through visibility policy; an automatically derived skill-based archetype label without a second bonus.
- **State ownership:** Birth/profile/handedness/body/personality, ordered origins, career status; no ratings/contract ownership.
- **Dependencies:** ID allocator, rule/start policy, Datapacks; registration/contract coordinated separately.
- **Public interfaces:** `CreatePlayer(ProfileSpec) -> Result<Player>`; `StartCareer(CareerStartSpec) -> Result<CareerBinding>`; `GetPlayer(PlayerId, ViewerContext) -> PlayerPublicView`.
- **Events:** `PlayerCreated`, `CareerStarted`, `PlayerProfileChanged`.
- **Persistence:** `players`, `player_origins`, career controlled ID; imported source ID/provenance preserved.
- **Failure cases:** Duplicate ID, invalid date, incompatible source/profile, selection of an unplayable status, changing controlled player midcareer.
- **Testing:** Same-name players, age boundary, existing-start preservation except personality, all origins, retained identity after draft/transfer/retirement.

## C02 — Ratings and effective ability (Phases 1, 3, 7)

- **Responsibility:** Maintain current base skills and calculate temporary effective performance without confusing them.
- **Inputs:** Current rating vector, state modifiers, matchup, proficiency, park/strategy context.
- **Outputs:** Effective skill snapshot and separately approved base-rating deltas; privacy-safe current evaluation.
- **State ownership:** Current 0–100 skills and change provenance; hidden developmental parameters belong to C04.
- **Dependencies:** Player, PlayerState, Proficiency, approved tuning; no statistics-to-rating overwrite.
- **Public interfaces:** `EvaluateEffectiveAbility(AbilityContext) -> EffectiveAbility`; `ApplyBaseChange(AuthorizedAbilityDelta) -> Result<Ratings>`.
- **Events:** `PlayerAbilityChanged` with reason/version; public projection excludes hidden causal inputs.
- **Persistence:** `ratings`, skill-change history; transient effective values are recomputed.
- **Failure cases:** Out-of-range/NaN inputs, unknown skill, stale deltas, changing base ability for a temporary injury.
- **Testing:** Bounds, independent modifier effects, state changes leave base skills intact, cheat current ratings remain 0–100, no hidden UI values.

## C03 — Player state and workload (Phases 5, 7–10, 19–22)

- **Responsibility:** Evolve condition, fatigue, sharpness, stress, satisfaction and recent form; account for dated exposure.
- **Inputs:** Pitch counts, PA, fielding innings, travel, training/life load, rest, availability and date.
- **Outputs:** State delta, workload summary and risk inputs; self precise/NPC observed projection.
- **State ownership:** Current transient state and dated workload, not base skill or medical episode.
- **Dependencies:** Player, schedule, typed effects from injury/training/life.
- **Public interfaces:** `ApplyExposure(WorkloadExposure) -> Result<StateDelta>`; `Recover(RecoveryContext) -> StateDelta`.
- **Events:** `PlayerStateChanged`, `WorkloadRecorded`.
- **Persistence:** `player_states`, `workload_days`, unique exposure receipt; retain rolling windows and lifetime inputs actually required.
- **Failure cases:** Duplicate exposure, negative duration, two-way load omission, treating two competitions as two independent bodies.
- **Testing:** Workload additivity, exactly-once recovery, pitching+batting+fielding combination, save/day-cursor continuity.

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
- **Testing:** Early/late/bust/breakout/decline cohorts, high-potential failure, deterministic replay, no rating reaction to one stat line.

## C05 — Position/role proficiency and training (Phase 7)

- **Responsibility:** Support freely attempted positions/two-way roles and finite training allocation with real unfamiliarity penalties.
- **Inputs:** Requested role/position, existing skills/proficiency, focus/intensity, learned repertoire, training/game exposure.
- **Outputs:** Training plan, proficiency/role preference changes, effective-ability penalties and pitch-learning progress.
- **State ownership:** Proficiencies, preferred roles, training allocation and repertoire learning state; AI owns actual selection.
- **Dependencies:** Player, Ratings, State, Development, date/exposure.
- **Public interfaces:** `SetTraining(TrainingPlan) -> Result<TrainingPlan>`; `RequestRole(RoleIntent) -> Result<RoleIntent>`; `ApplyPractice(PracticeExposure) -> ProficiencyDelta`.
- **Events:** `TrainingPlanChanged`, `PlayerChangedPosition`, `PlayerRoleChanged`, `PitchLearned`.
- **Persistence:** `proficiencies`, `training_plans`, `pitch_repertoire`, dated role changes.
- **Failure cases:** Allocation >100%, invalid position, reinitializing a known skill on return, promising a starting role from a preference.
- **Testing:** SS→CF→SS retention, batter↔pitcher, shared two-way budget/time, learning by camp/first/second team, no artificial two-way ceiling.

## C06 — Club and team policy (Phases 1, 9)

- **Responsibility:** Stable franchise identity, club policies, team-role expectations and managerial performance evaluation.
- **Inputs:** Player performance/state/proficiency, team needs, coaching policy, dated affiliation.
- **Outputs:** Club context, trust/role evaluations, selection priorities and public explanation categories.
- **State ownership:** Club identity/presentation metadata, staff references, policy, trust/team-role history; no personal relationship score.
- **Dependencies:** Players, statistics, observations, roster query; rule snapshots.
- **Public interfaces:** `EvaluatePlayer(TeamEvaluationContext) -> ManagerEvaluation`; `GetClubContext(TeamId) -> ClubContext`.
- **Events:** `ManagerEvaluationChanged`, `TeamRoleChanged`.
- **Persistence:** `clubs`, `club_policies`, `manager_evaluations`; identity survives name/logo changes.
- **Failure cases:** Club-name joins, inactive franchise treated as active participant, friendship overwriting professional evaluation.
- **Testing:** Same name/different IDs, rename continuity, role transitions, separate trust/relationship combinations.

## C07 — Roster, lineup and pitcher use (Phases 2, 9–10)

- **Responsibility:** Validate and select eligible assignments, lineups/rotation/bullpen/substitutions and position coverage.
- **Inputs:** Availability, registration, competition rules, workload, club policies, abilities/proficiency, game situation.
- **Outputs:** Legal lineup/roster transition or typed shortage, with public reasons.
- **State ownership:** Dated registrations/assignments, current selection and rotation; contract affiliation changes are coordinated with C13/C19.
- **Dependencies:** Player/Club, Injury, State, game rules; later military/national reservation.
- **Public interfaces:** `SelectLineup(SelectionContext) -> Result<Lineup>`; `ProposeRosterMove(RosterContext) -> RosterProposal`; `ValidateAssignment(AssignmentChange) -> ValidationResult`.
- **Events:** `LineupSelected`, `RosterMoveProposed`, `PlayerCalledUp`, `PlayerDemoted`.
- **Persistence:** `registrations`, `assignments`, `rotation_plans`, role/assignment history with effective dates.
- **Failure cases:** Ineligible player, duplicate slot, no catcher/pitcher, exhausted bullpen, incompatible simultaneous appearances.
- **Testing:** Shortages and legal fallback, two-way/DH rules, injury filtering, no automatic promotion from second-team stats.

## C08 — Game simulation (Phases 2–3, 5)

- **Responsibility:** Authoritative baseball state transitions and seeded outcomes using one engine for every competition/playback mode.
- **Inputs:** Legal lineups, ratings/state/proficiency snapshots, park/matchup, strategy, count/situation, rules and RNG state.
- **Outputs:** `GameStep` with new state, scored play/stat evidence, workload and next decision/end status.
- **State ownership:** Active game, batting order, substitutions, runner responsibility, score/count/outs and cursor.
- **Dependencies:** Baseball reducer/scorer, ability evaluator, roster validation, RNG; no SQL/UI.
- **Public interfaces:** `StartGame(GameStart) -> Result<GameState>`; `AdvanceGame(GameState, LegalStrategy, RuleSnapshot, RngCursor) -> Result<GameStep>`.
- **Events:** `GameDecisionRequested`, `PlateAppearanceCompleted`, `RunnerPlayCompleted`, `SubstitutionMade`, `GameCompleted`.
- **Persistence:** `game_states`, `game_players`, `scored_plays`, final box scores; save at every allowed stable decision boundary.
- **Failure cases:** Stale decision, illegal strategy, impossible reducer outcome, invalid probabilities, nonterminating rule state.
- **Testing:** All §6.2 baseball oracles, 100 complete games, 100k PA calibration, seeded replay, mode invariance, resume with identical next outcome.

## C09 — Schedule and date progression (Phase 6)

- **Responsibility:** Maintain fixtures and deterministic dates/stages, stop at relevant player choices and support safe cancellation.
- **Inputs:** Competition definitions, participating squads, dates, rule calendar, pending choices and availability reservations.
- **Outputs:** Valid fixture plan or conflict report; advancement receipt with last committed stage/stop reason.
- **State ownership:** Fixture identity/status, world date and day-stage completion markers.
- **Dependencies:** Season, Game, roster and pending decisions; later activity/tournament/service reservations.
- **Public interfaces:** `BuildSchedule(ScheduleSpec) -> Result<Schedule>`; `AdvanceDate(AdvanceRequest) -> Result<AdvanceOutcome>`.
- **Events:** `FixtureScheduled`, `FixtureRescheduled`, `DateAdvanced`.
- **Persistence:** `fixtures`, `day_cursor`, scheduled tasks/reservations; stable order by date/competition/game ID.
- **Failure cases:** Conflicting fixtures, unresolved required decision, incomplete earlier stage, unsupported cancellation rule.
- **Testing:** Off-days, resumable multi-game day, postponed fixtures, no duplicated completed games, cancellation and reload parity.

## C10 — Season and awards (Phases 6, 11)

- **Responsibility:** Camp/exhibition/regular/All-Star/postseason/Korean Series/awards/offseason stages and season closure.
- **Inputs:** Rules, fixture results, standings, award eligibility/statistics and calendar.
- **Outputs:** Stage transitions, brackets, award results and finalized season facts.
- **State ownership:** Stage, bracket/series, award selection and closure marker.
- **Dependencies:** Schedule, Game, Stats, club roster; market/draft later register dated hooks.
- **Public interfaces:** `AdvanceSeasonStage(StageContext) -> Result<SeasonDelta>`; `EvaluateAwards(AwardContext) -> AwardResults`.
- **Events:** `SeasonStageChanged`, `AllStarSelected`, `TeamClinchedPostseason`, `TeamWonChampionship`, `PlayerWonAward`, `SeasonClosed`.
- **Persistence:** `seasons`, `series`, `awards`, `team_seasons`, closure receipts.
- **Failure cases:** Unfinished required fixture, tied rank without rule, repeated closure, award without eligible candidate.
- **Testing:** Every stage, tiebreaks, postseason stat separation, title/award idempotency, ten full-season seeds.

## C11 — Statistics and metric definitions (Phases 2, 11, 23)

- **Responsibility:** Convert scored facts into exact counts and versioned derived metrics by competition, season, team stint and role.
- **Inputs:** Scored plays, game attribution, historical baseline coverage, league/park/metric definitions.
- **Outputs:** Batting/pitching/fielding lines, standings inputs, rankings and advanced metrics with availability/method metadata.
- **State ownership:** Counting facts/lines and metric projections; does not choose game results or ratings.
- **Dependencies:** Game scorer, imported history and competition definitions.
- **Public interfaces:** `ApplyScoredPlay(ScoredPlay) -> StatDelta`; `GetStats(StatsQuery) -> StatPage`; `RebuildMetrics(MetricScope, Version) -> RebuildReport`.
- **Events:** `StatisticsFinalized`; record/milestone owners determine their own once-only events.
- **Persistence:** Game lines, season/team-stint counts, historical baselines, projection receipts and metric version.
- **Failure cases:** Double count, overlapping imported coverage, missing denominator, innings-as-decimal, incomplete WAR inputs.
- **Testing:** Count conservation, zero/unknown distinctions, transfer/two-way splits, formula fixtures, rebuild equivalence.

## C12 — Injury and rehabilitation (Phases 8, 10)

- **Responsibility:** Decide episodes, prognosis/recovery, play-rest legality, relapse and rare long-term effects.
- **Inputs:** Workload, durability/state, age, position, episode history, medical choice, rehab exposure and seeded roll.
- **Outputs:** Injury/recovery delta, availability, temporary modifiers and explicit rare permanent-change request.
- **State ownership:** Body/side/type/severity/dates/recovery/reinjury episode history and plan.
- **Dependencies:** State, Development, Roster eligibility, actual second-team games for rehabilitation.
- **Public interfaces:** `EvaluateRisk(InjuryContext, RngState) -> InjuryResult`; `ChooseCare(CareChoice) -> Result<RecoveryDelta>`; `ReviewRehab(RehabExposure) -> RecoveryDelta`.
- **Events:** `PlayerInjured`, `InjuryWorsened`, `RehabilitationStarted`, `PlayerClearedForRehab`, `PlayerReturnedFromInjury`.
- **Persistence:** `injuries`, `rehab_steps`, prior episode links, care choices/effect receipts.
- **Failure cases:** Illegal play-through, clearing all injuries when one heals, duplicate permanent effect, rehab marked as first-team return.
- **Testing:** All severities, recurrence, ordinary-base preservation, stacked injuries, actual second-team rehab, save during choice.

## C13 — Playing contracts and service credit (Phases 13–15)

- **Responsibility:** Validate/activate domestic playing terms and accrue service/payment obligations.
- **Inputs:** Player/club eligibility, offers, chosen terms, rules, game/service evidence and calendar.
- **Outputs:** Active contract/offer state, service totals, ledger obligations, eligibility facts.
- **State ownership:** Contract terms/negotiation state/credit ledger, not wallet balance or club assignment rows.
- **Dependencies:** Roster, Money, Agent advice; Application coordinates affiliation and payments.
- **Public interfaces:** `ProposeContract(OfferSpec) -> Result<ContractOffer>`; `AcceptOffer(OfferId, PlayerDecision) -> Result<ContractActivation>`; `AccrueService(ServiceEvidence) -> ServiceDelta`.
- **Events:** `ContractOfferReceived`, `PlayerSignedContract`, `ContractExpired`, `ServiceCreditAccrued`.
- **Persistence:** Contracts, term rows, offers, service evidence and uniquely keyed payment obligations.
- **Failure cases:** Expired offer, overlapping contract, invalid money/date, missing consent, repeated accrual or payment scheduling.
- **Testing:** Entry activation, multi-year/option terms, no-trade clause, thresholds, money rounding, transaction rollback.

## C14 — Draft (Phase 13)

- **Responsibility:** Determine eligible pool, order and AI selections without player-selected outcomes.
- **Inputs:** Cohort eligibility, standings/rules, team need/evaluation and seeded policy.
- **Outputs:** Picks, drafted/undrafted facts and entry-offer requests.
- **State ownership:** Draft edition/order/picks and selection progress; Player identity is shared.
- **Dependencies:** Feeder, Scouting, ClubAI, Registration and fixed entry Contract activation.
- **Public interfaces:** `BuildDraftPool(DraftContext) -> Result<DraftPool>`; `RunNextPick(DraftId) -> Result<DraftPick>`; `CompleteDraft(DraftId) -> DraftSummary`.
- **Events:** `PlayerEnteredDraft`, `PlayerDrafted`, `PlayerWentUndrafted`.
- **Persistence:** Draft edition, eligible entries, picks, current pick and rules/seed; unique player/pick per edition.
- **Failure cases:** Ineligible/duplicate candidate, exhausted pool, duplicate pick retry, paused own entry decision.
- **Testing:** Full draft, ties/order rules, depleted pool, reload mid-draft, actual roster activation after signing.

## C15 — Amateur feeder (Phase 12)

- **Responsibility:** Supply future fictional players and evolving background organizations without external league simulation.
- **Inputs:** Year, organization snapshot, distributions, existing university/other-path prospects and seed.
- **Outputs:** New identities/profiles/development inputs, amateur summaries and school-output facts.
- **State ownership:** Amateur organizations, cohort generation markers, enrollment history and school reputation/output.
- **Dependencies:** Player/Development factories, Datapacks, completed draft/career outcomes.
- **Public interfaces:** `GenerateCohort(CohortSpec, RngState) -> Result<CohortResult>`; `UpdateSchoolReputation(SchoolOutcomeBatch) -> SchoolDelta`.
- **Events:** `AmateurPlayerGenerated`, `ProspectCohortPublished`, `SchoolReputationChanged`.
- **Persistence:** Amateur organization snapshot, cohorts, origins/enrollments and output history inside each save.
- **Failure cases:** Duplicate yearly generation, empty eligible organization pool, university prospect recreated under new ID, potential fixed by school.
- **Testing:** 20 cohorts, all origins, real-background/fake-player separation, low-reputation elite outcomes, source-pack independence.

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
- **Inputs:** Contract/service history, rule version, club need/budget, preferences and player decision.
- **Outputs:** FA state, offers/comparison and activation/compensation plan.
- **State ownership:** FA edition/declaration/market status; C13 owns signed contracts.
- **Dependencies:** Contracts, Roster, ClubAI, Agent and advisory relationship/fame facts.
- **Public interfaces:** `CheckFAEligibility(PlayerId, Date) -> EligibilityResult`; `DeclareFA(Declaration) -> Result<FAState>`; `ResolveFAChoice(OfferId) -> Result<ActivationPlan>`.
- **Events:** `PlayerDeclaredFA`, `FAOfferReceived`, `FAOfferDeclined`, `FAAgreementReached`.
- **Persistence:** Declarations, market windows/offer links, decision deadlines, compensation obligations.
- **Failure cases:** Ineligible declaration, expired window, no offers, concurrent acceptances, illegal compensation.
- **Testing:** Exact thresholds, multiple offers, choice authority, no-offer path and multi-owner rollback.

## C19 — Trades, releases and foreign intake (Phase 16)

- **Responsibility:** AI-initiated legal personnel transfers and sustainable domestic intake/exits.
- **Inputs:** Club needs/value, contracts, eligibility/quotas, no-trade consent and available candidates.
- **Outputs:** Validated transaction affecting both clubs or rejection; intake/release history.
- **State ownership:** Transaction proposal/result, foreign candidate pool; contract/roster mutations remain with their owners.
- **Dependencies:** ClubAI, Contract, Roster, Player, CareerHistory.
- **Public interfaces:** `EvaluateTrade(TradeContext) -> TradeProposal`; `ValidateTransfer(TransferPlan) -> ValidationResult`; `ExecuteTransfer(ApprovedTransfer) -> Result<TransferReceipt>`.
- **Events:** `PlayerTraded`, `PlayerReleased`, `ForeignPlayerSigned`.
- **Persistence:** Transaction participants/terms/date, immutable origin and team-stint history, unique execution receipt.
- **Failure cases:** Missing consent, invalid destination/quota, stale evaluation, one club updated while the other fails, endless proposal loop.
- **Testing:** No-trade consent/rejection, bilateral atomic rollback, released re-entry, foreign quotas without foreign league simulation.

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

- **Responsibility:** Player-centered relationship state with relevant people, including rivals and agents.
- **Inputs:** Confirmed interactions/choices, personality, team changes and relevant character identities.
- **Outputs:** Relationship deltas and public categories/reasons; weak preference inputs for career choices.
- **State ownership:** Directed player-to-character relationship edges/history; managerial trust remains C06.
- **Dependencies:** Player/Character registry, Story, calendar and approved effects.
- **Public interfaces:** `ApplyInteraction(RelationshipEffect) -> Result<RelationshipDelta>`; `GetRelationships(PlayerId) -> RelationshipView`.
- **Events:** `RelationshipChanged`, `RivalryEstablished`.
- **Persistence:** `characters`, `relationships`, reason history; player-linked and non-player characters both supported.
- **Failure cases:** Missing/deleted participant, double effect, full NPC graph expansion, forced FA choice through relationship.
- **Testing:** Friendly rival/competitor, trust separate from friendship, transfers, public category masking, saved effects.

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

- **Responsibility:** Account for salary/bonus/incentives/commercial income, commissions, expenses, savings and ownership.
- **Inputs:** Validated payment obligations, purchase commands, catalog snapshots and scheduled expenses.
- **Outputs:** Ledger entries, wealth/earnings summaries, owned asset records and purchase effects.
- **State ownership:** Integer KRW ledger/opening balances/ownership; playing/commercial terms belong elsewhere.
- **Dependencies:** Money value type, Contracts, Life, Commercial, catalog validation.
- **Public interfaces:** `PostPayment(PaymentObligation) -> Result<LedgerReceipt>`; `Purchase(PurchaseCommand) -> Result<PurchaseReceipt>`; `GetWealth(PlayerId) -> WealthSummary`.
- **Events:** `IncomePaid`, `CommissionPaid`, `ExpensePaid`, `AssetPurchased`, `SavingsTransferred`.
- **Persistence:** Append-only ledger with unique obligation/period keys, assets/ownership, opening balance provenance.
- **Failure cases:** Overflow, negative affordability, duplicate obligation, savings counted as income, undocumented valuation policy.
- **Testing:** Money conservation, rounding, recurring costs/load, current-vs-lifetime totals and failed purchase rollback.

## C26 — Structured story and decisions (Phases 5, 21)

- **Responsibility:** Select eligible events and apply defined choices to authoritative owners before any generated prose.
- **Inputs:** Confirmed world facts, personality/life/role conditions, participants, cooldowns, choice and seeded event selection.
- **Outputs:** Pending decisions with legal options, validated effect batch and fact bundle.
- **State ownership:** Story instance, eligibility/cooldown/expiry and choice receipt, not its target domains' state.
- **Dependencies:** Calendar barrier and typed effect ports to State/Relationships/Contracts/Life.
- **Public interfaces:** `EvaluateStories(StoryContext) -> StoryCandidates`; `ResolveChoice(DecisionId, ChoiceId) -> Result<ChoiceEffectBatch>`.
- **Events:** `StoryTriggered`, `StoryChoiceResolved`, `DecisionResolved`, `InterviewIntentSelected`.
- **Persistence:** Definitions from JSON; instances, participant IDs, pending choices and cooldowns in SQLite.
- **Failure cases:** Stale choice, contradictory predicates, absent participant, expired decision, repeated effects or AI-created options.
- **Testing:** Deterministic selection, decision stop/resume, reload/idempotency, unavailable participants and coherent family/role transitions.

## C27 — Career and league history (Phases 4, 11, 23)

- **Responsibility:** Preserve player and league facts and present chronological careers/records across imported and simulated history.
- **Inputs:** Committed events, stat/award facts, validated imported coverage and subject IDs.
- **Outputs:** Career timeline, league history, milestone facts and retirement inputs.
- **State ownership:** Durable history facts and milestone occurrence; rich views are rebuildable projections.
- **Dependencies:** Stats, Contracts, Injury, Draft, Military, Life, Season and narrative links.
- **Public interfaces:** `RecordHistory(ConfirmedHistoryFact) -> HistoryReceipt`; `QueryCareer(CareerQuery) -> CareerPage`; `DetectMilestones(MilestoneContext) -> MilestoneFacts`.
- **Events:** `CareerMilestoneReached`, `LeagueRecordSet`.
- **Persistence:** History source/coverage/date/IDs, milestones and projection receipt; retained after retirement.
- **Failure cases:** Duplicate milestone, overlapping imported totals, dangling subject, interpreting missing facts as zero.
- **Testing:** Historical+save continuity, imported opening baseline, year/team/role/competition scopes, projection rebuild and retired lookup.

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

## C29 — News/events and Gemini presentation (Phases 5, 26)

- **Responsibility:** Express committed facts with importance-aware templates/optional AI, factual links and backend-defined interviews.
- **Inputs:** Whitelisted public facts, importance, outlet/style, approved D5 expression policy and content budget.
- **Outputs:** Escaped article/interview/reaction presentation or immediate fallback, never a domain mutation.
- **State ownership:** Presentation cache/requests/status; authoritative facts stay with domain owners.
- **Dependencies:** Fact builder, template renderer, optional Gemini adapter, public entity-link resolver.
- **Public interfaces:** `BuildPublicFacts(EventId, ViewerContext) -> PublicFactBundle`; `RenderNarrative(PublicFactBundle, PresentationPolicy) -> NarrativeResult`.
- **Events:** Presentation `NarrativeReady`/`NarrativeFailed`; no AI-origin gameplay events.
- **Persistence:** Optional text/model/prompt/fingerprint/cache records; API keys excluded; completed templates work without network.
- **Failure cases:** Hallucinated facts, schema errors, timeout/quota, hidden data in prompt, markup injection, late result attached to new branch.
- **Testing:** Zero ordinary API calls, online/offline domain parity, duplicate suppression, false injury/romance/contract adversaries, fallback and approved-expression checks.

## C30 — Retirement (Phases 16, 25)

- **Responsibility:** Routine NPC exit and final controlled-player career closure, farewell, summary and bounded epilogue.
- **Inputs:** Career decision/policy, ability/age/opportunity/health, retained career facts and existing legacy results.
- **Outputs:** Retirement fact, closed playable state, farewell/summary and scheduled epilogue facts.
- **State ownership:** Retirement plan/date/status, career closure and epilogue milestones.
- **Dependencies:** Player, Roster/Contract release policies, History, RetiredNumbers and Hall evaluation.
- **Public interfaces:** `PlanRetirement(RetirementIntent) -> Result<RetirementPlan>`; `FinalizeRetirement(RetirementContext) -> Result<RetirementDelta>`; `AdvanceEpilogue(EpilogueRequest) -> EpilogueStep`.
- **Events:** `RetirementDecisionRequested`, `PlayerRetired`, `FarewellHeld`, `EpilogueCompleted`.
- **Persistence:** Retirement date/cause, closure revision, pending milestones, summarized reference IDs; player row retained.
- **Failure cases:** Active gameplay after retirement, lost deferred honors, deleting history, infinite post-retirement world loop.
- **Testing:** Early/quiet/star retirement, contract cleanup, summary reconciliation, save-to-epilogue continuation and locked career actions.

## C31 — Retired numbers (Phase 25)

- **Responsibility:** Independently evaluate franchise legacy and delayed honors for each relevant club.
- **Inputs:** Team-specific tenure/role/records/fans/titles/awards/loyalty/story, jersey history and approved tuning.
- **Outputs:** Candidate/declined/announced honor facts with public qualitative reasons.
- **State ownership:** Hidden legacy case/evaluation, scheduled review and club-number honor record.
- **Dependencies:** Retirement, TeamHistory, Stats, Reputation and calendar.
- **Public interfaces:** `EvaluateFranchiseLegacy(LegacyContext) -> LegacyCase`; `ResolveNumberHonor(LegacyCaseId, Date) -> Result<HonorDecision>`.
- **Events:** `NumberRetirementConsidered`, `NumberRetired`.
- **Persistence:** Team/player/number/date, score/model version internally, pending review and ceremony facts.
- **Failure cases:** Public raw score, career totals replacing club contribution, duplicate number conflict, forced one-team restriction.
- **Testing:** Long backup vs franchise star, league legend without club honor, delayed review, rare multiple-team honor, replay-safe announcement.

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
- **Persistence:** §5 protocol, catalog reconstructible from immutable generations, rule/RNG/allocator/decision/day/game state included.
- **Failure cases:** Disk full, stale revision, interrupted backup/publication, corrupt/latest/newer save, migration failure, loaded branch callback collision.
- **Testing:** Fault injection at every boundary, exact next-outcome replay, catalog rebuild, prior-version upgrades, multi-career/slot isolation and unsupported-version preservation.

## C34 — Datapacks and asset import (Phases 4, 27)

- **Responsibility:** Validate/materialize initial world data, optional assets and explicit data-only overlays; never refresh active facts automatically.
- **Inputs:** Folder/individual files, manifest/schema/rules, source provenance and asset references.
- **Outputs:** Validation report, immutable effective pack, initial-world specification and presentation asset catalog.
- **State ownership:** Installed/staged pack metadata; after initialization all mutable content belongs to the career save.
- **Dependencies:** JSON parser/schema checks, explicit ID/reference map, allowed asset loader; no executable mod loader.
- **Public interfaces:** `ValidatePack(PackSource) -> ValidationReport`; `MaterializePack(PackSelection) -> Result<ValidatedPack>`; `CreateWorld(ValidatedPack, CareerStartSpec, Seed) -> WorldInitialization`.
- **Events:** Application `DatapackInstalled`; career initialization records provenance.
- **Persistence:** Static JSON/schema/assets and save snapshots of authoritative initial data; source/content hashes and coverage.
- **Failure cases:** Duplicate IDs, broken references, invalid dates/enums/ranges, incompatible versions, unsafe paths, corrupt/missing assets, ambiguous overlay precedence.
- **Testing:** Every validator class, error file/entity/field paths, missing optional image fallback, invalid import rollback and original-pack deletion after career start.

## C35 — Player database and common profile (Phases 6, 24)

- **Responsibility:** Search active/second-team/generated/prospect/released/retired players and expose one privacy-safe profile.
- **Inputs:** Viewer-scoped query, name/team/position/age/role/level/status/origin filters, sort/page and selected PlayerId.
- **Outputs:** Bounded public list/profile tabs and consistent link targets.
- **State ownership:** No authoritative gameplay state; navigation/query cache is UI/session state.
- **Dependencies:** Application visibility policy and read repositories for owned domains.
- **Public interfaces:** `SearchPlayers(PlayerSearchQuery, ViewerContext) -> PlayerPage`; `GetPlayerProfile(PlayerId, Tab, ViewerContext) -> ProfileView`.
- **Events:** None; query lifecycle notifications only.
- **Persistence:** Indexes and optional projection caches; filter/scroll preferences separate from core state.
- **Failure cases:** Hidden truth in DTO/sort key, missing ID, stale callback, N+1 reads, all rows materialized as controls.
- **Testing:** Combined filters, duplicate names, retired profiles, missing tabs, no private fields and restored navigation at both resolutions.

## C36 — Team database and common profile (Phases 6, 24)

- **Responsibility:** Discover franchises and display current rosters/rank plus continuous team history without management commands.
- **Inputs:** TeamId, season/history/record/roster filter and page state.
- **Outputs:** Overview/history/season/roster/records/awards/retired-number read models, links and drill-down lists.
- **State ownership:** No authoritative state; caches carry source revision.
- **Dependencies:** Team/History/Stats/Roster/Legacy read models and PlayerLink/TeamLink routes.
- **Public interfaces:** `ListTeams(TeamQuery) -> TeamPage`; `GetTeamProfile(TeamId, TeamTabQuery) -> TeamProfileView`.
- **Events:** None; projection availability notifications only.
- **Persistence:** Indexed queries/projections and separate route state.
- **Failure cases:** Title counts disagree with years, renamed team loses history, first/second/developmental/military filters conflated.
- **Testing:** Current/historical separation, 6+2 titles drill-down, roster filters, team→player→back context restore and FHD information parity.

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

## C38 — Advertising and sponsors (Phase 22)

- **Responsibility:** Create player-approved commercial opportunities with explicit image/equipment/time obligations and benefits.
- **Inputs:** Performance/fame/popularity/image/national career, brand criteria, agent and player decision.
- **Outputs:** Offers, accepted agreements, scheduled appearances, payments and equipment conditions.
- **State ownership:** Commercial terms/status/obligations; actual wallet, calendar and equipment ownership use their owners.
- **Dependencies:** Agent, Assets, Life/calendar, Reputation and narrative fact publisher.
- **Public interfaces:** `GenerateCommercialOffers(CommercialContext) -> OfferBatch`; `AcceptCommercialOffer(OfferId) -> Result<CommercialPlan>`; `CompleteAppearance(ObligationId) -> Result<EffectBatch>`.
- **Events:** `CommercialOfferReceived`, `AdvertisementSigned`, `SponsorSigned`, `CommercialAppearanceCompleted`, `CommercialAgreementExpired`.
- **Persistence:** Commercial agreements, requirement snapshots, scheduled obligations and receipts/history.
- **Failure cases:** Offer creates income before acceptance, conflicting equipment brands/time, duplicated fees, excessive ability bonus.
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
