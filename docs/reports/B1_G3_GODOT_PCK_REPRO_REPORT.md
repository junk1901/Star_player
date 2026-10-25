# B1 G3 Godot PCK 재현성 조사 결과와 .NET 빌드 정책 후보

- 작성: Claude Code, 2026-10-26. 근거: OWNER 지시 "G3 전체 진단 결과 수용 및 Godot PCK 재현성 조사"(2026-10-26), [TASK](../tasks/B1_G3_GODOT_PCK_REPRO_TASK.md)
- **성격:**
  - 진단입니다.
  - 운영 G3 승인, Build Freeze, Artifact Schema 변경, 정식 Scene 수정, Godot Export 동결이 **아닙니다**.
  - B1 기준본(Tag `b1-code-accepted-2026-10-26` → `7bdde7dd…`)의 코드·Scene은 수정하지 않았습니다. Scene 수정은 진단 clone에서만 했습니다.
- **이전 이력(수정하지 않음):** [전체 빌드 진단](B1_G3_FULL_BUILD_DIAGNOSTIC_REPORT.md), [SimHarness 실험](B1_G3_REPRO_EXPERIMENT_REPORT.md), [조사](B1_G3_BUILD_REPRODUCIBILITY_INVESTIGATION.md)
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 결론 요약

| 항목 | 결과 |
|---|---|
| 원인 | **확인.** `unique_id`가 없는 Scene 4개를 Export하면, Export마다 변환 `.scn`의 `node_ids`가 새로 생성됩니다 |
| 재저장 후 | 모든 노드에 `unique_id`를 저장한 Scene으로 Export하면 같은 경로 반복과 다른 경로에서 `StarPlayer.pck`가 **바이트 동일**합니다(`3a187e19…`, 4회) |
| Scene별 확인 | Scene 하나만 재저장하면 그 Scene은 안정되고 나머지 3개는 계속 변합니다. 원인은 Scene마다 독립입니다 |
| 공식 이슈 #115971 | 증상·조건·해결 방법이 이슈 설명과 일치합니다. 다만 편집기 UI 저장이나 "Upgrade Project Files"를 쓰지 않고 스크립트로 재저장했습니다(4절) |
| Godot 실행 | 재저장 Scene Export와 원본 Scene Export 모두 G2~G4(headless)와 E2~E4(Export cold copy) PASS 표시. 스크린샷 3개가 바이트 동일 |
| 정식 Scene 수정 | 하지 않았습니다. **별도 OWNER 승인 필요**(11절) |

## 2. Godot 실제 버전과 Export 조건

| 항목 | 값 |
|---|---|
| Godot 편집기 | `4.7.2.stable.mono.official.ed1daf0bf`(`E:\Madegame\tools\godot\Godot_v4.7.2-stable_mono_win64\…_console.exe --version`) |
| Godot .NET | `Godot.NET.Sdk/4.7.2`(`game/StarPlayer.Client.csproj`, `global.json` `msbuild-sdks`) |
| Export된 `GodotSharp.dll` | FileVersion `4.7.2.0`, ProductVersion `4.7.2+ed1daf0bf001b61586d9930840f2f1394092c079`, SHA `2afe2686…` |
| Export Template | `%APPDATA%\Godot\export_templates\4.7.2.stable.mono`, `version.txt` = `4.7.2.stable.mono` |
| Template SHA-256 | `version.txt` `66796c18…73e2`, `windows_debug_x86_64.exe` `4526aac3…fdea`, `windows_debug_x86_64_console.exe` `f78703ba…0c52`, `windows_release_x86_64.exe` `16e0ec3c…d55d`, `windows_release_x86_64_console.exe` `7895a787…7017`(verify T3 값과 같음) |
| 플랫폼·preset | Windows Desktop x86_64, preset `Windows Desktop`(`embed_pck=false`, `modify_resources=false`, `export_filter="all_resources"`), `export_presets.cfg` `0f51945d…` |
| 명령 | `godot --headless --path game --build-solutions --quit`, 그다음 `godot --headless --path game --export-release "Windows Desktop" ../build/export/StarPlayer.exe` |
| 환경 | `ContinuousIntegrationBuild=true`(환경 변수), 이전 진단과 같은 MSBuild 변수 |
| Export 직전 상태 | 매번 `bin/`·`obj/`·`game/.godot/`·`build/export/` 삭제(캐시 없음). Scene SHA와 `git status`는 각 실행의 `meta.json`에 기록 |
| PCK | 형식 4, Godot 4.7.2, 내부 파일 18개, 13,452 B(모든 실행에서 크기 같음) |

## 3. 대상 Scene과 원본 상태

| Scene(원본 경로) | 노드 | `unique_id` | 헤더 | Git blob(LF) SHA-256 | 깨끗한 체크아웃 작업 트리(CRLF) SHA-256 |
|---|---:|---:|---|---|---|
| `game/Bootstrap/Main.tscn` → `export-baf82205…-Main.scn` | 1 | 0 | `[gd_scene load_steps=2 format=3]` | `88ce9018…`(173 B) | `00770c63…`(179 B) |
| `game/UI/MainMenu/MainMenu.tscn` → `export-9719393e…-MainMenu.scn` | 14 | 0 | `load_steps=2 format=3` | `9d3d933c…`(3,014 B) | `99170af7…`(3,112 B) |
| `game/UI/PlayerDiagnostics/PlayerDiagnosticsScreen.tscn` → `export-3fb95cf1…-PlayerDiagnosticsScreen.scn` | 13 | 0 | `load_steps=2 format=3` | `a2bcb178…`(2,632 B) | `65d14c60…`(2,724 B) |
| `game/UI/GameDiagnostics/GameDiagnosticsScreen.tscn` → `export-accaf5d0…-GameDiagnosticsScreen.scn` | 23 | 0 | `load_steps=3 format=3` | `53c8ed08…`(4,667 B) | `be5b82a7…`(4,823 B) |

- **이전 형식:**
  - 4개 모두 `load_steps`가 있고 헤더에 `uid=`가 없으며, 노드에 `unique_id`가 없습니다.
  - TSCN 문서에 따르면 `unique_id`는 Godot 4.6 이후 저장본에만 있고, `load_steps`는 4.6 이전 저장본의 속성입니다. 즉 **4.6 이전 형식**입니다.
  - 처음 커밋은 2026-10-10~11입니다. 어떤 도구로 썼는지(편집기 저장인지 직접 작성인지)는 Git 기록으로 확인되지 않습니다.
- **개행:**
  - `.tscn`에는 `.gitattributes` 규칙이 없습니다(`text: unspecified`).
  - system `core.autocrlf=true`에서 깨끗한 체크아웃은 CRLF, Git blob은 LF입니다. 원본 작업 트리 `E:\Madegame\Star_player`는 3개가 LF, `PlayerDiagnosticsScreen.tscn`은 CRLF입니다.
  - 그래서 Godot이 읽는 Scene 바이트가 체크아웃 설정에 따라 다릅니다. 이번 실험에서는 CRLF 원본(A)과 LF 재저장본(B)의 변환 `.scn` 차이가 `node_ids` 안에만 있었습니다. 개행이 변환 결과에 영향을 준 증거는 없습니다(11절).

