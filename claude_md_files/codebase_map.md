# Codebase Map — 기능별 코드 위치

> 1차 기록 (2026-05-06). 코드 위치는 실제 파일 확인 기반.
> 2차 작업 시 카테고리 분류 예정.

---

## 오브젝트 계층 요약

```
ObjectManager (MonoSingleton)
├── m_myFleet : SpaceFleet
│   └── m_ships[] : SpaceShip
│       └── m_moduleBodys[] : ModuleBody
│           ├── m_moduleSlots[] : ModuleSlot
│           │   ├── ModuleBeam   → LauncherBeam[]
│           │   ├── ModuleMissile→ LauncherMissile[]
│           │   └── ModuleHanger → LauncherAircraft[]
│           └── ShieldGrid
└── m_enemyFleets[] : SpaceFleet
    └── (동일 구조)

DataManager (Singleton)
├── m_currentCharacter : Character
├── m_currentFleetInfo : FleetInfo
├── m_dataTableModule  : DataTableModule
├── m_dataTableZone    : DataTableZone
└── m_dataTableResearch: DataTableResearch
```

---

## 씬 진입 / 게임 초기화

| 기능 | 파일 | 줄 |
|------|------|----|
| 씬 로드 (로딩 씬 경유) | [LoadingManager.cs](Assets/Scripts/System/Loading/LoadingManager.cs) | `LoadSceneWithLoading()` |
| SpaceScene 초기화 진입점 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `Start()` L119 |
| 내 함대 스폰 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `SpawnFleet()` L360 |
| UI 초기화 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `UIManager.Instance.ShowMainPanel()` L238 |
| 튜토리얼 시작 체크 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `StartTutorialIfNeeded()` L182 |
| 서버 연결 확인 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `OnChangeScene()` L42 → `InvokeRepeating(CheckConnection)` |

---

## 내 함대 스폰

| 기능 | 파일 | 줄 |
|------|------|----|
| 호출 진입점 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `SpawnFleet()` L360 |
| FleetInfo 기반 함대 초기화 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `InitializeSpaceFleet()` L252 |
| 함선 GameObject 생성 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `CreateSpaceShipFromData()` L270 |
| 함선 추가 및 진형 배치 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `AddShip()` L282 |
| 함선 컴포넌트 초기화 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `InitializeSpaceShip()` L58 |
| Body 프리팹 인스턴스화 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `InitSpaceShipBody()` L86 |
| Body 컴포넌트 초기화 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `InitializeModuleBody()` L89 |
| 카메라 타겟 설정 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `CameraController.Instance.SetTargetOfCameraController()` L373 |
| 기함 선택 이벤트 발행 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `EventManager.Trigger_SpaceShipSelected(flagship)` L378 |

---

## 적 함대 스폰 (Zone)

| 기능 | 파일 | 줄 |
|------|------|----|
| 존 진입 후 적 스폰 시작 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `StartSpawnEnemies()` L244 |
| 빈 적 함대 오브젝트 생성 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `CreateEnemyFleetShell()` L392 |
| 적 함대 Shell 초기화 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `InitializeAsZoneEnemyFleetShell()` L238 |
| 스폰 큐 구성 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `StartSpawning()` L138 → `InitializeZoneSpawn()` L131 |
| 함선 순차 스폰 코루틴 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `SpawnShipCoroutine()` L155 |
| 단일 적 함선 생성 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `SpawnSingleShip()` L174 |
| 적 함선 데이터 구성 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `CreateShipInfoFromConfig()` L187 |
| 적 스폰 중지 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `StopEnemySpawning()` L311 |

---

## 적 함대 스폰 (PvP)

| 기능 | 파일 | 줄 |
|------|------|----|
| PvP 전투 시작 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `StartPvpBattle()` L269 |
| PvP 적 함대 초기화 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `InitializeSpaceFleet()` L252 (EFleetSource.fleet_source_player_remote) |
| PvP 적 함대 워프 진입 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `enemyFleet.StartFleetWarpIn()` L292 |

---

## 함대 워프 진입

| 기능 | 파일 | 줄 |
|------|------|----|
| 워프 진입 시작 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `StartFleetWarpIn()` L66 |
| 워프 이동 코루틴 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `FleetWarpInMove()` L98 |
| 신규 함선 워프 진입 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `AddShip()` L282 (bWarp=true 경로) |

---

## 진형 시스템

| 기능 | 파일 | 줄 |
|------|------|----|
| 진형 적용 (즉시/부드럽게) | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `UpdateShipFormation()` L420 |
| 진형 목표 위치 계산 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `CalculateFormationTargets()` L436 |
| CubeGrid 진형 파싱 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `ParseCubeGrid()` L466 |
| Circle 진형 파싱 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `ParseCircle()` L581 |
| 진형 변경 (서버 동기화 포함) | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `ChangeFormation()` L634 |
| 진형 재계획 (함선 변경 시) | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `RefreshFormation()` L369 |
| 함선 → 진형 목적지 이동 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `MoveToFormation()` |
| 진형 이동 코루틴 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `FormationMovementLoop()` |
| Body 크기 변경 → 진형 재계획 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `OnShipBodyChanged()` L362 |
| 진형 프리셋 DB | [FormationPresetDB.cs](Assets/Scripts/Space/Fleet/FormationPreset.cs) | `FormationPresetDB.Get()` |

---

## 전투 상태 관리

| 기능 | 파일 | 줄 |
|------|------|----|
| 함대 상태 전환 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `SetFleetState()` L848 |
| 함선에 함대 상태 적용 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `ApplyFleetStateToShip()` L116 |
| 자동 전투 시작 (Battle 상태) | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `ApplyFleetStateToShip()` L135 (EUnitState.Battle 분기) |
| 자동 전투 중지 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `StopAutoCombat()` L159 |
| 전투 강제 종료 (전멸/퇴각) | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `ForceEndBattle()` L156 |

---

## 적 탐색 / 조준

| 기능 | 파일 | 줄 |
|------|------|----|
| 적 타겟 탐색 코루틴 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `FindTargetModuleBody()` L177 |
| 적 후보 수집 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `CollectCandidateEnemyBodies()` L204 |
| 최소 각도 타겟 선택 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `FindMinAngleBody()` L240 |
| 타겟 방향 회전 코루틴 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `RotateTowardTarget()` L260 |
| 전투 종료 후 전방 복귀 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `ReturnToFleetForward()` L278 |
| 아군 기준 적 함선 탐색 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `GetEnemy()` L557 |
| 적 스폰 위치 계산 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `GetEnemySpawnPosition()` L591 |

---

## 빔 발사

| 기능 | 파일 | 줄 |
|------|------|----|
| 빔 모듈 초기화 | [ModuleBeam.cs](Assets/Scripts/Space/Fleet/ModuleBeam.cs) | `InitializeModuleBeam()` |
| 빔 발사대 초기화 | [LauncherBeam.cs](Assets/Scripts/Space/Fleet/LauncherBeam.cs) | `InitializeLauncherBeam()` L16 |
| 빔 발사 진입점 | [LauncherBeam.cs](Assets/Scripts/Space/Fleet/LauncherBeam.cs) | `Fire()` L44 |
| 빔 발사 코루틴 | [LauncherBeam.cs](Assets/Scripts/Space/Fleet/LauncherBeam.cs) | `FireBeamCoroutine()` L50 |
| 풀에서 빔 투사체 꺼내기 | [LauncherBeam.cs](Assets/Scripts/Space/Fleet/LauncherBeam.cs) | `m_poolManager.Get<ProjectileBeam>(PROJECTILE_BEAM)` L60 |
| 빔 투사체 초기화 | [ProjectileBase.cs](Assets/Scripts/Space/Projectile/ProjectileBase.cs) | `InitializeProjectile()` |
| 즉시 빔 발사 | [LauncherBeamInstant.cs](Assets/Scripts/Space/Fleet/LauncherBeamInstant.cs) | `Fire()` |

---

## 미사일 발사

| 기능 | 파일 | 줄 |
|------|------|----|
| 미사일 모듈 초기화 | [ModuleMissile.cs](Assets/Scripts/Space/Fleet/ModuleMissile.cs) | `InitializeModuleMissile()` |
| 미사일 발사대 초기화 | [LauncherMissile.cs](Assets/Scripts/Space/Fleet/LauncherMissile.cs) | `InitializeLauncherMissile()` L12 |
| 미사일 발사 진입점 | [LauncherMissile.cs](Assets/Scripts/Space/Fleet/LauncherMissile.cs) | `Fire()` L39 |
| 미사일 발사 코루틴 | [LauncherMissile.cs](Assets/Scripts/Space/Fleet/LauncherMissile.cs) | `FireMissileCoroutine()` L45 |
| 풀에서 미사일 꺼내기 | [LauncherMissile.cs](Assets/Scripts/Space/Fleet/LauncherMissile.cs) | `m_poolManager.Get<ProjectileMissile>(m_missilePoolName)` L55 |

---

## 항공기(함재기) 발사

| 기능 | 파일 | 줄 |
|------|------|----|
| 항공기 모듈 | [ModuleHanger.cs](Assets/Scripts/Space/Fleet/ModuleHanger.cs) | |
| 항공기 발사대 | [LauncherAircraft.cs](Assets/Scripts/Space/Fleet/LauncherAircraft.cs) | `Fire()` |
| 항공기 기본 클래스 | [AirCraftBase.cs](Assets/Scripts/Space/Fleet/AirCraftBase.cs) | `ForceReturnToCarrier()` |
| 표준 항공기 | [AircraftStandard.cs](Assets/Scripts/Space/Fleet/AircraftStandard.cs) | |
| 전투 종료 시 귀환 명령 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `OrderAllAircraftReturn()` L350 |

---

## 발사대 공통 / 투사체 공통

| 기능 | 파일 | 줄 |
|------|------|----|
| 발사대 공통 기반 | [LauncherBase.cs](Assets/Scripts/Space/Fleet/LauncherBase.cs) | `FireAtTarget()` |
| 투사체 공통 기반 | [ProjectileBase.cs](Assets/Scripts/Space/Projectile/ProjectileBase.cs) | |
| 빔 투사체 | [ProjectileBeamInstant.cs](Assets/Scripts/Space/Projectile/ProjectileBeamInstant.cs) | |
| 미사일 투사체 | [ProjectileMissle.cs](Assets/Scripts/Space/Projectile/ProjectileMissle.cs) | |
| 투사체 일괄 정리 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `CleanupAllProjectiles()` L332 |

---

## 피격 / 데미지

| 기능 | 파일 | 줄 |
|------|------|----|
| 함선 데미지 진입점 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `TakeDamage()` L302 |
| 랜덤 바디 선택 후 분산 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `GetRandomAliveBody()` L352 |
| 모듈 데미지 | [ModuleBase.cs](Assets/Scripts/Space/Fleet/ModuleBase.cs) | `TakeDamage()` |
| HP 이벤트 발행 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `EventManager.Trigger_FleetUpdateHP/ShipUpdateHP` L314 |
| 함선 파괴 처리 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `TakeDamage()` L317 (IsAlive == false 분기) |
| 폭발 이펙트 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `m_poolManager.Get<EffectBase>(EFFECT_EXPLOSION_SHIP)` L325 |
| 함선 생존 확인 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `IsAlive()` L333 |
| 함대 생존 확인 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `IsFleetAlive()` L766 |
| 함선 함대에서 제거 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `RemoveShip()` L655 |
| 플레이어 함대 전멸 이벤트 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `RemoveShip()` → `EventManager.Trigger_MyFleetDestroyed()` L673 |

