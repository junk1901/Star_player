# Star Player Phase 3-F F-C2-A Implementation Report — Official Execution Integrity

- 작성: Claude Code, 2026-10-17, 소유자 `TASK.md` "Star Player Phase 3-F F-C2-A Official Execution Integrity"
- 기준 HEAD: `97045829d5836ff6dec8b40109c84d016d92c340` (`docs: freeze phase 3-F F-C1 validation policies`, Parent `5492e80`) — 작업 중 불변
- 최상위 정책: [ADR-008](../decisions/ADR-008.md) (변경 없음, 상태 문구만 최신화)
- 최종 판정: **`PHASE3_F_FC2A_IMPLEMENTED_WITH_NOTES`** · `OFFICIAL_PREFLIGHT_READY`(골격) · **`OFFICIAL_EXECUTION_NOT_READY`** · `FINAL_EXECUTION_FREEZE_PENDING`
- 검토 상태: `검토 대기` (구현자 자체 시험만; GPT Chat·Codex 검토 `미실시`)
- **Phase 3-F Gate: `INCOMPLETE`. Phase 3 Gate: `INCOMPLETE`.**

## A. Git 및 구현 개요

- 시작/종료 Branch `main`, HEAD `97045829d5836ff6dec8b40109c84d016d92c340`. Staging 0, Commit 0, Push 0. 시작 시 추적 변경 0, 미추적은 보호 파일 3개뿐이었다.
- 범위: Strict CLI, Git Working Tree Integrity, Seed Manifest Reader, Official Preflight 골격, Cohort/Policy Registry 기반 Replay. 모두 `tools/StarPlayer.SimHarness`(Domain + Simulation만 참조) 안이다. **Production `src/`, GameEngine, 확률·RNG·Commit/CAS·통계 공식, Model/Tuning/Rules 콘텐츠는 변경하지 않았다.**
- 신규 파일(미추적): `tools/StarPlayer.SimHarness/Cli.cs`, `GitIntegrity.cs`, `SeedManifest.cs`, `Replay.cs`, `Official.cs`; `tests/StarPlayer.SimHarness.Tests/StrictCliTests.cs`, `GitIntegrityTests.cs`, `SeedManifestTests.cs`, `OfficialAndReplayTests.cs`, `Fixtures/legacy-manifest-neutral-default.json`, `Fixtures/legacy-manifest-automated-v1.json`; 이 보고서; `Star/02_Decisions/2026-10-17_Phase_3F_FC2A_Execution_Integrity.md`.
- 수정 파일: `tools/StarPlayer.SimHarness/Program.cs`(엄격 파서 연결, `official` 명령, Registry 기반 replay, `Build`의 코호트·정책·Competition Key 전달), `HarnessData.cs`(`NeutralPark()` 추출 — 같은 값, 공유만), `tools/StarPlayer.SimHarness/README.md`; 문서 `CURRENT_STATE.md`, `docs/decisions/ADR-008.md`(상태 행), `docs/game-engine.md`(§35 상태, §36 신설), `docs/SYSTEM_CONTRACTS.md`, `docs/reports/PHASE_3F_FC1_POLICY_PROPOSAL.md`·`PHASE_3F_FC1_POLICY_FREEZE_REPORT.md`(역사 기록 보존 + 이후 상태 주석), `ROADMAP.md`(3-F 상태 노트의 F-C2 상태 문구), `Star/00_Dashboard.md`, `Star/01_Phase_Reviews/Phase_03_Review.md`, `Star/02_Decisions/2026-10-17_Phase_3F_FC1_Policy_Freeze.md`.
- 계약 충돌: 없음(ADR-008을 덮어쓴 곳 없음). 해석 NOTE는 §H.
- 범위 초과: 없음. F-C2-B/C, 공식 Seed 생성·활성화, 100,000 PA·100경기·48셀 실행, Tuning 변경, `verify.ps1` 전체, 전체 Mutation, Codex 호출 모두 하지 않았다.

## B. Strict CLI (Part A)

- **Red(실제 프로세스, 변경 전 빌드):** `run --seedz 1 --run-id red-seedz --out …` → 기본 Seed 3·11·29 3경기 실행, **EXIT=0**. 추가로 발견: `run --seeds 3 --rules bogus --advancement zzz` → 조용히 `test-pzl-non-automated` / `advancement-policy-v2`로 대체, EXIT=0. 신규 `Cli` 형식이 없어 `StrictCliTests`는 컴파일 Red(CS0103).
- **구현(`Cli.cs`):** `Cli.Commands`가 명령별 옵션 화이트리스트의 단일 권위 구조다(`run`: seeds·rules·advancement·event-budget·tuning·run-id·out / `replay`: manifest(필수) / `official`: seed-manifest·run-id(필수)·out). 거부 코드: `Cli.command_missing`, `Cli.command_unknown`, `Cli.option_unknown`, `Cli.option_duplicate`(같은 값이어도), `Cli.value_missing`(다음 토큰이 `--`), `Cli.value_invalid`, `Cli.argument_unexpected`, `Cli.option_required`, `Cli.option_forbidden`(official에서 `--tuning`·`--seeds`·`--rules`·`--advancement`·`--event-budget` — FC-D10의 시험용 seam 거부와 "공식 입력은 고정 Manifest·프로파일에서만"). `--name=value`, 대소문자 변형, 약어는 미등록 옵션이다. Seed 값은 부호·공백·빈 항목·선행 0 없는 `0..ulong.MaxValue` 정수 목록, 반복 Seed는 합법(GameId가 다름, FC-D4.6). `rules`/`advancement`는 열거값만 허용하고 `Build`도 미지 값을 기본값으로 바꾸지 않는다.
- 모든 CLI 거부는 Exit 2이며 메시지에 코드와 해당 인자를 쓴다. 생략된 옵션의 문서화된 기본값(`--seeds` 3,11,29 등)은 명령이 적용하고 파서는 값을 만들지 않는다.
- **호환성:** 기존 `run`/`replay`/`--tuning FILE`/스모크 경로 유지. `run`(옵션 없음)의 `config_hash b9bbac72…195a`·`run_result_hash 28e0536c…6ec6`가 변경 전 빌드와 동일(`StrictCliTests.The_default_smoke_run_keeps_its_pre_FC2A_hashes`). Exit 0/1/2/3 계약(`CliExitCodeTests` 6건)과 기존 `ReportAndReplayTests` 그대로 PASS.

## C. Git Integrity (Part B)

- **순수 Parser `GitStatusParser`:** `git status --porcelain=v1 -z --untracked-files=all --ignored`의 NUL 구분 기록 `XY PATH`; `R`/`C`는 두 번째 NUL 필드가 원래 경로(실제 Git 출력으로 형식 확인). 상태 문자 ` MTADRCU`, `??`, `!!`만 허용. 잘린 출력(마지막 NUL 없음), 남는 바이트, 짧은 기록, 3번째 바이트가 공백이 아님, 빈·절대·`..` 경로, 원래 경로 없는 rename → `Git.status_malformed`(정상 트리로 보지 않음). `-z`이므로 공백·한글 경로를 그대로 읽는다.
- **정책 `GitIntegrityPolicy`:** 예외는 정확한 3개 경로(`TASK.md`, `docs/2023recordRule.pdf`, `docs/2026-official-baseball-rules.pdf`)가 **미추적(`??`)일 때만**. 대소문자 변형·접미사·다른 폴더·앞 공백·추적/Staging/ignored 상태의 같은 이름은 모두 거부(`Git.dirty`). 그 외 추적 변경·Staging·미추적·ignored 파일 거부(`build/` 아래 이전 실행 출력 포함).
- **빌드 산출물:** 프로젝트(`src|tools|tests/<project>/`)의 `bin/`·`obj/` 아래 ignored 파일만 후보다. **경로만으로 신뢰하지 않는다** — F-C2-A에는 빌드-소스 SHA 결속 증명이 없으므로 이 경우 상태는 `BuildOutputUnverified`(`Git.build_output_unverified`, Preflight `NOT_READY`)이며 Clean이 아니다. 거부 항목이 하나라도 있으면 `Dirty`가 우선한다.
- **Adapter `GitIntegrity.Check`:** `rev-parse --show-toplevel`(기대 루트와 정규화 비교, 하위 폴더 불일치 포함 → `Git.root_mismatch`), `rev-parse HEAD`(소문자 40/64 hex 아니면 `Git.head_invalid`), status(위 옵션). 시작 실패·비0 종료·예외 → `Git.query_failed`, UTF-8 아님 → `Git.output_not_utf8`. `GIT_OPTIONAL_LOCKS=0`으로 index를 갱신하지 않는다(저장소 무변경). 실제 임시 Git 저장소 통합 시험 1건(Clean/한글·공백 경로 Dirty/빌드 산출물 NOT_READY/추적 변경/루트 불일치).
- **현재 실제 저장소:** `official` 실행 시 `Git.dirty`(개발 변경 + `build/` 아래 ignored 파일 등 781건) → 거부. 보호 파일 3개 자체는 예외 처리됨.

