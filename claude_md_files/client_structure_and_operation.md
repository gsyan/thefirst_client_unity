# 클라이언트 구조 및 작동 설명
# Unity 프로젝트의 씬 구성, 시스템 계층, 주요 매니저 역할 및 UI 구조 정리

---

## 씬 구성

| 씬 | 역할 |
|----|------|
| `LoadingScene` | 앱 시작 시 초기 로딩, 자동 로그인 시도 |
| `MainScene` | 로그인 / 캐릭터 선택 / 메인 로비 (UIMain) |
| `SpaceScene` | 게임 플레이 (함대 관리, 탐사, PvP, 랭킹 등) (UISpace) |
| `Test` | 개발용 테스트 씬 |

---

## 시스템 매니저 계층

```
MonoSingleton<T> / Singleton<T>
    ├── NetworkManager      API 호출, JWT 토큰 관리, 하트비트
    ├── LoadingManager      씬 전환 및 로딩 화면 제어
    ├── AdManager           광고 (배너/전면/리워드) 제어
    ├── PoolManager         오브젝트 풀 관리
    └── ObjectManager       풀 외 동적 오브젝트 관리

UIManager (MonoBehaviour)
    ├── UIMain              MainScene 전용 UI 매니저
    └── UISpace             SpaceScene 전용 UI 매니저
```

---

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

---

## UI 구조

### UIMain (MainScene)
- `Prefabs/UI/Panel_Main/` 에서 패널 프리팹 동적 로드
- 주요 패널: `UIPanelFirst`, `UIPanelLoginType`, `UIPanelLogin`, `UIPanelLoginEmail`

### UISpace (SpaceScene)
- 주요 패널: `UIPanelSpace`, `UIPanelMineral`, `UIPanelCameraView`
- 탭 시스템: `UITabFleet`, `UITabExploration`, `UITabPvp`, `UITabResearch`, `UITabShip`, `UITabSettings`

### UIPopup 계층
- `UIPopupBase` 상속
- 주요 팝업: `UIPopupAlert`, `UIPopupConfirm`, `UIPopupFormation`, `UIPopupRanking`, `UIPopupRenameCharacter`, `UIPopupModuleSubTypeManage`
- 외부 라이센스 표시: `UIPopupLicense`

---

## 네트워크 통신 흐름

```
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
```

---

## DTO 구조

- `System/Network/NetworkDTOs.cs` — 모든 요청/응답 DTO 정의
- **직접 수정 금지** → 클라 C# DTO 수정 후 generator로 서버 Java DTO 생성

---

## 씬 전환 규칙

| 전환 | 트리거 |
|------|--------|
| LoadingScene → MainScene | 자동 로그인 완료 또는 실패 |
| MainScene → SpaceScene | 캐릭터 선택 완료 (JWT에 characterId 포함 후) |
| SpaceScene → MainScene | 로그아웃 / 캐릭터 변경 |

씬 전환 시 반드시:
1. `EventManager.UnsubscribeAll()` 호출
2. `NetworkManager.Instance.OnChangeScene()` 호출

---


## 이팩트 에셋
현재 검토 중인 에셋 목록. 최종 채택 전 단계.
상세한 모듈화 - 성능 문제 체크!
---

### URP VFX Mega Bundle — $79.99($159.99)
https://assetstore.unity.com/packages/vfx/urp-vfx-ultra-bundle-257146

### Super Realistic ARPG FX Bundle - 600+ Unique VFX! $175($350)
https://assetstore.unity.com/packages/vfx/particles/spells/super-realistic-arpg-fx-bundle-600-unique-vfx-361334

### Sci-Fi Effects - $35
https://assetstore.unity.com/packages/vfx/particles/sci-fi-effects-20416

## 함선 모듈 에셋
현재 검토 중인 에셋 목록. 최종 채택 전 단계.
상세한 모듈화 - 드로우콜에 문제 생기지 않을지! 반드시 체크
---

### Ultimate Spaceships Creator — $95 - 여러가지, 함재기까지
https://assetstore.unity.com/packages/3d/vehicles/space/ultimate-spaceships-creator-196802

### Spaceships Vol. #18 - 이건 뭔가 전투 함선 같은 느낌이 안듬
https://assetstore.unity.com/packages/3d/vehicles/space/spaceships-vol-18-155565

### Spaceship Pirate Fleet Collection I - $50 확실한 전투 함선
https://assetstore.unity.com/packages/3d/vehicles/space/spaceship-pirate-fleet-collection-i-75094

### US Space Navy Collection $175 - 너무 시싸서 keep
https://assetstore.unity.com/packages/3d/vehicles/space/us-space-navy-collection-195258


