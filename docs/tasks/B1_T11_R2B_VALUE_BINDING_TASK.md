# Star Player B1 — T11 R2-B 마지막 MINOR 수정 TASK (Preflight 결정적 값 결속)

- 작성: Claude Code, 2026-10-25
- 근거: OWNER 채팅 지시 "Star Player — B1 T11 R2-B 마지막 MINOR 수정"(2026-10-25 수신). 지시 전문의 요구는 1절에 모두 옮겼습니다.
- 범위: **R2-B 남은 MINOR 1건만** 다룹니다. R1·R2-A·R3·R4는 Codex가 RESOLVED로 판정했으므로 재설계하지 않습니다. 새 OWNER 규범이나 계약 변경은 없습니다.
- 이전 작업 이력 [B1_T11_R2_FINAL_FIX_TASK.md](B1_T11_R2_FINAL_FIX_TASK.md)는 그대로 보존합니다. 루트 `TASK.md`(NSC-6 OWNER 증거)는 수정하지 않습니다. 진행 상황은 [B1_IMPLEMENTATION_LOG.md](../reports/B1_IMPLEMENTATION_LOG.md)에 남깁니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 0. 기준 상태

| 항목 | 값 |
|---|---|
| Git HEAD | `257403ddae557a54c4013c17705370caeac7a2e0`, 스테이징 0, Commit·Push 없음 |
| 보호 SHA | 계약 `1338a2f7…7d64` · `TASK.md` `ecbc0859…f825` · 기준 목록 `a9865a2f…9430`(5개) |
| 직전 결과(참고만) | verify 2026-10-25 18:41:01 `GATE: PASS` 3,039. 보고서: [B1_T11_R2_FINAL_FIX_REPORT.md](../reports/B1_T11_R2_FINAL_FIX_REPORT.md) |

## 1. OWNER 지시 요지와 최신 Codex Finding

**Finding:** R2-B MINOR. `PreflightRecords.Verify`는 Preflight를 독립 재실행하지만 `SameMembers`가 일부 객체의 멤버 이름과 순서만 확인하고 값은 보지 않습니다. 그래서 다음과 같이 제출 기록의 결정적 필드가 달라도 거부하지 못합니다.

- `preflight.games_per_cell`, `schema_status`, `official_evidence`, `complete`, `family_gate`, `dominance_gate` 등

Codex 원문은 저장소에 없고, 위 내용은 OWNER 지시의 기술입니다.

**요구:**

1. 이 TASK를 작성하고, 이 범위를 구현·시험합니다.
2. 독립 재실행에서 만든 기대 Preflight 기록과 제출 기록을 비교합니다. 대상은 Family·Dominance·Unit Preflight의 **결정적 필드 전체**이며, 다음을 모두 지킵니다.
   - 이름·순서·타입·값을 검사하고, 중첩 객체·배열도 비교합니다.
   - 누락·추가·중복은 거부합니다.
   - 제출값을 기대값으로 쓰는 자기 검증을 하지 않습니다.
   - 기대값은 신뢰된 승인 입력과 재실행에서 도출합니다.
   - Raw SHA·길이가 일치하더라도 값이 다르면 거부합니다.
   - 기존 환경·출력 경로 제외 정책은 유지하고, 제외 범위를 넓히지 않습니다.
   - A+에서 제외된 필드라는 이유로 Preflight 비교에서 제외하지 않습니다.
   - 기존 오류 분류와 Fail-Closed를 유지합니다.
3. RED/GREEN: 정상 기록에서 결정적 필드 하나를 바꾸고 Raw SHA·byte_length·ArtifactRef(·Inventory)를 다시 맞춘 뒤 검증해, **SHA 불일치가 아니라 Preflight 내용 검증에서** 거부되는지 봅니다. 변조 대상은 다음과 같습니다.
   - games_per_cell, schema_status, official_evidence false→true, complete false→true, family_gate, dominance_gate NOT_EVALUATED→PASS
   - 숫자·문자열·boolean 타입 변경, 중첩 필드, 필드 누락·추가
   - 정상 기록은 통과해야 합니다.
   - 수정 전 실행하지 못한 시험은 RED라고 주장하지 않습니다.
