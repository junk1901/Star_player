# B1 G3 Godot Scene 정식 변경안 — 검증 결과 (미커밋, Codex 검수 대기)

- 작성: Claude Code, 2026-10-26. 근거: OWNER 지시 "G3 Godot Scene 정식 변경안 작성"(2026-10-26), [TASK](../tasks/B1_G3_SCENE_MIGRATION_TASK.md)
- **성격:**
  - 변경안과 검증 결과입니다.
  - 새 코드 기준본 수용, 공식 G3 PASS, Build Freeze가 **아닙니다**.
  - **Stage·Commit·Push·Tag를 하지 않았습니다.** 변경은 `E:\Madegame\Star_player` 작업 트리에만 있습니다.
- 기존 B1 Tag `b1-code-accepted-2026-10-26` → `7bdde7dd…`, 구현 Manifest 213개 파일, 승인 계약, OWNER 원문, 루트 `TASK.md`는 변경하지 않았습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 요약

| 항목 | 결과 |
|---|---|
| 변경 파일 | Scene 4개 + `.gitattributes` 1행 규칙(주석 1행 포함 2행). 새 SHA는 [Manifest](B1_G3_SCENE_MIGRATION_MANIFEST.md) |
| Scene 바이트 | PCK 재현성을 입증한 진단 재저장본 R1과 4개 모두 SHA 동일 |
| diff 범위 | 헤더 `load_steps` 제거 4행, `[node …]` 행 끝 `unique_id` 추가 51행, LF 개행. **그 외 변경 0** |
| unique_id | 51개 노드 모두 있음, Scene 안·Scene 사이 중복 0. 참조 정상. Export `node_ids` = 저장값(전체 배열) |
| 개행 규칙 `*.tscn text eol=lf` | 추적 파일 821개 중 속성이 바뀌는 것은 대상 4개뿐. 필터 blob = 원본 바이트. 체크아웃 결과 = 의도한 LF 바이트 |
| PCK | 같은 폴더 2회, Import Cache 유지 1회, 다른 폴더 2회 모두 `3a187e19…`(13,452 B). 나머지 Export 파일 197개 동일 |
| 전체 회귀 | `tools/verify.ps1` **GATE PASS**. 3,041 PASS / 0 FAIL / 0 SKIP, Golden 4(U5 포함), G1~G4·E1~E4 PASS, Dry Pilot `ALL_9_IDENTICAL=True` |
| E3·E4 | **FULL PASS 근거 확보:** 7/7, `hidden_identifier_leaks=0`, 스크린샷 5개 전부, 원본과 바이트 동일, 오류·경고 0 |

## 2. 변경 내용

| 경로 | 노드 | 바뀐 행(LF 정규화 기준) | 새 SHA-256 |
|---|---:|---|---|
| `game/Bootstrap/Main.tscn` | 1 | 헤더 1 + 노드 1 | `3501aba8fd59b5450855bca47bc2de66f7b6f1b251df3ac84d04d64f9787edf4` |
| `game/UI/MainMenu/MainMenu.tscn` | 14 | 헤더 1 + 노드 14 | `79dbc1dd026645d12fb05963e90130aa340403b3247f839f51613b727395192f` |
| `game/UI/PlayerDiagnostics/PlayerDiagnosticsScreen.tscn` | 13 | 헤더 1 + 노드 13 | `defd9603508305d24ae864113b786c698927489e0756497870df043262f9f85c` |
| `game/UI/GameDiagnostics/GameDiagnosticsScreen.tscn` | 23 | 헤더 1 + 노드 23 | `98886edae773b66fba879ca11a2c7156d7b3983a4523d70d9991418f23634490` |
| `.gitattributes` | — | 끝에 주석 1행 + `*.tscn text eol=lf` | `a001c39c1abe64addc003452613f773b46c0e1bf09cb56b723784b95f5c5ce1b` |

