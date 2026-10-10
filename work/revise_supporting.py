from pathlib import Path
import re

ROOT=Path(r'E:\Madegame\Star_player')

def read(name): return (ROOT/name).read_text(encoding='utf-8-sig')
def write(name,text): (ROOT/name).write_text(text,encoding='utf-8',newline='\n')
def change(text,old,new):
    assert text.count(old)==1,(text.count(old),old[:90])
    return text.replace(old,new)

s=read('docs/SYSTEM_CONTRACTS.md')
s=change(s,'Product authority remains the two source documents and direct master request.', 'Product authority includes G/U, the original master request and the latest [owner revision R](../sources/ROADMAP_REVISION_REQUEST.txt), which takes precedence on changed decisions. [Portrait specification P](../Star_Player_Portrait_System_Design.MD) remains its own unchanged Source of Truth.')
s=change(s,'Required history/stat facts commit with their causes; optional prose is asynchronous.', 'Required history/stat facts commit with their causes; expressive prose is asynchronous. Club/Manager AI means rule-based C# policies. C43 is the sole validated inbound Gemini semantic path; generated prose/images never become commands. Recorded accepted interpretations are external command inputs for deterministic replay, not fresh LLM calls. All actual player-facing text is Korean.')

def add(cid, field, extra):
    global s
    pattern=rf'(^## {cid} — .*?\n- \*\*{re.escape(field)}:\*\* )([^\n]+)'
    m=re.search(pattern,s,re.S|re.M)
    assert m,(cid,field)
    s=s[:m.start(2)]+m[2]+' '+extra+s[m.end(2):]

add('C02','Responsibility','`CatcherRatings` holds Receiving/Framing, Blocking, Pop Time and Game Calling independently of C proficiency; shared Fielding/Arm/Endurance remain separate inputs.')
add('C02','Inputs','Catcher tools, C proficiency and C41 pair context use documented bounded effective modifiers. Pop Time is a 0–100 higher-is-better skill; raw observed seconds have a separate unit.')
add('C02','Outputs','Self catcher values may be exact; NPC tools and C proficiency use approved observation projections, never hidden truth.')
add('C02','State ownership','No composite CatcherRating/Caught Stealing Ability, and chemistry/Game Calling cannot permanently boost pitcher Stuff or other base ratings.')
add('C02','Testing','Excellent catcher tools/low C familiarity and average tools/high C familiarity; modifier caps, pitch-profile framing disabled where required, unchanged base ratings under chemistry/fatigue.')
s=change(s,'## C03 — Player state and workload (Phases 5, 7–10, 19–22)','## C03 — Player state and workload (Phases 2, 5, 7–10, 19–22)')
add('C03','Inputs','Actual catcher defensive exposure, repeated starts and rest from C08; no workload from a nominal C position alone.')
add('C03','State ownership','Catcher load is dated biological workload shared across all squads and roles; receiving work cannot be counted twice as separate fielding bodies.')
add('C03','Testing','Catching generally exceeds ordinary fielding load under declared tuning; repeated starts cause temporary fatigue, rest restores effective performance, base ratings remain unchanged.')
add('C04','Testing','Catcher skills grow independently from C proficiency and pair familiarity.')
add('C05','Inputs','Catcher drill/game exposure separately trains the four catcher skills and C proficiency within the same finite time budget.')
add('C05','Testing','C→another position→C preserves prior C proficiency; actual represented camp/bullpen exposure can be forwarded to C41 once.')
add('C06','Inputs','Persistent ManagerProfile and ClubPhilosophy weights drive bounded rule-based scores, not generative-AI decisions.')
add('C06','State ownership','Staff-bound ManagerProfile: veteran/prospect preference, offensive aggression, defense/catcher-defense preference, starter leash, bullpen aggression, trust sensitivity, development patience. Club-bound ClubPhilosophy: development/win-now focus, FA/trade aggression, prospect protection, roster stability. Save policy/version and effective staff assignment.')
add('C06','Persistence','`manager_profiles`, `club_philosophies` and dated policy assignments survive reload/staff turnover; prior choices retain their policy evidence.')
add('C06','Testing','Different profile weights affect curated decisions within caps; identical state/seed/policy matches; manager replacement does not rewrite history. Market dimensions activate in C18/C19 only.')
add('C07','Inputs','For catcher selection: batting/defensive tools, C proficiency, health/condition/fatigue, scheduled-pitcher BatteryChemistry, manager profile, club philosophy and opponent/context; legality filters come first via C40.')
add('C07','Outputs','Personal/ace-specific catcher patterns emerge from bounded scoring; no hardcoded personal-catcher flag owns selection.')
add('C07','Testing','Ace-specific/veteran-rookie/established/newly traded battery scenarios; unhealthy or exhausted preferred catcher loses eligibility/utility; chemistry cannot override rest or create a friendship guarantee.')
add('C08','Inputs','Catcher tools/C proficiency, pitcher delivery/runner-control and pair chemistry are independent from runner speed/stealing context; GameRules govern allowed framing effects.')
add('C08','Outputs','Distinct PB/WP scorer evidence and earned-run effects, steal/caught-stealing attribution, catcher defensive participation and actual battery/catching exposure.')
add('C08','Testing','Mid-inning catcher change and shared overlap; WP/PB not merged; marginal-call framing only in eligible rule profiles; bounded blocking/Pop Time/Game Calling/chemistry influences with no base-rating inflation.')
add('C09','Dependencies','C40 RegularSeasonRules; C42 Inbox projects this same barrier rather than duplicating decisions.')
add('C09','Testing','An unresolved important interview can pause at its required boundary; a pending expressive article cannot. Expiry is an explicit recorded outcome/notice, never silent disappearance.')
add('C10','Dependencies','C40 RegularSeasonRules/PostseasonRules/AwardRules supply versioned definitions, not scene/service constants.')
add('C11','Inputs','Retained catcher WP/PB/defensive attribution and scorer evidence must support required metrics without inventing missing imported history.')
add('C12','Inputs','Catcher-specific and repeated-start workload from C03, without one-size-fits-all fielding load.')
add('C12','Testing','Catcher fatigue affects effective play and risk; repeated catching alone causes no permanent rating loss; first-care help never submits a care choice.')
for cid,area in [('C13','ContractRules'),('C14','DraftRules'),('C18','FARules'),('C19','ForeignPlayerRules and applicable roster/contract rules')]:
    add(cid,'Dependencies',f'C40 KBO Rule Engine owns {area}; persist the evaluated rule version.')
