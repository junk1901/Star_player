# B1 G3 전체 빌드 재현성 확장 진단 결과

- 작성: Claude Code, 2026-10-26. 근거: OWNER 지시 "G3 전체 빌드 재현성 확장 진단"(2026-10-26), [TASK](../tasks/B1_G3_FULL_BUILD_DIAGNOSTIC_TASK.md)
- **성격:**
  - 진단입니다.
  - B-4 공식 빌드 절차 확정, 운영 G3 PASS, 공식 Build Provenance 등록, Build Freeze, Godot Export 동결이 **아닙니다**.
  - 저장소의 코드·설정·`tools/verify.ps1`·Tag는 바꾸지 않았습니다. CI 속성은 진단 명령에서만 줬습니다.
- **이전 이력(수정하지 않음):** [조사 보고서](B1_G3_BUILD_REPRODUCIBILITY_INVESTIGATION.md)(기존 빌드 6개), [SimHarness 실험 보고서](B1_G3_REPRO_EXPERIMENT_REPORT.md)(22회)
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 결론 요약

| 항목 | 결과 |
|---|---|
| 전체 솔루션 13개 프로젝트 DLL·PDB(CI) | **같은 경로 반복, 다른 경로 3개(길이 2종)에서 모두 바이트 동일.** 비교 대상 파일 누락 0 |
| 그 밖의 빌드 출력 | `deps.json` 22·`runtimeconfig.json` 18·apphost `.exe` 14·외부 DLL 528 동일. **예외:** 시험 출력 폴더의 `.msCoverageSourceRootsMapping_*` 6개가 빌드 경로에 따라 다름(코드 커버리지 도구의 경로 대응표, 어셈블리 아님) |
| 비결정성 | 같은 Commit·입력·정책에서 .NET DLL·PDB 차이 **0**(이번 범위). Godot Export에서는 **있음**: `StarPlayer.pck`가 같은 경로 반복에서도 다름 |
| SourceLink | 55개 StarPlayer 어셈블리 모두 `/_/*` → `https://raw.githubusercontent.com/junk1901/Star_player/7bdde7dd…/*`. 실제 빌드 Commit과 일치 |
| 다른 Commit(입력 동일) | SourceLink Commit과 그 파생 식별자만 다름 → **출처 차이**(재현성 결함 아님) |
| 로컬 경로 원격 | SourceLink가 생성되지 않음 → 정식 원격으로 쓸 수 없음 |
| Godot Export | .NET 출력 195/195, Export 파일 197/198 동일. `StarPlayer.pck`만 다르며, 원인은 변환된 장면 4개의 `node_ids` 값 |
| 게임 결과 불변 | 진단 빌드로 U1~U6 3,041 PASS / 0 FAIL / 0 SKIP, Golden 4/4, Dry Pilot `ALL_9_IDENTICAL=True` |

## 2. 통제 조건

