<#
.SYNOPSIS
  A-1 pre-publication scan of one commit and all its ancestors (read-only; nothing is fetched, pushed or changed).

.DESCRIPTION
  Every blob reachable from -Commit is in the tree of some ancestor commit, so the text patterns run with "git grep -P" over all
  ancestor commits (binary files are skipped by -I and listed separately). Commit messages and author / committer identities are
  scanned too.

  SECRET classes (block publication, exit 1): private key blocks, GitHub / Google / AWS / Slack / LLM-provider key shapes and
  quoted credential assignments. PRIVACY classes (reported for the OWNER's decision): e-mail addresses, Windows account SIDs,
  user-profile paths, local project paths and Windows machine names.

  -Out receives summary.json (counts, paths, value digests) and findings.tsv (full values: keep it outside the repository).
  The patterns are simple regular expressions: a clean result is not proof that nothing sensitive is present.

  Exit: 0 no SECRET finding, 1 SECRET finding, 2 usage.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Repo,
    [Parameter(Mandatory = $true)][string]$Commit,
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$PublicRef
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
$utf8 = New-Object Text.UTF8Encoding($false)

function Invoke-Git([string[]]$a, [int[]]$ok = @(0)) {
    $o = & git.exe -C $Repo -c core.quotepath=false @a
    if ($ok -notcontains $LASTEXITCODE) { throw "git $($a -join ' ') exited $LASTEXITCODE" }
    @($o)
}
function Get-Sha([string]$s) { ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($utf8.GetBytes($s))) -replace '-', '').ToLowerInvariant() }

if (Test-Path -LiteralPath $Out) { [Console]::Error.WriteLine("usage: '$Out' already exists."); exit 2 }
$full = @(Invoke-Git @('rev-parse', '--verify', "$Commit^{commit}"))[0]
$revs = @(Invoke-Git @('rev-list', $full))
$newRevs = if ($PublicRef) { @(Invoke-Git @('rev-list', $full, "^$PublicRef")) } else { $revs }

$classes = [ordered]@{
    private_key        = @{ kind = 'SECRET'; re = '-----BEGIN [A-Z ]*PRIVATE KEY-----' }
    github_token       = @{ kind = 'SECRET'; re = 'gh[pousr]_[A-Za-z0-9]{36}|github_pat_[A-Za-z0-9_]{22,}' }
    google_api_key     = @{ kind = 'SECRET'; re = 'AIza[0-9A-Za-z_\-]{35}' }
    aws_access_key     = @{ kind = 'SECRET'; re = 'AKIA[0-9A-Z]{16}' }
    slack_token        = @{ kind = 'SECRET'; re = 'xox[baprs]-[A-Za-z0-9\-]{10,}' }
    llm_api_key        = @{ kind = 'SECRET'; re = 'sk-(ant-)?[A-Za-z0-9_\-]{32,}' }
    credential_literal = @{ kind = 'SECRET'; re = '(?i)(password|passwd|secret|api[_-]?key|access[_-]?token|client[_-]?secret)["'']?\s*[:=]\s*["''][^"''\s]{8,}["'']' }
    email              = @{ kind = 'PRIVACY'; re = '[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}' }
    account_sid        = @{ kind = 'PRIVACY'; re = 'S-1-5-21-\d+-\d+-\d+-\d+' }
    user_profile_path  = @{ kind = 'PRIVACY'; re = '(?i)[A-Z]:\\{1,2}Users\\{1,2}[^\\\s"''`|<>]+' }
    project_local_path = @{ kind = 'PRIVACY'; re = '(?i)[A-Z]:\\{1,2}Madegame' }
    machine_name       = @{ kind = 'PRIVACY'; re = 'DESKTOP-[A-Z0-9]{7}' }
}

# patterns go through files: Windows PowerShell 5.1 does not escape quotes inside native-command arguments
New-Item -ItemType Directory -Path "$Out\patterns" | Out-Null
foreach ($name in $classes.Keys) { [IO.File]::WriteAllText("$Out\patterns\$name.txt", $classes[$name].re + "`n", $utf8) }
$findings = New-Object System.Collections.Generic.List[object]
$seen = @{}
foreach ($name in $classes.Keys) {
    for ($i = 0; $i -lt $revs.Count; $i += 40) {
        $chunk = @($revs[$i..([Math]::Min($i + 39, $revs.Count - 1))])
        foreach ($line in (Invoke-Git (@('grep', '-I', '-n', '-o', '-P', '-f', "$Out\patterns\$name.txt") + $chunk) @(0, 1))) {
            $m = [regex]::Match($line, '^([0-9a-f]{40}):(.*?):(\d+):(.*)$')
            if (-not $m.Success) { continue }
            $key = "$name|$($m.Groups[2].Value)|$($m.Groups[4].Value)"
            if ($seen.ContainsKey($key)) { continue }
            $seen[$key] = $true
            $findings.Add([pscustomobject]@{ class = $name; kind = $classes[$name].kind; where = 'blob'; path = $m.Groups[2].Value; line = [int]$m.Groups[3].Value; first_seen_in = $m.Groups[1].Value; value = $m.Groups[4].Value })
        }
    }
}

