param([string]$PackagePath)
$ErrorActionPreference = 'Stop'
if (-not $PackagePath) { $PackagePath = Join-Path (Split-Path -Parent $PSScriptRoot) 'outputs' }
$packageRoot = (Resolve-Path -LiteralPath $PackagePath).Path
$failures = [System.Collections.Generic.List[string]]::new()
$checks = [System.Collections.Generic.List[string]]::new()
function Assert-Check([bool]$Condition, [string]$Message) {
    if ($Condition) { $checks.Add($Message) } else { $failures.Add($Message) }
}
$roadmap = [System.IO.File]::ReadAllText((Join-Path $packageRoot 'ROADMAP.md'))
Assert-Check ($roadmap.StartsWith('# Star Player Development Roadmap')) 'Required roadmap title'
$requiredSections = @('1. Architecture Summary','2. Repository Structure','3. Core Domain Model','4. Data Flow','5. Persistence Strategy','6. Testing Strategy','7. Phase Dependency Map','8. Development Phases','Approved Owner Decisions')
foreach ($section in $requiredSections) { Assert-Check ($roadmap.Contains('## ' + $section)) ('Roadmap section: ' + $section) }
$phaseMatches = [regex]::Matches($roadmap, '(?m)^## Phase (\d+) — .+$')
Assert-Check ($phaseMatches.Count -eq 30) 'Exactly 30 phases'
$fields = @('Goal','Dependencies','Scope','Explicit Non-Goals','Architecture / Systems','Domain Models','Implementation Tasks','UI Work','Persistence Work','Events','Tests','Acceptance Criteria','Deliverables','Risks','Documentation Updates','Claude Code Handoff')
for ($i=0; $i -lt $phaseMatches.Count; $i++) {
    $start = $phaseMatches[$i].Index
    $end = if ($i+1 -lt $phaseMatches.Count) { $phaseMatches[$i+1].Index } else { $roadmap.IndexOf('## 9. Implementation Handoff') }
    $body = $roadmap.Substring($start,$end-$start)
    $phaseNumber = [int]$phaseMatches[$i].Groups[1].Value
    Assert-Check ($phaseNumber -eq $i) ('Sequential phase ' + $i)
    foreach ($field in $fields) {
        $pattern = '(?m)^### ' + [regex]::Escape($field) + '\r?$'
        Assert-Check ([regex]::Matches($body,$pattern).Count -eq 1) ('Phase ' + $i + ' field: ' + $field)
    }
    Assert-Check ($body.Contains('**Gate: G +**')) ('Phase ' + $i + ' explicit gate')
    Assert-Check ([regex]::IsMatch($body,'(?m)^> .+')) ('Phase ' + $i + ' copyable handoff')
    Assert-Check ($body.Contains('PHASE_' + $i + '_REPORT.md')) ('Phase ' + $i + ' report deliverable')
}
$depBlock = $roadmap.Substring($roadmap.IndexOf('| Phase | Direct prerequisites |'), $roadmap.IndexOf('Prototype 3 spans') - $roadmap.IndexOf('| Phase | Direct prerequisites |'))
$depMatches = [regex]::Matches($depBlock, '(?m)^\| (\d+) \| ([^|]+) \|')
Assert-Check ($depMatches.Count -eq 30) 'Dependency map contains every phase'
foreach ($dep in $depMatches) {
    $phaseNumber = [int]$dep.Groups[1].Value
    foreach ($number in [regex]::Matches($dep.Groups[2].Value,'\d+')) {
        Assert-Check ([int]$number.Value -lt $phaseNumber) ('Acyclic direct edge ' + $number.Value + ' -> ' + $phaseNumber)
    }
}
$contracts = [System.IO.File]::ReadAllText((Join-Path $packageRoot 'docs/SYSTEM_CONTRACTS.md'))
$contractMatches = [regex]::Matches($contracts,'(?m)^## C(\d{2}) — .+$')
Assert-Check ($contractMatches.Count -eq 48) 'Exactly 48 system contracts'
$contractFields = @('Responsibility','Inputs','Outputs','State ownership','Dependencies','Public interfaces','Events','Persistence','Failure cases','Testing')
for ($i=0; $i -lt $contractMatches.Count; $i++) {
    $start = $contractMatches[$i].Index
    $end = if($i+1 -lt $contractMatches.Count) { $contractMatches[$i+1].Index } else { $contracts.Length }
    $body = $contracts.Substring($start,$end-$start)
    Assert-Check ([int]$contractMatches[$i].Groups[1].Value -eq ($i+1)) ('Sequential contract C' + ($i+1))
    foreach ($field in $contractFields) { Assert-Check ($body.Contains('**' + $field + ':**')) ('Contract C' + ($i+1) + ' field: ' + $field) }
}
$markdownFiles = Get-ChildItem -LiteralPath $packageRoot -Filter '*.md' -Recurse | Where-Object { $_.FullName -notlike '*\sources\UI_UX_GUIDE_v1.8.md' }
$linkCount = 0
foreach ($file in $markdownFiles) {
    $body = [System.IO.File]::ReadAllText($file.FullName)
    Assert-Check (([regex]::Matches($body,'(?m)^```').Count % 2) -eq 0) ('Closed code fences: ' + $file.Name)
    Assert-Check (-not [regex]::IsMatch($body,'\b(TODO|TBD|FIXME)\b')) ('No unresolved author placeholders: ' + $file.Name)
    foreach ($link in [regex]::Matches($body,'\[[^\]]+\]\(([^)]+)\)')) {
        $target = $link.Groups[1].Value
        if($target -match '^(https?://|#)') { continue }
        $target = $target.Split('#')[0]
        $resolved = Join-Path $file.DirectoryName $target
        Assert-Check (Test-Path -LiteralPath $resolved -PathType Leaf) ('Existing local link: ' + $file.Name + ' -> ' + $target)
        $linkCount++
    }
}
$expectedHashes = @{
    'Star_Player_game_v1.6.txt'='87EADC3684B368FFBF9CB8A48646794E1E37E2E74006FD749D36CCC0978491DA';
    'UI_UX_GUIDE_v1.8.md'='45B98A38116CBA56C5E53B66E688A737DFD0764BEFD74B7FAEF13037D68C8DBB';
    'MASTER_REQUEST.txt'='813452BA3B325CBBBC558304F4E6008CB7A915615B8FF17A758B7A88FD406C76';
    'ROADMAP_REVISION_REQUEST.txt'='6027C298C8510991A933DE8654027C7CDAE07794389CBFAB92A8F49977AC0876'
}
foreach ($name in $expectedHashes.Keys) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $packageRoot ('sources/' + $name)) -Algorithm SHA256).Hash
    Assert-Check ($hash -eq $expectedHashes[$name]) ('Unmodified source hash: ' + $name)
}
$portraitHash = (Get-FileHash -LiteralPath (Join-Path $packageRoot 'Star_Player_Portrait_System_Design.MD') -Algorithm SHA256).Hash
Assert-Check ($portraitHash -eq 'D53DF3D253CEC20A62BF89F5B558AD5C737F4B6ED24E2AB0A342BB0E0EB94486') 'Independent Portrait specification unchanged'
$sourceIndex = [System.IO.File]::ReadAllText((Join-Path $packageRoot 'docs/SOURCE_SECTION_INDEX.md'))
$uiSource = [System.IO.File]::ReadAllText((Join-Path $packageRoot 'sources/UI_UX_GUIDE_v1.8.md'))
foreach ($heading in [regex]::Matches($uiSource,'(?m)^#{1,2} (\d+)\. ([^\r\n]+)')) {
    $label = $heading.Groups[1].Value + '. ' + $heading.Groups[2].Value
    Assert-Check ($sourceIndex.Contains($label)) ('Indexed UI section ' + $heading.Groups[1].Value)
}
$decisionMatches = [regex]::Matches($roadmap,'(?m)^### D(\d+) — .+$')
Assert-Check ($decisionMatches.Count -eq 6) 'Six explicit owner decision records'
for($i=0; $i -lt $decisionMatches.Count; $i++) {
    $end = if($i+1 -lt $decisionMatches.Count){$decisionMatches[$i+1].Index}else{$roadmap.Length}
    $body = $roadmap.Substring($decisionMatches[$i].Index,$end-$decisionMatches[$i].Index)
    foreach($field in @('Status','Decision','Rationale','Implementation Boundary','Impact')) {
        Assert-Check ($body.Contains('**'+$field+':**')) ('Decision D'+($i+1)+' field: '+$field)
    }
    Assert-Check ($body.Contains('**Status:** APPROVED.')) ('Decision D'+($i+1)+' approved status')
}
Assert-Check (-not [regex]::IsMatch($roadmap,'(?i)D1, D2, D3, D4 and D6 (are|remain) pending')) 'No owner decision remains pending'
Assert-Check ($roadmap.Contains('preserve first, measure, then prune only if evidence requires it')) 'D6 preserve-first policy recorded'
Assert-Check ($roadmap.Contains('### 1.11 Club Finance Lite approved exception and renewed scope lock')) 'Club Finance Lite approved exception recorded'
Assert-Check ($contracts.Contains('## C48 — Club Finance Lite')) 'Club Finance Lite contract recorded'
Assert-Check ($contracts.Contains('C13 alone owns player contracts/obligations')) 'Single contract obligation authority recorded'
Assert-Check ($roadmap.Contains('Phase 11 owns baseball results and achievement facts and has no dependency on C48 or Phase 14')) 'No Phase 11 to Phase 14 finance dependency'
$sourceRoot = Join-Path $packageRoot 'sources'
$unexpectedCode = Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Where-Object {$_.Extension -in @('.cs','.tscn','.sql','.csproj','.godot')}
Assert-Check (@($unexpectedCode).Count -eq 0) 'No production code, scenes or SQL migrations in deliverables'
$result = [ordered]@{status=if($failures.Count -eq 0){'PASS'}else{'FAIL'}; checks_passed=$checks.Count; checks_failed=$failures.Count; phases=$phaseMatches.Count; contracts=$contractMatches.Count; local_links_checked=$linkCount; failures=@($failures); scope='Documentation structure and integrity only; no game runtime tests'}
$result | ConvertTo-Json -Depth 4
if($failures.Count -gt 0){exit 1}