- **Scene 출처:** Scene 바이트는 `E:\Madegame\g3x\pck-out\R1_pack\`(진단 clone `pb`의 재저장본)에서 그대로 복사했고 SHA가 같습니다.
  - 재저장 방법은 [PCK 진단 보고서](B1_G3_GODOT_PCK_REPRO_REPORT.md) 5절 R1입니다. Godot 4.7.2에서 instantiate → `PackedScene.pack` → `ResourceSaver.save`를 썼습니다.
  - 편집기 UI의 Save Scene·"Upgrade Project Files"와 같은 결과인지는 확인하지 않았습니다.
- 전체 diff는 부록 A입니다.

## 3. unique_id 검증

검사 스크립트가 원본 blob(`7bdde7d`)과 후보를 행 단위로 비교했습니다.

| Scene | 행 수(전후 같음) | 노드 | `unique_id` | 서로 다른 값 | 허용 외 변경 | `ext_resource` 참조 | 부모 경로 | Export `node_ids` = 저장 `unique_id` |
|---|---:|---:|---:|---:|---:|---|---|---|
| Main | 7 | 1 | 1 | 1 | 0 | 1개 정상 | 정상 | 같음 |
| MainMenu | 99 | 14 | 14 | 14 | 0 | 1개 정상 | 정상 | 같음 |
| PlayerDiagnosticsScreen | 93 | 13 | 13 | 13 | 0 | 1개 정상 | 정상 | 같음 |
| GameDiagnosticsScreen | 157 | 23 | 23 | 23 | 0 | 2개 정상 | 정상 | 같음 |

- **행 비교 방식:**
  - 바뀐 행은 두 종류뿐입니다. 헤더(원본에서 ` load_steps=N`만 지운 것과 같음)와 노드 행(원본 노드 행 끝 `]` 앞에 ` unique_id=<정수>`만 붙은 것)입니다.
  - `ext_resource` 행은 원본과 같고, `sub_resource`는 없습니다.
- **값의 범위:** Scene 사이 중복은 0이고, 값은 51개(50449747 ~ 2118838403)입니다.
- **로드:**
  - 후보 clone에서 Godot 4.7.2로 4개 Scene을 로드하고 인스턴스화했습니다. `LOAD_OK`, 상태 노드 수 = 인스턴스 노드 수(1·14·13·23).
  - GDScript `SceneState`에는 노드별 `unique_id` 조회 메서드가 없어(`has_method` = false), 그 대응은 PCK 해석으로 확인했습니다.
- **결정성:** Export 5회 모두 같은 PCK이므로 `unique_id`가 다시 생성되지 않았습니다. 값은 저장된 소스 값입니다.

## 4. 개행 정책 영향

| 검사 | 결과 |
|---|---|
| 추적 중인 `.tscn` | 정확히 4개(대상과 같음) |
| 속성이 바뀌는 추적 파일(`7bdde7d` clone과 새 `.gitattributes` 적용 clone의 `check-attr` 비교, 821개) | **4개**(대상 Scene). `text`: unspecified→set, `eol`: unspecified→lf. 그 밖의 817개는 같음 |
| 작업 트리 | 4개 모두 `i/lf w/lf attr/text eol=lf`, CR 0 |
| blob | `git hash-object`(필터) = `--no-filters` = 새 바이트의 blob(`a0a6310b…`, `592c3595…`, `1521d61f…`, `21bc44da…`) |
| 체크아웃 결과(진단 clone, `hash-object -w` + `cat-file --filters`, Stage 없음, `core.autocrlf=true`) | 새 규칙: 4개 모두 새 바이트와 같음(CR 0). 이전 규칙이었다면 CRLF(CR 6·98·92·156)로 바뀌었을 것 |
| `.gitattributes` 자체 | LF, CR 0. 기존 `.gitattributes text eol=lf` 규칙 그대로 |

- `.gitattributes`는 B1 구현 Manifest 213개 항목 중 하나(이전 SHA `f339e2ef…`)입니다. 후보 적용 트리에서는 그 항목만 일치하지 않습니다. 213개 Manifest는 수정하지 않았고 `7bdde7d` 기록으로 남습니다.
- `game/` 아래의 다른 텍스트 파일(`*.cs.uid` 7개, `project.godot`, `export_presets.cfg`)은 이 규칙의 대상이 아니며 바뀌지 않았습니다. 그 개행 정책은 이번 범위 밖입니다.

## 5. PCK 재현성

- Godot `4.7.2.stable.mono.official.ed1daf0bf`, 검증된 Template 5개(verify T3 PASS)를 썼습니다.
- Export 조건은 [PCK 진단](B1_G3_GODOT_PCK_REPRO_REPORT.md) 2절과 같습니다: `ContinuousIntegrationBuild=true`, 매번 `bin/obj`·`game/.godot`·`build/export` 삭제.
- **예외 M_C_warm:** Import Cache(`game/.godot`)와 `bin/obj`는 앞 실행(M_A2)의 것을 유지했고, `build/export`만 삭제 후 다시 만들었습니다(`run_pck.ps1 -NoClean`).
- **정정(2026-10-26, Codex 검수 NOTE 반영):** 검수본의 "빌드 출력 유지(삭제 없음)" 표현은 Export 폴더까지 유지한 것처럼 읽혀 위와 같이 고쳤습니다. 문서 설명만 고쳤고, 실험 결과·원시 로그·게임 코드는 바뀌지 않았습니다.

| ID | 조건 | clone | `StarPlayer.pck` SHA-256 | 바이트 | 나머지 Export 파일 |
|---|---|---|---|---:|---|
| M_A1 | 같은 폴더 1회 | `E:\Madegame\g3x\sma` | `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799` | 13,452 | 197개, 원본 Scene Export(A1)와 같음 |
| M_A2 | 같은 폴더 2회 | `sma` | 같음 | 13,452 | 같음 |
| M_C_warm | Import Cache(`game/.godot`)·`bin/obj` 유지, `build/export`는 삭제 후 재생성 | `sma` | 같음 | 13,452 | 같음 |
| M_B1 | 다른 폴더(경로 길이 다름) 1회 | `E:\Madegame\g3x\smblonger` | 같음 | 13,452 | 같음 |
| M_B2 | 다른 폴더 2회 | `smblonger` | 같음 | 13,452 | 같음 |

- 5회 모두 진단 재저장본 PCK(B1·B2·C1·C2)와도 같습니다.
- 판정은 PCK 전체 바이트 비교입니다. `node_ids` 제외 비교나 사후 패치는 하지 않았습니다.
- Export 파일 수는 198개(PCK + 197개)로 원본 Scene Export와 같습니다.
- **깨끗한 체크아웃 조건:** Commit 전이라 새 Commit의 체크아웃은 할 수 없습니다. 대신 다음으로 대체했습니다(Commit 후 다시 확인 필요).
  - `7bdde7d` 깨끗한 clone에 후보 5개 파일을 바이트 복사
  - 4절의 체크아웃 변환 확인

## 6. 전체 회귀 (후보 적용 clone `E:\Madegame\g3x\smv`)

**`tools/verify.ps1 -LogName B1_SCENE_MIGRATION_VERIFY_LOG.md`:**

- 2026-10-26 05:49:19~06:09:09 KST, exit 0
- 로그의 `Git: 7bdde7d (dirty working tree)`는 후보 5개 파일의 미커밋 변경 때문이며 의도한 상태입니다(D2: 5 entries).

| ID | 결과 |
|---|---|
| T1~T3 | SDK 8.0.425, Godot 4.7.2.stable.mono.official.ed1daf0bf, Template PASS |
| B1~B3 | Release 빌드 PASS(경고 0) |
| U1 / U2 / U3(U3a·U3b) / U4 / U5 / U6 | 327 / 741 / 128(14·48) / 1,237 / 538 / 70 PASS. **합계 3,041 PASS / 0 FAIL / 0 SKIP** |
| Golden 4 | U5에 포함되어 PASS |
| G1~G4 | PASS. G2 `진단 결과: 통과 (8/8 단계)`, G3 `hidden_identifier_leaks=0 listed_players=10 own_player_cards=1`, G4 `games=6 finished_with_box_score=6 self_check=PASS` |
| E1·E1b | PASS(Export 새로 생성, client DLL = ExportRelease publish 출력) |
| E2~E4 | PASS(verify 기준: 아래 증거) |
| D1·D3 | PASS(205개 문서, 보호 파일 5개) |
| **GATE** | **PASS** |

**Dry Pilot 9 Run(`tools/b1-drypilot-repro.ps1`, smv 빌드 DLL `40b77cc0…`):**

- **`ALL_9_IDENTICAL=True`**. ConfigHash·RunResultHash·환경 외 Manifest 9개 모두 보관본과 같습니다.
- DLL SHA가 원본 clone 검증(`987f2013…`)과 다른 것은 verify의 기본 빌드(CI 속성 없음)가 빌드 경로를 기록하기 때문입니다(이전 G3 진단). Scene 변경과 무관합니다. SimHarness는 Scene을 입력으로 쓰지 않습니다.

**E3·E4 등 Godot 실행 증거(빠짐없이):**

- 비교 기준: 원본 `7bdde7d` 깨끗한 clone 검증 `E:\Madegame\Star_player_b1_verify3\build\verify\`
- 후보 출력: `E:\Madegame\g3x\smv\build\verify\`

| 증거 | 후보 결과 | 원본과 비교 |
|---|---|---|
| E2 `exported-diagnostics.txt` | `STARPLAYER_DIAGNOSTICS=PASS`, 8/8, 10행 | 텍스트 동일 |
| E3 `exported-player-diagnostics.txt` | `STARPLAYER_PLAYER_DIAGNOSTICS=PASS`, **`자체 점검: 통과 (7/7)`**, **`hidden_identifier_leaks=0`**, 393행 | 텍스트 동일 |
| E4 `exported-game-diagnostics.txt` | `STARPLAYER_GAME_DIAGNOSTICS=PASS`, `자체 점검: 통과 (7/7)`, 376행 | 텍스트 동일 |
| G2·G3·G4 headless 출력 | PASS, G3 7/7·leaks 0, G4 7/7 | 3개 모두 텍스트 동일 |
| `exported-shell.png` | 392,404 B `5388d19d1ac01c87…` | 바이트 동일 |
| `exported-player-card.png` | 557,620 B `4ede6a126aa97690…` | 바이트 동일 |
| `exported-player-card-npc.png` | 586,783 B `c8c70b5f983b1b13…` | 바이트 동일 |
| `exported-game-screen.png` | 443,278 B `4455e6ece67ec39f…` | 바이트 동일 |
| `exported-game-screen-end.png` | 494,745 B `1f26329a4be0d86f…` | 바이트 동일 |
| G1~G4·E1~E4 원시 로그의 ERROR/WARNING 행 | 0개 | 원본도 0개 |
| 반환 코드 | G1~G4·E1~E4 모두 0 | — |

- 원시 로그와 출력은 `E:\Madegame\g3x\smv\build\verify\`(`verify-result.json` `143cd6ba…`)에 있습니다. verify 로그 사본은 [B1_SCENE_MIGRATION_VERIFY_LOG.md](B1_SCENE_MIGRATION_VERIFY_LOG.md)(`8ba6afaf…`, 미커밋)입니다.
- 진단 Export(M_A1)의 실행 검사(G2~G4·E2~E4)도 exit 0과 PASS 표시였습니다(보조 증거).

## 7. 기존 B1 계약과 다른 범위

- **계약 밖의 소스 변경입니다.**
  - B1-T00 승인 계약의 범위는 SimHarness 증거 체계(§18 Build Provenance·Test Profile 등)입니다.
  - Godot 클라이언트 Scene은 계약의 결과 계산·증거 경로에 들어가지 않습니다.
  - 이번 변경은 계약 조항을 바꾸지 않고, 계약이 정한 Hash·Replay·Golden에도 영향이 없습니다(6절 회귀 동일).
- **`.gitattributes`:**
  - B1 기준본에서 OWNER 결정 G2(바이트 보존 정책, Commit `b2635ab`)로 정한 파일입니다.
  - 이번에는 기존 규칙을 하나도 바꾸지 않고 `*.tscn` 규칙만 추가했습니다. 일괄 정규화는 없습니다.
- **기준본 관계:** 이 변경이 Commit되면 B1 Tag 대상 `7bdde7d`와 다른 새 소스 Commit이 됩니다. 기존 Tag는 역사적 코드 수용 기준본으로 남습니다.

## 8. SourceLink와 Provenance 후속 영향 (이번에 등록·변경하지 않음)

새 소스 Commit을 쓰면 다음을 다시 계산·검증해야 합니다.

| 항목 | 이유 |
|---|---|
| `source_git_sha`(§18.3)·ApprovalRecord Commit 결속 | 새 Commit SHA |
| SourceLink Commit | CI 빌드의 SourceLink URL에 Commit SHA가 들어감. 모든 StarPlayer 어셈블리의 PDB와 DLL 식별자(MVID·PDB ID 등)가 바뀜. IL·리소스는 같을 것으로 예상(이전 실험의 Commit 차이 결과)하나 다시 확인 필요 |
| DLL·PDB SHA, VERIFIER_BINARY SHA, Artifact 후보 109개 SHA | 위와 같은 이유 |
| Test Profile의 소스 결속(fixture blob, `source_git_sha`) | 새 Commit 기준 |
| Godot Export 산출물 SHA | Scene 변경으로 PCK가 `3a187e19…`가 됨(원본 Scene에는 고정값이 없었음) |

**B-4 후보(OWNER 검토용):**

- **SourceLink URL 후보:** `https://github.com/junk1901/Star_player.git`
  - 저장소가 있다는 것은 OWNER가 확인했습니다.
  - B1 Commit의 원격 조회는 **미확인**이며, Remote Push는 하지 않았습니다.
