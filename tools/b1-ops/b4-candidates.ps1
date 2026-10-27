<#
.SYNOPSIS
  B-4 Artifact candidate list, rule v2, for one clean build (read-only). The output is CANDIDATE_NOT_APPROVED.

.DESCRIPTION
  Rule v2 (B1_G4_IMPLEMENTATION_REPORT.md 5): SIMHARNESS_BINARY + every file of the SimHarness, verifier and 4 Test Profile output
  folders, minus *.pdb (A8) and coverage source-root maps (A9), which are listed as unpinned output files instead.
  Roles: the SimHarness / verifier / test primary assemblies, *.deps.json and *.runtimeconfig.json = RUNTIME_CONFIG, everything else
  DEPENDENCY. Nothing is classified as unused. Each row also gets a descriptive category (test host, VSTest, adapter, satellite
  resource, native, ...) and, when -Trace files are given, the observed state of that diagnostic trace (IMAGE_LOADED /
  PROCESS_STARTED / NOT_OBSERVED_IN_RUN, or NOT_TRACED for a folder no trace ran from).
  -Previous compares with an earlier candidate file (added / removed / changed bytes by relative path).

  Exit: 0 written, 2 usage.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BuildRoot,
    [Parameter(Mandatory = $true)][string]$SourceCommit,
    [Parameter(Mandatory = $true)][string]$Out,
    [string[]]$Trace = @(),
    [string]$Previous
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'
$utf8 = New-Object Text.UTF8Encoding($false)
function Fail-Usage([string]$m) { [Console]::Error.WriteLine("usage: $m"); exit 2 }

