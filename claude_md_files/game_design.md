# 게임 기획 메모

## 게임 개요
- 장르: 우주 함대 전투 시뮬레이션
- 핵심 루프: 존 탐험 → 자원 수집 → 함선 추가/모듈 강화 → 더 높은 존 탐험
- PvP 있음


## 재화 시스템

### 4종 자원
| 자원 | 존 수확 시작 | 현재 용도 |
|------|-------------|-----------|
| Mineral | 전 구간 | 함선 추가, 슬롯 unlock, std 모듈 레벨업 |
| MineralRare | zone 3-X 이상 | 함선 추가(4번째~), adv 모듈 레벨업, adv 모듈 추가(슬롯 1회) |
| MineralExotic | 미사용 (현재 0) | 추후 콘텐츠 전용 |
| MineralDark | 미사용 (현재 0) | 추후 콘텐츠 전용 |

ME/MD는 zone 필드는 존재하나 현재 값 0. 추후 새 콘텐츠 추가 시 활성화.

### 자원 소모처 구조
1. 함선 추가          → addShipCosts (M + MR만 사용)
2. 모듈 슬롯 unlock   → 5,000 M 고정/슬롯
3. 모듈 레벨업        → upgradeCost (std=M, adv=MR 중심)
4. 모듈 서브타입 추가  → subTypeAddCost (슬롯 단위 1회, MR)


## 함선 시스템

### 함선 구조
- 함체(body) module 과 body 에 장작된 beam, missile, hanger, engine 모듈로 이루어짐
- 함체 모듈은 다른 module을 설치할 수 있는 module slot을 가짐
- 초기 기본 지급 함선은 함체(body) + 빔 모듈 1개(unlock된 상태) + 엔진 모듈 1개(unlock된 상태)
- 초기 기본 지급 함선의 함체의 module slot 은 빔×2, 미사일×1, 격납고×1, 엔진×1 (총 5슬롯)
- module slot unlock 비용: 5,000 M 고정/슬롯
- 함체의 sub type 추가는 module slot 개수의 확대로 이어질 수 있음

### addShipCosts (DataTableConfig.cs) — 확정
CostStruct(techLevel, mineral, mineralRare, 0, 0)
idx 0: ( 0,         0,         0)  ← 초기 함선 (무료)
idx 1: ( 1,     5,000,         0)  ← 2번째 (~1일, M만)
idx 2: ( 2,    10,000,         0)  ← 3번째 (~0.6일, M만)
idx 3: ( 4,    60,000,    60,000)  ← 4번째 (~15일, MR 게이트 시작)
idx 4: ( 6,   170,000,   170,000)  ← 5번째 (~15일)
idx 5: ( 8,   370,000,   370,000)  ← 6번째 (~15일)
idx 6: (10, 1,400,000, 1,400,000)  ← 7번째 (~30일)
idx 7: (12, 2,500,000, 2,500,000)  ← 8번째 (~30일)
idx 8: (14, 4,000,000, 4,000,000)  ← 9번째 (~30일)
ME/MD는 현재 미사용 (추후 콘텐츠 전용)

- M은 zone 수입도 비례 증가 → 실질 게이트는 MR
- 4~6번째: MR 누적 수입 기준 각 ~15일 / 7~9번째: 각 ~30일 목표
- 기술레벨 게이팅 미구현 (향후 필요)

### 운용 구간 계획
- 2~3척 / 4~5척 / 6~7척 / 8~9척 (구간별 기술레벨 제한 예정)

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

- 모듈 서브 타입 추가: 슬롯 단위 1회 비용, 이후 해당 슬롯에서 자유 교체 (비용 없음)
- 슬롯 unlock / 레벨업 / 모듈 추가: 모두 모듈 인스턴스별 독립
- 상위 모듈 적용 조건: 현재 모듈 max level + subTypeAddCost 납부 (최초 1회)

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
- 슬롯 단위 1회 납부, 이후 해당 슬롯에서 자유 교체
- 추가 조건: 현재 모듈 Lv.10(max)
- std→adv 추가: 5,000 MR / 슬롯
- 추가 시 레벨 리셋: adv Lv.1로 시작 (adv max > std max 성능 보장)
- 등록 이력: ShipModuleLevel 레코드 존재 여부로 판단 (신규 테이블 불필요)

