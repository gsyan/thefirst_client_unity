# Ship 탭 UI 기획 + 모듈 시스템
# 함선별 모듈 슬롯 관리, 레벨업/서브타입 추가 흐름, 좌측 리스트 + 우측 디테일 레이아웃
# SubType 7자리 인코딩 기준: type/tech/grade/ver

## Ship 탭 UI

### 레이아웃
```
┌──────────────────────────────────────────────────────────────────┐
│  [< Ship_1 >]   ⚔ ATK:15  ❤ HP:100  ⚡ SPD:50  🔧 REP:5  [...] │  ← 상단 1행
│                 ✈ ATK:0   ✈ Count:0  ✈ Launch:0                │  ← 상단 2행 (함재기 0이면 숨김)
├─────────────────────────────┬────────────────────────────────────┤
│  [Body아이콘]  [슬롯1]       │  T1 Body.Std                      │
│  [Beam아이콘]  [슬롯1][슬롯2]│  Lv. 1                            │
│  [Msle아이콘]  [슬롯1]       │  ❤ 100                            │
│  [Eng아이콘]   [슬롯1]       │  🔧 5                             │
│  [Hngr아이콘]  [슬롯1]       │                                   │
│                             │  [ Level Up Module (100 M) ]       │
│                             │  [ Manage Module Type ▶ ]          │
└─────────────────────────────┴────────────────────────────────────┘
```

### 상단 헤더
- `[<]` `[>]` 버튼으로 함대 내 함선 순환 선택 (Ship 탭 내에서 함선 전환 가능)
- 현재 함선 이름 중앙 표시
- **1행**: ⚔ ATK / ❤ HP / ⚡ SPD / 🔧 REP (해당 함선 기준) + `[...]` 상세 팝업
- **2행**: ✈ ATK / ✈ Count / ✈ Launch (함재기 0이면 행 전체 숨김)

### Module Map (좌측 2열 리스트)
- 좌측 열: 모듈 타입 아이콘 (Body / Beam / Missile / Engine / Hanger 순서)
- 우측 열: 해당 타입의 슬롯 버튼을 개수에 맞게 가로 정렬
  - 슬롯 색상: 잠금(빨강) / 해금(초록) / 선택(노란 테두리)
  - 선택된 슬롯의 모듈 정보가 우측 패널에 표시됨

### Module Detail 카드 (우측)
- 선택된 모듈 정보를 배경 Panel + 테두리 카드 형식으로 표시
- 구조:
  - ModuleHeader (고정): 모듈 타입 · 서브타입 · 레벨
  - ScrollView (가변): 스탯 목록 (RowLabelValue) — 내용이 길어질 경우 스크롤
  - ButtonArea (고정 하단): [Level Up (비용)] / [Manage Module Type ▶]
- Content에 ContentSizeFitter(Vertical Preferred) + VerticalLayoutGroup 적용
- 코드 변경 없음 (m_moduleStatsContainer = ScrollView의 Content 오브젝트)



## 모듈 시스템

### SubType 7자리 인코딩 — 확정
X  XX  XX  XX
│  │   │   └── ver    (01~99)
│  │   └────── grade  (01=std, 02=adv)
│  └────────── tech   (01=t1, 02=t2...)
└───────────── type   (1=body, 2=engine, 3=beam, 4=missile, 5=hanger)

body_t1_std_ver1 = 1010101
body_t1_adv_ver1 = 1010201
engine_t1_std_ver1 = 2010101
파싱: `type=val/1000000, tech=(val/10000)%100, grade=(val/100)%100, ver=val%100`

### 모듈 진행 구조 — 설계 확정
[슬롯 unlock]  슬롯 타입별 고정 비용 → 기본(std Lv.1) 모듈 자동 장착
    ↓
[모듈 레벨업]  현재 모듈 Lv.1 → Lv.10 (max)
    ↓ 조건: 현재 모듈 max level
[모듈 서브 타입 추가] subTypeAddCost (슬롯 단위) → 추가된 서브 타입 (ex:adv) Lv.1로 교체
    ↓
[모듈 레벨업]  adv Lv.1 → Lv.10 (max)
    ↓ 이후 tier 반복...

- 모듈 서브 타입 추가: 슬롯 단위 1회 비용, 이후 해당 슬롯에서 자유 교체 (비용 없음, 레벨 조건 없음)
- 슬롯 unlock / 레벨업 / 모듈 추가: 모두 모듈 인스턴스별 독립
- 신규 서브타입 잠금해제 조건: **기술 레벨 ≥ 서브타입 tech tier** + 현재 모듈 max level + subTypeAddCost 납부 (최초 1회)
- 이미 보유한 서브타입으로 재교체: 레벨/비용 조건 없음 (ShipModuleLevel 이력 존재 = 무료 자유 교체)

### 슬롯 unlock 비용 — 확정
- 5,000 M 고정 (슬롯 타입 무관)
- unlock 즉시 기본(std Lv.1) 모듈 자동 장착

### 모듈 레벨업 (datatable_module.csv — 확정)
- 모듈 인스턴스별 독립, 최대 Lv.10
- std: cost_m만 사용 / adv: cost_m(std 동일) + cost_mr(2배)

| level | std cost_m | adv cost_m | adv cost_mr |
|-------|-----------|-----------|------------|
| 1     | 100       | 100       | 600        |
| 2     | 150       | 150       | 900        |
| 3     | 225       | 225       | 1,350      |
| 4     | 338       | 338       | 2,025      |
| 5     | 506       | 506       | 3,038      |
| 6     | 759       | 759       | 4,556      |
| 7     | 1,139     | 1,139     | 6,834      |
| 8     | 1,709     | 1,709     | 10,253     |
| 9     | 2,563     | 2,563     | 15,378     |
| 10    | 3,844     | 3,844     | 23,066     |
| 합계  | ~11,333M  | ~11,333M  | ~68,000MR  |

### 모듈 추가 비용 (subTypeAddCost) — 확정
- 슬롯 단위 1회 납부, 이후 해당 슬롯에서 자유 교체 (레벨 무관)
- 신규 잠금해제 조건 (AND):
  1. **기술 레벨 ≥ 서브타입 tech tier** — `EModuleSubType` 7자리 인코딩에서 `GetTechTier()` 파싱 (`(val/10000)%100`)
  2. 현재 모듈 Lv.10(max)
  3. subTypeAddCost 납부
- 이미 보유한 서브타입으로 재교체: 기술레벨/레벨/비용 조건 없음 (자유 교체)
- std→adv 추가: 5,000 MR / 슬롯
- 추가 시 레벨 리셋: adv Lv.1로 시작 (adv max > std max 성능 보장)
- 등록 이력: ShipModuleLevel 레코드 존재 여부로 판단 (신규 테이블 불필요)
- 현재 모든 서브타입 tech tier = 1 (`body_t1_std_ver1 = 1010101` 등)
