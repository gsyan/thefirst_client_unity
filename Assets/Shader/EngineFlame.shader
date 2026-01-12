Shader "SpaceFleet/EngineFlame"
{
    Properties
    {
        [Header(Base Color)]
        _Color ("Flame Color", Color) = (0.2,0.9,1,1)

        [Header(Glow Effect)]
        _GlowIntensity ("Glow Intensity", Range(0,50)) = 25.0
        _PulseSpeed ("Pulse Speed", Range(0,100)) = 10.0
        _PulseAmplitude ("Pulse Amplitude", Range(0,1)) = 0.4
    }

    SubShader
    {
        Tags {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha One  // Additive blending for glow
        ZWrite Off
        Cull Back

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
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _GlowIntensity;
                float _PulseSpeed;
                float _PulseAmplitude;
            CBUFFER_END


            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Pulse effect (breathing)
                float time = _Time.y;
                float pulse = sin(time * _PulseSpeed) * _PulseAmplitude + (1.0 - _PulseAmplitude);
                pulse = max(pulse, 0.5); // Prevent pulse from going below 0.5 to avoid color shift

                // HDR 범위로 출력 (Bloom을 위해)
                float intensity = _GlowIntensity * pulse;

                // HDR 색상 출력 (1.0 이상의 값으로 Bloom threshold를 넘김)
                float3 finalColor = _Color.rgb * intensity;

                float alpha = 1.0;
                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