| 항목 | 값 |
|---|---|
| 실험 소스 Commit | `7bdde7dd091f9a66957bc740c77d470c7732b3ad`(Tag `b1-code-accepted-2026-10-26`). 실험 C만 `b17742aa13161937e150400415548ca758f122e5` |
| 입력 동일성 | `docs/`를 뺀 전체 파일 목록(경로 + blob id) 해시가 두 Commit 모두 `b39d7a26…c227`로 같음. Git tree: `7bdde7d` `076eca1b…`, `b17742a` `0017e8bc…`(차이는 `docs/`뿐) |
| clone | `git clone --no-hardlinks E:/Madegame/Star_player` → `checkout --detach <commit>` |
| clone 위치 | `E:\Madegame\g3x\fa`, `fb`(fa와 경로 길이 같음), `fclong`(4자 김), `fv`(불변성 검증용) |
| 작업 트리 | 모든 실행 전 `git status --porcelain --untracked-files=all` 0행(아니면 중단). 실행 후에도 0행. 지금 4개 clone 모두 HEAD `7bdde7d`, 추적 변경 0 |
| SDK | 8.0.425(`global.json`), 모든 실행에서 같음 |
| 캐시 분리 | 빌드 전마다 clone 안의 모든 `bin/`·`obj/`, `game/.godot/`, `build/export/` 삭제. Restore를 다시 함 |
| 솔루션 빌드 | `dotnet build StarPlayer.sln -c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false -p:ContinuousIntegrationBuild=true`(verify B1 명령 + CI) |
| 환경 변수 | `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `UseSharedCompilation=false`, `DOTNET_CLI_UI_LANGUAGE=en`, `DOTNET_NOLOGO=1`, `DOTNET_CLI_TELEMETRY_OPTOUT=1` |
| `Deterministic` | `true`(기존 `Directory.Build.props` `9e981e84…`, 변경 없음) |
| 입력 파일 | 튜닝 `992d7e24…`, 규칙 `b016b37b…`, `export_presets.cfg` `0f51945d…`, `project.godot` `9da464f4…`(모든 실행에서 같음) |
| 원격 URL(실험 통제값) | `https://github.com/junk1901/Star_player.git` |
| Godot | `4.7.2.stable.mono.official.ed1daf0bf`, Export Template 5개(verify T3 SHA와 같음), 플랫폼 Windows Desktop x86_64, preset `Windows Desktop`(`embed_pck=false`, `modify_resources=false`) |
| Godot 명령 | `godot --headless --path game --build-solutions --quit`, 그다음 `godot --headless --path game --export-release "Windows Desktop" ../build/export/StarPlayer.exe`. 환경 변수 `ContinuousIntegrationBuild=true`로 Godot 내부 `dotnet` 호출에 CI 전달 |
| 결과 | 모든 실행 exit 0, 경고 0. 솔루션 빌드 약 18초, Godot 단계 약 14초씩 |

**원격 URL 처리:**

- 원본 작업 트리 `origin`에 실제로 설정된 값을 실험 clone의 `.git/config`에만 넣었습니다.
- 인증 정보는 없습니다. Fetch·Push는 하지 않았습니다.
- **승인된 공식 URL이 아닙니다**(9절 `OWNER_VALUE_PENDING`).

## 3. 실험과 비교 결과

| ID | clone | Commit | 원격 | 출력 파일 |
|---|---|---|---|---:|
| S1·S1r | fa(2회) | `7bdde7d` | GitHub | 793·793 |
| S2 | fb | `7bdde7d` | GitHub | 793 |
| S3 | fclong | `7bdde7d` | GitHub | 793 |
| S4c | fa | `b17742a` | GitHub | 793 |
| S5L | fa | `7bdde7d` | 로컬 경로 `E:/Madegame/Star_player` | 793 |
| V0 | fv | `7bdde7d` | GitHub | 793(이후 불변성 검증) |
| X1·X1r | fa(2회) | `7bdde7d` | GitHub | 393·393(Export 198 + ExportRelease publish 195) |
| X2 | fb | `7bdde7d` | GitHub | 393 |
| X3 | fclong | `7bdde7d` | GitHub | 393 |

| 비교 | 동일 | 다름 | 누락 | 판정 |
|---|---:|---:|---:|---|
| S1 ↔ S1r(같은 경로 반복) | 793 | 0 | 0 | **바이트 동일** |
| S1 ↔ S2(다른 경로, 길이 같음) | 787 | 6 | 0 | DLL·PDB 동일. 다른 것은 coverage 대응표 6개 |
| S1 ↔ S3(다른 경로, 길이 다름) | 787 | 6 | 0 | 같음 |
| S1 ↔ V0(네 번째 경로) | 787 | 6 | 0 | 같음 |
| S1 ↔ S4c(Commit만 다름) | 683 | 110 | 0 | StarPlayer DLL 55 + PDB 55. DLL은 MVID/PDB 식별자 72바이트뿐. PDB는 SourceLink Commit과 PDB ID. **출처 차이** |
| S1 ↔ S5L(원격 로컬 경로) | 683 | 110 | 0 | 같은 110개. SourceLink 없음. DLL은 식별자 72바이트뿐 |
| X1 ↔ X1r(같은 경로 반복) | 392 | 1 | 0 | `StarPlayer.pck`만 다름 → **비결정성** |
| X1 ↔ X2, X1 ↔ X3(다른 경로) | 392 | 1 | 0 | 같은 `StarPlayer.pck`만 다름(경로와 무관) |

**바이트 분류:**

