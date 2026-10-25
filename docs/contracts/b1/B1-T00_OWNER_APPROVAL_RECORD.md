# B1-T00 소유자 승인 기록

> Official Execution = `NOT_READY` · Final Execution Freeze = `PENDING` · Phase 3 전체 완료 승인 아님

## A. 승인 대상

| 항목 | 값 |
|---|---|
| 최종 규범 문서 | `B1-T00_FINAL_CONTRACT_DRAFT.md` (저장소 루트, 175,154바이트, UTF-8 BOM 없음, LF) |
| 최종 규범 문서 SHA-256 | `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64` |
| 기준 Git HEAD | `257403ddae557a54c4013c17705370caeac7a2e0` |
| TASK.md SHA-256 | `ecbc0859c809c68151c93f92058c31a7ab0b84ce8d8cc9a4a25512b7dd06f825` (NSC-6 OWNER 결정 증거, 원본 보존) |
| S00.5 원문 | `C:\Users\user\Downloads\붙여넣은 마크다운(20261020-150009).md`, 65,759바이트, SHA-256 `578bf4af24f376e1d799d3b69ddf9fd71f8e956adde795324db91e7c3ce0a3d9` (2026-10-24 Claude 재계산 일치) |

## B. 승인 근거

- 기술 계약 판정: Codex `READY_FOR_OWNER_APPROVAL` (S08)
- 원문 통합 검수: Codex `SOURCE_RECONCILED`
- 위 두 판정은 OWNER가 승인 지시에서 근거로 명시한 기존 Codex 검수 결과입니다. 판정 원문은 이 저장소에 보관되어 있지 않습니다.
- **확인되지 않은 자료 보존 상태:** S00~S07 원문 자료집 `StarPlayer_B1-T00_Claude_계약원문_전체자료집.md`는 저장소와 `C:\Users\user\Downloads`·Documents·Desktop에서 찾지 못했습니다(2026-10-24). 로컬 보관 위치는 **미확인**이며 임의로 만들지 않았습니다. 계약 초안 머리말의 "8건 SHA 일치"는 초안 작성 당시 기록이며 이번에 재확인하지 못했습니다.

## C. 승인 사실

- 승인자: 본 작업 지시를 발행한 프로젝트 OWNER (개인 이름·서명은 기록하지 않음)
- 승인 일시: 2026-10-24T14:25:36+09:00 (Asia/Seoul, 승인 지시 수신 후 Claude가 측정한 시각)
- 승인 방식: OWNER의 명시적 텍스트 승인 (Claude Code 대화)
- 승인 범위: B1-T00 기술 계약 채택(위 SHA의 바이트 그대로), 기준본 고정 방식, NSC-4 B안, NSC-5 권장안, B1-T00~T10 일괄 구현 허가
- 불포함: Official Execution, Final Execution Freeze, AT-4(P-1)·AT-1~AT-14r 실측 통과, §23 운영 값(Cohort·Seed·Pin·SID·Pipe·DB 경로·환경 실측·비트 Golden), T11 PASS, Phase 3 완료
- 계약을 바꾸려면 새 문서 SHA와 새 승인 기록이 필요합니다. NSC-4·NSC-5는 승인 문서 본문을 덮어쓰지 않고 [별도 결정 기록](B1-T00_NSC4_NSC5_OWNER_DECISION.md)에 둡니다.

## D. 기준본 고정

- 인코딩·개행: UTF-8 BOM 없음, LF (현재 바이트 그대로)
- 저장 위치: 저장소 루트 `B1-T00_FINAL_CONTRACT_DRAFT.md` 유지
- Git 속성: `.gitattributes`에 `B1-T00_FINAL_CONTRACT_DRAFT.md -text` 추가 (`git check-attr text` = `unset` 확인)
- 별도 보존본: `docs/contracts/b1/B1-T00_FINAL_CONTRACT.approved.md` (같은 바이트, 읽기 전용 속성, `-text`)
- SHA 목록: [B1-T00_BASELINE.sha256](B1-T00_BASELINE.sha256) (`sha256sum -c` 형식)
- 검증 절차: `tools/verify.ps1` 항목 `D3`이 두 계약 파일과 TASK.md·사용자 PDF 2개의 SHA-256을 목록과 대조합니다. 하나라도 다르면 FAIL입니다.
- 적용 후 재계산(2026-10-24): 루트 파일·보존본 모두 `1338a2f7…7d64` 일치, TASK.md `ecbc0859…f825` 일치
- Git Commit·Push: 하지 않음 (별도 승인 필요)