## 기술레벨 시스템
- 목표 레벨: 2 / 4 / 6 / 8 (1차)
- 역할: 함선 추가 제한 조건, 오프라인 캡 확장
- 구현 완료: datatable_research.csv의 tech_level_N 노드 기반, FleetService.researchTechLevel 처리

## 오프라인 적립 시간 시스템
기본:                    3시간
기술레벨 2 달성:          +1시간 (4시간)
기술레벨 4 달성:          +1시간 (5시간)
기술레벨 6 달성:          +1시간 (6시간)
기술레벨 8 달성:          +1시간 (7시간)
함대 지휘관 패스 (월정액): 24시간

### 온라인/오프라인 구분 방식 — 구현 완료
- **온라인**: 앱 포그라운드 활동 (캡 없이 전량 적립)
- **오프라인**: 앱 백그라운드/종료 (offlineCap 적용)
- 구분 기준: `lastOnlineAt` (Character 엔티티)
  - 모든 인증된 API 호출 성공 시 갱신 (30s 스로틀) → `OnlineActivityInterceptor`
  - grace period: 60s (N - L ≤ 60s면 온라인 취급)
  - 백그라운드 전환 시 하트비트 중단 → `OnApplicationPause`
- 수확 계산 (`calcCreditedSeconds`):
  - C→L 구간: 온라인, 캡 없음
  - L→N 구간: 오프라인, offlineCap 적용
  - N-L ≤ 60s: 전 구간 온라인 취급
  - lastOnlineAt 없음: 전 구간 오프라인 fallback
- 수확 버튼 클릭 시 하트비트 선발송 후 collect — 네트워크 불안정 보정

---

## 존 탐험 시스템

### 존 구조 — 확정
- 이름 형식: **X-Y** (X=함선 개수 그룹, Y=스테이지, 1~10)
- 총 91개 존: Zone-0(안전지역) + 1-1 ~ 9-10
- 랭킹: 클리어 존 이름을 숫자 점수로 변환해 Redis 저장 (RankingService.java)

### 수확 방식 — 확정 (구현 완료)
- 클리어한 **모든 존** 각각 수확 후 합산 ← 구현 완료
  - 서버: `ZoneService.collectZoneResources()` — `getAllZoneConfigsUpTo(clearedZone)`으로 전 존 순회 합산
  - 클라: `UITabExploration.UpdateZoneInfo()` — `DataTableZone.GetAllZonesUpTo()`로 합산 rate 계산
- 오프라인: mineralPerHour 기반 자동 수확 (적립 상한 적용)
- 온라인: 1.5배 효율 (킬 보상으로 구현)

### 자원 도입 순서
zone 1-X, 2-X: Mineral만
zone 3-X~    : + MineralRare 수확 시작
zone 5-X~    : ME 필드 존재, 현재 값 0 (미사용)
zone 7-X~    : MD 필드 존재, 현재 값 0 (미사용)

### 존 보상 수치 (DataTableZoneEditor.cs GenerateDefaultZones — 확정)
stage 보정: `stageMult = 1 + (stage-1) × 0.05` (stage1=1.0x, stage10=1.45x)

| x(함선수) | mineral/h | rare/h | kill M | kill MR |
|----------|-----------|--------|--------|---------|
| 1        | 600       | 0      | 40     | 0       |
| 2        | 1,200     | 0      | 75     | 0       |
| 3        | 2,000     | 350    | 130    | 10      |
| 4        | 3,200     | 700    | 220    | 22      |
| 5        | 5,000     | 1,200  | 360    | 40      |
| 6        | 8,000     | 2,000  | 600    | 70      |
| 7        | 13,000    | 3,200  | 1,000  | 120     |
| 8        | 21,000    | 5,200  | 1,600  | 200     |
| 9        | 33,000    | 8,500  | 2,600  | 320     |

각 값 × stageMult로 stage별 보정 적용.

### 진행 속도 계산 (기본 3h 캡 기준)
하루 등가 수입 = mineralPerHour × 9h (오프라인 2회 × 3h + 온라인 1.5배 × 2h)

- 4번째 함선 (20,000 M + 5,000 MR): zone 3 MR ~428/h × 9 ≈ 3,852/day → ~1.3일
- 진행 게이트는 MR (Mineral은 수입도 비례 증가해 게이트 역할 약함)

