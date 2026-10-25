# B1 G3 Scene 변경안 SHA Manifest (변경 후보, 미커밋)

- 작성: Claude Code, 2026-10-26. 근거: [TASK](../tasks/B1_G3_SCENE_MIGRATION_TASK.md), 결과: [보고서](B1_G3_SCENE_MIGRATION_REPORT.md)
- **비교 기준:** B1 Code Commit `7bdde7dd091f9a66957bc740c77d470c7732b3ad`(Tag `b1-code-accepted-2026-10-26`)
  - 변경 후보는 이 Commit 위의 **작업 트리 변경**입니다(Stage·Commit 없음).
  - 승인되어 Commit되면 새 소스 Commit이 됩니다.
- **별도 기록:**
  - B1 구현 Manifest([B1_IMPLEMENTATION_MANIFEST.md](B1_IMPLEMENTATION_MANIFEST.md), 213개)는 수정하지 않았습니다. 이 목록은 그와 별도입니다.
  - `.gitattributes`는 213개 항목 중 하나이므로, 변경 후보를 적용한 트리에서는 그 항목의 `sha256sum -c`가 일치하지 않습니다. 213개 Manifest는 `7bdde7d` 기준본의 기록으로 남습니다.
- 기계 검증용: [B1_G3_SCENE_MIGRATION_MANIFEST.sha256](B1_G3_SCENE_MIGRATION_MANIFEST.sha256)(새 바이트, 경로 정렬, `sha256sum -c` 형식)

## 1. 변경 파일 (5개)

| 경로 | 이전 blob(`7bdde7d`) | 이전 blob 바이트 · SHA-256 | 이전 깨끗한 체크아웃 작업 트리(`core.autocrlf=true`) 바이트 · SHA-256 | 새 blob | 새 바이트 · SHA-256(= 작업 트리 = blob 내용) |
|---|---|---|---|---|---|
| `.gitattributes` | `180ee2616b83` | 2,993 · `f339e2ef904d0866995890c8ac7c95db17dd0f54c6a146cdbd75fed930f9d419` | 2,993 · 같음 | `d00fd79d24d8` | 3,147 · `a001c39c1abe64addc003452613f773b46c0e1bf09cb56b723784b95f5c5ce1b` |
| `game/Bootstrap/Main.tscn` | `a5d3d718a2d0` | 173 · `88ce9018f82a95ee3ce86ec8957ca1689d63ab0af4c81260b2caadbffdd1349f` | 179 · `00770c63e2b958fc6be2c60455c90b9c3092b21ecb68a746644a5f5733db1e70` | `a0a6310b34bb` | 180 · `3501aba8fd59b5450855bca47bc2de66f7b6f1b251df3ac84d04d64f9787edf4` |
| `game/UI/MainMenu/MainMenu.tscn` | `73b4a330ff06` | 3,014 · `9d3d933c2c373e8ffb7ae4dafe0662a1d32bd3a47bad2cda2c7e428eff34f378` | 3,112 · `99170af7b9acf98c98caa451149026f041629aff0d6ae1224402da3386b9c72c` | `592c3595ac7d` | 3,290 · `79dbc1dd026645d12fb05963e90130aa340403b3247f839f51613b727395192f` |
| `game/UI/PlayerDiagnostics/PlayerDiagnosticsScreen.tscn` | `671e11ebaf42` | 2,632 · `a2bcb1781f3c4faf3b050b7f6dcac352aa6da2d5aa99e05429c97a3d6284ff98` | 2,724 · `65d14c608f5fd4c6e30eccd94a6a60881abd088d82da9024ac345347c8c955ab` | `1521d61ff919` | 2,885 · `defd9603508305d24ae864113b786c698927489e0756497870df043262f9f85c` |
| `game/UI/GameDiagnostics/GameDiagnosticsScreen.tscn` | `8d271742ae54` | 4,667 · `53c8ed082ebaf310dcfbc9e069ffa863d2e6133817594c9efb24357b636ccd3e` | 4,823 · `be5b82a78cac827276c975c04df482714f30a380ba4d3d51b601856967e8ebe1` | `21bc44daecbb` | 5,122 · `98886edae773b66fba879ca11a2c7156d7b3983a4523d70d9991418f23634490` |

- **Scene 출처:** Scene 4개의 새 바이트는 진단 재저장본 R1(`E:\Madegame\g3x\pck-out\R1_pack\`, 진단 clone `pb`)과 SHA가 같습니다.
- **필터 결과:** 새 `.gitattributes`(`*.tscn text eol=lf`)에서 필터 적용 blob(`git hash-object`)과 원본 바이트 blob(`--no-filters`)이 같습니다. 체크아웃 변환 결과도 새 바이트와 같습니다(보고서 4절).

## 2. 함께 작성한 문서 (변경 후보 문서, 미커밋)

- `docs/tasks/B1_G3_SCENE_MIGRATION_TASK.md`
- `docs/reports/B1_G3_SCENE_MIGRATION_REPORT.md`
- `docs/reports/B1_G3_SCENE_MIGRATION_MANIFEST.md`(이 문서)·`.sha256`
- `docs/tasks/B1_G3_SCENE_MIGRATION_CODEX_REVIEW_REQUEST.md`

문서는 작성 중 바뀔 수 있어 SHA를 이 목록에 고정하지 않습니다. Commit 수용 결정 때 Stage 목록과 함께 SHA를 확정합니다.

## 3. 다시 확인하는 방법

```bash
sha256sum -c docs/reports/B1_G3_SCENE_MIGRATION_MANIFEST.sha256
```
