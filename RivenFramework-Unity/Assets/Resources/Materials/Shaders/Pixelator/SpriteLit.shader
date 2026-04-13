Shader "Custom/SpriteLit"
{
    Properties
    {
        _MainTex    ("Sprite Texture", 2D) = "white" {}
        _Color      ("Color Tint", Color) = (1,1,1,1)
        _Cutoff     ("Alpha Cutoff", Range(0,1)) = 0.5

        _TilingX ("Tiling X", Float) = 1
        _TilingY ("Tiling Y", Float) = 1
        _OffsetX ("Offset X", Float) = 0
        _OffsetY ("Offset Y", Float) = 0

        _Metallic   ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0

        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionStrength ("Emission Strength", Float) = 1.0
        [Toggle] _UseEmission ("Enable Emission", Float) = 0

        [Toggle] _SpecularHighlights ("Specular Highlights", Float) = 1
        [Toggle] _GlossyReflections  ("Reflections", Float) = 1
        [Toggle] _TwoSided           ("Two Sided", Float) = 0

        [Enum(Opaque,0,Cutout,1,Transparent,2)]
        _Mode ("Blend Mode", Float) = 0

        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _ZWrite   ("__zw",  Float) = 1.0
        [HideInInspector] _Cull     ("__cull", Float) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        // Front face pass
        Cull Back
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:premul vertex:vert
        #pragma shader_feature _ALPHABLEND_ON
        #pragma shader_feature _ALPHATEST_ON
        #pragma shader_feature _EMISSION
        #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
        #pragma shader_feature _GLOSSYREFLECTIONS_OFF
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _Cutoff;
        float _TilingX;
        float _TilingY;
        float _OffsetX;
        float _OffsetY;
        float _Metallic;
        float _Glossiness;
        fixed4 _EmissionColor;
        float  _EmissionStrength;
        float  _UseEmission;
        float  _SpecularHighlights;
        float  _GlossyReflections;

        struct Input
        {
            float2 uv_MainTex;
        };

        void vert(inout appdata_full v)
        {
            // Flip the vertex normal so the lighting system treats this face as front-facing
            v.normal = -v.normal;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex * float2(_TilingX, _TilingY) + float2(_OffsetX, _OffsetY);
            fixed4 c = tex2D(_MainTex, uv) * _Color;

            #if defined(_ALPHATEST_ON)
                clip(c.a - _Cutoff);
                o.Albedo = c.rgb;
                o.Alpha  = 1.0;
            #elif defined(_ALPHABLEND_ON)
                o.Albedo = c.rgb;
                o.Alpha  = c.a;
            #else
                o.Albedo = c.rgb;
                o.Alpha  = 1.0;
            #endif

            o.Metallic   = _Metallic;
            o.Smoothness = _Glossiness;

            if (_UseEmission > 0.5)
                o.Emission = c.rgb * _EmissionColor.rgb * _EmissionStrength;

            if (_SpecularHighlights < 0.5)
            {
                o.Smoothness = 0;
                o.Metallic   = 0;
            }

            if (_GlossyReflections < 0.5)
                o.Occlusion = 0;
        }
        ENDCG

        // Back face pass — only renders when Two Sided is on (_Cull == 0)
        Cull Front
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:premul
        #pragma shader_feature _ALPHABLEND_ON
        #pragma shader_feature _ALPHATEST_ON
        #pragma shader_feature _EMISSION
        #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
        #pragma shader_feature _GLOSSYREFLECTIONS_OFF
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _Cutoff;
        float _TilingX;
        float _TilingY;
        float _OffsetX;
        float _OffsetY;
        float _Metallic;
        float _Glossiness;
        fixed4 _EmissionColor;
        float  _EmissionStrength;
        float  _UseEmission;
        float  _SpecularHighlights;
        float  _GlossyReflections;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex * float2(_TilingX, _TilingY) + float2(_OffsetX, _OffsetY);
            fixed4 c = tex2D(_MainTex, uv) * _Color;

            #if defined(_ALPHATEST_ON)
                clip(c.a - _Cutoff);
                o.Albedo = c.rgb;
                o.Alpha  = 1.0;
            #elif defined(_ALPHABLEND_ON)
                o.Albedo = c.rgb;
                o.Alpha  = c.a;
            #else
                o.Albedo = c.rgb;
                o.Alpha  = 1.0;
            #endif

            // Flip normal so back face is lit correctly
            o.Normal     = float3(0, 0, -1);
            o.Metallic   = _Metallic;
            o.Smoothness = _Glossiness;

            if (_UseEmission > 0.5)
                o.Emission = c.rgb * _EmissionColor.rgb * _EmissionStrength;

            if (_SpecularHighlights < 0.5)
            {
                o.Smoothness = 0;
                o.Metallic   = 0;
            }

            if (_GlossyReflections < 0.5)
                o.Occlusion = 0;
        }
        ENDCG

        // Explicit shadow caster pass that respects alpha cutout on both faces
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _ALPHATEST_ON
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Cutoff;
            float _TilingX;
            float _TilingY;
            float _OffsetX;
            float _OffsetY;

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv * float2(_TilingX, _TilingY) + float2(_OffsetX, _OffsetY);
                fixed4 c = tex2D(_MainTex, uv) * _Color;
                #if defined(_ALPHATEST_ON)
                    clip(c.a - _Cutoff);
                #endif
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    CustomEditor "SpriteLitGUI"
    FallBack "Diffuse"
}