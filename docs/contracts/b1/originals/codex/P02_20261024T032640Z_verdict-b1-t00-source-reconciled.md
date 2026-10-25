**최종 판정: `SOURCE_RECONCILED`**

이전 검수의 nullable 설명 불일치와 NSC-6 의미 계약 문제가 해결됐습니다. 현재 검수 범위에서 추가 정정이 필요한 계약 충돌은 발견하지 못했습니다.

1. **실제 검수 파일 — MATCH**

   - 파일: [B1-T00_FINAL_CONTRACT_DRAFT.md](E:/Madegame/Star_player/B1-T00_FINAL_CONTRACT_DRAFT.md)
   - 크기: **175,154바이트**
   - SHA-256: `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64`

   Claude 보고값과 정확히 일치합니다. 별도 `(4).md`와 동일하다고 가정하지 않았습니다.

2. **NSC-6 의미 계약 — MATCH**

   실제 [TASK.md OWNER 결정](E:/Madegame/Star_player/TASK.md:22)을 읽었으며, SHA도 보고값과 일치합니다.

   `ecbc0859c809c68151c93f92058c31a7ab0b84ce8d8cc9a4a25512b7dd06f825`

   [계약 §7](E:/Madegame/Star_player/B1-T00_FINAL_CONTRACT_DRAFT.md:665)에 다음이 정확히 반영됐습니다.

   - `completed_games`는 Completed만 집계하고, 나머지 세 관찰 값에는 Failed 경기의 해당 관찰도 포함합니다.
   - Unit 전체 실행 경기, PZL6 고정 20경기, Phase Root 전체 54개 Unit 및 Automated 한정 범위가 구분됩니다.
   - 경기별 관찰을 중복 없이 합산합니다.
   - NonAutomated Unit의 `pzl-10`은 NOT_APPLICABLE이어도 coverage를 기록합니다.
   - `coverage.pitches`와 Completed 경기만 합산하는 `Technical.counters.pitches`의 동치를 강제하지 않습니다.
   - 기존 0건 NOT_EVALUATED 규칙을 유지하며, Phase Root PASS에 새로운 coverage 0 금지 조건을 추가하지 않습니다.

   N05-a~e, N36-a·b, A10-a, A25-a·b도 이 정의와 일치합니다. 값·범위 불일치에는 기존 `Evidence.pzl_mismatch`를 사용하며, 정상적인 카운터 차이나 coverage 0 자체를 새 실패 조건으로 만들지 않습니다.

3. **nullable 설명 정정 — MATCH**

   [§4](E:/Madegame/Star_player/B1-T00_FINAL_CONTRACT_DRAFT.md:465)와 [2부 §2.2](E:/Madegame/Star_player/B1-T00_FINAL_CONTRACT_DRAFT.md:1846)가 일치합니다.

   - `frozen_games_per_cell`: non-null `P`, `plan.games_per_cell`과 동일.
   - Unit PZL `plan.run_result_*`: Schema상 nullable 유지. 정상 인증에서는 Complete 부모의 non-null 값과 동일.

   이전의 상충하는 설명은 해소됐습니다.

4. **마지막 편집 정정 — MATCH**

   현재 문서에서 네 항목을 확인했습니다.

   - [§0 6번](E:/Madegame/Star_player/B1-T00_FINAL_CONTRACT_DRAFT.md:32): NSC-4·5만 미해결, NSC-6 RESOLVED.
   - [5부](E:/Madegame/Star_player/B1-T00_FINAL_CONTRACT_DRAFT.md:2201): 검수 목록 1~7 정상.
   - [승인 SHA 표](E:/Madegame/Star_player/B1-T00_FINAL_CONTRACT_DRAFT.md:2168): S08 당시 SHA와 NSC-6 결정 당시 SHA를 구분.
   - 최종 승인 때 실제 TASK.md SHA를 재계산하며, 두 참고값으로 대신하지 못하도록 명시.

5. **변경 범위 외 규범 불변성 — 확인 범위 내 이상 없음**

   현재 문서와 이전 검수에서 확인한 규범을 대조한 범위에서는 Hash·RNG·Replay·Registry·통계 정책을 변경한 내용이 발견되지 않았습니다.

   다만 이전 SHA `295cca7e…6967c`의 원본 파일을 확보하지 못했으므로, **최신 편집까지의 전체 diff와 “그 외 변경 0건”은 미확인**입니다. 현재 파일의 정합성 판정과 변경 이력의 완전한 입증은 구분합니다.

6. **잔여 Gate — 미해결 상태 유지**

   [NSC-4·5](E:/Madegame/Star_player/B1-T00_FINAL_CONTRACT_DRAFT.md:1864)는 각각 다음 시점 전 필수 해결사항으로 남아 있습니다.

   - NSC-4: T06a Trust Anchor 검증 구현·운영 배치 전.
   - NSC-5: T09 Registry 설정 Reader/Writer 구현 전.

   P-1/AT-4를 포함한 T09 Windows 실측도 **미실시**로 유지됩니다. 통과한 것으로 표현되지 않았습니다.

7. **최종 판정 — `SOURCE_RECONCILED`**

   현재 SHA의 문서에 대한 원문 통합 종결을 권고합니다. 추가 계약 설계 검수 루프는 필요하지 않습니다. 기존 기술 계약의 `READY_FOR_OWNER_APPROVAL`과 이번 문서의 `SOURCE_RECONCILED`는 별개의 판정입니다.

8. **OWNER 승인에 필요한 마지막 조치**

   소유자가 최종 문서 바이트·SHA, 승인 시점의 HEAD·TASK.md SHA를 고정하고, 승인 범위와 잔여 Gate를 기록하면 됩니다. 이번 검수에서 소유자 승인이나 구현 허가를 수행하지 않았습니다.

파일·Git 변경, 빌드·테스트·Golden 생성·Windows 실측은 수행하지 않았습니다. Official Execution은 `NOT_READY`, Final Execution Freeze는 `PENDING`입니다.