# B1 — OWNER 결정 3(7건) 적용 보고서 (Codex 표적 재검수 제출용)

- 작성: Claude Code, 2026-10-25 · 근거: [OWNER 결정 기록 3](../contracts/b1/B1-T00_OWNER_DECISION_3.md)(수신 2026-10-25T08:04:02+09:00), [최종 결정안](../contracts/b1/B1_OWNER_DECISION_PROPOSALS_3_FINAL.md), 이전 수정 [B1_T11_FIX_REPORT.md](B1_T11_FIX_REPORT.md)
- 상태: **구현·자체 시험 완료, Codex READ-ONLY 표적 재검수 대기.** B1 완료 아님 · Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · Git Commit·Push 하지 않음
- 바꾸지 않은 것: 승인 계약·보존본(SHA `1338a2f7…7d64`), 승인 기록·NSC-4/5 결정·결정 2 원문, `TASK.md`(`ecbc0859…`), PDF 2개, GameResultHash·RunResultHash·ConfigHash·Attestation Hash 계산식, RNG 소비, Replay 순서

## 1. 결정 7건 적용 결과

| 결정 | 적용 | 수정 파일 | 시험 |
|---|---|---|---|
| 1 violations | 현행 유지(경기별·원인 카운터별, 4필드, 정렬·중복 금지, 재계산 검증) — 코드 변경 없음 | — | 기존 `UnitPzlEvidenceTests`·`EvidenceHashTests`·`OfficialDocumentTests`(Schema.order·재투영 비교) |
| 2 RAW_INVENTORY | 닫힌 Reader `Archive.ReadInventory`(정확히 `{artifacts, format}`, 형식 ID `simharness-raw-inventory-v1`, JCS 바이트, ArtifactRef 7필드, ordinal·중복 금지, 금지 역할). `Receipts.CheckInventory`: 영수증의 VERIFIER_DERIVED RAW_INVENTORY 정확히 1개를 Archive 스냅샷으로 읽어(SHA·길이) ORIGINAL source_files와 같음을 검사 — 발급 경계(`Certification.Issue`)와 Phase 자식 결속(`BindChildren`)에서 필수 실행 | `SimHarness/Evidence/Archive.cs`, `EvidenceVerifier/Receipts.cs`, `Certification.cs` | `Decision3EvidenceTests.Decision3_2_…`(추가·누락 멤버, 형식 ID, 정렬, 중복, 금지 역할, origin 멤버, 비JCS), `T11CertificationTests.Decision3_2_…`(실제 영수증 통과, 원본 누락·추가 → `Receipt.inventory_mismatch`, SHA 불일치 → `Archive.raw_mismatch`, 중복 → `Inventory.order`, 없음·ORIGINAL 표기 → `Receipt.inventory_missing`, Phase 경계에서 누락 인벤토리 자식 거부) |
| 3 witness_digest | 현행 유지(네 수치 모두 BITS64) — 코드 변경 없음. 운영 값은 승인 운영 환경에서 별도 생성 | — | 기존 `CertificationAndEnvironmentTests`(E2-5) |
| 4 #6 최종 상태 | T04a 관찰 Seam 읽기 전용 확장: `PzlEventObserver.FinalState`(경기 종료 시 실행기가 가진 GameState를 읽기만). `GameExecutor`에 null 조건 호출 2곳(중단·완료). `Pzl6Verifier.FinalDifference`: (i) 원·왕복 Reducer 재생 상태 전 필드, (ii) 왕복 재생 vs **Production 최종 GameState** 전 필드, (iii) 원·왕복 재생 Fact, (iv) 왕복 재생 Fact vs **Production 커밋 Fact**(기존 observe 경로), (v) 커버리지(GameState·FinalizedGame·EventReceipt 멤버, GameFact 형식 집합) — Production 상태가 없거나 누락 필드가 있으면 실패. 재생 Fact는 커밋 게이트와 같이 종료 시 `CompletedFact` 포함. F-26 public 메서드 미도입 | `SimHarness/Evidence/PzlEventObserver.cs`, `SimHarness/GameExecutor.cs`, `EvidenceVerifier/Pzl6Verifier.cs` | `Decision3EvidenceTests.Decision3_4_…`(관찰 유무 Hash 동일, 완료·중단·미시작 경기의 FinalState), `T11CertificationTests.Decision3_4_…`(실제 Production 경기로 통과, Stats·Final만 다른 Production 상태·Production 상태 없음·Fact 누락 → 각 경로로 실패, 실제 #6 PASS 유지), Dry Pilot 9 Run 재현 |
| 5 GAME_RESULTS | 신규 `simharness-game-results-v1`(`GameResultsEvidence`): Header 7멤버 / `{game: §3.6 GameResult, record: "game"}` 실행 경기 순서 / End 4멤버, 닫힌 Shape, §10.1 framing 공유(LF·CR·BOM·빈 행·마지막 LF·1 MiB, JCS 바이트). 생산자는 Unit마다 1개를 Unit PZL이 참조(R-13). 검증: Unit PZL당 정확히 1(game_id null, 자기 Unit), 전체 스냅샷에서 Unit별 1, Header 결속(`reference_invalid`), `game` = 부모 문서 행 = 재실행 투영, `result_hash` = Unit PZL `game_result_hash`, End 집계(`replay_mismatch`), 형식·개수(`archive_invalid`). Family/Dominance 문서의 직접 GAME_RESULTS 참조 금지(이중 집계 방지) | `SimHarness/Evidence/GameResultsEvidence.cs`(신규), `EvidenceDocuments.cs`(Shape), `UnitPzlEvidence.cs`(framing 공유로 추출 — GAME_EVENTS 동작·원인 코드 동일), `EvidenceWriter.cs`, `Archive.cs`, `EvidenceVerifier/Verifier.cs` | `Decision3EvidenceTests.Decision3_5_…` 3개(실패 경기 포함 4경기 보존·부모 행과 JCS 동일·결정적 바이트, framing·닫힌 Shape·순서·누락·건수·크기, 행·Hash·Header·End 변조), `T11CertificationTests.Decision3_5_…`(검증기 경로: 누락·중복 → `Archive.game_results_count`, SHA 변조 → `Archive.raw_mismatch`, 순서 교체 → `GameResults.order`, 내용 변조 → `GameResults.game_differs`), `ArchiveTests`(누락·2개·game_id·다른 Unit) |
| 6 출력 배치 | 현행 유지(`run/<run_id>/…` 내부 배치, B-4 동결과 별개) — 코드 변경 없음 | — | 기존 T11-03 시험 |
| 7 #6 JSON | 20개 JSON을 role **PZL6**, `game_id` = 경기 ID, ID `pzl6-json-<mode>-<index:D2>`로 정정. #6 문서는 PZL6·game_id null 1개. `Archive.AllowedRoles(Pzl6)`에서 GAME_RESULTS 제외·PZL6 허용, `CheckReferences(Pzl6)` 정확히 20개·ID 형식, 검증기는 표본 순서의 unit·game·index 일치와 원본 바이트·SHA·길이(Archive)·Writer 출력 동일, 전체 스냅샷에서 PZL6 = 문서 1 + JSON 20(Integration만), game_id 있는 GAME_RESULTS 0 | `Archive.cs`, `Verifier.cs`, 시험 생산자 | `T11CertificationTests.Decision3_7_…`(20개·ID·game_id, 옛 역할 GAME_RESULTS → `reverse_reference`, 19개·잘못된 ID → `pzl6_json_count`, 바이트 변경 → `pzl6_bundle_differs`, game_id 있는 Unit GAME_RESULTS → `game_results_count`, Family 문서의 GAME_RESULTS → `reverse_reference`) |

