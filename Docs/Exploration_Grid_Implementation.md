# 탐사 그리드 구현 작업 문서

> 기획 확정 사항은 `Exploration_Revamp.md` 참고. 이 문서는 실제 구현 착수용 분석/계획 기록.
> 상태: 코드 작성 진행 중 — **`GridCellButton.prefab` 조립 완료. 다음은 `UITabExplorationGrid`를 UIPanelSpace 프리팹에 붙이는 작업** (§0-2 참고)

## 1. 작업 순서 (합의됨)

1. **그리드/셀 이동 구조** — ✅ 데이터/생성 로직 완료, UI 코드 완료, **프리팹 조립만 미완**
2. **함대 구성 시스템** (지휘력 점유/반환 + 인덱스 배치 + 전방/후방) — ✅ 로직 완료, 서버 DTO/API 연동 완료
3. 테스트용 더미 함선 프리셋 3종 — ✅ 완료
4. 성능포인트 세부 밸런스(장착+강화, 슬롯 배열) — ✅ 완료
5. 실제 화면(Unity 씬)에 그리드 UI 붙여서 눈으로 확인 — 🔶 진행 중, 프리팹 조립 마무리 필요

## 0. 진행 로그 (2026-07-21)

### 0-1. 완료된 파일

**그리드/셀 데이터**
- `Assets/Scripts/Exploration/GridDimensions.cs` — 그리드 가로/세로 크기 struct
- `Assets/Scripts/Exploration/GridCellData.cs` — 셀 데이터 struct (x,y,isStart,isEscape,isEmpty,isCleared)
- `Assets/Scripts/Exploration/ExplorationGridData.cs` — 그리드 컨테이너 (2차원 배열, `IsAdjacent()` 4방향 판정)
- `Assets/Scripts/Exploration/ExplorationGridGenerator.cs` — 존 시드 기반 결정적 그리드 생성 (시작/탈출점 최소간격, 빈 셀 확률)
- `Assets/Scripts/System/Data/DataTableZoneGridSize.cs` + `Assets/Scripts/Editor/DataTableZoneGridSizeEditor.cs` — 존별 그리드 크기 규칙표 (CSV Import/Export, 기존 DataTable 컨벤션 준수)
- `Assets/Resources/DataTable/Exploration/datatable_zone_grid_size.csv` + `Assets/Resources/DataTable/DataTableZoneGridSize.asset` — 실제 데이터(1~3존=3x3, 4~10존=5x5, 11~20존=7x7, 21존~=7x10)

**함대 구성 / 지휘력**
- `Assets/Scripts/Exploration/ShipPresetData.cs` — 함선 프리셋 (presetId, displayNameKey, prefabName, commandCost, statAllocation)
- `Assets/Scripts/Exploration/FleetSlotEntry.cs` — 배치된 함선 1개 (shipPresetId, isFront)
- `Assets/Scripts/Exploration/FleetComposition.cs` — 지휘력 점유/반환 로직, `ToNetworkFleetInfo()`로 DTO 변환
- `Assets/Scripts/System/Network/NetworkDTOs.cs` — Exploration Grid Data Classes 리전: `ExplorationShipSlot`, `ExplorationFleetInfo`, `EnterExplorationCellRequest/Response`, `ClearExplorationCellRequest/Response`, `EscapeExplorationZoneRequest/Response`, `IncreaseCommandPowerMaxRequest/Response`, `UnlockShipPresetRequest/Response`
- `Assets/Scripts/System/Network/ApiClient.cs`, `NetworkManager.cs` — 위 DTO 대응 API 메서드 5종 (`/exploration/...` 엔드포인트)

