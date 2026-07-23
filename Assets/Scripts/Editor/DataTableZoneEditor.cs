// DataTableZone 에디터 - 존 데이터 편집 GUI
// CSV Import: datatable_zone_camera.csv + datatable_zone_celestial.csv + datatable_zone_grid.csv → ScriptableObject
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

[CustomEditor(typeof(DataTableZone))]
public class DataTableZoneEditor : Editor
{
    private DataTableZone m_dataTableZone;
    private Vector2 scrollPosition;

    private static readonly string k_selectedZonePrefKey = "DataTableZoneEditor_SelectedZone";

    private void OnEnable()
    {
        m_dataTableZone = (DataTableZone)target;
        m_selectedZoneIndex = EditorPrefs.GetInt(k_selectedZonePrefKey, 1);
    }

    public override void OnInspectorGUI()
    {
        if (m_dataTableZone == null) return;

        serializedObject.Update();
        Undo.RecordObject(m_dataTableZone, "Edit DataTableZone");

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
            EditorUtility.SetDirty(m_dataTableZone);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private new void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("Datatable Zone", EditorStyles.largeLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Zones: {m_dataTableZone.zoneList.Count(z => z.zoneIndex > 0)}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawUtilityTools()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Utility Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Import All", GUILayout.Height(28))) ImportAll();
        if (GUILayout.Button("Export All", GUILayout.Height(28))) ExportAll();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);
        DrawPlanetGeneratorTool();
    }

    private void ImportAll()
    {
        ImportCamera();
        ImportCelestial();
        ImportGrid();
        EditorUtility.DisplayDialog("완료", "전체 Import 완료", "OK");
    }

    private void ExportAll()
    {
        DataTableZoneCSVUtility.ExportZone(m_dataTableZone);
        DataTableZoneCSVUtility.ExportCelestial(m_dataTableZone);
        DataTableZoneCSVUtility.ExportGrid(m_dataTableZone);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("완료", "전체 Export 완료", "OK");
    }

    private static readonly string k_cameraCSV      = "Assets/Resources/DataTable/Zone/datatable_zone_camera.csv";
    private static readonly string k_celestialCSV   = "Assets/Resources/DataTable/Zone/datatable_zone_celestial.csv";
    private static readonly string k_gridCSV        = "Assets/Resources/DataTable/Zone/datatable_zone_grid.csv";

