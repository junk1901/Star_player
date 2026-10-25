# B1 OWNER 결정 기록 3 — 최종 설계 결정 7건 승인 및 구현 허가

- 수신 일시: 2026-10-25T08:04:02+09:00 (Asia/Seoul, 이 작업이 지시를 받은 시각) · 방식: OWNER 명시적 텍스트 결정("Star Player B1 — OWNER 최종 설계 결정 7건 승인 및 구현 허가") · 결정자: 지시를 내린 OWNER(이름·서명은 기록하지 않음)
- 근거안: [B1_OWNER_DECISION_PROPOSALS_3_FINAL.md](B1_OWNER_DECISION_PROPOSALS_3_FINAL.md)
- 승인 범위: B1의 증거 형식, 비교 범위, Archive 관리 및 구현 보완에 한정.
- 바뀌지 않는 것: 승인 계약 `B1-T00_FINAL_CONTRACT_DRAFT.md` 바이트·SHA `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64`, [승인 기록](B1-T00_OWNER_APPROVAL_RECORD.md)·[NSC-4·NSC-5 결정](B1-T00_NSC4_NSC5_OWNER_DECISION.md)·[결정 2](B1-T00_OWNER_DECISION_2.md) 원문, `TASK.md`, PDF, Hash·RNG·Replay 계산식. 충돌하면 계약 본문이 우선하고 충돌은 OWNER에게 보고합니다.
- 유지: Official Execution = NOT_READY, Final Execution Freeze = PENDING, Phase 3 = INCOMPLETE. 운영 계정 생성·보호 디렉터리 설치·정식 AT·공식 대규모 실행은 별도 승인 전 수행하지 않음. Git Commit·Push 금지.

## 결정 1 — violations

현재 구현의 경기별·원인 카운터별 1건 생성 방식을 채택한다. 기존 Violation 4필드, 정렬·중복 금지, 재계산 검증 조건을 유지한다.

## 결정 2 — RAW_INVENTORY

형식 ID `simharness-raw-inventory-v1`을 채택한다. Root는 `format`, `artifacts`의 정확히 두 필드로 고정한다. `artifacts`에는 검증 스냅샷에 속한 필수 ORIGINAL 원본을 빠짐없이 포함하고, 자기 자신·후속 영수증·Registry 기록·나중에 생성한 Canonical Payload는 포함하지 않는다. 동일 영수증의 ORIGINAL `source_files`와 정합성을 검사하고, 닫힌 Schema Reader·중복·누락·SHA 불일치 반례를 추가한다.

## 결정 3 — witness_digest

`difference`, `standard_error`, `lower_bound`에도 BITS64 표현을 적용한다. 기존 구현을 유지하며 다른 수치 표현이나 통계 계산식을 변경하지 않는다. 운영 Environment Profile의 witness_digest는 추후 승인된 운영 환경에서 별도로 생성하고 검증한다.

## 결정 4 — PZL #6 최종 상태 비교

기존 이중 Reducer 재생 비교를 유지하되 다음 비교를 추가한다.

1. Production이 실제 확정한 최종 GameState 전 필드와 재생 결과 비교
2. 기존 observe 경로로 수집한 커밋별 GameFact와 재생 결과 비교
3. 최종 공식 기록, Receipt 및 전체 비교 필드 커버리지 검증

T04a의 관찰 기능은 읽기 전용으로 확장한다. 새 RNG 소비, Hash 계산식, 게임 상태 변경은 허용하지 않는다. F-26의 public 위임 메서드 대신 기존 구조 비교 방식을 사용하는 구현 선택을 승인하되, 누락 필드가 있거나 Production 상태와 비교하지 못하면 인증을 거부해야 한다.

## 결정 5 — Unit별 GAME_RESULTS

신규 형식 `simharness-game-results-v1`을 승인한다. 각 Unit에서 실행된 모든 경기의 §3.6 GameResult를 보존하는 bundle을 정확히 하나 생성한다. 구성은 Header 1행, 실행 경기 순서대로 GameResult 레코드 1행씩, End 1행이다. 최종 결정안에 명시한 Header·End 필드와 타입을 닫힌 Schema로 적용한다. Game 레코드 구조는 `{game: GameResult, record}`이며 내부 `game` 객체의 JCS 바이트가 부모 공식 결과 문서의 해당 GameResult 객체 JCS 바이트와 같아야 한다. 승인된 JCS·UTF-8·LF 규칙, 레코드 크기 상한, 경기 수·순서, GameId·Seed·ResultHash 일치, 파일 Raw SHA 및 ArtifactRef 결속을 검증한다. 실패 경기도 누락하거나 다른 경기로 교체하지 않는다. Unit PZL의 `additional_evidence.artifacts`를 통해 참조하며 기존 R-13 Artifact DAG를 유지한다. 기존 GAME_EVENTS와 GAME_RESULTS의 증거 역할을 혼합하지 않는다.

