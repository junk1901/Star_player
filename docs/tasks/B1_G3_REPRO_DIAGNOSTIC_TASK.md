# B1 G3 바이너리 재현성 통제 실험 (진단 빌드)

- 작성: Claude Code, 2026-10-26. 근거: OWNER 작업 지시 "B1 G3 바이너리 재현성 통제 실험"(2026-10-26, 채팅)
- **성격:**
  - 원인 조사용 진단 빌드입니다.
  - 정식 G3 운영 Gate 실행, Build Freeze, 운영 빌드 정책의 영구 적용이 **아닙니다**.
  - 실험 결과를 보고한 뒤 멈춥니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준 자료

| 자료 | 위치·값 |
|---|---|
| B1 코드 기준 Tag | `b1-code-accepted-2026-10-26` → `7bdde7dd091f9a66957bc740c77d470c7732b3ad`(이동·재생성 금지) |
| 실험 소스 Commit | **`7bdde7dd091f9a66957bc740c77d470c7732b3ad`**. Post-Tag 문서 Commit `03eb906…`은 실험 소스가 아니며 Tag의 일부도 아님 |
| 사전 조사 | [B1_G3_BUILD_REPRODUCIBILITY_INVESTIGATION.md](../reports/B1_G3_BUILD_REPRODUCIBILITY_INVESTIGATION.md)(기존 6개 빌드, 읽기 전용) |
| 운영 Gate 계획 | [B1_OPERATIONAL_GATE_PLAN.md](B1_OPERATIONAL_GATE_PLAN.md) G3: 깨끗한 체크아웃에서 Release 재빌드한 바이너리 SHA가 기록과 같아야 함 |
| 승인 계약 §18.3 | `simharness-build-provenance-v1`: `source_git_sha`(= 승인 기록 Commit), `dotnet_sdk: "8.0.425"`, `configuration: "Release"`, `build_procedure_clause_id`(B-4 승인 조항), artifacts의 `raw_sha256`·`byte_length` |
| 빌드 설정 | `global.json`(SDK 8.0.425, `rollForward: disable`), `Directory.Build.props`(`Deterministic=true`, `IncludeSourceRevisionInInformationalVersion=false`), `tools/StarPlayer.SimHarness/StarPlayer.SimHarness.csproj`(EmbeddedResource 2개), `.gitattributes`(내장 입력 JSON `-text`) |

## 2. 공통 조건

- **실험 폴더:**
  - `E:\Madegame\g3x\` 아래에 따로 만든 clone에서만 빌드합니다(`git clone --no-hardlinks` 후 `7bdde7d` 분리 체크아웃).
  - 저장소 추적 파일(코드·설정)은 바꾸지 않습니다.
  - 빌드 속성은 명령행 `-p:`로만 줍니다.
  - Git 원격 변경은 실험 E의 clone 설정(`.git/config`)에서만 합니다. Push·Fetch는 하지 않습니다.
- **빌드 명령:**
  - `dotnet build tools/StarPlayer.SimHarness/StarPlayer.SimHarness.csproj -c Release --disable-build-servers -nr:false [-p:…]`
  - 환경 변수는 `tools/verify.ps1`과 같습니다: `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `UseSharedCompilation=false`, `DOTNET_CLI_UI_LANGUAGE=en`.
  - 빌드 대상은 SimHarness 프로젝트와 그 참조(Domain, Simulation)입니다.
- **캐시 분리:** 빌드 전마다 clone 안의 모든 `bin/`·`obj/`를 지웁니다. Restore도 다시 합니다.
- **산출물 보존:** 각 빌드의 DLL·PDB를 `E:\Madegame\g3x\out\<실험ID>\`에 복사합니다. `build/`와 실험 폴더는 Git에 추가하지 않습니다.
- **실험마다 기록하는 항목:**
  - Commit, 작업 트리 상태, SDK·컴파일러, Restore 의존성(`project.assets.json`)
  - 명령·속성, 환경 변수
  - 소스·EmbeddedResource SHA, SourceLink·원격 저장소
  - DLL·PDB SHA·크기, MVID, Debug Directory, IL·메타데이터 해시, PDB 문서 경로
- **분석 도구:**
  - 실험 폴더 밖 저장소에 넣지 않는 진단용 C# 도구(`System.Reflection.Metadata`)와 Python 비교 스크립트를 씁니다.
  - 차이 바이트는 다음 범주로 분류합니다: 소스 경로, SourceLink, Commit/Version 메타데이터, MVID/PDB 식별자, IL, EmbeddedResource, 미분류. 확정하지 못한 원인은 추정으로 표시합니다.

## 3. 실험

| ID | 내용 | 바꾸는 변수 |
|---|---|---|
| A | 같은 clone·경로에서 같은 조건으로 두 번 빌드(빌드마다 `bin/obj` 삭제) | 없음(반복) |
| B | 기본 설정, 다른 경로: 길이가 같은 경로 쌍과 다른 쌍 | 빌드 경로 |
| C | B와 같은 경로에서 `-p:ContinuousIntegrationBuild=true`(`Deterministic` 유지) | CI 속성 |
| D | 다른 경로에서 `PathMap`으로 같은 가상 루트(`/_/`) 지정. 미적용·적용·CI 함께 적용 비교 | PathMap(+CI) |
| E | SourceLink 분리: 원격 URL(로컬 경로/GitHub URL), `EnableSourceLink=false`(원인 분석용) | 원격 URL, SourceLink |

## 4. 판정 항목 (개별 판정)

1. 같은 경로 반복 빌드의 바이트 동일 여부
2. 다른 경로 기본 빌드의 바이트 동일 여부
3. ContinuousIntegrationBuild 효과
4. PathMap 효과
5. SourceLink 차이의 영향
6. 실제 Commit 일치 여부
7. IL·게임 입력 바이트 불변 여부
8. 공식 운영 빌드에 권장할 최소 설정(제안만)
9. 남은 비결정적 출력 또는 미확인 원인

## 5. 산출물과 보존

- 보고서: `docs/reports/B1_G3_REPRO_EXPERIMENT_REPORT.md`. 기존 조사 보고서는 별도 이력으로 둡니다.
- **보존 Commit(필요한 경우):**
  - 보고서·이 TASK·기존 조사 보고서만 대상입니다.
  - 민감정보 검사, D1, D3, 변경 범위를 확인한 뒤 별도의 문서 전용 로컬 Commit으로 만듭니다.
- **금지:**
  - B1 기준 Tag, 구현 Manifest, 게임 코드·계산식·RNG·Replay, 시험, Golden, 승인 계약, OWNER 원문, 루트 `TASK.md` 변경
  - 빌드 정책 영구 적용, 정식 G3 승인, Build Freeze 선언, G1·G2·G4~G9 착수
  - Remote Push, Official Execution 활성화