## D. Seed Reader (Part C)

- 형식 `simharness-seed-manifest-v1`: `schema_version`, `family`, `competition_key`, `cohort_id`, `policy_id`, `rules_profile_id`, `game_id_rule`(`game-id-v1` = `game:{key}:{cohort}-{index:D6}`, `RunConfig.Spec`과 동일), `block_size_games`, `max_games`, `pa_target`, `seeds[{index, seed, game_id}]`. 모든 항목 필수(`pa_target`은 null 허용), 미지·중복 JSON 항목 거부(System.Text.Json의 "마지막 값 사용"을 별도 검사로 차단).
- **무결성:** `SeedManifestReader.Read(root, 상대경로, 고정 SHA-256)` — 파일 원본 바이트의 SHA-256을 **외부에서 Pin된 값**과 대조한다. Pin이 없거나 형식이 틀리면 읽지 않는다(`Seed.pin_invalid`). 파일에는 자기 해시 항목이 없다(넣으면 미지 항목 거부). 공식 Pin 표 `SeedManifestPins.Official`은 **비어 있음**.
- **경로:** 루트 기준 `/` 상대 `.json`만; 절대·드라이브·역슬래시·빈/`.`/`..` 구간·루트 이탈 → `Seed.path_invalid`, 없음 → `Seed.file_missing`, 16 MiB 초과 거부.
- **Family:** `fc2-dry`(비공식 dry run), `fc3-cal`·`fc3-int`·`fc3-dom`(공식). Competition Key는 GameId 구성 규칙을 따르고 family 이름 또는 `family-…` 접두여야 한다. 여러 Manifest 사이 키 재사용 거부(`CheckUnique`; GameId는 키를 단사적으로 포함하므로 키 유일성 = GameId 유일성).
- **유일성·순서:** index 중복, GameId 중복, 1부터 연속된 파일 순서 아님, GameId ≠ 규칙 값 → 각각 거부. **Seed 값 중복은 허용**(같은 Seed + 다른 GameId = 다른 RNG 경로, FC-D4.6; RNG 충돌로 단정하지 않음).
- **Block:** 블록 크기가 상한을 정확히 나눔, Seed 수 ≤ 상한(및 ≤ 999,999), 완전한 블록만. `fc3-cal`은 `pa_target ≥ 100,000`(FC-D7.1) 필수, 그 외 family는 `pa_target` 금지, `fc3-int`는 정확히 100경기(FC-D7.2). 정지 규칙 `AfterGames(경기수, 누적 PA)`: 블록 경계에서만 정지 — 목표 도달(`StopTargetReached`) 또는 선언 Seed 소진(`StopSeedsExhausted`); 진행 중 블록은 끝까지(FC-D5.3); Seed를 추가하지 않음. **실제 블록 크기(7/63 후보)·상한·Seed 범위는 확정하지 않았다**(Manifest가 선언, Final Execution Freeze 대상).
- 오류 Fixture(모두 TEMP, 가상 Seed 990001…): Hash 불일치, 한 자리 변조(형식상 유효 — Pin만 검출), 절반 잘림(Pin 불일치 / 자체 Pin이면 JSON 오류), 중복 GameId·Index, Key/family 불일치, 순서·간격·0부터, 필수 항목 누락·null, 미지·중복 항목, 잘못된 family·schema·rule, 잘못된 Seed(-1, 1.5, "3", 2^64, null, true), 빈 목록, 블록 경계·불완전 블록·상한 초과, 불법 경로.

## E. Official Mode (Part D)

- 명령: `official --seed-manifest <저장소 상대 경로> --run-id ID [--out build/simharness/official/…]`. `run`과 분리. **게임을 실행하는 코드 경로가 없다**: Preflight가 전부 PASS여도 `Official.execution_not_implemented`로 거부한다(현 승인값으로는 도달 불가).
- **Preflight 검사(전부 실행·보고, 단락 없음):** `cli`, `git_integrity`, `git_sha`(승인 SHA), `model_tuning`(`prob-model-v4`/`tuning-virtual-proto-v4`/`5011f8ac…8fd8` — 실제 PASS), `seed_manifest`(Pin 없으면 파일을 읽지 않고 NOT_READY), `family`(공식만), `rules`(FC-D7.5의 두 `gamerules-v2` fictional-test 프로파일 + 커밋된 내용 해시), `policy`(Registry; cal/int는 `normal-balanced-v1`만, FC-D3), `cohort`(family별 ADR 코호트: cal = `neutral-mixed-v1`/`virtual-league-v1`(FC-D7), dom = `neutral-v1`/`neutral-opp-v1`(FC-D4); int는 ADR 미지정 → NOT_READY; 미구현 → NOT_READY), `cohort_hash`(동결 InputsHash), `game_ids`, `output_directory`, `execution_policy`(승인 ID·해시), `execution_path`, `attestation`. 상태 PASS / FAIL / NOT_READY, 오류 코드는 첫 FAIL 코드, FAIL이 없으면 `Official.not_ready`.
- **현재 승인값 `OfficialApprovals.Current`:** Git SHA·실행 정책·Seed Pin·코호트 InputsHash 모두 없음, `ExecutionPathReady=false`(F-C2-B/C 평가 경로·Final Execution Freeze 없음) → **어떤 입력이든 NOT_READY 또는 FAIL**. 실제 프로세스 결과: Exit 2, `run_status PREFLIGHT_REFUSED`, `gate_status NOT_EVALUATED`, `error_code`(실 저장소에서 `Git.dirty`), 미승인 값(`execution_policy_*`, `cohort_hash`, `seed_manifest_hash`, `config_hash`, `attestation_hash`)은 null — 위조해서 채우지 않음. 출력 디렉터리를 만들지 않는다.
- **READY 경로는 시험 전용 Fixture 승인값으로만** 확인(TEMP Manifest, 가짜 Clean Git): 이때만 Attestation이 생성되며, 조건을 하나씩 깨면 해당 검사가 FAIL/NOT_READY가 된다(Dirty, 빌드 산출물 미검증, Git 조회 실패, SHA 불일치, Seed Hash 불일치, 경로 이탈, 코호트 Hash 불일치·미동결, 실행 정책 불일치, 실행 경로 미준비, 출력 경로 이탈, 비공식 family, 미승인 Rules, 미구현/부적합 코호트, 미등록 정책).
- **Attestation(`simharness-official-attestation-v1`):** git SHA, 실행 정책 ID·해시, Seed Manifest 경로·원본 SHA-256·family·Competition Key, GameId 규칙, 코호트 ID·InputsHash, 정책 ID, 모델·튜닝 버전·해시, Rules ID·해시, 진루 정책, 구장 스냅샷, 이벤트 예산, **기존 공식 그대로의 ConfigHash** — 21개 전부가 `Hash()`에 들어간다. 시험이 직렬화된 모든 속성을 하나씩 바꿔 해시 변화를 확인한다(새 필드 누락 방지). 시각·환경·로그는 포함하지 않는다. Manifest에는 `official = { schema_version: simharness-official-v1, attestation, attestation_hash }` 봉투로 결속한다.
- **실행 정책 후보:** `OfficialProfile.Id = fc3-official-execution-v1`과 `Hash()`(고정 프로파일 + family 코호트 + calibration 정책)는 이름만 정의했고 승인값이 아니다.
- **출력 디렉터리:** `build/simharness/official/<run-id>`(기본) 또는 그 아래의 새 경로만; 절대·드라이브·역슬래시·빈/`.`/`..` 구간·루트 자체·스모크 경로·추적 소스 경로 → `Official.output_invalid`, 기존 파일/폴더 → `Official.output_exists`(덮어쓰기 없음). run-id는 디렉터리 이름 규칙 검사.
- **Exit 코드와 Gate 분리:** 공식 명령의 거부는 Exit 2(기존 의미 "실행 거부된 입력"과 같은 범주; 새 코드 추가 없음). Gate 상태 어휘는 `NOT_EVALUATED`·`INSUFFICIENT_SAMPLE`·`PASS`·`FAIL`이며 F-C2-A에서 생성되는 값은 `NOT_EVALUATED`뿐이다.

