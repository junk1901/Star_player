# Star Player verification (Phase 0 foundation + Phase 1 player model/diagnostics). Repeatable: run from anywhere.
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1 [-GodotConsolePath <path>]
# Exit code: 0 = PASS, 1 = FAIL (an executed check failed), 2 = INCOMPLETE (a check could not run).
# A check that cannot run is INCOMPLETE and is never reported as PASS.
param(
    [string]$LogName = 'PHASE_1_VERIFY_LOG.md',
    [string]$GodotConsolePath = $(if ($env:STARPLAYER_GODOT) { $env:STARPLAYER_GODOT } else { 'E:\Madegame\tools\godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' })
)
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
# Build servers and MSBuild node reuse outlive the command that started them, keep the redirected output handles open and
# made Start-Process -Wait hang in an independent review. They are switched off for THIS script's process tree only (the
# variables die with the script); no dotnet/MSBuild process of another project is ever touched.
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:UseSharedCompilation = 'false'
$scriptStart = Get-Date

$work = Join-Path $root 'build\verify'
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $work | Out-Null
$results = [System.Collections.Generic.List[object]]::new()

function Add-Result($id, $name, $command, $exit, $status, $detail) {
    $results.Add([pscustomobject]@{ Id = $id; Name = $name; Command = $command; ExitCode = $exit; Status = $status; Detail = $detail })
    Write-Host ("[{0}] {1} (exit={2}) {3}" -f $status, $name, $exit, $detail)
}

# Runs a native command with stdout+stderr captured to a log file; returns @{ Exit; Text; TimedOut }.
# The wait has a time limit: on expiry only the process tree this call started is killed (exit code 124), so a stuck child can
# never block the script forever, and nothing else on the machine is touched.
function Invoke-Logged($id, $exe, [string]$arguments, $workingDirectory = $root, [int]$timeoutSec = 600) {
    $log = Join-Path $work "$id.log"
    $err = Join-Path $work "$id.err"
    $p = Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $workingDirectory -PassThru -NoNewWindow `
        -RedirectStandardOutput $log -RedirectStandardError $err
    $null = $p.Handle   # keep the handle so ExitCode stays readable
    $timedOut = -not $p.WaitForExit($timeoutSec * 1000)
    if ($timedOut) {
        & taskkill.exe /PID $p.Id /T /F | Out-Null
        $exit = 124
    } else {
        $p.WaitForExit()
        $exit = $p.ExitCode
    }
    $text = (Get-Content $log -Raw -Encoding UTF8) + (Get-Content $err -Raw -Encoding UTF8)
    if ($timedOut) { $text = "$text`nTIMED OUT after $timeoutSec s; process tree killed by verify.ps1" ; Write-Host "[TIMEOUT] $id exceeded $timeoutSec s" }
    Set-Content -Path $log -Value $text -Encoding UTF8
    Remove-Item $err -ErrorAction SilentlyContinue
    return @{ Exit = $exit; Text = [string]$text; TimedOut = $timedOut }
}

# ---- 0. Toolchain ----
$pinned = (Get-Content (Join-Path $root 'global.json') -Raw | ConvertFrom-Json).sdk.version
$sdkVersion = (& dotnet --version 2>$null)
if ($sdkVersion -eq $pinned) { Add-Result 'T1' '.NET SDK matches global.json' 'dotnet --version' 0 'PASS' "SDK $sdkVersion" }
else { Add-Result 'T1' '.NET SDK matches global.json' 'dotnet --version' 1 'INCOMPLETE' "pinned $pinned, found '$sdkVersion'" }

