Shader "SpaceFleet/Metal"
{
    Properties
    {
        [Header(Base)]
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)

        [Header(Surface)]
        _Metallic ("Metallic", Range(0,1)) = 0.8
        _Smoothness ("Smoothness", Range(0,1)) = 0.7
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0

        [Header(Emission)]
        _EmissionColor ("Emission Color", Color) = (0,0,0)
        _EmissionMap ("Emission", 2D) = "white" {}

        [Header(Selection Grid)]
        _GridColor ("Grid Color", Color) = (0,1,1,1)
        _GridIntensity ("Grid Intensity", Range(0,2)) = 0
        _GridThickness ("Grid Thickness", Range(0.001, 0.1)) = 0.02
        _GridSpacing ("Grid Spacing", Range(0.1, 10)) = 3.0
        _GridAnimationSpeed ("Grid Animation Speed", Range(0, 5)) = 2
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
                float4 _EmissionColor;
                float4 _GridColor;
                float _GridIntensity;
                float _GridThickness;
                float _GridSpacing;
                float _GridAnimationSpeed;
            CBUFFER_END

            // Grid calculation function with proper UV handling
            float calculateGrid(float2 uv, float spacing, float thickness)
            {
                // Simple approach: just use spacing directly on UV
                // spacing controls grid density (smaller = more grids)
                float2 gridUV = uv / spacing;

                // Create grid lines at integer positions
                float2 grid = abs(frac(gridUV - 0.5) - 0.5) / fwidth(gridUV);
                float gridLine = min(grid.x, grid.y);

                // Convert to mask (0 = grid line, 1 = no line)
                return 1.0 - saturate(gridLine / thickness);
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

                float3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;

                // Lighting calculation
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float NdotV = saturate(dot(normalWS, viewDirWS));

                // PBR lighting (simplified)
                float3 diffuse = albedo * mainLight.color * NdotL * mainLight.shadowAttenuation;

                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float roughness = 1.0 - _Smoothness;
                float specularPower = exp2(10 * _Smoothness + 1);
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
                float3 ambient = SampleSH(normalWS) * albedo * 0.3;

                // Grid effect
                float3 gridEffect = float3(0, 0, 0);
                if (_GridIntensity > 0)
                {
                    // Use UV coordinates with spacing adjustment
                    float gridMask = calculateGrid(input.uv, _GridSpacing, _GridThickness);

                    // Animation pulse (sawtooth wave for sharp transitions)
                    float time = _Time.y * _GridAnimationSpeed * 5;
                    float pulse = frac(time / 6.28318); // Linear sawtooth 0.0 ~ 1.0, resets sharply
                    pulse = 1.0 - abs(pulse * 2.0 - 1.0); // Triangle wave 0->1->0 for smooth but faster transitions

                    // Enhanced grid visibility
                    float3 gridColor = _GridColor.rgb * _GridIntensity * 1.0;
                    gridEffect = gridColor * gridMask * pulse;

                    // Add glow effect around grid lines
                    float glowMask = calculateGrid(input.uv, _GridSpacing, _GridThickness * 3.0);
                    gridEffect += gridColor * glowMask * 0.3 * pulse;
                }

                float3 finalColor = diffuse + specular + ambient + emission + gridEffect;

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