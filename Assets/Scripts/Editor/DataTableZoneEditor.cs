// DataTableZone 에디터 - 존 데이터 편집 GUI
// CSV Import: datatable_zone_stage.csv + datatable_zone_enemy.csv → ScriptableObject
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

[CustomEditor(typeof(DataTableZone))]
public class DataTableZoneEditor : Editor
{
    private DataTableZone m_dataTableZone;
    private Vector2 scrollPosition;

    private Dictionary<string, bool> zoneFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, Dictionary<int, bool>> shipFoldouts = new Dictionary<string, Dictionary<int, bool>>();
    private Dictionary<int, bool> zoneGroupFoldouts = new Dictionary<int, bool>(); // x값(그룹) 폴드아웃
    private Dictionary<int, bool> m_cameraAnchorFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> m_celestialFoldouts    = new Dictionary<int, bool>();

    // 자동 배치 생성 파라미터 (에디터 전용)
    private Dictionary<int, int>   m_autoGenSeed       = new Dictionary<int, int>();
    private Dictionary<int, float> m_autoGenXRange     = new Dictionary<int, float>();
    private Dictionary<int, float> m_autoGenZRange     = new Dictionary<int, float>();
    private Dictionary<int, float> m_autoGenMinZGap    = new Dictionary<int, float>();
    private Dictionary<int, int>   m_autoGenStageCount = new Dictionary<int, int>();
    private readonly Color zoneColor       = new Color(0.7f, 0.85f, 0.95f);
    private readonly Color shipColor       = new Color(0.85f, 0.95f, 0.85f);
    private readonly Color slotColor       = new Color(0.9f, 0.9f, 0.95f);
    private readonly Color multiplierColor = new Color(0.95f, 0.88f, 0.75f);

    // Body SubType 목록 캐싱
    private EModuleSubType[] bodySubTypes;
    private string[] bodySubTypeNames;

    private void OnEnable()
    {
        m_dataTableZone = (DataTableZone)target;
        CacheBodySubTypes();
    }

    private void CacheBodySubTypes()
    {
        var bodyTypes = new List<EModuleSubType>();
        foreach (EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if ((EModuleType)subType.GetModuleType() == EModuleType.body)
                bodyTypes.Add(subType);
        }
        bodySubTypes = bodyTypes.ToArray();
        bodySubTypeNames = bodyTypes.Select(t => t.ToString()).ToArray();
    }

