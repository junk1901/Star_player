# B1 G1/G2 정책 채택·G5-MODEL-01/02 후속 보고서

- 작성: Claude Code, 2026-10-30. 근거: OWNER 지시 "G1/G2 정책 확정 및 G5-MODEL-01/02 후속 조치"(2026-10-30)
- Codex 판정(입력): 최종 **FIX_REQUIRED** · G2 E-1 **ACCEPTABLE** · G1-POLICY-01 **RESOLVED** · G5-TEMP-01 **OPEN_MAJOR** · G5-MODEL-01 **MAJOR** · G5-MODEL-02 **MINOR**
- **성격:** 정책 기록, 문서 보완, 설계·분석입니다. Push·Tag·계약·`.gitattributes`·제품·검증기·G4/G5 코드 변경, 운영 계정 생성, 예약 작업 등록, ACL 적용, Trust Anchor·Pin 등록, 공식 인증, 공식 Gate PASS 선언, Build Freeze는 **없습니다**. 이번 작업에서 새 실행·진단은 하지 않았습니다(코드 읽기만). 기존 증거와 로그는 그대로입니다.
- 기준: 공식 원격 `main` = `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f`(제품 소스 후보, 변경 없음). 로컬(미게시) Commit `e8f8f72`·`200f448`·`92297cb`·`7bae96e`·`c4a04bb`·`0d5c390`·`1fea694`·이 보고서 Commit
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · G5-TEMP-01 = **OPEN_MAJOR**

## 1. G1/G2 OWNER 정책 기록

- 기록 위치: [G1·G2 결정안 C절](../tasks/B1_G1_G2_CONDITIONAL_DECISION_PROPOSAL.md), [B-4 6.1](../tasks/B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md), [운영 Gate 준비표](../tasks/B1_OPERATIONAL_GATE_READINESS.md) G1·G2, [결정 노트](../../Star/02_Decisions/2026-10-30_B1_G1_G2_Policy_And_G5_Direction.md)
- **G2 = E-1:** 공식 빌드 정책 8개 조건을 그대로 적었습니다. "Checkout 설정과 무관한 같은 바이트"를 E-1이 보장하지 않는다는 점과 301개 SourceLink 체크섬 차이를 알려진 제한으로 명시했습니다. 계약·`.gitattributes` 변경 없음.
- **G1 = G1-a:** 감사 도구 `d3945b5ed51f0c49e39b2689bd73a8b14240ff9d13564745aca12f22eeb56ba0`, 대상 Commit `c77ed1a…`·Tree `a7b9b937e2283fcdb0adb5d83114274940f0e71a`. 도구는 수정하지 않았습니다.
  - "도구 PASS만으로 다른 Commit을 승인하지 않음"을 정확히 적었습니다. 고정 8개 파일이 같은 다른 Commit도 PASS할 수 있으므로, 공식 감사는 Commit·Tree 확인을 먼저 합니다.
  - 탐지 한계와 PowerShell 5.1·7.4.22 결과를 유지합니다. 공식 감사용 PowerShell 경로·버전·SHA·로그는 승인 B-22로 운영 환경에서 고정합니다.
- 두 채택 모두 **운영 Gate PASS가 아니라고** 각 위치에 적었습니다.

## 2. G5-MODEL-01 보완 결과 (C-3)

- [보호 모델](../tasks/B1_G5_TEMP01_PROTECTION_MODEL.md) 5절을 다시 썼습니다. 이전 문장은 9절 개정 이력에 원문과 폐기 이유를 남겼습니다.
- **코드에서 확인한 사실:**
  - Snapshot은 제출 문서와 그 모든 ArtifactRef 파일을 핸들 고정 경로로 한 번 읽고 길이·SHA를 대조합니다(`Verifier.cs` 168~190행, `Archive.Snapshot`).
  - 결속이 끊기는 곳은 이 바이트를 TEMP에 쓰고 Replay가 경로로 다시 읽는 구간뿐입니다.
