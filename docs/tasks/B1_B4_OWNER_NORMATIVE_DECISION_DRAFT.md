# B-4 OWNER 규범 결정 자료 — 원격·실행 신뢰 모델·G4/G5 선행 설계 (DRAFT, 확정 아님)

- 작성: Claude Code, 2026-10-27. 근거: OWNER 지시 "B-4 REVIEW_READY 수용 및 OWNER 규범 결정 준비"(2026-10-27)
- **성격:**
  - OWNER 검토용 결정 자료 초안입니다. 이 문서의 어떤 내용도 승인·동결·등록·PASS가 아닙니다.
  - B-4 최종 승인, 공식 G3 PASS, Build Provenance 등록, 운영 실행 승인이 **아닙니다**. Codex REVIEW_READY는 B-4 초안의 문서 검수 단계 종료일 뿐입니다.
  - 기존 F-01~F-20의 미해결 운영·구현 조건은 그대로 유지합니다.
  - 승인 계약의 닫힌 Schema(§18.2·§18.3·§21)를 확장하지 않습니다. Schema 밖의 통제가 필요하면 "별도 OWNER 규범"으로 표시합니다.
  - **미커밋 문서입니다.** Stage·Commit·Push·Tag, 원격 생성·Fetch·Push, 코드 수정, 로드 추적, 운영 Gate 실행은 하지 않았습니다.
- **고정 기준(변경하지 않음):**

| 항목 | 값 |
|---|---|
| 역사적 B1 Tag | `b1-code-accepted-2026-10-26` → `7bdde7dd091f9a66957bc740c77d470c7732b3ad` |
| Scene Migration 검증 소스 | `00091736d5386aec3f5c67cf159291976e7133a2` |
| 현재 문서 HEAD | `96070c5fa1571b079df9c204597215db9da974ac` |
| 검수된 B-4 결정 초안 | [B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md](B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md) `62ae952e4582f4403be8d34ac7dcd32a4579ec2d70f6eac4bfe0914f0d4ebfe5` |
| 검수된 Codex 요청서 | [B1_B4_CODEX_READONLY_REVIEW_REQUEST.md](B1_B4_CODEX_READONLY_REVIEW_REQUEST.md) `49f6d73ce1331c0781ce94183be8d30ad0028be4ff12fa1c2785c015056e3327` |
| 광고된 원격 ref(2026-10-26 조회, 재조회 안 함) | `HEAD`·`refs/heads/main` → `7456f44fd3ecd687afce210eb5be972114f8479c`, Tag 0개 |

- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE** · Artifact 집합 = **CANDIDATE_INCOMPLETE**

**분류 표기:** [사실] 확인됨 · [관찰: 개발 PC] 운영 값 아님 · [문서 근거] 공개 동작 근거, 미실측 · [후보] OWNER 미결정 · [규범] 계약이 정하지 않아 OWNER 규범이 필요 · NOT_IMPLEMENTED · OWNER_VALUE_PENDING

## 0. Codex B-4 검수 원문 로컬 보존 기록

