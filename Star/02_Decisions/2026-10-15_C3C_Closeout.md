---
type: decision
date: "2026-10-15"
status: C3-C 테스트·변이 재검증·전체 회귀·증거 보존 완료, Closeout 문서 작성·최종 정합화 완료 — Codex 독립 리뷰 CODEX_C3C_REVIEW_PASS_WITH_NOTES, 모델 예외 MODEL_DEVIATION_OWNER_ACCEPTED, 로컬 Commit 소유자 결정 대기, PHASE_3_GATE_INCOMPLETE
related_phase: "03"
tags:
  - star-player
  - decision
  - phase-3-c3
---

# 2026-10-15 — Phase 3-C3-C Closeout 기록

> [!warning] 문서 작성 완료 ≠ 최종 승인
> C3-C 기술 검증(테스트·변이·회귀)과 증거 보존은 실제로 수행됐다. 이 문서를 포함한 Closeout 문서 9개(기존 7개 수정 + 신규 2개)는 작성 후 Codex 독립 리뷰(`CODEX_C3C_REVIEW_PASS_WITH_NOTES`)를 거쳐 2026-10-15 정합화됐다. 그래도 C3-C 최종 기술 승인, Phase 3 Gate PASS, 로컬 Commit, GitHub Push 어느 것도 아니다.

## 출처
- 소유자 `TASK.md` "Phase 3-C3-C Final Closeout Documentation & Evidence Integration"(2026-10-15, 기준 HEAD `52cab03`, 문서만).
- 공식 보고서: [PHASE_3C3C_REPORT](../../docs/reports/PHASE_3C3C_REPORT.md) · 계약 기록: [game-engine §27.21](../../docs/game-engine.md) · [ADR-007 "Decisions (C3-C closeout)"](../../docs/decisions/ADR-007.md) · 이전 결정: [2026-10-13 C3 설계 승인](2026-10-13_C3_Design_Approval.md).
- 정정 Addendum: [PHASE_3C3B_REPORT](../../docs/reports/PHASE_3C3B_REPORT.md) 끝 절.

## 수행된 사실 (확정)
| 항목 | 내용 |
|---|---|
| C3-C-1 | 통합 테스트 33건(`03c8675`, OD-2 고정 연쇄 Fixture `MaxEvents = 10`, 테스트 어셈블리 전용) |
| C3-C-2 | 원자성·경쟁·취소·종료 테스트 30건(`52cab03`), E4 Race 11건 × 5회 PASS |
| 전체 회귀 (C3-C-3C) | xUnit 1,804 PASS / FAIL 0 / SKIP 0 / EXIT 0, `verify.ps1` 2회 `GATE: PASS`(22 PASS + 2 INFO, 회차 간 일치) |
| 변이 (C3-C-3B) | 실행 36건: KILLED 34, SURVIVED 2(B-M7, A-M25) |
| 증거 보존 | D5(트랜스크립트 3 + 사전 감사 1), E4(원본 16), 3B·3C 증거 — 모두 저장소 밖 접근 제한 보관 |

## 정책과 처리 (OD-1~OD-4 이행 기록)
- **변이 원본 복구와 재정의.** C3-C-0이 `MISSING`으로 기록한 C3-B 변이 정의와 C3-A 원본 정의는 세션 트랜스크립트에서 복구됐다(`TRANSCRIPT_RECOVERED`). 원본은 그대로(`EXACT`) 재실행하고, 현재 코드에 적용할 수 없는 것만 재정의(`RECONSTRUCTED`)했으며 둘을 다른 ID로 구별한다. 당시의 독립 stdout/stderr 원본 파일은 여전히 없다.
- **A-M9 / A-M10 적용 불가.** 원본 치환 대상이 현재 코드에 없어 `NOT_APPLICABLE_TO_CURRENT_TREE`로 기록하고 실행 36건에 넣지 않는다. 동등 범위 재정의 A-M9R·A-M10R은 KILLED. A-M13은 원본(A-M13-ORIGINAL)과 재정의(A-M13R)가 다른 계약을 검사하므로 둘 다 유지한다. A-M26·A-M10R2 미실행.
- **B-M7 / A-M25 잠정 NOTE (OD-3).** 둘 다 SURVIVED. B-M7은 `LIKELY_REDUNDANT_GUARD / NOT_FORMALLY_PROVEN`, A-M25는 `UNRESOLVED`. TC-14·TC-15는 전제 고정 테스트이며 해결 근거가 아니다. 잠정 비차단 NOTE로 유지하고 Codex 독립 리뷰 전까지 해결로 처리하지 않는다.
- **TC-12 / TC-13.** 독립 정의를 찾지 못해 `UNDEFINED — NOT SCORED`. 새 정의를 만들지 않는다.
- **증거 보존 방식.** 원본은 바이트 복사(`File.Copy`, 덮어쓰기 금지) 후 SHA-256·크기 대조, 상속 차단 ACL(현재 사용자·SYSTEM·Administrators), CodexSandbox 계정 실효 접근 0 확인(AuthZ, 양성 대조 포함). E4는 당시 해시가 없어 `E4_NO_HISTORICAL_SHA256_BASELINE`. 암호화 `ENCRYPTION_UNKNOWN`.
- **공식 Git 증거 최소화.** Git에는 수치·판정표·종료 코드·Gate 결과·SHA-256·크기·식별자·한계만 둔다. 트랜스크립트, 원시 로그·TRX, 홈 경로·장치명·계정 SID·세션 ID는 편입하지 않으며 보관 위치는 논리 식별자(`PRIVATE_D5_VAULT`, `PRIVATE_E4_VAULT`, `PRIVATE_3B_EVIDENCE`, `PRIVATE_3C_EVIDENCE`)로만 적는다.

