# Star Player B1 — Codex T11 표적 재검수 TASK (READ-ONLY)

- 작성: Claude Code, 2026-10-25. 현재 작업 트리 기준으로 작성한 **재검수 지시서**이며, 과거 구현 지시서를 소급해 만든 것이 아닙니다.
- 루트 `TASK.md`는 B1 NSC-6 승인 증거이며 D3 보호 대상입니다(SHA `ecbc0859…f825`). 이 문서는 그것을 대신하지 않습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
- 기준 Git: HEAD `257403ddae557a54c4013c17705370caeac7a2e0`, 스테이징 0, Commit·Push 없음(작업 트리 변경은 `git status`, 미추적은 `git status --porcelain --untracked-files=all`)

## 0. Codex 수행 제한 (반드시 지킬 것)

- **READ-ONLY:** 코드·문서·시험·`TASK.md`·계약을 수정하지 않는다. Stage·Commit·Push를 하지 않는다.
- 실행하지 않은 시험을 PASS로 표시하지 않는다. 빌드·시험·verify를 직접 실행했다면 명령과 결과를 그대로 적고, 실행하지 않았다면 "Claude 실행 증거 확인"으로 구분한다.
- 운영 Gate(별도 계정·관리자 작업·운영 값)를 충족으로 처리하지 않는다.
- 판정 우선순위: ① 승인 계약 `B1-T00_FINAL_CONTRACT_DRAFT.md` → ② OWNER 승인·결정 기록 → ③ 코드와 시험 → ④ Claude 보고서. **Claude의 완료 보고만으로 판정하지 않는다.**
- 운영 Gate 미완료와 코드 구현 결함을 구분해 보고한다.

## 1. 검수 대상 자료 (2026-10-25 작성 시점 실제 확인)

| 자료 | 경로 | 존재 | SHA-256(앞 16자) · 수정 시각 |
|---|---|---|---|
| 승인 B1-T00 최종 기술 계약 | `B1-T00_FINAL_CONTRACT_DRAFT.md` | 있음 | `1338a2f7e4bf2e95` · 2026-10-24 12:21:33 |
| 계약 승인 보존본 | `docs/contracts/b1/B1-T00_FINAL_CONTRACT.approved.md` | 있음 | `1338a2f7e4bf2e95` · 2026-10-24 12:21:33 |
| OWNER 승인 기록 | `docs/contracts/b1/B1-T00_OWNER_APPROVAL_RECORD.md` | 있음 | `cb8dd03b560abe6a` · 2026-10-24 14:26:40 |
| OWNER 결정 기록 1 (NSC-4·NSC-5) | `docs/contracts/b1/B1-T00_NSC4_NSC5_OWNER_DECISION.md` | 있음 | `af7142e534da18c8` · 2026-10-24 14:27:15 |
| OWNER 결정 기록 2 | `docs/contracts/b1/B1-T00_OWNER_DECISION_2.md` | 있음 | `fb469705a3a545d5` · 2026-10-24 22:11:07 |
| OWNER 결정 기록 3 | `docs/contracts/b1/B1-T00_OWNER_DECISION_3.md` | 있음 | `bcb7bee78a177e2b` · 2026-10-25 08:04:34 |
| 결정 3 근거안 | `docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS_3_FINAL.md` | 있음 | `94bdf0c60679d4a6` · 2026-10-25 06:36:58 |
| T11 수정 보고서 | `docs/reports/B1_T11_FIX_REPORT.md` | 있음 | `f8e915401533446c` · 2026-10-25 06:28:16 |
| 결정 3 적용 보고서 | `docs/reports/B1_DECISION3_REPORT.md` | 있음 | `08e0263989849418` · 2026-10-25 08:38:51 |
| Codex 최초 T11 FIX_REQUIRED 보고서(수신본, 원문 보존) | `docs/reports/B1_T11_CODEX_REVIEW_RECEIVED.md` | 있음 | `e36fb586c403e9ab` · 2026-10-25 05:49:56 |
| T11 수정 작업 지시 원본(교체본 보관) | `docs/tasks/2026-10-25_B1_T11_FIX_REQUIRED_TASK.md` | 있음 | `90ec63b9c759171f` · 2026-10-25 04:25:42 |
| 현재 전체 verify 로그 | `docs/reports/B1_VERIFY_LOG.md` (Run at 2026-10-25 08:37:27 +09:00, `GATE: PASS`) | 있음 | `725a250ee6f9d8b1` · 2026-10-25 08:37:28 |
| verify 원시 결과 | `build/verify/verify-result.json`(UTF-8 BOM), `build/verify/U1~U6.log` 등 | 있음(git-ignored) | `22f15eddde313261` · 2026-10-25 08:37:28 |
| 기준본 SHA 목록 | `docs/contracts/b1/B1-T00_BASELINE.sha256` | 있음 | `a9865a2f7db4bfe4` · 2026-10-24 14:26:49 (`sha256sum -c` 5개 OK) |
| 독립 Golden | `tests/Fixtures/b1-golden/expected.json`, `tools/b1-golden/golden.py` | 있음 | `a07d0afa44765fb5` · 2026-10-24 18:02:45 |
| Dry Pilot 재현 결과 | `build/b1-t04a-repro/<9 run>/manifest.json` ↔ `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_dry_run1/runs/*-a/manifest.json` | 있음(git-ignored) | 2026-10-25 결정 3 Seam 변경 후 재생성 |

