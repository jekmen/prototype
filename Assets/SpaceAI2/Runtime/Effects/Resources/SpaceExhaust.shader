Shader "SpaceAI2/Effects/Exhaust"
{
    Properties { [HDR] _Color ("Color", Color) = (0.08,0.55,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            float4 _Color;
            struct appdata { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos:SV_POSITION; float3 local:TEXCOORD0; };
            v2f vert(appdata v) { UNITY_SETUP_INSTANCE_ID(v); v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.local=v.vertex.xyz*2; return o; }
            float4 frag(v2f i):SV_Target {
                float tail=saturate(0.5-i.local.z*0.5);
                float radial=length(i.local.xy);
                float bands=0.7+0.3*cos(i.local.z*38+_Time.y*35);
                float a=pow(saturate(1-radial),0.65)*pow(tail,0.6);
                float core=pow(saturate(1-radial*1.8),3)*tail;
                return float4(lerp(_Color.rgb*2.5,float3(4,4.5,5),core)*bands,a);
            }
            ENDCG
        }
    }
}
