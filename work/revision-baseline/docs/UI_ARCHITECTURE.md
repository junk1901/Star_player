# Star Player UI Architecture

Implementation companion to [ROADMAP.md](../ROADMAP.md). Authority: [UI/UX Guide v1.8](../sources/UI_UX_GUIDE_v1.8.md) (**U**) and [Game Design v1.6](../sources/Star_Player_game_v1.6.txt) (**G**). This document fills in engineering/layout details; it does not establish final colors, fonts, artwork or animation before Alpha.

The additional portrait-design file discovered in the new workspace is **excluded**, following the owner's 2026-10-05 instruction to use only the original two documents. Existing datapack photo support and fallback assets remain in scope. No seeded portrait generator or Gemini image/batch workflow is introduced.

## 1. Scene and presenter boundary

One `AppShell` persists while ordinary content routes change. Its application session is injected by Bootstrap. Use `Control`, `MarginContainer`, `VBoxContainer`, `HBoxContainer`, `PanelContainer`, `ScrollContainer`, `TabContainer`, `GridContainer`, standard dialogs and a thin presenter per screen. A presenter sends commands and consumes public read models; it cannot call a SQL connection or domain mutator.

```text
AppShell (Control, full rect)
  ShellMargin
    ShellColumn (VBoxContainer)
      TopStatusBar
      Body (HBoxContainer)
        NavigationMenu (scrollable)
        ContentHost (expand/fill)
        ContextPanel (compact or tabbed at FHD)
      StatusFooter (only when meaningful)
  OverlayHost (profile inspection / modal choice / confirmation)
  ToastHost (nonblocking)
```

Main Menu and career-final summary are distinct roots. Game/major celebrations may use the source-authorized full-content layout. `GameSession` lives in Application; opening a profile never destroys it. Scene names may follow `game/UI/<Screen>/<Screen>Screen.tscn` and a matching presenter. Do not create an inheritance hierarchy for every panel.

Presenter lifecycle: enter route → restore local state → request revision-tagged read model → render loading/data/empty/error → subscribe to relevant committed-revision notifications → cancel queries/unsubscribe on exit. Bind results only if route and session tokens still match. Use deferred main-thread updates for Godot controls; background tasks return plain immutable data.

## 2. Two explicit layouts

Official design/QA targets are exactly **2560 × 1440** and **1920 × 1080**, both 16:9. Do not implement 4K/ultrawide/16:10/minimum-laptop projects as required work.

These are proposed starting container dimensions in UI pixels at 100% content scale, refined by Prototype 2 usability tests. They are not a new visual-design promise.

| Element | QHD baseline | FHD compact | Behavior |
|---|---:|---:|---|
| Outer margins | 24 | 16 | Container margins, never manual offsets for every label |
| Top status | 80 | 72 | Date, season, team/level, next action remain visible |
| Navigation | 224 | 192 | Same labels/routes; scroll if height requires |
| Body gaps | 16 | 12 | Reduce space before reducing text |
| Context panel | 344 | 280 | On dense screens move secondary cards into a context tab |
| Main content, all panels shown | 1912 | 1392 | Remaining width after margins/menu/panel/two gaps |
| Optional footer | 32 | 28 | Collapses when unused |
| Body text starting range | 18–22 | 18–22 | Keep readable; final font/tokens decided in Phase 28 |

Measure the actual viewport rather than assuming OS DPI equals pixel size. Choose the layout profile from supported logical viewport size. Test at both targets in the exported desktop build. Do not combine automatic uniform downscaling with compact layout in a way that shrinks text twice.

For tables, keep identity and decision-critical columns visible. FHD may move age/RBI/secondary columns to details but never remove access. A records row can use `Player | POS | G | AVG | HR | OPS | WAR` with the rest in its details/profile. Show both batting and pitching summaries for two-way players using stacked cards or tabs; do not quietly choose one role.

## 3. Navigation and state restoration