**근거 공백(Codex가 별도 확인 대상으로 기록할 것):**

1. **OWNER 결정 3의 승인 근거:** 결정 3은 Claude Code 채팅으로 받은 OWNER 텍스트 지시("Star Player B1 — OWNER 최종 설계 결정 7건 승인 및 구현 허가", 2026-10-25 08:04:02 수신)를 근거로 Claude가 작성한 기록입니다. **그 지시 원문 사본은 저장소에 없습니다**(결정 2·승인 기록과 같은 방식, T11 수정 지시처럼 `docs/tasks/`에 보관된 원문은 없음). 따라서 Codex는 결정 3을 "OWNER 원문으로 확인된 승인"으로 단정하지 말고, **OWNER가 결정 기록 3의 내용이 자신의 지시와 같다는 확인**을 별도 확인 대상으로 기록합니다. 결정 2·승인 기록·NSC 결정도 같은 성격(채팅 지시 기반 Claude 작성 기록)입니다.
2. **Dry Pilot 재현 스크립트:** 비교에 쓴 스크립트(`repro_drypilot.ps1`)는 Claude 작업 폴더에 있고 저장소에는 없습니다. Codex는 위 두 Manifest 집합을 직접 대조(환경 필드 `environment_not_hashed` 제외)하거나 결과를 "Claude 실행 증거"로 표시합니다.

## 2. 검수 범위 A — T11 Findings 8건

각 항목에 대해 **수정 코드가 지적을 실제로 해소했는지**, **시험이 그 경로를 직접 겨냥하는지**, **계약 조건을 충족하는지**를 판정합니다. 행 번호는 작성 시점 기준입니다.

### T11-01 MAJOR — 검증 결과와 영수증 발급 결속