- 진단 도구가 DLL·PDB 영역의 오프셋을 읽어 분류했습니다. 방법은 [SimHarness 실험 보고서](B1_G3_REPRO_EXPERIMENT_REPORT.md) 3절과 같습니다.
- S1↔S4c·S5L의 DLL 차이는 55개 모두 "MVID/PDB 식별자"(COFF 시각·MVID·CodeView GUID/stamp·PdbChecksum) 범주뿐입니다. IL·메타데이터·리소스·경로는 같습니다.

## 4. 프로젝트별 DLL·PDB 재현성 (S1 기준)

모든 행이 S1 = S1r = S2 = S3이며 DLL·PDB 둘 다 같습니다. V0도 같습니다.

| 프로젝트 출력 | DLL 바이트 | DLL SHA-256 | PDB SHA-256 | MVID | IL | SourceLink Commit |
|---|---:|---|---|---|---|---|
| `game/.godot/mono/temp/bin/Release/StarPlayer.Client.dll` | 56,832 | `5a5e17bdb6b3f785…` | `c46f26cfe3843ae2…` | `f5d681e8` | `3c21d48f` | `7bdde7d` |
| `src/StarPlayer.Application/…/StarPlayer.Application.dll` | 140,288 | `1772f78f852665df…` | `9ecb27ce82dee579…` | `66f0e4ee` | `d6e97cc8` | `7bdde7d` |
| `src/StarPlayer.Domain/…/StarPlayer.Domain.dll` | 389,120 | `e00239849182f7c7…` | `75a3da7bdd00d18a…` | `820ef548` | `1f2f9c98` | `7bdde7d` |
| `src/StarPlayer.Infrastructure/…/StarPlayer.Infrastructure.dll` | 434,688 | `15b9330cd5ffdec2…` | `254e12d6132b9664…` | `7b7d56c9` | `274c22eb` | `7bdde7d` |
| `src/StarPlayer.Simulation/…/StarPlayer.Simulation.dll` | 366,592 | `6296c9fc09cf567e…` | `0e45d4e9cef7fdf7…` | `6bdf03e6` | `cf96f76d` | `7bdde7d` |
| `tests/StarPlayer.Application.Tests/…` | 113,152 | `3bbe9638a6096812…` | `19f396697c5744f9…` | `a23c728e` | `d58a4d10` | `7bdde7d` |
| `tests/StarPlayer.Domain.Tests/…` | 182,272 | `32730832a9794027…` | `5933756886e1c38c…` | `63236006` | `0dc7daca` | `7bdde7d` |
| `tests/StarPlayer.EvidenceVerifier.Tests/…` | 206,848 | `9635f44caf592c27…` | `499cde64299caf6f…` | `6d2af10d` | `44406734` | `7bdde7d` |
| `tests/StarPlayer.Infrastructure.Tests/…` | 1,122,816 | `673be1c097ae654e…` | `fa805db4e4786a9b…` | `b239d05d` | `22f98716` | `7bdde7d` |
| `tests/StarPlayer.SimHarness.Tests/…` | 618,496 | `2603b16fa7738b34…` | `71dfcc044d025d55…` | `04f4ab2b` | `b4bfbdea` | `7bdde7d` |
| `tests/StarPlayer.Simulation.Tests/…` | 648,704 | `4d806e5fc1d59705…` | `01da68a564df982b…` | `363303f5` | `e62e9ab3` | `7bdde7d` |
| `tools/StarPlayer.EvidenceVerifier/…/StarPlayer.EvidenceVerifier.dll` | 218,112 | `347fc509974014c2…` | `5ab5cb4e4250cc1f…` | `b9d0ea89` | `5d38ce64` | `7bdde7d` |
| `tools/StarPlayer.SimHarness/…/StarPlayer.SimHarness.dll` | 906,752 | `84cb531f314cc586…` | `44c99b6e3d819cb3…` | `4420ec59` | `f40f4044` | `7bdde7d` |

**확인된 사실:**

