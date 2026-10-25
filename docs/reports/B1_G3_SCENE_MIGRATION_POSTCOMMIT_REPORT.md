# B1 G3 Scene Migration — Commit 이후 깨끗한 체크아웃 검증 (미커밋 기록)

- 작성: Claude Code, 2026-10-26. 근거: OWNER 결정 "G3 Scene Migration Commit 승인 및 Clean Checkout 검증"(2026-10-26)
- **결론:**
  - Scene Migration 로컬 Commit **`00091736d5386aec3f5c67cf159291976e7133a2`**을 만들었습니다(부모 `cd5f413`).
  - 그 Commit을 직접 체크아웃한 깨끗한 clone에서 모든 검증 조건을 충족했습니다.
  - 새 Tag는 만들지 않았습니다. Remote Push는 없습니다.
- **이 문서와 verify 로그 사본은 Commit 범위 밖이라 커밋하지 않았습니다.**
- 공식 G3 PASS, Build Freeze, Official Execution, Phase 3 완료가 아닙니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. Commit

| 항목 | 값 |
|---|---|
| Commit | `00091736d5386aec3f5c67cf159291976e7133a2`, 부모 `cd5f4137af562a99927d8cb549a07cd6b9c39bf6` |
| 파일(11) | 수정 5: `.gitattributes`, `game/Bootstrap/Main.tscn`, `game/UI/MainMenu/MainMenu.tscn`, `game/UI/PlayerDiagnostics/PlayerDiagnosticsScreen.tscn`, `game/UI/GameDiagnostics/GameDiagnosticsScreen.tscn` |
| | 추가 6: `docs/tasks/B1_G3_SCENE_MIGRATION_CODEX_REVIEW_REQUEST.md`, `docs/tasks/B1_G3_SCENE_MIGRATION_TASK.md`, `docs/reports/B1_G3_SCENE_MIGRATION_REPORT.md`, `docs/reports/B1_G3_SCENE_MIGRATION_MANIFEST.md`, `docs/reports/B1_G3_SCENE_MIGRATION_MANIFEST.sha256`, `docs/reports/B1_SCENE_MIGRATION_VERIFY_LOG.md` |
| Stage | 위 11개 경로만 명시해 Stage(`git add -A` 미사용). Stage 후 남은 변경 0, blob = 작업 트리 바이트 11/11 |
| Stage 전 검사 9개 | 모두 PASS: Scene 검수 SHA, 51개 `unique_id`, 허용 diff만, LF 정책, Scene Manifest 5/5, D3 5/5, Tag·역사적 Manifest, 민감정보 0, 대상 외 변경 0 |
| Codex NOTE 반영 | 보고서 5절의 warm 조건을 정정했습니다. 정확한 조건: Import Cache(`game/.godot`)·`bin/obj` 유지, `build/export` 삭제 후 재생성. 원시 로그와 Codex 원문은 수정하지 않았습니다 |

## 2. 깨끗한 체크아웃 검증

- **방식:** `git clone --no-hardlinks E:/Madegame/Star_player` → HEAD `00091736…`(덮어쓰기 없음)
- **clone 위치:** `E:\Madegame\g3x\nv`(verify), `npa`·`npblonger`(PCK), `nci`(CI 빌드), `ntag`(역사적 Tag)
- `core.autocrlf=true`, 실행 전 `git status` 0행

### 2.1 소스 무결성

| 검사 | 결과 |
|---|---|
| Commit SHA | `00091736d5386aec3f5c67cf159291976e7133a2` |
| 작업 트리 | clean(0행). verify 후에는 verify 로그 1개만 미추적 |
| Scene Manifest | **5/5** |
| `unique_id` | 51개 모두 있고 51개 모두 다름(Main 1·MainMenu 14·PlayerDiagnosticsScreen 13·GameDiagnosticsScreen 23), CR 0 |
| `.gitattributes` LF 정책 | 규칙 있음, Scene 4개 `i/lf w/lf attr/text eol=lf`. 실제 체크아웃(`core.autocrlf=true`)에서도 LF |
| D3 | **5/5** |
| 역사적 Tag | `b1-code-accepted-2026-10-26` → `7bdde7dd…`(annotated, 변경 없음) |
| 역사적 Manifest 213개 | Tag를 체크아웃한 `ntag`에서 **213/213**. Manifest 파일 자체도 Tag와 같음(수정 없음) |

### 2.2 전체 검증 (`nv`)

**`tools/verify.ps1`:**

- 2026-10-26 06:50~07:07 KST, exit 0, 로그의 `Git: 0009173 (clean)`
- **GATE PASS**