4. 양성 경로를 유지합니다.
   - Calibration·Integration·Dominance·Phase 영수증, INPUT_BUNDLE 4, PREFLIGHT 57
   - RAW_INVENTORY = SourceFiles, 승인·Build·Seed·Manifest·Regression Proof 결속
   - Seed 991000 유지, Unit 기록의 `run_status=NOT_STARTED`·`error_code=Official.execution_not_implemented` 의미 유지
5. R1·R2-A·R3·R4, T11-01~08, OWNER 결정 7건, GAME_RESULTS·PZL6, Regression Proof, Hash·RNG·Replay를 약화하지 않습니다. 기대값을 단순 변경하거나 검사를 제거하지 않습니다.
6. 전체 회귀를 실행합니다.
   - Release·전체 시험·Golden 4·Phase 영수증·R1~R4·OWNER 결정 7
   - `tools/b1-drypilot-repro.ps1`(ConfigHash·RunResultHash·환경 제외 Manifest), `tools/verify.ps1`
   - D3 5개, 문서 수정 후 D1·D3 재확인
7. 결과 문서는 `docs/reports/B1_T11_R2B_VALUE_BINDING_REPORT.md`입니다. 기존 보고서를 소급해 고치지 않습니다.
8. 금지: 계약·OWNER 원문·`TASK.md`·D3 기준 변경, Commit·Push, 운영 Gate 실행·PASS 처리, Official Execution·Final Execution Freeze.
9. 완료 후 멈추고 Codex READ-ONLY 재검수와 OWNER 최종 수용을 기다립니다.

## 2. 계약 §21 관련 조건

- `PREFLIGHT`: Family·Unit별 필요 기록, 첫 경기 전, Raw SHA 필수
- 보존 규칙: 생성·Archive·스냅샷 시점 바이트를 대조하고, Hash한 뒤 다시 열어 다른 내용을 파싱하지 않습니다.
- 참조와 원본 바이트는 영수증에서 결속합니다(§2.5).
- Raw SHA·ArtifactRef 일치는 내용의 올바름을 보증하지 않습니다(이번 결함).

## 3. 현재 결함 위치

`tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs`:

- `PreflightRecords.Verify` — Family 기록은 schema_version·run_type·run_id·family·run_status·ready·error_code·Unit 목록·checks만 보고, schema_status·official_evidence·official_evidence_note·complete·games_per_cell·family_gate(_reason)·dominance_gate(_reason)는 보지 않습니다.
- Unit 기록은 고정 목록 값과 checks의 평가 대상 검사만 봅니다.
- `SameMembers`는 최상위와 `preflight`의 이름·순서만 확인합니다. 중첩 Unit 행과 check 행의 타입·값은 일부만 확인합니다.

## 4. 수정 범위와 비교 정책

- **기대 기록:** 검증기 재실행 Report(승인값, 경기 없음, 쓰기 없음)를 READY 기록으로 바꿉니다. 재실행이 NOT_READY인 이유가 "검증기 승인 인스턴스는 ExecutionPathReady=false"뿐인 검사만 READY 값으로 대체합니다.
  - `execution_path`: (PASS, null, 공식 실행 준비 메시지)
  - Unit `attestation`: (PASS, null, 검증기가 승인 입력으로 직접 만든 Attestation Hash)
  - `unit_preflight`/`cell_preflight`: (PASS, null, 전체 통과 메시지)
  - 메시지 문자열은 SimHarness의 단일 정의를 공개 상수·함수로 옮겨 생산자와 검증기가 같이 씁니다. 바이트는 그대로입니다.
  - Unit Report의 Attestation도 검증기가 `OfficialPreflight.BuildAttestation`으로 승인 입력에서 만듭니다(AMPV와 같은 경로). 이 Attestation이 보존 UNIT_MANIFEST의 Attestation과 같은지도 확인합니다.
  - 기대 문서는 기존 형식 함수(`FamilyOfficial/DominanceOfficial.PreflightDocument`, `OfficialOutput.Result`)로 직렬화합니다.