## 4. 공식 이슈와의 관련성

**참고 자료:**

- [Godot 이슈 #115971](https://github.com/godotengine/godot/issues/115971) "Scenes created in 4.5.1-stable do not export deterministically in 4.6-stable unless you re-save them"
  - 열림, milestone 4.8. 4.6-stable·4.7.dev 재현 보고
  - 요지: `unique_id` 도입(PR #106837) 뒤, 저장된 ID가 없는 이전 Scene은 불러올 때 ID가 생성되고 Export마다 달라진다는 보고입니다.
  - 해결 방법: 편집기 재저장, "Upgrade Project Files"
- [TSCN 파일 형식(4.6)](https://docs.godotengine.org/en/4.6/engine_details/file_formats/tscn.html)
  - `unique_id`는 4.6 이후 저장본에만 있습니다.
  - `load_steps`는 4.6 이전 저장본의 사용 중지 속성입니다.

**이 저장소에서 실험으로 확인한 것(이슈 내용을 가정하지 않음):**

| 이슈가 말하는 조건·결과 | 이번 확인 |
|---|---|
| 저장된 `unique_id`가 없는 이전 형식 Scene | 4개 모두 해당(3절) |
| Export마다 변환 `.scn`의 ID가 달라짐 | A1↔A2: 4개 Scene 모두 다르고, 다른 바이트가 **모두 `node_ids` 배열 안**(6절) |
| 재저장하면 안정됨 | B·C 4회 동일. D에서 재저장한 Scene만 안정 |
| 편집기 재저장 또는 Upgrade Project Files | **이 방법은 쓰지 않았습니다.** 5절의 스크립트 재저장(R1)을 썼습니다. 편집기 UI 동작과 같은지는 확인하지 않았습니다 |

- Godot 엔진 내부의 ID 생성 위치(불러올 때인지, 변환할 때인지)는 소스 수준에서 확인하지 않았습니다. 관찰된 동작만 확인했습니다.

## 5. Scene 재저장 (진단 clone `E:\Madegame\g3x\pb`에서만)

| 방법 | 명령 | 결과 |
|---|---|---|
| R0 | `resave.gd`: `ResourceLoader.load` → `ResourceSaver.save` | **`unique_id` 추가 0개.** 헤더 `load_steps` 제거와 개행 LF 변환만 일어남(git diff 정규화 기준 4행). 재현성 해결에 쓸 수 없어 실험에서 제외 |
| R1 | `resave_pack.gd`: 불러온 Scene을 `instantiate(GEN_EDIT_STATE_MAIN)` → 새 `PackedScene.pack(root)` → `ResourceSaver.save`(편집기 저장이 편집 중인 Scene을 pack하는 방식을 흉내 냄) | **모든 노드에 `unique_id` 추가**(1·14·13·23개). 실험 B~D에 사용 |

- 실행: `godot --headless --path game --script <절대 경로>.gd -- <res://Scene …>`, Godot 4.7.2, C# 빌드(`--build-solutions`) 후
- **실행 방식 주의:** 처음 R0을 실행할 때 `--build-solutions`를 이 도구 셸에서 직접 실행하자 편집기 레이아웃 로딩 뒤 멈췄습니다. 그래서 그 Godot 프로세스(진단용, 내가 시작한 것)를 종료했습니다. `powershell -File` 자식 프로세스로 실행하면 정상 종료했으며, 이후 모든 실행은 그 방식입니다.

**R1 재저장 전후(git diff 정규화 기준 55행 = 헤더 4 + 노드 51):**

| Scene | 원본 blob SHA | 재저장 SHA-256(LF) | 바이트 | `unique_id` 추가 | 다른 변경 |
|---|---|---|---:|---:|---|
| Main | `88ce9018…` | `3501aba8fd59b5450855bca47bc2de66f7b6f1b251df3ac84d04d64f9787edf4` | 180 | 1 | 헤더 `load_steps=2` 제거 |
| MainMenu | `9d3d933c…` | `79dbc1dd026645d12fb05963e90130aa340403b3247f839f51613b727395192f` | 3,290 | 14 | 헤더 `load_steps=2` 제거 |
| PlayerDiagnosticsScreen | `a2bcb178…` | `defd9603508305d24ae864113b786c698927489e0756497870df043262f9f85c` | 2,885 | 13 | 헤더 `load_steps=2` 제거 |
| GameDiagnosticsScreen | `53c8ed08…` | `98886edae773b66fba879ca11a2c7156d7b3983a4523d70d9991418f23634490` | 5,122 | 23 | 헤더 `load_steps=3` 제거 |

- **바뀐 것:**
  - 헤더에서 `load_steps`가 빠졌습니다.
  - 각 `[node …]` 행 끝에 `unique_id=<정수>`가 붙었습니다.
  - 파일 개행이 LF가 됐습니다(원본 작업 트리 CRLF와 바이트가 다름).
- **바뀌지 않은 것:** 그 밖의 속성, `ext_resource`, 리소스 참조, 노드 이름·부모·형식, 행 순서
- 전체 diff는 `E:\Madegame\g3x\pck-out\R1_pack\git_diff.txt`(`3e741e04…`)에 있습니다. R0은 `…\R0_resourcesaver\git_diff.txt`(`d30d114c…`)입니다.
- **진단본 표시:** 재저장 Scene은 **진단 clone의 수정본이며 승인 소스와 같지 않습니다.**

## 6. PCK 반복 Export 결과

| ID | clone | Scene | `StarPlayer.pck` SHA-256 | 바이트 |
|---|---|---|---|---:|
| A1 | `pa` | 원본(CRLF 작업 트리) | `d7f7b6593931e7ea24db905ac283d0b45424032cf428a2a710349981d98501f5` | 13,452 |
| A2 | `pa` | 원본 | `18a523455873f0db1bbb9b37a3032945166bdc267e2de0a883abac631b0151ac` | 13,452 |
| B1 | `pb` | R1 재저장 4개 | `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799` | 13,452 |
| B2 | `pb` | R1 재저장 4개 | `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799` | 13,452 |
| C1 | `pcxlong`(경로 길이 다름) | B와 같은 바이트 복사 | `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799` | 13,452 |
| C2 | `pcxlong` | 같음 | `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799` | 13,452 |
| D_Main_1/2 | `pd` | Main만 재저장 | `46069201…` / `25f3106f…` | 13,452 |
| D_MainMenu_1/2 | `pd` | MainMenu만 | `016eea98…` / `291a71e6…` | 13,452 |
| D_PlayerDiagnosticsScreen_1/2 | `pd` | PlayerDiagnosticsScreen만 | `b16ffa27…` / `ed08d550…` | 13,452 |
| D_GameDiagnosticsScreen_1/2 | `pd` | GameDiagnosticsScreen만 | `ea6502cb…` / `14b0194d…` | 13,452 |

**PCK 내부 비교(18개 파일, 형식 4 디렉터리 해석):**

| 비교 | 다른 내부 파일 | 내용 |
|---|---|---|
| A1 ↔ A2 | 변환 `.scn` 4개 | Main 4 B, MainMenu 56 B, PlayerDiagnosticsScreen 52 B, GameDiagnosticsScreen 92 B. **모두 `node_ids` 배열 안**(배열 길이 = 노드 수 1·14·13·23) |
| B1 ↔ B2, B1 ↔ C1, C1 ↔ C2 | 없음 | 전체 바이트 동일 |
| A1 ↔ B1 | 변환 `.scn` 4개 | 모두 `node_ids` 배열 안. B의 `node_ids`는 재저장 `.tscn`의 `unique_id` 값과 같음(Main `480977380`, PlayerDiagnosticsScreen `1845178902, 591600197, 870879902…`) |
| D_x_1 ↔ D_x_2 | 재저장하지 않은 Scene 3개 | 각 경우 재저장한 Scene은 같고, 나머지 3개만 `node_ids`가 다름 |

- **그 밖의 Export 파일 197개**(`StarPlayer.exe`·`StarPlayer.console.exe`·.NET 어셈블리·런타임 등)는 A1·A2·B1·B2·C1·C2 모두 같습니다.
- 다른 내부 파일 14개(`project.binary`, `uid_cache.bin`, `.remap` 4개 등)도 모든 실행에서 같습니다.
- `node_ids`를 비교에서 빼거나 사후 패치하지 않았습니다. 판정은 전체 바이트 비교입니다.

## 7. Godot 실행·Scene 로딩 검증

A1(원본 Scene)과 B1(재저장 Scene)에서 verify와 같은 실행 인자로 확인했습니다.

| 검사 | A1 | B1 |
|---|---|---|
| G2 headless `--diagnostics` | exit 0, `STARPLAYER_DIAGNOSTICS=PASS` | 같음 |
| G3 headless `--player-diagnostics` | exit 0, PASS, `자체 점검: 통과 (7/7)`, `hidden_identifier_leaks=0` | 같음 |
| G4 headless `--game-diagnostics` | exit 0, PASS, 7/7, `games=6 finished_with_box_score=6 self_check=PASS` | 같음 |
| E2 Export cold copy `--diagnostics`(2560×1440) | exit 0, PASS, `진단 결과: 통과 (8/8 단계)`, 스크린샷 있음 | 같음 |
| E3 Export cold copy `--player-diagnostics` | exit 0, PASS, 스크린샷 있음 | 같음 |
| E4 Export cold copy `--game-diagnostics` | exit 0, PASS, 첫 행 `자체 점검: 통과 (7/7)`, 스크린샷 있음 | 같음 |
| 스크린샷 SHA(A1 = B1) | `E2_shell.png` `5388D19D…`, `E3_player-card.png` `4EDE6A12…`, `E4_game-screen.png` `4455E6EC…` | 같음 |

**한계:**

- 실행 스크립트의 한국어 "7/7" 정규식이 PowerShell 5.1에서 BOM 없는 파일을 잘못 읽어 실패했습니다. 그래서 G3·G4의 7/7·누수 판정은 저장된 결과 파일을 유니코드 이스케이프 패턴으로 다시 확인했습니다.
- E3의 7/7·`hidden_identifier_leaks=0`과 E3·E4의 두 번째 스크린샷(`player-card-npc.png`, `game-screen-end.png`)은 이번 스크립트가 보존하지 않았습니다. 그래서 **verify E3·E4 기준 전체를 확인한 것은 아닙니다**.

## 8. B-4 .NET 빌드 후보 절차 (OWNER 정책 방향 기록, 확정 아님)

1. 승인된 Git Commit의 Clean Checkout. 추적·미추적 변경 0, `git rev-parse HEAD` = 승인 Commit
2. 같은 SourceLink 원격 정책: `origin` = 승인된 공식 URL(9절, 현재 미정)
3. .NET SDK 8.0.425(`global.json`, `rollForward: disable`), Release
4. `Deterministic=true`(기존 `Directory.Build.props`, 변경 없음), `ContinuousIntegrationBuild=true`(전용 명령에서 전달)
5. 빌드 전에 모든 `bin/`·`obj/` 초기화
6. 명령: `dotnet build StarPlayer.sln -c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false -p:ContinuousIntegrationBuild=true`
   - 환경 변수: `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `UseSharedCompilation=false`
7. 출력 검사:
   - 모든 StarPlayer 어셈블리의 SourceLink Commit = 승인 Commit, URL = 공식 URL
   - CodeView·PDB 문서 경로가 `/_/`로 시작(출력 경로 정규화)
8. 경로가 다른 두 독립 폴더에서 같은 절차로 빌드해 10절 artifact와 PDB SHA가 동일한지 확인합니다. 누락은 실패입니다.

- 근거: [전체 빌드 진단](B1_G3_FULL_BUILD_DIAGNOSTIC_REPORT.md)(13개 프로젝트, 경로 4개 동일)
- Godot Export는 이 절차와 범위가 다릅니다(12절).

## 9. SourceLink와 운영 환경 미결 승인값

| 항목 | 상태 |
|---|---|
| 공식 SourceLink 원격 URL | 후보 `https://github.com/junk1901/Star_player.git`(실험 사용값). **OWNER_VALUE_PENDING** |
| 승인된 신뢰 원격에서 승인 Commit 확인 | **미확인.** Remote Push 금지이고 Fetch도 하지 않아, 원격에 `7bdde7d`가 있는지 알지 못합니다. 공식 검증 전에 필요합니다 |
| 운영 `environment_profile`(§18.2 `simharness-environment-profile-v1`) | 이번 자료로 만들지 않았습니다. 13절 빌드 환경 기록과 분리합니다. N12 Golden도 승인하지 않았습니다 |
| `build_procedure_clause_id`(B-4 조항) | 미정. 8절은 후보입니다 |

## 10. Artifact 역할별 파일 후보 (§18.3 닫힌 역할 유지, B-4 후보)

**선정 근거:**

- **SIMHARNESS_BINARY:** 실제 실행 바이너리는 `dotnet <SimHarness.dll>`로 실행되는 DLL입니다(`tools/b1-drypilot-repro.ps1`과 같은 방식).
- **VERIFIER_BINARY:** 인증 경계에서 실행되는 검증기 DLL입니다.
- **TEST_ASSEMBLY:**
  - 승인 Test Profile의 Case가 쓰는 어셈블리입니다. 계약 Test Profile 약어 "Infra·Sim·Harn·Dom", 즉 `StarPlayer.Infrastructure.Tests`·`StarPlayer.Simulation.Tests`·`StarPlayer.SimHarness.Tests`·`StarPlayer.Domain.Tests`입니다.
  - 검증기는 이 artifact의 SHA를 확인한 뒤 그 경로에서 `dotnet test <assembly>`를 실행합니다(`TestProfiles.cs` 156~158행).
- **DEPENDENCY:** 위 소비자 각각의 `*.deps.json`에 적힌 runtime·native(Windows RID) 파일 중, 그 소비자 출력 폴더에 실제로 있는 것입니다.
- **RUNTIME_CONFIG:** 위 소비자의 `*.runtimeconfig.json`·`*.deps.json`입니다.
- **TRUST_CONFIG:** 빌드 출력이 아닙니다. 승인된 보호 앵커 설정이며, 이번 범위에서 정하지 않습니다.

**집계(S1 CI 빌드 기준):** 5개 빌드(S1·S1r·S2·S3·V0, 경로 4개)에서 109개 모두 SHA가 같고, 누락 0입니다. 전체 목록은 부록 A입니다.

| 역할 | 파일 수 |
|---|---:|
| SIMHARNESS_BINARY | 1 |
| VERIFIER_BINARY | 1 |
| TEST_ASSEMBLY | 4 |
| DEPENDENCY | 91 |
| RUNTIME_CONFIG | 12 |

**제외(Provenance 실행·시험·종속 집합에 넣지 않음, 목록·이유·SHA 기록):**

| 파일 | 이유 | SHA-256(S1) |
|---|---|---|
| `tests/StarPlayer.Application.Tests/bin/Release/net8.0/.msCoverageSourceRootsMapping_StarPlayer.Application.Tests` | 코드 커버리지 도구의 경로 대응표(빌드 경로·NuGet 캐시 경로 포함). 어셈블리가 아니며 경로마다 다름 | `b7723b786866cb851c1dbae146d1194d26a3560da8aaff85d6b05f058b0034fb` |
| `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/.msCoverageSourceRootsMapping_StarPlayer.Domain.Tests` | 같음 | `32592342a8f4e3876f14c65a6da3b7d8fe656f61748d628bb716cff2b5a9d79d` |
| `tests/StarPlayer.EvidenceVerifier.Tests/bin/Release/net8.0/.msCoverageSourceRootsMapping_StarPlayer.EvidenceVerifier.Tests` | 같음 | `796f2a38fb300f0f08745132417a317ec814c1d4db5c3ae4c31247e95bc1398f` |
| `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/.msCoverageSourceRootsMapping_StarPlayer.Infrastructure.Tests` | 같음 | `c8df2f181c94c5f4c96995bbc3dc7257a1fdd932a004168a9356081268eddd9b` |
| `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/.msCoverageSourceRootsMapping_StarPlayer.SimHarness.Tests` | 같음 | `dd08f0457b368754ab6b3cf0968bad64674926cb41d05cb795447b1f555625a0` |
| `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/.msCoverageSourceRootsMapping_StarPlayer.Simulation.Tests` | 같음 | `058fa168bbc3bae4604c12076f8857a2bf773ced39108dc8b5e38dc783358e4b` |
| 모든 `*.pdb` | OWNER 결정: 별도 진단 증거. SHA 목록은 [전체 빌드 진단](B1_G3_FULL_BUILD_DIAGNOSTIC_REPORT.md) 10절, `E:\Madegame\g3x\evidence\pdb_S1\` | — |
| Godot `StarPlayer.pck` 등 Export 파일 | OWNER 결정: DEPENDENCY에 넣지 않음. 게임 배포 Export는 별도 범위 | — |
| apphost `*.exe`, Test Profile에 없는 어셈블리(`StarPlayer.Application.Tests`·`StarPlayer.EvidenceVerifier.Tests`)와 그 폴더 | 위 소비자가 쓰지 않음(`dotnet <dll>`·`dotnet test <dll>` 실행) | 전체 빌드 진단 보고서와 `E:\Madegame\g3x\full-out\S1\meta.json`에 SHA 있음 |

- **이 목록의 한계:**
  - `deps.json` 기준입니다. `dotnet test`가 SDK 쪽에서 읽는 파일(vstest 호스트 등)과 공유 런타임(`Microsoft.NETCore.App`)은 빌드 출력이 아니어서 포함하지 않았습니다.
  - 실제 실행 중 열리는 파일을 추적하지는 않았습니다.
  - `relative_path`의 루트(체크아웃 루트)와 정렬·중복 규칙(§18.3: ordinal 오름차순, 중복 금지)은 B-4에서 확정해야 합니다.
  - 이 목록은 Provenance 등록이 아닙니다.

## 11. OWNER 추가 결정 필요 항목

1. **정식 Scene 수정:** 원본 Scene 4개를 Godot 4.7.2로 재저장해 `unique_id`를 저장할지(별도 TASK·검수). 이번 R1 결과는 진단본입니다.
   - 편집기 UI 저장 또는 "Upgrade Project Files"로 할 경우 결과가 R1과 같은지 확인해야 합니다.
2. **Scene 입력 바이트 정책:** `.tscn`(그리고 `game/` 아래 다른 텍스트 리소스)의 개행 규칙. 현재는 `.gitattributes` 규칙이 없어 체크아웃 설정에 따라 CRLF/LF가 달라집니다.
3. **Godot PCK 수용 기준:** 같은 승인 입력·통제된 Export 환경에서 같은 경로·다른 경로 모두 바이트 동일(제안). 함께 정할 것:
   - 내부 파일 역할·개수·SHA 기록 방식
   - Godot 실행 검증 범위(verify G2~G4·E2~E4 전체)
4. 공식 SourceLink URL 확정, 원격에서 승인 Commit을 확인하는 방법(Push 허가와 별개)
5. 10절 artifact 후보 목록의 확정(루트·포함 범위·SDK 쪽 파일 처리)과 B-4 조항 ID
6. 운영 Environment Profile(§18.2) 작성·등록 시점. 이번 빌드 환경 기록과 섞지 않습니다.

## 12. 범위 구분

- **B1 SimHarness 증거 바이너리의 G3 범위:**
  - 대상: §18.3 artifact(10절)
  - 근거: .NET CI 빌드의 경로 무관 재현성(전체 빌드 진단)
  - Godot Export와 무관합니다.
- **Godot 게임 배포 Export 검증 범위:**
  - 대상: `StarPlayer.pck`와 Export 폴더
  - 현재 원본 Scene으로는 재현되지 않습니다. 재저장 진단본으로는 재현됩니다.
  - 운영 배포 산출물로 동결하지 않았습니다.

## 13. 빌드 환경 기록 (진단 자료, 운영 Environment Profile 아님)

| 항목 | 값 |
|---|---|
| Git | 실험 소스 `7bdde7dd091f9a66957bc740c77d470c7732b3ad`. SourceLink `/_/*` → `https://raw.githubusercontent.com/junk1901/Star_player/7bdde7dd…/*`(전체 빌드 진단 5절) |
| OS | Microsoft Windows 11 Home, `Microsoft Windows NT 10.0.26200.0`, build 26200, 64-bit, `PROCESSOR_ARCHITECTURE=AMD64`, RID `win-x64` |
| .NET SDK | 8.0.425(commit `4a98f641c7`), MSBuild 17.11.48+02bf66295, workload 8.0.400-manifests.ac071bc2, `C:\Program Files\dotnet\sdk\8.0.425\` |
| 런타임 | Microsoft.NETCore.App 8.0.31(host commit `1219a42122`) |
| 컴파일러 | Roslyn `4.11.0-3.25569.22+3fb752d448006a3144a60ccf181d745e555422f9`(PDB 컴파일 옵션) |
| NuGet 패키지(22개, `deps.json`) | Microsoft.CodeCoverage 17.14.1, Microsoft.Data.Sqlite(.Core) 8.0.31, Microsoft.NET.Test.Sdk 17.14.1, Microsoft.TestPlatform.ObjectModel·TestHost 17.14.1, Newtonsoft.Json 13.0.3, SQLitePCLRaw.bundle_e_sqlite3·core·lib.e_sqlite3·provider.e_sqlite3 2.1.12, System.Collections.Immutable 8.0.0, System.Memory 4.5.3, System.Reflection.Metadata 8.0.0, xunit 2.9.3(core·assert·extensibility.core·extensibility.execution 2.9.3, abstractions 2.0.3, analyzers 1.18.0), xunit.runner.visualstudio 2.8.2 |
| Godot | 2절 |
| 빌드 설정·환경 변수 | 8절 6항, 2절 |
| 입력 파일 | `Directory.Build.props` `9e981e84…`, `global.json` `97b37563…`, 튜닝 `992d7e24…`, 규칙 `b016b37b…`, `project.godot` `9da464f4…`, `export_presets.cfg` `0f51945d…`, Scene 3절 |
| Restore 의존성 | 위 패키지(NuGet 전역 캐시 `C:\Users\user\.nuget\packages\`, 비어 있는 캐시에서의 Restore는 미시험) |
| 출력 SHA | .NET: 전체 빌드 진단 4절·10절. Export: 6절. Artifact 후보: 부록 A |
| 재현성 비교 결과 | .NET: 경로 4개 동일(coverage 대응표 제외). PCK: 원본 Scene 불일치, 재저장 Scene 동일 |

## 14. 실험 도구·산출물 (저장소 밖, Git에 추가하지 않음)

| 경로 | 내용 | SHA-256(앞 16자) |
|---|---|---|
| `E:\Madegame\g3x\tools\run_pck.ps1` | Export·실행 검사·기록 | `34b07a1ee30425d3` |
| `E:\Madegame\g3x\tools\resave.gd`·`resave_pack.gd` | R0·R1 재저장 | `1689c572874c7e00`·`680501bb3e626bf3` |
| `E:\Madegame\g3x\tools\resave_run.ps1` | 재저장 실행·기록 | `7a58e2e20c22ff35` |
| `E:\Madegame\g3x\tools\pck_batch.ps1` | 실험 A~D 일괄 실행 | `d763ef82700015bd` |
| `E:\Madegame\g3x\tools\pck_summary.py`·`pck_diff.py` | PCK 해석·비교 | `4d0880241820f8cc`·`57f7c45b1a012611` |
| `E:\Madegame\g3x\tools\roles_candidates.py`, `E:\Madegame\g3x\roles_candidates.json` | Artifact 후보 생성·결과 | `1366a15d002c2226`·`935863dbdfb1ea01` |
| `E:\Madegame\g3x\pck-out\<ID>\` | 각 실행의 `StarPlayer.pck`, `meta.json`, 로그, 실행 검사 출력·스크린샷 | — |
| `E:\Madegame\g3x\pck-out\R0_resourcesaver\`·`R1_pack\` | 재저장 Scene 바이트와 diff | 5절 |

- **진단 clone 상태:**
  - `pa`·`pd`는 `7bdde7d` 그대로입니다(변경 0).
  - `pb`·`pcxlong`에는 R1 재저장 Scene 4개가 **진단 수정본으로 남아 있습니다**(승인 소스 아님).

**재현 방법:**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File E:\Madegame\g3x\tools\resave_run.ps1 -Clone E:\Madegame\g3x\pb -Id <ID> -Script E:/Madegame/g3x/tools/resave_pack.gd
powershell -NoProfile -ExecutionPolicy Bypass -File E:\Madegame\g3x\tools\run_pck.ps1 -Clone E:\Madegame\g3x\pb -Id <ID> -AllowModified "<scene>|<scene>" [-RunChecks]
python -I E:\Madegame\g3x\tools\pck_summary.py A1:A2 B1:B2
```

Remote Push 없음. 운영 Gate G1~G9 미실행. Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 부록 A. Artifact 역할별 파일 후보 전체 (S1 CI 빌드, 5개 빌드 동일)

| # | role | relative_path | byte_length | raw_sha256 |
|---:|---|---|---:|---|
| 1 | SIMHARNESS_BINARY | `tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.dll` | 906,752 | `84cb531f314cc586a693487564d8be2562e14e9aeb65bc9994f3ad5c8ba4a3d9` |
| 2 | VERIFIER_BINARY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.EvidenceVerifier.dll` | 218,112 | `347fc509974014c27b19ac9f369f788bb70bcc7a6a4a2e505b837586093aa278` |
| 3 | TEST_ASSEMBLY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/StarPlayer.Domain.Tests.dll` | 182,272 | `32730832a9794027fe29d5467c0538e18346091d5e80f545cadbf64486ec4891` |
| 4 | TEST_ASSEMBLY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/StarPlayer.Infrastructure.Tests.dll` | 1,122,816 | `673be1c097ae654e5da08f73136781b6547fd6af0db2e9a9c03999f95a5054b5` |
| 5 | TEST_ASSEMBLY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/StarPlayer.SimHarness.Tests.dll` | 618,496 | `2603b16fa7738b34f07b8ea799e775852fdf78b5ef72742fa23d0459ca3bc9e4` |
| 6 | TEST_ASSEMBLY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/StarPlayer.Simulation.Tests.dll` | 648,704 | `4d806e5fc1d5970526155442932fb4175f6ad9b7381dcdec89b43ae4bd77b2b0` |
| 7 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/Microsoft.TestPlatform.CommunicationUtilities.dll` | 137,776 | `e38a359aa502bb1867d6092dca9aba5c130dd6f9111decea8156aa746b2c69e6` |
| 8 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/Microsoft.TestPlatform.CoreUtilities.dll` | 98,336 | `f6e2495c44640d8915b2aefc938f391c970300266da014350ff35512a448ee84` |
| 9 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/Microsoft.TestPlatform.CrossPlatEngine.dll` | 316,960 | `d55399bc66dc5b451d60ad82662452ca4a5f735918135dfc91ca9b5e3e38a6ab` |
| 10 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/Microsoft.TestPlatform.PlatformAbstractions.dll` | 51,232 | `7b919ce4429c3b7b8fa2c36fd641b70b2f459a30ecb7c65fd80f437673cec930` |
| 11 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/Microsoft.TestPlatform.Utilities.dll` | 67,104 | `01ffd64d25a4bd905aef9594d2aa700e020273a90a77a8720af6bec8b4fd5444` |
| 12 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/Microsoft.VisualStudio.CodeCoverage.Shim.dll` | 17,464 | `a753bc15f23a957f4f91a059376e4472c6bacd1503622de4b9b2b079ad8ea289` |
| 13 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/Microsoft.VisualStudio.TestPlatform.Common.dll` | 248,352 | `059aa2f335c3f7b14ac3fb7caa1625bc2767581b9548e9ff53a246c7f2f94d6a` |
| 14 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/Microsoft.VisualStudio.TestPlatform.ObjectModel.dll` | 334,392 | `f83e38ee9020baa42e284e2299e233070ffa86ce4b10213583f9b7c8a521bcd0` |
| 15 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/Newtonsoft.Json.dll` | 712,464 | `22c649f75fce5be7c7ccda8880473b634ef69ecf33f5d1ab8ad892caf47d5a07` |
| 16 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/StarPlayer.Domain.dll` | 389,120 | `e00239849182f7c7192f8e184f40cf5a0e5b07006baff1f94e99551c88a8ecb7` |
| 17 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/testhost.dll` | 49,696 | `92cbdf52db5249112f9f53f1fcdaf9f9aa3fce467141c688a31a7fe095e4d304` |
| 18 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/xunit.abstractions.dll` | 21,928 | `3166dc70323fb30ccf1cedb0fe86f2ad122c46d254542342d90386efc4c9285c` |
| 19 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/xunit.assert.dll` | 187,088 | `bcd9711b22d227ac8cd4c1568e72433a0f576d2ec8aae64ac508081892015b1d` |
| 20 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/xunit.core.dll` | 70,352 | `2f7feeec8c6dec7df684c25fb80aadb292b963d09723460f03d53c5ad2088acc` |
| 21 | DEPENDENCY | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/xunit.execution.dotnet.dll` | 279,760 | `e70febe547c15f8155db3e2d95c3ba4445a59cbc21a3e7541ab22ed49d3dcf7b` |
| 22 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/Microsoft.Data.Sqlite.dll` | 174,904 | `88551560cc4857c1ad24280850eb0ce68286d19339692c96ad34abc0d43b4b94` |
| 23 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/Microsoft.TestPlatform.CommunicationUtilities.dll` | 137,776 | `e38a359aa502bb1867d6092dca9aba5c130dd6f9111decea8156aa746b2c69e6` |
| 24 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/Microsoft.TestPlatform.CoreUtilities.dll` | 98,336 | `f6e2495c44640d8915b2aefc938f391c970300266da014350ff35512a448ee84` |
| 25 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/Microsoft.TestPlatform.CrossPlatEngine.dll` | 316,960 | `d55399bc66dc5b451d60ad82662452ca4a5f735918135dfc91ca9b5e3e38a6ab` |
| 26 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/Microsoft.TestPlatform.PlatformAbstractions.dll` | 51,232 | `7b919ce4429c3b7b8fa2c36fd641b70b2f459a30ecb7c65fd80f437673cec930` |
| 27 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/Microsoft.TestPlatform.Utilities.dll` | 67,104 | `01ffd64d25a4bd905aef9594d2aa700e020273a90a77a8720af6bec8b4fd5444` |
| 28 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/Microsoft.VisualStudio.CodeCoverage.Shim.dll` | 17,464 | `a753bc15f23a957f4f91a059376e4472c6bacd1503622de4b9b2b079ad8ea289` |
| 29 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/Microsoft.VisualStudio.TestPlatform.Common.dll` | 248,352 | `059aa2f335c3f7b14ac3fb7caa1625bc2767581b9548e9ff53a246c7f2f94d6a` |
| 30 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/Microsoft.VisualStudio.TestPlatform.ObjectModel.dll` | 334,392 | `f83e38ee9020baa42e284e2299e233070ffa86ce4b10213583f9b7c8a521bcd0` |
| 31 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/Newtonsoft.Json.dll` | 712,464 | `22c649f75fce5be7c7ccda8880473b634ef69ecf33f5d1ab8ad892caf47d5a07` |
| 32 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/SQLitePCLRaw.batteries_v2.dll` | 5,120 | `815ce410c9eab531dd219318e3ba16c5e38e83d361cfbbdbb4bbca222c497b0b` |
| 33 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/SQLitePCLRaw.core.dll` | 51,200 | `af340ac9eb9195795660d7fc1114f486b6ab63998f33176e2f24c8e0acb9aa84` |
| 34 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/SQLitePCLRaw.provider.e_sqlite3.dll` | 36,864 | `df996bcae63c2cabcfe8d4b9c5110eb39a2a4706178b07d875665391c6eb3995` |
| 35 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/StarPlayer.Application.dll` | 140,288 | `1772f78f852665df0d8cac8467f3bab0e5e4ed654455de91b8bbfa5ec1112b29` |
| 36 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/StarPlayer.Domain.dll` | 389,120 | `e00239849182f7c7192f8e184f40cf5a0e5b07006baff1f94e99551c88a8ecb7` |
| 37 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/StarPlayer.EvidenceVerifier.Tests.dll` | 206,848 | `9635f44caf592c2701f9dc0285de073dfebf196440e617ff0e9a0f152842061e` |
| 38 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/StarPlayer.EvidenceVerifier.dll` | 218,112 | `347fc509974014c27b19ac9f369f788bb70bcc7a6a4a2e505b837586093aa278` |
| 39 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/StarPlayer.Infrastructure.dll` | 434,688 | `15b9330cd5ffdec2934f8acaa999e0d7d3d644d73cbb4d83d8ca3dc385e13329` |
| 40 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/StarPlayer.SimHarness.Tests.dll` | 618,496 | `2603b16fa7738b34f07b8ea799e775852fdf78b5ef72742fa23d0459ca3bc9e4` |
| 41 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/StarPlayer.SimHarness.dll` | 906,752 | `84cb531f314cc586a693487564d8be2562e14e9aeb65bc9994f3ad5c8ba4a3d9` |
| 42 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/StarPlayer.Simulation.dll` | 366,592 | `6296c9fc09cf567e9106004138a83cb47105660899596006e836561edd5b55c7` |
| 43 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/runtimes/win-arm64/native/e_sqlite3.dll` | 1,814,528 | `4177542756bd94f75b55fb74367e0cf34e66a88ffaf95755489af14c6ca2f792` |
| 44 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/runtimes/win-x64/native/e_sqlite3.dll` | 1,978,880 | `b7385d722c83fb52142a00477a726723745916d22a555711ee89834c1111fb2e` |
| 45 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/runtimes/win-x86/native/e_sqlite3.dll` | 1,533,952 | `d13eb819d1353d98c684c88672868bbfcc6a52eb5987bdf2557ec02fabce4ea7` |
| 46 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/testhost.dll` | 49,696 | `92cbdf52db5249112f9f53f1fcdaf9f9aa3fce467141c688a31a7fe095e4d304` |
| 47 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/xunit.abstractions.dll` | 21,928 | `3166dc70323fb30ccf1cedb0fe86f2ad122c46d254542342d90386efc4c9285c` |
| 48 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/xunit.assert.dll` | 187,088 | `bcd9711b22d227ac8cd4c1568e72433a0f576d2ec8aae64ac508081892015b1d` |
| 49 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/xunit.core.dll` | 70,352 | `2f7feeec8c6dec7df684c25fb80aadb292b963d09723460f03d53c5ad2088acc` |
| 50 | DEPENDENCY | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/xunit.execution.dotnet.dll` | 279,760 | `e70febe547c15f8155db3e2d95c3ba4445a59cbc21a3e7541ab22ed49d3dcf7b` |
| 51 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/Microsoft.TestPlatform.CommunicationUtilities.dll` | 137,776 | `e38a359aa502bb1867d6092dca9aba5c130dd6f9111decea8156aa746b2c69e6` |
| 52 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/Microsoft.TestPlatform.CoreUtilities.dll` | 98,336 | `f6e2495c44640d8915b2aefc938f391c970300266da014350ff35512a448ee84` |
| 53 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/Microsoft.TestPlatform.CrossPlatEngine.dll` | 316,960 | `d55399bc66dc5b451d60ad82662452ca4a5f735918135dfc91ca9b5e3e38a6ab` |
| 54 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/Microsoft.TestPlatform.PlatformAbstractions.dll` | 51,232 | `7b919ce4429c3b7b8fa2c36fd641b70b2f459a30ecb7c65fd80f437673cec930` |
| 55 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/Microsoft.TestPlatform.Utilities.dll` | 67,104 | `01ffd64d25a4bd905aef9594d2aa700e020273a90a77a8720af6bec8b4fd5444` |
| 56 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/Microsoft.VisualStudio.CodeCoverage.Shim.dll` | 17,464 | `a753bc15f23a957f4f91a059376e4472c6bacd1503622de4b9b2b079ad8ea289` |
| 57 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/Microsoft.VisualStudio.TestPlatform.Common.dll` | 248,352 | `059aa2f335c3f7b14ac3fb7caa1625bc2767581b9548e9ff53a246c7f2f94d6a` |
| 58 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/Microsoft.VisualStudio.TestPlatform.ObjectModel.dll` | 334,392 | `f83e38ee9020baa42e284e2299e233070ffa86ce4b10213583f9b7c8a521bcd0` |
| 59 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/Newtonsoft.Json.dll` | 712,464 | `22c649f75fce5be7c7ccda8880473b634ef69ecf33f5d1ab8ad892caf47d5a07` |
| 60 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/StarPlayer.Domain.dll` | 389,120 | `e00239849182f7c7192f8e184f40cf5a0e5b07006baff1f94e99551c88a8ecb7` |
| 61 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/StarPlayer.SimHarness.dll` | 906,752 | `84cb531f314cc586a693487564d8be2562e14e9aeb65bc9994f3ad5c8ba4a3d9` |
| 62 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/StarPlayer.Simulation.dll` | 366,592 | `6296c9fc09cf567e9106004138a83cb47105660899596006e836561edd5b55c7` |
| 63 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/testhost.dll` | 49,696 | `92cbdf52db5249112f9f53f1fcdaf9f9aa3fce467141c688a31a7fe095e4d304` |
| 64 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/xunit.abstractions.dll` | 21,928 | `3166dc70323fb30ccf1cedb0fe86f2ad122c46d254542342d90386efc4c9285c` |
| 65 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/xunit.assert.dll` | 187,088 | `bcd9711b22d227ac8cd4c1568e72433a0f576d2ec8aae64ac508081892015b1d` |
| 66 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/xunit.core.dll` | 70,352 | `2f7feeec8c6dec7df684c25fb80aadb292b963d09723460f03d53c5ad2088acc` |
| 67 | DEPENDENCY | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/xunit.execution.dotnet.dll` | 279,760 | `e70febe547c15f8155db3e2d95c3ba4445a59cbc21a3e7541ab22ed49d3dcf7b` |
| 68 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/Microsoft.TestPlatform.CommunicationUtilities.dll` | 137,776 | `e38a359aa502bb1867d6092dca9aba5c130dd6f9111decea8156aa746b2c69e6` |
| 69 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/Microsoft.TestPlatform.CoreUtilities.dll` | 98,336 | `f6e2495c44640d8915b2aefc938f391c970300266da014350ff35512a448ee84` |
| 70 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/Microsoft.TestPlatform.CrossPlatEngine.dll` | 316,960 | `d55399bc66dc5b451d60ad82662452ca4a5f735918135dfc91ca9b5e3e38a6ab` |
| 71 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/Microsoft.TestPlatform.PlatformAbstractions.dll` | 51,232 | `7b919ce4429c3b7b8fa2c36fd641b70b2f459a30ecb7c65fd80f437673cec930` |
| 72 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/Microsoft.TestPlatform.Utilities.dll` | 67,104 | `01ffd64d25a4bd905aef9594d2aa700e020273a90a77a8720af6bec8b4fd5444` |
| 73 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/Microsoft.VisualStudio.CodeCoverage.Shim.dll` | 17,464 | `a753bc15f23a957f4f91a059376e4472c6bacd1503622de4b9b2b079ad8ea289` |
| 74 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/Microsoft.VisualStudio.TestPlatform.Common.dll` | 248,352 | `059aa2f335c3f7b14ac3fb7caa1625bc2767581b9548e9ff53a246c7f2f94d6a` |
| 75 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/Microsoft.VisualStudio.TestPlatform.ObjectModel.dll` | 334,392 | `f83e38ee9020baa42e284e2299e233070ffa86ce4b10213583f9b7c8a521bcd0` |
| 76 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/Newtonsoft.Json.dll` | 712,464 | `22c649f75fce5be7c7ccda8880473b634ef69ecf33f5d1ab8ad892caf47d5a07` |
| 77 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/StarPlayer.Domain.dll` | 389,120 | `e00239849182f7c7192f8e184f40cf5a0e5b07006baff1f94e99551c88a8ecb7` |
| 78 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/StarPlayer.Simulation.dll` | 366,592 | `6296c9fc09cf567e9106004138a83cb47105660899596006e836561edd5b55c7` |
| 79 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/testhost.dll` | 49,696 | `92cbdf52db5249112f9f53f1fcdaf9f9aa3fce467141c688a31a7fe095e4d304` |
| 80 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/xunit.abstractions.dll` | 21,928 | `3166dc70323fb30ccf1cedb0fe86f2ad122c46d254542342d90386efc4c9285c` |
| 81 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/xunit.assert.dll` | 187,088 | `bcd9711b22d227ac8cd4c1568e72433a0f576d2ec8aae64ac508081892015b1d` |
| 82 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/xunit.core.dll` | 70,352 | `2f7feeec8c6dec7df684c25fb80aadb292b963d09723460f03d53c5ad2088acc` |
| 83 | DEPENDENCY | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/xunit.execution.dotnet.dll` | 279,760 | `e70febe547c15f8155db3e2d95c3ba4445a59cbc21a3e7541ab22ed49d3dcf7b` |
| 84 | DEPENDENCY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/Microsoft.Data.Sqlite.dll` | 174,904 | `88551560cc4857c1ad24280850eb0ce68286d19339692c96ad34abc0d43b4b94` |
| 85 | DEPENDENCY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/SQLitePCLRaw.batteries_v2.dll` | 5,120 | `815ce410c9eab531dd219318e3ba16c5e38e83d361cfbbdbb4bbca222c497b0b` |
| 86 | DEPENDENCY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/SQLitePCLRaw.core.dll` | 51,200 | `af340ac9eb9195795660d7fc1114f486b6ab63998f33176e2f24c8e0acb9aa84` |
| 87 | DEPENDENCY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/SQLitePCLRaw.provider.e_sqlite3.dll` | 36,864 | `df996bcae63c2cabcfe8d4b9c5110eb39a2a4706178b07d875665391c6eb3995` |
| 88 | DEPENDENCY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.Application.dll` | 140,288 | `1772f78f852665df0d8cac8467f3bab0e5e4ed654455de91b8bbfa5ec1112b29` |
| 89 | DEPENDENCY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.Domain.dll` | 389,120 | `e00239849182f7c7192f8e184f40cf5a0e5b07006baff1f94e99551c88a8ecb7` |
| 90 | DEPENDENCY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.Infrastructure.dll` | 434,688 | `15b9330cd5ffdec2934f8acaa999e0d7d3d644d73cbb4d83d8ca3dc385e13329` |
| 91 | DEPENDENCY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.SimHarness.dll` | 906,752 | `84cb531f314cc586a693487564d8be2562e14e9aeb65bc9994f3ad5c8ba4a3d9` |
| 92 | DEPENDENCY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.Simulation.dll` | 366,592 | `6296c9fc09cf567e9106004138a83cb47105660899596006e836561edd5b55c7` |
| 93 | DEPENDENCY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/runtimes/win-arm64/native/e_sqlite3.dll` | 1,814,528 | `4177542756bd94f75b55fb74367e0cf34e66a88ffaf95755489af14c6ca2f792` |
| 94 | DEPENDENCY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/runtimes/win-x64/native/e_sqlite3.dll` | 1,978,880 | `b7385d722c83fb52142a00477a726723745916d22a555711ee89834c1111fb2e` |
| 95 | DEPENDENCY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/runtimes/win-x86/native/e_sqlite3.dll` | 1,533,952 | `d13eb819d1353d98c684c88672868bbfcc6a52eb5987bdf2557ec02fabce4ea7` |
| 96 | DEPENDENCY | `tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.Domain.dll` | 389,120 | `e00239849182f7c7192f8e184f40cf5a0e5b07006baff1f94e99551c88a8ecb7` |
| 97 | DEPENDENCY | `tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.Simulation.dll` | 366,592 | `6296c9fc09cf567e9106004138a83cb47105660899596006e836561edd5b55c7` |
| 98 | RUNTIME_CONFIG | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/StarPlayer.Domain.Tests.deps.json` | 18,702 | `d917acfaa0e61c7e2c3a457ef9e9cc97db205064eb8974101673e639ca6c4966` |
| 99 | RUNTIME_CONFIG | `tests/StarPlayer.Domain.Tests/bin/Release/net8.0/StarPlayer.Domain.Tests.runtimeconfig.json` | 388 | `7b7401577f185805bf9f44124cdbb0b919047ad4cde5b4ac4e9f0b37468f68ca` |
| 100 | RUNTIME_CONFIG | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/StarPlayer.Infrastructure.Tests.deps.json` | 30,953 | `e28c54df3b195698ab3076cd50f39399288965db3c1232e49aa11d7bb70d6914` |
| 101 | RUNTIME_CONFIG | `tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/StarPlayer.Infrastructure.Tests.runtimeconfig.json` | 388 | `7b7401577f185805bf9f44124cdbb0b919047ad4cde5b4ac4e9f0b37468f68ca` |
| 102 | RUNTIME_CONFIG | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/StarPlayer.SimHarness.Tests.deps.json` | 19,660 | `f94aafefaf9cf789f093331b3068f1a0a3fbdcc24b97139f0cb69f10cb771bb9` |
| 103 | RUNTIME_CONFIG | `tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/StarPlayer.SimHarness.Tests.runtimeconfig.json` | 388 | `7b7401577f185805bf9f44124cdbb0b919047ad4cde5b4ac4e9f0b37468f68ca` |
| 104 | RUNTIME_CONFIG | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/StarPlayer.Simulation.Tests.deps.json` | 19,211 | `bee4740641c59f633ca6ebbc8143009138a7e09488075824f596342bca076d31` |
| 105 | RUNTIME_CONFIG | `tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/StarPlayer.Simulation.Tests.runtimeconfig.json` | 388 | `7b7401577f185805bf9f44124cdbb0b919047ad4cde5b4ac4e9f0b37468f68ca` |
| 106 | RUNTIME_CONFIG | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.EvidenceVerifier.deps.json` | 11,239 | `740761f59b5e4ae54468fc09524fb110cb210ebac4e60d37b5393bed81401d38` |
| 107 | RUNTIME_CONFIG | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.EvidenceVerifier.runtimeconfig.json` | 340 | `28329cf08f6505e73806b17558b187c02f0c1c516fe47ebfb7a013d082aaa306` |
| 108 | RUNTIME_CONFIG | `tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.deps.json` | 1,399 | `d3607fddee8f01391219068cd9dbc01884ae8178ce85151e83d3d6921fdcd068` |
| 109 | RUNTIME_CONFIG | `tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.runtimeconfig.json` | 340 | `28329cf08f6505e73806b17558b187c02f0c1c516fe47ebfb7a013d082aaa306` |
