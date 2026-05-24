Shader "Custom/PlanetSurface"
{
    Properties
    {
        _LandMaskTex     ("Land Mask (Grayscale)", 2D)         = "white" {}
        _DeepSeaColor    ("Deep Sea Color",        Color)      = (0.05, 0.15, 0.45, 1.0)
        _ShallowSeaColor ("Shallow Sea Color",     Color)      = (0.10, 0.35, 0.65, 1.0)
        _CoastColor      ("Coast Color",           Color)      = (0.75, 0.70, 0.50, 1.0)
        _GrasslandColor  ("Grassland Color",       Color)      = (0.28, 0.55, 0.18, 1.0)
        _ForestColor     ("Forest Color",          Color)      = (0.08, 0.28, 0.08, 1.0)
        _DesertColor     ("Desert Color",          Color)      = (0.80, 0.65, 0.30, 1.0)
        _HighlandColor   ("Highland Color",        Color)      = (0.55, 0.48, 0.38, 1.0)
        _LandCoverage    ("Land Coverage",         Range(0,1)) = 0.5
        _RotationRad     ("Rotation Rad",          Float)      = 0.0
        _DarkSideMin     ("Dark Side Min",         Range(0,1)) = 0.15
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
                half4  _CoastColor;
                half4  _GrasslandColor;
                half4  _ForestColor;
                half4  _DesertColor;
                half4  _HighlandColor;
                half   _LandCoverage;
                float  _RotationRad;
                half   _DarkSideMin;
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 RotateUV(float2 uv, float rad)
            {
                float2 c = uv - 0.5;
                float  s = sin(rad), cs = cos(rad);
                return float2(cs * c.x - s * c.y, s * c.x + cs * c.y) + 0.5;
            }

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _LandMaskTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ── 1. 육지/바다 판정 ──────────────────────────────────
                float2 uv     = RotateUV(IN.uv, _RotationRad);
                half   mask   = SAMPLE_TEXTURE2D(_LandMaskTex, sampler_LandMaskTex, uv).r + (_LandCoverage - 0.5h) * 2.0h;
                half   seaLvl = 1.0h - _LandCoverage;

                // ── 2. 바다 색 (고도 그라디언트) ──────────────────────
                half  tSea   = saturate(mask / max(seaLvl, 0.001h));
                half3 seaCol = lerp(_DeepSeaColor.rgb, _ShallowSeaColor.rgb,
                                    smoothstep(0.4h, 1.0h, tSea));

                // ── 3. 바이옴 값 = 육지 고도 (mask를 육지 범위로 정규화) ─
                half biomeVal = saturate((mask - seaLvl) / max(1.0h - seaLvl, 0.001h));

                // ── 4. 고도 4등분 임계값 ──────────────────────────────
                half t1 = 0.25h;
                half t2 = 0.50h;
                half t3 = 0.75h;

                half edge = 0.04h;
                half3 biomeCol = _GrasslandColor.rgb;
                biomeCol = lerp(biomeCol, _ForestColor.rgb,
                                smoothstep(t1 - edge, t1 + edge, biomeVal));
                biomeCol = lerp(biomeCol, _DesertColor.rgb,
                                smoothstep(t2 - edge, t2 + edge, biomeVal));
                biomeCol = lerp(biomeCol, _HighlandColor.rgb,
                                smoothstep(t3 - edge, t3 + edge, biomeVal));

                // ── 5. 해안선 전환 (coast 색 → 바이옴 색) ────────────
                half coastFade = smoothstep(seaLvl + 0.02h, seaLvl + 0.10h, mask);
                half3 landCol  = lerp(_CoastColor.rgb, biomeCol, coastFade);

                // ── 6. 바다/육지 블렌딩 ───────────────────────────────
                half  landMask = smoothstep(seaLvl - 0.03h, seaLvl + 0.03h, mask);
                half3 surface  = lerp(seaCol, landCol, landMask);

                // ── 7. 태양 조명 ──────────────────────────────────────
                Light mainLight = GetMainLight();
                half  sunDot    = dot(normalize(IN.normalWS), mainLight.direction);
                half  sunFactor = lerp(_DarkSideMin, 1.0h, saturate(sunDot));

                return half4(surface * sunFactor, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
