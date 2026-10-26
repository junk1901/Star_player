# Codex READ-ONLY 사전 검수 요청 — B1 B-4 빌드 절차·실행 신뢰 경계

- 작성: Claude Code, 2026-10-26. 근거: OWNER 지시 "B-4 부분 정책 수용 및 미해결 신뢰 경계 검토" 8항
- **요청 범위:**
  - 설계와 증거에 대한 READ-ONLY 사전 검수입니다. 파일·Git 변경, 빌드·시험·도구 실행, 원격 Fetch·Push, Stage·Commit·Tag는 하지 않습니다.
  - 운영 G1~G9의 실측 PASS를 대신하지 않습니다. 이 검수 결과로 B-4 동결, G3 PASS, Build Provenance 등록, 운영 승인을 하지 않습니다.
  - 쟁점마다 **CONFIRMED / NOT_A_DEFECT / NEEDS_OWNER_DECISION / MISSING_FINDING(새 항목)** 중 하나와 근거를 주십시오. 아래 F-xx는 Claude Code의 잠정 후보이며 판정이 아닙니다.
  - 이 요청서는 OWNER가 전달하며, Claude가 Codex에 직접 보내지 않습니다.
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · Artifact 집합 = **CANDIDATE_INCOMPLETE**

## 0. 표적 재검수 요청 (2차, 2026-10-27)

- **근거:** OWNER 지시 "B-4 Codex FIX_REQUIRED 최소 정정"(2026-10-27). 1차 판정 FIX_REQUIRED(B4-R01·B4-R02, 모두 MINOR)
- **범위:** B4-R01·B4-R02 해결 여부와 수정 영향 확인으로 제한합니다. 1차 쟁점 1~10의 재검수는 요청하지 않습니다. READ-ONLY이며 파일·Git 변경, 빌드·시험·도구 실행, 원격 Fetch·Push는 하지 않습니다.
- **대상:** 초안 [B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md](B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md) 개정 2(18절에 정정 전후 문장)와 이 요청서의 정정 문장(2절 쟁점 8·9, 3절 F-15 행)

| 확인 요청 | 위치 |
|---|---|
| B4-R01: SimHarness·Verifier Apphost(8개)와 VSTest `testhost.exe`(6개, Test Profile 대상 폴더 4개)를 구분했는지. 어떤 EXE도 일괄 제외하지 않고 PENDING으로 두었는지. 로드 추적 없이 LOAD_CONFIRMED·UNUSED 분류가 남아 있지 않은지. 109개가 CANDIDATE_INCOMPLETE로 유지되는지. EXE 바이트 동일 사실이 따로 보존되는지. Apphost 최종 결정을 공식 실행 명령 확정 전으로 미뤘는지 | 초안 7.3·7.4·7.7·8.3·8.4·18.1절 |
| B4-R02: "광고된 Ref 미확인 / Commit 객체·Raw 콘텐츠 조회 가능성 미검증 / 공식 원격 출처 검증 미완료"로 정정되고, 원격에 없다거나 조회 불가라는 확정 서술이 남아 있지 않은지. Ref 값 `7456f44…`가 그대로인지 | 초안 2·11.2·11.4·18.2절, 이 요청서 2절 쟁점 9·3절 F-15 |
| 수정 영향: A1~A11 수용 범위, 18.4절 미확정 항목, 그 밖의 절이 바뀌지 않았는지. 18.3절이 전달된 판정을 바꾸지 않고, 전달되지 않은 판정을 채우지 않았는지 | 초안 0·18.3·18.4절 |

- 판정은 PASS / FIX_REQUIRED(항목별 근거)로 주십시오.
- 위 머리말과 1절 이하는 1차 요청(2026-10-26) 본문입니다. B4-R01·B4-R02에 해당하는 문장만 정정했고, 정정 위치에 "(2차 정정)"을 붙였습니다.

## 1. 기준과 대상

