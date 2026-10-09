# Source Section Index

Original headings and one-based line numbers from the unchanged source copies. Requirement mappings are in [Requirements Traceability](REQUIREMENTS_TRACEABILITY.md). Headings quoted in Korean preserve the source wording; surrounding architecture documents are English. This index is navigation evidence, not implementation evidence.

## G — Game design

Source: [Star_Player_game_v1.6.txt](../sources/Star_Player_game_v1.6.txt).

| Line | Original heading |
|---:|---|
| 1 | KBO Simulator 게임 기획서 v1.6 |
| 3 | 문서 구조 |
| 23 | PART 1. 프로젝트 정의와 핵심 방향 |
| 40 | 핵심 컨셉 |
| 64 | 게임 핵심 목표 |
| 84 | 게임 세계 구조 |
| 139 | 게임의 핵심 재미 |
| 155 | 중요한 설계 원칙 |
| 163 | 구단 운영 최소화 |
| 169 | 현실성과 게임성의 균형 |
| 174 | 돈의 의미 |
| 186 | 선택의 의미 |
| 198 | PART 2. 게임 시작 및 선수 생성 |
| 202 | 스타 플레이어 시작 방식 |
| 225 | 능력치 설정 |
| 275 | 선수 성격 |
| 300 | 기존 선수 선택 |
| 324 | 신규 선수 생성 밸런스 |
| 329 | 권장 생성 포인트 |
| 343 | 포인트 비용 |
| 362 | 치트 모드 |
| 380 | 치트 모드와 잠재력 |
| 398 | 출신별 밸런스 |
| 425 | 투타 겸업 생성 |
| 433 | 신체 조건 |
| 446 | 능력치 공개 시스템 |
| 457 | 플레이어 자신의 현재 능력 |
| 487 | 다른 선수의 현재 능력 |
| 502 | 잠재력 비공개 |
| 521 | 상태 정보 |
| 537 | PART 3. 핵심 게임 루프와 경기 플레이 |
| 541 | 핵심 게임 루프 |
| 585 | 경기 플레이 방식 |
| 592 | 타자 플레이 |
| 615 | 투수 플레이 |
| 636 | 경기 시뮬레이션 엔진 |
| 668 | 경기 진행 속도 |
| 680 | PART 4. 선수 능력·성장·상태 시스템 |
| 684 | 선수 능력치 |
| 702 | 투수 |
| 722 | 선수 상태 |
| 739 | 성장 시스템 |
| 761 | 노화 시스템 |
| 773 | 훈련 시스템 |
| 796 | 능력치 산정 시스템 |
| 828 | 내부 능력치 스케일 |
| 859 | 2026 실제 선수 초기 능력 산정 |
| 882 | 리그 상대평가 |
| 894 | 표본 크기 보정 |
| 919 | 타자 능력 산정 |
| 971 | 투수 능력 산정 |
| 1007 | 자동 산정과 수동 보정 |
| 1031 | 기본 능력과 단기 상태 분리 |
| 1037 | 현재 상태 |
| 1060 | 능력치 변화 속도 |
| 1085 | 잠재력 비공개 및 평가 시스템 |
| 1101 | 숨겨진 실제 잠재력 |
| 1118 | 스카우터 평가 |
| 1144 | 잠재력 범위 평가 |
| 1157 | 언론 평가 |
| 1168 | 유망주 랭킹 |
| 1195 | 평가 정확도 |
| 1221 | 평가자별 차이 |
| 1237 | 성장과 잠재력의 관계 |
| 1258 | 플레이어 본인의 잠재력 |
| 1277 | 기존 선수의 잠재력 |
| 1284 | 잠재력 시스템 설계 원칙 |
| 1300 | 투타 겸업 및 포지션 변경 시스템 |
| 1308 | 역할별 능력과 숙련도 분리 |
| 1330 | 포지션 변경 |
| 1355 | 포지션 숙련도 |
| 1379 | 포지션 숙련도 성장 |
| 1400 | 투타 겸업 |
| 1411 | 투수에서 타자로 전환 |
| 1431 | 타자에서 투수로 전환 |
| 1451 | 투타 겸업 성장 |
| 1465 | 투타 겸업 피로도 |
| 1490 | 실전 능력 계산 |
| 1495 | 기본 능력 |
| 1521 | AI 감독의 역할 판단 |
| 1538 | 포지션 경쟁과 커리어 선택 |
| 1556 | 포지션 및 역할 관련 랜덤 스토리 |
| 1582 | 커리어 기록 |
| 1605 | 투타 겸업 및 포지션 변경 설계 원칙 |
| 1621 | 부상 |
| 1660 | 부상 세부 모델 |
| 1672 | 부상 부위 |
| 1691 | 부상 심각도 |
| 1718 | 참고 출전 |
| 1747 | 부상 위험 계산 |
| 1782 | 부상 이력과 재발 |
| 1801 | 능력치 영향 |
| 1835 | 재활 과정 |
| 1860 | 부상 관련 AI 뉴스 |
| 1870 | 능력 공개·선수 생성·부상 통합 원칙 |
| 1889 | 팀 내 입지 |
| 1917 | 감독 신뢰 |
| 1935 | PART 5. 시즌·신인·2군·프로 커리어 |
| 1939 | 시즌 구조 |
| 1966 | 아마추어·신인 공급 시스템 |
| 2000 | 고교 야구부 데이터 |
| 2033 | 대학 야구부 데이터 |
| 2064 | 해외팀 데이터 |
| 2109 | 신인 자동 생성 흐름 |
| 2147 | 학교와 선수 능력의 관계 |
| 2182 | 아마추어 스카우팅 |
| 2221 | 학교 및 팀 명성 |
| 2254 | 출신 학교의 커리어 기록 |
| 2298 | 데이터팩 구조 |
| 2334 | 세이브 독립성 |
| 2352 | 설계 원칙 |
| 2369 | 102.12 육성선수 경로 |
| 2403 | 권장 선수 데이터 |
| 2442 | 선수 신분 상태 |
| 2480 | 미지명 선수의 진로 분기 |
| 2519 | 육성선수 계약 |
| 2539 | 정식선수 전환 |
| 2571 | 육성선수와 잠재력 |
| 2616 | 커리어 기록 |
| 2654 | 데이터 설계 원칙 |
| 2669 | 신인 드래프트 |
| 2688 | 2군 리그 시스템 |
| 2757 | 선수 계약 |
| 2768 | FA |
| 2849 | 트레이드 |
| 2859 | PART 6. 국가대표·국제대회·병역 |
| 2863 | 국가대표 시스템 |
| 2882 | 국제대회 시스템 |
| 2903 | 국가대표 선발 및 커리어 효과 |
| 2921 | 국가대표 커리어 기록 |
| 2937 | 군 복무 시스템 |
| 2958 | 일반 군 복무 |
| 2972 | 군 체육팀과 2군 리그 연동 |
| 2999 | 국제대회와 병역 연동 |
| 3012 | 병역과 국제대회 커리어 이벤트 |
| 3025 | 병역 결정 시스템 |
| 3040 | 병역 관련 랜덤 스토리 |
| 3062 | 시즌 구조 확장 |
| 3097 | 개인 커리어 기록 확장 |
| 3120 | 커리어 예시 |
| 3171 | PART 7. 생활·자산·관계·상업 시스템 |
| 3175 | 개인 자산 |
| 3187 | 소비 시스템 |
| 3202 | 자동차 |
| 3222 | 주거 |
| 3240 | 야구 장비 |
| 3257 | 자기 투자 |
| 3272 | 생활 및 취미 |
| 3328 | 랜덤 스토리 시스템 |
| 3351 | 랜덤 스토리 예 |
| 3363 | FA 대박 |
| 3374 | 슬럼프 |
| 3386 | 인간관계 시스템 |
| 3477 | 명성 및 인기 |
| 3551 | 생활·관계·상업 시스템 통합 구조 |
| 3576 | PART 8. 기록·역사·뉴스·선수·구단 데이터베이스 |
| 3580 | 기록 시스템 |
| 3611 | 고급 기록 |
| 3623 | 개인 커리어 기록 |
| 3647 | KBO 역사 |
| 3661 | 구단 DB 및 공통 구단 프로필 시스템 |
| 3692 | 구단 기본 정보 |
| 3729 | 구단 역사 요약 |
| 3763 | 연도별 구단 역사 |
| 3802 | 실제 시작 역사와 세이브 역사 |
| 3842 | 프랜차이즈 기록 |
| 3878 | 주요 선수와 구단 레전드 |
| 3908 | 구단별 수상 기록 |
| 3937 | 영구결번 연동 |
| 3954 | 현재 로스터 연동 |
| 3973 | 전역 구단 링크 |
| 4004 | team_id 기반 연결 |
| 4027 | 구단 DB 설계 원칙 |
| 4042 | 뉴스 시스템 |
| 4070 | AI 활용 |
| 4093 | AI 뉴스 시스템 |
| 4101 | 뉴스 생성 흐름 |
| 4162 | AI에 전달하는 데이터 |
| 4183 | 사실 무결성 규칙 |
| 4198 | 가상 언론 매체 |
| 4224 | 인터뷰 시스템 |
| 4249 | AI 뉴스와 API 비용 관리 |
| 4262 | 전체 선수 DB 및 공통 선수 프로필 시스템 |
| 4276 | 선수 검색 및 필터 |
| 4299 | 공통 선수 프로필 |
| 4335 | 능력치 공개 규칙 |
| 4363 | 전역 선수 링크 |
| 4391 | 장기 세이브와 은퇴 선수 |
| 4411 | player_id 기반 연결 |
| 4433 | 설계 원칙 |
| 4448 | PART 9. 은퇴·영구결번·명예의 전당 |
| 4452 | 영구결번 시스템 |
| 4461 | Franchise Legacy 평가 요소 |
| 4479 | 팀 내 입지 — 약 20% |
| 4491 | 해당 구단에서의 성적과 기록 — 약 20% |
| 4504 | 팬 인기와 상징성 — 약 15% |
| 4515 | 우승 및 포스트시즌 기여 — 약 10% |
| 4524 | 개인 수상 및 상징적인 업적 — 약 5% |
| 4534 | 구단 충성도 및 커리어 이야기 — 약 5% |
| 4545 | 구단별 평가 |
| 4570 | 영구결번 결정 시점 |
| 4592 | 영구결번 뉴스 이벤트 |
| 4607 | 여러 구단에서의 영구결번 |
| 4623 | 능력치·AI 뉴스·영구결번 통합 원칙 |
| 4639 | 은퇴 후 콘텐츠 |
| 4660 | 커리어 총결산 |
| 4686 | 은퇴 이벤트 |
| 4707 | 은퇴 후 후일담 |
| 4733 | 명예의 전당 시스템 |
| 4740 | 명예의 전당 |
| 4747 | Hall of Fame Score |
| 4767 | 후보 및 선정 |
| 4791 | NPC 선수 명예의 전당 |
| 4804 | 명예의 전당과 AI 뉴스 |
| 4818 | PART 10. 데이터팩·세이브·모딩 |
| 4822 | 외부 데이터 및 파일 등록 시스템 |
| 4835 | 기본 데이터와 사용자 데이터 분리 |
| 4857 | 파일 등록 시스템 |
| 4895 | 데이터팩 시스템 |
| 4936 | 선수 고유 ID 시스템 |
| 4967 | 구단 고유 ID 시스템 |
| 4982 | 이미지 등록 및 대체 시스템 |
| 4996 | 데이터팩과 세이브 파일 분리 |
| 5023 | 새 게임과 데이터팩 선택 |
| 5043 | 데이터 유효성 검증 |
| 5067 | 데이터 에디터 확장 가능성 |
| 5083 | 모딩 시스템 기반 |
| 5147 | 개인 사용 및 향후 공유 구조 |
| 5162 | 외부 데이터 시스템 설계 원칙 |
| 5176 | 기본 모딩 시스템 |
| 5182 | 기본 지원 범위 |
| 5212 | 초기 제외 범위 |
| 5226 | 모딩과 세이브 안정성 |
| 5240 | PART 11. UI·기술 스택·배포 |
| 5244 | PC UI 기본 방향 |
| 5265 | 메인 화면 예 |
| 5284 | 게임 엔진 및 개발 언어 |
| 5291 | Godot 선택 이유 |
| 5309 | C# 선택 이유 |
| 5332 | UI 기술 스택 |
| 5337 | 지원 해상도 |
| 5364 | UI 기본 구조 |
| 5384 | 전체 기술 스택 |
| 5419 | UI와 게임 로직 분리 |
| 5447 | 배포 범위 |
| 5462 | PART 12. 개발 범위·확장·최종 확정 원칙 |
| 5466 | 초기 개발 범위 |
| 5482 | Prototype 2 |
| 5495 | Prototype 3 |
| 5507 | 이후 |
| 5529 | 추후 확장 후보 |
| 5537 | 현재 확정된 핵심 방향 |
| 5652 | 해외 진출 관련 범위 제한 |
| 5675 | 기획 확정 원칙 |

