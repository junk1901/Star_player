# Codex READ-ONLY 표적 재검수 요청 — G1-a 감사 도구 재결속(`g1-names-check-8ca9705.ps1`)

- 작성: Claude Code, 2026-10-30. 근거: OWNER 지시 "G1-a 감사 도구 재결속 지시"(2026-10-30)
- 이전 판정 유지: G1-POLICY-01 RESOLVED(승인 도구 `d3945b5e`), G2 E-1 ACCEPTABLE, C-3 REVIEW_READY(G5-TEMP-01 RESOLVED_IN_CODE, G5-MODEL-01 RESOLVED)
- **요청 범위:** 새 도구 버전이 승인 도구와 같은 규칙·보호 수준으로 제품 소스 후보 `8ca9705`에 정확히 결속되었는지만 판정합니다. 제품 코드·C-3 재검수는 범위 밖입니다.
- READ-ONLY: 파일·Git 변경, 원격 Fetch·Push를 하지 않습니다. 직접 실행하지 않은 시험은 **제출 증거 대조**라고 적어 주십시오.
- **판정 방식:**
  - 새 도구: **ACCEPTABLE / NEEDS_CHANGE**
  - 전체 Verdict: **REVIEW_READY / FIX_REQUIRED / INSUFFICIENT_EVIDENCE**
  - 새 Finding은 MAJOR·MINOR와 근거를 함께 적습니다.
- 판정은 OWNER 승인이나 공식 G1 PASS가 아닙니다. 이 요청서는 OWNER가 전달합니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 식별

| 항목 | 값 |
|---|---|
| 검증 대상(제품 소스 후보) | Commit `8ca9705539bc941919f5290de149c4678f5147dd`, Tree `fd87de669127228dd15cbe9cbc85272326a65ff7` |
| 새 도구 | `tools/b1-ops/g1-names-check-8ca9705.ps1`, SHA-256 `4d086a5cf2613f22d532ade52c8df42df59700c88b8cd5a3306098c7ee43f96b`, Commit `98724888f4ca7f6a59bfb2f29297e037c1aae7d6` |
| 승인 도구(변경 없음) | `tools/b1-ops/g1-names-check.ps1`, SHA-256 `d3945b5ed51f0c49e39b2689bd73a8b14240ff9d13564745aca12f22eeb56ba0` |
| 보고서 | [B1_G1_TOOL_8CA9705_REPORT.md](../reports/B1_G1_TOOL_8CA9705_REPORT.md) |
| 증거 | `E:\Madegame\g3x\phase3ops\g1v2\`(Clone 2개, 도구 Blob 2개, 생성·재산출 스크립트, `logs\ps51`·`logs\ps7`, `SHA256SUMS_g1v2.txt`) |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player_c3 show 98724888:tools/b1-ops/g1-names-check-8ca9705.ps1 | sha256sum
git -C E:/Madegame/Star_player_c3 show 98724888:tools/b1-ops/g1-names-check.ps1 | sha256sum
git -C E:/Madegame/Star_player_c3 diff --no-index <(git -C E:/Madegame/Star_player_c3 show 98724888:tools/b1-ops/g1-names-check.ps1) <(git -C E:/Madegame/Star_player_c3 show 98724888:tools/b1-ops/g1-names-check-8ca9705.ps1)
git -C E:/Madegame/Star_player_c3 diff -w 7043bb9 8ca9705 -- tools/StarPlayer.SimHarness tools/StarPlayer.EvidenceVerifier
```

## 2. 판정을 요청하는 항목

1. **차이의 범위:** 승인 도구와의 차이가 지배 줄 2개·파일 SHA 3개·주석뿐이고, 규칙·정규식·이름 9종·정의 13줄·검사 논리·`-SelfTest` 사례가 바이트 그대로인지(보고서 2절)
2. **기준값의 정확성:** 새 값이 `8ca9705`의 실제 바이트와 맞는지 — 바뀐 지배 줄 2개(두 Replay의 `resultFiles(…ResultFile) is not { } raw`), 파일 3개 SHA = `git show 8ca9705:<파일>`의 SHA-256. 나머지 48줄·5개 파일 값이 그대로 맞는지(보고서 3절, 독립 Python 재산출)
3. **보호 수준 유지:** 기존 6·신규 7 반례 FAIL, 오탐 대조 2 PASS, LIMIT 1(탐지 못함으로 명시) — PowerShell 5.1·7.4.22(보고서 5절)
4. **대상 확인 절차:** 도구 출력 HEAD에 의존하지 않고 Commit·Tree·Clean·추적 파일 집합 = 검사 파일 집합을 외부에서 확인했는지(보고서 4절)
5. **결속의 대칭성:** 승인 도구는 `8ca9705`에서, 새 도구는 `c77ed1a`에서 각각 불일치 7건으로 FAIL — 두 버전이 각자 기준 Commit에만 결속되는지
6. **식별 분리:** 새 도구(Commit `98724888`)와 제품 소스 후보(`8ca9705`)가 혼동 없이 구분되는지, "도구 PASS ≠ Commit 승인" 문구가 충분한지

## 3. 범위 밖

- 제품 코드·C-3·G4/G5 재검수, 공식 G1 실행(운영 기계), OWNER 결정의 대리
- Push, Tag, 운영 ACL·계정·Pin·Trust Anchor·Registry, 공식 Gate PASS, Build Freeze
