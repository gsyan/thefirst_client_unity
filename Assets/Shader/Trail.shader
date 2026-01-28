Shader "SpaceFleet/Trail"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0,10)) = 2.0
    }

    SubShader
    {
        Tags {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Intensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 텍스처 샘플링 (없으면 white = 1,1,1,1)
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // 텍스처 알파가 0이면 (텍스처 없음) 원형 마스크 사용
                float circle = 1.0;
                if (tex.a < 0.01)
                {
                    float2 center = input.uv - 0.5;
                    float dist = length(center) * 2.0;
                    circle = saturate(1.0 - dist);
                    tex = float4(1, 1, 1, 1);
                }

                // Vertex Color * Material Color * Texture
                float4 col = input.color * _Color * tex;

                // HDR 출력 (Bloom 지원)
                float3 finalColor = col.rgb * _Intensity;

                return float4(finalColor, col.a * circle);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
