# 렌더링 최적화 현황 및 작업 계획

> 함선 모듈 프리팹 기반 드로우콜/GPU 최적화 작업 문서.
> 프로파일링 수치, 원인 분석, 완료 작업, 잔여 작업 기록.

---

## 프로파일링 수치

### 작업 전 (안전지역, 내 전함 2대, 빔 모듈 2개씩)
| 항목 | 수치 |
|------|------|
| CPU | ~18ms |
| GPU | ~25ms |
| SetPass Calls | 31 |
| Draw Calls / Batches | 103 / 103 |
| Dynamic / Static / Instancing 배칭 | 0 / 0 / 0 |
| Shadow Casters | 36 |
| Triangles / Vertices | 24.7k / 42.6k |

### EnergyFlame GPU Instancing 적용 후 (빔 모듈 1개씩으로 줄인 상태)
| 항목 | 수치 |
|------|------|
| SetPass Calls | 29 |
| Draw Calls / Batches | 60 / 60 |
| Instancing | Batched Draw Calls: 2 → Batches: 1 ✅ |
| Shadow Casters | 16 |
| Triangles / Vertices | 17.1k / 29.1k |

---

## 구조 파악

### 함선 에셋
- FBX 1종: `USSNFreedomClassDreadnought`
- 파츠: Bridge1~3, Engine1~3(x4 포함), EngineConnection, Hull, Module1/1A/1B/1x2/2/2x2/3/3A~C/3x6
- **FBX 파츠 모두 머티리얼 1개씩** → `USSNFreedomClassDreadnoughtBlue` (GPU Instancing ON, URP/Lit)

### Body 프리팹 (Assets/Resources/Prefabs/ShipModule/Body/)
| 프리팹 | GameObject 수 | MeshRenderer 수 |
|--------|-------------|----------------|
| body_t1_m1 | 108 | 1 |
| body_t2~t5 m1 | 110~131 | 1 |
| body_t6_m1 | 144 | 4 |
| body_t7_m1 | 145 | 2 |
| body_t8~t9 m1 | 149~172 | 4 |
| body_t10~t11 m1 | 176~189 | 4 |
| body_t10~t11 m3 | 188~198 | 8 |
| body_t12~t14 m1 | 204~223 | 8 |

- GameObject 대다수는 **ShieldVertex_N** (Transform + ShieldVertex MonoBehaviour만 있는 빈 오브젝트)
- 실제 렌더링 파츠는 FBX 중첩 프리팹 (Engine1, Bridge1 등)
- Beam/Missile/Hanger 슬롯 안의 PlaceHolder → Module1A (같은 Blue 머티리얼, 선택 가능, 모듈 배치 시 사라짐)

### 셰이더 / 머티리얼
| 머티리얼 | 셰이더 | 용도 | 비고 |
|----------|--------|------|------|
| USSNFreedomClassDreadnoughtBlue 등 5종 | URP/Lit | FBX 함선 외형 | GPU Instancing ON, SRP Batcher 우선 |
| MatEnergyFlame | EngineFlame (커스텀) | 엔진 글로우 큐브 | GPU Instancing ON ✅ (작업 완료) |
| MatBeam | BeamAdditive | 빔 발사체 | 미최적화 |
| MatParticle | - | 이펙트 3종 | - |
| ~~MatMatal~~ / ~~MatPlastic~~ | ~~Metal~~ | 미사용 | **삭제됨** |

---

## SRP Batcher vs GPU Instancing 관계

- **SRP Batcher ON** (Mobile_RPAsset, PC_RPAsset 모두 `m_UseSRPBatcher: 1`)
- SRP Batcher 호환 셰이더(`CBUFFER_START(UnityPerMaterial)`) → SRP Batcher 우선, GPU Instancing 무시
- GPU Instancing을 실제로 사용하려면 → CBUFFER 제거 후 `UNITY_INSTANCING_BUFFER` 사용 (SRP Batcher 비호환으로 전환)
- SRP Batcher: SetPass 절감 (CPU), 드로우콜 수는 그대로
- GPU Instancing: 동일 메시+머티리얼 → 드로우콜 N→1 (CPU+GPU 모두 절감)

