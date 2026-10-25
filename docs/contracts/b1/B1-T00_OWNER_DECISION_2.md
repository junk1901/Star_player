# B1 OWNER 추가 결정 기록 2 — NSC-4 최소 정정 · T05 codec 진입점 · T09 Registry 신원 조회

- 수신 일시: 2026-10-24T21:49:42+09:00 (Asia/Seoul, 이 작업이 지시를 받은 시각) · 방식: OWNER 명시적 텍스트 결정 ("Star Player B1 — OWNER 추가 결정 및 T11 이전 마무리") · 결정자: 지시를 내린 OWNER (이름·서명은 기록하지 않음)
- 바뀌지 않는 것: 승인 계약 `B1-T00_FINAL_CONTRACT_DRAFT.md` 바이트와 SHA-256 `1338a2f7e4bf2e95e9cbc3aa6ff29364791b8d774965dbce73a1e329a7b97d64`, [NSC-4·NSC-5 결정 기록](B1-T00_NSC4_NSC5_OWNER_DECISION.md) 원문, §16 T2 Fail-Closed, Hash·RNG·Replay, `OfficialApprovals.Current` 차단. 이 기록은 그 위에 더하는 결정이며, 충돌하면 계약 본문이 우선하고 충돌은 OWNER에게 보고합니다.
- 근거 제안: [OWNER 결정안](B1_OWNER_DECISION_PROPOSALS.md) 제안 1~3. 결정 3은 제안 3의 Recommended Option이 아니라 **Alternative(명시 조회 op)** 를 채택했습니다.
- T11 Codex 검수 범위에 포함됩니다. Official Execution = NOT_READY, Final Execution Freeze = PENDING, Git Commit·Push 금지는 그대로입니다.

## 결정 1 — NSC-4 B안 최소 정정

**OWNER 결정(요지).** B안의 보안 목표를 유지합니다. INHERIT_ONLY ACE는 그 객체에 실제 적용되는 ACE와 구분하되, 자식에게 상속되어 적용될 수 있는 ACE까지 무조건 무시하지 않습니다. 상위 디렉터리는 앵커 경로의 삭제·이름 변경·교체·권한 변경 가능성으로 검사합니다. 보호된 앵커 파일과 디렉터리에는 비TCB 주체의 실질적 변경 권한을 허용하지 않습니다. Reparse Point와 신뢰할 수 없는 경로 대체는 계속 거부합니다. ACL 검사 오류나 신뢰를 입증할 수 없는 경우는 Fail-Closed입니다. 운영 환경이 검증되지 않으면 PASS로 처리하지 않습니다.

**구현 규칙** (`tools/StarPlayer.SimHarness/Evidence/TrustAnchor.cs` `AnchorAcl`):

| 경로 구성요소 | 판정 대상 ACE | TCB 밖 주체에게 금지(Generic 매핑 후) | 소유자 |
|---|---|---|---|
| 앵커 파일 | INHERIT_ONLY가 아닌 Allow ACE(상속 `ID` 포함) | `FILE_WRITE_DATA`·`FILE_APPEND_DATA`·`FILE_WRITE_EA`·`FILE_WRITE_ATTRIBUTES`·`DELETE`·`WRITE_DAC`·`WRITE_OWNER` | TCB |
| 앵커가 있는 디렉터리 | 같음 | 위 항목(`ADD_FILE`·`ADD_SUBDIRECTORY`는 같은 비트) + `FILE_DELETE_CHILD` | TCB |
| 그 위 모든 디렉터리(드라이브 루트 포함) | 같음 | `DELETE`·`FILE_DELETE_CHILD`·`WRITE_DAC`·`WRITE_OWNER` (삭제·이름 변경·교체·권한 변경) | TCB |
| 모든 구성요소 | 그 객체에 적용되는 ACE가 `CREATOR OWNER`·`CREATOR GROUP`·`OWNER RIGHTS`를 가리키면 | 해석이 모호하므로 거부 | — |
| 상속 판정(INHERIT_ONLY 포함) | 각 구성요소의 상속 가능 Allow ACE를 **다음 구성요소**에 적용될 것으로 다시 판정(다음 구성요소 DACL이 보호(`P`)되면 상속 없음, 파일은 `OI`·디렉터리는 `CI`, CREATOR OWNER·OWNER RIGHTS → 그 구성요소의 소유자, CREATOR GROUP → 그 그룹) | 다음 구성요소의 금지 권한 | — |