- **Artifact 후보 109개:** SIMHARNESS_BINARY 1, VERIFIER_BINARY 1, TEST_ASSEMBLY 4, DEPENDENCY 91, RUNTIME_CONFIG 12([PCK 진단](B1_G3_GODOT_PCK_REPRO_REPORT.md) 10절·부록 A). 최종 승인 전 후보입니다.
- **TRUST_CONFIG:** 운영 앵커가 확정되지 않았습니다. 109개 목록만으로 완결된 공식 Provenance라고 하지 않습니다.
- **역할에 넣지 않은 것:** coverage 대응표 6개, PDB, PCK는 닫힌 역할에 추가하지 않았습니다.
- **Apphost:**
  - Test Profile은 `dotnet test <dll>`(`TestProfiles.cs` 156행)로 실행되므로 시험 apphost는 쓰이지 않음이 확인됩니다.
  - **SimHarness·Verifier의 공식 실행 명령은 계약·코드에서 확정되지 않았습니다.** 그래서 `StarPlayer.SimHarness.exe`(151,552 B, `56b5efb2d34b155c34c12dd198cba789fb0837cd0f184816862f045b0b14c1ee`)와 `StarPlayer.EvidenceVerifier.exe`(152,064 B, `d7f17165c95250f7af727e61f4942220941b443df173ad5d6b4d914da97aa0d6`)는 **제외 확정이 아닌 보류**입니다(S1 CI 빌드 값).
