<#
.SYNOPSIS
  A-1 post-publication ref check: the refs a remote really has must equal the approved set exactly (read-only; nothing is pushed).

.DESCRIPTION
  Runs "git ls-remote --refs <Remote>". --refs lists only real refs: no HEAD (a symbolic pseudo-ref the new repository always
  advertises) and no peeled "^{}" rows (which an annotated tag adds), so neither a normal first push nor an approved annotated tag
  can be mistaken for an extra ref (A1-R02). Every listed ref must be one of -Expect with exactly that object ID; an expected ref
  that is missing, a different ID, any other ref (another branch, an unapproved tag) or a ref listed twice fails. A failed or
  unparseable query is ERROR, never PASS. Ref names are compared ordinal, case included (A1-FIX2-01): refs/tags/V-approved does
  not approve refs/tags/v-approved, and two refs that differ only in case stay two refs.

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

if ($Remote.StartsWith('-', [StringComparison]::Ordinal)) { Fail-Usage "remote '$Remote' looks like an option." }
# A1-FIX2-01: ref names are compared byte for byte. PowerShell's own collections and operators ([ordered]@{}, @{}, -eq, -ne, -match,
# -contains, Sort-Object -Unique, Compare-Object) ignore case by default, so every ref-name lookup goes through these Ordinal
# dictionaries and every pattern through -cmatch; refs/tags/V-approved and refs/tags/v-approved are two different refs (as on a
# case-sensitive server or a reftable repository). Object IDs are compared separately, also ordinal, after a lower-case-hex check.
$want = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
foreach ($x in @($Expect | ForEach-Object { $_ -split '\|' } | Where-Object { $_ })) {
    $cut = $x.LastIndexOf('=')                                                # a ref name may contain '='; an object ID never does
    if ($cut -lt 1) { Fail-Usage "expectation '$x' must be <ref>=<object id>." }
    $ref = $x.Substring(0, $cut); $id = $x.Substring($cut + 1)
    if ($ref -cnotmatch '^refs/(heads/main|tags/[^\s^~:?*\[\\]+)$') { Fail-Usage "expected ref '$ref' must be refs/heads/main or refs/tags/<name>." }
    if ($id -cnotmatch '^([0-9a-f]{40}|[0-9a-f]{64})$') { Fail-Usage "object ID for '$ref' must be a full lower-case hex ID." }
    if ($want.ContainsKey($ref)) { Fail-Usage "'$ref' expected twice." }
    $want.Add($ref, $id)
}
if (-not $want.ContainsKey('refs/heads/main')) { Fail-Usage 'exactly one refs/heads/main is required.' }

$ErrorActionPreference = 'Continue'
$lines = @(& $Git ls-remote --refs $Remote 2>&1 | ForEach-Object { "$_" })
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($code -ne 0) {
    foreach ($l in $lines) { Write-Output "    $l" }
    Write-Output "RESULT=ERROR ls-remote exited $code (not verified)"
    exit 2
}
$have = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
$bad = New-Object System.Collections.Generic.List[string]
foreach ($l in @($lines | Where-Object { $_ })) {
    if ($l -cnotmatch '^([0-9a-f]{40}|[0-9a-f]{64})\t(refs/\S+)$') { Write-Output "    unparseable: $l"; Write-Output 'RESULT=ERROR unexpected ls-remote output (not verified)'; exit 2 }
    if ($have.ContainsKey($Matches[2])) { $bad.Add("DUPLICATE $($Matches[2]) listed twice ($($have[$Matches[2]]), $($Matches[1]))"); continue }    # never overwritten
    $have.Add($Matches[2], $Matches[1])
}
$haveRefs = [string[]]@($have.Keys); [Array]::Sort($haveRefs, [StringComparer]::Ordinal)
$wantRefs = [string[]]@($want.Keys); [Array]::Sort($wantRefs, [StringComparer]::Ordinal)
foreach ($ref in $haveRefs) {
    if (-not $want.ContainsKey($ref)) { $bad.Add("UNEXPECTED $ref $($have[$ref])") }
    elseif (-not [string]::Equals($have[$ref], $want[$ref], [StringComparison]::Ordinal)) { $bad.Add("MISMATCH $ref remote=$($have[$ref]) expected=$($want[$ref])") }
}
foreach ($ref in $wantRefs) { if (-not $have.ContainsKey($ref)) { $bad.Add("MISSING $ref expected=$($want[$ref])") } }
foreach ($ref in $haveRefs) { Write-Output ("{0} {1}" -f $have[$ref], $ref) }
foreach ($b in $bad) { Write-Output "    - $b" }
Write-Output "RESULT=$(if ($bad.Count) { 'FAIL' } else { 'PASS' }) remote_refs=$($have.Count) expected=$($want.Count) bad=$($bad.Count)"
exit $(if ($bad.Count) { 1 } else { 0 })