## F. Replay Registry (Part E)

- **Red(실제 프로세스, 변경 전 replay 코드):** 변경 전 매니페스트에서 `policy_id`를 `patient-careful-v1`로, `rules_profile_id`를 `test-pzl-bogus`로, `cohort_inputs_hash`를 변조, `manifest_version`을 v2로, `advancement_policy_version`을 v9로 바꾼 5개 모두 `REPLAY MATCH`, **EXIT=0**(정책 ID 무시, 미지 규칙 → non-automated, 미지 진루 → v2로 대체, 코호트 해시·스키마 미검사).
- **구현:** `CohortRegistry`(구현: `neutral-v1`; ADR 선언·미구현: `neutral-opp-v1`, `neutral-mixed-v1`, `virtual-league-v1` → `Registry.cohort_not_implemented`; 그 외 `Registry.cohort_unknown`), `PolicyRegistry`(`normal-balanced-v1`; 그 외 `Registry.policy_unknown`). **미구현 ID를 `neutral-v1`로 대체하는 Placeholder 없음.** `ReplayCheck.Validate`: `manifest_version`·`input_schema_version` 확인, 시험용 튜닝 실행 거부 유지, Registry 조회, 재구성 코호트 InputsHash = 기록값, 정책 Registry 조회, Rules·진루 정책은 정확한 기지 값만, 기록된 `competition_key`로 재구성, 중복 JSON 항목 거부. `official` 봉투가 있으면 `OfficialReplay.Verify`(봉투·Attestation 버전, Attestation 해시 재계산, ConfigHash 결속, Seed Pin, 실행 정책, 코호트 Hash, Git SHA, 실행 경로 준비)와 Attestation↔매니페스트 실행 기록 대조(`Replay.attestation_manifest_mismatch`). 현재 승인값으로는 공식 replay가 항상 거부된다(`Official.seed_manifest_not_frozen` 등) — 공식 replay 성공으로 표현하지 않는다.
- **Legacy:** 변경 전 빌드로 만든 매니페스트 2개(`Fixtures/legacy-manifest-neutral-default.json` — 기본 실행, `…automated-v1.json` — `--seeds 3,11 --rules automated --advancement v1`)가 기록된 `run_result_hash`(`28e0536c…6ec6`, `9e991e89…8903`)로 그대로 `REPLAY MATCH`. 기존 replay 시험(성공·실패 실행 replay, 변조 결과 해시 → 1, 시험용 튜닝 → 2) 유지.

## G. 회귀

| 명령 | 환경 | 결과 |
|---|---|---|
| `dotnet build StarPlayer.sln -c Release` | Windows 11, .NET SDK 8.0.425 | exit 0, 경고 0, 오류 0 |
| `dotnet test StarPlayer.sln -c Release --no-build` (위 빌드 직후) | 동일 | exit 0, **2,658 PASS**, 0 FAIL, 0 SKIP |
| `dotnet test StarPlayer.sln -c Release` (문서 수정 후 최종, 빌드 포함) | 동일 | exit 0, **2,658 PASS**, 0 FAIL, 0 SKIP |

- 프로젝트별: Domain 327, Application 127, Simulation 741, Infrastructure 1,233(모두 기준과 동일), **SimHarness 230**(기준 100 + 신규 130: StrictCli 46, GitIntegrity 32, SeedManifest 27, OfficialAndReplay 25 — Theory 사례 포함).
- 신규 시험의 Red: CLI·Replay는 위 실제 프로세스 Red(EXIT 0) + 컴파일 Red; Git·Seed·Official은 컴파일 Red(형식 부재). 구현 중 시험 Fixture 오류 1건 수정(Seed 시험의 `…000006`이 6번 항목과 실제로 중복되어 `game_id_duplicate`가 올바른 판정 → 겹치지 않는 `…000099`로 교정; 생산 코드 판정은 바꾸지 않음).
- 표적 Sabotage 3건(전체 Mutation 아님): 보호 경로 비교를 대소문자 무시로 → 1 FAIL 검출; Seed 원본 Hash 대조 제거 → 2 FAIL 검출; Replay 코호트 InputsHash 대조 제거 → 1 FAIL 검출. 모두 원본 SHA-256 일치로 복구.
- 기존 Golden/Hash: `RunConfig.ConfigHash` 공식·`run-result-v1`·게임 결과/이벤트/RNG 해시 코드 무변경, 기본 스모크와 두 Legacy 매니페스트의 해시 일치. v1~v4 Model/Tuning, PlayerInputSet v1~v3, ef1/ef2, RNG, Commit/CAS, GameStarter, Metrics F1~F8, BoxScore/Oracle — 해당 코드·콘텐츠 무변경, 기존 시험 전부 PASS.
- `tools/verify.ps1` 전체: `미실시`(지시에 따라). 대규모 시뮬레이션: `미실시`. 독립 리뷰(GPT Chat·Codex): `미실시`.

## H. 남은 작업 및 NOTE

- **F-C2-B (미착수):** `neutral-opp-v1`·`neutral-mixed-v1`·`virtual-league-v1` 코호트(게임 index별 선발·타순 로테이션을 InputsHash에 결속), 승인 `EvaluationPolicy`(최소 표본, F1/F8 층화·정수 공식, F1 코호트 정정, 군집 분산·DEFF 진단).
- **F-C2-C (미착수):** 12개 행렬 정책(TakeFirst 포함), 48셀 실행기·판정기(Welch, IUT/Holm, 4상태), A-1 검증, PZL #5·#6·#10·#12a 검사, `fc2-dry` 소규모 dry run.
- **Final Execution Freeze (PENDING):** 공식 Seed Manifest·Pin, 블록 크기·상한, 셀별 표본, 코호트 InputsHash, 실행 정책 승인, 공식 Git SHA, **빌드 산출물-소스 SHA 결속 증명**(현재 NOT_READY 사유).
- **F-C3 (미실행).**
- NOTE-1: 공식 출력은 `build/simharness/official/` 아래(ignored)에 쓰이므로, 그 출력이 남아 있으면 다음 공식 실행의 Git 검사가 `Dirty`로 거부한다(FC-D10은 승인된 빌드 출력 외 ignored 파일을 거부). 실행 출력의 허용 범위는 실제 공식 실행을 구현하는 단계에서 소유자 결정이 필요하다.
- NOTE-2: `fc3-int`의 공식 코호트는 ADR-008에 명시가 없다(제안서의 후보는 `virtual-league-v1`). Preflight는 이를 정하지 않고 `Official.family_cohorts_not_declared`(NOT_READY)로 둔다.
- NOTE-3: 공식 명령 거부도 Exit 2를 쓴다(새 종료 코드 없음). 거부 사유의 구분은 결과 문서의 `error_code`와 `checks`로 한다.
- NOTE-4: 일반 replay 매니페스트의 중복 JSON 항목도 이제 거부한다(이전에는 마지막 값 사용). 기존에 만든 매니페스트에는 중복 항목이 없어 영향 없음.
- 신규 미해결 계약 충돌: 없음.

