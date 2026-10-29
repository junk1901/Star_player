# B1 G2/G3 전체 진단 보고서 (개발 PC, 공식 원격 `c77ed1a`)

- 작성: Claude Code, 2026-10-29. 근거: OWNER 지시 "Approval A 완료 및 G2/G3 전체 진단 실행"(2026-10-29)
- **성격: 진단입니다.** 공식 G1~G3 PASS가 아닙니다. 운영 신뢰 입력·승인 기록·Build Provenance·Trust Anchor·Pin은 등록하지 않았습니다. Push·Tag·이력 변경·제품 코드 변경은 없습니다.
- **진단 소스:** `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f`(공식 원격 `main`). 코드 검수 소스 `1751fe04aaafbda65462915d2dac89e56536a8ae`.
- **증거 위치(저장소 밖):** `E:\Madegame\g3x\phase3ops\g23\`(Clone별 `A\`·`B\`, `compare.json`, 스크립트), PCK `E:\Madegame\g3x\pck-out\G23A_P1~P5`·`G23B_P1`. 해시는 8절.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. A-1 최초 게시 완료 사실과 보존 증거

- OWNER가 승인 A 완료를 기록했습니다. 원격 `refs/heads/main` = `c77ed1a`, Tag 0개, 다른 Branch 없음(2026-10-29 이 진단 끝에서도 `ls-remote --refs` 결과 같음).
- 기록: [게시 기록](B1_A1_PUBLICATION_RECORD.md), 명령 로그 `E:\Madegame\g3x\phase3ops\push_approvalA_log.txt`.
- 로컬 후속 기록 Commit `e8f8f72`와 이 보고서 Commit은 게시하지 않았습니다.

## 2. 진단 소스와 불변 확인

| 확인 | 결과 |
|---|---|
| `1751fe0..c77ed1a` 변경 파일 | 12개, 모두 문서: `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `docs/reports/` 3개, `docs/tasks/` 7개 |
| 제품·게임·검증기·G4/G5 도구 코드 | 변경 0(`src game tests tools content docs/contracts TASK.md global.json Directory.Build.props *.sln` diff 0줄) |
| 승인 계약·Golden·Hash·RNG·Replay | 변경 없음. 두 Clone의 D3 보호 파일 5개 SHA 일치 |
| 예상 바이너리 차이 | 코드는 같지만 SourceLink Commit(PDB 식별값)이 달라 StarPlayer DLL·PDB만 바뀔 것으로 예상 → 실측에서 B-4 후보 27행(STARPLAYER)만 `1751fe0` 개발 빌드와 다름, 414행 불변(6절) |

## 3. 진단 환경 (실측)

| 항목 | 값 |
|---|---|
| OS | Microsoft Windows 11 Home 10.0.26200 build 26200 |
| PowerShell | 5.1.26100.9711 Desktop |
| .NET SDK | 8.0.425(설치 SDK 1개) |
| .NET Runtime | Microsoft.NETCore.App 8.0.31(그 밖 3.1.32·6.0.11), AspNetCore 8.0.31, WindowsDesktop 8.0.31·6.0.11 |
| Godot | 4.7.2.stable.mono.official.ed1daf0bf |
| Git | 2.45.1.windows.1, `core.autocrlf=true`(system 설정, global 없음) |

## 4. 두 독립 Clone과 G2 진단

