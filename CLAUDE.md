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
- 요청 내용이 현재 코드 현황과 맞지 않거나 의도치 않은 결과를 낳을 가능성이 있으면 반드시 먼저 문제를 제기하고 확인 후 진행할 것
- if(!isok()) 지양, if(isok() == true) 지향
- 주석 1~2줄로 제한

## 코딩 규칙 Unity 프로젝트
- Unity 6000.3.12f1+ 기준으로 작성할 것, - C# 코드는 Unity 스타일에 맞출 것
- 항상 성능과 GC Alloc을 고려할 것
- **[주의]코딩중 using UnityEditor.ShaderGraph; 를 사용하게되면 android 빌드 실패함
- Update 남용 금지, 이벤트 또는 코루틴 우선 (public static class EventManager 참고할 것)
- 오브젝트 생성, 소멸을 최소화 하기 위해 pool을 사용 ( public class PoolManager 참고할 것)
- Raycast, Physics 사용 시 반드시 성능 비용 및 가능한 대안 설명 포함
- switch 표현식(switch expression) 지양

## 프로젝트별 MD 참고 및 수정
- 전체 기획 : 클라 프로젝트 루트/claude_md_files/game_design/
    [overview.md] : 게임 개요, 재화 시스템 (4종 자원, 소모처 구조)
    [combat.md] : 전투 배속 시스템 (GameSpeedController, 피치 연동)
    [tab_fleet.md] : Fleet 탭 UI, 함선 시스템, 기술레벨 시스템, 자원 적립 캡
    [tab_ship.md] : Ship 탭 UI, 모듈 시스템 (SubType 인코딩, 레벨업, 서브타입 추가)
    [tab_exploration.md] : Exploration 탭 UI, 존 탐험 시스템, 존 보상 수치, 광고 입장 정책
    [tab_pvp.md] : PVP 탭 UI (점수/순위, 대전 상대 카드, Refresh)
    [tab_settings.md] : Settings 탭 UI (계정 관리, 언어, 라이센스, 개발자 도구)
    
- 서버 구조 및 운영 : 서버 프로젝트 루트/claude_md_files/server_structure_and_operation.md
- 클라 구조 및 운영 : 클라 프로젝트 루트/claude_md_files/client_structure_and_operation.md
- 클라 사용 asset store list : 클라 프로젝트 루트/claude_md_files/asset_store_list.md
- 젠킨스 설정 : 클라 프로젝트 루트/claude_md_files/jenkins-setup-tutorial.md
- **[작업중] 진형 이동 시스템 재설계** : 클라 프로젝트 루트/claude_md_files/formation_movement_redesign.md
- **[작업중] 렌더링 최적화** : 클라 프로젝트 루트/claude_md_files/rendering_optimization.md

## 주의 사항
- `DataTableZone`, `DataTableModule` 등은 서버와 반드시 동기화
- 외부 라이센스 표시는 아주 중요한 일, UIPopupLicense 으로 하고 있으니 추가할 것들이 생기면 실시간으로 추가
