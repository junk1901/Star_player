<#
.SYNOPSIS
  B1 G5 protected-root ACL tool: Plan | Verify (read-only) | Apply | Restore.

.DESCRIPTION
  Input is a layout file (format starplayer-g5-layout-v1, see docs/tasks/B1_G5_OPERATIONAL_DESIGN_DRAFT.md). This is G5 preparation
  tooling, not a B1-T00 contract schema; every SID and path in a real layout is an OWNER value.

  Verify rules (TCB = SYSTEM, BUILTIN\Administrators, NT SERVICE\TrustedInstaller, the same set as AnchorAcl):
    * every path component from the drive root to the root is not a reparse point;
    * ancestors: owner in TCB, and no allow ACE that applies to the ancestor gives a non-TCB SID
      DELETE, FILE_DELETE_CHILD, WRITE_DAC or WRITE_OWNER (the NSC-4 ancestor rule);
    * the root and every descendant (reparse points are reported and never followed): owner in TCB + the root's writers,
      a DACL is present, only plain allow/deny ACEs, and no allow ACE (inherited and inherit-only included) gives a SID outside
      TCB + writers any write-class right (data, append/add, EA, attributes, delete, delete child, WRITE_DAC, WRITE_OWNER);
      CREATOR OWNER / OWNER RIGHTS stand for the owner, a CREATOR GROUP write ACE is refused;
    * an "apply" root has a protected DACL (no inheritance from its parent); each reader has read+execute, each writer add-file
      and add-subdirectory on the root;
    * principal SIDs are distinct, outside the TCB and not direct members of BUILTIN\Administrators.

  Apply (OWNER only, elevated, -OwnerApproved): snapshot of the owner and DACL of every existing path under the apply roots ->
  each apply root gets the owner and protected DACL of the layout, every descendant is reset to inherit only -> full Verify.
  Any error or violation restores the snapshot, removes the directories Apply created and compares again
  (exit 1 = rolled back and identical, exit 3 = rollback incomplete).

  -DiagnosticRoot is for isolated tests only: every root must lie under it, the current user counts as trusted and is the
  owner Apply sets, the diagnostic root and its ancestors are not judged, elevation is not required and the result is
  labelled DIAGNOSTIC (never operational evidence).

  Exit: 0 PASS, 1 violations / rolled back, 2 usage or layout error, 3 rollback failed.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Plan', 'Verify', 'Apply', 'Restore')][string]$Mode,
    [string]$Layout,
    [string]$Snapshot,
    [string]$Report,
    [string]$DiagnosticRoot,
    [switch]$OwnerApproved
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

$Tcb = @('S-1-5-18', 'S-1-5-32-544', 'S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464')
$CreatorOwner = 'S-1-3-0'; $CreatorGroup = 'S-1-3-1'; $OwnerRights = 'S-1-3-4'
$WriteClass = [int64](0x2 -bor 0x4 -bor 0x10 -bor 0x40 -bor 0x100 -bor 0x10000 -bor 0x40000 -bor 0x80000)
$AncestorClass = [int64](0x40 -bor 0x10000 -bor 0x40000 -bor 0x80000)
$ReadExecute = [int64]0x1200A9
$AddEntries = [int64](0x2 -bor 0x4)
$Sections = [Security.AccessControl.AccessControlSections]'Owner, Access'

function Fail-Usage([string]$m) { [Console]::Error.WriteLine("usage: $m"); exit 2 }

function Get-Mapped([int64]$mask) {
    $m = $mask -band 0xFFFFFFFFL
    $r = $m -band 0x0DFFFFFFL
    if ($m -band 0x80000000L) { $r = $r -bor 0x120089L }
    if ($m -band 0x40000000L) { $r = $r -bor 0x120116L }
    if ($m -band 0x20000000L) { $r = $r -bor 0x1200A0L }
    if ($m -band 0x12000000L) { $r = $r -bor 0x1F01FFL }
    $r
}

