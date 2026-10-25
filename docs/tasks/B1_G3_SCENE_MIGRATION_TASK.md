# B1 G3 Godot Scene 정식 변경안 (unique_id 저장)

- 작성: Claude Code, 2026-10-26. 근거: OWNER 지시 "G3 Godot Scene 정식 변경안 작성"(2026-10-26, 채팅)
- **성격:**
  - 변경안 작성·검증입니다.
  - 새 코드 기준본 수용, 공식 G3 PASS, Build Freeze가 **아닙니다**.
  - Codex READ-ONLY 검수 전에는 Stage·Commit·Push·Tag를 하지 않습니다. 변경은 작업 트리에만 둡니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준

| 항목 | 값 |
|---|---|
| B1 Tag·Commit(유지) | `b1-code-accepted-2026-10-26` → `7bdde7dd091f9a66957bc740c77d470c7732b3ad` |
| 최근 G3 진단 보고 Commit | `cd5f4137af562a99927d8cb549a07cd6b9c39bf6` |
| 근거 보고서 | [B1_G3_GODOT_PCK_REPRO_REPORT.md](../reports/B1_G3_GODOT_PCK_REPRO_REPORT.md)(원인: `unique_id` 누락. 재저장 진단본 PCK `3a187e19…` 4회 동일) |
| 변경 불가 | 기존 Tag, 구현 Manifest 213개 파일(`B1_IMPLEMENTATION_MANIFEST.*`), 승인 계약, OWNER 원문, 루트 `TASK.md`, 게임 로직·RNG·Hash·Replay, Golden 기대값 |

## 2. 변경 범위

**Scene 4개만 대상입니다.** 진단 clone에서 PCK 재현성을 입증한 R1 바이트(`E:\Madegame\g3x\pck-out\R1_pack\`)를 그대로 씁니다.

| Scene | 경로 | 후보 SHA-256(R1과 같아야 함) |
|---|---|---|
| Main | `game/Bootstrap/Main.tscn` | `3501aba8fd59b5450855bca47bc2de66f7b6f1b251df3ac84d04d64f9787edf4` |
| MainMenu | `game/UI/MainMenu/MainMenu.tscn` | `79dbc1dd026645d12fb05963e90130aa340403b3247f839f51613b727395192f` |
| PlayerDiagnosticsScreen | `game/UI/PlayerDiagnostics/PlayerDiagnosticsScreen.tscn` | `defd9603508305d24ae864113b786c698927489e0756497870df043262f9f85c` |
| GameDiagnosticsScreen | `game/UI/GameDiagnostics/GameDiagnosticsScreen.tscn` | `98886edae773b66fba879ca11a2c7156d7b3983a4523d70d9991418f23634490` |

- **허용 변경:**
  - 노드별 `unique_id` 저장
  - Godot 4.7.2 직렬화로 사라진 헤더 `load_steps`
  - LF 개행
- **그 외 변경 금지:** 노드 계층·이름·형식·속성, 리소스 참조, Script, 동작
- **개행 정책 후보:** `.gitattributes`에 `*.tscn text eol=lf`
  - 먼저 영향 파일을 조사합니다. 추적 중인 `.tscn`은 위 4개뿐입니다.
  - 다른 파일이 바뀌면 일괄 정규화하지 않고 범위를 좁힙니다.
  - `.gitattributes`는 구현 Manifest 213개 중 하나이므로, 변경은 신규 Manifest에 따로 기록합니다.

## 3. 검증

1. **diff:** 원본 대비 전체 diff에 허용 변경 외 항목이 0개여야 합니다(정규화 diff·바이트 비교 모두).
2. **unique_id:** 모든 노드에 있어야 하고 Scene 안에서 중복이 없어야 합니다(Scene 사이 중복도 조사).
   - `ext_resource` 참조와 부모 경로가 정상이어야 합니다.
   - Godot에서 로드·인스턴스화가 정상이어야 합니다.
   - Export `.scn`의 `node_ids`가 저장된 `unique_id`와 같아야 하고, 반복 Export에서 재생성되지 않아야 합니다.
3. **개행:**
   - `git check-attr`, `git ls-files --eol`
   - `git hash-object`(필터 적용)와 `--no-filters`가 같아야 합니다.
   - 진단 clone에서 체크아웃 변환 결과가 LF인지 확인합니다(`hash-object -w` + `cat-file --filters`, Stage 없음).
4. **PCK:** Godot `4.7.2.stable.mono.official.ed1daf0bf`와 검증된 Template을 씁니다.
   - 같은 폴더에서 독립 Export 2회, 다른 폴더에서 2회
   - Import Cache를 남긴 채 다시 Export(조건 C)
   - 모든 PCK의 SHA-256·byte_length와 나머지 Export 파일의 개수·SHA를 비교합니다.
   - `node_ids` 제외 비교나 사후 패치는 금지합니다.
5. **회귀:** 변경을 적용한 깨끗한 clone에서 전체 `tools/verify.ps1`을 실행합니다.
   - 3,041개 시험, Golden 4, G2~G4, E2~E4가 포함됩니다.
   - Dry Pilot 9 Run을 실행합니다.
   - E3 7/7, `hidden_identifier_leaks=0`, E3·E4 스크린샷 전부, 원본(`7bdde7d`)과의 화면 비교, Headless·Export 오류·경고, 반환 코드, 원시 로그를 기록합니다.
6. **신규 SHA Manifest:** `docs/reports/B1_G3_SCENE_MIGRATION_MANIFEST.md`·`.sha256`(변경 파일의 이전·새 SHA)

## 4. 중단 기준

- 허용 범위 밖의 Scene 변경, 노드 수 불일치, `unique_id` 누락·중복
- 대상 4개 밖의 파일이 개행 규칙으로 바뀌는 경우
- 같은 입력에서 PCK가 1바이트라도 다른 경우
- verify `GATE`가 PASS가 아니거나 시험 FAIL·SKIP이 있거나 Dry Pilot이 불일치하는 경우
- E3·E4 증거가 빠진 경우(FULL PASS로 표시하지 않음)
- 게임 로직·Hash·Golden이 바뀌는 경우

## 5. 후속 영향 (기록만)

- **새 소스 Commit:** Scene 변경은 새 소스 Commit을 만듭니다. 기존 B1 Tag는 역사적 코드 수용 기준본으로 남습니다.
- **다시 계산할 값:** 새 Commit을 쓰면 다음을 다시 계산·검증해야 합니다.
  - `source_git_sha`, SourceLink Commit, DLL·PDB·Verifier SHA
  - Build Provenance, Test Profile 결속, ApprovalRecord Commit 결속
  - 이번에는 등록·변경하지 않습니다.
- **B-4 후보:**
  - SourceLink URL `https://github.com/junk1901/Star_player.git`: 저장소 존재는 OWNER 확인. B1 Commit의 원격 조회는 미확인. Push 없음.
  - Artifact 후보 109개(1·1·4·91·12)
  - TRUST_CONFIG 미정, Clause ID·공식 출력 경로 OWNER_VALUE_PENDING
  - Apphost는 공식 실행 명령에서 쓰지 않는다는 것이 확인된 경우에만 제외합니다.
- **§18.2 Environment Profile:** 등록하지 않습니다. 필요한 실측값과 N12 Golden을 확보한 뒤 별도 승인을 받습니다.
