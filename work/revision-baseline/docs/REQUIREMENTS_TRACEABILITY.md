# Star Player Requirements Traceability

Authority: [G: Game Design v1.6](../sources/Star_Player_game_v1.6.txt), [U: UI/UX Guide v1.8](../sources/UI_UX_GUIDE_v1.8.md), and [M: master task](../sources/MASTER_REQUEST.txt). Sections below refer to original part/section headings; the generated [source heading index](SOURCE_SECTION_INDEX.md) supplies original line locations. This matrix maps requirements to **planned** implementation and tests. It is not evidence that the game is implemented.

Each row is closed only by passing the referenced phase criteria and Common Gate G. “Later” below means a specific later phase, not permission to omit the requirement. Source examples are illustrative unless identified as fixed. Owner decisions D1–D6 preserve unresolved policy rather than silently choosing it.

## Product coverage

| ID | Source | Requirement / important interpretation | Contract(s) | Implementation phase(s) and decisive evidence |
|---|---|---|---|---|
| R01 | G1, G12; M4–6 | One-player PC single-player career; autonomous wider world; no manager/GM mode | C01/C06–C10 | 5 controlled actor; 9 legal AI; 17 world turnover; unauthorized other-player command rejected |
| R02 | G2; U36–37 | New player and existing-player start; existing facts preserved except editable personality | C01 | 5 both flows; 13 full entry; original ratings/contracts/injuries/history unchanged at selection |
| R03 | G2; U36 | Name/DOB/age/origin/body/position/handedness/features/form/repertoire; automatic player archetype from skill mix | C01/C02/C05 | 5 creator; 7 repertoire/proficiency; D4 bounds free-text mechanics; archetype is a derived label |
| R04 | G2/G4; M13–14 | Advisory rookie budget, no normal hard cap; cheat no over-budget warning; shared two-way normal resources | C01/C02 | 5 over-budget normal and cheat fixtures accepted within 0–100 scale |
| R05 | G2/G4/G8; U7/13/26 | Self exact current ability; NPC grades/ranges/confidence; everyone hides true potential/growth type | C02/C16/C35 | 1 public DTO tests; 12/24/26 query/sort/tooltip/narrative leak audits |
| R06 | G3; U8–9 | Choice-driven hitter and pitcher strategies, real game context and immediate results | C08 | 2 scoring; 3 resolver; 5 player loop; D1 resolves input cadence |
| R07 | G3; M16 | Ability/matchup/pitch/velocity/state/defense/running/park/situation/strategy/randomness influence | C02/C08 | 3 normalized probability traces and targeted influence/calibration tests |
| R08 | G3; U8.4 | Own batting, own pitching, important situations, entire game, instant modes; bench fast-forward | C08 | 5 first three core modes (own batting/own pitching/instant); 11 remaining modes; identical-action mode parity |
| R09 | G4 | Six hitter skills, pitcher skills/repertoire, 0–100 scale and separate transient state | C02/C03 | 1 types/bounds; 3 effective ability; 5 state; no direct short-performance rating overwrite |
| R10 | G4 | Initial real-rating estimation: league relative, sample corrected, scouting/manual overrides; reality decoupled after start | C02/C34 | 27 methodology/evidence/cutoff tests; D3 prevents future-data leakage |
| R11 | G4 | Hidden potential, uncertain evaluator-specific reports, ranks not exact potential order | C04/C16 | 7 growth; 12 observations/cohort; erroneous ranges and observer disagreement fixtures |
| R12 | G4 | Training focus/intensity anytime, pitch learning, gradual growth, aging, early/late/bust/breakout | C04/C05 | 7 all configured growth classes and finite allocation; UI risk/coach guidance |
| R13 | G4 | Flexible position/pitcher/hitter/two-way conversion; proficiency penalties and transfer; no arbitrary ceiling | C05/C07 | 7 reversible known-position proficiency; 9 AI selection; 10 lower-league practice |
| R14 | G4 | Two-way workload counts pitches, PA, defense, rest, travel, training and injury history | C03/C12 | 7 combined workload; 8 risk; 10/18/19 cross-competition exposure |
| R15 | G4; U20 | Body/type/severity/recovery/relapse; play-through vs rest; ordinary effects temporary, serious effects rare | C12 | 8 severity/state tests; 10 actual rehab games; history preserved |
| R16 | G4; U6/12/17 | Team-role progression and manager trust independent from friendship | C06/C23 | 9 professional evaluation; 21 friendly/low-trust and competitor/friend fixtures |
| R17 | G5; U38 | Complete camp/exhibition/regular/All-Star/postseason/Korean Series/awards/offseason cycle | C09/C10 | 6 calendar; 11 ten full-season seeds and saved stage boundaries |
| R18 | G5; U50 | Annual future fictional cohort from high schools/universities/other/overseas backgrounds | C15 | 12 20 cohorts, unique IDs/origins; foreign background has no simulated foreign season |
| R19 | G5; U50 | School development/exposure/reputation affects distributions/observation, never fixes potential; slow output-based reputation | C15/C16 | 12 low-reputation elite/famous-school ordinary cases, bounded reputation changes |
| R20 | G5; U50 | Permanent origin chains and school output/alumni, saved separately from mutable source pack | C15/C27/C35 | 12 saved seed organizations; 24 school panels/alumni filtering; no pack overwrite |
| R21 | G5; U50 | AI draft order/picks, uncertain preview, actual roster entry; player cannot choose result | C14/C13 | 13 unique picks and actual signed roster activation; own entry decision barrier |
| R22 | G5; U51; M24–25 | Origin, draft result, entry path, registration and league level separate; no developmental DB | C39/C07 | 1 model; 13 developmental→registered→first debut; source origin retained |
| R23 | G5; U51 | Undrafted high-school university/developmental/other/end branches; university similar without automatic enrollment | C39/C15 | 13 all branch fixtures, same ID through university re-entry, no forced low potential |
| R24 | G5; U21 | Real second-team schedules/games/records/state/injury/development/AI lineups/pitcher use | C17 | 10 two actual leagues shared engine; level-specific stats and shared workload |
| R25 | G5; U21/20 | Call-ups/demotions/rehab depend on ability/form/health/need/trust/policy, not guaranteed stats | C07/C12/C17 | 9 blocked-position fixture; 10 real rehab/registration/move transactions |
| R26 | G5; U14 | Salary/length/signing bonus/options and own final contract choice | C13/C25 | 13 fixed entry; 14 negotiation/payments; amount/period/incentive reconciliation |
| R27 | G5; U14 | FA eligibility, multi-club offers and meaningful advisory money/role/team/loyalty preferences | C18 | 15 thresholds/no-offer/multiple-offer/own choice; no foreign market |
| R28 | G5/G7 | Agent types/negotiation/marketing/information/trust/reputation/fees/advice, no automatic own-player signing | C37 | 14 domestic; 22 marketing; no-agent early start and fee replay |
| R29 | G1/G5 | AI trades/releases/foreign-player contracts; no-trade where applicable | C19 | 16 bilateral atomicity/quota/re-entry; foreign candidate pool without foreign league |
| R30 | G6; U22 | AI national selection by competition/eligibility/role/health/performance/fame, not guaranteed | C20 | 18 positional competition and rule-edition fixtures |
| R31 | G6; U22 | Asian Games/Olympics/WBC/Premier12 profiles, shared game engine, separate stats and medals | C21 | 18 one fixture edition each; domestic leave/return, separated records |
| R32 | G6 | National career affects fame/popularity/commercial opportunity and permanent history | C21/C27/C38 | 18 confirmed effects; 22 criteria; 23 career totals |
| R33 | G6; U23 | Military statuses, ordinary service, competitive sports team, real second-team play and original-team return | C22 | 19 all paths/eligibility/contract clocks; no guaranteed selection or ability collapse |
| R34 | G6 | Asian Games gold/Olympic top-three plus individual eligibility; sports-personnel distinct from exemption | C22 | 19 positive and negative medal/status fixtures; versioned rule data |
| R35 | G7; U15–16 | Personal income, cars/housing/equipment/coaching/health/travel/hobbies/family/savings | C24/C25 | 14 earned income; 20 every category and ledger/time conservation |
| R36 | G7/G12 | Overseas personal training retained; spending supports recovery/training, not large car/relationship skill bonuses | C05/C24/C25 | 20 camp/activity fixtures; prohibited direct large boosts absent |
| R37 | G7; U17 | Relevant player-centered relationships: staff/teammates/competitors/rivals/veterans/juniors/family/partner/agent | C23 | 21 each class, categories/reasons not raw numbers; no full NPC network |
| R38 | G7; U16 | Dating/marriage/family/birth/spending/rest/FA-location context as light events; choice remains player-owned | C24/C26 | 21 complete family stages and overridden advisory preference |
| R39 | G7; U19 | Conditional random stories using current facts/personality/wealth/role/health, predefined meaningful choices | C26 | 21 eligibility/cooldown/participant/idempotency/offline fixtures |
| R40 | G7/G8 | Reputation/popularity/media attention/team influence/image distinct enough to drive stories/commerce | C26/C38 | 21 reasoned fact deltas; 22 offer criteria, no AI-created fame fact |
| R41 | G7; U14 | Ads/sponsors categories, terms/image/time/fatigue/training trade-offs, equipment obligations | C38/C37 | 22 ad+equipment lifecycle with income/fee/time/state accounting |
| R42 | G8; U11 | Counting stats and future advanced metrics: WAR/wRC+/OPS+/FIP/ERA+/BABIP | C11 | 2 scoring inputs; 23 documented formulas, method/coverage/zero-denominator tests |
| R43 | G8; U24 | Career years/teams/contracts/awards/injury/income/role/national/military/history | C27 | Facts from 4 onward; 23 complete timeline/reconciliation; 25 final summary |
| R44 | G8; U13/52 | League/team continuous history, title/runner-up/first-place/postseason counts and exact year detail | C28/C36 | 11 facts; 23 aggregate 6+2 and provenance; 24 drill-down |
| R45 | G8; U52 | Franchise leaderboards/major players/awards/retired numbers/current roster | C11/C28/C36 | 23 team-stint counts; 24 complete tabs; 25 honor results |
| R46 | G8; U13 | Player DB active/second-team/generated/prospect/retired and common profiles by ID | C35 | 6 minimal; 24 all filters/tabs and hidden-value checks |
| R47 | G8; U13/50–52 | Global player/team links, school panel links, persistent return filters/sort/scroll/tab/season | C35/C36 | 5 live-game inspection; 6 minimal routes; 24 full link audit and 10-step restore |
| R48 | G8; U18/19 | Engine facts → importance → template/Gemini; major bundles/outlets/interviews with fixed effects | C26/C29 | 5 templates; 21 interview effects; 26 optional API and D5 integrity gate |
| R49 | G8; M10/75–76 | Minimal fact payloads, no hidden truth/unconfirmed events, caching/budgets/failure fallback | C29 | 26 adversarial output, offline identical world, no ordinary API calls |
| R50 | G9; U25/52 | Team-specific hidden legacy, multi-factor delayed number retirement and rare multi-team honors | C31 | 25 all honor scenarios with hidden scores and saved delays |
| R51 | G9; U25 | Retirement ends main career, farewell/summary/bounded future epilogue; no post-retirement play | C30 | 16 NPC exit facts; 25 controlled ending/locked gameplay and complete summary |
| R52 | G9 | Hall separate from retired number; league-wide factors, waiting period, NPC inclusion, hidden score | C32 | 25 dominant/borderline/NPC/independent-honor fixtures |
| R53 | G10; U35 | Fictional default works without real assets; local individual/pack import and placeholders | C34 | 4 minimal; 10 fictional10; 27 complete offline pack and all validation classes |
| R54 | G10; M43–48 | JSON/image mods, explicit IDs/assets/rosters/contracts/draft/rules; no executable code/foreign world | C34 | 27 schemas/overlay rules/path bounds; exclusions enforced in every phase |
| R55 | G10; U34 | Save becomes authority; independent worlds/slots, metadata, manual/autosave/latest-valid Continue | C33 | 4 fault injection/midgame resume/catalog rebuild; source-pack edit/delete immunity |
| R56 | G11; M7–9 | Fixed Godot stable/C#/Control/SQLite/JSON/Gemini stack, UI/core separation | Shared | 0 dependency/export probe; every phase boundary test |
| R57 | G11; U46 | Exactly QHD/FHD, QHD baseline, same FHD access without uniform text shrink | UI architecture | 5 both layouts; 11/24 tables; 28/29 full screenshot/action inventory |
| R58 | G12; U38–40/47 | Incremental Prototype1/2/3 UI, Alpha final design, Polish after stable systems | All | 3 / 5 / 11 / 28 / 29 milestones; UI delivered with each feature |
| R59 | U3–7/27–33 | Shell/status/menus/home priorities, state labels, tables/tooltips/popups, not all events blocking | UI architecture | 5 shell; 6 tables; 8 injury priority; 24 states; 28 final consistency |
| R60 | U8.5–8.7; M49 | Compact defense position+name, active offense/runners/evaluation, no required game portraits | C08/public UI | 3 initial field; 5 both viewports; §1.5 explicit NPC privacy resolution |
| R61 | U34/46; M45/58–59 | Stable save points, multiple manual slots/careers, transactions/migrations/recovery | C33 | 4 foundation; every persisted phase upgrade; 29 final matrix |
| R62 | M55–57 | Per-system contracts, structured integration events and reproducible random systems | All 39 contracts | Contract entries + 0 boundaries + 3 RNG + 4 event/command receipts |
| R63 | M60/83 | Long-save size/query/performance and project risks | C11/C27/C28/C33 | 17 20-season and 29 50-season tests; §10 risk matrix |
| R64 | M61–72/81–87 | Phased scope/non-goals/tasks/UI/storage/events/tests/gates/handoffs, phase reports/current state | All | 30 complete phase templates; G; §9 and supplied AGENTS/CURRENT_STATE |

