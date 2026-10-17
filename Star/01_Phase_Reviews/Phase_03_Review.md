---
type: phase-review
phase: "03"
phase_name: "Seeded game simulation / Prototype 1 — 작업 패키지 3-A (RNG), 3-B1 (구역·규칙 데이터 계약), 3-B2 (확률·튜닝 계약), 3-C1 (이벤트 위치·ef2·JSON v2), 3-C2A (단일 투구 확률 분기·game/target@1)"
status: 검토 대기
started: "2026-10-11"
completed: ""
developer_ai: Claude Code
reviewed_by: ""
branch: "main"
commit: "3-C2A: 'feat: add phase 3c2a deterministic pitch branch resolver' (기준 HEAD d3570b76e93246076253681568b709d3ea1d5a3b 다음 커밋; SHA는 git log 참조 — 문서가 자기 커밋 SHA를 담을 수 없음; 3-C1은 d3570b7)"
roadmap_anchor: "phase-3--seeded-game-simulation--prototype-1"
last_updated: "2026-10-17"
tags:
  - star-player
  - phase-review
---

# Phase 03 Review — Seeded game simulation / Prototype 1 (3-A RNG만 착수)

> [!warning] 상태 규칙
> 허용 상태는 `초안`, `검토 대기`, `승인 완료`, `보완 필요`입니다. 구현 AI는 외부 검토 없이 `승인 완료`로 변경하지 않습니다. 실행하지 않은 검증은 `미검증`, 수행하지 않은 독립 리뷰는 `미실시`로 기록합니다.

> [!info] 3-F F-A/B 통합 구현 (2026-10-17, `TASK.md` "Star Player Phase 3-F F-A/B Integrated Implementation", 기준 HEAD `430f42d` = 커밋된 Game Calling v4 Closeout) — `IMPLEMENTED_WITH_NOTES` · `TESTED` · `REVIEW_PENDING` (승인 아님, 독립 리뷰 `미실시`)
> **F-A:** `tools/StarPlayer.SimHarness`(Domain + Simulation만 참조, Production `InternalsVisibleTo` 없음)가 변경 없는 엔진으로 완전한 결정적 경기를 실행한다(`prob-model-v4`/`tuning-virtual-proto-v4`, `player-input-set-v3`, `gamerules-v2` 프로필, `advancement-policy-v2`, 중립 구장). 행동만 고르고 확정은 `PitchCommitGate`/`CommitGate`/`GameCursor.Publish`가 한다. **Production 추가는 `GameStarter.Begin(GameStart, seed)` 하나**: 엔진에 경기를 시작하는 공개 경로가 없었고(기존 루프는 테스트 어셈블리의 `InternalsVisibleTo`로 내부 팩토리 사용), `GameState`가 아니라 `GameStart`를 받아 Reducer로 상태를 만들므로 위조 불가(`GameStarterBoundaryTests` 3개). 기본 정책 `normal-balanced-v1`(Normal·Balanced, Take·교체 없음, 합법성 증명용). Event Budget·정체 감지·취소·예외는 `Failed`/`Cancelled`로 끝나며 GameId·Seed·엔진 오류 코드·마지막 영수증 12건을 보존하고, 연장 한도 합법 무승부는 정상 종료다.
> **F-B:** F1~F8을 확정 커밋의 이벤트·사실(`PlateAppearanceCompleted`)만으로 집계(`MetricsRecorder`), 목표는 튜닝 파일에서 읽음, BABIP=(H-HR)/(AB-SO-HR+SF), 스윙률은 HBP 투구 제외, Zone율은 **실제** 위치 기준. `BoxScoreBuilder`와 15항목 교차 검증(경로 A=이벤트 집계, 경로 B=BoxScore; 단 둘 다 Reducer의 타석 결과 분류에서 출발하므로 독립성은 집계 규칙과 이벤트 수준 3개 검사에 있음). 손으로 합산한 독립 Oracle 2종(두 번째는 공개 Reducer로 손으로 구동한 실제 경기), 4건 수식 훼손 점검 전부 검출. Manifest·Summary·Replay(`config_hash`·`run_result_hash`; 시각·환경·git은 `environment_not_hashed`로 Hash 제외).
> **코호트·판정:** 중립 코호트(모든 능력치 50, 우투우타)는 `matchup.handedness`에 중립 키가 없어 `same_hand` 보정이 모든 투구에 적용된다(손잡이 무관 코호트는 불가능). Virtual League는 확장 지점만 있고 능력치 분포·전략 혼합비를 정하지 않았다. 최소 표본·평가 코호트가 미승인이라 **공식 판정은 전부 `NOT_EVALUATED`**(분모 0은 `INSUFFICIENT_SAMPLE`), 중립 결과를 F1~F5의 근거로 쓰지 않는다. 튜닝 파일의 `targets[]`는 코호트 필드가 없고 설명이 F1 "neutral inputs" / F2~F5 "league average"로 달라 재해석 없이 보고만 했다.
> **테스트:** 신규 SimHarness 86 + Simulation 3 + Infrastructure 1. 전체 xUnit Release **2,427 PASS**(Domain 327, Application 127, Simulation 741, Infrastructure 1,146, SimHarness 86; 0 FAIL, 0 SKIP, 종료 코드 0). 스모크 3경기(시드 3·11·29)·10경기(시드 1~10, 연장 1경기 포함, 약 1.6초, 최대 작업 집합 61MB; 벤치마크 아님). 모델·튜닝 v1~v4, Golden, `ef1`/`ef2`, RNG, Commit/CAS 파일 변경 없음. 기존 테스트 삭제·Skip·약화 없음.
> **미완료·미검증:** 100,000 PA, 100경기, 8개 목표의 공식 판정, 최소 표본, Virtual League 분포, 전략 혼합, 공식 Seed 목록, 지배 전략 검사("declared test matrix"가 어디에도 없음 = `NOT_SPECIFIED`), Pitch Zone Lite 점검 목록(ROADMAP 문장은 **12개** 절 — 이전 "11개"는 정정됨, 아래 부록), Codex 재검토·전체 Mutation·`tools/verify.ps1` 실행 `미실시`(`verify.ps1`에는 Codex F-03 수정으로 SimHarness.Tests `U5`가 추가됨, 실행은 안 함). 기존 2026 KBO 공식 RuleProfile은 미검증(프로필은 `fictional_test`). Phase 4 저장 미구현. F-C 미착수·미승인, 3-F·Phase 3 Gate `INCOMPLETE`. 상세: [보고서](../../docs/reports/PHASE_3F_FA_FB_IMPLEMENTATION_REPORT.md) · [game-engine §34](../../docs/game-engine.md) · [결정 노트](../02_Decisions/2026-10-17_Phase_3F_FA_FB_Harness.md) · ADR-007 부록.

> [!info] 3-E Game Calling v4 (GC-A/B/C) 통합 구현 (2026-10-17, `TASK.md` "Star Player Phase 3-E Game Calling v4 Integrated Implementation", 기준 HEAD `131dcac` = 커밋된 Framing v3) — `IMPLEMENTED` · `TESTED` · `REVIEW_PENDING` (승인 아님)
> 소유자 정책 GC1~GC10과 추가 계약 GC-S1·GC-S2(Codex `CODEX_PHASE3_E_GC_ARCH_REVISIONS_REQUIRED` 수용; 검토 원문은 저장소에 없어 인용하지 않음)를 GC-A → GC-B → GC-C 순서로, 각 Gate 통과 후 구현했다. **GC-A:** `prob-model-v4` + `tuning-virtual-proto-v4`(해시 `5011f8ac…b8fd8`, 독립 Python 계산과 일치; v1 `5b70dca7…3ea5`·v2 `7f02cb4f…dd70`·v3 `682a5f06…e389a` 불변, 기존 Fixture/Golden 30개 바이트 동일), `ModifierConditionScope.BorderlineTarget`(목표 구역이 Borderline 12구역일 때만 `catcher.game_calling` 적용; Clear 5구역은 `Applied=false`·계수 1,000,000; `StageProbability.Compute` 내부에서 강제, 미정의 조합·비구역 조건은 Fail-closed), 중립 50·1,000ppm/점·계수 950,000~1,050,000, Control×GC 곱셈 결합(0/0 878,750 · 0/100 971,250 · 50/50 1,000,000 · 100/0 1,021,250 · 100/100 1,128,750; **중복은 제거된 것이 아니라 Combined Cap으로 제한**), Model/Tuning 명시적 대응표(v1/v1~v4/v4, 잘못된 12조합 거부), 독립 Oracle 714건(306 기본 조합 + 408; Clear/Borderline 분류는 Oracle이 기하 규칙으로 직접 도출). **GC-B:** `PlayerInputSet` 콘텐츠 v3(`GameCalling`·`PitcherControl`, 독립 계산 리터럴 3개; v1·v2 ID 불변), `FrozenCatcherInput` 확장(Framing 필수 = NonAutomated, GameCalling 필수 = v4 모든 모드)과 `FrozenPitcherInput`; **GC-S1(GC-F01):** v4 Control의 권위 = `GameState`의 현재 투수 + 경기에 고정된 콘텐츠 v3 — `Prepare`가 동결하고 시트 Control 불일치를 RNG 이전에 거부(`pitcher_control_mismatch`), `CheckPrepared` 6d와 Commit이 재도출(`pitcher_control_changed`·`player_inputs_changed`·`pitcher_changed`…), Prepared/Audit/Trace 값은 기대값이 아님, 경기 중 새 Control 자동 채택 없음; **GC-S2(GC-F02):** `CommitEvidence.LinkActualLocation`이 실제 입력으로 target/location 분포·Trace를 재계산하고 H0 비공개 복사본에서 기록된 추첨을 재생(`PitchRollReplay`, 스냅샷으로만 생성·게임 Draw 아님)해 Roll·RawDraws·Outcome·H1을 대조 — **같은 Outcome 구간 안의 Roll 변조도 CAS 이전에 거부**(실제 반례 테스트), 모든 위조 시도 후 Aggregate·Revision·Cursor·RNG·LastCommit 불변. 새 Stage·Stream·Draw 없음(`game/location@1` 유지).
> **GC-C:** 15개 통합 시나리오 전체 경기(v1 · v2 · v3 · v4 Automated/NonAutomated · Control×GC 모서리 · 전체 활성), 중립 v4 = v3 = v2 영수증 일치(버전 메타데이터만 상이, 전체 영수증 바이트 비교는 하지 않음), 독립 PCG32 참조 구현 대조(402개 시작 위치, 거절 추첨 6건 포함). 전체 xUnit Release **2,333 PASS**(Domain 327, Application 127, Simulation 738, Infrastructure 1,141; 0 FAIL, 0 SKIP, 종료 코드 0; 신규 159건), 의도적 기존 테스트 변경 6메서드(어서션 9줄, 약화 없음; `docs/decisions/ADR-007.md`에 목록), Sabotage 10건 모두 검출·원본 복구(SHA-256 확인; `CheckLink` 직접 테스트로 RawDraws·H1 가드의 중복을 분리). **Phase 3-E 구현 Gate 자체 판정: `PHASE3_E_IMPLEMENTATION_GATE_PASS_PENDING_COMMIT`(구현자 판정, 독립 검토 `미실시`; ROADMAP 3-E의 "기존 포수 능력·Battery 이중 계산 없음"은 연결된 슬롯 기준의 제한적 PASS이며 Blocking·Pop Time·WP/PB·도루 저지는 Phase 3 리졸버에 연결되어 있지 않음).** **미구현·미실시:** `battery.chemistry`(Phase 7, Reserved), 약점 공략 AI, 동적 유효 Control, v1~v3 커밋의 Roll 재생(소급 변경 금지), 3-F, Phase 4 영속화(콘텐츠 버전+내용+ID 함께 저장 필요), 실제 Runner, KBO 공식 RuleProfile 외부 근거(미검증), Codex·전체 Mutation·`verify.ps1`·대규모 시뮬레이션. Phase 3 Gate `INCOMPLETE`, GitHub Push 금지. 상세: `docs/reports/PHASE_3E_GAME_CALLING_V4_REPORT.md`, `docs/game-engine.md` §33, `docs/decisions/ADR-007.md`, `Star/02_Decisions/2026-10-17_Game_Calling_V4.md`.

> [!info] 3-E Framing v3 Package A+B 통합 구현 (2026-10-16, `TASK.md` "Star Player Phase 3-E Framing v3 Integrated Implementation", 기준 HEAD `b64d3e9`; 이후 로컬 커밋 `131dcac`, 푸시 안 함 — 2026-10-17 정정) — `IMPLEMENTED` · `TESTED` · `REVIEW_PENDING`
> 소유자 정책 E1~E7과 Codex 아키텍처 판정 `CODEX_PHASE3_E_ARCH_PASS_WITH_NOTES`(지시서가 명시; 검토 원문은 저장소에 없어 인용하지 않음)를 구현했다. **A:** `prob-model-v3` + `tuning-virtual-proto-v3`(해시 `682a5f06…e389a`, 독립 Python 계산과 일치; v1 `5b70dca7…3ea5`·v2 `7f02cb4f…dd70` 불변), `ModifierConditionScope`(All | NonAutomatedBorderline)로 NonAutomated + Borderline(12구역)에서만 `catcher.framing` 적용(Automated·Clear 5구역은 `Applied=false`, 계수 1,000,000), 중립 50·1,000ppm/점·계수 950,000~1,050,000, called_pitch 키 엄격 해석, Model/Tuning 명시적 대응표(v1/v1·v2/v2·v3/v3), `GameExecutionRules` 다중 버전·선택 모델 기준 구장 범위. **B:** `PlayerInputSet` 콘텐츠 v1(ID 불변·독립 계산 리터럴) / v2(`ReceivingFraming`, 고정 null 토큰), `FrozenCatcherInput`(Prepare에서 실제 포수 ID·Rating 동결, 첫 Draw 전 Typed Error, 50 대입 없음), `CheckPrepared` 6c(포수 교체·Rating 변경·다른 게임/Set), `CommitEvidence.LinkCalledPitch`(실제 규칙 모드·위치·상태·Set에서 called_pitch 순수 재계산, 감사 포수 ID 불신). 새 Stage·Stream·Draw 없음.
> 전체 xUnit Release **2,174 PASS**(Domain 316, Application 127, Simulation 728, Infrastructure 1003; 0 FAIL, 0 SKIP), 독립 Oracle 238건(102 기본 조합 + 136), 전체 경기 파이프라인 회귀(중립 포수 = Model v2 경기, 영수증 일치), Sabotage 18건 중 17건 검출·복구(미검출 1건은 `CheckPrepared`가 먼저 막는 중복 방어선과 동등한 변이이며 game-engine §32에 분석; 둘 다 제거하면 검출). 의도적 기존 테스트 변경 6건(`docs/decisions/ADR-007.md`에 목록). **미구현·미실시:** Game Calling·battery.chemistry, 3-E 전체 Gate(`INCOMPLETE`), 3-F, Phase 4 영속화(콘텐츠 버전·내용·ID 함께 저장 필요), 실제 Runner, KBO 공식 RuleProfile 외부 근거(미검증; 테스트 프로필을 공식으로 표기하지 않음), Codex·Mutation·`verify.ps1`. Phase 3 Gate `INCOMPLETE`. 상세: `docs/game-engine.md` §32, `docs/decisions/ADR-007.md`.

