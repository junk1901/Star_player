<#
.SYNOPSIS
  B1 G5 counterexample probe: can the account running this script replace, delete, rename or re-permission what the layout protects?

.DESCRIPTION
  Run it in a NON-elevated session of the account under test (producer, verifier, reader, deployer). For every layout root it asks
  the file system for handles with one write-class right at a time and closes them at once: nothing is written, renamed,
  deleted or re-permissioned, so a probe that is unexpectedly ALLOWED changes nothing.

    root directory   add file, add subdirectory, write EA / attributes, delete child, delete, WRITE_DAC, WRITE_OWNER
    files (sample)   write data, append, write EA / attributes, delete (rename needs it), WRITE_DAC, WRITE_OWNER
    ancestors        delete, delete child, WRITE_DAC, WRITE_OWNER (the "parent path change" counterexample)

  Covered counterexamples: executable replacement (R-BUILD / R-DOTNET / R-GIT files: write, delete, add file), TRX tampering
  (R-WORK: write and delete of an existing TRX, add file) and a change of a parent path (ancestors).
  The file sample is the first -MaxFiles files of each root in ordinal path order plus every -Target.

  Expected outcome: DENIED (Win32 error 5) for every probe, except data rights inside a root that lists the account as a writer
  (reported ALLOWED_AS_WRITER). WRITE_DAC / WRITE_OWNER on the root itself and every ancestor probe must be denied for everyone.
  ERROR_SHARING_VIOLATION (32) means the access check passed and only a share mode blocked the handle: it counts as ALLOWED.
  FILE_FLAG_BACKUP_SEMANTICS is used to open directories; in an elevated token with backup / restore privileges enabled the probe
  is meaningless, so an elevated session is refused.

  Exit: 0 every expectation met, 1 an unexpected ALLOWED or an inconclusive probe, 2 usage.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Layout,
    [Parameter(Mandatory = $true)][string]$Role,
    [string[]]$Target = @(),
    [int]$MaxFiles = 64,
    [string]$Report,
    [string]$DiagnosticRoot
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

function Fail-Usage([string]$m) { [Console]::Error.WriteLine("usage: $m"); exit 2 }

