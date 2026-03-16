# Claude Code Rules

## 언어
- 모든 답변은 반드시 한국어로 작성할 것

## 출력 스타일
- 불필요한 설명 금지(코드 → 설명 순서 유지)

## 서버 프로젝트(Java Spring)
- 루트: D:\BK\thefirst\thefirst_server 또는 C:\bk\thefirst\thefirst_server

## 코드 생성 도구
- Python generator 경로: 서버경로\tools\generator
- **[금지] 서버 dto 파일을 직접 생성/수정하지 말 것** - 반드시 클라 C# DTO 수정 후 generator로 생성
- py generate 관련 사항은 이 경로의 구조를 기준으로 설명할 것

## 코드 규칙 공통
- 불확실한 내용은 추측 및 추론하지 말고 반드시 실제 호출 체인(caller)을 Grep으로 추적하는 등 확인한 후 답할 것
- 코드 수정 시 수정 전/후 의도와 이유를 반드시 설명할 것
- if(!isok()) 지양, if(isok() == true) 지향
- 주석 1~2줄로 제한

## 코딩 규칙 Unity 프로젝트
- Unity 2021+ 기준으로 작성할 것, - C# 코드는 Unity 스타일에 맞출 것
- 항상 성능과 GC Alloc을 고려할 것
- **[주의]코딩중 using UnityEditor.ShaderGraph; 를 사용하게되면 android 빌드 실패함
- Update 남용 금지, 이벤트 또는 코루틴 우선 (public static class EventManager 참고할 것)
- 오브젝트 생성, 소멸을 최소화 하기 위해 pool을 사용 ( public class PoolManager 참고할 것)
- Raycast, Physics 사용 시 반드시 성능 비용 및 가능한 대안 설명 포함
- switch 표현식(switch expression) 지양

## 프로젝트별 MD 참고 및 수정
- 전체 기획 : 클라 프로젝트 루트/claude_md_files/game_design.md
- 서버 구조 및 운영 : 서버 프로젝트 루트/claude_md_files/server_structure_and_operation.md
- 클라 구조 및 운영 : 클라 프로젝트 루트/claude_md_files/client_structure_and_operation.md
- 젠킨스 설정 : 클라 프로젝트 루트/claude_md_files/jenkins-setup-tutorial.md

## 주의 사항
- 모든 파일(generated 파일 제외)엔 최상단에 1~3줄 요약이 있어야 해. 없다면 추가
- 작업 진행 후 최상단 요약 수정 필요가 있다면 수정
- 외부 라이센스 표시는 아주 중요한 일, UIPopupLicense 으로 하고 있으니 추가할 것들이 생기면 실시간으로 추가

## 주의 사항 잠정적 유예 ( 현재 지키지 않아도 되는 것, 향후 복구를 위해 남겨둔 것)
- ~~localization 을 위한 csv 수정, 생성은 직접 하지 말고, 이용자에게 부탁할 것~~ → 잠정 유예: csv 직접 수정 허용