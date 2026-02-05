Shader "SpaceFleet/SkyboxBlend6Sided"
{
    Properties
    {
        _Blend ("Blend", Range(0, 1)) = 0
        _TintA ("Tint A", Color) = (0.5, 0.5, 0.5, 0.5)
        _TintB ("Tint B", Color) = (0.5, 0.5, 0.5, 0.5)
        _ExposureA ("Exposure A", Range(0, 8)) = 1
        _ExposureB ("Exposure B", Range(0, 8)) = 1

        [NoScaleOffset] _FrontTexA ("Front A [+Z]", 2D) = "grey" {}
        [NoScaleOffset] _BackTexA ("Back A [-Z]", 2D) = "grey" {}
        [NoScaleOffset] _LeftTexA ("Left A [+X]", 2D) = "grey" {}
        [NoScaleOffset] _RightTexA ("Right A [-X]", 2D) = "grey" {}
        [NoScaleOffset] _UpTexA ("Up A [+Y]", 2D) = "grey" {}
        [NoScaleOffset] _DownTexA ("Down A [-Y]", 2D) = "grey" {}

        [NoScaleOffset] _FrontTexB ("Front B [+Z]", 2D) = "grey" {}
        [NoScaleOffset] _BackTexB ("Back B [-Z]", 2D) = "grey" {}
        [NoScaleOffset] _LeftTexB ("Left B [+X]", 2D) = "grey" {}
        [NoScaleOffset] _RightTexB ("Right B [-X]", 2D) = "grey" {}
        [NoScaleOffset] _UpTexB ("Up B [+Y]", 2D) = "grey" {}
        [NoScaleOffset] _DownTexB ("Down B [-Y]", 2D) = "grey" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    float _Blend;
    half4 _TintA;
    half4 _TintB;
    half _ExposureA;
    half _ExposureB;

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
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = v.texcoord;
        return o;
    }

    half4 skybox_frag(v2f i, sampler2D texA, sampler2D texB)
    {
        half4 colA = tex2D(texA, i.texcoord);
        half4 colB = tex2D(texB, i.texcoord);

        colA.rgb = colA.rgb * _TintA.rgb * _TintA.a * _ExposureA * unity_ColorSpaceDouble.rgb;
        colB.rgb = colB.rgb * _TintB.rgb * _TintB.a * _ExposureB * unity_ColorSpaceDouble.rgb;

        return lerp(colA, colB, _Blend);
    }
    ENDCG

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            sampler2D _FrontTexA;
            sampler2D _FrontTexB;
            half4 frag(v2f i) : SV_Target { return skybox_frag(i, _FrontTexA, _FrontTexB); }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            sampler2D _BackTexA;
            sampler2D _BackTexB;
            half4 frag(v2f i) : SV_Target { return skybox_frag(i, _BackTexA, _BackTexB); }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            sampler2D _LeftTexA;
            sampler2D _LeftTexB;
            half4 frag(v2f i) : SV_Target { return skybox_frag(i, _LeftTexA, _LeftTexB); }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            sampler2D _RightTexA;
            sampler2D _RightTexB;
            half4 frag(v2f i) : SV_Target { return skybox_frag(i, _RightTexA, _RightTexB); }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            sampler2D _UpTexA;
            sampler2D _UpTexB;
            half4 frag(v2f i) : SV_Target { return skybox_frag(i, _UpTexA, _UpTexB); }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            sampler2D _DownTexA;
            sampler2D _DownTexB;
            half4 frag(v2f i) : SV_Target { return skybox_frag(i, _DownTexA, _DownTexB); }
            ENDCG
        }
    }
}