- **기록 경로:** 55개 StarPlayer 어셈블리(13개 + 다른 출력 폴더의 사본) 모두 CodeView 경로가 `/_/`로 시작합니다.
- **SourceLink:** 55개 모두 위의 SourceLink 한 종류입니다.
- **빌드 범위와 무관:** SimHarness DLL `84cb531f…`와 Domain DLL `e0023984…`는 이전 실험의 SimHarness 프로젝트 단독 빌드(EC1·EC2·EC3, CI + GitHub 원격)와 바이트가 같습니다.

## 5. SourceLink URL·Commit 정합성

| 조건 | SourceLink | 판정 |
|---|---|---|
| 같은 Commit, 정식 후보 원격(GitHub URL), CI | 키 `/_/*`, URL `https://raw.githubusercontent.com/junk1901/Star_player`, Commit `7bdde7dd…` = 실제 빌드 Commit | 일치. 경로 4개에서 동일 |
| 입력 동일, Commit `b17742a` | Commit만 `b17742aa…`로 바뀜 | 출처 차이. 재현성 결함 아님 |
| 원격이 로컬 경로 | 생성되지 않음(PDB에 SourceLink 없음) | 정식 원격 아님. 공식 정책 불충족 |

- **SourceLink 형식:** 인증 토큰·비밀정보는 없습니다(URL은 공개 raw 콘텐츠 주소 형식).
- **Assembly 속성:** 모든 빌드에서 `InformationalVersion` `0.0.0-phase0`입니다. Commit 정보는 없습니다(`IncludeSourceRevisionInInformationalVersion=false`, 변경하지 않음).

## 6. Godot Export 비교

| 파일 | 바이트 | SHA-256 | X1=X1r=X2=X3 | 비고 |
|---|---:|---|---|---|
| `build/export/StarPlayer.exe` | 109,513,728 | `16e0ec3cfd398b93…` | 동일 | Template `windows_release_x86_64.exe`와 같은 바이트 |
| `build/export/StarPlayer.console.exe` | 188,928 | `7895a7873947deaf…` | 동일 | Template `windows_release_x86_64_console.exe`와 같은 바이트 |
| `build/export/StarPlayer.pck` | 13,452 | X1 `5dab0e8ba412e7c5…` | **다름** | 아래 |
| `build/export/data_StarPlayer.Client_windows_x86_64/StarPlayer.Client.dll` | 56,320 | `ae2c5f53a7339fb1…` | 동일 | ExportRelease publish 출력과 같음 |
| 같은 폴더 `StarPlayer.Application/Domain/Infrastructure/Simulation.dll` | 143,360·400,896·438,272·382,976 | `d617d8d6…`·`52657170…`·`8062e1da…`·`8ae1521a…` | 동일 | |
| 같은 폴더 `StarPlayer.Client.deps.json`·`runtimeconfig.json` | 34,156·372 | `185bc9de…`·`d7a0beca…` | 동일 | |
| 같은 폴더 `GodotSharp.dll`, .NET 런타임·`e_sqlite3.dll` 등 | — | — | 동일 | 나머지 188개 |

- **.NET 출력 재현성:**
  - Export의 .NET 파일과 ExportRelease publish 출력 195개가 4회 모두 같습니다.
  - Export의 StarPlayer 어셈블리에는 Debug Directory에 `Reproducible` 항목만 있고 CodeView·PDB가 없습니다. 따라서 SourceLink도 없습니다.
- **패키지(`StarPlayer.pck`) 재현성:**
  - PCK 형식 4, Godot 4.7.2, 파일 18개
  - 14개는 같습니다: `project.binary`, `uid_cache.bin`, `.remap` 4개, 스크립트 자리 표시 파일 등.
  - **다른 4개는 export 때 `.tscn`에서 변환된 `.scn`입니다:** `Main`, `MainMenu`, `PlayerDiagnosticsScreen`, `GameDiagnosticsScreen`.
  - 각 장면 안에서 다른 부분은 **`node_ids` 배열뿐**입니다(4·56·52·91바이트). 배열 길이는 각 장면의 노드 수(1·14·13·23)와 같습니다.
  - PCK 목록의 해당 파일 MD5(16바이트×4)가 그에 맞게 바뀝니다. 다른 바이트 차이는 없습니다(전체 267바이트).
  - 원본 `.tscn` 4개(`format=3`)에는 `unique_id`가 없습니다.
  - **확인:** Export가 노드마다 실행 때마다 다른 ID를 만들어 넣습니다(같은 경로 반복에서도 다름).
  - **추정:** 원본에 저장된 ID가 없어 Godot 4.7이 변환 때 ID를 새로 만듭니다. 엔진 내부 생성 방식은 확인하지 않았습니다.
