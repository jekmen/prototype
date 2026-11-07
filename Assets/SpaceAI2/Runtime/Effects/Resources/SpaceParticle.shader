Shader "SpaceAI2/Effects/Particle"
{
    Properties { _Mode ("Glow / fire / smoke / ring", Float) = 0 }
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
            #include "UnityCG.cginc"
            float _Mode;
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float4 frag(v2f i):SV_Target
            {
                float2 p=i.uv*2-1; float r=length(p);
                float n=noise(p*5+_Time.y*0.65)*0.65+noise(p*13-_Time.y)*0.35;
                float a=pow(saturate(1-r),2);
                float3 c=i.color.rgb*2.5;
                if (_Mode>0.5 && _Mode<1.5) {
                    a=saturate((1-r)*2.5+n-0.65)*saturate((1-r)*4);
                    c=lerp(i.color.rgb*0.25,float3(1.5,0.65,0.08),saturate((1-r)*1.5+n-0.4));
                    a*=0.65;
                }
                if (_Mode>1.5 && _Mode<2.5) { a=pow(saturate(1-r),1.3)*(0.3+n)*0.22; c=i.color.rgb; }
                if (_Mode>2.5) { a=exp(-abs(r-0.78)*65)*saturate(1-r)*4; c=i.color.rgb*2; }
                return float4(c,a*i.color.a);
            }
            ENDCG
        }
    }
}
