# Unity-MCP (AI 에디터 툴) 로컬 설치 가이드

**현재 이 프로젝트에서 사용 중인 버전: `0.86.3`** (2026-07-30 업그레이드, 0.84.1 → 0.86.3). 아래 설치 절차의 `<원하는 버전 태그>` 자리에 이 버전을 넣으면 됨. 이후 다시 업그레이드하면 이 줄도 같이 갱신할 것.

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
   "com.ivanmurzak.unity.mcp": "https://github.com/IvanMurzak/Unity-MCP.git?path=/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp#0.86.3",
   ```
   (더 최신 버전이 나왔는지는 `git ls-remote --tags https://github.com/IvanMurzak/Unity-MCP.git`로 확인. 위 버전은 이 프로젝트가 현재 맞춰둔 버전이므로, 다른 버전을 쓰면 asmdef/Tests 등에서 아래 "알려진 이슈"와 유사한 API 불일치가 날 수 있음)
2. Unity가 자동으로 git에서 패키지를 받아오고, NuGet 의존 DLL(`ReflectorNet.dll`, `System.Text.Json.dll` 등)을
   `Assets/Plugins/NuGet/`에 다운로드한다. (이 폴더도 `.gitignore` 대상 — 각자 로컬에 알아서 받아짐)
3. 설치/컴파일이 끝나면 **`Packages/manifest.json`의 해당 줄은 다시 지우고 커밋하지 말 것** —
   로컬 테스트/사용 후 커밋 전에 원복하거나, `git checkout -- Packages/manifest.json`으로 되돌린다.
   (실수로 커밋해도 당장 문제는 없지만, CI가 이 패키지를 다시 받으려고 시도하게 되므로 권장하지 않음)
4. **커스텀 MCP 툴 스크립트 복사** — 이 저장소에서 직접 추가한 커스텀 AI 툴(`Tool_UISimulateClick.cs` 등)은
   원본 패키지 저장소엔 없으므로 git URL 설치만으로는 따라오지 않는다. `Packages/UnityMcpCustomTools/`(git 추적 대상)에
   `.cs.txt`로 보관해뒀으니, 패키지 설치가 끝난 뒤 아래처럼 확장자를 바꿔 실제 패키지 폴더에 복사해 넣을 것:
   ```
   Packages/UnityMcpCustomTools/Tool_UISimulateClick.cs.txt
     → Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/Tool_UISimulateClick.cs
   ```
   (새 커스텀 툴을 추가할 때도 같은 패턴으로: 실제 동작 파일은 패키지 폴더에, 백업용 `.cs.txt` 사본은 여기에 추가)

## 알려진 이슈 (재설치/트러블슈팅 시 참고)

- **인앱 "Install Update" 버튼은 git 설치 상태에서 항상 실패한다** — 내부적으로 UPM 레지스트리 방식을 쓰기 때문. 수동으로 `manifest.json`의 버전 태그(`#0.86.3` 등)를 바꿔서 업데이트할 것.
- **버전 업그레이드 시 Tests 폴더도 반드시 새 버전 걸로 같이 받을 것** — Runtime API가 바뀌면(예: 0.86.3에서 `CloudToken` 필드가 `Token`/`LocalToken`으로 개명됨) 옛 Tests가 새 Runtime과 안 맞아 컴파일 에러가 남.
- **NuGet DLL 버전이 이전 것으로 남아있으면 컴파일 에러 남** — `Assets/Plugins/NuGet/.nuget-installed.json`에서 실제 설치된 버전 확인. 자동 리졸버가 안 돌면 Editor 메뉴 `Tools/AI Game Developer/Dependencies/Force Resolve NuGet DLLs`로 강제 재해석.
- **업그레이드 중 5~10분 정도 Unity가 응답 없어 보이는 구간이 정상적으로 발생할 수 있음**(SignalR 클라이언트 등 핵심 DLL 교체 시 도메인 리로드+MCP 서버 재기동 겹침). CPU 사용량이 있으면 강제종료 전에 좀 더 기다려볼 것.
- **`Runtime/com.IvanMurzak.Unity.MCP.Runtime.asmdef`의 `includePlatforms`가 `["Editor"]`인지 확인** — 원본 패키지는 `includePlatforms: []`(모든 플랫폼)로 되어 있는데, 이 상태로 실제 빌드(Android/iOS 등)를 하면 이 어셈블리가 참조하는 사전빌드 DLL이 없어서 `error CS0234`로 실빌드가 깨진다. (`defineConstraints`에 `UNITY_EDITOR`를 넣는 방법은 **효과 없음** — `defineConstraints`는 커스텀 스크립팅 define만 검사하고 컴파일러 내장 심볼(`UNITY_EDITOR`)은 인식 안 함. 실제로 시도했다가 실패 확인함.)
- **NuGet 의존 DLL(`Assets/Plugins/NuGet/*.dll`)은 Unity 에디터를 대화형으로 한 번 켜서 리졸버(`NuGetDependencyResolver`)가 다운로드하게 해야 함** — 순수 배치모드(`-batchmode -quit`)로는 이 리졸버가 실행될 기회가 없어서 DLL이 안 채워짐. 이게 바로 이 패키지를 CI에서 아예 빼야 하는 핵심 이유 중 하나.
- 업데이트 중 컴파일 에러가 나서 도메인 리로드가 막히면, 그 에러를 고쳐줄 리졸버 자체도 재실행이 안 되는 교착 상태에 빠질 수 있음 — 이럴 땐 `Library/ScriptAssemblies` 폴더를 삭제해서 강제 클린 재컴파일.

## 참고

이전에 이 저장소에 실제로 embedded 되어 있었을 때의 상세 트러블슈팅 기록은 git 히스토리(`ai mcp 패키지 직접 포함 방식으로 수정`, `빌드 실패 수정` 등 커밋)에서 확인 가능.
