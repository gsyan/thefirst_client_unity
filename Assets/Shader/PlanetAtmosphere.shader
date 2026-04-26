Shader "Custom/PlanetAtmosphere"
{
    Properties
    {
        _AtmosphereColor ("Atmosphere Color", Color) = (0.3, 0.6, 1.0, 1.0)
        _RimPower        ("Rim Power",        Range(1.0, 12.0)) = 6.0
        _Intensity       ("Intensity",        Range(0.0,  5.0)) = 1.0
        // 어두운 면 대기 강도 (0 = 완전 소등, 1 = 동일 밝기)
        _DarkSideMin     ("Dark Side Min",    Range(0.0,  1.0)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent+1"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Atmosphere"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One   // Additive
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _AtmosphereColor;
                half  _RimPower;
                half  _Intensity;
                half  _DarkSideMin;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                float3 posWS    = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS   = GetWorldSpaceNormalizeViewDir(posWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normal  = normalize(IN.normalWS);
                float3 viewDir = normalize(IN.viewDirWS);

                // 가장자리 강도 (높을수록 얇은 대기)
                half rim = 1.0h - saturate(dot(viewDir, normal));
                rim = pow(rim, _RimPower);

                // 태양 방향 — 조명된 쪽은 밝게, 어두운 쪽은 DarkSideMin 배율로 감쇠
                Light mainLight  = GetMainLight();
                half  sunDot     = dot(normal, mainLight.direction);
                half  sunFactor  = lerp(_DarkSideMin, 1.0h, saturate(sunDot));

                return half4(_AtmosphereColor.rgb * (rim * _Intensity * sunFactor), 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