## 2. Codex T11 Findings 8건 수정 유지 증거

`T11FixTests`(05·06·07·08), `T11CertificationTests`(01·02·03), `ArchiveTests.T11_04_*`, `OfficialPzl6VerificationTests`(세션 경로 Phase 루트)가 이번 변경 후 전체 실행에서 그대로 통과(3절). 이번 수정으로 바뀐 기대값 2건(시험 쪽): T11-03의 UNIT_MANIFEST 누락은 이제 더 앞 단계인 §21 전체 개수 검사가 `Archive.required_count`로 거부(DIAGNOSTICS 누락은 그대로 `Archive.run_file_missing`, 둘 다 `archive_invalid`), T11-01 공개 API 단정은 "영수증 JObject를 받아 발급하는 공개 메서드 없음"으로 정밀화(새 `Receipts.CheckInventory`는 검사만 함).

## 3. 전체 시험과 verify.ps1

**`tools/verify.ps1 -LogName B1_VERIFY_LOG.md`** — Run at 2026-10-25 08:37:27 +09:00, **`GATE: PASS`**, 로그 [B1_VERIFY_LOG.md](B1_VERIFY_LOG.md) SHA-256 `725a250ee6f9d8b1700f2389d529e1aa32ea4052ac3e5b78f03f640b67d89244`

