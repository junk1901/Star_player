# Star Player B1-T00 — NSC-6 소유자 결정 및 최종 통합 문서 정정

## 현재 상태

- 기술 계약: READY_FOR_OWNER_APPROVAL
- 통합 문서: NOT_RECONCILED
- Official Execution: NOT_READY
- Final Execution Freeze: PENDING

Codex가 발견한 문서 불일치 2건만 해결한다.

## 1. MISMATCH-01 수정

2부 §2.2의 `frozen_games_per_cell` 설명을 최신 정정안으로 대체한다.

- `frozen_games_per_cell: P` (non-null)
- `frozen_games_per_cell == plan.games_per_cell`
- Unit PZL `plan.run_result_version`·`plan.run_result_hash`는 Schema상 nullable을 유지하지만, 정상 인증에서는 부모의 non-null 값과 같아야 한다.

기존 §4 규범과 Schema는 변경하지 않는다.

## 2. NSC-6 OWNER 채택 결정

다음 의미 계약을 채택한다.

### coverage 네 필드

- `completed_games`: 해당 범위에서 status=Completed인 경기 수
- `events`: 해당 범위에서 관찰된 모든 확정 사건 수. Failed 경기의 확정 Commit도 포함
- `pitches`: 해당 범위에서 `pitch != null`인 EventObservation 수. `pitch`, `take`, `in_play` 포함. Failed 경기 포함
- `called_pitch_steps`: 실제 관찰된 BranchStep 중 기존 `StageIds.CalledPitch`에 해당하는 Step 수. Failed 경기 포함

기존 R-8의 `completed_games`·`events` 정의는 P-11 이후에도 유지되는 잔여 의미 규범으로 명시 채택한다.

`coverage.pitches`는 Completed 경기만 집계하는 `Technical.counters.pitches`와 항상 같아야 한다고 요구하지 않는다.

coverage는 해당 Check에 지정된 증거 범위의 관찰 건수이며, Check의 PASS 기여 건수와 반드시 같지는 않다.

### 범위

- Unit PZL `pzl-5`, `pzl-10`, `pzl-12a`: 해당 Unit의 실행된 모든 경기
- Unit PZL `pzl-12b`: 네 필드 모두 0
- NonAutomated Unit의 `pzl-10`: NOT_APPLICABLE이지만 Unit 증거 범위의 coverage를 기록
- PZL6 `pzl-6`: 승인된 고정 20경기 표본
- Phase Root #1·#2·#3·#5·#12a: 전체 54개 Unit의 실행 경기
- Phase Root #10: Automated Unit만
- Phase Root #6: PZL6 고정 20경기
- Phase Root #4·#7·#8·#9·#11 및 #12b: 네 필드 모두 0

TEST_PROFILE의 Case 건수는 `test_evidence.cases`에서 관리한다. 관찰과 Test Profile을 함께 사용하는 Check는 관찰 증거 부분만 coverage에 반영한다.

### 재검산

- Unit의 Completed 경기 수, 확정 사건 수, 투구 사건 수, called-pitch Step 수를 각각 독립 재관찰과 비교한다.
- PZL6 및 Phase Root에서는 각 대상 범위의 경기별 관찰 값을 중복 없이 집계한다.
- 불일치는 기존 오류 체계에 따라 거부하고, 기존 Hash·RNG·Replay 계산식은 변경하지 않는다.

### 0건과 Gate

기존 Unit PZL의 판정 규칙은 그대로 유지한다.

- `pzl-5`에서 events=0이면 기존 NOT_EVALUATED 판정을 유지한다.
- Automated `pzl-10`에서 called_pitch_steps=0이면 기존 NOT_EVALUATED 판정을 유지한다.

**Phase Root PASS에 대해 별도의 신규 'coverage 필드 0 금지' 조건은 추가하지 않는다.**

PASS/FAIL/NOT_EVALUATED는 기존 확정된 PZL·Gate 규칙으로만 재계산한다.

## 3. 최종 문서에 반영

다음 항목만 수정한다.

- §7: coverage 의미와 계산 범위 규범 추가
- 2부 §2.2: nullable 설명 오류 수정
- 2부 §2.3·§2.4: NSC-6 해결 및 R-8 잔여 유지 관계 표시
- 3부: 기존 반례 하위 사례에 coverage 관련 검증 추가
- 4부·5부: 승인 체크리스트와 원문 대조 항목 갱신

NSC-4·5는 기존 정정안대로 각각 T06a·T09 구현 전 필수 해결 Gate로 기록한다.

## 4. 변경 제한

새로운 PASS 조건, 통계 정책, Hash 계약 버전, RNG 소비, Replay 순서, Registry 보안 설계를 추가하거나 변경하지 않는다.

새로운 운영 SID·Seed Pin·Golden 값을 만들어 넣지 않는다.

코드·ADR·TASK.md·Git 변경과 빌드·테스트·공식 실행은 금지한다.

## 5. 결과

다음 내용을 제출한다.

1. 실제 적용한 계약 문장
2. 변경 위치와 변경 전후 비교
3. 기존 규범과 신규 OWNER 결정의 구분
4. 추가·변경한 반례
5. 문서 SHA-256
6. Codex가 확인할 최소 검수 범위

Codex의 변경 범위 독립 검수 후에만 SOURCE_RECONCILED로 판정한다. 이번 결정은 B1-T00 전체 소유자 승인이나 구현 착수 허가를 의미하지 않는다.