| 항목 | Clone A | Clone B |
|---|---|---|
| 경로(길이 다름) | `E:\Madegame\g3x\g23a` | `E:\Madegame\g3x\g23b-independent-official-clone-longer-absolute-path` |
| Clone | `git clone --no-hardlinks https://github.com/junk1901/Star_player.git` → `checkout --detach c77ed1a` | 같음 |
| HEAD | `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f` | 같음 |
| `origin` | `https://github.com/junk1901/Star_player.git`(`-ceq` 일치) | 같음 |
| Tree | `a7b9b937…` = 로컬 게시 Commit Tree | 같음 |
| Clone 시 원격 `main` | `c77ed1a` | 같음 |
| 변경 추적 파일·Tag | 0·0 | 0·0 |
| D3 보호 파일 5개 | 모두 일치 | 모두 일치 |
| **`tools/verify.ps1`** | exit 0, **GATE: PASS**(1,201초) | exit 0, **GATE: PASS**(1,177초) |
| xUnit U1~U6 | 327·741·128(U3a 14, U3b 48)·1,237·538·90 = **3,061 / 0 / 0** | 같음 |
| Godot G1~G4, Export E1·E1b·E2~E4 | 모두 PASS | 모두 PASS |
| D1 문서 링크 | PASS, 257개 파일 | PASS, 257개 파일 |
| D3 | PASS, 5개 | PASS, 5개 |
| verify Export PCK | `3a187e19…`, 13,452 B | 같음 |
| **Golden**(`dotnet test tests\StarPlayer.SimHarness.Tests -c Release --no-build --filter FullyQualifiedName~GoldenTests`) | **4/4 PASS** | **4/4 PASS** |
| **Dry Pilot 9회**(보관 Run-a Manifest와 비교) | **9/9 동일**: `config_hash`·`run_result_hash`·`environment` 외 Manifest 전체 | **9/9 동일**, 두 Clone의 9행도 서로 같음 |

- **Dry Pilot 입력:** 원본 보관 Archive `E:\Madegame\Star_player\build\verify-archive\PHASE_3F_FC2_PZL_DRY_PILOT_dry_run1`(130개 파일, 목록 SHA `be6f927d…`)을 각 Clone `build/`에 복사했습니다. 다른 두 사본(`g4clean2`, `Star_player_b1_verify`)과 바이트 동일함을 확인했습니다.
- **Dry Pilot에 쓴 빌드:** 5절 CI 빌드의 SimHarness(`b22d46a1…`, 두 Clone 같음)입니다. 처음 실행에서 진단 스크립트가 Seed 경로를 `\`로 넘겨 SimHarness가 `Seed.path_invalid`로 거부했고, 이어진 PCK 실행이 `bin/obj`를 지운 뒤라 CI 빌드 후 다시 실행했습니다. 첫 시도의 SimHarness 출력 로그(`A\drypilot_fc2-dry-cal-na.log`)는 재실행으로 덮여 보존되지 않았습니다. 실패 사실은 `run_all.log`에, 원인 문구(`Seed.path_invalid`: 저장소 기준 `/` 구분 상대 경로여야 함)는 이 보고서에 남깁니다.
- verify 로그 변경(`docs/reports/PHASE_2_VERIFY_LOG.md`)은 사본을 남긴 뒤 각 Clone에서 되돌렸습니다. 최종 두 Clone 모두 변경 0건입니다.

## 5. G3 CI 빌드 재현성 진단

- **명령(두 Clone 동일):** `dotnet build StarPlayer.sln -c Release --disable-build-servers -nr:false -p:UseSharedCompilation=false -p:ContinuousIntegrationBuild=true`
  - 지시문의 `-nr -p=false -p=true`는 문서의 이 명령이 표기 과정에서 줄어든 것으로 보고, A-1 문서 6절 3단계 명령을 그대로 썼습니다.
  - 환경: `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `UseSharedCompilation=false`, `DOTNET_CLI_UI_LANGUAGE=en`
  - 빌드 전: 저장소 안 모든 `bin`·`obj` 삭제(`.git` 제외)
- **결과:** 두 Clone 모두 exit 0, 13개 프로젝트, **Warning 0 / Error 0**(17·18초)
- **전체 출력 비교(모든 `bin\Release` 파일, 상대경로·크기·SHA-256, Ordinal):**

