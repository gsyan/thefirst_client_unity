# Fleet 탭 UI 기획 + 함선 시스템 + 기술레벨 시스템 + 자원 적립 캡
# 함선 추가 비용(addShipCosts), 기술레벨별 자원 캡, Fleet 탭 레이아웃 정의
# Manage Ship(UITabShip 이동) / Focus Repair(추후 구현) 버튼 포함

## Fleet 탭 UI

### 레이아웃
```
┌──────────────────────────────────────────────────────┐
│  ⚙N  🚀Xh  A(techLv)  [...]  [Lv.N→N+1]            │  ← 1행: 함대 요약
│  ⚔ATK  ❤HP  ⚡SPD  🔧REPAIR  [...]                  │  ← 2행: 함선 전투력 합산
│  ✈ATK  ✈Count  ✈Launch                              │  ← 3행: 함재기 (0이면 숨김)
├─────────────────────────────────┬────────────────────┤
│  [Ship_1]  [🔒]  [🔒]          │  Manage Ship       │
│  ⚔15                           │  Focus Repair      │
│  HP 100/100                     │                    │
│                                 │                    │
│  [🔒]      [🔒]  [🔒]          │                    │
│  [🔒]      [🔒]  [🔒]          │                    │
├─────────────────────────────────┴────────────────────┤
│  Formation: Linear Horizontal  [Change Formation]    │  하단 고정
└──────────────────────────────────────────────────────┘
```

### Fleet Stats 상단 행 구성
- **1행 — 함대 요약**
  - ⚙N: 함대 내 함선 수
  - 🚀Xh: 수리 잔여 시간 또는 연구 진행 시간
  - A(N): 현재 기술레벨
  - `[...]`: 상세 팝업 버튼
  - `[Lv.N→N+1]`: 기술레벨 업그레이드 버튼 (조건 충족 시 활성화)
- **2행 — 함선 전투력 합산**: ⚔ ATK / ❤ HP / ⚡ SPD / 🔧 REPAIR + `[...]` 상세 팝업
- **3행 — 함재기 합산**: ✈ ATK / ✈ Count / ✈ Launch (함재기 0이면 행 전체 숨김)

### 함선 그리드
- 해금 슬롯: 함선 카드 (이름 + ⚔ATK + HP 게이지 "현재/최대")
- 잠금 슬롯(🔒): 클릭 시 상황에 맞는 추가 로직 작동
  - 함선 추가 비용 충족 → 함선 추가 확인 팝업
  - 기술레벨 미달 → 기술레벨 필요 안내
- 선택 시: 노란 Outline 외곽선

### 우측 액션 버튼
- **Manage Ship**: 선택 함선을 Ship 탭에서 선택한 것과 동일한 효과 → UITabShip으로 이동
- **Focus Repair**: 집중 수리 (추후 구현 예정 — 자원 소모로 HP 즉시 회복)

### Formation (하단 고정 바)
- 현재 진형명 텍스트 + [Change Formation] 버튼
- 버튼 클릭 → 진형 목록 팝업

### 아이콘 리소스
- 단기: TMP 유니코드 (⚔ ❤ ⚡ 🔧 ✈)
- 장기: game-icons.net (CC BY 3.0) SVG → PNG 변환 후 Sprite 교체
  - 검색 키워드: sword, heart, lightning, wrench, aircraft, shield


---

## 함선 시스템

### 함선 구조
- 함체(body) module 과 body 에 장착된 beam, missile, hanger, engine 모듈로 이루어짐
- 함체(body) 모듈은 다른 module을 설치할 수 있는 module slot을 가짐
- 초기 기본 지급 함선은 함체(body) + 빔 모듈 1개(unlock된 상태) + 엔진 모듈 1개(unlock된 상태)
- 초기 기본 지급 함선의 함체의 module slot 은 빔×2, 미사일×1, 격납고×1, 엔진×1 (총 5슬롯)
- module slot unlock 비용: 1 M 고정/슬롯
- 함체의 sub type 추가는 module slot 개수의 확대로 이어질 수 있음

### addShipCosts (DataTableConfig.cs) — 수치 재검토 필요
CostStruct(techLevel, mineral)
idx 0: ( 0, 0)  ← 초기 함선 (무료)
idx 1: ( 1, 10)  ← 2번째
idx 2: ( 2, 10)  ← 3번째
idx 3: ( 4, 10)  ← 4번째
idx 4: ( 6, 10)  ← 5번째
idx 5: ( 8, 10)  ← 6번째
idx 6: (10, 10)  ← 7번째
idx 7: (12, 10)  ← 8번째
idx 8: (14, 10)  ← 9번째

### 운용 구간 계획
- 2~3척 / 4~5척 / 6~7척 / 8~9척 (구간별 기술레벨 제한 예정)

### 기함 및 함급 성장 설계
- **기함(Flagship)**: 게임 시작 시 초기 지급되는 유일한 함선, 항상 진형 중심부 배치
  - 최대 성장: 드레드노트급 (Unity scale 기준 약 15×15×50 unit)
  - **삭제 불가** (Reset Ship 버튼 비활성화)
- **추가 함선**: 기함 이하 함급까지만 성장 가능 (배틀쉽/크루저/프리깃 등)
  - 최소 함급(프리깃): Unity scale 기준 약 2×1×4 unit
  - Reset Ship으로 모든 투자 환급 후 삭제 가능
- **진형 구도**: 거대한 기함 중심 + 그보다 작은 호위 함선들이 주변 포진
  - 크기 차이가 기함의 위압감을 시각적으로 표현하는 요소로 활용