## 스카이박스 / 우주 배경 천체
현재 검토 중인 에셋 목록. 최종 채택 전 단계.
---

### [후보 A] Space Skybox Kit — $10
https://assetstore-fallback.unity.com/packages/2d/textures-materials/space-skybox-kit-176564?utm_source=chatgpt.com#content

- 큐브맵 16개 + 소행성 스프라이트 10개 + 행성 스프라이트 6개 + **PSD 원본 포함** (레이어 편집 가능)
- VFX Graph 미사용, 커스텀 셰이더 없음 → Android 빌드 안전, Draw Call 1회
- 2D 스프라이트 기반이라 3D 입체감 없음 → 배경 연출 전용 용도에 적합
- **Android 적용 시 필수 설정** (Texture Import Settings > Override for Android):
  - Max Size: `2048` / Format: `ASTC 6x6` (최신) 또는 `ETC2` (구형) / Generate Mipmaps: `OFF`
  - 원본 4096 큐브맵 비압축 ~192MB → 위 설정 시 ~10MB로 절감

---

### [후보 B] Galaxy Materials — $22
https://assetstore-fallback.unity.com/packages/vfx/shaders/galaxy-materials-skybox-update-191773

- 갤럭시 셰이더 16종 + Cubemap 텍스처 19개 + Noise/Gradient 텍스처 44개
- URP: Shader Graph 기반 (VFX Graph 미사용) → Android 빌드 가능
- **주의**: 갤럭시 셰이더를 3D 메시에 실시간 적용 시 GPU 부하 높음 (Quest2 GPU 킬러 리뷰 존재)
- Android에서는 Cubemap 텍스처 19개만 일반 Skybox/Cubemap 셰이더에 사용하는 전략 권장
- 갤럭시 셰이더 자체는 모바일 비권장

---

### [후보 C] Space Graphics Toolkit (SGT) — $99.95 (세일 시 $49.97)
https://assetstore.unity.com/packages/tools/level-design/space-graphics-toolkit-4160

스카이박스뿐 아니라 행성/블랙홀/소행성/강착원반까지 통합 패키지. 평점 5.0 / 리뷰 390개. 9년 이상 업데이트 중.

**포함 컴포넌트 및 Android 가능 여부:**

| 컴포넌트 | 용도 | Android |
|---|---|---|
| SgtSkysphere | 우주 배경 (스카이박스 대체) | 가능 |
| SgtStarfield | 별 배경 수만 개 | 가능 (Billboard 기반) |
| SgtBelt | 소행성대 수천 개 | 가능 (GPU Instanced) |
| SgtRing | 강착원반 / 행성 고리 | 가능 (광산란 OFF 조건) |
| SgtCorona | 항성 코로나 | 가능 |
| SgtSphereShadow | 행성 그림자 | 가능 |
| SgtSingularity | 블랙홀 + 중력렌즈 왜곡 | **조건부** — URP에서 Opaque Texture + Depth Texture 강제 활성 필요, 대역폭 비용 |
| SgtAtmosphere | 볼류메트릭 대기권 | **주의** — Ray marching, 설정 최적화 필요 |
| SgtJovian | 볼류메트릭 가스행성 | **비권장** — Ray marching, 중하위 기기 프레임 드랍 |

**사용 전략 (이 프로젝트 기준):**
- 쓸 것: SgtSkysphere + SgtStarfield + SgtRing + SgtBelt + SgtSphereShadow
- 조건부: SgtSingularity — 블랙홀이 등장하는 특정 씬에서만 Opaque Texture 활성화
- 안 쓸 것: SgtJovian (가스행성은 정적 텍스처 구체로 대체)

**모바일 실사용 증거:** SGT 사용 게임 Super Starship 3, Space Trek 2150이 iOS/Android에 실제 출시됨 (개발자 Les Bird가 SGT 포럼에서 직접 증언)

**학습 난이도:** 기본 씬 구성은 Inspector 드래그앤드롭 수준. 공전/자전 등 게임 로직 연동 시 C# 작업 필요. 예제 씬 30개 이상 포함. 영상 튜토리얼 없음. 제작자가 포럼에서 직접 9년째 답변 중.


---

## 주의사항 / 알려진 제약

- `using UnityEditor.ShaderGraph;` 포함 시 Android 빌드 실패
- `Update` 직접 사용 지양 → `EventManager` 또는 코루틴 사용
- `DataTableZone`, `DataTableModule` 등은 서버와 반드시 동기화
- `AdManager` 배너/전면은 NoAds 인앱 구현 시 비활성화 예정
