# Star Player B1 — 운영 Gate 후속 계획 (계획만, 실행 없음)

- 작성: Claude Code, 2026-10-25. 근거: [B1_CODE_ACCEPTANCE_TASK.md](B1_CODE_ACCEPTANCE_TASK.md) 1절 5항
- **실행하지 않는 계획입니다.** 운영 보안 구성(계정·서비스·보호 디렉터리·ACL), 공식 실행, AT 실측은 시작하지 않았습니다. 각 단계는 OWNER의 별도 지시가 있어야 시작합니다.
- 코드 수정이 필요한 단계는 그 지시 범위에서 별도 TASK·Codex 검수를 거칩니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**
- 계약 근거: `B1-T00_FINAL_CONTRACT_DRAFT.md`(SHA `1338a2f7…7d64`) — §15·§16(Trust)·§17(R03 Registry 보안)·§18(E2-1~E2-7)·§21(Archive)·3.5(T09 수용 시험 AT-1~AT-14r)·N27·N12. NSC-4·NSC-5는 [결정 1](../contracts/b1/B1-T00_NSC4_NSC5_OWNER_DECISION.md)에 있습니다.

## 1. 의존성과 권장 순서

```
G1 B-4 공식 출력 파일명·경로 ─┐
G2 CRLF/LF 정책·빌드 입력 고정 ─┼─▶ G3 운영 Build Provenance·바이너리 SHA ─▶ G4 검증기 바이너리·Test Profile 실행 경로 신뢰
                              │
G5 보호 신뢰 앵커·Windows 서비스 계정 분리 ─▶ G6 AT-1~AT-14r·정식 AT-4/P-1 ─▶ G7 N27-d/e/f 실측
G8 운영 Environment Profile 승인 ─▶ G9 N12 비트 Golden·witness_digest 승인값
G3·G4·G6·G7·G9 ─▶ (별도 OWNER 지시) Final Execution Freeze 준비 → Official Execution 검토
```

- G1·G2는 서로 독립이며 G3의 선행 조건입니다. G5는 G1~G4와 독립으로 병행할 수 있습니다.
- G8은 운영 기계가 확정되어야 하며, G9는 G8의 승인 환경에서만 의미가 있습니다.
- 공식 실행·Freeze는 이 계획의 범위 밖입니다.

## 2. 단계별 내용

