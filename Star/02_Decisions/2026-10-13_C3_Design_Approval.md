---
type: decision
date: "2026-10-13"
status: 소유자 승인 (설계 D-1~D-8·계약 A~F + C3-A 세부 계약 O-1~O-9 + C3-B 세부 계약 B-01~B-07, 문서만 — C3-B 구현 승인 아님)
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
| 1 | 오류 코드와 우선순위 (D-6) | `CheckPrepared`는 `PitchCandidate.*` 코드(`event_id_in_use`, `event_duplicate`, `receipt_invalid`, `rng_state_changed`, `state_stale` 등 20여 개)를 이미 고정 순서로 사용한다. D-6은 이 코드·순서 불변을 요구한다. **[정정 2026-10-13, 원문 보존: `event_id_in_use`·`event_duplicate`·`receipt_invalid`(및 `pitch_count_invalid`)는 `CheckPrepared`가 아니라 각 후보 빌더(`BuildNonInPlayCandidate`, `InPlayPitchPlanning.Build`, `AdvancementPlanning.Resolve`)가 검사한다. `rng_state_changed`·`state_stale` 등 나머지가 `CheckPrepared` 소속이다. 위 "20여 개"는 `PitchPrepare.*` 보조 코드를 포함한 근사치다.]** | 관문 고유 코드의 접두사·이름(개념 식별자 `already_committed` 등), 관문 검사와 `CheckPrepared`가 동시에 해당할 때의 승리 순서, 재제출이 `already_committed`와 기존 `state_stale`/`rng_state_changed` 중 어느 쪽으로 보고되는지 |
| 2 | 비정규 ID 처리 | 게이트는 `{GameId}:e{6+ 자리}` 정규 ID만 받고 *(정정: 순번은 정확히 6자리, 최대 999999)* 진단용 비정규 ID(`e1` 등)는 거절한다. `GameDiagnosticsService`는 C3 밖이다. | 비정규 ID가 섞인 복원 이력에서 `LastEventSequence` 정합성을 검사할지 복원 자체를 거절할지, 거절 시 코드·메시지 |
| 3 | CAS 보관 타입 | 설계는 "기대 집합체 참조 CAS"만 정했고 보관 타입은 미정이다(`Interlocked.CompareExchange` 대상 필드 소유자, 단일 직렬 기록자와의 관계). | 보관 타입의 종류·수명·소유자, 취소 토큰 통과 지점의 코드화 |
| 4 | 구조적 비교 픽스처 | 비교 항목 목록은 승인됐으나 테스트 픽스처 목록은 없다. | 필수 픽스처 집합 |
| 5 | `CommitRecord` 영속 필드 | 설계는 Phase 4 인수인계로만 남겼다. | Phase 4가 저장할 필드 목록 |
| 6 | ROADMAP 3-F 라벨 (D-4) | ROADMAP 수정 금지 상태 유지. | 라벨 문구 조정 시점·방식 |

기타 관찰(해결 아님): `docs/game-engine.md` 27.3은 `Revision`이 "마지막 변경이 확정화뿐인 Publish"에도 +1이라고 쓰나 D-5는 종료 확정이 같은 Stage의 단일 Publish라고 한다. 두 문장이 같은 Publish를 가리키는 것으로 읽히지만 구현 계약에서 한 문장으로 맞추는 편이 안전하다. 또 27.6 (5)의 후보 `UnconfirmedStep` 미리보기 비교는 후보가 있는 제안(투구·인플레이)에만 적용되며 `Approach`/`RunnerPlay`/`Substitution`은 미리보기 없이 재실행 결과만 채택한다는 점도 구현 계약에서 명시할 것. *(위 두 문장은 아래 C3-A 세부 계약 절에서 해소: Revision은 Apply+Finalize 하나의 Publish에서 +1, 미리보기가 없는 세 이벤트는 재실행 결과를 직접 채택.)*

## C3-A 세부 계약 O-1~O-9 승인 (2026-10-13, `TASK.md` "Phase 3-C3-A 세부 계약 최종 승인 반영", 기준 HEAD `5328917` — 문서만)

> [!warning] 계약 승인 ≠ 구현 승인
> 소유자가 O-1~O-9를 일괄 승인했다(날짜는 소유자의 채팅 승인에 근거; `TASK.md` 수정 시각은 증거가 아님). **C3-A 프로덕션, C3-B, C3-C는 `NOT STARTED / NOT AUTHORIZED`, Phase 3 Gate `INCOMPLETE`.** D-1~D-8과 계약 A~F의 의미는 바뀌지 않았다. 직전의 읽기 전용 계약 점검은 저장소 파일이 아닌 세션 보고서이며(실행 없이 코드를 읽은 결과), 권고와 승인 내용이 다르면 승인 내용이 우선하고 권고는 이력이다. Codex 독립 검토 `미실시`.

상세 계약은 [game-engine §27.16](../../docs/game-engine.md)이 기준이며, 아래는 결정과 이유만 적는다. 근거: [ADR-007 "Decisions (3-C3-A detailed contract approval)"](../../docs/decisions/ADR-007.md).

