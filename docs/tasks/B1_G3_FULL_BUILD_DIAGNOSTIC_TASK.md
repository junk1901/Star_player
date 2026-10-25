# B1 G3 전체 빌드 재현성 확장 진단

- 작성: Claude Code, 2026-10-26. 근거: OWNER 지시 "G3 전체 빌드 재현성 확장 진단"(2026-10-26, 채팅)
- **성격:**
  - 진단 범위 승인입니다.
  - B-4 공식 빌드 절차 확정, 운영 G3 PASS, 공식 Build Provenance 등록, Build Freeze가 **아닙니다**.
  - 결과를 제출하고 멈춥니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 재개용 기준

| 항목 | 값 |
|---|---|
| B1 코드 기준 Tag | `b1-code-accepted-2026-10-26` → `7bdde7dd091f9a66957bc740c77d470c7732b3ad`(이동·삭제·재생성 금지) |
| 실험 소스 Commit | `7bdde7dd091f9a66957bc740c77d470c7732b3ad` |
| 실험 C 비교 Commit | `b17742aa13161937e150400415548ca758f122e5`(최신 Post-Tag 문서 Commit). `7bdde7d`와 다른 파일은 `docs/` 아래 12개뿐이며 빌드 입력 아님(`git diff --name-only` 확인) |
| 사전 결과 | [B1_G3_BUILD_REPRODUCIBILITY_INVESTIGATION.md](../reports/B1_G3_BUILD_REPRODUCIBILITY_INVESTIGATION.md)(읽기 전용 조사), [B1_G3_REPRO_EXPERIMENT_REPORT.md](../reports/B1_G3_REPRO_EXPERIMENT_REPORT.md)(SimHarness 22회) |
| 이번 보고서 | `docs/reports/B1_G3_FULL_BUILD_DIAGNOSTIC_REPORT.md` |
| 실험 위치(저장소 밖) | `E:\Madegame\g3x\`: clone, `tools\`(실행·분석 스크립트), `full-out\<ID>\`(출력 사본·기록) |

**지금까지 확인된 사실**(SimHarness·Domain·Simulation, [실험 보고서](../reports/B1_G3_REPRO_EXPERIMENT_REPORT.md) 5절):

- 같은 경로·같은 조건 반복은 바이트 동일입니다.
- 기본 빌드는 절대 경로 때문에 경로마다 다릅니다.
- `ContinuousIntegrationBuild=true` 또는 `PathMap`이면 경로 3개에서 DLL·PDB가 동일합니다.
- SourceLink는 PDB 내용과 DLL 식별자만 바꿉니다.
- IL·EmbeddedResource는 불변입니다.

**남은 검증 항목**(이번 범위):

- 전체 솔루션 프로젝트: EvidenceVerifier, 시험 어셈블리 6개, Application, Infrastructure, Godot Client, `deps.json`·`runtimeconfig.json`·apphost
- 다른 Commit(같은 입력)의 출처 차이
- SourceLink 정규 URL과 로컬 경로 URL 비교
- Godot Export 패키지
- 진단 빌드로 게임 결과 불변 확인

## 2. OWNER 정책 방향 (진단 기준, 확정 아님)

- 재현성 기본 후보는 `ContinuousIntegrationBuild=true`, 대안은 `PathMap`입니다.
- 속성은 진단 명령에서만 줍니다. `Directory.Build.props`는 바꾸지 않습니다.
- **SourceLink:**
  - 유지합니다. SourceLink Commit은 실제 빌드 소스 Commit과 같아야 합니다.
  - 공식 URL은 OWNER가 확정할 신뢰된 정식 저장소 주소입니다. 아직 승인 값이 없으므로 **OWNER_VALUE_PENDING**입니다.
  - 로컬 파일 경로 원격은 정식 원격으로 인정하지 않습니다.
- **실험 통제 URL:**
  - 원본 작업 트리 `origin`에 실제로 설정된 `https://github.com/junk1901/Star_player.git`을 씁니다. 인증 정보는 없으며, 승인된 공식 값이 아닙니다.
  - 실험 clone의 `.git/config`에만 설정합니다. Fetch·Push는 하지 않습니다.
- **PDB:** G3 진단 증거로 바이트와 SHA를 보존합니다. Build Provenance(§18.3)의 닫힌 artifact 역할에는 추가하지 않습니다.

## 3. 통제 조건 (모든 빌드)

- **Commit:** 같은 Commit(실험 C만 예외로 Commit을 바꿈). 빌드 전 HEAD 확인과 깨끗한 작업 트리(`git status --porcelain --untracked-files=all` 0행)를 확인합니다.
- **SDK·도구:** SDK 8.0.425(`global.json`), 같은 컴파일러
- **캐시:** 빌드 전에 clone 안의 모든 `bin/`·`obj/`와 `game/.godot/`를 지웁니다. Restore도 다시 합니다.
- **솔루션 빌드:** `dotnet build StarPlayer.sln -c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false -p:ContinuousIntegrationBuild=true`
  - verify B1 명령에 CI 속성만 더한 것입니다. `Deterministic=true`는 기존 설정입니다.
- **환경 변수:** `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `UseSharedCompilation=false`, `DOTNET_CLI_UI_LANGUAGE=en`, `DOTNET_NOLOGO=1`, `DOTNET_CLI_TELEMETRY_OPTOUT=1`
- **Godot Export:**
  - verify G1·E1과 같은 명령: `godot --headless --path game --build-solutions --quit`, 그다음 `--export-release "Windows Desktop" ../build/export/StarPlayer.exe`
  - Godot 내부 `dotnet` 호출에는 환경 변수 `ContinuousIntegrationBuild=true`(MSBuild가 속성으로 읽음)로 CI를 전달합니다.
  - Godot 4.7.2.stable.mono, Export Template 5개(verify T3의 SHA)를 사용합니다.

## 4. 실험

| ID | 내용 | 바꾸는 변수 |
|---|---|---|
| S-A | 같은 clone에서 솔루션 CI 빌드 2회 | 없음(반복) |
| S-B | 다른 clone(경로 길이 같음·다름)에서 솔루션 CI 빌드 | 경로 |
| S-C | 같은 경로에서 Commit만 `b17742a`(입력 동일)로 바꿔 빌드 | Commit → SourceLink(출처 차이로 분류, 재현성 결함 아님) |
| S-D | 같은 경로에서 원격을 로컬 경로로 바꿔 빌드 | SourceLink URL(없음) |
| X | Godot Export: 같은 경로 2회, 다른 경로 | 반복·경로 |
| V | 진단 빌드로 U1~U6 시험, Golden, Dry Pilot 9개 재실행. 보관 결과와 비교 | — |

- **판정:**
  - 같은 Commit·같은 입력·같은 정책에서 생긴 차이만 재현성 결함으로 봅니다.
  - 비교 대상 파일이 없으면 실패입니다.
  - 파일별로 상대 경로, 크기, SHA-256을 기록합니다. .NET 어셈블리는 MVID, IL 해시, SourceLink Commit도 기록합니다.

## 5. 금지

- 게임 코드·시험 수정, `Directory.Build.props` 영구 변경, `tools/verify.ps1` 정책 변경
- 계약·OWNER 원문·루트 `TASK.md` 변경
- 공식 Build Provenance 등록, G3 PASS 선언, G1·G2·G4~G9 착수, Godot Export의 운영 산출물 동결
- B1 Tag 변경, Remote Push
- 진단 DLL·PDB·임시 빌드 폴더는 Git에 넣지 않습니다. 진단 문서만 검사를 거쳐 별도 로컬 Commit으로 보존할 수 있습니다.