- **영향:**
  - 장면 노드 ID이며 SimHarness·게임 계산 경로와 무관합니다.
  - 이번 진단에서 Godot 실행 시험(verify G2~G4, E2~E4)은 하지 않았습니다.

## 7. 불변성 검증 (진단 빌드 V0 사용, 기존 기준본 덮어쓰지 않음)

| 항목 | 결과 |
|---|---|
| U1 Domain | 327 PASS / 0 / 0 |
| U2 Simulation | 741 PASS / 0 / 0 |
| U3 Application | 128 PASS / 0 / 0(U3a·U3b는 U3의 부분 집합이라 따로 실행하지 않음) |
| U4 Infrastructure | 1,237 PASS / 0 / 0 |
| U5 SimHarness(Golden 포함) | 538 PASS / 0 / 0 |
| U6 EvidenceVerifier | 70 PASS / 0 / 0(8분 56초) |
| 합계 | **3,041 PASS / 0 FAIL / 0 SKIP** |
| Golden 4개 | 4/4 PASS. Dominance·Integration·Calibration·Phase payload 바이트와 `a_plus`가 독립 Golden과 같음 |
| Dry Pilot 9 Run | **`ALL_9_IDENTICAL=True`**. ConfigHash·RunResultHash·환경 외 Manifest 9개 모두 보관본과 같음(DLL `84cb531f…`) |
| 실행 후 추적 변경 | 0 |

- GameResultHash·RNG 소비·Replay·Attestation 관련 검사는 위 U4~U6·Golden 시험과 Dry Pilot Manifest 비교에 포함된 범위에서 확인했습니다. 게임 계산식은 바꾸지 않았습니다.

## 8. Build Provenance 역할별 적용 범위 (§18.3, 진단 의견)

| 역할 | 대상 후보 | 이번 결과 | 남은 문제 |
|---|---|---|---|
| SIMHARNESS_BINARY | `tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.dll` | 경로 4개 동일 | 공식 URL·절차 미확정 |
| VERIFIER_BINARY | `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.EvidenceVerifier.dll` | 경로 4개 동일 | 같음 |
| TEST_ASSEMBLY | 시험 어셈블리 6개 | 경로 4개 동일 | 같은 출력 폴더의 coverage 대응표 6개는 경로에 따라 다름. artifact 대상에서 제외할지 OWNER 결정 필요 |
| DEPENDENCY | StarPlayer 라이브러리 4개, 외부 DLL 528개(NuGet·런타임) | 동일 | 어느 파일을 포함할지 목록 미정 |
| RUNTIME_CONFIG | `*.runtimeconfig.json` 18개(`*.deps.json` 22개 포함 여부 미정) | 동일 | 포함 범위 미정 |
| TRUST_CONFIG | 빌드 출력 아님 | 해당 없음 | — |

- **PDB:**
  - 위 역할에 넣지 않았습니다(OWNER 결정).
  - G3 진단 증거로 바이트와 SHA를 보존했습니다(10절).

## 9. B-4에 제안할 최소 빌드 절차 (제안, 승인·적용 아님)

1. **소스:** 승인 Commit의 깨끗한 체크아웃에서 빌드합니다.
   - `git rev-parse HEAD` = `source_git_sha`이어야 합니다.
   - 추적·미추적 변경이 0이어야 합니다.
   - 원격 `origin`이 승인된 공식 URL이어야 합니다.
2. **빌드:** `bin/`·`obj/`(Godot은 `game/.godot/`도)가 없는 상태에서 SDK 8.0.425로 빌드합니다.
   - 명령: `dotnet build StarPlayer.sln -c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false -p:ContinuousIntegrationBuild=true`
   - 환경 변수는 2절과 같습니다.
3. **출력 검사:**
   - 모든 StarPlayer 어셈블리의 CodeView·PDB 문서 경로가 `/_/`로 시작해야 합니다.
   - SourceLink 키가 `/_/*`, URL이 공식 값, Commit이 `source_git_sha`이어야 합니다.