| 항목 | 결과 |
|---|---|
| 파일 수 | A 793, B 793. 한쪽에만 있는 파일 0 |
| 바이트 동일 | **787개** |
| 다른 파일 | **6개**: 테스트 출력의 `.msCoverageSourceRootsMapping_<시험 프로젝트>`. 내용에 Clone 절대경로가 들어가 크기·SHA가 다름 |
| 해석 | 6개는 B-4 Artifact 규칙 v2가 이미 제외하는 Coverage 대응표(A9)입니다. Artifact 후보 441행은 두 Clone에서 완전히 같습니다(6절) |

- **DLL/PDB:** StarPlayer 어셈블리 55개(테스트 출력 사본 포함) 모두 두 Clone에서 DLL·PDB SHA, PDB ID, SourceLink가 같습니다. 전부 DLL의 CodeView GUID+stamp = PDB ID(결속 확인), CodeView 경로는 `/_/…`(Clone 경로 미포함)입니다. PDB 덤프 파일 SHA도 두 Clone이 같습니다(`f6789cf2…`).
  - `tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.EvidenceVerifier.dll`: DLL `33e694ee…`, PDB `c42e0046…`, PDB ID `cb08a894…`
  - `tools/StarPlayer.SimHarness/bin/Release/net8.0/StarPlayer.SimHarness.dll`: DLL `b22d46a1…`, PDB `4358acc0…`, PDB ID `d922caae…`
  - 이 값은 **진단값**입니다. 공식 값은 승인 소스 Commit·공식 빌드 환경에서 다시 측정합니다.
- **이전 개발 빌드 SHA는 재사용하지 않았습니다.** 모든 값은 이번 두 Clone의 실측입니다.

## 6. SourceLink 확인

| 조건 | 결과 |
|---|---|
| 공식 URL 대소문자 | Clone `origin` = `https://github.com/junk1901/Star_player.git` |
| SourceLink JSON | 55개 어셈블리 모두 `{"documents":{"/_/*":"https://raw.githubusercontent.com/junk1901/Star_player/c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f/*"}}`. 값 종류 1개 |
| Commit = `c77ed1a` 전체 SHA | 일치. Verifier·SimHarness PDB 같음 |
| 두 Clone의 SourceLink | 같음 |
| 원격 파일 실제 조회 | PDB 문서 445개 중 저장소 추적 원본 **372개를 모두 Raw URL로 조회, 바이트 = Git Blob**. 나머지 73개는 빌드가 만든 `obj/` 생성 파일과 NuGet SDK 파일 1개(`/_1/…`)로, 저장소에 없어 404가 정상입니다 |
| 이전 비공개 후보 URL 문자열 | 793개 출력 파일 어디에도 없음(`star-player-official`, `Star_player_official`) |

- **[새 발견] 줄바꿈 설정이 빌드 입력입니다.**
  - PDB의 문서 체크섬은 **체크아웃 파일**의 해시입니다. 이 PC는 `core.autocrlf=true`이므로 체크아웃이 CRLF입니다. 그래서 저장소 원본 문서 372개 중 PDB 체크섬이 Raw(LF Blob)와 같은 것은 71개뿐이고, 301개는 다릅니다. 체크아웃 파일과는 411개 모두 같습니다.
  - 따라서 다른 `core.autocrlf` 설정으로 Clone하면 PDB 바이트가 바뀌고, PDB 체크섬이 DLL에 들어가므로 **DLL 바이트도 바뀝니다**. 공식 빌드 절차는 이 값을 고정해야 합니다.
  - B-4 7절의 원격 출처 검증(Raw ↔ Blob, PDB 체크섬 ↔ 체크아웃 파일)은 두 조건 모두 통과합니다. 다만 디버거가 SourceLink로 받은 파일은 301개에서 체크섬 불일치를 보고합니다.

## 7. B-4 Artifact 후보 비교 (`b4-candidates.ps1`, 미승인)

