# B1 잔여 OWNER 결정안 3 — 7개 항목 (제안 — 미채택, 적용하지 않음)

> 2026-10-25: OWNER 방향에 맞춘 최종 정합성 정리는 [B1_OWNER_DECISION_PROPOSALS_3_FINAL.md](B1_OWNER_DECISION_PROPOSALS_3_FINAL.md)이며 그 문서가 우선합니다(이 초안은 이력으로 보존).

- 작성: Claude Code, 2026-10-25. 승인 계약 `B1-T00_FINAL_CONTRACT_DRAFT.md`(SHA `1338a2f7…7d64`), [승인 기록](B1-T00_OWNER_APPROVAL_RECORD.md), [NSC-4·NSC-5 결정](B1-T00_NSC4_NSC5_OWNER_DECISION.md), [결정 2](B1-T00_OWNER_DECISION_2.md)는 바꾸지 않았습니다. 아래 결정 문장은 OWNER가 채택해야 효력이 있습니다.
- 분류: **해석** = 승인 계약 문장 안에서 이미 허용된 선택지 중 하나를 확정(새 의무·새 형식 없음) · **새 규범** = 계약에 없는 형식·의무를 추가(OWNER 승인 없이 확정 불가)
- 현재 상태 근거: [T11 수정 보고서](../../reports/B1_T11_FIX_REPORT.md), 코드는 HEAD `257403d` + 작업 트리

| # | 항목 | 분류 | 현재 구현이 이미 그 방식인가 |
|---|---|---|---|
| 1 | violations 생성 단위 | 해석 | 예 |
| 2 | RAW_INVENTORY 파일 형식·형식 ID | **새 규범(최소)** — 형식 ID·직렬화 | 예 |
| 3 | witness_digest 수치 표현 | 해석 | 예 |
| 4 | PZL #6 최종 상태 비교 | 해석 + F-26 권고와의 차이 확정 | 예 |
| 5 | §21 Unit별 GAME_RESULTS bundle | **새 규범** — 형식 | **아니오(BLOCKED, 미구현)** |
| 6 | `run/<run_id>/…` 출력 배치 | 해석(B-4 파일명 영역) | 예 |
| 7 | #6의 20경기 JSON을 GAME_RESULTS 역할로 | 해석(역할 배정) — 대안은 새 규범 | 예 |

---

## 1. violations 생성 단위 (해석)

- **현재 구현** (`tools/StarPlayer.SimHarness/Evidence/UnitPzlEvidence.cs` `Checks`): 실행된 경기마다, `PzlChecks.Evaluate`가 세는 원인 카운터가 0이 아닌 것 하나당 1건. pzl-5: `version_mismatches`·`fingerprint_mismatches`·`foreign_event_ids`·`duplicate_event_ids`·`event_id_collisions`·`receipt_log_mismatches`(code = 카운터 이름, `game_id`), 같은 GameId 중복은 `duplicate_game_ids`(game_id = 그 ID). pzl-10: Automated 경기의 `framing_non_neutral`. pzl-12a: 등록되지 않은 Stream마다 `drawn_unregistered`(`field` = Stream 이름). pzl-12b: 없음. #6: 표본 × 거짓인 검사 플래그마다 `pzl6_mismatch`(`field` = 플래그). Phase: FAIL 시험 Case마다 `case_failed`(`field` = CASE_ID).
- **계약 요구** (§7): `Violation = {code: TEXT1, game_id: TEXT1?, event_id: TEXT1?, field: TEXT1?}`, `(game_id, event_id, field, code)` ordinal 정렬, null 먼저, 동일 조합 중복 거부. 생성 단위는 정하지 않음.
- **최소 결정 문장:** "Violation은 (경기, 위반 원인) 한 쌍마다 1건 만든다. 원인은 그 검사가 세는 카운터 이름(Unit PZL), 거짓인 표본 검사 플래그(#6), 실패한 시험 Case(Phase)이며, 원인을 더 잘게(사건별) 나누지 않는다. `event_id`는 v1에서 쓰지 않는다(null)."
- **변경 대상:** 채택 시 없음(현행). 다른 단위를 택하면 `UnitPzlEvidence.Checks`, `Pzl6Verifier.Document`, `PhaseVerifier.Checks`.
- **Hash·Golden·Replay 영향:** 현행 채택 시 없음. 다른 단위는 Unit PZL·#6·Phase 문서 바이트 → A+ → `tests/Fixtures/b1-golden` 재생성 필요. Replay 무관.
- **수용 시험:** (a) 위반 카운터 2종이 있는 경기 1개 → violations 2건·정렬, (b) 같은 (경기, 원인) 중복 → 스키마 거부(기존 규칙), (c) 사건 수와 무관하게 건수 불변.

