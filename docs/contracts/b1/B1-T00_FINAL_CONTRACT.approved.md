# B1-T00 최종 단일 규범 기술 계약 — 통합 초안 (S00.5 보완판)

> **문서 지위**
> - 기술 계약 판정: Codex 최종 독립 검수 **`READY_FOR_OWNER_APPROVAL`** (S08)
> - 소유자 승인: **대기 중**(승인 기록 없음) · T01 이후 구현 허가: **없음**
> - Official Execution: **`NOT_READY`** · Final Execution Freeze: **`PENDING`**
> - 이 문서는 S00·**S00.5**·S01~S07을 정해진 대체 순서로 통합한 **초안**입니다. 승인 대상 기준본은 4부 절차에 따라 바이트를 고정한 뒤에 확정됩니다.
> - 작성 과정에서 코드·ADR·TASK.md·Git은 수정하지 않았습니다. 빌드·테스트·공식 실행도 하지 않았습니다.
> - **보완판 변경 범위:** 새 설계는 없습니다. S00.5의 R-1~R-5·R-7~R-14·N01~N14 원문을 복원하고, 후행 PATCH로 대체된 문장은 복원하지 않았습니다. 항목별 대조는 2부 §2.4에 있습니다.
>
> **원문 진본 확인(읽기 전용, 2026-10-24):**
> - 자료집의 BEGIN/END 마커 사이 본문에서 앞뒤 개행을 제거하고 SHA-256을 계산했습니다. S00~S07 **8건 모두 자료집에 표기된 SHA와 일치**합니다.
> - S00.5는 별도 파일 `붙여넣은 마크다운(20261020-150009).md`(65,759바이트, 394줄)의 원본 바이트 SHA-256 `578bf4af24f376e1d799d3b69ddf9fd71f8e956adde795324db91e7c3ce0a3d9`를 기록했습니다.
> - HEAD는 `257403ddae557a54c4013c17705370caeac7a2e0`로 S08 보고값과 같습니다. TASK.md SHA-256 `2f4fb6f86ff4679d4a5a9c0ab6d5670a9b0ebfcac119086d230a1800b79939f7`은 **기존 S08 검수 시점의 참고값**입니다(초안 작성 시 같은 값으로 측정). TASK.md는 이후 바뀌었으므로, 값 구분은 4부 B를 따릅니다.

---

# 1부. 최종 단일 규범 계약 전문

## §0. 적용 원칙

1. **대체 순서.** S00 → S00.5 → S01 → S02 → S03 → S04 → S05 → S06 → S07 순으로 적용합니다. 같은 조항에 대한 후행 대체 문장만 선행 문장보다 우선합니다. 대체되지 않은 S00 기본 명세는 모두 유효합니다.
   - S00.5(「B-1 최종 기술 계약 정합성 검토 및 구현 TASK 설계 보고서」, 2026-10-20 15:00)는 S00(14:24)과 S01(16:04) 사이 문서입니다. S01 §1.2는 "R-1~R-5, R-7, R-9~R-13은 지난 보고서의 정의를 그대로 유지, R-8과 R-14는 P-11과 P-02로 대체, R-6은 RESERVED"라고 정했습니다.
   - 따라서 S00.5의 R-x는 S01 이후 PATCH가 같은 조항을 다시 정하지 않은 범위에서만 유효합니다.
2. **S07에서도 유지되는 S06 조항:** R03-3의 Reader ACE 생성, R03-4 1단계의 두 클라이언트 유형 분리.
3. **구현 범위의 세 구분.**
   - 기존 구현: Manifest, ResultHash, ConfigHash, Attestation, Replay, Metrics, PZL 집계
   - B-1 추가 구현: 공식 JSON 4계열, 의미 기반 A+, 사건별 PZL 증거(GAME_EVENTS), 독립 검증 영수증, 신뢰 기록부 등록
   - 외부 승인 대기: Cohort·Seed·Pin·실행 상한·빌드 출처·결합 정책·공식 표본·운영 계정
4. **기존 계산 불변.** 기존 Hash·RNG·ResultHash·ConfigHash·Attestation Hash·Gate 정책은 바꾸지 않습니다. 새 규칙은 증거 수집·투영·검증·보존·등록에만 적용됩니다.
5. **기준 HEAD:** `257403ddae557a54c4013c17705370caeac7a2e0`
6. 본문 중 `【NSC-n】` 표시는 2부 §2.3의 미해결 `NEEDS_SOURCE_CHECK` 항목입니다. 남은 미해결 표시는 NSC-4·NSC-5뿐이며, 둘 다 미해결 구현 Gate입니다. NSC-1~3은 S00.5로 해소되어 RESOLVED입니다. NSC-6은 OWNER 결정(TASK.md 2026-10-24)이 §7에 반영되어 RESOLVED입니다. 해소된 항목의 본문 표시는 지웠습니다.

---

## §1. 공통 규칙

### 1.1 사전 해석 규칙 (S00 §2.1)

반복 구조는 닫힌 공통 타입을 참조합니다. 예를 들어 `units[].technical: Technical`은 Technical 표의 모든 필드를 그 경로에 전개한다는 뜻이며, 와일드카드가 아닙니다.

1. 명시하지 않은 속성은 거부합니다.
2. 모든 필드는 필수입니다.
3. `?`는 "필드는 반드시 존재하고 값으로 null을 허용"한다는 뜻입니다.
4. 누락과 null을 구분합니다.
5. 기본값 보충, null 요소 제거, enum 자동 변환을 하지 않습니다.
6. 모든 공식 JSON 원본 바이트의 SHA-256을 별도로 보존합니다.
7. `additional_evidence.artifacts`를 제외한 공식 필드는 의미 A+에 포함합니다.
8. 각 필드는 지정된 독립 출처에서 재계산해 비교합니다.
9. **Archive** 경로와 일반 Raw SHA, 환경·시간·디버그 정보는 의미 A+에 넣지 않습니다. 단 승인 입력(§9.5 R-2 분류 ①~③)인 `seed_manifest_path`, `seed_manifest_sha256`, `approval_record_sha256`은 포함합니다(S00.5 F-02 Errata).
10. 승인 입력인 Seed Pin과 외부 승인 기록 Pin은 공식 의미에 포함합니다.
11. 오류에는 JSON Pointer, Family·Unit·Game 식별자, 검증 단계, 원인 코드를 남깁니다.

### 1.2 공통 스칼라 (S00 + S01 §1.1·P-03)

