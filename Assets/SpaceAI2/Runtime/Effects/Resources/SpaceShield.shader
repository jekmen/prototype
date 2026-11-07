Shader "SpaceAI2/Effects/Shield"
{
    Properties
    {
        [HDR] _Color ("Impact color", Color) = (0.08,0.55,1,1)
        _Distortion ("Refraction (pixels)", Range(0,24)) = 12
    }
    SubShader
    {
        Tags { "Queue"="Transparent+50" "RenderType"="Transparent" }
        // All visible shields share one scene-color capture per camera.
        GrabPass { "_SpaceShieldBackground" }
        ZWrite Off Cull Back
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _SpaceShieldBackground;
            float4 _SpaceShieldBackground_TexelSize;
            float4 _Color, _Hits[4];
            float _Distortion;
            struct v2f
            {
                float4 pos:SV_POSITION;
                float3 local:TEXCOORD0;
                float3 normal:TEXCOORD1;
                float4 grab:TEXCOORD2;
            };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos=UnityObjectToClipPos(v.vertex);
                o.local=v.vertex.xyz*2;
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.grab=ComputeGrabScreenPos(o.pos);
                return o;
            }
            float hash(float3 p)
            {
                p=frac(p*0.1031);
                p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            float noise(float3 p)
            {
                float3 cell=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(
                    lerp(lerp(hash(cell),hash(cell+float3(1,0,0)),f.x),
                         lerp(hash(cell+float3(0,1,0)),hash(cell+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(cell+float3(0,0,1)),hash(cell+float3(1,0,1)),f.x),
                         lerp(hash(cell+float3(0,1,1)),hash(cell+1),f.x),f.y),f.z);
            }
            float turbulence(float3 p)
            {
                return noise(p)*0.57+noise(p*2.07+7.1)*0.29+noise(p*4.13+19.7)*0.14;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 local=normalize(i.local);
                float3 flow=local*11+float3(0,_Time.y*0.65,-_Time.y*0.4);
                float cloud=turbulence(flow);
                float mask=0, edge=0, hot=0;
                [unroll] for(int h=0;h<4;h++)
                {
                    float age=_Time.y-_Hits[h].w;
                    float t=saturate(age/1.1);
                    float alive=step(0,age)*(1-smoothstep(0.55,1,t));
                    float d=length(local-_Hits[h].xyz);
                    float radius=0.08+0.95*(1-exp(-t*3.5));
                    float inner=max(0,radius-0.33);
                    float front=radius-d+(cloud-0.5)*0.24;
                    float back=d-inner+(cloud-0.5)*0.17;
                    float field=min(front,back);
                    float dissolve=cloud-lerp(0.22,0.78,t);
                    float coverage=smoothstep(-0.025,0.025,field)*smoothstep(-0.035,0.035,dissolve)*alive;
                    float border=(1-smoothstep(0.015,0.075,min(field,dissolve)))*coverage;
                    mask=max(mask,coverage);
                    edge+=border;
                    hot+=exp(-d*16)*exp(-max(age,0)*12)*alive;
                }
                // Nothing outside an impact patch contributes color or refraction.
                clip(max(mask,hot)-0.005);
                float2 screen=i.grab.xy/i.grab.w;
                float2 ripple=float2(turbulence(flow+float3(4,13,7)),turbulence(flow+float3(17,2,9)))*2-1;
                float2 normal=mul((float3x3)UNITY_MATRIX_V,normalize(i.normal)).xy;
                float2 offset=(ripple*1.8+normal*0.35)*_Distortion*abs(_SpaceShieldBackground_TexelSize.xy)*mask;
                float3 background=tex2D(_SpaceShieldBackground,clamp(screen+offset,0.001,0.999)).rgb;
                float3 emission=_Color.rgb*(mask*0.10+edge*2.3)+float3(0.7,0.9,1)*hot*2;
                return float4(background+emission,saturate(mask*0.72+hot*0.85));
            }
            ENDCG
        }
    }
}
