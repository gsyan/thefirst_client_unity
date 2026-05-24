using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LandMaskPainter))]
public class LandMaskPainterEditor : Editor
{
    static readonly Color[] _biomeColors =
    {
        new Color(0.02f, 0.06f, 0.25f),  // 0-60   심해
        new Color(0.04f, 0.14f, 0.38f),  // 61-100 얕은바다
        new Color(0.45f, 0.40f, 0.25f),  // 101-127 해안
        new Color(0.28f, 0.55f, 0.18f),  // 128-170 평야
        new Color(0.08f, 0.28f, 0.08f),  // 171-200 숲
        new Color(0.80f, 0.65f, 0.30f),  // 201-220 사막
        new Color(0.55f, 0.48f, 0.38f),  // 221-255 고원
    };

    static readonly string[] _biomeLabels =
        { "심해\n0-60", "얕은바다\n61-100", "해안\n101-127", "평야\n128-170", "숲\n171-200", "사막\n201-220", "고원\n221-255" };

    // 각 구간 픽셀 수(합=256)
    static readonly int[] _biomeRanges = { 61, 40, 27, 43, 30, 20, 35 };

    // 바이옴 경계 (히스토그램 빨간선)
    static readonly int[] _biomeBounds = { 61, 101, 128, 171, 201, 221 };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var t = (LandMaskPainter)target;

        EditorGUILayout.Space(6);
        DrawBiomeBar();

        if (t.hasHistogram)
        {
            EditorGUILayout.Space(4);
            DrawHistogram(t.histogram);
        }

        if (t.previewTex != null)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);
            Rect r = GUILayoutUtility.GetRect(0, 160, GUILayout.ExpandWidth(true));
            EditorGUI.DrawPreviewTexture(r, t.previewTex, null, ScaleMode.ScaleToFit);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("분석", GUILayout.Height(28)))      Analyze(t);
        if (GUILayout.Button("미리보기", GUILayout.Height(28)))  Preview(t);
        if (GUILayout.Button("저장", GUILayout.Height(28), GUILayout.Width(70))) Save(t);
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(t.statusMsg))
            EditorGUILayout.HelpBox(t.statusMsg, MessageType.None);
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    void DrawBiomeBar()
    {
        Rect bar = GUILayoutUtility.GetRect(0, 38, GUILayout.ExpandWidth(true));
        float x = bar.x;
        for (int i = 0; i < 7; i++)
        {
            float w = (_biomeRanges[i] / 256f) * bar.width;
            EditorGUI.DrawRect(new Rect(x, bar.y, w - 1, bar.height), _biomeColors[i]);
            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                { fontSize = 7, normal = { textColor = Color.white } };
            GUI.Label(new Rect(x, bar.y + 2, w, bar.height), _biomeLabels[i], style);
            x += w;
        }
    }

    void DrawHistogram(int[] hist)
    {
        EditorGUILayout.LabelField("히스토그램", EditorStyles.boldLabel);
        Rect rect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));

        int maxVal = 1;
        for (int i = 0; i < 256; i++) if (hist[i] > maxVal) maxVal = hist[i];

        float bw = rect.width / 256f;
        for (int i = 0; i < 256; i++)
        {
            float h = (hist[i] / (float)maxVal) * rect.height;
            EditorGUI.DrawRect(new Rect(rect.x + i * bw, rect.y + rect.height - h, bw, h),
                               new Color(0.75f, 0.75f, 0.75f));
        }
        foreach (int b in _biomeBounds)
        {
            float bx = rect.x + (b / 255f) * rect.width;
            EditorGUI.DrawRect(new Rect(bx, rect.y, 1, rect.height), new Color(1f, 0.3f, 0.3f, 0.9f));
        }
    }

    // ── 처리 ──────────────────────────────────────────────────────────────────

    Texture2D LoadFromFile(LandMaskPainter t)
    {
        if (t.sourceTex == null) return null;
        string assetPath = AssetDatabase.GetAssetPath(t.sourceTex);
        if (string.IsNullOrEmpty(assetPath)) return null;

        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(assetPath));
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(bytes);
        return tex;
    }

    Color32[] Process(LandMaskPainter t, Color32[] src)
    {
        float range = Mathf.Max(t.inputMax - t.inputMin, 1);
        float g     = Mathf.Max(t.gamma, 0.01f);

        var result = new Color32[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            float v = src[i].r;
            v = (v - t.inputMin) / range * 255f;   // 정규화
            v += t.valueShift;                       // 이동
            v = Mathf.Pow(Mathf.Clamp01(v / 255f), g) * 255f;  // 감마
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);
            result[i] = new Color32(b, b, b, 255);
        }
        return result;
    }

    void BuildHistogram(LandMaskPainter t, Color32[] pixels)
    {
        var hist = new int[256];
        foreach (var p in pixels) hist[p.r]++;
        t.histogram    = hist;
        t.hasHistogram = true;
    }

    void Analyze(LandMaskPainter t)
    {
        var tex = LoadFromFile(t);
        if (tex == null) { t.statusMsg = "sourceTex 없음"; return; }

        var pixels = tex.GetPixels32();
        int min = 255, max = 0;
        foreach (var p in pixels)
        {
            if (p.r < min) min = p.r;
            if (p.r > max) max = p.r;
        }

        t.inputMin = min;
        t.inputMax = max;
        BuildHistogram(t, pixels);
        t.statusMsg = $"분석 완료 — min:{min}  max:{max}  픽셀수:{pixels.Length:N0}";
        DestroyImmediate(tex);
        EditorUtility.SetDirty(t);
    }

    void Preview(LandMaskPainter t)
    {
        var tex = LoadFromFile(t);
        if (tex == null) { t.statusMsg = "sourceTex 없음"; return; }

        var pixels    = tex.GetPixels32();
        BuildHistogram(t, pixels);
        var processed = Process(t, pixels);

        if (t.previewTex == null)
            t.previewTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        else if (t.previewTex.width != tex.width)
            t.previewTex.Reinitialize(tex.width, tex.height);

        t.previewTex.SetPixels32(processed);
        t.previewTex.Apply();
        t.statusMsg = $"미리보기 완료 — {tex.width}x{tex.height}";

        DestroyImmediate(tex);
        EditorUtility.SetDirty(t);
        Repaint();
    }

    void Save(LandMaskPainter t)
    {
        var tex = LoadFromFile(t);
        if (tex == null) { t.statusMsg = "sourceTex 없음"; return; }

        string assetPath = AssetDatabase.GetAssetPath(t.sourceTex);
        var processed    = Process(t, tex.GetPixels32());

        var outTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        outTex.SetPixels32(processed);
        outTex.Apply();

        File.WriteAllBytes(Path.GetFullPath(assetPath), outTex.EncodeToPNG());
        AssetDatabase.Refresh();
        t.statusMsg = $"저장 완료 → {assetPath}";

        DestroyImmediate(tex);
        DestroyImmediate(outTex);
        EditorUtility.SetDirty(t);
    }
}
