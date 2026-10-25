# B1 G3 빌드 재현성 원인 조사 — SimHarness DLL SHA 차이 (읽기 전용)

- 작성: Claude Code, 2026-10-26. 근거: OWNER 지시 [O19](../contracts/b1/originals/owner/O19_20261025T181935Z_post-tag-evidence-and-g3-order.txt) 4항
- **범위:**
  - 이미 있는 빌드 결과 6개만 읽었습니다. 새 빌드, 빌드 설정 변경, 게임 계산식·DLL 출력 변경은 없습니다.
  - G3 운영 Build Provenance 완료나 Build Freeze를 선언하지 **않습니다**.
- **분석 도구:**
  - Python 스크립트로 PE·CLI 메타데이터·Debug Directory·Portable PDB를 직접 해석하고 바이트 단위로 비교했습니다.
  - 스크립트와 원시 결과는 `build/g3-investigation/`(git-ignored)에 있습니다.

| 파일 | SHA-256 |
|---|---|
| `g3_pe.py` | `bc9ca7c1…17e3` |
| `g3_pe2.py` | `f0e97fdc…e79a` |
| `g3_report.json` | `a219426c…2403` |

## 1. 대상 빌드

모두 `tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.dll`입니다.

| 표시 | 빌드 위치 | 소스 | 빌드 방법 | DLL SHA-256 | 바이트 |
|---|---|---|---|---|---:|
| orig | `E:\Madegame\Star_player` | 검수 작업 트리(HEAD `257403d` + 미커밋, 내용 = `c15360c`) | 작업 중 빌드(2026-10-25 19:04) | `31110bf5…117b` | 906,752 |
| v1 | `E:\Madegame\Star_player_b1_verify` | `c15360c` | verify 솔루션 빌드 | `44f31cab…495a` | 906,752 |
| v2 | `E:\Madegame\Star_player_b1_verify2` | `ff368a9` | verify 솔루션 빌드 | `e5c0fd53…e9e` | 906,752 |
| v3 | `E:\Madegame\Star_player_b1_verify3` | `7bdde7d`(Tag) | verify 솔루션 빌드 | `987f2013…aad8` | 906,752 |
| u1 | `E:\Madegame\Star_player_b1_u6time` | `ff368a9` | U6 시험 프로젝트만 빌드 | `5fc7f598…` | 906,752 |
| u2 | `E:\Madegame\Star_player_b1_u6time2` | `ff368a9` | 솔루션 빌드 | `211cfe18…` | 906,752 |

- `c15360c`·`ff368a9`·`7bdde7d` 사이에서 `src/`, `tools/StarPlayer.SimHarness/`, `content/`, `Directory.Build.props`, `global.json`, `StarPlayer.sln`의 변경은 **0개**입니다(`git diff --name-only`).
- 같은 Commit `ff368a9`에서 빌드한 v2·u1·u2도 DLL SHA가 서로 다릅니다.
- 함께 출력된 `StarPlayer.Domain.dll`·`StarPlayer.Simulation.dll`도 6개 빌드에서 모두 다릅니다. 이 두 DLL은 바이트 분석을 하지 않았습니다.

## 2. 확인된 사실

### 2.1 SDK·컴파일러·빌드 옵션 (6개 빌드 모두 같음)

| 항목 | 값 | 근거 |
|---|---|---|
| SDK | 8.0.425 고정(`global.json`, `rollForward: disable`) | verify 로그의 `.NET SDK: 8.0.425 (pinned 8.0.425)` |
| 컴파일러 | Roslyn `4.11.0-3.25569.22+3fb752d4…` | PDB 컴파일 옵션 기록 |
| 런타임 | `8.0.31` | PDB 컴파일 옵션 기록 |
| 출력·최적화 | ConsoleApplication, `optimization=release`, AnyCpu, C# 12.0, nullable Enable | PDB 컴파일 옵션 기록 |
| 전처리 기호 | 같음 | PDB 컴파일 옵션 기록 |
| 원본 파일 수 | 41개 | PDB 컴파일 옵션 기록 |
| `Deterministic` | `true`(`Directory.Build.props`) | DLL에 `Reproducible` Debug Directory 항목이 있음 |
| `ContinuousIntegrationBuild` | 설정 없음 | 저장소 전체 `.props`·`.targets`·`.csproj` 검색 |
| `PathMap` | 설정 없음 | 같은 검색 |
| `DebugType` | 기본값(별도 Portable PDB). 내장 PDB 없음 | Debug Directory에 `EmbeddedPdb` 항목 없음 |
| Assembly 버전 | `AssemblyVersion` `0.0.0.0`, `FileVersion` `0.0.0.0`, `InformationalVersion` `0.0.0-phase0` | 생성된 `obj/…/AssemblyInfo.cs` |
| Commit 해시 접미사 | 없음(`IncludeSourceRevisionInInformationalVersion=false`) | 같은 파일 |

