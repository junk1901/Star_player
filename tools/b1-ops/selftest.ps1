<#
.SYNOPSIS
  Self-test of the b1-ops tools in an isolated scratch root (non-elevated; no operational ACL, account, anchor or pin is touched).

.DESCRIPTION
  -Root must not exist; it is created, used as the -DiagnosticRoot of every ACL-changing call and removed at the end unless -Keep.
  The only paths outside it are read-only: the probe of C:\Program Files\dotnet and Git (handles opened and closed, nothing
  written) and the pin hash of dotnet.exe. Exit 0 when every case passes, 1 otherwise.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Root,
    [string]$Report,
    [switch]$Keep
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$utf8 = New-Object Text.UTF8Encoding($false)
if (Test-Path -LiteralPath $Root) { [Console]::Error.WriteLine("usage: '$Root' already exists."); exit 2 }
$Root = [IO.Path]::GetFullPath($Root).TrimEnd('\')
[void][IO.Directory]::CreateDirectory($Root)
$me = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$results = New-Object System.Collections.Generic.List[object]

function Run([string]$script, [string[]]$arguments) {
    $ErrorActionPreference = 'Continue'                                     # a usage message on stderr is an expected result here
    $out = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $here $script) @arguments 2>&1 | ForEach-Object { "$_" }
    [pscustomobject]@{ Exit = $LASTEXITCODE; Text = ($out -join "`n") }
}
function Check([string]$name, [bool]$ok, [string]$detail) {
    $results.Add([pscustomobject]@{ case = $name; result = $(if ($ok) { 'PASS' } else { 'FAIL' }); detail = $detail })
    Write-Output ("{0} {1}  {2}" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $name, $detail)
}
function Sid([string]$s) { New-Object Security.Principal.SecurityIdentifier($s) }
function Get-Dacl([string]$p) { if ([IO.Directory]::Exists($p)) { [IO.Directory]::GetAccessControl($p, 'Access') } else { [IO.File]::GetAccessControl($p, 'Access') } }
function Set-Dacl([string]$p, $a) { if ([IO.Directory]::Exists($p)) { [IO.Directory]::SetAccessControl($p, $a) } else { [IO.File]::SetAccessControl($p, $a) } }
function Sddl([string]$p) { (Get-Acl -LiteralPath $p).GetSecurityDescriptorSddlForm('Owner, Access') }
function Tree-Sddl([string]$p) { @(Get-ChildItem -LiteralPath $p -Recurse -Force | Sort-Object FullName | ForEach-Object { "$($_.FullName)|$(Sddl $_.FullName)" }) + @("$p|$(Sddl $p)") }
function Write-Json([string]$path, $obj) { [IO.File]::WriteAllText($path, ($obj | ConvertTo-Json -Depth 6), $utf8) }
function New-Tree([string]$t) {
    foreach ($d in 'base\build\sub', 'base\trust') { [void][IO.Directory]::CreateDirectory("$t\$d") }
    [IO.File]::WriteAllText("$t\base\build\sub\a.dll", 'a'); [IO.File]::WriteAllText("$t\base\build\b.json", 'b'); [IO.File]::WriteAllText("$t\base\trust\anchor.json", 'c')
}
function Layout([string]$t, [string]$file) {
    Write-Json $file ([ordered]@{
            format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = [ordered]@{ verifier = 'S-1-5-19'; producer = 'S-1-5-20' }
            roots = @(
                [ordered]@{ id = 'R-BASE'; path = "$t\base"; apply = $true; writers = @(); readers = @('verifier', 'producer') },
                [ordered]@{ id = 'R-TRUST'; path = "$t\base\trust"; apply = $true; writers = @(); readers = @('verifier') },
                [ordered]@{ id = 'R-BUILD'; path = "$t\base\build"; apply = $true; writers = @(); readers = @('verifier', 'producer') },
                [ordered]@{ id = 'R-WORK'; path = "$t\base\work"; apply = $true; writers = @('verifier'); readers = @() })
        })
}