- **보존 위치(저장소 밖, Git Commit 하지 않음):** `E:\Madegame\g3x\codex-originals-b4\`(새로 만든 빈 디렉터리에 `x` 모드로 생성, 덮어쓰기 없음)
- **원본 로그:** `C:\Users\user\.codex\sessions\2026\10\04\rollout-2026-10-04T21-41-07-01a106ee-aab7-7a10-8f22-d304b84ba8ee.jsonl`
- **추출 방법:** 각 행을 JSON으로 읽어 `payload.content[0].text`(content 항목은 각각 1개)를 UTF-8 그대로 저장하고 다시 읽어 바이트 일치를 확인했습니다. 기존 [원문 색인](../contracts/b1/originals/INDEX.md)과 같은 방식입니다. 스크립트는 Claude가 `python -I`로 실행했습니다.
- **대화에서 복사한 문장은 원본으로 쓰지 않았습니다.** OWNER 지시문에 인용된 판정 요약은 원본이 아니며, 아래 파일만 세션 원본입니다.

| 파일 | 의미 | OWNER 지시 3항 필수 | 원본 행 | 로그 역할 | 기록 시각(UTC / KST) | 바이트 | 원문 SHA-256 | 원시 로그 행 SHA-256 | 비밀정보 | 상태 |
|---|---|---|---:|---|---|---:|---|---|---|---|
| `20261026T213708Z_request-b4-r01-r02-targeted-review.txt` | B4-R01·R02 표적 재검수 요청 | 예 | 13405 | user | 2026-10-26T21:37:08.204Z / 10-27 06:37:08 | 2,660 | `069fafaa7ad6ec15f40a7593d6b1f5261fe0b19e68fe6b55599f2c9532e96e07` | `b3a5583a0b9332efbb1ce91db3308f9da3245465a6011902b2b9d089b5c9b17d` | 0건 | NOT_INDEPENDENTLY_VERIFIED |
| `20261026T214006Z_verdict-b4-review-ready.txt` | 최종 판정 REVIEW_READY | 예 | 13473 | assistant | 2026-10-26T21:40:06.531Z / 10-27 06:40:06 | 3,608 | `9921cde4abef1df42e7469dd3a434a1d1743bd00f0b1031aad98ef2c6408e1a1` | `caa8f433be43818bebb8697628acfa133636b92abaddd174cd31116b9113aebd` | 0건 | NOT_INDEPENDENTLY_VERIFIED |
| `20261026T211858Z_request-b4-readonly-review.txt` | 1차 검수 요청 메시지(첨부 파일 표기) | 보조 | 13195 | user | 2026-10-26T21:18:58.929Z / 10-27 06:18:58 | 333 | `ad80723222e0a38e700425b45eeae408317d486e8bed712b60baf66e68d09025` | `72b481aacf39647399a0a181dc95e85cbe706c38f4693f6063c38dce02afeaba` | 0건 | NOT_INDEPENDENTLY_VERIFIED |
| `20261026T212819Z_verdict-b4-fix-required.txt` | 1차 판정 FIX_REQUIRED(F-01~F-20 개별 판정 포함) | 보조 | 13398 | assistant | 2026-10-26T21:28:19.916Z / 10-27 06:28:19 | 19,038 | `eba89c8ee4f82c0acab6c938e153197d2cf19a9f86818fe2ee2a8dbe3935ed8e` | `b85db6504108b8fb1ee123908c98550c33433138ce18240e5f43c3fa13f433f3` | 0건 | NOT_INDEPENDENTLY_VERIFIED |
| `20261026T211858Z_request-b4-readonly-review_attachment.txt` | 13195행이 가리키는 첨부 파일(1차 요청 본문) | 보조 | 해당 없음(파일) | — | 첨부 파일 수정 시각 10-27 06:18:54 KST | 9,021 | `5beff008c305237a3d941bfcfb08455a48aa08d823b7263bdb74b12cc374df1d` | 해당 없음 | 0건 | NOT_INDEPENDENTLY_VERIFIED |

- **목록 파일:** `manifest.json`(`723139d9a59c18918bb3f8c5235633288c2202f2c64de76d7a3c8fcf8501c2d4`, 로그 메시지 4개), `manifest-attachments.json`(`06dbcce3eb8df2c127fef93c10f5315e099a85e6573713e3a1ca7240f4ae1ab4`, 첨부 1개)
- **첨부 원본 경로:** `C:\Users\user\.codex\attachments\252185ae-4b53-4b61-9734-dac1c3bc0bfd\붙여넣은 텍스트.txt`. 첨부 파일은 로그 행이 아니라 별도 파일입니다. 수정 시각은 메시지보다 약 4초 앞서지만, 메시지 당시 바이트와 같다는 독립 증명은 없습니다.
- **비밀정보 검사:** API Key·Token·`key=value` 형식의 비밀값·개인 키·이메일 패턴. 다섯 파일 모두 0건입니다(단순 정규식이며 완전한 검사가 아님).
- **NOT_INDEPENDENTLY_VERIFIED:** 원본은 이 PC의 로컬 Codex 기록입니다. 서비스가 서명한 기록이 아니므로 Codex가 실제로 낸 응답과 같다는 것은 이 기록만으로 독립 증명되지 않습니다.
- **제외한 기록:** `C:\Users\user\.codex\sessions\2026\10\27\rollout-2026-10-27T06-18-37-01a17a14-….jsonl` 13행은 "agent history를 평가하는" 별도 요약 기록이며 B-4 검수 원문이 아닙니다.
- **Git 보존:** 이번에는 허가되지 않았습니다. `docs/contracts/b1/originals/codex/`에 C13~C16(및 첨부)로 넣을지는 N-14(11절)입니다.

**원문으로 확인된 정정 사항(검수된 B-4 초안은 수정하지 않음):** 1차 판정 F-16은 초안의 "원문 35개"를 "HEAD 기준 원문 txt 33개 + 색인 2개(INDEX.md·index.json)"로 읽어야 정확하다고 했습니다. 이 자료는 그 기준을 씁니다(1.2절).

## 1. 공식 Git 원격 비교와 추천

### 1.1 대안별 평가

| 평가 항목 | A. 별도 비공개 공식 원격 | B. 기존 공개 GitHub 원격 | C. 당분간 로컬 전용 보존 |
|---|---|---|---|
| Git 이력 보존 | 전체 이력을 그대로 Push하면 보존. 강제 Push·재작성 없음 | 같음 | 로컬 `.git` 하나뿐. 디스크 손상에 취약(백업은 별도) |
| 승인 Commit SHA 유지 | 유지(Push는 SHA를 바꾸지 않음) | 유지 | 유지 |
| SourceLink URL | 새 URL(A-1) 또는 같은 URL(A-2, 아래) | `https://raw.githubusercontent.com/junk1901/Star_player/<commit>/*` | 정식 원격 없음. 로컬 경로 원격이면 SourceLink 미생성 [사실], 미게시 URL을 설정하면 조회 불가능한 URL을 기록 |
| DLL/PDB SHA 영향 | A-1: URL이 PDB에 들어가고 PDB 식별자가 DLL에 들어가므로 StarPlayer DLL·PDB SHA가 진단 빌드(N_S1·N_S2)와 달라짐 [사실: 기전, 정확한 결과는 실측 필요]. A-2: 진단 빌드와 같은 URL | 진단 빌드와 같은 URL이므로 진단 SHA 결과를 그대로 기대 가능(공식 빌드로 재확인 필요) | 공식 SHA를 확정할 수 없음 |
| OWNER·Codex 원문 공개 범위 | 접근 권한자만 | **선택한 Commit과 조상 전체가 공개됨**(1.2절). 되돌릴 수 없음 | 공개 없음 |
| 필요한 Git 권한 | 저장소 생성(A-1) 또는 공개 범위 변경(A-2, 계정 설정 변경), Push 권한 1계정, 보호 규칙 설정, 검증자 읽기 권한 | Push 권한, 보호 규칙 설정 | 없음 |
| 공식 출처 검증 방법 | 인증된 읽기 전용 조회(1.3절). 자격 증명은 저장소·로그·증거 밖에 둠 | 익명 조회로 누구나 재현 가능(1.3절) | 원격 검증 불가 → A6·A7 취지상 공식 출처 검증 **미완료로 고정** |
| 감사·증거 보존 적합성 | 접근 통제·보호 규칙으로 적합. 단일 제공자 의존, 검증자 접근 관리 필요 | 독립 검증성은 가장 높음. 민감 원문 공개 위험 | 낮음. 외부 독립 확인 수단 없음 |
| 후속 빌드·운영 Gate 영향 | 원격 결정 후 G3 공식 빌드 진행 가능. A-1이면 진단 SHA를 기대값으로 쓸 수 없고 새 2폴더 비교 필요 | 원격 결정 후 G3 진행 가능 | G3 공식 출처 결속 불가. G4 설계·구현 준비는 가능 |

