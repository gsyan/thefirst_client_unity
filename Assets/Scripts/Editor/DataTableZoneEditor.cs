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

        DrawImportExportRow("Zone Camera",  ImportCamera,    () => { DataTableZoneCSVUtility.ExportZone(m_dataTableZone);     AssetDatabase.Refresh(); });
        DrawImportExportRow("Celestial",    ImportCelestial, () => { DataTableZoneCSVUtility.ExportCelestial(m_dataTableZone); AssetDatabase.Refresh(); });
        DrawImportExportRow("Stage",        ImportStage,     () => { DataTableZoneCSVUtility.ExportZoneStage(m_dataTableZone); AssetDatabase.Refresh(); });
        DrawImportExportRow("Enemy",        ImportEnemy,     () => { DataTableZoneCSVUtility.ExportEnemy(m_dataTableZone);     AssetDatabase.Refresh(); });

        EditorGUILayout.EndVertical();
    }

    private void DrawImportExportRow(string label, System.Action onImport, System.Action onExport)
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button($"Import {label}")) onImport();
        if (GUILayout.Button($"Export {label}")) onExport();
        EditorGUILayout.EndHorizontal();
    }

    private static readonly string k_cameraCSV    = "Assets/Resources/DataTable/Zone/datatable_zone_camera.csv";
    private static readonly string k_celestialCSV = "Assets/Resources/DataTable/Zone/datatable_zone_celestial.csv";
    private static readonly string k_stageCSV     = "Assets/Resources/DataTable/Zone/datatable_zone_stage.csv";
    private static readonly string k_enemyCSV     = "Assets/Resources/DataTable/Zone/datatable_zone_enemy.csv";

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
        EditorUtility.DisplayDialog("완료", $"Zone Camera import 완료 ({m_dataTableZone.zoneList.Count}개)", "OK");
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
        EditorUtility.DisplayDialog("완료", "Celestial import 완료", "OK");
    }

    private void ImportStage()
    {
        if (!File.Exists(k_stageCSV)) { EditorUtility.DisplayDialog("Error", $"파일 없음:\n{k_stageCSV}", "OK"); return; }

        // 기존 enemyFleet 보존
        var enemyBackup = new Dictionary<string, FleetInfo>();
        for (int j = 0; j < m_dataTableZone.zoneStageList.Count; j++)
        {
            ZoneStageConfig zs = m_dataTableZone.zoneStageList[j];
            if (zs.enemyFleet != null) enemyBackup[zs.zoneName] = zs.enemyFleet;
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
            int.TryParse(col.Length > 3 ? col[3] : "0", out int techPt);
            int.TryParse(col.Length > 4 ? col[4] : "0", out int modPt);
            float.TryParse(col.Length > 5 ? col[5] : "0", out float spawnDelay);
            float.TryParse(col.Length > 6 ? col[6] : "0", out float spawnInterval);
            float.TryParse(col.Length > 7 ? col[7] : "0", out float fpx);
            float.TryParse(col.Length > 8 ? col[8] : "0", out float fpy);
            float.TryParse(col.Length > 9 ? col[9] : "0", out float fpz);
            float.TryParse(col.Length > 10 ? col[10] : "0", out float frotY);
            float.TryParse(col.Length > 11 ? col[11] : "0", out float playerFireDelay);
            float.TryParse(col.Length > 12 ? col[12] : "0", out float enemyFireDelay);

            string zoneName = $"{zoneIndex}-{stage}";
            enemyBackup.TryGetValue(zoneName, out FleetInfo fleet);
            m_dataTableZone.zoneStageList.Add(new ZoneStageConfig
            {
                zoneName               = zoneName,
                zoneDescription        = $"Zone {zoneName}",
                zoneIndex              = zoneIndex,
                delayBeforeSpawn       = spawnDelay > 0 ? spawnDelay : 3f,
                shipSpawnInterval      = spawnInterval > 0 ? spawnInterval : 1.5f,
                mineralClearReward     = clearReward,
                techPointClearReward   = techPt,
                modulePointClearReward = modPt,
                fleetPosition          = new Vector3(fpx, fpy, fpz),
                fleetRotationY         = frotY,
                playerFireDelaySec     = playerFireDelay,
                enemyFireDelaySec      = enemyFireDelay,
                enemyFleet             = fleet != null ? fleet : new FleetInfo { fleetName = zoneName, ships = new List<ShipInfo>() },
            });
            count++;
        }
        m_dataTableZone.BuildRuntimeCache();
        EditorUtility.SetDirty(m_dataTableZone);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("완료", $"Stage import 완료 ({count}개)", "OK");
    }

    private void ImportEnemy()
    {
        if (!File.Exists(k_enemyCSV)) { EditorUtility.DisplayDialog("Error", $"파일 없음:\n{k_enemyCSV}", "OK"); return; }

        // zoneName → enemyFleet 재구성
        var enemyMap = new Dictionary<string, List<ShipInfo>>();
        string[] lines = File.ReadAllLines(k_enemyCSV);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            if (!int.TryParse(col[0], out int zoneIndex) || !int.TryParse(col[1], out int stageNum)) continue;

            string zoneName = $"{zoneIndex}-{stageNum}";
            if (!enemyMap.ContainsKey(zoneName)) enemyMap[zoneName] = new List<ShipInfo>();

            int.TryParse(col[2], out int shipIdx);
            System.Enum.TryParse(col[3], out EModuleSubType bodyType);
            int.TryParse(col[4], out int bodyLv);

            var beams = new List<ModuleInfo>(); var missiles = new List<ModuleInfo>(); var hangers = new List<ModuleInfo>();
            if (!string.IsNullOrEmpty(col[5]) && System.Enum.TryParse(col[5], out EModuleSubType beamType) && int.TryParse(col[6], out int beamLv))
            { int cnt = col.Length > 7 && int.TryParse(col[7], out int bc) ? bc : 1; for (int s = 0; s < cnt; s++) beams.Add(new ModuleInfo { moduleType = EModuleType.beam, moduleSubType = beamType, moduleLevel = beamLv, bodyIndex = 0, slotIndex = s }); }
            if (!string.IsNullOrEmpty(col[8]) && System.Enum.TryParse(col[8], out EModuleSubType missileType) && int.TryParse(col[9], out int missileLv))
            { int cnt = col.Length > 10 && int.TryParse(col[10], out int mc) ? mc : 1; for (int s = 0; s < cnt; s++) missiles.Add(new ModuleInfo { moduleType = EModuleType.missile, moduleSubType = missileType, moduleLevel = missileLv, bodyIndex = 0, slotIndex = s }); }
            if (!string.IsNullOrEmpty(col[11]) && System.Enum.TryParse(col[11], out EModuleSubType hangerType) && int.TryParse(col[12], out int hangerLv))
            { int cnt = col.Length > 13 && int.TryParse(col[13], out int hc) ? hc : 1; for (int s = 0; s < cnt; s++) hangers.Add(new ModuleInfo { moduleType = EModuleType.hanger, moduleSubType = hangerType, moduleLevel = hangerLv, bodyIndex = 0, slotIndex = s }); }

            float.TryParse(col[14], out float bodyR); float.TryParse(col[15], out float beamR);
            float.TryParse(col[16], out float missileR); float.TryParse(col[17], out float hangerR);
            enemyMap[zoneName].Add(new ShipInfo
            {
                shipName = $"EnemyShip_{shipIdx}", positionIndex = shipIdx,
                bodyMultiplier = bodyR, beamMultiplier = beamR, missileMultiplier = missileR, hangerMultiplier = hangerR,
                bodies = new List<ModuleBodyInfo> { new ModuleBodyInfo { moduleType = EModuleType.body, moduleSubType = bodyType, moduleLevel = bodyLv, bodyIndex = 0, beams = beams, missiles = missiles, hangers = hangers } }
            });
        }

        for (int j = 0; j < m_dataTableZone.zoneStageList.Count; j++)
        {
            ZoneStageConfig zs = m_dataTableZone.zoneStageList[j];
            if (!enemyMap.TryGetValue(zs.zoneName, out List<ShipInfo> ships)) continue;
            if (zs.enemyFleet == null) zs.enemyFleet = new FleetInfo { fleetName = zs.zoneName };
            zs.enemyFleet.ships = ships;
        }
        EditorUtility.SetDirty(m_dataTableZone);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("완료", "Enemy import 완료", "OK");
    }



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
                // 새 스테이지 추가 (enemyFleet 기본값)
                m_dataTableZone.zoneStageList.Add(new ZoneStageConfig
                {
                    zoneName          = stageName,
                    zoneDescription   = $"Zone {stageName}",
                    zoneIndex         = zoneIndex,
                    delayBeforeSpawn  = 3f,
                    shipSpawnInterval = 1.5f,
                    fleetPosition     = new Vector3(x, 0f, z),
                    enemyFleet        = new FleetInfo { fleetName = stageName, ships = new List<ShipInfo>() },
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
        int shipCount = (zoneStage.enemyFleet != null && zoneStage.enemyFleet.ships != null) ? zoneStage.enemyFleet.ships.Count : 0;
        zoneFoldouts[zoneName] = EditorGUILayout.Foldout(zoneFoldouts[zoneName],
            $"Zone {zoneStage.zoneName} (Ships: {shipCount})", true, EditorStyles.foldoutHeader);

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
            zoneStage.techPointClearReward   = EditorGUILayout.IntField("TechPoint (최초 클리어)", zoneStage.techPointClearReward);
            zoneStage.modulePointClearReward = EditorGUILayout.IntField("ModulePoint (최초 클리어)", zoneStage.modulePointClearReward);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 전투 설정
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("전투 설정", EditorStyles.boldLabel);
            zoneStage.delayBeforeSpawn      = EditorGUILayout.Slider("첫 스폰 지연 (초)", zoneStage.delayBeforeSpawn, 0f, 60f);
            zoneStage.shipSpawnInterval     = EditorGUILayout.Slider("함선 간 스폰 딜레이 (초)", zoneStage.shipSpawnInterval, 0f, 30f);
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

            // 적 함대 구성 함선 템플릿 (전체가 한 함대로 스폰)
            EditorGUILayout.BeginHorizontal();
            int fleetShipCount = (zoneStage.enemyFleet != null && zoneStage.enemyFleet.ships != null) ? zoneStage.enemyFleet.ships.Count : 0;
            EditorGUILayout.LabelField($"함선 템플릿 ({fleetShipCount})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add Ship", GUILayout.Width(100)))
            {
                if (zoneStage.enemyFleet == null)
                    zoneStage.enemyFleet = new FleetInfo { fleetName = zoneStage.zoneName, ships = new List<ShipInfo>() };
                if (zoneStage.enemyFleet.ships == null)
                    zoneStage.enemyFleet.ships = new List<ShipInfo>();
                var body = new ModuleBodyInfo { moduleType = EModuleType.body, moduleSubType = EModuleSubType.body_t1_m1, moduleLevel = 1, bodyIndex = 0, beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangers = new List<ModuleInfo>() };
                zoneStage.enemyFleet.ships.Add(new ShipInfo { shipName = $"EnemyShip_{fleetShipCount}", positionIndex = fleetShipCount, bodyMultiplier = 1f, beamMultiplier = 1f, missileMultiplier = 1f, hangerMultiplier = 1f, bodies = new List<ModuleBodyInfo> { body } });
                EditorUtility.SetDirty(m_dataTableZone);
            }
            EditorGUILayout.EndHorizontal();

            if (zoneStage.enemyFleet != null && zoneStage.enemyFleet.ships != null)
            {
                for (int shipIndex = 0; shipIndex < zoneStage.enemyFleet.ships.Count; shipIndex++)
                {
                    DrawShips(zoneName, shipIndex, zoneStage.enemyFleet.ships[shipIndex], zoneStage);
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawShips(string zoneName, int shipIndex, ShipInfo ship, ZoneStageConfig zoneStage)
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
            zoneStage.enemyFleet.ships.RemoveAt(shipIndex);
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