Top-level routes preserve U §5: Home, My Player, Game, Schedule, Training, Records, Team, League, Contract, Assets, Life, Relationships, News, Career. League contains Player Database, Teams, and Newcomers/Draft. No new top-level Player DB. National/military views are reached from profile/career/contextual events rather than mandatory extra permanent top-level menus.

Each navigation entry stores route kind, stable subject ID, selected tab, query text, filter values, sort column/direction, season/competition/stat scope, page cursor and scroll anchor `(row_id, offset)`. Restore by stable row identity where rows have shifted. A missing/retired player remains a profile identity; a truly unavailable reference shows a typed empty/error state with Back.

`PlayerLink(PlayerId, public_label)` and `TeamLink(TeamId, public_label)` call the same route service everywhere. School links open a lightweight amateur-team panel with an alumni filter action. News links come from backend entity references, not name matching in generated prose. Duplicate names remain unambiguous through team/position context.

During live games, profile inspection is a modal/drawer over the game route. Disable game-advance controls while its blocking overlay owns focus. Returning restores the same `decision_id`, count, strategy selection and game revision. Ordinary history/news inspection never consumes RNG or advances world time.

## 4. Shared components, introduced on demand

| Component | First phase | Contract / responsibility |
|---|---|---|
| StatusBadge / RatingDisplay | 1 | Show provided public number/grade/range/category; cannot fetch truth |
| Scoreboard / BaseOccupancy | 2 | Display scored game facts with inning, half, count, outs and score |
| FieldDiagram / StrategyPanel | 3 | Position/name labels, runner evaluation, legal choices and pending/busy state |
| SaveSlotCard / ValidationIssueList | 4 | Metadata, compatibility, file/entity/field errors and recovery choices |
| AppShell / PlayerLink / Notification / ChoiceModal | 5 | Stable routes, factual links, priority, focus and explicit command submission |
| TeamLink / StatTable / FilterBar / ProfileTabs | 6 | Common identity navigation, bounded rows, fixed header, numeric alignment, query state |
| InjuryCard / RecoverySteps | 8 | Estimated prognosis and actual current stage, care commands |
| OfferComparison / AgentAdvice | 14 | Comparable money/length/role/conditions, advisory text separate from final choice |
| Timeline / HistoryFilters | 11 basic; 23 complete | Dated facts and provenance without duplicate totals |
| SchoolPanel / DraftPickRow | 12–13 | Origin and actual/estimated pick information distinguished |
| AssetCard / ActivityChoice | 20 | Ownership, cost/time/effect descriptions and affordability errors |
| NewsCard / ArticleView | 5 templates; 26 complete | Public facts/escaped text, typed links and fallback |
| Celebration / CareerSummary | 25; 29 polish | Presents committed outcomes; dismissal cannot issue another outcome |

Use standard Godot controls initially. `Tree` can serve early tabular views if it meets actual requirements. Once long-save measurements demand it, use paged/recycled visible rows with stable data keys; do not instantiate tens of thousands of row scenes. A small shared table behavior is enough; avoid a universal JSON-driven UI framework.

## 5. Screen and route inventory

“Complete” is the phase that delivers required information/behavior, not final polish. All screens participate in Phase 28/29 visual/QA gates. U references are section numbers in the original guide.

