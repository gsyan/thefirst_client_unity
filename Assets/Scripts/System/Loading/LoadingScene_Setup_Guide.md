# LoadingScene 설정 가이드

## 1. Unity Editor에서 LoadingScene 생성
1. Unity Editor에서 File -> New Scene 선택
2. Scene을 "LoadingScene"으로 저장 (Assets/Scenes/LoadingScene.unity)

## 2. LoadingScene UI 구성
### Canvas 설정:
1. Hierarchy에서 우클릭 -> UI -> Canvas 생성
2. Canvas의 UI Scale Mode를 "Scale With Screen Size"로 설정
3. Reference Resolution: 1920x1080

### UI 요소 생성:
1. **LoadingText** (TMP_Text)
   - Canvas 하위에 생성
   - Text: "Loading..."
   - Font Size: 48
   - 위치: 화면 중앙
   - Anchor: Center

2. **ProgressText** (TMP_Text)
   - Canvas 하위에 생성
   - Text: "0%"
   - Font Size: 36
   - 위치: LoadingText 아래
   - Anchor: Center

3. **TipText** (TMP_Text)
   - Canvas 하위에 생성
   - Text: "Loading tips will appear here..."
   - Font Size: 24
   - 위치: 화면 하단
   - Anchor: Bottom Center
   - Color: 회색 (70% Alpha)

## 3. LoadingManager 설정
1. 빈 GameObject 생성 -> 이름: "LoadingManager"
2. LoadingManager 스크립트 추가
3. LoadingManager의 각 필드에 해당 UI 요소 연결:
   - Loading Text -> LoadingText
   - Progress Text -> ProgressText

## 4. UILoading 설정
1. Canvas에 UILoading 스크립트 추가
2. UILoading의 각 필드에 해당 UI 요소 연결:
   - loading Text -> LoadingText
   - progress Text -> ProgressText
   - tip Text -> TipText

## 5. Build Settings 설정
1. File -> Build Settings 열기
2. LoadingScene을 Scenes In Build에 추가
3. 다른 씬들도 올바른 순서로 정렬되어 있는지 확인

## 주의사항
- LoadingScene은 다른 씬들보다 먼저 빌드에 포함되어야 합니다.
- UI 요소들이 올바르게 연결되었는지 확인하세요.
- TextMeshPro 패키지가 설치되어 있는지 확인하세요.