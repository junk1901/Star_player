# Codex READ-ONLY 표적 재검수 요청 — G5-MODEL-01·G5-MODEL-02·로컬 S4U 모델

- 작성: Claude Code, 2026-10-30. 근거: OWNER 지시 "G1/G2 정책 확정 및 G5-MODEL-01/02 후속 조치"(2026-10-30) 9항
- 이전 판정: 최종 FIX_REQUIRED · G2 E-1 ACCEPTABLE · G1-POLICY-01 RESOLVED · G5-TEMP-01 OPEN_MAJOR · G5-MODEL-01 MAJOR · G5-MODEL-02 MINOR
- **요청 범위(이것만):**
  1. G5-MODEL-01: C-3의 Snapshot 색인·전체 Unit Manifest 결속 설계가 정확해졌는가
  2. G5-MODEL-02: gMSA의 외부 신뢰 범위를 명확히 정의했고, 현재 로컬 S4U 선택에서 제외했는가
  3. 로컬 S4U 모델의 구현 가능성과 G5-TEMP-01 해결 조건 검토
- **유지:** G1-POLICY-01 RESOLVED, G2 E-1 ACCEPTABLE. 제품 코드 전체 재검수는 범위 밖입니다. **이번 문서 재검수로 G5-TEMP-01을 운영 RESOLVED로 바꾸지 않습니다.**
- READ-ONLY: 파일·Git 변경, 원격 Fetch·Push, ACL·계정·작업 변경을 하지 않습니다. 이번 제출에는 새 실행 증거가 없습니다(문서·설계와 코드 읽기만).
- **판정 방식:**
  - G5-MODEL-01: **RESOLVED / OPEN**
  - G5-MODEL-02: **RESOLVED / OPEN**
  - 로컬 S4U 운영 경로: **OPERATIONAL_PATH_ACCEPTABLE / NEEDS_CHANGE / CODE_FIX_REQUIRED** (운영 PASS가 아님)
  - G5-TEMP-01 운영 상태: **OPEN_MAJOR 유지**(판정 대상 아님)
  - 전체 Verdict: **REVIEW_READY / FIX_REQUIRED / INSUFFICIENT_EVIDENCE**
  - 새 Finding은 MAJOR·MINOR와 근거를 함께 적습니다.
- 이 요청서는 OWNER가 전달하며, Claude가 Codex에 직접 보내지 않습니다. 검수 결과는 OWNER 승인이 아닙니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준과 자료

