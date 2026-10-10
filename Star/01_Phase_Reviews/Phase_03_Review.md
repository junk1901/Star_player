---
type: phase-review
phase: "03"
phase_name: "Seeded game simulation / Prototype 1 — 작업 패키지 3-A (RNG)"
status: 검토 대기
started: "2026-10-11"
completed: ""
developer_ai: Claude Code
reviewed_by: ""
branch: "main"
commit: "미커밋 (기준 커밋 b1e6b73ecf299147ab1b6b4dc2697ff463c66a1b 위의 작업 트리에만 존재)"
roadmap_anchor: "phase-3--seeded-game-simulation--prototype-1"
last_updated: "2026-10-11"
tags:
  - star-player
  - phase-review
---

# Phase 03 Review — Seeded game simulation / Prototype 1 (3-A RNG만 착수)

> [!warning] 상태 규칙
> 허용 상태는 `초안`, `검토 대기`, `승인 완료`, `보완 필요`입니다. 구현 AI는 외부 검토 없이 `승인 완료`로 변경하지 않습니다. 실행하지 않은 검증은 `미검증`, 수행하지 않은 독립 리뷰는 `미실시`로 기록합니다.

> [!success] 3-A 승인 (2026-10-11)
> 작업 패키지 3-A(RNG)는 **PASS WITH NOTES / GPT Chat 승인**, Codex 최종 집중 재검토 **PASS WITH NOTES**. 전체 xUnit 709 PASS(Claude 실행), Randomness 111 PASS(Codex 독립 재실행), `verify.ps1` 2회 `GATE: PASS`(Claude 실행, Codex 로그 확인). 마지막 비차단 노트(`GameRandomSnapshot.cs`의 오래된 주석)는 주석만 수정했고 RNG 동작·알고리즘·직렬화는 변경 없음. **Phase 3 전체 Gate는 INCOMPLETE, Phase 3-B 미착수.** 아래 `검토 대기`·`미승인`·`미실시` 표기는 승인 이전 라운드의 기록이며 3-A에 한해 이 블록이 최신이다. 승인 근거: GPT Chat 최종 판정(소유자 전달, `TASK.md`).

> [!important] 범위
> 소유자는 **Phase 3-A RNG 구현만** 승인했다(`TASK.md`). 3-B~3-F와 Phase 4는 미승인·미착수다. 이 문서는 **Phase 3 전체 Gate를 PASS로 기록하지 않는다.** Phase 3 Gate(100,000 PA, 100경기, 보정 구간, Pitch Zone Lite 검사 등)는 전부 `미검증`/INCOMPLETE다.

## 최종 보완 라운드 (2026-10-11, `TASK.md` — Codex 독립 리뷰 2회 통합 수정)

> [!note] 상태
> 리뷰 상태는 계속 **`검토 대기`**입니다(승인 아님). Phase 3 전체 Gate는 **INCOMPLETE**. 아래 본문(A~I)의 수치·서술 중 이 블록과 다른 것은 1차 구현 당시 기록이며, 이 블록과 [PHASE_3A_REPORT.md Part F](../../docs/reports/PHASE_3A_REPORT.md)가 최신입니다.

- **이전 수정 지시 반영 여부 확인:** 반영된 것이 없음을 먼저 확인(리뷰 이후 `Randomness/` 소스 무변경)하고 결함 전부를 수정 전 코드에서 재현했습니다. 중복 수정 없음.
- **A. 누락 스트림 복원:** 소비된 스트림을 지운 스냅샷이 구조 검증을 통과해 처음 값부터 조용히 재시작하는 문제 → `Restore(snapshot, expectedStateHash)` 계약(커밋 시 라이브 상태 해시와 복원 결과 대조, `Rng.state_hash_mismatch`). 해시는 손상 검출용이며 인증 수단이 아님. Phase 4에서 게임 상태·영수증과 같은 저장 트랜잭션으로 확정. `rngset1` 정규 형식·Golden Vector 불변.
- **B. M1:** 사용 전(0회 소비) 스트림은 스냅샷에서 제외 → 조회만으로 `StateHash` 불변(`game/location` 조회 vs 미접근 일치 테스트).
- **C. `Atomically`:** 진행 중 원본 잠금(원본·사전 획득 스트림 draw, `Stream()`, 중첩 범위 거부), 복사본 객체 비공유·범위 종료 후 폐쇄, `finally` 잠금 해제. 보장 범위는 메모리 RNG 상태뿐(사건 ID·리듀서·영수증·영속 커밋 제외) — 문서화.
- **D. Draws 한도:** `NextUInt32` 상태 변경 전 검사, `NextBounded` 롤백, `Advance` 사전 검증.
- **E. 기타:** 비정규 정렬·미사용 항목 거부, `InvariantCulture` 명시, 보고서 rejection 설명 정정, 단일 작성자 계약 명시.
- **근거:** RED 24개 중 16개 실패(수정 전) → GREEN. Randomness 111개 통과, 전체 **709개**(Domain 145 · Simulation 371 · Application 114 · Infrastructure 79) 통과, 신규 방어 로직 변형 18/18 검출. [RED/GREEN 증거](../../docs/reports/PHASE_3A_RED_EVIDENCE.md).
- **Phase 3-C/4 인계(기록만, 미구현):** 사건 ID 순번과 리듀서 결과의 공동 원자성, 게임 상태·영수증·RNG의 성공 단위, 영속 커밋 성공 후에만 메모리 발행, `game/location` 소비 순서·확률 척도, `ef2`/리플레이 — [ADR-004](../../docs/decisions/ADR-004.md) 참조.

