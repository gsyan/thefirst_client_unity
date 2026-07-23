// 함선 프리셋 ScriptableObject — 성능포인트 배분 결과값(고정)을 담는 그릇
// CSV Import(에디터 전용) → ScriptableObject 갱신 → JSON Export → 서버 배포 순서로 사용
// CSV: Assets/Resources/DataTable/ShipPreset/datatable_ship_preset.csv
// 카테고리별 슬롯 개수는 DataTableConfig.gameSettings.shipStatFormula.maxModuleSlots 기준 고정 컬럼 — 빈 칸이면 미장착
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "DataTableShipPreset", menuName = "Custom/DataTableShipPreset")]
public class DataTableShipPreset : ScriptableObject
{
    [SerializeField] private List<ShipPresetData> shipPresetDataList = new();

    public List<ShipPresetData> GetShipPresetDataList() { return shipPresetDataList; }

    public ShipPresetData GetShipPreset(string presetId)
    {
        for (int i = 0; i < shipPresetDataList.Count; i++)
        {
            if (shipPresetDataList[i].presetId == presetId)
                return shipPresetDataList[i];
        }
        return null;
    }

    public Dictionary<string, ShipPresetData> BuildLookupTable()
    {
        var table = new Dictionary<string, ShipPresetData>();
        for (int i = 0; i < shipPresetDataList.Count; i++)
        {
            table[shipPresetDataList[i].presetId] = shipPresetDataList[i];
        }
        return table;
    }

    #region JSON Export/Import

