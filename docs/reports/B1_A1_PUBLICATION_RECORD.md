# B1 A-1 최초 GitHub 게시 기록 (OWNER 승인 A)

- 작성: Claude Code, 2026-10-29. 근거: OWNER 지시 "OWNER Approval A 승인 및 최초 GitHub Push 실행"(2026-10-29)
- **승인 범위:** Remote `origin` = `https://github.com/junk1901/Star_player.git`(Public 유지), `refs/heads/main` 하나, 원격 기준 `7456f44fd3ecd687afce210eb5be972114f8479c` → 게시 대상 `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f`, Fast-forward only, Tag 게시 없음, Force 금지. 신규 50개 Commit 공개와 규칙 PDF 2개의 공개 재배포 권한은 OWNER가 확인했습니다.
- **승인하지 않은 것(이번 작업에서 하지 않음):** 운영 ACL 적용, 역할 계정·SID 확정, R-ARCHIVE 권한, Trust Anchor 설치, 운영 Pin 등록, 공식 G1~G9, Build Freeze. Approval A는 G2/G3나 Build Provenance의 공식 PASS가 아닙니다.
- 명령·출력 원본: `E:\Madegame\g3x\phase3ops\push_approvalA_log.txt`(저장소 밖). 인증 정보는 기록되지 않았습니다(자격 증명은 Git Credential Manager가 처리, 로그에 토큰 형태 문자열 0건).
- 상태: Official Execution = **NOT_READY** · Final Execution Freeze = **PENDING** · Phase 3 = **INCOMPLETE**

## 1. 결과: **Push 성공**

| 항목 | 값 |
|---|---|
| 게시 전 원격 `refs/heads/main` | `7456f44fd3ecd687afce210eb5be972114f8479c` |
| 게시 후 원격 `refs/heads/main` | **`c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f`** |
| 실제 게시된 Ref | `refs/heads/main` 하나(`7456f44..c77ed1a`, Fast-forward) |
| 원격 Ref 전체(게시 후, `ls-remote --refs`) | `refs/heads/main` 하나. Tag 0개, 다른 Branch 0개 |
| 원격 `HEAD` | `ref: refs/heads/main` → `c77ed1a…` |
| 새로 공개된 Commit | 50개(Merge 0개) |
| 공식 원격에 게시된 기준 소스 | `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f`(과 그 조상 `1751fe0`·`a980ea5`·`7bdde7d` 포함) |

## 2. 실행 순서와 확인

| 단계 | 내용 | 결과 |
|---|---|---|
| 사전 1 | 로컬 HEAD = 승인 Commit | `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f` 일치, 브랜치 `phase3/g4-official-entry` |
| 사전 2 | 작업 트리 Clean | 변경 0건 |
| 사전 3 | `origin` URL(fetch·push) = `https://github.com/junk1901/Star_player.git` | 대소문자까지 일치(`-ceq`). `remote.origin.push`·`pushurl`·`mirror`, `remote.pushDefault`, `push.default`, `push.followTags`, Branch `pushRemote`, `url.*.insteadOf`/`pushInsteadOf` 설정 없음 |
| 사전 4·5 | 원격 `main` = `7456f44`, 미승인 Branch·Tag 없음 | `a1-refcheck.ps1 -Expect refs/heads/main=7456f44…` → `PASS remote_refs=1`(Ordinal 이름 비교, 정확한 객체 ID) |
| 사전 6 | 원격 `main`이 게시 Commit의 조상 | `merge-base --is-ancestor` 성공, 새 Commit 50개 |
| 사전 7 | 최신 A-1 Scan | `c77ed1a`: SECRET 0건(`NO_SECRET_PATTERN_FOUND`, 패턴 기반), `phase3ops\a1scan_c77ed1a\summary.json` `1556b107…` |
| 사전 8 | 게시 Ref 하나 | Refspec `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f:refs/heads/main` |
| Dry Run | `git -c push.followTags=false push --dry-run --porcelain --no-follow-tags origin c77ed1a5…:refs/heads/main` | 종료 코드 0. 한 줄: 플래그 공백(Fast-forward), `7456f44..c77ed1a`. 강제(`+`)·Tag 없음 |
| 재조회 | 원격 재대조, HEAD 재확인 | `PASS remote_refs=1`, HEAD 그대로 |
| Push | `git -c push.followTags=false push --porcelain --no-follow-tags origin c77ed1a5…:refs/heads/main` | 종료 코드 0. `7456f44..c77ed1a`, `Done` |

