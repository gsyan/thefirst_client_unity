Shader "Custom/PlanetCloud"
{
    Properties
    {
        _CloudTex      ("Cloud Alpha Tex", 2D)          = "white" {}
        _CloudColor    ("Cloud Color",     Color)        = (1.0, 1.0, 1.0, 0.9)
        _CloudCoverage    ("Cloud Coverage",      Range(0.0, 1.0)) = 0.5
        _RotationRad      ("Rotation Rad",       Float)          = 0.0
        _DarkSideMin      ("Dark Side Min",      Range(0.0, 1.0)) = 0.1
        _MidLatOpacity    ("MidLat Opacity",     Range(0.0, 1.0))  = 0.0
        _MidLatCenter     ("MidLat Center (UV v)",Range(0.1, 0.45)) = 0.25
        _MidLatWidth      ("MidLat Width",       Range(0.0, 0.5))  = 0.12
        _CloudSoftness    ("Cloud Softness",     Range(0.0, 0.5))  = 0.3
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
                half   _MidLatOpacity;
                half   _MidLatCenter;
                half   _MidLatWidth;
                half   _CloudSoftness;
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
                // 위도별 opacity — 8비트 banding 없이 float 정밀도로 직접 계산
                // IN.uv.y 사용: RotateUV는 2D 회전이라 v도 틀어지므로 회전 전 원본 사용
                half origV  = (half)IN.uv.y;
                half distN  = abs(origV - _MidLatCenter);
                half distS  = abs(origV - (1.0h - _MidLatCenter));
                half mFactor = saturate(1.0h - min(distN, distS) / max(_MidLatWidth, 0.001h));
                mFactor      = mFactor * mFactor * (3.0h - 2.0h * mFactor);
                half latOpacity = 1.0h - mFactor * _MidLatOpacity;

                float2 uv          = RotateUV(IN.uv, _RotationRad);
                half4  cloudSample = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv);

                // R: coverage 기준 이동 + softness로 구름 결 표현
                // softness=0 → binary, softness=0.5 → smoothstep(0,1) → R값이 그대로 alpha에 반영
                half adjusted = cloudSample.r + (_CloudCoverage - 0.5h) * 2.0h;
                half lo       = 0.5h - _CloudSoftness;
                half hi       = 0.5h + _CloudSoftness;
                half alpha    = smoothstep(lo, hi, adjusted) * latOpacity * _CloudColor.a;
                
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
