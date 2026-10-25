# B1 Post-Tag 증거 색인 (Tag 이후 추가 증거)

- 작성: Claude Code, 2026-10-26. 근거: OWNER 지시 [O19](../contracts/b1/originals/owner/O19_20261025T181935Z_post-tag-evidence-and-g3-order.txt) 1·2항
- 기계 검증용: [B1_POST_TAG_EVIDENCE.sha256](B1_POST_TAG_EVIDENCE.sha256)(경로 정렬, `sha256sum -c` 형식). 이 색인과 `.sha256` 자신은 목록에 넣지 않습니다.
- **이 색인은 Tag 이후 추가된 증거만 다룹니다.**
  - B1 구현 Manifest 213개 항목([B1_IMPLEMENTATION_MANIFEST.md](B1_IMPLEMENTATION_MANIFEST.md)·`.sha256`)은 바꾸지 않았습니다.
  - [원문 색인](../contracts/b1/originals/INDEX.md)(O01~O16, Codex 12)도 바꾸지 않았습니다. 두 문서 모두 Manifest 항목입니다.

## 1. Tag에서 고정한 내용 (변경 없음)

| 항목 | 값 |
|---|---|
| Tag | `b1-code-accepted-2026-10-26`(annotated, 객체 `5e19ddf505a2e05d2fc36228707f9b2d06fe8217`) |
| 대상 Commit | `7bdde7dd091f9a66957bc740c77d470c7732b3ad` |
| 고정 범위 | 그 Commit의 트리 전체. B1 구현 Manifest 213개 항목, D3 5개, Codex 원문 12개, Red 원시 로그 3개 포함 |
| 검증(Tag 대상 Commit의 깨끗한 체크아웃 `E:\Madegame\Star_player_b1_verify3`) | `GATE: PASS`, 3,041 PASS / 0 FAIL / 0 SKIP, U6 70/70(9분 13초), Golden 4/4, D1 0개, Manifest 213/213, D3 5/5, Codex 12/12, Red 3/3, Dry Pilot 9 동일 |

- **위 검증은 Tag 대상 Commit의 결과입니다.**
  - Tag 이후 문서 Commit에서 전체 verify를 다시 실행하지 않았습니다.
  - 문서 Commit 뒤에는 D1 링크, D3 SHA, 이 목록의 SHA, Git 변경 범위만 확인합니다.

## 2. Tag 이후 추가한 증거

### 2.1 OWNER 지시 원문 (Claude Code 세션 기록, 사용자 메시지)

- **추출 방법:** O01~O16과 같습니다. 로컬 세션 기록(JSONL)의 해당 행 `message.content` 문자열을 **그대로** UTF-8로 저장했습니다(BOM·추가 문자 없음). 원문을 다시 쓰거나 추정해 복원한 내용은 없습니다.
  - 원본 경로: `C:\Users\user\.claude\projects\E--Madegame-Star-player\ebd95d80-533b-4324-b731-d26d936bb2f3.jsonl`
  - 저장된 문자열은 채팅 입력 형식(`<pasted_content …>` 감싸기 포함) 그대로입니다.
- **원본 행 SHA-256:** 그 행의 원시 바이트(줄바꿈 제외) SHA입니다.
- **독립 검증:** **NOT_INDEPENDENTLY_VERIFIED**. 로컬 기록이며 채팅 서비스가 서명한 기록이 아닙니다.
- **표시:** 승인 계약 원본이나 Codex 판정 원본이 **아닙니다**. 원문 색인(INDEX.md)에는 추가하지 않았습니다(Manifest 항목 보호).
- **비밀정보:** 검사 결과 0건(API Key·Token·비밀번호·Bearer·개인 키·이메일 패턴)
- **바이트 보존:** 기존 `docs/contracts/b1/** -text` 규칙이 적용됩니다. 확장자는 원본대로 `.txt`입니다.

