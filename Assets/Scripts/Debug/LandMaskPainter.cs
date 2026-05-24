using UnityEngine;

public class LandMaskPainter : MonoBehaviour
{
    // ─── 바이옴 정의 (셰이더·런타임 공용 단일 정보원) ─────────────────────────
    public const float SeaLevel = 127.5f / 255f;  // 0.5 — 바다/육지 경계 (byte 127.5)

    public static readonly string[] BiomeNames =
        { "심해", "얕은바다", "저지대", "평야", "고원" };

    // min/max 포함 (inclusive). 순서: 심해→얕은바다→저지대→평야→고원
    public static readonly (byte min, byte max)[] BiomeRanges =
    {
        (  0,  63),  // 심해      64칸
        ( 64, 127),  // 얕은바다  64칸
        (128, 159),  // 저지대    32칸
        (160, 238),  // 평야      79칸
        (239, 255),  // 고원      17칸
    };

    public Texture2D sourceTex;
    [HideInInspector] public CelestialBodyConfig previewBodyConfig = new();

    // ─── 생성 설정 (Editor에서 직접 그림) ───────────────────────────────────
    [HideInInspector] public int pctDeepSea    = 35;
    [HideInInspector] public int pctShallowSea = 25;
    [HideInInspector] public int pctLowland    = 10;
    [HideInInspector] public int pctPlains     = 25;
    [HideInInspector] public int pctHighland   = 5;

    // R채널 고도 노이즈
    [HideInInspector] public int   rSeed        = 0;
    [HideInInspector] public int   rNoiseScale  = 4;   // 수평 주기 수(정수=이음새 없음)
    [HideInInspector] public int   rOctaves     = 6;
    [HideInInspector] public float rPersistence = 0.5f;
    [HideInInspector] public float rLacunarity  = 2f;
    [HideInInspector] public bool  rRidged      = false; // true = 섬/군도 스타일 (능선 분리)
    [HideInInspector] public float poleOceanWidth = 0.10f; // 극지방 바다 강제 폭 (0~0.30 = 텍스처 높이 %)

    // G채널 바이옴 변이 노이즈 (zone 내 biome 판정)
    [HideInInspector] public int   gSeed        = 42;
    [HideInInspector] public int   gScale       = 3;
    [HideInInspector] public int   gOctaves     = 4;
    [HideInInspector] public float gPersistence = 0.5f;
    [HideInInspector] public float gLacunarity  = 2f;

    // 저지대 G 변이 비율 (Sand/Green/Lake, 합계=100)
    [HideInInspector] public int gLowlandSand  = 40;
    [HideInInspector] public int gLowlandGreen = 50;
    [HideInInspector] public int gLowlandLake  = 10;

    // 평야 G 변이 비율 (Desert/Grass/Forest, 합계=100)
    [HideInInspector] public int gPlainsDesert = 15;
    [HideInInspector] public int gPlainsGrass  = 50;
    [HideInInspector] public int gPlainsForest = 35;

    // 고원 G 변이 비율 (Snow, 합계=100)
    [HideInInspector] public int gHighlandSnow = 100;

    [HideInInspector] public Texture2D previewTex;  // 컬러 시각화 (Inspector 표시용)
    [HideInInspector] public Texture2D rawTex;      // 실제 RG 텍스처 (저장용)
    [HideInInspector] public string    statusMsg    = "";
    [HideInInspector] public int[]     histogram    = new int[256];
    [HideInInspector] public bool      hasHistogram = false;
}
