# Claude Code 사용 가이드 (기획자용)

VSCode + Claude Code 확장을 설치해서, 코드를 몰라도 AI에게 "이 기능이 어디 있는지", "이 값이 어떻게 동작하는지" 등을 물어보고 확인할 수 있도록 하는 세팅 가이드입니다. 겸사겸사 Unity의 스크립트 편집기도 VSCode로 연결합니다.

## 1. VSCode 설치

1. https://code.visualstudio.com/ 접속
2. "Download for Windows" 클릭 → 다운로드된 설치 파일 실행
3. 설치 마법사에서 옵션은 기본값 그대로 두고 진행 (`Add to PATH` 체크는 켜진 상태 유지 권장)
4. 설치 완료 후 VSCode 실행

## 2. 프로젝트 폴더 열기

1. VSCode 상단 메뉴 `파일(File) > 폴더 열기(Open Folder...)`
2. Unity 프로젝트 폴더 선택 (예: `C:\bk\thefirst\thefirst_client_unity`)
3. 처음 열면 오른쪽 아래에 "추천 확장을 설치하시겠습니까?" 알림이 뜸 → 설치 권장 (Unity 연동용 확장이 자동으로 뜸)

## 3. Unity 에디터 - 외부 스크립트 편집기를 VSCode로 설정

1. Unity 에디터 실행
2. 상단 메뉴 `Edit > Preferences`
3. 왼쪽 목록에서 `External Tools` 선택
4. `External Script Editor` 항목을 `Visual Studio Code`로 변경
   - 목록에 안 뜨면 `Browse...`로 직접 지정: 기본 설치 경로는 `C:\Users\사용자명\AppData\Local\Programs\Microsoft VS Code\Code.exe`
5. 이후 Unity에서 스크립트 파일을 더블클릭하면 VSCode가 열림

## 4. Claude Code 확장 설치

1. VSCode 왼쪽 사이드바에서 Extensions 아이콘 클릭 (또는 `Ctrl+Shift+X`)
2. 검색창에 `Claude Code` 입력
3. Anthropic에서 만든 "Claude Code" 확장의 `Install` 클릭
4. 설치가 끝나면 왼쪽 사이드바에 Claude 아이콘이 새로 생김 → 클릭해서 패널 열기
5. 처음 실행하면 로그인 안내가 뜸 → 브라우저가 열리면 claude.ai 계정으로 로그인 진행

## 5. 기본 사용법

- 왼쪽 Claude 패널을 열고, 채팅창에 궁금한 걸 자연어로 입력
  - 예: "CameraController.cs 파일이 어떤 역할을 하는지 설명해줘"
  - 예: "함선 이름을 표시하는 UI 프리팹이 어디 있는지 찾아줘"
  - 예: "탐사 그리드 관련 스크립트 목록 보여줘"
- Claude가 파일을 읽거나 검색하려고 할 때 권한 요청 창이 뜨면 `Allow` 클릭
- 파일 수정/삭제처럼 되돌리기 어려운 작업은 실행 전에 먼저 확인 메시지가 뜸 → 내용을 확인한 후 승인

## 6. 주의사항

- 파일 수정, git commit/push 같은 요청은 Claude가 실행 전 반드시 확인을 물어보니, 내용을 꼭 확인하고 승인할 것
- 확실하지 않으면 승인하지 말고 먼저 물어볼 것 (예: "이거 실행하면 정확히 뭐가 바뀌어?")
- 사내 규칙(`CLAUDE.md`)에 따라 Claude는 항상 한국어로 답변함