## Explicitly deferred or excluded

| Item | Source treatment | Roadmap treatment |
|---|---|---|
| Expanded individual skill fields (separate platoon/bunt/steal/judgment/pickoff/crisis ratings) | G4 describes “future” expansion | Preserve room for versioned skill additions; initial gameplay uses existing base skills/proficiency and rule mechanics. Not invented as mandatory extra attributes |
| Trade requests | G5 says consider later; M26 says where applicable | Not required initial gameplay. No-trade clauses and actual AI trades are included in Phase 16 |
| Full in-game data editor | G10 says later, file-first; U43 later extension | Phase 27 provides importer/validator/tool, not a mandatory full editor |
| Full foreign leagues/careers/posting/overseas FA | Explicit exclusions | Not implementation phases; background teams, foreign KBO intake, international squads and personal overseas camps remain covered |
| Steam/Steamworks/cloud/achievements | Explicit exclusions | Personal desktop export only |
| Manager/GM/coaching/post-retirement management, multiplayer, executable mods | Explicit exclusions | Not implementation phases |
| Extra resolutions and permanent early final visual design | U38–46 excludes/defers | QHD/FHD only; final palette/fonts/icons at Alpha |
| Seed portraits and Gemini image/batch | New workspace file, not original sources; owner explicitly excluded on 2026-10-05 | Excluded; existing photo import and fallback remain |

## Source tensions and approval boundaries

Resolved using explicit rules: NPC field ratings use estimates (not literal numeric mockup examples); player remains unable to manage substitutions; developmental registration is separate from level; minimal databases arrive during Prototype 3; old version labels in U49 do not replace the supplied versions.

D1: PA orchestration vs count/decision cadence. D2: detailed competition/career rule editions. D3: real 2026 pack cutoff/data. D4: new-player start and free-text effects. D5: strict AI prose integrity. D6: historical-detail retention. Each is fully described in ROADMAP's final section with issue, reason, recommendation, alternative and impact.

## Verification focus for the documentation package

The package validator checks all 30 phase headings and required subsections, objective gate/handoff presence, internal Markdown file links, 39 ten-field contracts, source hashes, direct dependency order and the existence of all numbered UI source sections in the source index. Author review additionally checks product ownership, source coverage and contradictions. These are document checks only; no game unit/UI/simulation/export tests have been run.
