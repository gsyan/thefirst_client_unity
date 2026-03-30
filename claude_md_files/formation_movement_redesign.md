# 진형 이동 시스템 로직 참고

## 전체 흐름

```
[진형 변경 요청]
  SpaceFleet.ChangeFormation(newFormationType)
    → 서버 API 호출 (NetworkManager.ChangeFormation)
    → 성공 시 UpdateShipFormation(newFormationType, smooth=true)

[진형 갱신 — 함선 추가/제거/크기변화]
  SpaceFleet.RefreshFormation()
    → StopFormationMovement() (전체 함선)
    → UpdateShipFormation(m_currentFormationType, smooth=true)

[Body 교체로 함선 크기 변화]
  SpaceShip.Apply_ChangeModule (body 분기)
    → EventManager.Trigger_ShipBodyChanged(this)
    → SpaceFleet.OnShipBodyChanged(ship) → RefreshFormation()
```

---

## 목적지 계산 — SpaceFleet.CalculateFormationTargets()

**원칙**: `positionIndex` 고정 슬롯, 교환(Hungarian) 없음.
`m_presetMap`에서 `EFormationType` → `FormationPreset` 조회 후 parseType에 따라 분기.

```
UpdateShipFormation(formationType, smooth)
  └─ CalculateFormationTargets(formationType)
       └─ m_presetMap[formationType] → FormationPreset
            ├─ CubeGrid  → ParseCubeGrid()
            └─ Circle    → ParseCircle()
  └─ smooth=true  → MoveToFormation()
     smooth=false → transform.localPosition 직접 세팅
```

---

## 진형 배치 데이터 — FormationPreset (ScriptableObject)

```csharp
public enum EFormationParseType { CubeGrid, Circle }

public struct FormationSlot {
    public int positionIndex;
    public Vector2Int gridCoord;  // CubeGrid 전용: (x, z) 정수 격자
    public float circleAngle;     // Circle 전용: 각도(도), 0=전방, 90=우
}
```

Assets/Resources/Formation/ 에 4개 asset:
- `Preset_formation_type_linear_horizontal.asset`
- `Preset_formation_type_cross.asset`
- `Preset_formation_type_x.asset`
- `Preset_formation_type_circle.asset`

`Tools > Formation > Generate Presets` 로 생성.
이미 존재하면 스킵, `EFormationType`에 새 값 추가 시 Linear 슬롯 기본값으로 자동 생성.

---

## ParseCubeGrid — 정수 격자 → 누적 간격 변환

```
positionIndex 0 (기함) → (0, 0, 0)

각 축별 누적 간격:
  cumX[n] = 해당 x-슬롯 함선들의 max halfX 기반 누적
  cumZ[n] = 해당 z-슬롯 함선들의 max halfZ 기반 누적

ship at gridCoord (ix, iz):
  world_x = cumX[|ix|] * sign(ix)
  world_z = cumZ[|iz|] * sign(iz)
```

### 각 진형의 격자 배치 (positionIndex: gridCoord)

**Linear** `8 6 4 2 0 1 3 5 7` (홀수=우측, 짝수=좌측)
```
0:(0,0)  1:(+1,0)  2:(-1,0)  3:(+2,0)  4:(-2,0) ...
```

**Cross** (대각 쌍 배치)
```
6(-2,+2)        5(+2,+2)
  2(-1,+1)  1(+1,+1)
        0(0,0)
  4(-1,-1)  3(+1,-1)
8(-2,-2)        7(+2,-2)
```

**X** (Cross와 topology 동일, z 2배 확장)
```
0:(0,0)  1:(+1,+2)  2:(-1,+2)  3:(+1,-2)  4:(-1,-2) ...
```

### 상수
```csharp
private const float FORMATION_GAP = 2f;  // 함선 간 최소 여백
```

---

## ParseCircle — 각도 → 원주 위치 변환

반지름은 런타임에 함선 사이즈 기반 자동 계산:
```
radiusBySpacing  = n * (maxShipSize + GAP) / (2π)
radiusByFlagship = flagship.halfX + GAP + maxShipSize/2
radius = Max(두 값)
```

각 함선 수별 circleAngle (0°=전방, 90°=우, 180°=후방, 270°=좌):

| shipCount | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
|---|---|---|---|---|---|---|---|---|---|
| 2 | — | 90° | — | — | — | — | — | — | — |
| 3 | — | 90° | 270° | — | — | — | — | — | — |
| 4 | — | 60° | 300° | 180° | — | — | — | — | — |
| 5 | — | 45° | 315° | 135° | 225° | — | — | — | — |
| 6 | — | 0° | 288° | 72° | 216° | 144° | — | — | — |
| 7 | — | 30° | 330° | 90° | 270° | 150° | 210° | — | — |
| 8 | — | 25.7° | 334.3° | 77.1° | 282.9° | 128.6° | 231.4° | 180° | — |
| 9 | — | 22.5° | 337.5° | 67.5° | 292.5° | 112.5° | 247.5° | 157.5° | 202.5° |

홀수 positionIndex = 오른쪽(x>0), 짝수 = 왼쪽(x<0)

---

## 이동 실행 — SpaceShip `#region Formation Movement`

### 상태
```csharp
public FormationMoveState m_formationMoveState;  // Idle / Moving / Arrived
```