> [!info] 3-D Park / Fielding / Advancement Policy v2 통합 구현 (2026-10-16, `TASK.md` "Star Player Phase 3-D Park / Fielding / Advancement v2 Integrated Implementation", 기준 HEAD `054704d`; 이후 로컬 커밋 `b64d3e9`, 푸시 안 함) — `IMPLEMENTED` · `TESTED` · `REVIEW_PENDING`
> 소유자 결정 P1~P4(구장), R1~R5(수비·주자·송구·입력 오류), S1~S3(불변 입력·스냅샷 보존·버전/RNG)을 구현했다. Codex 아키텍처 검토 판정 `CODEX_PHASE3_D_ARCH_REVISIONS_REQUIRED`는 지시서가 명시한 것이며 검토 원문은 저장소에 없으므로 인용하지 않고 소유자 승인 계약으로만 기록했다. **A. Park:** `prob-model-v2`·`tuning-virtual-proto-v2`(해시 `7f02cb4f…dd70`, 독립 Python 계산과 일치) 신설, v1·해시 `5b70dca7…3ea5` 불변. ADR-006의 "새 튜닝 버전이면 충분"은 코드상 부족해(`Reserved`는 모델 속성, 입력 범위 1~3000이 결합 상한과 양립 불가) 모델 v2가 필요했음을 ADR-006 Addendum에 기록. 구장 3개 슬롯 활성(800~1250‰, 중립 1000, 1,000ppm/‰), `park.hits`는 모델의 단타 전용 가중치, 도메인 허용 3000‰ 같은 범위 밖 값은 경기 시작 전 거부(Clamp 없음). `GameExecution`이 `GameStart`→`GameState`→`FinalizedGame`에 불변 보존(레거시=null=기록 없음·중립, 활성 모델은 중립 구장도 명시적 스냅샷), Start·Restore·Resolver에서 검증, `PreviewComparison`에 포함, `Finalize`가 복사. **B. Fielding:** 불변 `PlayerInputSet`(선수 기본 Fielding/Speed/Arm, 식별자=정규 텍스트 SHA-256)과 `PreparedPitch`에 묶인 `PitchFieldingInputs`(유격수=땅볼·라인, 중견수=뜬공, 2루수=팝업; 기록상 풋아웃 선수와 같다고 주장하지 않음), `CheckPrepared` 6b가 CAS 전에 집합 식별과 현재 수비수·능력치를 다시 증명(CAS는 GameState만 보호). **C. Advancement:** `advancement-policy-v2`(대주자 반영 현재 주자 Speed + Retriever Arm을 1회 `Compute`·1회 정규화, 4개 맥락, 감사 기록·보살 크레딧 일치 검증), v1 불변. 새 Stage·Stream·Draw 없음, H1/H2·맥락당 1회 추첨 유지. 테스트: 전체 xUnit Release **2,036 PASS**(신규 129: Domain 5·Simulation 30·Infrastructure 94, 0 FAIL/SKIP), 독립 Oracle(수비 900·진루 784 케이스), 6개 조합 전체 경기 통합 검증(중립 v2 = 레거시와 사건 단위 동일), 로컬 Sabotage 15/15 검출·복구. 의도적 기존 테스트 변경 6건(v2 미지원 버전 리터럴 3건, 필드 목록 2건, P-08 정적 확인 1건)은 game-engine §31에 기록. **한계:** Phase 4 영속화(`GameExecution` 6개 값·선수 입력 집합 내용)·실제 경기 Runner·포지션 숙련도/컨디션·가상 수치의 균형(3-F)·3-E·3-F 미착수; Codex·Mutation·`verify.ps1`·대규모 시뮬레이션 `미실시`. 이 문서의 최상위 status는 `검토 대기` 유지, Phase 3 Gate `INCOMPLETE`. 상세: [game-engine §31](../../docs/game-engine.md), [ADR-007](../../docs/decisions/ADR-007.md) "Decisions (Park / Fielding / Advancement v2)", [ADR-006](../../docs/decisions/ADR-006.md) Addendum.

> [!info] 3-D BatterTendency v2 통합 구현 (2026-10-15, `TASK.md` "Star Player Phase 3-D BatterTendency v2 Integrated Implementation", 기준 HEAD `ee9756a`) — `IMPLEMENTED_WITH_NOTES` · `TESTED` · `REVIEW_PENDING`
> 소유자 D-T1~D-T6 승인 반영: DataPack `format_version: 2`(선택 `batter_zone_tendencies[]`, 희소·원본 순서 보존, v1 완전 호환·v1+신규 필드 거부, 생략/`[]` 형태 보존), 불변 `BatterZoneProfiles`(Domain, 누락=중립·손상=오류), `PlayerWorld.BatterTendencies`·`Format`(내부 보관), `PitchPreparation.PrepareFromProfiles`(GameState의 현재 타자 PlayerId → 기존 `CheckSheets` → 조회 → 기존 `Prepare`), HiddenTendencyBoundary 강화(정확한 두 지정 위치만 예외)·SourceScan 확장, RNG 계약 정정(성향은 stage·추첨을 추가하지 않으나 결과가 달라지면 후속 stage·총 호출 수가 달라질 수 있음). 신규 테스트 64건(Domain 14 · Application 10 · Infrastructure 40), 기존 테스트 어서션 수정 1건(`format_version: 2` 미지원 → 3/0 미지원), 전체 xUnit **1,907 PASS**(Domain 277 · Application 127 · Simulation 697 · Infrastructure 806; Release, FAIL 0, SKIP 0, 종료 코드 0). 골든·튜닝 해시·`ef1`/`ef2` 불변, 공식 v1 팩 불변. 로컬 sabotage 6건 모두 검출 후 복구. **한계:** 실제 경기 Runner·Phase 4 영속화(프로필 데이터 재현)·구장 `Hits`·수비/주자 보정·3-E·3-F 미착수; Codex·Mutation·`verify.ps1`·대규모 시뮬레이션 `미실시`. 이 문서의 최상위 status는 `검토 대기` 유지, Phase 3 Gate `INCOMPLETE`. 상세: [game-engine §30](../../docs/game-engine.md), [ADR-007](../../docs/decisions/ADR-007.md) "Decisions (BatterTendency v2)".

> [!info] 3-D 능력치 보정 연결 (2026-10-15, `TASK.md` "Star Player Phase 3-D Ability Modifier Wiring", 기준 HEAD `a73ef54`) — `IMPLEMENTED` · `TESTED` · `REVIEW_PENDING`
> 투구 판정 9개 슬롯(control·eye·approach·count_pressure·stuff·contact·handedness·velocity·power)을 연결했다. 임시 정책 D-ABILITY-1(유효 능력치 = 현재 기본 능력치, 상태 미적용)과 D-ABILITY-2(3볼·<2스트라이크 three_balls, 2스트라이크·<3볼 two_strikes, 3-2 neutral)는 소유자 승인의 임시 단순화이며 최종 밸런스가 아니다. 새 stage·난수·튜닝 버전·저장 형식 없음. 새 독립 오라클(Python) 포함 신규 테스트 31건, 전체 xUnit 1,843 PASS(Release). 미해결: 타자 성향 데이터 공급, 구장 `Hits` 계수, 수비/주자/송구 슬롯, 3-E·3-F. 이 문서의 최상위 status는 `검토 대기` 유지, Phase 3 Gate `INCOMPLETE`. 상세: [game-engine §29](../../docs/game-engine.md), [ADR-007](../../docs/decisions/ADR-007.md).

> [!info] 3-D-1 코스 성향 연결 (2026-10-15, `TASK.md` "Star Player Phase 3 Next Development", 기준 HEAD `53805aa`) — `IMPLEMENTED` · `TESTED` · `REVIEW_PENDING`
> `swing_result`·`batted_ball`에 `TendencyContext`를 연결했다(선택 입력 `BatterTendency`, null이면 중립). 난수·이벤트·영속화 변경 없음. 신규 테스트 8건, 전체 xUnit 1,812 PASS(Release). 미해결: 타자 성향의 저장 위치(소유자·아키텍처 결정), 구장 `Hits` 계수 소유자 결정, 능력 보정 입력, 3-E·3-F. 이 문서의 최상위 status는 `검토 대기` 유지, Phase 3 Gate `INCOMPLETE`. 상세: [game-engine §28](../../docs/game-engine.md), [ADR-007](../../docs/decisions/ADR-007.md).

> [!info] 3-C3-C 검증 결과 · Closeout 문서 통합 (2026-10-15, `TASK.md` "Phase 3-C3-C Final Closeout Documentation & Evidence Integration", 기준 HEAD `52cab03` — 문서만)
> C3-C 기술 검증은 수행 완료, Closeout 문서는 작성·최종 정합화 완료(2026-10-15), **Codex 독립 리뷰는 `CODEX_C3C_REVIEW_PASS_WITH_NOTES`**(BLOCKER 0 / MAJOR 0 / MINOR 1; 이 리뷰 판정은 C3-C Closeout 문서·코드 검토에 한정). 이 문서의 최상위 status는 **`검토 대기` 유지** — Phase 3 전체의 최종 승인(`승인 완료`)이나 구현 종료가 아니다(3-D 이후 미착수, `PHASE_3_GATE_INCOMPLETE`).
> - C3-C-1/2 테스트: 신규 63건(C3-C-1 `03c8675` 33, C3-C-2 `52cab03` 30), TC-01~11·TC-14~15. TC-12/13 `UNDEFINED — NOT SCORED`. E4 Race 11건 × 5회 PASS(종료 코드 0 ×5).
> - Mutation 36건 실행(C3-C-3B): 34 KILLED / 2 SURVIVED — C3-A 원본 19, C3-A 재정의 3(A-M9R/A-M10R/A-M13R), C3-B 원본 12 KILLED + B-M7 생존, A-M25 특별 검증 생존. A-M9/A-M10 원본 적용 불가(36건에 미포함), A-M26·A-M10R2 미실행. 신규 63건의 추가 검출력 0.
> - 전체 회귀(C3-C-3C, 격리 Clone): xUnit 1,804 PASS / FAIL 0 / SKIP 0 / EXIT 0, `verify.ps1` Run 1·2 `GATE: PASS`(22 PASS + 2 INFO, 회차 간 일치).
> - 증거 보존: D5(트랜스크립트·사전 감사, 4/4 SHA 일치), E4(원본 16개, `E4_NO_HISTORICAL_SHA256_BASELINE`), 3B 매니페스트 275개·3C 매니페스트 96개 해시 일치 — 모두 저장소 밖 접근 제한 보관, Git에는 요약·해시만.
> - Codex 독립 리뷰(소유자 전달 결과 기록): MINOR 1 = 보고서의 "이 보고서 외 공식 문서 9개" → "이 보고서 포함 총 9개"(기존 7개 수정 + 신규 2개), **정정 완료**. 필수 Production·테스트 수정 0. Codex는 Production 코드·테스트를 독립 검토했으나 비공개 Mutation Patch·TRX·원본 로그는 열람하지 못했다(`REPORTED_AND_ATTESTED_NOT_INDEPENDENTLY_READ`); Claude Code의 기존 해시 검사는 유효하며 Codex가 원본을 검증했다고 주장하지 않는다. 이 리뷰 기록은 Codex 결과의 전달 기록이며 GPT Chat 승인이나 `승인 완료`가 아니다.
> - 기술 NOTE(유지): B-M7 `SURVIVED / LIKELY_REDUNDANT_GUARD / NOT_FORMALLY_PROVEN`(정상 경로 중복 방어 논거는 타당, 비정상 상태 동등성 미증명), A-M25 `SURVIVED / UNRESOLVED`(OD-3 잠정 비차단, TC-14/15 PASS는 해결 근거 아님), TC-14 Production `IsComplete` 병행 호출(별도 완료 조건·필드 검사도 있어 순환 검증은 아님), Reducer Oracle 재사용, TC-04/05 양쪽 게시 순서 결정적 검증(통계적 경쟁 증거 아님), Seed 상한 20,000/60,000/3,000 및 생성 실패 skip 경로, CAS 직후 취소 직접 주입 부재, Race 승자 분포 미기록, TC-12/13 `UNDEFINED — NOT SCORED`. 절차 NOTE: NuGet `NETWORK_ACCESS_UNVERIFIED`, Godot 공유 사용자 경로 쓰기.
> - 모델 예외: 소유자 2026-10-15 "모델 다른거 사용은 상관없음" → `MODEL_DEVIATION_OWNER_ACCEPTED`(이전 `OWNER_ACCEPTANCE_PENDING`은 역사적 상태; 재실행 없음, 다른 NOTE 면제 아님).
> - 다음: 로컬 Commit은 **소유자 결정 대기**(미실시). C3-C 최종 Commit 미실시. GitHub Push 금지. Phase 3 Gate `INCOMPLETE`.
> - 문서: [PHASE_3C3C_REPORT](../../docs/reports/PHASE_3C3C_REPORT.md), [game-engine §27.21](../../docs/game-engine.md), [ADR-007](../../docs/decisions/ADR-007.md), [결정 기록](../02_Decisions/2026-10-15_C3C_Closeout.md), [3C3B 정정 Addendum](../../docs/reports/PHASE_3C3B_REPORT.md).

