# 클라이언트 구조 및 작동 설명
# Unity 프로젝트의 씬 구성, 시스템 계층, 주요 매니저 역할 및 UI 구조 정리

---

## 씬 구성
| 씬 | 역할 |
|----|------|
| `LoadingScene` | 앱 시작 시 초기 로딩, 자동 로그인 시도 |
| `MainScene` | 로그인 / 캐릭터 선택 / 메인 로비 (UIMain) |
| `SpaceScene` | 게임 플레이 (함대 관리, 탐사, PvP, 랭킹 등) (UISpace) |

## 시스템 매니저 계층
MonoSingleton<T> / Singleton<T>
    ├── NetworkManager      API 호출, JWT 토큰 관리, 하트비트
    ├── LoadingManager      씬 전환 및 로딩 화면 제어
    ├── AdManager           광고 (배너/전면/리워드) 제어
    ├── PoolManager         오브젝트 풀 관리
    └── ObjectManager       풀 외 동적 오브젝트 관리

UIManager (MonoBehaviour)
    ├── UIMain              MainScene 전용 UI 매니저
    └── UISpace             SpaceScene 전용 UI 매니저

## 주요 매니저 역할
### NetworkManager (`System/Network/NetworkManager.cs`)
- 서버 API 호출 창구 (ApiClient 래핑)
- 401 응답 시 RefreshToken으로 자동 재발급 (중복 요청 방지용 `m_pendingRefreshTask` 공유)
- SpaceScene 진입 시 30초 주기 하트비트 (`/api/zone/heartbeat`) 자동 발송
- 씬 전환 시 `OnChangeScene()` 호출 필요
### ApiClient (`System/Network/ApiClient.cs`)
- 실제 HTTP 요청 처리 (UnityWebRequest 기반)
- RefreshToken을 PlayerPrefs에 저장/로드
### EventManager (`System/Events/EventManager.cs`)
- static 이벤트 허브
- 씬 전환 시 `UnsubscribeAll()` 로 전체 구독 해제 (리플렉션 자동 처리)
- Update 대신 이벤트 기반 통신에 사용
### PoolManager / ObjectPool (`System/Object/`)
- 오브젝트 생성·소멸 최소화용 풀
- 새 오브젝트 필요 시 `new` 대신 풀에서 꺼내 사용
### DataManager + DataTable (`System/Data/`)
- 서버와 공유하는 게임 데이터 JSON 로드
- `DataTableConfig`, `DataTableModule`, `DataTableResearch`, `DataTableZone`
- **서버와 동기화 필요** → generator 툴 (`서버/tools/generator`) 사용

## UI 구조
### UIMain (MainScene)
- `Prefabs/UI/Panel_Main/` 에서 패널 프리팹 동적 로드
- 주요 패널: `UIPanelFirst`, `UIPanelLoginType`, `UIPanelLogin`, `UIPanelLoginEmail`
### UISpace (SpaceScene)
- 주요 패널: `UIPanelSpace`, `UIPanelCameraView`
- 탭 시스템: `UITabFleet`, `UITabExploration`, `UITabPvp`, `UITabResearch`, `UITabShip`, `UITabSettings`
### UIPopup 계층
- `UIPopupBase` 상속
- 주요 팝업: `UIPopupAlert`, `UIPopupConfirm`, `UIPopupFormation`, `UIPopupRanking`, `UIPopupRenameCharacter`, `UIPopupModuleSubTypeManage`
- 외부 라이센스 표시: `UIPopupLicense`

## 네트워크 통신 흐름
UI 이벤트 발생
    ↓
NetworkManager.XXX() 호출
    ↓
ApiClient.PostAsync() / GetAsync()
    ↓
401 응답 → RefreshToken 재발급 → 원 요청 재시도 (1회)
    ↓
성공: DTO 파싱 → 콜백 또는 EventManager.Trigger()
실패: UIPopupAlert 표시 (ServerErrorCode 기반)

## DTO 구조
- `System/Network/NetworkDTOs.cs` — 모든 요청/응답 DTO 정의
- **직접 수정 금지** → 클라 C# DTO 수정 후 generator로 서버 Java DTO 생성

## 씬 전환 규칙
| 전환 | 트리거 |
|------|--------|
| LoadingScene → MainScene | 자동 로그인 완료 또는 실패 |
| MainScene → SpaceScene | 캐릭터 선택 완료 (JWT에 characterId 포함 후) |
| SpaceScene → MainScene | 로그아웃 / 캐릭터 변경 |
씬 전환 시 반드시:
1. `EventManager.UnsubscribeAll()` 호출
2. `NetworkManager.Instance.OnChangeScene()` 호출
