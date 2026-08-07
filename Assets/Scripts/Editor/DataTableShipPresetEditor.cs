// DataTableShipPreset 커스텀 에디터 — 함선 프리셋 Inspector UI 및 CSV Import/Export 툴
// CSV 경로: Assets/Resources/DataTable/ShipPreset/datatable_ship_preset.csv (식별+스칼라 스탯)
//          Assets/Resources/DataTable/ShipPreset/modules_in_preset.csv (장착 모듈, preset_id당 여러 행)
// Import는 반드시 Preset CSV → Modules CSV 순서로 처리(모듈 슬롯 배열이 Preset Import 시점에 먼저 준비되어야 함)

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DataTableShipPreset))]
public class DataTableShipPresetEditor : Editor
{
    private DataTableShipPreset dataTable;
    private bool m_foldout = false;
    private ShipStatFormulaSettings m_formula;
    private DataTableModule m_moduleTable;

    // prefabName → 타입별 슬롯 개수 캐시 (Resources.Load 반복 호출 방지)
    private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<EModuleType, int>> m_slotCountCache
        = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<EModuleType, int>>();

    private void OnEnable()
    {
        dataTable = (DataTableShipPreset)target;
        m_formula = LoadFormula();
        m_moduleTable = LoadModuleTable();
    }

    private ShipStatFormulaSettings LoadFormula()
    {
        string[] guids = AssetDatabase.FindAssets("t:DataTableConfig");
        if (guids.Length == 0) return null;

        DataTableConfig config = AssetDatabase.LoadAssetAtPath<DataTableConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (config == null || config.gameSettings == null) return null;

        return config.gameSettings.shipStatFormula;
    }

    private DataTableModule LoadModuleTable()
    {
        string[] guids = AssetDatabase.FindAssets("t:DataTableModule");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<DataTableModule>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // prefabName에 실제 적용된 바디 프리팹(Prefabs/ShipModule/Body/{prefabName})을 분석해 타입별 슬롯 개수를 반환
    // DataTableModule.ExtractModuleSlotsFromPrefab과 동일한 방식(ModuleSlot 컴포넌트 수집)
    private System.Collections.Generic.Dictionary<EModuleType, int> GetSlotCountsForPrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
            return null;

        if (m_slotCountCache.TryGetValue(prefabName, out var cached))
            return cached;

        var counts = new System.Collections.Generic.Dictionary<EModuleType, int>();

        string prefabPath = $"Prefabs/ShipModule/Body/{prefabName}";
        GameObject prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab != null)
        {
            ModuleSlot[] slots = prefab.GetComponentsInChildren<ModuleSlot>(true);
            foreach (ModuleSlot slot in slots)
            {
                EModuleType moduleType = slot.m_moduleSlotInfo.moduleType;
                counts.TryGetValue(moduleType, out int current);
                counts[moduleType] = current + 1;
            }
        }

