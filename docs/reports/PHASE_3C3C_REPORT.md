# Phase 3-C3-C Closeout Report — C3-C Integration Verification & Closeout

- 작성: 2026-10-15, 소유자 `TASK.md` "Phase 3-C3-C Final Closeout Documentation & Evidence Integration" (문서만; 기준 HEAD `52cab0312180e870510bbf5be14eab3a12228b28`)
- 작성자: Claude Code (Opus 5.5; TASK 권장 모델 Sonnet — §G N1, 소유자 수용 `MODEL_DEVIATION_OWNER_ACCEPTED`). 2026-10-15 최종 정합화(소유자 `TASK.md` "Star Player C3-C Final Closeout Document Reconciliation", 문서만): Claude Sonnet 5.5
- 계약 근거: [game-engine §27.18–27.21](../game-engine.md), [ADR-007](../decisions/ADR-007.md) "Decisions (C3-C closeout)", [결정 기록](../../Star/02_Decisions/2026-10-15_C3C_Closeout.md)
- **최종 상태 코드: `C3C_CLOSEOUT_READY_FOR_OWNER_COMMIT_DECISION_WITH_NOTES`** — 문서 정합성 검사 통과 후 소유자의 로컬 Commit 결정 대기를 뜻한다. `FINAL_APPROVED`·Phase 3 Gate PASS·Commit 완료 아님. Codex 독립 리뷰 `CODEX_C3C_REVIEW_PASS_WITH_NOTES`(§I), Phase 3 Gate `PHASE_3_GATE_INCOMPLETE`, GitHub Push 금지. (이전 상태 `C3C_CLOSEOUT_DOCS_READY_FOR_CODEX_REVIEW_WITH_NOTES`는 Codex 리뷰 전 시점의 역사적 기록.)

이 보고서는 저장소 밖 비공개 증거를 요약한 **정리본**이다. 원시 로그·TRX·트랜스크립트는 Git에 넣지 않았고, 수치·판정·SHA-256·크기만 옮겼다. 보관 위치는 논리 식별자(`PRIVATE_*`)로만 적으며 실제 경로 대응은 비공개 보존 자료에 있다.

## A. Executive Summary

**목적.** C3-A(원자적 확정 관문)와 C3-B(투구 후보 확정)를 실제 연쇄·경쟁·취소·종료 경로에서 통합 검증하고(OD-2), 과거 변이 결과를 현재 트리에서 재검증하며(OD-4), 증거를 보존·정리해 공식 Closeout을 준비한다(OD-1 방식).

**완료된 작업 (상태 구분).**

| 단계 | 내용 | 상태 |
|---|---|---|
| C3-C-0 | C3-B 사후 Closeout 문서 (`828a988`) | 문서 완료 (증거 일부 `SUMMARY_ONLY`/`MISSING`, 이 보고서 §D·[3C3B Addendum](PHASE_3C3B_REPORT.md)에서 정정) |
| C3-C-1 | 통합 테스트 33건 (`03c8675`) | 테스트 완료 |
| C3-C-2 | 원자성·경쟁·취소·종료 테스트 30건 (`52cab03`) + E4 Race 5회 재검증 | 테스트 완료 |
| C3-C-3A | 변이 사전 감사 + D5 원본 증거 보존 | 원본 증거 보존 완료 |
| C3-C-3B | 격리 변이 재검증 36건 | Mutation 검증 완료 |
| C3-C-3C | 격리 Clone 전체 회귀 (xUnit 1회, `verify.ps1` 2회) | 회귀 완료 |
| E4 보존 | E4 원본 16개 보호 보관 | 원본 증거 보존 완료 |
| Closeout 문서 | 이 보고서 포함 총 9개 (기존 7개 수정 + 신규 2개) | 작성 완료 · Codex 리뷰 반영 최종 정합화 완료 (2026-10-15) |
| Codex 독립 리뷰 | OD-3 조건 | `CODEX_C3C_REVIEW_PASS_WITH_NOTES` — BLOCKER 0 / MAJOR 0 / MINOR 1(문서 수량 문구, 정정 완료). 비공개 증거는 독립 열람 안 함(§I) |
| 모델 변경 | C3-C-3C·E4 보존·Closeout 통합 (Sonnet 권장 → Opus 5.5) | `MODEL_DEVIATION_OWNER_ACCEPTED` (§G N1) |
| 로컬 Commit | 이 문서들 | 대기 (소유자 결정; 미실시) |
| Phase 3 Gate | — | `PHASE_3_GATE_INCOMPLETE` |

**미해결 NOTE.** 기술 8개(§F, B-M7·A-M25 생존 포함)와 절차·환경 NOTE(§G; 모델 예외는 `MODEL_DEVIATION_OWNER_ACCEPTED`로 해소되었으나 다른 NOTE를 면제하지 않는다). Codex 리뷰 후에도 B-M7·A-M25는 해결되지 않았다(§I).

