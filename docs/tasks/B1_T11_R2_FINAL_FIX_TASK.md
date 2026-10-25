# Star Player B1 — T11 R2 잔여 결함 최종 수정 TASK (R2-A·R2-B)

- 작성: Claude Code, 2026-10-25
- 근거: OWNER 채팅 지시 "Star Player — B1 T11 R2 잔여 결함 최종 수정"(2026-10-25 수신). 지시 원문의 요구는 아래 1절에 모두 옮겼습니다.
- 범위: **R2-A INPUT_BUNDLE 실제 소비 입력 완전성 · R2-B PREFLIGHT 필요한 기록 보존·검증만** 다룹니다.
  - R1·R3·R4는 Codex가 RESOLVED로 판정했으므로 재설계하지 않습니다.
  - 새 설계나 계약 변경은 없습니다. 새 공개 형식이나 정책이 불가피한 사항만 `OWNER_DECISION_REQUIRED`로 분리합니다.
- 루트 `TASK.md`는 D3 보호 파일이며 수정하지 않습니다. 진행 상황은 [B1_IMPLEMENTATION_LOG.md](../reports/B1_IMPLEMENTATION_LOG.md)에 남깁니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 0. 기준 상태 (작성 시점)

| 항목 | 값 |
|---|---|
| Git HEAD | `257403ddae557a54c4013c17705370caeac7a2e0`, 스테이징 0, Commit·Push 없음 |
| 보호 SHA | 계약 `1338a2f7…7d64` · `TASK.md` `ecbc0859…f825` · 기준 목록 `a9865a2f…9430`(5개) · 결정 1/2/3 `af7142e5…`/`fb469705…`/`bcb7bee7…` |
| 직전 결과(참고만) | 2026-10-25 15:49:34 verify `GATE: PASS` 3,032. [직전 보고서](../reports/B1_T11_FIX_R2_REPORT.md)에 R1~R4 수정 내용이 있습니다. |

**Codex 원문:** R2-A·R2-B 재검수 원문은 저장소에 없습니다. OWNER 지시에 담긴 지적은 다음 두 가지입니다.

- R2-A: INPUT_BUNDLE을 Environment·PZL Test Profile 2개로 고정한 것은 실제 소비 입력 전체가 아닙니다.
- R2-B: PREFLIGHT를 0개로 고정하고 일괄 거부하는 것은 §21 "Family·Unit별 필요 기록"을 충족하지 못합니다.

원문을 받으면 `docs/reports/`에 보존하고 대조합니다.

## 1. OWNER 지시 요지 (이번 승인 범위)

1. 이 TASK를 작성하고, 이 파일을 기준으로 구현과 시험을 이어 수행합니다.
2. R2-A:
   - 두 Profile로 고정한 검사를 없앱니다.
   - Production·독립 검증 경로의 소비 입력을 전수 조사합니다(`HarnessData.LoadTuningV4`·`LoadRules`, `SimHarness.csproj` EmbeddedResource, 실행·Replay·평가의 추가 로딩, 승인 계획·Build Provenance 연결).
   - 각 입력에 대해 다음 여섯 가지를 결속합니다.
     - 논리 식별자 ↔ 파일·리소스
     - Raw SHA
     - 소비 바이트 = 보존 원본
     - Snapshot·ArtifactRef·RAW_INVENTORY·SourceFiles
     - 승인 Pin·Build Provenance
     - 중복·누락·치환·추가 거부
   - EmbeddedResource는 빌드 원본과 런타임 소비 바이트의 관계를 검증합니다. 의미 Hash가 입력을 포함한다는 이유로 Raw 보존을 생략하지 않습니다.
3. R2-B:
   - PREFLIGHT 0개 고정과 일괄 거부를 없앱니다.
   - 필요한 Family·Unit Preflight 기록을 빠짐없이 보존합니다.
   - 첫 경기 전 실제 결과와 결속합니다.
   - Attestation이나 DIAGNOSTICS만으로는 대체하지 않습니다.
   - 출처·범위·바이트·SHA를 확인하고, Snapshot·SourceFiles에 결속하며, 독립 검증에서 실제 Preflight 결과와 비교합니다.
   - 누락·혼합·변조·중복은 Fail-Closed입니다.
4. 4절의 필수 반례와 양성 시험을 둡니다. 가능한 것은 수정 전에 Red를 재현합니다.
5. 5절의 기존 해결 사항을 보존합니다. 검증을 약화하거나 기대값을 임의로 완화하지 않습니다.
6. 6절의 전체 회귀를 실행하고 결과를 새로 기록합니다.
7. 7절의 작업 제한을 지킵니다.
8. 완료 보고서는 `docs/reports/B1_T11_R2_FINAL_FIX_REPORT.md`입니다. 작성 후 멈추고 Codex READ-ONLY R2 재검수를 기다립니다.