**적용하는 형식(근거안 §5 그대로):**

| 레코드 | 정확한 멤버(JCS 키 순) | 타입·규칙 |
|---|---|---|
| Header(1행) | `bundle, contracts, family, run_id, unit_evidence_id, unit_id, validation_set_id` | `bundle` = `"simharness-game-results-v1"`, `contracts` = 승인 5-tuple, `family` = `fc3-cal\|fc3-int\|fc3-dom`, `run_id` RUN, `unit_evidence_id` = 대응 Unit PZL의 `execution.evidence_id`, `unit_id` EXACT, `validation_set_id` ID — 모두 대응 Unit PZL과 일치 |
| Game(실행 경기마다 1행) | `game, record` | `record` = `"game"`, `game` = §3.6 GameResult, 부모 문서 `units[i].technical.games[k]`와 JCS 바이트 동일, `game.index` 1..n 연속, `game_id`·`seed` = 승인 Seed 계획, 실패 경기 포함 |
| End(마지막 행) | `completed_games, executed_games, failed_games, record` | `record` = `"end"`, 수 = 부모 Technical의 `games_completed`·`games_executed`·`games_failed`, `executed_games` = Game 행 수 = Unit PZL `games` 길이 |

직렬화: `레코드 := JCS(JSON 객체) || 0x0A`, UTF-8, BOM·CR·빈 행 없음, 마지막 바이트 0x0A, 레코드 1 MiB 이하. ArtifactRef: role `GAME_RESULTS`, `unit_id` = 그 Unit, `game_id` null, Unit PZL 문서마다 정확히 1개. 검증: Header 결속 → 각 `game` = 부모 문서 행 = 검증기 재실행 투영, `game.result_hash` = Unit PZL `games[k].game_result_hash` → 건수·End. 오류 분류: 형식 위반·파일 누락·Unit당 0개 또는 2개 이상 → `archive_invalid`, 내용 불일치 → `replay_mismatch`, 결속 불일치 → `reference_invalid`.

## 결정 6 — 출력 배치

기존 `run/<run_id>/…` 배치를 현재 구현의 내부 출력 경로로 수용한다. 이는 B-4의 공식 출력 경로와 실행 Manifest에 관한 OWNER 동결을 대신하지 않는다. 파일 배치를 바꾼다는 이유로 기존 승인 Hash·Replay 의미나 Archive 검증을 변경하지 않는다.

## 결정 7 — PZL #6 JSON 보존 (신규 세부 규범)

고정 20경기 JSON의 GAME_RESULTS 역할 사용을 중단한다. 20개 JSON은 PZL6 증거에 소속시키며 다음을 추가 OWNER 규범으로 명시한다.

- Integration PZL6 공식 문서 1개: `role=PZL6`, `game_id=null`
- 고정 표본 20개의 JSON 파일: 각각 `role=PZL6`, `game_id` = 해당 경기 ID
- 해당 20개 파일을 하나의 논리적 20경기 JSON bundle로 취급한다.
- PZL6 관련 Artifact는 이 구조에서 총 21개이며 각 Artifact ID는 고유하다.
- JSON 파일의 Artifact ID는 `pzl6-json-<mode>-<index:D2>` 형식을 사용한다.
- JSON의 UnitId·GameId·Index·Seed는 승인된 모드별 최초 10경기 계획과 일치해야 한다.
- 20개 JSON의 원본 바이트·SHA·길이를 각각 검증한다.
- PZL6 공식 문서의 추가 증거 참조를 통해 결속하고 R-13 방향을 유지한다.
- 이 20개 JSON은 Unit별 GAME_RESULTS 필수 개수에 포함하지 않는다.

이것은 §21의 20경기 JSON bundle을 여러 물리 파일로 구현하는 방식을 명시적으로 고정한 신규 세부 규범이다. 기존 계약이 자동으로 21개 Artifact를 허용했다고 주장하지 않는다.

## 구현 지시(요지)

RAW_INVENTORY Reader 및 영수증 결속 보완, PZL #6 Production 최종 상태와 커밋별 GameFact 비교, Unit별 GAME_RESULTS bundle 생성·검증, PZL6 20경기 JSON 역할 및 참조 정정, Archive 필수 역할·개수·DAG 검증 보완. Codex T11-01~08 수정 결과 보존. 필수 회귀: 기존 Golden 전체, T11-01~08 직접 반례, GAME_RESULTS 누락·중복·순서 교체·SHA 변조, PZL6 20개 JSON과 Unit GAME_RESULTS의 역할·개수 충돌, Production 전체 GameState·GameFact 비교, T04a Seam 변경 후 Dry Pilot 9 Run 불변성, 전체 Release 시험·`verify.ps1`, D3. 완료 후 Codex READ-ONLY 표적 재검수를 위해 멈춘다.
