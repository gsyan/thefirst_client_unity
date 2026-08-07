// 함선 프리셋 ScriptableObject — 성능포인트 배분 결과값(고정)을 담는 그릇
// CSV Import(에디터 전용, Preset→Modules 순서로 반드시 함께) → ScriptableObject 갱신 → JSON Export → 서버 배포 순서로 사용
// CSV 1: Assets/Resources/DataTable/ShipPreset/datatable_ship_preset.csv — 식별 정보 + 스칼라 스탯(preset_id 1행)
// CSV 2: Assets/Resources/DataTable/ShipPreset/modules_in_preset.csv — 장착 모듈(preset_id당 장착 개수만큼 행, module_type으로 구분)
// 슬롯 인덱스는 CSV 2에서 같은 preset_id+module_type이 등장하는 순서 — 상한은 DataTableConfig.gameSettings.shipStatFormula.maxModuleSlots
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

    // ---- Ship Preset CSV(식별 정보 + 스칼라 스탯) — datatable_ship_preset.csv ----
    public string ExportShipPresetCsv()
    {
        DataTableModule moduleTable = LoadModuleTable();
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("preset_id,unlock_commander_level,prefab_name,command_cost,health_points,turn_rate_points,repair_points");

        for (int i = 0; i < shipPresetDataList.Count; i++)
        {
            ShipPresetData data = shipPresetDataList[i];
            ShipStatAllocation alloc = data.statAllocation;
            // command_cost는 저장된 캐시값이 아니라 Export 시점에 항상 새로 계산 — Inspector를 열어보지 않아도 최신 값 보장
            int commandCost = alloc.GetTotalPointsUsed(moduleTable, data.prefabName);

            sb.AppendLine(string.Format(ic, "{0},{1},{2},{3},{4},{5},{6}",
                data.presetId, data.unlockCommanderLevel, data.prefabName, commandCost,
                alloc.healthPoints, alloc.turnRatePoints, alloc.repairPoints));
        }

        return sb.ToString();
    }

    // datatable_ship_preset.csv 컬럼 순서(인덱스 고정) — ExportShipPresetCsv 헤더 참고. preset_id를 UI.csv 로컬라이즈 키로 그대로 사용
    // 모듈 장착 정보는 여기서 채우지 않음 — LoadModulesInPresetCsv를 이 뒤에 반드시 호출해야 함(슬롯 배열만 슬롯 수만큼 빈 상태로 준비)
    public void LoadShipPresetCsv(string csvText)
    {
        int slotCount = GetSlotCount();
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

            ShipStatAllocation alloc = new ShipStatAllocation();
            InitializeAllocationArrays(alloc, slotCount);
            alloc.healthPoints = ParseInt(GetCol(cols, 4));
            alloc.turnRatePoints = ParseInt(GetCol(cols, 5));
            alloc.repairPoints = ParseInt(GetCol(cols, 6));

            shipPresetDataList.Add(new ShipPresetData
            {
                presetId             = presetId,
                unlockCommanderLevel = ParseInt(GetCol(cols, 1)),
                prefabName           = GetCol(cols, 2),
                // command_cost(cols[3])는 읽지 않음 — LoadModulesInPresetCsv 마지막에 성능포인트 총합으로 항상 재계산됨
                statAllocation       = alloc,
            });
        }

        Debug.Log($"[DataTableShipPreset] Preset CSV Import 완료: {shipPresetDataList.Count}개");
        EditorUtility.SetDirty(this);
    }

    private void InitializeAllocationArrays(ShipStatAllocation alloc, int slotCount)
    {
        alloc.beamModuleSubType = new string[slotCount];
        alloc.beamAttackPoints = new int[slotCount];
        alloc.beamFireRatePoints = new int[slotCount];
        alloc.beamProjectileSpeedPoints = new int[slotCount];
        alloc.missileModuleSubType = new string[slotCount];
        alloc.missileAttackPoints = new int[slotCount];
        alloc.missileFireRatePoints = new int[slotCount];
        alloc.missileProjectileSpeedPoints = new int[slotCount];
        alloc.missileSilencePoints = new int[slotCount];
        alloc.hangarModuleSubType = new string[slotCount];
        alloc.hangarShipAttackPoints = new int[slotCount];
        alloc.hangarFighterAttackPoints = new int[slotCount];
        alloc.hangarAmmoPoints = new int[slotCount];
        alloc.hangarHealthPoints = new int[slotCount];
        alloc.interceptorModuleSubType = new string[slotCount];
        alloc.interceptorDelayPoints = new int[slotCount];
        alloc.interceptorRegenRatePoints = new int[slotCount];
    }

    // ---- Modules In Preset CSV(프리셋당 장착 모듈 1행) — modules_in_preset.csv ----
    private const string k_moduleTypeBeam = "beam";
    private const string k_moduleTypeMissile = "missile";
    private const string k_moduleTypeHangar = "hangar";
    private const string k_moduleTypeInterceptor = "interceptor";
    private const string k_moduleTypeShield = "shield";

    public string ExportModulesInPresetCsv()
    {
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("preset_id,module_type,subtype,attack,firerate,projectilespeed,silence,ship,fighter,ammo,health,delay,regen,gauge");

        for (int i = 0; i < shipPresetDataList.Count; i++)
        {
            ShipPresetData data = shipPresetDataList[i];
            ShipStatAllocation alloc = data.statAllocation;

            AppendWeaponRows(sb, ic, data.presetId, k_moduleTypeBeam, alloc.beamModuleSubType, alloc.beamAttackPoints, alloc.beamFireRatePoints, alloc.beamProjectileSpeedPoints, null);
            AppendWeaponRows(sb, ic, data.presetId, k_moduleTypeMissile, alloc.missileModuleSubType, alloc.missileAttackPoints, alloc.missileFireRatePoints, alloc.missileProjectileSpeedPoints, alloc.missileSilencePoints);
            AppendHangarRows(sb, ic, data.presetId, alloc);
            AppendInterceptorRows(sb, ic, data.presetId, alloc);
            AppendShieldRow(sb, ic, data.presetId, alloc);
        }

        return sb.ToString();
    }

    // 빔/미사일 공용 — silencePoints는 미사일 전용(빔은 null 전달). 열 순서: preset_id,module_type,subtype,attack,firerate,projectilespeed,silence,ship,fighter,ammo,health,delay,regen,gauge
    private void AppendWeaponRows(System.Text.StringBuilder sb, System.Globalization.CultureInfo ic, string presetId, string moduleType,
        string[] subType, int[] attack, int[] fireRate, int[] projectileSpeed, int[] silence)
    {
        for (int i = 0; i < subType.Length; i++)
        {
            if (string.IsNullOrEmpty(subType[i])) continue;
            string silenceValue = silence != null ? silence[i].ToString(ic) : "";
            sb.AppendLine(string.Format(ic, "{0},{1},{2},{3},{4},{5},{6},,,,,,,",
                presetId, moduleType, subType[i], attack[i], fireRate[i], projectileSpeed[i], silenceValue));
        }
    }

    private void AppendHangarRows(System.Text.StringBuilder sb, System.Globalization.CultureInfo ic, string presetId, ShipStatAllocation alloc)
    {
        for (int i = 0; i < alloc.hangarModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(alloc.hangarModuleSubType[i])) continue;
            sb.AppendLine(string.Format(ic, "{0},{1},{2},,,,,{3},{4},{5},{6},,,",
                presetId, k_moduleTypeHangar, alloc.hangarModuleSubType[i],
                alloc.hangarShipAttackPoints[i], alloc.hangarFighterAttackPoints[i], alloc.hangarAmmoPoints[i], alloc.hangarHealthPoints[i]));
        }
    }

    private void AppendInterceptorRows(System.Text.StringBuilder sb, System.Globalization.CultureInfo ic, string presetId, ShipStatAllocation alloc)
    {
        for (int i = 0; i < alloc.interceptorModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(alloc.interceptorModuleSubType[i])) continue;
            sb.AppendLine(string.Format(ic, "{0},{1},{2},,,,,,,,,{3},{4},",
                presetId, k_moduleTypeInterceptor, alloc.interceptorModuleSubType[i],
                alloc.interceptorDelayPoints[i], alloc.interceptorRegenRatePoints[i]));
        }
    }

    private void AppendShieldRow(System.Text.StringBuilder sb, System.Globalization.CultureInfo ic, string presetId, ShipStatAllocation alloc)
    {
        if (string.IsNullOrEmpty(alloc.shieldModuleSubType)) return;
        sb.AppendLine(string.Format(ic, "{0},{1},{2},,,,,,,,,{3},{4},{5}",
            presetId, k_moduleTypeShield, alloc.shieldModuleSubType,
            alloc.shieldDelayPoints, alloc.shieldRegenRatePoints, alloc.shieldGaugePoints));
    }

    // LoadShipPresetCsv 이후에 호출 — preset_id로 기존 프리셋을 찾아 장착 슬롯을 채움. 슬롯 인덱스는 같은 preset_id+module_type 내 CSV 등장 순서
    public void LoadModulesInPresetCsv(string csvText)
    {
        DataTableModule moduleTable = LoadModuleTable();
        Dictionary<string, ShipPresetData> lookup = BuildLookupTable();
        var nextSlotIndex = new Dictionary<string, int>(); // key: presetId + "_" + moduleType

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);
            string presetId = GetCol(cols, 0);
            string moduleType = GetCol(cols, 1);
            string subType = GetCol(cols, 2);
            if (string.IsNullOrEmpty(presetId) || string.IsNullOrEmpty(moduleType) || string.IsNullOrEmpty(subType)) continue;

            if (lookup.TryGetValue(presetId, out ShipPresetData data) == false)
            {
                Debug.LogWarning($"[DataTableShipPreset] modules_in_preset.csv: 알 수 없는 preset_id '{presetId}' (줄 {i + 1})");
                continue;
            }

            int attack = ParseInt(GetCol(cols, 3));
            int fireRate = ParseInt(GetCol(cols, 4));
            int projectileSpeed = ParseInt(GetCol(cols, 5));
            int silence = ParseInt(GetCol(cols, 6));
            int ship = ParseInt(GetCol(cols, 7));
            int fighter = ParseInt(GetCol(cols, 8));
            int ammo = ParseInt(GetCol(cols, 9));
            int health = ParseInt(GetCol(cols, 10));
            int delay = ParseInt(GetCol(cols, 11));
            int regen = ParseInt(GetCol(cols, 12));
            int gauge = ParseInt(GetCol(cols, 13));

            ShipStatAllocation alloc = data.statAllocation;

            if (moduleType == k_moduleTypeShield)
            {
                alloc.shieldModuleSubType = subType;
                alloc.shieldGaugePoints = gauge;
                alloc.shieldDelayPoints = delay;
                alloc.shieldRegenRatePoints = regen;
                continue;
            }

            string slotKey = presetId + "_" + moduleType;
            nextSlotIndex.TryGetValue(slotKey, out int slotIndex);
            nextSlotIndex[slotKey] = slotIndex + 1;

            switch (moduleType)
            {
                case k_moduleTypeBeam:
                    if (AssignSlot(alloc.beamModuleSubType.Length, slotIndex, presetId, moduleType, i) == false) break;
                    alloc.beamModuleSubType[slotIndex] = subType;
                    alloc.beamAttackPoints[slotIndex] = attack;
                    alloc.beamFireRatePoints[slotIndex] = fireRate;
                    alloc.beamProjectileSpeedPoints[slotIndex] = projectileSpeed;
                    break;
                case k_moduleTypeMissile:
                    if (AssignSlot(alloc.missileModuleSubType.Length, slotIndex, presetId, moduleType, i) == false) break;
                    alloc.missileModuleSubType[slotIndex] = subType;
                    alloc.missileAttackPoints[slotIndex] = attack;
                    alloc.missileFireRatePoints[slotIndex] = fireRate;
                    alloc.missileProjectileSpeedPoints[slotIndex] = projectileSpeed;
                    alloc.missileSilencePoints[slotIndex] = silence;
                    break;
                case k_moduleTypeHangar:
                    if (AssignSlot(alloc.hangarModuleSubType.Length, slotIndex, presetId, moduleType, i) == false) break;
                    alloc.hangarModuleSubType[slotIndex] = subType;
                    alloc.hangarShipAttackPoints[slotIndex] = ship;
                    alloc.hangarFighterAttackPoints[slotIndex] = fighter;
                    alloc.hangarAmmoPoints[slotIndex] = ammo;
                    alloc.hangarHealthPoints[slotIndex] = health;
                    break;
                case k_moduleTypeInterceptor:
                    if (AssignSlot(alloc.interceptorModuleSubType.Length, slotIndex, presetId, moduleType, i) == false) break;
                    alloc.interceptorModuleSubType[slotIndex] = subType;
                    alloc.interceptorDelayPoints[slotIndex] = delay;
                    alloc.interceptorRegenRatePoints[slotIndex] = regen;
                    break;
                default:
                    Debug.LogWarning($"[DataTableShipPreset] modules_in_preset.csv: 알 수 없는 module_type '{moduleType}' (줄 {i + 1})");
                    break;
            }
        }

        // command_cost는 두 CSV 조합이 끝난 뒤 성능포인트 총합으로 최종 재계산
        for (int i = 0; i < shipPresetDataList.Count; i++)
        {
            ShipPresetData data = shipPresetDataList[i];
            data.commandCost = data.statAllocation.GetTotalPointsUsed(moduleTable, data.prefabName);
        }

        Debug.Log($"[DataTableShipPreset] Modules CSV Import 완료");
        EditorUtility.SetDirty(this);
    }

    // 슬롯 배열 범위를 벗어나면(프리팹 슬롯 수보다 CSV 행이 많음) 경고만 남기고 무시
    private bool AssignSlot(int arrayLength, int slotIndex, string presetId, string moduleType, int lineNumber)
    {
        if (slotIndex >= arrayLength)
        {
            Debug.LogWarning($"[DataTableShipPreset] modules_in_preset.csv: '{presetId}'의 {moduleType} 슬롯이 최대 개수({arrayLength})를 초과했습니다 (줄 {lineNumber + 1})");
            return false;
        }
        return true;
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