> [!info] 3-C3-B 구현 완료 기록 · C3-C-0 문서 Closeout (2026-10-14, `TASK.md` "Phase 3-C3-C-0 문서 Closeout 및 검증 증거 보완", 기준 HEAD `8ed806f` — 문서만)
> C3-B 프로덕션 구현은 **완료 / PASS WITH NOTES / 로컬 커밋 `8ed806f`**(푸시 안 함)로 기록한다. 기술 검토 `PASS WITH NOTES`는 소유자 전달 내용이며 검토 문서 원문은 저장소에 없다. 이 문서의 최상위 status는 **`검토 대기` 유지**(Phase 3 전체 검토 완료 아님), **Phase 3 Gate `INCOMPLETE`**, `승인 완료`로 바꾸지 않았다. 이번 단계에서 코드·테스트·verify·Mutation을 실행하지 않았고 스테이징·커밋·푸시·Codex 호출을 하지 않았다.
> - 사후 Closeout 보고서: [PHASE_3C3B_REPORT](../../docs/reports/PHASE_3C3B_REPORT.md) — 과거 원본의 재현이 아니며 증거 유형(RAW_AVAILABLE / SUMMARY_ONLY / MISSING / DERIVED_VERIFICATION)을 항목별로 표시.
> - 보존한 원본: Priority verify Run 1/2 로그·stdout, 트리 매니페스트 2개와 생성 스크립트(SHA-256 대조 일치). 두 verify는 Commit 이전 `654b5d9` dirty 트리에서 실행됐고 Commit 이후 verify는 없다. 현재 트리 해시 재계산은 매니페스트와 경로+해시 차이 0건(DERIVED).
> - 원본 없음: 전체 xUnit 단독 원본(프로젝트별 수는 verify 로그에 있음, 합계 1,741), Authority 67/77 실행 원본, C3-B 변이 13건 중 12건 검출·M7 생존 원본.
> - C3-C 인계: M7·M25 잠정 비차단 NOTE, C3-A 원래 변이 21건 최종 트리 미재실행, M9/M10/M13 재정의, C3-B 변이 재정의·재실행, Codex Medium 리뷰 필요. 소유자는 OD-1~OD-4를 승인했으나 **승인은 수행이 아니다**(C3-C 통합 테스트·변이 재실행 `NOT STARTED / NOT AUTHORIZED`).
> - 문서: [game-engine §27.19–27.20](../../docs/game-engine.md), [ADR-007](../../docs/decisions/ADR-007.md), [결정 기록](../02_Decisions/2026-10-13_C3_Design_Approval.md).

> [!info] (이전 상태) 3-C3-B 세부 계약 B-01~B-07 승인 반영 (2026-10-13, `TASK.md` "Phase 3-C3-B 설계 승인 문서 반영", 기준 HEAD `18ed60e` — 문서만, 구현 승인 아님)
> 소유자가 C3-B 세부 계약 **B-01~B-07을 일괄 승인**했다(날짜는 소유자의 채팅 승인에 근거). D-1~D-8·A~F·O-1~O-9와 C3-A 코드·경계 테스트는 불변. **C3-B 프로덕션 / C3-C는 `NOT STARTED / NOT AUTHORIZED`, Phase 3 Gate `INCOMPLETE`**, 이 문서의 최상위 status는 `검토 대기` 유지. `src/`·`tests/` 불변, 스테이징·커밋·푸시 없음(푸시는 Phase 3 최종 Gate 후 별도 승인), Codex 독립 검토 `미실시`.
> - 핵심: 대안 A(`PitchCommitGate` → 내부 불변 `CommitEvidence` → 공통 `CommitGate` 코어 → `StagedCommit` → 기존 `GameCursor.Publish`), O-2 순서 불변(7a `CheckPrepared`·7b 원본 연결·10a 구조적 미리보기·10b RNG 증거), 비인플레이·기본 인플레이는 `NextRng` 동일 인스턴스, 해결 인플레이는 감사 H0/H1/H2·`game/runner@1` 대조, 스트림별 `RawDraws` 증분(호출 수와 구분), `GameState`/`GameStep` 전 멤버 비교와 5개 타입 속성 목록 고정 테스트, 신규 오류 5개, `PendingAdvancement` 확정 불가, 해시 일치는 생성 경로 증명 아님.
> - C3-A 상태: 구현 완료 / GPT Chat 기술 검토 `PASS WITH NOTES` / 로컬 커밋 `18ed60e`. C3-C 인계: M25 생존, 원래 변이 21건 최종 트리 미재실행.
> - 문서: [game-engine §27.18](../../docs/game-engine.md), [ADR-007](../../docs/decisions/ADR-007.md), [결정 기록](../02_Decisions/2026-10-13_C3_Design_Approval.md).

> [!info] 3-C3-A 구현 완료·검토 대기 (2026-10-13, `TASK.md` "Phase 3-C3-A 프로덕션 구현 지시서", 기준 HEAD `7456f44`) — 승인 아님
> `AuthoritativeGame`·`CommitRecord`·`StagedCommit`·`CommitGate.Stage`·`GameCursor.Publish`를 승인된 계약(O-1~O-9)대로 구현했다. 신규 67 테스트(Finalize 교체 훅 제거로 69에서 2건 삭제), 전체 xUnit 1,580 통과(실행 결과), 결함 주입은 원래 26건 중 21건 최초 트리 결과·5건 재실행(M25 생존)·신규 M26 검출(보고서 §15·§25). **GPT Chat 기술 검토 `GPT_CHAT_TECHNICAL_PASS_WITH_NOTES`(소유자 전달; 이 문서의 최상위 status는 `검토 대기` 유지)**, C3-C 확인 사항: M25 방어 분기 커버리지 한계, `verify.ps1` 결과는 [보고서](../../docs/reports/PHASE_3C3A_REPORT.md) §16. **C3-B / C3-C / Phase 4는 `NOT STARTED / NOT AUTHORIZED`, Phase 3 Gate `INCOMPLETE`**, 이 문서의 최상위 status는 `검토 대기` 유지, GPT Chat·Codex 검토 `미실시`, 스테이징·커밋·푸시 안 함.

> [!info] 3-C3-A 세부 계약 O-1~O-9 승인 반영 (2026-10-13, `TASK.md` — 문서만, 구현 승인 아님)
> 소유자가 C3-A 세부 계약 **O-1~O-9를 일괄 승인**했다(날짜는 소유자의 채팅 승인에 근거). D-1~D-8·계약 A~F는 불변. **C3-A 프로덕션 / C3-B / C3-C는 `NOT STARTED / NOT AUTHORIZED`**, 이 문서의 최상위 status는 `검토 대기` 유지, **Phase 3 Gate `INCOMPLETE`**. `src/`·테스트·Reducer·RNG·`ef1`/`ef2`·ROADMAP 불변, 스테이징·커밋·푸시 없음, Codex 독립 검토 `미실시`.
> - 핵심: 오류 `Commit.*`/`Authoritative.*`(기존 `Id.*`·`Rng.*`·`Game.*` 통과), 검증 순서 대안 C, 취소는 `OperationCanceledException`(CAS 이전만), C3-A는 `ApproachEvent`·`RunnerPlayEvent`·`SubstitutionEvent`만 확정(투구 3종은 `event_kind_not_supported`), 독립 `Revision`(Apply+Finalize = 하나의 Publish = +1), EventId 순번 정확히 6자리·최대 999999, 손상·불완전 종료 이력 복원 거부(영수증 검증은 ID·소속·연속성·형식까지, 지문↔본문 일치는 검증 불가), 최소 `CommitRecord`.
> - 정정: `event_id_in_use` 등 4개 코드는 `CheckPrepared`가 아니라 후보 빌더 소속. 열린 항목: C3-B 구조 비교 픽스처, Phase 4 `CommitRecord`·`GameStatDelta` 영속 필드, ROADMAP 3-F 라벨(소유자).
> - 문서: [game-engine §27.16](../../docs/game-engine.md), [ADR-007](../../docs/decisions/ADR-007.md), [결정 기록](../02_Decisions/2026-10-13_C3_Design_Approval.md).

> [!info] 3-C3 설계 승인 반영 (2026-10-13, `TASK.md` — 설계만, 구현 승인 아님)
> 소유자가 C3 설계 결정 **D-1~D-8과 추가 계약 A~F를 일괄 승인**했다(승인 날짜 2026-10-13은 프로젝트 소유자의 채팅 승인에 근거하며 `TASK.md` 수정 시각은 승인 증거가 아님). **C3-A / C3-B / C3-C 및 C3 프로덕션 구현은 `NOT STARTED / NOT AUTHORIZED`**, 이 문서의 최상위 status는 `검토 대기` 유지, **Phase 3 Gate `INCOMPLETE`**. `src/`·테스트·Reducer·RNG·`ef1`/`ef2`·ROADMAP 불변, 스테이징·커밋·푸시 없음, C3 설계에 대한 Codex 독립 검토 `미실시`.
> - 핵심: `AuthoritativeGame`(명시적 `LastEventSequence`·단조 `Revision`), 새 EventId = 마지막 확정 순번 + 1, 모든 공식 이벤트 단일 Stage/Publish, `GameReducer.Apply` 재실행 결과 채택(미리보기는 구조적 비교용), 종료 이벤트의 `Finalize` 동일 Stage·동일 Publish, 참조 CAS, RNG 증분(RawDraws) 검증(해시 일치는 경로 증명 아님), 비인플레이 `(PreparedPitch, Candidate)` 쌍, 불변 `CommitRecord`, 영속화는 Phase 4 인수인계, PA 구동기는 3-F 하네스(D-4).
> - ROADMAP 차이(수정 안 함): D-4의 3-F 하네스 배정은 Phase 3 작업 3·4와 Gate에는 맞지만 3-F 라벨 문구와 다르다 — 소유자 조정 대상.
> - 문서: [game-engine §27](../../docs/game-engine.md), [ADR-007](../../docs/decisions/ADR-007.md), [결정 기록](../02_Decisions/2026-10-13_C3_Design_Approval.md). 아래 3-C2B-3 블록의 "3-C3 미착수"는 구현 기준으로 유효하다.

> [!success] 3-C2B-3 기술 승인 — Git 반영 전 (2026-10-12, `TASK.md` 최종 검토 후속)
> 작업 패키지 3-C2B-3은 **Codex Medium 독립 집중 리뷰 `PASS WITH NOTES`**(Critical/High/Medium 0), **GPT Chat 기술 검토 `PASS WITH NOTES`**를 받았다(두 판정은 분리 기록, 사용자 전달; GPT Chat은 전체 테스트를 직접 실행하지 않았고 1,513건은 Claude Code 실행 결과). 승인 범위는 **3-C2B-3 기술 검토에 한정**되며 **Git 반영(스테이징·커밋·푸시) 전**이다. **3-C3 NOT STARTED / NOT AUTHORIZED, Phase 3 Gate INCOMPLETE.** 이 문서의 최상위 status는 `검토 대기` 유지. Low 문서 불일치 2건(M15 변형 검사 분류, ADR-007 테스트 수 1,511 → 1,513 및 단계 A 21 → 23)을 정정했다([보고서 §21](../../docs/reports/PHASE_3C2B3_REPORT.md)). 프로덕션 코드·테스트·원시 증거는 수정하지 않았다. 아래 "검토 대기"·"미실시" 표현은 승인 이전 시점의 기록이다.
>
> [!note] 3-C2B-3 구현 완료 — 검토 대기 (2026-10-12, `TASK.md`, 기준 HEAD `ab7147b`)
> 작업 패키지 3-C2B-3(`advancement` 해결기)은 구현·자체 검증을 마쳤고 **`검토 대기`**입니다(승인 아님). 소유자가 승인한 P-01~P-10을 코드로 옮겼고 정책은 바꾸지 않았다. GPT Chat 검토 `미실시`, Codex 독립 검토 `미실시`(권장), 3-C3·Phase 4 미착수, **Phase 3 전체 Gate INCOMPLETE**, 이 문서의 최상위 status는 `검토 대기` 유지, 스테이징·커밋·푸시 안 함. 자체 판정 `PASS WITH NOTES`(외부 승인 아님).
> - **구현:** `AdvancementTypes.cs`·`AdvancementRules.cs`·`AdvancementPlanning.cs`·`AdvancementEventBuilder.cs`(신규), `InPlayCandidateTypes.cs`(감사 선택 필드 `AdvancementDetail`, 후보 생성자 1개), `InPlayPitchPlanning.cs`(공용 검증 `VerifyApplied` 추출, 동작 불변). `AdvancementPlanning.Resolve`는 `CheckPrepared`를 가장 먼저 재사용하고, H1을 개인 사본으로 복원해 맥락마다 `game/runner@1` 1회를 항상 추첨하며(`Void`·`Hold` 포함), 선행 주자 우선 순차 조건부 합성 → 명시 `Sequence` → 실제 `InPlayResult` 이벤트 → 변경 없는 `GameReducer.Apply` → Reducer 파생 공식 기록 대조 → `InPlayPitchCandidate`(`NextRng` = H2)를 돌려준다.
> - **검증(실제 실행):** 신규 59 테스트(단계 A 23, 단계 B 36); RED 20 실패→GREEN 23/23, RED 35 실패/1 통과→GREEN 36/36; 전체 xUnit 1,513 통과(기준 1,454 + 59); 원시 행동 벡터 5,562건(합법 4,722 / 불법 840 / 불법 중 Reducer 수락 480) 전수, 득점+제3아웃 318건은 선행 우선 순서에서만 득점 인정; 결함 주입 변이 25건 + 무변경 대조군 1건: 행동 수준 검출 24건, M15는 첫 유효 실행에서 생존 후 오류 메시지 검증으로만 검출(메시지 수준, 보고서 §21), 대조군은 의도대로 생존, `verify.ps1` 연속 2회 `GATE: PASS`(로그 `PHASE_3C2B3_VERIFY_LOG_run1/2.md`, Claude 실행, 최종 트리).
> - **NOTES:** 3-C3 인수인계(H2·ID·영수증·상태 채택, 확정 직전 권위 RNG(H0)·실제 순번 재검사, 번호 건너뜀 감지 불가), KBO 2026 인증, 능력 연결(별도 계약)·보정(3-F), 감사 영속화(Phase 4), 선행 주자 득점이 항상 먼저라는 네 개 맥락 한정 가정. 일부 테스트는 RED 없이 첫 실행부터 통과(Reducer·규칙 위임 검증).
> - 문서: [보고서](../../docs/reports/PHASE_3C2B3_REPORT.md), [ADR-007](../../docs/decisions/ADR-007.md), [game-engine §26](../../docs/game-engine.md), [해석 기록](../02_Decisions/2026-10-12_C2B3_Resolver_Interpretations.md), [RED A](../../docs/reports/PHASE_3C2B3_RED_A_raw.txt), [RED B](../../docs/reports/PHASE_3C2B3_RED_B_raw.txt), [변형 검사](../../docs/reports/PHASE_3C2B3_MUTATION_raw.txt).

