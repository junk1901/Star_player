# Star Player UI Architecture

Implementation companion to [ROADMAP.md](../ROADMAP.md). Authority: [UI/UX Guide v1.8](../sources/UI_UX_GUIDE_v1.8.md) (**U**) and [Game Design v1.6](../sources/Star_Player_game_v1.6.txt) (**G**). This document fills in engineering/layout details; it does not establish final colors, fonts, artwork or animation before Alpha.

The [owner revision R](../sources/ROADMAP_REVISION_REQUEST.txt) controls its changed UX decisions, targeted revision A adds venue/season/team context, final feature revision F adds jersey history plus imported-rookie discovery/start, owner-decision revision O approves D1–D6, CF adds one bounded read-only Club Finance Lite presentation as an approved Scope Lock exception, and the user-approved Broadcast Commentary Lite (ROADMAP §1.12) adds an offline text broadcaster chat to the Game view as a second presentation-only exception, and Live Match Viewer Lite (ROADMAP §1.13) adds the LMV-A 2D icon field to the Game view as a third. [Portrait specification P](../Star_Player_Portrait_System_Design.MD) is approved and remains independent: Phase 24 Seed/profile base, Phase 26 explicit selected Gemini image workflow, Phase 28 final polish. Do not duplicate its specification here. All player-facing UI, help, questions, notifications and generated content are **Korean**; English route/component names below are developer identifiers only.

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

For tables, keep identity and decision-critical columns visible. FHD may move age/RBI/secondary columns to details but never remove access. A records row can use `선수 | 포지션 | 경기 | 타율 | 홈런 | OPS | WAR` with the rest in its details/profile. Show both batting and pitching summaries for two-way players using stacked cards or tabs; do not quietly choose one role.

## 3. Navigation and state restoration

Top-level routes preserve U §5: Home, My Player, Game, Schedule, Training, Records, Team, League, Contract, Assets, Life, Relationships, News, Career. League contains Player Database, Teams, and Newcomers/Draft. No new top-level Player DB. Korean labels include 홈, 내 선수, 경기, 일정, 훈련, 기록, 구단, 리그, 계약, 자산, 생활, 인간관계, 뉴스, 커리어; League subroutes use 선수 DB, 구단 목록, 신인 / 드래프트. Shell actions use 의사결정 대기함, 도움말 and 설정. National/military views are reached from profile/career/contextual events rather than mandatory extra permanent top-level menus.

Each navigation entry stores route kind, stable subject ID, selected tab, query text, filter values, sort column/direction, season/competition/stat scope, page cursor and scroll anchor `(row_id, offset)`. Restore by stable row identity where rows have shifted. A missing/retired player remains a profile identity; a truly unavailable reference shows a typed empty/error state with Back.

`PlayerLink(PlayerId, public_label)`, `TeamLink(TeamId, public_label)` and `StadiumLink(StadiumId, public_label)` call the same route service everywhere. School links open a lightweight amateur-team panel with an alumni filter action. News links come from backend entity references, not name matching in generated prose. Duplicate names remain unambiguous through team/position context.

During live games, profile inspection is a modal/drawer over the game route. Disable game-advance controls while its blocking overlay owns focus. Returning restores the same `decision_id`, count, strategy selection and game revision. Ordinary history/news inspection never consumes RNG or advances world time.

## 4. Shared components, introduced on demand

