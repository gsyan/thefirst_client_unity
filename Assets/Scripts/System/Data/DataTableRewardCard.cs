// 셀 클리어 보상카드 종류 테이블 ScriptableObject
// CSV Import(에디터 전용) → ScriptableObject 갱신 → JSON Export → 서버 배포 순서로 사용 (DataTableShipPreset과 동일 컨벤션)
// CSV: Assets/Resources/DataTable/RewardCard/datatable_reward_card.csv
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "DataTableRewardCard", menuName = "Custom/DataTableRewardCard")]
public class DataTableRewardCard : ScriptableObject
{
    [SerializeField] private List<RewardCardData> rewardCardDataList = new();

    public List<RewardCardData> GetRewardCardDataList() { return rewardCardDataList; }

    public RewardCardData GetCard(string cardId)
    {
        for (int i = 0; i < rewardCardDataList.Count; i++)
        {
            if (rewardCardDataList[i].cardId == cardId)
                return rewardCardDataList[i];
        }
        return null;
    }

    #region JSON Export/Import

    public string ExportToJson()
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(rewardCardDataList, settings);
    }

    public void ImportFromJson(string json)
    {
        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RewardCardData>>(json);
        if (list != null)
        {
            rewardCardDataList = list;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    // 서버가 후보 추첨/효과 적용에 필요한 필드만 선별 — nameKey/descKey/rarity는 클라 표시 전용이라 제외
    public string ExportToServerJson()
    {
        var serverList = new List<object>();
        for (int i = 0; i < rewardCardDataList.Count; i++)
        {
            RewardCardData data = rewardCardDataList[i];
            serverList.Add(new
            {
                cardId = data.cardId,
                effectType = data.effectType.ToString(),
                isPersistent = data.isPersistent,
                value1 = data.value1,
                value2 = data.value2,
                weight = data.weight,
            });
        }

        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(serverList, settings);
    }

    #endregion

    #region CSV Import/Export (Editor only)

#if UNITY_EDITOR
    public string ExportCsv()
    {
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("card_id,name_key,desc_key,rarity,effect_type,is_persistent,value1,value2,weight,icon_name");

        for (int i = 0; i < rewardCardDataList.Count; i++)
        {
            RewardCardData data = rewardCardDataList[i];
            sb.AppendLine(string.Format(ic, "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                data.cardId, data.nameKey, data.descKey, data.rarity, data.effectType,
                data.isPersistent, data.value1, data.value2, data.weight, data.iconName));
        }

        return sb.ToString();
    }

    public void LoadCsv(string csvText)
    {
        rewardCardDataList.Clear();

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);
            string cardId = GetCol(cols, 0);
            if (string.IsNullOrEmpty(cardId)) continue;

            if (System.Enum.TryParse(GetCol(cols, 4), out ECardEffectType effectType) == false)
            {
                Debug.LogWarning($"[DataTableRewardCard] 알 수 없는 effect_type '{GetCol(cols, 4)}' (줄 {i + 1})");
                continue;
            }

            rewardCardDataList.Add(new RewardCardData
            {
                cardId = cardId,
                nameKey = GetCol(cols, 1),
                descKey = GetCol(cols, 2),
                rarity = ParseInt(GetCol(cols, 3)),
                effectType = effectType,
                isPersistent = ParseBool(GetCol(cols, 5)),
                value1 = ParseFloat(GetCol(cols, 6)),
                value2 = ParseFloat(GetCol(cols, 7)),
                weight = ParseInt(GetCol(cols, 8)),
                iconName = GetCol(cols, 9),
            });
        }

        Debug.Log($"[DataTableRewardCard] CSV Import 완료: {rewardCardDataList.Count}개");
        EditorUtility.SetDirty(this);
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
        return int.TryParse(s, out int r) ? r : 0;
    }

    private float ParseFloat(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0f;
        return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r) ? r : 0f;
    }

    private bool ParseBool(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        return bool.TryParse(s, out bool r) ? r : false;
    }
#endif

    #endregion
}
