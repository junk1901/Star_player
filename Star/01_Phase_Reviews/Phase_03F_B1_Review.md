---
type: phase-review
phase: "03F-B1"
phase_name: "Phase 3-F B-1 증거 계약 구현 (B1-T00~T10)"
status: 검토 대기
started: "2026-10-24"
completed: ""
developer_ai: Claude Code
reviewed_by: ""
branch: "main"
commit: "257403ddae557a54c4013c17705370caeac7a2e0 (미커밋 작업 트리)"
roadmap_anchor: "Phase 3-F F-C2 B-1"
last_updated: "2026-10-25"
tags:
  - star-player
  - phase-review
---

# Phase 3-F B-1 Review — B1-T00~T10 증거 계약 구현

> [!warning] 상태 규칙
> 허용 상태는 `초안`, `검토 대기`, `승인 완료`, `보완 필요`입니다. 구현 AI는 외부 검토 없이 `승인 완료`로 변경하지 않습니다. 실행하지 않은 검증은 `미검증`, 수행하지 않은 독립 리뷰는 `미실시`로 기록합니다.

## A. 기본 정보

| 항목 | 내용 |
|---|---|
| Phase | Phase 3-F F-C2 B-1 — 증거 계약 구현 (B1-T00~T10) |
| 작업 시작일 | 2026-10-24 |
| 작업 완료일 | 2026-10-24 (T10까지. T11 Codex 검수 전) |
| 개발 담당 AI | Claude Code |
| 리뷰 상태 | 검토 대기 |
| 관련 ROADMAP | [ROADMAP.md](../../ROADMAP.md) |
| 관련 Git 브랜치 | main |
| 관련 커밋 | 없음(미커밋, HEAD `257403d` 불변) |

- 대시보드: [Star Player 개발 대시보드](../00_Dashboard.md)
- 공식 현재 상태: [CURRENT_STATE.md](../../CURRENT_STATE.md)
- 정본 근거: [B1 통합 보고서](../../docs/reports/B1_INTEGRATED_REPORT.md), [Traceability](../../docs/contracts/b1/B1_TRACEABILITY.md), [작업 기록](../../docs/reports/B1_IMPLEMENTATION_LOG.md)

## B. 개발 목표

- 목적: 승인된 B1-T00 기술 계약(SHA `1338a2f7…7d64`)에 따라 공식 증거 형식·의미 Hash·독립 검증·영수증·기록부를 구현
- 범위: T00~T10(S00.5 §6 + 최종 계약), T11은 Codex
- 완료 기준: S00.5 §6 TASK별 수용 기준, 기존 Hash·RNG·Replay·Gate 불변, 전체 회귀·`verify.ps1`

## C. 실제 구현 내역

통합 보고서 1·2절. 요약: 신규 `tools/StarPlayer.SimHarness/Evidence/`(14 파일), 신규 `tools/StarPlayer.EvidenceVerifier`, 기존 `GameExecutor.cs`(관찰 Seam)·`Official.cs`(R01-A/B/D)·`Replay.cs`·`FamilyOfficial.cs`·`DominanceOfficial.cs`(권한 전달·`Reruns`)·`verify.ps1`(U6·D3) 수정.

### 계획 대비 달라진 내용

- #6 codec: 검증기 바이너리에 직접 결합하지 못함(Infrastructure 신뢰 경계 고정) → delegate 주입
- F-26 Simulation 공개 비교 메서드 대신 검증기 내부 구조 비교(Simulation 무변경)
- (2차 보완에서 해소) Calibration은 실제 엔진 12경기/층 TEST_ONLY Run으로 끝-끝 검증·Golden 작성(SEEDS_EXHAUSTED, Gate INSUFFICIENT_SAMPLE)
- R03-4 7단계 조회 수단: 닫힌 요청 형식에 조회 op가 없어 모든 응답의 Instance ID 대조로 구현, 해석 확정 전 BLOCKED([제안 3](../../docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS.md))

## D. 테스트 및 검증

통합 보고서 3~6절. Dry Pilot 9 Run 재현 동일, 독립 Golden 4세트(Calibration·Integration·Dominance·Phase) 일치, Test Profile 실제 실행(4개 어셈블리 하위 `dotnet test`+TRX), 실제 Named Pipe·AccessCheck 시험, 전체 회귀와 `verify.ps1` 결과는 보고서 3절. 시험 GATE와 TASK 수용 판정은 보고서 1절에서 분리.

### 테스트하지 못한 항목

- AT-1~AT-14r, AT-4(P-1) 정식 측정: `미검증`(별도 계정·서비스 없음). AT-4 사전 측정만 수행(Win32 1307, AT-4 PASS 아님)
- 보호 앵커 디렉터리 생성·ACL 적용과 T2 운영 실측: `미검증`(결정 1 구현·자체 시험은 완료, 관리자 작업은 별도 허가 필요)
- N27-d·e·f 직접 시험: `미검증`(별도 계정·서비스 배치 필요)

## E. 발견된 문제

