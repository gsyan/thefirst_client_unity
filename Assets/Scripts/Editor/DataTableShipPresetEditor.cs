// DataTableShipPreset 커스텀 에디터 — 함선 프리셋 Inspector UI 및 CSV Import/Export 툴
// CSV 경로: Assets/Resources/DataTable/Exploration/datatable_ship_preset.csv

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DataTableShipPreset))]
public class DataTableShipPresetEditor : Editor
{
    private DataTableShipPreset dataTable;
    private bool m_foldout = false;
    private ShipStatFormulaSettings m_formula;

    private void OnEnable()
    {
        dataTable = (DataTableShipPreset)target;
        m_formula = LoadFormula();
    }

    private ShipStatFormulaSettings LoadFormula()
    {
        string[] guids = AssetDatabase.FindAssets("t:DataTableConfig");
        if (guids.Length == 0) return null;

        DataTableConfig config = AssetDatabase.LoadAssetAtPath<DataTableConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (config == null || config.gameSettings == null) return null;

        return config.gameSettings.shipStatFormula;
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

        EditorGUILayout.PropertyField(presetIdProp, new GUIContent("Preset Id"));
        EditorGUILayout.PropertyField(elementProp.FindPropertyRelative("displayNameKey"), new GUIContent("Display Name Key"));
        EditorGUILayout.PropertyField(elementProp.FindPropertyRelative("prefabName"), new GUIContent("Prefab Name"));
        EditorGUILayout.PropertyField(elementProp.FindPropertyRelative("commandCost"), new GUIContent("Command Cost"));

        EditorGUILayout.Space(6);
        SerializedProperty allocProp = elementProp.FindPropertyRelative("statAllocation");
        DrawStatAllocation(allocProp);

        EditorGUILayout.EndVertical();
    }

    private void DrawStatAllocation(SerializedProperty alloc)
    {
        if (m_formula != null)
        {
            int totalPoints = BuildAllocationSnapshot(alloc).GetTotalPointsUsed(m_formula);
            EditorGUILayout.LabelField("Total Points Used", totalPoints.ToString(), EditorStyles.boldLabel);
        }

        EditorGUILayout.LabelField("Flat Stats", EditorStyles.boldLabel);
        DrawPlainInt(alloc, "healthPoints", "Health Points", "장착 개념 없이 순수 포인트 배분. 기본값/계수 미확정 — 임시 1p=+0.1");
        DrawPlainInt(alloc, "turnRatePoints", "Turn Rate Points", "장착 개념 없이 순수 포인트 배분. 기본값/계수 미확정 — 임시 1p=+0.1");
        DrawPlainInt(alloc, "repairPoints", "Repair Points", "장착 개념 없이 순수 포인트 배분. 기본값/계수 미확정 — 임시 1p=+0.1");

        int beamCost = m_formula != null ? m_formula.beam.installCost : 0;
        EditorGUILayout.LabelField("Beam", EditorStyles.boldLabel);
        DrawSingleStatSlots(alloc, "beamModuleSubType", "beamReinforcePoints", "Beam", beamCost);

        int missileCost = m_formula != null ? m_formula.missile.installCost : 0;
        EditorGUILayout.LabelField("Missile", EditorStyles.boldLabel);
        DrawSingleStatSlots(alloc, "missileModuleSubType", "missileReinforcePoints", "Missile", missileCost);

        int hangarCost = m_formula != null ? m_formula.hangar.installCost : 0;
        EditorGUILayout.LabelField("Hangar", EditorStyles.boldLabel);
        DrawHangarSlots(alloc, hangarCost);

        int shieldCost = m_formula != null ? m_formula.shield.installCost : 0;
        EditorGUILayout.LabelField("Shield", EditorStyles.boldLabel);
        DrawInstalledBool(alloc, "shieldInstalled", "Shield Installed", shieldCost, "장착 여부(0/1), 코스트는 shipStatFormula.shield.installCost. 강화 서브스탯 3종은 1p=1선택");
        DrawPlainInt(alloc, "shieldGaugePoints", "Shield Gauge Points", "게이지 강화 포인트");
        DrawPlainInt(alloc, "shieldDelayPoints", "Shield Delay Points", "무방비 딜레이 단축 포인트");
        DrawPlainInt(alloc, "shieldRegenRatePoints", "Shield Regen Rate Points", "회복속도(초당 게이지 회복량) 강화 포인트");

        int interceptorCost = m_formula != null ? m_formula.interceptor.installCost : 0;
        EditorGUILayout.LabelField("Interceptor", EditorStyles.boldLabel);
        DrawInterceptorSlots(alloc, interceptorCost);
    }