add('C18','Inputs','Saved ClubPhilosophy FA aggression/win-now/development/roster stability within bounded budgets and legal offers.')
add('C19','Inputs','Saved ClubPhilosophy trade aggression/prospect protection/roster stability within bounded policy scoring.')
add('C19','Testing','Transferred pitchers retain identity-based prior battery history and build new pair familiarity from actual exposure, not a global reset.')
add('C23','Outputs','A modest personal-familiarity growth input may be supplied to C41 only for a represented relationship; absence is neutral and high friendship never guarantees strong battery chemistry.')
add('C23','State ownership','Baseball pair familiarity remains C41, not a friendship edge or manager-trust value.')

def contract(cid,title,fields):
    global s
    body=f'## {cid} — {title}\n\n'+'\n'.join(f'- **{k}:** {v}' for k,v in fields.items())+'\n\n'
    pat=rf'^## {cid} — .*?(?=^## C\d{{2}} — |\Z)'
    assert len(fields)==10
    if re.search(pat,s,re.S|re.M): s=re.sub(pat,lambda _:body,s,flags=re.S|re.M)
    else: s=s.rstrip()+'\n\n'+body

contract('C26','Structured stories and interview context (Phases 5, 21)',{
'Responsibility':'Select eligible ordinary stories, apply defined ordinary choices, and prepare authoritative interview requests/context/consequence-rule categories; final interview interpretation belongs to C43 in Phase 26.',
'Inputs':'Confirmed world facts, personality/life/role, participants, cooldowns, ordinary choices and seeded event selection; reporter/topic intent and permitted interview targets/categories.',
'Outputs':'Pending decisions/legal ordinary options, validated ordinary effect batches and interview context. No predefined final interview-answer intent substitutes for free text.',
'State ownership':'Story/interview request instance, context, eligibility/cooldown/deadline/barrier and ordinary choice receipt; target domain values remain with their owners.',
'Dependencies':'Calendar barrier, C42 visibility surface and typed State/Relationships/Contracts/Life effects; C43 later validates free-text semantic input.',
'Public interfaces':'`EvaluateStories(StoryContext) -> StoryCandidates`; `ResolveChoice(DecisionId, ChoiceId) -> Result<ChoiceEffectBatch>` for ordinary stories; `CreateInterview(ConfirmedInterviewContext) -> Result<InterviewRequest>`.',
'Events':'`StoryTriggered`, `StoryChoiceResolved`, `DecisionResolved`, `InterviewRequested`; no fixed-choice InterviewIntentSelected event.',
'Persistence':'Definitions/rules from versioned data; story/interview context, participant IDs, pending barriers and cooldowns in SQLite; no fabricated historical interviews.',
'Failure cases':'Stale ordinary choice, contradictory predicates, absent participants, expired request, duplicate effects or interpreting arbitrary text before C43 exists.',
'Testing':'Ordinary deterministic/offline story choice; decision stop/resume and explicit expiry; interview context/rule-only fixtures before Phase 26; no premature interview consequences.'})
add('C27','Outputs','Game Archive and milestone progress queries in Phase 23 expose retained evidence/coverage, not a new simulation or quest system (C44).')
add('C27','Persistence','Retain original player interview answers/context and accepted interpretation/receipt references; absent past quotes stay absent.')

contract('C29','News and expressive Narrative AI (Phases 5, 26)',{
'Responsibility':'Render routine information procedurally and use Gemini for high-value articles and rich connected reporter/fan/SNS/community expression; factual authority remains Game Core.',
'Inputs':'Visibility-filtered confirmed facts, actual stored interview answer/quote references and committed consequences, importance, fictional voice/outlet, approved D5 boundary and request budget.',
'Outputs':'Validated/escaped cached Korean narrative bundles, or explicit pending/retry/failure status. AI-native fan/SNS reactions need no template equivalent and prose never returns gameplay commands.',
'State ownership':'Presentation requests/member status/cache and content metadata only; C43 owns validated interpretation processing, C26/domain owners own facts.',
'Dependencies':'Public fact builder, routine template renderer, Gemini adapter, typed link/reference validation, C43 completion facts and source interviews.',
'Public interfaces':'`BuildPublicFacts(EventId, ViewerContext) -> PublicFactBundle`; `RenderNarrative(PublicFactBundle, PresentationPolicy) -> NarrativeResult`; requests include career/branch/session/fingerprint and actual quote IDs.',
'Events':'Presentation `NarrativeReady`/`NarrativeFailed`; generated expression alone emits no authoritative relationship, injury, result or other domain event.',
'Persistence':'Bundle members/text/fact/answer references/model/prompt/policy/locale/status in cache; reopening uses accepted cache. API secrets stay outside saves/git/logs. A failed member retry preserves completed members.',
'Failure cases':'Unsupported facts/quotes, malformed schema, prompt/markup injection, hidden prompt data, timeout/quota, stale branch response or duplicate generation. Withhold rejected content and offer retry; never invent a successful AI result.',
'Testing':'Zero low-value routine calls; rich expression and voice diversity grounded in supplied facts; false injury/contract/relationship/past-quote adversaries; wrong-save callbacks; cache reuse; post-commit failure cannot reapply interview effects.'})
add('C33','Persistence','Preserve at least three completed rolling autosave generations, paired pre-decision recovery until successful post-decision save, explicit recovery notices and copy-before-migrate; preferences cannot weaken this floor.')
add('C33','Testing','Interview apply receipt/answer round-trip and portrait missing-asset fallback do not weaken commit/snapshot fault-injection tests.')
add('C34','Dependencies','C40 rule-area validation and C45 cosmetic asset mapping; original P remains the portrait design authority.')
add('C34','Testing','Catcher/rule/profile defaults, real-photo precedence, Seed/AI references and missing-image fallback without changing player facts.')
add('C35','Outputs','C44 comparisons reuse these exact visibility projections; C45 supplies cosmetic portraits without granting hidden ability data.')
add('C35','Testing','Self-vs-NPC and NPC-vs-NPC comparisons, inaccessible private sort keys, favorites never alter visibility.')

