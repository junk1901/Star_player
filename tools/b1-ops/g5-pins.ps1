<#
.SYNOPSIS
  B1 G5 execution-pin tool: Trace (diagnostic load observation) | Adopt (offline replay of a trace's process attribution) |
  Collect (candidate pins) | Verify (re-hash pins).

.DESCRIPTION
  Trace   Starts one command and samples its process tree until the root process exits: every process image is recorded as
          PROCESS_STARTED and every module the OS lists for a tree process as IMAGE_LOADED (the observation states of
          B1_B4_OWNER_NORMATIVE_DECISION_DRAFT.md 6.1).
          A process INSTANCE is (pid, exact creation time, UTC ticks truncated to the microsecond, the precision both
          Win32_Process.CreationDate and Process.StartTime share); no tolerance window is used. Attribution rule (Step-Tree):
            * the root instance is the started process;
            * another instance is ADOPTED only if, in the same snapshot, its parent pid is held by an instance already in the
              tree (same pid AND same creation time) and it was created at or after that parent;
            * if its parent pid was a tree pid but that parent instance is no longer provable (exited, or the pid now belongs
              to another instance), it is UNVERIFIED_PARENT: listed, never attributed, its modules never read;
            * a process whose parent pid never belonged to the tree is ignored (a descendant of an unobserved short-lived
              intermediate is therefore missed, never wrongly included).
          Modules are read through ONE process handle (B1OpsNative ModuleReader, G5-R05): OpenProcess once, the creation time
          read from that handle must equal the instance's, the list comes from EnumProcessModulesEx / GetModuleFileNameExW on
          the same handle, the process must still be running afterwards, and the handle is closed. The pid is never resolved a
          second time (an open handle also keeps the pid from being reused). A different instance, an exit before or during the
          read, a failed open or a failed / partial list yields no module; every read status is counted per instance
          (module_read_statuses; module_reads_failed counts OPEN_FAILED, TIME_UNREADABLE, READ_FAILED and INCOMPLETE).
          Outputs: <out> (path, state, first_ms, instances, images),
          <out>.processes.tsv (status, pid, created_utc, ppid, parent_created_utc, ...) and <out>.raw.tsv (every snapshot row
          whose pid or parent pid belonged to the tree), which -Mode Adopt replays offline.
          It is a non-elevated, sampling diagnostic: a module loaded and unloaded between two samples, a short-lived child, a file
          only opened (not mapped) and an IL assembly the runtime maps without the OS loader are not seen. Absence is
          NOT_OBSERVED_IN_RUN, never "unused". The operational trace uses ETW (kernel process / image load + file I/O + CLR loader
          events) on the approved machine, see B1_G5_OPERATIONAL_DESIGN_DRAFT.md 8; an ETW gap is reported, never filled in.
  Adopt   Replays Step-Tree over a raw snapshot file (-Raw, columns sample, pid, ppid, created_ticks, image) from a root
          (-RootPid, -RootTicks) and prints the ADOPTED / UNVERIFIED_PARENT instances as JSON. Used by the self-test and to
          re-check a trace's attribution independently of the run.
  Collect Turns observed paths (Trace TSV or a plain list, one path per line) into a pin candidate file: canonical path, no reparse
          point on the way, SHA-256 of the bytes, and the source files each path was observed in; files under an -ExcludeRoot
          (the build root, pinned by Build Provenance) are left out. dotnet.exe goes to "dotnet", the first git.exe to "git",
          everything else to "execution_pins" in the exact shape of the verifier install configuration (path + raw_sha256). The
          file is marked CANDIDATE_NOT_APPROVED: only the OWNER turns candidates into approved pins.
  Verify  Re-hashes every pin of a candidate file or an install configuration (dotnet, git, execution_pins) from a handle opened
          without write / delete sharing, the way the verifier does. Exit 1 on any MISMATCH, MISSING, REPARSE or DUPLICATE.

  Exit: 0 PASS, 1 failure, 2 usage.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Trace', 'Adopt', 'Collect', 'Verify')][string]$Mode,
    [string]$FilePath,
    [string]$ArgumentList = '',
    [string]$WorkingDirectory,
    [string]$Out,
    [string[]]$List = @(),
    [string]$Pins,
    [string[]]$ExcludeRoot = @(),
    [string]$DotnetRoot = 'C:\Program Files\dotnet',
    [string]$GitRoot = 'C:\Program Files\Git',
    [int]$IntervalMs = 50,
    [string]$Raw,
    [int]$RootPid,
    [int64]$RootTicks
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

function Fail-Usage([string]$m) { [Console]::Error.WriteLine("usage: $m"); exit 2 }
function Test-Under([string]$path, [string]$root) { $path.StartsWith($root.TrimEnd('\') + '\', 'OrdinalIgnoreCase') }
function Get-Sha([byte[]]$b) { ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($b)) -replace '-', '').ToLowerInvariant() }
$utf8 = New-Object Text.UTF8Encoding($false)

# The first reparse point on the path from the file up to the drive root, or $null.
function Get-Reparse([string]$path) {
    $p = $path
    while ($p) {
        if (([IO.File]::GetAttributes($p) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { return $p }
        $p = [IO.Path]::GetDirectoryName($p)
    }
    $null
}

function Get-Category([string]$p) {
    $win = $env:windir
    if (Test-Under $p $DotnetRoot) {
        $rel = $p.Substring($DotnetRoot.TrimEnd('\').Length + 1)
        if ($rel -ieq 'dotnet.exe') { return 'DOTNET_HOST' }
        if ($rel -like 'host\fxr\*') { return 'DOTNET_HOSTFXR' }
        if ($rel -like 'shared\*') { return 'DOTNET_SHARED_RUNTIME' }
        if ($rel -like 'sdk\*') { return 'DOTNET_SDK' }
        return 'DOTNET_OTHER'
    }
    if (Test-Under $p $GitRoot) { return 'GIT' }
    if ($win -and (Test-Under $p $win)) { return 'WINDOWS' }
    'OTHER'
}

function Get-Ticks([datetime]$t) { $u = $t.ToUniversalTime().Ticks; $u - ($u % 10) }      # microsecond precision, UTC

# One snapshot (rows: pid, ppid, ticks) applied to the attribution state (see the header). Repeats until stable, so a parent and its
# child first seen in the same snapshot are both handled.
function New-TreeState([int]$rootPid, [int64]$rootTicks) {
    $s = @{ Tree = @{}; Unverified = @{}; Pids = @{ $rootPid = $true } }
    $s.Tree["$rootPid@$rootTicks"] = [pscustomobject]@{ status = 'ROOT'; pid = $rootPid; ticks = $rootTicks; ppid = 0; parent_ticks = 0 }
    $s
}
function Step-Tree($state, $rows) {
    $alive = @{}
    foreach ($r in $rows) { $alive[[int]$r.pid] = [int64]$r.ticks }
    do {
        $added = 0
        foreach ($r in $rows) {
            $key = "$($r.pid)@$($r.ticks)"
            if ($state.Tree.ContainsKey($key) -or $state.Unverified.ContainsKey($key)) { continue }
            if (-not $state.Pids.ContainsKey([int]$r.ppid)) { continue }                 # parent never part of the tree
            $parentKey = if ($alive.ContainsKey([int]$r.ppid)) { "$($r.ppid)@$($alive[[int]$r.ppid])" } else { $null }
            if ($parentKey -and $state.Tree.ContainsKey($parentKey) -and [int64]$r.ticks -ge $alive[[int]$r.ppid]) {
                $state.Tree[$key] = [pscustomobject]@{ status = 'ADOPTED'; pid = [int]$r.pid; ticks = [int64]$r.ticks; ppid = [int]$r.ppid; parent_ticks = $alive[[int]$r.ppid] }
                $state.Pids[[int]$r.pid] = $true
                $added++
            }
            else {
                $why = if (-not $parentKey) { 'parent exited before the child was observed' } elseif (-not $state.Tree.ContainsKey($parentKey)) { 'parent pid now belongs to another instance' } else { 'child older than its parent' }
                $state.Unverified[$key] = [pscustomobject]@{ status = 'UNVERIFIED_PARENT'; pid = [int]$r.pid; ticks = [int64]$r.ticks; ppid = [int]$r.ppid; parent_ticks = $(if ($parentKey) { $alive[[int]$r.ppid] } else { 0 }); reason = $why }
            }
        }
    } while ($added)
}
function Format-Ticks([int64]$t) { if ($t -le 0) { '' } else { ([datetime]::new($t, [DateTimeKind]::Utc)).ToString('yyyy-MM-ddTHH:mm:ss.ffffffZ') } }

if ($Mode -eq 'Adopt') {
    if (-not $Raw -or -not $RootPid -or -not $RootTicks) { Fail-Usage 'Adopt needs -Raw, -RootPid and -RootTicks.' }
    $state = New-TreeState $RootPid $RootTicks
    $rows = @([IO.File]::ReadAllLines($Raw, $utf8) | Select-Object -Skip 1 | Where-Object { $_ } | ForEach-Object {
            $f = $_ -split "`t"; [pscustomobject]@{ sample = [int]$f[0]; pid = [int]$f[1]; ppid = [int]$f[2]; ticks = [int64]$f[3] } })
    foreach ($g in ($rows | Group-Object sample | Sort-Object { [int]$_.Name })) { Step-Tree $state $g.Group }
    $replay = [ordered]@{
        adopted = @($state.Tree.Values | Sort-Object ticks | ForEach-Object { "$($_.status) $($_.pid)@$($_.ticks) parent=$($_.ppid)@$($_.parent_ticks)" })
        unverified = @($state.Unverified.Values | Sort-Object ticks | ForEach-Object { "$($_.pid)@$($_.ticks) parent=$($_.ppid) ($($_.reason))" })
    }
    Write-Output ($replay | ConvertTo-Json -Depth 3)
    exit 0
}

if ($Mode -eq 'Trace') {
    if (-not $FilePath -or -not $Out) { Fail-Usage 'Trace needs -FilePath and -Out.' }
    if (Test-Path -LiteralPath $Out) { Fail-Usage "'$Out' already exists." }
    $start = @{ FilePath = $FilePath; PassThru = $true; NoNewWindow = $true; RedirectStandardOutput = "$Out.stdout.txt"; RedirectStandardError = "$Out.stderr.txt" }
    if ($ArgumentList) { $start.ArgumentList = $ArgumentList }
    if ($WorkingDirectory) { $start.WorkingDirectory = $WorkingDirectory }
    if (-not ('B1Ops.ModuleReader' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'B1OpsNative.cs') }
    $api = New-Object B1Ops.Win32ProcessApi
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $root = Start-Process @start
    $null = $root.Handle                                                        # keeps the handle so ExitCode and StartTime survive the exit
    $state = New-TreeState $root.Id (Get-Ticks $root.StartTime)
    $info = @{}                                                                 # instance key -> image, command line, first_ms, module read counters
    $seen = @{}                                                                 # path|state -> row
    function Note([string]$path, [string]$st, [string]$key, [string]$image) {
        if (-not $path) { return }
        $k = $path.ToLowerInvariant() + '|' + $st
        if (-not $seen.ContainsKey($k)) { $seen[$k] = [pscustomobject]@{ state = $st; path = $path; first_ms = $clock.ElapsedMilliseconds; instances = @{}; images = @{} } }
        $seen[$k].instances[$key] = $true
        $seen[$k].images[$image] = $true
    }
    $rawLines = New-Object System.Collections.Generic.List[string]
    $rawLines.Add("sample`tpid`tppid`tcreated_ticks`timage")
    $samples = 0
    do {
        $exited = $root.HasExited
        foreach ($t in @($state.Tree.Values)) {                                 # modules of live tree instances first: the discovery query is slow
            $key = "$($t.pid)@$($t.ticks)"
            if (-not $info.ContainsKey($key)) { $info[$key] = @{ image = ''; command_line = ''; first_ms = $clock.ElapsedMilliseconds; ok = 0; failed = 0; statuses = @{} } }
            $read = [B1Ops.ModuleReader]::Read($api, $t.pid, $t.ticks)          # one handle: creation time checked, modules listed, closed
            $st = $info[$key].statuses
            $st[$read.Status] = 1 + $(if ($st.ContainsKey($read.Status)) { $st[$read.Status] } else { 0 })
            if ($read.Status -eq 'OK') {
                $img = [IO.Path]::GetFileName($read.Modules[0])                 # the first module is the process image
                foreach ($m in $read.Modules) { Note $m 'IMAGE_LOADED' $key $img }
                $info[$key].ok++
            }
            elseif ($read.Status -in 'OPEN_FAILED', 'TIME_UNREADABLE', 'READ_FAILED', 'INCOMPLETE') { $info[$key].failed++ }    # counted, never skipped
        }
        $snapshot = @(Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId, ExecutablePath, CommandLine, CreationDate | Where-Object { $_.CreationDate })
        $rows = @($snapshot | ForEach-Object { [pscustomobject]@{ pid = [int]$_.ProcessId; ppid = [int]$_.ParentProcessId; ticks = (Get-Ticks $_.CreationDate); image = $_.ExecutablePath; cmd = $_.CommandLine } })
        Step-Tree $state $rows
        foreach ($r in $rows) {
            if ($state.Pids.ContainsKey($r.pid) -or $state.Pids.ContainsKey($r.ppid)) { $rawLines.Add("$samples`t$($r.pid)`t$($r.ppid)`t$($r.ticks)`t$($r.image)") }
            $key = "$($r.pid)@$($r.ticks)"
            if (($state.Tree.ContainsKey($key) -or $state.Unverified.ContainsKey($key)) -and (-not $info.ContainsKey($key) -or -not $info[$key].image)) {
                if (-not $info.ContainsKey($key)) { $info[$key] = @{ ok = 0; failed = 0; first_ms = $clock.ElapsedMilliseconds; statuses = @{} } }
                $info[$key].image = [string]$r.image; $info[$key].command_line = [string]$r.cmd
                if ($state.Tree.ContainsKey($key)) { Note $r.image 'PROCESS_STARTED' $key ([IO.Path]::GetFileName([string]$r.image)) }
            }
        }
        $samples++
        if (-not $exited) { Start-Sleep -Milliseconds $IntervalMs }
    } while (-not $exited)
    $root.WaitForExit()
    $lines = @("state`tpath`tfirst_ms`tinstances`timages")
    $lines += @($seen.Values | Sort-Object state, path | ForEach-Object { "$($_.state)`t$($_.path)`t$($_.first_ms)`t$((@($_.instances.Keys) | Sort-Object) -join ',')`t$((@($_.images.Keys) | Sort-Object) -join ',')" })
    [IO.File]::WriteAllLines($Out, $lines, $utf8)
    $pl = @("status`tpid`tcreated_ticks`tcreated_utc`tppid`tparent_created_utc`tfirst_ms`tmodule_reads_ok`tmodule_reads_failed`tmodule_read_statuses`treason`timage`tcommand_line")
    foreach ($t in @(@($state.Tree.Values) + @($state.Unverified.Values) | Sort-Object ticks)) {
        $key = "$($t.pid)@$($t.ticks)"
        $i = if ($info.ContainsKey($key)) { $info[$key] } else { @{ image = ''; command_line = ''; first_ms = ''; ok = 0; failed = 0; statuses = @{} } }
        $reason = if ($t.PSObject.Properties['reason']) { $t.reason } else { '' }
        $sts = (@($i.statuses.Keys) | Sort-Object | ForEach-Object { "$_=$($i.statuses[$_])" }) -join ';'
        $pl += "$($t.status)`t$($t.pid)`t$($t.ticks)`t$(Format-Ticks $t.ticks)`t$($t.ppid)`t$(Format-Ticks $t.parent_ticks)`t$($i.first_ms)`t$($i.ok)`t$($i.failed)`t$sts`t$reason`t$($i.image)`t$($i.command_line)"
    }
    [IO.File]::WriteAllLines("$Out.processes.tsv", $pl, $utf8)
    [IO.File]::WriteAllLines("$Out.raw.tsv", $rawLines, $utf8)
    $failedReads = @($info.Values | Where-Object { $_.failed -gt 0 }).Count
    Write-Output "exit=$($root.ExitCode) samples=$samples adopted=$($state.Tree.Count) unverified=$($state.Unverified.Count) instances_with_failed_module_reads=$failedReads paths=$($seen.Count) root=$($root.Id)@$(Get-Ticks $root.StartTime) ms=$($clock.ElapsedMilliseconds)"
    Write-Output 'RESULT=TRACED (sampling diagnostic; absence is NOT_OBSERVED_IN_RUN)'
    exit 0
}

if ($Mode -eq 'Collect') {
    if (-not $List.Count -or -not $Out) { Fail-Usage 'Collect needs -List and -Out.' }
    if (Test-Path -LiteralPath $Out) { Fail-Usage "'$Out' already exists." }
    $List = @($List | ForEach-Object { $_ -split '\|' } | Where-Object { $_ })
    $ExcludeRoot = @($ExcludeRoot | ForEach-Object { $_ -split '\|' } | Where-Object { $_ } | ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\') })
    $paths = @{}; $from = @{}
    $sources = foreach ($f in $List) {
        $bytes = [IO.File]::ReadAllBytes($f)
        $rows = @($utf8.GetString($bytes) -split "`r?`n" | Where-Object { $_ })
        $col = -1
        if ($rows.Count -and $rows[0] -match "`t") { $col = [Array]::IndexOf(($rows[0] -split "`t"), 'path'); $rows = @($rows | Select-Object -Skip 1) }
        foreach ($r in $rows) {
            $p = if ($col -ge 0) { ($r -split "`t")[$col] } else { $r.Trim() }
            if ($p) {
                $full = [IO.Path]::GetFullPath($p); $k = $full.ToLowerInvariant(); $paths[$k] = $full
                if (-not $from.ContainsKey($k)) { $from[$k] = @{} }
                $from[$k][[IO.Path]::GetFileName($f)] = $true
            }
        }
        [ordered]@{ file = [IO.Path]::GetFullPath($f); sha256 = (Get-Sha $bytes) }
    }
    $files = New-Object System.Collections.Generic.List[object]
    $refused = New-Object System.Collections.Generic.List[string]
    foreach ($p in @($paths.Values)) {
        if (@($ExcludeRoot | Where-Object { Test-Under $p $_ }).Count) { continue }
        if (-not [IO.File]::Exists($p)) { $refused.Add("MISSING $p"); continue }
        $rp = Get-Reparse $p
        if ($rp) { $refused.Add("REPARSE $p (at $rp)"); continue }
        $len = (New-Object IO.FileInfo($p)).Length
        $files.Add([pscustomobject]@{ path = $p; raw_sha256 = (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant(); byte_length = $len; category = (Get-Category $p); observed_in = @(@($from[$p.ToLowerInvariant()].Keys) | Sort-Object) })
    }
    $sorted = $files.ToArray()
    [Array]::Sort($sorted, [Comparison[object]] { param($a, $b) [string]::CompareOrdinal($a.path, $b.path) })
    $dotnet = @($sorted | Where-Object { $_.category -eq 'DOTNET_HOST' }) | Select-Object -First 1
    $git = @($sorted | Where-Object { [IO.Path]::GetFileName($_.path) -ieq 'git.exe' }) | Select-Object -First 1
    $pinsOut = @($sorted | Where-Object { $_ -ne $dotnet -and $_ -ne $git } | ForEach-Object { [ordered]@{ path = $_.path; raw_sha256 = $_.raw_sha256 } })
    $doc = [ordered]@{
        format = 'starplayer-g5-pin-candidates-v1'; status = 'CANDIDATE_NOT_APPROVED'; computer = $env:COMPUTERNAME; time_utc = [DateTime]::UtcNow.ToString('o')
        sources = @($sources); exclude_roots = $ExcludeRoot
        dotnet = $(if ($dotnet) { [ordered]@{ path = $dotnet.path; raw_sha256 = $dotnet.raw_sha256 } } else { $null })
        git = $(if ($git) { [ordered]@{ path = $git.path; raw_sha256 = $git.raw_sha256 } } else { $null })
        execution_pins = $pinsOut; files = $sorted; refused = $refused.ToArray()
    }
    [IO.File]::WriteAllText($Out, ($doc | ConvertTo-Json -Depth 5), $utf8)
    foreach ($g in ($sorted | Group-Object category)) { Write-Output ("{0,-22} {1}" -f $g.Name, $g.Count) }
    foreach ($x in $refused) { Write-Output "    - $x" }
    Write-Output "RESULT=$(if ($refused.Count) { 'COLLECTED_WITH_REFUSALS' } else { 'COLLECTED' }) files=$($sorted.Count) execution_pins=$($pinsOut.Count) out=$Out sha256=$(Get-Sha ([IO.File]::ReadAllBytes($Out)))"
    exit $(if ($refused.Count) { 1 } else { 0 })
}

# Verify
if (-not $Pins) { Fail-Usage 'Verify needs -Pins.' }
$j = [IO.File]::ReadAllText($Pins, $utf8) | ConvertFrom-Json
$all = New-Object System.Collections.Generic.List[object]
foreach ($k in 'dotnet', 'git') { if ($j.PSObject.Properties[$k] -and $j.$k) { $all.Add([pscustomobject]@{ field = $k; path = [string]$j.$k.path; raw_sha256 = [string]$j.$k.raw_sha256 }) } }
$i = 0
foreach ($x in @($j.execution_pins)) { if ($x) { $all.Add([pscustomobject]@{ field = "execution_pins/$i"; path = [string]$x.path; raw_sha256 = [string]$x.raw_sha256 }) }; $i++ }
$dup = @{}
$rows = foreach ($p in $all) {
    $status = 'OK'
    $key = $p.path.ToLowerInvariant()
    if ($dup.ContainsKey($key)) { $status = 'DUPLICATE' }
    $dup[$key] = $true
    if ($status -eq 'OK' -and -not [IO.File]::Exists($p.path)) { $status = 'MISSING' }
    if ($status -eq 'OK' -and (Get-Reparse $p.path)) { $status = 'REPARSE' }
    if ($status -eq 'OK') {
        $s = New-Object IO.FileStream($p.path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try { $sha = ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($s)) -replace '-', '').ToLowerInvariant() } finally { $s.Dispose() }
        if ($sha -ne $p.raw_sha256) { $status = 'MISMATCH' }
    }
    [pscustomobject]@{ field = $p.field; status = $status; path = $p.path }
}
$bad = @($rows | Where-Object { $_.status -ne 'OK' })
foreach ($b in $bad) { Write-Output "    - $($b.status) $($b.field) $($b.path)" }
Write-Output "RESULT=$(if ($bad.Count) { 'FAIL' } else { 'PASS' }) pins=$(@($rows).Count) bad=$($bad.Count) source=$Pins"
exit $(if ($bad.Count) { 1 } else { 0 })
