<#
.SYNOPSIS
  A-1 post-publication ref check: the refs a remote really has must equal the approved set exactly (read-only; nothing is pushed).

.DESCRIPTION
  Runs "git ls-remote --refs <Remote>". --refs lists only real refs: no HEAD (a symbolic pseudo-ref the new repository always
  advertises) and no peeled "^{}" rows (which an annotated tag adds), so neither a normal first push nor an approved annotated tag
  can be mistaken for an extra ref (A1-R02). Every listed ref must be one of -Expect with exactly that object ID; an expected ref
  that is missing, a different ID, or any other ref (another branch, an unapproved tag) fails. A failed or unparseable query is
  ERROR, never PASS.

  -Expect takes "<ref>=<object id>" entries (also "|"-separated): exactly one refs/heads/main and any OWNER-approved refs/tags/<name>.
  The ID is the ref's own object ID as "git rev-parse <ref>" prints it locally: the commit for main and for a lightweight tag, the
  tag object (not the peeled commit) for an annotated tag. -Git is the git executable (the approved pinned one in operation).

  Exit: 0 PASS, 1 FAIL, 2 usage or ERROR.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Remote,
    [Parameter(Mandatory = $true)][string[]]$Expect,
    [string]$Git = 'git.exe'
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

function Fail-Usage([string]$m) { [Console]::Error.WriteLine("usage: $m"); exit 2 }

if ($Remote.StartsWith('-')) { Fail-Usage "remote '$Remote' looks like an option." }
$want = [ordered]@{}
foreach ($x in @($Expect | ForEach-Object { $_ -split '\|' } | Where-Object { $_ })) {
    $ref, $id = $x -split '=', 2
    if ($ref -notmatch '^refs/(heads/main|tags/[^\s^~:?*\[\\]+)$') { Fail-Usage "expected ref '$ref' must be refs/heads/main or refs/tags/<name>." }
    if ($id -notmatch '^([0-9a-f]{40}|[0-9a-f]{64})$') { Fail-Usage "object ID for '$ref' must be a full lower-case hex ID." }
    if ($want.Contains($ref)) { Fail-Usage "'$ref' expected twice." }
    $want[$ref] = $id
}
if (-not $want.Contains('refs/heads/main')) { Fail-Usage 'exactly one refs/heads/main is required.' }

$ErrorActionPreference = 'Continue'
$lines = @(& $Git ls-remote --refs $Remote 2>&1 | ForEach-Object { "$_" })
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($code -ne 0) {
    foreach ($l in $lines) { Write-Output "    $l" }
    Write-Output "RESULT=ERROR ls-remote exited $code (not verified)"
    exit 2
}
$have = [ordered]@{}
foreach ($l in @($lines | Where-Object { $_ })) {
    if ($l -notmatch '^([0-9a-f]{40}|[0-9a-f]{64})\t(refs/\S+)$') { Write-Output "    unparseable: $l"; Write-Output 'RESULT=ERROR unexpected ls-remote output (not verified)'; exit 2 }
    $have[$Matches[2]] = $Matches[1]
}
$bad = New-Object System.Collections.Generic.List[string]
foreach ($ref in $have.Keys) {
    if (-not $want.Contains($ref)) { $bad.Add("UNEXPECTED $ref $($have[$ref])") }
    elseif ($have[$ref] -ne $want[$ref]) { $bad.Add("MISMATCH $ref remote=$($have[$ref]) expected=$($want[$ref])") }
}
foreach ($ref in $want.Keys) { if (-not $have.Contains($ref)) { $bad.Add("MISSING $ref expected=$($want[$ref])") } }
foreach ($ref in $have.Keys) { Write-Output ("{0} {1}" -f $have[$ref], $ref) }
foreach ($b in $bad) { Write-Output "    - $b" }
Write-Output "RESULT=$(if ($bad.Count) { 'FAIL' } else { 'PASS' }) remote_refs=$($have.Count) expected=$($want.Count) bad=$($bad.Count)"
exit $(if ($bad.Count) { 1 } else { 0 })
