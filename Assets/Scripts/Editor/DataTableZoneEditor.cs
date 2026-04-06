// DataTableZone 에디터 - 존 데이터 편집 GUI
// CSV Import: datatable_zone.csv + datatable_zone_enemy.csv → ScriptableObject
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

[CustomEditor(typeof(DataTableZone))]
public class DataTableZoneEditor : Editor
{
    private DataTableZone config;
    private Vector2 scrollPosition;

    private Dictionary<int, bool> zoneFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, Dictionary<int, bool>> shipFoldouts = new Dictionary<int, Dictionary<int, bool>>();
    private Dictionary<int, bool> shipCountGroupFoldouts = new Dictionary<int, bool>(); // x값(함선개수) 그룹 폴드아웃
    private readonly Color zoneColor       = new Color(0.7f, 0.85f, 0.95f);
    private readonly Color shipColor       = new Color(0.85f, 0.95f, 0.85f);
    private readonly Color slotColor       = new Color(0.9f, 0.9f, 0.95f);
    private readonly Color multiplierColor = new Color(0.95f, 0.88f, 0.75f);

    // Body SubType 목록 캐싱
    private EModuleSubType[] bodySubTypes;
    private string[] bodySubTypeNames;

    private void OnEnable()
    {
        config = (DataTableZone)target;
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
        if (config == null) return;

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
            EditorUtility.SetDirty(config);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private new void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("Datatable Zone", EditorStyles.largeLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Zones: {config.zones.Count}", EditorStyles.miniLabel);

        if (GUILayout.Button("+ Add Zone", GUILayout.Width(100)))
        {
            config.zones.Add(new ZoneConfig
            {
                zoneName = $"Zone {config.zones.Count + 1}",
                zoneDescription = "New Zone"
            });
            EditorUtility.SetDirty(config);
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
                "datatable_zone.csv + datatable_zone_enemy.csv에서 전체 데이터를 가져옵니다.\n기존 데이터가 삭제됩니다.\n\n계속하시겠습니까?", "Yes", "Cancel"))
            {
                ImportFromCSV();
            }
        }