**성능포인트 배분 시스템 (슬롯 배열 최종본)**
- `Assets/Scripts/Exploration/ShipStatAllocation.cs` — 카테고리별 슬롯 배열(`bool[] xxxSlotInstalled` + `int[] xxxPoints`). 슬롯 개수는 하드코딩 없이 `DataTableConfig` 참조
- `Assets/Scripts/Exploration/ShipFinalStats.cs`, `ShipStatCalculator.cs` — 슬롯별 독립 계산 (순수 함수, `DataTableConfig` 직접 참조 안 함)
- `Assets/Scripts/System/Data/DataTableConfig.cs` — `GameSettings.shipStatFormula`(`ShipStatFormulaSettings`)에 Beam/Missile/Hangar/Shield/Interceptor/FlatStats 계수 + `maxModuleSlots`(기본 6) 전부 데이터로 관리 (Inspector 폴드아웃 가능한 중첩 클래스)
- `Assets/Scripts/System/Data/DataTableShipPreset.cs` + `Assets/Scripts/Editor/DataTableShipPresetEditor.cs` — 슬롯 6개 고정 컬럼 CSV(빈 칸=미장착), Inspector에 슬롯별 장착 토글+강화값 UI, 드래그-스크럽 방지(TextField 파싱), Total Points Used 표시
- `Assets/Resources/DataTable/Exploration/datatable_ship_preset.csv` + `.asset` — 더미 3종(beam_light_01 cost100/500p, missile_heavy_01 cost250/750p, carrier_01 cost500/1000p) — 코스트가 포인트보다 가파르게 증가하는 체감곡선 예시로 검증됨
- `Assets/Resources/Localization/csv/UI.csv` — `ShipPreset_BeamLight01` 등 로컬라이즈 키 3종 추가

**그리드 UI (신규, `UITabExploration.cs`는 건드리지 않음 — 별개 클래스로 신규 제작)**
- `Assets/Scripts/UI/UITab/GridCellButton.cs` — 셀 버튼. 고정 좌표 배치(월드좌표 불필요), 상태별(Locked/Reachable/Current/Cleared) 색상, 인접 셀 깜빡임(코루틴), 이음선 없음(격자 배치라 불필요, `UIZoneConnector` 재사용 안 하기로 결정)
- `Assets/Scripts/UI/UITab/UITabExplorationGrid.cs` — `UITabBase` 상속 신규 컨트롤러. `EnterZone(zoneNumber, seed)` → 그리드 생성 → 버튼 배치(로컬 리스트 풀링, 기존 `UITabExploration.cs` 버튼풀 패턴과 동일) → 인접 셀 클릭 시 이동. **전투 진입/네트워크 연동은 아직 안 붙임**

**서버 연동**: 클라 C# DTO 기준으로 generator 실행 완료(사용자 확인). 지휘력 최대치는 별도 조회 없이 세션 데이터에 포함, 함대 배치는 실시간 동기화 없이 전투시작 요청에 동봉.

**컴파일 확인**: Unity MCP `assets-refresh` + `editor-application-get-state`로 매 단계 컴파일 성공 확인함. (마지막 확인 시점 전부 정상)

### 0-2. `GridCellButton` 프리팹 조립 — ✅ 완료 (2026-07-21)

`Assets/Resources/Prefabs/UI/Exploration/GridCellButton.prefab` 저장 완료. StartIcon/EscapeIcon 비활성화, RectTransform 180x180, 스크립트 필드(m_rectTransform/m_backgroundImage/m_startIcon/m_escapeIcon/m_button) 연결 모두 확인됨. 씬의 임시 오브젝트는 삭제, MainScene 저장 완료.

**작업 중 확인된 Unity MCP 툴 사용법 (재사용 참고용)**
- GameObject `SetActive(false)`: `gameobject-modify`의 `activeSelf`는 read-only 프로퍼티라 실패함 → `reflection-method-find`/`reflection-method-call`로 `UnityEngine.GameObject.SetActive(bool)`를 직접 호출해야 함. `targetObject`에 `{"typeName":"UnityEngine.GameObject","value":{"instanceID":N}}` 형식으로 기존 인스턴스 지정 가능
- `gameobject-component-modify`의 `pathPatches`로 private 필드(`m_rectTransform` 등 UnityEngine.Object 참조 필드)에 다른 컴포넌트/오브젝트 연결 가능 — `Value`에 `{"typeName":"<컴포넌트타입>","value":{"instanceID":N}}` 형식 사용

