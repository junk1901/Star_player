<#
.SYNOPSIS
  A-1 pre-publication scan of one commit and all its ancestors (read-only; nothing is fetched, pushed or changed).

.DESCRIPTION
  Every blob reachable from -Commit is in the tree of some ancestor commit, so the text patterns run with "git grep -P" over all
  ancestor commits (binary files are skipped by -I and listed separately). Every ancestor commit object is also read raw and its
  author name / e-mail, committer name / e-mail, other header lines and whole message are scanned with the same patterns.

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

# commit metadata: every ancestor commit object is read as raw bytes ("git cat-file commit") and split at the first empty line, so no
# message content (separators, control characters, blank lines) can cut a field short. Fields: author name / e-mail, committer
# name / e-mail, every other header line (encoding, gpgsig, mergetag, ...) and the whole message.
function Get-GitBytes([string[]]$a) {
    $psi = New-Object Diagnostics.ProcessStartInfo('git.exe')
    $psi.Arguments = (@('-C', "`"$Repo`"") + $a) -join ' '
    $psi.UseShellExecute = $false; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true; $psi.CreateNoWindow = $true
    $proc = [Diagnostics.Process]::Start($psi)
    $ms = New-Object IO.MemoryStream
    $err = $proc.StandardError.ReadToEndAsync()
    $proc.StandardOutput.BaseStream.CopyTo($ms)
    $proc.WaitForExit()
    if ($proc.ExitCode -ne 0) { throw "git $($a -join ' ') exited $($proc.ExitCode): $($err.Result)" }
    $ms.ToArray()
}
$ids = @{}
$metaFields = 0
foreach ($rev in $revs) {
    $text = $utf8.GetString((Get-GitBytes @('cat-file', 'commit', $rev)))
    $cut = $text.IndexOf("`n`n")
    $head = if ($cut -ge 0) { $text.Substring(0, $cut) } else { $text }
    $fields = New-Object System.Collections.Generic.List[object]
    $fields.Add(@('message', $(if ($cut -ge 0) { $text.Substring($cut + 2) } else { '' })))
    $other = New-Object System.Collections.Generic.List[string]
    foreach ($h in ($head -split "`n")) {
        $m = [regex]::Match($h, '^(author|committer) (.*) <([^>]*)> (-?\d+) ([+-]\d{4})$')
        if ($m.Success) {
            $fields.Add(@("$($m.Groups[1].Value)_name", $m.Groups[2].Value)); $fields.Add(@("$($m.Groups[1].Value)_email", $m.Groups[3].Value))
            $who = "$($m.Groups[2].Value) <$($m.Groups[3].Value)>"; $ids[$who] = 1 + $(if ($ids.ContainsKey($who)) { $ids[$who] } else { 0 })
        }
        elseif ($h -match '^(author|committer) ') { $fields.Add(@("$($Matches[1])_unparsed", $h)) }
        elseif ($h -notmatch '^(tree|parent) [0-9a-f]{40}$') { $other.Add($h) }
    }
    if ($other.Count) { $fields.Add(@('header_other', ($other -join "`n"))) }
    foreach ($fv in $fields) {
        $metaFields++
        foreach ($name in $classes.Keys) {
            foreach ($mm in [regex]::Matches([string]$fv[1], $classes[$name].re)) {
                $key = "$name|commit:$($fv[0])|$($mm.Value)"
                if ($seen.ContainsKey($key)) { continue }
                $seen[$key] = $true
                $findings.Add([pscustomobject]@{ class = $name; kind = $classes[$name].kind; where = "commit:$($fv[0])"; path = "(commit:$($fv[0]))"; line = 0; first_seen_in = $rev; value = $mm.Value })
            }
        }
    }
}

$binary = @(Invoke-Git @('log', '--numstat', '--format=', $full) | Where-Object { $_ -match "^-`t-`t" } | ForEach-Object { ($_ -split "`t", 3)[2] } | Sort-Object -Unique)
$originals = @(Invoke-Git @('ls-tree', '-r', '--name-only', $full, '--', 'docs/contracts/b1/originals'))

$tsv = @("kind`tclass`twhere`tpath`tline`tfirst_seen_in`tvalue") + @($findings | ForEach-Object { "$($_.kind)`t$($_.class)`t$($_.where)`t$($_.path)`t$($_.line)`t$($_.first_seen_in)`t$($_.value)" })
[IO.File]::WriteAllLines("$Out\findings.tsv", $tsv, $utf8)
$summary = [ordered]@{
    tool = 'b1-ops/a1-scan.ps1'; commit = $full; public_ref = $PublicRef; time_utc = [DateTime]::UtcNow.ToString('o')
    commits = $revs.Count; commits_not_in_public_ref = @($newRevs).Count; commit_fields_scanned = $metaFields
    scope = 'text blobs of every ancestor tree (git grep -I), and per commit: author name / e-mail, committer name / e-mail, other headers, whole message'
    meaning = 'pattern-based: 0 SECRET findings means no implemented pattern matched in this scope, not that no secret exists'
    identities = @($ids.Keys | Sort-Object | ForEach-Object { [ordered]@{ identity_sha256 = (Get-Sha $_); commits_fields = $ids[$_] } })
    classes = @(foreach ($name in $classes.Keys) {
            $g = @($findings | Where-Object { $_.class -eq $name })
            [ordered]@{ class = $name; kind = $classes[$name].kind; findings = $g.Count; distinct_values = @($g | ForEach-Object { $_.value } | Sort-Object -Unique).Count
                paths = @($g | ForEach-Object { $_.path } | Sort-Object -Unique)
                value_sha256 = @($g | ForEach-Object { Get-Sha $_.value } | Sort-Object -Unique) }
        })
    binary_paths = $binary; originals = $originals
}
[IO.File]::WriteAllText("$Out\summary.json", ($summary | ConvertTo-Json -Depth 5), $utf8)

$secret = @($findings | Where-Object { $_.kind -eq 'SECRET' })
foreach ($c in $summary.classes) { Write-Output ("{0,-8} {1,-19} findings={2,-5} distinct={3,-4} paths={4}" -f $c.kind, $c.class, $c.findings, $c.distinct_values, @($c.paths).Count) }
Write-Output "commits=$($revs.Count) not_in_public_ref=$(@($newRevs).Count) commit_fields_scanned=$metaFields identities=$($ids.Count) binary_paths=$($binary.Count) originals=$($originals.Count)"
Write-Output "RESULT=$(if ($secret.Count) { 'SECRET_FOUND' } else { 'NO_SECRET_PATTERN_FOUND (pattern-based; not proof that no secret exists)' }) commit=$full out=$Out"
exit $(if ($secret.Count) { 1 } else { 0 })