## U — UI/UX guide

Source: [UI_UX_GUIDE_v1.8.md](../sources/UI_UX_GUIDE_v1.8.md).

| Line | Original heading |
|---:|---|
| 1 | Star Player UI/UX GUIDE v1.8 |
| 3 | 1. 문서 목적 |
| 21 | 2. UI 핵심 목표 |
| 25 | 2.1 선수 중심 |
| 37 | 2.2 정보 중심 PC UI |
| 58 | 2.3 한 화면에 너무 많은 정보를 넣지 않는다 |
| 87 | 2.4 플레이 흐름을 끊지 않는다 |
| 111 | 3. 전체 화면 구조 |
| 148 | 4. 상단 상태 바 |
| 184 | 5. 좌측 메인 메뉴 |
| 214 | 6. 홈 화면 |
| 224 | 6.1 기본 정보 |
| 238 | 6.2 시즌 성적 |
| 258 | 6.3 현재 상태 |
| 274 | 6.4 팀 내 상태 |
| 293 | 6.5 다음 행동 |
| 319 | 7. 내 선수 화면 |
| 336 | 7.1 프로필 |
| 355 | 7.2 능력치 |
| 391 | 7.3 잠재력 관련 정보 |
| 416 | 8. 경기 화면 |
| 420 | 8.1 기본 구조 |
| 457 | 8.2 경기 화면 우선 정보 |
| 484 | 8.3 경기 결과 |
| 511 | 8.4 경기 진행 모드 |
| 524 | 8.5 야구장 다이어그램 표시 원칙 |
| 528 | 수비팀 표시 |
| 551 | 공격팀 표시 |
| 576 | 주자 정보 |
| 606 | 8.6 기본 정보와 상세 정보 분리 |
| 648 | 8.7 수비 / 주자 UI 활용 원칙 |
| 667 | 9. 경기 결과 화면 |
| 704 | 10. 훈련 화면 |
| 747 | 11. 기록 화면 |
| 778 | 12. 팀 화면 |
| 813 | 13. 리그 화면 |
| 831 | 13.1 전체 선수 DB |
| 868 | 13.2 공통 선수 프로필 |
| 913 | 13.3 선수 프로필 능력 공개 규칙 |
| 917 | 플레이어 자신의 선수 |
| 927 | NPC 선수 |
| 946 | 잠재력 |
| 952 | 13.4 전역 선수 링크 |
| 999 | 13.5 은퇴 선수 프로필 |
| 1021 | 14. 계약 화면 |
| 1056 | 15. 자산 화면 |
| 1080 | 16. 생활 화면 |
| 1110 | 17. 인간관계 화면 |
| 1143 | 18. 뉴스 화면 |
| 1181 | 19. 이벤트 팝업 |
| 1203 | 20. 부상 UI |
| 1247 | 21. 1군 / 2군 UI |
| 1273 | 22. 국가대표 UI |
| 1294 | 23. 병역 UI |
| 1320 | 24. 커리어 화면 |
| 1361 | 25. 은퇴 UI |
| 1388 | 26. 능력치 표시 원칙 |
| 1390 | 플레이어 자신의 현재 능력 |
| 1399 | 다른 선수 현재 능력 |
| 1414 | 실제 잠재력 |
| 1422 | 27. 중요 정보 우선순위 |
| 1426 | Priority A — 즉시 보여줘야 하는 정보 |
| 1437 | Priority B — 홈 화면에 요약 |
| 1446 | Priority C — 상세 메뉴에서 확인 |
| 1456 | 28. 알림 시스템 |
| 1460 | 일반 알림 |
| 1469 | 주요 이벤트 |
| 1477 | 대형 이벤트 |
| 1492 | 29. UI 상태 표현 |
| 1514 | 30. 색상 사용 원칙 |
| 1539 | 31. 표와 리스트 원칙 |
| 1559 | 32. 팝업 사용 원칙 |
| 1575 | 33. 툴팁 |
| 1592 | 34. 저장과 로딩 UI |
| 1623 | 34.1 저장 정책 |
| 1627 | 수동 저장 |
| 1633 | 자동 저장 |
| 1647 | 세이브 슬롯 |
| 1666 | 이어하기 |
| 1672 | 데이터팩과 세이브 |
| 1692 | 35. 데이터팩 UI |
| 1723 | 36. 신규 선수 생성 UI |
| 1738 | 36.1 능력치 생성 |
| 1763 | 37. 기존 선수 선택 UI |
| 1794 | 38. Prototype별 UI 구현 범위 |
| 1800 | Prototype 1 — 기능 검증 UI |
| 1828 | Prototype 2 — 핵심 UX 검증 |
| 1869 | Prototype 3 — 한 시즌 UX 검증 |
| 1892 | 39. Alpha 단계 UI |
| 1916 | 40. Polish 단계 |
| 1937 | 41. 구현 기술 원칙 |
| 1951 | 41.1 UI와 게임 로직 분리 |
| 1975 | 41.2 AI 콘텐츠 |
| 1995 | 42. Godot UI 구현 원칙 |
| 2042 | 43. 화면 구현 우선순위 |
| 2081 | 44. UX 검증 기준 |
| 2085 | 경기 |
| 2091 | 홈 |
| 2096 | 능력치 |
| 2101 | 기록 |
| 2105 | 계약 |
| 2109 | 장기 플레이 |
| 2116 | 45. 현재 확정하지 않는 항목 |
| 2135 | 46. 해상도 및 화면 대응 기준 |
| 2145 | 46.1 공식 지원 해상도 |
| 2156 | 46.2 2560 × 1440 기준 |
| 2180 | 46.3 1920 × 1080 대응 |
| 2211 | 46.4 화면 비율 |
| 2228 | 46.5 창 모드 |
| 2244 | 46.6 해상도별 QA 기준 |
| 2272 | 46.7 Prototype 단계 적용 |
| 2274 | Prototype 1 |
| 2278 | Prototype 2 |
| 2282 | Prototype 3 |
| 2286 | Alpha |
| 2290 | Polish |
| 2298 | 46.8 최종 원칙 |
| 2314 | 47. Codex 로드맵 작성 시 UI 원칙 |
| 2337 | 48. 최종 UI/UX 방향 |
| 2362 | 49. 현재 단계 결론 |
| 2395 | 50. 아마추어·신인 공급 시스템 UI |
| 2407 | 50.1 접근 경로 |
| 2430 | 50.2 신인 풀 화면 |
| 2456 | 50.3 신인 프로필 |
| 2504 | 50.4 출신 학교 표시 |
| 2531 | 50.5 학교 / 팀 정보 패널 |
| 2565 | 50.6 학교별 선수 검색 |
| 2589 | 50.7 드래프트 전망 UI |
| 2615 | 50.8 드래프트 결과 UI |
| 2643 | 50.9 선수 DB와 연동 |
| 2671 | 50.10 실제 학교 데이터와 가상 선수 |
| 2693 | 50.11 Prototype 적용 시점 |
| 2695 | Prototype 1~2 |
| 2699 | Prototype 3 |
| 2703 | 장기 시즌 / Draft 구현 단계 |
| 2713 | 이후 확장 |
| 2724 | 50.12 UI 원칙 |
| 2737 | 51. 육성선수 UI |
| 2761 | 51.1 신인 프로필 표시 |
| 2783 | 51.2 육성선수 상태 표시 |
| 2815 | 51.3 정식선수 전환 이벤트 |
| 2841 | 51.4 선수 DB 필터 |
| 2871 | 51.5 커리어 타임라인 |
| 2898 | 51.6 UI 원칙 |
| 2909 | 52. 구단 DB 및 공통 구단 프로필 UI |
| 2927 | 52.1 구단 목록 |
| 2954 | 52.2 공통 구단 프로필 |
| 2996 | 52.3 구단 개요 |
| 3020 | 52.4 구단 역사 |
| 3047 | 52.5 우승 및 포스트시즌 기록 |
| 3075 | 52.6 프랜차이즈 기록 |
| 3106 | 52.7 주요 선수 |
| 3131 | 52.8 수상 기록 |
| 3156 | 52.9 영구결번 |
| 3178 | 52.10 현재 로스터 |
| 3203 | 52.11 전역 구단 링크 |
| 3245 | 52.12 뒤로가기 상태 유지 |
| 3261 | 52.13 해상도 대응 |
| 3290 | 52.14 Prototype 적용 시점 |
| 3292 | Prototype 1~2 |
| 3296 | Prototype 3 |
| 3305 | 장기 시즌 구현 단계 |
| 3314 | Alpha 이후 |
| 3327 | 52.15 UI 원칙 |

