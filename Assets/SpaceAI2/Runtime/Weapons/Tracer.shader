Shader "SpaceAI2/Tracer"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.75, 0.3, 1)
        _Core ("Core Width (of tracer width)", Range(0.05, 1)) = 0.3
        _Glow ("Glow Width (of tracer width)", Range(0.5, 6)) = 2.5
        _GlowStrength ("Glow Strength", Range(0, 2)) = 0.7
        _CoreWhiteness ("Core Whiteness", Range(0, 1)) = 0.75
        _TailFade ("Tail Brightness", Range(0, 1)) = 0.2
        _HeadFlare ("Head Flare", Range(0, 2)) = 0.6
        _MinPixels ("Min Width (screen fraction per meter of distance)", Range(0, 0.01)) = 0.0015
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float4 _Color;
            half _Core, _Glow, _GlowStrength, _CoreWhiteness, _TailFade, _HeadFlare, _MinPixels;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 local : TEXCOORD0;   // meters: across, along (from the bolt's center)
                float4 shape : TEXCOORD1;   // half length, core radius, glow radius, fade
            };

            // The instance matrix carries the bolt: translation = center, z axis = direction * length, x axis = width.
            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 center = unity_ObjectToWorld._m03_m13_m23;
                float3 axis = unity_ObjectToWorld._m02_m12_m22;
                float length_ = max(length(axis), 1e-4);
                float3 dir = axis / length_;
                float width = length(unity_ObjectToWorld._m00_m10_m20);

                float3 toCamera = _WorldSpaceCameraPos - center;
                float distance_ = length(toCamera);
                float3 view = toCamera / max(distance_, 1e-4);

                // The bolt is drawn as its silhouette in the view plane: seen end-on it shrinks to a round glow
                // instead of a quad turned edge-on.
                float3 projected = dir - dot(dir, view) * view;
                float shrink = length(projected);
                float3 along = shrink > 1e-4 ? projected / shrink : normalize(cross(view, float3(0.01, 1, 0)));
                float3 side = cross(along, view);
                float halfLength = length_ * 0.5 * shrink;

                float minimum = distance_ * _MinPixels;
                float core = max(width * _Core, minimum * 0.35);
                float glow = max(width * _Glow, minimum);
                float fade = saturate(width * _Glow / glow);

                float across = v.vertex.x * 2.0 * glow;
                float lengthwise = v.vertex.z * (2.0 * halfLength + 2.0 * glow);
                float3 world = center + side * across + along * lengthwise;

                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1));
                o.local = float2(across, lengthwise);
                o.shape = float4(halfLength, core, glow, fade);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float halfLength = i.shape.x;
                float along = clamp(i.local.y, -halfLength, halfLength);
                float d = length(float2(i.local.x, i.local.y - along));
                float t = halfLength > 1e-3 ? (along + halfLength) / (2.0 * halfLength) : 1;
                half taper = lerp(_TailFade, 1.0, t * t);

                half core = exp(-d * d / (i.shape.y * i.shape.y));
                half edge = saturate(1.0 - d / i.shape.z);
                half glow = exp(-3.0 * d / i.shape.z) * edge * _GlowStrength;
                half3 coreColor = lerp(_Color.rgb, half3(1, 1, 1), _CoreWhiteness);
                float2 toHead = float2(i.local.x, i.local.y - halfLength);
                half head = exp(-dot(toHead, toHead) / (i.shape.z * i.shape.z * 0.3)) * edge * edge * _HeadFlare;
                half3 color = (_Color.rgb * (glow * taper + head) + coreColor * core * taper) * lerp(0.4, 1.0, i.shape.w);
                return half4(color, 1);
            }
            ENDCG
        }
    }
}
