# B1 G3 Godot PCK 재현성 조사와 .NET 빌드 정책 후보 정리

- 작성: Claude Code, 2026-10-26. 근거: OWNER 지시 "G3 전체 진단 결과 수용 및 Godot PCK 재현성 조사"(2026-10-26, 채팅)
- **성격:**
  - 진단입니다.
  - 운영 G3 승인, Build Freeze, Artifact Schema 변경, 정식 Scene 수정이 **아닙니다**.
  - 결과를 제출하고 멈춥니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 재개용 기준

| 항목 | 값 |
|---|---|
| B1 코드 Tag·Commit | `b1-code-accepted-2026-10-26` → `7bdde7dd091f9a66957bc740c77d470c7732b3ad`(변경 금지) |
| 이전 결과 | [전체 빌드 진단](../reports/B1_G3_FULL_BUILD_DIAGNOSTIC_REPORT.md), [SimHarness 실험](../reports/B1_G3_REPRO_EXPERIMENT_REPORT.md), [조사](../reports/B1_G3_BUILD_REPRODUCIBILITY_INVESTIGATION.md) |
| 이번 보고서 | `docs/reports/B1_G3_GODOT_PCK_REPRO_REPORT.md` |
| 실험 위치(저장소 밖) | `E:\Madegame\g3x\`: clone `pa`·`pb`·`pcxlong`·`pd`, `tools\run_pck.ps1`·`resave.gd`·`pck_diff.py`·`roles_candidates.py`, `pck-out\<ID>\` |

**알려진 사실(전체 빌드 진단 6절):**

- `StarPlayer.pck`는 같은 경로 반복 Export에서도 다릅니다. 차이는 변환된 장면 4개의 `node_ids`뿐입니다.
- 원본 `.tscn` 4개에는 `unique_id`가 없고 `load_steps`가 있습니다(Godot 4.6 이전 형식).
- 참고 자료: Godot 이슈 #115971, TSCN 형식 문서(4.6+ `unique_id`). 알려진 이슈와 같다고 가정하지 않고 실험으로 확인합니다.

## 2. 실험

| ID | 내용 |
|---|---|
| A | 수정하지 않은 장면, 같은 경로(`pa`)에서 Export 2회 |
| B | 진단 clone `pb`에서 장면 4개를 Godot 4.7.2로 다시 저장한 뒤, 같은 경로에서 Export 2회 |
| C | B와 같은 수정 장면 바이트를 경로 길이가 다른 `pcxlong`에 복사해 Export 2회 |
| D | `pd`에서 장면을 하나씩만 수정 바이트로 바꿔 Export 2회씩 |

- **다시 저장하는 방법:** `godot --headless --path game --script E:\Madegame\g3x\tools\resave.gd -- <res://장면…>`
  - Godot의 `ResourceLoader.load`와 `ResourceSaver.save`로 같은 경로에 다시 씁니다.
  - 편집기 UI의 Save Scene이나 "Upgrade Project Files"와 같은 동작인지는 확인 대상입니다.
- **Export 조건:** 이전 진단과 같습니다.
  - 빌드 전 `bin/obj`·`game/.godot`·`build/export`를 삭제합니다.
  - `--build-solutions --quit` 후 `--export-release "Windows Desktop"`을 실행합니다.
  - 환경 변수 `ContinuousIntegrationBuild=true`를 줍니다.
- **기록:** `.tscn` 전체 diff, `unique_id` 추가 수, 원본·실험 SHA, PCK SHA·크기, 반복 비교, `node_ids`, 다른 Export 파일 차이
- **Godot 실행 검사:** verify G2~G4(headless)와 E2~E4(Export cold copy)와 같은 검사를 실행합니다.
- **수용 기준 후보:** PCK는 같은 승인 입력과 통제된 Export 환경에서 같은 경로·다른 경로 모두 바이트가 완전히 같아야 합니다.
  - `node_ids`를 비교에서 빼거나 사후에 패치해 PASS로 만들지 않습니다.
  - 다시 저장해서 해결되면, 정식 장면 수정은 별도 OWNER 승인 대상입니다.

## 3. 함께 정리할 것

- **B-4 .NET 빌드 후보 절차:** Clean Checkout, SDK 8.0.425, Release, `Deterministic=true`, `ContinuousIntegrationBuild=true`(명령에서 전달), `bin/obj` 초기화, SourceLink Commit·URL 확인, 경로 정규화 확인, 두 독립 폴더 SHA 동일
- **SourceLink URL 후보:** `https://github.com/junk1901/Star_player.git`(**OWNER_VALUE_PENDING**). 공식 검증에서는 승인 원격에서 해당 Commit을 확인할 수 있어야 합니다.
- **Artifact 역할별 파일 후보(§18.3 닫힌 역할 유지):**
  - `deps.json`으로 실제 소비 파일을 정합니다.
  - coverage 대응표 6개는 실행·시험·종속 집합에서 제외하되, 목록·이유·SHA를 남깁니다.
  - PDB는 별도 진단 증거로 둡니다. Godot PCK는 DEPENDENCY에 넣지 않습니다.
- **빌드 환경 기록:** 운영 `simharness-environment-profile-v1`(§18.2)과 분리합니다. 진단값으로 운영 Environment Profile이나 N12 Golden을 승인하지 않습니다.

## 4. 금지

- B1 Tag 이동, 기준본 코드·Scene 수정(진단 clone의 Scene만 수정 가능)
- 계약·OWNER 원문·루트 `TASK.md` 변경, Artifact Schema 변경
- Build Freeze·G3 PASS 선언, G1~G9 착수, Remote Push
