<#
.SYNOPSIS
  B-4 Artifact candidate list, rule v2, for one clean build (read-only). The output is CANDIDATE_INCOMPLETE.

.DESCRIPTION
  Rule v2 (B1_G4_IMPLEMENTATION_REPORT.md 5): SIMHARNESS_BINARY + every file of the SimHarness, verifier and 4 Test Profile output
  folders, minus *.pdb (A8) and coverage source-root maps (A9), which are listed as unpinned output files instead.
  Roles: the SimHarness / verifier / test primary assemblies, *.deps.json and *.runtimeconfig.json = RUNTIME_CONFIG, everything else
  DEPENDENCY. Nothing is classified as unused. Each row also gets a descriptive category (test host, VSTest, adapter, satellite
  resource, native, ...) and, when -Trace files are given, the observed state of that diagnostic trace (IMAGE_LOADED /
  PROCESS_STARTED / NOT_OBSERVED_IN_RUN, or NOT_TRACED for a folder no trace ran from).
  -Previous compares with an earlier candidate file (added / removed / changed bytes by relative path).

  Every path is checked through held handles before it is read (see the B4-R01 block below); any doubt refuses the list.

  Exit: 0 written, 1 refused (unsafe or incomplete input), 2 usage.
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
if (-not ('B1Ops.Native' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'B1OpsNative.cs') }
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