## B. Implementation & Test Coverage

C3-C 커밋 두 개는 테스트 파일만 추가했다(`src/`·`game/`는 `8ed806f` 이후 변경 없음). 모두 `tests/StarPlayer.Infrastructure.Tests/`.

| Commit | 파일 | 테스트 수 (C3-C-3C TRX) | TC |
|---|---|---:|---|
| `03c8675` C3-C-1 | `CommitChainFixture.cs` (OD-2 고정 스크립트 연쇄, `MaxEvents = 10`, 테스트 어셈블리 전용) | — | — |
| | `CommitIntegrationChainTests.cs` | 17 | TC-01, TC-02, TC-03, TC-15 |
| | `CommitIntegrationDeterminismTests.cs` | 16 | TC-07, TC-08, TC-10 |
| `52cab03` C3-C-2 | `CommitIntegrationFinalizeTests.cs` | 19 | TC-09, TC-11, TC-14 |
| | `CommitIntegrationRaceTests.cs` | 11 | TC-04, TC-05, TC-06 |
| **합계** | | **63** (C3-C-1 33 + C3-C-2 30) | TC-01~11, TC-14~15 |

- 전체 xUnit 추이: C3-B 1,741 → C3-C-1 1,774(+33) → C3-C-2 1,804(+30). C3-C-1/2 커밋 당시 실행의 독립 원시 로그는 저장소·보관소에 없다(`SUMMARY_ONLY`); 같은 63건은 C3-C-3C 현재 트리 회귀 TRX에서 전부 PASS(`SUMMARY_VERIFIED`, 현재 트리 기준).
- TC-14(종료 경로의 `Finalize` 결과)와 TC-15(투수별 투구 수 +1)는 생존 변이 A-M25·B-M7의 **전제 고정 테스트**다. 정상 경로 PASS이며 생존 변이의 해결 근거가 아니다(§C·§F).
- E4: `CommitIntegrationRaceTests` 11건 × 5회, 5회 모두 11/11 PASS(합 55/55), 종료 코드 0 ×5, stderr 0 bytes (§E `E4-*`).
- **TC-12 / TC-13: `UNDEFINED — NOT SCORED`.** 저장소 코드·문서 어디에도 독립 정의가 없다. 추측해 정의하거나 PASS로 기록하지 않는다.

## C. Mutation Verification

출처: `PRIVATE_3B_EVIDENCE` `C3C3B_MUTATION_RUN_REPORT.md`, `mutation_results.csv`/`.json`, `mutation_inventory.json`, `mutation_sha256_manifest.json` (해시는 §E). 실행 ID `c3c3b-20261015-012452`, 기준 HEAD `52cab03`, 격리 스냅샷(소스·테스트 229개가 HEAD blob과 바이트 일치).

| 구분 | KILLED | SURVIVED | 비고 |
|---|---:|---:|---|
| C3-A 원본 재실행 | 19 | 0 | 원본 Patch가 현재 트리에 적용 가능한 것 (A-M13-ORIGINAL 포함) |
| C3-A 재정의 | 3 | 0 | A-M9R / A-M10R / A-M13R |
| C3-B 원본 재실행 | 12 | 1 | B-M7 생존 |
| A-M25 특별 검증 | 0 | 1 | 별도 분석 (원본 그대로 재실행) |
| **합계** | **34** | **2** | **실행 36건** |

분모를 혼동하지 않는다.

| 분모 | 수 | 내용 |
|---|---:|---|
| C3-A 원본 정의 | 26 | A-M1~A-M25에서 원안 A-M7(동등 변이 `INVALID_MUTANT`, 집계 제외)을 A-M7a/A-M7b로 대체. A-M25 포함. A-M26은 이 26건 밖 |
| OD-4 재실행 대상 | 21 | 26건 − A-M4·A-M7a·A-M7b·A-M19(C3-A R3에서 최종 트리 재실행됨) − A-M25(특별 검증으로 별도) |
| 21건의 실제 처리 | 19 + 2 | 원본 적용 19건 실행, A-M9·A-M10 2건 `NOT_APPLICABLE_TO_CURRENT_TREE`(원본 치환 대상 문자열 0회) |
| 실행 | 36 | C3-A 원본 19 + C3-A 재정의 3 + C3-B 원본 13 + A-M25 1 |
| 결과 행 | 38 | 실행 36 + 적용 불가 2 (적용 불가 2건은 실행 36건에 포함하지 않음) |

별도 기록.