$root = [IO.Path]::GetFullPath($BuildRoot).TrimEnd('\')
if (Test-Path -LiteralPath $Out) { Fail-Usage "'$Out' already exists." }
$head = (& git.exe -C $root rev-parse HEAD).Trim()
$dirty = @(& git.exe -C $root status --porcelain --untracked-files=no).Count
$Trace = @($Trace | ForEach-Object { $_ -split '\|' } | Where-Object { $_ })

$folders = [ordered]@{
    'tools/StarPlayer.SimHarness/bin/Release/net8.0'        = @{ primary = 'StarPlayer.SimHarness.dll'; role = 'SIMHARNESS_BINARY' }
    'tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0'  = @{ primary = 'StarPlayer.EvidenceVerifier.dll'; role = 'VERIFIER_BINARY' }
    'tests/StarPlayer.Domain.Tests/bin/Release/net8.0'         = @{ primary = 'StarPlayer.Domain.Tests.dll'; role = 'TEST_ASSEMBLY' }
    'tests/StarPlayer.Simulation.Tests/bin/Release/net8.0'     = @{ primary = 'StarPlayer.Simulation.Tests.dll'; role = 'TEST_ASSEMBLY' }
    'tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0' = @{ primary = 'StarPlayer.Infrastructure.Tests.dll'; role = 'TEST_ASSEMBLY' }
    'tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0'     = @{ primary = 'StarPlayer.SimHarness.Tests.dll'; role = 'TEST_ASSEMBLY' }
}

function Get-Category([string]$rel) {
    $name = [IO.Path]::GetFileName($rel)
    if ($name -match '\.(deps|runtimeconfig)\.json$') { return 'RUNTIME_CONFIG' }
    if ($name -like 'StarPlayer.*.dll') { return 'STARPLAYER' }
    if ($name -like 'StarPlayer.*.exe') { return 'APPHOST' }
    if ($rel -match '/[a-z]{2}(-[A-Za-z]{2,4})*/[^/]+\.resources\.dll$') { return 'SATELLITE_RESOURCE' }
    if ($name -like 'testhost.*') { return 'TEST_HOST' }
    if ($name -like 'Microsoft.TestPlatform.*' -or $name -like 'Microsoft.VisualStudio.TestPlatform.*' -or $name -like 'Microsoft.VisualStudio.CodeCoverage.*') { return 'VSTEST_IN_FOLDER' }
    if ($name -like 'xunit.runner.*') { return 'TEST_ADAPTER' }
    if ($rel -match '/runtimes/[^/]+/native/' -or $name -like 'e_sqlite3.dll') { return 'NATIVE' }
    if ($name -like 'xunit.*') { return 'TEST_FRAMEWORK' }
    if ($name -like '*.dll') { return 'THIRD_PARTY_MANAGED' }
    'OTHER'
}

# full path (lower case) -> strongest observed state; the folders a trace ran from
$observed = @{}
$tracedFolders = @{}
foreach ($t in $Trace) {
    foreach ($line in ([IO.File]::ReadAllLines($t, $utf8) | Select-Object -Skip 1)) {
        $f = $line -split "`t"
        if ($f.Count -lt 2) { continue }
        $k = $f[1].ToLowerInvariant()
        if (-not $observed.ContainsKey($k) -or $f[0] -eq 'PROCESS_STARTED') { $observed[$k] = $f[0] }
    }
    foreach ($d in $folders.Keys) { if ([IO.Path]::GetFileNameWithoutExtension($t) -like "*_$(($d -split '/')[1])") { $tracedFolders[$d] = $true } }   # trace file name: <run>_<project folder>.tsv
}

$artifacts = New-Object System.Collections.Generic.List[object]
$unpinned = New-Object System.Collections.Generic.List[object]
foreach ($d in $folders.Keys) {
    $dir = Join-Path $root ($d -replace '/', '\')
    if (-not [IO.Directory]::Exists($dir)) { Fail-Usage "missing output folder $d (build first)." }
    foreach ($file in [IO.Directory]::GetFiles($dir, '*', [IO.SearchOption]::AllDirectories)) {
        if (([IO.File]::GetAttributes($file) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail-Usage "reparse point in the build output: $file" }
        $rel = $file.Substring($root.Length + 1).Replace('\', '/')
        $name = [IO.Path]::GetFileName($file)
        $row = [ordered]@{ relative_path = $rel; raw_sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(); byte_length = (New-Object IO.FileInfo($file)).Length }
        if ($name -like '*.pdb') { $row.reason = 'A8 PDB'; $unpinned.Add([pscustomobject]$row); continue }
        if ($name -like '*msCoverageSourceRootsMapping*') { $row.reason = 'A9 coverage map'; $unpinned.Add([pscustomobject]$row); continue }
        $role = if ($name -eq $folders[$d].primary) { $folders[$d].role } elseif ($name -match '\.(deps|runtimeconfig)\.json$') { 'RUNTIME_CONFIG' } else { 'DEPENDENCY' }
        $state = if (-not $tracedFolders.ContainsKey($d)) { 'NOT_TRACED' } elseif ($observed.ContainsKey($file.ToLowerInvariant())) { $observed[$file.ToLowerInvariant()] } else { 'NOT_OBSERVED_IN_RUN' }
        $artifacts.Add([pscustomobject]([ordered]@{ role = $role; relative_path = $rel; raw_sha256 = $row.raw_sha256; byte_length = $row.byte_length; category = (Get-Category $rel); observed = $state }))
    }
}
$a = $artifacts.ToArray(); [Array]::Sort($a, [Comparison[object]] { param($x, $y) [string]::CompareOrdinal($x.relative_path, $y.relative_path) })
$u = $unpinned.ToArray(); [Array]::Sort($u, [Comparison[object]] { param($x, $y) [string]::CompareOrdinal($x.relative_path, $y.relative_path) })

$delta = $null
if ($Previous) {
    $p = [IO.File]::ReadAllText($Previous, $utf8) | ConvertFrom-Json
    $old = @{}; foreach ($x in $p.artifacts) { $old[$x.relative_path] = $x.raw_sha256 }
    $new = @{}; foreach ($x in $a) { $new[$x.relative_path] = $x.raw_sha256 }
    $changed = @($a | Where-Object { $old.ContainsKey($_.relative_path) -and $old[$_.relative_path] -ne $_.raw_sha256 })
    $delta = [ordered]@{
        previous = [IO.Path]::GetFullPath($Previous); previous_source_git_sha = $p.source_git_sha
        added = @($a | Where-Object { -not $old.ContainsKey($_.relative_path) } | ForEach-Object { $_.relative_path })
        removed = @($old.Keys | Where-Object { -not $new.ContainsKey($_) } | Sort-Object)
        changed_count = $changed.Count
        changed_by_category = @($changed | Group-Object category | Sort-Object Name | ForEach-Object { [ordered]@{ category = $_.Name; count = $_.Count } })
        unchanged_count = @($a | Where-Object { $old.ContainsKey($_.relative_path) -and $old[$_.relative_path] -eq $_.raw_sha256 }).Count
    }
}

$doc = [ordered]@{
    format = 'starplayer-b4-artifact-candidates-v2'; status = 'CANDIDATE_NOT_APPROVED'; source_build = $root; source_git_sha = $SourceCommit
    head = $head; head_matches = ($head -eq $SourceCommit); tracked_changes = $dirty; time_utc = [DateTime]::UtcNow.ToString('o')
    rule = 'SIMHARNESS_BINARY + every file of the SimHarness, verifier and 4 Test Profile folders, minus *.pdb (A8) and coverage maps (A9)'
    traces = @($Trace | ForEach-Object { [IO.Path]::GetFullPath($_) })
    summary = [ordered]@{
        artifacts = $a.Count; unpinned = $u.Count
        by_role = @($a | Group-Object role | Sort-Object Name | ForEach-Object { [ordered]@{ role = $_.Name; count = $_.Count } })
        by_category = @($a | Group-Object category | Sort-Object Name | ForEach-Object { [ordered]@{ category = $_.Name; count = $_.Count } })
        by_observed = @($a | Group-Object observed | Sort-Object Name | ForEach-Object { [ordered]@{ observed = $_.Name; count = $_.Count } })
    }
    delta = $delta; artifacts = $a; unpinned_output_files = $u
}
[IO.File]::WriteAllText($Out, ($doc | ConvertTo-Json -Depth 6), $utf8)
Write-Output "artifacts=$($a.Count) unpinned=$($u.Count) head_matches=$($head -eq $SourceCommit) tracked_changes=$dirty"
foreach ($g in $doc.summary.by_role) { Write-Output ("  role     {0,-20} {1}" -f $g.role, $g.count) }
foreach ($g in $doc.summary.by_category) { Write-Output ("  category {0,-20} {1}" -f $g.category, $g.count) }
foreach ($g in $doc.summary.by_observed) { Write-Output ("  observed {0,-20} {1}" -f $g.observed, $g.count) }
if ($delta) { Write-Output "  delta vs $($delta.previous_source_git_sha): added=$(@($delta.added).Count) removed=$(@($delta.removed).Count) changed=$($delta.changed_count) unchanged=$($delta.unchanged_count)" }
Write-Output "RESULT=WRITTEN out=$Out sha256=$((Get-FileHash -LiteralPath $Out -Algorithm SHA256).Hash.ToLowerInvariant())"
exit 0
