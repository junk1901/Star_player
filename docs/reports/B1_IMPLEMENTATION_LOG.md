# B-1 (B1-T00~T10) 내부 구현 작업 기록

> 내부 작업 기록입니다. Codex 제출용 통합 보고서는 `docs/reports/B1_INTEGRATED_REPORT.md`에 따로 둡니다.
> 기준: 승인 계약 SHA-256 `1338a2f7…7d64`, HEAD `257403dd…a7e0`. Official Execution = NOT_READY, Final Execution Freeze = PENDING.

## T00 — 계약 기준본 (2026-10-24)

- 승인 기록: `docs/contracts/b1/B1-T00_OWNER_APPROVAL_RECORD.md`
- NSC-4·NSC-5 결정 기록: `docs/contracts/b1/B1-T00_NSC4_NSC5_OWNER_DECISION.md`
- 보존본: `docs/contracts/b1/B1-T00_FINAL_CONTRACT.approved.md` (읽기 전용), SHA 목록 `docs/contracts/b1/B1-T00_BASELINE.sha256`
- `.gitattributes`: 계약 파일·보존본 `-text` (`git check-attr text` = unset 확인)
- 진행 상태: 필드 사전·Traceability 문서 작성 예정

## 기준선 (2026-10-24, HEAD 257403dd, 변경 전)

- `dotnet build StarPlayer.sln -c Release`: 0 Warning, 0 Error
- 테스트: Domain 327 · Simulation 741 · Application 127 · Infrastructure 1,236 · SimHarness 374 = 2,805 통과 (실패·건너뜀 0)

## T01·T02 (2026-10-24)

- 신규: `tools/StarPlayer.SimHarness/Evidence/StrictJson.cs`, `Jcs.cs`, `EvidenceSchema.cs`, `EvidenceDocuments.cs`, `EvidenceRules.cs`
- F64는 BigInteger 정확 연산으로 최단 왕복 자릿수를 구함(`.NET "R"` 미사용). .NET 8 `"R"` 실측: `1e20→1E+20`, `1e21→1E+21`, `1e-5→1E-05`, `1e-6→1E-06`, `1.2345678901234568e17→1.2345678901234568E+17` (ES 규칙과 다름). .NET Framework(PowerShell 5.1) `"R"`은 `43143ff3c1cb0959`를 `1424953923781206.3`으로 출력(계약값 `…206.2`)
- 시험: `tests/StarPlayer.SimHarness.Tests/Evidence/StrictJsonAndJcsTests.cs` (GV-C01·C02 Raw·3 digest, GV-EV0, F64 10개, U64·D64 경계, A21~A24)

## T04a (2026-10-24)

- 신규: `Evidence/PzlEventObserver.cs`. 수정: `GameExecutor.cs` (선택 인자 `events`, `Advance`의 `out InPlayAudit?`, null 조건 호출 2곳. 인자 평가도 observer가 null이면 생략)
- 시험: `Evidence/ObserverSeamTests.cs` (Observer 유무 결과·카운터·PZL 관찰 동일, H0→H1→H2 연결과 CommitRecord RNG Hash 대조, A·C 경우, Evidence 소스 금지어 검사)
- Dry Pilot 9 Run 재현 (Release `StarPlayer.SimHarness.dll` SHA-256 `a1741beefa6be460553cfe2873d0d2b12071d1553f3f5d22584cf4e626630f9a`, `dry` 명령, 보관 Seed 파일 그대로): 9개 모두 config_hash·run_result_hash·매니페스트 전체(`environment_not_hashed` 제외)가 `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_dry_run1/runs/*-a/manifest.json`과 동일 (`ALL_9_IDENTICAL=True`)
- SimHarness 시험 415/415 통과 (기존 374 + 신규 41), 기존 EndToEnd 경계·금지어 시험 포함

## T03·T04b (2026-10-24, 진행 중)