## I. Git / 문서

- 종료 Git 상태(최종 채팅 보고에 실제 명령 출력 기록): Branch `main`, HEAD `97045829…c340` 불변, Staging 0, Commit 0, Push 0. 추적 수정은 위 §A의 수정 파일, 나머지는 미추적 신규 파일.
- 보호 사용자 파일 SHA-256(시작 = 종료 대조는 최종 보고): `TASK.md` `29753cf9…8f56`, `docs/2023recordRule.pdf` `9742caaf…12b0`, `docs/2026-official-baseball-rules.pdf` `f85860f3…7859`.
- F-C1 상태 정리: `FC1_POLICY_CONTRACT_FROZEN_UNCOMMITTED` → **`FC1_POLICY_CONTRACT_FROZEN_COMMITTED`** (`97045829d5836ff6dec8b40109c84d016d92c340`). 과거 보고서·제안서의 원래 문구는 그 시점 기록으로 보존하고 이후 상태를 주석으로 덧붙였다.

## J. 최종 판정

- **`PHASE3_F_FC2A_IMPLEMENTED_WITH_NOTES`** (NOTE-1~4; 핵심 조건 미충족 없음)
- `OFFICIAL_PREFLIGHT_READY` — Preflight 골격과 Fail-Closed 검사는 동작한다(시험 Fixture로만 READY 도달).
- **`OFFICIAL_EXECUTION_NOT_READY`** — 현 승인값으로 공식 실행·공식 replay는 불가능하다.
- `FINAL_EXECUTION_FREEZE_PENDING`
- F-C3 Official Gate: PASS 처리하지 않음. **Phase 3-F Gate·Phase 3 Gate `INCOMPLETE`.**

---

## Addendum — Codex 독립 리뷰 Findings 수정 (2026-10-17, 판정 `CODEX_PHASE3_F_FC2A_REVIEW_REVISIONS_REQUIRED`)

기준 HEAD `97045829…c340` 불변. 범위는 F-C2-A 하네스 수정에 한정(엔진·모델·튜닝·RNG·Commit/CAS·`ConfigHash`·Golden·`ef1/ef2` 무변경, 공식 Seed·Pin 미등록, F-C2-B/C 미착수, Staging/Commit/Push 0, Codex 추가 호출 0). 신규 시험은 `tests/StarPlayer.SimHarness.Tests/CodexFindingsTests.cs`(13개).

### F-01 (MAJOR) Replay가 기록된 Index/GameId를 무시 — 수정
- **원인:** `ReplayCheck.Validate`가 `games[].seed`만 읽고 `SimRunner.Run`이 `config.Spec(i + 1, seed)`로 index·GameId를 새로 만들었다. 매니페스트의 index/game_id를 바꿔도 같은 경기를 재생했다.
- **Red(수정 전):** `R1~R8` 반례가 `Program.Main replay`에서 EXIT 0(`REPLAY MATCH`)로 통과하거나 코드가 달라 11/13 시험 실패(아래 표).
- **수정:** `ReplayCheck.RecordedGames`가 실행 **전에** 기록된 목록을 검증한다 — 비어 있음 `Replay.game_list_mismatch`, 중복 index/GameId `Replay.game_identity_duplicate`, index가 1..n 기록 순서가 아님 `Replay.game_index_mismatch`, GameId가 `(competition_key, cohort, index)`의 규칙 값과 다름 `Replay.game_id_mismatch`. 재계산은 **검증용**일 뿐 기록 값을 덮어쓰지 않는다. `ReplayPlan`은 `Seeds` 대신 기록된 `Games`(`GameSpec`)를 갖고, `ReplayPlan.CheckAgainst(RunConfig)`가 (a) 모든 기록 경기가 설정의 `Spec`과 같고 (b) 그 경기 목록의 `config_hash`가 기록과 같음을 **첫 경기 시작 전에** 확인한다(변경된 Seed는 `Replay.config_hash_mismatch`, EXIT 2). `SimRunner.RunSpecs`가 기록된 `GameSpec`을 그대로 실행한다(`Run(seeds)`는 이를 호출하는 얇은 래퍼, 동작 불변).
- **Legacy 호환:** 기존 Legacy 매니페스트 2개는 `REPLAY MATCH` EXIT 0, run_result_hash `28e0536c…52ec6` / `9e991e89…a8903` 그대로. `ConfigHash`·`run-result-v1`·게임/이벤트/RNG 해시 코드 무변경.
- **동작 변경 1건(NOTE-5):** 기록된 `config_hash`가 기록 경기로 다시 계산한 값과 다른 매니페스트는 이전에는 게임을 돌린 뒤 `REPLAY DIFFERS`(EXIT 1)였고, 이제 실행 전 `Replay.config_hash_mismatch`(EXIT 2)다. 결과 해시만 변조한 매니페스트는 종전대로 EXIT 1.

### F-02 (MAJOR) Attestation과 승인 실행 입력의 연결 불완전 — 수정
- **원인:** `OfficialReplay.Verify`는 Attestation 자기 해시와 승인값(Seed 파일 **해시 문자열**·실행 정책·코호트 해시·Git SHA)만 대조했다. `seed_family`, `game_id_rule`, `park_snapshot`은 어디서도 대조되지 않았고, 핀된 Seed 파일을 **읽지 않아** 해시 문자열이 맞으면 임의의 게임 목록이 인정됐다(기존 시험은 존재하지 않는 `seeds/m.json`으로 성공을 단언했다).
- **수정:** `OfficialReplay.VerifyAgainstSeedFile`(신규, `Replay.Validate`의 공식 분기가 `Verify` 직후 호출): 신뢰 승인(`OfficialApprovals`)의 핀 → **실제 Seed 파일을 읽어** `SeedManifestReader.Read`(원본 SHA-256 = 외부 핀, 엄격 구조 검증) → 파싱 결과와 대조. 저장소 루트가 없으면 `Replay.official_seed_manifest_unavailable`로 거부(해시 문자열만으로 검증된 것으로 치지 않음). 공식 모드(`run.mode == "official"`)인데 envelope가 없으면 `Replay.official_envelope_missing`(일반 replay로 낮출 수 없음). 자기 해시가 맞는 변조 Attestation은 이제 승인 근거가 아니다.
- **Attestation 21개 필드 대조표:**

| 필드 | 검증 출처 |
|---|---|
| `schema_version` | 지원 버전 상수(`Verify`) |
| `git_commit_sha` | 외부 승인 `GitCommitSha`(`Verify`) |
| `execution_policy_id` / `_hash` | 외부 승인(`Verify`) **및** 고정 `OfficialProfile`(`VerifyAgainstSeedFile`) |
| `seed_manifest_path` / `seed_manifest_sha256` | 외부 승인 핀(`Verify`) + 실제 파일 원본 해시(`Read`) |
| `seed_family` | **파싱된 Seed 파일**의 family(`Replay.attestation_seed_family_mismatch`) + 공식 계열 여부 |
| `competition_key` | 매니페스트 `engine.competition_key`(`Verify` 뒤 튜플) + **Seed 파일**(`Replay.competition_key_mismatch`) |
| `game_id_rule` | 지원 규칙 `game-id-v1`(`Replay.game_id_rule_mismatch`) |
| `cohort_id` / `policy_id` / `rules_profile_id` | 매니페스트 + **Seed 파일** 선언(`Replay.seed_manifest_binding_mismatch`) + family별 승인 코호트·정책 |
| `cohort_inputs_hash` | 외부 승인 코호트 해시(`Verify`) + 매니페스트 |
| `model_version` / `tuning_version` / `tuning_hash` / `advancement_policy_version` / `event_budget` | 매니페스트 + 고정 `OfficialProfile`(`Replay.official_profile_mismatch`) |
| `rules_hash` | 매니페스트 + `OfficialProfile.RulesHashes[rules_profile_id]` |
| `park_snapshot` | 고정 `OfficialProfile.ParkText` **및** 매니페스트가 실제로 쓴 구장 기록(`Replay.park_snapshot_mismatch`) |
| `config_hash` | 매니페스트 `config_hash`(`Verify`) + 기록 경기로 재계산(`CheckAgainst`) |
| (게임 목록) | replay 경기 목록 == **Seed 파일 경기 목록**(index·Seed·GameId 순서 포함, `Replay.seed_games_mismatch`) |

  실행 결과와 무관한 진단 Metadata인 필드는 없다(모두 결정성 입력이거나 결속 대상). 구현에 없는 필드는 만들지 않았다.