| 항목 | 값 |
|---|---|
| 검증 소스 Commit | `00091736d5386aec3f5c67cf159291976e7133a2` |
| 저장소 HEAD | `96070c5fa1571b079df9c204597215db9da974ac`(문서 Commit. 코드는 `00091736`과 같음) |
| 주 검토 문서 | [B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md](B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md)(미커밋, 개정 1) |
| 계약 | `docs/contracts/b1/B1-T00_FINAL_CONTRACT.approved.md` §12·§15~§18·§20·§21·§23 |
| 코드 | `tools/StarPlayer.EvidenceVerifier/{Program,TestProfiles,Certification,TrustBootstrap,RequiredOriginals,ExecutionEnvironment}.cs`, `tools/StarPlayer.SimHarness/{Program,GitIntegrity}.cs`, `tools/StarPlayer.SimHarness/Evidence/ApprovalRecord.cs`, 두 도구의 `.csproj` |
| 빌드 증거(저장소 밖) | `E:\Madegame\g3x\nci`(N_S1)·`nci2long`(N_S2), `roles_candidates_00091736.json`(`1abb0741…`), `tools/roles_check_n.py`(`7360813f…`), `full_compare_n.json`(`1858c81b…`) |
| OWNER 부분 수용 | 초안 0절 A1~A11(정책 방향만. 동결 아님) |

**재현 명령(읽기 전용):**

```bash
git -C E:/Madegame/Star_player show 00091736d5386aec3f5c67cf159291976e7133a2:tools/StarPlayer.EvidenceVerifier/TestProfiles.cs
git -C E:/Madegame/Star_player grep -n "ProcessStartInfo(" 00091736d5386aec3f5c67cf159291976e7133a2 -- tools
sha256sum E:/Madegame/g3x/roles_candidates_00091736.json E:/Madegame/g3x/tools/roles_check_n.py
```

## 2. 검수 쟁점과 Finding 후보

### 쟁점 1. 109개 Artifact 후보의 완전성

- **사실:** 109개(SIMHARNESS 1·VERIFIER 1·TEST_ASSEMBLY 4·DEPENDENCY 91·RUNTIME_CONFIG 12)는 6개 소비자 폴더 `deps.json`의 runtime·native(win) 항목으로 만들었습니다. 로드 추적은 하지 않았습니다(초안 7절).
- **F-19 (과다 포함):** 검증기·Infra 폴더의 `runtimes/win-arm64`·`win-x86` `e_sqlite3.dll`, `Microsoft.VisualStudio.CodeCoverage.Shim.dll`은 win-x64 실행에서 로드되지 않을 수 있습니다.
- **질문:** `deps.json` 기준 생성 방법이 Artifact 후보 생성 방법으로 타당한지, 초안 7.4절의 5분류(기록·로드 / 기록·미로드 가능 / 미기록·로드 가능 / 빌드 출력 밖 / 작업 생성 파일)에 빠진 분류가 있는지 봐 주십시오.

### 쟁점 2. 누락 가능 Test Runner·Host 파일

- **F-04:** 시험 폴더의 `xunit.runner.visualstudio.testadapter.dll`·`xunit.runner.utility.netcoreapp10.dll`·`xunit.runner.reporters.netcoreapp10.dll`(폴더당 3개, 총 12), `testhost.exe`, 위성 리소스가 후보 밖입니다. 그런데 `dotnet test`는 시험 발견에 xunit 어댑터를 쓸 가능성이 큽니다. 실행 시 대조는 TEST_ASSEMBLY만 합니다(`Certification.cs` 119~121행). 시험 폴더의 DEPENDENCY·RUNTIME_CONFIG는 대조하지 않습니다.
- **F-05:** SDK 쪽 `vstest.console.dll`, `testhost.dll`, `Extensions\Microsoft.VisualStudio.TestPlatform.Extensions.Trx.TestLogger.dll`은 Build Provenance·Environment Profile 어디에도 없습니다. TRX Logger가 쓴 TRX가 검증기 판정 입력입니다(`TestProfiles.cs` 174행).
- **질문:** F-04·F-05가 결함인지, 계약 E4("Test Profile은 PASS/FAIL 판정용이며 비트 재현의 근거가 아님")의 범위 안에서 허용되는지 봐 주십시오.

