from pathlib import Path
import hashlib, json, re

root=Path(r'E:\Madegame\Star_player')
old=(root/'work/revision-baseline/ROADMAP.md').read_text(encoding='utf-8-sig')
new=(root/'outputs/ROADMAP.md').read_text(encoding='utf-8-sig')
checks=[]
def check(ok, label):
    assert ok,label
    checks.append(label)
def section(text,start,end): return text.split(start,1)[1].split(end,1)[0]
def phases(text):
    return {int(m[1]):m[0] for m in re.finditer(r'^## Phase (\d+) — .*?(?=^## Phase |^## 9\. Implementation Handoff)',text,re.M|re.S)}
op,np=phases(old),phases(new)
check(list(op)==list(np)==list(range(30)),'All thirty phase IDs/order preserved')
for n in range(30):
    if n!=26: check(op[n].splitlines()[0]==np[n].splitlines()[0],f'Phase {n} title preserved')
check(section(old,'### 5.3 Commit and snapshot protocol','### 5.4')==section(new,'### 5.3 Commit and snapshot protocol','### 5.4'),'Complete save commit/snapshot protocol unchanged')
old_migrations=section(old,'### 5.4 Migrations and historical continuity','### 5.5')
new_migrations=section(new,'### 5.4 Migrations and historical continuity','### 5.5')
check(old_migrations.split('Datapack history imports',1)[0].strip() in new_migrations,'Original migration protocol retained')
for term in ['source, coverage interval, scope, metric method, and completeness','opening baseline','Imported six championships plus two simulated championships must display eight','flagged incomplete rather than fabricated']:
    check(term in new_migrations,f'Historical-continuity requirement retained: {term}')
check('D1–D6 are approved and recorded' in section(new,'### 6.4 Common Gate G','## 7.'),'Common Gate G records approved decisions')
check('working DB and every retained snapshot family' in section(new,'### 6.3 Performance protocol','### 6.4'),'D6 measurement added without removing long-soak sample requirements')
def edges(text): return re.findall(r'^\| (\d+) \| (None|[\d, ]+) \|',text,re.M)
check(edges(old)==edges(new),'All direct dependency edges preserved')
required={
1:['CatcherRatings','C proficiency','KBO Rule Engine','observed-NPC','StadiumId','ParkFactorProfile','JerseyNumberAssignment'],
2:['passed balls','wild pitches','caught-stealing','CatcherExposure'],
3:['Framing','Blocking','Pop Time','Game Calling','chemistry','base-rating','ParkFactorSnapshot'],
4:['DraftClassSource','ImportedDraftFact','JerseyNumberAssignment','all-or-nothing'],
5:['Pending Decisions','contextual help','settings','three-generation','Korean','신인만 보기','PlayerId','never reruns'],
6:['Inbox','RegularSeasonRules','help/settings','expiry','weather','StadiumId','rescheduled'],
7:['BatteryChemistry','C proficiency','Returning to C','slow inactivity','receipts'],
8:['catcher','repeated starts','temporary','unchanged base ratings'],
9:['ManagerProfile','ClubPhilosophy','BatteryChemistry','personal-catcher','rule-based','staff','captain','TeamAtmosphere','C47'],
10:['club-number','C47'],
11:['All-Star','weather','makeup','staff','achievement evidence','no dependency on Phase 14/C48'],
12:['imported completed class','persist','reload never rerolls'],
13:['DraftClassSource','imported completed','generated live','Never branch on a literal calendar year','C47'],
14:['annual non-FA salary','agent','final decision','salary history','Club Finance Lite','SalaryCapRules','sponsorship','player budget','C48 payroll reconciles'],
15:['new-club number','C47','projected total payroll','cap charge','player budget'],
16:['released number','C47','incoming/outgoing salary','C48'],
17:['manager','stadium','salary','weather','All-Star','imported drafts','generated classes','club number','payroll growth','sponsor revenue distribution','struggling clubs'],
21:['InterviewContext','consequence','Phase 26','No `InterviewIntentSelected`','captain','TeamAtmosphere','previous manager'],
23:['Game Archive','MilestoneProgress','1,873','487','coverage','stadium','captain','manager tenure','All-Star','HistoricalDraftFact','JerseyNumberHistory','ClubFinancialHistoryFact'],
24:['watchlist','comparison','Game Archive','Seed 2D','no gameplay RNG','stadium','imported-rookie','등번호 이력','구단 재정 현황','read-only'],
25:['captain','C47','club-specific'],
26:['free-text','schema','whitelist','fan','SNS','raw','retry','stale','once','Batch','47 success/3 failure','pending'],
27:['optional','draft_history','initial jersey','no Game Core year special case','ClubFinanceBaseline','SalaryCapRuleDefinition','game-model estimates'],
28:['settings/help','Korean','autosave','audio'],
29:['ParkFactor','salary','staff','captain','weather','All-Star','imported/generated draft','C47','stable PlayerIds','C48 invariants','financially weak clubs can recover']}
for n,terms in required.items():
    for term in terms: check(term.casefold() in np[n].casefold(),f'Phase {n} revision coverage: {term}')
