// 존 번호별 탐사 그리드 크기 규칙표 ScriptableObject
// CSV Import(에디터 전용) → ScriptableObject 갱신 → JSON Export → 서버 배포 순서로 사용
// CSV: Assets/Resources/DataTable/Exploration/datatable_zone_grid_size.csv
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "DataTableZoneGridSize", menuName = "Custom/DataTableZoneGridSize")]
public class DataTableZoneGridSize : ScriptableObject
{
    [SerializeField] private List<ZoneGridSizeData> zoneGridSizeDataList = new();

    public List<ZoneGridSizeData> GetZoneGridSizeDataList() { return zoneGridSizeDataList; }

    // zoneNumber가 속한 구간의 그리드 가로/세로 크기 반환. CSV는 zone_max 오름차순 정렬 전제
    // zoneMin은 저장하지 않음 — 이전 행의 zoneMax+1로 유추 가능한 중복 데이터이므로 제외
    public GridDimensions GetGridDimensions(int zoneNumber)
    {
        for (int i = 0; i < zoneGridSizeDataList.Count; i++)
        {
            ZoneGridSizeData data = zoneGridSizeDataList[i];
            if (zoneNumber <= data.zoneMax)
            {
                return new GridDimensions(data.gridWidth, data.gridHeight);
            }
        }

        if (zoneGridSizeDataList.Count > 0)
        {
            ZoneGridSizeData lastData = zoneGridSizeDataList[zoneGridSizeDataList.Count - 1];
            return new GridDimensions(lastData.gridWidth, lastData.gridHeight);
        }

        return new GridDimensions(3, 3);
    }

    #region JSON Export/Import

    public string ExportToJson()
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(zoneGridSizeDataList, settings);
    }

    public void ImportFromJson(string json)
    {
        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ZoneGridSizeData>>(json);
        if (list != null)
        {
            zoneGridSizeDataList = list;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    #endregion

    #region CSV Import (Editor only)

#if UNITY_EDITOR
    public string ExportToCsv()
    {
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("zone_max,grid_width,grid_height");

        for (int i = 0; i < zoneGridSizeDataList.Count; i++)
        {
            ZoneGridSizeData data = zoneGridSizeDataList[i];
            sb.AppendLine(string.Format(ic, "{0},{1},{2}", data.zoneMax, data.gridWidth, data.gridHeight));
        }

        return sb.ToString();
    }

    // datatable_zone_grid_size.csv 컬럼 순서(인덱스 고정) — zone_max, grid_width, grid_height
    public void LoadFromCsv(string csvText)
    {
        zoneGridSizeDataList.Clear();

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);
            bool parsed = int.TryParse(GetCol(cols, 0), out int zoneMax);
            if (parsed == false) continue;

            zoneGridSizeDataList.Add(new ZoneGridSizeData
            {
                zoneMax     = zoneMax,
                gridWidth   = ParseInt(GetCol(cols, 1)),
                gridHeight  = ParseInt(GetCol(cols, 2)),
            });
        }

        Debug.Log($"[DataTableZoneGridSize] CSV Import 완료: {zoneGridSizeDataList.Count}개");
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
public class ZoneGridSizeData
{
    public int zoneMax;
    public int gridWidth;
    public int gridHeight;
}
