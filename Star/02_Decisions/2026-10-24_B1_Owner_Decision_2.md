---
type: decision
date: 2026-10-24
phase: "3-F B-1"
status: 결정됨
tags:
  - star-player
  - decision
---

# B-1 OWNER 결정 2 — NSC-4 최소 정정 · T05 codec 진입점 · T09 Registry 신원 조회

정본: [B1-T00_OWNER_DECISION_2.md](../../docs/contracts/b1/B1-T00_OWNER_DECISION_2.md) (이 노트는 요약이며 정본을 대신하지 않습니다)

- 수신 2026-10-24T21:49:42+09:00, OWNER 명시적 텍스트 결정. 승인 계약 SHA `1338a2f7…7d64`와 NSC-4·NSC-5 결정 원문은 변경 없음
- 결정 1: 앵커 경로 구성요소별 금지 권한(상위는 삭제·이름 변경·교체·권한 변경만), INHERIT_ONLY 구분 + 상속 판정, Reparse·Fail-Closed 유지. 후보 `C:\ProgramData\StarPlayerTrust`(보호 DACL, 소유자 Administrators) — 운영 적용·실측은 미검증
- 결정 2: `EventJsonV2Codec` 공개 진입점 1개(기존 Writer/Reader 위임), 경계 시험 최소 예외
- 결정 3: 닫힌 요청 형식에 `get_registry_identity` 추가, 클라이언트는 다른 연산 전에 같은 연결에서 조회
- 미해결: 구현 해석 1·2·3·6 — 정본 끝 결정 표