---

## 자동 수리

| 기능 | 파일 | 줄 |
|------|------|----|
| 자동 수리 코루틴 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `AutoRepair()` L885 |
| 수리 임계값 설정 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `ERepairThreshold` L7, `m_repairThreshold` L51 |
| 동시 수리 함선 수 설정 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `ERepairConcurrency` L15, `m_repairConcurrency` L52 |

---

## 함대 재건 / 함선 복구

| 기능 | 파일 | 줄 |
|------|------|----|
| 함대 전체 재건 (전멸 복구) | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `RebuildFleet()` L678 |
| 파괴 함선만 복구 (퇴각용) | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `RestoreDestroyedShips()` L708 |
| 전체 체력 비율 적용 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `ApplyHealthRatio()` L748 |

---

## 존 탐사 시스템

| 기능 | 파일 | 줄 |
|------|------|----|
| 탐사 탭 초기화 | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `InitializeUITabExploration()` L43 |
| 초기 함대 위치 설정 | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `SetInitialFleetPosition()` L60 |
| 그룹 탭 생성 (Z1~Z9) | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `SetupGroupTabs()` L85 |
| 그룹 탭 클릭 → 카메라 이동 | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `OnGroupTabClicked()` L98 |
| 존 스테이지 버튼 생성 | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `InitializeZoneStageButtons()` L132 |
| 탭 활성화 → 갤럭시 뷰 전환 | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `OnTabActivated()` L204 |
| 존 버튼 클릭 → 입장 확인 | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `OnEnterZoneFromButton()` L273 |
| 광고 시청 후 입장 | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `TryEnterZoneStageWithAd()` L355 |
| 존 입장 실행 (워프 → 전투) | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `EnterZoneStage()` L391 |
| 전투 시작 (적 스폰 위임) | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `StartBattleInZone()` L440 |
| 존 클리어 후 서버 보고 | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `OnEnemyFleetKilled()` L451 |
| 클리어 응답 처리 (보상) | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `OnClearZoneStageResponse()` L463 |
| 퇴각 처리 | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `RetreatToPreviousStage()` |
| 존 전멸 → ObjectManager 보고 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `SpawnShipCoroutine()` L170 → `ObjectManager.OnZoneEnemyFleetDefeated()` |
| 존 전멸 이벤트 발행 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `OnZoneEnemyFleetDefeated()` L412 |
| 존 데이터 테이블 | [DataTableZone.cs](Assets/Scripts/System/Data/DataTableZone.cs) | |

---

## 모듈 시스템

| 기능 | 파일 | 줄 |
|------|------|----|
| 모듈 타입 정의 | [CommonDefine.cs](Assets/Scripts/System/CommonDefine.cs) | `EModuleType`, `EModuleSubType` |
| 모듈 기본 클래스 | [ModuleBase.cs](Assets/Scripts/Space/Fleet/ModuleBase.cs) | |
| Body 초기화 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `InitializeModuleBody()` L89 |
| Body 레벨업 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `ApplyModuleLevelUp()` L62 |
| 모듈 슬롯 관리 | [ModuleSlot.cs](Assets/Scripts/Space/Fleet/ModuleSlot.cs) | |
| 모듈 플레이스홀더 | [ModulePlaceholder.cs](Assets/Scripts/Space/Fleet/ModulePlaceholder.cs) | |
| 함선 모듈 변경 적용 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `ApplyModuleChange()` |
| 모듈 해금 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `Apply_UnlockModule()` |
| 모듈 리셋 → 플레이스홀더 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `Apply_ResetModuleToPlaceholder()` |
| Body 교체 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `ChangeModuleBody()`, `ReplaceBodyWhilePreservingModules()` |
| 선택 모듈 시각화 | [SelectedModuleVisual.cs](Assets/Scripts/Space/Fleet/SelectedModuleVisual.cs) | |
| 모듈 데이터 테이블 | [DataTableModule.cs](Assets/Scripts/System/Data/DataTableModule.cs) | |

---

## 자원 / 재화 시스템

| 기능 | 파일 | 줄 |
|------|------|----|
| Mineral 변경 이벤트 발행 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | `TriggerMineralChange()` L38 |
| TechLevel 변경 이벤트 발행 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | `TriggerTechLevelChange()` L23 |
| 자원 바 UI | [UIResourceBar.cs](Assets/Scripts/UI/ETC/UIResourceBar.cs) | |
| 존 클리어 자원 보상 처리 | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `OnClearZoneStageResponse()` L463 |

---

## 서버 통신 (NetworkManager)

| 기능 | 파일 | 줄 |
|------|------|----|
| 공통 API 호출 래퍼 (401 자동 갱신) | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `RunAsync()` L178 |
| 자동 로그인 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `AutoLogin()` L749 |
| 이메일 로그인 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `Login()` L252 |
| 구글 로그인 (WebView / PC) | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `GoogleLogin()` L258 |
| 게스트 로그인 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `GuestLogin()` L271 |
| 하트비트 시작 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `StartHeartbeat()` L60 |
| 하트비트 전송 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `Heartbeat()` L779 |
| 함대 체력 저장 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `FleetHealthSave()` L730 |
| 체력 서버 저장 트리거 | [SpaceFleet.cs](Assets/Scripts/Space/Fleet/SpaceFleet.cs) | `SaveHealthToServer()` L333, `OnDestroy()` L321 |
| 진형 변경 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `ChangeFormation()` L668 |
| 함선 추가 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `AddShip()` L662 |
| 모듈 해금 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `UnlockModule()` L674 |
| 모듈 레벨업 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `LevelUpModule()` L680 |
| 모듈 변경 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `ChangeModule()` L692 |
| 모듈 리셋 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `ResetModule()` L706 |
| 기술 연구 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `ResearchTechLevel()` L686 |
| 존 클리어 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `ClearZoneStage()` L772 |
| PvP 리스트 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `PvpList()` L839 |
| PvP 전투 시작 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `PvpBattleStart()` L851 |
| PvP 전투 결과 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `PvpBattleResult()` L857 |
| PvP 랭킹 | [NetworkManager.cs](Assets/Scripts/System/Network/NetworkManager.cs) | `PvpRanking()` L863 |
| API 클라이언트 (HTTP) | [ApiClient.cs](Assets/Scripts/System/Network/ApiClient.cs) | |
| DTO 정의 | [NetworkDTOs.cs](Assets/Scripts/System/Network/NetworkDTOs.cs) | |

---

## 이벤트 시스템 (EventManager)

| 이벤트 | 파일 | 줄 |
|--------|------|----|
| 클래스 정의 / UnsubscribeAll | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L7, L10 |
| TechLevel 변경 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L22 |
| Mineral 변경 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L37 |
| 함대 함선 수 변경 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L54 |
| 함대 HP 갱신 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L68 |
| 함선 선택 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L85 |
| 함선 HP 갱신 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L99 |
| 함선 스탯 변경 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L113 |
| 함선 Body 교체 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L128 |
| 모듈 선택 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L147 |
| 카메라 포커스 타겟 변경 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L165 |
| 카메라 뷰포트 비율 변경 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L180 |
| 플레이어 함대 전멸 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L195 |
| 플레이어 함대 상태 변경 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L210 |
| 탐사 탭 열림/닫힘 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L216, L221 |
| 존 진입 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L227 |
| 적 함대 격멸 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L242 |
| PvP 전투 결과 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L257 |
| 게임 속도 변경 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L272 |
| 모듈 교체 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | L287 |

---

## UI 탭 시스템

| 기능 | 파일 | 줄 |
|------|------|----|
| 탭 기본 클래스 | [UITabBase.cs](Assets/Scripts/UI/UITab/UITabBase.cs) | |
| Fleet 탭 | [UITabFleet.cs](Assets/Scripts/UI/UITab/UITabFleet.cs) | `InitializeUITabFleet()` |
| Ship 탭 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `InitializeUITabShip()` |
| Exploration 탭 | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `InitializeUITabExploration()` L43 |
| PvP 탭 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `InitializeUITab()` |
| Tech 탭 | [UITabTech.cs](Assets/Scripts/UI/UITab/UITabTech.cs) | |
| Settings 탭 | [UITabSettings.cs](Assets/Scripts/UI/UITab/UITabSettings.cs) | `InitializeUITabSettings()` |
| 존 스테이지 버튼 (3D→Screen) | [UIZoneStageButton.cs](Assets/Scripts/UI/UITab/UIZoneStageButton.cs) | |
| 함선 선택 버튼 컴포넌트 | [ShipSelector.cs](Assets/Scripts/UI/ETC/ShipSelector.cs) | UITabFleet에서 사용 |
| 모듈 슬롯 선택 버튼 컴포넌트 | [ModuleSelector.cs](Assets/Scripts/UI/ETC/ModuleSelector.cs) | `Initialize()` L26 (잠금/선택 시각화), UITabShip에서 사용 |
| 함선 HP 게이지 바 (함대 탭 내 함선 카드) | [FleetButtonHPDisplay.cs](Assets/Scripts/UI/ETC/FleetButtonHPDisplay.cs) | Awake Subscribe_FleetUpdateHP, Co_LerpRatio, anchorMax.x |
| 함대 탭 버튼 Attack/HP 표시 | [UITabButtonFleet.cs](Assets/Scripts/UI/ETC/UITabButtonFleet.cs) | Subscribe_ShipStatsChanged, GetFleetCapabilityProfile(false) |
| PvP 상대 선택 카드 | [PvpSelectCard.cs](Assets/Scripts/UI/ETC/PvpSelectCard.cs) | `InitializePvpSelectCard()` L16, CommonUtility.GetFleetCapabilityProfile() |
| Tech 탭 버튼 기술레벨 표시 | [UITabButtonTech.cs](Assets/Scripts/UI/ETC/UITabButtonTech.cs) | Subscribe_TechLevelChanged, GetTechLevel() → Lv.N 표시 |
| Tab System (탭 전환 로직) | [TabSystem.cs](Assets/Scripts/UI/TabSystem.cs) | `SwitchToTab()`, `ForceActivateTab()`, `ForceDeactivateTab()` |
| 팝업 기반 클래스 | [UIPopupBase.cs](Assets/Scripts/UI/UIPopup/UIPopupBase.cs) | `HidePopup()` |
| 확인 팝업 구현 | [UIPopupConfirm.cs](Assets/Scripts/UI/UIPopup/UIPopupConfirm.cs) | `ShowPopupConfirm()` |
| 알림 팝업 구현 | [UIPopupAlert.cs](Assets/Scripts/UI/UIPopup/UIPopupAlert.cs) | `ShowPopupAlert()` (자동 닫힘 지원) |
| 레벨업 팝업 구현 | [UIPopupLevelup.cs](Assets/Scripts/UI/UIPopup/UIPopupLevelup.cs) | `ShowTechLevel()`, `ShowModule()` |
| 진형 선택 팝업 구현 | [UIPopupFormation.cs](Assets/Scripts/UI/UIPopup/UIPopupFormation.cs) | `ShowPopup()` |
| 서브타입 관리 팝업 구현 | [UIPopupModuleSubTypeManage.cs](Assets/Scripts/UI/UIPopup/UIPopupModuleSubTypeManage.cs) | `ShowPopup()` |
| 이름 변경 팝업 구현 | [UIPopupRenameCharacter.cs](Assets/Scripts/UI/UIPopup/UIPopupRenameCharacter.cs) | `ShowPopupRenameCharacter()` |
| 랭킹 팝업 구현 | [UIPopupRanking.cs](Assets/Scripts/UI/UIPopup/UIPopupRanking.cs) | `ShowPopupRanking()` |
| 라이센스 팝업 구현 | [UIPopupLicense.cs](Assets/Scripts/UI/UIPopup/UIPopupLicense.cs) | `ShowPopupLicense()` |
| 적 함대 프리셋 데이터 | [EnemyFleetPreset.cs](Assets/Scripts/System/EnemyFleetPreset.cs) | Zone 배치 참조 |
| 자원 바 UI | [UIResourceBar.cs](Assets/Scripts/UI/ETC/UIResourceBar.cs) | Mineral 이벤트 구독 |
| 로그인 패널 (로그인 유형 선택) | [UIPanelLoginType.cs](Assets/Scripts/UI/UIPanel_Main/UIPanelLoginType.cs) | |
| 로그인 패널 (이메일) | [UIPanelLoginEmail.cs](Assets/Scripts/UI/UIPanel_Main/UIPanelLoginEmail.cs) | |
| 로그인 패널 | [UIPanelLogin.cs](Assets/Scripts/UI/UIPanel_Main/UIPanelLogin.cs) | |
| 최초 진입 패널 | [UIPanelFirst.cs](Assets/Scripts/UI/UIPanel_Main/UIPanelFirst.cs) | |
| 격납고 비행 경로 | [HangerFlightPath.cs](Assets/Scripts/Space/Fleet/HangerFlightPath.cs) | 함재기 이/착함 경로 정의 |
| 공통 유틸리티 | [CommonUtility.cs](Assets/Scripts/System/Util/CommonUtility.cs) | `GetModuleCapabilityProfile()`, `SetUILocText()` 등 |
| 레이더 차트 | [ShipStatsRadarChart.cs](Assets/Scripts/UI/Chart/ShipStatsRadarChart.cs) | 함선 스탯 시각화 |