| Component | First phase | Contract / responsibility |
|---|---|---|
| StatusBadge / RatingDisplay | 1 | Show provided public number/grade/range/category; cannot fetch truth |
| Scoreboard / BaseOccupancy | 2 | Display scored game facts with inning, half, count, outs and score |
| FieldDiagram / StrategyPanel | 3 | Position/name labels, runner evaluation, legal choices and pending/busy state |
| SaveSlotCard / ValidationIssueList | 4 | Metadata, compatibility, file/entity/field errors and recovery choices |
| AppShell / PlayerLink / Notification / ChoiceModal | 5 | Stable routes, factual links, priority, focus and explicit command submission |
| PendingDecisionList / HelpTooltip / HelpPanel / SettingsPanel | 5–6 | C42; one authoritative barrier projection, first-time help/suppression/reset and real capability settings |
| TeamLink / StadiumLink / JerseyNumberBadge / StatTable / FilterBar / ProfileTabs | 6 | Common identity/navigation, current number from C47, bounded rows, fixed header, numeric alignment, query state |
| InjuryCard / RecoverySteps | 8 | Estimated prognosis and actual current stage, care commands |
| OfferComparison / AgentAdvice | 14 | Comparable money/length/role/conditions, advisory text separate from final choice |
| ClubFinancialSummary | 14 data; 24 complete | C48 public read model only; separates payroll/cap/budget/sponsorship and provides no management commands |
| Timeline / HistoryFilters | 11 basic; 23 complete | Dated facts and provenance without duplicate totals |
| SchoolPanel / DraftPickRow | 12–13 | Origin and actual/estimated pick information distinguished |
| AssetCard / ActivityChoice | 20 | Ownership, cost/time/effect descriptions and affordability errors |
| NewsCard / ArticleView / NarrativeBundleView | 5 routine templates; 26 complete | Confirmed facts, actual quote references, expressive Korean voices and pending/retry; no universal AI-native fallback |
| InterviewAnswerPanel | 26 | C43; reporter question, free-text original answer, submit/status/retry; no predefined final answer choices |
| FavoriteToggle / PlayerComparison / GameArchive / MilestoneProgress | 23 read models; 24 UI | C44; preferences/public projections/retained facts only |
| PortraitView / PortraitSelection | 24 base; 26 AI | C45; P-defined Seed/context/fallback and explicit single/batch workflow |
| Celebration / CareerSummary | 25; 29 polish | Presents committed outcomes; dismissal cannot issue another outcome |

Use standard Godot controls initially. `Tree` can serve early tabular views if it meets actual requirements. Once long-save measurements demand it, use paged/recycled visible rows with stable data keys; do not instantiate tens of thousands of row scenes. A small shared table behavior is enough; avoid a universal JSON-driven UI framework.

## 5. Screen and route inventory

“Complete” is the phase that delivers required information/behavior, not final polish. All screens participate in Phase 28/29 visual/QA gates. U references are section numbers in the original guide.