        if (GUILayout.Button("Validate All Ships"))
        {
            ValidateAllShips();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // datatable_zone.csv + datatable_zone_enemy.csv → ScriptableObject 전체 교체
    private void ImportFromCSV()
    {
        string zoneCSV  = "Assets/Resources/DataTable/Zone/datatable_zone.csv";
        string enemyCSV = "Assets/Resources/DataTable/Zone/datatable_zone_enemy.csv";

        if (!File.Exists(zoneCSV) || !File.Exists(enemyCSV))
        {
            EditorUtility.DisplayDialog("Error", $"CSV 파일을 찾을 수 없습니다.\n{zoneCSV}\n{enemyCSV}", "OK");
            return;
        }

        // --- enemy CSV 파싱 (zone,step → 함선 목록) ---
        // 헤더: zone,step,wave,body_type,body_level,beam_type,beam_level,beam_count,missile_type,missile_level,missile_count,hanger_type,hanger_level,hanger_count
        var enemyMap = new Dictionary<(int zone, int step), List<EnemyShipConfig>>();
        string[] enemyLines = File.ReadAllLines(enemyCSV);
        for (int i = 1; i < enemyLines.Length; i++)
        {
            string line = enemyLines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            if (col.Length < 8) continue;

            if (!int.TryParse(col[0], out int ez) || !int.TryParse(col[1], out int es)) continue;

            var key = (ez, es);
            if (!enemyMap.ContainsKey(key))
                enemyMap[key] = new List<EnemyShipConfig>();

            // 헤더: zone,step,wave,ship_count,body_type,body_level,beam_type,beam_level,beam_count,...
            var ship = new EnemyShipConfig();
            if (col.Length > 3 && int.TryParse(col[3], out int sc))
                ship.shipCount = sc;
            if (col.Length > 4 && System.Enum.TryParse(col[4], out EModuleSubType bodyType))
                ship.bodySubType = bodyType;
            if (col.Length > 5 && int.TryParse(col[5], out int bodyLv))
                ship.bodyLevel = bodyLv;

            RefreshShipModuleSlots(ship);

            // beam 장착: beam_type, beam_level, beam_count (count 빈 값 = 1)
            if (col.Length > 8 && !string.IsNullOrEmpty(col[6]) &&
                System.Enum.TryParse(col[6], out EModuleSubType beamType) &&
                int.TryParse(col[7], out int beamLv))
            {
                int beamCount = string.IsNullOrEmpty(col[8]) ? 1 : (int.TryParse(col[8], out int bc) ? bc : 1);
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
            if (col.Length > 11 && !string.IsNullOrEmpty(col[9]) &&
                System.Enum.TryParse(col[9], out EModuleSubType missileType) &&
                int.TryParse(col[10], out int missileLv))
            {
                int missileCount = string.IsNullOrEmpty(col[11]) ? 1 : (int.TryParse(col[11], out int mc) ? mc : 1);
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
            if (col.Length > 14 && !string.IsNullOrEmpty(col[12]) &&
                System.Enum.TryParse(col[12], out EModuleSubType hangerType) &&
                int.TryParse(col[13], out int hangerLv))
            {
                int hangerCount = string.IsNullOrEmpty(col[14]) ? 1 : (int.TryParse(col[14], out int hc) ? hc : 1);
                int filled = 0;
                foreach (var slot in ship.moduleSlots.Where(s => s.slotType == EModuleType.hanger).OrderBy(s => s.slotIndex))
                {
                    if (filled >= hangerCount) break;
                    slot.moduleSubType = hangerType;
                    slot.moduleLevel = hangerLv;
                    filled++;
                }
            }

            enemyMap[key].Add(ship);
        }

        // --- zone CSV 파싱 ---
        // 헤더: zone,step,kill_mineral,...,wave_count,wave_term,body_ratio,beam_ratio,missile_ratio,hanger_ratio
        config.zones.Clear();

        string[] zoneLines = File.ReadAllLines(zoneCSV);
        int imported = 0;
        for (int i = 1; i < zoneLines.Length; i++)
        {
            string line = zoneLines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            if (col.Length < 15) continue;

            if (!int.TryParse(col[0], out int shipCount) || !int.TryParse(col[1], out int stage)) continue;

            // zone=0 행 → Zone-0 안전지역 (전투 없음)
            if (shipCount == 0)
            {
                config.zones.Add(new ZoneConfig
                {
                    zoneName = "Zone-0",
                    zoneDescription = "안전지역",
                    shipCount = 0,
                    moduleLevel = 0,
                });
                continue;
            }

            // 헤더: zone,step,kill_mineral,kill_mineral_r,kill_mineral_e,kill_mineral_d,hour_mineral,hour_mineral_r,hour_mineral_e,hour_mineral_d,wave_term,body_ratio,beam_ratio,missile_ratio,hanger_ratio
            float.TryParse(col[2],  out float killM);
            float.TryParse(col[3],  out float killMR);
            float.TryParse(col[4],  out float killME);
            float.TryParse(col[5],  out float killMD);
            float.TryParse(col[6],  out float hourM);
            float.TryParse(col[7],  out float hourMR);
            float.TryParse(col[8],  out float hourME);
            float.TryParse(col[9],  out float hourMD);
            float.TryParse(col[10], out float waveTerm);
            float.TryParse(col[11], out float bodyR);
            float.TryParse(col[12], out float beamR);
            float.TryParse(col[13], out float missileR);
            float.TryParse(col[14], out float hangerR);

            int moduleLevel = Mathf.Min(stage, shipCount);
            enemyMap.TryGetValue((shipCount, stage), out var waveTemplates);

            var zone = new ZoneConfig
            {
                zoneName              = $"{shipCount}-{stage}",
                zoneDescription       = $"함선 {shipCount}척, 모듈 Lv.{moduleLevel}",
                shipCount             = shipCount,
                moduleLevel           = moduleLevel,
                zoneClearCount        = waveTemplates != null ? waveTemplates.Count : 10,
                delayBeforeSpawn      = waveTerm > 0 ? waveTerm : 3f,
                killRewardMineral     = killM,
                killRewardMineralRare = killMR,
                killRewardMineralExotic = killME,
                killRewardMineralDark = killMD,
                mineralPerHour        = hourM,
                mineralRarePerHour    = hourMR,
                mineralExoticPerHour  = hourME,
                mineralDarkPerHour    = hourMD,
                enemyBodyMultiplier    = bodyR > 0 ? bodyR : 1f,
                enemyBeamMultiplier    = beamR > 0 ? beamR : 1f,
                enemyMissileMultiplier = missileR > 0 ? missileR : 1f,
                enemyHangerMultiplier  = hangerR > 0 ? hangerR : 1f,
                enemyShipConfigs      = waveTemplates ?? new List<EnemyShipConfig>(),
            };

            config.zones.Add(zone);
            imported++;
        }

        EditorUtility.SetDirty(config);
        EditorUtility.DisplayDialog("Import Complete", $"Zone-0 포함 총 {config.zones.Count}개 임포트 완료\n(zone CSV: {imported}행)", "OK");
    }


    private void ValidateAllShips()
    {
        int totalShips = 0;
        int invalidShips = 0;

        foreach (var zone in config.zones)
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
            EditorUtility.SetDirty(config);
            EditorUtility.DisplayDialog("Validation", $"Total: {totalShips} ships\nFixed: {invalidShips} ships with missing slots", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Validation", $"Total: {totalShips} ships\nAll ships are valid!", "OK");
        }
    }

    private void DrawZoneList()
    {
        // x-y 형식인 경우 x(함선 개수)로 그룹핑
        var groupedZones = new Dictionary<int, List<(int index, ZoneConfig zone)>>();

        for (int i = 0; i < config.zones.Count; i++)
        {
            var zone = config.zones[i];
            int shipCount = ParseShipCountFromZoneName(zone.zoneName);

            if (!groupedZones.ContainsKey(shipCount))
                groupedZones[shipCount] = new List<(int, ZoneConfig)>();
            groupedZones[shipCount].Add((i, zone));
        }

        // 그룹이 있으면 그룹별로 표시, 없으면 기존 방식
        if (groupedZones.Count > 1 || (groupedZones.Count == 1 && !groupedZones.ContainsKey(0)))
        {
            foreach (var group in groupedZones.OrderBy(g => g.Key))
            {
                if (group.Key == 0)
                {
                    // x-y 형식이 아닌 zone들 (Zone-0 포함) — 스카이박스는 groupConfig(shipCount=0)로 관리
                    foreach (var (index, zone) in group.Value)
                    {
                        DrawZone(index);
                        EditorGUILayout.Space(3);
                    }
                    DrawZoneGroupConfig(0);
                    EditorGUILayout.Space(5);
                }
                else
                {
                    DrawShipCountGroup(group.Key, group.Value);
                }
            }
        }
        else
        {
            // 기존 방식
            for (int zoneIndex = 0; zoneIndex < config.zones.Count; zoneIndex++)
            {
                DrawZone(zoneIndex);
                EditorGUILayout.Space(5);
            }
        }
    }

    // zone 이름에서 함선 개수(x) 파싱 (x-y 형식)
    private int ParseShipCountFromZoneName(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName)) return 0;

        int dashIndex = zoneName.IndexOf('-');
        if (dashIndex > 0 && int.TryParse(zoneName.Substring(0, dashIndex), out int shipCount))
            return shipCount;

        return 0;
    }

    // 함선 개수별 그룹 그리기
    private void DrawShipCountGroup(int shipCount, List<(int index, ZoneConfig zone)> zones)
    {
        if (!shipCountGroupFoldouts.ContainsKey(shipCount))
            shipCountGroupFoldouts[shipCount] = false;

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.8f, 0.8f, 0.95f);
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        shipCountGroupFoldouts[shipCount] = EditorGUILayout.Foldout(
            shipCountGroupFoldouts[shipCount],
            $"Zone{shipCount}  ({shipCount}-1 ~ {shipCount}-{zones.Count})",
            true, EditorStyles.foldoutHeader);

        if (shipCountGroupFoldouts[shipCount])
        {
            EditorGUI.indentLevel++;

            // 그룹 공유 설정 (스카이박스)
            DrawZoneGroupConfig(shipCount);
            EditorGUILayout.Space(5);

            foreach (var (index, zone) in zones)
            {
                DrawZone(index);
                EditorGUILayout.Space(3);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    // ZoneGroupConfig 편집 UI (스카이박스)
    private void DrawZoneGroupConfig(int shipCount)
    {
        ZoneGroupConfig groupConfig = null;
        for (int i = 0; i < config.zoneGroups.Count; i++)
        {
            if (config.zoneGroups[i].shipCount == shipCount)
            {
                groupConfig = config.zoneGroups[i];
                break;
            }
        }

        if (groupConfig == null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("이 그룹의 스카이박스 설정이 없습니다.", MessageType.Info);
            if (GUILayout.Button("생성", GUILayout.Width(50)))
            {
                groupConfig = new ZoneGroupConfig { shipCount = shipCount };
                config.zoneGroups.Add(groupConfig);
                EditorUtility.SetDirty(config);
            }
            EditorGUILayout.EndHorizontal();
            return;
        }

        groupConfig.skyboxMaterial = (Material)EditorGUILayout.ObjectField(
            "Skybox Material", groupConfig.skyboxMaterial, typeof(Material), false);
    }

    private void DrawZone(int zoneIndex)
    {
        var zone = config.zones[zoneIndex];
        if (!zoneFoldouts.ContainsKey(zoneIndex))
            zoneFoldouts[zoneIndex] = false;

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = zoneColor;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        // Zone Header
        EditorGUILayout.BeginHorizontal();
        int waveTemplateCount = zone.enemyShipConfigs?.Count ?? 0;
        zoneFoldouts[zoneIndex] = EditorGUILayout.Foldout(zoneFoldouts[zoneIndex],
            $"Zone {zoneIndex}: {zone.zoneName} (Waves: {zone.zoneClearCount}, Templates: {waveTemplateCount}, Ships/Wave: {zone.shipCount})", true, EditorStyles.foldoutHeader);

        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            if (EditorUtility.DisplayDialog("Delete Zone", $"Delete '{zone.zoneName}'?", "Delete", "Cancel"))
            {
                config.zones.RemoveAt(zoneIndex);
                EditorUtility.SetDirty(config);
                return;
            }
        }
        EditorGUILayout.EndHorizontal();

        if (zoneFoldouts[zoneIndex])
        {
            EditorGUI.indentLevel++;

            // Zone Info
            EditorGUILayout.BeginVertical("box");
            zone.zoneName = EditorGUILayout.TextField("Zone Name", zone.zoneName);
            zone.zoneDescription = EditorGUILayout.TextField("Description", zone.zoneDescription);
            EditorGUILayout.EndVertical();

            // 킬 보상
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("킬 보상", EditorStyles.boldLabel);
            zone.killRewardMineral = EditorGUILayout.FloatField("Mineral", zone.killRewardMineral);
            zone.killRewardMineralRare = EditorGUILayout.FloatField("MineralRare", zone.killRewardMineralRare);
            zone.killRewardMineralExotic = EditorGUILayout.FloatField("MineralExotic", zone.killRewardMineralExotic);
            zone.killRewardMineralDark = EditorGUILayout.FloatField("MineralDark", zone.killRewardMineralDark);
            EditorGUILayout.EndVertical();

            // 시간당 자원 수확량
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("시간당 자원 수확량 (클리어 후)", EditorStyles.boldLabel);
            zone.mineralPerHour = EditorGUILayout.FloatField("Mineral/hour", zone.mineralPerHour);
            zone.mineralRarePerHour = EditorGUILayout.FloatField("MineralRare/hour", zone.mineralRarePerHour);
            zone.mineralExoticPerHour = EditorGUILayout.FloatField("MineralExotic/hour", zone.mineralExoticPerHour);
            zone.mineralDarkPerHour = EditorGUILayout.FloatField("MineralDark/hour", zone.mineralDarkPerHour);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 전투 설정
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("전투 설정", EditorStyles.boldLabel);
            zone.zoneClearCount = EditorGUILayout.IntField("클리어 카운트 (라운드 수)", zone.zoneClearCount);
            zone.delayBeforeSpawn = EditorGUILayout.Slider("스폰 간격 (초)", zone.delayBeforeSpawn, 0f, 60f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 적 함대 스탯 배율
            var origColor = GUI.backgroundColor;
            GUI.backgroundColor = multiplierColor;
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = origColor;
            EditorGUILayout.LabelField("적 함대 스탯 배율  (1.0 = 플레이어 동일)", EditorStyles.boldLabel);
            zone.enemyBodyMultiplier    = EditorGUILayout.Slider("Body    (체력)",            zone.enemyBodyMultiplier,    0.1f, 2.0f);
            zone.enemyBeamMultiplier    = EditorGUILayout.Slider("Beam    (공격력·체력)",     zone.enemyBeamMultiplier,    0.1f, 2.0f);
            zone.enemyMissileMultiplier = EditorGUILayout.Slider("Missile (공격력·체력)",     zone.enemyMissileMultiplier, 0.1f, 2.0f);
            zone.enemyHangerMultiplier  = EditorGUILayout.Slider("Hanger  (함재기 전 스탯)", zone.enemyHangerMultiplier,  0.1f, 2.0f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 아군 함대 위치
            EditorGUILayout.BeginVertical("box");
            zone.skyboxRotation = EditorGUILayout.Slider("Skybox Rotation", zone.skyboxRotation, 0f, 360f);
            zone.fleetPosition = EditorGUILayout.Vector3Field("Fleet Position", zone.fleetPosition);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 웨이브별 함선 템플릿 (각 Wave에서 shipCount만큼 복제 스폰)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"웨이브별 함선 템플릿 ({zone.enemyShipConfigs?.Count ?? 0} / {zone.zoneClearCount} waves)", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add Wave", GUILayout.Width(100)))
            {
                if (zone.enemyShipConfigs == null)
                    zone.enemyShipConfigs = new List<EnemyShipConfig>();
                var ship = new EnemyShipConfig { bodySubType = EModuleSubType.body_t1_m1, bodyLevel = 1 };
                RefreshShipModuleSlots(ship);
                zone.enemyShipConfigs.Add(ship);
                EditorUtility.SetDirty(config);
            }
            EditorGUILayout.EndHorizontal();

            if (zone.enemyShipConfigs != null)
            {
                for (int waveIndex = 0; waveIndex < zone.enemyShipConfigs.Count; waveIndex++)
                {
                    DrawWaveTemplate(zoneIndex, waveIndex, zone.enemyShipConfigs[waveIndex]);
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawWaveTemplate(int zoneIndex, int waveIndex, EnemyShipConfig ship)
    {
        if (!shipFoldouts.ContainsKey(zoneIndex))
            shipFoldouts[zoneIndex] = new Dictionary<int, bool>();
        if (!shipFoldouts[zoneIndex].ContainsKey(waveIndex))
            shipFoldouts[zoneIndex][waveIndex] = false;

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = shipColor;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        // Wave Header
        EditorGUILayout.BeginHorizontal();
        string slotInfo = ship.moduleSlots != null ? $", Slots: {ship.moduleSlots.Count}" : "";
        shipFoldouts[zoneIndex][waveIndex] = EditorGUILayout.Foldout(
            shipFoldouts[zoneIndex][waveIndex],
            $"Wave {waveIndex + 1}: {ship.shipCount}척 / {ship.bodySubType} Lv.{ship.bodyLevel}{slotInfo}", true);

        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            config.zones[zoneIndex].enemyShipConfigs.RemoveAt(waveIndex);
            EditorUtility.SetDirty(config);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (shipFoldouts[zoneIndex][waveIndex])
        {
            EditorGUI.indentLevel++;

            // 스폰 함선 수
            int newShipCount = EditorGUILayout.IntSlider("Ship Count", ship.shipCount, 1, 9);
            if (newShipCount != ship.shipCount) { ship.shipCount = newShipCount; EditorUtility.SetDirty(config); }

            // Body Type + Level
            EditorGUILayout.LabelField("Body", EditorStyles.boldLabel);
            int bodyIndex = System.Array.IndexOf(bodySubTypes, ship.bodySubType);
            if (bodyIndex < 0) bodyIndex = 0;
            int newBodyIndex = EditorGUILayout.Popup("Body Type", bodyIndex, bodySubTypeNames);
            if (newBodyIndex != bodyIndex)
            {
                ship.bodySubType = bodySubTypes[newBodyIndex];
                EditorUtility.SetDirty(config);
            }

            int newLevel = EditorGUILayout.IntSlider("Body Level", ship.bodyLevel, 1, 10);
            if (newLevel != ship.bodyLevel)
            {
                ship.bodyLevel = newLevel;
                EditorUtility.SetDirty(config);
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
                EditorUtility.SetDirty(config);
            }

            // 둘째 줄: Level — 비어있으면 비활성화
            EditorGUI.BeginDisabledGroup(slot.moduleSubType == EModuleSubType.none);
            int newLevel = EditorGUILayout.IntSlider("Level", slot.moduleLevel, 1, 10);
            if (newLevel != slot.moduleLevel)
            {
                slot.moduleLevel = newLevel;
                EditorUtility.SetDirty(config);
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