---

## 풀 시스템 (PoolManager)

| 기능 | 파일 | 줄 |
|------|------|----|
| PoolManager 초기화 | [PoolManager.cs](Assets/Scripts/System/Object/PoolManager.cs) | `InitializePoolManager()` |
| 풀 생성 | [PoolManager.cs](Assets/Scripts/System/Object/PoolManager.cs) | `CreatePool()` |
| 풀에서 꺼내기 | [PoolManager.cs](Assets/Scripts/System/Object/PoolManager.cs) | `Get<T>()` |
| 풀 반환 | [PoolManager.cs](Assets/Scripts/System/Object/PoolManager.cs) | `Return()` |
| ObjectPool (제네릭) | [ObjectPool.cs](Assets/Scripts/System/Object/ObjectPool.cs) | |
| 풀 등록 (ObjectManager) | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `InitializePools()` L27 |
| 빔 투사체 풀 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `PROJECTILE_BEAM` L31 |
| 즉시 빔 투사체 풀 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `PROJECTILE_BEAM_INSTANT` L37 |
| 미사일 풀 (Small/Medium/Large) | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | L43~L59 |
| 이펙트 풀 (BeamHead/Hit/Muzzle) | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | L62~L72 |
| 함선 폭발 이펙트 풀 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | L74~L78 |
| 미사일 폭발 이펙트 풀 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | L80~L84 |
| 워프 속도선 이펙트 풀 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | L87~L91 |
| 항공기 풀 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | L97~L101 |

---

## 광고 시스템

| 기능 | 파일 | 줄 |
|------|------|----|
| 광고 매니저 초기화 | [AdManager.cs](Assets/Scripts/System/Ad/AdManager.cs) | `OnInitialize()` |
| 리워드 광고 로드 | [AdManager.cs](Assets/Scripts/System/Ad/AdManager.cs) | `LoadRewardedAd()` |
| 리워드 광고 표시 | [AdManager.cs](Assets/Scripts/System/Ad/AdManager.cs) | `ShowRewardedAd()` |
| 존 입장 시 광고 요청 | [UITabExploration.cs](Assets/Scripts/UI/UITab/UITabExploration.cs) | `TryEnterZoneStageWithAd()` L355 |
| 광고 설정 ScriptableObject | [AdConfig.cs](Assets/Scripts/System/Ad/AdConfig.cs) | |

---

## 데이터 관리 (DataManager)

| 기능 | 파일 | 줄 |
|------|------|----|
| 데이터 테이블 로드 (초기화) | [DataManager.cs](Assets/Scripts/System/Data/DataManager.cs) | `OnInitialize()` L8 |
| 캐릭터 정보 설정 | [DataManager.cs](Assets/Scripts/System/Data/DataManager.cs) | `SetCharacterInfo()` L27 |
| 함대 정보 설정 | [DataManager.cs](Assets/Scripts/System/Data/DataManager.cs) | `SetFleetData()` L47 |
| 모듈 레벨업 비용 조회 | [DataManager.cs](Assets/Scripts/System/Data/DataManager.cs) | `GetModuleLevelUpCost()` L116 |
| 모듈 최대 레벨 조회 | [DataManager.cs](Assets/Scripts/System/Data/DataManager.cs) | `GetMaxModuleLevel()` L127 |
| 모듈 연구 비용 조회 | [DataManager.cs](Assets/Scripts/System/Data/DataManager.cs) | `GetModuleResearchCost()` L152 |
| 존 데이터 테이블 | [DataTableZone.cs](Assets/Scripts/System/Data/DataTableZone.cs) | |
| 모듈 데이터 테이블 | [DataTableModule.cs](Assets/Scripts/System/Data/DataTableModule.cs) | |
| 연구 데이터 테이블 | [DataTableResearch.cs](Assets/Scripts/System/Data/DataTableResearch.cs) | |
| 게임 설정 테이블 | [DataTableConfig.cs](Assets/Scripts/System/Data/DataTableConfig.cs) | |

---

## 게임 속도 / 배속

| 기능 | 파일 | 줄 |
|------|------|----|
| 게임 속도 컨트롤러 | [GameSpeedController.cs](Assets/Scripts/System/GameSpeedController.cs) | `CycleNext()`, `Reset()`, `RestoreSpeed()` |
| 속도 변경 이벤트 | [EventManager.cs](Assets/Scripts/System/Events/EventManager.cs) | `OnGameSpeedChanged` L272 |

---

## 튜토리얼

| 기능 | 파일 | 줄 |
|------|------|----|
| 튜토리얼 진행도 로드 | [TutorialManager.cs](Assets/Scripts/System/Tutorial/TutorialManager.cs) | `LoadProgressFromServerAsync()` |
| 튜토리얼 시작 | [TutorialManager.cs](Assets/Scripts/System/Tutorial/TutorialManager.cs) | `StartTutorial()` |
| 튜토리얼 데이터 | [TutorialData.cs](Assets/Scripts/System/Tutorial/TutorialData.cs) | |
| 튜토리얼 UI | [TutorialUI.cs](Assets/Scripts/UI/Tutorial/TutorialUI.cs) | |
| 튜토리얼 흐름 (ObjectManager) | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `StartTutorial()` L198 |

---

## Fleet 탭 (UITabFleet) 세부

| 기능 | 파일 | 줄 |
|------|------|----|
| 탭 초기화 | [UITabFleet.cs](Assets/Scripts/UI/UITab/UITabFleet.cs) | `InitializeUITabFleet()` L31 |
| 함선 선택기 그리드 구성 | [UITabFleet.cs](Assets/Scripts/UI/UITab/UITabFleet.cs) | `PopulateShipSelectorGrid()` L165 |
| 함선 선택 → 카메라 / 이벤트 | [UITabFleet.cs](Assets/Scripts/UI/UITab/UITabFleet.cs) | `OnShipSelectorClicked()` L246 |
| 함선 선택 → Ship 탭 전환 | [UITabFleet.cs](Assets/Scripts/UI/UITab/UITabFleet.cs) | `OnShipManageClicked()` L235 |
| 함선 HP 갱신 | [UITabFleet.cs](Assets/Scripts/UI/UITab/UITabFleet.cs) | `RefreshShipHealthDisplay()` L206 |
| 함선 추가 버튼 클릭 | [UITabFleet.cs](Assets/Scripts/UI/UITab/UITabFleet.cs) | `OnAddShipButtonClicked()` L319 |
| 함선 추가 검증 | [UITabFleet.cs](Assets/Scripts/UI/UITab/UITabFleet.cs) | `CanAddShip()` L366 |
| 함선 추가 실행 → 서버 | [UITabFleet.cs](Assets/Scripts/UI/UITab/UITabFleet.cs) | `ExecuteAddShip()` L336 |
| 진형 변경 버튼 → 팝업 | [UITabFleet.cs](Assets/Scripts/UI/UITab/UITabFleet.cs) | `OnFormationChangeClicked()` L142 |
| 진형 선택 후 적용 | [UITabFleet.cs](Assets/Scripts/UI/UITab/UITabFleet.cs) | `OnFormationSelected()` L148 |
| Tech Level 연구 순차 실행 | [UITabFleet.cs](Assets/Scripts/UI/UITab/UITabFleet.cs) | `ResearchTechLevelsSequentially()` L92 |

---

## Ship 탭 (UITabShip) 세부

| 기능 | 파일 | 줄 |
|------|------|----|
| 탭 초기화 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `InitializeUITabShip()` L65 |
| 함선 네비게이터 (< >) | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `OnPrevShipClicked()` L123, `OnNextShipClicked()` L131 |
| 함선 선택 (탭 내) | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `SelectShip()` L139 |
| 함선 헤더 스탯 갱신 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `UpdateShipHeader()` L217 |
| 모듈 선택 버튼 생성 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `PopulateModuleSelectButtons()` L670 |
| 행별 모듈 버튼 갱신 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `RefreshRow()` L688 |
| 모듈 선택 버튼 클릭 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `OnModuleSelectorClicked()` L744 |
| 모듈 디테일 카드 갱신 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `UpdateModuleStatsDisplay()` L530 |
| 모듈 해금 버튼 → 서버 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `OnModuleUnlockClicked()` L270, `ExecuteModuleUnlock()` L302 |
| 해금 응답 → 3D 적용 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `UpdateAfterModuleUnlock()` L331 |
| 모듈 레벨업 버튼 → 팝업 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `OnModuleLevelUpClicked()` L361 |
| 레벨업 검증 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `CanLevelup()` L405 |
| 레벨업 실행 → 서버 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `ExecuteModuleLevelUp()` L383 |
| 레벨업 응답 → 3D 적용 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `ApplyModuleLevelUp()` L481 |
| 서브타입 변경 → 팝업 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `OnSubTypeManageClicked()` L605 |
| 서브타입 선택 → 서버 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `OnModuleSubTypeSelected()` L613 |
| 서브타입 변경 응답 → 3D 적용 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `ApplyModuleChange()` L644 |
| 모듈 리셋 버튼 → 서버 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `OnResetModuleClicked()` L755, `ExecuteResetModule()` L788 |
| 모듈 리셋 응답 → 3D 적용 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `OnResetModuleResponse()` L806 |
| 함선 리셋+삭제 → 서버 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `OnResetShipClicked()` L846, `ExecuteResetShip()` L885 |
| 함선 삭제 응답 → 3D 적용 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `OnResetShipResponse()` L892 |
| 교체 후 모듈 재선택 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `ReselectReplacedModule()` L939 |
| 환급 문자열 생성 | [UITabShip.cs](Assets/Scripts/UI/UITab/UITabShip.cs) | `BuildRefundText()` L926 |