### 2.2 바이트가 같은 영역

- **IL 메서드 본문:** 모든 쌍에서 다른 바이트 **0개**
- **EmbeddedResource 2개:**
  - 6개 빌드 모두 같습니다.
  - 저장소 파일과도 바이트가 같습니다: `virtual_tuning_v4.json` 26,505 B `992d7e24…`(검수 CRLF 바이트), `game_rules_profiles_v2.json` 2,671 B `b016b37b…`.
  - 각 clone의 튜닝 파일도 `992d7e24…`입니다.
- **메타데이터:** v1·v2·v3·u1·u2 사이에서 다른 것은 **MVID 16바이트뿐**입니다.
- **파일 구조:** 파일 크기, `.text` 끝 위치, SizeOfImage, Debug Directory 위치가 6개 모두 같습니다.

### 2.3 바이트가 다른 영역

v2를 기준으로 비교했습니다.

| 쌍 | 조건 | DLL 다른 바이트 | 위치 |
|---|---|---:|---|
| v2 ↔ v3 | Commit 다름(입력 동일). 경로 길이 같음(`verify2`/`verify3`) | **73** | COFF TimeDateStamp 4, MVID 16, CodeView 항목 stamp 4, CodeView 데이터 17(GUID 16 + 경로 문자 1), PdbChecksum 해시 32 |
| v2 ↔ u2 | **같은 Commit**. 경로 다름, 길이 같음(`verify2`/`u6time2`) | **77** | 같은 항목. CodeView 경로 문자만 5바이트 |
| v2 ↔ v1, v2 ↔ u1 | 경로 길이 1자 다름 | 191·194 | 위 항목에 더해, 경로가 1바이트 짧아져 그 뒤 데이터가 밀린 영역(헤더 5, IAT 1, 리소스 뒤 영역 169~173) |
| v2 ↔ orig | 경로 길이 11자 다름, git remote 다름 | 453 | 위 항목 + 밀린 영역 + `#~` 테이블 10바이트(6바이트 간격) |

**PE 메타데이터 값(6개 빌드 각각 다름):**

| 항목 | 내용 |
|---|---|
| CodeView PDB 경로 | 빌드 위치의 절대 경로. 예: `E:\Madegame\Star_player_b1_verify2\tools\StarPlayer.SimHarness\obj\Release\net8.0\StarPlayer.SimHarness.pdb` |
| CodeView GUID·stamp | v2 GUID `81d8926a…791e`, stamp `0xa08adc4c` |
| PDB ID | PDB `#Pdb` 스트림의 20바이트와 위 CodeView GUID·stamp가 같습니다(v2에서 확인) |
| PdbChecksum | SHA256. v2 `81d8926afdbfa760…`. 앞부분이 CodeView GUID와 거의 같고, 일부 비트만 다릅니다 |
| MVID | v2 `e31c60b7…858f`, v3 `c36c34ce…b681` 등 빌드마다 다름 |
| COFF TimeDateStamp | 실제 시각이 아닌 값(예: `0x80465490`) |

**PDB 비교:**

| 쌍 | 크기 | 다른 바이트 | 내용 |
|---|---|---:|---|
| v2 ↔ v3 | 136,524 B로 같음 | **21** | PDB ID 20바이트 + `#Blob`의 경로 조각 `Star_player_b1_verify2`의 문자 1개 |
| v2 ↔ u2 | 같음 | 25 | PDB ID 20 + 경로 문자 5 |
| v2 ↔ v1 | 같음 | 53,671 | 경로 길이가 달라 `#Blob` 이후 오프셋 전체가 밀림 |

- **SourceLink:**
  - orig는 `obj/…/StarPlayer.SimHarness.sourcelink.json`이 생성됐고 PDB에 들어 있습니다. 내용은 `"E:\\Madegame\\Star_player\\*": "https://raw.githubusercontent.com/junk1901/Star_player/257403dd…/*"`입니다.
  - clone 5개에는 이 파일이 없고 PDB에도 SourceLink가 없습니다.
- **git remote:** orig `origin`은 GitHub URL, clone 5개는 로컬 경로 `E:/Madegame/Star_player`입니다.
- **SourceLink와 실제 빌드 소스의 불일치:** orig PDB의 SourceLink가 가리키는 Commit `257403d`에는 실제로 빌드한 B1 소스(미커밋 작업 트리)가 없습니다.

### 2.4 확인된 사실의 요약

