# Phase 3-F F-C2-A — Verify Evidence Manifest

`tools/verify.ps1` 전체 실행(2026-10-17 22:13 +09:00, `GATE: PASS`, 소유자 승인)의 증거 보존 기록이다. `verify.ps1`은 `docs/reports/PHASE_2_VERIFY_LOG.md`를 덮어쓰고 `build/verify/`를 매 실행마다 지우므로, 아래 두 위치로 **바이트 동일 복사**한 뒤 Phase 2 로그를 HEAD(`460c8c0`, 2026-10-11) 원본으로 복원했다. 원본 삭제 없음.

| 증거 | 위치 | SHA-256 |
|---|---|---|
| 검증 로그(추적) | `docs/reports/PHASE_3F_FC2A_VERIFY_LOG.md` | `3fb21bf2740fd10a29ffe2b34e4ef124727739babbebc164eaf143dcc1460d4a` |
| 결과 JSON(Git 무시) | `build/verify-archive/PHASE_3F_FC2A_run1/verify-result.json` | `83966cff4acb41735b4ad57ef13d730f8024273d12d0206895891c70b018ad4b` |
| 복원된 Phase 2 로그 | `docs/reports/PHASE_2_VERIFY_LOG.md` | `871aaab50e243a8e87724c89c3e216d75aa35e1607d9f79c31d59f2fcb245868` (= HEAD blob) |

- 보존 확인: 로그 `cmp` 동일, `diff -rq build/verify build/verify-archive/PHASE_3F_FC2A_run1` 차이 없음, 28/28 파일.
- 원시 로그 폴더 `build/verify-archive/PHASE_3F_FC2A_run1/`는 `build/`가 `.gitignore` 대상이라 Git에 포함되지 않는다(로컬 보존, `verify.ps1`이 지우는 `build/verify/`와 별개 경로). 파일별 SHA-256:

```
5c24cafddd28c10bf07b3be36f1173d9b4d607fe703dc291e9225c111b90b600  ./B1.log
6cf8383bf59cd549602b5d76edcca33e96db9effe5a0ad0af3b875befaeb0f81  ./E1.log
b46e1ae96f2b5fede33641c0ba163be0a2da13781c0e55da2784740dcbd8509c  ./E2.log
49fcead6a5878e469024bc48fa5148719621c56b0a5a4dad82e631ee674a1793  ./E3.log
00eff1a7417272f31cc3027bea42de6036cab3589fde83be1a81dbce9d3926dd  ./E4.log
5bb7279fca62a736f0dc20077a124d35be57eca385402b349bc509cfc3ca2ad8  ./G1.log
0dc598d187845dff7b1101af7815eb8f2ad9eeea2693439d05ffc8db574824da  ./G2.log
37c0b1be38fb6b70d307aa68b1c2e5fdcc5bac5982d7521e4773b2bac7c1245e  ./G3.log
42f975596d17f93a16fd903aa216bc281aaf4d2139452f16025d489b2410b407  ./G4.log
e32d943f48e7bb4e27b11934cd8584c7cfb49b5de2ac5874d078b2f179102bb3  ./U1.log
ae150e14276763841d20ffae8ae639b77a90ca437aca9d5ab06c7e492e17f395  ./U2.log
6f368266015426d8463accbfd6ddf34b8d8487b23e56464bdeeb50fe5fa768a0  ./U3.log
1e888364278a081a95702e09cc47c9fcbacf963fc05fd8bdd6bcc6b6c4579d3e  ./U3a.log
cbddfedd3f5eeb70451a097d521d32e34a7a3da654087f7c62f3a074e243e35a  ./U3b.log
0943b6625f4125ac56537b9ff8895e67ad8a012675878f1a0fb8fe06b86afdf2  ./U4.log
aef63374564aa7997cf83f94a6ce403c60d30e48b7519a00e47b7eed9149d1f7  ./U5.log
103d1a3e32c39d8c63033a544483cc89631afe2cb214f4c8c7fb78352ec89d38  ./exported-diagnostics.txt
47d6cc9f78de716e292f6f238713b317cb804d14a4a9433b3acf4357fc3e8095  ./exported-game-diagnostics.txt
1f26329a4be0d86f6dbbbcb311cb24f7ea66707cd6688757436fea5ef7ab3c2d  ./exported-game-screen-end.png
4455e6ece67ec39f704592a2b84e74a3e4e07a0a7301e71bbfd26ac90a84b9f0  ./exported-game-screen.png
c8c70b5f983b1b13829ca2e9e8a28d024c11bcbff051bedba77684292d348ab1  ./exported-player-card-npc.png
4ede6a126aa97690574ee15f06ccd687e80901796d5d0f1a1ba6ab44aa1aef45  ./exported-player-card.png
a53e654fe3f48af6ad8b9a73b5411aad2b71f10b620dbfae5482a58afb992cb0  ./exported-player-diagnostics.txt
5388d19d1ac01c87e8ea08857913f51d577b479057154fde01e450bc4637594e  ./exported-shell.png
103d1a3e32c39d8c63033a544483cc89631afe2cb214f4c8c7fb78352ec89d38  ./headless-diagnostics.txt
47d6cc9f78de716e292f6f238713b317cb804d14a4a9433b3acf4357fc3e8095  ./headless-game-diagnostics.txt
a53e654fe3f48af6ad8b9a73b5411aad2b71f10b620dbfae5482a58afb992cb0  ./headless-player-diagnostics.txt
83966cff4acb41735b4ad57ef13d730f8024273d12d0206895891c70b018ad4b  ./verify-result.json
```