- **A-M9 / A-M10 원본: 현재 코드에 적용 불가.** 동등 범위의 재정의 A-M9R(증거 없는 경로의 종류 거절만 무력화)·A-M10R(비증거 경로만 RNG 복사)로 검증, 둘 다 KILLED.
- **A-M13-ORIGINAL과 A-M13R은 다른 의미의 변이다.** 원본 Patch는 id-invalid 대 authority_inconsistent 우선순위만 바꾸고(이름과 불일치), A-M13R은 이름의 의도인 kind-before-id 우선순위를 바꾼다. 서로 다른 테스트가 각각 검출했다(두 계약 모두 고정). 둘 다 집계에 포함되며 이중 집계가 아니다.
- **A-M25는 C3-A 원본 26건의 일부**이며 특별 검증 1건으로만 집계한다(이중 집계 없음).
- **A-M26: 미실행** (원본 26건 밖, 별도 승인 필요). **A-M10R2: 미실행** (기본 범위 제외).
- **신규 통합 테스트 63건의 추가 변이 검출력: 0건.** 63건(CommitIntegration 필터)은 36건 모두에 실행됐고 A-M1·A-M2·A-M3·A-M5·A-M10R·A-M16·A-M20·B-M5 8건을 검출했지만 모두 원래 필터에서 이미 KILLED였고, 생존 2건(A-M25, B-M7)은 검출하지 못했다.
- 원래 필터 선택 수는 현재 트리 기준(C3-A `Authority` 77, C3-B 151)이라 과거(69/67, 150)와 다르다. 과거 실패 수와의 차이는 B-M3(27→28) 1건뿐이다.
- KILLED 34건은 모두 종료 코드 1 + TRX 실패 > 0, SURVIVED 2건은 종료 코드 0 + 실패 0 + executed = total. 모든 변이 빌드에서 Simulation DLL SHA가 기준과 달라졌음(실제 컴파일 확인), 시작·끝 Baseline Gate PASS와 DLL SHA 재현(오염 없음).

**생존 변이 (해결로 처리하지 않음).**

- **B-M7** (`CommitEvidence` 9단계 `CheckApplied` 무력화): `SURVIVED — LIKELY_REDUNDANT_GUARD / NOT_FORMALLY_PROVEN`. 정적 추적상 7b 링크 검사·`CommitGate` 영수증 대조·리듀서의 `CountPitch` 단일 호출 구조 때문에 실패 분기가 성립하지 않는다는 논증이 있으나 형식 증명이 아니다.
- **A-M25** (`Finalize` 이후 방어 무력화): `SURVIVED — UNRESOLVED`. `AuthoritativeGame` 생성 경로와 `Finalize` 불변식에 조건부인 정적 도달 불가 논증만 있다.

## D. Final Regression

출처: `PRIVATE_3C_EVIDENCE` `C3C3C_FINAL_VERIFICATION_REPORT.md`, `xunit_results.json`, `verify_comparison.json` (해시는 §E). 실행 ID `c3c3c-20261015-022440`. 원본 저장소 밖, 접근 제한 루트의 독립 로컬 Clone 3개(`core.autocrlf=false`, 원격 비활성화, 추적 파일 468개가 원본 HEAD blob과 바이트 일치, 실행 전후 불변).

| 검증 | PASS |
|---|---:|
| Domain | 263 |
| Application | 117 |
| Simulation | 697 |
| Infrastructure | 727 |
| 전체 | 1,804 |

- 명령 `dotnet test StarPlayer.sln -c Release --nologo --logger trx --results-directory <evidence>` (필터 없음). **FAIL 0, SKIP 0, EXIT 0**, TRX 4개(프로젝트당 1개)와 콘솔 요약이 프로젝트별로 일치, stderr 0 bytes.
- `tools/verify.ps1` (스크립트 무수정, 고유 `-LogName`으로 추적 로그 미변경, 서로 다른 Clone):
  - Run 1: `GATE: PASS` / EXIT 0 — 내부 검사 22 PASS + 2 INFO (FAIL 0, INCOMPLETE 0).
  - Run 2: `GATE: PASS` / EXIT 0 — 22 PASS + 2 INFO.
  - 두 회차의 24개 검사 상태, 테스트 수(U1 263, U2 697, U3 117, U3a 14, U3b 48, U4 727), `verify.ps1` SHA-256, 툴체인이 모두 일치. 로그 SHA-256 차이는 시각·Clone 경로 때문이며 결정성 실패가 아니다. 두 번의 PASS는 게임 엔진 결정성의 형식 증명이 아니다.
  - verify TRX: `NOT_APPLICABLE` (스크립트가 `--logger` 없이 실행해 TRX를 만들지 않음).

## E. Evidence Provenance

분류: `RAW_VERIFIED`(원본 존재, 해시·내용 대조), `TRANSCRIPT_RECOVERED`, `SUMMARY_VERIFIED`, `SUMMARY_ONLY`, `MISSING`, `NOT_APPLICABLE`. 확인 시점은 모두 2026-10-15 이 Closeout 사전 재확인(읽기 전용)이다.

