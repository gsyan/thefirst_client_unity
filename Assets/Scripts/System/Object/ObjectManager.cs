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
    private DebrisFieldSpawner m_debrisFieldSpawner;

    [HideInInspector] public List<SpaceFleet> m_teamAFleets = new List<SpaceFleet>();
    [HideInInspector] public List<SpaceFleet> m_teamBFleets = new List<SpaceFleet>();
    [HideInInspector] public List<SpaceFleet> m_teamCFleets = new List<SpaceFleet>();
    private static readonly List<SpaceFleet> s_emptyTeamFleets = new List<SpaceFleet>();

    // 지금 이 클라이언트를 플레이 중인 유저가 속한 팀 — 현재는 항상 TeamA지만, 향후 멀티플레이(같은 전투에 여러 유저)에서 B가 될 수도 있어 값으로 분리해둠
    [HideInInspector] public ETeam m_myTeam = ETeam.TeamA;

    // 지크프리트(튜토리얼 연출용) 함대 사용 중 여부 — 튜토리얼 종료 시 실제 함대로 전환하는 데 사용
    private bool m_isSiegfriedFleetActive = false;
    private int m_realCommanderLevelBackup;

    // 온보딩 튜토리얼(TutorialManager.ONBOARDING_TUTORIAL_SEQUENCE) 진행 중 스킵 버튼을 눌렀는지 여부
    private bool m_isOnboardingTutorialSkipped = false;

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
        m_debrisFieldSpawner = GetComponent<DebrisFieldSpawner>();
        if (m_debrisFieldSpawner != null)
            m_debrisFieldSpawner.SpawnZone(GetInitialZoneIndex());
        AdManager.Instance.ToString();// 광고 초기화 (존 입장 전 미리 로드)

        // 튜토리얼 진행도는 SelectCommander 응답 시점(UIMain.cs)에 이미 확보되어 있음 — 씬 로드 중 깜빡임 없이 바로 결정
        // 지크프리트 오프닝 시네마틱(TutorialCinematicController)이 주석처리되어 있어 튜토리얼 시작 자체를 임시로 막아둠 — 프리셋 기반으로 재작성 전까지
        /*
        bool bPassTutorial = false;
        if (bPassTutorial == true)
            StartNormalPlay();
        else
            // UI 초기화 후 약간의 딜레이 후 시작
            StartCoroutine(StartTutorial());
        */

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
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnTutorialSkipRequested -= OnOnboardingTutorialSkipRequested;
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

    private IEnumerator StartTutorial()
    {
        if (TutorialManager.Instance == null)
        { Debug.LogError("TutorialManager.Instance == null"); yield break; }

        if (TutorialManager.Instance.IsTutorialCompleted("Tutorial_FirstPlay_Complete") == true)
        { StartNormalPlay(); yield break; }

        // TutorialCinematicController 주석처리로 임시 비활성화 — 프리셋 기반으로 재작성 전까지 시네마틱 함대 스폰 안 함
        m_isSiegfriedFleetActive = true;

        GrantTutorialCommanderLevel();

        // Tutorial 안에 함선추가 등 실제 UI 조작 스텝이 있어 메인 패널(TapButtons 등)이 미리 열려있어야 함
        UIManager.Instance.ShowMainPanel();

        // 서버에서 튜토리얼 진행도 로드 대기 — UIMain.cs에서 이미 확보됐으면 즉시 반환(no-op)
        var loadTask = TutorialManager.Instance.LoadProgressFromServerAsync();
        while (!loadTask.IsCompleted)
        {
            yield return null;
        }

        // 스킵 버튼으로 온보딩 시퀀스 전체를 건너뛴 경우 다음 튜토리얼로 이어가지 않고 바로 노말 플레이로 전환
        TutorialManager.Instance.OnTutorialSkipRequested += OnOnboardingTutorialSkipRequested;
        RunTutorialSequence(0);
    }

    // TutorialManager.ONBOARDING_TUTORIAL_SEQUENCE를 index부터 순서대로 진행 — 마지막까지 끝나면 StartNormalPlay 호출
    private void RunTutorialSequence(int index)
    {
        if (m_isOnboardingTutorialSkipped == true) return;

        if (index >= TutorialManager.ONBOARDING_TUTORIAL_SEQUENCE.Length)
        {
            StartNormalPlay();
            return;
        }

        TutorialManager.Instance.StartTutorial(TutorialManager.ONBOARDING_TUTORIAL_SEQUENCE[index], (tutorialId) =>
        {
            RunTutorialSequence(index + 1);
        });
    }

    // 스킵 버튼 클릭으로 온보딩 시퀀스 전체가 완료 처리됐을 때 호출 — 남은 시퀀스 진행 없이 바로 노말 플레이로 전환
    private void OnOnboardingTutorialSkipRequested()
    {
        TutorialManager.Instance.OnTutorialSkipRequested -= OnOnboardingTutorialSkipRequested;
        m_isOnboardingTutorialSkipped = true;
        StartNormalPlay();
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

        // 온보딩(ONBOARDING_TUTORIAL_SEQUENCE) 완료 후 실제 함대로 최초 진입한 시점 — 이미 완료된 경우 StartTutorial이 즉시 no-op
        // 일일 보상 팝업(달력)이 화면을 가리고 있으면 튜토리얼이 탐사 버튼을 가리켜도 안 보이므로,
        // 팝업이 닫힌 뒤(또는 애초에 안 떴으면 즉시)에 시작하도록 콜백으로 순서를 맞춤
        if (DailyBonusManager.Instance != null)
            DailyBonusManager.Instance.CheckAndShowDailyRewardPopup(() => TutorialManager.Instance.StartTutorial("Tutorial_Exploration"));
        else
            TutorialManager.Instance.StartTutorial("Tutorial_Exploration");
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
    // 함선 시스템 대격변으로 PvP 로직 전체 주석처리 — 삭제 아님, UITabPvp.cs와 함께 비활성화
    /*
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
    */

    public void TryStartCombat(SpaceFleet enemyFleet, EUnitState battleState, float playerDelay = 0f, float enemyDelay = 0f)
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

    // 함선 시스템 대격변으로 구존-스테이지 웨이브 필드(m_pendingWaves 등)가 주석 처리되어 현재는 no-op — 복원 시 함께 되살릴 것
    public void StopEnemySpawning()
    {
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

    // 튜토리얼 중 함선 추가(4→5번째) 및 지크프리트 함대(T14 기함 포함)의 모듈 레벨업이 지휘 레벨 요구사항에
    // 막히지 않도록 임시로 레벨 지급 — 함선 수 요구 레벨과 T14 서브타입 상한 요구 레벨 중 더 높은 쪽을 지급
    private void GrantTutorialCommanderLevel()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        SpaceFleet siegfriedFleet = GetMyFleet();
        int nextShipCount = siegfriedFleet != null ? siegfriedFleet.m_ships.Count + 1 : 1;
        int shipCountRequiredLevel = DataManager.Instance.m_dataTableCommander.GetRequiredCommanderLevel(nextShipCount);
        int requiredLevel = shipCountRequiredLevel; // TutorialCinematicController 주석처리로 지크프리트 서브타입 티어 요구치는 임시 제외

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

    // 지크프리트(연출용) 함대를 제거하고 실제 함대로 전환
    private void SwitchFromSiegfriedFleetToRealFleet()
    {
        m_isSiegfriedFleetActive = false;

        DestroyTutorialFleet(GetMyFleet());

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander != null)
        {
            commander.UpdateCommanderLevel(m_realCommanderLevelBackup);
        }

        SpawnFleet(warpIn: true);
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

        DataTableShipPreset presetTable = DataManager.Instance.m_dataTableShipPreset;
        ShipStatFormulaSettings formula = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula;
        if (fleetInfo != null && fleetInfo.ships != null)
        {
            for (int shipIndex = 0; shipIndex < fleetInfo.ships.Count; shipIndex++)
            {
                ShipInfo shipSlot = fleetInfo.ships[shipIndex];
                ShipPresetData preset = presetTable.GetShipPreset(shipSlot.shipPresetId);
                if (preset == null)
                {
                    Debug.LogError($"ShipPresetData not found: {shipSlot.shipPresetId}");
                    continue;
                }
                ModuleData bodyModuleData = null;
                if (System.Enum.TryParse(preset.prefabName, out EModuleSubType bodySubType))
                    bodyModuleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(bodySubType);

                // shipSlot.bodies는 서버에 저장된 "이 함선에 실제로 장착된 모듈 구성"(로드아웃) — 있으면(내 함대) 그걸 우선 쓰고,
                // 없으면(적 함대 등) preset.statAllocation(프리셋의 기본 장착 구성)을 그대로 씀
                ModuleBodyInfo actualModules = shipSlot.bodies != null && shipSlot.bodies.Count > 0 ? shipSlot.bodies[0] : null;
                ShipStatAllocation allocation = ShipStatAllocation.BuildFromModuleBodyInfo(preset.statAllocation, actualModules);

                ShipFinalStats finalStats = ShipStatCalculator.Calculate(allocation, formula, bodyModuleData, DataManager.Instance.m_dataTableModule);
                ExplorationShipSpawnBridge.SpawnShip(fleet, preset, finalStats, shipIndex, shipSlot.isFront, shipSlot.healthMultiplier, shipSlot.attackMultiplier);
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

        ship.m_shipInfo.isFront = isFront;
        myFleet.UpdateShipFormation(myFleet.m_currentFormationType, bSmooth: true);
    }

    // 함대편성 UI(FleetComposition)에서 슬롯 하나에 배치/교체하거나 장착 모듈만 바뀌었을 때 호출 — 그 슬롯의 함선만 파괴/재생성, 나머지 함선은 그대로 유지
    // modules를 생략하면(null) 프리셋 기본 장착 구성(바디 교체 시 리셋된 값)을 쓰고, 넘기면(모듈 편집 후) 그 실제 장착 구성으로 스탯을 계산함
    public void ReplaceMyFleetShipAt(int positionIndex, string shipPresetId, bool isFront, ModuleBodyInfo modules = null)
    {
        SpaceFleet myFleet = GetMyFleet();
        if (myFleet == null) return;

        // 존 런 진행 중 함선 종류를 바꿔도 체력이 회복되면 안 되므로, 교체 전 이전 함선의 체력 비율을 미리 계산해둠(빈 슬롯이면 1f=만피)
        SpaceShip oldShip = myFleet.m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == positionIndex);
        float previousHealthRatio = oldShip != null ? oldShip.GetHealthRatio() : 1f;
        if (oldShip != null)
            myFleet.RemoveShip(oldShip, refreshFormation: false, triggerDefeatEvents: false);

        DataTableShipPreset presetTable = DataManager.Instance.m_dataTableShipPreset;
        ShipPresetData preset = presetTable.GetShipPreset(shipPresetId);
        if (preset == null)
        {
            Debug.LogError($"ShipPresetData not found: {shipPresetId}");
            return;
        }

        ModuleData bodyModuleData = null;
        if (System.Enum.TryParse(preset.prefabName, out EModuleSubType bodySubType))
            bodyModuleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(bodySubType);

        ShipStatFormulaSettings formula = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula;
        ShipStatAllocation allocation = ShipStatAllocation.BuildFromModuleBodyInfo(preset.statAllocation, modules);
        ShipFinalStats finalStats = ShipStatCalculator.Calculate(allocation, formula, bodyModuleData, DataManager.Instance.m_dataTableModule);

        SpaceShip newShip = ExplorationShipSpawnBridge.SpawnShip(myFleet, preset, finalStats, positionIndex, isFront);
        // 존 런 진행 중이면 이전 함선의 손상 비율을 새 함선에 그대로 이전(회복 금지) — 평시 편성(런 없음)은 만피 유지
        if (newShip != null && IsExplorationRunActive() == true)
            newShip.ApplyHealthRatio(previousHealthRatio);

        // 스무스 이동(RefreshFormation) 대신 bSmooth: false로 즉시 최종 위치에 배치 — 연출 없이 바로 나옴
        myFleet.UpdateShipFormation(myFleet.m_currentFormationType, bSmooth: false);
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

        // 미스폰 웨이브 중 가장 앞의 것을 즉시 스폰 (term 타이머 취소)
        // 구존-스테이지 로직 주석처리(StartZoneEnemyWaves 비활성화)로 m_pendingWaves는 항상 null이라 이 블록은 도달 불가 — SpawnWave 재활성화 시 함께 복원
        /*
        if (m_pendingWaves == null) return;
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
        */

        // 모든 웨이브 스폰 완료 + 적 전멸 → 클리어
        // ForceEndBattle(true)가 m_isBattleEnding 가드(내 함대도 거의 동시에 전멸해 ForceEndBattle(false)가
        // 먼저 확정된 경우 승리로 뒤집지 않음) 및 함대 상태 리셋/배속 복원/정리를 전부 처리
        if (GetOpposingTeamFleets(m_myTeam).Count == 0)
        {
            EventManager.Trigger_AllEnemyFleetKilled();
            ForceEndBattle(true);
        }
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


