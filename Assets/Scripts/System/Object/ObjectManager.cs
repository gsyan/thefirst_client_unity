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

    protected override void OnInitialize()
    {
        DataManager.Instance.ApplyGameSettings();

        var console = DeveloperConsole.Instance;

        InitializePools();
    }

    private void InitializePools()
    {
        m_poolManager.InitializePoolManager(this);

        ProjectileBeam projectileBeamPrefab = Resources.Load<ProjectileBeam>("Prefabs/Projectile/ProjectileBeam");
        if (projectileBeamPrefab != null)
            m_poolManager.CreatePool(EPoolName.PROJECTILE_BEAM, projectileBeamPrefab, 1, 50);
        else
            Debug.LogError("ProjectileBeamPrefab not found at Resources/Prefabs/Projectile/ProjectileBeam");

        ProjectileBeamInstant projectileBeamInstantPrefab = Resources.Load<ProjectileBeamInstant>("Prefabs/Projectile/ProjectileBeamInstant");
        if (projectileBeamInstantPrefab != null)
            m_poolManager.CreatePool(EPoolName.PROJECTILE_BEAM_INSTANT, projectileBeamInstantPrefab, 1, 20);
        else
            Debug.LogWarning("ProjectileBeamInstant not found at Resources/Prefabs/Projectile/ProjectileBeamInstant");

        ProjectileMissile projectileMissileSmallPrefab = Resources.Load<ProjectileMissile>("Prefabs/Projectile/ProjectileMissileSmall");
        if (projectileMissileSmallPrefab != null)
            m_poolManager.CreatePool(EPoolName.PROJECTILE_MISSILE_SMALL, projectileMissileSmallPrefab, 1, 50);
        else
            Debug.LogError("ProjectileMissileSmall not found at Resources/Prefabs/Projectile/ProjectileMissileSmall");

        ProjectileMissile projectileMissileMediumPrefab = Resources.Load<ProjectileMissile>("Prefabs/Projectile/ProjectileMissileMedium");
        if (projectileMissileMediumPrefab != null)
            m_poolManager.CreatePool(EPoolName.PROJECTILE_MISSILE_MEDIUM, projectileMissileMediumPrefab, 1, 50);
        else
            Debug.LogError("ProjectileMissileMedium not found at Resources/Prefabs/Projectile/ProjectileMissileMedium");

        ProjectileMissile projectileMissileLargePrefab = Resources.Load<ProjectileMissile>("Prefabs/Projectile/ProjectileMissileLarge");
        if (projectileMissileLargePrefab != null)
            m_poolManager.CreatePool(EPoolName.PROJECTILE_MISSILE_LARGE, projectileMissileLargePrefab, 1, 50);
        else
            Debug.LogError("ProjectileMissileLarge not found at Resources/Prefabs/Projectile/ProjectileMissileLarge");


        EffectBase effectPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectBeamHead");
        if (effectPrefab == null) Debug.LogError("Not found at Resources/Prefabs/Effect/EffectBeamHead");
        m_poolManager.CreatePool(EPoolName.EFFECT_BEAM_HEAD, effectPrefab, 5, 20);

        effectPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectBeamHit");
        if (effectPrefab == null) Debug.LogError("Not found at Resources/Prefabs/Effect/EffectBeamHit");
        m_poolManager.CreatePool(EPoolName.EFFECT_BEAM_HIT, effectPrefab, 5, 20);

        effectPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectBeamMuzzle");
        if (effectPrefab == null) Debug.LogError("Not found at Resources/Prefabs/Effect/EffectBeamMuzzle");
        m_poolManager.CreatePool(EPoolName.EFFECT_BEAM_MUZZLE, effectPrefab, 5, 20);
        
        EffectBase effectExplosionShipPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectExplosionShip");
        if (effectExplosionShipPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_EXPLOSION_SHIP, effectExplosionShipPrefab, 3, 10);
        else
            Debug.LogError("EffectExplosionShip not found at Resources/Prefabs/Effect/EffectExplosionShip");

        EffectBase effectExplosionMissileSmallPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectExplosionMissileSmall");
        if (effectExplosionMissileSmallPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_EXPLOSION_MISSILE_SMALL, effectExplosionMissileSmallPrefab, 3, 10);
        else
            Debug.LogError("EffectExplosionShip not found at Resources/Prefabs/Effect/EffectExplosionShip");


        EffectBase effectWarpSpeedLinesPrefab = Resources.Load<EffectBase>("Prefabs/Effect/WarpSpeedLines");
        if (effectWarpSpeedLinesPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_WARP_SPEEDLINES, effectWarpSpeedLinesPrefab, 5, 20);
        else
            Debug.LogError("WarpSpeedLines not found at Resources/Prefabs/Effect/WarpSpeedLines");





        AircraftStandard aircraftStandardPrefab = Resources.Load<AircraftStandard>("Prefabs/Aircraft/AircraftStandard");
        if (aircraftStandardPrefab != null)
            m_poolManager.CreatePool(EPoolName.AIRCRAFT_STANDARD, aircraftStandardPrefab, 1, 30);
        else
            Debug.LogError("AircraftStandardPrefab not found at Resources/Prefabs/Aircraft/AircraftStandard");

    }
    #endregion

    private CelestialBodySpawner m_celestialBodySpawner;

    [HideInInspector] public SpaceFleet m_myFleet;
    [HideInInspector] public List<SpaceFleet> m_enemyFleets = new List<SpaceFleet>();
    [HideInInspector] public List<SpaceMineral> m_mineralList = new List<SpaceMineral>();

    // Zone 전투 관련
    private ZoneStageConfig m_currentZoneStageConfig;
    private System.Action<bool> m_onZoneBattleComplete;
    private Coroutine m_spawnCoroutine;
    private Queue<EnemyShipConfig> m_shipSpawnQueue = new Queue<EnemyShipConfig>();
    private bool[] m_occupiedSlots;
    private SpaceFleet m_activeZoneEnemyFleet;
    private bool m_queueExhausted = false;

    // PvP 전투 관련
    private bool m_isPvpBattle;
    private System.Action<bool> m_onPvpBattleComplete;

    // 초기화 순서가 이슈인 경우 이곳에서 순차적으로 진행
    private void Start()
    {
        m_celestialBodySpawner = GetComponent<CelestialBodySpawner>();
        if (m_celestialBodySpawner != null)
            m_celestialBodySpawner.SpawnAll();

        SpawnFleet();

        NetworkManager.Instance.OnChangeScene();

        // UI 초기화
        UIManager.Instance.InitializeUIManager();

        // 플레이어 함대 전멸 이벤트 구독
        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetDestroyed);

        // 광고 초기화 (존 입장 전 미리 로드)
        AdManager.Instance.ToString();

        // 튜토리얼 초기화
        //TutorialManager.Instance.ResetAllTutorials();
        // 튜토리얼 체크 및 시작, StartGameplay
        StartTutorialIfNeeded();
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

    // 전투 강제 종료 (전멸/퇴각 공통)
    public void ForceEndBattle(bool isVictory)
    {
        GameSpeedController.Reset(); // timeScale 및 오디오 피치 복원
        if (m_myFleet != null)
            m_myFleet.SetFleetState(EUnitState.Idle);
        StopEnemySpawning();
        OrderAllAircraftReturn();
        CleanupAllProjectiles();
        RemoveAllEnemyFleets();

        if (m_isPvpBattle)
        {
            m_isPvpBattle = false;
            var pvpCallback = m_onPvpBattleComplete;
            m_onPvpBattleComplete = null;
            pvpCallback?.Invoke(isVictory);
        }
        else
        {
            var callback = m_onZoneBattleComplete;
            m_onZoneBattleComplete = null;
            m_currentZoneStageConfig = null;
            callback?.Invoke(isVictory);
        }
    }

    // 튜토리얼 시작 체크
    private void StartTutorialIfNeeded()
    {
        if (TutorialManager.Instance == null)
        {
            StartGameplay();
            return;
        }

        bool bTest = true;
        if (bTest == true)
            PassTutorial();
        else
            // UI 초기화 후 약간의 딜레이 후 시작
            StartCoroutine(StartTutorial());
    }

    private IEnumerator StartTutorial()
    {
        // 서버에서 튜토리얼 진행도 로드 대기
        var loadTask = TutorialManager.Instance.LoadProgressFromServerAsync();
        while (!loadTask.IsCompleted)
        {
            yield return null;
        }

        // 튜토리얼 시작 (완료 시 StartGameplay 호출)
        TutorialManager.Instance.StartTutorial("Tutorial_FirstPlay", (tutorialId) =>
        {
            // 스토리 튜토리얼 완료
            TutorialManager.Instance.StartTutorial("Tutorial_Mineral", (tutorialId) =>
            {
                // 자원 튜토리얼 완료 → 메인 패널 표시
                UIManager.Instance.ShowMainPanel();

                TutorialManager.Instance.StartTutorial("Tutorial_Fleet", (tutorialId) =>
                {
                    StartGameplay();
                });
            });
        });
    }

    // 튜토리얼 완료 후 게임플레이 시작
    private void StartGameplay()
    {
        
    }

    private void PassTutorial()
    {
        ShowGamePanels();
    }

    private void ShowGamePanels()
    {
        NetworkManager.Instance.StartHeartbeat();
        UIManager.Instance.ShowMainPanel();
    }



    // ZoneConfig 기반 적 함선 순차 스폰 — 슬롯 단위 관리, 큐 소진 + 전멸 시 클리어
    public void StartSpawnEnemies(ZoneStageConfig zoneStageConfig, System.Action<bool> onComplete)
    {
        if (zoneStageConfig == null || zoneStageConfig.enemyShipConfigs == null || zoneStageConfig.enemyShipConfigs.Count == 0)
        {
            onComplete?.Invoke(true);
            return;
        }

        if (m_spawnCoroutine != null)
            StopCoroutine(m_spawnCoroutine);

        m_currentZoneStageConfig = zoneStageConfig;
        m_onZoneBattleComplete = onComplete;

        m_shipSpawnQueue.Clear();
        foreach (var cfg in zoneStageConfig.enemyShipConfigs)
            m_shipSpawnQueue.Enqueue(cfg);

        int slotCount = Mathf.Max(1, zoneStageConfig.maxConcurrentEnemyShips);
        m_occupiedSlots = new bool[slotCount];
        m_queueExhausted = false;

        m_activeZoneEnemyFleet = CreateEnemyFleetShell();
        m_enemyFleets.Add(m_activeZoneEnemyFleet);

        GameSpeedController.RestoreSpeed();
        if (m_myFleet != null) m_myFleet.SetFleetState(EUnitState.Battle);
        m_spawnCoroutine = StartCoroutine(SpawnEnemyFleetCoroutine());
    }

    // PvP 전투 시작 - 서버에서 받은 상대 FleetInfo로 적 함대 생성
    public void StartPvpBattle(FleetInfo opponentFleetInfo, System.Action<bool> onComplete)
    {
        if (opponentFleetInfo == null || m_myFleet == null)
        {
            onComplete?.Invoke(false);
            return;
        }
        GameSpeedController.RestoreSpeed(); // 이전 전투 배속 복원

        m_isPvpBattle = true;
        m_onPvpBattleComplete = onComplete;

        Vector3 spawnPosition = GetEnemySpawnPosition();
        GameObject fleetObj = new GameObject("PvpEnemyFleet");
        fleetObj.transform.position = spawnPosition;

        Vector3 directionToPlayer = m_myFleet.transform.position - spawnPosition;
        directionToPlayer.y = 0;
        if (directionToPlayer != Vector3.zero)
            fleetObj.transform.rotation = Quaternion.LookRotation(directionToPlayer);

        SpaceFleet enemyFleet = fleetObj.AddComponent<SpaceFleet>();
        enemyFleet.InitializeSpaceFleet(opponentFleetInfo, EFleetSide.fleet_side_enemy, EFleetSource.fleet_source_player_remote, EUnitState.Move);
        enemyFleet.StartFleetWarpIn();
        m_myFleet.SetFleetState(EUnitState.Battle);

        m_enemyFleets.Add(enemyFleet);
    }

    // 이벤트에 의한 함대 파괴 — 정식 파괴는 이곳, cleanup loop(GetEnemy)는 안전망
    public void RemoveEnemyFleet(SpaceFleet fleet)
    {
        if (fleet == null) return;

        m_enemyFleets.Remove(fleet);
        Destroy(fleet.gameObject);

        if (m_isPvpBattle && m_enemyFleets.Count == 0)
            ForceEndBattle(true);
    }

    // 적 스폰 코루틴 중지 및 Zone 전투 상태 초기화
    public void StopEnemySpawning()
    {
        if (m_spawnCoroutine != null)
        {
            StopCoroutine(m_spawnCoroutine);
            m_spawnCoroutine = null;
        }

        m_currentZoneStageConfig = null;
        m_shipSpawnQueue.Clear();
        m_occupiedSlots = null;
        m_activeZoneEnemyFleet = null;
        m_queueExhausted = false;
    }

    // 모든 적 함대 제거
    public void RemoveAllEnemyFleets()
    {
        for (int i = m_enemyFleets.Count - 1; i >= 0; i--)
        {
            if (m_enemyFleets[i] != null)
                Destroy(m_enemyFleets[i].gameObject);
        }
        m_enemyFleets.Clear();
        m_activeZoneEnemyFleet = null;
    }

    // 모든 활성 빔/미사일: 코루틴/이펙트 정리 후 풀 반환
    public void CleanupAllProjectiles()
    {
        ProjectileBeam[] beams = FindObjectsByType<ProjectileBeam>(FindObjectsSortMode.None);
        foreach (var beam in beams)
        {
            if (beam != null && beam.gameObject.activeSelf)
                beam.ReturnToPool();
        }

        ProjectileMissile[] missiles = FindObjectsByType<ProjectileMissile>(FindObjectsSortMode.None);
        foreach (var missile in missiles)
        {
            if (missile != null && missile.gameObject.activeSelf)
                missile.ReturnToPool(showHitEffect: false);
        }
    }

    // 모든 함재기에게 귀환 명령
    public void OrderAllAircraftReturn()
    {
        AircraftBase[] aircrafts = FindObjectsByType<AircraftBase>(FindObjectsSortMode.None);
        foreach (var aircraft in aircrafts)
        {
            if (aircraft != null && aircraft.gameObject.activeSelf)
                aircraft.ForceReturnToCarrier();
        }
    }

    private void SpawnFleet()
    {
        // 서버에서 받은 함대 정보가 없으면 스폰하지 않음
        if (DataManager.Instance.m_currentFleetInfo == null) return;

        GameObject fleetObj = new GameObject("MyFleet");
        m_myFleet = fleetObj.AddComponent<SpaceFleet>();
        m_myFleet.InitializeSpaceFleet(DataManager.Instance.m_currentFleetInfo);

        if (DataManager.Instance.m_currentCharacter != null)
            DataManager.Instance.m_currentCharacter.SetOwnedFleet(m_myFleet);

        // 카메라가 함대를 타겟으로 설정
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);

        // 기함을 초기 선택 상태로 설정 (줌 범위 적용 및 UI 초기화)
        SpaceShip flagship = m_myFleet.GetFlagship();
        if (flagship != null)
            EventManager.Trigger_SpaceShipSelected(flagship);
    }

    // 워프 완료 시점에 호출 — 아군 함대를 존별 지정 위치로 텔레포트
    public void SetMyFleetPosition(Vector3 position, float rotationY = 0f)
    {
        if (m_myFleet == null) return;
        m_myFleet.transform.position = position;
        m_myFleet.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
    }

    

    // 빈 적 함대 오브젝트 생성 — 이후 함선을 1척씩 추가할 껍데기
    private SpaceFleet CreateEnemyFleetShell()
    {
        Vector3 spawnPosition = GetEnemySpawnPosition();
        GameObject fleetObj = new GameObject("EnemyFleet");
        fleetObj.transform.position = spawnPosition;

        if (m_myFleet != null)
        {
            Vector3 dir = m_myFleet.transform.position - spawnPosition;
            dir.y = 0;
            if (dir != Vector3.zero)
                fleetObj.transform.rotation = Quaternion.LookRotation(dir);
        }

        SpaceFleet fleet = fleetObj.AddComponent<SpaceFleet>();
        fleet.InitializeAsZoneEnemyFleetShell("EnemyFleet", EFormationType.formation_type_linear_horizontal);
        return fleet;
    }

    // delayBeforeSpawn 대기 후 큐 순서대로 1척씩 스폰 — 슬롯이 빌 때까지 대기
    private IEnumerator SpawnEnemyFleetCoroutine()
    {
        if (m_currentZoneStageConfig.delayBeforeSpawn > 0)
            yield return new WaitForSeconds(m_currentZoneStageConfig.delayBeforeSpawn);

        while (m_shipSpawnQueue.Count > 0)
        {
            EnemyShipConfig next = m_shipSpawnQueue.Peek();
            int slot = FindFreeSlot(next.isFlagShip);
            if (slot == -1)
            {
                yield return new WaitUntil(() => m_shipSpawnQueue.Count > 0 && FindFreeSlot(m_shipSpawnQueue.Peek().isFlagShip) != -1);
                continue;
            }

            m_shipSpawnQueue.Dequeue();
            m_occupiedSlots[slot] = true;
            SpawnSingleEnemyShip(next, slot);

            if (m_shipSpawnQueue.Count > 0)
                yield return new WaitForSeconds(m_currentZoneStageConfig.shipSpawnInterval);
        }

        m_queueExhausted = true;
        m_spawnCoroutine = null;
        CheckZoneClear();
    }

    // 함선 1척 생성 후 함대에 합류 (워프인 포함)
    private void SpawnSingleEnemyShip(EnemyShipConfig config, int slotIndex)
    {
        if (m_activeZoneEnemyFleet == null) return;

        ShipInfo shipInfo = CreateShipInfoFromConfig(config, slotIndex);
        GameObject shipGo = new GameObject(shipInfo.shipName);
        SpaceShip spaceShip = shipGo.AddComponent<SpaceShip>();
        spaceShip.m_bodyMultiplier    = config.bodyMultiplier;
        spaceShip.m_beamMultiplier    = config.beamMultiplier;
        spaceShip.m_missileMultiplier = config.missileMultiplier;
        spaceShip.m_hangerMultiplier  = config.hangerMultiplier;
        spaceShip.InitializeSpaceShip(m_activeZoneEnemyFleet, shipInfo);
        m_activeZoneEnemyFleet.AddShip(spaceShip, bWarp: true);
    }

    // isFlagShip=true → 슬롯 0 전용, isFlagShip=false → 슬롯 0 제외
    private int FindFreeSlot(bool isFlagShip)
    {
        if (m_occupiedSlots == null) return -1;
        if (isFlagShip)
        {
            return (m_occupiedSlots.Length > 0 && m_occupiedSlots[0] == false) ? 0 : -1;
        }
        for (int i = 1; i < m_occupiedSlots.Length; i++)
        {
            if (m_occupiedSlots[i] == false) return i;
        }
        return -1;
    }

    // SpaceFleet.RemoveShip에서 zone enemy 함선 파괴 시 호출
    public void OnZoneEnemyShipSlotFreed(int slotIndex)
    {
        if (m_occupiedSlots != null && slotIndex >= 0 && slotIndex < m_occupiedSlots.Length)
            m_occupiedSlots[slotIndex] = false;

        CheckZoneClear();
    }

    // 큐 소진 + 활성 함선 없음 → 클리어
    private void CheckZoneClear()
    {
        if (m_queueExhausted == false) return;
        if (m_activeZoneEnemyFleet != null && m_activeZoneEnemyFleet.IsFleetAlive()) return;

        SpaceFleet fleet = m_activeZoneEnemyFleet;
        m_activeZoneEnemyFleet = null;
        RemoveEnemyFleet(fleet); // 파괴는 RemoveEnemyFleet 단일 지점
        EventManager.Trigger_EnemyFleetKilled();
    }

    // EnemyShipConfig를 ShipInfo로 변환
    private ShipInfo CreateShipInfoFromConfig(EnemyShipConfig config, int positionIndex)
    {
        var bodyInfo = new ModuleBodyInfo
        {
            moduleType = EModuleType.body,
            moduleSubType = config.bodySubType,
            moduleLevel = config.bodyLevel,
            bodyIndex = 0,
            beams = new List<ModuleInfo>(),
            missiles = new List<ModuleInfo>(),
            hangers = new List<ModuleInfo>()
        };

        // 슬롯 설정에 따라 모듈 추가 (none = 빈 슬롯, 건너뜀)
        foreach (var slot in config.moduleSlots)
        {
            if (slot.moduleSubType == EModuleSubType.none) continue;

            var moduleInfo = new ModuleInfo
            {
                moduleType = slot.slotType,
                moduleSubType = slot.moduleSubType,
                moduleLevel = slot.moduleLevel,
                bodyIndex = 0,
                slotIndex = slot.slotIndex
            };

            switch (slot.slotType)
            {
                case EModuleType.beam:
                    bodyInfo.beams.Add(moduleInfo);
                    break;
                case EModuleType.missile:
                    bodyInfo.missiles.Add(moduleInfo);
                    break;
                case EModuleType.hanger:
                    bodyInfo.hangers.Add(moduleInfo);
                    break;
            }
        }

        return new ShipInfo
        {
            shipName = $"EnemyShip_{positionIndex}",
            positionIndex = positionIndex,
            bodies = new List<ModuleBodyInfo> { bodyInfo }
        };
    }

    #region Prefabs ---------------------------------------------------------------
    [System.Serializable]
    public class PrefabPaths
    {
        [Header("Module Prefabs")]
        public string shipModulePrefabPath = "Prefabs/ShipModule/";
        
        [Header("Space Resource Prefabs")]
        public string mineralPrefabPath = "Prefabs/SpaceResource/Mineral";
        public string asteroidPrefabPath = "Prefabs/SpaceResource/Asteroid";
        public string crystalPrefabPath = "Prefabs/SpaceResource/Crystal";
        
        // [Header("UI Prefabs")]
        // public string damageTextPrefabPath = "Prefabs/UI/DamageText";
        // public string healthBarPrefabPath = "Prefabs/UI/HealthBar";
        // public string minimapIconPrefabPath = "Prefabs/UI/MinimapIcon";
    }

    private PrefabPaths prefabPaths = new PrefabPaths();
    private Dictionary<string, GameObject> cachedPrefabs = new Dictionary<string, GameObject>();

    public GameObject LoadPrefab(string prefabSort, string typeName, string prefabName, int level = 0, string variant = "")
    {
        string cacheKey = CreateCacheKey(prefabSort, typeName, prefabName, level, variant);

        // Return immediately if in cache
        if (cachedPrefabs.ContainsKey(cacheKey))
            return cachedPrefabs[cacheKey];

        string resourcePath = GetPrefabPath(prefabSort, typeName, prefabName, level, variant);        
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
            return null;

        // Save to cache
        if (prefab != null)
            cachedPrefabs[cacheKey] = prefab;

        return prefab;
    }
    
    private string CreateCacheKey(string prefabSort, string typeName, string prefabName, int level, string variant)
    {
        string key = $"{prefabSort}_{prefabName}";
        if (string.IsNullOrEmpty(typeName) == false) key += $"_{typeName}";
        if (level > 0) key += $"_{level}";
        if (string.IsNullOrEmpty(variant) == false) key += $"_{variant}";
        return key;
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
                    case "mineral": return prefabPaths.mineralPrefabPath;
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
        return Resources.Load<GameObject>(path);
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
    
    
    
    
    
    



    
    
    
    // Create default mineral when prefab is missing
    private void CreateDefaultMineral()
    {
        GameObject defaultMineral = new GameObject("DefaultMineral");
        defaultMineral.transform.position = RandomPosition();
        defaultMineral.transform.rotation = Quaternion.identity;
        
        SpaceMineral mineral = defaultMineral.AddComponent<SpaceMineral>();
        m_mineralList.Add(mineral);
    }

    public SpaceShip GetEnemy()
    {
        if (m_enemyFleets.Count > 0)
        {
            // Clean dead fleets first
            for (int i = m_enemyFleets.Count - 1; i >= 0; i--)
            {
                if (m_enemyFleets[i] == null) { m_enemyFleets.RemoveAt(i); continue; }
                bool fleetAlive = m_enemyFleets[i].IsFleetAlive();
                bool hasQueuedShips = m_enemyFleets[i].IsZoneEnemy && m_shipSpawnQueue.Count > 0;
                if (fleetAlive == false && hasQueuedShips == false)
                {
                    Destroy(m_enemyFleets[i].gameObject);
                    m_enemyFleets.RemoveAt(i);
                }
            }

            // Find random alive enemy ship
            foreach (SpaceFleet fleet in m_enemyFleets)
            {
                if (fleet != null && fleet.IsFleetAlive() == true && fleet.m_fleetState == EUnitState.Battle)
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
        if (m_myFleet == null || m_myFleet.transform == null) return Vector3.zero;

        // 내 함선 중 z 사이즈가 가장 큰 것의 절반을 기준 오프셋으로 사용
        float maxZ = 0f;
        float maxHalfZ = 0f;
        foreach (SpaceShip ship in m_myFleet.m_ships)
        {
            if (ship == null) continue;
            float sizeZ = ship.CalculateShipBounds().size.z;
            if (sizeZ > maxZ)
            {
                maxZ = sizeZ;  
                maxHalfZ = maxZ * 0.5f;
            } 
        }

        Vector3 basePos = m_myFleet.transform.position;
        Vector3 spawnPosition = basePos + m_myFleet.transform.forward * (maxHalfZ + maxZ * 10f);
        spawnPosition.y = 0f;

        return spawnPosition;
    }

    private Vector3 GetMineralSpawnPosition()
    {
        // 내 함대의 위치와 방향 가져오기
        if (m_myFleet == null || m_myFleet.transform == null) return RandomPosition(); // 내 함대가 없으면 기존 랜덤 위치

        Vector3 fleetPosition = m_myFleet.transform.position;
        Vector3 fleetForward = m_myFleet.transform.forward;

        // 시야각 45도 (반각 22.5도)를 라디안으로 변환
        float halfAngle = 22.5f * Mathf.Deg2Rad;
        
        // 적절한 거리 설정 (400~500 유닛)
        float spawnDistance = UnityEngine.Random.Range(400.0f, 500.0f);
        
        // -22.5도 ~ +22.5도 사이의 랜덤 각도
        float randomAngle = UnityEngine.Random.Range(-halfAngle, halfAngle);
        
        // 함대 forward 벡터를 기준으로 회전
        Vector3 spawnDirection = Quaternion.AngleAxis(randomAngle * Mathf.Rad2Deg, Vector3.up) * fleetForward;
        
        // 최종 스폰 위치 계산
        Vector3 spawnPosition = fleetPosition + spawnDirection * spawnDistance;
        
        // Y 위치는 0으로 고정
        spawnPosition.y = 0;
        
        return spawnPosition;
    }

}

