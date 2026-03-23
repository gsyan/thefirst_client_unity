# Exploration 탭 UI 기획 + 존 탐험 시스템
# X-Y 형식 총 91개 존, 클리어 존 합산 자원 수확, 광고 기반 입장 정책
# 게이지 바 UI, 존 보상 수치 테이블, 진행 속도 계산 포함

## Exploration 탭 UI

### 레이아웃
```
┌──────────────────────────────────────────────────────┐
│  [Z1][Z2][Z3][Z4][Z5][Z6][Z7][Z8][Z9]               │  ← 함선 그룹(X) 탭
├──────────────────────────────────────────────────────┤
│  1-1   1-2   1-3   1-4   1-5                        │
│  1-6   1-7   1-8   1-9   1-10                       │  ← 존 그리드 (5열 × 6행)
│  ...                                                 │
│  1-26  1-27  1-28  1-29  1-30                       │
├──────────────────────────────────────────────────────┤
│  Max Collectable ( 🌿 600 )                          │
│  [게이지██░░░░░░░░] 6.3%      [Collect Mineral]     │
│                                        [Enter Zone]  │
└──────────────────────────────────────────────────────┘
```

### 탭 구성
- Z1~Z9: 함선 그룹별(X값) 탭, 각 탭에 해당 그룹의 스테이지 1~30 표시
- 클리어한 존: 밝게 표시, 미클리어: 어둡게 표시
- 선택 존: 노란 테두리 + 우측 정보 표시 (함선 수 요구, 수확량 등)

### 수확 게이지 UI — 구현 완료
- `m_harvestGaugeFill` (Image, anchorMax.x 방식): `elapsed / cap` → fillAmount
- `m_harvestGaugeText` (TMP_Text): "XX%" (1초 갱신)
- `m_harvestLimitText` (TMP_Text): 게이지 100%일 때 M/R/E/D 최대량 (0인 자원 생략)
 - 기술레벨 오를수록 cap 증가 → 같은 elapsed 기준 % 낮아지며 총량 늘어나는 효과
- 수확 버튼 클릭 → `collectZone` API → collectDateTime 갱신 → 게이지 0%로 리셋


## 존 탐험 시스템

### 존 구조 — 확정
- 이름 형식: **X-Y** (X=함선 개수 그룹, Y=스테이지, 1~30)
- 총 91개 존: Zone-0(안전지역) + 1-1 ~ 9-10 (스테이지 10 기준 / 실제 구현 30 스테이지)
- 랭킹: 클리어 존 이름을 숫자 점수로 변환해 Redis 저장 (RankingService.java)

### 수확 방식 — 확정 (구현 완료)
- 클리어한 **모든 존** 합산 수확
  - 서버: `ZoneService.collectZone()` — `min(elapsed, offlineCap)` 단순 계산, 온/오프 구분 없음
  - 클라: `UITabExploration.UpdateZoneInfo()` — clearedZones 목록으로 합산 rate 계산
- 온라인 1.5배 효율: 킬 보상으로만 구현 (시간당 적립과 무관)
- 캡 초과 시간은 손실 — 유저가 직접 수확 버튼을 눌러야 적립
- 스테이지 클리어시에는 자동 수확, 그외 자동 수확 없음 (로그인/하트비트 강제수확 모두 제거)

### 자원 도입 순서
zone 1-X, 2-X: Mineral만
zone 3-X~    : + MineralRare 수확 시작
zone 5-X~    : ME 필드 존재, 현재 값 0 (미사용)
zone 7-X~    : MD 필드 존재, 현재 값 0 (미사용)

### 존 보상 수치 (DataTableZoneEditor.cs GenerateDefaultZones — 확정)
- maxStages = 30, stageScaleFactor = 10/30 (총 자원량 유지 보정)
- stageMult = Mathf.Lerp(1.0, 1.45, (stage-1)/(maxStages-1)) (stage1=1.0x, stage30=1.45x)
- 아래 수치는 stage1 기준 실효값 (코드 내 base값 × stageScaleFactor × stage1 stageMult)

| x(함선수) | mineral/h | rare/h | kill M | kill MR |
|----------|-----------|--------|--------|---------|
| 1        | 200       | 0      | 13     | 0       |
| 2        | 400       | 0      | 25     | 0       |
| 3        | 667       | 117    | 43     | 3       |
| 4        | 1,067     | 233    | 73     | 7       |
| 5        | 1,667     | 400    | 120    | 13      |
| 6        | 2,667     | 667    | 200    | 23      |
| 7        | 4,333     | 1,067  | 333    | 40      |
| 8        | 7,000     | 1,733  | 533    | 67      |
| 9        | 11,000    | 2,833  | 867    | 107     |

각 값 × stageMult로 stage별 보정 적용 (stage30 기준 약 1.45배).

### 진행 속도 계산 (기본 3h 캡 기준)
하루 등가 수입 = mineralPerHour × 9h (오프라인 2회 × 3h + 온라인 1.5배 × 2h)

- 4번째 함선 (60,000 M + 60,000 MR): zone 3-1 MR ~117/h × 9 ≈ 1,053/day → ~57일 (zone 3-X 합산 시 단축)
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