contract('C40','KBO Rule Engine (Phases 1–3, 6, 9–16)',{
'Responsibility':'Single conceptual owner of typed, data-driven/versioned GameRules, RegularSeasonRules, RosterRules, RegistrationRules, PostseasonRules, DraftRules, ContractRules, FARules, ForeignPlayerRules and AwardRules; no monolithic service or extra process.',
'Inputs':'Validated RuleProfile area snapshots, edition/effective dates, relevant typed gameplay/eligibility context; D2-approved production rules.',
'Outputs':'Resolved GameRuleSnapshot or area-specific typed evaluation/constraints/reason codes with source version; missing/unsupported versions are explicit errors.',
'State ownership':'Rule definitions/profile composition and immutable career rule snapshots; consumers own resulting state transitions.',
'Dependencies':'Domain invariants, datapack validation/freeze and save compatibility; no UI/HTTP/live-rule refresh.',
'Public interfaces':'`ResolveGameRules(RuleProfileId, GameContext) -> Result<GameRuleSnapshot>`; typed area operations such as `EvaluateRegistration(RegistrationContext, RegistrationRules) -> EligibilityResult`; avoid untyped catch-all rule scripting.',
'Events':'Consumers emit factual effects with evaluated rule version; merely reading rules emits no gameplay event.',
'Persistence':'`rule_snapshots` stores area version/content hash/effective dates; saves freeze rules and migration preserves prior behavior or explicitly versions compatibility. No hardcoded 2026 values scattered in domain classes.',
'Failure cases':'Conflicting areas, unsupported edition, missing rule subset, live pack replacement, duplicated service-side eligibility constants or unapproved real-world assumptions.',
'Testing':'Area validation/version lookup in Phase 1; legality in 2; differing rule-profile fixtures per consuming phase; save/load and original-pack edit independence; D2 evidence at affected gates.'})
contract('C41','Battery Chemistry (Phases 1, 3, 7–9, 21)',{
'Responsibility':'Maintain baseball-specific pitcher-catcher familiarity and provide bounded effective coordination modifiers; independent of personal friendship, manager trust, C proficiency and base skills.',
'Inputs':'Role-ordered pitcher/catcher IDs, actual shared game exposure, represented camp/bullpen work, repeated-season/high-pressure evidence, elapsed inactivity and optional modest represented-relationship growth modifier from Phase 21.',
'Outputs':'Pair-specific progression delta and bounded communication/receiving/blocking/Game Calling/execution-stability context; public projection follows observation policy, never private skill disclosure.',
'State ownership':'`catcher_id`, `pitcher_id`, bounded chemistry, `shared_appearances`, `last_battery_date`, update cursor/exposure receipts/tuning version; no global chemistry field on catcher.',
'Dependencies':'Actual C08/C03 exposure, C05 practice, rule/tuning snapshot and date; C23 modifier is optional and neutral until implemented, not a new NPC friendship graph.',
'Public interfaces':'`GetBatteryContext(BatteryPairId) -> BatteryContext`; `ApplyBatteryExposure(BatteryExposure) -> Result<BatteryDelta>`; `UpdateInactivity(BatteryDateContext) -> BatteryDelta` under documented slow-decay tuning.',
'Events':'`BatteryExposureRecorded`, `BatteryChemistryChanged`; historical personal-catcher patterns may be derived later, not commands setting an inviolable pairing.',
'Persistence':'Unique `(catcher_id, pitcher_id)` row and pair/source exposure receipt; retain dated metadata across transfers/retirement where history references it. Missing historical pairs initialize neutral/unknown without invented appearances.',
'Failure cases':'Duplicate/shared game counted twice, same-player pair, global catcher score, excessive decay/churn, friendship automatically setting elite chemistry, or permanent Stuff/base-rating inflation.',
'Testing':'One catcher/three pitchers differ; exact overlap attribution and repeated exposure idempotency; camp/game/pressure/slow-decay caps; save replay; base ratings unchanged; emergent Phase 9 pairings respect health/fatigue.'})
contract('C42','Pending Decisions, contextual help and settings (Phases 5–6; later content)',{
'Responsibility':'Expose existing authoritative barriers in a compact Korean Inbox and supply nonintrusive contextual help and capability-driven settings; no email simulator or forced tutorial campaign.',
'Inputs':'Persisted decision/status/deadline/stop reason, confirmed first-time events, metric/rule glossary keys, implemented capability flags and user preferences.',
'Outputs':'`PendingDecisionView` plus count/routes, notices/hover-or-focus tooltips/reopenable glossary, valid settings UI; ordinary notifications remain distinct from unresolved choices.',
'State ownership':'Presentation preferences only: help seen/suppressed/reset, display/text scale/speeds/default modes/confirmation/allowed autosave options. Decision facts and expiry stay in C26/subsystem owners; audio/Gemini/portrait controls exist only with the feature.',
'Dependencies':'Calendar/query/command boundary, authoritative decision owners, versioned rule descriptions and UI routing; C43 supplies AI-interview states later.',
'Public interfaces':'`GetPendingDecisions(ViewerContext) -> PendingDecisionView[]`; `OpenDecision(DecisionId) -> Route`; `GetHelp(HelpTopic, PublicContext) -> HelpView`; `UpdatePreferences(PreferenceChange, SettingsCapabilities) -> Result<UserPreferences>`.',
'Events':'Help/settings/navigation notifications are presentation-only; resolving/expiring a decision still uses its existing domain command and receipt.',
'Persistence':'Versioned local preferences scoped to user/career where appropriate, outside simulation state/hash; Inbox queries durable decisions. Closing UI cannot accept/expire a decision. Preference corruption resets safely without modifying a career.',
'Failure cases':'Silent disappearance on advance, hidden important interview, duplicated Inbox truth, missing reopen/reset, unavailable settings, or autosave controls disabling mandatory snapshots/three-generation minimum.',
'Testing':'Contract/FA/care/national/military/interview items as owners ship; deadline/explicit expiry and reload parity; first injury/second-team/FA/developmental notices; metric help; suppress/reset/reopen; QHD/FHD Korean accessibility and no RNG/state change.'})
contract('C43','AI-native interview interpretation and core consequences (Phases 21, 26)',{
'Responsibility':'Accept player free text, validate untrusted structured Gemini meaning, and coordinate Game Core-owned consequences exactly once; generated narrative prose is not interpreted as commands.',
'Inputs':'C26 confirmed request/context, original Korean answer/revision/hash, finite allowed target/topic/tone/intensity/sentiment/tags/references, provider envelope bound to career/branch/session/interview/fact/schema/rule versions.',
'Outputs':'Validated interpretation or rejected/pending/retry state; core-computed effect batch and atomic application receipt; confirmed context/original answer/committed consequences for C29 expressive reactions.',
'State ownership':'Interview answers, request identity/status, accepted meaning and apply-once receipt; actual relationship/reputation values belong to their existing core owners and cannot be chosen numerically by Gemini.',
'Dependencies':'C26 rules/context, strict Application validator, Infrastructure provider, command queue/unit of work, public fact builder, domain effect owners and C42 Inbox.',
'Public interfaces':'`SubmitInterviewAnswer(InterviewId, AnswerRevision, RawText) -> Result<PendingInterpretation>`; `ValidateAndApplyInterview(InterpretationEnvelope) -> Result<InterviewOutcome>`; `RetryInterview(InterviewId, AnswerRevision) -> Result<RequestStatus>`.',
'Events':'`InterviewAnswerSubmitted`, `InterviewConsequencesApplied`, `InterviewCompleted` plus core-owned effect events after atomic commit; validation/API failure has no authoritative consequence event.',
'Persistence':'Preserve raw submitted text/context; accepted interpretation/effects/receipt commit together. Unique applied interview per career branch forbids repeated effects across answer revisions. Unapplied edits invalidate old callbacks; applied answers are immutable history. Replay consumes saved meaning without an LLM; secrets are excluded.',
'Failure cases':'Unsupported tags/ranges/targets/factual references or numeric mutation fields, invented past quote, stale answer/fact/session, duplicate/concurrent application, timeout/no key/quota, input prompt injection. Remain visibly pending/retryable as appropriate with no fake template answer or partial mutation.',
'Testing':'Exact raw-answer retention; malformed/unsupported/invented references rejected; changed-save/answer callbacks rejected; retry/reload/race applies once; failed post-commit reaction does not repeat effects; recorded-meaning deterministic replay; secret/privacy audit and no interpretation before Phase 26.'})
contract('C44','Discovery QoL and historical read models (Phases 23–24)',{
'Responsibility':'Favorites/watchlist for players/prospects, two-player comparison, Game Archive and milestone progress over retained facts; no additional simulator or RPG quest system.',
'Inputs':'Viewer context, stable career/branch/player IDs, existing public player projections, retained game/stat/history/coverage and milestone definitions.',
'Outputs':'Scoped watchlist, visibility-safe comparison, filtered historical game pages and milestone current/target/remaining/availability views.',
'State ownership':'Watchlist is user preference data, never simulation truth; archive/comparison/progress are rebuildable read models and navigation state.',
'Dependencies':'C11/C27/C35/C36, common routes/links and C42 preferences; no direct hidden-rating or true-potential access.',
'Public interfaces':'`SetFavorite(PlayerId, CareerScope, bool) -> PreferenceReceipt`; `ComparePlayers(PlayerId, PlayerId, ViewerContext) -> PlayerComparisonView`; `QueryGameArchive(GameArchiveQuery) -> GamePage`; `GetMilestoneProgress(PlayerId, Scope) -> MilestoneProgress[]`.',
'Events':'No gameplay events for viewing/bookmarking/comparing; C27 emits actual milestone occurrence once when facts cross a threshold.',
'Persistence':'Favorites in separately versioned preferences scoped by career/branch; query indexes/cache carry source revision. Archive uses existing retained score/box score/lineup/scoring progression/own appearances/important events and marks unavailable detail.',
'Failure cases':'Favorites leaking across same PlayerId in different saves, comparison revealing NPC truth, fabricated historical detail, double-counted career baselines, negative remaining count or unknown history shown as zero.',
'Testing':'Self/NPC and NPC/NPC comparison privacy, bookmark/reload/isolation, season/date/team/postseason/own-appearance filters, archive read-only hash, 1,873→127 hits and 487→13 HR progress, surpassed and incomplete totals.'})
contract('C45','Portrait reference and application boundary (Phases 24, 26–29)',{
'Responsibility':'Integrate the independent [P specification](../Star_Player_Portrait_System_Design.MD) into common profiles: offline Seed 2D base first, then optional explicitly user-selected Gemini images. P owns portrait feature detail.',
'Inputs':'PlayerId/save scope, unique cosmetic seed and renderer/part-set version, public appearance descriptors, club/national/neutral context, supplied photo and explicit user target/generate/regenerate choice.',
'Outputs':'Local rendered/cached portrait with Seed/neutral fallback, generation confirmation/status and per-player results; no gameplay facts or hidden-rating payload.',
'State ownership':'Cosmetic seed/mode/path/context metadata and P request/model/prompt/time/status fields; image assets scoped to career/branch; face identity separate from kit/context. Seed data survives AI use/deletion.',
'Dependencies':'C35 profiles/C34 asset validation, separate cosmetic allocator (no gameplay RNG), existing safe asset publication and Phase 26 Gemini image capability; P §34 local steps map to roadmap 24 then 26 then 28–29.',
'Public interfaces':'`ResolvePortrait(PlayerId, PortraitContext, CareerScope) -> PortraitView`; `RequestPortraits(ExplicitPortraitSelection) -> Result<PortraitJob>`; `RetryFailedPortraits(JobSelection) -> Result<PortraitJob>`; `RestoreSeedPortrait(Selection) -> Result<PortraitPreference>`.',
'Events':'Presentation-only status notifications; no automatic calls on games, awards, selection, aging, profile open or save load. Narrative request eligibility never authorizes image generation.',
'Persistence':'Keep unchanged P and save seed/renderer version plus local immutable asset references; WebP cache keyed by safe PlayerId/context and save branch, no names as keys. Publish/validate image before reference commit, preserve old assets and never store API keys in saves/git/logs.',
'Failure cases':'Missing/corrupt AI file→Seed; missing club/national asset→neutral; partial batch maps per player and retries only failures; unsupported Batch/provider is an explicit incomplete subgate, not silent automatic alternative calls.',
'Testing':'P seed/context/mode/fallback cases in 24; explicit single then Batch/cost/large-selection confirmation, 47/3 partial results, no automatic API, restart/cache reuse, same-ID save isolation and prior-image preservation in 26; import precedence and QHD/FHD polish in 27–29.'})
write('docs/SYSTEM_CONTRACTS.md',s)

