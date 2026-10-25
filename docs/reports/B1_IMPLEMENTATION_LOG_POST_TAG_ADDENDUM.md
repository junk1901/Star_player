# B1 구현 기록 — Tag 이후 추가분 (Post-Tag Addendum)

- 작성: Claude Code, 2026-10-26. 근거: OWNER 지시 O19(Post-Tag 증거 보존) 1·2항
- **이 파일은 Claude가 쓴 기록이며, 승인 계약 원본·OWNER 원문·Codex 판정 원본이 아닙니다.**
- [B1_IMPLEMENTATION_LOG.md](B1_IMPLEMENTATION_LOG.md)는 구현 Manifest 213개 항목 중 하나입니다(SHA `2fa62ab1…feea`).
  - Tag `b1-code-accepted-2026-10-26` 이후 그 파일 163행 뒤에 미커밋으로 덧붙였던 8행을 **바이트 그대로** 이 파일로 옮겼습니다.
  - 원래 파일은 Tag 시점 바이트로 되돌렸습니다. Manifest 213개 항목을 바꾸지 않기 위해서입니다.
  - 8행은 원래 위치의 삽입 행이었으며, 그 밖의 행은 바뀌지 않았습니다.
- 아래 "116개"는 오기입니다(정정: 실제 128개 = Codex 원문 122 + Red 로그 누락 6). 문구는 덧붙였던 그대로입니다.

## 옮긴 8행 (원래 위치: `B1_IMPLEMENTATION_LOG.md` 163행 뒤)

- (Commit 이후, 미커밋 기록) Commit `b2635ab`(G2)·`c15360c`(기준본). 깨끗한 clone 재검증은 다음과 같았습니다.
  - 목록 SHA 210/210·D3 5·Dry Pilot 동일
  - verify `GATE: FAIL`: D1 — Codex 원문 `.md`의 절대 경로 링크 116개(**오기**, 정정: 실제 128개 = Codex 원문 122 + Red 로그 누락 6), U6 — 600초 제한 초과(제한 없이 70/70 PASS)
  - Tag 보류: [B1_GIT_BASELINE_REPORT.md](B1_GIT_BASELINE_REPORT.md)
- (2차, 미커밋 기록) OWNER 후속 결정에 따라 Commit `ff368a9`(D1·U6 수정)을 만들었습니다. 깨끗한 clone 결과는 다음과 같습니다.
  - U6 70/70 PASS, 11분 39초
  - D1 FAIL: 커밋되지 않은 Red 원시 로그 3개(`.gitignore` `*.log`)
  - Tag 보류: [B1_GIT_BASELINE_REPORT.md](B1_GIT_BASELINE_REPORT.md) 6~9절