# B4-R01: the build root, every folder on the way to each output folder and every entry below is opened through a handle that does not
# share delete (reparse points opened, not followed) and held until the list is written: no component can be swapped between the
# check and the read. A reparse point, a final path other than the expected one (junction / mount point / root escape), a hard link
# (one file under two names), a duplicate relative path, a size that differs from the bytes read, a missing output folder or a
# missing / extra primary binary refuses the whole list (exit 1); nothing is skipped.
$held = New-Object System.Collections.Generic.List[object]
function Refuse([string]$m) { foreach ($h in $held) { $h.Dispose() }; [Console]::Error.WriteLine("REFUSED: $m"); Write-Output "RESULT=REFUSED $m"; exit 1 }
function Open-Checked([string]$p, [uint32]$access) {
    try { $h = [B1Ops.Native]::Open($p, $access) } catch { Refuse "$($p): $($_.Exception.Message)" }
    $held.Add($h)
    $info = [B1Ops.Native]::Query($h)
    if ($info.IsReparse) { Refuse "$($p): reparse point" }
    $final = [B1Ops.Native]::FinalPath($h).TrimEnd('\')
    if (-not $final.Equals($p.TrimEnd('\'), 'OrdinalIgnoreCase')) { Refuse "$($p): final path is '$final' (junction, mount point or root escape)" }
    [pscustomobject]@{ H = $h; Info = $info }
}
$opened = @{}
foreach ($d in $folders.Keys) {
    $dir = Join-Path $root ($d -replace '/', '\')
    $c = New-Object IO.DirectoryInfo($dir); $chain = @()
    while ($c) { $chain = @($(if ($c.Parent) { $c.FullName.TrimEnd('\') } else { $c.FullName })) + $chain; $c = $c.Parent }
    foreach ($x in $chain) {
        if ($opened.ContainsKey($x.ToLowerInvariant())) { continue }
        if (-not [IO.Directory]::Exists($x)) { Refuse "missing output folder $d ($x does not exist; build first)" }
        $o = Open-Checked $x 0
        if (-not $o.Info.IsDirectory) { Refuse "$($x): not a directory" }
        $opened[$x.ToLowerInvariant()] = $true
    }
}

$artifacts = New-Object System.Collections.Generic.List[object]
$unpinned = New-Object System.Collections.Generic.List[object]
$relSeen = @{}; $idSeen = @{}
foreach ($d in $folders.Keys) {
    $dir = Join-Path $root ($d -replace '/', '\')
    $primaries = 0
    $stack = New-Object System.Collections.Generic.Stack[string]
    $stack.Push($dir)
    while ($stack.Count) {
        $cur = $stack.Pop()
        foreach ($e in @([IO.Directory]::GetFileSystemEntries($cur) | Sort-Object)) {
            $isDir = ([IO.File]::GetAttributes($e) -band [IO.FileAttributes]::Directory) -ne 0
            $o = Open-Checked $e $(if ($isDir) { 0 } else { [B1Ops.Native]::ReadData })
            if ($o.Info.IsDirectory -ne $isDir) { Refuse "$($e): changed type during the scan" }
            if ($isDir) { $stack.Push($e); continue }
            $rel = $e.Substring($root.Length + 1).Replace('\', '/')
            if ($relSeen.ContainsKey($rel.ToLowerInvariant())) { Refuse "duplicate relative path $rel" }
            $relSeen[$rel.ToLowerInvariant()] = $true
            $id = "$($o.Info.VolumeSerial):$($o.Info.FileId)"
            if ($o.Info.Links -ne 1 -or $idSeen.ContainsKey($id)) { Refuse "$($rel): hard link (one file under $($o.Info.Links) names)" }
            $idSeen[$id] = $true
            $read = [int64]0
            $sha = [B1Ops.Native]::Sha256($o.H, [ref]$read)
            if ($read -ne $o.Info.Size) { Refuse "$($rel): read $read bytes but the file has $($o.Info.Size)" }
            $name = [IO.Path]::GetFileName($e)
            $row = [ordered]@{ relative_path = $rel; raw_sha256 = $sha; byte_length = $read }
            if ($name -like '*.pdb') { $row.reason = 'A8 PDB'; $unpinned.Add([pscustomobject]$row); continue }
            if ($name -like '*msCoverageSourceRootsMapping*') { $row.reason = 'A9 coverage map'; $unpinned.Add([pscustomobject]$row); continue }
            $isPrimary = $name -eq $folders[$d].primary -and $cur -eq $dir
            if ($isPrimary) { $primaries++ }
            $role = if ($isPrimary) { $folders[$d].role } elseif ($name -match '\.(deps|runtimeconfig)\.json$') { 'RUNTIME_CONFIG' } else { 'DEPENDENCY' }
            $state = if (-not $tracedFolders.ContainsKey($d)) { 'NOT_TRACED' } elseif ($observed.ContainsKey($e.ToLowerInvariant())) { $observed[$e.ToLowerInvariant()] } else { 'NOT_OBSERVED_IN_RUN' }
            $artifacts.Add([pscustomobject]([ordered]@{ role = $role; relative_path = $rel; raw_sha256 = $sha; byte_length = $read; category = (Get-Category $rel); observed = $state }))
        }
    }
    if ($primaries -ne 1) { Refuse "$d must contain its primary binary $($folders[$d].primary) exactly once (found $primaries)" }
}
$roleCount = @{}; foreach ($x in $artifacts) { $roleCount[$x.role] = 1 + $(if ($roleCount.ContainsKey($x.role)) { $roleCount[$x.role] } else { 0 }) }
foreach ($want in @(@('SIMHARNESS_BINARY', 1), @('VERIFIER_BINARY', 1), @('TEST_ASSEMBLY', 4))) {
    $n = if ($roleCount.ContainsKey($want[0])) { $roleCount[$want[0]] } else { 0 }
    if ($n -ne $want[1]) { Refuse "role $($want[0]) has $n primary binaries, expected $($want[1])" }
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

$exeRows = @($a | Where-Object { $_.relative_path -like '*.exe' } | ForEach-Object { $_.relative_path })
$doc = [ordered]@{
    format = 'starplayer-b4-artifact-candidates-v2'; status = 'CANDIDATE_INCOMPLETE';
    roles_note = 'role is one of the closed Build Provenance roles; category and observed are descriptive diagnostics, not provenance fields. EXE rows are DEPENDENCY candidates whose interpretation (F-14 / N-11) is still pending; external execution-chain pins and TRUST_CONFIG are outside this list.'
    exe_rows_interpretation_pending = $exeRows; source_build = $root; source_git_sha = $SourceCommit
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