s=read('AGENTS.md')
s=change(s,'The direct request in `sources/MASTER_REQUEST.txt` defines the assignment.', 'The original `sources/MASTER_REQUEST.txt` and latest `sources/ROADMAP_REVISION_REQUEST.txt` define the assignment; the latest explicit owner revision overrides conflicting earlier requirements.')
s=change(s,"The owner explicitly excluded `Star_Player_Portrait_System_Design.MD` from this roadmap on 2026-10-05. Its presence in the workspace does not authorize Seed 2D or Gemini image/batch work. Do not revise the supplied original files silently.", '`Star_Player_Portrait_System_Design.MD` is now an approved separate Source of Truth, superseding the earlier exclusion. Preserve it unchanged and reference it: Seed 2D/profile base in Phase 24, explicit user-selected Gemini single/batch enhancement in Phase 26, final polish in 28–29. Its local phase labels do not renumber ROADMAP. Source documents do not authorize production work during this revision.')
s=change(s,'- Godot 4.x stable .NET, C#, Control, SQLite, JSON and optional Gemini text presentation. No replacement stack.', '- Godot 4.x stable .NET, C#, Control, SQLite, JSON and Gemini narrative/AI-native interaction plus optional selected portraits. No replacement stack.')
s=change(s,'- One controlled player. Team operations belong to AI. Stable IDs, not display names, join all records.', '- One controlled player. Club/Manager AI is bounded deterministic/seeded rule-based C#, never an LLM. Save ManagerProfile/ClubPhilosophy and versioned KBO Rule Engine profiles. Stable IDs, not names, join records.')
s=change(s,'- Base skills, transient state, hidden development profile, proficiency, team role, manager trust and personal relationships are separate.', '- Base skills, transient state, hidden development profile, proficiency, team role, manager trust and personal relationships are separate. Catcher tools differ from C proficiency; BatteryChemistry belongs to each pitcher-catcher pair, affects effective performance only and is not friendship.')
s=change(s,'- AI receives only public confirmed facts, cannot issue gameplay commands, and always has local fallback. Keys are excluded from saves/git/logs.', '- Gemini receives privacy-safe confirmed facts/context; raw prose never becomes commands. C43 alone validates finite interview meaning before core rules compute and atomically apply consequences once. Preserve raw answers and accepted interpretations; deterministic replay uses those records, never a new model reply. AI-native interview/fan/SNS features may remain pending/retryable without template equivalents; failures apply no partial effects. Keys stay outside saves/git/logs.\n- Portraits retain Seed/neutral fallback and local cache; only explicit user generate/regenerate actions may call the image API. Missing portraits cannot prevent core simulation/load.\n- Keep Pending Decisions visible across navigation/load/advance; contextual help is suppressible and reopenable. Settings appear only with implemented capabilities and cannot weaken mandatory recovery snapshots or the three-generation floor.')
s=change(s,'- Official layouts: 2560×1440 and 1920×1080 only. Preserve functionality at FHD; no blanket text shrink.', '- Official layouts: 2560×1440 and 1920×1080 only. All actual player-facing UI/help/messages/generated text remain Korean; English roadmap/type names are technical notation. Preserve functionality at FHD; no blanket text shrink.')
s=change(s,'or excluded portraits.', 'or portrait features outside the approved P specification.')
s=change(s,'the roadmap\'s affected owner decisions require explicit resolution.', "the roadmap's affected pending owner decisions require explicit resolution. D5 is already approved; only D1/D2/D3/D4/D6 remain pending")
write('AGENTS.md',s)