- **기존 지적:** `Receipts.Issue`가 호출자 영수증·`recheck=()=>null`을 받고, Trust Bootstrap·환경·Test Profile을 필수 선행으로 묶는 Production 경로가 없으며, PZL 적격성을 보지 않음.
- **수정 위치:** `tools/StarPlayer.EvidenceVerifier/Certification.cs`(신규: `Open` 66, `IssueFamily` 156, `IssuePhase` 169, `Recheck` 149), `Receipts.cs`(`Issue` 147 → `internal`, `PzlEligibility` 117), `Verifier.cs`(검증 결과 등록 `ConditionalWeakTable` 127, `VerifyFamily` 131)
- **수정 내용:** 세션은 E2-1 → Trust Bootstrap(E2-2/3) → E2-4(C0·C1) → E2-5 통과 시에만 생성. 결과는 세션 안에서 검증된 인스턴스만 인정(`new`·`with` 결과 거부). 영수증은 내부에서 구성(target·A+·승인·원본 목록·`test_only`·PZL), §19 기술·통계·PZL 축, E2-7 내부 재수집. 공식 CLI 발급 명령은 추가하지 않음.
- **회귀 시험:** `tests/StarPlayer.EvidenceVerifier.Tests/T11CertificationTests.cs`의 `T11_01_*` 3개, `OfficialPzl6VerificationTests`(세션 경로 Phase 루트)
- **계약 조건:** §18.4 E2-1~E2-7, §19, §20.2 1~7
- **남은 위험(확인 요청):** Pipe 클라이언트의 공개 시험 Seam(`identitySeam`·`accessSeam`)은 연결을 `Trusted=false`로 만드는지, 검증기 실행 바이너리 자기 SHA와 VERIFIER_BINARY 대조가 없는 점, 검증 결과 결속이 프로세스 내부(ConditionalWeakTable)라는 점, 공개 API 단정의 범위(4절 D).

### T11-02 MAJOR — Phase 자식 영수증 결속

- **기존 지적:** `RequireChildren`이 개수·등록만 보고 중복·역할·Payload·집합·TEST_ONLY를 보지 않으며 발급 경로에서 호출되지 않음.
- **수정 위치:** `Certification.cs` `BindChildren` 190(`IssuePhase`가 가장 먼저 호출), `PhaseVerifier.RequireChildren` 제거
- **수정 내용:** 정확히 3개·서로 다른 ID·등록 바이트 동일·등록 바이트 엄격 파싱·역할 CAL→INT→DOM·검증 집합·승인 ID/SHA/Freeze·target(run_id·evidence_id·A+·상태) = 세션 검증 결과·`test_only` = 세션·(결정 3 §2) 자식 RAW_INVENTORY 정합.
- **회귀 시험:** `T11CertificationTests.T11_02_*`, `Decision3VerifierTests.cs`의 `Decision3_2_*`(Phase 경계 인벤토리 거부)
- **계약 조건:** §9.2, §20.1, §20.3(A28)
- **남은 위험:** TEST_ONLY 세계에서 Dominance가 §19 인증 불가라 **Phase 영수증 발급 성공 경로는 시험되지 않음**(거부 경로만).

### T11-03 MAJOR — Archive 전체 참조·Replay 스냅샷

- **기존 지적:** Unit PZL의 GAME_EVENTS 외 참조와 PZL6 하위 참조 미검증, §21 필수 역할·개수 미검증, Replay·Attestation이 외부 결과 디렉터리를 다시 읽음.
- **수정 위치:** `Verifier.cs` `VerifyFamily` 131(전체 참조 Snapshot·artifact_id 단일성), `RequiredCounts` 313, `RunFile` 332, `VerifyPzl6` 408, `tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs` `WriteRun` 56, `Pzl6Verifier.cs` `Bundle` 203
- **수정 내용:** 모든 ArtifactRef를 한 번씩 읽어 SHA·길이 확인, 같은 ID는 한 참조만. 실행 색인·Unit manifest를 Archive에 보존하고 그 바이트를 새 전용 디렉터리에 펼쳐 기존 Replay 실행, Attestation도 같은 바이트로 파싱. `FamilyVerificationRequest`에서 결과 디렉터리 입력 제거.
- **회귀 시험:** `T11CertificationTests.T11_03_*` 2개, `Pzl6StandaloneTests`
- **계약 조건:** §2.5 ArtifactRef, §20.2 1, §21(역할·개수·재열기 금지), R-13
- **남은 위험:** Replay용 임시 디렉터리(`%TEMP%\starplayer-verifier\<guid>`) 사용, 실행 색인 역할 DIAGNOSTICS 배정(결정 6에서 내부 배치로 수용).

### T11-04 MAJOR — Archive 경로 교체 경쟁