- **명령 표기 보정:** 지시문의 Refspec `c77ed1a…/heads/main`에는 `:refs`가 빠져 있었습니다. 그대로 쓰면 Git은 존재하지 않는 원본 Ref 이름으로 읽어 실패합니다. 승인 범위(이 SHA를 `refs/heads/main`에 Fast-forward)와 정확히 같은 `c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f:refs/heads/main`으로 실행했고, Dry Run 출력으로 전송 Ref가 그 하나뿐임을 확인한 뒤 Push했습니다.
- `--force`·`--force-with-lease`·`--mirror`·`--all`·`--tags`·Rebase·이력 재작성·Tag 생성·이동·삭제는 하지 않았습니다.

## 3. 게시 후 검증 (실제 원격)

| 확인 | 결과 |
|---|---|
| `refs/heads/main` = 승인 Commit | `a1-refcheck.ps1 -Expect refs/heads/main=c77ed1a…` → **PASS** `remote_refs=1 expected=1 bad=0` |
| 예상 밖 Branch | 없음 |
| 게시된 Tag | 없음(`ls-remote` 전체 출력: `HEAD`, `refs/heads/main` 두 줄뿐) |
| Commit 조회 | 빈 bare 저장소에 `git fetch --no-tags --depth=1 <URL> c77ed1a…` 성공, 객체 종류 `commit`, Tree `a7b9b937…` = 로컬 Tree(임시 저장소 삭제) |
| GitHub 웹 | `https://github.com/junk1901/Star_player/commit/c77ed1a5d15d43d5a97a4e5e777c9c3ea698771f` → HTTP 200 |
| SourceLink Raw 조회 | `https://raw.githubusercontent.com/junk1901/Star_player/c77ed1a…/<path>` 3개 파일(`Directory.Build.props`, `src/StarPlayer.Domain/Baseball/Battery.cs`, `tools/b1-ops/a1-refcheck.ps1`) HTTP 200, 바이트 SHA-256 = `git cat-file blob` SHA-256 |
| 조상 Commit Raw 조회 | `1751fe0`·`a980ea5`의 `Directory.Build.props` HTTP 200 |
| URL 대소문자 | 원격·SourceLink 문자열 모두 `junk1901/Star_player` 그대로 |
| B1 Tag 로컬 대상 | `b1-code-accepted-2026-10-26` → `5e19ddf…` → `7bdde7dd091f9a66957bc740c77d470c7732b3ad`(전후 같음) |
| 로컬 Tag | `b1-code-accepted-2026-10-26` 하나(전후 같음, Fetch는 `--no-tags`) |
| 로컬 작업 트리 | HEAD `c77ed1a`, 변경 0건 |
| 로컬 추적 Ref | `refs/remotes/origin/main` = `c77ed1a…`(Push가 갱신, `git fetch --no-tags origin`으로 재확인) |

## 4. SourceLink 공식 URL

- Clone·Push URL: `https://github.com/junk1901/Star_player.git`
- SourceLink: `https://raw.githubusercontent.com/junk1901/Star_player/<source_git_sha>/*`
- 게시 Commit과 조상 Commit에서 Raw 조회가 실제로 됩니다(3절). 공식 빌드의 확인 규칙은 [A-1 문서](../tasks/B1_A1_PUBLICATION_PREP.md) 5절입니다.