# commit messages and identities
$log = (Invoke-Git @('log', '--format=%H%x1f%an <%ae>%x1f%cn <%ce>%x1f%B%x1e', $full)) -join "`n"
$ids = @{}
foreach ($rec in ($log -split [char]0x1e)) {
    $f = $rec.Trim() -split [char]0x1f
    if ($f.Count -lt 4) { continue }
    foreach ($who in $f[1], $f[2]) { $ids[$who] = 1 + $(if ($ids.ContainsKey($who)) { $ids[$who] } else { 0 }) }
    foreach ($name in $classes.Keys) {
        foreach ($mm in [regex]::Matches($f[3], $classes[$name].re)) {
            $key = "$name|commit-message|$($mm.Value)"
            if ($seen.ContainsKey($key)) { continue }
            $seen[$key] = $true
            $findings.Add([pscustomobject]@{ class = $name; kind = $classes[$name].kind; where = 'commit-message'; path = ''; line = 0; first_seen_in = $f[0]; value = $mm.Value })
        }
    }
}

$binary = @(Invoke-Git @('log', '--numstat', '--format=', $full) | Where-Object { $_ -match "^-`t-`t" } | ForEach-Object { ($_ -split "`t", 3)[2] } | Sort-Object -Unique)
$originals = @(Invoke-Git @('ls-tree', '-r', '--name-only', $full, '--', 'docs/contracts/b1/originals'))

$tsv = @("kind`tclass`twhere`tpath`tline`tfirst_seen_in`tvalue") + @($findings | ForEach-Object { "$($_.kind)`t$($_.class)`t$($_.where)`t$($_.path)`t$($_.line)`t$($_.first_seen_in)`t$($_.value)" })
[IO.File]::WriteAllLines("$Out\findings.tsv", $tsv, $utf8)
$summary = [ordered]@{
    tool = 'b1-ops/a1-scan.ps1'; commit = $full; public_ref = $PublicRef; time_utc = [DateTime]::UtcNow.ToString('o')
    commits = $revs.Count; commits_not_in_public_ref = @($newRevs).Count
    identities = @($ids.Keys | Sort-Object | ForEach-Object { [ordered]@{ identity_sha256 = (Get-Sha $_); commits_fields = $ids[$_] } })
    classes = @(foreach ($name in $classes.Keys) {
            $g = @($findings | Where-Object { $_.class -eq $name })
            [ordered]@{ class = $name; kind = $classes[$name].kind; findings = $g.Count; distinct_values = @($g | ForEach-Object { $_.value } | Sort-Object -Unique).Count
                paths = @($g | ForEach-Object { if ($_.where -eq 'blob') { $_.path } else { '(commit message)' } } | Sort-Object -Unique)
                value_sha256 = @($g | ForEach-Object { Get-Sha $_.value } | Sort-Object -Unique) }
        })
    binary_paths = $binary; originals = $originals
}
[IO.File]::WriteAllText("$Out\summary.json", ($summary | ConvertTo-Json -Depth 5), $utf8)

$secret = @($findings | Where-Object { $_.kind -eq 'SECRET' })
foreach ($c in $summary.classes) { Write-Output ("{0,-8} {1,-19} findings={2,-5} distinct={3,-4} paths={4}" -f $c.kind, $c.class, $c.findings, $c.distinct_values, @($c.paths).Count) }
Write-Output "commits=$($revs.Count) not_in_public_ref=$(@($newRevs).Count) identities=$($ids.Count) binary_paths=$($binary.Count) originals=$($originals.Count)"
Write-Output "RESULT=$(if ($secret.Count) { 'SECRET_FOUND' } else { 'NO_SECRET_PATTERN' }) commit=$full out=$Out"
exit $(if ($secret.Count) { 1 } else { 0 })
