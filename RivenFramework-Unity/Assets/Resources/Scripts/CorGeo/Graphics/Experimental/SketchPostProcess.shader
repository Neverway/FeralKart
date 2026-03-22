Shader "Custom/SketchPostProcess"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _CameraDepthNormalsTexture ("DepthNormals", 2D) = "black" {}

        _EdgeStrength ("Edge Strength", Float) = 1.0
        _PosterizeLevels ("Posterize Levels", Int) = 4

        _HatchTex ("Hatch Texture", 2D) = "white" {}
        _HatchStrength ("Hatch Strength", Float) = 0.5
        _HatchScale ("Hatch Scale", Float) = 4.0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

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
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _CameraDepthNormalsTexture;
            sampler2D _HatchTex;

            float _EdgeStrength;
            int _PosterizeLevels;
            float _HatchStrength;
            float _HatchScale;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float getLum(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            // Sobel filter for edge detection
            float sobel(sampler2D tex, float2 uv, float2 texel)
            {
                float3 tl = tex2D(tex, uv + texel * float2(-1,  1));
                float3  l = tex2D(tex, uv + texel * float2(-1,  0));
                float3 bl = tex2D(tex, uv + texel * float2(-1, -1));

                float3  t = tex2D(tex, uv + texel * float2( 0,  1));
                float3  b = tex2D(tex, uv + texel * float2( 0, -1));

                float3 tr = tex2D(tex, uv + texel * float2( 1,  1));
                float3  r = tex2D(tex, uv + texel * float2( 1,  0));
                float3 br = tex2D(tex, uv + texel * float2( 1, -1));

                float3 gx = (-tl - 2*l - bl) + (tr + 2*r + br);
                float3 gy = (tl + 2*t + tr) - (bl + 2*b + br);

                return length(gx + gy);
            }

            // Posterization
            float3 posterize(float3 color, int levels)
            {
                return floor(color * levels) / levels;
            }

            float3 hatch(float2 uv, float intensity)
            {
                // Higher intensity = more hatching
                float2 hatUV = uv * (_HatchScale * (1.0 + intensity * 5.0));
                float h = tex2D(_HatchTex, hatUV).r;
                return h.xxx;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 texel = float2(1.0/_ScreenParams.x, 1.0/_ScreenParams.y);

                float4 col = tex2D(_MainTex, i.uv);

                // Depth-normals texture for edges
                float4 dn = tex2D(_CameraDepthNormalsTexture, i.uv);

                float edge = sobel(_CameraDepthNormalsTexture, i.uv, texel) * _EdgeStrength;

                // Posterization
                float3 post = posterize(col.rgb, _PosterizeLevels);

                // Hatching based on luminance
                float lum = getLum(post);
                float3 hat = hatch(i.uv, 1.0 - lum) * _HatchStrength;

                // Combine everything
                float3 finalColor = post - edge.xxx + hat;

                return float4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}
