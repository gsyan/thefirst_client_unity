// 단일 패스로 면 채움(_FaceAlpha)과 모서리 코어+글로우를 함께 계산 — 멀티패스가 URP에서 안정적으로 그려지지 않아 1패스로 통합
Shader "Custom/HoloGridCell"
{
    Properties
    {
        _BaseColor("Edge/Fill Color", Color) = (1, 0.85, 0.3, 1)
        _EdgeWidth("Edge Core Width", Range(0.001, 0.1)) = 0.006
        _GlowSpread("Glow Spread (core width 배수)", Range(1, 12)) = 6
        _GlowIntensity("Glow Intensity", Range(0, 3)) = 1.2
        _CornerBoost("Corner Boost", Range(0, 3)) = 1.2
        _FaceAlpha("Face Fill Alpha", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _EdgeWidth;
                half _GlowSpread;
                half _GlowIntensity;
                half _CornerBoost;
                half _FaceAlpha;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half2 distToEdge = min(IN.uv, 1.0 - IN.uv);
                half edgeDist = min(distToEdge.x, distToEdge.y);

                half glowWidth = _EdgeWidth * _GlowSpread;
                half coreMask = 1.0 - smoothstep(_EdgeWidth * 0.5, _EdgeWidth, edgeDist);
                half glowMask = 1.0 - smoothstep(_EdgeWidth, glowWidth, edgeDist);

                half cornerMaskX = 1.0 - smoothstep(_EdgeWidth, glowWidth, distToEdge.x);
                half cornerMaskY = 1.0 - smoothstep(_EdgeWidth, glowWidth, distToEdge.y);
                half cornerMask = cornerMaskX * cornerMaskY;

                half edgeIntensity = coreMask + glowMask * glowMask * _GlowIntensity + cornerMask * _CornerBoost;

                half alpha = saturate(_FaceAlpha + edgeIntensity);
                clip(alpha - 0.001);

                half3 color = _BaseColor.rgb * (1.0 + edgeIntensity);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