trace=(root/'outputs/docs/REQUIREMENTS_TRACEABILITY.md').read_text(encoding='utf-8')
check([int(x) for x in re.findall(r'^\| R(\d+) \|',trace,re.M)]==list(range(1,108)),'All 64 original requirement IDs retained plus 43 revision rows')
contracts=(root/'outputs/docs/SYSTEM_CONTRACTS.md').read_text(encoding='utf-8')
check([int(x) for x in re.findall(r'^## C(\d{2}) —',contracts,re.M)]==list(range(1,49)),'All 48 sequential contracts present')
for term in ['DraftClassSource','Imported','Generated','never a timeless Player field','F closed the committed gameplay feature scope']:
    check(term.casefold() in new.casefold(),f'R3 architecture coverage: {term}')
for decision in range(1,7):
    block=section(new,f'### D{decision} —',f'### D{decision+1} —') if decision < 6 else new.split('### D6 —',1)[1]
    check('**Status:** APPROVED.' in block,f'D{decision} approved')
check('D1, D2, D3, D4 and D6 are pending' not in new,'No owner decision remains pending')
check('preserve first, measure, then prune only if evidence requires it' in new,'D6 preserve-first policy')
check('current and previous season' not in new,'No two-season raw-pitch default')
for term in ['Club Finance Lite approved exception and renewed scope lock','C13 authoritative contract obligations','Total Player Payroll','Salary-Cap Payroll','Club Player Budget','Sponsorship income never changes that legal threshold automatically','Phase 11 owns baseball results and achievement facts and has no dependency on C48 or Phase 14']:
    check(term.casefold() in new.casefold(),f'Club Finance Lite architecture coverage: {term}')
for term in ['C13 alone owns player contracts/obligations','SponsorshipSettlement','GetClubFinancialSummary','Gemini, generated prose and wall-clock randomness are forbidden decision inputs']:
    check(term.casefold() in contracts.casefold(),f'C48 contract coverage: {term}')
for rel,source in [('Star_Player_Portrait_System_Design.MD',Path(r'C:\Users\user\Downloads\Star_Player_Portrait_System_Design.MD')),('sources/ROADMAP_REVISION_REQUEST.txt',Path(r'C:\Users\user\.codex\attachments\e69f6bf2-a508-4775-8ea3-dce34891a5c3\붙여넣은 텍스트.txt'))]:
    check(hashlib.sha256((root/'outputs'/rel).read_bytes()).digest()==hashlib.sha256(source.read_bytes()).digest(),f'Exact original bytes preserved: {rel}')
for path in [root/'outputs/ROADMAP.md',root/'outputs/AGENTS.md',root/'outputs/CURRENT_STATE.md',*list((root/'outputs/docs').glob('*.md'))]:
    if path.name=='SOURCE_SECTION_INDEX.md': continue
    text=path.read_text(encoding='utf-8')
    for obsolete in ['always has local fallback','Backend interview choices and relationship effects exist before','backend-defined interview choices','its additional portrait-design document is excluded','Optional Gemini narrative presentation','39 system contracts']:
        check(obsolete not in text,f'Obsolete policy absent from {path.name}: {obsolete}')
changed=[n for n in range(30) if op[n]!=np[n]]
check(set([1,3,6,9,11,14,17,21,23,24,25,29]).issubset(changed),'Every R2 target phase differs from the pre-revision baseline')
check(set([1,4,5,9,10,12,13,15,16,17,23,24,25,27,29]).issubset(changed),'Every R3 target phase differs from the pre-revision baseline')
check(set([2,3,4,5,13,17,21,23,24,27,29]).issubset(changed),'Every R4 target phase differs from the pre-revision baseline')
check(set([9,11,14,15,16,17,23,24,27,29]).issubset(changed),'Every R5 Club Finance Lite target phase differs from the pre-revision baseline')
result={'status':'PASS','checks_passed':len(checks),'changed_phases':changed,'phase_order_and_dependencies':'unchanged','save_protocol':'unchanged','owner_decisions':'D1-D6 approved','feature_scope':'re-locked after owner-approved Club Finance Lite exception','source_integrity':'exact original bytes','scope':'Documentation checks only; no production code or runtime tests'}
(root/'work/revision-validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