s=read('CURRENT_STATE.md')
s=change(s,'Architecture/roadmap preparation complete as a proposal.', 'Targeted owner roadmap revision R1 complete as documentation; latest decisions are recorded while unresolved choices remain explicit.')
s=change(s,'39 system contracts','45 system contracts')
s=change(s,'and unchanged source copies.', 'and unchanged G/U/master/R/Portrait source copies. P remains independent; the 0–29 numbering, order and dependency edges are preserved.')
s=change(s,'D1–D6 at the end of ROADMAP gate only the relevant later product decisions: action cadence, rule editions/fidelity, 2026 data cutoff, career-entry/free-text policy, strict generated-text integrity and historical detail retention.', 'D1/D2/D3/D4/D6 gate only relevant later choices: action cadence, rule editions/fidelity, 2026 data cutoff, career-entry/free-text traits and historical-detail retention. D5 is approved: expressive Narrative AI plus validated interview semantics with core-owned consequences. The original raw interview/receipt records remain retained independently of optional prose pruning.')
s=change(s,'and optional Gemini fact presentation.', 'the explicit KBO Rule Engine, independent catcher/C proficiency and pair chemistry, rule-based manager/club policies, and Gemini expression with one validated AI-native interview bridge. Seed/profile base is Phase 24; optional selected Gemini portraits are Phase 26.')
s=change(s,"The portrait document present in `E:/Madegame/Star_player` is excluded by the owner's 2026-10-05 instruction.", 'Portrait design is approved by the latest revision and preserved unchanged in the package. Gemini text/image/Batch capability versions must be verified in Phase 26; no present API/model/pricing capability is claimed by this roadmap. No addition required a new top-level phase.')
s=change(s,'Give Claude Code Phase 0 using the ready-to-copy handoff in ROADMAP §9.1.', 'Review the revised documentation package. After separate authorization to implement, give Claude Code Phase 0 using the ready-to-copy handoff in ROADMAP §9.1. This revision does not begin Phase 0.')
s=s.rstrip()+'\n- `sources/ROADMAP_REVISION_REQUEST.txt`\n- `Star_Player_Portrait_System_Design.MD`\n'
write('CURRENT_STATE.md',s)