- 해결: `verify.ps1` BOM 누락으로 G3·G4 오탐 FAIL → BOM 복원 후 재실행
- 해결(2차 보완 시험 작성 중): 잘못된 시험 기대 3건(Pipe 위 Instance ID, N05-c 검출 위치, 재투영 Run 짝) — 구현 결함이 아닌 시험 기대 오류, 작업 기록 참조
- 해결(2차 보완 전체 verify에서 발견): `RegistryPipeService.Dispose`와 서비스 스레드의 핸들 경쟁으로 testhost 비정상 종료(GATE INCOMPLETE) → 스레드별 핸들 소유·종료 대기로 수정 후 `GATE: PASS`(2,972 PASS)
- 결정 2 반영(3차): NSC-4 정정 판정, #6 codec 공개 진입점, `get_registry_identity` 구현·시험, N07·N32 직접 시험, 전체 verify `GATE: PASS` 3,001 PASS. 시험 기대 오류 4건 수정(구현 결함 아님)
- 미해결: [B1 미해결 항목](../03_Issues/2026-10-24_B1_Open_Items.md) — NSC-4 B안 실측 실패, #6 codec 경계, 7단계 조회 수단, 해석 수용, 미실시 목록

## F. 설계 변경

| 변경 내용 | 변경 이유 | 영향 모듈 | 결정 문서 | Codex 추가 검토 |
|---|---|---|---|---|
| `ReplayAuthority`·`VerifyEnvelope`·`BuildAttestation` 추출 | 계약 §14.5·§14.6 | Official·Replay·Family/DominanceOfficial | 승인 계약 | 미실시(T11 대상) |
| `GameExecutor` 관찰 Seam | 계약 T04a | GameExecutor | 승인 계약 | 미실시(T11 대상) |
| NSC-4·NSC-5 | OWNER 결정 | TrustAnchor·ApprovalRecord | [결정](../02_Decisions/2026-10-24_B1_T00_Owner_Approval.md) | 미실시 |
| NSC-4 정정·T05 codec 진입점·T09 신원 조회 | OWNER 결정 2 | TrustAnchor·Infrastructure Fixtures·TrustedBoundaryTests·Registry·PipeRegistry | [결정](../02_Decisions/2026-10-24_B1_Owner_Decision_2.md) | 미실시(T11 대상) |

## G. AI 리뷰

### GPT Chat 리뷰

| 항목 | 결과 |
|---|---|
| 기능명세 충족 여부 | 미실시 |
| 누락된 요구사항 | 미실시 |
| 개선 권장사항 | 미실시 |
| 최종 검토 의견 | 미실시 |

### Codex 리뷰

| 항목 | 결과 |
|---|---|
| 아키텍처 적합성 | T11 1차: FIX_REQUIRED (MAJOR 5, MINOR 3) — [수신본](../../docs/reports/B1_T11_CODEX_REVIEW_RECEIVED.md) |
| 모듈 간 의존성 | T11 1차 지적 반영(발급 경계·Archive 스냅샷) |
| 중요 로직 검증 | 재검수 미실시 |
| 구조적 위험 요소 | 재검수 미실시 |

## H. 다음 단계 인수인계