---

## PvP 탭 (UITabPvp) 세부

| 기능 | 파일 | 줄 |
|------|------|----|
| 탭 초기화 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `InitializeUITab()` L29 |
| 탭 활성화 → 목록/랭크 요청 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `OnTabActivated()` L47 |
| 상대 목록 요청 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `RequestPvpList()` L66 |
| 내 랭크 요청 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `RequestPvpMyRank()` L73 |
| 상대 카드 채우기 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `PopulateOpponentList()` L112 |
| 갱신 버튼 → 서버 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `OnRefreshClicked()` L133, `ExecuteRefresh()` L147 |
| 공격 버튼 → 상대 정보 팝업 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `OnAttackClicked()` L167 |
| 전투 시작 요청 → 서버 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `ExecuteAttack()` L193 |
| 서버 응답 → 워프 → 전투 시작 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `OnBattleStartResponse()` L199 |
| 전투 결과 보고 → 서버 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `ReportBattleResult()` L231 |
| 결과 응답 → 점수/순위 갱신 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `OnBattleResultResponse()` L242 |
| 전투 종료 후 워프 복귀 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `ReturnFromBattle()` L269 |
| 복귀 후 함대 재건/함선 복구 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `RebuildFleet()` / `RestoreDestroyedShips()` L281 |
| 랭킹 팝업 | [UITabPvp.cs](Assets/Scripts/UI/UITab/UITabPvp.cs) | `OnRankListButtonClicked()` L60 |

---

## Tech 탭 (UITabTech) 세부

| 기능 | 파일 | 줄 |
|------|------|----|
| 탭 초기화 | [UITabTech.cs](Assets/Scripts/UI/UITab/UITabTech.cs) | `InitializeUITabTech()` L32 |
| Tech Level 표시 갱신 | [UITabTech.cs](Assets/Scripts/UI/UITab/UITabTech.cs) | `UpdateTechLevelDisplay()` L79 |
| 함선 슬롯 바 갱신 | [UITabTech.cs](Assets/Scripts/UI/UITab/UITabTech.cs) | `RefreshShipSlots()` L66 |
| 다음 연구 노드 계산 | [UITabTech.cs](Assets/Scripts/UI/UITab/UITabTech.cs) | `GetNextTechLevelNode()` L121 |
| Tech 레벨업 버튼 → 팝업 | [UITabTech.cs](Assets/Scripts/UI/UITab/UITabTech.cs) | `OnTechLevelButtonClicked()` L132 |
| Tech Level 순차 연구 | [UITabTech.cs](Assets/Scripts/UI/UITab/UITabTech.cs) | `ResearchTechLevelsSequentially()` L147 |

---

## Settings 탭 (UITabSettings) 세부

| 기능 | 파일 | 줄 |
|------|------|----|
| 탭 초기화 | [UITabSettings.cs](Assets/Scripts/UI/UITab/UITabSettings.cs) | `InitializeUITabSettings()` L46 |
| 로그아웃 | [UITabSettings.cs](Assets/Scripts/UI/UITab/UITabSettings.cs) | `OnLogoutButtonClicked()` |
| 캐릭터 이름 변경 | [UITabSettings.cs](Assets/Scripts/UI/UITab/UITabSettings.cs) | `OnRenameCharacterButtonClicked()` |
| 구글 계정 연동/해제 | [UITabSettings.cs](Assets/Scripts/UI/UITab/UITabSettings.cs) | `OnGoogleAccountButtonClicked()` |
| 개발자 콘솔 토글 | [UITabSettings.cs](Assets/Scripts/UI/UITab/UITabSettings.cs) | `DeveloperConsole.Instance.ToggleConsole()` L70 |
| 광고 스킵 토글 (개발용) | [UITabSettings.cs](Assets/Scripts/UI/UITab/UITabSettings.cs) | `AdManager.s_devSkipAd` L76 |
| 개발자 패널 표시 (빌드 타입) | [UITabSettings.cs](Assets/Scripts/UI/UITab/UITabSettings.cs) | `#if DEVELOPMENT_BUILD` L51 |

---

## 카메라 컨트롤러 (CameraController)

| 기능 | 파일 | 줄 |
|------|------|----|
| 카메라 Enum 정의 | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | `ECameraFocusTarget` L9 |
| 초기화 | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | `OnInitialize()` L80 |
| 카메라 위치/회전 매 프레임 갱신 | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | `UpdateCameraTransform()` L135 |
| 함선 선택 시 줌 범위 적용 | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | `OnSpaceShipSelectedForZoom()` L105, `ApplyZoomRangeFromShip()` L119 |
| 카메라 타겟 설정 | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | `SetTargetOfCameraController()` |
| 카메라 포커스 타겟 변경 | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | `SetCameraFocusTarget()`, `CycleCameraFocusTarget()` |
| 갤럭시 뷰 진입 (탐사 탭) | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | `EnterGalaxyView()`, `ExitGalaxyView()`, `ExitGalaxyViewMoveTo()` |
| 존 앵커 포커스 | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | `FocusOnZoneAnchor()` |
| 모듈 숨김 여부 확인 후 포커스 | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | `FocusOnModuleIfHidden()` |
| Viewport 너비 설정 | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | `SetViewportWidth()`, `GetViewportWidth()` |
| 즉시 타겟 스냅 | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | `SnapToTarget()` |
| Center 모드 줌 갱신 | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | `RefreshCenterModeZoom()` |
| 함선 선택 (터치/마우스 레이캐스트) | [CameraController.cs](Assets/Scripts/Space/Camera/CameraController.cs) | 입력 처리 부분 |

---

## UIPanelSpace (메인 게임 패널)

| 기능 | 파일 | 줄 |
|------|------|----|
| 탭 시스템 초기화 | [UIPanelSpace.cs](Assets/Scripts/UI/UIPanel_Game/UIPanelSpace.cs) | `InitializeUIPanelSpace()` L34 |
| 탭 전환 → 카메라 viewport 애니메이션 | [UIPanelSpace.cs](Assets/Scripts/UI/UIPanel_Game/UIPanelSpace.cs) | `OnTabSelectionChanged()` L95, `Co_AnimateLayout()` L111 |
| 모듈 선택 시 Ship 탭 자동 전환 | [UIPanelSpace.cs](Assets/Scripts/UI/UIPanel_Game/UIPanelSpace.cs) | `OnModuleSelectedAutoTabSwitch()` L151 |
| 패널 표시 시 함선 선택 활성화 | [UIPanelSpace.cs](Assets/Scripts/UI/UIPanel_Game/UIPanelSpace.cs) | `OnShowUIPanel()` L70 |

---

## UIPanelCameraView (전투 HUD)

| 기능 | 파일 | 줄 |
|------|------|----|
| 이벤트 구독 (Awake) | [UIPanelCameraView.cs](Assets/Scripts/UI/UIPanel_Game/UIPanelCameraView.cs) | `Awake()` L29 |
| 카메라 포커스 버튼 그룹 | [UIPanelCameraView.cs](Assets/Scripts/UI/UIPanel_Game/UIPanelCameraView.cs) | `Start()` L41 |
| 배속 버튼 클릭 | [UIPanelCameraView.cs](Assets/Scripts/UI/UIPanel_Game/UIPanelCameraView.cs) | `OnSpeedButtonClicked()` L112 |
| 배속 라벨 갱신 | [UIPanelCameraView.cs](Assets/Scripts/UI/UIPanel_Game/UIPanelCameraView.cs) | `RefreshSpeedLabel()` L122 |
| 존 이름 표시 | [UIPanelCameraView.cs](Assets/Scripts/UI/UIPanel_Game/UIPanelCameraView.cs) | `OnZoneEntered()` L135 |
| 패널 표시/숨김 조건 | [UIPanelCameraView.cs](Assets/Scripts/UI/UIPanel_Game/UIPanelCameraView.cs) | `RefreshVisibility()` L94 (Battle 상태 AND 탐사탭 닫힘) |
| Viewport 비율 → 패널 위치 | [UIPanelCameraView.cs](Assets/Scripts/UI/UIPanel_Game/UIPanelCameraView.cs) | `OnViewportChanged()` L146 |

---

## 빔 모듈 자동 공격 (ModuleBeam)

| 기능 | 파일 | 줄 |
|------|------|----|
| 모듈 초기화 | [ModuleBeam.cs](Assets/Scripts/Space/Fleet/ModuleBeam.cs) | `InitializeModuleBeam()` L52 |
| 발사대 생성 (attackFireCount 개) | [ModuleBeam.cs](Assets/Scripts/Space/Fleet/ModuleBeam.cs) | `InitializeByModuleSlot()` L101 |
| 자동 공격 코루틴 시작 | [ModuleBeam.cs](Assets/Scripts/Space/Fleet/ModuleBeam.cs) | `Start()` L114 |
| 자동 공격 루프 | [ModuleBeam.cs](Assets/Scripts/Space/Fleet/ModuleBeam.cs) | `AutoAttack()` L128 |
| 조준각 확인 후 발사 | [ModuleBeam.cs](Assets/Scripts/Space/Fleet/ModuleBeam.cs) | `ExecuteAttackOnTarget()` L153 |
| 타겟 설정 (SpaceShip에서 호출) | [ModuleBeam.cs](Assets/Scripts/Space/Fleet/ModuleBeam.cs) | `SetTarget()` L204 |
| 레벨업 스탯 갱신 | [ModuleBeam.cs](Assets/Scripts/Space/Fleet/ModuleBeam.cs) | `ApplyModuleLevelUp()` L177 |

---

## 격납고 모듈 / 함재기 관리 (ModuleHanger)

| 기능 | 파일 | 줄 |
|------|------|----|
| 격납고 초기화 | [ModuleHanger.cs](Assets/Scripts/Space/Fleet/ModuleHanger.cs) | `InitializeModuleHanger()` L67 |
| 함재기 데이터 풀 생성 | [ModuleHanger.cs](Assets/Scripts/Space/Fleet/ModuleHanger.cs) | `AircraftInfo` 풀 L99 |
| 자동 발사 코루틴 | [ModuleHanger.cs](Assets/Scripts/Space/Fleet/ModuleHanger.cs) | `AutoAttack()` L154 |
| 함재기 출격 실행 | [ModuleHanger.cs](Assets/Scripts/Space/Fleet/ModuleHanger.cs) | `ExecuteLaunchOnTarget()` L173 |
| 함재기 정비 코루틴 (복귀 후 재준비) | [ModuleHanger.cs](Assets/Scripts/Space/Fleet/ModuleHanger.cs) | `MaintenanceProcess()` L182 |
| 준비된 함재기 꺼내기 | [ModuleHanger.cs](Assets/Scripts/Space/Fleet/ModuleHanger.cs) | `GetReadyAircraft()` L203 |
| 함재기 복귀 처리 | [ModuleHanger.cs](Assets/Scripts/Space/Fleet/ModuleHanger.cs) | `ReturnAircraft()` L217 |
| 현재 타겟 반환 | [ModuleHanger.cs](Assets/Scripts/Space/Fleet/ModuleHanger.cs) | `GetCurrentTarget()` L345 |
| AircraftInfo 구조체 | [ModuleHanger.cs](Assets/Scripts/Space/Fleet/ModuleHanger.cs) | `AircraftInfo` L8 |

---

## 함재기 (AircraftBase / AircraftStandard)