$godotReady = Test-Path $GodotConsolePath
$godotVersion = 'not found'
if ($godotReady) {
    $godotVersion = ((& $GodotConsolePath --version 2>$null) | Select-Object -First 1)
    Add-Result 'T2' 'Godot .NET present' "$GodotConsolePath --version" 0 'PASS' $godotVersion
} else {
    Add-Result 'T2' 'Godot .NET present' "$GodotConsolePath --version" 127 'INCOMPLETE' 'Godot executable not found (set STARPLAYER_GODOT or -GodotConsolePath)'
}
$templateDir = Join-Path $env:APPDATA 'Godot\export_templates\4.7.2.stable.mono'
# SHA-256 of the files extracted from the official, SHA-512-verified Godot_v4.7.2-stable_mono_export_templates.tpz.
$templateHashes = [ordered]@{
    'version.txt'                        = '66796c18ef62c6e4a56a5c7412735b1a1bf805b010585d54b5569642e24373e2'
    'windows_debug_x86_64.exe'           = '4526aac3ac47c65de66f99f715f56b35a2e2e4446fcb23e6d280a830cd97fdea'
    'windows_debug_x86_64_console.exe'   = 'f78703ba3a05dd51a3464f8c90d4c72f54f95d5a8669993e7a5c3ce522b30c52'
    'windows_release_x86_64.exe'         = '16e0ec3cfd398b938f514de95ff2474b532b5061ada78f92226dba3f7584d55d'
    'windows_release_x86_64_console.exe' = '7895a7873947deaff6c8c859c1f77be051023413035048e2db74ceba07a65017'
}
$missing = @(); $mismatch = @()
foreach ($name in $templateHashes.Keys) {
    $file = Join-Path $templateDir $name
    if (-not (Test-Path $file)) { $missing += $name }
    elseif ((Get-FileHash $file -Algorithm SHA256).Hash.ToLower() -ne $templateHashes[$name]) { $mismatch += $name }
}
$templatesReady = ($missing.Count -eq 0 -and $mismatch.Count -eq 0)
$t3Name = 'Export templates installed and byte-identical to the official archive'
$t3Cmd = "SHA-256 of 5 files in $templateDir"
if ($templatesReady) { Add-Result 'T3' $t3Name $t3Cmd 0 'PASS' ((Get-Content (Join-Path $templateDir 'version.txt') -Raw).Trim()) }
elseif ($mismatch.Count -gt 0) { Add-Result 'T3' $t3Name $t3Cmd 1 'FAIL' "hash mismatch: $($mismatch -join ', ')" }
else { Add-Result 'T3' $t3Name $t3Cmd 1 'INCOMPLETE' "missing: $($missing -join ', '); run tools\install-export-templates.ps1" }

# ---- 1. Release build ----
$buildStart = (Get-Date).AddSeconds(-2)
$debugDllPath = Join-Path $root 'game\.godot\mono\temp\bin\Debug\StarPlayer.Client.dll'
$debugStamp = if (Test-Path $debugDllPath) { (Get-Item $debugDllPath).LastWriteTimeUtc.Ticks } else { 0 }
# Incremental builds legitimately leave an up-to-date DLL untouched, so freshness means "not older than its compiled sources" (.cs/.csproj; scene files are not compile inputs).
$clientSources = @(Get-ChildItem (Join-Path $root 'game') -Recurse -File -Include *.cs, *.csproj | Where-Object { $_.FullName -notmatch '\\(\.godot|bin|obj)\\' })
$newestClientSource = ($clientSources | Measure-Object LastWriteTimeUtc -Maximum).Maximum
$b = Invoke-Logged 'B1' 'dotnet' 'build StarPlayer.sln -c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false' $root 900
$warnings = if ($b.Text -match '(\d+) Warning\(s\)') { [int]$Matches[1] } else { -1 }
$status = if ($b.Exit -eq 0 -and $warnings -eq 0) { 'PASS' } else { 'FAIL' }
Add-Result 'B1' 'Release build (all 9 projects)' 'dotnet build StarPlayer.sln -c Release --disable-build-servers -nr:false' $b.Exit $status "warnings=$warnings"

