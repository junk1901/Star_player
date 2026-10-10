# Data model (Phase 1 data dictionary)

Scope: the in-memory Domain vocabulary and the **fictional JSON fixture schema v1** that exist after Phase 1. There is no SQLite schema, migration or save format yet (Phase 4). Field names use the documented stable `snake_case` style so the same names can become SQLite columns later; the logical mapping names at the end are intentions, not DDL.

Authority: ROADMAP §3 and `docs/SYSTEM_CONTRACTS.md` C01–C06, C40, C41, C46, C47. Where Phase 1 added a vocabulary choice the sources did not fix, it is marked **(Phase 1 choice)**.

## Identifiers (`Domain/Common/Ids.cs`)

Opaque canonical strings, validated by one rule set: non-blank, no leading/trailing whitespace, 1–64 characters, alphabet `A-Z a-z 0-9 _ - . :` (so `/` is free for composite keys). `default(XId)` is "no id" and never equals a created id. Names are never join keys.

| Type | Meaning |
|---|---|
| `PlayerId` | one person across amateur/pro/retired life |
| `ClubId` | franchise identity (C06), survives renames; used by C47 |
| `TeamId` | competition-context squad key kept from Phase 0 (not used by Phase 1 data) |
| `AmateurTeamId` | origin organization (school/university/overseas club); separate from `ClubId` |
| `StadiumId`, `ParkFactorProfileId` | C46 |
| `RuleProfileId`, `RuleVersionId` | C40 |
| `BatteryPairId` | composite `(catcher PlayerId, pitcher PlayerId)`; key text `catcher_id/pitcher_id` |
| `CareerId` | Phase 0 |

## Value objects

| Type | Rule |
|---|---|
| `SkillValue` | integer 0–100 inclusive; the skill scale, not a creation budget (cheat still 0–100) |
| `Money` | signed 64-bit KRW, checked add/subtract/multiply; `Scale(num, den, rounding)` exact in 128-bit, `TowardZero` or `HalfAwayFromZero` |
| `InningsOuts` | integer outs; `112` ⇒ `37.1`; parse accepts only `.0/.1/.2`; adds as outs |
| `JerseyNumber` | integer 0–999 absolute; legal range per club/era comes from rules data (`JerseyNumberRange`) |
| `PopTimeObservation` | measured catcher pop time in **milliseconds** (500–6000); a different type from the 0–100 Pop Time skill |
| `ParkFactor` | permille, 1000 = neutral; only produced through `ParkFactorBounds` |

## Player (C01–C05)

Separate owners, none embedded in another:

| Type | Holds | Notes |
|---|---|---|
| `Player` | id, display name, birth date, throws, bats, height/weight, personality traits, biography | age = completed years from birth date vs world date (Feb 29 ⇒ Mar 1 in common years). No club, contract, number or ratings. Personality traits (성실함 … 자유분방함, from the source list) and biography are identity data with **no effect** in Phase 1 (D4). **(Phase 1 choice: trait list)** |
| `Ratings` | `HitterRatings` (contact, power, eye, speed, fielding, arm), `PitcherRatings` (stuff, control, velocity, endurance + `PitchRepertoire`), `CatcherRatings` (receiving_framing, blocking, pop_time, game_calling) | a set is null when the role is absent; `Kind` (hitter/pitcher/two-way) is derived. Catcher tools require hitter ratings. Fielding/arm/endurance are single fields; "shared" means one value consumed by several evaluators later, never duplicated |
| `PlayerState` | condition, fatigue, sharpness, stress, satisfaction, recent_form (0–100 each) | transient; never base skill |
| `ProficiencyProfile` | preferred position, per-fielding-position proficiency (C, 1B … RF), per-role proficiency (batter, starting pitcher, relief pitcher) | independent of ratings. Changing position keeps every recorded proficiency; an unrecorded position reads as 0 = "no recorded familiarity", not an invented constant |
| `PlayerStatus` | ordered origins, entry path, registration status, league assignment, dated club affiliations, career status | axes are separate; invariants in `PlayerStatus.Create` (e.g. 육성선수 + 2군 is legal, an amateur with a league is not). **(Phase 1 choice: enum members taken from source §102.12)** |
| `DevelopmentProfile` | **hidden**: true potential, growth type, prime years, decline onset, per-skill ceilings | not a member of `PlayerSheet`; separate list in the pack; must satisfy `ceiling ≥ current` |
| `PlayerSheet` | player + ratings + state + proficiency + status | the only thing the visibility layer receives |

