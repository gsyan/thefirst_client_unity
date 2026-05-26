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

    private Dictionary<int, bool> zoneFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, Dictionary<int, bool>> shipFoldouts = new Dictionary<int, Dictionary<int, bool>>();
    private Dictionary<int, bool> zoneGroupFoldouts = new Dictionary<int, bool>(); // x값(그룹) 폴드아웃
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
        if (GUILayout.Button("Import from CSV"))
        {
            if (EditorUtility.DisplayDialog("Import from CSV",
                "datatable_zone.csv + datatable_zone_stage.csv + datatable_zone_enemy.csv + datatable_zone_celestial.csv 에서 전체 데이터를 가져옵니다.\n기존 데이터가 삭제됩니다.\n\n계속하시겠습니까?", "Yes", "Cancel"))
            {
                ImportFromCSV();
            }
        }

        if (GUILayout.Button("Export All CSV"))
            ExportAllCSV();

        if (GUILayout.Button("Validate All Ships"))
        {
            ValidateAllShips();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // datatable_zone.csv + datatable_zone_stage.csv + datatable_zone_enemy.csv + datatable_zone_celestial.csv → ScriptableObject 전체 교체
    private void ImportFromCSV()
    {
        string zoneCommonCSV  = "Assets/Resources/DataTable/Zone/datatable_zone.csv";
        string zoneCSV        = "Assets/Resources/DataTable/Zone/datatable_zone_stage.csv";
        string enemyCSV       = "Assets/Resources/DataTable/Zone/datatable_zone_enemy.csv";
        string celestialCSV   = "Assets/Resources/DataTable/Zone/datatable_zone_celestial.csv";

        if (!File.Exists(zoneCommonCSV) || !File.Exists(zoneCSV) || !File.Exists(enemyCSV))
        {
            EditorUtility.DisplayDialog("Error", $"CSV 파일을 찾을 수 없습니다.\n{zoneCommonCSV}\n{zoneCSV}\n{enemyCSV}", "OK");
            return;
        }

        // --- datatable_zone.csv 파싱 → zoneList (완전 재구성) ---
        var oldZoneMap = new Dictionary<int, ZoneConfig>();
        for (int j = 0; j < m_dataTableZone.zoneList.Count; j++)
            oldZoneMap[m_dataTableZone.zoneList[j].zoneIndex] = m_dataTableZone.zoneList[j];

        m_dataTableZone.zoneList.Clear();

        string[] zoneCommonLines = File.ReadAllLines(zoneCommonCSV);
        for (int i = 1; i < zoneCommonLines.Length; i++)
        {
            string line = zoneCommonLines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');

            if (!int.TryParse(col[0], out int zoneIndex)) continue;

            float.TryParse(col[1], out float cx);
            float.TryParse(col[2], out float cy);
            float.TryParse(col[3], out float cz);
            float.TryParse(col[4], out float zoom);
            float.TryParse(col[5], out float rotX);
            float.TryParse(col[6], out float rotY);

            m_dataTableZone.zoneList.Add(new ZoneConfig
            {
                zoneIndex          = zoneIndex,
                galaxyCameraTarget = new Vector3(cx, cy, cz),
                galaxyCameraZoom   = zoom,
                galaxyCameraRotX   = rotX,
                galaxyCameraRotY   = rotY,
                celestialBodies    = new List<CelestialBodyConfig>(),
            });
        }

        // --- datatable_zone_celestial.csv 파싱 → celestialBodies (존재할 때만) ---
        // 헤더: zone_index,pos_x,pos_y,pos_z,scale_x,scale_y,scale_z,
        //        sea_r,sea_g,sea_b,land_r,land_g,land_b,land_coverage,land_rotation,
        //        has_clouds,cloud_r,cloud_g,cloud_b,cloud_a,cloud_coverage,cloud_rotation,cloud_scale,
        //        has_atmosphere,atm_r,atm_g,atm_b,atmosphere_scale
        if (File.Exists(celestialCSV))
        {
            var zoneMap = new Dictionary<int, ZoneConfig>();
            for (int j = 0; j < m_dataTableZone.zoneList.Count; j++)
                zoneMap[m_dataTableZone.zoneList[j].zoneIndex] = m_dataTableZone.zoneList[j];

            string[] celestialLines = File.ReadAllLines(celestialCSV);
            for (int i = 1; i < celestialLines.Length; i++)
            {
                string line = celestialLines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                string[] col = line.Split(',');
                if (!int.TryParse(col[0], out int zi)) continue;
                if (!zoneMap.TryGetValue(zi, out ZoneConfig zc)) continue;

                float F(int idx, float def = 0f) => col.Length > idx && float.TryParse(col[idx], out float v) ? v : def;
                bool  B(int idx)                 => col.Length > idx && col[idx].Trim().ToLower() == "true";
                Color C(int idx, Color def)
                {
                    if (col.Length <= idx) return def;
                    return ColorUtility.TryParseHtmlString(col[idx].Trim(), out Color c) ? c : def;
                }

                // col 인덱스: 0=zone, 1~3=pos, 4~6=rot, 7~9=scale,
                // 10=land_coverage, 11=biome_blend, 12=g_blend,
                // 13=deep_sea_color, 14=shallow_sea_color, 15=lowland_sand_color, 16=lowland_green_color,
                // 17=plains_desert_color, 18=plains_grass_color, 19=plains_forest_color, 20=highland_snow_color,
                // 21=has_polar_ice, 22=ice_color, 23=ice_color_edge, 24=pole_ice_width,
                // 25=has_clouds, 26=cloud_color, 27=cloud_coverage, 28=cloud_rotation, 29=cloud_scale,
                // 30=has_atmosphere, 31=atmosphere_color, 32=atmosphere_scale
                zc.celestialBodies.Add(new CelestialBodyConfig
                {
                    position          = new Vector3(F(1), F(2), F(3)),
                    rotation          = new Vector3(F(4), F(5), F(6)),
                    scale             = new Vector3(F(7), F(8), F(9)),
                    landCoverage      = F(10, 0.5f),
                    biomeBlend        = F(11, 0.01f),
                    gBlend            = F(12, 0.02f),
                    deepSeaColor      = C(13, CommonUtility.HexColor("#0D2673")),
                    shallowSeaColor   = C(14, CommonUtility.HexColor("#1A59A6")),
                    lowlandSandColor  = C(15, CommonUtility.HexColor("#BFB380")),
                    lowlandGreenColor = C(16, CommonUtility.HexColor("#90C060")),
                    plainsDesertColor = C(17, CommonUtility.HexColor("#A99159")),
                    plainsGrassColor  = C(18, CommonUtility.HexColor("#478C2E")),
                    plainsForestColor = C(19, CommonUtility.HexColor("#236523")),
                    highlandSnowColor = C(20, CommonUtility.HexColor("#E8F0F5")),
                    hasPolarIce       = B(21),
                    iceColor          = C(22, CommonUtility.HexColor("#F2FAFF")),
                    iceColorEdge      = C(23, CommonUtility.HexColor("#ADD1F0")),
                    poleIceWidth      = F(24, 0.12f),
                    hasClouds         = B(25),
                    cloudColor        = C(26, CommonUtility.HexColor("#FFFFFFD9")),
                    cloudCoverage     = F(27, 0.5f),
                    cloudRotation     = F(28),
                    cloudScale        = F(29, 1.001f),
                    hasAtmosphere     = B(30),
                    atmosphereColor   = C(31, CommonUtility.HexColor("#4D99FF")),
                    atmosphereScale   = F(32, 1.002f),
                });
            }
        }

        // --- enemy CSV 파싱 (zone,stage → 함선 목록) ---
        // 헤더: zone_stage,stage,flag_ship,body_type,body_level,beam_type,beam_level,beam_count,missile_type,missile_level,missile_count,hanger_type,hanger_level,hanger_count,body_ratio,beam_ratio,missile_ratio,hanger_ratio
        // flag_ship: 1=기함(슬롯0 전용), 0=일반(슬롯0 제외)
        var enemyMap = new Dictionary<(int zone, int stage), List<EnemyShipConfig>>();
        string[] enemyLines = File.ReadAllLines(enemyCSV);
        for (int i = 1; i < enemyLines.Length; i++)
        {
            string line = enemyLines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            
            // key 값 찾기
            int.TryParse(col[0],  out int zoneIndex);
            int.TryParse(col[1],  out int stageIndex);
            var key = (zoneIndex, stageIndex);
            if (!enemyMap.ContainsKey(key))
                enemyMap[key] = new List<EnemyShipConfig>();

            int.TryParse(col[2], out int shipIndex);
            System.Enum.TryParse(col[3], out EModuleSubType bodyType);
            int.TryParse(col[4], out int bodyLv);
            var ship = new EnemyShipConfig();
            ship.shipIndex = shipIndex;
            ship.bodySubType = bodyType;
            ship.bodyLevel = bodyLv;
            // 함체 정보로 모듈 정보
            RefreshShipModuleSlots(ship);
            
            // beam 장착: beam_type, beam_level, beam_count (count 빈 값 = 1)
            if ( string.IsNullOrEmpty(col[5]) == false && System.Enum.TryParse(col[5], out EModuleSubType beamType) && int.TryParse(col[6], out int beamLv))
            {
                int beamCount = string.IsNullOrEmpty(col[7]) ? 1 : (int.TryParse(col[7], out int bc) ? bc : 1);
                int filled = 0;
                foreach (var slot in ship.moduleSlots.Where(s => s.slotType == EModuleType.beam).OrderBy(s => s.slotIndex))
                {
                    if (filled >= beamCount) break;
                    slot.moduleSubType = beamType;
                    slot.moduleLevel = beamLv;
                    filled++;
                }
            }

            // missile 장착: missile_type, missile_level, missile_count (count 빈 값 = 1)
            if (string.IsNullOrEmpty(col[8]) == false && System.Enum.TryParse(col[8], out EModuleSubType missileType) && int.TryParse(col[9], out int missileLv))
            {
                int missileCount = string.IsNullOrEmpty(col[10]) ? 1 : (int.TryParse(col[10], out int mc) ? mc : 1);
                int filled = 0;
                foreach (var slot in ship.moduleSlots.Where(s => s.slotType == EModuleType.missile).OrderBy(s => s.slotIndex))
                {
                    if (filled >= missileCount) break;
                    slot.moduleSubType = missileType;
                    slot.moduleLevel = missileLv;
                    filled++;
                }
            }

            // hanger 장착: hanger_type, hanger_level, hanger_count (count 빈 값 = 1)
            if (string.IsNullOrEmpty(col[11]) == false && System.Enum.TryParse(col[11], out EModuleSubType hangerType) && int.TryParse(col[12], out int hangerLv))
            {
                int hangerCount = string.IsNullOrEmpty(col[13]) ? 1 : (int.TryParse(col[13], out int hc) ? hc : 1);
                int filled = 0;
                foreach (var slot in ship.moduleSlots.Where(s => s.slotType == EModuleType.hanger).OrderBy(s => s.slotIndex))
                {
                    if (filled >= hangerCount) break;
                    slot.moduleSubType = hangerType;
                    slot.moduleLevel = hangerLv;
                    filled++;
                }
            }

            float.TryParse(col[14], out float bodyR);
            float.TryParse(col[15], out float beamR);
            float.TryParse(col[16], out float missileR);
            float.TryParse(col[17], out float hangerR);
            ship.bodyMultiplier    = bodyR;
            ship.beamMultiplier    = beamR;
            ship.missileMultiplier = missileR;
            ship.hangerMultiplier  = hangerR;
            enemyMap[key].Add(ship);
        }

        // --- zone CSV 파싱 ---
        // 헤더: zone,stage,mineral_clear_reward,tech_point_clear_reward,module_point_clear_reward,spawn_delay,ship_spawn_interval,fleet_pos_x,fleet_pos_y,fleet_pos_z,fleet_rot_y
        m_dataTableZone.zoneStageList.Clear();

        string[] zoneLines = File.ReadAllLines(zoneCSV);
        int imported = 0;
        for (int i = 1; i < zoneLines.Length; i++)
        {
            string line = zoneLines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');

            if (!int.TryParse(col[0], out int zoneIndex) || !int.TryParse(col[1], out int stage)) continue;

            int.TryParse(col[2], out int clearReward);
            int.TryParse(col.Length > 3 ? col[3] : "0", out int techPointReward);
            int.TryParse(col.Length > 4 ? col[4] : "0", out int modulePointReward);
            float.TryParse(col.Length > 5 ? col[5] : "0", out float spawnDelay);
            float.TryParse(col.Length > 6 ? col[6] : "0", out float shipSpawnInterval);
            float.TryParse(col.Length > 7 ? col[7] : "0", out float fpx);
            float.TryParse(col.Length > 8 ? col[8] : "0", out float fpy);
            float.TryParse(col.Length > 9 ? col[9] : "0", out float fpz);
            float.TryParse(col.Length > 10 ? col[10] : "0", out float frotY);

            enemyMap.TryGetValue((zoneIndex, stage), out var waveTemplates);

            var zoneStage = new ZoneStageConfig
            {
                zoneName                  = $"{zoneIndex}-{stage}",
                zoneDescription           = $"Zone {zoneIndex}-{stage}",
                zoneIndex                 = zoneIndex,
                delayBeforeSpawn          = spawnDelay > 0 ? spawnDelay : 3f,
                shipSpawnInterval         = shipSpawnInterval > 0 ? shipSpawnInterval : 1.5f,
                mineralClearReward        = clearReward,
                techPointClearReward      = techPointReward,
                modulePointClearReward    = modulePointReward,
                fleetPosition             = new Vector3(fpx, fpy, fpz),
                fleetRotationY            = frotY,
                enemyShipConfigs          = waveTemplates ?? new List<EnemyShipConfig>(),
            };

            m_dataTableZone.zoneStageList.Add(zoneStage);
            imported++;
        }

        EditorUtility.SetDirty(m_dataTableZone);
        EditorUtility.DisplayDialog("Import Complete",
            $"ZoneConfig: {m_dataTableZone.zoneList.Count}개\n" +
            $"ZoneStage: {m_dataTableZone.zoneStageList.Count}개 (zone CSV: {imported}행)", "OK");
    }

    private void ExportAllCSV()
    {
        DataTableZoneCSVUtility.ExportAll(m_dataTableZone);
        EditorUtility.DisplayDialog("Export Complete",
            "datatable_zone.csv\ndatatable_zone_stage.csv\ndatatable_zone_enemy.csv\ndatatable_zone_celestial.csv", "OK");
    }


    private void ValidateAllShips()
    {
        int totalShips = 0;
        int invalidShips = 0;

        foreach (var zone in m_dataTableZone.zoneStageList)
        {
            if (zone.enemyShipConfigs == null) continue;
            foreach (var ship in zone.enemyShipConfigs)
            {
                totalShips++;
                if (ship.moduleSlots == null || ship.moduleSlots.Count == 0)
                {
                    invalidShips++;
                    RefreshShipModuleSlots(ship);
                }
            }
        }

        if (invalidShips > 0)
        {
            EditorUtility.SetDirty(m_dataTableZone);
            EditorUtility.DisplayDialog("Validation", $"Total: {totalShips} ships\nFixed: {invalidShips} ships with missing slots", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Validation", $"Total: {totalShips} ships\nAll ships are valid!", "OK");
        }
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
                DrawZoneStage(stageIndex);
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

        EditorGUILayout.LabelField("갤럭시 뷰 카메라 앵커", EditorStyles.boldLabel);
        zoneConfig.galaxyCameraTarget = EditorGUILayout.Vector3Field("  Camera Target", zoneConfig.galaxyCameraTarget);
        zoneConfig.galaxyCameraZoom   = EditorGUILayout.FloatField("  Camera Zoom", zoneConfig.galaxyCameraZoom);
        zoneConfig.galaxyCameraRotX   = EditorGUILayout.Slider("  Rot X (앙각)", zoneConfig.galaxyCameraRotX, -80f, 80f);
        zoneConfig.galaxyCameraRotY   = EditorGUILayout.FloatField("  Rot Y (수평)", zoneConfig.galaxyCameraRotY);

        // 천체 배치
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("천체 배치", EditorStyles.boldLabel);

        if (zoneConfig.celestialBodies == null)
            zoneConfig.celestialBodies = new System.Collections.Generic.List<CelestialBodyConfig>();

        CelestialBodyEditorGUI.DrawCelestialBodyList(zoneConfig.celestialBodies, m_dataTableZone);

        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(m_dataTableZone);
    }

    private void DrawZoneStage(int stageIntegratedIndex)
    {
        var zoneStage = m_dataTableZone.zoneStageList[stageIntegratedIndex];
        if (!zoneFoldouts.ContainsKey(stageIntegratedIndex))
            zoneFoldouts[stageIntegratedIndex] = false;

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = zoneColor;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        // Zone Header
        EditorGUILayout.BeginHorizontal();
        int shipCount = zoneStage.enemyShipConfigs?.Count ?? 0;
        zoneFoldouts[stageIntegratedIndex] = EditorGUILayout.Foldout(zoneFoldouts[stageIntegratedIndex],
            $"Zone {stageIntegratedIndex}: {zoneStage.zoneName} (Ships: {shipCount})", true, EditorStyles.foldoutHeader);

        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            if (EditorUtility.DisplayDialog("Delete Zone", $"Delete '{zoneStage.zoneName}'?", "Delete", "Cancel"))
            {
                m_dataTableZone.zoneStageList.RemoveAt(stageIntegratedIndex);
                EditorUtility.SetDirty(m_dataTableZone);
                return;
            }
        }
        EditorGUILayout.EndHorizontal();

        if (zoneFoldouts[stageIntegratedIndex])
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
            EditorGUILayout.LabelField($"함선 템플릿 ({zoneStage.enemyShipConfigs?.Count ?? 0})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add Ship", GUILayout.Width(100)))
            {
                if (zoneStage.enemyShipConfigs == null)
                    zoneStage.enemyShipConfigs = new List<EnemyShipConfig>();
                var ship = new EnemyShipConfig { bodySubType = EModuleSubType.body_t1_m1, bodyLevel = 1 };
                RefreshShipModuleSlots(ship);
                zoneStage.enemyShipConfigs.Add(ship);
                EditorUtility.SetDirty(m_dataTableZone);
            }
            EditorGUILayout.EndHorizontal();

            if (zoneStage.enemyShipConfigs != null)
            {
                for (int shipIndex = 0; shipIndex < zoneStage.enemyShipConfigs.Count; shipIndex++)
                {
                    DrawShips(stageIntegratedIndex, shipIndex, zoneStage.enemyShipConfigs[shipIndex]);
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawShips(int stageIndex, int shipIndex, EnemyShipConfig ship)
    {
        if (!shipFoldouts.ContainsKey(stageIndex))
            shipFoldouts[stageIndex] = new Dictionary<int, bool>();
        if (!shipFoldouts[stageIndex].ContainsKey(shipIndex))
            shipFoldouts[stageIndex][shipIndex] = false;

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = shipColor;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        // Wave Header
        EditorGUILayout.BeginHorizontal();
        string slotInfo = ship.moduleSlots != null ? $", Slots: {ship.moduleSlots.Count}" : "";
        shipFoldouts[stageIndex][shipIndex] = EditorGUILayout.Foldout(
            shipFoldouts[stageIndex][shipIndex],
            $"Ship {shipIndex + 1} [idx:{ship.shipIndex}]: {ship.bodySubType} Lv.{ship.bodyLevel}{slotInfo}", true);

        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            m_dataTableZone.zoneStageList[stageIndex].enemyShipConfigs.RemoveAt(shipIndex);
            EditorUtility.SetDirty(m_dataTableZone);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (shipFoldouts[stageIndex][shipIndex])
        {
            EditorGUI.indentLevel++;

            int newShipIndex = EditorGUILayout.IntField("Ship Index (진형 슬롯)", ship.shipIndex);
            if (newShipIndex != ship.shipIndex) { ship.shipIndex = newShipIndex; EditorUtility.SetDirty(m_dataTableZone); }

            // Body Type + Level
            EditorGUILayout.LabelField("Body", EditorStyles.boldLabel);
            int bodyIndex = System.Array.IndexOf(bodySubTypes, ship.bodySubType);
            if (bodyIndex < 0) bodyIndex = 0;
            int newBodyIndex = EditorGUILayout.Popup("Body Type", bodyIndex, bodySubTypeNames);
            if (newBodyIndex != bodyIndex)
            {
                ship.bodySubType = bodySubTypes[newBodyIndex];
                EditorUtility.SetDirty(m_dataTableZone);
            }

            int newLevel = EditorGUILayout.IntSlider("Body Level", ship.bodyLevel, 1, 10);
            if (newLevel != ship.bodyLevel)
            {
                ship.bodyLevel = newLevel;
                EditorUtility.SetDirty(m_dataTableZone);
            }


            EditorGUILayout.Space(5);

            // Module Slots
            if (ship.moduleSlots != null && ship.moduleSlots.Count > 0)
            {
                DrawModuleSlots(ship);
            }
            else
            {
                EditorGUILayout.HelpBox("No module slots found. Click 'Refresh Slots' to load from prefab.", MessageType.Warning);
            }

            EditorGUILayout.Space(5);

            // 스탯 배율 (함선별)
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

    private void DrawModuleSlots(EnemyShipConfig ship)
    {
        // 타입별로 그룹화 — 슬롯이 없어도 항상 표시
        var beamSlots    = ship.moduleSlots.Where(s => s.slotType == EModuleType.beam).OrderBy(s => s.slotIndex).ToList();
        var missileSlots = ship.moduleSlots.Where(s => s.slotType == EModuleType.missile).OrderBy(s => s.slotIndex).ToList();
        var hangerSlots  = ship.moduleSlots.Where(s => s.slotType == EModuleType.hanger).OrderBy(s => s.slotIndex).ToList();

        DrawSlotGroup("Beam",    beamSlots,    EModuleType.beam);
        DrawSlotGroup("Missile", missileSlots, EModuleType.missile);
        DrawSlotGroup("Hanger",  hangerSlots,  EModuleType.hanger);
    }

    private void DrawSlotGroup(string groupName, List<EnemyModuleSlotConfig> slots, EModuleType moduleType)
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

            // 첫째 줄: SubType (none=비어있음)
            int currentIndex = System.Array.IndexOf(subTypes, slot.moduleSubType);
            if (currentIndex < 0) currentIndex = 0;
            int newIndex = EditorGUILayout.Popup(currentIndex, subTypeNames);
            if (newIndex != currentIndex)
            {
                slot.moduleSubType = subTypes[newIndex];
                EditorUtility.SetDirty(m_dataTableZone);
            }

            // 둘째 줄: Level — 비어있으면 비활성화
            EditorGUI.BeginDisabledGroup(slot.moduleSubType == EModuleSubType.none);
            int newLevel = EditorGUILayout.IntSlider("Level", slot.moduleLevel, 1, 10);
            if (newLevel != slot.moduleLevel)
            {
                slot.moduleLevel = newLevel;
                EditorUtility.SetDirty(m_dataTableZone);
            }
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

    // Body 프리팹에서 슬롯 정보 추출
    private void RefreshShipModuleSlots(EnemyShipConfig ship)
    {
        ship.moduleSlots = new List<EnemyModuleSlotConfig>();

        string prefabPath = ObjectManager.GetShipModulePrefabPath(EModuleType.body.ToString(), ship.bodySubType.ToString());
        GameObject prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Prefab not found: {prefabPath}");
            return;
        }

        ModuleSlot[] slots = prefab.GetComponentsInChildren<ModuleSlot>(true);
        if (slots == null || slots.Length == 0)
        {
            Debug.LogWarning($"No ModuleSlot found in: {prefabPath}");
            return;
        }

        foreach (var slot in slots)
        {
            // 유효한 슬롯 타입만 (none/max 제외)
            EModuleType t = slot.m_moduleSlotInfo.moduleType;
            if (t == EModuleType.none || t == EModuleType.max || t == EModuleType.body) continue;

            ship.moduleSlots.Add(new EnemyModuleSlotConfig
            {
                slotType      = t,
                slotIndex     = slot.m_moduleSlotInfo.slotIndex,
                moduleSubType = EModuleSubType.none,  // 비어있음 — 에디터에서 직접 지정
                moduleLevel   = 1,
            });
        }
    }
}
#endif
