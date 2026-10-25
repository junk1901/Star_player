# Star Player — B1-T11 최종 통합 독립 검수

## 1. 검수 목적

Claude Code가 구현한 B1-T00\~T10 전체에 대해 Codex 독립 검수를 수행한다.

이번 검수는 전체 B1 구현에 대한 최종 통합 리뷰다. 중간 TASK별 리뷰를 반복하지 않는다.

검수 방식: READ-ONLY

코드·문서·TASK.md·Git 파일을 수정하지 않는다. Commit·Push·Staging도 금지한다. 추가 테스트 실행이 저장소 파일을 변경할 가능성이 있다면 임의 실행하지 말고 필요한 시험을 보고한다.

## 2. 검수 기준

프로젝트:\
`E:\Madegame\Star_player`

기준 HEAD:\
`257403ddae557a54c4013c17705370caeac7a2e0`

승인된 최종 B1 기술 계약:\
`B1-T00_FINAL_CONTRACT_DRAFT.md`

계약 SHA-256:\
`1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64`

검수 전에 실제 SHA, HEAD, 승인 기록, OWNER 결정 기록, 현재 작업 트리 상태를 확인한다.

최종 계약과 승인된 OWNER 결정 기록을 규범 기준으로 사용한다. 제안서나 오래된 작업 지시서가 이를 임의로 대체하지 않도록 한다.

## 3. 필수 검토 자료

다음 문서를 먼저 읽는다.

- `docs/reports/B1_INTEGRATED_REPORT.md`
- `docs/contracts/b1/B1-T00_OWNER_APPROVAL_RECORD.md`
- `docs/contracts/b1/B1-T00_OWNER_DECISION_2.md`
- `docs/contracts/b1/B1-T00_BASELINE.sha256`
- `docs/reports/B1_TRACEABILITY.md`
- `Star/03_Issues/2026-10-24_B1_Open_Items.md`
- `Star/01_Phase_Reviews/Phase_03F_B1_Review.md`
- 최종 verify.ps1 실행 로그 및 B1 구현 이력

실제 파일 경로가 다르면 저장소에서 확인한 경로를 기록한다. 존재하지 않는 자료나 미확인 결과를 검증 완료로 간주하지 않는다.

기존 S00.5 §5\~§6의 TASK 의존성과 수용 기준도 확인한다.

## 4. TASK별 독립 검수

B1-T00\~T10 전체를 검수한다.

각 TASK별로 다음을 확인한다.

1. 승인 계약과 구현의 일치 여부
2. 필수 기능 누락 여부
3. 의존성과 아키텍처 경계 준수
4. 실제 테스트가 수용 기준을 검증하는지
5. 실패·변조 입력에 대한 Fail-Closed 동작
6. 기존 ResultHash·ConfigHash·Attestation·RNG·Replay 불변성
7. 테스트 미실행 항목이나 검증 공백
8. 해당 TASK를 PASS로 판정할 근거

단순히 테스트 개수가 많거나 verify.ps1이 PASS라는 이유만으로 계약 충족 판정을 내리지 않는다.

## 5. 중점 검수

다음 항목은 반드시 실제 코드와 검증 증거를 대조한다.

### A. Evidence Schema 및 Hash

- Strict JSON Reader
- 닫힌 4Schema
- JCS / U64 / F64
- 공식 문서 투영
- A+ Canonical Payload
- Artifact DAG 및 RAW_INVENTORY

### B. 경기 증거 및 Replay

- GameExecutor Observation Seam
- H0/H1/H2 및 RNG StreamUse
- Unit PZL과 GAME_EVENTS
- NSC-6 coverage 의미와 재계산
- 고정 20경기 PZL #6
- 실제 EventJsonV2Codec 연결
- N07 Witness 재평가
- N32 첫 Commit 전 실패 경기

### C. Registry 신뢰 경계

