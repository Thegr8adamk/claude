using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RexMagnum.EditorTools
{
    /// <summary>
    /// Menu "Rex Magnum > Build Island": makes Scenes/Island.unity from Island/Island.json (exported by
    /// Blender/island_export.py).
    ///  1. Textures + models import settings (ASTC 1K on Android), material remaps
    ///  2. URP materials: baked PBR tiles, glass, water (RexMagnum/Water), falls (RexMagnum/Waterfall), foliage
    ///  3. Building prefabs: LOD0 / LOD1, a collision mesh without water / glass-free parts, static flags
    ///  4. Terrain 1 km: heights (RAW 16-bit), 6 painted layers, palms / trees / ferns / rocks, grass details
    ///  5. Ocean with waves, sun, sky, fog, reflection probe, post-processing, SSAO
    ///  6. Rex + camera + weapons + blobs on the pool deck, automatic quality tier (QualityAuto)
    /// Batch mode: -executeMethod RexMagnum.EditorTools.IslandBuilder.BuildFromCommandLine
    /// </summary>
    public static class IslandBuilder
    {
        const string Dir = "Assets/RexMagnum/Island";
        const string Mats = Dir + "/Materials";
        const string Gen = Dir + "/Generated";
        const string Prefabs = "Assets/RexMagnum/Prefabs/Island";
        const string Scenes = "Assets/RexMagnum/Scenes";

        // ---------------------------------------------------------------- json
        [Serializable] class MatRow
        {
            public string name, kind, baseMap, normalMap, maskMap, emissionMap, water;
            public float[] color, emission;
            public float metallic, smoothness, alpha, tile, flow;
            public bool twoSided;
        }
        [Serializable] class AssetRow { public string name, fbx; public int tris; }
        [Serializable] class Proto { public string name, fbx, kind; }
        [Serializable] class Veg { public Proto[] protos; public float[] instances; }
        [Serializable] class Layer { public string name, diffuse, normal, mask; public float tile; }
        [Serializable] class TerrainInfo { public float size, minH, maxH; public int res; public string raw; public string[] splat; public Layer[] layers; }
        [Serializable] class Spawn { public float[] pos; public float yaw; }
        [Serializable] class Data { public MatRow[] materials; public AssetRow[] assets; public Veg vegetation; public TerrainInfo terrain; public Spawn spawn; }

        /// <summary>Blender (x east, y north, z up) to Unity (x, y up, z north).</summary>
        static Vector3 U(float x, float y, float z) => new Vector3(x, z, y);

        static Matrix4x4 axisFix = Matrix4x4.identity;   // corrects the FBX axis mapping if it is not the expected one

        [MenuItem("Rex Magnum/Build Island", priority = 3)]
        public static void Build()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp))
                throw new Exception("Rex Magnum needs URP (Project Settings > Graphics).");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string json = Dir + "/Island.json";
            if (!File.Exists(json)) throw new Exception("Missing " + json + ". Run Blender/island_export.py first.");
            foreach (var f in new[] { Mats, Gen, Prefabs, Scenes }) EnsureFolder(f);
            AssetDatabase.Refresh();
            urp.supportsCameraDepthTexture = true;
            EditorUtility.SetDirty(urp);

            var data = JsonUtility.FromJson<Data>(File.ReadAllText(json));
            try
            {
                EditorUtility.DisplayProgressBar("Rex Magnum", "Textures", 0.05f);
                ConfigureTextures();
                EditorUtility.DisplayProgressBar("Rex Magnum", "Materials", 0.15f);
                var mats = BuildMaterials(data.materials);
                EditorUtility.DisplayProgressBar("Rex Magnum", "Models", 0.25f);
                ConfigureModels(mats);
                axisFix = ReadAxisProbe();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var oldCam = Camera.main;
                if (oldCam != null) UnityEngine.Object.DestroyImmediate(oldCam.gameObject);

                EditorUtility.DisplayProgressBar("Rex Magnum", "Buildings", 0.35f);
                var world = new GameObject("Island").transform;
                foreach (var a in data.assets) BuildingPrefab(a, data.materials, world);

                EditorUtility.DisplayProgressBar("Rex Magnum", "Terrain", 0.55f);
                var terrain = BuildTerrain(data, world);
                EditorUtility.DisplayProgressBar("Rex Magnum", "Ocean + lighting", 0.8f);
                Ocean(mats, world);
                Lighting();

                EditorUtility.DisplayProgressBar("Rex Magnum", "Rex + blobs + weapons", 0.9f);
                var (rexPrefab, controller) = RexBuilder.BuildAssets();
                var sp = data.spawn.pos;
                Vector3 spawn = U(sp[0], sp[1], sp[2]);
                RexBuilder.AddPlayer(rexPrefab, controller, spawn, data.spawn.yaw + 180f, spawn + new Vector3(-12f, 0f, 3f), 2500f);
                var nav = UnityEngine.Object.FindFirstObjectByType<RuntimeNavMesh>();
                if (nav != null) { nav.center = spawn; nav.size = new Vector3(160f, 60f, 160f); }
                var game = GameObject.Find("Game") ?? new GameObject("Game");
                var q = game.AddComponent<QualityAuto>();
                q.terrains = new[] { terrain };

                string path = Scenes + "/Island.unity";
                EditorSceneManager.SaveScene(scene, path);
                var list = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
                list.Insert(0, new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = list.ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("[Rex Magnum] Island done. Open " + path + " and press Play.");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------------------------------------------------------------- import settings

        static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir + "/Textures" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                string n = Path.GetFileNameWithoutExtension(p);
                bool normal = n.EndsWith("_nrm"), mask = n.EndsWith("_mask");
                bool alpha = n.StartsWith("leaf_") || n.StartsWith("Fall_");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !(normal || mask);
                ti.alphaSource = (mask || alpha) ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                ti.alphaIsTransparency = alpha;
                ti.mipmapEnabled = true;
                ti.wrapMode = n.StartsWith("leaf_") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                ti.anisoLevel = 8;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                var android = ti.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = 1024;
                android.format = TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(android);
                ti.SaveAndReimport();
            }
        }

        static void ConfigureModels(Dictionary<string, Material> mats)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.importCameras = false;
                mi.importLights = false;
                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.addCollider = false;
                mi.isReadable = true;          // collision meshes + baked vegetation meshes read the vertices
                mi.meshCompression = ModelImporterMeshCompression.Low;
                mi.indexFormat = ModelImporterIndexFormat.Auto;
                mi.importNormals = ModelImporterNormals.Import;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var kv in mats)
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
                mi.SaveAndReimport();
            }
        }

        /// <summary>AxisProbe.fbx has empties at Blender (10,0,0), (0,10,0), (0,0,10). Expected in Unity:
        /// (10,0,0), (0,0,10), (0,10,0). If the importer maps them differently, return the fix matrix.</summary>
        static Matrix4x4 ReadAxisProbe()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/AxisProbe.fbx");
            if (asset == null) return Matrix4x4.identity;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            Vector3 P(string n) { var t = go.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == n); return t ? t.position / 10f : Vector3.zero; }
            Vector3 px = P("PX"), py = P("PY"), pz = P("PZ");
            UnityEngine.Object.DestroyImmediate(go);
            var actual = new Matrix4x4(px, py, pz, new Vector4(0, 0, 0, 1));
            var expect = new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(0, 1, 0, 0), new Vector4(0, 0, 0, 1));
            if (Mathf.Abs(actual.determinant) < 0.5f) return Matrix4x4.identity;
            var fix = expect * actual.inverse;
            bool same = (fix.GetColumn(0) - new Vector4(1, 0, 0, 0)).magnitude < 0.05f &&
                        (fix.GetColumn(1) - new Vector4(0, 1, 0, 0)).magnitude < 0.05f &&
                        (fix.GetColumn(2) - new Vector4(0, 0, 1, 0)).magnitude < 0.05f;
            if (!same) Debug.LogWarning("[Rex Magnum] FBX axes differ from the expected mapping; applying a fix: " + fix);
            return same ? Matrix4x4.identity : fix;
        }

        static void ApplyFix(Transform t)
        {
            if (axisFix == Matrix4x4.identity) return;
            t.localRotation = axisFix.rotation * t.localRotation;
            var s = axisFix.lossyScale;
            if (axisFix.determinant < 0f) s.x = -Mathf.Abs(s.x);
            t.localScale = Vector3.Scale(t.localScale, s);
        }

        // ---------------------------------------------------------------- materials

        static Texture2D Tex(string file) =>
            string.IsNullOrEmpty(file) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + file);

        static Color C(float[] c, float a = 1f) => c == null || c.Length < 3 ? Color.gray : new Color(c[0], c[1], c[2], a);

        static Material Asset(string name, Shader sh)
        {
            string p = $"{Mats}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
            m.shader = sh;
            return m;
        }

        static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static Dictionary<string, Material> BuildMaterials(MatRow[] rows)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var water = Shader.Find("RexMagnum/Water");
            var fall = Shader.Find("RexMagnum/Waterfall");
            var d = new Dictionary<string, Material>();
            foreach (var r in rows)
            {
                Material m;
                switch (r.kind)
                {
                    case "water":
                        m = Asset(r.name, water);
                        m.SetTexture("_NormalMap", Tex("Textures/Water_nrm.png"));
                        if (r.water == "slime")
                        {
                            m.SetColor("_ShallowColor", new Color(0.45f, 1f, 0.3f, 0.55f));
                            m.SetColor("_DeepColor", new Color(0.04f, 0.3f, 0.05f, 0.95f));
                            m.SetColor("_Glow", new Color(0.05f, 0.3f, 0.03f, 0f));
                            m.SetFloat("_DepthFade", 1.5f);
                            m.SetFloat("_FoamDepth", 0.25f);
                            m.SetColor("_FoamColor", new Color(0.7f, 1f, 0.6f, 0.8f));
                        }
                        else
                        {
                            m.SetColor("_ShallowColor", new Color(0.5f, 0.92f, 0.95f, 0.2f));
                            m.SetColor("_DeepColor", new Color(0.05f, 0.45f, 0.75f, 0.85f));
                            m.SetFloat("_DepthFade", 2.2f);
                            m.SetFloat("_FoamDepth", 0.12f);
                            m.SetFloat("_NormalTiling", 0.25f);
                            m.SetFloat("_NormalStrength", 0.35f);
                        }
                        break;
                    case "fall":
                        m = Asset(r.name, fall);
                        m.SetTexture("_MainTex", Tex(r.baseMap));
                        m.SetFloat("_Flow", r.flow == 0f ? 1f : r.flow);
                        break;
                    case "glass":
                        m = Asset(r.name, lit);
                        m.SetColor("_BaseColor", C(r.color, r.alpha));
                        m.SetFloat("_Smoothness", 0.95f);
                        m.SetFloat("_Metallic", 0f);
                        Transparent(m);
                        if (r.emission != null) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", C(r.emission)); }
                        break;
                    case "foliage":
                        m = Asset(r.name, lit);
                        m.SetTexture("_BaseMap", Tex(r.baseMap));
                        m.SetColor("_BaseColor", Color.white);
                        m.SetFloat("_Smoothness", r.smoothness);
                        m.SetFloat("_AlphaClip", 1f);
                        m.SetFloat("_Cutoff", 0.45f);
                        m.EnableKeyword("_ALPHATEST_ON");
                        m.SetFloat("_Cull", 0f);
                        m.renderQueue = (int)RenderQueue.AlphaTest;
                        break;
                    default:
                        m = Asset(r.name, lit);
                        var bm = Tex(r.baseMap);
                        m.SetTexture("_BaseMap", bm);
                        m.SetColor("_BaseColor", bm != null ? Color.white : C(r.color));
                        m.SetFloat("_Metallic", r.metallic);
                        m.SetFloat("_Smoothness", Mathf.Clamp01(r.smoothness));
                        var nm = Tex(r.normalMap);
                        if (nm != null) { m.SetTexture("_BumpMap", nm); m.EnableKeyword("_NORMALMAP"); }
                        var mk = Tex(r.maskMap);
                        if (mk != null)
                        {
                            m.SetTexture("_MetallicGlossMap", mk);
                            m.EnableKeyword("_METALLICSPECGLOSSMAP");
                            m.SetFloat("_Smoothness", 1f);
                        }
                        if (r.emission != null && r.emission.Length >= 3)
                        {
                            m.EnableKeyword("_EMISSION");
                            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                            m.SetColor("_EmissionColor", C(r.emission) * 0.6f);
                            var em = Tex(r.emissionMap);
                            if (em != null) m.SetTexture("_EmissionMap", em);
                        }
                        break;
                }
                if (r.twoSided && m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                d[r.name] = m;
            }
            return d;
        }

        // ---------------------------------------------------------------- buildings

        static readonly HashSet<string> NoCollide = new HashSet<string>();

        static void BuildingPrefab(AssetRow a, MatRow[] rows, Transform parent)
        {
            if (NoCollide.Count == 0)
                foreach (var r in rows)
                    if (r.kind == "water" || r.kind == "fall" || r.kind == "foliage" || r.name.Contains("Curtain") ||
                        r.name.Contains("Towel") || r.name == "A_Straw" || r.name == "A_Vine" || r.name == "A_Dark")
                        NoCollide.Add(r.name);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{a.fbx}");
            if (asset == null) { Debug.LogWarning("Missing " + a.fbx); return; }
            var root = new GameObject(a.name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            ApplyFix(model.transform);
            var rs = model.GetComponentsInChildren<MeshRenderer>();
            var lod0 = rs.Where(r => !r.name.EndsWith("_LOD1")).ToArray();
            var lod1 = rs.Where(r => r.name.EndsWith("_LOD1")).ToArray();
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(lod1.Length > 0
                ? new[] { new LOD(0.18f, lod0), new LOD(0.006f, lod1) }
                : new[] { new LOD(0.006f, lod0) });
            lg.RecalculateBounds();
            foreach (var r in rs)
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            // collision: LOD0 without water, falls, curtains, straw...
            foreach (var r in lod0)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var col = CollisionMesh(mf.sharedMesh, r.sharedMaterials, a.name);
                if (col == null) continue;
                var c = new GameObject(a.name + "_Collision");
                c.transform.SetParent(r.transform, false);
                c.AddComponent<MeshCollider>().sharedMesh = col;
                GameObjectUtility.SetStaticEditorFlags(c, StaticEditorFlags.NavigationStatic);
            }
            string path = $"{Prefabs}/{a.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.isStatic = true;
        }

        static Mesh CollisionMesh(Mesh src, Material[] mats, string name)
        {
            var tris = new List<int>();
            for (int s = 0; s < src.subMeshCount; s++)
            {
                string mn = s < mats.Length && mats[s] != null ? mats[s].name : "";
                if (NoCollide.Contains(mn)) continue;
                tris.AddRange(src.GetTriangles(s));
            }
            if (tris.Count == 0) return null;
            var m = new Mesh { name = name + "_Collision", indexFormat = IndexFormat.UInt32 };
            m.SetVertices(src.vertices);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            string p = $"{Gen}/{name}_Collision.asset";
            AssetDatabase.DeleteAsset(p);
            AssetDatabase.CreateAsset(m, p);
            return m;
        }

        // ---------------------------------------------------------------- vegetation

        /// <summary>Copy of the FBX mesh with the axis fix applied (so the prefab root can hold it directly).</summary>
        static (Mesh mesh, Material[] mats) BakedMesh(string fbx, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{fbx}");
            if (asset == null) return (null, null);
            var mr = asset.GetComponentInChildren<MeshRenderer>();
            var mf = mr != null ? mr.GetComponent<MeshFilter>() : null;
            if (mf == null) return (null, null);
            var src = mf.sharedMesh;
            var m = UnityEngine.Object.Instantiate(src);
            m.name = name;
            var M = axisFix * Matrix4x4.TRS(Vector3.zero, mf.transform.localRotation, mf.transform.localScale);
            m.vertices = src.vertices.Select(v => M.MultiplyPoint3x4(v)).ToArray();
            m.normals = src.normals.Select(n => M.MultiplyVector(n).normalized).ToArray();
            if (M.determinant < 0f)
                for (int s = 0; s < m.subMeshCount; s++) m.SetTriangles(m.GetTriangles(s).Reverse().ToArray(), s);
            m.RecalculateBounds();
            m.RecalculateTangents();
            string p = $"{Gen}/{name}.asset";
            AssetDatabase.DeleteAsset(p);
            AssetDatabase.CreateAsset(m, p);
            return (m, mr.sharedMaterials);
        }

        static GameObject TreePrefab(Proto pr)
        {
            var (mesh, mats) = BakedMesh(pr.fbx, pr.name);
            if (mesh == null) return null;
            var root = new GameObject(pr.name);
            var child = new GameObject(pr.name + "_Mesh");
            child.transform.SetParent(root.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = child.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = pr.kind == "fern" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(pr.kind == "fern" ? 0.02f : 0.008f, new Renderer[] { r }) });
            if (pr.kind == "palm" || pr.kind == "tree")
            {
                var cc = root.AddComponent<CapsuleCollider>();
                cc.radius = pr.kind == "palm" ? 0.25f : 0.4f;
                cc.height = 6f;
                cc.center = new Vector3(0f, 3f, 0f);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/Veg_{pr.name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject DetailPrefab(Proto pr)
        {
            var (mesh, mats) = BakedMesh(pr.fbx, pr.name);
            if (mesh == null) return null;
            var root = new GameObject(pr.name);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = root.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/Detail_{pr.name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------- terrain

        static Color32[] LoadPng(string file, out int w, out int h)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            t.LoadImage(File.ReadAllBytes(file));
            w = t.width; h = t.height;
            var px = t.GetPixels32();
            UnityEngine.Object.DestroyImmediate(t);
            return px;
        }

        static Terrain BuildTerrain(Data data, Transform parent)
        {
            var ti = data.terrain;
            int res = ti.res;
            var td = new TerrainData { heightmapResolution = res };
            td.size = new Vector3(ti.size, ti.maxH - ti.minH, ti.size);
            // heights: RAW 16-bit little endian, row 0 = south (Blender y = -size/2) = terrain z 0
            var bytes = File.ReadAllBytes($"{Dir}/{ti.raw}");
            var hts = new float[res, res];
            for (int r = 0; r < res; r++)
                for (int c = 0; c < res; c++)
                {
                    int i = (r * res + c) * 2;
                    hts[r, c] = (bytes[i] | (bytes[i + 1] << 8)) / 65535f;
                }
            td.SetHeights(0, 0, hts);

            // layers
            var layers = new List<TerrainLayer>();
            foreach (var l in ti.layers)
            {
                string p = $"{Gen}/{l.name}.terrainlayer";
                var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p);
                if (tl == null) { tl = new TerrainLayer(); AssetDatabase.CreateAsset(tl, p); }
                tl.diffuseTexture = Tex(l.diffuse);
                tl.normalMapTexture = Tex(l.normal);
                tl.maskMapTexture = Tex(l.mask);
                tl.tileSize = new Vector2(l.tile, l.tile);
                tl.normalScale = 1f;
                tl.smoothness = 0.1f;
                EditorUtility.SetDirty(tl);
                layers.Add(tl);
            }
            td.terrainLayers = layers.ToArray();

            // paint weights from the two splat PNGs (PNG row 0 = south, but Unity textures start at the bottom)
            int ar = 1024;
            td.alphamapResolution = ar;
            var s0 = LoadPng($"{Dir}/{ti.splat[0]}", out int sw, out int sh);
            var s1 = LoadPng($"{Dir}/{ti.splat[1]}", out _, out _);
            int nl = layers.Count;
            var alpha = new float[ar, ar, nl];
            for (int z = 0; z < ar; z++)
                for (int x = 0; x < ar; x++)
                {
                    int px = Mathf.Min(sw - 1, x * sw / ar);
                    int pr = Mathf.Min(sh - 1, z * sh / ar);        // png row (south first)
                    int idx = (sh - 1 - pr) * sw + px;               // texture row
                    var a = s0[idx]; var b = s1[idx];
                    float[] w = { a.r, a.g, a.b, a.a, b.r, b.g };
                    float sum = 0f;
                    for (int k = 0; k < nl; k++) sum += w[k];
                    for (int k = 0; k < nl; k++) alpha[z, x, k] = sum > 0 ? w[k] / sum : (k == 0 ? 1f : 0f);
                }
            td.SetAlphamaps(0, 0, alpha);

            // trees: palms, jungle trees, ferns, rocks (terrain instancing); grass clumps -> details
            var protos = data.vegetation.protos;
            var treeIndex = new Dictionary<int, int>();
            var treeProtos = new List<TreePrototype>();
            var detailProtos = new List<DetailPrototype>();
            for (int i = 0; i < protos.Length; i++)
            {
                var pr = protos[i];
                if (pr.kind == "grass")
                {
                    var dp = DetailPrefab(pr);
                    if (dp == null) continue;
                    detailProtos.Add(new DetailPrototype
                    {
                        usePrototypeMesh = true, prototype = dp, renderMode = DetailRenderMode.VertexLit,
                        useInstancing = true, minWidth = 0.8f, maxWidth = 1.4f, minHeight = 0.7f, maxHeight = 1.4f,
                        healthyColor = Color.white, dryColor = new Color(0.85f, 0.8f, 0.6f), noiseSpread = 0.3f,
                        alignToGround = 0.6f, density = 1f
                    });
                    continue;
                }
                var tp = TreePrefab(pr);
                if (tp == null) continue;
                treeIndex[i] = treeProtos.Count;
                treeProtos.Add(new TreePrototype { prefab = tp, bendFactor = pr.kind == "palm" ? 0.3f : 0.1f });
            }
            td.treePrototypes = treeProtos.ToArray();
            var inst = new List<TreeInstance>();
            var v = data.vegetation.instances;
            for (int k = 0; k + 5 < v.Length; k += 6)
            {
                if (!treeIndex.TryGetValue((int)v[k], out int ti2)) continue;
                float x = v[k + 1], y = v[k + 2], z = v[k + 3];
                inst.Add(new TreeInstance
                {
                    prototypeIndex = ti2,
                    position = new Vector3((x + ti.size / 2) / ti.size, (z - ti.minH) / (ti.maxH - ti.minH), (y + ti.size / 2) / ti.size),
                    rotation = -v[k + 4], widthScale = v[k + 5], heightScale = v[k + 5],
                    color = Color.white, lightmapColor = Color.white
                });
            }
            td.SetTreeInstances(inst.ToArray(), true);

            // grass: dense where the grass layer is strong, none under the resort, sand, rock or jungle floor
            if (detailProtos.Count > 0)
            {
                td.detailPrototypes = detailProtos.ToArray();
                int dr = 1024;
                td.SetDetailResolution(dr, 32);
                var rng = new System.Random(5);
                var maps = new int[detailProtos.Count][,];
                for (int i = 0; i < maps.Length; i++) maps[i] = new int[dr, dr];
                for (int z = 0; z < dr; z++)
                    for (int x = 0; x < dr; x++)
                    {
                        float g = alpha[z * ar / dr, x * ar / dr, Mathf.Min(2, nl - 1)];
                        if (g < 0.45f) continue;
                        float wx = x * ti.size / dr - ti.size / 2, wy = z * ti.size / dr - ti.size / 2;
                        if (Mathf.Abs(wx - 40f) < 58f && wy > -315f && wy < -226f) continue;   // hotel + pool deck
                        int n = Mathf.RoundToInt(g * 3f);
                        for (int c = 0; c < n; c++) maps[rng.Next(maps.Length)][z, x] += 1;
                    }
                for (int i = 0; i < maps.Length; i++) td.SetDetailLayer(0, 0, i, maps[i]);
            }

            AssetDatabase.DeleteAsset($"{Gen}/IslandTerrain.asset");
            AssetDatabase.CreateAsset(td, $"{Gen}/IslandTerrain.asset");
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-ti.size / 2, ti.minH, -ti.size / 2);
            var t = go.GetComponent<Terrain>();
            t.materialTemplate = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            AssetDatabase.CreateAsset(t.materialTemplate, $"{Gen}/TerrainLit.mat");
            t.drawInstanced = true;
            t.treeDistance = 650f;
            t.treeBillboardDistance = 300f;
            t.detailObjectDistance = 110f;
            t.heightmapPixelError = 3f;
            t.basemapDistance = 400f;
            var tc = go.GetComponent<TerrainCollider>();
            tc.terrainData = td;
            go.isStatic = true;
            return t;
        }

        // ---------------------------------------------------------------- ocean, sky, light, post

        static void Ocean(Dictionary<string, Material> mats, Transform parent)
        {
            var sh = Shader.Find("RexMagnum/Water");
            var m = Asset("Ocean", sh);
            m.SetTexture("_NormalMap", Tex("Textures/Water_nrm.png"));
            m.SetColor("_ShallowColor", new Color(0.3f, 0.82f, 0.82f, 0.3f));
            m.SetColor("_DeepColor", new Color(0.02f, 0.2f, 0.36f, 0.97f));
            m.SetFloat("_DepthFade", 6f);
            m.SetFloat("_FoamDepth", 0.7f);
            m.SetFloat("_WaveHeight", 0.35f);
            m.SetFloat("_WaveLength", 26f);
            EditorUtility.SetDirty(m);
            // grid: fine near the island, coarse far away
            const int n = 240;
            const float size = 3200f;
            var verts = new Vector3[(n + 1) * (n + 1)];
            for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                {
                    float u = x / (float)n * 2f - 1f, w = z / (float)n * 2f - 1f;
                    u = Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), 1.6f);
                    w = Mathf.Sign(w) * Mathf.Pow(Mathf.Abs(w), 1.6f);
                    verts[z * (n + 1) + x] = new Vector3(u * size / 2, 0f, w * size / 2);
                }
            var tris = new int[n * n * 6];
            int t = 0;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int a = z * (n + 1) + x;
                    tris[t++] = a; tris[t++] = a + n + 1; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = a + n + 1; tris[t++] = a + n + 2;
                }
            var mesh = new Mesh { name = "OceanGrid", indexFormat = IndexFormat.UInt32, vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 10f, size));
            AssetDatabase.DeleteAsset($"{Gen}/OceanGrid.asset");
            AssetDatabase.CreateAsset(mesh, $"{Gen}/OceanGrid.asset");
            var go = new GameObject("Ocean");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Lighting()
        {
            var sun = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.name = "Sun";
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 2.0f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.transform.rotation = Quaternion.Euler(36f, -145f, 0f);
            RenderSettings.sun = sun;

            var sky = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/Sky.mat");
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, Mats + "/Sky.mat"); }
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", new Color(0.45f, 0.62f, 0.85f));
            sky.SetColor("_GroundColor", new Color(0.3f, 0.36f, 0.4f));
            sky.SetFloat("_Exposure", 1.25f);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0011f;
            RenderSettings.fogColor = new Color(0.7f, 0.8f, 0.9f);

            // realtime reflection probe (sky + island) for the water and glass
            var rp = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Realtime;
            rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            rp.size = new Vector3(2400f, 600f, 2400f);
            rp.resolution = 256;
            rp.transform.position = new Vector3(40f, 40f, -250f);
            rp.importance = 0;

            // post-processing
            var vol = new GameObject("PostProcessing").AddComponent<Volume>();
            vol.isGlobal = true;
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Gen + "/IslandPost.asset");
            if (prof == null) { prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, Gen + "/IslandPost.asset"); }
            T Get<T>() where T : VolumeComponent { if (!prof.TryGet(out T c)) c = prof.Add<T>(true); c.active = true; return c; }
            var tm = Get<Tonemapping>(); tm.mode.Override(TonemappingMode.ACES);
            var bl = Get<Bloom>(); bl.threshold.Override(1.1f); bl.intensity.Override(0.35f); bl.scatter.Override(0.6f);
            var ca = Get<ColorAdjustments>(); ca.postExposure.Override(0.25f); ca.contrast.Override(12f); ca.saturation.Override(8f);
            var wb = Get<WhiteBalance>(); wb.temperature.Override(6f);
            var vg = Get<Vignette>(); vg.intensity.Override(0.18f);
            foreach (var c in prof.components) EditorUtility.SetDirty(c);
            EditorUtility.SetDirty(prof);
            vol.sharedProfile = prof;
            AddSSAO();
        }

        /// <summary>Adds the URP "Screen Space Ambient Occlusion" renderer feature (contact shadows in corners).</summary>
        static void AddSSAO()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;
            var so = new SerializedObject(urp);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0) return;
            var data = list.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData;
            if (data == null || data.rendererFeatures.Any(f => f != null && f.GetType().Name.Contains("AmbientOcclusion"))) return;
            var type = typeof(ScriptableRendererFeature).Assembly.GetTypes()
                .FirstOrDefault(t => t.Name == "ScreenSpaceAmbientOcclusion" && typeof(ScriptableRendererFeature).IsAssignableFrom(t));
            if (type == null) return;
            var feat = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            feat.name = "SSAO";
            AssetDatabase.AddObjectToAsset(feat, data);
            data.rendererFeatures.Add(feat);
            var dso = new SerializedObject(data);
            var map = dso.FindProperty("m_RendererFeatureMap");
            if (map != null)
            {
                map.arraySize++;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out string _, out long id);
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                dso.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(data);
        }
    }
}
