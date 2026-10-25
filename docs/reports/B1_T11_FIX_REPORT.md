# B1 — Codex T11 FIX_REQUIRED 수정 보고서 (재검수 제출용)

- 작성: Claude Code, 2026-10-25 · 대상: [Codex B1-T11 검수 결과(수신본)](B1_T11_CODEX_REVIEW_RECEIVED.md) MAJOR 5 · MINOR 3 · 작업 지시: 루트 `TASK.md`
- 상태: **수정·자체 시험 완료, Codex 재검수 대기**. B1 전체 완료 아님 · Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · Git Commit·Push 하지 않음
- 바꾸지 않은 것: 승인 계약 `B1-T00_FINAL_CONTRACT_DRAFT.md`(SHA `1338a2f7…7d64`), OWNER 결정 기록 원문(승인 기록·NSC-4/5·결정 2), `TASK.md`, 사용자 PDF 2개, GameResultHash·RunResultHash·ConfigHash·Attestation Hash 계산, RNG 소비·Replay 순서

## 0. D3 기준본 — 복구 완료 (2026-10-25)

- 경위: 기준 SHA 목록의 `TASK.md`(`ecbc0859…f825`, B1 NSC-6 증거)가 2026-10-25 04:25에 T11 수정 지시로 덮어써져(`90ec63b9…f5f5`) D3가 FAIL이었습니다.
- OWNER 결정: 기존 SHA를 유지하는 복원. 현재 SHA로 기준 목록을 바꾸는 것은 불허.
- 수행:
  1. 덮어쓰기 전 교체본을 원본 바이트 그대로 [`docs/tasks/2026-10-25_B1_T11_FIX_REQUIRED_TASK.md`](../tasks/2026-10-25_B1_T11_FIX_REQUIRED_TASK.md)에 보관(`cmp` 동일, SHA `90ec63b9…f5f5`).
  2. 원본 탐색: Git 전체 객체(blob 1,539개, 스태시·떠도는 객체 포함) 없음. 로컬 파일 67,039개(SHA-256 대조, 읽기만) 중 VS Code 로컬 기록 `%APPDATA%\Code\User\History\-182b8fa2\yzag.md` 1개 일치. 같은 폴더 `entries.json`의 대상은 `file:///e%3A/Madegame/Star_player/TASK.md`, `yzag.md` 저장 시각 2026-10-24 12:14:14(승인 14:25 직전 마지막 판), 다음 판 `iCkd.md`(10-25 04:25:42)는 교체본과 SHA 동일.
  3. 그 파일을 바이트 복사해 `TASK.md` 복원 → SHA `ecbc0859c809c68151c93f92058c31a7ab0b84ce8d8cc9a4a25512b7dd06f825` 일치. 내용 재작성 없음.
  4. 확인: 계약 `1338a2f7…7d64`, 승인 보존본 `1338a2f7…7d64`, PDF 2개, `sha256sum -c B1-T00_BASELINE.sha256` 5개 OK. 기준 목록 파일은 수정하지 않음(내용 동일, 수정 시각 2026-10-24 14:26:49 그대로).
- 앞으로 `TASK.md`는 교체하지 않고 새 작업 지시는 [`docs/tasks/`](../tasks/README.md) 또는 채팅으로 관리합니다.

## 1. Findings별 수정·재현 시험·결과

Red→Green: **Red 확인** = 수정 전 코드에서 같은 시험이 실패하는 것을 실제로 실행해 확인 · **수정 후 작성** = 경계 자체가 없었거나 구조를 바꾼 뒤 작성(수정 전 Red 실행 없음, Codex 재현 경로를 겨냥).

