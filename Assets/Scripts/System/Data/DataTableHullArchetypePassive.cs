// 아키타입 패시브(1~5성과 별개 축, 4단계) ScriptableObject — 단계별 해금 조건(counter_type/threshold)과 설명 로컬라이즈 키 관리
// 카운터 집계와 해금 판정은 서버가 담당(서버 JSON으로 Export) — 클라는 표시와 Export용 데이터만 다룸
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
    // datatable_hull_archetype_passive.csv 컬럼 순서(인덱스 고정) — hull_archetype, stage, counter_type, threshold, description_key, effect_type, effect_value, effect_scope
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
                effectType     = GetCol(cols, 5),
                effectValue    = GetCol(cols, 6),
                effectScope    = GetCol(cols, 7),
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
    public string counterType;  // 이 단계가 세는 카운터 이름 — 서버 카운터 이벤트(destroy_enemy_ship 등)와 같은 문자열
    public int threshold;       // 이 단계 해금에 필요한 값 — 앞 단계 해금 이후 이 단계 안에서만 0부터 센 값(누적 총량 아님)
    public string descriptionKey; // UI.csv 로컬라이즈 키 — 효과 설명 텍스트
    // 효과 3개 컬럼은 한 단계에 효과가 여러 개일 때 ';'로 구분한 목록(예: "beam_attack_percent;missile_attack_percent" / "50;50" / "fleet") — 값/범위가 1개뿐이면 모든 효과에 같이 적용
    public string effectType;     // 전투에 적용되는 효과 종류(예: scavenge_chance) — 비어 있으면 아직 적용되는 효과 없음
    public string effectValue;    // 효과 수치(퍼센트포인트/초 등 effectType별 단위)
    public string effectScope;    // self(그 아키타입 함선) 또는 fleet(함대 전체)

    private List<HullArchetypePassiveEffect> m_parsedEffects; // 런타임 파싱 캐시 — 직렬화되지 않음

    // 이 단계의 효과 목록 — 첫 호출 때 컬럼 3개를 파싱해 캐시
    public List<HullArchetypePassiveEffect> GetEffects()
    {
        if (m_parsedEffects != null) return m_parsedEffects;

        m_parsedEffects = new List<HullArchetypePassiveEffect>();
        if (string.IsNullOrEmpty(effectType) == true) return m_parsedEffects;

        string[] types = effectType.Split(';');
        string[] values = string.IsNullOrEmpty(effectValue) == false ? effectValue.Split(';') : new string[0];
        string[] scopes = string.IsNullOrEmpty(effectScope) == false ? effectScope.Split(';') : new string[0];

        for (int i = 0; i < types.Length; i++)
        {
            string type = types[i].Trim();
            if (string.IsNullOrEmpty(type) == true) continue;

            string valueText = values.Length == 0 ? "0" : values[Mathf.Min(i, values.Length - 1)];
            string scopeText = scopes.Length == 0 ? "self" : scopes[Mathf.Min(i, scopes.Length - 1)];
            bool isParsed = float.TryParse(valueText.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value);
            if (isParsed == false)
                Debug.LogError($"[HullArchetypePassive] 효과 수치 파싱 실패: {hullArchetype} stage={stage} type={type} value='{valueText}'");

            m_parsedEffects.Add(new HullArchetypePassiveEffect { effectType = type, effectValue = value, effectScope = scopeText.Trim() });
        }
        return m_parsedEffects;
    }
}

public class HullArchetypePassiveEffect
{
    public string effectType;
    public float effectValue;
    public string effectScope;
}
