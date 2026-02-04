Shader "Custom/RadialBlur"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "RadialBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float _CenterX;
            float _CenterY;
            int _Samples;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 center = float2(_CenterX, _CenterY);
                float2 dir = uv - center;

                float dist = length(dir);
                float blurAmount = _Intensity * dist;

                half4 color = half4(0, 0, 0, 0);

                // 중심에서 멀어질수록 방사형 블러
                float2 blurDir = (dist > 0.001) ? normalize(dir) * blurAmount / _Samples : float2(0, 0);

                for (int i = 0; i < _Samples; i++)
                {
                    float2 sampleUV = uv - blurDir * i;
                    color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV);
                }

                color /= _Samples;
                return color;
            }
            ENDHLSL
        }
    }
}
