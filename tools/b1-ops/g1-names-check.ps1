<#
.SYNOPSIS
  G1-a static consistency check of the B-4 normative file-name / path table against the production source (read-only).

.DESCRIPTION
  G1-a proposal (docs/tasks/B1_G1_G2_CONDITIONAL_DECISION_PROPOSAL.md, part B): the official names stay as the code has them and the
  normative table in B-4 (docs/tasks/B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md 6.1) is the single definition. This tool pins every
  occurrence of each governed name literal in the production source (src/, tools/StarPlayer.SimHarness/, tools/StarPlayer.EvidenceVerifier/,
  *.cs, git-tracked) by file and count. Any new, missing, moved or renamed occurrence fails, so a name cannot drift in one place only;
  a deliberate change has to update the B-4 table and this inventory together (and is then a reviewed change).
  It is static: dynamic agreement of producer, verifier and tests is covered by the existing TEST_ONLY end-to-end tests
  (producer -> archive -> verifier), which use these same code paths. Test files are only counted (informational).
  -SelfTest copies the governed files into a scratch directory, applies counterexample mutations and expects FAIL for each.

  Exit: 0 PASS, 1 FAIL, 2 usage.
#>
[CmdletBinding()]
param(
    [string]$Root,
    [switch]$SelfTest
)
Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'
if (-not $Root) { $Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }   # $PSScriptRoot is empty in a PS 5.1 param default

# name -> regex and the exact production occurrences (repository-relative path -> count) of the B-4 6.1 table
$Inventory = [ordered]@{
    'manifest.json (UNIT_MANIFEST file)'          = @('(?<=["/])manifest\.json"', [ordered]@{ 'tools/StarPlayer.EvidenceVerifier/Verifier.cs' = 1; 'tools/StarPlayer.SimHarness/DominanceOfficial.cs' = 3; 'tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs' = 2; 'tools/StarPlayer.SimHarness/FamilyOfficial.cs' = 2; 'tools/StarPlayer.SimHarness/RunReport.cs' = 1 })
    'preflight.json (Family PREFLIGHT)'           = @('"preflight\.json"', [ordered]@{ 'tools/StarPlayer.SimHarness/Official.cs' = 1 })
    'preflight/ (Unit PREFLIGHT directory)'       = @('PreflightDirectory = "preflight"', [ordered]@{ 'tools/StarPlayer.SimHarness/Official.cs' = 1 })
    'run/<run_id>/ (run archive prefix)'          = @('"run/|\$"run/', [ordered]@{ 'tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs' = 1 })
    'build/simharness/official (result root)'     = @('"build/simharness/official"', [ordered]@{ 'tools/StarPlayer.SimHarness/Official.cs' = 1 })
    'family-result.json (Family run index)'       = @('"family-result\.json"', [ordered]@{ 'tools/StarPlayer.SimHarness/FamilyOfficial.cs' = 1 })
    'dominance-result.json (Dominance run index)' = @('"dominance-result\.json"', [ordered]@{ 'tools/StarPlayer.SimHarness/DominanceOfficial.cs' = 1 })
    'units/ (unit directory)'                     = @('"units/"|\$"units/\{', [ordered]@{ 'tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs' = 1; 'tools/StarPlayer.SimHarness/FamilyOfficial.cs' = 1 })
    'cells/ (Dominance cell directory)'           = @('"cells/"', [ordered]@{ 'tools/StarPlayer.SimHarness/DominanceOfficial.cs' = 1 })
}
$Scopes = @('src', 'tools/StarPlayer.SimHarness', 'tools/StarPlayer.EvidenceVerifier')

