# Codex READ-ONLY 표적 재검수 요청 — B1 G5·A-1 잔여 Finding 3건 (2차 FIX)

- 작성: Claude Code, 2026-10-28. 근거: OWNER 지시 "G5/A-1 잔여 Finding 3건 표적 수정" 7항
- **요청 범위:**
  - 범위는 2절 항목으로 한정합니다.
  - READ-ONLY입니다. 파일·Git 변경, 원격 Fetch·Push, ACL 변경은 하지 않습니다.
  - Codex가 직접 실행하지 않은 시험은 판정에 **제출 증거 대조**라고 적어 주십시오.
- **판정 방식:**
  - Finding마다 **RESOLVED / OPEN**으로 판정합니다.
  - 새 Finding은 MAJOR·MINOR와 근거를 함께 적습니다.
  - 최종 Verdict는 **REVIEW_READY / FIX_REQUIRED / INSUFFICIENT_EVIDENCE** 중 하나입니다.
  - Codex PASS여도 G5 실제 적용·A-1 게시·운영 Gate 승인이 아닙니다.
- 이 요청서는 OWNER가 전달하며, Claude가 Codex에 직접 보내지 않습니다. 신규 운영 정책은 Claude가 확정하지 않았습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준과 대상

| 항목 | 값 |
|---|---|
| Worktree·브랜치 | `E:\Madegame\Star_player_g4`, `phase3/g4-official-entry` |
| 이전 검수 HEAD | `fb1e33838000a86534a18de3210e53635ddd3286` |
| **개발·검증 기준 Commit** | **`4a3d61b7f4b30db5cc0c5061ad0adcbf90e340df`**(도구·`docs/tasks` 두 문서) |
| 후속 문서 Commit | 이 요청서·보고서·`CURRENT_STATE.md`·Dashboard. 검증 대상이 아님 |
| 보고서 | [B1_G5_FIX2_REPORT.md](../reports/B1_G5_FIX2_REPORT.md) |
| 이전 판정 | 이번 FIX_REQUIRED 원문은 저장소 밖 원문 폴더에 아직 없음(OWNER 지시의 요약이 근거). 1차 판정 원문은 `E:\Madegame\g3x\codex-originals-g4\20261028T113717Z_verdict-g5-a1-fix-required.txt`(`6f126dbb…`) |
| 이전 수정 검증 소스·G4 수용 소스 | `306c402`, `a980ea5a73ff2e3dfb032d2c3956d2d51c239711` |
| Clean Checkout | `E:\Madegame\g3x\g4clean3`(`4a3d61b`) — 보고서 4절 |
| 증거(저장소 밖) | `E:\Madegame\g3x\g5fix2\` — 보고서 2·3·4절의 SHA |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player_g4 diff fb1e33838000a86534a18de3210e53635ddd3286 4a3d61b7f4b30db5cc0c5061ad0adcbf90e340df
git -C E:/Madegame/Star_player_g4 diff --stat a980ea5 4a3d61b -- src game tests tools/StarPlayer.EvidenceVerifier tools/StarPlayer.SimHarness tools/verify.ps1 TASK.md docs/contracts content
git -C E:/Madegame/Star_player_g4 log --oneline fb1e33838000a86534a18de3210e53635ddd3286..HEAD
```

## 2. 판정을 요청하는 항목