## M — Direct master request

Source: [MASTER_REQUEST.txt](../sources/MASTER_REQUEST.txt).

| Line | Original heading |
|---:|---|
| 1 | STAR PLAYER — CODEX MASTER ARCHITECTURE & DEVELOPMENT ROADMAP INSTRUCTIONS |
| 4 | 1. ROLE |
| 42 | 2. FIRST TASK |
| 59 | 3. SOURCE OF TRUTH |
| 105 | 4. PROJECT OVERVIEW |
| 125 | 5. CORE GAME WORLD |
| 174 | 6. CORE GAME LOOP |
| 221 | 7. FIXED TECHNOLOGY STACK |
| 237 | 8. TECHNOLOGY RESPONSIBILITIES |
| 239 | Godot Control UI |
| 259 | C# Game Core |
| 290 | SQLite |
| 312 | JSON |
| 330 | Gemini API |
| 345 | 9. CORE ARCHITECTURE RULE |
| 376 | 10. AI CONTENT ARCHITECTURE |
| 412 | 11. DOMAIN IDENTITY |
| 435 | 12. PLAYER MODEL SEPARATION |
| 441 | Base / Current Ability |
| 445 | Short-Term State |
| 457 | Hidden Potential |
| 461 | Position / Role Proficiency |
| 470 | Team Role |
| 486 | 13. ABILITY VISIBILITY RULES |
| 491 | User-Controlled Player |
| 498 | Other Players |
| 511 | True Potential |
| 525 | 14. PLAYER CREATION |
| 544 | 15. BASEBALL GAMEPLAY |
| 551 | Hitter Examples |
| 561 | Pitcher Examples |
| 574 | 16. GAME SIMULATION INPUTS |
| 595 | 17. GAME PLAY MODES |
| 608 | 18. POSITION CHANGE & TWO-WAY SYSTEM |
| 636 | 19. PLAYER DEVELOPMENT |
| 664 | 20. INJURY SYSTEM |
| 696 | 21. FIRST-TEAM / SECOND-TEAM SYSTEM |
| 729 | 22. AMATEUR FEEDER SYSTEM |
| 757 | 23. SCHOOL / AMATEUR TEAM EFFECTS |
| 774 | 24. DEVELOPMENTAL PLAYER RULE |
| 817 | 25. UNDRAFTED PLAYER PATHS |
| 839 | 26. CONTRACT / FA / TRADE |
| 866 | 27. AGENT SYSTEM |
| 890 | 28. NATIONAL TEAM |
| 915 | 29. MILITARY SYSTEM |
| 940 | 30. RELATIONSHIPS / LIFE SYSTEM |
| 971 | 31. ASSETS / LIFESTYLE |
| 996 | 32. PLAYER DATABASE |
| 1026 | 33. COMMON PLAYER PROFILE |
| 1047 | 34. GLOBAL PLAYER LINKS |
| 1070 | 35. TEAM DATABASE |
| 1099 | 36. TEAM IDENTITY |
| 1108 | 37. TEAM HISTORY |
| 1125 | 38. FRANCHISE RECORDS |
| 1152 | 39. GLOBAL TEAM LINKS |
| 1172 | 40. RETIRED NUMBER SYSTEM |
| 1195 | 41. HALL OF FAME |
| 1222 | 42. RETIREMENT |
| 1241 | 43. DATA PACK ARCHITECTURE |
| 1262 | 44. SAVE AUTHORITY |
| 1295 | 45. SAVE SYSTEM |
| 1321 | 46. MODDING SCOPE |
| 1345 | 47. CODE MODDING OUT OF SCOPE |
| 1358 | 48. OVERSEAS SCOPE |
| 1377 | 49. GAME SCREEN FIELD VISUALIZATION |
| 1384 | Defense |
| 1397 | Offense |
| 1411 | 50. UI / UX AUTHORITY |
| 1446 | 51. SUPPORTED RESOLUTIONS |
| 1456 | Design Baseline |
| 1461 | Secondary Supported Resolution |
| 1480 | Do Not Spend Initial Development Time On |
| 1489 | 52. UI DEVELOPMENT TIMING |
| 1496 | Prototype 1 |
| 1504 | Prototype 2 |
| 1514 | Prototype 3 |
| 1521 | Alpha |
| 1526 | Polish |
| 1539 | 53. DESIGN PRINCIPLES |
| 1570 | 54. CODEBASE STRUCTURE |
| 1597 | 55. SYSTEM CONTRACT REQUIREMENT |
| 1648 | 56. EVENT-DRIVEN INTEGRATION |
| 1691 | 57. DETERMINISM & RANDOMNESS |
| 1713 | 58. DATABASE STRATEGY |
| 1745 | 59. SAVE COMPATIBILITY |
| 1760 | 60. PERFORMANCE |
| 1781 | 61. CLAUDE CODE IMPLEMENTATION MODEL |
| 1796 | 62. PHASE SIZE |
| 1812 | 63. ROADMAP PRIORITY |
| 1859 | 64. DO NOT OVERLOAD EARLY PHASES |
| 1874 | 65. ROADMAP FORMAT |
| 1882 | Star Player Development Roadmap |
| 1886 | 1. Architecture Summary |
| 1898 | 2. Repository Structure |
| 1905 | 3. Core Domain Model |
| 1912 | 4. Data Flow |
| 1924 | 5. Persistence Strategy |
| 1936 | 6. Testing Strategy |
| 1949 | 7. Phase Dependency Map |
| 1954 | 8. Development Phases |
| 1959 | 66. REQUIRED PHASE TEMPLATE |
| 1963 | Phase N — <Name> |
| 1965 | Goal |
| 1974 | Dependencies |
| 1979 | Scope |
| 1984 | Explicit Non-Goals |
| 1991 | Architecture / Systems |
| 1996 | Domain Models |
| 2001 | Implementation Tasks |
| 2008 | UI Work |
| 2015 | Persistence Work |
| 2027 | Events |
| 2032 | Tests |
| 2043 | Acceptance Criteria |
| 2052 | Deliverables |
| 2064 | Risks |
| 2069 | Documentation Updates |
| 2074 | Claude Code Handoff |
| 2079 | 67. ACCEPTANCE GATES |
| 2101 | 68. PHASE REPORT |
| 2109 | Phase N Report |
| 2111 | Goal |
| 2113 | Completed |
| 2115 | Files Changed |
| 2117 | Tests |
| 2119 | Acceptance Criteria |
| 2121 | Design Decisions |
| 2123 | Known Issues |
| 2125 | Deferred Work |
| 2127 | Technical Debt |
| 2129 | Next Phase Preconditions |
| 2136 | 69. CURRENT STATE |
| 2146 | Current State |
| 2148 | Current Phase |
| 2150 | Last Completed Phase |
| 2152 | Working Systems |
| 2154 | Current Blockers |
| 2156 | Important Architecture Decisions |
| 2158 | Known Issues |
| 2160 | Next Task |
| 2162 | Relevant Documents |
| 2167 | 70. AGENTS DOCUMENT |
| 2190 | 71. DOCUMENTATION STRUCTURE |
| 2221 | 72. ARCHITECTURE DECISION RECORDS |
| 2236 | 73. UI COMPONENT STRATEGY |
| 2257 | 74. UI NAVIGATION STATE |
| 2272 | 75. ERROR / EMPTY / LOADING STATES |
| 2290 | 76. EXTERNAL SERVICE FAILURE |
| 2303 | 77. SECURITY / DATA SAFETY |
| 2320 | 78. INITIAL DEVELOPMENT SCOPE EXCLUSIONS |
| 2339 | 79. DESIGN DECISION AUTHORITY |
| 2364 | 80. UI DESIGN AUTHORITY |
| 2388 | 81. ROADMAP QUALITY BAR |
| 2409 | 82. DO NOT PRODUCE PRODUCTION CODE |
| 2431 | 83. MAJOR RISKS SECTION |
| 2459 | 84. UNRESOLVED DECISIONS |
| 2463 | Decisions Requiring Owner Approval |
| 2478 | 85. FINAL OUTPUT REQUIREMENTS |
| 2500 | 86. FINAL PRINCIPLE |
| 2531 | 87. START NOW |