# ---- 1b. Build configuration mapping and real output ----
function Get-SolutionMappingErrors($path) {
    $text = Get-Content $path -Raw
    $found = 0; $bad = @()
    foreach ($m in [regex]::Matches($text, '(\{[0-9A-Fa-f-]{36}\})\.(Debug|Release)\|Any CPU\.(ActiveCfg|Build\.0) = ([^\r\n]+)')) {
        $found++
        if ($m.Groups[4].Value.Trim() -ne "$($m.Groups[2].Value)|Any CPU") { $bad += "$($m.Groups[1].Value) $($m.Groups[2].Value)|$($m.Groups[3].Value) -> $($m.Groups[4].Value.Trim())" }
    }
    if ($found -eq 0) { $bad += 'no project configuration mappings found' }
    return $bad
}
$mapErrors = @(Get-SolutionMappingErrors (Join-Path $root 'StarPlayer.sln')) + @(Get-SolutionMappingErrors (Join-Path $root 'game\StarPlayer.Client.sln'))
Add-Result 'B2' 'Solutions map Release->Release and Debug->Debug for every project' 'parse StarPlayer.sln and game\StarPlayer.Client.sln' $(if ($mapErrors.Count) { 1 } else { 0 }) $(if ($mapErrors.Count) { 'FAIL' } else { 'PASS' }) ($mapErrors -join '; ')

function Get-ClientProps($configuration) {
    $json = (& dotnet msbuild game\StarPlayer.Client.csproj -nr:false "-p:UseSharedCompilation=false" "-p:Configuration=$configuration" -getProperty:Configuration -getProperty:OutputPath -getProperty:Optimize -getProperty:DefineConstants) -join "`n"
    return ($json | ConvertFrom-Json).Properties
}
$release = Get-ClientProps 'Release'; $debug = Get-ClientProps 'Debug'; $exportRelease = Get-ClientProps 'ExportRelease'
$releaseDll = Join-Path $release.OutputPath 'StarPlayer.Client.dll'
$debugDll = Join-Path $debug.OutputPath 'StarPlayer.Client.dll'
$problems = @()
if ($release.Configuration -ne 'Release' -or $release.Optimize -ne 'true' -or $release.DefineConstants -match '(^|;)DEBUG(;|$)') { $problems += 'Release properties are not an optimized non-DEBUG build' }
if (-not (Test-Path $releaseDll)) { $problems += "missing $releaseDll" } elseif ((Get-Item $releaseDll).LastWriteTimeUtc -lt $newestClientSource) { $problems += 'Release client output is older than the client sources (stale)' }
$debugStampNow = if (Test-Path $debugDll) { (Get-Item $debugDll).LastWriteTimeUtc.Ticks } else { 0 }
if ($debugStampNow -ne $debugStamp) { $problems += 'the Release build rewrote the Debug client output' }
if ($debug.Optimize -ne 'false' -or $debug.DefineConstants -notmatch '(^|;)DEBUG(;|$)') { $problems += 'editor Debug configuration was altered' }
if ($exportRelease.Optimize -ne 'true' -or $exportRelease.DefineConstants -notmatch 'EXPORTRELEASE') { $problems += 'ExportRelease configuration is not an optimized export build' }
$b3Detail = if ($problems.Count) { $problems -join '; ' } else { "Release -> $releaseDll (Optimize=true, not older than sources); Debug output untouched (Optimize=false); ExportRelease Optimize=true" }
Add-Result 'B3' 'Client builds in the requested configuration into its own output path' 'dotnet msbuild game\StarPlayer.Client.csproj -p:Configuration=<Release|Debug|ExportRelease> -getProperty:...' $(if ($problems.Count) { 1 } else { 0 }) $(if ($problems.Count) { 'FAIL' } else { 'PASS' }) $b3Detail