1. **G5-FIX-01**(MAJOR, `g5-acl.ps1` `Get-ObjectKey`·`Invoke-Preflight`·Apply·`Restore-Snapshot`, `B1OpsNative.cs` `SetSddlExact`·`FileSystem`) — RESOLVED / OPEN
   - 모든 변경 대상 객체를 변경 전에 같은 핸들로 링크 수 1, 파일 ID 고유, NTFS로 확인하는지
   - 읽기 실패·NTFS 아님에서 Fail-Closed인지
   - 거부 시 스냅샷·디렉터리 생성·변경이 없는지
   - `SetKernelObjectSecurity` 전환으로 이름 순회 전파가 없어졌는지
   - 계산한 상속 사본(`OICIID`/`ID`)이 Windows 상속과 같은지(시험 `acl.fix01_exact_descriptors_equal_windows_inheritance`)
   - 첫 변경 직전·쓰기 직전·마지막 재확인과 같은 핸들 복구가 충분한지
   - 스냅샷 v3 객체 ID와 Restore 객체 일치 검사
   - **판단 요청:**
     - 남는 경쟁(확인·쓰기 사이 링크 생성 후 마지막 재확인 전 삭제, Apply 중 새 항목)을 유지보수 조건으로 운영 전제화한 것이 타당한지(보고서 5·6절)
     - "apply Root" 문구를 비적용 Root로 해석한 것(보고서 1.1절)
2. **G5-R05**(MINOR, `B1OpsNative.cs` `IProcessApi`·`ModuleReader`·`Win32ProcessApi`, `g5-pins.ps1` Trace) — RESOLVED / OPEN
   - 생성 시각 확인과 모듈 열거가 같은 `OpenProcess` 핸들 하나에서 이루어지는지
   - PID로 다시 여는 경로가 없는지
   - 종료·불완전 열거·핸들 실패가 모듈 없이 실패 또는 미검증으로 처리되는지
   - 핸들이 항상 해제되는지
   - 결과에 PID·정확한 생성 시각·식별 근거가 남는지
   - 시험 대역으로 만든 결정적 경쟁 반례가 타당한지
   - 수정 전 증거가 "이전 호출 형태의 모형"이라는 점의 충분성
3. **A1-R02**(MINOR, `a1-refcheck.ps1`, A-1 문서 2.2절 6단계·7절) — RESOLVED / OPEN
   - `ls-remote --refs`가 `HEAD`·Peeled 줄을 승인 Ref로 오인하지 않는지
   - `refs/heads/main`·`refs/tags/*`를 정확한 객체 ID(Annotated는 Tag 객체)로 비교하는지
   - 기대 밖 Ref가 계속 거부되는지
   - 조회 오류가 PASS가 아닌지
   - 로컬 bare 시험 12건
4. **기존 RESOLVED 6건 재발 여부:** G5-R01·R02·R03·R04, A1-R01, B4-R01
   - 특히 G5-R01 경로 결속과 G5-R03 복구가, 새 전파 없는 쓰기·객체 ID 복구와 함께 유지되는지
5. **신규 MAJOR·MINOR**
6. **변경 전후 반례 증거:** 수정 전 FAIL(보고서 2절)과 수정 후 PASS(3절)의 분리
7. **전체 회귀 및 SourceLink 정합성:**
   - Clean Checkout verify, D1·D3, PCK(보고서 4절)
   - 제품·검증기·계약 불변(7절)
   - A-1 Secret 검사
   - Golden·Dry Pilot은 재실행하지 않고 이전 증거를 재사용했습니다(4절)
8. **유지 여부:**
   - G5 Probe PASS(`g5-probe.ps1` 변경 없음)
   - G4 독립 검수 PASS 상태
   - B1 Tag·Golden·Hash·RNG·Replay 불변
9. **운영 G5에 남는 실측 조건(확인만):**
   - 유지보수 조건의 운영 모드 실측
   - 다른 계정 소유자 복구·승격 경로
   - NTFS 외 볼륨 거부 분기
   - ETW 기반 Pin
   - 숨겨진 Ref
10. **OWNER 미확정 운영 조건(판정 대상 아님, 확인만):**
    - 운영 기계·SID
    - R-ARCHIVE 권한
    - R-GIT 체인
    - 실제 ACL·Anchor
    - A-1 결정 A1-1~A1-7

## 3. 범위 밖

- OWNER 규범·운영 값의 대리 결정
- 운영 ACL·앵커·Pin 설치, 원격 생성·Push, 운영 G1~G9, Build Provenance 등록
