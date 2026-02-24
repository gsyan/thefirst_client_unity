# Claude Code Rules

## 언어
- 모든 답변은 반드시 한국어로 작성할 것

## 출력 스타일
- 불필요한 설명 금지(코드 → 설명 순서 유지)

## 서버 프로젝트(Java Spring)
- 루트: D:\BK\thefirst\thefirst_server 또는 C:\bk\thefirst\thefirst_server

## 코드 생성 도구
- Python generator 경로: 서버경로\tools\generator
- 서버 dto들 모두 클라 메인으로 generate 된 것(서버 코드 수정시 주석에 Auto-generated 발견되면 클라부터 수정)
- py generate 관련 사항은 이 경로의 구조를 기준으로 설명할 것

## 코드 규칙 공통
- 불확실한 내용은 추측하지 말고 모른다고 명시할 것 
- 코드 수정 시 수정 전/후 의도와 이유를 반드시 설명할 것
- if(!isok()) 지양, if(isok() == true) 지향
- 주석 한줄 또는 두줄 제한

## 코딩 규칙 Unity 프로젝트
- Unity 2021+ 기준으로 작성할 것, - C# 코드는 Unity 스타일에 맞출 것
- 항상 성능과 GC Alloc을 고려할 것
- 코딩중 using UnityEditor.ShaderGraph; 를 사용하게되면 android 빌드 실패함
- Update 남용 금지, 이벤트 또는 코루틴 우선 (public static class EventManager 참고할 것)
- 오브젝트 생성, 소멸을 최소화 하기 위해 pool을 사용 ( public class PoolManager 참고할 것)
- Raycast, Physics 사용 시 반드시 성능 비용 및 가능한 대안 설명 포함
- switch 표현식(switch expression) 지양

## 주의 사항
- 모든 파일(generated 파일 제외)엔 최상단에 1~3줄 요약이 있어야 해. 없다면 추가
- 작업 진행 후 최상단 요약 수정 필요가 있다면 수정