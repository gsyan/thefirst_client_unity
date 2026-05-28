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

        EffectBase effectFireOnShipPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectFireOnShip");
        if (effectFireOnShipPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_FIRE_ON_SHIP, effectFireOnShipPrefab, 5, 15);
        else
            Debug.LogError("EffectFireOnShip not found at Resources/Prefabs/Effect/EffectFireOnShip");

        EffectBase effectScorchMarkPrefab = Resources.Load<EffectBase>("Prefabs/Effect/EffectScorchMark");
        if (effectScorchMarkPrefab != null)
            m_poolManager.CreatePool(EPoolName.EFFECT_SCORCH_MARK, effectScorchMarkPrefab, 10, 30);
        else
            Debug.LogError("EffectScorchMark not found at Resources/Prefabs/Effect/EffectScorchMark");





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

    // Zone 전투 관련
    private System.Action<bool> m_onZoneBattleComplete;

    // PvP 전투 관련
    private bool m_isPvpBattle;
    private System.Action<bool> m_onPvpBattleComplete;

    // 초기화 순서가 이슈인 경우 이곳에서 순차적으로 진행
    private void Start()
    {
        m_celestialBodySpawner = GetComponent<CelestialBodySpawner>();
        if (m_celestialBodySpawner != null)
            m_celestialBodySpawner.SpawnZone(GetInitialZoneIndex());

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

    private int GetInitialZoneIndex()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.m_characterInfo == null) return 1;

        var clearedZones = character.m_characterInfo.clearedZones;
        if (clearedZones == null || clearedZones.Count == 0) return 1;

        string lastCleared = clearedZones[^1];
        int dashIdx = lastCleared.IndexOf('-');
        if (dashIdx <= 0) return 1;
        return int.TryParse(lastCleared.Substring(0, dashIdx), out int zoneIndex) ? zoneIndex : 1;
    }

    public void ChangeZone(int zoneIndex)
    {
        if (m_celestialBodySpawner != null)
            m_celestialBodySpawner.SpawnZone(zoneIndex);
    }



    // ZoneConfig 기반 적 함선 순차 스폰 — 슬롯 단위 관리, 큐 소진 + 전멸 시 클리어
    public void StartSpawnEnemies(ZoneStageConfig zoneStageConfig, System.Action<bool> onComplete)
    {
        if (zoneStageConfig == null || zoneStageConfig.enemyShipConfigs == null || zoneStageConfig.enemyShipConfigs.Count == 0)
        {
            onComplete?.Invoke(true);
            return;
        }

        for (int i = 0; i < m_enemyFleets.Count; i++)
        {
            if (m_enemyFleets[i] != null && m_enemyFleets[i].IsZoneEnemy == true)
                m_enemyFleets[i].StopSpawning();
        }

        m_onZoneBattleComplete = onComplete;

        SpaceFleet newZoneFleet = CreateEnemyFleetShell();
        m_enemyFleets.Add(newZoneFleet);

        GameSpeedController.RestoreSpeed();
        if (m_myFleet != null) m_myFleet.SetFleetState(EUnitState.Battle);
        newZoneFleet.StartSpawning(zoneStageConfig);
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
        enemyFleet.StartFleetWarpIn(() => enemyFleet.SetFleetState(EUnitState.Battle));
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

    // 적 스폰 중지 및 Zone 전투 상태 초기화
    public void StopEnemySpawning()
    {
        for (int i = 0; i < m_enemyFleets.Count; i++)
        {
            if (m_enemyFleets[i] != null && m_enemyFleets[i].IsZoneEnemy == true)
                m_enemyFleets[i].StopSpawning();
        }
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
        fleet.InitializeAsZoneEnemyFleetShell("EnemyFleet", EFormationType.linear_horizontal);
        return fleet;
    }

    // SpaceFleet.SpawnShipCoroutine에서 전멸 판정 후 호출
    public void OnZoneEnemyFleetDefeated(SpaceFleet fleet)
    {
        RemoveEnemyFleet(fleet);
        EventManager.Trigger_EnemyFleetKilled();
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
    

    public SpaceShip GetEnemy()
    {
        if (m_enemyFleets.Count > 0)
        {
            // Clean dead fleets first
            for (int i = m_enemyFleets.Count - 1; i >= 0; i--)
            {
                if (m_enemyFleets[i] == null) { m_enemyFleets.RemoveAt(i); continue; }
                if (m_enemyFleets[i].IsFleetAlive() == false)
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
        Vector3 spawnPosition = basePos + m_myFleet.transform.forward * (maxHalfZ + 50f);
        spawnPosition.y = 0f;

        return spawnPosition;
    }

}