> [!note] 3-C2B-3-P 추가 진루 정책 검증 수행 — 승인 대기 (2026-10-12, `TASK.md`, 기준 HEAD `f669589`)
> 정책 검증 단계이며 **구현이 아니다**: `src/` 불변, 3-C2B-3·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE, GPT Chat·Codex 검토 `미실시`, 이 문서의 최상위 status는 `검토 대기` 유지, 커밋·푸시 안 함. 자체 판정 `POLICY_READY_FOR_REVIEW`(승인 아님).
> - **검증(테스트 전용):** 보류 1,314건 재구성(열린 결정 2개는 270건), 원시 행동 벡터 5,562건을 실제 Reducer에 적용(합법 4,722 수락·일치 / 불법 840 중 480 수락), D3 A/B 비교(시도 결합 시 분포 동일), 사건 순서 민감도 318건, 끝내기 8사례, 선수 능력 `MISSING_INPUT`, 실제 `PreparedPitch` RNG 핸드오버(H1→H2) 4건. 신규 테스트 64, 전체 xUnit 1,451 통과(기준 1,387), 변이 검사 9/9(첫 라운드 생존 2건 이력 보존).
> - **정책 P-01~P-10: 최초 제출 시 모두 `PROPOSED`(현재는 아래 FIX2 참조: P-05만 `OWNER APPROVED`).** 승인 필요 핵심: P-05 사건 순서(득점 인정 여부를 뒤집음), P-08 선수 능력 연결 범위, P-03 D3(A/B), P-07 고정 소비.
> - 문서: [검증 보고서](../../docs/reports/PHASE_3C2B3_POLICY_VALIDATION_REPORT.md), [정책 결정 초안](../../docs/reports/PHASE_3C2B3_POLICY_DECISION_DRAFT.md), [구현 인수인계 초안](../../docs/reports/PHASE_3C2B3_IMPLEMENTATION_HANDOFF_DRAFT.md).

> [!note] 3-C2B-3-P Codex 리뷰 수정 (2026-10-12) — 재검토 `미실시`, `FIX_READY_FOR_REVIEW`(정책 승인 아님)
> - FIX-1: `Resolve` 입력(GameState·H0 RNG·현재 타자/투수·튜닝·마지막 발급 순번)과 최신 상태 검증 계약을 인수인계 §6에 정의(`CheckPrepared` 재사용, H0/H1/H2 경계, EventId·영수증 충돌). 실제 Pending 기반 반례 테스트 3건 추가, 전체 xUnit 1,454 통과(직접 실행).
> - FIX-2: P-05를 프로토타입 순서 정책 v1로 재정의(당시 `OWNER_APPROVAL_REQUIRED` — 이 시점 기록, FIX2에서 정정). `src/` 불변.

> [!note] 3-C2B-3-P FIX2 (2026-10-12) — 문서 계약 정정, 재검토 `미실시`, `FIX2_READY_FOR_REVIEW`(승인 아님)
> - 이력: 최초 Codex `CHANGES REQUIRED`(보존) → FIX-1 기술 계약 보완 → **P-05 소유자 승인 2026-10-12** → 재검토 Codex가 문서 상태 오류·P-06 이벤트 타입 오류 지적 → FIX2.
> - FIX2-1: **P-05 `Prototype Sequence Policy v1` = `OWNER APPROVED`**(기존 네 개 맥락 한정, 선행 주자 우선·명시 `Sequence`, 실제 관측 순서·KBO 인증 아님, 시간 기반 모델로 교체 가능). **P-01~P-04·P-06~P-10은 `PROPOSED` 유지, C2B-3-P 전체는 최종 승인 전**, 3-C2B-3 프로덕션·3-C3 미착수, Phase 3 Gate INCOMPLETE. 소유자 승인 ≠ Codex 기술 PASS.
> - FIX2-2: P-06을 "이벤트 입력 `InPlayResult`(뜬공 아웃은 `Out`) vs Reducer 파생 `BatterResult.SacrificeFly`"로 정정(`GameEvents.cs:26`에 `SacrificeFly` 없음, 파생은 `GameReducer.Plays.cs:383`). `Apply` 뒤 이벤트 수정 금지.
> - 검증(직접 실행): 핸드오프 7/7, Simulation 정책 60/60, 전체 xUnit 1,454 통과. `src/`·테스트·증거 불변. [검증 보고서 §19](../../docs/reports/PHASE_3C2B3_POLICY_VALIDATION_REPORT.md).

> [!note] 3-C2B-3-P 최종 정책 승인 반영 (2026-10-12) — 구현 승인 아님, `POLICY_DOCUMENTATION_READY`(자체 판정; 승인 아님)
> - **P-01~P-10 전부 `OWNER APPROVED`**(P-05는 먼저 별도 승인·보존, 나머지는 일괄 승인; 사용자가 채팅으로 기록 확인). 위 FIX2 블록의 "다른 정책 `PROPOSED` 유지"는 그 시점의 기록이다.
> - 상태: C2B-3-P 정책 소유자 승인 완료 / 구현 전 문서 정합성 점검. **3-C2B-3 프로덕션 `NOT STARTED / NOT AUTHORIZED`, 3-C3 `NOT STARTED`, Phase 3 Gate `INCOMPLETE`.**
> - 구분: 소유자 정책 승인 ≠ GPT Chat FIX2 보고서 기반 `PASS WITH NOTES`(사용자 전달, 독립 확인 안 함) ≠ Codex 독립 검토(최초 `CHANGES REQUIRED`·FIX1/FIX2 이력 보존, FIX2 이후 Codex PASS 기록 없음 `미실시`).
> - 반영 문서: 정책 결정 초안·인수인계 초안·[검증 보고서 §20](../../docs/reports/PHASE_3C2B3_POLICY_VALIDATION_REPORT.md)·[ADR-007](../../docs/decisions/ADR-007.md)·[game-engine §25](../../docs/game-engine.md)·SYSTEM_CONTRACTS C08·CURRENT_STATE. `src/`·테스트·이전 증거 불변.

> [!success] 3-C2B-2 최종 승인 완료 (2026-10-12)
> 작업 패키지 3-C2B-2는 **Codex Medium 집중 재검토 `PASS`**, **GPT Chat 최종 `PASS`**를 받았다. 승인 범위는 **3-C2B-2에만 한정**되며 Phase 3 전체 완료가 아니다(3-C2B-3·3-C3·Phase 4 미착수, **Phase 3 전체 Gate INCOMPLETE**). 이 문서의 최상위 status는 규칙에 따라 `검토 대기`로 유지한다. Commit 진행은 승인됐으나 실제 Commit/Push 결과는 Git 기록이 권위이며 Push는 미승인이다.
> - 출처 구분: Codex Medium 집중 재검토 `PASS`는 사용자가 ChatGPT 대화에 전달한 독립 리뷰 보고이고, GPT Chat 최종 `PASS`(C2B-2 작업 패키지 한정 최종 승인 및 Git Commit 승인)는 GPT Chat이 실제로 내린 판정이며(2026-10-12 사용자 채팅으로 확인), 전체 xUnit·verify는 Claude Code 실행 결과이다.
> - 근거: 기존 19개 인플레이 템플릿 구현, 양의 확률 45개 조합 검증, 기본 경기 1,080건·끝내기 가능 상황 4,320건 검증, 5,400건 독립 오라클 비교 불일치 0, `PendingAdvancement` 1,314건 분리 유지, 끝내기 공식 기록 축소 192건 검증, D5 결승 득점자 감사 계약 보완(`InPlayWinningRunner`), H0/H1/H2 RNG 계약 정정, 전체 xUnit 1,387 PASS(Claude Code 실행), `verify.ps1` 연속 2회 `GATE: PASS`(Claude Code 실행), Codex Medium 집중 재검토 `PASS`, GPT Chat 최종 `PASS`.
> - 테스트 증거 구분: Codex가 직접 실행한 관련 테스트는 Simulation 87건 + Infrastructure 57건 = 144건 PASS(사용자 전달 보고, 이번 문서 작업에서 재실행하지 않음)이고, Claude Code가 실행한 전체 테스트는 1,387건 PASS이다. GPT Chat·Codex가 로컬 전체 테스트를 독립 실행한 것이 아니며 두 수치는 합산하지 않는다.
> - 수정 이력: 과거 Codex `CHANGES REQUIRED`(Medium 1: 타자주자 결승 득점자 식별 불가, Low 1: RNG 문서 충돌)는 삭제하지 않고 수정 이력으로 보존한다(보고서 §17).
> - 미해결: C2B-3의 복수 주자 Advancement 계약 F-1; `PendingAdvancement` 후속 판정 F-2; 추가 진루 이후 공식 기록 재계산 U-2; C3의 원자적 확정 및 EventId 재검사; KBO 2026 공식 규칙 인증 미완료; 원정팀 승리 제3아웃 상황의 `WinningRunner` null 전용 명명 테스트 미추가(비차단 NOTE).
> - 문서: [보고서 §17–18](../../docs/reports/PHASE_3C2B2_REPORT.md), [ADR-007](../../docs/decisions/ADR-007.md), [game-engine §24](../../docs/game-engine.md).
> - 아래 3-C2B-2 "구현 완료 — 검토 대기" 기록의 "검토 대기"·"승인 아님"·"미실시"·"스테이징·커밋·푸시 안 함" 표현은 승인 이전 시점의 기록이며 자체 판정과 증거 한계는 그대로 보존한다.

> [!note] 3-C2B-2 구현 완료 — 검토 대기 (2026-10-12, `TASK.md`, 기준 HEAD `34c2028`)
> 작업 패키지 3-C2B-2(기본 인플레이 계획·`walkoff-credit-v1`·`PendingAdvancement` 격리·인플레이 후보)는 구현·자체 검증을 마쳤고 **`검토 대기`**입니다(승인 아님). GPT Chat 검토 `미실시`, Codex 검토 `미실시`(자동 진행 안 함). 3-C2B-3·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE, 이 문서의 최상위 status는 `검토 대기` 유지, 스테이징·커밋·푸시 안 함.
> - **구현:** `InPlayTemplateCatalog`(19 템플릿·승인 45 조합), `InPlayBasePlanner`(주자 이동·맥락 키), `InPlayOfficialCredit`(끝내기), `InPlayCandidateTypes`(`BaseInPlayPlan`·`InPlayPitchCandidate`·`PendingAdvancement`·`InPlayAudit`), `InPlayPitchPlanning`(`PlanBase`/`Build`/`Verify`); `PitchPreparation`은 준비 상태 검사를 `CheckPrepared`로 추출(코드·순서 불변).
> - **검증(실제 실행):** 5,400건 필드 단위 일치(불일치 0; 일반 780/300, 끝내기 3,114/1,014/192), 신규 172 테스트, 주입 결함 32/32 검출, 전체 xUnit 1,377 통과(기준 1,205 +172), `verify.ps1` 연속 2회 `GATE: PASS`(로그 `PHASE_3C2B2_VERIFY_LOG_run1/2.md`). RNG 추가 소비 0·EventId 커서 불변·GameState/영수증 불변·취소 7단계 테스트 통과.
> - **Codex 리뷰 수정(2026-10-12):** Medium(타자주자 결승 득점자 식별 불가)·Low(RNG H0/H1/H2 문서 충돌) 수정. 전체 xUnit 1,387 통과(신규 10), verify 2회 `GATE: PASS`. 증거: `docs/reports/PHASE_3C2B2_REPORT.md` §17. (이 수정 시점의 '재검토 미실시'는 이후 Codex 재검토 PASS로 대체됨: 위 최종 승인.)
> - **NOTES:** F-1/F-2(3-C2B-3 필수), U-2, `WinningRunner` null(타자 결승) — Codex Medium 지적으로 `BatterRunner` 종류 구분 수정(2026-10-12, 보고서 §17, 재검토 `미실시`), 보류는 커서 미사용, 오라클 동일 작성자, KBO 미인증, 3-F `미실시`. 자체 판정 `PASS WITH NOTES`.
> - 문서: [보고서](../../docs/reports/PHASE_3C2B2_REPORT.md), [ADR-007](../../docs/decisions/ADR-007.md), [game-engine §24](../../docs/game-engine.md), [RED](../../docs/reports/PHASE_3C2B2_RED_raw.txt), [변형 검사](../../docs/reports/PHASE_3C2B2_MUTATION_raw.txt).