if (-not ('G5Probe.Native' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace G5Probe {
    public static class Native {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        // 0 when a handle with exactly this access was granted (and closed at once), otherwise the Win32 error.
        public static int Open(string path, uint access) {
            using (SafeFileHandle h = CreateFileW(@"\\?\" + path, access, 7u, IntPtr.Zero, 3u, 0x02000000u | 0x00200000u, IntPtr.Zero)) {
                return h.IsInvalid ? Marshal.GetLastWin32Error() : 0;
            }
        }
    }
}
'@
}

$Target = @($Target | ForEach-Object { $_ -split '\|' } | Where-Object { $_ })   # 'powershell -File' passes one string: separate paths with '|'
$me = [Security.Principal.WindowsIdentity]::GetCurrent()
if (([Security.Principal.WindowsPrincipal]$me).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { Fail-Usage 'run the probe in a non-elevated session of the account under test.' }

$j = Get-Content -LiteralPath $Layout -Raw -Encoding UTF8 | ConvertFrom-Json
if ($j.format -ne 'starplayer-g5-layout-v1') { Fail-Usage 'layout format must be starplayer-g5-layout-v1.' }
$roleSid = $null
foreach ($p in $j.principals.PSObject.Properties) { if ($p.Name -eq $Role) { $roleSid = [string]$p.Value } }
if (-not $roleSid) { Fail-Usage "role '$Role' is not a layout principal." }
$diag = if ($DiagnosticRoot) { [IO.Path]::GetFullPath($DiagnosticRoot).TrimEnd('\') } else { $null }
if (-not $diag -and $roleSid -ne $me.User.Value) { Fail-Usage "this session is $($me.User.Value), not the '$Role' principal $roleSid." }

$DirRights = [ordered]@{ add_file = 0x2; add_subdirectory = 0x4; write_ea = 0x10; delete_child = 0x40; write_attributes = 0x100; delete = 0x10000; write_dac = 0x40000; write_owner = 0x80000 }
$FileRights = [ordered]@{ write_data = 0x2; append_data = 0x4; write_ea = 0x10; write_attributes = 0x100; delete = 0x10000; write_dac = 0x40000; write_owner = 0x80000 }
$AncestorRights = [ordered]@{ delete = 0x10000; delete_child = 0x40; write_dac = 0x40000; write_owner = 0x80000 }

function Test-Under([string]$path, [string]$root) { $path.Equals($root, 'OrdinalIgnoreCase') -or $path.StartsWith($root.TrimEnd('\') + '\', 'OrdinalIgnoreCase') }

# First -MaxFiles files under $root in ordinal order, never following a reparse point.
function Get-Sample([string]$root, [int]$max) {
    $files = New-Object System.Collections.Generic.List[string]
    $queue = New-Object System.Collections.Generic.Queue[string]
    $queue.Enqueue($root)
    while ($queue.Count -gt 0 -and $files.Count -lt $max) {
        $d = $queue.Dequeue()
        try { $entries = [IO.Directory]::GetFileSystemEntries($d) } catch { continue }
        [Array]::Sort($entries, [StringComparer]::Ordinal)
        foreach ($e in $entries) {
            $a = [IO.File]::GetAttributes($e)
            if ($a -band [IO.FileAttributes]::ReparsePoint) { continue }
            if ($a -band [IO.FileAttributes]::Directory) { $queue.Enqueue($e) } elseif ($files.Count -lt $max) { $files.Add($e) }
        }
    }
    $files
}

$rows = New-Object System.Collections.Generic.List[object]
function Probe([string]$rootId, [string]$kind, [string]$path, $rights, [bool]$writer) {
    foreach ($name in $rights.Keys) {
        $code = [G5Probe.Native]::Open($path, [uint32]$rights[$name])
        $observed = switch ($code) { 0 { 'ALLOWED' } 32 { 'ALLOWED' } 5 { 'DENIED' } default { "ERROR_$code" } }
        $permissionRight = $name -eq 'write_dac' -or $name -eq 'write_owner'
        $expected = if ($writer -and $kind -ne 'ancestor' -and -not ($kind -eq 'root' -and ($permissionRight -or $name -eq 'delete'))) { 'ANY' } else { 'DENIED' }
        $ok = $observed -eq 'DENIED' -or ($expected -eq 'ANY' -and $observed -eq 'ALLOWED')
        $status = if ($observed -like 'ERROR_*') { 'INCONCLUSIVE' } elseif (-not $ok) { 'UNEXPECTED_ALLOWED' } elseif ($observed -eq 'ALLOWED') { 'ALLOWED_AS_WRITER' } else { 'DENIED' }
        $rows.Add([pscustomobject]@{ root = $rootId; kind = $kind; path = $path; right = $name; win32 = $code; status = $status })
    }
}

foreach ($r in $j.roots) {
    $path = [IO.Path]::GetFullPath([string]$r.path).TrimEnd('\')
    if ($diag -and -not (Test-Under $path $diag)) { Fail-Usage "root '$($r.id)' is outside the diagnostic root." }
    if (-not [IO.Directory]::Exists($path)) { $rows.Add([pscustomobject]@{ root = $r.id; kind = 'root'; path = $path; right = '-'; win32 = 3; status = 'INCONCLUSIVE' }); continue }
    $writer = @($r.writers) -contains $Role
    $p = [IO.Directory]::GetParent($path)
    while ($p) {
        $a = $p.FullName
        if (-not $diag -or ((Test-Under $a.TrimEnd('\') $diag) -and $a.TrimEnd('\') -ne $diag)) { Probe $r.id 'ancestor' $a $AncestorRights $false }
        $p = $p.Parent
    }
    Probe $r.id 'root' $path $DirRights $writer
    $targets = @(Get-Sample $path $MaxFiles) + @($Target | Where-Object { Test-Under ([IO.Path]::GetFullPath($_)) $path } | ForEach-Object { [IO.Path]::GetFullPath($_) })
    foreach ($f in @($targets | Sort-Object -Unique)) { Probe $r.id 'file' $f $FileRights $writer }
}

$bad = @($rows | Where-Object { $_.status -eq 'UNEXPECTED_ALLOWED' -or $_.status -eq 'INCONCLUSIVE' })
foreach ($g in ($rows | Group-Object root)) {
    $c = $g.Group | Group-Object status | ForEach-Object { "$($_.Name)=$($_.Count)" }
    Write-Output ("{0,-10} probes={1,-5} {2}" -f $g.Name, $g.Count, ($c -join ' '))
}
foreach ($b in $bad) { Write-Output "    - $($b.status) $($b.kind) $($b.right) win32=$($b.win32) $($b.path)" }
$result = if ($bad.Count) { 'FAIL' } else { 'PASS' }
Write-Output "RESULT=$result role=$Role sid=$($me.User.Value)$(if ($diag) { ' (DIAGNOSTIC)' })"
if ($Report) {
    $doc = [ordered]@{ tool = 'b1-ops/g5-probe.ps1'; result = $result; role = $Role; user_sid = $me.User.Value; diagnostic = [bool]$diag; computer = $env:COMPUTERNAME; time_utc = [DateTime]::UtcNow.ToString('o'); probes = $rows.ToArray() }
    [IO.File]::WriteAllText($Report, ($doc | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding($false)))
}
exit $(if ($bad.Count) { 1 } else { 0 })