- **보완 설계:** Replay가 Snapshot의 색인·Manifest **버퍼**를 직접 받습니다(파일 기록·경로 재읽기 없음). OWNER 조건 1~9를 항목별로 대응시켰습니다(5.2절 표).
  - 색인 `manifest_sha256`·`manifest_path` ↔ Snapshot Manifest SHA·경로를 직접 대조
  - 색인·Manifest 버퍼 SHA·길이 ↔ ArtifactRef 재확인
  - 색인 ArtifactRef는 제출 문서의 `artifacts`에서만 받음
  - 검증·파싱은 같은 버퍼, 불일치 시 거부
- 이전 C-4(메모리 Replay)는 보완 C-3에 포함했고, C-1·C-2는 TEMP 경로를 남기므로 부분 완화로만 둡니다.
- **구현하지 않았고**, G5-TEMP-01을 해결했다고 주장하지 않습니다.

## 3. G5-MODEL-02 보완 결과 (gMSA)

- 보호 모델 8절을 새로 썼습니다. gMSA를 이번 운영 선택에서 **제외**하고, S4U와 같은 보장으로 표현하던 문장(3.2 S0, 3.3 결론, 6절 2)을 고쳤습니다(9절 개정 이력).
- 기록한 외부 신뢰 범위:
  - AD·KDS(모든 DC, KDS Root Key와 그 백업)
  - 암호 회수 허용 주체(`msDS-GroupMSAMembership`)와 모든 회수 가능 호스트
  - 관리·위임 권한(Domain/Enterprise Admins, 객체·OU 위임으로 회수 목록을 바꿀 수 있는 주체)
  - 다른 호스트 장악 시 같은 SID 자격 증명이 생겨 Registry 역할 판정(토큰 SID)에 미치는 영향
  - 운영 기계 밖 신뢰 경계
- 이 조건 전체를 OWNER가 승인하지 않으면 gMSA는 사용할 수 없다고 적었습니다. AD 설정·계정 생성은 하지 않았습니다.

## 4. 로컬 S4U 실행기 구성안

- [S4U 실행기 구성안](../tasks/B1_G5_S4U_LAUNCHER_PLAN.md) 1~3절과 보호 모델 3절(S0~S9 로컬 S4U 정리)입니다. **NOT_IMPLEMENTED.**
- 담은 내용:
  - VERIFIER(로컬 S4U 전용)·DEPLOYER 분리, Git Clone·CI 빌드에 VERIFIER를 쓰지 않음
  - 계정 생성·관리 주체(TCB)와 무작위 미기록 암호
  - 배치 로그온 허용과 거부 권한 4종
  - S4U 작업 등록 자격(관리자, VERIFIER 암호 불필요)
  - 작업의 등록·실행·수정·삭제·DACL 권한(TCB만)
  - 실행기·설정 파일과 SHA 항목
  - 검증기 SID 프로세스 생성 경로
  - 같은 SID 다른 프로세스 부재 조건
  - TEMP 경로·ACL·Reparse·DeleteChild·Rename·Replace 차단, 열린 핸들·동시 Writer 통제
  - 실행 중 유지, 등록 전 검증, Fail-Closed
- **S4U 제약:**
  - 네트워크 자격 증명이 없고 EFS·사용자 DPAPI 비밀을 쓸 수 없습니다.
  - [사실] Test Profile은 빌드된 DLL에 `dotnet test`를 실행하므로(`TestProfiles.cs` 273행) 복원이 없고, Registry는 로컬 Pipe입니다.
  - 프로필 로드, `dotnet` 첫 실행, Telemetry는 M-12A에서 실측합니다.
- **새 사실과 충돌 기록(보호 모델 3.4):**
  - 검증기는 영수증을 자기 실행 안에서 등록합니다(`Receipts.Issue` → `registry.Register`, `Receipts.cs` 166행).
  - 시작 절차의 사후 대조 S8은 항상 등록 뒤라 등록을 막지 못하고, 계약에는 등록 영수증 철회 절차가 없습니다.
  - 따라서 운영 경로는 세션 중 보호가 **전부 예방 통제(OS 접근 검사)로 성립**함을 실측해야만 수용 가능합니다. 실측하지 못하면 C-3 전환을 권고합니다.
  - Issue / Reason / Recommended / Alternative / Impact 형식으로 기록했습니다.