| Route / scene | Required information and actions | Query/command owners | First / complete phase | Source |
|---|---|---|---|---|
| MainMenu | New Career, Continue latest valid, Load, Data Management, Settings, Exit | Save, Pack, session | 0 / 4 | U34–35 |
| SaveLoad | Career/player/team/age/season/time/pack, multiple manual slots, autosave, compatibility, retry/recovery | C33 | 4 / 4 | U34 |
| DataManagement / PackPicker | Folder/file selection, validate before install, issue paths, selected new-career pack | C34 | 4 / 27 | U35; G10 |
| NewPlayer | Basic info → origin → position/handedness → skills → personality → confirmation, then the production Draft / Entry flow; Phase 5 shortcut is clearly fictional/post-entry; free biography text is descriptive | C01/C02/C05, approved D4 | 5 prototype / 13 production | U36; G2 |
| ExistingPlayer | Name/team/position/age/level plus 신인만 보기/드래프트 연도/지명 라운드 search; preserve imported draft/club/contract/registration/initial-number facts and PlayerId; edit personality only | C01/C14/C35/C47 | 5 / 5 | U37; G2; F5–6 |
| Home | Self/role, both-role season summaries, condition/fatigue/sharpness/stress/satisfaction, injury priority, trust, captain/team-atmosphere context, next activity and active All-Star race status | State, Stats, Club, calendar | 5 / 11, later cards as features arrive | U6; A4–6 |
| MyPlayer | Profile/skills/state/position/evaluation/career; own exact current ratings; hidden potential represented only by uncertain evaluation | C01–C05 | 5 / 7 | U7 |
| Game | Score/inning/real count/outs/runners/opponent/state/legal PA approach/mode; default one choice per PA, explicit one-pitch `Take` resumes if the PA continues; compact defense field; result and next situation. Phase 5+: LMV-A icon field (nine defenders, batter/runners, ball movement, out/safe/run effects, skip/speed) driven by read-only presentation cues, with a safe static fallback when events lack detail; collapsible read-only broadcaster chat (caster/analyst names and roles, off/highlights/fuller; side panel at QHD, tab/drawer at FHD) that never covers PA/count/score/runner/decision controls | C08/C07 public projection; approved D1; BCL commentary projection (§1.12); LMV-A presentation cues (§1.13) | 3 / 11 (field and chat 5, fixture crew 6, audit 28) | U8 |
| GameResult / BoxScore | Outcome, own stats, season update, key plays/milestones/state/injury/trust/news; detailed box score | C08/C11 | 3 / 11 | U9 |
| Training | Focus/intensity, fatigue/risk categories, coach advice, two-way allocation, position/pitch learning | C05/C04/C03 | 5 / 7 | U10; G4 |
| Schedule | Date/stage/competition, venue, minimal weather/status, fixtures/results, original↔makeup relation, next activity, required-choice barrier, safe advancement/cancel | C09/C10/C46 | 6 / 19 | U3–4/38; G5–6; A1/A7 |
| Records | Season/career/second-team/national/postseason/rankings; batting/pitching and metric explanations | C11 | 6 / 23 | U11/33 |
| Team | Current rank/results/roster/current jersey numbers/position competition/injuries/rotation/expected role, manager/coaches, captain, bounded team-atmosphere context and later read-only C48 financial summary; no management buttons | C06/C07/C47/C48 | 6 / 10; staff/team context 9/21; finance 14/24 | U12; A3–5; F1; CF7 |
| League | Standings, individual records, awards race, All-Star vote/rank/trend/final result, prospects, news, history, linked teams and relevant public club-finance context | C10/C11/C28/C48 | 6 / 24; All-Star 11; finance 14/24 | U13; A6; CF7 |
| PlayerDatabase | Name/team/position/age/hitter-pitcher/level/rookie-prospect-veteran/active-retired-all/origin/registration filters; stable sorting | C35 | 6 / 24 | U13/50/51 |
| PlayerProfile | Profile/current number/number history/draft source-result/evaluation/stats/career/contract/injury/awards/national/news; stable ID; youth/retired context adjusts emphasis | C14/C35/C47 | 6 / 24; legacy outcomes 25 | U13; F1/F5 |
| TeamList / TeamProfile | Overview/history/season/roster/current and historical numbers/records/awards/retired numbers; title/year drill-down, key players, linked stadium history, manager tenure, captain facts and read-only current/annual financial summaries | C36/C28/C46/C47/C48 | 6 / 24; finance 14/24; honor results 25 | U52; A1/A3/A4; F1; CF7 |
| NewcomersDraft | Pool/high-school/university/overseas/projection/history tabs; imported completed vs generated source and actual outcomes separate from estimates | C14–C16/C39 | 12 / 24 | U50–51; F2–10 |
| AmateurTeamPanel | Type/region/reputation/exposure categories, recent output, active/retired alumni, origin chain | C15/C35 | 12 / 24 | U50 |
| Contract / FA | Separate annual non-FA salary renewal and FA market; current/previous salary, club proposal, agent advice, bounded counteroffer/deadline, comparison, explainable public budget/cap context, own final decision and legal new-club number selection on signing | C13/C18/C37/C38/C47/C48 | 14 / 22; finance context 14/24 | U14; A2; F1; CF6–7 |
| Assets | Current assets/lifetime and annual income/expenses; cars/housing/equipment/investment/savings/other | C25 | 14 income / 20 | U15 |
| Life | Housing/car/family, satisfaction/stress, available rest/friends/family/shopping/private-training activities | C24/C25 | 20 / 21 | U16 |
| Relationships | Relevant people, categories/reasons/history/news; current manager trust/evaluation separate from personal relationship, with former staff history retained | C23/C06 | 21 / 21 | U17; A3 |
| News | All/self/team/league/national/life categories; importance, date, linked facts and rich major bundles | C29 | 5 / 26 | U18 |
| ChoiceEvent | Ordinary story/legal choices and confirmed participants; interview answers use the separate free-text route | C26 + owner command | 5 / 21 | U19/27–32; R16 |
| PendingDecisions / 의사결정 대기함 | Count, subject/type, due date/status, blocking reason and route to authoritative contract/FA/care/national/military/interview action; explicit expiry notices | C42 + decision owners | 5 / 6; later types on arrival | R4 |
| Help / 도움말 | First-time notices, hover/focus explanations, glossary, do-not-show-again, reset and reopen | C42 | 5 / 6 framework; later feature topics; 28 polish | R5 |
| Interview / 인터뷰 | Reporter question, free-text answer, preserved text, submit/loading/pending/retry and resulting reactions; no false completed answer on API failure | C43/C29/C42 | 21 context only / 26 interaction | R13–17 |
| Favorites / 관심 선수 | Bookmark players/prospects in Player DB; scoped preferences, no changed simulation | C44 | 24 / 24 | R10 |
| PlayerComparison / 선수 비교 | Two selected players; self exact current skills, NPC observed estimates, unavailable fields explicit | C44/C35 | 24 / 24 | R10 |
| GameArchive / 경기 기록 보관함 | Records/Career filters by season/date/team/postseason/own appearance; retained score/box score/lineup/progression/important events with coverage | C44/C27 | 23 data / 24 UX | R10 |
| MilestoneProgress / 기록 달성 현황 | Current/target/remaining on Career; no quest rewards or invented totals | C44/C27 | 23 data / 24 UX | R10 |
| PortraitProfile / PortraitSelection | Common profile Seed display; later explicit single/batch Gemini targets, count/cost confirmation, per-player retry/Seed restore | C45 | 24 base / 26 AI; 28 polish | R11; P16–21/34 |
| InjuryDetail | Body/type/severity/estimated absence/relapse category, play-rest and recovery stages | C12 | 8 / 10 | U20 |
| LevelChange | Separate registration and league assignment, reason, career/news links | C07/C17/C39 | 10 / 13 | U21/51 |
| NationalTournament | Selection/nonselection, role, edition, fixtures/bracket/medals, shared game route, career records | C20/C21 | 18 / 18 | U22 |
| Military | Always-visible profile status; defer/apply/ordinary service choices as eligible; result/return | C22 | 19 / 19 | U23 |
| Career | Year records, imported/generated draft facts, club stints with jersey timelines, contracts/salary history, awards/All-Star facts, captain tenures, relevant staff/stadium context, international, injuries, entry/role/military/life events and milestones | C14/C27/C46/C47 | 6 basic / 23 | U24; A1–4/A6; F1–12 |
| RetirementSummary / Hall | Lifetime/team/jersey timeline/peak/awards/national/titles/income/injuries/relations/news/honors, farewell and future epilogue | C30–C32/C47 | 25 / 25 | U25; G9; F1 |
| Settings / 설정 | Implemented display/window/scaling/speeds/modes/confirmation/autosave/help and, from Phase 5, commentary off/highlights/fuller (presentation-only, no gameplay effect); later audio, Narrative/Portrait AI configuration and data management | C42/C29/C45/C34 | 5–6 framework / 28 complete presentation; audio when 29 implements it | U34/46; R6/19 |