| ID | 결정 | 이유 · 기존 설계와의 관계 |
|---|---|---|
| O-1 | 오류 코드 `Commit.*`(Stage·Publish 구분, Publish는 `staged_missing`·`revision_stale`·`cas_lost`)와 `Authoritative.*`(생성·복원). `Id.*`·`Rng.*`·`Game.*`는 통과하고 대체하지 않음 | D-6이 남긴 "최종 코드 이름"을 확정. `event_id_in_use` 등 4개 코드는 후보 빌더 소속(위 정정) |
| O-2 | 검증 순서 대안 C: 취소 → 입력 → 종류 → 집합체 정합 → ID 형식·경기 → `already_committed` → 순번 건너뜀 → [C3-B: 후보 연결·`CheckPrepared`] → `event_id_in_use` → `Apply`·영수증 → [C3-B: 미리보기·RNG] → `Finalize` → Revision·결과 | 재제출은 우연한 최신성 코드가 아니라 `already_committed`로, 오래된 후보는 기존 구체 코드로 보고. `Apply`가 종료 경기의 같은 ID를 성공(중복)으로 답하므로 5번이 9번보다 앞서야 함. `CheckPrepared` 코드·순서 불변 |
| O-3 | 취소는 `OperationCanceledException`, 새 코드 없음, CAS 직전까지만; CAS 성공 뒤에는 검사·예외 전파 콜백 금지 | 기존 3-C2A~C2B-3 관례. 확정된 변경을 실패로 보고하지 않음(계약 B) |
| O-4 | C3-A 허용: `ApproachEvent`·`RunnerPlayEvent`·`SubstitutionEvent`. `PitchEvent`·`TakeEvent`·`InPlayEvent`는 `Commit.event_kind_not_supported` | RNG 증거 없이 투구만 확정하는 우회 차단. D-3(공통 관문)은 유지, 투구는 C3-B에서 같은 관문으로 들어옴 |
| O-5 | `Revision`은 EventId 순번과 독립, 복원에서 동일성 비강제, Publish마다 정확히 +1, `long.MaxValue`에서 거부. 종료 이벤트의 Apply+Finalize는 하나의 Publish이며 Finalize 전용 Publish 없음 | 27.3의 "Finalize만 바뀐 Publish도 +1" 문구를 D-5와 맞춤(27.3에 대체 표시, 이력 보존) |
| O-6 | `StartNew`(빈 이력·0·0·null)와 `Restore` 검사 목록, 순번 정확히 6자리·최대 999999, 비정규·손상·불완전 종료(`Final == null`) 복원 거부, 복원 `LastCommit` = null | 자동 재번호·삭제·묵시 복구 금지. 영수증에 본문이 없어 ID·소속·연속성·형식만 검증 가능하고 지문↔본문 일치는 검증 불가임을 명시 |
| O-7 | `public sealed` + `internal` 생성자, `HiddenTendencyBoundaryTests`에 고정 | 기존 `PreparedPitch` 관례. 숨김 타입 비노출(D-8) |
| O-8 | 복원 시 `ScoredPlay.EventId`·`SubstitutionRecord.EventId`만 영수증과 대조. `StatEntry.SourceId`·`ScoredPlay.Id`는 EventId로 가정하지 않음 | 점검 보고서 권고(Stats 포함)를 코드 확인 후 좁힘: `SourceId`는 이벤트 ID라고 선언된 필드가 아님 |
| O-9 | 최소 불변 `CommitRecord`(이벤트·영수증·순번·Revision 전후·RNG 해시 전후·Facts·Finalized), CAS 전에 구성해 `LastCommit`과 동일 인스턴스, 성공 후 반환 | `GameStatDelta`·Phase 4 영속 필드는 후속 계약 |

**상태.** O-1~O-9 `OWNER APPROVED` · C3-A 세부 계약 `OWNER APPROVED` (문서) · D-1~D-8 `OWNER APPROVED` (불변) · C3-A 프로덕션 `NOT STARTED / NOT AUTHORIZED` · C3-B `NOT STARTED / NOT AUTHORIZED` · C3-C `NOT STARTED / NOT AUTHORIZED` · Phase 3 Gate `INCOMPLETE`.

**아직 열려 있는 항목(여기서 결정하지 않음).** 구조적 비교 픽스처 목록(C3-B), `CommitRecord`·`GameStatDelta`의 Phase 4 영속 필드, ROADMAP 3-F 라벨과 D-4의 정합(소유자). 위 "구현 전 남은 결정" 중 오류 코드·우선순위, 비정규 ID가 섞인 복원 이력 검사, CAS 보관 타입은 C3-A 범위에서 이 절로 해소됐다(원문은 이력으로 보존). *(2026-10-13 후속: 구조적 비교 픽스처 목록은 아래 C3-B 절 B-03·B-06으로 해소. 이 문단의 C3-A 상태 표기는 그 시점 기록이며, C3-A 프로덕션은 이후 구현·GPT Chat 기술 검토 `PASS WITH NOTES`·로컬 커밋 `18ed60e`.)*