- **기존 지적:** 경로 검사 후 문자열 경로로 생성·읽기(TOCTOU), 영수증 재시도 읽기도 경로 기반.
- **수정 위치:** `tools/StarPlayer.SimHarness/PathSafety.cs` `PinnedDirectory.Open` 121·`ReadFile` 212, `Evidence/Archive.cs` `Snapshot` 40·`WriteNew` 170·`TryRead` 190, `Receipts.Issue`(재시도 읽기 → `TryRead`)
- **수정 내용:** 볼륨 루트부터 핸들 사슬 고정(링크 미추적, FILE_SHARE_DELETE 없음), FILE_CREATE 원자 생성, 고정 부모 기준 핸들 읽기.
- **회귀 시험:** `tests/StarPlayer.SimHarness.Tests/Evidence/ArchiveTests.cs` `T11_04_*` 3개(Hook으로 확인~사용 사이 부모를 Junction 교체 — 수정 전 Red 확인: 루트 밖 파일 생성)
- **계약 조건:** §2.5 "실제 접근에는 핸들·Reparse Point 검증", §21 보존 규칙
- **남은 위험:** Test Profile은 TEST_ASSEMBLY SHA 확인 후 경로로 실행(교체 창), 기존 Replay가 Seed를 저장소 경로로 읽는 점(승인 Pin SHA로 검사).

### T11-05 MAJOR — Environment Profile 정확한 집합

- **기존 지적:** `runtime_files` 3행·`upper_quantile` 36행 길이만 강제, 비교가 제출 배열만 순회.
- **수정 위치:** `tools/StarPlayer.SimHarness/Evidence/ApprovalRecord.cs` `RuntimeFileNames` 71·`Exactly` 83, `EvidenceVerifier/ExecutionEnvironment.cs` `Mismatch` 56·`GoldenMismatch` 102
- **회귀 시험:** `T11FixTests.cs` `T11EnvironmentProfileTests` 2개(Codex 경로·중복·재정렬·누락 — Red 확인)
- **계약 조건:** §18.2, E2-4, E2-5
- **남은 위험:** 운영 Environment Profile·비트 Golden 승인값 없음(운영 Gate).

### T11-06 MINOR — Reader 전용 클라이언트

- **수정 위치:** `PipeRegistry.cs` `RegistryReaderClient` 515, 공용 `PipeSession` 542
- **수정 내용:** 같은 1~9단계 서버 인증을 공유, 1단계는 `evidence_reader_sids`만, Get·GetByScope만, `IEvidenceRegistry` 아님.
- **회귀 시험:** `T11FixTests.cs` `T11ReaderClientTests`
- **계약 조건:** R-03-4
- **남은 위험:** 한 계정·Seam 시험만(운영 계정 분리 AT 미실시).

### T11-07 MINOR — ImpersonateSelf 실패 정리

- **수정 위치:** `PipeRegistry.cs` `SelfAccess` 159(`AccessCalls` Seam), `Registry.cs` `CheckAccess` 317(FailFast 조건)
- **회귀 시험:** `T11FixTests.cs` `T11SelfAccessTests`(실제 래퍼 호출 순서·횟수 — Red 확인)
- **계약 조건:** MINOR-01, R03-4 5-7
- **남은 위험:** 실제 Win32 경로는 이 계정의 성공·거부 경로만 실측.

### T11-08 MINOR — Pipe 인스턴스 생성 실패

- **수정 위치:** `PipeRegistry.cs` `Grow` 318, `Dispose` 436
- **수정 내용:** n < 4에서 3회 실패 → `Fault` 기록(Trace) 후 정상 중지, 포화와 구분, 중지는 `CancelSynchronousIo`로 각 스레드의 동기 I/O 취소(읽지 않는 클라이언트·FlushFileBuffers 대기 해제), 스레드가 자기 핸들만 닫음.
- **회귀 시험:** `T11FixTests.cs` `T11PipeInstanceTests` 3개(Red 확인: 오류 미기록, 중지 10초 지연·스레드 잔존)
- **계약 조건:** S3·S4·S5·S6
- **남은 위험:** 오류 기록이 `Trace`뿐(영속 이벤트 로그 없음), 운영 계정 배치 수용 시험 미실시.

## 3. 검수 범위 B — OWNER 결정 7건 (결정 기록 3)