s=read('docs/UI_ARCHITECTURE.md')
s=change(s,"The additional portrait-design file discovered in the new workspace is **excluded**, following the owner's 2026-10-05 instruction to use only the original two documents. Existing datapack photo support and fallback assets remain in scope. No seeded portrait generator or Gemini image/batch workflow is introduced.", 'The latest [owner revision R](../sources/ROADMAP_REVISION_REQUEST.txt) controls changed UX decisions. [Portrait specification P](../Star_Player_Portrait_System_Design.MD) is approved and remains independent: Phase 24 Seed/profile base, Phase 26 explicit selected Gemini image workflow, Phase 28 final polish. Do not duplicate its specification here. All player-facing UI, help, questions, notifications and generated content are **Korean**; English route/component names below are developer identifiers only.')
s=change(s,'`Player | POS | G | AVG | HR | OPS | WAR`','`선수 | 포지션 | 경기 | 타율 | 홈런 | OPS | WAR`')
s=change(s,'No new top-level Player DB.', 'No new top-level Player DB. Korean labels include 홈, 내 선수, 경기, 일정, 훈련, 기록, 구단, 리그, 계약, 자산, 생활, 인간관계, 뉴스, 커리어; League subroutes use 선수 DB, 구단 목록, 신인 / 드래프트. Shell actions use 의사결정 대기함, 도움말 and 설정.')
s=change(s,'| AppShell / PlayerLink / Notification / ChoiceModal | 5 | Stable routes, factual links, priority, focus and explicit command submission |', '''| AppShell / PlayerLink / Notification / ChoiceModal | 5 | Stable routes, factual links, priority, focus and explicit command submission |
| PendingDecisionList / HelpTooltip / HelpPanel / SettingsPanel | 5–6 | C42; one authoritative barrier projection, first-time help/suppression/reset and real capability settings |''')
s=change(s,'| NewsCard / ArticleView | 5 templates; 26 complete | Public facts/escaped text, typed links and fallback |', '''| NewsCard / ArticleView / NarrativeBundleView | 5 routine templates; 26 complete | Confirmed facts, actual quote references, expressive Korean voices and pending/retry; no universal AI-native fallback |
| InterviewAnswerPanel | 26 | C43; reporter question, free-text original answer, submit/status/retry; no predefined final answer choices |
| FavoriteToggle / PlayerComparison / GameArchive / MilestoneProgress | 23 read models; 24 UI | C44; preferences/public projections/retained facts only |
| PortraitView / PortraitSelection | 24 base; 26 AI | C45; P-defined Seed/context/fallback and explicit single/batch workflow |''')
s=change(s,'| ChoiceEvent | Fixed legal choices, confirmed participants, only intended outcome hints, priority and pending state | C26 + owner command | 5 / 21 | U19/27–32 |','''| ChoiceEvent | Ordinary story/legal choices and confirmed participants; interview answers use the separate free-text route | C26 + owner command | 5 / 21 | U19/27–32; R16 |
| PendingDecisions / 의사결정 대기함 | Count, subject/type, due date/status, blocking reason and route to authoritative contract/FA/care/national/military/interview action; explicit expiry notices | C42 + decision owners | 5 / 6; later types on arrival | R4 |
| Help / 도움말 | First-time notices, hover/focus explanations, glossary, do-not-show-again, reset and reopen | C42 | 5 / 6 framework; later feature topics; 28 polish | R5 |
| Interview / 인터뷰 | Reporter question, free-text answer, preserved text, submit/loading/pending/retry and resulting reactions; no false completed answer on API failure | C43/C29/C42 | 21 context only / 26 interaction | R13–17 |
| Favorites / 관심 선수 | Bookmark players/prospects in Player DB; scoped preferences, no changed simulation | C44 | 24 / 24 | R10 |
| PlayerComparison / 선수 비교 | Two selected players; self exact current skills, NPC observed estimates, unavailable fields explicit | C44/C35 | 24 / 24 | R10 |
| GameArchive / 경기 기록 보관함 | Records/Career filters by season/date/team/postseason/own appearance; retained score/box score/lineup/progression/important events with coverage | C44/C27 | 23 data / 24 UX | R10 |
| MilestoneProgress / 기록 달성 현황 | Current/target/remaining on Career; no quest rewards or invented totals | C44/C27 | 23 data / 24 UX | R10 |
| PortraitProfile / PortraitSelection | Common profile Seed display; later explicit single/batch Gemini targets, count/cost confirmation, per-player retry/Seed restore | C45 | 24 base / 26 AI; 28 polish | R11; P16–21/34 |''')
s=change(s,'| Settings | Supported viewport/window mode, audio/display, optional Gemini connection/budget, data management | Preferences, Narrative, Pack | 4 basic / 28 | U34/46; master76 |','| Settings / 설정 | Implemented display/window/scaling/speeds/modes/confirmation/autosave/help; later audio, Narrative/Portrait AI configuration and data management | C42/C29/C45/C34 | 5–6 framework / 28 complete presentation; audio when 29 implements it | U34/46; R6/19 |')
s=change(s,'- **Priority A:** Immediate decisions/information: injury, contract/FA, call-up/demotion, national selection, military, retirement.', '- **Priority A:** Immediate decisions/information: injury, contract/FA, call-up/demotion, national selection, military, important interview, retirement.')
s=change(s,'Optional AI error keeps template text available.', 'Routine news remains available independently. AI-native interview/fan/SNS failures show Korean pending/retry states without pretending that a template is an AI result; an already-applied interview consequence is never repeated when reactions retry. Portrait errors retain the separate Seed/neutral image fallback.')
s=s.rstrip()+'''

## 9. Early UX ownership and later capability content

Phase 5–6 establishes C42 once. Inbox shows the same persisted decisions that stop advancement; closing a popup or entering a profile leaves the item pending. Sort by urgency/date using public metadata, retain explicit resolved/expired notices, and expose the reason advancement stopped. It is a compact action surface, not an email simulation. Each owner adds its type when implemented, including Phase 8 care, Phase 14 contract, Phase 15 FA, Phase 18 national, Phase 19 military and Phase 26 important interviews.

Contextual help never executes an action. First injury explains care/recovery; first second-team assignment explains registration vs level; first FA eligibility uses the saved rule edition; developmental registration clarifies terminology; advanced metric hover/focus opens the formula/coverage explanation. Use public data, skippable first-time notices, persistent do-not-show-again and a reopenable/searchable glossary/help panel. Reset changes presentation preferences only.

Settings are capability-driven, with independently versioned user preferences rather than duplicated domain rules. Phase 5–6 exposes QHD/FHD behavior, supported window/borderless/fullscreen, readable UI text/font scaling, implemented game/text/animation speeds, implemented default game modes, confirmation preferences, permitted autosave cadence and help preferences/reset. Missing later modes/audio/providers are absent, not fake toggles. Core-required pre/post recovery and at least three completed autosaves cannot be disabled by a convenience setting. Test focus/overflow at both resolutions and the supported text scales. Phase 26 adds actual Gemini narrative/image configuration; Phase 28 completes styling/accessibility; Phase 29 adds audio controls only with audio.

## 10. Free-text interviews and expressive reactions

Phase 21 has confirmed requests/topics/rules only; no predefined final answer intent. In Phase 26, show a Korean reporter question and editable free-text response. Preserve exact submitted text/version; disable duplicate submit while awaiting interpretation, permit retry without duplicate consequences, and make unapplied edits invalidate old requests. Only C43's strictly validated semantic envelope reaches core rule mapping. Show consequences only from committed core outcomes, then linked reporter/media/fan/SNS voices grounded in supplied facts and that exact answer.

Distinguish factual claims/quotes from fictional opinion, humor and labeled speculation. A past quote links the actual stored interview; never fabricate text attributed to an older interview. Cache accepted bundle members; failed reactions remain retryable without undoing/reapplying consequences. A required unresolved interview stays visible in Inbox and may block its relevant advance boundary; unrelated article generation does not. Defer/decline is shown only when the authoritative request allows it. No-key/timeout/quota/invalid-output states preserve answer/save/load safety and do not insert a canned interpretation.

## 11. Catcher, QoL and portrait presentation

Player/Training views distinguish Receiving/Framing, Blocking, Pop Time, Game Calling, shared Fielding/Arm/Endurance and C proficiency. Self values follow normal exact-current access; NPC views/comparison retain observations/confidence. Team selection reasons may describe a familiar battery or catcher rest without exposing private policy scores or turning a pairing into a management button. Battery familiarity and personal friendship have separate labels and explanations.

Favorites remain scoped user preferences; comparison obtains two normal public projections. Archive/progress screens consume Phase 23 read models with no extra simulation. Unknown historical detail is labeled unavailable, and record progress derives from the same reconciled totals as actual milestones. All nested views restore the caller's game/filter/scroll state.

Portrait visuals/actions follow P without replicating its design. Phase 24 introduces Seed/profile/mode/fallback, with stable face and contextual kit and no image API. Phase 26 adds explicit player selection and single/batch confirmation, visible per-player statuses, failed-only retry, regeneration and Seed restore. Show the number of targets/model/image count/cost estimate or explicit unavailable estimate; retain large-selection confirmation. Current provider capability/pricing is verified during implementation, never invented in UI. Opening a profile, switching contexts, changing teams, aging, awards or loading saves never initiates Gemini images. Keep portrait API controls absent until implemented.

Revision QA extends §8 with distinct catcher skill/proficiency privacy, pair vs friendship labels, unresolved Inbox persistence, all help/reset paths, capability settings, comparison/favorites/archive/progress, exact raw Korean interview retention, invalid/late/duplicate interpretation callbacks, independent reaction retries and P's seeded/batch/fallback cases. These remain planned tests, not completed runtime evidence.
'''
write('docs/UI_ARCHITECTURE.md',s)