### 쟁점 3. Verifier의 SimHarness DLL 사본

- **F-06:** 검증기는 Replay·Schema·JCS·Archive·Dominance 통계·내장 입력(`HarnessData.ResourceBytes`, INPUT_BUNDLE 기대 SHA)을 자기 폴더의 `StarPlayer.SimHarness.dll` 사본(DEPENDENCY)에서 실행합니다. 이 사본과 SIMHARNESS_BINARY가 같아야 한다는 규칙과 실행 중 대조가 없습니다. N_S1에서는 우연히 같은 바이트(`a085d613…`)입니다.
- **F-07:** 실행 중인 검증기 DLL(VERIFIER_BINARY)과 로드한 DLL의 SHA 대조가 없습니다(G4 남은 위험).
- **제안 설계:** 초안 9절 V-1(정적 동일성 규칙)·V-2(E2-3 뒤와 E2-7 직전의 로드 목록 결속, 핸들 유지, Fail-Closed 조건 8개)·V-3(ACL).
- **질문:** V-1~V-3이 "다른 DLL이 로드됐는데도 인증이 가능한 경로"를 닫는지, 남는 경로가 있는지, Fail-Closed 조건에 빠진 것이 있는지 봐 주십시오.

### 쟁점 4. PATH 기반 dotnet 실행 신뢰 경계

- **F-01:** `TestProfiles.Run`은 `new ProcessStartInfo("dotnet")`(`TestProfiles.cs` 156행)을 씁니다. 초안은 Windows `CreateProcess` 검색 순서(부모 이미지 디렉터리 → 현재 디렉터리 → 시스템 디렉터리 → PATH)가 적용된다고 판단했습니다([문서 근거], 미실측). 부모가 Apphost라면 1순위 위치는 검증기 `bin` 폴더입니다.
- **F-02:** 자식의 `WorkingDirectory`를 정하지 않아, `dotnet test`의 `global.json` 탐색과 SDK 선택이 검증기 현재 디렉터리에 따라 달라질 수 있습니다.
- **F-09:** `git`도 이름 검색으로 실행합니다(`GitIntegrity.cs` 169행). 검증기 `TestProfiles.Fixtures`의 blob SHA가 이 출력에 의존합니다.
- **F-18:** §18.2는 `runtime_files` 3개만 고정합니다. `dotnet.exe`·`hostfxr.dll`·`hostpolicy.dll`은 어떤 승인 문서에도 고정되지 않습니다.
- **F-08:** 금지 접두사(`DOTNET_`·`COMPlus_`) 밖에도 로더 영향 변수(`CORECLR_ENABLE_PROFILING`·`CORECLR_PROFILER_PATH*`, `VSTEST_*`)가 있습니다. Environment Profile은 환경 digest 값을 고정하지 않고 C0 = C1 = C2만 비교합니다.
- **질문:** F-01·F-02·F-08·F-09·F-18의 사실 판단(특히 .NET `Process.Start`의 Windows 실행 파일 해석)이 맞는지, OWNER 우선 후보(승인 절대경로 `dotnet.exe` + 승인 DLL)가 이를 충분히 닫는지 봐 주십시오.

### 쟁점 5. Profile Archive 역할과 기존 INPUT_BUNDLE 해석

- **F-11:** 생산 검증기 `ConsumedInputs.Expected`(R2-A, C10 PASS 범위)는 INPUT_BUNDLE ORIGINAL SHA 집합에 Environment Profile·PZL Test Profile 원문 SHA를 요구합니다(`RequiredOriginals.cs` 14~15행, `Certification.cs` 154행). 초안 이전 판은 이를 "TEST_ONLY 구현"으로만 적었으므로 개정 1에서 정정했습니다.
- **안:** 초안 10.1절 P-A(INPUT_BUNDLE 해석, 추천)·P-B·P-C·P-D(새 역할, 금지)
- **질문:** P-A가 §21 "소비 입력 파일별"의 타당한 해석인지, Registry Pin과 Archive 보존본의 역할 구분(Archive는 신뢰 출처가 아님)이 §16 순환 금지와 맞는지 봐 주십시오.