## 2. 승인 계약 §21 관련 조항

| role | 필수 개수·단위 | 시점 | Raw SHA |
|---|---|---|---|
| `INPUT_BUNDLE` | 소비 입력 파일별 | build/run 전·검증 | 필수 |
| `PREFLIGHT` | Family·Unit별 필요 기록 | 첫 경기 전 | 필수 |
| `ATTESTATION` | Unit별 1 (Manifest 내부 허용) | Preflight·저장 | 소속 파일 보존 |
| `DIAGNOSTICS` | 채택한 summary·로그별 | 실행 시 | 채택 파일 모두 필수 |

- 파일명과 출력 루트는 B-4가 정합니다.
- 보존 규칙: 덮어쓰지 않고, 생성·Archive·스냅샷 시점의 바이트를 대조하며, Hash한 뒤 다시 열어 파싱하지 않습니다.
- 자기 참조는 제외합니다.
- 참조는 아래로만 향합니다(R-13).

## 3. 조사 결과 — 실제 소비 입력과 Preflight 기록의 출처

### 3.1 R2-A 소비 입력 (Production 실행·Replay·평가·검증기)

| 입력 | 실제 로딩 경로 | 성격 | 이번 결정 |
|---|---|---|---|
| `content/tuning/virtual_tuning_v4.json` | EmbeddedResource `virtual_tuning_v4.json`(`SimHarness.csproj`) → `HarnessData.ReadResource`/`LoadTuningV4`. 사용처: `OfficialPreflight.EnvironmentChecks`, `FamilyOfficial` Config(`:260`), `Dominance`(`:111`) | 데이터 파일 | **INPUT_BUNDLE 필수.** 바이트 = 런타임 리소스 스트림 원본 |
| `content/rules/game_rules_profiles_v2.json` | EmbeddedResource `game_rules_profiles_v2.json` → `HarnessData.LoadRules`. 사용처: Preflight `rules`, `FamilyOfficial` Config(`:259`), `Dominance`(`:112`) | 데이터 파일 | **INPUT_BUNDLE 필수** |
| Environment Profile | 기록부 → Trust Bootstrap. E2-4·E2-5에서 소비 | 승인 Pin 원문 | INPUT_BUNDLE 필수(유지) |
| PZL Test Profile | 기록부 → Trust Bootstrap. §12.3에서 소비 | 승인 Pin 원문 | INPUT_BUNDLE 필수(유지) |
| Seed Manifest | `SeedManifestReader`(승인 Pin) | 데이터 파일 | 전용 역할 SEED_MANIFEST(기존 필수) |
| 승인 기록·Build Provenance | 기록부 | 원문 | 전용 역할(기존 필수) |
| 실행 색인·Unit Manifest | Archive | 실행 산출물 | DIAGNOSTICS·UNIT_MANIFEST(기존 필수) |
| 중립 구장·코호트·정책·실행 프로파일·평가 정책 | `HarnessData.NeutralPark`, `NeutralCohort`, `PolicyRegistry`, `OfficialProfile`, `EvaluationPolicy` | **코드 상수**(입력 파일 아님) | 바이너리(Build Provenance SIMHARNESS_BINARY) 범위. INPUT_BUNDLE 아님 |
| 실행 허가 기록 | 기록부(L7) | 권한 기록 | 영수증 `execution_authorization_id`로 결속. 입력 데이터가 아니므로 INPUT_BUNDLE 아님 |

- **실측:** `content/tuning/virtual_tuning_v4.json`은 작업 트리에서 CRLF(system `core.autocrlf=true`)이고 Git blob은 LF입니다(26,505 B ≠ 26,346 B). 빌드는 작업 트리 바이트를 내장합니다. 따라서 "소비 바이트"는 Git blob이 아니라 **런타임 리소스 스트림의 바이트**입니다. 규칙 파일은 작업 트리와 blob이 같습니다.
- **도출 규칙:**
  - 필수 INPUT_BUNDLE의 Raw SHA 다중 집합 = {승인 기록의 environment_profile Pin, pzl_test_profile Pin, 런타임 `virtual_tuning_v4.json` 리소스 원본 SHA, 런타임 `game_rules_profiles_v2.json` 리소스 원본 SHA}입니다.
  - 리소스 목록은 `HarnessData.Inputs`(논리 이름 ↔ 저장소 경로) 하나에서 나옵니다. 시험은 이 목록이 `SimHarness.csproj`의 EmbeddedResource와, 어셈블리의 실제 Manifest Resource 목록과 정확히 같음을 고정합니다.
  - 검증기는 리소스 바이트가 검증 저장소 루트의 원본 파일 바이트와 같은지 확인합니다(다르면 `approval_mismatch`/`Input.embedded_differs`).
  - 승인값과의 의미 결속은 기존 Preflight `model_tuning`·`rules`가 맡습니다(ContentHash = OfficialProfile 승인값).