### 존 입장 조건
- 구현 완료: zone X-Y 진입 시 함선 X척 이상 필요
  - 클라: OnTryZoneClicked에서 ParseZoneRequiredShips로 체크 → 부족 시 메시지 표시
  - 서버: clearZone에서 activeFleet의 ship count 검증 → ZONE_CLEAR_FAIL_INSUFFICIENT_SHIPS

### 광고(리워드) 존 입장 원칙
- 존 입장 시 리워드 광고를 시청하는 것이 기본 흐름
- **게임 자체가 인터넷 필수** → 유저가 광고 우회 목적으로 네트워크를 끊는 상황은 없다고 전제
- 따라서 광고 실패는 항상 네트워크 불안정 또는 AdMob 서버 문제로 간주 → 유저 책임 없음

#### 입장 허용/불허 판단 기준 (EAdResult)
| 상황 | EAdResult | 입장 |
|---|---|---|
| 광고 완시청 + 보상 수령 | Rewarded | ✅ 허용 |
| 광고 표시 실패 (네트워크 등) | Failed | ✅ 허용 |
| 유저가 광고를 직접 닫음 | UserClosed | ❌ 불허 |
| 광고 미준비 (로드 안됨) | — | ✅ 허용 + 즉시 재로드 |

#### 구현 포인트
- `AdManager._rewardEarned` 플래그로 `OnAdFullScreenContentClosed` 시 보상 여부 판별
- `OnAdFullScreenContentFailed` → `EAdResult.Failed`
- 광고 미준비 입장 시 `AdManager.RequestLoad()` 즉시 호출



## UI 기획 — 확정

### Fleet 탭 