### 쟁점 6. TRUST_CONFIG 상대경로 Root

- **F-10:** §18.3은 `relative_path`의 루트를 정하지 않습니다. 코드는 TRUST_CONFIG의 SHA만 대조하고 경로를 해석하지 않습니다(`TrustBootstrap.cs` 59~62행). 다른 역할은 체크아웃 루트와 결합됩니다.
- **안:** 초안 10.2절 T-A(식별 이름, 추천)·T-B(앵커 디렉터리 기준 대조)·T-C(체크아웃 루트, T2와 충돌하여 거부)·T-D(역할별 Root 표)
- **질문:** T-A·T-D가 §18.3·§21 Schema를 바꾸지 않는지, T6 "사후 대조만"과 맞는지 봐 주십시오.

### 쟁점 7. 기존 계약과 B-4 후보 절차의 정합성

- **F-03:** `Certification.RunTestProfile`은 TEST_ASSEMBLY 바이트를 읽어 SHA를 확인한 뒤 같은 경로를 자식 `dotnet test`에 넘깁니다(`Certification.cs` 119~121행). 확인과 실행 사이에 파일이 바뀔 수 있습니다. §21 "Hash한 뒤 같은 경로를 다시 열어 다른 파일을 파싱하지 않습니다"가 실행 파일에도 적용되는지 해석이 필요합니다.
- **F-12:** TEST_ASSEMBLY `relative_path`는 체크아웃 루트(`buildRoot`)와 결합됩니다. 운영에서 설치 폴더를 따로 두면 RELPATH의 의미가 달라집니다.
- **질문:** 초안 4절 1~13단계와 6절 경로 표가 §18.3·§21·§23 B-4 Fail-Closed와 충돌하지 않는지, F-03·F-12를 봐 주십시오.

### 쟁점 8. 공식 실행 명령 및 Apphost 미확정

- **F-13:** Verifier CLI는 `environment`만 있습니다. 인증 명령과 T1 앵커 위치를 읽는 운영 설정은 NOT_IMPLEMENTED입니다. SimHarness `official*`는 NOT_READY로 거부하며, 저장소 루트를 현재 디렉터리에서 찾습니다(`Program.cs` 315~321행).
- **F-14:** Apphost는 DLL 이름만 담고 DLL 바이트를 결속하지 않는다고 판단했습니다([문서 근거]). exe를 닫힌 역할 중 어디에 둘지, `verifiers[].binary_sha256`이 무엇을 가리킬지 계약에 없습니다.
- **질문:** 실행 명령이 정해지기 전 Apphost 결정을 보류한 것이 맞는지 봐 주십시오. (2차 정정) 1차의 "방식 A 채택 시 apphost 14개를 Artifact 후보에서 빼는 안"은 철회했습니다. EXE 14개 중 6개는 VSTest `testhost.exe`이며 모두 사용 확인 전 PENDING입니다(초안 7.7절).

### 쟁점 9. 승인 SourceLink Commit의 원격 검증

- **F-15:** `git ls-remote` 결과, 원격 ref는 `main` → `7456f44` 하나이고 Tag는 없습니다. (2차 정정) `7bdde7d`·`00091736`·`96070c5`를 가리키는 광고된 ref는 확인되지 않았습니다. Commit 객체와 SourceLink Raw 콘텐츠의 조회 가능성은 미검증이고, 공식 원격 출처 검증은 완료되지 않았습니다. SourceLink URL은 PDB에 들어가고 PDB 식별자는 DLL에 들어가므로, 원격 URL을 바꾸면 Artifact SHA가 바뀝니다.
- **F-16:** 공개 원격에 Push하면 OWNER·Codex 원문 35개와 로컬 경로가 든 파일이 새로 공개됩니다. 승인 SHA 결속 때문에 이력 재작성으로 지울 수 없습니다(초안 11.3절).
- **질문:** "원격 Commit 확인 전에는 원격 출처 검증 완료로 표시하지 않음"이 B-4 Fail-Closed로 충분히 정의됐는지, R-1~R-4(초안 11.4절)에 빠진 선택지나 위험이 있는지 봐 주십시오.

