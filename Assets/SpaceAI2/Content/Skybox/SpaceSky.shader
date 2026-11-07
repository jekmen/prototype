Shader "SpaceAI2/Skybox/Space"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Nebula (equirectangular)", 2D) = "black" {}
        _NebulaTint ("Nebula Tint", Color) = (1, 1, 1, 1)
        _NebulaExposure ("Nebula Exposure", Range(0, 4)) = 1.6
        _StarDensity ("Star Density", Range(0, 1)) = 0.5
        _StarBrightness ("Star Brightness", Range(0, 8)) = 2.5
        _StarSize ("Star Size (pixels)", Range(0.3, 3)) = 0.8
        _Rotation ("Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _NebulaTint;
            half _NebulaExposure;
            half _StarDensity;
            half _StarBrightness;
            half _StarSize;
            float _Rotation;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                float s, c;
                sincos(radians(_Rotation), s, c);
                float3 p = v.vertex.xyz;
                o.dir = float3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            // Hash without sine (Dave Hoskins): stable on GPUs where sin() loses precision.
            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            // One star per occupied cell of a 3D grid; the size is set in screen pixels so stars stay sharp at any resolution.
            half3 StarLayer(float3 dir, float scale, half density, half brightness, float pixel)
            {
                float3 cell = floor(dir * scale);
                float3 h = Hash33(cell);
                if (h.x > density) return 0;
                float3 star = normalize(cell + 0.25 + 0.5 * Hash33(cell + 17.13));
                float3 offset = dir - star;
                float sigma = pixel * _StarSize;
                half glow = exp(-dot(offset, offset) / (2.0 * sigma * sigma));
                half intensity = brightness * pow(h.y, 10.0);
                half3 tint = lerp(half3(0.7, 0.82, 1.0), half3(1.0, 0.78, 0.55), h.z * h.z);
                return tint * glow * intensity;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float2 uv = float2(atan2(dir.x, dir.z) / (2.0 * UNITY_PI) + 0.5, asin(dir.y) / UNITY_PI + 0.5);
                half3 color = tex2Dlod(_MainTex, float4(uv, 0, 0)).rgb * _NebulaTint.rgb * _NebulaExposure;

                float pixel = max(length(fwidth(dir)), 1e-5);
                color += StarLayer(dir, 50.0, _StarDensity * 0.12, _StarBrightness * 3.0, pixel);
                color += StarLayer(dir, 120.0, _StarDensity * 0.08, _StarBrightness, pixel);
                color += StarLayer(dir, 280.0, _StarDensity * 0.06, _StarBrightness * 0.5, pixel);
                return half4(color, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
