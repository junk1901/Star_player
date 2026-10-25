1. **Executive Verdict — `READY_FOR_OWNER_APPROVAL`**

최신 R-A·R-B·R-C 통합본과 추가 제공된 직전 MINOR-03 원문을 대조했습니다. **MINOR-03은 RESOLVED이며, 이번 검수 범위에서 남은 계약 충돌은 없습니다.**

기존에 해결된 PATCH-01~06과 MINOR-01·02는 재설계하지 않았습니다.

2. **R-A — RESOLVED**

`evidence_reader_sids`는 필수이며, 누락 시 `Evidence.schema_invalid`입니다. 승인된 Reader 전체를 포함해야 하고, 승인된 Reader가 없을 때만 빈 배열을 허용합니다. 제출물·CLI·요청으로 집합을 변경할 수 없습니다. [통합본 R03-1](<C:/Users/user/.codex/attachments/e939e2be-016d-42f4-a945-024fc5bfac3d/붙여넣은 텍스트.txt:19>)

보충 원문으로 실제 연결 경로도 확인했습니다.

- Reader용 ACE를 서버가 생성합니다.
- 조회 전용 클라이언트에도 서버 인증 절차를 동일하게 적용합니다.
- Reader는 공식 EvidenceVerifier에 진입할 수 없습니다.
- 클라이언트 API 제한과 별개로 서버가 Reader의 모든 Register를 거부합니다.

따라서 초기 권한표의 **작성자 조회 허용·등록 금지**를 보존하며, 조회 권한이 인증 권한으로 승격되지 않습니다.

3. **R-B — RESOLVED**

Generic Mapping 이후 `(SID, mask)` 다중집합의 정확한 일치를 요구합니다. Registry 계정은 `0x001F01FF`, VERIFIER·각 OWNER·각 Reader는 `0x00120003`으로 각각 정확히 한 개입니다.

중복·누락·추가 SID·권한 부족·초과가 모두 거부됩니다. 이전 부분집합 검사 `m & ~0x00120083 == 0`과 별도 4(e)는 명시적으로 삭제됐습니다. MINOR-01의 AccessCheck 및 FORBIDDEN 검사는 그대로 유지됩니다. [검증 규칙](<C:/Users/user/.codex/attachments/e939e2be-016d-42f4-a945-024fc5bfac3d/붙여넣은 텍스트.txt:28>), [대체표](<C:/Users/user/.codex/attachments/e939e2be-016d-42f4-a945-024fc5bfac3d/붙여넣은 텍스트.txt:72>)

4. **R-C — RESOLVED**

시작 시 한 번 확보한 불변 설정 스냅샷에서 DACL과 역할표를 함께 생성합니다. 실행 중 재조회·독립 갱신을 금지하고, 역할 충돌·실행 계정·등록 가능 종류를 검증합니다. 첫 Pipe의 보안 설명자가 기대 구성과 다르면 시작을 거부하고 핸들을 정리합니다.

클라이언트가 직접 확인하는 것은 **앵커와 실제 Pipe DACL의 일치**이며, 서비스 내부 역할표까지 직접 검증했다고 주장하지 않습니다. 이 책임 구분도 정확합니다. [통합본 R03-5](<C:/Users/user/.codex/attachments/e939e2be-016d-42f4-a945-024fc5bfac3d/붙여넣은 텍스트.txt:41>)

5. **잔여 계약 충돌 — 없음**

OWNER·VERIFIER의 기존 등록 권한, Reader의 Get/GetByScope 허용과 Register 거부가 일치합니다. 최초 Trust Anchor의 독립성, Replay·Hash·RNG·GAME_EVENTS 불변 원칙과 P-1/AT-4 운영 전 Gate도 유지됩니다.

AT-14k~r은 권한 부족·중복 ACE·설정 누락·역할 충돌·Reader 등록 시도·실행 중 설정 변경을 구체적으로 검증합니다. 이들은 **T09 구현 수용 시험**이며, 현재 통과했다고 판단한 것은 아닙니다.

6. **단일 승인 기준본 준비도 — 준비 가능**

대체 순서는 명확합니다.

`통합 수정본 → 6개 Errata → 잔여 3개 정정 → PATCH-03 Windows 계약 → MINOR-01·02 → MINOR-03 → 최신 R-A·R-B·R-C`

최종 문서에는 이번 보충 원문의 **R03-3 Reader ACE 생성**과 **R03-4 1단계 클라이언트 유형 분리**도 반드시 포함해야 합니다. 삭제된 구문은 규범 본문에서 제거하고 변경 이력에만 남기면 됩니다. 추가 설계 결정은 필요하지 않습니다.

현재 확인값은 다음과 같습니다.

- HEAD: `257403ddae557a54c4013c17705370caeac7a2e0`
- TASK.md SHA-256: `2f4fb6f86ff4679d4a5a9c0ab6d5670a9b0ebfcac119086d230a1800b79939f7`
- Staging 0, 미추적 보호 파일 3개만 존재
- 검수 전후 보호 파일 SHA 동일

실제 승인 시에는 **통합된 규범 문서 자체의 내용·SHA**를 고정하고 HEAD·TASK.md SHA를 다시 확인해야 합니다. TASK.md 해시만으로 계약 본문의 동일성을 대신할 수는 없습니다.

7. **B1-T00 최종 판정 — `READY_FOR_OWNER_APPROVAL`**

계약상 종결을 권고합니다. 추가 설계 검수 루프는 필요하지 않습니다. 이는 소유자의 승인이나 T01 이후 구현 허가가 아닙니다.

파일 변경·빌드·테스트·Windows 실측은 수행하지 않았습니다. P-1/AT-4를 포함한 운영 전 시험은 남아 있으며, Official Execution은 `NOT_READY`, Final Execution Freeze는 `PENDING`입니다.