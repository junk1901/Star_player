---
type: phase-review
phase: "00"
phase_name: "Reproducible desktop foundation"
status: 승인 완료
started: "2026-10-10"
completed: "2026-10-10"
developer_ai: Claude Code
reviewed_by: "GPT Chat (사용자 전달, 2026-10-10)"
branch: "main"
commit: "미커밋 (작업 트리에만 존재)"
roadmap_anchor: "phase-0--reproducible-desktop-foundation"
last_updated: "2026-10-10"
tags:
  - star-player
  - phase-review
---

# Phase 00 Review — Reproducible desktop foundation

> [!warning] 상태 규칙
> 허용 상태는 `초안`, `검토 대기`, `승인 완료`, `보완 필요`입니다. 구현 AI는 외부 검토 없이 `승인 완료`로 변경하지 않습니다. 실행하지 않은 검증은 `미검증`, 수행하지 않은 독립 리뷰는 `미실시`로 기록합니다.

## A. 기본 정보

| 항목 | 내용 |
|---|---|
| Phase | Phase 00 — Reproducible desktop foundation |
| 작업 시작일 | 2026-10-10 |
| 작업 완료일 | 2026-10-10 (자체 검증 종료) |
| 개발 담당 AI | Claude Code |
| 리뷰 상태 | 승인 완료 |
| 관련 ROADMAP | [Phase 0](../../ROADMAP.md#phase-0--reproducible-desktop-foundation) |
| 관련 Git 브랜치 | main |
| 관련 커밋 | 없음 — 변경은 커밋·푸시하지 않고 작업 트리에 남김 (`outputs/` → 루트 이동은 rename으로 스테이징) |

- 대시보드: [Star Player 개발 대시보드](../00_Dashboard.md)
- 공식 현재 상태: [CURRENT_STATE.md](../../CURRENT_STATE.md)
- 공식 Phase 보고서: [PHASE_0_REPORT.md](../../docs/reports/PHASE_0_REPORT.md), 검증 로그: [PHASE_0_VERIFY_LOG.md](../../docs/reports/PHASE_0_VERIFY_LOG.md)

## B. 개발 목표

### 개발 목적

고정 스택(Godot 4.x .NET + C# + SQLite)이 이 개발 PC에서 빌드·헤드리스 테스트·Windows Export·SQLite 실행까지 재현 가능함을 증명한다.

### 구현 범위

- 공식 문서를 `outputs/`에서 루트로 정리(0-A)
- 솔루션/5개 경계 프로젝트/4개 테스트 프로젝트/아키텍처 테스트(0-B)
- Godot 진단 화면, SQLite 진단, Windows Export 검증, `tools/verify.ps1`(0-C)

### 완료 기준

- [x] 모든 프로젝트 Release 빌드 (경고 0)
- [x] Godot 없이 핵심 테스트 실행
- [x] 금지 의존성 테스트 통과
- [x] Export된 빌드가 에디터 없이 셸을 열고 SQLite Commit/Rollback/Backup 보고
- [x] 재현 명령 기록
- [x] Common Gate G 적용 항목 (저장소/마이그레이션/리플레이 조항은 Phase 4 이전이라 해당 없음)
- [x] (Codex 지적 이전 빌드) 내보낸 EXE의 진단 시작/종료 버튼 마우스 클릭 — 사용자 수동 테스트 보고 (직접 관찰 아님, 아래 D 참조)
- [x] 새 Export(Codex 지적 수정 후)의 버튼 마우스 클릭 — 사용자가 직접 완료했다고 전달 (항목별 결과·빌드 경로·스크린샷은 전달되지 않아 기록하지 않음)
- [x] Codex 지적 5건 수정 및 `tools/verify.ps1` 재검증 PASS (아래 D·E·F 참조)

### 관련 요구사항 및 시스템 계약

- 요구사항: [REQUIREMENTS_TRACEABILITY.md](../../docs/REQUIREMENTS_TRACEABILITY.md)
- 시스템 계약: [SYSTEM_CONTRACTS.md](../../docs/SYSTEM_CONTRACTS.md)
- 관련 계약/요구사항 ID: ROADMAP §1.2(계층 표), §2(저장소 구조), §6.4(Common Gate G). Phase 0은 게임 계약을 소비하지 않음.

## C. 실제 구현 내역

### 구현한 기능

- 문서 정리: `outputs/*` → 루트(`git mv`), Obsidian 링크·`work/` 스크립트 경로 갱신, 기획 내용 무변경
- Domain: `PlayerId`/`TeamId`/`CareerId`, `WorldRevision`, `Result`/`Error`
- Application: `IStorageDiagnostics` 포트, `DiagnosticsService`, `DiagnosticReport`, `BuildInfo`
- Infrastructure: `SqliteStorageDiagnostics` (Microsoft.Data.Sqlite)
- Godot: `CompositionRoot`, `GameRoot`, `MainMenu`(한국어 진단 화면), 진단 실행 모드(`-- --diagnostics`)
- `tools/verify.ps1`, `docs/toolchain.md`, `docs/decisions/ADR-001.md`

### 주요 변경 파일

| 파일 | 변경 내용 |
|---|---|
| `StarPlayer.sln`, `global.json`, `Directory.*.props` | 솔루션, SDK 8.0.425 고정, 공통 컴파일러 설정, 패키지 버전 |
| `src/StarPlayer.*` | 4개 계층 프로젝트 |
| `tests/StarPlayer.*.Tests` | 31개 xUnit 테스트 (아키텍처, SQLite 통합 포함) |
| `game/*` | Godot 프로젝트, 진단 화면, Export 프리셋, 클라이언트 전용 .sln |
| `tools/verify.ps1` | 반복 실행 가능한 검증 스크립트 |
| `docs/toolchain.md`, `docs/decisions/ADR-001.md`, `docs/reports/PHASE_0_*.md` | 툴체인, 결정, 보고서, 검증 로그 |
| `CURRENT_STATE.md`, `Star/00_Dashboard.md` | 실제 상태 반영 |
| `outputs/**` → 루트 | 공식 문서 단일 위치 정리 (rename) |
| `work/*`, `Star/**` 링크 | 경로 참조 수정 |

### 새 모듈과 기존 모듈 변경

- 새 모듈: 위 4개 계층 + Godot 클라이언트 + 테스트 4개
- 수정한 기존 모듈: 없음 (기존 코드 없음). 문서 위치와 링크만 변경.

### 계획 대비 달라진 내용

- Godot Export가 `<assembly_name>.sln`을 `game/` 옆에서 요구하여 `game/StarPlayer.Client.sln`을 추가 (ADR-001 §3).
- Godot 에디터가 `StarPlayer.Client.csproj`에 SDK 버전·TargetFramework를 추가함 — 그대로 유지.
- `rcedit` 미사용(`application/modify_resources=false`): 별도 다운로드가 승인 범위 밖.

## D. 테스트 및 검증

### 실행 환경

| 항목 | 값 |
|---|---|
| OS | Windows 11 Home 10.0.26200 |
| Godot | 4.7.2.stable.mono.official.ed1daf0bf |
| .NET SDK | 8.0.425 (런타임 8.0.31) |
| 빌드 구성 | Release |

### 실제 실행 결과

**1차 실행 (Codex 지적 이전 — 이력 보존, 이후 Codex 판정 `CHANGES REQUIRED`로 대체됨).** 원본 로그: [PHASE_0_VERIFY_LOG_initial.md](../../docs/reports/PHASE_0_VERIFY_LOG_initial.md) (`tools/verify.ps1` 종료 코드 0).

| 명령 | 기대 결과 | 실제 결과 | 상태 | 로그·결과 파일 |
|---|---|---|---|---|
| `dotnet build StarPlayer.sln -c Release` | 오류·경고 0 | 0 Warning, 0 Error | PASS | build/verify/B1.log |
| `dotnet test` Domain / Simulation / Application / Infrastructure | 전부 통과 | 9 / 1 / 14 / 7 통과, 실패·건너뜀 0, Godot 프로세스 0개 | PASS | U1–U4 |
| `godot --headless --path game --build-solutions --quit` | exit 0 | exit 0 | PASS | G1 |
| `godot --headless ... -- --diagnostics` | 8단계 통과 | 8/8, exit 0 | PASS | G2 |
| `godot --export-release "Windows Desktop"` | 실행 파일 + 네이티브 SQLite | exe/pck/console/e_sqlite3.dll | PASS | E1 |
| 내보낸 exe를 임시 폴더 복사본에서 실행 (QHD, 진단 모드) | 셸 표시 + 8/8 | 스크린샷 생성, 8/8, exit 0 | PASS | E2, build/verify/exported-shell.png |
| `verify.ps1 -GodotConsolePath <없는 경로>` | INCOMPLETE | 종료 코드 2, Godot 항목 INCOMPLETE | PASS(스크립트 동작 확인) | — |
| 기획 문서 검증 `work/Validate-PlanningPackage.ps1` | PASS | 1346 checks PASS | PASS | — |

**2차 실행 (Codex 지적 수정 후, 2026-10-10).** 원본 로그: [PHASE_0_VERIFY_LOG.md](../../docs/reports/PHASE_0_VERIFY_LOG.md) (`tools/verify.ps1` 종료 코드 0, Gate PASS).

| 명령 / 검사 | 1차 → 2차 | 상태 | 로그 ID |
|---|---|---|---|
| `dotnet build StarPlayer.sln -c Release` | 경고 0 → 경고 0 (Client가 이제 실제 Release로 빌드됨) | PASS | B1 |
| 솔루션 구성 매핑 (Release→Release, Debug→Debug), 이전 결함 매핑에서 실패함을 확인 | 미검사 → 검사 추가 | PASS | B2 |
| Client 출력 경로·구성 (Release/Debug/ExportRelease 속성, 산출물 신선도) | 미검사 → 검사 추가 | PASS | B3 |
| `dotnet test` Domain / Simulation | 9 / 1 → 9 / 1 | PASS | U1, U2 |
| `dotnet test` Application | 14 → 65 (아키텍처 14, 결정론 48, 서비스 3) | PASS | U3, U3a, U3b |
| `dotnet test` Infrastructure (실제 SQLite) | 7 → 7 | PASS | U4 |
| xUnit 합계 | 31 → 82, 실패·건너뜀 0, Godot 프로세스 0개 | PASS | U1–U4 |
| Export 템플릿 5개 파일 SHA-256 | 존재만 확인 → 해시 확인 | PASS | T3 |
| Godot headless 빌드 / 진단 | 8/8 → 8/8 | PASS | G1, G2 |
| 신규 Windows Export (ExportRelease, 이번 실행에서 생성, `e_sqlite3.dll` 포함) | 1차 Export → 신규 Export | PASS | E1, E1b |
| 신규 Export를 임시 격리 폴더에서 실행 (QHD) | 8/8 → 8/8 | PASS | E2 |
| 기획 문서 검증기 / 상대 링크 검사(`docs/`, `Star/` 67개) | 1346 PASS → 1346 PASS, 깨진 링크 0 | PASS | — |

### 수동 UI 테스트 (사용자 보고, Codex 지적 수정 이전 빌드)

2026-10-10 프로젝트 소유자가 내보낸 Windows EXE를 직접 실행해 보고한 결과이며, Claude Code가 직접 관찰한 것은 아닙니다. 상세: [PHASE_0_MANUAL_TEST.md](../../docs/reports/PHASE_0_MANUAL_TEST.md)

| 수동 확인 항목 | 보고된 결과 |
|---|---|
| EXE 정상 실행 | PASS |
| QHD 진단 화면 표시 | PASS |
| 한글 정상 표시 | PASS |
| 진단 시작 버튼 마우스 클릭 | PASS |
| SQLite 진단 결과 8/8 표시 | PASS |
| 종료 버튼 마우스 클릭 | PASS |
| 프로그램 정상 종료 | PASS |

- **적용 범위:** 위 7개 항목은 Codex 수정 전에 내보낸 빌드에 대한 사용자 보고입니다. 수정 후 새 Export의 수동 테스트는 아래 "새 Export 수동 테스트"를 참조하세요(완료 보고만 전달됨).
- 스크린샷: 소유자가 수동 테스트 스크린샷 경로를 제공하지 않았으므로 기록하지 않음. 존재하는 스크린샷은 자동 검증(E2)의 `build/verify/exported-shell.png`(git 제외, verify 실행마다 재생성)뿐.
- 테스트한 빌드 경로·커밋, 시각, 해상도는 보고되지 않아 `미기재`.

### 새 Export 수동 테스트 (사용자 수동 검증)

Codex 지적 수정 후 새로 내보낸 Windows Export의 수동 버튼 클릭 테스트를 사용자가 직접 완료했다고 GPT Chat 최종 검토 전달문에서 알려왔습니다. Claude Code가 직접 관찰한 것은 아닙니다. 항목별 결과, 테스트한 빌드 경로·커밋, 시각, 해상도, 스크린샷은 전달되지 않았으므로 **추측하여 기록하지 않습니다** (위 표의 7개 항목 PASS는 수정 이전 빌드에 대한 이전 보고입니다).

### 완료 기준별 충족 여부

| 완료 기준 | 상태 | 근거 |
|---|---|---|
| 솔루션 Release 빌드 | PASS | B1 |
| Godot 없이 핵심 테스트 | PASS | U1–U4 |
| 아키텍처 의존성 검증 | PASS | U3, 변조(mutation) 확인으로 탐지 동작 검증 |
| Godot 진단 화면 실행 | PASS | G2, E2 스크린샷(신규 Export), 사용자 수동 테스트 보고(이전 빌드) |
| 새 Export의 버튼 마우스 클릭 | 사용자 수동 검증 완료 보고 (세부 미전달) | 위 "새 Export 수동 테스트" |
| Codex 지적 5건 수정 | PASS(자체 검증), GPT Chat 승인, Codex 재검토 `미실시` | 아래 E·F, PHASE_0_REPORT.md §7 |
| Export된 Windows 프로그램 실행 | PASS | E2 |
| Export 환경 SQLite Commit/Rollback/FK/Backup | PASS | E2 8/8 |
| 검증 명령·결과 기록 | PASS | 검증 로그 |
| 공식 보고서·Obsidian 리뷰 | PASS | 본 문서, PHASE_0_REPORT.md |

### 테스트하지 못한 항목

- FHD(1920×1080) 레이아웃 — Phase 0 범위 밖, `미검증`
- 다른 PC/클린 OS에서의 Export 실행 — `미검증` (이 PC의 임시 폴더 복사본에서만 확인)

## E. 발견된 문제

### 발생한 오류

- `Result<T>`의 readonly 필드 대입 컴파일 오류, `Environment` 모호성(Godot vs System), xUnit2029 분석기 오류, 감지기 테스트 기대값 오류
- Godot Export: "A solution file is required for projects with C# files"
- 헤드리스 실행에서 `OS.GetCmdlineArgs()`가 `--` 뒤 인자를 포함하지 않음
- 문서 이동 중 `git stash` 사용으로 작업 트리 줄바꿈이 일시 변경됨

### Codex 독립 리뷰 지적 (CHANGES REQUIRED, 사용자 전달 내용 기준)

1. [High] Godot Client가 Domain을 직접 참조하고 아키텍처 테스트도 허용, Presenter의 Domain 사용을 검출 못 함
2. [High] Godot 4.7.2 Mono Windows Export Template이 설치 경로에 없어 신규 Export 실패
3. [Medium] 루트/Client 솔루션이 Release에서 Client를 Debug로 매핑
4. [Medium] Simulation 결정론 검사가 `new Random ()`, `DateTime.Today`, `Guid.NewGuid()`, `File.ReadAllText()` 등 우회·누락을 놓침
5. [Low] `game/StarPlayer.Client.csproj.old` 임시 백업 파일

(Codex 원문 보고서는 전달받지 않았고, 위 항목은 사용자의 수정 지시에 적힌 내용입니다.)

### 해결한 문제

- 위 오류는 모두 수정. Export는 클라이언트 전용 .sln 추가, 인자는 `GetCmdlineUserArgs()`도 읽도록 수정.
- Codex 1: Client csproj에서 Domain 참조 제거, `DiagnosticsService.Run()`이 Application DTO(`DiagnosticReport`)를 반환하도록 변경, 아키텍처 테스트 강화(컴파일된 Client DLL의 어셈블리 참조 검사로 전이 참조 우회 검출, 소스 검사 확장). 변조 2종(Domain 타입 직접 사용 / 네임스페이스 없이 Application 경유 사용)이 모두 검출됨을 확인 후 원복.
- Codex 2: 템플릿 폴더를 치워 같은 실패를 재현 → 공식 TPZ SHA-512 검증 후 `tools/install-export-templates.ps1`로 복구(복구본과 기존 파일 바이트 동일) → 신규 Export 성공. 이 PC에서는 수정 시작 시점에 템플릿이 존재했으며 리뷰 환경에서 없었던 원인은 `미확인`.
- Codex 3: 두 솔루션의 Release 매핑을 Release로 수정(Godot용 솔루션에는 ExportDebug/ExportRelease 추가, Debug 유지). `verify.ps1`에 B2/B3/E1b 추가.
- Codex 4: `SourceScan` 기반 결정론 탐지기와 테스트 47건(검출 32, 오탐 방지 15) + 실제 Simulation 소스 스캔. 변조(Simulation에 `File.ReadAllText`, `Guid.NewGuid()`, `new Random ()`)가 검출됨을 확인 후 제거.
- Codex 5: `.old`는 Godot의 csproj 백업(SDK 버전·TargetFramework 줄만 차이)임을 확인하고 삭제, `.gitignore`에 `*.csproj.old` 추가.
- 줄바꿈: 커밋된 blob과 원본 파일(Downloads / .codex 첨부)로 복원하고 `SOURCE_MANIFEST.md`의 SHA-256, 크기, 문서 검증기로 원본 바이트 일치를 재확인.

### 미해결 문제

- 남은 `미검증`/`미실시`: FHD, 다른 PC Export, Codex 재검토(`미실시`). 새 Export의 수동 클릭 테스트는 사용자가 완료했다고 전달(세부 미전달).
- 리뷰 환경에서 Export Template이 없었던 원인 `미확인`.
- `Star/환영합니다!.md`(Obsidian 환영 노트)는 사용자가 의도적으로 삭제한 파일임을 확인 — 복원하지 않음.

### 기술 부채 및 향후 영향

- 한글 글꼴이 번들되지 않음(시스템 폰트 대체에 의존), exe 아이콘/버전 리소스 없음, 폴더형 Export — Phase 28–29에서 정리 가능
- 로컬 `core.autocrlf=true` 환경: 이후 원본 소스 파일을 건드리지 말 것

관련 이슈 문서는 만들지 않았다(독립 추적이 필요한 버그 없음).

## F. 설계 변경

| 변경 내용 | 변경 이유 | 영향 모듈 | 결정 문서 | Codex 추가 검토 |
|---|---|---|---|---|
| 클라이언트 전용 `.sln` 추가 | Godot Export 요구사항 | game | [ADR-001](../../docs/decisions/ADR-001.md) | 미실시 |
| Client의 Domain 직접 참조 제거, `DiagnosticsService`가 `Result<T>` 대신 DTO 반환 | Codex 지적 1: UI→Application 경계 | game, Application | [ADR-001](../../docs/decisions/ADR-001.md) (Codex remediation) | 재검토 미실시 |

공식 설계 문서(ROADMAP 등)의 기획 내용은 변경하지 않았다.

## G. AI 리뷰

### GPT Chat 리뷰

| 항목 | 결과 |
|---|---|
| 기능명세 충족 여부 | 항목별 세부 의견은 전달되지 않아 미기재 |
| 누락된 요구사항 | 항목별 세부 의견은 전달되지 않아 미기재 |
| 개선 권장사항 | 항목별 세부 의견은 전달되지 않아 미기재 |
| 최종 검토 의견 | **승인 완료** (사용자가 2026-10-10 전달) |

### Codex 리뷰 (선택적)

| 항목 | 결과 |
|---|---|
| 아키텍처 적합성 | `CHANGES REQUIRED` (사용자 전달) — 5건 수정 완료, GPT Chat이 수정 결과를 검토해 승인. **수정 후 Codex 재검토는 미실시** |
| 모듈 간 의존성 | Client→Domain 직접 참조 지적(수정함), 재검토 미실시 |
| 중요 로직 검증 | 해당 없음(Phase 0에는 게임 로직 없음) |
| 구조적 위험 요소 | Release 매핑, 결정론 검사 약점, 템플릿 누락, 백업 파일 — 수정함, 재검토 미실시 |

## H. 다음 Phase 인수인계

### 시작 전 확인 사항

- 소유자의 Phase 1 구현 지시 (Phase 0 리뷰는 승인 완료)
- `docs/toolchain.md`의 경로(`E:\Madegame\tools\godot\...`) 또는 `STARPLAYER_GODOT` 환경 변수

### 의존 관계

- Phase 1은 Domain의 ID/Result 규약과 Application 포트 구조를 그대로 사용

### 주의할 문제 및 후속 작업

- Infrastructure 이외 계층에 SQL/Godot 참조를 추가하면 `ArchitectureTests`가 실패함
- SQLite 연결은 `Foreign Keys=True`를 명시할 것

### 추가 검증 필요 사항

- Codex 재검토, 다른 PC에서의 Export 실행, FHD 레이아웃 (Phase 1 시작은 소유자의 별도 지시 필요)

## I. 최종 결과

| 항목 | 결과 |
|---|---|
| 완료 기준 충족 여부 | Codex 지적 수정 후 자체 검증 PASS(`verify.ps1` 종료 코드 0). 수정 이전 빌드의 수동 UI 테스트 7항목 PASS 보고와, 새 Export의 수동 테스트 완료 보고(세부 미전달)가 있음 |
| 최종 리뷰 상태 | 승인 완료 |
| 남은 문제 | 위 "미해결 문제" / 기술 부채 |
| 승인자 또는 승인 근거 | GPT Chat 최종 검토 결과 `승인 완료` (프로젝트 소유자가 2026-10-10 채팅으로 전달). 근거: Codex 지적 5건 수정 후 `tools/verify.ps1` 연속 2회 PASS, 새 Export의 사용자 수동 버튼 클릭 테스트 완료 보고. Codex 재검토는 `미실시`. |
| 최종 수정 날짜 | 2026-10-10 |

### Phase 종료 체크

- [x] ROADMAP 완료 기준과 Common Gate G 확인
- [x] 실제 구현·변경 파일과 Git 변경사항 기록
- [x] 실행한 테스트의 명령·환경·실제 결과 기록
- [x] 미검증 항목, 미해결 문제와 설계 변경 식별
- [x] 중요한 설계 변경의 결정 문서 연결
- [x] 리뷰 상태를 `검토 대기`로 변경 후, GPT Chat 최종 검토 결과 전달에 따라 `승인 완료`로 변경
- [x] [CURRENT_STATE.md](../../CURRENT_STATE.md)와 실제 상태 일치 확인
- [x] GPT Chat 전달용 간략 요약 작성 (PHASE_0_REPORT.md §6)
- [x] 다음 Phase를 자동 시작하지 않음
