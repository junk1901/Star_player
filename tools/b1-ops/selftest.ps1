<#
.SYNOPSIS
  Self-test of the b1-ops tools in an isolated scratch root (non-elevated; no operational ACL, account, anchor, pin or remote is touched).

.DESCRIPTION
  -Root must not exist; it is created, used as the -DiagnosticRoot of every ACL-changing call and removed at the end unless -Keep.
  The only paths outside it are read-only: the probe of C:\Program Files\dotnet and Git (handles opened and closed, nothing
  written) and the pin hash of dotnet.exe. Faults (a failing restore item, a concurrent writer, a path swap after the preflight, a
  failed group lookup) are injected through g5-acl's -DiagnosticInject, which the tool accepts only inside a diagnostic root.
  Secret-shaped test values are assembled at run time so this file never matches a1-scan's patterns. Exit 0 when every case passes.
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
function Git { $ErrorActionPreference = 'Continue'; & git.exe @args 2>&1 | Out-Null }
function Check([string]$name, [bool]$ok, [string]$detail) {
    $results.Add([pscustomobject]@{ case = $name; result = $(if ($ok) { 'PASS' } else { 'FAIL' }); detail = $detail })
    Write-Output ("{0} {1}  {2}" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $name, $detail)
}
function Sid([string]$s) { New-Object Security.Principal.SecurityIdentifier($s) }
function Get-Dacl([string]$p) { if ([IO.Directory]::Exists($p)) { [IO.Directory]::GetAccessControl($p, 'Access') } else { [IO.File]::GetAccessControl($p, 'Access') } }
function Set-Dacl([string]$p, $a) { if ([IO.Directory]::Exists($p)) { [IO.Directory]::SetAccessControl($p, $a) } else { [IO.File]::SetAccessControl($p, $a) } }
function Sddl([string]$p) { (Get-Acl -LiteralPath $p).GetSecurityDescriptorSddlForm('Owner, Access') }
function Tree-Sddl([string]$p) { @(Get-ChildItem -LiteralPath $p -Recurse -Force | Sort-Object FullName | ForEach-Object { "$($_.FullName)|$(Sddl $_.FullName)" }) + @("$p|$(Sddl $p)") }
function Same-Tree($a, $b) { @(Compare-Object $a $b).Count -eq 0 }
function Test-Reparse([string]$p) { ([IO.File]::GetAttributes($p) -band [IO.FileAttributes]::ReparsePoint) -ne 0 }
function Siblings([string]$t, [string]$file, [string[]]$names) {
    foreach ($x in $names) { [void][IO.Directory]::CreateDirectory("$t\$x\sub"); [IO.File]::WriteAllText("$t\$x\f.txt", $x); [IO.File]::WriteAllText("$t\$x\sub\g.txt", $x) }
    Write-Json $file ([ordered]@{ format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = [ordered]@{ verifier = 'S-1-5-19' }
            roots = @($names | ForEach-Object { [ordered]@{ id = "R-$_"; path = "$t\$_"; apply = $true; writers = @(); readers = @('verifier') } }) })
}
function Write-Json([string]$path, $obj) { [IO.File]::WriteAllText($path, ($obj | ConvertTo-Json -Depth 6), $utf8) }
function Junction([string]$link, [string]$target) { [void](cmd.exe /c mklink /J "$link" "$target") }
function New-Tree([string]$t) {
    foreach ($d in 'base\build\sub', 'base\trust') { [void][IO.Directory]::CreateDirectory("$t\$d") }
    [IO.File]::WriteAllText("$t\base\build\sub\a.dll", 'a'); [IO.File]::WriteAllText("$t\base\build\b.json", 'b'); [IO.File]::WriteAllText("$t\base\trust\anchor.json", 'c')
}
function Layout([string]$t, [string]$file, [object[]]$extra = @()) {
    Write-Json $file ([ordered]@{
            format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = [ordered]@{ verifier = 'S-1-5-19'; producer = 'S-1-5-20' }
            roots = @(
                [ordered]@{ id = 'R-BASE'; path = "$t\base"; apply = $true; writers = @(); readers = @('verifier', 'producer') },
                [ordered]@{ id = 'R-TRUST'; path = "$t\base\trust"; apply = $true; writers = @(); readers = @('verifier') },
                [ordered]@{ id = 'R-BUILD'; path = "$t\base\build"; apply = $true; writers = @(); readers = @('verifier', 'producer') },
                [ordered]@{ id = 'R-WORK'; path = "$t\base\work"; apply = $true; writers = @('verifier'); readers = @() }) + $extra
        })
}
function Acl([string]$t, [string]$lay, [string]$snap, [string]$inject) {
    $a = @('-Mode', 'Apply', '-Layout', $lay, '-DiagnosticRoot', $t, '-Snapshot', $snap)
    if ($inject) { $a += @('-DiagnosticInject', $inject) }
    Run 'g5-acl.ps1' $a
}

