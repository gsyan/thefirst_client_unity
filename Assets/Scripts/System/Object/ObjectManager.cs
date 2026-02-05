//------------------------------------------------------------------------------
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

        ProjectileMissile projectileMissilePrefab = Resources.Load<ProjectileMissile>("Prefabs/Projectile/ProjectileMissile");
        if (projectileMissilePrefab != null)
            m_poolManager.CreatePool(EPoolName.PROJECTILE_MISSILE, projectileMissilePrefab, 1, 50);
        else
            Debug.LogError("ProjectileMisslePrefab not found at Resources/Prefabs/Projectile/ProjectileMissile");



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

        EffectBase effectShipExplosionPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectShipExplosion");
        if (effectShipExplosionPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_SHIP_EXPLOSION, effectShipExplosionPrefab, 3, 10);
        else
            Debug.LogError("effectShipExplosionPrefab not found at Resources/Prefabs/Effect/EffectShipExplosion");

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

    // Zone 전투 관련
    private ZoneConfig m_currentZoneConfig;
    private int m_currentWaveIndex;
    private int m_totalSpawnedEnemies;
    private int m_totalDestroyedEnemies;
    private System.Action<bool> m_onZoneBattleComplete;
    private Coroutine m_spawnCoroutine;

    // 초기화 순서가 이슈인 경우 이곳에서 순차적으로 진행
    private void Start()
    {
        DataManager.Instance.RestoreCurrentCharacterInfo();
        DataManager.Instance.RestoreCurrentFleetInfo();

        SpawnFleet();        

        NetworkManager.Instance.OnChangeScene();

        // UI 초기화
        UIManager.Instance.InitializeUIManager();

        // 튜토리얼 초기화
        //TutorialManager.Instance.ResetAllTutorials();
        // 튜토리얼 체크 및 시작, StartGameplay
        StartTutorialIfNeeded();
    }

    // 튜토리얼 시작 체크
    private void StartTutorialIfNeeded()
    {
        if (TutorialManager.Instance == null)
        {
            StartGameplay();
            return;
        }

        // UI 초기화 후 약간의 딜레이 후 시작
        StartCoroutine(StartTutorialDelayed());
    }

    private IEnumerator StartTutorialDelayed()
    {
        yield return new WaitForSeconds(1f);

        // 튜토리얼 시작 (완료 시 StartGameplay 호출)
        TutorialManager.Instance.StartTutorial("Tutorial_FirstPlay", (tutorialId) =>
        {
            // 스토리 튜토리얼 완료 → 자원 패널 표시
            UIManager.Instance.ShowPanel("UIPanelMineral");

            TutorialManager.Instance.StartTutorial("Tutorial_Mineral", (tutorialId) =>
            {
                // 자원 튜토리얼 완료 → 메인 패널 표시
                UIManager.Instance.ShowMainPanel();

                TutorialManager.Instance.StartTutorial("Tutorial_FleetButton", (tutorialId) =>
                {
                    StartGameplay();
                });
            });
        });
    }

    // 튜토리얼 완료 후 게임플레이 시작
    private void StartGameplay()
    {
        //StartCoroutine(SpawnMineral());
    }

    // ZoneConfig 기반 적 스폰 시작
    public void StartSpawnEnemies(ZoneConfig zoneConfig, System.Action<bool> onComplete)
    {
        if (zoneConfig == null || zoneConfig.waves.Count == 0)
        {
            onComplete?.Invoke(true);
            return;
        }

        // 기존 스폰 중이면 중지
        if (m_spawnCoroutine != null)
            StopCoroutine(m_spawnCoroutine);

        m_currentZoneConfig = zoneConfig;
        m_currentWaveIndex = 0;
        m_totalSpawnedEnemies = 0;
        m_totalDestroyedEnemies = 0;
        m_onZoneBattleComplete = onComplete;

        m_spawnCoroutine = StartCoroutine(SpawnWaves());
    }

    public void RemoveEnemyFleet(SpaceFleet fleet)
    {
        if (fleet == null) return;

        int shipCount = fleet.m_fleetInfo.ships.Count;
        m_enemyFleets.Remove(fleet);
        Destroy(fleet.gameObject);

        // Zone 전투 중이면 파괴된 적 카운트 증가
        if (m_currentZoneConfig != null)
        {
            m_totalDestroyedEnemies += shipCount;
            CheckZoneBattleComplete();
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
        m_totalSpawnedEnemies = 0;
        m_totalDestroyedEnemies = 0;
        m_onZoneBattleComplete = null;
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

    // 모든 활성 빔/미사일 풀로 반환
    public void CleanupAllProjectiles()
    {
        // 빔 제거
        ProjectileBeam[] beams = FindObjectsByType<ProjectileBeam>(FindObjectsSortMode.None);
        foreach (var beam in beams)
        {
            if (beam != null && beam.gameObject.activeSelf)
                m_poolManager.Return(EPoolName.PROJECTILE_BEAM, beam);
        }

        // 미사일 제거
        ProjectileMissile[] missiles = FindObjectsByType<ProjectileMissile>(FindObjectsSortMode.None);
        foreach (var missile in missiles)
        {
            if (missile != null && missile.gameObject.activeSelf)
                m_poolManager.Return(EPoolName.PROJECTILE_MISSILE, missile);
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

    private void CheckZoneBattleComplete()
    {
        // 모든 wave 스폰 완료 + 모든 적 처치 시
        if (m_currentWaveIndex >= m_currentZoneConfig.waves.Count &&
            m_totalDestroyedEnemies >= m_totalSpawnedEnemies &&
            m_enemyFleets.Count == 0)
        {
            var callback = m_onZoneBattleComplete;
            m_currentZoneConfig = null;
            m_onZoneBattleComplete = null;
            callback?.Invoke(true);
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

        // 임시로 배틀로 초기화, 최종적으로는 none으로 하고 중간에 함대 상태 바꾸는 기능이 있어야 함
        m_myFleet.SetFleetState(EFleetState.Battle);
    }

    

    // Wave 기반 적 스폰
    private IEnumerator SpawnWaves()
    {
        while (m_currentWaveIndex < m_currentZoneConfig.waves.Count)
        {
            WaveConfig wave = m_currentZoneConfig.waves[m_currentWaveIndex];

            // Wave 시작 전 대기
            if (wave.delayBeforeWave > 0)
                yield return new WaitForSeconds(wave.delayBeforeWave);

            // 해당 Wave의 적 함대 스폰
            SpawnEnemyFleetFromWave(wave);

            m_currentWaveIndex++;

            // 현재 Wave의 적이 모두 죽을 때까지 대기 (다음 Wave로 넘어가기 전)
            yield return new WaitUntil(() => m_enemyFleets.Count == 0);
        }

        // 모든 Wave 스폰 완료 후 전투 완료 체크
        CheckZoneBattleComplete();
    }

    // WaveConfig 기반으로 적 함대 생성
    private void SpawnEnemyFleetFromWave(WaveConfig wave)
    {
        if (m_myFleet == null || wave.enemyShips.Count == 0) return;

        Vector3 spawnPosition = GetEnemySpawnPosition();
        GameObject fleetObj = new GameObject($"EnemyFleet_Wave{m_currentWaveIndex}");
        fleetObj.transform.position = spawnPosition;

        Vector3 directionToPlayer = m_myFleet.transform.position - spawnPosition;
        directionToPlayer.y = 0;
        if (directionToPlayer != Vector3.zero)
            fleetObj.transform.rotation = Quaternion.LookRotation(directionToPlayer);

        SpaceFleet enemyFleet = fleetObj.AddComponent<SpaceFleet>();

        List<ShipInfo> enemyShips = new List<ShipInfo>();
        for (int i = 0; i < wave.enemyShips.Count; i++)
        {
            EnemyShipConfig shipConfig = wave.enemyShips[i];
            ShipInfo enemyShipInfo = CreateShipInfoFromConfig(shipConfig, i);
            enemyShips.Add(enemyShipInfo);
        }

        FleetInfo enemyFleetInfo = new FleetInfo
        {
            fleetName = $"EnemyFleet_Wave{m_currentWaveIndex}",
            formation = EFormationType.LinearHorizontal,
            ships = enemyShips
        };

        enemyFleet.InitializeSpaceFleet(enemyFleetInfo, true);
        m_totalSpawnedEnemies += wave.enemyShips.Count;

        StartCoroutine(AddEnemyFleetNextFrame(enemyFleet));
    }

    // EnemyShipConfig를 ShipInfo로 변환
    private ShipInfo CreateShipInfoFromConfig(EnemyShipConfig config, int positionIndex)
    {
        var bodyInfo = new ModuleBodyInfo
        {
            moduleType = EModuleType.Body,
            moduleSubType = config.bodySubType,
            moduleLevel = config.bodyLevel,
            bodyIndex = 0,
            engines = new List<ModuleInfo>(),
            beams = new List<ModuleInfo>(),
            missiles = new List<ModuleInfo>(),
            hangers = new List<ModuleInfo>()
        };

        // 슬롯 설정에 따라 모듈 추가
        foreach (var slot in config.moduleSlots)
        {
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
                case EModuleType.Engine:
                    bodyInfo.engines.Add(moduleInfo);
                    break;
                case EModuleType.Beam:
                    bodyInfo.beams.Add(moduleInfo);
                    break;
                case EModuleType.Missile:
                    bodyInfo.missiles.Add(moduleInfo);
                    break;
                case EModuleType.Hanger:
                    bodyInfo.hangers.Add(moduleInfo);
                    break;
            }
        }

        // 기본 엔진이 없으면 추가
        if (bodyInfo.engines.Count == 0)
        {
            bodyInfo.engines.Add(new ModuleInfo
            {
                moduleType = EModuleType.Engine,
                moduleSubType = EModuleSubType.Engine_Standard,
                moduleLevel = 1,
                bodyIndex = 0,
                slotIndex = 0
            });
        }

        return new ShipInfo
        {
            shipName = $"EnemyShip_{positionIndex}",
            positionIndex = positionIndex,
            bodies = new List<ModuleBodyInfo> { bodyInfo }
        };
    }

    private IEnumerator AddEnemyFleetNextFrame(SpaceFleet enemyFleet)
    {
        yield return null;
        m_enemyFleets.Add(enemyFleet);
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
    
    
    
    
    
    



    private IEnumerator SpawnMineral()
    {
        while (true)
        {
            yield return new WaitForSeconds(DataManager.Instance.m_dataTableConfig.gameSettings.m_explorationInterval);
            
            // Dynamic space resource prefab loading
            GameObject mineralPrefab = LoadSpaceResourcePrefab("", "Mineral");
                
            if (mineralPrefab != null)
            {
                GameObject tempObject = Instantiate(mineralPrefab, RandomPosition(), Quaternion.identity);
                SpaceMineral temp = tempObject.GetComponent<SpaceMineral>();
                if (temp != null && m_myFleet != null && m_myFleet.GetRandomAliveShip() != null)
                {
                    //temp.Initialize(m_myFleet.GetRandomAliveShip());
                }
                m_mineralList.Add(temp);
                Debug.Log($"Space resource created: {tempObject.name}");
            }
            else
            {
                Debug.LogWarning("Cannot find mineral prefab. Creating default mineral.");
                CreateDefaultMineral();
            }
        }
    }
    
    /// <summary>
    /// Create default mineral when prefab is missing
    /// </summary>
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
                if (fleet != null && fleet.IsFleetAlive() == true)
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

    private Vector3 GetEnemySpawnPosition()
    {
        // 내 함대의 위치와 방향 가져오기
        if (m_myFleet == null || m_myFleet.transform == null) return Vector3.zero;

        Vector3 fleetPosition = m_myFleet.transform.position;
        Vector3 fleetForward = m_myFleet.transform.forward;

        // 적 거리 설정
        float spawnDistance = UnityEngine.Random.Range(600.0f, 700.0f);
        
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

