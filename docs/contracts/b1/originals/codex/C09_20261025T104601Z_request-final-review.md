# Star Player — B1-T11 최종 READ-ONLY 독립 재검수

## 목적

Claude Code가 마지막 잔여 Finding인 R2-B MINOR를 수정했다.

이전 Codex 검수 결과:

- R1: RESOLVED
- R2-A: RESOLVED
- R2-B: NOT_RESOLVED — Preflight 결정적 필드 값 비교 누락
- R3: RESOLVED
- R4: RESOLVED

이번 검수에서는 R2-B의 실제 해결 여부를 독립적으로 판정하고, 기존 해결 항목의 회귀 여부와 B1 코드 구현 최종 수용 가능성을 평가한다.

Claude Code의 수정 완료 보고나 verify.ps1 PASS만으로 결함 해결을 인정하지 않는다.

## 1. 검수 기준 자료

다음 파일을 실제로 읽는다.

- docs/tasks/B1_T11_R2B_VALUE_BINDING_TASK.md
- docs/reports/B1_T11_R2B_VALUE_BINDING_REPORT.md
- docs/reports/B1_T11_R2B_VALUE_VERIFY_LOG.md
- B1_T11_R2B_VALUE_RED.log (실제 위치 확인)
- docs/reports/B1_T11_R2_FINAL_FIX_REPORT.md
- 승인 B1-T00 기술 계약
- OWNER 승인 기록과 결정 1\~3
- 현재 Production, SimHarness, EvidenceVerifier 코드
- R2-B 관련 신규 및 기존 테스트
- tools/b1-drypilot-repro.ps1

승인 계약 SHA:\
1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64

Git HEAD 기준:\
257403ddae557a54c4013c17705370caeac7a2e0

보호 대상 루트 TASK.md SHA:\
ecbc0859c809c68151c93f92058c31a7ab0b84ce8d8cc9a4a25512b7dd06f825

미추적 신규 구현 및 시험 파일도 빠짐없이 검토한다.

## 2. R2-B 결함 해결 검수

기존 결함:

PreflightRecords.Verify에서 필드 이름과 순서는 비교했지만 일부 결정적 값은 비교하지 않았다.

정상 PREFLIGHT의 다음 필드를 변경하고 Raw SHA 및 참조까지 일관되게 갱신해도 영수증이 발급될 수 있었다.

- preflight.games_per_cell
- schema_status
- official_evidence
- complete
- family_gate
- dominance_gate

수정된 RequiredOriginals.cs와 PreflightRecords 경로를 직접 추적한다.

다음 사항을 검증한다.

1. 기대 Preflight는 제출 문서가 아닌 신뢰된 승인 입력과 독립 재실행 결과로 생성되는가?
2. 재실행에서 준비 상태로 전환하는 세 검사(execution_path, unit_preflight/cell_preflight, attestation)가 정확한 범위로 제한되는가?
3. Attestation은 승인 입력으로 재생성되며 실제 Unit Manifest와 일치하는가?
4. 기존 형식 Writer가 생성한 기대 기록과 제출 기록을 전체 JSON 트리로 비교하는가?
5. 필드 이름, 순서, 타입, 값, 중첩 객체·배열을 모두 검사하는가?
6. 누락·추가·중복 멤버를 거부하는가?
7. 결정적 문자열·숫자·boolean의 타입 차이도 거부하는가?
8. Raw SHA와 byte_length가 정상적으로 갱신돼도 내용 불일치를 거부하는가?
9. 기존 Fail-Closed 정책을 유지하는가?
10. 정상 PREFLIGHT는 인증 가능한가?

특히 Preflight.record_differs로 거부하는 검사가 실제 Certification 발급 경계까지 적용되는지 확인한다.

## 3. 비교 제외 정책

현재 유일한 값 비교 제외 항목은 output_directory 검사 메시지라고 보고됐다.

다음을 독립 확인한다.

- output_directory의 id, status, code는 계속 비교하는가?
- 제외되는 것은 기계별 경로가 들어간 message 값뿐인가?
- 제외 규칙이 다른 검사로 확대될 가능성은 없는가?
- 해당 message가 인증 판정이나 후속 실행 입력으로 사용되지 않는가?
- 이전에 제외됐던 cli, git_integrity, git_sha, execution_path, attestation 값이 실제로 비교되는가?

불필요한 필드 비교 예외가 남아 있으면 Finding으로 기록한다.

## 4. RED/GREEN 시험 증거

Claude 보고:

수정 전 실제 실패 재현:

- schema_status 변조 후 영수증 발급 성공
- Dominance games_per_cell 2→3 변조 후 영수증 발급 성공

