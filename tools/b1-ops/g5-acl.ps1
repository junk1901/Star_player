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
    * principal SIDs are distinct, outside the TCB and not direct members of BUILTIN\Administrators. A failed membership lookup is
      a violation (never PASS); nested group membership is not resolved (a stated limit, reported as a note).

  Apply (OWNER only, elevated, -OwnerApproved) runs a PREFLIGHT before any snapshot, creation or change; any preflight error refuses
  Apply with nothing written (result REFUSED, exit 1):
    * every existing component from the drive root to each apply root is opened through a handle that does not share delete
      (reparse points are opened, not followed) and held until Apply ends, so no component can be renamed, deleted or swapped;
      through that handle it must not be a reparse point and its final path must equal the expected path (a junction, mount point
      or short name anywhere on the way is refused; containment in -DiagnosticRoot is therefore real, not a string prefix);
    * every existing ancestor that is not itself an apply root of this layout must already satisfy the ancestor rule (a trusted
      parent: only the TCB can rename, delete or re-permission it);
    * no reparse point and no unreadable entry under an existing apply root;
    * every object Apply will change (each existing apply root and every entry under it) is opened and held through its own
      handle, and through that handle (G5-FIX-01): the volume is NTFS, the link count is exactly 1 and the file ID is non-zero
      and unique among all objects to change. A file with a second hard link (in a non-applied root such as R-ARCHIVE, another
      directory or the same root) or whose link count / file ID cannot be read is refused: changing its descriptor would change
      every other path of the same object;
    * a root of the layout nested under an apply root and not applied itself (e.g. R-ARCHIVE under R-BASE) must already exist with
      a protected DACL, so that it never inherits the new ACEs (a layout that needs it changed is refused instead of changing a
      root the OWNER did not approve); its whole tree is also compared before / after (detection only);
    * outside -DiagnosticRoot (or with the 'maintenance-check' injection): no running process is owned by a layout principal.
  Right before the first change every held object is queried again (link count 1, same file ID); a difference refuses Apply
  with nothing written. Changes then go through handles only and never propagate (SetKernelObjectSecurity, nothing is walked by
  name): a missing directory is created already carrying its final protected descriptor, a root's owner and protected DACL are
  written on its held handle, and every entry below gets owner + the inherited copy of its root's ACEs (exactly what Windows
  would compute for these ACEs) on its own handle, after the same link-count / file-ID check on that handle. An entry that
  appears after the preflight is checked the same way before it is touched. After the last change every handle is queried once
  more; a link added meanwhile fails Apply and rolls the object back through the same handle. A full Verify and the nested-root
  comparison follow. On any failure every snapshot entry is restored independently through its held handle, or a fresh handle
  whose file ID must equal the snapshot (one failure does not stop the others), the directories Apply created (journal
  <snapshot>.created, intermediate directories included, pre-existing paths never) are removed if empty, and the real state is
  compared with the snapshot again: exit 1 ROLLED_BACK only when nothing differs, no created directory remains and no object
  outside the snapshot was changed, otherwise exit 3 ROLLBACK_FAILED with every residue listed. This is a best-effort
  compensation, not a transaction: a crash or power loss during Apply leaves the snapshot and journal for Restore.

  Remaining limits (OWNER operating preconditions, see the G5 design 4.3 / 5): the held handles stop renames and deletes of the
  checked objects and every change is bound to a checked handle, but a principal that can still write inside a root, or create
  a link to one of its files, can add entries or links while Apply runs (detected and rolled back, not prevented); Apply
  therefore requires the maintenance condition (no principal process; the OWNER also signs principals out or disables them).
  An unknown process owner is reported, not treated as a principal.

  -DiagnosticRoot is for isolated tests only: every root must lie under it, the current user counts as trusted and is the
  owner Apply sets, the diagnostic root and its ancestors are not judged by the ancestor rule (they are still reparse-checked),
  elevation is not required and the result is labelled DIAGNOSTIC (never operational evidence). -DiagnosticInject (only with
  -DiagnosticRoot) injects faults for the self-test: apply-fail:<path>, restore-fail:<path>, drop-file:<path>,
  swap-after-check:<path>, link-query-fail:<path>, links-misreported:<path> (the link count is ignored, so only the
  file-ID cross-check remains), add-link-after-check:<new>=<existing>, add-link-after-change:<new>=<existing>,
  admin-lookup-fail, maintenance-check (paths relative to the diagnostic root).

  Exit: 0 PASS, 1 violations / refused / rolled back, 2 usage or layout error, 3 rollback failed or unexpected error after a change.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Plan', 'Verify', 'Apply', 'Restore')][string]$Mode,
    [string]$Layout,
    [string]$Snapshot,
    [string]$Report,
    [string]$DiagnosticRoot,
    [string[]]$DiagnosticInject = @(),
    [switch]$OwnerApproved
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'
$script:changed = $false
trap {
    [Console]::Error.WriteLine("unexpected error: $($_.Exception.Message)")
    exit $(if ($script:changed) { 3 } else { 2 })
}
if (-not ('B1Ops.Native' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'B1OpsNative.cs') }