## C3-B 세부 계약 B-01~B-07 승인 (2026-10-13, `TASK.md` "Phase 3-C3-B 설계 승인 문서 반영", 기준 HEAD `18ed60e` — 문서만)

> [!warning] 계약 승인 ≠ 구현 승인
> 소유자가 B-01~B-07을 일괄 승인했다(날짜는 소유자의 채팅 승인에 근거; `TASK.md` 수정 시각은 증거가 아님). **C3-B 프로덕션·C3-C는 `NOT STARTED / NOT AUTHORIZED`, Phase 3 Gate `INCOMPLETE`.** D-1~D-8·A~F·O-1~O-9와 C3-A 코드·경계 테스트는 바뀌지 않는다. 직전 읽기 전용 구현 전 분석은 세션 보고서(실행 없이 HEAD `18ed60e` 코드를 읽은 결과)이며, 권고와 승인 내용이 다르면 승인 내용이 우선한다. Codex 독립 검토 `미실시`. GitHub Push는 Phase 3 최종 Gate 후 소유자의 별도 승인으로만.

상세 계약은 [game-engine §27.18](../../docs/game-engine.md)이 기준이며, 아래는 결정과 이유만 적는다. 근거: [ADR-007 "Decisions (3-C3-B detailed contract approval)"](../../docs/decisions/ADR-007.md).

| ID | 결정 | 이유 · 기존 계약과의 관계 |
|---|---|---|
| B-01 | 비인플레이 `(PreparedPitch, NonInPlayPitchCandidate)` 쌍(타입 변경 없음), 인플레이 `Plan.Prepared`. 비인플레이·기본 인플레이는 후보 `NextRng`가 `Draft.NextRng`와 **같은 인스턴스**, 해결 인플레이는 감사(H0/H1/H2 해시·`game/runner@1`·결정·맥락 수·논리/원시 추첨 수·이벤트 일치·해결 여부)로 연결. 기록된 사실로 결정적 재구성은 검토, 원 추첨 재실행은 제외. `PendingAdvancement` 확정 불가 | 해시는 같은 추첨 수의 다른 준비를 구별하지 못함. 계약 D(쌍)·27.9 유지 |
| B-02 | 스트림별 `RawDraws` 증분 검증(H0→H1 초안 사용량, H1→H2 진루 상세, 나머지 0), 호출 수와 RawDraws 구분, `Commit.rng_evidence_mismatch`(실패 스트림·항목을 메시지에) | `Restore`가 추첨 수에 대한 유일한 위치를 증명. 생성 경로는 증명하지 않음(신뢰 경계 기록). 계약 C 유지 |
| B-03 | `GameState`·`GameStep` 모든 멤버 구조 비교(중첩 컬렉션 내용·순서), 5개 타입 속성 목록 고정 테스트, `Commit.preview_mismatch`(첫 차이 경로) | D-7 전체 범위. 재실행 결과만 채택 |
| B-04 | **대안 A**: `PitchCommitGate` → 내부 불변 `CommitEvidence` → 공통 `CommitGate` 코어 → `StagedCommit` → 기존 `GameCursor.Publish`. 순서 O-2 대안 C(7a `CheckPrepared`, 7b 연결, 10a 미리보기, 10b RNG) | C3-A 경계 테스트 6건이 `CommitGate`를 고정하므로 B(테스트 약화)·C(partial, 리플렉션 실패) 기각. 증거는 결속·1회성·범용 토큰 금지·조기 검사 금지. 공통 코어임을 구조·테스트로 입증 |
| B-05 | 신규 5개: `Commit.candidate_missing`·`candidate_link_mismatch`·`preview_mismatch`·`rng_evidence_mismatch`·`pitch_count_invalid`; `Commit.receipt_mismatch` 재사용(ef2·위치 포함) | 빌더 코드는 빌더 소속(O-1). 기존 `CheckPrepared`·`Id.*`·`Rng.*`·`Game.*` 코드·순서 불변 |
| B-06 | 정상·실패·경쟁·취소 매트릭스(§27.18), 위조는 기존 internal 테스트 경계에서만, 프로덕션 우회 API 없음, C3-A 67·전체 1,580·`verify.ps1` 2회 회귀 | M25 생존·변이 21건 미재실행은 C3-C 인계 |
| B-07 | Simulation 내부, 새 진입 타입만 `HiddenTendencyBoundaryTests`에 추가, PA 루프·생성기 재설계·Reducer/RNG/진루 규칙 변경·SQLite·Repository·UI·Phase 4·C3-C 하네스 제외 | D-8 유지 |

**상태.** B-01~B-07 `OWNER APPROVED`(문서) · D-1~D-8·O-1~O-9 `OWNER APPROVED`(불변) · C3-A 프로덕션 구현 완료 / GPT Chat 기술 검토 `PASS WITH NOTES` / 로컬 커밋 `18ed60e` · C3-B 프로덕션 `NOT STARTED / NOT AUTHORIZED` · C3-C `NOT STARTED / NOT AUTHORIZED` · Phase 3 Gate `INCOMPLETE`.
