# B1-T00 NSC-4·NSC-5 OWNER 결정 기록

- 결정 일시: 2026-10-24T14:25:36+09:00 (Asia/Seoul) · 방식: OWNER 명시적 텍스트 승인 · 승인 기록: [B1-T00_OWNER_APPROVAL_RECORD.md](B1-T00_OWNER_APPROVAL_RECORD.md)
- 원본 계약과의 관계: 승인 계약(SHA-256 `1338a2f7…7d64`) 2부 §2.3이 NSC-4·NSC-5를 "OWNER가 정해야 하는 미해결 구현 Gate"로 남겼습니다. 이 기록은 그 빈칸을 채우는 **신규 OWNER 결정**이며, 승인 계약 본문을 고치지 않습니다. 승인 계약과 이 기록이 충돌하면 계약 본문이 우선하고, 충돌은 OWNER에게 보고합니다.
- 적용 시점: NSC-4는 T06a Trust Anchor 검증 구현부터, NSC-5는 T09 Registry 서비스 설정 Reader/Writer 구현부터 적용합니다.
- 바뀌지 않는 것: §16 T2 Fail-Closed 원칙, Hash 계약, RNG 소비, Replay 순서, 통계 정책, `OfficialApprovals.Current` 차단 정책.
- T11 Codex 검수 범위에 이 기록의 정합성과 구현 준수 여부가 포함됩니다.

## NSC-4 — 앵커 ACL 검사 대상 (B안 채택)

**규범.** §16 T2의 "현재 검증 프로세스 계정과 증거 작성자 주체에게 쓰기·삭제·ACL 변경 권한을 주지 않아야 합니다"는 다음 허용 목록 규칙으로 판정합니다.

- 앵커 관리 허용 주체(TCB): `S-1-5-18` (SYSTEM), `S-1-5-32-544` (BUILTIN\Administrators), `S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464` (NT SERVICE\TrustedInstaller).
- 검사 대상: 앵커 파일과 그 상위 디렉터리 전부(드라이브 루트까지).
- 다음 권한을 TCB 밖 SID에 부여하는 Allow ACE가 하나라도 있으면 거부합니다.
  - 파일: `FILE_WRITE_DATA`, `FILE_APPEND_DATA`, `FILE_WRITE_EA`, `FILE_WRITE_ATTRIBUTES`, `DELETE`, `WRITE_DAC`, `WRITE_OWNER`
  - 디렉터리: 위 항목 + `FILE_DELETE_CHILD`(하위 객체 삭제), `FILE_ADD_FILE`, `FILE_ADD_SUBDIRECTORY`
- 소유자 SID는 TCB에 속해야 합니다.

**검증 기준.**

1. Generic 권한(`GENERIC_ALL`, `GENERIC_WRITE`, `GENERIC_READ`, `GENERIC_EXECUTE`, `MAXIMUM_ALLOWED`)은 File/Directory 일반 매핑으로 바꾼 뒤 판정합니다.
2. 상속 ACE와 그룹 ACE도 같은 규칙으로 판정합니다. 그룹이 TCB 밖이면(예: Everyone, Users, Authenticated Users, CREATOR OWNER, OWNER RIGHTS) 거부합니다.
3. NULL DACL(모든 접근 허용)은 거부합니다.
4. Allow·Deny·Audit 외의 ACE 형식(Object ACE, Callback ACE, 해석할 수 없는 형식)이 DACL에 있으면 거부합니다. Deny ACE는 허용 판정을 완화하는 근거로 쓰지 않습니다.
5. 검증 프로세스와 증거 작성자(`verifier_principal_sid`, `owner_principal_sids`, `evidence_reader_sids`)가 보호 경로에 쓰기·삭제·ACL 변경 실효 권한을 가져서는 안 됩니다. 위 규칙은 TCB 밖 모든 SID를 거부하므로 이 조건을 포함합니다.
6. 검증기는 관리자 권한(Elevated 토큰, `BUILTIN\Administrators` 그룹이 Enabled)으로 실행되면 시작을 거부합니다. 검증·증거 작성 계정이 관리자 권한으로 이 검사를 우회하는 운영은 허용하지 않습니다.
7. OWNER 등록 주체(`owner_principal_sids`)와 관리자 권한으로 앵커를 관리하는 주체는 운영 권한이 구분됩니다. 앵커 수정은 TCB 경로로만 합니다.
8. 실제 Windows 환경에서 상위 디렉터리까지 포함한 검증이 실패하면 보안 조건을 완화하지 않고 실패를 보고합니다.

**수용 시험 상태.** N25·T2 관련 시험은 순수 판정 로직(SDDL 입력)으로 구현·자체 시험합니다. 실제 운영 앵커 경로의 Windows 실측은 운영 앵커가 없으므로 **미검증**입니다.

## NSC-5 — Registry 서비스 설정 DB 경로 (권장안 채택)

**규범.** `simharness-registry-service-config-v1`에 다음 필드를 둡니다.

- 필드명: `registry_database_path`
- 타입: JSON string, 검증 타입 `WINDOWS_LOCAL_ABS_PATH`

**`WINDOWS_LOCAL_ABS_PATH`.**

- 허용: 로컬 드라이브 기반 절대 파일 경로 `^[A-Za-z]:\\`로 시작하는 경로. 예: `C:\StarPlayerRegistry\registry.db`
- 거부:
  - UNC(`\\server\share`), 장치·확장 경로(`\\?\`, `\\.\`, `\??\`)
  - 상대 경로, 드라이브 상대 경로(`C:foo`), 루트 상대 경로(`\foo`)
  - 빈 구간, `.`·`..` 구간, `/` 구분자, 끝의 `.`·공백
  - 제어 문자 U+0000–U+001F, 잘못된 경로 문자 `< > " | ? *`, 드라이브 뒤의 `:`(대체 데이터 스트림)
  - 예약 장치 이름(`CON`, `PRN`, `AUX`, `NUL`, `COM0`–`COM9`, `LPT0`–`LPT9`, 확장자가 붙은 형태 포함)
  - Reparse Point가 있는 경로(PathSafety)
  - 저장소 루트 또는 증거 루트 안의 경로
- 구문 검증만으로 통과시키지 않습니다. `Path.GetFullPath` 정규화 결과가 원문과 ordinal로 같아야 하고, 실제 경로의 Reparse Point·대상 위치·보안 설정을 확인합니다.
- 서비스 설정은 기존 계약(R03-5-1)대로 시작할 때 한 번만 읽어 불변 스냅샷으로 씁니다.

**수용 시험 상태.** 필드·타입 검증은 단위 시험으로 구현·자체 시험합니다. AT-14n·AT-14r은 실제 서비스 배치가 필요하므로 **미검증**입니다.
