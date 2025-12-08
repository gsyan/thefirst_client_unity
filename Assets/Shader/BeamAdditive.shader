Shader "SpaceFleet/BeamAdditive"
{
    Properties
    {
        _MainTex ("Beam Texture", 2D) = "white" {}
        _Color ("Beam Color", Color) = (0,0.5,1,1)
        _Intensity ("Intensity", Range(0,10)) = 2.0
        _BeamThickness ("Beam Thickness", Range(0.01, 1.0)) = 0.05
        _EdgeFadeArea ("Edge Fade Area", Range(0.0, 0.5)) = 0.1
        _EdgeFadeXPower ("Edge Fade X Power", Range(1.0, 5.0)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "BeamAdditive"
            Tags { "LightMode"="UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Intensity;
                float _BeamThickness;
                float _EdgeFadeArea;
                float _EdgeFadeXPower;
                
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.vertex = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float center = 0.5;
                uv.y = 0.5 + (uv.y - center) * _BeamThickness;

                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                float distFromStart = input.uv.x;
                float distFromEnd = 1.0 - input.uv.x;
                
                float edgeFadeX = 1.0;
                if (distFromStart < _EdgeFadeArea)
                    edgeFadeX = pow(distFromStart / _EdgeFadeArea, _EdgeFadeXPower);
                if (distFromEnd < _EdgeFadeArea)
                    edgeFadeX = min(edgeFadeX, pow(distFromEnd / _EdgeFadeArea, _EdgeFadeXPower));
                
                float edgeFadeY = 1.0;
                float distFromCenterY = abs(input.uv.y - 0.5) * 2.0;
                float3 finalColor = texColor.rgb * _Color.rgb * _Intensity * edgeFadeX * (1.0 - distFromCenterY);
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