## 2. RAW_INVENTORY 파일 형식·형식 ID (새 규범 — 최소)

- **현재 구현** (`Evidence/Archive.cs` `Inventory`, T11-01 이후 영수증마다 `verifier/<receipt_id>/raw-inventory.json`, VERIFIER_DERIVED): `JCS({format: "simharness-raw-inventory-v1", artifacts: [ArtifactRef…]})`, artifact_id ordinal 정렬·중복 거부, RAW_INVENTORY·VERIFICATION_RECEIPT·REGISTRY_RECORD·CANONICAL_PAYLOAD 역할 거부.
- **계약 요구** (§21): 역할 `RAW_INVENTORY`, "검증 스냅샷별 1, 원본 고정 후, 자기 자신 제외", 자기 참조 제외 규칙, 작성 순서(… → RAW_INVENTORY → CANONICAL_PAYLOAD → 영수증). 파일 형식·형식 ID는 없음.
- **최소 결정 문장:** "RAW_INVENTORY는 JCS 직렬화한 `{format: \"simharness-raw-inventory-v1\", artifacts: ArtifactRef[]}` 하나이며, `artifacts`는 그 검증 스냅샷의 ORIGINAL 원본 전부를 artifact_id ordinal 순서로 담고 자신·영수증·기록부 항목·Canonical Payload를 담지 않는다. 영수증 `source_files`에 VERIFIER_DERIVED로 결속한다."
- **변경 대상:** 채택 시 없음.
- **Hash·Golden·Replay 영향:** A+ 무관(Artifact). 형식 ID·멤버를 바꾸면 RAW_INVENTORY 파일 SHA → 영수증 바이트·영수증 SHA가 바뀜. Golden·Replay 무관.
- **수용 시험:** (a) 형식 ID·멤버 정확 일치, (b) 금지 역할 포함 시 거부(기존 `ArchiveTests`), (c) 영수증의 RAW_INVENTORY 파일 내용 = 그 영수증 ORIGINAL source_files(origin 제외) 집합.

## 3. witness_digest 수치 표현 (해석)

- **현재 구현** (`tools/StarPlayer.EvidenceVerifier/ExecutionEnvironment.cs` `WitnessDigest`): 240개 Witness의 `difference`·`standard_error`·`lower_bound`·`non_superiority_z`를 모두 BITS64(IEEE754 binary64 빅엔디언 16진), `holm_critical`은 BITS64 또는 null.
- **계약 요구** (§18.2): `witness_digest = sha256(ASCII("STARPLAYER-ENV-WITNESS-V1") || 0x0A || JCS([{side, strategy, rival, column, difference, standard_error, lower_bound, non_superiority_z: BITS64, holm_critical: BITS64|null, holm_rejected}]))`. `: BITS64` 표기가 앞 세 필드에는 붙어 있지 않음. E2-5의 목적은 비트 재현(같은 프로세스 비트 비교).
- **최소 결정 문장:** "witness_digest의 `difference`·`standard_error`·`lower_bound`도 BITS64로 적는다(§18.2 식의 `: BITS64`는 수치 필드 전체에 적용)."
- **변경 대상:** 채택 시 없음. F64 문자열을 택하면 `WitnessDigest`.
- **Hash·Golden·Replay 영향:** 운영 Environment Profile의 `witness_digest` 승인값이 이 결정에 따라 정해짐(아직 승인값 없음, N12). 증거 문서·A+·Golden·Replay 무관.
- **수용 시험:** (a) Witness 240개·생성 순서, (b) 한 필드의 최하위 비트만 다른 값 → digest 다름, (c) 같은 프로세스 두 번 계산 동일.