수정 후 거부:

- schema_status
- official_evidence 값 및 타입
- complete 값 및 타입
- family_gate
- games_per_cell 값 및 타입
- dominance_gate
- 중첩 결정적 필드
- 멤버 추가·제거

이 시험의 실제 내용을 확인한다.

수정 전 실행한 반례와 수정 후에만 실행한 반례를 구분한다.

RED 로그가 증명하지 않는 시험을 소급해 RED 성공으로 처리하지 않는다.

공유 상수를 생산자와 검증기가 함께 사용하는 방식이 잘못된 기대값을 동시에 생성해 결함을 가리는지 검토한다. 승인 계약과 기존 공식 형식을 독립 기준으로 확인한다.

## 5. 정상 Certification 경로

다음 양성 경로를 확인한다.

- Calibration Receipt 발급
- Integration Receipt 발급
- Dominance Receipt 발급
- Phase Receipt 발급
- INPUT_BUNDLE 4개 보존
- PREFLIGHT 57개 보존
- RAW_INVENTORY와 Receipt SourceFiles 결속
- Regression Proof 결속
- 기존 Seed 991000 유지

Preflight의 NOT_STARTED와 Official.execution_not_implemented 값이 최종 실행 성공 상태로 잘못 승격되지 않았는지도 확인한다.

## 6. 기존 Finding 회귀 확인

이미 해결된 다음 항목은 재설계가 아니라 회귀 여부만 확인한다.

- R1 Frozen Snapshot
- R2-A 실제 소비 입력 완전성
- R3 Phase Artifact 원본 합집합
- R4 PinnedDirectory 경로 안전성

T11-01\~08의 기존 해결 판정을 침해하는 새로운 변경이 있는지도 확인한다.

## 7. 제출 테스트 및 불변성

최종 Claude 자체 검증 결과:

- tools/verify.ps1: GATE PASS
- 3,041 PASS / 0 FAIL / 0 SKIP
- Golden 4개 PASS
- Dry Pilot 9 Run: ALL_9_IDENTICAL=True
- D3: 5개 SHA 일치
- 문서 링크: 197개 파일, 깨진 링크 0개

원시 로그, 빌드 정보, 결과 파일과 대조한다.

게임 계산식, Hash, RNG 소비, 기본 Replay 순서가 변경되지 않았는지 검토한다.

직접 실행하지 않은 시험은 Codex 직접 PASS로 표시하지 않는다.

## 8. OWNER 출처 및 운영 Gate

다음은 B1 코드 구현 검수와 별도로 판단한다.

- OWNER 결정 3 원문 확인
- R2 OWNER 지시 원문 확인
- B-4 공식 Preflight 출력 경로
- 운영 빌드 입력의 CRLF/LF 바이트 정책
- 운영 Build Provenance
- 검증기 바이너리 신뢰 경계
- 보호 앵커 디렉터리 설치 및 ACL
- AT-1\~AT-14r
- 정식 AT-4/P-1
- N27-d/e/f
- 승인 Environment Profile
- N12 비트 Golden 및 witness_digest

원본 채팅이 저장소에 없다면 확인 불가로 표시하고, 승인 출처가 확인됐다고 주장하지 않는다.

운영 Gate 미실시를 코드 구현 결함과 혼동하지 않는다.

## 9. 수행 제한

READ-ONLY 검수.

- 코드 수정 금지
- 문서·TASK 수정 금지
- Git Stage·Commit·Push 금지
- 승인 계약 변경 금지
- 빌드·테스트·Dry Pilot을 임의 실행하지 않음
- 미실행 시험 PASS 주장 금지
- Official Execution 활성화 금지

## 10. 최종 판정

다음을 개별 판정한다.

1. R2-B: RESOLVED / NOT_RESOLVED / INSUFFICIENT_EVIDENCE
2. R1·R2-A·R3·R4 회귀 여부
3. PREFLIGHT 계약 준수 여부
4. T10 전체 코드 수용 기준 충족 여부
5. B1 코드 구현 최종 수용 가능 여부
6. 새로운 MAJOR·MINOR Finding
7. OWNER 추가 확인 사항
8. Windows 운영 Gate 잔여 사항

T11 최종 판정:\
PASS / FIX_REQUIRED / BLOCKED / INSUFFICIENT_EVIDENCE

Finding이 모두 해결됐으면 코드 검수 PASS를 명시하되, 그것이 OWNER 최종 승인이나 공식 운영 실행 허가는 아님을 명확히 한다.

Official Execution = NOT_READY\
Final Execution Freeze = PENDING\
Phase 3 = INCOMPLETE

파일 변경 없이 검수 결과만 보고한다.