- **B-4 Clause ID·공식 출력 경로:** OWNER_VALUE_PENDING

## 9. §18.2 Environment Profile 등록 시점 (등록하지 않음)

승인된 Windows 실행 환경에서 다음을 먼저 확보해야 합니다. 그다음 모든 필수 값과 N12 비트 Golden을 갖춘 뒤 별도 OWNER 승인을 받아 등록합니다.

- 실제 OS 버전(`Environment.OSVersion.Version`)·런타임(`framework_description`, `netcore_app_version`, RID)·아키텍처(X64)
- `ucrtbase.dll` 파일 버전과 SHA
- `System.Private.CoreLib.dll`·`clrjit.dll`·`coreclr.dll` SHA
- 환경 변수 정책(금지 접두사 `COMPlus_`·`DOTNET_`, §18.4) 검증
- UpperQuantile 36개 BITS64, `witness_digest`
- 관련 신뢰 앵커와 승인 등록 근거

이번 진단의 빌드 환경 기록([PCK 진단](B1_G3_GODOT_PCK_REPRO_REPORT.md) 13절)은 위 프로필 값이 아닙니다.

## 10. 검수 요청과 다음 단계

- **Codex READ-ONLY 표적 검수 요청서:** [B1_G3_SCENE_MIGRATION_CODEX_REVIEW_REQUEST.md](../tasks/B1_G3_SCENE_MIGRATION_CODEX_REVIEW_REQUEST.md)
- **다음 단계:** 검수 결과와 수정 범위를 보고한 뒤, OWNER의 별도 Commit 수용 결정을 요청합니다.
- **현재 작업 트리 변경(미커밋):**
  - 소스: `.gitattributes`, Scene 4개
  - 문서: 이 보고서, TASK, Manifest `.md`·`.sha256`, 검수 요청서, verify 로그 사본