## 4. PZL #6 최종 상태 비교 (해석 + F-26 차이 확정)

- **현재 구현** (`Pzl6Verifier.Check`): 원 사건 목록과 v2 왕복 사건 목록을 **각각 기존 Reducer로 재생**한 두 최종 `GameState`를 필드 단위로 구조 비교(`FirstDifference`, 반사), `GameState` 필드 인벤토리(`CoverageGap`)가 비교 대상과 다르면 실패. 그 상태와 실제 `GameExecutor` 실행 결과는 **점수·이닝·초말·Outcome·적용 사건 수**와 Event Hash로 대조. Production이 확정한 전체 GameState를 직접 대조하지는 않음(Codex 지적).
- **계약 요구** (§6): "4. 기존 Reducer로 재생해 최종 상태·기록·Receipt를 비교합니다. … 최종 상태는 경기 상태와 공식 기록 전체를 구조적으로 비교합니다. PreviewComparison을 재사용해도 되지만, GameState·Finalized Facts의 필드가 비교에서 빠지면 인증을 거부하는 커버리지 검사가 필요합니다." 추적표 F-26: "T05: public 위임 비교 메서드(Codex 리뷰) — 구현 TASK(INFO-1)".
- **최소 결정 문장:** "#6 단계 4의 최종 상태 비교는 원 사건과 왕복 사건을 각각 기존 Reducer로 재생한 두 GameState의 전 필드 구조 비교로 하며, 필드 인벤토리에서 하나라도 빠지면 실패한다. Production 실행과의 대조는 점수·이닝·초말·Outcome·사건 수·Event Hash·GameResultHash·RNG Hash로 한다. F-26의 Simulation public 위임 메서드는 v1에 도입하지 않는다."
- **변경 대상:** 채택 시 없음(시험 보강만). F-26 방식을 택하면 `src/StarPlayer.Simulation`에 공개 비교 메서드 + 경계 시험 + `Pzl6Verifier`.
- **Hash·Golden·Replay 영향:** 없음(검사 방식만).
- **수용 시험:** (a) 두 재생 상태 중 요약 필드가 아닌 필드 하나만 다르게 만든 상태 → `FirstDifference` 검출, (b) `GameState`에 새 필드 추가 시 `CoverageGap` 실패, (c) 실행기 결과와 점수 등 요약 필드 불일치 → `final_state_equal=false`. (a)·(c)의 직접 시험은 채택 시 추가.

## 5. §21 Unit별 GAME_RESULTS bundle 형식 (새 규범 — 현재 BLOCKED)

