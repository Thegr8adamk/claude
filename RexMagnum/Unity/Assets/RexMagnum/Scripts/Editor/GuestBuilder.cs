using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RexMagnum.EditorTools
{
    /// <summary>
    /// Guests + Carmen (Characters/Guests, exported by Blender/guests.py): Humanoid import (they use Rex's
    /// animations through Rex.controller), URP materials (skin maps, hair with alpha), prefabs with NavMeshAgent +
    /// Guest AI. Called by IslandBuilder; menu "Rex Magnum > Build Guests" only makes the prefabs.
    /// </summary>
    public static class GuestBuilder
    {
        const string Dir = "Assets/RexMagnum/Characters/Guests";
        const string Mats = Dir + "/Materials";
        const string Prefabs = "Assets/RexMagnum/Prefabs/Guests";

        [Serializable] class MatRow { public string name, baseMap, normalMap, maskMap; public float[] color; public float smoothness; public bool alphaClip, twoSided; }
        [Serializable] class Row { public string name, fbx, voice; public bool security; public MatRow[] materials; }
        [Serializable] class Table { public Row[] guests; }

        [MenuItem("Rex Magnum/Build Guests", priority = 4)]
        public static void Menu() => Build();

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static Dictionary<string, GameObject> Build()
        {
            var result = new Dictionary<string, GameObject>();
            string json = Dir + "/Guests.json";
            if (!File.Exists(json)) { Debug.LogWarning("[Rex Magnum] No guests yet (run Blender/guests.py)."); return result; }
            EnsureFolder(Mats);
            EnsureFolder(Prefabs);
            AssetDatabase.Refresh();
            var table = JsonUtility.FromJson<Table>(File.ReadAllText(json));
            Textures();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/RexMagnum/Characters/Rex/Rex.controller");
            foreach (var g in table.guests)
            {
                var mats = Materials(g);
                string fbx = $"{Dir}/{g.fbx}";
                var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
                if (mi == null) continue;
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var kv in mats)
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
                mi.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var root = new GameObject(g.name);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                if (!inst.TryGetComponent<Animator>(out var an)) an = inst.AddComponent<Animator>();
                an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;     // ~50 guests: no bone updates off screen
                an.runtimeAnimatorController = controller;
                an.applyRootMotion = false;
                bool human = an.avatar != null && an.avatar.isHuman;
                var l = human ? an.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
                var r = human ? an.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
                if (l != null && r != null && Vector3.Dot(Vector3.Cross(r.position - l.position, Vector3.up), Vector3.forward) < 0f)
                    inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                // both eyes look where the body faces (the same right-arm x up test as the flip above)
                var face = l != null && r != null ? Vector3.Cross(r.position - l.position, Vector3.up) : root.transform.forward;
                EyeMeshes(inst, face.sqrMagnitude > 1e-6f ? face.normalized : root.transform.forward, g.name);
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = 1.75f;
                agent.angularSpeed = 540f;
                agent.acceleration = 12f;
                agent.enabled = false;          // on at runtime once the NavMesh is built (Guest: RuntimeNavMesh.TryEnable)
                var cap = root.AddComponent<CapsuleCollider>();
                cap.radius = 0.3f; cap.height = 1.75f; cap.center = new Vector3(0, 0.88f, 0);
                root.AddComponent<AudioSource>().spatialBlend = 1f;
                var guest = root.AddComponent<Guest>();
                guest.guestName = g.name;
                guest.security = g.security;
                result[g.name] = PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/{g.name}.prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
            Debug.Log($"[Rex Magnum] Guests: {string.Join(", ", result.Keys)}");
            return result;
        }

        /// <summary>The Blender eyeballs are UV-less spheres (iris came from shader nodes, so they showed blank grey).
        /// Swap in a sphere of the same size whose UVs put the front pole (looking forward) at the centre of
        /// Textures/Eye_BC.png (iris + pupil + highlight), with more texels on the visible front half.</summary>
        public static void EyeMeshes(GameObject inst, Vector3 facing, string who, string folder = Prefabs)
        {
            int k = 0;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null || mr.sharedMaterial == null || mr.sharedMaterial.name != "Rex_Eye") continue;
                var b = mf.sharedMesh.bounds;
                float rad = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
                Vector3 f = mf.transform.InverseTransformDirection(facing).normalized;     // same for both eyes (no wall-eye)
                Vector3 up = Vector3.ProjectOnPlane(mf.transform.InverseTransformDirection(Vector3.up), f).normalized;
                Vector3 right = Vector3.Cross(up, f);
                const int rings = 24, segs = 32;
                var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
                for (int i = 0; i <= rings; i++)
                {
                    float th = Mathf.PI * i / rings;                       // 0 = front pole (pupil)
                    float r = 0.5f * Mathf.Pow(th / Mathf.PI, 0.6f);       // must match Blender/eye_texture.py
                    for (int j = 0; j <= segs; j++)
                    {
                        float ph = 2f * Mathf.PI * j / segs;
                        var n = f * Mathf.Cos(th) + (right * Mathf.Cos(ph) + up * Mathf.Sin(ph)) * Mathf.Sin(th);
                        verts.Add(b.center + n * rad); norms.Add(n);
                        uvs.Add(new Vector2(0.5f + r * Mathf.Cos(ph), 0.5f + r * Mathf.Sin(ph)));
                    }
                }
                for (int i = 0; i < rings; i++)
                    for (int j = 0; j < segs; j++)
                    {
                        int a = i * (segs + 1) + j, c = a + segs + 1;
                        tris.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                    }
                var mesh = new Mesh { name = $"{who}_Eye{k}" };
                mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
                // winding must face outwards: flip if a triangle on the middle ring (not the degenerate pole) points inwards
                int q0 = rings / 2 * segs * 6;
                var t0 = Vector3.Cross(verts[tris[q0 + 1]] - verts[tris[q0]], verts[tris[q0 + 2]] - verts[tris[q0]]);
                if (Vector3.Dot(t0, verts[tris[q0]] - b.center) < 0f)
                {
                    for (int q = 0; q < tris.Count; q += 3) { int s = tris[q + 1]; tris[q + 1] = tris[q + 2]; tris[q + 2] = s; }
                    mesh.SetTriangles(tris, 0);
                }
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                string p = $"{folder}/{who}_Eye{k++}.asset";
                var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);     // keep the asset (GUID): other prefabs may use it
                if (old != null) { EditorUtility.CopySerialized(mesh, old); UnityEngine.Object.DestroyImmediate(mesh); mesh = old; EditorUtility.SetDirty(old); }
                else AssetDatabase.CreateAsset(mesh, p);
                mf.sharedMesh = mesh;
            }
        }

        static void Textures()
        {
            AssetDatabase.StartAssetEditing();     // one import pass for all files (much faster)
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir + "/Textures" }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                    string n = Path.GetFileNameWithoutExtension(p);
                    bool normal = n.EndsWith("_N"), mask = n.EndsWith("_MS"), hair = n.StartsWith("Hair_");
                    ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    ti.sRGBTexture = !(normal || mask);
                    ti.alphaSource = (mask || hair) ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                    ti.alphaIsTransparency = hair;
                    ti.maxTextureSize = 2048;
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

        static Dictionary<string, Material> Materials(Row g)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var d = new Dictionary<string, Material>();
            foreach (var r in g.materials)
            {
                string p = $"{Mats}/{r.name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, p); }
                m.shader = lit;
                // Blender linear colour -> sRGB (SetColor expects sRGB in a Linear project)
                var c = r.color != null && r.color.Length >= 3 ? new Color(r.color[0], r.color[1], r.color[2]).gamma : Color.white;
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", r.smoothness > 0 ? r.smoothness : 0.35f);
                var bm = string.IsNullOrEmpty(r.baseMap) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/{r.baseMap}");
                if (bm != null) m.SetTexture("_BaseMap", bm);
                var nm = string.IsNullOrEmpty(r.normalMap) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/{r.normalMap}");
                if (nm != null) { m.SetTexture("_BumpMap", nm); m.EnableKeyword("_NORMALMAP"); }
                var mk = string.IsNullOrEmpty(r.maskMap) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/{r.maskMap}");
                // skin mask alpha is ~0.55 (oily look): scale it to ~0.45
                if (mk != null) { m.SetTexture("_MetallicGlossMap", mk); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", r.name.EndsWith("_Skin") ? 0.8f : 1f); }
                if (r.alphaClip)
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.4f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                }
                if (r.twoSided) m.SetFloat("_Cull", 0f);
                EditorUtility.SetDirty(m);
                d[r.name] = m;
            }
            return d;
        }
    }
}
