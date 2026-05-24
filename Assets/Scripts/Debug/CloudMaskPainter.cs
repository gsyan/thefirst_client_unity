using UnityEngine;

public class CloudMaskPainter : MonoBehaviour
{
    public Texture2D sourceTex;

    // 노이즈
    [HideInInspector] public int   seed        = 77777;
    [HideInInspector] public int   noiseScale  = 6;
    [HideInInspector] public int   octaves     = 6;
    [HideInInspector] public float persistence = 0.55f;
    [HideInInspector] public float lacunarity  = 2f;
    [HideInInspector] public bool  ridged      = true;

    // 회오리 (Vortex Domain Warp)
    [HideInInspector] public bool  useVortex      = true;
    [HideInInspector] public int   vortexCount    = 4;      // 회오리 개수
    [HideInInspector] public float vortexStrength = 3.0f;   // 회전 강도 (라디안)
    [HideInInspector] public float vortexRadius   = 0.30f;  // 영향 반경 (UV 단위)
    [HideInInspector] public int   vortexSeed     = 99;
    [HideInInspector] public bool  vortexEye      = true;   // 허리케인 눈 (중심 맑음)
    [HideInInspector] public float vortexEyeSize  = 0.07f;  // 눈 반경 (UV 단위)

    // 위도 조절
    [HideInInspector] public bool  usePolarBoost      = true;
    [HideInInspector] public float polarBoostWidth    = 0.18f;
    [HideInInspector] public float polarBoostAmount   = 0.25f;
    [HideInInspector] public bool  useEquatorClear    = false;
    [HideInInspector] public float equatorClearWidth  = 0.10f;
    [HideInInspector] public float equatorClearAmount = 0.20f;

    // 결과
    [HideInInspector] public Texture2D previewTex;
    [HideInInspector] public Texture2D rawTex;
    [HideInInspector] public string    statusMsg    = "";
    [HideInInspector] public int[]     histogram    = new int[256];
    [HideInInspector] public bool      hasHistogram = false;
}