try {
    # ---------------- g5-acl: rules ----------------
    $t = "$Root\acl"; New-Tree $t; $lay = "$Root\layout.json"; Layout $t $lay
    $before = Tree-Sddl "$t\base"
    $r = Run 'g5-acl.ps1' @('-Mode', 'Plan', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.plan_changes_nothing' ($r.Exit -eq 0 -and (Same-Tree $before (Tree-Sddl "$t\base"))) "exit=$($r.Exit)"
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.verify_refuses_inherited_tree' ($r.Exit -eq 1 -and $r.Text -match 'not protected' -and $r.Text -match 'R-WORK .*FAIL') "exit=$($r.Exit)"
    $r = Acl $t $lay "$Root\snap-a.json" ''
    Check 'acl.apply_pass' ($r.Exit -eq 0 -and $r.Text -match 'RESULT=PASS') "exit=$($r.Exit)"
    $fileSddl = Sddl "$t\base\build\sub\a.dll"
    $explicit = @([regex]::Matches($fileSddl, '\([AD];([A-Z]*);') | Where-Object { $_.Groups[1].Value -notmatch 'ID' })
    Check 'acl.apply_descendants_inherit_only' ($explicit.Count -eq 0 -and $fileSddl -match '\(A;ID;') $fileSddl
    Check 'acl.apply_root_protected' ((Sddl "$t\base\build") -match 'D:PAI') (Sddl "$t\base\build")
    Check 'acl.apply_created_root_born_protected' ((Sddl "$t\base\work") -match 'D:PAI?\(A;OICI;FA;;;SY\)' -and (Sddl "$t\base\work") -match '\(A;OICI;0x1301bf;;;LS\)') (Sddl "$t\base\work")
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.verify_pass_after_apply' ($r.Exit -eq 0) "exit=$($r.Exit)"

    $acl = Get-Dacl "$t\base\build\sub\a.dll"; $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((Sid 'S-1-5-20'), 'Write', 'Allow'))); Set-Dacl "$t\base\build\sub\a.dll" $acl
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
    Junction "$t\base\build\jn" "$t\base\trust"
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.verify_detects_reparse_point' ($r.Exit -eq 1 -and $r.Text -match 'jn: reparse point') "exit=$($r.Exit)"
    [IO.Directory]::Delete("$t\base\build\jn")
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t)
    Check 'acl.verify_pass_after_tamper_undone' ($r.Exit -eq 0) "exit=$($r.Exit)"
    $r = Acl $t $lay "$Root\snap-a.json" ''
    Check 'acl.snapshot_never_overwritten' ($r.Exit -eq 2) "exit=$($r.Exit)"
    $r = Run 'g5-acl.ps1' @('-Mode', 'Apply', '-Layout', $lay, '-Snapshot', "$Root\snap-x.json")
    Check 'acl.apply_refused_without_owner_elevation' ($r.Exit -eq 2 -and -not (Test-Path "$Root\snap-x.json")) "exit=$($r.Exit)"
    $r = Run 'g5-acl.ps1' @('-Mode', 'Apply', '-Layout', $lay, '-Snapshot', "$Root\snap-x.json", '-DiagnosticInject', 'admin-lookup-fail')
    Check 'acl.injection_requires_diagnostic_root' ($r.Exit -eq 2 -and $r.Text -match 'only accepted together with -DiagnosticRoot') "exit=$($r.Exit)"
    $out = "$Root\outside.json"; Write-Json $out ([ordered]@{ format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = @{ v = 'S-1-5-19' }; roots = @([ordered]@{ id = 'X'; path = 'C:\Windows'; apply = $true; writers = @(); readers = @() }) })
    $r = Acl $t $out "$Root\snap-y.json" ''
    Check 'acl.diagnostic_root_confines_roots' ($r.Exit -eq 2) "exit=$($r.Exit)"
    $bad = "$Root\unknown.json"; Write-Json $bad ([ordered]@{ format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = @{ v = 'S-1-5-19' }; roots = @([ordered]@{ id = 'X'; path = "$t\x"; apply = $true; writers = @('nobody'); readers = @() }) })
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $bad, '-DiagnosticRoot', $t)
    Check 'acl.layout_unknown_principal' ($r.Exit -eq 2) "exit=$($r.Exit)"

    # ---------------- G5-R04: a failed membership lookup is never a PASS ----------------
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t, '-DiagnosticInject', 'admin-lookup-fail')
    Check 'acl.r04_admin_lookup_failure_fails_verify' ($r.Exit -eq 1 -and $r.Text -match 'principals FAIL' -and $r.Text -match 'could not be verified') "exit=$($r.Exit)"
    $r = Run 'g5-acl.ps1' @('-Mode', 'Verify', '-Layout', $lay, '-DiagnosticRoot', $t, '-Report', "$Root\verify-r04.json")
    $note = ((Get-Content -LiteralPath "$Root\verify-r04.json" -Raw | ConvertFrom-Json).roots | Where-Object { $_.id -eq 'principals' }).note
    Check 'acl.r04_nested_group_limit_stated_separately' ($r.Exit -eq 0 -and $note -match 'nested group membership NOT resolved') $note

    # ---------------- G5-R01: path protection before any change ----------------
    $t3 = "$Root\acl3"; New-Tree $t3; $lay3 = "$Root\layout3.json"; Layout $t3 $lay3
    Junction "$t3\base\build\jn" "$t3\base\trust"
    $pre = Tree-Sddl "$t3\base"
    $r = Acl $t3 $lay3 "$Root\snap-c.json" ''
    Check 'acl.r01_reparse_under_apply_root_refused_before_change' ($r.Exit -eq 1 -and $r.Text -match 'RESULT=REFUSED' -and -not (Test-Path "$Root\snap-c.json") -and (Same-Tree $pre (Tree-Sddl "$t3\base")) -and -not (Test-Path "$t3\base\work")) "exit=$($r.Exit)"
    [IO.Directory]::Delete("$t3\base\build\jn")

    $t5 = "$Root\acl5"; New-Tree "$t5\real"; Junction "$t5\jup" "$t5\real"; $lay5 = "$Root\layout5.json"; Layout "$t5\jup" $lay5
    $pre = Tree-Sddl "$t5\real"
    $r = Acl $t5 $lay5 "$Root\snap-e.json" ''
    Check 'acl.r01_upper_junction_refused' ($r.Exit -eq 1 -and $r.Text -match 'RESULT=REFUSED' -and $r.Text -match 'jup' -and -not (Test-Path "$Root\snap-e.json") -and (Same-Tree $pre (Tree-Sddl "$t5\real"))) "exit=$($r.Exit)"

    $d6 = "$Root\acl6\d"; New-Tree "$Root\acl6\outside"; [void][IO.Directory]::CreateDirectory($d6); Junction "$d6\link" "$Root\acl6\outside"
    $lay6 = "$Root\layout6.json"; Layout "$d6\link" $lay6
    $pre = Tree-Sddl "$Root\acl6\outside"
    $r = Acl $d6 $lay6 "$Root\snap-f.json" ''
    Check 'acl.r01_junction_inside_diagnostic_root_to_outside_refused' ($r.Exit -eq 1 -and $r.Text -match 'RESULT=REFUSED' -and -not (Test-Path "$Root\snap-f.json") -and (Same-Tree $pre (Tree-Sddl "$Root\acl6\outside"))) "exit=$($r.Exit)"

    $t7 = "$Root\acl7"; New-Tree $t7; $lay7 = "$Root\layout7.json"; Layout $t7 $lay7
    $r = Acl $t7 $lay7 "$Root\snap-g.json" 'swap-after-check:base\build'
    Check 'acl.r01_swap_after_check_blocked_by_held_handle' ($r.Exit -eq 0 -and $r.Text -match 'injected: BLOCKED' -and -not (Test-Path "$t7\base\build.swapped") -and -not (Test-Reparse "$t7\base\build")) "exit=$($r.Exit)"

    $lay8 = "$Root\layout8.json"
    Write-Json $lay8 ([ordered]@{ format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = [ordered]@{ producer = $me }; roots = @([ordered]@{ id = 'R-X'; path = "$Root\acl8\x"; apply = $true; writers = @(); readers = @() }) })
    [void][IO.Directory]::CreateDirectory("$Root\acl8")
    $r = Acl "$Root\acl8" $lay8 "$Root\snap-h.json" 'maintenance-check'
    Check 'acl.r01_maintenance_condition_refuses_running_principal' ($r.Exit -eq 1 -and $r.Text -match 'principal process is running' -and -not (Test-Path "$Root\acl8\x")) "exit=$($r.Exit)"

    # ---------------- G5-R02: a nested root that is not applied stays unchanged ----------------
    $t9 = "$Root\acl9"; New-Tree $t9; [void][IO.Directory]::CreateDirectory("$t9\base\archive\run"); [IO.File]::WriteAllText("$t9\base\archive\run\e.json", 'e')
    $lay9 = "$Root\layout9.json"; Layout $t9 $lay9 @([ordered]@{ id = 'R-ARCHIVE'; path = "$t9\base\archive"; apply = $false; writers = @('producer'); readers = @('verifier') })
    $preBase = Tree-Sddl "$t9\base"
    $r = Acl $t9 $lay9 "$Root\snap-i.json" ''
    Check 'acl.r02_inheriting_nonapplied_nested_root_refused' ($r.Exit -eq 1 -and $r.Text -match 'G5-R02' -and -not (Test-Path "$Root\snap-i.json") -and (Same-Tree $preBase (Tree-Sddl "$t9\base"))) "exit=$($r.Exit)"
    $acl = New-Object Security.AccessControl.DirectorySecurity
    $acl.SetSecurityDescriptorSddlForm("D:PAI(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;FA;;;$me)(A;OICI;0x1301bf;;;NS)(A;OICI;0x1200a9;;;LS)", 'Access')
    Set-Dacl "$t9\base\archive" $acl                                    # protected, owner and principals as the layout expects
    $preArchive = Tree-Sddl "$t9\base\archive"
    $r = Acl $t9 $lay9 "$Root\snap-j.json" ''
    Check 'acl.r02_protected_nonapplied_nested_root_unchanged' ($r.Exit -eq 0 -and (Same-Tree $preArchive (Tree-Sddl "$t9\base\archive")) -and (Sddl "$t9\base") -match 'D:PAI') "exit=$($r.Exit)"
    $t10 = "$Root\acl10"; New-Tree $t10; $lay10 = "$Root\layout10.json"; Layout $t10 $lay10 @([ordered]@{ id = 'R-ARCHIVE'; path = "$t10\base\archive"; apply = $false; writers = @(); readers = @() })
    $pre = Tree-Sddl "$t10\base"
    $r = Acl $t10 $lay10 "$Root\snap-k.json" ''
    Check 'acl.r02_missing_nonapplied_nested_root_refused' ($r.Exit -eq 1 -and $r.Text -match 'must already exist' -and (Same-Tree $pre (Tree-Sddl "$t10\base")) -and -not (Test-Path "$t10\base\archive")) "exit=$($r.Exit)"

    # ---------------- G5-R03: rollback ----------------
    $t2 = "$Root\acl2"; New-Tree $t2; $lay2 = "$Root\layout2.json"; Layout $t2 $lay2
    $acl = Get-Dacl "$t2\base\build\b.json"; $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((Sid 'S-1-1-0'), 'ReadAndExecute', 'Allow'))); Set-Dacl "$t2\base\build\b.json" $acl
    $pre = Tree-Sddl "$t2\base"
    $r = Acl $t2 $lay2 "$Root\snap-b.json" ''
    $applied = $r.Exit -eq 0 -and (Test-Path "$t2\base\work")
    $r = Run 'g5-acl.ps1' @('-Mode', 'Restore', '-Snapshot', "$Root\snap-b.json", '-DiagnosticRoot', $t2)
    Check 'acl.r03_manual_restore_round_trip' ($applied -and $r.Exit -eq 0 -and (Same-Tree $pre (Tree-Sddl "$t2\base")) -and -not (Test-Path "$t2\base\work")) "applied=$applied restore_exit=$($r.Exit)"

    $t11 = "$Root\acl11"; New-Tree $t11; $lay11 = "$Root\layout11.json"; Layout $t11 $lay11
    $pre = Tree-Sddl "$t11\base"
    $r = Acl $t11 $lay11 "$Root\snap-l.json" 'apply-fail:base\build\sub\a.dll'
    Check 'acl.r03_failed_apply_full_rollback' ($r.Exit -eq 1 -and $r.Text -match 'RESULT=ROLLED_BACK' -and (Same-Tree $pre (Tree-Sddl "$t11\base")) -and -not (Test-Path "$t11\base\work")) "exit=$($r.Exit)"

    $t12 = "$Root\acl12"; $lay12 = "$Root\layout12.json"; Siblings $t12 $lay12 @('a', 'bb')
    $preA = Tree-Sddl "$t12\a"; $preB = Tree-Sddl "$t12\bb"
    $r = Acl $t12 $lay12 "$Root\snap-m.json" 'apply-fail:bb\sub\g.txt|restore-fail:a'
    Check 'acl.r03_first_restore_item_fails_others_restored' ($r.Exit -eq 3 -and $r.Text -match 'RESULT=ROLLBACK_FAILED' -and $r.Text -match 'acl12\\a: not restored' -and (Same-Tree $preB (Tree-Sddl "$t12\bb")) -and -not (Same-Tree $preA (Tree-Sddl "$t12\a"))) "exit=$($r.Exit)"

    $t13 = "$Root\acl13"; $lay13 = "$Root\layout13.json"; Siblings $t13 $lay13 @('a', 'bb', 'ccc')
    $preA = Tree-Sddl "$t13\a"; $preB = Tree-Sddl "$t13\bb"; $preC = Tree-Sddl "$t13\ccc"
    $r = Acl $t13 $lay13 "$Root\snap-n.json" 'apply-fail:ccc\sub\g.txt|restore-fail:bb'
    Check 'acl.r03_middle_restore_item_fails_others_restored' ($r.Exit -eq 3 -and $r.Text -match 'bb: not restored' -and @([regex]::Matches($r.Text, 'not restored')).Count -eq 1 -and (Same-Tree $preA (Tree-Sddl "$t13\a")) -and (Same-Tree $preC (Tree-Sddl "$t13\ccc")) -and -not (Same-Tree $preB (Tree-Sddl "$t13\bb"))) "exit=$($r.Exit)"

    $t14 = "$Root\acl14"; [void][IO.Directory]::CreateDirectory("$t14\other"); $lay14 = "$Root\layout14.json"
    Write-Json $lay14 ([ordered]@{ format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = [ordered]@{ verifier = 'S-1-5-19' }
            roots = @([ordered]@{ id = 'R-DEEP'; path = "$t14\newA\newB\root"; apply = $true; writers = @(); readers = @('verifier') }, [ordered]@{ id = 'R-OTHER'; path = "$t14\other"; apply = $true; writers = @(); readers = @() }) })
    $preOther = Sddl "$t14\other"
    $r = Acl $t14 $lay14 "$Root\snap-o.json" 'apply-fail:other'
    $journal = @(Get-Content -LiteralPath "$Root\snap-o.json.created")
    Check 'acl.r03_deep_new_root_and_intermediates_removed' ($r.Exit -eq 1 -and $r.Text -match 'ROLLED_BACK' -and $journal.Count -eq 3 -and -not (Test-Path "$t14\newA") -and (Test-Path "$t14\other") -and (Sddl "$t14\other") -eq $preOther) "exit=$($r.Exit) journal=$($journal.Count)"

    $t15 = "$Root\acl15"; [void][IO.Directory]::CreateDirectory("$t15\other"); $lay15 = "$Root\layout15.json"
    Write-Json $lay15 ([ordered]@{ format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = [ordered]@{ verifier = 'S-1-5-19' }
            roots = @([ordered]@{ id = 'R-DEEP'; path = "$t15\newA\newB\root"; apply = $true; writers = @(); readers = @('verifier') }, [ordered]@{ id = 'R-OTHER'; path = "$t15\other"; apply = $true; writers = @(); readers = @() }) })
    $r = Acl $t15 $lay15 "$Root\snap-p.json" 'apply-fail:other|drop-file:newA\newB'
    Check 'acl.r03_created_paths_left_behind_are_residue' ($r.Exit -eq 3 -and $r.Text -match 'ROLLBACK_FAILED' -and $r.Text -match 'newB: created by Apply and left in place' -and $r.Text -match 'newA: created by Apply and left in place' -and -not (Test-Path "$t15\newA\newB\root") -and (Test-Path "$t15\other") -and (Test-Path $t15)) "exit=$($r.Exit)"

    # ---------------- G5-FIX-01: a hard link never widens the change ----------------
    function HlTree([string]$t) {                                       # R-BUILD (apply), R-ARCHIVE (not applied, protected, extra NS reader), R-WORK (missing)
        foreach ($d in 'build\sub', 'archive', 'elsewhere') { [void][IO.Directory]::CreateDirectory("$t\$d") }
        [IO.File]::WriteAllText("$t\build\a.dll", 'a'); [IO.File]::WriteAllText("$t\build\sub\b.json", 'b'); [IO.File]::WriteAllText("$t\archive\run.json", 'archive')
        $acl = New-Object Security.AccessControl.DirectorySecurity
        $acl.SetSecurityDescriptorSddlForm("D:PAI(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;FA;;;$me)(A;OICI;0x1200a9;;;LS)(A;OICI;0x1200a9;;;NS)", 'Access')
        Set-Dacl "$t\archive" $acl
        Write-Json "$t.layout.json" ([ordered]@{ format = 'starplayer-g5-layout-v1'; owner_sid = 'S-1-5-32-544'; principals = [ordered]@{ verifier = 'S-1-5-19' }
                roots = @([ordered]@{ id = 'R-BUILD'; path = "$t\build"; apply = $true; writers = @(); readers = @('verifier') },
                    [ordered]@{ id = 'R-ARCHIVE'; path = "$t\archive"; apply = $false; writers = @(); readers = @('verifier') },
                    [ordered]@{ id = 'R-WORK'; path = "$t\work"; apply = $true; writers = @('verifier'); readers = @() }) })
    }
    function Without($tree, [string]$name) { @($tree | Where-Object { $_ -notlike "*\$name|*" }) }
    function HlRefused([string]$case, [string]$t, [string]$inject, [string]$why) {
        $preB = Tree-Sddl "$t\build"; $preA = Tree-Sddl "$t\archive"; $preE = Tree-Sddl "$t\elsewhere"
        $r = Acl $t "$t.layout.json" "$t.snap.json" $inject
        Check $case ($r.Exit -eq 1 -and $r.Text -match 'RESULT=REFUSED' -and $r.Text -match $why -and -not (Test-Path "$t.snap.json") -and -not (Test-Path "$t\work") -and
            (Same-Tree $preB (Tree-Sddl "$t\build")) -and (Same-Tree $preA (Tree-Sddl "$t\archive")) -and (Same-Tree $preE (Tree-Sddl "$t\elsewhere"))) "exit=$($r.Exit)"
    }
    $h1 = "$Root\hl1"; HlTree $h1; New-Item -ItemType HardLink -Path "$h1\build\linked.json" -Target "$h1\archive\run.json" | Out-Null
    HlRefused 'acl.fix01_archive_file_linked_into_build_refused' $h1 '' 'linked\.json: 2 hard links'
    $h2 = "$Root\hl2"; HlTree $h2; New-Item -ItemType HardLink -Path "$h2\elsewhere\copy.json" -Target "$h2\build\sub\b.json" | Out-Null
    HlRefused 'acl.fix01_link_in_other_directory_refused' $h2 '' 'b\.json: 2 hard links'
    $h3 = "$Root\hl3"; HlTree $h3; New-Item -ItemType HardLink -Path "$h3\build\sub\a-again.dll" -Target "$h3\build\a.dll" | Out-Null
    HlRefused 'acl.fix01_two_links_in_same_root_refused' $h3 '' 'a\.dll: 2 hard links'
    HlRefused 'acl.fix01_same_file_id_cross_check_refused' $h3 'links-misreported:build\a.dll|links-misreported:build\sub\a-again.dll' 'same file ID as'
    $h4 = "$Root\hl4"; HlTree $h4
    HlRefused 'acl.fix01_link_count_unreadable_refused' $h4 'link-query-fail:build\sub\b.json' 'could not be read'

    $h5 = "$Root\hl5"; HlTree $h5
    $preB = Tree-Sddl "$h5\build"; $preA = Tree-Sddl "$h5\archive"
    $r = Acl $h5 "$h5.layout.json" "$h5.snap.json" 'add-link-after-check:archive\late.json=build\a.dll'
    Check 'acl.fix01_link_added_after_check_refused_before_change' ($r.Exit -eq 1 -and $r.Text -match 'RESULT=REFUSED' -and $r.Text -match 'a\.dll: 2 hard links' -and $r.Text -match 'LINKED' -and
        -not (Test-Path "$h5.snap.json") -and -not (Test-Path "$h5\work") -and (Same-Tree $preB (Tree-Sddl "$h5\build")) -and (Same-Tree $preA (Without (Tree-Sddl "$h5\archive") 'late.json'))) "exit=$($r.Exit)"

    $h6 = "$Root\hl6"; HlTree $h6
    $preB = Tree-Sddl "$h6\build"; $preA = Tree-Sddl "$h6\archive"; $preRun = Sddl "$h6\archive\run.json"
    $r = Acl $h6 "$h6.layout.json" "$h6.snap.json" 'add-link-after-check:build\late.json=archive\run.json'
    Check 'acl.fix01_link_into_apply_root_after_check_never_written' ($r.Exit -eq 1 -and $r.Text -match 'RESULT=ROLLED_BACK' -and $r.Text -match 'late\.json: 2 hard links' -and
        (Sddl "$h6\archive\run.json") -eq $preRun -and (Same-Tree $preA (Tree-Sddl "$h6\archive")) -and (Same-Tree $preB (Without (Tree-Sddl "$h6\build") 'late.json')) -and -not (Test-Path "$h6\work")) "exit=$($r.Exit)"

    $h7 = "$Root\hl7"; HlTree $h7
    $preB = Tree-Sddl "$h7\build"; $preA = Tree-Sddl "$h7\archive"; $preDll = Sddl "$h7\build\a.dll"
    $r = Acl $h7 "$h7.layout.json" "$h7.snap.json" 'add-link-after-change:archive\late2.json=build\a.dll'
    Check 'acl.fix01_link_added_after_change_rolled_back_through_handle' ($r.Exit -eq 1 -and $r.Text -match 'RESULT=ROLLED_BACK' -and $r.Text -match 'a\.dll: 2 hard links' -and
        (Same-Tree $preB (Tree-Sddl "$h7\build")) -and (Sddl "$h7\archive\late2.json") -eq $preDll -and (Same-Tree $preA (Without (Tree-Sddl "$h7\archive") 'late2.json')) -and -not (Test-Path "$h7\work")) "exit=$($r.Exit)"

    $h8 = "$Root\hl8"; HlTree $h8
    $preA = Tree-Sddl "$h8\archive"
    $r = Acl $h8 "$h8.layout.json" "$h8.snap.json" ''
    Check 'acl.fix01_single_link_apply_pass' ($r.Exit -eq 0 -and $r.Text -match 'RESULT=PASS' -and (Same-Tree $preA (Tree-Sddl "$h8\archive")) -and (Test-Path "$h8\work")) "exit=$($r.Exit)"
    [void][IO.Directory]::CreateDirectory("$h8\build\os-dir"); [IO.File]::WriteAllText("$h8\build\sub\os-file.txt", 'os')
    Check 'acl.fix01_exact_descriptors_equal_windows_inheritance' ((Sddl "$h8\build\os-dir") -eq (Sddl "$h8\build\sub") -and (Sddl "$h8\build\sub\os-file.txt") -eq (Sddl "$h8\build\sub\b.json") -and (Sddl "$h8\build\a.dll") -eq (Sddl "$h8\build\sub\b.json")) (Sddl "$h8\build\sub\b.json")
    $r = Run 'g5-acl.ps1' @('-Mode', 'Restore', '-Snapshot', "$h8.snap.json", '-DiagnosticRoot', $h8)
    $restoredOk = $r.Exit -eq 0
    Remove-Item -LiteralPath "$h8\build\a.dll"; [IO.File]::WriteAllText("$h8\build\a.dll", 'replaced')
    $r = Run 'g5-acl.ps1' @('-Mode', 'Restore', '-Snapshot', "$h8.snap.json", '-DiagnosticRoot', $h8)
    Check 'acl.fix01_restore_refuses_a_replaced_object' ($restoredOk -and $r.Exit -eq 3 -and $r.Text -match "a\.dll: not restored: file ID .* is not the snapshot's object") "restore_exit=$($r.Exit)"

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
    Check 'pins.collect' ($r.Exit -eq 0 -and $cand.dotnet.path -eq 'C:\Program Files\dotnet\dotnet.exe' -and @($cand.execution_pins).Count -eq 2 -and $cand.status -eq 'CANDIDATE_NOT_APPROVED' -and @($cand.files[0].observed_in) -contains 'list.txt') "exit=$($r.Exit)"
    $r = Run 'g5-pins.ps1' @('-Mode', 'Verify', '-Pins', "$n\cand.json")
    Check 'pins.verify_pass' ($r.Exit -eq 0) "exit=$($r.Exit)"
    [IO.File]::WriteAllText("$n\real\one.dll", 'changed'); [IO.File]::Delete("$n\real\two.dll")
    $r = Run 'g5-pins.ps1' @('-Mode', 'Verify', '-Pins', "$n\cand.json")
    Check 'pins.verify_detects_replacement_and_removal' ($r.Exit -eq 1 -and $r.Text -match 'MISMATCH .*one\.dll' -and $r.Text -match 'MISSING .*two\.dll') "exit=$($r.Exit)"
    $cfg = "$n\config-like.json"; Write-Json $cfg ([ordered]@{ format = 'x'; execution_pins = @([ordered]@{ path = 'C:\Program Files\dotnet\dotnet.exe'; raw_sha256 = $cand.dotnet.raw_sha256 }, [ordered]@{ path = 'C:\Program Files\dotnet\dotnet.exe'; raw_sha256 = $cand.dotnet.raw_sha256 }) })
    $r = Run 'g5-pins.ps1' @('-Mode', 'Verify', '-Pins', $cfg)
    Check 'pins.verify_detects_duplicate' ($r.Exit -eq 1 -and $r.Text -match 'DUPLICATE') "exit=$($r.Exit)"
    Junction "$n\jn" "$n\real"
    [IO.File]::WriteAllLines("$n\list2.txt", [string[]]@("$n\jn\one.dll"))
    $r = Run 'g5-pins.ps1' @('-Mode', 'Collect', '-List', "$n\list2.txt", '-Out', "$n\cand2.json")
    Check 'pins.collect_refuses_reparse_path' ($r.Exit -eq 1 -and $r.Text -match 'REPARSE') "exit=$($r.Exit)"
    [IO.Directory]::Delete("$n\jn")
    $r = Run 'g5-pins.ps1' @('-Mode', 'Trace', '-FilePath', "$env:windir\System32\WindowsPowerShell\v1.0\powershell.exe", '-ArgumentList', '-NoProfile -Command Start-Sleep -Seconds 2', '-Out', "$n\trace.tsv")
    $tr = @(Get-Content -LiteralPath "$n\trace.tsv")
    $pt = @(Get-Content -LiteralPath "$n\trace.tsv.processes.tsv")
    Check 'pins.trace_observes_modules' ($r.Exit -eq 0 -and @($tr | Where-Object { $_ -match "^IMAGE_LOADED`t.*\\ntdll\.dll`t" }).Count -eq 1) "lines=$($tr.Count)"
    Check 'pins.r05_trace_records_instance_identity' ($pt[0] -match '^status\tpid\tcreated_ticks\tcreated_utc\tppid\tparent_created_utc' -and $pt[1] -match '^ROOT\t\d+\t\d+0\t\d{4}-\d\d-\d\dT[\d:.]+Z' -and (Test-Path "$n\trace.tsv.raw.tsv")) ($pt[1] -replace "`t", ' | ')

    # G5-R05: attribution rule replayed over a recorded snapshot sequence (pid reuse, fast reuse, exited parent, unobserved parent)
    $raw = @("sample`tpid`tppid`tcreated_ticks`timage",
        "0`t600`t200`t1150`tgrandchild-listed-before-its-parent", "0`t100`t1`t1000`troot", "0`t200`t100`t1100`tchild", "0`t500`t777`t1200`tparent-never-observed",
        "1`t100`t1`t1000`troot", "1`t200`t999`t1300`tunrelated-reuses-pid-200", "1`t300`t200`t1400`tchild-of-the-reused-pid",
        "2`t100`t1`t1000`troot", "2`t400`t200`t1500`tchild-whose-parent-exited",
        "3`t100`t1`t1000`troot", "3`t200`t555`t1610`tfast-reuse-1us-later", "3`t301`t200`t1620`tchild-of-the-fast-reuse",
        "4`t100`t1`t1010`troot-pid-reused-1us-later", "4`t700`t100`t2000`tchild-of-the-reused-root-pid")
    [IO.File]::WriteAllLines("$n\raw-fixture.tsv", [string[]]$raw, $utf8)
    $r = Run 'g5-pins.ps1' @('-Mode', 'Adopt', '-Raw', "$n\raw-fixture.tsv", '-RootPid', '100', '-RootTicks', '1000')
    $j = $r.Text | ConvertFrom-Json
    $ad = @($j.adopted); $un = @($j.unverified)
    Check 'pins.r05_normal_parent_child_adopted' ($ad -contains 'ROOT 100@1000 parent=0@0' -and $ad -contains 'ADOPTED 200@1100 parent=100@1000' -and $ad -contains 'ADOPTED 600@1150 parent=200@1100' -and $ad.Count -eq 3) ($ad -join ' ; ')
    Check 'pins.r05_pid_reuse_not_adopted' (@($un | Where-Object { $_ -like '300@1400 *pid now belongs to another instance*' }).Count -eq 1 -and -not (@($ad) -match '@1300')) ''
    Check 'pins.r05_parent_exited_then_pid_reused_child_unverified' (@($un | Where-Object { $_ -like '400@1500 *parent exited*' }).Count -eq 1) ''
    Check 'pins.r05_fast_reuse_sub_second_is_another_instance' (@($un | Where-Object { $_ -like '301@1620 *another instance*' }).Count -eq 1 -and @($un | Where-Object { $_ -like '700@2000 *another instance*' }).Count -eq 1) ''
    Check 'pins.r05_unobserved_parent_not_attributed' (-not (@($ad + $un) -match '^.*500@1200')) ''

    # G5-R05: module reading bound to one process handle, driven through a scripted IProcessApi (deterministic races, no real pid reuse)
    $fakeCs = @'
