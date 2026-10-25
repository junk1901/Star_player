# B-1 (B1-T00~T10) 통합 보고서 — Codex T11 독립 검수 제출용

> **2026-10-25 갱신:** Codex T11 판정은 FIX_REQUIRED였고 수정 결과는 [B1_T11_FIX_REPORT.md](B1_T11_FIX_REPORT.md)에 있습니다. 아래 본문의 상태·시험 수치는 수정 전(2026-10-24) 기록이며, 최신 상태는 그 보고서가 우선합니다.

- 작성: Claude Code, 2026-10-24 (3차: OWNER 결정 2 반영) · 상태: **구현·자체 시험 완료(T10까지), T11 미실시** · 리뷰 상태 `검토 대기`
- Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 완료 아님 · Git Commit·Push 하지 않음
- 근거 자료: [내부 작업 기록](B1_IMPLEMENTATION_LOG.md), [조항·반례 Traceability](../contracts/b1/B1_TRACEABILITY.md), [필드 단위 Traceability](../contracts/b1/B1_FIELD_TRACEABILITY.md), [OWNER 결정 기록 2](../contracts/b1/B1-T00_OWNER_DECISION_2.md), [OWNER 결정안(제안 원문)](../contracts/b1/B1_OWNER_DECISION_PROPOSALS.md), [승인 기록](../contracts/b1/B1-T00_OWNER_APPROVAL_RECORD.md), [NSC-4·NSC-5 결정](../contracts/b1/B1-T00_NSC4_NSC5_OWNER_DECISION.md), Verify 로그 [B1_VERIFY_LOG.md](B1_VERIFY_LOG.md)

## 1. TASK별 구현과 수용 기준 (S00.5 §6 기준)

시험 GATE(3절, verify.ps1)와 TASK 수용 판정은 따로 봅니다. GATE PASS는 "실행한 시험이 모두 통과"라는 뜻이고, TASK 수용은 아래 판정입니다.
판정: **충족**(코드·자체 시험 기준) · **부분**(기준 일부만, 이유 명시) · **BLOCKED**(새 OWNER 결정 없이는 진행 불가, 승인 규범은 바꾸지 않음) · **미충족**. 오른쪽 끝 열은 이 TASK에 걸린 **미검증 운영 Gate**(별도 계정·관리자 작업·운영 값이 필요해 자체 시험과 분리)입니다.