| 항목 | 결과 |
|---|---|
| B1 Release build (13 projects) | PASS, warnings 0 |
| U1 Domain | PASS 327 / FAIL 0 / SKIP 0 |
| U2 Simulation | PASS 741 / 0 / 0 |
| U3 Application (U3a 14, U3b 48은 부분 재실행) | PASS 128 / 0 / 0 |
| U4 Infrastructure | PASS 1,237 / 0 / 0 (Phase 루트 세션 경로 — GAME_RESULTS·#6 JSON 역할·Production 비교가 적용된 Integration 포함) |
| U5 SimHarness | PASS 535 / 0 / 0 (+5: `Decision3EvidenceTests`; `ArchiveTests` 갱신) |
| U6 EvidenceVerifier | PASS 58 / 0 / 0 (+4: 결정 2·4·5·7 검증기 시험) |
| 합계 | **3,026 PASS / 0 FAIL / 0 SKIP** |
| G1~G4 Godot headless, E1~E4 Export·Cold run | 모두 PASS |
| D1 문서 링크(186개 파일) | PASS |
| D3 기준본 SHA(계약·보존본·TASK.md·PDF 2) | PASS (5 files match) |

- 부분 실행 이력(정직 기록): SimHarness 1차 1 FAIL(`ArchiveTests`가 옛 규칙 "GAME_EVENTS만 있으면 유효"를 고정 — 결정 5로 바뀐 규칙에 맞게 갱신), 검증기 클래스 1차 2 FAIL(2절의 시험 기대 2건). 구현 결함은 없었습니다.

## 4. 기존 Hash·RNG·Replay 불변성

- **Dry Pilot 9 Run 재현**(관찰 Seam 변경 후 Release SimHarness DLL SHA-256 `13198f07ea87ea4e01d8004cc46812d22382fa62c1bc6d9221fa4692645a2d39`, 보관 Seed 그대로): `ALL_9_IDENTICAL=True` — 9개 모두 `config_hash`·`run_result_hash`·환경 외 Manifest 전체가 `build/verify-archive/PHASE_3F_FC2_PZL_DRY_PILOT_dry_run1/runs/*-a`와 동일(예: cal-na `669f8dc9…78b4ff`, dom-sa-ak `9a6d3f63…32966a`, stop-na `0ebd5c36…9bed05`).
- `GameExecutor` 변경은 `events?.Finished(state)` 두 곳뿐(관찰자 없으면 인자 평가도 없음), 상태·RNG를 읽기만 함. `ObserverSeamTests`(관찰 유무 결과 동일)·`Decision3_4` 관찰 시험 통과.
- Golden 고정 입력·기대값 무변경, `GoldenTests` 4개 통과. 새 Artifact(GAME_RESULTS, #6 JSON 역할)는 M(D)에서 제외되어 A+에 영향 없음.
- GAME_EVENTS framing은 공통 함수로 옮겼을 뿐 원인 코드·동작 동일(`UnitPzlEvidenceTests` 통과).

## 5. 시험 PASS와 TASK 수용 조건 (분리)

| 구분 | 상태 |
|---|---|
| 전체 Release 시험·verify.ps1 | 3절 결과 그대로 |
| 결정 1·3·6 | 수용 — 현행 유지 결정, 기존 시험이 근거 |
| 결정 2·4·5·7 | 수용(코드·자체 시험 기준) — 1절 시험 |
| Codex 재검수 | **미실시**(이 보고서 제출 후 READ-ONLY 표적 재검수) |
| OWNER 최종 수용 | **미실시** |

## 6. 미완료 운영 Gate (변동 없음)

보호 앵커 디렉터리 실제 설치·ACL 공격 실측(미검증), AT-1~AT-14r(미실시), AT-4/P-1 정식 측정(미실시, 1307 사전 측정은 PASS 아님), N27-d·e·f 계정별 실측(미실시), 운영 Environment Profile 승인(미완료), N12 운영 비트 Golden·witness_digest 승인값(미생성, 결정 3에 따라 운영 환경에서 별도 생성). 공식 대규모 실행 없음.

## 7. 변경 파일과 Git 상태

- HEAD `257403ddae557a54c4013c17705370caeac7a2e0`(변경 없음), Commit·Push 하지 않음, 스테이징 0
- `git status --porcelain` 36개 항목(추적 수정 13 — 이번 라운드에서 내용이 바뀐 추적 파일은 `tools/StarPlayer.SimHarness/GameExecutor.cs`(읽기 전용 호출 2곳)뿐, 미추적 23 묶음), `--untracked-files=all` 155개
- 이번 라운드 신규: `docs/contracts/b1/B1-T00_OWNER_DECISION_3.md`, `tools/StarPlayer.SimHarness/Evidence/GameResultsEvidence.cs`, `tests/StarPlayer.SimHarness.Tests/Evidence/Decision3EvidenceTests.cs`, `tests/StarPlayer.EvidenceVerifier.Tests/Decision3VerifierTests.cs`, 이 보고서
- 이번 라운드 수정(미추적 디렉터리 안): `SimHarness/Evidence/{Archive,EvidenceDocuments,EvidenceWriter,PzlEventObserver,UnitPzlEvidence}.cs`, `EvidenceVerifier/{Verifier,Receipts,Certification,Pzl6Verifier}.cs`, 시험 `TestOnlyWorld.cs`·`T11CertificationTests.cs`·`ArchiveTests.cs`
- 보호 파일: 계약·보존본 `1338a2f7…7d64`, `TASK.md` `ecbc0859…f825`, PDF 2개 — 기준 목록과 일치(D3 PASS)

## 8. Codex 표적 재검수 범위

1. T11-01~08 수정 유지(2절)
2. 결정 2: `Archive.ReadInventory`, `Receipts.CheckInventory`와 두 호출 위치
3. 결정 4: `PzlEventObserver.FinalState`/`Finished`, `GameExecutor` 두 호출의 읽기 전용성, `Pzl6Verifier.FinalDifference`·`CoverageGap`·`Replay` Fact 수집
4. 결정 5: `GameResultsEvidence`(형식·Compare), `EvidenceDocuments` Shape, `Archive.CheckReferences(UnitPzl)`, `Verifier.RequiredCounts`·GAME_RESULTS 비교 위치·오류 분류
5. 결정 7: `Archive.AllowedRoles`·`CheckReferences(Pzl6)`, `Verifier` #6 문서 선택·`VerifyPzl6`
6. 불변성: Dry Pilot 재현 결과, Golden, Observer 시험