4. **재현성:**
   - 경로가 다른 두 번째 깨끗한 체크아웃에서 같은 절차로 빌드해 artifact SHA가 같은지 확인합니다.
   - 비교 대상 파일 누락은 실패입니다.
   - coverage 대응표처럼 경로를 담는 도구 파일은 artifact에서 제외하거나 별도 규칙을 둡니다(OWNER 결정).
5. **기록:**
   - artifacts `raw_sha256`·`byte_length`를 기록합니다.
   - PDB 바이트·SHA는 별도 진단 증거로 보존합니다.
6. **게임 결과:** 바이너리 동일성과 별도로 U1~U6·Golden·Dry Pilot을 확인합니다.
7. **Godot Export:** 지금 상태로는 `StarPlayer.pck`가 재현되지 않습니다. 운영 배포 산출물 규칙은 따로 정해야 합니다.

## 10. PDB 진단 증거 (저장소 밖, Git에 추가하지 않음)

- **보존 위치:** `E:\Madegame\g3x\evidence\pdb_S1\`
  - S1의 1차 PDB 13개를 바이트 그대로 복사했습니다(상대 경로 유지).
  - SHA 목록 `pdb_sha256.txt`(`2b829052…`)와 모든 PDB 사본 55개 목록 `all_pdb_copies_sha256.txt`(`8126bd8a…`)가 함께 있습니다.
- 각 실행의 전체 출력 사본은 `E:\Madegame\g3x\full-out\<ID>\files\`에 있습니다(실행당 약 237 MB).

```
c46f26cfe3843ae2f81700bc1d747e5b8d6d4c3d343765baf8a6f42865b2b5cf  game/.godot/mono/temp/bin/Release/StarPlayer.Client.pdb
9ecb27ce82dee5794517a1935ffcb0d30e7821cd634f593e1c6e4f36fec11dc1  src/StarPlayer.Application/bin/Release/net8.0/StarPlayer.Application.pdb
75a3da7bdd00d18a781379d1e747686044869e27fc147810e57252e935247ed1  src/StarPlayer.Domain/bin/Release/net8.0/StarPlayer.Domain.pdb
254e12d6132b9664a43d398739e3428027c2116575ac75cf1be8606bf14207c1  src/StarPlayer.Infrastructure/bin/Release/net8.0/StarPlayer.Infrastructure.pdb
0e45d4e9cef7fdf70c8495b033f37b47fd6bf0de0f19dce9e019932eb09688e5  src/StarPlayer.Simulation/bin/Release/net8.0/StarPlayer.Simulation.pdb
19f396697c5744f9719889ab07dec4e54209ed87f25c9fce0537e8cc20db3a84  tests/StarPlayer.Application.Tests/bin/Release/net8.0/StarPlayer.Application.Tests.pdb
5933756886e1c38ce745645a7ee621a4a8461f725f6729f0b57d137590c129fb  tests/StarPlayer.Domain.Tests/bin/Release/net8.0/StarPlayer.Domain.Tests.pdb
499cde64299caf6f2d0b6a9b7953a1240731b528ce34c3c00fd1f81c8ebc6ae4  tests/StarPlayer.EvidenceVerifier.Tests/bin/Release/net8.0/StarPlayer.EvidenceVerifier.Tests.pdb
fa805db4e4786a9bd0cc342fece81c7148df5288c5aca994d3d5a1fa867ba193  tests/StarPlayer.Infrastructure.Tests/bin/Release/net8.0/StarPlayer.Infrastructure.Tests.pdb
71dfcc044d025d55f77246eb7192b02c7e3cfb634058e2efcce0abee02ded09d  tests/StarPlayer.SimHarness.Tests/bin/Release/net8.0/StarPlayer.SimHarness.Tests.pdb
01da68a564df982bae4f876ede314193665fc4824f241fdc70a5d78cde62e35c  tests/StarPlayer.Simulation.Tests/bin/Release/net8.0/StarPlayer.Simulation.Tests.pdb
5ab5cb4e4250cc1f5681ac55e220c1135b9c283db3df82afcb21cc249984188b  tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.EvidenceVerifier.pdb
44c99b6e3d819cb3a667faae16afc9922b49f63ffaa3936d8d95d44b370a7598  tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.pdb
```

## 11. OWNER가 정해야 할 값과 규범

| 항목 | 상태 |
|---|---|
| 공식 SourceLink 저장소 URL | **OWNER_VALUE_PENDING**. 실험값 `https://github.com/junk1901/Star_player.git`은 승인 값 아님 |
| 빌드 명령·환경 변수의 B-4 조항화(`build_procedure_clause_id`) | 미정. 9절 제안 |
| coverage 대응표(`.msCoverageSourceRootsMapping_*`) 처리 | 미정(artifact 제외 또는 별도 규칙) |
| DEPENDENCY·RUNTIME_CONFIG 포함 파일 목록(외부 DLL, `deps.json`) | 미정 |
| Godot Export 재현성 기준 | 미정. `.pck` 비결정성을 허용할지, 장면 `unique_id`를 소스에 저장하는 방안 등을 시험할지 정해야 함. 소스 변경이므로 별도 승인 대상 |
| `environment_profile`(빌드 기계·계정·경로 조건) | 미정 |

