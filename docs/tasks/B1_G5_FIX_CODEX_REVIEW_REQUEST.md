# Codex READ-ONLY 표적 재검수 요청 — B1 G5 운영 도구·A-1 게시 준비 FIX

- 작성: Claude Code, 2026-10-28. 근거: OWNER 지시 "G5/A-1 Codex FIX_REQUIRED 표적 수정" 11항
- **요청 범위:**
  - 범위는 2절 항목으로 한정합니다.
  - READ-ONLY입니다. 파일·Git 변경, 원격 Fetch·Push, ACL 변경은 하지 않습니다.
  - Codex가 직접 실행하지 않은 시험은 **제출 증거의 검토**라고 판정에 적어 주십시오.
- **판정 방식:**
  - Finding마다 **RESOLVED / OPEN**으로 판정합니다.
  - 새 Finding은 MAJOR·MINOR와 근거를 함께 적습니다.
  - Codex PASS여도 G5 실제 적용·A-1 게시·운영 Gate 승인이 아닙니다.
- 이 요청서는 OWNER가 전달하며, Claude가 Codex에 직접 보내지 않습니다. 신규 운영 정책은 Claude가 확정하지 않았습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준과 대상

| 항목 | 값 |
|---|---|
| Worktree·브랜치 | `E:\Madegame\Star_player_g4`, `phase3/g4-official-entry` |
| 이전 검수 기준 HEAD | `2e588093097baf0d7450248e224d2f51155ce6b6` |
| **수정 Commit** | **`306c402cbd0543d05dd9b63b075f608740e2b992`** (도구·문서) |
| 보고서 | [B1_G5_FIX_REPORT.md](../reports/B1_G5_FIX_REPORT.md) |
| 이전 판정 원문 | `E:\Madegame\g3x\codex-originals-g4\20261028T113717Z_verdict-g5-a1-fix-required.txt`(`6f126dbb…`) |
| G4 수용 소스 | `a980ea5a73ff2e3dfb032d2c3956d2d51c239711` |
| Clean Checkout | `E:\Madegame\g3x\g4clean3`(`306c402`) — 보고서 4절 |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player_g4 diff 2e588093097baf0d7450248e224d2f51155ce6b6 306c402cbd0543d05dd9b63b075f608740e2b992 -- tools/b1-ops docs/tasks
git -C E:/Madegame/Star_player_g4 diff --stat a980ea5 306c402 -- src game tests tools/StarPlayer.EvidenceVerifier tools/StarPlayer.SimHarness tools/verify.ps1 TASK.md docs/contracts content
git -C E:/Madegame/Star_player_g4 log --oneline 2e588093097baf0d7450248e224d2f51155ce6b6..HEAD
```

## 2. 판정을 요청하는 항목

1. **G5-R01**(MAJOR, `g5-acl.ps1` `Invoke-Preflight`, `B1OpsNative.cs`):
   - 스냅샷·생성·ACL 변경 전에 경로 체인 전체를 검증하는지
   - 상위 Junction·진단 Root 내부 Junction·검사 후 교체가 거부·차단되는지
   - 문자열 접두사가 아닌 실제 최종 경로로 격리를 판정하는지
   - 핸들 결속(삭제 공유 없음 + 데이터 접근)과 신뢰된 상위 조건이 TOCTOU를 충분히 줄이는지
   - 사후 Verify를 사전 보장으로 쓰지 않는지
   - **판단 요청:** 남는 경쟁(쓰기 권한 주체의 Apply 중 새 항목 생성)을 유지보수 조건으로 운영 전제화한 것이 타당한지(보고서 5절)
2. **G5-R02**(MAJOR):
   - 상속을 허용하는 비적용 중첩 Root를 변경 전에 거부하는지
   - 보호된 비적용 Root가 부모 적용 뒤에도 SDDL 불변인지
   - R-ARCHIVE 권한을 임의로 정하지 않는지
   - **판단 요청:** OWNER 지시 문구 "apply Root는 직접 수정하지 않는다"를 비적용 Root로 해석한 것(보고서 1절)
3. **G5-R03**(MAJOR):
   - 항목별 복구가 계속되는지
   - 실패·잔존을 `ROLLBACK_FAILED`·종료 코드 3으로 확실히 남기는지(예외 경로 포함)
   - 중간 디렉터리를 일지로 추적하는지, 기존 경로를 삭제하지 않는지
   - 정상 복구와 불완전 복구를 구분하는지
   - 트랜잭션이라고 주장하지 않는지
4. **G5-R04**(MINOR): 그룹 조회 실패가 PASS가 되지 않는지, 중첩 그룹 한계와 구분되는지
5. **G5-R05**(MINOR):
   - (PID, 정확한 생성 시각) 식별에 허용 범위가 없는지
   - 부모 인스턴스 미입증 시 미확인 처리하는지
   - 생성 시각·부모 인스턴스 기록과 `Adopt` 재판정으로 사후 대조가 가능한지
6. **A1-R01**(MINOR):
   - 작성자·Committer 이름·이메일과 메시지 전체가 SECRET·PRIVACY 검사 대상인지
   - 원시 Commit 객체 파싱이 구분 문자에 안전한지
   - 시험 문자열이 이력에 남지 않는지(`306c402` 검사 SECRET 0건)
7. **A1-R02**(MINOR):
   - `--no-follow-tags` 명시 Ref Push와 게시 전후 Ref 대조 절차
   - `push.followTags=true` 실측(자체 시험 `a1.push_no_follow_tags`)
8. **B4-R01**(MINOR):
   - 열거 전·중 Reparse·루트 이탈·하드 링크·중복·크기 검사
   - 주 바이너리 6개 역할 검사
   - EXE와 닫힌 역할의 구분, `CANDIDATE_INCOMPLETE`
9. **유지 여부:**
   - G5 Probe PASS(파일 변경 없음)
   - G4 코드 수용 상태
   - 제품·검증기·계약·D3·B1 Tag·Golden·Hash·RNG·Replay 불변
10. **시험·회귀 증거:**
    - 수정 전 반례 FAIL 기록(`g5fix\prefix_FAIL.txt`)
    - 자체 시험 68건
    - Clean Checkout verify 결과(보고서 4절)
11. **OWNER 미확정 운영 조건(판정 대상 아님, 확인만):**
    - 운영 기계·SID
    - R-ARCHIVE 권한
    - R-GIT 체인
    - ETW·Pin
    - 실제 ACL·Anchor
    - A-1 결정 A1-1~A1-7

## 3. 범위 밖

- OWNER 규범·운영 값의 대리 결정
- 운영 ACL·앵커·Pin 설치, 원격 생성·Push, 운영 G1~G9, Build Provenance 등록