## 11. 도구·산출물 (저장소 밖, Git에 추가하지 않음)

| 경로 | 내용 | SHA-256(앞 16자) |
|---|---|---|
| `E:\Madegame\g3x\tools\run_pck.ps1` | Export·실행 검사(`-NoClean` 추가) | `e093dd652bcbb44d` |
| `E:\Madegame\g3x\tools\scene_batch.ps1` | 이번 PCK·로드·verify·Dry Pilot 일괄 실행 | `8ba628440ce788fa` |
| `E:\Madegame\g3x\tools\load_check.gd` | Scene 로드·인스턴스화 확인 | `14764cfca8969be5` |
| `E:\Madegame\g3x\pck-out\M_*\` | 5회 Export의 PCK·`meta.json`·로그, M_A1 실행 검사 출력·스크린샷, `load_check.log` | — |
| `E:\Madegame\g3x\smv\build\verify\` | verify 원시 로그·출력·스크린샷 | — |
| `E:\Madegame\g3x\smv_drypilot.txt` | Dry Pilot 출력 | `2cfda9dfe4fa2913` |

- **검사 스크립트:** unique_id·diff 검사, 속성 비교, 체크아웃 변환 확인, 회귀 증거 수집에 쓴 스크립트는 세션 임시 폴더에서 실행했습니다(결과는 이 보고서에 기록).
- **진단 clone:**
  - `sma`·`smblonger`·`smv`에는 후보 5개 파일이 **미커밋 수정본**으로 있습니다.
  - `pb`·`pcxlong`에는 R1 Scene 진단본이 있습니다.
  - 모두 승인 소스가 아닙니다.

Remote Push 없음. 운영 Gate G1~G9 미실행. Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 부록 A. Scene 전체 diff (`git diff 7bdde7d -- game`, LF 정규화 기준)

```diff
diff --git a/game/Bootstrap/Main.tscn b/game/Bootstrap/Main.tscn
index a5d3d71..a0a6310 100644
--- a/game/Bootstrap/Main.tscn
+++ b/game/Bootstrap/Main.tscn
@@ -1,6 +1,6 @@
-[gd_scene load_steps=2 format=3]
+[gd_scene format=3]
 
 [ext_resource type="Script" path="res://Bootstrap/GameRoot.cs" id="1_root"]
 
-[node name="Main" type="Node"]
+[node name="Main" type="Node" unique_id=480977380]
 script = ExtResource("1_root")
diff --git a/game/UI/GameDiagnostics/GameDiagnosticsScreen.tscn b/game/UI/GameDiagnostics/GameDiagnosticsScreen.tscn
index 8d27174..21bc44d 100644
--- a/game/UI/GameDiagnostics/GameDiagnosticsScreen.tscn
+++ b/game/UI/GameDiagnostics/GameDiagnosticsScreen.tscn
@@ -1,9 +1,9 @@
-[gd_scene load_steps=3 format=3]
+[gd_scene format=3]
 
 [ext_resource type="Script" path="res://UI/GameDiagnostics/GameDiagnosticsScreen.cs" id="1_screen"]
 [ext_resource type="Script" path="res://Components/BaseDiamond.cs" id="2_bases"]
 
-[node name="GameDiagnosticsScreen" type="Control"]
+[node name="GameDiagnosticsScreen" type="Control" unique_id=1969789454]
 layout_mode = 3
 anchors_preset = 15
 anchor_right = 1.0
@@ -12,7 +12,7 @@ grow_horizontal = 2
 grow_vertical = 2
 script = ExtResource("1_screen")
 
-[node name="Background" type="ColorRect" parent="."]
+[node name="Background" type="ColorRect" parent="." unique_id=563421412]
 layout_mode = 1
 anchors_preset = 15
 anchor_right = 1.0
@@ -21,7 +21,7 @@ grow_horizontal = 2
 grow_vertical = 2
 color = Color(0.09, 0.1, 0.13, 1)
 
-[node name="Margin" type="MarginContainer" parent="."]
+[node name="Margin" type="MarginContainer" parent="." unique_id=1937460089]
 layout_mode = 1
 anchors_preset = 15
 anchor_right = 1.0
@@ -33,94 +33,94 @@ theme_override_constants/margin_top = 40
 theme_override_constants/margin_right = 70
 theme_override_constants/margin_bottom = 40
 
-[node name="Column" type="VBoxContainer" parent="Margin"]
+[node name="Column" type="VBoxContainer" parent="Margin" unique_id=213060421]
 layout_mode = 2
 theme_override_constants/separation = 16
 
-[node name="HeaderRow" type="HBoxContainer" parent="Margin/Column"]
+[node name="HeaderRow" type="HBoxContainer" parent="Margin/Column" unique_id=124128182]
 layout_mode = 2
 theme_override_constants/separation = 32
 
-[node name="TitleLabel" type="Label" parent="Margin/Column/HeaderRow"]
+[node name="TitleLabel" type="Label" parent="Margin/Column/HeaderRow" unique_id=305266412]
 layout_mode = 2
 size_flags_horizontal = 3
 theme_override_font_sizes/font_size = 48
 text = "경기 진단 (Phase 2) — 스크립트 테스트 경기"
 
