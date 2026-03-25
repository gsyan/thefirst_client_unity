// 선택된 모듈 위에 오버레이로 그리드를 렌더링하는 Additive 쉐이더.
// 원본 머티리얼/쉐이더와 무관하게 동작. 월드공간 삼면 투영으로 UV 없이도 균일한 그리드 표시.
Shader "SpaceFleet/GridOverlay"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (0,1,1,1)
        _GridSpacing ("Grid Spacing", Range(0.01, 5.0)) = 0.4
        _GridThickness ("Grid Thickness", Range(0.5, 5.0)) = 2.0
        _GridAnimationSpeed ("Animation Speed", Range(0, 5)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "GridOverlay"
            Tags { "LightMode"="UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GridColor;
                float  _GridSpacing;
                float  _GridThickness;
                float  _GridAnimationSpeed;
            CBUFFER_END

            // 월드 공간 좌표로 그리드 라인 강도 계산
            float GridLine(float2 uv)
            {
                float2 g = abs(frac(uv / _GridSpacing - 0.5) - 0.5) / fwidth(uv / _GridSpacing);
                return 1.0 - saturate(min(g.x, g.y) / _GridThickness);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 법선 기반 삼면 투영 블렌딩
                float3 absN = abs(normalize(input.normalWS));
                float3 pos  = input.positionWS;

                float gXY = GridLine(pos.xy) * absN.z;
                float gXZ = GridLine(pos.xz) * absN.y;
                float gYZ = GridLine(pos.yz) * absN.x;
                float grid = saturate(gXY + gXZ + gYZ);

                // 진폭 펄스 애니메이션
                float pulse = 0.6 + 0.4 * sin(_Time.y * _GridAnimationSpeed * 3.14159);

                return float4(_GridColor.rgb * grid * pulse, 1.0);
            }
            ENDHLSL
        }
    }
}
