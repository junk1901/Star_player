# Star Player B1 — 코드 구현 최종 수용 준비 TASK

- 작성: Claude Code, 2026-10-25
- 근거: OWNER 채팅 지시 "Star Player — B1 코드 구현 최종 수용 준비"(2026-10-25 수신). 지시 요지는 1절에 옮겼습니다.
- 성격: **준비 작업**입니다. 문서 작성과 조사만 하며, 코드 수정·Stage·Commit·Push·Tag·운영 구성·공식 실행은 하지 않습니다.
- 루트 `TASK.md`(NSC-6 OWNER 증거)는 수정하지 않습니다. 진행 상황은 [B1_IMPLEMENTATION_LOG.md](../reports/B1_IMPLEMENTATION_LOG.md)에 남깁니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · OWNER 최종 수용 = **미실시**

## 1. OWNER 지시 요지

| # | 지시 | 산출물 |
|---|---|---|
| 1 | 이 TASK를 작성합니다. 루트 `TASK.md`는 수정하지 않습니다. | 이 문서 |
| 2 | Codex 최종 PASS를 별도 검수 기록으로 보존합니다. Codex 직접 확인 / Claude 로그 확인 / Codex 미실행 시험 / PASS 범위 / 운영 Gate 미완료를 구분하고, Codex 판정을 수정하거나 확대 해석하지 않습니다. | [B1_T11_CODEX_FINAL_PASS_RECEIVED.md](../reports/B1_T11_CODEX_FINAL_PASS_RECEIVED.md) |
| 3 | OWNER 승인·결정 1~3·R2 지시의 출처 확인표를 만듭니다. 원본 채팅이 없으면 그대로 표시하고, 원문을 지어내거나 소급 승인하지 않습니다. 최종 수용 확인서 **초안**을 작성하되, 서명·승인 완료 기록은 만들지 않습니다. | [B1_OWNER_ACCEPTANCE_DRAFT.md](../reports/B1_OWNER_ACCEPTANCE_DRAFT.md) |
| 4 | 구현 기준본 고정 준비: 추적 수정·미추적 파일 전수, 파일별 SHA-256, 시험·Golden, 원시 로그·Codex 기록, D3, 빌드 입력 바이트 상태, 포함·제외 산출물, 승인 후 Commit·Tag 계획. HEAD만으로 복원할 수 있다고 주장하지 않으며, 허가 전에는 Stage·Commit·Push·Tag를 하지 않습니다. | [B1_IMPLEMENTATION_MANIFEST.md](../reports/B1_IMPLEMENTATION_MANIFEST.md), `B1_IMPLEMENTATION_MANIFEST.sha256` |
| 5 | 운영 Gate 후속 계획(코드 수정 없음): B-4 파일명·경로, CRLF/LF·빌드 입력, 운영 Build Provenance·바이너리 SHA, 검증기 바이너리·Test Profile 경로, 보호 앵커·서비스 계정, AT-1~AT-14r·AT-4/P-1, N27-d/e/f, 운영 Environment Profile, N12·witness_digest. 각 항목의 선행 조건·시험 기준·OWNER 결정 필요 여부를 적습니다. | [B1_OPERATIONAL_GATE_PLAN.md](B1_OPERATIONAL_GATE_PLAN.md) |
| 6 | 금지: 계약·OWNER 원문·`TASK.md`·D3 기준 변경, 게임 계산식·Hash·RNG·Replay 변경, Commit·Push·Tag, Official Execution, Final Execution Freeze | — |
| 7 | 완료 보고 후 멈추고 OWNER 결정을 기다립니다. | 채팅 보고 |

## 2. 수용 기준

1. 위 산출물 5개가 있습니다.
2. 산출물에 Codex 원문이 없다는 사실이 명시되어 있고, OWNER가 전달한 요약은 원문 그대로 인용되어 있습니다.
3. 출처 확인표의 모든 항목에 "저장소 원문 있음/없음"과 확인 필요 여부가 있습니다.
4. 기준본 목록의 SHA는 이 작업 시점의 작업 트리 바이트이며, 생성 방법과 재계산 명령이 있습니다.
5. Commit·Tag 계획은 실행하지 않은 계획이며, OWNER 허가 조건을 명시합니다.
6. 마지막에 D1 링크와 D3 SHA를 다시 확인합니다. 코드·시험을 바꾸지 않았으므로 전체 verify는 다시 실행하지 않고, 마지막 verify(2026-10-25 19:40:30 `GATE: PASS` 3,041)를 근거로 둡니다.

## 3. 재개 방법

1. 이 문서와 구현 로그 "B1 코드 수용 준비" 절을 읽습니다.
2. 산출물이 있는지 확인합니다.
3. 기준본 목록은 `python -I` 스크립트로 다시 생성할 수 있습니다(목록 문서 4절). 코드나 문서를 고쳤다면 목록을 다시 생성합니다.