- **Fixture 승인 주입 불가:** `replay`·`official` 명령은 `OfficialApprovals.Current`만 쓰고 CLI 인자로 핀을 받지 않는다. Fixture 승인으로 만든 일관된 공식 매니페스트도 실제 `replay` 명령에서는 EXIT 2. 실제 `official` 명령은 `PREFLIGHT_REFUSED`/`NOT_EVALUATED`/EXIT 2 유지.
- **기존 시험 1건 강화:** `OfficialAndReplayTests.An_official_envelope_must_also_agree_with_the_manifest_it_is_attached_to`의 첫 단언은 Seed 파일 없이 성공(`IsSuccess`)을 요구했다(= F-02 결함). 이제 파일이 없으면 `Seed.file_missing`, 루트가 없으면 `Replay.official_seed_manifest_unavailable`을 단언한다. 성공 경로는 실제 TEMP Seed 파일을 쓰는 `CodexFindingsTests.A_consistent_official_world_*`가 담당한다. 다른 단언은 변경 없음.

### F-03 (MINOR) Junction/Symlink 경로 이탈 — 수정
- **원인:** `SeedManifestReader.PathProblem`과 `OfficialOutput.Resolve`가 `GetFullPath` + 문자열 접두사만 비교했다. 문자열상 루트 안인 경로가 Junction이면 실제로 밖이다.
- **수정:** `PathSafety.FirstReparsePoint(root, segments)`(신규 `PathSafety.cs`, 약 45줄): 루트 자체와 **존재하는** 각 경로 구성요소(마지막 요소 포함)의 `ReparsePoint` 속성을 검사한다. 없는 구성요소부터는 새 경로로 보고 통과, 검사할 수 없으면 Fail-Closed. Seed 읽기는 `Seed.path_reparse_point`(핀 검사보다 앞서 거부, 바이트 읽기 직전에 재검사), 공식 출력은 `Official.output_reparse_point`(`output_exists`보다 앞). 대규모 파일시스템 추상화 없음.
- **시험(TEMP, 실제 Junction `mklink /J`):** 정상 루트 내부, Traversal·루트 접두사 유사 경로(`official-evil`), 외부로 연결된 Junction(Seed 경로·출력 첫 구성요소·출력 부모·기존 출력 자체), 루트 자체가 링크, 존재하지 않는 정상 새 출력 경로, 실제 `official` 명령(가짜 `StarPlayer.sln` 루트)이 EXIT 2와 `output_reparse_point`를 보고하고 링크 뒤에 아무것도 만들지 않음. 기존 출력 덮어쓰기 금지(`Official.output_exists`)는 기존 시험이 유지.
- **Symlink:** `Directory/File.CreateSymbolicLink` 시험은 이 계정에 심볼릭 링크 권한이 없어 `NOT_RUN`(시험은 권한 예외를 잡아 조용히 반환하므로 xUnit 집계상 PASS 1건이지만 **심볼릭 링크 분기는 실행되지 않았다**). 동일한 `ReparsePoint` 속성 검사를 Junction 시험이 실제로 검증한다.
- **남은 한계(TOCTOU):** 검사는 사용 직전 시점의 확인이지 잠금이 아니다. 현재 공식 출력은 쓰지 않으므로 영향이 없고, 공식 쓰기 활성화(F-C2-B/C) 단계에서 파일 핸들 기반·원자적 생성(`FileMode.CreateNew`) 및 열린 핸들의 최종 경로 재검증이 필요한지 다시 판단해야 한다. 또한 OneDrive 등 클라우드 placeholder도 `ReparsePoint`로 보여 거부된다(공식 경로에서는 의도한 Fail-Closed).

### Red → Green 표

| 시험 (`CodexFindingsTests`) | Red(수정 전) | Green |
|---|---|---|
| `R1_to_R8_*` | 변조 매니페스트가 EXIT 0(`REPLAY MATCH`) | 코드별 거부, 모두 EXIT 2 |
| `R7_*` | Seed 변경 → 게임 실행 후 해시 차이만 | 실행 전 `Replay.config_hash_mismatch` |
| `The_untouched_legacy_manifests_*` | `Games` 부재 | 2개 모두 `CheckAgainst` 통과 |
| `A_consistent_official_world_*` | 해시 문자열만 맞으면 성공 | 핀 + 실제 파일 필요 |
| `A1_to_A3_*`, `A4_to_A7_*`, `A6_A8_A10_*` | 변조가 통과하거나 다른 코드 | 항목별 거부 코드 일치 |
| `A_fixture_approval_cannot_reach_*` | (회귀 가드, 수정 전에도 통과) | 통과 |
| `F03_*` 4개 | `output_reparse_point` 없음 / Junction 통과 | 거부 |
| `F03_a_symlink_*` | — | `NOT_RUN`(권한) |

수정 전 13개 중 **11 FAIL, 2 PASS**(회귀 가드 1 + Symlink NOT_RUN 1); 수정 후 13 PASS.

### 시험 결과(실제)
- `dotnet build StarPlayer.sln -c Release`: 경고 0, 오류 0.
- `dotnet test StarPlayer.sln -c Release`: **2,671 PASS**(Domain 327, Application 127, SimHarness 243, Simulation 741, Infrastructure 1,233), 0 FAIL, 0 SKIP. 기준 2,658 + 신규 13. 기존 시험 삭제·약화 없음(위 1건은 결함 단언을 반대로 강화).
- 실제 CLI: Legacy replay 2개 `REPLAY MATCH` EXIT 0; 기본 스모크 `run --seeds 3,11` EXIT 0 → 그 매니페스트 `replay` EXIT 0; 변조 매니페스트 EXIT 2; `official` EXIT 2(`PREFLIGHT_REFUSED`, `NOT_EVALUATED`; 이 작업 트리에서는 `Git.dirty`가 첫 실패 코드).
- 표적 Sabotage 4/4 검출 후 SHA 복구(복구 SHA = 시작 SHA): S1 Replay Identity 검증 제거 → 2 FAIL(`R1~R8`, `A4~A7`); S2 `seed_family`·`game_id_rule` 대조 제거 → 1 FAIL(`A1~A3`); S3 Seed 파일 경기 목록 대신 해시 문자열만 → 1 FAIL(`A4~A7`); S4 Reparse 검사 무력화(컴파일되는 형태; 첫 시도는 CS0162로 무효였고 재실행) → 4 FAIL(`F03_*`). 전체 Mutation `미실시`, `tools/verify.ps1` `미실시`.

### 상태
- 판정 후보: **`PHASE3_F_FC2A_CODEX_FINDINGS_FIXED_WITH_NOTES`** (NOTE-1~5, Symlink 분기 `NOT_RUN`, TOCTOU 한계).
- `OFFICIAL_PREFLIGHT_READY`(골격·Fail-Closed, Fixture로만 READY) · **`OFFICIAL_EXECUTION_NOT_READY`** · `FINAL_EXECUTION_FREEZE_PENDING`. 3-F·Phase 3 Gate `INCOMPLETE`. 독립 재리뷰(Codex·GPT Chat) `미실시`.

## Addendum 2 — Codex 독립 재리뷰 Findings N-01~N-03 수정 (2026-10-17, 판정 `REVISIONS_REQUIRED`)