- Codex T11 통합 독립 검수(체크리스트: 통합 보고서 9절)
- OWNER 결정: [결정안 제안 1~3](../../docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS.md)(NSC-4 최소 정정, #6 codec 공개 진입점, 7단계 조회 수단)과 통합 보고서 7절 해석 1·2·3·6 수용 여부
- 공식 실행·Final Execution Freeze는 이번 작업 범위 밖

## I. 최종 결과

| 항목 | 결과 |
|---|---|
| 완료 기준 충족 여부 | 부분(통합 보고서 1절) |
| 최종 리뷰 상태 | 검토 대기 |
| 남은 문제 | 통합 보고서 8절 |
| 승인자 또는 승인 근거 | 미승인 |
| 최종 수정 날짜 | 2026-10-24 |

## J. Codex T11 FIX_REQUIRED 대응 (2026-10-25)

- Codex 1차 판정: FIX_REQUIRED(MAJOR 5, MINOR 3). 수정·자체 시험 완료, 보고서 [B1_T11_FIX_REPORT.md](../../docs/reports/B1_T11_FIX_REPORT.md)
- Red 확인: T11-04·05·07·08. T11-01·02·03·06은 경계·형식 신설로 수정 후 시험 작성(Red 실행 없음, 보고서 1절)
- `verify.ps1`: GATE FAIL — D3 하나(TASK.md 교체, OWNER 판단 필요). 시험 3,017 PASS / 0 FAIL / 0 SKIP
- 리뷰 상태: `검토 대기` 유지(Codex 재검수·OWNER 수용 전 승인 아님)
- D3 복구(2026-10-25): TASK.md를 확인된 원본(VS Code 로컬 기록, SHA `ecbc0859…` 일치)에서 복원, 교체본은 `docs/tasks/` 보관, 기준 목록 무변경. `verify.ps1` **GATE: PASS** 3,017 PASS
- 잔여 OWNER 결정안: [B1_OWNER_DECISION_PROPOSALS_3.md](../../docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS_3.md)(7항목). Codex 표적 재검수는 그 결정 뒤 요청
- OWNER 결정 3(2026-10-25 08:04) 7건 구현·자체 시험: [B1_DECISION3_REPORT.md](../../docs/reports/B1_DECISION3_REPORT.md). Dry Pilot 9 Run 동일, `verify.ps1` **GATE: PASS** 3,026 PASS. Codex READ-ONLY 표적 재검수 대기, 리뷰 상태 `검토 대기` 유지
- Codex 2차 T11 재검수 FIX_REQUIRED(R1~R4, 2026-10-25) 수정·자체 시험: [B1_T11_FIX_R2_REPORT.md](../../docs/reports/B1_T11_FIX_R2_REPORT.md) (TASK [B1_T11_FIX_R2_TASK.md](../../docs/tasks/B1_T11_FIX_R2_TASK.md)). Red 실제 확인: R1·R2·R3·R4 각 1개(추가 반례 시험은 수정 후 작성). R2 도출은 OWNER 선택 "승인 Pin 기준". 실제 Phase 영수증 끝-끝 발급 PASS(TEST_ONLY, 48셀 Seed 기준값 991000으로 변경). Dry Pilot 9 Run 동일. `verify.ps1` **GATE: PASS** 3,032 PASS / 0 FAIL / 0 SKIP. Codex READ-ONLY 표적 재검수 `미실시`, OWNER 최종 수용 `미실시`, 리뷰 상태 `검토 대기` 유지
- Codex T11 R2 표적 재검수 FIX_REQUIRED(R2-A INPUT_BUNDLE·R2-B PREFLIGHT, R1·R3·R4 RESOLVED) 최종 수정·자체 시험: [B1_T11_R2_FINAL_FIX_REPORT.md](../../docs/reports/B1_T11_R2_FINAL_FIX_REPORT.md) (TASK [B1_T11_R2_FINAL_FIX_TASK.md](../../docs/tasks/B1_T11_R2_FINAL_FIX_TASK.md)). Red 실제 확인 2건, 반례·양성 시험 추가, 실제 Phase 영수증(INPUT_BUNDLE 4·PREFLIGHT 57) 발급. Dry Pilot 9 Run 동일. `verify.ps1` **GATE: PASS** 3,039 PASS / 0 FAIL / 0 SKIP. Codex READ-ONLY R2 재검수 `미실시`, OWNER 최종 수용 `미실시`, 리뷰 상태 `검토 대기` 유지
- Codex T11 재검수 FIX_REQUIRED(R2-B MINOR 1건, R1·R2-A·R3·R4 RESOLVED) 수정·자체 시험: [B1_T11_R2B_VALUE_BINDING_REPORT.md](../../docs/reports/B1_T11_R2B_VALUE_BINDING_REPORT.md) (TASK [B1_T11_R2B_VALUE_BINDING_TASK.md](../../docs/tasks/B1_T11_R2B_VALUE_BINDING_TASK.md)). Preflight 결정적 필드 전체를 독립 재실행 기대 기록과 비교, Raw SHA를 다시 맞춘 변조 거부(RED 2건 실제 확인). Family·Phase 영수증 발급 유지. Dry Pilot 9 Run 동일. `verify.ps1` **GATE: PASS** 3,041 PASS / 0 FAIL / 0 SKIP. Codex READ-ONLY 재검수 `미실시`, OWNER 최종 수용 `미실시`, 리뷰 상태 `검토 대기` 유지
- Codex T11 최종 READ-ONLY 독립 검수 PASS(2026-10-25, OWNER 전달 요약, 원문 저장소에 없음): [수신 기록](../../docs/reports/B1_T11_CODEX_FINAL_PASS_RECEIVED.md). 코드 수용 준비 산출물: [수용 확인서 초안](../../docs/reports/B1_OWNER_ACCEPTANCE_DRAFT.md)(서명·승인 없음), [구현 목록](../../docs/reports/B1_IMPLEMENTATION_MANIFEST.md), [운영 Gate 계획](../../docs/tasks/B1_OPERATIONAL_GATE_PLAN.md). OWNER 최종 수용 `미실시`, 리뷰 상태 `검토 대기` 유지(승인 완료는 OWNER·GPT Chat 기록으로만)
- OWNER 결정(2026-10-26, [원문 O16](../../docs/contracts/b1/originals/owner/O16_20261025T151044Z_code-acceptance-and-git-baseline-decision.txt)): B1 코드 구현을 현재 검수 범위에서 **수용**. Codex T11 최종 PASS 원문 [C10](../../docs/contracts/b1/originals/codex/C10_20261025T140659Z_verdict-final-pass.md). Git 기준본 Commit·Tag는 조건부 허가(Push 불허). 이 결정은 OWNER 기록이며, 이 리뷰 문서의 상태 필드는 GPT Chat·OWNER 리뷰 절차에 따라 갱신합니다(Claude는 `승인 완료`로 바꾸지 않음). 운영 Gate·Official Execution·Phase 3 완료는 포함되지 않음
