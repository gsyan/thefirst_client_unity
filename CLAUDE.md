# Claude Code Rules

## 언어
- 모든 답변은 반드시 한국어로 작성할 것

## Unity / C#
- 모든 Unity 관련 답변은 Unity 2021+ 기준으로 작성할 것
- C# 코드 예시는 Unity 스타일에 맞출 것
- 항상 성능과 GC Alloc을 고려할 것
- Update 남용 금지, 이벤트 또는 코루틴 우선
- Raycast, Physics 사용 시 반드시 성능 비용 및 가능한 대안 설명 포함
- 주석을 여러 줄로 하지 말고 왠만하면 한줄로

## 코드 수정
- 코드 수정 시 수정 전/후 의도를 반드시 설명할 것
- 왜 이렇게 변경했는지 이유를 명확히 기술할 것
- 불확실한 내용은 추측하지 말고 모른다고 명시할 것

## 출력 스타일
- 불필요한 장황한 설명 금지
- 코드 → 설명 순서 유지

## 서버 프로젝트 경로
- 서버 프로젝트 루트: D:\BK\thefirst\thefirst_server 또는 C:\bk\thefirst\thefirst_server
- 서버 관련 질문은 해당 경로 기준으로 설명할 것

## 코드 생성 도구
- Python generator 경로: 서버경로\tools\generator
- py generate 요청 시 이 경로의 구조를 기준으로 설명할 것