# Codex READ-ONLY 표적 재검수 요청 — G1-POLICY-01 수정·G5-TEMP-01 운영 경로·G2 E-1 영향

- 작성: Claude Code, 2026-10-30. 근거: OWNER 지시 "Codex FIX_REQUIRED 후속 조치"(2026-10-30) 5항
- 이전 판정(대상 `7bae96e`): FIX_REQUIRED · G2 E-1 ACCEPTABLE · G1 G1-a NEEDS_CHANGE · G1-POLICY-01 MINOR · G5-TEMP-01 OPEN_MAJOR
- **요청 범위(이것만):** A. G1-POLICY-01 수정 · B. G5-TEMP-01 운영 해결 조건의 충분성 · C. G2 E-1 ACCEPTABLE에 영향을 주는 변경이 없는지
  - 기존 G4/G5 수용 코드 전체 재검수는 범위 밖입니다.
  - READ-ONLY: 파일·Git 변경, 원격 Fetch·Push, ACL 변경, 서비스·작업 등록을 하지 않습니다. 직접 실행하지 않은 시험은 **제출 증거 대조**라고 적어 주십시오.
- **판정 방식:**
  - G1-POLICY-01: **RESOLVED / OPEN**
  - G5-TEMP-01: **OPERATIONAL_PATH_ACCEPTABLE / NEEDS_CHANGE / CODE_FIX_REQUIRED**
  - 전체 Verdict: **REVIEW_READY / FIX_REQUIRED / INSUFFICIENT_EVIDENCE**
  - 새 Finding은 MAJOR·MINOR와 근거를 함께 적습니다.
- **중요:** `OPERATIONAL_PATH_ACCEPTABLE`은 운영 머신 보안 검증 PASS가 아닙니다. 운영 기계 M-12A~J 통과 전까지 G5-TEMP-01의 운영 상태는 **OPEN_MAJOR**로 유지됩니다.
- 이 요청서는 OWNER가 전달하며, Claude가 Codex에 직접 보내지 않습니다. 검수 결과는 OWNER 승인이 아닙니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준과 자료

| 항목 | 값 |
|---|---|
| 공식 원격 `main`(게시, 수정 없음) | `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f` |
| 도구 수정 Commit(로컬, 미게시) | `c4a04bbf84f041a832ed417b2455cdd4596b90f5` |
| 문서 Commit(로컬, 미게시) | 이 요청서를 담은 Commit(`c4a04bb`의 자식) |
| 수정 도구 | `tools/b1-ops/g1-names-check.ps1`, SHA-256 `d3945b5ed51f0c49e39b2689bd73a8b14240ff9d13564745aca12f22eeb56ba0`(이전 `28f07574…`) |
| 보고서 | [후속 보고서](../reports/B1_G1G5_FIX_REPORT.md) |
| G5-TEMP-01 모델 | [보호 모델](B1_G5_TEMP01_PROTECTION_MODEL.md) |
| 규범 표·정책안 | [B-4 초안](B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md) 6.1절, [G1·G2 결정안](B1_G1_G2_CONDITIONAL_DECISION_PROPOSAL.md) B.3·B.4·B.7 |
| 증거(저장소 밖) | `E:\Madegame\g3x\phase3ops\g1fix\`(Clone 2개, 도구 Blob 2개, 드라이버, `logs\ps51\`, `SHA256SUMS_g1fix.txt`) |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player_g4 log --oneline 7bae96e..HEAD
git -C E:/Madegame/Star_player_g4 diff 7bae96e c4a04bb -- tools/b1-ops/g1-names-check.ps1
git -C E:/Madegame/Star_player_g4 diff --stat c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f HEAD -- src game tests content docs/contracts TASK.md .gitattributes tools/StarPlayer.SimHarness tools/StarPlayer.EvidenceVerifier global.json Directory.Build.props
git -C E:/Madegame/Star_player_g4 show c4a04bb:tools/b1-ops/g1-names-check.ps1 | sha256sum
```

## 2. 판정을 요청하는 항목

### A. G1-POLICY-01 (보고서 2절)

