using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;

#if UNITY_EDITOR
using UnityEditor;
#endif


[System.Serializable]
public class DailyBonusRewardEntry
{
    public EDailyBonusTier tier = EDailyBonusTier.Normal;
    public EDailyBonusRewardType rewardType = EDailyBonusRewardType.ExplorationPoint;
    public int amount = 10;
}

[System.Serializable]
public class DailyBonusDayConfig
{
    public int day;
    public DailyBonusRewardEntry[] rewards;
}

[CreateAssetMenu(fileName = "DataTableDailyBonus", menuName = "Custom/DataTableDailyBonus")]
public class DataTableDailyBonus : ScriptableObject
{
    public DailyBonusDayConfig[] days = new DailyBonusDayConfig[0];

    public string GetExportFileName() { return "DataTableDailyBonus"; }

    public DailyBonusRewardEntry[] GetRewards(int day)
    {
        if (days == null) return null;
        for (int i = 0; i < days.Length; i++)
        {
            if (days[i] != null && days[i].day == day)
                return days[i].rewards;
        }
        return null;
    }

    public DailyBonusRewardEntry[] GetRewards(int day, EDailyBonusTier tier)
    {
        DailyBonusRewardEntry[] all = GetRewards(day);
        if (all == null) return null;

        List<DailyBonusRewardEntry> filtered = new List<DailyBonusRewardEntry>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].tier == tier)
                filtered.Add(all[i]);
        }
        return filtered.Count > 0 ? filtered.ToArray() : null;
    }

    public string ExportToJson()
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
        };
        string json = JsonConvert.SerializeObject(days, settings);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
        return json;
    }

    public void ImportFromJson(string json)
    {
        DailyBonusDayConfig[] imported = JsonConvert.DeserializeObject<DailyBonusDayConfig[]>(json);
        if (imported != null)
        {
            days = imported;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    // CSV 형식: day,tier,rewardType,amount  (같은 day 여러 줄 = 복수 보상)
    public void ImportFromCsv(string csv)
    {
        string[] lines = csv.Split('\n');
        Dictionary<int, List<DailyBonusRewardEntry>> temp = new Dictionary<int, List<DailyBonusRewardEntry>>();

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = line.Split(',');
            if (cols.Length < 4) continue;

            if (int.TryParse(cols[0].Trim(), out int day) == false) continue;
            if (System.Enum.TryParse(cols[1].Trim(), out EDailyBonusTier tier) == false) continue;
            if (System.Enum.TryParse(cols[2].Trim(), out EDailyBonusRewardType rewardType) == false) continue;
            if (int.TryParse(cols[3].Trim(), out int amount) == false) continue;

            if (temp.ContainsKey(day) == false)
                temp[day] = new List<DailyBonusRewardEntry>();

            temp[day].Add(new DailyBonusRewardEntry { tier = tier, rewardType = rewardType, amount = amount });
        }

        List<int> keys = new List<int>(temp.Keys);
        keys.Sort();

        days = new DailyBonusDayConfig[keys.Count];
        for (int i = 0; i < keys.Count; i++)
        {
            days[i] = new DailyBonusDayConfig
            {
                day     = keys[i],
                rewards = temp[keys[i]].ToArray()
            };
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }
}
