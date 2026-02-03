Shader "SpaceFleet/WarpDistortion"
{
    Properties
    {
        [Header(Distortion)]
        _DistortionStrength ("Distortion Strength", Range(0, 0.2)) = 0.05
        _DistortionSpeed ("Distortion Speed", Range(0, 10)) = 3.0
        _DistortionScale ("Distortion Scale", Range(0.1, 10)) = 2.0

        [Header(Ring Effect)]
        _RingColor ("Ring Color", Color) = (0.2, 0.9, 1, 1)
        _RingWidth ("Ring Width", Range(0.01, 0.5)) = 0.15
        _RingIntensity ("Ring Intensity", Range(0, 20)) = 5.0
        _RingRadius ("Ring Radius", Range(0.1, 0.9)) = 0.4

        [Header(Glow)]
        _GlowColor ("Glow Color", Color) = (0.1, 0.5, 1, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 2.0

        [Header(Animation)]
        _PulseSpeed ("Pulse Speed", Range(0, 20)) = 8.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WarpDistortion"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float _DistortionStrength;
                float _DistortionSpeed;
                float _DistortionScale;
                float4 _RingColor;
                float _RingWidth;
                float _RingIntensity;
                float _RingRadius;
                float4 _GlowColor;
                float _GlowIntensity;
                float _PulseSpeed;
            CBUFFER_END

            // 심플 노이즈 함수
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // UV를 중심 기준으로 변환 (-0.5 ~ 0.5)
                float2 centeredUV = input.uv - 0.5;
                float dist = length(centeredUV) * 2.0; // 0 ~ 1 범위

                // 시간
                float time = _Time.y;

                // 펄스 효과
                float pulse = sin(time * _PulseSpeed) * 0.3 + 0.7;

                // 노이즈 기반 패턴
                float2 noiseUV = centeredUV * _DistortionScale + time * _DistortionSpeed * 0.1;
                float n = noise(noiseUV);

                // 링 이펙트 (확장하는 원형 웨이브)
                float waveTime = frac(time * 0.5);
                float wave1 = smoothstep(_RingWidth, 0.0, abs(dist - waveTime));
                float wave2 = smoothstep(_RingWidth, 0.0, abs(dist - frac(waveTime + 0.5)));

                // 정적 링
                float staticRing = smoothstep(_RingWidth, 0.0, abs(dist - _RingRadius));
                staticRing *= pulse;

                // 외곽 페이드
                float edgeFade = 1.0 - smoothstep(0.7, 1.0, dist);

                // Fresnel 효과 (뷰 각도에 따른 글로우)
                float3 viewDir = normalize(input.viewDirWS);
                float3 normal = normalize(input.normalWS);
                float fresnel = pow(1.0 - saturate(dot(viewDir, normal)), 3.0);

                // 링 컬러
                float ringMask = (wave1 + wave2 * 0.5 + staticRing) * edgeFade;
                float3 ringColor = _RingColor.rgb * ringMask * _RingIntensity;

                // 글로우 컬러 (내부 영역)
                float innerGlow = (1.0 - dist) * edgeFade * pulse;
                innerGlow += n * 0.3 * edgeFade;
                float3 glowColor = _GlowColor.rgb * innerGlow * _GlowIntensity;

                // Fresnel 글로우
                float3 fresnelColor = _RingColor.rgb * fresnel * _GlowIntensity * 0.5;

                // 최종 컬러
                float3 finalColor = ringColor + glowColor + fresnelColor;
                float alpha = saturate(ringMask + innerGlow * 0.5 + fresnel * 0.3) * edgeFade;

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
