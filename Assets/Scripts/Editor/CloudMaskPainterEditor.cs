using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CloudMaskPainter))]
public class CloudMaskPainterEditor : Editor
{
    struct VortexCenter { public float cx, cy, dir; }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var t = (CloudMaskPainter)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("── 생성 ──────────────────────────────────", EditorStyles.boldLabel);

        DrawNoiseSection(t);
        EditorGUILayout.Space(4);
        DrawVortexSection(t);
        EditorGUILayout.Space(4);
        DrawLatitudeSection(t);
        EditorGUILayout.Space(4);
        DrawPreviewSection(t);
        EditorGUILayout.Space(8);

        if (t.hasHistogram == true) { EditorGUILayout.Space(4); DrawHistogram(t.histogram); }

        if (t.previewTex != null)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("미리보기 (파란 하늘 + 흰 구름)", EditorStyles.boldLabel);
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

    void DrawNoiseSection(CloudMaskPainter t)
    {
        EditorGUILayout.LabelField("노이즈 설정", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        int   seed        = EditorGUILayout.IntField("Seed",                        t.seed);
        int   noiseScale  = EditorGUILayout.IntSlider("Scale (정수=이음새 없음)",   t.noiseScale,    1, 64);
        int   octaves     = EditorGUILayout.IntSlider("Octaves",                    t.octaves,       1,  8);
        float persistence = EditorGUILayout.Slider("Persistence",                   t.persistence, 0.1f, 1f);
        float lacunarity  = EditorGUILayout.Slider("Lacunarity",                    t.lacunarity,   1f,  4f);
        bool  ridged      = EditorGUILayout.Toggle("Ridged (능선형, 회오리 선명도 ↑)", t.ridged);
        if (EditorGUI.EndChangeCheck() == true)
        {
            Undo.RecordObject(t, "Cloud Noise Settings");
            t.seed = seed; t.noiseScale = noiseScale; t.octaves = octaves;
            t.persistence = persistence; t.lacunarity = lacunarity; t.ridged = ridged;
            EditorUtility.SetDirty(t);
        }
    }

    void DrawVortexSection(CloudMaskPainter t)
    {
        EditorGUILayout.LabelField("회오리 (Vortex Domain Warp)", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        bool  useVortex      = EditorGUILayout.Toggle("사용",                     t.useVortex);
        int   vortexCount    = EditorGUILayout.IntSlider("개수",                  t.vortexCount,    1,  8);
        float vortexStrength = EditorGUILayout.Slider("회전 강도 (rad)",          t.vortexStrength, 0.5f, 6f);
        float vortexRadius   = EditorGUILayout.Slider("영향 반경 (UV)",           t.vortexRadius,   0.1f, 0.6f);
        int   vortexSeed     = EditorGUILayout.IntField("배치 Seed",              t.vortexSeed);
        bool  vortexEye      = EditorGUILayout.Toggle("허리케인 눈 (중심 맑음)",  t.vortexEye);
        float vortexEyeSize  = EditorGUILayout.Slider("  눈 반경 (UV)",           t.vortexEyeSize,  0.01f, 0.2f);
        if (EditorGUI.EndChangeCheck() == true)
        {
            Undo.RecordObject(t, "Cloud Vortex Settings");
            t.useVortex      = useVortex;
            t.vortexCount    = vortexCount;
            t.vortexStrength = vortexStrength;
            t.vortexRadius   = vortexRadius;
            t.vortexSeed     = vortexSeed;
            t.vortexEye      = vortexEye;
            t.vortexEyeSize  = vortexEyeSize;
            EditorUtility.SetDirty(t);
        }
    }

    void DrawLatitudeSection(CloudMaskPainter t)
    {
        EditorGUILayout.LabelField("위도 조절", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        bool  usePolarBoost      = EditorGUILayout.Toggle("극지방 구름 강화",  t.usePolarBoost);
        float polarBoostWidth    = EditorGUILayout.Slider("  극지방 폭",        t.polarBoostWidth,    0f, 0.4f);
        float polarBoostAmount   = EditorGUILayout.Slider("  강화량",           t.polarBoostAmount,   0f, 3.0f);
        bool  useEquatorClear    = EditorGUILayout.Toggle("적도 구름 감소",  t.useEquatorClear);
        float equatorClearWidth  = EditorGUILayout.Slider("  적도 감소 폭",  t.equatorClearWidth,  0f, 0.3f);
        float equatorClearAmount = EditorGUILayout.Slider("  감소량",        t.equatorClearAmount, 0f, 0.5f);
        if (EditorGUI.EndChangeCheck() == true)
        {
            Undo.RecordObject(t, "Cloud Latitude Settings");
            t.usePolarBoost      = usePolarBoost;
            t.polarBoostWidth    = polarBoostWidth;
            t.polarBoostAmount   = polarBoostAmount;
            t.useEquatorClear    = useEquatorClear;
            t.equatorClearWidth  = equatorClearWidth;
            t.equatorClearAmount = equatorClearAmount;
            EditorUtility.SetDirty(t);
        }
    }

    void DrawPreviewSection(CloudMaskPainter t)
    {
        EditorGUI.BeginChangeCheck();
        if (EditorGUI.EndChangeCheck() == true)
        {
            Undo.RecordObject(t, "Cloud Preview Coverage");
            EditorUtility.SetDirty(t);
            if (t.rawTex != null) RebuildPreview(t);
        }
    }

    // ── 노이즈 (LandMaskPainterEditor와 동일 — 수평 타일링 Value Noise) ────────

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

    // ── 회오리 (Domain Warp) ──────────────────────────────────────────────────

    static VortexCenter[] GenerateVortices(int count, int seed)
    {
        var result = new VortexCenter[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = new VortexCenter
            {
                cx  = Hash2D(i, 7,  seed),
                cy  = 0.15f + Hash2D(i, 13, seed) * 0.70f,  // 극지 회피 (0.15~0.85)
                dir = Hash2D(i, 19, seed) > 0.5f ? 1f : -1f, // 시계/반시계
            };
        }
        return result;
    }

    // UV 좌표를 각 회오리 중심 기준으로 회전 왜곡 (원본 UV 유지)
    static void WarpUV(ref float u, ref float v, VortexCenter[] vortices, float strength, float radius)
    {
        float du = 0f, dv = 0f;
        for (int k = 0; k < vortices.Length; k++)
        {
            float dx = u - vortices[k].cx;
            if (dx > 0.5f) dx -= 1f;    // 수평 최단거리 (타일링)
            if (dx < -0.5f) dx += 1f;
            float dy = v - vortices[k].cy;
            float r  = Mathf.Sqrt(dx * dx + dy * dy);
            if (r < 0.0001f) continue;

            // 거리 기반 회전량 — 중심에서 최대, 반경 밖에서 0
            float t     = Mathf.Clamp01(1f - r / radius);
            t           = t * t * (3f - 2f * t);
            float angle = vortices[k].dir * strength * t;
            float cosA  = Mathf.Cos(angle), sinA = Mathf.Sin(angle);
            float wdx   = cosA * dx - sinA * dy;
            float wdy   = sinA * dx + cosA * dy;
            du += wdx - dx;
            dv += wdy - dy;
        }
        u = ((u + du) % 1f + 1f) % 1f;
        v = Mathf.Clamp01(v + dv);
    }

    // 회오리 중심 주변의 눈(eye) 감쇠값 반환 (0~1)
    static float ComputeEyeReduction(float u, float v, VortexCenter[] vortices, float eyeSize)
    {
        float maxReduction = 0f;
        for (int k = 0; k < vortices.Length; k++)
        {
            float dx = u - vortices[k].cx;
            if (dx > 0.5f) dx -= 1f;
            if (dx < -0.5f) dx += 1f;
            float dy = v - vortices[k].cy;
            float r  = Mathf.Sqrt(dx * dx + dy * dy);
            float t  = Mathf.Clamp01(1f - r / eyeSize);
            t        = t * t * (3f - 2f * t);
            if (t > maxReduction) maxReduction = t;
        }
        return maxReduction;
    }

    // ── 처리 ──────────────────────────────────────────────────────────────────

    void Generate(CloudMaskPainter t)
    {
        const int W = 1024, H = 512;

        // 회오리 중심 생성
        VortexCenter[] vortices = (t.useVortex && t.vortexCount > 0)
            ? GenerateVortices(t.vortexCount, t.vortexSeed)
            : new VortexCenter[0];

        // FBM 샘플링 — 회오리 warp 적용 후 노이즈 조회
        float[] noise = new float[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float u = (float)x / W, v = (float)y / H;
            if (vortices.Length > 0)
                WarpUV(ref u, ref v, vortices, t.vortexStrength, t.vortexRadius);
            noise[y * W + x] = SampleFBM(u, v, t.seed, t.noiseScale, t.octaves, t.persistence, t.lacunarity, t.ridged);
        }

        // 1차 min-max 정규화 (eye·위도 보정을 비례 스케일로 적용하기 위한 rough 0~1)
        float nMin = float.MaxValue, nMax = float.MinValue;
        for (int i = 0; i < noise.Length; i++) { if (noise[i] < nMin) nMin = noise[i]; if (noise[i] > nMax) nMax = noise[i]; }
        float nRange = Mathf.Max(nMax - nMin, 0.0001f);
        for (int i = 0; i < noise.Length; i++) noise[i] = (noise[i] - nMin) / nRange;

        // 허리케인 눈 — 원본 UV 기준으로 중심부 구름 제거
        if (t.vortexEye && vortices.Length > 0)
        {
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float origU = (float)x / W, origV = (float)y / H;
                float eye   = ComputeEyeReduction(origU, origV, vortices, t.vortexEyeSize);
                noise[y * W + x] = Mathf.Clamp01(noise[y * W + x] - eye);
            }
        }

        // 분위수(rank) 정규화 — ridged FBM 고값 편향 제거, coverage 0.5 = 정확히 상위 50%
        // 위도 boost보다 먼저 실행해야 극지방 boost가 rank에 의해 초기화되지 않음
        {
            var indices = new int[W * H];
            for (int i = 0; i < W * H; i++) indices[i] = i;
            System.Array.Sort(indices, (a, b) => noise[a].CompareTo(noise[b]));
            for (int rank = 0; rank < W * H; rank++)
                noise[indices[rank]] = rank / (float)(W * H - 1);
        }

        // 위도 조절 — rank 정규화 이후 적용: 극지방 boost가 직접 coverage threshold를 넘김
        for (int y = 0; y < H; y++)
        {
            float v     = (float)y / H;
            float boost = 0f;
            if (t.usePolarBoost == true)
            {
                float pp = Mathf.Clamp01(1f - Mathf.Min(v, 1f - v) / Mathf.Max(t.polarBoostWidth, 0.001f));
                pp    = pp * pp * (3f - 2f * pp);
                boost += pp * t.polarBoostAmount;
            }
            if (t.useEquatorClear == true)
            {
                float ep = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) / Mathf.Max(t.equatorClearWidth, 0.001f));
                ep    = ep * ep * (3f - 2f * ep);
                boost -= ep * t.equatorClearAmount;
            }
            if (boost == 0f) continue;
            for (int x = 0; x < W; x++)
                noise[y * W + x] = Mathf.Clamp01(noise[y * W + x] + boost);
        }

        // 히스토그램 + raw 픽셀 (R=구름밀도, A=255 고정 — 위도별 opacity는 셰이더에서 계산)
        var hist  = new int[256];
        var rawPx = new Color32[W * H];
        for (int i = 0; i < W * H; i++)
        {
            byte b   = (byte)Mathf.RoundToInt(noise[i] * 255f);
            rawPx[i] = new Color32(b, 0, 0, 255);
            hist[b]++;
        }
        t.histogram = hist; t.hasHistogram = true;

        if (t.rawTex == null)
            t.rawTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        else if (t.rawTex.width != W || t.rawTex.height != H)
            t.rawTex.Reinitialize(W, H);
        t.rawTex.SetPixels32(rawPx);
        t.rawTex.Apply();

        BuildPreview(t, noise, W, H);

        string vortexInfo = t.useVortex ? $"  vortex:{t.vortexCount}개 str:{t.vortexStrength:F1}" : "";
        t.statusMsg = $"생성 완료 — {W}x{H}  seed:{t.seed}  scale:{t.noiseScale}{vortexInfo}";
        EditorUtility.SetDirty(t);
        Repaint();
    }

