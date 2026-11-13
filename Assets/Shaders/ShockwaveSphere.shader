// 미사일 범위 피해 충격파 구 — 테두리(fresnel)가 밝아 경계가 또렷하고, 노이즈 텍스처가 흘러 일렁임. 가산 합성 반투명
// _Alpha는 ExplosionShockwave가 MaterialPropertyBlock으로 매 프레임 조절(머티리얼 인스턴스 생성 없음)
Shader "TheFirst/ShockwaveSphere"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2.0, 0.9, 0.25, 1)
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseScrollSpeed ("Noise Scroll Speed (UV/sec)", Vector) = (0.15, 0.25, 0, 0)
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.6
        _RimPower ("Rim Power (클수록 테두리가 얇아짐)", Range(0.5, 8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 4)) = 1.5
        _FillAlpha ("Fill Alpha (구 안쪽 면 밝기)", Range(0, 1)) = 0.15
        _Alpha ("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
            };

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _NoiseTex_ST;
                float4 _NoiseScrollSpeed;
                float _NoiseStrength;
                float _RimPower;
                float _RimIntensity;
                float _FillAlpha;
                float _Alpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _NoiseTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float facing = saturate(dot(normalWS, viewDirWS));
                float rim = pow(1.0 - facing, _RimPower) * _RimIntensity;

                float2 scrolledUV = input.uv + _NoiseScrollSpeed.xy * _Time.y;
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, scrolledUV).r;
                float noiseFactor = lerp(1.0, noise * 2.0, _NoiseStrength);

                float alpha = saturate((rim + _FillAlpha) * noiseFactor) * _Alpha * _Color.a;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
