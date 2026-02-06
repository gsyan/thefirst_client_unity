#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(DataTableZone))]
public class DataTableZoneEditor : Editor
{
    private DataTableZone config;
    private Vector2 scrollPosition;

    private Dictionary<int, bool> zoneFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, Dictionary<int, bool>> waveFoldouts = new Dictionary<int, Dictionary<int, bool>>();
    private Dictionary<int, Dictionary<int, Dictionary<int, bool>>> shipFoldouts = new Dictionary<int, Dictionary<int, Dictionary<int, bool>>>();
    private Dictionary<int, bool> shipCountGroupFoldouts = new Dictionary<int, bool>(); // x값(함선개수) 그룹 폴드아웃

    private readonly Color zoneColor = new Color(0.7f, 0.85f, 0.95f);
    private readonly Color waveColor = new Color(0.95f, 0.85f, 0.7f);
    private readonly Color shipColor = new Color(0.85f, 0.95f, 0.85f);
    private readonly Color slotColor = new Color(0.9f, 0.9f, 0.95f);

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
            if (CommonUtility.GetModuleTypeFromSubType(subType) == EModuleType.Body)
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
                zoneDescription = "New Zone",
                waves = new List<WaveConfig>()
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
        if (GUILayout.Button("Generate Default Zones (1-1 ~ 9-10)"))
        {
            if (EditorUtility.DisplayDialog("Generate Default Zones",
                "Zone 1-1 ~ 9-10을 기본 데이터로 생성합니다.\n(x-y: x=함선개수, y=스테이지)\n- Wave 수 = y (스테이지)\n- 레벨 = min(x, y)\n기존 데이터가 삭제됩니다.\n\n계속하시겠습니까?", "Yes", "Cancel"))
            {
                GenerateDefaultZones();
            }
        }

