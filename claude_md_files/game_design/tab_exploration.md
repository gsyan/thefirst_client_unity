# Exploration 탭 UI 기획 + 존 탐험 시스템
# X-Y 형식 존 구조, 스테이지 클리어 시 M 즉시 지급, 광고 기반 입장 정책
# UI 레이아웃: 스크린샷 기반 추후 정리 예정

## Exploration 탭 UI

### UI 레이아웃
- 추후 스크린샷 기반으로 정리 예정

## 존 탐험 시스템

### 존 구조 — 확정
- 이름 형식: **X-Y** (X=함선 개수 그룹, Y=스테이지, 1~5)
- 총 46개 존: Zone-0(안전지역) + 1-1 ~ 9-5
- 랭킹: 클리어 존 이름을 숫자 점수로 변환해 Redis 저장 (RankingService.java)

### 자원 획득 방식 — 확정
- 스테이지 클리어 시 M 즉시 지급 (직접 보상)
- 자동 적립 없음, 수확 버튼 없음
- PM/TM은 존 탐사와 무관 (PVP/구매 전용)

### 존 보상 수치
- 실제 수치는 `Assets/Resources/DataTable/Zone/datatable_zone_stage.csv` 기준
- stage는 1~5, zone 그룹(x)이 높을수록 보상 증가

### 존 입장 조건
- 구현 완료: zone X-Y 진입 시 함선 X척 이상 필요
  - 서버: clearZone에서 activeFleet의 ship count 검증 → ZONE_CLEAR_FAIL_INSUFFICIENT_SHIPS
- 구현 완료: 클리어된 존(isRestored=false)은 재입장 불가
  - 클라: 선택 시 Enter 버튼 비활성화 (ApplyZoneStageSelection)
  - 서버: clearZoneStage에서 이중 차단 → ZONE_ALREADY_CLEARED(111303)

### 적 수복 시스템 — 구현 완료
- 대상: zone 2 이상의 클리어된 스테이지
- 주기: 24시간마다 클리어된 zone 2+ 스테이지 중 랜덤 1개가 비클리어(isRestored=true) 상태로 전환
- 타이머 세팅: zone 2+ 최초 클리어 시 `zone_meta.enemy_restore_time` = now
- 수복 체크: 접속 시 `CharacterService.getCharacterInfoDto()`에서 서버 처리 후 반환
- 랭킹 보장: Redis 점수는 `cleared_zone` 전체(isRestored 포함) max 기준 → 수복 후에도 최고 기록 유지
- 수복된 존은 재클리어 가능 → 케이스2 처리 (isRestored=false 복구, 클리어 보상 없음)
- DB 구조: `cleared_zone.is_restored` + `cleared_zone.restored_at` (관리용) + `zone_meta` 테이블

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
