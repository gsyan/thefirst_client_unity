Shader "UI/TutorialMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Dim Color", Color) = (0, 0, 0, 0.7)
        _HoleCenter ("Hole Center", Vector) = (0.5, 0.5, 0, 0)
        _HoleSize ("Hole Size", Vector) = (0.2, 0.2, 0, 0)
        _EdgeSoftness ("Edge Softness", Float) = 0.01
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _HoleCenter; // xy: center (screen space 0-1)
            float4 _HoleSize;   // xy: half size (screen space 0-1)
            float _EdgeSoftness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 스크린 좌표 (0-1)
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // 구멍 영역 계산 (사각형)
                float2 dist = abs(screenUV - _HoleCenter.xy) - _HoleSize.xy;
                float hole = max(dist.x, dist.y);

                // 부드러운 엣지
                float alpha = smoothstep(-_EdgeSoftness, _EdgeSoftness, hole);

                fixed4 col = _Color;
                col.a *= alpha;

                return col;
            }
            ENDCG
        }
    }
}