## 6. Game layout and interaction detail

The score/count header is always visible, and its count always comes from authoritative simulated pitch state. The left decision panel shows current batter and opposing pitcher, handedness, relevant public ability, condition/fatigue and legal strategy choices. Default interaction asks for one PA approach, never mandatory every-pitch input. An explicit `Take` applies to the next actual pitch; if the PA remains live, the panel returns at the legal decision point. Starting width: about 440 QHD / 360 FHD; it can stack subsections rather than squeeze every label. The central field shows nine defenders at recognizable positions using `POS + PlayerLink`, plus only the current batter and occupied bases on offense. No portrait thumbnails are required on the normal game field.

Place runner labels outside defensive labels with small leader lines/offset anchors if occupied bases collide with first/second/third-base defenders. Keep clickable hit regions distinct. At FHD, detailed defense/runner cards open on click/hover rather than placing all data permanently on the field. Tooltip disclosure follows the same viewer policy as the profile.

Runner evaluation is exact only for self; NPC grade/range/confidence is informative but does not reveal internal ratings. Current opponent information likewise uses estimates. Current mode is always labeled. The engine supplies the count and legal choices; the UI cannot fabricate a plausible count, reinterpret `Take` as the whole PA or show unavailable tactical actions. Substitution information explains AI decisions; it does not grant manager control.

