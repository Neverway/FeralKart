Shader "Neverway/Resoulex Toon"
{
    // Properties Block (For defining variables)
    Properties
    {
        [Header(Stencil Settings)][Space]
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        [Enum(UnityEngine.Rendering.StencilOp)] _ZWrite ("ZWrite", Float) = 1
        
        [Header(Material Settings)][Space]
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
        [KeywordEnum(TruePBR, StylizedPBR)] _SpecularMode ("Specular Mode", Float) = 0
        _AlphaClip ("Alpha Clip", Range(0, 1)) = 0.5
        _Color ("Color", Color) = (1,1,1,1)

        [Header(Main Texture Properties)][Space]
        _RampSmoothness ("Ramp Smoothness", Range(0.1, 1.0)) = 0.1
        [NoScaleOffset] _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Roughness Power", Range(0.0, 1.0)) = 0.5
        [NoScaleOffset] _SpecGlossMap ("Roughness Map", 2D) = "white" {}
        _Metallic ("Metallic Power", Range(0.0, 1.0)) = 0.0
        [NoScaleOffset] _MetallicGlossMap ("Metallic Map", 2D) = "white" {}
        _BumpScale ("Normal Power", Range(0.0, 1.0)) = 1.0
        [NoScaleOffset][Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _Parallax ("Height Scale", Range(0, 0.08)) = 0
        [NoScaleOffset] _ParallaxMap ("Height Map", 2D) = "black" {}
        [NoScaleOffset] _OcclusionMap ("Occlusion", 2D) = "white" {}
        
        _SpecularStrength("Specular Strength", Range(0,1)) = 0.5
        
        _Tiling ("Tiling", Vector) = (1, 1, 0, 0)
        _Offset ("Offset", Vector) = (0, 0, 0, 0)

        [Header(Emission Properties)][Space]
        _EmissionDivision ("Emission Division", Range(1, 10)) = 1
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)
        [NoScaleOffset] _EmissionMap ("Emission", 2D) = "white" {}

        [Header(Detail Layer)][Space]
        _DetailAlbedoMap ("Detail Texture", 2D) = "black" {}
        _DetailProminence ("Detail Prominence", Range(0, 1)) = 0.2
        _DetailColor ("Detail Color", Color) = (0, 0, 0, 0)
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque"}
        LOD 200
        Cull [_CullMode]
        ZTest [_ZTest]
        ZWrite [_ZWrite]
        
        CGPROGRAM

        // Includes (Imported libraries & Shader type settings)
        #pragma surface surf Ramp fullforwardshadows addshadow
        #pragma multi_compile _SPECULARMODE_TRUEPBR _SPECULARMODE_STYLIZEDPBR
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling


        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "UnityStandardBRDF.cginc"
        #include "UnityStandardUtils.cginc"
        #include "SX_Helpers.cginc"

        // Input Struct (What Unity data to pass to Surface Function)
        struct Input
        {
            float2 uv_MainTex;
            float2 uv_DetailAlbedoMap;
            float2 uv_Normal;
            float3 viewDir;
            float3 worldPos;
            float4 screenPos;
            
            UNITY_VERTEX_INPUT_INSTANCE_ID  
        };

        // Surface Output (Material properties)
        half _RampSmoothness;

        float _AlphaClip;

        sampler2D _MainTex;

        half _Glossiness;
        sampler2D _SpecGlossMap;

        half _Metallic;
        sampler2D _MetallicGlossMap;

        half _BumpScale;
        sampler2D _BumpMap;

        half _Parallax;
        sampler2D _ParallaxMap;

        sampler2D _OcclusionMap;

        half _EmissionDivision;
        half4 _EmissionColor;
        sampler2D _EmissionMap;
        
        half _SpecularStrength;

        sampler2D _DetailAlbedoMap;
        half _DetailProminence;
        half3 _DetailColor;
        
        float2 _Tiling;
        float2 _Offset;

        fixed4 _Color;

        float _UseSlice;
        float _ColorOnly;

        float3 _SliceCenterOne;
        float3 _SliceCenterTwo;

        float3 _SliceNormalOne;
        float3 _SliceNormalTwo;

        struct SurfaceOutputToon
        {
            fixed3 Albedo;
            fixed3 Normal;
            fixed3 worldPos;
            fixed3 viewDir;
            half3 Emission;
            half Metallic;
            half Roughness;
            half Occlusion;
            fixed Alpha;
            fixed2 screenUv;
        };
        
        half3 BRDFToon(half3 diffColor, half3 specColor, half oneMinusReflectivity, half smoothness, float3 normal, float3 viewDir, UnityLight light, UnityIndirect gi)
        {
            float perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);
            float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);

            roughness = max(roughness, 0.002);

            float3 lightDirection = normalize(light.dir);
            float3 viewDirection = normalize(viewDir);
            float3 halfDirection = Unity_SafeNormalize(viewDirection + lightDirection);
            float3 lightReflectDirection = normalize(reflect(-lightDirection, normal));
            
            half preNdotL = DotClamped(normal, lightDirection);
            half NdotL = smoothstep(0, _RampSmoothness, preNdotL);
            half NdotH = DotClamped(normal, halfDirection);
            half NdotV = abs(dot(normal, viewDirection));
            half LdotH = DotClamped(lightDirection, halfDirection);

            half diffuseTerm = DisneyDiffuse(NdotV, NdotL, LdotH, perceptualRoughness) * NdotL;

            float specularTerm;
            half steps;

            #ifdef _SPECULARMODE_TRUEPBR
                // Changing this to linear scale ~Liz
                //steps = _RampSmoothness * _RampSmoothness * 100 * 64;
                steps = lerp(8, 128, _RampSmoothness);
    
                //steps = 64;
                
                //float rNdotL = round(NdotL * steps) / steps;
                //float rNdotV = round(NdotV * steps) / steps;
    
                float rNdotH = round(NdotH * steps) / steps;
                
                float V = SmithJointGGXVisibilityTerm(NdotL, NdotV, roughness);
                float D = GGXTerm(rNdotH, roughness);
                specularTerm = V * D * UNITY_PI;
                // This was already commented out //specularTerm = lerp(round(specularTerm * steps) / steps, specularTerm, 1 - smoothness);
            #elif _SPECULARMODE_STYLIZEDPBR
                steps = _RampSmoothness * _RampSmoothness * 300;
                specularTerm = round(pow(NdotH * NdotL, smoothness * 10) * steps) / steps;
            
                //float toonSpec = pow(NdotH, 32.0 * smoothness);
                //specularTerm = smoothstep(0.5, 0.6, toonSpec);
            #endif

            
            specularTerm *= _SpecularStrength;  
            specularTerm = max(0, specularTerm * NdotL);

            half surfaceReduction = 1.0 / (roughness * roughness + 1.0);

            specularTerm *= any(specColor) ? 1.0 : 0.0;

            half grazingTerm = saturate(smoothness + (1 - oneMinusReflectivity));
            half3 color = diffColor * (gi.diffuse + light.color * diffuseTerm)
                    + specularTerm * light.color * FresnelTerm(specColor, LdotH)
                    + surfaceReduction * gi.specular * FresnelLerp(specColor, grazingTerm, NdotV);

            return color;
        }

        // Not sure what this is
        inline void LightingRamp_GI(SurfaceOutputToon s, UnityGIInput data, inout UnityGI gi)
        {
            #if defined(UNITY_PASS_DEFERRED) && UNITY_ENABLE_REFLECTION_BUFFERS
                gi = UnityGlobalIllumination(data, s.Occlusion, s.Normal);
            #else
                Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(1 - s.Roughness, data.worldViewDir, s.Normal, lerp(unity_ColorSpaceDielectricSpec.rgb, s.Albedo, s.Metallic));
                gi = UnityGlobalIllumination(data, s.Occlusion, s.Normal, g);
            #endif
        }
        
        // Custom lighting model (Above we defined lighting model as "Ramp" so unity expects a func called Lighting<ModelName>)
        half4 LightingRamp(SurfaceOutputToon surface, float3 viewDir, UnityGI gi)
        {
            float3 normal = normalize(surface.Normal);
            float3 viewDirection = normalize(viewDir);
            half oneMinusReflectivity;
            half3 specColor;

            surface.Albedo = DiffuseAndSpecularFromMetallic(surface.Albedo, surface.Metallic, specColor, oneMinusReflectivity);

            half3 c = BRDFToon(surface.Albedo, specColor, oneMinusReflectivity, 1-surface.Roughness, normal, viewDirection, gi.light, gi.indirect);

            half4 emission = half4(surface.Emission + c, surface.Alpha);

            return emission;
        }

        // Surface Function (Where sampling and properties come together and output)
        void surf (Input IN, inout SurfaceOutputToon output)
        {
            UNITY_SETUP_INSTANCE_ID(IN);
            float2 uv = IN.uv_MainTex * _Tiling + _Offset;
            float2 parallaxOffset = ParallaxOffset (tex2D(_ParallaxMap, uv).r, _Parallax, IN.viewDir);

            uv += parallaxOffset;

            fixed4 col = tex2D(_MainTex, uv) * _Color;
            half4 detailCol = tex2D(_DetailAlbedoMap, IN.uv_DetailAlbedoMap);
            half detailMask = luminance(detailCol.rgb) * detailCol.a * _DetailProminence;

            output.viewDir = IN.viewDir;
            output.worldPos = IN.worldPos;
            output.screenUv = IN.screenPos.xy / IN.screenPos.w;

            output.Albedo = lerp(col.rgb, detailCol.rgb * _DetailColor, detailMask);

            if (_ColorOnly == 0)
            {
                output.Normal = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale);

                output.Metallic = tex2D(_MetallicGlossMap, uv).r * _Metallic;

                output.Roughness = tex2D(_SpecGlossMap, uv).r * _Glossiness;

                output.Occlusion = tex2D(_OcclusionMap, uv).r;

                output.Emission = tex2D(_EmissionMap, uv) * _EmissionColor/_EmissionDivision;

                output.Alpha = col.a;
            }
            else
            {
                output.Roughness = tex2D(_SpecGlossMap, uv).r * _Glossiness;
            }

            clip(col.a - _AlphaClip);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
