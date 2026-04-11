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

        Blend SrcAlpha One
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // GPU Instancing용 인스턴스별 프로퍼티 (MPB와 호환)
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _GlowIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _PulseSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _PulseAmplitude)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

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
                UNITY_SETUP_INSTANCE_ID(input);

                float4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float glowIntensity = UNITY_ACCESS_INSTANCED_PROP(Props, _GlowIntensity);
                float pulseSpeed = UNITY_ACCESS_INSTANCED_PROP(Props, _PulseSpeed);
                float pulseAmplitude = UNITY_ACCESS_INSTANCED_PROP(Props, _PulseAmplitude);

                float pulse = sin(_Time.y * pulseSpeed) * pulseAmplitude + (1.0 - pulseAmplitude);
                pulse = max(pulse, 0.5);

                float3 finalColor = color.rgb * glowIntensity * pulse;

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
