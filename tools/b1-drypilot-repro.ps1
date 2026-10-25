# B1-T04a / T11 (repo copy of the reproduction used since the OWNER decision 3 round): re-run the 9 Dry Pilot seed files with the CURRENT Release SimHarness and compare config_hash / run_result_hash
# (and the whole manifest except run_id and environment) with the archived run-a manifests.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$arch = 'build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_dry_run1'
$dll = 'tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.dll'
$outRoot = 'build/b1-t04a-repro'
Remove-Item $outRoot -Recurse -Force -ErrorAction SilentlyContinue
$pilot = [IO.File]::ReadAllText((Join-Path $root "$arch/pilot.json"))
$keys = 'fc2-dry-cal-na','fc2-dry-cal-nn','fc2-dry-cal-va','fc2-dry-cal-vn','fc2-dry-dom-sa-nb','fc2-dry-dom-sa-ak','fc2-dry-dom-sa-pc','fc2-dry-dom-sa-tb','fc2-dry-stop-na'
$rows = @()
foreach ($k in $keys) {
    $seed = "$arch/seeds/$k.json"
    $sha = (Get-FileHash $seed -Algorithm SHA256).Hash.ToLower()
    $out = "$outRoot/$k"
    $p = Start-Process dotnet -ArgumentList "$dll dry --seed-manifest $seed --seed-sha256 $sha --run-id $k-a --out $out" -NoNewWindow -Wait -PassThru -RedirectStandardOutput "$env:TEMP\b1t04a_$k.out" -RedirectStandardError "$env:TEMP\b1t04a_$k.err"
    $new = Get-Content "$out/manifest.json" -Raw | ConvertFrom-Json
    $old = Get-Content "$arch/runs/$k-a/manifest.json" -Raw | ConvertFrom-Json
    # whole manifest except environment (machine/time): run ids are equal because the same -a run id is used
    $strip = { param($m) $m.PSObject.Properties.Remove('environment_not_hashed'); ($m | ConvertTo-Json -Depth 50 -Compress) }
    $sameManifest = (& $strip $new) -eq (& $strip $old)
    $rows += [pscustomobject]@{ Run = $k; Exit = $p.ExitCode; ConfigHash = $new.run.config_hash; OldConfigHash = $old.run.config_hash; RunResultHash = $new.results.run_result_hash; OldRunResultHash = $old.results.run_result_hash; SameManifestExceptEnvironment = $sameManifest }
}
$rows | Format-Table -AutoSize | Out-String -Width 400
$allSame = -not ($rows | Where-Object { $_.ConfigHash -ne $_.OldConfigHash -or $_.RunResultHash -ne $_.OldRunResultHash -or -not $_.SameManifestExceptEnvironment -or $_.Exit -ne 0 })
"ALL_9_IDENTICAL=$allSame"
"dll_sha256=" + (Get-FileHash $dll -Algorithm SHA256).Hash.ToLower()
