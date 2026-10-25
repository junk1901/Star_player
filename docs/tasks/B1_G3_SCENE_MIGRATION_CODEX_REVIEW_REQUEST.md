# Codex READ-ONLY 표적 검수 요청 — B1 G3 Godot Scene 변경안

- 작성: Claude Code, 2026-10-26. 근거: OWNER 지시 "G3 Godot Scene 정식 변경안 작성" 11항
- **요청 범위:**
  - READ-ONLY 검수입니다. 파일·Git 변경, Stage·Commit·Push·Tag는 하지 않습니다.
  - 판정은 PASS / FIX_REQUIRED(항목별 근거)로 주십시오.
  - 이 요청서는 OWNER가 전달하며, Claude가 Codex에 직접 보내지 않습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준과 대상

| 항목 | 값 |
|---|---|
| 기준 Commit | `7bdde7dd091f9a66957bc740c77d470c7732b3ad`(Tag `b1-code-accepted-2026-10-26`, 변경 없음). 저장소 HEAD는 문서 Commit `cd5f4137af562a99927d8cb549a07cd6b9c39bf6`이며 코드·Scene은 `7bdde7d`와 같음 |
| 변경 후보 | `E:\Madegame\Star_player` 작업 트리의 미커밋 변경 5개 |
| 변경 파일 | `.gitattributes`, `game/Bootstrap/Main.tscn`, `game/UI/MainMenu/MainMenu.tscn`, `game/UI/PlayerDiagnostics/PlayerDiagnosticsScreen.tscn`, `game/UI/GameDiagnostics/GameDiagnosticsScreen.tscn` |
| 새 SHA | [B1_G3_SCENE_MIGRATION_MANIFEST.md](../reports/B1_G3_SCENE_MIGRATION_MANIFEST.md)·`.sha256` |
| 근거·결과 | [TASK](B1_G3_SCENE_MIGRATION_TASK.md), [보고서](../reports/B1_G3_SCENE_MIGRATION_REPORT.md), 원인 진단 [B1_G3_GODOT_PCK_REPRO_REPORT.md](../reports/B1_G3_GODOT_PCK_REPRO_REPORT.md) |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player diff 7bdde7dd091f9a66957bc740c77d470c7732b3ad -- .gitattributes game
git -C E:/Madegame/Star_player status --porcelain
sha256sum -c docs/reports/B1_G3_SCENE_MIGRATION_MANIFEST.sha256
```

## 2. 확인을 요청하는 항목

1. **Scene diff 범위:** 4개 Scene의 변경이 다음 세 가지뿐인지 확인해 주십시오. 노드 계층·이름·형식·속성·리소스 참조·Script 변경이 0인지도 봐 주십시오.
   - 헤더 `load_steps` 제거
   - 51개 `[node …]` 행 끝의 `unique_id=<정수>`
   - LF 개행
2. **unique_id:**
   - 노드마다 하나씩 있는지, Scene 안팎에서 중복이 없는지
   - 값이 저장된 결정적 값이고, Export의 `node_ids`가 그 값과 같은지(보고서 5·6절 증거)
3. **개행 규칙:**
   - `*.tscn text eol=lf`가 대상 4개 외에 다른 파일의 속성을 바꾸지 않는지(보고서 4절: 821개 추적 파일 중 4개만 바뀜)
   - 체크아웃 결과가 의도한 LF 바이트인지
   - `.gitattributes`가 B1 구현 Manifest 213개 항목 중 하나라는 점의 처리가 적절한지
4. **PCK 재현성 증거:**
   - 같은 폴더 2회, 다른 폴더 2회, Import Cache 유지 1회에서 PCK 전체 바이트가 같은지
   - `node_ids` 제외나 사후 패치 같은 우회가 없는지
5. **회귀 증거:** 보고서 7절의 다음 항목이 FULL PASS 판정에 충분한지 봐 주십시오.
   - verify `GATE`
   - 3,041개 시험, Golden 4, Dry Pilot 9
   - G2~G4, E2~E4(E3 7/7·leak 0·스크린샷 전부)
   - 원본과의 화면 비교
6. **영향 범위:**
   - 게임 로직·RNG·Hash·Replay·Golden 기대값이 바뀌지 않았는지
   - 새 소스 Commit이 생길 때 다시 계산할 값(보고서 9절)의 목록이 빠짐없는지
7. **계약과의 관계:** 이 변경이 B1-T00 승인 계약 범위 밖의 소스 변경이라는 정리(보고서 8절)가 맞는지

## 3. 범위 밖

- 운영 Build Provenance 등록, G3 PASS, Build Freeze, G1~G9
- §18.2 Environment Profile, SourceLink 공식 URL 확정, B-4 조항
- Remote 원격 조회·Push
