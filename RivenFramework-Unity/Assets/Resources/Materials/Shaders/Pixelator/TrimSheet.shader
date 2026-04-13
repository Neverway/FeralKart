Shader "Custom/TrimSheet"
{
    Properties
    {
        _MainTex ("Tileset", 2D) = "white" {}
        
        _CropX      ("Crop X (pixels)", Float) = 0
        _CropY      ("Crop Y (pixels)", Float) = 0
        _CropWidth  ("Crop Width (pixels)", Float) = 64
        _CropHeight ("Crop Height (pixels)", Float) = 64
        
        _TexWidth   ("Texture Width (pixels)", Float) = 512
        _TexHeight  ("Texture Height (pixels)", Float) = 512
        
        _TexelsPerUnit ("Texels Per Unit", Float) = 64

        _TilingX ("Tiling X", Float) = 1
        _TilingY ("Tiling Y", Float) = 1
        _OffsetX ("Offset X", Float) = 0
        _OffsetY ("Offset Y", Float) = 0
        
        _Metallic   ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0
        _Color      ("Color Tint", Color) = (1,1,1,1)
        _Cutoff     ("Alpha Cutoff", Range(0,1)) = 0.5

        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionStrength ("Emission Strength", Float) = 1.0
        [Toggle] _UseEmission ("Enable Emission", Float) = 0

        [Toggle] _SpecularHighlights ("Specular Highlights", Float) = 1
        [Toggle] _GlossyReflections  ("Reflections", Float) = 1
        
        [Enum(Opaque,0,Cutout,1,Transparent,2)]
        _Mode ("Blend Mode", Float) = 0
        
        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _ZWrite   ("__zw",  Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        
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

        float _CropX;
        float _CropY;
        float _CropWidth;
        float _CropHeight;
        float _TexWidth;
        float _TexHeight;
        float _TexelsPerUnit;

        float _TilingX;
        float _TilingY;
        float _OffsetX;
        float _OffsetY;

        float _Metallic;
        float _Glossiness;
        fixed4 _Color;
        float _Cutoff;
        float _Mode;

        fixed4 _EmissionColor;
        float  _EmissionStrength;
        float  _UseEmission;
        float  _SpecularHighlights;
        float  _GlossyReflections;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        fixed4 SampleCrop(float2 uv)
        {
            float u0 = _CropX      / _TexWidth;
            float v0 = _CropY      / _TexHeight;
            float uw = _CropWidth  / _TexWidth;
            float vh = _CropHeight / _TexHeight;

            float2 tiledUV = uv * float2(_TilingX, _TilingY) + float2(_OffsetX, _OffsetY);
            float2 cropped = float2(u0, v0) + frac(tiledUV) * float2(uw, vh);
            return tex2D(_MainTex, cropped);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float scale = 1.0 / _TexelsPerUnit;
            float3 scaledPos = IN.worldPos / scale;

            float3 blend = abs(IN.worldNormal);
            blend = pow(blend, 8);
            blend /= (blend.x + blend.y + blend.z);

            fixed4 cx = SampleCrop(scaledPos.zy);
            fixed4 cy = SampleCrop(scaledPos.xz);
            fixed4 cz = SampleCrop(scaledPos.xy);

            fixed4 c = (cx * blend.x + cy * blend.y + cz * blend.z) * _Color;

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
    }

    CustomEditor "TrimSheetGUI"
    FallBack "Diffuse"
}