#### 레이아웃
┌──────────────────────────────────────────┐
│ [Fleet Stats 2행]                        │  상단 고정
├──────────────────────────────────────────┤
│  [#1]      [#2]      [#3]               │
│  ATK:120   ATK:110   ATK:100            │
│  HP게이지  HP게이지  HP게이지               │  함선 그리드
│                                          │
│  [#4]      [+ 추가]                      │
│  ATK:120   5,000 M                       │
│  HP게이지                                 │
├──────────────────────────────────────────┤
│  Formation: Linear Horizontal  [교체 ▶] │  하단 고정
└──────────────────────────────────────────┘

#### Fleet Stats (상단 2행)
- 1행: 함선 전투력 합산 — ⚔ ATK  ❤ HP  ⚡ SPD  🔧 REPAIR
- 2행: 함재기 능력 합산 — ✈ ATK  ✈ Count  ✈ Launch (함재기 0이면 숨김)
- 아이콘: 단기는 TMP 유니코드 문자, 나중에 game-icons.net PNG로 교체
- 향후 표시 정보가 많아지면 Stats 영역 클릭 시 상세 팝업으로 분리

#### 함선 카드 (ShipSelector 프리팹)
- 배경: 함선 썸네일 이미지 (현재는 단색 사각형)
- 텍스트: 함선 이름(shipName) + ATK 수치
- HP 게이지: HpBarFill Image (anchorMax.x 방식으로 비율 표현)
  - 색 단계 미적용 (현재 단색) — 추후 Image 색상 단계 추가 가능
- HP 수치: "현재 / 최대" 형식 (m_textHp)
- 선택 시: Outline 외곽선 (노란색)
- Add Ship 카드: 비용 표시 (m_textAddShipCost, "M X,XXX")

#### Formation (하단 고정 바)
- 현재 진형명 텍스트 + [교체 ▶] 버튼 1개
- 교체 버튼 클릭 → 진형 목록 팝업 (기존 스크롤뷰 내용을 팝업으로 이동)

#### 아이콘 리소스
- 단기: TMP 유니코드 (⚔ ❤ ⚡ 🔧 ✈)
- 장기: game-icons.net (CC BY 3.0) SVG → PNG 변환 후 Sprite 교체
  - 검색 키워드: sword, heart, lightning, wrench, aircraft, shield


### Ship 탭 — 확정

#### 레이아웃
```
┌──────────────────────────────────────────────────────────────────┐
│  [< Ship_1 >]   ⚔ ATK:15  ❤ HP:100  ⚡ SPD:50  🔧 REP:5       │  ← 상단 1행
│                 ✈ ATK:0   ✈ Count:0  ✈ Launch:0               │  ← 상단 2행 (함재기 0이면 숨김)
├─────────────────────────────┬────────────────────────────────────┤
│      MODULE MAP             │      SELECTED MODULE               │
│                             │  ┌──────────────────────────────┐  │
│       [B] [B]               │  │  Beam  ·  std  ·  Lv.3       │  │
│        │   │                │  │  ──────────────────────────  │  │
│  [M]──[BODY]──[E]           │  │  ATK       15                │  │
│            │                │  │  Level     3 / 10            │  │
│           [H]               │  └──────────────────────────────┘  │
│                             │                                    │
│                             │  [ Level Up Module  (150 M) ]      │
│                             │  [ Manage Module Type ▶ ]          │
└─────────────────────────────┴────────────────────────────────────┘
```

#### 상단 헤더
- `[<]` `[>]` 버튼으로 함대 내 함선 순환 선택 (Ship 탭 내에서 함선 전환 가능)
- 현재 함선 이름 중앙 표시
- 1행: ⚔ ATK / ❤ HP / ⚡ SPD / 🔧 REP (해당 함선 기준)
- 2행: ✈ ATK / ✈ Count / ✈ Launch (함재기 0이면 숨김) — Fleet 탭과 동일 구조

#### Module Map (좌측)
- Body를 중앙에 배치, 슬롯들이 주변에 연결된 계층 레이아웃
- Body: 상단 배치, Beam/Missile 좌우, Engine/Hanger 하단
- 각 슬롯 버튼: 잠금(빨강) / 해금(초록) / 선택(노란 테두리) 색상 유지

#### Module Detail 카드 (우측)
- 선택된 모듈 정보를 배경 Panel + 테두리 카드 형식으로 표시
- 구조:
  - ModuleHeader (고정): 모듈 타입 · 서브타입 · 레벨
  - ScrollView (가변): 스탯 목록 (RowLabelValue) — 내용이 길어질 경우 스크롤
  - ButtonArea (고정 하단): [Level Up (비용)] / [Manage Module Type ▶]
- Content에 ContentSizeFitter(Vertical Preferred) + VerticalLayoutGroup 적용
- 코드 변경 없음 (m_moduleStatsContainer = ScrollView의 Content 오브젝트)


## 전투 배속 시스템

### 개요
- 전투 중 게임 속도를 조절하는 배속 버튼 — `UIPanelCameraView`에 포함 (전투 중에만 표시)
- 버튼 클릭 시 단계별 순환: **x0.5 → x1.0 → x1.5 → x2.0 → x3.0 → x0.5**
- 구현: `GameSpeedController` (static class) — `Time.timeScale` 직접 조작

### 배속 적용 범위
- **적용됨**: 전투 연출 (함선 이동, 발사체, 이펙트, 애니메이션)
- **간접 적용됨**: 적함 격추 킬 보상 — 1킬당 지급 금액은 고정이지만, 배속으로 전투가 빨리 끝나므로 **단위 실시간당 킬 보상 효율 증가**
  - 흐름: `EnemyFleetKilled` 이벤트 → 서버 `KillZoneEnemy` API → 자원 지급
- **적용 안됨**: 존 시간당 수확량(mineralPerHour) — 서버에서 실제 경과 시간(elapsedSeconds) 기준으로 계산하므로 `Time.timeScale`과 무관

### 오디오 피치 연동
- 배속에 따라 오디오 피치도 함께 변경 (EventManager의 GameSpeedChanged 이벤트로 전파)
- 피치 단계: 0.75 / 1.0 / 1.20 / 1.40 / 1.60

### 배속 유지 정책
- 전투 종료 시 `Reset()` — timeScale만 1.0 복원, **인덱스는 유지** → 다음 전투에서 이전 배속 그대로 재사용
- 전투 시작 시 `RestoreSpeed()` — 저장된 인덱스 기준으로 배속 복원

--------------------------------------------------------------------------------------------------------------------------------

## 설계 및 미결 목록 - 지급
- [ ] UI 및 UX 개선 작업 (Ship 탭 개선 — 레이아웃 확정, 구현 필요)
- [ ] ME/MD 용도 콘텐츠 기획 (현재 미사용)
- [ ] 외부 라이센스 표시 (game-icons.net)


--------------------------------------------------------------------------------------------------------------------------------

## 작업 목록


