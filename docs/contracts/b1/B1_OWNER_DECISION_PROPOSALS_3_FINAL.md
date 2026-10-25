# B1 OWNER 결정안 3 — 최종 정합성 정리 (7건, 승인 권장안 · 미채택)

- 작성: Claude Code, 2026-10-25. 앞선 초안 [B1_OWNER_DECISION_PROPOSALS_3.md](B1_OWNER_DECISION_PROPOSALS_3.md)를 OWNER 방향(2026-10-25 "OWNER 결정안 7건 최종 정합성 확인")에 맞춰 계약 원문·현재 코드와 다시 대조한 결과입니다. 이 문서는 **결정안**이며 신규 규범 확정이나 GAME_RESULTS 구현 착수 허가가 아닙니다.
- 기준: 승인 계약 `B1-T00_FINAL_CONTRACT_DRAFT.md`(SHA `1338a2f7…7d64`), [승인 기록](B1-T00_OWNER_APPROVAL_RECORD.md), [NSC-4·NSC-5 결정](B1-T00_NSC4_NSC5_OWNER_DECISION.md), [결정 2](B1-T00_OWNER_DECISION_2.md), [T11 수정 보고서](../../reports/B1_T11_FIX_REPORT.md). 원본은 바꾸지 않았습니다.
- 원칙: 승인 계약의 규범을 약화하지 않고, 새로운 Hash·RNG·Replay 계산식을 도입하지 않습니다. **해석** = 계약 문장 안의 선택지 확정 · **새 규범** = 계약에 없는 형식·의무 추가.

## 요약

| # | 항목 | 계약 충돌 | 분류 | 현재 구현 유지 | 구현 보완 | 새 규범 |
|---|---|---|---|---|---|---|
| 1 | violations 생성 단위 | 없음 | 해석 | **가능** | 없음 | 없음 |
| 2 | RAW_INVENTORY 형식·형식 ID | 없음 | 최소 새 규범(형식 ID·멤버) | **가능** | 소규모(닫힌 Reader·대조 시험) | 형식 ID `simharness-raw-inventory-v1` |
| 3 | witness_digest 수치 표현 | 없음 | 해석 | **가능** | 없음 | 없음 |
| 4 | PZL #6 최종 상태 비교 | **부분 미충족**(Production 최종 상태·기록과의 직접 비교 없음) | 해석 | 유지 + 보완 | **필요**(관찰 Seam 읽기 1곳 + 비교 2개) | 없음 |
| 5 | §21 Unit별 GAME_RESULTS bundle | 없음(현재 미구현·BLOCKED) | **새 규범**(형식) | 해당 없음 | 채택 시 구현 | 형식 `simharness-game-results-v1` |
| 6 | `run/<run_id>/…` 배치 | 없음(B-4 파일명 영역) | 해석(임시 내부 배치) | **가능** | 없음 | 없음(B-4 공식 경로 동결과 별개) |
| 7 | #6 20경기 JSON의 역할 | **있음**(§21 PZL6 행·5번 집계와 충돌) | 해석 | 불가 | **필요**(역할 GAME_RESULTS → PZL6) | 없음(단일 묶음 파일을 원하면 새 규범) |

---

## 1. violations 생성 단위 — 현재 구현 유지