function Get-Canonical([string]$p) { [IO.Path]::GetFullPath($p).TrimEnd('\') }

function Test-Under([string]$path, [string]$root) {
    $path.Equals($root, 'OrdinalIgnoreCase') -or $path.StartsWith($root.TrimEnd('\') + '\', 'OrdinalIgnoreCase')
}

function Test-Reparse([string]$p) { ([IO.File]::GetAttributes($p) -band [IO.FileAttributes]::ReparsePoint) -ne 0 }

function Get-Security([string]$p, [bool]$dir) {
    if ($dir) { [IO.Directory]::GetAccessControl($p, $Sections) } else { [IO.File]::GetAccessControl($p, $Sections) }
}

function Get-Sd([string]$p, [bool]$dir) {
    [Security.AccessControl.RawSecurityDescriptor]::new((Get-Security $p $dir).GetSecurityDescriptorBinaryForm(), 0)
}

# The first reason the descriptor lets a SID outside $allowed hold a $forbidden right, or $null.
function Get-Violation($sd, [string[]]$allowed, [int64]$forbidden, [bool]$ancestor) {
    if ($null -eq $sd.Owner -or $allowed -notcontains $sd.Owner.Value) {
        $o = if ($null -eq $sd.Owner) { '(none)' } else { $sd.Owner.Value }
        return "owner $o is not allowed"
    }
    if ($null -eq $sd.DiscretionaryAcl -or ($sd.ControlFlags -band [Security.AccessControl.ControlFlags]::DiscretionaryAclPresent) -eq 0) { return 'NULL DACL' }
    foreach ($ace in $sd.DiscretionaryAcl) {
        if (-not ($ace -is [Security.AccessControl.CommonAce]) -or ($ace.AceType -ne 'AccessAllowed' -and $ace.AceType -ne 'AccessDenied')) { return "unsupported ACE type $($ace.AceType)" }
        if ($ace.AceType -eq 'AccessDenied') { continue }
        $bad = (Get-Mapped $ace.AccessMask) -band $forbidden
        if ($bad -eq 0) { continue }
        $inheritOnly = ([int]$ace.AceFlags -band 0x08) -ne 0
        if ($ancestor -and $inheritOnly) { continue }                     # judged on the next component, which carries the inherited copy
        $sid = $ace.SecurityIdentifier.Value
        if ($sid -eq $CreatorOwner -or $sid -eq $OwnerRights) {
            if ($inheritOnly) { continue }                                  # the creator of a child must already hold an add right
            $sid = $sd.Owner.Value
        }
        elseif ($sid -eq $CreatorGroup) { return ('CREATOR GROUP gets 0x{0:x}' -f $bad) }
        if ($allowed -notcontains $sid) {
            $kind = if (([int]$ace.AceFlags -band 0x10) -ne 0) { 'inherited' } else { 'explicit' }
            return ('{0} gets 0x{1:x} ({2} ACE{3})' -f $sid, $bad, $kind, $(if ($inheritOnly) { ', inherit-only' } else { '' }))
        }
    }
    $null
}

function Get-Granted($sd, [string]$sid) {
    $g = [int64]0
    foreach ($ace in $sd.DiscretionaryAcl) {
        if ($ace -is [Security.AccessControl.CommonAce] -and $ace.AceType -eq 'AccessAllowed' -and $ace.SecurityIdentifier.Value -eq $sid -and
            ([int]$ace.AceFlags -band 0x08) -eq 0) { $g = $g -bor (Get-Mapped $ace.AccessMask) }
    }
    $g
}

# Root and descendants, top-down, never following a reparse point. Items: Path, Dir, Reparse, Error.
function Get-Tree([string]$root, [string[]]$stop = @()) {
    $out = New-Object System.Collections.Generic.List[object]
    $stack = New-Object System.Collections.Generic.Stack[string]
    $out.Add([pscustomobject]@{ Path = $root; Dir = $true; Reparse = (Test-Reparse $root); Error = $null })
    if (-not $out[0].Reparse) { $stack.Push($root) }
    while ($stack.Count -gt 0) {
        $d = $stack.Pop()
        try { $entries = @([IO.Directory]::GetFileSystemEntries($d)) | Sort-Object }
        catch { $out.Add([pscustomobject]@{ Path = $d; Dir = $true; Reparse = $false; Error = "cannot enumerate: $($_.Exception.Message)" }); continue }
        foreach ($e in $entries) {
            if ($stop -contains $e) { continue }                              # a nested layout root is judged by its own entry
            $attr = [IO.File]::GetAttributes($e)
            $isDir = ($attr -band [IO.FileAttributes]::Directory) -ne 0
            $rp = ($attr -band [IO.FileAttributes]::ReparsePoint) -ne 0
            $out.Add([pscustomobject]@{ Path = $e; Dir = $isDir; Reparse = $rp; Error = $null })
            if ($isDir -and -not $rp) { $stack.Push($e) }
        }
    }
    $out
}

function Read-Layout([string]$file) {
    if (-not $file) { Fail-Usage '-Layout is required.' }
    $bytes = [IO.File]::ReadAllBytes((Get-Canonical $file))
    try { $j = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json } catch { Fail-Usage "layout is not JSON: $($_.Exception.Message)" }
    if ($j.format -ne 'starplayer-g5-layout-v1') { Fail-Usage "layout format must be starplayer-g5-layout-v1." }
    $principals = @{}
    foreach ($p in $j.principals.PSObject.Properties) {
        if ($p.Value -notmatch '^S-1-\d+(-\d+)+$') { Fail-Usage "principal '$($p.Name)' is not a SID: '$($p.Value)'." }
        $principals[$p.Name] = [string]$p.Value
    }
    if ($j.owner_sid -notmatch '^S-1-\d+(-\d+)+$') { Fail-Usage 'owner_sid is not a SID.' }
    $ids = @{}
    $roots = foreach ($r in $j.roots) {
        if ($ids.ContainsKey($r.id)) { Fail-Usage "root id '$($r.id)' twice." }
        $ids[$r.id] = 1
        if ($r.path -notmatch '^[A-Za-z]:\\') { Fail-Usage "root '$($r.id)' path must be a local absolute path." }
        foreach ($n in @($r.writers) + @($r.readers)) { if ($n -and -not $principals.ContainsKey($n)) { Fail-Usage "root '$($r.id)' names unknown principal '$n'." } }
        [pscustomobject]@{
            Id = [string]$r.id; Path = (Get-Canonical $r.path); Apply = [bool]$r.apply
            Writers = @(@($r.writers) | Where-Object { $_ } | ForEach-Object { $principals[$_] })
            Readers = @(@($r.readers) | Where-Object { $_ } | ForEach-Object { $principals[$_] })
        }
    }
    [pscustomobject]@{ Owner = [string]$j.owner_sid; Principals = $principals; Roots = @($roots | Sort-Object { $_.Path.Length }); Sha256 = ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($bytes)) -replace '-', '').ToLowerInvariant() }
}

$me = [Security.Principal.WindowsIdentity]::GetCurrent()
$elevated = ([Security.Principal.WindowsPrincipal]$me).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$diag = $null
if ($DiagnosticRoot) {
    $diag = Get-Canonical $DiagnosticRoot
    if (-not [IO.Directory]::Exists($diag)) { Fail-Usage "diagnostic root '$diag' does not exist." }
}
$script:L = $null
$trusted = @($Tcb)
if ($diag) { $trusted += $me.User.Value }

function Build-Sddl($r, [string]$owner) {
    $d = 'D:PAI(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)'
    if ($diag) { $d += "(A;OICI;FA;;;$($me.User.Value))" }                 # the diagnostic stand-in for the TCB
    foreach ($w in $r.Writers) { $d += "(A;OICI;0x1301bf;;;$w)" }
    foreach ($x in $r.Readers) { if ($r.Writers -notcontains $x) { $d += "(A;OICI;0x1200a9;;;$x)" } }
    "O:$owner$d"
}

function Get-Nested($L, $r) { @($L.Roots | Where-Object { $_.Path -ne $r.Path -and (Test-Under $_.Path $r.Path) } | ForEach-Object { $_.Path }) }

function Invoke-Verify($L) {
    $results = New-Object System.Collections.Generic.List[object]
    $v = New-Object System.Collections.Generic.List[string]
    $sids = @($L.Principals.Values)
    if (@($sids | Sort-Object -Unique).Count -ne $sids.Count) { $v.Add('principals: two roles share one SID') }
    foreach ($k in $L.Principals.Keys) { if ($Tcb -contains $L.Principals[$k]) { $v.Add("principals: '$k' is a TCB SID") } }
    try {
        $admins = @(Get-LocalGroupMember -SID 'S-1-5-32-544' | ForEach-Object { $_.SID.Value })
        foreach ($k in $L.Principals.Keys) { if ($admins -contains $L.Principals[$k]) { $v.Add("principals: '$k' is a direct member of BUILTIN\Administrators") } }
        $adminCheck = 'checked (direct members only)'
    }
    catch { $adminCheck = "UNVERIFIED: $($_.Exception.Message)" }
    $results.Add([pscustomobject]@{ id = 'principals'; path = ''; status = $(if ($v.Count) { 'FAIL' } else { 'PASS' }); items = 0; violations = $v.ToArray(); note = "administrators membership $adminCheck" })

    foreach ($r in $L.Roots) {
        $rv = New-Object System.Collections.Generic.List[string]
        $count = 0
        if (-not [IO.Directory]::Exists($r.Path)) { $rv.Add("$($r.Path): missing") }
        else {
            # ancestors, drive root down to the parent
            $chain = New-Object System.Collections.Generic.List[string]
            $p = [IO.Directory]::GetParent($r.Path)
            while ($p) { $chain.Insert(0, $p.FullName.TrimEnd('\') + $(if ($p.Parent) { '' } else { '\' })); $p = $p.Parent }
            foreach ($a in $chain) {
                if ($diag -and (-not (Test-Under $a.TrimEnd('\') $diag) -or $a.TrimEnd('\') -eq $diag)) { continue }     # the diagnostic root and above are out of scope
                try {
                    if (Test-Reparse $a) { $rv.Add("$($a): ancestor is a reparse point"); continue }
                    $why = Get-Violation (Get-Sd $a $true) $trusted $AncestorClass $true
                    if ($why) { $rv.Add("$($a): ancestor: $why") }
                }
                catch { $rv.Add("$($a): ancestor unreadable: $($_.Exception.Message)") }
            }
            $allowed = @($trusted) + @($r.Writers)
            foreach ($item in (Get-Tree $r.Path (Get-Nested $L $r))) {
                $count++
                if ($item.Error) { $rv.Add("$($item.Path): $($item.Error)"); continue }
                if ($item.Reparse) { $rv.Add("$($item.Path): reparse point (not followed)"); continue }
                try {
                    $sd = Get-Sd $item.Path $item.Dir
                    $why = Get-Violation $sd $allowed $WriteClass $false
                    if ($why) { $rv.Add("$($item.Path): $why") }
                    if ($item.Path -eq $r.Path) {
                        if ($r.Apply -and ($sd.ControlFlags -band [Security.AccessControl.ControlFlags]::DiscretionaryAclProtected) -eq 0) { $rv.Add("$($item.Path): DACL is not protected (inherits from its parent)") }
                        foreach ($x in $r.Readers) { if (((Get-Granted $sd $x) -band $ReadExecute) -ne $ReadExecute) { $rv.Add("$($item.Path): reader $x lacks read+execute") } }
                        foreach ($w in $r.Writers) { if (((Get-Granted $sd $w) -band $AddEntries) -ne $AddEntries) { $rv.Add("$($item.Path): writer $w lacks add file/subdirectory") } }
                    }
                }
                catch { $rv.Add("$($item.Path): unreadable: $($_.Exception.Message)") }
            }
        }
        $results.Add([pscustomobject]@{ id = $r.Id; path = $r.Path; status = $(if ($rv.Count) { 'FAIL' } else { 'PASS' }); items = $count; violations = $rv.ToArray(); note = $(if ($r.Apply) { 'apply' } else { 'verify-only' }) })
    }
    $results
}

function Get-Snapshot($L) {
    $entries = New-Object System.Collections.Generic.List[object]
    foreach ($r in @($L.Roots | Where-Object { $_.Apply })) {
        if (-not [IO.Directory]::Exists($r.Path)) { $entries.Add([pscustomobject]@{ path = $r.Path; dir = $true; exists = $false; sddl = $null }); continue }
        foreach ($item in (Get-Tree $r.Path (Get-Nested $L $r))) {
            if ($item.Error) { throw "snapshot: $($item.Path): $($item.Error)" }
            if ($item.Reparse) { $entries.Add([pscustomobject]@{ path = $item.Path; dir = $item.Dir; exists = $true; sddl = $null; reparse = $true }); continue }
            $entries.Add([pscustomobject]@{ path = $item.Path; dir = $item.Dir; exists = $true; sddl = (Get-Security $item.Path $item.Dir).GetSecurityDescriptorSddlForm($Sections) })
        }
    }
    $entries
}

function Set-Descriptor([string]$p, [bool]$dir, [string]$sddl, $sections) {
    $s = if ($dir) { New-Object Security.AccessControl.DirectorySecurity } else { New-Object Security.AccessControl.FileSecurity }
    $s.SetSecurityDescriptorSddlForm($sddl, $sections)
    if ($dir) { [IO.Directory]::SetAccessControl($p, $s) } else { [IO.File]::SetAccessControl($p, $s) }
}

# Owner and DACL of every snapshot entry, parents first; returns the paths that still differ.
function Restore-Snapshot($entries) {
    foreach ($e in @($entries | Where-Object { $_.exists -and $_.sddl } | Sort-Object { $_.path.Length })) {
        if (-not (Test-Path -LiteralPath $e.path)) { continue }
        Set-Descriptor $e.path ([bool]$e.dir) $e.sddl ([Security.AccessControl.AccessControlSections]::Access)
        $want = ([Security.AccessControl.RawSecurityDescriptor]::new($e.sddl)).Owner.Value
        $now = (Get-Sd $e.path ([bool]$e.dir)).Owner.Value
        if ($want -ne $now) {
            & icacls.exe $e.path /setowner "*$want" /C /Q | Out-Null
            if ($LASTEXITCODE -ne 0) { Write-Warning "icacls /setowner failed for $($e.path)" }
        }
    }
    $diff = @(foreach ($e in @($entries | Where-Object { $_.exists -and $_.sddl })) {
        if (-not (Test-Path -LiteralPath $e.path)) { "$($e.path): missing after restore"; continue }
        $now = (Get-Security $e.path ([bool]$e.dir)).GetSecurityDescriptorSddlForm($Sections)
        if ($now -ne $e.sddl) { "$($e.path): $now != $($e.sddl)" }
    })
    foreach ($e in @($entries | Where-Object { -not $_.exists } | Sort-Object { $_.path.Length } -Descending)) {
        if (-not [IO.Directory]::Exists($e.path)) { continue }                # a root Apply created: removed again, only if still empty
        try { [IO.Directory]::Delete($e.path, $false) } catch { $diff += "$($e.path): created by Apply and not removed: $($_.Exception.Message)" }
    }
    $diff
}

function Write-Result($result, $rows, [string]$extra) {
    $doc = [ordered]@{
        tool = 'b1-ops/g5-acl.ps1'; mode = $Mode; result = $result; diagnostic = [bool]$diag; diagnostic_root = $diag
        elevated = $elevated; user_sid = $me.User.Value; computer = $env:COMPUTERNAME; time_utc = [DateTime]::UtcNow.ToString('o')
        layout_sha256 = $(if ($script:L) { $script:L.Sha256 } else { $null }); note = $extra; roots = @($rows | ForEach-Object { $_ })
    }
    foreach ($row in $rows) {
        Write-Output ("{0,-10} {1,-4} items={2,-6} {3}" -f $row.id, $row.status, $row.items, $row.path)
        foreach ($x in $row.violations) { Write-Output "    - $x" }
    }
    Write-Output "RESULT=$result$(if ($diag) { ' (DIAGNOSTIC)' })$(if ($extra) { " $extra" })"
    if ($Report) { [IO.File]::WriteAllText($Report, ($doc | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding($false))) }
}

if ($Mode -eq 'Restore') {
    if (-not $Snapshot -or -not (Test-Path -LiteralPath $Snapshot)) { Fail-Usage '-Snapshot <existing file> is required.' }
    $snap = Get-Content -LiteralPath $Snapshot -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $diag -and (-not $elevated -or -not $OwnerApproved)) { Fail-Usage 'Restore changes ACLs: run elevated with -OwnerApproved (or inside -DiagnosticRoot).' }
    if ($diag) { foreach ($e in $snap.entries) { if (-not (Test-Under $e.path $diag)) { Fail-Usage "snapshot path '$($e.path)' is outside the diagnostic root." } } }
    $diff = @(Restore-Snapshot $snap.entries)
    $rows = @([pscustomobject]@{ id = 'restore'; path = $Snapshot; status = $(if ($diff.Count) { 'FAIL' } else { 'PASS' }); items = @($snap.entries).Count; violations = $diff; note = '' })
    Write-Result $(if ($diff.Count) { 'RESTORE_INCOMPLETE' } else { 'RESTORED' }) $rows ''
    exit $(if ($diff.Count) { 3 } else { 0 })
}

$script:L = Read-Layout $Layout
if ($diag) { foreach ($r in $L.Roots) { if (-not (Test-Under $r.Path $diag)) { Fail-Usage "root '$($r.Id)' ($($r.Path)) is outside the diagnostic root." } } }
$owner = if ($diag) { $me.User.Value } else { $L.Owner }

if ($Mode -eq 'Plan') {
    foreach ($r in $L.Roots) {
        if ($r.Apply) { Write-Output "$($r.Id) $($r.Path)`n    create if missing; owner+DACL := $(Build-Sddl $r $owner)`n    every descendant: owner := $owner, explicit ACEs removed, inherit only" }
        else { Write-Output "$($r.Id) $($r.Path)`n    verify only (no change)" }
    }
    Write-Output 'RESULT=PLAN (nothing changed)'
    exit 0
}

if ($Mode -eq 'Verify') {
    $rows = Invoke-Verify $L
    $fail = @($rows | Where-Object { $_.status -ne 'PASS' }).Count -gt 0
    Write-Result $(if ($fail) { 'FAIL' } else { 'PASS' }) $rows ''
    exit $(if ($fail) { 1 } else { 0 })
}

# Apply
if (-not $diag -and (-not $elevated -or -not $OwnerApproved)) { Fail-Usage 'Apply changes ACLs: run elevated with -OwnerApproved on the approved machine (or inside -DiagnosticRoot).' }
if (-not $Snapshot) { Fail-Usage '-Snapshot <new file> is required for Apply.' }
if (Test-Path -LiteralPath $Snapshot) { Fail-Usage "snapshot '$Snapshot' already exists (never overwritten)." }
$entries = Get-Snapshot $L
[IO.File]::WriteAllText($Snapshot, ([ordered]@{ format = 'starplayer-g5-acl-snapshot-v1'; layout_sha256 = $L.Sha256; time_utc = [DateTime]::UtcNow.ToString('o'); entries = @($entries) } | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
try {
    foreach ($r in @($L.Roots | Where-Object { $_.Apply })) {
        if (-not [IO.Directory]::Exists($r.Path)) { [void][IO.Directory]::CreateDirectory($r.Path) }
        if (Test-Reparse $r.Path) { throw "$($r.Path) is a reparse point" }
        Set-Descriptor $r.Path $true (Build-Sddl $r $owner) $Sections
        foreach ($item in @(Get-Tree $r.Path (Get-Nested $L $r) | Select-Object -Skip 1)) {
            if ($item.Error) { throw "$($item.Path): $($item.Error)" }
            if ($item.Reparse) { continue }                                    # left alone; Verify refuses it
            Set-Descriptor $item.Path $item.Dir "O:$($owner)D:AI" $Sections
        }
    }
    $rows = Invoke-Verify $L
    $bad = @($rows | Where-Object { $_.status -ne 'PASS' })
    if ($bad.Count) { throw ('post-apply verify failed: ' + ((@($bad | ForEach-Object { $_.violations }) | Select-Object -First 5) -join '; ')) }
    Write-Result 'PASS' $rows "snapshot=$Snapshot"
    exit 0
}
catch {
    $why = $_.Exception.Message
    $diff = @(Restore-Snapshot $entries)
    $rows = @(Invoke-Verify $L)
    if ($diff.Count) { Write-Result 'ROLLBACK_FAILED' $rows "cause=$why; residue=$($diff -join '; ')"; exit 3 }
    Write-Result 'ROLLED_BACK' $rows "cause=$why"
    exit 1
}
