# Star Player Portrait System Design v1.0

## 1. 문서 목적

이 문서는 `Star Player`의 생성 선수 프로필 이미지 시스템 구현 방향을 정의한다.

핵심 목표는 다음과 같다.

- 모든 생성 선수에게 기본 프로필 이미지를 제공한다.
- 기본 프로필 생성에는 외부 API를 사용하지 않는다.
- 사용자가 원하는 선수만 선택적으로 고품질 AI Portrait로 변경할 수 있도록 한다.
- AI 이미지 생성 비용을 사용자가 직접 통제할 수 있도록 한다.
- 한번 생성한 AI 이미지는 로컬에 저장하여 반복 API 호출을 방지한다.
- AI Portrait를 사용하더라도 기존 Seed 기반 Portrait 데이터를 유지한다.

---

# 2. 최종 정책

Portrait 시스템은 다음 두 가지 방식을 사용한다.

```text
1. Seed 기반 2D Portrait
2. Gemini AI Portrait
```

기본 Portrait는 항상 `Seed 기반 2D Portrait`를 사용한다.

Gemini AI Portrait는 자동으로 생성하지 않는다.

사용자가 직접 원하는 선수를 선택한 뒤 AI Portrait 생성을 요청할 때만 Gemini API를 사용한다.

```text
모든 생성 선수
↓
Seed 기반 2D Portrait 생성
↓
기본 프로필로 사용

사용자가 원하는 선수 선택
↓
1명 또는 여러 명 선택
↓
Gemini Batch 요청
↓
AI Portrait 생성
↓
로컬 저장
↓
선택한 선수만 AI Portrait 사용
```

---

# 3. 핵심 원칙

## 3.1 AI Portrait 자동 생성 금지

다음과 같은 상황에서도 AI Portrait를 자동으로 생성하지 않는다.

```text
1군 승격
MVP 수상
올스타 선정
국가대표 선발
신인왕 수상
FA 계약
스타 선수 성장
```

선수의 성적이나 상태와 관계없이 기본적으로 Seed 2D Portrait를 유지한다.

AI Portrait 생성 여부는 전적으로 사용자가 결정한다.

---

## 3.2 사용자가 직접 생성 대상 선택

AI Portrait 생성 방식은 수동 선택을 기본으로 한다.

예:

```text
□ 김민수   두산   SS   Seed 2D
☑ 박준혁   LG     SP   Seed 2D
☑ 이현우   한화   CF   Seed 2D
□ 최민재   삼성   RP   AI

선택 선수: 2명

[AI 프로필 일괄 생성]
```

1명만 선택해서 생성할 수도 있고 여러 명을 선택하여 한 번에 생성할 수도 있다.

---

# 4. Seed 기반 2D Portrait

## 4.1 목적

Seed 2D Portrait는 모든 생성 선수에게 기본적으로 제공되는 프로필 이미지다.

외부 API를 사용하지 않는다.

따라서:

- API 비용 없음
- 인터넷 연결 불필요
- 빠른 생성
- 수천 명의 선수를 생성해도 비용 증가 없음
- 동일한 Seed에서 항상 동일한 얼굴 재생성 가능

이라는 장점을 가진다.

---

# 5. Portrait Seed

모든 생성 선수는 고유한 `portrait_seed`를 가진다.

예:

```text
player_id      = P2035_00521
portrait_seed  = 583921
```

Seed를 기준으로 선수의 외형 파츠를 결정한다.

개념 예:

```text
portrait_seed = 583921

얼굴형    04
피부톤    02
눈        11
눈썹      07
코        03
입        08
헤어      15
수염      00
```

동일한 Seed를 사용하면 항상 동일한 얼굴 조합을 생성한다.

---

# 6. Seed 2D 구성 요소

Seed 2D Portrait는 크게 **선수 고유 외형 요소**와 **소속/상황에 따라 변경되는 표시 요소**로 분리한다.

## 6.1 선수 고유 외형 요소

기본 구성 요소:

```text
얼굴형
피부톤
눈
눈썹
코
입
헤어스타일
수염
```

필요할 경우 추가 가능한 요소:

```text
귀
점 / 주근깨
안경
스포츠 고글
헤어밴드
기타 외형 액세서리
```

이 요소들은 기본적으로 `portrait_seed`를 기준으로 결정하며, 선수가 팀을 이동하거나 국가대표에 선발되더라도 동일하게 유지한다.