기준 HEAD `97045829…c340` 불변. 범위는 하네스 3개 파일(`Replay.cs`·`SeedManifest.cs`·`PathSafety.cs`)과 시험 2개 파일(`CodexFindingsTests.cs`·`OfficialAndReplayTests.cs`). 엔진·모델·튜닝·RNG·Commit/CAS·`ConfigHash`·`ResultHash`·Golden·`ef1/ef2`·Legacy Fixture 변경 없음. 공식 Seed·Pin 등록 없음, 승인 정책 변경 없음, F-C2-B/C 착수 없음, Official 실제 실행 없음, `tools/verify.ps1` 전체는 `미실시`(지시대로). Staging·Commit·Push 0.

### N-01 (MAJOR) Official Replay Downgrade — 수정
- **원인:** 공식 여부를 `official` envelope가 있는지, 또는 `run.mode == "official"`인지로만 판단했다. 둘 다 매니페스트에서 지우거나 고칠 수 있고 `mode`는 `ConfigHash`에 들어가지 않는다. 그래서 envelope를 지우고 mode를 바꾸면 공식 계열 경기(예: Key `fc3-dom-x`, 코호트 `neutral-v1`)가 Legacy 경로로 넘어가 `REPLAY MATCH`를 받을 수 있었다.
- **계약 판단:** Competition Key는 GameId(`game:{key}:{cohort}-{index:D6}`)와 `ConfigHash`에 모두 들어가므로, 바꾸면 다른 실행이 된다. 따라서 매니페스트에서 지울 수 없는 유일한 공식 표지다. 공식 Key 공간은 공식 family id 자체 또는 `{family}-…`로 정했다. 이는 `SeedManifestReader`가 공식 Seed 파일에 허용하는 Key와 정확히 같고, 대소문자를 무시해 변형 Key로 빠져나갈 수 없게 했다. 코호트는 표지가 될 수 없다(`neutral-v1`은 Legacy와 `fc3-dom`이 함께 쓴다). 승인 정책·ADR-008 계약은 바꾸지 않았다(FC-D5 Key 규칙을 그대로 사용).
- **수정:** `SeedFamilies.IsOfficialCompetitionKey`를 새로 만들었다. `ReplayCheck.Validate`는 스키마 검사 바로 뒤, 다른 어떤 검사보다 먼저 다음을 확인한다: envelope가 없는데 mode가 `official`이거나 Key가 공식 Key이면 `Replay.official_envelope_missing`. envelope가 있는데 mode가 `official`이 아니면 `Replay.official_mode_mismatch`(신규 코드). 그 뒤의 공식 검증(`Verify` → `VerifyAgainstSeedFile`)은 그대로다. `"official": null`은 부재로 보지 않고 `Replay.manifest_invalid`로 거부한다.
- **Legacy 호환:** `sim`·`fc2-dry*` Key는 envelope 없이 종전과 같이 동작한다. Legacy Fixture 2개는 실제 CLI에서 `REPLAY MATCH`이고 해시도 같다(`28e0536c…2ec6`, `9e991e89…8903`).
- **기존 시험 2건 Fixture 보정:** `OfficialAndReplayTests.An_official_manifest_is_never_replayed_as_official_while_nothing_is_frozen`와 `…An_official_envelope_must_also_agree_with_the_manifest_it_is_attached_to`는 Legacy 매니페스트(mode `smoke`)에 envelope만 붙였다. 새 계약에서는 이것이 `official_mode_mismatch`이므로, 원래 대상인 단언을 그대로 검사하도록 mode를 `official`로 설정했다. 그리고 mode `smoke` + envelope 거부 단언 1개를 추가했다. 기존 단언의 기대 코드는 바꾸거나 지우지 않았다.
- **NOTE-6(동작 변경):** envelope가 있고 mode가 공식이 아닌 매니페스트는 이제 envelope 검증 코드 대신 `Replay.official_mode_mismatch`로 거부된다. F-C2-A에는 공식 매니페스트를 기록하는 경로가 없으므로, 실제로 이런 매니페스트가 만들어지는 경로는 없다.
- **남은 범위:** 비공식 Key로 처음부터 다시 실행해 만든 매니페스트(새 GameId·해시)는 단지 다른 비공식 실행일 뿐이다. 공식 replay 신뢰를 얻지 못하므로 Downgrade가 아니다.

### N-02 (MINOR) 루트 상위 Junction/Symlink — 수정
- **원인:** `PathSafety.FirstReparsePoint`가 루트 자체와 그 아래만 검사했고, 루트의 상위 디렉터리는 검사하지 않았다.
- **수정:** `Path.GetFullPath(root)`의 Volume Root부터 시작해 각 상위 디렉터리 → 루트 → 하위 구성요소 순으로 검사한다. 존재하지 않는 경로와 Fail-Closed 규칙은 같다. Seed 읽기와 공식 출력이 모두 이 함수 하나를 쓰므로 한 곳만 고쳤다.
- **시험(TEMP, 실제 `mklink /J`):** 부모가 Junction인 루트(끝 구분자가 있는 경우와 없는 경우)에서 Seed `Seed.path_reparse_point`·출력 `Official.output_reparse_point`, 두 단계 위 Junction, 상위가 모두 일반 디렉터리인 정상 루트는 영향 없음.
- **한계:** 검사 후 사용까지의 TOCTOU 창은 F-03 NOTE와 같다. `subst`·네트워크 드라이브처럼 드라이브 문자 자체를 다른 위치에 매핑하는 경우는 reparse point가 아니므로 이 검사가 잡지 못한다.

### N-03 (MINOR) Symlink 시험 증거 — 집계 수정, **실제 Symlink 증거는 미충족**
- **원인:** 권한 예외를 잡고 `return`해서 xUnit이 PASS로 집계했다.
- **수정:** 그 시험을 지우고 `SymlinkFactAttribute`(시험 프로젝트 안, 패키지 추가 없음)를 도입했다. TEMP에 실제 링크를 만들어 보는 Probe를 종류별(디렉터리·파일)로 1회 실행한다. 만들 수 없으면 xUnit `Skip`에 `NOT_RUN: …` 사유를 기록하고 `건너뜀`으로 집계한다(`verify.ps1`은 skipped > 0을 INCOMPLETE로 판정). Probe는 성공했는데 본문에서 링크 생성이 실패하면 예외를 잡지 않으므로 FAIL이다. 시험 2개: `N03_a_directory_symlink_*`(Seed 경로 구성요소, 링크가 루트 자체, 링크가 루트 상위 = N-02)와 `N03_a_file_symlink_*`(Seed 파일 자체; 링크 뒤 바이트가 Pin과 같아도 거부).
- **이 환경:** 일반 계정이고 개발자 모드가 꺼져 있다. 두 Probe 모두 `IOException: 클라이언트가 필요한 권한을 가지고 있지 않습니다`로 실패해 **2 SKIP(NOT_RUN)**이다. 시험 본문은 컴파일만 확인했고 실행은 `미검증`이다. **디렉터리·파일 Symlink의 실제 검증 증거는 이번에도 없다.** 증거를 얻으려면 개발자 모드 또는 `SeCreateSymbolicLinkPrivilege`가 있는 계정에서 `dotnet test tests/StarPlayer.SimHarness.Tests -c Release --filter "FullyQualifiedName~N03_"`를 실행해야 한다.
- **미해결 기준 충돌(소유자 결정 필요):**
  - **Issue:** 이 환경에서 SimHarness가 0 SKIP 기준을 충족할 수 없다.
  - **Reason:** 이 계정은 Symlink를 만들 수 없고, xUnit 2.9에서 PASS가 아닌 비실패 결과는 Skip뿐이다.
  - **Recommended Option:** NOT_RUN Skip을 유지하고, 권한이 있는 환경에서 한 번 실행한 증거를 Gate 근거로 첨부한다.
  - **Alternative:** Symlink 시험을 기본 실행에서 분리(Trait 필터)하고, 권한 환경 전용 명령으로 실행한다. 또는 권한이 없으면 FAIL로 처리한다.
  - **Impact:** 현재 `verify.ps1` U5는 이 환경에서 INCOMPLETE가 된다.

