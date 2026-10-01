// Rex Magnum - falling water: streak texture scrolls down (UV v), white aerated water with soft alpha,
// small light wrap so it is not flat, fog. Two-sided.
Shader "RexMagnum/Waterfall"
{
    Properties
    {
        _MainTex ("Streaks (RGBA)", 2D) = "white" {}
        _Color ("Tint", Color) = (0.92, 0.97, 0.96, 1)
        _Speed ("Fall Speed (UV/s)", Float) = 0.6
        _Flow ("Flow Sign (1 = UV v grows downward)", Float) = 1
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _Glow ("Self Light", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _Color;
                float _Speed, _Flow, _Opacity, _Glow;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float fog : TEXCOORD2; };
            V vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float s = _Time.y * _Speed * _Flow;
                float4 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(i.uv.x, i.uv.y * 0.5 - s));
                float4 b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(i.uv.x * 1.3 + 0.21, i.uv.y * 0.8 - s * 1.4));
                float m = saturate(a.a * 0.6 + b.a * 0.6);
                Light L = GetMainLight();
                float wrap = saturate(abs(dot(normalize(i.normalWS), L.direction)) * 0.5 + 0.5);
                float3 rgb = _Color.rgb * (L.color * wrap + _Glow) * lerp(0.75, 1.05, m);
                rgb = MixFog(rgb, i.fog);
                return half4(rgb, m * _Opacity);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