- **Build Provenance:** TEST_ONLY Build Provenance에는 SIMHARNESS_BINARY가 없습니다. 실행 바이너리 자기 SHA 대조는 직전 라운드부터 남은 위험이며, 운영 Build Provenance와 B-4 대상입니다.

### 3.2 R2-B Preflight 기록

| 기록 | 현재 위치 | 문제 |
|---|---|---|
| Family/Dominance Preflight(`FamilyPreflightReport`/`DominancePreflightReport`, 검사 cli·git_integrity·git_sha·model_tuning·output_directory·seed_manifests·(games_per_cell)·unit/cell_preflight·execution_policy·execution_path) | 메모리. 실행 색인 `preflight.checks`(DIAGNOSTICS)에 요약 | 색인은 DIAGNOSTICS 집계이며, Unit 결과가 없습니다 |
| Unit Preflight(`PreflightReport`, 검사 15개) | 메모리. Attestation만 UNIT_MANIFEST에 있음 | Attestation은 Preflight 기록이 아닙니다 |

- **기존 형식(새 형식 없음):**
  - Family: `FamilyOfficial.PreflightDocument`·`DominanceOfficial.PreflightDocument`(`official-cal/int/dom` CLI가 출력하는 Preflight 문서)
  - Unit: `OfficialOutput.Result(PreflightReport)`(`official` CLI가 출력하는 단일 Manifest Preflight 결과 `simharness-official-result-v1`)
- **보존 시점:** 공식 실행(`FamilyOfficial.Execute`·`DominanceOfficial.Execute`)이 출력 디렉터리를 고정한 직후, **첫 Unit·첫 경기 전**에 기록합니다.
  - `preflight.json`(Family 문서)
  - `preflight/<unit>.json`(Unit 결과)
  - 둘 다 실행이 진입한 바로 그 Report의 직렬화이며, 새로 만들고 덮어쓰지 않습니다.
  - 생산자 `EvidenceWriter.WriteRun`이 이 파일을 바이트 그대로 PREFLIGHT(Family 1, unit_id null · Unit별 1, unit_id = Unit)로 Archive에 보존합니다.
- **검증:**
  - 개수: Family 1 + Unit별 정확히 1이며, 경로·역할·unit_id가 일치하고 그 밖의 PREFLIGHT는 없어야 합니다.
  - 내용: 검증기가 같은 승인값으로 Family/Dominance Preflight를 직접 다시 실행하고(경기 없음, 쓰기 없음, 새 출력 이름) 비교합니다.
    - 기록의 모든 검사 = PASS, 검사 ID 순서 = 재실행 순서
    - AMPV 고정 분류표의 "평가 대상" 검사(Unit: model_tuning·seed_manifest·family·rules·policy·cohort·cohort_hash·game_ids·execution_policy / Family: model_tuning·seed_manifests·games_per_cell·execution_policy)의 code·message = 재실행
    - Unit 기록의 attestation_hash·config_hash·seed_manifest_hash·tuning_hash·rule_snapshot_hash = 그 Unit의 보존 UNIT_MANIFEST Attestation
    - Family 기록의 run_id·family·Unit 목록(ID·Seed 경로·ready) = 승인 계획
  - 실행 전용 검사(cli·git_integrity·git_sha·output_directory·execution_path·attestation)는 환경·경로에 따라 달라 값 비교에서 빼고, PASS만 요구합니다.
- **`OWNER_DECISION_REQUIRED` 후보(이번 구현은 내부 배치):** 파일명 `preflight.json`·`preflight/<unit>.json`은 B-4가 정할 사항입니다. OWNER 결정 3 §6의 `run/<run_id>/` 내부 배치와 같은 성격으로 두고, B-4 공식 경로를 주장하지 않습니다.

## 4. 수정 대상과 Red/Green 반례

