# 렌더링 최적화 현황 및 작업 계획

> 함선 모듈 프리팹 기반 드로우콜/GPU 최적화 작업 문서.
> 프로파일링 수치, 원인 분석, 개선 방향 기록.

---

## 현재 수치 (2026-04-11, 안전지역, 내 전함 2대만)

| 항목 | 수치 | 목표 |
|------|------|------|
| CPU | ~18ms | <10ms |
| GPU | ~25ms | <16ms |
| SetPass Calls | 31 | - |
| Draw Calls | 103 | <30 |
| Batches | 103 (배칭 0) | <30 |
| Triangles | 24.7k | - |
| Vertices | 42.6k | - |
| Shadow Casters | 36 | 0 (우주라 불필요) |

배칭 분류: Dynamic=0, Static=0, Instancing=0 → **배칭 전혀 안 됨**

---

## Body 프리팹 현황 (Assets/Resources/Prefabs/ShipModule/Body/)

| 프리팹 | GameObject 수 | MeshRenderer 수 | 비고 |
|--------|-------------|----------------|------|
| body_t1_m1 | 108 | 1 | ShieldVertex 오브젝트 대다수 |
| body_t2_m1 | 118 | 1 | |
| body_t3_m1 | 111 | 1 | |
| body_t4_m1 | 117 | 1 | |
| body_t5_m1 | 131 | 1 | |
| body_t6_m1 | 144 | 4 | |
| body_t7_m1 | 145 | 2 | |
| body_t8_m1 | 149 | 4 | |
| body_t9_m1 | 172 | 4 | |
| body_t10_m1 | 176 | 4 | |
| body_t10_m3 | 188 | 8 | m3 = 슬롯 3개짜리 대형 |
| body_t11_m1 | 189 | 4 | |
| body_t11_m3 | 198 | 8 | |
| body_t12_m1 | 204 | 8 | |
| body_t13_m1 | 219 | 8 | |
| body_t14_m1 | **223** | 8 | 가장 크고 복잡 |

### 구조 특징
- 각 GameObject의 대부분이 **ShieldVertex_N** 이름의 빈 오브젝트 (Transform + ShieldVertex MonoBehaviour)
- 실제 렌더링 담당: MeshRenderer + MeshFilter를 가진 소수의 GameObject
- 함체 1개가 씬에 스폰될 때 100~223개 Transform 계층 생성

## Beam/Missile/Hanger 프리팹 현황

- **모두 MeshRenderer=0** → 현재 별도 메시 렌더링 없음 (파티클 또는 LineRenderer 추정)
- 향후 최적화 대상에 포함 예정

---

## 원인 분석

### 1. GPU Instancing OFF (가장 큰 원인)
- 머티리얼 `MatMatal.mat`: `m_EnableInstancingVariants: 0`
- 셰이더 `Assets/Shader/Metal.shader`에는 `#pragma multi_compile_instancing` 이미 포함됨
- → **머티리얼에서 Enable GPU Instancing 체크만 하면 됨**

### 2. Shadow Caster 패스 존재
- Metal.shader에 ShadowCaster 패스 구현됨
- 일부 MeshRenderer에 CastShadows=1 오버라이드 적용된 것 확인
- 우주 배경이라 그림자가 의미 없음 → 전부 OFF 또는 셰이더에서 패스 제거 검토

### 3. ShieldVertex 오브젝트 과다
- 함체 1개당 ~100개 내외의 빈 GameObject
- Transform hierarchy가 CPU에서 매 프레임 처리됨
- 방패 쉴드 메시 동적 생성에 사용하는 버텍스 포인트 오브젝트들로 추정
- 개선 방향: 오브젝트 대신 float3[] 배열로 데이터 보관 검토

### 4. 셰이더 복잡도 (GPU 25ms 원인)
- 3개 텍스처 샘플링 (Albedo, Normal, Emission)
- PBR 라이팅 (diffuse + specular + ambient) + Additional Lights 루프
- Grid Effect: `fwidth()` 함수 사용 (DDX/DDY 파생연산 - 픽셀 셰이더에서 비쌈)
- 모바일에서 특히 무거운 구조

### 5. MeshRenderer 분산 (배칭 조건 불충족)
- 함체 하나가 최대 8개 MeshRenderer를 별도 GameObject로 가짐
- Unity Dynamic Batching은 300버텍스 이하만 가능 → 함선 메시는 초과 확실
- GPU Instancing만이 현실적인 배칭 수단

---

## 개선 방향 (우선순위 순)

### Step 1: GPU Instancing 활성화 [ ]
- `MatMatal.mat`, `MatPlastic.mat` 등 함선 머티리얼 전체 Enable GPU Instancing 체크
- 코드에서 `.material` 직접 접근 여부 확인 → WarpEffectShip, ProjectileBeam은 MPB 사용 중 (OK)
- 예상 효과: 동일 티어 함선이 여러 대 있을 때 드로우콜 대폭 감소

### Step 2: Shadow 제거 [ ]
- 우주 씬에서 그림자 불필요
- Metal.shader에서 ShadowCaster 패스 제거 또는 비활성화
- 또는 URP Renderer에서 Shadow Pass 전체 OFF
- 예상 효과: Shadow Casters 36 → 0, 드로우콜 ~30% 감소

### Step 3: 셰이더 모바일 최적화 [ ]
- Grid Effect를 `_GridIntensity > 0` 분기가 아니라 별도 셰이더 변형(keyword)으로 분리
- fwidth() 대신 texel 기반 grid 또는 texture lookup 검토
- Additional Lights 루프 → 모바일에서 0으로 설정하거나 per-vertex로 전환

### Step 4: ShieldVertex 구조 개선 [ ]
- ShieldVertex GameObject → 데이터 배열로 전환
- 씬에 함체 스폰 시 생성되는 GameObject 수를 MeshRenderer 수 정도로 줄이기

### Step 5: 함선 메시 통합 (Mesh Combine) [ ]
- 에디터 빌드 시점 또는 스폰 시 동일 머티리얼 MeshRenderer들을 CombineMeshes
- 함체당 MeshRenderer 8개 → 1개로 줄이기
- 장단점: 드로우콜 감소 vs. GPU Instancing 효율 감소 (trade-off 검토 필요)

---

## 주의 사항
- Beam/Missile 최적화는 Step 1~3 이후 별도 진행
- GPU Instancing과 Mesh Combine은 동시에 쓰면 효과가 상충될 수 있음
  - 인스턴스가 많을 때: Instancing 우세
  - 인스턴스가 적을 때: Combine 우세
- 모든 변경 후 프로파일러로 수치 비교 필수
