# Unity-MCP (AI 에디터 툴) 로컬 설치 가이드

이 프로젝트는 AI 에이전트가 Unity 에디터를 직접 조작할 수 있게 해주는 `com.ivanmurzak.unity.mcp` 패키지를 사용합니다.
**이 패키지는 개발자 개인 로컬 환경에서만 필요한 도구**이고 게임 자체(빌드 결과물)와는 아무 관련이 없어서,
저장소(git)에는 포함하지 않습니다 — `Packages/manifest.json`에도 이 패키지 항목이 없고,
`.gitignore`가 `Packages/com.ivanmurzak.unity.mcp/`를 명시적으로 제외합니다.

## 왜 커밋 대상에서 뺐는가

한동안 이 패키지를 저장소 안에 직접 넣는(embedded/vendoring) 방식을 시도했었는데, 그렇게 하면
Jenkins 같은 CI 워크스페이스도 이 패키지를 그대로 받아서 **컴파일하려고 시도**하게 되고, 그 과정에서
여러 문제(아래 "알려진 이슈" 참고)가 반복적으로 발생했다. 근본적으로 이 도구는 CI/실빌드가
전혀 알 필요가 없는 것이므로, 아예 CI 체크아웃에 존재하지 않게 만드는 게 가장 확실한 해결책이다.

## 설치 방법

1. `Packages/manifest.json`의 `"dependencies"` 블록에 아래 줄 추가:
   ```json
   "com.ivanmurzak.unity.mcp": "https://github.com/IvanMurzak/Unity-MCP.git?path=/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp#<원하는 버전 태그>",
   ```
   (버전 태그는 `git ls-remote --tags https://github.com/IvanMurzak/Unity-MCP.git`로 확인)
2. Unity가 자동으로 git에서 패키지를 받아오고, NuGet 의존 DLL(`ReflectorNet.dll`, `System.Text.Json.dll` 등)을
   `Assets/Plugins/NuGet/`에 다운로드한다. (이 폴더도 `.gitignore` 대상 — 각자 로컬에 알아서 받아짐)
3. 설치/컴파일이 끝나면 **`Packages/manifest.json`의 해당 줄은 다시 지우고 커밋하지 말 것** —
   로컬 테스트/사용 후 커밋 전에 원복하거나, `git checkout -- Packages/manifest.json`으로 되돌린다.
   (실수로 커밋해도 당장 문제는 없지만, CI가 이 패키지를 다시 받으려고 시도하게 되므로 권장하지 않음)

## 알려진 이슈 (재설치/트러블슈팅 시 참고)

- **인앱 "Install Update" 버튼은 git 설치 상태에서 항상 실패한다** — 내부적으로 UPM 레지스트리 방식을 쓰기 때문. 수동으로 `manifest.json`의 버전 태그(`#0.84.1` 등)를 바꿔서 업데이트할 것.
- **`Runtime/com.IvanMurzak.Unity.MCP.Runtime.asmdef`의 `includePlatforms`가 `["Editor"]`인지 확인** — 원본 패키지는 `includePlatforms: []`(모든 플랫폼)로 되어 있는데, 이 상태로 실제 빌드(Android/iOS 등)를 하면 이 어셈블리가 참조하는 사전빌드 DLL이 없어서 `error CS0234`로 실빌드가 깨진다. (`defineConstraints`에 `UNITY_EDITOR`를 넣는 방법은 **효과 없음** — `defineConstraints`는 커스텀 스크립팅 define만 검사하고 컴파일러 내장 심볼(`UNITY_EDITOR`)은 인식 안 함. 실제로 시도했다가 실패 확인함.)
- **NuGet 의존 DLL(`Assets/Plugins/NuGet/*.dll`)은 Unity 에디터를 대화형으로 한 번 켜서 리졸버(`NuGetDependencyResolver`)가 다운로드하게 해야 함** — 순수 배치모드(`-batchmode -quit`)로는 이 리졸버가 실행될 기회가 없어서 DLL이 안 채워짐. 이게 바로 이 패키지를 CI에서 아예 빼야 하는 핵심 이유 중 하나.
- 업데이트 중 컴파일 에러가 나서 도메인 리로드가 막히면, 그 에러를 고쳐줄 리졸버 자체도 재실행이 안 되는 교착 상태에 빠질 수 있음 — 이럴 땐 `Library/ScriptAssemblies` 폴더를 삭제해서 강제 클린 재컴파일.

## 참고

이전에 이 저장소에 실제로 embedded 되어 있었을 때의 상세 트러블슈팅 기록은 git 히스토리(`ai mcp 패키지 직접 포함 방식으로 수정`, `빌드 실패 수정` 등 커밋)에서 확인 가능.
