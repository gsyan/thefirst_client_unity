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
    private Dictionary<int, bool> decorFoldouts = new Dictionary<int, bool>();          // 행성 배치 세트 폴드아웃

    private readonly Color zoneColor       = new Color(0.7f, 0.85f, 0.95f);
    private readonly Color shipColor       = new Color(0.85f, 0.95f, 0.85f);
    private readonly Color slotColor       = new Color(0.9f, 0.9f, 0.95f);
    private readonly Color multiplierColor = new Color(0.95f, 0.88f, 0.75f);
    private readonly Color decorColor      = new Color(0.88f, 0.95f, 0.88f);

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
            if (CommonUtility.GetModuleTypeFromSubType(subType) == EModuleType.body)
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

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Generate All Planets & Fleet Positions"))
        {
            if (EditorUtility.DisplayDialog("Generate All",
                "모든 존 그룹의 행성과 함선 포지션을 재생성합니다.\n기존 설정이 덮어써집니다.\n\n계속하시겠습니까?", "Yes", "Cancel"))
            {
                for (int gi = 0; gi < config.zoneGroups.Count; gi++)
                {
                    var group = config.zoneGroups[gi];
                    if (group.shipCount == 0) // 안전지역 — 행성 없음, 함대 원점 고정
                    {
                        group.spaceDecors = null;
                        foreach (var z in config.zones)
                            if (z.shipCount == 0) z.fleetPosition = Vector3.zero;
                        continue;
                    }
                    int seed = group.shipCount * 137 + gi;
                    group.spaceDecors = GenerateDefaultPlanets(seed);
                    GenerateFleetPositionsForGroup(group.shipCount, group.spaceDecors, seed);
                }
                EditorUtility.SetDirty(config);
                Debug.Log($"[DataTableZone] {config.zoneGroups.Count}개 그룹 행성 + 포지션 생성 완료");
            }
        }

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
                    // x-y 형식이 아닌 zone들 (Zone-0 포함) — 행성 세트는 groupConfig(shipCount=0)로 관리
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

            // 그룹 공유 설정 (행성 세트)
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

    // ZoneGroupConfig 편집 UI (행성 세트)
    private void DrawZoneGroupConfig(int shipCount)
    {
        // 해당 shipCount의 groupConfig 조회 또는 자동 생성
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
            EditorGUILayout.HelpBox("이 그룹의 행성 설정이 없습니다.", MessageType.Info);
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
        EditorGUILayout.Space(3);

        if (!decorFoldouts.ContainsKey(shipCount))
            decorFoldouts[shipCount] = false;

        decorFoldouts[shipCount] = EditorGUILayout.Foldout(
            decorFoldouts[shipCount],
            $"행성 배치 세트 ({groupConfig.spaceDecors?.Length ?? 0}개)", true);

        if (decorFoldouts[shipCount])
            DrawSpaceDecors(groupConfig);
    }

    // T = 30일 × (radius / 6000)^1.5
    // 30 — 공전 궤도의 반지름 6000일 때의 공전 주기(일). 이게 T0
    // 6000 — 기준이 되는 공전 궤도의 반지름 단위. 이게 r0
    private const float orbitBasePeriodDays = 30f; // 기준 반지름에서의 공전 주기(일)
    private const float orbitBaseRadius     = 6000f; // 기준 반지름 (Unity unit)

    // 케플러 T = orbitBasePeriodDays * (r/orbitBaseRadius)^1.5 기반 행성 4개 생성
    // seed로 존마다 다른 배치, 크기는 고정 (내행성 소형 암석 / 외행성 대형 가스)
    private SpaceDecorConfig[] GenerateDefaultPlanets(int seed)
    {
        var rng = new System.Random(seed);
        // (공전 궤도 기준 반지름, 고정 크기, 행성 스프라이트 인덱스 풀)
        var templates = new (float baseRadius, float scale, int[] spritePool)[]
        {
            (1200f,  5.5f, new[] { 3, 4, 5 }),   // 내행성1 — 소형 암석
            (2700f,  9f,   new[] { 1, 2, 7 }),   // 내행성2 — 중형 암석
            (5400f, 22f,   new[] { 2, 3, 7 }),   // 외행성1 — 대형 가스
            (13500f, 40f,  new[] { 1, 4, 5 }),   // 외행성2 — 초대형 가스
        };

        var result = new SpaceDecorConfig[templates.Length];
        var usedSprites = new System.Collections.Generic.HashSet<int>();

        for (int i = 0; i < templates.Length; i++)
        {
            var (baseRadius, scale, spritePool) = templates[i];

            // 반지름 ±15% 편차 (seed 기반, 존마다 다름)
            float radius = baseRadius * (0.85f + (float)rng.NextDouble() * 0.30f);
            // T(일) = 30 * (r/6000)^1.5
            float periodDays = orbitBasePeriodDays * Mathf.Pow(radius / orbitBaseRadius, 1.5f);
            float periodSeconds = periodDays * 86400f;

            // 중복 없는 스프라이트 선택 (seed 기반)
            int spriteIndex = spritePool[rng.Next(spritePool.Length)];
            for (int attempt = 0; attempt < 10 && usedSprites.Contains(spriteIndex); attempt++)
                spriteIndex = spritePool[rng.Next(spritePool.Length)];
            usedSprites.Add(spriteIndex);

            result[i] = new SpaceDecorConfig
            {
                type        = SpaceDecorType.Planet,
                spriteIndex = spriteIndex,
                orbitRadius = Mathf.Round(radius),
                orbitPeriod = Mathf.Round(periodSeconds),
                scale       = scale,
            };
        }
        return result;
    }

    // 행성 궤도 사이 안전 밴드에 30개 포지션 생성 → 해당 shipCount ZoneConfig에 순서대로 할당
    // 밴드: core(3) / 내1~내2 사이(7) / 내2~외1 사이(10) / 외1~외2 사이(10)
    private void GenerateFleetPositionsForGroup(int shipCount, SpaceDecorConfig[] planets, int seed)
    {
        // 실제 생성된 궤도 반지름 오름차순 정렬
        float[] r = new float[planets.Length];
        for (int i = 0; i < planets.Length; i++) r[i] = planets[i].orbitRadius;
        System.Array.Sort(r);

        // 안전 밴드: 행성 궤도와 겹치지 않는 구간 (궤도 ±12~18% 여유)
        var bands = new (float rMin, float rMax, int count)[]
        {
            (r[0] * 0.35f, r[0] * 0.70f,  3),   // core — 항성 근처
            (r[0] * 1.18f, r[1] * 0.82f,  7),   // 내행성1 ~ 내행성2 사이
            (r[1] * 1.12f, r[2] * 0.88f, 10),   // 내행성2 ~ 외행성1 사이
            (r[2] * 1.10f, r[3] * 0.90f, 10),   // 외행성1 ~ 외행성2 사이
        };

        var rng = new System.Random(seed + 9999);
        var positions = new Vector3[30];
        int posIdx = 0;

        foreach (var (rMin, rMax, count) in bands)
        {
            if (rMax <= rMin) continue; // 궤도가 너무 가까워 밴드 불성립 시 스킵
            float angleStep = 360f / count;
            float angleOffset = (float)(rng.NextDouble() * 360f); // 존마다 다른 시작 각도
            for (int j = 0; j < count && posIdx < 30; j++)
            {
                float radius = rMin + (float)rng.NextDouble() * (rMax - rMin);
                float angleRad = (angleOffset + angleStep * j) * Mathf.Deg2Rad;
                positions[posIdx++] = new Vector3(
                    radius * Mathf.Cos(angleRad),
                    0f,
                    radius * Mathf.Sin(angleRad));
            }
        }

        // 해당 shipCount ZoneConfig에 순서대로 할당
        int fillIdx = 0;
        for (int i = 0; i < config.zones.Count && fillIdx < 30; i++)
        {
            if (config.zones[i].shipCount == shipCount)
                config.zones[i].fleetPosition = positions[fillIdx++];
        }
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
            EditorGUILayout.LabelField("아군 함대 위치 (절대 좌표)", EditorStyles.boldLabel);
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
                var ship = new EnemyShipConfig { bodySubType = EModuleSubType.body_t1_std_ver1, bodyLevel = 1 };
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

    private string FormatOrbitPeriod(float seconds)
    {
        if (seconds <= 0f) return "정지";
        float days = seconds / 86400f;
        if (days < 1f)   return $"약 {seconds / 3600f:F1}시간";
        if (days < 365f) return $"약 {days:F1}일";
        return $"약 {days / 365f:F2}년";
    }

    private EModuleSubType[] GetSubTypesForModuleType(EModuleType moduleType)
    {
        var subTypes = new List<EModuleSubType> { EModuleSubType.none };
        foreach (EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if (CommonUtility.GetModuleTypeFromSubType(subType) == moduleType)
                subTypes.Add(subType);
        }
        return subTypes.ToArray();
    }

    private void DrawSpaceDecors(ZoneGroupConfig groupConfig)
    {
        groupConfig.spaceDecors = DrawSpaceDecorsInternal(groupConfig.spaceDecors);
    }

    // 배열을 받아 편집 UI를 그리고 수정된 배열을 반환
    private SpaceDecorConfig[] DrawSpaceDecorsInternal(SpaceDecorConfig[] decors)
    {
        var origColor = GUI.backgroundColor;
        GUI.backgroundColor = decorColor;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = origColor;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("", EditorStyles.boldLabel);
        if (GUILayout.Button("+ Add", GUILayout.Width(60)))
        {
            var list = decors != null ? new List<SpaceDecorConfig>(decors) : new List<SpaceDecorConfig>();
            list.Add(new SpaceDecorConfig { type = SpaceDecorType.Planet, spriteIndex = 1, scale = 10f });
            decors = list.ToArray();
            EditorUtility.SetDirty(config);
        }
        EditorGUILayout.EndHorizontal();

        if (decors != null && decors.Length > 0)
        {
            var list = new List<SpaceDecorConfig>(decors);
            int deleteIndex = -1;

            for (int i = 0; i < list.Count; i++)
            {
                var decor = list[i];
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                decor.type = (SpaceDecorType)EditorGUILayout.EnumPopup("Type", decor.type);
                if (GUILayout.Button("X", GUILayout.Width(22))) deleteIndex = i;
                EditorGUILayout.EndHorizontal();

                int maxIndex = decor.type == SpaceDecorType.Asteroid ? 10 : 7;
                decor.spriteIndex = EditorGUILayout.IntSlider("Sprite Index", decor.spriteIndex, 1, maxIndex);
                decor.orbitRadius = EditorGUILayout.FloatField("Orbit Radius", decor.orbitRadius);

                float days = decor.orbitPeriod / 86400f;
                float newDays = EditorGUILayout.FloatField("Orbit Period (days)", days);
                if (!Mathf.Approximately(newDays, days))
                    decor.orbitPeriod = newDays * 86400f;
                EditorGUILayout.LabelField("", FormatOrbitPeriod(decor.orbitPeriod), EditorStyles.miniLabel);
                decor.scale = EditorGUILayout.FloatField("Scale", decor.scale);

                list[i] = decor;
                EditorGUILayout.EndVertical();
            }

            if (deleteIndex >= 0)
            {
                list.RemoveAt(deleteIndex);
                EditorUtility.SetDirty(config);
            }

            decors = list.ToArray();
        }

        EditorGUILayout.EndVertical();
        return decors;
    }

    // Body 프리팹에서 슬롯 정보 추출
    private void RefreshShipModuleSlots(EnemyShipConfig ship)
    {
        ship.moduleSlots = new List<EnemyModuleSlotConfig>();

        string prefabPath = ObjectManager.GetShipModulePrefabPath(
            EModuleType.body.ToString(),
            ship.bodySubType.ToString(),
            ship.bodyLevel);
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