## R — Latest owner revision

Source: [ROADMAP_REVISION_REQUEST.txt](../sources/ROADMAP_REVISION_REQUEST.txt).

| Line | Original heading |
|---:|---|
| 25 | PRIMARY GOAL |
| 51 | IMPORTANT TERMINOLOGY |
| 61 | 1. ADD AN EXPLICIT KBO RULE ENGINE |
| 93 | 2. KEEP THE CURRENT SAVE RECOVERY DESIGN |
| 114 | 3. EXPAND MANAGER AND CLUB PERSONALITY |
| 154 | 4. KEEP PENDING DECISION BACKEND AND ADD VISIBLE UX |
| 180 | 5. ADD CONTEXTUAL HELP / TUTORIAL FRAMEWORK |
| 217 | 6. MOVE BASIC SETTINGS EARLIER |
| 246 | 7. ADD A DEDICATED CATCHER SYSTEM |
| 283 | Phase integration |
| 285 | Phase 1 |
| 293 | Phase 2 |
| 306 | Phase 3 |
| 334 | Phase 7 |
| 340 | Phase 8 |
| 352 | 8. ADD BATTERY CHEMISTRY |
| 379 | Relationship separation |
| 393 | Chemistry growth |
| 407 | Gameplay effect |
| 431 | 9. USE BATTERY CHEMISTRY IN CLUB AI |
| 463 | 10. ADD MID/LATE QoL FEATURES |
| 467 | Favorites / Watchlist |
| 476 | Player Comparison |
| 488 | Game Archive |
| 515 | Milestone Tracking |
| 535 | 11. REINTEGRATE THE PORTRAIT SYSTEM |
| 583 | 12. REVISE THE GENERATIVE AI PHILOSOPHY |
| 605 | Generative AI should NOT own: |
| 625 | Generative AI should be actively used for: |
| 645 | 13. CHANGE INTERVIEWS TO FREE-TEXT AI-NATIVE INTERVIEWS |
| 704 | 14. CONNECT INTERVIEWS TO FAN / SNS / MEDIA REACTION |
| 740 | 15. AI-NATIVE FEATURES DO NOT REQUIRE TEMPLATE EQUIVALENTS |
| 773 | 16. REVISE PHASE 21 |
| 801 | 17. REVISE PHASE 26 |
| 853 | 18. REVISE D5 |
| 887 | 19. PLAYER-FACING UI LANGUAGE |
| 911 | 20. UPDATE SUPPORTING CONTRACTS AND TRACEABILITY |
| 940 | 21. ROADMAP REVISION RULES |
| 986 | 22. REQUIRED CROSS-CHECK |
| 1059 | 23. FINAL OUTPUT |