**다음 단계 — `UITabExplorationGrid`를 실제 탭에 연결 (§0-2-1 참고, 프리팹 구조 컨벤션 수정됨)**
- **[중요, 2026-07-21 수정]** 이 프로젝트 컨벤션상 `TabExploration`처럼 각 탭은 **UIPanelSpace.prefab(`Assets/Resources/Prefabs/UI/Panel_Game/UIPanelSpace.prefab`) 안에 직접 자식 GameObject로 존재**함(`Canvas (Environment)/UIPanelSpace/TabExploration`, TabCommander/TabFleet/TabShip 등과 형제). **탭 전체를 별도 프리팹으로 만드는 방식은 컨벤션 위반** — `UITabExplorationGrid`도 UIPanelSpace 프리팹 스테이지를 열어(`assets-prefab-open`) 그 안에 `TabExploration`과 형제로 새 GameObject를 만들어 붙여야 함
- `GridCellButton.prefab`(개별 셀 풀링 버튼)은 `UIZoneStageButton.prefab` 컨벤션과 동일하게 별도 프리팹으로 유지하는 게 맞음(수정 불필요)
- `m_cellButtonPrefab`/`m_cellRoot`/`m_gridSizeTable` 필드를 연결해 실제 씬에서 `EnterZone()` 호출 테스트

### 0-3. 이후 남은 작업 (프리팹 완성 후)
- `UITabExplorationGrid`에 전투 진입 연동: 인접 셀 클릭 시 바로 이동만 하지 말고 정찰 UI(§1-3 듀얼 카메라 흐름) → "전투시작" 확정 → `EnterExplorationCellRequest`(FleetComposition.ToNetworkFleetInfo() 포함) 전송 흐름 연결
- 실드/요격체 이산 쿨다운 기반 재시뮬레이션 (Exploration_Revamp.md §5 미결)
- 구현 우선순위 확정 (Exploration_Revamp.md §5 미결)

## 2. 기존 코드 분석 (완료)

### 2-1. 노드 연결 관계 저장 방식
- `DataTableZone.cs` L112: `List<ZoneStageConfig> zoneStageList` — 전체 스테이지 순차 저장
- `DataTableZone.cs` L114: `Dictionary<int, List<ZoneStageConfig>> m_stagesByZone` — 존 그룹별 단계화
- `UITabExploration.cs` L39: `Dictionary<string, UIZoneStageButton> m_zoneStageButtons` — UI 인스턴스 매핑
- **연결 관계 = 순번 파싱뿐**: `ParseZoneGroup()`/`ParseZoneStage()`(L604~695)가 "1-1" 형식 존명에서 그룹/스테이지 번호를 뽑아 순번 순서(X-1→X-2→X-3...)로만 인접 판정. 좌표 기반 인접성 개념 자체가 없음

### 2-2. 다음 노드 활성화 로직 (`UITabExploration.cs`)
- `IsPreviousStageCleared()` (L846~874): 이전 스테이지 클리어 여부로 다음 진입 가능성 판정
- `IsZoneGroupCleared()` (L822~837): 존 그룹 전체 클리어 확인
- `IsZoneGroupLocked()` (L840~844): 이전 존 그룹 미클리어 시 잠금
- `UIZoneTabNode.SetState()` (L32~45): 상태(selected/cleared/locked) → 색상 반영

### 2-3. 연결선 렌더링 — 완전 동적 계산
- `UIZoneConnector.SetPoints()` (L27~58): 두 점(A,B) screen-space 좌표 입력 → `diff.magnitude`/`Atan2` 로 거리·각도 계산 → `RectTransform.sizeDelta`/`localEulerAngles` 조정
- `RefreshZoneConnectors()` (L324~356): 정렬된 노드 순서대로 인접 쌍 순회하며 `SetPoints()` 호출
- 색상: `fromCleared` 여부로 "Unlocked"(초록)/"Zone.Locked"(빨강) 분기

### 2-4. 클릭→진입 이벤트 흐름
```
UIZoneStageButton.m_enterButton.onClick
  └─ OnEnterZoneStageFromButton() [L632]
       ├─ IsPreviousStageCleared() 체크
       ├─ ShowConfirmPopup()
       └─ EnterZoneStageWithServerData() [L783]
            → NetworkManager.GetStageEnemies()
               → OnGetStageEnemiesResponse() [L789]
                  → EnterZoneStage() [L703]
                     → SetFleetState(Warp) → m_battleZoneStage = zoneStage
                     → CloseAllTabs() → StartBattleInZone()
```

