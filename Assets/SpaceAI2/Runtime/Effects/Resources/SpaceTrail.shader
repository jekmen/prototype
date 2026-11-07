Shader "SpaceAI2/Effects/Trail"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; return o; }
            float4 frag(v2f i):SV_Target {
                float crossSection=pow(saturate(1-abs(i.uv.y*2-1)),2);
                float turbulence=0.75+0.25*sin(i.uv.x*65+sin(i.uv.x*19)*3-_Time.y*4);
                return float4(i.color.rgb*1.4,crossSection*turbulence*i.color.a);
            }
            ENDCG
        }
    }
}
