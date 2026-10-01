// Rex Magnum - mirror: the reflected camera image is sampled in screen space (correct from every angle).
Shader "RexMagnum/MirrorScreen"
{
    Properties { _BaseMap ("Reflection", 2D) = "white" {} _Tint ("Tint", Color) = (0.92, 0.94, 0.95, 1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial) float4 _BaseMap_ST; float4 _Tint; CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float4 screenPos : TEXCOORD0; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.screenPos = ComputeScreenPos(o.positionCS); return o; }
            half4 frag(V i) : SV_Target { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.screenPos.xy / i.screenPos.w) * _Tint; }
            ENDHLSL
        }
    }
    FallBack Off
}
