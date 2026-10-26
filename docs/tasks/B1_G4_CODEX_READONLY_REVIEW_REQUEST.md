# Codex READ-ONLY 검수 요청 — B1 G4 공식 인증 진입점·실행 결속, G5 경계 설계

- 작성: Claude Code, 2026-10-27. 근거: OWNER 지시 "Phase 3 신속 완료를 위한 Claude Opus 5.5 자율 개발 위임" 6항(G4 핵심 보안 구현 완료, G5 신뢰·권한 경계 설계 완료 시점)
- **요청 범위:**
  - READ-ONLY 검수입니다. 파일·Git 변경, 원격 Fetch·Push는 하지 않습니다.
  - Codex가 직접 실행하지 않은 시험은 **제출된 증거의 검토**입니다. 판정에 그 구분을 적어 주십시오.
  - 판정은 PASS / FIX_REQUIRED(항목별 MAJOR·MINOR와 근거)로 주십시오.
  - 이 검수는 공식 보안 수용, G4·G5 PASS, Build Provenance 등록, 운영 승인이 아닙니다.
  - 이 요청서는 OWNER가 전달하며, Claude가 Codex에 직접 보내지 않습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 기준과 대상

| 항목 | 값 |
|---|---|
| 작업 위치 | Worktree `E:\Madegame\Star_player_g4`, 브랜치 `phase3/g4-official-entry`(로컬 전용) |
| 기준 Commit | `96070c5fa1571b079df9c204597215db9da974ac`(문서 HEAD), 검증 소스 `00091736d5386aec3f5c67cf159291976e7133a2` |
| G4 구현 Commit | [보고서](../reports/B1_G4_IMPLEMENTATION_REPORT.md) 1절 |
| 구현 보고서 | [B1_G4_IMPLEMENTATION_REPORT.md](../reports/B1_G4_IMPLEMENTATION_REPORT.md) |
| G5·A-1 설계 | [B1_G5_A1_OPERATIONAL_PREP_DRAFT.md](B1_G5_A1_OPERATIONAL_PREP_DRAFT.md) |
| 근거 Finding | [B-4 규범 결정 자료](B1_B4_OWNER_NORMATIVE_DECISION_DRAFT.md) 4·7절, 1차 판정 원문 `E:\Madegame\g3x\codex-originals-b4\20261026T212819Z_verdict-b4-fix-required.txt` |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player_g4 log --oneline 96070c5fa1571b079df9c204597215db9da974ac..HEAD
git -C E:/Madegame/Star_player_g4 diff 96070c5fa1571b079df9c204597215db9da974ac -- tools tests
```

## 2. 확인을 요청하는 항목

1. **진입점 순서(G4-T1, F-13):** 설치 설정 → 시작 환경·호스트·Pin → 앵커 → Registry → `Certification.Open`(결속) → 발급 순서가 지켜지는지, 앞 단계 실패 시 뒤 단계(특히 Registry 연결·증거 읽기)가 일어나지 않는지 봐 주십시오. CLI가 앵커·Root·Pin 인자를 받지 않는지도 확인해 주십시오(§16 T1).
2. **설치 설정 형식:** `starplayer-verifier-install-config-v1`은 B1-T00 계약 Schema가 아닌 운영 제안입니다. §16 T1 "OWNER가 관리하는 설치·운영 설정"의 구현으로 타당한지, 닫힌 Schema(§13·§17 R03-1·§18.2·§18.3·§21)를 바꾸지 않았는지 봐 주십시오.
3. **결속 필수성:** 공식(ResolvedOfficial) 세션은 결속 없이 열리지 않고, TEST_ONLY 세션만 결속 없이 열리는지 봐 주십시오(`Execution.unbound`). 결속이 있으면 TEST_ONLY에도 같은 검사가 적용됩니다.
4. **F-01·F-02·F-09:** 자식 `dotnet test`가 절대경로와 고정 작업 디렉터리로 시작하는지, 공식 세션이 Pin된 `git`을 쓰는지 봐 주십시오. 비공식 경로의 `git`은 여전히 이름 검색입니다(보고서 7절 남은 위험).
5. **F-07(T3):** 영수증 `verifier`가 실행 중 DLL SHA와 일치하는 `verifiers[]` 항목인지, 위치·SHA·항목 수 검사의 Fail-Closed 조건이 충분한지 봐 주십시오.
6. **F-06(T4):** V-1(검증기 폴더 SimHarness 사본 = SIMHARNESS_BINARY)과 V-2(로드된 어셈블리·모듈 검사, 이후 로드 감시, E2-7 재검사)가 우회되지 않는지 봐 주십시오. 동적(`IsDynamic`) 어셈블리를 허용한 판단도 봐 주십시오(보고서 4절).
7. **F-03·F-04(T5):** Pin 파일을 쓰기·삭제 공유 없이 연 핸들로 해시하고 실행이 끝날 때까지 유지하는 방식이 "검사와 실행 바이트 불일치"를 막는지, 폴더 안의 Pin되지 않은 파일을 거부하는 규칙이 맞는지 봐 주십시오.
8. **F-05·F-18(T6):** `execution_pins` 방식의 한계(자식 프로세스가 로드하는 Pin 밖 파일)가 정확히 기록됐는지 봐 주십시오.
9. **F-17(T7):** 새 결과 디렉터리, TRX 하나, 한 번 읽기가 판정 입력 교체를 막는지 봐 주십시오.
10. **F-08(T8):** E2-1을 바꾸지 않고 OWNER 목록의 접두사를 추가로 거부하는 방식이 계약과 양립하는지 봐 주십시오(접두사 값은 OWNER 규범 N-7).
11. **원인 코드:** 새 실패는 기존 17개 상위 코드(`approval_mismatch`·`environment_mismatch`·`test_profile_mismatch`) 아래 새 원인 문자열(`Trust.install_config_*`, `Execution.*`, `Environment.startup_policy`, `TestProfile.runner`·`results_directory`·`trx`·`roots`)을 씁니다. §1.3과 충돌이 없는지 봐 주십시오.
12. **회귀:** 기존 Hash·Replay·Golden·영수증 바이트가 바뀌지 않았는지, 보고서의 verify 결과가 충분한지 봐 주십시오.
13. **Artifact 후보 규칙 v2:** 폴더 전체 − PDB·Coverage 대응표(441행 + 비Pin 31개, `00091736` N_S1 기준)가 B4-R01(EXE 일괄 제외 금지)과 F-19(보수적 포함 허용)에 맞는지, EXE를 DEPENDENCY로 두는 해석(F-14)을 봐 주십시오.
14. **G5 설계:** Root·계정·ACL 절차·외부 Pin·교체 차단·감사 보존이 F-20과 NSC-4 B에 맞는지 봐 주십시오.

## 3. 범위 밖

- 운영 ACL 적용, 앵커·설치 설정 설치, 계정 생성, 원격 생성·Push
- G1~G9 공식 판정, Build Freeze, Build Provenance 등록, 최종 인증 발급
- OWNER 규범 결정(N-5·N-6·N-7 등)의 대리 결정
