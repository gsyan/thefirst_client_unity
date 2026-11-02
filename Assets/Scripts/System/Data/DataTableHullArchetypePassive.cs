// 아키타입 패시브(1~5성과 별개 축, 4단계) ScriptableObject — 단계별 해금 조건(counter_type/threshold)과 설명 로컬라이즈 키 관리
// 실제 카운터 증가 로직(격침 수 집계 등)은 아키타입별로 별도 작업(M9~M11) — 여기서는 표시용 데이터만 다룸
// CSV: Assets/Resources/DataTable/HullArchetypePassive/datatable_hull_archetype_passive.csv
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "DataTableHullArchetypePassive", menuName = "Custom/DataTableHullArchetypePassive")]
public class DataTableHullArchetypePassive : ScriptableObject
{
    [SerializeField] private List<HullArchetypePassiveData> passiveDataList = new();

    public List<HullArchetypePassiveData> GetPassiveDataList() { return passiveDataList; }

    // hullArchetype의 1~4단계 데이터를 stage 오름차순으로 반환 — 없으면 빈 리스트
    public List<HullArchetypePassiveData> GetPassivesForArchetype(string hullArchetype)
    {
        List<HullArchetypePassiveData> result = new();
        for (int i = 0; i < passiveDataList.Count; i++)
        {
            if (passiveDataList[i].hullArchetype == hullArchetype)
                result.Add(passiveDataList[i]);
        }
        result.Sort((a, b) => a.stage.CompareTo(b.stage));
        return result;
    }

    #region JSON Export/Import

    public string ExportToJson()
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(passiveDataList, settings);
    }

    public void ImportFromJson(string json)
    {
        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<HullArchetypePassiveData>>(json);
        if (list != null)
        {
            passiveDataList = list;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    #endregion

    #region CSV Import (Editor only)

#if UNITY_EDITOR
    // datatable_hull_archetype_passive.csv 컬럼 순서(인덱스 고정) — hull_archetype, stage, counter_type, threshold, description_key
    public void LoadFromCsv(string csvText)
    {
        passiveDataList.Clear();

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);
            string archetype = GetCol(cols, 0);
            if (string.IsNullOrEmpty(archetype)) continue;

            passiveDataList.Add(new HullArchetypePassiveData
            {
                hullArchetype  = archetype,
                stage          = ParseInt(GetCol(cols, 1)),
                counterType    = GetCol(cols, 2),
                threshold      = ParseInt(GetCol(cols, 3)),
                descriptionKey = GetCol(cols, 4),
            });
        }

        Debug.Log($"[DataTableHullArchetypePassive] CSV Import 완료: {passiveDataList.Count}개");
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
        s = s.Replace(",", "").Trim();
        return int.TryParse(s, out int r) ? r : 0;
    }
#endif

    #endregion
}

[System.Serializable]
public class HullArchetypePassiveData
{
    public string hullArchetype;
    public int stage;           // 1~4
    public string counterType;  // CommanderHullArchetypeProgress.counterType과 매칭되는 자유 문자열
    public int threshold;       // 이 단계 해금에 필요한 누적 카운터값
    public string descriptionKey; // UI.csv 로컬라이즈 키 — 효과 설명 텍스트
}