# ---- 2. Core tests, with Godot not running ----
$godotRunning = @(Get-Process -Name 'Godot*' -ErrorAction SilentlyContinue).Count
$tests = @(
    @('U1', 'Domain tests', 'tests\StarPlayer.Domain.Tests'),
    @('U2', 'Simulation tests', 'tests\StarPlayer.Simulation.Tests'),
    @('U3', 'Application tests (includes architecture dependency checks)', 'tests\StarPlayer.Application.Tests'),
    @('U3a', 'Architecture boundary tests (project refs, compiled client refs, presenter sources)', 'tests\StarPlayer.Application.Tests', 'FullyQualifiedName~ArchitectureTests'),
    @('U3b', 'Simulation determinism detector tests', 'tests\StarPlayer.Application.Tests', 'FullyQualifiedName~SimulationDeterminismTests'),
    @('U4', 'Infrastructure tests (real SQLite integration)', 'tests\StarPlayer.Infrastructure.Tests')
)
foreach ($t in $tests) {
    $filter = if ($t.Count -gt 3) { " --filter `"$($t[3])`"" } else { '' }
    $r = Invoke-Logged $t[0] 'dotnet' "test $($t[2]) -c Release --no-build$filter" $root 600
    if ($r.Text -match 'Failed:\s+(\d+), Passed:\s+(\d+), Skipped:\s+(\d+), Total:\s+(\d+)') {
        $failed = [int]$Matches[1]; $passed = [int]$Matches[2]; $skipped = [int]$Matches[3]
        $st = if ($r.Exit -eq 0 -and $failed -eq 0 -and $skipped -eq 0 -and $passed -gt 0) { 'PASS' } elseif ($failed -gt 0) { 'FAIL' } else { 'INCOMPLETE' }
        Add-Result $t[0] $t[1] "dotnet test $($t[2]) -c Release --no-build$filter" $r.Exit $st "passed=$passed failed=$failed skipped=$skipped godot_processes=$godotRunning"
    } else {
        Add-Result $t[0] $t[1] "dotnet test $($t[2]) -c Release --no-build$filter" $r.Exit 'FAIL' 'no test summary found in output'
    }
}

# ---- 3. Godot headless smoke ----
if ($godotReady) {
    $g1 = Invoke-Logged 'G1' $GodotConsolePath '--headless --path game --build-solutions --quit' $root 900
    Add-Result 'G1' 'Godot builds the C# solution headless' 'godot --headless --path game --build-solutions --quit' $g1.Exit $(if ($g1.Exit -eq 0 -and $g1.Text -notmatch 'error CS') { 'PASS' } else { 'FAIL' }) ''

    $out = Join-Path $work 'headless-diagnostics.txt'
    $g2 = Invoke-Logged 'G2' $GodotConsolePath "--headless --path game --quit-after 1200 -- --diagnostics --diagnostics-out=`"$out`"" $root 300
    $text = if (Test-Path $out) { Get-Content $out -Raw -Encoding UTF8 } else { '' }
    $ok = $g2.Exit -eq 0 -and $text -match 'STARPLAYER_DIAGNOSTICS=PASS'
    Add-Result 'G2' 'Godot headless launch runs SQLite diagnostics' 'godot --headless --path game -- --diagnostics' $g2.Exit $(if ($ok) { 'PASS' } else { 'FAIL' }) (($text -split "`n" | Select-Object -First 1).Trim())

    $pout = Join-Path $work 'headless-player-diagnostics.txt'
    $g3 = Invoke-Logged 'G3' $GodotConsolePath "--headless --path game --quit-after 1200 -- --player-diagnostics --diagnostics-out=`"$pout`"" $root 300
    $ptext = if (Test-Path $pout) { Get-Content $pout -Raw -Encoding UTF8 } else { '' }
    $pok = $g3.Exit -eq 0 -and $ptext -match 'STARPLAYER_PLAYER_DIAGNOSTICS=PASS' -and $ptext -match '자체 점검: 통과 \(7/7\)' -and $ptext -match 'hidden_identifier_leaks=0'
    Add-Result 'G3' 'Godot headless: Phase 1 player card self-check (7/7), no hidden-field leaks' 'godot --headless --path game -- --player-diagnostics' $g3.Exit $(if ($pok) { 'PASS' } else { 'FAIL' }) (($ptext -split "`n" | Where-Object { $_ -match 'hidden_identifier_leaks' } | Select-Object -First 1))
} else {
    Add-Result 'G3' 'Godot headless: Phase 1 player card self-check' 'godot --headless ... -- --player-diagnostics' 127 'INCOMPLETE' 'Godot missing'
    Add-Result 'G1' 'Godot builds the C# solution headless' 'godot --headless ... --build-solutions' 127 'INCOMPLETE' 'Godot missing'
    Add-Result 'G2' 'Godot headless launch runs SQLite diagnostics' 'godot --headless ... -- --diagnostics' 127 'INCOMPLETE' 'Godot missing'
}

# ---- 4. Windows export + cold run of the exported program ----
$exportDir = Join-Path $root 'build\export'
if ($godotReady -and $templatesReady) {
    $exportStart = (Get-Date).AddSeconds(-2)
    Remove-Item $exportDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $exportDir | Out-Null
    $e1 = Invoke-Logged 'E1' $GodotConsolePath '--headless --path game --export-release "Windows Desktop" ../build/export/StarPlayer.exe' $root 900
    $native = Get-ChildItem $exportDir -Recurse -Filter 'e_sqlite3.dll' -ErrorAction SilentlyContinue | Select-Object -First 1
    $ok = $e1.Exit -eq 0 -and (Test-Path (Join-Path $exportDir 'StarPlayer.exe')) -and (Test-Path (Join-Path $exportDir 'StarPlayer.console.exe')) -and $native -and $e1.Text -notmatch 'ERROR:'
    Add-Result 'E1' 'Windows Desktop release export' 'godot --headless --path game --export-release "Windows Desktop" ../build/export/StarPlayer.exe' $e1.Exit $(if ($ok) { 'PASS' } else { 'FAIL' }) $(if ($native) { "bundled native: $($native.Name)" } else { 'e_sqlite3.dll missing from export' })

    if ($ok) {
        # The export must come from the ExportRelease configuration and be produced by this run, not a stale folder.
        $publishedDll = Join-Path $root 'game\.godot\mono\temp\bin\ExportRelease\win-x64\StarPlayer.Client.dll'
        $exportedDll = Join-Path $exportDir 'data_StarPlayer.Client_windows_x86_64\StarPlayer.Client.dll'
        $pck = Get-Item (Join-Path $exportDir 'StarPlayer.pck')
        $freshProblems = @()
        if ($pck.LastWriteTime -lt $exportStart) { $freshProblems += 'StarPlayer.pck is older than this run' }
        if (-not (Test-Path $publishedDll)) { $freshProblems += 'no ExportRelease publish output' }
        elseif ((Get-Item $publishedDll).LastWriteTimeUtc -lt $newestClientSource) { $freshProblems += 'ExportRelease publish output is older than the client sources (stale)' }
        if (-not (Test-Path $exportedDll)) { $freshProblems += 'exported StarPlayer.Client.dll missing' }
        else {
            $exportedHash = (Get-FileHash $exportedDll).Hash
            if ((Test-Path $publishedDll) -and (Get-FileHash $publishedDll).Hash -ne $exportedHash) { $freshProblems += 'exported client differs from the ExportRelease publish output' }
            if ((Test-Path $debugDll) -and (Get-FileHash $debugDll).Hash -eq $exportedHash) { $freshProblems += 'exported client is byte-identical to the Debug build' }
        }
        $e1bDetail = if ($freshProblems.Count) { $freshProblems -join '; ' } else { "export folder recreated this run (pck $($pck.LastWriteTime.ToString('HH:mm:ss'))); client DLL SHA-256 identical to ExportRelease publish output and different from Debug" }
        Add-Result 'E1b' 'Export is fresh and built from the ExportRelease configuration' 'compare game\.godot\mono\temp\bin\ExportRelease\win-x64 with build\export data folder' $(if ($freshProblems.Count) { 1 } else { 0 }) $(if ($freshProblems.Count) { 'FAIL' } else { 'PASS' }) $e1bDetail

        # Cold start: copy to a fresh folder so nothing from the repo, editor cache or .godot is reachable.
        $cold = Join-Path $env:TEMP ('starplayer-cold-' + [guid]::NewGuid().ToString('N'))
        Copy-Item $exportDir $cold -Recurse
        $coldOut = Join-Path $cold 'diagnostics.txt'
        $coldPng = Join-Path $cold 'shell.png'
        $x = Invoke-Logged 'E2' (Join-Path $cold 'StarPlayer.console.exe') "--resolution 2560x1440 -- --diagnostics --diagnostics-out=`"$coldOut`" --screenshot=`"$coldPng`"" $cold 300
        $text = if (Test-Path $coldOut) { Get-Content $coldOut -Raw -Encoding UTF8 } else { '' }
        $shot = Test-Path $coldPng
        if ($shot) { Copy-Item $coldPng (Join-Path $work 'exported-shell.png') }
        if (Test-Path $coldOut) { Copy-Item $coldOut (Join-Path $work 'exported-diagnostics.txt') }
        $pass = $x.Exit -eq 0 -and $text -match 'STARPLAYER_DIAGNOSTICS=PASS' -and $text -match '8/8' -and $shot
        Add-Result 'E2' 'Exported program: shell opens, SQLite commit/rollback/FK/backup/reopen pass (cold copy)' 'StarPlayer.console.exe --resolution 2560x1440 -- --diagnostics --screenshot=...' $x.Exit $(if ($pass) { 'PASS' } else { 'FAIL' }) "screenshot=$shot; $(($text -split "`n" | Select-Object -First 1).Trim())"
        $pcOut = Join-Path $cold 'player-diagnostics.txt'
        $pcPng = Join-Path $cold 'player-card.png'
        $x3 = Invoke-Logged 'E3' (Join-Path $cold 'StarPlayer.console.exe') "--resolution 2560x1440 -- --player-diagnostics --diagnostics-out=`"$pcOut`" --screenshot=`"$pcPng`"" $cold 300
        $ptext = if (Test-Path $pcOut) { Get-Content $pcOut -Raw -Encoding UTF8 } else { '' }
        $shots = (Test-Path $pcPng) -and (Test-Path (Join-Path $cold 'player-card-npc.png'))
        if ($shots) { Copy-Item $pcPng (Join-Path $work 'exported-player-card.png'); Copy-Item (Join-Path $cold 'player-card-npc.png') (Join-Path $work 'exported-player-card-npc.png') }
        if (Test-Path $pcOut) { Copy-Item $pcOut (Join-Path $work 'exported-player-diagnostics.txt') }
        $p3 = $x3.Exit -eq 0 -and $ptext -match 'STARPLAYER_PLAYER_DIAGNOSTICS=PASS' -and $ptext -match '자체 점검: 통과 \(7/7\)' -and $ptext -match 'hidden_identifier_leaks=0' -and $shots
        Add-Result 'E3' 'Exported program: Phase 1 player card at QHD (cold copy, embedded fictional pack, screenshots)' 'StarPlayer.console.exe --resolution 2560x1440 -- --player-diagnostics --screenshot=...' $x3.Exit $(if ($p3) { 'PASS' } else { 'FAIL' }) "screenshots=$shots"
        Remove-Item $cold -Recurse -Force -ErrorAction SilentlyContinue
    } else {
        Add-Result 'E2' 'Exported program SQLite diagnostics' 'StarPlayer.console.exe -- --diagnostics' 1 'FAIL' 'export did not produce a runnable build'
        Add-Result 'E3' 'Exported program: Phase 1 player card' 'StarPlayer.console.exe -- --player-diagnostics' 1 'FAIL' 'export did not produce a runnable build'
    }
} else {
    $why = if (-not $godotReady) { 'Godot missing' } else { 'export templates missing' }
    Add-Result 'E1' 'Windows Desktop release export' 'godot --export-release ...' 127 'INCOMPLETE' $why
    Add-Result 'E2' 'Exported program SQLite diagnostics' 'StarPlayer.console.exe -- --diagnostics' 127 'INCOMPLETE' $why
    Add-Result 'E3' 'Exported program: Phase 1 player card' 'StarPlayer.console.exe -- --player-diagnostics' 127 'INCOMPLETE' $why
}

# ---- Leftover build servers started during this run (informational; never killed here) ----
$leftover = @(Get-Process -Name 'dotnet', 'MSBuild', 'VBCSCompiler' -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -ge $scriptStart })
$leftoverText = if ($leftover.Count) { ($leftover | ForEach-Object { "$($_.ProcessName)#$($_.Id)" }) -join ', ' } else { 'none' }
Add-Result 'Z1' 'Build-server processes started during this run and still alive (informational, not part of the gate)' 'Get-Process dotnet,MSBuild,VBCSCompiler where StartTime >= script start' 0 'INFO' $leftoverText

