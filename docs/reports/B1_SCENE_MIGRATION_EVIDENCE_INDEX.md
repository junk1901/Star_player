# B1 G3 Scene Migration — 수용 근거 증거 색인 (Codex 원문 C11·C12, Commit 이후 검증)

- 작성: Claude Code, 2026-10-26. 근거: OWNER 결정 "Scene Migration 수용 및 Post-Commit 증거 보존"(2026-10-26, 채팅)
- **OWNER 결정:**
  - Scene Migration 소스 변경을 **`00091736d5386aec3f5c67cf159291976e7133a2`** 기준으로 수용합니다.
  - 공식 G3 PASS, 운영 Build Provenance 등록, Build Freeze, Official Execution, Phase 3 완료는 승인하지 않았습니다.
- 기계 검증용: [B1_SCENE_MIGRATION_EVIDENCE.sha256](B1_SCENE_MIGRATION_EVIDENCE.sha256)(경로 정렬, `sha256sum -c` 형식). 이 색인과 `.sha256` 자신은 목록에 넣지 않습니다.
- **수정하지 않은 것:** B1 구현 Manifest 213개, [Scene Migration Manifest](B1_G3_SCENE_MIGRATION_MANIFEST.md) 5개, [원문 색인](../contracts/b1/originals/INDEX.md)(O01~O16·C01~C10, 구현 Manifest 항목), 기존 Codex·OWNER 원문 바이트
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 식별자 구분

| 구분 | Commit |
|---|---|
| **Scene Migration 검증 소스 Commit** | `00091736d5386aec3f5c67cf159291976e7133a2`(Commit 이후 깨끗한 체크아웃 검증 대상) |
| 역사적 B1 코드 수용 기준본 | `7bdde7dd091f9a66957bc740c77d470c7732b3ad`(Tag `b1-code-accepted-2026-10-26`, 변경 없음) |
| Codex 검수 대상 | Commit 전 변경안: `7bdde7d` clone에 후보 5개 파일을 적용한 작업 트리(당시 저장소 HEAD `cd5f413`) |
| 이 색인을 담은 문서 Commit | Scene Migration 검증 소스 Commit이 **아닙니다**. 이후 HEAD가 바뀌어도 위 `00091736…`이 검증 소스입니다 |

## 2. Codex 원문 (Codex 세션 기록)

- **원본 세션 로그:** `C:\Users\user\.codex\sessions\2026\10\04\rollout-2026-10-04T21-41-07-01a106ee-aab7-7a10-8f22-d304b84ba8ee.jsonl`(C01~C10과 같은 파일)
- **추출 필드:** 해당 행 JSON의 `payload.content[0].text`를 그대로 UTF-8로 저장했습니다(BOM·추가 문자 없음).
  - 2026-10-26에 로그에서 다시 읽어 저장 바이트와 같음을 확인했습니다.
  - Claude가 다시 쓰거나 요약한 원문은 없습니다.
- **위치:** "행 시작·끝 바이트"는 로그 파일 안에서 그 행의 바이트 오프셋입니다(끝은 미포함, 줄바꿈 제외). "원본 행 SHA-256"은 그 행 원시 바이트의 SHA입니다.
- **독립 검증:** **NOT_INDEPENDENTLY_VERIFIED**. 로컬 기록이며 Codex 서비스 측 기록과 대조하지 않았습니다.
- **바이트 보존:** `docs/contracts/b1/** -text` 규칙이 적용됩니다. 확장자는 `.txt`입니다.

| ID | 파일 | 작성 주체(로그 role) | 의미 | 원본 행 | 행 시작·끝 바이트 | 기록 시각(UTC / KST) | 바이트 | SHA-256 | 원본 행 SHA-256 |
|---|---|---|---|---:|---|---|---:|---|---|
| C11 | [C11_20261025T213601Z_request-g3-scene-migration-review.txt](../contracts/b1/originals/codex/C11_20261025T213601Z_request-g3-scene-migration-review.txt) | `user`: Codex에 전달된 요청문. 실제 작성자는 로그로 확인되지 않음 | "G3 Scene Migration 최종 READ-ONLY 독립 검수" 요청 | 12998 | 101,822,046 – 101,831,116 | 2026-10-25T21:36:01.207Z / 2026-10-26 06:36:01 KST | 7,246 | `3f4133e908c49353ab54288302a666e842526a9ec33826ea88249185b6d5ae0f` | `cd2aca90eaf7323b3267a81141968b48c622adb3e9716d0514eda9b36709841b` |
| C12 | [C12_20261025T214355Z_verdict-g3-scene-migration-pass.txt](../contracts/b1/originals/codex/C12_20261025T214355Z_verdict-g3-scene-migration-pass.txt) | `assistant`: Codex 판정 | "최종 Verdict: PASS", MAJOR 0·MINOR 0·비차단 NOTE 1(warm Export 조건 표현) | 13174 | 103,264,442 – 103,274,040 | 2026-10-25T21:43:55.185Z / 2026-10-26 06:43:55 KST | 8,702 | `02497573d5185fbc1f84d6e74e989dae506162471f170e7e3d20541ae78c9df1` | `894b1e98679d7e4a6212b76698ab715d8629aa8de69f27595b2692ee848c27c1` |

