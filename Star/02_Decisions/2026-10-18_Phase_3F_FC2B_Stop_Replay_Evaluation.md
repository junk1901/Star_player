---
type: decision
date: "2026-10-18"
status: Phase 3-F F-C2-B 구현·자체 시험 완료(IMPLEMENTED_WITH_NOTES, 검토 대기, 미커밋) — 공식 실행 NOT_READY, F-C2-C 미착수, Phase 3 전체 Gate INCOMPLETE
related_phase: "03"
tags:
  - star-player
  - decision
  - phase-3-f
---

# 2026-10-18 — Phase 3-F F-C2-B 정지/Replay·Cohort·층화 평가 구현 결정

권위 문서는 [ADR-008](../../docs/decisions/ADR-008.md)·[ADR-009](../../docs/decisions/ADR-009.md)(정책 변경 없음)과 [F-C2-B 보고서](../../docs/reports/PHASE_3F_FC2B_IMPLEMENTATION_REPORT.md)다. 이 노트는 ADR-009 Open 항목 등 구현 중 내린 지역 결정만 요약한다.

## 결정

- **단일 정지 규칙:** `StopRule.Decide`(정지 판단)와 `StopRule.Execute`(유일한 정지 인식 실행기). `SeedManifest.AfterGames`는 위임. 실행과 Replay가 같은 코드로 같은 신뢰 계획을 실행해 정지점을 재도출한다.
- **dry Replay 신뢰 출처(SR-D8.6):** 운영자 Pin(`replay --seed-manifest --seed-sha256`). `fc2-dry` Key에만 허용, 공식 Replay에는 거부, 매니페스트 자기 선언 Pin은 쓰지 않음.
- **결과 버전 필드(SR-D7.4):** `results.run_result_version`(없으면 v1). 요구 버전은 신뢰 계획의 family로 결정.
- **`run-result-v2`(SR-D7.3):** `run-result-v2` / config_hash / `plan|N|target` / `stop|…` / 경기 Hash / 집계. Golden Vector 고정.
- **라벨·코드(SR-D3.6):** `CANCELLED`(Exit 1), `ABORTED_INTEGRITY`(Exit 2), `Replay.stop_*`·`Replay.result_*` 등. 새 Exit Code 없음.
- **dry 목표 상한:** 1~99,999(공식 하한 미만).
- **무결성 중단:** 블록마다 Seed 파일 Pin 재검증(`dry`·dry Replay), 경기의 `config` 단계 Pin 실패도 중단.
- **Cohort 로테이션:** `CohortRotation`을 InputsHash에 결속(로테이션 없는 `neutral-v1` Hash 불변). 경기 층은 선발 손과 라인업 방향에서 결정.
- **평가 결합:** 층 Gate는 실패 경기 → FAIL, 소진·하한·최소 미달 → INSUFFICIENT_SAMPLE, 공식 지표 FAIL > 부족 > 미판정. 4층은 합산 없이 결합. Batch-means 20경기는 진단 파라미터.

## 남은 결정

- Final Execution Freeze: 블록 크기·상한, 공식 Seed·Pin, 코호트 InputsHash, 실행 정책, Git SHA, 빌드 결속 증명.
- 공식 실행 활성화와 공식 출력 쓰기(TOCTOU, Dirty 분류) — 별도 지시.
- 독립 리뷰의 평가 결합 규칙 해석 확인.