    private void ImportCamera()
    {
        if (!File.Exists(k_cameraCSV)) { EditorUtility.DisplayDialog("Error", $"파일 없음:\n{k_cameraCSV}", "OK"); return; }

        // celestialBodies 유지를 위해 기존 맵 보존
        var oldCelestialMap = new Dictionary<int, List<CelestialBodyConfig>>();
        for (int j = 0; j < m_dataTableZone.zoneList.Count; j++)
        {
            ZoneConfig zc = m_dataTableZone.zoneList[j];
            oldCelestialMap[zc.zoneIndex] = zc.celestialBodies;
        }

        m_dataTableZone.zoneList.Clear();
        string[] lines = File.ReadAllLines(k_cameraCSV);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            if (!int.TryParse(col[0], out int zoneIndex)) continue;
            float.TryParse(col[1], out float cx); float.TryParse(col[2], out float cy); float.TryParse(col[3], out float cz);
            float.TryParse(col[4], out float zoom); float.TryParse(col[5], out float rotX); float.TryParse(col[6], out float rotY);
            oldCelestialMap.TryGetValue(zoneIndex, out var celestials);
            m_dataTableZone.zoneList.Add(new ZoneConfig
            {
                zoneIndex          = zoneIndex,
                galaxyCameraTarget = new Vector3(cx, cy, cz),
                galaxyCameraZoom   = zoom,
                galaxyCameraRotX   = rotX,
                galaxyCameraRotY   = rotY,
                celestialBodies    = celestials != null ? celestials : new List<CelestialBodyConfig>(),
            });
        }
        EditorUtility.SetDirty(m_dataTableZone);
        AssetDatabase.Refresh();
        Debug.Log($"[DataTableZone] Zone Camera import 완료 ({m_dataTableZone.zoneList.Count}개)");
    }

    private void ImportCelestial()
    {
        if (!File.Exists(k_celestialCSV)) { EditorUtility.DisplayDialog("Error", $"파일 없음:\n{k_celestialCSV}", "OK"); return; }

        var zoneMap = new Dictionary<int, ZoneConfig>();
        for (int j = 0; j < m_dataTableZone.zoneList.Count; j++)
        {
            m_dataTableZone.zoneList[j].celestialBodies = new List<CelestialBodyConfig>();
            zoneMap[m_dataTableZone.zoneList[j].zoneIndex] = m_dataTableZone.zoneList[j];
        }

        string[] lines = File.ReadAllLines(k_celestialCSV);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            if (!int.TryParse(col[0], out int zi)) continue;
            if (!zoneMap.TryGetValue(zi, out ZoneConfig zc)) continue;

            float F(int idx, float def = 0f) => col.Length > idx && float.TryParse(col[idx], out float v) ? v : def;
            bool  B(int idx)                 => col.Length > idx && col[idx].Trim().ToLower() == "true";
            Color C(int idx, Color def)      { if (col.Length <= idx) return def; return ColorUtility.TryParseHtmlString(col[idx].Trim(), out Color c) ? c : def; }

            zc.celestialBodies.Add(new CelestialBodyConfig
            {
                position = new Vector3(F(1), F(2), F(3)), rotation = new Vector3(F(4), F(5), F(6)), scale = new Vector3(F(7), F(8), F(9)),
                landCoverage = F(10, 0.5f), biomeBlend = F(11, 0.01f), gBlend = F(12, 0.02f),
                deepSeaColor = C(13, CommonUtility.HexColor("#0D2673")), shallowSeaColor = C(14, CommonUtility.HexColor("#1A59A6")),
                lowlandSandColor = C(15, CommonUtility.HexColor("#BFB380")), lowlandGreenColor = C(16, CommonUtility.HexColor("#90C060")),
                plainsDesertColor = C(17, CommonUtility.HexColor("#A99159")), plainsGrassColor = C(18, CommonUtility.HexColor("#478C2E")),
                plainsForestColor = C(19, CommonUtility.HexColor("#236523")), highlandSnowColor = C(20, CommonUtility.HexColor("#E8F0F5")),
                hasPolarIce = B(21), iceColor = C(22, CommonUtility.HexColor("#F2FAFF")), iceColorEdge = C(23, CommonUtility.HexColor("#ADD1F0")), poleIceWidth = F(24, 0.12f),
                hasClouds = B(25), cloudColor = C(26, CommonUtility.HexColor("#FFFFFFD9")), cloudCoverage = F(27, 0.5f), cloudRotation = F(28), cloudScale = F(29, 1.01f),
                cloudMidLatOpacity = F(30, 0f), cloudMidLatCenter = F(31, 0.25f), cloudMidLatWidth = F(32, 0.12f), cloudSoftness = F(33, 0.3f),
                hasAtmosphere = B(34), atmosphereColor = C(35, CommonUtility.HexColor("#4D99FF")), atmosphereScale = F(36, 1.01f),
            });
        }
        EditorUtility.SetDirty(m_dataTableZone);
        AssetDatabase.Refresh();
        Debug.Log("[DataTableZone] Celestial import 완료");
    }

    private void ImportGrid()
    {
        if (!File.Exists(k_gridCSV)) { EditorUtility.DisplayDialog("Error", $"파일 없음:\n{k_gridCSV}", "OK"); return; }

        var zoneMap = new Dictionary<int, ZoneConfig>();
        for (int j = 0; j < m_dataTableZone.zoneList.Count; j++)
            zoneMap[m_dataTableZone.zoneList[j].zoneIndex] = m_dataTableZone.zoneList[j];

        string[] lines = File.ReadAllLines(k_gridCSV);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] col = line.Split(',');
            if (!int.TryParse(col[0], out int zi)) continue;
            if (!zoneMap.TryGetValue(zi, out ZoneConfig zc)) continue;

            int.TryParse(col[1], out zc.gridWidth);
            int.TryParse(col[2], out zc.gridHeight);
            int.TryParse(col[3], out zc.enemyFleetsPerCell);
            int.TryParse(col[4], out zc.enemyBudget);
            int.TryParse(col[5], out zc.enemyMaxCost);
            int.TryParse(col[6], out zc.enemyDeviation);
            int.TryParse(col[7], out zc.enemyMaxShipsPerFleet);
        }
        EditorUtility.SetDirty(m_dataTableZone);
        AssetDatabase.Refresh();
        Debug.Log("[DataTableZone] Grid import 완료");
    }

    #region 행성 절차적 생성 (zoneList.celestialBodies 직접 수정)

    // 행성 타입 템플릿 — zone1~9의 수작업 팔레트를 기준으로 삼고, 색은 타입 내에서만 hue/sat/val을 살짝 흔들어 재사용
    private class PlanetTypeTemplate
    {
        public string name;
        public Color deepSea, shallowSea, lowlandSand, lowlandGreen, plainsDesert, plainsGrass, plainsForest, highlandSnow;
        public Color iceColor, iceColorEdge, cloudColor, atmosphereColor;
        public float landCoverage, biomeBlend, gBlend;
        public bool  hasPolarIce;
        public float poleIceWidth = 0.12f;
        public bool  hasClouds;
        public float cloudCoverage = 0.5f, cloudMidLatOpacity = 0.5f, cloudMidLatCenter = 0.3f, cloudMidLatWidth = 0.2f, cloudSoftness = 0.4f;
    }

    private static readonly PlanetTypeTemplate[] k_planetTypes = new PlanetTypeTemplate[]
    {
        new PlanetTypeTemplate { name = "지구형(온대)", // Zone 1 기준
            deepSea = CommonUtility.HexColor("#0D2673"), shallowSea = CommonUtility.HexColor("#1959A5"),
            lowlandSand = CommonUtility.HexColor("#BFB380"), lowlandGreen = CommonUtility.HexColor("#90C060"),
            plainsDesert = CommonUtility.HexColor("#A99159"), plainsGrass = CommonUtility.HexColor("#478C2E"), plainsForest = CommonUtility.HexColor("#236523"),
            highlandSnow = CommonUtility.HexColor("#E8F0F5"), iceColor = CommonUtility.HexColor("#F2F9FF"), iceColorEdge = CommonUtility.HexColor("#ADD1EF"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD8"), atmosphereColor = CommonUtility.HexColor("#4C99FF"),
            landCoverage = 0.5f, biomeBlend = 0.1f, gBlend = 0.8f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.5f },

        new PlanetTypeTemplate { name = "지구형(해양)", // Zone 2 기준
            deepSea = CommonUtility.HexColor("#0D2673"), shallowSea = CommonUtility.HexColor("#1A59A6"),
            lowlandSand = CommonUtility.HexColor("#BFB380"), lowlandGreen = CommonUtility.HexColor("#90C060"),
            plainsDesert = CommonUtility.HexColor("#A99159"), plainsGrass = CommonUtility.HexColor("#478C2E"), plainsForest = CommonUtility.HexColor("#236523"),
            highlandSnow = CommonUtility.HexColor("#E8F0F5"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#4D99FF"),
            landCoverage = 0.4f, biomeBlend = 0.07f, gBlend = 0.3f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.2f },

        new PlanetTypeTemplate { name = "용암형", // Zone 3 기준
            deepSea = CommonUtility.HexColor("#D66600"), shallowSea = CommonUtility.HexColor("#F7CA11"),
            lowlandSand = CommonUtility.HexColor("#BFB380"), lowlandGreen = CommonUtility.HexColor("#E3F084"),
            plainsDesert = CommonUtility.HexColor("#A99159"), plainsGrass = CommonUtility.HexColor("#E4DA64"), plainsForest = CommonUtility.HexColor("#C23D25"),
            highlandSnow = CommonUtility.HexColor("#C52225"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#A50000D9"), atmosphereColor = CommonUtility.HexColor("#FCC705"),
            landCoverage = 0.28f, biomeBlend = 0f, gBlend = 0f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.48f },

        new PlanetTypeTemplate { name = "극지형", // Zone 4 기준
            deepSea = CommonUtility.HexColor("#0D2673"), shallowSea = CommonUtility.HexColor("#3E6EAA"),
            lowlandSand = CommonUtility.HexColor("#BFB380"), lowlandGreen = CommonUtility.HexColor("#90C060"),
            plainsDesert = CommonUtility.HexColor("#D6C08E"), plainsGrass = CommonUtility.HexColor("#478C2E"), plainsForest = CommonUtility.HexColor("#5F845F"),
            highlandSnow = CommonUtility.HexColor("#E8F0F5"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#4D99FF"),
            landCoverage = 0.611f, biomeBlend = 0.07f, gBlend = 0.78f, hasPolarIce = true, hasClouds = false },

        new PlanetTypeTemplate { name = "외계(보라)형", // Zone 5 기준
            deepSea = CommonUtility.HexColor("#6F38D9"), shallowSea = CommonUtility.HexColor("#6D19A6"),
            lowlandSand = CommonUtility.HexColor("#590090"), lowlandGreen = CommonUtility.HexColor("#6860C0"),
            plainsDesert = CommonUtility.HexColor("#5968A9"), plainsGrass = CommonUtility.HexColor("#2D5B8C"), plainsForest = CommonUtility.HexColor("#76297A"),
            highlandSnow = CommonUtility.HexColor("#71336A"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#FF4DF6"),
            landCoverage = 0.637f, biomeBlend = 0.09f, gBlend = 1.57f, hasPolarIce = false, hasClouds = false },

        new PlanetTypeTemplate { name = "사막형", // Zone 6 기준
            deepSea = CommonUtility.HexColor("#896C28"), shallowSea = CommonUtility.HexColor("#A89260"),
            lowlandSand = CommonUtility.HexColor("#BB9F5C"), lowlandGreen = CommonUtility.HexColor("#998147"),
            plainsDesert = CommonUtility.HexColor("#B6A170"), plainsGrass = CommonUtility.HexColor("#B27E05"), plainsForest = CommonUtility.HexColor("#6E6144"),
            highlandSnow = CommonUtility.HexColor("#E8F0F5"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#BC9F5D"),
            landCoverage = 0.5f, biomeBlend = 0.1f, gBlend = 0.61f, hasPolarIce = false, hasClouds = false },

        new PlanetTypeTemplate { name = "오션(청록)형", // Zone 7 기준
            deepSea = CommonUtility.HexColor("#0D2673"), shallowSea = CommonUtility.HexColor("#1A59A6"),
            lowlandSand = CommonUtility.HexColor("#576BCC"), lowlandGreen = CommonUtility.HexColor("#0F2156"),
            plainsDesert = CommonUtility.HexColor("#6EB2A8"), plainsGrass = CommonUtility.HexColor("#2D6C8C"), plainsForest = CommonUtility.HexColor("#233665"),
            highlandSnow = CommonUtility.HexColor("#E8F0F5"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#4D99FF"),
            landCoverage = 0.5f, biomeBlend = 0.01f, gBlend = 0.47f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.34f },

        new PlanetTypeTemplate { name = "화성(적색)형", // Zone 8 기준
            deepSea = CommonUtility.HexColor("#73330D"), shallowSea = CommonUtility.HexColor("#A64C19"),
            lowlandSand = CommonUtility.HexColor("#A16A5B"), lowlandGreen = CommonUtility.HexColor("#C06D60"),
            plainsDesert = CommonUtility.HexColor("#A95F59"), plainsGrass = CommonUtility.HexColor("#8C522D"), plainsForest = CommonUtility.HexColor("#653A23"),
            highlandSnow = CommonUtility.HexColor("#D65941"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#D65A42"),
            landCoverage = 0.618f, biomeBlend = 0.01f, gBlend = 0.61f, hasPolarIce = true, hasClouds = false },

        new PlanetTypeTemplate { name = "지구형(심림)", // Zone 9 기준
            deepSea = CommonUtility.HexColor("#0D2673"), shallowSea = CommonUtility.HexColor("#1A59A6"),
            lowlandSand = CommonUtility.HexColor("#265D24"), lowlandGreen = CommonUtility.HexColor("#90C060"),
            plainsDesert = CommonUtility.HexColor("#11540D"), plainsGrass = CommonUtility.HexColor("#478C2E"), plainsForest = CommonUtility.HexColor("#236523"),
            highlandSnow = CommonUtility.HexColor("#E8F0F5"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFFFFFD9"), atmosphereColor = CommonUtility.HexColor("#4D99FF"),
            landCoverage = 0.578f, biomeBlend = 0.01f, gBlend = 0.68f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.24f },

        new PlanetTypeTemplate { name = "독성/유독형", // 신규
            deepSea = CommonUtility.HexColor("#0D3B1A"), shallowSea = CommonUtility.HexColor("#4CBB17"),
            lowlandSand = CommonUtility.HexColor("#4A5D23"), lowlandGreen = CommonUtility.HexColor("#6FBF3C"),
            plainsDesert = CommonUtility.HexColor("#355E1F"), plainsGrass = CommonUtility.HexColor("#8FD400"), plainsForest = CommonUtility.HexColor("#1B3B0E"),
            highlandSnow = CommonUtility.HexColor("#C8FFB0"), iceColor = CommonUtility.HexColor("#E4FFD0"), iceColorEdge = CommonUtility.HexColor("#B0E890"),
            cloudColor = CommonUtility.HexColor("#7FFF0090"), atmosphereColor = CommonUtility.HexColor("#39FF14"),
            landCoverage = 0.5f, biomeBlend = 0.05f, gBlend = 0.5f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.35f },

        new PlanetTypeTemplate { name = "얼음/동결형", // 신규
            deepSea = CommonUtility.HexColor("#1B3A57"), shallowSea = CommonUtility.HexColor("#6FA8DC"),
            lowlandSand = CommonUtility.HexColor("#DCEEFB"), lowlandGreen = CommonUtility.HexColor("#BFE3F0"),
            plainsDesert = CommonUtility.HexColor("#E8F4FA"), plainsGrass = CommonUtility.HexColor("#D2ECF9"), plainsForest = CommonUtility.HexColor("#A9CEDC"),
            highlandSnow = CommonUtility.HexColor("#FFFFFF"), iceColor = CommonUtility.HexColor("#FFFFFF"), iceColorEdge = CommonUtility.HexColor("#CFE9FF"),
            cloudColor = CommonUtility.HexColor("#FFFFFFE0"), atmosphereColor = CommonUtility.HexColor("#CFE9FF"),
            landCoverage = 0.8f, biomeBlend = 0.05f, gBlend = 0.5f, hasPolarIce = true, poleIceWidth = 0.3f, hasClouds = true, cloudCoverage = 0.3f },

        new PlanetTypeTemplate { name = "가스/거인형", // 신규 (용암형과 같은 셰이더를 색만 바꿔 밴드 느낌)
            deepSea = CommonUtility.HexColor("#D9A441"), shallowSea = CommonUtility.HexColor("#E8C170"),
            lowlandSand = CommonUtility.HexColor("#C97B3D"), lowlandGreen = CommonUtility.HexColor("#E0B080"),
            plainsDesert = CommonUtility.HexColor("#B5651D"), plainsGrass = CommonUtility.HexColor("#D2955B"), plainsForest = CommonUtility.HexColor("#8B4A1E"),
            highlandSnow = CommonUtility.HexColor("#F5E1C8"), iceColor = CommonUtility.HexColor("#F2FAFF"), iceColorEdge = CommonUtility.HexColor("#ADD1F0"),
            cloudColor = CommonUtility.HexColor("#FFF3D9C0"), atmosphereColor = CommonUtility.HexColor("#FFD27F"),
            landCoverage = 0.5f, biomeBlend = 0.15f, gBlend = 2.0f, hasPolarIce = false, hasClouds = true, cloudCoverage = 0.6f },
    };

    private int  m_genPlanetZoneStart = 10;
    private int  m_genPlanetZoneEnd   = 100;
    private int  m_genPlanetSeed      = 20260709;
    private bool m_genPlanetFoldout   = false;

    private void DrawPlanetGeneratorTool()
    {
        EditorGUILayout.BeginVertical("box");
        m_genPlanetFoldout = EditorGUILayout.Foldout(m_genPlanetFoldout, "행성 절차적 생성", true, EditorStyles.foldoutHeader);
        if (m_genPlanetFoldout)
        {
            EditorGUILayout.HelpBox($"zoneStart~zoneEnd 구간의 zoneList[].celestialBodies[0]을 타입 템플릿({k_planetTypes.Length}종) 중 하나로 재생성합니다.\n" +
                "타입은 존마다 시드 기반으로 하나 골라 그 팔레트 안에서만 hue/채도/명도를 살짝 흔듭니다(완전 랜덤 RGB 아님).", MessageType.Info);

            EditorGUI.indentLevel++;
            m_genPlanetZoneStart = EditorGUILayout.IntField("Zone Start", m_genPlanetZoneStart);
            m_genPlanetZoneEnd   = EditorGUILayout.IntField("Zone End",   m_genPlanetZoneEnd);
            m_genPlanetSeed      = EditorGUILayout.IntField("Random Seed", m_genPlanetSeed);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Generate", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("행성 절차적 생성",
                    $"zone{m_genPlanetZoneStart}~{m_genPlanetZoneEnd} 구간의 행성 데이터를 재생성합니다.\n계속하시겠습니까?", "Generate", "Cancel"))
                {
                    GeneratePlanetData();
                }
            }
        }
        EditorGUILayout.EndVertical();
    }

    private Color JitterColor(Color c, System.Random rng, float hueJitter, float satJitter, float valJitter)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        h = Mathf.Repeat(h + ((float)rng.NextDouble() * 2f - 1f) * hueJitter, 1f);
        s = Mathf.Clamp01(s + ((float)rng.NextDouble() * 2f - 1f) * satJitter);
        v = Mathf.Clamp01(v + ((float)rng.NextDouble() * 2f - 1f) * valJitter);
        Color result = Color.HSVToRGB(h, s, v);
        result.a = c.a;
        return result;
    }

    // base 주변 ±range로 흔들고 min~max로 clamp (완전 랜덤이 아니라 타입의 base 수치 근처로만 변형)
    private float JitterFloat(float baseValue, float range, System.Random rng, float min, float max)
    {
        float v = baseValue + ((float)rng.NextDouble() * 2f - 1f) * range;
        return Mathf.Clamp(v, min, max);
    }

    private void GeneratePlanetData()
    {
        var zoneMap = new Dictionary<int, ZoneConfig>();
        for (int i = 0; i < m_dataTableZone.zoneList.Count; i++)
            zoneMap[m_dataTableZone.zoneList[i].zoneIndex] = m_dataTableZone.zoneList[i];

        int touched = 0;
        for (int zone = m_genPlanetZoneStart; zone <= m_genPlanetZoneEnd; zone++)
        {
            if (!zoneMap.TryGetValue(zone, out ZoneConfig zoneConfig))
            {
                zoneConfig = new ZoneConfig { zoneIndex = zone };
                m_dataTableZone.zoneList.Add(zoneConfig);
                zoneMap[zone] = zoneConfig;
            }

            var rng = new System.Random(m_genPlanetSeed ^ (zone * 73856093));
            PlanetTypeTemplate type = k_planetTypes[rng.Next(0, k_planetTypes.Length)];

            var body = new CelestialBodyConfig
            {
                position = Vector3.zero,
                rotation = new Vector3(JitterFloat(0f, 20f, rng, -40f, 40f), rng.Next(0, 360), JitterFloat(0f, 20f, rng, -40f, 40f)),
                scale    = new Vector3(500f, 500f, 500f),

                deepSeaColor      = JitterColor(type.deepSea,      rng, 0.03f, 0.08f, 0.08f),
                shallowSeaColor   = JitterColor(type.shallowSea,   rng, 0.03f, 0.08f, 0.08f),
                lowlandSandColor  = JitterColor(type.lowlandSand,  rng, 0.03f, 0.08f, 0.08f),
                lowlandGreenColor = JitterColor(type.lowlandGreen, rng, 0.03f, 0.08f, 0.08f),
                plainsDesertColor = JitterColor(type.plainsDesert, rng, 0.03f, 0.08f, 0.08f),
                plainsGrassColor  = JitterColor(type.plainsGrass,  rng, 0.03f, 0.08f, 0.08f),
                plainsForestColor = JitterColor(type.plainsForest, rng, 0.03f, 0.08f, 0.08f),
                highlandSnowColor = JitterColor(type.highlandSnow, rng, 0.03f, 0.05f, 0.05f),

                landCoverage = JitterFloat(type.landCoverage, 0.1f, rng, 0f, 1f),
                biomeBlend   = JitterFloat(type.biomeBlend,   0.02f, rng, 0f, 0.2f),
                gBlend       = JitterFloat(type.gBlend,       0.2f, rng, 0f, 5f),

                hasPolarIce  = type.hasPolarIce,
                iceColor     = JitterColor(type.iceColor,     rng, 0.02f, 0.05f, 0.05f),
                iceColorEdge = JitterColor(type.iceColorEdge, rng, 0.02f, 0.05f, 0.05f),
                poleIceWidth = type.poleIceWidth,

                hasClouds          = type.hasClouds,
                cloudColor         = JitterColor(type.cloudColor, rng, 0.02f, 0.05f, 0.05f),
                cloudCoverage      = JitterFloat(type.cloudCoverage, 0.15f, rng, 0f, 1f),
                cloudRotation      = rng.Next(0, 360),
                cloudScale         = 1.01f,
                cloudMidLatOpacity = type.cloudMidLatOpacity,
                cloudMidLatCenter  = type.cloudMidLatCenter,
                cloudMidLatWidth   = type.cloudMidLatWidth,
                cloudSoftness      = type.cloudSoftness,

                hasAtmosphere   = true,
                atmosphereColor = JitterColor(type.atmosphereColor, rng, 0.02f, 0.05f, 0.05f),
                atmosphereScale = 1.01f,
            };

            zoneConfig.celestialBodies = new List<CelestialBodyConfig> { body };
            touched++;
        }

        EditorUtility.SetDirty(m_dataTableZone);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("완료", $"행성 생성 완료! ({touched}개 존)", "OK");
    }

    #endregion

    private int m_selectedZoneIndex = 1;

    // Zone 번호 입력 필드 → 그 존 하나의 정보만 아래에 표시 (100개 목록 나열 대신 동적 조회)
    // 선택한 Zone 번호는 EditorPrefs에 저장되어 에디터를 재시작해도 유지됨
    private void DrawZoneList()
    {
        EditorGUILayout.BeginHorizontal("box");
        EditorGUI.BeginChangeCheck();
        int newZoneIndex = EditorGUILayout.IntField("Zone", m_selectedZoneIndex);
        if (EditorGUI.EndChangeCheck())
        {
            m_selectedZoneIndex = newZoneIndex;
            EditorPrefs.SetInt(k_selectedZonePrefKey, m_selectedZoneIndex);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);

        if (m_selectedZoneIndex <= 0)
        {
            EditorGUILayout.HelpBox("Zone 번호는 1 이상이어야 합니다.", MessageType.Info);
            return;
        }

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.8f, 0.8f, 0.95f);
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;

        DrawZoneConfig(m_selectedZoneIndex);

        EditorGUILayout.EndVertical();
    }

    // ZoneConfig 편집 UI (스카이박스 + 갤럭시 카메라 앵커)
    private void DrawZoneConfig(int zoneIndex)
    {
        ZoneConfig zoneConfig = null;
        for (int i = 0; i < m_dataTableZone.zoneList.Count; i++)
        {
            if (m_dataTableZone.zoneList[i].zoneIndex == zoneIndex)
            {
                zoneConfig = m_dataTableZone.zoneList[i];
                break;
            }
        }

        if (zoneConfig == null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("이 그룹의 스카이박스 설정이 없습니다.", MessageType.Info);
            if (GUILayout.Button("생성", GUILayout.Width(50)))
            {
                zoneConfig = new ZoneConfig { zoneIndex = zoneIndex };
                m_dataTableZone.zoneList.Add(zoneConfig);
                EditorUtility.SetDirty(m_dataTableZone);
            }
            EditorGUILayout.EndHorizontal();
            return;
        }

        EditorGUI.BeginChangeCheck();

        // 존 번호가 바뀔 때마다 다시 그려지는 단일 상세 뷰라 접힐 필요가 없음 — 항상 펼친 상태로 표시
        EditorGUILayout.LabelField("갤럭시 뷰 카메라 앵커", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        zoneConfig.galaxyCameraTarget = EditorGUILayout.Vector3Field("Camera Target", zoneConfig.galaxyCameraTarget);
        zoneConfig.galaxyCameraZoom   = EditorGUILayout.FloatField("Camera Zoom", zoneConfig.galaxyCameraZoom);
        zoneConfig.galaxyCameraRotX   = EditorGUILayout.Slider("Rot X (앙각)", zoneConfig.galaxyCameraRotX, -80f, 80f);
        zoneConfig.galaxyCameraRotY   = EditorGUILayout.FloatField("Rot Y (수평)", zoneConfig.galaxyCameraRotY);
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("천체 배치", EditorStyles.boldLabel);
        if (zoneConfig.celestialBodies == null)
            zoneConfig.celestialBodies = new System.Collections.Generic.List<CelestialBodyConfig>();
        EditorGUI.indentLevel++;
        CelestialBodyEditorGUI.DrawCelestialBodyList(zoneIndex, zoneConfig.celestialBodies, m_dataTableZone);
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("탐사 그리드 / 적 함대 설정", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("그리드 크기", EditorStyles.miniBoldLabel);
        zoneConfig.gridWidth  = EditorGUILayout.IntField(new GUIContent("Grid Width",  "탐사 그리드 가로 셀 수"), zoneConfig.gridWidth);
        zoneConfig.gridHeight = EditorGUILayout.IntField(new GUIContent("Grid Height", "탐사 그리드 세로 셀 수"), zoneConfig.gridHeight);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("셀 적함대 절차적 생성", EditorStyles.miniBoldLabel);
        zoneConfig.enemyFleetsPerCell    = EditorGUILayout.IntField(new GUIContent("Fleets Per Cell",     "셀당 순차 웨이브 개수"),               zoneConfig.enemyFleetsPerCell);
        zoneConfig.enemyBudget           = EditorGUILayout.IntField(new GUIContent("Enemy Budget",        "웨이브 1개의 지휘력 예산"),             zoneConfig.enemyBudget);
        zoneConfig.enemyMaxCost          = EditorGUILayout.IntField(new GUIContent("Enemy Max Cost",      "웨이브에 편성 가능한 함선 1척의 commandCost 상한"), zoneConfig.enemyMaxCost);
        zoneConfig.enemyDeviation        = EditorGUILayout.IntField(new GUIContent("Enemy Deviation",     "Enemy Max Cost 랜덤 편차"),           zoneConfig.enemyDeviation);
        zoneConfig.enemyMaxShipsPerFleet = EditorGUILayout.IntField(new GUIContent("Max Ships Per Fleet",  "웨이브 1개의 함선 수 상한"),            zoneConfig.enemyMaxShipsPerFleet);
        EditorGUI.indentLevel--;

        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(m_dataTableZone);
    }

}
#endif