    public string ExportToJson()
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(shipPresetDataList, settings);
    }

    public void ImportFromJson(string json)
    {
        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ShipPresetData>>(json);
        if (list != null)
        {
            shipPresetDataList = list;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    #endregion

    #region CSV Import/Export (Editor only)

#if UNITY_EDITOR
    private int GetSlotCount()
    {
        string[] guids = AssetDatabase.FindAssets("t:DataTableConfig");
        if (guids.Length == 0) return 6;

        DataTableConfig config = AssetDatabase.LoadAssetAtPath<DataTableConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (config == null || config.gameSettings == null) return 6;

        return config.gameSettings.shipStatFormula.maxModuleSlots;
    }

    private DataTableModule LoadModuleTable()
    {
        string[] guids = AssetDatabase.FindAssets("t:DataTableModule");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<DataTableModule>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    public string ExportToCsv()
    {
        int slotCount = GetSlotCount();
        DataTableModule moduleTable = LoadModuleTable();
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.Append("preset_id,unlock_commander_level,display_name_key,prefab_name,command_cost,health_points,turn_rate_points,repair_points");
        for (int i = 1; i <= slotCount; i++) sb.Append($",beam_slot{i}_subtype,beam_slot{i}_attack,beam_slot{i}_firerate,beam_slot{i}_projectilespeed");
        for (int i = 1; i <= slotCount; i++) sb.Append($",missile_slot{i}_subtype,missile_slot{i}_attack,missile_slot{i}_firerate,missile_slot{i}_projectilespeed,missile_slot{i}_silence");
        for (int i = 1; i <= slotCount; i++) sb.Append($",hangar_slot{i}_subtype,hangar_slot{i}_ship,hangar_slot{i}_fighter,hangar_slot{i}_ammo,hangar_slot{i}_health");
        for (int i = 1; i <= slotCount; i++) sb.Append($",interceptor_slot{i}_subtype,interceptor_slot{i}_delay,interceptor_slot{i}_regen");
        sb.AppendLine(",shield_subtype,shield_gauge_points,shield_delay_points,shield_regen_rate_points");

        for (int i = 0; i < shipPresetDataList.Count; i++)
        {
            ShipPresetData data = shipPresetDataList[i];
            ShipStatAllocation alloc = data.statAllocation;
            // command_cost는 저장된 캐시값이 아니라 Export 시점에 항상 새로 계산 — Inspector를 열어보지 않아도 최신 값 보장
            int commandCost = alloc.GetTotalPointsUsed(moduleTable, data.prefabName);

            sb.Append(string.Format(ic, "{0},{1},{2},{3},{4},{5},{6},{7}",
                data.presetId, data.unlockCommanderLevel, data.displayNameKey, data.prefabName, commandCost,
                alloc.healthPoints, alloc.turnRatePoints, alloc.repairPoints));

            AppendWeaponSlots(sb, slotCount, alloc.beamModuleSubType, alloc.beamAttackPoints, alloc.beamFireRatePoints, alloc.beamProjectileSpeedPoints, null);
            AppendWeaponSlots(sb, slotCount, alloc.missileModuleSubType, alloc.missileAttackPoints, alloc.missileFireRatePoints, alloc.missileProjectileSpeedPoints, alloc.missileSilencePoints);
            AppendHangarSlots(sb, slotCount, alloc);
            AppendInterceptorSlots(sb, slotCount, alloc);

            sb.AppendLine(string.Format(ic, ",{0},{1},{2},{3}",
                alloc.shieldModuleSubType, alloc.shieldGaugePoints, alloc.shieldDelayPoints, alloc.shieldRegenRatePoints));
        }

        return sb.ToString();
    }

    // 빔/미사일 공용 — silencePoints는 미사일 전용(빔은 null 전달)
    private void AppendWeaponSlots(System.Text.StringBuilder sb, int slotCount, string[] moduleSubType, int[] attackPoints, int[] fireRatePoints, int[] projectileSpeedPoints, int[] silencePoints)
    {
        for (int i = 0; i < slotCount; i++)
        {
            bool installed = i < moduleSubType.Length && string.IsNullOrEmpty(moduleSubType[i]) == false;
            sb.Append(',');
            if (installed) sb.Append(moduleSubType[i]);
            sb.Append(',');
            if (installed) sb.Append(attackPoints[i]);
            sb.Append(',');
            if (installed) sb.Append(fireRatePoints[i]);
            sb.Append(',');
            if (installed) sb.Append(projectileSpeedPoints[i]);
            if (silencePoints != null)
            {
                sb.Append(',');
                if (installed) sb.Append(silencePoints[i]);
            }
        }
    }

    private void AppendHangarSlots(System.Text.StringBuilder sb, int slotCount, ShipStatAllocation alloc)
    {
        for (int i = 0; i < slotCount; i++)
        {
            bool installed = i < alloc.hangarModuleSubType.Length && string.IsNullOrEmpty(alloc.hangarModuleSubType[i]) == false;
            sb.Append(',');
            if (installed) sb.Append(alloc.hangarModuleSubType[i]);
            sb.Append(',');
            if (installed) sb.Append(alloc.hangarShipAttackPoints[i]);
            sb.Append(',');
            if (installed) sb.Append(alloc.hangarFighterAttackPoints[i]);
            sb.Append(',');
            if (installed) sb.Append(alloc.hangarAmmoPoints[i]);
            sb.Append(',');
            if (installed) sb.Append(alloc.hangarHealthPoints[i]);
        }
    }

    private void AppendInterceptorSlots(System.Text.StringBuilder sb, int slotCount, ShipStatAllocation alloc)
    {
        for (int i = 0; i < slotCount; i++)
        {
            bool installed = i < alloc.interceptorModuleSubType.Length && string.IsNullOrEmpty(alloc.interceptorModuleSubType[i]) == false;
            sb.Append(',');
            if (installed) sb.Append(alloc.interceptorModuleSubType[i]);
            sb.Append(',');
            if (installed) sb.Append(alloc.interceptorDelayPoints[i]);
            sb.Append(',');
            if (installed) sb.Append(alloc.interceptorRegenRatePoints[i]);
        }
    }

    // datatable_ship_preset.csv 컬럼 순서(인덱스 고정) — ExportToCsv 헤더 참고. display_name_key는 UI.csv 참조 키
    // 슬롯 컬럼이 빈 칸이면 미장착으로 처리
    public void LoadFromCsv(string csvText)
    {
        int slotCount = GetSlotCount();
        DataTableModule moduleTable = LoadModuleTable();
        shipPresetDataList.Clear();

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);
            string presetId = GetCol(cols, 0);
            if (string.IsNullOrEmpty(presetId)) continue;

            int col = 5; // 0~4는 preset_id/unlock_commander_level/display_name_key/prefab_name/command_cost

            ShipStatAllocation alloc = new ShipStatAllocation();
            alloc.healthPoints = ParseInt(GetCol(cols, col++));
            alloc.turnRatePoints = ParseInt(GetCol(cols, col++));
            alloc.repairPoints = ParseInt(GetCol(cols, col++));

            ReadWeaponSlots(cols, slotCount, ref col, out alloc.beamModuleSubType, out alloc.beamAttackPoints, out alloc.beamFireRatePoints, out alloc.beamProjectileSpeedPoints, out _, hasSilence: false);
            ReadWeaponSlots(cols, slotCount, ref col, out alloc.missileModuleSubType, out alloc.missileAttackPoints, out alloc.missileFireRatePoints, out alloc.missileProjectileSpeedPoints, out alloc.missileSilencePoints, hasSilence: true);
            ReadHangarSlots(cols, slotCount, ref col, alloc);
            ReadInterceptorSlots(cols, slotCount, ref col, alloc);

            alloc.shieldModuleSubType = GetCol(cols, col++);
            alloc.shieldGaugePoints = ParseInt(GetCol(cols, col++));
            alloc.shieldDelayPoints = ParseInt(GetCol(cols, col++));
            alloc.shieldRegenRatePoints = ParseInt(GetCol(cols, col++));

            // command_cost는 더 이상 CSV 값을 신뢰하지 않음 — 성능포인트 총합(GetTotalPointsUsed, 몸체 설치 비용 포함)이 그대로 지휘력이 됨
            string prefabName = GetCol(cols, 3);
            shipPresetDataList.Add(new ShipPresetData
            {
                presetId             = presetId,
                unlockCommanderLevel = ParseInt(GetCol(cols, 1)),
                displayNameKey       = GetCol(cols, 2),
                prefabName           = prefabName,
                commandCost          = alloc.GetTotalPointsUsed(moduleTable, prefabName),
                statAllocation = alloc,
            });
        }

        Debug.Log($"[DataTableShipPreset] CSV Import 완료: {shipPresetDataList.Count}개");
        EditorUtility.SetDirty(this);
    }

    // 빔/미사일 공용 — hasSilence가 true면 미사일 전용 침묵 컬럼까지 읽음(빔은 false, silencePoints는 slotCount 크기의 빈 배열)
    private void ReadWeaponSlots(string[] cols, int slotCount, ref int col, out string[] moduleSubType, out int[] attackPoints, out int[] fireRatePoints, out int[] projectileSpeedPoints, out int[] silencePoints, bool hasSilence)
    {
        moduleSubType = new string[slotCount];
        attackPoints = new int[slotCount];
        fireRatePoints = new int[slotCount];
        projectileSpeedPoints = new int[slotCount];
        silencePoints = new int[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            string subTypeCell = GetCol(cols, col++);
            string attackCell = GetCol(cols, col++);
            string fireRateCell = GetCol(cols, col++);
            string projectileSpeedCell = GetCol(cols, col++);
            string silenceCell = hasSilence ? GetCol(cols, col++) : null;

            bool installed = string.IsNullOrEmpty(subTypeCell) == false;
            moduleSubType[i] = subTypeCell;
            attackPoints[i] = installed ? ParseInt(attackCell) : 0;
            fireRatePoints[i] = installed ? ParseInt(fireRateCell) : 0;
            projectileSpeedPoints[i] = installed ? ParseInt(projectileSpeedCell) : 0;
            if (hasSilence)
                silencePoints[i] = installed ? ParseInt(silenceCell) : 0;
        }
    }

    private void ReadHangarSlots(string[] cols, int slotCount, ref int col, ShipStatAllocation alloc)
    {
        alloc.hangarModuleSubType = new string[slotCount];
        alloc.hangarShipAttackPoints = new int[slotCount];
        alloc.hangarFighterAttackPoints = new int[slotCount];
        alloc.hangarAmmoPoints = new int[slotCount];
        alloc.hangarHealthPoints = new int[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            string subTypeCell = GetCol(cols, col++);
            string shipCell = GetCol(cols, col++);
            string fighterCell = GetCol(cols, col++);
            string ammoCell = GetCol(cols, col++);
            string healthCell = GetCol(cols, col++);

            bool installed = string.IsNullOrEmpty(subTypeCell) == false;
            alloc.hangarModuleSubType[i] = subTypeCell;
            alloc.hangarShipAttackPoints[i] = installed ? ParseInt(shipCell) : 0;
            alloc.hangarFighterAttackPoints[i] = installed ? ParseInt(fighterCell) : 0;
            alloc.hangarAmmoPoints[i] = installed ? ParseInt(ammoCell) : 0;
            alloc.hangarHealthPoints[i] = installed ? ParseInt(healthCell) : 0;
        }
    }

    private void ReadInterceptorSlots(string[] cols, int slotCount, ref int col, ShipStatAllocation alloc)
    {
        alloc.interceptorModuleSubType = new string[slotCount];
        alloc.interceptorDelayPoints = new int[slotCount];
        alloc.interceptorRegenRatePoints = new int[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            string subTypeCell = GetCol(cols, col++);
            string delayCell = GetCol(cols, col++);
            string regenCell = GetCol(cols, col++);

            bool installed = string.IsNullOrEmpty(subTypeCell) == false;
            alloc.interceptorModuleSubType[i] = subTypeCell;
            alloc.interceptorDelayPoints[i] = installed ? ParseInt(delayCell) : 0;
            alloc.interceptorRegenRatePoints[i] = installed ? ParseInt(regenCell) : 0;
        }
    }

    private string GetCol(string[] cols, int idx)
    {
        if (idx >= cols.Length) return "";
        return cols[idx].Trim();
    }

    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if (c == '"')
                inQuotes = !inQuotes;
            else if (c == ',' && inQuotes == false)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
                current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    private int ParseInt(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        s = s.Replace(",", "").Trim();
        return int.TryParse(s, out int r) ? r : 0;
    }
#endif

    #endregion
}