## 12. 미검증 운영 조건

- **다른 환경:** 다른 기계, 다른 Windows 계정·사용자 경로, 다른 NuGet 캐시 위치, 다른 SDK 설치 위치에서는 시험하지 않았습니다. coverage 대응표에는 `C:\Users\user\.nuget\packages\`가 들어 있어 사용자 경로에 의존합니다.
- **네트워크:** NuGet 캐시가 비어 있는 상태(실제 패키지 다운로드)의 Restore를 시험하지 않았습니다.
- **Godot 실행:** Godot 실행 시험(G2~G4, E2~E4 Cold-copy)을 진단 Export로 실행하지 않았습니다.
- **다른 Commit:** 같은 입력·다른 Commit은 1쌍만 시험했습니다(`7bdde7d`/`b17742a`).
- **운영 G3:** 운영 G3 수용 기준(기록된 SHA와 운영 재빌드 일치), 서명, Archive 보존은 하지 않았습니다. G1·G2·G4~G9는 착수하지 않았습니다.

## 13. 실험 도구·결과 (저장소 밖, Git에 추가하지 않음)

| 경로 | 내용 | SHA-256(앞 16자) |
|---|---|---|
| `E:\Madegame\g3x\tools\run_full.ps1` | 솔루션·Export 실행과 조건·출력 기록 | `7333b221617d63e3` |
| `E:\Madegame\g3x\tools\compare_full.py` | 파일별 비교·분류 | `240bc471d1ed30d9` |
| `E:\Madegame\g3x\tools\summarize_full.py`·`tables_full.py` | 요약·표·PDB 증거 복사 | `bb41f65812f9ca84`·`c3c2a735325a35e3` |
| `E:\Madegame\g3x\tools\pck_diff.py` | PCK 해석·파일별 비교 | `57f7c45b1a012611` |
| `E:\Madegame\g3x\tools\run_invariance.ps1` | U1~U6·Golden·Dry Pilot | `dc5e7278637a6fd4` |
| `E:\Madegame\g3x\tools\pedump\Program.cs` | DLL·PDB 진단 덤프(이전 실험과 같은 파일) | `946222f5161b45aa` |
| `E:\Madegame\g3x\full_compare_solution.json`·`full_compare_export.json`·`full_compare_v0.json` | 비교 전체 결과 | `c928e771891147df`·`447452d7a099f7ca`·`8ba57d4ed09c737f` |
| `E:\Madegame\g3x\full-out\V0_invariance.txt` | 불변성 검증 결과 | `fe8f31b4051dcab8` |

**재현 방법:**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File E:\Madegame\g3x\tools\run_full.ps1 -Clone E:\Madegame\g3x\fa -Id <ID> -Mode solution
powershell -NoProfile -ExecutionPolicy Bypass -File E:\Madegame\g3x\tools\run_full.ps1 -Clone E:\Madegame\g3x\fa -Id <ID> -Mode export
python -I E:\Madegame\g3x\tools\compare_full.py <out.json> S1:S2 X1:X2
```

Remote Push 없음. 운영 Gate G1~G9 미실행. Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