Phase 3 provides test-game choices. Phase 5 adds own batting, own pitching and instant modes. Phase 11 completes important-situations and entire-game presentation using the same resolver and a deterministic importance selector over game state. A presentation mode chooses when to pause/show results, not which simulation formula to run. Choosing a different strategy may legitimately change the result; changing only playback must not.

**Live Match Viewer Lite (LMV-A) and extension boundary.** From Phase 5 the central Game field is the LMV-A icon field: a presentation-only 2D coordinate system, nine defender icons, batter and runner icons, ball movement after contact, out/safe/run effects and skip/speed controls. It plays versioned presentation cues derived read-only from committed events; a cue describes only what the event model records, and anything not recorded is drawn as a generic path or a static result, never asserted as an official detail. At QHD the field stays central with the BCL chat beside it; at FHD the field keeps its readability and the chat is a tab or collapsible panel. No field, chat or effect may cover the PA approach choice, real count, score, outs, runners or required actions. A future LMV-B (SD characters) would attach as another renderer over the same cues; it is not part of the current design Gates, and LMV-A/B modes are unrelated to BCL's broadcaster Crew A/B.

## 7. Priority and screen states

- **Priority A:** Immediate decisions/information: injury, annual salary/contract/FA, call-up/demotion, national selection, military, important interview, retirement. Required choices block advancement with a visible reason. Informational call-up, staff-change and weather notices need acknowledgment only when their authoritative workflow requires it and cannot alter the result.
- **Priority B:** Home summary: condition, recent performance, trust/role, next game, recent news.
- **Priority C:** Detailed stats/history/relationships/assets remain browsable without repetitive popups.

Every data screen implements Loading, Ready, Empty and Error. Empty explains whether there are no matches, no history yet, or unavailable historical data; a missing asset is not a broken player record. Error retains route/filter state and offers Retry/Back. Query cancellation is normal when navigating. Save errors retain the prior valid generation and never report success. Routine news remains available independently. AI-native interview/fan/SNS failures show Korean pending/retry states without pretending that a template is an AI result; an already-applied interview consequence is never repeated when reactions retry. Portrait errors retain the separate Seed/neutral image fallback. Active processing disables repeated submits while retaining readable state.

Choice dialogs use a focus trap, explicit options and a safe return policy. Escape closes an inspection popup but never silently accepts/rejects a pending contract or injury choice. Destructive slot overwrite needs a clear target confirmation; creating a new slot does not. Unread toasts do not stack blocking modals. Coalesce repeated minor notifications while preserving their event list.

## 8. Verification inventory

For each screen row record: implementation phase, scenario/save fixture, viewport, public DTO, action/result, screenshot path, keyboard/mouse behavior, loading/empty/error evidence and any open defect. No evidence means not tested.

Required stress fixtures: long Korean/Latin same-name players and teams; 50-season histories; large player and club KRW/stat totals; financial values unavailable or estimate-labeled; two-way player; injured developmental player in second team; military squad assignment; retired multi-team legend; missing/corrupt images; invalid pack; old/new/corrupt save; Gemini offline/timeout; nested profile during a pending live-game decision.