| 항목 | Clone A | Clone B |
|---|---|---|
| 상태 | `CANDIDATE_INCOMPLETE` | `CANDIDATE_INCOMPLETE` |
| 행 수 | 441 | 441 |
| 역할별 | SIMHARNESS_BINARY 1, VERIFIER_BINARY 1, TEST_ASSEMBLY 4, RUNTIME_CONFIG 24, DEPENDENCY 411 | 같음 |
| 주 바이너리 6개 | SimHarness·Verifier·Domain/Simulation/Infrastructure/SimHarness Tests 각 1 | 같음 |
| 상대경로·크기·SHA | **441행 모두 A = B** | |
| 누락·중복·Hardlink·Reparse | 도구 검사 통과(종료 코드 0) | 같음 |
| 후보 밖 출력 파일 | 31개 = PDB 27개(A8, 두 Clone 같음) + Coverage 대응표 4개(A9, 경로 포함이라 두 Clone 다름) | 같은 31개 경로 |
| `1751fe0` 개발 빌드 후보 대비 | 추가 0, 삭제 0, 변경 27(모두 STARPLAYER), 불변 414 | 같음 |

- 441행은 예상과 같았습니다. TRUST_CONFIG 경로 루트와 외부 Pin은 OWNER 미확정 그대로 두었습니다.

## 8. PCK 5회 재현성

`E:\Madegame\g3x\tools\run_pck.ps1`(기존 G3 진단 도구, `3d9df1ec…`)로 실행했습니다. 매회 `bin`·`obj`·`game/.godot`·`build/export`를 지우고, 환경에 `ContinuousIntegrationBuild=true`를 둔 채 `godot --headless --path game --build-solutions --quit` → `godot --headless --path game --export-release "Windows Desktop" ../build/export/StarPlayer.exe`를 실행합니다.

| 반복 | Clone | Godot | 종료 | PCK 크기 | PCK SHA-256 |
|---|---|---|---|---|---|
| 1 | A | 4.7.2.stable.mono.official.ed1daf0bf | build 0, export 0 | 13,452 | `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799` |
| 2 | A | 같음 | 0·0 | 13,452 | 같음 |
| 3 | A | 같음 | 0·0 | 13,452 | 같음 |
| 4 | A | 같음 | 0·0 | 13,452 | 같음 |
| 5 | A | 같음 | 0·0 | 13,452 | 같음 |
| 추가(다른 경로) | B | 같음 | 0·0 | 13,452 | 같음 |

- **5회 SHA 전부 일치**, 기존 참고값과도 같습니다. Export 폴더 198개 파일 전체 목록 SHA도 6회 모두 같았습니다. 두 Clone의 `verify.ps1` Export PCK도 같은 값입니다(총 8회 일치).
- 소스 Commit `c77ed1a`, 작업 트리 변경 0건(각 `meta.json`).

## 9. G1 출력 이름·경로 진단과 확정 제안

기준: [B-4 빌드 절차 초안](../tasks/B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md) 6절 표, [운영 Gate 계획](../tasks/B1_OPERATIONAL_GATE_PLAN.md) G1 수용 기준(같은 경로를 생산자·검증기·시험이 쓰고, Hash·Replay 불변, 이름 정의 한 곳).

**표시:** ① 현행 구현과 일치 ② 문서에서만 확정 가능 ③ 코드 변경 필요 ④ 운영 Root·ACL 결정 필요 ⑤ 다른 OWNER 규범 결정 필요

