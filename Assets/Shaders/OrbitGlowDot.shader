// 중심에서 바깥으로 부드럽게 사그라드는 원형 발광 점 — 가산 블렌딩(Blend One One)으로 별도 Bloom 없이도 빛나 보임
Shader "Custom/OrbitGlowDot"
{
    Properties
    {
        _BaseColor("Glow Color", Color) = (1, 1, 1, 1)
        _Intensity("Intensity", Range(0, 5)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend One One

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
                half _Intensity;
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
                half2 centered = IN.uv - 0.5;
                half normalizedDist = length(centered) * 2.0; // 0 = 중심, 1 = 가장자리
                half falloff = saturate(1.0 - normalizedDist);
                falloff *= falloff;
                clip(falloff - 0.01);

                half3 color = _BaseColor.rgb * falloff * _Intensity;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
