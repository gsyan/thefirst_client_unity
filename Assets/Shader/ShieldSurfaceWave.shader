Shader "SpaceFleet/ShieldSurfaceWave"
{
    Properties
    {
        _WaveSpeed ("Wave Speed", Range(0.1, 50)) = 8.0
        _SpeedDecay ("Speed Decay", Range(0, 30)) = 0.0
        _TrailMinLifetime ("Trail Min Lifetime", Range(0.1, 10)) = 1.0
        _TrailMaxLifetime ("Trail Max Lifetime", Range(0.1, 10)) = 3.0
        _NoiseScale ("Trail Patch Scale", Range(0.1, 10)) = 1.5

        [Header(Wave Distortion)]
        _DistortionAmount ("Distortion Amount", Range(0, 5)) = 0.5
        _DistortionScale ("Distortion Scale", Range(0.1, 10)) = 2.0

        [Header(Rim Light)]
        [Toggle] _UseRimLight ("Use Rim Light", Float) = 1
        _Color ("Color", Color) = (0.6, 0.9, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 10)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ShieldSurfaceWave"
            Tags { "LightMode"="UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 positionCS : SV_POSITION;
            };

            #define SHIELD_WAVE_SLOT_COUNT 4

            CBUFFER_START(UnityPerMaterial)
                // xyz = 피격 지점(오브젝트 스페이스), w = 피격 시각 — 슬롯 4개로 동시/연속 피격을 함께 표시(링버퍼, C#에서 가장 오래된 슬롯부터 덮어씀)
                float4 _HitData[SHIELD_WAVE_SLOT_COUNT];
                float _WaveSpeed;
                float _SpeedDecay;
                float _TrailMinLifetime;
                float _TrailMaxLifetime;
                float _NoiseScale;
                float _DistortionAmount;
                float _DistortionScale;
                float _UseRimLight;
                float4 _Color;
                float _RimPower;
                float _RimIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            // 순수 해시(픽셀마다 뚝뚝 끊기는 값) — value noise의 격자점 값으로만 사용
            float hash3(float3 p)
            {
                return frac(sin(dot(p, float3(12.9898, 78.233, 45.164))) * 43758.5453123);
            }

            // 격자점 8개를 삼선형 보간한 부드러운 3D 노이즈 — 인접 좌표끼리 값이 연속적으로 이어져 지글거림 없이 뭉게구름 같은 패치를 만듦
            float noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep 보간

                float n000 = hash3(i + float3(0, 0, 0));
                float n100 = hash3(i + float3(1, 0, 0));
                float n010 = hash3(i + float3(0, 1, 0));
                float n110 = hash3(i + float3(1, 1, 0));
                float n001 = hash3(i + float3(0, 0, 1));
                float n101 = hash3(i + float3(1, 0, 1));
                float n011 = hash3(i + float3(0, 1, 1));
                float n111 = hash3(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            float4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                // 이 픽셀 고유의 무작위 소멸 지속시간 — 슬롯이 달라도 같은 좌표는 항상 같은 값을 씀(트레일끼리 일관성 유지)
                // _NoiseScale로 패치 크기 조절 — 값이 작을수록 노이즈 격자가 넓어져(1개 패치가 큼) 부드럽고 큼직한 얼룩, 크면 잘게 쪼개진 패치
                float randomLifetime = lerp(_TrailMinLifetime, _TrailMaxLifetime, noise3(input.positionOS * _NoiseScale));

                // 슬롯마다 독립적으로 트레일을 계산해 합산 — 동시/연속 피격이 겹쳐도 각자의 흔적이 함께 보임
                float totalMask = 0.0;
                UNITY_UNROLL
                for (int i = 0; i < SHIELD_WAVE_SLOT_COUNT; i++)
                {
                    float3 hitPositionOS = _HitData[i].xyz;
                    float hitTime = _HitData[i].w;

                    // 피격 지점(오브젝트 스페이스)으로부터의 유클리드 거리 — 지오데식 돔 표면 위 등거리선 근사. 도달 한계 없이 실드 표면 끝까지 퍼짐
                    float dist = distance(input.positionOS, hitPositionOS);

                    // 완벽한 구형 파동면은 인공적으로 보여서, 위치 기반 노이즈로 실효 거리를 방향마다 살짝 흔들어 파동 앞머리가 울퉁불퉁한 경계로 보이게 함
                    float distortion = (noise3(input.positionOS * _DistortionScale) - 0.5) * 2.0 * _DistortionAmount;
                    dist = max(0.0, dist + distortion);

                    // 지수 감속: radius(t) = (_WaveSpeed/_SpeedDecay) * (1 - e^(-_SpeedDecay*t)) — 처음엔 _WaveSpeed로 빠르게 퍼지다 점점 느려짐.
                    // arrivalTime은 이 함수의 역함수(거리→도달시각). _SpeedDecay가 0에 가까우면 기존 등속(dist/_WaveSpeed)으로 수렴하도록 아주 작은 값으로 클램프해 0나눗셈 방지
                    float decay = max(_SpeedDecay, 0.0001);
                    float maxReachableDist = _WaveSpeed / decay; // 이 거리를 넘으면 파동이 영원히 도달하지 못함(감속으로 수렴)
                    float distRatio = saturate(dist / maxReachableDist);
                    float arrivalTime = hitTime + (-log(1.0 - distRatio) / decay);
                    if (dist >= maxReachableDist)
                        arrivalTime = 1e9; // 수렴 거리 밖은 영원히 도달 못 함

                    // 파동이 아직 도달 전이면 mask 0
                    float painted = step(0.0, _Time.y - arrivalTime);

                    // 이 픽셀에 파동이 "도달한 순간"의 속도 비율(0~1) — v(t) = _WaveSpeed * e^(-decay*t)를 _WaveSpeed로 정규화한 값.
                    // 파동이 빠르게 지나간(초반) 자리는 진하게, 느려진 뒤(후반) 도달한 자리는 옅게 칠해져 자연히 투명해지며 잦아드는 느낌을 줌.
                    // 도달 못한 픽셀(arrivalTime=1e9)은 exp가 사실상 0이 되어 어차피 painted=0과 함께 무해함
                    float speedRatio = exp(-decay * (arrivalTime - hitTime));

                    // 수명은 "피격 시각(hitTime)" 기준으로 통일 — 도달 시각 기준으로 하면 먼저 도달한 피격 지점이 항상 먼저 소멸해버려
                    // 파동 앞머리만 남고 정작 맞은 자리가 가장 먼저 비는 문제가 생김. hitTime 기준이면 모든 픽셀이 같은 출발선에서 경쟁해 진짜 무작위 소멸이 됨
                    float age = _Time.y - hitTime;
                    float notExpired = step(age, randomLifetime);

                    totalMask += painted * notExpired * speedRatio;
                }

                // 트레일이 퍼진 영역(clampedMask)의 경계를 프레넬 림라이트로만 표현 — 표면을 색으로 채우지 않고 가장자리만 은은하게 드러냄
                float clampedMask = saturate(totalMask);

                // Cull Off라 뒷면(실드 안쪽)도 그려지는데, 뒷면은 노멀 방향이 화면 기준 반대라 그대로 두면 NdotV가 왜곡돼 뒷면까지 밝게 겹쳐 보임 — 뒷면이면 노멀을 뒤집어 보정
                float3 shadingNormalWS = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
                float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
                float NdotV = saturate(dot(shadingNormalWS, viewDirWS));
                float rim = pow(1.0 - NdotV, _RimPower);
                float3 finalColor = _Color.rgb * rim * _RimIntensity * clampedMask * _UseRimLight;

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