- 그대로 유지: TCB 집합(SYSTEM·Administrators·TrustedInstaller), NULL DACL 거부, Allow·Deny 외 ACE 형식 거부, Deny ACE 미완화, 관리자(Elevated) 실행 거부, 모든 구성요소 Reparse Point 거부, 저장소·증거 루트 밖, 한 번 읽기.
- Fail-Closed: 서술자를 읽지 못한 구성요소(없음·권한 없음·기타 예외)는 위반입니다.

**보호 디렉터리 후보와 적용 ACL (OWNER가 관리자 권한으로 적용 — 이 작업에서는 만들지 않음):**

```
C:\ProgramData\StarPlayerTrust\            소유자 BUILTIN\Administrators, DACL 보호(상속 차단)
  SDDL 목표: O:BAG:..D:PAI(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;0x1200a9;;;<verifier_principal_sid>)
C:\ProgramData\StarPlayerTrust\trust-anchor.json   위 디렉터리에서 상속(쓰기 권한은 TCB만)
```

관리자 PowerShell(승격)에서 OWNER가 실행할 명령 예:

```
New-Item -ItemType Directory C:\ProgramData\StarPlayerTrust
icacls C:\ProgramData\StarPlayerTrust /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" "*<verifier_principal_sid>:(OI)(CI)RX"
icacls C:\ProgramData\StarPlayerTrust /setowner "*S-1-5-32-544"
```

`RX`는 `0x1200a9`(읽기·실행)입니다. 그룹 SID는 판정에 쓰이지 않습니다(상위에 CREATOR GROUP 상속 ACE가 없음). 적용 뒤 앵커 파일을 그 안에 TCB 계정으로 두고 `AnchorAcl.CheckPath`로 확인합니다.

