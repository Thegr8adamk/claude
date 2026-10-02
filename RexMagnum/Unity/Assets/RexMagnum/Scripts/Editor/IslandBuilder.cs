using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
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
        static BlobSpawner mainSpawner;

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
            public float tileV, uvTile;      // uvTile: metres per UV unit in the FBX when it differs from the baked tile
            public bool twoSided;
        }
        [Serializable] class AssetRow { public string name, fbx; public int tris; }
        [Serializable] class Proto { public string name, fbx, kind; }
        [Serializable] class Veg { public Proto[] protos; public float[] instances; }
        [Serializable] class Layer { public string name, diffuse, normal, mask; public float tile; }
        [Serializable] class TerrainInfo { public float size, minH, maxH; public int res; public string raw; public string[] splat; public Layer[] layers; }
        [Serializable] class Spawn { public float[] pos; public float yaw; }
        [Serializable] class Room
        {
            public float[] doorHinge, bedLie, wake, fullBottle, towel, closetMirror, bathMirror, tv, lamp1, lamp2, ceiling, balcony;
        }
        [Serializable] class HotelPts
        {
            public float[] pistol, knife, swordfish, sniper, elevatorX, elevatorFloors, stage, poolDoors;
        }
        [Serializable] class GolfPts { public float[] proShop, golfClub; public float[] carts, chargers; }
        [Serializable] class Data { public MatRow[] materials; public AssetRow[] assets; public Veg vegetation; public TerrainInfo terrain; public Spawn spawn; public Room room304; public HotelPts hotel; public float[] pool; }

        static Vector3 U(float[] p) => p == null || p.Length < 3 ? Vector3.zero : U(p[0], p[1], p[2]);

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
                foreach (var a in data.assets)
                    if (a.name != "GolfCart") BuildingPrefab(a, data.materials, world);

                EditorUtility.DisplayProgressBar("Rex Magnum", "Terrain", 0.55f);
                var terrain = BuildTerrain(data, world);
                EditorUtility.DisplayProgressBar("Rex Magnum", "Ocean + lighting", 0.8f);
                Ocean(mats, world);
                Lighting();

                EditorUtility.DisplayProgressBar("Rex Magnum", "Rex + blobs + weapons", 0.9f);
                var (rexPrefab, controller) = RexBuilder.BuildAssets();
                var room = data.room304;
                bool hasRoom = room != null && room.wake != null && room.wake.Length == 3;
                var sp = data.spawn.pos;
                Vector3 spawn = hasRoom ? U(room.wake) : U(sp[0], sp[1], sp[2]);
                var rex = RexBuilder.AddPlayer(rexPrefab, controller, spawn, hasRoom ? 270f : data.spawn.yaw + 180f, null, 2500f);
                var nav = UnityEngine.Object.FindAnyObjectByType<RuntimeNavMesh>();
                Vector3 pool = data.pool != null && data.pool.Length == 3 ? U(data.pool) : spawn;
                if (nav != null) { nav.center = pool + new Vector3(0f, 0f, -30f); nav.size = new Vector3(200f, 60f, 240f); }
                var spawner = UnityEngine.Object.FindAnyObjectByType<BlobSpawner>();
                mainSpawner = spawner;          // the copy source for the factory (later spawners are not it)
                if (spawner != null) { spawner.transform.position = pool; spawner.radius = 18f; spawner.maxAlive = 6; }
                var game = GameObject.Find("Game") ?? new GameObject("Game");
                var dn = game.AddComponent<DayNight>();
                dn.sun = RenderSettings.sun;
                var dome = GameObject.Find("CloudDome");          // made in Lighting(); DayNight drives its glow / coverage
                if (dome != null) dn.cloudDome = dome.GetComponent<Renderer>();
                var q = game.AddComponent<QualityAuto>();
                q.terrains = new[] { terrain };
                if (hasRoom) Room304(room, rex, game);
                Golf(File.ReadAllText(json), pool);
                Factory(File.ReadAllText(json));
                Tunnels(File.ReadAllText(json), terrain);
                Boss(terrain);
                foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))   // lamps that switch on at dusk
                    if (l.type == LightType.Point && l.GetComponent<LightFlicker>() == null && !l.name.StartsWith("Cab") &&
                        l.transform.parent != null && l.transform.parent.name.StartsWith("Hotel_Gameplay") == false &&
                        !l.transform.parent.name.StartsWith("Room304") && !l.transform.parent.name.StartsWith("Tunnels") &&
                        !l.transform.parent.name.StartsWith("Factory"))
                        dn.nightLights.Add(l);
                if (data.hotel != null && data.hotel.elevatorFloors != null)
                {
                    Hotel(data.hotel);
                    Guests(data.hotel, pool);
                }
                Beach(world);
                LightProbeGrid(terrain, data.hotel != null ? data.hotel.elevatorFloors : null);

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

        const float FoliageCutoff = 0.45f;     // leaf material _Cutoff = texture alphaTestReferenceValue

        static void ConfigureTextures()
        {
            AssetDatabase.StartAssetEditing();     // one import pass for all files (much faster)
            try
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
                    // cut-out leaves: keep the alpha coverage in small mips so far trees do not thin out / vanish
                    ti.mipMapsPreserveCoverage = n.StartsWith("leaf_");
                    ti.alphaTestReferenceValue = FoliageCutoff;
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
            finally { AssetDatabase.StopAssetEditing(); }
        }

        static void ConfigureModels(Dictionary<string, Material> mats)
        {
            AssetDatabase.StartAssetEditing();     // one import pass for all files (much faster)
            try
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
                    mi.meshCompression = ModelImporterMeshCompression.Off;   // full precision: big buildings (90 m) keep exact vertices
                    mi.indexFormat = ModelImporterIndexFormat.UInt32;           // no 16-bit index wrap on the 100k-triangle buildings
                    mi.importNormals = ModelImporterNormals.Import;
                    mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    foreach (var kv in mats)
                        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
                    mi.SaveAndReimport();
                }
            
            }
            finally { AssetDatabase.StopAssetEditing(); }
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

        // Island.json colours are Blender linear values; Material.SetColor expects sRGB in a Linear project -> .gamma
        static Color C(float[] c, float a = 1f) => c == null || c.Length < 3 ? Color.gray : Srgb(c[0], c[1], c[2], a);
        static Color Srgb(float r, float g, float b, float a) { var s = new Color(r, g, b).gamma; s.a = a; return s; }

        /// <summary>HDR emission (linear, may be > 1): convert the colour part to sRGB and keep the intensity.</summary>
        public static Color Hdr(Color lin)
        {
            float k = Mathf.Max(1f, lin.maxColorComponent);
            var g = new Color(lin.r / k, lin.g / k, lin.b / k).gamma;
            return g * Mathf.LinearToGammaSpace(k);
        }

        static Color E(float[] e) => e == null || e.Length < 3 ? Color.black : Hdr(new Color(e[0], e[1], e[2]));


        static Material Asset(string name, Shader sh)
        {
            string p = $"{Mats}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
            m.shader = sh;
            // a reused .mat keeps the last build's keywords / queue / blend (a row that was glass and is now opaque
            // would stay transparent): start from the opaque defaults, the kind below sets what it needs
            m.shaderKeywords = new string[0];
            m.renderQueue = -1;
            m.SetOverrideTag("RenderType", "");
            m.SetShaderPassEnabled("ShadowCaster", true);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 2f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.One);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.Zero);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 1);
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
                        if (r.water == "slime" || r.water == "river")
                        {
                            m.SetColor("_ShallowColor", new Color(0.45f, 1f, 0.3f, 0.55f));
                            m.SetColor("_DeepColor", new Color(0.04f, 0.3f, 0.05f, 0.95f));
                            m.SetColor("_Glow", new Color(0.05f, 0.3f, 0.03f, 0f));
                            m.SetFloat("_DepthFade", 1.5f);
                            m.SetFloat("_FoamDepth", 0.25f);
                            m.SetColor("_FoamColor", new Color(0.7f, 1f, 0.6f, 0.8f));
                            m.SetFloat("_River", r.water == "river" ? 1f : 0f);
                            if (r.water == "river") { m.SetFloat("_FlowSpeed", 0.25f); m.SetFloat("_NormalStrength", 0.8f); }
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
                        m.SetShaderPassEnabled("ShadowCaster", false);     // clear glass must not cast solid shadows
                        if (r.emission != null && r.emission.Length >= 3) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", E(r.emission)); }
                        break;
                    case "foliage":
                        m = Asset(r.name, lit);
                        m.SetTexture("_BaseMap", Tex(r.baseMap));
                        m.SetColor("_BaseColor", Color.white);
                        m.SetFloat("_Smoothness", r.smoothness);
                        m.SetFloat("_AlphaClip", 1f);
                        m.SetFloat("_Cutoff", FoliageCutoff);
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
                        if (r.name == "A_Stucco" && FlatNormalMap(r.normalMap)) nm = StuccoNormal();
                        if (nm != null) { m.SetTexture("_BumpMap", nm); m.EnableKeyword("_NORMALMAP"); }
                        // re-baked tiles on old FBX UVs (2 m per UV unit): scale so the tile keeps its real size
                        m.SetTextureScale("_BaseMap", r.uvTile > 0f && r.tile > 0f
                            ? new Vector2(r.uvTile / r.tile, r.uvTile / (r.tileV > 0f ? r.tileV : r.tile)) : Vector2.one);
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
                            m.SetColor("_EmissionColor", Hdr(new Color(r.emission[0], r.emission[1], r.emission[2]) * 0.6f));
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
        static readonly HashSet<string> SeeThrough = new HashSet<string>();     // glass, water, falls, foliage: never occluders

        static void BuildingPrefab(AssetRow a, MatRow[] rows, Transform parent)
        {
            if (NoCollide.Count == 0)
                foreach (var r in rows)
                    if (r.kind == "water" || r.kind == "fall" || r.kind == "foliage" || r.name.Contains("Curtain") ||
                        r.name.Contains("Towel") || r.name == "A_Straw" || r.name == "A_Vine" || r.name == "A_Dark")
                        NoCollide.Add(r.name);
            if (SeeThrough.Count == 0)
                foreach (var r in rows)
                    if (r.kind == "glass" || r.kind == "water" || r.kind == "fall" || r.kind == "foliage") SeeThrough.Add(r.name);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{a.fbx}");
            if (asset == null) { Debug.LogWarning("Missing " + a.fbx); return; }
            var root = new GameObject(a.name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            StripImportedLODs(model);
            ApplyFix(model.transform);
            var rs = model.GetComponentsInChildren<MeshRenderer>();
            // collision: LOD0 without water, falls, curtains, straw... (from the full mesh, before the glass split)
            foreach (var r in rs.Where(x => x.name.EndsWith("_LOD0")))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var col = CollisionMesh(mf.sharedMesh, r.sharedMaterials, a.name);
                if (col == null) continue;
                var c = new GameObject(a.name + "_Collision");
                c.transform.SetParent(r.transform, false);
                c.AddComponent<MeshCollider>().sharedMesh = col;
            }
            foreach (var r in rs)
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                // only the building shells occlude; extras (Ruins_Core, door, bottles) move or are small: occludee only
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, r.name.Contains("_LOD")
                    ? StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic : StaticEditorFlags.OccludeeStatic);
            }
            // see-through sub-meshes in an occluder hide what is behind the windows / pools / falls after the
            // occlusion bake: move them to a child renderer that is only an occludee (same LOD level as its parent)
            var seeThrough = new Dictionary<MeshRenderer, MeshRenderer>();
            foreach (var r in rs.Where(x => x.name.Contains("_LOD")))      // extras (door, bottles) move: left whole
            {
                var st = SplitSeeThrough(r, a.name);
                if (st != null) seeThrough[r] = st;
            }
            MeshRenderer[] Level(string suffix) => rs.Where(r => r.name.EndsWith(suffix))
                .SelectMany(r => seeThrough.TryGetValue(r, out var st) ? new[] { r, st } : new[] { r }).ToArray();
            var lod0 = Level("_LOD0");       // extras (_Door, _Bottle, _Core) stay outside the LODs
            var lod1 = Level("_LOD1");
            var lod2 = Level("_LOD2");      // heavy buildings: middle level for phones
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(lod2.Length > 0 ? new[] { new LOD(0.3f, lod0), new LOD(0.1f, lod1), new LOD(0.006f, lod2) }
                : lod1.Length > 0 ? new[] { new LOD(0.18f, lod0), new LOD(0.006f, lod1) }
                : new[] { new LOD(0.006f, lod0) });
            lg.RecalculateBounds();
            string path = $"{Prefabs}/{a.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.isStatic = true;
        }

        /// <summary>The FBX importer adds its own LODGroup when meshes are named _LOD0/_LOD1. With our LODGroup on the
        /// parent, two groups own the same renderers and Unity can draw the low (decimated) copies inside the full one:
        /// big slanted wedges through Room 304 and the balcony. Unpack the instance and remove the imported group.</summary>
        static void StripImportedLODs(GameObject model)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(model))
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var g in model.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(g);
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.triangles.Length == 0) r.enabled = false;  // empty LOD (Room304_LOD1)
            }
        }

        /// <summary>Moves the see-through sub-meshes (glass, water, falls, foliage) of r into a child renderer that is
        /// OccludeeStatic only; r keeps the solid ones and stays the occluder. A renderer that is all see-through just
        /// loses OccluderStatic. Returns the new child renderer, or null.</summary>
        static MeshRenderer SplitSeeThrough(MeshRenderer r, string building)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return null;
            var src = mf.sharedMesh;
            var mats = r.sharedMaterials;
            var solid = new List<int>();
            var clear = new List<int>();
            // a sub-mesh without a material was never drawn: it goes to neither part (mats[s] would be out of range)
            for (int s = 0; s < src.subMeshCount && s < mats.Length; s++)
                (mats[s] != null && SeeThrough.Contains(mats[s].name) ? clear : solid).Add(s);
            if (clear.Count == 0) return null;
            if (solid.Count == 0)
            {
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.OccludeeStatic);
                return null;
            }
            Mesh Part(List<int> subs, string tag)
            {
                // only the vertices these sub-meshes use (a whole copy kept every vertex in both parts: double memory),
                // every channel the source has (UV2 lightmap UVs, colours...)
                var map = new int[src.vertexCount];
                for (int k = 0; k < map.Length; k++) map[k] = -1;
                var used = new List<int>();
                var subTris = new List<int[]>();
                foreach (int sub in subs)
                {
                    var t = src.GetTriangles(sub);
                    for (int k = 0; k < t.Length; k++)
                    {
                        if (map[t[k]] < 0) { map[t[k]] = used.Count; used.Add(t[k]); }
                        t[k] = map[t[k]];
                    }
                    subTris.Add(t);
                }
                List<T> Pick<T>(List<T> all) => all.Count == 0 ? all : used.Select(k => all[k]).ToList();
                var m = new Mesh { name = $"{building}_{r.name}_{tag}" };
                m.indexFormat = used.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                var v3 = new List<Vector3>();
                src.GetVertices(v3); m.SetVertices(Pick(v3));
                src.GetNormals(v3); if (v3.Count > 0) m.SetNormals(Pick(v3));
                var v4 = new List<Vector4>();
                src.GetTangents(v4); if (v4.Count > 0) m.SetTangents(Pick(v4));
                var col = new List<Color>();
                src.GetColors(col); if (col.Count > 0) m.SetColors(Pick(col));
                for (int ch = 0; ch < 8; ch++)
                {
                    var attr = VertexAttribute.TexCoord0 + ch;
                    if (!src.HasVertexAttribute(attr)) continue;
                    src.GetUVs(ch, v4);
                    var uvs = Pick(v4);
                    int dim = src.GetVertexAttributeDimension(attr);
                    if (dim <= 2) m.SetUVs(ch, uvs.Select(u => (Vector2)u).ToList());
                    else if (dim == 3) m.SetUVs(ch, uvs.Select(u => (Vector3)u).ToList());
                    else m.SetUVs(ch, uvs);
                }
                m.subMeshCount = subs.Count;
                for (int i = 0; i < subs.Count; i++) m.SetTriangles(subTris[i], i);    // with sub-mesh bounds (WaterVolume reads them)
                m.RecalculateBounds();
                string p = $"{Gen}/{m.name}.asset";
                AssetDatabase.DeleteAsset(p);
                AssetDatabase.CreateAsset(m, p);
                // no CPU copy in the build: nothing reads these at runtime (WaterVolume only reads the sub-mesh bounds,
                // the colliders use their own CollisionMesh); the editor (lightmap UVs) can still read the asset
                var mso = new SerializedObject(m);
                var rp = mso.FindProperty("m_IsReadable");
                if (rp != null) { rp.boolValue = false; mso.ApplyModifiedPropertiesWithoutUndo(); }
                return m;
            }
            var child = new GameObject(r.name.Replace("_LOD", "_SeeThroughLOD"));     // no "_LOD0" suffix: not a lightmap / collision part
            child.transform.SetParent(r.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = Part(clear, "SeeThrough");
            var cr = child.AddComponent<MeshRenderer>();
            cr.sharedMaterials = clear.Select(i => mats[i]).ToArray();
            cr.shadowCastingMode = r.shadowCastingMode;
            GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.OccludeeStatic);
            mf.sharedMesh = Part(solid, "Solid");
            r.sharedMaterials = solid.Select(i => mats[i]).ToArray();
            return cr;
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

        // ---------------------------------------------------------------- room 304 (first playable)

        static GameObject Point(string name, Vector3 pos, float yaw, Transform parent)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return g;
        }

        static void PointLight(string name, Vector3 pos, Color c, float intensity, float range, Transform parent, bool shadows)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = pos;
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        static void Room304(Room room, GameObject rex, GameObject game)
        {
            var inst = GameObject.Find("Island/Room304");
            if (inst == null) { Debug.LogWarning("[Rex Magnum] Room304 prefab not found in the scene."); return; }
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var gp = new GameObject("Room304_Gameplay").transform;
            // stuck door on a hinge
            var door = inst.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.EndsWith("_Door"));
            if (door != null)
            {
                GameObjectUtility.SetStaticEditorFlags(door.gameObject, 0);     // it moves: no static batching
                var hinge = Point("DoorHinge", U(room.doorHinge), 0f, gp);
                door.transform.SetParent(hinge.transform, true);
                door.gameObject.AddComponent<MeshCollider>();
                var hh = hinge.AddComponent<Health>();
                hh.maxHealth = 30f;
                hinge.AddComponent<AudioSource>().spatialBlend = 1f;
                hinge.AddComponent<StuckDoor>();
            }
            // kickable empty bottles
            foreach (var b in inst.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.Contains("_Bottle")))
            {
                b.gameObject.isStatic = false;
                GameObjectUtility.SetStaticEditorFlags(b.gameObject, 0);
                var mc = b.gameObject.AddComponent<MeshCollider>();
                mc.convex = true;
                var rb = b.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.35f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // small and fast: no tunnelling
            }
            rex.AddComponent<RexKick>();
            // pickups: full beer on the table, towel on the balcony rail
            foreach (var (prefab, at) in new[] { ("Pickup_Bottle", room.fullBottle), ("Pickup_Towel", room.towel) })
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/RexMagnum/Prefabs/Weapons/{prefab}.prefab");
                if (pf == null) continue;
                var p = (GameObject)PrefabUtility.InstantiatePrefab(pf, gp);
                p.transform.position = U(at);
            }
            // mirrors (closet faces -X in Unity, bathroom faces +X)
            foreach (var (n, at, normal) in new[] { ("ClosetMirror", room.closetMirror, Vector3.left), ("BathMirror", room.bathMirror, Vector3.right) })
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = n;
                UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(gp, false);
                q.transform.position = U(at) + normal * 0.012f;
                q.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
                q.transform.localScale = n == "ClosetMirror" ? new Vector3(1.4f, 2.0f, 1f) : new Vector3(1.3f, 1.0f, 1f);
                var pm = q.AddComponent<PlanarMirror>();
                pm.localNormal = Vector3.back;
            }
            // lights: lamps, ceiling, flickering TV
            var warm = new Color(1f, 0.8f, 0.55f);
            // soft values: the bedside lamps stand close to the walls (a strong point light there burns a white spot)
            PointLight("Lamp1", U(room.lamp1) + Vector3.down * 0.1f, warm, 0.45f, 3f, gp, false);
            PointLight("Lamp2", U(room.lamp2) + Vector3.down * 0.1f, warm, 0.45f, 3f, gp, false);
            PointLight("CeilingLight", U(room.ceiling), new Color(1f, 0.9f, 0.75f), 1.6f, 7f, gp, true);
            var tv = new GameObject("TVLight").AddComponent<Light>();
            tv.transform.SetParent(gp, false);
            tv.transform.position = U(room.tv);
            tv.type = LightType.Point;
            tv.range = 5f;
            tv.gameObject.AddComponent<LightFlicker>().baseIntensity = 0.6f;
            // wake-up points, respawn in the room (brief: Rex wakes in room 304 after dying)
            var bed = Point("BedLie", U(room.bedLie), 270f, gp);
            var wake = Point("WakePoint", U(room.wake), 270f, gp);
            var life = rex.GetComponent<RexLife>();
            if (life != null) life.respawnPoint = wake.transform;
            // reflection probe for the room (glass, bottles, mirror frames)
            var rp = new GameObject("RoomProbe").AddComponent<ReflectionProbe>();
            rp.transform.SetParent(gp, false);
            rp.transform.position = U(room.ceiling) + Vector3.down * 1.5f;
            rp.size = new Vector3(5.5f, 3.4f, 9.5f);
            rp.boxProjection = true;
            rp.mode = ReflectionProbeMode.Realtime;
            rp.refreshMode = ReflectionProbeRefreshMode.ViaScripting;     // InteriorProbes: load + dusk / dawn
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            if (gp.GetComponent<InteriorProbes>() == null) gp.gameObject.AddComponent<InteriorProbes>();
            rp.importance = 2;
            // title camera: slow shot of the hotel from the beach; then the flow takes over
            var tc = new GameObject("TitleCamera").AddComponent<Camera>();
            tc.transform.position = new Vector3(75f, 26f, -420f);
            tc.farClipPlane = 2500f;
            var tdata = tc.gameObject.AddComponent<UniversalAdditionalCameraData>();
            tdata.renderPostProcessing = true;
            var flow = game.AddComponent<GameFlow>();
            flow.titleCamera = tc;
            var rig = UnityEngine.Object.FindAnyObjectByType<RexCameraRig>();
            flow.playerCamera = rig != null ? rig.GetComponent<Camera>() : Camera.main;
            flow.rex = rex.GetComponent<RexController>();
            flow.bedPoint = bed.transform;
            flow.wakePoint = wake.transform;
            flow.disableDuringTitle = new Behaviour[] { rex.GetComponent<RexActions>(), rex.GetComponent<RexWeapons>(), rig };
        }

        // ---------------------------------------------------------------- beach: dive shop + tiki bar pickups

        /// <summary>Brief section 5: harpoon gun at the beach dive shop, exploding coconuts on the beach and at the
        /// tiki bar (+ a cocktail there). Placed on the floor found by a ray down inside each building.</summary>
        static void Beach(Transform world)
        {
            Physics.SyncTransforms();
            var root = new GameObject("Beach_Gameplay").transform;
            Vector3? Floor(string building, Vector3 offset)
            {
                var b = world.Find(building);
                if (b == null) return null;
                var rs = b.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) return null;
                var bounds = rs[0].bounds;
                foreach (var r in rs) bounds.Encapsulate(r.bounds);
                Vector3 from = new Vector3(bounds.center.x, bounds.min.y + 1.6f, bounds.center.z) + offset;
                if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 8f)) return hit.point;
                return new Vector3(from.x, bounds.min.y, from.z);
            }
            var dive = Floor("DiveShop", Vector3.zero);
            if (dive.HasValue)
            {
                Pickup("Pickup_Harpoon", dive.Value + Vector3.up * 0.9f, root);
                Pickup("Ammo_Harpoon", dive.Value + new Vector3(0.8f, 0.9f, 0f), root);
            }
            var tiki = Floor("TikiBar", Vector3.zero);
            if (tiki.HasValue)
            {
                Pickup("Pickup_Coconut", tiki.Value + new Vector3(0.9f, 1.0f, 0f), root);
                FoodPickup.Spawn(FoodPickup.Kind.Cocktail, tiki.Value + new Vector3(-0.9f, 1.0f, 0f), 120f, root);
            }
            if (dive.HasValue && tiki.HasValue)            // coconuts on the sand between the two
            {
                Vector3 mid = (dive.Value + tiki.Value) * 0.5f;
                if (Physics.Raycast(mid + Vector3.up * 30f, Vector3.down, out RaycastHit sand, 60f))
                    Pickup("Ammo_Coconut", sand.point + Vector3.up * 0.1f, root);
            }
            // blobs from the sea (brief section 4): walk south from the beach until the seabed is under water; the
            // spawner sits on the sea surface there (inside the pool NavMesh box, z > -405)
            Vector3 sea = U(40f, -380f, 0f);
            for (float y = -335f; y >= -400f; y -= 2f)
            {
                var q = U(40f, y, 0f);
                if (Physics.Raycast(q + Vector3.up * 60f, Vector3.down, out RaycastHit bed, 120f, ~0, QueryTriggerInteraction.Ignore) &&
                    bed.point.y < -0.8f) { sea = q; break; }
            }
            sea.y = 0f;                                       // sea level (Ocean at y 0)
            ZoneSpawner("BeachBlobs", sea, 4, 12f, root);
        }

        // ---------------------------------------------------------------- hotel: pickups + elevator

        static GameObject Pickup(string name, Vector3 at, Transform parent)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/RexMagnum/Prefabs/Weapons/{name}.prefab");
            if (pf == null) return null;
            var p = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            p.transform.position = at;
            return p;
        }

        /// <summary>Healing on the way (playtest: long walks between food): a small teak crate with two cocktails on
        /// top at ground point g. The drinks come back after 120 s.</summary>
        static void DrinkStand(string name, Vector3 g, Transform parent)
        {
            var wood = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/A_Teak.mat");
            var crate = Box(name, g + Vector3.up * 0.35f, new Vector3(0.9f, 0.7f, 0.6f), wood, parent);
            crate.isStatic = true;
            FoodPickup.Spawn(FoodPickup.Kind.Cocktail, g + new Vector3(-0.22f, 0.72f, 0f), 120f, parent);
            FoodPickup.Spawn(FoodPickup.Kind.Cocktail, g + new Vector3(0.22f, 0.72f, 0f), 120f, parent);
        }

        /// <summary>Ground under p (highest hit below p + 3 m), or p itself.</summary>
        static Vector3 GroundAt(Vector3 p)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : p;
        }

        /// <summary>One more blob spawner for a zone, with the same blob prefabs as the pool spawner.</summary>
        static BlobSpawner ZoneSpawner(string name, Vector3 at, int maxAlive, float radius, Transform parent)
        {
            // the pool spawner, not whichever zone spawner Find returns first
            var src = mainSpawner != null ? mainSpawner : UnityEngine.Object.FindAnyObjectByType<BlobSpawner>();
            if (src == null) return null;
            var sp = new GameObject(name).AddComponent<BlobSpawner>();
            sp.transform.SetParent(parent, false);
            sp.transform.position = at;
            sp.prefabs = src.prefabs;
            sp.maxAlive = maxAlive;
            sp.radius = radius;
            return sp;
        }

        static GameObject Box(string name, Vector3 center, Vector3 size, Material m, Transform parent)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(parent, false);
            b.transform.position = center;
            b.transform.localScale = size;
            if (m != null) b.GetComponent<Renderer>().sharedMaterial = m;
            return b;
        }

        static void Hotel(HotelPts h)
        {
            var root = new GameObject("Hotel_Gameplay").transform;
            // brief: pistol in the casino lounge, knife at the buffet, swordfish in the freezer, sniper on the roof
            Pickup("Pickup_Pistol", U(h.pistol) + Vector3.up * 0.05f, root);
            Pickup("Pickup_Knife", U(h.knife) + Vector3.up * 0.05f, root);
            Pickup("Pickup_Swordfish", U(h.swordfish) + Vector3.up * 0.1f, root);
            Pickup("Pickup_Sniper", U(h.sniper) + Vector3.up * 0.1f, root);
            Pickup("Ammo_Pistol", U(h.pistol) + new Vector3(0.6f, 0.05f, 0f), root);
            Pickup("Ammo_Sniper", U(h.sniper) + new Vector3(0.8f, 0.1f, 0f), root);
            // food + drinks heal Rex (brief section 8): buffet next to the knife, a cocktail on the lounge stage
            FoodPickup.Spawn(FoodPickup.Kind.Pizza, U(h.knife) + new Vector3(1.6f, 0.05f, 0f), 120f, root);
            FoodPickup.Spawn(FoodPickup.Kind.Fruit, U(h.knife) + new Vector3(-1.6f, 0.05f, 0f), 120f, root);
            if (h.stage != null && h.stage.Length == 3)
            {
                FoodPickup.Spawn(FoodPickup.Kind.Cocktail, U(h.stage) + new Vector3(0f, 0.85f, 0f), 120f, root);
                // Rex's lounge act (brief section 6): the hotel-bar piano plays from the stage, heard only near the lounge
                var lounge = Resources.Load<AudioClip>("Music/lounge_act");
                if (lounge != null)
                {
                    var spk = new GameObject("LoungeMusic");
                    spk.transform.SetParent(root, false);
                    spk.transform.position = U(h.stage) + Vector3.up * 1.5f;
                    var a = spk.AddComponent<AudioSource>();
                    a.clip = lounge;
                    a.loop = true;
                    a.playOnAwake = true;
                    a.spatialBlend = 1f;
                    a.rolloffMode = AudioRolloffMode.Linear;
                    a.minDistance = 4f;
                    a.maxDistance = 22f;
                    a.volume = 0.6f;
                }
            }
            // elevator: cab (floor, 3 walls, ceiling light) + a sliding landing door on every floor
            var steel = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/A_Aluminum.mat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/A_Teak.mat");
            Vector3 c = U(h.elevatorX[0], h.elevatorX[1], h.elevatorFloors[0]);
            var cab = new GameObject("ElevatorCab").transform;
            cab.SetParent(root, false);
            cab.position = c;
            Box("CabFloor", c + new Vector3(0, -0.05f, 0), new Vector3(2.9f, 0.1f, 3.2f), wood, cab);
            Box("CabBack", c + new Vector3(1.45f, 1.2f, 0), new Vector3(0.06f, 2.4f, 3.2f), steel, cab);
            Box("CabSideN", c + new Vector3(0, 1.2f, 1.6f), new Vector3(2.9f, 2.4f, 0.06f), steel, cab);
            Box("CabSideS", c + new Vector3(0, 1.2f, -1.6f), new Vector3(2.9f, 2.4f, 0.06f), steel, cab);
            Box("CabRoof", c + new Vector3(0, 2.42f, 0), new Vector3(2.9f, 0.06f, 3.2f), steel, cab);
            var l = new GameObject("CabLight").AddComponent<Light>();
            l.transform.SetParent(cab, false);
            l.transform.position = c + Vector3.up * 2.2f;
            l.type = LightType.Point; l.range = 4f; l.intensity = 1.5f; l.color = new Color(1f, 0.92f, 0.8f);
            var doors = new List<Transform>();
            float doorX = U(h.elevatorX[0], 0, 0).x - 1.6f - 0.22f;     // just outside the shaft's corridor wall
            foreach (float y in h.elevatorFloors.Take(h.elevatorFloors.Length - 1))
            {
                var d = Box("LandingDoor", new Vector3(doorX, y + 1.1f, c.z), new Vector3(0.05f, 2.2f, 1.3f), steel, root);
                doors.Add(d.transform);
            }
            var e = root.gameObject.AddComponent<Elevator>();
            e.cab = cab;
            e.floorHeights = h.elevatorFloors.Take(h.elevatorFloors.Length - 1).ToArray();
            e.landingDoors = doors.ToArray();
            // box-projected probes inside (lobby + every guest corridor): interiors reflect walls, not the bright sky.
            // Hotel centre: the pool doors are on the north facade (hotel-local y = 11), the hotel is 90 x 22 m.
            Vector3 hc = U(h.poolDoors) - new Vector3(0f, 0f, 11f);
            float fl0 = h.elevatorFloors[0];
            BoxProbe("LobbyProbe", new Vector3(hc.x, fl0 + 1.6f, hc.z), new Vector3(89f, 3.3f, 21.4f), 0.7f, root);
            float xa = hc.x - 39.8f, xb = doorX + 0.07f;          // stairwell side .. elevator shaft wall (hotel-local)
            for (int f = 1; f < h.elevatorFloors.Length - 1; f++)
                BoxProbe("CorridorProbe" + f, new Vector3((xa + xb) * 0.5f, h.elevatorFloors[f] + 1.6f, hc.z),
                         new Vector3(xb - xa, 3.3f, 3.2f), 0.6f, root);
        }

        /// <summary>Small realtime box-projected reflection probe for an interior, rendered once on load (64 px, short
        /// far clip). Importance 1: wins over the island probe (0), loses to room 304's own probe (2). The lower
        /// intensity dims the sky-bright specular ambient indoors.</summary>
        static void BoxProbe(string name, Vector3 center, Vector3 size, float intensity, Transform parent, bool dark = false)
        {
            var rp = new GameObject(name).AddComponent<ReflectionProbe>();
            rp.transform.SetParent(parent, false);
            rp.transform.position = center;
            rp.size = size;
            rp.boxProjection = true;
            rp.blendDistance = 1f;
            rp.mode = ReflectionProbeMode.Realtime;
            // by script (InteriorProbes): on load and again at dusk / dawn, not the noon sky all night
            rp.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            if (parent.GetComponent<InteriorProbes>() == null) parent.gameObject.AddComponent<InteriorProbes>();
            rp.resolution = 64;
            rp.importance = 1;
            rp.intensity = intensity;
            rp.farClipPlane = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) + 10f;
            rp.shadowDistance = 0f;            // six extra scene renders on load: skip their shadow maps
            if (dark)
            {
                rp.clearFlags = ReflectionProbeClearFlags.SolidColor;    // underground: no sky behind gaps in the rock
                rp.backgroundColor = new Color(0.02f, 0.02f, 0.02f);
            }
        }

        // ---------------------------------------------------------------- golf: carts, chargers, golf club

        static List<Vector3> Pts(string json, string key)
        {
            // "carts": [[x, y, z], ...] (JsonUtility cannot read nested arrays) -> parse by hand
            var res = new List<Vector3>();
            int i = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (i < 0) return res;
            int a = json.IndexOf('[', i), depth = 0, b = a;
            for (; b < json.Length; b++) { if (json[b] == '[') depth++; else if (json[b] == ']' && --depth == 0) break; }
            var nums = System.Text.RegularExpressions.Regex.Matches(json.Substring(a, b - a), @"-?\d+(\.\d+)?(e-?\d+)?");
            for (int k = 0; k + 2 < nums.Count; k += 3)
                res.Add(U(float.Parse(nums[k].Value, System.Globalization.CultureInfo.InvariantCulture),
                          float.Parse(nums[k + 1].Value, System.Globalization.CultureInfo.InvariantCulture),
                          float.Parse(nums[k + 2].Value, System.Globalization.CultureInfo.InvariantCulture)));
            return res;
        }

        static void Golf(string json, Vector3 resortSpawn)
        {
            int g = json.IndexOf("\"golf\"", StringComparison.Ordinal);
            if (g < 0) return;
            string golf = json.Substring(g);
            var root = new GameObject("Golf_Gameplay").transform;
            var club = Pts(golf, "golfClub");
            if (club.Count > 0) Pickup("Pickup_GolfClub", club[0] + Vector3.up * 0.1f, root);
            // drinks at the pro shop, beside the club
            var shop = Pts(golf, "proShop");
            if (shop.Count > 0) DrinkStand("ProShopDrinks", GroundAt(shop[0] + new Vector3(-1.6f, 0.5f, -1.2f)), root);
            // blobs on the course too (from the water hazards if any, else out of the rough), with their own NavMesh
            var tees = Pts(golf, "tees");
            if (tees.Count > 0)
            {
                Vector3 mid = Vector3.zero;
                foreach (var t in tees) mid += t;
                mid /= tees.Count;
                var gs = ZoneSpawner("GolfBlobs", GroundAt(mid), 4, 16f, root);
                if (gs != null)
                {
                    var gnav = gs.gameObject.AddComponent<RuntimeNavMesh>();
                    gnav.center = mid + new Vector3(0f, 0f, -15f);          // course + pro shop (tees -100..32, shop -126)
                    gnav.size = new Vector3(200f, 60f, 200f);
                }
            }
            foreach (var c in Pts(golf, "chargers"))
            {
                var t = new GameObject("ChargeStation").transform;
                t.SetParent(root, false);
                t.position = c;
                GolfCart.RegisterCharger(t);
                var cs = t.gameObject.AddComponent<ChargeStation>();
            }
            var cartFbx = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/Buildings/GolfCart.fbx");
            if (cartFbx == null) return;
            var cartPrefab = CartPrefab(cartFbx);
            var spots = Pts(golf, "carts");
            spots.Add(resortSpawn + new Vector3(-40f, 0.1f, -12f));        // one cart at the resort (charge station too)
            foreach (var p in spots)
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(cartPrefab, root);
                c.transform.position = p + Vector3.up * 0.05f;
            }
            var rc = new GameObject("ChargeStation").transform;
            rc.SetParent(root, false);
            rc.position = resortSpawn + new Vector3(-40f, 0.1f, -14f);
            rc.gameObject.AddComponent<ChargeStation>();
        }

        static void Factory(string json)
        {
            int f = json.IndexOf("\"factory\"", StringComparison.Ordinal);
            if (f < 0) return;
            string fac = json.Substring(f, Math.Min(600, json.Length - f));
            var core = Pts(fac, "core");
            if (core.Count == 0) return;
            var root = new GameObject("Factory_Gameplay").transform;
            // brief section 6: a golf-cart charge station at the ruins too, 8 m outside the factory door
            var door = Pts(fac, "door");
            var hallPt = Pts(fac, "hall");
            if (door.Count > 0 && hallPt.Count > 0)
            {
                Vector3 outward = Vector3.ProjectOnPlane(door[0] - hallPt[0], Vector3.up).normalized;
                Vector3 at = door[0] + outward * 8f;
                Physics.SyncTransforms();
                var hits = Physics.RaycastAll(at + Vector3.up * 30f, Vector3.down, 60f);
                if (hits.Length > 0) at = hits.OrderBy(h => h.point.y).First().point;   // lowest hit: the ground, not the ruins roof
                var ch = new GameObject("ChargeStation").transform;
                ch.SetParent(root, false);
                ch.position = at + Vector3.up * 0.05f;
                ch.gameObject.AddComponent<ChargeStation>();
                // drinks beside the charge station: heal before (or after) the factory fight
                Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
                DrinkStand("RuinsDrinks", GroundAt(at + side * 3f + Vector3.up * 0.5f), root);
            }
            var go = new GameObject("FactoryCore");
            go.transform.SetParent(root, false);
            go.transform.position = core[0];
            var col = go.AddComponent<SphereCollider>();
            col.radius = 2.0f;
            go.AddComponent<Health>();
            var fc = go.AddComponent<FactoryCore>();
            // the glowing core sphere is its own object in Ruins.fbx (Ruins_Core): it pulses and vanishes when destroyed
            var ruins = GameObject.Find("Island/Ruins");
            if (ruins != null)
            {
                PrefabUtility.UnpackPrefabInstance(ruins, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                fc.coreRenderer = ruins.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.EndsWith("_Core"));
            }
            var l = new GameObject("CoreLight").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.type = LightType.Point; l.color = new Color(0.4f, 1f, 0.3f); l.range = 18f; l.intensity = 4f;
            fc.coreLight = l;
            var hall = new List<Light>();
            foreach (var off in new[] { new Vector3(-9, 4, -5), new Vector3(9, 4, -5), new Vector3(-9, 4, 4), new Vector3(9, 4, 4) })
            {
                var hl = new GameObject("HallLight").AddComponent<Light>();
                hl.transform.SetParent(root, false);
                hl.transform.position = core[0] + off;
                hl.type = LightType.Point; hl.color = new Color(0.35f, 1f, 0.3f); hl.range = 10f; hl.intensity = 1.5f;
                hall.Add(hl);
            }
            fc.hallLights = hall.ToArray();
            // a blob spawner inside the factory (eggs hatching) until the core is destroyed
            var spawnerSrc = mainSpawner != null ? mainSpawner : UnityEngine.Object.FindAnyObjectByType<BlobSpawner>();
            if (spawnerSrc != null)
            {
                var sp = UnityEngine.Object.Instantiate(spawnerSrc.gameObject, root).GetComponent<BlobSpawner>();
                foreach (var c in sp.GetComponents<Component>())
                    if (!(c is Transform) && !(c is BlobSpawner)) UnityEngine.Object.DestroyImmediate(c);
                sp.transform.position = core[0] + new Vector3(0, -3f, -6f);
                var fnav = sp.gameObject.AddComponent<RuntimeNavMesh>();
                fnav.center = core[0];
                fnav.size = new Vector3(80f, 40f, 80f);
                sp.radius = 6f;
                sp.maxAlive = 4;
            }
        }

        /// <summary>A hanging board at the cave fork, read when walking in from the cave mouth: which way to the ship and
        /// to the ruins (players got lost in the dark cavern). The approach runs along the main tunnel's last lamp
        /// (lamps sit 2.5 m above the floor on the path points); arrows come from the branch end directions.</summary>
        static void TunnelSign(Vector3 fork, List<Vector3> lamps, Vector3 ruinsEnd, Vector3 shipEnd, Transform root)
        {
            Vector3 Flat(Vector3 p) { p.y = 0f; return p; }
            // the lamp 15 to 40 m before the fork on the main tunnel: the one farthest from both branch ends
            var prev = lamps.Where(p => { float dd = Vector3.Distance(Flat(p), Flat(fork)); return dd > 15f && dd < 40f; })
                .OrderByDescending(p => Mathf.Min(Vector3.Distance(p, ruinsEnd), Vector3.Distance(p, shipEnd)))
                .Select(p => (Vector3?)p).FirstOrDefault();
            if (prev == null) return;
            var dir = Flat(fork - prev.Value).normalized;
            if (dir.sqrMagnitude < 0.5f) return;
            string Arrow(Vector3 end, string label)
            {
                float ang = Vector3.SignedAngle(dir, Flat(end - fork), Vector3.up);    // + = to the right
                return ang > 30f ? label + "  -->" : ang < -30f ? "<--  " + label : "^  " + label + "  ^";
            }
            var sign = new GameObject("TunnelSign").transform;
            sign.SetParent(root, false);
            var at = Vector3.Lerp(prev.Value, fork + Vector3.up * 2.5f, 0.6f);
            sign.position = at + Vector3.up * 0.2f;                      // about 2.7 m above the floor: Rex walks under it
            sign.rotation = Quaternion.LookRotation(dir);                 // text and quad face the player walking in
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);     // a plank, not a quad: hides the text from behind
            UnityEngine.Object.DestroyImmediate(board.GetComponent<Collider>());
            board.name = "Board";
            board.transform.SetParent(sign, false);
            board.transform.localScale = new Vector3(2.6f, 1.1f, 0.05f);
            var bm = Asset("TunnelSign", Shader.Find("Universal Render Pipeline/Lit"));
            bm.SetColor("_BaseColor", new Color(0.18f, 0.11f, 0.06f));    // old painted plank
            bm.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(bm);
            board.GetComponent<MeshRenderer>().sharedMaterial = bm;
            // the words are painted into the face texture at build time (a dynamic-font TextMesh needed the GUI/Text shader
            // at runtime: unlit, its own ZTest quirks, glyph atlas rebuilds). A quad on the plank's front, normal lit material
            var face = SignTexture(new[] { Arrow(shipEnd, "SHIP"), Arrow(ruinsEnd, "RUINS") }, "TunnelSign");
            var oldTxt = Gen + "/TunnelSignText.mat";                       // the old font material: no longer used
            if (AssetDatabase.LoadAssetAtPath<Material>(oldTxt) != null) AssetDatabase.DeleteAsset(oldTxt);
            if (face == null) return;
            var fq = GameObject.CreatePrimitive(PrimitiveType.Quad);         // a quad faces -Z: towards the player walking in
            UnityEngine.Object.DestroyImmediate(fq.GetComponent<Collider>());
            fq.name = "Face";
            fq.transform.SetParent(sign, false);
            fq.transform.localPosition = new Vector3(0f, 0f, -0.027f);       // just in front of the plank's face (-0.025)
            fq.transform.localScale = new Vector3(2.5f, 1.0f, 1f);
            var fm = Asset("TunnelSignFace", Shader.Find("Universal Render Pipeline/Lit"));
            fm.SetTexture("_BaseMap", face);
            fm.SetColor("_BaseColor", Color.white);
            fm.SetFloat("_Smoothness", 0.2f);
            // luminous paint: the letters stay readable in the dark cavern (the brown ground glows next to nothing)
            fm.EnableKeyword("_EMISSION");
            fm.SetTexture("_EmissionMap", face);
            fm.SetColor("_EmissionColor", new Color(0.55f, 0.55f, 0.55f));
            fm.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(fm);
            var fr = fq.GetComponent<MeshRenderer>();
            fr.sharedMaterial = fm;
            fr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // 5 x 7 glyphs for the sign's words (only what the arrows / names use)
        static readonly Dictionary<char, string[]> SignGlyphs = new Dictionary<char, string[]>
        {
            ['S'] = new[] { " ### ", "#   #", "#    ", " ### ", "    #", "#   #", " ### " },
            ['H'] = new[] { "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
            ['I'] = new[] { " ### ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
            ['P'] = new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " },
            ['R'] = new[] { "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #" },
            ['U'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
            ['N'] = new[] { "#   #", "##  #", "# # #", "# # #", "#  ##", "#   #", "#   #" },
            ['<'] = new[] { "   # ", "  #  ", " #   ", "#    ", " #   ", "  #  ", "   # " },
            ['>'] = new[] { " #   ", "  #  ", "   # ", "    #", "   # ", "  #  ", " #   " },
            ['-'] = new[] { "     ", "     ", "     ", "#####", "     ", "     ", "     " },
            ['^'] = new[] { "  #  ", " # # ", "#   #", "     ", "     ", "     ", "     " },
        };

        /// <summary>Paints text lines (block letters, anti-aliased) on an old plank colour with a little grain and saves
        /// it as Generated/{name}.png. CPU only: works in batch mode without a GPU. 1024 x 512 on a 2.5 x 1 m face.</summary>
        static Texture2D SignTexture(string[] lines, string name)
        {
            const int W = 1024, H = 512;
            int maxChars = Mathf.Max(1, lines.Max(l => l.Length));
            // glyph cell 6 x 9 "dots"; the face is 2.5 : 1 and the texture 2 : 1, so a dot is 0.8 as wide as tall
            float sy = Mathf.Min(400f / (lines.Length * 9f), 940f / (maxChars * 6f * 0.8f)), sx = sy * 0.8f;
            float top = (H + (lines.Length * 9f - 2f) * sy) * 0.5f;        // first line's top, block centred (texture y grows upwards)
            var bg = new Color(0.17f, 0.105f, 0.055f);
            var ink = new Color(1f, 0.83f, 0.38f);
            var px = new Color[W * H];
            bool Dot(float gx, float gy)                                     // gx, gy in texture pixels
            {
                float ly = (top - gy) / (9f * sy);
                int li = Mathf.FloorToInt(ly);
                if (li < 0 || li >= lines.Length) return false;
                string ln = lines[li];
                float x0 = (W - ln.Length * 6f * sx) * 0.5f;
                float cx = (gx - x0) / (6f * sx);
                int ci = Mathf.FloorToInt(cx);
                if (ci < 0 || ci >= ln.Length || !SignGlyphs.TryGetValue(ln[ci], out var g)) return false;
                int dx = Mathf.FloorToInt((cx - ci) * 6f), dy = Mathf.FloorToInt((ly - li) * 9f);
                return dx < 5 && dy < 7 && g[dy][dx] == '#';
            }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int cov = 0;                                             // 3 x 3 samples: soft letter edges
                    for (int sj = 0; sj < 3; sj++)
                        for (int si = 0; si < 3; si++)
                            if (Dot(x + (si + 0.5f) / 3f, y + (sj + 0.5f) / 3f)) cov++;
                    // grain along the plank, and worn paint
                    float grain = Mathf.PerlinNoise(x * 0.004f, y * 0.09f) * 0.5f + Mathf.PerlinNoise(x * 0.02f, y * 0.4f) * 0.5f;
                    float wear = Mathf.Clamp01(Mathf.PerlinNoise(x * 0.03f + 17f, y * 0.03f + 5f) * 1.6f - 0.25f);
                    var c = bg * Mathf.Lerp(0.75f, 1.2f, grain);
                    c = Color.Lerp(c, ink * Mathf.Lerp(0.8f, 1f, wear), cov / 9f * Mathf.Lerp(0.75f, 1f, wear));
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            return SaveGenPng(tex, name, false);
        }

        /// <summary>Saves a generated texture as Generated/{name}.png (and destroys the in-memory one), imports it with
        /// mips (sRGB colour or a normal map) and returns the asset.</summary>
        static Texture2D SaveGenPng(Texture2D tex, string name, bool normal)
        {
            string png = $"{Gen}/{name}.png";
            File.WriteAllBytes(png, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(png) is TextureImporter ti)
            {
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !normal;
                ti.alphaSource = TextureImporterAlphaSource.None;
                ti.mipmapEnabled = true;
                ti.wrapMode = normal ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                ti.anisoLevel = 4;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        }

        static Transform DoorPair(string name, Vector3 a, Vector3 aOut, Vector3 b, Vector3 bOut, Transform root)
        {
            ZoneDoor Make(string n, Vector3 at)
            {
                var g = new GameObject(n);
                g.transform.SetParent(root, false);
                g.transform.position = at + Vector3.up * 1.2f;
                var bc = g.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(3f, 2.6f, 1.2f);
                return g.AddComponent<ZoneDoor>();
            }
            Transform Exit(string n, Vector3 at, Vector3 lookFrom)
            {
                var t = new GameObject(n).transform;
                t.SetParent(root, false);
                t.position = at;
                var d = at - lookFrom; d.y = 0;
                if (d.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(d.normalized);
                return t;
            }
            var da = Make(name + "_DoorA", a);
            var db = Make(name + "_DoorB", b);
            da.exit = Exit(name + "_ExitB", bOut, b);
            db.exit = Exit(name + "_ExitA", aOut, a);
            return da.transform;
        }

        static void Tunnels(string json, Terrain terrain)
        {
            int f = json.IndexOf("\"tunnels\"", StringComparison.Ordinal);
            if (f < 0) return;
            string tj = json.Substring(f);
            var main = Pts(tj, "main");
            var ruins = Pts(tj, "ruins");
            var ship = Pts(tj, "ship");
            if (main.Count < 2) return;
            var root = new GameObject("Tunnels_Gameplay").transform;
            float H(Vector3 p) => terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : p.y;
            // cave mouth (behind the small waterfall) <-> start of the main tunnel
            var mouth = U(-160f, 232f, 0f); mouth.y = H(mouth);
            var mouthOut = U(-160f, 222f, 0f); mouthOut.y = H(mouthOut) + 0.1f;
            DoorPair("Cave", mouth, mouthOut, main[0], main[0] + (main[1] - main[0]).normalized * 4f, root);
            // ruins branch end <-> the factory's back corner (inside the temple hall)
            int fac = json.IndexOf("\"factory\"", StringComparison.Ordinal);
            if (ruins.Count >= 2 && fac >= 0)
            {
                var hall = Pts(json.Substring(fac, Math.Min(600, json.Length - fac)), "hall");
                if (hall.Count > 0)
                    DoorPair("RuinsTunnel", ruins[1], ruins[1] + (ruins[0] - ruins[1]).normalized * 4f,
                             hall[0] + new Vector3(-10f, 0f, 8f), hall[0] + new Vector3(-6f, 0f, 2f), root);   // exit on open floor, not in a slime vat
            }
            // ship branch end <-> a rock crack beside the crash site
            if (ship.Count >= 2)
            {
                var top = U(10f, 290f, 0f); top.y = H(top);
                var topOut = U(14f, 296f, 0f); topOut.y = H(topOut) + 0.1f;
                DoorPair("ShipTunnel", ship[1], ship[1] + (ship[0] - ship[1]).normalized * 4f, top, topOut, root);
            }
            // warm work lights along the tunnels (old miners' lamps), the cavern glows green
            var lights = Pts(tj, "lights");
            for (int i = 0; i < lights.Count; i++)
            {
                var l = new GameObject("TunnelLamp").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.position = lights[i];
                l.type = LightType.Point;
                l.range = 14f;
                l.intensity = 2.2f;
                l.color = i % 5 == 4 ? new Color(0.4f, 1f, 0.35f) : new Color(1f, 0.75f, 0.45f);
                l.shadows = i % 3 == 0 ? LightShadows.Soft : LightShadows.None;
            }
            if (ruins.Count >= 2 && ship.Count >= 2) TunnelSign(main[main.Count - 1], lights, ruins[1], ship[1], root);
            // one dark box probe per tunnel branch (axis-aligned box around the branch, 4 m margin)
            foreach (var (pn, seg) in new[] { ("TunnelProbeMain", main), ("TunnelProbeRuins", ruins), ("TunnelProbeShip", ship) })
            {
                if (seg.Count < 2) continue;
                Vector3 a = seg[0], b = seg[seg.Count - 1];
                var size = new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z)) + Vector3.one * 8f;
                BoxProbe(pn, (a + b) * 0.5f + Vector3.up * 1.5f, size, 0.4f, root, true);
            }
            var spawner = mainSpawner != null ? mainSpawner : UnityEngine.Object.FindAnyObjectByType<BlobSpawner>();
            if (spawner != null && main.Count >= 2)
            {
                var sp = new GameObject("TunnelBlobs").AddComponent<BlobSpawner>();
                sp.transform.SetParent(root, false);
                sp.transform.position = main[1];
                sp.prefabs = spawner.prefabs;
                sp.maxAlive = 3;
                sp.radius = 10f;
                var nav = sp.gameObject.AddComponent<RuntimeNavMesh>();      // blobs need a NavMesh down here too
                nav.center = (main[0] + main[main.Count - 1]) * 0.5f;
                nav.size = new Vector3(260f, 80f, 260f);
            }
        }

        /// <summary>King Glorp's crown (brief: spikes grow from his head). The spikes in his body mesh are part of the
        /// one smooth skin, so they cannot scale; this ring of gold spikes sits on the top bone, starts small and grows
        /// each fight phase (KingGlorpBoss.PhaseChange scales it).</summary>
        static Transform GlorpCrown(Blob g)
        {
            var smr = g.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null) return null;
            Transform top = smr.bones != null && smr.bones.Length > 0
                ? smr.bones.Where(b => b != null).OrderByDescending(b => b.position.y).First()
                : g.transform;
            var b = smr.bounds;
            var crown = new GameObject("Crown").transform;
            crown.SetParent(top, false);
            crown.position = new Vector3(b.center.x, b.max.y - b.size.y * 0.04f, b.center.z);
            crown.rotation = Quaternion.identity;
            crown.localScale = Vector3.one * 0.45f;
            float r = Mathf.Min(b.extents.x, b.extents.z) * 0.38f, hgt = b.size.y * 0.16f, w = b.size.y * 0.04f;
            // one mesh: 7 spikes, 8-sided cones leaning out a little
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 c0 = dir * r, tip = c0 + dir * hgt * 0.25f + Vector3.up * hgt;
                int start = v.Count;
                for (int k = 0; k < 8; k++)
                {
                    float q = k / 8f * Mathf.PI * 2f;
                    v.Add(c0 + new Vector3(Mathf.Cos(q), 0f, Mathf.Sin(q)) * w);
                }
                v.Add(tip);
                for (int k = 0; k < 8; k++) { tri.Add(start + k); tri.Add(start + 8); tri.Add(start + (k + 1) % 8); }
            }
            var mesh = new Mesh { name = "GlorpCrown" };
            mesh.SetVertices(v);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            const string crownPath = "Assets/RexMagnum/Prefabs/Blobs/GlorpCrown.asset";
            AssetDatabase.DeleteAsset(crownPath);                // rebuilds replace the old mesh
            AssetDatabase.CreateAsset(mesh, crownPath);
            crown.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            crown.gameObject.AddComponent<MeshRenderer>().sharedMaterial = smr.sharedMaterial;   // same gold jelly
            return crown;
        }

        static void Boss(Terrain terrain)
        {
            var glorp = AssetDatabase.LoadAssetAtPath<Blob>("Assets/RexMagnum/Prefabs/Blobs/Blob_KingGlorp.prefab");
            if (glorp == null) return;
            var at = U(55f, 278f, 0f);                         // in front of the crashed ship, on the plateau
            if (terrain != null) at.y = terrain.SampleHeight(at) + terrain.transform.position.y;
            var g = (Blob)PrefabUtility.InstantiatePrefab(glorp);
            g.name = "KingGlorp";
            g.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 200f, 0f));
            var boss = g.gameObject.AddComponent<KingGlorpBoss>();
            boss.crown = g.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Contains("Crown") || t.name.Contains("Spike"));
            if (boss.crown == null) boss.crown = GlorpCrown(g);
            var tourist = AssetDatabase.LoadAssetAtPath<Blob>("Assets/RexMagnum/Prefabs/Blobs/Blob_Tourist.prefab");
            boss.minionPrefabs = tourist != null ? new[] { tourist } : new Blob[0];
            // props for him to absorb as armor: deck chairs, barrels, crates around the crash site
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/A_ShipHull.mat");
            var rng = new System.Random(7);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                var p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (10f + (float)rng.NextDouble() * 10f);
                if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.5f;
                var c = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Cylinder);
                c.name = "ShipDebris";
                c.transform.position = p;
                c.transform.localScale = new Vector3(0.9f, 0.6f, 0.9f) * (0.8f + (float)rng.NextDouble() * 0.6f);
                if (mat != null) c.GetComponent<Renderer>().sharedMaterial = mat;
                c.AddComponent<Rigidbody>().mass = 30f;
            }
            // drinks on the ship approach, by the tunnel's rock-crack exit (~45 m out: before the fight starts)
            var approach = U(14f, 293f, 0f);
            if (terrain != null) approach.y = terrain.SampleHeight(approach) + terrain.transform.position.y;
            DrinkStand("ShipApproachDrinks", GroundAt(approach + Vector3.up * 0.5f), g.transform.parent);
            var nav = new GameObject("ShipNavMesh").AddComponent<RuntimeNavMesh>();
            nav.center = at;
            nav.size = new Vector3(120f, 60f, 120f);
        }

        static GameObject CartPrefab(GameObject fbx)
        {
            var root = new GameObject("GolfCart");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            StripImportedLODs(model);
            ApplyFix(model.transform);
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                if (r.name.EndsWith("_LOD1") || r.name.EndsWith("_LOD2")) r.gameObject.SetActive(false);
            // the cart FBX has its vertices where the first cart was parked (world coordinates): move the body to
            // this prefab's origin (wheels on the ground), where the collider and the seat are
            var body = model.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.EndsWith("_LOD0")).ToArray();
            if (body.Length > 0)
            {
                var bb = body[0].bounds;
                foreach (var r in body) bb.Encapsulate(r.bounds);
                // the cart's origin in Blender is "ground under the middle" at the first parking point (golf.carts[0]):
                // offset by that point (bb.min.y also held an A_RoomLight submesh 1.33 m under the wheels)
                Vector3 park = new Vector3(bb.center.x, bb.min.y, bb.center.z);
                string jsonPath = Dir + "/Island.json";
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    int g = json.IndexOf("\"golf\"", StringComparison.Ordinal);
                    var carts = g >= 0 ? Pts(json.Substring(g), "carts") : new List<Vector3>();
                    // sanity: the point must be at the model (else keep the bounds guess)
                    if (carts.Count > 0 && new Vector2(carts[0].x - bb.center.x, carts[0].z - bb.center.z).magnitude < 3f) park = carts[0];
                }
                model.transform.position -= park - root.transform.position;
            }
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.95f, 0.02f);
            box.size = new Vector3(1.3f, 1.4f, 2.45f);
            root.AddComponent<Rigidbody>();
            var seat = new GameObject("Seat").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = new Vector3(-0.25f, 0.45f, -0.1f);
            var cart = root.AddComponent<GolfCart>();
            cart.seat = seat;
            var lights = new List<Light>();
            foreach (float x in new[] { -0.4f, 0.4f })
            {
                var l = new GameObject("Headlight").AddComponent<Light>();
                l.transform.SetParent(root.transform, false);
                l.transform.localPosition = new Vector3(x, 0.75f, 1.3f);
                l.type = LightType.Spot; l.range = 25f; l.spotAngle = 60f; l.intensity = 3f;
                lights.Add(l);
            }
            cart.headlights = lights.ToArray();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/GolfCart.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------- guests

        /// <summary>Hotel-local point (Blender x, y; floor index) to Unity world.</summary>
        static Vector3 HotelPt(HotelPts h, float x, float y, int floor) => U(x + 40f, y - 300f, h.elevatorFloors[floor]);

        static void Guests(HotelPts h, Vector3 pool)
        {
            FoodPickup.Spawn(FoodPickup.Kind.Cocktail, pool + new Vector3(8.5f, 0.9f, -6f), 120f);   // swim-up bar (away from Fin, so Use does one thing)
            var prefabs = GuestBuilder.Build();
            if (prefabs.Count == 0) return;
            var root = new GameObject("Guests").transform;
            // where they are when the chaos starts (brief: hiding in rooms, closets, under chairs; at the pool)
            var spots = new (string who, Vector3 at, float yaw)[]
            {
                ("Carmen", HotelPt(h, -2f, 0.8f, 0), 180f),                  // reception desk, facing the doors
                ("Heather", HotelPt(h, -9.5f, -6.5f, 0), 30f),               // lobby sofas
                ("Justin", HotelPt(h, 15f, -5.5f, 0), 0f),                   // casino card table
                ("Gerald", HotelPt(h, 30f, -4.5f, 0), 0f),                   // buffet
                ("Lucy", HotelPt(h, -10f, 0.5f, 3), 90f),                    // 4th-floor corridor (hiding)
                ("Callie", pool + new Vector3(-20f, 0f, 14.5f), 200f),       // pool lounger
                ("Fin", pool + new Vector3(12f, 0f, -4f), 270f),             // by the swim-up bar
                // 12 extra guests
                ("Marge", HotelPt(h, -33f, -6f, 0), 0f),                     // lounge table, waiting for the show
                ("Dwayne", HotelPt(h, 20f, 5f, 1), 0f),                      // gym
                ("Priya", pool + new Vector3(-10f, 0f, 14.5f), 200f),        // pool lounger
                ("Hiro", HotelPt(h, 17.5f, 2.5f, 0), 180f),                  // roulette table
                ("Brenda", pool + new Vector3(5f, 0f, -14.5f), 20f),         // pool lounger (south row)
                ("Tyler", pool + new Vector3(-38f, 7.2f, 14f), 160f),        // top of the water slide
                ("Rosa", HotelPt(h, -30.5f, 4.9f, 0), 90f),                  // lounge bar stool
                ("Walter", HotelPt(h, 14f, -4f, 0) + new Vector3(1.4f, 0f, 0f), 270f),   // card table
                ("Amara", pool + new Vector3(30f, 0f, 12f), 180f),           // by the hot tubs
                ("Chad", pool + new Vector3(30f, 0.8f, -18f), 0f),           // on the stage
                ("Doris", HotelPt(h, 20f, -6f, 1), 0f),                      // spa
                ("Earl", HotelPt(h, 31.5f, 2.5f, 0), 180f),                  // buffet (second counter)
            };
            var hides = new List<Transform>();
            foreach (var p in new[] { HotelPt(h, -13f, 9.5f, 0), HotelPt(h, 7f, 9f, 0), HotelPt(h, 25f, -9.5f, 0),
                                      HotelPt(h, -20f, -8f, 0), HotelPt(h, 36.5f, 8f, 0), HotelPt(h, -30f, 0f, 3),
                                      HotelPt(h, 20f, 0f, 1) })
            {
                var g = new GameObject("HideSpot").transform;
                g.SetParent(root, false);
                g.position = p;
                hides.Add(g);
            }
            var rewards = new[] { "Ammo_Pistol", "Ammo_Sniper", "Ammo_Harpoon", "Pickup_Coconut" }
                .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/RexMagnum/Prefabs/Weapons/{n}.prefab"))
                .Where(x => x != null).ToArray();
            void Place(string who, Vector3 at, float yaw, int outfit)
            {
                if (!prefabs.TryGetValue(who, out var pf)) return;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(pf, root);
                g.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                var guest = g.GetComponent<Guest>();
                guest.hideSpots = hides.ToArray();
                guest.rewardPrefabs = rewards;
                if (outfit != 0) g.AddComponent<GuestOutfit>().seed = outfit;
            }
            foreach (var (who, at, yaw) in spots) Place(who, at, yaw, 0);
            // owner request: reuse the bodies around the island with new outfits (not Carmen: one security chief)
            var bodies = prefabs.Keys.Where(n => n != "Carmen").OrderBy(n => n).ToArray();
            var extra = new List<Vector3>();
            for (int fl = 1; fl <= 4; fl++)                       // hotel corridors, every floor
                foreach (float x in new[] { -25f, 5f, 28f }) extra.Add(HotelPt(h, x, 0.4f, fl));
            for (int i = 0; i < 10; i++) extra.Add(pool + new Vector3(-30f + i * 6.5f, 0f, i % 2 == 0 ? 15f : -15f));
            var ter = Terrain.activeTerrain;
            for (int i = 0; i < 8; i++)                                                                   // beach: on the sand
            {
                var b = pool + new Vector3(-40f + i * 12f, -3.5f, -95f);
                if (ter != null)
                {
                    // never in the sea: walk up the beach (north) until the sand is dry
                    for (int k = 0; k < 12; k++)
                    {
                        b.y = ter.SampleHeight(b) + ter.transform.position.y;
                        if (b.y >= 0.6f) break;
                        b.z += 3f;
                    }
                }
                extra.Add(b);
            }
            extra.Add(HotelPt(h, -6f, -8f, 0)); extra.Add(HotelPt(h, 12f, 7.5f, 0)); extra.Add(HotelPt(h, 22f, 7.5f, 0));
            var rngE = new System.Random(304);
            for (int i = 0; i < extra.Count; i++)
            {
                var p = extra[i];
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas)) p = hit.position;
                Place(bodies[rngE.Next(bodies.Length)], p, rngE.Next(360), 1000 + i);
            }
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
            // palms / trees: a cross-quad picture of the tree far away (the terrain's own billboards only work
            // with SpeedTree shaders, so LODGroup trees drew the full mesh out to treeDistance)
            var far = pr.kind == "palm" || pr.kind == "tree" ? TreeImpostor(mesh, mats, pr.name) : null;
            if (far != null)
            {
                var fg = new GameObject(pr.name + "_Far");
                fg.transform.SetParent(root.transform, false);
                fg.AddComponent<MeshFilter>().sharedMesh = far.Value.mesh;
                var fr = fg.AddComponent<MeshRenderer>();
                fr.sharedMaterial = far.Value.mat;
                fr.shadowCastingMode = ShadowCastingMode.Off;      // far beyond the shadow distance anyway
                lg.SetLODs(new[] { new LOD(0.06f, new Renderer[] { r }), new LOD(0.008f, new Renderer[] { fr }) });
                lg.fadeMode = LODFadeMode.CrossFade;        // dithered blend mesh <-> picture instead of a pop
                lg.animateCrossFading = true;               // time-based: the LODs' fade widths stay 0
            }
            else lg.SetLODs(new[] { new LOD(pr.kind == "fern" ? 0.02f : 0.008f, new Renderer[] { r }) });
            if (pr.kind == "palm" || pr.kind == "tree")
            {
                var cc = root.AddComponent<CapsuleCollider>();
                cc.radius = pr.kind == "palm" ? 0.25f : 0.4f;
                cc.height = 6f;
                cc.center = new Vector3(0f, 3f, 0f);
            }
            else if (pr.kind == "rock")
            {
                // boulders are solid too (terrain trees only take capsule colliders): a squat capsule from the mesh size
                var b = mesh.bounds;
                var cc = root.AddComponent<CapsuleCollider>();
                cc.radius = Mathf.Max(0.2f, Mathf.Min(b.extents.x, b.extents.z) * 0.9f);
                cc.height = Mathf.Max(cc.radius * 2f, b.size.y);
                cc.center = new Vector3(b.center.x, b.center.y, b.center.z);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/Veg_{pr.name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Far LOD for a tree: renders the mesh once from the side (orthographic, flat white ambient, no
        /// lights, so the picture is close to plain albedo and the sun / night still light it in game) into a 256 px
        /// cut-out texture, and puts it on two crossed quads. Null when the capture fails.</summary>
        static (Mesh mesh, Material mat)? TreeImpostor(Mesh mesh, Material[] mats, string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;     // -nographics batch build
            const int Res = 256;
            const int CaptureLayer = 31;
            var key = new Color(1f, 0f, 1f);       // chroma key: alpha from the background colour (robust with any RT alpha)
            var b = mesh.bounds;
            float size = Mathf.Max(b.size.y, Mathf.Max(b.size.x, b.size.z)) * 1.02f;
            var origin = new Vector3(0f, -5000f, 0f);
            var go = new GameObject("ImpostorCapture");
            var cam = new GameObject("ImpostorCamera").AddComponent<Camera>();
            var rt = new RenderTexture(Res, Res, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled).ToArray();
            bool fog = RenderSettings.fog;
            var probe = RenderSettings.ambientProbe;
            var prevActive = RenderTexture.active;
            var aoOff = new List<ScriptableRendererFeature>();      // SSAO darkened the picture (baked-in, doubled in game)
            Texture2D tex;
            try
            {
                go.layer = CaptureLayer;
                go.transform.position = origin;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                foreach (var l in lights) l.enabled = false;
                RenderSettings.fog = false;
                foreach (var f in AOFeatures()) if (FeatureOn(f)) { SetFeature(f, false); aoOff.Add(f); }
                var sh = new SphericalHarmonicsL2();
                sh.AddAmbientLight(Color.white);
                RenderSettings.ambientProbe = sh;
                cam.enabled = false;
                cam.orthographic = true;
                cam.orthographicSize = size * 0.5f;
                cam.aspect = 1f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = key;
                cam.cullingMask = 1 << CaptureLayer;
                cam.allowMSAA = false;
                cam.allowHDR = false;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = size * 4f;
                cam.transform.position = origin + b.center + Vector3.back * size * 2f;
                cam.transform.rotation = Quaternion.identity;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(Res, Res, TextureFormat.RGBA32, true);
                tex.ReadPixels(new Rect(0, 0, Res, Res), 0, 0);
            }
            catch (Exception e) { Debug.LogWarning($"[Rex Magnum] Tree far LOD for {name} skipped: {e.Message}"); return null; }
            finally
            {
                RenderTexture.active = prevActive;
                cam.targetTexture = null;
                foreach (var l in lights) if (l != null) l.enabled = true;
                foreach (var f in aoOff) SetFeature(f, true);
                RenderSettings.fog = fog;
                RenderSettings.ambientProbe = probe;
                UnityEngine.Object.DestroyImmediate(cam.gameObject);
                UnityEngine.Object.DestroyImmediate(go);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
            // key out the background; give the cut-out pixels the mean leaf colour (no pink fringes in small mips)
            var px = tex.GetPixels();
            Color sum = Color.clear;
            int solid = 0;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                bool bg = Mathf.Abs(c.r - key.r) + c.g + Mathf.Abs(c.b - key.b) < 0.25f;
                if (bg) px[i].a = 0f;
                else { px[i].a = 1f; sum += c; solid++; }
            }
            UnityEngine.Object.DestroyImmediate(tex);
            // nothing drawn, or no background left (no GPU in batch mode, failed render): keep the single LOD
            if (solid < 50 || solid > px.Length * 0.9f) { Debug.LogWarning($"[Rex Magnum] Tree far LOD for {name}: bad capture, skipped."); return null; }
            var mean = sum / solid;
            mean.a = 0f;
            for (int i = 0; i < px.Length; i++) if (px[i].a == 0f) px[i] = mean;
            var outTex = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
            outTex.SetPixels(px);
            outTex.Apply();
            string png = $"{Gen}/{name}_Far.png";
            File.WriteAllBytes(png, outTex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(outTex);
            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(png) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true;
                ti.mipMapsPreserveCoverage = true;          // far trees must not thin out in the small mips
                ti.alphaTestReferenceValue = 0.5f;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = Res;
                ti.SaveAndReimport();
            }
            var farTex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            if (farTex == null) return null;
            var m = Asset(name + "_Far", Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", farTex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            // two crossed quads (X and Z planes) through the trunk; normals up and tilted out to the quad's side (a
            // round crown: both faces take the same light, and a low dawn / dusk sun does not leave far trees black
            // as straight-up normals did)
            float h = size * 0.5f;
            Vector3 c0 = b.center;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var nrm = new List<Vector3>();
            var tri = new List<int>();
            foreach (var axis in new[] { Vector3.right, Vector3.forward })
            {
                int o = v.Count;
                Vector3 nl = (Vector3.up * 0.5f - axis).normalized, nr = (Vector3.up * 0.5f + axis).normalized;
                v.Add(c0 - axis * h - Vector3.up * h); uv.Add(new Vector2(0f, 0f)); nrm.Add(nl);
                v.Add(c0 + axis * h - Vector3.up * h); uv.Add(new Vector2(1f, 0f)); nrm.Add(nr);
                v.Add(c0 + axis * h + Vector3.up * h); uv.Add(new Vector2(1f, 1f)); nrm.Add(nr);
                v.Add(c0 - axis * h + Vector3.up * h); uv.Add(new Vector2(0f, 1f)); nrm.Add(nl);
                tri.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 });
            }
            var q = new Mesh { name = name + "_Far" };
            q.SetVertices(v);
            q.SetUVs(0, uv);
            q.SetTriangles(tri, 0);
            q.SetNormals(nrm);
            q.RecalculateBounds();
            q.RecalculateTangents();
            string mp = $"{Gen}/{name}_Far.asset";
            AssetDatabase.DeleteAsset(mp);
            AssetDatabase.CreateAsset(q, mp);
            return (q, m);
        }

        /// <summary>The SSAO features of the renderers the editor draws with (the impostor capture turns them off).</summary>
        static IEnumerable<ScriptableRendererFeature> AOFeatures()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) yield break;
            var list = new SerializedObject(urp).FindProperty("m_RendererDataList");
            for (int i = 0; list != null && i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererData data)
                    foreach (var f in data.rendererFeatures)
                        if (f != null && f.GetType().Name.Contains("AmbientOcclusion")) yield return f;
        }

        // by reflection: the check build's older URP has no isActive / SetActive
        static bool FeatureOn(ScriptableRendererFeature f) => !(f.GetType().GetProperty("isActive")?.GetValue(f) is bool on) || on;
        static void SetFeature(ScriptableRendererFeature f, bool on) =>
            f.GetType().GetMethod("SetActive", new[] { typeof(bool) })?.Invoke(f, new object[] { on });

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

        /// <summary>True when a baked normal map has (almost) no relief: std of red / green under 0.01 (the stucco bake
        /// came out flat, std 0.002: the hotel walls read as painted cardboard up close).</summary>
        static bool FlatNormalMap(string file)
        {
            string path = Dir + "/" + file;
            if (string.IsNullOrEmpty(file) || !File.Exists(path)) return true;
            var px = LoadPng(path, out _, out _);
            if (px.Length == 0) return true;
            double sr = 0, sg = 0, qr = 0, qg = 0;
            foreach (var c in px) { sr += c.r; sg += c.g; qr += c.r * c.r; qg += c.g * c.g; }
            double n = px.Length, vr = qr / n - (sr / n) * (sr / n), vg = qg / n - (sg / n) * (sg / n);
            return Math.Sqrt(Math.Max(vr, vg)) / 255.0 < 0.01;
        }

        /// <summary>Procedural roughcast stucco normal map (tileable value noise, three octaves + fine grit), saved as
        /// Generated/A_Stucco_nrm_gen.png. Used only while the baked one is flat.</summary>
        static Texture2D StuccoNormal()
        {
            const int N = 512;
            var hgt = new float[N * N];
            // tileable: the four wrapped Perlin samples blended by position (seamless at the edges)
            float Tile(float x, float y, float f)
            {
                float u = x / N, v = y / N, o = 300f;                          // o: keeps the shifted samples positive
                float n00 = Mathf.PerlinNoise(x * f + o, y * f + o), n10 = Mathf.PerlinNoise((x - N) * f + o, y * f + o);
                float n01 = Mathf.PerlinNoise(x * f + o, (y - N) * f + o), n11 = Mathf.PerlinNoise((x - N) * f + o, (y - N) * f + o);
                return Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v);
            }
            var rng = new System.Random(304);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float blobs = Tile(x, y, 1f / 24f), mid = Tile(x, y, 1f / 9f), fine = Tile(x, y, 1f / 3f);
                    // trowel blobs: flattened tops (plaster pressed smooth), grit between them
                    float hv = Mathf.SmoothStep(0.3f, 0.75f, blobs) * 0.55f + mid * 0.3f + fine * 0.15f;
                    hgt[y * N + x] = hv + (float)rng.NextDouble() * 0.03f;
                }
            var px = new Color[N * N];
            const float k = 3.5f;                                                // relief strength
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = hgt[y * N + (x + 1) % N] - hgt[y * N + (x + N - 1) % N];
                    float dy = hgt[((y + 1) % N) * N + x] - hgt[((y + N - 1) % N) * N + x];
                    var nv = new Vector3(-dx * k, -dy * k, 1f).normalized;          // green up (Unity / OpenGL convention)
                    px[y * N + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f, 1f);
                }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
            tex.SetPixels(px);
            tex.Apply();
            return SaveGenPng(tex, "A_Stucco_nrm_gen", true);
        }

        static Color32[] LoadPng(string file, out int w, out int h)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            t.LoadImage(File.ReadAllBytes(file));
            w = t.width; h = t.height;
            var px = t.GetPixels32();
            UnityEngine.Object.DestroyImmediate(t);
            return px;
        }

        /// <summary>Large-scale colour variation (the painted layers were one flat tone over hundreds of metres): a tinted
        /// copy of the sand (sun-bleached / darker warm) and of the grass (dry, yellower), with shifted tiling, blended in by
        /// low-frequency noise (patches of ~40 to 150 m). 8 layers = still two terrain passes. Runs after the grass details
        /// were placed from the original weights, so grass density does not change.</summary>
        static void MacroVariation(TerrainData td, List<TerrainLayer> layers, List<string> names, float[,,] src, float size)
        {
            var pairs = new List<(int baseIdx, TerrainLayer to, float amount, float seed)>();
            void Variant(string baseName, string name, Color tint, float amount, float seed)
            {
                int bi = names.IndexOf(baseName);
                if (bi < 0 || bi >= layers.Count || layers[bi] == null) return;
                var bl = layers[bi];
                string path = $"{Gen}/{name}.terrainlayer";
                var vl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (vl == null) { vl = new TerrainLayer(); AssetDatabase.CreateAsset(vl, path); }
                vl.diffuseTexture = bl.diffuseTexture;
                vl.normalMapTexture = bl.normalMapTexture;
                vl.maskMapTexture = bl.maskMapTexture;
                vl.tileSize = bl.tileSize * 1.37f;                   // a different repeat: also breaks the visible tiling
                vl.tileOffset = new Vector2(1.7f, 0.9f);
                vl.normalScale = bl.normalScale;
                vl.smoothness = bl.smoothness;
                vl.diffuseRemapMin = Vector4.zero;
                vl.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1f);   // the URP terrain shader multiplies the albedo by this
                EditorUtility.SetDirty(vl);
                pairs.Add((bi, vl, amount, seed));
            }
            Variant("T_Sand", "T_SandWarm", new Color(0.9f, 0.82f, 0.7f), 0.7f, 11f);
            Variant("T_Grass", "T_GrassDry", new Color(1f, 0.9f, 0.56f), 0.65f, 37f);
            if (pairs.Count == 0) return;
            int ar = src.GetLength(0), nl = src.GetLength(2), nn = nl + pairs.Count;
            var all = new List<TerrainLayer>(layers);
            foreach (var pa in pairs) all.Add(pa.to);
            var dst = new float[ar, ar, nn];
            float m = size / ar;                                     // metres per alphamap texel
            for (int z = 0; z < ar; z++)
                for (int x = 0; x < ar; x++)
                {
                    for (int k = 0; k < nl; k++) dst[z, x, k] = src[z, x, k];
                    for (int q = 0; q < pairs.Count; q++)
                    {
                        var pa = pairs[q];
                        float wx = x * m, wz = z * m;
                        // two octaves (150 m and 45 m): broad patches with ragged edges
                        float nv = Mathf.PerlinNoise(wx / 150f + pa.seed, wz / 150f + pa.seed) * 0.7f +
                                   Mathf.PerlinNoise(wx / 45f + pa.seed * 2f, wz / 45f + pa.seed) * 0.3f;
                        float f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.68f, nv)) * pa.amount;
                        float moved = dst[z, x, pa.baseIdx] * f;
                        dst[z, x, pa.baseIdx] -= moved;
                        dst[z, x, nl + q] = moved;
                    }
                }
            td.terrainLayers = all.ToArray();
            td.SetAlphamaps(0, 0, dst);
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
            var walk = ShapePoolAccess(hts, ti, data, parent);      // ramp + deck rim (the buildings are placed already)
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
            // wet sand at the water line: the darker T_WetSand layer takes over the dry sand up to ~0.6 m above the
            // sea (y 0). The splat had a soft 0.2 - 0.9 m blend that read as one flat tone at the 1 m splat texels.
            // The existing layer, not a new tinted copy: a 9th layer would add a third terrain pass.
            int iSand = ti.layers.ToList().FindIndex(ln => ln.name == "T_Sand"), iWet = ti.layers.ToList().FindIndex(ln => ln.name == "T_WetSand");
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
                    if (iSand >= 0 && iWet >= 0 && iSand < nl && iWet < nl)
                    {
                        int hr = Mathf.Min(res - 1, z * (res - 1) / (ar - 1)), hc = Mathf.Min(res - 1, x * (res - 1) / (ar - 1));
                        float hw = ti.minH + hts[hr, hc] * (ti.maxH - ti.minH);
                        float wet = hw < -1f ? 0f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, hw));
                        float moved = alpha[z, x, iSand] * wet * 0.9f;
                        alpha[z, x, iSand] -= moved;
                        alpha[z, x, iWet] += moved;
                    }
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
                        useInstancing = true, minWidth = 1.0f, maxWidth = 1.8f, minHeight = 0.7f, maxHeight = 1.4f,   // wider: fewer clumps cover the same ground
                        healthyColor = Color.white, dryColor = new Color(0.85f, 0.8f, 0.6f), noiseSpread = 0.3f,
                        alignToGround = 60f, density = 1f     // Unity 6: percent (0-100)
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
                // Unity 6 terrains default to coverage mode (values out of 255): our maps hold grass counts per cell
                var scatter = typeof(TerrainData).GetMethod("SetDetailScatterMode");
                if (scatter != null) scatter.Invoke(td, new object[] { Enum.Parse(scatter.GetParameters()[0].ParameterType, "InstanceCountMode") });
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
                        if ((wx + 280f) * (wx + 280f) + (wy + 40f) * (wy + 40f) < 115f * 115f) continue; // golf (mown)
                        // at most 1 clump (~200 triangles) per 1 m cell: 3 per cell was 10-20 M grass triangles on High;
                        // QualityAuto's per-tier density + distance keep High under ~1.5 M
                        if (rng.NextDouble() > 0.5 + 0.5 * Mathf.InverseLerp(0.45f, 0.8f, g)) continue;
                        maps[rng.Next(maps.Length)][z, x] = 1;
                    }
                for (int i = 0; i < maps.Length; i++) td.SetDetailLayer(0, 0, i, maps[i]);
            }
            MacroVariation(td, layers, ti.layers.Select(ln => ln.name).ToList(), alpha, ti.size);

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
            t.detailObjectDistance = 80f;          // = QualityAuto High (it sets the real value per tier at start)
            t.detailObjectDensity = 0.35f;
            t.heightmapPixelError = 3f;
            t.basemapDistance = 400f;
            var tc = go.GetComponent<TerrainCollider>();
            tc.terrainData = td;
            go.isStatic = true;
            CheckWalk(walk);
            return t;
        }

        /// <summary>Pool deck access (review: the deck was cut off for Rex and the agents). The lobby slab is ~3.48, the
        /// ground at the pool doors ~3.0 - 3.1 and the deck top 4.18 with a 0.4 m tile face and open space under its edge
        /// all round: no step <= stepOffset 0.35 / NavMesh climb 0.4 anywhere. Raises the terrain (never lowers it,
        /// except in the ramp band): a ramp from the lobby doors up to the deck edge, and a rim around the deck's outer
        /// edge up to (deck top - 0.08), blending out over 4 m. Deck cells are found by short rays onto the PoolArea
        /// collision (the ground has no collider yet), so the basin, bar and tower are left alone. Returns the
        /// lobby -> deck -> off-deck path for CheckWalk (null when something is missing).</summary>
        static Vector3[] ShapePoolAccess(float[,] hts, TerrainInfo ti, Data data, Transform world)
        {
            var poolT = world.Find("PoolArea");
            var hotelT = world.Find("Hotel");
            if (poolT == null || data.pool == null || data.pool.Length < 3 || data.hotel == null || data.hotel.poolDoors == null ||
                data.hotel.poolDoors.Length < 3)
            { Debug.LogWarning("[Rex Magnum] Pool access: PoolArea / pool / hotel doors missing, terrain not shaped."); return null; }
            Physics.SyncTransforms();
            Bounds BoundsOf(Transform bt)
            {
                var brs = bt.GetComponentsInChildren<Renderer>().Where(br => br.enabled).ToArray();
                if (brs.Length == 0) return new Bounds(bt.position, Vector3.zero);
                var bb = brs[0].bounds;
                foreach (var br in brs) bb.Encapsulate(br.bounds);
                return bb;
            }
            Bounds pb = BoundsOf(poolT);
            Bounds hb = hotelT != null ? BoundsOf(hotelT) : new Bounds(U(40f, -300f, 3.48f), new Vector3(90f, 1f, 22f));
            float deckTop = data.pool[2];
            float slab = data.hotel.poolDoors[2];
            Vector3 door = U(data.hotel.poolDoors);
            int res = ti.res;
            float cell = ti.size / (res - 1), range = ti.maxH - ti.minH;
            int Idx(float wv) => Mathf.Clamp(Mathf.RoundToInt((wv + ti.size / 2) / cell), 0, res - 1);
            float Wpos(int iv) => iv * cell - ti.size / 2;
            float Get(int rr, int cc) => ti.minH + hts[rr, cc] * range;
            void Put(int rr, int cc, float hv) => hts[rr, cc] = Mathf.Clamp01((hv - ti.minH) / range);
            float doorBefore = Get(Idx(door.z + 1f), Idx(door.x));

            // 1 = deck top (a PoolArea hit within 0.2 m of it), 2 = other PoolArea geometry (basin, bar, tower), 0 = open
            int c0 = Idx(pb.min.x - 6f), c1 = Idx(pb.max.x + 6f), r0 = Idx(Mathf.Min(pb.min.z, door.z) - 6f), r1 = Idx(pb.max.z + 6f);
            int nc = c1 - c0 + 1, nr = r1 - r0 + 1;
            var kind = new byte[nr, nc];
            var rayHits = new RaycastHit[16];
            float deckMinX = float.MaxValue, deckMaxX = float.MinValue, deckMinZ = float.MaxValue;
            int deckCells = 0;
            for (int rr = 0; rr < nr; rr++)
                for (int cc = 0; cc < nc; cc++)
                {
                    // from just above the deck: under the umbrellas, so the deck itself is the first PoolArea face
                    var from = new Vector3(Wpos(c0 + cc), deckTop + 0.6f, Wpos(r0 + rr));
                    int n = Physics.RaycastNonAlloc(from, Vector3.down, rayHits, 6f, ~0, QueryTriggerInteraction.Ignore);
                    for (int k = 0; k < n; k++)
                    {
                        if (!rayHits[k].collider.transform.IsChildOf(poolT)) continue;
                        if (Mathf.Abs(rayHits[k].point.y - deckTop) < 0.2f) { kind[rr, cc] = 1; break; }
                        kind[rr, cc] = 2;
                    }
                    if (kind[rr, cc] != 1) continue;
                    deckCells++;
                    deckMinX = Mathf.Min(deckMinX, from.x); deckMaxX = Mathf.Max(deckMaxX, from.x); deckMinZ = Mathf.Min(deckMinZ, from.z);
                }
            Debug.Log($"[Rex Magnum] Pool access: PoolArea bounds {pb.min:F1} .. {pb.max:F1}, Hotel bounds {hb.min:F1} .. {hb.max:F1}, " +
                      $"deck top {deckTop:F2}, lobby slab {slab:F2}, {deckCells} deck cells (x {deckMinX:F1} .. {deckMaxX:F1}, south edge z {deckMinZ:F1}).");
            if (deckCells < 20) { Debug.LogWarning("[Rex Magnum] Pool access: deck not found by the rays (no PoolArea collision?), terrain not shaped."); return null; }

            // rim: open ground within 4 m of the deck rises to (deck top - 0.08), smoothly back to the ground; deck cells at
            // the outer edge (under the tiles, hidden) go up too, so the ground meets the deck edge itself, not 1 m out
            const float RimWidth = 4f;
            float rimTop = deckTop - 0.08f;
            int reach = Mathf.CeilToInt(RimWidth / cell);
            int raised = 0;
            for (int rr = 0; rr < nr; rr++)
                for (int cc = 0; cc < nc; cc++)
                {
                    if (kind[rr, cc] == 2) continue;
                    float wx = Wpos(c0 + cc), wz = Wpos(r0 + rr);
                    if (wx > hb.min.x && wx < hb.max.x && wz > hb.min.z && wz < hb.max.z) continue;   // under the hotel
                    float best = float.MaxValue;
                    for (int dr = -reach; dr <= reach; dr++)
                        for (int dc = -reach; dc <= reach; dc++)
                        {
                            int rq = rr + dr, cq = cc + dc;
                            if (rq < 0 || cq < 0 || rq >= nr || cq >= nc) continue;
                            if (kind[rq, cq] != (kind[rr, cc] == 1 ? (byte)0 : (byte)1)) continue;
                            best = Mathf.Min(best, Mathf.Sqrt(dr * dr + dc * dc) * cell);
                        }
                    float hv = Get(r0 + rr, c0 + cc), want;
                    if (kind[rr, cc] == 1) { if (best > 1.5f) continue; want = rimTop; }            // deck edge cell
                    else { if (best > RimWidth) continue; want = Mathf.Lerp(rimTop, hv, Mathf.SmoothStep(0f, 1f, best / RimWidth)); }
                    if (want > hv) { Put(r0 + rr, c0 + cc, want); raised++; }
                }

            // ramp: lobby doors -> deck south edge (x = doors +- 3 m, blending out over 2 more), from just under the slab
            // to just under the deck top; 1.5 m under the lobby floor too, so the ground meets the slab at the threshold
            float deckZ = deckMinZ;
            for (int rr = Idx(door.z) - r0; rr < nr; rr++)          // the deck edge in front of the doors
                if (rr >= 0 && kind[rr, Mathf.Clamp(Idx(door.x) - c0, 0, nc - 1)] == 1) { deckZ = Wpos(r0 + rr); break; }
            float rampLo = slab - 0.03f, rampHi = deckTop - 0.06f;
            for (int rr = Idx(door.z - 1.5f); rr <= Idx(deckZ); rr++)
                for (int cc = Idx(door.x - 5f); cc <= Idx(door.x + 5f); cc++)
                {
                    float wz = Wpos(rr), wx = Wpos(cc);
                    float want = Mathf.Lerp(rampLo, rampHi, Mathf.InverseLerp(door.z, deckZ, wz));
                    float side = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 5f, Mathf.Abs(wx - door.x)));
                    Put(rr, cc, Mathf.Lerp(Get(rr, cc), want, side));
                }
            float doorAfter = Get(Idx(door.z + 1f), Idx(door.x));
            Debug.Log($"[Rex Magnum] Pool access: rim raised {raised} cells to {rimTop:F2}; ramp z {door.z:F1} .. {deckZ:F1} " +
                      $"({rampLo:F2} -> {rampHi:F2}, slope {(rampHi - rampLo) / Mathf.Max(0.5f, deckZ - door.z) * 100f:F0}%); " +
                      $"ground at the doors {doorBefore:F2} -> {doorAfter:F2} (slab {slab:F2}).");
            if (Mathf.Abs(doorAfter - slab) > 0.3f) Debug.LogWarning($"[Rex Magnum] Pool access: ground at the lobby doors is {doorAfter - slab:F2} m from the slab.");

            // walk check path: inside the lobby, out of the doors, up onto the deck, along it and off both outer sides
            float alongZ = deckZ + 2f;
            return new[]
            {
                new Vector3(door.x, slab, door.z - 4f), new Vector3(door.x, slab, door.z), new Vector3(door.x, deckTop, alongZ),
                new Vector3(deckMinX - 6f, deckTop, alongZ), new Vector3(door.x, deckTop, alongZ), new Vector3(deckMaxX + 6f, deckTop, alongZ)
            };
        }

        /// <summary>Edit mode has no NavMesh bake: walk the path in 0.5 m steps with down rays from 0.9 m over the
        /// last ground point and warn for any step over Rex's stepOffset (0.35 m). A wall or a lounger in the way is
        /// reported the same way (it is a block for Rex too).</summary>
        static void CheckWalk(Vector3[] path)
        {
            if (path == null || path.Length < 2) return;
            Physics.SyncTransforms();
            float y = path[0].y;
            if (Physics.Raycast(path[0] + Vector3.up * 0.9f, Vector3.down, out RaycastHit h0, 3f, ~0, QueryTriggerInteraction.Ignore)) y = h0.point.y;
            int steps = 0, bad = 0;
            float worst = 0f;
            for (int s = 1; s < path.Length; s++)
            {
                Vector3 a = path[s - 1], b = path[s];
                a.y = b.y = 0f;
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.5f));
                for (int k = 1; k <= n; k++)
                {
                    var q = Vector3.Lerp(a, b, k / (float)n);
                    steps++;
                    if (!Physics.Raycast(new Vector3(q.x, y + 0.9f, q.z), Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        bad++;
                        if (bad <= 8) Debug.LogWarning($"[Rex Magnum] Walk check: no ground (drop > 2 m) at {q.x:F1}, {q.z:F1} (leg {s}).");
                        continue;
                    }
                    float dy = hit.point.y - y;
                    worst = Mathf.Max(worst, Mathf.Abs(dy));
                    if (Mathf.Abs(dy) > 0.35f)
                    {
                        bad++;
                        if (bad <= 8) Debug.LogWarning($"[Rex Magnum] Walk check: {dy:+0.00;-0.00} m step at {q.x:F1}, {hit.point.y:F2}, {q.z:F1} " +
                                                       $"(leg {s}, {hit.collider.name}).");
                    }
                    y = hit.point.y;
                }
            }
            if (bad == 0) Debug.Log($"[Rex Magnum] Walk check lobby -> pool deck -> off the deck (W + E): {steps} steps, all <= 0.35 m (worst {worst:F2}).");
            else Debug.LogWarning($"[Rex Magnum] Walk check lobby -> pool deck: {bad} of {steps} steps over 0.35 m or without ground.");
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

        /// <summary>Coarse light probes (resort, ruins, cave mouth): after Bake Lighting the lower building LODs, Rex,
        /// blobs and props are lit from these instead of the one ambient probe (they were flat / too bright in shade).
        /// A sparse grid over each area at 1, 4 and 10 m above the ground, plus a layer inside the hotel on every floor
        /// (+1.5 m) and on the roof (floors 4 / 5 and the roof had none: the 10 m layer stops at floor 3). Points inside
        /// colliders are skipped (also inside non-convex mesh colliders, see InsideSolid).</summary>
        static void LightProbeGrid(Terrain terrain, float[] hotelFloors)
        {
            var areas = new List<Bounds>();
            Bounds hotelBox = default;
            bool hasHotel = false;
            foreach (var an in new[] { "Hotel", "PoolArea", "TikiBar", "DiveShop", "Ruins" })
            {
                var go = GameObject.Find("Island/" + an);
                if (go == null) continue;
                var rs = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();   // hidden ones have empty bounds at the origin
                if (rs.Length == 0) continue;
                var bb = rs[0].bounds;
                foreach (var rr in rs) bb.Encapsulate(rr.bounds);
                if (an == "Hotel") { hotelBox = bb; hasHotel = true; }
                bb.Expand(new Vector3(12f, 0f, 12f));                       // the paths and open ground around it
                areas.Add(bb);
            }
            var mouthAt = U(-160f, 232f, 0f);                               // cave mouth (as in Tunnels)
            areas.Add(new Bounds(mouthAt, new Vector3(36f, 10f, 36f)));
            Physics.SyncTransforms();
            var pts = new List<Vector3>();
            foreach (var bb in areas)
            {
                float step = Mathf.Max(8f, Mathf.Max(bb.size.x, bb.size.z) / 8f);   // at most ~9 x 9 columns per area
                for (float x = bb.min.x; x <= bb.max.x + 0.01f; x += step)
                    for (float z = bb.min.z; z <= bb.max.z + 0.01f; z += step)
                    {
                        var col = new Vector3(x, 0f, z);
                        float gy = terrain != null ? terrain.SampleHeight(col) + terrain.transform.position.y : bb.min.y;
                        foreach (float up in new[] { 1f, 4f, 10f })
                        {
                            var pp = new Vector3(x, gy + up, z);
                            if (Physics.CheckSphere(pp, 0.3f, ~0, QueryTriggerInteraction.Ignore) || InsideSolid(pp)) continue;   // inside a wall / rock
                            if (pts.Any(o => (o - pp).sqrMagnitude < 4f)) continue;                            // overlapping areas
                            pts.Add(pp);
                        }
                    }
            }
            // hotel interior: one layer per floor (elevatorFloors: the floors, the last value is the roof), ~6 m apart
            if (hasHotel && hotelFloors != null)
            {
                float hs = 6f;
                foreach (float fy in hotelFloors)
                    for (float x = hotelBox.min.x + hs * 0.5f; x < hotelBox.max.x; x += hs)
                        for (float z = hotelBox.min.z + hs * 0.5f; z < hotelBox.max.z; z += hs)
                        {
                            var hp = new Vector3(x, fy + 1.5f, z);
                            if (Physics.CheckSphere(hp, 0.3f, ~0, QueryTriggerInteraction.Ignore) || InsideSolid(hp)) continue;
                            if (pts.Any(o => (o - hp).sqrMagnitude < 4f)) continue;
                            pts.Add(hp);
                        }
            }
            if (pts.Count < 4) return;
            var lpg = new GameObject("LightProbes").AddComponent<LightProbeGroup>();   // at the origin: positions are world
            // by reflection: the setter is missing from older editor assemblies
            var probeProp = typeof(LightProbeGroup).GetProperty("probePositions");
            if (probeProp != null && probeProp.CanWrite) probeProp.SetValue(lpg, pts.ToArray());
            Debug.Log($"[Rex Magnum] {pts.Count} light probes (Bake Lighting fills them).");
        }

        /// <summary>A point inside a non-convex mesh collider (rock, hill, thick wall): CheckSphere does not see it (mesh
        /// colliders are hollow). Upward ray with back faces on: hitting a face from behind within 50 m = inside; anything
        /// right overhead (0.5 m) = in a slab / under a stair. Such probes baked black and darkened whatever used them.</summary>
        static bool InsideSolid(Vector3 p)
        {
            bool old = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                if (!Physics.Raycast(p, Vector3.up, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore)) return false;
                return hit.distance < 0.5f || Vector3.Dot(hit.normal, Vector3.up) > 0.01f;   // the face looks up: we hit its back
            }
            finally { Physics.queriesHitBackfaces = old; }
        }

        /// <summary>Cloud layer over the Procedural sky (it had none: an empty blue dome all day). A sphere drawn around the
        /// camera by RexMagnum/Clouds; DayNight drives the sunset glow / night / coverage. Scaled huge only so its bounds
        /// always hold the camera (never culled); the shader places it at the camera's far plane.</summary>
        static void CloudDome()
        {
            var sh = Shader.Find("RexMagnum/Clouds");
            if (sh == null) { Debug.LogWarning("[Rex Magnum] RexMagnum/Clouds shader missing: no cloud layer."); return; }
            var cm = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/Clouds.mat");
            if (cm == null) { cm = new Material(sh); AssetDatabase.CreateAsset(cm, Mats + "/Clouds.mat"); }
            cm.shader = sh;
            cm.SetFloat("_Cover", 0.45f);
            cm.SetFloat("_Opacity", 0.9f);
            EditorUtility.SetDirty(cm);
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(dome.GetComponent<Collider>());
            dome.name = "CloudDome";
            dome.transform.localScale = Vector3.one * 6000f;
            var dr = dome.GetComponent<MeshRenderer>();
            dr.sharedMaterial = cm;
            dr.shadowCastingMode = ShadowCastingMode.Off;
            dr.receiveShadows = false;
            dr.lightProbeUsage = LightProbeUsage.Off;
            dr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            dr.allowOcclusionWhenDynamic = false;          // a huge box: occlusion culling must never hide it
        }

        static void Lighting()
        {
            var sun = UnityEngine.Object.FindAnyObjectByType<Light>();
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
            CloudDome();
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
            // the interior probes (room 304, lobby, corridors, tunnels) are box-projected and blend at their edges
            // on every URP asset in use (the default and each quality level's own, like Project Setup): QualityAuto can
            // switch to a level whose asset still had cross-fade off (the tree impostors popped again on that tier)
            var urpAll = new List<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpCur) urpAll.Add(urpCur);
            for (int qi = 0; qi < QualitySettings.names.Length; qi++)
                if (QualitySettings.GetRenderPipelineAssetAt(qi) is UniversalRenderPipelineAsset urpQ && !urpAll.Contains(urpQ)) urpAll.Add(urpQ);
            foreach (var urpA in urpAll)
            {
                QualityAuto.SetUrp(urpA, "reflectionProbeBoxProjection", "m_ReflectionProbeBoxProjection", true);
                QualityAuto.SetUrp(urpA, "reflectionProbeBlending", "m_ReflectionProbeBlending", true);
                // dithered LOD cross-fade (also here, not only in Project Setup: a project set up before it had pops)
                QualityAuto.SetUrp(urpA, "enableLODCrossFade", "m_EnableLODCrossFade", true);
                EditorUtility.SetDirty(urpA);
            }

            // post-processing
            var vol = new GameObject("PostProcessing").AddComponent<Volume>();
            vol.isGlobal = true;
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Gen + "/IslandPost.asset");
            if (prof == null) { prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, Gen + "/IslandPost.asset"); }
            // components must be sub-assets of the profile file, or they are lost when Unity restarts (no ACES, no bloom);
            // an older profile can hold such lost (null) entries: drop them first
            prof.components.RemoveAll(x => x == null);
            T Get<T>() where T : VolumeComponent
            {
                if (!prof.TryGet(out T c)) { c = prof.Add<T>(true); AssetDatabase.AddObjectToAsset(c, prof); }
                c.active = true;
                return c;
            }
            var tm = Get<Tonemapping>(); tm.mode.Override(TonemappingMode.ACES);
            // bloom only on real highlights (sun glints, neon, emissives), not on sunlit sand and white walls;
            // no extra exposure: the albedos are now correct (sRGB-converted) so the scene is bright enough without it
            var bl = Get<Bloom>(); bl.threshold.Override(1.5f); bl.intensity.Override(0.35f); bl.scatter.Override(0.6f);
            var ca = Get<ColorAdjustments>(); ca.postExposure.Override(0f); ca.contrast.Override(12f); ca.saturation.Override(8f);
            var wb = Get<WhiteBalance>(); wb.temperature.Override(6f);
            var vg = Get<Vignette>(); vg.intensity.Override(0.18f);
            foreach (var c in prof.components) EditorUtility.SetDirty(c);
            EditorUtility.SetDirty(prof);
            vol.sharedProfile = prof;
            AddSSAO();
        }

        // ---------------------------------------------------------------- optional bakes (menus)

        /// <summary>Optional: lightmaps for the buildings, so rooms and corridors get real occlusion instead of the flat
        /// sky ambient. Night-safe: every light is set to Realtime (DayNight moves the sun, switches the lamps) and the
        /// bake uses a dim flat ambient instead of the noon sky, so the lightmaps hold soft AO-darkened fill light only
        /// (a lightmapped wall ignores DayNight's ambient: a day-sky bake glowed all night). The scene's sky ambient
        /// comes back when the bake ends. Slow (GPU lightmapper, minutes); the game works without it. Buildings get
        /// lightmap UVs (FBX reimport) first. The light probes bake with the same flat ambient: they keep only each
        /// spot's openness, and DayNight.ScaleProbes multiplies the live sky ambient by it (they follow day / night).</summary>
        [MenuItem("Rex Magnum/Bake Lighting (slow, optional)", priority = 30)]
        public static void BakeLighting()
        {
            var island = GameObject.Find("Island");
            if (island == null) { EditorUtility.DisplayDialog("Rex Magnum", "Open Scenes/Island.unity first (Build Island makes it).", "OK"); return; }
            if (Lightmapping.isRunning) { EditorUtility.DisplayDialog("Rex Magnum", "A lighting bake is already running.", "OK"); return; }
            if (!EditorUtility.DisplayDialog("Rex Magnum", "Bake building lightmaps?\n\n" +
                    "Time: the model reimport takes 1 to 3 minutes (Unity is busy), then the bake runs in the background, " +
                    "about 5 to 30 minutes (progress bar at the bottom right of the Unity window). To stop the bake: click " +
                    "the X next to that progress bar, or Window > Rendering > Lighting > Cancel.\n\n" +
                    "What it does: soft shadows in corners and rooms (ambient occlusion with a dim fill light). It is " +
                    "night-safe: the sun and all lamps stay realtime, so nights stay dark. Shaded building sides look a " +
                    "little darker by day.\n\nSave the scene when the bake is done.", "Bake", "Cancel")) return;
            // night-safe: no light may be baked into the lightmaps (DayNight turns the sun and switches the lamps)
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                { SetProp(l, "lightmapBakeType", "Realtime"); EditorUtility.SetDirty(l); }
            // lightmap UVs for every building FBX (not vegetation / characters)
            var fbx = new HashSet<string>();
            foreach (var r in island.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                if ((GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.OccluderStatic) == 0) continue;
                string p = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) fbx.Add(p);
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var p in fbx)
                    if (AssetImporter.GetAtPath(p) is ModelImporter mi && !mi.generateSecondaryUV)
                    {
                        mi.generateSecondaryUV = true;
                        mi.SaveAndReimport();
                    }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            // buildings: every LOD (LOD0 / 1 / 2) in the lightmap, moving parts and see-through parts on probes / SH.
            // Only LOD0 used to be lightmapped: it showed the flat bake ambient (fixed AO fill), the lower LODs the
            // live probes (DayNight.ScaleProbes) -> a brightness jump at every LOD switch. Now all levels show the same
            // bake; the lower LODs at a lower lightmap resolution (they are only seen from far away).
            foreach (var r in island.GetComponentsInChildren<MeshRenderer>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                if ((flags & StaticEditorFlags.OccluderStatic) == 0) continue;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags | StaticEditorFlags.ContributeGI);
                int lodLevel = r.name.EndsWith("_LOD0") ? 0 : r.name.EndsWith("_LOD1") ? 1 : r.name.EndsWith("_LOD2") ? 2 : -1;
                SetProp(r, "receiveGI", lodLevel >= 0 ? "Lightmaps" : "LightProbes");
                if (lodLevel > 0)
                {
                    var rso = new SerializedObject(r);
                    var sp = rso.FindProperty("m_ScaleInLightmap");
                    if (sp != null) { sp.floatValue = lodLevel == 1 ? 0.5f : 0.25f; rso.ApplyModifiedPropertiesWithoutUndo(); }
                }
                // the glass split (SplitSeeThrough) made copies in Generated/: they missed the FBX's lightmap UVs
                var mf = r.GetComponent<MeshFilter>();
                if (lodLevel >= 0 && mf != null && mf.sharedMesh != null && AssetDatabase.GetAssetPath(mf.sharedMesh).StartsWith(Gen))
                {
                    // the split parts are saved non-readable (no CPU copy in the build): readable while we unwrap
                    var mso = new SerializedObject(mf.sharedMesh);
                    var rp = mso.FindProperty("m_IsReadable");
                    bool wasReadable = rp == null || rp.boolValue;
                    if (!wasReadable) { rp.boolValue = true; mso.ApplyModifiedPropertiesWithoutUndo(); }
                    var uv2 = new List<Vector2>();
                    mf.sharedMesh.GetUVs(1, uv2);
                    if (uv2.Count == 0) { Unwrapping.GenerateSecondaryUVSet(mf.sharedMesh); EditorUtility.SetDirty(mf.sharedMesh); }
                    if (!wasReadable) { mso.Update(); rp.boolValue = false; mso.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(mf.sharedMesh); }
                }
            }
            AssetDatabase.SaveAssets();
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(Gen + "/IslandLighting.lighting");
            if (ls == null) { ls = new LightingSettings(); AssetDatabase.CreateAsset(ls, Gen + "/IslandLighting.lighting"); }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            SetProp(ls, "lightmapper", "ProgressiveGPU");
            SetProp(ls, "lightmapResolution", 2f);       // texels per metre: the hotel alone is 90 m long
            SetProp(ls, "lightmapMaxSize", 2048);
            SetProp(ls, "ao", true);
            SetProp(ls, "aoMaxDistance", 1.5f);
            EditorUtility.SetDirty(ls);
            Lightmapping.lightingSettings = ls;
            EditorSceneManager.MarkSceneDirty(island.scene);
            // dim flat ambient for the bake (about the night level, not the noon sky); the sky comes back after
            // the old ambient goes to EditorPrefs, not a static: a script reload / editor restart mid-bake lost it
            // and the scene kept the flat bake ambient for good
            if (!EditorPrefs.HasKey(BakeAmbientKey))
            {
                var al = RenderSettings.ambientLight;
                EditorPrefs.SetString(BakeAmbientKey, string.Join(";", (int)RenderSettings.ambientMode,
                    al.r.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    al.g.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    al.b.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.31f, 0.34f);
            if (!Lightmapping.BakeAsync())
            {
                RestoreBakeAmbient();
                Debug.LogWarning("[Rex Magnum] Lightmap bake did not start (another bake running?).");
                return;
            }
            HookBake();
            Debug.Log("[Rex Magnum] Lightmap bake started (progress bottom-right, X there cancels). Save the scene when it is done.");
        }

        const string BakeAmbientKey = "RexMagnum.BakeAmbient";     // "mode;r;g;b" while a bake holds the flat ambient

        static void HookBake()
        {
            Lightmapping.bakeCompleted -= WaitBake;
            Lightmapping.bakeCompleted += WaitBake;
            EditorApplication.update -= WaitBake;          // also catches a cancelled bake (no bakeCompleted then)
            EditorApplication.update += WaitBake;
        }

        /// <summary>After a domain reload or an editor restart the handlers are gone: hook again when an old ambient
        /// is still stored (WaitBake restores it at once when no bake runs any more).</summary>
        [InitializeOnLoadMethod]
        static void CheckBakeAmbient()
        {
            if (EditorPrefs.HasKey(BakeAmbientKey)) EditorApplication.delayCall += HookBake;
        }

        static void WaitBake()
        {
            if (Lightmapping.isRunning) return;
            if (!EditorPrefs.HasKey(BakeAmbientKey) || GameObject.Find("Island") == null)
            {
                if (!EditorPrefs.HasKey(BakeAmbientKey)) { EditorApplication.update -= WaitBake; Lightmapping.bakeCompleted -= WaitBake; }
                return;     // another scene open: wait for the island scene
            }
            EditorApplication.update -= WaitBake;
            Lightmapping.bakeCompleted -= WaitBake;
            RestoreBakeAmbient();
            Debug.Log("[Rex Magnum] Lighting bake ended; sky ambient restored. Save the scene (File > Save).");
        }

        static void RestoreBakeAmbient()
        {
            var s = EditorPrefs.GetString(BakeAmbientKey, "").Split(';');
            EditorPrefs.DeleteKey(BakeAmbientKey);
            if (s.Length < 4) return;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (int.TryParse(s[0], out int mode)) RenderSettings.ambientMode = (AmbientMode)mode;
            if (float.TryParse(s[1], System.Globalization.NumberStyles.Float, ci, out float cr) &&
                float.TryParse(s[2], System.Globalization.NumberStyles.Float, ci, out float cg) &&
                float.TryParse(s[3], System.Globalization.NumberStyles.Float, ci, out float cb))
                RenderSettings.ambientLight = new Color(cr, cg, cb);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Sets a property by name (enum values by name). Lighting / GI members are set this way so the
        /// file also builds against older editor assemblies.</summary>
        static void SetProp(object o, string name, object value)
        {
            var pi = o.GetType().GetProperty(name);
            if (pi == null || !pi.CanWrite) { Debug.LogWarning($"[Rex Magnum] {o.GetType().Name}.{name} not found"); return; }
            if (pi.PropertyType.IsEnum && value is string en) value = System.Enum.Parse(pi.PropertyType, en);
            else value = System.Convert.ChangeType(value, pi.PropertyType);
            pi.SetValue(o, value);
        }

        /// <summary>Occlusion culling data for the island (buildings, terrain hills and the hotel walls hide what is
        /// behind them). The menu bakes in the background with a progress bar and a Cancel button; the Windows /
        /// Android build menus ask first when the scene has none yet (EnsureOcclusion).</summary>
        [MenuItem("Rex Magnum/Bake Occlusion Culling", priority = 31)]
        public static void BakeOcclusion()
        {
            if (GameObject.Find("Island") == null) { Debug.LogWarning("[Rex Magnum] Open Scenes/Island.unity first."); return; }
            if (StaticOcclusionCulling.isRunning) return;
            OcclusionSettings();
            if (!StaticOcclusionCulling.GenerateInBackground()) { Debug.LogWarning("[Rex Magnum] Occlusion culling bake did not start."); return; }
            occStart = EditorApplication.timeSinceStartup;
            EditorApplication.update += OcclusionProgress;
        }

        static double occStart;

        /// <summary>Progress bar while the background bake runs (Umbra gives no fraction: show the time so far).</summary>
        static void OcclusionProgress()
        {
            double t = EditorApplication.timeSinceStartup - occStart;
            if (StaticOcclusionCulling.isRunning)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Rex Magnum: occlusion culling",
                        $"Baking... {(int)t / 60}:{(int)t % 60:00} so far (usually 2 to 10 minutes). Cancel stops it.",
                        Mathf.Repeat((float)t / 120f, 1f)))
                {
                    StaticOcclusionCulling.Cancel();
                    Debug.Log("[Rex Magnum] Occlusion culling bake cancelled.");
                }
                return;
            }
            EditorApplication.update -= OcclusionProgress;
            EditorUtility.ClearProgressBar();
            if (StaticOcclusionCulling.umbraDataSize > 0)
            {
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log($"[Rex Magnum] Occlusion culling baked in {(int)t / 60}:{(int)t % 60:00} ({StaticOcclusionCulling.umbraDataSize / 1024} KB).");
            }
            else Debug.LogWarning("[Rex Magnum] Occlusion culling bake stopped or failed: no data.");
        }

        static void OcclusionSettings()
        {
            StaticOcclusionCulling.smallestOccluder = 4f;
            StaticOcclusionCulling.smallestHole = 0.3f;
            StaticOcclusionCulling.backfaceThreshold = 100f;
        }

        /// <summary>Before a build: if the island scene has no occlusion data, asks the owner first (bake now, which
        /// takes minutes and cannot be stopped once it runs / build without it / stop the build). Opens the scene if
        /// needed, then reopens the scene that was open. Returns false when the build must stop.</summary>
        public static bool EnsureOcclusion()
        {
            string path = Scenes + "/Island.unity";
            if (!File.Exists(path)) return true;
            var active = EditorSceneManager.GetActiveScene();
            bool other = active.path != path;
            if (other)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            bool go = true;
            if (StaticOcclusionCulling.umbraDataSize == 0 && !StaticOcclusionCulling.isRunning)
            {
                int pick = Application.isBatchMode ? 0 : EditorUtility.DisplayDialogComplex("Rex Magnum: occlusion culling",
                    "The island has no occlusion culling data yet. With it, the game does not draw what is behind " +
                    "buildings and hills (faster, most on phones).\n\nBaking takes about 2 to 10 minutes, one time only. " +
                    "Unity is busy until it is done and it cannot be stopped once it starts.\n\n" +
                    "\"Build without\" makes the game now (it works, a bit slower). You can bake later with " +
                    "Rex Magnum > Bake Occlusion Culling (that one has a Cancel button).",
                    "Bake now, then build", "Stop the build", "Build without");
                if (pick == 1) go = false;
                else if (pick == 0)
                {
                    OcclusionSettings();
                    double t0 = EditorApplication.timeSinceStartup;
                    if (StaticOcclusionCulling.Compute())
                    {
                        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                        Debug.Log($"[Rex Magnum] Occlusion culling baked in {(int)(EditorApplication.timeSinceStartup - t0)} s " +
                                  $"({StaticOcclusionCulling.umbraDataSize / 1024} KB).");
                    }
                    else Debug.LogWarning("[Rex Magnum] Occlusion culling bake failed; building without it.");
                }
            }
            if (other && !string.IsNullOrEmpty(active.path)) EditorSceneManager.OpenScene(active.path, OpenSceneMode.Single);
            return go;
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