---

## 6.2 KBO 구단 요소

소속 KBO 구단에 따라 변경 가능한 요소:

```text
구단 모자
타격 헬멧
유니폼
구단 로고
팀 컬러
구단 배경
유니폼 번호 표현
```

선수가 다른 KBO 구단으로 이적하면 얼굴은 유지하고 구단 관련 요소만 변경한다.

예:

```text
두산 소속
↓
두산 모자
두산 유니폼
두산 배경

FA 이적
↓
LG 모자
LG 유니폼
LG 배경

portrait_seed 유지
얼굴 유지
```

---

## 6.3 국가대표 요소

선수가 대한민국 국가대표에 선발된 경우 Seed 2D Portrait에서 별도의 **국가대표 표시 세트**를 사용할 수 있다.

국가대표 구성 요소:

```text
대한민국 대표팀 모자
대한민국 대표팀 타격 헬멧
대한민국 대표팀 유니폼
대한민국 대표팀 로고
태극기 요소
대표팀 컬러
국가대표 전용 배경
국제대회 패치
대회 로고 또는 대회 표시
```

지원 가능한 국제대회 예:

```text
WBC
올림픽
아시안게임
Premier12
기타 국가대표 국제대회
```

국가대표 차출 시 선수의 얼굴 자체를 새로 생성하지 않는다.

```text
KBO Portrait
↓
동일 portrait_seed
↓
대한민국 국가대표 표시 요소 적용
```

예:

```text
김OO
두산 소속

평상시 Portrait
두산 모자
두산 유니폼
두산 배경

↓

대한민국 국가대표 선발

국가대표 Portrait
대한민국 대표팀 모자
대한민국 대표팀 유니폼
태극기 / 대표팀 배경
WBC 패치

↓

대회 종료

다시 두산 Portrait 표시
```

즉 국가대표 Portrait는 새로운 선수 얼굴이 아니라 기존 선수 Portrait의 **표시 Variant**로 취급한다.

---

## 6.4 Portrait Context

Seed 2D Portrait는 현재 표시 상황을 별도의 Context로 구분할 수 있다.

예:

```text
club
national_team
neutral
```

### club

현재 KBO 소속팀을 기준으로 표시한다.

```text
모자
유니폼
구단 로고
팀 컬러
구단 배경
```

### national_team

국가대표팀을 기준으로 표시한다.

```text
대표팀 모자 / 헬멧
대표팀 유니폼
대표팀 로고
태극기
국제대회 패치
대표팀 배경
```

### neutral

특정 구단이나 대표팀 요소를 제거한 기본 Portrait다.

활용 예:

```text
드래프트 전 신인
FA 신분
은퇴 선수
출신 학교 프로필
명예의 전당
기타 중립 프로필
```

---

## 6.5 동일 선수의 Variant

한 선수는 동일한 얼굴 Seed를 유지하면서 여러 표시 Variant를 가질 수 있다.

예:

```text
portrait_seed = 583921

├─ neutral
│   └─ 기본 복장
│
├─ club
│   └─ 두산 유니폼
│
└─ national_team
    └─ 대한민국 유니폼
```

얼굴 데이터는 하나만 유지한다.

```text
Face Data
= portrait_seed

Presentation Data
= team / national team / context
```

이렇게 분리하여 소속 변경이나 국가대표 차출 시 불필요하게 새로운 얼굴을 생성하지 않는다.

---

# 7. Portrait Mode

선수는 현재 어떤 Portrait 방식을 사용하는지 상태를 가진다.

기본 값:

```text
portrait_mode = seed2d
```

AI Portrait 생성 후:

```text
portrait_mode = ai
```

지원 상태:

```text
seed2d
ai
```

`portrait_mode`와 `portrait_context`는 별개로 취급한다.

예:

```text
portrait_mode = seed2d
portrait_context = national_team
```

또는:

```text
portrait_mode = ai
portrait_context = club
```

---

# 8. AI Portrait 생성

AI Portrait는 Gemini Image API를 사용한다.

기본 방식:

```text
선수 선택
↓
AI Portrait 생성 요청
↓
Gemini Batch
↓
이미지 생성
↓
게임 로컬 저장
↓
Portrait Mode 변경
```

---

# 9. Gemini Batch 사용

여러 선수의 Portrait를 생성할 경우 가능한 한 Gemini Batch 방식을 사용한다.