> [!note] 3-C2B-2-P 인플레이 정책 확정·템플릿 실현성 검증 — GPT Chat `PASS WITH NOTES`(검증 한정) (2026-10-12, `TASK.md`)
> 소유자가 승인한 D1–D5 프로토타입 정책을 ADR-007·game-engine §23·SYSTEM_CONTRACTS C08에 기록하고, 모든 양의 확률 인플레이 조합이 실제 `GameReducer`에서 합법인지 테스트 전용 도구로 검증했다. **프로덕션 코드(`src/`)는 변경하지 않았고 3-C2B-2 구현은 미착수다.** GPT Chat 최종 검토 `PASS WITH NOTES`(3-C2B-2-P 정책·템플릿 실현성 검증에 한정; 수치는 Claude Code 제출 실행 결과이며 GPT Chat·Codex가 로컬 신규 테스트 전체를 독립 실행한 것이 아님), Codex 독립 리뷰 `미실시`(자동 진행 안 함), 3-C2B-3·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE, 커밋·푸시 안 함. 이 문서의 최상위 status는 `검토 대기` 유지.
> - **상태 구분:** D1–D5 소유자 정책 = 승인; 세부 템플릿 실현성 검증 = GPT Chat `PASS WITH NOTES`(구현 승인 아님); 3-C2B-2 구현 = 미착수; 3-C2B-3 = 미착수; 3-C3 = 미착수; Phase 3 전체 Gate = INCOMPLETE.
> - **검증(실제 실행):** 60개 명목 조합 = 양의 확률 45 + 확률 0 15; 템플릿 19개; 일반 1,080건·끝내기 4,320건 전부 실행(거부 0, 결함 0, 독립 Python 오라클 일치); 분류 일반 LEGAL_BASE 780 / PENDING_ADVANCEMENT 300, 끝내기 LEGAL_BASE 3,114 / PENDING_ADVANCEMENT 1,014 / NEEDS_WALKOFF_POLICY 192; 손으로 정한 대표 10건 일치; 주입 결함 12종 검출; 신규 29 테스트, 전체 xUnit 1,205 통과(Domain 263, Simulation 412, Application 117, Infrastructure 413; 기준 1,176에서 +29), 실패 0; `verify.ps1` 연속 2회 `GATE: PASS`(로그 `PHASE_3C2B2P_VERIFY_LOG_run1/2.md`).
> - **발견·NOTES:** F-1 복수 주자 `advancement` 맥락 미정(3-C2B-3 설계), F-2 일반 인플레이 27.8%가 보류라 3-C2B-3 필수, F-3 수비수 고정 선택 통계 편향·병살/야수선택/희생 없음, F-5 인필드 플라이는 템플릿 전제로만 구분, KBO 2026 조항 미인증(OBR 임시 근거), 오라클은 C# 코드와 작성자·정책 해석 공유. 자체 판정 `PASS WITH NOTES`(GPT Chat이 같은 판정으로 확인). 기본 이벤트 5,400건 중 LEGAL_BASE 3,894 / PENDING_ADVANCEMENT 1,314. 추가 미해결: 후보 API의 RNG 0회 추가 소비·EventId 불변·취소/실패 불변성 미검증, 3-C3 공동 원자성 미구현. 기존 자체 검토·과거 Codex `CHANGES REQUIRED` 기록은 유지.
> - 문서: [ADR-007](../../docs/decisions/ADR-007.md) 3-C2B-2 절, [game-engine §23](../../docs/game-engine.md), [보고서](../../docs/reports/PHASE_3C2B2_POLICY_REPORT.md), [템플릿 매트릭스](../../docs/reports/PHASE_3C2B2_TEMPLATE_MATRIX.md), [원시 결과](../../docs/reports/PHASE_3C2B2_FEASIBILITY_RAW.json).

> [!success] 3-C2B-1 최종 승인 (2026-10-11, 소유자 `TASK.md`)
> 작업 패키지 3-C2B-1은 **GPT Chat `PASS WITH NOTES` / 최종 승인**, **독립 Codex 리뷰 `PASS WITH NOTES`**(신규 Critical/High/Medium 결함 없음)를 받았다. 승인 범위는 **3-C2B-1 비인플레이 후보 생성에만 한정**되며 3-C2B-2/3·3-C2B 전체·Phase 3 전체 완료가 아니다(3-C2B-2/3·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE). 이 문서의 최상위 status는 규칙에 따라 `검토 대기`로 유지한다.
> - 근거(소유자 기록 인용, 이번 문서 작업에서 재실행하지 않음): 전체 xUnit 1,176 PASS(Claude 실행), Codex 직접 실행 Release 63/63 PASS, `verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, Codex 로그 확인), 3-C2A Golden Vector 유지, 기존 RNG·`ef1`/`ef2`·`GameReducer` 계약 유지.
> - 유지 NOTES: `PlayerSheet`가 권위 있는 공급원에서 왔는지 확인하는 책임은 호출 경계; 마지막 발급 EventId 순번은 3-C3에서 최종 재검사; `GameState`/RNG/ID/영수증 공동 원자성은 3-C3; M16 변형 검사 생존(선행 ID 충돌 검사로 도달 불가능한 `WasDuplicate` 방어 분기); 변경된 `HiddenTendencyBoundaryTests`의 Codex 독립 신규 빌드는 NuGet 접근 문제로 `미실시`; 인플레이 후보 생성·주자 이동·수비 템플릿·`advancement` 미구현; 끝내기 장타 공식 기록 정책 미확정; Phase 3-F 통계 보정 `미실시`.
> - 문서: [보고서 §16](../../docs/reports/PHASE_3C2B1_REPORT.md), [ADR-007](../../docs/decisions/ADR-007.md) 3-C2B-1 절.
> - 아래 3-C2B-1 "구현 완료 — 검토 대기" 기록의 "검토 대기"·"승인 아님"·"미실시" 표현은 승인 이전 시점의 기록이며 자체 판정과 증거 한계는 그대로 보존한다.

> [!note] 3-C2B-1 구현 완료 — 검토 대기 (2026-10-11, `TASK.md`)
> 작업 패키지 3-C2B-1(준비된 투구 + 비인플레이 이벤트 후보)은 구현·자체 검증을 마쳤고 **`검토 대기`**입니다(승인 아님). GPT Chat 검토 `미실시`, Codex 독립 검토 `미실시`(자동 진행 안 함). 3-C2B-2/3(설계 미확정)·3-C3·Phase 4는 미착수, Phase 3 전체 Gate는 INCOMPLETE. 이 문서의 최상위 status는 `검토 대기`로 유지한다. 3-C2A 최종 승인 기록은 아래에 그대로 보존한다.
> - **구현:** `src/StarPlayer.Simulation/Baseball/PitchPreparation.cs`(신규). `Prepare`는 현재 타자·투수 `PlayerSheet`의 ID를 상태와 대조하고 `Player.Bats`/`Player.Throws`로 기존 `PitchBranchResolver.Resolve`를 실제 호출, 외부에서 생성·변경할 수 없는 `PreparedPitch`를 만든다. `BuildNonInPlayCandidate`는 경기 ID·RNG 맥락·시작 `StateHash`·타자/투수·투타 방향·규칙/튜닝/확률 모델·동일 `GameState` 객체를 재확인하고 다섯 분기를 `PitchEvent`/`TakeEvent`(`ef2`)로 바꾸며 별도 작업용 `GameEventIds`로 후보 ID를 발급, 기존 `GameReducer.Apply`로 원본 상태에서 검증한다. 인플레이는 `in_play_not_supported_yet`, 추가 RNG 0, 상태·RNG·ID 커서 불변.
> - **변경 파일:** `PitchPreparation.cs`(신규), `PitchPreparationTests.cs`(신규), `HiddenTendencyBoundaryTests.cs`(신규 타입 2개를 시그니처 가드에 추가), 문서(보고서·ADR-007·game-engine §22·SYSTEM_CONTRACTS C08·CURRENT_STATE·이 리뷰·대시보드). `GameReducer`·`PitchBranchResolver`·이벤트/JSON/RNG/튜닝/Golden은 변경 없음.
> - **테스트(실제 실행):** 신규 63 테스트; 스텁 대비 RED 60 실패/3 통과; 변형 검사 유효 18건 중 17건 검출, 1건 생존(M16: 사전 ID 충돌 검사 때문에 도달 불가한 `WasDuplicate` 방어 분기); 전체 xUnit 1,176 통과(Domain 263, Simulation 383, Application 117, Infrastructure 413; 기준 1,113에서 +63), 실패 0; `tools/verify.ps1` 연속 2회 `GATE: PASS`; 3-C2A 기존 테스트·Golden 불변. 원시 로그와 상세는 [보고서](../../docs/reports/PHASE_3C2B1_REPORT.md) §11~12.
> - **NOTES:** 위 M16 생존; `PreparedPitch`의 상태 객체 동일성은 단기 메모리 가드일 뿐 Phase 4 무결성·3-C3 공동 원자성을 대신하지 않음; 신뢰 가능한 `PlayerSheet` 공급은 호출자 책임; 3-C3는 후보의 ID를 바꾸지 않고 채택해야 함; 인플레이·`advancement`·주자 이동·수비 기록·타석 자동 진행 미구현(3-C2B-2/3, 3-C3); 통계 보정 3-F.
> - 문서: [ADR-007](../../docs/decisions/ADR-007.md) 3-C2B-1 절, [game-engine §22](../../docs/game-engine.md), [보고서](../../docs/reports/PHASE_3C2B1_REPORT.md).

> [!success] 3-C2A 최종 승인 (2026-10-11, 소유자 `TASK.md`)
> 작업 패키지 3-C2A는 **GPT Chat `PASS WITH NOTES` / 최종 승인**, **Codex 최종 집중 재검토 `PASS WITH NOTES`**를 받았다. 기존 Codex `CHANGES REQUIRED`(Medium 1)는 해결·재검증됐고 새 Critical/High/Medium 결함은 없다(아래 판정과 수정 이력 보존). 근거(소유자 기록 인용): 전체 xUnit 1,113 PASS(Claude 실행), Codex 직접 실행 Release 92/92 PASS, `verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행·Codex 로그 확인), 독립 Python 골든 검증 PASS, 3-A 골든 바이트 불변, `game/target@1` 독립성·정상 분기 RNG 호출 수·원본 GameState/RNG 스냅샷 불변성 확인. **승인 범위는 3-C2A뿐**이며 3-C2 전체·Phase 3 전체 완료가 아니다: 3-C2B·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE. 이 문서의 최상위 status는 규칙에 따라 `검토 대기`로 유지한다.
> - **비차단 NOTES(유지):** 수정 전 RED 9 실패/75 통과는 원시 로그 없이 요약만 확인됨; 변형 검사 15건 원시 출력의 독립 검토 한계; 분기 중간 오류·늦은 취소 추가 검증; 실제 타자·투수와 요청 입력 연결(3-C2B); 초안→합법 `GameEvent` 변환(3-C2B); `advancement`·주자 이동 검증(3-C2B); 이벤트 ID·영수증·RNG·`GameState` 공동 원자성(3-C3); DB 트랜잭션·RNG 기대 해시(Phase 4); 100,000 PA / 100경기 통계 보정(3-F).
> - 문서: [보고서 §15](../../docs/reports/PHASE_3C2A_REPORT.md), [ADR-007](../../docs/decisions/ADR-007.md).
> - 아래 "검토 대기"·"미실시"·"Commit/Push 안 함" 표현은 승인 이전 시점의 기록이다.

> [!note] 3-C2A 구현 완료 — 검토 대기 (2026-10-11, `TASK.md`)
> 작업 패키지 3-C2A(단일 투구 확률 분기 + `game/target@1`)는 구현·자체 검증을 마쳤고 **`검토 대기`**입니다(승인 아님). GPT Chat 검토 `미실시`, Codex 독립 검토 `미실시`(자동 진행 안 함). 3-C2B·3-C3·Phase 4는 미착수, Phase 3 전체 Gate는 INCOMPLETE. 이 문서의 최상위 status는 `검토 대기`로 유지한다.
> - **구현:** 일곱째 스트림 `game/target@1`(투구마다 `NextBounded(1,000,000)` 1회, `game/location@1`보다 먼저; 알고리즘·`kd1`·`rngset1`·3-A Golden Vector 불변)과 `PitchBranchResolver.Resolve`: 목표 구역 → 실제 위치(`PitchLocation.FromZone`) → 타자 결정 → 콜 또는 스윙 결과 → 타구 → 수비 결과를 각각 계약된 스트림에서 정확히 1회 추첨(도달하지 않은 단계는 추첨 없음, 확률 100% 단계도 추첨, `advancement`는 3-C2B). 스위치 타자는 투수 투구 손의 반대쪽(투구마다, RNG 없음), 명시적 Take는 일반 타자 결정(HBP 질량 유지) 뒤 콜만(스윙·타구·수비 없음). 결과는 `PitchBranchDraft`이며 `GameEvent`·`GameState`가 아니다.
> - **테스트(실제 실행):** 전체 xUnit 1102 통과(Domain 263, Simulation 383, Application 117, Infrastructure 339; 기준 1021에서 +81), 실패 0. 기대값은 독립 Python 오라클(28 시나리오·55 투구, 거부 샘플링 벡터). 스텁 대비 RED 69 실패/12 통과, 변형 검사 15건 모두 검출. `tools/verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, 로그 `PHASE_3C2A_VERIFY_LOG_run1/2.md`).
> - **NOTES:** 스트림 계약은 자체 RED 실행이 없음, 변형 검사 원시 출력 미보존, 분기 중간 실패 주입·늦은 취소 미테스트, 오라클은 C#과 작성자·계약 해석을 공유, 해결기는 호출자가 준 타석 방향·투구 손을 상태의 타자·투수와 대조하지 않음, Take/투구 단계 규칙은 리듀서와 코드 공유 없이 재진술, 통계 보정 미실시(3-F).
> - **3-C2B/3-C3 인계:** 합법 `GameEvent` 변환(수비 기록·진루·아웃 순서·제3아웃·끝내기·실책 귀속), `advancement` 추첨, 이벤트 ID·`ef2` 영수증, 새 데이터의 위치 필수화, 타석 방향·투구 손의 라인업 연결, 타석 자동 진행, 상태+RNG+ID+영수증 공동 원자성, Phase 4 트랜잭션·RNG 기대 해시.
> - 문서: [ADR-007](../../docs/decisions/ADR-007.md) 3-C2A 절, [ADR-004](../../docs/decisions/ADR-004.md) 항목 20, [game-engine §21](../../docs/game-engine.md), [보고서](../../docs/reports/PHASE_3C2A_REPORT.md), [RED·변형 요약](../../docs/reports/PHASE_3C2A_RED_EVIDENCE.md).

> [!warning] 3-C2A Codex 지적 수정 — 검토 대기 유지 (2026-10-11, `TASK.md`)
> 3-C2A에 대해 GPT Chat 1차 검토는 `PASS WITH NOTES`, Codex 독립 검토는 **`CHANGES REQUIRED` — Medium 1건**이었다(위 3-C2A 기록과 NOTES는 보존). 최종 승인은 보류 상태이며 이번 수정 후에도 **`검토 대기`**다(`승인 완료` 아님).
> - **결함:** `PitchBranchResolver`가 `GameState.GameId`와 RNG 스냅샷 `ContextKey`를 비교하지 않아 `g001` + `game:3c2a:probe`처럼 서로 다른 경기의 입력도 성공했다. 테스트 Fixture가 GameId를 `g001`로 고정해 이를 가려 왔다.
> - **수정:** 스냅샷 복원 성공 후·첫 추첨 전에 `string.Equals(GameId, ContextKey, Ordinal)` 검사, 불일치 시 `PitchBranch.game_context_mismatch`(정규화·ID 변경·기본값 대체 없음). 정상 입력의 확률·RNG 소비·다음 스냅샷은 그대로(HBP 3 / 볼·루킹 4 / 헛스윙·파울 4 / 인플레이 6회). 기존 `Rng.*` 오류 코드 유지.
> - **Fixture·Golden:** 상태 생성 helper가 경기 ID를 명시적으로 받는다(임의 덮어쓰기 helper 없음). Phase 3-A Golden Vector와 3-C2A Golden JSON·Python 오라클은 변경하지 않았다(골든 `context`를 GameId로 사용).
> - **테스트(실제 실행):** RED 9 실패/75 통과(84), GREEN 84 통과. 전체 xUnit **1113 통과**(Domain 263, Simulation 383, Application 117, Infrastructure 350; 기준 1102에서 +11), 실패 0. `tools/verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, 로그 `PHASE_3C2A_FIX_VERIFY_LOG_run1/2.md`). `git diff --check` 오류 없음.
> - **미실시:** Codex 재검토 `미실시`(자동 진행 안 함), GPT Chat의 수정 검토 `미실시`. 3-C2B·3-C3·Phase 4 미착수, Phase 3 Gate INCOMPLETE, Commit/Push 안 함.
> - 문서: [보고서 §14](../../docs/reports/PHASE_3C2A_REPORT.md), [RED 증거](../../docs/reports/PHASE_3C2A_RED_EVIDENCE.md), [ADR-007](../../docs/decisions/ADR-007.md) 항목 13.