- 신규: `Evidence/OfficialDocuments.cs` (Complete 결과만 투영, 기존 Evaluate·StratifiedEvaluator·DominanceEvaluation 재사용, `FamilyOfficial.Execute`·DRAFT 색인 미수정), `Evidence/UnitPzlEvidence.cs` (EventObservation·events_digest·Unit PZL 문서·GAME_EVENTS 쓰기/읽기·경기별 재관찰 비교), `Evidence/JDiff.cs`
- 시험: `Evidence/OfficialDocumentTests.cs`, `Evidence/UnitPzlEvidenceTests.cs`, `Evidence/JEdit.cs`
- SimHarness 시험 440/440 통과 (기존 374 + 신규 66). 다른 4개 시험 프로젝트는 이 시점 재실행 안 함(미검증)
- 미완료: N05-e(실패 경기의 투구 사건이 있는 Unit) 실측 사례 미작성. 현재 예산 1 실패 경기는 approach 사건 1건뿐이라 pitches가 같음

## T07·T08·T06a·T05·T06b·T09 (2026-10-24, 재개 후)

- T07 `Evidence/EvidenceHash.cs` (M(D), 4 Payload, 부모·자식 결속, Prefix SHA, Phase checks R-9·coverage 범위) — `EvidenceHashTests` 4개
- T08 `Evidence/Archive.cs` (한 번 읽기 스냅샷, Raw SHA·길이, Junction 거부, 덮어쓰기 금지, DAG 역할 제한, RAW_INVENTORY) — `ArchiveTests` 3개(실제 `mklink /J` Junction 생성 포함)
- T06a `Evidence/ApprovalRecord.cs`, `Evidence/TrustAnchor.cs` (승인 기록 v1·실행 허가·프로필 3종·앵커·서비스 설정 NSC-5·NSC-4 B안 ACL) — `ApprovalAndTrustTests` 47개
- NSC-4 실측(이 PC, 표준 사용자, 비관리자): `C:\Windows\System32\kernel32.dll`, `…\drivers\etc\hosts`는 상위 디렉터리의 `(A;OICIIO;GA;;;CO)`(CREATOR OWNER 상속 전용 ACE) 때문에 거부, `C:\`는 `(A;;LC;;;AU)`(Authenticated Users 하위 디렉터리 추가)·`(A;OICIIO;SDGXGWGR;;;AU)` 보유, 사용자 생성 파일은 소유자 위반. **B안을 문자 그대로 적용하면 표준 Windows 경로에서 앵커 위치가 성립하지 않음** → 완화하지 않고 OWNER 보고
- R01-A/B/D 리팩터링: `Official.cs`(`ReplayAuthority`, `VerifyEnvelope`, `BuildAttestation`), `Replay.cs`·`FamilyOfficial.cs`·`DominanceOfficial.cs`(권한 인자 전달, `Reruns` 추가 속성) — SimHarness 494/494 통과
- 신규 프로젝트 `tools/StarPlayer.EvidenceVerifier` (Pzl6Verifier, ExecutionEnvironment, Registry, Receipts, Verifier, Program), `tests/StarPlayer.EvidenceVerifier.Tests` 17개 통과, `Infrastructure.Tests/OfficialPzl6VerificationTests` 1개 통과(실제 v2 codec, Integration 끝-끝 검증)
- 경계 결정 필요: `ScriptedGameJson`이 internal이고 Infrastructure의 InternalsVisibleTo 목록·공개 형식 목록이 `TrustedBoundaryTests`로 고정됨 → 검증기 바이너리에 실제 codec 연결 불가. 현재는 delegate 주입, Infrastructure.Tests에서만 결합

## T10 (2026-10-24)

- 독립 Golden: `tools/b1-golden/golden.py`(Python 3.14.7, C# 미참조) → `tests/Fixtures/b1-golden/expected.json`, `phase-input.json`. 동결 입력 53개(Dominance 문서 + Unit 48, Integration 문서 + Unit 2 + #6)는 `B1_GOLDEN_EXPORT` 설정 시에만 한 번 내보냄. `GoldenTests` 3개 통과(Dom·Int·Phase Payload 바이트 수·SHA·A+, GV-C02 DOM·PHASE 두 번째 구현 일치)
- 추가 반례 시험 `CertificationAndEnvironmentTests` 5개 통과(A14, A16, A26, A27, N11, N16, N29, N29-a, N30-a, N31)
- `verify.ps1`: B1 문구 13 projects, U6, D3. **사고:** 1차 재작성 때 BOM(`EF BB BF`)이 빠져 Windows PowerShell 5.1이 한국어 정규식을 잘못 읽어 G3·G4 FAIL → BOM 복원, 해당 실행 중단 후 전체 재실행(결과는 통합 보고서 3절)

## 2차 보완 — T11 이전 미완료 항목 (2026-10-24, OWNER 지시 "T11 이전 미완료 항목 정리 및 구현 보완")

- OWNER 결정안 작성(적용 안 함): `docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS.md` 제안 1(NSC-4 INHERIT_ONLY·경로 교체 권한), 제안 2(T05 codec 공개 진입점), 제안 3(R03-4 7단계 조회 수단)
- T06a: `TrustBootstrap.Resolve` — Registry ID 대조, L2 조회, L3 등록자·SHA, L4 읽기, L5 검증 집합 단일성, L6 프로필 3종·PATCH-04, T6 TRUST_CONFIG == 앵커 SHA, L7 허가, L8 재조회. `BootstrapAndProfileTests` 4개
- T06b: Calibration 실제 엔진 Run(12경기/층, SEEDS_EXHAUSTED) 끝-끝 검증. `TestProfiles`(필수 Case, Fixture `git ls-tree`, TEST_ASSEMBLY 바이트, 하위 `dotnet test` + TRX 대조), `PhaseVerifier`(13 Check, A28). Infrastructure.Tests Phase 루트 시험에서 4개 어셈블리 실제 실행(TRX 98.6초, §12.2 Case 전부 Passed)
- T09: `PipeRegistry.cs` — Win32 P/Invoke(LibraryImport, csproj `AllowUnsafeBlocks`), `RegistryPipeService`·`RegistryPipeClient`. `PipeRegistryTests` 5개(실제 Pipe). 1차 실패 1건: Pipe 위 `TrustBootstrap`에서 다른 앵커가 Instance ID는 같아 `registry_mismatch`가 아닌 것을 시험이 잘못 기대 → 시험을 서비스 앵커로 실제 Resolve + T6 `anchor_mismatch`로 수정
- AT-4 사전 측정: 이 계정(UAC 필터 관리자, Medium)이 Owner=다른 SID Pipe 생성 → Win32 1307(ERROR_INVALID_OWNER)
- T00: `tools/b1-golden/fields.py` → `B1_FIELD_TRACEABILITY.md`(616행). 첫 생성에서 `/units`·`/units/*` 규칙 누락으로 생성 실패(의도한 안전장치) → 규칙 추가
- T10: 하네스 `CounterexampleTests` 5개(N01·N02·N33·N34·N05-c·N05-e·N35), 검증기 `VerifierCounterexampleTests` 4개(A04·A12·A13·N20·N22-b). 1차 실패 2건: N05-c를 스키마가 잡는다고 잘못 기대(문서에 투구 수 정보 없음 → 재투영 비교에서 잡힘으로 수정), 재투영에 원래 Run을 넘겨 `observation_missing`(재실행 Run으로 수정)

- 전체 verify 1차(보완 후): `GATE: INCOMPLETE` — U6 testhost 비정상 종료(`RegistryPipeService.Dispose`가 서비스 스레드 사용 중인 핸들을 닫아 `FlushFileBuffers`가 `ObjectDisposedException`), U4 로그 재기록 파일 잠금(손자 프로세스 추정). 수정: 스레드별 핸들 소유·깨움 후 종료 대기·세션 예외 Fail-Closed, `verify.ps1` 로그 재기록 재시도(BOM 유지). 2차 `GATE: PASS` 2,972 PASS(18:58:49)

## 3차 — OWNER 결정 2 반영 (2026-10-24, 수신 21:49:42 +09:00)

- 결정 기록: `docs/contracts/b1/B1-T00_OWNER_DECISION_2.md`(결정 1~3 + 해석 1·2·3·6 결정 표). 제안 문서는 원문 보존 + 처리 결과 표기
- 결정 1(NSC-4): `AnchorAcl` — `AclTarget`(앵커 파일·앵커 디렉터리·상위), INHERIT_ONLY 자체 미적용 + 상속 판정(`Inherits`, CREATOR OWNER·GROUP 치환, 보호 DACL 상속 없음), 모호 SID 거부, `CheckChain` 하향 판정, 서술자 읽기 실패 Fail-Closed. 이 PC 실제 서술자 측정(`Get-Acl`, 읽기만): `hosts`·`kernel32.dll` 사슬 통과. 후보 `C:\ProgramData\StarPlayerTrust`(목표 SDDL)는 실제 `C:\`·`C:\ProgramData`와 결합해 통과, 공격 8종 거부. 후보 디렉터리는 만들지 않음(관리자 작업). 시험 기대 오류 2건(하향 순서상 첫 위반은 사용자 프로필 디렉터리, Junction 정리) 수정
- 결정 2(T05): `src/StarPlayer.Infrastructure/Fixtures/EventJsonV2Codec.cs`, 검증기 기본 codec, `TrustedBoundaryTests` 최소 예외 + 표면 고정 시험, `Pzl6StandaloneTests` 2개. Fixture 수정 1건(Archive 재작성 금지 → Integration Unit·Seed는 세계당 한 번)
- 결정 3(T09): `get_registry_identity`(닫힌 형식, 읽기 전용, 인증된 모든 역할), 클라이언트 Connect가 5단계 뒤 같은 연결에서 먼저 조회, 일반 응답의 Registry ID 제거, 끊긴 세션 재사용 금지
- N07: `WitnessWorld`(셀당 4경기 실제 엔진 TEST_ONLY)에서 Witness 생성 → 순서 변경은 스키마 통과·재평가 비교 `evaluation_mismatch`. N32: 첫 결정에서 실패하는 시험 정책으로 실제 실행기가 B 경우 생성(stage exception, Commit 0), `GameStarter.Begin` 재구성 = `final_rng_canonical`
- 전체 verify: 한 번에 `GATE: PASS` 3,001 PASS(22:26:24)

## T11 FIX_REQUIRED 수정 (2026-10-25)

- 지시: 교체된 루트 `TASK.md`(SHA `90ec63b9…`)와 Codex 보고서(대화로 수신, `B1_T11_CODEX_REVIEW_RECEIVED.md`). TASK.md 교체로 D3 기준 SHA 불일치 — 되돌리지 않고 보고
- Red 확인 후 수정: T11-05(환경 프로필 2 FAIL), T11-07(ImpersonateSelf=false에서 Revert 미호출), T11-08(S4 오류 미기록, 중지 10초 지연), T11-04(Hook으로 부모를 Junction 교체 → 루트 밖 파일 생성 재현)
- 수정 후 작성: T11-01(Certification 경계), T11-02(자식 결속), T11-03(전체 참조·run 파일·#6 JSON), T11-06(Reader 클라이언트)
- 사고: Pipe 중지에서 다른 스레드의 DisconnectNamedPipe가 동기 핸들 I/O 뒤에 대기해 시험 교착 → 실행 중단 후 CancelSynchronousIo로 변경. EvidenceVerifier 1차 전체 실행 9 FAIL(시험 측 오류) → 수정 후 54/54
- verify: GATE FAIL(D3 TASK.md만), 시험 3,017 PASS

## D3 복구 (2026-10-25)

- 교체본 TASK.md → `docs/tasks/2026-10-25_B1_T11_FIX_REQUIRED_TASK.md`(바이트 동일). 원본 탐색: Git blob 1,539개 없음, 로컬 파일 67,039개 중 VS Code History `-182b8fa2/yzag.md` 1개 SHA 일치(`entries.json` 대상 = 이 저장소 TASK.md) → 바이트 복사 복원. `sha256sum -c` 5개 OK, 기준 목록 무변경
- verify 06:26:54 GATE PASS 3,017. 잔여 결정안 3 작성

## OWNER 결정 3 구현 (2026-10-25, 수신 08:04:02)

- 결정 기록 작성 → GAME_RESULTS 형식(`GameResultsEvidence`, GAME_EVENTS framing 공통 함수로 추출)·생산자·Archive 역할/개수·검증기 비교 → #6 JSON 역할 PZL6 → 관찰 Seam `FinalState`·`Pzl6Verifier.FinalDifference` → RAW_INVENTORY Reader·`Receipts.CheckInventory`
- 시험 1차 실패 3건(모두 시험 쪽 기대: 옛 Unit PZL 참조 규칙, T11-03 거부 원인 순서, 공개 API 단정 범위) → 갱신
- Dry Pilot 9 Run 재현 `ALL_9_IDENTICAL=True`(DLL `13198f07…`). verify 08:37:27 GATE PASS 3,026

## T11 2차 수정 R1~R4 (2026-10-25)

- 지시서: [B1_T11_FIX_R2_TASK.md](../tasks/B1_T11_FIX_R2_TASK.md). R2 도출 방식은 OWNER가 "승인 Pin 기준"을 선택했습니다(채팅). Codex 2차 원문은 받지 못했습니다.
- 진행 순서: R4 → R1 → R2·R3 → Phase 양성 Fixture. 단계마다 Red를 실제로 실행했습니다(원시 [B1_T11_FIX_R2_RED.log](B1_T11_FIX_R2_RED.log)).
- 양성 Phase 경로를 위해 TEST_ONLY 48셀 Seed 기준값을 991000으로 바꿨습니다. 임시 탐색 결과 990000은 NOT_EVALUATED, 991000은 PASS였고, 탐색 코드는 삭제했습니다.
- 한 차례 중단이 있었습니다. 명령 승인 서비스가 판정을 반환하지 않아(서버 측 일시 오류) 실행을 멈췄고, 보안 검사를 우회하지 않았습니다. OWNER의 "계속 진행해"를 받고 Infrastructure 양성 시험부터 재개했습니다.
- 결과:
  - Infrastructure Phase 영수증 끝-끝 1/1 PASS
  - Dry Pilot 9 Run `ALL_9_IDENTICAL=True`(DLL `96baca7e…0902`, 재현 스크립트 `tools/b1-drypilot-repro.ps1`를 저장소에 추가)
  - verify 15:49:34 GATE PASS 3,032
- 보고서: [B1_T11_FIX_R2_REPORT.md](B1_T11_FIX_R2_REPORT.md)

## T11 R2 최종 수정 R2-A·R2-B (2026-10-25)

- 지시서: [B1_T11_R2_FINAL_FIX_TASK.md](../tasks/B1_T11_R2_FINAL_FIX_TASK.md). Codex R2 원문은 받지 못했습니다. R1·R3·R4는 재설계하지 않았습니다.
- 조사 결과:
  - 실제 소비 입력은 내장 리소스 2개(`virtual_tuning_v4.json`·`game_rules_profiles_v2.json`)와 승인 Profile 2개입니다. 구장·코호트·정책·실행 프로파일은 코드입니다.
  - 튜닝 파일은 작업 트리 CRLF(system `core.autocrlf=true`), Git blob LF이며, 빌드는 작업 트리 바이트를 내장합니다.
  - Preflight는 파일을 쓰지 않았고, 색인(DIAGNOSTICS)에는 Family 검사 요약만 있습니다.
- Red: R2A·R2B 각 1개를 실제로 실행했습니다(현재 둘 다 통과 = 결함). 원시 출력은 [B1_T11_R2_FINAL_RED.log](B1_T11_R2_FINAL_RED.log)입니다.
- 구현:
  - `HarnessData.Inputs`·`ResourceBytes`
  - 공식 실행이 첫 Unit 전에 기존 형식의 Preflight 기록을 씁니다(`OfficialPins.WritePreflight`).
  - `EvidenceWriter.WriteRun`이 PREFLIGHT를 보존합니다.
  - 검증기: `ConsumedInputs`·`PreflightRecords`(신규 `RequiredOriginals.cs`), `RequiredCounts`, `Certification.RequireOriginals`(Family 목록 인자)
  - Fixture: 세계 루트에 content 원본 복사, 생산자가 내장 입력 2개 보존
- 단계 시험: SimHarness 538/538 PASS(신규 2)
- 단계 시험(이어서):
  - EvidenceVerifier 67/68: `R3_two_families…`의 공유 원본 수 4 → 6은 입력 2개 증가에 따른 정확한 개수 갱신이며, 재실행 1/1 PASS
  - Infrastructure Phase 영수증 끝-끝 1/1
  - Dry Pilot `ALL_9_IDENTICAL=True`(DLL `2d017ffa…ec0`)
  - verify 18:41:01 **GATE PASS 3,039**
- 보고서: [B1_T11_R2_FINAL_FIX_REPORT.md](B1_T11_R2_FINAL_FIX_REPORT.md). Codex READ-ONLY R2 재검수 대기 상태에서 멈춤

## T11 R2-B 값 결속 (2026-10-25)

- 지시서: [B1_T11_R2B_VALUE_BINDING_TASK.md](../tasks/B1_T11_R2B_VALUE_BINDING_TASK.md). R1·R2-A·R3·R4 RESOLVED이므로 R2-B MINOR만 다룹니다. Codex 원문은 받지 못했습니다.
- RED: `R2B_value_*` 2개를 실제로 실행했습니다(복사 Archive에서 PREFLIGHT 값 하나 변경, SHA·길이·참조·문서 재작성). Calibration `schema_status` 변경과 Dominance `games_per_cell` 2→3이 영수증까지 발급되었습니다. 원시 출력: [B1_T11_R2B_VALUE_RED.log](B1_T11_R2B_VALUE_RED.log)
- 구현:
  - `PreflightRecords`를 기대 기록 투영 + 전체 JSON 트리 비교로 바꿨습니다(`SameMembers`·부분 필드 비교 제거).
  - READY 메시지 3개를 SimHarness 단일 정의(공개 상수·함수)로 옮겼습니다. 바이트는 그대로입니다.
  - 값 비교 제외는 `output_directory` 메시지 1개로 좁혔습니다.
- 단계 시험: R2B 4/4 PASS
- 단계 시험(이어서): EvidenceVerifier 70/70, SimHarness 538/538, Phase 영수증 1/1, Dry Pilot `ALL_9_IDENTICAL=True`(DLL `31110bf5…117b`), verify 19:40:30 **GATE PASS 3,041**
- 보고서: [B1_T11_R2B_VALUE_BINDING_REPORT.md](B1_T11_R2B_VALUE_BINDING_REPORT.md). Codex READ-ONLY 재검수 대기 상태에서 멈춤

## B1 코드 수용 준비 (2026-10-25)

- 지시서: [B1_CODE_ACCEPTANCE_TASK.md](../tasks/B1_CODE_ACCEPTANCE_TASK.md). Codex 최종 READ-ONLY PASS(OWNER 전달 요약, 원문 없음)를 받은 뒤의 준비 작업이며, 코드·시험은 바꾸지 않았습니다.
- 산출물:
  - [Codex PASS 수신 기록](B1_T11_CODEX_FINAL_PASS_RECEIVED.md)
  - [OWNER 수용 확인서 초안·출처 확인표](B1_OWNER_ACCEPTANCE_DRAFT.md)
  - [구현 목록·SHA](B1_IMPLEMENTATION_MANIFEST.md)
  - [운영 Gate 계획](../tasks/B1_OPERATIONAL_GATE_PLAN.md)
- Stage·Commit·Push·Tag는 하지 않았습니다. OWNER 결정 대기 상태에서 멈춤

## B1 코드 수용·Git 기준본 고정 (2026-10-26)

- OWNER 결정: B1 코드 구현 수용(현재 검수 범위), G2 개행 정책, 조건부 로컬 Commit·annotated Tag 허가, Push 불허, 운영 Gate 실행 불허
  - 원문: [O16](../contracts/b1/originals/owner/O16_20261025T151044Z_code-acceptance-and-git-baseline-decision.txt)
  - 기록: [B1_CODE_ACCEPTANCE_OWNER_DECISION.md](../contracts/b1/B1_CODE_ACCEPTANCE_OWNER_DECISION.md)
- 원문 보존: 로컬 Claude Code·Codex 세션 기록에서 OWNER 지시 16건과 Codex 원문 12건(판정 7·요청 5)을 바이트 그대로 추출했습니다([색인](../contracts/b1/originals/INDEX.md)).
  - 비밀정보 검출 0건
  - 출처 상태: NOT_INDEPENDENTLY_VERIFIED(로컬 기록)
  - S00~S07 자료집: UNAVAILABLE
- G2: `.gitattributes`에 정확한 바이트(`-text`/`binary`)와 명시적 개행(`eol=lf`/`eol=crlf`) 규칙을 추가했습니다. 일괄 정규화는 없습니다.
  - `git status` 변화는 없었습니다.
  - Index blob 변화는 내용 수정 파일과 튜닝 JSON(검수 바이트 CRLF 보존)뿐입니다.
- 이후 Commit·깨끗한 체크아웃 검증·Tag 결과는 Tag 메시지와 Commit 이후 기록에 남깁니다.

## 중단 이력

- 2026-10-24 사용량 한도로 T04b 직후 한 번 중단, 같은 날 재개해 T05~T10 진행(위 기록). 남은 항목은 통합 보고서 8절
- Git Commit·Push 하지 않음

## 진행 순서

T00 → (T01 ∥ T02 ∥ T04a) → (T03 ∥ T04b ∥ T06a ∥ T08) → (T05 ∥ T07) → T06b → T09 → T10
