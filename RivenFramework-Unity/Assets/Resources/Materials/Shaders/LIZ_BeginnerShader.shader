// Title (The name and menu the shader appears as)
Shader "Custom/LIZ_BeginnerShader"
{
    // Properties Block (For defining variables)
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _RampSmoothness ("Ramp Smoothness", Float) = 0.1
    }
    
    // SubShader Block (A sub-shader (You can have multiple))
    SubShader
    {
         // Unity render output type (can be Opaque, Transparent, or Cutout)
        Tags { "RenderType"="Opaque" }
        // Level of detail (for creating multiple sub-shaders)
        LOD 200
        // Start of the GPU code (ends at ENDCG)
        CGPROGRAM

        // Includes (Imported libraries)
        #pragma surface surf Ramp // Defines the lighting model to use (in this case it's a custo, "Ramp" model defined below)
        #pragma target 3.0

        // Input Struct (What Unity data to pass to Surface Function)
        struct Input
        {
            float2 uv_MainTex;
        };

        // Surface Output (Material properties)
        sampler2D _MainTex;
        fixed4 _Color;
        half _RampSmoothness;

        // Custom lighting model (Above we defined lighting model as "Ramp" so unity expects a func called Lighting<ModelName>)
        half4 LightingRamp(SurfaceOutput output, half3 lightDirection, half atten)
        {
            // Combines the dot product between the light direction and the normals ot get basic lighting
            half normalDotLight = saturate(dot(output.Normal, lightDirection));

            // Step function for toon shading the dot light with the ramp smoothness
            half ramp = smoothstep(0.5 - _RampSmoothness, 0.5 + _RampSmoothness, normalDotLight);

            // Combined color of lights, the ramp, and some attenuation value (that I don't know where it comes from)
            half3 color = output.Albedo * _LightColor0.rgb * ramp * atten;

            // Output the result with color and alpha channel
            return half4(color, output.Alpha);
        }

        // Instance batching boilerplate
        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)

        // Surface Function (Where sampling and properties come together and output)
        void surf (Input input, inout SurfaceOutput output)
        {
            fixed4 color = tex2D(_MainTex, input.uv_MainTex) * _Color;
            output.Albedo = color.rgb;
            output.Alpha = color.a;
            output.Specular = _RampSmoothness;
        }
        ENDCG
    }
}
