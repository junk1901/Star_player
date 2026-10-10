# Toolchain (pinned in Phase 0)

Recorded 2026-10-10 from the development PC (Windows 11 Home 10.0.26200, x64). Facts below are what was installed and executed, not intentions.

## Pinned versions

| Item | Version | Where pinned / how obtained |
|---|---|---|
| .NET SDK | 8.0.425 (LTS), `rollForward: disable` | `global.json`; installed with `winget install Microsoft.DotNet.SDK.8` (installer hash verified by winget) |
| Target framework | `net8.0`, C# 12, nullable on, warnings-as-errors | `Directory.Build.props` |
| Godot | 4.7.2-stable **Mono (.NET)** win64, build `4.7.2.stable.mono.official.ed1daf0bf` | GitHub release `4.7.2-stable`, zip SHA-512 matched `SHA512-SUMS.txt`; unzipped to `E:\Madegame\tools\godot\` (outside the repo) |
| Godot export templates | 4.7.2 Mono, Windows x86_64 debug/release extracted from `Godot_v4.7.2-stable_mono_export_templates.tpz` (SHA-512 matched) | `%APPDATA%\Godot\export_templates\4.7.2.stable.mono\` |
| Godot C# SDK | `Godot.NET.Sdk` 4.7.2 (supplies `GodotSharp` 4.7.2) | `global.json` `msbuild-sdks` and the `Sdk=` attribute of `game/StarPlayer.Client.csproj` |
| SQLite provider | `Microsoft.Data.Sqlite` 8.0.31 (bundles native `e_sqlite3`, SQLite 3.53.3 observed at runtime) | `Directory.Packages.props` |
| Test stack | xUnit 2.9.3, xunit.runner.visualstudio 2.8.2, Microsoft.NET.Test.Sdk 17.14.1 | `Directory.Packages.props` |

Godot 4.5+ documents ".NET 8 or later" as its C# requirement; .NET 8 was chosen as the LTS that the 4.7.2 SDK package targets (`net8.0`). Preview SDKs and "latest" floating versions are not used.

## Repository layout actually created

```
StarPlayer.sln                  # all 9 projects; used by build/test
global.json  Directory.Build.props  Directory.Packages.props
src/StarPlayer.{Domain,Simulation,Application,Infrastructure}
tests/StarPlayer.{Domain,Simulation,Application,Infrastructure}.Tests
game/                           # Godot project root
  project.godot  export_presets.cfg
  StarPlayer.Client.csproj      # Godot.NET.Sdk project
  StarPlayer.Client.sln         # client-only solution required by Godot (see ADR-001)
  Bootstrap/  UI/MainMenu/
tools/verify.ps1
tools/install-export-templates.ps1   # restores the Godot export templates from the official .tpz
```

Project references (enforced by `ArchitectureTests`): Domain ← Simulation ← Application ← Infrastructure; the Godot client references Application and Infrastructure only (no Domain/Simulation; Infrastructure used only under `game/Bootstrap`), and no test project compiles against the client (`Application.Tests` only builds it, `ReferenceOutputAssembly=false`).

## Reproduce

Prerequisites: the SDK and Godot files above. If Godot lives elsewhere, set `STARPLAYER_GODOT` to the path of `Godot_v4.7.2-stable_mono_win64_console.exe` (or pass `-GodotConsolePath`).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1
```

It runs, in order: toolchain checks (including SHA-256 of the five installed export-template files), solution-configuration and client output-path checks, `dotnet build StarPlayer.sln -c Release`, the four test projects, Godot headless build + diagnostics launch, Windows export, and a cold-copy run of the exported program (diagnostic launch mode, QHD, screenshot). Exit code 0 = PASS, 1 = FAIL, 2 = INCOMPLETE (a tool is missing; never reported as PASS). Results are written to `docs/reports/PHASE_0_VERIFY_LOG.md`; raw logs and the exported build go to `build/` (git-ignored).

Individual commands:

```powershell
dotnet build StarPlayer.sln -c Release
dotnet test StarPlayer.sln -c Release
godot_console --headless --path game --build-solutions --quit
godot_console --headless --path game --quit-after 1200 -- --diagnostics --diagnostics-out=out.txt
godot_console --headless --path game --export-release "Windows Desktop" ../build/export/StarPlayer.exe
build\export\StarPlayer.console.exe -- --diagnostics --diagnostics-out=out.txt --screenshot=shell.png
```

Restoring missing export templates (verifies the official archive's SHA-512 first):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\install-export-templates.ps1
```

Build configurations: `dotnet build -c Release` builds the client as a normal optimized Release into `game/.godot/mono/temp/bin/Release/`; the Godot editor uses `Debug`; Godot's export publishes `ExportRelease` (`bin/ExportRelease/win-x64`). `verify.ps1` checks all three.

## Known limitations and exceptions

- **No executable resource editing.** `application/modify_resources=false`: setting a custom icon/version resource needs `rcedit`, which was not downloaded in Phase 0. The exported exe carries Godot's default resources.
- **Export layout is not single-file.** `StarPlayer.exe` + `StarPlayer.pck` + `data_StarPlayer.Client_windows_x86_64/` (managed assemblies, .NET runtime, native `e_sqlite3.dll`) must stay together. `StarPlayer.console.exe` is Godot's console wrapper, exported only so diagnostics can be captured from a shell.
- **Godot editor rewrites `game/StarPlayer.Client.csproj`** on first open/build (it added the `Godot.NET.Sdk/4.7.2` version and `TargetFramework`); the committed file is the rewritten form.
- **Code signing, installers, FHD layout and a bundled Korean font** are out of scope for Phase 0. Korean text currently relies on Godot's system-font fallback (rendered correctly on this PC).
- Warnings-as-errors applies to every project, including the Godot client; no exemption was needed.
