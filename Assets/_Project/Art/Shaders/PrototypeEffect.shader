Shader "SousTension/PrototypeEffect"
{
    Properties
    {
        _Tint("Tint", Color) = (0.4,0.6,0.65,0.5)
        _Mode("Water 0 Steam 1 Frost 2 Condensation 3 Contamination 4 Glow 5", Float) = 0
        _Strength("Effect strength", Range(0,1)) = 0.6
        _WaveAmplitude("Wave amplitude metres", Range(0,0.08)) = 0.01
        _Speed("Speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Mode, _Strength, _WaveAmplitude, _Speed;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
            V vert(A a)
            {
                V o;
                if (_Mode < 0.5)
                    a.positionOS.y += _WaveAmplitude * sin(a.positionOS.x*3.1+_Time.y*_Speed)
                        * cos(a.positionOS.z*2.7+_Time.y*_Speed*0.7);
                o.world = TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = a.uv;
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv;
                float edge=smoothstep(0,0.12,p.x)*smoothstep(0,0.12,p.y)
                    *smoothstep(0,0.12,1-p.x)*smoothstep(0,0.12,1-p.y);
                float pattern=1;
                float3 color=_Tint.rgb;
                if (_Mode < 0.5)
                {
                    float ripple=0.5+0.5*sin(i.world.x*8+i.world.z*6+_Time.y*_Speed*2);
                    color*=0.8+0.25*ripple;
                    edge=1;
                }
                else if (_Mode < 1.5)
                {
                    pattern=noise(p*5-float2(0,_Time.y*_Speed*0.3))*noise(p*9+_Time.y*0.1);
                    pattern=smoothstep(0.05,0.65,pattern);
                }
                else if (_Mode < 2.5)
                    pattern=smoothstep(0.36,0.65,noise(p*30))*0.8+noise(p*6)*0.2;
                else if (_Mode < 3.5)
                    pattern=pow(saturate(noise(p*float2(25,7)+float2(0,_Time.y*_Speed*0.05))),3);
                else if (_Mode < 4.5)
                {
                    edge*=1-smoothstep(0.25,0.5,length(p-0.5));
                    pattern=0.65+noise(p*7+_Time.y*_Speed*0.08)*0.35;
                }
                else
                {
                    edge*=1-smoothstep(0.05,0.5,length(p-0.5));
                    pattern=0.7+0.3*sin(_Time.y*_Speed);
                    color*=2;
                }
                return half4(color,_Tint.a*_Strength*edge*pattern);
            }
            ENDHLSL
        }
    }
}
