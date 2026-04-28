Shader "Custom/SkyboxStarsSingle"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Star Texture", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.5, 0.5, 0.5, 0.5)
        _Exposure ("Exposure", Range(0, 8)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
        _BlackPoint ("Black Point", Range(0, 0.4)) = 0.1
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    half4 _Tint;
    half _Exposure;
    float _Rotation;
    half _BlackPoint;

    float4 RotateAroundYInDegrees(float4 vertex, float degrees)
    {
        float alpha = degrees * UNITY_PI / 180.0;
        float sina, cosa;
        sincos(alpha, sina, cosa);
        float2x2 m = float2x2(cosa, -sina, sina, cosa);
        return float4(mul(m, vertex.xz), vertex.yw).xzyw;
    }

    struct appdata_t
    {
        float4 vertex : POSITION;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f
    {
        float4 vertex : SV_POSITION;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    v2f vert(appdata_t v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.vertex = UnityObjectToClipPos(RotateAroundYInDegrees(v.vertex, _Rotation));
        o.texcoord = v.texcoord;
        return o;
    }

    half4 SampleStars(float2 uv, float2 offset)
    {
        half4 col = tex2D(_MainTex, frac(uv + offset));
        // 배경 그라디언트를 순수 검정으로 → 면 경계선 소멸
        col.rgb = max(0, col.rgb - _BlackPoint);
        col.rgb *= _Tint.rgb * _Tint.a * _Exposure * unity_ColorSpaceDouble.rgb;
        return col;
    }
    ENDCG

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        // Front  +Z
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return SampleStars(i.texcoord, float2(0.00, 0.00)); }
            ENDCG
        }
        // Back   -Z
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return SampleStars(i.texcoord, float2(0.37, 0.61)); }
            ENDCG
        }
        // Left   +X
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return SampleStars(i.texcoord, float2(0.71, 0.23)); }
            ENDCG
        }
        // Right  -X
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return SampleStars(i.texcoord, float2(0.13, 0.84)); }
            ENDCG
        }
        // Up     +Y
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return SampleStars(i.texcoord, float2(0.55, 0.47)); }
            ENDCG
        }
        // Down   -Y
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return SampleStars(i.texcoord, float2(0.82, 0.19)); }
            ENDCG
        }
    }
}
