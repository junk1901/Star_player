---
type: decision
date: "2026-10-13"
status: 소유자 승인 (설계만)
related_phase: "03"
tags:
  - star-player
  - decision
  - phase-3-c3
---

# 2026-10-13 — Phase 3-C3 설계 승인 (D-1~D-8, 계약 A~F)

> [!warning] 설계 승인 ≠ 구현 승인
> 프로젝트 소유자가 C3 설계 결정 D-1~D-8과 추가 계약 A~F를 일괄 승인했다. **C3-A / C3-B / C3-C 및 C3 프로덕션 구현은 `NOT STARTED / NOT AUTHORIZED`**이며, 각 하위 단계의 코드는 소유자의 별도 지시가 필요하다. Phase 3 Gate는 `INCOMPLETE`.

## 날짜 근거
승인 날짜 2026-10-13은 프로젝트 소유자의 채팅 승인에 근거한다. `TASK.md` 수정 시각은 승인 자체의 증거로 사용하지 않는다. 다른 날짜가 확인되면 정정한다.

## 출처와 구분
- 공식 계약 본문: [game-engine §27](../../docs/game-engine.md), [ADR-007 "Decisions (3-C3 design approval)"](../../docs/decisions/ADR-007.md), SYSTEM_CONTRACTS C08.
- **과거 분석 시점의 권고(채팅 보고서: C3 사전 설계 분석, 2026-10-13 읽기 전용 보충 검토)와 승인된 정책은 구분한다.** 둘이 다르면 승인된 정책이 우선하며, 과거 권고는 이력이다. 이 두 분석은 저장소 파일이 아니라 세션 보고서다.
- GPT Chat의 구조 긍정 평가와 보충 검토 요청은 소유자 지시를 통해 전달된 것이며 이 세션이 독립 확인하지 않았다. C3 설계에 대한 Codex 독립 검토는 `미실시`(자동 실행 안 함).

## 승인된 결정 요약
| ID | 결정 |
|---|---|
| D-1 | `AuthoritativeGame`이 마지막 확정 순번을 명시 보관, 확정·생성·복원 시 영수증 이력과 대조. `Count`만으로 연속성 증명 금지 |
| D-2 | 순번 건너뜀·재사용 금지, 새 ID = 마지막 확정 순번 + 1, 진단용 비정규 ID는 관문 밖 |
| D-3 | 비인플레이 투구·기본 인플레이·추가 진루 인플레이·Approach·RunnerPlay·Substitution 모두 공통 Stage/Publish. 생성기·PA 구동기는 만들지 않음. RNG 미소비 이벤트는 RNG 보존 |
| D-4 | PA 구동기·다음 행동 선택·새 후보 생성은 후속 3-F 하네스. ROADMAP과의 차이는 기록만 하고 수정하지 않음 |
| D-5 | 종료 이벤트는 같은 Stage에서 `Finalize`까지. 실패 시 Publish 없음. `Final == null`인 종료 상태 공개 금지. EventId·영수증·RNG 없음 |
| D-6 | 이미 확정한 후보 재제출은 성공 처리하지 않음(`already_committed` 또는 기존 최신성 계약). 오류 우선순위는 구현 계약에서 고정, `CheckPrepared` 코드·순서 불변 |
| D-7 | 현재 권위 상태로 `GameReducer.Apply` 재실행, 재실행 결과를 채택, 미리보기와 구조적 비교(영수증·이력, 득점·아웃·주자·카운트, 이닝·종료, `ScoredPlay`·`Sequence`, 타점·타수·잔루·공식 기록, Delta·Facts, 중첩 상태). `GameState ==` 불가. 불일치 시 거절 |
| D-8 | 권위 집합체·관문은 Simulation 내부. Application/Infrastructure 공개 시그니처에 숨김 타입 노출 금지. Repository·DB·UI API 추가 없음 |

추가 계약: A `AuthoritativeGame`(불변, `Revision` 단조 증가 long), B Stage/Publish·CAS(동시 준비 중 하나만 성공, 취소는 CAS 전까지만), C RNG 검증(구조·시드·컨텍스트·H0 스트림 보존·Draws 역행 금지·RawDraws 증분 일치; 해시 일치를 경로 증명으로 기록하지 않음), D `(PreparedPitch, NonInPlayPitchCandidate)` 쌍 + 잘못된 연결 거부, E `CommitRecord`(불변, 숨김 타입 비노출), F 영속화는 Phase 4 인수인계(CAS가 DB-메모리 원자성을 해결한다고 쓰지 않음).