- **A의 두 형태:**
  - **A-1 새 비공개 저장소:** 기존 공개 저장소와 분리됩니다. URL이 바뀌므로 공식 빌드 SHA는 새로 측정해야 합니다.
  - **A-2 기존 저장소를 비공개로 전환:** URL이 유지됩니다. 이미 공개된 `7456f44`까지의 19개 Commit은 공개 이력으로 남고(복제·캐시 가능성), 32개 후속 Commit은 새로 공개되지 않습니다. 저장소 공개 범위 변경은 계정 설정 변경이므로 OWNER가 직접 해야 합니다.
- **해결책으로 쓰지 않는 것:** 과거 이력 재작성, 강제 Push, 원문 삭제 후 재Commit. 승인 Commit의 조상에 들어 있는 파일은 최신 트리에서 지워도 이력에 남고, 재작성은 승인 SHA 결속(A11)을 깨뜨립니다.

### 1.2 원격 공개 시 민감정보·이력 영향 ([사실], 로컬 Git 객체 검사)

검사: `git rev-list --objects`로 도달 가능한 모든 blob(과거 판 포함)을 `git cat-file --batch`로 읽어 검사했습니다(2026-10-27). 원격 ref 값은 2026-10-26 조회값입니다.

| 범위 | 원격에 아직 없는 Commit | 원격에 없는 blob | OWNER·Codex 원문 경로 | 로컬 경로 문자열이 든 파일 경로 | 비밀정보 패턴 | 이메일 문자열 |
|---|---:|---:|---|---:|---|---|
| `00091736`과 조상 전체 | 31 | 711 | 33(txt 31 = OWNER O01~O19, Codex P01·P02·C01~C10 + INDEX.md·index.json) | 177(전체), 원격 대비 신규 65 | 0 | 시험용 `t@example.invalid` 1건 |
| `96070c5`과 조상 전체 | 32 | 717 | 35(txt 33 = 위 + C11·C12 + 색인 2) | 원격 대비 신규 69 | 0 | 같음 |
| 이미 공개된 `7456f44`과 조상 | — | — | 0 | 114 | 0 | 0 |

- **새로 공개되는 내용의 성격:** OWNER 지시 원문(결정 1~3, 코드 수용·Git 기준본 결정 포함), Codex 요청·판정 원문, 승인 계약·운영 Gate 계획, 원시 시험 로그·보고서의 로컬 경로(`E:\Madegame…`, `C:\Users\user…`, Windows 사용자 이름 `user` 포함)
- **Commit 메타데이터:** 작성자 이름·메일 주소가 모든 Commit에 있습니다. 이미 공개된 19개 Commit에도 같은 값이 있어 새 노출은 아닙니다.
- **한계:** 비밀정보 검사는 단순 정규식입니다. 운영 계정 SID·Pipe 이름 같은 운영 값은 아직 정해지지 않아 이력에 없습니다(정해진 뒤 문서에 넣으면 같은 공개 문제가 생김).

### 1.3 SourceLink 출처 검증 절차 (후보, 미실행)

1. **원격에서 깨끗한 clone:** 승인 원격 URL에서 새 빈 디렉터리로 `git clone --no-hardlinks`, `git checkout --detach <source_git_sha>`. 로컬 복사본에서 clone하지 않습니다.
2. **광고된 ref 확인:** `git ls-remote <url>`에서 보호된 ref가 승인 Commit을 가리키거나 조상으로 포함하는지 확인합니다. 광고된 ref가 없다는 것만으로 객체 부재를 단정하지 않습니다(B4-R02).
3. **객체 확인:** 빈 저장소에 `git fetch <url> <source_git_sha>`로 해당 Commit 객체를 받아 SHA가 같은지 확인합니다.
4. **PDB 확인:** 모든 StarPlayer PDB의 SourceLink JSON이 `/_/*` → `<승인 raw URL 기준>/<source_git_sha>/*`인지 확인합니다.
5. **Raw 콘텐츠 확인:** PDB에 기록된 문서 경로마다 raw URL을 조회하고, 그 바이트를 `git cat-file blob <sha>:<path>`와 비교합니다.
   - 주의: raw URL은 **blob 바이트**를 돌려주고, PDB 문서 체크섬은 **체크아웃 바이트**(`.gitattributes` 개행 변환 후)의 Hash입니다. raw는 blob과, PDB 체크섬은 체크아웃 파일과 비교합니다.
6. **기록:** 명령·출력·시각·조회 URL 목록을 진단 증거로 보존합니다.
7. **Fail-Closed:** 어느 단계든 불일치·조회 실패·인증 실패면 공식 원격 출처 검증 미완료입니다.
8. **비공개(A)일 때:** 읽기 전용 자격 증명을 쓰며, 저장소·세이브·로그·증거 파일에 넣지 않습니다.

### 1.4 추천 [후보]

