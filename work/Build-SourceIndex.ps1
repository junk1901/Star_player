$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $taskRoot 'outputs'
$indexLines = [System.Collections.Generic.List[string]]::new()
$indexLines.Add('# Source Section Index')
$indexLines.Add('')
$indexLines.Add('Original headings and one-based line numbers from the unchanged source copies. Requirement mappings are in [Requirements Traceability](REQUIREMENTS_TRACEABILITY.md). Headings quoted in Korean preserve the source wording; surrounding architecture documents are English. This index is navigation evidence, not implementation evidence.')
$indexLines.Add('')
$sources = @(
    @{ Key='G'; Name='Star_Player_game_v1.6.txt'; Label='Game design'; },
    @{ Key='U'; Name='UI_UX_GUIDE_v1.8.md'; Label='UI/UX guide'; },
    @{ Key='M'; Name='MASTER_REQUEST.txt'; Label='Direct master request'; },
    @{ Key='R'; Name='ROADMAP_REVISION_REQUEST.txt'; Label='Latest owner revision'; },
    @{ Key='P'; Name='Star_Player_Portrait_System_Design.MD'; Label='Independent Portrait design'; }
)
$counts = @{}
foreach ($source in $sources) {
    $sourceRelative = if ($source.Key -eq 'P') { $source.Name } else { 'sources/' + $source.Name }
    $sourceLines = [System.IO.File]::ReadAllLines((Join-Path $packageRoot $sourceRelative))
    $indexLines.Add('## ' + $source.Key + ' — ' + $source.Label)
    $indexLines.Add('')
    $indexLines.Add('Source: [' + $source.Name + '](../' + $sourceRelative + ').')
    $indexLines.Add('')
    $indexLines.Add('| Line | Original heading |')
    $indexLines.Add('|---:|---|')
    $fenced = $false
    $count = 0
    for ($i=0; $i -lt $sourceLines.Length; $i++) {
        $line = $sourceLines[$i]
        if ($line -match '^\s*```') { $fenced = -not $fenced; continue }
        if (-not $fenced -and $line -match '^#{1,6}\s+(.+)') {
            $heading = $Matches[1].Replace('|','\|')
            $indexLines.Add('| ' + ($i+1) + ' | ' + $heading + ' |')
            $count++
        }
    }
    $indexLines.Add('')
    $counts[$source.Key] = $count
}
[System.IO.File]::WriteAllLines((Join-Path $packageRoot 'docs/SOURCE_SECTION_INDEX.md'), $indexLines, [System.Text.UTF8Encoding]::new($false))
$counts | ConvertTo-Json -Compress