# ---- Gate and record ----
$gate = if ($results | Where-Object Status -eq 'FAIL') { 'FAIL' } elseif ($results | Where-Object Status -eq 'INCOMPLETE') { 'INCOMPLETE' } else { 'PASS' }
$commit = (& git rev-parse --short HEAD 2>$null)
$dirty = if (& git status --porcelain 2>$null) { 'dirty working tree' } else { 'clean' }
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Phase 1 verification log (includes the Phase 0 regression checks)')
$lines.Add('')
$lines.Add('Generated by `tools/verify.ps1`; overwritten on each run. Raw logs: `build/verify/` (git-ignored).')
$lines.Add('')
$lines.Add("- Run at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')")
$lines.Add("- OS: $([System.Environment]::OSVersion.VersionString) ($((Get-CimInstance Win32_OperatingSystem).Caption))")
$lines.Add("- .NET SDK: $sdkVersion (pinned $pinned); Godot: $godotVersion")
$lines.Add("- Git: $commit ($dirty)")
$lines.Add('- Build servers: MSBUILDDISABLENODEREUSE=1, DOTNET_CLI_USE_MSBUILD_SERVER=0, UseSharedCompilation=false for this script only; `--disable-build-servers -nr:false` on the solution build; per-command time limits (build/export 900 s, tests 600 s, Godot runs 300 s) with kill of only the timed-out process tree')
$lines.Add("- **Gate result: $gate**")
$lines.Add('')
$lines.Add('| ID | Check | Command | Exit | Status | Detail |')
$lines.Add('|---|---|---|---:|---|---|')
foreach ($r in $results) {
    $cmd = ($r.Command -replace '\|', '\|')
    $detail = ([string]$r.Detail -replace '\|', '\|')
    $lines.Add("| $($r.Id) | $($r.Name) | ``$cmd`` | $($r.ExitCode) | $($r.Status) | $detail |")
}
$reportDir = Join-Path $root 'docs\reports'
New-Item -ItemType Directory -Force $reportDir | Out-Null
[System.IO.File]::WriteAllLines((Join-Path $reportDir $LogName), $lines, (New-Object System.Text.UTF8Encoding($false)))
$results | ConvertTo-Json | Set-Content (Join-Path $work 'verify-result.json') -Encoding UTF8
Write-Host "GATE: $gate"
switch ($gate) { 'PASS' { exit 0 } 'FAIL' { exit 1 } default { exit 2 } }