using System; using System.Collections.Generic;
public sealed class FakeProc { public long Ticks; public string[] Mods; public bool Alive = true; }
public sealed class FakeProcessApi : B1Ops.IProcessApi
{
    public Dictionary<int, FakeProc> Pids = new Dictionary<int, FakeProc>();
    private readonly Dictionary<IntPtr, FakeProc> handles = new Dictionary<IntPtr, FakeProc>();
    private int next = 100;
    public int Opens, Closes, OpenError;
    public string Event = "";          // exit-after-time | reuse-before-modules | exit-during-modules | partial | fail-modules
    public FakeProc Reuser;            // takes the pid over when the instance exits
    private void Exit(int pid) { foreach (var kv in Pids) if (kv.Key == pid) kv.Value.Alive = false; if (Reuser != null) Pids[pid] = Reuser; }
    public IntPtr Open(int pid, out int e)
    {
        Opens++; e = 0;
        if (OpenError != 0) { e = OpenError; return IntPtr.Zero; }
        FakeProc p; if (!Pids.TryGetValue(pid, out p) || !p.Alive) { e = 87; return IntPtr.Zero; }
        IntPtr h = new IntPtr(next++); handles[h] = p; return h;
    }
    public long CreationTicks(IntPtr h)
    {
        long t = handles[h].Ticks;
        if (Event == "exit-after-time") { foreach (var kv in new List<KeyValuePair<int, FakeProc>>(Pids)) if (kv.Value == handles[h]) Exit(kv.Key); Event = ""; }
        return t;
    }
    public bool IsRunning(IntPtr h)
    {
        bool alive = handles[h].Alive;      // reuse-before-modules: the instance exits and its pid is reused right after this check passed
        if (alive && Event == "reuse-before-modules") { foreach (var kv in new List<KeyValuePair<int, FakeProc>>(Pids)) if (kv.Value == handles[h]) Exit(kv.Key); Event = ""; }
        return alive;
    }
    public string[] Modules(IntPtr h, out bool complete, out string error)
    {
        FakeProc p = handles[h]; complete = false; error = null;
        if (!p.Alive || Event == "fail-modules") { error = "EnumProcessModulesEx failed (299)"; return null; }
        if (Event == "partial") { error = "GetModuleFileNameExW failed for module 1 of 2"; return new string[] { p.Mods[0], null }; }
        if (Event == "exit-during-modules") { foreach (var kv in new List<KeyValuePair<int, FakeProc>>(Pids)) if (kv.Value == p) Exit(kv.Key); }
        complete = true; return p.Mods;
    }
    public void Close(IntPtr h) { Closes++; handles.Remove(h); }
}
public static class PreFixPatternModel
{
    // The call pattern of g5-pins before the fix: GetProcessById + StartTime and .Modules each resolve the pid on their own.
    public static string[] Read(B1Ops.IProcessApi api, int pid, long expected)
    {
        int e; IntPtr h = api.Open(pid, out e); if (h == IntPtr.Zero) return null;
        long t = api.CreationTicks(h); api.Close(h);
        if (B1Ops.ModuleReader.Micro(t) != expected) return null;
        h = api.Open(pid, out e); if (h == IntPtr.Zero) return null;
        bool c; string err; string[] m = api.Modules(h, out c, out err); api.Close(h); return m;
    }
}
'@
    [IO.File]::WriteAllText("$Root\FakeProcessApi.cs", $fakeCs, $utf8)
    $runner = @'