1. Codex 반례 두 건(`RunPath` `run/wrong/{runId}/`, `UnitPreflightName` `.txt`)이 이제 FAIL인지, 수정 전 도구에서는 PASS였는지(RED/GREEN 표 2.3절, 로그 `redgreen_c77-crlf.log`와 사례별 `.old.log`·`.new.log`)
2. 같은 개수·다른 경로 반례(3건)와 새 파일·이름 없는 값 흐름 반례가 충분한지
3. 검사 2(정의 13줄)가 B-4 6.1 표의 각 행(`run/<run_id>/`, Family·Unit Preflight, Unit Manifest, `manifest.json`, 확장자, 실행 색인 이름, 경로 생성 함수)에 대응하는지
4. 검사 3(지배 줄 50줄 다중집합)·검사 4(8개 파일 CRLF→LF SHA = `c77ed1a` Blob)가 "검수되지 않은 생성식 변경이 PASS로 남지 않음"을 충분히 보장하는지
5. 남는 한계(보고서 2.5절: 고정 파일 밖 조각 조립·복제, 고정 파일 밖 값 흐름, 시험 코드 정보용)의 기술이 정확하고, 이를 종단 시험·코드 검수에 맡기는 것이 수용 가능한지
6. 도구가 읽기 전용인지, `-SelfTest`가 대상과 분리되는지, 오탐 가능성(CRLF/LF, 무관한 파일)
7. PowerShell 7.4.22 실행 증거(보고서 2.4절, `logs\ps7\`)가 5.1과 같은 판정인지

### B. G5-TEMP-01 운영 경로 (보호 모델 문서)

1. 공격 경로 A1~A7(1절)이 완전한지. 특히 `starplayer-verifier` 선생성(A2), 기존 핸들(A4), 같은 SID(A5)
2. 보호 모델(2절): 보호 R-WORK의 `verifier-tmp`, 세션마다 새 TEMP, SID 전용·단일 세션이 1절 목표를 충족하는지
3. 강제 시작 경계(3절)
   - Registry가 토큰 SID로 검증기 역할을 정하므로 "검증기 SID 프로세스를 만들 수 있는 방법 = 공식 인증 시작 경로"라는 분석
   - S4U 작업 또는 gMSA + 거부 로그온 권한 + 작업 보안 설명자로 비TCB 우회가 막힌다는 판단(문서 근거, 미실측)
   - 암호를 아는 계정은 불충분, 가상 계정은 서비스 호스트 코드 필요라는 판단
   - 실행 중 보호를 시작 절차가 예방하지 못하고(사후 탐지 S8), ACL 구조와 SID 전용에 의존한다는 점
4. M-12A~J(4절)가 실행 계정·명령·기대·증거·중단 조건까지 판정 가능한 수준인지. M-12I 대조 실험(비공식 기계에서 교체가 실제로 성공함을 먼저 보임)의 타당성
5. 시작 절차가 NOT_IMPLEMENTED인 상태에서 `OPERATIONAL_PATH_ACCEPTABLE`이 가능한지, 아니면 코드 대안(5절 C-1~C-4, 권고 C-3)이 필요한지(`CODE_FIX_REQUIRED`)
6. `c77ed1a` 유지 조건 6가지(6절)의 완전성
7. [사실] 주장 확인: C0/C1/C2 환경 digest가 TMP 값·ACL을 Profile과 대조하지 않음(`ExecutionEnvironment.Mismatch`), 재생 디렉터리 기록 후 핸들 해제와 경로 재읽기(`Archive.WriteNew`, `FamilyOfficial.cs` 457~494행)

### C. G2 E-1 영향 (보고서 4절)

1. `c77ed1a..HEAD`의 제품·계약·`.gitattributes`·SDK·빌드 속성 차이 0
2. E-1 조건, 기존 Checkout 설정 독립성 미충족 기록, 301개 SourceLink 차이 기록이 그대로인지

## 3. 범위 밖

- 수용된 G4/G5 코드 전면 재검수, OWNER 규범·운영 값의 대리 결정
- 운영 ACL·앵커·Pin 설치, 시작 절차 구현, 원격 Push, 공식 G1~G9, 공식 인증, Build Provenance 등록, Build Freeze
