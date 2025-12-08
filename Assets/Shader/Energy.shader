Shader "SpaceFleet/Energy"
{
    Properties
    {
        [Header(Base)]
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Color", Color) = (0.2,0.8,1,1)

        [Header(Surface)]
        _Metallic ("Metallic", Range(0,1)) = 0.3
        _Smoothness ("Smoothness", Range(0,1)) = 0.9
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0

        [Header(Energy Effect)]
        _EnergyColor ("Energy Color", Color) = (0,1,2,1)
        _EnergyIntensity ("Energy Intensity", Range(0,5)) = 2.0
        _EnergySpeed ("Energy Speed", Range(0,10)) = 3.0
        _EnergyScale ("Energy Scale", Range(0.1,5)) = 1.0

        [Header(Emission)]
        _EmissionColor ("Emission Color", Color) = (0,0.5,1)
        _EmissionMap ("Emission", 2D) = "white" {}
        _EmissionIntensity ("Emission Intensity", Range(0,10)) = 1.0

        [Header(Highlight)]
        _HighlightColor ("Highlight Color", Color) = (0,0,0,0)
        _HighlightIntensity ("Highlight Intensity", Range(0,2)) = 0

        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(0,10)) = 2.0
        _FresnelColor ("Fresnel Color", Color) = (0,1,2,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Metallic;
                float _Smoothness;
                float4 _BumpMap_ST;
                float _BumpScale;
                float4 _EnergyColor;
                float _EnergyIntensity;
                float _EnergySpeed;
                float _EnergyScale;
                float4 _EmissionColor;
                float _EmissionIntensity;
                float4 _HighlightColor;
                float _HighlightIntensity;
                float _FresnelPower;
                float4 _FresnelColor;
            CBUFFER_END

            // Simple noise function
            float noise(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
            }

            // Energy flow effect
            float energyFlow(float2 uv, float time)
            {
                float2 flowUV = uv * _EnergyScale;
                flowUV += float2(sin(time * _EnergySpeed), cos(time * _EnergySpeed * 0.7)) * 0.1;

                float n1 = noise(flowUV);
                float n2 = noise(flowUV * 2.0 + time * 0.5);
                float n3 = noise(flowUV * 4.0 - time * 0.3);

                return (n1 + n2 * 0.5 + n3 * 0.25) / 1.75;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS.xyz, input.tangentOS.w);
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.vertex = vertexInput.positionCS;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Sample textures
                float4 albedoAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 albedo = albedoAlpha.rgb * _Color.rgb;

                float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalWS = normalize(TransformTangentToWorld(normalTS, tangentToWorld));

                float3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb * _EmissionIntensity;

                // Lighting calculation
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float NdotV = saturate(dot(normalWS, viewDirWS));

                // Energy flow effect
                float time = _Time.y;
                float energyPattern = energyFlow(input.uv, time);
                float3 energyEffect = _EnergyColor.rgb * energyPattern * _EnergyIntensity;

                // Fresnel effect for energy glow
                float fresnel = pow(1.0 - NdotV, _FresnelPower);
                float3 fresnelGlow = _FresnelColor.rgb * fresnel;

                // PBR lighting (energy material)
                float3 diffuse = albedo * mainLight.color * NdotL * mainLight.shadowAttenuation;

                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float roughness = 1.0 - _Smoothness;
                float specularPower = exp2(12 * _Smoothness + 1); // Energy has sharp highlights
                float3 specular = pow(NdotH, specularPower) * _Metallic * mainLight.color * mainLight.shadowAttenuation;

                // Additional lights
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    float NdotL_add = saturate(dot(normalWS, light.direction));
                    diffuse += albedo * light.color * NdotL_add * light.distanceAttenuation;
                }

                // Ambient
                float3 ambient = SampleSH(normalWS) * albedo * 0.2; // Energy materials have less ambient

                // Highlight effect
                float3 highlight = _HighlightColor.rgb * _HighlightIntensity;

                // Combine all effects
                float3 finalColor = diffuse + specular + ambient + emission + energyEffect + fresnelGlow + highlight;

                // Energy materials glow more
                finalColor = lerp(finalColor, finalColor * 1.5, energyPattern * 0.3);

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}