---

## 완료된 작업

### ✅ EnergyFlame GPU Instancing
- **EngineFlame.shader**: `CBUFFER_START(UnityPerMaterial)` 제거 → `UNITY_INSTANCING_BUFFER_START(Props)` 로 교체, `#pragma multi_compile_instancing` 추가
- **MatEnergyFlame.mat**: `m_EnableInstancingVariants: 0 → 1`
- 결과: 함선 N대의 EnergyFlame(Cube 메시) → **1 드로우콜**
- WarpEffectShip.cs의 MPB(`_GlowIntensity`) 그대로 동작 — INSTANCING_BUFFER에서는 MPB가 인스턴스별 데이터로 정상 처리됨

### ✅ 메시 합치기 에디터 툴
- **CombineMeshTarget.cs** (`Assets/Scripts/System/Util/`)
  - Body 프리팹 루트에 부착
  - `List<GameObject> m_combineTargets` — 합칠 루트 오브젝트 드래그 지정
  - 지정 오브젝트 하위를 재귀적으로 MeshFilter 수집해 합침
- **HullMeshCombineEditor.cs** (`Assets/Scripts/Editor/`)
  - `CombineMeshTarget` Custom Inspector에 **Combine Meshes** 버튼
  - 합쳐진 메시 → `Assets/GeneratedMeshes/HullCombined/{prefab명}_Combined.mesh` 저장
  - 프리팹에 `Combined` 오브젝트 생성 (첫 번째 자식, MeshFilter + MeshRenderer + MeshCollider)
  - 원본 FBX MeshRenderer 비활성화
  - Prefab Editor에서 직접 열어도 동작 (PrefabStageUtility로 경로 획득)

**body_t1_m1 적용 상태:**
- `FBXs` 빈 오브젝트에 Engine1, Bridge1 묶음 → CombineMeshTarget 타깃으로 지정
- `Combined` 오브젝트 생성 확인

---

## 남은 작업

### [ ] Body 프리팹 전체 Combine 적용
- body_t2~t14 각각 프리팹 열어서 CombineMeshTarget 설정 후 Combine 실행
- 각 프리팹마다 FBX 파츠 구성 확인 필요 (파츠 수가 다름)

### [ ] 빔 LineRenderer → 메시 교체 (GPU Instancing)
- 현재: LineRenderer (동적 메시, GPU Instancing 불가)
- 방향: 단순 Quad/Plane 메시로 교체 → 방향/길이는 Transform으로 처리
- 동일 메시 + MatBeam → 전체 빔 1 드로우콜 가능
- ProjectileBeam.cs 이미 MPB 사용 중 → INSTANCING_BUFFER 방식으로 셰이더 전환 필요

### [ ] ShieldVertex 구조 개선
- 함체당 100~220개 빈 GameObject (Shield 메시 동적 생성용 버텍스 포인트)
- 방향: float3[] 배열로 데이터 보관, GameObject 제거

### [ ] Placeholder GPU Instancing 검토 (낮은 우선순위)
- Module1A (Cube): 동일 메시 + Blue 머티리얼, 모듈 배치 시 사라짐
- URP/Lit 셰이더 → SRP Batcher 우선으로 현재 GPU Instancing 불가
- 실용성 낮음 (배치 시 사라지는 오브젝트)

### [ ] 모바일 최적화 수치 재측정
- 전체 Body Combine 적용 후 실제 기기에서 GPU ms 재확인

---

## 주의 사항
- EnergyFlame(Cube), 미사일 부스터(Sphere): 같은 MatEnergyFlame, 다른 메시 → 각각 1 드로우콜 = 총 2 드로우콜
- FBX 머티리얼 5종(Blue/Grey/Beige/Red/White) 중 현재 함선은 **모두 Blue 1종** 사용 확인
- Combine 시 EnergyFlame은 대상에서 제외 (다른 머티리얼)
- Shadow는 우주 배경이라 불필요 → Combined MeshRenderer에 shadowCastingMode=Off 설정됨