> [!success] 3-C1 승인 (2026-10-11)
> 작업 패키지 3-C1(이벤트 위치 payload·`ef2`·이벤트 JSON 버전 2)은 **PASS WITH NOTES / GPT Chat 최종 승인**, Codex 최종 집중 재검토 **PASS WITH NOTES**. 기존 Medium(`ReadEventsV2` 명시적 null)과 추가 중첩 컬렉션 null 오류는 해결·재검증(이전 `CHANGES REQUIRED` 판정과 수정 이력은 아래 보존). 전체 xUnit 1,021 PASS(Claude 실행), Codex 직접 실행 83 PASS(Infrastructure 61, Simulation 22), `verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, Codex 로그 확인), 30,974개 `ef1` 영수증 다이제스트·레거시 writer 바이트 유지, 신규 `ef2` 지문·충돌·JSON 호환성 확인. **이 승인은 3-C1 작업 패키지에만 적용되며 Phase 3 전체 Gate는 INCOMPLETE, 3-C2/3-C3 미착수다. 이 문서의 최상위 status는 `검토 대기`로 유지한다.** ADR-007은 3-C1 한정 Accepted. 유지 NOTES:
> - 최초 3-C1 구현의 RED 로그 부재
> - 최초 및 이후 변형 검사 원시 출력의 독립 검증 한계
> - 이벤트 외부 JSON 입력 검증의 별도 안정화
> - 3-C2: `game/target@1` 및 목표 구역 선택
> - 3-C2: 실제 투구 위치 필수화와 타자 방향 검증
> - 3-C2: `NextBounded` 호출 수와 `Draws` 구분
> - 3-C2: 명시적 `Take` 및 스위치 타자 정책
> - 3-C3: GameState·RNG·이벤트 ID·영수증 공동 원자성
> - Phase 4: DB 트랜잭션 및 복구

> [!success] 3-B2 승인 (2026-10-11)
> 작업 패키지 3-B2(확률·튜닝 계약)는 **PASS WITH NOTES / GPT Chat 승인**, Codex 최종 집중 재검토 **PASS**. 이전 Codex `CHANGES REQUIRED`(High 2, Medium 1)는 해결·재검증(이력은 아래 보존). 전체 xUnit 955 PASS(Claude 실행), Codex 직접 실행 71 PASS(Domain 31, Infrastructure 40), `verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, Codex 로그 확인). **Phase 3 전체는 승인되지 않았다 — 이 문서의 최상위 상태는 `검토 대기`로 유지, Phase 3 Gate INCOMPLETE, 3-C 미착수.** 유지하는 NOTES:
> - 3-C: target_zone 선택 방식 및 RNG 스트림 계약
> - 3-C: NextBounded 호출 수와 실제 Draws 구분
> - 3-C: 실제 투구 결과, 이벤트, ef2 및 공동 원자성
> - 3-D: 코스 성향의 실제 확률 연결
> - 3-D 이전: 구장 Hits 계수의 단타 전용 매핑에 대한 소유자 결정
> - 3-E: Framing 상한 및 실제 계산
> - 3-F: 100,000 PA, 100경기, 8개 집계 목표 검증
> - Phase 4: RNG 기대 해시 및 경기 상태·영수증의 트랜잭션 저장
> - 확률표의 이전 설계 조정 이력(목표 구역 가중치 두 차례 조정)과 독립 검증의 한계 유지

> [!success] 3-A 승인 (2026-10-11)
> 작업 패키지 3-A(RNG)는 **PASS WITH NOTES / GPT Chat 승인**, Codex 최종 집중 재검토 **PASS WITH NOTES**. 전체 xUnit 709 PASS(Claude 실행), Randomness 111 PASS(Codex 독립 재실행), `verify.ps1` 2회 `GATE: PASS`(Claude 실행, Codex 로그 확인). 마지막 비차단 노트(`GameRandomSnapshot.cs`의 오래된 주석)는 주석만 수정했고 RNG 동작·알고리즘·직렬화는 변경 없음. **Phase 3 전체 Gate는 INCOMPLETE, Phase 3-B 미착수.** 아래 `검토 대기`·`미승인`·`미실시` 표기는 승인 이전 라운드의 기록이며 3-A에 한해 이 블록이 최신이다. 승인 근거: GPT Chat 최종 판정(소유자 전달, `TASK.md`).

> [!important] 범위
> 소유자는 **Phase 3-A RNG 구현만** 승인했다(`TASK.md`). 3-B~3-F와 Phase 4는 미승인·미착수다. 이 문서는 **Phase 3 전체 Gate를 PASS로 기록하지 않는다.** Phase 3 Gate(100,000 PA, 100경기, 보정 구간, Pitch Zone Lite 검사 등)는 전부 `미검증`/INCOMPLETE다.

## 3-C1 진행 상황 (2026-10-11, `TASK.md` — 이벤트 위치 payload·`ef2`·이벤트 JSON 버전 2)

> [!warning] 2차 결함 수정 라운드 — 중첩 이벤트 목록의 null 요소 (2026-10-11)
> GPT Chat 추가 검토 **`CHANGES REQUIRED`**. 1차 수정의 탐색에서 발견된 결함: 이벤트 내부 목록(`moves`, `strikeout_credits`, `batter_credits`, 이동의 `credits`)과 시나리오 `events`의 **null 요소**가 `NullReferenceException`(`errors: [null]`은 경로 없는 `Fixture.enum_invalid`)을 일으킨다. **세 reader(레거시 `ReadEvents`, `ReadEventsV2`, 경기 시나리오 reader) 모두에서 직접 실행으로 재현**(추정 아님). 회귀 테스트 24건을 먼저 작성해 **RED 19 실패 / 5 통과**(5건은 호환성 보호) → 공통 변환 함수(`Item`·`Credits`·`Moves`·`Errors`·`Events`) 수정 → **GREEN 24/24**, 신규 변형 검사 6건 모두 검출. 오류는 `Fixture.field_missing` + 정확한 경로(`events[0].moves[1].credits[0]`, 시나리오는 `g001.events[2]` 접두사 유지), 전체 입력 실패. 목록 자체가 null인 선택 항목은 그대로 허용(`strikeout_credits: null`은 기존 의미 유지, 빈 목록으로 바꾸지 않음). 전체 xUnit 1021 통과(Domain 263, Simulation 383, Application 117, Infrastructure 258; 997에서 +24). `ef1`/`ef2`·30,974 다이제스트·Golden Vector·`GameEvents`·`GameReducer`·RNG 불변(고정 테스트 통과). `tools/verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, 로그 `PHASE_3C1_FIX2_VERIFY_LOG_run1/2.md`). **Codex 재검토 `미실시`, 3-C1 최종 승인 대기, 상태는 `검토 대기` 유지.** 유지 노트: 최초 3-C1 RED 로그 부재, 최초 변형 검사 7건 원시 로그 미검토, 이벤트 밖 null 멤버(라인업·벤치·expected, 다른 변환기) 미조사, 신규 verify 로그의 CRLF·단독 CR 후행 공백. 근거: [보고서 Part H](../../docs/reports/PHASE_3C1_REPORT.md), [RED 요약](../../docs/reports/PHASE_3C1_FIX2_RED_EVIDENCE.md). 아래 1차 수정 라운드의 "수정하지 않은 관련 노트"는 이 라운드로 해결됨.

> [!warning] Codex 결함 수정 라운드 (2026-10-11)
> 3-C1 코드에 대한 Codex 독립 리뷰 결과 **`CHANGES REQUIRED`** (Medium 1). `ReadEventsV2`가 `{"format_version":2,"events":null}`에서 `ArgumentNullException`, `"events":[null]`에서 `NullReferenceException`을 던졌다(`required`는 명시적 JSON null을 허용). 회귀 테스트를 먼저 작성해 **RED(5건 실패 / 23건 통과)** 를 확인하고 `ReadEventsV2`만 수정 → **GREEN 28/28**. null 목록·null 요소는 `Fixture.field_missing`(경로 `events` / `events[i]`)으로 전체 읽기 실패, 부분 성공·기본값 대체 없음. 전체 xUnit 997 통과(Domain 263, Simulation 383, Application 117, Infrastructure 234; 기준 985에서 +12). `ef1`/`ef2`·Golden Vector·`Location` 계약·`GameReducer`·레거시 reader/writer 불변(고정 테스트 통과). `tools/verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, 로그 `PHASE_3C1_FIX_VERIFY_LOG_run1/2.md`). **Codex 재검토 `미실시`, 3-C1 최종 승인 대기, 상태는 `검토 대기` 유지.** 수정하지 않은 관련 노트: 이벤트 내부 목록의 명시적 null(`moves: [null]`, `strikeout_credits: [null]` 등)은 `ReadEventsV2`·레거시 `ReadEvents`·시나리오 reader에서 여전히 예외(별도 지시 필요). 최초 3-C1 RED 로그 부재와 변형 검사 7건 원시 로그 미검토 노트는 그대로 유지. 근거: [보고서 Part G](../../docs/reports/PHASE_3C1_REPORT.md), [RED 요약](../../docs/reports/PHASE_3C1_FIX_RED_EVIDENCE.md).

> [!note] 상태
> 3-C1은 구현·자체 검증을 마쳤고 **`검토 대기`**입니다(승인 아님). GPT Chat 검토 `미실시`, 3-C1 코드에 대한 Codex 독립 검토 `미실시`(Codex는 3-C *아키텍처*만 검토: `READY WITH NOTES`). 이 문서의 최상위 상태는 `검토 대기`이며 Phase 3 전체 Gate는 INCOMPLETE, 3-C2·3-C3·Phase 4는 미착수다.

