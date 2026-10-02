// Rex Magnum - cloud layer: a sphere around the camera (just inside the far plane) with procedural, slowly drifting
// fbm clouds, alpha-blended over the Procedural skybox. Lit by the main light (its colour turns orange at sunset and
// moon-blue at night, so the clouds follow DayNight by themselves) + the sky ambient; DayNight adds the sunset glow
// (_Dusk), the night dimming (_Night) and slowly changes the coverage. No textures, no depth write.
Shader "RexMagnum/Clouds"
{
    Properties
    {
        _Cover ("Coverage", Range(0, 1)) = 0.45
        _Softness ("Edge Softness", Range(0.02, 0.5)) = 0.16
        _Scale ("Scale", Float) = 1.4
        _Wind ("Wind (xy direction, z speed)", Vector) = (1, 0.35, 0.006, 0)
        _Opacity ("Opacity", Range(0, 1)) = 0.9
        _Dusk ("Sunset Glow", Range(0, 1)) = 0
        _DuskColor ("Sunset Colour", Color) = (1, 0.5, 0.32, 1)
        _Night ("Night", Range(0, 1)) = 0
    }
    SubShader
    {
        // after the opaques and the skybox, before the other transparents (water, glass)
        Tags { "RenderType"="Transparent" "Queue"="Transparent-400" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off            // the camera sits at the centre: every view ray crosses the sphere once
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Wind, _DuskColor;
                float _Cover, _Softness, _Scale, _Opacity, _Dusk, _Night;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            V vert(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);   // single-pass instanced VR
                float3 d = normalize(i.positionOS.xyz);
                o.dir = d;
                // centred on the camera at 90% of its far plane: nearer geometry hides it, the sky does not
                o.positionCS = TransformWorldToHClip(_WorldSpaceCameraPos + d * (_ProjectionParams.z * 0.9));
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float Noise(float2 p)
            {
                float2 c = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(c), Hash(c + float2(1, 0)), f.x), lerp(Hash(c + float2(0, 1)), Hash(c + float2(1, 1)), f.x), f.y);
            }
            float Fbm(float2 p, int oct)
            {
                float s = 0.0, a = 0.5;
                for (int k = 0; k < oct; k++) { s += a * Noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return s;
            }

            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.dir);
                clip(d.y - 0.001);                                   // nothing below the horizon (the ocean is there)
                // a flat cloud deck seen in perspective: small and dense near the horizon, large overhead
                float2 uv = d.xz / (d.y + 0.12) * _Scale;
                float2 w = normalize(_Wind.xy + 1e-4) * _Wind.z * _Time.y;
                float den = Fbm(uv + w, 5) * 0.75 + Fbm(uv * 2.7 - w * 1.7, 3) * 0.25;
                float t = lerp(0.72, 0.32, _Cover);                  // more coverage: a lower threshold
                float cov = smoothstep(t - _Softness, t + _Softness, den);
                float thick = saturate((den - t) * 4.0);

                Light sun = GetMainLight();
                float mu = dot(d, sun.direction);
                half3 amb = SampleSH(half3(0, 1, 0));
                // thin edges catch the light (silver lining towards the sun), thick centres are grey underneath
                float shade = lerp(1.0, 0.5, thick);
                float silver = pow(saturate(mu), 6.0) * (1.0 - thick) * 1.2;
                half3 col = amb * 0.85 + sun.color * (0.45 * shade + silver);
                // sunset: warm undersides, strongest on the sun's side of the sky
                float glow = _Dusk * (0.35 + 0.65 * pow(saturate(mu * 0.5 + 0.5), 3.0));
                col = lerp(col, col * _DuskColor.rgb * 1.5 + _DuskColor.rgb * 0.15, glow);
                col *= lerp(1.0, 0.55, _Night);
                // haze: fade into the fog colour and out towards the horizon
                float h = saturate(d.y * 5.0);
                col = lerp(unity_FogColor.rgb, col, saturate(d.y * 3.0));
                float alpha = cov * _Opacity * h * h * lerp(1.0, 0.75, _Night);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