| # | 확인할 것 | 코드·시험 |
|---|---|---|
| 1 | violations: (경기, 원인) 1건, 4필드, `(game_id, event_id, field, code)` ordinal·null 먼저·중복 금지가 Unit PZL·#6·Phase 읽기에서 강제, 제출값 = 재계산 | `UnitPzlEvidence.Checks` 127·`Sorted` 160, `EvidenceRules.Violations` 499, `EvidenceHash.PhaseChecks`; 기존 `UnitPzlEvidenceTests`·`EvidenceHashTests` |
| 2 | RAW_INVENTORY: 정확히 `{artifacts, format}`, 형식 ID, ArtifactRef 7필드, 정렬·중복·금지 역할, 영수증 ORIGINAL source_files와 동일, 발급 경계·Phase 자식에서 필수 실행 | `Archive.ReadInventory` 128, `Receipts.CheckInventory` 98, `Certification.Issue`·`BindChildren`; `Decision3EvidenceTests.Decision3_2_*`, `Decision3VerifierTests.Decision3_2_*` |
| 3 | witness_digest: `difference`·`standard_error`·`lower_bound`·`non_superiority_z` BITS64, holm_critical BITS64\|null, 240개·생성 순서, 통계식 무변경 | `ExecutionEnvironment.WitnessDigest` 87; `CertificationAndEnvironmentTests` |
| 4 | #6: 이중 재생 비교 + **Production 최종 GameState 전 필드** + **커밋별 GameFact** + 커버리지(GameState·FinalizedGame·EventReceipt·GameFact 형식), Production 상태 없음·누락 필드 → 실패. 관찰 Seam이 읽기 전용이고 RNG·Hash·상태를 바꾸지 않음 | `PzlEventObserver.Finished` 53, `GameExecutor.cs` 33·86(`events?.Finished`), `Pzl6Verifier.FinalDifference` 72·`CoverageGap` 57; `Decision3EvidenceTests.Decision3_4_*`, `Decision3VerifierTests.Decision3_4_*`, Dry Pilot 재현 |
| 5 | GAME_RESULTS: 닫힌 Header 7멤버·`{game, record}`·End 4멤버, JCS·UTF-8·LF·1 MiB, 실행 경기 순서·실패 경기 포함, `game` JCS = 부모 문서 행 = 재실행 투영, `result_hash` = Unit PZL, Unit PZL당·스냅샷 전체에서 Unit별 정확히 1, Family/Dominance 직접 참조 금지(R-13), 오류 분류 | `GameResultsEvidence.Read` 40·`Compare` 66, `EvidenceShapes.GameResults*`, `Archive.CheckReferences` 79·`AllowedRoles` 62, `Verifier.RequiredCounts` 313·비교 270; `Decision3EvidenceTests.Decision3_5_*` 3개, `Decision3VerifierTests.Decision3_5_*`, `ArchiveTests` |
| 6 | `run/<run_id>/…`: 내부 배치로만 쓰이고 B-4 공식 경로를 주장하지 않음, Hash·Replay 의미 무변경 | `EvidenceWriter.WriteRun` 56, `Verifier.RunFile` 332; `T11_03_*` |
| 7 | #6 JSON: role PZL6·`game_id` 지정·ID `pzl6-json-<mode>-<index:D2>`, #6 문서 PZL6·game_id null 1개, 합계 21·ID 고유, 표본 계획(UnitId·GameId·Index, Seed는 표본 경유)과 일치, 바이트·SHA·길이 검증, GAME_RESULTS 집계 제외 | `Archive.AllowedRoles`·`CheckReferences(Pzl6)`, `Verifier.VerifyPzl6` 408·`RequiredCounts`; `Decision3VerifierTests.Decision3_7_*` |

## 4. 검수 범위 C — 불변성

