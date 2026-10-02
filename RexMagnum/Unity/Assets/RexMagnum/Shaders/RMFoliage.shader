// Rex Magnum - trees for URP: PBR lit (UniversalFragmentPBR), alpha-clipped two-sided leaves with a little light
// through them, and vertex wind sway. The tree meshes (Blender island_hq.make_palm / make_jungle_tree) carry the
// wind weights in the vertex colour: R = main bend (0 at the trunk base, grows up the trunk and out to the leaf tips,
// continuous so leaves stay on their branch), G = per-branch phase, B = branch / leaf flutter weight (0 on bark).
// The same sway runs in every pass (shadows and depth match the lit mesh). Per-tree phase from the object's world
// position, so instanced terrain trees do not move in step.
// Global _RMWind (DayNight / storms): extra wind, 0 = not set = normal, 1 = double, -1 = still (impostor bake).
Shader "RexMagnum/Foliage"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.45
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _MetallicGlossMap ("Mask (R metallic, A smoothness)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.35
        _Translucency ("Translucency (sun through leaves)", Range(0, 2)) = 0.6
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Header(Wind)]
        _WindStrength ("Wind Strength", Range(0, 3)) = 1
        _WindSpeed ("Wind Speed", Float) = 1.2
        _WindDirection ("Wind Direction (xz)", Vector) = (1, 0, 0.35, 0)
        _BendAmount ("Trunk Bend (m at R = 1)", Float) = 0.9
        _BranchAmount ("Branch Sway (m at B = 1)", Float) = 0.22
        _FlutterAmount ("Leaf Flutter (m)", Float) = 0.035
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        // SRP batcher: every material property in UnityPerMaterial, the same block in every pass
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor, _WindDirection;
            float _Cutoff, _BumpScale, _Metallic, _Smoothness, _Translucency, _Cull;
            float _WindStrength, _WindSpeed, _BendAmount, _BranchAmount, _FlutterAmount, _AlphaClip;
        CBUFFER_END
        float _RMWind;      // global, 0 = not set = normal wind

        // world-space sway of one vertex (col = wind vertex colour)
        float3 RMWindOffset(float3 posWS, float3 normalWS, float4 col)
        {
            float strength = _WindStrength * max(0.0, 1.0 + _RMWind);
            float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
            float2 dir = normalize(_WindDirection.xz + float2(1e-4, 0));
            float t = _Time.y * _WindSpeed;
            float tp = dot(originWS.xz, float2(0.071, 0.053));
            // slow gusts that roll across the island downwind
            float gust = 0.6 + 0.8 * (sin(dot(originWS.xz, dir) * 0.015 - t * 0.35) * 0.5 + 0.5);
            // whole tree leans downwind and sways
            float sway = (sin(t + tp) * 0.5 + sin(t * 0.43 + tp * 1.7) * 0.3 + 0.35) * gust;
            float3 off = float3(dir.x, 0, dir.y) * (col.r * _BendAmount * sway);
            off.y -= col.r * col.r * _BendAmount * abs(sway) * 0.15;      // bent tips dip a little (no stretching)
            // branches / fronds: own phase, sideways + up and down
            float ph = col.g * 6.2831853 + tp;
            float bs = sin(t * 1.9 + ph) * 0.6 + sin(t * 3.1 + ph * 1.3) * 0.4;
            off += (float3(-dir.y, 0, dir.x) * 0.5 + float3(0, 1, 0)) * (col.b * _BranchAmount * bs * gust);
            // leaf flutter along the normal, phase from the vertex position
            float fl = sin(t * 8.0 + dot(posWS, float3(1.7, 2.3, 1.1)) + ph);
            off += normalWS * (col.b * col.b * _FlutterAmount * fl * gust);
            return off * strength;
        }

        void RMClip(float2 uv)
        {
            #if defined(_ALPHATEST_ON)
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a - _Cutoff);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            // probe blending / box projection: UniversalFragmentPBR reaches GlossyEnvironmentReflection with
            // d.positionWS and d.normalizedScreenSpaceUV (URP 17 signature), both set in frag below
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            // terrain trees are drawn instanced: without lodfade unity_LODFade is one value per draw call, so the
            // cross-fade of every tree in the batch used the same (wrong) fade; lodfade makes it per instance (the
            // SpeedTree shaders do the same). Non-instanced and SRP-batched draws are unaffected
            #pragma instancing_options lodfade
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct A
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                half4 fogVertex : TEXCOORD4;      // x = fog, yzw = per-vertex additional lights
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            V vert(A i)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);   // single-pass instanced VR: right eye index
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(i.normalOS);
                ws += RMWindOffset(ws, n, i.color);
                o.positionWS = ws;
                o.normalWS = n;
                o.tangentWS = float4(TransformObjectToWorldDir(i.tangentOS.xyz), i.tangentOS.w * GetOddNegativeScale());
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fogVertex.x = ComputeFogFactor(o.positionCS.z);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                o.fogVertex.yzw = VertexLighting(ws, n);
                #endif
                return o;
            }

            half4 frag(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                clip(base.a - _Cutoff);
                #endif
                #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(i.positionCS);
                #endif
                float sgn = IS_FRONT_VFACE(face, 1.0, -1.0);    // back faces of two-sided leaves light as their own side
                float3 nWS = normalize(i.normalWS) * sgn;
                #ifdef _NORMALMAP
                float3 tWS = normalize(i.tangentWS.xyz);
                float3 bWS = cross(nWS, tWS) * i.tangentWS.w;
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                nWS = normalize(mul(nTS, float3x3(tWS, bWS, nWS)));
                #endif
                half4 mask = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, i.uv);

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = nWS;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fogVertex.x;
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                d.vertexLighting = i.fogVertex.yzw;
                #endif
                d.bakedGI = SampleSH(nWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = base.rgb;
                s.alpha = 1;
                s.metallic = _Metallic * mask.r;
                s.smoothness = _Smoothness * mask.a;
                s.normalTS = half3(0, 0, 1);
                s.occlusion = 1;
                s.specular = 0;
                half4 col = UniversalFragmentPBR(d, s);
                // sun shining through thin leaves (back-lit): albedo-tinted, shadowed
                Light L = GetMainLight(d.shadowCoord);
                half back = pow(saturate(dot(d.viewDirectionWS, -L.direction)), 4) * 0.7 + saturate(dot(-nWS, L.direction)) * 0.3;
                col.rgb += base.rgb * L.color * (back * L.shadowAttenuation * _Translucency);
                col.rgb = MixFog(col.rgb, d.fogCoord);
                return half4(col.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options lodfade      // per-instance fade for instanced terrain trees (see ForwardLit)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            float3 _LightDirection;     // set by URP for the shadow pass
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V vert(A i)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(i.normalOS);
                ws += RMWindOffset(ws, n, i.color);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 ld = normalize(_LightPosition - ws);
                #else
                float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, n, ld));
                #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                RMClip(i.uv);
                #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(i.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options lodfade      // per-instance fade for instanced terrain trees (see ForwardLit)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            V vert(A i)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                ws += RMWindOffset(ws, TransformObjectToWorldNormal(i.normalOS), i.color);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                RMClip(i.uv);
                #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(i.positionCS);
                #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options lodfade      // per-instance fade for instanced terrain trees (see ForwardLit)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            V vert(A i)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(i.normalOS);
                ws += RMWindOffset(ws, n, i.color);
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = n;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                RMClip(i.uv);
                #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(i.positionCS);
                #endif
                float3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n);
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