예:

```text
선수 73명 선택
↓
Batch Request 생성
↓
Gemini Batch 실행
↓
73개의 결과 이미지 수신
↓
player_id와 이미지 매칭
↓
WebP 저장
```

실시간 생성보다 Batch 처리를 우선하여 비용을 줄인다.

---

# 10. API 호출 원칙

AI Portrait는 최초 생성 또는 사용자가 명시적으로 재생성을 요청할 때만 API를 호출한다.

다음 상황에서는 API를 호출하지 않는다.

```text
선수 프로필 열기
선수 DB 검색
경기 출전
뉴스 등장
올스타 선정
국가대표 선정
시즌 진행
세이브 로드
게임 재시작
```

AI Portrait가 이미 존재한다면 저장된 이미지 파일을 읽는다.

---

# 11. 이미지 캐시

Gemini에서 생성한 Portrait는 로컬에 영구 저장한다.

예:

```text
UserData/
└─ Saves/
   └─ save_001/
      └─ portraits/
         ├─ ai/
         │  ├─ P2035_00521.webp
         │  ├─ P2035_00672.webp
         │  └─ P2036_00018.webp
         │
         └─ generated/
```

AI Portrait의 기본 저장 형식은 `WebP`를 권장한다.

파일명에는 `player_id`를 사용한다.

```text
P2035_00521.webp
```

선수 이름을 파일명으로 사용하지 않는다.

---

# 12. Seed 데이터 유지

AI Portrait를 생성하더라도 기존 `portrait_seed`는 삭제하지 않는다.

예:

```text
player_id       = P2035_00521
portrait_seed   = 583921
portrait_mode   = ai
portrait_path   = portraits/ai/P2035_00521.webp
```

따라서 언제든지:

```text
AI Portrait
↓
Seed 2D Portrait
```

로 복귀할 수 있다.

AI 이미지를 삭제하더라도 선수의 기본 얼굴 정보는 유지된다.

---

# 13. 데이터 구조

Portrait 관련 데이터는 다음과 같이 관리한다.

```text
player_id
portrait_seed
portrait_mode
portrait_path

ai_status
ai_model
ai_prompt_version
ai_generated_at
```

필요할 경우 UI 표시용 Context는 런타임에서 결정한다.

```text
portrait_context
= club | national_team | neutral
```

예:

```json
{
  "player_id": "P2035_00521",
  "portrait_seed": 583921,
  "portrait_mode": "ai",
  "portrait_path": "portraits/ai/P2035_00521.webp",
  "ai_status": "done",
  "ai_model": "gemini-image",
  "ai_prompt_version": "v1",
  "ai_generated_at": "2035-07-03T14:20:00"
}
```

---

# 14. AI 생성 상태

AI Portrait의 처리 상태는 별도로 관리한다.

```text
none
queued
processing
done
failed
```

의미:

```text
none
AI 생성 요청 없음

queued
Batch 대기 중

processing
생성 작업 진행 중

done
생성 완료

failed
생성 실패
```

이를 통해 Batch 작업 일부가 실패하더라도 전체 작업을 다시 실행할 필요가 없도록 한다.

---

# 15. 실패 처리

예:

```text
50명 Batch 요청

47명 성공
3명 실패
```

결과:

```text
47명
ai_status = done

3명
ai_status = failed
```

실패한 선수만 다시 선택해서 재시도할 수 있도록 한다.

```text
[실패한 Portrait 재시도]
```

---

# 16. 선수 DB UI

선수 DB에 Portrait 관련 선택 기능을 제공한다.

예:

```text
☐ 이름      팀      POS    Portrait
──────────────────────────────
☐ 김민수    두산    SS     Seed 2D
☑ 박준혁    LG      SP     Seed 2D
☑ 이현우    한화    CF     Seed 2D
☐ 최민재    삼성    RP     AI
```

하단 또는 상단:

```text
선택 선수: 2명

[AI 프로필 일괄 생성]
```

---

# 17. Portrait 필터

필요할 경우 다음 필터를 지원할 수 있다.

```text
전체
Seed 2D
AI Portrait
AI 생성 실패
```

예:

```text
Portrait 상태 [Seed 2D ▼]
```

이를 통해 아직 AI Portrait가 없는 선수만 쉽게 선택할 수 있다.

---

# 18. 개별 선수 프로필