### Red → Green

| 시험 | Red(수정 전 코드) | Green |
|---|---|---|
| `N01_an_official_competition_key_without_its_envelope_*` | FAIL — envelope를 지우고 mode를 바꾼 매니페스트를 `Validate`가 받아들임 | PASS(`official_envelope_missing`, CLI EXIT 2, 게임 실행 전) |
| `N01_an_envelope_with_a_mode_other_than_official_*` | FAIL — mode `smoke` + envelope 통과 | PASS(`official_mode_mismatch`) |
| `N01_the_official_competition_key_namespace_*` | FAIL(분류 함수가 없어 `false`를 돌려주는 임시 Stub으로 측정) | PASS |
| `N01_legacy_and_dry_run_keys_*` | PASS(회귀 가드) | PASS |
| `N02_a_link_ABOVE_the_root_*` | FAIL — 상위 Junction 아래 루트의 Seed 읽기 성공 | PASS |
| `F03_a_symlink_*`(구) → `N03_*` 2개 | 권한이 없는데 `통과` 1건(가짜 PASS) | `건너뜀` 2건, 사유 `NOT_RUN: …` |

`CodexFindingsTests` 기준 수정 전 18개 중 4 FAIL / 14 PASS(가짜 PASS 1건 포함), 수정 후 23개 중 21 PASS / 2 SKIP / 0 FAIL. Red 단계 출력은 작업 중 Scratch 로그로만 보관했고 저장소에는 넣지 않았다.

### 시험 결과(실제)
- `dotnet build StarPlayer.sln -c Release`: exit 0, 경고 0, 오류 0.
- `dotnet test StarPlayer.sln -c Release --no-build`: exit 0. **2,675 PASS, 0 FAIL, 2 SKIP / 전체 2,677**(Domain 327, Application 127, Simulation 741, Infrastructure 1,233, SimHarness 247 PASS + 2 SKIP / 249). 이전 2,671 대비: 신규 7개(N01 4, N02 1, N03 2), 삭제 1개(가짜 PASS였던 `F03_a_symlink_*`).
- 실제 CLI(`StarPlayer.SimHarness.dll`): Legacy replay 2개 `REPLAY MATCH`, EXIT 0. Legacy 매니페스트의 Key·GameId만 `fc3-dom-x`·`FC3-CAL-L1`·`fc3-int`로 바꾸고 mode를 `smoke`로 둔 Downgrade는 모두 `Replay.official_envelope_missing`, EXIT 2. `fc2-dry-x`(비공식)는 공식 Gate를 통과하고, Key만 바꿨기 때문에 다음 검사인 `Replay.config_hash_mismatch`에서 EXIT 2(예상대로).
- Official 거부 경로: `official` 명령의 NOT_READY(EXIT 2)는 기존 시험(`A_fixture_approval_cannot_reach_*`, `F03_the_official_command_*`)이 실제 하위 프로세스로 확인한다(이번 작업에서 별도 수동 실행은 하지 않음).
- `미실시`: `tools/verify.ps1` 전체, Sabotage(이번에는 수정 전 코드에 대한 Red로 대신함), 독립 재리뷰.

### N-04 후속 NOTE (미수정, 기록만)
- `TASK.md` 요약을 기준으로 해석한 내용이다. Calibration(`fc3-cal`)은 블록 경계에서 PA 목표에 도달하면 조기 종료한다(`SeedManifest.AfterGames`). 따라서 실제로 실행되는 경기는 Seed 파일의 앞쪽 블록들일 수 있다. 그런데 `OfficialReplay.VerifyAgainstSeedFile`은 Seed 파일 전체 목록과 `SequenceEqual`을 요구한다. 즉 조기 종료한 공식 calibration 실행은 공식 replay를 통과할 수 없다. 공식 실행을 활성화하기 전(F-C2-B/C·Final Execution Freeze)에 블록 접두 비교 계약으로 해결해야 한다. 이번 작업에서는 구현하지 않았다.

### 상태
- 판정 후보: **`PHASE3_F_FC2A_CODEX_REREVIEW_N01_N03_FIXED_WITH_NOTES`** (N-03 실제 Symlink 증거 `NOT_RUN`, N-04 미해결, NOTE-6). 승인 아님. Codex·GPT Chat 재리뷰 `미실시`.
- `OFFICIAL_EXECUTION_NOT_READY` · `FINAL_EXECUTION_FREEZE_PENDING` 변경 없음. 3-F·Phase 3 Gate `INCOMPLETE`.
- 보호 사용자 파일 SHA-256(시작 = 종료): `TASK.md` `370f8556…4328`(이번 지시서; 이전 Addendum의 `29753cf9…`는 이전 지시서), `docs/2023recordRule.pdf` `9742caaf…12b0`, `docs/2026-official-baseball-rules.pdf` `f85860f3…7859`.

## Addendum 3 — N-05 Null Competition Key 수정 + Symlink 증거 재확인 (2026-10-17, 소유자 `TASK.md` "Final Findings Resolution")

### N-05 (Null 필수 문자열) — 수정
- **원인:** `ReplayCheck.Validate`가 `engine.competition_key`를 `GetString()!`로 읽었다. JSON `null`은 `GetString()`이 null을 돌려주므로(예외 없음) `SeedFamilies.IsOfficialCompetitionKey(null)`의 `key.Equals`에서 `NullReferenceException`이 났고, `Validate`의 `catch`는 `KeyNotFound/InvalidOperation/Format`만 잡아 처리되지 않은 예외로 CLI가 종료됐다.
- **수정(1곳):** `Replay.cs`에 `Required(parent, name)`(null이면 `FormatException`)를 두고 `GetString()!`를 쓰던 네 필드(`engine.competition_key`, `run.config_hash`, `run.run_id`, `results.run_result_hash`)에 적용했다. 기존 `catch`가 `Replay.manifest_invalid`로 바꾼다. 같은 `!` 패턴의 형제 필드를 함께 막았다. Official/Legacy 판별 정책, Hash 계약, 기존 오류 코드는 변경 없음.
- **회귀 시험:** `N05_a_null_required_string_is_a_manifest_refusal_not_an_exception`. 네 필드 각각에 대해 (a) official envelope 경로, (b) Legacy 경로 모두 `Replay.manifest_invalid`, (c) CLI `replay --manifest` EXIT 2. 추가로 `cohort_id`·`policy_id`·`rules_profile_id`·`advancement_policy_version`·`cohort_inputs_hash`가 null이어도 예외 없이 실패 값 + EXIT 2.
- **Red→Green:** 수정 전(`Required`를 `GetString()!`로 되돌린 임시 상태) 같은 시험이 FAIL(official 경로가 `Replay.manifest_invalid` 대신 `Replay.game_id_mismatch`를 반환; Legacy 경로는 위 NRE 경로). 수정 후 PASS. 임시 되돌림은 복원했다.
- **범위 메모:** `run.mode`는 Legacy replay에서 라벨일 뿐 쓰이지 않아 null이어도 기존대로 수락된다(예외 아님). 이 동작은 바꾸지 않았다.

### N-01~N-03 회귀 여부
- N-01(`N01_*` 4개)·N-02(루트 상위 Reparse 시험)·N-03(Symlink 집계) 시험 모두 이번 전체 실행에서 PASS/SKIP 상태 불변. 회귀 없음.

### Symlink 실제 실행 여부 — **미실행(SKIP 2), 품질 Gate INCOMPLETE**
- 이번 세션에서도 두 Probe(`Directory.CreateSymbolicLink`, `File.CreateSymbolicLink`)가 `IOException: 클라이언트가 필요한 권한을 가지고 있지 않습니다`로 실패했다. 일반 계정이며 개발자 모드·관리자 권한·보안 설정은 **변경하지 않았다**(지시 준수).
- 따라서 `N03_a_directory_symlink_*`·`N03_a_file_symlink_*`는 `NOT_RUN` 사유와 함께 SKIP이고 **PASS로 처리하지 않는다.** 0 SKIP 기준 미충족 → 해당 품질 Gate `INCOMPLETE`. 해소하려면 소유자가 Symlink 권한이 있는 계정/개발자 모드에서 실행하도록 승인해야 한다.

