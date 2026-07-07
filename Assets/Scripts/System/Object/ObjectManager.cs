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

    protected override void OnInitialize()
    {
        DataManager.Instance.ApplyGameSettings();

        var console = DeveloperConsole.Instance;

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


        EffectBase effectPrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/EffectBeamHead");
        if (effectPrefab == null) Debug.LogError("Not found at Resources/Prefabs/Effect/EffectBeamHead");
        m_poolManager.CreatePool(EPoolName.EFFECT_BEAM_HEAD, effectPrefab, 5, 20);

        effectPrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/EffectBeamHit");
        if (effectPrefab == null) Debug.LogError("Not found at Resources/Prefabs/Effect/EffectBeamHit");
        m_poolManager.CreatePool(EPoolName.EFFECT_BEAM_HIT, effectPrefab, 5, 20);

        effectPrefab = ResourceManager.Instance.Load<EffectBase>("Prefabs/Effect/EffectBeamMuzzle");
        if (effectPrefab == null) Debug.LogError("Not found at Resources/Prefabs/Effect/EffectBeamMuzzle");
        m_poolManager.CreatePool(EPoolName.EFFECT_BEAM_MUZZLE, effectPrefab, 5, 20);
        
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

    [HideInInspector] public List<SpaceFleet> m_teamAFleets = new List<SpaceFleet>();
    [HideInInspector] public List<SpaceFleet> m_teamBFleets = new List<SpaceFleet>();
    [HideInInspector] public List<SpaceFleet> m_teamCFleets = new List<SpaceFleet>();
    private static readonly List<SpaceFleet> s_emptyTeamFleets = new List<SpaceFleet>();

    // 지금 이 클라이언트를 플레이 중인 유저가 속한 팀 — 현재는 항상 TeamA지만, 향후 멀티플레이(같은 전투에 여러 유저)에서 B가 될 수도 있어 값으로 분리해둠
    [HideInInspector] public ETeam m_myTeam = ETeam.TeamA;

    // 지크프리트(튜토리얼 연출용) 함대 사용 중 여부 — 튜토리얼 종료 시 실제 함대/모듈포인트로 전환하는 데 사용
    private bool m_isSiegfriedFleetActive = false;
    private int m_realModulePointBackup;
    private int m_realCommanderLevelBackup;

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

    // 활성 미사일 추적 — 아군/적군 분리, 요격 타겟 탐색용
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

    // 기준 위치에서 가장 가까운 적 미사일 반환 (없으면 null)
    public ProjectileMissile GetNearestEnemyMissile(Vector3 from, bool isMysideFriendly)
    {
        List<ProjectileMissile> targets = isMysideFriendly ? m_enemyMissiles : m_friendlyMissiles;
        ProjectileMissile nearest = null;
        float nearestSqrDist = float.MaxValue;

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            ProjectileMissile m = targets[i];
            if (m == null || m.gameObject.activeSelf == false)
            {
                targets.RemoveAt(i);
                continue;
            }
            float sqrDist = (m.transform.position - from).sqrMagnitude;
            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = m;
            }
        }
        return nearest;
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
        AdManager.Instance.ToString();// 광고 초기화 (존 입장 전 미리 로드)

        // 튜토리얼 진행도는 SelectCommander 응답 시점(UIMain.cs)에 이미 확보되어 있음 — 씬 로드 중 깜빡임 없이 바로 결정
        bool bPassTutorial = false;
        if (bPassTutorial == true)
            StartNormalPlay();
        else
            // UI 초기화 후 약간의 딜레이 후 시작
            StartCoroutine(StartTutorial());
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
        m_isBattleEnding = true;
        SpaceFleet myFleet = GetMyFleet();
        bool isPvp = myFleet != null && myFleet.m_fleetState == EUnitState.BattlePvp;

        GameSpeedController.Reset(); // timeScale 및 오디오 피치 복원
        if (myFleet != null)
            myFleet.SetFleetState(EUnitState.Idle);
        StopEnemySpawning();
        OrderAllAircraftReturn();
        CleanupAllProjectiles();
        RemoveAllEnemyFleets();

        if (isPvp)
            EventManager.TriggerPvpBattleEnd(isVictory);
        else
            EventManager.TriggerZoneStageBattleEnd(isVictory);
    }

    // 순서대로 진행할 튜토리얼 목록 — 단계를 늘리려면 이 배열에만 추가하면 됨
    private static readonly string[] TUTORIAL_SEQUENCE =
    {
        "Tutorial_FirstPlay",
        "Tutorial_FirstPlay_ManageShip",
        "Tutorial_FirstPlay_Battle",
        "Tutorial_FirstPlay_Complete"
    };

    private IEnumerator StartTutorial()
    {
        if (TutorialManager.Instance == null)
        { Debug.LogError("TutorialManager.Instance == null"); yield break; }
            
        if (TutorialManager.Instance.IsTutorialCompleted("Tutorial_FirstPlay_Complete") == true)
        { StartNormalPlay(); yield break; } 

        SpawnFleetWithInfo(TutorialCinematicController.BuildSiegfriedFleetInfo("Siegfried Fleet"));
        m_isSiegfriedFleetActive = true;

        // 원점(0,0,0)이 아니라 실제 스테이지 1-10 위치에서 시작 — 배경/천체가 있는 위치로 배치
        ZoneStageConfig siegfriedStage = DataManager.Instance.m_dataTableZone.GetZoneStageByName("1-10");
        if (siegfriedStage != null)
            SetMyFleetPosition(DataManager.Instance.m_dataTableZone.ResolveFleetWorldPosition(siegfriedStage), siegfriedStage.fleetRotationY);

        GrantTutorialModulePoint();
        GrantTutorialCommanderLevel();
        
        // Tutorial 안에 함선추가 등 실제 UI 조작 스텝이 있어 메인 패널(TapButtons 등)이 미리 열려있어야 함
        UIManager.Instance.ShowMainPanel();

        // 서버에서 튜토리얼 진행도 로드 대기 — UIMain.cs에서 이미 확보됐으면 즉시 반환(no-op)
        var loadTask = TutorialManager.Instance.LoadProgressFromServerAsync();
        while (!loadTask.IsCompleted)
        {
            yield return null;
        }
        RunTutorialSequence(0);
    }

    // TUTORIAL_SEQUENCE를 index부터 순서대로 진행 — 마지막까지 끝나면 StartGameplay 호출
    private void RunTutorialSequence(int index)
    {
        if (index >= TUTORIAL_SEQUENCE.Length)
        {
            StartNormalPlay();
            return;
        }

        TutorialManager.Instance.StartTutorial(TUTORIAL_SEQUENCE[index], (tutorialId) =>
        {
            RunTutorialSequence(index + 1);
        });
    }

    private void StartNormalPlay()
    {
        if (m_isSiegfriedFleetActive == true)
            SwitchFromSiegfriedFleetToRealFleet();
        else
            SpawnFleet();
        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetDestroyed);// 플레이어 함대 전멸 이벤트 구독
        NetworkManager.Instance.StartHeartbeat();
        UIManager.Instance.ShowMainPanel();        
    }

    private int GetInitialZoneIndex()
    {
        var commander = DataManager.Instance.m_currentCommander;
        if (commander == null || commander.m_commanderInfo == null) return 1;

        var clearedZones = commander.m_commanderInfo.clearedZones;
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



    // Zone 적 함대 웨이브 — term 기준 순차 스폰, 전멸 시 다음 웨이브 즉시 스폰
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

    // PvP 전투 시작 - 서버에서 받은 상대 FleetInfo로 적 함대 생성
    public void StartPvpBattle(FleetInfo opponentFleetInfo)
    {
        SpaceFleet myFleet = GetMyFleet();
        if (opponentFleetInfo == null || myFleet == null)
        {
            EventManager.TriggerPvpBattleEnd(false);
            return;
        }
        GameSpeedController.RestoreSpeed(); // 이전 전투 배속 복원

        Vector3 spawnPosition = GetEnemySpawnPosition();
        GameObject fleetObj = new GameObject("PvpEnemyFleet");
        fleetObj.transform.position = spawnPosition;

        Vector3 directionToPlayer = myFleet.transform.position - spawnPosition;
        directionToPlayer.y = 0;
        if (directionToPlayer != Vector3.zero)
            fleetObj.transform.rotation = Quaternion.LookRotation(directionToPlayer);

        ETeam enemyTeam = GetOpposingTeam(m_myTeam);
        SpaceFleet enemyFleet = fleetObj.AddComponent<SpaceFleet>();
        enemyFleet.InitializeSpaceFleet(opponentFleetInfo, enemyTeam, EFleetSource.fleet_source_player_remote, EUnitState.Move);
        enemyFleet.StartFleetWarpIn(() =>
        {
            TryStartCombat(enemyFleet, EUnitState.BattlePvp);
        });

        GetTeamFleets(enemyTeam).Add(enemyFleet);
    }

    private void TryStartCombat(SpaceFleet enemyFleet, EUnitState battleState, float playerDelay = 0f, float enemyDelay = 0f)
    {
        m_isBattleEnding = false;
        SpaceFleet myFleet = GetMyFleet();
        if (myFleet == null) return;
        if (enemyFleet == null) return;

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
        Destroy(fleet.gameObject);

        SpaceFleet myFleet = GetMyFleet();
        if (myFleet != null && myFleet.m_fleetState == EUnitState.BattlePvp && enemyTeamFleets.Count == 0)
            ForceEndBattle(true);
    }

    public void StopEnemySpawning()
    {
        if (m_waveTimerCoroutines != null)
        {
            for (int i = 0; i < m_waveTimerCoroutines.Length; i++)
            {
                if (m_waveTimerCoroutines[i] != null)
                {
                    StopCoroutine(m_waveTimerCoroutines[i]);
                    m_waveTimerCoroutines[i] = null;
                }
            }
        }
        m_pendingWaves   = null;
        m_waveSpawned    = null;
        m_currentWaveStage = null;
    }

    // 모든 적 함대 제거
    public void RemoveAllEnemyFleets()
    {
        List<SpaceFleet> enemyTeamFleets = GetOpposingTeamFleets(m_myTeam);
        for (int i = enemyTeamFleets.Count - 1; i >= 0; i--)
        {
            if (enemyTeamFleets[i] != null)
                Destroy(enemyTeamFleets[i].gameObject);
        }
        enemyTeamFleets.Clear();
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

    // 귀환 연출 없이 활성화된 모든 함재기를 즉시 풀로 반환 — 튜토리얼 종료 등 즉시 정리가 필요할 때 사용
    public void DestroyAllAircraft()
    {
        foreach (var aircraft in m_poolManager.GetActiveInstances<AircraftStandard>(EPoolName.AIRCRAFT_STANDARD))
            aircraft.ForceReturnToPoolImmediate();
    }

    // 튜토리얼 중에는 서버에 반영되지 않는 임시 모듈포인트 지급 — 실제 값은 백업해뒀다가 튜토리얼 종료 시 복원
    private void GrantTutorialModulePoint()
    {
        const int TUTORIAL_MODULE_POINT = 100;

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        m_realModulePointBackup = commander.GetModulePoint();
        commander.UpdateModulePoint(TUTORIAL_MODULE_POINT);
    }

    // 튜토리얼 중 함선 추가(4→5번째) 시 지휘 레벨 요구사항에 막히지 않도록 임시로 레벨 지급
    private void GrantTutorialCommanderLevel()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        SpaceFleet siegfriedFleet = GetMyFleet();
        int nextShipCount = siegfriedFleet != null ? siegfriedFleet.m_ships.Count + 1 : 1;
        int requiredLevel = DataManager.Instance.m_dataTableCommanderLevel.GetRequiredCommanderLevel(nextShipCount);

        m_realCommanderLevelBackup = commander.GetCommanderLevel();
        commander.UpdateCommanderLevel(requiredLevel);
    }

    // 튜토리얼 임시 함대(지크프리트 등 연출용)만 제거 — 실제 함대 스폰은 하지 않음
    // (탈출 함선 폭발 연출 직후처럼, 실제 스폰은 StartNormalPlay()에서 한 번만 일어나야 할 때 사용)
    public void DestroyTutorialFleet(SpaceFleet fleet)
    {
        if (fleet == null) return;
        GetTeamFleets(m_myTeam).Remove(fleet);
        Destroy(fleet.gameObject);
    }

    // 지크프리트(연출용) 함대를 제거하고 실제 함대 + 실제 모듈포인트로 전환
    private void SwitchFromSiegfriedFleetToRealFleet()
    {
        m_isSiegfriedFleetActive = false;

        DestroyTutorialFleet(GetMyFleet());

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander != null)
        {
            commander.UpdateModulePoint(m_realModulePointBackup);
            commander.UpdateCommanderLevel(m_realCommanderLevelBackup);
        }

        SpawnFleet(warpIn: true);
    }

    private void SpawnFleet(bool warpIn = false)
    {
        // 서버에서 받은 함대 정보가 없으면 스폰하지 않음
        if (DataManager.Instance.m_currentFleetInfo == null) return;

        SpawnFleetWithInfo(DataManager.Instance.m_currentFleetInfo, warpIn);
    }

    // warpIn=true면 Trigger_MyFleetSet으로 위치가 확정된 직후 그 자리로 워프인 연출(튜토리얼 종료 후 첫 함대 등장 등)
    private void SpawnFleetWithInfo(FleetInfo fleetInfo, bool warpIn = false)
    {
        GameObject fleetObj = new GameObject("MyFleet");
        SpaceFleet myFleet = fleetObj.AddComponent<SpaceFleet>();
        myFleet.InitializeSpaceFleet(fleetInfo, m_myTeam, EFleetSource.fleet_source_player, EUnitState.Idle);
        GetTeamFleets(m_myTeam).Add(myFleet);

        // 함대 스폰/교체를 UI 등 늦게 초기화되는 쪽에서도 알 수 있도록 알림 (존 초기 위치도 이 안에서 확정됨)
        EventManager.Trigger_MyFleetSet();

        // 카메라가 함대를 타겟으로 설정
        CameraController.Instance.SetTargetOfCameraController(myFleet.transform);

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

        if (m_pendingWaves == null) return;

        // 미스폰 웨이브 중 가장 앞의 것을 즉시 스폰 (term 타이머 취소)
        for (int i = 0; i < m_waveSpawned.Length; i++)
        {
            if (m_waveSpawned[i] == false)
            {
                if (m_waveTimerCoroutines[i] != null)
                {
                    StopCoroutine(m_waveTimerCoroutines[i]);
                    m_waveTimerCoroutines[i] = null;
                }
                SpawnWave(i);
                return;
            }
        }

        // 모든 웨이브 스폰 완료 + 적 전멸 → 클리어
        if (GetOpposingTeamFleets(m_myTeam).Count == 0)
        {
            EventManager.Trigger_AllEnemyFleetKilled();
            EventManager.TriggerZoneStageBattleEnd(true);
        }
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
                    Destroy(enemyTeamFleets[i].gameObject);
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