Seed 2D를 사용하는 선수:

```text
현재 Portrait
Seed 2D

[AI 프로필 생성]
```

AI Portrait를 사용하는 선수:

```text
현재 Portrait
AI

[AI 프로필 재생성]
[Seed 2D로 변경]
```

국가대표 활동 중에는 필요에 따라:

```text
[소속팀 Portrait]
[국가대표 Portrait]
```

전환도 지원할 수 있다.

---

# 19. 일괄 작업

지원할 기능:

```text
AI Portrait 일괄 생성
AI Portrait 재생성
Seed 2D로 일괄 변경
```

AI 이미지 삭제 기능은 별도로 제공할 수 있다.

예:

```text
[AI 이미지 삭제]
```

삭제 시:

```text
portrait_mode = seed2d
portrait_path = null
```

로 돌아간다.

`portrait_seed`는 유지한다.

---

# 20. 비용 확인 UI

Gemini Batch 요청을 실행하기 전에 사용자에게 예상 작업량을 표시한다.

예:

```text
AI 프로필 일괄 생성

선택 선수
73명

모델
Gemini Image

예상 생성 이미지
73장

예상 API 비용
약 $X.XX

────────────────

[취소]
[Batch 생성]
```

API 가격은 변경될 수 있으므로 게임 코드에 특정 금액을 영구 하드코딩하는 것은 피한다.

가능하면 설정값 또는 별도 구성 데이터로 관리한다.

---

# 21. 대량 선택 보호

실수로 수백 또는 수천 명에게 API 요청을 보내는 상황을 방지한다.

예:

```text
선택 선수: 1,243명

많은 수의 AI Portrait가 생성됩니다.

예상 비용을 확인해주세요.

[취소]
[계속]
```

일정 인원 이상에서는 추가 확인 창을 표시한다.

---

# 22. AI Prompt 생성

AI Prompt는 선수가 가지고 있는 게임 데이터를 바탕으로 자동 작성한다.

사용 가능 정보 예:

```text
성별
국적
나이
포지션
신장
체중
체형
헤어 스타일
얼굴 특징
소속팀
유니폼
국가대표 여부
현재 국제대회
```

KBO 프로필 예:

```text
Korean professional baseball player,
22 years old,
shortstop,
athletic build,
short black hair,
official roster portrait,
front facing,
neutral expression,
studio lighting,
baseball uniform
```

국가대표 프로필을 별도로 생성할 경우:

```text
Korean national baseball team player,
22 years old,
shortstop,
athletic build,
official national team roster portrait,
South Korea baseball uniform,
front facing,
neutral expression
```

사용자에게 실제 Prompt를 직접 입력하게 하는 기능은 필수 기능으로 두지 않는다.

---

# 23. Prompt Version

AI Portrait 생성 규칙이 변경될 가능성을 고려하여 `ai_prompt_version`을 저장한다.

예:

```text
v1
v2
v3
```

기존 Portrait는 Prompt가 변경되더라도 자동 재생성하지 않는다.

사용자가 명시적으로 재생성을 요청한 경우에만 새로운 Prompt Version을 사용한다.

---

# 24. 동일 선수의 외형 일관성

AI Portrait는 가능하면 Seed 2D의 외형 데이터를 참고하여 Prompt를 생성한다.

예:

```text
portrait_seed
↓
외형 데이터 해석

피부톤
얼굴형
헤어스타일
눈 형태
수염 여부
↓
Gemini Prompt
```

이를 통해 Seed 2D와 AI Portrait 사이에 완전히 다른 사람이 나타나는 현상을 줄인다.

---

# 25. 세이브 독립성

AI Portrait는 해당 세이브의 선수와 연결한다.

예:

```text
Save A
P2035_00123
→ Portrait A

Save B
P2035_00123
→ Portrait B
```

각 세이브의 생성 선수는 독립적인 게임 세계에 존재하므로 Portrait 역시 세이브 단위로 관리하는 것을 기본으로 한다.

---

# 26. 실제 선수와 생성 선수

초기 목적은 자동 생성된 가상 선수에게 Portrait를 제공하는 것이다.

실제 KBO 선수 데이터팩에서 제공되는 실제 선수 사진이 존재한다면 해당 사진을 우선 사용할 수 있다.

개념:

```text
실제 선수
→ 데이터팩 사진

생성 선수
→ Seed 2D

사용자 선택
→ Gemini AI Portrait
```

