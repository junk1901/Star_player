---
type: architecture-decision
status: 승인 완료
date: 2026-10-10
scope: planning-only
related_phases: [9, 11, 14, 15, 16, 17, 23, 24, 27, 29]
---

# Club Finance Lite 승인 및 Scope Lock 재활성화

## 결정

소유자는 기존 Scope Lock의 단일 제한적 예외로 Club Finance Lite를 승인했다. 범위는 구단 급여 집계, 버전형 KBO 샐러리캡 평가, 성과형 구단 스폰서 수익, 규칙 기반 Club AI의 계약·FA·트레이드 판단, 읽기 전용 재정/이력 화면이다.

## 경계

- C13 계약과 지급 의무가 선수 연봉의 유일한 권위다.
- C48은 급여를 파생 집계하며 구단 예산, 스폰서 계약/정산, 역사적 샐러리캡 평가를 소유한다.
- 총연봉, 샐러리캡 산정액, 구단 선수 예산은 서로 다른 값이다.
- Phase 11은 확정된 야구 성과 사실만 기록하고 Phase 14 재정 기능에 의존하지 않는다.
- 구단 스폰서는 Phase 22 개인 선수 스폰서와 분리한다.
- 사용자에게 GM·구단주 권한이나 재정 조작 UI를 제공하지 않는다.
- 공식 근거가 없는 실제 KBO 금액·절차는 검증된 사실처럼 작성하지 않는다.

## 결과

Phase 0–29의 번호·순서·직접 의존성은 유지한다. D1–D6은 변경하지 않는다. 이 예외를 반영한 뒤 Scope Lock은 다시 활성화된다. 이번 결정은 문서 개정만 승인하며 구현 Phase 시작을 승인하지 않는다.

## 공식 문서

- [ROADMAP §1.11](../../outputs/ROADMAP.md#111-club-finance-lite-approved-exception-and-renewed-scope-lock)
- [C48 System Contract](../../outputs/docs/SYSTEM_CONTRACTS.md#c48--club-finance-lite-phases-9-11-1417-2324-27-29)
- [Requirements R101–R107](../../outputs/docs/REQUIREMENTS_TRACEABILITY.md#newly-approved-revision-coverage)