- **현재 구현:** 없음. Unit별 경기 결과는 Family/Dominance 문서의 `units[].technical.games[]`(§3.6 GameResult)와 UNIT_MANIFEST 안의 8필드 요약(`results.games`: game_id·seed·status·end_kind·event_hash·rng_hash·result_hash·error_code)에만 있음. `GAME_RESULTS` 역할 파일은 Unit별로 강제하지 않음.
- **계약 요구** (§21): `GAME_RESULTS | Unit별 실행 경기 bundle 1 | Unit 종료 | Raw SHA 필수 | 대용량은 Archive`. 형식은 없음(GAME_EVENTS는 §10이 정의, GAME_RESULTS는 정의 없음).
- **제안 형식 `simharness-game-results-v1`** (역할·개수·증거 범위를 §21 그대로 지키도록, GAME_EVENTS §10과 같은 직렬화 규칙):
  - 파일: UTF-8, BOM 없음, 각 행은 JCS 한 줄 + LF, 마지막 LF 필수, 빈 행·CR 금지.
  - 1행 Header: `{bundle: "GAME_RESULTS", format: "simharness-game-results-v1", contracts: 5-tuple, validation_set_id, family, run_id, unit_id, parent_evidence_id, config_hash, run_result_hash}` — `parent_evidence_id` = 참조하는 Family/Dominance 문서의 `execution.evidence_id`.
  - 2…n+1행 Game: `{record: "GAME", game: GameResult}` — `GameResult`는 §3.6 객체 **그대로**(부모 문서 `units[i].technical.games[k]`와 JCS 바이트 동일), Unit의 **실행된 모든 경기**(실패 경기 포함, 정지 후 미실행 제외)를 부모 실행 순서로.
  - 마지막 행 End: `{record: "END", executed_games, completed_games, failed_games}`.
  - ArtifactRef: role `GAME_RESULTS`, `unit_id` non-null, `game_id` null, Family/Dominance 문서가 Unit마다 정확히 1개 참조.
  - 검증: Header 결속(계약·집합·Family·run·unit·부모 evidence_id·config/run hash) → 각 Game 행 = 검증기 재실행의 `OfficialDocuments.Game` 투영과 바이트 동일, = 부모 문서 행과 동일 → 건수 = 실행 경기 수, End 집계 일치. 제안 오류 분류: 파일 형식 위반·Unit별 파일 누락은 `archive_invalid`, 내용이 재실행 투영과 다르면 `replay_mismatch`(결정 시 확정).
- **대안(해석, 새 형식 없음):** "§21 GAME_RESULTS는 UNIT_MANIFEST 안 `results.games` 8필드 요약으로 충족된다(ATTESTATION처럼 Manifest 내부 허용)." — 표에 Manifest 내부 허용 문구가 GAME_RESULTS에는 없어 계약 문장과 거리가 있음.
- **최소 결정 문장(제안 형식 채택 시):** "§21 GAME_RESULTS는 Unit별 1개의 `simharness-game-results-v1` 파일로 하며, 그 형식은 B1 OWNER 결정 3 §5의 정의를 따른다."
- **변경 대상:** `tools/StarPlayer.SimHarness/Evidence/`(Writer·Reader·Shape), `EvidenceWriter`(생산), `Verifier.VerifyFamily`(필수 개수·내용 대조), 시험 생산자, 수용 시험, Traceability.
- **Hash·Golden·Replay 영향:** A+ 무관(Artifact, M(D)에서 제외). 영수증 source_files에 추가(영수증 바이트 변화). Golden 고정 입력·기대 A+ 변화 없음. Replay 무관(검증기 재실행 투영과 대조만).
- **수용 시험:** (a) Unit별 1개·실패 경기 포함 건수, (b) 한 경기 행 변조 → 거부, (c) 경기 누락·순서 변경 → 거부, (d) Header의 다른 run·unit·부모 ID → 거부, (e) CR·BOM·마지막 LF 없음 → 거부, (f) Unit 누락 → 거부.

## 6. `run/<run_id>/…` 출력 배치 (해석 — B-4 파일명 영역)

- **현재 구현** (T11-03, `EvidenceWriter.WriteRun`·`Verifier.RunFile`): 실행 결과 디렉터리의 DRAFT 색인(`family-result.json`·`dominance-result.json`)을 `run/<run_id>/<색인 파일>`, Unit manifest를 `run/<run_id>/<units|cells>/<id>/manifest.json`으로 Archive에 복사해 부모 문서가 참조(색인 role `DIAGNOSTICS` `unit_id` null, manifest role `UNIT_MANIFEST` `unit_id` 지정). 검증기는 이 바이트를 새 전용 디렉터리에 펼쳐 기존 Replay를 실행하고 Attestation도 같은 바이트로 파싱. 정확히 1개씩이 아니면 `Archive.run_file_missing`.
- **계약 요구** (§21): "파일명과 실제 출력 루트는 B-4에서 정합니다. 여기서는 역할과 개수만 고정합니다." UNIT_MANIFEST "Unit별 1", ATTESTATION "Unit별 1(Manifest 내부 허용)", §20.2 1 "Hash·파싱·Replay가 같은 바이트를 씁니다", 재열기 금지.
- **최소 결정 문장:** "B-4 전까지 실행 결과 파일은 Archive의 `run/<run_id>/` 아래에 결과 디렉터리 상대 경로 그대로 둔다. DRAFT 색인은 DIAGNOSTICS(unit_id null) 1개, Unit manifest는 UNIT_MANIFEST(unit_id) Unit별 1개이며 ATTESTATION은 그 Manifest 내부로 충족한다."
- **변경 대상:** 채택 시 없음. B-4가 다른 파일명을 정하면 `EvidenceWriter.RunPath`·`Verifier.RunFile`의 경로 규칙.
- **Hash·Golden·Replay 영향:** A+ 무관. 영수증 source_files 경로. Replay 계산·순서 무관(입력 위치만 다름). Golden 무관.
- **수용 시험:** 기존 `T11CertificationTests.T11_03_the_replay_reads_only_the_archived_run_files…`(결과 디렉터리 변경 후에도 Archive 바이트로 검증, 색인·manifest 누락 거부) + 색인 역할을 다른 역할로 바꾸면 거부.