| TASK | 구현 | 수용 기준 판정 | 미검증 운영 Gate |
|---|---|---|---|
| T00 | 승인 기록, 기준본 고정(보존본·`-text`·SHA 목록·verify D3), NSC-4·5 결정 기록, **OWNER 결정 기록 2**, 조항 Traceability, 필드 단위 Traceability 616행 | **충족** — ADR-010·SYSTEM_CONTRACTS는 갱신하지 않음(7절) | — |
| T01 | 엄격 Reader, 닫힌 4Schema + 교차 규칙, 미승인 5-tuple·DRAFT 키 거부, JSON Pointer·원인 코드 | **충족** | — |
| T02 | JCS, U64·D64, ES binary64(BigInteger), GV-C01·C02·EV0 | **충족** | — |
| T03 | Family·Dominance 투영(Complete만, 기존 평가 재사용), `EvidenceWriter` | **충족**(세 Family 모두 실제 엔진 TEST_ONLY Run) | — |
| T04a | `GameExecutor` 관찰 Seam | **충족** — Observer 유무 결과 동일, Dry Pilot 9 Run 매니페스트 동일 | — |
| T04b | EventObservation, events_digest, Unit PZL, GAME_EVENTS, 재관찰 비교, NSC-6 coverage | **충족** | — |
| T05 | #6 고정 20경기 6단계, **결정 2: Infrastructure 공개 진입점 `EventJsonV2Codec`(기존 Writer/Reader 위임), 검증기 기본 결합, 경계 시험 최소 예외** | **충족** — 검증기 단독 #6 정상·변조 시험. 단 7절 해석 6(F-26 비교 방식)은 OWNER 결정 대기 | — |
| T06a | 승인 기록·허가·프로필·앵커·서비스 설정 형식, §13, PATCH-04, **결정 1: NSC-4 정정 판정**, 관리자 실행 거부, NSC-5, Registry 경유 L2~L8·T6·Registry ID | **충족**(코드·자체 시험) | 보호 앵커 디렉터리 생성·ACL 적용과 T2 운영 실측(관리자 작업, 별도 허가 필요) — **미검증** |
| T06b | AMPV, ReplayAuthority, BuildAttestation, 통합 검증, R01-C, Calibration 끝-끝, Test Profile 실제 실행(TRX), Phase 13 Check | **충족**(TEST_ONLY). Phase 영수증은 이 세계에서 세 Family 모두 §19 인증 불가라 발행 대상 없음(A28 거부 확인) | 승인 환경 프로필·승인 Test Profile 아래 실행 — **미검증**(운영 값 미생성) |
| T07 | M(D), 4 Payload, 결속, Prefix SHA, Phase checks | **충족** | — |
| T08 | 한 번 읽기 스냅샷, Junction 거부, 덮어쓰기 금지, DAG, RAW_INVENTORY | **충족** — RAW_INVENTORY 형식은 7절 해석 2(OWNER 결정 대기) | — |
| T09 | 영수증, §19, 발행, SQLite 기록부, 요청 형식, 프레이밍, Named Pipe 서비스·클라이언트(Win32), **결정 3: `get_registry_identity` 명시 조회(7단계)**, 끊긴 세션 폐기 | **충족**(코드·한 계정 실제 Pipe 시험, Seam 연결은 `Trusted=false`) | AT-1~AT-14r, AT-4(P-1) 정식 측정(별도 서비스·검증·OWNER·Reader 계정) — **미실시** |
| T10 | 독립 Python Golden(Calibration·Integration·Dominance·Phase), 반례 시험(기존 28 + 신규, NSC-6 coverage, 이번 N07·N32 직접 시험), 전체 회귀, verify.ps1 | **부분** — N27-d·e·f는 별도 계정이 필요해 직접 시험 없음. N12 비트 Golden·witness_digest 승인값은 운영 값이라 미생성(해석 3 결정 대기) | N12 승인값 — **미생성** |
| T11 | — | **미실시**(Codex) | — |

## 2. 변경 파일·클래스·주요 동작

**수정(기존):**

