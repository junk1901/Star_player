# B1 G5-TEMP-01 보완 C-3 구현 보고서 — Snapshot 바이트 Replay

- 작성: Claude Code, 2026-10-30. 근거: OWNER 지시 "G5-TEMP-01 코드 수정 방식(C-3) 전환"(2026-10-30)
- **성격:** OWNER가 허용한 범위의 최소 코드 수정·격리 시험·로컬 Commit입니다. Push·Tag·계약·`.gitattributes`·Golden·Hash·RNG·Replay 기준값 변경, 운영 ACL·계정·예약 작업, Trust Anchor·Pin 등록, 공식 인증, 공식 Gate PASS, Build Freeze는 **없습니다**. Codex 검수 전이므로 G5-TEMP-01은 **OPEN_MAJOR**입니다.
- 작업 위치: 새 Worktree `E:\Madegame\Star_player_c3`, 새 Branch `phase3/g5-temp01-c3`(시작점 `7043bb90bb47bfa83d7f4a4e068b5b8ae78e54f0`). 기존 `phase3/g4-official-entry`와 그 Commit·외부 증거는 그대로입니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · G5-TEMP-01 = **OPEN_MAJOR**

## 1. 변경 파일과 Commit

| 항목 | 값 |
|---|---|
| 코드 Commit | **`8ca9705539bc941919f5290de149c4678f5147dd`** (Tree `fd87de669127228dd15cbe9cbc85272326a65ff7`) |
| 문서 Commit | 이 보고서를 담은 Commit(`8ca9705`의 자식) |
| 제품 코드(3개) | `tools/StarPlayer.EvidenceVerifier/Verifier.cs`, `tools/StarPlayer.SimHarness/FamilyOfficial.cs`, `tools/StarPlayer.SimHarness/DominanceOfficial.cs` — 공백 무시 diff +27/−30 |
| 시험(3개) | 새 `tests/StarPlayer.EvidenceVerifier.Tests/T11CertificationTests.G5Temp01.cs`(T-2~T-7), `tests/StarPlayer.SimHarness.Tests/FamilyOfficialTests.cs`·`DominanceOfficialTests.cs`(각 1건, 바이트·경로 Replay 동등성) |
| 바꾸지 않은 것 | `src`, `game`, `content`, `docs/contracts`, `TASK.md`, `.gitattributes`, `global.json`, `Directory.Build.props`, 기존 시험 기대값, Golden, `tools/b1-ops` |

## 2. 데이터 흐름

**이전(`c77ed1a`·`7043bb9`):**

```text
Archive.Snapshot (핸들 고정 1회 읽기, ArtifactRef 길이·SHA 대조)
 → RunFile: 색인 1개 + Unit Manifest N개 (역할·상대경로·unit_id)
 → Archive.WriteNew → %TEMP%\starplayer-verifier\<guid>\…   ← 변경 가능 구간
 → Replay.Verify(경로): File.ReadAllBytes로 다시 열기
 → 비교 → Receipts.Issue → registry.Register(등록)
```

**이후(`8ca9705`):**

```text
Archive.Snapshot (그대로)
 → RunFile: 색인 1개 + Unit Manifest N개 (그대로)
 → replayFiles = { 결과 폴더 내 경로 → Snapshot의 같은 byte[] }   (Verifier.cs 246~247행)
 → Replay.Verify(Func<string, byte[]?>): 파일마다 한 번 요청, SHA·검사·파싱을 그 버퍼 하나로
 → 비교 → Receipts.Issue → registry.Register
```

- 검증기 인증 경로에서 `Path.GetTempPath()`·재생용 `Archive.WriteNew`·`Directory.Delete`가 사라졌습니다. `tools/StarPlayer.EvidenceVerifier`에 `GetTempPath`는 남아 있지 않습니다(검색 0건).
- 다른 쓰기 가능한 폴더로 옮기지 않았고, 파일 SHA를 확인한 뒤 다시 여는 방식도 쓰지 않습니다. Replay는 Snapshot이 검증한 **같은 배열**을 소비합니다.

## 3. 새 메모리 Replay 인터페이스