| 논리 식별자 | 의미 | 접근 |
|---|---|---|
| (저장소) `docs/reports/` | C3-C-0 원본 7개 (Git 추적) | 공개 저장소 대상 |
| `PRIVATE_D5_VAULT` | C3-A/C3-B 트랜스크립트 원본 3 + C3-C-3A 사전 감사 1 | 상속 차단, 현재 사용자·SYSTEM·Administrators만 |
| `PRIVATE_E4_VAULT` | E4 원본 16 + 매니페스트·보존 보고서 | 상속 차단, 같은 3주체만, CodexSandbox 계정 실효 접근 0 (AuthZ) |
| `PRIVATE_3B_EVIDENCE` | C3-C-3B 변이 증거 276개 | 상속 차단, 같은 3주체만 |
| `PRIVATE_3C_EVIDENCE` | C3-C-3C 회귀 증거 97개 | 상속 차단, 같은 3주체만 |

**C3-C-0 (Git, `828a988`; HEAD와 blob 동일)** — `RAW_VERIFIED`. 한계: 두 verify는 Commit 이전 `654b5d9` dirty 트리에서 실행.

| 파일 (`docs/reports/`) | 크기 (B) | SHA-256 |
|---|---:|---|
| `PHASE_3C3B_PRIORITY_VERIFY_LOG_run1.md` | 5,647 | `4a5e9e13647d3b98d5a17bd9bc080402e599e25af1f2de09ba2303d409001886` |
| `PHASE_3C3B_PRIORITY_VERIFY_LOG_run2.md` | 5,647 | `3f88950f44e51c8e69d75de579152bfb4b1ad4ebb6b4574e78b6a77f2df1c531` |
| `PHASE_3C3B_PRIORITY_VERIFY_STDOUT_run1.txt` | 2,846 | `53f7e5ebb7ac1169d185bf19278ee556c994805feb25defe8c4efd8b74fc2efe` |
| `PHASE_3C3B_PRIORITY_VERIFY_STDOUT_run2.txt` | 2,846 | `82dbe328604676bb6d96f1c188667e1403b292155862d580346b7073cf19c55c` |
| `PHASE_3C3B_TREE_MANIFEST_snap0_before_run1.txt` | 27,932 | `e33195260ed6eff007417677b96c1b7e539c07cfc268729e678c9e72eaa6241a` |
| `PHASE_3C3B_TREE_MANIFEST_snap3_before_commit.txt` | 27,932 | `e33195260ed6eff007417677b96c1b7e539c07cfc268729e678c9e72eaa6241a` |
| `PHASE_3C3B_TREE_MANIFEST_snap.ps1.txt` | 539 | `522050d2d379219a63e1daf9c49b247ff235f25dd78e1518eafd5942ed33851f` |

**C3-C-1 / C3-C-2** — 테스트 소스는 Git(`03c8675`, `52cab03`). 커밋 당시 실행 원본: `SUMMARY_ONLY`. 현재 트리 결과: C3-C-3C TRX로 `SUMMARY_VERIFIED`.

**`PRIVATE_D5_VAULT`** — `TRANSCRIPT_RECOVERED` (사본 4/4 SHA-256·크기가 D5 매니페스트와 일치). 한계: 트랜스크립트는 당시 변이 정의·실행 기록을 담지만 당시의 독립 stdout/stderr 원본 파일은 아니다. 트랜스크립트는 개인정보를 포함해 Git 편입 금지.

| ID | 유형 | 크기 (B) | SHA-256 |
|---|---|---:|---|
| EV-A1 | C3-A 변이 Round 1·2 트랜스크립트 (JSONL) | 1,849,560 | `8dd8a3becebce57f10b50d54d5d36306e8cc2bb44be6eb93abdf624cdd49c957` |
| EV-A2 | C3-A 변이 Round 3 트랜스크립트 (JSONL) | 1,166,515 | `e47036eba690440d4a13044d1474791aa0c19ec6f637c32ac3c2219b8121fafa` |
| EV-B1 | C3-B 변이 실행 트랜스크립트 (JSONL) | 2,347,862 | `c28f6efc2d821c2b945532e28668e6dbbd2bb35b1e782b313426127070bb19bf` |
| EV-R1 | C3-C-3A 변이 사전 감사 보고서 | 22,383 | `688e1385c2313c1b326589be5124861a519f8e13cf07c01902ffc867229b5d1a` |
| (manifest) | D5 `evidence_manifest.json` | 6,219 | `23ac024fc17a71cce4214c5c8d0dfb915162af28d40b17a63c20946fc0e74bf3` |
| (report) | D5 `preservation_report.md` | 6,909 | `fd56a2bd5c4c4736fc3273fa4d5498444852a445bb40b572b0c6c972fbaee33a` |