Enums (JSON spelling = snake_case of the member): `PitchType` fastball, slider, curve, changeup, forkball, sinker · `FieldPosition` pitcher, catcher, first_base … right_field, designated_hitter · `OriginType` high_school, university, overseas, other_prospect · `EntryPath` draft, development_contract, other · `RegistrationStatus` amateur, draft_candidate, development, registered, released · `LeagueAssignment` first_team, second_team · `CareerStatus` active, retired.

### Event payload schemas (no event loop yet)

```json
PlayerCreated        { "player_id", "birth_date", "kind", "preferred_position", "created_on" }
PlayerChangedPosition{ "player_id", "from", "to", "effective_on" }
```

Payloads carry ids and public facts only; never development data.

## Club, jersey (C06, C47)

`Club(club_id, name, short_name)`.

`JerseyNumberAssignment(player_id, club_id, jersey_number, effective_from, effective_to, assignment_reason)` — half-open interval `[from, to)`: the start day is included, the end day is not, `to = null` ⇒ active. An interval ending on a day and another starting that same day do not overlap, so a change or transfer can take effect on one date. `AssignmentReason`: imported_initial, rookie, player_request, club_decision, transfer, free_agency, release, retirement (the last two only close). `RetiredNumberRestriction(club_id, number, effective_from)` blocks new assignments on/after that date; earlier assignments remain valid history. `JerseyNumberLedger` (immutable; its public collections are read-only copies) enforces: legal range, retired restriction, **a player holds at most one number at a time across all clubs** (`player_already_has_number` for the same club, `player_at_other_club` for another club), and no overlapping holders of one number at one club (`number_in_use`). `Change` closes the old interval and opens the new one in one step; a failed request returns an error and leaves the ledger unchanged; closed rows are permanent history and survive later retirements. `ActiveOn(player, date)` returns a `Result`: success with no row, success with the single active row, or `conflicting_active_rows` if corrupt data holds several (it never picks "the first"). The pack loader validates every row and the query service refuses to start on a conflict. Real atomic persistence and the full transfer flow are later phases.

## Rules (C40)

`RuleArea`: game, regular_season, roster, registration, postseason, draft, contract, free_agency, foreign_player, award. `RuleAreaDefinition` (immutable, built only by `Create`) holds area, version, effective window, provenance and its `RuleSimplification` list (each `{id, description}`, ids unique per definition, description required). `RuleCatalog` registers definitions; versions of one area form a timeline (windows may not overlap, a same-day handover is legal). `RuleProfile` composes one resolved version per area for an effective date. Inputs are defensively copied and every public collection is a read-only copy, so a created catalog/profile snapshot cannot change. `default(RuleVersionId)` is invalid everywhere (`RuleVersionId.invalid`). Unknown area/version ⇒ `RuleCatalog.version_not_found`/`area_unknown`; an area missing from a profile ⇒ `RuleProfile.area_missing`; overlapping versions ⇒ `RuleCatalog.overlapping_versions`. **No rule content or 2026 constants exist yet**; each consuming phase adds its detailed D2 subset.

## Stadium / ParkFactor (C46)