-[node name="BackButton" type="Button" parent="Margin/Column/HeaderRow"]
+[node name="BackButton" type="Button" parent="Margin/Column/HeaderRow" unique_id=1453291888]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(320, 76)
 layout_mode = 2
 theme_override_font_sizes/font_size = 34
 text = "뒤로"
 
-[node name="Body" type="HBoxContainer" parent="Margin/Column"]
+[node name="Body" type="HBoxContainer" parent="Margin/Column" unique_id=1657396856]
 layout_mode = 2
 size_flags_vertical = 3
 theme_override_constants/separation = 28
 
-[node name="Left" type="VBoxContainer" parent="Margin/Column/Body"]
+[node name="Left" type="VBoxContainer" parent="Margin/Column/Body" unique_id=669911819]
 custom_minimum_size = Vector2(640, 0)
 layout_mode = 2
 theme_override_constants/separation = 14
 
-[node name="GameList" type="ItemList" parent="Margin/Column/Body/Left"]
+[node name="GameList" type="ItemList" parent="Margin/Column/Body/Left" unique_id=78639402]
 unique_name_in_owner = true
 layout_mode = 2
 size_flags_vertical = 3
 theme_override_font_sizes/font_size = 26
 
-[node name="NextButton" type="Button" parent="Margin/Column/Body/Left"]
+[node name="NextButton" type="Button" parent="Margin/Column/Body/Left" unique_id=810611188]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(0, 88)
 layout_mode = 2
 theme_override_font_sizes/font_size = 36
 text = "다음 사건 진행"
 
-[node name="RestartButton" type="Button" parent="Margin/Column/Body/Left"]
+[node name="RestartButton" type="Button" parent="Margin/Column/Body/Left" unique_id=206929151]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(0, 72)
 layout_mode = 2
 theme_override_font_sizes/font_size = 30
 text = "처음부터 다시"
 
-[node name="RunButton" type="Button" parent="Margin/Column/Body/Left"]
+[node name="RunButton" type="Button" parent="Margin/Column/Body/Left" unique_id=1775171017]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(0, 72)
 layout_mode = 2
 theme_override_font_sizes/font_size = 30
 text = "경기 끝까지 진행 (진단용)"
 
-[node name="Right" type="VBoxContainer" parent="Margin/Column/Body"]
+[node name="Right" type="VBoxContainer" parent="Margin/Column/Body" unique_id=810165189]
 layout_mode = 2
 size_flags_horizontal = 3
 theme_override_constants/separation = 12
 
-[node name="StatusLabel" type="Label" parent="Margin/Column/Body/Right"]
+[node name="StatusLabel" type="Label" parent="Margin/Column/Body/Right" unique_id=1087047023]
 unique_name_in_owner = true
 layout_mode = 2
 theme_override_font_sizes/font_size = 28
 text = "-"
 autowrap_mode = 3
 
-[node name="ScoreGrid" type="GridContainer" parent="Margin/Column/Body/Right"]
+[node name="ScoreGrid" type="GridContainer" parent="Margin/Column/Body/Right" unique_id=1011101425]
 unique_name_in_owner = true
 layout_mode = 2
 theme_override_constants/h_separation = 6
 theme_override_constants/v_separation = 4
 columns = 13
 
-[node name="InfoRow" type="HBoxContainer" parent="Margin/Column/Body/Right"]
+[node name="InfoRow" type="HBoxContainer" parent="Margin/Column/Body/Right" unique_id=257266998]
 layout_mode = 2
 theme_override_constants/separation = 24
 
-[node name="Bases" type="Control" parent="Margin/Column/Body/Right/InfoRow"]
+[node name="Bases" type="Control" parent="Margin/Column/Body/Right/InfoRow" unique_id=331767073]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(300, 300)
 layout_mode = 2
 script = ExtResource("2_bases")
 
-[node name="InfoLabel" type="Label" parent="Margin/Column/Body/Right/InfoRow"]
+[node name="InfoLabel" type="Label" parent="Margin/Column/Body/Right/InfoRow" unique_id=1176261704]
 unique_name_in_owner = true
 layout_mode = 2
 size_flags_horizontal = 3
@@ -128,18 +128,18 @@ theme_override_font_sizes/font_size = 30
 text = "-"
 autowrap_mode = 3
 
-[node name="EventLabel" type="Label" parent="Margin/Column/Body/Right"]
+[node name="EventLabel" type="Label" parent="Margin/Column/Body/Right" unique_id=469315488]
 unique_name_in_owner = true
 layout_mode = 2
 theme_override_font_sizes/font_size = 28
 text = "-"
 autowrap_mode = 3
 
-[node name="BoxScroll" type="ScrollContainer" parent="Margin/Column/Body/Right"]
+[node name="BoxScroll" type="ScrollContainer" parent="Margin/Column/Body/Right" unique_id=1501452198]
 layout_mode = 2
 size_flags_vertical = 3
 
