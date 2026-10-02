// Rex Magnum - realistic water for URP: two scrolling normal maps, depth colour (needs the camera depth
// texture), shore foam, Fresnel sky reflection, sun glint, optional Gerstner waves (ocean) and slime glow.
Shader "RexMagnum/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.25, 0.75, 0.78, 0.35)
        _DeepColor ("Deep Color", Color) = (0.02, 0.18, 0.3, 0.95)
        _DepthFade ("Depth Fade (m)", Float) = 4
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalTiling ("Normal Tiling (1/m)", Float) = 0.08
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.6
        _FlowSpeed ("Flow Speed", Float) = 0.03
        _FlowDir ("Flow Direction (xz)", Vector) = (1, 0.3, 0, 0)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _FoamColor ("Foam Color", Color) = (0.95, 0.97, 0.97, 1)
        _FoamDepth ("Foam Depth (m)", Float) = 0.6
        _Glow ("Glow (emission)", Color) = (0, 0, 0, 0)
        _WaveHeight ("Wave Height (m)", Float) = 0
        _WaveLength ("Wave Length (m)", Float) = 28
        _WaveFade ("Wave Fade Distance (m)", Float) = 900
        _River ("River Mode (flow along UV v, foam from vertex colour R)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor, _DeepColor, _FoamColor, _Glow, _FlowDir, _NormalMap_ST;
                float _DepthFade, _NormalTiling, _NormalStrength, _FlowSpeed, _Smoothness, _FoamDepth;
                float _WaveHeight, _WaveLength, _WaveFade, _River;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float fog : TEXCOORD3;
                float2 uv : TEXCOORD4;
                float4 color : TEXCOORD5;
                float2 crest : TEXCOORD6;        // ocean: wave height (-1..1 of the peak) and steepness, for crest foam
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 Gerstner(float3 p, float2 dir, float steep, float len, inout float3 tangent, inout float3 binormal)
            {
                float k = 6.2831853 / len;
                float c = sqrt(9.8 / k);
                float2 d = normalize(dir);
                float f = k * (dot(d, p.xz) - c * _Time.y);
                float a = steep / k;
                tangent += float3(-d.x * d.x * steep * sin(f), d.x * steep * cos(f), -d.x * d.y * steep * sin(f));
                binormal += float3(-d.x * d.y * steep * sin(f), d.y * steep * cos(f), -d.y * d.y * steep * sin(f));
                return float3(d.x * a * cos(f), a * sin(f), d.y * a * cos(f));
            }

            V vert(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);   // single-pass instanced VR: right eye index
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(i.normalOS);
                o.crest = 0;
                if (_WaveHeight > 0)
                {
                    float fade = saturate(1 - distance(ws.xz, _WorldSpaceCameraPos.xz) / _WaveFade);
                    float s = _WaveHeight / _WaveLength * 6.2831853 * fade;
                    float3 t = float3(1, 0, 0), b = float3(0, 0, 1);
                    float3 off = Gerstner(ws, float2(1, 0.4), s * 0.5, _WaveLength, t, b)
                               + Gerstner(ws, float2(-0.3, 1), s * 0.35, _WaveLength * 0.61, t, b)
                               + Gerstner(ws, float2(0.7, -0.6), s * 0.2, _WaveLength * 0.37, t, b);
                    ws += off;
                    n = normalize(cross(b, t));
                    // the three amplitudes add up to ~0.79 x _WaveHeight; far (faded) waves stay low: no crest foam there
                    o.crest = float2(off.y / (_WaveHeight * 0.79 + 1e-4), 1 - n.y);
                }
                o.positionWS = ws;
                o.normalWS = n;
                o.positionCS = TransformWorldToHClip(ws);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);   // depth texture is a per-eye array in VR
                // closed water boxes (pools, tanks): only the top face is water; the side / bottom faces
                // z-fought with the pool walls and flickered. Ocean (waves) and rivers keep every face.
                if (_WaveHeight <= 0 && _River < 0.5 && i.normalWS.y < 0.5) discard;
                float2 uv = i.positionWS.xz * _NormalTiling;
                float2 flow = normalize(_FlowDir.xy + 1e-4) * _FlowSpeed * _Time.y;
                if (_River > 0.5)
                {
                    // river mesh: u = metres across, v = metres downstream; ripples stretched along the flow and
                    // faster where the vertex colour G (slope) is high
                    uv = i.uv * float2(_NormalTiling * 1.6, _NormalTiling * 0.5);
                    flow = float2(0, -_FlowSpeed * (1 + i.color.g * 4) * _Time.y);
                }
                float3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv + flow), _NormalStrength);
                float3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv * 1.7 - flow * 1.3 + 0.37), _NormalStrength);
                float3 nt = normalize(float3(n1.xy + n2.xy, n1.z * n2.z));
                float3 up = normalize(i.normalWS);
                // reference axis never parallel to the normal (cross with +Z alone gave NaN on +-Z faces)
                float3 refAxis = abs(up.z) > 0.9 ? float3(1, 0, 0) : float3(0, 0, 1);
                float3 tx = normalize(cross(up, refAxis));
                float3 bz = cross(tx, up);
                float3 N = normalize(tx * nt.x + bz * nt.y + up * nt.z);

                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float depth = max(0, sceneEye - i.screenPos.w);
                float t = saturate(depth / _DepthFade);
                float4 col = lerp(_ShallowColor, _DeepColor, t);

                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float fres = pow(1 - saturate(dot(N, V)), 5) * 0.95 + 0.04;
                float3 refl = GlossyEnvironmentReflection(reflect(-V, N), 1 - _Smoothness, 1);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 H = normalize(L.direction + V);
                float spec = pow(saturate(dot(N, H)), 600 * _Smoothness + 8) * 6 * L.shadowAttenuation;
                float diff = saturate(dot(up, L.direction)) * 0.6 + 0.4;

                // sky ambient (spherical harmonics, up) so night / shaded water is lit by the sky, not black
                float3 amb = SampleSH(float3(0, 1, 0));
                float3 rgb = col.rgb * (diff * L.color + amb * 0.7) + _Glow.rgb;
                rgb = lerp(rgb, refl, fres);
                rgb += spec * L.color;
                float alpha = saturate(col.a + fres * 0.5);

                // foam where the water is shallow (shore, rocks, pool edges), broken up by the normal map
                float foam = saturate(1 - depth / _FoamDepth) * saturate(n1.x * 2 + 0.6);
                if (_River > 0.5)   // rapids: vertex colour R, broken into streaks by the moving normal map
                    foam = max(foam, saturate(i.color.r * 1.6 - 0.35 + (n1.x + n2.y) * 0.8));
                if (_WaveHeight > 0)
                {
                    // ocean whitecaps: on the high, steep part of each crest, torn into streaks by the normal maps
                    float crest = smoothstep(0.4, 0.85, i.crest.x) * saturate(0.35 + i.crest.y * 6);
                    foam = max(foam, crest * saturate((n1.x + n2.y) * 1.5 + 0.45) * 0.85);
                }
                rgb = lerp(rgb, _FoamColor.rgb * (L.color * 0.8 + amb * 0.6 + 0.05), foam * _FoamColor.a);
                alpha = max(alpha, foam);
                rgb = MixFog(rgb, i.fog);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