Check text/button/table clipping, scroll and fixed headers, numeric alignment, popup bounds, wrapped articles, field label overlap, visible strategy/mode/next action and FHD access to moved columns. Run hidden-data serialization tests as well as screenshots; absence on a screenshot does not prove the DTO is safe. No final palette or icon set is required before Alpha.

## 9. Early UX ownership and later capability content

Phase 5–6 establishes C42 once. Inbox shows the same persisted decisions that stop advancement; closing a popup or entering a profile leaves the item pending. Sort by urgency/date using public metadata, retain explicit resolved/expired notices, and expose the reason advancement stopped. It is a compact action surface, not an email simulation. Each owner adds its type when implemented, including Phase 8 care, Phase 14 contract, Phase 15 FA, Phase 18 national, Phase 19 military and Phase 26 important interviews.

Contextual help never executes an action. First injury explains care/recovery; first second-team assignment explains registration vs level; first FA eligibility uses the saved rule edition; developmental registration clarifies terminology; advanced metric hover/focus opens the formula/coverage explanation. Use public data, skippable first-time notices, persistent do-not-show-again and a reopenable/searchable glossary/help panel. Reset changes presentation preferences only.

Settings are capability-driven, with independently versioned user preferences rather than duplicated domain rules. Phase 5–6 exposes QHD/FHD behavior, supported window/borderless/fullscreen, readable UI text/font scaling, implemented game/text/animation speeds, implemented default game modes, confirmation preferences, permitted autosave cadence and help preferences/reset. Missing later modes/audio/providers are absent, not fake toggles. Core-required pre/post recovery and at least three completed autosaves cannot be disabled by a convenience setting. D6 adds no retention control unless measured Phase 17/29 evidence later demonstrates a real need. Test focus/overflow at both resolutions and the supported text scales. Phase 26 adds actual Gemini narrative/image configuration; Phase 28 completes styling/accessibility; Phase 29 adds audio controls only with audio.

## 10. Free-text interviews and expressive reactions

Phase 21 has confirmed context/topics/rules only; no predefined final answer intent. These are inactive feature records until Phase 26, so unavailable interaction cannot become a mandatory advance barrier. In Phase 26, activate actual interview requests and show a Korean reporter question and editable free-text response. Preserve exact submitted text/version; disable duplicate submit while awaiting interpretation, permit retry without duplicate consequences, and make unapplied edits invalidate old requests. Only C43's strictly validated semantic envelope reaches core rule mapping. Show consequences only from committed core outcomes, then linked reporter/media/fan/SNS voices grounded in supplied facts and that exact answer.

Distinguish factual claims/quotes from fictional opinion, humor and labeled speculation. A past quote links the actual stored interview; never fabricate text attributed to an older interview. Cache accepted bundle members; failed reactions remain retryable without undoing/reapplying consequences. A required unresolved interview stays visible in Inbox and may block its relevant advance boundary; unrelated article generation does not. Defer/decline is shown only when the authoritative request allows it. No-key/timeout/quota/invalid-output states preserve answer/save/load safety and do not insert a canned interpretation.

## 11. Catcher, QoL and portrait presentation

Player/Training views distinguish Receiving/Framing, Blocking, Pop Time, Game Calling, shared Fielding/Arm/Endurance and C proficiency. Self values follow normal exact-current access; NPC views/comparison retain observations/confidence. Team selection reasons may describe a familiar battery or catcher rest without exposing private policy scores or turning a pairing into a management button. Battery familiarity and personal friendship have separate labels and explanations.

Favorites remain scoped user preferences; comparison obtains two normal public projections. Archive/progress screens consume Phase 23 read models with no extra simulation. Unknown historical detail is labeled unavailable, and record progress derives from the same reconciled totals as actual milestones. All nested views restore the caller's game/filter/scroll state.

