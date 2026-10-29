---
type: issue
date: 2026-10-24
phase: "3-F B-1"
status: 미해결
tags:
  - star-player
  - issue
---

# B-1 미해결 항목 (2026-10-24)

> 2026-10-24 21:49 OWNER 결정 2로 1·2·3절은 **결정됨**(구현·자체 시험 완료, [결정 기록 2](../../docs/contracts/b1/B1-T00_OWNER_DECISION_2.md)). 아래 원문은 기록으로 보존합니다. 남은 항목은 끝의 "3차 이후 남은 항목".

상세 근거: [B1 통합 보고서](../../docs/reports/B1_INTEGRATED_REPORT.md), [Traceability](../../docs/contracts/b1/B1_TRACEABILITY.md)

## 1. NSC-4 B안 실측 — 표준 Windows 경로에서 앵커 위치가 성립하지 않음 (OWNER 판단 필요)

- **Issue:** B안을 문자 그대로 적용하면 상위 디렉터리 검사에서 표준 경로가 모두 거부됩니다. 이 PC(표준 사용자, 비관리자) 실측: `C:\Windows\System32`는 `(A;OICIIO;GA;;;CO)`(CREATOR OWNER, 상속 전용) 때문에 거부, `C:\`는 `(A;;LC;;;AU)`(Authenticated Users에 하위 디렉터리 추가 권한)로 거부, 사용자 생성 파일은 소유자 위반.
- **Reason:** OWNER 결정은 "상속 ACE도 같은 규칙", "상위 디렉터리까지", "실패하면 완화하지 않고 보고"입니다. 그래서 완화하지 않았습니다.
- **Recommended Option:** 상속 전용(INHERIT_ONLY) ACE는 그 개체 자신에게 권한을 주지 않으므로 그 개체 판정에서 제외하고, 드라이브 루트는 전용 하위 디렉터리(TCB 소유, 상속 차단)로 대체해 검사 시작점을 "앵커 전용 디렉터리"로 정하는 규칙을 OWNER가 명시.
- **Alternative:** 현행 B안 유지 + 앵커를 TCB 전용으로 새로 구성한 전용 볼륨·경로에서만 운영(드라이브 루트 ACL까지 조정).
- **Impact:** 결정 전에는 T2 실측 PASS 불가, 정상 인증 운영 불가(Fail-Closed 유지).
- 상세 결정안(계약 차이·변경 파일·수용 시험·보안·회귀 영향): [OWNER 결정안 제안 1](../../docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS.md)

## 2. #6 JSON codec 경계 (OWNER 판단 필요)

- **Issue:** `ScriptedGameJson`(v2 Writer/Reader)이 `internal`이고, Infrastructure의 InternalsVisibleTo 목록·공개 형식 목록이 `TrustedBoundaryTests`로 고정되어 있어 검증기 바이너리가 실제 codec을 쓸 수 없습니다.
- **Recommended Option:** Infrastructure에 #6 전용 공개 진입점 하나를 두고 `TrustedBoundaryTests` 고정 목록을 그에 맞게 갱신(신뢰 경계 변경이므로 OWNER·Codex 확인).
- **Alternative:** `StarPlayer.EvidenceVerifier`를 Infrastructure friend 목록에 추가.
- **Impact:** 현재는 codec을 delegate로 주입하고 Infrastructure.Tests에서만 결합합니다. 운영 검증기 단독 실행에서는 #6이 `Evidence.pzl_incomplete`로 끝납니다.
- 상세 결정안: [OWNER 결정안 제안 2](../../docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS.md)

## 3. R03-4 7단계 Registry ID 조회 수단 (OWNER 판단 필요, BLOCKED)

- **Issue:** 7단계는 "같은 핸들로 `registry_instance_id` 조회"인데 닫힌 요청 형식(get·get_by_scope·register)에 조회 수단이 없습니다.
- **Reason:** 요청 형식 변경은 닫힌 규범 변경이라 임의로 하지 않았습니다.
- **Recommended Option:** 현행 구현(모든 응답에 Instance ID, 다른 필드보다 먼저 대조)을 해석으로 채택. 상세: [OWNER 결정안 제안 3](../../docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS.md)
- **Alternative:** 요청 형식에 `instance` op 추가
- **Impact:** 채택 전 T09는 BLOCKED 항목 1개를 가진 "부분"

## 4. 구현 해석 수용 (OWNER 판단 필요)

- 통합 보고서 7절 해석 1(violations 단위)·2(RAW_INVENTORY 형식)·3(witness BITS64)·6(#6 비교 방식)은 문서 바이트·운영 값·F-26 문구에 영향이 있어 OWNER 결정이 필요합니다. 나머지는 단순 구현 선택으로 분류했습니다.

## 5. 미실시·미생성 (기술 부채)

- AT-1~AT-14r, AT-4(P-1) 정식 측정(별도 서비스·검증·OWNER·Reader 계정 필요). 이번에는 한 계정에서 실제 Pipe 시험과 AT-4 사전 측정(Win32 1307)만 수행
- 환경 프로필 승인값·비트 Golden 36개·witness_digest 승인값(운영 값)
- 반례 "부분": N07(Witness 없는 세계), N27-d·e·f(별도 계정), N32(B 경우를 엔진이 만들지 않음)

## 해소됨 (2026-10-24 2차 보완)

- Registry Named Pipe 서버·클라이언트, Trust Bootstrap L2~L8, TRUST_CONFIG 사후 대조(N26), Test Profile TRX 실행·대조(A25), Phase 13 Check 생성, Calibration 끝-끝 검증·Golden — 구현·시험 완료(통합 보고서 1절)

## 3차 이후 남은 항목 (2026-10-24)

- **OWNER 결정 대기:** 구현 해석 1(violations 생성 단위)·2(RAW_INVENTORY 형식)·3(witness_digest 세 필드 표기)·6(#6 비교 방식 vs F-26) — 결정 기록 2 끝 표
- **미검증 운영 Gate:** 보호 앵커 디렉터리(`C:\ProgramData\StarPlayerTrust`) 생성·ACL 적용과 T2 실측, AT-1~AT-14r, AT-4 정식 측정 — 별도 계정·관리자 작업, 별도 허가 전 수행하지 않음
- **미생성:** 환경 프로필 승인값, 비트 Golden 36개, witness_digest 승인값(N12)
- **직접 시험 없음:** N27-d·e·f(별도 계정·서비스 배치 필요)

## T11 이후 남은 항목 (2026-10-25)

- ~~**TASK.md 기준 SHA 불일치**~~ — **해소(2026-10-25):** 확인된 원본에서 복원, D3 PASS. 원래 기록: 기준 목록 `ecbc0859…`(NSC-6 증거) ≠ 현재 `90ec63b9…`(T11 수정 지시). D3 FAIL의 유일한 원인. 상세: [수정 보고서 0절](../../docs/reports/B1_T11_FIX_REPORT.md)
- ~~**BLOCKED:** §21 GAME_RESULTS 필수화~~ — **해소(2026-10-25):** OWNER 결정 3 §5로 형식 승인·구현
- **구현 선택 확인 요청:** 실행 파일 배치 `run/<run_id>/…`, #6 JSON 20개를 GAME_RESULTS로, 영수증별 VERIFIER_DERIVED 파일
- OWNER 해석 4건(권장 결정문은 수정 보고서 5절), 운영 Gate는 변화 없음
- 잔여 결정 7항목 단일안: [B1_OWNER_DECISION_PROPOSALS_3.md](../../docs/contracts/b1/B1_OWNER_DECISION_PROPOSALS_3.md)

## OWNER 결정 3 이후 (2026-10-25)

- 결정안 7건 모두 결정·적용됨([결정 3](../../docs/contracts/b1/B1-T00_OWNER_DECISION_3.md), [보고서](../../docs/reports/B1_DECISION3_REPORT.md)). 남은 것은 Codex 표적 재검수, OWNER 최종 수용, 운영 Gate

## Codex FIX_REQUIRED 후속 (2026-10-30)

- **G1-POLICY-01 (MINOR):** 도구 수정(`c4a04bb`) — Codex 재검수 대기. PowerShell 7 실행 미검증. 정적 검사 한계(고정 파일 밖 조각 조립)는 종단 시험·코드 검수 몫. [후속 보고서](../../docs/reports/B1_G1G5_FIX_REPORT.md)
- **G5-TEMP-01 (OPEN_MAJOR):** 운영 보호 모델·시작 절차 설계(NOT_IMPLEMENTED)·M-12A~J. 운영 기계 실측 통과 전 공식 인증 금지. 운영 경로가 부족하면 코드 대안 C-3(별도 TASK). [보호 모델](../../docs/tasks/B1_G5_TEMP01_PROTECTION_MODEL.md)
