// 게임 오브젝트(함대, 투사체, 이펙트, 배경 데코) 생성·관리 및 Zone 전투(라운드 스폰·클리어) 제어
//using Mono.Cecil;
using NUnit.Framework.Constraints;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ObjectManager : MonoSingleton<ObjectManager>
{

    #region MonoSingleton ---------------------------------------------------------------
    protected override bool ShouldDontDestroyOnLoad => false;   // Destroyed when other scenes are loaded

    public PoolManager m_poolManager = new PoolManager();

    // 전투 종료 처리 중 데미지 차단 플래그
    public bool m_isBattleEnding = false;

    // 셀 웨이브 순차 스폰(첫 웨이브 제외 나머지) — StartZoneEnemyWaves로 등록, term 간격으로 스폰하되 현재 웨이브를 먼저 전멸시키면 즉시 다음 웨이브 스폰
    private List<FleetInfo> m_pendingZoneWaves;
    private bool[] m_zoneWaveSpawned;
    private Coroutine[] m_zoneWaveTimerCoroutines;
    private float m_zoneWavesBattleStartTime;
    private float m_zoneWaveSpawnTermSec;
    private Vector3 m_zoneWaveBasePos;

    protected override void OnInitialize()
    {
        DataManager.Instance.ApplyGameSettings();

        InitializePools();
    }

    private void InitializePools()
    {
        m_poolManager.InitializePoolManager(this);

        ProjectileBeam projectileBeamPrefab = ResourceManager.Instance.Load<ProjectileBeam>("Prefabs/Projectile/ProjectileBeam");
        if (projectileBeamPrefab != null)
            m_poolManager.CreatePool(EPoolName.PROJECTILE_BEAM, projectileBeamPrefab, 1, 50);
        else
            Debug.LogError("ProjectileBeamPrefab not found at Resources/Prefabs/Projectile/ProjectileBeam");

        ProjectileBeamHitscan projectileBeamHitscanPrefab = ResourceManager.Instance.Load<ProjectileBeamHitscan>("Prefabs/Projectile/ProjectileBeamHitscan");
        if (projectileBeamHitscanPrefab != null)
            m_poolManager.CreatePool(EPoolName.PROJECTILE_BEAM_HITSCAN, projectileBeamHitscanPrefab, 1, 20);
        else
            Debug.LogWarning("ProjectileBeamHitscan not found at Resources/Prefabs/Projectile/ProjectileBeamHitscan");

        ProjectileMissile projectileMissileSmallPrefab = ResourceManager.Instance.Load<ProjectileMissile>("Prefabs/Projectile/ProjectileMissileSmall");
        if (projectileMissileSmallPrefab != null)
            m_poolManager.CreatePool(EPoolName.PROJECTILE_MISSILE_SMALL, projectileMissileSmallPrefab, 1, 50);
        else
            Debug.LogError("ProjectileMissileSmall not found at Resources/Prefabs/Projectile/ProjectileMissileSmall");

        ProjectileMissile projectileMissileMediumPrefab = ResourceManager.Instance.Load<ProjectileMissile>("Prefabs/Projectile/ProjectileMissileMedium");
        if (projectileMissileMediumPrefab != null)
            m_poolManager.CreatePool(EPoolName.PROJECTILE_MISSILE_MEDIUM, projectileMissileMediumPrefab, 1, 50);
        else
            Debug.LogError("ProjectileMissileMedium not found at Resources/Prefabs/Projectile/ProjectileMissileMedium");

        ProjectileMissile projectileMissileLargePrefab = ResourceManager.Instance.Load<ProjectileMissile>("Prefabs/Projectile/ProjectileMissileLarge");
        if (projectileMissileLargePrefab != null)
            m_poolManager.CreatePool(EPoolName.PROJECTILE_MISSILE_LARGE, projectileMissileLargePrefab, 1, 50);
        else
            Debug.LogError("ProjectileMissileLarge not found at Resources/Prefabs/Projectile/ProjectileMissileLarge");

        InterceptorUnit interceptorUnitPrefab = ResourceManager.Instance.Load<InterceptorUnit>("Prefabs/Projectile/ProjectileInterceptor");
        if (interceptorUnitPrefab != null)
            m_poolManager.CreatePool(EPoolName.PROJECTILE_INTERCEPTOR, interceptorUnitPrefab, 1, 30);
        else
            Debug.LogError("ProjectileInterceptor not found at Resources/Prefabs/Projectile/ProjectileInterceptor");


        EffectBase effectPrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/EffectBeamHead");
        if (effectPrefab == null) Debug.LogError("Not found at Resources/Prefabs/Effect/EffectBeamHead");
        m_poolManager.CreatePool(EPoolName.EFFECT_BEAM_HEAD, effectPrefab, 5, 20);

        effectPrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/EffectBeamHit");
        if (effectPrefab == null) Debug.LogError("Not found at Resources/Prefabs/Effect/EffectBeamHit");
        m_poolManager.CreatePool(EPoolName.EFFECT_BEAM_HIT, effectPrefab, 5, 20);

        effectPrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/EffectBeamMuzzle");
        if (effectPrefab == null) Debug.LogError("Not found at Resources/Prefabs/Effect/EffectBeamMuzzle");
        m_poolManager.CreatePool(EPoolName.EFFECT_BEAM_MUZZLE, effectPrefab, 5, 20);

        EffectShieldHit effectShieldHitPrefab = ResourceManager.Instance.Load<EffectShieldHit>("Prefabs/Effect/EffectShieldHit");
        if (effectShieldHitPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_SHIELD_HIT, effectShieldHitPrefab, 5, 20);
        else
            Debug.LogError("EffectShieldHit not found at Resources/Prefabs/Effect/EffectShieldHit");


        EffectBase effectExplosionShipPrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/EffectExplosionShip");
        if (effectExplosionShipPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_EXPLOSION_SHIP, effectExplosionShipPrefab, 3, 10);
        else
            Debug.LogError("EffectExplosionShip not found at Resources/Prefabs/Effect/EffectExplosionShip");

        EffectBase effectExplosionMissileSmallPrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/EffectExplosionMissileSmall");
        if (effectExplosionMissileSmallPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_EXPLOSION_MISSILE_SMALL, effectExplosionMissileSmallPrefab, 3, 10);
        else
            Debug.LogError("EffectExplosionShip not found at Resources/Prefabs/Effect/EffectExplosionShip");


        EffectBase effectInterceptPrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/EffectIntercept");
        if (effectInterceptPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_INTERCEPT, effectInterceptPrefab, 3, 10);
        else
            Debug.LogError("EffectIntercept not found at Resources/Prefabs/Effect/EffectIntercept");

        EffectScout effectScoutPrefab = ResourceManager.Instance.Load<EffectScout>("Prefabs/Effect/EffectScout");
        if (effectScoutPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_SCOUT, effectScoutPrefab, 3, 10);
        else
            Debug.LogError("EffectScout not found at Resources/Prefabs/Effect/EffectScout");

        EffectBase effectSupportSparklePrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/EffectSupportSparkle");
        if (effectSupportSparklePrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_SUPPORT_SPARKLE, effectSupportSparklePrefab, 3, 10);
        else
            Debug.LogError("EffectSupportSparkle not found at Resources/Prefabs/Effect/EffectSupportSparkle");

        EffectBase effectWarpSpeedLinesPrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/WarpSpeedLines");
        if (effectWarpSpeedLinesPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_WARP_SPEEDLINES, effectWarpSpeedLinesPrefab, 5, 20);
        else
            Debug.LogError("WarpSpeedLines not found at Resources/Prefabs/Effect/WarpSpeedLines");

        EffectBase effectFireOnShipPrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/EffectFireOnShip");
        if (effectFireOnShipPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_FIRE_ON_SHIP, effectFireOnShipPrefab, 5, 15);
        else
            Debug.LogError("EffectFireOnShip not found at Resources/Prefabs/Effect/EffectFireOnShip");

        EffectBase effectScorchMarkPrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/EffectScorchMark");
        if (effectScorchMarkPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_SCORCH_MARK, effectScorchMarkPrefab, 10, 30);
        else
            Debug.LogError("EffectScorchMark not found at Resources/Prefabs/Effect/EffectScorchMark");





        AircraftStandard aircraftStandardPrefab = ResourceManager.Instance.Load<AircraftStandard>("Prefabs/Aircraft/AircraftStandard");
        if (aircraftStandardPrefab != null)
            m_poolManager.CreatePool(EPoolName.AIRCRAFT_STANDARD, aircraftStandardPrefab, 1, 30);
        else
            Debug.LogError("AircraftStandardPrefab not found at Resources/Prefabs/Aircraft/AircraftStandard");

    }
    #endregion

    private CelestialBodySpawner m_celestialBodySpawner;
    private DebrisFieldSpawner m_debrisFieldSpawner;

    [HideInInspector] public List<SpaceFleet> m_teamAFleets = new List<SpaceFleet>();
    [HideInInspector] public List<SpaceFleet> m_teamBFleets = new List<SpaceFleet>();
    [HideInInspector] public List<SpaceFleet> m_teamCFleets = new List<SpaceFleet>();
    private static readonly List<SpaceFleet> s_emptyTeamFleets = new List<SpaceFleet>();

    // 지금 이 클라이언트를 플레이 중인 유저가 속한 팀 — 현재는 항상 TeamA지만, 향후 멀티플레이(같은 전투에 여러 유저)에서 B가 될 수도 있어 값으로 분리해둠
    [HideInInspector] public ETeam m_myTeam = ETeam.TeamA;

    // 진행 중인 탐험 존 런에서 선택 확정한 보상카드 지속버프 상태 — UIPanelExplorationGrid가 카드 선택/재접속 복구 시 채우고,
    // ObjectManager는 내 함대 스폰 시(SpawnFleetFromPreset) 이 값을 읽어 최종 스탯에 적용만 함(소유권은 그리드 패널에 있음)
    public RewardCardSessionState m_rewardCardSessionState = new RewardCardSessionState();

    // 내 팀의 대표 함대(=기존 m_myFleet) — 팀 리스트 순서(index)가 아닌 fleetSource로 실제 플레이어 함대를 식별
    // (시네마틱 함대도 같은 TeamA에 등록될 수 있어 리스트 첫 번째로 판단하면 오판 가능)
    public SpaceFleet GetMyFleet()
    {
        List<SpaceFleet> fleets = GetTeamFleets(m_myTeam);
        for (int i = 0; i < fleets.Count; i++)
        {
            if (fleets[i] != null && fleets[i].m_fleetSource == EFleetSource.fleet_source_player)
                return fleets[i];
        }
        return null;
    }

    // 보상카드 지속버프가 바뀔 때(카드 선택/런 종료 초기화) 호출 — 이미 스폰된 내 함대의 모든 함선에 최신 배율을 다시 반영하고,
    // 버프 아이콘 UI(UIPanelExplorationGrid/UIPanelBattle의 UIRewardCardBuffDisplay)도 즉시 갱신되도록 이벤트 발행
    // 아키타입 패시브 효과(능력치 배율 등)가 바뀌었을 때(함선 생성/파괴/배치 해제, 패시브 해금) 호출 — 모든 함선의 효과 캐시를 먼저 비우고 모듈 값(체력/공격력/실드)을 다시 계산
    // 캐시를 전부 비운 뒤에 다시 계산해야 함 — 함선별로 비우고 계산하면 아직 비우지 않은 다른 함선의 낡은 값을 참조함
    public void RefreshPassiveEffectsOnFleet(SpaceFleet fleet)
    {
        if (fleet == null) return;

        foreach (SpaceShip ship in fleet.m_ships)
            if (ship != null) ship.InvalidatePassiveEffectCache();

        foreach (SpaceShip ship in fleet.m_ships)
            if (ship != null) ship.RefreshRewardCardBuffs();

        EventManager.Trigger_FleetUpdateHP();
        EventManager.Trigger_ShipUpdateHP();
    }

    public void RefreshPassiveEffectsOnMyFleet()
    {
        RefreshPassiveEffectsOnFleet(GetMyFleet());
    }

    public void RefreshRewardCardBuffsOnMyFleet()
    {
        SpaceFleet myFleet = GetMyFleet();
        if (myFleet != null)
        {
            foreach (SpaceShip ship in myFleet.m_ships)
                if (ship != null) ship.RefreshRewardCardBuffs();
        }

        EventManager.Trigger_RewardCardBuffsChanged();
    }

    // 내 팀 기준 적 팀 함대 목록(=기존 m_enemyFleets) — UI/카메라 등 "플레이어 관점" 참조용
    public List<SpaceFleet> GetEnemyFleets()
    {
        return GetOpposingTeamFleets(m_myTeam);
    }

    // fleet이 내 팀이 아니면 true(=기존 IsEnemy) — 플레이어 관점의 아군/적 판정
    public bool IsEnemyOfMyTeam(SpaceFleet fleet)
    {
        return fleet != null && fleet.m_team != m_myTeam;
    }

    public List<SpaceFleet> GetTeamFleets(ETeam team)
    {
        if (team == ETeam.TeamA) return m_teamAFleets;
        if (team == ETeam.TeamB) return m_teamBFleets;
        return m_teamCFleets;
    }

    // team 소속 함대의 교전 상대 팀 반환 (TeamA<->TeamB만 서로 적대, TeamC는 기본적으로 교전 없음)
    public List<SpaceFleet> GetOpposingTeamFleets(ETeam team)
    {
        if (team == ETeam.TeamA) return m_teamBFleets;
        if (team == ETeam.TeamB) return m_teamAFleets;
        return s_emptyTeamFleets;
    }

    // TeamA<->TeamB만 서로 적대 팀으로 취급 (TeamC는 자기 자신 반환 — 교전 상대 없음)
    public ETeam GetOpposingTeam(ETeam team)
    {
        if (team == ETeam.TeamA) return ETeam.TeamB;
        if (team == ETeam.TeamB) return ETeam.TeamA;
        return ETeam.TeamC;
    }

    // 활성 미사일 추적 — 아군/적군 분리. 빔의 미사일 관통 파괴 판정(ProjectileBeam.cs)과 미사일끼리 우연히 충돌했을 때의
    // 파괴 판정(ProjectileMissile.CheckCollision)이 사용
    public List<ProjectileMissile> m_friendlyMissiles = new List<ProjectileMissile>();
    public List<ProjectileMissile> m_enemyMissiles    = new List<ProjectileMissile>();

    public void RegisterMissile(ProjectileMissile missile, bool isEnemy)
    {
        List<ProjectileMissile> list = isEnemy ? m_enemyMissiles : m_friendlyMissiles;
        if (list.Contains(missile) == false)
            list.Add(missile);
    }

    public void UnregisterMissile(ProjectileMissile missile)
    {
        m_friendlyMissiles.Remove(missile);
        m_enemyMissiles.Remove(missile);
    }

    // 초기화 순서가 이슈인 경우 이곳에서 순차적으로 진행
    private void Start()
    {
        NetworkManager.Instance.OnChangeScene();
        UIManager.Instance.InitializeUIManager();// UI 초기화
        SoundManager.Instance.InitializeSoundManager();// SoundManager 이벤트 구독이 SpawnFleet→TriggerMyFleetStateChanged 이전에 완료되어야 함
        m_celestialBodySpawner = GetComponent<CelestialBodySpawner>();
        if (m_celestialBodySpawner != null)
            m_celestialBodySpawner.SpawnZone(GetInitialZoneIndex());
        m_debrisFieldSpawner = GetComponent<DebrisFieldSpawner>();
        if (m_debrisFieldSpawner != null)
            m_debrisFieldSpawner.SpawnZone(GetInitialZoneIndex());
        AdManager.Instance.ToString();// 광고 초기화 (존 입장 전 미리 로드)

        // 튜토리얼 진행도는 SelectCommander 응답 시점(UIMain.cs)에 이미 확보되어 있음
        StartNormalPlay();
    }

    // 탐사 그리드를 연 적이 없어도, UITabExplorationGrid가 실제로 계산할 것과 동일한 시작 셀 월드좌표를 미리 구함 —
    // 구식 스테이지 좌표(GetZoneFirstStage/ResolveFleetWorldPosition, 수천~만 단위 오프셋)는 신규 탐사 그리드가 쓰는
    // 좌표 공간(ZoneConfig.galaxyCameraTarget 기준)과 전혀 달라 그리드로 진입 시 함대가 엉뚱한 곳에서 튀어나오므로 주의
    public Vector3 GetInitialGridStartCellPosition()
    {
        int zoneNumber = GetInitialZoneIndex();
        ZoneConfig zoneConfig = DataManager.Instance.m_dataTableZone.GetZoneByZoneIndex(zoneNumber);
        if (zoneConfig == null) return Vector3.zero;

        ExplorationGridData gridData = ExplorationGridGenerator.Generate(zoneConfig);

        int startRow = gridData.startRow;
        int startCol = gridData.startCol;
        bool hasActiveCell = TryGetActiveExplorationCell(out int activeRow, out int activeCol);
        if (hasActiveCell == true && gridData.IsInBounds(activeRow, activeCol) == true)
        {
            startRow = activeRow;
            startCol = activeCol;
        }

        return gridData.GetCell(startRow, startCol).worldPos;
    }

    protected override void OnDestroy()
    {
        EventManager.Unsubscribe_MyFleetDestroyed(OnMyFleetDestroyed);
        base.OnDestroy();
    }

    private void OnMyFleetDestroyed()
    {
        ForceEndBattle(false);
    }

    // 전투 종료 판정 후 실제 정리(함선 제거 등)까지의 텀 — 마지막 격추 연출이 끝날 시간을 확보
    private const float BATTLE_END_DELAY_SEC = 1.5f;

    // 전투 강제 종료 (전멸/퇴각 공통) — delaySec 기본값은 격추 연출 대기용, 유저가 직접 누른 퇴각은 연출 기다릴 이유가 없어 0으로 즉시 처리 가능
    public void ForceEndBattle(bool isVictory, float delaySec = BATTLE_END_DELAY_SEC)
    {
        // 양 함대가 거의 동시에 전멸하는 등 승패 판정이 근접 타이밍에 중복 발생할 수 있음 —
        // 먼저 확정된 결과만 인정하고 뒤이어 들어오는 결과는 무시
        if (m_isBattleEnding == true) return;
        m_isBattleEnding = true;
        StartCoroutine(DelayedEndBattle(isVictory, delaySec));
    }

    private IEnumerator DelayedEndBattle(bool isVictory, float delaySec)
    {
        if (delaySec > 0f)
            yield return new WaitForSeconds(delaySec);

        PassiveAudit.OnCombatEnd(isVictory);
        SpaceFleet myFleet = GetMyFleet();
        bool isPvp = myFleet != null && myFleet.m_fleetState == EUnitState.BattlePvp;

        GameSpeedController.Reset(); // timeScale 및 오디오 피치 복원
        if (myFleet != null)
            myFleet.SetFleetState(EUnitState.Idle);
        SyncBattleFrontRearChangesToServer(myFleet);
        StopEnemySpawning();
        OrderAllAircraftReturn();
        CleanupAllProjectiles();
        RemoveAllEnemyFleets();

        if (isPvp)
            EventManager.TriggerPvpBattleEnd(isVictory);
        else
            EventManager.TriggerZoneStageBattleEnd(isVictory);
    }

    // 전투 중 함선 탭으로 토글된 전방/후방(SetMyFleetShipFront, 서버 미전송)을 전투 종료 시점에 한 번에 서버로 반영
    private void SyncBattleFrontRearChangesToServer(SpaceFleet myFleet)
    {
        if (myFleet == null) return;

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return;

        for (int i = 0; i < myFleet.m_ships.Count; i++)
        {
            SpaceShip ship = myFleet.m_ships[i];
            if (ship == null) continue;

            int positionIndex = ship.m_shipInfo.positionIndex;
            bool isFront = ship.m_shipInfo.isFront;
            if (composition.GetIsFront(positionIndex) == isFront) continue;

            composition.SetFront(positionIndex, isFront);
            NetworkManager.Instance.SetFleetShipFront(new FleetSetFrontRequest
            {
                slotIndex = positionIndex,
                isFront = isFront,
            });
        }
    }

    private void StartNormalPlay()
    {
        SpawnFleet();
        ApplyPendingActiveZoneRunProgress();
        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetDestroyed);// 플레이어 함대 전멸 이벤트 구독
        NetworkManager.Instance.StartHeartbeat();
        UIManager.Instance.ShowMainPanel();

        // 출석 보상은 자동 지급되지 않고 레드닷만 갱신되므로(달력은 유저가 직접 열어야 함) 튜토리얼 시작을 지연시킬 필요 없음
        if (DailyBonusManager.Instance != null)
            DailyBonusManager.Instance.CheckDailyBonusStatus();
        if (NoticeManager.Instance != null)
            NoticeManager.Instance.CheckAndShowNotices();
    }

    // MainScene(UIMain.SelectCommander)에서 SpaceScene 진입 전에 미리 받아둔 진행중 존런 스냅샷(있으면)을
    // 함대 스폰 직후 같은 프레임에 적용 — 죽은 함선이 잠깐이라도 풀피로 그려지는 프레임 자체가 없음.
    // 탐사UI(UIPanelExplorationGrid)의 RequestActiveZoneRunProgress는 적립포인트/클리어셀 등 그리드 상태
    // 복원용으로 별도 유지하되, 보상카드 지속버프는 여기서 이미 끝났으므로 거기선 재적용하지 않음(체력 스냅샷만 자체적으로 다시 적용)
    private void ApplyPendingActiveZoneRunProgress()
    {
        GetActiveZoneRunProgressResponse progress = DataManager.Instance.m_pendingActiveZoneRunProgress;
        DataManager.Instance.m_pendingActiveZoneRunProgress = null; // 1회성 소비
        if (progress == null) return;

        m_rewardCardSessionState.ApplyPersistentCardIds(progress.purchasedRewardCards);
        RefreshRewardCardBuffsOnMyFleet(); // 이미 스폰된 내 함선 모듈/체력에 즉시 반영
        ApplyFleetHealthSnapshotOnMyFleet(progress.shipHealthRatios);
    }

    // 슬롯 포지션 인덱스 기준으로 이미 스폰된 내 함대에 마지막 클리어 시점 체력/실드 스냅샷을 적용 —
    // UIPanelExplorationGrid.ApplyFleetHealthSnapshot과 동일한 방식, 로그인 직후(탐사UI 진입 전) 복원용
    private void ApplyFleetHealthSnapshotOnMyFleet(System.Collections.Generic.List<ShipHealthRatioInfo> shipHealthRatios)
    {
        if (shipHealthRatios == null || shipHealthRatios.Count == 0) return;

        SpaceFleet myFleet = GetMyFleet();
        if (myFleet == null) return;

        foreach (ShipHealthRatioInfo entry in shipHealthRatios)
        {
            SpaceShip ship = myFleet.m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == entry.positionIndex);
            if (ship == null) continue;

            if (entry.healthRatio <= 0f)
                ship.DestroySilently(); // 마지막 클리어 시점에 이미 파괴됐던 슬롯 — 되살아난 풀피 함선을 조용히 다시 파괴 상태로 동기화
            else
                ship.ApplyHealthAndShieldRatio(entry.healthRatio, entry.shieldRatio);
        }
    }

    public int GetInitialZoneIndex()
    {
        var commander = DataManager.Instance.m_currentCommander;
        if (commander == null || commander.m_commanderInfo == null) return 1;

        // 진행 중인 탐험 런이 있으면 그 존을 우선 사용 — 아직 탈출(완전 클리어)하지 못한 상태(highestClearedZoneNumber에는 안 잡힘)라도
        // 재접속 시 원래 있던 존으로 복귀해야 함
        int activeZoneNumber = commander.m_commanderInfo.explorationZoneNumber;
        if (activeZoneNumber > 0) return activeZoneNumber;

        // 진행 중인 런이 없으면 탈출로 확정 클리어한 최고 존의 다음 존으로 이동 — 없으면(0) 자연스럽게 zone 1
        int nextZoneNumber = commander.m_commanderInfo.highestClearedZoneNumber + 1;

        int zoneCount = DataManager.Instance.m_dataTableZone != null ? DataManager.Instance.m_dataTableZone.zoneList.Count : 0;
        if (zoneCount > 0 && nextZoneNumber > zoneCount) return zoneCount;

        return nextZoneNumber;
    }

    // 진행 중인 탐험 런의 마지막 클리어 셀 좌표(0-indexed) — CommanderInfo.explorationCell("row-col") 파싱
    public bool TryGetActiveExplorationCell(out int row, out int col)
    {
        row = 0;
        col = 0;

        var commander = DataManager.Instance.m_currentCommander;
        if (commander == null || commander.m_commanderInfo == null) return false;

        string explorationCell = commander.m_commanderInfo.explorationCell;
        if (string.IsNullOrEmpty(explorationCell) == true) return false;

        int dashIdx = explorationCell.IndexOf('-');
        if (dashIdx <= 0) return false;

        bool rowParsed = int.TryParse(explorationCell.Substring(0, dashIdx), out row);
        bool colParsed = int.TryParse(explorationCell.Substring(dashIdx + 1), out col);
        return rowParsed == true && colParsed == true;
    }

    public void ChangeZone(int zoneIndex)
    {
        if (m_celestialBodySpawner != null)
            m_celestialBodySpawner.SpawnZone(zoneIndex);
        if (m_debrisFieldSpawner != null)
            m_debrisFieldSpawner.SpawnZone(zoneIndex);
    }



    // Zone 적 함대 웨이브 — term 기준 순차 스폰, 전멸 시 다음 웨이브 즉시 스폰
    // 함선 시스템 대격변으로 구존-스테이지 로직 전체 주석처리 — 삭제 아님, UITabExploration.cs와 함께 비활성화
    // ZoneStageConfig 자체가 제거되어 필드 선언도 함께 주석 처리 — 복원 시 함께 되살릴 것
    /*
    private List<StageEnemyFleetSpawnConfig> m_pendingWaves;
    private bool[] m_waveSpawned;
    private Coroutine[] m_waveTimerCoroutines;
    private float m_wavesBattleStartTime;
    private ZoneStageConfig m_currentWaveStage;

    public void StartZoneEnemyWaves(List<StageEnemyFleetSpawnConfig> waves, ZoneStageConfig zoneStage)
    {
        m_pendingWaves             = waves;
        m_waveSpawned              = new bool[waves.Count];
        m_waveTimerCoroutines      = new Coroutine[waves.Count];
        m_wavesBattleStartTime     = Time.time;
        m_currentWaveStage         = zoneStage;

        GameSpeedController.RestoreSpeed();

        for (int i = 0; i < waves.Count; i++)
        {
            int idx = i;
            m_waveTimerCoroutines[idx] = StartCoroutine(WaveTimerCoroutine(idx));
        }
    }

    private IEnumerator WaveTimerCoroutine(int waveIndex)
    {
        float elapsed   = Time.time - m_wavesBattleStartTime;
        float spawnTime = m_pendingWaves[waveIndex].fleetIndex * m_currentWaveStage.spawnTerm;
        float remaining = spawnTime - elapsed;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        if (m_waveSpawned[waveIndex] == false)
            SpawnWave(waveIndex);
    }

    private void SpawnWave(int waveIndex)
    {
        if (m_waveSpawned[waveIndex] == true) return;
        m_waveSpawned[waveIndex] = true;

        StageEnemyFleetSpawnConfig wave = m_pendingWaves[waveIndex];
        if (wave.fleetInfo == null || wave.fleetInfo.ships == null || wave.fleetInfo.ships.Count == 0) return;

        SpaceFleet myFleet = GetMyFleet();
        Vector3 spawnPos = GetEnemySpawnPositionFromWave(wave);
        Vector3 dirToPlayer = myFleet != null ? myFleet.transform.position - spawnPos : Vector3.zero;
        Quaternion spawnRot = dirToPlayer != Vector3.zero ? Quaternion.LookRotation(dirToPlayer) : Quaternion.identity;

        GameObject fleetObj = new GameObject($"EnemyFleet_{waveIndex}");
        fleetObj.transform.position = spawnPos;
        fleetObj.transform.rotation = spawnRot;

        ETeam enemyTeam = GetOpposingTeam(m_myTeam);
        SpaceFleet newFleet = fleetObj.AddComponent<SpaceFleet>();
        GetTeamFleets(enemyTeam).Add(newFleet);

        // fleet_source_zone_data → IsZoneEnemy == true → 전멸 시 OnZoneEnemyFleetDefeated 호출
        newFleet.InitializeSpaceFleet(wave.fleetInfo, enemyTeam, EFleetSource.fleet_source_zone_data, EUnitState.Move);
        float playerDelay = m_currentWaveStage != null ? m_currentWaveStage.playerFireDelaySec : 0f;
        float enemyDelay  = m_currentWaveStage != null ? m_currentWaveStage.enemyFireDelaySec  : 0f;
        newFleet.StartFleetWarpIn(() =>
        {
            TryStartCombat(newFleet, EUnitState.BattleExploration, playerDelay, enemyDelay);
        });
    }

    private Vector3 GetEnemySpawnPositionFromWave(StageEnemyFleetSpawnConfig wave)
    {
        SpaceFleet myFleet = GetMyFleet();
        if (myFleet == null) return Vector3.zero;
        FleetPositionPreset preset = DataManager.Instance.m_dataTableZone.GetFleetPosition(wave.positionIndex);
        if (preset == null) return Vector3.zero;

        // 내 함대를 중심으로 한 구 표면 위에 배치 — distance가 항상 실제 거리(구의 반지름)가 되도록 y를 눌러 깎지 않음
        Vector3 basePos      = myFleet.transform.position;
        Vector3 localDir     = Quaternion.Euler(preset.rotX, preset.rotY, preset.rotZ) * Vector3.forward;
        Vector3 worldDir     = myFleet.transform.TransformDirection(localDir);
        Vector3 spawnPos     = basePos + worldDir * preset.distance;
        return spawnPos;
    }
    */

    // PvP 전투 시작 - 서버에서 받은 상대 FleetInfo로 적 함대 생성
    // 함선 시스템 대격변으로 주석처리됐던 것을 UIPanelRank 2단계 구현에 맞춰 복원(의존 메서드 전부 현재 시그니처와 일치 확인함)
    // opponentPassiveProgress: 상대 지휘관의 아키타입 패시브 진행도 — 함선 스폰 전에 함대에 심어서 상대 함선도 같은 패시브 능력치 효과를 받게 함
    // opponentArchetypeUpgrades: 상대 지휘관의 아키타입 모듈강화 레벨 — 상대 함선 모듈이 내 강화 레벨이 아니라 상대 자신의 레벨로 만들어지게 함(함선 스폰 전에 심음)
    public void StartPvpBattle(FleetInfo opponentFleetInfo, List<HullArchetypeProgressInfo> opponentPassiveProgress, List<ArchetypeUpgradeInfo> opponentArchetypeUpgrades)
    {
        SpaceFleet myFleet = GetMyFleet();
        if (opponentFleetInfo == null || myFleet == null)
        {
            EventManager.TriggerPvpBattleEnd(false);
            return;
        }
        Vector3 spawnPosition = GetEnemySpawnPosition();
        GameObject fleetObj = new GameObject("PvpEnemyFleet");
        fleetObj.transform.position = spawnPosition;

        Vector3 directionToPlayer = myFleet.transform.position - spawnPosition;
        directionToPlayer.y = 0;
        if (directionToPlayer != Vector3.zero)
            fleetObj.transform.rotation = Quaternion.LookRotation(directionToPlayer);

        ETeam enemyTeam = GetOpposingTeam(m_myTeam);
        SpaceFleet enemyFleet = fleetObj.AddComponent<SpaceFleet>();
        // 진행도 응답이 없는 경우(구버전 서버 등)도 빈 목록으로 두어 문턱 0 단계(1단계)는 상대에게도 적용되게 함
        List<HullArchetypeProgressInfo> passiveProgress = opponentPassiveProgress != null ? opponentPassiveProgress : new List<HullArchetypeProgressInfo>();
        List<ArchetypeUpgradeInfo> archetypeUpgrades = opponentArchetypeUpgrades != null ? opponentArchetypeUpgrades : new List<ArchetypeUpgradeInfo>();
        enemyFleet.m_remoteBuildData = new CommanderBuildData(passiveProgress, archetypeUpgrades);
        enemyFleet.InitializeSpaceFleet(opponentFleetInfo, enemyTeam, EFleetSource.fleet_source_player_remote, EUnitState.Move);
        enemyFleet.StartFleetWarpIn(() =>
        {
            TryStartCombat(enemyFleet, EUnitState.BattlePvp);
        });

        GetTeamFleets(enemyTeam).Add(enemyFleet);
    }

    public void TryStartCombat(SpaceFleet enemyFleet, EUnitState battleState, float playerDelay = 0f, float enemyDelay = 0f)
    {
        GameSpeedController.RestoreSpeed(); // 이전 전투 배속 복원
        m_isBattleEnding = false;
        SpaceFleet myFleet = GetMyFleet();
        if (myFleet == null) return;
        if (enemyFleet == null) return;

        PassiveAudit.OnCombatStart(myFleet, enemyFleet);
        myFleet.SetFleetState(battleState);
        enemyFleet.SetFleetState(battleState);
        StartCoroutine(DelayedStartCombat(myFleet,    playerDelay));
        StartCoroutine(DelayedStartCombat(enemyFleet, enemyDelay));
    }

    private IEnumerator DelayedStartCombat(SpaceFleet fleet, float delaySec)
    {
        if (delaySec > 0f)
            yield return new WaitForSeconds(delaySec);

        if (fleet != null)
            fleet.StartCombat();
    }

    // 이벤트에 의한 함대 파괴 — 정식 파괴는 이곳, cleanup loop(GetEnemy)는 안전망
    public void RemoveEnemyFleet(SpaceFleet fleet)
    {
        if (fleet == null) return;

        List<SpaceFleet> enemyTeamFleets = GetOpposingTeamFleets(m_myTeam);
        enemyTeamFleets.Remove(fleet);
        DestroyFleet(fleet);

        SpaceFleet myFleet = GetMyFleet();
        if (myFleet != null && myFleet.m_fleetState == EUnitState.BattlePvp && enemyTeamFleets.Count == 0)
            ForceEndBattle(true);
    }

    public void StopEnemySpawning()
    {
        ClearPendingZoneWaves();
    }

    // 셀에 웨이브가 2개 이상일 때, 첫 웨이브(이미 스폰됨) 제외 나머지를 순차 스폰 등록 — UIPanelExplorationGrid.OnConfirmStartBattle에서 호출
    // wave0Pos: 이미 스폰된 웨이브0의 위치 — 나머지 웨이브는 내 함대를 중심으로 이 위치와 같은 반지름의 원주 위, 다른 각도에 배치됨
    public void StartZoneEnemyWaves(List<FleetInfo> remainingWaves, Vector3 wave0Pos, float spawnTermSec)
    {
        ClearPendingZoneWaves();
        if (remainingWaves == null || remainingWaves.Count == 0) return;

        m_pendingZoneWaves = remainingWaves;
        m_zoneWaveSpawned = new bool[remainingWaves.Count];
        m_zoneWaveTimerCoroutines = new Coroutine[remainingWaves.Count];
        m_zoneWavesBattleStartTime = Time.time;
        m_zoneWaveSpawnTermSec = spawnTermSec;
        m_zoneWaveBasePos = wave0Pos;

        for (int i = 0; i < remainingWaves.Count; i++)
        {
            int waveIndex = i;
            m_zoneWaveTimerCoroutines[waveIndex] = StartCoroutine(ZoneWaveTimerCoroutine(waveIndex));
        }
    }

    private IEnumerator ZoneWaveTimerCoroutine(int waveIndex)
    {
        // 나머지 웨이브 리스트의 0번이 두 번째 웨이브이므로 (waveIndex+1) * term 시점에 스폰
        float elapsed = Time.time - m_zoneWavesBattleStartTime;
        float spawnTime = (waveIndex + 1) * m_zoneWaveSpawnTermSec;
        float remaining = spawnTime - elapsed;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        if (m_zoneWaveSpawned[waveIndex] == false)
            SpawnZoneWave(waveIndex);
    }

    private void SpawnZoneWave(int waveIndex)
    {
        if (m_zoneWaveSpawned[waveIndex] == true) return;
        m_zoneWaveSpawned[waveIndex] = true;

        FleetInfo wave = m_pendingZoneWaves[waveIndex];
        if (wave == null || wave.ships == null || wave.ships.Count == 0) return;

        // 내 함대를 중심으로, 웨이브0과 같은 반지름의 원주 위에서 좌우로 각도를 번갈아 벌려 배치 — 겹치지 않을 정도만 떨어뜨림
        SpaceFleet myFleet = GetMyFleet();
        Vector3 center = myFleet != null ? myFleet.transform.position : m_zoneWaveBasePos;

        Vector3 toWave0 = m_zoneWaveBasePos - center;
        float radius = toWave0.magnitude;
        Vector3 baseDir = radius > 0.01f ? toWave0 / radius : Vector3.forward;

        const float k_waveAngleStepDeg = 30f;
        int angleStepCount = waveIndex / 2 + 1;
        float angleSign = waveIndex % 2 == 0 ? 1f : -1f;
        float angleOffsetDeg = angleSign * angleStepCount * k_waveAngleStepDeg;

        Vector3 spawnDir = Quaternion.AngleAxis(angleOffsetDeg, Vector3.up) * baseDir;
        Vector3 spawnPos = center + spawnDir * radius;
        Quaternion spawnRot = Quaternion.LookRotation(center - spawnPos);

        // 웨이브0(SpawnFleetFromPreset)과 완전히 동일한 스폰 방식 재사용 — ExplorationShipSpawnBridge로 함선 생성, Idle 상태로 대기
        ETeam enemyTeam = GetOpposingTeam(m_myTeam);
        SpaceFleet newFleet = SpawnFleetFromPreset(wave, enemyTeam, EFleetSource.fleet_source_zone_data, spawnPos, spawnRot, $"EnemyFleet_Wave{waveIndex + 1}");
        newFleet.StartFleetWarpIn(() =>
        {
            TryStartCombat(newFleet, EUnitState.BattleExploration, 0f, 0f);
        });
    }

    // 아직 스폰되지 않은 웨이브가 남아있는지 — 남아있으면 "적 팀 전멸"이 곧 클리어를 의미하지 않음
    private bool HasPendingZoneWaves()
    {
        if (m_zoneWaveSpawned == null) return false;
        for (int i = 0; i < m_zoneWaveSpawned.Length; i++)
        {
            if (m_zoneWaveSpawned[i] == false)
                return true;
        }
        return false;
    }

    private void ClearPendingZoneWaves()
    {
        if (m_zoneWaveTimerCoroutines != null)
        {
            for (int i = 0; i < m_zoneWaveTimerCoroutines.Length; i++)
            {
                if (m_zoneWaveTimerCoroutines[i] != null)
                    StopCoroutine(m_zoneWaveTimerCoroutines[i]);
            }
        }

        m_pendingZoneWaves = null;
        m_zoneWaveSpawned = null;
        m_zoneWaveTimerCoroutines = null;
    }

    // 모든 적 함대 제거
    public void RemoveAllEnemyFleets()
    {
        List<SpaceFleet> enemyTeamFleets = GetOpposingTeamFleets(m_myTeam);
        for (int i = enemyTeamFleets.Count - 1; i >= 0; i--)
        {
            if (enemyTeamFleets[i] != null)
                DestroyFleet(enemyTeamFleets[i]);
        }
        enemyTeamFleets.Clear();
    }

    // 함대 GameObject 파괴 공통 경로 — 요격체 유닛은 함선 자식으로 붙어 있어 함대와 같이 파괴되면 풀 반납(SetParent)이 막히므로, 파괴 전에 먼저 풀로 반납
    private void DestroyFleet(SpaceFleet fleet)
    {
        fleet.ClearAllInterceptorUnits();
        Destroy(fleet.gameObject);
    }

    // 모든 활성 빔/미사일: 코루틴/이펙트 정리 후 풀 반환 — 씬 전체 스캔 대신 풀 하위만 순회
    public void CleanupAllProjectiles()
    {
        foreach (var beam in m_poolManager.GetActiveInstances<ProjectileBeam>(EPoolName.PROJECTILE_BEAM))
            beam.ReturnToPool();

        foreach (var beam in m_poolManager.GetActiveInstances<ProjectileBeamHitscan>(EPoolName.PROJECTILE_BEAM_HITSCAN))
            beam.ReturnToPool();

        foreach (var missile in m_poolManager.GetActiveInstances<ProjectileMissile>(EPoolName.PROJECTILE_MISSILE_SMALL))
            missile.ReturnToPool(showHitEffect: false);
        foreach (var missile in m_poolManager.GetActiveInstances<ProjectileMissile>(EPoolName.PROJECTILE_MISSILE_MEDIUM))
            missile.ReturnToPool(showHitEffect: false);
        foreach (var missile in m_poolManager.GetActiveInstances<ProjectileMissile>(EPoolName.PROJECTILE_MISSILE_LARGE))
            missile.ReturnToPool(showHitEffect: false);
    }

    // 모든 함재기에게 귀환 명령 — 씬 전체 스캔 대신 풀 하위만 순회
    public void OrderAllAircraftReturn()
    {
        foreach (var aircraft in m_poolManager.GetActiveInstances<AircraftStandard>(EPoolName.AIRCRAFT_STANDARD))
            aircraft.ForceReturnToCarrier();
    }

    private void SpawnFleet(bool warpIn = false)
    {
        // 서버에서 받은 함대 정보가 없으면 스폰하지 않음
        if (DataManager.Instance.m_currentFleetInfo == null) return;

        SpawnMyFleetFromPreset(DataManager.Instance.m_currentFleetInfo, warpIn);
    }

    // 프리셋 함대 정보로 팀 소속 함대를 생성 — 플레이어/적 스폰 공통 로직
    // 카메라 타겟팅, Trigger_MyFleetSet 등 플레이어 전용 후처리는 호출부 책임
    // 체력/공격력 배율은 함선마다 shipSlot.healthMultiplier/attackMultiplier(서버가 ShipInfo에 실어 보낸 값)를 그대로 씀 —
    // 클라 로컬 ZoneConfig를 별도로 조회하지 않음(서버 응답을 신뢰 소스로 통일, 내 함대는 항상 기본값 1이라 영향 없음)
    public SpaceFleet SpawnFleetFromPreset(FleetInfo fleetInfo, ETeam team, EFleetSource source, Vector3 position, Quaternion rotation, string fleetName)
    {
        GameObject fleetObj = new GameObject(fleetName);
        fleetObj.transform.SetPositionAndRotation(position, rotation);
        SpaceFleet fleet = fleetObj.AddComponent<SpaceFleet>();
        fleet.m_fleetInfo = fleetInfo;
        fleet.m_team = team;
        fleet.m_fleetSource = source;
        fleet.m_fleetState = EUnitState.Idle;
        if (fleetInfo != null)
            fleet.m_currentFormationType = fleetInfo.formation;

        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;
        ShipStatFormulaSettings formula = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula;
        if (fleetInfo != null && fleetInfo.ships != null)
        {
            for (int shipIndex = 0; shipIndex < fleetInfo.ships.Count; shipIndex++)
            {
                ShipInfo shipSlot = fleetInfo.ships[shipIndex];
                ModuleData bodyModuleData = moduleTable.GetModuleDataFromTable(shipSlot.hullSubType);
                if (bodyModuleData == null)
                {
                    Debug.LogError($"ModuleData(body) not found: {shipSlot.hullSubType}");
                    continue;
                }

                // shipSlot.hulls는 서버에 저장된 "이 함선에 실제로 장착된 모듈 구성"(로드아웃) — 있으면(내 함대) 그걸 우선 쓰고,
                // 없으면(적 함대 등) 빈 로드아웃(기본 장착 구성)을 그대로 씀
                ModuleHullInfo actualModules = shipSlot.hulls != null && shipSlot.hulls.Count > 0 ? shipSlot.hulls[0] : null;
                ShipStatAllocation allocation = ShipStatAllocation.BuildFromModuleHullInfo(formula.maxModuleSlots, actualModules);

                // 보상카드 지속버프는 여기서 미리 곱해두지 않음 — 스폰되는 모듈들이 자기 자신의 RefreshRewardCardBuff()에서
                // 그 시점의 최신 배율을 직접 읽어 반영함(내 함대가 아니면 배율은 항상 1). 이러면 스폰 이후 카드를 더 골라도
                // ObjectManager.RefreshRewardCardBuffsOnMyFleet()가 같은 모듈들을 다시 갱신해주기만 하면 됨
                // 아키타입 모듈강화(M6)는 내 함대에만 적용 — 적 함대는 강화 없이 서브타입 원본 수치
                string upgradeArchetype = source == EFleetSource.fleet_source_player ? bodyModuleData.hullArchetype : null;
                ShipFinalStats finalStats = ShipStatCalculator.Calculate(allocation, formula, bodyModuleData, moduleTable, upgradeArchetype);
                ExplorationShipSpawnBridge.SpawnShip(fleet, bodyModuleData, finalStats, actualModules, shipIndex, shipSlot.isFront, shipSlot.healthMultiplier, shipSlot.attackMultiplier, shipSlot.id);
            }
        }
        // bWarp: false로 스폰된 함선들은 최종 대형 위치보다 뒤(-Z)에 멈춰있으므로, 연출 없이 즉시 최종 위치로 확정
        fleet.UpdateShipFormation(fleet.m_currentFormationType, bSmooth: false);
        fleet.SetFleetState(EUnitState.Idle);
        GetTeamFleets(team).Add(fleet);
        return fleet;
    }

    // warpIn=true면 Trigger_MyFleetSet으로 위치가 확정된 직후 그 자리로 워프인 연출(튜토리얼 종료 후 첫 함대 등장 등)
    // 함대 초기 위치는 탐사 그리드 시작 셀 좌표로 처음부터 확정 — 스폰 후 재배치하던 방식은 카메라가 원점을 스냅했다가
    // 실제 위치로 보간되는 부작용이 있어 제거함
    private void SpawnMyFleetFromPreset(FleetInfo fleetInfo, bool warpIn = false)
    {
        Vector3 startPos = GetInitialGridStartCellPosition();
        SpaceFleet myFleet = SpawnFleetFromPreset(fleetInfo, m_myTeam, EFleetSource.fleet_source_player, startPos, Quaternion.identity, "MyFleet");
        FinalizeMyFleetSpawn(myFleet, warpIn);
    }

    // 함대편성 UI(FleetComposition)에서 전방/후방만 토글할 때 호출 — 파괴/재생성 없이 기존 함선을 그 자리에서 스무스 이동
    public void SetMyFleetShipFront(int positionIndex, bool isFront)
    {
        SpaceFleet myFleet = GetMyFleet();
        if (myFleet == null) return;

        SpaceShip ship = myFleet.m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == positionIndex);
        if (ship == null) return;
        if (ship.m_shipInfo.isFront == isFront) return;

        ship.m_shipInfo.isFront = isFront;
        myFleet.UpdateShipFormation(myFleet.m_currentFormationType, bSmooth: true);
        RetargetEnemiesAiming(ship);
    }

    // isFront가 바뀐 ship을 이미 조준 중이던 적 함선들만 찾아 타겟을 재탐색시킴 —
    // ApplyRearLastRule은 타겟을 새로 뽑는 시점에만 적용되므로, 이미 락온된 적은 이렇게 찔러주지 않으면 후방으로 뺀 함선을 계속 공격함
    private void RetargetEnemiesAiming(SpaceShip ship)
    {
        List<SpaceFleet> enemyFleets = GetEnemyFleets();
        for (int i = 0; i < enemyFleets.Count; i++)
        {
            SpaceFleet enemyFleet = enemyFleets[i];
            if (enemyFleet == null) continue;

            for (int j = 0; j < enemyFleet.m_ships.Count; j++)
            {
                SpaceShip enemyShip = enemyFleet.m_ships[j];
                if (enemyShip != null && enemyShip.m_targetShip == ship)
                    enemyShip.StartFindingTargets();
            }
        }
    }

    // 함대편성 UI(FleetComposition)에서 슬롯 하나에 배치/교체하거나 장착 모듈만 바뀌었을 때 호출 — 그 슬롯의 함선만 파괴/재생성, 나머지 함선은 그대로 유지
    // modules를 생략하면(null) 빈 로드아웃(함체 교체 시 리셋된 값)을 쓰고, 넘기면(모듈 편집 후) 그 실제 장착 구성으로 스탯을 계산함
    public void ReplaceMyFleetShipAt(int positionIndex, string hullSubType, bool isFront, ModuleHullInfo modules = null)
    {
        SpaceFleet myFleet = GetMyFleet();
        if (myFleet == null) return;

        // 존 런 진행 중 함선 종류를 바꿔도 체력이 회복되면 안 되므로, 교체 전 이전 함선의 체력 비율을 미리 계산해둠(빈 슬롯이면 1f=만피)
        SpaceShip oldShip = myFleet.m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == positionIndex);
        bool isNewShip = oldShip == null; // 빈 슬롯에 처음 배치되는 경우 — 워프인 연출 대상(기존 함선의 함체 교체는 즉시 적용 유지)
        float previousHealthRatio = oldShip != null ? oldShip.GetHealthRatio() : 1f;
        if (oldShip != null)
            myFleet.RemoveShip(oldShip, refreshFormation: false, triggerDefeatEvents: false);

        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;
        ModuleData bodyModuleData = moduleTable.GetModuleDataFromTable(hullSubType);
        if (bodyModuleData == null)
        {
            Debug.LogError($"ModuleData(body) not found: {hullSubType}");
            return;
        }

        ShipStatFormulaSettings formula = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula;
        ShipStatAllocation allocation = ShipStatAllocation.BuildFromModuleHullInfo(formula.maxModuleSlots, modules);
        ShipFinalStats finalStats = ShipStatCalculator.Calculate(allocation, formula, bodyModuleData, moduleTable, bodyModuleData.hullArchetype);

        // 슬롯 정체성(id)은 함체가 바뀌어도 유지 — 비어있던 슬롯이었으면(oldShip null) 아직 서버에 확정된 id가 없으므로 0
        long preservedId = oldShip != null ? oldShip.m_shipInfo.id : 0;
        SpaceShip newShip = ExplorationShipSpawnBridge.SpawnShip(myFleet, bodyModuleData, finalStats, modules, positionIndex, isFront, id: preservedId, bWarp: isNewShip);
        // 존 런 진행 중이면 이전 함선의 손상 비율을 새 함선에 그대로 이전(회복 금지) — 평시 편성(런 없음)은 만피 유지
        if (newShip != null && IsExplorationRunActive() == true)
            newShip.ApplyHealthRatio(previousHealthRatio);

        // m_fleetInfo.ships도 함께 갱신 — 전멸 복구(SpaceFleet.RebuildFleet)가 화면에 보이는 m_ships가 아니라
        // 이 데이터로 함선을 재생성하므로, 편성 화면에서 바꾼 내용이 여기 반영 안 되면 전멸 후 예전 편성으로 되살아남
        if (newShip != null)
            UpdateFleetInfoShipAt(myFleet, positionIndex, newShip.m_shipInfo);

        // 신규 함선은 튜토리얼의 ShipArrivedAtFormation 조건(step6)이 도착을 기다릴 수 있게 등록
        if (isNewShip == true && newShip != null && TutorialManager.Instance != null && TutorialManager.Instance.IsPlaying == true)
            TutorialManager.Instance.SetPendingNewShip(newShip);

        // 신규 함선(isNewShip)은 이미 워프인 이동 중이므로 재스냅 대상에서 제외 — 나머지 함선만 대형 조정을 위해 부드럽게 슬라이드.
        // 기존 슬롯 함체 교체는 그대로 즉시 배치(bSmooth: false, 연출 없음)
        myFleet.UpdateShipFormation(myFleet.m_currentFormationType, bSmooth: isNewShip, excludeShip: isNewShip ? newShip : null);
    }

    // 내 함대에 배치된 함선이 하나라도 있는지 — 함대편성에서 전부 해제할 수 있어 PVP/탐사 진입 직전에 확인(함대 정보 기준, 전투 중 생존 여부와 무관)
    public bool HasAnyShipInMyFleet()
    {
        SpaceFleet myFleet = GetMyFleet();
        if (myFleet == null || myFleet.m_fleetInfo == null || myFleet.m_fleetInfo.ships == null) return false;
        return myFleet.m_fleetInfo.ships.Count > 0;
    }

    // 함대편성 UI의 Remove로 슬롯을 비울 때 호출 — 그 슬롯의 함선만 파괴. 다른 슬롯의 positionIndex는 건드리지 않음(자리 유지, 압축 없음)
    public void RemoveMyFleetShipAt(int positionIndex)
    {
        SpaceFleet myFleet = GetMyFleet();
        if (myFleet == null) return;

        SpaceShip removedShip = myFleet.m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == positionIndex);
        if (removedShip != null)
            myFleet.RemoveShip(removedShip, refreshFormation: false, triggerDefeatEvents: false);

        if (myFleet.m_fleetInfo != null && myFleet.m_fleetInfo.ships != null)
            myFleet.m_fleetInfo.ships.RemoveAll(s => s.positionIndex == positionIndex);

        myFleet.UpdateShipFormation(myFleet.m_currentFormationType, bSmooth: false);

        // 빠진 함선이 주던 함대 범위 패시브 효과를 남은 함선들에서 제거
        RefreshPassiveEffectsOnFleet(myFleet);
    }

    // myFleet.m_fleetInfo.ships에서 positionIndex가 일치하는 항목을 새 ShipInfo로 교체(없으면 추가) — ReplaceMyFleetShipAt 전용
    private void UpdateFleetInfoShipAt(SpaceFleet myFleet, int positionIndex, ShipInfo newShipInfo)
    {
        if (myFleet.m_fleetInfo == null) return;
        if (myFleet.m_fleetInfo.ships == null)
            myFleet.m_fleetInfo.ships = new List<ShipInfo>();

        int existingIndex = myFleet.m_fleetInfo.ships.FindIndex(s => s.positionIndex == positionIndex);
        if (existingIndex >= 0)
            myFleet.m_fleetInfo.ships[existingIndex] = newShipInfo;
        else
            myFleet.m_fleetInfo.ships.Add(newShipInfo);
    }

    // 현재 진행 중인 존 런이 있는지 — 존 런 중에는 함선 교체 시 체력 유지/자동회복 금지 등의 판정에 사용
    public bool IsExplorationRunActive()
    {
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        return commanderInfo != null && commanderInfo.explorationZoneNumber != 0;
    }

    // 함대 스폰/재구성 공통 후처리 — UI에 함대 교체를 알리고, 카메라 타겟과 초기 선택 함선을 설정
    private void FinalizeMyFleetSpawn(SpaceFleet myFleet, bool warpIn)
    {
        // 함대 스폰/교체를 UI 등 늦게 초기화되는 쪽에서도 알 수 있도록 알림 (존 초기 위치도 이 안에서 확정됨)
        EventManager.Trigger_MyFleetSet();

        // 카메라가 함대를 타겟으로 설정 — 최초 스폰 직후라 씬 배치 카메라 위치에서 Lerp로 튀는 걸 막기 위해 즉시 스냅
        CameraController.Instance.SetTargetOfCameraController(myFleet.transform, snapImmediately: true);

        // 기함을 초기 선택 상태로 설정 (줌 범위 적용 및 UI 초기화)
        SpaceShip flagship = myFleet.GetFlagship();
        if (flagship != null)
            EventManager.Trigger_SpaceShipSelected(flagship);

        if (warpIn == true)
            myFleet.StartFleetWarpIn();
    }

    // 워프 완료 시점에 호출 — 아군 함대를 존별 지정 위치로 텔레포트
    public void SetMyFleetPosition(Vector3 position, float rotationY = 0f)
    {
        SpaceFleet myFleet = GetMyFleet();
        if (myFleet == null) return;
        myFleet.transform.position = position;
        myFleet.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
    }

    

    // SpaceFleet.SpawnShipCoroutine에서 전멸 판정 후 호출
    public void OnZoneEnemyFleetDefeated(SpaceFleet fleet)
    {
        RemoveEnemyFleet(fleet);

        if (GetOpposingTeamFleets(m_myTeam).Count != 0) return;

        // 미스폰 웨이브가 남아있으면 그 중 가장 앞의 것을 term 타이머 기다리지 않고 즉시 스폰(전멸 시 즉시 다음 웨이브)
        if (HasPendingZoneWaves() == true)
        {
            for (int i = 0; i < m_zoneWaveSpawned.Length; i++)
            {
                if (m_zoneWaveSpawned[i] == true) continue;

                if (m_zoneWaveTimerCoroutines[i] != null)
                {
                    StopCoroutine(m_zoneWaveTimerCoroutines[i]);
                    m_zoneWaveTimerCoroutines[i] = null;
                }
                SpawnZoneWave(i);
                return;
            }
        }

        // 모든 웨이브 스폰 완료 + 적 전멸 → 클리어
        // ForceEndBattle(true)가 m_isBattleEnding 가드(내 함대도 거의 동시에 전멸해 ForceEndBattle(false)가
        // 먼저 확정된 경우 승리로 뒤집지 않음) 및 함대 상태 리셋/배속 복원/정리를 전부 처리
        EventManager.Trigger_AllEnemyFleetKilled();
        ForceEndBattle(true);
    }

    #region Prefabs ---------------------------------------------------------------
    [System.Serializable]
    public class PrefabPaths
    {
        [Header("Module Prefabs")]
        public string shipModulePrefabPath = "Prefabs/ShipModule/";

        [Header("Space Resource Prefabs")]
        public string asteroidPrefabPath = "Prefabs/SpaceResource/Asteroid";
        public string crystalPrefabPath = "Prefabs/SpaceResource/Crystal";
        
        // [Header("UI Prefabs")]
        // public string damageTextPrefabPath = "Prefabs/UI/DamageText";
        // public string healthBarPrefabPath = "Prefabs/UI/HealthBar";
        // public string minimapIconPrefabPath = "Prefabs/UI/MinimapIcon";
    }

    private PrefabPaths prefabPaths = new PrefabPaths();

    public GameObject LoadPrefab(string prefabSort, string typeName, string prefabName, int level = 0, string variant = "")
    {
        string resourcePath = GetPrefabPath(prefabSort, typeName, prefabName, level, variant);
        return ResourceManager.Instance.Load<GameObject>(resourcePath);
    }
    
    private string GetPrefabPath(string prefabSort, string typeName, string prefabName, int level, string variant)
    {
        string basePath = GetBasePrefabPath(prefabSort, typeName, prefabName);
        if (string.IsNullOrEmpty(basePath))
            return $"Prefabs/{prefabSort}/{typeName}/{prefabName}";

        string fullPath = basePath;
        
        if (prefabSort == "ShipModule")
        {
            fullPath += $"{prefabName}";
            if (level > 0) fullPath += $"_{level}";
        }
        
        if (string.IsNullOrEmpty(variant) == false) fullPath += $"_{variant}";
        
        
        return fullPath;
    }

    private string GetBasePrefabPath(string prefabSort, string typeName, string prefabName)
    {
        switch (prefabSort.ToLower())
        {
            // Module Prefabs
            case "shipmodule":
                {
                    if (string.IsNullOrEmpty(typeName) == false)
                        return prefabPaths.shipModulePrefabPath + $"{typeName}/";
                    else
                        return prefabPaths.shipModulePrefabPath;
                }
                
            // Space Resource Prefabs
            case "spaceresource":
                switch (prefabName.ToLower())
                {
                    case "asteroid": return prefabPaths.asteroidPrefabPath;
                    case "crystal": return prefabPaths.crystalPrefabPath;
                }
                break;

            // UI Prefabs
            // case "ui":
            //     switch (prefabName.ToLower())
            //     {
            //         case "damagetext": return prefabPaths.damageTextPrefabPath;
            //         case "healthbar": return prefabPaths.healthBarPrefabPath;
            //         case "minimapicon": return prefabPaths.minimapIconPrefabPath;
            //     }
            //     break;
        }

        return null; // Use user-defined path
    }
    
    
    // 프리팹 경로 생성 (에디터에서도 사용 가능)
    public static string GetShipModulePrefabPath(string moduleTypeName, string modulePrefabName)
    {
        return $"Prefabs/ShipModule/{moduleTypeName}/{modulePrefabName}";
    }

    public GameObject LoadShipModulePrefab(string moduleTypeName, string modulePrefabName)
    {
        string path = GetShipModulePrefabPath(moduleTypeName, modulePrefabName);
        return ResourceManager.Instance.Load<GameObject>(path);
    }

    public GameObject LoadModulePlaceholderPrefab()
    {
        return LoadPrefab("ShipModule", "", "Placeholder", 0);
    }
    
    
    // Convenience methods for frequently used prefabs
    public GameObject LoadSpaceResourcePrefab(string moduleTypeName, string modulePrefabName, string variant = "") 
    {
        return LoadPrefab("SpaceResource", moduleTypeName, modulePrefabName, 1, variant);
    }
    
    // public GameObject LoadUIPrefab(string uiType, string variant = "") 
    // {
    //     return LoadPrefab("UI", uiType, 1, variant);
    // }
    #endregion Prefabs ---------------------------------------------------------------
    

    public SpaceShip GetEnemy()
    {
        List<SpaceFleet> enemyTeamFleets = GetOpposingTeamFleets(m_myTeam);
        if (enemyTeamFleets.Count > 0)
        {
            // Clean dead fleets first
            for (int i = enemyTeamFleets.Count - 1; i >= 0; i--)
            {
                if (enemyTeamFleets[i] == null) { enemyTeamFleets.RemoveAt(i); continue; }
                if (enemyTeamFleets[i].IsFleetAlive() == false)
                {
                    DestroyFleet(enemyTeamFleets[i]);
                    enemyTeamFleets.RemoveAt(i);
                }
            }

            // Find random alive enemy ship
            foreach (SpaceFleet fleet in enemyTeamFleets)
            {
                if (fleet != null && fleet.IsValidCombatTarget() == true)
                {
                    SpaceShip enemyShip = fleet.GetRandomAliveShipWarpDone();
                    if (enemyShip != null)
                        return enemyShip;
                }
            }
        }
        return null;
    }

    private Vector3 RandomPosition()
    {
        return new Vector3(UnityEngine.Random.Range(-10.0f, 10.0f), 0, UnityEngine.Random.Range(-10.0f, 10.0f));
    }

    public Vector3 GetEnemySpawnPosition()
    {
        SpaceFleet myFleet = GetMyFleet();
        if (myFleet == null || myFleet.transform == null) return Vector3.zero;

        // 내 함선 중 z 사이즈가 가장 큰 것의 절반을 기준 오프셋으로 사용
        float maxZ = 0f;
        float maxHalfZ = 0f;
        foreach (SpaceShip ship in myFleet.m_ships)
        {
            if (ship == null) continue;
            float sizeZ = ship.CalculateShipBounds().size.z;
            if (sizeZ > maxZ)
            {
                maxZ = sizeZ;
                maxHalfZ = maxZ * 0.5f;
            }
        }

        Vector3 basePos = myFleet.transform.position;
        Vector3 spawnPosition = basePos + myFleet.transform.forward * (maxHalfZ + 50f);
        spawnPosition.y = 0f;

        return spawnPosition;
    }

}


