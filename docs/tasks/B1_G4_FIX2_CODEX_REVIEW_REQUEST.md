# Codex READ-ONLY 재검수 요청 — B1 G4 잔여 MINOR(G4-FIX-01·02)

- 작성: Claude Code, 2026-10-28. 근거: OWNER 지시 "G4 잔여 MINOR 2건 최종 표적 수정" 6항
- **요청 범위:**
  - G4-FIX-01·G4-FIX-02의 해결 여부, 이미 RESOLVED인 G4-01·03·04·NOTE의 재발 여부, 그 수정의 영향 범위로 제한합니다.
  - READ-ONLY입니다. 파일·Git 변경, 원격 Fetch·Push는 하지 않습니다.
  - Codex가 직접 실행하지 않은 시험은 **제출된 증거의 검토**라고 판정에 적어 주십시오.
  - 판정은 RESOLVED / NOT_RESOLVED(Finding별), 새 Finding은 MAJOR·MINOR와 근거로 주십시오.
  - Codex PASS여도 공식 운영 G4 PASS, 보안 수용, 운영 승인이 아닙니다.
  - 이 요청서는 OWNER가 전달하며, Claude가 Codex에 직접 보내지 않습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준과 대상

| 항목 | 값 |
|---|---|
| Worktree·브랜치 | `E:\Madegame\Star_player_g4`, `phase3/g4-official-entry` |
| 이전 검수 기준 | 검증 소스 `6110d5fcfde0a1d89b8aab1a6e5f84213d598d84`, 문서 HEAD `75e272503e6b199292c6bac7b95083b8c7ac4d75` |
| **수정·검증 소스 Commit** | **`a980ea5a73ff2e3dfb032d2c3956d2d51c239711`** |
| 보고서 | [B1_G4_FIX2_REPORT.md](../reports/B1_G4_FIX2_REPORT.md) |
| 이전 판정 원문 | `E:\Madegame\g3x\codex-originals-g4\20261027T134630Z_verdict-g4-fix-targeted-fix-required.txt`(`1a377b22…`, NOT_INDEPENDENTLY_VERIFIED) |
| Clean Checkout | `E:\Madegame\g3x\g4clean3`(`a980ea5`) — 보고서 3·5절 |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player_g4 diff 75e272503e6b199292c6bac7b95083b8c7ac4d75 a980ea5a73ff2e3dfb032d2c3956d2d51c239711
git -C E:/Madegame/Star_player_g4 log --oneline 75e272503e6b199292c6bac7b95083b8c7ac4d75..HEAD
```

## 2. 확인을 요청하는 항목

1. **G4-FIX-01 (TRX 실행 수준 오류):**
   - `TestProfiles.RunSummary`가 `ResultSummary`·`Counters`·`RunInfo`를 읽어 정상 Case 실패와 실행 인프라 실패를 나누는지
   - Failed Case + 실행 오류, Passed Case + 실행 오류, 종료 코드 0 + 실행 오류가 모두 `TestProfile.run_error`인지
   - 형태가 다르거나(뿌리·Namespace·ResultSummary·Counters 개수·정수·합계·outcome 누락, XML 오류, DTD) 하면 `TestProfile.trx`로 Fail-Closed하는지
   - 종료 코드와 `ResultSummary/@outcome`(0 ↔ `Completed`, 1 ↔ `Failed`)의 일관성
   - **판단 요청:** `Warning` RunInfo까지 실행 수준 문제로 거부하는 보수적 선택(보고서 2절)이 타당한지
   - 상위 코드는 기존 `Evidence.test_profile_mismatch`이고 새 Schema·인증 상태가 없는지(새 하위 원인 `TestProfile.run_error` 1개)
   - 정상 Case 실패(종료 코드 1 + Failed + 깨끗한 실행)가 여전히 FAIL Case 결과로 남는지
2. **G4-FIX-02 (출력 파이프 시간 제한):**
   - 종료 대기와 stdout·stderr 소진이 한 `CancellationTokenSource`(제한 시간 + 호출자 취소)를 쓰는지, `.Result` 무제한 대기가 없어졌는지
   - `WaitAsync(limit.Token)`이 동기 파이프 읽기가 취소에 응하지 않아도 반환을 보장하는지
   - 시간 초과·취소 시 프로세스 트리 종료 시도(`Kill(entireProcessTree: true)`)와 Reader 정리, 출력이 끝나지 않은 실행을 성공으로 보지 않는지
   - 하위 프로세스가 프로세스 트리 밖으로 빠져나간 경우의 한계 기록(보고서 2절)이 정확한지
3. **시험:** 보고서 3절의 반례가 각 Finding을 실제로 검출하는지(수정 전 코드로 실행한 FAIL 기록 포함), 파이프 유지 반례가 다른 시험과 격리되고 고아 프로세스를 남기지 않는지
4. **재발 여부:** G4-01·03·04·NOTE 관련 코드가 이번 diff에서 바뀌지 않았는지
5. **불변성:** 기존 시험 기대값·Golden·Hash·RNG·Replay, 승인 계약·D3, B1 Tag, 루트 `TASK.md`
6. **문서 NOTE:** [G5 준비 문서](B1_G5_A1_OPERATIONAL_PREP_DRAFT.md) 4절의 Windows·Root 자동 허용 설명이 정정됐는지
7. **Git·SourceLink:** Clean Checkout 빌드의 SourceLink Commit이 `a980ea5`이고, 그 URL이 A-1 공식 원격이 아닌 실험 통제값이라는 기록이 정확한지

## 3. 범위 밖

- OWNER 규범(N-5·N-6·N-7·N-9·N-10·N-11, OS·공유 런타임 및 동적 어셈블리 승인 Pin)의 대리 결정
- G5 운영 계정·ACL, 공식 인증 성공 경로, 자식 실행 체인 파일 로드 추적, A-1 비공개 원격, B-4 최종 Artifact·Build Provenance
- 운영 ACL·앵커·설치 설정 설치, 원격 생성·Push, 운영 G1~G9, Build Provenance 등록, 인증 발급
