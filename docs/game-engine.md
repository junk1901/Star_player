# Game Engine Contract (Phase 2)

Status: implemented for Phase 2 and waiting for review (amended after the Codex reviews of 2026-10-10 and 2026-10-11, sections 9 and 10, and by the R02 / OBR original-text round of 2026-10-11, sections 11 and 12, and by the C33 final recording-responsibility fix, section 13). Scope: the deterministic baseball state reducer and scorer (C08), the
official-record projection and the scripted-game diagnostics. **No probability, no outcome selection and no season, club or career
logic is part of this engine.** Phase 3 will supply outcomes; this phase only proves that a given sequence of facts always yields
a legal game state and exact official records.

Owning source: ROADMAP §6.2 (mandatory oracles) and Phase 2; AGENTS.md invariants (D1, D2, D4, D6); `docs/SYSTEM_CONTRACTS.md` C07/C08/C11.

## 1. Layers and files

| Layer | Content | Path |
|---|---|---|
| Domain | immutable game model, events, records, `GameRuleSnapshot` | `src/StarPlayer.Domain/Baseball/Game/` (`GameRules.cs`, `GameEvents.cs`, `GameModel.cs`, `GameRecords.cs`) |
| Simulation | pure `GameReducer` (incl. `GameReducer.Responsibility.cs`, the slot ledger), `EarnedRuns`, `BoxScoreBuilder` | `src/StarPlayer.Simulation/Baseball/` |
| Application | public view DTOs, Korean text, box-score projection/reconciler, diagnostics service, expected-result comparison | `src/StarPlayer.Application/Projections/`, `Ports/IScriptedGameSource.cs` (internal) |
| Infrastructure | JSON readers for rule profiles and scripted games, embedded resources, the only public composition entry | `src/StarPlayer.Infrastructure/Fixtures/` |
| Content | rule profiles; diagnostic games | `content/rules/game_rules_profiles.json`, `content/fictional/phase2_diagnostic_games.json` |
| Test fixtures | 100 scripted games and 30 C33 slot-ledger games with independently produced expected results, and their generator | `tests/Fixtures/` |
| Client | diagnostic scene (QHD), base-diamond control | `game/UI/GameDiagnostics/`, `game/Components/BaseDiamond.cs` |

Dependency direction is unchanged (ADR-001/002): Domain ← Simulation ← Application ← Infrastructure; the Godot client sees only
public Application DTOs and `GameDiagnosticsComposition.CreateFictionalPhase2()`. The reducer uses no Godot, SQL, HTTP, clock,
`System.Random` or dictionary-order dependence (`SimulationDeterminismTests` still scan the Simulation project).

## 2. Reducer API

```
GameReducer.Start(GameStart)                 -> Result<GameState>
GameReducer.Apply(GameState, GameEvent)      -> Result<GameStep>
GameReducer.LegalActions(GameState)          -> LegalActionSet
GameReducer.Finalize(GameState)              -> Result<FinalizeResult>
GameReducer.CompletedFact(FinalizedGame)     -> GameCompleted
BoxScoreBuilder.Build(GameState)             -> BoxScore
```

All functions are pure. `GameState` is an immutable record; `Apply` returns a new state, or an `Error` with a stable code and the
caller's state is untouched (there is no partial mutation to roll back). `GameStep` carries the new state, a `GameStatDelta` (every
counting fact with its source event ID), new `ScoredPlay` evidence and typed `GameFact`s (`PlateAppearanceCompleted`,
`RunnerPlayCompleted`, `SubstitutionMade`; `GameCompleted` comes from `Finalize`).

### 2.1 Inputs

`GameStart(GameId, Rules, Away, Home)` with `TeamSetup(Lineup[9 × LineupSlot], StartingPitcher, Bench[])`. `Rules` is a
`GameRuleSnapshot` (section 5). Start validation (all explicit errors): lineup orders 1–9 once each, no empty/duplicate player, defensive
positions match the rule (eight fielders plus DH when the profile has a DH, plus Pitcher otherwise), the starter may be in a DH lineup
only in the DH slot and only when the profile has the two-way rule, bench players are unique and not in the lineup, no player on both
teams.

Events (each has a stable string `Id`; bases are numbers 0 = batter source / out destination, 1–3, 4 = home):

| Event | Meaning |
|---|---|
| `ApproachEvent(Id, Normal/Patient/Aggressive)` | D1 default input: one strategy for the plate appearance. Phase 2 records the choice and gives it no effect. |
| `PitchEvent(Id, PitchSpec)` | One real pitch inside the approach-driven PA: ball, called/swinging strike, foul, foul bunt, hit by pitch, optionally wild pitch / passed ball with runner moves, or a dropped third strike. |
| `TakeEvent(Id, PitchSpec)` | D1 explicit one-pitch action: consumes exactly one actual pitch (ball, called strike or HBP only). If the PA continues the stage becomes `Decision`: exact count and runners are kept and the player may choose an approach or Take again. |
| `InPlayEvent(Id, BattedBallType, InPlayResult, Sacrifice, Moves, BatterCredits, Errors, BatterSequence)` | Batted-ball result that ends the PA; counts as one pitch. `BatterSequence` orders the batter's own out or home run among the play's other outs and runs. |
| `RunnerPlayEvent(Id, Steal/Pickoff/Balk/Other/Appeal, Moves, Errors)` | Non-PA play: steal, caught stealing, pickoff, balk, other runner event. `Appeal` exists only to be **refused** (`Game.appeal_not_supported`, section 4a). |
| `SubstitutionEvent(Id, PinchHit/PinchRun/DefensiveSub/PositionSwap/PitchingChange/DhToField/NameDesignatedHitter, Team, Player, Base, Slot, Slot2)` | Roster move. Illegal while a pitch sequence is running. `DhToField` is legal only in profiles with `DhMayBeForfeited` (section 4); `NameDesignatedHitter` is a request that is always rejected (a DH cannot be created during play). |

`RunnerMove(From, To, Cause, OutKind, Credits, Sequence, OutAt, Method)` describes one runner (0 = the batter):

- `OutKind` (`Force` / `Tag`; `Other` is refused for runners, section 4a) is **required for every runner out** and is **checked, not trusted**: the reducer derives from the state whether
  the runner really was forced at that moment (section 4, "Outs and runs on the play that ends the inning") and rejects a declared kind that disagrees
  (`Game.out_kind_missing`, `Game.out_kind_mismatch`, `Game.force_out_invalid`).
- `Method` (`Tag` / `BaseTouch`) is the physical way the out was made and never decides force status; `BaseTouch` is rejected together with `OutKind.Tag`.
- `OutAt` is the base where the out was made (default `From + 1`; `From` for a pickoff back at his base; 4 = home). It must be `From..4`.
- The batter (`From = 0`) may appear in `Moves` of a **hit** (`Single`/`Double`/`Triple`) with `To = 0`: he was safe on the hit and was then put out trying for more.
  The evidence that he reached is **the out base, not a flag**: `OutAt` must be a base beyond the one the hit gave him (single: 2-4, double: 3-4, triple: 4),
  `OutKind` must be `Tag` (a batter who stands on a base is never forced), credits must carry exactly one putout, and the order follows the same `Sequence`
  rules (or `BatterSequence`). Anything else is refused with `Game.batter_reach_not_established` / `Game.batter_move_invalid` / `Game.out_kind_mismatch`.
  The play stays ONE plate appearance with the hit, the at-bat, and one out; a hit with two outs on the play is not a grounded-into double play (the run keeps its RBI).
- `Sequence` (1, 2, 3...) is the order in which the play's outs and runs happened. It is **required for all outs and runs of a play that has a third out and
  a run** (`Game.play_order_required`) and otherwise optional, but always all-or-none, positive and distinct (`Game.play_order_incomplete`,
  `Game.play_order_invalid`). A batter caught out on a fly/line/pop ball or struck out is always first (`Game.batter_out_order_invalid`).
  When no sequence is given the default order is: a caught/struck-out batter first, then runners lead-runner first, then a ground-ball batter last.

The reducer **never chooses** a result; it checks it. A scripted source (Phase 2) or the Phase 3 resolver produces the events.

### 2.2 Outputs and state

`GameState` (authoritative): inning, half, outs, `PitchCountState(Balls, Strikes)`, `RunnerState` (first/second/third
`BaseRunner(Player, ResponsiblePitcher, RunnerKey, LineupOrder)`), runs and line scores, plate-appearance `Stage`
(`Start` / `Decision` / `InSequence`), chosen approach, per-team `TeamGameState` (lineup with defensive positions, current pitcher,
`PitcherUsage` stints, bench, removed, appeared, ever-in-lineup, next batter index), append-only `Plays`, `Stats`,
`Substitutions`, `AppliedEvents` (event receipts, section 3a), `Status`/`Outcome` and `Final`. Each `TeamGameState` carries `DhActive`, the team's own designated-hitter state (section 4). The state alone is enough to resume a game, including in the
middle of a PA after a `Take` (count, runners, stage and walk-charge override are inside it; the resolved pitch is never replayed).

`LegalActionSet(Stage, Actions, DefenseComplete, IsOver)` lists what the game accepts next; anything else is rejected with an error.

## 3. Invariants (each has automated tests)

1. An impossible play returns an error with a stable code and leaves the original state and records unchanged.
2. Outs per half-inning never exceed the rule value (3); no negative outs; at most one runner per base; runners never move backward,
   share a base or pass one another; every out carries exactly one fielder putout credit.
3. Every change of score has a `ScoredPlay` whose `Runs` list matches the change; `AwayRunsAfter`/`HomeRunsAfter` are checkable.
4. Re-applying an already processed event with the same id **and the same content** is an idempotent no-op (`WasDuplicate`, no new
   evidence); the same id with other content is a conflict (`Game.event_id_conflict`, state unchanged); a new event after the game is over is rejected.
5. Pitcher innings are integer outs; fielding outs, pitching outs, catcher outs and battery outs agree.
6. Batting, pitching and fielding records cross-check (section 7.4): runs, hits, errors, PA/AB, outs, LOB.
7. `Finalize` builds the box score once. A second call returns the same `FinalizedGame` with `WasNew == false` and recounts nothing;
   catcher and battery exposure is derived once from the stat entries, so replay or repeated finalization cannot double it.
8. Same state and event → same result (no randomness, clock or hidden input).
9. WP and PB are separate play kinds, causes and counting keys everywhere (`PitchWP` vs `FldPB`), including their earned-run consequences.
10. Counts are authoritative simulated state: balls/strikes come from real pitches; foul with two strikes keeps the count; the PA ends
    on the fourth ball (rule value), third strike, HBP or a batted ball.
11. Under D1 a `Take` consumes exactly one real pitch. If the PA continues, count and runners are exactly as after that pitch and the
    next legal decision point is offered. No every-pitch input is required.
12. A run that did not count (third out on the batter-runner, a force out, or crossing the plate after the third out) appears nowhere in the
    official records: no run, RBI, earned run, runs allowed or box-score total; the `RunnerMoveRecord` keeps `RunVoided = true` as evidence.
13. A team's `DhActive` only ever changes from true to false, only through a transition the rule profile allows, and never affects the other team.
14. A hit by pitch is a dead ball: it never carries a wild pitch, passed ball, dropped third strike or any runner move except the plain forced advance.

## 3a. Event identity and the resume contract

Every processed event leaves an `EventReceipt(EventId, Kind, FingerprintVersion, Fingerprint)` in `GameState.AppliedEvents` (in processing order).
`Kind` is `approach | pitch | take | in_play | runner_play | substitution`. `Fingerprint` is the lower-case hex SHA-256 of a canonical text built by
`EventFingerprint` (version `ef1`):

- format `ef1|kind|name=value|...`, fields in a fixed order per kind; the event `Id` is **not** part of the text (it is the key);
- enums use their member NAME (renaming a member requires a new version), integers are invariant-culture decimals, booleans `1`/`0`, absent = `~`;
- strings are escaped; lists of moves, credits and errors are **sets** (each element canonicalised, sorted ordinally), so list order never matters;
  null and empty lists are the same, except `StrikeoutCredits` (null = "default catcher putout");
- nothing depends on `GetHashCode`, object identity, process, OS, culture, thread or the order of members in JSON.

`GameReducer.Apply` compares an incoming id with its receipt: same kind and fingerprint = duplicate; different = `Game.event_id_conflict`; a receipt
of another fingerprint version = `Game.event_fingerprint_version_mismatch`. A rejected event leaves no receipt, so a corrected event may reuse its id.
A format change must bump `EventFingerprint.Version`; a literal-text/hash test (`EventIdentityTests`) fails on any silent change.

**`ef2` (Phase 3-C1, ADR-007).** An event that carries a pitch location (`PitchEvent`/`TakeEvent` through `PitchSpec.Location`, `InPlayEvent.Location`) is identified by `ef2`: `ef2|kind|<the ef1 field list>|zone_model=..|zone=..|side=..|boundary=..` (same escaping and enum rules). All other events, including legacy pitches without a location, keep `ef1` exactly; a receipt is never converted. Because `Apply` compares the stored receipt version first, an `ef1` receipt id re-delivered with a location added and an `ef2` receipt id re-delivered with its location removed are both `Game.event_fingerprint_version_mismatch`; an `ef2` event with another zone, side or other content is `Game.event_id_conflict`; an unchanged `ef2` event is a duplicate. Section 20 has the JSON side.