---

# 27. AI Portrait 생성 권한

게임 시스템은 다음 행동을 자동으로 수행하지 않는다.

```text
스타 선수를 찾아 자동 생성
전체 1군 선수 자동 생성
신인 전체 자동 생성
국가대표 선발 선수 자동 생성
매 시즌 AI Portrait 자동 갱신
선수 나이 변화에 따른 자동 재생성
```

AI Portrait 생성은 항상 사용자의 직접적인 명령으로만 수행한다.

---

# 28. 나이 변화

초기 버전에서는 나이에 따라 AI Portrait를 자동 재생성하지 않는다.

예:

```text
19세에 AI Portrait 생성
↓
30세가 되어도 기존 이미지 유지
```

사용자가 원할 경우 직접:

```text
[AI 프로필 재생성]
```

을 선택한다.

향후 필요하면 나이 변화 Portrait 기능을 확장할 수 있다.

---

# 29. 설정 화면

향후 설정 예:

```text
Portrait 설정

기본 Portrait
● Seed 2D

AI Portrait Provider
Gemini

AI 이미지 해상도
1024 × 1024

AI 이미지 형식
WebP

Batch 사용
ON
```

---

# 30. API Key

Gemini API Key는 세이브 데이터에 저장하지 않는다.

```text
Save File
→ API Key 저장 금지
```

API Key는 별도의 사용자 설정 또는 안전한 로컬 설정 방식으로 관리한다.

Git 저장소에도 포함하지 않는다.

```text
.env
user_settings
local_config
```

등의 별도 저장 영역을 사용한다.

---

# 31. 데이터팩과 Portrait

Portrait 시스템은 선수 데이터 자체와 분리한다.

```text
Player Data
↓
player_id
portrait_seed

Portrait System
↓
Seed 2D 또는 AI

UI
↓
최종 Portrait 표시
```

따라서 Portrait 이미지가 없어도 게임의 선수 데이터와 시뮬레이션은 정상적으로 동작해야 한다.

---

# 32. Portrait 로딩 우선순위

선수 프로필을 표시할 때 다음 순서로 처리한다.

```text
portrait_mode == ai
+
AI 이미지 존재
↓
AI Portrait 표시
```

AI 이미지가 없거나 손상된 경우:

```text
Seed 2D Portrait 표시
```

Seed 2D를 사용할 경우 현재 Context에 따라:

```text
KBO 소속
→ Club Variant

국가대표 화면
→ National Team Variant

중립 화면
→ Neutral Variant
```

를 선택한다.

---

# 33. 기본 Fallback

항상 Seed 2D가 최종 Fallback 역할을 한다.

```text
AI 파일 삭제
API 오류
Batch 실패
이미지 손상
파일 경로 오류

↓ 모두

Seed 2D 표시
```

Seed 2D 내부에서도 구단 또는 국가대표 Asset을 찾지 못한 경우:

```text
Neutral Portrait
```

로 표시한다.

---

# 34. 구현 단계

## Phase 1 — Seed 2D

먼저 다음 기능을 구현한다.

```text
portrait_seed 생성
Seed 기반 외형 결정
2D 파츠 조합
KBO 구단 Variant
국가대표 Variant
선수 프로필 표시
Seed 재현성 테스트
```

Gemini API는 이 단계에서 필요하지 않다.

---

## Phase 2 — Portrait Mode

추가:

```text
portrait_mode
portrait_path
portrait_context
AI 이미지 표시
Seed / AI 전환
Fallback
```

---

## Phase 3 — 개별 AI Portrait

먼저 단일 선수 대상으로 Gemini 연동을 검증한다.

```text
선수 1명 선택
↓
Prompt 생성
↓
Gemini 요청
↓
이미지 저장
↓
프로필 표시
```

---

## Phase 4 — Gemini Batch

추가:

```text
다중 선수 선택
Batch 요청 생성
처리 상태 관리
결과 매칭
이미지 저장
실패 처리
```

---

## Phase 5 — UX 개선

추가:

```text
예상 비용
일괄 선택
Portrait 필터
실패 재시도
일괄 Seed 복원
AI 재생성
구단 / 국가대표 Portrait 전환
```

---

# 35. 최종 구조