| ID | 작업 | 선행 조건 | 시험·수용 기준 | OWNER 결정 |
|---|---|---|---|---|
| **G1** | B-4 공식 출력 파일명·경로: 공식 결과 루트, `run/<run_id>/` 내부 배치(결정 3 §6), Preflight 기록 `preflight.json`·`preflight/<unit>.json`(R2-B 내부 배치)의 최종 이름 | 없음 | 확정 이름으로 생산자·검증기·시험이 같은 경로를 쓰고, 기존 Hash·Replay가 불변이며, 문서와 코드에 이름이 한 곳에서만 정의됨 | **필요**(§21 "파일명과 실제 출력 루트는 B-4에서 정함") |
| **G2** | CRLF/LF 정책과 운영 빌드 입력 고정. 실측: `content/tuning/virtual_tuning_v4.json` 작업 트리 CRLF(system `core.autocrlf=true`)·blob LF. `TASK.md`·`docs/tasks/2026-10-25_…`는 CRLF이며 `-text` 규칙 없음 | 없음 | 빌드 입력이 체크아웃 설정과 무관하게 같은 바이트(`-text` 또는 `eol=` 고정)이고, 내장 리소스 SHA = 고정 바이트, 보호 파일이 커밋·체크아웃 왕복 후 SHA 유지(D3) | **필요**(LF·CRLF 중 무엇으로 고정할지. 바이트가 바뀌면 튜닝 의미 Hash는 같아도 내장 리소스 Raw SHA·SimHarness 바이너리 SHA가 바뀜) |
| **G3** | 운영 Build Provenance: SIMHARNESS_BINARY·VERIFIER_BINARY·TEST_ASSEMBLY·DEPENDENCY·RUNTIME_CONFIG·TRUST_CONFIG 실측 SHA, build procedure clause | G1·G2, 승인 Git SHA(Commit 이후) | 깨끗한 체크아웃에서 Release 재빌드한 바이너리 SHA가 기록과 같음(재현성), `ApprovalRecord.verifiers[].binary_sha256` = VERIFIER_BINARY(PATCH-04) | **필요**(빌드 절차·기준 Commit) |
| **G4** | 검증기 바이너리 자기 SHA 대조, Test Profile 실행 경로 신뢰. 남은 위험: TEST_ASSEMBLY는 SHA 확인 후 경로로 실행(교체 창), 실행 중인 검증기 바이너리 자기 SHA 미대조 | G3 | 실행 바이너리 SHA = Build Provenance. Test Profile 어셈블리를 핸들 고정 또는 검증된 복사본에서 실행. 경로 교체 반례 거부 | 코드 변경 범위는 **필요**(구현 방식) |
| **G5** | 보호 신뢰 앵커 디렉터리 설치·ACL(NSC-4), Registry Windows 서비스 계정·서비스 SID 분리(NSC-5, R03-2), Reader 계정 | 운영 기계·계정 준비 | NSC-4 허용 목록 규칙 실측(검증 프로세스·작성자에게 쓰기·삭제·ACL 변경 권한 없음, 관리자 토큰 실행 거부), 서비스 설정 Reader/Writer 정합 | **필요**(계정·SID·경로 운영 값, §23) |
| **G6** | AT-1~AT-14r(계약 3.5), **정식 AT-4/P-1**(표준 사용자가 Owner=`registry_account_sid`로 Pipe 생성 → `ERROR_INVALID_OWNER`일 때만 PATCH-03 운영 사용) | G5 | 계약 3.5 표의 각 기대 결과 실측. AT-4 실패가 확인되지 않으면 PATCH-03 운영 사용 불가. 이전 1307 사전 측정은 PASS 아님 | 결과 수용은 **필요** |
| **G7** | N27-d(재연결 시 다른 서버 → 재인증 전 사용 불가), N27-e(서비스 권한 없는 SQLite 직접 수정 ACL 거부), N27-f(Impersonation 실패 차단·Revert 실패 FailFast = AT-11) 계정별 실측 | G5·G6 | 운영 계정 배치에서 각 반례 거부 실측. 현재는 이 계정의 Seam·단일 계정 시험만 있음 | 결과 수용은 **필요** |
| **G8** | 승인 운영 Environment Profile(OS·런타임·ucrtbase·runtime_files 3개·forbidden prefixes·math_golden) | 운영 기계 확정 | 운영 기계 C0가 Profile과 정확히 일치(E2-4), 변경 시 거부 | **필요**(운영 기계·Profile 승인) |
| **G9** | N12 비트 Golden(`UpperQuantile` j=1..36, 36비트), `witness_digest`(BITS64, 결정 3 §3), Holm 동률 Fixture | G8 | 승인 환경에서 생성한 값이 Profile `math_golden`과 같음(E2-5), 동률 Fixture 고정 | **필요**(승인값 채택) |

## 3. OWNER 결정이 필요한 항목 요약

1. G1 공식 출력 파일명·경로(B-4)
2. G2 줄바꿈 정책(LF/CRLF)과 보호 파일 `-text` 규칙. Commit 전에 정해야 D3 SHA가 저장소 왕복 후에도 유지됩니다([구현 목록](../reports/B1_IMPLEMENTATION_MANIFEST.md) 5절).
3. G3 운영 빌드 절차와 기준 Commit
4. G5 운영 계정·SID·Pipe·DB·앵커 경로 값(§23 운영 값)
5. G6·G7 실측 결과 수용
6. G8 운영 기계와 Environment Profile 승인, G9 승인값 채택
7. 그 뒤 Final Execution Freeze·Official Execution 착수 여부(이 계획 범위 밖)

## 4. 하지 않은 것

- 다음은 이 계획에서 하지 않았습니다: 계정 생성, 서비스 설치, ACL 설정, 보호 디렉터리 생성, AT 실측, 운영 Profile·Golden 생성, 공식 실행, Freeze.
- 코드·계약·OWNER 원문·`TASK.md`·D3 기준은 바꾸지 않았습니다.