| 표기 | 정확한 정의 |
|---|---|
| `N` | JSON 정수 `0..9007199254740991` |
| `I` | JSON 정수 `0..2147483647` |
| `P` | JSON 정수 `1..2147483647` |
| `PPM` | JSON 정수 `0..1000000` |
| `U64` | §8.2의 정규 UInt64 십진 **문자열** |
| `D64` | 정규 비음수 십진 문자열 `0..9223372036854775807`. RNG raw Draws용 |
| `H` | 소문자 64자리 16진. 기존 Hash와 Raw SHA 표현 |
| `AH` | `sha256:` 뒤에 소문자 64자리. 신규 A+ 표현 |
| `GIT` | 소문자 40자리 Git SHA. 다른 형식은 v1에서 거부 |
| `ID` | ASCII `[A-Za-z0-9][A-Za-z0-9._-]{0,127}` |
| `RUN` | 기존 RunId 규칙. ASCII 영문·숫자·`_-.`, 1~64자 |
| `VER` | JSON 문자열 `^[1-9][0-9]{0,8}$`. ordinal 완전 일치로만 비교(OD-1) |
| `TEXT1` | 일반 단일행 문자열. 길이 1 이상, U+0000–U+001F 금지. **기존 `TEXT`를 대체** |
| `CODE` | `TEXT1`이면서 `^[A-Z][A-Za-z0-9]*\.[a-z0-9_]+$` (예: `Harness.event_budget_exceeded`) |
| `RNGSET1` | `GameRandomSnapshot.Canonical()` 출력 그대로. 첫 행은 `rngset1\|{alg}\|{kdv}\|{16 소문자 hex}\|{context}`, 스트림 행은 `{stream}\|{16hex}\|{16hex}\|{draws}`(draws ≥ 1, 스트림 canonical ordinal 오름차순). 행 구분은 LF뿐, 끝 LF 없음, CR 금지. TEXT1 규칙은 적용하지 않음. 문법 확인 뒤 재관찰 원문과 바이트 비교 |
| `EVCANON` | `EventFingerprint.Canonical(e)` 출력 그대로. TEXT1 규칙 미적용, 재관찰 원문과 바이트 비교 |
| `STREAM` | `^[a-z0-9_]+(/[a-z0-9_]+)*@[1-9][0-9]*$`, 이름 64자 이하 (RngStreamId.cs:19-25) |
| `RELPATH` | `/` 구분 상대 경로. 빈 구간·`.`·`..`·`:`·`\`·NUL 금지. 실제 접근은 PathSafety/PinnedDirectory로 검증 |
| `BITS64` | `^[0-9a-f]{16}$`. IEEE754 binary64 비트를 빅엔디언 16진으로 표기 |
| `EXACT` | 기존 등록 ID·버전·조건 문자열을 변경 없이 보존 |
| `F64` | §8.4의 상태 포함 binary64 값 객체 |
| `Gate` | `{status, reason}` (§3.10) |
| `SID_USER` | `^S-1-5-21(-[0-9]+){3}-[0-9]+$` (로컬·도메인 사용자 계정) |
| `SID_SERVICE` | `^S-1-5-80(-[0-9]+)+$` (`NT SERVICE\{이름}`) |

**문자열 역할 배정(P-03):** `reason`·`name`·`Violation.code`를 비롯한 일반 문자열은 `TEXT1`, 오류 코드 필드는 `CODE`, RNG는 `RNGSET1`, 사건 원문은 `EVCANON`입니다.

**기존 식별자:** 기존 GameId·PlayerId·EventId는 신규 `ID` 문법으로 재정의하지 않습니다. 기존 생성기와 값 객체의 검증을 적용하고, 승인·재실행 결과와 ordinal로 정확히 비교합니다.

### 1.3 오류 분류 (S00 + P-16, 닫힌 상위 목록 17개)

| 상위 코드 | 적용 |
|---|---|
| `Evidence.schema_invalid` | 누락·null 위반·자료형·범위·중복 키·미등록 속성·비정규 수치 |
| `Evidence.contract_not_approved` | Schema/Hash 버전 조합 미승인 |
| `Evidence.approval_mismatch` | 외부 승인·Seed Pin·Cohort·빌드·검증 집합 불일치, Trust Bootstrap 실패 |
| `Evidence.reference_invalid` | 논리 참조·경로·역할·중복·자식 관계 오류 |
| `Evidence.replay_mismatch` | 재실행·사건·RNG·기록 불일치 |
| `Evidence.evaluation_mismatch` | Metrics·Gate·Reason·통계 판정 불일치 |
| `Evidence.pzl_incomplete` | 필수 경기·사건·PreparedPitch 관찰 누락 |
| `Evidence.pzl_mismatch` | PZL 값·순서·검사 결과 불일치 |
| `Evidence.hash_mismatch` | 기존 Hash 또는 신규 의미 Hash 불일치 |
| `Evidence.archive_invalid` | 원본 누락·Raw SHA·바이트 수·스냅샷·경로 안전성 위반 |
| `Evidence.not_certifiable` | v1 인증 허용 조건에 속하지 않는 상태 |
| `Evidence.registration_pending` | 등록 성공 여부 미확인 |
| `Evidence.registration_conflict` | 같은 영수증 ID의 다른 내용 |
| `Evidence.registration_unauthorized` | 권한 없는 기록부 등록 |
| `Evidence.environment_mismatch` | 계산 환경 부적합 (§18) |
| `Evidence.test_profile_mismatch` | Test Profile Case 집합·결과 불일치 (§12.3) |
| `Evidence.authorization_missing` | 실행 허가 부재 (§14) |

**원인 코드(상위 목록에 추가하지 않음):**

| 원인 코드 | 상위 코드 |
|---|---|
| `Trust.anchor_missing`, `Trust.anchor_untrusted`, `Trust.anchor_mismatch` | `Evidence.approval_mismatch` |
| `Trust.registry_mismatch`, `Trust.registry_identity_mismatch`, `Trust.registry_unavailable`, `Trust.registry_config_mismatch` | `Evidence.approval_mismatch` |
| `Trust.client_principal_not_allowed` | `Evidence.approval_mismatch` |
| `Trust.test_only_flag_misuse` | `Evidence.authorization_missing` |

기존 `Replay.*`, `Seed.*`, `Official.*`, `Metrics.*`, `FamOfficial.*`, `DomOfficial.*` 오류는 원인 코드로 보존합니다. 새 오류로 감싸더라도 원인 코드를 지우지 않습니다.

---

## §2. 공통 Root 영역

### 2.1 Schema (S00 + P-01·P-02·PATCH-02)

| 경로 | 타입·값 |
|---|---|
| `schema.id` | 역할별 고정 ID (아래 표) |
| `schema.version` | `VER`, v1은 `"1"` |
| `schema.contracts` | **정확히 5키** `{family, dominance, pzl_unit, pzl6, hash}`, 모두 `VER`, v1은 모두 `"1"` |

| 종류 | `schema.id` | 역할키 |
|---|---|---|
| Family | `simharness-family-result` | `family` |
| Dominance | `simharness-dominance-result` | `dominance` |
| Unit PZL | `simharness-pzl-unit-evidence` | `pzl_unit` |
| 공식 #6 | `simharness-pzl6-roundtrip-evidence` | `pzl6` |

**contracts 규칙:**
- 키 누락·추가·중복은 `schema_invalid`입니다. 중복 키는 DOM 저장 전에 엄격 Reader가 거부합니다.
- 정수 `1`, `"01"`, `"1.0"`은 `schema_invalid`입니다.
- 여섯 번째 계약 계열은 없습니다. 승인 기록 자체의 버전은 `format`으로만 식별합니다.
- 4Schema `schema.contracts`, Phase Root `contracts`, 영수증 `contracts`, GAME_EVENTS Header `contracts`는 승인 기록의 `contracts`와 필드별로 ordinal 일치해야 합니다.
- 다섯 값이 각각 유효하다는 이유만으로 조합을 수용하지 않습니다. 정확한 조합이 승인 기록에 있어야 합니다.

**R-1 유지 조항 (S00.5):**
- 비교는 ordinal 완전 일치로만 합니다. 대소 비교와 수치 해석은 금지합니다.
- 하나의 Payload에 들어가는 모든 문서의 `contracts` 객체는 바이트까지 동일해야 합니다.
- 허용 조합은 승인 기록 `contracts`와 5-tuple이 완전히 일치하는 것뿐입니다(v1은 모두 `"1"`).
- **재검증:** 문서의 5-tuple로 버전별 검증기 레지스트리를 고릅니다("latest" 선택 금지). 과거 증거는 영수증 `verifier.source_git_sha`·`binary_sha256`에 Pin된 검증기로 다시 검증합니다.
- `RoundTripSample.event_format_version`(정수 2)은 계약 버전이 아니라 ScriptedGameJson 형식을 관찰한 사실이므로 정수로 둡니다.
- **v1 대응표 (S00.5 F-01 Errata, 명확화):** `contracts.hash = "1"` ⇔ `hash_contract = "simharness-evidence-hash-v1"` ⇔ Prefix `…-V1` ⇔ 영수증 `simharness-verification-receipt-v1` ⇔ 승인 기록 `simharness-approval-record-v1`

### 2.2 Execution

| 경로 | 타입 | 독립 검증 출처 |
|---|---|---|
| `execution.evidence_id` | `ID`, 검증 집합 내 고유 | R-12 도출식 (아래) |
| `execution.validation_set_id` | `ID` | 승인 기록 |
| `execution.run_id` | `RUN` | 승인 기록 `families[family].run_id` |
| `execution.family` | `fc3-cal`·`fc3-int`·`fc3-dom` (PZL6는 `fc3-int`) | 승인 기록 |
| `execution.git_commit_sha` | `GIT` | 승인 기록 `git_commit_sha` |
| `execution.build_provenance_id` | `ID` | 승인 기록 `build_provenance.id` |

`build_provenance_id`는 자기 선언 문자열이 아닙니다. 승인 기록이 소스 SHA, 실행 바이너리·리소스 Pin, 빌드 검증 자료에 이 ID를 연결해야 하며, B-4가 미확정이면 공식 인증을 거부합니다.

**`evidence_id` 도출식 (R-12, 결정적 도출):**

```
{vs}.{family}.result              Family·Dominance 문서
{vs}.{family}.pzl.{unit_id 소문자}  Unit PZL 문서
{vs}.fc3-int.pzl6                 공식 #6 문서
{vs}.phase                        Phase Root
```

- `vs`는 `validation_set_id`(≤64자)이고, `family`는 `execution.family`입니다.
- 제출된 값과 도출한 값이 다르면 `Evidence.reference_invalid`입니다.

### 2.3 Approvals

| 경로 | 타입 | 검증 |
|---|---|---|
| `approvals.approval_record_id` | `ID` | Registry 조회 (§16) |
| `approvals.approval_record_sha256` | `H` | 조회한 원본 바이트의 SHA |
| `approvals.freeze_id` | `ID` | 승인 기록 `freeze_id` |

증거 작성자가 제출한 승인 파일은 신뢰 기준을 대체하지 않으며 비교 대상일 뿐입니다.

### 2.4 부모·자식 결속 S1~S6 (P-02, R-14 대체)

- **S1.** `schema.id`는 역할별 고정값이어야 합니다. DRAFT 키나 다른 ID는 거부합니다.
- **S2.** `schema.version == schema.contracts[역할키]`
- **S3.** 각 문서의 `schema.contracts` 5필드가 승인 기록 `contracts`와 필드별 ordinal 일치
- **S4.** `approvals` 3필드가 확정 승인 참조(`approval_record_id`, 조회한 `raw_sha256`, `freeze_id`)와 일치
- **S5.** `execution`의 역할 공통 필드(`validation_set_id`, `run_id`, `family`, `git_commit_sha`, `build_provenance_id`)가 승인 기록과 일치
- **S6.** `execution.evidence_id`가 §2.2 R-12 도출식으로 도출한 값과 일치

부모와 자식은 각각 승인 기록과 비교되므로, 서로 일치하는 것은 그 결과로 따라옵니다. **원본 JSON 바이트를 부모·자식 사이에 비교하지 않습니다.**

**R-14 잔여 필드 간 제약 (F-14에 따라 P-02 이후에도 유지):**

| 축 | 제약 |
|---|---|
| git | `execution.git_commit_sha == attestation.git_commit_sha == 승인 기록.git_commit_sha` |
| run_id | `execution.run_id == units[].inputs.unit_run_id` |
| seed_family | `attestation.seed_family == execution.family` |
| counters | `technical.plate_appearances == counters.plate_appearances`, `technical.pitches == counters.pitches` |
| block_size | `stop.block_size == inputs.block_size_games` |
| games_executed | `games_executed == games.length` |
| metrics | `metrics_computed=false` ⇔ `metrics_error_code≠null` |

R-14의 나머지 두 문장은 후행 규범으로 대체되어 복원하지 않습니다.
- "Unit PZL·PZL6의 `execution`·`approvals`·`schema`는 부모와 바이트까지 같아야 함" → P-02 S1~S6
- "`TEXT`에는 C0 제어문자·빈 문자열 금지" → P-03 `TEXT1`(내용 동일)

### 2.5 AdditionalEvidence

| 경로 | 타입·순서 | 의미 A+ |
|---|---|---|
| `additional_evidence.pzl_unit_ids` | `ID[]`, 부모 Unit 순서, 중복 금지 | 포함 |
| `additional_evidence.pzl6_id` | `ID?` | 포함 |
| `additional_evidence.artifacts` | `ArtifactRef[]`, `artifact_id` ordinal 오름차순 | **제외** |

- Family·Dominance: `pzl_unit_ids`는 Unit마다 정확히 하나입니다.
- `pzl6_id`는 Integration에서만 non-null이고, Calibration·Dominance에서는 null입니다.
- Unit PZL·#6 문서: `pzl_unit_ids=[]`, `pzl6_id=null`

**`ArtifactRef` (7필드):**

| 필드 | 타입 |
|---|---|
| `artifact_id` | `ID`, 목록 내 고유 |
| `role` | §21의 닫힌 역할 ID |
| `unit_id` | `EXACT?`, Unit 소속이면 non-null |
| `game_id` | 기존 GameId?, 경기 소속이면 non-null |
| `relative_path` | `RELPATH` (Archive 루트 기준) |
| `raw_sha256` | `H` |
| `byte_length` | `N` |

문자열 경로 검사만으로 안전하다고 보지 않습니다. 실제 접근에는 핸들·Reparse Point 검증을 적용합니다. 참조를 의미 Hash에서 제외해도 검증에서 제외되는 것은 아니며, 참조와 원본 바이트는 영수증에서 결속합니다.

---

## §3. Family Schema

### 3.1 Root (정확히 7개 속성)

`schema, execution, approvals, plan, units, aggregate_evaluation, additional_evidence`

| 경로 | 타입·제약 | 독립 검증 |
|---|---|---|
| `plan.unit_ids` | Calibration `["L1","L2","L3","L4"]`, Integration `["automated","non-automated"]` | 승인 계획 |
| `plan.combination_policy_clause_id` | `EXACT` | 승인 기록 `combination_policy_clauses` (B-6c/B-6d) |
| `units` | `FamilyUnitResult[]`, 계획과 같은 순서·개수 | 누락·중복·교체 검사 |
| `aggregate_evaluation` | `Gate` | 승인 결합 정책으로 재계산 |

`FamilyUnitResult = {unit_id, inputs: UnitInputs, technical: Technical, evaluation: FamilyUnitEvaluation}`

### 3.2 UnitInputs (Family·Dominance `units[].inputs`)

| 필드 | 타입·조건 | 독립 검증 |
|---|---|---|
| `unit_run_id` | `RUN` | 원본 Manifest·승인 Unit |
| `mode` | `EXACT` | 승인 실행 모드, 원본 `run.mode` |
| `cohort_kind` | `Neutral` \| `VirtualLeague` | 승인 Cohort 재구성 |
| `rules_version` | `EXACT` | 실제 RuleSnapshot |
| `rules_status` | 기존 등록 enum 문자열 | 실제 RuleSnapshot |
| `called_pitch_mode` | `Automated` \| `NonAutomated` (null 금지) | 실제 RuleSnapshot |
| `player_input_set_content_version` | `P` | 승인 PlayerInputSet |
| `player_input_set_id` | `EXACT` | 콘텐츠로 ID 재계산 |
| `planned_games` | `P`, 최대 999999 | 승인 Seed 계획 크기 |
| `block_size_games` | `P?` | Calibration 필수, 나머지 null |
| `pa_target` | `N?`, non-null이면 양수 | Calibration 필수, 나머지 null |
| `games` | `GameSpec[]` | **전체 승인 Seed 계획** (실행 Prefix만 넣지 않음) |
| `attestation` | Attestation v1 21필드 | 기존 검증 경로 |
| `attestation_hash` | `H` | 기존 Attestation Hash 재계산 |

**`GameSpec = {index: 1..999999, game_id: 기존 규칙으로 재생성한 GameId, seed: U64}`**
- 배열은 승인 Seed 파일 순서를 유지합니다.
- Index·GameId 중복, 순서 변경, 기존 Reader의 유일성·생성 규칙 위반을 거부합니다.

### 3.3 Attestation v1 21필드 (`units[].inputs.attestation`)

| 필드 | 값 | 필드 | 값 |
|---|---|---|---|
| `schema_version` | `simharness-official-attestation-v1` | `policy_id` | 승인 전략 ID |
| `git_commit_sha` | `GIT` | `model_version` | 승인 Probability Model ID |
| `execution_policy_id` | `EXACT` | `tuning_version` | 승인 Tuning ID |
| `execution_policy_hash` | `H` | `tuning_hash` | 기존 Tuning ContentHash `H` |
| `seed_manifest_path` | 기존 승인 경로 문자열 | `rules_profile_id` | 승인 RuleProfile ID |
| `seed_manifest_sha256` | 외부 승인 Seed 원본 `H` | `rules_hash` | 기존 RuleSnapshot ContentHash `H` |
| `seed_family` | 부모 Family와 동일 | `advancement_policy_version` | 승인 Advancement 정책 ID |
| `competition_key` | 승인 Competition Key | `park_snapshot` | 기존 `OfficialProfile.ParkTextOf` 문자열 |
| `game_id_rule` | 기존 등록 규칙 ID | `event_budget` | 정수 `1..999999`, 승인값 일치 |
| `cohort_id` | 승인 Cohort ID | `config_hash` | 전체 승인 계획으로 계산한 기존 ConfigHash |
| `cohort_inputs_hash` | 기존 InputsHash `H` | | |

이 객체를 JCS로 표현해도 **기존 Attestation Hash 계산식은 바꾸지 않습니다**(Official.cs:80, RunModel.cs:133).

### 3.4 Technical (S00 + P-07)

| 필드 | 타입·조건 | 재계산 출처 |
|---|---|---|
| `run_status` | `COMPLETED` \| `GAMES_FAILED` \| `METRICS_ERROR` | 기존 RunStatus |
| `run_result_version` | Calibration `"run-result-v2"`, Integration·Dominance `"run-result-v1"` 고정 | 기존 ResultVersion |
| `run_result_hash` | `H` | 기존 계산 |
| `games_requested` | `I` | 전체 계획 수 |
| `games_executed` | `I` | 실제 실행 결과 수 |
| `games_completed` / `games_failed` | `I` | Completed / Failed 수 |
| `games_cancelled` | `I`, **반드시 0** | |
| `games_not_started` | `I`, **반드시 0** | 기존 NotStartedCount |
| `games_unexecuted_after_stop` | `I` | 기존 UnexecutedAfterStop |
| `plate_appearances` / `pitches` | `N` | 완료 경기 Aggregate |
| `committed_events` | `N` | 실행된 모든 경기의 committed events 합(실패 경기의 확정 사건 포함) |
| `end_kinds` | `EndKindCount[]` | 아래 규칙 |
| `error_codes` | `CODE[]` | Failure.Code 중복 제거 후 ordinal 정렬 |
| `metrics_computed` | bool | `run.Metrics.IsSuccess` |
| `metrics_error_code` | `CODE?` | 성공이면 null, 실패면 원인 코드 |
| `stop` | `Stop?` | 아래 규칙 |
| `counters` | `Counters` | 완료 경기만 합산 |
| `games` | `GameResult[]` | 승인 계획의 실제 실행 Prefix 순서 |

- `abort_code` 필드는 **삭제**되었습니다.
- 공식 문서는 `FamilyOfficialResult.Complete` 또는 `DominanceOfficialResult.Complete`일 때만 생성합니다(R-4). 불완전 실행(취소·무결성 중단·일부 Unit만 실행)은 DRAFT/원본 증거로만 남습니다.
- Family 수준 상태는 문서에 넣지 않고 기존 `StatusOf(units)`로 도출합니다(영수증 §20.1 R-11에서 사용).

**`EndKindCount = {end_kind: ExtraInnings|Regulation|TieAtExtraInningsLimit, count: 양수 I}`**
- `None`은 제외하고, `end_kind` ordinal 오름차순, 중복 금지입니다.
- 실행 경기 중 `EndKind != None`인 결과를 집계합니다.

**`Stop` (4필드)**
- 필드: `executed_games: P`, `stop_reason: TARGET_REACHED|SEEDS_EXHAUSTED`, `cumulative_pa: N`, `block_size: P`
- Calibration은 **항상 non-null**입니다. 공식 문서는 Complete 실행에만 생성되고, Complete이면 Rule 종료이므로 Stop이 항상 존재합니다(R-4). Integration·Dominance에서는 null입니다.
- 최초 합법 블록 경계는 승인 계획에서 독립 재도출합니다.

### 3.5 Counters (정확히 28필드, 모두 필수·non-null `N`)

| 구분 | 필드 |
|---|---|
| 타석 (12) | `plate_appearances, at_bats, hits, singles, doubles, triples, home_runs, walks, hit_by_pitch, strikeouts, sacrifice_flies, sacrifice_bunts` |
| 투구 (12) | `pitches, located_pitches, unlocated_pitches, zone_pitches, hit_by_pitch_pitches, swings, swinging_strikes, fouls, in_play_pitches, called_balls, called_strikes, takes` |
| 교차검산 (2) | `event_home_runs, event_hits` |
| 사건 (2) | `events, duplicates_ignored` |

- 적용 경로: `units[].technical.counters`, `units[].technical.games[].counters`
- 확정 Commit의 사건·PA 완료 사실에서 수집하며, BoxScore 경로와 따로 계산한 뒤 대조합니다.
- 실패 경기의 부분 카운터는 경기별로 보존하지만 정상 Aggregate에는 합산하지 않습니다.
- 수집이 끝나면 가변 객체 참조가 아니라 값으로 복사합니다.

### 3.6 GameResult

| 필드 | 타입 | 필드 | 타입 |
|---|---|---|---|
| `index` | 승인 GameSpec index | `committed_events` | `I`, 최대 999999 |
| `game_id` | 승인 GameId | `event_hash` | 기존 `H` |
| `seed` | 승인 `U64` | `rng_hash` | `H?` (D행만 null) |
| `status` | `Completed` \| `Failed` | `result_hash` | 기존 game-result-v1 `H` |
| `end_kind` | `None`·`Regulation`·`ExtraInnings`·`TieAtExtraInningsLimit` | `error_code` | `CODE?` |
| `outcome` | `AwayWin`·`HomeWin`·`Tie`·null | `failure` | `Failure?` |
| `away_runs`, `home_runs` | `I` | `counters` | Counters |
| `final_inning` | `I` | `box_score_cross_check` | `CrossCheck?` |
| `final_half` | `Top` \| `Bottom` | `regulation_half_innings` | `I[16]?` |
| | | `regulation_error_code` | `CODE?` |

**상태표 (P-06, 기존 코드와 일치: GameExecutor.cs:28-35, :52-89, :182-197)**

| 행 | 조건 | 허용 `failure.stage` | outcome | end_kind | box_score_cross_check | rng_hash | Regulation (Dominance만) |
|---|---|---|---|---|---|---|---|
| A | `Completed` | (failure null) | non-null | ≠None | non-null, `passed=true` | H | `half_innings`·`error_code` 중 정확히 하나만 non-null |
| B | `Failed`, 엔진 종료 상태 | `observe, progress, final, metrics, crosscheck, exception` | non-null | ≠None | stage가 `crosscheck`일 때만 non-null·`passed=false` | H | 정확히 하나만 non-null |
| C | `Failed`, 시작 후 종료 전 | `budget, advance, observe, progress, final, exception` | null | None | null | H | 둘 다 null |
| D | `Failed`, 시작 전 | `config, start` | null | None | null | **null** | 둘 다 null |

- D행은 추가로 `failure.rng_hash=null`, `committed_events=0`, `final_inning=0`, `final_half="Top"`, Counters 28개 모두 0이어야 합니다.
- `error_code == failure.code`
- 공식 문서에서는 `stage=cancelled`와 `status=Cancelled`를 금지합니다.
- `regulation_error_code`는 `Dominance.regulation_incomplete`, `Dominance.line_score_total_mismatch`, `Dominance.line_score_mismatch` 중 하나입니다.
- Family 문서는 두 Regulation 필드를 항상 null로 투영합니다(비인증 진단).
- Dominance의 `regulation_half_innings`는 1~8회 초·말 순서의 16개 값입니다.
- 실패 경기의 부분 상태와 null 여부도 재실행 결과와 비교합니다.

**`Failure = {code: CODE, stage: TEXT1(위 표의 허용값), event_sequence: I, revision: N, rng_hash: H?, last_receipts: ReceiptRef[] (≤12, 원래 저장 순서)}`**
- `ReceiptRef = {event_id: 기존 EventId, kind: 기존 사건 종류, fingerprint: H}`
- 원문 오류 메시지는 Raw 진단에 보존하고 의미 A+에서는 제외합니다.

**`CrossCheck = {passed: bool, items: CrossCheckItem[15]}`, `CrossCheckItem = {name: TEXT1, from_events: N, from_box_score: N}`**
- 항목 순서(고정): `PA, AB, H, 1B, 2B, 3B, HR, BB, HBP, SO, SF, Pitches, HR(in-play events), H(in-play events), HBP(pitch events)`
- 15개 항목의 양쪽 값을 재계산합니다. `passed`는 모든 항목이 일치하는지 여부와 같아야 합니다.

### 3.7 FamilyUnitEvaluation (S00 + P-08)

| 필드 | 타입 |
|---|---|
| `kind` | `CALIBRATION` \| `INTEGRATION` |
| `policy_id` | `EXACT?` |
| `policy_hash` | 기존 평가 정책 `H?` |
| `evaluation_computed` | bool (P-08: 기존 `metrics_computed`에서 이름 변경) |
| `error_code` | `CODE?` |
| `gate` | Gate |
| `shares` | `Shares?` |
| `metrics` | `OfficialMetric[]` |

**CALIBRATION (R-5)**
- `policy_id`·`policy_hash` = `EvaluationPolicy.FcEvaluationV1`의 ID·Hash입니다. **계산 성공 여부와 관계없이 항상 non-null**이며, Canonical 텍스트도 이 값을 포함합니다.
- `evaluation_computed` = `StratifiedEvaluator.Evaluate`의 성공 여부
- `error_code` = 기존 `UnitVerdict.ErrorCode`. 계산에 성공해도 run 지표 오류가 있으면 non-null일 수 있습니다.
- `shares`는 계산에 성공했을 때만 non-null입니다.
- `metrics`는 계산에 성공하면 `Official=true`인 지표만 F-ID 숫자 순으로 담고, 실패하면 `[]`입니다. 공식 지표는 L1·L2가 `F1, F6, F7, F8`, L3·L4가 `F2, F3, F4, F5`입니다.
- 계산이 실패하면 원인 코드와 실제 Gate를 보존합니다(SR-D9.2 NOT_EVALUATED 유지).

**INTEGRATION (R-5)**
- `policy_id=null`, `policy_hash=null`
- `evaluation_computed` = `run.MetricsComputed`
- `error_code` = run 지표 오류 코드
- `shares=null`, `metrics=[]`. 진단용 F1~F8을 공식 지표로 승격하지 않습니다.
- 승인된 Integration 완료 정책으로 계산한 `gate`를 보존합니다. 진단 수치는 Raw summary에만 둡니다.

**`Shares`:** `same_games: I, opposite_games: I, mixed_games: I, same_pa: N, opposite_pa: N, same_pitches: N, opposite_pitches: N, same_swings: N, opposite_swings: N`

### 3.8 OfficialMetric

| 필드 | 타입 | 필드 | 타입 |
|---|---|---|---|
| `f_id` | 해당 층의 공식 F-ID | `denominator_opposite` | `N` |
| `target_id` | 등록 Target ID | `measured_ppm` | `PPM?` |
| `method` | `standardized_3_7_4_7` \| `observed_aggregate` | `target_low_ppm` | `PPM` |
| `numerator`, `denominator` | `N` | `target_high_ppm` | `PPM`, low 이상 |
| `numerator_same`, `denominator_same` | `N` | `min_denominator` | `N` |
| `numerator_opposite` | `N` | `min_same`, `min_opposite` | `N?` |
| | | `gate` | Gate |

- 배열은 F-ID 숫자 오름차순, 중복 금지입니다.
- 값·분모·표본 기준·반올림·Reason은 기존 평가 경로로 재계산합니다.
- Wilson·SE·DEFF·batch 진단값은 이 공식 객체에 넣지 않습니다.

**R-5 세부 조건:**
- `method="standardized_3_7_4_7"` ⇔ 해당 지표가 정책의 StandardizedMetrics에 포함됨
- `min_same`·`min_opposite`는 둘 다 null이거나 둘 다 non-null입니다. observed 지표는 둘 다 null입니다.
- `measured_ppm=null` ⇔ (standardized: 같은 쪽 또는 반대쪽 분모가 0) 또는 (observed: 분모가 0)

### 3.9 (예약) R-6 — RESERVED / UNUSED (P-09)

### 3.10 Gate

- `status`: `PASS`, `FAIL`, `INSUFFICIENT_SAMPLE`, `NOT_EVALUATED`. Dominance에서만 `INCOMPLETE`를 추가로 허용합니다.
- `reason`: `TEXT1`, 재계산 결과와 문자열까지 일치해야 합니다.

---

## §4. Dominance Schema (S00 + P-10/R-7)

**Root:** `schema, execution, approvals, plan, units, aggregate_evaluation, additional_evidence`

| 경로 | 타입 |
|---|---|
| `plan.cell_ids` | 승인된 48개 Cell ID, 승인 실행 순서 |
| `plan.games_per_cell` | `P` (P-10) |
| `units` | `DominanceUnit[48]` = `{cell: Cell, inputs: UnitInputs, technical: Technical}` |
| `aggregate_evaluation` | `DominanceEvaluation` |

**`Cell`:** `id`(기존 카탈로그 ID), `batter_strategy`(`Normal|Patient|Aggressive|TakeFirst`), `pitcher_strategy`(`Balanced|Attack|Careful`), `hand`(`Same|Opposite`), `called_pitch_mode`(`Automated|NonAutomated`), `cohort_id`, `policy_id`(`dom-*-v1`), `competition_key`
- 48개 객체 전체를 기존 카탈로그와 비교하며, 같은 ID에 다른 축 조합을 붙이는 것을 거부합니다.

**`DominanceEvaluation`:** `policy_id`(기존 통계 정책 ID), `frozen_games_per_cell`(계획 값과 동일), `cells: CellEvaluation[48]`(계획 순서), `set_problems: TEXT1[]`(기존 평가기의 생성 순서), `batter: SideResult`, `pitcher: SideResult`, `gate: Gate`
- `frozen_games_per_cell: P`이며 `frozen_games_per_cell == plan.games_per_cell`입니다(R-7, S00의 `P?`를 대체).
- `gate=INCOMPLETE` ⇔ 어느 쪽도 `DOMINANCE_DETECTED`·`NOT_EVALUATED`가 아니고, 적어도 한쪽이 `INCONCLUSIVE` (R-7)

**`CellEvaluation`:**
- `cell_id`
- `state`: `Ok|Missing|IntegrityViolation|Incomplete|EngineFailure|MetricsFailure|InsufficientSample`
- `reason: TEXT1`, `config_hash: H` (P-10), `run_result_hash: H` (P-10)
- `planned_games: I`, `failed_games: I`, `metrics_failures: I`, `sample: CellSample?`
- `sample` non-null ⇔ `state=Ok` (R-7)

**`CellSample = {games: I, total_runs: N, sum_of_squares: N, mean: F64, variance: F64}`**
- 16개 하프이닝을 게임 단위로 묶어 합계와 제곱합을 다시 계산합니다.
- 평균·분산은 기존 계산식의 binary64 결과를 §8.4 형식으로 표현합니다.

**`SideResult = {side: Batter|Pitcher, verdict: NOT_EVALUATED|DOMINANCE_DETECTED|NO_DOMINANCE_SUPPORTED|INCONCLUSIVE, reason: TEXT1, strategies: StrategyResult[]}`**
- 전략 순서: 타자 `Normal, Patient, Aggressive, TakeFirst`, 투수 `Balanced, Attack, Careful`
- 기존 평가가 전략 결과를 만들지 못했으면 빈 배열이며, 추정 결과를 채우지 않습니다.
- `verdict=NOT_EVALUATED` ⇔ `strategies=[]`. 그 밖에는 타자 4개·투수 3개를 모두 채웁니다(R-7).

**`StrategyResult = {strategy, dominant: bool, not_dominant: bool, label: DOMINANT|NOT_DOMINANT|UNDETERMINED, witnesses: Witness[]}`**
- `label`: dominant이면 `DOMINANT`, 아니고 not_dominant이면 `NOT_DOMINANT`, 둘 다 아니면 `UNDETERMINED` (R-7)

**`Witness`:** `rival`, `column`(기존 비교 열 ID), `strategy_cell`, `rival_cell`, `difference: F64`, `standard_error: F64`, `lower_bound: F64`, `superior: bool`, `non_superiority_z: F64`, `holm_critical: F64`, `holm_rejected: bool`
- **배열 순서는 기존 알고리즘의 생성 순서**(= `DominanceEvaluation.Canonical` 순서)입니다(P-10). 즉 rival enum 순서 → hand(`Same`, `Opposite`) → mode(`Automated`, `NonAutomated`) → 반대축 enum 순서입니다(S00.5 F-12 Errata). 동일한 (rival, column) 키의 중복은 거부합니다.
- **F64 상태 (R-7):** CellSample의 `mean`·`variance`와 Witness의 `difference`·`standard_error`·`lower_bound`·`non_superiority_z`는 **FINITE만** 허용합니다. `holm_critical`은 FINITE 또는 `NOT_APPLICABLE/HOLM_STOPPED`이며, `NOT_APPLICABLE`이면 `holm_rejected=false`입니다.
- Holm의 정렬·동률 처리·원래 비교 인덱스 대응은 기존 알고리즘을 유지합니다.
- 비교 수: 타자 전략당 36개, 투수 전략당 32개
- Holm 중단 이후 critical 값은 `{"state":"NOT_APPLICABLE","value":null,"reason":"HOLM_STOPPED"}`입니다.
- 정의되지 않은 값을 숫자 0으로 대체하지 않습니다. Metrics 실패·미동결 표본·누락 Cell에 대한 기존 Gate 우선순위를 유지합니다.
- **F64 이유 코드 v1 닫힌 목록: `{HOLM_STOPPED}`** (P-10/R-7). `UNDEFINED` 상태는 예약하되 v1에서는 거부합니다(S00.5 F-13 Errata).

---

## §5. Unit PZL Schema (S00 + P-04·P-11)

**Root:** `schema, execution, approvals, plan, registered_streams, games, checks, additional_evidence`

| 경로 | 타입·조건 |
|---|---|
| `plan.unit_id` | 부모 Unit ID |
| `plan.config_hash` | 기존 `H` |
| `plan.run_result_version` | 기존 버전 문자열? (부모와 일치해야 하므로 P-07에 따라 사실상 non-null) |
| `plan.run_result_hash` | 기존 `H?` (위와 같음) |
| `registered_streams` | 고정 7개, 이 순서: `game/ai@1, game/contact@1, game/field@1, game/location@1, game/pitch@1, game/runner@1, game/target@1` |
| `games` | `PzlGame[]`, **실행된 모든 경기**, 부모 실행 순서 (P-11) |
| `checks` | `Check[4]`, 이 순서: `pzl-5, pzl-10, pzl-12a, pzl-12b` |

### 5.1 PzlGame (P-11, 닫힌 필드)

`index, game_id, seed: U64, status: Completed|Failed, game_result_hash: H, event_hash: H, rng_hash: H?, committed_events: I, summary: PzlSummary, events_digest: AH, final_rng_canonical: RNGSET1?, observation_present: true`

- `events[]` 필드는 **삭제**되었습니다. 사건 원문은 GAME_EVENTS 번들(§10)에 있습니다.
- `rng_hash`와 `final_rng_canonical`이 null인 것은 §3.6 D행(= §10.4 A 경우)과 동치입니다.
- GameId·Seed·결과 Hash는 이 상위 항목에서 A+에 결속됩니다.
- `observation_present`는 반드시 true입니다. 관찰 누락은 0건 관찰과 다릅니다.

### 5.2 PzlSummary (14필드)

`events, located_events, ef2_receipts, version_mismatches, fingerprint_mismatches, foreign_event_ids, duplicate_event_ids, event_id_collisions, receipt_log_mismatches, called_pitch_steps, framing_inputs, framing_entries, framing_non_neutral: N` 그리고 `drawn_streams: STREAM[]`

- `drawn_streams`는 ordinal 오름차순, 중복 금지이며, 사용한 Stream이 없으면 `[]`입니다(P-04). 개별 Stream ID로 독립 재수집 결과와 비교합니다.
- 구형 `PzlGameObservation.DrawnStreams` CSV(`""` 포함)는 기존 summary·DRAFT를 읽을 때만 `Split(',', RemoveEmptyEntries)`로 해석하고, 신규 문서에는 쓰지 않습니다.

### 5.3 EventObservation (GAME_EVENTS 사건 행)

| 필드 | 타입 |
|---|---|
| `sequence` | 정수 `1..999999` |
| `event_id` | GameId·Sequence의 정규 EventId |
| `kind` | `approach, pitch, take, in_play, runner_play, substitution` 중 하나 (EventFingerprint.cs:38-47) |
| `canonical_event` | `EVCANON` (ef1/ef2) |
| `receipt` | `EventReceipt = {event_id, kind, fingerprint_version: ef1\|ef2, fingerprint: H}` |
| `rng_before`, `rng_after` | `RNGSET1` |
| `pitch` | `PitchObservation?` — `pitch`·`take`·`in_play`에서만 non-null |

- `receipt`는 `EventFingerprint.Receipt`로 재계산합니다. ID·종류·버전·지문 전체와 최종 AppliedEvents 순서를 비교합니다.
- RNG를 소비하지 않은 사건은 `rng_before == rng_after`입니다. 연결 규칙은 §10.4를 따릅니다.

### 5.4 PitchObservation

| 필드 | 타입 |
|---|---|
| `branch_contract_version` | 기존 Draft 버전 |
| `branch_kind` | 기존 PitchBranchKind |
| `explicit_take` | bool |
| `intent` | 기존 PitchIntent |
| `target_zone` | 등록 TargetZone ID |
| `location` | Location |
| `prepared` | PreparedObservation |
| `h1_rng_canonical` | `RNGSET1` (C2A 완료 직후 Snapshot) |
| `steps` | `BranchStep[]`, 실제 실행 순서 |
| `stream_usage` | `StreamUse[]`, Stream ordinal 순서 |
| `next_bounded_calls` | `I` |
| `runner_calls` | `I` = `AdvancementAudit.LogicalDraws`, Advancement가 없으면 0 |
| `runner_raw_draws` | `D64` = `AdvancementAudit.RawDraws`, 없으면 `"0"` |

- RNG 대응: `rng_before = H0`, `h1_rng_canonical = H1`, `rng_after = H2`. Advancement가 없으면 H1 = H2입니다.
- 수집 출처(F-20): 커서 전후 Rng, `Draft.NextRng`, `AdvancementDetail`

**`Location = {zone_model_version, pitch_zone_id, applied_batter_side(기존 Left/Right의 등록 표현), boundary_class: Clear|Borderline}`**
- 17구역과 구역·경계 분류 조합은 기존 PitchLocation 규칙으로 검증합니다.

**`PreparedObservation`:**
- `batter_id`, `pitcher_id`, `catcher_id`(실제 현재 포수)
- `player_input_set_id`
- `input_receiving_framing`: 정수 `0..100`, InputSet에서 조회
- `frozen_receiving_framing`: 정수 `0..100` 또는 null, 실제 PreparedPitch 값
- `rules_profile_id`, `rules_version`, `rules_hash: H`, `probability_model_version`, `tuning_version`, `tuning_hash: H`
- `frozen_receiving_framing=null`은 관찰 누락이 아닙니다(Automated처럼 Frozen Framing 입력이 없는 정상 계약). 그러나 **PreparedPitch 자체의 존재는 필수**입니다.

**`BranchStep = {stage: EXACT, condition: EXACT, outcome: EXACT, stream: EXACT, roll: 0..999999, raw_draws: 양수 D64, framing_entries: FramingEntry[]}`**
- `FramingEntry = {slot: "catcher.framing", factor_ppm: N, applied: bool}`, 원래 Trace 순서
- 독립 재수집한 필터 결과와 비교합니다.
- 숨겨진 성향 벡터·개발 정보 같은 내부 선수 데이터는 복사하지 않습니다.

**`StreamUse = {stream: EXACT, calls: I, raw_draws: D64}`**
- 호출 수와 raw Draws는 별개 값입니다. rejection sampling으로 추가된 raw Draws는 오류가 아닙니다.
- Step·StreamUse·H0/H1/H2 상태 변화를 함께 검증합니다.

> 현재의 집계형 PzlRecorder에는 위 사건별 자료가 구현돼 있지 않습니다. 사건별 자료는 B-1 추가 구현 범위입니다.

---

## §6. 공식 PZL #6 Schema

**Root:** `schema, execution, approvals, plan, samples, check, additional_evidence`

| 경로 | 타입 |
|---|---|
| `execution.family` | `fc3-int` |
| `plan.integration_evidence_id` | 같은 검증 집합의 Integration 증거 ID |
| `plan.selection_rule` | `FIRST_10_PER_MODE` |
| `plan.sample_count_per_mode` | 정수 10 |
| `samples` | `RoundTripSample[20]`: 승인 Integration `automated` 계획의 최초 10경기, 이어서 `non-automated`의 최초 10경기 |
| `check` | Check, ID `pzl-6` |

실패·미완료 경기나 검사 실패 경기를 다른 경기로 교체하지 않습니다. 필수 표본을 검증하지 못하면 인증할 수 없습니다.

**`RoundTripSample`:**
- 식별·Hash: `unit_id: automated|non-automated`, `index`, `game_id`, `seed: U64`, `game_result_hash`, `event_hash`, `rng_hash: H`
- 형식·건수: `event_format_version: 2`, `event_count`, `located_event_count`, `ef1_receipts`, `ef2_receipts: I`
- 검사 결과(bool): `event_sequence_equal`, `locations_equal`, `receipts_equal`, `rewrite_bytes_equal`, `final_state_equal`, `event_hash_equal`, `event_replay_match`, `seed_replay_match`

**검증 경로:**
1. 원 사건을 기존 v2 Writer로 직렬화합니다.
2. 기존 Reader로 읽어 사건별 모든 필드를 비교합니다.
3. 읽은 사건을 같은 Writer로 다시 써서 최초 Writer 바이트와 비교합니다.
4. 기존 Reducer로 재생해 최종 상태·기록·Receipt를 비교합니다.
5. 승인 입력과 Seed로 Production 실행을 별도로 재수행합니다.
6. Event·RNG·GameResultHash와 PZL을 독립 비교합니다.

- 이벤트 JSON을 Reducer에 적용한 것만으로는 RNG 재생이 증명되지 않습니다. 그래서 `event_replay_match`와 `seed_replay_match`를 분리합니다.
- 최종 상태는 경기 상태와 공식 기록 전체를 구조적으로 비교합니다. PreviewComparison을 재사용해도 되지만, GameState·Finalized Facts의 필드가 비교에서 빠지면 인증을 거부하는 커버리지 검사가 필요합니다.
- boolean 값은 제출자의 자기 선언이 아니라 독립 검사 결과와 비교합니다.
- Raw JSON SHA는 ArtifactRef와 영수증에만 둡니다.

---

## §7. Check 공통 구조 (S00 + P-14)

적용 경로: Unit PZL `checks[]`, 공식 #6 `check`, Phase Root `checks[]`

| 필드 | 타입 |
|---|---|
| `id` | `pzl-1`~`pzl-11`, `pzl-12a`, `pzl-12b` |
| `status` | `PASS, FAIL, NOT_EVALUATED, NOT_APPLICABLE, DEFERRED` |
| `reason` | `TEXT1` |
| `coverage` | `{completed_games, events, pitches, called_pitch_steps: N}` (의미와 계산 범위는 아래 "coverage 의미와 계산 범위") |
| `violations` | `Violation[]` |
| `test_evidence` | `TestEvidence?` |
| `deferral` | `Deferral?` |

**`Violation = {code: TEXT1, game_id: TEXT1?, event_id: TEXT1?, field: TEXT1?}`**
- `(game_id, event_id, field, code)` ordinal 순서이며 null이 먼저 옵니다. 동일 조합 중복은 거부합니다.

**`TestEvidence = {profile_id: ID, profile_sha256: H, source_git_sha: GIT, cases: [{id: CASE_ID, status: "PASS"|"FAIL"}]}`** (P-14)
- `cases`는 id ordinal 오름차순, 중복 금지입니다. `SKIP`과 `reason`은 삭제되었습니다.
- 실행 시간·환경 정보는 넣지 않습니다.

**`Deferral = {code: "DEFERRED_TO_PHASE_5_6_28", bcl_phases: [5,6,28], lmv_a_phases: [5,28]}`**
- `pzl-12b`에서만 non-null이며, 12b는 항상 `DEFERRED`입니다. 다른 항목은 `deferral=null`입니다.

정적 계약이나 음성·양성 대조가 필요한 항목은 `test_evidence`가 필수입니다. 실행 관찰만으로 판정하는 개별 Unit 검사는 null을 허용합니다.

**`test_evidence` 필수표와 허용 status (R-9):**

| 문서 | Check | status 허용 | test_evidence |
|---|---|---|---|
| Unit PZL | pzl-5, pzl-12a | PASS / FAIL / NOT_EVALUATED | null |
| Unit PZL | pzl-10 | Automated: PASS/FAIL/NOT_EVALUATED · NonAutomated: NOT_APPLICABLE | null |
| Unit PZL | pzl-12b | DEFERRED | null, `deferral` non-null |
| Phase Root | #1, #2, #3, #5 | PASS / FAIL | null (관찰 기반) |
| Phase Root | #4, #7, #8, #9, #11 | PASS / FAIL | **필수** |
| Phase Root | #6 | PASS / FAIL | **필수** (Legacy 코퍼스) + PZL6 문서 |
| Phase Root | #10 | PASS / FAIL | **필수** (NonAutomated 양성 대조) |
| Phase Root | #12a | PASS / FAIL | **필수** (등록 집합 = 고정 집합) |
| Phase Root | #12b | DEFERRED | null, `deferral` non-null |

증거 유형과 판정 출처는 §11.2(S01 §4.3, R-9 유지), 필수 Case는 §12.2에서 정합니다.

**coverage 의미와 계산 범위 (NSC-6 OWNER 결정, TASK.md 2026-10-24)**

출처 구분:
- `completed_games`·`events`: S00.5 R-8의 정의를 P-11 이후에도 유지되는 **잔여 의미 규범으로 OWNER가 명시 채택**했습니다.
- `pitches`·`called_pitch_steps`의 의미와 아래 범위 규칙: **신규 OWNER 결정**입니다.
- 필드 타입(`N`)은 S00 §2.7 그대로입니다.

coverage는 해당 Check에 지정된 증거 범위의 관찰 건수입니다. Check의 PASS 기여 건수와 반드시 같지는 않습니다.

| 필드 | 세는 것 | Failed 경기 |
|---|---|---|
| `completed_games` | 해당 범위에서 `status=Completed`인 경기 수 | 세지 않음 |
| `events` | 해당 범위에서 관찰된 모든 확정 사건(EventObservation) 수 | **포함** (Failed 경기의 확정 Commit 포함) |
| `pitches` | 해당 범위에서 `pitch != null`인 EventObservation 수 (`pitch`·`take`·`in_play`) | **포함** |
| `called_pitch_steps` | 실제 관찰된 BranchStep 중 기존 `StageIds.CalledPitch`에 해당하는 Step 수 | **포함** |

`coverage.pitches`는 Completed 경기만 집계하는 `Technical.counters.pitches`와 항상 같아야 한다고 요구하지 않습니다.

계산 범위:

| 문서·Check | 범위 |
|---|---|
| Unit PZL `pzl-5`, `pzl-10`, `pzl-12a` | 해당 Unit의 실행된 모든 경기 |
| NonAutomated Unit의 `pzl-10` | status는 NOT_APPLICABLE이지만 Unit 증거 범위의 coverage를 기록 |
| Unit PZL `pzl-12b` | 네 필드 모두 0 |
| PZL6 `pzl-6` | 승인된 고정 20경기 표본 |
| Phase Root #1·#2·#3·#5·#12a | 전체 54개 Unit의 실행 경기 |
| Phase Root #10 | Automated Unit만 |
| Phase Root #6 | PZL6 고정 20경기 |
| Phase Root #4·#7·#8·#9·#11, #12b | 네 필드 모두 0 |

- TEST_PROFILE의 Case 건수는 `test_evidence.cases`에서 관리합니다.
- 관찰과 Test Profile을 함께 쓰는 Check는 관찰 증거 부분만 coverage에 반영합니다.

재검산:
- Unit의 Completed 경기 수, 확정 사건 수, 투구 사건 수, called-pitch Step 수를 각각 독립 재관찰 결과와 비교합니다.
- PZL6와 Phase Root는 각 대상 범위의 경기별 관찰 값을 중복 없이 집계합니다.
- 불일치는 기존 오류 체계에 따라 거부합니다(PZL 값 불일치 → `Evidence.pzl_mismatch`). 기존 Hash·RNG·Replay 계산식은 바꾸지 않습니다.

0건과 Gate:
- 기존 Unit PZL 판정 규칙을 유지합니다. `pzl-5`는 `events=0`이면 NOT_EVALUATED, Automated `pzl-10`은 `called_pitch_steps=0`이면 NOT_EVALUATED입니다.
- **Phase Root PASS에 coverage 필드 0을 금지하는 신규 조건은 추가하지 않습니다.**
- PASS/FAIL/NOT_EVALUATED는 기존에 확정된 PZL·Gate 규칙으로만 재계산합니다.

---

## §8. Canonicalization 규격 (S00 §3)

### 8.1 JSON 읽기 (신규 4Schema와 모든 신규 형식)

- UTF-8, BOM 없음
- 중복 키는 덮어쓰기형 DOM에 저장하기 전에 거부
- 불량 UTF-8, 고립 surrogate, 뒤에 붙은 추가 JSON 값은 거부
- NFC/NFD 변환, Trim, 개행 치환, 대소문자 변환 금지
- 정수 토큰은 `0|[1-9][0-9]*`이며, 정수 필드에서 `1.0`, `1e0`, `-0`은 거부
- 동등한 JSON escape는 파싱 후 같은 문자열로 취급
- 객체 키는 JCS 순서, 배열은 본 명세의 의미 순서
- JCS의 문자열·UTF-16 키 정렬·UTF-8 출력은 RFC 8785 §3.2를 규범으로 합니다.
- 기존 Seed·Manifest·Legacy Reader는 바꾸지 않습니다.

### 8.2 UInt64 Seed

- 문법: `0 | [1-9][0-9]{0,19}`이고, 값이 `18446744073709551615` 이하여야 합니다.
- 거부 예: `"00" "01" "+1" "-0" " 1" "1 " "1e3" "1.0" "18446744073709551616"`
- 기존 Seed 파일의 JSON 숫자는 기존 Reader로 정확한 UInt64로 읽고, 새 증거에서만 정규 문자열로 투영합니다. **기존 Seed 파일을 문자열 형식으로 다시 쓰지 않습니다.**

### 8.3 일반 정수

- JSON 수치 상한은 `2^53−1`이고, 필드별 I/P/index/PPM 상한을 추가로 적용합니다.
- 계산 결과가 증거 상한을 넘으면 거부합니다. 반올림·clamp·double 경유 변환으로 억지 수용하지 않습니다.
- 상한을 늘려야 하면 새 Schema 버전에서 정의합니다.
- RNG Draws는 기존 Int64 상한을 보존하기 위해 `D64` 문자열로 표현합니다. rngset1 형식은 바꾸지 않습니다.

### 8.4 binary64 (`F64 = {state, value, reason}`)

| 필드 | 정의 |
|---|---|
| `state` | `FINITE` \| `UNDEFINED` \| `NOT_APPLICABLE` |
| `value` | FINITE이면 정규 십진 **문자열**, 그 밖에는 null |
| `reason` | FINITE이면 null, 그 밖에는 등록된 이유 코드 (v1 닫힌 목록: `{HOLM_STOPPED}`. `UNDEFINED`는 예약 상태이며 v1에서 거부. S00 예시 `UNDEFINED/INSUFFICIENT_GAMES`는 공식 경로에서 나오지 않음) |

**정규 문자열 생성 절차:**
1. 기존 계산 결과의 IEEE754 비트열을 입력으로 씁니다.
2. 비유한 값을 정상 수치로 바꾸지 않습니다.
3. `+0`과 `−0`은 모두 `"0"`입니다.
4. RFC 8785가 참조하는 ECMAScript Number-to-string과 Note 2의 최단 왕복 표현을 적용합니다.
5. 최소 자릿수 후보가 여럿이면 원값에 가장 가까운 것을 고릅니다.
6. 거리가 같으면 짝수 유효숫자를 고릅니다.
7. 절댓값이 `10^-6 이상 10^21 미만`이면 일반 표기를 씁니다.
8. 그 밖에는 소문자 `e`를 쓰고 양의 지수에 `+`를 붙입니다.
9. 불필요한 선행 0·후행 0·지수 선행 0을 쓰지 않습니다.
10. 결과를 JSON 문자열로 저장한 뒤 JCS escape를 적용합니다.

| 값·비트 | `value` |
|---|---|
| `−0` | `"0"` |
| 최소 양수 subnormal | `"5e-324"` |
| 최대 유한값 | `"1.7976931348623157e+308"` |
| `10^-6` / `10^-7` / `10^21` | `"0.000001"` / `"1e-7"` / `"1e+21"` |
| 비트 `43143ff3c1cb0959` | `"1424953923781206.2"` |

- 수치 규범은 RFC 8785 §3.2.2.3·Appendix B에 고정하며, 향후 ECMAScript 변경을 자동으로 따르지 않습니다.
- `.NET ToString("R")`이 이 계약을 만족한다고 가정하지 않습니다. 적합 변환을 구현하고 Golden으로 비교합니다.
- 값 문자열 `"NaN"`, `"Infinity"`, `"-Infinity"`는 거부합니다.

### 8.5 기존 계산·Hash 불변

다음은 바꾸지 않습니다: 정수 PPM half-up, F1/F8 층화식, Welch/IUT/Holm 식과 비교 절차, 기존 Hash 입력 문자열, 기존 Dominance Canonical의 `"R"` 표현, 기존 ResultHash·ConfigHash·Attestation Hash.

새 A+만 위 정규 표현을 씁니다. 미정의 값의 상태화는 증거 표현일 뿐이며 Gate 변경이 아닙니다.

---

## §9. A+ Hash Payload·증거 그래프 (S00 §4)

### 9.1 의미 투영 `M(D)`

- 검증된 문서에서 `additional_evidence.artifacts` 속성**만** 제거합니다. 하위 증거에도 개별 적용합니다.
- 물리 문서에 다음을 추가하지 않습니다: 자신의 A+, 자신의 Raw SHA, 자신의 영수증 참조, 자기 선언형 `official_evidence`, DRAFT 승격 플래그.

### 9.2 Payload

| 대상 | 정확한 속성과 값 |
|---|---|
| Calibration | `hash_contract="simharness-evidence-hash-v1"`, `result=M(Cal)`, `pzl_units=[M(L1),M(L2),M(L3),M(L4)]`, `pzl6=null` |
| Integration | 같은 4속성. `result=M(Int)`, `pzl_units=[M(automated),M(non-automated)]`, `pzl6=M(공식 20경기 #6)` |
| Dominance | `hash_contract`, `result=M(Dom)`, `pzl_units`=`plan.cell_ids` 순서의 48개 `M(Unit PZL)` (`pzl6` 속성 없음) |
| Phase Root | `hash_contract`, `contracts`(5-tuple), `validation_set_id`, `approvals`(승인 ID·승인 Raw SHA·Freeze ID), `children`, `checks`(#1~#11·#12a·#12b, 13개) |

**결속 조건:**
- PZL 문서는 부모의 ID·Unit·승인·실행·ConfigHash·RunResultHash와 일치해야 합니다.
- #6은 Integration ID·실행·승인·20경기 식별·순서가 일치해야 합니다.

**`children`:**
- 요소: `{family, run_id, evidence_id, evidence_hash: AH}`, 순서는 Calibration → Integration → Dominance
- 외부 승인은 `validation_set_id → 3개 승인 run_id → Family별 승인 Unit·Seed 계획·Pin → Freeze·계약 조합·빌드·평가 규칙`의 대응을 제공해야 합니다.
- 세 자식이 같은 검증 집합에 속하고 각각 등록된 독립 영수증이 있어야 합니다. Hash·Family·RunId·EvidenceId가 모두 일치해야 합니다.
- 같은 Git SHA만으로는 같은 검증 집합으로 인정하지 않습니다. 자식 누락·중복·역할 교체는 거부합니다. 자식 영수증은 Phase Root를 참조하지 않습니다.

### 9.3 바이트 결합식

`C = JCS(Payload)`의 UTF-8 바이트(BOM 없음, 끝 개행 없음)입니다.

```
Calibration = SHA256(UTF8("STARPLAYER-APLUS-FAMILY-V1"    + LF) || C_cal)
Integration = SHA256(UTF8("STARPLAYER-APLUS-FAMILY-V1"    + LF) || C_int)
Dominance   = SHA256(UTF8("STARPLAYER-APLUS-DOMINANCE-V1" + LF) || C_dom)
Phase Root  = SHA256(UTF8("STARPLAYER-APLUS-PHASE-V1"     + LF) || C_phase)
```

- `LF`는 실제 바이트 `0x0A`입니다. 길이 prefix, NUL, 추가 구분자는 넣지 않습니다.
- 표기는 `sha256:<소문자 64자리>`입니다. Calibration과 Integration은 Payload 안의 Family 값으로 구별됩니다.

### 9.4 결속 대상 구분

| 데이터 | 의미 A+ | Raw SHA·영수증 |
|---|---|---|
| 승인 Seed Pin·ConfigHash·ResultHash·Attestation | 포함 | 원본도 보존 |
| 기술 실행 사실·공식 Metrics·Gate·Reason | 포함 | 원본도 보존 |
| 필수 PZL 관찰·독립 비교 결과 (`events_digest` 포함) | 포함 | 원본도 보존 |
| 일반 Manifest·summary·로그 파일 SHA, GAME_EVENTS 번들 Raw SHA·경로·바이트 수 | 제외 | 필수 |
| 시간·OS·경과 시간·메모리·디버그 | 제외 | 채택한 원본에서 보존 |
| 검증기 식별·영수증·등록 기록 | 제외 | 별도 결속 |
| DEFF 등 비인증 진단 | 제외 | Raw 진단 보존 |

의미가 같고 바이트가 다른 원본은 같은 A+에 다른 Raw 목록을 가진 **별개 영수증**입니다.

### 9.5 결속 5분류 (R-2)

| 분류 | 필드 | A+ | 결속 경로 |
|---|---|---|---|
| ① 승인 입력의 논리 식별 | validation_set_id, run_id, family, approval_record_id, freeze_id, build_provenance_id, competition_key, cohort_id, policy_id, rules_profile_id, **attestation.seed_manifest_path**(승인 Pin 사전의 키, 저장소 기준 상대 경로) | 포함 | 직접 |
| ② 기존 Attestation Hash에 결속된 값 | Attestation 21필드 전체 | 포함 | 직접 + `attestation_hash` 재계산 (간접 결속은 중복이지만 유지) |
| ③ 승인 원본 Pin | `attestation.seed_manifest_sha256`, `approvals.approval_record_sha256` | 포함 | 직접. SEED_MANIFEST artifact의 `raw_sha256`과 같아야 함 |
| ④ 일반 Archive 경로·Raw SHA | ArtifactRef 7필드, Unit manifest·summary·로그 SHA | 제외 | 영수증 `source_files` |
| ⑤ 비인증 환경·시간·진단 | `environment_not_hashed`, elapsed, Failure.message, Wilson·SE·DEFF·batch, Family의 Regulation | 제외 | Raw 보존만 |

- **동일성 범위:** A+가 같다는 것은 "같은 승인 입력(Seed 경로 키 포함)에서 나온 같은 의미 결과"라는 뜻입니다. 저장 위치나 원본 바이트가 같다는 뜻은 아닙니다.
- A+는 **Archive 경로와는 무관**하지만 **승인 Seed 경로 키에는 의존**합니다. 같은 바이트의 Seed 파일이라도 다른 경로로 승인되면 Attestation Hash와 A+가 모두 달라지며, 이것은 의도된 동작입니다(S00.5 F-02).

---

## §10. GAME_EVENTS Bundle v1 (S01 §3 + PATCH-06, OD-2)

### 10.1 직렬화

- 파일 하나가 Unit PZL 문서 하나에 대응합니다(Archive 역할 `GAME_EVENTS`, Unit PZL마다 정확히 1개).
- 내용은 `레코드 := JCS(JSON 객체) || 0x0A`의 연속입니다. UTF-8, BOM·CR·빈 행이 없고, 마지막 바이트는 `0x0A`이며 그 뒤에 아무것도 없습니다.
- 레코드 하나는 1 MiB 이하이며, 넘으면 `archive_invalid`입니다.
- JCS가 문자열 안의 LF를 `\n`으로 escape하므로 행 경계가 모호해지지 않습니다.

### 10.2 레코드 순서

```
1행          Header
실행 경기마다 Game 1행 + EventObservation 정확히 committed_events행 (sequence 1..n)
마지막 행    End
```

```
Header: {"bundle":"simharness-game-events-v1","contracts":{5-tuple VER},"family":…,"run_id":RUN,
         "unit_evidence_id":ID,"unit_id":…,"validation_set_id":ID}
Game:   {"committed_events":I,"events_digest":AH,"game_id":…,"index":P,"record":"game","seed":U64,
         "status":"Completed"|"Failed"}
사건 행: §5.3 EventObservation의 JCS 바이트 그대로 (래퍼·record 키 없음)
End:    {"executed_games":I,"record":"end","total_events":N}
```

### 10.3 결속 규칙

- Header의 `family, run_id, validation_set_id, unit_id, unit_evidence_id, contracts`는 대응 Unit PZL 문서와 일치해야 합니다(§2.4 S3·S5·S6).
- Game 레코드는 계획 순서대로 `index = 1..executed_games`가 연속이어야 하며, `game_id`·`seed`는 승인 Seed 계획과 같아야 합니다.
- Game 레코드 수는 `End.executed_games`, Unit PZL `games` 길이, `technical.games_executed`와 모두 같아야 합니다.
- **실패 경기의 확정 사건도 포함합니다**(실패 전에 Publish된 모든 Commit).
- `sequence`는 연속이어야 하고(누락·중복 금지), `event_id`는 정규 EventId여야 하며, `receipt`는 재계산 값과 같아야 합니다.

### 10.4 RNG 경계 (PATCH-06, 분기는 `committed_events`와 RNG 존재 여부로 결정하며 `stage` 문자열만으로 정하지 않음)

| 필드 | A. 시작 전 실패 | B. 시작 후 첫 Commit 전 실패 | C. Commit ≥ 1 |
|---|---|---|---|
| `status` | Failed | Failed | Completed 또는 Failed |
| `failure.stage` | `config`, `start` | `advance`, `exception` (`budget`은 예산 ≥ 1이므로 불가) | §3.6 A/B/C행 |
| `committed_events` | 0 | 0 | ≥ 1 |
| 번들 사건 행 | 0 | 0 | = `committed_events` |
| `events_digest` | 빈 배열 digest | 빈 배열 digest | 재관찰 배열 digest |
| `rng_hash` / `failure.rng_hash` | null / null | H / H (같은 값) | H |
| `final_rng_canonical` | null | `RNGSET1` 헤더 1행(스트림 행 0개) | `RNGSET1` |
| `summary.drawn_streams` | `[]` | `[]` | 실제 스트림 |
| game-result-v1 재계산의 rng 입력 | `"-"` (P-05) | 그 H | 그 H |

- **B 검증:** 검증기는 `GameStarter.Begin(new GameStart(game_id, rules, away, home, execution), seed)` → `cursor.Current.Rng.Canonical()`로 초기 스냅샷을 만듭니다. 이것이 `final_rng_canonical`과 바이트까지 같고, 그 SHA-256이 `rng_hash`·`failure.rng_hash`와 같아야 합니다. RNG 타입을 직접 생성하지 않고 이 Production 경로만 씁니다.
- **C 검증:** 첫 `rng_before`는 B와 같은 방법으로 재구성한 초기 스냅샷과 같아야 합니다. 각 `rng_after`는 다음 `rng_before`와 같아야 합니다. 마지막 `rng_after`는 `final_rng_canonical`과 같고, 그 SHA-256은 `rng_hash`와 같아야 합니다. 실패 경기도 마지막 Publish 이후 RNG가 바뀌지 않으므로 같은 규칙을 적용합니다.
- **같은 빈 digest 허용:** 여러 경기의 빈 배열 digest가 같아도 정상입니다. 경기 식별은 `index, game_id, seed, game_result_hash, event_hash, rng_hash`가 담당합니다.
- **코드 근거:**
  - GameExecutor.cs:28-43: 시작 전 실패는 커서가 null이므로 RNG가 `"-"`이고 관찰이 비어 있습니다.
  - GameStarter.cs:17-26: 아무것도 commit·draw·persist하지 않으며, context key는 GameId입니다.
  - GameRandom.cs:69-71: 스냅샷에는 `Draws>0`인 스트림만 들어갑니다.
  - GameCursor: 커서 RNG는 Publish할 때만 바뀝니다.
  - GameExecutor.cs:58-62: Publish 직후, 실패 판정보다 먼저 관찰합니다.

### 10.5 경기별 Digest

```
events_digest = "sha256:" + hex(SHA256(ASCII("STARPLAYER-APLUS-PZL-EVENTS-V1") || 0x0A || UTF8(JCS(EventObservation[]))))
```

- 배열의 JCS는 `"[" + 각 원소 JCS를 ","로 연결 + "]"`이므로 스트리밍으로 계산할 수 있습니다.
- 빈 배열 Golden: `sha256:ed666f17bb1a25a68a4457864105ec21fb879a61b7c3919422e1918b14e2ed6e`

### 10.6 독립 검증 (메모리 상한: 경기 1개 분량, 이벤트 예산 3,000 이하)

1. 한 번 읽은 스냅샷 바이트로 번들 Raw SHA를 계산합니다.
2. 승인 입력으로 해당 Unit을 재실행하며 관찰을 수집합니다. 경기가 끝날 때마다 다음을 수행합니다.
   - (a) 재관찰한 각 사건의 JCS를 번들의 다음 사건 행과 **바이트 비교**합니다.
   - (b) 재관찰 결과로 digest를 다시 계산해 번들 Game 레코드·Unit PZL의 `events_digest`와 대조합니다.
   - (c) Game 레코드 필드를 대조합니다.
3. 제출된 번들만 Hash해서 통과시키는 경로는 없습니다. 재실행이 없으면 검증도 없습니다.

불일치 분류: 관찰 누락·건수 부족은 `pzl_incomplete`, 값 차이는 `pzl_mismatch`, 형식 위반은 `archive_invalid`입니다.

### 10.7 Null 복원 (P-05)

- `"-"` 복원 대상은 game-result-v1 재계산 입력의 `outcome`, `rng_hash`, `error_code` **셋뿐**입니다. 이 값이 null이면 `"-"`로 바꿉니다(GameExecutor.cs:191-195).
- 투영 방향: `GameRunResult.RngHash=="-"`이거나 `RunFailure.RngHash=="-"`일 때만 신규 문서에서 null입니다.
- 기존 Manifest `results.games[].rng_hash`와 비교할 때만 null과 `"-"`를 대응시킵니다. 그 밖의 null→`"-"` 변환은 금지합니다.

---

## §11. PZL 증거 매핑·독립 재수집

### 11.1 12항목 매핑 (#12는 ADR-008에 따라 #12a·#12b로 분리)

| 항목 | 필수 증거·독립 검증 |
|---|---|
| #1 동일 Seed 재현 | 모든 완료 경기의 신규 Production Replay. 사건·위치·H0/H1/H2·최종 RNG·기록 비교 |
| #2 모든 실제 투구에 위치 | 모든 Pitch/Take/InPlay의 유효 위치, 관찰 투구 수와 Counters 비교 |
| #3 인플레이 중복 집계 금지 | InPlay 1건 = 투구 1개인지, 사건 계수와 투수 BoxScore 비교 |
| #4 ef1 불변 | 기존 Golden, 30,974개 영수증, legacy writer 바이트 |
| #5 ef2 식별·충돌 | 전체 Commit의 Canonical·Receipt·Sequence·ID 중복·충돌 |
| #6 JSON·Replay | Legacy 코퍼스 + 공식 Integration 최초 20경기 |
| #7 신규 형식 위치 누락 거부 | 누락·null·잘못된 모델 등 Reader 음성 검사 |
| #8 중립 성향 무효과 | 같은 입력·roll에서 성향 없음과 중립 성향의 분포·Draft 비교 |
| #9 단조성·상한 | 승인 모델별 단독·경계·결합 검사, 해당 버전의 기존 cap 사용 |
| #10 ABS Framing 0 | 전체 PreparedPitch/Trace 관찰, Automated 전 범위, NonAutomated 양성 대조 |
| #11 Legacy RuleSnapshot Hash | 기존 프로필·fixture·Pin을 승인 검사 프로파일에 명시하고 재검증 |
| #12a 현재 RNG 경계 | 등록 7개 Stream과 완전 일치, 모든 Draft·Advancement·최종 Snapshot에 외부 Stream 없음 |
| #12b 실제 BCL/LMV | DEFERRED. 실행·통과로 기록 금지 |

### 11.2 Check별 승인 증거 유형 (R-9)

| Check | 증거 유형 | 판정 출처 |
|---|---|---|
| #1 | OBSERVATION | Family·Dominance replay MATCH, 모든 경기 `events_digest` 재관찰 일치 |
| #2 | OBSERVATION | 모든 pitch·take·in_play의 Location 유효, `unlocated_pitches=0`, `located_pitches=pitches` |
| #3 | OBSERVATION | CrossCheck `Pitches` 항목, in_play 1건 = 투구 1개 |
| #5 | OBSERVATION | 모든 Unit PZL의 `pzl-5` PASS |
| #4, #7, #8, #9, #11 | TEST_PROFILE | §12.2 |
| #6 | PZL6_DOCUMENT + TEST_PROFILE | 공식 20경기 문서 + §12.2 |
| #10 | OBSERVATION + TEST_PROFILE | Automated Unit의 `pzl-10` PASS(NonAutomated는 NOT_APPLICABLE) + §12.2 |
| #12a | OBSERVATION + TEST_PROFILE | 모든 Unit의 `pzl-12a` PASS + §12.2 |
| #12b | DEFERRED | `deferral` 고정, PASS 금지 |

테스트가 존재한다는 사실만으로 PASS로 두지 않습니다.

### 11.3 독립 재수집 절차

1. 외부 승인에서 전체 Unit·Seed 계획을 도출합니다.
2. 원본을 안전한 읽기 스냅샷으로 고정합니다.
3. 첫 경기 실행 전에 전체 Unit의 Pin·계획·Attestation·ConfigHash를 검증합니다.
4. 신규 Production 실행에 독립 Observer를 연결합니다.
5. Publish 성공 후 CommitRecord와 해당 투구의 실제 PreparedPitch를 수집합니다.
6. 사건별로 비교한 뒤 PZL 집계를 다시 계산합니다.
7. 최종 Receipt log와 RNG를 재비교합니다.
8. 승인된 공식 #6 고정 20경기를 검증합니다.

- Observer는 경기 결과를 결정하지 않고 게임 RNG를 소비하지 않습니다.
- SimHarness가 Infrastructure를 직접 참조하도록 계층 경계를 바꾸지 않습니다. #6 JSON 검증은 Infrastructure 측이나 독립 검증 도구에서 수행합니다.
- 진단용 `Pzl ?? PzlGameObservation.None` 처리는 공식 검증에 쓸 수 없습니다(PzlChecks.cs:133).

---

## §12. PZL Test Profile v1 (S01 §4 + PATCH-04·P-14)

### 12.1 `simharness-pzl-test-profile-v1` (OWNER가 `PZL_TEST_PROFILE`로 등록, 닫힌 Schema)

```
format, profile_id: ID,
source_git_sha: GIT            (== ApprovalRecord.git_commit_sha),
build_provenance_id: ID        (== ApprovalRecord.build_provenance.id),
runner: { dotnet_sdk:"8.0.425", test_sdk:"17.14.1", xunit:"2.9.3", xunit_runner:"2.8.2" },
fixtures: [{ path: RELPATH, git_blob_sha256: H, byte_length: N }],
checks:   [{ check_id, cases: [CASE_ID] }]   순서 pzl-4, pzl-6, pzl-7, pzl-8, pzl-9, pzl-10, pzl-11, pzl-12a
```

- `runner` 값은 global.json과 `Directory.Packages.props`의 현재 값입니다.
- **Fixture 집합(닫힘):** `source_git_sha` 트리에서 추적되는 `tests/Fixtures/**`와 `content/**`의 모든 파일입니다. path ordinal 오름차순이며, SHA는 `git cat-file blob` 바이트 기준입니다.
- **CASE_ID 문법:** `{테스트 프로젝트명}|{xUnit 2.9.3 표시 이름}`
  - Fact: `{네임스페이스}.{클래스}.{메서드}`
  - Theory: 위 이름 뒤에 `({param}: {값}, …)`. 문자열은 큰따옴표와 C# escape, 정수는 10진수
  - 동결할 때 승인 환경의 `--list-tests` 출력과 집합이 정확히 같아야 등록합니다.

### 12.2 필수 Case

약어: Infra=StarPlayer.Infrastructure.Tests, Sim=StarPlayer.Simulation.Tests, Harn=StarPlayer.SimHarness.Tests, Dom=StarPlayer.Domain.Tests. `…`는 바로 앞 클래스와 같다는 뜻입니다.

| Check | 필수 Case |
|---|---|
| pzl-4 | Infra `Ef1CorpusPinTests.Every_receipt_of_the_hundred_phase_2_games_is_byte_identical_to_the_one_before_ef2_existed`, `Ef1CorpusPinTests.The_legacy_event_writer_produces_the_same_bytes_for_the_hundred_phase_2_games_as_before_event_format_2_existed`, `EventJsonV2Tests.Receipts_of_both_fingerprint_versions_are_kept_as_written` · Sim `PitchLocationEventTests.An_event_with_a_location_is_ef2_and_every_other_event_stays_ef1`, `PitchLocationEventTests.The_ef2_text_is_the_whole_ef1_content_plus_the_four_location_fields_in_a_fixed_order` |
| pzl-6 | Infra `EventJsonV2Tests.A_version_2_event_list_round_trips_with_every_location_and_the_same_receipts`, `…A_version_2_game_replays_to_the_same_state_and_mixed_receipts_survive_their_own_json`, `…Only_format_version_2_is_read_as_version_2_and_a_legacy_array_is_never_promoted`, `…The_legacy_reader_reads_bare_arrays_gives_them_no_location_and_refuses_both_a_location_member_and_a_version_2_envelope`, `PzlJsonRoundTripReplayTests.A_located_engine_game_cannot_be_written_in_the_legacy_format_and_its_version_2_json_is_never_read_as_legacy`, `PzlJsonRoundTripReplayTests.Ten_engine_games_of_each_mode_round_trip…` ×2 [(`"test-pzl-automated"`, `"fc2-dry-cal-va"`, 900101), (`"test-pzl-non-automated"`, `"fc2-dry-cal-vn"`, 900151)] |
| pzl-7 | Infra `EventJsonV2Tests.A_version_2_pitch_event_without_a_location_is_an_error` ×4 [`"p1"`,`"p2"`,`"i1"`,`"t1"`], `…A_version_2_pitch_event_with_a_null_location_is_an_error` ×3 [`"p1"`,`"i1"`,`"t1"`], `…An_unsupported_zone_model_an_unknown_zone_a_bad_side_and_a_wrong_class_are_each_rejected_without_correction`, `…A_location_member_that_is_missing_or_unknown_is_a_json_error`, `…A_non_pitch_event_neither_needs_nor_accepts_a_location`, `…The_legacy_writer_refuses_an_event_with_a_location_instead_of_dropping_it_and_the_version_2_writer_refuses_a_pitch_without_one` · Sim `PitchLocationEventTests.A_location_cannot_be_built_with_an_unknown_model_a_wrong_class_an_unknown_zone_or_an_invalid_side` |
| pzl-8 | Infra `PitchBranchTendencyTests.No_tendency_and_the_explicit_neutral_vector_give_the_same_draft_and_the_same_distributions`, `…A_neutral_tendency_moves_no_probability_in_any_stage`, `…A_tendency_consumes_no_random_number_and_never_changes_a_roll` |
| pzl-9 | Infra `PitchBranchTendencyTests.Swing_result_and_batted_ball_shift_the_favourable_mass_by_exactly_value_times_cap_over_100_and_no_other_stage_moves`, `…The_shift_is_monotonic_in_the_tendency_value_for_the_same_pitch` |
| pzl-10 | Infra `FramingScopeMatrixTests.The_oracle_covers_every_zone_mode_and_rating_of_the_approved_matrix`, `…Every_oracle_case_equals_the_production_distribution_the_applied_flag_and_the_effective_factor`, `…Automated_never_applies_framing_at_any_rating_in_any_zone`, `…Non_automated_clear_zones_never_apply_framing_and_borderline_zones_do` (양성 대조), `…Neutral_framing_and_no_framing_input_give_the_baseline_in_every_condition_and_the_same_trace_class` · Harn `PzlCheckTests.Pzl10_an_automated_run_has_no_non_neutral_framing_factor_while_the_non_automated_control_shows_the_counter_working`, `PzlCheckTests.Pzl10_a_non_neutral_factor_fails_an_automated_run_and_no_called_pitch_is_not_evaluated` · Dom `GameRulesCalledPitchModeTests.Automated_mode_means_framing_cannot_change_a_call_and_non_automated_allows_it_within_later_caps` |
| pzl-11 | Dom `GameRulesCalledPitchModeTests.The_legacy_content_hash_is_the_value_computed_before_the_pitch_zone_fields_existed`, `…A_legacy_snapshot_does_not_declare_a_called_pitch_mode_or_zone_model_and_says_so`, `…Adding_the_new_fields_leaves_every_other_rule_value_identical_to_the_legacy_snapshot` · Infra `GameRulesV2ProfileTests.Every_legacy_profile_keeps_its_independently_computed_content_hash`, `GameRulesV2ProfileTests.Legacy_profiles_declare_no_called_pitch_mode_including_the_kbo_oriented_one_and_are_schema_v1`, `Phase3B2CompatibilityTests.The_legacy_gamerules_profiles_still_declare_no_called_pitch_mode_and_keep_seven_hashes` |
| pzl-12a | Harn `PzlCheckTests.Pzl12a_the_registered_streams_are_exactly_the_pinned_game_set_and_real_runs_draw_only_from_them`, `…Pzl12a_a_presentation_stream_in_the_registry_or_in_a_game_fails_and_12b_is_always_deferred_never_pass`, `…Pzl12a_the_harness_and_its_result_hashes_depend_on_no_presentation_or_infrastructure_assembly` |

### 12.3 TestEvidence 독립 확인

검증기가 Build Provenance의 `TEST_ASSEMBLY` artifact(SHA로 확인한 것)를 승인 환경에서 직접 실행하고, 자기 TRX 결과를 만듭니다. 자식 `dotnet test`는 금지 접두사 변수를 제거한 환경으로 실행합니다(§18.4 E4).

1. 직접 실행한 결과의 case 집합이 profile `checks[i].cases`와 정확히 같아야 합니다.
2. 제출된 `TestEvidence.cases`가 검증기 결과와 바이트까지 같아야 합니다.

**거부 규칙:**
- 누락·추가·중복·미승인 Case, `Skipped`, `NotExecuted`는 `Evidence.test_profile_mismatch`이며 인증하지 않습니다.
- FAIL은 Check FAIL이며 인증하지 않습니다.
- PASS라는 주장만 있고 직접 실행 결과가 없으면 인정하지 않습니다.

---

## §13. Approval Record v1 (S01 §2.1 + PATCH-02·PATCH-04)

### 13.1 `simharness-approval-record-v1` (닫힌 Schema, 모든 필드 필수)

Hash 계약 v1에 종속되며, 저장소 밖에서 조회합니다(R-10). 필드 집합은 R-10 초안을 대체한 S01 §2.1 + PATCH-02가 최종입니다. `test_only=true`인 기록으로는 정상 인증을 발급할 수 없습니다.

```
format                     : "simharness-approval-record-v1"
approval_record_id         : ID
validation_set_id          : ID (≤64자)
freeze_id                  : ID
test_only                  : bool
contracts                  : { family, dominance, pzl_unit, pzl6, hash : VER }   정확히 5키, v1 모두 "1"
git_commit_sha             : GIT
build_provenance           : { id: ID, raw_sha256: H }
environment_profile        : { id: ID, raw_sha256: H }
pzl_test_profile           : { id: ID, raw_sha256: H }
execution_policy           : { id: "fc3-official-execution-v1", hash: H }
evaluation_policies        : { calibration: { id: "fc-evaluation-policy-v1", hash: H }, dominance: { id: "fc3-dominance-v1" } }
dominance_games_per_cell   : P (≥2)
integration_cohort_ids     : EXACT[] (≥1, ordinal 오름차순, 중복 금지)
cohort_inputs_hashes       : [{ cohort_id: EXACT, inputs_hash: H }] (cohort_id ordinal 오름차순, 중복 금지)
combination_policy_clauses : { calibration: EXACT, integration: EXACT }
certification_allowances   : { insufficient_sample: bool }
families                   : [FamilyApproval] 정확히 3개, 순서 fc3-cal, fc3-int, fc3-dom
verifiers                  : [{ id: ID, version: TEXT1, source_git_sha: GIT, binary_sha256: H }]
                             (≥1, (id, version) ordinal 오름차순, (id, version)과 binary_sha256 각각 중복 금지)
FamilyApproval = { family, run_id: RUN, units: [UnitApproval] (카탈로그 순서) }
UnitApproval   = { unit_id, cohort_id, rules_profile_id, planned_games: P,
                   seed_manifest_path: RELPATH(.json), seed_manifest_sha256: H }
```

### 13.2 Unit 카탈로그 (정확한 집합. 누락·추가·순서 위반은 모두 거부)

| family | unit_id 순서 | cohort_id | rules_profile_id | planned_games |
|---|---|---|---|---|
| fc3-cal | L1, L2, L3, L4 | L1·L2 `neutral-mixed-v1`, L3·L4 `virtual-league-v1` | L1·L3 `test-pzl-automated`, L2·L4 `test-pzl-non-automated` | = Seed 수 |
| fc3-int | `automated`, `non-automated` | `integration_cohort_ids`의 원소 | 각각 `test-pzl-automated`, `test-pzl-non-automated` | = 100 |
| fc3-dom | `DominanceMatrix.Cells` 48개 Id, Number 순서 | 셀 정의값 | 셀 정의값 | = `dominance_games_per_cell` |

### 13.3 거부 규칙 (모두 `Evidence.approval_mismatch`)

- 모든 Unit을 통틀어 `seed_manifest_path`나 `seed_manifest_sha256`이 중복되면 거부합니다(같은 Unit 안의 중복 포함).
- `run_id`가 3개 Family 사이에서 중복되면 거부합니다.
- `cohort_inputs_hashes`의 cohort 집합이 Unit들이 쓰는 cohort의 합집합과 정확히 같지 않으면 거부합니다(미승인 Cohort 포함).
- `integration_cohort_ids`에 `CohortRegistry`에 없는 ID가 있으면 거부합니다.
- 같은 `approval_record_id`나 `validation_set_id`가 다른 바이트로 존재하면 거부합니다(§15 UNIQUE).
- Verifier 중복은 거부합니다.

### 13.4 필드별 독립 검증 출처

| 필드 | 대조 대상 |
|---|---|
| `execution_policy.hash` | `OfficialProfile.Hash()` 재계산 (Official.cs:51) |
| `evaluation_policies.calibration.hash` | `EvaluationPolicy.FcEvaluationV1.Hash()` 재계산 |
| `inputs_hash` | `CohortRegistry.Resolve(id).InputsHash` |
| `seed_manifest_sha256` | `SeedManifestReader.Read`의 Pin 대조, 이어서 파일의 cohort·policy·rules·Seed 수를 UnitApproval·카탈로그와 대조 (SeedManifest.cs:84-104) |
| `contracts` | 검증기가 지원하는 조합 |
| `build_provenance`, `environment_profile`, `pzl_test_profile` | 각각 Registry 조회 원문의 SHA와 원문의 ID. **`source_git_sha` 비교는 아래 PATCH-04 규칙만 적용** |
| `verifiers[].binary_sha256` | Build Provenance의 `VERIFIER_BINARY` artifact |

**PATCH-04 결속:**
- `BuildProvenance.source_git_sha == ApprovalRecord.git_commit_sha`
- `PzlTestProfile.source_git_sha == ApprovalRecord.git_commit_sha`
- `PzlTestProfile.build_provenance_id == ApprovalRecord.build_provenance.id`
- Environment Profile에는 `source_git_sha` 필드가 **없습니다**. 이 필드를 읽거나, 없을 때 기본값을 채워 통과시키는 것을 금지합니다. 들어 있으면 미정의 필드로 `schema_invalid`입니다.
- `ApprovalRecord.environment_profile.{id, raw_sha256} == BuildProvenance.environment_profile.{id, raw_sha256}`이고, Registry 원문의 SHA와 `environment_profile_id`가 일치해야 합니다.
- 세 참조 원문은 각각 따로 조회해 ID·SHA·OWNER 등록 SID·엄격 파싱 내용을 검증합니다.

### 13.5 `OfficialApprovals` 매핑 (검증기 전용 인스턴스, `Current`는 변경하지 않음)

- `GitCommitSha = git_commit_sha`
- `ExecutionPolicyId/Hash`
- `SeedManifestPins = {모든 Unit의 path → sha}`
- `CohortInputsHashes`, `DominanceGamesPerCell`, `IntegrationCohortIds`
- `ExecutionPathReady = §14.3 R01-1의 결과`

---

## §14. 실행 허가·권한 상태·Replay 권한 (S01 §2.4 + PATCH-01 → S03)

### 14.1 `simharness-execution-authorization-v1` (닫힌 Schema, OWNER만 등록)

`{format, authorization_id: ID, approval_record_id: ID, approval_record_sha256: H, test_only: bool, families: [family]}`

- `families`는 승인 기록 순서의 부분열이며, 길이 1 이상, 중복 금지입니다.
- `test_only`는 승인 기록의 `test_only`와 같아야 합니다.
- B-1 구현은 이 기록을 **읽기만** 하며 생성 기능을 두지 않습니다.

### 14.2 세 상태 축

| 상태 축 | 값 | 판정 |
|---|---|---|
| `approval_resolution` | UNRESOLVED / CONFLICT / RESOLVED_TEST_ONLY / RESOLVED_OFFICIAL | §16 L-단계 결과 |
| `execution_authority[family]` | NOT_AUTHORIZED / AUTHORIZED_TEST_ONLY / AUTHORIZED_OFFICIAL | 허가 기록이 있고, 승인 SHA가 일치하고, family가 포함됐는지 |
| `verification_authority` | NONE / TEST_ONLY / OFFICIAL | OFFICIAL은 RESOLVED_OFFICIAL이고 대상 Family가 모두 AUTHORIZED_OFFICIAL이며 프로필 3종이 확정됐을 때만. TEST_ONLY 조합이면 TEST_ONLY |

- `test_only=true`는 정상 인증도 공식 실행 허가도 아닙니다. TEST_ONLY 영수증은 `test_only=true`로만 발급되며 인증으로 계산되지 않습니다.
- 승인 기록이 유효해도 `OfficialApprovals.Current.ExecutionPathReady=false`는 바뀌지 않습니다. 공식 실행 경로 연결은 B-1 범위 밖입니다.

### 14.3 R01-1 — 검증기 인스턴스의 `ExecutionPathReady`

다음 다섯 조건이 **모두** 참일 때만 `true`이고, 그 밖에는 `false`입니다.

1. `approval_resolution == RESOLVED_OFFICIAL`
2. 대상 Family의 `execution_authority == AUTHORIZED_OFFICIAL`
3. OWNER가 등록한 `EXECUTION_AUTHORIZATION`의 `approval_record_id`·`approval_record_sha256`이 승인 기록과 일치하고, **AMPV(§14.6)가 PASS**
4. 신뢰 Registry에서 조회한 승인 원문의 SHA와 모든 Seed Pin을 대조함 (§16 L2~L6)
5. 승인 기록과 실행 허가 기록이 모두 `test_only == false`

`AUTHORIZED_TEST_ONLY`이면 다른 조건과 관계없이 `false`입니다.

### 14.4 R01-2 — 실행 경로 차단

- 이 인스턴스는 replay·검증에만 씁니다.
- 검증기는 `FamilyOfficial.Execute`·`DominanceOfficial.Execute`를 호출하지 않으며, 이를 테스트로 고정합니다.
- `OfficialApprovals.Current`(전부 null, `ExecutionPathReady=false`, Official.cs:73)와 CLI 공식 경로(Program.cs:185, :223)는 바꾸지 않습니다.
- B-1의 어떤 산출물도 공식 실행 경로를 켜지 않습니다.

### 14.5 Replay 권한 (R01-3 잔여 + R01-A·B·C)

- `ReplayAuthority { OfficialExecution (기본값), TestOnlyVerification }` 인자를 `ReplayCheck.Validate`, `FamilyOfficialReplay.Verify`, `DominanceOfficialReplay.Verify`에 기본값 `OfficialExecution`으로 추가합니다. 이 함수들은 인자를 아래로 넘기기만 하며, 검사 순서·위치·오류 코드는 바꾸지 않습니다(Replay.cs:188-282).
- **R01-A.** `OfficialReplay.VerifyEnvelope(official, configHash, a)`는 현재 `Verify`에서 준비 검사 바로 앞까지의 분기 전체와 예외 처리를 **순서·조건·오류 코드 그대로** 옮긴 함수입니다(Official.cs:422-453, :456-459). `Verify(…, authority = OfficialExecution)`의 동작은 다음과 같습니다.
  1. `VerifyEnvelope`를 실행하고, 오류가 있으면 그대로 반환합니다.
  2. 성공했을 때만 **현재 준비 검사 위치(:454)**에서 `authority` 게이트를 평가합니다.
  - 기본값으로 호출한 결과는 리팩터링 전과 같아야 합니다.
- **R01-B. 게이트 정의**
  - `OfficialExecution`: `a.ExecutionPathReady ? 통과 : Official.not_ready` (현재 코드와 동일)
  - `TestOnlyVerification(authorizedFamilies)`:
    - `a.ExecutionPathReady == true`이면 `Evidence.authorization_missing`(원인 `Trust.test_only_flag_misuse`)으로 거부
    - `att.SeedFamily ∉ authorizedFamilies`이면 `Official.not_ready`
    - 그 밖에는 통과
    - `authorizedFamilies`는 `approval_resolution == RESOLVED_TEST_ONLY`이고 `execution_authority == AUTHORIZED_TEST_ONLY`인 Family 집합입니다.
  - TEST_ONLY 결과는 `test_only=true` 영수증으로만 남고, `Execute`·CLI·`Current`에 전달하지 않습니다.
- **R01-C. 오류 감싸기**
  - B-1 인증 계층은 replay가 실제로 `Official.not_ready`를 반환했을 때만 `Evidence.authorization_missing`으로 감쌉니다(원인 코드 보존).
  - 그 밖의 오류(`Replay.*`, `Seed.*`, `FamOfficial.*`, `DomOfficial.*`, `Official.*`)는 원인 코드 그대로 상위 분류에만 매핑하고, 권한 오류로 바꾸지 않습니다.
  - **권한을 미리 일괄 검사하는 단계는 두지 않습니다.**

### 14.6 R01-D — AMPV (승인 매핑 사전 검증; 기존 Preflight의 `Ready` 계산과 보고서는 읽기만 함)

1. 승인 기록으로 후보 인스턴스 A를 만듭니다. A의 `ExecutionPathReady`는 **항상 false**입니다.
2. Family별 계획을 스캔합니다: `FamilyPreflight.PinnedUnits(plan, root, A)` 또는 `DominancePreflight.PinnedCells(root, A)`. PASS가 아니면 원인 코드 그대로 실패입니다. Dominance는 추가로 `DominancePreflight.PlanSizeCheck(A, scan)`이 PASS여야 합니다.
3. Unit마다 `OfficialPreflight.Run(new OfficialRequest(root, unit.SeedManifestPath, run_id, null), A, git)`을 호출합니다. 이 함수는 단락 평가 없이 모든 검사를 실행·보고합니다(Official.cs:154-265).
4. 반환된 `Checks`를 아래 표로 분류합니다. 표에 없는 `Id`가 나오면 실패입니다.

| check id | 분류 | AMPV 요구 |
|---|---|---|
| `model_tuning, seed_manifest, family, rules, policy, cohort, cohort_hash, game_ids, execution_policy` | EVALUATED | `Pass`. 그 밖의 상태는 원인 코드 그대로 실패이며 무시하지 않음 |
| `execution_path` | AUTH_DIRECT | 정확히 `(NotReady, Official.not_ready)`. `Pass`가 나오면 실패 |
| `attestation` | AUTH_DERIVED | 정확히 `(NotReady, Official.input_unavailable)`. 판정에 쓰지 않고 5단계로 대체 |
| `cli, git_integrity, git_sha, output_directory` | EXECUTION_ONLY | AMPV에서 평가하지 않음. 실제 공식 실행 때 기존 Preflight가 판정 |

5. Attestation 생성 블록(Official.cs:245-259)을 결과가 같은 public 함수 `OfficialPreflight.BuildAttestation(...)`으로 추출합니다. `Run`은 이 함수를 그대로 호출하므로 보고서는 변하지 않습니다. AMPV는 이 함수를 직접 호출해 성공을 요구하며, 실패하면 `Official.park_not_approved` 또는 설정 오류 코드로 실패입니다.
6. Family 수준의 `unit_preflight`·`cell_preflight` 집계 행(FamilyOfficial.cs:66-80, DominanceOfficial.cs:56-73)은 **읽지 않습니다**. 집계 행을 PASS로 간주하는 처리는 금지합니다.

기존 Hash 영향은 없습니다. `VerifyEnvelope`와 `BuildAttestation` 추출이 결과를 바꾸지 않는다는 것은 기존 테스트 통과로 확인합니다. 이 리팩터링은 Codex 리뷰를 권장합니다.

### 14.7 Replay 제어 흐름과 오류 우선순위 (기본 모드, 현행 코드 동작 유지)

**단일 Manifest (`ReplayCheck.Validate`, 오류가 나면 그 자리에서 첫 오류 반환)**
1. 기존 사전 검사(Replay.cs:192-245): 스키마, Envelope·mode, `run_status`, 결과 버전, 시험 튜닝, Cohort Hash, 정책, 규칙, 진루, 경기 목록
2. `VerifyEnvelope`: Envelope 형식 → Attestation 파싱·버전 → `Replay.attestation_mismatch` → `Replay.attestation_config_mismatch` → `Official.seed_manifest_not_frozen` → `Replay.seed_manifest_hash_mismatch` → 실행 정책 → Cohort Hash → Git SHA
3. 게이트(`Official.not_ready`, 또는 TEST_ONLY 게이트)
4. `Replay.attestation_manifest_mismatch`
5. `VerifyAgainstSeedFile`: `Seed.file_hash_mismatch`, 이어서 바인딩·프로필·경기 목록
6. `WithStop`

**Family·Dominance (FamilyOfficial.cs:447-533, DominanceOfficial.cs:406-483):** 다음 순서를 마친 뒤 Unit·Cell마다 위 1~6을 수행하고 재실행을 비교합니다.
1. 색인 존재·중복 키·형식·Family·완료 여부
2. **승인 Pin으로 실제 Seed 파일 전체 재읽기** (Dominance는 추가로 `PlanSizeCheck`)
3. 승인 기록 비교
4. Unit 순서·경로·Manifest SHA

| 상황 | 단일 Manifest | Family / Dominance |
|---|---|---|
| 준비 false + Attestation Hash 오류(색인 Manifest SHA도 갱신) | `Replay.attestation_mismatch` | `Replay.attestation_mismatch`. SHA를 갱신하지 않으면 `FamOfficial.unit_manifest_hash_mismatch` / `DomOfficial.cell_manifest_hash_mismatch` |
| 준비 false + 승인 Pin과 Attestation SHA 불일치 | `Replay.seed_manifest_hash_mismatch` | 승인 Pin이 실제 파일과 맞으면 `Replay.seed_manifest_hash_mismatch`, 다르면 2단계 `Seed.file_hash_mismatch` |
| 준비 false + 실제 Seed 파일 변조 | `Official.not_ready` | `Seed.file_hash_mismatch` (2단계) |
| 준비 true + 실제 Seed 파일 변조 | `Seed.file_hash_mismatch` (5단계) | `Seed.file_hash_mismatch` (2단계) |

**TestOnlyVerification:** 경로는 같고 3번 게이트만 R01-B의 TEST_ONLY 정의로 바뀝니다. 앞단 오류가 있으면 그 오류가 먼저 반환됩니다.

---

## §15. 신뢰 기록부 (S01 §2.2, OD-3 SQLite; 권한은 §17 R03-5가 최종)

```sql
CREATE TABLE registry_entry(
  registry_sequence       INTEGER PRIMARY KEY AUTOINCREMENT,
  entry_kind              TEXT NOT NULL CHECK(entry_kind IN ('APPROVAL_RECORD','EXECUTION_AUTHORIZATION','BUILD_PROVENANCE',
                                                             'ENVIRONMENT_PROFILE','PZL_TEST_PROFILE','VERIFICATION_RECEIPT')),
  entry_id                TEXT NOT NULL,
  scope_key               TEXT NULL,          -- APPROVAL_RECORD에서만 validation_set_id, 그 외 NULL
  raw_sha256              TEXT NOT NULL,      -- 서비스가 raw_bytes로 직접 계산
  raw_bytes               BLOB NOT NULL,
  registrant_principal_id TEXT NOT NULL,      -- 서비스가 OS 인증으로 얻은 SID 문자열 (R03-5)
  UNIQUE(entry_kind, entry_id));
CREATE UNIQUE INDEX registry_scope ON registry_entry(entry_kind, scope_key) WHERE scope_key IS NOT NULL;
CREATE TRIGGER registry_no_update BEFORE UPDATE ON registry_entry BEGIN SELECT RAISE(ABORT,'immutable'); END;
CREATE TRIGGER registry_no_delete BEFORE DELETE ON registry_entry BEGIN SELECT RAISE(ABORT,'immutable'); END;
```

**연산:**
- `Register(kind, id, raw_bytes)`는 `BEGIN IMMEDIATE` 안에서 수행합니다.
  1. 권한을 확인합니다. 역할×종류는 R03-5-3 표를 따르고, 권한이 없으면 `registration_unauthorized`입니다. 단, **EVIDENCE_READER는 트랜잭션을 시작하기 전에 거부**합니다.
  2. 엄격 Reader로 파싱하고, 내용 안의 id가 인자 id와 같은지 확인합니다.
  3. `(kind, id)`가 없으면 INSERT하고 `Inserted(seq)`를 반환합니다.
  4. 있고 바이트가 같으면 `AlreadyRegistered(seq)`를 반환합니다(멱등).
  5. 있고 바이트가 다르면 `registration_conflict`입니다. scope 충돌도 conflict입니다.
- `Get(kind, id)`: entry를 반환하고, 없으면 `NotFound`입니다.
- `GetByScope("APPROVAL_RECORD", validation_set_id)`

**권한 분리:**
- DB 파일과 디렉터리의 쓰기·삭제·ACL 변경은 서비스 계정만 할 수 있습니다(§17 R03-2 ③).
- 쓰기는 서비스 Pipe를 통해서만 가능합니다. 검증기는 SQLite 파일을 직접 열지 않으므로, 파일을 직접 고친 사본은 신뢰 대상이 아닙니다.
- 같은 사용자가 자유롭게 고칠 수 있는 옆 폴더 JSON은 권한 분리를 만족하지 않습니다.
- 운영 계정·SID·경로는 운영 시점에 정합니다.
- 이 신뢰 모델은 OS 관리자까지 Hash만으로 방어한다는 주장이 아닙니다. 소유자 통제 기록부와 승인 검증기가 신뢰 기반입니다.

---

## §16. Trust Bootstrap과 신뢰 조회 (PATCH-03: S02 T1~T7 → S03/S04/S07)

- **T1. 앵커 확보.** 검증기는 OWNER가 관리하는 설치·운영 설정에서 앵커(§17 R03-1 형식)를 읽습니다. 증거 제출물에 포함된 경로나 CLI 증거 인자로 받은 경로는 쓰지 않습니다.
- **T2. 신뢰 출처와 권한 검증 (Fail-Closed).**
  - 앵커 경로는 저장소 루트와 증거 스냅샷 루트 **밖**에 있어야 합니다.
  - 경로에 Reparse Point가 없어야 합니다(PathSafety).
  - 파일을 한 번 열어 그 바이트로 SHA 계산과 파싱을 함께 합니다.
  - 파일과 상위 디렉터리의 ACL이 현재 검증 프로세스 계정과 증거 작성자 주체에게 쓰기·삭제·ACL 변경 권한을 주지 않아야 합니다 【NSC-4】.
- **T3. Registry 연결과 서버 인증.** §17 R03-4 1~7단계를 수행합니다(Registry ID 대조 포함).
- **T4. 승인 기록 조회 (L2~L5).**
  - L2: `Get(APPROVAL_RECORD, approvals.approval_record_id)`. `NotFound`이면 `approval_mismatch`입니다.
  - L3: `registrant_principal_id ∈ owner_principal_sids`를 확인하고, 반환된 `raw_bytes`의 SHA를 클라이언트가 다시 계산합니다. 이 값이 `raw_sha256` 및 증거의 `approvals.approval_record_sha256`과 모두 같아야 합니다.
  - L4: `raw_bytes`를 엄격 파싱해 §13 전체를 검증합니다. 이후 Hash 계산·파싱·재생은 모두 이 한 번 읽은 바이트를 씁니다.
  - L5: `GetByScope`로 같은 `validation_set_id`의 다른 승인 기록이 없는지 확인합니다.
- **T5. 참조 원문 조회 (L6).** Build Provenance·Environment Profile·PZL Test Profile을 각각 조회해 OWNER SID·SHA·엄격 파싱을 확인하고, PATCH-04 결속(§13.4)을 검사합니다.
- **T6. TRUST_CONFIG 사후 대조.** Build Provenance의 `TRUST_CONFIG` artifact `raw_sha256`이 T1 앵커 바이트의 SHA와 같은지**만** 확인합니다. 앵커를 얻는 데는 쓰지 않습니다.
- **L7.** Family마다 `EXECUTION_AUTHORIZATION`을 확정합니다(§14).
- **L8.** 위 단계 중 하나라도 불일치하면 정상 인증을 하지 않습니다. 재조회 결과가 다르면 `registration_conflict`입니다.
- **T7. 실패 처리.** 앵커 부재와 T2·T3·T6 불일치, Registry 연결 실패는 모두 Fail-Closed입니다(원인 코드는 §1.3, 상위 `approval_mismatch`).

**순환 금지:** 의존 순서는 `앵커 → Registry → Build Provenance → (사후 대조) 앵커`입니다. 앵커의 신뢰는 증거 제출 파일, 아직 신뢰하지 않은 Registry 응답, Build Provenance 어디에도 의존하지 않습니다.

| 단계 | 내용 | 실패 시 |
|---|---|---|
| ① | 앵커 (T1·T2) | `Trust.anchor_missing` / `Trust.anchor_untrusted` |
| ② | Pipe 연결 → 보안 설명자 → AccessCheck (R03-4 1~6) | `Trust.registry_identity_mismatch` / `registry_config_mismatch` / `registry_unavailable` |
| ③ | 같은 연결에서 Registry ID (R03-4 7) | `Trust.registry_mismatch` |
| ④ | 승인 기록 조회 (L2) | `approval_mismatch` |
| ⑤ | 등록 SID ∈ `owner_principal_sids`, 원문 SHA (L3~L5) | `approval_mismatch` |
| ⑥ | 세 참조 원문 (L6, PATCH-04) | `approval_mismatch` |
| ⑦ | `TRUST_CONFIG` 사후 대조 | `Trust.anchor_mismatch` |
| ⑧ | 실행 허가 (§14) | §14 규칙 |
| ⑨ | 독립 검증 시작 | — |

연결이 끊기면 ②부터 다시 시작합니다.

---

## §17. Windows Registry 보안 계약 (R03-1~R03-6 최종)

### R03-1 Trust Anchor (S07 최종)

```
simharness-verifier-trust-anchor-v1 = {
  format: "simharness-verifier-trust-anchor-v1",
  registry_instance_id: ID,
  registry_pipe_name: ^[A-Za-z0-9._-]{1,128}$,       // 접속 경로 \\.\pipe\{name} 고정, 원격 서버 이름 금지
  registry_account_sid: SID_USER,                     // Registry 전용 서비스 실행 계정
  registry_service_sid: SID_SERVICE | null,           // v1 기본값 null (R03-2 ⑥)
  owner_principal_sids: SID_USER[]   (≥1, ordinal 오름차순, 중복 금지),
  verifier_principal_sid: SID_USER,
  evidence_reader_sids: SID_USER[]   (≥0, ordinal 오름차순, 중복 금지) }
```

- 다섯 역할 SID(`registry_account_sid`, `registry_service_sid`, `owner_principal_sids`, `verifier_principal_sid`, `evidence_reader_sids`)는 서로 겹치지 않아야 하며, 겹치면 앵커가 무효입니다.
- Well-known 그룹·시스템 SID(`S-1-1-0`, `S-1-5-11`, `S-1-5-32-*`, `S-1-5-18/19/20`)는 문법상 들어갈 수 없습니다.
- **`evidence_reader_sids`:**
  - OWNER가 Registry 조회를 승인한 증거 작성자 SID의 **전체 집합**입니다. 승인된 작성자의 SID는 반드시 포함해야 합니다.
  - 빈 배열은 승인된 작성자가 0명일 때만 허용합니다.
  - **필수 필드**입니다. 앵커나 서비스 설정에 필드가 없으면 `Evidence.schema_invalid`이며, `[]` 기본값을 채우지 않습니다.
  - 출처는 OWNER가 통제하는 앵커와 서비스 보호 설정뿐입니다. 증거 제출물·CLI·Registry 요청으로는 지정하거나 바꿀 수 없습니다.
  - 각 SID는 로컬 관리자가 아닌 표준 사용자여야 합니다.
  - 공식 인증의 통계 판정, A+ Hash, 영수증 인증 조건에 영향을 주지 않습니다.

### R03-2 SID 역할

| 용도 | SID | 근거 |
|---|---|---|
| ① Pipe Owner | `registry_account_sid` (보안 설명자에 명시) | 서비스 프로세스 TokenUser |
| ② 인스턴스 생성 권한 | `registry_account_sid`만 | DACL |
| ③ SQLite·서비스 설정 쓰기 | `registry_account_sid`(`registry_service_sid`가 non-null이면 그 SID만) + Administrators(TCB) | 파일 ACL |
| ④ 연결 허용 | `verifier_principal_sid`, `owner_principal_sids`, `evidence_reader_sids` | DACL |
| ⑤ 서버 자체 점검 | 시작 시 TokenUser == `registry_account_sid`가 아니면 종료 | `GetTokenInformation(TokenUser)` |
| ⑥ 서비스 SID | `SERVICE_SID_TYPE_UNRESTRICTED`로 등록된 경우에만 non-null. 이 SID는 **TokenGroups**에 `SE_GROUP_ENABLED_BY_DEFAULT \| SE_GROUP_OWNER`로 들어가며 TokenUser가 아닙니다. 용도는 ③의 강화뿐이고, 클라이언트는 이를 검사하지 않습니다(할 수도 없음) | `GetTokenInformation(TokenGroups)` |

**운영 조건 (OD-3 배치 체크리스트):**
- 이 계정은 Registry 서비스 하나만 실행합니다.
- "서비스로 로그온" 외의 로그온 권한(로컬·RDP·배치·네트워크)은 거부합니다.
- 이 계정은 Administrators에 속하지 않습니다.
- 증거 작성자는 이 계정의 자격 증명을 모릅니다.
- 다른 서비스나 예약 작업이 이 계정으로 실행되지 않습니다.
- Reader SID는 모두 표준 사용자입니다.

### R03-3 서버 측 Pipe 생성 (S04 + S05 S1~S7 + S06 Reader ACE)

**`CreateNamedPipeW` 인자:**
- 이름: `\\.\pipe\{registry_pipe_name}`
- `dwOpenMode = PIPE_ACCESS_DUPLEX (0x3)`. 첫 인스턴스에만 `| FILE_FLAG_FIRST_PIPE_INSTANCE (0x00080000)`를 붙입니다. `WRITE_DAC`·`WRITE_OWNER`·`ACCESS_SYSTEM_SECURITY`는 지정하지 않습니다.
- `dwPipeMode = PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS (0x8)`
- `nMaxInstances = 4` (v1 고정, 모든 인스턴스가 같은 값)
- 보안 설명자:
  - Owner = Group = `registry_account_sid`
  - DACL은 Protected(`SE_DACL_PROTECTED`)이며, 상속 플래그 없는 ACCESS_ALLOWED ACE만 둡니다.

```
registry_account_sid      : FILE_ALL_ACCESS (0x001F01FF)
verifier_principal_sid    : 0x00120003 (FILE_READ_DATA | FILE_WRITE_DATA | READ_CONTROL | SYNCHRONIZE)
각 owner_principal_sids    : 0x00120003
각 evidence_reader_sids    : 0x00120003
```

- Reader의 `0x00120003`은 Pipe 전송 권한일 뿐입니다. Register나 SQLite 쓰기 권한이 아니며, 작업 권한은 R03-5가 정합니다.
- Reader에게는 `FILE_CREATE_PIPE_INSTANCE (0x4)`, `GENERIC_WRITE`, `DELETE`, `WRITE_DAC`, `WRITE_OWNER`가 든 ACE를 주지 않습니다. Reader는 SQLite 파일에 대한 읽기 ACL도 받지 않으며, 조회는 Pipe로만 합니다.

**인스턴스 운영 (S05):**
- **S1.** 서비스는 자기가 연 서버 인스턴스 핸들 수 `n`을 직접 셉니다.
- **S2.** 첫 인스턴스만 `FILE_FLAG_FIRST_PIPE_INSTANCE`로 만듭니다. 실패하면(`ERROR_ACCESS_DENIED`, 이름 선점) 시작을 거부하고 이벤트를 기록합니다. 이후 인스턴스는 이 플래그 없이 같은 보안 설명자·`PIPE_REJECT_REMOTE_CLIENTS`·모드로 만듭니다.
- **S3.** 용량이 남아 있는 동안 대기 인스턴스를 유지합니다.
  - 대기 인스턴스가 0개이고 `n < 4`이면 새로 만듭니다.
  - `n == 4`이면 만들지 않고 "포화"로 기록합니다. 포화를 이름 선점이나 위조로 판단하지 않습니다.
  - 포화는 오류 코드가 아니라 `n`으로만 판단합니다.
- **S4.** `n < 4`인데 생성이 실패하면 3회(간격 1초) 재시도합니다. 그래도 실패하면 오류를 기록하고 정상 중지합니다. 무한 재시도는 금지합니다.
- **S5.** 처리를 마친 인스턴스는 닫지 않고 `FlushFileBuffers → DisconnectNamedPipe → ConnectNamedPipe` 순으로 재사용합니다. `n`은 변하지 않습니다.
- **S6.** 실행 중에는 Pipe 이름의 마지막 서버 핸들을 닫지 않습니다. 모든 핸들을 닫는 것은 중지할 때뿐입니다.
- **S7.** `ConnectNamedPipe`가 FALSE를 반환할 때:
  - `ERROR_PIPE_CONNECTED`이면 정상 연결입니다.
  - `ERROR_NO_DATA`이면 S5의 Disconnect 후 재대기합니다.
  - 이 결과들은 R03-4·R03-5 인증을 생략할 근거가 아닙니다.

**프레이밍:** 4바이트 빅엔디언 길이 + JCS UTF-8 본문, 최대 16 MiB. Byte 모드를 쓰며 읽기 모드는 바꾸지 않습니다.

### R03-4 클라이언트 연결과 서버 인증 (단일 절차)

**1. 자기 신원 확인 (두 클라이언트 유형)**

| 클라이언트 | 1단계 조건 | 허용 요청 |
|---|---|---|
| Official EvidenceVerifier | TokenUser ∈ {`verifier_principal_sid`} ∪ `owner_principal_sids`. TokenUser ∈ `evidence_reader_sids`이면 시작 거부(`Trust.client_principal_not_allowed`) | Get, GetByScope, Register (종류는 R03-5) |
| Registry Read-Only Client | TokenUser ∈ `evidence_reader_sids` | Get, GetByScope만. Register 연산·인증·영수증 발급 기능이 타입에 존재하지 않음 |

EVIDENCE_READER를 EvidenceVerifier의 허용 SID 목록에 추가하는 구현은 금지합니다. 두 유형 모두 2~9단계를 똑같이 수행합니다.

**2. 연결**
- `CreateFileW` 인자:
  - `lpFileName="\\.\pipe\{name}"`, `dwDesiredAccess=0x00120003`, `dwShareMode=0`, `lpSecurityAttributes=NULL`
  - `dwCreationDisposition=OPEN_EXISTING (3)`
  - `dwFlagsAndAttributes=SECURITY_SQOS_PRESENT (0x00100000) | SECURITY_IDENTIFICATION (0x00010000)` (동기 I/O, OVERLAPPED 없음)
  - `hTemplateFile=NULL`
- 선언: `[LibraryImport("kernel32.dll", EntryPoint="CreateFileW", SetLastError=true, StringMarshalling=StringMarshalling.Utf16)] static partial IntPtr CreateFileW(string, uint, uint, IntPtr, uint, uint, IntPtr);`
- **금지:** `GENERIC_WRITE`, `FILE_GENERIC_WRITE`, `FILE_APPEND_DATA`/`FILE_CREATE_PIPE_INSTANCE (0x4)`, 기본 `NamedPipeClientStream.Connect()`
- 결과가 `INVALID_HANDLE_VALUE`이면 실패입니다.
- **C1.** 전체 제한 시간은 `REGISTRY_CONNECT_DEADLINE_MS = 30000`입니다.
- **C2.** `ERROR_PIPE_BUSY`이면 `WaitNamedPipeW(name, 남은 시간)`으로 유한하게 기다립니다. `NMPWAIT_WAIT_FOREVER`와 `NMPWAIT_USE_DEFAULT_WAIT`는 쓰지 않습니다.
- **C3.** Wait가 TRUE이면 새 `CreateFileW`를 시도합니다. 다시 BUSY이면 C2로 갑니다. 인스턴스가 없어 Wait가 즉시 반환되면 100 ms 쉬고 재시도합니다.
- **C4.** 제한 시간 초과(`ERROR_SEM_TIMEOUT` 포함), 제한 시간 끝까지 `ERROR_FILE_NOT_FOUND`, `ERROR_ACCESS_DENIED`를 포함한 그 밖의 연결 실패, 연결 직후 끊김은 모두 `Trust.registry_unavailable`(OS 오류 코드 보존)입니다. 받은 바이트는 해석하지 않습니다.
- **C5.** 재시도 끝에 얻은 핸들도 3~7단계를 반드시 수행합니다. Wait 성공은 인스턴스가 누구 것인지 보장하지 않습니다.
- **C6.** `ERROR_PIPE_CONNECTED`는 서버 측 결과이며, 클라이언트 판정에 쓰지 않습니다.

**3. 보안 설명자 조회:** `GetSecurityInfo(h, SE_KERNEL_OBJECT, OWNER|GROUP|DACL_SECURITY_INFORMATION, …)`

**4. 구조 검증 (닫힌 규칙)**
- (a) Owner == `registry_account_sid`
- (b) DACL이 존재하고 NULL이 아님
- (c) 모든 ACE가 `ACCESS_ALLOWED_ACE_TYPE`이고 `AceFlags == 0`
- (a)~(c) 실패는 `Trust.registry_identity_mismatch`입니다.
- (d) 각 ACE 마스크에 `MapGenericMask(FileMapping{0x00120089, 0x00120116, 0x001200A0, 0x001F01FF})`를 적용한 뒤, (trustee SID, mask) **다중집합**이 Trust Anchor로 계산한 기대 구성과 정확히 같아야 합니다. ACE 순서는 비교하지 않습니다.
  - `(registry_account_sid, 0x001F01FF)` 정확히 1개
  - `(verifier_principal_sid, 0x00120003)` 정확히 1개
  - 각 `owner_principal_sids`마다 `(sid, 0x00120003)` 정확히 1개
  - 각 `evidence_reader_sids`마다 `(sid, 0x00120003)` 정확히 1개
  - 필수 ACE 누락, 같은 SID의 중복, 기대 구성에 없는 SID, 마스크 부족·초과, 계정 마스크 불일치는 `Trust.registry_config_mismatch`입니다.

**5. 실제 접근 판정 (자기 토큰, MINOR-01).** 실패하면 즉시 거부합니다(`Trust.registry_identity_mismatch`).
- 5-1. `ImpersonateSelf(SecurityIdentification)`. FALSE이면 거부합니다.
- 5-2. `OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, OpenAsSelf=TRUE, &hToken)`. FALSE이면 거부합니다.
- 5-3. `GrantedAccess=0`, `AccessStatus=FALSE`로 초기화한 뒤 `AccessCheck(pSD, hToken, MAXIMUM_ALLOWED, &FileMapping, pPrivilegeSet(1,024바이트), &len, &GrantedAccess, &AccessStatus)`를 호출합니다. `pSD`는 3단계 결과입니다.
- 5-4. 반환이 FALSE이면 거부합니다. `GrantedAccess`를 읽지 않고, 초기값 0을 근거로 통과시키지도 않습니다(`ERROR_INSUFFICIENT_BUFFER` 포함, 재시도 없음).
- 5-5. 반환이 TRUE이고 `AccessStatus`가 FALSE이면 거부합니다.
- 5-6. 둘 다 TRUE일 때만 `GrantedAccess`를 씁니다. `(GrantedAccess & 0x000D0114) != 0`이면 거부합니다. `FORBIDDEN = FILE_CREATE_PIPE_INSTANCE | FILE_WRITE_EA | FILE_WRITE_ATTRIBUTES | DELETE | WRITE_DAC | WRITE_OWNER`
- 5-7 (finally, 모든 경로):
  - `hToken`이 열렸으면 `CloseHandle`, `pSD`는 `LocalFree`로 해제합니다.
  - 5-1을 **호출했다면 반환값과 관계없이** `RevertToSelf`를 한 번 호출합니다. FALSE이면 `Environment.FailFast`로 즉시 종료합니다.
  - 실패로 끝나면 `SafePipeHandle`을 Dispose합니다. 성공했을 때만 소유권을 6단계로 넘깁니다.
- 통과 조건: `ImpersonateSelf 성공 ∧ OpenThreadToken 성공 ∧ AccessCheck TRUE ∧ AccessStatus TRUE ∧ (GrantedAccess & FORBIDDEN)=0 ∧ RevertToSelf 성공`. 이 단계는 3·4·7단계를 대체하지 않습니다.

**6. 스트림 감싸기:** `new NamedPipeClientStream(PipeDirection.InOut, isAsync:false, isConnected:true, new SafePipeHandle(h, ownsHandle:true))`
- 이후 `Connect()`를 부르지 않습니다.
- `ReadMode`(get·set), `NumberOfServerInstances`, `TransmissionMode`, `InBufferSize`, `OutBufferSize`에 접근하지 않습니다. 이 핸들에 부여하지 않은 권한을 요구하기 때문입니다.

**7. Registry ID 대조:** 같은 핸들로 `registry_instance_id`를 조회해 앵커와 비교합니다. 불일치는 `Trust.registry_mismatch`입니다.

**8. 조회:** 같은 핸들에서만 승인 원문·Raw SHA·등록 SID를 조회합니다(§16 L2~L6). 한 검증 세션의 모든 조회·등록은 이 핸들에서만 합니다.

**9. 종료·재연결:** 끊기거나 오류가 나면 세션을 폐기하고 2단계(연결)부터 다시 합니다. 투명한 자동 재연결은 금지합니다.

- 2~7단계 중 하나라도 실패하면 응답 바이트를 해석하지 않습니다(상위 `Evidence.approval_mismatch`).
- `GetNamedPipeServerProcessId`는 **사용하지 않습니다**. 서버가 보낸 PID·SID·ID 문자열은 신원 증명이 아닙니다.

### R03-5 서버 역할과 권한

- **R03-5-1 설정 스냅샷.**
  - 서비스는 시작할 때 보호된 `simharness-registry-service-config-v1`을 **한 번** 읽어 불변 스냅샷을 만듭니다. 필드는 앵커의 `registry_instance_id`, `registry_pipe_name`, 모든 역할 SID(`evidence_reader_sids` 필수)에 SQLite 경로를 더한 것입니다 【NSC-5】.
  - Pipe 보안 설명자와 역할 매핑은 **오직 이 스냅샷에서** 만듭니다.
  - 실행 중 재조회나 한쪽만 갱신하는 것은 금지하며, 설정을 바꾸려면 재시작합니다.
- **R03-5-2 시작 시 검증.** 다음 중 하나라도 실패하면 시작을 거부합니다.
  - SID 문법, 배열의 정렬·중복
  - 역할 간 상호 배타성
  - 자기 TokenUser == `registry_account_sid`
  - `registry_service_sid`가 non-null이면 TokenGroups에 Enabled 상태로 있는지
  - 역할별 등록 가능 종류 표가 고정값과 일치하는지
  - 첫 인스턴스를 만든 직후 자기 서버 핸들의 `GetSecurityInfo(OWNER|GROUP|DACL)`가 스냅샷 기준 R03-4 4(a)~(d) 기대 구성과 정확히 같은지. 다르면 모든 핸들을 닫습니다.
- **R03-5-3 요청 처리 (요청마다 이 순서 고정).**
  1. 프레임 전체를 읽습니다. Impersonation 대상은 마지막으로 읽은 메시지의 문맥이기 때문입니다.
  2. `ImpersonateNamedPipeClient(h)`. 실패하면 요청을 거부하고 실행하지 않습니다.
  3. `try { OpenThreadToken(TOKEN_QUERY, OpenAsSelf=TRUE)` → `TokenImpersonationLevel`이 `SecurityAnonymous`이면 거부 → `TokenUser`로 SID 획득 `} finally { RevertToSelf()` (실패하면 `Environment.FailFast`, 이후 요청 처리 없음) `}`
  4. **Revert가 끝난 뒤** 서비스 자신의 문맥에서, 스냅샷 역할표로 역할을 정합니다.

     | TokenUser | 역할 |
     |---|---|
     | ∈ `owner_principal_sids` | OWNER |
     | == `verifier_principal_sid` | VERIFIER_SERVICE |
     | ∈ `evidence_reader_sids` | EVIDENCE_READER |
     | 그 밖 | UNAUTHORIZED → 요청 거부, 연결 종료 |

  5. 엄격 Reader로 요청을 파싱합니다(닫힌 형식).
     ```
     {op:"get", entry_kind, entry_id}
     {op:"get_by_scope", entry_kind:"APPROVAL_RECORD", scope_key}
     {op:"register", entry_kind, entry_id, raw_bytes_base64}
     ```
     `role`, `registrant_principal_id`, `principal` 같은 미정의 필드가 있으면 `Evidence.schema_invalid`입니다.
  6. 권한 표를 적용합니다.

     | 역할 | Get | GetByScope | Register |
     |---|---|---|---|
     | OWNER | 허용 | 허용 | APPROVAL_RECORD · EXECUTION_AUTHORIZATION · BUILD_PROVENANCE · ENVIRONMENT_PROFILE · PZL_TEST_PROFILE |
     | VERIFIER_SERVICE | 허용 | 허용 | VERIFICATION_RECEIPT |
     | EVIDENCE_READER | 허용(SELECT만) | 허용(SELECT만) | 종류·본문과 관계없이 권한 판정 단계에서 즉시 `Evidence.registration_unauthorized`. `BEGIN`조차 하지 않으며 INSERT·UPDATE·DELETE 없음 |
     | UNAUTHORIZED | 거부 | 거부 | 거부 |

  - 매핑에 성공한 등록 요청만 insert-or-compare 트랜잭션(§15)을 수행합니다. `registrant_principal_id`는 3에서 얻은 SID로만 기록합니다.
- **R03-5-4 신뢰 경계.** 클라이언트는 Trust Anchor와 실제 Pipe DACL의 일치만 직접 검증합니다. 서비스 내부 설정과 역할 매핑이 DACL과 같다는 것은 R03-5-1·2와 T09 수용 시험으로 보장합니다. 클라이언트는 서비스 설정을 직접 검증했다고 주장하지 않습니다.

### R03-6 위협 모델

- **TCB:** Windows 커널·NPFS·LSA, SYSTEM, 로컬 Administrators와 `SeRestorePrivilege`·`SeTakeOwnershipPrivilege`·`SeDebugPrivilege` 보유자, `registry_account_sid` 계정, 앵커를 통제하는 OWNER
- **비신뢰:** 증거 작성자(표준 사용자여야 함), 그 밖의 로컬 표준 사용자, 네트워크(`PIPE_REJECT_REMOTE_CLIENTS`)
- 증거 작성자가 관리자이면 이 계약의 보장이 성립하지 않으므로 정상 인증 운영을 개시해서는 안 됩니다.
- `TRUST_CONFIG`는 사후 대조에만 씁니다.
- **보장 구분:**
  - **G1.** Pipe 객체의 Owner·접근 제어 (문서로 직접 뒷받침)
  - **G2.** 전용 계정 신뢰. Owner가 `registry_account_sid`라는 것은 생성 프로세스가 그 SID를 소유자로 할당할 수 있었다는 뜻입니다. 즉 그 계정 자신이거나 `SeRestorePrivilege`를 가진 TCB입니다.
  - **G3.** 서버 프로세스·바이너리 신원은 **증명하지 않습니다**. 전용 계정 운영 조건으로만 막습니다.
- **P-1 (운영 Gate):** NPFS가 Pipe를 만들 때 소유자 SID 할당을 제한한다는 직접 문서는 없습니다(SetNamedSecurityInfo·SeAssignSecurity의 간접 근거만 있음). **AT-4 실측에서 생성 실패(`ERROR_INVALID_OWNER`)가 확인되기 전에는 PATCH-03을 공식 인증 운영에 사용하지 않습니다.** 실측에 실패하면 Fail-Closed입니다.

---

## §18. Execution Environment (S01 §5 + PATCH-04·PATCH-05 → S03 E2 → S04 E2-1 문구)

### 18.1 직렬화와 재계산 환경의 구분

- **직렬화:** F64 문자열은 ES 최단 왕복 표현이며 환경과 무관한 순수 규칙입니다(T02).
- **재계산:** 같은 binary64를 다시 얻으려면 프로필과 비트 Golden이 필요합니다. 대상은 `UpperQuantile`의 `Math.Log`·`Math.Sqrt`(Dominance.cs:263-288)와 Holm 절차(:304-318)이며, 계산식은 바꾸지 않습니다.

### 18.2 `simharness-environment-profile-v1` (OWNER 등록, 닫힌 Schema, `source_git_sha` 없음)

```
format, environment_profile_id: ID,
os:      { platform: "Windows", version: EXACT (Environment.OSVersion.Version), architecture: "X64" },
process_architecture: "X64",
runtime: { framework_description: EXACT, netcore_app_version: EXACT, runtime_identifier: "win-x64" },
native_math:   { library: "ucrtbase.dll", file_version: EXACT, raw_sha256: H },
runtime_files: [{ name: "System.Private.CoreLib.dll"|"clrjit.dll"|"coreclr.dll", raw_sha256: H }]  (name ordinal 오름차순, 정확히 3개)
dotnet_variables: { forbidden_prefixes: ["COMPlus_", "DOTNET_"] },     적용 범위·시점은 §18.4
math_golden: {
  upper_quantile: [{ divisor: j, bits: BITS64 }]   j = 1..36 오름차순 (UpperQuantile(0.05/j); Holm m=36·m=32와 ZAlpha(j=1) 입력 전체)
  witness_fixture: "dom-witness-fixture-v1",
  witness_digest: AH }
```

**`dom-witness-fixture-v1` (TEST_ONLY, 결정적)**
- 셀 c = 1..48마다 `CellSample(Games=20, TotalRuns=80+c, SumOfSquares=⌊(80+c)²/20⌋+40+c)`를 만듭니다. 분산은 항상 양수입니다(n·Q − T² ≥ 20·(39+c) > 0).
- `DominanceStatistics.Evaluate`로 Witness 240개(타자 4×36 + 투수 3×32)를 기존 생성 순서대로 얻습니다.
- digest:
  ```
  witness_digest = sha256(ASCII("STARPLAYER-ENV-WITNESS-V1") || 0x0A || JCS([{side, strategy, rival, column, difference, standard_error, lower_bound, non_superiority_z: BITS64, holm_critical: BITS64|null, holm_rejected}]))
  ```
- Holm 동률: 입력은 생성 순서, Z 내림차순 정렬, 동률은 입력 인덱스 오름차순입니다. 동률 Fixture(N12 확장)로 따로 고정합니다.

### 18.3 `simharness-build-provenance-v1` (OWNER 등록, 닫힌 Schema)

```
format, build_provenance_id: ID, source_git_sha: GIT (== ApprovalRecord.git_commit_sha),
environment_profile: { id, raw_sha256: H },
dotnet_sdk: "8.0.425", configuration: "Release", build_procedure_clause_id: EXACT (B-4 승인 조항),
artifacts: [{ role: "SIMHARNESS_BINARY"|"VERIFIER_BINARY"|"TEST_ASSEMBLY"|"DEPENDENCY"|"RUNTIME_CONFIG"|"TRUST_CONFIG",
              relative_path: RELPATH, raw_sha256: H, byte_length: N }]   (relative_path ordinal 오름차순, 중복 금지)
```

**결속 사슬:**
- `승인 기록 → build_provenance{id,sha} → environment_profile{id,sha}`이고, 이 값은 `승인 기록 → environment_profile`과 같아야 합니다.
- `승인 기록 → pzl_test_profile`: `source_git_sha`와 `build_provenance_id`가 일치해야 합니다.
- 승인 기록의 `verifiers[].binary_sha256` ∈ `VERIFIER_BINARY`
- 영수증의 verifier·profile 참조 = 승인 기록 값
- `TRUST_CONFIG` = 앵커 SHA (사후 대조만)

### 18.4 환경 규칙과 Bootstrap 순서

- **E1. 프로세스 경계.**
  - 일반 빌드·회귀 프로세스: `tools/verify.ps1`과 그 자식 `dotnet build`/`dotnet test`. 인증 근거가 아니며, 동작과 환경 설정(`DOTNET_CLI_UI_LANGUAGE`, `DOTNET_NOLOGO`, `DOTNET_CLI_TELEMETRY_OPTOUT`, `DOTNET_CLI_USE_MSBUILD_SERVER`, verify.ps1:13-20)을 바꾸지 않습니다.
  - 공식 인증 검증 프로세스: `OfficialExecution` 인증 모드로 실행한 EvidenceVerifier 프로세스 하나. 승인 프로필은 이 프로세스에만 적용합니다.
- **E2-1 (초기 캡처).** 제출 증거, Trust Anchor, 승인 자료를 읽기 전에 초기 실행 환경 `C0`를 수집합니다. `C0` 수집에 필요한 OS·런타임 파일 읽기는 허용합니다.
  - 수집 항목: 환경 블록 digest, OS 버전·아키텍처, 프로세스 아키텍처, `FrameworkDescription`, `Microsoft.NETCore.App` 버전, RID, `ucrtbase.dll` 버전과 SHA, `System.Private.CoreLib`·`clrjit`·`coreclr`의 SHA
  - 환경 블록 digest: 이름을 대문자로 바꾼 `NAME=VALUE\n`을 이름 ordinal 순으로 이어 붙인 UTF-8 바이트의 SHA-256
  - 이 단계에서는 프로필 없이 판단할 수 있는 정적 조건만 검사합니다. 이름이 대소문자 구분 없이 `DOTNET_` 또는 `COMPlus_`로 시작하는 변수가 있으면 즉시 `environment_mismatch`로 종료합니다. 프로필 비교는 하지 않습니다.
- **E2-2 (Trust Bootstrap).** §16 ①~③. 실패하면 `approval_mismatch`(`Trust.*`)입니다.
- **E2-3 (승인 환경 확보).** §16 ④~⑧: 승인 기록, Environment Profile·Build Provenance, ID·Raw SHA·등록 SID, PATCH-04 결속. 실패하면 `approval_mismatch`입니다.
- **E2-4 (적합성).** `C0`를 승인 프로필과 정확히 비교하고, `C1`을 다시 수집해 `C1 == C0`(환경 블록 digest 포함)인지 확인합니다. 다르면 `environment_mismatch`이며, 다른 프로필로 자동 전환하지 않습니다.
- **E2-5 (비트 Golden).** 같은 프로세스에서 `upper_quantile` 36비트와 `witness_digest`를 계산·비교합니다. 하나라도 다르면 `environment_mismatch`입니다.
- **E2-6 (실제 검증).** E2-1~5가 모두 성공한 뒤에만 AMPV → Replay → Metrics → PZL 재관찰 → Test Profile 자식 실행을 시작합니다.
- **E2-7 (발급 직전).** 영수증 영속 저장(§20.2 단계 5) 직전에 `C2`를 수집해, `C2 == C0`이고 프로필에도 적합한지 확인합니다. 실패하면 영수증을 저장하지도 등록하지도 않습니다(`environment_mismatch`). 응답 유실 후 **새 프로세스**에서 재시도하면 등록(단계 6) 전에 E2-1~E2-5를 다시 수행합니다.
- **E3.** 실패 코드는 `Evidence.environment_mismatch`이며, 정상 인증을 거부합니다.
- **E4.** Test Profile 자식 `dotnet test`의 환경 블록에서는 금지 접두사 변수를 제거합니다. dotnet CLI가 자기 자식에게 직접 설정하는 변수는 검사 범위 밖입니다. Test Profile은 PASS/FAIL 판정용이며 비트 재현의 근거가 아닙니다.
- **E5.**
  - xUnit은 환경 적합성 판정 함수를 순수 함수(환경 스냅샷 + 프로필 입력)로 시험합니다.
  - 비트 Golden xUnit은 `verify.ps1` 환경에서 회귀 경보로만 쓰며 인증 근거가 아닙니다.
  - 비트 재현성은 E2-5만 근거로 삼습니다.
  - Dominance 계산식과 F64 규칙은 바꾸지 않습니다.
- **추가 규칙:**
  - 제출자 쪽 계산 환경은 신뢰하지 않습니다. 제출 F64가 검증기 재계산과 비트까지 같지 않으면 `evaluation_mismatch`입니다.
  - 신뢰되는 적합성은 위 프로필 필드에만 해당합니다.
  - 머신 이름·CPU·메모리·시각·`environment_not_hashed` 등은 `DIAGNOSTICS`(VERIFIER_DERIVED)로만 보존하고 A+에서 제외합니다.

---

## §19. 인증 상태표 (S00 §6.1·6.2)

**네 상태 축 (Gate를 인증 상태로 덮어쓰지 않음):**
- 기술: 정상 완료 / engine·metrics 실패 / 무결성 중단
- 통계: PASS / FAIL / INSUFFICIENT_SAMPLE / NOT_EVALUATED / INCOMPLETE
- PZL: 필수 PASS / 정당한 비해당 / DEFERRED / 위반 / 관찰 누락
- 인증: 미검증 / 부적격 / 등록 대기 / 등록 완료

| 기술 | 통계 | PZL | 인증 |
|---|---|---|---|
| 승인대로 정상 완료 | PASS | 필수 PASS·허용된 비해당·12b DEFERRED | 발행·등록 가능 |
| 승인대로 정상 완료 | 통계적 FAIL | 동일 | 실패 결과의 증거로 발행 가능 |
| 승인된 정상 종료 | INSUFFICIENT_SAMPLE | 동일 | 아래 조건 충족 시 가능 |
| 정상 완료 | NOT_EVALUATED | 동일 | 거부 (v1 허용 사유 목록 비어 있음) |
| 정상 완료 | Dominance INCOMPLETE/INCONCLUSIVE | 동일 | 거부 |
| engine/metrics 계산 실패 | Gate가 FAIL이어도 | 임의 | v1 정상 인증 거부, 실패 원본 보존 |
| 취소·미승인 중단 | 임의 | 임의 | 거부 |
| 무결성 실패·Replay 불일치 | 임의 | 임의 | 거부 |
| 필수 관찰 누락 | 임의 | 불완전 | 거부 |
| 비무결성 PZL 평가 위반 | 임의 | FAIL | 거부 (v1 허용 목록 비어 있음) |
| 검증 적격·등록 미확인 | 적격 | 적격 | 등록 대기, 인증 완료 표시 금지 |
| TEST_ONLY 권한 | 임의 | 임의 | `test_only=true` 영수증만 발급, 인증으로 계산하지 않음 |

**표본 부족 조건부 허용 (모두 필요):**
- 승인 기록에 `certification_allowances.insufficient_sample=true`가 있음
- 모든 필수 Unit과 승인 계획이 존재함
- 선택적 실행 중단이 아님
- 정규 Stop을 재도출할 수 있음
- engine·metrics·Replay·PZL 무결성 오류가 없음
- 정상 `SEEDS_EXHAUSTED` 또는 승인 최소 분모 부족을 정확히 기록함
- Phase Gate를 PASS로 표시하지 않음

**기타:**
- `NOT_APPLICABLE`은 본 명세가 대상과 이유를 정한 경우(예: NonAutomated Unit의 ABS Framing 검사)에만 허용합니다.
- 진단 Metric의 NOT_EVALUATED, Holm 중단 후 NOT_APPLICABLE, #12b DEFERRED를 필수 공식 Gate의 미평가와 혼동하지 않습니다.
- 기술 실패를 거부하는 것은 v1의 보수적 발행 범위일 뿐이며, 기존 Gate 정책을 바꾸지 않습니다.

---

## §20. 영수증·발행·등록 (S00 §6.3·6.4 + P-15 + E2-7)

### 20.1 `simharness-verification-receipt-v1`

Hash 계약 v1에 종속되며, 여섯 번째 독립 계약이 아닙니다.

| 필드 | 정의 |
|---|---|
| `format` | `simharness-verification-receipt-v1` |
| `receipt_id` | 소문자 UUID-D, 재시도 시 유지 |
| `validation_set_id` | 승인 집합 ID |
| `approvals` | 승인 ID·승인 SHA·Freeze ID |
| `contracts` | 5-tuple VER |
| `verifier` | `{id, version, source_git_sha: GIT, binary_sha256: H}` (`profile_id`는 삭제됨, P-15) |
| `build_provenance`, `environment_profile`, `pzl_test_profile` | 각각 `{id, raw_sha256}` |
| `execution_authorization_id` | `ID` |
| `test_only` | bool |
| `verification_decision` | `ELIGIBLE` |
| 기술·통계 상태 | **`targets[]` 각 항목에 둠** (R-11, 아래). Root의 단일 `technical_status`·`statistical_status`는 삭제 |
| `pzl_status` | `REQUIRED_CHECKS_SATISFIED` |
| `targets` | `Target[]` |
| `checks` | 실제로 검사한 Check[] |
| `source_files` | `SourceFile[]` |
| `child_receipt_ids` | Family는 `[]`, Phase는 정확한 세 자식 |

- `Target = {kind: CALIBRATION|INTEGRATION|DOMINANCE|PHASE, run_id: RUN?, evidence_id: ID, evidence_hash: AH, technical_status, statistical_status}`
  - Phase의 `run_id`는 null입니다.
  - `technical_status`: `COMPLETED`만 허용합니다(v1 발급 범위) (R-11).
  - `statistical_status` (R-11):

    | target kind | 허용값 |
    |---|---|
    | CALIBRATION, INTEGRATION (Family) | `PASS` \| `FAIL` \| `INSUFFICIENT_SAMPLE` |
    | DOMINANCE | `PASS` \| `FAIL` |
    | PHASE | **`null`** (Phase Gate는 FC-D12의 독립 리뷰와 소유자 승인 대상) |

  - Phase 영수증은 FAIL 자식을 포함할 수 있습니다(실패 증거 묶음). 다만 Gate PASS로 표시하지 않습니다.
  - Family 영수증은 target 1건, Phase 영수증은 Calibration·Integration·Dominance·Phase 순서의 4건입니다.
- `SourceFile` = ArtifactRef 7필드 + `origin: ORIGINAL|VERIFIER_DERIVED`, `artifact_id` ordinal 순서, 중복 거부
- 영수증에는 자기 SHA나 후속 기록부 항목 SHA를 넣지 않습니다. 영수증 원본의 Raw SHA는 기록부가 보존합니다.

### 20.2 발행 순서

1. 검증 스냅샷을 확보합니다. Hash·파싱·Replay가 같은 바이트를 씁니다.
2. 모든 필수 검사를 수행합니다(E2-6 순서).
3. 의미 Payload를 생성하고 제출값과 비교합니다. 미검증 값을 Hash에 넣지 않습니다.
4. A+·원본 SHA 목록·Canonical Payload·검증기 출처를 영수증에 결속합니다.
5. **E2-7 재확인 후** 새 이름으로 영속 저장합니다. flush·재읽기·SHA 확인을 하며 덮어쓰지 않습니다.
6. 독립 기록부에 원자적으로 등록합니다(ID·Raw SHA·전체 내용).
7. 기록부를 재조회해 ID·SHA·A+·승인·검증기·원본 목록이 일치할 때만 인증 완료입니다.

### 20.3 등록 동작

| 상황 | 처리 |
|---|---|
| 새 ID | 한 번만 영속 등록 |
| 같은 ID·같은 SHA·같은 내용 | 멱등 성공 |
| 같은 ID·다른 내용 | conflict, 교체 금지 |
| 등록 실패 | 미등록 또는 등록 대기 |
| 응답 유실 | 같은 영속 영수증으로 조회·재시도 (새 프로세스면 E2-1~5 재수행) |
| 재조회 불일치 | conflict 또는 무결성 실패 |
| 권한 없음 | 거부. 로컬 JSON으로 대체 금지 |
| 일부 자식만 등록 | 개별 Family 증거에 한정, Phase 인증 불가 |

기록부 최소 의미: `receipt_id, receipt_raw_sha256, receipt_content, registry_sequence, registrant_principal_id` (§15 테이블로 구현).

---

## §21. Archive 보존·검증 목록 (S00 §7 + P-12·P-13)

파일명과 실제 출력 루트는 B-4에서 정합니다. 여기서는 역할과 개수만 고정합니다.

| role | 필수 개수·단위 | 시점 | Raw SHA | Git 보존 |
|---|---|---|---|---|
| `APPROVAL_RECORD` | 검증 집합의 승인 버전 1 | 실행 전·검증 시작 | 외부 Pin 필수 | 소규모 승인 자료 |
| `BUILD_PROVENANCE` | 승인 build ID별 1세트 | 실행 전·독립 검증 | 필수 | 출처 요약 |
| `INPUT_BUNDLE` | 소비 입력 파일별 | build/run 전·검증 | 필수 | 입력 정책 |
| `SEED_MANIFEST` | 승인 Unit별 정확히 1 | 실행 전·블록 전·독립 검증 | 외부 Pin 필수 | 승인 Seed 보존 |
| `PREFLIGHT` | Family·Unit별 필요 기록 | 첫 경기 전 | 필수 | 요약 가능 |
| `UNIT_MANIFEST` | Unit별 1 | Unit 종료 | 필수 | 소규모 가능 |
| `ATTESTATION` | Unit별 1 (Manifest 내부 허용) | Preflight·저장 | 소속 파일 보존 | 가능 |
| `GAME_RESULTS` | Unit별 실행 경기 bundle 1 | Unit 종료 | 필수 | 대용량은 Archive |
| `GAME_EVENTS` | **Unit PZL 문서마다 정확히 1**, `simharness-game-events-v1`, ArtifactRef `unit_id` non-null·`game_id` null | Commit 후 수집·종료 후 봉인 | 필수 | 원칙적으로 Archive |
| `PREPARED_OBSERVATIONS` | **RESERVED / UNUSED** (투구 관찰은 GAME_EVENTS의 `EventObservation.pitch`에 있음) | — | — | — |
| `FAMILY_RESULT` | Calibration·Integration 각 1 | 전체 Unit 후 | 필수 | 보존 |
| `DOMINANCE_RESULT` | 1 | 48 Unit 후 | 필수 | 보존 |
| `UNIT_PZL` | Unit별 1 | 관찰 후 | 필수 | 공식 결과 보존 |
| `PZL6` | Integration 1 + 20경기 JSON bundle | 고정 표본 검사 후 | 필수 | 공식 결과 보존 |
| `REGRESSION_PROOF` | 승인 검사 프로파일별 1세트 | 필수 검사 시 | 필수 | 요약 보존 |
| `DIAGNOSTICS` | 채택한 summary·로그별 | 실행 시 | 채택 파일 모두 필수 | 대용량은 비Git |
| `CANONICAL_PAYLOAD` | 발행 Family별 1·Phase 1 | 검증 성공 후 | 필수 | 가능 |
| `RAW_INVENTORY` | 검증 스냅샷별 1 | 원본 고정 후 | 자기 자신 제외 | 가능 |
| `VERIFICATION_RECEIPT` | 발행별 1 | 등록 전 | 기록부 보존 | 사본 가능 |
| `REGISTRY_RECORD` | 영수증별 1 | 원자적 등록 | 기록부 자체 관리 | 정본은 독립 관리 |

- Attestation이 Manifest 내부에 있으면 원본 파일 SHA와 JSON Pointer로 위치를 지정하며, 별도 중복 파일을 강제하지 않습니다.
- **자기 참조 제외:**
  - RAW_INVENTORY에는 자신·영수증·후속 기록부 항목·나중에 생성된 Canonical Payload를 넣지 않습니다.
  - 영수증 `source_files`에는 원본과 검증기 생성 Canonical Payload·Inventory(VERIFIER_DERIVED)를 넣을 수 있지만, 영수증 자신과 후속 기록부 항목은 넣지 않습니다.
- **보존 규칙:**
  - 같은 RunId·영수증 ID의 파일은 덮어쓰지 않습니다.
  - 생성·Archive 확정·검증 스냅샷 시점의 바이트를 대조합니다.
  - Hash한 뒤 같은 경로를 다시 열어 다른 파일을 파싱하지 않습니다.
  - 정확한 바이트를 Git에 넣어야 하는 선택된 증거에만 `-text`를 적용하고, Git blob과 Archive SHA를 바이너리로 비교합니다.
  - PowerShell 문자열 파이프를 바이너리 보존 수단으로 쓰지 않습니다.
  - 과거 CRLF 로그를 고쳐 당시 SHA에 맞추지 않으며, 기존 원본과 차이 설명을 함께 보존합니다.
  - 보존 기간·자동 삭제 규칙은 추가하지 않습니다.
- **Artifact DAG (R-13):**
  - 참조는 `Family/Dominance → Unit PZL/PZL6 → 번들/Manifest` 방향으로만 허용합니다. 하위 문서는 상위 문서의 Raw SHA를 참조하지 않습니다.
  - 작성 순서: 번들 → Unit PZL → PZL6 → Family/Dominance → RAW_INVENTORY → CANONICAL_PAYLOAD → 영수증
  - 각 Unit의 SEED_MANIFEST artifact는 정확히 1개이며, `raw_sha256 == attestation.seed_manifest_sha256 == 승인 기록 Pin`이어야 합니다.

---

## §22. 기존 코드·ADR 정합성과 불변

| 기존 계약 | 적용 |
|---|---|
| game-result-v1 | 기존 값을 재계산·참조하며 대체하지 않음 (GameExecutor.cs:191) |
| run-result-v1/v2 | Calibration v2, Integration·Dominance v1 (SimRunner.cs:31) |
| ConfigHash | 전체 승인 계획으로 기존 함수 실행 (RunModel.cs:133) |
| Attestation v1 | 21필드와 기존 Hash 유지 (Official.cs:80) |
| Seed 신뢰 경계 | 외부 Pin → 실제 바이트 → 계획 → ConfigHash (SeedManifest.cs:121) |
| Stop/Replay | 기록된 Stop을 믿지 않고 재도출 (StopRule.cs:45). 오류 우선순위는 §14.7 |
| B6-01 / B6-02 | 계산 실패 층의 상태·원인 보존 / Manifest PA·투구 수와 재실행 비교 |
| Calibration 계산 실패 | SR-D9.2 NOT_EVALUATED 유지 |
| Integration·Dominance 계산 실패 | 기존 FAIL 유지, 인증 축과 분리 |
| 진단 지표 | DEFF·SE·Wilson 등은 A+에서 제외 |
| ef1/ef2, RNG | 기존 Canonical·영수증 비교, rngset1 원문 유지 |
| DRAFT | 기존 문서를 변경·승격하지 않음 |
| PZL #12b | DEFERRED 고정 |
| Phase 최종 Gate | A+와 별개로 독립 검토·소유자 승인 필요 (ADR-008:137) |
| `OfficialApprovals.Current`·CLI | 변경 없음, NOT_READY 유지 |
| 기본 Replay 모드 | 결과·오류 코드가 리팩터링 전과 바이트 동일 |

**변화 0이어야 하는 것:** game-result-v1, run-result-v1/v2, ConfigHash, Attestation v1, ef1/ef2, DRAFT 색인 바이트, 정수 통계·RNG·CAS·공식 기록 규칙

## §23. 운영 값·외부 정책 (기술 계약 미완성과 별개, 미확정이면 Fail-Closed)

| 항목 | 처리 |
|---|---|
| B-6a Integration Cohort | 외부 승인 조회. 실행 정책 Hash에 포함된 것으로 오인 금지 |
| B-2/B-6b Unit–Seed 매핑 | 승인 Unit·외부 Pin으로 전체 계획 도출 |
| B-6c/B-6d 결합 정책 | 승인 조항 ID 필수, DRAFT 구현 자동 승인 금지 |
| B-3 실행 상한·Dominance 표본 수 | 외부 승인값 필요 |
| B-4 빌드 출처·출력 루트 | 출처·권한·안전한 보존이 성립하기 전에는 거부 |
| B-5 InputsHash | 승인값과 재계산 결과 대조 |
| B-7 Pin | CLI·결과 파일의 자기 선언으로 대체 금지 |
| P-1 공식 #6 표본·Pin | 승인 Integration의 모드별 첫 10경기, 실제 값 미지정 |
| 기타 B-2~B-8·P | 대응 승인 항목이 없으면 Fail-Closed |
| 운영 실측값 | 승인 기록·실행 허가·`validation_set_id`·`run_id`, Seed 경로·Pin, 기록부 계정·SID·Pipe 이름·DB 경로, 승인 환경의 OS·런타임·`ucrtbase` 버전·SHA, 비트 Golden 36개와 `witness_digest`, Fixture blob SHA, 전체 문서 Golden |

**소유자 채택 대상(S00 §10):**
1. 닫힌 4Schema와 Hash v1
2. v1 인증 적격 상태표
3. 사건별 PZL 관찰과 검사 프로파일
4. 독립 신뢰 기록부의 관리 주체·권한 분리·영속 등록
5. 완전한 Golden의 독립 기대 바이트·digest

---

# 2부. 출처 추적 및 대체 관계표

## 2.1 조항별 출처

| 최종 조항 | 기본 출처 | 후행 대체·보강 | 처리된 구 규범 |
|---|---|---|---|
| §1.1 해석 규칙 | S00 §2.1 | — | — |
| §1.2 스칼라 | S00 §2.1 | S01 §1.1, P-03; S04 R03-1 SID 문법 | `TEXT` → `TEXT1`/`CODE`/`RNGSET1`/`EVCANON` |
| §1.3 오류 | S00 §2.1 | P-16; S02 T7; S03 R03-3·R01-B; S04·S05·S06 원인 코드 | 14개 목록 → 17개 |
| §2.1 Schema·contracts | S00 §2.2 | P-01, P-02 S1·S2, **S02 PATCH-02** | 정수 `1`, 6키 contracts(`version` 포함) |
| §2.2~2.4 Execution·Approvals·결속 | S00 §2.2 | P-02 S1~S6 (R-14 대체) | 부모·자식 원본 바이트 일치 요구 |
| §2.5 AdditionalEvidence | S00 | — | — |
| §3 Family | S00 §2.3 | P-03, P-05, P-06(§1.3), P-07, P-08 | run_status 5값, `abort_code`, nullable run_result, Cancelled 상태, `metrics_computed`(Evaluation) |
| §4 Dominance | S00 §2.4 | P-10(R-7) | Witness 정렬 규칙 `(rival 순번, column)`, `games_per_cell: P?`, Cell Hash `H?` |
| §5 Unit PZL | S00 §2.5 | P-04, P-11, S01 §3.2·§3.6 | `PzlGame.events[]`, "완료 경기만", `drawn_streams: EXACT[]` |
| §6 PZL #6 | S00 §2.6 | — | — |
| §7 Check | S00 §2.7 | P-03, P-14 | TestEvidence `profile_id`만·`SKIP`·`reason` |
| §8 Canonicalization | S00 §3 | (S01 §5.1 직렬화 구분) | — |
| §9 A+ | S00 §4 | P-01·PATCH-02 (contracts) | — |
| §10 GAME_EVENTS | S01 §3 (OD-2) | **S02 PATCH-06** | 사건 ≥1을 전제한 단일 RNG 연결 문장, 시작 전/후 미구분 문장 |
| §11 PZL 매핑 | S00 §5 | S01 §4.3 (R-9) | — |
| §12 Test Profile | S01 §4 | P-14, PATCH-04, PATCH-05 E4 | — |
| §13 Approval Record | S01 §2.1 | **PATCH-02**, **PATCH-04**, PATCH-01 | 6키 contracts, `source_git_sha` 일괄 비교, `ExecutionPathReady = authority ≠ NOT_AUTHORIZED` |
| §14 권한·Replay·AMPV | S01 §2.4 | S02 PATCH-01 (R01-1~4) → **S03 PATCH-01 (R01-A~D)** | "순서만 바꿉니다", R01-4 일괄 감싸기, 조건 3의 Preflight PASS 조항, "실행 허가 없는 공식 결과는 검증 불가" |
| §15 Registry | S01 §2.2 (OD-3) | S04 R03-5, S06·**S07 R03-5** | "서비스가 호출자 SID를 주체로 매핑"(일반 서술), 증거 작성자 전원 읽기 가능 |
| §16 Trust Bootstrap | S01 §2.3 L1~L8 | S02 PATCH-03 T1~T7 → S03 R03-1·T3 대체 → S04 | L1 `TRUST_CONFIG` 앵커, T1 `registry_endpoint`·`owner_principal_ids` TEXT1, T3 엔드포인트 연결 |
| §17 R03-1 | S03 R03-1 | S04 → S06 → **S07 (R-A)** | S03 `registry_service_sid` 서버 신원, 범용 SID 문법, Reader 배열 "길이 0도 허용" |
| §17 R03-2 | S03 R03-2 | **S04 R03-2** | S03 서비스 SID 기반 DACL |
| §17 R03-3 | S04 R03-3 | **S05 S1~S7**, **S06 Reader ACE** | "항상 대기 인스턴스 1개 이상", "예: 4" |
| §17 R03-4 | S03 R03-3·R03-4 | S04 → **S05 MINOR-01 (5단계)·C1~C6** → **S06 (1단계 분리)** → **S07 (4(d))** | `GetNamedPipeServerProcessId`, `TokenImpersonationLevel.Identification` 스트림, 4(d) `m & ~0x00120083 == 0` 부분집합 규칙, "계정 제한 없음", 4(e) |
| §17 R03-5 | S03 R03-5 | S04 → S06 → **S07 (R-C)** | 역할 매핑 출처 미정의 서술 |
| §17 R03-6 | S03 R03-6 | S04 R03-6, S06 Reader 운영 전제 | — (S03의 "검증기는 SQLite 직접 읽지 않음"은 §15로 유지) |
| §18 환경 | S01 §5 | PATCH-04, S02 PATCH-05 (E1·E3·E4·E5 유지) → **S03 E2-1~7** → **S04 E2-1 문구** | 범위 없는 금지 변수 문장, S02 E2(a)~(c), "어떤 파일도 읽기 전에" |
| §19 상태표 | S00 §6.1·6.2 | S01 §2.4 (TEST_ONLY) | — |
| §20 영수증 | S00 §6.3·6.4 | P-15, S03 E2-7 | `verifier.profile_id`, 단일 `technical_status`/`statistical_status` |
| §21 Archive | S00 §7 | P-12, P-13 | `PREPARED_OBSERVATIONS` 사용 |
| §22·§23 | S00 §9·§10 | S01 §7, S02 D, S03 D | — |
| 문서 지위 | — | **S08** | S00 §1 "구현과 Golden 제작 시작 가능", S00.5 §9 "Implementation-Ready: 아니오(조건부)", S01 "Codex 원문 대조 PARTIAL", S06 자기 검수 `CONDITIONAL` 등 과거 판정 |

**S00.5 반영 위치 (보완판 추가분):**

| 최종 조항 | S00.5 출처 | 비고 |
|---|---|---|
| §0.1 대체 순서 | 문서 시점·S01 §1.2 | S00 → S00.5 → S01 |
| §1.1 규칙 9 | §3 F-02 Errata, R-2 | 승인 입력 ①~③ 포함 명시 |
| §2.1 R-1 유지 조항·v1 대응표 | R-1, §3 F-01 Errata | 5-tuple 정의는 PATCH-02가 최종 |
| §2.2 `evidence_id` 도출식 | R-12 | NSC-1 해소 |
| §2.4 잔여 필드 간 제약 | R-14 (F-14) | NSC-3 해소. 바이트 일치·TEXT 문장은 대체됨 |
| §3.4 Technical·Stop | R-4 | P-07에 없는 Complete 생성 조건·Stop 상시 존재·StatusOf 도출 |
| §3.7·§3.8 | R-5 | Calibration policy 상시 non-null, Integration `evaluation_computed`, OfficialMetric 조건 |
| §4 Dominance | R-7, §3 F-12·F-13 Errata | frozen 일치, sample⇔Ok, SideResult·label, F64 상태, INCOMPLETE 조건, Witness 생성 순서 상세, UNDEFINED 예약 |
| §7 Check | R-9 | Check별 허용 status·`test_evidence` 필수표 |
| §8.4 F64 reason | R-7, F-13 Errata | |
| §9.5 결속 5분류 | R-2 | |
| §13.1 머리말 | R-10 | 필드 집합은 S01 §2.1이 대체 |
| §20.1 Target 상태 | R-11 | NSC-1 해소 |
| §21 Artifact DAG | R-13 | NSC-1 해소 |
| 3부 N01~N14 | §7 신규 반례 | NSC-2 해소. N12는 S01 §5.2 최신 조건 우선 |
| 3부 Golden | §7 | GV-C02 DOMINANCE·PHASE, binary64 경계 사례 |

## 2.2 해석이 필요했지만 행동 차이가 없는 항목

- **R01-1 조건 3.** S03은 조건 3의 Preflight 조항만 삭제했습니다(인용문 앞에 `…` 표시). 동시에 R01-D에서 "조건 3은 AMPV PASS라는 뜻"이라고 했습니다. 그래서 남은 문구(허가 기록의 승인 ID·SHA 일치)와 AMPV를 함께 적었습니다. 허가 기록의 SHA 일치는 조건 2(§14.2)가 이미 요구하므로 두 해석의 요구 집합은 같습니다.
- **재연결 시작 단계.** S04 9단계는 "1단계부터", S05와 S03 B-3은 "2단계/②부터"라고 합니다. 후행 S05를 따랐습니다. 1단계(자기 TokenUser)는 프로세스 안에서 변하지 않으므로 결과는 같습니다.
- **`frozen_games_per_cell`.** S00.5 R-7과 S01 P-10에 따라 타입은 `P`(non-null)로 확정되었습니다. 또한 `frozen_games_per_cell == plan.games_per_cell`이어야 합니다(§4). S00의 `P?`는 대체되어 남지 않습니다.
- **Unit PZL `plan.run_result_version`·`plan.run_result_hash`.** 기존 Schema의 nullable 표기(`?`)를 유지합니다. 다만 정상 인증 문서에서는 Complete 부모 실행의 non-null 값(P-07)과 같아야 하므로 항상 non-null입니다.

## 2.3 `SOURCE_CONFLICT / NEEDS_SOURCE_CHECK`

**해결 판정 (보완판):**

| ID | 원래 공백 | 판정 | 근거 |
|---|---|---|---|
| **NSC-1** | R-1·R-2·R-3·R-4·R-5·R-7·R-10·R-11·R-12·R-13 전문 미수록 | **RESOLVED** | S00.5 §4에 전문이 있습니다. 항목별 대조는 §2.4입니다. R-6은 원문에도 없음(RESERVED). |
| **NSC-2** | N01~N14 원문 미수록 | **RESOLVED** | S00.5 §7 "신규 반례와 회귀 테스트"에 14건 원문이 있습니다. 3부 §3.3에 복원했으며, N12는 S01 §5.2 최신 조건을 우선했습니다. |
| **NSC-3** | R-14 잔여 동치 제약의 정확한 규칙 | **RESOLVED** | S00.5 R-14에 7개 축의 정확한 등식이 있습니다(§2.4 반영). |
| **NSC-6** | `Check.coverage` 의미. R-8의 coverage 두 문장이 P-11 이후에도 유지되는지 원문만으로 확정할 수 없었음(S01 P-11 패치 행은 coverage를 언급하지 않고, §1.2는 R-8 전체 대체라고 함) | **RESOLVED (OWNER 결정)** | TASK.md(2026-10-24)의 OWNER 결정으로 R-8의 `completed_games`·`events` 정의를 잔여 의미 규범으로 명시 채택했습니다. `pitches`·`called_pitch_steps`와 범위 규칙은 신규 OWNER 결정입니다. 규범은 §7 "coverage 의미와 계산 범위"에 있습니다. |

**남은 항목:**

| ID | 원문 위치 | Issue | Reason | Recommended Option | Alternative | Impact |
|---|---|---|---|---|---|---|
| **NSC-4** (하) | S02 PATCH-03 T2 | 앵커 ACL 검사의 "증거 작성자 주체" 집합 정의가 없습니다. S00.5에도 다루는 문장이 없습니다. | 열거 가능한 SID 집합이 있어야 ACL을 검사할 수 있습니다. | OWNER가 운영 체크리스트에 대상 SID 집합을 명시(예: `evidence_reader_sids` 포함 여부) | "TCB 외 모든 주체에게 쓰기·삭제·ACL 변경 불가"로 해석하도록 명시 | §16 T2, N25. **미해결 구현 Gate:** T06a Trust Anchor 검증 구현과 운영 배치 전에 반드시 해결합니다. 미해결 상태에서는 N25와 T2 관련 수용 시험을 통과 처리하지 않습니다. |
| **NSC-5** (하) | S04 R03-5, S07 R03-5-1 | `simharness-registry-service-config-v1`의 SQLite 경로 필드 이름·타입이 없습니다. S00.5에도 없습니다(S00.5 시점에는 이 설정 형식이 없었음). | 닫힌 Schema에는 필드 이름이 필요합니다. | T09 착수 전에 OWNER가 필드명을 지정 | — | §17 R03-5-1, AT-14n. **미해결 구현 Gate:** T09 Registry 서비스 설정 Reader/Writer 구현 전에 반드시 해결합니다. 미해결 상태에서는 AT-14n·AT-14r과 R03-5-1·2 관련 수용 시험을 통과 처리하지 않습니다. |
| INFO-1 | 전체 | T01~T11 TASK 정의는 TASK.md와 S00.5 §6에만 있습니다. 규범 본문에서는 참조 ID로만 씁니다. | — | 승인 시 TASK.md SHA로 결속(4부) | — | 규범 영향 없음 |

> 위 항목 외에 S00·S00.5·S01~S07 사이의 **실제 규범 충돌은 발견하지 못했습니다.** 이는 S08의 "잔여 계약 충돌 없음"과도 일치합니다.

## 2.4 R-1~R-14 항목별 대조 (S00.5 → 최종 본문)

| R | S00.5 원문 요지 | 후행 처리 (S01~S07) | 최종 본문 |
|---|---|---|---|
| R-1 | `VER` 문자열, ordinal 비교. `schema.version == contracts[역할]`. Payload 내 contracts 바이트 동일. 허용 조합 = 승인 기록 5-tuple. 버전별 검증기 레지스트리("latest" 금지), Pin된 검증기로 재검증. `event_format_version`은 정수 유지 | S01 P-01·P-02 S2·S3 유지. S01 §2.1의 6키 contracts는 S02 PATCH-02가 5키로 복귀(R-1과 일치) | §2.1 (전 문장 반영) |
| R-2 | M(D) 유지. 결속 5분류. 동일성 범위. 규칙 9 문구 수정(F-02) | 대체 없음 (S01 F-02 "R-2 (5분류) 유지") | §1.1 규칙 9, §9.5 |
| R-3 | GameResult 상태표 4행, stage 닫힌 목록, `last_receipts` ≤12, Family Regulation null, null→`"-"` | **S01 §1.3(P-06)이 상태표 전체를 대체.** B행 CrossCheck 조건은 R-3의 "`error_code=Harness.crosscheck_mismatch`"에서 S01의 "stage가 `crosscheck`일 때"로 바뀜. null→`"-"` 범위는 P-05가 3필드로 한정 | §3.6 (S01 기준, R-3 문장 복원 안 함) |
| R-4 | run_status 3값, `abort_code` 삭제, run_result 고정, cancelled·not_started=0, Calibration `stop` 항상 non-null, Complete일 때만 생성, Family 상태는 `StatusOf(units)` 도출 | S01 P-07이 앞의 4항목을 재확인. 나머지 3항목은 대체 없음 | §3.4 (전 문장 반영) |
| R-5 | Calibration policy 상시 non-null(FcEvaluationV1), `evaluation_computed`·`error_code`·`shares`·`metrics` 조건. Integration 조건. OfficialMetric 3조건 | S01 P-08 "R-5 유지(이름 변경)". S00의 "계산 성공 시 policy 필수"를 대체 | §3.7·§3.8 |
| R-6 | (원문에 없음) | S01 P-09 RESERVED/UNUSED | §3.9 |
| R-7 | games_per_cell·frozen `P`·일치, Cell Hash `H`, sample⇔Ok, SideResult·label 규칙, Witness F64 상태, INCOMPLETE 조건 | S01 P-10 "R-7 유지". Witness 생성 순서·F64 `{HOLM_STOPPED}`·UNDEFINED 예약은 S00.5 §3 F-12·F-13 Errata | §4, §8.4 |
| R-8 | Unit PZL 전 실행 경기, `status`, `events.length`, 미시작 경기 null, coverage 정의, `observation_present` 상수, 옵션 B `events_digest` | **S01 P-11이 R-8을 대체**(옵션 B 채택, `events[]` 삭제·GAME_EVENTS 번들). `events_digest` 식은 S01 §3.4 형식이 최종. **예외:** coverage 정의 두 문장(`completed_games`=Completed 수, `events`=전체 사건 수)은 OWNER가 잔여 의미 규범으로 명시 채택(NSC-6, TASK.md 2026-10-24) | §5.1, §10. R-8의 나머지 문장은 복원하지 않음. coverage 두 문장은 §7에 반영 |
| R-9 | Check별 허용 status·`test_evidence` 필수표 | S01 §4.3 "R-9 유지"(증거 유형 표 추가) | §7 표, §11.2 |
| R-10 | 승인 기록 초안 필드 (`build_provenance_id`, `pzl_test_profile_id` 등), 저장소 밖 조회, `Current` 불변, `test_only` 인증 불가 | **필드 집합은 S01 §2.1 + PATCH-02가 대체**(`{id, raw_sha256}` 참조 3종, UnitApproval 확장 등). 머리 규칙은 유지 | §13.1 머리말, §13.5, §14 (필드는 S01 기준) |
| R-11 | `targets[]`별 `technical_status`(COMPLETED)·`statistical_status`(Family/Dominance/Phase 별 허용값), Root 단일 상태 삭제, Phase는 FAIL 자식 허용 | S01 P-15 "상태는 target별(R-11)" | §20.1 |
| R-12 | `evidence_id` 4개 도출식, vs ≤64, 불일치 `reference_invalid` | S01 P-02 S6가 참조. `validation_set_id ≤64자`는 S01 §2.1과 일치 | §2.2, §2.4 S6 |
| R-13 | Artifact DAG 방향, 작성 순서, SEED_MANIFEST 1개·Pin 일치 | S01 P-13(GAME_EVENTS)은 별개 항목. 대체 없음 | §21 |
| R-14 | 필드 간 제약 9개 | **S01이 R-14를 P-02로 대체**하되 F-14 매핑에서 7개 축 잔여 제약 유지. 바이트 일치 문장은 P-02, TEXT 문장은 P-03이 대체 | §2.4 (잔여 7개만) |

**복원하지 않은 과거 규칙 (후행 PATCH 우선):** R-3 상태표 문장 전체, R-8 전체(단 coverage 두 문장은 OWNER 채택으로 유지, NSC-6), R-10 필드 목록, R-14의 부모·자식 바이트 일치·TEXT 문장, S00.5 §8 OD-1~OD-3 "결정 필요" 상태(S01에서 OD-1 문자열, OD-2 옵션 B, OD-3 SQLite로 반영됨), S00.5 §9 준비도 판정.

---

# 3부. Golden · 반례 · T09 수용 시험 · 22 Findings 누락 검사표

## 3.1 Golden

| ID | 내용 | 기대값 | 상태 |
|---|---|---|---|
| GV-C01 | 31바이트 `{"seed":"18446744073709551615"}` | Raw `dddc693f1087dccd961870d3e6f79997db44912d90c27b94906e43e56f8b8305`; FAMILY `038327f726000c97325b4fcb4a49d435a3ca7997c331aa2752d2690f2d2890c9`; DOMINANCE `5dacde4078f227d414373f550b3be24a731ce32e59fd33a63033fbabfcf79fb4`; PHASE `a6aa2c4fedee603823e2f2e01e28d33805f5b772d754be2e4092db0fce6d019d` | 2026-10-24 Claude 재계산(Python hashlib) **일치**. 단위 Vector이며 공식 증거로 발급 금지 |
| GV-C02 | 68바이트 `{"max":"1.7976931348623157e+308","min":"5e-324","negative_zero":"0"}` | Raw `8081fa9c30d301d53e313cc594722efa308f67a04d079512aca5c255876a773f`; FAMILY `28bebb45cbb16006668ec6460619a6d33dd15afe8479c0fa20fd8058575ca705` | 재계산 **일치**. 신규 구현 적합성 테스트는 아님 |
| GV-C02 (추가 digest, S00.5 §7) | 위와 같은 68바이트 | DOMINANCE `e581ac6c69c0d5d877f4ced827e51a373c63ac412d301eeb1083c4c154700c24`; PHASE `7d69df8a9cd3ad9d7d59a5bcc0bc74f81fc210a985cd986c214942f100d5fe61` | S00.5에서 1회 계산, 2026-10-24 Claude가 같은 방식(Python hashlib)으로 재계산해 **일치**. 별도 구현 대조(T10) 전이므로 **확정 Golden 아님** |
| F64 경계 (S00.5 §7 권장) | `1e15`, `1e-5`, `1.2345678901234568e17` | `"1000000000000000"`, `"0.00001"`, `"123456789012345680"` | S00.5 계산값. 2026-10-24 Node.js `String(x)` 출력과 **일치**(참고). .NET `"R"`과 출력이 다른 구간이므로 Golden 추가 권장. **확정 Golden 아님** |
| GV-EV0 | 빈 EventObservation 배열 digest | `sha256:ed666f17bb1a25a68a4457864105ec21fb879a61b7c3919422e1918b14e2ed6e` | 재계산 **일치** |
| ENV-UQ | `upper_quantile` j=1..36 비트 | — | **미생성** (승인 환경 운영 값) |
| ENV-WD | `witness_digest` | — | **미생성** |
| N12 동률 Fixture | Holm 동률 고정 | — | **미생성** |
| 완전 문서 Golden | Cal·Int·Dom·Phase 각 1세트 | — | **미계산·미검증** (B-1 구현 완료 조건) |

**완전 문서 Golden 제작 규칙 (S00 §8.2), 각 세트에 포함할 것:**
1. Production과 분리된 TEST_ONLY 신뢰 기록
2. 독립적으로 고정한 4Schema 입력
3. 독립 기대 Metrics·Gate·PZL·기존 Hash
4. §9 Payload
5. 기대 JCS 바이트
6. Prefix를 붙인 기대 SHA
7. 기대 오류 또는 등록 상태

Production Payload 생성기로 기대 바이트를 만들어 자기검증하지 않습니다. 실제 Cohort·Seed·Pin을 임의로 채운 digest는 만들지 않습니다.

## 3.2 GV-A01~A28 (S01·S02에서 근거 갱신)

| ID | 입력 | 기대 결과 | 최종 근거 |
|---|---|---|---|
| A01 | 다른 검증 집합의 정상 Dominance A+ 혼합 | approval_mismatch | §2.4 S3~S5, §13.3 run_id 유일, PATCH-01·03 |
| A02 | 같은 Git SHA, 다른 승인 집합·RunId 자식 | 거부 | §2.4 S3~S5, §9.2 |
| A03 | Seed Pin 변경 | 첫 경기 전 외부 Pin 불일치 거부 | §13.4, §16 L3~L4, PATCH-01·03 |
| A04 | Seed·GameId·ConfigHash·Attestation 동시 재작성 | 외부 승인 계획 불일치 | 같음 |
| A05 | Gate·Reason 변경 후 Raw SHA 재계산 | 독립 평가 불일치 | §3.6 상태표, P-07 |
| A06 | committed_events·end_kinds·error_codes·end_kind 개별 변경 | 기술 집계 불일치 | 같음 |
| A07 | 같은 건수의 다른 경기 PZL로 교체 | 식별·Hash·사건·재수집 불일치 | §10.3, §10.6 |
| A08 | 한 투구의 PreparedPitch 제거 | pzl_incomplete | 같음 |
| A09 | H1·roll·raw Draws 동시 위조 | H0 독립 재생에서 검출 | 같음 |
| A10 | #6 표본을 11번째 경기로 교체 | 고정 표본 규칙 위반 | §6, §11.2 #6 |
| A10-a | (NSC-6 하위 사례) `pzl-6` 또는 Phase Root #6의 coverage가 고정 20경기 범위가 아니거나 같은 경기를 중복 집계 | `pzl_mismatch` | §7 coverage 범위 |
| A11 | DRAFT ID·official_evidence만 변경 | 미충족 | §2.4 S1 |
| A12 | 비인증 OS·시간만 바뀐 스냅샷 | A+ 동일, Raw·영수증 다름 | R-2 ⑤(§9.5), R-13(§21), §5.1 |
| A13 | A12에 기존 영수증 재사용 | Raw 목록 불일치 | 같음 |
| A14 | 기록부 등록 실패 | 등록 대기, 인증 완료 금지 | §15, §20.3 |
| A15 | 등록 후 응답 유실 | 같은 ID·바이트로 재시도·조회 | 같음 (+E2-7 재수행) |
| A16 | 같은 영수증 ID·다른 원본 목록 | registration_conflict | 같음 |
| A17 | 증거 작성자가 자체 로컬 기록부 생성 | 신뢰 기록부로 인정 안 함 | §15 권한, §16 T1, PATCH-01·03; A17-a |
| A18 | 비Pin 로그 LF/CRLF 변경 | A+ 동일, Raw 변경 | R-2·R-13, §5.1 |
| A19 | Seed Manifest LF/CRLF 변경 | 승인 Raw Pin과 다르면 거부 | §13.4, L3~L4 |
| A20 | 필수 Archive 파일 누락 | archive_invalid | R-2·R-13, §21 |
| A21 | UInt64 상한 초과·선행 0·양수 기호 | schema_invalid | §1.2, §8.2 |
| A22 | count `9007199254740992` | schema_invalid | §8.3 |
| A23 | 중복 키·고립 surrogate·불량 UTF-8 | Canonicalization 전 거부 | §8.1 |
| A24 | `"NaN"`, `"1E+21"`, `"-0"` | 비정규 거부 | §8.4 |
| A25 | 필수 테스트 누락인데 PASS 주장 | 검증 범위 불충족 | §12.3 |
| A25-a | (NSC-6 하위 사례) Phase Root #4·#7·#8·#9·#11·#12b 또는 Unit `pzl-12b`의 coverage가 0이 아님, 또는 Test Case 건수를 coverage에 넣음 | `pzl_mismatch` | §7 coverage 범위 |
| A25-b | (NSC-6 하위 사례) Phase Root #10의 coverage에 NonAutomated Unit 관찰을 포함 | `pzl_mismatch` | §7 coverage 범위 |
| A26 | 검증된 통계 FAIL | 실패 증거로 인증 가능, Gate PASS 아님 | R-11(§20.1 `statistical_status=FAIL`), §19 |
| A27 | Metrics 계산 실패를 통계 FAIL로 위장 | 기술 상태 검출, 정상 인증 거부 | §3.6, P-07, §19 |
| A28 | 세 자식 중 하나 미등록 | Phase 인증 불가 | §20.3 |

(A-번호와 S00 GV-A 번호의 대응은 S01 §6.3 매핑의 내용이 GV-A 표와 일치하는 것으로 식별했습니다.)

## 3.3 N01~N36과 하위 사례

**N01~N14 (S00.5 원문 복원).** N01~N14의 "원문"은 S00.5 §7의 문장 그대로입니다. "최종 기대"는 원문에 명시되거나 최종 본문 규칙이 직접 정하는 결과만 적었습니다.

| ID | 원문 (S00.5 §7) | 최종 기대 | 근거 |
|---|---|---|---|
| N01 | `rng_hash=null`인 경기의 ResultHash `"-"` 재현 | 재계산 입력에서 null을 `"-"`로 복원해 기존 game-result-v1 Hash와 같아야 함. 복원 대상은 3필드뿐 | P-05(§10.7), §3.6 D행 |
| N02 | 완료 경기의 Regulation 오류 | Completed 경기에서 `regulation_half_innings=null`·`regulation_error_code` non-null이 허용되고, 두 필드가 정확히 하나만 non-null | §3.6 A행 |
| N03 | 실패 경기의 CrossCheck `passed=false` | `stage=crosscheck`인 Failed 경기에서만 CrossCheck non-null·`passed=false` | §3.6 B행 |
| N04 | 불완전 실행으로 공식 문서 생성 시 거부 | Complete가 아닌 실행에서는 공식 4Schema 문서를 생성하지 않음 | P-07, R-4(§3.4) |
| N05 | 실패 경기 관찰 누락 | 실패 경기도 Unit PZL `games`와 GAME_EVENTS에 포함되어야 하며, 누락은 `pzl_incomplete` | P-11(§5), §10.3·§10.6 |
| N05-a | (NSC-6 하위 사례) `coverage.events`에서 Failed 경기의 확정 사건을 뺌 | `pzl_mismatch` | §7 coverage (R-8 잔여 채택) |
| N05-b | (NSC-6 하위 사례) `coverage.completed_games`에 Failed 경기를 셈 | `pzl_mismatch` | §7 coverage (R-8 잔여 채택) |
| N05-c | (NSC-6 하위 사례) `coverage.pitches`에 `approach`·`runner_play`·`substitution` 사건을 넣거나 Failed 경기의 투구 사건을 뺌 | `pzl_mismatch` | §7 coverage (OWNER 결정) |
| N05-d | (NSC-6 하위 사례) `coverage.called_pitch_steps`가 재관찰한 `StageIds.CalledPitch` Step 수와 다름 | `pzl_mismatch` | §7 coverage (OWNER 결정) |
| N05-e | (NSC-6 하위 사례) `coverage.pitches ≠ Technical.counters.pitches`이지만 재관찰 값과는 일치 (Failed 경기가 있는 Unit) | 정상 (불일치만으로 거부하지 않음) | §7 coverage (OWNER 결정) |
| N06 | 버전 `1`(정수)이나 `"01"` 거부 | `schema_invalid` | P-01, PATCH-02(§2.1), N23과 같은 축 |
| N07 | Witness 순서 변경 | 기존 생성 순서와 다르면 재계산 불일치로 거부 | P-10/R-7(§4) |
| N08 | evidence_id 자기 선언 불일치 | `reference_invalid` | R-12(§2.2) |
| N09 | 승인 기록 `test_only` 인증 거부 | `test_only=true` 기록으로는 정상 인증 발급 불가(TEST_ONLY 영수증만) | R-10(§13.1), §14.2, PATCH-01 R01-3 |
| N10 | Artifact 역방향 참조 | 하위 문서가 상위 Raw SHA를 참조하면 거부 | R-13(§21) |
| N11 | Phase target에 통계 상태 기재 시 거부 | Phase target `statistical_status`는 null만 허용 | R-11(§20.1) |
| N12 | `UpperQuantile` 68개 입력 결과의 비트 Golden | **최신 조건 우선:** S01 §5.2가 입력을 j=1..36(`UpperQuantile(0.05/j)`, Holm m=36·m=32와 ZAlpha 전체)의 **36비트**로 정정하고 `witness_digest`와 Holm 동률 Fixture를 추가함. 하나라도 다르면 `environment_mismatch`(E2-5) | §18.2, PATCH-05 E2-5, F-19 |
| N13 | Observer 유무와 관계없이 모든 기존 Hash 불변 | game-result-v1·run-result-v1/v2·Event/RNG Hash·RNG 소비가 비트까지 동일. S02의 N13 확장: 기본 replay 모드 결과·오류 코드 동일, ConfigHash·Attestation·ef1/ef2·DRAFT 색인 바이트 변화 0 | §22, T04a |
| N13-a | (S03 하위 사례) 기본 모드 오류 코드가 리팩터링 전과 같음 | `VerifyEnvelope`·`BuildAttestation` 추출 후 결과 동일 | `OfficialAndReplayTests.Official_replay_checks_every_pinned_value_against_the_approvals`, `…Every_attestation_field_is_bound_into_the_attestation_hash`, `…Only_fully_pinned_test_approvals_make_a_preflight_ready_and_its_attestation_names_every_input`, `CodexFindingsTests.A_consistent_official_world_is_accepted_only_with_the_trusted_pin_and_the_real_file` |
| N14 | (옵션 B) `events_digest` 변조 | 옵션 B는 S01에서 채택됨(OD-2). 재관찰 digest와 다르면 `pzl_mismatch` | §10.5·§10.6 |

**N15~N36과 하위 사례 (S01~S04).**

| ID | 내용 / 기대 | 근거 |
|---|---|---|
| N15 | 승인 유효, 실행 허가 없음 → `authorization_missing` | §14 |
| N16 | 공식 검증 프로세스에 `DOTNET_TieredPGO` → `environment_mismatch` | §18.4 |
| N17 | 번들 사건 행 순서만 바꿈 → `pzl_mismatch` | §10.6 |
| N18 | TEST_ONLY 승인 + TEST_ONLY 허가 → `ExecutionPathReady=false`, preflight `execution_path` NOT_READY, 공식 실행 거부 | §14.3 |
| N18-a~d | §14.7 우선순위 표 4행 × 두 진입점 | `CodexFindingsTests.A6_A8_A10_a_forged_manifest_and_attestation_that_agree_with_each_other_still_meet_the_external_pin`, `FamilyOfficialTests.A_calibration_replay_refuses_every_tampered_or_untrusted_record_before_any_game`, `DominanceOfficialTests.Replay_refuses_tampered_cell_manifests` |
| N19 | 정상 승인, 허가 없음 → 실행 거부, 검증 `authorization_missing` | §14.5 |
| N19-a | `Official.not_ready`만 감싸고 다른 코드는 그대로 둠 (신규) | R01-C |
| N20 | 승인 SHA·Seed Pin 불일치 → 실행·검증 모두 거부 (`approval_mismatch`/`Seed.file_hash_mismatch` 보존) | §16, §14.7 |
| N21 | TEST_ONLY 검증 후 `Current` 비트 동일, 3개 CLI NOT_READY | R01-2 |
| N21-a | TEST_ONLY 성공 후 CLI NOT_READY | `CodexFindingsTests.A_fixture_approval_cannot_reach_the_real_replay_command_and_the_real_official_command_stays_not_ready`, `OfficialAndReplayTests.The_official_command_refuses_to_start_and_reports_not_ready_without_a_gate_verdict` |
| N22 | TestOnlyVerification + `ExecutionPathReady=true` → 거부 | R01-B |
| N22-a | 게이트 위치에서 거부. Attestation 오류가 함께 있으면 Attestation 오류가 먼저 (신규) | R01-A·B |
| N22-b | AMPV: EVALUATED 하나라도 비통과면 실패 / `git_integrity`·`output_directory`만 비통과면 통과 / `execution_path=Pass`면 실패 / 집계 행만 보고 PASS 처리 금지 | `FamilyOfficialTests.The_calibration_plan_comes_only_from_the_pins_and_every_gap_or_conflict_is_refused`, `GitIntegrityTests.Ignored_project_build_output_is_not_trusted_by_its_path_and_keeps_the_check_not_ready` |
| N23 | contracts 6필드·정수 1·`"01"`·키 누락·중복 키 → `schema_invalid` | §2.1 |
| N24 | 앵커 없음 → 거부 | §16 T1 |
| N25 | 앵커가 증거 디렉터리 안 / 검증 계정에 쓰기 권한 → 거부 | T2 |
| N26 | `TRUST_CONFIG` SHA ≠ 앵커 SHA → 거부 | T6 |
| N27 | Registry instance_id ≠ 앵커 → 거부 | R03-4 7단계 |
| A17-a / N27-a | Registry ID는 같지만 Pipe Owner·계정 다름 → 거부 (T09) | R03-4 4 |
| N27-b | 비인가 주체의 Pipe 이름 선점 → 서비스 시작 거부, 클라이언트 거부 | R03-3 S2, R03-4 4(a) |
| N27-c | 위조 OWNER SID 응답 → 미수용 / 요청에 `registrant_principal_id` → 서비스 거부 | R03-4, R03-5 |
| N27-d | 인증 후 재연결 시 다른 서버 → 재인증 전 사용 불가 | R03-4 9 |
| N27-e | 서비스 권한 없이 SQLite 직접 수정 → ACL 거부, 검증기는 파일을 직접 읽지 않음 | §15, R03-2 ③ |
| N27-f | Impersonation 실패 → 등록 차단 / RevertToSelf 실패 → FailFast (=AT-11) | R03-5-3 |
| N28 | Build Provenance `source_git_sha` 불일치 / PZL Profile `source_git_sha` 불일치 / environment `{id, sha}` 불일치 / Env Profile에 `source_git_sha` 추가 → 모두 거부. 정상 Env Profile은 통과 | §13.4 |
| N29 | `verify.ps1` 변수가 남은 채 인증 모드 실행 → `environment_mismatch`. `verify.ps1` 회귀는 영향 없음 | E1, E2-1 |
| N29-a | E2-1에서는 금지 변수만 판정, 프로필 비교 없음 | E2-1 |
| N30 | 시작과 발급 직전 사이 환경 변경 → `environment_mismatch` | E2-7 |
| N30-a | 프로필 불일치 → AMPV·Replay 시작 전 종료 | E2-4·E2-6 |
| N30-b | `C1≠C0` 또는 `C2≠C0` → 거부, 저장·등록 없음 | E2-4·E2-7 |
| N31 | 비트 Golden 1개 불일치 → `environment_mismatch` | E2-5 |
| N32 | B 경우에서 `final_rng_canonical` ≠ 초기 재구성 → `pzl_mismatch` | §10.4 |
| N33 | B 경우에서 `rng_hash=null` → `schema_invalid` | 같음 |
| N34 | A 경우에서 `rng_hash` non-null → `schema_invalid` | 같음 |
| N35 | C 경우에서 첫 `rng_before` ≠ 초기 스냅샷 → `pzl_mismatch` | 같음 |
| N36 | 두 경기의 빈 digest가 같음 → 상위 식별자가 다르면 정상 통과 | 같음 |
| N36-a | (NSC-6 하위 사례) A·B 경우 경기만 있는 Unit: `coverage.events=0`이고 `pzl-5`는 기존 규칙으로 NOT_EVALUATED가 정상. 같은 Unit에서 `pzl-5` PASS를 제출하면 재계산 불일치로 `pzl_mismatch` | §7 coverage 0건 규칙, 기존 pzl-5 판정 |
| N36-b | (NSC-6 하위 사례) Phase Root 관찰 Check의 coverage 필드가 0이라는 이유만으로 거부·FAIL 처리하는 구현 → 금지. status는 기존 PZL·Gate 규칙으로만 재계산 | §7 coverage ("신규 0 금지 조건 없음") |

## 3.4 MINOR-01 반례 (T09, P/Invoke seam 주입)

| 상황 | 기대 |
|---|---|
| `AccessCheck` 반환 FALSE (`GrantedAccess`에 쓰레기 값) | 거부, 그 값을 읽지 않음 |
| 반환 TRUE, `AccessStatus` FALSE | 거부 |
| 반환 TRUE, Status TRUE, `GrantedAccess`에 0x4 포함 | 거부 |
| 반환 TRUE, Status TRUE, 금지 권한 없음, 다른 단계 정상 | 진행 |
| 같은 상황에서 Owner 불일치 | 거부 (5단계만으로 통과하지 않음) |
| `ImpersonateSelf` 실패 / `OpenThreadToken` 실패 | 거부, `RevertToSelf` 호출 확인 |
| `RevertToSelf` 실패 | FailFast |
| 각 실패 경로 | 토큰·보안 설명자·Pipe 핸들 해제 확인 |

## 3.5 T09 수용 시험 (실제 Windows 실측, **전부 미실시**)

| AT | 상황 | 기대 결과 | 관련 반례 |
|---|---|---|---|
| AT-1 | 정상 Registry + 정상 클라이언트 | 2~7단계 통과, 조회 성공 | N27 |
| AT-2 | 정상 클라이언트 계정이 같은 이름으로 `CreateNamedPipeW` | `ERROR_ACCESS_DENIED` | N27-b |
| AT-3 | `GENERIC_WRITE` 또는 기본 `Connect`로 연결 | 접근 거부 | N27-b |
| **AT-4 (P-1)** | 표준 사용자가 Owner=`registry_account_sid`로 Pipe 생성 | `ERROR_INVALID_OWNER`. **실패해야만 PATCH-03 운영 사용** | A17-a |
| AT-5 | Owner가 다른 Pipe | 4(a) 거부 | A17-a |
| AT-6 | DACL에 비인가 SID 또는 생성 권한(GENERIC 변형 포함) | 4(d) 또는 5단계 거부 | N27-a |
| AT-7 | 이름 선점 후 서비스 시작 | 서비스 시작 거부, 클라이언트 인증 실패 | N27-b |
| AT-8 | 서비스 종료 후 가짜 Pipe | 거부 | N27-d |
| AT-9 | 끊김 후 다른 서버 | 재인증 전 응답 0바이트 해석 | N27-d |
| AT-10 | 요청에 OWNER SID 포함 / 위조 응답 | 서비스 거부 / 클라이언트 미수용 | N27-c |
| AT-11 | Impersonation 실패 / Revert 실패 주입 | 등록 차단 / FailFast | N27-f |
| AT-12 | 비인가 계정의 SQLite 직접 쓰기 | 쓰기 거부 | N27-e |
| AT-13a | 인스턴스 1~3개 사용 중 | 대기 인스턴스 1개 확보 (`n ≤ 4`) | — |
| AT-13b | 4개 모두 사용 중 | 포화 기록, 생성 시도 0회, 서비스 계속 실행 | — |
| AT-13c | 1개 해제 | S5 재사용으로 대기 복구, `n` 불변 | — |
| AT-13d | 클라이언트가 `ERROR_PIPE_BUSY` 받음 | 유한 대기 후 연결, 전 단계 수행 | — |
| AT-13e | 30초 내 가용 인스턴스 없음 | `registry_unavailable`, 응답 미수용 | — |
| AT-13f | 서비스 재시작 / 이름 선점 | AT-7·AT-8 결과 유지 | — |
| AT-13g | `ERROR_PIPE_CONNECTED`·`ERROR_NO_DATA` | S7대로 처리, 인증 생략 없음 | — |
| AT-14a / b | Reader의 Get / GetByScope | 성공 | — |
| AT-14c / d | Reader의 Register(APPROVAL_RECORD / VERIFICATION_RECEIPT) | `registration_unauthorized`, DB 변경 0 | — |
| AT-14e | Reader가 `role:"OWNER"`·`registrant_principal_id` 제출 | `schema_invalid`, 권한 변화 없음 | — |
| AT-14f | Reader의 `CreateNamedPipeW` | `ERROR_ACCESS_DENIED` | — |
| AT-14g | Reader의 SQLite 직접 수정 | ACL 거부 | — |
| AT-14h | 미승인 SID로 Pipe 연결 | `CreateFileW` 접근 거부 | — |
| AT-14i | Reader 계정으로 EvidenceVerifier 실행 | 1단계 시작 거부. 우회해도 서버가 RECEIPT 등록 거부 | — |
| AT-14j | 앵커와 서비스 설정의 Reader 집합 불일치 | (S07에서 4(e) 삭제) **4(d) 다중집합 불일치로 `registry_config_mismatch`** | — |
| AT-14k | OWNER `0x00120001` / VERIFIER 쓰기 누락 / 중복 ACE / 계정 ACE 누락·제한 / Reader 마스크 초과 / 미승인 SID 추가 | 각각 `registry_config_mismatch` | — |
| AT-14l | 서비스 시작 시 자기 보안 설명자 검증 실패 | 시작 거부, 핸들 0개 | — |
| AT-14m | 스냅샷에서 Reader·OWNER SID 겹침 | 시작 거부 | — |
| AT-14n | 앵커나 서비스 설정에 `evidence_reader_sids` 없음 | `schema_invalid`, 기본값 `[]` 생성 없음 | — |
| AT-14o | 승인된 작성자가 있는데 배열이 `[]` | 계약 위반. OWNER 배치 점검표에서 차단, 실행 시 해당 작성자 `CreateFileW` 접근 거부 | — |
| AT-14p | 승인된 Reader의 Get / GetByScope | 허용 (14a·b 재확인) | — |
| AT-14q | Reader의 entry_kind 6종 Register | 모두 `registration_unauthorized`, DB 변경 0 | — |
| AT-14r | 실행 중 설정 파일 변경 | 재시작 전까지 DACL·역할 매핑 불변 | — |
| 추가 | Identification 수준에서 등록 SID 기록, `registry_service_sid` non-null일 때 TokenGroups 자체 점검 | 동작 확인 | — |

**AT-14j 처리:** S06에서 AT-14j의 기대 결과는 "4(e)에서 거부"였습니다. S07이 4(e)를 4(d)에 흡수했으므로, 기대 결과(`registry_config_mismatch`)는 같고 검출 위치만 4(d)로 바뀝니다.

## 3.6 22 Findings 대체 계약 (BLOCKER 1 · MAJOR 11 · MINOR 8 · INFO 2)

| Finding | 등급 | 최종 대체 계약 | 포함 위치 |
|---|---|---|---|
| F-09 | BLOCKER | §2 필드 사전 전체 (반례 A01–A04, A17, N09) | §1~§7 ✅ |
| F-01 | MAJOR | P-01, R-1 | §2.1 ✅ |
| F-02 | MAJOR | R-2 (5분류) | §1.1 규칙 9, §9.5 ✅ |
| F-03 | MAJOR | P-05, §1.3 D행 | §10.7, §3.6 ✅ |
| F-04 | MAJOR | §1.3 Regulation 열 | §3.6 ✅ |
| F-05 | MAJOR | §1.3 B행 | §3.6 ✅ |
| F-06 | MAJOR | P-07 | §3.4 ✅ |
| F-07 | MAJOR | P-11 | §5.1 ✅ |
| F-08 | MAJOR | GAME_EVENTS (OD-2) | §10 ✅ |
| F-10 | MAJOR | P-15, R-11 | §20.1 ✅ |
| F-19 | MAJOR | 계산 환경 (N12) | §18 ✅ |
| F-26 | MAJOR | T05: public 위임 비교 메서드(Codex 리뷰) | 구현 TASK (INFO-1) |
| F-11 | MINOR | P-08 (R-5) | §3.7 ✅ |
| F-12, F-13 | MINOR | P-10 (R-7) | §4 ✅ |
| F-14 | MINOR | P-02 + R-14 잔여 동치 제약 | §2.4 ✅ |
| F-15 | MINOR | R-12 | §2.2 ✅ |
| F-16 | MINOR | R-9, §4.3 | §11.2 ✅ |
| F-17 | MINOR | R-13, P-13 | §21 ✅ |
| F-23 | MINOR | P-03, §1.3 | §1.2, §3.6 ✅ |
| F-20 | INFO | 수집 출처 (커서 전후 Rng, `Draft.NextRng`, `AdvancementDetail`) | §5.4 ✅ |
| F-22 | INFO | GV-C01·C02 유지 | 3.1 ✅ |

R-6은 RESERVED/UNUSED(P-09), R-8은 P-11로, R-14는 P-02로 대체되었습니다(R-14 잔여 7개 축은 유지).

**등급 집계 정정 이력:** S00.5 §1 요약은 "MAJOR 9건"이라고 적었지만, 표 기준 집계는 MAJOR 11건입니다. S01이 이를 오기로 정정했습니다(BLOCKER 1 · MAJOR 11 · MINOR 8 · INFO 2 = 22).

## 3.7 필수 항목 누락 검사표

| 항목 | 위치 | 상태 |
|---|---|---|
| 해석 규칙 11개, 공통 스칼라 21종, 상위 오류 17개 + 원인 코드 | §1 | ✅ |
| Root·Schema·Execution·Approvals·AdditionalEvidence·ArtifactRef | §2 | ✅ |
| Family 7속성, UnitInputs 14, GameSpec 3, Attestation 21, Technical, EndKindCount, Stop 4, Counters 28, GameResult 20 + 상태표, Failure, ReceiptRef, CrossCheck 15항목, FamilyUnitEvaluation, Shares 9, OfficialMetric 16, Gate | §3 | ✅ |
| Dominance: Cell, DominanceEvaluation, CellEvaluation, CellSample, SideResult, StrategyResult, Witness 11필드 | §4 | ✅ |
| Unit PZL: Root, 7 Stream, PzlGame 12, PzlSummary 14, EventObservation, EventReceipt, PitchObservation 13, Location, Prepared 12, BranchStep, FramingEntry, StreamUse | §5 | ✅ |
| #6 RoundTripSample 20필드, 6단계 검증 | §6 | ✅ |
| Check·Violation·TestEvidence(P-14)·Deferral | §7 | ✅ |
| JCS·UInt64·정수·binary64·불변 | §8 | ✅ |
| A+ M(D)·Payload 4종·바이트식·결속 구분 | §9 | ✅ |
| GAME_EVENTS 형식·레코드·결속·A/B/C RNG·digest·검증·Null 복원 | §10 | ✅ |
| PZL #1~#12b·증거 유형·재수집 | §11 | ✅ |
| Test Profile·필수 Case 8 Check·독립 확인 | §12 | ✅ |
| Approval Record·카탈로그·거부·검증 출처·PATCH-04 | §13 | ✅ |
| 실행 허가·상태 3축·R01-1/2·R01-A~D·AMPV 표·우선순위 표 | §14 | ✅ |
| Registry SQL·연산·권한 | §15 | ✅ |
| T1~T7·L2~L8·B-3 흐름 | §16 | ✅ |
| R03-1~R03-6 (Reader ACE, 1단계 분리, 4(d) 다중집합, MINOR-01·02, R-C 스냅샷, P-1) | §17 | ✅ |
| Env Profile·Witness Fixture·Build Provenance·E1~E5·E2-1~7 | §18 | ✅ |
| 인증 상태표·영수증·발행·등록 | §19·§20 | ✅ |
| Archive 20 역할 (P-12·P-13) | §21 | ✅ |
| Golden 3종 + 미생성 목록 | 3.1 | ✅ |
| A01~A28 / N01~N36 + 하위 사례 / MINOR-01 / AT-1~AT-14r | 3.2~3.5 | ✅ (N01~N14는 S00.5 원문 복원) |
| R-1~R-14 항목별 대조 (S00.5) | 2부 §2.4 | ✅ |
| R-5 Calibration policy 상시 non-null·Integration `evaluation_computed`·OfficialMetric 3조건 | §3.7·§3.8 | ✅ |
| R-11 target별 상태, R-12 도출식 4개 | §20.1, §2.2 | ✅ |
| R-7 Dominance 세부, R-13 DAG, R-14 잔여 7개 축 | §4, §21, §2.4 | ✅ |
| `Check.coverage` 네 필드 의미·범위·재검산·0건 규칙 (NSC-6 OWNER 결정) | §7 | ✅ (반례 N05-a~e, N36-a·b, A10-a, A25-a·b) |
| NSC-4·NSC-5 미해결 구현 Gate | 2부 §2.3 | ✅ (T06a·T09 전 해결) |
| 22 Findings | 3.6 | ✅ |
| 삭제된 구 규범이 본문에 남지 않음 (`m & ~0x00120083`, 4(e), 6키 contracts, PID API, "항상 대기 1개", 범위 없는 env 규칙, `TRUST_CONFIG` 앵커) | 2부 2.1 | ✅ 본문에는 없고 이력에만 기록 |

---

# 4부. 실제 소유자 승인 단계 체크리스트

> 아래 항목은 **소유자가 직접 수행·기록**합니다. Claude는 승인을 기록하거나 주장하지 않습니다.

**A. 기준본 고정**
- [ ] 5부의 Codex 원문 대조 검수 결과를 확인합니다. NSC-1~3은 S00.5로, NSC-6은 OWNER 결정(TASK.md 2026-10-24)으로 RESOLVED 처리했습니다. 이 처리는 아직 Codex의 변경 범위 독립 검수 전입니다.
- [ ] NSC-6 OWNER 결정의 근거로, 결정 당시 보고된 TASK.md SHA-256 `ecbc0859c809c68151c93f92058c31a7ab0b84ce8d8cc9a4a25512b7dd06f825`(4부 B 참고값 ②)를 승인 기록에 남깁니다. 이 결정은 B1-T00 전체 소유자 승인이나 구현 착수 허가가 아닙니다.
- [ ] NSC-4·NSC-5는 미해결 구현 Gate입니다. NSC-4는 T06a 구현과 운영 배치 전에, NSC-5는 T09 구현 전에 해결합니다. 미해결 상태에서는 해당 수용 시험을 통과 처리하지 않습니다.
- [ ] 이 파일의 바이트를 기준본으로 고정합니다. BOM·개행(LF/CRLF) 규칙은 소유자가 정하고 기록합니다. 이 초안은 UTF-8(BOM 없음)·LF로 작성되었으며, 편집기나 Git 설정이 개행을 바꾸면 SHA도 바뀝니다.
- [ ] 저장 위치를 정합니다. 저장소에 둘지, 그리고 `-text` 적용 여부를 기록합니다.

**B. 승인 시점에 기록할 SHA (승인 시점에 다시 계산)**

| 항목 | 승인 시 실측값 | 참고값 (2026-10-24 Claude 읽기 전용 측정) |
|---|---|---|
| 최종 규범 문서 SHA-256 | ______ | (승인 기준본 파일로 계산) |
| Git HEAD | ______ | `257403ddae557a54c4013c17705370caeac7a2e0` |
| TASK.md SHA-256 | ______ (**최종 승인 시점에 실제 TASK.md를 다시 계산**. 아래 두 참고값으로 대체 금지) | ① `2f4fb6f86ff4679d4a5a9c0ab6d5670a9b0ebfcac119086d230a1800b79939f7`: 기존 S08 검수 시점 참고값<br>② `ecbc0859c809c68151c93f92058c31a7ab0b84ce8d8cc9a4a25512b7dd06f825`: NSC-6 OWNER 결정 당시 보고된 값 |
| 작업 트리 상태 | ______ | 미추적 3개(`TASK.md`, `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf`), 스테이징 0 (이 초안 파일 생성 전 기준) |
| S00~S07 원문 SHA | (자료집 표기값) | 8건 모두 마커 본문 재계산과 일치 확인 |
| S00.5 원문 SHA | ______ | `578bf4af24f376e1d799d3b69ddf9fd71f8e956adde795324db91e7c3ce0a3d9` (`붙여넣은 마크다운(20261020-150009).md`, 65,759바이트) |

**C. 승인 기록 내용 (권장 최소 항목)**
- [ ] 승인 대상: 위 문서 SHA, HEAD, TASK.md SHA
- [ ] 판정 근거: Codex `READY_FOR_OWNER_APPROVAL`(S08). S08은 요약이므로 원문 보관 위치를 기록합니다.
- [ ] 승인 범위를 명시합니다: "B1-T00 기술 계약 채택"만 해당하며, 다음은 **포함하지 않습니다.**
  - T01 이후 구현 착수 허가(별도 지시 필요)
  - Official Execution (`NOT_READY` 유지)
  - Final Execution Freeze (`PENDING` 유지)
  - P-1/AT-4와 AT-1~AT-14r 실측 통과 (미실시)
- [ ] 승인자, 일시, 승인 방식(서명·Git 커밋·외부 기록 등)
- [ ] 이후 계약을 바꾸려면 새 문서 SHA와 새 승인 기록이 필요하다는 규칙

**D. 승인 후에도 남는 운영 Gate**
- [ ] AT-4(P-1)를 실측으로 통과하기 전에는 Registry 기반 공식 인증 운영 금지
- [ ] §23 운영 값(Cohort·Seed·Pin·B-3·B-4·B-5·B-6c/d·SID·Pipe·DB 경로·환경 실측·비트 Golden)을 OWNER 등록 전까지 Fail-Closed로 유지
- [ ] 완전 문서 Golden은 B-1 구현 완료 조건으로 남음

---

# 5부. Codex 원문 대조 검수 요청 (범위 한정)

**목적:** 이 단일 문서가 S00·S00.5·S01~S07 원문을 정확히 통합했는지만 대조합니다. 새 설계 제안이나 재설계는 범위 밖입니다.

**입력:**
- 이 문서 (검수 시점의 SHA-256을 기록)
- 자료집 `StarPlayer_B1-T00_Claude_계약원문_전체자료집.md` (S00~S07, 원문 SHA는 자료집 §B)
- S00.5 `붙여넣은 마크다운(20261020-150009).md` (SHA-256 `578bf4af24f376e1d799d3b69ddf9fd71f8e956adde795324db91e7c3ce0a3d9`)

**대조 항목:**
1. 2부 §2.1·§2.4의 대체 관계가 원문 순서(S00 → S00.5 → S01 → … → S07)와 맞는지
2. 후행 PATCH로 대체된 문장이 본문에 남거나 복원되지 않았는지. 특히 R-3·R-8·R-10 필드 목록·R-14 바이트 일치, `m & ~0x00120083`, 4(e), 6키 contracts, `GetNamedPipeServerProcessId`, "항상 대기 1개", 범위 없는 환경 규칙, `TRUST_CONFIG` 앵커
3. R-5·R-7·R-11·R-12·R-13·R-14 잔여 제약이 원문과 같은 뜻으로 옮겨졌는지
4. N01~N14 복원과 N12 최신 조건 우선 처리
5. NSC-1~3 RESOLVED 판정과 NSC-4·5의 미해결 구현 Gate 기록(T06a·T09 전 해결)
6. 원문에 없는 기술 결정, Golden·Seed Pin·SID 값이 새로 들어가지 않았는지
7. **NSC-6 정정 변경 범위 (TASK.md 2026-10-24):**
   - 2부 §2.2의 `frozen_games_per_cell`·`plan.run_result_*` 설명이 §4 규범·Schema와 일치하는지 (MISMATCH-01)
   - §7 "coverage 의미와 계산 범위"가 TASK.md의 OWNER 결정 문장과 의미가 같은지. 특히 Phase Root PASS에 "coverage 0 금지" 조건이 없는지
   - R-8 coverage 두 문장이 "OWNER 채택 잔여 규범"으로, `pitches`·`called_pitch_steps`와 범위 규칙이 "신규 OWNER 결정"으로 구분 표시되었는지 (§7, 2부 §2.3·§2.4)
   - 3부 신규 하위 사례(N05-a~e, N36-a·b, A10-a, A25-a·b)가 기존 오류 체계와 PASS 규칙만 쓰는지
   - 위 범위 밖 본문이 바뀌지 않았는지 (Hash 계약·RNG·Replay·Registry·통계 정책 불변)

**금지:** 소유자 승인 기록, 코드·ADR·TASK.md·Git 변경, 빌드·테스트·Official Execution, Final Freeze

**판정 형식:** 항목별 `MATCH` / `MISMATCH`(원문 위치와 문장) / `NEEDS_SOURCE_CHECK`. 전체 결론은 `SOURCE_RECONCILED` 또는 `NOT_RECONCILED`로 냅니다. 이 판정은 기술 계약 판정(`READY_FOR_OWNER_APPROVAL`)이나 소유자 승인을 대신하지 않습니다.