| 대상 | 파일·함수 |
|---|---|
| 입력 목록 | `tools/StarPlayer.SimHarness/HarnessData.cs` `Inputs`·`ResourceBytes` |
| Preflight 기록 | `FamilyOfficial.Execute`, `DominanceOfficial.Execute`, `OfficialOutput`(파일명 상수) |
| 생산자 | `Evidence/EvidenceWriter.WriteRun` |
| 검증기 | `EvidenceVerifier/Verifier.cs` `RequiredCounts`·`VerifyPreflight`(신규)·`InputBundles`(신규), `Certification.RequireOriginals`(INPUT_BUNDLE 기대 집합, PREFLIGHT 개수) |
| Fixture | `TestOnlyWorld`(원본 content 파일을 세계 루트에 복사), `Production.Trust`(리소스 원본 2개 보존) |

| 구분 | 반례·양성 |
|---|---|
| R2-A | ① 두 Profile만 있고 튜닝·규칙 누락 → 거부(**수정 전 Red**: 현재 통과) ② 실제 소비 입력 전부 → 통과 ③ 하나 누락 ④ Raw SHA 불일치 ⑤ 같은 식별자의 다른 바이트 ⑥ EmbeddedResource ↔ 원본 파일 불일치 ⑦ 승인 집합 밖 입력 추가 ⑧ Inventory·SourceFiles 양쪽에서 같은 원본 제거 → 모두 거부 |
| R2-B | ① PREFLIGHT 0개 → 거부(**수정 전 Red**: 현재 0개가 통과) ② 정상 Family·Unit 기록 → 통과 ③ 특정 Unit 누락 ④ 다른 Unit 기록으로 대체 ⑤ 내용·SHA 변조 ⑥ Attestation만 ⑦ DIAGNOSTICS만 → 모두 거부 ⑧ 유효 기록의 원본·Inventory·Receipt 결속 → 통과 |
| Phase | ① 정상 Calibration·Integration·Dominance 영수증 ② 필수 ORIGINAL 전체를 포함한 Phase 영수증 ③ 공유 ORIGINAL은 동일성 조건에서만 병합 ④ Family 원본·INPUT_BUNDLE·PREFLIGHT가 빠지면 Phase 발급에서도 거부 |

## 5. 회귀 조건 (변경 없이 유지)

- R1 불변 스냅샷, R3 합집합·ID 충돌 검사, R4 PinnedDirectory, T11-01~08
- OWNER 결정 7건, GAME_RESULTS·PZL6 형식
- TEST_ONLY Seed 991000, Regression Proof
- GameResultHash·RunResultHash·ConfigHash·Attestation Hash, RNG 소비, 기본 Replay 순서, Golden 기대값
- Preflight 파일 추가는 Manifest·색인 내용을 바꾸지 않습니다. Dry Pilot(`dry` 경로)에는 영향이 없어야 합니다.

## 6. 최종 수용 기준

1. 4절의 반례·양성 시험이 PASS이고, Red 가능 항목은 수정 전 실행 결과를 보존합니다.
2. 다음 검증을 수정 후 새로 실행합니다.
   - Release 빌드(경고 0)
   - 모든 시험 프로젝트
   - 정상 Family → Phase 영수증
   - Golden 4개
   - `tools/b1-drypilot-repro.ps1` `ALL_9_IDENTICAL=True`
   - `tools/verify.ps1` `GATE: PASS`
   - D3 5개 OK
3. 보고서·기록 문서를 고친 뒤 링크와 D3를 다시 확인합니다.
4. B1 PASS와 Phase 3 완료를 선언하지 않습니다.

## 7. 작업 제한

- 다음 원본은 변경하지 않습니다: 승인 계약, OWNER 기록, 루트 `TASK.md`, PDF, D3 기준.
- Commit·Push를 하지 않습니다.
- 운영 Gate를 PASS로 처리하지 않습니다.
- Official Execution과 Final Execution Freeze를 활성화하지 않습니다.

## 8. 중단 후 재개 방법

1. 이 문서와 구현 로그의 "T11 R2 최종 수정" 절을 읽고 마지막 완료 단계를 확인합니다.
2. `git status`와 `sha256sum -c docs/contracts/b1/B1-T00_BASELINE.sha256`를 확인합니다.
3. 순서: R2-A Red → R2-B Red → R2-A 구현 → R2-B 구현 → Fixture → 단계 시험 → Dry Pilot → verify → 보고서·기록.
4. 이미 구현된 단계는 다시 만들지 않습니다(코드에서 `HarnessData.Inputs`·`OfficialOutput.PreflightFile`·`Verifier.VerifyPreflight` 존재로 판단).
5. 명령 승인 서비스가 실패하면 보안 검사를 우회하지 않고 중단 원인을 보고합니다.
