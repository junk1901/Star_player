# Star Player — B1 T11 표적 독립 재검수 실행

`docs/tasks/B1_T11_REVIEW_TASK.md`를 읽고, 해당 TASK에 정의된 범위에 따라 Codex T11 READ-ONLY 표적 재검수를 수행해.

## 검수 목적

기존 T11 FIX_REQUIRED의 MAJOR 5건·MINOR 3건이 실제로 해결됐는지 확인하고, 추가 OWNER 결정 7건이 승인 계약 및 별도 결정 기록대로 구현됐는지 독립 판정한다.

Claude Code의 완료 보고와 `verify.ps1` PASS만으로 수용하지 말고 실제 코드·계약·테스트 증거를 대조한다.

## 중점 사항

1. T11-01\~08 각각의 수정과 실제 인증 경로 결속 확인
2. OWNER 결정 7건의 구현 적합성 확인
3. 새 GAME_RESULTS와 PZL6의 역할·개수·Artifact DAG 검증
4. Production GameState·GameFact와 #6 Replay 비교 확인
5. 변경된 테스트 기대값 3건이 검증 완화인지 확인
6. 기존 Hash·RNG·Replay 불변성 확인
7. 최종 3,026 PASS / 0 FAIL / 0 SKIP 증거 확인
8. D3 보호 파일 및 승인 기준본의 무결성 확인

## 승인 출처 확인

OWNER 승인 및 결정 기록은 채팅 지시를 바탕으로 Claude가 작성했다.

저장소에 원본 채팅 사본이 없다면 승인 출처를 독립 확인했다고 주장하지 않는다.

승인 출처의 증거 충분성과 실제 코드의 계약 준수 여부를 별도 판정한다. 확인할 수 없는 부분은 OWNER 확인 필요 항목으로 기록한다.

## Dry Pilot 검증

9개 Run의 기존 산출물과 재현 결과를 비교하되, 재현 스크립트가 저장소에 없다는 사실을 명시한다.

제출된 결과만으로 확인 가능한 불변성과 직접 재실행해야 검증 가능한 사항을 구분한다.

## 수행 제한

- READ-ONLY
- 코드·문서 수정 금지
- Stage·Commit·Push 금지
- 미실행 시험을 직접 실행 PASS로 표기 금지
- 운영 Gate를 임의 PASS 처리하지 않음

## 최종 보고

TASK에 정의된 판정 형식에 따라 다음을 제출한다.

- T11-01\~08 개별 해결 판정
- OWNER 결정 7건 개별 계약 준수 판정
- 남은 MAJOR·MINOR 결함
- 승인 출처 및 검증 증거의 한계
- T10 수용 조건 충족 여부
- B1 코드 구현 수용 가능 여부
- Windows 운영 Gate 잔여 항목
- T11 최종 재검수 판정
- OWNER가 추가 결정해야 할 사항

Official Execution = NOT_READY\
Final Execution Freeze = PENDING\
Phase 3 = INCOMPLETE

검수가 끝나면 수정하지 말고 결과만 보고해.