- **계약** §7: `Violation = {code: TEXT1, game_id: TEXT1?, event_id: TEXT1?, field: TEXT1?}`, `(game_id, event_id, field, code)` ordinal 순서·null 먼저·동일 조합 중복 거부. 생성 단위는 없음.
- **현재 구현·준수 확인:**
  - 생성(`UnitPzlEvidence.Checks`): (경기, 원인 카운터)마다 1건 — pzl-5 6개 카운터·`duplicate_game_ids`, pzl-10 `framing_non_neutral`(Automated), pzl-12a `drawn_unregistered`(field = Stream). #6: (표본, 거짓 플래그)마다 `pzl6_mismatch`. Phase: FAIL Case마다 `case_failed`(field = CASE_ID). `event_id`는 쓰지 않음(null).
  - 정렬: `UnitPzlEvidence.Sorted`가 (G, E, F, C) ordinal·null 먼저로 정렬(생산 측 동일 항목은 하나로).
  - 중복·순서 거부: 읽기 측 `EvidenceRules.Violations`가 Unit PZL(checks), #6(check), Phase(checks, `EvidenceHash.PhaseChecks`)에서 위반 시 `Schema.order`.
  - 증거 결속: Unit PZL은 재관찰 재투영과 M(D) 비교(`Evidence.unit_pzl_differs`), #6은 검증기 재계산과 비교(`pzl6_differs`), Phase는 검증기가 직접 생성 → 제출 violations가 재계산과 다르면 거부. violations는 A+ 대상(Artifact 아님).
- **승인 권장 문장:** "Violation은 (경기, 위반 원인) 한 쌍마다 1건 만든다. 원인은 Unit PZL에서는 그 Check가 세는 카운터 이름, #6에서는 거짓인 표본 검사 플래그, Phase에서는 실패한 시험 Case이다. v1에서 `event_id`는 null이다. 정렬·중복 금지는 §7 그대로이며, 제출된 violations는 검증기 재계산과 같아야 한다."
- **영향:** 현행이므로 Hash·Golden·Replay 변화 없음.
- **Codex 최소 시험:** (a) 한 경기에 카운터 2종 → 2건·정렬, (b) 순서 뒤바꿈·중복 → `Schema.order`(3개 문서 종류), (c) violations만 바꾼 Unit PZL → `unit_pzl_differs`.

## 2. RAW_INVENTORY — 현재 구현 유지 + 형식 명시

- **계약** §21: 역할 RAW_INVENTORY "검증 스냅샷별 1, 원본 고정 후, 자기 자신 제외", 자신·영수증·후속 기록부 항목·나중에 생성된 Canonical Payload 제외, 작성 순서 … → RAW_INVENTORY → CANONICAL_PAYLOAD → 영수증. 영수증 `source_files`에 VERIFIER_DERIVED로 넣을 수 있음. 형식은 없음.
- **현재 구현:** `Archive.Inventory` → `JCS({format, artifacts})`, `Certification.Issue`가 영수증마다 `verifier/<receipt_id>/raw-inventory.json`(VERIFIER_DERIVED)으로 쓰고 결속. 쓰기 측만 있고 **닫힌 Reader가 없음**.
- **정확한 형식(권장):**
  - 파일: UTF-8, BOM 없음, 단일 JCS 객체, 끝 개행 없음(JCS 바이트 그대로).
  - `{ "artifacts": ArtifactRef[], "format": "simharness-raw-inventory-v1" }` — 정확히 이 두 멤버.
  - `artifacts`: §2.5 ArtifactRef 7필드 그대로(origin 없음), artifact_id ordinal 오름차순·중복 금지, 그 검증 스냅샷의 **ORIGINAL 원본 전부**(문서 자신 포함), 역할 RAW_INVENTORY·VERIFICATION_RECEIPT·REGISTRY_RECORD·CANONICAL_PAYLOAD 금지.
  - 같은 영수증의 ORIGINAL `source_files`에서 `origin`을 뺀 집합과 정확히 같다.
- **승인 권장 문장:** "RAW_INVENTORY는 `simharness-raw-inventory-v1` 형식의 JCS 객체 `{artifacts, format}` 하나이며, `artifacts`는 그 검증 스냅샷의 ORIGINAL ArtifactRef 전부를 artifact_id ordinal 순서로 담고 자신·영수증·기록부 항목·Canonical Payload를 담지 않는다. 영수증마다 1개를 VERIFIER_DERIVED로 결속하고, 그 내용은 영수증의 ORIGINAL source_files와 같아야 한다."
- **구현 보완(소규모, 규범 추가 없음):** 닫힌 Shape와 읽기 함수, 영수증 대조 시험.
- **영향:** A+ 무관(Artifact). 형식 ID·멤버를 바꾸면 RAW_INVENTORY SHA → 영수증 바이트가 바뀜. Golden·Replay 무관.
- **Codex 최소 시험:** (a) 형식 ID·멤버 정확(추가·누락 멤버 거부), (b) 금지 역할·자기 참조 거부, (c) 정렬·중복 거부, (d) 발급된 영수증의 RAW_INVENTORY = ORIGINAL source_files.

