# 작업 지시 보관 (docs/tasks)

- 루트 `TASK.md`는 B1 NSC-6 결정 근거로 보호되는 기준 파일입니다(SHA-256 `ecbc0859…f825`, [기준 목록](../contracts/b1/B1-T00_BASELINE.sha256), `verify.ps1` D3). 새 작업 지시로 교체하지 않습니다(OWNER 결정, 2026-10-25).
- 새 작업 지시는 이 디렉터리에 날짜를 붙인 파일로 두거나 Claude Code 채팅으로 전달합니다. 보관 파일은 받은 바이트 그대로 두며 `.gitattributes`에서 `-text`입니다.

| 파일 | 내용 | SHA-256 |
|---|---|---|
| `2026-10-25_B1_T11_FIX_REQUIRED_TASK.md` | Codex T11 FIX_REQUIRED 수정 지시(2026-10-25 04:25에 루트 `TASK.md`를 덮어썼던 판, 복원 전에 원본 바이트로 보관) | `90ec63b9c759171f52ff8c7eaecaaf23f7bdb53713a331d1c2d77644fca9f5f5` |
| `B1_CODE_ACCEPTANCE_TASK.md` | B1 코드 구현 최종 수용 준비(2026-10-25) | (지시서 — 보관용 바이트 고정 대상 아님) |
| `B1_OPERATIONAL_GATE_PLAN.md` | B1 운영 Gate 후속 계획(실행 없음, 2026-10-25) | (계획 문서) |
| `B1_T11_R2B_VALUE_BINDING_TASK.md` | Codex T11 R2-B 마지막 MINOR(Preflight 결정적 값 결속) 수정 지시(2026-10-25) | (지시서 — 보관용 바이트 고정 대상 아님) |
| `B1_T11_R2_FINAL_FIX_TASK.md` | Codex T11 R2 잔여 결함(R2-A INPUT_BUNDLE·R2-B PREFLIGHT) 최종 수정 지시(2026-10-25) | (지시서 — 보관용 바이트 고정 대상 아님) |
| `B1_T11_FIX_R2_TASK.md` | Codex T11 2차 FIX_REQUIRED(R1~R4) 수정 지시(2026-10-25) | (지시서 — 보관용 바이트 고정 대상 아님) |
| `B1_T11_REVIEW_TASK.md` | Codex T11 READ-ONLY 표적 재검수 지시(2026-10-25, 현재 작업 트리 기준) | (이 파일 자체가 지시서 — 보관용 바이트 고정 대상 아님) |