| ID | 의미 | 기록 시각(UTC / KST) | 원본(세션 파일:행) | 바이트 | SHA-256 | 원본 행 SHA-256 |
|---|---|---|---|---:|---|---|
| [O17](../contracts/b1/originals/owner/O17_20261025T161612Z_d1-u6-follow-up-decision.txt) | B1 Git 기준본 검증 실패 후속 OWNER 결정(D1·U6 수정) | 2026-10-25T16:16:12.236Z / 2026-10-26 01:16:12 KST | `ebd95d80-533b-4324-b731-d26d936bb2f3.jsonl`:6 (`message.content`, uuid `a699f21b-18a8-4318-a008-dc49ca9da620`) | 4,423 | `5c65b4f93ff441d8a4c30922aa2b2be810a0358bd6643162786ddc3b09f8d984` | `cae4c24990f380f0602cc04f3b8172e4310ae26c1b7b408c73bc3f6213cf46ff` |
| [O18](../contracts/b1/originals/owner/O18_20261025T175109Z_d1-residual-fix-decision.txt) | B1 Git Baseline D1 잔여 오류 수정 OWNER 결정(Red 로그·Tag 조건) | 2026-10-25T17:51:09.546Z / 2026-10-26 02:51:09 KST | `ebd95d80-533b-4324-b731-d26d936bb2f3.jsonl`:547 (`message.content`, uuid `059ed607-424a-481e-9052-79742e7cf2cc`) | 4,675 | `2c3c4661c2a8c3aff5acf2f49f9943e737ea3daed58f61465f94b0eba94f863c` | `fcb0dcf57f665d2ef1904aa0ce45f6694ed05f359eb09d035827808b4b260e83` |
| [O19](../contracts/b1/originals/owner/O19_20261025T181935Z_post-tag-evidence-and-g3-order.txt) | B1 Post-Tag 증거 보존 및 G3 조사 준비(이 색인의 근거) | 2026-10-25T18:19:35.166Z / 2026-10-26 03:19:35 KST | `ebd95d80-533b-4324-b731-d26d936bb2f3.jsonl`:765 (`message.content`, uuid `2eebd740-b514-4ac2-90da-63400ffdd87c`) | 3,704 | `1a60c661a39c257324fb1c61e3aa5fd4aad7a47724ca1f891f45011208f4df76` | `c40a9be597c2976f38060a3a740722df7762157019d7bc6be5463a62af984752` |

### 2.2 Claude 작성 기록과 verify 출력 사본

- **표시:** 이 파일들은 승인 계약 원본, OWNER 원문, Codex 판정 원본이 **아닙니다**.
- **바이트 보존:** `docs/reports/B1_* -text` 규칙이 적용됩니다.
- **민감정보 검사:**
  - 비밀정보 0건입니다. 보고서의 "Token·Bearer"는 검사 대상 패턴을 설명한 문구입니다.
  - 들어 있는 경로는 이 PC의 빌드·도구 경로뿐입니다(계정명은 일반값 `user`).
- "시각"은 이 PC의 파일 최종 수정 시각입니다.

| 파일 | 종류 | 출처 | 시각 | 바이트 | SHA-256 | 독립 검증 |
|---|---|---|---|---:|---|---|
| [B1_GIT_BASELINE_REPORT.md](B1_GIT_BASELINE_REPORT.md) | Claude 작성 보고서 | 이 저장소에서 Claude가 작성(1~3차 결과, 11절 Post-Tag) | 2026-10-26 03:21:25 KST | 31,403 | `80e8f648daade5219baff8d2c5adac470710ad2985dce5abf4a37b7e47d2f922` | 해당 없음(Claude 기록). 인용한 실측값은 각 원시 로그가 근거 |
| [B1_IMPLEMENTATION_LOG_POST_TAG_ADDENDUM.md](B1_IMPLEMENTATION_LOG_POST_TAG_ADDENDUM.md) | Claude 작성 기록 | `B1_IMPLEMENTATION_LOG.md` 163행 뒤 미커밋 8행을 바이트 그대로 옮김 | 2026-10-26 03:21:13 KST | 1,831 | `489d4deb50bf2c865866fe05bf05144a61fdec7cd7870d8893a662b3e1c13eb1` | 해당 없음(Claude 기록) |
| [B1_CLEAN_CHECKOUT_VERIFY_LOG_ff368a9.md](B1_CLEAN_CHECKOUT_VERIFY_LOG_ff368a9.md) | verify 출력 사본(기계 생성) | `E:\Madegame\Star_player_b1_verify2\docs\reports\B1_CLEAN_CHECKOUT_VERIFY_LOG.md`(`tools/verify.ps1` 출력, Run at 2026-10-26 01:44:28 KST, Git `ff368a9 (clean)`, GATE FAIL) | 2026-10-26 01:48:36 KST | 6,723 | `ff1d183bd0ca998f760f2e80e60f3010f28c13979bce951295308ae4762c6f66` | 원본 파일과 바이트 같음을 Claude가 확인. 독립 검증 미실시 |
| [B1_CLEAN_CHECKOUT_VERIFY_LOG_7bdde7d.md](B1_CLEAN_CHECKOUT_VERIFY_LOG_7bdde7d.md) | verify 출력 사본(기계 생성) | `E:\Madegame\Star_player_b1_verify3\docs\reports\B1_CLEAN_CHECKOUT_VERIFY_LOG.md`(`tools/verify.ps1` 출력, Run at 2026-10-26 03:12:53 KST, Git `7bdde7d (clean)`, GATE PASS) | 2026-10-26 03:15:13 KST | 6,334 | `a30bc1e32eeadc73efcee970d77ea7c87dac07e89399b7bb22e5e5ebdae6ee93` | 원본 파일과 바이트 같음을 Claude가 확인. 독립 검증 미실시 |

## 3. 다시 확인하는 방법

```bash
sha256sum -c docs/reports/B1_POST_TAG_EVIDENCE.sha256
```

```bash
git rev-parse "b1-code-accepted-2026-10-26^{commit}"
```

두 번째 명령의 결과는 `7bdde7dd091f9a66957bc740c77d470c7732b3ad`이어야 합니다.