## 3. witness_digest — BITS64 채택

- **계약** §18.2 식: `{side, strategy, rival, column, difference, standard_error, lower_bound, non_superiority_z: BITS64, holm_critical: BITS64|null, holm_rejected}`. E2-5는 같은 프로세스 비트 비교. `: BITS64` 표기가 앞 세 필드에는 없음.
- **현재 구현** (`ExecutionEnvironment.WitnessDigest`): 네 수치 모두 BITS64, holm_critical BITS64|null, Witness 240개 생성 순서, 개수 검사.
- **승인 권장 문장:** "§18.2 witness_digest의 `difference`·`standard_error`·`lower_bound`도 BITS64(IEEE754 binary64 빅엔디언 16진 16자)로 적는다. 다른 멤버·순서·접두사·개수(240)는 §18.2 그대로이다."
- **기존 Hash·운영 Golden 영향:** 증거 문서·A+·GameResult/RunResult/Config/Attestation Hash·Replay 무관. 영향은 Environment Profile의 `math_golden.witness_digest` 값 하나: 운영 승인값은 **아직 없음**(N12 미생성)이므로 이 결정 뒤에 그 값을 승인해야 함. TEST_ONLY 프로필은 이 구현으로 계산한 값을 이미 사용.
- **Codex 최소 시험:** (a) 240개·생성 순서, (b) 세 필드 중 하나의 최하위 비트만 바꾼 Witness → digest 다름, (c) 같은 프로세스 2회 동일, (d) 프로필 값 불일치 → `environment_mismatch`(E2-5, 기존 시험).

## 4. PZL #6 최종 상태 비교 — 범위 구분과 최소 보완

- **계약** §6 4단계: "기존 Reducer로 재생해 최종 상태·기록·Receipt를 비교합니다." 추가 규칙: "최종 상태는 경기 상태와 공식 기록 전체를 구조적으로 비교합니다. PreviewComparison을 재사용해도 되지만, GameState·Finalized Facts의 필드가 비교에서 빠지면 인증을 거부하는 커버리지 검사가 필요합니다." 5·6단계: Production 재실행, Event·RNG·GameResultHash·PZL 독립 비교. F-26(구현 TASK): Simulation public 위임 비교 메서드.
- **현재 구현의 정확한 보장 범위** (`Pzl6Verifier.Check`):
  1. 원 사건 목록 재생 상태 vs v2 왕복 사건 재생 상태: `GameState` **전 필드** 구조 비교 — `Final`(`FinalizedGame`, 확정 공식 기록), `Plays`, `Stats`, `Substitutions`, `AppliedEvents`(Receipt), 라인·주자·카운트 포함. 필드 인벤토리(`CoverageGap`) 누락 시 실패.
  2. 왕복 재생 상태 vs **Production 실행**: 점수·이닝·초말·Outcome·적용 사건 수, Event Hash(재생 Receipt로 재계산 = Production EventHash = 문서).
  3. Production 2회 실행·문서: GameResultHash·EventHash·RngHash·Status 일치(`seed_replay_match`).
