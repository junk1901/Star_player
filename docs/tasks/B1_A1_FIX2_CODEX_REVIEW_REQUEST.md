# Codex READ-ONLY 표적 재검수 요청 — A1-FIX2-01 (Ref 이름 정확 비교)

- 작성: Claude Code, 2026-10-29. 근거: OWNER 지시 "A1-FIX2-01 단일 MINOR 수정 및 최종 표적 검수" 7항
- **요청 범위:**
  - 범위는 2절 항목으로 한정합니다.
  - READ-ONLY입니다. 파일·Git 변경, 원격 Fetch·Push, ACL 변경은 하지 않습니다.
  - Codex가 직접 실행하지 않은 시험은 판정에 **제출 증거 대조**라고 적어 주십시오.
- **판정 방식:**
  - A1-FIX2-01은 **RESOLVED / OPEN**으로 판정합니다.
  - 새 Finding은 MAJOR·MINOR와 근거를 함께 적습니다.
  - 최종 Verdict는 **REVIEW_READY / FIX_REQUIRED / INSUFFICIENT_EVIDENCE** 중 하나입니다.
  - Codex PASS여도 A-1 원격 생성·게시·운영 Gate 승인이 아닙니다.
- 이 요청서는 OWNER가 전달하며, Claude가 Codex에 직접 보내지 않습니다. 신규 운영 정책은 Claude가 확정하지 않았습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준과 대상

| 항목 | 값 |
|---|---|
| Worktree·브랜치 | `E:\Madegame\Star_player_g4`, `phase3/g4-official-entry` |
| 이전 검수 HEAD | `a89f0d5fde45e69e6eda7f02860feb7d1e1b498e` |
| 이전 검증 소스 | `4a3d61b7f4b30db5cc0c5061ad0adcbf90e340df` |
| **소스(개발·검증 기준) Commit** | **`1751fe04aaafbda65462915d2dac89e56536a8ae`** |
| 후속 문서 Commit | 이 요청서·보고서·verify 로그 사본·`CURRENT_STATE.md`·Dashboard. 검증 대상이 아님 |
| 보고서 | [B1_A1_FIX2_REPORT.md](../reports/B1_A1_FIX2_REPORT.md) |
| 이전 판정 원문 | `E:\Madegame\g3x\codex-originals-g4\20261028T153931Z_verdict-g5-a1-fix2-fix-required-a1fix201.txt`(`305dd763…`, NOT_INDEPENDENTLY_VERIFIED, 보고서 4절) |
| Clean Checkout | `E:\Madegame\g3x\g4clean3`(`1751fe0`) — 보고서 3절 |
| 증거(저장소 밖) | `E:\Madegame\g3x\g5fix3\` — 보고서 2·3절의 SHA |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player_g4 diff a89f0d5fde45e69e6eda7f02860feb7d1e1b498e 1751fe04aaafbda65462915d2dac89e56536a8ae
git -C E:/Madegame/Star_player_g4 diff --stat 4a3d61b 1751fe0 -- tools/b1-ops/g5-acl.ps1 tools/b1-ops/g5-pins.ps1 tools/b1-ops/B1OpsNative.cs tools/b1-ops/g5-probe.ps1 tools/b1-ops/b4-candidates.ps1 tools/b1-ops/a1-scan.ps1
git -C E:/Madegame/Star_player_g4 diff --stat a980ea5 1751fe0 -- src game tests tools/StarPlayer.EvidenceVerifier tools/StarPlayer.SimHarness tools/verify.ps1 TASK.md docs/contracts content
git -C E:/Madegame/Star_player_g4 log --oneline a89f0d5fde45e69e6eda7f02860feb7d1e1b498e..HEAD
```

## 2. 판정을 요청하는 항목

1. **A1-FIX2-01**(`a1-refcheck.ps1`) — RESOLVED / OPEN
2. **Ref 이름 Ordinal 비교:**
   - 기대·조회 목록이 모두 `StringComparer.Ordinal` 사전인지
   - 저장·조회·형식 검증(`-cmatch`)·정렬에 대소문자 무시 경로가 남지 않았는지
   - ID 비교가 이름과 분리되어 소문자 16진수 확인 뒤 Ordinal로 이루어지는지
3. **Ref 중복·미승인 Ref 거부:**
   - 조회 결과 중복이 덮어써지지 않고 `DUPLICATE`(FAIL)인지
   - 대소문자 변형이 독립 Ref로 남는지
   - 미승인·누락·불일치가 FAIL인지
   - 조회 실패·형식 오류가 ERROR인지
4. **정상 Git 게시 Ref 처리:**
   - 정상 main, 정확한 승인 Tag, 두 변형의 명시 승인이 PASS인지
   - 게시 절차의 `for-each-ref` 기반 ID 확인과 객체 ID Push 원본(보고서 1절 관련 결함 수정)
5. **기존 A1-R02 수정의 재발 여부:** `--refs`, HEAD·Peeled 줄 제외, Tag 객체 ID, `--no-follow-tags`
6. **기존 G5 해결 Finding의 수정 영향:** G5·B-4·A-1 Scan 도구 변경 0줄(보고서 7절)
7. **신규 MAJOR·MINOR**
8. **신규 시험과 Clean Checkout 증거:**
   - RED 9/19 오판과 GREEN 0/19(보고서 2절), 실행기 결함 기록
   - 자체 시험 109건
   - Clean Checkout verify, D1·D3, PCK, A-1 Scan(보고서 3절)
   - Golden·Dry Pilot·PCK 5회 재현성은 재실행하지 않고 이전 증거를 재사용했습니다.

## 3. 범위 밖

- OWNER 규범·운영 값의 대리 결정
- 운영 ACL·앵커·Pin 설치, A-1 원격 생성·Push, 공식 G1~G9, Build Provenance 등록
- 보고서 6절 운영 조건(`SetKernelObjectSecurity` 지원 범위, 유지보수 격리)은 기록만 하며 이번 판정 대상이 아닙니다.