```csharp
// 새 진입점: 결과 폴더 안의 경로 → 바이트(null = 없음)
FamilyOfficialReplay.Verify(OfficialFamilyPlan plan, Func<string, byte[]?> resultFiles, OfficialApprovals a, string repositoryRoot,
    CancellationToken cancellation = default, Func<RunConfig, GameSpec, GameRunResult>? play = null, ReplayAuthority? authority = null)
DominanceOfficialReplay.Verify(Func<string, byte[]?> resultFiles, OfficialApprovals a, string repositoryRoot,
    CancellationToken cancellation = default, Func<…>? rerun = null, ReplayAuthority? authority = null)

// 기존 경로 진입점: SimHarness CLI·기존 시험용. 같은 새 진입점을 호출하는 얇은 래퍼(파일마다 한 번 읽기)
FamilyOfficialReplay.Verify(plan, string resultDirectory, …)   DominanceOfficialReplay.Verify(string resultDirectory, …)
```

- Replay의 판정 규칙·비교·오류 코드·Hash 계산은 바뀌지 않았습니다. 바뀐 것은 색인과 Manifest 바이트를 **어디서 받는가**뿐입니다.
- 색인이 없으면 `result_missing`(기존과 같음), Manifest가 없으면 `FileNotFoundException` → 기존과 같은 `result_invalid`(경기 실행 전).
- 경로 진입점은 남아 있지만 검증기는 호출하지 않습니다(호출처: `Verifier.cs` 258·267행만 새 진입점).

## 4. 색인·Manifest 원본 결속 (OWNER 4항 대응)

| 요구 | 구현 위치(`8ca9705`) | 비고 |
|---|---|---|
| 4.1 색인 = 최초 Snapshot 바이트 | `Verifier.cs` 236행 `RunFile(snapshot, "DIAGNOSTICS", null, run/<run_id>/<ResultFile>)` → 246행 같은 배열 | 기존 |
| 4.1 ArtifactRef Raw SHA·크기 | `Archive.cs` 53행(Snapshot 읽기 때 `byte_length`·`raw_sha256` 대조, 불일치 `Archive.raw_mismatch`) | 기존 |
| 4.1 역할·상대경로 | `Verifier.cs` 370행 `RunFile`(역할·경로 정확히 1개, `unit_id` 일치) | 기존 |
| 4.1 엄격 파싱·Schema | `FamilyOfficial.cs` 466행 이후·`DominanceOfficial.cs` 445행 이후(중복 항목 거부, `schema_version`, 승인값 대조) | 기존 규칙, 입력만 바뀜 |
| 4.1 재열기 없음 | 246~247행 버퍼 조회, 258·267행 새 진입점 | **신규** |
| 4.2 Unit별 Snapshot Manifest 바이트·Raw SHA·`byte_length`·unit_id·상대경로 | `Verifier.cs` 237행 `RunFile(snapshot, "UNIT_MANIFEST", id, run/<run_id>/<unit dir>/manifest.json)`(계획의 **모든** Unit), `Archive.cs` 53행 | 기존 |
| 4.2 색인 `manifest_sha256` = Snapshot Manifest SHA(직접) | `FamilyOfficial.cs` 498~501행, `DominanceOfficial.cs` 482~484행: SHA-256(받은 버퍼) ≠ 색인 기록 → `unit_manifest_hash_mismatch`/`cell_manifest_hash_mismatch` | 이제 그 버퍼가 Snapshot 배열 자체 |
| 4.2 ArtifactRef가 맞아도 색인과 다르면 거부 | 위 행. ArtifactRef 대조(Archive.cs)와 색인 대조(Replay)가 같은 바이트에 둘 다 적용 | T-2 |
| 4.2 모든 Unit | Replay 루프가 계획의 모든 Unit을 순서대로 검사(순서·경로·SHA)하고 **경기 실행 전**에 끝냄. 첫 불일치에서 거부 | T-5(중간·마지막) |
| 4.3 같은 버퍼로 SHA·파싱 | Replay 안에서 `bytes` 하나로 SHA·`DuplicateMember`·`JsonDocument.Parse` | 기존 원칙 유지 |
| 4.4 등록 전 결속·Replay 완료 | `OfficialEntry.cs` 89행(`IssueFamily(VerifyFamily(…))`), 93~97행(세 Family를 모두 검증한 뒤 발행). `VerifyFamily`가 던지면 `IssueFamily`·`registry.Register`(`Receipts.cs` 166행)에 도달하지 않음 | T-7 |