- **충돌 판단:** 1은 §6의 "경기 상태와 공식 기록 전체의 구조 비교"와 커버리지 검사를 충족합니다. 그러나 비교 상대가 **원 사건의 Reducer 재생**이고, **Production이 확정한 최종 GameState**와는 요약 필드·Hash만 대조합니다. Commit마다 확정된 `GameFact`(공식 기록 사실, `CommitRecord.Facts`)도 대조하지 않습니다. "Production이 확정한 전체 상태를 직접 대조한다"고는 말할 수 없으므로 **보완이 필요**합니다. 새 규범은 아닙니다(§6 4단계의 해석과 구현 보완).
- **최소 보완안(채택 시 구현):**
  - (a) Production 최종 상태: B-1 T04a 관찰 Seam(`PzlEventObserver`)에 게임 종료 시 `cursor.Current.State`를 **읽기만** 하는 호출 1곳을 추가합니다(`Started`·`Committed`와 같은 null 조건 호출). 기존 결과·RNG·Hash에 영향이 없음을 `ObserverSeamTests`와 Dry Pilot 재현으로 재확인합니다. #6에서 `FirstDifference(Production 최종 GameState, 왕복 재생 상태)`가 null이어야 `final_state_equal = true`.
  - (b) 공식 기록 사실: 기존 `observe` 콜백으로 Production Commit의 `Facts`를 모으고, 왕복 사건 재생의 Reducer Step Facts와 순서·내용 동일성을 비교합니다(새 Seam 없음).
  - (c) F-26: v1에 Simulation public 위임 메서드를 도입하지 않습니다(Simulation 공개 표면 무변경).
- **승인 권장 문장:** "#6 4단계의 '최종 상태·기록 비교'는 (i) 원 사건과 v2 왕복 사건의 Reducer 재생 GameState 전 필드 비교, (ii) 왕복 재생 GameState와 Production이 확정한 최종 GameState의 전 필드 비교, (iii) 왕복 재생 Fact 목록과 Production Commit Fact 목록의 비교를 모두 포함하며, GameState 필드 인벤토리 누락 시 실패한다. Production 최종 상태는 T04a 관찰 Seam의 읽기 전용 호출로 얻는다. F-26의 Simulation public 위임 메서드는 v1에 도입하지 않는다."
- **변경 대상:** `tools/StarPlayer.SimHarness/Evidence/PzlEventObserver.cs`, `tools/StarPlayer.SimHarness/GameExecutor.cs`(호출 1곳), `tools/StarPlayer.EvidenceVerifier/Pzl6Verifier.cs`, 시험.
- **Hash·Golden·Replay 영향:** 계산식 무변경. GameExecutor는 읽기 전용 호출만 추가(T04a와 같은 방식). #6 문서의 불리언은 현재 표본에서 모두 true이므로 Golden 기대값 변화 없음 예상(채택 후 실제 재실행으로 확인 필요).
- **Codex 최소 시험:** (a) Observer 유무로 모든 결과 Hash·Counters·PZL 동일(기존 시험 + 새 호출), (b) 왕복 재생 상태의 비요약 필드 하나(예: `Stats` 항목)만 다르게 만든 Production 상태 → `final_state_equal = false`, (c) Fact 하나 누락·변경 → false, (d) `GameState`에 필드 추가 시 커버리지 실패.

## 5. §21 Unit별 GAME_RESULTS bundle — 신규 형식 기본안 (새 규범, 미구현)

- **계약** §21: `GAME_RESULTS | Unit별 실행 경기 bundle 1 | Unit 종료 | Raw SHA 필수 | 대용량은 Archive`. R-13: 참조는 Family/Dominance → Unit PZL/PZL6 → 번들/Manifest 방향만. 형식 정의 없음(현재 BLOCKED).
- **참조 구조 검토:**
  - Family/Dominance 문서가 직접 참조하는 안: R-13상 아래 방향이라 허용되나(SEED_MANIFEST가 그 예), Unit PZL과의 교차 결속이 없음.
  - **Unit PZL이 참조하는 안(권장):** GAME_EVENTS(§10, "Unit PZL 문서마다 정확히 1")와 같은 구조로 R-13 사슬 Family/Dominance → Unit PZL → 번들에 맞습니다. Unit PZL `games[].game_result_hash`와 bundle 각 행의 `result_hash`를 교차 결속할 수 있습니다. Family·Calibration·Integration·Dominance 모두 Unit(셀)마다 Unit PZL이 있으므로 "Unit별 1"을 그대로 만족합니다.