- **추천: A(비공개 공식 원격).** 공개되면 되돌릴 수 없는 원문·경로 노출을 피하면서 원격 출처 검증을 할 수 있습니다.
  - A-2(같은 URL)는 진단 빌드의 SHA 재현 증거를 그대로 이어 쓸 수 있다는 장점이 있습니다. 대신 계정 설정 변경과, 이미 공개된 19개 Commit의 공개 상태가 남는다는 점을 OWNER가 받아들여야 합니다.
  - A-2가 어려우면 A-1이고, 이 경우 공식 빌드 SHA를 새로 측정합니다.
- **B**는 독립 검증성이 가장 높지만 1.2절의 공개 범위를 OWNER가 명시적으로 수용할 때만 고려합니다.
- **C**는 결정 보류이며 G3 공식 출처 결속이 불가능합니다. G4 설계·구현 준비와는 병행할 수 있습니다.
- 원격 선택은 OWNER가 결정합니다. Push·Fetch·원격 생성은 하지 않았습니다.

## 2. 공식 실행 명령 후보

- **OWNER 우선 후보 유지:** 승인 절대경로 `dotnet.exe` + 승인 DLL. 아래 `<…>`는 OWNER_VALUE_PENDING입니다.

| 경로 | 명령 후보 | 작업 디렉터리 | 현재 상태 |
|---|---|---|---|
| SimHarness 공식 실행 | `"<DOTNET>\dotnet.exe" "<R-BUILD>\tools\StarPlayer.SimHarness\bin\Release\net8.0\StarPlayer.SimHarness.dll" official-cal\|official-int\|official-dom --run-id <ID> [--out <출력>]` | `<R-BUILD>`(저장소 루트를 현재 디렉터리에서 찾기 때문) | 명령 있음, NOT_READY로 거부 |
| Verifier 환경 사전 확인 | `"<DOTNET>\dotnet.exe" "<R-BUILD>\tools\StarPlayer.EvidenceVerifier\bin\Release\net8.0\StarPlayer.EvidenceVerifier.dll" environment` | 고정 디렉터리 | 명령 있음 |
| Verifier 공식 인증 | `"<DOTNET>\dotnet.exe" "<…>\StarPlayer.EvidenceVerifier.dll" certify --family <fc3-cal\|fc3-int\|fc3-dom> --approval-record-id <ID> --approval-record-sha256 <H> --authorization-id <ID> --document <RELPATH>` — 앵커 경로·Registry 연결·Archive Root·Build Root·작업 Root는 CLI가 아니라 보호된 설치 설정에서 읽음(§16 T1) | 고정 디렉터리 | **NOT_IMPLEMENTED**(F-13). 인자는 `Certification.Open`·`VerifyFamily`·`RunTestProfile`의 입력에서 도출한 형태 후보 |
| Test Profile 자식 | `"<부모와 같은 DOTNET>\dotnet.exe" test "<R-BUILD>\tests\<P>.Tests\bin\Release\net8.0\<P>.Tests.dll" --filter … --logger "trx;LogFileName=…" --results-directory "<R-WORK>\<세션>"` | 고정(4.2절 T-2) | 인자 형태는 현재와 같음. 실행 파일·작업 디렉터리 고정은 NOT_IMPLEMENTED(F-01·F-02) |
| `git` 자식 | `"<GIT>\git.exe" -C "<R-BUILD>" …` | `<R-BUILD>` | NOT_IMPLEMENTED(F-09) |

- **Apphost:** 공식 실행 명령이 확정되기 전에는 Apphost 포함·제외를 정하지 않습니다(B4-R01). `testhost.exe`는 Apphost와 별개로 PENDING입니다.

## 3. 실행 체인 Pin 및 보호 Root 설계

### 3.1 보호 Root (후보, 모두 OWNER_VALUE_PENDING)

| Root | 내용 | 쓰기 허용 주체(후보) | 현재 |
|---|---|---|---|
| R-DOTNET | `dotnet.exe`, `host\fxr`, `shared\Microsoft.NETCore.App`, `sdk\8.0.425` | TrustedInstaller·SYSTEM·OWNER 설치 계정 | [관찰: 개발 PC] `C:\Program Files\dotnet`, Users 읽기·실행만 |
| R-BUILD | 승인 Commit의 깨끗한 체크아웃과 빌드 출력. Build Provenance `relative_path`의 루트 | OWNER 빌드·배포 계정만 | 정해지지 않음 |
| R-GIT | 승인 `git.exe` 설치 | TrustedInstaller·SYSTEM·OWNER 설치 계정 | [관찰: 개발 PC] `C:\Program Files\Git` |
| R-TRUST | 신뢰 앵커와 보호 설치 설정 | TCB 전용(NSC-4 B) | 코드로 검사(앵커만) |
| R-ARCHIVE | 증거 Archive | 계약대로 생산자 쓰기, 검증기 읽기 | OWNER_VALUE_PENDING |
| R-WORK | 검증기 세션 작업 디렉터리(TRX·임시 파일) | VERIFIER_SERVICE만 | 현재 호출자가 지정 |

- TRUST_CONFIG의 `relative_path`는 검수된 초안 10.2절의 T-D(역할별 Root 표) + T-A(식별 이름, 해석하지 않음)를 후보로 유지합니다. 제출 Archive 경로를 Trust Root로 승격하지 않습니다(Codex 1차 판정 6절).

### 3.2 구성요소별 통제 구분

표기: **가능** · **불가** · **일부** · **필요** · **해석**(계약 해석으로 가능 여부 결정)