- Trust Anchor 및 승인 기록 Reader
- NSC-4 결정 1·2 반영
- Named Pipe Owner / DACL / AccessCheck
- Registry 계정과 역할 분리
- `get_registry_identity` 초기 요청
- 같은 연결에서 승인 기록 조회
- TRUST_CONFIG 사후 대조
- 승인·Seed·Build·Environment 결속
- Registry 멱등성과 충돌 검사
- 영수증 발급·등록의 원자성 및 실패 처리

### D. 테스트

- 최종 verify.ps1 GATE: PASS 증거
- 보고된 3,001 PASS / 0 FAIL / 0 SKIP의 실제 범위
- 독립 Golden 및 기존·신규 반례
- 실제 실행·변조·경계 시험의 적절성
- 누락되거나 미실행된 수용 시험

## 6. 미결 OWNER 해석 4건

통합 보고서 7절의 다음 항목을 각각 독립 검토한다.

1. `violations` 생성 단위
2. RAW_INVENTORY 파일 형식
3. `witness_digest` 앞 세 필드의 표기 방식
4. PZL #6 최종 상태 비교 방식

각 항목에 대해 다음을 판정한다.

- 기존 승인 계약으로 해석이 유일하게 결정되는가?
- 현재 구현이 기존 규범을 만족하는가?
- 규범 추가 또는 변경이 필요한가?
- 현재 구현을 그대로 유지할 수 있는가?
- 변경이 필요하다면 최소 수정 범위와 이유는 무엇인가?

각 항목에 대해 ACCEPTABLE_IMPLEMENTATION, OWNER_DECISION_REQUIRED 또는 CONTRACT_VIOLATION 중 하나로 분류하고 근거를 제시한다.

Codex는 OWNER 결정을 대리하지 않는다.

## 7. 운영 Gate와 구현 검수 분리

다음 항목의 상태를 독립적으로 확인한다.

- 보호 앵커 디렉터리 실제 설치·ACL 실측
- AT-1\~AT-14r
- AT-4(P-1) 정식 측정
- N27-d·e·f의 계정별 실측
- Environment Profile 운영 승인
- N12 운영 비트 Golden

실행하지 않은 시험은 PASS로 표시하지 않는다.

Win32 1307 사전 측정만으로 AT-4 전체 PASS를 선언하지 않는다.

운영 실측 미완료가 B1 코드 구현 완료, T10 완료, T11 판정 또는 Official Execution 준비도에 미치는 영향을 기존 계약의 정확한 조항에 따라 구분한다.

## 8. 최종 판정

다음 네 가지를 별도로 판정한다.

A. B1 구현 계약 준수

B. 자동화 테스트 및 Golden 검증 충분성

C. Windows 운영 보안 Gate 충족 여부

D. T11 최종 독립 검수 결과

T11 최종 판정은 다음 중 하나로 제시한다.

- PASS
- FIX_REQUIRED
- BLOCKED_BY_OWNER_DECISION
- INSUFFICIENT_EVIDENCE

PASS가 가능한 경우와 운영 Gate 때문에 여전히 불가능한 범위를 명확히 구별한다. 미결 OWNER 승인이나 미실시 필수 검증을 임의 PASS로 대체하지 않는다.

## 9. 결과 보고 형식

1. Executive Summary
2. 실제 확인한 HEAD·SHA·작업 트리
3. T00\~T10 작업별 독립 판정표
4. Findings — CRITICAL / MAJOR / MINOR / NOTE
5. 발견한 결함의 위치·근거·재현 방법
6. 미결 OWNER 해석 4건에 대한 판단
7. 미검증 운영 Gate 목록
8. T11 최종 판정
9. 필수 수정 사항 및 우선순위
10. OWNER 최종 결정 또는 추가 실측이 필요한 사항

문제를 발견하면 수정 코드를 직접 작성하지 말고, Claude Code가 처리할 수 있도록 정확한 수정 요구사항을 제시한다.

검수 후 파일·코드·Git 변경 없이 종료한다.

Official Execution = NOT_READY\
Final Execution Freeze = PENDING\
Phase 3 = INCOMPLETE

이번 검수는 B1-T11 범위에 한정하며 Phase 3 전체 완료를 승인하지 않는다.