- **형식 `simharness-game-results-v1`(기본안):**
  - **직렬화(§10.1과 동일):** `레코드 := JCS(JSON 객체) || 0x0A`의 연속. UTF-8, BOM·CR·빈 행 없음, 마지막 바이트 0x0A, 레코드 1 MiB 이하(초과 시 `archive_invalid`).
  - **레코드 순서:** 1행 Header → 실행 경기마다 Game 1행(부모 실행 순서) → 마지막 행 End.
  - **Header:** `{"bundle":"simharness-game-results-v1","contracts":{5-tuple VER},"family":FAMILY,"run_id":RUN,"unit_evidence_id":ID,"unit_id":EXACT,"validation_set_id":ID}` — 정확히 이 7멤버, 타입은 §1.2 스칼라. 값은 대응 Unit PZL 문서와 일치(§2.4 S3·S5·S6, §10.3과 동일 규칙).
  - **Game:** `{"game":GameResult,"record":"game"}` — `GameResult`는 §3.6 객체 **그대로**(Family/Dominance 문서 `units[i].technical.games[k]`와 JCS 바이트 동일, Dominance는 Regulation 필드 포함 규칙도 §3.6·§4 그대로). Game 행 수 = 실행 경기 수. `game.index`는 1..n 연속, `game_id`·`seed`는 승인 Seed 계획과 같음. **실패 경기 포함**, 정지 후 미실행 경기 제외.
  - **End:** `{"completed_games":N,"executed_games":N,"failed_games":N,"record":"end"}`.
  - **건수:** `End.executed_games` = Game 행 수 = Unit PZL `games` 길이 = `technical.games_executed` = 번들(GAME_EVENTS) `End.executed_games`. `completed_games` = `technical.games_completed`, `failed_games` = `technical.games_failed`.
  - **ArtifactRef:** role `GAME_RESULTS`, `unit_id` = 그 Unit, `game_id` null, 상대 경로는 B-4 전까지 생산자 결정. Unit PZL 문서의 artifacts에 **정확히 1개**.
  - **SHA:** ArtifactRef `raw_sha256`·`byte_length`가 한 번 읽은 스냅샷 바이트와 일치(§21), 영수증 source_files에 ORIGINAL로 결속. 의미 Hash(A+)에는 넣지 않음(M(D)는 artifacts 제외).
  - **검증:** Header 결속 → 각 Game 행의 `game` JCS가 (i) 부모 문서 행, (ii) 검증기 재실행의 `OfficialDocuments.Game` 투영과 바이트 동일, (iii) `game.result_hash` = Unit PZL `games[k].game_result_hash` → 건수·End 집계. **오류 분류(제안):** 형식 위반·파일 누락·Unit당 0개/2개 이상 → `archive_invalid`, 내용 불일치 → `replay_mismatch`, 결속 불일치 → `reference_invalid`.