    public override void OnInspectorGUI()
    {
        if (m_dataTableZone == null) return;

        serializedObject.Update();
        Undo.RecordObject(m_dataTableZone, "Edit DataTableZone");

        EditorGUILayout.Space(5);
        DrawHeader();
        EditorGUILayout.Space(5);
        DrawUtilityTools();
        EditorGUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawZoneList();
        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(m_dataTableZone);
            m_dataTableZone.BuildRuntimeCache();
            serializedObject.ApplyModifiedProperties();
        }
    }

    private new void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("Datatable Zone", EditorStyles.largeLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Zones: {m_dataTableZone.zoneStageList.Count}", EditorStyles.miniLabel);

        if (GUILayout.Button("+ Add Zone", GUILayout.Width(100)))
        {
            m_dataTableZone.zoneStageList.Add(new ZoneStageConfig
            {
                zoneName = $"ZoneStage {m_dataTableZone.zoneStageList.Count + 1}",
                zoneDescription = "New Zone"
            });
            EditorUtility.SetDirty(m_dataTableZone);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawUtilityTools()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Utility Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Import All", GUILayout.Height(28))) ImportAll();
        if (GUILayout.Button("Export All", GUILayout.Height(28))) ExportAll();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);
        DrawEnemyGeneratorTool();
        EditorGUILayout.Space(10);
        DrawPlanetGeneratorTool();
        EditorGUILayout.Space(10);
        DrawFleetPositionBatchGeneratorTool();
    }

    // CSV 간 의존 순서(Stage가 먼저 있어야 Enemy Fleet/Enemy가 붙을 zoneStageList가 존재)를 내부적으로 처리
    private void ImportAll()
    {
        ImportCamera();
        ImportCelestial();
        ImportStage();
        ImportEnemyFleet();
        ImportEnemy();
        LoadEnemyConfigCsv();
        EditorUtility.DisplayDialog("완료", "전체 Import 완료", "OK");
    }

    private void ExportAll()
    {
        DataTableZoneCSVUtility.ExportZone(m_dataTableZone);
        DataTableZoneCSVUtility.ExportCelestial(m_dataTableZone);
        DataTableZoneCSVUtility.ExportZoneStage(m_dataTableZone);
        DataTableZoneCSVUtility.ExportEnemyFleet(m_dataTableZone);
        DataTableZoneCSVUtility.ExportEnemy(m_dataTableZone);
        if (m_genZoneConfigs != null) SaveEnemyConfigCsv(showDialog: false);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("완료", "전체 Export 완료", "OK");
    }

    private static readonly string k_cameraCSV      = "Assets/Resources/DataTable/Zone/datatable_zone_camera.csv";
    private static readonly string k_celestialCSV   = "Assets/Resources/DataTable/Zone/datatable_zone_celestial.csv";
    private static readonly string k_stageCSV       = "Assets/Resources/DataTable/Zone/datatable_zone_stage.csv";
    private static readonly string k_enemyCSV       = "Assets/Resources/DataTable/Zone/datatable_zone_enemy.csv";
    private static readonly string k_enemyFleetCSV  = "Assets/Resources/DataTable/Zone/datatable_zone_enemy_fleet_position.csv";

    private void ImportCamera()
    {
        if (!File.Exists(k_cameraCSV)) { EditorUtility.DisplayDialog("Error", $"파일 없음:\n{k_cameraCSV}", "OK"); return; }

        // celestialBodies 유지를 위해 기존 맵 보존
        var oldCelestialMap = new Dictionary<int, List<CelestialBodyConfig>>();
        for (int j = 0; j < m_dataTableZone.zoneList.Count; j++)
        {
            ZoneConfig zc = m_dataTableZone.zoneList[j];
            oldCelestialMap[zc.zoneIndex] = zc.celestialBodies;
        }

        m_dataTableZone.zoneList.Clear();
        string[] lines = File.ReadAllLines(k_cameraCSV);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            if (!int.TryParse(col[0], out int zoneIndex)) continue;
            float.TryParse(col[1], out float cx); float.TryParse(col[2], out float cy); float.TryParse(col[3], out float cz);
            float.TryParse(col[4], out float zoom); float.TryParse(col[5], out float rotX); float.TryParse(col[6], out float rotY);
            oldCelestialMap.TryGetValue(zoneIndex, out var celestials);
            m_dataTableZone.zoneList.Add(new ZoneConfig
            {
                zoneIndex          = zoneIndex,
                galaxyCameraTarget = new Vector3(cx, cy, cz),
                galaxyCameraZoom   = zoom,
                galaxyCameraRotX   = rotX,
                galaxyCameraRotY   = rotY,
                celestialBodies    = celestials != null ? celestials : new List<CelestialBodyConfig>(),
            });
        }
        EditorUtility.SetDirty(m_dataTableZone);
        AssetDatabase.Refresh();
        Debug.Log($"[DataTableZone] Zone Camera import 완료 ({m_dataTableZone.zoneList.Count}개)");
    }

    private void ImportCelestial()
    {
        if (!File.Exists(k_celestialCSV)) { EditorUtility.DisplayDialog("Error", $"파일 없음:\n{k_celestialCSV}", "OK"); return; }

        var zoneMap = new Dictionary<int, ZoneConfig>();
        for (int j = 0; j < m_dataTableZone.zoneList.Count; j++)
        {
            m_dataTableZone.zoneList[j].celestialBodies = new List<CelestialBodyConfig>();
            zoneMap[m_dataTableZone.zoneList[j].zoneIndex] = m_dataTableZone.zoneList[j];
        }

        string[] lines = File.ReadAllLines(k_celestialCSV);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            if (!int.TryParse(col[0], out int zi)) continue;
            if (!zoneMap.TryGetValue(zi, out ZoneConfig zc)) continue;

            float F(int idx, float def = 0f) => col.Length > idx && float.TryParse(col[idx], out float v) ? v : def;
            bool  B(int idx)                 => col.Length > idx && col[idx].Trim().ToLower() == "true";
            Color C(int idx, Color def)      { if (col.Length <= idx) return def; return ColorUtility.TryParseHtmlString(col[idx].Trim(), out Color c) ? c : def; }

            zc.celestialBodies.Add(new CelestialBodyConfig
            {
                position = new Vector3(F(1), F(2), F(3)), rotation = new Vector3(F(4), F(5), F(6)), scale = new Vector3(F(7), F(8), F(9)),
                landCoverage = F(10, 0.5f), biomeBlend = F(11, 0.01f), gBlend = F(12, 0.02f),
                deepSeaColor = C(13, CommonUtility.HexColor("#0D2673")), shallowSeaColor = C(14, CommonUtility.HexColor("#1A59A6")),
                lowlandSandColor = C(15, CommonUtility.HexColor("#BFB380")), lowlandGreenColor = C(16, CommonUtility.HexColor("#90C060")),
                plainsDesertColor = C(17, CommonUtility.HexColor("#A99159")), plainsGrassColor = C(18, CommonUtility.HexColor("#478C2E")),
                plainsForestColor = C(19, CommonUtility.HexColor("#236523")), highlandSnowColor = C(20, CommonUtility.HexColor("#E8F0F5")),
                hasPolarIce = B(21), iceColor = C(22, CommonUtility.HexColor("#F2FAFF")), iceColorEdge = C(23, CommonUtility.HexColor("#ADD1F0")), poleIceWidth = F(24, 0.12f),
                hasClouds = B(25), cloudColor = C(26, CommonUtility.HexColor("#FFFFFFD9")), cloudCoverage = F(27, 0.5f), cloudRotation = F(28), cloudScale = F(29, 1.01f),
                cloudMidLatOpacity = F(30, 0f), cloudMidLatCenter = F(31, 0.25f), cloudMidLatWidth = F(32, 0.12f), cloudSoftness = F(33, 0.3f),
                hasAtmosphere = B(34), atmosphereColor = C(35, CommonUtility.HexColor("#4D99FF")), atmosphereScale = F(36, 1.01f),
            });
        }
        EditorUtility.SetDirty(m_dataTableZone);
        AssetDatabase.Refresh();
        Debug.Log("[DataTableZone] Celestial import 완료");
    }

    private void ImportStage()
    {
        if (!File.Exists(k_stageCSV)) { EditorUtility.DisplayDialog("Error", $"파일 없음:\n{k_stageCSV}", "OK"); return; }

        // 기존 enemyFleets 보존
        var enemyBackup = new Dictionary<string, List<StageEnemyFleetSpawnConfig>>();
        for (int j = 0; j < m_dataTableZone.zoneStageList.Count; j++)
        {
            ZoneStageConfig zs = m_dataTableZone.zoneStageList[j];
            if (zs.enemyFleets != null && zs.enemyFleets.Count > 0) enemyBackup[zs.zoneName] = zs.enemyFleets;
        }

        m_dataTableZone.zoneStageList.Clear();
        string[] lines = File.ReadAllLines(k_stageCSV);
        int count = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            if (!int.TryParse(col[0], out int zoneIndex) || !int.TryParse(col[1], out int stage)) continue;

            int.TryParse(col[2], out int clearReward);
            int.TryParse(col.Length > 3 ? col[3] : "0", out int expPt);
            int.TryParse(col.Length > 4 ? col[4] : "0", out int modPt);
            float.TryParse(col.Length > 5 ? col[5] : "0", out float spawnTerm);
            float.TryParse(col.Length > 6 ? col[6] : "0", out float fpx);
            float.TryParse(col.Length > 7 ? col[7] : "0", out float fpy);
            float.TryParse(col.Length > 8 ? col[8] : "0", out float fpz);
            float.TryParse(col.Length > 9 ? col[9] : "0", out float frotY);
            float.TryParse(col.Length > 10 ? col[10] : "0", out float playerFireDelay);
            float.TryParse(col.Length > 11 ? col[11] : "0", out float enemyFireDelay);

            string zoneName = $"{zoneIndex}-{stage}";
            enemyBackup.TryGetValue(zoneName, out List<StageEnemyFleetSpawnConfig> fleets);
            m_dataTableZone.zoneStageList.Add(new ZoneStageConfig
            {
                zoneName               = zoneName,
                zoneDescription        = $"Zone {zoneName}",
                zoneIndex              = zoneIndex,
                spawnTerm              = spawnTerm > 0 ? spawnTerm : 20f,
                mineralClearReward     = clearReward,
                expClearReward         = expPt,
                modulePointClearReward = modPt,
                fleetPosition          = new Vector3(fpx, fpy, fpz),
                fleetRotationY         = frotY,
                playerFireDelaySec     = playerFireDelay,
                enemyFireDelaySec      = enemyFireDelay,
                enemyFleets            = fleets != null ? fleets : new List<StageEnemyFleetSpawnConfig>(),
            });
            count++;
        }
        m_dataTableZone.BuildRuntimeCache();
        EditorUtility.SetDirty(m_dataTableZone);
        AssetDatabase.Refresh();
        Debug.Log($"[DataTableZone] Stage import 완료 ({count}개)");
    }

    private void ImportEnemyFleet()
    {
        if (!File.Exists(k_enemyFleetCSV)) { EditorUtility.DisplayDialog("Error", $"파일 없음:\n{k_enemyFleetCSV}", "OK"); return; }

        var presets = new List<FleetPositionPreset>();
        string[] lines = File.ReadAllLines(k_enemyFleetCSV);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            if (!int.TryParse(col[0], out int grade)) continue;

            int.TryParse(col[1], out int index);
            float.TryParse(col[2], out float distance);
            float.TryParse(col[3], out float rotX);
            float.TryParse(col[4], out float rotY);
            float.TryParse(col[5], out float rotZ);

            presets.Add(new FleetPositionPreset { index = index, grade = grade, distance = distance, rotX = rotX, rotY = rotY, rotZ = rotZ });
        }

        m_dataTableZone.fleetPositionPresets = presets;
        EditorUtility.SetDirty(m_dataTableZone);
        AssetDatabase.Refresh();
        Debug.Log("[DataTableZone] Enemy Fleet Position import 완료");
    }

    private void ImportEnemy()
    {
        if (!File.Exists(k_enemyCSV)) { EditorUtility.DisplayDialog("Error", $"파일 없음:\n{k_enemyCSV}", "OK"); return; }

        // zoneName+fleetIndex → ships 재구성
        // 컬럼: zone_stage,stage,fleet_index,ship_index,body_type,body_level,...
        var shipMap = new Dictionary<string, Dictionary<int, List<ShipInfo>>>();
        string[] lines = File.ReadAllLines(k_enemyCSV);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            if (!int.TryParse(col[0], out int zoneIndex) || !int.TryParse(col[1], out int stageNum)) continue;

            string zoneName = $"{zoneIndex}-{stageNum}";
            if (!shipMap.ContainsKey(zoneName)) shipMap[zoneName] = new Dictionary<int, List<ShipInfo>>();

            int.TryParse(col[2], out int fleetIdx);
            if (!shipMap[zoneName].ContainsKey(fleetIdx)) shipMap[zoneName][fleetIdx] = new List<ShipInfo>();

            int.TryParse(col[3], out int shipIdx);
            System.Enum.TryParse(col[4], out EModuleSubType bodyType);
            int.TryParse(col[5], out int bodyLv);

            var beams = new List<ModuleInfo>(); var missiles = new List<ModuleInfo>(); var hangers = new List<ModuleInfo>();
            if (!string.IsNullOrEmpty(col[6]) && System.Enum.TryParse(col[6], out EModuleSubType beamType) && int.TryParse(col[7], out int beamLv))
            { int cnt = col.Length > 8 && int.TryParse(col[8], out int bc) ? bc : 1; for (int s = 0; s < cnt; s++) beams.Add(new ModuleInfo { moduleType = EModuleType.beam, moduleSubType = beamType, moduleLevel = beamLv, bodyIndex = 0, slotIndex = s }); }
            if (!string.IsNullOrEmpty(col[9]) && System.Enum.TryParse(col[9], out EModuleSubType missileType) && int.TryParse(col[10], out int missileLv))
            { int cnt = col.Length > 11 && int.TryParse(col[11], out int mc) ? mc : 1; for (int s = 0; s < cnt; s++) missiles.Add(new ModuleInfo { moduleType = EModuleType.missile, moduleSubType = missileType, moduleLevel = missileLv, bodyIndex = 0, slotIndex = s }); }
            if (!string.IsNullOrEmpty(col[12]) && System.Enum.TryParse(col[12], out EModuleSubType hangerType) && int.TryParse(col[13], out int hangerLv))
            { int cnt = col.Length > 14 && int.TryParse(col[14], out int hc) ? hc : 1; for (int s = 0; s < cnt; s++) hangers.Add(new ModuleInfo { moduleType = EModuleType.hanger, moduleSubType = hangerType, moduleLevel = hangerLv, bodyIndex = 0, slotIndex = s }); }

            float.TryParse(col[15], out float bodyR); float.TryParse(col[16], out float beamR);
            float.TryParse(col[17], out float missileR); float.TryParse(col[18], out float hangerR);
            shipMap[zoneName][fleetIdx].Add(new ShipInfo
            {
                shipName = $"EnemyShip_{shipIdx}", positionIndex = shipIdx,
                bodyMultiplier = bodyR, beamMultiplier = beamR, missileMultiplier = missileR, hangerMultiplier = hangerR,
                bodies = new List<ModuleBodyInfo> { new ModuleBodyInfo { moduleType = EModuleType.body, moduleSubType = bodyType, moduleLevel = bodyLv, bodyIndex = 0, beams = beams, missiles = missiles, hangers = hangers } }
            });
        }

        for (int j = 0; j < m_dataTableZone.zoneStageList.Count; j++)
        {
            ZoneStageConfig zs = m_dataTableZone.zoneStageList[j];
            if (!shipMap.TryGetValue(zs.zoneName, out Dictionary<int, List<ShipInfo>> fleetShips)) continue;
            if (zs.enemyFleets == null) continue;
            for (int fi = 0; fi < zs.enemyFleets.Count; fi++)
            {
                StageEnemyFleetSpawnConfig fc = zs.enemyFleets[fi];
                if (!fleetShips.TryGetValue(fc.fleetIndex, out List<ShipInfo> ships)) continue;
                if (fc.fleetInfo == null) fc.fleetInfo = new FleetInfo { fleetName = $"{zs.zoneName}_fleet{fc.fleetIndex}" };
                fc.fleetInfo.ships = ships;
            }
        }
        EditorUtility.SetDirty(m_dataTableZone);
        AssetDatabase.Refresh();
        Debug.Log("[DataTableZone] Enemy Ships import 완료");
    }

    #region 적 함대 절차적 생성 (zoneStageList 직접 수정, CSV 거치지 않음)

    // T1~T14 함체 프리팹의 빔/미사일/격납고 슬롯 개수 상한 (datatable_module.csv 기준, moduleLevel=1 슬롯 구성)
    private static readonly Dictionary<int, (int beam, int missile, int hanger)> k_slotCap = new()
    {
        { 1,  (1, 1, 1) }, { 2,  (2, 1, 1) }, { 3,  (2, 1, 1) }, { 4,  (2, 2, 1) },
        { 5,  (2, 2, 2) }, { 6,  (2, 2, 2) }, { 7,  (3, 2, 2) }, { 8,  (3, 3, 2) },
        { 9,  (3, 3, 3) }, { 10, (4, 3, 3) }, { 11, (4, 4, 3) }, { 12, (4, 4, 4) },
        { 13, (5, 4, 4) }, { 14, (5, 5, 4) },
    };

    private const string k_enemyConfigCSV = "Assets/Resources/DataTable/Zone/datatable_zone_enemy_config.csv";

    private class ZoneEnemyConfigRow
    {
        public int zone;
        public int fleets;
        public int budget;
        public int maxTier;
        public int deviation;
    }

    private int m_genZoneStart = 1;
    private int m_genZoneEnd   = 100;
    private int m_genMaxTier   = 14;
    private int m_genMaxShips  = 5;
    private int m_genRandomSeed = 12345; // 공용 시드 — 같으면 항상 같은 결과(재현 가능), 바뀌면 다른 패턴
    private List<ZoneEnemyConfigRow> m_genZoneConfigs;                      // zone 순서 보존
    private Dictionary<int, ZoneEnemyConfigRow> m_genZoneConfigMap = new(); // zone → row 빠른 조회
    private Vector2 m_genGridScroll;
    private bool[] m_genMissileStages = new bool[10] { false, true, false, true, false, true, false, true, false, true }; // stage1~10 중 미사일 장착할 스테이지
    private bool[] m_genHangerStages  = new bool[10] { false, false, false, false, false, false, false, false, false, true }; // stage1~10 중 격납고 장착할 스테이지 — 켜지면 전체 함선에 슬롯 풀로 장착
    private bool m_genFoldout = false;

    private void DrawEnemyGeneratorTool()
    {
        EditorGUILayout.BeginVertical("box");
        m_genFoldout = EditorGUILayout.Foldout(m_genFoldout, "적 함대 절차적 생성", true, EditorStyles.foldoutHeader);
        if (m_genFoldout)
        {
            EditorGUILayout.HelpBox("zoneStart 이전 데이터(사람이 손으로 짠 기준 데이터)는 건드리지 않습니다.\n대상 존의 zoneStageList가 이미 존재해야 합니다(Import Stage 선행 필요).", MessageType.Info);

            EditorGUI.indentLevel++;
            m_genZoneStart = EditorGUILayout.IntField("Zone Start", m_genZoneStart);
            m_genZoneEnd   = EditorGUILayout.IntField("Zone End",   m_genZoneEnd);
            EditorGUILayout.Space(4);
            m_genMaxTier   = EditorGUILayout.IntField("Max Tier",                m_genMaxTier);
            m_genMaxShips  = EditorGUILayout.IntField("Max Ships Per Fleet",     m_genMaxShips);
            EditorGUILayout.Space(4);
            m_genRandomSeed = EditorGUILayout.IntField("Random Seed (공용)", m_genRandomSeed);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("존별 함체 그레이드 예산 / 상한", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox($"CSV: {k_enemyConfigCSV}\nBudget을 MaxTier만큼씩 깎아 함선을 만들고, 남은 잔여로 마지막 함선을 만듭니다.\n예) Budget16, MaxTier14 → [14, 2] (2척)\nDeviation>0이면 [MaxTier-Deviation, MaxTier] 범위에서 시드 기반으로 선택(같은 시드면 항상 같은 결과).", MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Config CSV")) LoadEnemyConfigCsv();
            if (GUILayout.Button("Save Config CSV")) SaveEnemyConfigCsv();
            EditorGUILayout.EndHorizontal();

            if (m_genZoneConfigs == null) LoadEnemyConfigCsv();

            if (m_genZoneConfigs != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("", GUILayout.Width(EditorGUIUtility.labelWidth));
                GUILayout.Label("Fleets", GUILayout.Width(50));
                GUILayout.Label("Budget", GUILayout.Width(55));
                GUILayout.Label("MaxTier", GUILayout.Width(55));
                GUILayout.Label("Deviation", GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();

                m_genGridScroll = EditorGUILayout.BeginScrollView(m_genGridScroll, GUILayout.Height(300));
                for (int i = 0; i < m_genZoneConfigs.Count; i++)
                {
                    ZoneEnemyConfigRow row = m_genZoneConfigs[i];
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Zone {row.zone}", GUILayout.Width(EditorGUIUtility.labelWidth));
                    row.fleets    = EditorGUILayout.IntField(row.fleets,    GUILayout.Width(50));
                    row.budget    = EditorGUILayout.IntField(row.budget,    GUILayout.Width(55));
                    row.maxTier   = EditorGUILayout.IntField(row.maxTier,   GUILayout.Width(55));
                    row.deviation = EditorGUILayout.IntField(row.deviation, GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.Space(4);
            DrawStageToggleRow("Missile Stages", m_genMissileStages);
            DrawStageToggleRow("Hanger Stages (전체 함선)", m_genHangerStages);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Generate", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("적 함대 절차적 생성",
                    $"zone{m_genZoneStart}~{m_genZoneEnd} 구간의 적 함대 데이터를 재생성합니다.\n계속하시겠습니까?", "Generate", "Cancel"))
                {
                    GenerateEnemyData();
                }
            }
        }
        EditorGUILayout.EndVertical();
    }

    // stage 1~10 토글을 한 줄에 컴팩트하게 표시
    private void DrawStageToggleRow(string label, bool[] stages)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < 10; i++)
            stages[i] = GUILayout.Toggle(stages[i], $"{i + 1}", "Button", GUILayout.Width(24), GUILayout.Height(20));
        EditorGUILayout.EndHorizontal();
    }

    private void LoadEnemyConfigCsv()
    {
        m_genZoneConfigs = new List<ZoneEnemyConfigRow>();
        m_genZoneConfigMap = new Dictionary<int, ZoneEnemyConfigRow>();

        if (File.Exists(k_enemyConfigCSV) == false)
        {
            Debug.LogWarning($"[EnemyGen] {k_enemyConfigCSV} 없음");
            return;
        }

        string[] lines = File.ReadAllLines(k_enemyConfigCSV);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            if (col.Length < 5) continue;
            if (int.TryParse(col[0], out int zone) == false) continue;

            var row = new ZoneEnemyConfigRow
            {
                zone      = zone,
                fleets    = int.TryParse(col[1], out int f) ? f : 1,
                budget    = int.TryParse(col[2], out int b) ? b : 1,
                maxTier   = int.TryParse(col[3], out int m) ? m : 1,
                deviation = int.TryParse(col[4], out int d) ? d : 0,
            };
            m_genZoneConfigs.Add(row);
            m_genZoneConfigMap[zone] = row;
        }
    }

    private void SaveEnemyConfigCsv(bool showDialog = true)
    {
        if (m_genZoneConfigs == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("zone,fleets,budget,max_tier,deviation");
        foreach (ZoneEnemyConfigRow row in m_genZoneConfigs)
            sb.AppendLine($"{row.zone},{row.fleets},{row.budget},{row.maxTier},{row.deviation}");

        File.WriteAllText(k_enemyConfigCSV, sb.ToString(), System.Text.Encoding.UTF8);
        AssetDatabase.Refresh();
        if (showDialog) EditorUtility.DisplayDialog("완료", "Enemy Config CSV 저장 완료", "OK");
    }

    private ZoneEnemyConfigRow GetZoneConfig(int zone)
    {
        if (m_genZoneConfigMap.TryGetValue(zone, out ZoneEnemyConfigRow row))
            return row;
        return new ZoneEnemyConfigRow { zone = zone, fleets = 1, budget = 1, maxTier = 1, deviation = 0 };
    }

    private int GetBlockFleets(int zone) => Mathf.Max(1, GetZoneConfig(zone).fleets);

    private int GetGradeBudget(int zone) => Mathf.Max(1, GetZoneConfig(zone).budget);

    // 시드 기반 [MaxTier-Deviation, MaxTier] 범위 선택 — 같은 시드+zone+stage면 항상 같은 결과(재현 가능한 의사랜덤)
    private int GetBlockMaxTier(int zone, int stage)
    {
        ZoneEnemyConfigRow cfg = GetZoneConfig(zone);
        int baseTier = cfg.maxTier;
        int deviation = Mathf.Max(0, cfg.deviation);

        int tier = baseTier;
        if (deviation > 0)
        {
            var rng = new System.Random(m_genRandomSeed ^ (zone * 73856093) ^ (stage * 19349663));
            int delta = rng.Next(0, deviation + 1); // 0~deviation 포함
            tier = baseTier - delta;
        }

        return Mathf.Clamp(tier, 1, m_genMaxTier);
    }

    // budget을 1~blockMaxTier 사이에서 매번 랜덤하게(시드 기반, 재현 가능) 떼어내 함선을 만듦
    // 예) budget=4, blockMaxTier=2 → [2,2] 또는 [2,1,1] 또는 [1,2,1] 등 시드에 따라 다른 조합
    private List<int> GenGradePartition(int budget, int blockMaxTier, int zone, int stage, int fleetIndex)
    {
        var grades = new List<int>();
        int remaining = budget;
        var rng = new System.Random(m_genRandomSeed ^ (zone * 73856093) ^ (stage * 19349663) ^ (fleetIndex * 83492791));

        while (remaining > 0 && grades.Count < m_genMaxShips - 1)
        {
            int maxPiece = Mathf.Min(remaining, blockMaxTier);
            int grade = rng.Next(1, maxPiece + 1); // 1~maxPiece 포함
            grades.Add(grade);
            remaining -= grade;
        }
        if (remaining > 0)
            grades.Add(Mathf.Min(remaining, blockMaxTier)); // 마지막 잔여(척수 상한 도달 시 blockMaxTier로 클램프)

        if (grades.Count == 0) grades.Add(1);
        grades.Sort((a, b) => b.CompareTo(a)); // 내림차순 — ship_index 0(기함)이 항상 최고 티어
        return grades;
    }

    // 전체 데이터셋 통틀어 zone1 stage1~5에서만 0.4→0.8로 램프업 (호출 범위와 무관하게 고정 zone1 기준), 그 외는 항상 1.0
    private float GenBodyRatio(int zone, int stage)
    {
        if (zone == 1 && stage <= 5)
            return Mathf.Round((0.3f + stage * 0.1f) * 100f) / 100f;
        return 1f;
    }

    private void GenerateEnemyData()
    {
        var stageMap = new Dictionary<string, ZoneStageConfig>();
        for (int i = 0; i < m_dataTableZone.zoneStageList.Count; i++)
        {
            ZoneStageConfig zs = m_dataTableZone.zoneStageList[i];
            stageMap[zs.zoneName] = zs;
        }

        int touched = 0;
        for (int zone = m_genZoneStart; zone <= m_genZoneEnd; zone++)
        {
            int fleets = GetBlockFleets(zone);
            int budget = GetGradeBudget(zone);

            for (int stage = 1; stage <= 10; stage++)
            {
                int blockMaxTier = GetBlockMaxTier(zone, stage);
                string zoneName = $"{zone}-{stage}";
                if (!stageMap.TryGetValue(zoneName, out ZoneStageConfig zs)) continue; // Import Stage 선행 필요

                var enemyFleets = new List<StageEnemyFleetSpawnConfig>();
                for (int fidx = 0; fidx < fleets; fidx++)
                {
                    var ships2 = new List<ShipInfo>();
                    List<int> grades = GenGradePartition(budget, blockMaxTier, zone, stage, fidx); // budget을 1~blockMaxTier 랜덤 분할(시드 기반)로 함선 그레이드 결정

                    for (int sidx = 0; sidx < grades.Count; sidx++)
                    {
                        int shipTier = grades[sidx];
                        int bodyLevel = stage; // 티어 내 레벨은 스테이지 그대로 사용(1~10), 정체 허용
                        if (k_slotCap.TryGetValue(shipTier, out var cap) == false)
                            cap = k_slotCap[14]; // 프리팹에 존재하지 않는 티어 — T14 슬롯으로 대체

                        var beams = new List<ModuleInfo>();
                        for (int b = 0; b < cap.beam; b++) // 주력무기: 슬롯을 항상 풀로 채움
                            beams.Add(new ModuleInfo { moduleType = EModuleType.beam, moduleSubType = ParseSubType($"beam_t{shipTier}_m1"), moduleLevel = bodyLevel, bodyIndex = 0, slotIndex = b });

                        var missiles = new List<ModuleInfo>();
                        bool hasMissile = m_genMissileStages[stage - 1];
                        if (zone <= 2 && stage < 9) hasMissile = false; // zone1~2는 stage9,10에서만 미사일 허용
                        if (hasMissile)
                            for (int m = 0; m < cap.missile; m++)
                                missiles.Add(new ModuleInfo { moduleType = EModuleType.missile, moduleSubType = ParseSubType($"missile_t{shipTier}_m1"), moduleLevel = 1, bodyIndex = 0, slotIndex = m });

                        var hangers = new List<ModuleInfo>();
                        bool hasHanger = m_genHangerStages[stage - 1];
                        if (hasHanger)
                            for (int h = 0; h < cap.hanger; h++) // 켜진 스테이지의 모든 함선에 슬롯 풀로 장착
                                hangers.Add(new ModuleInfo { moduleType = EModuleType.hanger, moduleSubType = ParseSubType($"hanger_t{shipTier}_m1"), moduleLevel = 1, bodyIndex = 0, slotIndex = h });

                        var body = new ModuleBodyInfo
                        {
                            moduleType = EModuleType.body, moduleSubType = ParseSubType($"body_t{shipTier}_m1"), moduleLevel = bodyLevel, bodyIndex = 0,
                            beams = beams, missiles = missiles, hangers = hangers,
                        };

                        float ratio = GenBodyRatio(zone, stage);
                        ships2.Add(new ShipInfo
                        {
                            shipName = $"EnemyShip_{sidx}", positionIndex = sidx,
                            bodyMultiplier = ratio, beamMultiplier = 1f, missileMultiplier = 1f, hangerMultiplier = 1f,
                            bodies = new List<ModuleBodyInfo> { body },
                        });
                    }

                    enemyFleets.Add(new StageEnemyFleetSpawnConfig
                    {
                        fleetIndex = fidx, positionIndex = fidx, // 0번 함대 = 0번 위치 프리셋
                        fleetInfo = new FleetInfo { fleetName = $"{zoneName}_fleet{fidx}", ships = ships2 },
                    });
                }

                zs.enemyFleets = enemyFleets;
                touched++;
            }
        }

        EditorUtility.SetDirty(m_dataTableZone);
        m_dataTableZone.BuildRuntimeCache();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("완료", $"적 함대 생성 완료! ({touched}개 스테이지)", "OK");
    }

    private EModuleSubType ParseSubType(string name)
    {
        return System.Enum.TryParse(name, out EModuleSubType result) ? result : EModuleSubType.none;
    }

    #endregion

    #region 행성 절차적 생성 (zoneList.celestialBodies 직접 수정)

    // 행성 타입 템플릿 — zone1~9의 수작업 팔레트를 기준으로 삼고, 색은 타입 내에서만 hue/sat/val을 살짝 흔들어 재사용
    private class PlanetTypeTemplate
    {
        public string name;
        public Color deepSea, shallowSea, lowlandSand, lowlandGreen, plainsDesert, plainsGrass, plainsForest, highlandSnow;
        public Color iceColor, iceColorEdge, cloudColor, atmosphereColor;
        public float landCoverage, biomeBlend, gBlend;
        public bool  hasPolarIce;
        public float poleIceWidth = 0.12f;
        public bool  hasClouds;
        public float cloudCoverage = 0.5f, cloudMidLatOpacity = 0.5f, cloudMidLatCenter = 0.3f, cloudMidLatWidth = 0.2f, cloudSoftness = 0.4f;
    }

    private static readonly PlanetTypeTemplate[] k_planetTypes = new PlanetTypeTemplate[]
    {
        new PlanetTypeTemplate { name = "지구형(온대)", // Zone 1 기준
            deepSea = CommonUtility.HexColor("#0D2673"), shallowSea = CommonUtility.HexColor("#1959A5"),
            lowlandSand = CommonUtility.HexColor("#BFB380"), lowlandGreen = CommonUtility.HexColor("#90C060"),
            plainsDesert = CommonUtility.HexColor("#A99159"), plainsGrass = CommonUtility.HexColor("#478C2E"), plainsForest = CommonUtility.HexColor("#236523"),
            highlandSnow = CommonUtility.HexColor("#E8F0F5"), iceColor = CommonUtility.HexColor("#F2F9FF"), iceColorEdge = CommonUtility.HexColor("#ADD1EF"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD8"), atmosphereColor = CommonUtility.HexColor("#4C99FF"),
            landCoverage = 0.5f, biomeBlend = 0.1f, gBlend = 0.8f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.5f },

        new PlanetTypeTemplate { name = "지구형(해양)", // Zone 2 기준
            deepSea = CommonUtility.HexColor("#0D2673"), shallowSea = CommonUtility.HexColor("#1A59A6"),
            lowlandSand = CommonUtility.HexColor("#BFB380"), lowlandGreen = CommonUtility.HexColor("#90C060"),
            plainsDesert = CommonUtility.HexColor("#A99159"), plainsGrass = CommonUtility.HexColor("#478C2E"), plainsForest = CommonUtility.HexColor("#236523"),
            highlandSnow = CommonUtility.HexColor("#E8F0F5"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#4D99FF"),
            landCoverage = 0.4f, biomeBlend = 0.07f, gBlend = 0.3f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.2f },

        new PlanetTypeTemplate { name = "용암형", // Zone 3 기준
            deepSea = CommonUtility.HexColor("#D66600"), shallowSea = CommonUtility.HexColor("#F7CA11"),
            lowlandSand = CommonUtility.HexColor("#BFB380"), lowlandGreen = CommonUtility.HexColor("#E3F084"),
            plainsDesert = CommonUtility.HexColor("#A99159"), plainsGrass = CommonUtility.HexColor("#E4DA64"), plainsForest = CommonUtility.HexColor("#C23D25"),
            highlandSnow = CommonUtility.HexColor("#C52225"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#A50000D9"), atmosphereColor = CommonUtility.HexColor("#FCC705"),
            landCoverage = 0.28f, biomeBlend = 0f, gBlend = 0f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.48f },

        new PlanetTypeTemplate { name = "극지형", // Zone 4 기준
            deepSea = CommonUtility.HexColor("#0D2673"), shallowSea = CommonUtility.HexColor("#3E6EAA"),
            lowlandSand = CommonUtility.HexColor("#BFB380"), lowlandGreen = CommonUtility.HexColor("#90C060"),
            plainsDesert = CommonUtility.HexColor("#D6C08E"), plainsGrass = CommonUtility.HexColor("#478C2E"), plainsForest = CommonUtility.HexColor("#5F845F"),
            highlandSnow = CommonUtility.HexColor("#E8F0F5"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#4D99FF"),
            landCoverage = 0.611f, biomeBlend = 0.07f, gBlend = 0.78f, hasPolarIce = true, hasClouds = false },

        new PlanetTypeTemplate { name = "외계(보라)형", // Zone 5 기준
            deepSea = CommonUtility.HexColor("#6F38D9"), shallowSea = CommonUtility.HexColor("#6D19A6"),
            lowlandSand = CommonUtility.HexColor("#590090"), lowlandGreen = CommonUtility.HexColor("#6860C0"),
            plainsDesert = CommonUtility.HexColor("#5968A9"), plainsGrass = CommonUtility.HexColor("#2D5B8C"), plainsForest = CommonUtility.HexColor("#76297A"),
            highlandSnow = CommonUtility.HexColor("#71336A"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#FF4DF6"),
            landCoverage = 0.637f, biomeBlend = 0.09f, gBlend = 1.57f, hasPolarIce = false, hasClouds = false },

        new PlanetTypeTemplate { name = "사막형", // Zone 6 기준
            deepSea = CommonUtility.HexColor("#896C28"), shallowSea = CommonUtility.HexColor("#A89260"),
            lowlandSand = CommonUtility.HexColor("#BB9F5C"), lowlandGreen = CommonUtility.HexColor("#998147"),
            plainsDesert = CommonUtility.HexColor("#B6A170"), plainsGrass = CommonUtility.HexColor("#B27E05"), plainsForest = CommonUtility.HexColor("#6E6144"),
            highlandSnow = CommonUtility.HexColor("#E8F0F5"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#BC9F5D"),
            landCoverage = 0.5f, biomeBlend = 0.1f, gBlend = 0.61f, hasPolarIce = false, hasClouds = false },

        new PlanetTypeTemplate { name = "오션(청록)형", // Zone 7 기준
            deepSea = CommonUtility.HexColor("#0D2673"), shallowSea = CommonUtility.HexColor("#1A59A6"),
            lowlandSand = CommonUtility.HexColor("#576BCC"), lowlandGreen = CommonUtility.HexColor("#0F2156"),
            plainsDesert = CommonUtility.HexColor("#6EB2A8"), plainsGrass = CommonUtility.HexColor("#2D6C8C"), plainsForest = CommonUtility.HexColor("#233665"),
            highlandSnow = CommonUtility.HexColor("#E8F0F5"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#4D99FF"),
            landCoverage = 0.5f, biomeBlend = 0.01f, gBlend = 0.47f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.34f },

        new PlanetTypeTemplate { name = "화성(적색)형", // Zone 8 기준
            deepSea = CommonUtility.HexColor("#73330D"), shallowSea = CommonUtility.HexColor("#A64C19"),
            lowlandSand = CommonUtility.HexColor("#A16A5B"), lowlandGreen = CommonUtility.HexColor("#C06D60"),
            plainsDesert = CommonUtility.HexColor("#A95F59"), plainsGrass = CommonUtility.HexColor("#8C522D"), plainsForest = CommonUtility.HexColor("#653A23"),
            highlandSnow = CommonUtility.HexColor("#D65941"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#D65A42"),
            landCoverage = 0.618f, biomeBlend = 0.01f, gBlend = 0.61f, hasPolarIce = true, hasClouds = false },

        new PlanetTypeTemplate { name = "지구형(심림)", // Zone 9 기준
            deepSea = CommonUtility.HexColor("#0D2673"), shallowSea = CommonUtility.HexColor("#1A59A6"),
            lowlandSand = CommonUtility.HexColor("#265D24"), lowlandGreen = CommonUtility.HexColor("#90C060"),
            plainsDesert = CommonUtility.HexColor("#11540D"), plainsGrass = CommonUtility.HexColor("#478C2E"), plainsForest = CommonUtility.HexColor("#236523"),
            highlandSnow = CommonUtility.HexColor("#E8F0F5"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#4D99FF"),
            landCoverage = 0.578f, biomeBlend = 0.01f, gBlend = 0.68f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.24f },

        new PlanetTypeTemplate { name = "독성/유독형", // 신규
            deepSea = CommonUtility.HexColor("#0D3B1A"), shallowSea = CommonUtility.HexColor("#4CBB17"),
            lowlandSand = CommonUtility.HexColor("#4A5D23"), lowlandGreen = CommonUtility.HexColor("#6FBF3C"),
            plainsDesert = CommonUtility.HexColor("#355E1F"), plainsGrass = CommonUtility.HexColor("#8FD400"), plainsForest = CommonUtility.HexColor("#1B3B0E"),
            highlandSnow = CommonUtility.HexColor("#C8FFB0"), iceColor = CommonUtility.HexColor("#E4FFD0"), iceColorEdge = CommonUtility.HexColor("#B0E890"),
            cloudColor = CommonUtility.HexColor("#7FFF0090"), atmosphereColor = CommonUtility.HexColor("#39FF14"),
            landCoverage = 0.5f, biomeBlend = 0.05f, gBlend = 0.5f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.35f },

        new PlanetTypeTemplate { name = "얼음/동결형", // 신규
            deepSea = CommonUtility.HexColor("#1B3A57"), shallowSea = CommonUtility.HexColor("#6FA8DC"),
            lowlandSand = CommonUtility.HexColor("#DCEEFB"), lowlandGreen = CommonUtility.HexColor("#BFE3F0"),
            plainsDesert = CommonUtility.HexColor("#E8F4FA"), plainsGrass = CommonUtility.HexColor("#D2ECF9"), plainsForest = CommonUtility.HexColor("#A9CEDC"),
            highlandSnow = CommonUtility.HexColor("#FFFFFF"), iceColor = CommonUtility.HexColor("#FFFFFF"), iceColorEdge = CommonUtility.HexColor("#CFE9FF"),
            cloudColor = CommonUtility.HexColor("#FFFFFFE0"), atmosphereColor = CommonUtility.HexColor("#CFE9FF"),
            landCoverage = 0.8f, biomeBlend = 0.05f, gBlend = 0.5f, hasPolarIce = true, poleIceWidth = 0.3f, hasClouds = true, cloudCoverage = 0.3f },

        new PlanetTypeTemplate { name = "가스/거인형", // 신규 (용암형과 같은 셰이더를 색만 바꿔 밴드 느낌)
            deepSea = CommonUtility.HexColor("#D9A441"), shallowSea = CommonUtility.HexColor("#E8C170"),
            lowlandSand = CommonUtility.HexColor("#C97B3D"), lowlandGreen = CommonUtility.HexColor("#E0B080"),
            plainsDesert = CommonUtility.HexColor("#B5651D"), plainsGrass = CommonUtility.HexColor("#D2955B"), plainsForest = CommonUtility.HexColor("#8B4A1E"),
            highlandSnow = CommonUtility.HexColor("#F5E1C8"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFF3D9C0"), atmosphereColor = CommonUtility.HexColor("#FFD27F"),
            landCoverage = 0.5f, biomeBlend = 0.15f, gBlend = 2.0f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.6f },
    };

    private int  m_genPlanetZoneStart = 10;
    private int  m_genPlanetZoneEnd   = 100;
    private int  m_genPlanetSeed      = 20260709;
    private bool m_genPlanetFoldout   = false;

    private void DrawPlanetGeneratorTool()
    {
        EditorGUILayout.BeginVertical("box");
        m_genPlanetFoldout = EditorGUILayout.Foldout(m_genPlanetFoldout, "행성 절차적 생성", true, EditorStyles.foldoutHeader);
        if (m_genPlanetFoldout)
        {
            EditorGUILayout.HelpBox($"zoneStart~zoneEnd 구간의 zoneList[].celestialBodies[0]을 타입 템플릿({k_planetTypes.Length}종) 중 하나로 재생성합니다.\n" +
                "타입은 존마다 시드 기반으로 하나 골라 그 팔레트 안에서만 hue/채도/명도를 살짝 흔듭니다(완전 랜덤 RGB 아님).", MessageType.Info);

            EditorGUI.indentLevel++;
            m_genPlanetZoneStart = EditorGUILayout.IntField("Zone Start", m_genPlanetZoneStart);
            m_genPlanetZoneEnd   = EditorGUILayout.IntField("Zone End",   m_genPlanetZoneEnd);
            m_genPlanetSeed      = EditorGUILayout.IntField("Random Seed", m_genPlanetSeed);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Generate", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("행성 절차적 생성",
                    $"zone{m_genPlanetZoneStart}~{m_genPlanetZoneEnd} 구간의 행성 데이터를 재생성합니다.\n계속하시겠습니까?", "Generate", "Cancel"))
                {
                    GeneratePlanetData();
                }
            }
        }
        EditorGUILayout.EndVertical();
    }

    private Color JitterColor(Color c, System.Random rng, float hueJitter, float satJitter, float valJitter)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        h = Mathf.Repeat(h + ((float)rng.NextDouble() * 2f - 1f) * hueJitter, 1f);
        s = Mathf.Clamp01(s + ((float)rng.NextDouble() * 2f - 1f) * satJitter);
        v = Mathf.Clamp01(v + ((float)rng.NextDouble() * 2f - 1f) * valJitter);
        Color result = Color.HSVToRGB(h, s, v);
        result.a = c.a;
        return result;
    }

    // base 주변 ±range로 흔들고 min~max로 clamp (완전 랜덤이 아니라 타입의 base 수치 근처로만 변형)
    private float JitterFloat(float baseValue, float range, System.Random rng, float min, float max)
    {
        float v = baseValue + ((float)rng.NextDouble() * 2f - 1f) * range;
        return Mathf.Clamp(v, min, max);
    }

    private void GeneratePlanetData()
    {
        var zoneMap = new Dictionary<int, ZoneConfig>();
        for (int i = 0; i < m_dataTableZone.zoneList.Count; i++)
            zoneMap[m_dataTableZone.zoneList[i].zoneIndex] = m_dataTableZone.zoneList[i];

        int touched = 0;
        for (int zone = m_genPlanetZoneStart; zone <= m_genPlanetZoneEnd; zone++)
        {
            if (!zoneMap.TryGetValue(zone, out ZoneConfig zoneConfig))
            {
                zoneConfig = new ZoneConfig { zoneIndex = zone };
                m_dataTableZone.zoneList.Add(zoneConfig);
                zoneMap[zone] = zoneConfig;
            }

            var rng = new System.Random(m_genPlanetSeed ^ (zone * 73856093));
            PlanetTypeTemplate type = k_planetTypes[rng.Next(0, k_planetTypes.Length)];

            var body = new CelestialBodyConfig
            {
                position = Vector3.zero,
                rotation = new Vector3(JitterFloat(0f, 20f, rng, -40f, 40f), rng.Next(0, 360), JitterFloat(0f, 20f, rng, -40f, 40f)),
                scale    = new Vector3(500f, 500f, 500f),

                deepSeaColor      = JitterColor(type.deepSea,      rng, 0.03f, 0.08f, 0.08f),
                shallowSeaColor   = JitterColor(type.shallowSea,   rng, 0.03f, 0.08f, 0.08f),
                lowlandSandColor  = JitterColor(type.lowlandSand,  rng, 0.03f, 0.08f, 0.08f),
                lowlandGreenColor = JitterColor(type.lowlandGreen, rng, 0.03f, 0.08f, 0.08f),
                plainsDesertColor = JitterColor(type.plainsDesert, rng, 0.03f, 0.08f, 0.08f),
                plainsGrassColor  = JitterColor(type.plainsGrass,  rng, 0.03f, 0.08f, 0.08f),
                plainsForestColor = JitterColor(type.plainsForest, rng, 0.03f, 0.08f, 0.08f),
                highlandSnowColor = JitterColor(type.highlandSnow, rng, 0.03f, 0.05f, 0.05f),

                landCoverage = JitterFloat(type.landCoverage, 0.1f, rng, 0f, 1f),
                biomeBlend   = JitterFloat(type.biomeBlend,   0.02f, rng, 0f, 0.2f),
                gBlend       = JitterFloat(type.gBlend,       0.2f, rng, 0f, 5f),

                hasPolarIce  = type.hasPolarIce,
                iceColor     = JitterColor(type.iceColor,     rng, 0.02f, 0.05f, 0.05f),
                iceColorEdge = JitterColor(type.iceColorEdge, rng, 0.02f, 0.05f, 0.05f),
                poleIceWidth = type.poleIceWidth,

                hasClouds          = type.hasClouds,
                cloudColor         = JitterColor(type.cloudColor, rng, 0.02f, 0.05f, 0.05f),
                cloudCoverage      = JitterFloat(type.cloudCoverage, 0.15f, rng, 0f, 1f),
                cloudRotation      = rng.Next(0, 360),
                cloudScale         = 1.01f,
                cloudMidLatOpacity = type.cloudMidLatOpacity,
                cloudMidLatCenter  = type.cloudMidLatCenter,
                cloudMidLatWidth   = type.cloudMidLatWidth,
                cloudSoftness      = type.cloudSoftness,

                hasAtmosphere   = true,
                atmosphereColor = JitterColor(type.atmosphereColor, rng, 0.02f, 0.05f, 0.05f),
                atmosphereScale = 1.01f,
            };

            zoneConfig.celestialBodies = new List<CelestialBodyConfig> { body };
            touched++;
        }

        EditorUtility.SetDirty(m_dataTableZone);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("완료", $"행성 생성 완료! ({touched}개 존)", "OK");
    }

    #endregion

    #region 함대 스폰 위치 일괄 자동 배치 (존별 "함대 위치 자동 배치" 도구를 zoneStart~zoneEnd 전체에 반복 적용)

    private int  m_genFleetZoneStart  = 1;
    private int  m_genFleetZoneEnd    = 100;
    private int  m_genFleetSeed       = 20260709;
    private float m_genFleetXRange    = 11000f;
    private float m_genFleetZRange    = 5000f;
    private float m_genFleetMinZGap   = 2500f;
    private int  m_genFleetStageCount = 10;
    private bool m_genFleetFoldout    = false;

    private void DrawFleetPositionBatchGeneratorTool()
    {
        EditorGUILayout.BeginVertical("box");
        m_genFleetFoldout = EditorGUILayout.Foldout(m_genFleetFoldout, "함대 스폰 위치 일괄 자동 배치", true, EditorStyles.foldoutHeader);
        if (m_genFleetFoldout)
        {
            EditorGUILayout.HelpBox("zoneStart~zoneEnd 구간의 각 Zone에 대해 '함대 위치 자동 배치'를 반복 실행합니다.\n" +
                "존마다 Seed에 zoneIndex를 섞어 서로 다른 배치를 만듭니다. 해당 Zone의 기존 스테이지는 전체 교체됩니다.", MessageType.Info);

            EditorGUI.indentLevel++;
            m_genFleetZoneStart  = EditorGUILayout.IntField("Zone Start", m_genFleetZoneStart);
            m_genFleetZoneEnd    = EditorGUILayout.IntField("Zone End",   m_genFleetZoneEnd);
            m_genFleetSeed       = EditorGUILayout.IntField("Random Seed (공용)", m_genFleetSeed);
            EditorGUILayout.Space(4);
            m_genFleetXRange     = EditorGUILayout.FloatField("X Range",   m_genFleetXRange);
            m_genFleetZRange     = EditorGUILayout.FloatField("Z Range",   m_genFleetZRange);
            m_genFleetMinZGap    = EditorGUILayout.FloatField("Min Z Gap", m_genFleetMinZGap);
            m_genFleetStageCount = EditorGUILayout.IntSlider("스테이지 수", m_genFleetStageCount, 1, 10);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Generate", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("함대 스폰 위치 일괄 자동 배치",
                    $"zone{m_genFleetZoneStart}~{m_genFleetZoneEnd} 구간의 스테이지를 각각 {m_genFleetStageCount}개로 전체 교체합니다.\n계속하시겠습니까?", "Generate", "Cancel"))
                {
                    GenerateFleetPositionsBatch();
                }
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void GenerateFleetPositionsBatch()
    {
        int touched = 0;
        for (int zone = m_genFleetZoneStart; zone <= m_genFleetZoneEnd; zone++)
        {
            int zoneSeed = m_genFleetSeed ^ (zone * 73856093);
            GenerateFleetPositions(zone, zoneSeed, m_genFleetXRange, m_genFleetZRange, m_genFleetMinZGap, m_genFleetStageCount);
            touched++;
        }

        EditorUtility.DisplayDialog("완료", $"함대 스폰 위치 일괄 생성 완료! ({touched}개 존)", "OK");
    }

    #endregion

    private void DrawZoneList()
    {
        // x-y 형식인 경우 x(그룹 인덱스)로 그룹핑
        var tempZoneList = new Dictionary<int, List<(int index, ZoneStageConfig zoneStage)>>();

        for (int i = 0; i < m_dataTableZone.zoneStageList.Count; i++)
        {
            var zoneStage = m_dataTableZone.zoneStageList[i];
            int zoneIndex = zoneStage.zoneIndex;//ParseZoneIndexFromZoneName(zoneStage.zoneName);

            if (!tempZoneList.ContainsKey(zoneIndex))
                tempZoneList[zoneIndex] = new List<(int, ZoneStageConfig)>();
            tempZoneList[zoneIndex].Add((i, zoneStage));
        }

        foreach (var zone in tempZoneList.OrderBy(g => g.Key))
            DrawZoneGroup(zone.Key, zone.Value);
    }

    // 그룹 인덱스별 그룹 그리기
    private void DrawZoneGroup(int zoneIndex, List<(int index, ZoneStageConfig zoneStage)> zoneStageList)
    {
        if (!zoneGroupFoldouts.ContainsKey(zoneIndex))
            zoneGroupFoldouts[zoneIndex] = false;

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.8f, 0.8f, 0.95f);
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        zoneGroupFoldouts[zoneIndex] = EditorGUILayout.Foldout(
            zoneGroupFoldouts[zoneIndex],
            $"Zone{zoneIndex}  ({zoneIndex}-1 ~ {zoneIndex}-{zoneStageList.Count})",
            true, EditorStyles.foldoutHeader);

        if (zoneGroupFoldouts[zoneIndex])
        {
            EditorGUI.indentLevel++;

            // 그룹 공유 설정 (스카이박스)
            DrawZoneConfig(zoneIndex);
            EditorGUILayout.Space(5);

            foreach (var (stageIndex, zoneStage) in zoneStageList)
            {
                DrawZoneStage(stageIndex, zoneStage.zoneName);
                EditorGUILayout.Space(3);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    // ZoneConfig 편집 UI (스카이박스 + 갤럭시 카메라 앵커)
    private void DrawZoneConfig(int zoneIndex)
    {
        ZoneConfig zoneConfig = null;
        for (int i = 0; i < m_dataTableZone.zoneList.Count; i++)
        {
            if (m_dataTableZone.zoneList[i].zoneIndex == zoneIndex)
            {
                zoneConfig = m_dataTableZone.zoneList[i];
                break;
            }
        }

        if (zoneConfig == null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("이 그룹의 스카이박스 설정이 없습니다.", MessageType.Info);
            if (GUILayout.Button("생성", GUILayout.Width(50)))
            {
                zoneConfig = new ZoneConfig { zoneIndex = zoneIndex };
                m_dataTableZone.zoneList.Add(zoneConfig);
                EditorUtility.SetDirty(m_dataTableZone);
            }
            EditorGUILayout.EndHorizontal();
            return;
        }

        EditorGUI.BeginChangeCheck();

        if (!m_cameraAnchorFoldouts.ContainsKey(zoneIndex)) m_cameraAnchorFoldouts[zoneIndex] = false;
        m_cameraAnchorFoldouts[zoneIndex] = EditorGUILayout.Foldout(m_cameraAnchorFoldouts[zoneIndex], "갤럭시 뷰 카메라 앵커", true, EditorStyles.foldoutHeader);
        if (m_cameraAnchorFoldouts[zoneIndex] == true)
        {
            EditorGUI.indentLevel++;
            zoneConfig.galaxyCameraTarget = EditorGUILayout.Vector3Field("Camera Target", zoneConfig.galaxyCameraTarget);
            zoneConfig.galaxyCameraZoom   = EditorGUILayout.FloatField("Camera Zoom", zoneConfig.galaxyCameraZoom);
            zoneConfig.galaxyCameraRotX   = EditorGUILayout.Slider("Rot X (앙각)", zoneConfig.galaxyCameraRotX, -80f, 80f);
            zoneConfig.galaxyCameraRotY   = EditorGUILayout.FloatField("Rot Y (수평)", zoneConfig.galaxyCameraRotY);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        if (!m_celestialFoldouts.ContainsKey(zoneIndex)) m_celestialFoldouts[zoneIndex] = false;
        m_celestialFoldouts[zoneIndex] = EditorGUILayout.Foldout(m_celestialFoldouts[zoneIndex], "천체 배치", true, EditorStyles.foldoutHeader);
        if (m_celestialFoldouts[zoneIndex] == true)
        {
            if (zoneConfig.celestialBodies == null)
                zoneConfig.celestialBodies = new System.Collections.Generic.List<CelestialBodyConfig>();

            EditorGUI.indentLevel++;
            CelestialBodyEditorGUI.DrawCelestialBodyList(zoneIndex, zoneConfig.celestialBodies, m_dataTableZone);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);
        DrawAutoPlacementUI(zoneIndex);

        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(m_dataTableZone);
    }

    private void DrawAutoPlacementUI(int zoneIndex)
    {
        if (!m_autoGenSeed.ContainsKey(zoneIndex))        m_autoGenSeed[zoneIndex]       = zoneIndex * 100;
        if (!m_autoGenXRange.ContainsKey(zoneIndex))     m_autoGenXRange[zoneIndex]     = 11000f;
        if (!m_autoGenZRange.ContainsKey(zoneIndex))     m_autoGenZRange[zoneIndex]     = 5000f;
        if (!m_autoGenMinZGap.ContainsKey(zoneIndex))    m_autoGenMinZGap[zoneIndex]    = 1500f;
        if (!m_autoGenStageCount.ContainsKey(zoneIndex)) m_autoGenStageCount[zoneIndex] = 10;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("함대 위치 자동 배치", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        m_autoGenSeed[zoneIndex]        = EditorGUILayout.IntField("Seed",        m_autoGenSeed[zoneIndex]);
        m_autoGenXRange[zoneIndex]      = EditorGUILayout.FloatField("X Range",   m_autoGenXRange[zoneIndex]);
        m_autoGenZRange[zoneIndex]      = EditorGUILayout.FloatField("Z Range",   m_autoGenZRange[zoneIndex]);
        m_autoGenMinZGap[zoneIndex]     = EditorGUILayout.FloatField("Min Z Gap", m_autoGenMinZGap[zoneIndex]);
        m_autoGenStageCount[zoneIndex]  = EditorGUILayout.IntSlider("스테이지 수",  m_autoGenStageCount[zoneIndex],  1, 10);
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(2);
        if (GUILayout.Button("생성 (기존 스테이지 전체 교체)"))
        {
            bool confirm = EditorUtility.DisplayDialog(
                "함대 위치 자동 배치",
                $"Zone {zoneIndex}의 스테이지를 {m_autoGenStageCount[zoneIndex]}개로 전체 교체합니다.\n계속하시겠습니까?",
                "생성", "취소");
            if (confirm == true)
            {
                GenerateFleetPositions(zoneIndex, m_autoGenSeed[zoneIndex],
                    m_autoGenXRange[zoneIndex], m_autoGenZRange[zoneIndex],
                    m_autoGenMinZGap[zoneIndex], m_autoGenStageCount[zoneIndex]);
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void GenerateFleetPositions(int zoneIndex, int seed, float xRange, float zRange, float minZGap, int stageCount)
    {
        // 기존 스테이지 맵 수집 (fleetPosition/Rotation 외 데이터 보존용)
        var existingMap = new Dictionary<string, ZoneStageConfig>();
        for (int j = 0; j < m_dataTableZone.zoneStageList.Count; j++)
        {
            ZoneStageConfig zs = m_dataTableZone.zoneStageList[j];
            if (zs.zoneIndex == zoneIndex) existingMap[zs.zoneName] = zs;
        }

        // 위치 계산
        int N = stageCount;
        var rng = new System.Random(seed);
        float prevZ = float.MaxValue;

        for (int i = 0; i < N; i++)
        {
            float x = N == 1 ? 0f : Mathf.Lerp(-xRange, xRange, (float)i / (N - 1));

            float z = 0f;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                z = (float)(rng.NextDouble() * 2.0 - 1.0) * zRange;
                if (prevZ == float.MaxValue || Mathf.Abs(z - prevZ) >= minZGap)
                    break;
            }
            prevZ = z;

            string stageName = $"{zoneIndex}-{i + 1}";
            if (existingMap.TryGetValue(stageName, out ZoneStageConfig existing))
            {
                // 기존 스테이지 — fleetPosition/Rotation만 업데이트
                existing.fleetPosition  = new Vector3(x, 0f, z);
                existing.fleetRotationY = 0f;
            }
            else
            {
                // 새 스테이지 추가 (enemyFleets 기본값)
                m_dataTableZone.zoneStageList.Add(new ZoneStageConfig
                {
                    zoneName          = stageName,
                    zoneDescription   = $"Zone {stageName}",
                    zoneIndex         = zoneIndex,
                    spawnTerm         = 20f,
                    fleetPosition     = new Vector3(x, 0f, z),
                    enemyFleets       = new List<StageEnemyFleetSpawnConfig>(),
                });
            }
        }

        m_dataTableZone.zoneStageList.Sort((a, b) =>
        {
            int zoneCmp = a.zoneIndex.CompareTo(b.zoneIndex);
            if (zoneCmp != 0) return zoneCmp;
            int dashA  = a.zoneName.IndexOf('-');
            int stageA = dashA >= 0 && int.TryParse(a.zoneName[(dashA + 1)..], out int sa) ? sa : 0;
            int dashB  = b.zoneName.IndexOf('-');
            int stageB = dashB >= 0 && int.TryParse(b.zoneName[(dashB + 1)..], out int sb) ? sb : 0;
            return stageA.CompareTo(stageB);
        });

        m_dataTableZone.BuildRuntimeCache();
        EditorUtility.SetDirty(m_dataTableZone);
        Debug.Log($"[AutoPlace] Zone {zoneIndex}: {N}개 스테이지 fleetPosition 생성 완료 (seed={seed})");
    }

    private void DrawZoneStage(int listIndex, string zoneName)
    {
        var zoneStage = m_dataTableZone.zoneStageList[listIndex];
        if (!zoneFoldouts.ContainsKey(zoneName))
            zoneFoldouts[zoneName] = false;

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = zoneColor;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        // Zone Header
        EditorGUILayout.BeginHorizontal();
        int totalShipCount = 0;
        if (zoneStage.enemyFleets != null)
            for (int fi = 0; fi < zoneStage.enemyFleets.Count; fi++)
                if (zoneStage.enemyFleets[fi].fleetInfo != null && zoneStage.enemyFleets[fi].fleetInfo.ships != null)
                    totalShipCount += zoneStage.enemyFleets[fi].fleetInfo.ships.Count;
        zoneFoldouts[zoneName] = EditorGUILayout.Foldout(zoneFoldouts[zoneName],
            $"Zone {zoneStage.zoneName} (Fleets: {zoneStage.enemyFleets.Count}, Ships: {totalShipCount})", true, EditorStyles.foldoutHeader);

        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            if (EditorUtility.DisplayDialog("Delete Zone", $"Delete '{zoneStage.zoneName}'?", "Delete", "Cancel"))
            {
                m_dataTableZone.zoneStageList.RemoveAt(listIndex);
                EditorUtility.SetDirty(m_dataTableZone);
                return;
            }
        }
        EditorGUILayout.EndHorizontal();

        if (zoneFoldouts[zoneName])
        {
            EditorGUI.indentLevel++;

            // Zone Info
            EditorGUILayout.BeginVertical("box");
            zoneStage.zoneName = EditorGUILayout.TextField("Zone Name", zoneStage.zoneName);
            zoneStage.zoneDescription = EditorGUILayout.TextField("Description", zoneStage.zoneDescription);
            EditorGUILayout.EndVertical();

            // 시간당 자원 수확량
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("클리어 보상", EditorStyles.boldLabel);
            zoneStage.mineralClearReward     = EditorGUILayout.IntField("Mineral (매 클리어)",      zoneStage.mineralClearReward);
            zoneStage.expClearReward         = EditorGUILayout.IntField("Exp (매 클리어)",          zoneStage.expClearReward);
            zoneStage.modulePointClearReward = EditorGUILayout.IntField("ModulePoint (최초 클리어)", zoneStage.modulePointClearReward);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 전투 설정
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("전투 설정", EditorStyles.boldLabel);
            zoneStage.spawnTerm             = EditorGUILayout.Slider("함대 등장 간격 (초, 2번째 함대부터)", zoneStage.spawnTerm, 0f, 60f);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("발사 시작 딜레이 (0 = 즉시)", EditorStyles.miniLabel);
            zoneStage.playerFireDelaySec    = EditorGUILayout.Slider("아군 발사 딜레이 (초)", zoneStage.playerFireDelaySec, 0f, 10f);
            zoneStage.enemyFireDelaySec     = EditorGUILayout.Slider("적군 발사 딜레이 (초)", zoneStage.enemyFireDelaySec,  0f, 10f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 아군 함대 위치
            EditorGUILayout.BeginVertical("box");
            zoneStage.fleetPosition = EditorGUILayout.Vector3Field("Fleet Position", zoneStage.fleetPosition);
            zoneStage.fleetRotationY = EditorGUILayout.Slider("Fleet Rotation Y", zoneStage.fleetRotationY, 0f, 360f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 적 함대 목록 (복수 웨이브)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"적 함대 ({zoneStage.enemyFleets.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add Fleet", GUILayout.Width(100)))
            {
                if (zoneStage.enemyFleets == null) zoneStage.enemyFleets = new List<StageEnemyFleetSpawnConfig>();
                int nextIdx = zoneStage.enemyFleets.Count;
                zoneStage.enemyFleets.Add(new StageEnemyFleetSpawnConfig
                {
                    fleetIndex    = nextIdx,
                    positionIndex = nextIdx,
                    fleetInfo     = new FleetInfo { fleetName = $"{zoneStage.zoneName}_fleet{nextIdx}", ships = new List<ShipInfo>() }
                });
                EditorUtility.SetDirty(m_dataTableZone);
            }
            EditorGUILayout.EndHorizontal();

            if (zoneStage.enemyFleets != null)
            {
                for (int fi = 0; fi < zoneStage.enemyFleets.Count; fi++)
                    DrawEnemyFleetSpawn(zoneName, fi, zoneStage.enemyFleets[fi], zoneStage);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private Dictionary<string, bool> m_fleetFoldouts = new Dictionary<string, bool>();

    private void DrawEnemyFleetSpawn(string zoneName, int fleetIdx, StageEnemyFleetSpawnConfig fleetSpawn, ZoneStageConfig zoneStage)
    {
        string foldKey = $"{zoneName}_fleet{fleetIdx}";
        if (!m_fleetFoldouts.ContainsKey(foldKey)) m_fleetFoldouts[foldKey] = false;

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = multiplierColor;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        EditorGUILayout.BeginHorizontal();
        int shipCount = fleetSpawn.fleetInfo != null && fleetSpawn.fleetInfo.ships != null ? fleetSpawn.fleetInfo.ships.Count : 0;
        m_fleetFoldouts[foldKey] = EditorGUILayout.Foldout(m_fleetFoldouts[foldKey],
            $"Fleet [{fleetSpawn.fleetIndex}]  term:{fleetSpawn.fleetIndex * zoneStage.spawnTerm}s  pos:{fleetSpawn.positionIndex}  ({shipCount} ships)", true);
        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            zoneStage.enemyFleets.RemoveAt(fleetIdx);
            EditorUtility.SetDirty(m_dataTableZone);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (m_fleetFoldouts[foldKey] == true)
        {
            EditorGUI.indentLevel++;
            fleetSpawn.fleetIndex    = EditorGUILayout.IntField("Fleet Index", fleetSpawn.fleetIndex);
            fleetSpawn.positionIndex = EditorGUILayout.IntField("Position Index", fleetSpawn.positionIndex);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"함선 ({shipCount})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add Ship", GUILayout.Width(100)))
            {
                if (fleetSpawn.fleetInfo == null)
                    fleetSpawn.fleetInfo = new FleetInfo { fleetName = $"{zoneName}_fleet{fleetIdx}", ships = new List<ShipInfo>() };
                var body = new ModuleBodyInfo { moduleType = EModuleType.body, moduleSubType = EModuleSubType.body_t1_m1, moduleLevel = 1, bodyIndex = 0, beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangers = new List<ModuleInfo>() };
                fleetSpawn.fleetInfo.ships.Add(new ShipInfo { shipName = $"EnemyShip_{shipCount}", positionIndex = shipCount, bodyMultiplier = 1f, beamMultiplier = 1f, missileMultiplier = 1f, hangerMultiplier = 1f, bodies = new List<ModuleBodyInfo> { body } });
                EditorUtility.SetDirty(m_dataTableZone);
            }
            EditorGUILayout.EndHorizontal();

            if (fleetSpawn.fleetInfo != null && fleetSpawn.fleetInfo.ships != null)
            {
                for (int si = 0; si < fleetSpawn.fleetInfo.ships.Count; si++)
                    DrawShips(foldKey, si, fleetSpawn.fleetInfo.ships[si], fleetSpawn);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawShips(string zoneName, int shipIndex, ShipInfo ship, StageEnemyFleetSpawnConfig fleetSpawn)
    {
        if (!shipFoldouts.ContainsKey(zoneName))
            shipFoldouts[zoneName] = new Dictionary<int, bool>();
        if (!shipFoldouts[zoneName].ContainsKey(shipIndex))
            shipFoldouts[zoneName][shipIndex] = false;

        ModuleBodyInfo body = (ship.bodies != null && ship.bodies.Count > 0) ? ship.bodies[0] : null;

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = shipColor;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        EditorGUILayout.BeginHorizontal();
        EModuleSubType dispSubType = body != null ? body.moduleSubType : EModuleSubType.none;
        int dispLevel = body != null ? body.moduleLevel : 0;
        shipFoldouts[zoneName][shipIndex] = EditorGUILayout.Foldout(
            shipFoldouts[zoneName][shipIndex],
            $"Ship {shipIndex + 1} [idx:{ship.positionIndex}]: {dispSubType} Lv.{dispLevel}", true);

        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            fleetSpawn.fleetInfo.ships.RemoveAt(shipIndex);
            EditorUtility.SetDirty(m_dataTableZone);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (shipFoldouts[zoneName][shipIndex])
        {
            EditorGUI.indentLevel++;

            int newPosIndex = EditorGUILayout.IntField("Ship Index (진형 슬롯)", ship.positionIndex);
            if (newPosIndex != ship.positionIndex) { ship.positionIndex = newPosIndex; EditorUtility.SetDirty(m_dataTableZone); }

            if (body != null)
            {
                EditorGUILayout.LabelField("Body", EditorStyles.boldLabel);
                int bodyTypeIdx = System.Array.IndexOf(bodySubTypes, body.moduleSubType);
                if (bodyTypeIdx < 0) bodyTypeIdx = 0;
                int newBodyTypeIdx = EditorGUILayout.Popup("Body Type", bodyTypeIdx, bodySubTypeNames);
                if (newBodyTypeIdx != bodyTypeIdx) { body.moduleSubType = bodySubTypes[newBodyTypeIdx]; EditorUtility.SetDirty(m_dataTableZone); }

                int newLevel = EditorGUILayout.IntSlider("Body Level", body.moduleLevel, 1, 10);
                if (newLevel != body.moduleLevel) { body.moduleLevel = newLevel; EditorUtility.SetDirty(m_dataTableZone); }

                EditorGUILayout.Space(5);
                DrawModuleSlots(body);
            }

            EditorGUILayout.Space(5);

            var origColor = GUI.backgroundColor;
            GUI.backgroundColor = multiplierColor;
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = origColor;
            EditorGUILayout.LabelField("스탯 배율  (1.0 = 플레이어 동일)", EditorStyles.boldLabel);
            ship.bodyMultiplier    = EditorGUILayout.Slider("Body    (체력)",            ship.bodyMultiplier,    0.1f, 3.0f);
            ship.beamMultiplier    = EditorGUILayout.Slider("Beam    (공격력·체력)",     ship.beamMultiplier,    0.1f, 3.0f);
            ship.missileMultiplier = EditorGUILayout.Slider("Missile (공격력·체력)",     ship.missileMultiplier, 0.1f, 3.0f);
            ship.hangerMultiplier  = EditorGUILayout.Slider("Hanger  (함재기 전 스탯)", ship.hangerMultiplier,  0.1f, 3.0f);
            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawModuleSlots(ModuleBodyInfo body)
    {
        if (body.beams    == null) body.beams    = new List<ModuleInfo>();
        if (body.missiles == null) body.missiles = new List<ModuleInfo>();
        if (body.hangers  == null) body.hangers  = new List<ModuleInfo>();

        DrawSlotGroup("Beam",    body.beams,    EModuleType.beam);
        DrawSlotGroup("Missile", body.missiles, EModuleType.missile);
        DrawSlotGroup("Hanger",  body.hangers,  EModuleType.hanger);
    }

    private void DrawSlotGroup(string groupName, List<ModuleInfo> slots, EModuleType moduleType)
    {
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = slotColor;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        EditorGUILayout.LabelField($"{groupName} ({slots.Count})", EditorStyles.boldLabel);

        if (slots.Count == 0)
        {
            EditorGUILayout.LabelField("— 없음 —", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        var subTypes     = GetSubTypesForModuleType(moduleType);
        var subTypeNames = subTypes.Select(t => t.ToString()).ToArray();

        foreach (var slot in slots)
        {
            EditorGUILayout.BeginVertical("box");

            int currentIndex = System.Array.IndexOf(subTypes, slot.moduleSubType);
            if (currentIndex < 0) currentIndex = 0;
            int newIndex = EditorGUILayout.Popup(currentIndex, subTypeNames);
            if (newIndex != currentIndex) { slot.moduleSubType = subTypes[newIndex]; EditorUtility.SetDirty(m_dataTableZone); }

            EditorGUI.BeginDisabledGroup(slot.moduleSubType == EModuleSubType.none);
            int newLevel = EditorGUILayout.IntSlider("Level", slot.moduleLevel, 1, 10);
            if (newLevel != slot.moduleLevel) { slot.moduleLevel = newLevel; EditorUtility.SetDirty(m_dataTableZone); }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
    }

    private EModuleSubType[] GetSubTypesForModuleType(EModuleType moduleType)
    {
        var subTypes = new List<EModuleSubType> { EModuleSubType.none };
        foreach (EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if ((EModuleType)subType.GetModuleType() == moduleType)
                subTypes.Add(subType);
        }
        return subTypes.ToArray();
    }
}
#endif