**Resumable game fixture (no SQLite before Phase 4):** a game is its start data (rules profile id + hash, both `TeamSetup`s) plus the ordered
events as JSON (`ScriptedGameJson.WriteEvents/ReadEvents`, same shape as a scripted game's `events`) plus the receipts as JSON
(`EventReceiptJson`: `[{event_id, kind, fingerprint_version, fingerprint}]`). Replaying the events rebuilds the identical state including each
team's `DhActive` and every receipt; re-sending any event after the round trip is a duplicate. This contract is tested (`ReplayContractTests`).

## 4. Scoring conventions implemented

Plate appearance classification: `PA` for every completed appearance; `AB` for everything except walk, HBP, sacrifice fly and
sacrifice bunt. Hits count single/double/triple/home run. Strikeout, dropped third strike (batter reaches only with first base open or
two outs; WP/PB cause recorded on the pitch, batter result `StrikeoutReachedBase`, strikeout still charged), reach on error (`ROE`, no
hit), fielder's choice (needs a runner on base), sacrifice bunt (scorer-flagged, only for a bunt that retires only the batter and
advances a runner), sacrifice fly (derived: fly ball, exactly one out on the play, fewer than two outs before, at least one run that
scored without error help). **Double plays are two different facts (OBR 9.11 vs 9.02(a)(17), fixed 2026-10-11):** the *defensive* double play (`ScoredPlay.IsDoublePlay`, one `FldDP` for each fielder with a
putout or assist) is two or more outs on one plate-appearance play that is not a hit; the batter's *grounded-into* double play (`GIDP`) exists only when the batted ball is a ground ball and the first two outs, in the verified
order, are a **force double play** (both force plays) or a **reverse-force double play** (the first out is a force play and the second is made on a runner for whom the first out removed the force). The batter-runner put out on a
ground ball is itself a force play (the definition's own examples). A non-force double play (a runner thrown out at the plate who was not forced), a fly/line double play, a hit with two outs on the play and a strikeout followed
by a caught stealing are never a GIDP. Not modelled: an error between the putouts (the input has no order for errors, evidence row C40).

Runs and RBI: every scoring runner records who is charged and the cause. The pitcher charged is decided by the **responsibility slot ledger** of section 4b (OBR 9.16(g): each pitcher is charged with the number of runners he put on base, not with the individual runners; fixed 2026-10-11, C33). RBI goes to the batter for runs scored on a plate-appearance
result except reach-on-error, strikeouts, a grounded-into force / reverse-force double play (OBR 9.04(b)(1)) and error/WP/PB/steal/balk causes. A walk or HBP that forces a run in gets an
RBI. If a reliever replaces the pitcher when the batter is ahead in the count (2-0, 2-1, 3-0, 3-1 or 3-2, OBR 9.16(h)(1)) and the batter then walks, the walk and the walked runner are charged to the pitcher who started
the plate appearance (`WalkChargeOverride`); at any other count, and for any other result of that batter, the reliever is charged (9.16(h)(2)(3)). *Inherited runners and the slot ledger: section 4b.*

### Outs and runs on the play that ends the inning

(General baseball principle used for the engine; the KBO 2026 rule text could not be read, see section 5 for the exact status. The 2026-01-29
KBO board notice, bdSe=11814, confirms two things the model relies on: a force is released as soon as the runner touches the base before the
throw, and for a lead runner's run the time order against the other runner's out is decisive.)

1. Terminal events of a play are the outs and the runs, in `Sequence` order (see 2.1). A play with a third out and a run must state the order.
2. **Force status is derived**, never declared. A runner is forced at the moment of his out iff the batter-runner has not yet been put out (a ground
   ball or a reaching batter; never after a caught ball or a strikeout), and every base behind him was occupied at the start of the play by a runner
   not yet put out. A forced runner put out at the base he was forced to (`OutAt == From + 1`) is a **force out whether he is tagged or the base is
   touched**; put out beyond that base, or at his own base, or after an earlier out removed the force, it is a **non-force out**. A batter-runner out
   is classified separately.
3. The classification of the **third out** decides the runs of that play: batter-runner out **before reaching first base** or force out = **no run counts,
   whatever the order**; any other out (including the batter-runner put out **after** a safe hit while trying for more) = only runs that crossed the plate
   **before** it (smaller `Sequence`) count; a run after the third out never counts.
4. A run that does not count is "voided": the runner is not on base afterwards, it is not an RBI, run, run allowed or earned run, and it is not left on base.
   (A stolen-base credit for a runner whose run is voided is kept; simplification.)
5. The earned-run reconstruction reuses the same rule (`ThirdOutRule`) with each move's stored order and classification: real outs keep their
   classification, an error-saved runner gets the classification he would have had, a batter saved by an error is a batter-runner out.

### 4a. Appeal plays are excluded (owner decision 2026-10-11)

Star Player does not implement appeal plays: a missed base, a failed retouch / tag-up, an appeal third out with its special run rule, a fourth-out
appeal. A game state assumes every runner touched the bases legally. The engine neither creates an appeal situation nor turns one into a plain tag out:

- `RunnerPlayKind.Appeal` and runner outs of kind `Other` are refused (`Game.appeal_not_supported`), the state is unchanged;
- on a caught fly/line/pop ball a runner put out back at his own base (the retouch case) is refused; a runner who tags up and is thrown out at the next base is an ordinary tag out;
- retiring a runner who is not forced by touching a base (an out `Method` of `BaseTouch` without a force) is refused.

This is a **game simplification, not a statement about KBO or MLB rules**: both leagues have appeal plays. The rule items are in the evidence file as
`R15-appeal-plays-excluded` (`GAME_SIMPLIFICATION_APPROVED`).

Hit by pitch: the ball is dead. The reducer rejects a hit by pitch combined with a wild pitch, passed ball, dropped third strike (`Game.hbp_with_mishap`)
or with any runner move (`Game.hbp_extra_advance_invalid`); the forced advance (and a forced run with its RBI) is filled in by the reducer. Ball four
may still carry a wild pitch (a live ball). A strikeout credit list is accepted only on a real strikeout (`Game.strikeout_credits_invalid`).

Walk-off: the home team batting in or after the regulation inning that takes the lead (with runs that **count**) ends the game on that play (no later
runner may score unless the play is a home run, which scores everyone); extra runs on a non-home-run walk-off are rejected. If the home team leads
after the top half of the final regulation (or extra) inning the bottom half is not played (**home skips last half**).

Left on base (team only; no individual LOB is recorded) follows the OBR 9.03(c) box proof - at-bats + walks + hit batters + sacrifices = runs + left on base + the opponent's putouts - so every batter ends exactly one way:
scored (the run counts), put out, or left on base. Left on base are (a) the runners standing on base when a half-inning ends with the third out, and (b) each runner who crossed the plate on a play whose third out voided his run
(`RunVoided`: neither out nor scored; counted once per runner key; fixed 2026-10-11, before that the proof was short by exactly the voided runs in 70 of 200 team-games). A walk-off ends the inning unfinished; the profile rule
`CountWalkOffRunnersAsLeftOnBase` (true in every shipped profile) counts the runners standing on base when the game ends (the winning-run scorer is gone; a batter standing on first after a walk-off single, or three runners
after a bases-loaded walk, are left on base); the proof needs this, and the OBR text has no explicit definition for the unfinished inning. `BoxScoreReconciler` checks the proof for every finished game.

Extra innings and ties: after the regulation innings a tie continues; with an `ExtraInningsLimit` the game is a tie when the limit
inning ends level; without a limit it continues until decided. The profile decides (section 5).

Earned runs have two views (OBR 9.16, fixed 2026-10-11): the **team figure** (`StatKey.TeamER`) reconstructs the whole half-inning from its start; a **pitcher's** earned run (`PitchER`) is judged in the inning as reconstructed from
the moment *he* entered (the real outs and the real runners at his entry are the starting position), so a relief pitcher gets no benefit of the previous pitchers' chances for outs that were not accepted (9.16(i)). The official examples
(1)-(3) are hand-oracle tests: with two out, a walk and an error, then a relief home run, the reliever has one earned run while the team has none. The first pitcher of a half-inning is judged from its start, which equals the team
reconstruction. Which pitcher a run is charged to (R) is decided by the slot ledger (section 4b); whether it is earned (ER) is decided here, from the charged pitcher's own view; the two are computed separately.

Earned-run reconstruction: after each half-inning, the half is replayed as if every error and passed ball had not happened (rules
in `EarnedRuns.cs` header): a batter who reached on an error is out; a runner who advanced on an error/passed ball stays (or moves one
base when forced by a batter who still reaches); a runner saved from an out by an error is out; other runners advance by the real
number of bases without passing or sharing a base; the replay stops at the third reconstructed out; a run is earned only if its runner scores in the replay;
the runs of the play that makes the third reconstructed out follow the third-out rule above (batter-runner or force = none, otherwise those before it). Earned runs are charged to the
`ResponsiblePitcher` of each run.

Pitcher decisions (simplified, rule-profile values): the winning pitcher is the pitcher of record when the winning team took the lead
it never gave up; a starter needs `StarterWinMinOuts` outs, otherwise the reliever with most outs (earliest first); the loss goes to
the pitcher responsible for the run that created that lead; a save needs a finisher other than the winner who entered with the lead
held to the end and (lead ≤ `SaveMaxLead` and ≥ `SaveMinOuts` outs) or (lead ≤ runners on base + 2) or ≥ `SaveLongOuts` outs.

Steals: only one-base advances or caught-stealing outs; the catcher records `CatSBA` for a stolen base and `CatCS` when the catcher
holds the putout credit; the runner records `RunSB` / `RunCS`; a pickoff records `RunPicked`; a balk moves every runner exactly one base and is the
pitcher's (`PitchBK`).

Catcher and battery: every real pitch adds `CatPitches` and `BatteryPitches(pitcher partner)` for the catcher in the lineup **at that moment** (so a
mid-inning catcher change splits the workload correctly); every out adds `CatOuts`/`BatteryOuts`; every batter faced adds
`BatteryBF`. `CatcherExposure` (pitches, outs, stolen bases allowed, caught stealing, passed balls) and `BatteryExposure`
(pitches, outs, batters faced, `SharedAppearances = 1`) are built once in `BoxScoreBuilder` from these entries. They are workload facts
only: the exposure never changes a rating, never counts as friendship, and says nothing about the catcher's C proficiency.

Substitutions: pinch hitter (batting team only, bench only, replaces the batter now due), pinch runner (replaces the runner on a base
and in the lineup slot), defensive substitution (by batting-order slot; the runner or current batter must use the pinch moves),
position swap (not pitcher/DH), pitching change (fielding team only; bench or, for a DH rule with the two-way rule, the DH player
may take the mound; an already used or removed player cannot return). A two-way player batting and pitching has separate stat keys and one
`GameAppearance` (`InBattingOrder`, `Pitched`).

**DH state.** `GameRules.DesignatedHitter` says the DH exists at the start; each team's `DhActive` (starts equal to it) says whether it still has one.
With `DhActive` the pitcher is not in the batting order; without it the pitcher bats in the slot with position `Pitcher`, a pinch hitter for the pitcher
leaves the defense incomplete (only substitutions legal, `Game.defense_incomplete`) and a bench pitcher replaces the pitcher slot. The **two-way exception (OBR 5.11(b))** belongs only to the starting pitcher who is also the starting DH (`TeamGameState.StartingDh == StartingPitcher`, fixed by the starting lineup): while he pitches he cannot be replaced as
the DH, after a relief pitcher comes in he stays the DH and can never pitch again (`Game.pitcher_already_used`), and if he then takes a fielding position the DH role ends. Any *other* DH-slot player (an ordinary DH, or a
replacement who became the DH) who goes to the mound has taken a defensive position: the DH role ends under OBR 5.11(a)(12) (`DhMayBeForfeited` profiles; he becomes the pitcher in his own batting slot), and a profile that
does not let the DH end refuses the move (`Game.pitcher_not_eligible`). Until 2026-10-11 the engine let any DH-slot player pitch and keep the role (evidence row C32).

**The starting DH must bat once (OBR 5.11(a)(2), fixed 2026-10-11):** while a team still has its DH, its starting DH cannot be replaced by a pinch hitter or a defensive substitution before his first plate appearance (completed, or
already in progress with a pitch taken) unless the opposing club has changed pitchers; a refused move is `Game.dh_must_bat` and the state is untouched. Later replacements are not bound by this.

The **forfeiture transitions** are profile switches, and both follow the original 2026 OBR text (read 2026-10-11, section 11; **not** a verified KBO rule):
`DhMayBeForfeited` enables the first one, OBR 5.11(a)(12) (the DH assumes a defensive position: he keeps batting in his own spot, the pitcher bats in the place of the replaced fielder, the team has no DH for the rest of the game).
`FielderToMoundForfeitsDh` enables the second one, OBR 5.11(a)(14) ("a player on defense goes to the mound (i.e., replaces the pitcher)" terminates the DH role). **Correction of 2026-10-11:** earlier
revisions called the second transition "invented (neither OBR nor KBO)" and kept it fictional-only; that classification was wrong, the effect is an OBR clause (rule R27). What the OBR text does **not** say is who fills the vacated defensive
position, so the engine does not choose: the event itself must name the arrangement it supports (below), and every other arrangement is refused (evidence rows C29, C39). Both switches are on in the KBO-oriented profile (as `OBR_FALLBACK_APPROVED`, not as certified KBO rules).

| Transition | Event | Effect |
|---|---|---|
| DH takes the field (OBR 5.11(a)(12)) | `DhToField(Slot = fielder's order)` | the DH player takes that fielder's position in his own slot, the fielder leaves the game, the current pitcher bats in the fielder's slot (position `Pitcher`), `DhActive = false` |
| fielder goes to the mound (OBR 5.11(a)(14)) | `PitchingChange(Player = a lineup fielder, Slot = the DH's batting-order number)` | that fielder becomes the pitcher in his slot, the DH player takes the fielder's old position **because the event says so**, the old pitcher leaves the game, `DhActive = false`; without `Slot` the move is refused (`Game.dh_vacated_position_unspecified`), with another number too (`Game.dh_vacated_position_invalid`) |

Rejected (state unchanged): either transition in a profile without forfeiture (`Game.dh_forfeit_not_allowed` / `Game.pitcher_not_eligible`), when the team's DH
is not active (`Game.dh_not_active`), targeting the DH or pitcher slot (`Game.dh_forfeit_target_invalid`), moving a batter now due or a runner
(`Game.use_pinch_substitution`), moving a two-way player who is pitching from the DH slot (`Game.two_way_dh_field_while_pitching`,
`Game.two_way_dh_forfeit_conflict`). Once `DhActive` is false nothing can re-create a DH: `NameDesignatedHitter` is rejected for both states
(`Game.dh_cannot_be_reactivated`, `Game.dh_already_active`), and `PositionSwap` still refuses pitcher/DH slots. After forfeiture the ordinary no-DH rules apply (a
lineup fielder may take the mound by swapping positions with the pitcher). The other team's state is never touched.

### 4b. Run responsibility: the slot ledger (C33, OBR 9.16(g), fixed 2026-10-11)

OBR 9.16(g) and its Comment: a relief pitcher is not charged with runs of runners who were on base when he entered, nor of runners who reach on a fielder's choice that puts out a runner left by a preceding pitcher; "it is the intent ... to charge each pitcher with
the number of runners he put on base, rather than with the individual runners". Before the fix the engine stored one pitcher per runner and charged the run to the runner's own pitcher, which cannot express examples 2, 3, 5 and 6.

**Model.** Each runner on base occupies one *slot* of the pitcher who put a runner there (`BaseRunner.ResponsiblePitcher` is the slot, not the runner's origin). The runner is where the slot currently sits; the quantity is what the rule counts. Code: `RunResponsibility` and
`GameReducer.HandOverFieldersChoiceSlot` in `GameReducer.Responsibility.cs`, wired in `FinishPlay`. No new event, no new persisted field; the event payloads, fingerprints and receipts are unchanged.

| Situation | Effect on slots |
|---|---|
| Batter reaches on a hit, walk (charge per 9.16(h), `WalkChargeOverride`), hit by pitch, error, dropped third strike | a new slot of the pitcher charged with him |
| Batter reaches on a fielder's choice **and a runner is put out on the play** (`FieldersChoice`) | the batter takes the slot of the runner put out (9.16(g) text; examples 2, 3, 5, 6). If several runners are put out and they belong to **different pitchers**, the play is **refused** (`Game.ambiguous_responsibility`, below); several runners of the **same** pitcher pass that pitcher's slot on |
| Fielder's choice in which nobody is put out | a new slot of the pitcher on the mound |
| Runner caught stealing, picked off, out on his own advance (also at home on a hit), out on a ground out | his slot disappears (examples 4, 7: "put out without action by the batter" and extra-base outs) |
| Runs score | each run takes the **oldest** slot (pitcher order of entry into the game) among the scoring runners and the runners still on base, in the order of `ScoredPlay.Runs`; a runner who stays on base keeps his own slot when it is still free, the others receive the leftovers, lead runner first (the Comment: "charged with all runs subsequently scored up to and including the number of runners such pitcher left") |
| Pinch runner, pitching change | the slot stays with the base; nothing is recomputed |

**R and ER are separate.** `ScoredRun.ResponsiblePitcher` is the pitcher charged with that run and gives exactly one `PitchR` (so the pitchers' R of a team add up to the opponent's credited runs; the same runner key is never charged twice). `PitchER` additionally requires the scoring runner's key to be
in the earned set of the *charged pitcher's* 9.16(i) view (`EarnedRuns.Reconstruct(half, limit, startIndex)` unchanged), and `TeamER` is unchanged. The loser (9.17(d)) is `ScoredRun.ResponsiblePitcher` of the run that gave the winner a lead it never gave up (`BoxScoreBuilder.Decisions`, unchanged), so a run charged through a slot now makes
that pitcher the loser. `RunnerMoveRecord.ResponsiblePitcher` is the slot the runner carried when the play started (evidence only). Nothing changes on a rejected event: the ledger is computed inside `FinishPlay` after validation, from the immutable state.

**Official examples (hand oracles, `ResponsibilityLedgerTests`, expectations from the rule text).** 1 Abel's run to Peter; 2 Baker (FC) scores for Peter; 3 Abel out at home on a fielder's choice, Baker's run to Peter; 4 Abel picked off, Baker's run to Roger; 5 one run each to Peter, Roger, Sierra; 6 one run to Peter, one to Roger; 7 Baker's run to Roger. Before the
fix examples 2, 3, 5 and 6 failed (reliever charged, e.g. 0/3 instead of 1/1/1); examples 1 and 4 passed already and are kept as regression pins.

**Unsupported or recorded choices.** (a) Example 7 says Baker advances to second on the throw: a batter's own extra-base advance on a hit is not an event of this model (batter moves are limited to reach-on-error and out-after-hit), so the test uses a stolen base to the same effect; the responsibility structure (Peter's runner out on his own advance, Roger's runner scores) is
what is verified. No Gate effect. (b) A batter-runner called out for interference before reaching first (Comment) needs the unsupported "other" out kind and is refused as before. (c) A fielder's choice that puts out runners of two or more different pitchers is **not supported and refused** (section 14). (d) The earned verdict follows the scoring runner's identity in the charged pitcher's reconstruction.
Engine behaviour for a given rules profile changed (the `ContentHash` of a profile only covers rule parameters); no game is persisted before Phase 4, so no compatibility break exists yet; a persisted game must carry an engine rule version when SQLite mapping starts.

## 5. GameRules (D2 scope, provenance and certification status)

`GameRuleSnapshot` is immutable and built only through validated `Create`. Fields: profile ID, version, status
(`KboOrientedUnverified` | `FictionalTest`), provenance text, `OpenItems`, outs per half (3), balls for walk (4), strikes for
strikeout (3), regulation innings (9), extra-innings limit (null = none), tie allowed, designated hitter, two-way player rule,
starter-win minimum outs, save lead/outs values, `DhMayBeForfeited`, `CountWalkOffRunnersAsLeftOnBase`. `ContentHash` is a SHA-256 over the canonical
field values; a finished game stores the profile ID and the hash it used. Creation rejects contradictory data with stable codes (e.g.
`GameRules.tie_inconsistent`, `GameRules.two_way_requires_dh`, `GameRules.dh_forfeit_requires_dh`). The reducer reads legality **only** from the
snapshot passed to `Start`; changing a file afterwards cannot affect a started or finished game.

### Rule-source policy (approved by the owner, 2026-10-11): KBO first, 2026 OBR as fallback

1. KBO league-specific rules (regular-season extra-innings limit, tie handling, KBO's own DH provisions, ABS, pitch clock...) come first.
2. KBO official rule book and record rules, where the article can be read.
3. 2026 MLB Official Baseball Rules (OBR) as the approved fallback for general play and scoring rules **only when no KBO provision was found**. This is a Star Player
   policy, **not** a claim that the rule equals 2026 KBO; "no KBO rule found" is never recorded as "KBO has no such rule". Where KBO and OBR conflict, KBO wins.
4. Star Player simplifications approved by the owner or explicitly recorded scorer simplifications (appeals excluded, no real-time pitch-clock logic, ...).
MLB-only operating rules are not adopted (no automatic runner in extra innings, no three-batter minimum, no MLB roster rules). Regular-season and
postseason rules are not mixed. The resolved rules are frozen in the immutable `GameRuleSnapshot` (`RulePolicyId = kbo-first-obr2026-fallback-v1` for the KBO-oriented
profile; `fictional-test-v1` for test profiles); a later change cannot alter a started or finished game.

Rule status vocabulary (`content/rules/game_rules_evidence.json`): `KBO_VERIFIED` (official KBO evidence read), `OBR_FALLBACK_APPROVED` (no KBO provision found,
approved OBR fallback; **not** a KBO certification), `GAME_SIMPLIFICATION_APPROVED` (an owner-approved simplification or approved operating standard; the separate field
`clause_certified` says whether a rule-book clause was read), `UNRESOLVED` (no evidence/policy/scope decided). Each entry records rule id, area,
profile scope, value, source URLs, what was actually read, status, reason, whether it is required for the Gate and the hand-test ids. A test
(`RuleEvidenceTests`) guards the bookkeeping: no fallback is dressed as KBO verification, every listed test exists, the Gate blockers equal the required rules still `UNRESOLVED`.
**Test success is never counted as official evidence.**

Profiles (`content/rules/game_rules_profiles.json`):

| Profile | Status | DH | Two-way | DH may end (DH plays defense, 5.11(a)(12)) | Fielder to mound ends DH (5.11(a)(14)) | Extra innings |
|---|---|---|---|---|---|---|
| `kbo-oriented-unverified-v0` | KBO-oriented (see evidence file) | yes (OBR fallback) | yes (OBR fallback) | **yes** (OBR fallback) | **yes** (OBR fallback, since 2026-10-11) | limit 11 (KBO-verified), tie at the limit applied on owner approval (R02, clause not certified) |
| `test-dh-cap11` | fictional test | yes | yes | no | no | limit 11 |
| `test-dh-forfeit` | fictional test | yes | yes | yes | yes (same switches as the KBO-oriented profile, for the tests) | limit 11 |
| `test-no-dh-cap11` | fictional test | no | no | - | - | limit 11 |
| `test-dh-unlimited` | fictional test | yes | yes | no | no | unlimited, no tie |
| `test-dh-cap10` | fictional test | yes | yes | no | no | limit 10 |
| `test-dh-no-two-way` | fictional test | yes | no | no | no | limit 11 |

Test-only profile variants built in code by the Simulation tests (`TestRules`) are named `test-*` and are fictional too. `test-dh-forfeit` carries the same DH switches as the
KBO-oriented profile (a test asserts it), so `DhStateTests` exercises exactly the transitions the KBO-oriented profile allows.

### Sources actually read (2026-10-10 and 2026-10-11)

KBO (official): `GameManage2026` (https://www.koreabaseball.com/Kbo/League/GameManage2026.aspx: ABS, pitch clock, 3-foot lane, check-swing and strategic-overrun
review, shift penalty, roster 68/29; nothing on extra innings, DH or scoring); notice 11353 (https://www.koreabaseball.com/MediaNews/Notice/View.aspx?bdSe=11353, 2025-01-22:
regular-season extras reduced from 12 to 11 innings from 2025, no article number); notice 11896 (https://www.koreabaseball.com/MediaNews/Notice/View.aspx?bdSe=11896: the 2026
rule book exists, **its text is not on the page**); notice 11814 (https://www.koreabaseball.com/MediaNews/Notice/View.aspx?bdSe=11814, 2026-01-29: strategic-overrun decision; echoes
that a force is released once the base is touched and that run/out timing decides a lead runner's run; a press release, not an article); record-rules page
(https://www.koreabaseball.com/Kbo/AboutKbo/Committee/RecordRule.aspx: PDF 2023 edition + image e-book; the PDF link answers with a file download that was not retrieved, so **no
scoring-rule text was readable**).

MLB (comparison/fallback): the 2026 Official Baseball Rules PDF (official URL https://mktg.mlbstatic.com/mlb/official-information/2026-official-baseball-rules.pdf) **was read in its original text on
2026-10-11** from the owner-supplied copy `docs/2026-official-baseball-rules.pdf` (SHA-256 `f85860f3…997859`, 1,316,921 bytes, 192 pages, text extracted with `pdftotext -layout`). It was not compared byte-for-byte with the URL. Clause numbers are cited as
verified **only** where `obr_comparison` in the evidence file records the comparison (38 rows, section 11). The earlier reading of the MLB glossary pages (force play, dead ball, appeal plays, designated hitter rule, substitutions,
regulation game, RBI, earned run, save, win, left on base) remains supporting explanation and is not counted as clause confirmation.

KBO additions on 2026-10-11: notice 11795 (2025-12-19 "2026 KBO 리그 규정 개정": its "승부치기 10~12회, 이후 무승부" sentence lies inside the ▲퓨처스리그 경기일정 편성 item, so it is **not** first-team evidence, rule R28) and the KBO news
item bdSe=61318 (2026-06-03 Hanwha–Doosan 3-3, extra inning 11, tie). `docs/2023recordRule.pdf` (KBO 2023 record rules) is in the repository but its extracted text has no Korean glyphs, so it was **not compared** (R26 stays `UNRESOLVED`).

### Result of the policy (details and test ids in `content/rules/game_rules_evidence.json`)

- `KBO_VERIFIED`: regular-season extra-innings limit of 11 (R01; official 2025-01-22 announcement; 2026 operation confirmed by an official 2026 game record; the rule-book clause itself is not read).
- `OBR_FALLBACK_APPROVED`: counts/innings/home skips last half (R04), DH exists (R05), DH ends when he plays defense (R06), two-way DH after leaving the mound (R08), third out and runs
  (R11), batter-runner out after reaching (R12), force play derived from the state (R13), hit by pitch dead ball (R14), RBI (R16), earned run definition (R17), win/loss (R19), save (R20),
  team left on base (R21), substitutions (R23), fielder-to-mound ends the DH (R27, OBR 5.11(a)(14)). **Not KBO certification;** the OBR clauses are now compared against the original text (section 11).
- `GAME_SIMPLIFICATION_APPROVED`: **a game still level when the 11-inning limit ends is a tie (R02, owner application approval 2026-10-11; evidence = the KBO 2025 announcement and the 2026 operating records;
  the KBO rule-book clause is not read, `clause_certified = false`)**, no MLB automatic runner (R03), appeals excluded (R15), earned-run reconstruction (R18), walk-off left on base (R22), no pitch-clock/ABS logic (R24).
- `UNRESOLVED` (none required for the Gate, none implemented): other OBR DH-ending triggers (R07: pitcher to another position, pitcher batting for the DH), replacing a pitching two-way DH (R09), other situations out of scope (R25),
  KBO record-rule text unread (R26), Futures-league extra innings and 승부치기 (R28, a separate profile before any second-team game exists). The DH first-plate-appearance rule (R10) is implemented since 2026-10-11.

**R02 resolution and D2 certification are separate facts.** R02 is no longer `UNRESOLVED`: the owner approved applying "tie at the 11-inning limit" on the basis of an official KBO announcement and real 2026 regular-season games that ended 11 innings level.
That approval is **not** a certification of a KBO rule-book clause and does not by itself make the KBO-oriented profile a certified D2 baseline (the KBO 2026 rule book and record rules are still unread).

**Gate consequence.** Rule-source blockers (`gate_blockers`: required rules that are `UNRESOLVED`) and implementation defects are kept apart. `gate_blockers` is empty. The six engine differences the original-text comparison
(section 11) found (`OPEN_DEVIATION`) were all fixed on 2026-10-11 (section 12) and no new one is open. C33 (inherited-runner counting, Gate-required rule R17) was fixed on 2026-10-11 (section 13) and is `MATCH`; C38 (sacrifice-fly variants) is an owner-approved, explicitly unsupported, not Gate-required follow-up (section 13). Gate-required implementation defects: none open. The Phase 2 Gate is still not declared passed (INCOMPLETE: independent re-review, manual and FHD checks, and D2 certification are outstanding). The status labels are never turned into "certified KBO rule".

## 6. Documented simplifications (not claimed as official rules)

- RBI: batter gets RBI for scoring runs on PA results except reach-on-error, strikeout, grounded-into force / reverse-force double play and non-play causes; no judgement
  case (e.g. error that would have scored anyway) is modelled.
- Earned runs use the replay convention in section 4; run responsibility between pitchers follows the slot ledger of section 4b (a documented simplification only where 4b says so); an error-saved out that would
  have been the third out is classified with the base picture at its move, not re-simulated.
- Sacrifice fly (C38): the basic case only (caught fly ball, exactly one out on the play, fewer than two outs before, a run scoring without error help). Line drives, dropped fly balls (error), combined outs and the outfielder-handled test of OBR 9.08(d) are **not supported**; this is the documented scope of the basic case, not a hidden rule that scores them as an ordinary fly out. Not Gate-required; owner-approved carry-over, to be re-evaluated in Phase 3 with the batted-ball and fielding event model. Nothing beyond the basic case is implemented.
- Wins/losses/saves use the section 4 rule; no "scorer's discretion" decisions.
- The order of outs and runs is scorer input (`Sequence`); the engine checks it for consistency (all-or-none, positive, distinct, batter first when caught) but cannot
  know the physical order. Appeal plays are excluded (section 4a); runners are assumed to have touched every base.
- A batter out after a safe hit is proven only by its out base (`OutAt`); whether he really touched the base the hit gave him is assumed.
- A stolen-base credit stays when the runner's run is voided by the third out.
- `ApproachKind` is recorded and has no effect in Phase 2.
- Pitch totals are counted for every real pitch the event stream contains (a batted ball counts as one).
- Left on base is a team figure only; walk-off runners are counted by profile rule (section 4).
- Who fills the position left by a fielder who goes to the mound with an active DH is the manager's choice in the OBR text; the engine supports exactly one arrangement and only when the event names it (the DH takes the position, the old pitcher leaves, C29); other arrangements and simultaneous multiple substitutions (double switch) are refused, not guessed (C39).
- Further simplifications and open differences against the original OBR text are tabulated in `obr_comparison` (section 11).

## 7. Official records and the independent oracle

### 7.1 Counting keys

`StatKey` (stored per `StatEntry(Team, Player, Key, Amount, SourceId, Partner)`): batting `BatPA AB H 1B 2B 3B HR BB HBP SO SF SH RBI GIDP ROE FC`;
running `RunR RunSB RunCS RunPicked`; pitching `PitchOuts BF H R ER BB K HBP HR WP BK Pitches`; fielding `FldPO A E DP PB`; catcher
`CatPitches CatOuts CatSBA CatCS`; battery `BatteryPitches Outs BF`; team `TeamLOB`. Player lines are summed from entries in a fixed key order.

### 7.2 Reconciliation identities (`BoxScoreReconciler`)

Per team and per game (about 25 checks, each reported with Korean wording): line score sum = team runs = batter runs = opposing pitchers' runs; team hits =
batter hits = opposing pitchers' hits = 1B+2B+3B+HR; PA = opposing batters faced = AB+BB+HBP+SF+SH; walks, strikeouts, HBP and home runs batter = opposing
pitcher; stolen bases = opposing catcher's stolen bases allowed; pitching outs = fielder putouts = catcher outs = battery outs; pitches = catcher pitches = battery
pitches; batters faced = battery batters faced; **the OBR 9.03(c) box proof** (at-bats + walks + hit batters + sacrifices = runs + left on base + the opponent's putouts); ER ≤ R; RBI ≤ runs; catcher exposure sums to the pitch total; battery exposure `SharedAppearances` is 1 per pair; no player appears
twice in the appearance list. Errors (team E) come from fielding-error entries and are compared with the expected result in the 100-game suite.

### 7.3 Scripted games

JSON (`format_version` 1): `rules` (profile ID), `away`/`home` (`lineup[order, player, position]`, `pitcher`, `bench`), `events[]` (`type` =
`approach | take | pitch | in_play | runner_play | substitution` with the same fields as the C# events, enum values snake_case), and
`expected` (outcome, runs, line scores, hits, errors, LOB, per-player stat keys and decisions, catcher/battery exposure). Moves may carry `sequence`,
`out_at`, `out_method` (snake_case enum text), in-play events `batter_sequence`; rule profiles carry `dh_may_be_forfeited` and
`count_walk_off_runners_as_left_on_base`.
`ScriptedGameSource` is internal; the client sees `GameDiagnosticsService` views only.
The loader rejects unknown fields; per-game metadata of the C33 set therefore lives in the sidecar `phase2_c33_responsibility_games_30.meta.json`.

### 7.4 Independent expectations

`tests/Fixtures/generator/generate_scripted_games.py` generates the 100 games **and** their expected values with its own scorer, written from this
document rather than from the C# code, using a fixed seed (stdlib only). Hand-computed oracles in
`tests/StarPlayer.Simulation.Tests` (224 tests) cover each §6.2 case and every Codex finding with expectations written by hand in the test, not read from the engine.
`ScriptedGameSuiteTests` replays the 100 games through the real reducer and compares final score, hits/errors, per-player batting and pitching,
outs/innings, PA/AB classification, catcher and battery participation, and pitcher earned/unearned runs against the generated expectations; it
also checks per-event invariants, finalize-once and, with tampered expectations, that the comparison really fails.

The generator also implements the third-out rule, the force derivation and the walk-off left-on-base policy on its own (`resolve`,
`reconstruct`), and produces plays with a third out and a run in both orders (about ninety-seven events across the 100 games: counted runs before a non-force third
out, voids by force out, by batter-runner out and after a non-force third out, double steals).

The same generator draws a second set of 30 games (`tests/Fixtures/phase2_c33_responsibility_games_30.json`, optional fourth argument): many pitching changes with runners on, many fielder's choices including the forced retirement of the lead runner, and only candidates in which at least one run is charged through a moved slot (33 runs in 30 games).
The 100 games contain **none** (measured: 8266 charges, 31 fielder's-choice outs, 0 differences), so they prove no more than "no regression" for C33; the 30-game set does exercise it: with the ledger switched off the comparison fails with 99 mismatches. It is an integration check like the 100 games.

The 100-game suite is an **integration check, not an independent rule oracle**: the Python scorer shares the author and the reading of the rules with the engine. The
independent check is the set of hand-computed tests (`ThirdOutAndOrderTests`, `BatterRunnerAfterHitTests`, `HitByPitchTests`, ...), kept separately.

Independence limit (stated honestly): the generator and the engine are written by the same author from the same reading of this
document, so they share conventions (section 6). The suite proves the engine implements the documented rules consistently; it cannot
prove the documented simplifications equal the official KBO rules (section 5 open items).

## 8. Test criteria (Phase 2 Gate G +)

| Criterion | Where tested |
|---|---|
| Count boundaries, fouls with two strikes, foul bunt, walk/strikeout/HBP, dropped third strike | `CountAndPlateAppearanceTests` |
| Take then resume at the exact count and runners; Take illegal after approach; every-pitch input not required | `CountAndPlateAppearanceTests`, `GameFlowTests` |
| 1B/2B/3B/HR, ground/fly/line outs, errors, FC, DP, force/tag, SF/SH, base combinations (empty to loaded) | `CountAndPlateAppearanceTests`, `RunnerPlayAndEarnedRunTests` |
| Steal/CS/pickoff/balk; WP and PB separately incl. earned-run consequence | `RunnerPlayAndEarnedRunTests` |
| Inning change, walk-off, home skips last half, extra innings and tie per profile | `GameFlowTests` |
| Substitutions, DH, two-way, no-DH pitcher slot, legality errors | `SubstitutionDecisionAndBatteryTests` |
| Mid-inning catcher change, battery exposure counted once, inherited runners, earned/unearned, decisions | `SubstitutionDecisionAndBatteryTests`, `RunnerPlayAndEarnedRunTests` |
| Duplicate play ID, duplicate finalization, rejected play leaves state unchanged | `GameFlowTests` |
| Rule-profile snapshots, unverified/fictional labels, corrupted profile rejected | `GameFlowTests`, `GameRulesProfileTests` |
| 100 scripted complete games reconcile and match independent expectations | `ScriptedGameSuiteTests` |
| Phase 0/1 regression, architecture boundaries, hidden-data policy | all existing suites + `tools/verify.ps1` |
| Domain collection immutability (Phase 1 debt) | `CollectionImmutabilityTests` |
| Third out: force derived from the state, run/out order, batter-runner third out, force removed by an earlier out, plays between pitches, validation errors, earned-run reuse | `ThirdOutAndOrderTests`, `CodexReproTests` (1, 2), the 100 scripted games (`ReplayContractTests.The_hundred_games_exercise_...`) |
| Event id: duplicate vs conflict, fingerprint contract, JSON round trip, receipts, after game over | `EventIdentityTests`, `ReplayContractTests`, `CodexReproTests` (3) |
| DH state: separate from the rule, forfeiture, no re-creation, team independence, two-way, state kept through the fixture contract | `DhStateTests`, `ReplayContractTests.The_dh_state_...` |
| Hit by pitch dead ball; strikeout credit validity | `HitByPitchTests`, `CodexReproTests` (4) |
| Batter-runner out before / after reaching first, hit + tag out, evidence of reaching, order swap, accounting (hit, at-bat, outs, RBI, runs, ER, box score) | `BatterRunnerAfterHitTests`, `ReplayContractTests`, the 100 games |
| Appeals excluded and refused | `AppealExclusionTests`, `ThirdOutAndOrderTests` |
| Rule evidence bookkeeping, OBR comparison table, R02 wording, Gate blockers, fixed deviations name existing hand tests | `RuleEvidenceTests` |
| OBR 9.03(c) box proof, exact, in all 200 team-games of the 100 games and in the hand cases; reconciler detects a missing left-on-base runner | `BoxProofTests`, `ObrDeviationTests`, `ScriptedGameSuiteTests` |
| Double play vs grounded-into double play; RBI after a non-force double play (C12) | `ObrDeviationTests`, `CountAndPlateAppearanceTests` |
| OBR 9.16(i) official earned-run examples 1-3 with `TeamER` (C14); 9.16(h) walk-count table (C34) | `ObrDeviationTests`, `RunnerPlayAndEarnedRunTests` |
| OBR 9.16(g) Comment examples 1-7, caught stealing / pickoff / pinch runner boundaries, R vs ER, loser by 9.17(d), rejected event leaves state, replay (C33) | `ResponsibilityLedgerTests` |
| Slot ledger at game scale against the Python scorer; every run charged once; pitchers' R add up to credited runs after every event; JSON round trip and replay (C33) | `ResponsibilityScriptedGamesTests` |
| Starting DH must bat once; two-way exception only for the starting pitcher listed as DH; explicit DH slot for fielder-to-mound (C24, C32, C29) | `ObrDeviationTests`, `DhStateTests`, `SubstitutionDecisionAndBatteryTests`, `ReplayContractTests` |
| Walk-off left on base by kind of ending and profile policy; team-only LOB | `WalkOffLeftOnBaseTests` |

## 9. Codex review of 2026-10-10 (CHANGES REQUIRED) and what was changed

The independent Codex review of Phase 2 returned `CHANGES REQUIRED`: High 1 (third out and run decision), High 2 (event id idempotency collision), High 3
(DH kept/lost state), Medium 4 (hit by pitch combined with wild pitch / passed ball), plus a remark on left on base at a walk-off. This section keeps that
verdict and records the fixes; the first-round Phase 2 text (sections 1 to 8 before this amendment) is otherwise unchanged in intent.

| Finding | Cause in the first implementation | Fix | Main tests |
|---|---|---|---|
| High 1 | outs carried only a declared `OutKind` that was trusted; the third-out rule looked at "is any out a declared force out" and a scorer flag `TimingPlayRuns`; the order of outs and runs did not exist; a null kind on a runner out was accepted | `Sequence`/`OutAt`/`Method` on the move, required out kind, force derived from the state and the order, third-out evaluation shared with the earned-run replay (`ThirdOutRule`), voided runs, `TimingPlayRuns` removed | `ThirdOutAndOrderTests` (16), `CodexReproTests` 1-2, 100-game coverage test |
| High 2 | a processed event was remembered by id only, so another event with the same id was silently treated as a duplicate | `EventReceipt` with kind and `ef1` fingerprint, `Game.event_id_conflict`, fixture contract for events and receipts | `EventIdentityTests` (10), `ReplayContractTests` (5) |
| High 3 | the engine read `GameRules.DesignatedHitter` everywhere, so a team could never lose its DH and a transition could not even be expressed | `TeamGameState.DhActive`, rule flag `DhMayBeForfeited`, two fictional transitions, no re-creation, independent teams | `DhStateTests` (13) |
| Medium 4 | `HitByPitch` accepted a mishap and its moves | dead-ball validation with explicit error codes | `HitByPitchTests` (8) |
| Walk-off LOB | a walk-off always recorded 0 | runners on base at game end counted by profile rule, team only | `WalkOffLeftOnBaseTests` (8) |

## 10. Codex re-review of 2026-10-11 (CHANGES REQUIRED) and the owner decisions of the same day

The Codex re-review returned `CHANGES REQUIRED` again: **High** — a batter-runner who reached first on a hit and was then put out could not be expressed
(a hit result made any batter out a "before first base" out); **Medium** — an appeal third out (preceding runner who missed a base) was not handled. The first verdict (section 9) is kept.

| Item | Handling |
|---|---|
| High: batter-runner out before vs after reaching first | fixed: `Move(From = 0, To = 0)` on a hit, proven by `OutAt` beyond the hit base, classified NonForce (section 2.1, section 4), earned-run replay uses the stored class, a hit with two outs is no double play. Tests `BatterRunnerAfterHitTests`, 100-game scenarios (counted and voided runs), JSON round trip |
| Medium: appeal third outs | **approved scope exclusion** (owner, 2026-10-11): appeals are not implemented and refused explicitly (section 4a); the Medium finding is closed as `승인된 범위 제외`, **not** as "implemented" |
| New: rule-source policy KBO first / 2026 OBR fallback | section 5 and `content/rules/game_rules_evidence.json`; KBO-oriented profile now allows the OBR-style "DH plays defense ends the DH" transition, the invented fielder-to-mound transition stays fictional-only |

## 11. R02 decision and the OBR original-text comparison (2026-10-11)

Instruction: GPT Chat review of the final Phase 2 fix (`TASK.md`). The status of the phase stays `검토 대기`; nothing is committed or pushed; Phase 3 is not started.

**R02.** Owner application approval: a regular-season game still level after the 11th inning is a tie. Basis (kept apart from clause certification): KBO board announcement of 2025-01-22 (limit 11) and the official KBO news item of the
2026-06-03 Hanwha–Doosan game (3-3, ended after the 11th); a press report of 2026-10-04 KT–Lotte (2-2) was read as additional unofficial support. The KBO league-regulation clause was not found (`clause_certified = false`). The evidence status is
`GAME_SIMPLIFICATION_APPROVED`, no longer `UNRESOLVED`. Risk recorded: notice 11795's "승부치기 10~12회 이후 무승부" belongs to the Futures-league item, not to the first team (R28).

**OBR original text.** `docs/2026-official-baseball-rules.pdf` was read; clauses 5.05, 5.06(c), 5.08, 5.09(a), 5.10, 5.11, 7.01, 9.02-9.04, 9.08, 9.16, 9.17, 9.19 and the definitions FORCE PLAY and DOUBLE PLAY were compared with the engine
(38 rows, `obr_comparison`). Clauses outside this list were not compared (`obr_not_compared`). Result classes: `MATCH` 16, `KBO_PRECEDENCE` 1, `APPROVED_SIMPLIFICATION` 9 (owner decision or recorded before this round),
`PROPOSED_SIMPLIFICATION` 3 (first recorded now, **not** called approved), `NOT_MODELLED` 3 (refused input, safe side), `OPEN_DEVIATION` 6.

| Open difference | OBR clause | Engine now | State |
|---|---|---|---|
| C12 | 9.04(b)(1), 9.02(a)(17) | any 2-out non-hit play is a double play (GIDP, no RBI); OBR only force / reverse-force double plays | open, code reading only |
| C14 | 9.16(i) | earned-run reconstruction is per half-inning and ignores pitching changes (no team-unearned runs) | open, code reading only |
| C19 | 9.03(c) | runs voided by the third out are not left on base, so the box proof is short in 70 of 200 team-games (exactly by the voided runs) | open, **measured** (`BoxProofTests`) |
| C24 | 5.11(a)(2) | a pinch hitter may replace the DH before his first plate appearance | open, not required for the Gate |
| C32 | 5.11(b), (a)(4)(9)(12) | any DH-slot player (also a pinch hitter) may take the mound and keep the DH under the two-way rule | open, code reading only |
| C34 | 9.16(h) | the relief pitcher's first-batter walk goes to the previous pitcher only at 3-0 / 3-1; OBR: 2-0, 2-1, 3-0, 3-1, 3-2 | open, code reading only |

Each open row records Issue, Reason, Recommended Option, Alternative and Impact in the evidence file. "Code reading only" means the difference follows from the code and no failing reproduction test exists yet (`미검증` as a test). None of the six was changed in the engine in that first round (no scope expansion); the owner then ordered all six fixed, see section 12 (the table above is the state before those fixes).

**DH classification.** OBR 5.11(a)(14) is an original clause: a fielder replacing the pitcher ends the DH. The earlier "fictional / invented" label of `fielder_to_mound_forfeits_dh` was wrong and is corrected (rule R27). The flag is now on in the KBO-oriented profile (one
profile field; both transitions were already implemented and tested). The 5.11(b) two-way exception was also read: the pitcher-as-DH keeps the DH after leaving the mound (matches), but the engine's entry path into pitching is wider than the clause (C32).

**Consequences for earlier statements.** "OBR clause numbers unread" (sections 5, 6 and the first revision of the evidence file) is superseded; the MLB glossary readings are supporting text only. The rule R21 statement "a run that was voided is not a left-on-base runner" is not supported by 9.03(c) (C19). No existing test expectation was changed.

## 12. Fix round for the six OBR differences (2026-10-11, GPT Chat instruction "OBR 편차 처리 최종 결정")

R02 stays as decided (section 11). For each of C12, C14, C19, C24, C32 and C34 a minimal hand-worked reproduction was written first, it **failed on the unchanged engine** (15 failing tests, recorded in the report), then the
engine was fixed. C29 was handled as an explicit-request rule. Nothing was approved as a simplification to make a difference disappear.

| Row | OBR | Fix | Main hand tests (`ObrDeviationTests`) |
|---|---|---|---|
| C12 | 9.02(a)(17), 9.04(b)(1), 9.11 | `GroundedIntoForceDoublePlay`: GIDP and withheld RBI only for force / reverse-force double plays on ground balls; defensive DP credit stays separate | reverse-force DP, non-force DP (no GIDP, DP credit), RBI kept after a non-force DP, line-drive DP, hit with two outs, K then CS |
| C14 | 9.16(i) | `EarnedRuns.Reconstruct(half, limit, startIndex)`: a pitcher's earned runs are judged from his own entry; `StatKey.TeamER` = whole-inning reconstruction | the three official examples |
| C19 | 9.03(c) | voided-run runners are left on base once per runner; reconciler checks the proof | batter-runner third out, force third out, walk-off, 200/200 |
| C24 | 5.11(a)(2) | `TeamGameState.StartingDh`; `Game.dh_must_bat` | pinch hit and defensive sub refused/allowed, state untouched |
| C32 | 5.11(b), (a)(12) | DH-slot player to the mound ends the DH (or is refused); the exception is `HasTwoWayStarter` | ordinary DH, replacement DH, two-way starter |
| C34 | 9.16(h) | walk charge at 2-0, 2-1, 3-0, 3-1, 3-2 | theory over 11 counts, K by the reliever |
| C29 | 5.11(a)(5), (14) | the event must carry `Slot` = the DH's order; no automatic placement | unspecified / wrong slot / explicit |

Existing tests whose hand-counted expectation had to change (each re-derived from the clause, none weakened): left on base after a voided run (+1) in `ThirdOutAndOrderTests` (3), `CountAndPlateAppearanceTests` (1),
`BatterRunnerAfterHitTests` (1); the 3-2 walk in `RunnerPlayAndEarnedRunTests`; the DH walking to the mound in `SubstitutionDecisionAndBatteryTests`; `Slot` for fielder-to-mound and an opposing pitching change before a DH
replacement in `DhStateTests`. The 100 games were regenerated by the Python scorer (updated with the same clauses, independently implemented from this document) and match 100/100; the hand oracles remain the primary check.

**Pending owner decisions at that time (history; both are decided in section 13):** C33 (9.16(g) inherited-runner counting by number of runners; Gate-required rule R17) and C38 (9.08(d) sacrifice-fly derivation; not Gate-required). Both rows in the evidence
file carry the original rule, the difference, the impact and a recommendation. A fly-ball double play that scores a run now gives the RBI (9.04(a)(1)) but remains an at-bat instead of a sacrifice fly (C38).

## 13. C33 final recording responsibility (2026-10-11, owner instruction TASK.md)

The owner decided C33 must be fixed before final approval and C38 is carried over. **C33:** the slot ledger of section 4b replaces the one-pitcher-per-runner charge. The independent oracle was written first from the 2026 OBR text (`ResponsibilityLedgerTests`, examples 1-7 and boundaries); on the unchanged engine 8 of 17 tests failed (log:
`docs/reports/PHASE_2_C33_RED.log [Git 제외 원본, 추적 요약: docs/reports/PHASE_2_RED_EVIDENCE.md]`), after the fix all 17 pass. The Python scorer was changed with the same clauses; the 100 games regenerate byte-identical (no game exercises the rule), so a 30-game set in which every game contains a moved slot was added (section 7.4); the engine matches it on every number, and fails it 99 times with the ledger off (negative control).
Existing tests were not changed; C34 (the 9.16(h) walk charge) is unchanged and the walked runner's slot goes to the previous pitcher as before.
**C38:** owner-approved Gate non-required carry-over: the basic sacrifice fly stays, variants stay unsupported and are documented as such (section 6); re-evaluate in Phase 3 together with the batted-ball and fielding event model. It is not implemented.

## 14. Ambiguous responsibility on a fielder's choice (2026-10-11, Codex focused re-review, High; owner instruction)

**Defect found.** After the C33 fix, a fielder's choice in which two runners of *different* pitchers were put out made the engine give the batter the slot of the oldest pitcher. The input does not say which out was the fielder's choice (slot passes on, 9.16(g)) and which was an independent out on a runner's own advance (slot disappears,
examples 4 and 7), so that choice was arbitrary and could move a run to the wrong pitcher. Reproduction: Peter's runner on second, Roger's on first, Sierra pitching; the batter reaches on a fielder's choice, the runner on first is forced out at second and the runner on second is tagged out at home going for more.

**Decision (owner, a Phase 2 support limit; not an OBR or KBO statement).** The event is refused with `Game.ambiguous_responsibility` before any state change: the check runs in `FinishPlay` after plan validation and order resolution succeed and before outs, runs, RBI, statistics, slots or the event receipt are produced. The refusal condition is exact: result `FieldersChoice`, the batter reaches base,
two or more runners are put out on the play, and the slots (`ResponsiblePitcher`) of those runners belong to more than one pitcher. The order of the outs does not matter (tested both ways). Nothing is refused when the put-out runners all belong to one pitcher (that pitcher's slot is the only possible one), when one runner is put out (examples 2, 3, 5, 6), or when no fielder's choice is claimed.

**Bypass check.** The only place a slot passes to a batter is `InPlayResult.FieldersChoice`; every other path creates a fresh slot of the pitcher on the mound and removes the slots of runners put out (hit, reach on error, walk, hit by pitch, dropped third strike, steal, pickoff, balk, wild pitch, passed ball). Entering the same physical play as a single was **a silent way around the rule** and is closed in section 15 (a hit is refused when a runner is forced out). A reach on error is not a fielder's choice in the scorer's classification and then both slots disappear; whether the error really caused the batter to reach is not
decidable from the event (section 15, limit of `ReachedOnError`). Runner-play events carry no batter. The refusal is the same for a re-delivered event (no receipt is written, so there is no duplicate to replay).

**What the scorer can do.** Re-enter the plate appearance only with the official scoring classification that fits the real play (never to avoid the refusal: a different label is allowed only if it is what the play officially was). The engine never guesses and never changes a result.

## 15. No base hit on a batted ball that forces out a runner (2026 OBR 9.05(b)(1), 2026-10-11, Codex final focused review, High)

**Defect.** `Single`, `Double` and `Triple` were accepted together with the force out of a preceding runner on the same batted ball. OBR 9.05(b)(1): the scorer shall not credit a base hit when a runner is forced out by a batted ball, or would have been forced out except for a fielding error. Consequence: the play of section 14 (refused as a fielder's choice) could be
entered as a single and was then recorded with a hit and an at-bat - the C33 refusal was bypassed by relabelling. Applied through the approved KBO-first, 2026-OBR-fallback policy; **not a KBO certification**.

**Fix (first clause of 9.05(b)(1) only).** In `FinishPlay`, after plan validation and order resolution succeeded and before slots, outs, runs, statistics or the receipt exist, a result of `Single`, `Double`, `Triple` or `HomeRun` whose play contains an out classified `OutClassification.Force` is refused with `Game.hit_with_force_out`. The classification is the one
`ResolveOrder` derives from the state, not the label: a runner forced and put out at the base he was forced to is a force out however the out was made (a tag counts), a force removed by an earlier out of the runner behind is respected (the later out is a tag out and the hit is not refused by this rule), a batter put out after a safe hit is not a force out of a preceding runner.
The state instance is returned unchanged, nothing is recorded, the same event gives the same error again, and the engine never rewrites the result into a fielder's choice or a reach on error. Accepted hits are unchanged (hit, at-bat, RBI, a runner or the batter thrown out going for more).

**Second clause ("would have been forced out except for a fielding error"): fixed in section 16 (C43).** 9.05(a)(6), (b)(2)-(5) need scorer judgement, an appeal or interference input and are not decided by the engine: this rule is not a full implementation of the hit-denial rules.

**Limit of `ReachedOnError`.** The event checks that at least one error is recorded, not that the error was the direct cause of the batter reaching (the batter would have been out without it). It is therefore neither certified as always legal nor refused wholesale: a valid reach on error is accepted, and so is a reach on error with a runner forced out on the same ball (reproduced; evidence row C42, `NOT_MODELLED`; the causal question is partly handled for hits in section 16). The
"re-enter with another result" hint of section 14 and the C33 error message holds only when that classification is the official one of the real play.

## 16. Hit and an error that prevented a force out (OBR 9.05(b)(1) second clause, C43, 2026-10-11)

**Reproduced difference.** A `Single` carrying a runner on first moved to second as `ErrorSavedOut` (the scorer's statement that the error saved him from being put out) was credited as a hit. The first clause of 9.05(b)(1) was already enforced for real force outs (section 15).

**What the input can prove.** Two facts: the scorer's explicit cause `ErrorSavedOut` on a runner, and the force status of the out he would have suffered, which `ResolveOrder` derives from the state (every base behind occupied by a runner not yet retired, out at the base he was forced to) and which respects a force removed by an earlier real out. An error is otherwise not tied to anything: the event lists the fielding positions of the errors and the moves carry a cause, nothing says what a listed error did.

**Rules (all checked in `FinishPlay` after plan validation and order resolution, before any statistic, slot or receipt; the state instance is returned unchanged; a re-delivered event gets the same error; no result is ever changed into a fielder's choice or a reach on error).**
1. *Confirmed:* a `Single`/`Double`/`Triple` with an `ErrorSavedOut` runner whose derived classification is `Force` -> `Game.hit_with_force_out_saved_by_error`. If an earlier real out removed the force (the later would-be out is `NonForce`), the hit stands; in the order that keeps the force it is refused (tested both ways). A runner saved from a non-force out (for example from a tag at third with first base empty) leaves the hit untouched. (A double or triple puts the batter on second or third, so a first-base runner saved at the forced base cannot exist: such an input is already invalid.)
2. *Unsupported (cause unclear, C44):* a `Single`/`Double`/`Triple` with a runner on first at the start of the play (a forced runner exists) and at least one recorded error is refused with `Game.hit_error_cause_unclear` when **(a)** no move carries an error cause (`ErrorExtraBase`, `ErrorSavedOut`), or **(b)** two or more errors are recorded - **whatever number of error-caused moves exist**. The event lists error positions and gives moves a cause, but nothing links an error to the move it caused, so counting moves cannot show that every error is explained, and the plain forced advance of the runner might hide a force out one of the errors prevented. With exactly one error, one error-caused move is taken as the scorer's statement of what that error did (still not verified cause by cause). *Check order and error codes:* `Game.hit_with_force_out` (a real forced-out runner) -> `Game.hit_with_force_out_saved_by_error` (confirmed C43) -> `Game.hit_error_cause_unclear` (C44, unsupported); whichever fails first is the code returned. Multiple errors on one play can be perfectly legal in baseball; refusing them is a **game support limit caused by the input model**, not a KBO/OBR rule.
3. *Not doubted:* a hit with no runner on first (no forced runner can exist), a hit with no error, a hit with several errors and no runner on first, and a hit with exactly one error that an error-caused move marks (assumption: that move is what the recorded error did; not verified). Fielder's choice, reach on error, outs and walks are not judged by these rules.

**Support limit and who approved it.** Rule 1 is the OBR text applied through the KBO-first / OBR-fallback policy. **Rule 2 is the approved Phase 2 support limit C44** (evidence row C44, `NOT_MODELLED`). Approval record: decided on 2026-10-11 by **GPT Chat's design judgement**, relayed by the user in `TASK.md`; no separate direct owner approval is recorded, and Claude Code did not approve it. Decision content: (a) a hit with a runner on first and a recorded error that the current input cannot explain is refused explicitly; (b) an `ErrorSavedOut` combined with a verified force out is refused by the existing C43 check (rule 1); (c) supported events such as `ErrorExtraBase` on a normal hit are kept; (d) not every hit with an error is rejected (2026-10-11 Codex Medium: the condition also covers two or more errors with a runner on first, whatever number of error-caused moves exist); (e) an error whose cause is unconfirmed is never reclassified as another event. It is the narrowest check that does not forbid every `Single` with an error, and it rejects legal plays the input cannot distinguish (a first-base runner and an error whose effect the model cannot express, for example a batter's own extra base on a throw). It is **not** KBO official certification and **not** a complete OBR implementation. A small additional input naming what each error did (a cause per error) would remove the limit; it is deferred to the Phase 3 batted-ball and fielding model. `ReachedOnError` (C42) is unchanged: valid reaches on error are accepted, the direct-cause question is not modelled, and nothing here certifies them.

## 17. Phase 3-A: seeded random numbers (2026-10-11; decisions in `docs/decisions/ADR-004.md`)

The reducer above is unchanged and still has no randomness. Phase 3-A adds only the random-number foundation the later resolver (3-B to 3-F) will draw from, in `src/StarPlayer.Simulation/Randomness/`:

| Type | Role |
|---|---|
| `Pcg32` | PCG32 XSH-RR 64/32, version `pcg32-xsh-rr-v1`; unbiased `NextBounded` by rejection sampling; `Draws` counter; `Advance` jump-ahead; `FromState` |
| `RngStreamId`, `GameRngStreams` | stream identity `name@version`; the six game streams `game/pitch`, `game/contact`, `game/field`, `game/runner`, `game/location`, `game/ai` (all `@1`); Phase 3-C2A adds a seventh, `game/target@1` (ADR-004 item 20) |
| `StreamKeyDerivation` | `kd1`: SHA-256 of (master seed, context key, stream id) gives each stream's start; streams cannot affect each other |
| `GameRandom`, `RngStream` | the streams of one game; lazy creation; `Atomically(work, token)` publishes a private working copy only on success; the original is locked while a scope runs |
| `GameRandomSnapshot` | `rngset1` snapshot (only streams that have drawn) and `StateHash`; `GameRandom.Restore(snapshot)` is structure-only; `Restore(snapshot, expectedStateHash)` is the loading contract |
| `GameIds`, `GameEventIds` | `game:{competition}:{fixture}` and `{game id}:e{sequence:D6}`; no random source, no clock |

Rules for the resolver (binding on 3-B onward): draw only from the stream named for the purpose; use `NextBounded` with a fixed-denominator integer scale for probabilities (no floating-point draw exists yet); never draw in presentation, Broadcast Commentary Lite or Live Match Viewer Lite; the `game/location` draw of an actual pitch comes before that pitch's outcome draws (ROADMAP §1.14). A game step that may fail runs inside `GameRandom.Atomically` together with the reducer call, and the snapshot is committed with the game state and event receipts (Phase 4 persists it). Not implemented in 3-A: probabilities, location drawing, `ef2`, persistence.

Guarantees and limits (final fix round, ADR-004 items 13-19):
- **Snapshot.** Streams with zero draws are not in the snapshot, so looking at a stream never changes `StateHash`. `Restore(snapshot)` checks versions, canonical order, no duplicate/unused entries, increment, draw count and state; it **cannot** see a consumed stream that was removed. A loader must call `Restore(snapshot, expectedStateHash)` with the hash of the live state recorded at commit time; Phase 4 writes that hash in the same transaction as the game state and event receipts. The hash detects corruption and lost parts; it is **not** authentication.
- **`Atomically`.** Covers only the in-memory RNG state of that instance; while it runs the original is locked (draws, `Stream()`, nested scopes throw `InvalidOperationException`), and the copy is closed afterwards. It does not cover event-id counters, `GameReducer`/`GameState`, receipts or external effects, and publishes when the work returns, not after a durable commit.
- **Draw limit.** `Draws` is a `long`; at `long.MaxValue` a draw throws before changing state, `Advance` validates first.
- **Threads.** Single serialized writer; concurrent use is not supported and not detected.
- **Phase 3-C/4 handoff (recorded only):** event-id sequence and reducer result as one unit; one success unit for state, receipts and RNG; publishing memory only after the durable commit; the real `game/location` consumption order and probability scale; `ef2` and replay.

## 18. Phase 3-B1: Pitch Zone Lite data contracts (2026-10-11; decisions in `docs/decisions/ADR-005.md`)

3-B1 adds only data models and a rule-profile field. No pitch location is drawn, no event, `ef1`/`ef2` or reducer rule changed, and no probability exists. All code is in `src/StarPlayer.Domain/Baseball/PitchZone/` and `GameRules.cs`.

**Model id and version policy.** `ZoneModelVersion` `pzl-zones-v1` fixes (a) the 17 zone ids, (b) their batter-relative meaning, (c) the boundary table below and (d) the catcher's-view convention. Only versions this build knows can be created (`ZoneModelVersion.unsupported` otherwise; never defaulted). Renaming, adding or re-meaning a zone, changing one boundary class or the catcher's-view convention is a **new version**, never an edit. `PitchZoneId` is the persisted text (`in.r1c1`, `out.high_in`); anything else is `PitchZoneId.unknown`.

**Zones (canonical order).** Inner `in.r1c1`..`in.r3c3` row-major (row 1 high -> 3 low; column 1 inside -> 3 outside, batter-relative); outer `out.high`, `out.high_in`, `out.in`, `out.low_in`, `out.low`, `out.low_out`, `out.out`, `out.high_out`. `PitchZoneInfo` carries region, lateral (Inside/Middle/Outside), vertical (High/Middle/Low), boundary class and the inner row-major index (-1 for outer).

**Boundary table (`Clear`/`Borderline`, a pure function of the zone).** Rule: a region that shares an *edge* with the strike-zone outline is `Borderline`; the centre cell and the four diagonal outer zones, which touch the outline at most at a point, are `Clear`.

| Class | Zones |
|---|---|
| `Clear` (5) | `in.r2c2`, `out.high_in`, `out.low_in`, `out.low_out`, `out.high_out` |
| `Borderline` (12) | `in.r1c1`, `in.r1c2`, `in.r1c3`, `in.r2c1`, `in.r2c3`, `in.r3c1`, `in.r3c2`, `in.r3c3`, `out.high`, `out.in`, `out.low`, `out.out` |

The class is **virtual and categorical**: it is a property of the region, not a measurement of a pitch, and no accuracy of the real KBO ABS is claimed. Because it is a function of the zone alone, the location draw (3-C) needs no extra draw for it. `PitchLocation.Create(model, zone, side, boundary)` accepts only the table's class for that zone (`PitchLocation.boundary_mismatch`), `FromZone` derives it. Frozen before any calibration run.

**Batter side.** `AppliedBatterSide` {Right, Left} is the side actually used for one pitch and is part of `PitchLocation`. `AppliedBatterSideRules.Validate(declared BattingSide, applied)`: a Right/Left batter can only have his own side, a Switch hitter either (`AppliedBatterSide.not_possible` otherwise); `OnlySide` gives a one-sided batter's side and refuses a switch hitter (`choice_required`). *Which* side a switch hitter chooses per pitch is a pitch-resolver rule for 3-C. `ToCatcherView`/`FromCatcherView` convert between the batter-relative zone and a `CatcherViewPosition` (region, left/centre/right, high/middle/low); this model's convention is that a right-handed batter's inside is the catcher's left. The 17 zones are one-to-one with catcher's-view positions for each side. `TargetZone` (version + zone) is a separate type with no side or class.

**Batter zone tendency.** `BatterZoneTendency`: nine integers (row-major inner cells), each `-100..+100`, default all 0 (`Neutral`); +100 = the full per-zone cap of the later probability model in the batter's favour (the cap and every probability link are 3-B2/3-D). Average-effect neutrality: the sum of `value[i] x weight[i]` over a named `ZoneReferenceDistribution` must be exactly 0 (`not_average_neutral`); the stored `ReferenceId` says which. `ZoneReferenceDistribution.UniformV1` (`pzl-ref-uniform-v1`) is exactly nine weights of 1 and is the interim reference; **an id and its weights are one immutable registered definition** (`Resolve(id)`; `Create(id, weights)` rejects an unknown id with `id_unknown` and other weights under a known id with `weights_mismatch`; no public way to build an unregistered reference), so a stored `ReferenceId` always means the weights that judged the vector. A tuned reference is a 3-B2 decision and must be registered under a new id with fixed weights. Wrong length, a value outside the bound (each side checked) or a missing vector/reference is rejected with a stable code. Outer zones are not adjusted (`AdjustmentFor` = 0). It holds no link to Ratings/Contact and has no growth path.

**Hidden-data boundary.** The values are for the game resolver only. `ToString()` never prints them; a test (`HiddenTendencyBoundaryTests`) fails if any member signature in the Application or Infrastructure assemblies carries a `BatterZoneTendency`/`ZoneReferenceDistribution`; the Godot client cannot name Domain at all. The opponent pitching AI, UI, tooltips, news and Gemini payloads get no vector; a later weakness-exploiting AI may use only observable-record reports (ROADMAP 1.14, deferred).

**CalledPitchMode and `gamerules-v2`.** `CalledPitchMode` {`Automated`, `NonAutomated`} is owned by C40. `GameRuleSnapshot` gains `CalledPitchMode?`, `ZoneModel`, `RulesSchema` (`gamerules-v1`/`gamerules-v2`), `CalledPitchModeDeclared` and `FramingMayChangeCalls()`.
- *Legacy:* a profile without the fields has `CalledPitchMode == null`: "not declared" is kept as a fact. It is never read as Automated, NonAutomated or the real 2026 KBO ABS mode, and `FramingMayChangeCalls()` returns the error `GameRules.called_pitch_mode_undeclared` for it. The legacy canonical hash text is unchanged (new text is appended only when a mode is declared), so the seven existing hashes are byte-identical (independent Python oracle, `tests/Fixtures/generator/legacy_gamerules_hashes.py`).
- *New:* both fields are declared together or not at all (`GameRules.pitch_model_incomplete`); an undefined mode is `called_pitch_mode_invalid`. The hash of a v2 snapshot appends `|gamerules-v2|{mode}|{zone model}`.
- *JSON:* `game_rules_profiles.json` stays `format_version` 1 and untouched; a v1 file containing the new members is rejected (unknown member). `format_version` 2 (`content/rules/game_rules_profiles_v2.json`, fictional test profiles only) requires `called_pitch_mode` (`automated`/`non_automated`) and `zone_model` for every profile; an unknown mode is `Fixture.enum_invalid`, an unsupported zone model `ZoneModelVersion.unsupported`, another format number `Fixture.version_unsupported`.
- *Contract:* in `Automated` mode Framing's probability effect on a call is exactly zero; in `NonAutomated` mode it may act only on `Borderline` pitches within a cap and never changes a committed call (the cap and the computation are 3-E).

**Inputs the later 3-B2 probability model will receive (not implemented).** `ZoneModelVersion`/`PitchZoneModel`, the pitcher's `TargetZone`, the actual `PitchLocation` (zone, applied side, boundary class), the batter's `BatterZoneTendency.AdjustmentFor(zone)` (resolver-side only), the rule snapshot's `CalledPitchMode` (an undeclared mode must stop the PZL resolver with an error) and integer-scaled conditional distributions with a fixed denominator.

**Where 3-C attaches.** The resolver reads `PitchZoneModel.V1`, picks a `TargetZone`, draws the zone from `game/location` (ADR-004 item 7), builds the location with `PitchLocation.FromZone` and the side from `AppliedBatterSideRules`, then resolves the pitch; event payloads and `ef2` are 3-C. Nothing in 3-B1 touches `GameEvents.cs`, the reducer, `ef1` or any golden vector.

## 19. Phase 3-B2: integer conditional probabilities and the virtual tuning contract (2026-10-11; decisions in `docs/decisions/ADR-006.md`)

3-B2 defines the probability arithmetic, the model structure, a versioned virtual tuning data set and the calculation trace. **It resolves no pitch, draws no random number and changes no event, `ef1`/`ef2`, reducer, rule profile or RNG stream.** Code: `src/StarPlayer.Domain/Baseball/Probability/` (pure, no floating point), the reader `Infrastructure/Fixtures/TuningProfileJson.cs`, data `content/tuning/virtual_tuning_v1.json`.

**Scale.** A probability is a whole number of ppm; the denominator is `1,000,000` (`ProbabilityScale.Denominator`). Every conditional `Distribution` sums to exactly the denominator, each value is `0..1,000,000`. `ProbabilityScale.Normalize` (`largest-remainder-lowest-index-v1`): `floor(w*T/S)` per outcome, leftover units to the largest remainder, ties to the lowest index, zero weights stay zero, `Int128` arithmetic, 1-64 outcomes, weights `0..2^56`; invalid input is an explicit `Probability.*` error. Products round half up (`half-up-v1`). **Roll contract:** one categorical decision = one `RngStream.NextBounded(1,000,000)` call; `Distribution.Select(roll)` gives outcome `i` exactly `Ppm[i]` consecutive rolls (a zero outcome is never selected). `NextBounded(10^6)` skips about 0.0225% of raw outputs (threshold 967,296), so `Draws` can exceed the number of decisions - count decisions by calls.

**Stages (`prob-model-v1`, ROADMAP 4.1 order).** Each stage is a distribution family over its own outcome set, conditioned on an outcome of an earlier stage; stages are never mixed and a path probability is the product of its stage probabilities.

| Stage id | Condition key | Outcomes |
|---|---|---|
| `target_zone` | intent `balanced`/`attack`/`careful` | 17 zones (canonical order) |
| `actual_location` | target zone | 17 zones |
| `batter_decision` | actual zone | `hit_by_pitch`, `take`, `swing` |
| `called_pitch` | `{automated\|non_automated}:{zone}` | `ball`, `strike` |
| `swing_result` | actual zone | `miss`, `foul`, `in_play` |
| `batted_ball` | `heart`/`edge`/`outside` | `ground_{weak,medium,hard}`, `line_*`, `fly_*`, `popup` |
| `defense_result` | batted-ball outcome | `out`, `single`, `double`, `triple`, `home_run`, `reached_on_error` |
| `advancement` | 4 runner situations | `advance_extra`, `hold`, `out_on_bases` |

**Computation (`StageProbability.Compute`).** (1) the baseline row; (2) the supplied modifiers' factors, range-checked (a slot twice is `Modifier.duplicate_input`; a reserved slot accepts only its neutral input); (3) per outcome, the factors of the slots that target it are multiplied in the model's slot order (half-up) and clipped to the combined cap `[800,000, 1,250,000]`; (4) weights = baseline x combined factor, normalized (baseline-positive outcomes must stay positive); (5) with a `TendencyContext` only: favourable mass guarded into `[cap+minMass, D-cap-minMass]`, then shifted by `value*cap/100` ppm (`swing_result` cap 60,000 on `foul`+`in_play`; `batted_ball` cap 40,000 on the three `*_hard` outcomes), both sides keeping exact totals, inner zones only; (6) re-validation. Neutral input = exact baseline. Monotone direction per slot is fixed by the model (higher `pitcher.stuff` raises `miss`; higher `batter.contact` lowers it; higher `batter.power` raises hard contact; ...). Slots: `pitcher.control`, `batter.eye` (outer zones only), `strategy.approach`, `situation.count_pressure`, `pitcher.stuff`, `batter.contact`, `matchup.handedness`, `pitcher.velocity`, `batter.power`, `defense.fielding`, `runner.speed`, `defense.arm`; **reserved, neutral input only, no numeric authority yet:** `catcher.game_calling`, `battery.chemistry`, `catcher.framing`, `park.home_runs`, `park.hits`, `park.extra_base_hits`.

**Tendency neutrality, exactly.** For any vector accepted against `pzl-ref-uniform-v1`, the sum over the nine inner zones of the favourable mass with the vector equals the sum without it (exact integers, also under extreme ability inputs). The league-average output under the real, non-uniform pitch mix is **not** claimed to be unchanged; a test documents the gap, bounded by the cap.

**Tuning data and identity.** `content/tuning/virtual_tuning_v1.json` (`format_version` 1): model `prob-model-v1`, tuning `tuning-virtual-proto-v1`, status `virtual_prototype` (the numbers are design values for a fictional league, not statistics of any real league), zone model `pzl-zones-v1`, reference `pzl-ref-uniform-v1`, 105 baseline rows, modifier tuning, combined and tendency caps, 4 declared validation scenarios (fixed seeds, 200,000 samples, tolerance `binomial-4.5-sigma-ceiling-v1`) and 8 declared aggregate targets (`declared_for_3f_not_evaluated`). Identity `ContentHash` = SHA-256 of a canonical text from the validated values (`5b70dca79ec3a3c815f741b57c4c4490e93c5bcdca19900ad452732c66d73ea5`, also computed by the independent Python script `tests/Fixtures/generator/gen_virtual_tuning_v1.py --hash`). Unknown model/tuning/zone/reference versions and every out-of-range or inconsistent value are rejected with stable `Tuning.*` codes.

**Trace and hidden data.** `ProbabilityTrace` records versions and hash, zone model, reference id, policy, baseline, each modifier (input, factor, applied), the combined factor and cap clipping per outcome, the distribution after modifiers, the guard and tendency shifts and the final distribution. It is resolver-internal: `TendencyContext`, `ProbabilityTrace` and `StageResult` may appear in no Application or Infrastructure member signature (test), and `ToString()` prints no tendency value. No display API exists.

**Compatibility.** `GameRules` v1 and v2 hashes, the RNG algorithm / key derivation / snapshot format / stream names and the golden vectors are unchanged (pinned by tests); `ef1`/`ef2` and the reducer were not touched.

**Handoff.** 3-C: realise `target_zone` (deterministic policy or one sampled decision, with its stream chosen and ADR-004 item 7 amended if it is `game/location`), draw the actual location with one `NextBounded(1,000,000)` on `game/location@1` before the outcome draws, switch-hitter side rule, `ef2`. 3-D: tendency context for every inner-zone pitch, park caps (C46 `Hits` mapped to singles only here - owner confirmation needed). 3-E: Framing cap on `Borderline` pitches in `non_automated` mode. 3-F: statistical calibration against the declared targets (a changed number is a new tuning version). Not done in 3-B2: any sampled calibration, 100,000-PA or 100-game run.

## 20. Phase 3-C1: pitch location on events, `ef2` and event JSON format 2 (2026-10-11; decisions in `docs/decisions/ADR-007.md`)

3-C1 is the data and identity half of the pitch resolver flow. It adds no resolver: **no pitch is resolved, no random number is drawn, and no rule profile, RNG stream, `GameEventIds` value, `ef1` byte or reducer rule changed.** Status `검토 대기`; 3-C2 and 3-C3 are not started.

**Payload.** `PitchSpec` and `InPlayEvent` end with an optional `PitchLocation? Location = null` (existing parameters and defaults untouched). `PitchLocation` is the 3-B1 type: model version, zone, the batter side actually applied, boundary class, validated by its factory; the pitcher's `TargetZone` is a different type. Phase 2 events carry none (legacy; never inferred). An `InPlayEvent` is its own single pitch: its location describes that pitch and no `PitchEvent` precedes it. The reducer counts exactly as before (test: same stats and pitch totals with and without locations).

**Identity.** Section 3a. `GameEventLocation.Of(event)` (Domain) is the one accessor used by the fingerprint and by the JSON writers.

**Event JSON.**

| | Legacy (format 1) | Format 2 |
|---|---|---|
| Shape | bare array `[ {id, type, ...} ]`; scripted games `format_version: 1` | `{"format_version": 2, "events": [ ... ]}` |
| Read | `ScriptedGameJson.ReadEvents`; a `location` member is an unknown member (error); a version-2 envelope is `Fixture.version_unsupported` | `ReadEventsV2`; a bare array is `Fixture.version_unsupported`; other `format_version` values are `Fixture.version_unsupported` |
| Location | never created | required on `pitch`, `take`, `in_play` (`Fixture.location_missing` if absent or `null`); refused on other events (`Fixture.location_unexpected`) |
| Location content | - | `{zone_model, zone, side, boundary}` rebuilt through `PitchLocation.Create`: unsupported model `ZoneModelVersion.unsupported`, unknown zone `PitchZoneId.unknown`, bad enum text `Fixture.enum_invalid`, boundary that is not the zone's class `PitchLocation.boundary_mismatch`; nothing is corrected |
| Write | `WriteEvents`; refuses a located event (`Fixture.location_requires_v2`) | `WriteEventsV2`; refuses a pitch-bearing event without a location (`Fixture.location_missing`) |

Receipts keep their own JSON (`EventReceiptJson`), which already stores `fingerprint_version` per receipt; `ef1` and `ef2` receipts mix freely.

**Compatibility evidence.** `Ef1CorpusPinTests` holds two literals computed on the unchanged 3-B2 commit `ddbc999`: the SHA-256 over every receipt (id, kind, version, fingerprint) of the 30,974 events in the 100 Phase 2 games, and the SHA-256 over the bytes `WriteEvents` produces for those games. `EventIdentityTests` (literal `ef1` text and hash) and `ReplayContractTests` are unchanged and pass.

**Handoff.** 3-C2: `target_zone` realisation (recommended own stream `game/target@1`, ADR-004 amendment), the `game/location@1` draw before the outcome, explicit-`Take` hit-by-pitch policy, switch-hitter side rule, checking that an event's side is possible for the batter. 3-C3: event-id sequence, reducer result, receipts and RNG cursors as one success unit; commit before publication; Phase 4 SQLite transaction and the expected RNG hash.

## 21. Phase 3-C2A: the single-pitch probability branch (2026-10-11; decisions in `docs/decisions/ADR-007.md`, stream in `docs/decisions/ADR-004.md` item 20)

Status: approved 2026-10-11 for the 3-C2A work package only (GPT Chat `PASS WITH NOTES`, Codex final focused re-review `PASS WITH NOTES`; the earlier `검토 대기` wording is superseded). This is the first part of the pitch resolver flow: it connects the 3-B2 probability model to the real seeded streams for ONE pitch and returns a **draft**. It creates no `GameEvent`, calls no `GameReducer`, changes no `GameState`, `ef1`/`ef2`, event JSON, rule profile, tuning number or RNG algorithm.

**API** (`StarPlayer.Simulation.Baseball`, resolver-internal): `PitchBranchResolver.Resolve(PitchBranchRequest, CancellationToken)` returns `Result<PitchBranchDraft>`.

| Input (`PitchBranchRequest`) | Meaning |
|---|---|
| `State` (`GameState`) | read only: supplies the rules (called-pitch mode, zone model), the game status and the plate-appearance stage |
| `Tuning` (`TuningProfile`) | the virtual tuning whose baseline rows are the stage distributions (neutral inputs, no tendency, no modifier) |
| `Intent` (`PitchIntent`) | `Balanced` / `Attack` / `Careful`: the `target_zone` condition, an explicit input |
| `ExplicitTake` | the one-pitch Take action (`Start` / `Decision`) instead of a normal pitch (`InSequence`) |
| `BatterDeclaredSide`, `PitcherThrows` | nullable on purpose: missing is an error. A switch hitter stands opposite the pitcher's throwing hand |
| `Rng` (`GameRandomSnapshot`) | the caller's current position; the resolver continues from it on a private copy |

**Draw order and streams** (each decision exactly one `NextBounded(1,000,000)`; unreached stages draw nothing; certain outcomes are still drawn):

1. `target_zone` on `game/target@1`, condition = intent.
2. `actual_location` on `game/location@1`, condition = target zone; the result becomes a `PitchLocation` through `PitchLocation.FromZone` with the applied side.
3. `batter_decision` on `game/pitch@1`, condition = actual zone (`hit_by_pitch` / `take` / `swing`).
4. `hit_by_pitch` ends the branch. `take` (or any non-HBP decision of an explicit Take) draws `called_pitch` on `game/pitch@1` with condition `automated:<zone>` or `non_automated:<zone>` from the declared mode. `swing` draws `swing_result` on `game/contact@1`.
5. `in_play` draws `batted_ball` on `game/contact@1` (condition `heart` / `edge` / `outside`), then `defense_result` on `game/field@1` (condition = the batted-ball type).
6. `advancement` (`game/runner@1`) is **not** drawn (3-C2B).

Calls per pitch: hit by pitch 3; called ball/strike 4; swinging strike / foul 4; in play 6 (an explicit Take: 3 or 4). Raw draws can exceed calls (rejection sampling, about 0.0225% of raw outputs); the draft reports both, per step and per stream. `PitchBranchKind`: `HitByPitch`, `CalledBall`, `CalledStrike`, `SwingingStrike`, `Foul`, `InPlay` (the future mapping to `PitchSpec` / `InPlayEvent` is 3-C2B).

**Same game required.** `GameState.GameId` must equal the snapshot's `ContextKey` exactly (ordinal); otherwise `PitchBranch.game_context_mismatch` is returned after the snapshot restore and before any draw.

**Errors before the first draw:** `PitchBranch.request_missing`, `state_missing`, `tuning_missing`, `game_not_in_progress`, `take_not_legal`, `pitch_not_legal`, `called_pitch_mode_undeclared` (a `gamerules-v1` profile is never read as a mode), `zone_model_mismatch` / `zone_model_unsupported`, `probability_model_unsupported`, `tuning_version_unsupported`, `tuning_hash_invalid`, `intent_invalid`, `batter_side_missing` / `batter_side_invalid`, `pitcher_hand_missing` / `pitcher_hand_invalid`; and the `Rng.*` restore codes.

**Draft** (`PitchBranchDraft`, `PitchBranchStep`, `PitchBranchStreamUse`): contract `pitch-branch-v1`, intent, explicit-Take flag, `TargetZone`, `PitchLocation`, kind, the five outcome texts (only reached ones), the ordered steps (stage, condition, outcome, stream, exact roll, raw draws, `ProbabilityTrace`), total `NextBounded` calls, per-stream calls and raw draws (ordinal order), `NextRng`, probability-model version, tuning version and hash, rules profile id / version / hash. It is resolver-internal (not in any Application / Infrastructure signature; the traces and rolls are audit data and never reach a UI, Gemini payload or news text) and is never a `GameEvent`.

**Evidence.** Independent Python oracle `tests/Fixtures/generator/generate_pitch_branch_golden.py` (own PCG32 and `kd1`; reads the baseline rows directly) writes `tests/Fixtures/phase3c2a_pitch_branch_golden.json`: `game/target@1` key-derivation / output vectors, independence vectors of the six earlier streams, a rejection vector, and 28 scenarios / 55 pitches. Tests: `TargetStreamTests` (8), `PitchBranchResolverTests` (73) in `tests/StarPlayer.Infrastructure.Tests`; the Phase 3-A golden file and all its tests are unchanged.

**Handoff (3-C2B).** Legal `GameEvent`s from a draft (defensive credits, force / tag, every runner's movement, outs order, third out, walk-off, error attribution), the `advancement` draw on `game/runner@1` against the real runner situation, event ids and `ef2` receipts, mandatory location for new data, and wiring the batter side / pitcher hand from the current lineup. 3-C3: joint atomicity of state, RNG, ids and receipts and automatic plate-appearance progression.

## 22. Phase 3-C2B-1: prepared pitch and non-in-play event candidate (2026-10-11; decisions in `docs/decisions/ADR-007.md`)

Status: **approved 2026-10-11 for 3-C2B-1 only** (GPT Chat `PASS WITH NOTES`, Codex `PASS WITH NOTES`; the earlier `검토 대기` wording is superseded; 3-C2B-2/3, 3-C3 and Phase 4 are NOT approved and not started; the Phase 3 Gate is INCOMPLETE). It connects a 3-C2A draft to the real game state and builds a legal, reducer-checked but UNCONFIRMED event candidate for the five non-in-play branches. It changes no `GameState`, RNG, id cursor, `ef1`/`ef2`, event JSON, tuning number, rule profile or reducer rule.

**API** (`StarPlayer.Simulation.Baseball`, resolver-internal): `PitchPreparation.Prepare(state, rng, tuning, intent, explicitTake, batterSheet, pitcherSheet, ct) -> Result<PreparedPitch>` and `PitchPreparation.BuildNonInPlayCandidate(prepared, state, rng, tuning, batterSheet, pitcherSheet, lastIssuedEventSequence, ct) -> Result<NonInPlayPitchCandidate>`.

- `Prepare`: the sheets must be the state's current batter (`Batting.CurrentBatterSlot.Player`) and pitcher (`Fielding.Pitcher`) by id; declared side = `Player.Bats`, throwing hand = `Player.Throws`; the real `PitchBranchResolver.Resolve` is called. The result (`PreparedPitch`: state reference, game id, start RNG `StateHash`, ids, handedness, draft, rules/tuning/model identity) has no public constructor or setter and is never saved. Codes: `PitchPrepare.state_missing | rng_missing | tuning_missing | batter_sheet_missing | pitcher_sheet_missing | batter_mismatch | pitcher_mismatch`; resolver codes (`PitchBranch.*`, `Rng.*`) pass through.
- `BuildNonInPlayCandidate`: refuses a stale preparation with `PitchCandidate.draft_contract_unsupported | game_mismatch | rng_context_mismatch | rng_state_changed | batter_changed | pitcher_changed | batter_side_changed | pitcher_hand_changed | rules_changed | tuning_changed | probability_model_changed | state_stale` (the same-object check comes last). Then `in_play_not_supported_yet` (in play is an explicit limit), `take_cannot_swing`, the private-counter id (`Id.*` codes at the sequence limits, `event_id_in_use`), the reducer's own error, `event_duplicate`, `receipt_invalid`, `pitch_count_invalid`.
- Mapping: `HitByPitch` -> `PitchOutcome.HitByPitch`, `CalledBall` -> `Ball`, `CalledStrike`, `SwingingStrike`, `Foul`; `PitchEvent`, or `TakeEvent` when the Take flag is set (Take with a swing/foul draft is refused). `PitchSpec.Location` = the draft location; Mishap/DroppedThirdStrike None, no Moves, `StrikeoutCredits` null. The receipt is `ef2`.
- RNG: no new draw; `NextRng` equals the draft's next position. The candidate carries the reducer result as an unconfirmed `UnconfirmedStep`; adopting state, RNG and id together is 3-C3.

Handoff: in-play events, `advancement` (`game/runner@1`), runner movement and fielding credits (3-C2B-2/3, design not fixed); joint atomicity and automatic plate-appearance progression (3-C3); persistence and expected RNG hash (Phase 4).

## 23. Phase 3-C2B-2-P: in-play generation policy and template feasibility (2026-10-12; decisions in `docs/decisions/ADR-007.md` "Decisions (3-C2B-2 prototype policy)")

Status: **policy Owner Accepted (D1–D5, owner instruction `TASK.md`); detailed templates verified by test-only tooling, GPT Chat `PASS WITH NOTES` (feasibility verification only; Codex review `미실시`; not an implementation approval); 3-C2B-2 is NOT implemented.** *(Implementation status superseded by section 24: 3-C2B-2 was implemented on 2026-10-12 and awaits review; this section's text is unchanged.)* Nothing in `src/` changed. This section is the contract the 3-C2B-2 implementation must meet if the owner later approves it; it is not a description of working code. The evidence is `docs/reports/PHASE_3C2B2_POLICY_REPORT.md`, `PHASE_3C2B2_TEMPLATE_MATRIX.md` and the raw results `PHASE_3C2B2_FEASIBILITY_RAW.json`.

**Versions.** Generation policy `inplay-mapping-v1`; official record of a game-ending hit `walkoff-credit-v1`; template catalogue `inplay-template-v1`. Rule basis: the confirmed 2026 OBR as a *temporary* prototype basis (9.06(a)(f)(g), 7.01(e)(3) and its exception, the Infield Fly definition); the 2026 KBO clauses are **not** certified (D4).

**1. Inputs and the 45 combinations.** The in-play branch of a 3-C2A draft supplies a batted ball (`ground|line|fly` x `weak|medium|hard`, `popup`: 10 values) and a defence result (`out`, `single`, `double`, `triple`, `home_run`, `reached_on_error`: 6 values). In `tuning-virtual-proto-v1` 45 of the 60 combinations have a positive probability and 15 are exactly 0 ppm (ground weak: double, triple, home run; ground medium and hard: triple, home run; line weak: triple, home run; line medium: home run; fly weak: triple, home run; popup: double, triple, home run). Every positive combination maps to one of **19 templates** `ipt1.<class>.<result>` (class `ground|line|fly|pop`; intensity selects only the probability row and stays in the audit). A tuning change that makes a zero combination positive needs a template and a re-run of the matrix. The defence-result distribution never yields a fielder's choice.

**2. Templates (fixed fielders; prototype simplification).**

| Class | Out | Reached on error | Hits |
|---|---|---|---|
| ground | SS fields, throws to 1B: assist SS, **one** putout 1B | E6 (SS) | single (to LF), double (left-field corner) |
| line | SS catches: putout SS | E6 (SS) | single (CF), double (RF gap), triple (RF corner), out-of-the-park home run |
| fly | CF catches: putout CF | E8 (CF) | single (CF), double (CF gap), triple (RF), out-of-the-park home run |
| pop | 2B catches: putout 2B | E8 (CF): a ball beyond the infielders' ordinary effort that the CF could ordinarily catch | single: drops in shallow CF beyond the infielders' ordinary effort |

Rules: an out template has exactly one putout (the batter's); an assist exists only where the template contains a throw (only the ground out); a hit has no putout, assist or error; a reached-on-error template has one error at one position and is never reclassified as a fielder's choice or a compound error; the positions must exist in the current defence (checked on every run against `GameState.DefenderAt`).

**3. Base runner movement (every existing runner has exactly one `RunnerMove`; holds are explicit).** From the lead runner back; `n>n` holds; 4 = scored.

| Runners | Ground out, 0-1 outs | Any other out, or any out with 2 outs | Single | Double | Triple, home run | Reached on error |
|---|---|---|---|---|---|---|
| none | - | - | - | - | - | - |
| 1B | 1>2 | 1>1 | 1>2 | 1>3 | 1>4 | 1>2 |
| 2B | 2>2 | 2>2 | 2>3 | 2>4 | 2>4 | 2>2 |
| 1B,2B | 2>3,1>2 | 2>2,1>1 | 2>3,1>2 | 2>4,1>3 | 2>4,1>4 | 2>3,1>2 |
| 3B | 3>3 | 3>3 | 3>4 | 3>4 | 3>4 | 3>3 |
| 1B,3B | 3>3,1>2 | 3>3,1>1 | 3>4,1>2 | 3>4,1>3 | 3>4,1>4 | 3>3,1>2 |
| 2B,3B | 3>3,2>2 | 3>3,2>2 | 3>4,2>3 | 3>4,2>4 | 3>4,2>4 | 3>3,2>2 |
| loaded | 3>4,2>3,1>2 | 3>3,2>2,1>1 | 3>4,2>3,1>2 | 3>4,2>4,1>3 | 3>4,2>4,1>4 | 3>4,2>3,1>2 |

Policies: `RP-OUT-GROUND-FORCED` (a runner is forced when every base behind him is occupied; the batter's run to first forces him along, a non-forced runner holds; with two outs the batter's out is the third out and everyone holds), `RP-OUT-HOLD` (line, fly, pop out: no tag-up, no double-off), `RP-HIT-PLUS-1/2/3` (each runner advances the bases of the hit, capped at home), `RP-HR-ALL-SCORE`, `RP-ROE-FORCED-PUSH` (forced runners move one base). Causes: hits, outs and holds `MoveCause.Play`; a runner pushed by a reaching batter on a reached-on-error `MoveCause.Forced`. The batter-runner of a hit or an error gets **no** `RunnerMove` (his destination is the result; the reducer rejects a batter move that is not a post-hit out); a batter out is the event's own out with its credits. No `OutKind`, putout or assist is attached to any move: a base plan never puts a runner out. **No `Sequence` is ever needed:** a base plan never contains a run together with the third out (the third-out play holds every runner).

**4. Official record.** Ordinary situations (any inning that is not the last half of the final or an extra inning): official result = drawn result. Last half of the final or an extra inning, with `need = away runs - home runs + 1` runs required to take the lead (`inplay-mapping-v1` / `walkoff-credit-v1`):
- Drawn `double` or `triple` with `k` bases: let the runners that score under the drawn template (base `b` scores iff `b + k >= 4`) be listed lead runner first. If there are at least `need` of them, the *winning-run scorer* is the `need`-th; the official bases are `min(k, 4 - his starting base)` (OBR 9.06(f): only as many bases as the winning-run scorer advanced). When this is smaller than `k` the official result is the template of the smaller hit and **the facts of the play are those of that template** (the batter stops where the record says, D3); the run count then equals `need` exactly. The drawn result stays in the audit.
- Drawn `single`, `out`, `reached_on_error`: unchanged. Drawn `home_run`: out-of-the-park, batter and every runner score (D2; 9.06(g)); never converted.
- A non-home-run walk-off scores exactly `need` runs; the reducer refuses more (`Game.run_after_winning_run`) and the policy never produces more.
- Trailing runners and the batter-runner are recorded at the destinations of the official template at the moment the game ends (they count as left on base under the profile rule `CountWalkOffRunnersAsLeftOnBase`).

Worked examples: tied, runner on 3B, drawn double: need 1, scorers [3B], credit `min(2, 1) = 1` -> official **single**, one run, batter on 1B. Tied, runner on 2B, drawn triple: credit `min(3, 2) = 2` -> official **double** (the 9.06(f) comment: the batter-runner reaches second in the natural course of play). Tied, runner on 1B, drawn triple: credit 3 -> triple unchanged. Two behind, bases loaded, triple: need 3, winner from 1B, credit 3 -> triple, 3 runs. Tied, bases loaded, 2 outs, reached on error: the forced run from 3B scores, the result stays `ReachedOnError`, no RBI.

**5. When advancement is required (decision for 3-C2B-3).** A base plan is `AdvancementRequired` exactly when the situation is one of the four declared `advancement` contexts of `prob-model-v1` and the base plan does **not** already end the game and is not a third-out play: `single_runner_on_first` / `single_runner_on_second` (official single with a runner on 1B or 2B), `double_runner_on_first` (official double with a runner on 1B), `fly_out_runner_on_third` (fly-ball out with a runner on 3B and fewer than two outs). If the base plan ends the game, later advancement is moot and not required. The base plan of a required case is complete and legal (the reducer accepts it) but **must not** be given to 3-C3: whether the runner takes the extra base or is thrown out is the `advancement` draw. Acceptance by the reducer never turns `PENDING_ADVANCEMENT` into `LEGAL_BASE`. Cases with more than one runner are composite: the model declares no context, order or interaction for them (U-1).

**6. Popup and the Infield Fly rule.** The reducer does not model the Infield Fly rule; the templates keep the two situations apart by premise. *Pop out* (2B catches with ordinary effort): with first and second occupied (or bases loaded) and fewer than two outs this is an Infield Fly (OBR definition); the batter is out either way and the caught ball gives the same record; runners hold in the base plan (they may advance at their own risk, which is 3-C2B-3). *Pop single* and *pop reached-on-error*: the ball is beyond any infielder's ordinary effort (shallow center field), so **no** Infield Fly by premise, whatever the runners; the error of the reached-on-error template is the outfielder's (E8) and the ball is one the outfielder could ordinarily catch. A pop-up error by an infielder in an Infield Fly situation is **not** a generated play (the batter would be out).

**7. Type boundary for 3-C2B-2/3 (design only; no production code is written).**
- `BaseInPlayPlan` (Simulation, resolver-internal): the `PreparedPitch` and its in-play draft, the batted-ball class and intensity, the drawn defence result, the drawn and the official template, the batter's final base, the ordered runner moves, whether the half-inning or the game ends, the walk-off effect, the advancement-required flag with its context keys and a composite marker, the policy versions and the audit evidence. No RNG is drawn to build it.
- `PendingAdvancement`: a `BaseInPlayPlan` whose advancement is required. It is **not** a candidate: it has no event id, produces no `GameEvent` or receipt, cannot be adopted by 3-C3, and its RNG position is H1, the end of the 3-C2A pitch draw (`Draft.NextRng`; section 24 "RNG positions"), not the position before that draw (H0). The next `game/runner@1` draw must continue from H1.
- `InPlayPitchCandidate`: every runner decision made, official record made, the event (an `InPlayEvent` with the draft's location, so `ef2`) accepted by the unchanged `GameReducer.Apply` on the original state, the receipt, the unconfirmed `GameStep` and `NextRng`. Adopting it with the state, ids and RNG is 3-C3.

**8. Audit and replay contract (design; Simulation-internal; not in `ef1`/`ef2`, event JSON or any hash).**

| Field (snake_case) | Content |
|---|---|
| `inplay_policy_version`, `walkoff_credit_version`, `template_catalog_version` | `inplay-mapping-v1`, `walkoff-credit-v1` (or `not_applicable`), `inplay-template-v1` |
| `game_id`, `candidate_event_id` | the game and the final candidate event id |
| `original_batted_ball`, `original_defense_result` | e.g. `fly_hard`, `triple` |
| `official_result` | the final `InPlayResult` |
| `drawn_template_id`, `official_template_id` | equal unless `walkoff-credit-v1` changed the credit |
| `pre_play_state` | outs, runners with their runner keys, score, inning, half |
| `batter_final_base` | equals the credited base (D3) |
| `runner_moves` | runner key, from, to, cause, in scoring order |
| `winning_run` | the winning-run scorer (kind `ExistingRunner` or `BatterRunner`, runner key, player, starting base; 0 for the batter-runner) or none (null: the play does not end the game) |
| `record_change_reason` | `none` or `walkoff_credit_reduced` with the drawn bases and the winner's advance |
| `rules_*`, `tuning_*`, `probability_model_version` | identities and hashes in force |
| `start_rng_state_hash`, `final_rng_state_hash` | H0 = the position given to `PitchPreparation.Prepare` (before the 3-C2A pitch draw) and H1 = `Draft.NextRng` (after it): they cover the whole pitch and differ in general. The base-plan step itself consumes nothing (H1 -> H1); a 3-C2B-3 advancement draw continues from H1 and ends at H2 |
| `advancement` | `not_required`, `pending(context keys, composite)` or `resolved(...)` (3-C2B-3) |

3-C3 settles this record, the `GameEvent`, the receipt, the state, the RNG and the ids together; Phase 4 stores them durably. Neither is part of this package.

**9. Evidence of this package (test-only; details in the report).** 45 positive and 15 zero combinations read from the tuning JSON; 19 templates cross-checked against an independent Python oracle; the 1,080 ordinary cases (45 x 8 x 3) and the 4,320 walk-off cases (45 x 8 x 3 x 4) all executed through the real `GameReducer.Apply`, 0 refused, 0 defects, results equal to the oracle in every field; ten hand-derived cases; counterexamples (the reducer accepting a wrong walk-off credit, refusing excess runs, a phantom assist caught only by the template check); 12 injected defects all detected. `PENDING_ADVANCEMENT`: 300 of 1,080 ordinary and 1,014 of 4,320 walk-off cases. `NEEDS_WALKOFF_POLICY` (legal only through the credit rule): 192 walk-off cases. These are policy-feasibility facts, not statistics.

**10. Handoff.** 3-C2B-2: implement the plan and candidate generation to this contract, with its own tests and the matrix as acceptance data. 3-C2B-3: the `advancement` draw, composite contexts (U-1), recomputing the record and the third-out order (U-2). 3-C3: joint atomicity, automatic plate-appearance progression, the last issued event sequence (U-7). 3-F: calibration of fielder selection and extra-base behaviour (U-3). KBO certification of any rule used remains open (D4).

## 24. Phase 3-C2B-2: base in-play plan, pending advancement and in-play event candidate (2026-10-12; decisions in `docs/decisions/ADR-007.md` "Decisions (3-C2B-2 implementation)")

Status: **implemented, self-tested and finally approved 2026-10-12 for 3-C2B-2 only (Codex Medium focused re-review `PASS`, GPT Chat final `PASS`; the earlier `검토 대기` wording is superseded; the earlier Codex `CHANGES REQUIRED` is kept as fix history in the report)**. It implements section 23 without changing it. 3-C2B-3, 3-C3 and Phase 4 are not started; the Phase 3 Gate is INCOMPLETE. No reducer rule, probability number, RNG, `ef1`/`ef2`, event JSON or tuning value changed.

**API** (`StarPlayer.Simulation.Baseball`, resolver-internal): `InPlayPitchPlanning.PlanBase(prepared, state, rng, tuning, batter, pitcher, ct) -> Result<BaseInPlayPlan>` and `InPlayPitchPlanning.Build(…, int lastIssuedEventSequence, ct) -> Result<InPlayPlanOutcome>`, where the outcome is exactly one of `InPlayPitchCandidate` (event, id, unconfirmed reducer step, `NextRng`, audit) or `PendingAdvancement` (plan, context keys, composite marker, `RngAfterPitchDraw`; no event, id, receipt or step). Both take a real `PreparedPitch` from `PitchPreparation.Prepare`; there is no draft-injection API. The stale-preparation checks are `PitchPreparation.CheckPrepared`, the unchanged 3-C2B-1 contract.

**Flow of `Build`:** shared stale check → in-play branch required (`InPlayPlan.not_in_play`) → approved (batted ball, defence result) combination → template → official record (`walkoff-credit-v1` in a walk-off context) → one base move per existing runner → if a declared `advancement` context applies and the play ends neither the game nor the half-inning: `PendingAdvancement`; else a working id on a private `GameEventIds` (the cursor is read only; `InPlayCandidate.event_id_in_use`, `Id.*`) → `InPlayEvent` with the draft's location (`ef2`) → unchanged `GameReducer.Apply` on the original state → `Verify` → candidate. The draw is never repeated and no random number is consumed after the 3-C2A draw.

**Error codes:** `InPlayPlan.not_in_play | batted_ball_unknown | defense_result_unknown | template_unsupported | combination_not_approved | state_missing | state_invalid`; `InPlayCandidate.event_id_in_use | event_duplicate | receipt_invalid | pitch_count_invalid | official_record_invalid`; the shared `PitchCandidate.*` / `PitchPrepare.*` / `Rng.*`, `Id.*` and the reducer's `Game.*` pass through.

**Pending vs. candidate:** a plan is pending exactly when one of the four declared contexts applies to the official result (`single_runner_on_first`, `single_runner_on_second`, `double_runner_on_first`, `fly_out_runner_on_third` with fewer than two outs) and the play is not game-ending or a third-out play. Composite situations keep all keys. Measured over the 5,400 recorded cases: ordinary 780 complete / 300 pending (210 composite); walk-off 3,114 complete / 1,014 pending (654 composite) / 192 legal only through the credit rule.

**Audit:** `InPlayAudit` (D5 fields of section 23.8), Simulation-internal, outside `ef1`/`ef2` and event JSON, not persisted.

**Winning scorer.** `InPlayWinningRunner(Kind, RunnerKey, Player, StartingBase)` is the need-th scorer in scoring order: existing runners lead-first, then the batter-runner of an out-of-the-park home run (`Kind = BatterRunner`, `RunnerKey = GameState.NextRunnerKey`, `Player` = the current batter, `StartingBase = 0`; the same key and player `GameReducer` records in `ScoredRun`, never a new id or an existing runner's key). Three cases are distinct: null (the play does not end the game), `ExistingRunner`, `BatterRunner`. Example: the home team trails by 3 with the bases loaded and hits an out-of-the-park home run: four runs score, the batter-runner scores the fourth and is the winning scorer.

**RNG positions (H0 / H1 / H2).** H0 = the snapshot passed to `PitchPreparation.Prepare`, before the 3-C2A pitch draw (kept as `PreparedPitch.StartRngStateHash`). H1 = `Draft.NextRng`, after the 3-C2A draw. H2 = the position after a future 3-C2B-3 `advancement` draw. (1) The C2B-2 plan starts from H1. (2) C2B-2 itself consumes no random number, so the position before and after the C2B-2 planning is the same H1: `InPlayPitchCandidate.NextRng` and `PendingAdvancement.RngAfterPitchDraw` both equal H1. (3) 3-C2B-3 continues `game/runner@1` from H1. (4) H0 and H1 are in general different; a `PendingAdvancement` does not keep the pitch's start position. (5) The audit fields cover different ranges: `start_rng_state_hash` = H0 and `final_rng_state_hash` = H1 describe the whole pitch (H0 -> H1), whereas the C2B-2 sub-step alone is H1 -> H1; do not read H0 = H1 into them. (6) Only 3-C3 fixes the final RNG position together with the authoritative game state.

**Handoff:** 3-C2B-3 draws `advancement` from `RngAfterPitchDraw`, designs composite contexts (F-1), and recomputes the record and third-out order (U-2); 3-C3 owns joint atomicity, the last issued sequence re-check, plate-appearance progression and audit persistence; Phase 4 stores durably.

## 25. Phase 3-C2B-3-P: approved `advancement` policy contract (2026-10-12; decisions in `docs/decisions/ADR-007.md` "Decisions (3-C2B-3-P policy approval)")

Status: **policies P-01 to P-10 owner-approved 2026-10-12; design only. 3-C2B-3 production is NOT STARTED / NOT AUTHORIZED, 3-C3 is NOT STARTED, the Phase 3 Gate is INCOMPLETE.** *(History: this was the status when the policies were approved. 3-C2B-3 was implemented afterwards on the owner's separate instruction, see section 26; the policy text below is unchanged.)* Nothing in sections 20 to 24, the reducer, probabilities, RNG, `ef1`/`ef2` or event JSON changes. Evidence: `docs/reports/PHASE_3C2B3_POLICY_VALIDATION_REPORT.md`; wording of each policy: `docs/reports/PHASE_3C2B3_POLICY_DECISION_DRAFT.md`.

**Scope.** The four declared `advancement` contexts of `prob-model-v1` (`single_runner_on_first`, `single_runner_on_second`, `double_runner_on_first`, `fly_out_runner_on_third`) and the composite situations built from them. This resolves U-1 and U-2 of section 23.11 as policy; no code exists.

**Order and composition (P-01, P-02, P-03, P-04).** Lead runner first (third, second, first). Sequential conditional composition: a runner's attempt at an occupied base is impossible for both `Extra` and `Out`; the home plate is never blocked; once the third out or the winning run is fixed, the remaining contexts are `Void` and keep their position. A drawn but impossible outcome is attributed to `Hold` (policy B, `drawn_outcome` and `applied_outcome` both audited); equivalence with re-normalisation is accepted only for the current four contexts and is not generalised. The policy layer blocks base collisions, overtaking, illegal outs and moves after the play is over; the finished event is re-validated by the unchanged `GameReducer`, and reducer acceptance is not a proof of full baseball legality.

**Sequence (P-05, `Prototype Sequence Policy v1`).** Lead-runner scoring is placed before a trailing runner's non-force out with an explicit `Sequence`; third-out and run credit then follow the existing reducer rules. This is a deterministic prototype assumption: not an observed time order, not KBO 2026 certified, replaceable by a time-based running model.

**Event input vs. derived record (P-06).** The `InPlayEvent` is built with a domain `InPlayResult` (`Single`, `Double`, `Triple`, `HomeRun`, `Out`, `FieldersChoice`, `ReachedOnError`) after the final moves and `Sequence` are fixed. There is no `InPlayResult.SacrificeFly`. The reducer derives `BatterResult.SacrificeFly` when `Result = Out`, `Batted = Fly`, the play has exactly one out, fewer than two outs before the play and at least one non-error run (`GameReducer.Plays.cs`). The `ScoredPlay` returned by `Apply` is compared with the expected derived record (batter result, at-bat, RBI, runs, outs, LOB, winning runner); the event is never changed after `Apply`.

**RNG (P-07).** H0 = current authoritative RNG (compared with the pending plan's prepared start); H1 = `PendingAdvancement.RngAfterPitchDraw`; the private copy restored from H1 draws `game/runner@1` once per declared context, including `Void` contexts; no re-draw; the successful candidate's `NextRng` is H2. Logical `NextBounded` calls and raw `Draws` differ (rejection sampling). Adopting H2 into the authoritative RNG is 3-C3.

**Ability (P-08).** The first implementation is neutral: no per-player running or throwing modifier, the approved `advancement` probabilities and base modifier contract unchanged. Neutral does not mean a real ability of 0. No new `PlayerSheet` input, `AdvancementInputs` type or invented ability; ability wiring needs a later separately versioned contract.

**Audit (P-09).** Policy and version; per-context order; original draw (stream, call number, roll, probabilities, `drawn_outcome`); `applied_outcome` with reason (`none | blocked_base | play_over`); final event `Sequence`; H0, H1, H2 hashes; event input and reducer-derived record as separate fields; candidate verification result. Simulation-internal, outside `ef1`/`ef2`, event JSON and any hash; persistence is 3-C3 / Phase 4.

**Candidate generation (P-10).** Conceptual input `Resolve(pending, state, rng(H0), tuning, batter, pitcher, lastIssuedEventSequence, cancellation)` (final signature to be reconciled with `InPlayPitchPlanning.Build` / `PitchPreparation.CheckPrepared` at implementation). The unchanged `CheckPrepared` runs first and keeps its codes and priority; then the EventId boundary (private working id, existing receipts) is checked; the real `GameState`, RNG and EventId cursor are never modified; the result reuses `InPlayPitchCandidate` (`NextRng` = H2, `Audit.Advancement = resolved(...)`). A `PendingAdvancement` stays a sibling type, never a candidate. 3-C3 re-checks the real EventId cursor and state immediately before adoption and owns joint atomicity.

**Not part of this section.** Implementation, per-player ability wiring (3-F), composite-context calibration, KBO certification, persistence of the audit. Implementation requires the owner's separate instruction.

## 26. Phase 3-C2B-3: `advancement` resolver (2026-10-12; decisions in `docs/decisions/ADR-007.md` "Decisions (3-C2B-3 implementation)")

Status: **implemented and self-tested 2026-10-12, review pending (`검토 대기`), NOT approved by GPT Chat or Codex; 3-C3 and Phase 4 are not started; the Phase 3 Gate is INCOMPLETE.** It implements section 25 (policies P-01 to P-10) without changing it. No reducer rule, probability number, tuning value, RNG algorithm or stream, `ef1`/`ef2`, event JSON or earlier golden vector changed.

**API** (`StarPlayer.Simulation.Baseball`, resolver-internal): `AdvancementPlanning.Resolve(PendingAdvancement? pending, GameState? state, GameRandomSnapshot? rng /* H0 */, TuningProfile? tuning, PlayerSheet? batter, PlayerSheet? pitcher, int lastIssuedEventSequence, CancellationToken ct) -> Result<InPlayPitchCandidate>`. The result is the existing candidate type: `Event` (one `InPlayEvent` with the prepared pitch's location, `ef2`), a working `EventId`, the unconfirmed reducer step, `NextRng` = H2 and `Audit` (with `Audit.AdvancementDetail`, `Audit.Advancement == "resolved"`). Nothing is adopted: the caller's `GameState`, RNG (H0), the pending's H1, the id cursor and the receipts are never modified.

**Flow.** (1) `PitchPreparation.CheckPrepared(pending.Plan.Prepared, ...)` unchanged (state object, game, H0, current batter/pitcher and handedness, rules, tuning, probability model, draft contract; the caller's `rng` must be H0). (2) H1 (`pending.RngAfterPitchDraw`) restored with `GameRandom.Restore(H1, H1.StateHash)` into a private copy. (3) The declared contexts in lead-runner-first order and their approved rows (`StageProbability.Compute(tuning, "advancement", key)`, no modifier). (4) A working id from the read-only cursor (`Id.sequence_invalid`, `Id.sequence_exhausted`, `InPlayCandidate.event_id_in_use`). (5) Inside `GameRandom.Atomically`, one `NextBounded(1,000,000)` on `game/runner@1` per context, always, in processing order. (6) Sequential conditional composition. (7) The event, then the unchanged `GameReducer.Apply` on the original state. (8) Verification of the reducer's verdict. (9) The audit and the candidate.

**Contexts.** `single_runner_on_first` (start base 1), `single_runner_on_second` (2), `double_runner_on_first` (1), `fly_out_runner_on_third` (3). The base plan has put the runner on `PlanTo` (his start base for a hold); the attempt is one base further (`TargetTo` = `PlanTo` + 1, 4 = home). The stored `AdvancementKeys` order of the C2B-2 plan (trailing runner first) is kept; the processing order is third, second, first base.

**Composition.** For each context in order: if the third out or the winning run has already happened, the applied result is `Void` (reason `PlayOver`) whatever was drawn; otherwise a drawn `Extra` or `Out` onto an occupied base (home is never blocked) is applied as `Hold` (reason `OccupiedBase`, policy B); otherwise the drawn result is applied. `Void` and `Hold` runners stay on the base the base plan gave them. An `Out` is a tag out at the target base, `Extra` moves to it. The drawn and the applied result are both kept.

**Sequence (P-05).** If any existing runner scores or is put out, every such run and out gets an explicit `Sequence` 1..n in descending start-base order (lead runner first); the batter's caught-ball out is the implicit first event. A play with none carries none. This is the approved prototype assumption, not an observed play order and not a KBO 2026 certification.

**Event and official record (P-06).** The event keeps the base plan's real `InPlayResult` (a caught fly ball is `Out`; there is no `InPlayResult.SacrificeFly`). After `Apply` the result is compared with the rules and is never edited: batter result (`SacrificeFly` exactly when the reducer's own condition holds), counted runs (the unchanged `ThirdOutRule` over the sequence), outs, half-inning end, the game ending exactly with the winning run, per-runner records, the winning scorer (the need-th run in sequence order), credits and errors, RBI, at-bat and left-on-base. A difference is `Advancement.official_record_invalid` and no candidate exists.

**RNG (P-07).** H0 is the caller's authoritative position (proved equal to the prepared start by `CheckPrepared`); H1 is `pending.RngAfterPitchDraw`; H2 is the private copy's snapshot after K whole draws (`K` = number of declared contexts, 1 or 2 today). The raw outputs used per decision are recorded; they can exceed the call count.

**Audit (P-09).** `AdvancementAudit`, `AdvancementDecisionAudit`, `AdvancementMoveAudit` (see ADR-007 item 10). Simulation-internal, outside `ef1`/`ef2`, event JSON and any hash, not persisted.

**Error codes.** `Advancement.context_unknown | context_duplicated | runner_missing | runner_not_on_base | target_invalid | retriever_missing | outcomes_unexpected | drawn_invalid | state_missing | plan_invalid | official_record_invalid`; `InPlayCandidate.event_id_in_use | event_duplicate | receipt_invalid | pitch_count_invalid`; the shared `PitchCandidate.*` / `PitchPrepare.*` / `Rng.*` / `Id.*` and the reducer's `Game.*` pass through.

**Stages.** `start`, `validated`, `restored`, `id_issued`, `drawing` (per decision), `drawn`, `composed`, `event_built`, `before_reducer`, `after_reducer`, `verified`; the token is checked at each stage.

**Limits kept.** Neutral abilities (P-08, not a real ability of 0); only the four declared contexts; no batter-runner extra base, double play, fielder's choice, sacrifice bunt or force out; the candidate is unconfirmed (3-C3 adopts H2, the id, the receipt and the state together and re-checks the real RNG and cursor, including a numbering gap); no durable audit (Phase 4); no calibration (3-F); KBO 2026 certification is open.

## 27. Phase 3-C3: authoritative game and the single Stage/Publish gate (design approved by the owner 2026-10-13; NO implementation; decisions in `docs/decisions/ADR-007.md` "Decisions (3-C3 design approval)")

Status: **D-1 to D-8 and the additional contracts A to F are OWNER APPROVED as a design (owner instruction `TASK.md` "Phase 3-C3 설계 승인 문서 반영"; the approval date 2026-10-13 rests on the project owner's chat approval; the modification time of `TASK.md` is not evidence of the approval). This is a design approval only.** C3-A, C3-B, C3-C and any C3 production code are `NOT STARTED / NOT AUTHORIZED` (each needs the owner's separate instruction); the Phase 3 Gate is INCOMPLETE. **Addendum 2026-10-13: the C3-A detailed contract O-1 to O-9 (identifiers, error codes, verification order, cancellation, allowed events, `Revision`, creation/restoration, visibility, evidence references, `CommitRecord`) is OWNER APPROVED as documentation in 27.16; it is still not an implementation approval.** No source, test, reducer, RNG, `ef1`/`ef2`, event JSON, rule profile, tuning or ROADMAP text changed. Type and error-code names below are conceptual; the owner approved the contracts, not the identifiers (fixed when each part is authorized). "Analysis findings" are from a read-only reading of the code at HEAD `a8cb10c`; nothing was executed for them and any cost statement is an estimate, not a measurement.

**27.1 Purpose and boundary.** 3-C2B-1/2/3 produce UNCONFIRMED candidates and adopt nothing. 3-C3 owns only the safe confirmation of a candidate or proposed event: one gate that turns "the authoritative game plus one proposed official event" into "the next authoritative game plus one immutable commit record", atomically inside the process. It does not choose the next action, create new candidates, run a plate-appearance loop (D-4: the later 3-F harness), persist anything (F: Phase 4), add a repository, a database table or a UI API (D-8), or publish to presentation.

**27.2 Analysis findings the design rests on (read-only, 2026-10-13).**
- The last issued event sequence has no owner: `PitchPreparation.BuildNonInPlayCandidate`, `InPlayPitchPlanning.Build` and `AdvancementPlanning.Resolve` take it as a caller-supplied `int`; `GameEventIds.LastIssued` says "persist it with the game state" but nothing holds it; `GameState` has no such field and `AppliedEvents` stores receipts only.
- `GameReducer.Apply` is the only creator of a receipt (`EventFingerprint.Receipt`, appended to the returned `AppliedEvents`); it is deterministic, uses no random number, clock or I/O, and never mutates its input. Production callers: the three candidate builders (preview on the ORIGINAL state) and `GameDiagnosticsService` (scripted diagnostic games with ids such as `e1`; not canonical, not official).
- `Apply` sets `Status = Final` and `Outcome` inside `Conclude`; `GameState.Final` (the `FinalizedGame` with box score, decisions and catcher/battery exposure) is set only by `GameReducer.Finalize`, which is pure, builds no receipt or id, draws nothing, copies the already-final score and outcome and is idempotent (`WasNew`). Between the two a state with `Status == Final` and `Final == null` exists (`LegalActions` offers only `Finalize`). `GameDiagnosticsService` swallows a failed `Finalize`.
- `GameState` structural equality is not usable: `ImmutableList` members compare by reference, so two equal-content states compare unequal; existing tests compare components (`AppliedEvents`, `Stats`, `Lineup`, ...). No state digest exists.
- `NonInPlayPitchCandidate` keeps neither the `PreparedPitch` nor the base state; `InPlayPitchCandidate` reaches the prepared pitch through `Plan.Prepared`. `PitchPreparation.CheckPrepared` pins the state by `ReferenceEquals`.
- `GameRandom.Restore(snapshot)` proves the structure and that each stream state equals the state reached after exactly `Draws` draws from its derived start; it cannot know which streams should exist (its own documented limit). A hash match is a content summary, not a proof of the path.
- `HiddenTendencyBoundaryTests` pins that no Application/Infrastructure member signature names `PreparedPitch`, the candidates, the draft or the audits.

**27.3 `AuthoritativeGame` (contract A).** An immutable reference type in Simulation. Conceptual fields: `GameState`, `GameRandomSnapshot` (the authoritative position), `LastEventSequence`, `Revision` (a `long`, monotonic: +1 for every successful Publish; a game-ending event's `Apply` and `Finalize` are one Publish and there is no finalization-only Publish, see 27.16 O-5 — the earlier wording "including a Publish whose only change is the finalization" is superseded and kept here as history) and `LastCommit`. `AppliedEvents.Count` is an auxiliary consistency check only, never the proof of freshness or of numbering. The `GameState` object identity is stable while the game is published (candidates and `CheckPrepared` rely on it). Creation/restoration validates the aggregate (canonical GameId, RNG context equal to the GameId, valid snapshot, `LastEventSequence` consistent with the real receipt history).

**27.4 Event identity (D-1, D-2).** `LastEventSequence` is stored explicitly and compared with the real receipt history when an event is confirmed and when a game is created or restored; the canonical GameId and the continuity of the canonical event ids (`{GameId}:e{sequence}`, `GameEventIds`; correction 2026-10-13: the earlier wording "6+ digits" was inaccurate — the sequence is exactly six digits, 1 to 999999, `GameEventIds.MaxSequence`; a seventh digit is non-canonical and refused, see 27.16 O-6) are checked there too. `Count == LastEventSequence` alone never proves continuity. An official event's id must be exactly `LastEventSequence + 1` (parsed with the existing `GameEventIds` rules), must not already be a receipt id, and must not skip or reuse a number. A diagnostic non-canonical id (scripted games) is outside the gate and is refused by it; `GameDiagnosticsService` stays outside C3.

**27.5 One gate for the official events (D-3).** Every official event goes through the same Stage/Publish boundary: a non-in-play pitch event (`PitchEvent` / `TakeEvent`), a base in-play event, an in-play event with a resolved extra-base decision, `ApproachEvent`, `RunnerPlayEvent` and `SubstitutionEvent`. Creating an event and confirming it are separate responsibilities. An event that consumes no random number keeps the authoritative RNG snapshot unchanged. 3-C3 builds no event generator and no plate-appearance driver; `Approach`/`RunnerPlay`/`Substitution` enter as proposed events from whoever creates them. A `PendingAdvancement` is not a confirmable candidate and is refused.

**27.6 Stage (D-5, D-7, contract B).** Input: the current `AuthoritativeGame` and one proposal (an event and, for a pitch candidate, its linked evidence, 27.9). Stage computes, without touching anything shared, in this order (the exact error priority is fixed in the C3 implementation contract; the existing `CheckPrepared` codes and their order are not changed, D-6): (1) the authoritative aggregate and proposal are consistent and the proposal is not already committed; (2) the event id is exactly `LastEventSequence + 1` and canonical (27.4); (3) for a pitch candidate, `CheckPrepared` and the linkage of 27.9 against the CURRENT aggregate; (4) **`GameReducer.Apply` is run again on the current authoritative `GameState`** and its result, not the candidate's preview, is the basis of what is adopted; it must be a success, not a duplicate, with the event's own receipt (and `ef2` for an event that carries a location); (5) the candidate's `UnconfirmedStep` is a preview only and is compared structurally with the re-run: the event receipt and event history, runs, outs, runner placement, ball/strike count, inning and half, game-end state, every `ScoredPlay` and its `Sequence`, the official numbers (RBI, at-bat, left on base and the other reducer-derived records, i.e. the delta stat entries), the `Delta`, the `Facts`, and every other nested game state the event changes (pitcher usage, lineup, bench, substitutions, `NextRunnerKey` and the like). `GameState ==` is not used as proof of structural equality (27.2); a mismatch rejects the Publish; (6) the RNG checks of 27.8; (7) **if the re-run state is final, `GameReducer.Finalize` runs in the same Stage** after (4)-(5) pass; a `Finalize` failure means no Publish; the stage never exposes `Status == Final && Final == null`; `Finalize` issues no id or receipt and draws no number; the `GameCompleted` fact is added once when `WasNew`; (8) the next `AuthoritativeGame` (new state, RNG, `LastEventSequence`, `Revision + 1`) and the `CommitRecord` are built. A failed Stage changes nothing.

**27.7 Publish, CAS and cancellation (contract B).** Publish compares the expected `AuthoritativeGame` reference (the one Stage read) with the current reference using compare-and-swap, and swaps in the next aggregate in one step. Of two or more changes prepared from the same base, exactly one succeeds; the others are rejected as stale with no effect. Cancellation is honoured only before the final CAS; after a successful CAS the committed work is never reported as cancelled. The `CommitRecord` is created before the CAS, stored in the new aggregate and the same instance is returned. `Revision` is distinct from CAS: the CAS is a process-memory identity check; `Revision` is the monotonic value that can later be compared in storage. The single serialized writer remains the primary protection; the CAS is the additional contract.

**27.8 RNG verification (contract C).** For the proposal's `NextRng`, Stage verifies: the snapshot is structurally valid (`GameRandom.Restore`); the master seed equals the authoritative one; the context key equals the authoritative one (and the GameId); every stream present at H0 is still present; no stream's `Draws` goes backwards; the per-stream `Draws` increase equals the recorded usage of the draft (`PitchBranchDraft.StreamUsage` raw draws); for a resolved advancement the extra raw draws on `game/runner@1` equal the advancement record's raw draws; a stream that is new relative to H0 is accounted for by the recorded usage, and an existing one by its increase. An event that consumes no random number must leave the snapshot unchanged. **Matching hashes are never recorded as proof of how the position was produced.** A stream position is a pure function of (master seed, context, stream, draws), so a valid snapshot with the right increments is the one correct position for those draws; what remains unproven is that those draws are the ones that produced this candidate's content. That is the **trust boundary**: candidate and draft constructors are `internal` to Simulation (and visible to two test assemblies only), nothing outside builds a candidate or a prepared pitch, and replaying the draft to compare content is not part of C3. The limit is documented here and in the ADR; it is not described as a proof.

**27.9 Linking a non-in-play candidate to its source (contract D).** To keep existing types unchanged, the non-in-play adapter takes the internal pair `(PreparedPitch, NonInPlayPitchCandidate)`. A wrongly linked pair is never treated as valid. The verifiable matching conditions are: the prepared pitch passes `CheckPrepared` against the CURRENT authoritative state/RNG (same state object, game, H0 hash, batter/pitcher, rules, tuning, model); the candidate's `Kind` equals the draft's `Kind` and `NextBoundedCalls` its `NextBoundedCalls`; the candidate's `NextRng` equals the draft's `NextRng` (hash and structure); the event kind matches `ExplicitTake` (`TakeEvent` vs `PitchEvent`); the `PitchSpec` outcome is the one the draft's `Kind` maps to; the event's location equals the draft's `Location`; the id is the next canonical id; the re-run reducer step satisfies the preview checks; and the RNG usage of 27.8 comes from that prepared pitch's draft. An in-play candidate reuses `Plan.Prepared` (same conditions) and, when an advancement was resolved, its record's H0/H1/H2 and raw draws; the event, its id and `NextRng` (H1 for a base candidate, H2 for a resolved one) must agree with them. Any mismatch is a rejection, not a warning.

**27.10 `CommitRecord` (contract E).** Immutable data holding only what verification and later storage need: the confirmed event (or its stable description), its receipt (id, kind, fingerprint version and fingerprint), the event sequence, the revision before and after, the RNG state hash before and after, and the facts. It names no hidden candidate, prepared pitch, draft or audit type (D-8; `HiddenTendencyBoundaryTests`). The in-memory audits (`InPlayAudit`, `AdvancementAudit`) stay Simulation-internal evidence; what Phase 4 must persist is a separate decision and is not asserted here.

**27.11 Persistence boundary (contract F).** C3 guarantees only that the process-internal publication of the next authoritative game is atomic. SQLite transactions, recovery from a failed save and the order "durable commit, then memory/presentation publication" are Phase 4 hand-off contracts. A CAS success does **not** mean the memory and a database are atomically consistent, and this design does not claim it.

**27.12 Re-submitting a committed candidate (D-6).** A candidate that was already confirmed is never accepted again: the result is an explicit rejection (`already_committed`, or the existing freshness contract such as `PitchCandidate.state_stale` / `rng_state_changed`; which of them wins is fixed in the C3 implementation contract without changing the `CheckPrepared` codes or order). A re-submission must not advance the RNG, the event sequence or the game state, and must not raise `Revision`.

**27.13 Authorised split (design only).**
- **C3-A (gate foundation):** `AuthoritativeGame`, `Revision` and CAS, `StagedCommit`/`CommitRecord`, canonical ids and continuity, official events that consume no random number, `Finalize` in the same Publish, basic duplicate and freshness checks.
- **C3-B (pitch-candidate integration):** the non-in-play `(PreparedPitch, Candidate)` adapter, the in-play adapter, `CheckPrepared`, H0/H1/H2 and per-stream raw draws, the reducer re-run, structural preview verification, `ef2` receipts and the pitch count.
- **C3-C (stability and regression):** same-candidate re-submission, concurrent confirmation of different candidates, CAS races, stale candidates, failure/cancellation invariance, deterministic retry, game-event replay equivalence, `Finalize` atomicity, regression of C2B-1/2/3, the full xUnit suite and the project verification gate, the implementation report and an independent review.

Each part's code is a separate authorization. Out of scope for all three: a plate-appearance driver or any "next action" choice (3-F harness, D-4), new candidate generation, persistence, repository, UI API.

**27.14 Differences recorded, nothing changed (D-4).** ROADMAP Phase 3 task 3 ("feed results into the existing reducer ... resolve real pitches/counts internally and emit only valid decision boundaries") and task 4 (the headless harness) already require a plate-appearance flow, and the ROADMAP labels 3-C "pitch resolver flow" and 3-F "calibration and tests". The owner's D-4 assigns the PA loop and next-action choice to the later 3-F harness. That assignment is consistent with the Phase 3 tasks and Gate (nothing is removed) but is not what the 3-F label currently says; `ROADMAP.md` is **not edited** and the label wording is left for the owner to reconcile before 3-F. The ADR-007 lines "automatic plate-appearance progression is 3-C3" (3-C2A/3-C2B sections) and the matching handoff lines in this document (sections 21, 23 and 24) are past-time statements that D-4 supersedes; they are kept as history.

**27.15 Remaining decisions before any C3 code.** Final error codes and their priority (D-6) and the conceptual identifiers; the exact structural comparison list as test fixtures; how `LastEventSequence` consistency with receipts is checked for a restored game whose history contains non-canonical ids (they are refused by the gate); the CAS holder type; and the Phase 4 persistence list for `CommitRecord`. None blocks the design approval. **Status after 27.16 (2026-10-13):** for C3-A the error codes and priority, the identifiers, the history check of a restored game, the CAS holder and the minimal `CommitRecord` are decided in 27.16; still open are the structural-comparison test fixtures (C3-B), the Phase 4 persistence list for `CommitRecord` and `GameStatDelta`, and the ROADMAP 3-F label (owner).

**27.16 Phase 3-C3-A detailed contract (O-1 to O-9; OWNER APPROVED 2026-10-13 as DOCUMENTATION; NO implementation).** Source: owner instruction `TASK.md` "Phase 3-C3-A 세부 계약 최종 승인 반영", base HEAD `5328917`; the approval date rests on the project owner's chat approval, not on a file time. D-1 to D-8 and contracts A to F (27.1-27.15) are unchanged and remain the basis; this section fixes the identifiers, error codes, order and creation rules that 27.15 left open **for C3-A only**. C3-A production code, C3-B and C3-C are `NOT STARTED / NOT AUTHORIZED` (each needs the owner's separate instruction); Phase 3 Gate: INCOMPLETE. Facts below about existing code are from a read-only reading at HEAD `5328917`; nothing was executed.

*Types and visibility (O-7).* In `StarPlayer.Simulation.Baseball`: `AuthoritativeGame` (immutable, `public sealed`, construction only through internal factories), `GameCursor` (one per game; holds the single current `AuthoritativeGame` reference and offers `Current` and `Publish`), `CommitGate` (static, pure `Stage`), `StagedCommit` (immutable: the expected aggregate reference, the next aggregate, the `CommitRecord`) and `CommitRecord` (immutable). Types are `public` with `internal` constructors, like `PreparedPitch`, so that `HiddenTendencyBoundaryTests` can pin them by `typeof`; they are added to its list and no Application or Infrastructure member signature may name them (D-8). `HiddenTendencyBoundaryTests` itself stays satisfied. No existing type or contract is edited: `GameEventIds`, `GameReducer`, `PitchPreparation.CheckPrepared`, `EventFingerprint` and the RNG are unchanged.

*Error codes (O-1).* Existing `Id.*`, `Rng.*` and `Game.*` codes pass through unchanged and are never replaced by a new code (`Id.game_invalid`, `Id.sequence_invalid`, `Rng.*` from `GameRandom.Restore`, and the reducer's own `Game.*` refusal). New codes:

| Code | Raised when |
|---|---|
| `Commit.authority_missing` / `Commit.event_missing` | the aggregate or the event is null |
| `Commit.event_kind_not_supported` | C3-A receives `PitchEvent`, `TakeEvent` or `InPlayEvent` (O-4) |
| `Commit.authority_inconsistent` | the aggregate's last receipt does not match its `LastEventSequence`; a defence that the factories make unreachable |
| `Commit.event_game_mismatch` | the id has the canonical shape `{game}:e{six digits}` of another game |
| `Commit.event_id_invalid` | null, blank, non-canonical (for example a diagnostic id such as `e1`) or a sequence outside 1-999999 (including a seventh digit) |
| `Commit.event_sequence_gap` | the sequence is greater than `LastEventSequence + 1` |
| `Commit.event_id_in_use` | the sequence is at most `LastEventSequence` and the existing receipt differs in any of its four fields (typically a candidate numbered from a stale last sequence) |
| `Commit.already_committed` | the sequence is at most `LastEventSequence` and the existing receipt equals the proposal's `EventFingerprint.Receipt` in all four fields (D-6) |
| `Commit.receipt_mismatch` | the re-run reducer result is a duplicate, did not append exactly one receipt, or its last receipt is not the event's own |
| `Commit.finalize_failed` | `GameReducer.Finalize` failed; the message names the inner code |
| `Commit.final_state_incomplete` | after `Finalize` the state is not complete: `WasNew` false, `Final` null, or status, outcome, score or GameId disagree |
| `Commit.revision_stale` | **Publish:** the staged `Expected` reference is already not the cursor's current one; the CAS is not attempted |
| `Commit.cas_lost` | **Publish:** `Interlocked.CompareExchange` returned a reference other than `Expected` |
| `Commit.staged_missing` | **Publish:** the `StagedCommit` is null |
| `Commit.revision_exhausted` | `Revision == long.MaxValue` |

`Id.sequence_exhausted` stays the id issuer's code (`GameEventIds.Next`); the gate classifies a seventh-digit id as `Commit.event_id_invalid`. A reducer refusal in step 9 below passes through as its `Game.*` code; a `Finalize` failure is wrapped as `Commit.finalize_failed` so the two are told apart. Cancellation is **not** an error code (O-3). `Authoritative.*` codes (creation and restoration): `state_missing`, `rng_missing`, `rng_context_mismatch`, `history_event_id_invalid`, `history_sequence_gap`, `history_sequence_duplicate`, `history_receipt_invalid`, `last_sequence_mismatch`, `initial_state_not_empty`, `final_state_invalid`, `revision_invalid`, `evidence_without_receipt`.

*Correction of a source attribution.* `event_id_in_use`, `event_duplicate`, `receipt_invalid` and `pitch_count_invalid` are **not** checked by `PitchPreparation.CheckPrepared`; each candidate builder checks them itself (`PitchCandidate.*` in `BuildNonInPlayCandidate`, `InPlayCandidate.*` in `InPlayPitchPlanning.Build` and `AdvancementPlanning.Resolve`). `CheckPrepared` keeps exactly its existing codes and order; C3 does not change either. The earlier sentence in `Star/02_Decisions/2026-10-13_C3_Design_Approval.md` is corrected there and its history is kept.

*Stage order (O-2, alternative C).* `CommitGate.Stage(expected, event, cancellation)` is pure; the first failing step returns its error and later steps do not run:

| # | Check | C3-A |
|---|---|---|
| 0 | cancellation | yes |
| 1 | the aggregate and the event exist | yes |
| 2 | the event kind is allowed | yes |
| 3 | the aggregate is consistent (its last receipt matches `LastEventSequence`) | yes |
| 4 | event id form and GameId (`event_id_invalid`, `event_game_mismatch`) | yes |
| 5 | an identical event already committed (`already_committed`) | yes |
| 6 | sequence gap (`event_sequence_gap`) | yes |
| 7 | **C3-B insertion point:** candidate linkage and the unchanged `CheckPrepared` | not implemented |
| 8 | same id with a different event (`event_id_in_use`) | yes |
| 9 | `GameReducer.Apply` on the current authoritative state, then the receipt check | yes |
| 10 | **C3-B insertion point:** structural preview and RNG checks (27.6 (5)-(6), 27.8) | not implemented |
| 11 | if the re-run state is final: `GameReducer.Finalize` and the completeness check | yes |
| 12 | `Revision` overflow, last cancellation check, building the `StagedCommit` | yes |

Reasons: an identical re-submission is reported as `already_committed` before any freshness code; a stale candidate then gets the existing specific `PitchCandidate.*` code from step 7 in C3-B instead of a generic id error; step 8 follows step 7 so that it never hides them. `GameReducer.Apply` answers a re-sent identical id with a successful `WasDuplicate` before it looks at `Status == Final`, so step 5 must run before step 9 to keep a duplicate from looking like a success. In C3-A steps 7 and 10 are recorded insertion points only: no pitch-candidate check is implemented and `CheckPrepared`'s codes and order are not touched. Stage reads its input aggregate and changes nothing shared; a failure changes no `GameState`, RNG, event id, `Revision` or `LastCommit`.

*Publish and cancellation (O-3).* `GameCursor.Publish(staged, cancellation)`: (a) `staged` null: `Commit.staged_missing`; (b) `staged.Expected` is not the current reference: `Commit.revision_stale`, no CAS; (c) the last cancellation check, before the CAS; (d) `Interlocked.CompareExchange(ref current, staged.Next, staged.Expected)`; (e) the original reference equals `Expected`: return the staged `CommitRecord` as success; any other: `Commit.cas_lost`. Cancellation uses the existing convention, `OperationCanceledException`, and is checked at Stage steps and once before the CAS. **After a successful CAS no cancellation check runs, no exception can propagate and no callback that may throw is invoked; a committed change is never reported as failed.** Test hooks (stage-name callbacks, as in 3-C2B) may fail only before the CAS; a hook that runs after the CAS must not be able to alter the returned result, and tests assert that the success result and the published aggregate agree. Publishing the same `StagedCommit` twice, or two staged commits made from the same `Expected`, succeeds at most once; the loser gets `revision_stale` or `cas_lost` and nothing changes. A staged commit made for another game's cursor is refused the same way (its `Expected` is not that cursor's reference).

*Allowed events (O-4).* C3-A confirms only events that consume no random number: `ApproachEvent`, `RunnerPlayEvent`, `SubstitutionEvent`. `PitchEvent`, `TakeEvent` and `InPlayEvent` are refused with `Commit.event_kind_not_supported`; a pitch can enter the same gate only in C3-B, after the existing Prepare/Build/Resolve contracts and the H0 to H1/H2 checks are connected, so no path confirms a pitch without its RNG. For the allowed events the next aggregate keeps the **same** `GameRandomSnapshot` reference (equal `StateHash`), and the re-run `Apply` result is adopted directly: there is no candidate preview, so the structural comparison of 27.6 (5) does not apply. A future runner decision that does draw a random number needs its own RNG-carrying proposal path (C3-B or later), not C3-A. No event generator and no plate-appearance driver (D-4).

*Revision (O-5).* `AuthoritativeGame` holds `GameState State`, `GameRandomSnapshot Rng`, `int LastEventSequence`, `long Revision` and `CommitRecord? LastCommit` as separate values. `Revision` is independent of the event sequence: restoration does not force `Revision == LastEventSequence`. Every successful Publish raises it by exactly 1 (`Next.Revision = Expected.Revision + 1` computed in Stage; of staged commits made from the same `Expected`, the CAS lets one through). At `long.MaxValue` the commit is refused (`Commit.revision_exhausted`). **A successful Publish raises `Revision` by exactly 1. The `Apply` and `Finalize` of a game-ending event are processed in the same Stage and published as one Publish; C3-A has no finalization-only Publish.** `AppliedEvents.Count` is only an auxiliary history check and never replaces `Revision`.

*Creation and restoration (O-6).* `StartNew(state, rng)`: the receipt history is empty, `LastEventSequence = 0`, `Revision = 0`, `LastCommit = null` (`Authoritative.initial_state_not_empty` otherwise). `Restore(state, rng, lastEventSequence, revision)` checks: state and RNG exist; the GameId is canonical (the `GameEventIds.Create` rule, `Id.game_invalid`); the snapshot restores (`Rng.*` passes through) and its `ContextKey` equals the GameId (Ordinal, `Authoritative.rng_context_mismatch`); `lastEventSequence` is 0 to 999999 (`Id.sequence_invalid`); every receipt id parses as a canonical id of this game (`history_event_id_invalid`; another game or a non-canonical id is refused); the i-th receipt (counted from 1) has sequence i (`history_sequence_gap`; a repeated sequence is `history_sequence_duplicate`); every receipt has fingerprint version `ef1` or `ef2` and a 64-character lower-case hexadecimal fingerprint and a non-empty kind (`history_receipt_invalid`); `lastEventSequence` equals the number of receipts (`last_sequence_mismatch`); a final state is complete: `Status == Final` requires `Final != null` with matching GameId, outcome and score, and `Status == InProgress` requires `Final == null` and no outcome (`final_state_invalid`); `revision >= 0` (`revision_invalid`). **A game with `Status == Final && Final == null` is refused on restoration**, not finalized implicitly. The restored `LastCommit` is `null`. A corrupted history is refused: no renumbering, no deleted receipt, no implicit repair. The same complete-state rule holds for every Publish: an aggregate with `Status == Final && Final == null` can never exist.

*EventId form.* Official ids are `{GameId}:e{sequence}` with the sequence **exactly six digits, 1 to 999999** (`GameEventIds.MaxSequence`; there is no `int`-limit case). A new id is exactly `LastEventSequence + 1`. At 999999 no further official id exists; the issuer's `Id.sequence_exhausted` and the gate's refusal of a seventh digit both leave the aggregate unchanged. The receipt of sequence `s` of a valid aggregate is `AppliedEvents[s - 1]`, so the lookup in steps 5 and 8 is direct.

*Limit of the history check.* An `EventReceipt` holds only the id, kind, fingerprint version and fingerprint; the event body is not stored in `GameState`. **Verifiable:** id form, game membership, continuity, receipt form. **Not verifiable:** that a stored fingerprint matches an event body that was never stored. A restored history is therefore never called fully re-verified; reports and tests name this check "id and continuity", not "authenticity".

*Evidence references (O-8).* Restoration also checks the explicit event references that exist in the domain: `ScoredPlay.EventId` and `SubstitutionRecord.EventId` must each equal a receipt id (`Authoritative.evidence_without_receipt`). `StatEntry.SourceId` and `ScoredPlay.Id` are **not** treated as event ids: `SourceId` is a generic source field (the reducer currently writes the event id there, but its type does not declare that), and a play id is a different key. No fingerprint is rebuilt from stored data and no new restoration policy is introduced.

*`CommitRecord` (O-9).* Immutable, minimal for C3-A: the confirmed `GameEvent`, its `EventReceipt`, the event sequence, `RevisionBefore` and `RevisionAfter`, `RngStateHashBefore` and `RngStateHashAfter` (equal for an event without RNG), the reducer `Facts` (plus `GameCompleted` once when the step finalized), and whether the game was `Finalized`. It names no candidate, `PreparedPitch`, draft, audit or `AdvancementDetail`. `GameStatDelta` and the Phase 4 persistence format are later contracts. The record is built before the CAS from the same values as the next aggregate (sequence, revision, hashes and last receipt agree) and is that aggregate's `LastCommit` — the same instance that Publish returns; success is returned only after the CAS has succeeded.

*Scope.* C3-A: the aggregate, the cursor, the gate, the staged commit and record, canonical id and continuity, `Revision` and the CAS, official events without RNG, `Finalize` in the same Publish, and failure invariance for cancellation, duplicate and race. C3-A excludes pitch-candidate confirmation, the H0 to H1/H2 `RawDraws` checks, changes to the C2B-3 resolver, the whole plate-appearance loop, SQLite transactions, UI, a new repository and anything of Phase 4. C3-B (pitch-candidate integration) and C3-C (stability and regression) keep their 27.13 scope. Required test coverage for C3-A, when it is authorised: each new code with a fixture; overlapping failures in the order above; the restoration cases (empty history with last sequence 0 and 3, a contiguous history, a non-canonical id, a gap, a duplicate, an out-of-order pair, a seventh-digit id, another game's receipt, a last-sequence mismatch, last sequence 999999 accepted and 1000000 or -1 refused, a bad fingerprint version or shape, an incomplete final state, evidence without a receipt); no change of any aggregate member after each failure; RNG reference preserved for the three allowed events; `Revision` +1 once for a finalizing event and no `Final == null` state; the CAS race with exactly one winner; cancellation before the CAS and none reported after it; the boundary test.
