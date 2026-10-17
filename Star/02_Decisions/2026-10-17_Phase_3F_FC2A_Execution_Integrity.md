---
type: decision
date: "2026-10-17"
status: Phase 3-F F-C2-A 실행 무결성 구현·자체 시험 완료(IMPLEMENTED_WITH_NOTES, 검토 대기, 미커밋) — 공식 실행 NOT_READY, F-C2-B/C·F-C3 미착수, Phase 3 전체 Gate INCOMPLETE
related_phase: "03"
tags:
  - star-player
  - decision
  - phase-3-f
---

# 2026-10-17 — Phase 3-F F-C2-A 실행 무결성 구현 결정

권위 문서는 [ADR-008](../../docs/decisions/ADR-008.md)(정책 변경 없음)과 [F-C2-A 보고서](../../docs/reports/PHASE_3F_FC2A_IMPLEMENTATION_REPORT.md)다. 이 노트는 구현 중 내린 지역 결정만 요약한다.

## 결정

- **명령 체계:** 기본안 `official`을 별도 명령으로 채택(`run`·`replay` 유지). 공식 입력(Seed·코호트·정책·Rules)은 고정 Seed Manifest와 공식 실행 프로파일에서만 오며, 명령줄의 `--tuning`·`--seeds`·`--rules`·`--advancement`·`--event-budget`은 `Cli.option_forbidden`.
- **종료 코드:** 새 코드를 추가하지 않고 공식 거부도 Exit 2. 구분은 결과 문서 `error_code`·`checks`. 통계 Gate 상태(`NOT_EVALUATED` 등)는 종료 코드와 분리.
- **빌드 산출물:** 프로젝트 `bin/`·`obj/` ignored 파일은 예외 후보지만, 소스 SHA 결속 증명이 없으므로 `BuildOutputUnverified` = NOT_READY(경로만으로 신뢰하지 않음).
- **Seed 중복:** 같은 Seed 값의 반복은 허용(다른 GameId = 다른 RNG 경로, FC-D4.6). 유일성은 Index·GameId·Competition Key에 적용.
- **Hash 분리:** 기존 `RunConfig.ConfigHash`·결과 해시는 그대로 두고, 실행 의미 필드는 새 `simharness-official-attestation-v1`에 결속(ConfigHash 포함, 21개 필드 전부 해시).
- **Registry:** ADR-008이 이름만 정한 코호트(`neutral-opp-v1`, `neutral-mixed-v1`, `virtual-league-v1`)는 `Registry.cohort_not_implemented`로 선언만 하고 대체 입력을 두지 않음.

## 열린 항목 (소유자 결정 필요 시점: 실제 공식 실행 구현 단계)

- 공식 출력이 ignored `build/simharness/official/`에 남으면 다음 공식 실행의 Git 검사가 Dirty가 된다 — 실행 출력의 허용 범위.
- `fc3-int`의 공식 코호트가 ADR-008에 없다(제안서 후보 `virtual-league-v1`) — 현재 NOT_READY.