param([string]$Native, [string]$Fake)
$ErrorActionPreference = 'Stop'
Add-Type -Path $Native, $Fake
function Api([string]$event, [int]$openError = 0) {
    $a = New-Object FakeProcessApi; $a.Event = $event; $a.OpenError = $openError
    $a.Pids[42] = New-Object FakeProc -Property @{ Ticks = 1000; Mods = [string[]]@('C:\app\child.exe', 'C:\app\real.dll') }
    $a.Reuser = New-Object FakeProc -Property @{ Ticks = 2000; Mods = [string[]]@('C:\evil\other.exe', 'C:\evil\evil.dll') }
    $a
}
$out = [ordered]@{}
foreach ($c in @(@('normal', ''), @('reuse_before_modules', 'reuse-before-modules'), @('exit_before_modules', 'exit-after-time'), @('exit_during_modules', 'exit-during-modules'),
        @('partial', 'partial'), @('fail_modules', 'fail-modules'))) {
    $a = Api $c[1]; $r = [B1Ops.ModuleReader]::Read($a, 42, 1000)
    $out[$c[0]] = [ordered]@{ status = $r.Status; modules = @($r.Modules); opens = $a.Opens; closes = $a.Closes; handle_ticks = $r.HandleTicks; identity = $r.Identity }
}
$a = Api '' 5; $r = [B1Ops.ModuleReader]::Read($a, 42, 1000); $out['open_denied'] = [ordered]@{ status = $r.Status; modules = @($r.Modules); opens = $a.Opens; closes = $a.Closes }
$a = Api ''; $a.Pids.Remove(42) | Out-Null; $r = [B1Ops.ModuleReader]::Read($a, 42, 1000); $out['gone'] = [ordered]@{ status = $r.Status; opens = $a.Opens; closes = $a.Closes }
$a = Api ''; $a.Pids[42] = $a.Reuser; $r = [B1Ops.ModuleReader]::Read($a, 42, 1000); $out['other_instance'] = [ordered]@{ status = $r.Status; modules = @($r.Modules); closes = $a.Closes }
$a = Api 'exit-after-time'; $out['prefix_model_reuse'] = [ordered]@{ modules = @([PreFixPatternModel]::Read($a, 42, 1000)); opens = $a.Opens }
$out | ConvertTo-Json -Depth 4
'@
    [IO.File]::WriteAllText("$Root\r05-runner.ps1", $runner, $utf8)
    $ErrorActionPreference = 'Continue'
    $fj = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$Root\r05-runner.ps1" -Native (Join-Path $here 'B1OpsNative.cs') -Fake "$Root\FakeProcessApi.cs" 2>&1 | ForEach-Object { "$_" }) -join "`n"
    $ErrorActionPreference = 'Stop'
    $f = $fj | ConvertFrom-Json
    function NoMods($x) { @($x.modules | Where-Object { $_ }).Count -eq 0 }
    Check 'pins.r05_normal_read_one_handle' ($f.normal.status -eq 'OK' -and (@($f.normal.modules) -join ',') -eq 'C:\app\child.exe,C:\app\real.dll' -and $f.normal.opens -eq 1 -and $f.normal.closes -eq 1 -and $f.normal.handle_ticks -eq 1000) "$($f.normal.status) opens=$($f.normal.opens)"
    Check 'pins.r05_pid_reused_after_time_check_never_reads_other_instance' ($f.reuse_before_modules.status -eq 'READ_FAILED' -and (NoMods $f.reuse_before_modules) -and $f.reuse_before_modules.opens -eq 1 -and $f.reuse_before_modules.closes -eq 1) "$($f.reuse_before_modules.status)"
    Check 'pins.r05_prefix_call_pattern_model_reads_other_instance' ((@($f.prefix_model_reuse.modules) -join ',') -eq 'C:\evil\other.exe,C:\evil\evil.dll' -and $f.prefix_model_reuse.opens -eq 2) 'model of the pre-fix pattern (separate opens)'
    Check 'pins.r05_exit_before_read_no_modules' ($f.exit_before_modules.status -eq 'EXITED_BEFORE_READ' -and (NoMods $f.exit_before_modules) -and $f.exit_before_modules.closes -eq 1) "$($f.exit_before_modules.status)"
    Check 'pins.r05_exit_during_read_list_discarded' ($f.exit_during_modules.status -eq 'EXITED_DURING_READ' -and (NoMods $f.exit_during_modules) -and $f.exit_during_modules.closes -eq 1) "$($f.exit_during_modules.status)"
    Check 'pins.r05_open_failure_no_handle_no_modules' ($f.open_denied.status -eq 'OPEN_FAILED' -and (NoMods $f.open_denied) -and $f.open_denied.opens -eq 1 -and $f.open_denied.closes -eq 0 -and $f.gone.status -eq 'NOT_RUNNING') "$($f.open_denied.status)/$($f.gone.status)"
    Check 'pins.r05_partial_or_failed_list_not_used' ($f.partial.status -eq 'INCOMPLETE' -and (NoMods $f.partial) -and $f.fail_modules.status -eq 'READ_FAILED' -and (NoMods $f.fail_modules) -and $f.partial.closes -eq 1 -and $f.fail_modules.closes -eq 1) "$($f.partial.status)/$($f.fail_modules.status)"
    Check 'pins.r05_other_instance_no_modules' ($f.other_instance.status -eq 'OTHER_INSTANCE' -and (NoMods $f.other_instance) -and $f.other_instance.closes -eq 1) "$($f.other_instance.status)"
    $r = Run 'g5-pins.ps1' @('-Mode', 'Trace', '-FilePath', "$env:windir\System32\cmd.exe", '-ArgumentList', "/c $env:windir\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -Command Start-Sleep -Seconds 2", '-Out', "$n\trace-child.tsv")
    $rows = @(Get-Content -LiteralPath "$n\trace-child.tsv.processes.tsv" | Select-Object -Skip 1 | ForEach-Object { , ($_ -split "`t") })
    $child = @($rows | Where-Object { $_[0] -eq 'ADOPTED' -and $_[11] -like '*powershell.exe' })
    Check 'pins.r05_real_child_adopted_and_read_through_handle' ($r.Exit -eq 0 -and $child.Count -ge 1 -and [int]$child[0][7] -ge 1 -and $child[0][9] -match '(^|;)OK=\d+' -and
        @(Get-Content -LiteralPath "$n\trace-child.tsv" | Where-Object { $_ -match "^IMAGE_LOADED`t.*\\System\.Management\.Automation\.ni\.dll`t|^IMAGE_LOADED`t.*\\System\.Management\.Automation\.dll`t" }).Count -ge 1) $(if ($child.Count) { ($child[0][0..9] -join ' | ') } else { "rows=$($rows.Count) exit=$($r.Exit)" })

    # ---------------- a1-scan ----------------
    $g = "$Root\repo"; [void][IO.Directory]::CreateDirectory($g)
    Git -C $g init -q; Git -C $g config user.name selftest; Git -C $g config user.email selftest@example.invalid
    [IO.File]::WriteAllText("$g\k.txt", ('-----BEGIN RSA ' + 'PRIVATE KEY-----') + "`n" + ('AK' + 'IA' + 'ABCDEFGHIJKLMNOP') + "`n"); Git -C $g add k.txt; Git -C $g commit -q -m one
    Git -C $g rm -q k.txt; [IO.File]::WriteAllText("$g\ok.txt", 'path C:\' + 'Users\someone\x' + "`n"); Git -C $g add ok.txt; Git -C $g commit -q -m two
    $r = Run 'a1-scan.ps1' @('-Repo', $g, '-Commit', 'HEAD', '-Out', "$Root\scan1")
    $s = Get-Content -LiteralPath "$Root\scan1\summary.json" -Raw | ConvertFrom-Json
    $cls = @{}; foreach ($c in $s.classes) { $cls[$c.class] = $c.findings }
    Check 'scan.finds_secrets_removed_from_head' ($r.Exit -eq 1 -and $cls['private_key'] -eq 1 -and $cls['aws_access_key'] -eq 1) "exit=$($r.Exit)"
    Check 'scan.reports_privacy_classes' ($cls['user_profile_path'] -eq 1 -and $s.commits -eq 2) "user_profile_path=$($cls['user_profile_path'])"
    $g2 = "$Root\repo2"; [void][IO.Directory]::CreateDirectory($g2)
    Git -C $g2 init -q; Git -C $g2 config user.name selftest; Git -C $g2 config user.email selftest@example.invalid
    [IO.File]::WriteAllText("$g2\a.txt", "nothing here`n"); Git -C $g2 add a.txt; Git -C $g2 commit -q -m clean
    $r = Run 'a1-scan.ps1' @('-Repo', $g2, '-Commit', 'HEAD', '-Out', "$Root\scan2")
    Check 'scan.clean_repo_passes_without_claiming_absence' ($r.Exit -eq 0 -and $r.Text -match 'NO_SECRET_PATTERN_FOUND \(pattern-based; not proof') "exit=$($r.Exit)"

    # A1-R01: every commit metadata field is scanned (fake values assembled at run time, only in this scratch repository)
    $g3 = "$Root\repo3"; [void][IO.Directory]::CreateDirectory($g3)
    Git -C $g3 init -q; Git -C $g3 config user.name selftest; Git -C $g3 config user.email selftest@example.invalid
    [IO.File]::WriteAllText("$g3\a.txt", "clean`n"); Git -C $g3 add a.txt
    Git -C $g3 -c ('user.name=build ' + 'AK' + 'IA' + 'QRSTUVWXYZ234567') commit -q -m 'author name field'
    [IO.File]::WriteAllText("$g3\a.txt", "clean 2`n"); Git -C $g3 add a.txt
    $env:GIT_COMMITTER_EMAIL = ('gh' + 'p_' + ('a' * 36) + '@example.invalid'); Git -C $g3 commit -q -m 'committer e-mail field'; Remove-Item Env:\GIT_COMMITTER_EMAIL
    [IO.File]::WriteAllText("$g3\a.txt", "clean 3`n"); Git -C $g3 add a.txt
    $msg = "subject`n`nfirst paragraph " + [char]0x1e + [char]0x1f + "`n`n----`n%x1e`n`nlater paragraph`n" + ('-----BEGIN EC ' + 'PRIVATE KEY-----') + "`n"
    [IO.File]::WriteAllText("$Root\msg.txt", $msg, $utf8); Git -C $g3 commit -q -F "$Root\msg.txt"
    $r = Run 'a1-scan.ps1' @('-Repo', $g3, '-Commit', 'HEAD', '-Out', "$Root\scan3")
    $s = Get-Content -LiteralPath "$Root\scan3\summary.json" -Raw | ConvertFrom-Json
    $paths = @{}; foreach ($c in $s.classes) { $paths[$c.class] = @($c.paths) }
    Check 'scan.a1r01_author_name_scanned' ($r.Exit -eq 1 -and $paths['aws_access_key'] -contains '(commit:author_name)') ($paths['aws_access_key'] -join ',')
    Check 'scan.a1r01_committer_email_scanned' ($paths['github_token'] -contains '(commit:committer_email)') ($paths['github_token'] -join ',')
    Check 'scan.a1r01_message_after_separators_scanned' ($paths['private_key'] -contains '(commit:message)') ($paths['private_key'] -join ',')
    Check 'scan.a1r01_author_email_privacy_reported' ($paths['email'] -contains '(commit:author_email)' -and $paths['email'] -contains '(commit:committer_email)') ($paths['email'] -join ',')

    # A1-R02: an explicit main push with --no-follow-tags sends no other tag, even with push.followTags=true (local bare remote)
    $g4 = "$Root\repo4"; [void][IO.Directory]::CreateDirectory($g4)
    Git -C $g4 init -q; Git -C $g4 config user.name selftest; Git -C $g4 config user.email selftest@example.invalid
    [IO.File]::WriteAllText("$g4\a.txt", "a`n"); Git -C $g4 add a.txt; Git -C $g4 commit -q -m one; Git -C $g4 tag -a not-approved -m 'annotated, not approved'
    $sha = (& git.exe -C $g4 rev-parse HEAD).Trim()
    Git init -q --bare "$Root\safe.git"; Git init -q --bare "$Root\unsafe.git"
    Git -C $g4 -c push.followTags=true push --no-follow-tags --porcelain "$Root\safe.git" "$($sha):refs/heads/main"
    Git -C $g4 -c push.followTags=true push --porcelain "$Root\unsafe.git" "$($sha):refs/heads/main"
    $safe = @(& git.exe ls-remote "$Root\safe.git"); $unsafe = @(& git.exe ls-remote "$Root\unsafe.git")
    Check 'a1.push_no_follow_tags' ($safe.Count -eq 1 -and $safe[0] -match 'refs/heads/main$' -and @($unsafe | Where-Object { $_ -match 'refs/tags/not-approved' }).Count -ge 1) "safe=$($safe.Count) unsafe=$($unsafe.Count)"

    # A1-R02: post-publication ref check with ls-remote --refs (HEAD and peeled rows are not refs), one fresh bare remote per case
    Git -C $g4 tag -a v-approved -m 'annotated, approved'; Git -C $g4 tag lw-approved
    $tagA = (& git.exe -C $g4 rev-parse refs/tags/v-approved).Trim(); $tagL = (& git.exe -C $g4 rev-parse refs/tags/lw-approved).Trim()
    [IO.File]::WriteAllText("$g4\a.txt", "b`n"); Git -C $g4 add a.txt; Git -C $g4 commit -q -m two
    $sha2 = (& git.exe -C $g4 rev-parse HEAD).Trim()
    function Remote([string]$name, [string[]]$refspecs, [bool]$follow) {
        $b = "$Root\refs-$name.git"; Git init -q --bare --initial-branch=main $b
        $a = @('-C', $g4, '-c', 'push.followTags=true', 'push', '--porcelain'); if (-not $follow) { $a += '--no-follow-tags' }
        Git @($a + @($b) + $refspecs)
        $b
    }
    function RefCheck([string]$remote, [string[]]$expect) { Run 'a1-refcheck.ps1' @('-Remote', $remote, '-Expect', ($expect -join '|')) }
    $main = "refs/heads/main=$sha"
    $b1 = Remote 'main' @("$($sha):refs/heads/main") $false
    $plain = @(& git.exe ls-remote $b1)
    $r = RefCheck $b1 @($main)
    Check 'a1.r02_first_push_main_with_head_passes' ($r.Exit -eq 0 -and $r.Text -match 'RESULT=PASS remote_refs=1' -and @($plain | Where-Object { $_ -match "`tHEAD$" }).Count -eq 1) "exit=$($r.Exit) plain_rows=$($plain.Count)"
    $b2 = Remote 'annotated' @("$($sha):refs/heads/main", 'refs/tags/v-approved:refs/tags/v-approved') $false
    $plain = @(& git.exe ls-remote $b2)
    $r = RefCheck $b2 @($main, "refs/tags/v-approved=$tagA")
    Check 'a1.r02_approved_annotated_tag_passes' ($r.Exit -eq 0 -and $r.Text -match 'RESULT=PASS remote_refs=2' -and @($plain | Where-Object { $_ -match '\^\{\}$' }).Count -eq 1) "exit=$($r.Exit) plain_rows=$($plain.Count)"
    $r = RefCheck $b2 @($main, "refs/tags/v-approved=$sha")
    Check 'a1.r02_annotated_tag_expects_tag_object_not_peeled_commit' ($r.Exit -eq 1 -and $r.Text -match 'MISMATCH refs/tags/v-approved') "exit=$($r.Exit)"
    $b3 = Remote 'lightweight' @("$($sha):refs/heads/main", 'refs/tags/lw-approved:refs/tags/lw-approved') $false
    $r = RefCheck $b3 @($main, "refs/tags/lw-approved=$tagL")
    Check 'a1.r02_approved_lightweight_tag_passes' ($r.Exit -eq 0 -and $tagL -eq $sha) "exit=$($r.Exit)"
    $b4 = Remote 'unapproved' @("$($sha):refs/heads/main", 'refs/tags/v-approved:refs/tags/v-approved', 'refs/tags/not-approved:refs/tags/not-approved') $false
    $r = RefCheck $b4 @($main, "refs/tags/v-approved=$tagA")
    Check 'a1.r02_unapproved_tag_fails' ($r.Exit -eq 1 -and $r.Text -match 'UNEXPECTED refs/tags/not-approved') "exit=$($r.Exit)"
    $b5 = Remote 'follow' @("$($sha):refs/heads/main") $true
    $r = RefCheck $b5 @($main)
    Check 'a1.r02_follow_tags_without_flag_fails' ($r.Exit -eq 1 -and $r.Text -match 'UNEXPECTED refs/tags/') "exit=$($r.Exit)"
    $b6 = Remote 'nofollow' @("$($sha):refs/heads/main") $false
    $r = RefCheck $b6 @($main)
    Check 'a1.r02_follow_tags_config_with_no_follow_tags_passes' ($r.Exit -eq 0) "exit=$($r.Exit)"
    $b7 = Remote 'mismatch' @("$($sha2):refs/heads/main") $false
    $r = RefCheck $b7 @($main)
    Check 'a1.r02_main_sha_mismatch_fails' ($r.Exit -eq 1 -and $r.Text -match 'MISMATCH refs/heads/main') "exit=$($r.Exit)"
    $b8 = Remote 'branch' @("$($sha):refs/heads/main", "$($sha):refs/heads/feature") $false
    $r = RefCheck $b8 @($main)
    Check 'a1.r02_unexpected_branch_fails' ($r.Exit -eq 1 -and $r.Text -match 'UNEXPECTED refs/heads/feature') "exit=$($r.Exit)"
    $r = RefCheck $b6 @($main, "refs/tags/v-approved=$tagA")
    Check 'a1.r02_missing_expected_tag_fails' ($r.Exit -eq 1 -and $r.Text -match 'MISSING refs/tags/v-approved') "exit=$($r.Exit)"
    $r = RefCheck "$Root\no-such-remote.git" @($main)
    Check 'a1.r02_query_error_is_not_pass' ($r.Exit -eq 2 -and $r.Text -match 'RESULT=ERROR') "exit=$($r.Exit)"
    $r1 = RefCheck $b2 @("refs/tags/v-approved=$tagA"); $r2 = RefCheck $b2 @($main, "refs/tags/v-approved^{}=$sha")
    Check 'a1.r02_expectation_must_name_main_and_real_refs' ($r1.Exit -eq 2 -and $r2.Exit -eq 2) "exit=$($r1.Exit)/$($r2.Exit)"

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
    Git -C $b init -q; Git -C $b config user.name selftest; Git -C $b config user.email selftest@example.invalid; Git -C $b commit -q --allow-empty -m empty
    $head = (& git.exe -C $b rev-parse HEAD).Trim()
    [IO.File]::WriteAllLines("$Root\t_StarPlayer.Domain.Tests.tsv", [string[]]@("state`tpath`tfirst_ms`tprocesses", "PROCESS_STARTED`t$b\tests\StarPlayer.Domain.Tests\bin\Release\net8.0\testhost.exe`t1`tx"))
    $r = Run 'b4-candidates.ps1' @('-BuildRoot', $b, '-SourceCommit', $head, '-Out', "$Root\cand.json", '-Trace', "$Root\t_StarPlayer.Domain.Tests.tsv")
    $c = Get-Content -LiteralPath "$Root\cand.json" -Raw | ConvertFrom-Json
    $roles = @{}; foreach ($x in $c.summary.by_role) { $roles[$x.role] = $x.count }
    $th = @($c.artifacts | Where-Object { $_.relative_path -like 'tests/StarPlayer.Domain.Tests/*testhost.exe' })[0]
    $th2 = @($c.artifacts | Where-Object { $_.relative_path -like 'tests/StarPlayer.Simulation.Tests/*testhost.exe' })[0]
    Check 'b4.rule_v2_roles' ($r.Exit -eq 0 -and $c.summary.artifacts -eq 24 -and $c.summary.unpinned -eq 2 -and $roles['SIMHARNESS_BINARY'] -eq 1 -and $roles['VERIFIER_BINARY'] -eq 1 -and $roles['TEST_ASSEMBLY'] -eq 4 -and $roles['RUNTIME_CONFIG'] -eq 1 -and $roles['DEPENDENCY'] -eq 17 -and $c.status -eq 'CANDIDATE_INCOMPLETE' -and @($c.exe_rows_interpretation_pending).Count -eq 4) "artifacts=$($c.summary.artifacts) unpinned=$($c.summary.unpinned) status=$($c.status)"
    Check 'b4.categories_and_observation' ($th.category -eq 'TEST_HOST' -and $th.observed -eq 'PROCESS_STARTED' -and $th2.observed -eq 'NOT_TRACED' -and @($c.artifacts | Where-Object { $_.category -eq 'SATELLITE_RESOURCE' }).Count -eq 4) "testhost=$($th.observed)/$($th2.observed)"

    Junction "$Root\bj" $b
    $r = Run 'b4-candidates.ps1' @('-BuildRoot', "$Root\bj", '-SourceCommit', $head, '-Out', "$Root\cand-up.json")
    Check 'b4.r01_upper_junction_refused' ($r.Exit -eq 1 -and $r.Text -match 'REFUSED' -and -not (Test-Path "$Root\cand-up.json")) "exit=$($r.Exit)"
    [IO.Directory]::Delete("$Root\bj")
    [void][IO.Directory]::CreateDirectory("$Root\elsewhere"); [IO.File]::WriteAllText("$Root\elsewhere\foreign.dll", 'foreign')
    $dom = "$b\tests\StarPlayer.Domain.Tests\bin\Release\net8.0"
    Junction "$dom\linked" "$Root\elsewhere"
    $r = Run 'b4-candidates.ps1' @('-BuildRoot', $b, '-SourceCommit', $head, '-Out', "$Root\cand-in.json")
    Check 'b4.r01_inner_junction_refused' ($r.Exit -eq 1 -and $r.Text -match 'linked: reparse point' -and -not (Test-Path "$Root\cand-in.json")) "exit=$($r.Exit)"
    [IO.Directory]::Delete("$dom\linked")
    $inf = "$b\tests\StarPlayer.Infrastructure.Tests\bin"
    [IO.Directory]::Move($inf, "$Root\escaped-bin"); Junction $inf "$Root\escaped-bin"
    $r = Run 'b4-candidates.ps1' @('-BuildRoot', $b, '-SourceCommit', $head, '-Out', "$Root\cand-esc.json")
    Check 'b4.r01_root_escape_refused' ($r.Exit -eq 1 -and $r.Text -match 'REFUSED' -and -not (Test-Path "$Root\cand-esc.json")) "exit=$($r.Exit)"
    [IO.Directory]::Delete($inf); [IO.Directory]::Move("$Root\escaped-bin", $inf)
    $sim = "$b\tests\StarPlayer.Simulation.Tests\bin\Release\net8.0\StarPlayer.Simulation.Tests.dll"
    [IO.File]::Move($sim, "$sim.away")
    $r = Run 'b4-candidates.ps1' @('-BuildRoot', $b, '-SourceCommit', $head, '-Out', "$Root\cand-miss.json")
    Check 'b4.r01_missing_primary_refused' ($r.Exit -eq 1 -and $r.Text -match 'primary binary StarPlayer\.Simulation\.Tests\.dll' -and -not (Test-Path "$Root\cand-miss.json")) "exit=$($r.Exit)"
    [IO.File]::Move("$sim.away", $sim)
    $dup = "$b\tests\StarPlayer.SimHarness.Tests\bin\Release\net8.0\copy-of-domain.dll"
    New-Item -ItemType HardLink -Path $dup -Target "$dom\StarPlayer.Domain.Tests.dll" | Out-Null
    $r = Run 'b4-candidates.ps1' @('-BuildRoot', $b, '-SourceCommit', $head, '-Out', "$Root\cand-dup.json")
    Check 'b4.r01_duplicate_candidate_refused' ($r.Exit -eq 1 -and $r.Text -match 'hard link' -and -not (Test-Path "$Root\cand-dup.json")) "exit=$($r.Exit)"
    [IO.File]::Delete($dup)
    $r = Run 'b4-candidates.ps1' @('-BuildRoot', $b, '-SourceCommit', $head, '-Out', "$Root\cand-again.json")
    Check 'b4.r01_normal_output_after_cleanup' ($r.Exit -eq 0 -and (Test-Path "$Root\cand-again.json")) "exit=$($r.Exit)"
}
catch {
    Check 'selftest.unexpected_error' $false "$($_.Exception.Message) at line $($_.InvocationInfo.ScriptLineNumber)"
}

$failed = @($results | Where-Object { $_.result -ne 'PASS' }).Count
$summary = "selftest cases=$($results.Count) passed=$($results.Count - $failed) failed=$failed user_sid=$me elevated=$(([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))"
Write-Output $summary
if ($Report) { Write-Json $Report ([ordered]@{ tool = 'b1-ops/selftest.ps1'; time_utc = [DateTime]::UtcNow.ToString('o'); computer = $env:COMPUTERNAME; summary = $summary; cases = $results.ToArray() }) }
if (-not $Keep) {
    try {
        Get-ChildItem -LiteralPath $Root -Recurse -Force -Attributes ReparsePoint -ErrorAction SilentlyContinue | ForEach-Object { [IO.Directory]::Delete($_.FullName) }
        & icacls.exe $Root /reset /T /C /Q | Out-Null; Remove-Item -LiteralPath $Root -Recurse -Force
    }
    catch { Write-Warning "scratch root not fully removed: $($_.Exception.Message)" }
}
exit $(if ($failed) { 1 } else { 0 })
