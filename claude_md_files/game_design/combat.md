# 전투 배속 시스템, Zone 적 함대 스탯 배율 시스템
# UIPanelCameraView 내 배속 버튼, x0.5~x3.0 단계 순환, GameSpeedController(Time.timeScale)
# Zone 적 함대는 ZoneConfig의 모듈별 배율(enemyBody/Beam/Missile/Hanger/EngineMultiplier)로 스탯 조정

## 개요
- 전투 중 게임 속도를 조절하는 배속 버튼 — `UIPanelCameraView`에 포함 (전투 중에만 표시)
- 버튼 클릭 시 단계별 순환: **x0.5 → x1.0 → x1.5 → x2.0 → x3.0 → x0.5**
- 구현: `GameSpeedController` (static class) — `Time.timeScale` 직접 조작

## 배속 적용 범위
- **적용됨**: 전투 연출 (함선 이동, 발사체, 이펙트, 애니메이션)
- **간접 적용됨**: 적함 격추 킬 보상 — 1킬당 지급 금액은 고정이지만, 배속으로 전투가 빨리 끝나므로 **단위 실시간당 킬 보상 효율 증가**
  - 흐름: `EnemyFleetKilled` 이벤트 → 서버 `KillZoneEnemy` API → 자원 지급
- **적용 안됨**: 존 시간당 수확량(mineralPerHour) — 서버에서 실제 경과 시간(elapsedSeconds) 기준으로 계산하므로 `Time.timeScale`과 무관

## 오디오 피치 연동
- 배속에 따라 오디오 피치도 함께 변경 (EventManager의 GameSpeedChanged 이벤트로 전파)
- 피치 단계: 0.75 / 1.0 / 1.20 / 1.40 / 1.60

## 배속 유지 정책
- 전투 종료 시 `Reset()` — timeScale만 1.0 복원, **인덱스는 유지** → 다음 전투에서 이전 배속 그대로 재사용
- 전투 시작 시 `RestoreSpeed()` — 저장된 인덱스 기준으로 배속 복원

---

## Zone 적 함대 스탯 배율 시스템

### 목적
Zone 전투에서 플레이어와 적이 동일한 ModuleData 스탯을 공유하면 초반 교전이 답답함.
Zone별로 적 모듈 타입마다 배율을 지정해 난이도 차별화.
PvP(fleet_source_player_remote)는 영향 없음.

### 데이터 구조
`DataTableZone` → `ZoneConfig`에 5개 배율 필드:
- `enemyBodyMultiplier` — 함체 체력
- `enemyBeamMultiplier` — 빔 공격력·체력
- `enemyMissileMultiplier` — 미사일 공격력·체력
- `enemyHangerMultiplier` — 격납고·함재기 공격력·체력
- `enemyEngineMultiplier` — 엔진 속도·체력

### 적용 흐름
1. `ObjectManager.SpawnEnemyFleetsFromConfigs()` → `SpaceFleet.InitializeZoneEnemyFleet(fleetInfo, zoneConfig)` 호출
2. `SpaceFleet`에 배율 저장 → `InitializeSpaceFleet()` (fleet_side_enemy, fleet_source_zone_data)으로 위임
3. 각 모듈 초기화 시 `AutoDetectFleetInfo()` 이후 `m_myFleet.IsZoneEnemy == true` 체크 후 배율 적용

### 난이도 가이드 (Inspector 설정)
| Zone | Body | Beam | Missile | Hanger | Engine | 의도 |
|---|---|---|---|---|---|---|
| Zone 1~2 | 0.3 | 0.3 | 0.3 | 0.3 | 1.0 | 한두 방에 처치 |
| Zone 3~5 | 0.5 | 0.5 | 0.5 | 0.5 | 1.0 | 약간의 교전 |
| Zone 6+  | 0.8 | 0.7 | 0.8 | 0.6 | 1.0 | 실력 요구 |