## P — Independent Portrait design

Source: [Star_Player_Portrait_System_Design.MD](../Star_Player_Portrait_System_Design.MD).

| Line | Original heading |
|---:|---|
| 1 | Star Player Portrait System Design v1.0 |
| 3 | 1. 문서 목적 |
| 18 | 2. 최종 정책 |
| 55 | 3. 핵심 원칙 |
| 57 | 3.1 AI Portrait 자동 생성 금지 |
| 77 | 3.2 사용자가 직접 생성 대상 선택 |
| 98 | 4. Seed 기반 2D Portrait |
| 100 | 4.1 목적 |
| 118 | 5. Portrait Seed |
| 150 | 6. Seed 2D 구성 요소 |
| 154 | 6.1 선수 고유 외형 요소 |
| 184 | 6.2 KBO 구단 요소 |
| 221 | 6.3 국가대표 요소 |
| 291 | 6.4 Portrait Context |
| 303 | club |
| 315 | national_team |
| 328 | neutral |
| 345 | 6.5 동일 선수의 Variant |
| 378 | 7. Portrait Mode |
| 419 | 8. AI Portrait 생성 |
| 441 | 9. Gemini Batch 사용 |
| 465 | 10. API 호출 원칙 |
| 487 | 11. 이미지 캐시 |
| 518 | 12. Seed 데이터 유지 |
| 545 | 13. 데이터 구조 |
| 585 | 14. AI 생성 상태 |
| 620 | 15. 실패 처리 |
| 649 | 16. 선수 DB UI |
| 674 | 17. Portrait 필터 |
| 695 | 18. 개별 선수 프로필 |
| 727 | 19. 일괄 작업 |
| 758 | 20. 비용 확인 UI |
| 791 | 21. 대량 선택 보호 |
| 812 | 22. AI Prompt 생성 |
| 866 | 23. Prompt Version |
| 884 | 24. 동일 선수의 외형 일관성 |
| 908 | 25. 세이브 독립성 |
| 928 | 26. 실제 선수와 생성 선수 |
| 949 | 27. AI Portrait 생성 권한 |
| 966 | 28. 나이 변화 |
| 990 | 29. 설정 화면 |
| 1015 | 30. API Key |
| 1038 | 31. 데이터팩과 Portrait |
| 1061 | 32. Portrait 로딩 우선순위 |
| 1096 | 33. 기본 Fallback |
| 1122 | 34. 구현 단계 |
| 1124 | Phase 1 — Seed 2D |
| 1142 | Phase 2 — Portrait Mode |
| 1157 | Phase 3 — 개별 AI Portrait |
| 1175 | Phase 4 — Gemini Batch |
| 1190 | Phase 5 — UX 개선 |
| 1206 | 35. 최종 구조 |
| 1251 | 36. 최종 설계 원칙 |
| 1284 | 37. 최종 결정 |