s=read('docs/REQUIREMENTS_TRACEABILITY.md')
s=change(s,'and [M: master task](../sources/MASTER_REQUEST.txt).', 'and [M: master task](../sources/MASTER_REQUEST.txt), updated by [R: owner revision](../sources/ROADMAP_REVISION_REQUEST.txt). [P: Portrait design](../Star_Player_Portrait_System_Design.MD) is an independent approved source. R supersedes conflicting earlier choices; unchanged G/U/M/P copies preserve provenance.')
s=change(s,'Owner decisions D1–D6 preserve unresolved policy rather than silently choosing it.', 'D1/D2/D3/D4/D6 preserve unresolved policy; D5 records the newly approved factual-authority/expressive-AI boundary.')
s=change(s,'| R39 | G7; U19 | Conditional random stories using current facts/personality/wealth/role/health, predefined meaningful choices | C26 | 21 eligibility/cooldown/participant/idempotency/offline fixtures |','| R39 | G7; U19; R16 | Ordinary conditional stories retain meaningful predefined choices; final interview answers are free text, handled separately | C26/C43 | 21 ordinary eligibility/cooldown/idempotency/offline fixtures; 26 interview bridge |')
s=change(s,'| R48 | G8; U18/19 | Engine facts → importance → template/Gemini; major bundles/outlets/interviews with fixed effects | C26/C29 | 5 templates; 21 interview effects; 26 optional API and D5 integrity gate |','| R48 | G8; U18/19; R12–18 | Importance routing, expressive major bundles and free-text interviews; core owns consequences after validated interpretation | C26/C29/C43 | 5 routine templates; 21 context/rules; 26 free text/semantic bridge/reactions and approved D5 gate |')
s=change(s,'| R49 | G8; M10/75–76 | Minimal fact payloads, no hidden truth/unconfirmed events, caching/budgets/failure fallback | C29 | 26 adversarial output, offline identical world, no ordinary API calls |','| R49 | G8; M10/75–76 as amended by R15/17 | Privacy-safe context, factual authority, cache/budgets; designated AI-native features use pending/retry without universal template fallback | C29/C43 | 26 no partial effect, same recorded-input replay, no routine calls, unknown tags/facts/quotes rejected; failed reaction does not repeat effects |')
s=change(s,'| All 39 contracts |','| All 45 contracts |')
s=change(s,'## Explicitly deferred or excluded','''## Newly approved revision coverage

R section numbers below refer to the unchanged latest request, not the original master. Each row maps an owner decision to a phase and a decisive planned gate; none claims completed implementation.

| ID | Source | Approved change / boundary | Contract(s) | Implementation phase(s) and decisive evidence |
|---|---|---|---|---|
| R65 | R1 | Explicit KBO Rule Engine with typed versioned Game/RegularSeason/Roster/Registration/Postseason/Draft/Contract/FA/ForeignPlayer/Award areas | C40; consuming owners | 1 vocabulary; 2/3 game; 6 calendar; 9–16 remaining areas; version-switch fixtures and saved rule independence |
| R66 | R2/21 | Approved commit/snapshot/copy-migration/previous-valid/corrupt-latest/rolling-three/predecision/recovery-notice/fault-injection guarantees unchanged | C33 | 4 unchanged gate and §5.3; every persisted phase and 29 fault matrix; settings cannot weaken floors |
| R67 | R3 | Persistent ManagerProfile/ClubPhilosophy dimensions; bounded rule-based decisions, never LLM operations | C06/C07/C18/C19 | 9 distinct-policy/same-state-seed-policy replay and saved staff/profile fixtures; 15–16 market scoring |
| R68 | R4 | Compact visible Pending Decisions for contract/FA/care/national/military/important interview; no silently lost choice or email simulator | C42/C09/C26/C43 | 5–6 Inbox/barrier parity, navigation/reload and explicit-expiry notices; new types in 8/14/15/18/19/26 |
| R69 | R5 | First-time help, hover/focus tooltip, glossary/help, do-not-show-again and reopen/reset; no forced tutorial | C42 | 5–6 framework; 8 injury/10 second team/13 developmental/15 FA/23 metrics; 28 all topic/reset/focus cases |
| R70 | R6 | Early settings for actual capabilities; supported resolution/window/scaling/speeds/modes/confirmation/autosave/help, later audio/Narrative/Portrait AI | C42/C29/C45 | 5–6 valid settings and recovery floors; 26 provider settings; 28 final UX; 29 audio only when present |
| R71 | R7 | Four catcher skills, shared Fielding/Arm/Endurance and independent C proficiency; WP/PB/steal/defense/workload; bounded resolver/growth/fatigue | C02–C05/C08/C11/C12 | 1 tools-vs-proficiency/privacy; 2 scoring attribution; 3 factor/cap/framing-profile tests; 7 training/return-to-C; 8 repeated-start temporary effects |
| R72 | R8 | Per-pitcher/catcher BatteryChemistry, actual shared exposure/metadata, bounded growth/slow decay; distinct from friendship and base skill | C41/C23 | 1 pair types; 3 neutral/bounded effect fixtures; 7 saved pair progression/idempotency; 21 modest represented-friendship growth only |
| R73 | R9 | Catcher selection considers tools/batting/C proficiency/health/state/load/pair/manager/club/context; personal catcher emerges | C06/C07/C41 | 9 ace/veteran-rookie/established/newly traded battery fixtures, rest/health overrides and no primary personal-catcher flag; 16 transfer continuity |
| R74 | R10 | Favorites/prospect watchlist preference, private-safe two-player comparison, retained Game Archive and milestone progress; no extra simulator/quest | C44/C27/C35 | 23 archive/progress coverage/reconciliation; 24 filters, self/NPC comparison privacy, bookmark scope and navigation/read-only hash |
| R75 | R11; P1–37 | Separate portrait source restored; offline default Seed, optional selected Gemini, local cache/Seed fallback, supplied photos and context variants | C45/C34/C35 | 24 P34 local steps 1–2; 26 steps 3–5 single/batch/confirmation/retry; 27 import; 28–29 polish; original P hash unchanged |
| R76 | R12 | Generative AI actively adds valuable immersion/expression, never owns baseball/ratings/world/team/market/authoritative relationship decisions | C29/C43; all core owners | 9 no LLM selection; 21 context; 26 narrative importance/authority/payload tests; routine information zero calls |
| R77 | R13/17 | Free-text interview → Gemini bounded meaning → schema/whitelist/core mapping → atomic consequences; preserve actual raw answer | C43 | 26 malformed/unsupported/mutation fields/false reference/stale callback rejected; apply once across retry/reload/race; exact original text retained |
| R78 | R14 | Connected reporter/media/fan/SNS/community voices use confirmed facts and actual answer; past quotes only from stored interviews | C29/C43/C27 | 26 contextual bundle/cache, factual-vs-opinion validation, fabricated past quote rejected and post-commit member failure isolation |
| R79 | R15/17 | AI-native interpretation and rich fan/SNS have no mandatory template equivalent; failures pending/retry, no partial mutation and safe load/save | C29/C42/C43 | 26 offline/key/quota/timeout/adversarial matrix; required interview visible, unrelated expression not a date barrier; 29 exported failure states |
| R80 | R16 | Phase 21 retains relationship/family/rivalry/story facts/interview context/consequence rules; removes final intent-choice interview assumption | C23/C24/C26 | 21 no final answer effects/predefined intent/LLM interpretation; ordinary stories still work; Phase 26 supplies final interaction |
| R81 | R18 | D5 approved factual authority vs rich fictional expression; not clause rearrangement; prose cannot create events | C29/C43 | §4.5/D5/26 authority and adversarial tests; recorded semantic-input replay preserves deterministic core |
| R82 | R19 | All actual UI, help/settings/notices/interviews/generated content remains Korean; technical identifiers/prose may be English | UI/C42/C29 | 5–6 and each screen; 24/26/28 Korean label/text/focus/overflow evidence at QHD/FHD |
| R83 | R20–23 | Consistent companion contracts/UI/traceability, no production/Phase 0, preserved 30 numbering/order/dependencies/privacy/save gates/exclusions | All | Documentation diff/structure/source/hash/preserved-save checks; phase cross-checks above; no new top-level phase |

## Explicitly deferred or excluded''')
s=change(s,'initial gameplay uses existing base skills/proficiency and rule mechanics. Not invented as mandatory extra attributes', 'initial gameplay uses existing base skills/proficiency and rule mechanics except the four catcher skills now explicitly approved by R7. No unrelated new stealing attribute is implied')
s=change(s,'| Seed portraits and Gemini image/batch | New workspace file, not original sources; owner explicitly excluded on 2026-10-05 | Excluded; existing photo import and fallback remain |','| Portrait features beyond approved P | R11 approves P and supersedes the earlier exclusion | Seed/profile base 24; optional explicitly selected single/batch Gemini 26; no automatic generation or new top-level phase |')
s=change(s,'D5: strict AI prose integrity.', 'D5: approved AI factual-authority/expressive-interaction policy (resolved by R, not pending).')
s=change(s,'39 ten-field contracts','45 ten-field contracts')
s=s.rstrip()+'\n\nRevision validation also compares phase IDs/order/dependency edges and the complete save commit/snapshot protocol against the previous package; verifies P/R/G/U/M source hashes; checks the requested phase additions and removal of obsolete interview/fallback/portrait exclusions. These checks supplement, not replace, author review of semantic boundaries.\n'
write('docs/REQUIREMENTS_TRACEABILITY.md',s)