    void RebuildPreview(CloudMaskPainter t)
    {
        int W = t.rawTex.width, H = t.rawTex.height;
        Color32[] raw   = t.rawTex.GetPixels32();
        float[]   noise = new float[W * H];
        for (int i = 0; i < raw.Length; i++) noise[i] = raw[i].r / 255f;
        BuildPreview(t, noise, W, H);
        EditorUtility.SetDirty(t);
        Repaint();
    }

    static void BuildPreview(CloudMaskPainter t, float[] noise, int W, int H)
    {
        var prevPx = new Color32[W * H];
        var sky    = new Color(0.15f, 0.30f, 0.70f);

        for (int i = 0; i < W * H; i++)
        {
            float tt    = noise[i];
            float alpha = tt * tt * (3f - 2f * tt);
            Color result = Color.Lerp(sky, Color.white, alpha);
            prevPx[i]    = new Color32((byte)(result.r * 255), (byte)(result.g * 255), (byte)(result.b * 255), 255);
        }

        if (t.previewTex == null)
            t.previewTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        else if (t.previewTex.width != W || t.previewTex.height != H)
            t.previewTex.Reinitialize(W, H);
        t.previewTex.SetPixels32(prevPx);
        t.previewTex.Apply();
    }

    void DrawHistogram(int[] hist)
    {
        EditorGUILayout.LabelField("히스토그램 (R채널)", EditorStyles.boldLabel);
        Rect rect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        int maxVal = 1;
        for (int i = 0; i < 256; i++) if (hist[i] > maxVal) maxVal = hist[i];
        float bw = rect.width / 256f;
        for (int i = 0; i < 256; i++)
        {
            float h = hist[i] / (float)maxVal * rect.height;
            EditorGUI.DrawRect(new Rect(rect.x + i * bw, rect.y + rect.height - h, bw, h), new Color(0.75f, 0.75f, 0.75f));
        }
    }

    void Save(CloudMaskPainter t)
    {
        if (t.rawTex == null) { t.statusMsg = "저장할 텍스처 없음 (먼저 생성 실행)"; return; }
        string path = t.sourceTex != null
            ? AssetDatabase.GetAssetPath(t.sourceTex)
            : EditorUtility.SaveFilePanelInProject("Cloud Mask 저장", "planet_cloud_mask_new", "png", "저장할 위치 선택");
        if (string.IsNullOrEmpty(path) == true) { t.statusMsg = "저장 취소"; return; }
        File.WriteAllBytes(Path.GetFullPath(path), t.rawTex.EncodeToPNG());
        AssetDatabase.Refresh();
        t.statusMsg = $"저장 완료 → {path}";
        EditorUtility.SetDirty(t);
    }
}