| 구성요소 | Build Provenance 포함 | 기존 Environment Profile 통제 | 외부 보호 Pin 필요 | 운영 ACL 보호 | 별도 OWNER 규범 |
|---|---|---|---|---|---|
| `dotnet.exe`(muxer) | 불가(빌드 출력 아님) | 불가 | 필요 | 필요(R-DOTNET) | 필요(Pin 위치, N-5) |
| `hostfxr.dll` | 불가 | 불가 | 필요 | 필요 | 필요 |
| `hostpolicy.dll` | 불가 | 불가 | 필요 | 필요 | 필요 |
| .NET Runtime(`shared\Microsoft.NETCore.App\8.0.x`) | 불가 | 일부: `System.Private.CoreLib.dll`·`clrjit.dll`·`coreclr.dll` SHA, 버전 문자열, RID | 나머지 런타임 파일은 필요 여부 결정 | 필요 | 필요(3개 밖의 파일 범위) |
| SDK VSTest(`vstest.console.dll`, SDK `testhost.dll`, `Extensions\…Trx.TestLogger.dll` 등) | 불가 | 불가(SDK 버전 문자열 `8.0.425`만 §18.3에 있음) | 필요 | 필요 | 필요(F-05, E4 범위 해석) |
| `testhost.exe`(시험 출력 폴더) | 해석(빌드 출력이므로 DEPENDENCY 후보. 실제 사용 확인 전 PENDING) | 불가 | 포함하면 Provenance로 충분 | 필요(R-BUILD) | 필요(N-11 Artifact 범위) |
| xUnit Runner/Adapter(시험 출력 폴더) | 해석(DEPENDENCY 후보, 현재 109개 밖, PENDING) | 불가 | 같음 | 필요 | 같음 |
| Verifier 바이너리(`StarPlayer.EvidenceVerifier.dll`) | 가능(VERIFIER_BINARY) | 불가 | Provenance + 승인 기록 `verifiers[].binary_sha256` | 필요 | 실행 중 자기 대조 방식(F-07, T-3) |
| SimHarness 바이너리와 Verifier 사본 | 가능(SIMHARNESS_BINARY + DEPENDENCY) | 불가 | Provenance | 필요 | 동일 바이트 규칙(V-1)·실제 로드 결속(V-2)(F-06, T-4) |
| 실제 로드 Managed/Native DLL | 빌드 출력분은 가능(로드 추적 후). 공유 런타임·OS 분은 불가 | 일부(위 3개 + `ucrtbase.dll`) | 공유 런타임·OS 분은 필요 여부 결정 | 필요 | 로드 목록 기록 위치(N-8) |
| 시험 출력 TRX | 불가(실행 중 생성) | 불가 | 불가(매번 다름) | 필요(R-WORK) | 판정 입력 보호 방식(F-17, T-7) |
| 승인 Git 실행 파일 | 불가 | 불가 | 필요 | 필요(R-GIT) | 필요(F-09) |

### 3.3 외부 보호 Pin의 위치 후보 [규범]

| 안 | 위치 | 닫힌 Schema 변경 | 장점 | 한계 |
|---|---|---|---|---|
| P1 | B-4 조항 본문(승인 조항 문서)에 승인 SHA 목록 | 없음(`build_procedure_clause_id`는 문자열) | 승인 문서 체계 안 | 기계별 값이 조항에 들어감. 조항 버전이 자주 바뀜 |
| P2 | R-TRUST의 보호 설치 설정 파일(앵커 옆, 별도 파일) | 없음(앵커 R03-1 형식은 그대로) | TCB 보호, 기계별 값에 적합 | 계약에 없는 새 신뢰 입력 → OWNER 규범 필요. TRUST_CONFIG처럼 Provenance와 사후 대조할지 결정 필요 |
| P3 | 실행 시 실측값을 `DIAGNOSTICS`(VERIFIER_DERIVED)로 기록만 | 없음 | 구현 단순 | 차단이 아니라 기록. 단독으로는 Fail-Closed가 아님 |

- **추천:** P2로 차단하고 P3로 기록합니다. Codex 1차 판정 5절대로 절대경로 하나만으로는 hostfxr·SDK·VSTest·시험 폴더·시작 전 모듈·TRX 경로까지 보장되지 않으므로, Pin·ACL·시작 환경 통제를 함께 둡니다.

## 4. G4 구현 TASK 분할 (후보, 구현하지 않음)

- 각 TASK는 별도 OWNER 허가 후 하나씩 구현합니다. 모든 TASK의 공통 조건:
  - 기존 B1 수용 코드의 Hash·Replay·Golden 불변
  - 닫힌 Schema 변경 없음
  - TEST_ONLY 시험으로 Red → Green
  - 실패는 기존 원인 코드 체계로 Fail-Closed

