# Unity-MCP RQL 사용 가이드 (1.11.3 기준)

Unity 에디터를 조작하는 MCP 툴은 RQL(Reflection Query Language) 체계다. 설치/업그레이드는 `UNITY_MCP_SETUP.md`, 이 파일은 **쓰는 법**.
표기: **[검증됨]** = 이 프로젝트에서 실제 실행해 통과, **[미검증]** = 툴 설명/변경기록에만 있고 아직 실행 안 해봄. 실작업하며 [미검증]을 [검증됨]으로 바꿔 나갈 것.

## 사용 가능한 툴

- 조회: `rql-query`, 수정: `rql-mutate`, 타입 멤버 조회: `rql-schema`
- 기타: `package-*`(패키지), `script-execute`(C# 즉석 실행), `tests-run`, `screenshot-isolated`, `assetstore-*`, `unity-tool-list`
- 숨김(Visible=false)이라 안 보이는 툴: `script-read/update-or-create/delete`, `screenshot-camera/game-view/scene-view` — 필요하면 사용자가 Unity의 AI Game Developer 창 > Tools에서 켜야 함. 소스 파일은 Read/Edit로 다루므로 script-* 는 불필요.
- 예전 `gameobject-*`, `assets-*`, `scene-*`, `console-*`, `editor-*` 툴은 없다. 전부 RQL로 대체됨.

## 기본 문법

`소스 | 단계 | 단계 ...` — 결과는 헤더 한 줄 + `열|열` 행.

- 소스: `objects`(씬 전체, 비활성 포함), `roots`, `scenes`, `selection`, `prefab`(열린 프리팹), `console`, `shaders`, `componentTypes`, `profiler`, `tests`, `testResults`, `editor`, `settings`
- 에셋: `unity.assets("t:Prefab", "Assets/경로")`, `unity.asset("Assets/.../x.mat")`, GameObject는 `unity.find("이름")` / `unity.find("경로/이름")`, `ref("Light#1234")`
- 단계: `where` `select` `sort` `first N` `skip N` `count` `get` `recurse children` `distinct` `group-by` `sum/min/max/avg`
- 조건은 반드시 boolean: `where material != null` (O), `where material` (X)
- 계산식은 select 안에서 괄호로 감싼다: `select kind=(intensity > 2 ? "bright" : "dim")`
- 여러 쿼리는 `;`로 이어 한 번에(최대 10개), 결과는 `[i/n]` 블록별로 나옴

## 자주 쓸 쿼리

- 에디터 상태: `editor | select isPlaying isPaused isCompiling` **[검증됨]**
- 콘솔 에러 최근 5개: `console | where logType == "Error" | sort sequence desc | first 5 | select message` **[검증됨]**
- 이름으로 오브젝트: `objects | where name contains "Panel" | select $ref name active` **[미검증]**
- 컴포넌트 필드 읽기: `objects | get components | where $ is Light | select gameObject.name intensity` **[미검증]**
- 열린 프리팹 안 오브젝트: `prefab | recurse children | select $path name` **[미검증]**
- 에셋 조회: `unity.assets("t:Prefab", "Assets/Resources/Prefabs/UI") | select name assetPath` **[미검증]**
- 모르는 타입의 멤버: `rql-schema`에 타입 이름 전달(예: `Light`). 인자 없이 부르면 소스/설치된 확장/씬 컴포넌트 목록 **[미검증]**

## 수정(`rql-mutate`) 주의

- 모든 문장은 `set` 또는 `call`로 끝나야 하고, 문장마다 한 번에 적용/한 번에 Undo.
- 변경기록상 `dry_run`(적용 없이 검사)과 `max_targets`(한 문장이 건드릴 수 있는 최대 대상 수) 옵션이 있다. 처음 쓰는 쿼리는 dry_run 먼저 **[미검증]**.
- 삭제는 두 단계: 대상 목록을 사용자에게 보여주고 확인받은 뒤에만 삭제. 사용자가 정확한 대상을 지정한 경우는 예외.
- 프리팹 UI 배치는 사용자가 직접 하는 작업이므로 MCP로 대신하지 않는다(프로젝트 규칙).

## 토큰 절감 요령

- `select`로 필요한 열만, `first N`/`count`로 결과 크기 제한.
- 탐색은 한 호출에 `;`로 묶어서.
- 낯선 컴포넌트는 추측해서 반복 호출하지 말고 `rql-schema`로 한 번에 멤버 확인.
- 소스 파일은 RQL이 아니라 Read/Grep로.
