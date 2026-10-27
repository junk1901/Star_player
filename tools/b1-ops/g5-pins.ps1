<#
.SYNOPSIS
  B1 G5 execution-pin tool: Trace (diagnostic load observation) | Collect (candidate pins) | Verify (re-hash pins).

.DESCRIPTION
  Trace   Starts one command and samples its process tree until the root process exits: every process image is recorded as
          PROCESS_STARTED and every module the OS lists for a tree process as IMAGE_LOADED (the observation states of
          B1_B4_OWNER_NORMATIVE_DECISION_DRAFT.md 6.1). A process is identified by (pid, creation time), so a pid reused by an
          unrelated process after a tree process exited is not attributed. It is a non-elevated, sampling diagnostic: a module loaded and unloaded
          between two samples, a short-lived child, a file only opened (not mapped) and an IL assembly the runtime maps without the
          OS loader are not seen. Absence is NOT_OBSERVED_IN_RUN, never "unused". The operational trace uses ETW (kernel
          image load + file I/O + CLR loader events) on the approved machine, see B1_G5_OPERATIONAL_DESIGN_DRAFT.md.
  Collect Turns observed paths (Trace TSV or a plain list, one path per line) into a pin candidate file: canonical path, no reparse
          point on the way, SHA-256 of the bytes; files under an -ExcludeRoot (the build root, pinned by Build Provenance) are left
          out. dotnet.exe goes to "dotnet", the first git.exe to "git", everything else to "execution_pins" in the exact shape of
          the verifier install configuration (path + raw_sha256). The file is marked CANDIDATE_NOT_APPROVED: only the OWNER turns
          candidates into approved pins.
  Verify  Re-hashes every pin of a candidate file or an install configuration (dotnet, git, execution_pins) from a handle opened
          without write / delete sharing, the way the verifier does. Exit 1 on any MISMATCH, MISSING, REPARSE or DUPLICATE.

  Exit: 0 PASS, 1 failure, 2 usage.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Trace', 'Collect', 'Verify')][string]$Mode,
    [string]$FilePath,
    [string]$ArgumentList = '',
    [string]$WorkingDirectory,
    [string]$Out,
    [string[]]$List = @(),
    [string]$Pins,
    [string[]]$ExcludeRoot = @(),
    [string]$DotnetRoot = 'C:\Program Files\dotnet',
    [string]$GitRoot = 'C:\Program Files\Git',
    [int]$IntervalMs = 50
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