| 검사 | 결과 |
|---|---|
| xUnit | U1 327, U2 741, U3 128(U3a 14·U3b 48), U4 1,237, U5 538, U6 70 → **3,041 PASS / 0 FAIL / 0 SKIP** |
| Golden 4 | `--filter Evidence.GoldenTests`로 따로 실행해 **4/4 PASS**(Dominance·Integration·Calibration·Phase) |
| Dry Pilot 9 Run | **`ALL_9_IDENTICAL=True`**(9행 모두 True). verify 기본 빌드 DLL `123c213a…`(경로 의존 기본 빌드 값, Pin 아님) |
| G1~G4 | PASS. G2 8/8, G3 `hidden_identifier_leaks=0`, G4 `games=6 finished_with_box_score=6 self_check=PASS` |
| E1·E1b | PASS |
| E2 | PASS, 8/8, 스크린샷 |
| E3 | PASS, **`자체 점검: 통과 (7/7)`**, **`hidden_identifier_leaks=0`**, 스크린샷 2개 |
| E4 | PASS, `자체 점검: 통과 (7/7)`, **`games=6 finished_with_box_score=6 self_check=PASS`**, 스크린샷 2개 |
| 진단 텍스트 6개(headless 3, Export 3) | 모두 원본(`7bdde7d` 깨끗한 clone 검증 `E:\Madegame\Star_player_b1_verify3`)과 텍스트 동일 |
| 스크린샷 5개 | `exported-shell.png` `5388d19d…`, `exported-player-card.png` `4ede6a12…`, `exported-player-card-npc.png` `c8c70b5f…`, `exported-game-screen.png` `4455e6ec…`, `exported-game-screen-end.png` `1f26329a…`. **모두 원본과 바이트 동일** |
| Godot ERROR/WARNING | G1~G4·E1~E4 원시 로그 0행. Exit Code 모두 0 |
| D1 | PASS(222개 문서) |
| D3 | PASS(5개) |

- Golden 기대값은 갱신하지 않았습니다.
- 원시 결과는 `E:\Madegame\g3x\nv\build\verify\`에 있습니다. verify 로그 사본은 [B1_SCENE_MIGRATION_POSTCOMMIT_VERIFY_LOG.md](B1_SCENE_MIGRATION_POSTCOMMIT_VERIFY_LOG.md)(미커밋)입니다.

### 2.3 PCK 재현성 (새 Commit의 깨끗한 체크아웃)

| ID | 조건 | `StarPlayer.pck` SHA-256 | byte_length | 나머지 Export 파일 |
|---|---|---|---:|---|
| N_A1 | `npa`, 같은 경로 1회 | `3a187e19e7737c3329d81a2e76fe357c30f117c9033af5fcfc3706ea0c49e799` | 13,452 | 197개, 누락·추가·SHA 차이 0 |
| N_A2 | `npa`, 같은 경로 2회 | 같음 | 13,452 | 같음 |
| N_C_warm | `npa`, Import Cache·`bin/obj` 유지, `build/export` 재생성 | 같음 | 13,452 | 같음 |
| N_B1 | `npblonger`, 다른 경로 1회 | 같음 | 13,452 | 같음 |
| N_B2 | `npblonger`, 다른 경로 2회 | 같음 | 13,452 | 같음 |

- 5개 PCK가 N_A1과 **전체 바이트 동일**합니다. `node_ids` 제외 비교나 사후 패치는 없습니다.
- 나머지 Export 파일 197개는 후보 검증(M_A1) 때와 같습니다.
- N_A1의 실행 검사(G2~G4·E2~E4)도 exit 0과 PASS 표시였습니다.

## 3. 새 소스 Commit과 Provenance 구분 (등록·변경 없음)

**진단 CI 빌드:**

- clone `nci`, Commit `00091736…`, 실험 통제 원격 `https://github.com/junk1901/Star_player.git`
- 명령: `dotnet build StarPlayer.sln -c Release … -p:ContinuousIntegrationBuild=true`
- 출력 파일 793개
- **진단 SHA이며 공식 운영 Pin이 아닙니다.**

| 어셈블리 | DLL SHA-256(906,752 B 등) | PDB SHA-256 | SourceLink | IL·메타데이터(MVID 제외)가 `7bdde7d` CI 빌드와 같은가 |
|---|---|---|---|---|
| StarPlayer.SimHarness | `a085d6138e0bb46b48bf27c653fa236d6bd583678f7022c273ea6f7dc2919c90` | `79560f276f621de8e334c7f5b1a10e99f52508bcecf29f252c57ea41b64c42a9` | `/_/*` → `…/00091736d5386aec3f5c67cf159291976e7133a2/*` | 같음 |
| StarPlayer.EvidenceVerifier | `04a2c71c6c7593f57b615b030fd0506e0210a068ce4fcf64f654a5f8971b6239` | `160b52d76c8d45ee2fe42c72b37631529d1454e8542e14bac6fbfef780fbe25f` | 같음 | 같음 |
| StarPlayer.Domain | `3eed084ee9e4bd79fd35d6187c76aae94dc02e61eb7a7ecb648deb92e6cd7fdb` | `406e5047145b161369b57dd4ade2eaa1d7f3bf96ac86173fafce7554fadd66d4` | 같음 | 같음 |
| StarPlayer.Simulation | `ec44885878ab09123ad1ba6c7beb47cb477d6d04d24d7b999de01c154ab54a1d` | `213caca98186664168c48a324013aa09de3b1b406567e9ef070d9c8b3da018f2` | 같음 | 같음 |
| StarPlayer.Client | `6cbc244de0412e280c69a7ea14ee20a047e8e2038d93ec7527d1ddf1b72722b1` | `7af8c929c2343734c9ce9cf3ef09787710111dcc0d811c61e947b7edb02757cf` | 같음 | 같음 |