## 5. 공식 G2/G3 준비 상태

**갖춰진 것:**

- 공식 원격에 게시된 소스(`c77ed1a`, 조상 `1751fe0`·`a980ea5`)와 Raw 조회 가능성
- 절차: A-1 문서 6절(두 clone, 전체 검증, CI 빌드, Artifact, SourceLink, PCK)과 7절(원격 Commit·Raw 대조)
- 도구: `b4-candidates.ps1`, `a1-refcheck.ps1`, `verify.ps1`, 저장소 밖 `pedump`(PDB SourceLink 덤프)
- 사실: CI 빌드(`ContinuousIntegrationBuild=true`)는 clone 경로를 바이트에 넣지 않습니다. URL 표기는 바이트를 바꿉니다(대소문자). StarPlayer DLL은 Commit마다 바뀝니다(A-1 문서 5절, 전환 계획 2.2절).

**공식 실행 전에 필요한 것:**

| # | 필요 | 근거 | 결정 주체 |
|---|---|---|---|
| 1 | **승인 소스 Commit 확정(A1-6)**: `1751fe0`(최종 검증 소스) 또는 `c77ed1a`(게시 HEAD). 제품 코드는 둘 다 `a980ea5`와 같음. 이 값이 `source_git_sha` = `ApprovalRecord.git_commit_sha` = SourceLink Commit이 됨 | B-4 §18.3 결속 | OWNER |
| 2 | 공식 빌드 위치·계정: 개발 PC 진단으로 할지, 운영 기계(R-BUILD 스테이징, 배포 계정)에서 할지. CI 빌드 바이트는 경로와 무관하므로 측정은 먼저 할 수 있지만, Provenance `relative_path` Root(R-BUILD)는 승인 B 뒤 확정 | G5 설계 1·3절 | OWNER |
| 3 | G1(공식 출력 이름·Root) 확정 — 준비표상 G3 선행 조건 | [Gate 준비표](../tasks/B1_OPERATIONAL_GATE_READINESS.md) G1 | OWNER |
| 4 | 두 독립 clone(경로 길이 다름) → 각각 `verify.ps1` GATE PASS, D3, 내장 리소스 SHA(G2) | A-1 문서 6절 1~2단계 | 실행 |
| 5 | Golden 4/4, Dry Pilot 9회 재실행 | A-1 문서 6절 2단계 | 실행 |
| 6 | CI Release 빌드 → DLL·PDB SHA 재산출, 두 clone 일치 | A-1 문서 6절 3~4단계 | 실행 |
| 7 | `b4-candidates.ps1`로 Artifact 후보 재생성(두 clone, 모든 행 일치) | 같음 | 실행 |
| 8 | 모든 StarPlayer PDB SourceLink = 공식 Raw URL + 승인 소스 Commit | A-1 문서 5절 4번 | 실행 |
| 9 | Godot PCK 5회 반복 Export 전체 바이트 비교 — **지금까지 재실행하지 않음(미검증)** | B-4 5절 | 실행 |
| 10 | 결과 Codex 독립 검수 → OWNER 수용 | Gate 준비표 G2·G3 | Codex·OWNER |

- 4~9번은 1·2번이 정해지면 바로 실행할 수 있습니다. 1·2번 확정 전에 개발 PC에서 같은 절차를 **진단**으로 미리 돌려 볼 수도 있습니다(공식 PASS 아님).
- 이 게시 기록은 G2/G3·Build Provenance의 공식 PASS가 아닙니다.

## 6. 이 기록 이후의 로컬 Commit

- 이 보고서와 상태 문서 갱신은 게시 뒤의 **로컬 문서 Commit**입니다. 승인 A는 `c77ed1a`까지만 게시를 승인했으므로 이 Commit은 **게시하지 않았습니다**. 다음 게시는 별도 OWNER 승인이 필요합니다.