function Get-Sources([string]$root) {
    $list = foreach ($s in $Scopes) {
        $d = Join-Path $root $s
        if (Test-Path -LiteralPath $d) {
            Get-ChildItem -LiteralPath $d -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
                ForEach-Object { $_.FullName.Substring($root.TrimEnd('\').Length + 1).Replace('\', '/') }
        }
    }
    @($list | Sort-Object -CaseSensitive)
}

function Invoke-Check([string]$root) {
    $sources = Get-Sources $root
    $text = @{}
    foreach ($f in $sources) { $text[$f] = [IO.File]::ReadAllText((Join-Path $root $f)) }
    $bad = New-Object System.Collections.Generic.List[string]
    $rows = New-Object System.Collections.Generic.List[string]
    foreach ($name in $Inventory.Keys) {
        $rx = $Inventory[$name][0]; $want = $Inventory[$name][1]
        $have = New-Object 'System.Collections.Generic.Dictionary[string,int]' ([StringComparer]::Ordinal)
        foreach ($f in $sources) { $n = [regex]::Matches($text[$f], $rx).Count; if ($n) { $have[$f] = $n } }
        foreach ($f in $want.Keys) {
            $got = if ($have.ContainsKey($f)) { $have[$f] } else { 0 }
            if ($got -ne $want[$f]) { $bad.Add("$name : $f has $got, table says $($want[$f])") }
        }
        foreach ($f in $have.Keys) { if (-not $want.Contains($f)) { $bad.Add("$name : unlisted occurrence in $f ($($have[$f]))") } }
        $rows.Add(("{0,-46} {1}" -f $name, (@($have.Keys | Sort-Object -CaseSensitive | ForEach-Object { "$_=$($have[$_])" }) -join ', ')))
    }
    [pscustomobject]@{ Bad = $bad.ToArray(); Rows = $rows.ToArray(); Files = $sources.Count }
}

if ($SelfTest) {
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ('g1-names-selftest-' + [guid]::NewGuid().ToString('N'))
    $results = New-Object System.Collections.Generic.List[string]
    try {
        function Fresh {
            if (Test-Path $scratch) { Remove-Item -Recurse -Force $scratch }
            foreach ($f in Get-Sources $Root) { $dst = Join-Path $scratch $f; [void][IO.Directory]::CreateDirectory((Split-Path $dst)); [IO.File]::Copy((Join-Path $Root $f), $dst) }
        }
        function Mutate([string]$rel, [string]$from, [string]$to) {
            $p = Join-Path $scratch $rel; $t = [IO.File]::ReadAllText($p)
            if (-not $t.Contains($from)) { throw "selftest setup: '$from' not in $rel" }
            [IO.File]::WriteAllText($p, ([regex]::new([regex]::Escape($from))).Replace($t, $to, 1))
        }
        Fresh; $r = Invoke-Check $scratch; $results.Add("baseline_copy_passes $(if ($r.Bad.Count -eq 0) { 'PASS' } else { 'FAIL' })")
        $cases = @(
            @('verifier_renames_manifest', 'tools/StarPlayer.EvidenceVerifier/Verifier.cs', 'manifest.json"', 'manifest2.json"'),
            @('producer_renames_manifest', 'tools/StarPlayer.SimHarness/RunReport.cs', 'manifest.json"', 'unit-manifest.json"'),
            @('verifier_hardcodes_run_prefix', 'tools/StarPlayer.EvidenceVerifier/Verifier.cs', 'EvidenceWriter.RunPath(runId, "")', '$"run/{runId}/"'),
            @('preflight_renamed', 'tools/StarPlayer.SimHarness/Official.cs', '"preflight.json"', '"family-preflight.json"'),
            @('result_root_moved', 'tools/StarPlayer.SimHarness/Official.cs', '"build/simharness/official"', '"build/simharness/official2"'),
            @('second_dominance_index_literal', 'tools/StarPlayer.EvidenceVerifier/Verifier.cs', 'DominanceOfficial.ResultFile', '"dominance-result.json"')
        )
        foreach ($c in $cases) {
            Fresh; Mutate $c[1] $c[2] $c[3]; $r = Invoke-Check $scratch
            $results.Add("$($c[0]) $(if ($r.Bad.Count -gt 0) { 'PASS' } else { 'FAIL' }) :: $(($r.Bad | Select-Object -First 2) -join ' | ')")
        }
    }
    finally { if (Test-Path $scratch) { Remove-Item -Recurse -Force $scratch } }
    $results | ForEach-Object { Write-Output $_ }
    $failed = @($results | Where-Object { $_ -match '^\S+ FAIL' }).Count
    Write-Output "SELFTEST cases=$($results.Count) failed=$failed"
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not (Test-Path -LiteralPath (Join-Path $Root 'StarPlayer.sln'))) { [Console]::Error.WriteLine("usage: '$Root' is not the repository root."); exit 2 }
$r = Invoke-Check $Root
$r.Rows | ForEach-Object { Write-Output $_ }
foreach ($b in $r.Bad) { Write-Output "    - $b" }
$testCounts = foreach ($name in $Inventory.Keys) {
    $n = 0; foreach ($f in Get-ChildItem (Join-Path $Root 'tests') -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }) { $n += [regex]::Matches([IO.File]::ReadAllText($f.FullName), $Inventory[$name][0]).Count }
    "$($name.Split(' ')[0])=$n"
}
Write-Output "tests (informational, dynamic agreement is the TEST_ONLY end-to-end tests): $($testCounts -join ', ')"
Write-Output "RESULT=$(if ($r.Bad.Count) { 'FAIL' } else { 'PASS' }) production_files=$($r.Files) names=$($Inventory.Count) mismatches=$($r.Bad.Count)"
exit $(if ($r.Bad.Count) { 1 } else { 0 })
