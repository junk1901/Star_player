# Codex READ-ONLY 표적 검수 요청 — G5-TEMP-01 보완 C-3 구현 (Snapshot 바이트 Replay)

- 작성: Claude Code, 2026-10-30. 근거: OWNER 지시 "G5-TEMP-01 코드 수정 방식(C-3) 전환"(2026-10-30) 10항
- 이전 판정: 최종 FIX_REQUIRED · G1-POLICY-01 RESOLVED · G2 E-1 ACCEPTABLE · G5-TEMP-01 OPEN_MAJOR · G5-MODEL-01 MAJOR · G5-MODEL-02 MINOR
- **요청 범위:**
  - 실제로 바꾼 인증·Replay 경로와 그 시험만 검수합니다. G4/G5 전체 재검수는 범위 밖입니다.
  - G1-POLICY-01 RESOLVED·G2 E-1 ACCEPTABLE 판정은 유지합니다.
- READ-ONLY: 파일·Git 변경, 원격 Fetch·Push를 하지 않습니다. 직접 실행하지 않은 시험은 **제출 증거 대조**라고 적어 주십시오.
- **판정 방식:**
  - 2절 항목 1~11 각각: **PASS / NEEDS_CHANGE**(근거)
  - G5-TEMP-01: **RESOLVED_IN_CODE / OPEN** — 코드 수준 판정이며 운영 G5 PASS가 아닙니다
  - G5-MODEL-01: **RESOLVED / OPEN**
  - G5-MODEL-02: **NOT_APPLICABLE_TO_CURRENT_PATH / OPEN**
  - 전체 Verdict: **REVIEW_READY / FIX_REQUIRED / INSUFFICIENT_EVIDENCE**
  - 새 Finding은 MAJOR·MINOR와 근거를 함께 적습니다.
- Codex 검수 전에는 G5-TEMP-01을 RESOLVED로 선언하지 않습니다. 이 요청서는 OWNER가 전달하며, 검수 결과는 OWNER 승인이 아닙니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · G5-TEMP-01 = **OPEN_MAJOR**

## 1. 기준과 자료

| 항목 | 값 |
|---|---|
| 작업 위치 | Worktree `E:\Madegame\Star_player_c3`, Branch `phase3/g5-temp01-c3`(시작 `7043bb90bb47bfa83d7f4a4e068b5b8ae78e54f0`) |
| 코드 Commit | `8ca9705539bc941919f5290de149c4678f5147dd`(Tree `fd87de669127228dd15cbe9cbc85272326a65ff7`) |
| 문서 Commit | 이 요청서를 담은 Commit |
| 공개 기준(변경 없음) | `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f` |
| 보고서 | [B1_G5T01_C3_REPORT.md](../reports/B1_G5T01_C3_REPORT.md) |
| 설계 | [G5-TEMP-01 보호 모델](B1_G5_TEMP01_PROTECTION_MODEL.md) 5절(C-3), 10절(개정 2: 구현 기록) |
| 증거(저장소 밖) | `E:\Madegame\g3x\phase3ops\c3\logs\`(RED 2회·GREEN·verify·G1 도구 실행·Golden·Dry Pilot), Clean Clone `E:\Madegame\g3x\c3clean` |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player_c3 show --stat 8ca9705
git -C E:/Madegame/Star_player_c3 diff -w 7043bb9 8ca9705 -- tools
git -C E:/Madegame/Star_player_c3 diff --stat 7043bb9 8ca9705 -- src game content docs/contracts TASK.md .gitattributes global.json Directory.Build.props tools/b1-ops
git -C E:/Madegame/Star_player_c3 grep -n "GetTempPath" 8ca9705 -- tools/StarPlayer.EvidenceVerifier
```

## 2. 판정을 요청하는 항목

1. **Snapshot 색인 결속:** Replay가 받는 색인 바이트가 `RunFile(snapshot, "DIAGNOSTICS", …)`의 배열 그 자체인지(`Verifier.cs` 236·246~247행), 그 배열이 ArtifactRef 길이·SHA로 검증된 것인지(`Archive.cs` 53행)
2. **모든 Unit Manifest 결속:** 계획의 모든 Unit에 대해 Snapshot Manifest 배열이 전달되고(237·246행), Replay가 Unit마다 순서·경로·`manifest_sha256`을 그 배열로 대조하는지(`FamilyOfficial.cs` 494~501행, `DominanceOfficial.cs` 478~484행). 중간·마지막 Unit 반례 T-5
3. **ArtifactRef와 색인 참조의 일치:** ArtifactRef SHA가 맞아도 색인 `manifest_sha256`과 다르면 거부되는지(T-2)
4. **동일 바이트 Replay:** SHA·중복 항목 검사·파싱이 같은 버퍼인지, 복사·재생성이 없는지
5. **파일 재읽기·TOCTOU 제거:** 검증기 인증 경로에 TEMP 기록·경로 재열기·`GetTempPath`가 남지 않았는지. 경로 진입점(래퍼)이 남아 있는 것이 우회 경로가 되지 않는지(검증기 호출처는 258·267행만)
6. **실패 시 영수증 미등록:** `OfficialEntry.cs` 89·93~97행의 순서상 `VerifyFamily` 거부 시 `IssueFamily`·`registry.Register`에 도달하지 않는지. T-7(RED: 공격 시 1건 등록 → GREEN: 0건)
7. **Golden·Hash·RNG·Replay 불변:** 보고서 7·8절(verify 3,071, Clean Clone verify·Golden·Dry Pilot, 동등성 시험)
8. **G5-TEMP-01 해결 여부(코드 수준):** RED가 실제 취약 조건을 재현하는지(T-4·T-6a·T-7), 시험 장치(스레드 한정 `Archive.BetweenCheckAndUse` 리플렉션)가 타당한지
9. **G5-MODEL-01 설계와 코드 일치:** 보호 모델 5.2절 조건 1~9와 구현의 대응(보고서 4절 표). 설계의 "검증기 쪽 추가 대조"를 따로 넣지 않고 기존 대조가 Snapshot 배열에 적용되게 한 판단
10. **G5-MODEL-02의 현재 경로 비적용:** 해결이 계정 형태와 무관해져 gMSA·S4U가 G5-TEMP-01 해결 조건이 아닌지
11. **변경된 소스 Commit과 G1 감사 도구 영향:** 보고서 9절(승인 도구가 lines 2·files 3 불일치로 FAIL, names·definitions 불변). 새 도구 버전 방침(고정값만 재산출, 규칙 완화 없음)이 타당한지

## 3. 범위 밖

- 수용된 G4/G5 전체 재검수, S4U 실행기(보류), 새 G1 도구 버전 작성(기준 Commit 확정 뒤)
- 운영 ACL·계정·작업, Trust Anchor·Pin, Push, 공식 G1~G9, 공식 인증, Build Freeze