| 항목 | 값 |
|---|---|
| 공식 원격 `main`·제품 소스 후보 | `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f`(변경 없음) |
| 직전 검수 대상 이후 로컬 Commit(미게시) | `1fea694`(PS 7 기록) 다음의 이 요청서 Commit |
| G5-TEMP-01 보호 모델(개정 1) | [B1_G5_TEMP01_PROTECTION_MODEL.md](B1_G5_TEMP01_PROTECTION_MODEL.md) — 3절(S0~S9, 우회), **3.4절(등록 시점 한계·충돌 기록)**, 4절(M-12A~J), **5절(C-3 보완)**, 6절(유지 조건), **8절(gMSA)**, 9절(개정 이력) |
| 로컬 S4U 구성안 | [B1_G5_S4U_LAUNCHER_PLAN.md](B1_G5_S4U_LAUNCHER_PLAN.md) — 1~3절(구성), 4절(A/B 비교), 6절(승인 B 값) |
| 보고서 | [B1_G5_MODEL_FIX_REPORT.md](../reports/B1_G5_MODEL_FIX_REPORT.md) |
| OWNER 정책 기록 | [결정안 C절](B1_G1_G2_CONDITIONAL_DECISION_PROPOSAL.md) |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player_g4 log --oneline 1fea694..HEAD
git -C E:/Madegame/Star_player_g4 diff --stat c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f HEAD -- src game tests content docs/contracts TASK.md .gitattributes tools/StarPlayer.SimHarness tools/StarPlayer.EvidenceVerifier global.json Directory.Build.props tools/b1-ops
git -C E:/Madegame/Star_player_g4 diff 1fea694 HEAD -- docs/tasks/B1_G5_TEMP01_PROTECTION_MODEL.md
```

## 2. 판정을 요청하는 항목

### A. G5-MODEL-01 (보호 모델 5절)

1. 5.1절 결속 사슬 [사실] 확인: `Archive.Snapshot`이 제출 문서와 모든 ArtifactRef 파일을 핸들 고정 1회 읽기로 길이·SHA 대조하는지(`Verifier.cs` 168~190행, `Archive.cs`), 끊기는 곳이 TEMP 기록·경로 재읽기뿐인지(`Verifier.cs` 245~249행, `FamilyOfficial.cs` 457~494행, `DominanceOfficial.cs` 같은 구조)
2. 5.2절 표가 OWNER 조건 1~9를 빠짐없이, 정확히 충족하는지. 특히
   - 3: 색인 `manifest_sha256`·`manifest_path`와 Snapshot Manifest SHA·ArtifactRef 경로의 직접 대조
   - 5: 색인을 제출 문서 `artifacts`의 ArtifactRef로만 받고, 문서는 제출 ArtifactRef SHA와 `Compare(doc, expected)`로 결속한다는 사슬이 "승인된 Archive 원본 결속"으로 충분한지
   - 6~8: 같은 버퍼·경로 재열기 없음·TOCTOU 없음(경로 진입점은 CLI용 1회 읽기 래퍼)
3. 범위 밖으로 둔 것(R-BUILD에서 읽는 Seed Manifest 등)의 처리가 타당한지
4. 이전 C-3 폐기 이유(9절)가 정확한지, C-1·C-2를 부분 완화로 낮춘 판단

### B. G5-MODEL-02 (보호 모델 8절, 3.3·6절)

1. AD·KDS, 회수 허용 주체·호스트, 관리·위임 권한, 다른 호스트 통제의 같은 SID 영향, 운영 기계 밖 신뢰 경계의 기술이 정확하고 빠짐없는지
2. "OWNER가 승인하지 않으면 gMSA 사용 불가"와 이번 운영 선택 제외가 문서 전체에 일관되게 반영되었는지(3.2 S0, 3.3, 6절 2, S4U 구성안)
3. S4U와 gMSA를 같은 보장으로 표현하는 문장이 남아 있지 않은지

### C. 로컬 S4U 모델의 구현 가능성과 G5-TEMP-01 해결 조건

1. 3.3절 우회 차단(거부 권한 4종 + 배치 로그온만, 비TCB S4U = Identification 토큰, 작업 등록·수정의 관리자 필요, 작업 보안 설명자)이 문서 근거로 타당한지, 빠진 우회 경로가 있는지
2. **3.4절:** 영수증 등록이 검증기 내부(`Receipts.Issue`, `Receipts.cs` 166행)라 S8이 등록을 막지 못한다는 분석과 충돌 기록(권고: 세션 중 보호 전부를 예방 통제로 실측, 불가하면 C-3)이 타당한지. 운영 경로가 이 조건에서 수용 가능한지, 아니면 등록 전 검증이 필수라 C-3이 필요한지
3. S4U 제약(네트워크·EFS·DPAPI, 프로필·`dotnet` 첫 실행·Telemetry)을 M-12A로 다루는 것이 충분한지
4. M-12A~J 보완(예방 확인, 세션 중 프로세스 시계열, 우회 시도, EFS, 로그온 권한 변경 재검증)이 판정 가능한 수준인지
5. A/B 비교(S4U 구성안 4절)와 B 전환 조건의 정확성
6. `c77ed1a` 유지 조건 7가지(보호 모델 6절)와 승인 B 값 B-15~B-22의 완전성

## 3. 범위 밖

- 제품 코드 전체 재검수, G1-POLICY-01·G2 E-1 재판정, G5-TEMP-01 운영 RESOLVED 판정
- 실행기·C-3 구현, 운영 계정·작업·ACL 적용, 원격 Push, 공식 G1~G9, 공식 인증, Build Freeze