### 시험 결과(실제)
- `dotnet build StarPlayer.sln -c Release`: 경고 0, 오류 0.
- `dotnet test StarPlayer.sln -c Release --no-build`: 0 FAIL, **2,676 PASS + 2 SKIP / 전체 2,678**(Domain 327, Application 127, Simulation 741, Infrastructure 1,233, SimHarness 248 PASS + 2 SKIP / 250). 직전 2,675 대비 신규 +1(N-05). 기존 시험 삭제·약화 없음.
- `미실시`: `tools/verify.ps1`(지시로 금지), 공식 실제 실행(금지), Codex·GPT Chat 재리뷰.

### 상태
- 판정 후보: N-05 수정, N-01/N-02 유지, **N-03 실제 Symlink 증거 `NOT_RUN`(Gate INCOMPLETE)**, N-04·NOTE-6 변경 없음. 승인 아님.
- `OFFICIAL_EXECUTION_NOT_READY` · `FINAL_EXECUTION_FREEZE_PENDING` 변경 없음. 3-F·Phase 3 Gate `INCOMPLETE`. 기준 HEAD `97045829` 불변, 미스테이징·미커밋·미푸시.
- 보호 사용자 파일 SHA-256(시작 = 종료): `TASK.md` `4f1b4b52…`(이번 지시서), `docs/2023recordRule.pdf` `9742caaf…`, `docs/2026-official-baseball-rules.pdf` `f85860f3…`.

## Addendum 4 — Final Validation 시도 (2026-10-17, 소유자 `TASK.md` "Final Validation")
- **Symlink:** `N03` 2건을 다시 실행했으나 두 Probe 모두 `IOException: 클라이언트가 필요한 권한을 가지고 있지 않습니다`로 실패 → 2 SKIP(`NOT_RUN`) 유지. 승인된 Symlink 가능 환경은 확보되지 않았고 시스템 권한·개발자 모드는 변경하지 않았다. 0 SKIP 품질 Gate **INCOMPLETE**.
- **`tools/verify.ps1`:** 지시서가 "Symlink 테스트 성공 시" 조건으로 승인했으므로 조건 미충족 → `미실시`.
- **재확인:** Release 빌드 경고 0·오류 0; `dotnet test` 0 FAIL, 2,676 PASS + 2 SKIP / 2,678(Addendum 3과 동일). Legacy replay 2개 CLI `REPLAY MATCH`(해시 `28e0536c…`, `9e991e89…`) 불변. N-05·N-01~N-03·Official 거부 경로 시험 PASS.
- 코드·문서 변경 없음(이 Addendum 제외). HEAD `97045829` 불변, 미스테이징·미커밋·미푸시. Closeout **불가**(Symlink 증거 + `verify.ps1` 대기).

## Addendum 5 — Final Closeout 검증 (2026-10-17, 소유자 `TASK.md` "최종 Closeout 검증")
- **Symlink 실제 실행:** 소유자가 환경을 마련한 뒤(시스템 설정은 Claude Code가 변경하지 않음) `N03_a_directory_symlink_*`·`N03_a_file_symlink_*` 모두 **PASS**(각 15 ms·3 ms). N-05 시험도 PASS. SimHarness 250 PASS / 0 SKIP.
- **`dotnet test StarPlayer.sln -c Release`:** 0 FAIL, **2,678 PASS / 0 SKIP / 전체 2,678**(Domain 327, Application 127, SimHarness 250, Simulation 741, Infrastructure 1,233). 소유자의 로컬 PowerShell 결과(빌드 경고 0·오류 0, 2,678 PASS)와 일치. 0 SKIP 품질 Gate **충족**.
- **`tools/verify.ps1` 전체:** `GATE: PASS`(소유자 승인 하에 Claude Code가 실행). T1~T3·B1~B3·U1~U5(SimHarness 250/0 SKIP 포함)·G1~G4·Windows export·Exported program 3종·문서 링크(141개 파일) 전부 PASS, INFO 2건(변경 31개 항목 당시 집계, 잔존 Build-server 없음). 부수 효과: 스크립트가 추적 파일 `docs/reports/PHASE_2_VERIFY_LOG.md`를 덮어썼다(설계된 출력; 이전 Phase 2 시점 기록이 이번 실행 기록으로 대체됨). Git 항목은 스크립트가 `9704582 (dirty working tree)`로 기록.
- **회귀:** N-05, N-01~N-03, Legacy Replay(CLI `REPLAY MATCH` 2건 불변), Official 거부 경로 시험 모두 PASS. 공식 실제 실행 안 함.
- **Git/보호 파일:** HEAD `97045829` 불변, staged 0, 미커밋·미푸시. 변경 항목 32개(F-C2-A 31개 + `PHASE_2_VERIFY_LOG.md`). `TASK.md` `4f1b4b52…`, `docs/2023recordRule.pdf` `9742caaf…`, `docs/2026-official-baseball-rules.pdf` `f85860f3…` 시작 = 종료.
- **Closeout 판정:** F-C2-A 구현·시험·`verify.ps1` Gate는 충족. 남은 것은 NOTE(N-04 Calibration 조기 종료와 Seed 목록 `SequenceEqual` 불일치 후속, NOTE-6)와 독립 재리뷰(Codex·GPT Chat `미실시`)뿐이다. 3-F·Phase 3 Gate는 F-C2-B·F-C2-C·Final Execution Freeze 때문에 `INCOMPLETE` 유지. `승인 완료`는 기록된 리뷰만 설정할 수 있으며 이 문서는 `검토 대기`다.

## Addendum 6 — 검증 로그 정리 (2026-10-17, 소유자 `TASK.md` "Closeout Evidence Cleanup"; Codex 최종 재리뷰 BLOCKER/MAJOR 없음, MINOR 1건)
- **문제:** Addendum 5의 `verify.ps1` 실행이 추적 파일 `docs/reports/PHASE_2_VERIFY_LOG.md`(2026-10-11 Phase 2 원본)를 덮어썼고, `build/verify/`도 다음 실행 때 지워진다.
- **조치:** (1) 덮어써진 로그를 `docs/reports/PHASE_3F_FC2A_VERIFY_LOG.md`로 바이트 동일 복사(`cmp` 일치), (2) `build/verify/` 28개 파일 전체를 `build/verify-archive/PHASE_3F_FC2A_run1/`로 복사(`diff -rq` 차이 없음; `build/`는 Git 무시이므로 로컬 보존), (3) 해시 목록을 `docs/reports/PHASE_3F_FC2A_VERIFY_EVIDENCE.md`에 기록, (4) 보존 확인 후 `git restore -- docs/reports/PHASE_2_VERIFY_LOG.md`로 Phase 2 로그를 HEAD 원본으로 복원(SHA-256 `871aaab5…` = HEAD blob, 파일 diff 없음).
- **보존 해시:** 새 로그 `3fb21bf2…`, `verify-result.json` `83966cff…`. 전체 목록은 Evidence 문서 참조.
- 코드·정책 변경 없음, 시험·`verify.ps1` 재실행 없음. 이 Addendum은 Addendum 5의 "32개 항목 / PHASE_2_VERIFY_LOG 갱신" 서술을 정정한다: `git status --short` 최종 33개 항목 = 수정 14 + 신규 19(F-C2-A 구현 파일, `TASK.md`·PDF 2개, 신규 로그·Evidence 문서 2개 포함); `PHASE_2_VERIFY_LOG.md`는 더 이상 변경 목록에 없다. 이번 `TASK.md` SHA-256은 `fa6aaa0f…`, PDF 2개는 `9742caaf…`·`f85860f3…`(불변).
- 상태는 `검토 대기` 유지, 승인 아님. Stage/Commit/Push 없음.