- **승인 권장 문장(채택 시):** "§21 GAME_RESULTS는 Unit PZL 문서마다 정확히 1개 참조하는 `simharness-game-results-v1` 파일이며, 직렬화·레코드·건수·결속·오류 분류는 OWNER 결정 3 §5의 정의를 따른다. 각 Game 행의 GameResult는 부모 Family/Dominance 문서의 같은 경기 행과 바이트가 같아야 한다."
- **대안(새 형식 없음, 해석):** "§21 GAME_RESULTS는 UNIT_MANIFEST 안 `results.games`로 충족한다." — 표에 'Manifest 내부 허용'이 ATTESTATION에만 있고, 그 요약은 §3.6 20필드가 아니어서 **권장하지 않음**.
- **변경 대상(채택 후):** SimHarness Evidence(Writer·Reader·Shape), `EvidenceWriter.WriteUnits`(Unit PZL 전에 작성), `UnitPzlEvidence.Document`의 artifacts, `Archive.CheckReferences(UnitPzl)`(정확히 1개), `Verifier.VerifyFamily`(내용·건수 대조), 시험 생산자, Traceability.
- **Hash·Golden·Replay 영향:** A+ 무관. Unit PZL·부모 문서의 **Raw 바이트**와 영수증 source_files가 바뀜(artifacts 추가). Golden 고정 입력·기대 A+ 변화 없음(Artifact 제외). Replay 계산·순서 무관.
- **Codex 최소 시험:** (a) Unit별 1개·실패 경기 포함 건수, (b) Game 행 하나 변조 → `replay_mismatch`, (c) 행 누락·순서 교체·`index` 불연속 → 거부, (d) Header의 다른 run·unit·unit_evidence_id → 거부, (e) CR·BOM·빈 행·마지막 LF 없음·1 MiB 초과 → `archive_invalid`, (f) Unit PZL에 0개·2개 → 거부, (g) `result_hash` ≠ Unit PZL `game_result_hash` → 거부.

## 6. `run/<run_id>/…` — 내부 배치 인정, B-4 공식 경로와 구분

- **계약** §21: "파일명과 실제 출력 루트는 B-4에서 정합니다. 여기서는 역할과 개수만 고정합니다." UNIT_MANIFEST "Unit별 1", ATTESTATION "Unit별 1(Manifest 내부 허용)", §20.2 1 "Hash·파싱·Replay가 같은 바이트".
- **현재 구현:** DRAFT 색인을 `run/<run_id>/<색인>`(DIAGNOSTICS, unit_id null), Unit manifest를 `run/<run_id>/<units|cells>/<id>/manifest.json`(UNIT_MANIFEST)으로 Archive에 복사, 부모 문서가 참조, 검증기는 그 바이트만으로 Replay·Attestation.
- **승인 권장 문장:** "`run/<run_id>/…`는 B1 검증기·TEST_ONLY 생산자의 **내부 Archive 배치**로 인정한다. 이것은 B-4의 공식 출력 경로·파일명 동결이 아니며, B-4가 다른 경로를 정하면 그 결정이 우선한다. 경로와 무관하게 역할·개수(UNIT_MANIFEST Unit별 1 — ATTESTATION은 그 내부, 실행 색인은 DIAGNOSTICS 1)와 'Replay·Attestation·영수증이 같은 스냅샷 바이트' 규칙은 고정이다."
- **영향:** A+·Golden·Replay 무관(경로만). 영수증 source_files 경로.
- **Codex 최소 시험:** 기존 T11-03 시험(결과 디렉터리 변경 후에도 Archive 바이트로 검증, 색인·manifest 누락 거부) + 색인 역할 변조 거부.

## 7. #6 20경기 JSON — 역할을 PZL6으로 정리 (현재 구현 보완 필요)

- **계약** §21: `PZL6 | Integration 1 + 20경기 JSON bundle | 고정 표본 검사 후 | 필수`. §6 "Raw JSON SHA는 ArtifactRef와 영수증에만 둡니다." R-13: PZL6 → 번들.
- **현재 구현(T11-03):** #6 문서가 표본마다 **GAME_RESULTS** 파일 1개(기존 v2 Writer UTF-8 출력, `unit_id` = automated/non-automated, `game_id` = 경기 ID) 20개를 참조, 검증기 Writer 출력과 바이트 대조.
- **충돌:** §21은 20경기 JSON을 **PZL6 행**에 둡니다. 5번 기본안을 채택하면 GAME_RESULTS는 "Unit PZL마다 정확히 1(game_id null)"인데, 현재 #6 JSON도 `unit_id = automated` GAME_RESULTS여서 역할 기준 집계에서 중복 계산됩니다. 그래서 **현재 역할 배정은 유지할 수 없습니다.**
- **보완안(해석, 새 형식 없음):**
  - 역할: 20개 JSON 파일을 role **`PZL6`**, `unit_id` = 모드(automated/non-automated), `game_id` = 경기 ID(non-null)로 둡니다. #6 문서 자신은 role PZL6, `unit_id` null, `game_id` null입니다.
  - 참조 범위: Integration 문서 → #6 문서(PZL6, game_id null) **정확히 1**, #6 문서 → 20경기 JSON(PZL6, game_id non-null) **정확히 20**, 표본 순서(automated 1..10 → non-automated 1..10). Calibration·Dominance에는 PZL6 없음.
  - Artifact ID: `pzl6-json-<mode>-<index:D2>`(index = 그 모드 계획 순서 1..10). 전체 스냅샷에서 ID 하나는 참조 하나(T11-03 규칙).
  - 보존 개수: PZL6 역할 = Integration당 1 + 20 = 21. GAME_RESULTS 집계에는 들어가지 않습니다(5번과 분리).
  - 바이트: 기존 v2 Writer의 UTF-8 출력 그대로(새 형식 없음), 검증기 재계산과 바이트 동일.