## A. 기본 정보

| 항목 | 내용 |
|---|---|
| Phase | Phase 03 — Seeded game simulation / Prototype 1 (작업 패키지 3-A) |
| 작업 시작일 | 2026-10-11 |
| 작업 완료일 | 3-A 구현·자체 검증 2026-10-11 (Phase 3 전체는 미완료) |
| 개발 담당 AI | Claude Code |
| 리뷰 상태 | **검토 대기** (3-A 한정; 최종 보완 라운드 후에도 동일) |
| 관련 ROADMAP | [Phase 3](../../ROADMAP.md#phase-3--seeded-game-simulation--prototype-1) |
| 관련 Git 브랜치 | main |
| 관련 커밋 | 없음 (기준 `b1e6b73`; 커밋·푸시는 별도 허가 전까지 하지 않음) |

- 대시보드: [Star Player 개발 대시보드](../00_Dashboard.md)
- 공식 현재 상태: [CURRENT_STATE.md](../../CURRENT_STATE.md)
- 보고서: [PHASE_3A_REPORT.md](../../docs/reports/PHASE_3A_REPORT.md) · 검증 로그: [PHASE_3A_VERIFY_LOG.md](../../docs/reports/PHASE_3A_VERIFY_LOG.md) · 결정: [ADR-004](../../docs/decisions/ADR-004.md) · 계약: [game-engine.md §17](../../docs/game-engine.md)

## B. 개발 목표

### 개발 목적

이후 확률 해석기(3-B~3-F)가 사용할 버전 고정 난수 기반을 만든다: PCG32, 스트림 키 유도, 스트림 독립성, 스냅샷, 원자적 작업 범위, 결정적 ID. 확률·위치 추첨·이벤트 변경은 하지 않는다.

### 구현 범위

- PCG32 XSH-RR(`pcg32-xsh-rr-v1`), 편향 없는 bounded 정수(rejection sampling), 소비 횟수
- SHA-256 기반 `kd1` 스트림 키 유도, 스트림 `game/pitch`·`contact`·`field`·`runner`·`location`·`ai`(모두 `@1`), `game/location` 규약 확정
- `rngset1` 스냅샷 생성·복원(버전·상태 검증), `Atomically` 작업 범위
- 결정적 GameId·사건 순번 ID

### 완료 기준 (TASK.md §2~§6 기준; Phase 3 Gate 아님)

- [x] PCG32 Golden Vector(독립 참조) — 공식 공개 출력 + Python 독립 구현
- [x] 스트림 독립성·재현성
- [x] Snapshot 생성·복원, 잘못된 버전·상태 거부
- [x] Rejection sampling 경계·거부 경로, 잘못된 입력 거부
- [x] 결정적 ID·중복 방지, ID 생성 시 RNG 비소비
- [x] 원자적 작업의 실패·취소 보존
- [x] Phase 2 전체 테스트 회귀 (598개 변경 없이 통과)
- [ ] **Phase 3 Common Gate G / Phase 3 Acceptance Criteria — 해당 없음(미평가, INCOMPLETE)**

### 관련 요구사항 및 시스템 계약

- 시스템 계약: [SYSTEM_CONTRACTS.md C08](../../docs/SYSTEM_CONTRACTS.md), ROADMAP §4.1 · §6.1 · §1.14 (Pitch Zone Lite 스트림 규약)

## C. 실제 구현 내역

### 구현한 기능

PCG32, 스트림 ID/키 유도, `GameRandom`(스트림 집합·스냅샷·복원·`Atomically`), `GameIds`/`GameEventIds`. 상세는 보고서 §1.

### 주요 변경 파일

| 파일 | 변경 내용 |
|---|---|
| `src/StarPlayer.Simulation/Randomness/*.cs` (8개) | 신규: 난수 기반 구현 |
| `tests/StarPlayer.Simulation.Tests/Randomness/*.cs` (5개) | 신규: 71개 테스트 |
| `tests/Fixtures/generator/generate_rng_golden.py`, `tests/Fixtures/phase3a_rng_golden.json` | 신규: 독립 Python 참조와 Golden Vector |
| `docs/decisions/ADR-004.md` | 신규: RNG 결정 |
| `docs/game-engine.md` §17, `docs/SYSTEM_CONTRACTS.md` C08 | RNG 요약·상태 문장 추가 |
| `docs/reports/PHASE_3A_REPORT.md`, `PHASE_3A_VERIFY_LOG*.md` | 신규 보고·검증 로그 |
| `CURRENT_STATE.md`, `Star/00_Dashboard.md` | 3-A 실제 상태 반영 |

### 새 모듈과 기존 모듈 변경

- 새 모듈: `StarPlayer.Simulation.Randomness` 네임스페이스(새 프로젝트 없음)
- 수정한 기존 모듈: 없음 (기존 소스·테스트·픽스처 무수정). 추적 외 파일 `TASK.md`와 PDF 2개 보존.

### 계획 대비 달라진 내용

- 지시서가 인용한 "Codex Phase 3 RNG 설계 결정" 문서는 저장소에 없다. ROADMAP·SYSTEM_CONTRACTS 규약에서 설계를 도출했다(ADR-004 머리말, 보고서 §6-2).

## D. 테스트 및 검증

### 실행 환경

| 항목 | 값 |
|---|---|
| OS | Windows 11 Home 10.0.26200 |
| Godot | 4.7.2 Mono |
| .NET SDK | 8.0.425 |
| 빌드 구성 | Release(솔루션·verify), Debug(필터 실행) |

### 실제 실행 결과

| 명령 | 기대 결과 | 실제 결과 | 상태 | 로그·결과 파일 |
|---|---|---|---|---|
| `dotnet test tests\StarPlayer.Simulation.Tests --filter "FullyQualifiedName~Randomness"` | 신규 테스트 통과 | 71 통과 / 0 실패 | PASS | 콘솔 |
| `dotnet test StarPlayer.sln -c Release` | Phase 2 회귀 포함 전부 통과 | Domain 145 · Simulation 331 · Application 114 · Infrastructure 79 = 669 통과 / 0 실패 / 0 건너뜀 | PASS | 콘솔 |
| `tools\verify.ps1 -LogName PHASE_3A_VERIFY_LOG.md` | `GATE: PASS` | `GATE: PASS`, exit 0 (2분 40초) | PASS | [로그](../../docs/reports/PHASE_3A_VERIFY_LOG.md) |
| `python -I tests/Fixtures/generator/generate_rng_golden.py` | 공개 출력 자체검증 후 fixture 생성 | 통과 | PASS | `tests/Fixtures/phase3a_rng_golden.json` |
| 의도적 소스 변형 22건 | 전부 테스트 실패로 검출 | 22/22 검출 (보고서 §5) | PASS | 보고서 §5 |

### 완료 기준별 충족 여부

| 완료 기준 | 상태 | 근거 |
|---|---|---|
| Golden Vector(독립 참조 비교) | 충족 | 공식 공개 출력(42/54 6값·주사위 33개) + Python 독립 구현 벡터. 한계: kd1/rngset1은 같은 작성자 해석 |
| 스트림 독립성 | 충족 | 역순 생성·교차 draw·타 스트림 추가 draw 테스트 |
| Snapshot | 충족 | 복원 후 출력 일치, 버전·상태 위조 거부 코드별 테스트 |
| Rejection sampling | 충족 | 거부 경로 벡터 포함, bound 0 거부, bound 1 소비 1회 |
| 결정적 ID | 충족 | 순수·단사·단조 증가·재개·소진 실패 |
| 원자성 | 충족 | 실패 결과·예외·취소(전/후) 시 원본 불변 |
| Phase 2 회귀 | 충족 | 598개 변경 없이 통과 |
| Phase 3 전체 Gate | **해당 없음 / INCOMPLETE** | 3-B 이후 미착수 |

### 테스트하지 못한 항목

- Phase 3 Gate 전 항목(100,000 PA, 100경기, 보정, Pitch Zone Lite 검사, UI) — 미착수 (`미검증`)
- Godot UI 수동 확인·FHD — 3-A에 UI 변경 없음 (`미검증`)
- 업스트림 C 데모 직접 실행 — C 컴파일러 없음 (공개 출력 비교로 대체)
- 성능 측정 — 미실시

## E. 발견된 문제

### 발생한 오류

- 소스 스캔 테스트의 주석 제거 정규식이 첫 `//` 이후 파일 전체를 지움 → 변형 점검에서 발견, 수정 후 금지 패턴 변형 3건으로 재검증.
- 변형 점검 스크립트 동시 실행으로 첫 출력이 섞임 → 중단·해시로 복원 확인·순차 재실행.

### 해결한 문제

- 위 두 건.

### 미해결 문제

- 없음(결함). 아래 기술 부채·주의 사항 참조.

### 기술 부채 및 향후 영향

- `kd1`/`rngset1` 형식은 독립 검토 전(보고서 §6-1). 확률 척도(정수 분모)는 3-B에서 결정. `game/location` draw 수는 3-B/3-C에서 확정. 스냅샷 영속화는 Phase 4. `GameEventIds` 채택은 3-C.

## F. 설계 변경

| 변경 내용 | 변경 이유 | 영향 모듈 | 결정 문서 | Codex 추가 검토 |
|---|---|---|---|---|
| RNG 기반 신규 설계(승인 범위 내) | Phase 3-A 구현 | Simulation | [ADR-004](../../docs/decisions/ADR-004.md) | 미실시 (저장 데이터 무결성·핵심 시뮬레이션 규칙이므로 권장) |

## G. AI 리뷰

### GPT Chat 리뷰

| 항목 | 결과 |
|---|---|
| 기능명세 충족 여부 | 미실시 |
| 누락된 요구사항 | 미실시 |
| 개선 권장사항 | 미실시 |
| 최종 검토 의견 | 미실시 |

### Codex 리뷰 (선택적)

| 항목 | 결과 |
|---|---|
| 아키텍처 적합성 | 미실시 |
| 모듈 간 의존성 | 미실시 |
| 중요 로직 검증 | 미실시 |
| 구조적 위험 요소 | 미실시 |

## H. 다음 Phase 인수인계

### 시작 전 확인 사항

- 3-B는 소유자의 별도 승인 후에만 착수. `GameRandom` 사용 규칙은 [game-engine.md §17](../../docs/game-engine.md) 참조.

### 의존 관계

- 3-B(모델·튜닝 계약)는 ADR-004의 스트림·`game/location` 규약과 정수 확률 척도 결정에 의존한다.

### 주의할 문제 및 후속 작업

- 해석기는 `GameReducer.Apply`를 우회하지 않는다. 실패 가능한 단계는 `Atomically` 안에서 리듀서 호출과 함께 실행. Phase 2 후속 NOTES(C33·C38·C42·C44·D2·Phase 4 영속화)는 그대로 이월.

### 추가 검증 필요 사항

- `kd1`/`rngset1` 독립 검토(Codex 권장), 3-F의 100,000 PA·100경기·보정.

## I. 최종 결과

| 항목 | 결과 |
|---|---|
| 완료 기준 충족 여부 | 3-A 요구사항 충족(자체 검증). Phase 3 전체 Gate는 미평가 |
| 최종 리뷰 상태 | 검토 대기 |
| 남은 문제 | 기술 부채 목록(위 E) |
| 승인자 또는 승인 근거 | 3-A 한정: GPT Chat 최종 판정 PASS WITH NOTES (2026-10-11, 소유자 전달); Phase 3 전체는 미승인 |
| 최종 수정 날짜 | 2026-10-11 |

### Phase 종료 체크

- [x] ROADMAP 완료 기준과 Common Gate G 확인 (3-A 범위 외 항목은 INCOMPLETE로 명시)
- [x] 실제 구현·변경 파일과 Git 변경사항 기록
- [x] 실행한 테스트의 명령·환경·실제 결과 기록
- [x] 미검증 항목, 미해결 문제와 설계 변경 식별
- [x] 중요한 설계 변경의 결정 문서 연결 (ADR-004)
- [x] 리뷰 상태를 `검토 대기`로 변경
- [x] [CURRENT_STATE.md](../../CURRENT_STATE.md)와 실제 상태 일치 확인
- [x] GPT Chat 전달용 간략 요약 작성
- [x] 다음 Phase를 자동 시작하지 않음