## 결정 상태
| 항목 | 상태 |
|---|---|
| OD-1~OD-4 이행 | 수행 완료 (OD-3 Codex 독립 리뷰도 `COMPLETE WITH NOTES`) |
| B-M7 / A-M25 잠정 NOTE 유지 | OD-3 기존 승인에 따른 처리. Codex 리뷰 후에도 재분류 없음 (B-M7 `LIKELY_REDUNDANT_GUARD / NOT_FORMALLY_PROVEN`, A-M25 `UNRESOLVED`) |
| N1 모델 예외 (C3-C-3C·E4 보존·Closeout 통합이 권장 Sonnet 대신 Opus 5.5로 실행) | **`MODEL_DEVIATION_OWNER_ACCEPTED`** — 2026-10-15 소유자 결정, 아래 "소유자 확정 결정". (이전 `PENDING_OWNER_DECISION`은 역사적 상태) |
| N2 NuGet 네트워크 | `NETWORK_ACCESS_UNVERIFIED` (메타데이터 조회 정황, 패키지 다운로드 없음) |
| N3 Godot 공유 사용자 경로 쓰기 | 기록만 (격리 제한) |
| Git 증거 편입 범위 | 이번 문서 9개의 요약 수준만 반영(권장안). 원시 증거 편입은 하지 않음 |
| Codex 독립 리뷰 | **완료** — `CODEX_C3C_REVIEW_PASS_WITH_NOTES` (OD-3; 아래 "Codex 권고") |
| 로컬 Commit | 소유자 결정 대기 (미승인, 미실시) |
| Phase 3 Gate | `PHASE_3_GATE_INCOMPLETE` 유지; GitHub Push 금지 |

## 소유자 확정 결정 (2026-10-15)
- **모델 변경 수용.** 소유자: "모델 다른거 사용은 상관없음". 대상 작업 C3-C-3C 최종 검증·E4 증거 보존·Closeout 문서 통합(권장 Sonnet → 실제 Opus 5.5)의 승인 대기 상태를 해소: `MODEL_DEVIATION_OWNER_ACCEPTED`. 실제 사용 모델과 원래 권장 모델은 소급 수정하지 않고, 모델 변경만을 이유로 테스트를 재실행하지 않으며, 이후 모델 지정은 필수로 선언하지 않는 한 권장사항으로 취급한다. 다른 기술·보안 NOTE를 일괄 면제하지 않는다.
- 로컬 Commit, Phase 3 Gate 통과, GitHub Push는 **소유자가 아직 승인하지 않았다.**

## Codex 권고 (기술 판단; 소유자 결정 아님)
- 판정 `CODEX_C3C_REVIEW_PASS_WITH_NOTES`: BLOCKER 0 / MAJOR 0 / MINOR 1("이 보고서 외 공식 문서 9개" → "이 보고서 포함 총 9개", 정정 완료). 필수 Production·테스트 수정 0. Codex는 Production 코드와 테스트를 독립 검토했으나 비공개 Mutation Patch·TRX·원본 로그는 열람하지 못했다: `REPORTED_AND_ATTESTED_NOT_INDEPENDENTLY_READ` (Claude Code의 기존 해시 검사 결과를 무효화하지도, Codex가 원본을 검증했다고 주장하지도 않는다).
- B-M7: 정상 경로에서 중복 방어라는 논거는 타당하나 모든 비정상 상태의 동등성은 미증명 → `SURVIVED / LIKELY_REDUNDANT_GUARD / NOT_FORMALLY_PROVEN`. A-M25: 정상 `Finalize` 경로의 실패 사례는 없으나 Production 교체 훅이 없어 비정상 상태 구별 필요, 생존 Mutation이 제거·검출된 것은 아님 → `SURVIVED / UNRESOLVED`.

## 이후 단계에 남길 NOTE
- B-M7·A-M25 생존(형식 증명·도달 불가 증명 미완료), TC-14 Production `IsComplete` 호출 병행, Race Oracle의 `GameReducer` 재사용, 양쪽 게시 순서 결정적 검증 ≠ 실제 스레드 양쪽 승리 통계, Seed 탐색 생성 실패 skip 경로, CAS 직후 취소 직접 주입 부재, TC-12/13 `UNDEFINED — NOT SCORED`.
- 절차: NuGet `NETWORK_ACCESS_UNVERIFIED`, Godot 공유 사용자 경로 쓰기, `E4_NO_HISTORICAL_SHA256_BASELINE`.
