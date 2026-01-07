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

    
    private bool m_isFirstEnemySpawned = false;
    private float m_lastEnemyDestroyTime = 0f;

    // 초기화 순서가 이슈인 경우 이곳에서 순차적으로 진행
    private void Start()
    {
        DataManager.Instance.RestoreCurrentCharacterData();
        DataManager.Instance.RestoreCurrentFleetInfo();

        SpawnFleet();

        // 카메라가 함대를 타겟으로 설정
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);

        StartCoroutine(SpawnEnemies());
        //StartCoroutine(SpawnMineral());

        UIManager.Instance.InitializeUIManager();
    }

    public void RemoveEnemyFleet(SpaceFleet fleet)
    {
        if (fleet == null) return;
        m_enemyFleets.Remove(fleet);
        m_lastEnemyDestroyTime = Time.time;
        Destroy(fleet.gameObject);
    }

    private void SpawnFleet()
    {
        GameObject fleetObj = new GameObject("MyFleet");
        m_myFleet = fleetObj.AddComponent<SpaceFleet>();
        m_myFleet.InitializeSpaceFleet(DataManager.Instance.m_currentFleetInfo);

        if (DataManager.Instance.m_currentCharacter != null)
            DataManager.Instance.m_currentCharacter.SetOwnedFleet(m_myFleet);

        // 임시로 배틀로 초기화, 최종적으로는 none으로 하고 중간에 함대 상태 바꾸는 기능이 있어야 함
        m_myFleet.SetFleetState(EFleetState.Battle);
    }

    

    private IEnumerator SpawnEnemies()
    {
        while (true)
        {
            if (!m_isFirstEnemySpawned)
            {
                yield return new WaitForSeconds(DataManager.Instance.m_dataTableConfig.gameSettings.enemyFleetSpawnInterval);
                if (m_enemyFleets.Count == 0)
                {
                    SpawnEnemyFleetFromData();
                    m_isFirstEnemySpawned = true;
                }
            }
            else
            {
                if (m_enemyFleets.Count == 0 && Time.time - m_lastEnemyDestroyTime >= DataManager.Instance.m_dataTableConfig.gameSettings.enemyFleetSpawnInterval)
                {
                    SpawnEnemyFleetFromData();
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }
    private void SpawnEnemyFleetFromData()
    {
        if (m_myFleet == null) return;

        int playerShipCount = m_myFleet.m_ships.Count;
        int playerAverageLevel = m_myFleet.GetAverageShipLevel();

        EnemyFleetPreset presetAsset = Resources.Load<EnemyFleetPreset>("DataTable/EnemyFleetPresets");
        if (presetAsset == null)
        {
            Debug.LogError("EnemyFleetPresets not found in Resources/DataTable");
            return;
        }

        var (enemyShipCount, enemyLevel) = presetAsset.GetMatchingPreset(playerShipCount, playerAverageLevel);

        Vector3 spawnPosition = GetEnemySpawnPosition();
        GameObject fleetObj = new GameObject($"EnemyFleet");
        fleetObj.transform.position = spawnPosition;
        Vector3 directionToPlayer = m_myFleet.transform.position - spawnPosition;
        directionToPlayer.y = 0;
        if (directionToPlayer != Vector3.zero)
            fleetObj.transform.rotation = Quaternion.LookRotation(directionToPlayer);
        SpaceFleet enemyFleet = fleetObj.AddComponent<SpaceFleet>();

        List<ShipInfo> enemyShips = new List<ShipInfo>();
        for (int i = 0; i < enemyShipCount; i++)
        {
            ShipInfo enemyShipInfo = new ShipInfo
            {
                shipName = $"EnemyShip_{i}",
                positionIndex = i,
                bodies = new[]
                {
                    new ModuleBodyInfo
                    {
                        moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Body, EModuleSubType.Body_Battle, EModuleStyle.None),
                        moduleLevel = enemyLevel,
                        bodyIndex = 0,
                        engines = new[]
                        {
                            new ModuleInfo { moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Engine, EModuleSubType.Engine_Standard, EModuleStyle.None), moduleLevel = 1, bodyIndex = 0, slotIndex = 0 }
                        },
                        weapons = new[]
                        {
                            new ModuleInfo { moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Weapon, EModuleSubType.Weapon_Beam, EModuleStyle.None), moduleLevel = 1, bodyIndex = 0, slotIndex = 0 }
                        }
                    }
                }
            };
            enemyShips.Add(enemyShipInfo);
        }

        FleetInfo enemyFleetInfo = new FleetInfo
        {
            fleetName = $"EnemyFleet",
            formation = EFormationType.LinearHorizontal,
            ships = enemyShips.ToArray()
        };

        enemyFleet.InitializeSpaceFleet(enemyFleetInfo, true);
        // 임시로 배틀로 초기화 none 으로
        //enemyFleet.SetFleetState(EFleetState.Battle);

        StartCoroutine(AddEnemyFleetNextFrame(enemyFleet));
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
    
    
    public GameObject LoadShipModulePrefab(string moduleTypeName, string modulePrefabName, int moduleLevel = 1)
    {
        return LoadPrefab("ShipModule", moduleTypeName, modulePrefabName, moduleLevel);
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
            yield return new WaitForSeconds(DataManager.Instance.m_dataTableConfig.gameSettings.explorationInterval);
            
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
            var newData = character.GetInfo();
            newData.mineral += mineralAmount;
            DataManager.Instance.SetCharacterData(newData);
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
        if (m_myFleet == null || m_myFleet.transform == null)
        {
            return RandomPosition(); // 내 함대가 없으면 기존 랜덤 위치
        }

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