- **전체 비교:**
  - StarPlayer 어셈블리 55개 모두 SourceLink Commit이 `00091736…` 하나입니다. IL은 55개 모두 `7bdde7d` CI 빌드(S1)와 같습니다.
  - `7bdde7d` CI 빌드와 다른 출력은 116개입니다: StarPlayer DLL 55·PDB 55(SourceLink Commit과 그 파생 식별자 = 출처 차이), coverage 대응표 6개(빌드 경로 차이).
- **값 구분:**
  - **새 `source_git_sha`·Build Provenance `source_git_sha` 후보:** `00091736d5386aec3f5c67cf159291976e7133a2`
  - **SourceLink에 기록된 SHA:** 위와 같음(진단 빌드)
  - **DLL/PDB·VERIFIER_BINARY SHA:** 위 표(진단 빌드). 공식 원격 URL·절차 확정 후 다시 계산해야 합니다.
  - **Test Profile 소스 결속·ApprovalRecord 승인 Commit:** 바꾸지 않았습니다. 새 Commit을 쓰려면 다시 결속해야 합니다.
  - **기존 B1 Tag 대상 `7bdde7d`:** 역사적 코드 수용 기준본입니다(새 Commit과 다른 소스 상태).
- **미확정 유지:** 승인 SourceLink URL, B-4 Clause ID, 공식 출력 경로, TRUST_CONFIG, Apphost 규칙, §18.2 Environment Profile

## 4. Codex 검수 증거

- **원문 확보:** 로컬 Codex 세션 기록 `C:\Users\user\.codex\sessions\2026\10\04\rollout-2026-10-04T21-41-07-01a106ee-aab7-7a10-8f22-d304b84ba8ee.jsonl`(기존 C01~C10과 같은 파일)에서 `payload.content[0].text`를 바이트 그대로 추출했습니다.
- **보존 준비:** 이번 Commit 범위 밖이라 저장소에 넣지 않고 `E:\Madegame\g3x\codex-originals-prep\`에 두었습니다.

| 준비 ID | 내용 | 원본 행 | 기록 시각(UTC) | 바이트 | SHA-256 | 원본 행 SHA-256 |
|---|---|---:|---|---:|---|---|
| C11 | Codex 요청(user): "G3 Scene Migration 최종 READ-ONLY 독립 검수" | 12998 | 2026-10-25T21:36:01.207Z | 7,246 | `3f4133e908c49353ab54288302a666e842526a9ec33826ea88249185b6d5ae0f` | `cd2aca90eaf7323b3267a81141968b48c622adb3e9716d0514eda9b36709841b` |
| C12 | Codex 판정(assistant): "최종 Verdict: PASS", MAJOR 0·MINOR 0·NOTE 1 | 13174 | 2026-10-25T21:43:55.185Z | 8,702 | `02497573d5185fbc1f84d6e74e989dae506162471f170e7e3d20541ae78c9df1` | `894b1e98679d7e4a6212b76698ab715d8629aa8de69f27595b2692ee848c27c1` |

- **비밀정보 검사:** 0건입니다.
- **출처:** NOT_INDEPENDENTLY_VERIFIED(로컬 기록)입니다. 저장소 반영과 원문 색인 추가는 별도 OWNER 허가 대상입니다.
- **증거 구분:**
  - C12는 **Commit 전 후보**(역사적 Commit에 후보 파일을 적용한 clone 기준)에 대한 READ-ONLY 검수입니다. C12 원문 7절이 "아직 존재하지 않는 새 소스 Commit의 바이너리·Provenance까지 검증한 것은 아니다"라고 적고 있습니다.
  - 이 문서 2·3절은 **새 Commit 이후 Claude가 실행한 검증**입니다. Codex가 확인한 것이 아닙니다.

## 5. 남은 운영 Gate

- G1~G9: 모두 미승인·미실행
- G3: 공식 PASS가 아닙니다. 이번 결과는 진단 증거입니다.
- Build Freeze 없음, Official Execution 비활성
