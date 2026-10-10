---
type: phase-review
phase: "01"
phase_name: "Player identity, visibility, and baseball vocabulary"
status: 승인 완료
started: "2026-10-10"
completed: "2026-10-10"
developer_ai: Claude Code
reviewed_by: "GPT Chat 최종 검토(사용자 전달, 2026-10-10) · Codex 최종 재검토 PASS WITH NOTES"
branch: "main"
commit: "64167f788dc6a77fe4725bb13e06d946fd122ada (승인 후 사용자 지시로 커밋·푸시, origin/main)"
roadmap_anchor: "phase-1--player-identity-visibility-and-baseball-vocabulary"
last_updated: "2026-10-10"
codex_review: "1차 CHANGES REQUIRED → 보완 후 최종 재검토 PASS WITH NOTES"
tags:
  - star-player
  - phase-review
---

# Phase 01 Review — Player identity, visibility, and baseball vocabulary

> [!warning] 상태 규칙
> 허용 상태는 `초안`, `검토 대기`, `승인 완료`, `보완 필요`입니다. 구현 AI는 외부 검토 없이 `승인 완료`로 변경하지 않습니다. 실행하지 않은 검증은 `미검증`, 수행하지 않은 독립 리뷰는 `미실시`로 기록합니다.

## A. 기본 정보