| ID | 수정 | 수정 파일 | 재현·회귀 시험 | Red→Green | 수정 후 결과 |
|---|---|---|---|---|---|
| T11-01 MAJOR 발급 결속 | 유일한 Production 발급 경계 `Certification`: `Open`이 E2-1(금지 변수) → Trust Bootstrap(E2-2/3) → E2-4(C0 vs 프로필, C1 == C0) → E2-5(비트 Golden)를 모두 통과해야 세션을 만듦. 검증은 세션 안(`Certification.VerifyFamily`), 결과는 검증기가 만든 인스턴스만 인정(ConditionalWeakTable — `new`·`with`로 만든 결과 거부). 영수증은 검증 결과로 **내부에서** 구성(target·A+·승인·원본 목록·`test_only`·PZL 상태는 호출자 입력 아님), §19 기술·통계 + **PZL 축**(`Receipts.PzlEligibility`: pzl-5·12a PASS, pzl-10 PASS/정당 비해당, 12b DEFERRED, #6 PASS; FAIL·NOT_EVALUATED 거부), E2-7 재수집은 경계 내부. CANONICAL_PAYLOAD·RAW_INVENTORY를 VERIFIER_DERIVED로 보존·결속. `Receipts.Issue`는 `internal`(공개 Receipt·recheck 입력 제거). CLI 발급 명령은 추가하지 않음(공식 인증 운영 활성화 금지) | `tools/StarPlayer.EvidenceVerifier/Certification.cs`(신규), `Receipts.cs`, `Verifier.cs` | `T11CertificationTests`: 공개 API 표면, 결과 위조(`with`)·세션 밖 검증 결과·다른 세션 결과 거부, 영수증 내용 = 검증 값, E2-1·E2-4(C0·C1)·E2-7 거부, 부적격·PZL FAIL/NOT_EVALUATED 거부. `OfficialPzl6VerificationTests` Phase 루트가 세션 경로로 실행 | 수정 후 작성 | PASS |
| T11-02 MAJOR Phase 자식 결속 | `Certification.IssuePhase`가 **가장 먼저** 정확한 세 자식을 결속: 개수 3, 서로 다른 ID, 기록부에 같은 바이트로 등록, 등록 바이트를 엄격 파싱해 단일 target, 역할 CAL→INT→DOM, 검증 집합·승인 ID·SHA·Freeze, target(run_id·evidence_id·A+·기술·통계) = 이 세션이 검증한 Family, `test_only` = 세션. 이어서 세션 자신의 Test Profile 실행 결과, Phase 13 Check(12b DEFERRED 외 PASS). 약한 `PhaseVerifier.RequireChildren` 제거 | `Certification.cs`, `PhaseVerifier.cs` | `T11CertificationTests.T11_02_…`: 누락, `[c,c,c]`, 역할 교체, 미등록 바이트, A+·run_id 위조, test_only 위조, 다른 승인 기록(다른 검증 실행)의 정상 영수증 혼합 → 각 원인 코드. Dominance 자리 부적격 → 인증 불가(A28) | 수정 후 작성 | PASS |
| T11-03 MAJOR Archive 전체 참조·Replay 스냅샷 | 문서 → Unit PZL → PZL6의 **모든** ArtifactRef를 한 번씩 Snapshot(존재·길이·SHA), PZL6에도 `CheckReferences`, 같은 artifact_id는 하나의 참조만(충돌 거부). 실행 파일(DRAFT 색인 1 = DIAGNOSTICS, Unit별 manifest 1 = UNIT_MANIFEST)을 Archive `run/<run_id>/…`로 보존·필수화하고, 검증기가 Snapshot 바이트를 새 전용 디렉터리에 펼쳐 **그 바이트로만** 기존 Replay 실행, Attestation도 같은 바이트에서 파싱(외부 결과 디렉터리 입력 제거). PZL6의 20경기 JSON = 표본별 GAME_RESULTS 1개(기존 v2 Writer UTF-8 출력)를 필수화하고 검증기 Writer 출력과 바이트 대조. 영수증 source_files에 이 모든 원본 포함 | `Verifier.cs`, `Pzl6Verifier.cs`(`Bundle`), `tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs`(`WriteRun`), 시험 생산자 | `T11CertificationTests.T11_03_…`: Codex 경로(Unit PZL의 존재하지 않는 DIAGNOSTICS) → `Archive.missing`, artifact_id 충돌, 결과 디렉터리 변경 후에도 Archive 바이트로 검증, 색인·manifest 누락 → `Archive.run_file_missing`, PZL6 20개 JSON 보존. `Pzl6StandaloneTests`(손상 Writer·Reader 반례) | 수정 후 작성 | PASS |
| T11-04 MAJOR 경로 교체 경쟁 | 기존 `PinnedDirectory`를 재사용·확장: `Open`(볼륨 루트부터 핸들 사슬 고정, 링크 미추적, FILE_SHARE_DELETE 없음, 마지막 디렉터리 열기/생성), `ReadFile`(고정 부모 기준 핸들 읽기, 쓰기·삭제 공유 없음). `Archive.WriteNew`(FILE_CREATE 원자 생성 + 핸들 재읽기), `Archive.Snapshot`, 영수증 재시도 읽기(`Archive.TryRead`)가 모두 이 경로 사용. 기존 공식 출력 경로의 동작·오류 코드는 그대로 | `tools/StarPlayer.SimHarness/PathSafety.cs`, `Evidence/Archive.cs`, `Receipts.cs` | `ArchiveTests.T11_04_…`(시험 Hook으로 확인과 사용 사이에 부모를 Junction으로 교체): 쓰기 → 루트 밖 생성 차단·교체 불가, Snapshot 읽기 중 교체 불가, 재시도 읽기의 Junction 거부 | **Red 확인**(수정 전: 새 파일이 Archive 루트 밖에 생성, Snapshot 중 교체 성공) | PASS |
| T11-05 MAJOR 환경 프로필 정확한 집합 | Reader: `runtime_files` = 정확히 `System.Private.CoreLib.dll`·`clrjit.dll`·`coreclr.dll` ordinal 순서, `upper_quantile` divisor = 정확히 1..36 순서(중복·누락·재정렬 거부). 비교: 제출 배열이 아니라 필수 집합 기준으로 세 파일·36 divisor 모두 대조 | `tools/StarPlayer.SimHarness/Evidence/ApprovalRecord.cs`, `tools/StarPlayer.EvidenceVerifier/ExecutionEnvironment.cs` | `T11EnvironmentProfileTests`: Codex 경로(CoreLib 3행, divisor=1 36행), 중복·재정렬·누락 — Reader와 실제 비교 모두 거부 | **Red 확인**(2 FAIL) | PASS |
| T11-06 MINOR Reader 전용 클라이언트 | 연결·서버 인증 1~9단계를 `PipeSession`으로 추출해 두 클라이언트가 공유. `RegistryReaderClient`: 1단계에서 `evidence_reader_sids`만 허용, `Get`·`GetByScope`만 노출, `IEvidenceRegistry` 아님(Bootstrap·발급에 쓸 수 없음) | `tools/StarPlayer.EvidenceVerifier/PipeRegistry.cs` | `T11ReaderClientTests`: 공개 표면, 실제 Pipe 조회, 검증기·OWNER 계정 거부, 실제 AccessCheck 거부, 다른 Registry 신원 거부 | 수정 후 작성(형식 신설) | PASS |
| T11-07 MINOR ImpersonateSelf 실패 정리 | 5-7: ImpersonateSelf를 **호출했다면** 반환값과 무관하게 finally에서 RevertToSelf 1회, 토큰은 그 전에 닫음, Revert FALSE는 FailFast. Win32 호출을 `AccessCalls` Seam으로 묶어 순서·횟수 시험 | `PipeRegistry.cs`, `Registry.cs`(`CheckAccess` FailFast 조건) | `T11SelfAccessTests`: 실패·성공 경로별 실제 래퍼 호출 순서·횟수, Revert 실패 FailFast 2경로 | **Red 확인**(ImpersonateSelf=false에서 RevertToSelf 미호출) | PASS |
| T11-08 MINOR 인스턴스 생성 실패 | S4: n < 4에서 추가 생성 3회(1초 간격) 실패 → `Fault` 기록(Trace) 후 정상 중지(별도 스레드에서 Dispose). 포화(S3, n = 4)는 오류 아님으로 구분. 중지는 각 인스턴스 스레드의 동기 I/O를 `CancelSynchronousIo`로 취소(읽지 않는 클라이언트·FlushFileBuffers 대기 해제), 각 스레드가 자기 핸들만 닫음(S6), 이중 중지 안전 | `PipeRegistry.cs` | `T11PipeInstanceTests`: 생성 3회 실패 → 오류 기록·정지·연결 불가, 4 클라이언트 포화(오류 없음), 읽지 않는 연결 클라이언트가 있어도 5초 안에 중지·스레드 0 | **Red 확인**(오류 미기록, 중지 10초 지연·스레드 잔존) | PASS |

수정 중 발견·해결한 문제(구현): 다른 스레드의 `DisconnectNamedPipe`는 동기 핸들에서 진행 중 호출 뒤에 대기해 교착 → `CancelSynchronousIo`로 변경. 시험 기대 오류(수정): xUnit 시험별 인스턴스로 인한 생산 중복(세계 단위 공유로 변경), 다른 검증 실행 반례가 같은 승인 기록을 만들던 문제(freeze_id 변경), 컴파일러 생성 `Clone` 메서드 오탐.

## 2. 새로 생긴 구현 선택 (Codex·OWNER 확인 요청, 승인 규범 변경 없음)

| # | 선택 | 근거·한계 |
|---|---|---|
| C1 | 실행 파일 Archive 배치 `run/<run_id>/<결과 디렉터리 내 경로>`, 색인 역할 DIAGNOSTICS·manifest 역할 UNIT_MANIFEST | §21은 역할·개수만 고정하고 파일명은 B-4. 색인의 역할은 표에 직접 없음 |
| C2 | PZL6 "20경기 JSON bundle" = 표본별 GAME_RESULTS 파일 20개(기존 v2 Writer UTF-8 출력, `unit_id`·`game_id` 지정) | 새 형식을 만들지 않으려는 선택. 하나의 묶음 파일 형식이 필요하면 B-4 결정 |
| C3 | 영수증마다 `verifier/<receipt_id>/canonical-payload.json`·`raw-inventory.json`(VERIFIER_DERIVED) | §20.2 4·§21 "가능" 범위 |
| C4 | 공식 CLI 발급 명령 미추가 | "공식 인증 운영을 활성화하지 않는다" |
| **BLOCKED** | §21 `GAME_RESULTS` "Unit별 실행 경기 bundle 1"의 필수화 | 그 bundle 형식이 계약·B-4에 없음 → 새 규범 필요. 강제하지 않음 |
| 잔여 | 검증기 실행 바이너리 자기 SHA와 VERIFIER_BINARY 대조 없음, Test Profile은 SHA 확인 후 경로로 실행(그 사이 교체 창) | Codex 지적 범위 밖, 운영 배치(B-4)에서 다룰 사항으로 기록 |

## 3. 전체 시험 결과

**D3 복구 후 `tools/verify.ps1 -LogName B1_VERIFY_LOG.md`** — Run at 2026-10-25 06:26:54 +09:00, **`GATE: PASS`**, 로그 [B1_VERIFY_LOG.md](B1_VERIFY_LOG.md) SHA-256 `3c308f4da48c9cf097f22889301606f3e65de1d6273c6a68fab2260279ed97a8`

| 항목 | 결과 |
|---|---|
| B1 Release build (13 projects) | PASS, warnings 0 |
| U1 Domain | PASS 327 / FAIL 0 / SKIP 0 |
| U2 Simulation | PASS 741 / 0 / 0 |
| U3 Application (U3a 14, U3b 48은 부분 재실행) | PASS 128 / 0 / 0 |
| U4 Infrastructure | PASS 1,237 / 0 / 0 (Phase 루트: Certification 세션, 하위 `dotnet test` 4개 어셈블리 실제 실행, Family 영수증·Phase 자식 거부) |
| U5 SimHarness | PASS 530 / 0 / 0 (Golden 4개, Archive 핸들 경로·T11-04 경쟁 반례 포함) |
| U6 EvidenceVerifier | PASS 54 / 0 / 0 (T11-01~08 전부: 발급 경계, Phase 자식 결속, Archive·Replay 결속, 환경 프로필, Reader 클라이언트, RevertToSelf, Pipe 중지·자원 회수) |
| 합계 | **3,017 PASS / 0 FAIL / 0 SKIP** |
| G1~G4 Godot headless, E1~E4 Export·Cold run | 모두 PASS |
| D1 문서 링크(182개 파일) | PASS |
| D3 기준본 SHA | **PASS (5 files match)** |

- 이전 실행(05:47:47): D3만 FAIL(TASK.md), 시험 수치 동일. D3 외 결과는 복구 전후 같습니다.
- 수정 중 부분 실행 이력: EvidenceVerifier 1차 전체 60건 중 9 FAIL(모두 시험 측 오류) → 수정 후 54/54. Pipe 시험 1회 교착(타임아웃 중단) → 원인(동기 핸들의 `DisconnectNamedPipe` 대기) 수정.

## 4. 기존 불변성 증거

- 실행 코드(`GameExecutor`, `Official`, `Replay`, `FamilyOfficial`, `DominanceOfficial`)는 이번 수정에서 바꾸지 않았습니다(`git diff`의 이 파일들 내용은 1차 B-1 변경 그대로). `PathSafety.cs`는 `PinnedDirectory`에 새 진입점을 더했고 기존 `CreateNew` 동작·오류 코드는 같으며, 기존 공식 출력 시험(`DominanceOfficialTests`·`FamilyOfficialTests`·`PinnedDirectoryTests`)이 그대로 통과합니다.
- Hash·RNG: Golden 고정 입력·기대값 무변경, `GoldenTests` 4개 통과. Replay는 같은 바이트를 다른 위치에서 읽을 뿐 검사 순서·계산은 그대로.
- 문서 A+는 Artifact를 제외하므로(M(D)), 실행 파일·#6 JSON 참조 추가는 A+를 바꾸지 않습니다.

## 5. 미결 OWNER 해석 4건 — 권장 결정문과 현재 구현 영향 (임의 승인하지 않음)

> 2026-10-25: 이 4건과 GAME_RESULTS bundle·`run/` 배치·#6 JSON 역할을 합친 **단일 결정안**은 [B1_OWNER_DECISION_PROPOSALS_3.md](../contracts/b1/B1_OWNER_DECISION_PROPOSALS_3.md)입니다(이 절보다 우선).

| 항목 | 권장 결정문(제안) | 현재 구현 영향 |
|---|---|---|
| violations 생성 단위 | "Unit PZL `violations`는 경기별·위반 원인 카운터별 1건으로 만들고, §7의 `(game_id, event_id, field, code)` 정렬·중복 금지를 따른다." | 현재 그대로. 다른 단위를 택하면 Unit PZL 문서 바이트·A+·Golden이 바뀜 |
| RAW_INVENTORY 형식 | "`{format: \"simharness-raw-inventory-v1\", artifacts: [ArtifactRef…]}`, artifact_id ordinal 정렬, 자기·영수증·기록부·후속 Payload 제외, JCS 직렬화." | 현재 그대로이며 이번 수정으로 실제 영수증마다 VERIFIER_DERIVED로 생성·결속됨. 형식 ID를 바꾸면 영수증 source_files SHA가 바뀜 |
| witness_digest 앞 세 필드 | "difference·standard_error·lower_bound도 BITS64로 적는다(제출 F64 문자열이 아닌 비트 동일성이 목적인 E2-5와 일치)." | 현재 BITS64. 다른 표기를 택하면 `witness_digest` 값과 운영 Golden 승인값이 달라짐(아직 승인값 없음) |
| PZL #6 최종 상태 비교 | "#6 단계 4는 원 사건과 왕복 사건을 각각 기존 Reducer로 재생한 두 최종 상태의 GameState 전 필드 구조 비교(필드 인벤토리 누락 시 실패)로 하고, Production 실행과는 점수·이닝·Outcome·사건 수·Event Hash를 비교한다. F-26의 Simulation public 위임 메서드는 도입하지 않는다." | Codex 지적대로 **현재 보장 범위**는 "두 Reducer 재생 상태의 전 필드 동일 + 실행기 결과와는 위 요약 필드·Event Hash 동일"입니다. Production이 확정한 전체 GameState를 직접 대조하지 않습니다. 상태·공식 기록 필드만 달라지는 경우는 Reducer 재생 비교에서 잡히지만 실행기 내부 상태와의 직접 대조는 없음. F-26 방식을 택하면 Simulation 공개 표면 변경 필요 |

## 6. 운영 Gate (코드 결함 수정과 분리)

| Gate | 상태 |
|---|---|
| 보호 앵커 디렉터리 실제 설치·ACL 공격 실측 | 미검증 |
| AT-1~AT-14r(별도 Registry·OWNER·VERIFIER·Reader 계정) | 미실시 |
| AT-4 / P-1 정식 측정 | 미실시(사전 측정 Win32 1307은 PASS 아님) |
| N27-d·e·f 계정별 실측 | 미실시 |
| 운영 Environment Profile 승인 | 미완료 |
| N12 운영 비트 Golden 승인·동일 프로세스 검증 | 미완료 |
| 읽지 않는 클라이언트·FlushFileBuffers 대기 시 중지·자원 회수 | 자체 시험 PASS(한 계정, 실제 Pipe). 운영 계정 배치에서의 수용 시험은 미실시 |

## 7. Git 상태 (D3 복구 후)

- HEAD `257403ddae557a54c4013c17705370caeac7a2e0`(변경 없음), Commit·Push 하지 않음, 스테이징 0
- `git status --porcelain` 36개 항목(추적 수정 13 + 미추적 23 묶음), `--untracked-files=all` 기준 148개. 이번에 추가된 미추적: `docs/tasks/`(교체본 보관·README), `docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS_3.md`, `docs/reports/B1_T11_*.md`
- 보호 파일: 계약·보존본 `1338a2f7…7d64`, `TASK.md` `ecbc0859…f825`, PDF 2개 — 모두 기준 목록과 일치

## 8. Codex 재검수 대조 안내

- 기존 T11 Findings와 이 보고서 1절 표를 ID로 대조하십시오. 변경 Diff는 `git diff`(추적 파일)와 미추적 디렉터리(`tools/StarPlayer.EvidenceVerifier/`, `tools/StarPlayer.SimHarness/Evidence/`, `tests/StarPlayer.EvidenceVerifier.Tests/`)입니다.
- 이번 수정의 핵심 파일: `Certification.cs`(신규), `Verifier.cs`(VerifyFamily 재작성), `Receipts.cs`, `PhaseVerifier.cs`, `PipeRegistry.cs`, `Pzl6Verifier.cs`, `ExecutionEnvironment.cs`, `SimHarness/Evidence/{Archive,ApprovalRecord,EvidenceWriter}.cs`, `SimHarness/PathSafety.cs`.
- 새 시험: `T11FixTests.cs`(05·07·08·06), `T11CertificationTests.cs`(01·02·03), `ArchiveTests`(04), 갱신 `OfficialPzl6VerificationTests`, `TestOnlyWorld`(`Production`), `TestOnlyTrust`.