**`PRIVATE_E4_VAULT`** — `RAW_VERIFIED` (보존 시점부터). 상태 `E4_RAW_CONTENT_VERIFIED_AT_PRESERVATION`, **`E4_NO_HISTORICAL_SHA256_BASELINE`**: E4 실행 당시(2026-10-15 00:00 KST) 해시 기록이 없어 아래 해시는 보존 이후 바이트만 보증하며 과거 바이트를 소급 증명하지 않는다. E4는 원본 저장소에서 Debug 빌드로 실행됐고 당시 작업 트리 상태는 이 파일로 증명할 수 없다. 원본은 임시 폴더에 그대로 남아 있다(보관본이 기준).

| ID | 크기 (B) | SHA-256 |
|---|---:|---|
| E4-TRX-R1 | 19,516 | `ff635ee7fe0c5578ad077931b0ad9986d13e75f588da763fe11261f0cf5a81fc` |
| E4-TRX-R2 | 19,516 | `2f793fcb44b739a0ab2e0b020623db48df08b6ce99465c58b944a67aa4ff6e28` |
| E4-TRX-R3 | 19,516 | `df7ba83b94f24435c36f5b9765a6a803280080fede7960ee6d9a4472ce4de3ee` |
| E4-TRX-R4 | 19,516 | `87e7f1accfe2cf883abbf974b3286acc0fbb2b32f05356641d6eb7942e71a4d9` |
| E4-TRX-R5 | 19,516 | `8cd0e8bac3d83d4e3332f305947a964cba66b76303765adba72c47e382ac30b6` |
| E4-STDOUT-R1 | 1,225 | `b96b2bd4241f443a0fb0961d6eea27203caba7d874762f1ab4162e8d0ef7f853` |
| E4-STDOUT-R2 | 1,225 | `290a68f77d76e324340305d77db81590a36a5e7c59a3420046629edd1a020ed6` |
| E4-STDOUT-R3 | 1,225 | `ced750f2c4446addc22b2ec447f702754d0405090a8f6d6ebecf97b1fdd3cb9a` |
| E4-STDOUT-R4 | 1,225 | `09b876d1b7d21926856393375a3d4c69f380fa19e5c0a7b8a63105a3cd2921a5` |
| E4-STDOUT-R5 | 1,225 | `2df1ebffc46df5c305e7e149349a8714b9a249dc0d1c9f90e622b609d461faed` |
| E4-STDERR-R1~R5 | 0 (각각) | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` (빈 파일) |
| E4-EXIT-CSV | 1,330 | `0aa41662e499528611b75c1165f8a89ab331dae2241c6cb118601f6cd477abbc` |
| `e4_evidence_manifest.json` | 20,371 | `dbf928e29d5fe7681a1a8b8c32e7d902502d15c96778883609f992b09cd97766` |
| `e4_preservation_report.md` | 7,896 | `2241bbfd791e7e58c8cf6b92c1c78e4c3d86e43f615ab76e42894b6fb2395610` |

**`PRIVATE_3B_EVIDENCE`** — `RAW_VERIFIED` (매니페스트 275개 해시 재계산 일치; Patch 36, 실행 로그 154, TRX 78). 한계: 현재 트리 재실행이며 과거 실행의 원시 출력 자체는 아니다.

| 파일 | 크기 (B) | SHA-256 |
|---|---:|---|
| `C3C3B_MUTATION_RUN_REPORT.md` | 19,795 | `8d60cfd23f1bb784ef8799b9c807390a3f6d74ef0fbcf34ca17e402e9ea5904f` |
| `mutation_inventory.json` | 28,009 | `874c2200ba992ac0fcedcfd1788abc707c474fa48421cfb2c48c30fb1d263226` |
| `mutation_results.json` | 96,773 | `cba01d7df92bd46808a7e52eb786aef0cc08c36d7f162516aa928d1dbe58ec3e` |
| `mutation_results.csv` | 8,456 | `640bc020b921686bb63b11ee23ebd030a022e3e2163e121f5bf046f9e924a3a4` |
| `mutation_sha256_manifest.json` | 81,521 | `272727e872026b9b76f8a2c173ee5d1943609007758509b8fe6b384e401ca366` |

**`PRIVATE_3C_EVIDENCE`** — `RAW_VERIFIED` (매니페스트 96개 해시 재계산 일치). TRX에는 장치명·로컬 경로가 있어 Git 편입 금지.

| 파일 | 크기 (B) | SHA-256 |
|---|---:|---|
| `C3C3C_FINAL_VERIFICATION_REPORT.md` | 14,888 | `9a7012f70a07258ca59f69e7a10da737ad79eb3c39bfd3f14165510b8988afce` |
| `evidence_sha256_manifest.json` | 15,525 | `1bf9ca684801e13503b824ee37f38db8623f4b274830d86f1d6da74207e7ac0f` |
| `xunit_results.json` | 4,437 | `5a93f83f55e4afb88b61ff70e254aa8554da447851ba2f7a04df941137d23721` |
| `verify_comparison.json` | 2,354 | `052aacbffc932163ffe8725f2ee6fedfcd46f27a1362db99e7fab10670f571ce` |
| `source_sha256_manifest.json` | 60,024 | `b66797dc4ba1f09e6556521f05c091b0f3b6b9a40129cfc603c8a427cc2ea9eb` |
| `C3C3C_VERIFY_RUN1.md` | 5,683 | `87ce16d829ceb709e467c298817c737a693d2dfc2393487a7229420e54117cb6` |
| `C3C3C_VERIFY_RUN2.md` | 5,683 | `3af34182972e84cd82ccb454b0d1c6877eaf123659901c27d782076d2c342f90` |
| xUnit TRX Domain | 409,889 | `0914238eb1e2ad923b87b93d9f8cbe406ce6fd8286d051ba6e5650210a24484c` |
| xUnit TRX Simulation | 1,143,761 | `75f67e79a1dd7a6ee2603521754805644a9a77f9874e68c9217c0491eac2b07b` |
| xUnit TRX Infrastructure | 1,215,164 | `2c0e94187e1d6de856cc1353d8bb2d909deb33e41845fe4f059845113f210c2d` |
| xUnit TRX Application | 193,016 | `7769b0debf1d1dd1371c9309feee2f0c5b6e7bf678a523c2c9d4c0c1e5e608b6` |

여전히 없는 원본(`MISSING`, 숨기지 않음): C3-B 구현 완료 보고서 원본; C3-A·C3-B 변이 당시의 독립 stdout/stderr 파일(트랜스크립트 기록만 있음); C3-C-1/2 커밋 당시 전체 xUnit 단독 실행 원본.

**SHA-256 기재 수 집계 (2026-10-15 정합화 확인).** 이 보고서에는 64자리 SHA-256 문자열이 **43개** 있고 고유 값은 **42개**다. 차이 1은 C3-C-0 트리 매니페스트 `snap0_before_run1`과 `snap3_before_commit`이 바이트 동일한 파일이라 같은 해시(`e3319526…241a`)가 두 행에 쓰인 것이다. 이전 통합 보고의 "42개"는 고유 값 수와, Codex 확인의 "43개"는 문자열 총수와 일치하며 해시 오류가 아니다. 집계 기준이 다른 별개 수치는 구분한다: 한 행(`E4-STDERR-R1~R5`)의 해시 1개는 빈 파일 5개를 대표하고, 매니페스트 등재 수(`PRIVATE_3B_EVIDENCE` 275, `PRIVATE_3C_EVIDENCE` 96)와 해시 재계산 일치 수는 이 문서의 기재 수와 별개다. 개별 해시 값은 변경하지 않았다.

## F. Unresolved Technical Notes

OD-3에 따라 1·2는 **잠정 비차단 NOTE**다. Codex 독립 리뷰(`CODEX_C3C_REVIEW_PASS_WITH_NOTES`, §I)가 완료됐으나 둘 다 해결로 재분류되지 않았다. 아래 표의 "독립 리뷰" 열은 리뷰 전 권고이며 처리 결과는 §I.

| # | NOTE | 현재 상태와 근거 | Closeout 차단 | 독립 리뷰 |
|---|---|---|---|---|
| 1 | B-M7 | SURVIVED, 중복 방어 가능성(정적 논증, 형식 증명 아님). TC-15 PASS는 전제 고정일 뿐 해결 근거 아님 | 아니오(잠정) | 필요 |
| 2 | A-M25 | SURVIVED, UNRESOLVED(조건부 정적 도달 불가 논증). TC-14 PASS는 전제 고정일 뿐 해결 근거 아님 | 아니오(잠정) | 필요 |
| 3 | TC-14 Production 의존 | `CommitIntegrationFinalizeTests` TC-14가 Production `AuthoritativeGame.IsComplete`를 일부 호출(독립 오라클 `ChainChecks.AssertComplete`와 병행) | 아니오 | 권장 |
| 4 | Reducer Oracle | `CommitIntegrationRaceTests`의 경쟁 기대값이 Production `GameReducer.Apply`/`Finalize`를 재사용 | 아니오 | 권장 |
| 5 | Seed 탐색 상한 | `FindNonInPlay` 20,000 / `FindInPlay` 60,000 / `CommitChain.Find` 3,000 (테스트 기본값) | 아니오 | — |
| 6 | CAS 이후 취소 | CAS 이후 취소를 직접 주입할 지점이 없어 검증 제한 | 아니오 | 권장 |
| 7 | Race 승자 분포 | 반복별 승자 분포 미기록 | 아니오 | — |
| 8 | TC-12/13 | `UNDEFINED — NOT SCORED` | 아니오(소유자 확인 필요) | — |

## G. Procedural & Environment Notes

| ID | NOTE | 상태 |
|---|---|---|
| N1 | 모델: C3-C-3C 최종 회귀와 E4 보존은 TASK 지정(Claude Sonnet)과 달리 Claude Opus 5.5로 실행됨(이 Closeout 문서 작성도 Opus 5.5; C3-C-3B 보고서도 실행자를 Opus 5.5로 기록). 결과는 고정 스크립트·원시 로그·SHA-256으로 뒷받침되며 기술 PASS와 절차 준수는 별개. 2026-10-15 소유자 결정: "모델 다른거 사용은 상관없음" → 실제 사용 모델과 원래 권장 모델은 그대로 두고 수용만 기록하며 모델 변경만을 이유로 재실행하지 않는다. 이후 모델 지정은 필수로 선언하지 않는 한 권장사항. 다른 기술·보안 NOTE를 면제하지 않음 | **`MODEL_DEVIATION_OWNER_ACCEPTED`** (이전: `OWNER_ACCEPTANCE_PENDING`, 역사적 상태) |
| N2 | NuGet: C3-C-3C verify Run 1 빌드 중(02:27:00–01 KST) `%LOCALAPPDATA%\NuGet\v3-cache`의 nuget.org 서비스 인덱스·취약성 데이터 캐시가 갱신됨(파일 시각). 새 패키지 다운로드 없음, GitHub 접근 없음. 실제 네트워크 트래픽은 기록하지 않아 확정 불가 | `NETWORK_ACCESS_UNVERIFIED` (메타데이터 조회 정황) |
| N3 | Godot: verify 실행 중 `%APPDATA%\Godot`의 `editor_settings-4.7.tres`, `app_userdata\Star Player\logs`, `mono\build_logs`(Clone별)가 갱신됨(파일 시각; 내용 미열람). Clone 격리와 사용자 수준 Godot 설정 격리는 별개 | 격리 제한 기록 |
| N4 | `harness/`: C3-C-3C 격리 루트 안(저장소 밖)의 검증 보조 스크립트, 원본 Git 무영향, 해시는 3C 매니페스트에 포함 | 범위 위반 아님 |
| N5 | E4 당시 원본 SHA-256 부재 | `E4_NO_HISTORICAL_SHA256_BASELINE` (해소되지 않음) |
| N6 | C3-C-3B 실행 승인 범위는 "TASK.md 실행" 지시의 해석이었다(3B 보고서 A.3) | 기록 |

## H. Security & Privacy

- Claude Code 트랜스크립트(JSONL) 원본은 Git에서 제외하고 `PRIVATE_D5_VAULT`에만 둔다(이메일, 조직 UUID, 비공개 저장소 URL, 세션 내용 포함).
- 로컬 개인 식별 정보(사용자 홈 절대경로, 장치명, 계정 SID, 세션 ID)는 Git 문서에 넣지 않았다. TRX·원시 로그는 비공개 보관소에만 있다.
- 비공개 증거는 상속 차단 ACL(현재 사용자·SYSTEM·Administrators)로 보호한다. E4와 D5는 Windows AuthZ로 CodexSandbox 계정의 실효 접근 0을 확인했다(양성 대조 포함). AuthZ는 특권(백업·소유권 획득)을 모델링하지 않는다.
- E4·D5 디스크 암호화: `ENCRYPTION_UNKNOWN`.
- 이 보고서는 원본 증거가 아닌 **정리본**이다. 수치·판정은 원본(위 SHA-256)과 대조했고 원본 내용을 바꾸지 않았다.

## I. Codex Independent Review (OD-3) — `CODEX_C3C_REVIEW_PASS_WITH_NOTES`

출처: 소유자가 전달한 Codex 독립 리뷰 결과(2026-10-15). 이 문서는 전달된 판정을 기록할 뿐 Codex를 다시 호출하지 않았다.

| 항목 | 값 |
|---|---|
| BLOCKER | 0 |
| MAJOR | 0 |
| MINOR | 1 — 이 보고서 §A의 "이 보고서 외 공식 문서 9개"가 "이 보고서 포함 총 9개"여야 함 (**정정 완료**) |
| 기존 기술 NOTE | 유지 (§F) |
| 필수 Production 수정 / 필수 테스트 수정 | 0 / 0 |
| 리뷰 중 코드·문서 변경 | 0 |
| Codex의 Mutation·xUnit·`verify.ps1` 실행 | 0 |

**검증 범위 — 두 개념을 구분한다.**

- **A. Production 계약 독립 리뷰: 수행.** Codex가 실제 Production 코드와 테스트를 읽고 검토한 범위에서 승인 차단 계약 위반을 발견하지 못했다. 계약 위반이 없음을 증명한 것은 아니다.
- **B. 비공개 증거 독립 검증: 미수행.** Codex는 접근 제한 때문에 비공개 Mutation Patch·TRX·원본 로그를 직접 열람하지 못했다. 비공개 증거에 대한 Codex 검증 범위는 **`REPORTED_AND_ATTESTED_NOT_INDEPENDENTLY_READ`**(보고·확약 받음, 독립 열람 아님)이다. 이는 Claude Code가 앞서 직접 수행한 D5·E4·3B·3C 해시 검사(§E)를 무효화하지 않으며, 반대로 Codex가 그 원본까지 독립 검증했다는 뜻도 아니다.

**생존 Mutation (Codex 기술 검토 반영; 형식 증명 아님).**

| ID | 대상 | Codex 확인 | 유지 상태 |
|---|---|---|---|
| B-M7 | `CommitEvidence.CheckApplied`의 ef2 검증 및 투구 수 증가 검사 | 선행 링크 검증과 공통 Commit 코어에서 관련 검증이 수행되고 현재 Reducer의 정상 투구 경로에서 `CountPitch`가 호출되므로 정상 경로 중복 방어라는 논거는 타당. 원본 Patch 직접 열람 불가, 모든 비정상 상태에 대한 동등성은 증명되지 않음 | `SURVIVED / LIKELY_REDUNDANT_GUARD / NOT_FORMALLY_PROVEN` |
| A-M25 | `CommitGate`의 `Finalize` 후 방어 검사 | 정상 생성·복원 경로에서 완료 상태가 검증되고 현재 정상 `Finalize` 경로의 실패 사례 미발견, Production `Finalize` 교체 훅 없음. 내부 생성자·Reflection으로 만든 상태는 정상 경로와 구별해야 함. 생존 Mutation이 제거·검출된 것은 아님 | `SURVIVED / UNRESOLVED` |

"중복 방어의 근거를 Codex가 확인했다"와 "형식적 동등성을 증명했다"는 다른 주장이다. 새 Production 우회 훅을 추가하지 않았고 테스트를 실행하지 않았다.

**OD-3 상태.** 독립 코드 리뷰: `COMPLETE WITH NOTES`. 생존 Mutation: `UNRESOLVED NOTES`. 잠정 비차단 취급: 기존 OD-3 정책 유지. 실제 계약 위반이 없다고 증명된 것으로 과장하지 않는다.

**테스트 NOTE 재확인 (Codex 확인 사실; §F 3·4·5·6·8 보완).**

- **TC-14 Oracle:** Production `AuthoritativeGame.IsComplete` 호출이 있으나 테스트에는 별도의 완료 조건 검사와 실제 필드 비교도 있어 Production 호출 하나에만 의존하는 순환 검증은 아니다. A-M25 해결 근거로 쓰지 않는다.
- **Race Oracle:** `GameReducer.Apply/Finalize`를 재사용하므로 Reducer 자체의 동일한 야구 규칙 오류는 독립적으로 탐지하지 못할 수 있다. CAS 게시·영수증·Revision·RNG 등에 대한 독립 검사는 있다.
- **CAS 경쟁 순서:** TC-04·TC-05는 양쪽 게시 순서를 결정적으로 검증한다. 실제 스레드 경쟁에서 양쪽이 모두 승리했다는 통계적 증거와 구분한다(승자 분포 미기록, §F 7).
- **Seed 탐색:** 상한 초과는 테스트 실패다. 다만 후보 생성·해결 실패를 다음 Seed로 넘기는 경로가 있어 일부 생성기 결함이 관측되지 않을 수 있다. 모든 Seed·생성 분기의 정상성을 증명한다고 기록하지 않는다.
- **Cancellation:** CAS 이전 취소와 성공 후 반환된 상태에서의 취소는 테스트됐다. 정확한 CAS 직후 순간을 직접 주입한 테스트는 없고, 그 안전성은 코드 구조에 관한 논거다.
- **TC-12 / TC-13:** `UNDEFINED — NOT SCORED` 유지. 존재하지 않는 테스트를 만들거나 PASS로 기록하지 않는다.

## J. Remaining Gates

- Codex 독립 리뷰: **완료** (`CODEX_C3C_REVIEW_PASS_WITH_NOTES`, §I). 이전 `미실시` 기록은 리뷰 전 시점의 역사적 기록이다. 추가 Codex 리뷰는 요청하지 않는다.
- 리뷰 결과 반영: 완료 — MINOR 문구 정정, B-M7·A-M25 재분류 없음(`SURVIVED` 유지).
- 모델 예외: `MODEL_DEVIATION_OWNER_ACCEPTED` (이전 PENDING 해소).
- 이 문서들의 로컬 Commit: **소유자 결정 대기** (미스테이징·미커밋; Commit 완료 아님).
- Phase 3 Gate: **`PHASE_3_GATE_INCOMPLETE`** (3-C3-C 완료가 Phase 3 완료가 아님; 3-D 이후 미착수).
- GitHub Push: 금지 (Phase 3 최종 Gate 후 별도 승인).

**최종 상태 코드: `C3C_CLOSEOUT_READY_FOR_OWNER_COMMIT_DECISION_WITH_NOTES`**