## 5. S4U 방식과 C-3 방식의 구현량 비교

- [S4U 구성안](../tasks/B1_G5_S4U_LAUNCHER_PLAN.md) 4절 표(실제 수정 파일, 난이도, 운영 복잡도, 추가 신뢰 구성요소, 시험 범위, 잔여 위험, `c77ed1a` 유지 여부)입니다.
- 요약:
  - **A(S4U):** 제품 수정 0, `c77ed1a` 유지. 대신 운영 구성요소와 신뢰 구성요소(작업 스케줄러, LSA S4U, 보안 정책, PowerShell 호스트, 실행기)가 늘고, 세션 중 예방은 ACL·SID 전용에만 의존합니다.
  - **B(C-3):** SimHarness 2개·검증기 1개 파일과 시험 4개 파일을 고칩니다. TEMP가 신뢰 경로에서 사라지지만, 새 제품 Commit이 필요하므로 G2/G3 재실행, G1 도구 재고정, 소스 후보 재결정이 따릅니다.
- **B로 전환을 제안하는 경우:**
  - 3.3 우회 차단이나 세션 중 예방 조건이 실측에서 성립하지 않음
  - S4U에서 검증기가 끝까지 동작하지 않음
  - "등록 전 검증"이 필수로 판단됨

## 6. `c77ed1a` 유지 가능 조건

보호 모델 6절의 7개 조건(개정 1)입니다.

1. Codex가 운영 경로를 수용
2. B-5 = 로컬 S4U(3.3 조건)
3. 실행기 구현·검수·설치
4. M-12A~J 통과(예방 조건 포함)
5. 3.4 충돌 권고 수용
6. 시작 절차로만 인증
7. 변경 시 M-12J 재확인

## 7. 승인 B에 필요한 실제 값

- [S4U 구성안](../tasks/B1_G5_S4U_LAUNCHER_PLAN.md) 6절입니다. 기존 B-1~B-14에 B-15~B-22를 더했고, B-5를 로컬 S4U 전용 표준 사용자로 한정했습니다.
- 더한 값: 로그온 권한, 작업·보안 설명자, 실행기 SHA, 요청 디렉터리, `verifier-tmp` SDDL, 호스트 `powershell.exe`, 실행기 로그·S8 절차, G1 공식 감사 PowerShell
- 모두 `OWNER_VALUE_PENDING`입니다. [운영 전환 계획](../tasks/B1_PHASE3_OPERATIONS_TRANSITION.md) 3.1절 표에 행을 추가했습니다.

## 8. Codex 표적 재검수 요청서

- [G5-MODEL 재검수 요청서](../tasks/B1_G5_MODEL_CODEX_REVIEW_REQUEST.md)
- 범위: G5-MODEL-01, G5-MODEL-02, 로컬 S4U 모델의 구현 가능성과 G5-TEMP-01 해결 조건
- G1-POLICY-01 RESOLVED·G2 E-1 ACCEPTABLE은 유지하고, 제품 코드 전체 재검수는 요청하지 않습니다. G5-TEMP-01을 운영 RESOLVED로 바꾸는 판정도 요청하지 않습니다.

## 9. 남은 사항

| # | 항목 | 주체 |
|---|---|---|
| 1 | Codex 표적 재검수(G5-MODEL-01/02, S4U 구현 가능성) | Codex |
| 2 | 보호 모델 3.4 충돌 권고 수용 여부(S8 위반·등록 영수증 처리) | OWNER |
| 3 | 승인 B 값 B-1~B-22, 특히 운영 기계·B-5 로컬 S4U 계정 | OWNER |
| 4 | A 진행 시: 실행기 구현 TASK → 자체 시험 → Codex 검수 → 설치 → M-12A~J | OWNER 승인 후 Claude·Codex |
| 5 | A 불성립 시: C-3 구현 TASK(새 제품 Commit, G2/G3 재실행) | OWNER 결정 |
| 6 | 공식 소스 Commit 확정(A1-6, 후보 `c77ed1a`), 공식 G1·G2·G3 운영 실행 | OWNER |
