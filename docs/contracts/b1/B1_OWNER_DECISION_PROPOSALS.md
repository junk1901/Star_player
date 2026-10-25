# B-1 OWNER 결정안 (제안 기록 — 2026-10-24 OWNER 결정으로 처리됨)

> **처리 결과(2026-10-24T21:49:42+09:00):** 제안 1·2는 OWNER가 채택(세부 조건 추가), 제안 3은 Recommended Option 대신 **Alternative(명시 조회 op `get_registry_identity`)** 를 채택했습니다. 규범은 [OWNER 결정 기록 2](B1-T00_OWNER_DECISION_2.md)가 정본이고, 아래는 제안 당시 원문으로 보존합니다.

- 작성: Claude Code, 2026-10-24. 승인 계약(SHA `1338a2f7…7d64`)과 [NSC-4·NSC-5 결정](B1-T00_NSC4_NSC5_OWNER_DECISION.md)은 바꾸지 않았습니다. 아래 세 안은 OWNER가 채택해야 적용됩니다(제안 3은 현재 구현 방식의 해석 채택 여부).
- 형식: Issue · Reason · Recommended Option · Alternative · Impact (AGENTS.md)

---

## 제안 1 — NSC-4 B안 최소 정정 (앵커 ACL의 "실제 적용 권한" 판정)

### Issue

현행 NSC-4 B안은 "상속 ACE·그룹 ACE도 같은 규칙", "파일과 모든 상위 디렉터리에서 TCB 밖 주체에게 쓰기·삭제·하위 삭제·ACL·소유자 변경을 주는 ACE가 있으면 거부"입니다. 이 PC에서 실측하면 표준 Windows 경로가 모두 거부됩니다.