```text
                Player 생성
                     │
                     ▼
              portrait_seed
                     │
                     ▼
             Seed 2D Portrait
                     │
          ┌──────────┼──────────┐
          │          │          │
        Club      National    Neutral
       Variant     Variant     Variant
          │          │          │
          └──────────┼──────────┘
                     ▼
                기본 사용
                     │
          사용자가 AI 생성 선택
                     │
             ┌───────┴───────┐
             │               │
           1명             여러 명
             │               │
             └───────┬───────┘
                     ▼
               Gemini Batch
                     │
                     ▼
               AI Portrait
                     │
                     ▼
                WebP 저장
                     │
                     ▼
            portrait_mode = ai
                     │
                     ▼
                 게임 사용
```

---

# 36. 최종 설계 원칙

1. 모든 생성 선수의 기본 Portrait는 Seed 기반 2D로 생성한다.
2. Seed 2D 생성에는 외부 API를 사용하지 않는다.
3. 모든 생성 선수는 고유한 `portrait_seed`를 가진다.
4. 같은 Seed는 항상 같은 기본 얼굴을 생성해야 한다.
5. 얼굴 외형과 소속팀 표시 요소는 분리한다.
6. KBO 구단 변경 시 얼굴은 유지하고 모자, 유니폼, 로고, 배경 등을 변경한다.
7. 국가대표 선발 시에도 동일한 얼굴 Seed를 유지한다.
8. 국가대표 Portrait는 대표팀 모자, 헬멧, 유니폼, 로고, 태극기, 국제대회 패치 등을 사용할 수 있다.
9. 국가대표 Portrait는 새로운 선수를 생성하는 것이 아니라 동일 선수의 표시 Variant다.
10. `club`, `national_team`, `neutral` Context를 지원할 수 있다.
11. AI Portrait는 Gemini API를 사용한다.
12. AI Portrait를 게임이 자동 생성하지 않는다.
13. 사용자가 직접 선택한 선수만 AI Portrait를 생성한다.
14. 여러 선수는 Gemini Batch를 이용해 일괄 생성할 수 있다.
15. Batch 실행 전에 대상 인원과 예상 비용을 확인할 수 있도록 한다.
16. AI Portrait는 생성 후 로컬 파일로 저장한다.
17. 저장된 Portrait를 다시 볼 때 API를 재호출하지 않는다.
18. AI Portrait가 생성되어도 `portrait_seed`를 유지한다.
19. 사용자는 언제든 Seed 2D로 돌아갈 수 있다.
20. AI 재생성은 사용자가 직접 요청할 때만 수행한다.
21. 국가대표 선발 자체로 AI Portrait를 자동 생성하지 않는다.
22. AI 생성 실패는 선수별 상태로 기록한다.
23. 실패한 선수만 별도로 재시도할 수 있도록 한다.
24. AI 이미지가 존재하지 않거나 손상된 경우 Seed 2D를 Fallback으로 사용한다.
25. 구단 또는 국가대표 Asset이 없으면 Neutral Portrait를 Fallback으로 사용한다.
26. API Key는 세이브 또는 Git 저장소에 포함하지 않는다.
27. Portrait 시스템은 게임 핵심 데이터와 분리한다.
28. Portrait 시스템 장애가 경기, 시즌, 선수 성장 등 핵심 시뮬레이션에 영향을 주지 않도록 한다.

---

# 37. 최종 결정

```text
기본 Portrait
= Seed 기반 2D

Seed 2D Variant
= KBO 구단 / 국가대표 / 중립

국가대표 요소
= 대표팀 모자
+ 대표팀 헬멧
+ 대표팀 유니폼
+ 대표팀 로고
+ 태극기
+ 대표팀 배경
+ 국제대회 패치

AI Portrait
= 사용자 선택 기능

AI Provider
= Gemini API

대량 생성
= Gemini Batch

자동 AI 생성
= 사용하지 않음

AI 이미지 저장
= 로컬 WebP 캐시

AI Portrait 삭제 / 오류
= Seed 2D Fallback

API 비용 통제
= 사용자가 직접 대상 선수 선택
```

Star Player에서는 `Seed 2D`를 정식 기본 Portrait 시스템으로 사용하고, 소속 KBO 구단과 대한민국 국가대표 상태에 따라 표시 Variant를 변경한다.

`Gemini AI Portrait`는 사용자가 원하는 선수에게 선택적으로 적용할 수 있는 고품질 확장 기능으로 취급한다.