- **구현:** `PitchSpec`(→ `PitchEvent`/`TakeEvent`)와 `InPlayEvent`의 맨 끝에 선택적 `PitchLocation? Location = null` 추가(기존 인자 순서·기본값 불변, `InPlayEvent`는 자체가 투구 1회). 위치가 있는 이벤트는 `ef2`(`ef2|kind|<ef1 필드 그대로>|zone_model|zone|side|boundary`), 그 밖의 모든 이벤트는 `ef1` 그대로. `GameReducer.Apply`는 수정하지 않았다(영수증 버전 → 종류·지문 순서 비교가 이미 있음). 이벤트 JSON `format_version: 2`(`ReadEventsV2`/`WriteEventsV2`, 투구 이벤트에 위치 필수·엄격 검증, 비투구 이벤트에는 위치 불허)와 레거시 배열 형식 분리, 레거시 writer는 위치가 있는 이벤트를 조용히 버리지 않고 거부.
- **결정/계약:** [ADR-007](../../docs/decisions/ADR-007.md), [game-engine.md §20](../../docs/game-engine.md), [보고서](../../docs/reports/PHASE_3C1_REPORT.md). ROADMAP §1.14·C08의 "ef1 영수증에서 위치 제거" 부정확 문장을 계약 변경 없이 정정(ef1 영수증은 위치를 가진 적이 없음: ef1+위치 추가 / ef2−위치 제거 = 버전 불일치, 위치 변경 = 내용 충돌).
- **테스트(실제 실행):** `dotnet test StarPlayer.sln -c Release` 985 통과(Domain 263, Simulation 383, Application 117, Infrastructure 222; 기준 955에서 +30), 실패·건너뜀 0. `ddbc999`(3-B2)에서 계산한 두 리터럴(Phase 2 100경기 30,974개 영수증 SHA-256, 레거시 `WriteEvents` 바이트 SHA-256)이 현재 트리에서도 동일. 변형 검사 7건 모두 검출. RED 로그는 없음(구현 후 테스트 작성; 변형 검사와 HEAD 계산 해시가 대체 증거). `tools/verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, 로그 `PHASE_3C1_VERIFY_LOG_run1/2.md`).
- **기존 테스트 변경:** `EventIdentityTests`의 "모든 생성자 매개변수가 지문을 바꾼다" 테스트에 `Location` 변형을 추가하고 `InPlayEvent` 매개변수 개수 가드를 7→8로 갱신(기존 `ef1` 기대값은 변경 없음).
- **미검증/미실시:** Phase 3 Gate 전부, 3-C 해결기 흐름(3-C2), 공동 원자성(3-C3), Codex 코드 검토, GPT Chat 검토, 수동 UI·FHD(UI 변경 없음).
- **이월/주의:** 3-C2 — `game/target@1`(권장, ADR-004 개정+골든 벡터), `target_zone` 실현, `NextBounded` 호출 수와 실제 Draws 구분, 명시적 Take의 HBP 정책, 스위치 타자 타석 방향 정책, 이벤트 방향의 타자 적합성 검증. 3-C3 — 이벤트 ID·리듀서 결과·영수증·RNG 커서 공동 원자성, Phase 4 트랜잭션·RNG 기대 해시. 리듀서는 위치 없는 새 투구를 거부하지 않는다(새 데이터의 위치 필수는 v2 reader/writer와 3-C2 해결기가 담당).
- **승인자 또는 승인 근거:** 없음 — 3-C1은 미승인.

## 3-B2 진행 상황 (2026-10-11, `TASK.md` — 정수 확률·가상 튜닝 계약)

> [!warning] Codex 결함 수정 라운드 (2026-10-11)
> Codex 독립 리뷰 결과 **`CHANGES REQUIRED`** (High 2, Medium 1). ① `Distribution`이 내부 배열을 그대로 노출(형변환으로 공개 표만 바꿔 `Select`와 불일치) ② 공유 `ProbabilityModel.V1`의 중첩 목록과 `TuningProfile.Modifier().Table`이 외부에서 변경 가능(계산값이 바뀌어도 `ContentHash` 불변) ③ `"baselines": null` 입력에서 `NullReferenceException`. 세 건 모두 수정 전 코드에서 **RED 재현**(분포 `int[] [0, 600000]`, swing 320388 → 348837 ppm·해시 불변, NRE; [증거](../../docs/reports/PHASE_3B2_FIX_RED_EVIDENCE.md))한 뒤 수정했다: 공개 컬렉션 전부를 생성 시 복사한 `ImmutableArray`로 바꾸고(모델·튜닝·추적 포함, 반사 기반 감사 테스트), 리더는 필수 항목 null을 `Fixture.field_missing`으로 반환한다. 확률 모델·정규화·상한·튜닝 수치·시나리오·목표·튜닝 해시 `5b70dca7…3ea5`는 **불변**이며, 수정 전 코드에서 만든 105개 조건 전체 결과의 골든 다이제스트가 수정 후에도 동일하다. 신규 테스트 46개, Release 전체 xUnit 955 통과(909 + 46, Claude 실행). 자세한 내용은 [보고서 Part G](../../docs/reports/PHASE_3B2_REPORT.md), [ADR-006 항목 13–15](../../docs/decisions/ADR-006.md). **여전히 `검토 대기`(승인 아님), Codex 재검토 `미실시`, Phase 3 전체 Gate INCOMPLETE, Commit/Push·3-C 미착수.** 아래 본문의 테스트 수(909)와 "Codex `미실시`"는 수정 이전 기록이다.

> [!note] 상태
> 3-B2는 구현·자체 검증을 마쳤고 **`검토 대기`**입니다(승인 아님). GPT Chat 검토 `미실시`, Codex 독립 검토 `미실시`(권장: 확률 산술과 튜닝 식별은 이후 모든 리졸버 패키지의 기반). Phase 3 전체 Gate는 INCOMPLETE이며 3-C 이후·Phase 4·Git Commit/Push는 미착수/미허가입니다. 실제 투구·타격 판정, RNG 소비, 이벤트·`ef1`/`ef2`·리듀서·GameRules·RNG 스트림 변경, 영속화, 10만 PA/100경기 보정은 **하지 않았습니다.**

- **구현(계약만):** `src/StarPlayer.Domain/Baseball/Probability/` — 정수 확률 척도(ppm, 분모 1,000,000, 최대 잔여값 정규화·낮은 인덱스 우선·반올림 half-up·128비트 중간 연산, 부동소수점 없음), `prob-model-v1` 8개 단계(ROADMAP §4.1 순서, 단계별 독립 분모), 고정 순서·슬롯별 상한·결합 상한 `[0.8, 1.25]`의 보정 슬롯, Pitch Zone Lite 코스 성향(정확한 질량 이동, 상한 60,000/40,000 ppm, 창 보호, 기준 분포 가중 정확 중립 — 리그 평균 중립은 주장하지 않음), `ProbabilityTrace`, 버전형 **가상 프로토타입** 튜닝 `tuning-virtual-proto-v1`(`content/tuning/virtual_tuning_v1.json`, 해시 `5b70dca7…3ea5`, 독립 Python 스크립트로도 계산해 일치). 포수·배터리·구장 효과는 중립 입력만 받는 예약 슬롯(수치 권한 없음).
- **결정/계약:** [ADR-006](../../docs/decisions/ADR-006.md), [game-engine.md §19](../../docs/game-engine.md), [보고서](../../docs/reports/PHASE_3B2_REPORT.md). ADR-005 상태 줄만 승인 이력에 맞게 정리(결정 내용 불변).
- **테스트(실제 실행):** `dotnet test StarPlayer.sln` — 909 통과(Domain 253, Simulation 371, Application 117, Infrastructure 168; 기준 813에서 +96), 실패·건너뜀 0. 변형 검사 8건: 7건 즉시 검출, 1건("`batter.power` 타깃 추가") 처음 생존 → 슬롯 카탈로그 고정 테스트 추가 후 검출. 사전 선언 시나리오 4건(고정 시드 2026101101–04, 200,000 표본, 이항 4.5σ)은 첫 실행에 통과, 시드·허용치 조정 없음. `tools/verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, 2026-10-11 12:15·12:18; Release 909 통과, Godot 진단·Windows Export·QHD 확인 통과, 링크 검사 69개 파일; 로그 `docs/reports/PHASE_3B2_VERIFY_LOG_run1.md`·`_run2.md`, 보고서 §5.1). 독립 재실행은 `미실시`.
- **공개해야 할 설계 이력:** 목표 구역 가중치 1안은 정확 산술 점검에서 존 안 투구 비율 0.738(의도 0.45–0.65)이어서, 동결(해시 확정)·표본 실행 이전에 바깥쪽으로 두 차례 조정(최종 균형 0.640). 보정이 아니라 표 설계이며, 고정 해시는 최종 표만 포함.
- **미검증/미실시:** Phase 3 Gate 항목 전부, 10만 PA·100경기, 3-F 통계 목표 평가(8개 목표는 선언만, `declared_for_3f_not_evaluated`), 수동 UI·FHD(UI 변경 없음), Codex 독립 검토, GPT Chat 검토, 성능 측정.
- **이월/주의:** ① 3-C: `target_zone` 실현 방식과 RNG 스트림(제안 `game/ai@1`, `game/location@1`을 쓰면 ADR-004 항목 7 문구 수정 필요), 결정 수는 `NextBounded` 호출 수로 센다(원시 소모는 약 0.0225% 더 많을 수 있음) ② 3-D: 성향 문맥 연동, 구장 슬롯 활성화 전 C46 `Hits`의 의미(현재 단타 한정 매핑) 소유자 확인 ③ 3-E: Framing 상한 ④ 3-F: 가상 수치 교체·보정, 분포 가중 성향 기준 ⑤ ADR-004 헤더는 여전히 `Proposed`(미수정).
- **승인자 또는 승인 근거:** (작성 당시 "없음 — 미승인") → 2026-10-11 GPT Chat `PASS WITH NOTES` 승인 + Codex 최종 집중 재검토 `PASS`(`TASK.md`). 위 "3-B2 승인" 콜아웃 참조.

## 3-B1 진행 상황 (2026-10-11, `TASK.md` — Pitch Zone Lite 위치·규칙 데이터 계약)

> [!success] 3-B1 최종 승인 (2026-10-11)
> 내부 작업 패키지 3-B1은 **PASS WITH NOTES / GPT Chat 승인**, Codex 집중 재검토 **PASS WITH NOTES**. 기존 High 결함(기준 분포 ID와 가중치 대응)은 해결되었고 Codex 독립 재검토를 통과했으며, 새로 필요한 코드 수정은 없다. 전체 xUnit 813 PASS(Claude 실행), 관련 xUnit 104 PASS(Codex 직접 실행), `verify.ps1` 연속 2회 `GATE: PASS`(Claude 실행, Codex 로그 검토). **이 승인은 3-B1에만 해당한다 — Phase 3 전체 Gate는 INCOMPLETE, 3-B2 미착수, 이 문서의 최상위 상태(`status`)는 `검토 대기` 유지.** 아래의 `검토 대기`·`미승인`·`미실시` 표기와 Codex `CHANGES REQUIRED` 판정은 승인 이전 라운드의 이력이며 3-B1에 한해 이 블록이 최신이다.
> 유지하는 후속 NOTES: ① `UniformV1`은 ID·가중치 의미가 고정된 기준이며 다른 분포는 새 ID로 등록 ② 3-B2: 확률 분모·기준 확률·상한·보정 시드 확정 ③ 3-C: v2 규칙 팩의 경기 연동, 투구별 타석 방향 ④ 3-E: Borderline/Framing 영향 상한 ⑤ Application 조회 DTO 연결 시 숨겨진 성향 값의 직렬화 노출 검사 ⑥ 운영용 KBO `CalledPitchMode`의 공식 근거 미확정.

> [!note] 상태
> 3-B1은 구현·자체 검증을 마쳤고 **`검토 대기`**입니다(승인 아님). GPT Chat 검토 `미실시`, Codex 검토 `미실시`. Phase 3-B 전체·3-B2 이후·Phase 4는 미착수이며 Phase 3 전체 Gate는 INCOMPLETE입니다. 이 구간에서 위치 추첨, 이벤트·`ef1`/`ef2`·리듀서·RNG·확률 변경은 없습니다.

- **구현:** `src/StarPlayer.Domain/Baseball/PitchZone/` — `ZoneModelVersion` `pzl-zones-v1`, 17개 `PitchZoneId`와 버전형 Clear/Borderline 표(경계선과 변을 공유하면 Borderline: 12개, 나머지 5개 Clear), `PitchLocation`(구역·실제 타석 방향·경계 분류, 허용되지 않는 조합 거부), `TargetZone`, `AppliedBatterSideRules`(스위치 타자), 숨김·유계(`-100..+100`)·평균 중립 `BatterZoneTendency`와 `ZoneReferenceDistribution`. C40 `CalledPitchMode`(`Automated`/`NonAutomated`)와 구역 모델을 짝으로 요구하는 `gamerules-v2`(JSON `format_version` 2, 가상 프로필 2개). 구형 프로필은 판정 모드를 선언하지 않은 사실을 유지하고 7개 `ContentHash`가 변경 전 독립 계산값과 같음.
- **Codex 독립 리뷰와 보완 라운드:** Codex 결과 `CHANGES REQUIRED`(High 1, Low 1). High — `ZoneReferenceDistribution`이 같은 ID(`pzl-ref-uniform-v1`)에 다른 가중치를 허용해 저장된 `ReferenceId`의 의미가 둘일 수 있었음(반례: 가중치 `[3,1,1,...]`에서 `[1,-3,0,...]`가 중립으로 통과, 정식 UniformV1에서는 합 -2). 수정 — ID와 가중치를 한 쌍의 불변 등록 정의로 고정(`pzl-ref-uniform-v1` = `[1]*9`만, 미등록 ID·다른 가중치는 생성 단계에서 거부, 미등록 기준을 만드는 공개 경로 없음, 입력 배열 복사). 새 기준은 새 ID와 고정 가중치로 등록([ADR-005](../../docs/decisions/ADR-005.md) 결정 9). Low — `HiddenTendencyBoundaryTests`의 이름·주석을 실제 보장 범위(Application/Infrastructure 시그니처의 타입 노출)로 정리하고, 실제 직렬화 출력 검사는 Phase 3-D/5 DTO 연결 시점으로 인계. RED 7/232 실패(Codex 반례 포함) → GREEN. `dotnet test StarPlayer.sln -c Release` 813 통과(Domain 232, Simulation 371, Application 117, Infrastructure 93). Codex 재검토 `미실시`, 상태는 계속 `검토 대기`. 상세: [보고서](../../docs/reports/PHASE_3B1_REPORT.md) Part F.
- **변경 파일:** [PHASE_3B1_REPORT.md](../../docs/reports/PHASE_3B1_REPORT.md) §5. 결정: [ADR-005](../../docs/decisions/ADR-005.md). 계약: [game-engine.md §18](../../docs/game-engine.md).
- **테스트(실제 실행):** `dotnet test StarPlayer.sln -c Release` — 797 통과(Domain 216, Simulation 371, Application 117, Infrastructure 93; 기준 709에서 +88), 실패·건너뜀 0. 변형 검사 15건 모두 검출(처음 1건 생존 → 상·하한 각각 검증하는 테스트 추가 후 검출). `tools/verify.ps1` 결과는 보고서 Part 10. RED 로그는 없음(테스트가 타입 부재로 컴파일되지 않아 별도 RED 실행을 남기지 못함; 변형 검사로 대체).
- **미검증/미실시:** Phase 3 Gate 항목(100,000 PA, 100경기, 보정, 나머지 Pitch Zone Lite 검사), 수동 UI·FHD(UI 변경 없음), 데이터 계약의 독립 형식 검토(Codex 권장), 성능 측정.
- **이월/주의:** 12/17이 Borderline이라 분류가 거칠다 — Framing 크기는 3-E 상한이 담당하고 더 세밀한 표는 새 모델 버전(ADR-005 결정 2). `UniformV1`은 임시 기준 분포. 3-B2는 확률 척도·성향 상한·기준 분포·기준 확률을 보정 전에 확정해야 하며, 운영 KBO 프로필이 선언할 `CalledPitchMode`는 근거와 소유자 결정이 필요(구형 프로필은 미선언 유지).
- **승인자 또는 승인 근거:** (승인 이전 기록: 3-B1 미승인.) 최종: GPT Chat PASS WITH NOTES 승인 + Codex 집중 재검토 PASS WITH NOTES (2026-10-11, 소유자 전달 `TASK.md`).

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