if ($Mode -eq 'Trace') {
    if (-not $FilePath -or -not $Out) { Fail-Usage 'Trace needs -FilePath and -Out.' }
    if (Test-Path -LiteralPath $Out) { Fail-Usage "'$Out' already exists." }
    $start = @{ FilePath = $FilePath; PassThru = $true; NoNewWindow = $true; RedirectStandardOutput = "$Out.stdout.txt"; RedirectStandardError = "$Out.stderr.txt" }
    if ($ArgumentList) { $start.ArgumentList = $ArgumentList }
    if ($WorkingDirectory) { $start.WorkingDirectory = $WorkingDirectory }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $root = Start-Process @start
    $null = $root.Handle                                                        # keeps the handle so ExitCode survives the exit
    # pid -> creation time (UTC). A pid is reused once its process exits, so identity is (pid, creation time): a process belongs to the
    # tree only if its parent pid is still held by the recorded tree process (or by nobody) and it was created after that process.
    $tree = @{ $root.Id = $root.StartTime.ToUniversalTime() }
    function Same([datetime]$a, [datetime]$b) { [Math]::Abs(($a - $b).TotalMilliseconds) -lt 1000 }
    $procs = @{}                                                                # pid -> process row
    $seen = @{}                                                                 # path -> row
    function Note([string]$path, [string]$state, [string]$image) {
        if (-not $path) { return }
        $k = $path.ToLowerInvariant() + '|' + $state
        if (-not $seen.ContainsKey($k)) { $seen[$k] = [pscustomobject]@{ state = $state; path = $path; first_ms = $clock.ElapsedMilliseconds; processes = @{} } }
        $seen[$k].processes[$image] = $true
    }
    $samples = 0
    do {
        $exited = $root.HasExited
        foreach ($id in @($tree.Keys)) {                                       # modules of the known tree first: the discovery query is slow
            try {
                $p = [Diagnostics.Process]::GetProcessById($id)
                if (-not (Same $p.StartTime.ToUniversalTime() $tree[$id])) { continue }      # the pid now belongs to another process
                foreach ($m in $p.Modules) { Note $m.FileName 'IMAGE_LOADED' "$($p.ProcessName).exe" }
            }
            catch { }
        }
        $snapshot = @(Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId, ExecutablePath, CommandLine, CreationDate)
        $holder = @{}
        foreach ($c in $snapshot) { $holder[[int]$c.ProcessId] = $c.CreationDate.ToUniversalTime() }
        foreach ($c in $snapshot) {
            $cid = [int]$c.ProcessId; $pp = [int]$c.ParentProcessId; $created = $c.CreationDate.ToUniversalTime()
            if (-not $tree.ContainsKey($cid) -and $tree.ContainsKey($pp) -and $created -ge $tree[$pp].AddSeconds(-1) -and
                (-not $holder.ContainsKey($pp) -or (Same $holder[$pp] $tree[$pp]))) { $tree[$cid] = $created }
            if ($tree.ContainsKey($cid) -and (Same $created $tree[$cid]) -and -not $procs.ContainsKey($cid)) {
                $procs[[int]$c.ProcessId] = [pscustomobject]@{ pid = [int]$c.ProcessId; ppid = [int]$c.ParentProcessId; image = $c.ExecutablePath; command_line = $c.CommandLine; first_ms = $clock.ElapsedMilliseconds }
                Note $c.ExecutablePath 'PROCESS_STARTED' ([IO.Path]::GetFileName([string]$c.ExecutablePath))
            }
        }
        $samples++
        if (-not $exited) { Start-Sleep -Milliseconds $IntervalMs }
    } while (-not $exited)
    $root.WaitForExit()
    $lines = @("state`tpath`tfirst_ms`tprocesses")
    $lines += @($seen.Values | Sort-Object state, path | ForEach-Object { "$($_.state)`t$($_.path)`t$($_.first_ms)`t$((@($_.processes.Keys) | Sort-Object) -join ',')" })
    [IO.File]::WriteAllLines($Out, $lines, $utf8)
    $pl = @("pid`tppid`tfirst_ms`timage`tcommand_line") + @($procs.Values | Sort-Object first_ms | ForEach-Object { "$($_.pid)`t$($_.ppid)`t$($_.first_ms)`t$($_.image)`t$($_.command_line)" })
    [IO.File]::WriteAllLines("$Out.processes.tsv", $pl, $utf8)
    Write-Output "exit=$($root.ExitCode) samples=$samples processes=$($procs.Count) paths=$($seen.Count) ms=$($clock.ElapsedMilliseconds)"
    Write-Output 'RESULT=TRACED (sampling diagnostic; absence is NOT_OBSERVED_IN_RUN)'
    exit 0
}

if ($Mode -eq 'Collect') {
    if (-not $List.Count -or -not $Out) { Fail-Usage 'Collect needs -List and -Out.' }
    if (Test-Path -LiteralPath $Out) { Fail-Usage "'$Out' already exists." }
    $List = @($List | ForEach-Object { $_ -split '\|' } | Where-Object { $_ })
    $ExcludeRoot = @($ExcludeRoot | ForEach-Object { $_ -split '\|' } | Where-Object { $_ } | ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\') })
    $paths = @{}
    $sources = foreach ($f in $List) {
        $bytes = [IO.File]::ReadAllBytes($f)
        $rows = @($utf8.GetString($bytes) -split "`r?`n" | Where-Object { $_ })
        $col = -1
        if ($rows.Count -and $rows[0] -match "`t") { $col = [Array]::IndexOf(($rows[0] -split "`t"), 'path'); $rows = @($rows | Select-Object -Skip 1) }
        foreach ($r in $rows) {
            $p = if ($col -ge 0) { ($r -split "`t")[$col] } else { $r.Trim() }
            if ($p) { $full = [IO.Path]::GetFullPath($p); $paths[$full.ToLowerInvariant()] = $full }
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
        $files.Add([pscustomobject]@{ path = $p; raw_sha256 = (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant(); byte_length = $len; category = (Get-Category $p) })
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