Portrait visuals/actions follow P without replicating its design. Phase 24 introduces Seed/profile/mode/fallback, with stable face and contextual kit and no image API. Phase 26 adds explicit player selection and single/batch confirmation, visible per-player statuses, failed-only retry, regeneration and Seed restore. Show the number of targets/model/image count/cost estimate or explicit unavailable estimate; retain large-selection confirmation. Current provider capability/pricing is verified during implementation, never invented in UI. Opening a profile, switching contexts, changing teams, aging, awards or loading saves never initiates Gemini images. Keep portrait API controls absent until implemented.

Revision QA extends §8 with distinct catcher skill/proficiency privacy, pair vs friendship labels, unresolved Inbox persistence, all help/reset paths, capability settings, comparison/favorites/archive/progress, exact raw Korean interview retention, invalid/late/duplicate interpretation callbacks, independent reaction retries and P's seeded/batch/fallback cases. These remain planned tests, not completed runtime evidence.

## 12. Venue, season and team-context presentation

The UI consumes authoritative state and never exposes controls to hire/fire staff or choose the captain. Staff transitions use factual Korean notices such as `감독 교체` and `새 감독 부임`; the Team, Relationships and Career views retain the previous manager's relationship/history while showing the new manager's separate evaluation context. Captaincy appears as `주장` and primarily opens leadership, teammate, media and legacy context. `팀 분위기` is a bounded cause-based category, not a precise hidden score or a direct ability bonus.

Schedule and Game views show the assigned venue and factual interruption state using labels such as `우천 취소`, `경기 연기` and `보충 경기`. Rescheduled fixtures link back to the original fixture where useful. Stadium/profile history is read-only through Team/Profile/Career links; the UI never recomputes a completed game's park context from the current stadium definition.

`올스타 투표` can surface on Home, League and News during the configured period with position, current rank/trend and final selected/not-selected status. The KBO Rule Engine supplies the configured procedure and visibility. `연봉 협상` is a dedicated annual non-FA flow inside Contract, with proposal, prior-salary comparison, agent advice, due status, bounded counteroffer and explicit player decision; it must not be presented as the FA market.

## 13. Jersey numbers and imported-rookie presentation

Jersey numbers remain embedded in existing Player, Team, Career, entry and movement routes. Show `현재 등번호`, `등번호 이력`, `입단 시 등번호`, `등번호 변경`, `이적 후 새 등번호`, `사용 가능한 등번호`, `사용 중인 등번호` and `영구결번` as context requires. A controlled request submits one C47 command; unavailable/in-use/retired choices return a Korean reason and valid alternatives without partially changing affiliation or history. Do not add a permanent standalone number-management route.

Existing Player Start adds `신인만 보기`, `드래프트 연도`, 구단, 포지션 and 지명 라운드 filters. An imported completed result is labeled historical/complete and displays `지명 순번`, `출신 학교`, drafting/signed club and initial number where available. Selecting it binds the same PlayerId and begins at the configured start date; it never shows draft rerun controls. Newcomers/Draft and Career views distinguish imported locked history from generated save history without suggesting the imported player's real-world future is predetermined.

## 14. Club Finance Lite presentation

C48 appears only as a public read model in existing Team Profile, Contract/FA, League and relevant history routes. The current summary may show `구단 연봉 총액`, `샐러리캡 산정액`, `샐러리캡 기준액`, `샐러리캡 초과 현황`, `선수 영입 예산`, `예상 연봉 부담`, `스폰서 수익`, `시즌 성과 보너스` and a qualitative `구단 재정 현황`. Always keep total payroll, cap payroll/threshold and internal budget visually distinct. A financial reason may explain an offer limit without exposing private Club AI scores or implying that available cap room equals available cash.

Views are read-only: no budget sliders, sponsor-negotiation buttons, accounting statements, owner controls or GM actions. C48 club sponsorship is labeled separately from the player's C38 advertising/equipment sponsorship. Fictional assumptions or game-model estimates receive an explicit label/provenance; the UI does not present unavailable real KBO club budgets or sponsor amounts as verified facts. Opening/rebuilding a view cannot settle revenue, reassess history or consume RNG. FHD may stack the summary into compact cards while preserving every value and explanation available at QHD.