- 경로 길이가 같은 쌍(v2↔v3, v2↔u2)을 비교했습니다. DLL·PDB에서 다른 바이트 중 **입력처럼 보이는 것은 빌드 위치 경로 문자뿐**입니다.
- 나머지는 모두 식별·해시 값입니다: PDB ID, CodeView GUID·stamp, PdbChecksum, MVID, COFF TimeDateStamp.
- IL, 리소스, MVID 외의 메타데이터, 버전 속성, 컴파일 옵션은 같습니다.
- 계산 결과(ConfigHash·RunResultHash·Dry Pilot Manifest)는 B1 보고서 10.4·10.6에 따라 모든 빌드에서 같습니다. 바이너리 바이트 재현성은 이와 별개입니다.

## 3. 가능한 원인 (확정 아님)

| 가능한 원인 | 뒷받침하는 사실 | 확정하지 못한 이유 |
|---|---|---|
| **A. 빌드 절대 경로가 출력에 들어감**(PathMap 없음): DLL의 CodeView PDB 경로, PDB의 문서 경로 | 같은 Commit·같은 길이 경로(v2↔u2)에서 경로 문자 외의 입력 차이가 없음 | 같은 경로에서 두 번 빌드해 같은 바이트가 나오는지(비결정성 배제) 시험하지 않음 |
| **B. Deterministic 모드의 해시 연쇄:** 경로 → PDB 내용 → PDB ID·PdbChecksum → DLL 내용 → MVID·TimeDateStamp | PDB ID = CodeView GUID·stamp(관찰). 다른 바이트가 이 해시 값들에만 있음 | 연쇄는 Roslyn Deterministic 설계 설명과 일치하는 해석입니다. 이 저장소에서 인과를 실험으로 확인하지는 않음 |
| **C. SourceLink 입력 차이:** remote가 GitHub면 SourceLink(현재 HEAD Commit 포함)가 PDB에 들어가고, 로컬 경로 remote면 빠짐 | orig에만 SourceLink 있음 | orig는 경로도 달라 C만의 영향을 분리하지 못함. Commit마다 PDB가 바뀌는지 시험하지 않음 |
| **D. 경로 길이에 따른 배치 이동** | 경로 1자 차이(v1·u1)와 11자 차이(orig)에서 CodeView 데이터 뒤 영역이 밀림. orig의 `#~` 10바이트(6바이트 간격)는 그 뒤 데이터의 RVA 열(예: FieldRVA 행 6바이트)로 보임 | `#~` 테이블을 해석해 어느 테이블인지 확인하지 않음 |

- 같은 경로·같은 입력에서도 바이트가 달라지는 비결정성은 **배제하지 못했습니다**. 그런 쌍이 기존 빌드 중에 없습니다.

## 4. 추가 재현 시험 (새 빌드 필요 — OWNER 허가 대상, 실행하지 않음)

1. **같은 경로 반복 빌드:** 같은 clone에서 `bin`·`obj`를 지우고 두 번 빌드합니다. 결과가 바이트 동일하면 비결정성이 배제됩니다.
2. **경로만 다른 두 clone + PathMap:** `-p:PathMap=<root>=/_/` 또는 `ContinuousIntegrationBuild=true`(+ `DeterministicSourcePaths`)로 빌드합니다. 동일해지는지 봅니다(원인 A·B 확인).
3. **SourceLink 영향 분리:** 같은 경로에서 remote를 GitHub/로컬로 바꾸거나 `EnableSourceLink=false`로 빌드합니다. Commit만 다른 두 빌드에서 PDB·DLL이 달라지는지 봅니다(원인 C 확인).
4. **범위 확장:** `StarPlayer.Domain.dll`·`StarPlayer.Simulation.dll`과 13개 프로젝트 전체, Godot Export DLL에도 같은 분석을 합니다.
5. **`#~` 10바이트:** 테이블을 해석해 원인 D를 확인합니다(읽기 전용으로 가능).

## 5. OWNER 결정 대상 (변경하지 않음)

- 운영 빌드에서 **바이너리 바이트 재현성**을 요구할지 정해야 합니다. 경로가 달라도 같은 DLL이 필요한지, 아니면 계산 결과 Hash 불변만 요구할지입니다.
- 요구한다면 다음을 정해야 합니다.
  - `PathMap`·`ContinuousIntegrationBuild`·SourceLink·PDB 형식 정책
  - 고정 빌드 경로 또는 정규화 방식
  - 빌드 입력·출력 바이트 고정 목록
- 운영 Gate G3(Build Provenance)의 정식 실행. 이번 조사는 G3 실행이 아닙니다.

Remote Push 없음. 운영 Gate G1~G9 미실행. Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