`Stadium(stadium_id, presentation{display_name, city, capacity})` — rename keeps the id. `ParkFactorProfile(profile_id, stadium_id, version, effective_from, home_run_permille, hit_permille, extra_base_hit_permille)`; every factor must lie inside `ParkFactorBounds(min_permille, max_permille)`. The bounds are data (fixture: 850–1150 ‰, **a fixture value, not an approved tuning cap**; the real cap is decided by the owning later phase). `ParkFactorSnapshot` is the immutable resolved copy a game will keep. Nothing in venue types references ratings. `StadiumUsage(stadium_id, club_id, from, to)` is a dated reference only.

## Battery (C41)

`BatteryPairId(catcher, pitcher)` — role-ordered, different players. `BatteryChemistry(pair, shared_appearances, last_battery_date)` default is neutral (0 appearances, no date). The familiarity scale, growth and decay are Phase 7.

## Public projections (Application, never Domain types)

`PlayerPublicView` and its `AbilityLine`/`PitchLine`/`PlayerListItem`/`WorldSummaryView` DTOs. Own player: `exact` filled. Anyone else: `grade`, `range_low`–`range_high`, `confidence` (observation model `observation-v0`: bucket width 10, or 5 for club-mates; range always contains the value and is never one number; no RNG). **(Phase 1 choice, placeholder until Phase 12 scouting.)** There is no member for potential, growth type, prime, decline or ceilings, and `HiddenDataGuard` scans payloads for those identifiers.

## Trusted boundary (who can see the raw world)

`PlayerWorld` (the whole loaded pack, including hidden `DevelopmentProfile`s and the "my player" binding), `IPlayerWorldSource`, `ViewerContext`, the `PlayerInfoService` constructor, and in Infrastructure `FixturePlayerWorldSource`, `WorldJson` and the DTOs are **internal**. `InternalsVisibleTo` lists are pinned by a test: Application → Infrastructure + two test assemblies, Infrastructure → two test assemblies, Domain → its test assembly; the Godot client is on none of them. The client's only entry points are `PlayerInfoComposition.CreateFictionalPhase1(PlayerSessionOptions)` (Infrastructure) and `SqliteStorageDiagnostics`; the first returns the safe `PlayerInfoService`. `PlayerSessionOptions` has a single member, `CheatMode`; which player is "mine" comes from the pack's own `controlled_player_id` (the Phase 1 fixture binding, replaced by the saved career binding in Phase 4), so no option, query or UI input can name or replace it. Bootstrap and presenters share one assembly, so this is enforced by visibility and by compiled-IL / source scans rather than by namespace alone.

## Fixture pack schema v1 (`content/fictional/phase1_two_club_pack.json`, embedded in `StarPlayer.Infrastructure`)

Top level (all required, unknown fields rejected): `format_version` (=1), `label`, `as_of_date`, `controlled_player_id`, `rules{catalog[{area, version, effective_from, effective_to, provenance, simplifications[{id, description}]}], profile}`, `park_factor_bounds`, `jersey{min,max,retired[]}`, `clubs[]`, `stadiums[]`, `park_factor_profiles[]`, `stadium_usage[]`, `players[]`, `development_profiles[]`, `jersey_assignments[]`, `battery_pairs[]`.

A player record holds `player_id, display_name, birth_date, throws, bats, height_cm, weight_kg, personality[], biography, ratings{hitter,pitcher,catcher}, state, proficiency{preferred_position, positions[], roles[]}, status{origins[], entry_path, registration, league, affiliations[], career}`. Hidden data is only in `development_profiles[]`. Lists are in canonical order (enum order / sorted by id) so `write(read(pack)) == pack`. All content is fictional (labelled in `label` and in each biography); no real KBO player or club.

## Logical mapping names (intent for Phase 4, not DDL)

`players`, `player_origins`, `ratings`, `player_states`, `proficiencies`, `development_profiles`, `jersey_number_assignments` (unique active `(player_id, club_id)`; interval constraint per `(club_id, jersey_number)`), `stadiums`, `park_factor_profiles`, `stadium_usage`, `rule_snapshots`, `battery_chemistry` (unique `(catcher_id, pitcher_id)`). Migrations must not backfill invented history (e.g. a missing battery pair stays neutral).