| 기능 | 파일 | 줄 |
|------|------|----|
| 함재기 상태 Enum | [AirCraftBase.cs](Assets/Scripts/Space/Fleet/AirCraftBase.cs) | `EAircraftState` L6 |
| 함재기 기본 초기화 | [AirCraftBase.cs](Assets/Scripts/Space/Fleet/AirCraftBase.cs) | `InitializeAirCraft()` L52 |
| 함재기 강제 귀환 | [AirCraftBase.cs](Assets/Scripts/Space/Fleet/AirCraftBase.cs) | `ForceReturnToCarrier()` |
| 표준 함재기 구현 | [AircraftStandard.cs](Assets/Scripts/Space/Fleet/AircraftStandard.cs) | |
| 항공기 발사대 초기화 | [LauncherAircraft.cs](Assets/Scripts/Space/Fleet/LauncherAircraft.cs) | `InitializeLauncherAircraft()` L9 |
| 항공기 발사 코루틴 | [LauncherAircraft.cs](Assets/Scripts/Space/Fleet/LauncherAircraft.cs) | `FireCoroutine()` L39 |
| 풀에서 항공기 꺼내기 | [LauncherAircraft.cs](Assets/Scripts/Space/Fleet/LauncherAircraft.cs) | `m_poolManager.Get<AircraftStandard>(AIRCRAFT_STANDARD)` L56 |

---

## 발사대 기반 (LauncherBase)

| 기능 | 파일 | 줄 |
|------|------|----|
| 발사 진입점 (null 체크) | [LauncherBase.cs](Assets/Scripts/Space/Fleet/LauncherBase.cs) | `FireAtTarget()` L16 |
| 추상 발사 메서드 | [LauncherBase.cs](Assets/Scripts/Space/Fleet/LauncherBase.cs) | `Fire()` L22 |
| 게임 속도 변경 → 오디오 피치 동기화 | [LauncherBase.cs](Assets/Scripts/Space/Fleet/LauncherBase.cs) | `OnGameSpeedChanged()` L32 |
| FirePoint 인덱스로 찾기 | [LauncherBase.cs](Assets/Scripts/Space/Fleet/LauncherBase.cs) | `FindFirePointByIndex()` L38 |
| 파괴 시 이펙트 자식 분리 | [LauncherBase.cs](Assets/Scripts/Space/Fleet/LauncherBase.cs) | `OnDestroy()` L49 |

---

## 모듈 기반 클래스 (ModuleBase)

| 기능 | 파일 | 줄 |
|------|------|----|
| 공통 필드 정의 (HP, Attack, 투자 광물) | [ModuleBase.cs](Assets/Scripts/Space/Fleet/ModuleBase.cs) | L9~L24 |
| 함선 상태 → 모듈 상태 전파 | [ModuleBase.cs](Assets/Scripts/Space/Fleet/ModuleBase.cs) | `ApplyShipStateToModule()` L59 |
| 데미지 처리 | [ModuleBase.cs](Assets/Scripts/Space/Fleet/ModuleBase.cs) | `TakeDamage()` L77 |
| 잠금 해제 서브타입 관리 | [ModuleBase.cs](Assets/Scripts/Space/Fleet/ModuleBase.cs) | `SetUnlockedSubTypes()` L26, `IsSubTypeFree()` L43 |
| 투자 광물 관리 | [ModuleBase.cs](Assets/Scripts/Space/Fleet/ModuleBase.cs) | `SetInvestedMinerals()` L31, `HasInvestedMineral()` L38 |
| 함대/함선 자동 탐지 | [ModuleBase.cs](Assets/Scripts/Space/Fleet/ModuleBase.cs) | `AutoDetectFleetInfo()` |

---

## 투사체 기반 (ProjectileBase)

| 기능 | 파일 | 줄 |
|------|------|----|
| 투사체 기반 클래스 | [ProjectileBase.cs](Assets/Scripts/Space/Projectile/ProjectileBase.cs) | `ProjectileBase` L4 |
| 투사체 초기화 | [ProjectileBase.cs](Assets/Scripts/Space/Projectile/ProjectileBase.cs) | `InitializeProjectile()` L14 |
| 발사 함선 참조 저장 (Body 교체 대응) | [ProjectileBase.cs](Assets/Scripts/Space/Projectile/ProjectileBase.cs) | `m_sourceShip` L23 |
| 빔 투사체 | [ProjectileBase.cs](Assets/Scripts/Space/Projectile/ProjectileBase.cs) → [ProjectileBeam.cs](Assets/Scripts/Space/Projectile/ProjectileBeam.cs) | |
| 즉시 빔 투사체 | [ProjectileBeamInstant.cs](Assets/Scripts/Space/Projectile/ProjectileBeamInstant.cs) | |
| 미사일 투사체 | [ProjectileMissle.cs](Assets/Scripts/Space/Projectile/ProjectileMissle.cs) | |

---

## 함선 모듈 변경 (SpaceShip)

| 기능 | 파일 | 줄 |
|------|------|----|
| 모듈 해금 적용 → Body.ReplaceModuleInSlot | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `Apply_UnlockModule()` L704 |
| 모듈 플레이스홀더로 리셋 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `Apply_ResetModuleToPlaceholder()` L746 |
| 모듈/Body 교체 통합 진입점 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `ApplyModuleChange()` L758 |
| Body 교체 (기존 모듈 보존) | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `ChangeModuleBody()` L800 |
| Body 교체 + 모듈 재배치 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `ReplaceBodyWhilePreservingModules()` |
| 투자 광물 설정 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `SetModuleInvestedMinerals()` L408 |
| 함선 스탯 프로파일 계산 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `GetShipCapabilityProfile()` |
| 선택 모듈 시각화 설정 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `SetupSelectedModuleVisualing()` |
| 선택 모듈 시각화 갱신 | [SpaceShip.cs](Assets/Scripts/Space/Fleet/SpaceShip.cs) | `RefreshSelectedModuleVisuals()` L668 |

---

## 워프 이펙트 (WarpEffectShip)

| 기능 | 파일 | 줄 |
|------|------|----|
| 워프 이펙트 초기화 | [WarpEffectShip.cs](Assets/Scripts/Space/Effect/WarpEffectShip.cs) | `InitializeWarpEffect()` L50 |
| 함대 워프 진입 이펙트 | [WarpEffectShip.cs](Assets/Scripts/Space/Effect/WarpEffectShip.cs) | `StartFleetWarpIn()` |
| 함선 접근 워프 이펙트 | [WarpEffectShip.cs](Assets/Scripts/Space/Effect/WarpEffectShip.cs) | `StartApproachWarp()` |
| 워프 중지 | [WarpEffectShip.cs](Assets/Scripts/Space/Effect/WarpEffectShip.cs) | `StopWarp()` |

---

## 이펙트 기반 (EffectBase)

| 기능 | 파일 | 줄 |
|------|------|----|
| 이펙트 기반 클래스 | [EffectBase.cs](Assets/Scripts/Space/Effect/EffectBase.cs) | `PlayEffect()` |
| 워프 포스트프로세싱 | [WarpPostProcessing.cs](Assets/Scripts/Space/Effect/WarpPostProcessing.cs) | |
| 레디얼 블러 렌더 피처 | [RadialBlurFeature.cs](Assets/Scripts/Space/Effect/RadialBlurFeature.cs) | |

---

## 우주 공간 오브젝트

| 기능 | 파일 | 줄 |
|------|------|----|
| 천체(행성/소행성) 스폰 | [CelestialBodySpawner.cs](Assets/Scripts/Space/CelestialBodySpawner.cs) | `SpawnAll()` |
| 우주 미네랄 오브젝트 | [SpaceMineral.cs](Assets/Scripts/Space/SpaceMineral.cs) | |

---

## 기타 주요 시스템