| Route / scene | Required information and actions | Query/command owners | First / complete phase | Source |
|---|---|---|---|---|
| MainMenu | New Career, Continue latest valid, Load, Data Management, Settings, Exit | Save, Pack, session | 0 / 4 | U34–35 |
| SaveLoad | Career/player/team/age/season/time/pack, multiple manual slots, autosave, compatibility, retry/recovery | C33 | 4 / 4 | U34 |
| DataManagement / PackPicker | Folder/file selection, validate before install, issue paths, selected new-career pack | C34 | 4 / 27 | U35; G10 |
| NewPlayer | Basic info → origin → position/handedness → skills → personality → confirmation; DOB/body/form/repertoire/features per role | C01/C02/C05, D4 | 5 / 13 | U36; G2 |
| ExistingPlayer | Name/team/position/age/level search; preserve imported career facts; edit personality only | C01/C35 | 5 / 5 | U37; G2 |
| Home | Self/role, both-role season summaries, condition/fatigue/sharpness/stress/satisfaction, injury priority, trust, next activity | State, Stats, Club, calendar | 5 / 11, later cards as features arrive | U6 |
| MyPlayer | Profile/skills/state/position/evaluation/career; own exact current ratings; hidden potential represented only by uncertain evaluation | C01–C05 | 5 / 7 | U7 |
| Game | Score/inning/count/outs/runners/opponent/state/legal strategy/mode; compact defense field; result and next situation | C08/C07 public projection | 3 / 11 | U8 |
| GameResult / BoxScore | Outcome, own stats, season update, key plays/milestones/state/injury/trust/news; detailed box score | C08/C11 | 3 / 11 | U9 |
| Training | Focus/intensity, fatigue/risk categories, coach advice, two-way allocation, position/pitch learning | C05/C04/C03 | 5 / 7 | U10; G4 |
| Schedule | Date/stage/competition, fixtures/results, next activity, required-choice barrier, safe advancement/cancel | C09/C10 | 6 / 19 | U3–4/38; G5–6 |
| Records | Season/career/second-team/national/postseason/rankings; batting/pitching and metric explanations | C11 | 6 / 23 | U11/33 |
| Team | Current rank/results/roster/position competition/injuries/rotation/expected role; no management buttons | C06/C07 | 6 / 10 | U12 |
| League | Standings, individual records, awards race, prospects, news, history, linked teams | C10/C11/C28 | 6 / 24 | U13 |
| PlayerDatabase | Name/team/position/age/hitter-pitcher/level/rookie-prospect-veteran/active-retired-all/origin/registration filters; stable sorting | C35 | 6 / 24 | U13/50/51 |
| PlayerProfile | Profile/evaluation/stats/career/contract/injury/awards/national/news; uniform IDs; youth/retired context adjusts emphasis | C35 | 6 / 24; legacy outcomes 25 | U13 |
| TeamList / TeamProfile | Overview/history/season/roster/records/awards/retired numbers; title/year drill-down, key players, stadium/manager | C36/C28 | 6 / 24; honor results 25 | U52 |
| NewcomersDraft | Pool/high-school/university/overseas/projection/history tabs; actual outcomes separate from estimates | C14–C16/C39 | 12 / 24 | U50–51 |
| AmateurTeamPanel | Type/region/reputation/exposure categories, recent output, active/retired alumni, origin chain | C15/C35 | 12 / 24 | U50 |
| Contract / FA | Salary/length/bonus/options/service/agent, club and commercial offers, money/role/team comparison, own final decision | C13/C18/C37/C38 | 14 / 22 | U14 |
| Assets | Current assets/lifetime and annual income/expenses; cars/housing/equipment/investment/savings/other | C25 | 14 income / 20 | U15 |
| Life | Housing/car/family, satisfaction/stress, available rest/friends/family/shopping/private-training activities | C24/C25 | 20 / 21 | U16 |
| Relationships | Relevant people, categories/reasons/history/news; manager trust separately | C23/C06 | 21 / 21 | U17 |
| News | All/self/team/league/national/life categories; importance, date, linked facts and rich major bundles | C29 | 5 / 26 | U18 |
| ChoiceEvent | Fixed legal choices, confirmed participants, only intended outcome hints, priority and pending state | C26 + owner command | 5 / 21 | U19/27–32 |
| InjuryDetail | Body/type/severity/estimated absence/relapse category, play-rest and recovery stages | C12 | 8 / 10 | U20 |
| LevelChange | Separate registration and league assignment, reason, career/news links | C07/C17/C39 | 10 / 13 | U21/51 |
| NationalTournament | Selection/nonselection, role, edition, fixtures/bracket/medals, shared game route, career records | C20/C21 | 18 / 18 | U22 |
| Military | Always-visible profile status; defer/apply/ordinary service choices as eligible; result/return | C22 | 19 / 19 | U23 |
| Career | Year records, team stints, contracts, awards, international, injuries, entry/role/military/life events and milestones | C27 | 6 basic / 23 | U24 |
| RetirementSummary / Hall | Lifetime/team/peak/awards/national/titles/income/injuries/relations/news/honors, farewell and future epilogue | C30–C32 | 25 / 25 | U25; G9 |
| Settings | Supported viewport/window mode, audio/display, optional Gemini connection/budget, data management | Preferences, Narrative, Pack | 4 basic / 28 | U34/46; master76 |

