# Codex READ-ONLY 표적 검수 요청 — G2 E-1·G1 G1-a 조건부 수용 기준과 G5-TEMP-01

- 작성: Claude Code, 2026-10-29. 근거: OWNER 지시 "G1/G2 조건부 정책 선택 및 G2/G3 공식 실행 준비" 9항
- **요청 범위:**
  - 2절 항목으로 한정한 READ-ONLY 정책 검수입니다. 파일·Git 변경, 원격 Fetch·Push, ACL 변경, 빌드·시험 실행을 하지 않습니다.
  - Codex가 직접 실행하지 않은 시험은 판정에 **제출 증거 대조**라고 적어 주십시오.
  - 이 검수는 정책 제안의 독립 검토입니다. OWNER 승인이나 공식 Gate PASS가 아닙니다.
- **판정 방식:**
  - 정책안 A·B는 각각 **ACCEPTABLE / NEEDS_CHANGE / REJECT**로 판정합니다. 근거를 함께 적어 주십시오.
  - G5-TEMP-01은 **Finding 유효성과 심각도(MAJOR / MINOR / 없음)**를 판정합니다.
  - 새 Finding은 MAJOR·MINOR와 근거를 함께 적습니다.
  - 최종 Verdict는 **REVIEW_READY / FIX_REQUIRED / INSUFFICIENT_EVIDENCE** 중 하나입니다.
- 이 요청서는 OWNER가 전달하며, Claude가 Codex에 직접 보내지 않습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준과 자료

| 항목 | 값 |
|---|---|
| 공식 원격 `main`(게시) | `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f`(공식 빌드 소스 후보) |
| 로컬 기록 Commit(미게시) | `e8f8f72`·`200f448`·`92297cb`·이 요청서 Commit. 제품·계약·`.gitattributes` 변경 0 |
| 정책안 | [G1·G2 조건부 결정안](B1_G1_G2_CONDITIONAL_DECISION_PROPOSAL.md) A(G2-E1)·B(G1-a) |
| 규범 표 | [B-4 빌드 절차 초안](B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md) 6.1절 |
| 정적 검사 도구(신규) | `tools/b1-ops/g1-names-check.ps1` |
| 보고서 | [정책 준비 보고서](../reports/B1_G1G2_POLICY_PREP_REPORT.md), [선행 문제 보고서](../reports/B1_G2G3_PREREQ_REPORT.md), [G2/G3 진단 보고서](../reports/B1_G2G3_DIAGNOSTIC_REPORT.md) |
| 기존 G1·G2 원문 | [운영 Gate 계획](B1_OPERATIONAL_GATE_PLAN.md) 2절 표 |
| 승인 계약 | `docs/contracts/b1/B1-T00_FINAL_CONTRACT.approved.md`(§14, §18.3, §21, §22) |
| 증거(저장소 밖) | `E:\Madegame\g3x\phase3ops\{g23,eol,prodsep,policy}\`, `E:\Madegame\g3x\pck-out\{G23*,EOL*}` — 보고서의 해시 |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player_g4 log --oneline c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f..HEAD
git -C E:/Madegame/Star_player_g4 diff --stat c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f HEAD -- src game tests content docs/contracts TASK.md .gitattributes tools/StarPlayer.SimHarness tools/StarPlayer.EvidenceVerifier
git -C E:/Madegame/Star_player_g4 ls-files --eol content/tuning tools/StarPlayer.EvidenceVerifier
```

## 2. 판정을 요청하는 항목

### A. G2 E-1 조건부 수용 기준(결정안 A절)

1. 기존 G2 원문(A.1)과 변경안(A.3)의 차이가 숨김 없이 드러나는지, 빠지는 보장(A.4)의 목록이 완전한지
2. 승인 B1-T00 계약과 충돌하는지(특히 §21 보존 규칙의 `-text`·CRLF 문장, §18.3 Build Provenance, §22 불변 목록)
3. 바이트 재현성 보장 범위
   - 같은 설정·Commit·경로 무관 재현(3개 Clone 실측)
   - 설정이 다르면 다른 바이트(LF Clone 실측, B-4 후보 24행)
   - A.3-4 "승인 입력 바이트 목록" 대조로 설정 오류를 막을 수 있는지
4. SourceLink·PDB 체크섬(A.6)
   - 301개 파일의 PDB 체크섬이 Raw의 LF→CRLF 변환과 모두 일치(설명 불가 0)한다는 대조 방법이 타당한지
   - B-4 규범 결정 자료 1.3절(Raw ↔ Blob, PDB ↔ 체크아웃) 기준으로 공식 출처 검증에 충분한지
   - 승인 계약이나 B-4의 SourceLink 신뢰 요구와 실제로 충돌하는지
5. E-3(A.7)의 범위·영향 기술이 정확한지
6. 공식 Gate PASS 조건(A.3 1~7)이 판정 가능할 만큼 명확한지

### B. G1 G1-a 조건부 수용 기준(결정안 B절)

1. 기존 G1 원문(B.1)의 "이름 정의 한 곳"을 G1-a(B.3)로 바꾸는 것이 타당한지, 남는 한계(B.4)가 정확한지
2. `manifest.json` 다중 참조 통제
   - 생산 코드 5개 파일 9곳의 고정 목록
   - `g1-names-check.ps1`의 정규식·고정 목록·`-SelfTest` 반례 6건이 드리프트를 막기에 충분한지
   - 리터럴 조립(예 `"manifest" + ".json"`) 같은 우회 가능성
3. 계약 §22(DRAFT 색인 바이트 변화 0)와 기존 Hash·Replay 불변성: 이름을 바꾸지 않는 결정이 이를 지키는지
4. G1-b(B.5) 전환 영향 기술이 정확한지(SIMHARNESS_BINARY·VERIFIER_BINARY 변경)
5. 새 도구 `g1-names-check.ps1`
   - 읽기 전용인지
   - 판정 오류(정상을 FAIL, 드리프트를 PASS) 가능성

### C. G5-TEMP-01(정책 준비 보고서 3절)

1. 검증기 Replay(`Verifier.cs` 245~249행)가 판정 입력(실행 색인·Unit Manifest)을 검증기 계정 `%TEMP%`에 쓰고 기존 Replay가 경로로 다시 읽는 구조에서, TEMP를 다른 주체가 수정할 수 있을 때 판정이 바뀔 수 있다는 분석(`FamilyOfficial.cs` 457~494행, 색인 재읽기 시 스냅샷 SHA 미대조)이 맞는지
2. 개발 PC 실측(사용자 Temp에 다른 주체 Modify ACE) 기준의 심각도
3. 코드 변경 없는 운영 해결(서비스 `TMP`·`TEMP`를 보호 R-WORK 하위로, 운영 실측 M-12)이 충분한지, 아니면 코드 강화가 필요한지
4. M-12 전 공식 인증 중단 조건의 적절성

### D. 공통

1. 신규 보안·재현성 Finding
2. `c77ed1a`를 공식 소스로 동결하기 위해 필요한 OWNER 승인 목록(보고서 4·5절)의 완전성

## 3. 범위 밖

- 이미 수용된 G4/G5 코드의 전면 재검수
- OWNER 규범·운영 값의 대리 결정
- 운영 ACL·앵커·Pin 설치, 원격 Push, 공식 G1~G9, Build Provenance 등록, Build Freeze
