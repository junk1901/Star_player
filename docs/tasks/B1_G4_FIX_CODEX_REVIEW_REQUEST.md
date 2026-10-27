# Codex READ-ONLY 표적 재검수 요청 — B1 G4-01~04 수정

- 작성: Claude Code, 2026-10-27. 근거: OWNER 지시 "G4 Codex FIX_REQUIRED 표적 수정 및 재검증" 8항
- **요청 범위:**
  - G4-01·G4-02·G4-03·G4-04와 Codex NOTE(HoldTree 누락 파일)의 해결 여부, 그리고 그 수정의 영향 범위로 제한합니다.
  - READ-ONLY입니다. 파일·Git 변경, 원격 Fetch·Push는 하지 않습니다.
  - Codex가 직접 실행하지 않은 시험은 **제출된 증거의 검토**라고 판정에 적어 주십시오.
  - 판정은 RESOLVED / NOT_RESOLVED(Finding별), 새 Finding은 MAJOR·MINOR와 근거로 주십시오.
  - 이 검수는 공식 G4·G5 PASS, 보안 수용, 운영 승인이 아닙니다.
  - 이 요청서는 OWNER가 전달하며, Claude가 Codex에 직접 보내지 않습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준과 대상

| 항목 | 값 |
|---|---|
| Worktree·브랜치 | `E:\Madegame\Star_player_g4`, `phase3/g4-official-entry` |
| 이전 검수 기준 | `01d1626bbdfcfc28a59b154128db1e0ecc1ab821`(G4 구현 `430e306f…`) |
| 수정 코드 Commit | `ca312267df14b0bea6bf25d5bd69df0e363b5114` |
| 검증 소스 Commit | `6110d5fcfde0a1d89b8aab1a6e5f84213d598d84`(시험 격리 수정. `tools`·`src`·`game` 트리는 `ca31226`과 같음) |
| 보고서 | [B1_G4_FIX_REPORT.md](../reports/B1_G4_FIX_REPORT.md) |
| 이전 판정 원문 | `E:\Madegame\g3x\codex-originals-g4\20261027T103204Z_verdict-g4-fix-required.txt`(`2ac5cafe…`) |
| Clean Checkout | `E:\Madegame\g3x\g4clean`(`ca31226`, U6 1건 실패), `E:\Madegame\g3x\g4clean2`(`6110d5f`, GATE PASS) — 보고서 3.2·7절 |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player_g4 diff 01d1626bbdfcfc28a59b154128db1e0ecc1ab821 ca312267df14b0bea6bf25d5bd69df0e363b5114 -- tools tests
git -C E:/Madegame/Star_player_g4 log --oneline 01d1626bbdfcfc28a59b154128db1e0ecc1ab821..HEAD
```

## 2. 확인을 요청하는 항목

1. **G4-01:**
   - `%windir%`·`allowed_module_roots` 경로 신뢰가 없어졌는지(설정에 그 필드가 있으면 거부)
   - 모든 로드 어셈블리·모듈이 Pin 파일이어야 하는지
   - StarPlayer 어셈블리가 검증기 폴더 밖이면 거부되는지
   - 실제 사용 SimHarness가 사본인지(`Execution.simharness_load`)
   - 같은 이름 중복·별도 ALC·byte 로드가 거부되는지
   - 동적 어셈블리가 OWNER 목록(기본 빈 목록)으로만 허용되는지
   - OS·런타임 예외 집합을 임의로 만들지 않았는지(보고서 6절 실측과 대안)
2. **G4-02:**
   - 종료 코드와 TRX의 조합표(보고서 2절)가 정상 결과·실제 Case 실패·실행 인프라 실패를 올바르게 나누는지
   - 시작 실패·시간 초과·취소가 구분되는지
   - 기존 계약의 결과 상태·상위 오류 코드만 쓰는지
3. **G4-03:**
   - 공식 진입점이 설치 설정·앵커·Registry보다 먼저 E2-1을 수행하는지
   - 그 C0가 `Open`의 C0이고, C1·C2가 그것과 비교되는지
   - 결속 세션이 C0 없이 열리지 않는지
4. **G4-04:**
   - 고정한 작업 Root·결과 디렉터리 핸들
   - TRX 한 번 열기, 그 핸들에서 실제 경로·쓰기 주체 확인 후 같은 핸들에서 읽기
   - TRX 정확히 1개
   - 공식 세션의 R-WORK 쓰기 주체 규칙
   - 비공식 경로에서 ACL 규칙을 적용하지 않는 범위 결정(보고서 2절)이 타당한지
5. **NOTE:** 선언됐지만 디스크에 없는 Artifact가 거부되는지 봐 주십시오.
6. **시험:**
   - 신규 반례(보고서 3.1절)가 각 Finding을 실제로 검출하는지
   - 격리 자식 방식(`GenerateProgramFile=false`, `G4ChildProgram.Main`)이 기존 시험 실행에 영향이 없는지
   - TEST_ONLY 대역 Pin이 OWNER 값으로 오인될 여지가 없는지
7. **영향 범위:**
   - `PinnedDirectory` 공개화
   - `TestProfiles.Run` 시그니처(취소 인자, `TestRunner` 확장)
   - 기존 Test Profile 실행(U4·U6)의 결과 불변
   - Hash·RNG·Replay·Golden 불변
8. **Git·SourceLink:** Clean Checkout 빌드의 SourceLink Commit이 `6110d5f`인지, `ca31226`의 U6 실패 원인 분석(정적 Seam 경합)과 Collection 직렬화 수정이 타당한지, 그 URL이 아직 A-1 공식 원격이 아닌 실험 통제값이라는 기록이 정확한지 봐 주십시오.

## 3. 범위 밖

- OWNER 규범(N-5·N-6·N-7·N-9·N-10·N-11, OS·런타임 예외 집합)의 대리 결정
- 운영 ACL·앵커·설치 설정 설치, 원격 생성·Push, G1~G9, Build Provenance 등록, 인증 발급
