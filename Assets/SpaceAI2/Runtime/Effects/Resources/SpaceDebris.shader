Shader "SpaceAI2/Effects/Debris"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float4 color:COLOR; };
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal); o.color=v.color; return o; }
            float4 frag(v2f i):SV_Target {
                float light=0.25+0.75*saturate(dot(normalize(i.normal),normalize(float3(-0.3,0.7,-0.6))));
                return float4(float3(0.12,0.14,0.17)*light+i.color.rgb*2.3,i.color.a);
            }
            ENDCG
        }
    }
}