    // 빔/미사일처럼 슬롯당 강화 스탯이 하나뿐인 카테고리 공용 UI — 슬롯별로 서브타입 텍스트(빈 칸=미장착) + 강화 포인트 한 줄
    private void DrawSingleStatSlots(SerializedProperty alloc, string subTypeField, string pointsField, string slotLabel, int installCost)
    {
        SerializedProperty subTypeArray = alloc.FindPropertyRelative(subTypeField);
        SerializedProperty pointsArray = alloc.FindPropertyRelative(pointsField);

        for (int i = 0; i < subTypeArray.arraySize; i++)
        {
            SerializedProperty subTypeProp = subTypeArray.GetArrayElementAtIndex(i);
            SerializedProperty pointsProp = pointsArray.GetArrayElementAtIndex(i);
            bool installed = string.IsNullOrEmpty(subTypeProp.stringValue) == false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{slotLabel} {i + 1}", GUILayout.Width(60));
            subTypeProp.stringValue = EditorGUILayout.TextField(subTypeProp.stringValue, GUILayout.Width(120));

            using (new EditorGUI.DisabledScope(installed == false))
            {
                int newValue = DrawNoDragIntField(new GUIContent("Reinforce"), pointsProp.intValue);
                if (newValue != pointsProp.intValue) pointsProp.intValue = newValue;
                EditorGUILayout.LabelField($"장착 {installCost}", GUILayout.Width(70));
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawHangarSlots(SerializedProperty alloc, int installCost)
    {
        SerializedProperty subTypeArray = alloc.FindPropertyRelative("hangarModuleSubType");
        SerializedProperty shipAttackArray = alloc.FindPropertyRelative("hangarShipAttackPoints");
        SerializedProperty fighterAttackArray = alloc.FindPropertyRelative("hangarFighterAttackPoints");
        SerializedProperty ammoArray = alloc.FindPropertyRelative("hangarAmmoPoints");
        SerializedProperty healthArray = alloc.FindPropertyRelative("hangarHealthPoints");

        for (int i = 0; i < subTypeArray.arraySize; i++)
        {
            SerializedProperty subTypeProp = subTypeArray.GetArrayElementAtIndex(i);
            bool installed = string.IsNullOrEmpty(subTypeProp.stringValue) == false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Hangar {i + 1}", GUILayout.Width(60));
            subTypeProp.stringValue = EditorGUILayout.TextField(subTypeProp.stringValue, GUILayout.Width(120));
            EditorGUILayout.LabelField($"장착 {installCost}", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(installed == false))
            {
                EditorGUI.indentLevel++;
                DrawSlotInt(shipAttackArray.GetArrayElementAtIndex(i), "Ship Attack");
                DrawSlotInt(fighterAttackArray.GetArrayElementAtIndex(i), "Fighter Attack");
                DrawSlotInt(ammoArray.GetArrayElementAtIndex(i), "Ammo");
                DrawSlotInt(healthArray.GetArrayElementAtIndex(i), "Health");
                EditorGUI.indentLevel--;
            }
        }
    }

    private void DrawInterceptorSlots(SerializedProperty alloc, int installCost)
    {
        SerializedProperty installedArray = alloc.FindPropertyRelative("interceptorSlotInstalled");
        SerializedProperty delayArray = alloc.FindPropertyRelative("interceptorDelayPoints");
        SerializedProperty regenArray = alloc.FindPropertyRelative("interceptorRegenRatePoints");

        for (int i = 0; i < installedArray.arraySize; i++)
        {
            SerializedProperty installedProp = installedArray.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginHorizontal();
            installedProp.boolValue = EditorGUILayout.ToggleLeft($"Interceptor {i + 1}", installedProp.boolValue, GUILayout.Width(120));
            EditorGUILayout.LabelField($"장착 {installCost}", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(installedProp.boolValue == false))
            {
                EditorGUI.indentLevel++;
                DrawSlotInt(delayArray.GetArrayElementAtIndex(i), "Delay");
                DrawSlotInt(regenArray.GetArrayElementAtIndex(i), "Regen Rate");
                EditorGUI.indentLevel--;
            }
        }
    }

    private void DrawSlotInt(SerializedProperty prop, string label)
    {
        int newValue = DrawNoDragIntField(new GUIContent(label), prop.intValue);
        if (newValue != prop.intValue) prop.intValue = newValue;
    }

    private void DrawPlainInt(SerializedProperty parent, string fieldName, string label, string tooltip)
    {
        SerializedProperty fieldProp = parent.FindPropertyRelative(fieldName);
        int newValue = DrawNoDragIntField(new GUIContent(label, tooltip), fieldProp.intValue);
        if (newValue != fieldProp.intValue) fieldProp.intValue = newValue;
    }

    // 라벨 드래그로 값이 바뀌는 Unity 기본 IntField 동작을 없애기 위해 TextField로 직접 파싱
    private int DrawNoDragIntField(GUIContent label, int value)
    {
        string text = EditorGUILayout.TextField(label, value.ToString());
        int result;
        return int.TryParse(text, out result) ? result : value;
    }

    // 장착 여부(bool) + 옆에 "장착 시 installCost" 표시
    private void DrawInstalledBool(SerializedProperty parent, string fieldName, string label, int installCost, string tooltip)
    {
        SerializedProperty fieldProp = parent.FindPropertyRelative(fieldName);

        EditorGUILayout.BeginHorizontal();
        bool newValue = EditorGUILayout.Toggle(new GUIContent(label, tooltip), fieldProp.boolValue);
        if (newValue != fieldProp.boolValue) fieldProp.boolValue = newValue;
        EditorGUILayout.LabelField($"장착 시 {installCost}", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
    }

    private ShipStatAllocation BuildAllocationSnapshot(SerializedProperty alloc)
    {
        ShipStatAllocation snapshot = new ShipStatAllocation
        {
            healthPoints = alloc.FindPropertyRelative("healthPoints").intValue,
            turnRatePoints = alloc.FindPropertyRelative("turnRatePoints").intValue,
            repairPoints = alloc.FindPropertyRelative("repairPoints").intValue,
            shieldInstalled = alloc.FindPropertyRelative("shieldInstalled").boolValue,
            shieldGaugePoints = alloc.FindPropertyRelative("shieldGaugePoints").intValue,
            shieldDelayPoints = alloc.FindPropertyRelative("shieldDelayPoints").intValue,
            shieldRegenRatePoints = alloc.FindPropertyRelative("shieldRegenRatePoints").intValue,
        };

        snapshot.beamModuleSubType = ReadStringArray(alloc.FindPropertyRelative("beamModuleSubType"));
        snapshot.beamReinforcePoints = ReadIntArray(alloc.FindPropertyRelative("beamReinforcePoints"));
        snapshot.missileModuleSubType = ReadStringArray(alloc.FindPropertyRelative("missileModuleSubType"));
        snapshot.missileReinforcePoints = ReadIntArray(alloc.FindPropertyRelative("missileReinforcePoints"));
        snapshot.hangarModuleSubType = ReadStringArray(alloc.FindPropertyRelative("hangarModuleSubType"));
        snapshot.hangarShipAttackPoints = ReadIntArray(alloc.FindPropertyRelative("hangarShipAttackPoints"));
        snapshot.hangarFighterAttackPoints = ReadIntArray(alloc.FindPropertyRelative("hangarFighterAttackPoints"));
        snapshot.hangarAmmoPoints = ReadIntArray(alloc.FindPropertyRelative("hangarAmmoPoints"));
        snapshot.hangarHealthPoints = ReadIntArray(alloc.FindPropertyRelative("hangarHealthPoints"));
        snapshot.interceptorSlotInstalled = ReadBoolArray(alloc.FindPropertyRelative("interceptorSlotInstalled"));
        snapshot.interceptorDelayPoints = ReadIntArray(alloc.FindPropertyRelative("interceptorDelayPoints"));
        snapshot.interceptorRegenRatePoints = ReadIntArray(alloc.FindPropertyRelative("interceptorRegenRatePoints"));

        return snapshot;
    }

    private bool[] ReadBoolArray(SerializedProperty arrayProp)
    {
        bool[] result = new bool[arrayProp.arraySize];
        for (int i = 0; i < arrayProp.arraySize; i++)
            result[i] = arrayProp.GetArrayElementAtIndex(i).boolValue;
        return result;
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
        EditorGUILayout.LabelField("CSV Import", EditorStyles.boldLabel);

        string csvPath = Application.dataPath + "/Resources/DataTable/Exploration/datatable_ship_preset.csv";

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Import CSV"))
        {
            if (System.IO.File.Exists(csvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"파일 없음:\n{csvPath}", "OK");
            }
            else if (EditorUtility.DisplayDialog("Import Ship Preset CSV",
                "datatable_ship_preset.csv 를 읽어 함선 프리셋 데이터를 갱신합니다.\n기존 데이터는 삭제됩니다.", "Import", "Cancel"))
            {
                string csvText = System.IO.File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                dataTable.LoadFromCsv(csvText);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete",
                    $"Import 완료!\n함선 프리셋: {dataTable.GetShipPresetDataList().Count}개", "OK");
            }
        }

        if (GUILayout.Button("Export to CSV"))
        {
            if (EditorUtility.DisplayDialog("Export to CSV",
                $"현재 데이터를 CSV 파일로 덮어씁니다.\n\n{csvPath}\n\n계속하시겠습니까?", "Export", "Cancel"))
            {
                string csv = dataTable.ExportToCsv();
                System.IO.File.WriteAllText(csvPath, csv, System.Text.Encoding.UTF8);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete", "CSV Export가 완료되었습니다.", "OK");
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }
}
#endif