- 추가 검증 코드를 검증기에 따로 넣지 않았습니다. 4.x의 대조는 이미 있었고, 끊긴 곳은 "Snapshot 바이트 → TEMP → 경로 재읽기"뿐이었기 때문입니다. 이제 대조가 Snapshot 배열 자체에 적용됩니다.

## 5. 보안 반례 RED/GREEN

- RED = 수정 전 제품 코드(`7043bb9`)에 시험만 더한 상태. GREEN = `8ca9705`. 로그는 실행마다 다른 파일(`E:\Madegame\g3x\phase3ops\c3\logs\`).
- 공격 장치: 각 사례는 TEST_ONLY Dominance Archive의 **사본**을 씁니다. "Snapshot 뒤·Replay 전" 개입은 기존 시험 Seam `Archive.BetweenCheckAndUse`(SimHarness 내부, 리플렉션)를 **시험 스레드에만** 겁니다. 수정 전에는 이 Seam이 TEMP 사본 기록 직전마다 호출되었고, 수정 후에는 Snapshot의 Archive 읽기에서만 호출됩니다.

| ID | 시나리오 | RED(수정 전) | GREEN(수정 후) |
|---|---|---|---|
| T-1 | 정상 Snapshot(Archive 사본) | — | 검증 성공, Payload Hash = 공유 World 증거와 같음(T-6a 안에서 확인). 바이트·경로 Replay 동등성: Family(Calibration, 가짜 경기) 평가·Run Hash 동일, Dominance(실제 엔진 48셀) 평가·48 Run Hash = 저장 결과 |
| T-2 | 셀 0 Manifest를 바꾸고 ArtifactRef도 그 바이트로 재계산(색인은 H_good) | 거부 `hash_mismatch`(`cell_manifest_hash_mismatch`) — 통과 | 같음 — 통과 |
| T-3 | 셀 0 Manifest만 변경(ArtifactRef 그대로) | 거부 `archive_invalid` — 통과 | 같음 — 통과 |
| T-5 | 셀 23(중간)·47(마지막)만 T-2 방식 변조 | 거부, 메시지에 해당 셀 ID — 통과 | 같음 — 통과 |
| **T-4** | T-2 Archive + 공격: 색인 사본 기록 직전에 TEMP 사본의 셀 0 Manifest를 색인과 맞는 정상 바이트로 교체(일관된 쌍을 Replay에 공급) | **실패 — 거부 없이 검증 성공**(공격 성공) | **통과** — 거부 `cell_manifest_hash_mismatch`, 교체할 TEMP 사본 자체가 없음(`swapped=false`) |
| **T-6a** | 정상 Archive + 공격: TEMP 사본의 셀 5 Manifest를 `{}`로 변경. 검증 중 Seam이 본 경로 기록 | **실패** — `cell_manifest_hash_mismatch`로 거부(Replay가 TEMP 사본을 읽었음) | **통과** — 검증 성공, Payload Hash 동일, 재생용 경로(`dominance-result.json`·`cells/…`) 기록 0건 |
| T-6b | 정상 Archive + Snapshot이 셀 7 Manifest를 읽은 직후 Archive 원본 파일을 `{}`로 변경 | 통과(첫 실행은 시험 결함으로 실패 — 아래) | 통과 — 검증 성공, 변경 실제 발생 확인 |
| **T-7** | 세션·로컬 Registry로 인증: T-2 Archive(그대로) → T-4 공격 | **실패** — 공격 시 결과 `(null, 1)`: 거부 없이 **영수증 1건 등록** | **통과** — 두 경우 모두 `hash_mismatch`, Registry `VERIFICATION_RECEIPT` **0건** |

- RED에서 실패한 T-4·T-6a·T-7이 G5-TEMP-01의 실제 취약 조건을 재현합니다. 특히 T-7은 수정 전 코드가 **Archive의 Manifest와 다른 바이트로 Replay하고 영수증을 등록**함을 보였습니다.
- T-2·T-3·T-5·T-6b는 수정 전에도 통과했습니다. 수정 전에도 TEMP 사본은 Snapshot 바이트로 만들어졌기 때문입니다. 이들은 새 경로의 결속을 지키는 회귀 시험입니다(RED가 아님).
- **T-6b 첫 실행 실패(시험 결함):** 첫 RED 실행에서 T-6b의 개입 시점을 "`trust/` 파일 읽기"로 잡았는데, Snapshot은 문서 참조 순서상 `trust/`를 셀 Manifest보다 **먼저** 읽어서 Snapshot 자체가 `Archive.raw_mismatch`로 거부했습니다. 개입 시점을 "셀 7 Manifest를 읽은 직후"로 고친 뒤 두 번째 RED 실행에서 통과했습니다. 두 로그 모두 보존했습니다(`red_g5t01.*`, `red2_g5t01.*`).

| 실행 | 결과 | 로그 |
|---|---|---|
| RED 1(`7043bb9` + 시험 초안) | 8건 중 실패 4(T-4·T-6a·T-7·T-6b[시험 결함]) | `red_g5t01.console.log`·`.trx` |
| RED 2(T-6b 개입 시점 수정) | 8건 중 실패 3(T-4·T-6a·T-7) | `red2_g5t01.console.log`·`.trx` |
| GREEN 검증기(`8ca9705` 작업 트리) | 8/8 통과 | `green_g5t01_verifier.console.log`·`.trx` |
| GREEN SimHarness 동등성 | 2/2 통과 | `green_g5t01_harness.console.log`·`.trx` |

## 6. 영수증 미등록

- T-7(GREEN): 결속 실패(일반 불일치, TEMP 교체 공격) 모두에서 `Certification.VerifyFamily`가 거부하고, 같은 세션의 Registry `registry_entry`(`entry_kind = 'VERIFICATION_RECEIPT'`)는 **0건**입니다.
- 같은 시험의 RED 결과는 공격 시 **1건 등록**이었습니다.
- 정상 Registry 원본을 수정·삭제하는 코드는 없습니다(기존 INSERT 전용 Trigger 유지).

## 7. 전체 회귀

| 실행 | 위치 | 결과 |
|---|---|---|
| `verify.ps1`(개발) | Worktree `Star_player_c3`, `8ca9705` 직전 작업 트리(같은 제품·시험 바이트) | **GATE: PASS**. U1 327, U2 741, U3 128(U3a 14, U3b 48), U4 1,237, U5 540, U6 98 = **3,071 PASS / 0 FAIL / 0 SKIP**. B1(경고 0)·G1~G4·E1~E4·D1(270)·D3(5) PASS |
| `verify.ps1`(Clean Clone) | `E:\Madegame\g3x\c3clean`(`8ca9705`, `core.autocrlf=true`, 작업 트리 0건) | **GATE: PASS**, **3,071 PASS / 0 FAIL / 0 SKIP**(작업 트리와 같은 세부), B1·G1~G4·E1~E4·D1(270)·D3(5) PASS. 실행 뒤 변경은 이 로그 1건뿐 — [B1_G5T01_C3_VERIFY_LOG_clean.md](B1_G5T01_C3_VERIFY_LOG_clean.md) |
| Golden | 같은 Clone | `GoldenTests` **4/4 PASS**(`dotnet test tests/StarPlayer.SimHarness.Tests -c Release --no-build --filter "FullyQualifiedName~GoldenTests"`) |
| Dry Pilot 9 | 같은 Clone(보관 Archive 130개 파일 복사, 이전 절차와 같은 스크립트) | **`ALL_9_IDENTICAL=True`**: 9개 Run의 config_hash·run_result_hash·Manifest(환경 제외)가 보관값과 모두 같음. 보관 Archive 130개 파일은 원본과 SHA 동일. 스크립트 DLL SHA `a3389704…`는 코드·경로가 달라져 이전(`44937744…`)과 다르며 Pin이 아닙니다 |

- 시험 수 3,061 → 3,071: 늘어난 10건은 모두 이번 시험(검증기 8, SimHarness 2)입니다. 기존 시험의 기대값은 바꾸지 않았습니다.

## 8. Hash·Golden·RNG·Replay 불변 증거

- 게임 엔진(`src`)·클라이언트(`game`)·데이터(`content`)는 diff에 없습니다. SimHarness의 변경은 Replay 입력 경로뿐이며 실행·Hash 계산 코드는 바뀌지 않았습니다.
- U2(Simulation 741)·U4·U5(SimHarness 540, 기존 Replay 시험 포함)·U6 PASS, D3 보호 파일 5개 SHA 일치.
- Golden·Dry Pilot 결과는 7절 표(Clean Clone).
- 바이트·경로 Replay 동등성 시험: 같은 결과 폴더에서 두 진입점의 평가 Canonical·Run Hash가 같고, Dominance는 저장된 48셀 Run Hash와 같습니다.

## 9. G1 감사 도구 재검증 필요 범위

- 승인 도구(`d3945b5e…6ba0`, 고정값 `c77ed1a`)를 변경 작업 트리에 읽기 전용으로 실행: **RESULT=FAIL, 불일치 7건**(로그 `g1_tool_d3945b5e_on_c3_worktree.log`). 도구가 설계대로 변경을 잡았습니다.
  - names(9종 개수): **변화 없음**
  - definitions(B-4 6.1 정의 13줄): **변화 없음**
  - lines: 2줄 변경(두 Replay의 색인 읽기 줄 `resultFiles(…ResultFile) is not { } raw`)
  - files: 3개 파일 SHA 변경(`Verifier.cs`, `FamilyOfficial.cs`, `DominanceOfficial.cs`)
- **필요한 조치(이번에 하지 않음):** 새 기준 Commit에 맞춘 **별도 도구 버전**. 검사 규칙·정규식·한계는 그대로 두고, 고정값(지배 줄 2개 SHA, 파일 3개 SHA, 기준 Commit 표기)만 새 Commit에서 다시 산출합니다. 기존 판정 기준을 완화하지 않습니다. 새 도구의 전체 SHA를 새로 산출하고 Codex 검수를 받습니다. 기준 Commit이 확정되기 전에는 만들지 않습니다(고정값이 Commit에 묶임).
- B-4 6.1 이름 표와 정의는 그대로입니다(이름·경로 변경 없음).

## 10. Codex 표적 검수 요청서

[B1_G5T01_C3_CODEX_REVIEW_REQUEST.md](../tasks/B1_G5T01_C3_CODEX_REVIEW_REQUEST.md)

## 11. 공식 소스 후보와 G2/G3 재검증 계획

- **새 공식 소스 후보(제안):** `8ca9705539bc941919f5290de149c4678f5147dd` 또는 그 위 문서 Commit. OWNER가 확정합니다(A1-6). `c77ed1a`의 DLL·PDB SHA와 SourceLink는 새 후보에 쓰지 않습니다.
- **게시:** 후보 Commit이 공식 원격에 없으므로, 공식 SourceLink·원격 Clone 검증에는 게시(Push) 승인이 먼저 필요합니다. 이번 작업에서는 Push하지 않았습니다.
- **게시 뒤 절차(G2 E-1·G3, 운영 기계):**
  1. 공식 원격 독립 Clone 2개(`git clone -c core.autocrlf=true --no-hardlinks https://github.com/junk1901/Star_player.git`, 다른 경로), `checkout --detach <후보>`, Commit·Tree·URL·Clean 확인
  2. 승인 입력 바이트 목록 산출·두 Clone 비교
  3. 각 Clone에서 `verify.ps1` 전체, Golden 4/4, Dry Pilot 9/9
  4. CI Release Build(`-p:ContinuousIntegrationBuild=true` 등 E-1 옵션), `b4-candidates.ps1`로 Artifact SHA 재측정(SIMHARNESS_BINARY·VERIFIER_BINARY 등 변경 예상), 두 Clone 비교
  5. SourceLink: PDB의 Commit = 후보, 원격 Raw 대조(A.6 방법)
  6. Godot PCK 5회 재현성(`game`은 바뀌지 않았지만 새 Commit에서 다시 측정)
  7. 새 G1 도구 버전 PASS(9절, Codex 검수 후)
- 그 전까지는 로컬 진단만 하며 공식 SourceLink 검증 PASS를 선언하지 않습니다.

## 12. 남은 G5 운영 의존성

- 유지: 역할 계정 분리, 보호 R-BUILD·R-WORK·R-ARCHIVE·R-TRUST·R-DOTNET·R-GIT, 외부 실행 체인 Pin, Trust Anchor, 운영 계정 SID 검증, M-1~M-11. 이번 변경은 G5 전체를 대체하지 않습니다.
- **M-12 재평가(새 구현 기준):**
  - 검증기 Replay는 더 이상 TEMP에 판정 입력을 쓰지 않습니다. 따라서 M-12의 Replay 사본 항목(A·D·H·I의 TEMP 사본 부분)은 **이 경로에 해당 없음**이 됩니다. 대신 운영 기계에서 "검증기가 재생용 파일을 만들지 않음"을 ETW(Kernel-File)로 1회 확인하는 항목으로 바꿀 것을 제안합니다.
  - 로컬 S4U 관련 항목(M-12E·F의 S4U 부분, S0~S9)은 보류입니다(OWNER 결정).
  - **별도 검증 대상으로 남는 TEMP·작업 경로:** Test Profile이 실행하는 `dotnet test`(VSTest·testhost)가 프로세스 TEMP에 쓰는 자체 임시 파일. TRX는 R-WORK에서 핸들 고정 1회 읽기로 판정에 쓰입니다(G4). VSTest 임시 파일이 판정 입력이 되는지는 이번에 확인하지 않았습니다(**미검증**).
- G5-TEMP-01: Codex 검수 전 **OPEN_MAJOR**. 코드 수준 해결로 판정되어도 운영 G5 PASS가 아닙니다.

## 13. 증거 해시

저장소 밖 `E:\Madegame\g3x\phase3ops\c3\`(전체 목록 `SHA256SUMS_c3.txt`), SHA-256:

| 파일 | SHA-256 |
|---|---|
| `logs\red_g5t01.console.log` · `.trx` | `a386ab49871a55ed5c6a3c4933a0bd5313d4e5a30d580c5c21db259cf6501b2d` · `06a63ee2faa93a52a44633bfc550c63f0212f4aee04acf8a09558de02d973672` |
| `logs\red2_g5t01.console.log` · `.trx` | `a0a666529a7081701c989a0b1452de3b6a272af0444cf18dd7faa29d43f27d78` · `936fd104062a19428aaaee248d9b15d8f074b9dc4633973f2f9607b5cf8a3d2e` |
| `logs\green_g5t01_verifier.console.log` · `.trx` | `3189158c3644ec8073a51afecef2a0782ae207612df3bb698c2eaa5352325bfa` · `bbccd776b4bb5cf5c09d53acd22b7de64de1308fd8e06a5e8a40cac94bf50a3a` |
| `logs\green_g5t01_harness.console.log` · `.trx` | `5488549ace9bb105f222d1c05bc932bf65fdfcda993617a544438c925268639f` · `413dc8828c29d9ef0db26288888168f684b2fbbc3b4f0e269b35583fd2257fcb` |
| `logs\B1_G5T01_C3_VERIFY_LOG_worktree.md` · `verify_worktree.console.log` | `37ecea64aeb9a58d576fecc1ddf2b3c0c72a04beaba8009578e0fc9fd17004f0` · `74afb4326c80d2533b527b457915680ac9f04f6e354b9878e05f199379b1adff` |
| `logs\verify_clean.console.log` · 저장소 `docs/reports/B1_G5T01_C3_VERIFY_LOG_clean.md` | `e1cddc614f365d4ef602622b6159e49fdc69146156ac9ff6a08ba8ad5c38b629` · `2f3e579196d42670431748ae06858b4156adb3c9bc8d9e655ec16e2ee411ece9` |
| `logs\golden_clean.txt` · `drypilot_clean.txt` · `drypilot.ps1` | `4582a330993ca7eab640028a18ef0af4a4b8d276b3ecea4a6e7e2d9359b27f76` · `2b253789aeb93a73b87999aba0bbb7db1de38a2bf44a190e23e4320d3161fbd0` · `ff8c0d26d1b5ec764e617f7cd0faee74f75df483d310362c67194b28fccd60ed` |
| `logs\g1_tool_d3945b5e_on_c3_worktree.log` | `becb1681b96ebd15a95815aa4b5332ea20f032372535082a4d238fc6e203a297` |
| `logs\clean_clone.txt`(Clone HEAD·Tree·설정) | `6f532a59d588bfdd9df6527a0dcfe6297bc5f5b5e8c5df931a78be5a0858694f` |

- Clean Clone은 원격이 아니라 로컬 Worktree에서 만든 것입니다(`git clone -c core.autocrlf=true --no-hardlinks E:\Madegame\Star_player_c3`). 따라서 SourceLink는 공식 원격을 가리키지 않으며, 이 결과는 공식 G2/G3 증거가 아닙니다(A7).