| 경로 | 거부 원인(실측 SDDL) | 그 ACE가 실제로 하는 일 |
|---|---|---|
| `C:\Windows\System32` 등 | `(A;OICIIO;GA;;;CO)` | **INHERIT_ONLY**: 이 디렉터리 자체에는 아무 권한도 주지 않음. 새로 만들어지는 하위 개체의 소유자에게 상속될 서식(CREATOR OWNER) |
| `C:\` | `(A;;LC;;;AU)` = `FILE_ADD_SUBDIRECTORY` | 새 하위 디렉터리를 **추가**할 수 있음. 이미 있는 이름을 바꾸거나 지울 수는 없음 |
| `C:\` | `(A;OICIIO;SDGXGWGR;;;AU)` | INHERIT_ONLY: 루트 자체에는 권한 없음 |
| `C:\ProgramData` | `(A;CI;DCLCRPCR;;;BU)` = 파일·하위 디렉터리 추가, EA·속성 쓰기 | 기존 경로 구성 요소를 교체·삭제할 수 없음 |

### Reason

1. INHERIT_ONLY ACE는 Windows 접근 검사에서 그 개체 자신에게 적용되지 않습니다. 같은 권한이 실제 하위 개체에 적용되면 그 하위 개체의 DACL에 상속 ACE(`ID`)로 나타나므로, 거기서 판정하면 빠짐이 없습니다.
2. 상위 디렉터리에서 앵커 경로를 **바꾸는** 수단은 기존 구성 요소의 삭제·이름 변경(그 구성 요소의 `DELETE` 또는 부모의 `FILE_DELETE_CHILD`), ACL·소유자 변경(`WRITE_DAC`·`WRITE_OWNER`)뿐입니다. 파일·하위 디렉터리 **추가**나 속성·EA 쓰기는 이미 존재하는 이름을 다른 개체로 바꾸지 못합니다. 비어 있지 않은 디렉터리를 Reparse Point로 바꿀 수도 없고, 모든 구성 요소의 Reparse Point 여부는 읽기마다 따로 검사합니다(§16 T2).
3. 소유자는 암묵적 `WRITE_DAC`·`READ_CONTROL`을 가지므로 모든 구성 요소의 소유자가 TCB여야 한다는 조건은 그대로 필요합니다.

### Recommended Option (최소 정정안)

NSC-4 B안의 TCB 집합(SYSTEM·Administrators·TrustedInstaller), NULL DACL 거부, 미지 ACE 형식 거부, Deny ACE 미완화, 관리자 실행 거부는 **그대로 둡니다**. 판정만 다음처럼 바꿉니다.

| 대상 | 판정에 쓰는 ACE | TCB 밖 주체에게 금지하는 권한(Generic은 File/Directory 매핑 후) | 소유자 |
|---|---|---|---|
| 앵커 파일 | INHERIT_ONLY가 아닌 ACE(상속 ACE `ID` 포함) | `FILE_WRITE_DATA`, `FILE_APPEND_DATA`, `FILE_WRITE_EA`, `FILE_WRITE_ATTRIBUTES`, `DELETE`, `WRITE_DAC`, `WRITE_OWNER` (현행과 같음) | TCB |
| 앵커가 있는 디렉터리와 그 위의 모든 디렉터리(드라이브 루트 포함) | INHERIT_ONLY가 아닌 ACE | `DELETE`, `FILE_DELETE_CHILD`, `WRITE_DAC`, `WRITE_OWNER` | TCB |
| 모든 대상 | `CREATOR OWNER`(S-1-3-0)·`CREATOR GROUP`(S-1-3-1)·`OWNER RIGHTS`(S-1-3-4)가 INHERIT_ONLY가 아닌 ACE에 있으면 | 해석이 개체마다 달라 **거부** | — |

- 보호 경로 운영안(OWNER 체크리스트에 추가): 관리자가 `C:\ProgramData\StarPlayerTrust\`를 만들고 소유자를 `BUILTIN\Administrators`로, DACL을 보호(상속 차단)·`SYSTEM:FA`·`Administrators:FA`·검증 계정·Reader `0x1200a9`(읽기)만으로 설정합니다. 앵커는 그 안에 둡니다.

**기존 계약과의 차이:** §16 T2 문장("쓰기·삭제·ACL 변경 권한을 주지 않아야")은 그대로이고, NSC-4 결정 기록의 "상속 ACE도 같은 규칙" 해석을 "그 개체에 실제로 적용되는 ACE(INHERIT_ONLY 제외)"로, 상위 디렉터리 금지 권한을 "경로 교체 권한"으로 좁힙니다.

**변경 파일(채택 시):** `docs/contracts/b1/B1-T00_NSC4_NSC5_OWNER_DECISION.md`(개정 기록 추가, 원문 보존), `tools/StarPlayer.SimHarness/Evidence/TrustAnchor.cs`(`AnchorAcl.Violation`에 `isAncestor` 인자, INHERIT_ONLY 제외, CREATOR 계열 거부), `tests/StarPlayer.SimHarness.Tests/Evidence/ApprovalAndTrustTests.cs`.

**수용 시험(채택 시):**
1. SDDL: `(A;OICIIO;GA;;;CO)`만 있는 디렉터리 통과, `(A;;GA;;;CO)` 거부, 상위 디렉터리의 `(A;;LC;;;AU)` 통과·`(A;;0x40;;;AU)`(하위 삭제) 거부·`(A;;SD;;;AU)` 거부, 파일의 `(A;ID;0x2;;;AU)` 거부(현행 유지), 소유자 비TCB 거부(현행 유지)
2. 이 PC의 실제 서술자 재판정(쓰기 없이 읽기만): `C:\`·`C:\ProgramData`·`C:\Windows\System32` 상위 사슬 통과 예상, 사용자 프로필 아래 경로는 소유자 위반으로 거부 예상
3. 운영 실측(관리자가 보호 경로를 만든 뒤): 앵커 위치 T2 PASS, 표준 사용자 검증 계정으로 앵커 파일 쓰기·삭제·이름 변경·ACL 변경 시도가 모두 실패(AT로 신설)

**보안 영향:** 실제 접근 검사와 같은 의미로 좁힐 뿐이며, 앵커 바이트를 바꾸는 경로(파일 쓰기·교체·ACL·소유자)는 계속 TCB 전용입니다. 추가 권한(`ADD_FILE`·`ADD_SUBDIRECTORY`)을 허용해도 기존 이름을 가리지 못합니다(Reparse 검사와 한 번 읽기는 유지). **회귀 영향:** 판정 함수 하나와 그 시험만 바뀌고 Hash·RNG·Replay는 무관합니다.

### Alternative

현행 B안 유지. 이 경우 앵커는 상위 사슬 전체가 TCB 전용이고 INHERIT_ONLY 서식조차 없는 전용 볼륨(예: 별도 드라이브, 루트 ACL을 TCB 전용으로 재구성)에서만 운영할 수 있습니다.

### Impact

채택 전까지 T2 실측 PASS·정상 인증 운영은 불가(Fail-Closed). 코드·시험의 현행 B안 구현은 그대로 둡니다.

---

## 제안 2 — T05 #6 codec을 독립 EvidenceVerifier에서 재사용하는 최소 변경

### Issue

#6 검증은 기존 v2 Writer/Reader(`StarPlayer.Infrastructure.Fixtures.ScriptedGameJson.WriteEventsV2`·`ReadEventsV2`, `internal`)를 그대로 써야 합니다(§6 "기존 v2 Writer", §11.3 "Infrastructure 측이나 독립 검증 도구"). Infrastructure의 friend 목록과 공개 형식 목록은 `TrustedBoundaryTests`가 고정합니다:
- `InternalsVisibleTo_lists_pin_the_trusted_boundary…`: Infrastructure friend = `Application.Tests`, `Infrastructure.Tests`
- `Infrastructure_public_api_exposes_only_the_composition_entry_points_and_nothing_raw`: 공개 형식 = `GameDiagnosticsComposition`, `PlayerInfoComposition`, `SqliteStorageDiagnostics`

그래서 검증기 바이너리 단독 실행에서는 #6이 `Evidence.pzl_incomplete`(codec 미연결)로 끝납니다.

### Reason

신뢰 경계는 OWNER·Codex가 정한 고정 목록이라 Claude가 임의로 넓히지 않았습니다.

### Recommended Option

Infrastructure에 **#6 전용 공개 진입점 하나**를 추가합니다.

```csharp
namespace StarPlayer.Infrastructure.Fixtures;
/// B-1 #6: the existing event-format-2 writer / reader, unchanged, for the independent evidence verifier.
public static class EventJsonV2Codec
{
    public static string Write(IReadOnlyList<GameEvent> events) => ScriptedGameJson.WriteEventsV2(events);
    public static Result<IReadOnlyList<GameEvent>> Read(string json) => ScriptedGameJson.ReadEventsV2(json);
}
```

- 서명에는 Domain 형식(`GameEvent`, `Result<T>`)만 있고 DTO·원본 Reader·세계 형식은 계속 숨깁니다.
- `TrustedBoundaryTests`의 공개 형식 고정 목록에 `EventJsonV2Codec` 1개를 더하고, `PublicSurface`가 Domain 형식을 금지하는 단정(`DoesNotContain(surface, t => t.Assembly == DomainAssembly)`)에는 이 형식만 명시 예외로 둡니다.
- 검증기 `Program`이 `new EventJsonCodec(EventJsonV2Codec.Write, EventJsonV2Codec.Read)`를 직접 결합합니다.

**기존 계약과의 차이:** 계약 변경 없음(신뢰 경계 고정 시험의 변경). **변경 파일:** `src/StarPlayer.Infrastructure/Fixtures/EventJsonV2Codec.cs`(신규), `tests/StarPlayer.Application.Tests/TrustedBoundaryTests.cs`(고정 목록 2곳), `tools/StarPlayer.EvidenceVerifier/Program.cs`(결합), 시험 1개.

**수용 시험:** (1) 공개 진입점의 Write 바이트·Read 결과가 `ScriptedGameJson`과 동일(Infrastructure.Tests), (2) 검증기 프로젝트만으로 #6 끝-끝 PASS, (3) 수정된 `TrustedBoundaryTests` 통과와 다른 공개 형식 0개, (4) 기존 2,952개 회귀.

**보안·회귀 영향:** 공개 표면 +1(이미 공개된 Domain 형식만 사용, 쓰기 경로 없음). Hash·RNG·Replay 무관.

### Alternative

`StarPlayer.EvidenceVerifier`를 Infrastructure friend 목록(`InternalsVisibleTo`)에 추가하고 `TrustedBoundaryTests` friend 고정 목록을 갱신. 코드는 더 적지만 검증기가 Infrastructure 내부 전체를 보게 되어 표면이 넓습니다.

### Impact

채택 전까지 #6 실제 codec 결합은 Infrastructure.Tests에서만 이루어지고, 운영 검증기 단독 #6은 `pzl_incomplete`입니다.

---

## 제안 3 — R03-4 7단계 "같은 핸들로 `registry_instance_id` 조회"의 수단 (T09, BLOCKED 항목)

### Issue

R03-4 7단계는 "같은 핸들로 `registry_instance_id`를 조회해 앵커와 비교"를 요구하지만, R03-5-3 5단계의 요청 형식은 `get`·`get_by_scope`·`register` 세 가지로 닫혀 있고 Instance ID를 묻는 요청이 없습니다. 응답 형식은 계약에 없습니다.

### Reason

새 요청 `op`를 더하면 닫힌 요청 형식(AT-14e·N27-c)을 바꾸게 되어 Claude가 임의로 정할 수 없습니다. 현재 구현은 **요청 형식을 바꾸지 않고**, 서비스가 모든 응답에 스냅샷의 `registry_instance_id`를 넣고 클라이언트가 응답의 다른 필드를 읽기 전에 그 값을 앵커와 비교합니다(`RegistryPipeClient.Call`). 불일치는 `Trust.registry_mismatch`이고 나머지 응답은 해석하지 않습니다(`PipeRegistryTests.Step_1_and_7_…`).

### Recommended Option

현행 구현을 계약 해석으로 채택: "7단계 조회 = 첫 요청의 응답에 실린 `registry_instance_id`, 모든 응답에서 반복 대조". 요청 형식 변경 없음. **변경 파일:** 없음(채택 기록만). **수용 시험:** 현행 `PipeRegistryTests` 4개 + Instance ID 불일치 시 응답 본문 미사용.

### Alternative

요청 형식에 `{"op":"instance"}`를 추가하고 연결 직후 한 번 조회(R03-5-3 5단계 닫힌 목록 개정, `RegistryRequests` 형식 1개·시험 추가). 7단계를 실제 요청 전에 끝낼 수 있으나 닫힌 요청 형식이 바뀝니다.

### Impact

채택 전까지 T09의 7단계는 **BLOCKED(해석 미확정)** 로 보고합니다. 코드는 Recommended Option 방식으로 동작하고, 다른 단계·Hash·RNG·Replay에는 영향이 없습니다.