| 항목 | 내용 |
|---|---|
| Phase | Phase 01 — Player identity, visibility, and baseball vocabulary |
| 작업 시작일 | 2026-10-10 |
| 작업 완료일 | 2026-10-10 (자체 검증 종료) |
| 개발 담당 AI | Claude Code |
| 리뷰 상태 | 승인 완료 (PASS WITH NOTES, 2026-10-10) |
| 관련 ROADMAP | [Phase 1](../../ROADMAP.md#phase-1--player-identity-visibility-and-baseball-vocabulary) |
| 관련 Git 브랜치 | main |
| 관련 커밋 | `64167f7` `feat: complete phase 1 player domain and visibility` (승인 후 별도 지시로 커밋·푸시; 승인 시점에는 미커밋이었음) |
| 독립 Codex 리뷰 | `CHANGES REQUIRED` (High 2, Medium 2) → 아래 "Codex 리뷰 이력과 보완" 참조 |

- 대시보드: [Star Player 개발 대시보드](../00_Dashboard.md)
- 공식 현재 상태: [CURRENT_STATE.md](../../CURRENT_STATE.md)
- 보고서: [PHASE_1_REPORT.md](../../docs/reports/PHASE_1_REPORT.md) · 검증 로그: [PHASE_1_VERIFY_LOG.md](../../docs/reports/PHASE_1_VERIFY_LOG.md) · 결정: [ADR-002](../../docs/decisions/ADR-002.md) · 데이터 사전: [data-model.md](../../docs/data-model.md)

## Codex 리뷰 이력과 보완 (CHANGES REQUIRED 보존)

> [!warning] 원래 판정 보존
> 자체 검증(테스트 254개 통과, verify PASS) 이후 독립 Codex 리뷰가 **`CHANGES REQUIRED`**(High 2건, Medium 2건)를 반환했습니다. 이 판정은 삭제하지 않으며, 아래 보완은 같은 날 수행·재검증되었지만 **Codex 재검토는 `미실시`**입니다. 상세 증거: [PHASE_1_REPORT.md §0](../../docs/reports/PHASE_1_REPORT.md), [ADR-002 8–11](../../docs/decisions/ADR-002.md).

| # | 심각도 | Codex 지적 | 보완 | 재검증 |
|---|---|---|---|---|
| 1 | High | 공개 API로 원본 `PlayerWorld`(NPC 정확값·`DevelopmentProfile`) 접근 가능, 임의 `ControlledPlayerId`로 NPC 위장 가능 | 원본 세계·포트·ViewerContext·서비스 생성자·Infrastructure 원본 소스를 `internal`로, `InternalsVisibleTo`는 Infrastructure/테스트만(Client 제외, 테스트로 고정), `PlayerSessionOptions`에서 선수 ID 제거(조작 선수는 신뢰된 픽스처 바인딩), 공개 진입점은 `PlayerInfoComposition` 하나 | `TrustedBoundaryTests` 14개 + 가시성 테스트 보강, 변이 검사 9건 검출 후 원복 |
| 2 | High | 한 선수가 다른 구단에서 겹치는 기간에 등번호 보유 가능, `ActiveOn`이 임의의 첫 행 선택 | 교차 구단 중복 거부, 반개구간 `[from,to)`·같은 날 인계 허용, `ActiveOn`이 `Result`로 충돌 오류 반환, 실패 시 원장 불변, 컬렉션 읽기 전용 복사 | 도메인 신규 9개 + 픽스처 2개 |
| 3 | Medium | RuleProfile이 기본값 버전 ID 허용, 가변 컬렉션 공유, 단순화 항목 검증 부족 | 불변 정의/단순화(`{id, description}`), 입력 방어적 복사, 기본값·중복·겹치는 버전 거부 | 도메인 신규 7개 + 픽스처 2개 |
| 4 | Medium | `verify.ps1`이 MSBuild 자식 프로세스 때문에 종료되지 않을 수 있음 | 스크립트 한정 빌드 서버/노드 재사용 비활성화, 명령별 시간 제한과 해당 프로세스 트리만 종료, 잔여 프로세스 정보 점검(Z1) | 전체 verify 연속 2회 모두 PASS, 정상 종료, 잔여 서버 없음 |

## 최종 승인 기록 (2026-10-10)

> [!success] 승인 완료 — PASS WITH NOTES
> GPT Chat 최종 검토 결과를 사용자가 전달 (2026-10-10): **승인 완료 (PASS WITH NOTES)**. 근거(사용자 전달 내용): xUnit 289/289, `verify.ps1` 연속 2회 PASS, Windows Export·SQLite 8/8, 수정 후 수동 UI 검증 사용자 완료, Codex 최종 재검토 PASS WITH NOTES, Phase 2를 막는 High/Critical 없음.

### 판정 이력 (모두 보존)

| 순서 | 검토 | 판정 |
|---|---|---|
| 1 | 구현 AI 자체 검증 (254개, verify PASS) | 통과 (외부 검토 아님) |
| 2 | Codex 1차 독립 리뷰 | **`CHANGES REQUIRED`** (High 2, Medium 2) |
| 3 | 보완·재검증 (289개, verify 연속 2회 PASS) | 구현 AI 자체 확인 |
| 4 | Codex 최종 재검토 | **`PASS WITH NOTES`** (사용자 전달) |
| 5 | GPT Chat 최종 검토 | **`승인 완료 (PASS WITH NOTES)`** (사용자 전달) |

### 수동 UI 검증
수정 후 빌드의 수동 UI 검증을 사용자가 완료했다고 전달했습니다. 항목별 세부 결과나 스크린샷은 제공되지 않았으며 여기에 기록하지 않습니다. FHD는 여전히 Phase 5+ 항목입니다.

### 열린 Notes (추적 기술 부채)
- Domain 컬렉션 가변성(Codex Medium, Notes): `Ratings.cs`(`PitchRepertoire.Pitches`), `Proficiency.cs`, `PlayerStatus.cs`, `DevelopmentProfile.cs`, `Player.cs`가 `List` 기반 `IReadOnlyList`를 그대로 노출. **Phase 2에서 이 컬렉션을 경기 상태·결정론적 처리에 쓰기 전에** 방어 복사, 읽기 전용(`AsReadOnly`) 노출, 캐스팅 변이 테스트(`IList` 캐스트 후 변경 시 예외)를 적용한다. 현재 승인된 Phase 1 코드는 수정하지 않는다.

## B. 개발 목표

### 개발 목적

숨겨진 내부 데이터를 UI로 노출하지 않으면서 타자·투수·투타 겸업·동명이인 선수와 야구 기본 어휘(포수, 배터리, 규칙 프로필, 구장, 등번호)를 표현할 수 있음을 증명한다.

### 구현 범위

- 1-A: ID, Player 신원, 나이, 능력치(0~100), PlayerState/DevelopmentProfile/Proficiency 분리, 출신·입단 경로·등록 신분·1·2군 분리, `Money`, `InningsOuts`
- 1-B: 포수 4개 능력치와 C 숙련도 분리, 배터리 조합 ID, RuleProfile 영역/버전, Stadium/ParkFactor, C47 등번호 구간
- 1-C: `PlayerVisibilityPolicy`/`PlayerPublicView`/세션 기반 ViewerContext, JSON 가상 구단 2개, Godot 선수 정보 진단 화면
- 비범위(미구현 확인): 경기 시뮬레이션, 성장·노화, 선수 생성 UI, 실제 KBO 데이터, SQLite 스키마/마이그레이션, 드래프트, 구단 AI, Gemini, 최종 디자인

### 완료 기준

- [x] ROADMAP Acceptance Criteria (아래 D 표)
- [x] Common Gate G 적용 항목 (3번 N/A, 4번 FHD·수동 클릭은 `미검증`)

### 관련 요구사항 및 시스템 계약

- 요구사항: [REQUIREMENTS_TRACEABILITY.md](../../docs/REQUIREMENTS_TRACEABILITY.md) R72, R84, R94, R95, R65
- 시스템 계약: [SYSTEM_CONTRACTS.md](../../docs/SYSTEM_CONTRACTS.md)
- 관련 계약/요구사항 ID: C01–C06, C40, C41, C46, C47

## C. 실제 구현 내역

### 구현한 기능

- Domain 값 객체·모델 전반(위 범위), 불변식은 팩토리에서 검증하고 `Result`로 보고
- `PlayerSheet`는 숨겨진 `DevelopmentProfile`을 멤버로 갖지 않음 → 공개 뷰로 새는 경로가 구조적으로 없음
- Application 공개 DTO(도메인 타입 미참조), 내 선수 정확값 / 타 선수 관측 등급·범위·신뢰도(`observation-v0`), 치트 모드도 잠재력 비노출
- JSON 스키마 v1 + 읽기/쓰기(항등 라운드트립), 내장 가상 팩(10명·2구단)
- Godot `PlayerDiagnosticsScreen`, `--player-diagnostics`/`--cheat` 실행 모드, `verify.ps1`에 G3/E3 추가

### 주요 변경 파일

| 파일 | 변경 내용 |
|---|---|
| `src/StarPlayer.Domain/{Players,Baseball,Teams}/*` | 신규 모델 |
| `src/StarPlayer.Domain/Common/{Ids,Money,SnakeCase}.cs` | ID 확장/검증, Money |
| `src/StarPlayer.Application/{Queries,Sessions,Ports}/*` | 가시성 정책·DTO·서비스·자체 점검 |
| `src/StarPlayer.Infrastructure/Fixtures/*` | JSON DTO/매핑/소스 |
| `content/fictional/phase1_two_club_pack.json` | 가상 데이터 |
| `game/UI/PlayerDiagnostics/*`, `game/Bootstrap/*`, `game/UI/MainMenu/*` | 진단 화면과 연결 |
| `tests/**` | 신규 테스트 172개 |
| `tools/verify.ps1` | PHASE_1 로그, G3/E3, 신선도 검사 수정 |
| `docs/data-model.md`, `docs/decisions/ADR-002.md`, `docs/reports/PHASE_1_REPORT.md` | 문서 |

### 새 모듈과 기존 모듈 변경

- 새 모듈: Domain Players/Baseball/Teams, Application Queries/Sessions, Infrastructure Fixtures, Godot PlayerDiagnostics
- 수정한 기존 모듈: `Ids.cs`(검증 강화: 64자·허용 문자), Bootstrap, MainMenu, `verify.ps1`, 두 `.csproj`

### 계획 대비 달라진 내용

- `ClubId`를 Phase 0 `TeamId` 옆에 추가 (C47/C06 용어) — 통합 여부는 검토 요청
- 성격 특성 9종을 원본 목록에서 효과 없는 신원 데이터로 추가 (구조적 성격 효과는 Phase 21)
- `observation-v0`, 구장 계수 한도 850–1150‰, 등번호 0–99는 픽스처 값이며 승인된 튜닝이 아님
- 이벤트 페이로드는 레코드 타입만 정의(직렬화 변환기 없음)
- (Codex 보완) 규칙 단순화는 `{id, description}` 객체로 변경(팩 스키마 v1, 커밋 전 변경), 같은 영역의 규칙 버전은 기간이 겹칠 수 없음, `PlayerSessionOptions`에서 선수 ID 제거

## D. 테스트 및 검증

### 실행 환경

| 항목 | 값 |
|---|---|
| OS | Windows 11 Home 10.0.26200 |
| Godot | 4.7.2 stable Mono |
| .NET SDK | 8.0.425 |
| 빌드 구성 | Release (클라이언트 Release/ExportRelease 확인) |

### 실제 실행 결과

보완 전(첫 자체 검증)과 보완 후를 구분해 기록합니다.

| 구분 | 명령 | 결과 | 상태 | 로그·결과 파일 |
|---|---|---|---|---|
| 보완 전 | `tools/verify.ps1` | GATE PASS, xUnit 254개 (Domain 122 / Simulation 1 / Application 97 / Infrastructure 34) | 통과 (Codex가 결함 발견) | 이력만 보존 |
| 보완 후 1회차 | `tools/verify.ps1` | GATE PASS, exit 0, 19/19 PASS, 재생성된 Windows export | 통과 | [run1 로그](../../docs/reports/PHASE_1_VERIFY_LOG_run1.md) |
| 보완 후 2회차 | `tools/verify.ps1` (연속 재실행) | GATE PASS, exit 0, 19/19 PASS, 타임아웃 없음, 잔여 빌드 서버 없음 | 통과 | [현재 로그](../../docs/reports/PHASE_1_VERIFY_LOG.md) |
| 보완 후 | xUnit 합계 | **289개 통과** (Domain 138 / Simulation 1 / Application 112 / Infrastructure 38), 신규 35개, 기존 254개 회귀 없음 | 통과 | 위 로그 U1–U4 |
| 보완 후 | Release 빌드 | 경고 0 / 오류 0 | 통과 | B1 |
| 보완 후 | Godot headless 선수 카드 자체 점검 | 7/7, `hidden_identifier_leaks=0` | 통과 | G3 |
| 보완 후 | 새 Export 콜드 카피: SQLite | 8/8 | 통과 | E2 |
| 보완 후 | 새 Export 콜드 카피: 선수 진단 화면 QHD | PASS + 스크린샷 2장 | 통과 | E3 |

### 완료 기준별 충족 여부

| 완료 기준 | 상태 | 근거 |
|---|---|---|
| 네 시나리오가 합법적인 공개 DTO로 직렬화 | 충족 | `PlayerVisibilityTests`, `FixtureWorldTests` |
| 독립 필드가 JSON 라운드트립 | 충족 | `Pack_round_trips_to_identical_json`, 상태/숙련도/숨김 프로필 단독 변경 테스트 |
| 쿼리 플래그로 NPC 정확값 획득 불가 | 충족 | 적대적 페이로드·위장 ID 테스트, `ViewerContext` 비공개 생성 |
| 이름을 조인 키로 쓰지 않음 | 충족 | 동명이인 테스트, 모든 조회는 ID |
| 포수 능력과 C 숙련도가 공개 뷰에서도 별개 | 충족 | 두 포수 시나리오 테스트 |
| 규칙이 한 버전 소유자로 라우팅, 미지 버전 명시적 실패 | 충족 | `RulesVenueBatteryTests`, 팩 거부 테스트 |
| 구장 표시/시뮬레이션 분리, ID 유지, 계수 범위 검사 | 충족 | 구장 테스트 |
| C47 등번호가 구단별 기간 배정 | 충족 | `JerseyLedgerTests`, 팩 이력 |
| Common Gate G 3번(저장) | 해당 없음 | Phase 4 이전 |
| (보완) 원본 세계·숨은 프로필을 공개 경로로 얻을 수 없음, 조작 선수 위장 불가 | 충족 | `TrustedBoundaryTests`, 가시성 테스트, 변이 검사 |
| (보완) 한 선수의 구단 간 등번호 기간 중복 불가 | 충족 | 도메인 원장 테스트, 팩 로더 테스트 |
| (보완) RuleProfile 스냅샷 불변·검증 | 충족 | 규칙 테스트 |
| (보완) verify.ps1 재실행 안정성 | 충족 | 연속 2회 PASS |

### 테스트하지 못한 항목

- 사람이 직접 `선수 정보 진단` 버튼 → 목록 선택 → `뒤로`를 클릭하는 수동 테스트: **보완 전** 빌드는 사용자가 완료했다고 전달함(항목별 세부 기록은 없음). **보완 후** 빌드는 별도 근거가 없어 `미검증`
- FHD(1920×1080) 레이아웃: `미검증` (Gate G 4번은 Phase 5부터)
- Codex 재검토·GPT Chat 검토: 완료 (위 승인 기록). FHD: `미검증`

## E. 발견된 문제

### 발생한 오류

- (Codex) 공개 API 우회·조작 선수 위장, 구단 간 등번호 중복, RuleProfile 가변성, verify 비종료 위험 — 위 표
- `PlayerInfoService`가 기준일 설정 전에 소속 구단을 읽어 정렬이 깨짐 (테스트가 발견)
- Godot Label에서 `\r\n`이 두 줄로 표시됨
- `verify.ps1` 신선도 검사가 `.tscn`을 컴파일 입력으로 취급해 B3가 거짓 실패

### 해결한 문제

- 위 3건과 Codex 지적 4건 모두 수정 후 재검증 (최종 verify 연속 2회 PASS)

### 미해결 문제

- 없음 (실패하는 검증 없음). 아래 부채와 `미검증` 항목은 열려 있음.

### 기술 부채 및 향후 영향

- 다른 Domain 값 객체의 공개 컬렉션(`PitchRepertoire`, `ProficiencyProfile`, `PlayerStatus` 등)은 아직 `List`를 그대로 노출 — Client가 Domain에 접근할 수 없어 위험은 낮지만 후속 Phase에서 읽기 전용 복사로 통일 권장
- 이벤트 페이로드 직렬화 변환기는 이벤트 발행 시점(Phase 4+)에 추가
- `PlayerStatus`가 "구단 없는 정식 현역"을 거부 — Phase 15/16에서 FA 상태 필요 시 재검토
- `JerseyNumberLedger`는 목록 선형 탐색 — Phase 17/29 규모에서 재측정

관련 이슈 문서: 없음 (`03_Issues/`에 기록할 미해결 버그 없음).

## F. 설계 변경

| 변경 내용 | 변경 이유 | 영향 모듈 | 결정 문서 | Codex 추가 검토 |
|---|---|---|---|---|
| 숨김 데이터를 `PlayerSheet`에서 구조적으로 제외, 세션이 권한을 결정, 치트 모드 비확대, `observation-v0`, 픽스처 내장, `ClubId` 추가 | 숨은 정보 누출을 테스트로 증명 가능하게 하고 Phase 0 경계 유지 | Domain, Application, Infrastructure, Godot | [ADR-002](../../docs/decisions/ADR-002.md) | 미실시 (가시성 경계에 대해 권장) |

## G. AI 리뷰

### GPT Chat 리뷰

| 항목 | 결과 |
|---|---|
| 기능명세 충족 여부 | 승인 (PASS WITH NOTES) |
| 누락된 요구사항 | 없음 (Notes: 컬렉션 가변성) |
| 개선 권장사항 | Phase 2 전 컬렉션 방어 복사 |
| 최종 검토 의견 | 승인 완료 (PASS WITH NOTES) |

### Codex 리뷰 (선택적)

| 항목 | 결과 |
|---|---|
| 1차 판정 | **`CHANGES REQUIRED`** (High 2, Medium 2) — 보존 |
| 아키텍처 적합성 | 최종 재검토 PASS WITH NOTES |
| 모듈 간 의존성 | 최종 재검토 PASS WITH NOTES |
| 중요 로직 검증 | 최종 재검토 PASS WITH NOTES |
| 구조적 위험 요소 | 최종 재검토 PASS WITH NOTES |

## H. 다음 Phase 인수인계

### 시작 전 확인 사항

- 소유자의 별도 Phase 2 지시 필요. ADR-002의 검토 요청 6건에 대한 답변, Codex 보완 재검토(선택)
- Phase 2 구현 전에 `GameRules`(RuleProfile 상세 부분 집합, D2)를 명세해야 함

### 의존 관계

- Phase 2는 `RuleProfile`/`RuleArea.Game` 경계와 `InningsOuts`, `PlayerSheet` 뷰를 사용
- 성장(Phase 7)은 `DevelopmentProfile`을 별도 포트로 불러오고 `PlayerSheet`에 추가하지 않아야 함

### 주의할 문제 및 후속 작업

- 새로운 공개 DTO를 만들 때 `HiddenDataGuard`·구조 테스트를 같이 확장
- 치트 모드가 NPC 현재 능력치를 보여줘야 하는지 소유자 결정 필요

### 추가 검증 필요 사항

- 보완 후 빌드의 수동 클릭 테스트, FHD, Codex 보완 재검토

## I. 최종 결과

| 항목 | 결과 |
|---|---|
| 완료 기준 충족 여부 | 충족 (자체 검증, Codex 지적 4건 보완·재검증), 수동 UI 검증은 사용자 완료 전달(세부 없음), FHD 미검증 |
| 최종 리뷰 상태 | 승인 완료 |
| 남은 문제 | 열린 결함 없음. 추적 부채: Domain 컬렉션 가변성(Phase 2 사용 전 조치) |
| 승인자 또는 승인 근거 | GPT Chat 최종 검토 결과(사용자 전달, 2026-10-10) |
| 최종 수정 날짜 | 2026-10-10 |

### Phase 종료 체크

- [x] ROADMAP 완료 기준과 Common Gate G 확인
- [x] 실제 구현·변경 파일과 Git 변경사항 기록
- [x] 실행한 테스트의 명령·환경·실제 결과 기록
- [x] 미검증 항목, 미해결 문제와 설계 변경 식별
- [x] 중요한 설계 변경의 결정 문서 연결
- [x] 리뷰 상태를 `검토 대기`로 변경 후, 외부 승인으로 `승인 완료`
- [x] [CURRENT_STATE.md](../../CURRENT_STATE.md)와 실제 상태 일치 확인
- [x] GPT Chat 전달용 간략 요약 작성
- [x] 다음 Phase를 자동 시작하지 않음