s=read('sources/SOURCE_MANIFEST.md')
s=change(s,'Files here are unmodified copies of the supplied inputs;', 'Files here and the independent root Portrait document are unmodified copies of the supplied inputs;')
s=change(s,'## Technical references','''## Revision authority and independent Portrait source

| Key | File | Role | SHA-256 |
|---|---|---|---|
| R | [ROADMAP_REVISION_REQUEST.txt](ROADMAP_REVISION_REQUEST.txt) | Latest direct owner request; overrides conflicting earlier choices, including the earlier portrait exclusion and universal AI fallback | `6027C298C8510991A933DE8654027C7CDAE07794389CBFAB92A8F49977AC0876` |
| P | [Star_Player_Portrait_System_Design.MD](../Star_Player_Portrait_System_Design.MD) | Approved independent portrait Source of Truth; preserved unchanged, referenced rather than copied into ROADMAP | `D53DF3D253CEC20A62BF89F5B558AD5C737F4B6ED24E2AB0A342BB0E0EB94486` |

Original R path: `C:/Users/user/.codex/attachments/e69f6bf2-a508-4775-8ea3-dce34891a5c3/붙여넣은 텍스트.txt`. Original P path: `C:/Users/user/Downloads/Star_Player_Portrait_System_Design.MD`, explicitly reattached by the owner for this revision. R/P were read completely, alongside the entire existing roadmap and available supporting planning documents. P's internal Phase 1–5 are local portrait steps mapped into global Phases 24/26/28–29, not new global phase numbers.

## Technical references''')
s=change(s,'The workspace later changed to `E:/Madegame/Star_player`. Its `Star_Player_Portrait_System_Design.MD` was read for context but **excluded** after the owner explicitly chose “use only the original two documents” on 2026-10-05. It is not copied into the authoritative source set.', 'The workspace is `E:/Madegame/Star_player`. The earlier two-document-only decision is superseded by R11: the Portrait specification is now approved and included unchanged as a separate root reference. G/U/M remain unchanged historical inputs; R identifies the explicitly revised requirements. No source file has been silently edited.')
write('sources/SOURCE_MANIFEST.md',s)
print('Contracts, UI, traceability, AGENTS, current state and manifest revised.')
