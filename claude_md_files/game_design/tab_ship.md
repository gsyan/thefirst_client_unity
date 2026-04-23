# Ship 탭 UI 기획 + 모듈 시스템
# 함선별 모듈 슬롯 관리, 레벨업/서브타입 추가/리셋 흐름, 좌측 리스트 + 우측 디테일 레이아웃
# SubType 7자리 인코딩 기준: type/tech/grade/ver

## Ship 탭 UI

### 레이아웃
```
┌──────────────────────────────────────────────────────────────────┐
│  [< Ship_1 >]   ⚔ ATK:15  ❤ HP:100  ⚡ SPD:50  🔧 REP:5  [...] │  ← 상단 1행
│                 ✈ ATK:0   ✈ Count:0  ✈ Launch:0                │  ← 상단 2행 (함재기 0이면 숨김)
│                                              [ Reset Ship ]     │  ← 기함(Ship_1) 이면 비활성화
├─────────────────────────────┬────────────────────────────────────┤
│  [Body아이콘]  [슬롯1]       │  T1 Body.Std                      │
│  [Beam아이콘]  [슬롯1][슬롯2]│  Lv. 1                            │
│  [Msle아이콘]  [슬롯1]       │  ❤ 100                            │
│  [Eng아이콘]   [슬롯1]       │  🔧 5                             │
│  [Hngr아이콘]  [슬롯1]       │                                   │
│                             │  💰 투자: M 450 / PM 0 / TM 0     │  ← 모듈 투자 이력 표시
│                             │  [ Level Up Module ]               │
│                             │  [ Manage Module Type ▶ ]          │
│                             │  [ Reset Module ]                  │  ← 잠금 슬롯이면 숨김
└─────────────────────────────┴────────────────────────────────────┘
```

### 상단 헤더
- `[<]` `[>]` 버튼으로 함대 내 함선 순환 선택 (Ship 탭 내에서 함선 전환 가능)
- 현재 함선 이름 중앙 표시
- **1행**: ⚔ ATK / ❤ HP / ⚡ SPD / 🔧 REP (해당 함선 기준) + `[...]` 상세 팝업
- **2행**: ✈ ATK / ✈ Count / ✈ Launch (함재기 0이면 행 전체 숨김)
- **[Reset Ship]**: 헤더 우측 고정. 기함(positionIndex == 0) 이면 비활성화

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
  - InvestInfo (고정): 투자 이력 — `💰 투자: M xxx / PM xxx / TM xxx` (0인 재화 생략)
  - ButtonArea (고정 하단):
    - 잠금 슬롯: [Unlock Module (1 M)]
    - 해금 + 레벨업 가능: [Level Up Module] / [Reset Module]
    - 해금 + 최대레벨: [Manage Module Type ▶] / [Reset Module]
- Content에 ContentSizeFitter(Vertical Preferred) + VerticalLayoutGroup 적용


## 리셋 시스템

### Reset Module
- 선택된 슬롯 1개 대상
- 동작:
  1. 해당 슬롯 레벨 → 1로 리셋
  2. 해당 슬롯의 `unlockedSubTypes` 목록 초기화 (처음부터 언락한 적 없는 상태)
  3. 투자된 M / PM / TM 전액 각 재화풀로 환급 (unlock 비용 + subTypeAdd 비용 + 레벨업 비용 포함)
- confirm 팝업: 환급될 M/PM/TM 금액 표시 후 확인
- 잠금 슬롯(Placeholder)에서는 버튼 표시 안 함

### Reset Ship
- 해당 함선의 **모든 슬롯** 대상 (body 포함)
- 동작:
  1. 함선 내 모든 슬롯 Reset Module 일괄 처리 (투자분 전액 환급)
  2. 함선 삭제
- 기함(positionIndex == 0): **삭제 불가** — [Reset Ship] 버튼 비활성화
- confirm 팝업: 함선 이름 + 총 환급될 M/PM/TM 금액 표시 후 확인


## 모듈 시스템

### SubType 7자리 인코딩 — 확정
X  XX  XX  
│   │   └───── model  (01=m1, 02=m2)
│   └───────── tech   (01=t1, 02=t2...)
└───────────── type   (1=body, 2=beam, 3=missile, 4=hanger)

body_t1_m1 = 10101
body_t1_m2 = 10102
파싱: `type=val/10000, tech=(val/100)%100, model=val%100`

### 모듈 진행 구조 — 설계 확정
[슬롯 unlock]  1 M 고정 비용 → 기본(std Lv.1) 모듈 자동 장착
    ↓
[모듈 레벨업]  현재 모듈 Lv.1 → Lv.10 (max)
    ↓ 조건: 현재 모듈 max level
[모듈 서브 타입 추가] 10 M 고정 (슬롯 단위) → 추가된 서브 타입 (ex:adv) Lv.1로 교체
    ↓
[모듈 레벨업]  adv Lv.1 → Lv.10 (max)
    ↓ 이후 tier 반복...

- 모듈 서브 타입 추가: 슬롯 단위 1회 비용, 이후 해당 슬롯에서 자유 교체 (비용 없음, 레벨 조건 없음)
- 슬롯 unlock / 레벨업 / 모듈 추가: 모두 모듈 인스턴스별 독립
- 신규 서브타입 잠금해제 조건: **기술 레벨 ≥ 서브타입 tech tier** + 현재 모듈 max level + 10 M 납부 (최초 1회)
- 이미 보유한 서브타입으로 재교체: 레벨/비용 조건 없음 (unlockedSubTypes 이력 존재 = 무료 자유 교체)

### 슬롯 unlock 비용 — 확정
- 1 M 고정 (슬롯 타입 무관)
- unlock 즉시 기본(std Lv.1) 모듈 자동 장착

### 모듈 레벨업 비용 (datatable_module.csv)
- 모듈 인스턴스별 독립, 최대 Lv.10
- std / adv 구분 없이 M 단독 소모 (부족 시 PM→TM 자동 보충)

| level | cost_m |
|-------|--------|
| 1     | 100    |
| 2     | 150    |
| 3     | 225    |
| 4     | 338    |
| 5     | 506    |
| 6     | 759    |
| 7     | 1,139  |
| 8     | 1,709  |
| 9     | 2,563  |
| 10    | 3,844  |
| 합계  | ~11,333 |

### 모듈 추가 비용 (subTypeAddCost) — 확정
- 10 M 고정, 슬롯 단위 1회 납부, 이후 해당 슬롯에서 자유 교체
- 신규 잠금해제 조건 (AND):
  1. **기술 레벨 ≥ 서브타입 tech tier** — `EModuleSubType` 7자리 인코딩에서 `GetTechTier()` 파싱 (`(val/10000)%100`)
  2. 현재 모듈 Lv.10(max)
  3. 10 M 납부
- 이미 보유한 서브타입으로 재교체: 기술레벨/레벨/비용 조건 없음 (자유 교체)
- 추가 시 레벨 리셋: adv Lv.1로 시작
- 등록 이력: `ModuleInfo.unlockedSubTypes` 목록 존재 여부로 판단
- 현재 모든 서브타입 tech tier = 1 (`body_t1_m1 = 10101` 등)