- **승인 권장 문장:** "§21 PZL6의 '20경기 JSON bundle'은 고정 표본 20경기 각각의 기존 v2 Writer 출력 파일 20개이다. 각 파일은 role PZL6, `unit_id` = 모드, `game_id` = 경기 ID이며 #6 문서가 표본 순서대로 정확히 20개 참조한다. #6 문서 자신은 role PZL6, `game_id` null로 Integration 문서가 정확히 1개 참조한다. 이 파일들은 GAME_RESULTS가 아니며 Unit별 GAME_RESULTS 개수에 포함하지 않는다."
- **대안(새 규범):** 20경기 JSON을 한 파일 `simharness-pzl6-json-bundle-v1`(JCS+LF 행마다 `{game_id, json}`)로 묶어 role PZL6 1개로 둠(PZL6 = 1 + 1). '묶음 1개'를 문자 그대로 원할 때만.
- **변경 대상(채택 시):** `Archive.AllowedRoles(Pzl6)`에 PZL6 추가·GAME_RESULTS 제외, `Archive.CheckReferences(Pzl6)`(game_id non-null PZL6 정확히 20), `Verifier.VerifyFamily`(#6 문서 선택을 `role PZL6 ∧ game_id null`로), `Verifier.VerifyPzl6`(역할 필터), 시험 생산자, 관련 시험.
- **Hash·Golden·Replay 영향:** A+ 무관(Artifact). #6·Integration 문서 Raw 바이트·영수증 source_files 변화. Golden·Replay 무관.
- **Codex 최소 시험:** (a) #6 JSON 20개·역할 PZL6·표본 순서, (b) 19개·21개·순서 교체·바이트 변경 → 거부, (c) #6 JSON을 GAME_RESULTS로 두면 거부, (d) Integration에 game_id null PZL6가 0·2개 → 거부, (e) 5번 채택 후 Unit별 GAME_RESULTS 집계에 #6 JSON이 섞이지 않음.

---

## Codex 표적 재검수 최소 시험 묶음 (결정·보완 후)

1. T11-01~08 기존 회귀(`T11FixTests`, `T11CertificationTests`, `ArchiveTests.T11_04_*`, `OfficialPzl6VerificationTests`)
2. 1번 (a)~(c), 2번 (a)~(d), 3번 (a)~(d)
3. 4번 (a)~(d) — Observer 불변성 재확인 포함
4. 5번 (a)~(g) — 채택·구현된 경우
5. 6번 + 7번 (a)~(e)
6. 기존 Golden 4개, Dry Pilot 9 Run 재현(4번 Seam 변경 시), 전체 `verify.ps1`

## 결정 후 진행 순서(제안)

1. OWNER가 1~7 각각 채택·수정·거부를 새 결정 기록으로 남김(기존 원문 불변)
2. 구현 보완: 2(Reader), 4(Seam 읽기·비교), 7(역할), 5(채택 시 신규 형식)
3. 전체 회귀·`verify.ps1` → Codex 표적 재검수