| 기능 | 파일 | 줄 |
|------|------|----|
| 싱글톤 (MonoBehaviour) | [MonoSingleton.cs](Assets/Scripts/System/Util/MonoSingleton.cs) | |
| 싱글톤 (Pure C#) | [Singleton.cs](Assets/Scripts/System/Util/Singleton.cs) | |
| 공통 Enum/Define | [CommonDefine.cs](Assets/Scripts/System/CommonDefine.cs) | EModuleType, EModuleSubType, EFormationType, EUnitState |
| 클라이언트 Define | [ClientDefine.cs](Assets/Scripts/System/ClientDefine.cs) | |
| 로컬라이제이션 | [LocalizationManager.cs](Assets/Scripts/System/Localization/LocalizationManager.cs) | |
| 아이콘 스프라이트 캐시 | [IconSpriteCache.cs](Assets/Scripts/System/Util/IconSpriteCache.cs) | |
| UI 매니저 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | |
| 쉴드 그리드 | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | |
| 개발자 콘솔 | [DeveloperConsole.cs](Assets/Scripts/System/Dev/DeveloperConsole.cs) | |
| 디버그 오버레이 | [DebugOverlay.cs](Assets/Scripts/System/Dev/DebugOverlay.cs) | |
| 프리팹 경로 관리 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `LoadPrefab()` L439 |
| 천체 스폰 | [ObjectManager.cs](Assets/Scripts/System/Object/ObjectManager.cs) | `m_celestialBodySpawner.SpawnAll()` L123 |
| 에러 코드 매핑 | [ServerErrorCode.cs](Assets/Scripts/System/Util/ServerErrorCode.cs) | |

---

## Character 오브젝트 (캐릭터 상태)

| 기능 | 파일 | 줄 |
|------|------|----|
| 클래스 정의 (플레이어 상태 컨테이너) | [Character.cs](Assets/Scripts/Object/Character.cs) | L5 |
| 생성자 (CharacterInfo, 연구 목록 초기화) | [Character.cs](Assets/Scripts/Object/Character.cs) | `Character()` L12 |
| 표시 이름 반환 (empty_ 기본값 처리) | [Character.cs](Assets/Scripts/Object/Character.cs) | `GetDisplayName()` L21 |
| 현재 기술 레벨 계산 | [Character.cs](Assets/Scripts/Object/Character.cs) | `GetTechLevel()` L48 (completedResearchIds에서 최댓값) |
| 광물 합산 조회 | [Character.cs](Assets/Scripts/Object/Character.cs) | `GetTotalMineral()` L107 |
| 광물 소비 가능 여부 | [Character.cs](Assets/Scripts/Object/Character.cs) | `CheckEnoughMineral()` L113 |
| 광물 갱신 + 이벤트 발행 | [Character.cs](Assets/Scripts/Object/Character.cs) | `UpdateMineral()` L76, `UpdateAllMinerals()` L97 |
| 함대 참조 설정/조회 | [Character.cs](Assets/Scripts/Object/Character.cs) | `SetOwnedFleet()` L122, `GetOwnedFleet()` L127 |
| 연구된 모듈 설정 (서버 응답) | [Character.cs](Assets/Scripts/Object/Character.cs) | `SetResearchedModules()` L150 |
| 모듈 연구 완료 확인 | [Character.cs](Assets/Scripts/Object/Character.cs) | `IsModuleResearched()` L162 |
| 연구 ID 배열 세팅 (tech_level_N 등) | [Character.cs](Assets/Scripts/Object/Character.cs) | `SetCompletedResearchIds()` L194 |
| 단건 연구 완료 추가 | [Character.cs](Assets/Scripts/Object/Character.cs) | `AddCompletedResearchId()` L203 |
| 연구 완료 여부 확인 | [Character.cs](Assets/Scripts/Object/Character.cs) | `IsResearchCompleted()` L211 |

---

## 메인 씬 UI (UIMain)

| 기능 | 파일 | 줄 |
|------|------|----|
| UIMain (UIManager 서브클래스) | [UIMain.cs](Assets/Scripts/Main/UIMain.cs) | L11 |
| 메인씬 진입 → UIManager 초기화 | [UIMain.cs](Assets/Scripts/Main/UIMain.cs) | `Start()` L13, `InitializeUIManager()` L19 |
| Panel_Main 프리팹 일괄 로드/인스턴스화 | [UIMain.cs](Assets/Scripts/Main/UIMain.cs) | `InitializeUIManager()` L26 |
| 캐릭터 목록 요청 | [UIMain.cs](Assets/Scripts/Main/UIMain.cs) | `GetCharacters()` L55 |
| 캐릭터 생성 | [UIMain.cs](Assets/Scripts/Main/UIMain.cs) | `CreateCharacter()` L98 |
| 캐릭터 선택 → 토큰/함대/연구 정보 수신 | [UIMain.cs](Assets/Scripts/Main/UIMain.cs) | `SelectCharacter()` L128 |
| SpaceScene 로드 (로딩씬 경유) | [UIMain.cs](Assets/Scripts/Main/UIMain.cs) | `LoadingManager.LoadSceneWithLoading("SpaceScene")` L183 |

---

## 씬 로딩 (LoadingManager)

| 기능 | 파일 | 줄 |
|------|------|----|
| 씬 전환 진입점 (static) | [LoadingManager.cs](Assets/Scripts/System/Loading/LoadingManager.cs) | `LoadSceneWithLoading()` L114 |
| 비동기 씬 로드 코루틴 | [LoadingManager.cs](Assets/Scripts/System/Loading/LoadingManager.cs) | `LoadSceneAsync()` L29 |
| 로딩 진행률 UI 갱신 | [LoadingManager.cs](Assets/Scripts/System/Loading/LoadingManager.cs) | `UpdateLoadingUI()` L96 |
| 최소 로딩 시간 보장 (2초) | [LoadingManager.cs](Assets/Scripts/System/Loading/LoadingManager.cs) | `minimumLoadingTime` L32 |

---

## UIManager (패널/팝업 관리)

| 기능 | 파일 | 줄 |
|------|------|----|
| 팝업 레이어 Enum | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `EPopupLayer` L7 |
| 컨테이너 초기화 (GaugeBar/General/Tutorial/Popup) | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `InitializeContainers()` L59 |
| 패널 표시 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `ShowPanel()` L113 |
| 메인 패널 표시 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `ShowMainPanel()` L133 |
| 현재 패널 숨기기 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `HideCurrentPanel()` L139 |
| 패널 페이드 애니메이션 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `AnimatePanel()` L202 |
| 팝업 풀 취득/생성 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `GetOrCreatePopup<T>()` L292 |
| 팝업 교체 (Normal 레이어) | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `ReplacePopup()` L329 |
| 팝업 쌓기 (Overlay 레이어) | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `PushPopup()` L336 |
| 팝업 닫기 + 풀 반환 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `CloseTopPopup()` L342, `ReturnToPool()` L351 |
| 확인 팝업 표시 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `ShowConfirmPopup()` L369 |
| 알림 팝업 표시 (Overlay) | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `ShowPopupAlert()` L383 |
| 랭킹 팝업 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `ShowRankingPopup()` L396 |
| 진형 팝업 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `ShowFormationPopup()` L406 |
| 서브타입 관리 팝업 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `ShowModuleSubTypeManagePopup()` L416 |
| 이름 변경 팝업 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `ShowRenameCharacterPopup()` L426 |
| 라이센스 팝업 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `ShowLicensePopup()` L439 |
| 기술 레벨업 팝업 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `ShowTechLevelupPopup()` L449 |
| 모듈 레벨업 팝업 | [UIManager.cs](Assets/Scripts/System/Util/UIManager.cs) | `ShowModuleLevelupPopup()` L461 |

---

## 쉴드 그리드 (ShieldGrid)

| 기능 | 파일 | 줄 |
|------|------|----|
| 그리드 모드 Enum | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | `EShieldGridMode` L8 (Triangle/Hexagon) |
| 실드 그리드 생성 (에디터/런타임) | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | `GenerateShield()` L86 |
| 히트 지점 → 가장 가까운 셀 반환 | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | `GetHitCell()` L119 |
| 히트 지점 → 가장 가까운 꼭지점 반환 | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | `GetHitVertex()` L140 |
| 충돌체 생성 (MeshCollider + Rigidbody kinematic) | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | `GenerateCollider()` L162 |
| 진형 배치용 extents 반환 | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | `GetFormationExtents()` L199 |
| 진형 충돌 릴레이 owner 설정 (런타임) | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | `InitFormationRelay()` L202 |
| 삼각형 모드 생성 | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | `GenerateTriangleMode()` L219 |
| 헥사곤 모드 생성 | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | `GenerateHexagonMode()` L261 |
| ShieldTriggerRelay (자식 콜라이더 → SpaceShip 전달) | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | `ShieldTriggerRelay` L673 |
| 진형 이동 중 충돌 깊이 계산 | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | `ShieldTriggerRelay.Update()` L701 → `owner.OnShieldTriggerStay()` |
| ShieldCell / ShieldVertex 데이터 | [ShieldGrid.cs](Assets/Scripts/System/Util/ShieldGrid.cs) | `ShieldCell` L662, [ShieldVertex.cs](Assets/Scripts/System/Util/ShieldVertex.cs) |

---

## 미사일 모듈 자동 공격 (ModuleMissile)

| 기능 | 파일 | 줄 |
|------|------|----|
| 미사일 모듈 초기화 | [ModuleMissile.cs](Assets/Scripts/Space/Fleet/ModuleMissile.cs) | `InitializeModuleMissile()` L54 |
| 발사대 생성 (attackFireCount 개) | [ModuleMissile.cs](Assets/Scripts/Space/Fleet/ModuleMissile.cs) | `InitializeByModuleSlot()` L103 |
| 자동 공격 코루틴 시작 | [ModuleMissile.cs](Assets/Scripts/Space/Fleet/ModuleMissile.cs) | `Start()` L136 |
| 자동 공격 루프 | [ModuleMissile.cs](Assets/Scripts/Space/Fleet/ModuleMissile.cs) | `AutoAttack()` L149 |
| 모든 발사대 발사 | [ModuleMissile.cs](Assets/Scripts/Space/Fleet/ModuleMissile.cs) | `ExecuteAttackOnTarget()` L168 |
| 타겟 설정 (SpaceShip에서 호출) | [ModuleMissile.cs](Assets/Scripts/Space/Fleet/ModuleMissile.cs) | `SetTarget()` L220 |
| 서브타입별 풀 이름 결정 | [ModuleMissile.cs](Assets/Scripts/Space/Fleet/ModuleMissile.cs) | `GetMissilePoolName()` L117 |
| 레벨업 스탯 갱신 | [ModuleMissile.cs](Assets/Scripts/Space/Fleet/ModuleMissile.cs) | `ApplyModuleLevelUp()` L189 |
| Zone 적 함선 배율 적용 | [ModuleMissile.cs](Assets/Scripts/Space/Fleet/ModuleMissile.cs) | `InitializeModuleMissile()` L89 (IsZoneEnemy 분기) |

---

## 함체 모듈 (ModuleBody) 세부

| 기능 | 파일 | 줄 |
|------|------|----|
| Body 초기화 (서버 데이터 기반 복원) | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `InitializeModuleBody()` L89 |
| 슬롯 수집 및 정렬 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `CollectAndSortModuleSlots()` L318 |
| 저장된 모듈 재배치 (Body 교체 시) | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `RestoreSavedModules()` L153 |
| 미생성 모듈 서버 데이터로 생성 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `CreateMissingModules()` L198 |
| Beam 생성 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `InitializeBeam()` L247 |
| Missile 생성 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `InitializeMissile()` L266 |
| Hanger 생성 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `InitializeHanger()` L285 |
| 빈 슬롯 → Placeholder 채우기 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `FillEmptySlotsWithPlaceholders()` L336 |
| 슬롯 찾기 (타입 + 인덱스) | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `FindModuleSlot()` L430 |
| 모듈 → 플레이스홀더 리셋 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `ResetModuleToPlaceholder()` L544 |
| 슬롯 모듈 교체 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `ReplaceModuleInSlot()` L569 |
| 새 모듈 생성 및 배치 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `CreateAndPlaceModule()` L620 |
| 전 슬롯에 타겟 설정 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `SetTarget()` L450 |
| Body 파괴 시 슬롯 모듈 비활성화 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `TakeDamage()` L480 |
| 능력치 프로파일 계산 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `GetModuleCapabilityProfile()` L503 |
| Zone 적 함선 배율 적용 | [ModuleBody.cs](Assets/Scripts/Space/Fleet/ModuleBody.cs) | `InitializeModuleBody()` L114 (IsZoneEnemy 분기) |

---

## 로컬라이제이션 (LocalizationManager)

| 기능 | 파일 | 줄 |
|------|------|----|
| 초기화 (저장 로케일 복원) | [LocalizationManager.cs](Assets/Scripts/System/Localization/LocalizationManager.cs) | `OnInitialize()` L18 |
| 키로 문자열 가져오기 | [LocalizationManager.cs](Assets/Scripts/System/Localization/LocalizationManager.cs) | `Get()` L36 |
| 포맷 지원 문자열 가져오기 | [LocalizationManager.cs](Assets/Scripts/System/Localization/LocalizationManager.cs) | `Get()` L45 (params object[]) |
| 언어 변경 + PlayerPrefs 저장 | [LocalizationManager.cs](Assets/Scripts/System/Localization/LocalizationManager.cs) | `SetLocale()` L55 |
| 현재 로케일 코드 반환 | [LocalizationManager.cs](Assets/Scripts/System/Localization/LocalizationManager.cs) | `GetCurrentLocaleCode()` L66 |
| 사용 가능 로케일 목록 | [LocalizationManager.cs](Assets/Scripts/System/Localization/LocalizationManager.cs) | `GetAvailableLocales()` L73 |
| 언어 변경 이벤트 | [LocalizationManager.cs](Assets/Scripts/System/Localization/LocalizationManager.cs) | `OnLanguageChanged` L14 |

---

## 광고 매니저 (AdManager) 세부

| 기능 | 파일 | 줄 |
|------|------|----|
| 초기화 (AdConfig 로드, MobileAds.Initialize) | [AdManager.cs](Assets/Scripts/System/Ad/AdManager.cs) | `OnInitialize()` L40 |
| dev 빌드 기기 허용 목록 체크 | [AdManager.cs](Assets/Scripts/System/Ad/AdManager.cs) | `_isDeviceAllowed` L59 |
| 리워드 광고 로드 | [AdManager.cs](Assets/Scripts/System/Ad/AdManager.cs) | `LoadRewardedAd()` L80 |
| 로드 실패 시 30초 후 재시도 | [AdManager.cs](Assets/Scripts/System/Ad/AdManager.cs) | `RetryLoadAfterDelay()` L117 |
| 광고 이벤트 등록 (닫힘/실패) | [AdManager.cs](Assets/Scripts/System/Ad/AdManager.cs) | `RegisterRewardedAdEvents()` L123 |
| 리워드 광고 표시 | [AdManager.cs](Assets/Scripts/System/Ad/AdManager.cs) | `ShowRewardedAd()` L150 |
| 광고 준비 여부 프로퍼티 | [AdManager.cs](Assets/Scripts/System/Ad/AdManager.cs) | `IsRewardedAdReady` L175 |
| 개발자 광고 스킵 플래그 | [AdManager.cs](Assets/Scripts/System/Ad/AdManager.cs) | `s_devSkipAd` L178 (static) |
| 콜백 스레드 안전 큐 (메인 스레드 전달) | [AdManager.cs](Assets/Scripts/System/Ad/AdManager.cs) | `_mainThreadQueue`, `Dispatch()`, `Update()` L22~L38 |

---

## 튜토리얼 시스템 (TutorialManager) 세부

| 기능 | 파일 | 줄 |
|------|------|----|
| 서버에서 완료 진행도 로드 | [TutorialManager.cs](Assets/Scripts/System/Tutorial/TutorialManager.cs) | `LoadProgressFromServerAsync()` L39 |
| 튜토리얼 시작 (완료 시 즉시 콜백) | [TutorialManager.cs](Assets/Scripts/System/Tutorial/TutorialManager.cs) | `StartTutorial()` L70 |
| 다음 스텝 진행 | [TutorialManager.cs](Assets/Scripts/System/Tutorial/TutorialManager.cs) | `NextStep()` L97 |
| 튜토리얼 스킵 | [TutorialManager.cs](Assets/Scripts/System/Tutorial/TutorialManager.cs) | `SkipTutorial()` L113 |
| 특정 UI 클릭 시 스텝 진행 | [TutorialManager.cs](Assets/Scripts/System/Tutorial/TutorialManager.cs) | `OnTargetClicked()` L121 |
| 현재 스텝 실행 (패널 열기 + UI 표시) | [TutorialManager.cs](Assets/Scripts/System/Tutorial/TutorialManager.cs) | `ExecuteCurrentStep()` L144 |
| 튜토리얼 완료 → 서버 저장 | [TutorialManager.cs](Assets/Scripts/System/Tutorial/TutorialManager.cs) | `CompleteTutorial()` L162, `SaveTutorialToServer()` L189 |
| 카메라 회전 조건 체크 코루틴 | [TutorialManager.cs](Assets/Scripts/System/Tutorial/TutorialManager.cs) | `CheckCameraRotation()` L292 |
| 카메라 줌 조건 체크 코루틴 | [TutorialManager.cs](Assets/Scripts/System/Tutorial/TutorialManager.cs) | `CheckCameraZoom()` L317 |
| 모듈 선택 조건 (구독/카운트/특정타입) | [TutorialManager.cs](Assets/Scripts/System/Tutorial/TutorialManager.cs) | `OnModuleSelected()` L342 |

---

## ApiClient (HTTP 통신 레이어)

| 기능 | 파일 | 줄 |
|------|------|----|
| 클래스 정의 (baseUrl 환경별 분기) | [ApiClient.cs](Assets/Scripts/System/Network/ApiClient.cs) | L21 |
| 토큰 설정/저장 (PlayerPrefs) | [ApiClient.cs](Assets/Scripts/System/Network/ApiClient.cs) | `SetTokens()` L46 |
| 토큰 로드 | [ApiClient.cs](Assets/Scripts/System/Network/ApiClient.cs) | `LoadRefreshToken()` L59 |
| 토큰 초기화 | [ApiClient.cs](Assets/Scripts/System/Network/ApiClient.cs) | `ClearTokens()` L64 |
| 서버 헬스 체크 | [ApiClient.cs](Assets/Scripts/System/Network/ApiClient.cs) | `CheckServerAliveAsync()` L73 |
| 튜토리얼 진행도 로드 | [ApiClient.cs](Assets/Scripts/System/Network/ApiClient.cs) | `GetProgressListAsync()` |
| 튜토리얼 진행도 저장 | [ApiClient.cs](Assets/Scripts/System/Network/ApiClient.cs) | `SaveProgressAsync()` |

---

## GaugeBar / GaugeBars (HP 바)

| 기능 | 파일 | 줄 |
|------|------|----|
| 개별 3D 오브젝트 추적 HP 바 (WorldToScreenPoint 변환, 스무스 보간) | [GaugeBar.cs](Assets/Scripts/UI/GaugeBar/GaugeBar.cs) | `InitializeGaugeBar()`, `UpdateValue()`, `CalculateWorldPosition()` |
| SpaceShip에 부착, 모듈별 GaugeBar 관리 | [GaugeBars.cs](Assets/Scripts/UI/GaugeBar/GaugeBars.cs) | `GaugeBars` L13 |
| 모드별 게이지바 생성 (Body/All) | [GaugeBars.cs](Assets/Scripts/UI/GaugeBar/GaugeBars.cs) | `InitializeGaugeBars()` L56 |
| 모듈 교체 시 게이지바 갱신 | [GaugeBars.cs](Assets/Scripts/UI/GaugeBar/GaugeBars.cs) | `OnModuleReplaced()` L39 (EventManager.Subscribe_ModuleReplaced) |
| 모듈 타입별 색상 반환 | [GaugeBars.cs](Assets/Scripts/UI/GaugeBar/GaugeBars.cs) | `GetModuleColor()` L134 |
| 매 프레임 HP 비율 갱신 | [GaugeBars.cs](Assets/Scripts/UI/GaugeBar/GaugeBars.cs) | `UpdateAllGaugeBars()` L153 (Update) |
| 화면 범위+체력 만충 시 숨김 | [GaugeBars.cs](Assets/Scripts/UI/GaugeBar/GaugeBars.cs) | `LateUpdate()` L190 |
| 표시 모드 Enum | [GaugeBars.cs](Assets/Scripts/UI/GaugeBar/GaugeBars.cs) | `EGaugeBarMode` L4 |

---

## 스크롤뷰 아이템 컴포넌트

| 기능 | 파일 | 줄 |
|------|------|----|
| 진형 선택 아이템 (Outline 선택 시각화) | [ScrollViewFormationItem.cs](Assets/Scripts/UI/ScrollViewItem/ScrollViewFormationItem.cs) | `InitializeScrollViewFormationItem()` L17 |
| 랭킹 아이템 (순위/이름/점수, 내 순위 강조) | [ScrollViewRankingItem.cs](Assets/Scripts/UI/ScrollViewItem/ScrollViewRankingItem.cs) | `SetData()` L13, `SetLoading()` L21 |
| 연구 노드 아이템 (상태별 배경색 + 선택 Outline) | [ScrollViewResearchItem.cs](Assets/Scripts/UI/ScrollViewItem/ScrollViewResearchItem.cs) | `InitializeScrollViewResearchItem()` L31, `SetNodeState()` L56 |
| 연구 노드 상태 Enum | [ScrollViewResearchItem.cs](Assets/Scripts/UI/ScrollViewItem/ScrollViewResearchItem.cs) | `EResearchNodeState` L6 |
| 함선 선택 아이템 | [ScrollViewShipItem.cs](Assets/Scripts/UI/ScrollViewItem/ScrollViewShipItem.cs) | `InitializeScrollViewShipItem()` L10 |
| 함선 추가 아이템 | [ScrollViewShipItemAdd.cs](Assets/Scripts/UI/ScrollViewItem/ScrollViewShipItemAdd.cs) | `InitializeScrollViewShipItemAdd()` L10 |
| 웨이브 진행 아이템 (Pending/InProgress/Cleared 상태별 색상) | [ScrollViewWaveItem.cs](Assets/Scripts/UI/ScrollViewItem/ScrollViewWaveItem.cs) | `InitializeScrollViewWaveItem()` L25, `SetState()` L33 |
| 웨이브 상태 Enum | [ScrollViewWaveItem.cs](Assets/Scripts/UI/ScrollViewItem/ScrollViewWaveItem.cs) | `EWaveState` L5 |
| 모듈 선택 아이템 (selectedIndicator 활성/비활성) | [ScrollViewModuleItem.cs](Assets/Scripts/UI/ScrollViewItem/ScrollViewModuleItem.cs) | `InitializeScrollViewModuleItem()` L11, `SetSelected_ScrollViewModuleItem()` L22 |
| 존 목록 아이템 (Cleared/NotCleared, anchorMax.x 비율) | [ScrollViewZoneItem.cs](Assets/Scripts/UI/ScrollViewItem/ScrollViewZoneItem.cs) | `InitializeScrollViewZoneItem()` L27, `SetZoneItemState()` L46 |
| 존 상태 Enum | [ScrollViewZoneItem.cs](Assets/Scripts/UI/ScrollViewItem/ScrollViewZoneItem.cs) | `EZoneState` L7 |

---

## 튜토리얼 UI 컴포넌트 (개별)

| 기능 | 파일 | 줄 |
|------|------|----|
| 클릭 유도 화살표 (바운스 애니메이션, 방향별 회전) | [TutorialArrow.cs](Assets/Scripts/UI/Tutorial/TutorialArrow.cs) | `Show()` L17, `Hide()` L59 |
| 강조 마스크 (쉐이더 기반, 구멍 뚫기, ICanvasRaycastFilter) | [TutorialMask.cs](Assets/Scripts/UI/Tutorial/TutorialMask.cs) | `HighlightTarget()` L85, `SetClickable()` L144 |
| 터미네이트 마스크 쉐이더 프로퍼티 캐싱 | [TutorialMask.cs](Assets/Scripts/UI/Tutorial/TutorialMask.cs) | `HoleCenterID`, `HoleSizeID` L25-L28 |
| 상단 통과 영역 설정 (3D 공간 터치용) | [TutorialMask.cs](Assets/Scripts/UI/Tutorial/TutorialMask.cs) | `SetTopPassthrough()` L158 |
| 텍스트 박스 (타이핑 효과, 스토리/타겟 모드 위치 계산) | [TutorialTextBox.cs](Assets/Scripts/UI/Tutorial/TutorialTextBox.cs) | `ShowMessage()` L32 |
| 타이핑 효과 코루틴 | [TutorialTextBox.cs](Assets/Scripts/UI/Tutorial/TutorialTextBox.cs) | `TypewriterEffect()` L92 |
| 사각형 테두리 UI (Graphic 상속, 깜박임 효과) | [UIBorderFrame.cs](Assets/Scripts/UI/Tutorial/UIBorderFrame.cs) | `OnPopulateMesh()` L18, `Update()` L59 |
| 꺾인 연결선 UI (Graphic 상속, start→bend→end 두 세그먼트) | [UIConnectLine.cs](Assets/Scripts/UI/UITab/UIConnectLine.cs) | `SetBentPoints()` L16 |
| 채워진 원 UI (Graphic 상속, 순수 메시 생성) | [UIFilledCircle.cs](Assets/Scripts/UI/UITab/UIFilledCircle.cs) | `OnPopulateMesh()` L12 |
| 튜토리얼 클릭 핸들러 (IPointerClickHandler, targetId 기반) | [TutorialClickHandler.cs](Assets/Scripts/System/Tutorial/TutorialClickHandler.cs) | `OnPointerClick()` L11 → `TutorialManager.OnTargetClicked()` |

---

## 패널/팝업 기반 구조

| 기능 | 파일 | 줄 |
|------|------|----|
| 패널 기반 클래스 (panelName, bMainPanel, bHideCurWhenActive) | [UIPanelBase.cs](Assets/Scripts/UI/UIPanel_Game/UIPanelBase.cs) | `InitializeUIPanel()`, `OnShowUIPanel()`, `OnHideUIPanel()` |
| Space 씬 패널 매니저 (Panel_Game 프리팹 일괄 로드) | [UISpace.cs](Assets/Scripts/UI/UISpace.cs) | `InitializeUIManager()` L6 (UIManager 서브클래스) |
| 버튼 → 패널 전환 간단 컴포넌트 (PanelAction enum) | [PanelButton.cs](Assets/Scripts/UI/PanelButton.cs) | `OnButtonClick()` L58 (Show/Hide/Toggle/ShowMain) |
| 라디오 버튼 그룹 (단일 선택, allowDeselect, 색상 관리) | [ButtonGroupSystem.cs](Assets/Scripts/UI/ButtonGroupSystem.cs) | `Select()` L64, `Deselect()` L93 |
| 버튼 그룹 아이템 | [ButtonGroupSystem.cs](Assets/Scripts/UI/ButtonGroupSystem.cs) | `ButtonGroupItem` L7 |
| TabSystem 예제/헬퍼 (SimpleTab → TabData 변환) | [SimpleTabExample.cs](Assets/Scripts/UI/SimpleTabExample.cs) | `SetupSimpleTabs()` L21 |
| 가상 스크롤뷰 (viewport 크기만큼만 아이템 생성/재활용) | [InfiniteScrollView.cs](Assets/Scripts/UI/ETC/InfiniteScrollView.cs) | `Initialize()` L32, `JumpToIndex()` L94, `RefreshVisible()` L109 |
| 레이블+값 1행 UI (로컬라이즈 지원, value1/value2) | [RowLabelValue.cs](Assets/Scripts/UI/ETC/RowLabelValue.cs) | `SetRow()` L20, `SetLabel()` L32 |
| 로딩씬 UI (랜덤 팁 표시, LoadingManager에 UI 참조 전달) | [UILoading.cs](Assets/Scripts/System/Loading/UILoading.cs) | `Start()` L22 |

---

## 차트 (RadarChart)

| 기능 | 파일 | 줄 |
|------|------|----|
| 레이더 차트 기반 (Graphic 상속, 6축 다각형) | [RadarChart.cs](Assets/Scripts/UI/Chart/RadarChart.cs) | `OnPopulateMesh()` L49 |
| 데이터 채우기 영역 그리기 | [RadarChart.cs](Assets/Scripts/UI/Chart/RadarChart.cs) | `DrawDataArea()` L111 |
| 그리드 레벨 그리기 | [RadarChart.cs](Assets/Scripts/UI/Chart/RadarChart.cs) | `DrawGrid()` L65 |
| 간단한 레이더 차트 (라벨 자동 생성, CapabilityProfile 바인딩) | [SimpleRadarChart.cs](Assets/Scripts/UI/Chart/SimpleRadarChart.cs) | `SetRadarChartStats()` L49 |
| 레이더 차트 라벨 동적 생성 | [SimpleRadarChart.cs](Assets/Scripts/UI/Chart/SimpleRadarChart.cs) | `CreateLabels()` L93 |

---

## 유틸리티 컴포넌트

| 기능 | 파일 | 줄 |
|------|------|----|
| 안전 영역 어댑터 (노치/다이나믹 아일랜드 대응) | [SafeAreaAdapter.cs](Assets/Scripts/System/Util/SafeAreaAdapter.cs) | `ApplySafeArea()` L23 |
| 직렬화 가능 Dictionary (ISerializationCallbackReceiver) | [SerializableDictionary.cs](Assets/Scripts/System/Util/SerializableDictionary.cs) | `SerializableDictionary<TKey,TValue>` L6 |
| 함체 프리팹 메시 합치기 대상 지정 | [CombineMeshTarget.cs](Assets/Scripts/System/Util/CombineMeshTarget.cs) | `m_combineTargets` L8 |
| 금지어 테이블 ScriptableObject (JSON Export, 클라 실시간 체크) | [DataTableForbiddenWords.cs](Assets/Scripts/System/Data/DataTableForbiddenWords.cs) | `ContainsForbiddenWord()` L52, `ExportToJson()` L38 |

---

## 디버그 / 개발 도구

| 기능 | 파일 | 줄 |
|------|------|----|
| SpaceScene 직접 실행 더미 데이터 주입 (`#if UNITY_EDITOR`) | [SpaceSceneDebugBootstrap.cs](Assets/Scripts/Debug/SpaceSceneDebugBootstrap.cs) | `Awake()` L13 (DataManager 미초기화 시만 실행) |
| 더미 캐릭터 주입 | [SpaceSceneDebugBootstrap.cs](Assets/Scripts/Debug/SpaceSceneDebugBootstrap.cs) | `InjectDebugCharacter()` L23 |
| 더미 함대 주입 | [SpaceSceneDebugBootstrap.cs](Assets/Scripts/Debug/SpaceSceneDebugBootstrap.cs) | `InjectDebugFleet()` L38 |
| 존 천체 씬뷰 미리보기 (`#if UNITY_EDITOR`) | [ZonePreviewComponent.cs](Assets/Scripts/Debug/ZonePreviewComponent.cs) | `RefreshPreview()` L22 |
| 씬 오브젝트 → DataTableZone 반영 | [ZonePreviewComponent.cs](Assets/Scripts/Debug/ZonePreviewComponent.cs) | `ApplyFromScene()` L107 (Undo.RecordObject) |
| 천체 프리뷰 동기화 | [ZonePreviewComponent.cs](Assets/Scripts/Debug/ZonePreviewComponent.cs) | `SyncPreviewPlanet()` L71 |
| 테스트 씬 (함대 재스폰, 이펙트 숫자키 스폰) | [TestScene.cs](Assets/Scripts/System/Dev/TestScene.cs) | `RespawnMyFleet()` L81 |
| bodyPrefab 이름 → EModuleSubType 파싱 | [TestScene.cs](Assets/Scripts/System/Dev/TestScene.cs) | `SpawnTestShip()` L110 |
| 디버그 오버레이 (화면 좌상 텍스트 표시, DontDestroyOnLoad) | [DebugOverlay.cs](Assets/Scripts/System/Dev/DebugOverlay.cs) | `SetText()` L45, `CreateDebugUI()` L17 |

---

## 에디터 전용 도구 (Assets/Scripts/Editor/)

| 기능 | 파일 |
|------|------|
| Jenkins Android 빌드 진입점 (keystore/버전 환경변수 주입) | [BuildScript.cs](Assets/Scripts/Editor/BuildScript.cs) `BuildAndroid()` |
| 전체 DataTable 일괄 JSON Export 창 | [DataTableTotalEditor.cs](Assets/Scripts/Editor/DataTableTotalEditor.cs) `Tools > DataTable Total Manager` |
| DataTableModule 커스텀 인스펙터 (CSV Import/JSON Export) | [DataTableModuleEditor.cs](Assets/Scripts/Editor/DataTableModuleEditor.cs) |
| DataTableZone 커스텀 인스펙터 (CSV Import, 천체/파고 편집) | [DataTableZoneEditor.cs](Assets/Scripts/Editor/DataTableZoneEditor.cs) |
| DataTableResearch 커스텀 인스펙터 (CSV Import/JSON Export) | [DataTableResearchEditor.cs](Assets/Scripts/Editor/DataTableResearchEditor.cs) |
| DataTableConfig 커스텀 인스펙터 | [DataTableConfigEditor.cs](Assets/Scripts/Editor/DataTableConfigEditor.cs) |
| DataTableForbiddenWords 커스텀 인스펙터 | [DataTableForbiddenWordsEditor.cs](Assets/Scripts/Editor/DataTableForbiddenWordsEditor.cs) |
| 아이콘 아틀라스 빌드 (Slice+Rename+TMP SpriteAsset 갱신) | [IconAtlasBuilder.cs](Assets/Scripts/Editor/IconAtlasBuilder.cs) `Tools > Icon Atlas > Build` |
| 천체 목록 Inspector GUI 공통 헬퍼 | [CelestialBodyEditorGUI.cs](Assets/Scripts/Editor/CelestialBodyEditorGUI.cs) `DrawCelestialBodyList()` |
| TMP 폰트 Static → Dynamic 일괄 변환 | [FontAssetToDynamicConverter.cs](Assets/Scripts/Editor/FontAssetToDynamicConverter.cs) `Tools > TMP > Convert KR Fonts` |
| HangerFlightPath 씬뷰 편집 (Catmull-Rom WP 생성/삭제) | [HangerFlightPathEditor.cs](Assets/Scripts/Editor/HangerFlightPathEditor.cs) |
| ModuleBody 커스텀 인스펙터 | [ModuleBodyEditor.cs](Assets/Scripts/Editor/ModuleBodyEditor.cs) |
| ModuleSlot 커스텀 인스펙터 | [ModuleSlotEditor.cs](Assets/Scripts/Editor/ModuleSlotEditor.cs) |
| SerializableDictionary PropertyDrawer | [SerializableDictionaryDrawer.cs](Assets/Scripts/Editor/SerializableDictionaryDrawer.cs) |
| ShieldGrid 씬뷰 편집 (Generate/Clear 버튼) | [ShieldGridEditor.cs](Assets/Scripts/Editor/ShieldGridEditor.cs) |
| TestScene 커스텀 인스펙터 | [TestSceneEditor.cs](Assets/Scripts/Editor/TestSceneEditor.cs) |
| TutorialData ScriptableObject 편집 UI | [TutorialDataEditor.cs](Assets/Scripts/Editor/TutorialDataEditor.cs) |
| ZonePreviewComponent 커스텀 인스펙터 (Refresh/Clear/Apply) | [ZonePreviewComponentEditor.cs](Assets/Scripts/Editor/ZonePreviewComponentEditor.cs) |
| CombineMeshTarget 커스텀 인스펙터 (Combine Mesh 실행) | [CombineMeshTargetEditor.cs](Assets/Scripts/Editor/CombineMeshTargetEditor.cs) |

---

## 진형 에디터 도구

| 기능 | 파일 | 줄 |
|------|------|----|
| 기본 진형 프리셋 에셋 자동 생성 (Linear/Cross/X/Circle) | [FormationPresetGenerator.cs](Assets/Scripts/Space/Fleet/FormationPresetGenerator.cs) | `Tools > Formation > Generate Presets` |
| 씬뷰 진형 슬롯 Gizmo 시각화 (구체 + positionIndex 라벨) | [FormationPreview.cs](Assets/Scripts/Space/Fleet/FormationPreview.cs) | `OnDrawGizmosSelected()` |
| 미사일 노즐 ON/OFF/Pulse 제어 (풀 반환 시 ResetNozzle) | [BurstNozzle.cs](Assets/Scripts/Space/Projectile/BurstNozzle.cs) | `ResetNozzle()` |
| 슬롯 위치/타입/발사대 속성 (카메라 목표값, 이젝트 속도) | [ModuleSlot.cs](Assets/Scripts/Space/Fleet/ModuleSlot.cs) | `m_cameraRotationY/X`, `m_cameraZoom`, `m_missileEjectSpeed` |
| 발사대 FirePoint 인덱스 태그 컴포넌트 | [FirePoint.cs](Assets/Scripts/Space/Fleet/FirePoint.cs) | `m_index` |
| 기술 요구사항 래퍼 구조체 (techLevel=0이면 요구없음) | [RequireStruct.cs](Assets/Scripts/System/Data/RequireStruct.cs) | `techLevel` |
| 선택 모듈 GridOverlay 메시 동적 생성 | [SelectedModuleVisual.cs](Assets/Scripts/Space/Fleet/SelectedModuleVisual.cs) | `SetSelected()` L125 |
| 모듈 플레이스홀더 (데미지/상태변화 무반응) | [ModulePlaceholder.cs](Assets/Scripts/Space/Fleet/ModulePlaceholder.cs) | `InitializeModulePlaceholder()` |
