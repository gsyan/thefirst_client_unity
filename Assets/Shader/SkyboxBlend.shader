Shader "SpaceFleet/SkyboxBlend"
{
    Properties
    {
        _SkyboxA ("Skybox A", Cube) = "" {}
        _SkyboxB ("Skybox B", Cube) = "" {}
        _Blend ("Blend", Range(0, 1)) = 0
        _TintA ("Tint A", Color) = (1, 1, 1, 1)
        _TintB ("Tint B", Color) = (1, 1, 1, 1)
        _ExposureA ("Exposure A", Range(0, 8)) = 1
        _ExposureB ("Exposure B", Range(0, 8)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_SkyboxA);
            TEXTURECUBE(_SkyboxB);
            SAMPLER(sampler_SkyboxA);
            SAMPLER(sampler_SkyboxB);

            CBUFFER_START(UnityPerMaterial)
                float _Blend;
                float4 _TintA;
                float4 _TintB;
                float _ExposureA;
                float _ExposureB;
                float _Rotation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 texcoord : TEXCOORD0;
            };

            float3 RotateAroundYInDegrees(float3 vertex, float degrees)
            {
                float alpha = degrees * PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 rotated = RotateAroundYInDegrees(IN.positionOS.xyz, _Rotation);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.texcoord = rotated;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 colA = SAMPLE_TEXTURECUBE(_SkyboxA, sampler_SkyboxA, IN.texcoord);
                float4 colB = SAMPLE_TEXTURECUBE(_SkyboxB, sampler_SkyboxB, IN.texcoord);

                colA.rgb *= _TintA.rgb * _ExposureA;
                colB.rgb *= _TintB.rgb * _ExposureB;

                return lerp(colA, colB, _Blend);
            }
            ENDHLSL
        }
    }
}