## 상태
D-1~D-8 `OWNER APPROVED` · C3 설계 및 계약 `OWNER APPROVED` · C3-A/B/C `NOT STARTED / NOT AUTHORIZED` · C3 프로덕션 구현 `NOT STARTED / NOT AUTHORIZED` · Phase 3 Gate `INCOMPLETE`.

## 구현 전 남은 결정
- 최종 오류 코드와 우선순위(D-6), 개념 식별자의 확정
- 구조적 비교 항목의 테스트 픽스처 목록
- 비정규 ID가 섞인 복원 이력에서의 `LastEventSequence` 정합성 검사 방식
- CAS 보관 타입
- Phase 4가 영속할 `CommitRecord` 필드 목록
- ROADMAP 3-F 라벨 문구와 D-4의 정합(소유자 결정; 이번에는 수정하지 않음)

## C3-A 착수 전 결정 대기 항목 (정리만 함, 정책 미확정)
> [!note] 이 절은 소유자 결정이 필요한 쟁점의 정리이며 어떤 정책도 확정하지 않는다. 사실 관계는 HEAD `a8cb10c`의 코드를 읽은 것이고 실행·측정한 것이 아니다.

| # | 쟁점 | 확인된 사실 | 소유자 결정이 필요한 것 |
|---|---|---|---|
| 1 | 오류 코드와 우선순위 (D-6) | `CheckPrepared`는 `PitchCandidate.*` 코드(`event_id_in_use`, `event_duplicate`, `receipt_invalid`, `rng_state_changed`, `state_stale` 등 20여 개)를 이미 고정 순서로 사용한다. D-6은 이 코드·순서 불변을 요구한다. | 관문 고유 코드의 접두사·이름(개념 식별자 `already_committed` 등), 관문 검사와 `CheckPrepared`가 동시에 해당할 때의 승리 순서, 재제출이 `already_committed`와 기존 `state_stale`/`rng_state_changed` 중 어느 쪽으로 보고되는지 |
| 2 | 비정규 ID 처리 | 게이트는 `{GameId}:e{6+ 자리}` 정규 ID만 받고 진단용 비정규 ID(`e1` 등)는 거절한다. `GameDiagnosticsService`는 C3 밖이다. | 비정규 ID가 섞인 복원 이력에서 `LastEventSequence` 정합성을 검사할지 복원 자체를 거절할지, 거절 시 코드·메시지 |
| 3 | CAS 보관 타입 | 설계는 "기대 집합체 참조 CAS"만 정했고 보관 타입은 미정이다(`Interlocked.CompareExchange` 대상 필드 소유자, 단일 직렬 기록자와의 관계). | 보관 타입의 종류·수명·소유자, 취소 토큰 통과 지점의 코드화 |
| 4 | 구조적 비교 픽스처 | 비교 항목 목록은 승인됐으나 테스트 픽스처 목록은 없다. | 필수 픽스처 집합 |
| 5 | `CommitRecord` 영속 필드 | 설계는 Phase 4 인수인계로만 남겼다. | Phase 4가 저장할 필드 목록 |
| 6 | ROADMAP 3-F 라벨 (D-4) | ROADMAP 수정 금지 상태 유지. | 라벨 문구 조정 시점·방식 |

기타 관찰(해결 아님): `docs/game-engine.md` 27.3은 `Revision`이 "마지막 변경이 확정화뿐인 Publish"에도 +1이라고 쓰나 D-5는 종료 확정이 같은 Stage의 단일 Publish라고 한다. 두 문장이 같은 Publish를 가리키는 것으로 읽히지만 구현 계약에서 한 문장으로 맞추는 편이 안전하다. 또 27.6 (5)의 후보 `UnconfirmedStep` 미리보기 비교는 후보가 있는 제안(투구·인플레이)에만 적용되며 `Approach`/`RunnerPlay`/`Substitution`은 미리보기 없이 재실행 결과만 채택한다는 점도 구현 계약에서 명시할 것.