### 쟁점 10. 운영 환경·권한 Gate와의 의존성

- **F-17:** Test Profile TRX는 자식이 쓰고 부모가 다시 읽습니다. 결과 디렉터리를 생산자가 쓸 수 있으면 판정 입력을 바꿀 수 있습니다.
- **F-20:** 9절 V-3·8.5절 ACL은 G5에 묶여 있어, G4의 Fail-Closed 설계가 G5 없이는 완결되지 않습니다(초안 14절 의존 순서).
- **질문:** 초안 8.5절의 쓰기 금지 위치 표에 빠진 위치가 있는지, 14절 Gate 의존 순서가 맞는지 봐 주십시오.

## 3. Finding 후보 목록 (Claude 잠정, 판정 아님)

| ID | 쟁점 | 요지 | 근거 종류 |
|---|---|---|---|
| F-01 | 4 | 자식 `dotnet`을 이름으로 검색(부모 이미지 디렉터리·현재 디렉터리 우선) | 코드 [사실] + 검색 순서 [문서 근거] |
| F-02 | 4 | 자식 작업 디렉터리 미지정 → `global.json`·SDK 선택 의존 | 코드 [사실] + [문서 근거] |
| F-03 | 7 | TEST_ASSEMBLY SHA 확인 후 경로 재사용(TOCTOU) | 코드 [사실] |
| F-04 | 1·2 | 시험 폴더 의존성·xunit 어댑터 실행 시 미대조, 후보 밖 | 코드·빌드 출력 [사실] |
| F-05 | 2 | SDK VSTest·TRX Logger가 승인 문서 밖 | 개발 PC [관찰] |
| F-06 | 3 | 검증기 폴더 SimHarness 사본 결속 없음 | 코드·deps.json [사실] |
| F-07 | 3·8 | 검증기 자기 SHA 대조 없음 | 코드 [사실] |
| F-08 | 4 | 금지 접두사 밖 로더 영향 변수 | [문서 근거] |
| F-09 | 4 | `git` 이름 검색 | 코드 [사실] |
| F-10 | 6 | TRUST_CONFIG `relative_path` 의미 미정 | 계약·코드 [사실] |
| F-11 | 5 | Profile 원문 INPUT_BUNDLE 요구(정정 사항) | 코드 [사실] |
| F-12 | 7 | `relative_path` 루트가 체크아웃 루트에 묶임 | 코드 [사실] |
| F-13 | 8 | 공식 실행 명령·앵커 설정 NOT_IMPLEMENTED | 코드 [사실] |
| F-14 | 8 | Apphost가 DLL 바이트를 결속하지 않음, 역할 미정 | [문서 근거] + 계약 |
| F-15 | 9 | 승인 Commit을 가리키는 광고된 ref 미확인(객체·Raw 조회 미검증, 2차 정정), URL이 Artifact SHA에 영향 | `ls-remote` [사실] |
| F-16 | 9 | 공개 Push 시 원문·경로 새로 공개, 재작성 불가 | 이력 검사 [사실] |
| F-17 | 10 | TRX 결과 디렉터리 쓰기 권한 | 코드 [사실] |
| F-18 | 4 | `dotnet.exe`·`hostfxr`·`hostpolicy` 미고정 | 계약 [사실] |
| F-19 | 1 | 비x64 native 등 과다 포함 | 빌드 출력 [사실] |
| F-20 | 10 | G4 Fail-Closed가 G5 ACL에 의존 | 설계 판단 |

## 4. 범위 밖

- 운영 Build Provenance 등록, G3 PASS, Build Freeze, G1~G9 실측
- §18.2 Environment Profile 값, N12 Golden
- 코드 수정안의 구현 검수(구현하지 않았음)
- 원격 Fetch·Push, 저장소 공개 범위의 최종 판단(OWNER 몫)
- Clause ID 등록