        if (GUILayout.Button("Validate All Ships"))
        {
            ValidateAllShips();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // x-y 형식: x=함선 개수(1~9), y=스테이지(1~10), moduleLevel=min(y, x)
    private void GenerateDefaultZones()
    {
        config.zones.Clear();
        int totalZones = 0;

        // Zone-0: 안전지역 (적 없음)
        Material safeZoneSkybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/DeepSpaceSkyboxPack/DiverseSpace/Material/DiverseSpaceMaterial.mat");
        var safeZone = new ZoneConfig
        {
            zoneName = "Zone-0",
            zoneDescription = "안전지역",
            shipCount = 0,
            moduleLevel = 0,
            skyboxMaterial = safeZoneSkybox,
            waves = new List<WaveConfig>()
        };
        config.zones.Add(safeZone);
        totalZones++;

        float tempMineralPerHour = 3600f;

        for (int shipCount = 1; shipCount <= 9; shipCount++)
        {
            for (int stage = 1; stage <= 10; stage++)
            {
                // 레벨은 스테이지와 함선 개수 중 작은 값 (레벨 상한 = 함선 개수)
                int moduleLevel = Mathf.Min(stage, shipCount);

                // 스카이박스 머티리얼: 1~5는 GalacticGreen, 6~9는 GalaxyFire
                string skyboxFolder = shipCount <= 5 ? "GalacticGreen" : "GalaxyFire";
                Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/DeepSpaceSkyboxPack/{skyboxFolder}/Material/{skyboxFolder}Material.mat");
                
                var zone = new ZoneConfig
                {
                    zoneName = $"{shipCount}-{stage}",
                    zoneDescription = $"함선 {shipCount}척, 모듈 Lv.{moduleLevel}",
                    shipCount = shipCount,
                    moduleLevel = moduleLevel,
                    skyboxMaterial = skyboxMat,
                    waves = new List<WaveConfig>(),
                    clearMineral = 1000f + (shipCount - 1) * 1000,
                    clearMineralRare = 0,
                    clearMineralExotic = 0,
                    clearMineralDark = 0,
                    mineralPerHour = tempMineralPerHour,
                    mineralRarePerHour = 0,
                    mineralExoticPerHour = 0,
                    mineralDarkPerHour = 0
                };
                tempMineralPerHour += 100;

                // stage 수만큼 wave 생성
                for (int w = 0; w < stage; w++)
                {
                    var wave = new WaveConfig
                    {
                        delayBeforeWave = 5f,
                        enemyShips = new List<EnemyShipConfig>()
                    };

                    // 각 wave에 shipCount개의 적 함선 생성
                    for (int s = 0; s < shipCount; s++)
                    {
                        var ship = new EnemyShipConfig
                        {
                            bodySubType = EModuleSubType.Body_Battle,
                            bodyLevel = moduleLevel
                        };
                        RefreshShipModuleSlots(ship);
                        ApplyLevelBasedSlotRestrictions(ship, moduleLevel);
                        wave.enemyShips.Add(ship);
                    }
                    zone.waves.Add(wave);
                }
                config.zones.Add(zone);
                totalZones++;
            }
            tempMineralPerHour += 1000;
        }
        EditorUtility.SetDirty(config);
        EditorUtility.DisplayDialog("Complete", $"Zone 1-1 ~ 9-10 생성 완료! (총 {totalZones}개)", "OK");
    }

    // 레벨(y)에 따른 슬롯 제한 적용
    // 1-1,1-2: beam 1 / 1-3,1-4: beam 2 / 1-5,1-6: beam 2, missile 1
    private void ApplyLevelBasedSlotRestrictions(EnemyShipConfig ship, int level)
    {
        if (ship.moduleSlots == null) return;

        int maxBeam = 0, maxMissile = 0, maxHanger = 0;

        if (level <= 2)
        {
            maxBeam = 1; maxMissile = 0; maxHanger = 0;
        }
        else if (level <= 4)
        {
            maxBeam = 2; maxMissile = 0; maxHanger = 0;
        }
        else if (level <= 6)
        {
            maxBeam = 2; maxMissile = 1; maxHanger = 0;
        }
        else
        {
            // 레벨 7 이상: 제한 없음
            return;
        }

        int beamCount = 0, missileCount = 0, hangerCount = 0;
        for (int i = ship.moduleSlots.Count - 1; i >= 0; i--)
        {
            var slot = ship.moduleSlots[i];
            bool remove = false;

            switch (slot.slotType)
            {
                case EModuleType.Beam:
                    beamCount++;
                    if (beamCount > maxBeam) remove = true;
                    break;
                case EModuleType.Missile:
                    missileCount++;
                    if (missileCount > maxMissile) remove = true;
                    break;
                case EModuleType.Hanger:
                    hangerCount++;
                    if (hangerCount > maxHanger) remove = true;
                    break;
            }

            if (remove)
                ship.moduleSlots.RemoveAt(i);
        }
    }

    private void ValidateAllShips()
    {
        int totalShips = 0;
        int invalidShips = 0;

        foreach (var zone in config.zones)
        {
            foreach (var wave in zone.waves)
            {
                foreach (var ship in wave.enemyShips)
                {
                    totalShips++;
                    if (ship.moduleSlots == null || ship.moduleSlots.Count == 0)
                    {
                        invalidShips++;
                        RefreshShipModuleSlots(ship);
                    }
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
                    // x-y 형식이 아닌 zone들
                    foreach (var (index, zone) in group.Value)
                    {
                        DrawZone(index);
                        EditorGUILayout.Space(5);
                    }
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
            $"함선 {shipCount}척 ({zones.Count}개 Zone: {shipCount}-1 ~ {shipCount}-{zones.Count})",
            true, EditorStyles.foldoutHeader);

        if (shipCountGroupFoldouts[shipCount])
        {
            EditorGUI.indentLevel++;
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
        zoneFoldouts[zoneIndex] = EditorGUILayout.Foldout(zoneFoldouts[zoneIndex],
            $"Zone {zoneIndex}: {zone.zoneName} (Waves: {zone.TotalWaveCount}, Ships: {zone.TotalEnemyShipCount})", true, EditorStyles.foldoutHeader);

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
            zone.skyboxMaterial = (Material)EditorGUILayout.ObjectField("Skybox Material", zone.skyboxMaterial, typeof(Material), false);
            EditorGUILayout.EndVertical();

            // 클리어 보상
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("클리어 보상", EditorStyles.boldLabel);
            zone.clearMineral = EditorGUILayout.FloatField("Mineral", zone.clearMineral);
            zone.clearMineralRare = EditorGUILayout.FloatField("MineralRare", zone.clearMineralRare);
            zone.clearMineralExotic = EditorGUILayout.FloatField("MineralExotic", zone.clearMineralExotic);
            zone.clearMineralDark = EditorGUILayout.FloatField("MineralDark", zone.clearMineralDark);
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

            // Wave List Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Waves ({zone.waves.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add Wave", GUILayout.Width(100)))
            {
                zone.waves.Add(new WaveConfig
                {
                    delayBeforeWave = 5f,
                    enemyShips = new List<EnemyShipConfig>()
                });
                EditorUtility.SetDirty(config);
            }
            EditorGUILayout.EndHorizontal();

            // Waves
            for (int waveIndex = 0; waveIndex < zone.waves.Count; waveIndex++)
            {
                DrawWave(zoneIndex, waveIndex, zone.waves[waveIndex]);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawWave(int zoneIndex, int waveIndex, WaveConfig wave)
    {
        if (!waveFoldouts.ContainsKey(zoneIndex))
            waveFoldouts[zoneIndex] = new Dictionary<int, bool>();
        if (!waveFoldouts[zoneIndex].ContainsKey(waveIndex))
            waveFoldouts[zoneIndex][waveIndex] = false;

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = waveColor;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        // Wave Header
        EditorGUILayout.BeginHorizontal();
        waveFoldouts[zoneIndex][waveIndex] = EditorGUILayout.Foldout(waveFoldouts[zoneIndex][waveIndex],
            $"Wave {waveIndex + 1} (Delay: {wave.delayBeforeWave}s, Ships: {wave.enemyShips.Count})", true);

        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            config.zones[zoneIndex].waves.RemoveAt(waveIndex);
            EditorUtility.SetDirty(config);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (waveFoldouts[zoneIndex][waveIndex])
        {
            EditorGUI.indentLevel++;

            wave.delayBeforeWave = EditorGUILayout.Slider("Delay Before Wave (sec)", wave.delayBeforeWave, 0f, 60f);

            EditorGUILayout.Space(3);

            // Ship List Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Enemy Ships ({wave.enemyShips.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add Ship", GUILayout.Width(100)))
            {
                var ship = new EnemyShipConfig
                {
                    bodySubType = EModuleSubType.Body_Battle,
                    bodyLevel = 1
                };
                RefreshShipModuleSlots(ship);
                wave.enemyShips.Add(ship);
                EditorUtility.SetDirty(config);
            }
            EditorGUILayout.EndHorizontal();

            // Ships
            for (int shipIndex = 0; shipIndex < wave.enemyShips.Count; shipIndex++)
            {
                DrawEnemyShip(zoneIndex, waveIndex, shipIndex, wave.enemyShips[shipIndex]);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawEnemyShip(int zoneIndex, int waveIndex, int shipIndex, EnemyShipConfig ship)
    {
        if (!shipFoldouts.ContainsKey(zoneIndex))
            shipFoldouts[zoneIndex] = new Dictionary<int, Dictionary<int, bool>>();
        if (!shipFoldouts[zoneIndex].ContainsKey(waveIndex))
            shipFoldouts[zoneIndex][waveIndex] = new Dictionary<int, bool>();
        if (!shipFoldouts[zoneIndex][waveIndex].ContainsKey(shipIndex))
            shipFoldouts[zoneIndex][waveIndex][shipIndex] = false;

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = shipColor;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        // Ship Header
        EditorGUILayout.BeginHorizontal();
        string slotInfo = ship.moduleSlots != null ? $", Slots: {ship.moduleSlots.Count}" : "";
        shipFoldouts[zoneIndex][waveIndex][shipIndex] = EditorGUILayout.Foldout(
            shipFoldouts[zoneIndex][waveIndex][shipIndex],
            $"Ship {shipIndex + 1}: {ship.bodySubType} Lv.{ship.bodyLevel}{slotInfo}", true);

        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            config.zones[zoneIndex].waves[waveIndex].enemyShips.RemoveAt(shipIndex);
            EditorUtility.SetDirty(config);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (shipFoldouts[zoneIndex][waveIndex][shipIndex])
        {
            EditorGUI.indentLevel++;

            // Body Type + Level
            EditorGUILayout.LabelField("Body", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            int bodyIndex = System.Array.IndexOf(bodySubTypes, ship.bodySubType);
            if (bodyIndex < 0) bodyIndex = 0;
            int newBodyIndex = EditorGUILayout.Popup("Body Type", bodyIndex, bodySubTypeNames);
            if (newBodyIndex != bodyIndex)
                ship.bodySubType = bodySubTypes[newBodyIndex];

            int newLevel = EditorGUILayout.IntSlider("Body Level", ship.bodyLevel, 1, 10);
            if (newLevel != ship.bodyLevel)
                ship.bodyLevel = newLevel;

            if (EditorGUI.EndChangeCheck())
            {
                // Body 또는 Level 변경 시 슬롯 정보 갱신
                RefreshShipModuleSlots(ship);
                EditorUtility.SetDirty(config);
            }

            // Refresh 버튼
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh Slots", GUILayout.Width(100)))
            {
                RefreshShipModuleSlots(ship);
                EditorUtility.SetDirty(config);
            }
            EditorGUILayout.EndHorizontal();

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
        // 타입별로 그룹화
        var engineSlots = ship.moduleSlots.Where(s => s.slotType == EModuleType.Engine).OrderBy(s => s.slotIndex).ToList();
        var beamSlots = ship.moduleSlots.Where(s => s.slotType == EModuleType.Beam).OrderBy(s => s.slotIndex).ToList();
        var missileSlots = ship.moduleSlots.Where(s => s.slotType == EModuleType.Missile).OrderBy(s => s.slotIndex).ToList();
        var hangerSlots = ship.moduleSlots.Where(s => s.slotType == EModuleType.Hanger).OrderBy(s => s.slotIndex).ToList();

        if (engineSlots.Count > 0)
            DrawSlotGroup("Engine", engineSlots, EModuleType.Engine, ship);
        if (beamSlots.Count > 0)
            DrawSlotGroup("Beam", beamSlots, EModuleType.Beam, ship);
        if (missileSlots.Count > 0)
            DrawSlotGroup("Missile", missileSlots, EModuleType.Missile, ship);
        if (hangerSlots.Count > 0)
            DrawSlotGroup("Hanger", hangerSlots, EModuleType.Hanger, ship);
    }

    private void DrawSlotGroup(string groupName, List<EnemyModuleSlotConfig> slots, EModuleType moduleType, EnemyShipConfig ship)
    {
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = slotColor;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        EditorGUILayout.LabelField($"{groupName} ({slots.Count})", EditorStyles.boldLabel);

        // 해당 타입의 SubType 목록 가져오기
        var subTypes = GetSubTypesForModuleType(moduleType);
        var subTypeNames = subTypes.Select(t => t.ToString()).ToArray();

        EnemyModuleSlotConfig slotToRemove = null;
        foreach (var slot in slots)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"[{slot.slotIndex}]", GUILayout.Width(30));

            // SubType 선택
            int currentIndex = System.Array.IndexOf(subTypes, slot.moduleSubType);
            if (currentIndex < 0) currentIndex = 0;
            int newIndex = EditorGUILayout.Popup(currentIndex, subTypeNames, GUILayout.MinWidth(120));
            if (newIndex != currentIndex)
                slot.moduleSubType = subTypes[newIndex];

            // Level 선택
            EditorGUILayout.LabelField("Lv.", GUILayout.Width(25));
            slot.moduleLevel = EditorGUILayout.IntSlider(slot.moduleLevel, 1, 10);

            // 제거 버튼
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                slotToRemove = slot;
            }

            EditorGUILayout.EndHorizontal();
        }

        // 루프 밖에서 제거 (foreach 중 제거 방지)
        if (slotToRemove != null)
        {
            ship.moduleSlots.Remove(slotToRemove);
            EditorUtility.SetDirty(config);
        }

        EditorGUILayout.EndVertical();
    }

    private EModuleSubType[] GetSubTypesForModuleType(EModuleType moduleType)
    {
        var subTypes = new List<EModuleSubType>();
        foreach (EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if (CommonUtility.GetModuleTypeFromSubType(subType) == moduleType)
                subTypes.Add(subType);
        }
        return subTypes.ToArray();
    }

    // Body 프리팹에서 슬롯 정보 추출
    private void RefreshShipModuleSlots(EnemyShipConfig ship)
    {
        ship.moduleSlots = new List<EnemyModuleSlotConfig>();

        string prefabPath = ObjectManager.GetShipModulePrefabPath(
            EModuleType.Body.ToString(),
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
            var slotConfig = new EnemyModuleSlotConfig(
                slot.m_moduleSlotInfo.moduleType,
                slot.m_moduleSlotInfo.slotIndex
            );
            // 기본값으로 body level과 동일하게 설정
            slotConfig.moduleLevel = ship.bodyLevel;
            ship.moduleSlots.Add(slotConfig);
        }

        Debug.Log($"Loaded {ship.moduleSlots.Count} slots from {prefabPath}");
    }
}
#endif