## 6. Game layout and interaction detail

The score/count header is always visible. The left decision panel shows current batter and opposing pitcher, handedness, relevant public ability, condition/fatigue and legal strategy choices. Starting width: about 440 QHD / 360 FHD; it can stack subsections rather than squeeze every label. The central field shows nine defenders at recognizable positions using `POS + PlayerLink`, plus only the current batter and occupied bases on offense. No portrait thumbnails are required on the normal game field.

Place runner labels outside defensive labels with small leader lines/offset anchors if occupied bases collide with first/second/third-base defenders. Keep clickable hit regions distinct. At FHD, detailed defense/runner cards open on click/hover rather than placing all data permanently on the field. Tooltip disclosure follows the same viewer policy as the profile.

Runner evaluation is exact only for self; NPC grade/range/confidence is informative but does not reveal internal ratings. Current opponent information likewise uses estimates. Current mode is always labeled. The engine supplies the count and legal choices; the UI cannot fabricate a plausible count or show unavailable tactical actions. Substitution information explains AI decisions; it does not grant manager control.

Phase 3 provides test-game choices. Phase 5 adds own batting, own pitching and instant modes. Phase 11 completes important-situations and entire-game presentation using the same resolver and a deterministic importance selector over game state. A presentation mode chooses when to pause/show results, not which simulation formula to run. Choosing a different strategy may legitimately change the result; changing only playback must not.

## 7. Priority and screen states

- **Priority A:** Immediate decisions/information: injury, contract/FA, call-up/demotion, national selection, military, retirement. Required choices block advancement with a visible reason. Informational call-up notices need acknowledgment but cannot alter the result.
- **Priority B:** Home summary: condition, recent performance, trust/role, next game, recent news.
- **Priority C:** Detailed stats/history/relationships/assets remain browsable without repetitive popups.

Every data screen implements Loading, Ready, Empty and Error. Empty explains whether there are no matches, no history yet, or unavailable historical data; a missing asset is not a broken player record. Error retains route/filter state and offers Retry/Back. Query cancellation is normal when navigating. Save errors retain the prior valid generation and never report success. Optional AI error keeps template text available. Active processing disables repeated submits while retaining readable state.

Choice dialogs use a focus trap, explicit options and a safe return policy. Escape closes an inspection popup but never silently accepts/rejects a pending contract or injury choice. Destructive slot overwrite needs a clear target confirmation; creating a new slot does not. Unread toasts do not stack blocking modals. Coalesce repeated minor notifications while preserving their event list.

## 8. Verification inventory

For each screen row record: implementation phase, scenario/save fixture, viewport, public DTO, action/result, screenshot path, keyboard/mouse behavior, loading/empty/error evidence and any open defect. No evidence means not tested.

Required stress fixtures: long Korean/Latin same-name players and teams; 50-season histories; large money/stat totals; two-way player; injured developmental player in second team; military squad assignment; retired multi-team legend; missing/corrupt images; invalid pack; old/new/corrupt save; Gemini offline/timeout; nested profile during a pending live-game decision.

Check text/button/table clipping, scroll and fixed headers, numeric alignment, popup bounds, wrapped articles, field label overlap, visible strategy/mode/next action and FHD access to moved columns. Run hidden-data serialization tests as well as screenshots; absence on a screenshot does not prove the DTO is safe. No final palette or icon set is required before Alpha.