### FormationMovementLoop (코루틴, 매 프레임)
```
toTarget = m_formationTarget - localPos
dist = toTarget.magnitude

dist < ARRIVAL_THRESHOLD(0.1f):
  → localPos = m_formationTarget, state = Arrived, 종료

avoidDir  = m_avoidanceAccum        ← 이 프레임 누적값
m_avoidanceAccum = zero             ← 매 프레임 리셋

avoidWeight = Clamp01(avoidDir.magnitude)  // == Clamp01(penetrationDepth)
avoidWeight > m_avoidActivateThreshold:
  finalDir = Lerp(targetDir, avoidDir.normalized, avoidWeight).normalized
else:
  finalDir = targetDir

speedMult = Max(Clamp01(dist / SLOWDOWN_DISTANCE(0.5f)), 0.2f)
moveDist  = Min(speed * speedMult * deltaTime, dist)  ← 오버슈팅 방지
localPos += finalDir * moveDist
```

### 인스펙터 튜닝 필드
```csharp
[SerializeField] float m_avoidActivateThreshold = 0.1f;  // 회피 발동 최소 weight
[SerializeField] float m_avoidWeightScale        = 2f;   // penetrationDepth 감도 배율
```

### OnShieldTriggerStay(SpaceShip other, float penetrationDepth) — 우선권 회피
```
Moving 상태, 같은 fleet, 내 positionIndex > 상대 positionIndex 일 때만 실행
awayDir = (내 localPos - 상대 localPos)
m_avoidanceAccum += awayDir.normalized * (penetrationDepth * m_avoidWeightScale)
```

---

## 실드 트리거 연결 — ShieldGrid / ShieldTriggerRelay

```
[에디터 타임] ShieldGrid.GenerateCollider()
  → "ShieldCollider" 자식 오브젝트 생성
  → MeshCollider (convex, isTrigger=true) + kinematic Rigidbody 추가 (프리팹에 저장)

[런타임] ShieldGrid.InitFormationRelay(SpaceShip owner)
  → "ShieldCollider" 자식 찾기
  → ShieldTriggerRelay 없으면 AddComponent, relay.owner = owner

[SpaceShip.InitializeSpaceShip()]
  → m_shieldGrid.InitFormationRelay(this)

[SpaceShip.Apply_ChangeModule body 분기]
  → m_shieldGrid 재취득 후 InitFormationRelay(this) 재설정
```

```csharp
[RequireComponent(typeof(Collider))]
public class ShieldTriggerRelay : MonoBehaviour {
    public SpaceShip owner;
    private Collider m_collider;
    // OnTriggerStay → Physics.ComputePenetration으로 depth 계산
    //              → owner.OnShieldTriggerStay(otherShip, depth)
}
```

> **주의**: 기존 함체 프리팹은 `ShieldGrid > Generate Shield` 재실행 필요 (kinematic Rigidbody 추가)

---

## 에디터 시각화 — FormationPreview

빈 씬에 `FormationPreview` 컴포넌트 추가 후 preset 연결:
- 슬롯 위치에 구체 (기함=노랑, 홀수=청록, 짝수=주황)
- `Handles.Label`로 positionIndex 표시
- CubeGrid: 격자 가이드선
- Circle: 원 가이드 링
- `m_previewShipCount`로 Circle 함선 수별 미리보기 전환

---

## 주요 파일 위치

| 역할 | 경로 |
|---|---|
| 진형 배치 데이터 타입 | `Assets/Scripts/Space/Fleet/FormationPreset.cs` |
| 에디터 시각화 | `Assets/Scripts/Space/Fleet/FormationPreview.cs` |
| Preset asset 자동 생성 | `Assets/Scripts/Space/Fleet/FormationPresetGenerator.cs` |
| 목적지 계산 / 진형 갱신 | `Assets/Scripts/Space/Fleet/SpaceFleet.cs` — `ParseCubeGrid`, `ParseCircle` |
| 이동 상태/루프/회피 | `Assets/Scripts/Space/Fleet/SpaceShip.cs` — `#region Formation Movement` |
| 실드 트리거 릴레이 | `Assets/Scripts/System/Util/ShieldGrid.cs` — `ShieldTriggerRelay` (파일 하단) |
| 상태 열거형 | `Assets/Scripts/System/CommonDefine.cs` — `FormationMoveState` |
| 크기변화 이벤트 | `Assets/Scripts/System/Events/EventManager.cs` — `OnShipBodyChanged` |

---

## 설계 결정 메모

- **positionIndex 홀수=우측, 짝수=좌측** (linear_horizontal 기준, 적함대 시점에서 바라봄)
- **CubeGrid는 위상(topology)만 정의** — 실제 거리는 런타임 함선 사이즈 누적으로 결정
- **Circle은 함선 수별 데이터 테이블** — 수식 유도 복잡, 직접 편집 가능
- **EFormationType 추가 시** Generator가 Linear 기본값으로 자동 생성 → 이후 직접 슬롯 편집
- **Arrived 상태에서 회피 미적용** — 목적지 도달 후 트리거 기반 밀어냄 방지
- **CalculateShipBounds()는 Renderer 기반** — 파티클/트레일 제외, 비활성 렌더러 포함
