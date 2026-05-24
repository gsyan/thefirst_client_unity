Shader "Custom/PlanetSurface"
{
    Properties
    {
        _LandMaskTex   ("Land Mask (RG)",  2D)           = "white" {}
        
        // 색 — Inspector 비노출, MPB로 설정. 기본값은 에디터 프리뷰용
        _DeepSeaColor         ("DeepSeaColor",Color) = (0.05, 0.15, 0.45, 1)
        _ShallowSeaColor      ("ShallowSeaColor",Color) = (0.10, 0.35, 0.65, 1)
        _LowlandSandColor     ("LowlandSandColor",Color) = (0.75, 0.70, 0.50, 1)
        _LowlandGreenColor    ("LowlandGreenColor",Color) = (0.56, 0.75, 0.38, 1)
        _PlainsDesertColor    ("PlainsDesertColor",Color) = (0.48, 0.41, 0.24, 1)
        _PlainsGrassColor     ("PlainsGrassColor",Color) = (0.28, 0.55, 0.18, 1)
        _PlainsForestColor    ("PlainsForestColor",Color) = (0.08, 0.28, 0.08, 1)
        _HighlandSnowColor    ("HighlandSnowColor",Color) = (0.91, 0.94, 0.96, 1)
        _IceColor             ("IceColor",Color) = (0.95, 0.98, 1.0,  1)
        _IceColorEdge         ("IceColorEdge",Color) = (0.68, 0.82, 0.94, 1)

        [Header(Common)]
        _LandCoverage  ("Land Coverage",   Range(0,1))   = 0.5
        _RotationRad   ("Rotation Rad",    Float)        = 0.0
        _DarkSideMin   ("Dark Side Min",   Range(0,1))   = 0.15
        _HasPolarIce   ("Has Polar Ice",   Float)        = 0.0
        _PoleIceWidth  ("Pole Ice Width",  Range(0,0.4)) = 0.12
        [Header(Biome Blend)]
        _BiomeBlend    ("BiomeBlend (R)",  Range(0,0.05)) = 0.01
        _GBlend        ("GBlend (G)",      Range(0,0.15)) = 0.02        
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Geometry"
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PlanetSurface"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _LandMaskTex_ST;
                half4  _DeepSeaColor;
                half4  _ShallowSeaColor;
                half4  _LowlandSandColor;
                half4  _LowlandGreenColor;
                half4  _PlainsDesertColor;
                half4  _PlainsGrassColor;
                half4  _PlainsForestColor;
                half4  _HighlandSnowColor;
                half   _LandCoverage;
                float  _RotationRad;
                half   _DarkSideMin;
                half   _HasPolarIce;
                half4  _IceColor;
                half4  _IceColorEdge;
                half   _PoleIceWidth;
                half   _BiomeBlend;
                half   _GBlend;
            CBUFFER_END

            TEXTURE2D(_LandMaskTex);
            SAMPLER(sampler_LandMaskTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 normalOS    : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 RotateUV(float2 uv, float rad)
            {
                float2 c = uv - 0.5;
                float  s = sin(rad), cs = cos(rad);
                return float2(cs * c.x - s * c.y, s * c.x + cs * c.y) + 0.5;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ── 1. 텍스처 샘플링 (R=고도, G=변이) ────────────────────
                float2 uv  = RotateUV(IN.uv, _RotationRad);
                half2  rg  = SAMPLE_TEXTURE2D(_LandMaskTex, sampler_LandMaskTex, uv).rg;
                half   r   = rg.r;   // 고도 (raw, 0~1)
                half   g   = rg.g;   // 변이 (0~1)
                half   mask   = r + (_LandCoverage - 0.5h) * 2.0h;  // 보정된 지형 높이
                half   seaLvl = 0.5h;                              // 해수면 고정

                // ── 2. 바다 색 ────────────────────────────────────────────
                half  tSea   = saturate(mask / seaLvl);
                half3 seaCol = lerp(_DeepSeaColor.rgb, _ShallowSeaColor.rgb,
                                    smoothstep(0.4h, 1.0h, tSea));

                // ── 3. 바이옴 색 (mask 기준, 해수면 0.5 고정) ─────────────
                // 존 경계 (mask 기준)
                half bLowlandEnd = 159.0h / 255.0h;
                half bPlainsEnd  = 238.0h / 255.0h;
                half eZone       = _BiomeBlend;

                // 저지대 색 (G 기반, 0.25/0.5/0.75 균등 분할)
                half gvb = _GBlend;
                half3 lowlandCol = _LowlandSandColor.rgb;
                lowlandCol = lerp(lowlandCol, _LowlandGreenColor.rgb, smoothstep(0.33h - gvb, 0.33h + gvb, g));
                lowlandCol = lerp(lowlandCol, _ShallowSeaColor.rgb,   smoothstep(0.67h - gvb, 0.67h + gvb, g));

                // 평야 색 (G 기반)
                half3 plainsCol = _PlainsDesertColor.rgb;
                plainsCol = lerp(plainsCol, _PlainsGrassColor.rgb,  smoothstep(0.25h - gvb, 0.25h + gvb, g));
                plainsCol = lerp(plainsCol, _PlainsForestColor.rgb, smoothstep(0.50h - gvb, 0.50h + gvb, g));

                // 고원 색 (G 기반)
                half3 highlandCol = _HighlandSnowColor.rgb;

                // 존 경계 블랜드 (mask 기준)
                half3 biomeCol = lowlandCol;
                biomeCol = lerp(biomeCol, plainsCol,   smoothstep(bLowlandEnd - eZone, bLowlandEnd + eZone, mask));
                biomeCol = lerp(biomeCol, highlandCol, smoothstep(bPlainsEnd  - eZone, bPlainsEnd  + eZone, mask));

                // ── 4. 바다/육지 블랜딩 ───────────────────────────────────
                half  landMask = smoothstep(seaLvl - 0.0h, seaLvl + 0.0h, mask);
                half3 surface  = lerp(seaCol, biomeCol, landMask);

                // ── 5. 극지방 얼음 ────────────────────────────────────────
                half poleBlend = abs(normalize(IN.normalOS).y);
                half iceMod    = (saturate((mask - seaLvl) * 2.0h) - 0.5h) * _PoleIceWidth * 0.6h;
                half iceFade   = smoothstep(1.0h - _PoleIceWidth, 1.0h, poleBlend + iceMod) * _HasPolarIce;
                half  landIce  = saturate((mask - seaLvl) * 3.0h);
                half3 iceMixed = lerp(_IceColorEdge.rgb, _IceColor.rgb,
                                      saturate(iceFade * iceFade + landIce * 0.25h));
                surface        = lerp(surface, iceMixed, iceFade);

                // ── 6. 태양 조명 ──────────────────────────────────────────
                Light mainLight = GetMainLight();
                half  sunDot    = dot(normalize(IN.normalWS), mainLight.direction);
                half  sunFactor = lerp(_DarkSideMin, 1.0h, saturate(sunDot));

                return half4(surface * sunFactor, 1.0h);
            }

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.normalOS    = IN.normalOS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _LandMaskTex);
                return OUT;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