| 항목 | 현행 구현(정의 위치) | 표시 | 제안 |
|---|---|---|---|
| `run/<run_id>/` 내부 구조 | `EvidenceWriter.RunPath`(한 곳). 검증기가 같은 함수 사용 | ① | 그대로 확정 |
| Family Preflight | `run/<run_id>/preflight.json`(`OfficialOutput.PreflightFile`) | ① | 그대로 |
| Unit Preflight | `run/<run_id>/preflight/<unit 소문자>.json`(`PreflightDirectory`·`UnitPreflightName`) | ① | 그대로 |
| Unit Manifest | `run/<run_id>/units/<unit>/manifest.json`(Family), `…/cells/<cell>/manifest.json`(Dominance) | ①, 단 `manifest.json` 문자열이 `RunReport`·`FamilyOfficial`·`DominanceOfficial`·`EvidenceWriter`·`Verifier` 여러 곳에 있음 | 이름은 그대로 확정 권고(결과 문서의 `manifest_path`에 들어가므로 바꾸면 내용·Hash가 바뀜). "한 곳 정의" 기준을 문자 그대로 적용하면 상수 추출 정리(③, Hash 불변)가 필요 — OWNER 판단 |
| 실행 색인(DIAGNOSTICS) | `run/<run_id>/family-result.json`·`dominance-result.json`(`FamilyOfficial.ResultFile`·`DominanceOfficial.ResultFile`) | ① | 그대로 |
| Unit 증거 | `units/<unit>/game-events.jsonl`·`game-results.jsonl`·`unit-pzl.json`, `inputs/<seed 경로>`(`EvidenceWriter`) | ①(이름) | 그대로. 단 아래 Archive 조립 참고 |
| `FAMILY_RESULT` 문서 | 생산 코드에 고정 이름 없음(TEST_ONLY 호출자 지정) | ②+③ | 제안 `calibration-result.json`·`integration-result.json`(Archive 루트) |
| `DOMINANCE_RESULT` 문서 | TEST_ONLY `dominance-result.json` | ②+③ | 그대로 |
| PZL6 | TEST_ONLY `pzl6-<stem>.json`, `pzl6/<stem>/<unit>-NN.json`(생산 `Archive`는 Artifact ID 형태만 검사) | ②+③ | 그대로 |
| Regression Proof | TEST_ONLY `trust/regression-proof-<guid>.json` | ②+⑤ | 제안 `proof/<pzl_test_profile_id>.json`(Test Profile ID 규범 필요) |
| Trust 관련 파일 | TEST_ONLY `trust/approval-record.json`·`build-provenance.json`·`environment-profile.json`·`pzl-test-profile.json` | ⑤ | Registry·R-TRUST와의 관계, TRUST_CONFIG `relative_path` 루트([충돌], B-4 초안 4절)를 OWNER가 정한 뒤 |
| 검증기 산출 | `verifier/<receipt_id>/raw-inventory.json`·`canonical-payload.json`(`Certification`), `receipts/<id>.json`(`Receipts`) | ① | 그대로 |
| Archive Root | 없음(운영 절대경로) | ④ | G5 R-ARCHIVE(B-2·B-9) |
| Build Root | 없음 | ④ | G5 R-BUILD `\<source_git_sha>\`, Provenance `relative_path` 루트 |
| 생산자 결과 폴더 | `build/simharness/official/<run_id>`(`OfficialOutput.RootRelative`, 저장소 상대, 그 밖 경로 거부) | ①+④ | 아래 발견 2 |
| 상대경로 규칙 | `Archive.WriteNew`: `/` 구분, `..`·Reparse 거부, 덮어쓰기 금지, 쓴 바이트 재확인 | ① | 그대로 |

**G1 확정 제안:** ①로 표시한 현행 이름·배치를 G1 공식 이름으로 채택합니다. 코드 변경이 없으므로 Hash·Replay·계약 §21은 그대로입니다. ②는 문서 확정, ③·④·⑤는 아래 결정이 필요합니다.

**G1 진단에서 새로 확인된 사실:**

1. **Archive 조립 생산 명령이 없습니다.** `EvidenceWriter.WriteUnits`·`WriteSeeds`·`WriteRun`과 FAMILY_RESULT·PZL6·승인 사본을 Archive에 쓰는 호출은 TEST_ONLY `tests/StarPlayer.EvidenceVerifier.Tests/TestOnlyWorld.cs`에만 있습니다. 생산 SimHarness CLI(`official`·`official-cal`·`official-int`·`official-dom`)는 결과 폴더만 씁니다. 검증기는 Archive를 읽고 `verifier/…`·`receipts/…`만 씁니다. 공식 실행 증거를 만들려면(G6 이후) 조립 명령 추가(별도 TASK·Codex 검수) 또는 OWNER 운영 규범이 필요합니다. 기존 Hash·Replay와는 무관합니다.
2. **생산자 결과 폴더가 저장소 안에만 허용됩니다.** `OfficialOutput.Resolve`는 `<저장소>/build/simharness/official/` 밖을 거부합니다. G5 설계는 R-BUILD(검증 대상 빌드 체크아웃)를 봉인 후 생산자 쓰기 금지로 둡니다. 따라서 생산자는 R-BUILD 밖의 별도 체크아웃에서 실행하거나, 출력 루트 규칙을 바꿔야 합니다(④ 또는 ③).

## 10. 추천

- **공식 A1-6 소스 Commit: `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f` 권고.**
  - 공식 원격 `main` 자체이고, 이번 진단의 모든 실측(두 Clone, SourceLink, Artifact 441행, PCK)이 이 Commit에 결속되어 있습니다.
  - 제품 코드는 `1751fe0`·`a980ea5`와 같습니다. `1751fe0`을 고르면 SourceLink Commit이 달라져 StarPlayer 27행을 다시 측정해야 합니다.
  - 이후 문서 Commit을 게시해도 `c77ed1a`는 조상으로 남아 SourceLink 조회가 됩니다.
- **공식 빌드 장소: 운영 기계의 R-BUILD 스테이징(배포 계정), 승인 B 뒤.**
  - CI 빌드 바이트는 경로와 무관합니다(두 Clone 실측). 따라서 운영 기계에서 같은 SDK·NuGet·Git 설정으로 빌드하면 이번 진단값과 같아야 하고, 이번 값을 독립 대조 기준으로 쓸 수 있습니다.
  - 개발 PC에서 빌드해 복사하는 방식은 보관 사슬이 끊기므로 권하지 않습니다.
  - 고정할 빌드 입력: Clone URL 문자열, `source_git_sha`, .NET SDK 8.0.425, NuGet 복원 결과, **`core.autocrlf`**, CI 명령·환경 변수.

## 11. 새로 발견된 차이·차단 조건

| # | 내용 | 영향 | 차단 여부 |
|---|---|---|---|
| N-1 | `core.autocrlf`가 PDB 문서 체크섬과 DLL·PDB 바이트를 정합니다(6절) | 공식 빌드 재현성 입력 | 공식 G3 전에 값 고정 필요(OWNER) |
| N-2 | Coverage 대응표 6개가 Clone 경로를 담아 다릅니다 | 이미 A9로 Artifact 밖 | 차단 아님 |
| N-3 | 생산 Archive 조립 명령 없음(9절 발견 1) | G6 이후 공식 증거 생성 | G1·G6 전 결정 필요 |
| N-4 | 생산자 출력이 저장소 안으로 제한(9절 발견 2) | R-BUILD 봉인과 충돌 | 승인 B·G1 결정 필요 |
| N-5 | `manifest.json` 문자열 다중 정의 | G1 "한 곳 정의" 기준 | OWNER 판단(정리 시 Hash 불변) |

- **진단 작업 중 생긴 일(기록):**
  - 진단 스크립트의 Seed 경로 오류로 Dry Pilot 첫 실행이 실패했고, 수정 후 재실행했습니다(4절).
  - 이전 작업(2026-10-28)에서 `g4clean3`에 `git clean -fdx`를 실행할 때, 그 Clone `build/`에 복사해 둔 Dry Pilot Archive 사본도 함께 지워졌습니다. 당시 보고서는 "빌드 산출물만" 지웠다고 적었습니다. 원본(`E:\Madegame\Star_player\build\verify-archive\…`)과 다른 사본 2개는 그대로이며 바이트 동일합니다(4절).
  - PDB 문서 체크섬을 읽으려고 저장소 밖 진단 도구 `pedump`의 **사본**(`phase3ops\g23\pedump2`)에 필드 하나를 더했습니다. 원래 도구(`E:\Madegame\g3x\tools\pedump*`)는 바꾸지 않았습니다.
- **새 MAJOR 보안 문제·승인 계약 충돌:** 없습니다.

## 12. 공식 G1~G3 실행에 남은 OWNER 결정

| # | 결정 | 권고 |
|---|---|---|
| D-1 | 공식 소스 Commit(A1-6) | `c77ed1a` |
| D-2 | 공식 빌드 장소·계정 | 운영 기계 R-BUILD 스테이징, 배포 계정(승인 B 뒤) |
| D-3 | `core.autocrlf` 고정값(N-1) | `true`로 명시 고정(`git clone -c core.autocrlf=true …`). 지금까지의 모든 수용 증거(PCK·Golden·이번 진단)가 이 값입니다. `false`를 원하면 같은 진단을 다시 해야 합니다 |
| D-4 | G1 이름: ① 채택, ② 문서 확정 | 9절 표 그대로 |
| D-5 | `manifest.json` 다중 정의(N-5) | 이름 유지. 상수 정리는 선택(별도 TASK) |
| D-6 | Archive 조립 명령(N-3) | 별도 TASK로 추가 후 Codex 검수(G6 전) |
| D-7 | 생산자 출력 위치(N-4) | 생산자 전용 체크아웃(R-BUILD 밖)으로 운영. 코드 변경 없이 가능 |
| D-8 | Trust 파일·Regression Proof 이름, TRUST_CONFIG 루트 | B-4 규범 결정과 함께 |
| D-9 | G2·G3 공식 실행 승인 | 위 결정 뒤 운영 기계에서 이 보고서 절차 재실행 → Codex 검수 → OWNER 수용 |

## 13. 증거 해시 (SHA-256)

| 파일(`E:\Madegame\g3x\phase3ops\g23\`) | SHA-256 |
|---|---|
| `A\summary.json` | `b00fe11ac7320b4392a653dd61fce2d9b841033f961f951c1393990fe8dd9c90` |
| `B\summary.json` | `27a9aeaf3e3e661b624d723b078c2b730a3a75a34a81a9ce27895a0482c648ee` |
| `compare.json` | `82a0f46ad1742583145d24cd55aebecad2b53bb6bef387aa08a8553f267d410d` |
| `A\ci_manifest.tsv` / `B\ci_manifest.tsv` | `2bff0918…` / `2ab9490b…`(Coverage 대응표 6행만 다름) |
| `A\ci_pdb.json` = `B\ci_pdb.json` | `f6789cf24c8a620faa4bb9f47a227cdfa5041eb04683a639a12c190cea7d4135` |
| `A\b4_candidates.json` / `B\b4_candidates.json` | `f54f77c3…` / `55d9fabe…`(`source_build` 경로·Coverage 대응표만 다름) |
| `A\PHASE_2_VERIFY_LOG.md` / `B\…` | `c0bbd00e…` / `fdf41f77…` |
| `A\verify_stdout.log` / `B\…` | `52052201…` / `76e54568…` |
| `A\ci_build.log` / `B\…` | `a954e6cd…` / `55599faa…` |
| `A\golden.log` / `B\…` | `1ab3a786…` / `8079e241…` |
| `g23_clone_pipeline.ps1` | `b574d7e0…` |
| `g23_compare.ps1` | `018982c2…` |
| `g23_run_all.ps1` · `run_all.log` | `649cf2f1…` · `f0ffef25…` |
| `pedump2\Program.cs` · `pedump2-bin\pedump.dll` | `635cd043…` · `d6911d81…` |