## 부록 2026-10-16 — Codex F-01 (MAJOR) 수정 기록 (Claude Code 구현·자체 테스트, 독립 재검토 `미실시`)

- 지적: `CommitEvidence.LinkAbilities`가 감사 기록을 실제 `AdvancementContext`와 결속하지 않음(동일 Speed 다른 주자, 동일 Arm 다른 송구 수비수 위조 통과).
- 수정: `plan.Facts`에서 `AdvancementPlanner.Contexts`를 다시 도출해 결정 수·순서·키·RunnerKey·Runner·StartBase·PlanTo·TargetTo를 비교하고, v2는 `AdvancementSlots.For`로 기대 Abilities·Modifiers를 독립 도출해 비교. 새 오류 코드·RNG·공식 변경 없음. 상세는 [ADR-007](../../docs/decisions/ADR-007.md) 부록.
- 신규 테스트 4개(`AdvancementAuditBindingTests`): 수정 전 4/4 실패 확인 → 수정 후 통과. Sabotage(새 비교 비활성화) 시 4/4 실패, 원본 복구(SHA 일치) 확인.
- 전체 xUnit Release: Domain 282 / Application 127 / Simulation 727 / Infrastructure 904 = 2,040 통과, 실패 0, 건너뜀 0. Mutation 전체·verify.ps1은 `미검증`(미실행).
- Phase 3 Gate: `INCOMPLETE` 유지. 상태: `검토 대기` 유지(승인 아님).


> [!info] 3-E Game Calling v4 - Codex 최종 리뷰 F-01(MAJOR) 수정 · F-02(MINOR) 정정 (2026-10-17, `TASK.md` "Phase 3-E GC v4 Final Review F-01 Fix", 기준 HEAD `131dcac`, Closeout 로컬 Commit에 포함(SHA는 `git log` 참조)·미푸시) - `IMPLEMENTED` · `TESTED` · `REVIEW_PENDING` (승인 아님)
> **F-01:** v4 투구 후보의 Stage 구조가 처음 두 단계만 검사되고 이후 단계는 후보가 적은 Stream으로 재생되어, 실제 `game/ai@1` 추첨 1회를 마지막에 덧붙이고 Roll·RawDraws·StreamUsage·NextBoundedCalls·H1을 모두 맞춘 후보가 통과했다. **재현(수정 전):** 신규 `GameCallingStageStructureTests`가 실제 재생으로 만든 위조 후보(HBP 포함)를 실제 `PitchCommitGate.Stage`에 넣어 `staged` 됨을 확인. **수정:** `CommitEvidence.StructureProblem`이 실제 분기(HBP / 콜 / 헛스윙·파울 / 인플레이)에서 허용 Stage 목록·순서·개수·Stream을 먼저 도출해 정확히 일치할 때만 재생(분기 표지와 타자 결정·스윙 결과의 의미도 대조). RNG 알고리즘·Stream·Model/Tuning·Golden·ef1/ef2·PlayerInputSet·CAS·GC-S1·GC-S2·v1~v3 불변.
> **Sabotage:** S1(구조 검사 제거) 신규 4건 중 2건 실패, S3(기록된 Stream 그대로 재생) 2건 실패, S2(Stream 비교만 제거)는 처음엔 미검출(재생이 Rng 코드로 대신 거부) → 구조 케이스를 `Commit.candidate_link_mismatch`로 고정해 검출. 원본 SHA-256 복구 확인. 전체 Mutation·verify.ps1·Codex는 `미실시`/`미검증`.
> **F-02:** PCG32 참조 402개는 Resolver 대조 전체에 적용되지만 Commit 경로 테스트는 `prior_target_calls <= 1` 필터로 270개만 실행(270 staged, 건너뜀 0). 이전 "모든 위치가 staging된다" 서술을 정정(Commit 재생이 402개를 통과했다는 주장은 철회). 테스트에 카운터와 합계 검증 추가.
> 전체 xUnit Release **2,337 PASS**(Domain 327, Application 127, Simulation 738, Infrastructure 1,145 = 1,141 + 4; 0 FAIL, 0 SKIP, 종료 코드 0). Phase 3 Gate `INCOMPLETE` 유지, 상태 `검토 대기` 유지. 상세: `docs/reports/PHASE_3E_GAME_CALLING_V4_REPORT.md` §L, ADR-007 부록.

## 부록 2026-10-17 — 3-F F-A/B Codex Findings F-01~F-04 수정 기록 (Claude Code 구현·자체 테스트, 독립 재검토 `미실시`)

- 판정 대상: `CODEX_PHASE3_F_FA_FB_REVIEW_REVISIONS_REQUIRED`(정적 검토). 기준 HEAD `430f42d` 불변, 미스테이징·미커밋·미푸시. 모든 반례를 실제 런타임에서 먼저 재현(Red)한 뒤 수정.
- **F-01 (MAJOR):** `CohortInputs.Sheets`가 생성자가 채운 `Dictionary` 그대로였고(`IDictionary`로 캐스팅해 `Clear()`하면 24→0), `Away`/`Home`이 호출자의 `List` 기반 Lineup·Bench를 유지해 입력이 바뀌어도 `InputsHash`가 같았다(Red: 신규 8개 중 5개 실패). 수정: `ImmutableSortedDictionary`(PlayerId 서수 정렬) + `ImmutableArray` 팀 구성 복사본만 보관·노출하고 Hash를 그 복사본으로 계산. 빈 라인업 항목은 `Harness.cohort_incomplete`. 수정 중 `ImmutableDictionary`의 프로세스별 열거 순서로 기존 테스트가 간헐 실패(100개 중 1개)해 정렬 사전으로 재수정(8/8 재실행 통과). `PlayerSheet`는 불변이라 추가 복사 없음.
- **F-02 (MAJOR):** Harness 직접 로더가 필수 숫자 누락을 0으로, 중복 ProfileId를 첫 항목으로, 대소문자·숫자 문자열 Enum을 허용, null table 항목은 `NullReferenceException`(Red: 87개 중 20개 실패, Codex 반례 4종 포함). 수정: Infrastructure DTO와 동일한 `required` 구성, null 요소 검사, 중복 거부, 정확한 snake_case Enum. Infrastructure 코드·공개 범위 변경 없음. 대조 행렬 83건(J1~J12): 기존 로더가 수락하는 파일은 같은 Hash로 수락, 거부(또는 예외)하는 파일은 Harness가 오류 값으로 거부. 기존 로더의 `profiles[n] = null` → `NullReferenceException`은 Production 결함으로 기록만(범위 밖).
- **F-03 (MINOR):** `tools/verify.ps1` 테이블에 `U5`(SimHarness.Tests) 추가, 기존 U1~U4(U3a·U3b 포함) 불변, B1 설명 "all 9 projects"→"all 11 projects". PowerShell 구문 검사 0 오류. **스크립트 실행 안 함.**
- **F-04 (MINOR):** `AllCompleted`만 보던 종료 코드 → Red: `Metrics.target_missing`이 있어도 Exit 0. 수정: 0 = 전 경기 완료+지표 계산 성공(NOT_EVALUATED·INSUFFICIENT_SAMPLE·FAIL 판정 포함), 1 = 경기 실패/취소, 2 = 잘못된 입력, 3 = 경기는 완료됐으나 지표 계산 실패. `summary.json`에 `run_status`·`metrics.error_code`. 실제 자식 프로세스로 종료 코드 검증. 시험용 `run --tuning FILE`(매니페스트에 `tuning_source`/`tuning_file_sha256` 기록, replay는 거부).
- **PZL 목록:** ROADMAP 문장은 **12개 절**이다(조항별 재집계, 임의 삭제 없음). 이전 "11개"는 정정. F-C 정책(F1 same_hand 4% 보정, F2~F5 Virtual League, 최소 표본, 지배 전략 행렬, 대표 코호트)은 확정하지 않음.
- **테스트:** 신규 SimHarness +14(Cohort 8, CLI 6), Infrastructure +87(대조 행렬). 전체 xUnit Release **2,528 PASS**(Domain 327, Simulation 741, Application 127, Infrastructure 1,233, SimHarness 100; 0 FAIL, 0 SKIP, 종료 코드 모두 0). 소규모 Smoke(시드 3·11·29): 이전 보고서와 같은 226 PA·673 투구·899 이벤트·F1~F8 값, replay 일치. Sabotage 4건(S1 Cohort 방어 제거 4개 실패, S2 Hash 결속 약화 1개, S3 필수성 약화 2개, S4 종료 코드 약화 3개) 모두 검출·원본 SHA-256 복구 확인. 전체 Mutation·`verify.ps1`·Codex `미실시`/`미검증`.
- Phase 3-F Gate·Phase 3 Gate: `INCOMPLETE` 유지. 상태: `검토 대기` 유지(승인 아님). 상세: [보고서 부록](../../docs/reports/PHASE_3F_FA_FB_IMPLEMENTATION_REPORT.md) · ADR-007 부록 · [game-engine §34](../../docs/game-engine.md).

## 부록 2026-10-17 — 3-F F-A/B 로컬 Commit 및 F-C1 정책 제안서 초안 (Claude Code 기록, 독립 검토 `미실시`)

- **Commit:** `5492e80eba7d347f7deabe5dbdb98931f92db0f3` (Parent `430f42d0dbd61c51fbde5cb10fbf2e98d1eecc02`), 메시지 `feat: implement phase 3-F deterministic harness and metrics`, 38개 파일(수정 11·신규 27), 명시적 상대경로 Staging, 보호 사용자 파일 3개 제외. **미푸시.** 위 F-A/B 부록의 "미커밋" 서술은 Commit 이전 상태다.
- **Closeout 검증:** `dotnet build StarPlayer.sln -c Release` 종료 코드 0·경고 0, `dotnet test StarPlayer.sln -c Release` 종료 코드 0 — **2,528 PASS**(Domain 327, Application 127, Simulation 741, Infrastructure 1,233, SimHarness 100), 0 FAIL, 0 SKIP. 별도 프로세스 3회 실행 Hash 동일, replay 일치. `verify.ps1` 전체 `미검증`(미실행, U5 등록은 정적 확인). Codex F-01~F-04 재검토 `미실시`.
- **NOTE:** 미등록 CLI 옵션 무시(`run --seedz 1` → 기본 Seed, Exit 0; F-C2 선행), `GameRulesJson` `profiles[n]=null` NRE(Harness 경로 아님), 9명 아닌 라인업은 경기 시작 시 엔진이 거부.
- **F-C1:** [정책 제안서](../../docs/reports/PHASE_3F_FC1_POLICY_PROPOSAL.md) 작성, 상태 `DRAFT — OWNER APPROVAL REQUIRED`. FC-D1~FC-D12는 모두 승인 대기이며 동결 아님. 코드·테스트·Tuning 변경, 공식 Seed 활성화, 대규모 실행 없음. F-C2/F-C3 미착수.
- Phase 3-E 독립 최종 검토는 여전히 대기. Phase 3-F Gate·Phase 3 Gate: `INCOMPLETE` 유지. 상태: `검토 대기` 유지(승인 아님).

## 부록 2026-10-17 — 3-F F-C1 정책 계약 동결 (소유자 승인 기록, Claude Code 문서 반영, 독립 검토 `미실시`)

- **승인:** 소유자가 FC-D1~FC-D12 권장 정책을 승인했다(`OWNER_APPROVED`, 소유자 `TASK.md` "Star Player Phase 3-F F-C1 Policy Contract Freeze", 기준 HEAD `5492e80` 불변). 이 부록은 소유자 승인 사실의 기록이며 Phase 리뷰 승인(`승인 완료`)이 아니다.
- **문서:** 권위 [ADR-008](../../docs/decisions/ADR-008.md) (신규), 해설 [정책 제안서](../../docs/reports/PHASE_3F_FC1_POLICY_PROPOSAL.md) 개정 2(§20 승인 기록, 기존 분석 보존), [동결 보고서](../../docs/reports/PHASE_3F_FC1_POLICY_FREEZE_REPORT.md), `docs/game-engine.md` §35, `docs/SYSTEM_CONTRACTS.md` 항목, ROADMAP 3-F 상태 노트, [결정 노트](../02_Decisions/2026-10-17_Phase_3F_FC1_Policy_Freeze.md).
- **동결된 정책:** F1 코호트 충돌 해소(`neutral-mixed-v1`, Tuning 원문·JSON 불변), F1·F8 3/7·4/7 층화 표준화(층별·경기 군집 분산, 합산 Wilson 미적용), F6·F7 관측 집계, F2~F5 `virtual-league-v1`, 최소 분모 8개와 F1·F8 층별 최소 분모(재검산 일치), L1~L4 각 ≥ 100,000 PA + 모드별 100경기, 48셀 δ 0.05·α 0.05·검정력 0.8·Holm·IUT·paired 금지, A-1 가정은 F-C2 필수 검증, PZL 12a 필수·12b `DEFERRED`(Phase 5·6·28, Phase 3 Gate 비차단 예외), 실패·재실행 규칙, Official Mode 요구(F-C2 구현 계약).
- **미동결 (`FINAL_EXECUTION_FREEZE_PENDING`):** 공식 Seed Manifest·파일 Hash, 48셀 셀별 표본 수, 최종 Cohort 인스턴스·Hash, Dry Run 이후 실행 상한, 실행 직전 Git SHA.
- **검증:** 문서만 변경. 코드·테스트·Tuning·Seed 파일 변경 0, 보호 파일 3개 SHA-256 시작·종료 동일, Staging 0, `git diff --check` PASS(보고서 L절). 전체 xUnit·`verify.ps1`은 재실행하지 않음(2,528 PASS는 `5492e80`의 과거 결과).
- F-C2·F-C3 `NOT_STARTED`. Phase 3-F Gate·Phase 3 Gate: `INCOMPLETE` 유지. 상태: `검토 대기` 유지(승인 아님).
