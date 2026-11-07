Shader "Hidden/SpaceAI2/Bloom"
{
    Properties { _MainTex ("Source", 2D) = "black" {} _Bloom ("Bloom", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _Bloom;
        float4 _MainTex_TexelSize;
        float _Intensity;
        half4 blur(v2f_img i) {
            float2 d=_MainTex_TexelSize.xy*1.5;
            return (tex2D(_MainTex,i.uv+float2(d.x,d.y))+tex2D(_MainTex,i.uv+float2(-d.x,d.y))
                +tex2D(_MainTex,i.uv+float2(d.x,-d.y))+tex2D(_MainTex,i.uv-d))*0.25;
        }
        half4 threshold(v2f_img i):SV_Target {
            half4 c=blur(i); float l=max(c.r,max(c.g,c.b));
            return c*max(0,l-1.1)/max(l,0.0001);
        }
        half4 gaussian(v2f_img i):SV_Target { return blur(i); }
        half4 composite(v2f_img i):SV_Target {
            half4 c=tex2D(_MainTex,i.uv); c.rgb+=tex2D(_Bloom,i.uv).rgb*_Intensity; return c;
        }
        ENDCG
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment threshold
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment gaussian
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment composite
            ENDCG }
    }
}
