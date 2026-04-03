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
        m_poolManager.Initialize(this);

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





        ParticleSystem effectBeamMuzzlePrefab = Resources.Load<ParticleSystem>("Prefabs/Effect/EffectBeamMuzzle");
        if (effectBeamMuzzlePrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_BEAM_MUZZLE, effectBeamMuzzlePrefab, 5, 20);
        else
            Debug.LogError("EffectBeamMuzzlePrefab not found at Resources/Prefabs/Effect/EffectBeamMuzzle");




        EffectBase effectBeamHeadPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectBeamHead");
        if (effectBeamHeadPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_BEAM_HEAD, effectBeamHeadPrefab, 5, 20);
        else
            Debug.LogError("EffectBeamHeadPrefab not found at Resources/Prefabs/Effect/EffectBeamHead");

        EffectBase effectBeamHitPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectBeamHit");
        if (effectBeamHitPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_BEAM_HIT, effectBeamHitPrefab, 5, 20);
        else
            Debug.LogError("EffectHitBeamPrefab not found at Resources/Prefabs/Effect/EffectBeamHit");

        EffectBase effectMissileHitPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectMissileHit");
        if (effectMissileHitPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_MISSILE_HIT, effectMissileHitPrefab, 5, 20);
        else
            Debug.LogError("effectMissileHitPrefab not found at Resources/Prefabs/Effect/EffectMissileHit");

        // EffectBase effectShipExplosionPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectShipExplosion");
        // if (effectShipExplosionPrefab != null)
        //     m_poolManager.CreatePool(EPoolName.EFFECT_SHIP_EXPLOSION, effectShipExplosionPrefab, 3, 10);
        // else
        //     Debug.LogError("effectShipExplosionPrefab not found at Resources/Prefabs/Effect/EffectShipExplosion");

        EffectBase effectExplosionShipPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectExplosionShip");
        if (effectExplosionShipPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_EXPLOSION_SHIP, effectExplosionShipPrefab, 3, 10);
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

    [HideInInspector] public SpaceFleet m_myFleet;
    [HideInInspector] public List<SpaceFleet> m_enemyFleets = new List<SpaceFleet>();
    [HideInInspector] public List<SpaceMineral> m_mineralList = new List<SpaceMineral>();

    [Header("배경 데코 Sprite (Asteroid 1~10, Planet 1~7 순서대로 할당)")]
    public Sprite[] m_asteroidSprites;
    public Sprite[] m_planetSprites;
    private readonly List<GameObject> m_activeDecors = new();

    // Zone 전투 관련
    private ZoneConfig m_currentZoneConfig;
    private int m_currentWaveIndex;
    private System.Action<bool> m_onZoneBattleComplete;
    private Coroutine m_spawnCoroutine;

    // PvP 전투 관련
    private bool m_isPvpBattle;
    private System.Action<bool> m_onPvpBattleComplete;

    // 초기화 순서가 이슈인 경우 이곳에서 순차적으로 진행
    private void Start()
    {
        DataManager.Instance.RestoreCurrentCharacterInfo();
        DataManager.Instance.RestoreCurrentFleetInfo();

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
        if (m_myFleet != null) m_myFleet.SetFleetState(EFleetState.None);
        StopEnemySpawning();
        OrderAllAircraftReturn();
        CleanupAllProjectiles();
        RemoveAllEnemyFleets();
        ClearDecors();

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
            m_currentZoneConfig = null;
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
            // 스토리 튜토리얼 완료 → 자원 패널 표시
            UIManager.Instance.ShowPanel("UIPanelMineral");

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
        UIManager.Instance.ShowPanel("UIPanelMineral");
        UIManager.Instance.ShowMainPanel();
    }



    // ZoneConfig 기반 적 스폰 시작
    public void StartSpawnEnemies(ZoneConfig zoneConfig, System.Action<bool> onComplete)
    {
        if (zoneConfig == null || zoneConfig.enemyShipConfigs == null || zoneConfig.enemyShipConfigs.Count == 0)
        {
            onComplete?.Invoke(true);
            return;
        }

        // 기존 스폰 중이면 중지
        if (m_spawnCoroutine != null)
            StopCoroutine(m_spawnCoroutine);

        m_currentZoneConfig = zoneConfig;
        m_currentWaveIndex = 0;
        m_onZoneBattleComplete = onComplete;
        
        GameSpeedController.RestoreSpeed(); // 이전 전투 배속 복원
        if (m_myFleet != null) m_myFleet.SetFleetState(EFleetState.Battle);
        m_spawnCoroutine = StartCoroutine(SpawnWaves());
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
        // Move 상태로 초기화 — 워프 완료 후 StartEnemyFleetWarpIn 내부에서 Battle로 전환
        enemyFleet.InitializeSpaceFleet(opponentFleetInfo, EFleetSide.fleet_side_enemy, EFleetSource.fleet_source_player_remote, EFleetState.Move);
        enemyFleet.StartEnemyFleetWarpIn();
        m_myFleet.SetFleetState(EFleetState.Battle);

        m_enemyFleets.Add(enemyFleet);
    }

    // 존 배경 데코 생성 — 그룹 공유 행성 세트를 절대 좌표로 스폰
    public void StartSpawnDeco(SpaceDecorConfig[] decors)
    {
        ClearDecors();
        if (decors == null || decors.Length == 0) return;

        Camera cam = Camera.main;
        foreach (SpaceDecorConfig config in decors)
        {
            Sprite sprite = GetDecorSprite(config);
            if (sprite == null) continue;

            GameObject go = new GameObject($"Decor_{config.type}_{config.spriteIndex}");
            go.transform.localScale = Vector3.one * config.scale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            // 초기 위치·공전은 SpaceDecorItem.Initialize에서 랜덤 각도로 설정
            SpaceDecorItem item = go.AddComponent<SpaceDecorItem>();
            item.Initialize(UnityEngine.Random.Range(-5f, 5f), cam, config.orbitRadius, config.orbitPeriod);

            m_activeDecors.Add(go);
        }
    }

    private Sprite GetDecorSprite(SpaceDecorConfig config)
    {
        int idx = config.spriteIndex - 1;
        if (config.type == SpaceDecorType.Planet)
        {
            if (m_planetSprites == null || idx < 0 || idx >= m_planetSprites.Length) return null;
            return m_planetSprites[idx];
        }
        // Asteroid: 향후 3D 오브젝트로 처리 예정
        return null;
    }

    private void ClearDecors()
    {
        foreach (GameObject go in m_activeDecors)
        {
            if (go != null) Destroy(go);
        }
        m_activeDecors.Clear();
    }

    public void RemoveEnemyFleet(SpaceFleet fleet)
    {
        if (fleet == null) return;

        m_enemyFleets.Remove(fleet);
        Destroy(fleet.gameObject);

        // PvP 전투 중이면 적 함대 전멸 = 승리
        if (m_isPvpBattle && m_enemyFleets.Count == 0)
        {
            ForceEndBattle(true);
            return;
        }

        // Zone 전투 중이면 파괴된 적 카운트 증가 + 킬 보상 이벤트 발화 (클리어 체크는 코루틴의 WaitUntil이 담당)
        if (m_currentZoneConfig != null)
        {
            EventManager.Trigger_EnemyFleetKilled();
        }
    }

    // 적 스폰 코루틴 중지 및 Zone 전투 상태 초기화
    public void StopEnemySpawning()
    {
        if (m_spawnCoroutine != null)
        {
            StopCoroutine(m_spawnCoroutine);
            m_spawnCoroutine = null;
        }

        m_currentZoneConfig = null;
        m_currentWaveIndex = 0;
        //m_onZoneBattleComplete = null;
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
        GameObject fleetObj = new GameObject("MyFleet");
        m_myFleet = fleetObj.AddComponent<SpaceFleet>();
        m_myFleet.InitializeSpaceFleet(DataManager.Instance.m_currentFleetInfo);

        if (DataManager.Instance.m_currentCharacter != null)
            DataManager.Instance.m_currentCharacter.SetOwnedFleet(m_myFleet);

        // 카메라가 함대를 타겟으로 설정
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    // 워프 완료 시점에 호출 — 아군 함대를 존별 지정 위치로 텔레포트
    public void SetMyFleetPosition(Vector3 position)
    {
        if (m_myFleet == null) return;
        m_myFleet.transform.position = position;
    }

    

    // delayBeforeSpawn 후 웨이브 1개 스폰 — 클리어 판정은 서버가 담당
    private IEnumerator SpawnWaves()
    {
        if (m_currentZoneConfig.delayBeforeSpawn > 0)
            yield return new WaitForSeconds(m_currentZoneConfig.delayBeforeSpawn);

        var configs = m_currentZoneConfig.enemyShipConfigs;
        if (configs != null && configs.Count > 0)
        {
            // 웨이브 인덱스가 범위 초과 시 마지막 템플릿 재사용
            int idx = Mathf.Clamp(m_currentWaveIndex, 0, configs.Count - 1);
            SpawnEnemyFleetFromTemplate(configs[idx], configs[idx].shipCount);
        }
        EventManager.TriggerWaveStarted(m_currentWaveIndex + 1, m_currentZoneConfig.zoneClearCount);
    }

    // UITabExploration이 서버 응답 후 호출 — 다음 웨이브 스폰 (resetIndex: waveIndex 불일치 복구 시 0 리셋)
    public void SpawnNextWave(bool resetIndex = false)
    {
        if (m_currentZoneConfig == null) return;
        if (resetIndex == true)
            m_currentWaveIndex = 0;
        else
            m_currentWaveIndex++;
        m_spawnCoroutine = StartCoroutine(SpawnWaves());
    }

    // 웨이브 템플릿 1개를 template.shipCount만큼 복제해 적 함대 생성
    private void SpawnEnemyFleetFromTemplate(EnemyShipConfig template, int shipCount)
    {
        if (m_myFleet == null || template == null) return;
        if (shipCount <= 0) shipCount = template.shipCount;

        Vector3 spawnPosition = GetEnemySpawnPosition();
        GameObject fleetObj = new GameObject($"EnemyFleet_{m_currentWaveIndex}");
        fleetObj.transform.position = spawnPosition;

        Vector3 directionToPlayer = m_myFleet.transform.position - spawnPosition;
        directionToPlayer.y = 0;
        if (directionToPlayer != Vector3.zero)
            fleetObj.transform.rotation = Quaternion.LookRotation(directionToPlayer);

        SpaceFleet enemyFleet = fleetObj.AddComponent<SpaceFleet>();

        List<ShipInfo> enemyShips = new List<ShipInfo>();
        for (int i = 0; i < shipCount; i++)
        {
            enemyShips.Add(CreateShipInfoFromConfig(template, i));
        }

        FleetInfo enemyFleetInfo = new FleetInfo
        {
            fleetName = $"EnemyFleet_{m_currentWaveIndex}",
            formation = EFormationType.formation_type_linear_horizontal,
            ships = enemyShips
        };

        enemyFleet.InitializeZoneEnemyFleet(enemyFleetInfo, m_currentZoneConfig);
        m_enemyFleets.Add(enemyFleet);
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
    public static string GetShipModulePrefabPath(string moduleTypeName, string modulePrefabName, int moduleLevel)
    {
        // 현재 프리팹은 레벨 1만 존재
        // module level 1
        moduleLevel = 1;
        return $"Prefabs/ShipModule/{moduleTypeName}/{modulePrefabName}_{moduleLevel}";
    }

    public GameObject LoadShipModulePrefab(string moduleTypeName, string modulePrefabName, int moduleLevel = 1)
    {
        string path = GetShipModulePrefabPath(moduleTypeName, modulePrefabName, moduleLevel);
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
                if (m_enemyFleets[i] == null || m_enemyFleets[i].IsFleetAlive() == false)
                {
                    if (m_enemyFleets[i] != null)
                        Destroy(m_enemyFleets[i].gameObject);
                    m_enemyFleets.RemoveAt(i);
                }
            }

            // Find random alive enemy ship
            foreach (SpaceFleet fleet in m_enemyFleets)
            {
                if (fleet != null && fleet.IsFleetAlive() == true && fleet.m_fleetState == EFleetState.Battle)
                {
                    SpaceShip enemyShip = fleet.GetRandomAliveShip();
                    if (enemyShip != null)
                        return enemyShip;
                }
            }
        }
        return null;
    }

    public void SendExploration()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        SpaceMineral mineral = GetAvailableMineral();
        if (mineral == null) return;

        StartCoroutine(ExploreMineral(mineral));
    }

    private IEnumerator ExploreMineral(SpaceMineral mineral)
    {
        yield return new WaitForSeconds(5.0f);
        Int64 mineralAmount = UnityEngine.Random.Range(10, 50);

        var character = DataManager.Instance.m_currentCharacter;
        if (character != null)
        {
            character.UpdateMineral(character.m_characterInfo.mineral + mineralAmount);
            DataManager.Instance.SaveCharacterInfoToPlayerPrefs();
        }

        m_mineralList.Remove(mineral);
        Destroy(mineral.gameObject);
    }

    private SpaceMineral GetAvailableMineral()
    {
        foreach(var mineral in m_mineralList)
        {
            if (mineral.m_spaceMineralState != ESpaceMineralState.None) continue;
            mineral.m_spaceMineralState = ESpaceMineralState.Occupied;
            return mineral;
        }
        return null;
    }


    private Vector3 RandomPosition()
    {
        return new Vector3(UnityEngine.Random.Range(-10.0f, 10.0f), 0, UnityEngine.Random.Range(-10.0f, 10.0f));
    }

    public Vector3 GetEnemySpawnPosition()
    {
        // 내 함대의 위치와 방향 가져오기
        if (m_myFleet == null || m_myFleet.transform == null) return Vector3.zero;

        Vector3 fleetPosition = m_myFleet.transform.position;
        Vector3 fleetForward = m_myFleet.transform.forward;

        // 적 거리 설정
        float spawnDistance = 30.0f;
        
        // 최종 스폰 위치 계산
        Vector3 spawnPosition = fleetPosition +  fleetForward * spawnDistance;
        
        // Y 위치는 0으로 고정
        spawnPosition.y = 0;
        
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

