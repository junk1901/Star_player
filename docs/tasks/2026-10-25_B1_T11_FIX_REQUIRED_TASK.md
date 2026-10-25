# Star Player B1 — Codex T11 FIX_REQUIRED 수정 작업

## 작업 목적

Codex T11 통합 독립 검수 결과는 FIX_REQUIRED다.

- CRITICAL 0건
- MAJOR 5건
- MINOR 3건

기존 B1 구현을 유지하면서 Codex가 발견한 8개 결함을 수정한다.

이번 작업은 새로운 설계나 기능 확장이 아니라 승인된 B1 계약을 충족하기 위한 결함 수정이다.

구현 담당은 Claude Code, 독립 재검수 담당은 Codex다.

## 기준 문서

- `B1-T00_FINAL_CONTRACT_DRAFT.md`
- `docs/contracts/b1/B1-T00_OWNER_APPROVAL_RECORD.md`
- `docs/contracts/b1/B1-T00_OWNER_DECISION_2.md`
- `docs/reports/B1_INTEGRATED_REPORT.md`
- Codex B1-T11 FIX_REQUIRED 검수 보고서

승인 계약 SHA-256:
`1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64`

현재 Git HEAD:
`257403ddae557a54c4013c17705370caeac7a2e0`

먼저 실제 파일과 코드, 기존 테스트를 대조한다. Codex 보고서의 재현 경로는 정적 분석 결과이므로, 가능한 경우 실패하는 회귀 시험으로 입증한 후 수정한다.

## 수정 대상 8건

1. **B1-T11-01 — 인증 영수증 발급 결속**

   TrustBootstrap, 승인·환경 검증, Replay, PZL, Test Profile 및 적격성 판단을 실제 Production 검증 흐름으로 연결한다. 영수증은 검증된 결과에서만 구성하고, 호출자가 임의의 Receipt 또는 `recheck=null`로 인증 절차를 건너뛰지 못하게 한다. TEST_ONLY나 PZL FAIL 상태가 필드 조작으로 정상 인증으로 바뀌지 않도록 한다.

2. **B1-T11-02 — Phase 자식 영수증 결속**

   Calibration·Integration·Dominance의 정확한 세 자식을 확인한다. 역할, Validation Set, Approval Record, RunId, EvidenceId, Payload Hash, TEST_ONLY 및 인증 상태를 검증하고, Phase 발급 경계에서 반드시 실행한다. 동일 영수증 세 번 제출, 자식 혼합, 역할 교체, Hash 불일치를 거부하는 테스트를 추가한다.

3. **B1-T11-03 — Archive 전체 참조 및 Replay 스냅샷**

   Unit PZL과 PZL6의 모든 ArtifactRef를 검증한다. 필수 역할·개수·ID 중복·원본 SHA·파일 존재 여부를 확인하고, Replay·Attestation·영수증이 동일하게 검증된 원본 바이트를 사용하도록 한다. PZL6의 20경기 JSON bundle 보존도 시험한다.

4. **B1-T11-04 — Archive 경로 교체 경쟁**

   사전 경로 확인과 파일 사용 사이의 TOCTOU 위험을 제거한다. 기존 PinnedDirectory 및 핸들 기반 경로 검증 구조를 최대한 재사용한다. 신규 파일 생성, Snapshot 읽기, 영수증 재시도 읽기까지 동일한 경로 안전성 조건을 적용한다. Junction 및 부모 디렉터리 교체 반례를 추가한다.

5. **B1-T11-05 — Environment Profile 정확한 집합 검증**

   runtime_files는 정확히 `System.Private.CoreLib.dll`, `clrjit.dll`, `coreclr.dll`의 세 항목을 Ordinal 순서로 요구한다. upper_quantile은 정확히 divisor 1~36을 순서대로 요구한다. 중복·누락·재정렬을 모두 거부하고 실제 환경 비교에서도 우회되지 않도록 시험한다.

6. **B1-T11-06 — Reader 전용 Registry 클라이언트**

   기존 Pipe 인증 절차를 재사용하는 Read-Only 클라이언트를 구현한다. 허용 기능은 Get·GetByScope뿐이며 Register·인증·영수증 발급 API는 노출하지 않는다.

7. **B1-T11-07 — ImpersonateSelf 실패 정리**

   기존 계약대로 ImpersonateSelf 호출 여부를 기준으로 finally에서 RevertToSelf를 한 번 호출한다. 호출 결과에 따른 성공·실패 처리와 Revert 실패 시 FailFast 계약을 보존한다. 실제 호출 순서·횟수를 시험한다.

8. **B1-T11-08 — Pipe 인스턴스 생성 실패**

   n이 4보다 작은 상태에서 추가 인스턴스 생성이 3회 실패하면 오류 기록 후 정상 서비스 중지 절차를 수행한다. 인스턴스 포화와 생성 실패를 구분한다. 종료 시 FlushFileBuffers 교착, 스레드·핸들 수명 경쟁도 회귀 검증한다.

## 테스트 및 검증

각 Findings에 대해 가능한 한 Red → Green 테스트를 추가한다.

단순히 에러 반환 여부만 확인하지 말고 실제 인증·발급·등록 경로에서 해당 검사가 필수로 실행되는지 확인한다.

수정 후 전체 Release 빌드, 모든 테스트 프로젝트, tools/verify.ps1 및 기존 B1 Golden·반례를 실행한다.

기존 3,001 PASS는 기준 실행의 보고값일 뿐이며, 변경 후 실제 통과 수를 새로 기록한다.

기존 GameResultHash, RunResultHash, ConfigHash, Attestation Hash, RNG 소비와 Replay 순서를 변경하지 않는다.

## 미결 OWNER 해석

다음 네 항목은 이번 수정으로 임의 승인하지 않는다.

- violations 생성 단위
- RAW_INVENTORY 파일 형식
- witness_digest 앞 세 필드의 표현
- PZL #6 최종 상태 비교 방식

Codex가 제시한 판단을 반영해 각 항목별 권장 결정문과 현재 구현 영향만 정리한다.

기존 승인 계약과의 충돌 없이 해결 가능한 결함 수정은 계속 진행하되, 새 규범을 도입해야 하는 부분은 BLOCKED로 분리한다.

## 운영 Gate

별도 운영 계정과 Windows 보안 실측이 필요한 항목은 이번 코드 결함 수정과 분리한다.

AT-1~AT-14r, 정식 AT-4, N27-d/e/f, 운영 Environment Profile과 비트 Golden을 수행하지 않았다면 PASS로 기록하지 않는다.

공식 인증 운영을 활성화하지 않는다.

## 변경 제한

- 승인된 B1 기술 계약 원본 변경 금지
- 기존 OWNER 결정 기록 원본 변경 금지
- TASK.md 및 사용자 PDF 변경 금지
- Git Commit·Push 금지
- Official Execution 활성화 금지
- Final Execution Freeze 금지
- 미검증 시험을 PASS 처리하지 않음

## 완료 보고

Codex T11 Findings 8건 각각에 대한 수정 파일, 재현 시험, 수정 후 결과를 작성한다.

전체 테스트 결과, 기존 불변성 증거, 미결 OWNER 결정, 운영 Gate 상태 및 Git 상태를 함께 보고한다.

보고서는 Codex가 기존 T11 결과와 변경 Diff를 직접 대조할 수 있도록 구성한다.

수정 완료 후 작업을 멈춘다. Codex 재검수 PASS 및 OWNER 최종 수용 이전에는 B1 전체를 완료 처리하지 않는다.

Official Execution = NOT_READY
Final Execution Freeze = PENDING
Phase 3 = INCOMPLETE