| TASK | 해결하는 Finding | 실제 소스 변경 대상 | 기존 계약과의 관계 | 필요한 OWNER 결정 | 테스트 방법 | Fail-Closed 조건 | G5 실측 의존성 |
|---|---|---|---|---|---|---|---|
| **G4-T1** 공식 인증 CLI·Trust Anchor 진입 설정 | F-13, (F-10) | `EvidenceVerifier/Program.cs`, 새 설치 설정 Reader, `Certification.Open` 호출 연결 | §16 T1(앵커는 OWNER 관리 설정에서, CLI 증거 인자 금지), §18.4 E1·E2 | 명령 형태(2절), 설치 설정 위치·형식(N-4·N-5) | 설정 부재·저장소 안 설정·Reparse·Elevated·CLI 앵커 인자 거부 시험 | 설정 없음·신뢰 불가 위치·형식 오류 → 시작 전 거부 | R-TRUST ACL, 서비스 계정 |
| **G4-T2** 자식 프로세스 절대경로·작업 디렉터리 고정 | F-01, F-02, F-09 | `TestProfiles.Run`, `GitIntegrity.Run`, SimHarness `Program.Git` | E4(금지 접두사 제거) 유지 | 승인 경로 출처(N-5) | `ProcessStartInfo.FileName` 절대경로·`WorkingDirectory` 고정 단위 시험, 작업 디렉터리에 둔 가짜 `dotnet.exe`가 실행되지 않는 통합 시험 | 승인 경로 없음·SHA 불일치·작업 디렉터리 신뢰 불가 → Test Profile 시작 전 거부 | R-DOTNET·R-GIT·R-WORK ACL |
| **G4-T3** Verifier 실행 이미지와 승인 SHA 결속 | F-07, F-18 일부, F-14 일부 | `Certification`(영수증 verifier 값을 첫 항목이 아니라 실제 실행 DLL과 일치하는 항목으로), 새 자기 대조 | §13 `verifiers[]`, §18.3 결속 사슬, 영수증 verifier = 승인 값 | 자기 대조 범위(DLL만 / `dotnet.exe`까지)(N-5) | 바이트를 바꾼 TEST_ONLY 사본에서 거부, 일치 항목 0개·2개 거부 | 실행 DLL SHA ∉ `verifiers[].binary_sha256` 또는 ∉ VERIFIER_BINARY → 인증 거부 | R-BUILD ACL |
| **G4-T4** 실제 로드 SimHarness DLL 결속 | F-06 | T5 검사(V-1 정적 규칙), 새 로드 목록 수집(V-2) | §18.3 역할(변경 없음), 로드 목록은 DIAGNOSTICS 후보 | V-1 채택, 실패 원인 코드, 로드 목록 보존 위치(N-8) | 사본 SHA가 다른 Provenance 거부, 다른 경로·바이트에서 로드된 어셈블리 거부 | 검수된 초안 9절 조건 1~8 | 검증기 `bin` ACL(V-3) |
| **G4-T5** 실행까지 파일·경로 교체 방지 | F-03, F-04(실행 시 대조), F-12 | `Certification.RunTestProfile`, `TestProfiles.CheckAssembly`·`Run` | §21 재오픈 원칙은 위반 단정이 아니라 G4 수용 조건으로 명시(Codex 1차 6절), §12.3 | 방식(핸들 유지 / 검증기 전용 복사본), 대상 파일 집합(로드 추적 후)(N-9) | 확인과 실행 사이 교체(시험용 Seam), 의존성 바이트 불일치 | 대상 파일 중 하나라도 SHA 불일치·핸들 확보 실패 → Test Profile 불일치 | R-BUILD·R-WORK ACL |
| **G4-T6** VSTest 및 Test Host 신뢰 검증 | F-04, F-05 | `TestProfiles.Run`, 외부 Pin 대조 | E4(Test Profile은 PASS/FAIL 판정용), §18.2 닫힘 | 3.2절 SDK VSTest·`testhost.exe`·xUnit 범위(N-5·N-11) | 변조된 SDK 사본을 가진 임시 설치에서 거부, Pin 대조 단위 시험 | Pin 대상 불일치·추가 호스트 프로세스 경로 불일치 → Test Profile 불일치 | R-DOTNET ACL, 로드 추적 결과 |
| **G4-T7** TRX 출력 디렉터리 보호 | F-17 | `Certification.RunTestProfile`의 작업 디렉터리, `TestProfiles.Run`의 TRX 읽기 | §21 Archive 읽기의 PinnedDirectory 방식과 같은 원칙 | R-WORK 위치·ACL | 미리 있는 파일·Reparse Point·다른 주체 소유 디렉터리 거부, TRX를 핸들로 한 번만 읽기 | 디렉터리 신뢰 불가·TRX 중복·핸들 실패 → Test Profile 불일치 | R-WORK ACL |
| **G4-T8** 인증 프로세스 시작 환경 통제 | F-08, F-18 일부 | `ExecutionEnvironment`(E2-1 접두사 규칙은 그대로), 별도 시작 정책 검사 또는 서비스 실행기 | E2-1 닫힌 규칙은 소급 변경하지 않음. 추가 통제는 새 규범 | 추가 금지·필수 조건(`CORECLR_ENABLE_PROFILING`·`CORECLR_PROFILER*`·Notification Profiler 설정 등) 채택 여부(N-7) | 환경 사전을 입력으로 받는 순수 함수 시험 | 승인 시작 정책 위반 → 증거를 읽기 전에 종료 | 서비스 계정 환경 블록 |

- **권장 순서:** T1 → T2 → T3 → T4 → T7 → T5 → T6 → T8
  - T5·T6은 대상 파일 집합이 로드 추적 결과에 의존합니다.
  - T8은 규범 결정 N-7이 먼저입니다.
- **G5 관계:** 모든 TASK는 G5 없이 설계·구현·TEST_ONLY 시험까지는 할 수 있습니다. 최종 운영 수용에는 G5 보호 조건이 필요합니다(F-20).

## 5. G5 ACL 실측 의존성

| 대상 | 실측할 것 | 기대(후보) | 의존 TASK·Finding |
|---|---|---|---|
| R-DOTNET | 소유자, DACL, 상속, Reparse Point, 생산자·VERIFIER_SERVICE의 쓰기 시도 | 쓰기·삭제·ACL 변경 거부 | T2·T6, F-01·F-05·F-18 |
| R-BUILD(검증기 `bin`, 시험 출력 폴더) | 같음 | OWNER 배포 계정 외 쓰기 거부 | T3·T4·T5, F-03·F-04·F-06·F-07 |
| R-GIT | 같음 | 쓰기 거부 | T2, F-09 |
| R-TRUST | NSC-4 B 판정(코드 기존 검사) + 보호 설치 설정 파일 | TCB 전용 | T1, F-13 |
| R-WORK | VERIFIER_SERVICE 전용 여부 | 생산자 읽기·쓰기 거부 | T7, F-17 |
| R-ARCHIVE | §17·NSC-4 계정별 권한 | 계약대로 | 기존 G5 |
| 서비스 계정 | SID, 환경 블록, PATH, UI 문화권, 관리자 권한 여부 | Elevated 아님, 승인 환경 블록 | T8, F-08, 로드 추적 |