-[node name="BoxLabel" type="Label" parent="Margin/Column/Body/Right/BoxScroll"]
+[node name="BoxLabel" type="Label" parent="Margin/Column/Body/Right/BoxScroll" unique_id=473350613]
 unique_name_in_owner = true
 layout_mode = 2
 size_flags_horizontal = 3
@@ -147,7 +147,7 @@ theme_override_font_sizes/font_size = 24
 text = "-"
 autowrap_mode = 3
 
-[node name="CheckLabel" type="Label" parent="Margin/Column"]
+[node name="CheckLabel" type="Label" parent="Margin/Column" unique_id=193391112]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(0, 190)
 layout_mode = 2
diff --git a/game/UI/MainMenu/MainMenu.tscn b/game/UI/MainMenu/MainMenu.tscn
index 73b4a33..592c359 100644
--- a/game/UI/MainMenu/MainMenu.tscn
+++ b/game/UI/MainMenu/MainMenu.tscn
@@ -1,8 +1,8 @@
-[gd_scene load_steps=2 format=3]
+[gd_scene format=3]
 
 [ext_resource type="Script" path="res://UI/MainMenu/MainMenu.cs" id="1_menu"]
 
-[node name="MainMenu" type="Control"]
+[node name="MainMenu" type="Control" unique_id=692028515]
 layout_mode = 3
 anchors_preset = 15
 anchor_right = 1.0
@@ -11,7 +11,7 @@ grow_horizontal = 2
 grow_vertical = 2
 script = ExtResource("1_menu")
 
-[node name="Background" type="ColorRect" parent="."]
+[node name="Background" type="ColorRect" parent="." unique_id=384562947]
 layout_mode = 1
 anchors_preset = 15
 anchor_right = 1.0
@@ -20,7 +20,7 @@ grow_horizontal = 2
 grow_vertical = 2
 color = Color(0.09, 0.1, 0.13, 1)
 
-[node name="Margin" type="MarginContainer" parent="."]
+[node name="Margin" type="MarginContainer" parent="." unique_id=1872815912]
 layout_mode = 1
 anchors_preset = 15
 anchor_right = 1.0
@@ -32,64 +32,64 @@ theme_override_constants/margin_top = 120
 theme_override_constants/margin_right = 160
 theme_override_constants/margin_bottom = 120
 
-[node name="Column" type="VBoxContainer" parent="Margin"]
+[node name="Column" type="VBoxContainer" parent="Margin" unique_id=1756688185]
 layout_mode = 2
 theme_override_constants/separation = 28
 
-[node name="TitleLabel" type="Label" parent="Margin/Column"]
+[node name="TitleLabel" type="Label" parent="Margin/Column" unique_id=1238008540]
 unique_name_in_owner = true
 layout_mode = 2
 theme_override_font_sizes/font_size = 96
 text = "Star Player"
 
-[node name="SubtitleLabel" type="Label" parent="Margin/Column"]
+[node name="SubtitleLabel" type="Label" parent="Margin/Column" unique_id=1126545004]
 layout_mode = 2
 theme_override_font_sizes/font_size = 40
 text = "개발 진단 화면 — Phase 0 환경 · Phase 1 선수 정보 · Phase 2 경기 진단 (게임 기능은 아직 구현되지 않았습니다)"
 
-[node name="VersionLabel" type="Label" parent="Margin/Column"]
+[node name="VersionLabel" type="Label" parent="Margin/Column" unique_id=1373942838]
 unique_name_in_owner = true
 layout_mode = 2
 theme_override_font_sizes/font_size = 36
 text = "빌드 버전: -"
 
-[node name="Buttons" type="HBoxContainer" parent="Margin/Column"]
+[node name="Buttons" type="HBoxContainer" parent="Margin/Column" unique_id=2073735347]
 layout_mode = 2
 theme_override_constants/separation = 32
 
-[node name="StartButton" type="Button" parent="Margin/Column/Buttons"]
+[node name="StartButton" type="Button" parent="Margin/Column/Buttons" unique_id=2118838403]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(480, 110)
 layout_mode = 2
 theme_override_font_sizes/font_size = 44
 text = "진단 시작"
 
-[node name="PlayerDiagnosticsButton" type="Button" parent="Margin/Column/Buttons"]
+[node name="PlayerDiagnosticsButton" type="Button" parent="Margin/Column/Buttons" unique_id=1677808170]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(560, 110)
 layout_mode = 2
 theme_override_font_sizes/font_size = 44
 text = "선수 정보 진단"
 
-[node name="GameDiagnosticsButton" type="Button" parent="Margin/Column/Buttons"]
+[node name="GameDiagnosticsButton" type="Button" parent="Margin/Column/Buttons" unique_id=1086973684]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(520, 110)
 layout_mode = 2
 theme_override_font_sizes/font_size = 44
 text = "경기 진단"
 
-[node name="QuitButton" type="Button" parent="Margin/Column/Buttons"]
+[node name="QuitButton" type="Button" parent="Margin/Column/Buttons" unique_id=979871070]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(480, 110)
 layout_mode = 2
 theme_override_font_sizes/font_size = 44
 text = "종료"
 
-[node name="ResultScroll" type="ScrollContainer" parent="Margin/Column"]
+[node name="ResultScroll" type="ScrollContainer" parent="Margin/Column" unique_id=1420655530]
 layout_mode = 2
 size_flags_vertical = 3
 