| 파일 | 변경 |
|---|---|
| `tools/StarPlayer.SimHarness/GameExecutor.cs` | `Play(..., PzlEventObserver? events = null)`, `Advance(..., out InPlayAudit? inPlay)`, null 조건 호출 2곳 |
| `tools/StarPlayer.SimHarness/Official.cs` | `ReplayAuthority`, `OfficialReplay.VerifyEnvelope` 추출 + `Verify(..., authority)` 게이트, `OfficialPreflight.BuildAttestation` 추출 |
| `tools/StarPlayer.SimHarness/Replay.cs` | `ReplayCheck.Validate(..., authority)` 전달만 |
| `tools/StarPlayer.SimHarness/FamilyOfficial.cs`, `DominanceOfficial.cs` | 재생 함수에 `authority` 전달, `ReplayOutcome.Reruns`(init 속성) |
| `tools/verify.ps1` | B1 문구(13 projects), U6(EvidenceVerifier 시험), D3(기준본 SHA) — BOM 유지 |
| `StarPlayer.sln` | 신규 프로젝트 2개 |
| `.gitattributes` | 계약 파일·보존본·`tests/Fixtures/b1-golden/**` `-text` |
| `tests/StarPlayer.Infrastructure.Tests/*.csproj` | EvidenceVerifier.Tests 참조(#6 codec 결합·Phase 루트 시험) |
| `tests/StarPlayer.Application.Tests/TrustedBoundaryTests.cs` | 결정 2: Infrastructure 공개 형식 목록에 `EventJsonV2Codec` 1개(근거 주석), Domain 금지 단정은 그 형식만 제외, 그 형식의 서명·노출 형식 고정 시험 1개 추가 |

**신규:**
- `tools/StarPlayer.SimHarness/Evidence/` 14개(`StrictJson`, `Jcs`, `EvidenceSchema`, `EvidenceDocuments`, `EvidenceRules`, `PzlEventObserver`, `OfficialDocuments`, `UnitPzlEvidence`, `JDiff`, `EvidenceHash`, `Archive`, `ApprovalRecord`, `TrustAnchor`, `EvidenceWriter`)
- `src/StarPlayer.Infrastructure/Fixtures/EventJsonV2Codec.cs`(결정 2, 기존 v2 Writer/Reader 위임 2개)
- `tools/StarPlayer.EvidenceVerifier/`: `Pzl6Verifier`, `ExecutionEnvironment`, `Registry`, `Receipts`, `Verifier`, `TrustBootstrap`, `TestProfiles`, `PhaseVerifier`, `PipeRegistry`(`Native`·`RegistryPipeService`·`RegistryPipeClient`), `Program`; csproj에 `AllowUnsafeBlocks`(LibraryImport 마샬링 생성용)
- `tools/b1-golden/golden.py`(독립 Golden), `tools/b1-golden/fields.py`(필드 Traceability 생성)
- 시험: `tests/StarPlayer.SimHarness.Tests/Evidence/` 12개(+`CounterexampleTests`; `ApprovalAndTrustTests`는 결정 1 판정 시험으로 갱신), `tests/StarPlayer.EvidenceVerifier.Tests/` 12개(`TestOnlyWorld`, `TestOnlyTrust`, `VerifierTests`(+`RegistryTests`), `CertificationAndEnvironmentTests`, `BootstrapAndProfileTests`, `PipeRegistryTests`, `CounterexampleTests`, `Pzl6StandaloneTests`, `WitnessOrderTests`(+`WitnessWorld` 셀당 4경기 Fixture) 등), `tests/StarPlayer.Infrastructure.Tests/OfficialPzl6VerificationTests.cs`
- `tests/Fixtures/b1-golden/`(cal 문서+Unit 4, int 문서+#6+Unit 2, dom 문서+Unit 48, `expected.json`, `phase-input.json`)
- 문서: `docs/contracts/b1/*`(승인 기록·NSC 결정·보존본·SHA 목록·조항/필드 Traceability·OWNER 결정안), 본 보고서, 작업 기록, Obsidian 리뷰·결정·이슈

## 3. 실제 수행 시험

환경: Windows 11 Home 10.0.26200, .NET SDK 8.0.425, Godot 4.7.2 mono, Release, 계정 `S-1-5-21-2654241679-1008489621-169677055-1001`(UAC 필터 토큰: `BUILTIN\Administrators`는 "deny only", Medium 무결성, 비승격).

**최종 `tools/verify.ps1 -LogName B1_VERIFY_LOG.md`** — Run at 2026-10-24 22:26:24 +09:00, `GATE: PASS`, 로그 [B1_VERIFY_LOG.md](B1_VERIFY_LOG.md) SHA-256 `f5702359f8ebc7195dfe2911154554d6feb826ca3c0771a21db5ca41759413ed` (OWNER 결정 2 반영 후 첫 전체 실행, 한 번에 통과)

| 항목 | 결과 |
|---|---|
| B1 Release build (13 projects) | PASS, warnings 0 |
| U1 Domain | PASS 327 / FAIL 0 / SKIP 0 |
| U2 Simulation | PASS 741 / 0 / 0 |
| U3 Application (U3a 14, U3b 48 포함) | PASS 128 / 0 / 0 (+1: `EventJsonV2Codec` 표면 고정 시험) |
| U4 Infrastructure | PASS 1,237 / 0 / 0 (Phase 루트 시험이 하위 `dotnet test`로 4개 시험 어셈블리 실제 실행, 공개·내부 codec 동일성) |
| U5 SimHarness | PASS 527 / 0 / 0 (+24: NSC-4 정정 판정 시험, N32) |
| U6 EvidenceVerifier | PASS 41 / 0 / 0 (+4: #6 단독 2, `get_registry_identity` 1, N07 1) |
| 합계 | **3,001 PASS / 0 FAIL / 0 SKIP** |
| G1~G4 Godot headless, E1~E4 Export·Cold run | 모두 PASS |
| D1 문서 링크(178개 파일), D3 B1-T00 기준본 SHA(5개 파일) | PASS |

**실행 이력(정직 기록):**
- 2차 보완 때: 1차 전체 실행 `GATE: INCOMPLETE`(Pipe 서비스 종료 경쟁으로 testhost 비정상 종료 — 구현 결함, 수정), U4 로그 재기록 파일 잠금(손자 프로세스 추정, `verify.ps1` 재시도 추가) → 2차 `GATE: PASS` 2,972 PASS(18:58:49)
- 이번(결정 2 반영): 부분 시험 중 실패 4건은 모두 시험 기대 오류였고 구현은 그대로 유지 — NSC-4 실경로 시험이 하향 판정 순서를 무시(첫 위반은 사용자 프로필 디렉터리), Junction 정리 순서, Archive 재작성 금지(Unit을 세계당 한 번 쓰도록 Fixture 수정), C# 문자 리터럴 편집 오류. 전체 실행은 한 번에 `GATE: PASS`
- 이전 이력(BOM 누락 G3·G4 오탐 등)은 [작업 기록](B1_IMPLEMENTATION_LOG.md)

**SKIP·미실행:** 시험 SKIP 0. Windows 운영 측정(AT-1~AT-14r, AT-4 정식, 보호 앵커 디렉터리 T2)은 **미실시/미검증**(6절, 시험 GATE와 별개).

**그 밖의 실측:** Dry Pilot 9 Run 재현(4절), NSC-4 ACL 실측(6절), AT-4 사전 측정(6절), 실제 Named Pipe·AccessCheck(6절), Phase 루트의 하위 `dotnet test` 실제 실행(4개 어셈블리, §12.2 Case 전부 Passed), 실제 Junction 생성 거부(ArchiveTests).

## 4. Hash·RNG·Replay 불변성 증거

- **Dry Pilot 9 Run 재현** (B1-T04a 후 Release 바이너리 SHA-256 `a1741bee…630f9a`, `dry` 명령, 보관 Seed 그대로): 9개 모두 `config_hash`·`run_result_hash`·매니페스트 전체(`environment_not_hashed` 제외)가 `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_dry_run1/runs/*-a`와 동일.
- **Observer 유무 동일**(`ObserverSeamTests`), **기본 Replay 모드 불변**(N13-a, 기존 SimHarness 시험 전부 통과), **금지어 검사**(`Evidence/`에 RNG·확률·Reducer 직접 적용·시계 읽기 없음).
- 이번 보완(T06a·T06b·T09·T10)은 `tools/StarPlayer.SimHarness`의 기존 실행 코드를 바꾸지 않았습니다(검증기 프로젝트·시험·문서만 추가). 기존 ResultHash 재계산은 N01 시험이 문서 필드만으로 독립 재현합니다.
- 결정 2 반영분도 실행 경로를 바꾸지 않습니다: `EventJsonV2Codec`는 기존 `ScriptedGameJson` 위임만 하며(Write 바이트·Read 결과 동일 시험), `AnchorAcl`·Registry 변경은 검증·신뢰 경로에만 있습니다. Golden 고정 입력과 기대값은 바뀌지 않았고 `GoldenTests` 4개가 그대로 통과합니다.

## 5. Golden 및 변조 반례 결과

- **독립 Golden**(`tools/b1-golden/golden.py`, Python, C# 코드 없이 작성, `python -I`): 네 Payload의 바이트 수·SHA-256·A+를 C# 생산 코드가 그대로 재현(`GoldenTests` 4개).
  - Calibration `sha256:fd2494988fd64461d7875454ba5561e2ea58130c0a074ebded957493715af519` (실제 엔진 12경기/층, SEEDS_EXHAUSTED, Gate INSUFFICIENT_SAMPLE)
  - Integration `sha256:702d1225c0998234d83fa438e8dc733b98eb327c7ef5b1debef62c7780638ec4`
  - Dominance `sha256:c8c9aa483bb04603d26a9923d2243d51951ff115fc1ed012267c1b71aa5c446a`
  - Phase `sha256:f9f61fe2d1bf29325a70750bee37c7b7f96ca6b33f2de7f5a31831f88e4e97bc` (위 세 실제 자식 + 13 Check)
  - GV-C02 DOMINANCE `e581ac6c…` · PHASE `7d69df8a…`: 두 번째 구현 일치
  - 동결 입력은 TEST_ONLY 세계에서 `B1_GOLDEN_EXPORT`로 한 번 내보낸 뒤 고정(HEAD `257403dd`). 정식 승인 Golden 아님.
- **반례**: [Traceability 2절](../contracts/b1/B1_TRACEABILITY.md) — 기존 28개 + 신규 반례 + NSC-6 coverage 반례. 남은 "부분"은 1절 T10 행.

## 6. NSC-4·NSC-5와 Windows 실측 (코드·시험 상태와 분리)

| 측정 | 실제 수행 | 결과 | 판정 |
|---|---|---|---|
| NSC-4 B안(정정 전), 표준 경로 | 수행(읽기만, 1차) | System32 `(A;OICIIO;GA;;;CO)`, `C:\` `(A;;LC;;;AU)` 등으로 전부 거부 | 완화하지 않고 보고 → OWNER 결정 1 |
| NSC-4 정정 판정, 이 PC 실제 경로 | 수행(읽기만) | `C:\Windows\System32\drivers\etc\hosts`·`kernel32.dll` 사슬 통과, 사용자 프로필 아래 파일은 이 사용자 SID로 거부, 읽을 수 없는 경로 거부, 실제 Junction 거부 | 시험 PASS |
| NSC-4 후보 위치 `C:\ProgramData\StarPlayerTrust` | 수행(실제 `C:\`·`C:\ProgramData` 서술자 + 목표 SDDL의 후보·앵커) | 통과, 공격 변형 8종 거부 | 시험 PASS — **후보 디렉터리는 만들지 않음** |
| 보호 앵커 디렉터리 생성·ACL 적용, 별도 계정의 쓰기·삭제·이름 변경·ACL 변경 시도 | **미실시** | 관리자 작업·계정 필요(별도 허가 전 수행 금지) | **미검증 — 운영 T2 PASS 아님** |
| NSC-5 경로 규칙 | 수행 | 구문 17종·실경로 검사, 서비스 시작 시 구성 1회 읽기·저장소 안 경로 거부 | 시험 PASS |
| 실제 Named Pipe(한 계정) | 수행 | 선점 거부, TokenUser 불일치 거부, 역할별 허용·거부, **명시 신원 조회 불일치 → Connect 거부**, 미인가 주체 → 신원 조회에서 거부, 끊긴 세션 재사용 불가, 서버 없음 → 30초 후 `registry_unavailable` | 시험 PASS(역할 SID는 Seam, `Trusted=false`) |
| 실제 AccessCheck(R03-4 5단계) | 수행 | 이 계정은 `0x1F01FF` 보유 → 실제 Win32 결과로 `registry_identity_mismatch` | 시험 PASS |
| AT-4 사전 측정 | 수행 | 다른 SID 소유 Pipe 생성 → Win32 1307(ERROR_INVALID_OWNER) | **AT-4 PASS 아님**(UAC 필터 관리자 계정의 사전 측정) |
| AT-1~AT-14r | **미실시** | 별도 서비스·검증·OWNER·Reader 계정 필요 | 미실시 |

## 7. 기존 계약 대비 변경·해석

- 계약 본문 변경 없음(SHA `1338a2f7…7d64` 그대로, D3이 매 실행 확인). NSC-4·NSC-5 결정 기록도 변경 없음.
- OWNER 결정 2(2026-10-24T21:49:42+09:00, [기록](../contracts/b1/B1-T00_OWNER_DECISION_2.md)): NSC-4 최소 정정, T05 codec 공개 진입점, T09 `get_registry_identity` 명시 조회. 계약 SHA·NSC-4/5 결정 원문은 바꾸지 않았습니다.
- 구현 해석 분류 — **단순 구현 선택**(계약 의미를 바꾸지 않음) / **OWNER 결정 필요**(문서 바이트·A+·운영 값·F-26 문구에 영향) / **해소**(이번 결정으로 확정):

| # | 해석 | 분류 | 계약 원문 대조 |
|---|---|---|---|
| 1 | Unit PZL `violations` 생성 단위: 경기별·원인 카운터별 1건 | **OWNER 결정 필요** | §7은 형식·정렬·중복 금지만 규정, 생성 단위 없음 |
| 2 | RAW_INVENTORY 형식 `simharness-raw-inventory-v1` | **OWNER 결정 필요** | §21은 역할·개수·시점·자기 참조 제외만 규정, 파일 형식 없음 |
| 3 | witness_digest의 difference·standard_error·lower_bound를 BITS64로 | **OWNER 결정 필요** | §18 식에 `: BITS64`가 `non_superiority_z`·`holm_critical`에만 붙음, 앞 세 필드 표기 미명시 |
| 4 | Registry 응답 형식 `{status, entry\|entries\|registry_sequence\|code,cause}` | 단순 구현 선택 | 계약은 요청 형식만 규정, 양끝 같은 구현, 내용은 L3 SHA로 재검증 |
| 5 | 검증기 관리자(Elevated) 실행 거부 | 단순 구현 선택 | NSC-4 결정 6의 실행 수단 |
| 6 | #6 최종 상태 비교: 검증기 내부 구조 비교 + `GameState` 필드 인벤토리 | **OWNER 결정 필요** | §6 본문(구조 비교·커버리지 검사)은 충족, F-26 "public 위임 비교 메서드"와 다름. 결정 2는 codec만 다룸 |
| 7 | Dominance `CellEvaluation.config_hash`·`run_result_hash` null이면 투영 거부 | 단순 구현 선택 | 계약 타입 `H` 그대로 |
| 8 | R03-4 7단계 Registry ID 조회 | **해소(결정 3)** | `get_registry_identity` 명시 조회로 확정, 일반 응답의 ID 사후 대조 제거 |
| 9 | Test Profile을 메서드별 `--filter` 하위 `dotnet test`로 실행, Theory 행은 표시 이름 집합 동일성으로 대조 | 단순 구현 선택 | CASE_ID `{project}\|{표시 이름}` 규칙 그대로 |
| 10 | Pipe 시험 Seam을 쓴 연결은 `Trusted=false` | 단순 구현 선택 | 시험 Seam이 인증 결과를 만들지 못하게 함 |
| 11 | NSC-4 상속 판정: 상속 가능 ACE를 다음 구성요소에 적용될 것으로 재판정, CREATOR OWNER·GROUP 치환 | **해소(결정 1의 구현)** | 결정 1 "상속되어 적용될 수 있는 ACE까지 무조건 무시하지 않는다"의 구현 방식 — Codex 확인 대상 |
| 12 | T05 경계 예외: 공개 형식 1개 추가, 그 형식의 표면을 별도 고정 | **해소(결정 2의 구현)** | Friend 목록 변경 없음 |

- 결정 표(해석 1·2·3·6)의 근거·필요한 결정은 [OWNER 결정 기록 2](../contracts/b1/B1-T00_OWNER_DECISION_2.md) 끝 표에 있습니다. 임의로 승인하지 않았습니다.
- ADR-010·`docs/SYSTEM_CONTRACTS.md`는 갱신하지 않음: B-1 계약은 승인 기준본 자체가 정본입니다.

## 8. 미완료·차단·미검증

- **OWNER 결정 대기:** 7절 해석 1·2·3·6
- **미검증 운영 Gate(자체 시험과 분리):** 보호 앵커 디렉터리 생성·ACL 적용과 T2 운영 실측, AT-1~AT-14r, AT-4(P-1) 정식 측정, 운영 Registry 서비스 배치(별도 계정·관리자 작업, 별도 허가 전 수행하지 않음)
- **미생성(운영 값):** 환경 프로필 승인값, 비트 Golden 36개, witness_digest 승인값(N12)
- **반례 직접 시험 없음:** N27-d(재연결 후 다른 서버)·N27-e(SQLite 파일 ACL)·N27-f(Impersonation 실패) — 별도 계정·서비스 배치 필요. N27-d의 클라이언트 쪽 규칙(끊긴 세션 재사용 불가)은 시험함
- Phase 영수증 실제 발행: 이 TEST_ONLY 세계에서는 세 Family 모두 §19 인증 불가라 대상 없음

## 9. Codex T11 독립 검수 체크리스트

1. 승인 기준본 SHA·보존본·D3·`-text` 처리, 승인 기록이 OWNER 지시와 같은지
2. T04a: `GameExecutor` 변경이 RNG 소비·결과를 바꾸지 않는지
3. R01-A/B/D 리팩터링이 기본 모드 결과·오류 코드·검사 순서를 그대로 두는지
4. T01 Schema가 계약 필드 목록과 정확히 같은지 — [필드 Traceability](../contracts/b1/B1_FIELD_TRACEABILITY.md)로 경로 대조
5. T02 F64 ES 규칙과 JCS UTF-16 정렬
6. T07 Payload·M(D)·결속·Phase coverage(NSC-6)
7. 독립 Golden의 독립성과 동결 입력 경위
8. T06a `TrustBootstrap` L2~L8·T6 순서와 오류 분류, T06b Test Profile TRX 대조(누락·건너뜀·중복·새 행 거부)
9. **결정 1** `AnchorAcl` 정정(구성요소별 금지 권한, INHERIT_ONLY·상속 판정, 모호 SID, Fail-Closed)이 B안의 보안 목표를 유지하는지, 후보 ACL
10. **결정 2** `EventJsonV2Codec`가 위임만 하는지, `TrustedBoundaryTests` 예외가 최소인지
11. **결정 3** `get_registry_identity` 형식·권한, 클라이언트가 다른 연산 전에 조회하는지
12. T09 Pipe: SD 구성, FIRST_PIPE_INSTANCE, Impersonation·RevertToSelf FailFast, 클라이언트 4·5단계 실제 Win32 경로, 서비스 종료 경쟁 수정, 시험 Seam이 신뢰를 만들 수 없는지
13. 7절 해석 분류와 결정 표(해석 1·2·3·6)
14. 미구현·미실시·미검증 운영 Gate가 PASS로 표시된 곳이 없는지

## 10. Git 상태

- HEAD `257403ddae557a54c4013c17705370caeac7a2e0`(변경 없음), Commit·Push 하지 않음, 스테이징 0
- 수정 12개: `.gitattributes`, `CURRENT_STATE.md`, `Star/00_Dashboard.md`, `StarPlayer.sln`, `tests/StarPlayer.Application.Tests/TrustedBoundaryTests.cs`, `tests/StarPlayer.Infrastructure.Tests/StarPlayer.Infrastructure.Tests.csproj`, `tools/StarPlayer.SimHarness/{DominanceOfficial,FamilyOfficial,GameExecutor,Official,Replay}.cs`, `tools/verify.ps1`
- 비추적 19개 항목(이번 작업 신규 + 기존 보호 파일): `docs/contracts/`(결정 기록 2 포함), `src/StarPlayer.Infrastructure/Fixtures/EventJsonV2Codec.cs`, `docs/reports/B1_*.md` 3개, `Star/01_Phase_Reviews/Phase_03F_B1_Review.md`, `Star/02_Decisions/2026-10-24_B1_T00_Owner_Approval.md`, `Star/03_Issues/`, `tests/Fixtures/b1-golden/`, `tests/StarPlayer.EvidenceVerifier.Tests/`, `tests/StarPlayer.Infrastructure.Tests/OfficialPzl6VerificationTests.cs`, `tests/StarPlayer.SimHarness.Tests/Evidence/`, `tools/b1-golden/`, `tools/StarPlayer.EvidenceVerifier/`, `tools/StarPlayer.SimHarness/Evidence/`, 그리고 보호 파일 `B1-T00_FINAL_CONTRACT_DRAFT.md`·`TASK.md`·PDF 2개
- 보호 파일(변경 없음, D3 확인): `B1-T00_FINAL_CONTRACT_DRAFT.md` `1338a2f7…7d64`, `TASK.md` `ecbc0859…f825`, `docs/2023recordRule.pdf` `9742caaf…12b0`, `docs/2026-official-baseball-rules.pdf` `f85860f3…7859`
- 작업 중 생성된 비추적 실행 산출물: `build/b1-t04a-repro/`, `build/verify/`(git-ignored), 시험용 임시 Registry DB(`%TEMP%\starplayer-registry-tests`, `%TEMP%\simharness-tests`)
