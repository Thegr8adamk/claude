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
                var nav = UnityEngine.Object.FindFirstObjectByType<RuntimeNavMesh>();
                Vector3 pool = data.pool != null && data.pool.Length == 3 ? U(data.pool) : spawn;
                if (nav != null) { nav.center = pool + new Vector3(0f, 0f, -30f); nav.size = new Vector3(200f, 60f, 240f); }
                var spawner = UnityEngine.Object.FindFirstObjectByType<BlobSpawner>();
                if (spawner != null) { spawner.transform.position = pool; spawner.radius = 18f; spawner.maxAlive = 6; }
                var game = GameObject.Find("Game") ?? new GameObject("Game");
                var dn = game.AddComponent<DayNight>();
                dn.sun = RenderSettings.sun;
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
                    mi.meshCompression = ModelImporterMeshCompression.Low;
                    mi.indexFormat = ModelImporterIndexFormat.Auto;
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
                        if (r.emission != null && r.emission.Length >= 3) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", C(r.emission)); }
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
            var lod0 = rs.Where(r => r.name.EndsWith("_LOD0")).ToArray();       // extras (_Door, _Bottle, _Core) stay outside the LODs
            var lod1 = rs.Where(r => r.name.EndsWith("_LOD1")).ToArray();
            var lod2 = rs.Where(r => r.name.EndsWith("_LOD2")).ToArray();      // heavy buildings: middle level for phones
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(lod2.Length > 0 ? new[] { new LOD(0.3f, lod0), new LOD(0.1f, lod1), new LOD(0.006f, lod2) }
                : lod1.Length > 0 ? new[] { new LOD(0.18f, lod0), new LOD(0.006f, lod1) }
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
            PointLight("Lamp1", U(room.lamp1), warm, 1.2f, 4f, gp, false);
            PointLight("Lamp2", U(room.lamp2), warm, 1.2f, 4f, gp, false);
            PointLight("CeilingLight", U(room.ceiling), new Color(1f, 0.9f, 0.75f), 2.5f, 8f, gp, true);
            var tv = new GameObject("TVLight").AddComponent<Light>();
            tv.transform.SetParent(gp, false);
            tv.transform.position = U(room.tv);
            tv.type = LightType.Point;
            tv.range = 5f;
            tv.gameObject.AddComponent<LightFlicker>();
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
            rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            rp.importance = 2;
            // title camera: slow shot of the hotel from the beach; then the flow takes over
            var tc = new GameObject("TitleCamera").AddComponent<Camera>();
            tc.transform.position = new Vector3(75f, 26f, -420f);
            tc.farClipPlane = 2500f;
            var tdata = tc.gameObject.AddComponent<UniversalAdditionalCameraData>();
            tdata.renderPostProcessing = true;
            var flow = game.AddComponent<GameFlow>();
            flow.titleCamera = tc;
            var rig = UnityEngine.Object.FindFirstObjectByType<RexCameraRig>();
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
                if (Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out RaycastHit ground, 60f)) at = ground.point;
                var ch = new GameObject("ChargeStation").transform;
                ch.SetParent(root, false);
                ch.position = at + Vector3.up * 0.05f;
                ch.gameObject.AddComponent<ChargeStation>();
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
            var spawnerSrc = UnityEngine.Object.FindFirstObjectByType<BlobSpawner>();
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
                             hall[0] + new Vector3(-10f, 0f, 8f), hall[0] + new Vector3(-8f, 0f, 6f), root);
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
            var spawner = UnityEngine.Object.FindFirstObjectByType<BlobSpawner>();
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
            var nav = new GameObject("ShipNavMesh").AddComponent<RuntimeNavMesh>();
            nav.center = at;
            nav.size = new Vector3(120f, 60f, 120f);
        }

        static GameObject CartPrefab(GameObject fbx)
        {
            var root = new GameObject("GolfCart");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            ApplyFix(model.transform);
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                if (r.name.EndsWith("_LOD1") || r.name.EndsWith("_LOD2")) r.gameObject.SetActive(false);
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
            for (int i = 0; i < 8; i++) extra.Add(pool + new Vector3(-40f + i * 12f, -3.5f, -95f));     // beach
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
                        if ((wx + 280f) * (wx + 280f) + (wy + 40f) * (wy + 40f) < 115f * 115f) continue; // golf (mown)
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
