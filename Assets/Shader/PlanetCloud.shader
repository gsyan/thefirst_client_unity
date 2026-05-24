Shader "Custom/PlanetCloud"
{
    Properties
    {
        _CloudTex    ("Cloud Alpha Tex", 2D)    = "white" {}
        _CloudColor    ("Cloud Color",     Color)        = (1.0, 1.0, 1.0, 0.9)
        _CloudCoverage ("Cloud Coverage", Range(0.0, 1.0)) = 0.5
        _RotationRad   ("Rotation Rad",   Float)         = 0.0
        _DarkSideMin   ("Dark Side Min",  Range(0.0, 1.0)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PlanetCloud"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudTex_ST;
                half4  _CloudColor;
                half   _CloudCoverage;
                float  _RotationRad;
                half   _DarkSideMin;
            CBUFFER_END

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

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
                float2 centered = uv - 0.5;
                float  s = sin(rad);
                float  c = cos(rad);
                float2 rotated = float2(c * centered.x - s * centered.y,
                                        s * centered.x + c * centered.y);
                return rotated + 0.5;
            }

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _CloudTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv        = RotateUV(IN.uv, _RotationRad);
                half   cloudMask = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv).r;

                // Coverage 수치로 구름 영역 제어 (LandCoverage와 동일 방식)
                half   alpha = saturate((cloudMask - (1.0h - _CloudCoverage)) / 0.08h);
                alpha *= _CloudColor.a;

                Light mainLight  = GetMainLight();
                half  sunDot     = dot(normalize(IN.normalWS), mainLight.direction);
                half  sunFactor  = lerp(_DarkSideMin, 1.0h, saturate(sunDot));

                half3 color = _CloudColor.rgb * sunFactor;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