- 기존 GameResultHash·RunResultHash·ConfigHash·Attestation Hash 계산식 무변경(`tools/StarPlayer.SimHarness/{GameExecutor,Official,Replay,FamilyOfficial,DominanceOfficial}.cs` diff 확인 — 결정 3 라운드의 실행 코드 변경은 `GameExecutor.cs`의 읽기 전용 호출 2곳뿐이라고 보고됨)
- RNG 소비·Replay 순서 무변경(관찰 Seam이 그리지·결정하지·바꾸지 않음)
- Dry Pilot 9 Run: 보고값 `ALL_9_IDENTICAL=True`, DLL `13198f07ea87ea4e01d8004cc46812d22382fa62c1bc6d9221fa4692645a2d39`(1절 근거 공백 2)
- 기존 Golden 4개(`GoldenTests`, 고정 입력·`expected.json` 무변경)
- D3 기준본 SHA(`sha256sum -c docs/contracts/b1/B1-T00_BASELINE.sha256` 5개)

## 5. 검수 범위 D — 시험 검수

- **최종 3,026 PASS의 실제 범위:** verify 로그·원시 로그로 프로젝트별 수(Domain 327, Simulation 741, Application 128, Infrastructure 1,237, SimHarness 535, EvidenceVerifier 58)와 FAIL 0·SKIP 0, U3a·U3b가 Application 부분 재실행이라 합계에 중복되지 않음을 확인. Phase 루트 시험(Infrastructure)이 하위 `dotnet test`로 4개 시험 어셈블리를 실제 실행하는지.
- **기대값이 바뀐 시험(완화 여부 판단 요청):**
  1. `T11CertificationTests.T11_03_the_replay_reads_only_the_archived_run_files…`: UNIT_MANIFEST 누락의 원인 코드 `Archive.run_file_missing` → `Archive.required_count`(둘 다 `archive_invalid`, 결정 3의 전체 개수 검사가 먼저 실행). DIAGNOSTICS 누락은 그대로 `run_file_missing`.
  2. `T11CertificationTests.T11_01_no_public_api_accepts…`: 공개 API 단정을 "이름 `receipt`의 JObject 인자 금지"에서 "JObject를 받아 `IssuedReceipt`를 반환하는 공개 메서드 금지 + `recheck` 인자 금지"로 바꿈(새 `Receipts.CheckInventory(JObject receipt, …)`는 검사만 함). 그 이전 T11 라운드에서도 같은 시험이 레코드 `Clone`과 Pipe 클라이언트 `Func<string>` Seam 때문에 좁혀졌음.
  3. 참고(규칙 강화): `ArchiveTests.Documents_reference_only_downwards…`는 "GAME_EVENTS만 있는 Unit PZL 유효"에서 "GAME_RESULTS도 정확히 1개 필요"로 바뀜.
- **분리:** 시험 PASS(실행 결과)와 계약 수용 PASS(조건 충족)를 따로 판정.

## 6. 판정 형식 (Codex 제출)

1. T11-01~08 각각: **RESOLVED / NOT_RESOLVED / INSUFFICIENT_EVIDENCE** (+ 근거 위치)
2. OWNER 결정 7건 각각: **COMPLIANT / NON_COMPLIANT / OWNER_DECISION_REQUIRED**
3. T10 수용 기준 충족 여부
4. B1 전체 코드 구현 수용 가능 여부(운영 Gate와 분리)
5. Windows 운영 Gate 잔여 사항: 보호 앵커 디렉터리 설치·ACL 실측, AT-1~AT-14r, AT-4/P-1 정식 측정, N27-d·e·f, 운영 Environment Profile 승인, N12 비트 Golden·witness_digest 승인값
6. 1절 근거 공백 2건의 처리(OWNER 확인 필요 여부)
7. **Codex T11 재검수 최종 판정: PASS / FIX_REQUIRED / BLOCKED / INSUFFICIENT_EVIDENCE**

## 7. 최종 상태 (변경 없음)

Official Execution = NOT_READY · Final Execution Freeze = PENDING · Phase 3 = INCOMPLETE. 이 TASK 작성은 문서 1개 추가뿐이며 코드 수정·시험 재실행·계약 변경·Commit·Push를 하지 않았습니다. Codex 재검수와 OWNER 최종 수용 전에는 B1을 완료로 처리하지 않습니다.
