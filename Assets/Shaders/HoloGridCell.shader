// 단일 패스로 면 채움(_FaceAlpha)과 모서리 코어+글로우를 함께 계산 — 멀티패스가 URP에서 안정적으로 그려지지 않아 1패스로 통합
// 추가로 테두리 둘레(arcParam, 0~1 시계방향)를 따라 국부적으로 두꺼워지는 오빗 구간(_OrbitPhase)을 지원 — Reachable 셀에서 도는 강조에 사용
Shader "Custom/HoloGridCell"
{
    Properties
    {
        _BaseColor("Edge Color", Color) = (1, 0.85, 0.3, 1)
        _FillColor("Fill Color (상태 무관 고정)", Color) = (0.45, 0.51, 0.59, 1)
        _EdgeWidth("Edge Core Width", Range(0.001, 0.1)) = 0.006
        _GlowSpread("Glow Spread (core width 배수)", Range(1, 12)) = 6
        _GlowIntensity("Glow Intensity", Range(0, 3)) = 1.2
        _CornerBoost("Corner Boost", Range(0, 3)) = 1.2
        _FaceAlpha("Face Fill Alpha", Range(0, 1)) = 0
        _OrbitPhase("Orbit Phase (0~1, 둘레 위치)", Range(0, 1)) = 0
        _OrbitArcWidth("Orbit Arc Width (둘레 비율)", Range(0.01, 0.5)) = 0.12
        _OrbitBoost("Orbit Boost", Range(0, 5)) = 1.5
        _OrbitEnabled("Orbit Enabled", Range(0, 1)) = 0
        _GlowEnabled("Glow Boost Enabled (Reachable 전용)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FillColor;
                half _EdgeWidth;
                half _GlowSpread;
                half _GlowIntensity;
                half _CornerBoost;
                half _FaceAlpha;
                half _OrbitPhase;
                half _OrbitArcWidth;
                half _OrbitBoost;
                half _OrbitEnabled;
                half _GlowEnabled;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // uv 기준 사각형 둘레를 시계방향으로 이어붙인 0~1 좌표 — 어느 변에 더 가까운지로 변을 정하고,
            // 그 변을 따라가는 좌표(반대 축)로 둘레상 위치를 계산
            half ComputeArcParam(half2 uv, half2 distToEdge)
            {
                half arcParam;
                if (distToEdge.x < distToEdge.y)
                {
                    bool isRight = uv.x > 0.5;
                    half posAlongEdge = uv.y;
                    arcParam = isRight ? (0.25 + posAlongEdge * 0.25) : (0.75 + (1.0 - posAlongEdge) * 0.25);
                }
                else
                {
                    bool isTop = uv.y > 0.5;
                    half posAlongEdge = uv.x;
                    arcParam = isTop ? (0.5 + (1.0 - posAlongEdge) * 0.25) : (posAlongEdge * 0.25);
                }
                return arcParam;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half2 distToEdge = min(IN.uv, 1.0 - IN.uv);
                half edgeDist = min(distToEdge.x, distToEdge.y);

                half glowWidth = _EdgeWidth * _GlowSpread;
                half coreMask = 1.0 - smoothstep(_EdgeWidth * 0.5, _EdgeWidth, edgeDist);
                half glowMask = 1.0 - smoothstep(_EdgeWidth, glowWidth, edgeDist);

                half cornerMaskX = 1.0 - smoothstep(_EdgeWidth, glowWidth, distToEdge.x);
                half cornerMaskY = 1.0 - smoothstep(_EdgeWidth, glowWidth, distToEdge.y);
                half cornerMask = cornerMaskX * cornerMaskY;

                half edgeIntensity = coreMask + glowMask * glowMask * _GlowIntensity + cornerMask * _CornerBoost;

                // 오빗 구간 — 둘레상 현재 픽셀 위치와 _OrbitPhase 사이의 원형 거리가 arc width 안이면 국부적으로 부스트
                half arcParam = ComputeArcParam(IN.uv, distToEdge);
                half arcDiff = abs(arcParam - _OrbitPhase);
                arcDiff = min(arcDiff, 1.0 - arcDiff);
                half orbitMask = (1.0 - smoothstep(0.0, _OrbitArcWidth, arcDiff)) * glowMask;
                edgeIntensity += orbitMask * _OrbitBoost * _OrbitEnabled;

                half alpha = saturate(_FaceAlpha + edgeIntensity);
                clip(alpha - 0.001);

                // 면 중심은 상태와 무관한 고정 톤(_FillColor), 모서리/오빗 구간만 상태색(_BaseColor)으로 전환
                // 밝기 부스트(_GlowEnabled)는 Reachable 셀에만 켜짐 — 그 외 셀은 원색(_BaseColor) 그대로 얇은 라인만 표시,
                // 부스트가 켜지는 경우에도 배율에 상한을 둬서(최대 2배) 채도가 흰색으로 뭉개지지 않게 함
                half3 edgeColor = _BaseColor.rgb * (1.0 + min(edgeIntensity, 1.0) * _GlowEnabled);
                half3 color = lerp(_FillColor.rgb, edgeColor, saturate(edgeIntensity));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