- **비교:** 기대 JSON과 제출 JSON을 전체 트리로 비교합니다.
  - 객체: 멤버 이름 순서가 같아야 합니다(누락·추가·중복 거부).
  - 배열: 길이와 순서가 같아야 합니다.
  - 원시값: 타입(ValueKind)과 원문 토큰이 같아야 합니다.
- **제외(기존 정책 안에서 오히려 좁힘):** `output_directory` 검사의 `message`(출력 절대 경로, 환경 의존)만 값을 비교하지 않습니다. 이 항목도 문자열 타입이어야 하며, 그 행의 id·status(PASS)·code(null)는 비교합니다.
  - 직전 라운드에서 값 비교를 제외했던 cli·git_integrity·git_sha·execution_path·attestation은 이번에 결정적 값이 확인되어 모두 비교합니다.
  - git_integrity는 Clean일 때 고정 메시지이고, 재실행의 Git은 승인 SHA의 Clean 평가입니다.
- **오류:** 내용이 다르면 기존대로 `Evidence.approval_mismatch`/`Preflight.record_differs`이며, 다른 첫 JSON Pointer를 함께 보고합니다. 누락·경로는 기존 `archive_invalid` 분류를 유지합니다.

## 5. RED/GREEN 시험

- 위치: `tests/StarPlayer.EvidenceVerifier.Tests/T11R2BValueTests.cs`(신규, `T11CertificationTests` partial)
- 방법:
  1. 정상 Calibration·Dominance 증거를 새 Archive로 복사하면서 PREFLIGHT 한 개만 변조합니다.
  2. 변조 파일로 Raw SHA·byte_length·ArtifactRef를 다시 만들고, Family 문서도 그 참조로 다시 씁니다.
  3. 그 Archive의 세션에서 검증·발급합니다.
  4. 원인 `Preflight.record_differs`(SHA 불일치 아님)를 확인합니다.
- 변조 목록:
  - Calibration Family 기록: schema_status, official_evidence(→true, →"false" 문자열), complete(→true, →0 숫자), family_gate, family_gate_reason, official_evidence_note, 중첩 `preflight.units[0].seed_manifest_path`, 멤버 추가·누락
  - Dominance Family 기록: games_per_cell(2→3, →"2" 문자열), dominance_gate NOT_EVALUATED→PASS
  - Unit 기록: 중첩 `checks[git_sha].message`
- 정상 복사본(변조 없음)은 통과해야 합니다.
- 수정 전 RED는 실제로 실행해 보존합니다. 멤버 추가·누락처럼 기존에도 거부되던 항목은 RED가 아니라고 그대로 기록합니다.

## 6. 전체 회귀 조건

- 5절과 1절 4~6항을 따릅니다.
- 기존 `R2B_the_family_and_unit_preflight_records…` 시험의 반례는 모두 유지합니다(약화 없음).

## 7. 새 세션 재개 절차

1. 이 문서와 구현 로그 "T11 R2-B 값 결속" 절을 읽습니다.
2. `git status`와 `sha256sum -c docs/contracts/b1/B1-T00_BASELINE.sha256`을 확인합니다.
3. `RequiredOriginals.cs`에 `PreflightRecords.Expected`/`FirstDifference`가 있으면 구현이 끝난 것입니다. 시험부터 재개합니다.
4. 순서: RED 시험 → RED 실행·보존 → 구현 → 단계 시험 → Dry Pilot → verify → 보고서·기록 → D1·D3 재확인.
5. 명령 승인 서비스가 실패하면 보안 검사를 우회하지 않고 원인을 보고합니다.
