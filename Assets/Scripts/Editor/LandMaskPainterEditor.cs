using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LandMaskPainter))]
public class LandMaskPainterEditor : Editor
{
    // LandMaskPainter.BiomeRanges에서 파생 — static 생성자에서 1회 계산
    static readonly int[]    _biomeCounts;   // 각 바이옴 바이트 칸 수
    static readonly int[]    _biomeBounds;   // 히스토그램 경계선 (바이옴 시작 byte, 첫 제외)
    static readonly string[] _biomeLabels;   // 컬러바 레이블 "이름\nmin-max"
    static readonly string[] _biomeRangeStr; // 슬라이더 옆 범위 문자열 "min-max"

    static LandMaskPainterEditor()
    {
        var r = LandMaskPainter.BiomeRanges;
        var n = LandMaskPainter.BiomeNames;
        int len = r.Length;
        _biomeCounts   = new int[len];
        _biomeBounds   = new int[len - 1];
        _biomeLabels   = new string[len];
        _biomeRangeStr = new string[len];
        for (int i = 0; i < len; i++)
        {
            _biomeCounts[i]   = r[i].max - r[i].min + 1;
            _biomeLabels[i]   = $"{n[i]}\n{r[i].min}-{r[i].max}";
            _biomeRangeStr[i] = $"{r[i].min}-{r[i].max}";
            if (i > 0) _biomeBounds[i - 1] = r[i].min;
        }
    }

    // 5존 대표색: 심해/얕은바다/저지대(모래)/평야(잔디)/고원(침엽수)
    static Color[] GetBiomeColors(CelestialBodyConfig cfg) => new[]
    {
        cfg.deepSeaColor,       // 심해
        cfg.shallowSeaColor,    // 얕은바다
        cfg.lowlandSandColor,   // 저지대
        cfg.plainsGrassColor,   // 평야
        cfg.highlandSnowColor,  // 고원
    };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var t = (LandMaskPainter)target;
        var colors = GetBiomeColors(t.previewBodyConfig ?? new CelestialBodyConfig());

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("── 생성 ──────────────────────────────────", EditorStyles.boldLabel);
        DrawBiomeRatioSection(t, colors);
        EditorGUILayout.Space(4);
        DrawRNoiseSection(t);
        EditorGUILayout.Space(4);
        DrawGNoiseSection(t);
        EditorGUILayout.Space(4);
        DrawGVariantSection(t);
        EditorGUILayout.Space(8);

        DrawColorBar(_biomeCounts, colors, _biomeLabels, 38, 7);

        if (t.hasHistogram == true) { EditorGUILayout.Space(4); DrawHistogram(t.histogram); }