**자체 검증(이 PC, 계정 생성·보안 설정 변경 없이):** `ApprovalAndTrustTests`
- 규칙 시험: SDDL 25종(구성요소별 금지 권한, INHERIT_ONLY 자체 미적용, 모호 SID 거부, NULL DACL, Object ACE, Deny 미완화, Generic 매핑)과 상속 판정 10종(INHERIT_ONLY가 보호되지 않은 자식에 도달하면 거부, 보호 DACL이면 상속 없음, OI/CI 구분, CREATOR OWNER·GROUP 치환)
- 후보 위치: 이 PC의 **실제** `C:\`·`C:\ProgramData` 서술자 + 위 목표 SDDL의 후보 디렉터리·앵커 파일 → 통과. 공격 변형 8종(디렉터리 상속 미차단, 표준 사용자 소유, 검증 계정 파일 추가, OWNER 등록 주체의 하위 삭제, 앵커 파일이 상속받을 INHERIT_ONLY 쓰기, 파일 소유자 비TCB, 검증 계정 파일 쓰기·DACL 변경) → 모두 거부
- 실제 경로: `C:\Windows\System32\drivers\etc\hosts`·`C:\Windows\System32\kernel32.dll` 사슬 → 통과(정상 반례). 사용자 프로필 아래 파일 → 이 사용자 SID 때문에 거부. 읽을 수 없는 경로 → 거부. 앵커 경로의 실제 Junction → Reparse Point 거부
- **운영 판정: 미검증.** 후보 디렉터리를 실제로 만들고 ACL을 적용하는 일, 별도 검증·OWNER 계정으로 쓰기·삭제·이름 변경·ACL 변경 시도가 실패함을 실측하는 일은 하지 않았습니다(OWNER 별도 허가 필요). 그래서 T2 운영 PASS가 아닙니다.

## 결정 2 — T05 #6 codec 공개 진입점

**OWNER 결정(요지).** 독립 EvidenceVerifier가 기존 Infrastructure의 실제 #6 v2 JSON Writer/Reader를 재사용하도록 최소 공개 진입점 하나를 추가합니다. 복제 구현 금지, JSON 형식·Hash·RoundTrip 의미 변경 금지, 무관한 internal 형식 공개 확대 금지, 경계 시험은 필요한 최소 예외만 근거와 함께 수정, 검증기 단독 #6 정상·변조 반례 시험.

**구현:**
- `src/StarPlayer.Infrastructure/Fixtures/EventJsonV2Codec.cs` — `public static class`, `Write(IReadOnlyList<GameEvent>) → string`, `Read(string) → Result<IReadOnlyList<GameEvent>>`, 둘 다 `ScriptedGameJson.WriteEventsV2`·`ReadEventsV2`에 그대로 위임(복제 없음)
- `tools/StarPlayer.EvidenceVerifier`: `EventJsonCodec.Infrastructure`가 기본 codec, `Verifier`의 #6은 주입이 없으면 이것을 씀(`Evidence.pzl6_codec_unbound` 경로 제거)
- 경계 시험 `tests/StarPlayer.Application.Tests/TrustedBoundaryTests.cs`: 공개 형식 목록에 `EventJsonV2Codec` 1개 추가(근거 주석), Domain 형식 금지 단정은 이 형식만 제외하고 유지, 이 형식의 메서드 서명 2개와 노출 형식(`GameEvent`·`Result<>`·`IReadOnlyList<>`·`String`)을 별도 시험으로 고정. Friend(`InternalsVisibleTo`) 목록 변경 없음. DTO·Reader 비공개 단정 유지

**자체 검증:** `Pzl6StandaloneTests`(검증기 단독: 주입 없는 실제 codec으로 Integration+#6 끝-끝 PASS, 손상 codec으로 만든 #6 문서 → `pzl6_differs`, 정상 문서를 손상 codec으로 검증 → `pzl6_differs`), `OfficialPzl6VerificationTests`(공개 진입점 Write 바이트·Read 결과 = 내부 codec, 주입·비주입 A+ 동일), `TrustedBoundaryTests` 19개.

## 결정 3 — T09 Registry 신원 명시 조회

**OWNER 결정(요지).** 일반 응답의 Registry ID 사후 비교 대신 명시적 초기 조회를 채택합니다. 닫힌 요청 형식에 읽기 전용 `get_registry_identity`를 추가합니다. Pipe Owner·DACL·클라이언트 신원 검사를 먼저 하고, 같은 인증된 연결에서 조회하며, 앵커의 `registry_instance_id`와 일치해야만 다른 연산을 진행합니다. 불일치·오류·연결 교체는 즉시 Fail-Closed. 기존 Register 권한과 요청 3종의 의미는 바꾸지 않습니다.

**추가 요청·응답 형식(이 결정으로 확정):**

| 방향 | 형식(JCS, R03-3 프레이밍) |
|---|---|
| 요청 | `{"op":"get_registry_identity"}` — 다른 멤버 금지(`Evidence.schema_invalid`) |
| 성공 응답 | `{"registry_instance_id": ID, "status": "FOUND"}` — 서비스 스냅샷의 `registry_instance_id` |
| 거부 응답 | 기존 오류 응답 `{"cause": …, "code": …, "status": "ERROR"}`(예: 미인가 주체 `Registry.unauthorized`) |
| 권한 | 인증된 모든 역할(OWNER·VERIFIER_SERVICE·EVIDENCE_READER) 허용, 읽기 전용(트랜잭션·쓰기 없음), 미인가 주체 거부 |

- 클라이언트 순서(R03-4): 1단계 주체 → 2단계 연결 → 3·4단계 Pipe Owner·DACL → 5단계 실제 AccessCheck → **7단계 같은 연결의 첫 요청 = `get_registry_identity`, 앵커 값과 같을 때만 세션 반환** → 그 뒤 기존 연산. 응답 형식은 결정 3 이전(Registry ID를 일반 응답마다 싣던 방식)에서 원래 형식으로 되돌렸습니다.
- 연결이 끊기거나 응답을 읽지 못하면 세션은 폐기되고 이후 호출은 재연결 없이 `Trust.registry_unavailable`입니다(R03-4 9단계).

**자체 검증:** `RegistryTests.The_registry_identity_request_is_closed_read_only_and_open_to_every_authenticated_role`(세 역할 응답, 미인가 거부, 추가 멤버·다른 op 거부, 쓰기 없음), `PipeRegistryTests`(실제 Pipe: 다른 Instance ID → Connect에서 `registry_mismatch`, 미인가 주체 → Connect에서 `Registry.unauthorized`, 끊긴 세션 재사용 불가, 기존 왕복·역할·실제 AccessCheck 거부 유지).

## 이번 결정으로 해소되지 않은 구현 해석 (OWNER 결정 대기 — 임의 승인하지 않음)

통합 보고서 7절 해석 1·2·3·6을 계약 원문과 대조한 결과입니다.

| # | 구현 해석 | 계약 원문 | 대조 결과 | 필요한 결정 |
|---|---|---|---|---|
| 1 | Unit PZL `violations`를 경기별·원인 카운터별 1건으로 생성 | §7: `Violation = {code, game_id?, event_id?, field?}`, `(game_id, event_id, field, code)` ordinal 정렬·중복 거부 | 형식·정렬은 계약과 같음. **생성 단위(무엇을 한 건으로 셀지)는 계약에 없음** | 생성 단위 승인(문서 바이트·A+에 영향) |
| 2 | RAW_INVENTORY 파일 형식 `{format: "simharness-raw-inventory-v1", artifacts: [ArtifactRef…]}` | §21: 역할·개수(검증 스냅샷별 1)·작성 시점·자기 참조 제외·DAG 순서만 규정 | **파일 형식·형식 ID는 계약에 없음** | 형식·형식 ID 승인(영수증 Raw 목록에 포함) |
| 3 | `witness_digest`의 `difference`·`standard_error`·`lower_bound`도 BITS64로 표기 | §18 식: `{side, strategy, rival, column, difference, standard_error, lower_bound, non_superiority_z: BITS64, holm_critical: BITS64\|null, holm_rejected}` | `: BITS64` 표기가 `non_superiority_z`·`holm_critical`에만 붙어 있고 **앞 세 필드의 표기 형식은 명시되지 않음** | 세 필드의 표기(BITS64 / F64 문자열 등) 확정 — 승인 `witness_digest` 값이 이에 따라 달라짐 |
| 6 | #6 최종 상태를 검증기 내부 구조 비교 + `GameState` 필드 인벤토리(누락 시 실패)로 비교 | §6: "구조적으로 비교… PreviewComparison을 재사용해도 되지만, GameState·Finalized Facts의 필드가 비교에서 빠지면 인증을 거부하는 커버리지 검사가 필요" / 추적표 F-26: "T05: public 위임 비교 메서드(Codex 리뷰)" — 구현 TASK(INFO-1) | §6 본문 요건(구조 비교·커버리지 검사)은 충족. **F-26의 Simulation public 위임 메서드 방식과는 다름**(Simulation 무변경을 택함). 결정 2는 codec만 다루며 이 비교 방식은 정하지 않음 | 현행 방식 수용 또는 F-26대로 Simulation 공개 비교 메서드 추가 |
