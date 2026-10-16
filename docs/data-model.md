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

A player record holds `player_id, display_name, birth_date, throws, bats, height_cm, weight_kg, personality[], biography, ratings{hitter,pitcher,catcher}, state, proficiency{preferred_position, positions[], roles[]}, status{origins[], entry_path, registration, league, affiliations[], career}`. Hidden data is only in `development_profiles[]` (and, in a v2 pack, `batter_zone_tendencies[]`). Lists are in canonical order (enum order / sorted by id) so `write(read(pack)) == pack`. All content is fictional (labelled in `label` and in each biography); no real KBO player or club.

## Fixture pack schema v2 (Phase 3-D, BatterTendency v2; test-fixture format only, no save or SQLite structure)

`format_version: 2` is v1 plus one OPTIONAL top-level list, `batter_zone_tendencies[]`, of **hidden** rows `{player_id, zone_model, reference_id, values[9]}` (model `pzl-zones-v1`; a registered reference id; nine inner-cell integers in row-major order, each `-100..100`, reference-weighted sum exactly 0). Semantics: v1 -> every player neutral and the field is rejected; v2 omitted or `[]` -> every player neutral; v2 rows -> those players only (sparse; a player without a row is neutral; entry order is kept). Invalid data fails the whole read (explicit null, wrong JSON type, unknown member, duplicate or unknown `player_id`, a player without hitting ratings, an unregistered model or reference, wrong length, out-of-range value, non-zero weighted sum); nothing is repaired. A world remembers the format and shape it was read from and writes back v1 as v1, v2 omitted as omitted and v2 `[]` as `[]`. The rows are world content like `development_profiles[]`: not part of `PlayerSheet`, never returned by `PlayerInfoService`, never printed (`BatterZoneProfiles(hidden)`). The official fictional v1 pack stays v1 and unchanged; non-neutral tests build v2 packs from it in memory. Phase 4 decides the save mapping; it must be able to reproduce the profile data itself and the world format (a hash alone cannot resume a game).

## Logical mapping names (intent for Phase 4, not DDL)

`players`, `player_origins`, `ratings`, `player_states`, `proficiencies`, `development_profiles`, `jersey_number_assignments` (unique active `(player_id, club_id)`; interval constraint per `(club_id, jersey_number)`), `stadiums`, `park_factor_profiles`, `stadium_usage`, `rule_snapshots`, `battery_chemistry` (unique `(catcher_id, pitcher_id)`). Migrations must not backfill invented history (e.g. a missing battery pair stays neutral).

## Game execution contract (Phase 3-D v2; game facts, no save or SQLite structure)

`GameExecution` (Domain, optional trailing member of `GameStart`, `GameState` and `FinalizedGame`; `null` = legacy game) holds `probability_model_version` (`prob-model-v2`), `tuning_version` (`tuning-virtual-proto-v2`), `tuning_hash` (64 lowercase hex), `advancement_policy_version` (`advancement-policy-v1` | `advancement-policy-v2`), `park` (the resolved C46 `ParkFactorSnapshot`: `stadium_id`, `profile_id`, `version`, `home_runs`, `hits`, `extra_base_hits` in permille, each 800-1250 for the active model) and `player_inputs_id` (64 lowercase hex of the immutable player input set, or null). It is a game FACT: fixed at the start, never rewritten, retained by the finalized game. **Logical mapping for Phase 4 (not DDL):** a `games` row must be able to reproduce all six members, `stadium_id` + `park_factor_profile_id` + profile version + the three permille values (the snapshot is small enough to store itself), and the player input set CONTENT (`PlayerRunFieldInput`: player id + base fielding / speed / arm, null for a player without hitter ratings; its id alone cannot resume a game); a missing execution on an old game stays "not recorded" and is never backfilled. The tuning file `content/tuning/virtual_tuning_v2.json` (format 1, same schema as v1; model `prob-model-v2`, tuning `tuning-virtual-proto-v2`) is shipped data, not a save.

## PlayerInputSet content versions and the Framing catcher input (Phase 3-E A+B; game facts, no save or SQLite structure)

`PlayerInputSet` (Simulation, resolver-internal) is the game's immutable player input snapshot; `GameExecution.PlayerInputsId` pins its identity. Since Phase 3-E it has a **content version** that is part of the type: `player-input-set-v1` (`PlayerInputSet.Create`: per player `fielding`, `speed`, `arm`; `null` = no hitter ratings; identity text `player-input-set-v1\n<player_id>|fielding=<n|->|speed=<n|->|arm=<n|->` in ordinal PlayerId order, SHA-256 lowercase hex; **unchanged since Phase 3-D**) and `player-input-set-v2` (`PlayerInputSet.CreateForFraming`: the same line plus `|framing=<0-100|->`, `-` = player without catcher ratings; identity text starts with `player-input-set-v2`). The same sheets are a different identity in v1 and v2; a v1 set never holds a framing value. `FrozenCatcherInput` (game id, input-set id, catcher PlayerId, `ReceivingFraming`) is frozen into the `PreparedPitch` only for a `prob-model-v3` + `non_automated` pitch and is not persisted. `GameExecution.ProbabilityModelVersion` may now be `prob-model-v2` (with `tuning-virtual-proto-v2`) or `prob-model-v3` (with `tuning-virtual-proto-v3`); a v3 non_automated game must pin a content-v2 set.

**Content version 3 (Game Calling v4, 2026-10-17; game facts, no save or SQLite structure).** `player-input-set-v3` (`PlayerInputSet.CreateForGameCalling`) extends the line again: `<player_id>|fielding=<n|->|speed=<n|->|arm=<n|->|framing=<0-100|->|game_calling=<0-100|->|control=<0-100|->`, `game_calling` = the catcher's base `GameCalling` (`-` = no catcher ratings), `control` = the pitcher's base `Control` (`-` = no pitching ratings); identity text starts with `player-input-set-v3`, entries in ordinal PlayerId order, invariant digits, SHA-256 lowercase hex. v1 and v2 identities and bytes are unchanged (pinned literals computed independently); `Create` / `CreateForFraming` never capture the new fields (always `null` there); `PlayerRunFieldInput` gains the trailing defaulted `GameCalling` and `PitcherControl` and still holds only a PlayerId and nullable integers. `prob-model-v4` (with `tuning-virtual-proto-v4`) requires content v3 in every called-pitch mode: the CURRENT catcher's `GameCalling` (and, for non_automated, `ReceivingFraming`) and the CURRENT pitcher's `Control` are read from the set the game pinned; `FrozenCatcherInput` (now with nullable `ReceivingFraming` / `GameCalling`) and the new `FrozenPitcherInput` (game id, set id, pitcher PlayerId, Control) are frozen into the `PreparedPitch` and not persisted. `GameExecution.ProbabilityModelVersion` may now also be `prob-model-v4` (with `tuning-virtual-proto-v4`, `player_inputs_id` required). A missing role rating is never replaced by 50.

**Phase 4 NOTE.** Persisting a game requires the content VERSION, the content (every entry, including `ReceivingFraming`) and the identity together, and must reproduce all three on restore; a 64-hex id alone cannot resume a game (content v3 adds `GameCalling` and `PitcherControl` to every entry). No such structure was added.