- 실측은 운영 기계·계정 확정 후입니다. 이번 단계에서는 하지 않았습니다.

## 6. 동적 로드 추적 계획 (진단, 운영 Gate와 분리, 실행하지 않음)

### 6.1 원칙

- 진단 결과는 Artifact 집합 확정이나 G3·G4 PASS가 아닙니다. 109개는 계속 CANDIDATE_INCOMPLETE입니다.
- 파일 존재와 실제 로드를 구분합니다. 관찰 상태는 다음만 씁니다.

| 상태 | 뜻 |
|---|---|
| PRESENT | 디스크에 있음 |
| OPENED | 실행 중 파일 열기 관찰 |
| IMAGE_LOADED | 실행 이미지로 매핑 관찰 |
| PROCESS_STARTED | 그 파일로 프로세스 시작 관찰 |
| NOT_OBSERVED_IN_RUN | 이번 회차에서 관찰되지 않음(**미사용 판정 아님**) |

- 한 번의 추적으로 전체 경로의 완전성을 단정하지 않습니다. UNUSED·LOAD_CONFIRMED 같은 확정 분류는 OWNER가 정한 반복·조건 기준을 만족한 뒤에만 OWNER 결정으로 씁니다.

### 6.2 대상

| 대상 | 실행 방법(진단용) | 관찰할 것 |
|---|---|---|
| 승인 Test Profile 4개(Domain·Simulation·Infrastructure·SimHarness Tests) | `TestProfiles.Run`과 같은 인자, §12.2 필수 Case 필터 | 시험 폴더 파일 전체의 상태, 프로세스 트리 |
| VSTest Host | 위 실행의 `vstest.console` | SDK 쪽 파일 상태 |
| xUnit Runner/Adapter | 위 실행 | runner 3종·`xunit.*` 상태 |
| `testhost.exe` | 위 실행 | PROCESS_STARTED 여부, 또는 `dotnet exec testhost.dll` 방식 |
| 문화권 Resource DLL | UI 문화권 2가지(예: `ko-KR`, `en-US`)로 반복. Pass·Fail Case가 있는 경로 포함 | `<culture>\*.resources.dll` 상태 |
| Managed/Native 의존성 | 위 전부 + SimHarness `dry`·`official-*`(NOT_READY 거부까지) | SQLite native(아키텍처별), 공유 런타임, System32 |
| Verifier의 SimHarness DLL 사본 | Verifier `environment` 명령 | 아래 한계 참조 |

- **Verifier 사본의 한계:**
  - 현재 공식 CLI에는 SimHarness 코드를 쓰는 인증 경로가 없습니다(F-13). `environment` 경로에서는 사본이 로드되지 않을 수 있습니다.
  - TEST_ONLY 인증 시험은 시험 폴더의 다른 사본을 쓰므로 대표성이 없습니다.
  - 따라서 이 사본의 실제 로드 관찰은 G4-T1 구현 뒤에야 의미가 있습니다.

### 6.3 절차와 기록

1. 검수된 초안 4절 1~11단계로 진단 폴더에 깨끗한 빌드
2. 도구: Process Monitor(외부 다운로드 → 파일명·출처·크기 별도 승인) 또는 Windows 기본 `wpr.exe`. 관리자 권한은 도구에만 쓰고 대상은 일반 권한으로 실행
3. `DOTNET_`·`COMPlus_` 변수 금지. `COREHOST_TRACE`는 별도 보조 회차에만 사용
4. 각 대상을 최소 2회 반복하고, 문화권·Case 조건을 바꾼 회차를 포함
5. 원시 추적 파일, 명령, 환경 digest, 파일별 상태·SHA를 저장소 밖 진단 디렉터리에 보존
6. 결과표를 109개 후보·7.7절 EXE 표와 대조해 차이만 보고. 확정 분류는 하지 않음
7. 운영 최종 실측(검수된 초안 7.6절)은 G5 이후 별도

## 7. F-01~F-20 후속 처리표

- "Codex 판정"은 0절에 보존한 1차 판정 원문(`20261026T212819Z_verdict-b4-fix-required.txt`) 4절의 표를 옮긴 것입니다. 원문이 우선합니다.
- Codex의 MAJOR는 공식 운영 전 차단 중요도이며 B1 코드 수용 판정을 취소하지 않습니다.