## 7. #6의 20경기 JSON을 GAME_RESULTS 역할로 (해석 — 대안은 새 규범)

- **현재 구현** (T11-03, `Pzl6Verifier.Bundle`, 시험 생산자): #6 문서가 고정 표본마다 GAME_RESULTS 파일 1개(기존 v2 Writer UTF-8 출력, `unit_id` = automated/non-automated, `game_id` = 경기 ID) 20개를 표본 순서로 참조. 검증기는 자기 Writer 출력과 바이트 대조(`Evidence.pzl6_bundle_missing`·`pzl6_bundle_differs`).
- **계약 요구** (§21): `PZL6 | Integration 1 + 20경기 JSON bundle | 고정 표본 검사 후 | 필수`. §6 "Raw JSON SHA는 ArtifactRef와 영수증에만 둡니다." PZL6 문서의 허용 참조 역할은 UNIT_MANIFEST·GAME_RESULTS·DIAGNOSTICS·REGRESSION_PROOF(현 구현 `Archive.AllowedRoles`).
- **긴장:** 표는 20경기 JSON을 PZL6 행에 함께 적어 "역할 PZL6의 두 번째 산출물"로도 읽힘. 또 항목 5를 채택하면 GAME_RESULTS가 "Unit별 bundle(game_id null, Family/Dominance 문서 참조)"과 "#6 표본 JSON(game_id non-null, #6 문서 참조)" 두 뜻을 가짐.
- **최소 결정 문장(현행 채택):** "#6의 20경기 JSON은 표본마다 GAME_RESULTS 1개(기존 v2 Writer 출력 바이트, `game_id` non-null)로 #6 문서가 참조한다. Unit별 GAME_RESULTS bundle(`game_id` null, Family/Dominance 문서 참조)과는 참조 문서와 `game_id` 유무로 구별한다."
- **대안(새 규범):** 20경기 JSON을 하나의 `simharness-pzl6-json-bundle-v1` 파일(행마다 `{game_id, json}`)로 묶어 role PZL6으로 Family 문서가 참조(PZL6 개수 = 문서 1 + bundle 1).
- **변경 대상:** 현행 채택 시 없음. 대안 채택 시 새 형식 Reader/Writer, `Archive.CheckReferences`(PZL6 개수), `Verifier.VerifyPzl6`, 생산자.
- **Hash·Golden·Replay 영향:** A+ 무관. 영수증 source_files. Golden·Replay 무관.
- **수용 시험:** 기존 `T11_03_…_the_pzl6_keeps_its_twenty_json_files`, `Pzl6StandaloneTests`(손상 Writer·Reader) + 한 JSON 누락·바이트 변경·순서 교체 → 거부.

---

## 채택 후 순서 (제안)

1. OWNER가 1~7 각각 채택·수정·거부를 결정 기록(새 파일)으로 남김
2. 5(채택 시)의 구현과 시험, 4의 보강 시험, 각 항목 수용 시험
3. 전체 회귀 + `verify.ps1` → Codex 표적 재검수(T11-01~08 + 이 결정 범위)