try {
    # ---------------- g5-acl ----------------
    $t = "$Root\acl"; New-Tree $t; $lay = "$Root\layout.json"; Layout $t $lay
    $before = Tree-Sddl "$t\base"
    $r = Run 'g5-acl.ps1' @('-Mode', 'Plan', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.plan_changes_nothing' ($r.Exit -eq 0 -and @(Compare-Object $before (Tree-Sddl "$t\base")).Count -eq 0) "exit=$($r.Exit)"
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.verify_refuses_inherited_tree' ($r.Exit -eq 1 -and $r.Text -match 'not protected' -and $r.Text -match 'R-WORK .*FAIL') "exit=$($r.Exit)"
    $r = Run 'g5-acl.ps1' @('-Mode', 'Apply', '-Layout', $lay, '-DiagnosticRoot', $t, '-Snapshot', "$Root\snap-a.json")
    Check 'acl.apply_pass' ($r.Exit -eq 0 -and $r.Text -match 'RESULT=PASS') "exit=$($r.Exit)"
    $fileSddl = Sddl "$t\base\build\sub\a.dll"
    $explicit = @([regex]::Matches($fileSddl, '\([AD];([A-Z]*);') | Where-Object { $_.Groups[1].Value -notmatch 'ID' })
    Check 'acl.apply_descendants_inherit_only' ($explicit.Count -eq 0 -and $fileSddl -match '\(A;ID;') $fileSddl
    Check 'acl.apply_root_protected' ((Sddl "$t\base\build") -match 'D:PAI') (Sddl "$t\base\build")
    Check 'acl.apply_writer_grant' ((Sddl "$t\base\work") -match '\(A;OICI;0x1301bf;;;LS\)') (Sddl "$t\base\work")
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.verify_pass_after_apply' ($r.Exit -eq 0) "exit=$($r.Exit)"

    $acl = Get-Dacl "$t\base\build\sub\a.dll"
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((Sid 'S-1-5-20'), 'Write', 'Allow')))
    Set-Dacl "$t\base\build\sub\a.dll" $acl
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.verify_detects_producer_write_ace' ($r.Exit -eq 1 -and $r.Text -match 'a\.dll: S-1-5-20 gets') "exit=$($r.Exit)"
    $acl = Get-Dacl "$t\base\build\sub\a.dll"; [void]$acl.RemoveAccessRuleSpecific((New-Object Security.AccessControl.FileSystemAccessRule((Sid 'S-1-5-20'), 'Write', 'Allow'))); Set-Dacl "$t\base\build\sub\a.dll" $acl

    $acl = Get-Dacl "$t\base\trust"; $acl.SetAccessRuleProtection($false, $true); Set-Dacl "$t\base\trust" $acl
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.verify_detects_unprotected_root' ($r.Exit -eq 1 -and $r.Text -match 'trust: DACL is not protected') "exit=$($r.Exit)"
    $acl = Get-Dacl "$t\base\trust"; $acl.SetAccessRuleProtection($true, $false); Set-Dacl "$t\base\trust" $acl

    $acl = Get-Dacl "$t\base"; $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((Sid 'S-1-5-20'), 'Delete', 'Allow'))); Set-Dacl "$t\base" $acl
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.verify_detects_ancestor_delete' ($r.Exit -eq 1 -and $r.Text -match 'ancestor: S-1-5-20 gets 0x10000') "exit=$($r.Exit)"
    $acl = Get-Dacl "$t\base"; [void]$acl.RemoveAccessRuleSpecific((New-Object Security.AccessControl.FileSystemAccessRule((Sid 'S-1-5-20'), 'Delete', 'Allow'))); Set-Dacl "$t\base" $acl

    [void](cmd.exe /c mklink /J "$t\base\build\jn" "$t\base\trust")
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.verify_detects_reparse_point' ($r.Exit -eq 1 -and $r.Text -match 'jn: reparse point') "exit=$($r.Exit)"
    [IO.Directory]::Delete("$t\base\build\jn")
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.verify_pass_after_tamper_undone' ($r.Exit -eq 0) "exit=$($r.Exit)"

    $r = Run 'g5-acl.ps1' @('-Mode', 'Apply', '-Layout', $lay, '-DiagnosticRoot', $t, '-Snapshot', "$Root\snap-a.json")
    Check 'acl.snapshot_never_overwritten' ($r.Exit -eq 2) "exit=$($r.Exit)"
    $r = Run 'g5-acl.ps1' @('-Mode', 'Apply', '-Layout', $lay, '-Snapshot', "$Root\snap-x.json")
    Check 'acl.apply_refused_without_owner_elevation' ($r.Exit -eq 2 -and -not (Test-Path "$Root\snap-x.json")) "exit=$($r.Exit)"
    $out = "$Root\outside.json"; Write-Json $out ([ordered]@{ format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = @{ v = 'S-1-5-19' }; roots = @([ordered]@{ id = 'X'; path = 'C:\Windows'; apply = $true; writers = @(); readers = @() }) })
    $r = Run 'g5-acl.ps1' @('-Mode', 'Apply', '-Layout', $out, '-DiagnosticRoot', $t, '-Snapshot', "$Root\snap-y.json")
    Check 'acl.diagnostic_root_confines_roots' ($r.Exit -eq 2) "exit=$($r.Exit)"
    $bad = "$Root\unknown.json"; Write-Json $bad ([ordered]@{ format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = @{ v = 'S-1-5-19' }; roots = @([ordered]@{ id = 'X'; path = "$t\x"; apply = $true; writers = @('nobody'); readers = @() }) })
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $bad, '-DiagnosticRoot', $t)
    Check 'acl.layout_unknown_principal' ($r.Exit -eq 2) "exit=$($r.Exit)"

    # restore round trip: custom pre-existing ACLs come back exactly, the root Apply created is removed
    $t2 = "$Root\acl2"; New-Tree $t2; $lay2 = "$Root\layout2.json"; Layout $t2 $lay2
    $acl = Get-Dacl "$t2\base\build\b.json"; $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((Sid 'S-1-1-0'), 'ReadAndExecute', 'Allow'))); Set-Dacl "$t2\base\build\b.json" $acl
    $pre = Tree-Sddl "$t2\base"
    $r = Run 'g5-acl.ps1' @('-Mode', 'Apply', '-Layout', $lay2, '-DiagnosticRoot', $t2, '-Snapshot', "$Root\snap-b.json")
    $applied = $r.Exit -eq 0 -and (Test-Path "$t2\base\work")
    $r = Run 'g5-acl.ps1' @('-Mode', 'Restore', '-Snapshot', "$Root\snap-b.json", '-DiagnosticRoot', $t2)
    $d = @(Compare-Object $pre (Tree-Sddl "$t2\base"))
    Check 'acl.restore_round_trip' ($applied -and $r.Exit -eq 0 -and $d.Count -eq 0 -and -not (Test-Path "$t2\base\work")) "applied=$applied restore_exit=$($r.Exit) differences=$($d.Count)"

    # failed apply rolls back by itself: a junction makes the post-apply verify fail
    $t3 = "$Root\acl3"; New-Tree $t3; $lay3 = "$Root\layout3.json"; Layout $t3 $lay3
    [void](cmd.exe /c mklink /J "$t3\base\build\jn" "$t3\base\trust")
    $pre = Tree-Sddl "$t3\base"
    $r = Run 'g5-acl.ps1' @('-Mode', 'Apply', '-Layout', $lay3, '-DiagnosticRoot', $t3, '-Snapshot', "$Root\snap-c.json")
    $d = @(Compare-Object $pre (Tree-Sddl "$t3\base"))
    Check 'acl.failed_apply_rolls_back' ($r.Exit -eq 1 -and $r.Text -match 'ROLLED_BACK' -and $d.Count -eq 0 -and -not (Test-Path "$t3\base\work")) "exit=$($r.Exit) differences=$($d.Count)"
    [IO.Directory]::Delete("$t3\base\build\jn")

    # ---------------- g5-probe ----------------
    $p = "$Root\probe"; foreach ($x in 'deny', 'open', 'work') { [void][IO.Directory]::CreateDirectory("$p\$x"); [IO.File]::WriteAllText("$p\$x\f.trx", 'x') }
    $acl = Get-Dacl "$p\deny"
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((Sid $me), 'WriteData, AppendData, WriteExtendedAttributes, WriteAttributes, Delete, DeleteSubdirectoriesAndFiles', 'ContainerInherit, ObjectInherit', 'None', 'Deny')))
    Set-Dacl "$p\deny" $acl
    $pl = "$Root\probe-layout.json"
    Write-Json $pl ([ordered]@{ format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = [ordered]@{ producer = $me }
            roots = @([ordered]@{ id = 'R-DENY'; path = "$p\deny"; apply = $false; writers = @(); readers = @() }, [ordered]@{ id = 'R-OPEN'; path = "$p\open"; apply = $false; writers = @(); readers = @() },
                [ordered]@{ id = 'R-WORK'; path = "$p\work"; apply = $false; writers = @('producer'); readers = @() }) })
    $r = Run 'g5-probe.ps1' @('-Layout', $pl, '-Role', 'producer', '-DiagnosticRoot', $p, '-Report', "$Root\probe.json")
    $rows = @((Get-Content -LiteralPath "$Root\probe.json" -Raw | ConvertFrom-Json).probes)
    $deny = @($rows | Where-Object { $_.root -eq 'R-DENY' -and $_.right -notmatch 'write_dac|write_owner' -and -not ($_.kind -eq 'root' -and $_.right -eq 'delete') })
    Check 'probe.denied_data_rights_reported_denied' ($deny.Count -eq 10 -and @($deny | Where-Object { $_.status -ne 'DENIED' }).Count -eq 0) "probes=$($deny.Count)"
    # the root denies DELETE to this user, but its parent grants FILE_DELETE_CHILD: the root can still be deleted / renamed
    Check 'probe.parent_delete_child_exposes_root' (@($rows | Where-Object { $_.root -eq 'R-DENY' -and $_.kind -eq 'root' -and $_.right -eq 'delete' -and $_.status -eq 'UNEXPECTED_ALLOWED' }).Count -eq 1) 'parent grants delete child'
    Check 'probe.trx_tamper_in_open_root_is_unexpected' (@($rows | Where-Object { $_.root -eq 'R-OPEN' -and $_.path -like '*f.trx' -and $_.right -eq 'write_data' -and $_.status -eq 'UNEXPECTED_ALLOWED' }).Count -eq 1) 'write_data on f.trx'
    Check 'probe.writer_data_rights_allowed' (@($rows | Where-Object { $_.root -eq 'R-WORK' -and $_.kind -eq 'file' -and $_.status -eq 'ALLOWED_AS_WRITER' }).Count -gt 0) ''
    Check 'probe.owner_can_repermission_root_is_unexpected' (@($rows | Where-Object { $_.root -eq 'R-WORK' -and $_.kind -eq 'root' -and $_.right -eq 'write_dac' -and $_.status -eq 'UNEXPECTED_ALLOWED' }).Count -eq 1) 'implicit owner WRITE_DAC'
    Check 'probe.exit_reflects_unexpected' ($r.Exit -eq 1) "exit=$($r.Exit)"
    $acl = Get-Dacl "$p\deny"; $acl.Access | Where-Object { $_.AccessControlType -eq 'Deny' } | ForEach-Object { [void]$acl.RemoveAccessRule($_) }; Set-Dacl "$p\deny" $acl

    $dl = "$Root\devpc-layout.json"
    Write-Json $dl ([ordered]@{ format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = [ordered]@{ producer = $me }
            roots = @([ordered]@{ id = 'R-DOTNET'; path = 'C:\Program Files\dotnet'; apply = $false; writers = @(); readers = @() }, [ordered]@{ id = 'R-GIT'; path = 'C:\Program Files\Git'; apply = $false; writers = @(); readers = @() }) })
    $r = Run 'g5-probe.ps1' @('-Layout', $dl, '-Role', 'producer', '-MaxFiles', '32', '-Target', 'C:\Program Files\dotnet\dotnet.exe|C:\Program Files\Git\cmd\git.exe', '-Report', "$Root\probe-devpc.json")
    Check 'probe.devpc_dotnet_git_replacement_denied' ($r.Exit -eq 0 -and $r.Text -match '(?m)^R-DOTNET\s+probes=\d+\s+DENIED=\d+\s*$' -and $r.Text -match '(?m)^R-GIT\s+probes=\d+\s+DENIED=\d+\s*$') (($r.Text -split "`n" | Select-Object -First 3) -join ' / ')

    # ---------------- g5-pins ----------------
    $n = "$Root\pins"; [void][IO.Directory]::CreateDirectory("$n\real"); [IO.File]::WriteAllText("$n\real\one.dll", '1'); [IO.File]::WriteAllText("$n\real\two.dll", '2')
    [IO.File]::WriteAllLines("$n\list.txt", [string[]]@("$n\real\one.dll", "$n\real\two.dll", 'C:\Program Files\dotnet\dotnet.exe'))
    $r = Run 'g5-pins.ps1' @('-Mode', 'Collect', '-List', "$n\list.txt", '-Out', "$n\cand.json")
    $cand = Get-Content -LiteralPath "$n\cand.json" -Raw | ConvertFrom-Json
    Check 'pins.collect' ($r.Exit -eq 0 -and $cand.dotnet.path -eq 'C:\Program Files\dotnet\dotnet.exe' -and @($cand.execution_pins).Count -eq 2 -and $cand.status -eq 'CANDIDATE_NOT_APPROVED') "exit=$($r.Exit)"
    $r = Run 'g5-pins.ps1' @('-Mode', 'Verify', '-Pins', "$n\cand.json")
    Check 'pins.verify_pass' ($r.Exit -eq 0) "exit=$($r.Exit)"
    [IO.File]::WriteAllText("$n\real\one.dll", 'changed'); [IO.File]::Delete("$n\real\two.dll")
    $r = Run 'g5-pins.ps1' @('-Mode', 'Verify', '-Pins', "$n\cand.json")
    Check 'pins.verify_detects_replacement_and_removal' ($r.Exit -eq 1 -and $r.Text -match 'MISMATCH .*one\.dll' -and $r.Text -match 'MISSING .*two\.dll') "exit=$($r.Exit)"
    $cfg = "$n\config-like.json"; Write-Json $cfg ([ordered]@{ format = 'x'; execution_pins = @([ordered]@{ path = 'C:\Program Files\dotnet\dotnet.exe'; raw_sha256 = $cand.dotnet.raw_sha256 }, [ordered]@{ path = 'C:\Program Files\dotnet\dotnet.exe'; raw_sha256 = $cand.dotnet.raw_sha256 }) })
    $r = Run 'g5-pins.ps1' @('-Mode', 'Verify', '-Pins', $cfg)
    Check 'pins.verify_detects_duplicate' ($r.Exit -eq 1 -and $r.Text -match 'DUPLICATE') "exit=$($r.Exit)"
    [void](cmd.exe /c mklink /J "$n\jn" "$n\real")
    [IO.File]::WriteAllLines("$n\list2.txt", [string[]]@("$n\jn\one.dll"))
    $r = Run 'g5-pins.ps1' @('-Mode', 'Collect', '-List', "$n\list2.txt", '-Out', "$n\cand2.json")
    Check 'pins.collect_refuses_reparse_path' ($r.Exit -eq 1 -and $r.Text -match 'REPARSE') "exit=$($r.Exit)"
    [IO.Directory]::Delete("$n\jn")
    $r = Run 'g5-pins.ps1' @('-Mode', 'Trace', '-FilePath', "$env:windir\System32\WindowsPowerShell\v1.0\powershell.exe", '-ArgumentList', '-NoProfile -Command Start-Sleep -Seconds 2', '-Out', "$n\trace.tsv")
    $tr = @(Get-Content -LiteralPath "$n\trace.tsv")
    Check 'pins.trace_observes_modules' ($r.Exit -eq 0 -and @($tr | Where-Object { $_ -match "^IMAGE_LOADED`t.*\\ntdll\.dll`t" }).Count -eq 1) "lines=$($tr.Count)"

    # ---------------- a1-scan ----------------
    $g = "$Root\repo"; [void][IO.Directory]::CreateDirectory($g)
    & git.exe -C $g init -q; & git.exe -C $g config user.name selftest; & git.exe -C $g config user.email selftest@example.invalid
    # fake values are assembled at run time so this file itself never matches a1-scan's patterns (the repository is scanned too)
    [IO.File]::WriteAllText("$g\k.txt", ('-----BEGIN RSA ' + 'PRIVATE KEY-----') + "`n" + ('AK' + 'IA' + 'ABCDEFGHIJKLMNOP') + "`n"); & git.exe -C $g add k.txt; & git.exe -C $g commit -q -m one
    & git.exe -C $g rm -q k.txt; [IO.File]::WriteAllText("$g\ok.txt", 'path C:\' + 'Users\someone\x' + "`n"); & git.exe -C $g add ok.txt; & git.exe -C $g commit -q -m two
    $r = Run 'a1-scan.ps1' @('-Repo', $g, '-Commit', 'HEAD', '-Out', "$Root\scan1")
    $s = Get-Content -LiteralPath "$Root\scan1\summary.json" -Raw | ConvertFrom-Json
    $cls = @{}; foreach ($c in $s.classes) { $cls[$c.class] = $c.findings }
    Check 'scan.finds_secrets_removed_from_head' ($r.Exit -eq 1 -and $cls['private_key'] -eq 1 -and $cls['aws_access_key'] -eq 1) "exit=$($r.Exit)"
    Check 'scan.reports_privacy_classes' ($cls['user_profile_path'] -eq 1 -and $s.commits -eq 2) "user_profile_path=$($cls['user_profile_path'])"
    $g2 = "$Root\repo2"; [void][IO.Directory]::CreateDirectory($g2)
    & git.exe -C $g2 init -q; & git.exe -C $g2 config user.name selftest; & git.exe -C $g2 config user.email selftest@example.invalid
    [IO.File]::WriteAllText("$g2\a.txt", "nothing here`n"); & git.exe -C $g2 add a.txt; & git.exe -C $g2 commit -q -m clean
    $r = Run 'a1-scan.ps1' @('-Repo', $g2, '-Commit', 'HEAD', '-Out', "$Root\scan2")
    Check 'scan.clean_repo_passes' ($r.Exit -eq 0) "exit=$($r.Exit)"

    # ---------------- b4-candidates ----------------
    $b = "$Root\build"
    $files = @('tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.dll', 'tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.pdb',
        'tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.deps.json', 'tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.EvidenceVerifier.dll',
        'tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.SimHarness.dll')
    foreach ($proj in 'Domain', 'Simulation', 'Infrastructure', 'SimHarness') {
        $f = "tests/StarPlayer.$proj.Tests/bin/Release/net8.0"
        $files += "$f/StarPlayer.$proj.Tests.dll", "$f/testhost.exe", "$f/ko/xunit.resources.dll", "$f/runtimes/win-x64/native/e_sqlite3.dll", "$f/xunit.runner.visualstudio.testadapter.dll"
    }
    $files += 'tests/StarPlayer.Domain.Tests/bin/Release/net8.0/msCoverageSourceRootsMapping_StarPlayer.Domain.Tests'
    foreach ($f in $files) { $full = Join-Path $b ($f -replace '/', '\'); [void][IO.Directory]::CreateDirectory((Split-Path $full)); [IO.File]::WriteAllText($full, $f) }
    & git.exe -C $b init -q; & git.exe -C $b config user.name selftest; & git.exe -C $b config user.email selftest@example.invalid; & git.exe -C $b commit -q --allow-empty -m empty
    $head = (& git.exe -C $b rev-parse HEAD).Trim()
    [IO.File]::WriteAllLines("$Root\t_StarPlayer.Domain.Tests.tsv", [string[]]@("state`tpath`tfirst_ms`tprocesses", "PROCESS_STARTED`t$b\tests\StarPlayer.Domain.Tests\bin\Release\net8.0\testhost.exe`t1`tx"))
    $r = Run 'b4-candidates.ps1' @('-BuildRoot', $b, '-SourceCommit', $head, '-Out', "$Root\cand.json", '-Trace', "$Root\t_StarPlayer.Domain.Tests.tsv")
    $c = Get-Content -LiteralPath "$Root\cand.json" -Raw | ConvertFrom-Json
    $roles = @{}; foreach ($x in $c.summary.by_role) { $roles[$x.role] = $x.count }
    $th = @($c.artifacts | Where-Object { $_.relative_path -like 'tests/StarPlayer.Domain.Tests/*testhost.exe' })[0]
    $th2 = @($c.artifacts | Where-Object { $_.relative_path -like 'tests/StarPlayer.Simulation.Tests/*testhost.exe' })[0]
    Check 'b4.rule_v2_roles' ($r.Exit -eq 0 -and $c.summary.artifacts -eq 24 -and $c.summary.unpinned -eq 2 -and $roles['SIMHARNESS_BINARY'] -eq 1 -and $roles['VERIFIER_BINARY'] -eq 1 -and $roles['TEST_ASSEMBLY'] -eq 4 -and $roles['RUNTIME_CONFIG'] -eq 1 -and $roles['DEPENDENCY'] -eq 17) "artifacts=$($c.summary.artifacts) unpinned=$($c.summary.unpinned)"
    Check 'b4.categories_and_observation' ($th.category -eq 'TEST_HOST' -and $th.observed -eq 'PROCESS_STARTED' -and $th2.observed -eq 'NOT_TRACED' -and @($c.artifacts | Where-Object { $_.category -eq 'SATELLITE_RESOURCE' }).Count -eq 4) "testhost=$($th.observed)/$($th2.observed)"
}
catch {
    Check 'selftest.unexpected_error' $false $_.Exception.Message
}

$failed = @($results | Where-Object { $_.result -ne 'PASS' }).Count
$summary = "selftest cases=$($results.Count) passed=$($results.Count - $failed) failed=$failed user_sid=$me elevated=$(([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))"
Write-Output $summary
if ($Report) { Write-Json $Report ([ordered]@{ tool = 'b1-ops/selftest.ps1'; time_utc = [DateTime]::UtcNow.ToString('o'); computer = $env:COMPUTERNAME; summary = $summary; cases = $results.ToArray() }) }
if (-not $Keep) {
    try { & icacls.exe $Root /reset /T /C /Q | Out-Null; Remove-Item -LiteralPath $Root -Recurse -Force } catch { Write-Warning "scratch root not fully removed: $($_.Exception.Message)" }
}
exit $(if ($failed) { 1 } else { 0 })