### 2-5. 좌표계 — 혼합 방식
- **하드코딩**: `ZoneStageConfig.fleetPosition`(L100), `labelScreenOffset`(L104) — 에디터에서 디자이너가 직접 배치
- **자동계산**: `ResolveFleetWorldPosition()`(DataTableZone L284, `zone center + stage.fleetPosition`), `UIZoneStageButton.UpdateScreenPosition()`(L197, `WorldToScreenPoint()` 매 프레임 갱신)

## 3. 그리드 재설계 전략

| 항목 | 재사용 여부 | 비고 |
|---|---|---|
| World-to-Screen 변환 | ✅ 재사용 | `UpdateScreenPosition()` 그대로 |
| 클릭 이벤트 흐름 | ✅ 재사용 | 버튼 → 팝업 → 서버 요청 → 진입, 구조 그대로 |
| 연결선 렌더링(`UIZoneConnector.SetPoints`) | ✅ 재사용 | 좌표 두 점만 넘기면 되므로 그대로 활용 |
| 클리어 상태 판정 패턴 | ✅ 재사용 | `IsPreviousStageCleared()` 류 패턴을 그리드 인접셀 버전으로 변형 |
| **노드 연결 관계(순번 파싱)** | ❌ 신규 | `(gridX, gridY)` 좌표 기반 인접성 판정으로 완전 교체 |
| **인접 셀 검증 로직** | ❌ 신규 | 4방향(상하좌우)만 확정. Δx,Δy 체크로 구현 |
| **그리드 데이터 구조** | ❌ 신규 | 존마다 그리드 크기가 가변(3x3→5x5→7x7→7x10...)이므로 고정 2차원 배열 대신 `Dictionary<(int x, int y), GridCellData>` + 존별 width/height 메타 필요 |
| **가로 스크롤 레이아웃** | ❌ 신규 | 세로 한계 도달 시 가로로만 확장 → 고정 그리드 UI가 아니라 `ScrollRect` 기반 동적 배치 필요 |
| **이동 가능 셀 강조 표시** | ❌ 신규 | 현재 위치 기준 인접 셀에 깜빡임(블링크) 애니메이션 — 코루틴 또는 별도 하이라이트 컴포넌트 |
| **빈 셀(무적 보상) 로직** | ❌ 신규 | 낮은 확률로 적 없이 탐험 포인트만 주는 셀 — 스폰 테이블에 별도 확률/보상량 필드 필요 |
| **재방문 파밍 로직** | ❌ 신규 | 기존은 "1회 클리어 후 잠김 해제" 개념뿐, 재방문 시 적 재등장/보상 재획득 로직 없음 |

**핵심 결론**: 렌더링·이벤트 하위 계층(스크린 좌표 변환, 연결선, 클릭 흐름)은 그대로 재사용 가능하지만, **"노드가 어떻게 연결되는가"를 결정하는 상위 로직(순번 파싱)은 통째로 좌표 기반 그래프로 교체**해야 하고, **레이아웃도 고정 그리드가 아니라 가변 크기+가로 스크롤을 전제로 설계**해야 함.

## 4. 함대 슬롯 관리 UI (2026-07-21 확정)

3x3 등 공간형(그리드) 배치 UI는 기각 — UI가 지나치게 커짐. **리스트 기반 슬롯 관리**로 확정. 배치된 함선 리스트의 각 항목에 전방/후방 토글이 별도로 붙는 구조 (공간 배치가 아니라 리스트 순서 + 개별 속성). `Exploration_Revamp.md` §1-3 참고.

## 5. 그리드 생성/검증 권한 (2026-07-21 확정)

**그리드 생성은 클라이언트가 담당** — 클라가 시드/데이터를 갖고 그리드(좌표, 적 스폰, 시작/탈출점, 빈 셀 배치)를 직접 생성. **서버는 결과만 검증**(클리어/탈출 성공-실패), 그리드 자체를 authoritative하게 내려주지 않음. 기존 §1-7 "클라가 이벤트 전송 → 서버 기록" 패턴과 일치.

## 6. 용어 정리 (2026-07-21 확정)