$Tcb = @('S-1-5-18', 'S-1-5-32-544', 'S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464')
$CreatorOwner = 'S-1-3-0'; $CreatorGroup = 'S-1-3-1'; $OwnerRights = 'S-1-3-4'
$WriteClass = [int64](0x2 -bor 0x4 -bor 0x10 -bor 0x40 -bor 0x100 -bor 0x10000 -bor 0x40000 -bor 0x80000)
$AncestorClass = [int64](0x40 -bor 0x10000 -bor 0x40000 -bor 0x80000)
$ReadExecute = [int64]0x1200A9
$AddEntries = [int64](0x2 -bor 0x4)
$Sections = [Security.AccessControl.AccessControlSections]'Owner, Access'
$ReadOnly = [uint32][B1Ops.Native]::ReadControl
$Change = [uint32]([B1Ops.Native]::ReadControl -bor [B1Ops.Native]::WriteDac -bor [B1Ops.Native]::WriteOwner)
$utf8 = New-Object Text.UTF8Encoding($false)

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

# Drive root .. path, e.g. C:\, C:\a, C:\a\b
function Get-Chain([string]$path) {
    $chain = New-Object System.Collections.Generic.List[string]
    $d = New-Object IO.DirectoryInfo($path)
    while ($d) { $chain.Insert(0, $(if ($d.Parent) { $d.FullName.TrimEnd('\') } else { $d.FullName })); $d = $d.Parent }
    $chain
}

# Opens through a handle (reparse not followed, no delete sharing); refuses a reparse point and a final path other than $p.
function Open-Checked([string]$p, [uint32]$access) {
    $h = [B1Ops.Native]::Open($p, $access)
    try {
        $info = [B1Ops.Native]::Query($h)
        if ($info.IsReparse) { throw "$($p): reparse point" }
        $final = [B1Ops.Native]::FinalPath($h).TrimEnd('\')
        if (-not $final.Equals($p.TrimEnd('\'), 'OrdinalIgnoreCase')) { throw "$($p): final path is '$final' (junction, mount point or short name on the way)" }
        [pscustomobject]@{ H = $h; Info = $info }
    }
    catch { $h.Dispose(); throw }
}

function Get-HandleSddl([string]$p) {
    $o = Open-Checked $p $ReadOnly
    try { [B1Ops.Native]::GetSddl($o.H) } finally { $o.H.Dispose() }
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
    $ids = @{}; $paths = @{}
    $roots = foreach ($r in $j.roots) {
        if ($ids.ContainsKey($r.id)) { Fail-Usage "root id '$($r.id)' twice." }
        $ids[$r.id] = 1
        if ($r.path -notmatch '^[A-Za-z]:\\') { Fail-Usage "root '$($r.id)' path must be a local absolute path." }
        $canon = Get-Canonical $r.path
        if ($paths.ContainsKey($canon.ToLowerInvariant())) { Fail-Usage "root path '$canon' twice." }
        $paths[$canon.ToLowerInvariant()] = 1
        foreach ($n in @($r.writers) + @($r.readers)) { if ($n -and -not $principals.ContainsKey($n)) { Fail-Usage "root '$($r.id)' names unknown principal '$n'." } }
        [pscustomobject]@{
            Id = [string]$r.id; Path = $canon; Apply = [bool]$r.apply
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
$DiagnosticInject = @($DiagnosticInject | ForEach-Object { $_ -split '\|' } | Where-Object { $_ })
if ($DiagnosticInject.Count -and -not $diag) { Fail-Usage '-DiagnosticInject is only accepted together with -DiagnosticRoot.' }
$inject = @{}
foreach ($x in $DiagnosticInject) {
    $kind, $arg = $x -split ':', 2
    if ($kind -notin 'apply-fail', 'restore-fail', 'drop-file', 'swap-after-check', 'link-query-fail', 'links-misreported', 'add-link-after-check', 'add-link-after-change', 'admin-lookup-fail', 'maintenance-check') { Fail-Usage "unknown injection '$kind'." }
    if (-not $inject.ContainsKey($kind)) { $inject[$kind] = @() }
    if ($kind -like 'add-link-*') {
        $new, $old = $arg -split '=', 2
        if (-not $old) { Fail-Usage "$kind needs <new>=<existing>." }
        $inject[$kind] += , @((Get-Canonical (Join-Path $diag $new)), (Get-Canonical (Join-Path $diag $old)))
    }
    elseif ($arg) { $inject[$kind] += (Get-Canonical (Join-Path $diag $arg)) } else { $inject[$kind] += '*' }
}
function Injected([string]$kind, [string]$path) { $inject.ContainsKey($kind) -and ($inject[$kind] -contains '*' -or ($path -and $inject[$kind] -contains $path)) }
function Add-InjectedLinks([string]$kind) {                                   # a hard link created by someone else (diagnostic)
    if (-not $inject.ContainsKey($kind)) { return @() }
    foreach ($x in $inject[$kind]) {
        try { New-Item -ItemType HardLink -Path $x[0] -Target $x[1] -ErrorAction Stop | Out-Null; "LINKED $($x[0]) -> $($x[1])" } catch { "LINK FAILED $($x[0]): $($_.Exception.Message)" }
    }
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

# An intermediate directory Apply has to create: TCB only (principals reach the roots below by traverse).
function Intermediate-Sddl([string]$owner) { "O:$($owner)D:PAI(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)$(if ($diag) { "(A;OICI;FA;;;$($me.User.Value))" })" }

# The descriptor of an entry below root $r: owner + the inherited copy of the root's ACEs. Every root ACE is OICI (no IO / NP), so a
# directory inherits it as OICIID and a file as ID, in the same order: what Windows computes (self-test acl.fix01_exact_*).
function Child-Sddl($r, [string]$owner, [bool]$dir) {
    $aces = (Build-Sddl $r $owner) -replace '^.*?D:PAI', ''
    "O:$($owner)D:AI" + ($aces -replace ';OICI;', $(if ($dir) { ';OICIID;' } else { ';ID;' }))
}

# G5-FIX-01: an object may be changed only if, through its own handle, it has exactly one link and a file ID that no other object
# of this Apply has; otherwise its descriptor is shared with another path. Returns "<volume>:<file id>" or throws.
$script:fsByVolume = @{}
function Get-ObjectKey([string]$p, $h) {
    if (Injected 'link-query-fail' $p) { throw "$($p): link count / file ID could not be read (injected, diagnostic) - refused" }
    $i = [B1Ops.Native]::Query($h)
    if (-not $script:fsByVolume.ContainsKey($i.VolumeSerial)) { $script:fsByVolume[$i.VolumeSerial] = [B1Ops.Native]::FileSystem($h) }
    if ($script:fsByVolume[$i.VolumeSerial] -ne 'NTFS') { throw "$($p): volume file system '$($script:fsByVolume[$i.VolumeSerial])' - link count / 64-bit file ID not trusted, refused" }
    if ($i.FileId -eq 0) { throw "$($p): file ID 0 - not trusted, refused" }
    if ($i.Links -ne 1 -and -not (Injected 'links-misreported' $p)) { throw "$($p): $($i.Links) hard links - its descriptor is shared with another path, refused" }
    '{0:x8}:{1:x16}' -f $i.VolumeSerial, $i.FileId
}
function Register-Object([hashtable]$seen, [string]$key, [string]$p) {
    if ($seen.ContainsKey($key) -and $seen[$key] -ne $p) { throw "$($p): same file ID as $($seen[$key]) - two paths to one object, refused" }
    $seen[$key] = $p
}

function Get-Nested($L, $r) { @($L.Roots | Where-Object { $_.Path -ne $r.Path -and (Test-Under $_.Path $r.Path) } | ForEach-Object { $_.Path }) }

function Get-PrincipalProcesses([string[]]$sids) {
    $hits = New-Object System.Collections.Generic.List[string]; $unknown = 0
    foreach ($p in @(Get-CimInstance Win32_Process)) {
        try {
            $r = Invoke-CimMethod -InputObject $p -MethodName GetOwnerSid -ErrorAction Stop
            if ($r.ReturnValue -ne 0) { $unknown++ } elseif ($sids -contains $r.Sid) { $hits.Add("$($p.Name) pid=$($p.ProcessId) owner=$($r.Sid)") }
        }
        catch { $unknown++ }
    }
    [pscustomobject]@{ Hits = $hits.ToArray(); Unknown = $unknown }
}

function Invoke-Verify($L) {
    $results = New-Object System.Collections.Generic.List[object]
    $v = New-Object System.Collections.Generic.List[string]
    $sids = @($L.Principals.Values)
    if (@($sids | Sort-Object -Unique).Count -ne $sids.Count) { $v.Add('principals: two roles share one SID') }
    foreach ($k in $L.Principals.Keys) { if ($Tcb -contains $L.Principals[$k]) { $v.Add("principals: '$k' is a TCB SID") } }
    try {
        if (Injected 'admin-lookup-fail' $null) { throw 'injected lookup failure (diagnostic)' }
        $admins = @(Get-LocalGroupMember -SID 'S-1-5-32-544' -ErrorAction Stop | ForEach-Object { $_.SID.Value })
        foreach ($k in $L.Principals.Keys) { if ($admins -contains $L.Principals[$k]) { $v.Add("principals: '$k' is a direct member of BUILTIN\Administrators") } }
        $adminNote = 'direct members of BUILTIN\Administrators checked; nested group membership NOT resolved (check whoami /groups per account)'
    }
    catch {
        $v.Add("principals: BUILTIN\Administrators membership could not be verified ($($_.Exception.Message)) - not a PASS")
        $adminNote = 'membership lookup failed'
    }
    $results.Add([pscustomobject]@{ id = 'principals'; path = ''; status = $(if ($v.Count) { 'FAIL' } else { 'PASS' }); items = 0; violations = $v.ToArray(); note = $adminNote })

    foreach ($r in $L.Roots) {
        $rv = New-Object System.Collections.Generic.List[string]
        $count = 0
        if (-not [IO.Directory]::Exists($r.Path)) { $rv.Add("$($r.Path): missing") }
        else {
            foreach ($a in @(Get-Chain $r.Path | Select-Object -SkipLast 1)) {
                try {
                    if (Test-Reparse $a) { $rv.Add("$($a): ancestor is a reparse point"); continue }
                    if ($diag -and (-not (Test-Under $a.TrimEnd('\') $diag) -or $a.TrimEnd('\') -eq $diag)) { continue }     # the diagnostic root and above: reparse only
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

# ---------------------------------------------------------------- preflight (G5-R01 / R02): nothing is written here
function Invoke-Preflight($L, [string]$owner) {
    $errors = New-Object System.Collections.Generic.List[string]
    $held = @{}                                                                # lower-case path -> held handle (closed when Apply ends)
    $plan = New-Object System.Collections.Generic.List[object]                 # directories to create, parents first
    $baseline = New-Object System.Collections.Generic.List[object]             # non-applied nested roots: SDDL of the whole tree
    $items = New-Object System.Collections.Generic.List[object]                # every existing object Apply will change (held, checked)
    $seen = @{}                                                                # object key -> path
    $applyPaths = @($L.Roots | Where-Object { $_.Apply } | ForEach-Object { $_.Path })
    function Hold([string]$p, [uint32]$access) {
        $k = $p.ToLowerInvariant()
        if ($held.ContainsKey($k)) { return $held[$k] }
        $o = Open-Checked $p $access
        $held[$k] = $o
        $o
    }
    foreach ($r in @($L.Roots | Where-Object { $_.Apply })) {
        $missing = $false
        foreach ($c in (Get-Chain $r.Path)) {
            $isRoot = $c -eq $r.Path
            if (-not $missing -and -not [IO.Directory]::Exists($c)) {
                if ([IO.File]::Exists($c)) { $errors.Add("$($c): is a file, not a directory"); break }
                $missing = $true
            }
            if ($missing) {
                if (-not @($plan | Where-Object { $_.Path -eq $c }).Count) {
                    $root = @($L.Roots | Where-Object { $_.Path -eq $c })
                    if ($root.Count -and -not $root[0].Apply) { $errors.Add("$($c): a root that is not applied would have to be created"); break }
                    $plan.Add([pscustomobject]@{ Path = $c; Sddl = $(if ($root.Count) { Build-Sddl $root[0] $owner } else { Intermediate-Sddl $owner }); Root = [bool]$root.Count })
                }
                continue
            }
            try { $o = Hold $c $(if ($applyPaths -contains $c) { $Change } else { $ReadOnly }) }
            catch { $errors.Add("$($c): $($_.Exception.Message)"); break }
            if (-not $o.Info.IsDirectory) { $errors.Add("$($c): not a directory"); break }
            if (-not $isRoot -and $applyPaths -notcontains $c -and -not ($diag -and (-not (Test-Under $c.TrimEnd('\') $diag) -or $c.TrimEnd('\') -eq $diag))) {
                $why = Get-Violation ([Security.AccessControl.RawSecurityDescriptor]::new([B1Ops.Native]::GetSddl($o.H))) $trusted $AncestorClass $true
                if ($why) { $errors.Add("$($c): untrusted ancestor ($why): a component outside the TCB's control could be swapped") }
            }
        }
        if (-not $missing -and [IO.Directory]::Exists($r.Path)) {
            foreach ($item in (Get-Tree $r.Path (Get-Nested $L $r))) {
                if ($item.Error) { $errors.Add("$($item.Path): $($item.Error)"); continue }
                if ($item.Reparse) { $errors.Add("$($item.Path): reparse point under an apply root"); continue }
                try {
                    $o = Hold $item.Path $Change                                # held until Apply ends: no rename / delete / swap
                    $key = Get-ObjectKey $item.Path $o.H
                    Register-Object $seen $key $item.Path
                    $items.Add([pscustomobject]@{ Path = $item.Path; Dir = $item.Dir; Key = $key })
                }
                catch { $errors.Add($(if ($_.Exception.Message -like "$($item.Path)*") { $_.Exception.Message } else { "$($item.Path): $($_.Exception.Message)" })) }
            }
        }
        foreach ($n in @($L.Roots | Where-Object { -not $_.Apply -and $_.Path -ne $r.Path -and (Test-Under $_.Path $r.Path) })) {
            if (-not [IO.Directory]::Exists($n.Path)) { $errors.Add("$($n.Path): root '$($n.Id)' is not applied and lies under apply root '$($r.Id)'; it must already exist with a protected DACL"); continue }
            try { $o = Hold $n.Path $ReadOnly } catch { $errors.Add("$($n.Path): $($_.Exception.Message)"); continue }
            $sd = [Security.AccessControl.RawSecurityDescriptor]::new([B1Ops.Native]::GetSddl($o.H))
            if (($sd.ControlFlags -band [Security.AccessControl.ControlFlags]::DiscretionaryAclProtected) -eq 0) {
                $errors.Add("$($n.Path): root '$($n.Id)' is not applied but inherits from its parent: applying '$($r.Id)' would change it (G5-R02)")
                continue
            }
            foreach ($item in (Get-Tree $n.Path (Get-Nested $L $n))) {
                if ($item.Error -or $item.Reparse) { $errors.Add("$($item.Path): $(if ($item.Error) { $item.Error } else { 'reparse point' }) under a non-applied nested root"); continue }
                $baseline.Add([pscustomobject]@{ path = $item.Path; sddl = (Get-HandleSddl $item.Path) })
            }
        }
    }
    $note = ''
    if (-not $diag -or (Injected 'maintenance-check' $null)) {
        $pp = Get-PrincipalProcesses @($L.Principals.Values)
        foreach ($h in $pp.Hits) { $errors.Add("maintenance condition: a principal process is running: $h") }
        $note = "processes with unknown owner: $($pp.Unknown)"
    }
    else { $note = 'maintenance condition not checked (diagnostic)' }
    [pscustomobject]@{ Errors = $errors.ToArray(); Held = $held; Items = $items.ToArray(); Seen = $seen; Plan = @($plan | Sort-Object { $_.Path.Length }); Baseline = $baseline.ToArray(); Note = $note }
}

# The handle the snapshot entry was taken from (held by this Apply), or a fresh one whose object must be the snapshot's object.
function Open-Entry($e, [hashtable]$held) {
    $k = $e.path.ToLowerInvariant()
    if ($held.ContainsKey($k)) { return [pscustomobject]@{ H = $held[$k].H; Own = $false } }
    $o = Open-Checked $e.path $Change
    try {
        $i = [B1Ops.Native]::Query($o.H)
        $key = '{0:x8}:{1:x16}' -f $i.VolumeSerial, $i.FileId
        if ($key -ne $e.object) { throw "file ID $key is not the snapshot's object $($e.object) - not restored" }
    }
    catch { $o.H.Dispose(); throw }
    [pscustomobject]@{ H = $o.H; Own = $true }
}

# Every entry is restored on its own through the handle bound to its object (a failure is recorded and the rest continue), then
# re-read and compared; the directories Apply created are removed deepest first if empty. Returns the residue: an empty list
# means the state equals the snapshot again. Writes never propagate (SetSddlExact).
function Restore-Snapshot($entries, [string[]]$created, [hashtable]$held = @{}) {
    $residue = New-Object System.Collections.Generic.List[string]
    $null = [B1Ops.Native]::EnablePrivilege('SeRestorePrivilege')            # needed to give an item back another account's ownership
    foreach ($e in @($entries | Sort-Object { $_.path.Length })) {
        try {
            if (Injected 'restore-fail' $e.path) { throw 'injected restore failure (diagnostic)' }
            $o = Open-Entry $e $held
            try {
                $now = [B1Ops.Native]::GetSddl($o.H)
                if ($now -ne $e.sddl) {
                    $ownerDiffers = ([Security.AccessControl.RawSecurityDescriptor]::new($now)).Owner.Value -ne ([Security.AccessControl.RawSecurityDescriptor]::new($e.sddl)).Owner.Value
                    [B1Ops.Native]::SetSddlExact($o.H, $e.sddl, $ownerDiffers)
                }
            }
            finally { if ($o.Own) { $o.H.Dispose() } }
        }
        catch { $residue.Add("$($e.path): not restored: $($_.Exception.Message)") }
    }
    foreach ($e in $entries) {
        try {
            $o = Open-Entry $e $held
            try { $now = [B1Ops.Native]::GetSddl($o.H) } finally { if ($o.Own) { $o.H.Dispose() } }
            if ($now -ne $e.sddl) { $residue.Add("$($e.path): differs after restore: $now != $($e.sddl)") }
        }
        catch { $residue.Add("$($e.path): cannot be re-read after restore: $($_.Exception.Message)") }
    }
    foreach ($c in @($created | Sort-Object { $_.Length } -Descending)) {
        if (-not [IO.Directory]::Exists($c)) { continue }
        $k = $c.ToLowerInvariant()
        if ($held.ContainsKey($k)) { $held[$k].H.Dispose(); $held.Remove($k) }      # Apply's own handle on a directory it created
        try { [IO.Directory]::Delete($c, $false) } catch { $residue.Add("$($c): created by Apply and left in place: $($_.Exception.Message)") }
    }
    $residue.ToArray()
}

function Write-Result($result, $rows, [string]$extra) {
    $doc = [ordered]@{
        tool = 'b1-ops/g5-acl.ps1'; mode = $Mode; result = $result; diagnostic = [bool]$diag; diagnostic_root = $diag; injections = $DiagnosticInject
        elevated = $elevated; user_sid = $me.User.Value; computer = $env:COMPUTERNAME; time_utc = [DateTime]::UtcNow.ToString('o')
        layout_sha256 = $(if ($script:L) { $script:L.Sha256 } else { $null }); note = $extra; roots = @($rows | ForEach-Object { $_ })
    }
    foreach ($row in $rows) {
        Write-Output ("{0,-10} {1,-4} items={2,-6} {3}" -f $row.id, $row.status, $row.items, $row.path)
        foreach ($x in $row.violations) { Write-Output "    - $x" }
    }
    Write-Output "RESULT=$result$(if ($diag) { ' (DIAGNOSTIC)' })$(if ($extra) { " $extra" })"
    if ($Report) { [IO.File]::WriteAllText($Report, ($doc | ConvertTo-Json -Depth 6), $utf8) }
}

function Read-Journal([string]$snap) {
    $j = "$snap.created"
    if (Test-Path -LiteralPath $j) { @([IO.File]::ReadAllLines($j, $utf8) | Where-Object { $_ }) } else { @() }
}

if ($Mode -eq 'Restore') {
    if (-not $Snapshot -or -not (Test-Path -LiteralPath $Snapshot)) { Fail-Usage '-Snapshot <existing file> is required.' }
    $snap = [IO.File]::ReadAllText($Snapshot, $utf8) | ConvertFrom-Json
    if ($snap.format -ne 'starplayer-g5-acl-snapshot-v3') { Fail-Usage 'snapshot format must be starplayer-g5-acl-snapshot-v3.' }
    if (-not $diag -and (-not $elevated -or -not $OwnerApproved)) { Fail-Usage 'Restore changes ACLs: run elevated with -OwnerApproved (or inside -DiagnosticRoot).' }
    $created = @(Read-Journal $Snapshot)
    $entries = @($snap.entries | Where-Object { $_ })
    if ($diag) { foreach ($p in @($entries | ForEach-Object { $_.path }) + $created) { if (-not (Test-Under $p $diag)) { Fail-Usage "snapshot path '$p' is outside the diagnostic root." } } }
    $script:changed = $true
    $diff = @(Restore-Snapshot $entries $created)
    $rows = @([pscustomobject]@{ id = 'restore'; path = $Snapshot; status = $(if ($diff.Count) { 'FAIL' } else { 'PASS' }); items = $entries.Count; violations = $diff; note = "created=$($created.Count)" })
    Write-Result $(if ($diff.Count) { 'RESTORE_INCOMPLETE' } else { 'RESTORED' }) $rows ''
    exit $(if ($diff.Count) { 3 } else { 0 })
}

$script:L = Read-Layout $Layout
if ($diag) { foreach ($r in $L.Roots) { if (-not (Test-Under $r.Path $diag)) { Fail-Usage "root '$($r.Id)' ($($r.Path)) is outside the diagnostic root." } } }
$owner = if ($diag) { $me.User.Value } else { $L.Owner }

if ($Mode -eq 'Plan') {
    foreach ($r in $L.Roots) {
        if ($r.Apply) { Write-Output "$($r.Id) $($r.Path)`n    create if missing (born with this descriptor); owner+DACL := $(Build-Sddl $r $owner)`n    every descendant (one link each, checked first): owner := $owner, DACL := inherited copy of the root ACEs, written exactly, no propagation" }
        else { Write-Output "$($r.Id) $($r.Path)`n    verify only (no change; under an apply root it must already be protected)" }
    }
    Write-Output 'RESULT=PLAN (nothing changed; Apply runs the preflight first)'
    exit 0
}

if ($Mode -eq 'Verify') {
    $rows = Invoke-Verify $L
    $fail = @($rows | Where-Object { $_.status -ne 'PASS' }).Count -gt 0
    Write-Result $(if ($fail) { 'FAIL' } else { 'PASS' }) $rows ''
    exit $(if ($fail) { 1 } else { 0 })
}

# ---------------------------------------------------------------- Apply
if (-not $diag -and (-not $elevated -or -not $OwnerApproved)) { Fail-Usage 'Apply changes ACLs: run elevated with -OwnerApproved on the approved machine (or inside -DiagnosticRoot).' }
if (-not $Snapshot) { Fail-Usage '-Snapshot <new file> is required for Apply.' }
if ((Test-Path -LiteralPath $Snapshot) -or (Test-Path -LiteralPath "$Snapshot.created")) { Fail-Usage "snapshot '$Snapshot' (or its journal) already exists (never overwritten)." }
if (-not $diag) { foreach ($p in 'SeRestorePrivilege', 'SeTakeOwnershipPrivilege') { $null = [B1Ops.Native]::EnablePrivilege($p) } }

$pre = Invoke-Preflight $L $owner
$held = $pre.Held
try {
    if ($pre.Errors.Count) {
        $rows = @([pscustomobject]@{ id = 'preflight'; path = ''; status = 'FAIL'; items = $pre.Errors.Count; violations = $pre.Errors; note = $pre.Note })
        Write-Result 'REFUSED' $rows "nothing was changed and no snapshot was written; $($pre.Note)"
        exit 1
    }
    $swap = @()
    foreach ($p in @($inject['swap-after-check'] | Where-Object { $_ -and $_ -ne '*' })) {    # a path change after the preflight (diagnostic)
        try { [IO.Directory]::Move($p, "$p.swapped"); $swap += "MOVED $p"; [void](cmd.exe /c mklink /J "$p" "$p.swapped") } catch { $swap += "BLOCKED $($p): $($_.Exception.Message)" }
    }
    $swap += @(Add-InjectedLinks 'add-link-after-check')
    $swapNote = if ($swap.Count) { '; injected: ' + ($swap -join '; ') } else { '' }
    # G5-FIX-01: right before the first change, every held object is still the checked object with one link
    $keyOf = @{}                                                               # lower-case path -> object key, for every object Apply may change
    $recheck = New-Object System.Collections.Generic.List[string]
    foreach ($it in $pre.Items) {
        $keyOf[$it.Path.ToLowerInvariant()] = $it.Key
        try { if ((Get-ObjectKey $it.Path $held[$it.Path.ToLowerInvariant()].H) -ne $it.Key) { $recheck.Add("$($it.Path): not the object of the preflight") } }
        catch { $recheck.Add($_.Exception.Message) }
    }
    if ($recheck.Count) {
        $rows = @([pscustomobject]@{ id = 'recheck'; path = ''; status = 'FAIL'; items = $recheck.Count; violations = $recheck.ToArray(); note = $pre.Note })
        Write-Result 'REFUSED' $rows "an object changed after the preflight; nothing was changed and no snapshot was written$swapNote"
        exit 1
    }
    $entries = @($pre.Items | ForEach-Object { [pscustomobject]@{ path = $_.Path; dir = $_.Dir; object = $_.Key; sddl = [B1Ops.Native]::GetSddl($held[$_.Path.ToLowerInvariant()].H) } })
    [IO.File]::WriteAllText($Snapshot, ([ordered]@{ format = 'starplayer-g5-acl-snapshot-v3'; layout_sha256 = $L.Sha256; time_utc = [DateTime]::UtcNow.ToString('o'); entries = $entries } | ConvertTo-Json -Depth 5), $utf8)
    $created = New-Object System.Collections.Generic.List[string]
    $late = New-Object System.Collections.Generic.List[string]                # objects that appeared after the preflight and were changed
    $seen = $pre.Seen
    try {
        foreach ($step in $pre.Plan) {
            $script:changed = $true
            if (-not [B1Ops.Native]::CreateDirectory($step.Path, $step.Sddl)) { throw "$($step.Path) appeared after the preflight (not created by Apply)" }
            $created.Add($step.Path)
            [IO.File]::AppendAllText("$Snapshot.created", "$($step.Path)`r`n", $utf8)
            if (Injected 'drop-file' $step.Path) { [IO.File]::WriteAllText((Join-Path $step.Path 'concurrent-writer.txt'), 'x') }
            $o = Open-Checked $step.Path $Change
            $held[$step.Path.ToLowerInvariant()] = $o
            $key = Get-ObjectKey $step.Path $o.H; Register-Object $seen $key $step.Path; $keyOf[$step.Path.ToLowerInvariant()] = $key
        }
        foreach ($r in @($L.Roots | Where-Object { $_.Apply })) {
            if (Injected 'apply-fail' $r.Path) { throw "injected apply failure at $($r.Path) (diagnostic)" }
            $rk = $r.Path.ToLowerInvariant()
            if ((Get-ObjectKey $r.Path $held[$rk].H) -ne $keyOf[$rk]) { throw "$($r.Path): not the object of the preflight" }
            $script:changed = $true
            [B1Ops.Native]::SetSddlExact($held[$rk].H, (Build-Sddl $r $owner), $true)
            foreach ($item in @(Get-Tree $r.Path (Get-Nested $L $r) | Select-Object -Skip 1)) {
                if ($item.Error -or $item.Reparse) { throw "$($item.Path): $(if ($item.Error) { $item.Error } else { 'reparse point appeared during Apply' })" }
                if (Injected 'apply-fail' $item.Path) { throw "injected apply failure at $($item.Path) (diagnostic)" }
                $k = $item.Path.ToLowerInvariant()
                if ($held.ContainsKey($k)) {
                    if ((Get-ObjectKey $item.Path $held[$k].H) -ne $keyOf[$k]) { throw "$($item.Path): not the object of the preflight" }
                }
                else {                                                         # appeared after the preflight: checked before it is touched
                    $o = Open-Checked $item.Path $Change
                    $held[$k] = $o
                    $key = Get-ObjectKey $item.Path $o.H; Register-Object $seen $key $item.Path; $keyOf[$k] = $key
                    $late.Add($item.Path)
                }
                [B1Ops.Native]::SetSddlExact($held[$k].H, (Child-Sddl $r $owner $item.Dir), $true)
            }
        }
        $more = @(Add-InjectedLinks 'add-link-after-change')
        if ($more.Count) { $swapNote += '; injected: ' + ($more -join '; ') }
        foreach ($k in @($keyOf.Keys)) {                                       # a link added while Apply ran: roll the object back
            $p = $seen[$keyOf[$k]]
            if ((Get-ObjectKey $p $held[$k].H) -ne $keyOf[$k]) { throw "$($p): not the object of the preflight after the change" }
        }
        $rows = @(Invoke-Verify $L)
        $bad = @($rows | Where-Object { $_.status -ne 'PASS' })
        if ($bad.Count) { throw ('post-apply verify failed: ' + ((@($bad | ForEach-Object { $_.violations }) | Select-Object -First 5) -join '; ')) }
        foreach ($b in $pre.Baseline) { if ((Get-HandleSddl $b.path) -ne $b.sddl) { throw "non-applied nested root changed: $($b.path)" } }
        Write-Result 'PASS' $rows "snapshot=$Snapshot created=$($created.Count) objects=$($keyOf.Count) appeared_after_preflight=$($late.Count); $($pre.Note)$swapNote"
        exit 0
    }
    catch {
        $why = $_.Exception.Message
        $residue = New-Object System.Collections.Generic.List[string]
        try { foreach ($x in @(Restore-Snapshot $entries $created.ToArray() $held)) { $residue.Add($x) } } catch { $residue.Add("restore aborted: $($_.Exception.Message)") }
        foreach ($p in $late) { if (-not @($created | Where-Object { Test-Under $p $_ }).Count -or (Test-Path -LiteralPath $p)) { $residue.Add("$($p): appeared after the preflight and was changed by Apply; its earlier descriptor is not in the snapshot") } }
        try { foreach ($b in $pre.Baseline) { if ((Get-HandleSddl $b.path) -ne $b.sddl) { $residue.Add("non-applied nested root changed: $($b.path)") } } } catch { $residue.Add("nested root re-check failed: $($_.Exception.Message)") }
        try { $rows = @(Invoke-Verify $L) } catch { $rows = @([pscustomobject]@{ id = 'verify'; path = ''; status = 'FAIL'; items = 0; violations = @("verify failed: $($_.Exception.Message)"); note = '' }) }
        if ($residue.Count) {
            $rows = @($rows) + @([pscustomobject]@{ id = 'residue'; path = $Snapshot; status = 'FAIL'; items = $residue.Count; violations = $residue.ToArray(); note = 'state differs from the snapshot' })
            Write-Result 'ROLLBACK_FAILED' $rows "cause=$why; created=$($created.Count)$swapNote"
            exit 3
        }
        Write-Result 'ROLLED_BACK' $rows "cause=$why; created=$($created.Count) (all removed)$swapNote"
        exit 1
    }
}
finally {
    foreach ($o in @($held.Values)) { $o.H.Dispose() }
}