        m_slotCountCache[prefabName] = counts;
        return counts;
    }

    private int GetSlotCount(System.Collections.Generic.Dictionary<EModuleType, int> slotCounts, EModuleType moduleType)
    {
        if (slotCounts == null) return int.MaxValue; // 프리팹 분석 실패 시 기존처럼 배열 전체 표시
        slotCounts.TryGetValue(moduleType, out int count);
        return count;
    }

    // EModuleSubType 전체 이름 캐시 — prefix(예: "beam_")로 필터링해 카테고리별 드롭다운 옵션을 만드는 데 사용
    private static string[] s_allSubTypeNames;

    private string[] GetSubTypeOptions(string prefix)
    {
        if (s_allSubTypeNames == null)
            s_allSubTypeNames = System.Enum.GetNames(typeof(EModuleSubType));

        var options = new System.Collections.Generic.List<string> { "" }; // 인덱스 0 = 미장착(빈 문자열)
        foreach (string name in s_allSubTypeNames)
        {
            if (name.StartsWith(prefix))
                options.Add(name);
        }
        return options.ToArray();
    }

    // subTypeProp(EModuleSubType 이름 문자열, 빈 문자열=미장착)을 prefix로 필터링한 드롭다운으로 편집
    private void DrawSubTypePopup(SerializedProperty subTypeProp, string prefix, float width)
    {
        string[] values = GetSubTypeOptions(prefix);
        string[] displayNames = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
            displayNames[i] = string.IsNullOrEmpty(values[i]) ? "(none)" : values[i];

        int currentIndex = System.Array.IndexOf(values, subTypeProp.stringValue);
        if (currentIndex < 0) currentIndex = 0;

        int newIndex = EditorGUILayout.Popup(currentIndex, displayNames, GUILayout.Width(width));
        subTypeProp.stringValue = values[newIndex];
    }

    private void EnsureArraySize(SerializedProperty arrayProp, int size)
    {
        if (arrayProp.arraySize != size)
            arrayProp.arraySize = size;
    }

    // prefabName(예: body_t1_m1)에 대응하는 DataTableModule 원본 데이터 — Health/Repair/TurnRate 기본 수치 출처
    private ModuleData GetBodyModuleData(string prefabName)
    {
        if (m_moduleTable == null || string.IsNullOrEmpty(prefabName)) return null;
        if (System.Enum.TryParse(prefabName, out EModuleSubType bodySubType) == false) return null;
        return m_moduleTable.GetModuleDataFromTable(bodySubType);
    }

    // 슬롯 서브타입(예: beam_t2_m1) → DataTableModule의 해당 subType(level 1) cost_mp — ShipStatAllocation.GetInstallCost와 동일 규칙
    private int GetInstallCostForSubType(string subTypeName)
    {
        if (m_moduleTable == null || string.IsNullOrEmpty(subTypeName)) return 0;
        if (System.Enum.TryParse(subTypeName, out EModuleSubType subType) == false) return 0;
        ModuleData data = m_moduleTable.GetModuleDataFromTable(subType);
        return data != null ? data.statPoint : 0;
    }

    public override void OnInspectorGUI()
    {
        if (dataTable == null) return;

        serializedObject.Update();

        DrawCsvTools();
        EditorGUILayout.Space(10);
        DrawShipPresetList();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTable);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawShipPresetList()
    {
        SerializedProperty listProp = serializedObject.FindProperty("shipPresetDataList");

        EditorGUILayout.BeginVertical("box");
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.8f, 0.8f, 1.0f);
        m_foldout = EditorGUILayout.Foldout(m_foldout, $"Ship Preset Data ({listProp.arraySize})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (m_foldout)
        {
            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty elementProp = listProp.GetArrayElementAtIndex(i);
                DrawShipPresetElement(elementProp, i);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawShipPresetElement(SerializedProperty elementProp, int index)
    {
        SerializedProperty presetIdProp = elementProp.FindPropertyRelative("presetId");
        string label = string.IsNullOrEmpty(presetIdProp.stringValue) ? $"Element {index}" : presetIdProp.stringValue;

        EditorGUILayout.BeginVertical("box");
        elementProp.isExpanded = EditorGUILayout.Foldout(elementProp.isExpanded, label, true, EditorStyles.foldoutHeader);

        if (elementProp.isExpanded == false)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.PropertyField(elementProp.FindPropertyRelative("unlockCommanderLevel"), new GUIContent("Unlock Commander Level", "이 값 이상의 커맨더 레벨부터 사용 가능 (예: 10 = 10레벨부터)"));
        EditorGUILayout.PropertyField(presetIdProp, new GUIContent("Preset Id"));

        SerializedProperty prefabNameProp = elementProp.FindPropertyRelative("prefabName");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Prefab Name", GUILayout.Width(EditorGUIUtility.labelWidth));
        DrawSubTypePopup(prefabNameProp, "body_", 200);
        EditorGUILayout.LabelField($"장착 {GetInstallCostForSubType(prefabNameProp.stringValue)}", GUILayout.Width(70));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(6);
        SerializedProperty allocProp = elementProp.FindPropertyRelative("statAllocation");
        SerializedProperty commandCostProp = elementProp.FindPropertyRelative("commandCost");
        string prefabName = elementProp.FindPropertyRelative("prefabName").stringValue;
        DrawStatAllocation(allocProp, commandCostProp, prefabName);

        EditorGUILayout.EndVertical();
    }

    // commandCost는 CSV Import 시점뿐 아니라 인스펙터를 그릴 때마다 GetTotalPointsUsed()로 실시간 덮어써서 항상 동기화
    private void DrawStatAllocation(SerializedProperty alloc, SerializedProperty commandCostProp, string prefabName)
    {
        int totalPoints = BuildAllocationSnapshot(alloc).GetTotalPointsUsed(m_moduleTable, prefabName);
        commandCostProp.intValue = totalPoints;
        EditorGUILayout.LabelField("Command Cost (자동계산)", totalPoints.ToString(), EditorStyles.boldLabel);
        if (m_moduleTable == null)
            EditorGUILayout.HelpBox("DataTableModule을 찾을 수 없어 장착 코스트가 0으로 표시됩니다.", MessageType.Warning);

        var slotCounts = GetSlotCountsForPrefab(prefabName);
        if (string.IsNullOrEmpty(prefabName) == false && slotCounts == null)
            EditorGUILayout.HelpBox($"프리팹을 찾을 수 없어 슬롯을 분석하지 못했습니다: Prefabs/ShipModule/Body/{prefabName}", MessageType.Warning);

        ShipStatFormulaSettings formula = m_formula;
        if (formula == null)
        {
            formula = new ShipStatFormulaSettings();
            EditorGUILayout.HelpBox("DataTableConfig를 찾을 수 없어 기본 수치가 0으로 표시됩니다.", MessageType.Warning);
        }

        ModuleData bodyModuleData = GetBodyModuleData(prefabName);
        float baseHealth = 0f;
        float baseRepair = 0f;
        float baseTurnRate = 0f;
        if (bodyModuleData != null)
        {
            baseHealth = bodyModuleData.health;
            baseRepair = bodyModuleData.repair;
            baseTurnRate = bodyModuleData.turnRate;
        }
        else if (string.IsNullOrEmpty(prefabName) == false)
        {
            EditorGUILayout.HelpBox($"DataTableModule에서 {prefabName}의 기본 수치를 찾을 수 없습니다.", MessageType.Warning);
        }

        EditorGUILayout.LabelField("Flat Stats", EditorStyles.boldLabel);
        DrawStatPoint(alloc.FindPropertyRelative("healthPoints"), "Health Points", baseHealth, formula.flatStats.perPoint);
        DrawStatPoint(alloc.FindPropertyRelative("turnRatePoints"), "Turn Rate Points", baseTurnRate, formula.flatStats.perPoint);
        DrawStatPoint(alloc.FindPropertyRelative("repairPoints"), "Repair Points", baseRepair, formula.flatStats.perPoint);

        EditorGUILayout.LabelField("Beam", EditorStyles.boldLabel);
        DrawWeaponSlots(alloc, "beamModuleSubType", "beam_", "Beam", GetSlotCount(slotCounts, EModuleType.beam),
            "beamAttackPoints", "beamFireRatePoints", "beamProjectileSpeedPoints", null,
            formula.beam.attackPerPoint, formula.beam.attackCoolReductionPerPoint, formula.beam.attackCoolFloor,
            formula.beam.projectileSpeedPerPoint, 0f);

        EditorGUILayout.LabelField("Missile", EditorStyles.boldLabel);
        DrawWeaponSlots(alloc, "missileModuleSubType", "missile_", "Missile", GetSlotCount(slotCounts, EModuleType.missile),
            "missileAttackPoints", "missileFireRatePoints", "missileProjectileSpeedPoints", "missileSilencePoints",
            formula.missile.attackPerPoint, formula.missile.attackCoolReductionPerPoint, formula.missile.attackCoolFloor,
            formula.missile.projectileSpeedPerPoint, formula.missile.silenceTimePerPoint);

        EditorGUILayout.LabelField("Hangar", EditorStyles.boldLabel);
        DrawHangarSlots(alloc, GetSlotCount(slotCounts, EModuleType.hanger), formula.hangar);

        int shieldSlotCount = GetSlotCount(slotCounts, EModuleType.shield);
        if (shieldSlotCount > 0)
        {
            EditorGUILayout.LabelField("Shield", EditorStyles.boldLabel);
            DrawShieldSubType(alloc);
            ModuleData shieldModuleData = GetModuleDataForSubType(alloc.FindPropertyRelative("shieldModuleSubType").stringValue);
            float baseShieldGauge = shieldModuleData != null ? shieldModuleData.shieldGauge : 0f;
            float baseShieldDelay = shieldModuleData != null ? shieldModuleData.shieldDelay : 0f;
            float baseShieldRegenRate = shieldModuleData != null ? shieldModuleData.shieldRegenRate : 0f;
            DrawStatPoint(alloc.FindPropertyRelative("shieldGaugePoints"), "Shield Gauge Points", baseShieldGauge, formula.shield.gaugePerPoint);
            DrawStatPoint(alloc.FindPropertyRelative("shieldDelayPoints"), "Shield Delay Points", baseShieldDelay, -formula.shield.delayReductionPerPoint, formula.shield.delayFloor);
            DrawStatPoint(alloc.FindPropertyRelative("shieldRegenRatePoints"), "Shield Regen Rate Points", baseShieldRegenRate, formula.shield.regenRatePerPoint);
        }

        int interceptorSlotCount = GetSlotCount(slotCounts, EModuleType.interceptor);
        if (interceptorSlotCount > 0)
        {
            EditorGUILayout.LabelField("Interceptor", EditorStyles.boldLabel);
            DrawInterceptorSlots(alloc, interceptorSlotCount, formula.interceptor);
        }
    }

    // 빔/미사일 공용 UI — 슬롯별로 서브타입 드롭다운(빈 칸=미장착) + 속성별 강화 포인트(공격력/연사력/발사체속도, 미사일은 침묵시간 추가)
    // 장착 코스트 및 기본 수치(공격력/쿨다운/발사체속도/침묵시간)는 선택된 서브타입(티어)마다 DataTableModule에서 즉시 조회해 표시 — formula는 포인트당 증감 계수만 제공
    // silenceTimePerPoint는 미사일 전용(빔은 0 전달, silenceField가 null이라 실제로는 그려지지 않음)
    private void DrawWeaponSlots(SerializedProperty alloc, string subTypeField, string subTypePrefix, string slotLabel, int maxSlots,
        string attackField, string fireRateField, string projectileSpeedField, string silenceField,
        float attackPerPoint, float attackCoolReductionPerPoint, float attackCoolFloor,
        float projectileSpeedPerPoint, float silenceTimePerPoint)
    {
        SerializedProperty subTypeArray = alloc.FindPropertyRelative(subTypeField);
        SerializedProperty attackArray = alloc.FindPropertyRelative(attackField);
        SerializedProperty fireRateArray = alloc.FindPropertyRelative(fireRateField);
        SerializedProperty projectileSpeedArray = alloc.FindPropertyRelative(projectileSpeedField);
        SerializedProperty silenceArray = string.IsNullOrEmpty(silenceField) ? null : alloc.FindPropertyRelative(silenceField);

        // 새로 추가된 포인트 배열은 기존 데이터에서 크기 0으로 직렬화돼 있으므로 서브타입 배열 크기에 맞춰 보정
        EnsureArraySize(attackArray, subTypeArray.arraySize);
        EnsureArraySize(fireRateArray, subTypeArray.arraySize);
        EnsureArraySize(projectileSpeedArray, subTypeArray.arraySize);
        if (silenceArray != null)
            EnsureArraySize(silenceArray, subTypeArray.arraySize);

        int slotCountToShow = Mathf.Min(subTypeArray.arraySize, maxSlots);

        for (int i = 0; i < slotCountToShow; i++)
        {
            SerializedProperty subTypeProp = subTypeArray.GetArrayElementAtIndex(i);
            bool installed = string.IsNullOrEmpty(subTypeProp.stringValue) == false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{slotLabel} {i + 1}", GUILayout.Width(60));
            DrawSubTypePopup(subTypeProp, subTypePrefix, 120);
            EditorGUILayout.LabelField($"장착 {GetInstallCostForSubType(subTypeProp.stringValue)}", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            ModuleData moduleData = GetModuleDataForSubType(subTypeProp.stringValue);
            float baseAttack = moduleData != null ? moduleData.attack : 0f;
            float baseAttackCool = moduleData != null ? moduleData.attackCool : 0f;
            float baseProjectileSpeed = moduleData != null ? moduleData.speed : 0f;
            float baseSilenceTime = moduleData != null ? moduleData.silenceTime : 0f;

            using (new EditorGUI.DisabledScope(installed == false))
            {
                EditorGUI.indentLevel++;
                DrawStatPoint(attackArray.GetArrayElementAtIndex(i), "공격력", baseAttack, attackPerPoint);
                DrawStatPoint(fireRateArray.GetArrayElementAtIndex(i), "연사력(쿨다운)", baseAttackCool, -attackCoolReductionPerPoint, attackCoolFloor);
                DrawStatPoint(projectileSpeedArray.GetArrayElementAtIndex(i), "발사체 속도", baseProjectileSpeed, projectileSpeedPerPoint);
                if (silenceArray != null)
                    DrawStatPoint(silenceArray.GetArrayElementAtIndex(i), "침묵 시간", baseSilenceTime, silenceTimePerPoint);
                EditorGUI.indentLevel--;
            }
        }
    }

    // 슬롯 서브타입(예: beam_t1_m1) → DataTableModule 원본 데이터 조회 — 강화 포인트 UI의 기본 수치 표시용
    private ModuleData GetModuleDataForSubType(string subTypeName)
    {
        if (m_moduleTable == null || string.IsNullOrEmpty(subTypeName)) return null;
        if (System.Enum.TryParse(subTypeName, out EModuleSubType subType) == false) return null;
        return m_moduleTable.GetModuleDataFromTable(subType);
    }

    private void DrawHangarSlots(SerializedProperty alloc, int maxSlots, HangarFormula formula)
    {
        SerializedProperty subTypeArray = alloc.FindPropertyRelative("hangarModuleSubType");
        SerializedProperty shipAttackArray = alloc.FindPropertyRelative("hangarShipAttackPoints");
        SerializedProperty fighterAttackArray = alloc.FindPropertyRelative("hangarFighterAttackPoints");
        SerializedProperty ammoArray = alloc.FindPropertyRelative("hangarAmmoPoints");
        SerializedProperty healthArray = alloc.FindPropertyRelative("hangarHealthPoints");
        int slotCountToShow = Mathf.Min(subTypeArray.arraySize, maxSlots);

        for (int i = 0; i < slotCountToShow; i++)
        {
            SerializedProperty subTypeProp = subTypeArray.GetArrayElementAtIndex(i);
            bool installed = string.IsNullOrEmpty(subTypeProp.stringValue) == false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Hangar {i + 1}", GUILayout.Width(60));
            DrawSubTypePopup(subTypeProp, "hanger_", 120);
            EditorGUILayout.LabelField($"장착 {GetInstallCostForSubType(subTypeProp.stringValue)}", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(installed == false))
            {
                EditorGUI.indentLevel++;
                DrawStatPoint(shipAttackArray.GetArrayElementAtIndex(i), "Ship Attack", formula.baseShipAttack, formula.reinforcePerPoint);
                DrawStatPoint(fighterAttackArray.GetArrayElementAtIndex(i), "Fighter Attack", formula.baseFighterAttack, formula.reinforcePerPoint);
                DrawStatPoint(ammoArray.GetArrayElementAtIndex(i), "Ammo", formula.baseAmmo, formula.reinforcePerPoint);
                DrawStatPoint(healthArray.GetArrayElementAtIndex(i), "Health", formula.baseHealth, formula.reinforcePerPoint);
                EditorGUI.indentLevel--;
            }
        }
    }

    private void DrawShieldSubType(SerializedProperty alloc)
    {
        SerializedProperty subTypeProp = alloc.FindPropertyRelative("shieldModuleSubType");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent("Shield SubType", "빈 칸 = 미장착"), GUILayout.Width(120));
        DrawSubTypePopup(subTypeProp, "shield_", 120);
        EditorGUILayout.LabelField($"장착 {GetInstallCostForSubType(subTypeProp.stringValue)}", GUILayout.Width(70));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawInterceptorSlots(SerializedProperty alloc, int maxSlots, InterceptorFormula formula)
    {
        SerializedProperty subTypeArray = alloc.FindPropertyRelative("interceptorModuleSubType");
        SerializedProperty delayArray = alloc.FindPropertyRelative("interceptorDelayPoints");
        SerializedProperty regenArray = alloc.FindPropertyRelative("interceptorRegenRatePoints");
        int slotCountToShow = Mathf.Min(subTypeArray.arraySize, maxSlots);

        for (int i = 0; i < slotCountToShow; i++)
        {
            SerializedProperty subTypeProp = subTypeArray.GetArrayElementAtIndex(i);
            bool installed = string.IsNullOrEmpty(subTypeProp.stringValue) == false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Interceptor {i + 1}", GUILayout.Width(80));
            DrawSubTypePopup(subTypeProp, "interceptor_", 120);
            EditorGUILayout.LabelField($"장착 {GetInstallCostForSubType(subTypeProp.stringValue)}", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            ModuleData moduleData = GetModuleDataForSubType(subTypeProp.stringValue);
            float baseDelay = moduleData != null ? moduleData.interceptorDelay : 0f;
            float baseRegenRate = moduleData != null ? moduleData.interceptorRegenRate : 0f;

            using (new EditorGUI.DisabledScope(installed == false))
            {
                EditorGUI.indentLevel++;
                DrawStatPoint(delayArray.GetArrayElementAtIndex(i), "Delay", baseDelay, -formula.delayReductionPerPoint, formula.delayFloor);
                DrawStatPoint(regenArray.GetArrayElementAtIndex(i), "Regen Rate", baseRegenRate, formula.regenRatePerPoint);
                EditorGUI.indentLevel--;
            }
        }
    }

    // 강화 포인트 입력 필드 — 라벨에 강화 전 기본 수치를 함께 표시하고, 우측에 강화 반영된 최종 수치를 보여줌
    // perPoint가 음수면 감소형(연사력 쿨다운/딜레이 등) — floor로 하한을 둔다. floor를 생략하면(NegativeInfinity) 하한 없음
    private void DrawStatPoint(SerializedProperty prop, string label, float baseValue, float perPoint, float floor = float.NegativeInfinity)
    {
        EditorGUILayout.BeginHorizontal();
        string text = EditorGUILayout.TextField($"{label} (기본 {FormatStatValue(baseValue)})", prop.intValue.ToString());
        int parsedValue;
        if (int.TryParse(text, out parsedValue) && parsedValue != prop.intValue)
            prop.intValue = parsedValue;

        string sign = perPoint >= 0f ? "+" : "";
        EditorGUILayout.LabelField($"x{sign}{FormatStatValue(perPoint)}", GUILayout.Width(55));

        float rawFinalValue = baseValue + prop.intValue * perPoint;
        float finalValue = float.IsNegativeInfinity(floor) ? rawFinalValue : Mathf.Max(floor, rawFinalValue);
        EditorGUILayout.LabelField($"= {FormatStatValue(finalValue)}", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();
    }

    private string FormatStatValue(float value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private ShipStatAllocation BuildAllocationSnapshot(SerializedProperty alloc)
    {
        ShipStatAllocation snapshot = new ShipStatAllocation
        {
            healthPoints = alloc.FindPropertyRelative("healthPoints").intValue,
            turnRatePoints = alloc.FindPropertyRelative("turnRatePoints").intValue,
            repairPoints = alloc.FindPropertyRelative("repairPoints").intValue,
            shieldModuleSubType = alloc.FindPropertyRelative("shieldModuleSubType").stringValue,
            shieldGaugePoints = alloc.FindPropertyRelative("shieldGaugePoints").intValue,
            shieldDelayPoints = alloc.FindPropertyRelative("shieldDelayPoints").intValue,
            shieldRegenRatePoints = alloc.FindPropertyRelative("shieldRegenRatePoints").intValue,
        };

        snapshot.beamModuleSubType = ReadStringArray(alloc.FindPropertyRelative("beamModuleSubType"));
        snapshot.beamAttackPoints = ReadIntArray(alloc.FindPropertyRelative("beamAttackPoints"));
        snapshot.beamFireRatePoints = ReadIntArray(alloc.FindPropertyRelative("beamFireRatePoints"));
        snapshot.beamProjectileSpeedPoints = ReadIntArray(alloc.FindPropertyRelative("beamProjectileSpeedPoints"));
        snapshot.missileModuleSubType = ReadStringArray(alloc.FindPropertyRelative("missileModuleSubType"));
        snapshot.missileAttackPoints = ReadIntArray(alloc.FindPropertyRelative("missileAttackPoints"));
        snapshot.missileFireRatePoints = ReadIntArray(alloc.FindPropertyRelative("missileFireRatePoints"));
        snapshot.missileProjectileSpeedPoints = ReadIntArray(alloc.FindPropertyRelative("missileProjectileSpeedPoints"));
        snapshot.missileSilencePoints = ReadIntArray(alloc.FindPropertyRelative("missileSilencePoints"));
        snapshot.hangarModuleSubType = ReadStringArray(alloc.FindPropertyRelative("hangarModuleSubType"));
        snapshot.hangarShipAttackPoints = ReadIntArray(alloc.FindPropertyRelative("hangarShipAttackPoints"));
        snapshot.hangarFighterAttackPoints = ReadIntArray(alloc.FindPropertyRelative("hangarFighterAttackPoints"));
        snapshot.hangarAmmoPoints = ReadIntArray(alloc.FindPropertyRelative("hangarAmmoPoints"));
        snapshot.hangarHealthPoints = ReadIntArray(alloc.FindPropertyRelative("hangarHealthPoints"));
        snapshot.interceptorModuleSubType = ReadStringArray(alloc.FindPropertyRelative("interceptorModuleSubType"));
        snapshot.interceptorDelayPoints = ReadIntArray(alloc.FindPropertyRelative("interceptorDelayPoints"));
        snapshot.interceptorRegenRatePoints = ReadIntArray(alloc.FindPropertyRelative("interceptorRegenRatePoints"));

        return snapshot;
    }

    private string[] ReadStringArray(SerializedProperty arrayProp)
    {
        string[] result = new string[arrayProp.arraySize];
        for (int i = 0; i < arrayProp.arraySize; i++)
            result[i] = arrayProp.GetArrayElementAtIndex(i).stringValue;
        return result;
    }

    private int[] ReadIntArray(SerializedProperty arrayProp)
    {
        int[] result = new int[arrayProp.arraySize];
        for (int i = 0; i < arrayProp.arraySize; i++)
            result[i] = arrayProp.GetArrayElementAtIndex(i).intValue;
        return result;
    }

    private void DrawCsvTools()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("CSV Import / Export", EditorStyles.boldLabel);

        string presetCsvPath = Application.dataPath + "/Resources/DataTable/ShipPreset/datatable_ship_preset.csv";
        string modulesCsvPath = Application.dataPath + "/Resources/DataTable/ShipPreset/modules_in_preset.csv";

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Import CSV (Preset + Modules)"))
        {
            if (System.IO.File.Exists(presetCsvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"파일 없음:\n{presetCsvPath}", "OK");
            }
            else if (System.IO.File.Exists(modulesCsvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"파일 없음:\n{modulesCsvPath}", "OK");
            }
            else if (EditorUtility.DisplayDialog("Import Ship Preset CSV",
                "datatable_ship_preset.csv + modules_in_preset.csv 를 읽어 함선 프리셋 데이터를 갱신합니다.\n기존 데이터는 삭제됩니다.", "Import", "Cancel"))
            {
                string presetCsvText = System.IO.File.ReadAllText(presetCsvPath, System.Text.Encoding.UTF8);
                dataTable.LoadShipPresetCsv(presetCsvText);

                string modulesCsvText = System.IO.File.ReadAllText(modulesCsvPath, System.Text.Encoding.UTF8);
                dataTable.LoadModulesInPresetCsv(modulesCsvText);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete",
                    $"Import 완료!\n함선 프리셋: {dataTable.GetShipPresetDataList().Count}개", "OK");
            }
        }

        if (GUILayout.Button("Export to CSV (Preset + Modules)"))
        {
            if (EditorUtility.DisplayDialog("Export to CSV",
                $"현재 데이터를 CSV 파일로 덮어씁니다.\n\n{presetCsvPath}\n{modulesCsvPath}\n\n계속하시겠습니까?", "Export", "Cancel"))
            {
                string presetCsv = dataTable.ExportShipPresetCsv();
                System.IO.File.WriteAllText(presetCsvPath, presetCsv, System.Text.Encoding.UTF8);

                string modulesCsv = dataTable.ExportModulesInPresetCsv();
                System.IO.File.WriteAllText(modulesCsvPath, modulesCsv, System.Text.Encoding.UTF8);

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete", "CSV Export가 완료되었습니다.", "OK");
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }
}
#endif