기존 코드의 "Zone(존 그룹)"/"Stage(개별 스테이지)" 구분과 별개로, 신규 그리드 설계에서는 **"존" 안에 그리드가 있고, 그리드는 "셀"들로 구성**되는 구조로 용어를 통일한다.
- **존(Zone)**: 그리드 하나를 담는 컨테이너. 존 번호에 따라 그리드 크기가 달라짐
- **셀(Cell)**: 그리드를 구성하는 개별 칸. `GridCellData`가 셀 하나의 데이터

## 7. GridCellData 구성 (2026-07-21 확정)

- **적 함대 구성**: 고정(결정적) — `(존 시드, x, y)` 조합으로 언제든 동일하게 재현 가능. 셀에 시드/구성을 영구 저장할 필요 없음 (그리드 생성 시 1회 계산 후 세션 중 메모리 캐싱으로 충분)
- **보상카드**: 매번 새로 랜덤 — 클리어 순간 즉석 생성, 저장 대상 아님

```
GridCellData
- x, y (좌표)
- isStart / isEscape (시작점/탈출점 플래그)
- isEmpty (빈 셀 여부 — 낮은 확률 무적 보상)
- isCleared (클리어 여부, 재방문 판단·강조 표시용)
```
적 함대 구성과 보상카드는 필드로 저장하지 않고 각각 결정적 재계산 / 즉석 랜덤으로 처리.

- **시작점/탈출점 위치도 적 함대 구성과 동일하게 고정(결정적)**: 존 시드로부터 생성되며, **제작자가 해당 존의 시드값을 직접 바꾸지 않는 한 항상 동일** — 플레이어 방문마다 바뀌는 런타임 랜덤이 아니라 콘텐츠 저작 시점에 고정되는 디자인 값

**그리드 생성 제약 조건**
- 시작점-탈출점 사이 **최소 셀 간격(거리)을 설정 가능**하게 생성 로직에 파라미터로 둬야 함 — 너무 가까운 위치에 배치되어 탐험이 무의미하게 짧아지는 것 방지. 최소 거리값은 그리드 크기별로 다르게 설정 가능해야 함(아래 존별 그리드 크기 규칙표와 함께 정의)

## 8. 존별 그리드 크기 규칙표 (2026-07-21 확정)

```csharp
public enum EZoneGridSize
{
    Grid3x3,   // 존 1~3
    Grid5x5,   // 존 4~10
    Grid7x7,   // 존 11~20
    Grid7x10,  // 존 21~30
}
```

| 존 범위 | 그리드 크기 | 열거값 |
|---|---|---|
| 1~3 | 3x3 | `Grid3x3` |
| 4~10 | 5x5 | `Grid5x5` |
| 11~20 | 7x7 | `Grid7x7` |
| 21~30 | 7x10 | `Grid7x10` |

## 9. 확정 사항 (2026-07-21)

- **존 구간 경계(3/10/20/30)는 최종값으로 확정.** 밸런스 조정은 출시 후 후속 튜닝 영역
- **존 30 이후 그리드 크기는 7x10 고정** — 우선 이 범위로 구현하고, 필요해지면 추후 확장(추가 열거값)으로 대응. 지금 미리 설계할 필요 없음
- **셀 크기(간격)는 200x200(월드/UI 단위)로 우선 적용** — 시작점-탈출점 "최소 셀 개수 간격" 같은 정밀 값은 실제 UI가 나와봐야 감이 잡히므로, 지금은 이 셀 크기로 프로토타입을 만들고 체감하면서 최소 간격을 나중에 튜닝
- **서버 검증 방식**: 시작 셀 진입, 셀 클리어, 탈출 각 시점마다 클라가 해당 셀을 특정할 수 있는 정보를 서버에 개별 전송 → DB 기록. 서버는 그 정보가 타당한지(예: 인접 셀 이동인지, 이미 정의된 시작/탈출 셀이 맞는지) 검증 — 한 번에 몰아서 보고하는 방식이 아니라 이벤트마다 즉시 전송·기록하는 기존 패턴(§1-7) 그대로

## 10. 미결 사항

- [ ] 최소 셀 간격 정밀값 (프로토타입 체감 후 튜닝 예정, §9 참고)
- [ ] 프로토타입 완료 후, 실제 착수 순서(§1)대로 코드 작성 시작