- **비밀정보 검사:** 0건(API Key·Token·비밀번호·Bearer·개인 키·이메일 패턴)
- **보존 경위:**
  - 2026-10-26 Commit 전에 추출해 `E:\Madegame\g3x\codex-originals-prep\`에 두었습니다(당시 Commit 범위 밖).
  - 이번 OWNER 허가로 같은 바이트를 위 경로에 넣었습니다.
  - Commit 이후 검증 보고서 4절의 "보존 준비" 위치는 그때의 위치입니다. 그 보고서는 기록된 그대로 두었습니다.
- **확인 범위:** C12는 **Commit 전 변경안**을 검수한 것입니다. C12 원문 7절이 "아직 존재하지 않는 새 소스 Commit의 바이너리·Provenance까지 검증한 것은 아니다"라고 적고 있습니다. Codex가 직접 확인한 범위는 C12 원문이 정합니다.

## 3. Commit 이후 검증 증거 (Claude 실행)

| 파일 | 종류 | 검사 대상 Commit | 실행 시각 | 원본 경로 | 바이트 | SHA-256 | 시험 주체 |
|---|---|---|---|---|---:|---|---|
| [B1_G3_SCENE_MIGRATION_POSTCOMMIT_REPORT.md](B1_G3_SCENE_MIGRATION_POSTCOMMIT_REPORT.md) | Claude 작성 보고서(원문 아님) | `00091736d5386aec3f5c67cf159291976e7133a2` | 검증 2026-10-26 06:50~07:14 KST, 작성 07:16 KST | 이 저장소에서 작성 | 10,348 | `a85975945bd4d10aba4119175a49dc8df4115258923c398f5430729efef73c16` | Claude Code |
| [B1_SCENE_MIGRATION_POSTCOMMIT_VERIFY_LOG.md](B1_SCENE_MIGRATION_POSTCOMMIT_VERIFY_LOG.md) | `tools/verify.ps1` 출력 사본(기계 생성) | `00091736d5386aec3f5c67cf159291976e7133a2`(로그 `Git: 0009173 (clean)`) | Run at 2026-10-26 07:07:40 +09:00, `GATE: PASS` | `E:\Madegame\g3x\nv\docs\reports\B1_SCENE_MIGRATION_POSTCOMMIT_VERIFY_LOG.md`(그 Commit의 깨끗한 clone) | 6,318 | `eb603b9bd280ffdc171596b4335e47638bc295239cd72b54e097f71d9424c588` | Claude Code(원본과 바이트 같음 확인, 독립 검증 미실시) |

- **원시 출력:** 이 검증의 원시 출력(verify 결과·로그·스크린샷, PCK 5회, CI 빌드)은 저장소 밖 `E:\Madegame\g3x\nv\build\verify\`, `E:\Madegame\g3x\pck-out\N_*\`, `E:\Madegame\g3x\full-out\N_S1\`에 있습니다.
- **증거 구분:** Codex 검수 PASS(C12, Commit 전 변경안)와 Commit 이후 검증(Claude 실행)은 **서로 다른 증거**입니다.

## 4. 기존 기록과의 연결

| 기록 | 관계 |
|---|---|
| [B1_G3_SCENE_MIGRATION_TASK.md](../tasks/B1_G3_SCENE_MIGRATION_TASK.md)·[REPORT](B1_G3_SCENE_MIGRATION_REPORT.md)·[MANIFEST](B1_G3_SCENE_MIGRATION_MANIFEST.md)·[CODEX_REVIEW_REQUEST](../tasks/B1_G3_SCENE_MIGRATION_CODEX_REVIEW_REQUEST.md)·[후보 verify 로그](B1_SCENE_MIGRATION_VERIFY_LOG.md) | Commit `00091736…`에 포함된 변경안 문서. C11 요청과 C12 판정의 검수 대상 자료 |
| C11 ↔ 검수 요청서 | C11은 실제로 Codex에 들어간 요청 원문입니다. 저장소의 `B1_G3_SCENE_MIGRATION_CODEX_REVIEW_REQUEST.md`(Claude 작성)와 같은 문서가 아닙니다 |
| C12 NOTE ↔ 보고서 정정 | `B1_G3_SCENE_MIGRATION_REPORT.md` 5절의 warm 조건 표현이 Commit 전에 정정되어 Commit `00091736…`에 들어 있습니다 |
| [B1_G3_GODOT_PCK_REPRO_REPORT.md](B1_G3_GODOT_PCK_REPRO_REPORT.md) | 원인 진단(재저장 진단본 R1). Scene 바이트의 출처 |
| [원문 색인](../contracts/b1/originals/INDEX.md)·[Post-Tag 증거 색인](B1_POST_TAG_EVIDENCE_INDEX.md) | C01~C10·O01~O16과 O17~O19는 그 색인에 있습니다. C11·C12는 기존 색인을 수정하지 않기 위해 이 색인에만 등록합니다 |

## 5. 다시 확인하는 방법

```bash
sha256sum -c docs/reports/B1_SCENE_MIGRATION_EVIDENCE.sha256
```