-[node name="ResultLabel" type="Label" parent="Margin/Column/ResultScroll"]
+[node name="ResultLabel" type="Label" parent="Margin/Column/ResultScroll" unique_id=50449747]
 unique_name_in_owner = true
 layout_mode = 2
 size_flags_horizontal = 3
diff --git a/game/UI/PlayerDiagnostics/PlayerDiagnosticsScreen.tscn b/game/UI/PlayerDiagnostics/PlayerDiagnosticsScreen.tscn
index 671e11e..1521d61 100644
--- a/game/UI/PlayerDiagnostics/PlayerDiagnosticsScreen.tscn
+++ b/game/UI/PlayerDiagnostics/PlayerDiagnosticsScreen.tscn
@@ -1,8 +1,8 @@
-[gd_scene load_steps=2 format=3]
+[gd_scene format=3]
 
 [ext_resource type="Script" path="res://UI/PlayerDiagnostics/PlayerDiagnosticsScreen.cs" id="1_screen"]
 
-[node name="PlayerDiagnosticsScreen" type="Control"]
+[node name="PlayerDiagnosticsScreen" type="Control" unique_id=1845178902]
 layout_mode = 3
 anchors_preset = 15
 anchor_right = 1.0
@@ -11,7 +11,7 @@ grow_horizontal = 2
 grow_vertical = 2
 script = ExtResource("1_screen")
 
-[node name="Background" type="ColorRect" parent="."]
+[node name="Background" type="ColorRect" parent="." unique_id=591600197]
 layout_mode = 1
 anchors_preset = 15
 anchor_right = 1.0
@@ -20,7 +20,7 @@ grow_horizontal = 2
 grow_vertical = 2
 color = Color(0.09, 0.1, 0.13, 1)
 
-[node name="Margin" type="MarginContainer" parent="."]
+[node name="Margin" type="MarginContainer" parent="." unique_id=870879902]
 layout_mode = 1
 anchors_preset = 15
 anchor_right = 1.0
@@ -32,50 +32,50 @@ theme_override_constants/margin_top = 48
 theme_override_constants/margin_right = 80
 theme_override_constants/margin_bottom = 48
 
-[node name="Column" type="VBoxContainer" parent="Margin"]
+[node name="Column" type="VBoxContainer" parent="Margin" unique_id=202386883]
 layout_mode = 2
 theme_override_constants/separation = 20
 
-[node name="HeaderRow" type="HBoxContainer" parent="Margin/Column"]
+[node name="HeaderRow" type="HBoxContainer" parent="Margin/Column" unique_id=1609523274]
 layout_mode = 2
 theme_override_constants/separation = 32
 
-[node name="TitleLabel" type="Label" parent="Margin/Column/HeaderRow"]
+[node name="TitleLabel" type="Label" parent="Margin/Column/HeaderRow" unique_id=1188465824]
 layout_mode = 2
 size_flags_horizontal = 3
 theme_override_font_sizes/font_size = 52
 text = "선수 정보 진단 (Phase 1)"
 
-[node name="BackButton" type="Button" parent="Margin/Column/HeaderRow"]
+[node name="BackButton" type="Button" parent="Margin/Column/HeaderRow" unique_id=1295313979]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(360, 84)
 layout_mode = 2
 theme_override_font_sizes/font_size = 36
 text = "뒤로"
 
-[node name="SummaryLabel" type="Label" parent="Margin/Column"]
+[node name="SummaryLabel" type="Label" parent="Margin/Column" unique_id=153195244]
 unique_name_in_owner = true
 layout_mode = 2
 theme_override_font_sizes/font_size = 26
 text = "-"
 autowrap_mode = 3
 
-[node name="Body" type="HBoxContainer" parent="Margin/Column"]
+[node name="Body" type="HBoxContainer" parent="Margin/Column" unique_id=454614037]
 layout_mode = 2
 size_flags_vertical = 3
 theme_override_constants/separation = 28
 
-[node name="PlayerList" type="ItemList" parent="Margin/Column/Body"]
+[node name="PlayerList" type="ItemList" parent="Margin/Column/Body" unique_id=1743202337]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(760, 0)
 layout_mode = 2
 theme_override_font_sizes/font_size = 32
 
-[node name="CardScroll" type="ScrollContainer" parent="Margin/Column/Body"]
+[node name="CardScroll" type="ScrollContainer" parent="Margin/Column/Body" unique_id=451532760]
 layout_mode = 2
 size_flags_horizontal = 3
 
-[node name="CardLabel" type="Label" parent="Margin/Column/Body/CardScroll"]
+[node name="CardLabel" type="Label" parent="Margin/Column/Body/CardScroll" unique_id=1692484993]
 unique_name_in_owner = true
 layout_mode = 2
 size_flags_horizontal = 3
@@ -83,7 +83,7 @@ theme_override_font_sizes/font_size = 28
 text = "왼쪽에서 선수를 선택하세요."
 autowrap_mode = 3
 
-[node name="CheckLabel" type="Label" parent="Margin/Column"]
+[node name="CheckLabel" type="Label" parent="Margin/Column" unique_id=637653956]
 unique_name_in_owner = true
 custom_minimum_size = Vector2(0, 220)
 layout_mode = 2
```
