# Restores the Godot 4.7.2 Mono Windows export templates from the official .tpz.
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\install-export-templates.ps1
# Verifies the .tpz against the official SHA512-SUMS.txt, extracts the Windows x86_64 templates to the
# folder Godot reads, then checks every extracted file byte-for-byte against the archive entry.
param(
    [string]$Tpz = 'E:\Madegame\tools\godot\dl\Godot_v4.7.2-stable_mono_export_templates.tpz',
    [string]$Sums = 'E:\Madegame\tools\godot\dl\SHA512-SUMS.txt',
    [string]$Destination = (Join-Path $env:APPDATA 'Godot\export_templates\4.7.2.stable.mono')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$expected = ((Get-Content $Sums | Where-Object { $_ -match 'Godot_v4\.7\.2-stable_mono_export_templates\.tpz' }) -split '\s+')[0].ToLower()
$actual = (Get-FileHash $Tpz -Algorithm SHA512).Hash.ToLower()
if (-not $expected -or $actual -ne $expected) { throw "tpz SHA-512 mismatch (expected '$expected', actual '$actual')" }
Write-Host "tpz SHA-512 OK: $actual"

New-Item -ItemType Directory -Force $Destination | Out-Null
$zip = [IO.Compression.ZipFile]::OpenRead($Tpz)
try {
    $wanted = $zip.Entries | Where-Object { $_.Name -eq 'version.txt' -or $_.Name -match '^windows_(debug|release)_x86_64(_console)?\.exe$' }
    foreach ($entry in $wanted) {
        $target = Join-Path $Destination $entry.Name
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        $sha = [Security.Cryptography.SHA256]::Create()
        $stream = $entry.Open(); $fromZip = [BitConverter]::ToString($sha.ComputeHash($stream)); $stream.Dispose()
        $fromDisk = [BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($target)))
        if ($fromZip -ne $fromDisk) { throw "extracted file differs from archive entry: $($entry.Name)" }
        Write-Host ("{0}  {1}" -f $fromDisk.Replace('-', '').ToLower(), $target)
    }
} finally { $zip.Dispose() }