        if (t.previewTex != null)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("미리보기 (컬러)", EditorStyles.boldLabel);
            EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetRect(0, 160, GUILayout.ExpandWidth(true)),
                                         t.previewTex, null, ScaleMode.ScaleToFit);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("생성", GUILayout.Height(28))) Generate(t);
        if (GUILayout.Button("저장", GUILayout.Height(28))) Save(t);
        EditorGUILayout.EndHorizontal();

        if (string.IsNullOrEmpty(t.statusMsg) == false)
            EditorGUILayout.HelpBox(t.statusMsg, MessageType.None);
    }

    void DrawBiomeRatioSection(LandMaskPainter t, Color[] colors)
    {
        EditorGUILayout.LabelField("바이옴 비율", EditorStyles.boldLabel);
        // 순서: 심해/얕은바다/저지대/평야/고원 — LandMaskPainter.BiomeRanges 순서와 동일
        int[] pcts = { t.pctDeepSea, t.pctShallowSea, t.pctLowland, t.pctPlains, t.pctHighland };

        EditorGUI.BeginChangeCheck();
        for (int i = 0; i < 5; i++)
        {
            EditorGUILayout.BeginHorizontal();
            Rect cr = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14)); cr.y += 2;
            EditorGUI.DrawRect(cr, colors[i]);
            EditorGUILayout.LabelField($"{LandMaskPainter.BiomeNames[i]}  {_biomeRangeStr[i]}", GUILayout.Width(135));
            pcts[i] = EditorGUILayout.IntSlider(pcts[i], 0, 100);
            EditorGUILayout.LabelField($"{pcts[i]}%", GUILayout.Width(30));
            EditorGUILayout.EndHorizontal();
        }
        if (EditorGUI.EndChangeCheck() == true)
        {
            Undo.RecordObject(t, "Biome Ratio");
            t.pctDeepSea = pcts[0]; t.pctShallowSea = pcts[1]; t.pctLowland = pcts[2];
            t.pctPlains  = pcts[3]; t.pctHighland   = pcts[4];
            EditorUtility.SetDirty(t);
        }

        int total = 0; foreach (int p in pcts) total += p;
        bool isOk = total == 100;
        EditorGUILayout.HelpBox($"합계: {total}%  {(isOk ? "✓" : "⚠ 100%가 아님 — 자동 보정됩니다")}",
                                 isOk ? MessageType.Info : MessageType.Warning);
        DrawRatioBar(pcts, total, colors);
    }

    void DrawRatioBar(int[] pcts, int total, Color[] colors)
    {
        if (total <= 0) return;
        Rect bar = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
        var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { normal = { textColor = Color.white } };
        float x = bar.x;
        for (int i = 0; i < 5; i++)
        {
            float w = (float)pcts[i] / total * bar.width;
            if (w < 1f) { x += w; continue; }
            EditorGUI.DrawRect(new Rect(x, bar.y, w - 1, bar.height), colors[i]);
            if (w > 22f) GUI.Label(new Rect(x, bar.y + 2, w, bar.height), $"{pcts[i]}%", style);
            x += w;
        }
    }

    void DrawRNoiseSection(LandMaskPainter t)
    {
        EditorGUILayout.LabelField("R채널 — 고도 노이즈", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        int   rSeed        = EditorGUILayout.IntField("Seed",                    t.rSeed);
        int   rNoiseScale  = EditorGUILayout.IntSlider("Scale (정수=이음새 없음)", t.rNoiseScale,    1,   64);
        int   rOctaves     = EditorGUILayout.IntSlider("Octaves",                t.rOctaves,       1,    8);
        float rPersistence = EditorGUILayout.Slider("Persistence",               t.rPersistence, 0.1f,  1f);
        float rLacunarity  = EditorGUILayout.Slider("Lacunarity",                t.rLacunarity,   1f,   4f);
        bool  rRidged      = EditorGUILayout.Toggle("Ridged (섬/군도 분리)",      t.rRidged);
        float poleOceanWidth = EditorGUILayout.Slider("극지방 바다 폭 (0=없음)",  t.poleOceanWidth, 0f, 0.30f);
        if (EditorGUI.EndChangeCheck() == true)
        {
            Undo.RecordObject(t, "R Noise Settings");
            t.rSeed = rSeed; t.rNoiseScale = rNoiseScale; t.rOctaves = rOctaves;
            t.rPersistence = rPersistence; t.rLacunarity = rLacunarity;
            t.rRidged = rRidged; t.poleOceanWidth = poleOceanWidth;
            EditorUtility.SetDirty(t);
        }
    }

    void DrawGNoiseSection(LandMaskPainter t)
    {
        EditorGUILayout.LabelField("G채널 — 바이옴 변이 노이즈", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        int   gSeed        = EditorGUILayout.IntField("Seed",        t.gSeed);
        int   gScale       = EditorGUILayout.IntSlider("Scale",      t.gScale,       1,   64);
        int   gOctaves     = EditorGUILayout.IntSlider("Octaves",    t.gOctaves,     1,    8);
        float gPersistence = EditorGUILayout.Slider("Persistence",   t.gPersistence, 0.1f, 1f);
        float gLacunarity  = EditorGUILayout.Slider("Lacunarity",    t.gLacunarity,  1f,   4f);
        if (EditorGUI.EndChangeCheck() == true)
        {
            Undo.RecordObject(t, "G Noise Settings");
            t.gSeed = gSeed; t.gScale = gScale; t.gOctaves = gOctaves;
            t.gPersistence = gPersistence; t.gLacunarity = gLacunarity;
            EditorUtility.SetDirty(t);
        }
    }

    void DrawGVariantSection(LandMaskPainter t)
    {
        var cfg = t.previewBodyConfig ?? new CelestialBodyConfig();
        EditorGUILayout.LabelField("G채널 — 존별 변이 비율", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("저지대", EditorStyles.miniLabel);
        int gLS = DrawVariantRow("Sand",  cfg.lowlandSandColor,  t.gLowlandSand);
        int gLG = DrawVariantRow("Green", cfg.lowlandGreenColor, t.gLowlandGreen);
        int gLL = DrawVariantRow("Lake",  cfg.shallowSeaColor,   t.gLowlandLake);
        DrawVariantSumHint(gLS + gLG + gLL);
        DrawVariantRatioBar(new[]{ gLS, gLG, gLL },
                            new[]{ cfg.lowlandSandColor, cfg.lowlandGreenColor, cfg.shallowSeaColor });

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("평야", EditorStyles.miniLabel);
        int gPD = DrawVariantRow("Desert", cfg.plainsDesertColor, t.gPlainsDesert);
        int gPG = DrawVariantRow("Grass",  cfg.plainsGrassColor,  t.gPlainsGrass);
        int gPF = DrawVariantRow("Forest", cfg.plainsForestColor, t.gPlainsForest);
        DrawVariantSumHint(gPD + gPG + gPF);
        DrawVariantRatioBar(new[]{ gPD, gPG, gPF },
                            new[]{ cfg.plainsDesertColor, cfg.plainsGrassColor, cfg.plainsForestColor });

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("고원", EditorStyles.miniLabel);
        int gHS = DrawVariantRow("Snow", cfg.highlandSnowColor, t.gHighlandSnow);
        DrawVariantSumHint(gHS);
        DrawVariantRatioBar(new[]{ gHS },
                            new[]{ cfg.highlandSnowColor });

        if (EditorGUI.EndChangeCheck() == true)
        {
            Undo.RecordObject(t, "G Variant Ratios");
            t.gLowlandSand = gLS; t.gLowlandGreen = gLG; t.gLowlandLake = gLL;
            t.gPlainsDesert = gPD; t.gPlainsGrass = gPG; t.gPlainsForest = gPF;
            t.gHighlandSnow = gHS;
            EditorUtility.SetDirty(t);
        }
    }

    static int DrawVariantRow(string label, Color col, int val)
    {
        EditorGUILayout.BeginHorizontal();
        Rect cr = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12)); cr.y += 2;
        EditorGUI.DrawRect(cr, col);
        int result = EditorGUILayout.IntSlider(label, val, 0, 100);
        EditorGUILayout.LabelField($"{val}%", GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();
        return result;
    }

    static void DrawVariantSumHint(int sum)
    {
        bool ok = sum == 100;
        EditorGUILayout.HelpBox($"합계: {sum}%  {(ok ? "✓" : "⚠ 100%가 아님")}",
                                 ok ? MessageType.Info : MessageType.Warning);
    }

    static void DrawVariantRatioBar(int[] pcts, Color[] colors)
    {
        int total = 0; foreach (int p in pcts) total += p;
        if (total <= 0) return;
        Rect bar = GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true));
        float x = bar.x;
        for (int i = 0; i < pcts.Length; i++)
        {
            float w = (float)pcts[i] / total * bar.width;
            if (w < 1f) { x += w; continue; }
            EditorGUI.DrawRect(new Rect(x, bar.y, w - 1, bar.height), colors[i]);
            x += w;
        }
    }

    static void DrawColorBar(int[] widths, Color[] colors, string[] labels, int height, int fontSize)
    {
        Rect bar = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
        float total = 0; foreach (int w in widths) total += w;
        var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = fontSize, normal = { textColor = Color.white } };
        float x = bar.x;
        for (int i = 0; i < widths.Length; i++)
        {
            float w = widths[i] / total * bar.width;
            EditorGUI.DrawRect(new Rect(x, bar.y, w - 1, bar.height), colors[i]);
            if (labels != null) GUI.Label(new Rect(x, bar.y + 2, w, bar.height), labels[i], style);
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
            float h = hist[i] / (float)maxVal * rect.height;
            EditorGUI.DrawRect(new Rect(rect.x + i * bw, rect.y + rect.height - h, bw, h), new Color(0.75f, 0.75f, 0.75f));
        }
        foreach (int b in _biomeBounds)
            EditorGUI.DrawRect(new Rect(rect.x + b / 255f * rect.width, rect.y, 1, rect.height), new Color(1f, 0.3f, 0.3f, 0.9f));
    }

    // ── 노이즈 (수평 타일링 Value Noise) ──────────────────────────────────────

    static float Hash2D(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 1013904223 + seed;
            h = (h ^ (h >> 13)) * 1540483477;
            return ((h ^ (h >> 15)) & 0x7FFFFFFF) / 2147483647f;
        }
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    static float TileNoiseX(float x, float y, int periodX, int seed)
    {
        int   x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        int   x1 = x0 + 1,              y1 = y0 + 1;
        float fx = Smooth(x - x0),      fy = Smooth(y - y0);
        x0 = ((x0 % periodX) + periodX) % periodX;
        x1 = ((x1 % periodX) + periodX) % periodX;
        return Mathf.Lerp(Mathf.Lerp(Hash2D(x0, y0, seed), Hash2D(x1, y0, seed), fx),
                          Mathf.Lerp(Hash2D(x0, y1, seed), Hash2D(x1, y1, seed), fx), fy);
    }

    static float SampleFBM(float u, float v, int seed, int noiseScale, int octaves, float persistence, float lacunarity, bool ridged)
    {
        float amp = 1f, freq = 1f, val = 0f, maxV = 0f;
        for (int oct = 0; oct < octaves; oct++)
        {
            int   periodX = Mathf.Max(1, Mathf.RoundToInt(noiseScale * freq));
            float n       = TileNoiseX(u * periodX, v * noiseScale * 0.5f * freq, periodX, seed + oct * 1000);
            if (ridged == true) n = 1f - Mathf.Abs(n * 2f - 1f);
            val  += n * amp;
            maxV += amp;
            amp  *= persistence;
            freq *= lacunarity;
        }
        return val / maxV;
    }

    // ── 처리 로직 ─────────────────────────────────────────────────────────────

    void BuildHistogram(LandMaskPainter t, Color32[] pixels)
    {
        var hist = new int[256];
        foreach (var p in pixels) hist[p.r]++;
        t.histogram = hist; t.hasHistogram = true;
    }

    void UpdatePreview(LandMaskPainter t, int w, int h, Color32[] pixels)
    {
        if (t.previewTex == null)
            t.previewTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        else if (t.previewTex.width != w || t.previewTex.height != h)
            t.previewTex.Reinitialize(w, h);
        t.previewTex.SetPixels32(pixels);
        t.previewTex.Apply();
    }

    void UpdateRawTex(LandMaskPainter t, int w, int h, Color32[] pixels)
    {
        if (t.rawTex == null)
            t.rawTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        else if (t.rawTex.width != w || t.rawTex.height != h)
            t.rawTex.Reinitialize(w, h);
        t.rawTex.SetPixels32(pixels);
        t.rawTex.Apply();
    }

    // 바이옴(zone + G값) → 컬러 (미리보기용)
    static Color32 BiomeToColor(int zone, float gVal, CelestialBodyConfig cfg)
    {
        Color c;
        if (zone == 0)
        {
            c = cfg.deepSeaColor;
        }
        else if (zone == 1)
        {
            c = cfg.shallowSeaColor;
        }
        else if (zone == 2) // 저지대
        {
            c = cfg.lowlandSandColor;
            if (gVal > 0.28f) c = Color.Lerp(c, cfg.lowlandGreenColor, Mathf.InverseLerp(0.28f, 0.32f, gVal));
            if (gVal > 0.63f) c = Color.Lerp(c, cfg.shallowSeaColor, Mathf.InverseLerp(0.63f, 0.70f, gVal));
        }
        else if (zone == 3) // 평야
        {
            c = cfg.plainsDesertColor;
            if (gVal > 0.23f) c = Color.Lerp(c, cfg.plainsGrassColor,  Mathf.InverseLerp(0.23f, 0.27f, gVal));
            if (gVal > 0.48f) c = Color.Lerp(c, cfg.plainsForestColor, Mathf.InverseLerp(0.48f, 0.52f, gVal));
        }
        else // 고원
        {
            c = cfg.highlandSnowColor;
        }
        return new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255);
    }

    void Generate(LandMaskPainter t)
    {
        const int W = 1024, H = 512;

        // ── R채널: 고도 노이즈 ────────────────────────────────────────────────
        float[] rNoise = new float[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float u  = (float)x / W, v = (float)y / H;
            float ps = Mathf.Clamp01(Mathf.Min(v, 1f - v) / Mathf.Max(t.poleOceanWidth, 0.001f));
            float poleFade = ps * ps * (3f - 2f * ps);
            rNoise[y * W + x] = SampleFBM(u, v, t.rSeed, t.rNoiseScale, t.rOctaves, t.rPersistence, t.rLacunarity, t.rRidged) * poleFade;
        }

        // 0~1 정규화
        float rMin = float.MaxValue, rMax = float.MinValue;
        for (int i = 0; i < rNoise.Length; i++) { if (rNoise[i] < rMin) rMin = rNoise[i]; if (rNoise[i] > rMax) rMax = rNoise[i]; }
        float rRange = Mathf.Max(rMax - rMin, 0.0001f);
        for (int i = 0; i < rNoise.Length; i++) rNoise[i] = (rNoise[i] - rMin) / rRange;

        // 분위수 → 바이트 범위 할당
        float[] rSorted = (float[])rNoise.Clone();
        System.Array.Sort(rSorted);

        int[] pcts = { t.pctDeepSea, t.pctShallowSea, t.pctLowland, t.pctPlains, t.pctHighland };
        int totalPct = 0; foreach (int p in pcts) totalPct += p;
        if (totalPct <= 0) { t.statusMsg = "비율 합계가 0"; return; }

        float[] thresh = new float[5]; float cumPct = 0;
        for (int b = 0; b < 5; b++)
        {
            cumPct += pcts[b];
            thresh[b] = rSorted[Mathf.Clamp(Mathf.RoundToInt(cumPct / totalPct * (W * H - 1)), 0, W * H - 1)];
        }

        // ── G채널: 변이 노이즈 (독립 FBM) ────────────────────────────────────
        float[] gNoise = new float[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float u = (float)x / W, v = (float)y / H;
            gNoise[y * W + x] = SampleFBM(u, v, t.gSeed, t.gScale, t.gOctaves, t.gPersistence, t.gLacunarity, false);
        }

        float gMin = float.MaxValue, gMax = float.MinValue;
        for (int i = 0; i < gNoise.Length; i++) { if (gNoise[i] < gMin) gMin = gNoise[i]; if (gNoise[i] > gMax) gMax = gNoise[i]; }
        float gRange = Mathf.Max(gMax - gMin, 0.0001f);
        for (int i = 0; i < gNoise.Length; i++) gNoise[i] = (gNoise[i] - gMin) / gRange;

        // ── 픽셀 생성 — R바이트 + 존 결정 ────────────────────────────────────
        var ranges  = LandMaskPainter.BiomeRanges;
        var cfg     = t.previewBodyConfig ?? new CelestialBodyConfig();
        var pixelZone = new int[W * H];
        var rBytes    = new byte[W * H];

        for (int i = 0; i < rNoise.Length; i++)
        {
            float rv = rNoise[i];
            int zone = 4;
            for (int b = 0; b < 5; b++) { if (rv <= thresh[b]) { zone = b; break; } }
            pixelZone[i] = zone;

            float lo  = zone > 0 ? thresh[zone - 1] : rSorted[0], hi = thresh[zone];
            float t01 = hi > lo ? Mathf.Clamp01((rv - lo) / (hi - lo)) : 0.5f;
            rBytes[i] = (byte)Mathf.RoundToInt(Mathf.Lerp(ranges[zone].min, ranges[zone].max, t01));
        }

        // ── G바이트 — 존별 분위수 리매핑 ────────────────────────────────────
        // 셰이더 임계값 0.25/0.5/0.75 (4분할), 0.5 (2분할) 기준
        // 비율 슬라이더 → 각 변이가 차지할 픽셀 수 → G바이트 범위 배정
        int[][] gPcts = new int[5][];
        gPcts[0] = new[]{ 100 };                                                                // 심해 (G 미사용)
        gPcts[1] = new[]{ 100 };                                                                // 얕은바다 (G 미사용)
        gPcts[2] = new[]{ t.gLowlandSand,  t.gLowlandGreen,  t.gLowlandLake };
        gPcts[3] = new[]{ t.gPlainsDesert, t.gPlainsGrass,   t.gPlainsForest };
        gPcts[4] = new[]{ t.gHighlandSnow };

        var gBytes = RemapGPerZone(gNoise, pixelZone, gPcts, W * H);

        // ── 최종 픽셀 배열 ────────────────────────────────────────────────────
        var rawPx  = new Color32[W * H];
        var prevPx = new Color32[W * H];
        for (int i = 0; i < W * H; i++)
        {
            rawPx[i]  = new Color32(rBytes[i], gBytes[i], 0, 255);
            prevPx[i] = BiomeToColor(pixelZone[i], gBytes[i] / 255f, cfg);
        }

        BuildHistogram(t, rawPx);
        UpdateRawTex(t, W, H, rawPx);
        UpdatePreview(t, W, H, prevPx);
        t.statusMsg = $"생성 완료 — {W}x{H}  합계:{totalPct}%  rSeed:{t.rSeed}  gSeed:{t.gSeed}";
        EditorUtility.SetDirty(t); Repaint();
    }

    // 존별 G 분위수 리매핑 — 각 존의 픽셀을 G값 기준 정렬 후 비율대로 바이트 범위 배정
    // nVariants에 따라 바이트 구간 균등 분할 (2분할: 0~127/128~255, 4분할: 0~63/64~127/128~191/192~255)
    static byte[] RemapGPerZone(float[] gNoise, int[] pixelZone, int[][] gPcts, int pixelCount)
    {
        var gBytes = new byte[pixelCount];
        int zoneCount = gPcts.Length;

        for (int z = 0; z < zoneCount; z++)
        {
            int[] pcts = gPcts[z];
            int nVariants = pcts.Length;

            var list = new System.Collections.Generic.List<(float g, int idx)>();
            for (int i = 0; i < pixelCount; i++)
                if (pixelZone[i] == z) list.Add((gNoise[i], i));
            if (list.Count == 0) continue;

            list.Sort((a, b) => a.g.CompareTo(b.g));

            int total = 0; foreach (int p in pcts) total += p;
            if (total <= 0) total = 1;

            int[] cumCount = new int[nVariants];
            float cumPct = 0;
            for (int v = 0; v < nVariants; v++)
            {
                cumPct += pcts[v];
                cumCount[v] = Mathf.RoundToInt(cumPct / total * list.Count);
            }
            cumCount[nVariants - 1] = list.Count;

            int prev = 0;
            for (int v = 0; v < nVariants; v++)
            {
                int end   = cumCount[v];
                byte bMin = (byte)(v * 256 / nVariants);
                byte bMax = (byte)(Mathf.Min((v + 1) * 256 / nVariants, 256) - 1);
                for (int i = prev; i < end; i++)
                {
                    float t01 = (end > prev + 1) ? (float)(i - prev) / (end - prev - 1) : 0f;
                    gBytes[list[i].idx] = (byte)Mathf.RoundToInt(Mathf.Lerp(bMin, bMax, t01));
                }
                prev = end;
            }
        }
        return gBytes;
    }

    void Save(LandMaskPainter t)
    {
        if (t.rawTex == null) { t.statusMsg = "저장할 RG 텍스처 없음 (먼저 생성 실행)"; return; }
        string path = t.sourceTex != null
            ? AssetDatabase.GetAssetPath(t.sourceTex)
            : EditorUtility.SaveFilePanelInProject("Land Mask 저장", "planet_land_mask_new", "png", "저장할 위치 선택");
        if (string.IsNullOrEmpty(path) == true) { t.statusMsg = "저장 취소"; return; }
        File.WriteAllBytes(Path.GetFullPath(path), t.rawTex.EncodeToPNG());
        AssetDatabase.Refresh();
        t.statusMsg = $"저장 완료 → {path}";
        EditorUtility.SetDirty(t);
    }
}
