# B1 G3 바이너리 재현성 통제 실험 결과

- 작성: Claude Code, 2026-10-26. 근거: OWNER 작업 지시 "B1 G3 바이너리 재현성 통제 실험"(2026-10-26), [TASK](../tasks/B1_G3_REPRO_DIAGNOSTIC_TASK.md)
- **성격:**
  - 원인 조사용 진단 빌드입니다.
  - 정식 G3 운영 Gate 실행, Build Freeze, 운영 빌드 정책의 영구 적용이 **아닙니다**.
  - 저장소의 코드·설정·Tag는 바꾸지 않았습니다.
- **이전 이력:** 사전 조사는 [B1_G3_BUILD_REPRODUCIBILITY_INVESTIGATION.md](B1_G3_BUILD_REPRODUCIBILITY_INVESTIGATION.md)(기존 빌드 6개, 읽기 전용)에 따로 둡니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 공통 조건

| 항목 | 값 |
|---|---|
| 실험 소스 Commit | **`7bdde7dd091f9a66957bc740c77d470c7732b3ad`**(Tag `b1-code-accepted-2026-10-26`) |
| 문서 Commit `03eb906…` | 실험 소스가 아님. 각 clone은 `7bdde7d`로 분리 체크아웃 |
| 실험 clone | `git clone --no-hardlinks E:/Madegame/Star_player` → `git checkout --detach 7bdde7d` |
| clone 위치 | `E:\Madegame\g3x\c1`, `E:\Madegame\g3x\c2`(c1과 경로 길이 같음), `E:\Madegame\g3x\c3long`(4자 김) |
| 작업 트리 | 22회 빌드 모두 빌드 전 `git status --porcelain --untracked-files=all` 0행(스크립트가 검사, 아니면 중단). 빌드 후에도 0행 |
| SDK | 8.0.425(`dotnet --version`, `global.json` 고정), 22회 모두 같음 |
| 컴파일러·런타임 | Roslyn `4.11.0-3.25569.22+3fb752d4…`, 런타임 `8.0.31`(PDB 컴파일 옵션 기록). 컴파일 옵션 내용은 모든 빌드에서 같음 |
| Restore 의존성 | NuGet 패키지 없음. `project.assets.json`의 libraries = `StarPlayer.Domain/0.0.0-phase0`, `StarPlayer.Simulation/0.0.0-phase0`(프로젝트 참조) |
| 빌드 명령 | `dotnet build tools\StarPlayer.SimHarness\StarPlayer.SimHarness.csproj -c Release --disable-build-servers -nr:false [-p:…]` |
| 환경 변수 | `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `UseSharedCompilation=false`, `DOTNET_CLI_UI_LANGUAGE=en`, `DOTNET_NOLOGO=1`, `DOTNET_CLI_TELEMETRY_OPTOUT=1` |
| 소스 트리(Git tree id) | SimHarness `3270019c…`, Domain `41ddb08f…`, Simulation `8110e0ad…`. 22회 모두 같음 |
| 빌드 설정 파일 | `Directory.Build.props` `9e981e84…`(`Deterministic=true`), `global.json` `97b37563…`. 22회 모두 같음 |
| EmbeddedResource 입력 | `virtual_tuning_v4.json` `992d7e24…`, `game_rules_profiles_v2.json` `b016b37b…`. 22회 모두 같음 |
| 캐시 분리 | 빌드 전마다 clone 안의 모든 `bin/`·`obj/` 삭제. clone의 첫 빌드는 지울 것이 없음(0개), 이후 빌드는 6개 삭제 |
| 결과 | 22회 모두 exit 0, 경고 0, 각 약 7초 |

**참고 사항:**

- `project.assets.json`은 clone마다 SHA가 다릅니다(값 3개). 프로젝트 절대 경로를 담기 때문이며, libraries는 같습니다.
- 빌드 대상은 SimHarness와 그 참조(Domain, Simulation)뿐입니다. 13개 프로젝트 전체, 검증기·시험 어셈블리, Godot Export는 이번 범위가 아닙니다(8절).
- **실행 스크립트:** A1·A2를 실행한 뒤 `run_build.ps1`의 속성 인자 형식을 고쳤습니다. 배열 인자가 `powershell -File`에서 전달되지 않아 B·C·D 첫 실행이 인자 오류로 빌드 전에 멈췄기 때문입니다. 그 시도에서는 출력이 생기지 않았습니다. 빌드 절차 자체는 바꾸지 않았습니다.

## 2. 실험 결과

**각 실험에서 바꾼 변수:**

- A: 반복(없음)
- B: 경로
- C: `ContinuousIntegrationBuild=true`
- D: `PathMap=<clone>\=/_/`(+CI)
- E: Git 원격 URL(clone의 `.git/config`만, Fetch·Push 없음)과 `EnableSourceLink=false`

| ID | 경로 | 원격 | 속성 | SimHarness DLL | PDB | MVID | IL | 기록된 경로 | SourceLink |
|---|---|---|---|---|---|---|---|---|---|
| A1 | c1 | 로컬 | — | `fbe36ac5…` | `c97700f6…` | `4f38663f` | `f40f4044` | 절대 경로 | 없음 |
| A2 | c1 | 로컬 | — | `fbe36ac5…` | `c97700f6…` | `4f38663f` | `f40f4044` | 절대 경로 | 없음 |
| B2 | c2 | 로컬 | — | `c76447c4…` | `b7cdca0d…` | `1fb342ff` | `f40f4044` | 절대 경로 | 없음 |
| B3 | c3long | 로컬 | — | `c842734d…` | `613027bb…` | `5920d1c3` | `f40f4044` | 절대 경로 | 없음 |
| C1·C2·C3 | c1·c2·c3long | 로컬 | CI | `b26f18fc…` | `a14ad876…` | `15b1d38d` | `f40f4044` | `/_/…` | 없음 |
| D1·D2·D3 | c1·c2·c3long | 로컬 | PathMap | `b26f18fc…` | `a14ad876…` | `15b1d38d` | `f40f4044` | `/_/…` | 없음 |
| DC1·DC2·DC3 | c1·c2·c3long | 로컬 | CI + PathMap | `b26f18fc…` | `a14ad876…` | `15b1d38d` | `f40f4044` | `/_/…` | 없음 |
| E1·E1r | c1 | GitHub | — | `4556eed2…` | `1c1106e2…` | `4a36bd9a` | `f40f4044` | 절대 경로 | `E:\Madegame\g3x\c1\*` → `…/7bdde7dd…/*` |
| E2 | c2 | GitHub | — | `6958bfd2…` | `6094a5db…` | `d4c9988f` | `f40f4044` | 절대 경로 | `E:\Madegame\g3x\c2\*` → `…/7bdde7dd…/*` |
| EC1·EC2·EC3 | c1·c2·c3long | GitHub | CI | `84cb531f…` | `44c99b6e…` | `4420ec59` | `f40f4044` | `/_/…` | `/_/*` → `…/7bdde7dd…/*` |
| ES1 | c1 | GitHub | `EnableSourceLink=false` | `fbe36ac5…` | `c97700f6…` | `4f38663f` | `f40f4044` | 절대 경로 | 없음 |
| ESC1·ESC3 | c1·c3long | GitHub | CI + `EnableSourceLink=false` | `b26f18fc…` | `a14ad876…` | `15b1d38d` | `f40f4044` | `/_/…` | 없음 |

- SourceLink URL의 앞부분은 모두 `https://raw.githubusercontent.com/junk1901/Star_player/`입니다.
- **함께 빌드된 DLL:** `StarPlayer.Domain.dll`·`StarPlayer.Simulation.dll`도 같은 묶음끼리 SHA가 같고, 묶음이 다르면 다릅니다.
  - A1·A2·ES1: `cb4151bc…`·`5a069581…`
  - C·D·DC·ESC: `706ec4d0…`·`c0939ef8…`
  - EC: `e0023984…`·`6296c9fc…`
- **변하지 않은 값(22회 모두):**
  - IL 해시 `f40f4044…`(메서드 본문 4,398개)
  - EmbeddedResource 2개(위 입력 SHA와 같음)
  - Assembly 속성(`InformationalVersion` `0.0.0-phase0`, `AssemblyVersion`·`FileVersion` `0.0.0.0`)
  - 파일 크기 906,752 B
- **MVID 외 메타데이터 해시:**
  - 절대 경로 빌드는 `94209710…`, `/_/` 경로 빌드는 `691b53e2…`입니다.
  - 차이는 FieldRVA 표 9바이트뿐입니다(3절, 배치 이동).

## 3. 비교와 바이트 분류

**분류 방법:**

- 진단 도구가 DLL·PDB 각 영역의 파일 오프셋을 기록합니다.
- 다른 바이트마다 그 바이트를 포함하는 가장 작은 영역으로 분류했습니다. 범주는 소스 경로, SourceLink, Commit/Version 메타데이터, MVID/PDB 식별자, IL, EmbeddedResource, 배치(RVA·위치), 미분류입니다.
- 앞쪽 길이가 달라져 뒤가 밀린 PDB는 바이트 분류가 의미가 적습니다. 그래서 의미 비교를 함께 적습니다.

| 비교 | DLL | PDB | 의미 차이 |
|---|---|---|---|
| A1 ↔ A2(같은 경로 반복) | **동일** | **동일** | 없음 |
| A1 ↔ B2(경로 다름, 길이 같음) | 73 B: MVID/PDB 식별자 72(PdbChecksum 32, MVID 16, CodeView GUID 16, COFF 시각 4, CodeView stamp 4), 소스 경로 1 | 21 B: PDB ID 20, 문서 경로 1 | 경로 문자 `c1`→`c2`, 그로부터 계산되는 식별자 |
| A1 ↔ B3(경로 길이 다름) | 212 B: 소스 경로 72, 식별자 80, 배치 53(import directory 45, entry stub 6, IAT 1, reloc 1), 미분류 7(PE 헤더 2, `.text` 3, PdbChecksum 항목 2) | 57,582 B(밀림) | 경로, 식별자. 컴파일 옵션·참조 목록 내용은 같음 |
| A1 ↔ C1(CI 적용) | 461 B: 소스 경로 92, 식별자 80, 배치 278(mapped field data 210, import directory 49, FieldRVA 표 9, entry stub 8, IAT 1, reloc 1), 미분류 11 | 55,028 B(밀림) | 경로가 `/_/…`로 바뀜, 식별자. 컴파일 옵션 같음 |
| C1 ↔ C2 ↔ C3(CI, 경로 3개) | **동일** | **동일** | 없음 |
| A1 ↔ D1(PathMap) | A1↔C1과 같음 | 같음 | 경로 `/_/…` |
| C1 ↔ D1 ↔ DC1, D1 ↔ D3 | **동일** | **동일** | 없음 |
| A1 ↔ E1(같은 경로, 원격만 GitHub) | 72 B: 모두 MVID/PDB 식별자 | 58,304 B(SourceLink JSON 추가로 밀림) | PDB에 SourceLink 추가 |
| E1 ↔ E1r(반복) | **동일** | **동일** | 없음 |
| E1 ↔ E2(SourceLink 포함, 경로 다름) | 73 B: 식별자 72, 소스 경로 1 | 22 B: PDB ID 20, 문서 경로 1, SourceLink JSON 1 | SourceLink의 로컬 경로 키(`c1`/`c2`) |
| A1 ↔ ES1(GitHub 원격 + SourceLink 끔) | **동일** | **동일** | 없음 |
| C1 ↔ EC1(CI, SourceLink 유무) | 72 B: 모두 MVID/PDB 식별자 | 58,916 B(밀림) | PDB에 SourceLink(`/_/*` → 실제 Commit) |
| EC1 ↔ EC2 ↔ EC3(CI + SourceLink, 경로 3개) | **동일** | **동일** | 없음 |
| C1 ↔ ESC1, ESC1 ↔ ESC3 | **동일** | **동일** | 없음 |

**확인된 연결 관계:**

- **CodeView = PDB ID:** DLL의 CodeView GUID·stamp는 PDB `#Pdb` 스트림의 PDB ID 20바이트와 같습니다(모든 빌드).
- **참조 MVID:** SimHarness PDB의 컴파일 참조 기록에는 Domain·Simulation MVID가 있습니다(`000b9bec…`·`b9a448ff…`).
  - 이 값은 22회 모두 같습니다. 실제 Domain·Simulation DLL의 MVID는 조건마다 다릅니다.
  - 그 값은 `obj/Release/net8.0/ref/`의 **참조 어셈블리** MVID와 같습니다(c1에서 확인).
  - 즉 SimHarness는 경로 정보가 없는 참조 어셈블리로 컴파일되므로, 의존 DLL의 경로 차이가 SimHarness IL·메타데이터에 전파되지 않습니다.
- **배치 이동:** CI·PathMap은 CodeView 경로를 짧게 합니다(`/_/…`). 그래서 그 뒤의 mapped field data가 16바이트 앞으로 옮겨집니다(오프셋 904,312 → 904,296).
  - 이 데이터를 가리키는 FieldRVA 표 9바이트와 import·entry stub·reloc의 RVA가 바뀝니다.
  - B3처럼 경로가 4자 길어진 경우에는 정렬 여백 안에서 흡수되어 FieldRVA가 바뀌지 않았습니다.

## 4. 원인 분석

| 범주 | 판정 | 근거 |
|---|---|---|
| 소스 경로 | **확인** | 기본 빌드는 DLL CodeView에 PDB 절대 경로, PDB 문서 이름에 소스 절대 경로를 기록합니다. 경로만 다른 clone(A1↔B2)에서 입력처럼 보이는 차이는 경로 문자뿐이고, CI·PathMap으로 경로를 `/_/`로 바꾸면 세 경로(길이 2종)에서 바이트가 같아집니다 |
| MVID/PDB 식별자 | **확인(파생 값)** | 같은 입력이면 같고(A·C·D·E 반복과 경로 정규화 묶음), 경로나 SourceLink가 바뀔 때만 바뀝니다. PDB ID = CodeView GUID·stamp. Roslyn Deterministic 모드가 내용 해시로 이 값들을 만든다는 설명과 일치합니다 |
| SourceLink | **확인** | 같은 경로에서 원격만 GitHub으로 바꾸면 PDB에 SourceLink가 추가되어 DLL 식별자 72바이트가 바뀝니다(A1↔E1). 끄면 로컬 원격 결과와 바이트가 같습니다(A1=ES1) |
| SourceLink의 경로 키 | **확인** | CI 없이 SourceLink가 들어가면 키가 로컬 절대 경로(`E:\Madegame\g3x\c1\*`)라서 경로마다 다릅니다(E1↔E2). CI에서는 `/_/*`라 같습니다(EC1=EC2=EC3) |
| Commit/Version 메타데이터 | **확인** | Assembly 속성에는 Commit 정보가 없습니다(`IncludeSourceRevisionInInformationalVersion=false`). Commit은 SourceLink URL에만 들어가며, 이번 빌드에서는 실제 소스 Commit `7bdde7dd…`와 같았습니다 |
| Commit에 따라 바이너리가 달라지는지 | **추정** | SourceLink URL에 Commit SHA가 들어가므로, 같은 소스라도 Commit이 다르면 PDB와 DLL 식별자가 달라질 것입니다. 다른 Commit으로 빌드하지 않아(실험 소스를 `7bdde7d`로 고정) 실측하지 않았습니다 |
| 컴파일된 IL | **불변 확인** | 22회 모두 같음 |
| EmbeddedResource | **불변 확인** | 22회 모두 같고, 입력 파일 바이트와 같음 |
| 배치 이동(FieldRVA·import·stub·reloc) | **확인** | 3절 |
| 미분류 소량 | **추정** | PE 헤더 2~3, `.text` 3~6, PdbChecksum 항목 2바이트(길이가 다른 쌍에서만). Data Directory·import lookup·Debug 항목의 위치 값으로 보이나, 필드 단위로 해석하지 않았습니다 |

**사전 조사의 SourceLink 불일치 원인(확인):**

- 원본 작업 트리 빌드(2026-10-25 19:04)의 SourceLink는 `257403dd…`를 가리켰습니다. 그런데 실제로 빌드한 B1 소스는 그때 커밋되지 않았습니다(`c15360c`는 2026-10-26 00:23).
- SourceLink는 **HEAD Commit**을 기록하고 작업 트리의 미커밋 변경은 반영하지 않습니다. 해당 `sourcelink.json`은 2026-10-24 14:28 생성 뒤 HEAD가 바뀌지 않아 그대로 쓰였습니다.
- 원인은 **커밋되지 않은 작업 트리에서 빌드한 것**입니다.
- 이번 실험처럼 깨끗한 체크아웃(HEAD = 실제 소스)에서 빌드하면 SourceLink Commit이 실제 소스와 같습니다.

## 5. 판정 (개별)

| # | 항목 | 판정 |
|---:|---|---|
| 1 | 같은 경로 반복 빌드 | **바이트 동일**. 같은 조건 반복 2쌍(A1=A2, E1=E1r). 이와 별도로 같은 경로 c1에서 다른 조건(C1·D1·DC1·ESC1)이 같은 바이트를 냄. 이번 22회에서 비결정적 출력은 관찰되지 않음 |
| 2 | 다른 경로 기본 빌드 | **다름**. 원인은 절대 경로와 그 파생 식별자(4절) |
| 3 | ContinuousIntegrationBuild | **효과 있음**. DLL CodeView와 PDB 문서 경로가 `/_/…`로 정규화됐고(실제 기록값 확인), 경로 3개(길이 2종)에서 DLL·PDB가 동일. 원격이 로컬 경로여도 정규화됨 |
| 4 | PathMap | **효과 있음**. `PathMap=<clone>\=/_/`만으로 CI와 바이트까지 같은 결과. CI와 함께 써도 같음. 실제 기록 경로가 `/_/…`임을 확인 |
| 5 | SourceLink 영향 | PDB 내용과 DLL 식별자 72바이트를 바꿈. IL·리소스는 불변. CI가 없으면 SourceLink 키에 절대 경로가 남아 경로마다 달라짐. CI가 있으면 경로와 무관하게 동일 |
| 6 | 실제 Commit 일치 | 이번 실험: SourceLink Commit = `7bdde7dd…` = 실제 소스(깨끗한 체크아웃). 사전 조사의 원본 작업 트리 빌드는 불일치(미커밋 소스). Assembly 속성에는 Commit 없음 |
| 7 | IL·게임 입력 바이트 | **불변**. IL·EmbeddedResource·소스 트리·빌드 설정 22회 동일 |
| 8 | 권장 최소 설정 | 6절(제안만) |
| 9 | 남은 미확인 | 8절 |

**게임 결과의 별도 확인(바이너리 동일성과 구분):**

- c1에서 CI + SourceLink 끔 출력(DLL `b26f18fc…`, ESC1과 같은 바이트)으로 `tools/b1-drypilot-repro.ps1`을 실행했습니다(보관 Seed·Manifest는 원본 작업 트리에서 복사).
- 결과: **`ALL_9_IDENTICAL=True`**. ConfigHash·RunResultHash·환경 외 Manifest 9개 모두 보관본과 같습니다.
- 다른 실험 출력으로는 Dry Pilot을 실행하지 않았습니다. IL이 같다는 사실과 이 1회 실행이 근거입니다. 단위 시험(U1~U6)은 실험 빌드로 실행하지 않았습니다.

## 6. B-4·G3 빌드 절차 제안 (승인 아님, 적용하지 않음)

**최소 설정 후보:**

- **경로 정규화:** `ContinuousIntegrationBuild=true`(Deterministic 유지).
  - 이번 실험에서 원격 종류와 경로에 관계없이 정규화가 됐고, SourceLink 키도 `/_/*`가 됩니다.
  - 대안은 `PathMap=<checkout root>\=/_/`입니다. 결과 바이트는 같았지만, 루트 경로를 명령에 정확히 넣어야 하고 SourceLink 키는 정규화하지 않습니다(SourceLink를 쓰는 경우 CI가 필요).
- **SourceLink:** 유지 여부는 OWNER 결정 사항입니다. 이번 지시로 제거하지 않습니다.
  - 유지하면 공식 원격 URL을 고정해야 합니다. 원격 URL이 바이트에 들어가기 때문입니다(로컬 경로 원격이면 SourceLink 없음).
  - 또 빌드 후 SourceLink Commit = `source_git_sha`인지 대조해야 합니다.

**절차 후보(§18.3 `simharness-build-provenance-v1`과 연결):**

1. 승인 Commit을 깨끗한 체크아웃으로 받습니다. 빌드 전에 `git rev-parse HEAD` = `source_git_sha`이고 `git status --porcelain --untracked-files=all`이 비어 있어야 합니다(미커밋 소스 빌드 금지 — SourceLink 불일치 원인).
2. `bin/`·`obj/`가 없는 상태에서 SDK 8.0.425(`global.json`)로 Release 빌드합니다. 정규화 속성은 명령에 고정합니다.
3. 정규화가 실제로 적용됐는지 출력에서 검사합니다. 속성 지정만으로 성공을 가정하지 않습니다.
   - DLL CodeView 경로와 PDB 문서 경로가 `/_/`로 시작
   - SourceLink(있다면) 키 `/_/*`, Commit = `source_git_sha`, 원격 URL = 공식 값
4. DLL·PDB SHA와 바이트 수를 `artifacts[]`에 기록합니다. 다른 경로의 두 번째 깨끗한 체크아웃에서 같은 SHA가 나오는지 재현성을 확인합니다(G3 수용 기준).
5. 바이너리 동일성과 별도로 게임 결과 불변(Dry Pilot·Golden)을 검증합니다.

**정할 것:**

- 원격 URL·SourceLink 정책
- 정규화 속성을 명령행에 둘지, `Directory.Build.props`에 둘지(영구 설정 변경이므로 OWNER 결정 필요)
- PDB를 Provenance artifact로 고정할지
- 13개 프로젝트 전체와 Godot Export에 같은 규칙을 적용할지

## 7. 재현 방법

```powershell
git clone --no-hardlinks E:/Madegame/Star_player E:\Madegame\g3x\c1
git -C E:\Madegame\g3x\c1 checkout --detach 7bdde7dd091f9a66957bc740c77d470c7732b3ad
powershell -NoProfile -ExecutionPolicy Bypass -File E:\Madegame\g3x\tools\run_build.ps1 -Clone E:\Madegame\g3x\c1 -Id <ID> [-PropsText "ContinuousIntegrationBuild=true|PathMap=E:\Madegame\g3x\c1\=/_/"]
python -I E:\Madegame\g3x\tools\compare.py E:\Madegame\g3x\out_compare.json
```

- `run_build.ps1`이 하는 일:
  1. HEAD = `7bdde7d`이고 작업 트리가 깨끗한지 검사합니다.
  2. 모든 `bin/obj`를 지우고 빌드합니다.
  3. DLL·PDB를 `E:\Madegame\g3x\out\<ID>\`에 복사합니다.
  4. 진단 도구(`pedump`)로 `dump_*.json`을, 조건을 `meta.json`으로 기록합니다.
- 실험 E는 빌드 전에 clone의 `origin`을 `https://github.com/junk1901/Star_player.git`으로 바꾸고, 끝난 뒤 로컬 경로로 되돌렸습니다(Fetch·Push 없음).

## 8. 남은 미확인 사항과 한계

- **다른 Commit:** 같은 소스·다른 Commit에서 SourceLink 때문에 바이너리가 달라지는지 실측하지 않았습니다(4절 추정).
- **다른 기계:** 다른 기계·사용자 계정·SDK 설치 위치·NuGet 캐시 위치에서는 재현을 시험하지 않았습니다. 같은 PC, 같은 계정에서만 실행했습니다.
- **범위 밖:** 13개 프로젝트 전체, `StarPlayer.EvidenceVerifier`·시험 어셈블리, Godot Export DLL, `deps.json`·`runtimeconfig.json`은 분석하지 않았습니다. Domain·Simulation은 SHA 묶음만 확인했고 바이트 분류는 하지 않았습니다.
- **미분류 소량 바이트:** 길이가 다른 쌍에만 나오는 7~11바이트는 필드 단위로 해석하지 않았습니다(4절).
- **반복 횟수:** 같은 조건의 반복 빌드는 2쌍(A1/A2, E1/E1r)뿐입니다. 경로 정규화 묶음은 경로 3개에서 같은 바이트를 냈습니다. 비결정성이 없다는 결론은 이 범위 안에서만 말할 수 있습니다.
- 정식 G3, Build Freeze, 정책 적용은 하지 않았습니다. G1·G2·G4~G9는 착수하지 않았습니다.

## 9. 실험 산출물 (저장소 밖, Git에 추가하지 않음)

| 경로 | 내용 | SHA-256(앞 16자) |
|---|---|---|
| `E:\Madegame\g3x\tools\run_build.ps1` | 빌드 실행·조건 기록 | `1d312e9a44d5d614` |
| `E:\Madegame\g3x\tools\pedump\Program.cs`·`pedump.csproj` | DLL·PDB 진단 덤프(`System.Reflection.Metadata`) | `946222f5161b45aa`·`279dc204c886f07e` |
| `E:\Madegame\g3x\tools\compare.py` | 묶음·바이트 분류·의미 비교 | `ddab2703239efdb3` |
| `E:\Madegame\g3x\out\<ID>\` | 22개 빌드의 DLL·PDB(SimHarness·Domain·Simulation), `build.log`, `meta.json`, `dump_*.json` | — |
| `E:\Madegame\g3x\out_compare.json` | 비교 전체 결과 | `7bd43765edd321f8` |
| `E:\Madegame\g3x\c1`·`c2`·`c3long` | 실험 clone(`7bdde7d`, 원격 로컬 경로로 복구) | — |

Remote Push 없음. 운영 Gate G1~G9 미실행. Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