| ID | Codex 판정 | 후속 처리 | 결정·실측 |
|---|---|---|---|
| F-01 | CONFIRMED / MAJOR | G4-T2 | N-5, G5 R-DOTNET |
| F-02 | CONFIRMED / MAJOR | G4-T2 | N-5 |
| F-03 | CONFIRMED / MAJOR | G4-T5 | N-9, G5 R-BUILD |
| F-04 | CONFIRMED / MAJOR | G4-T5·T6, 로드 추적 | N-10·N-11 |
| F-05 | CONFIRMED / MAJOR | G4-T6 | N-5, G5 R-DOTNET |
| F-06 | CONFIRMED / MAJOR | G4-T4 | N-8, G5 검증기 `bin` |
| F-07 | CONFIRMED / MAJOR | G4-T3 | N-5 |
| F-08 | CONFIRMED / MAJOR | G4-T8 | N-7 |
| F-09 | CONFIRMED / MAJOR | G4-T2 | N-5, G5 R-GIT |
| F-10 | CONFIRMED / 운영 의존성 | T-D + T-A 해석 결정(코드 변경 없음) | N-6 |
| F-11 | NOT_CONFIRMED — 새 결함 아님 / NOTE | 공식 파일명·경로 결정만 유지 | 검수된 초안 D-3 |
| F-12 | CONFIRMED / 운영 의존성 | R-BUILD 정의. 별도 설치 배치를 채택하면 G4-T5에서 대응 | N-6 |
| F-13 | CONFIRMED / 운영 전 차단 | G4-T1 | N-4 |
| F-14 | CONFIRMED / 운영 의존성 | 공식 실행 명령 결정 후 Apphost 결정(B4-R01) | N-4 |
| F-15 | CONFIRMED / 운영 의존성 | 원격 결정 → 1.3절 검증 절차 | N-1·N-3 |
| F-16 | CONFIRMED / NOTE·OWNER 결정 | 1.2절 공개 범위 판단(원문 txt 33 + 색인 2) | N-1·N-2 |
| F-17 | CONFIRMED / MAJOR | G4-T7 | G5 R-WORK |
| F-18 | CONFIRMED / MAJOR | G4-T3·T6·T8, 3.3절 Pin 위치 | N-5 |
| F-19 | NOT_CONFIRMED — 결함으로 유지하지 않음 / NOTE | 로드 추적 후 Artifact 범위 결정 | N-10·N-11 |
| F-20 | CONFIRMED / 운영 의존성 | G5 실측(5절) | G5 |

## 8. 계속 미확정인 항목 (변경 없음)

- 공식 SourceLink 원격, Artifact 완전 집합, 실제 Apphost·Test Host 사용 여부, TRUST_CONFIG Root, 공식 실행 명령, 인증 실행 체인 Pin, G5 계정·ACL, 공식 출력 Root, `build_procedure_clause_id` 등록, 운영 Environment Profile, N12 Golden, Build Provenance 등록
- A1~A11 부분 수용 범위는 바꾸지 않았습니다.

## 9. 이번 작업에서 한 것과 하지 않은 것

**한 것:**

- Codex 로그 검색과 원문 5개 로컬 보존(0절)
- 로컬 Git 객체의 공개 범위 검사(1.2절)
- 코드 읽기: `Certification.Open`·영수증 verifier 항목(184행)

**하지 않은 것:**

- 원격 조회(재조회 포함)·Fetch·Push·원격 생성
- 빌드·시험·로드 추적·SimHarness·Verifier 실행
- 코드·Scene·시험·Golden·계약·OWNER 원문·루트 `TASK.md`·Tag 수정
- 검수된 B-4 초안·요청서 수정
- Stage·Commit

## 10. 검수된 문서와의 관계

- 검수된 B-4 초안의 D-1~D-18은 그대로 둡니다. 아래 N-x는 이번 지시 범위의 결정을 다시 묶은 것이며, 대응하는 D 번호를 함께 적었습니다.

## 11. OWNER가 승인해야 할 다음 결정 목록

| ID | 결정 | 관련 | 성격 |
|---|---|---|---|
| N-1 | 공식 원격: A-1 / A-2 / B / C | 1절, D-1 | 정책 |
| N-2 | (B·A-2일 때) 공개되는 이력 범위의 명시적 수용 | 1.2절, F-16 | 정책 |
| N-3 | SourceLink 출처 검증 절차(1.3절) 채택, 비공개일 때 자격 증명 관리 방식 | 1.3절, F-15 | 정책 |
| N-4 | 공식 실행 명령 형태(2절)와 G4-T1 구현 허가. 그 뒤 Apphost 결정 | 2절, D-8, F-13·F-14 | 정책 + 코드 허가 |
| N-5 | 실행 체인 외부 Pin 위치(P1/P2/P3)와 대상 범위(3.2절) | 3절, D-6, F-01·F-05·F-07·F-09·F-18 | **규범** |
| N-6 | 보호 Root 표(3.1절)와 TRUST_CONFIG T-D + T-A 의미 고정 | 3.1절, D-13, F-10·F-12 | **규범** |
| N-7 | 인증 시작 환경의 추가 통제(E2-1 밖, 소급 적용 없음) | G4-T8, D-7, F-08 | **규범** |
| N-8 | V-1·V-2 채택, 실패 원인 코드, 로드 목록 보존 위치 | G4-T4, D-9, F-06 | **규범** + 코드 허가 |
| N-9 | §21 재오픈 원칙을 G4 수용 조건으로 명시하는 해석, G4-T5 방식 | G4-T5, D-10, F-03 | **규범** |
| N-10 | 동적 로드 추적(6절) 실행 승인과 도구 선택(외부 다운로드면 별도 승인) | 6절, D-11 | 실행 허가 |
| N-11 | 로드 추적 뒤 Artifact 범위 규칙(`testhost.exe`·xUnit·리소스·비x64 native) | 3.2절, D-12, F-04·F-19 | 정책 |
| N-12 | G4-T1~T8 분할·순서 승인. 구현은 TASK마다 별도 허가 | 4절 | 계획 승인 |
| N-13 | G5 운영 기계·계정·Root 값 확정 시점 | 5절, D-4 | 운영 |
| N-14 | 0절 원문 5개를 `docs/contracts/b1/originals/codex/`에 C13~C16(+첨부)로 넣고 색인을 갱신할지. 이번 지시는 Git Commit을 허가하지 않음 | 0절, 검수된 초안 D-17 | 기록 정책 |
