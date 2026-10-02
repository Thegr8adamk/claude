using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RexMagnum.EditorTools
{
    /// <summary>
    /// Menu "Rex Magnum > Build Weapons": imports Weapons/*.fbx, makes URP materials from Weapons.json,
    /// held-weapon prefabs (Weapon component with grip/tip/muzzle markers) and glowing pickup prefabs.
    /// </summary>
    public static class WeaponBuilder
    {
        const string Dir = "Assets/RexMagnum/Weapons";
        const string Mats = Dir + "/Materials";
        const string Prefabs = "Assets/RexMagnum/Prefabs/Weapons";

        [Serializable] class Row { public string name, baseMap, normalMap, maskMap; public float[] color; public float metallic, smoothness, emission; public bool transparent, twoSided; }
        [Serializable] class Table { public Row[] materials; }

        public class Result
        {
            public Dictionary<WeaponId, GameObject> held = new Dictionary<WeaponId, GameObject>();
            public Dictionary<string, GameObject> pickups = new Dictionary<string, GameObject>();
        }

        static readonly Dictionary<string, WeaponId> Ids = new Dictionary<string, WeaponId>
        {
            { "Weapon_Bottle", WeaponId.Bottle }, { "Weapon_BottleBroken", WeaponId.BottleBroken }, { "Weapon_Towel", WeaponId.Towel },
            { "Weapon_Knife", WeaponId.Knife }, { "Weapon_GolfClub", WeaponId.GolfClub }, { "Weapon_Swordfish", WeaponId.Swordfish },
            { "Weapon_Pistol", WeaponId.Pistol }, { "Weapon_HarpoonGun", WeaponId.Harpoon }, { "Weapon_Sniper", WeaponId.Sniper },
            { "Weapon_Coconut", WeaponId.Coconut },
        };

        [MenuItem("Rex Magnum/Build Weapons", priority = 2)]
        public static void Menu() => Build();

        public static Result Build()
        {
            var res = new Result();
            if (!AssetDatabase.IsValidFolder(Dir)) return res;
            Ensure(Mats);
            Ensure(Prefabs);
            Textures();
            var mats = Materials();
            foreach (var kv in Ids)
            {
                string fbx = $"{Dir}/{kv.Key}.fbx";
                var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (mi == null) continue;
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.importBlendShapes = kv.Value == WeaponId.Towel;     // towel: "Whip" roll shape
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var m in mats) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.Key), m.Value);
                mi.SaveAndReimport();
                res.held[kv.Value] = MakeHeld(fbx, kv.Value);
                res.pickups[kv.Key] = MakePickup(res.held[kv.Value], kv.Value, false);
            }
            foreach (var id in new[] { WeaponId.Pistol, WeaponId.Harpoon, WeaponId.Sniper, WeaponId.Coconut })
                if (res.held.TryGetValue(id, out var h)) res.pickups["Ammo_" + id] = MakePickup(h, id, true);
            AssetDatabase.SaveAssets();
            return res;
        }

        static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
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
                    bool normal = n.EndsWith("_N"), mask = n.EndsWith("_MS");
                    ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    ti.sRGBTexture = !(normal || mask);
                    ti.alphaSource = mask ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                    ti.mipmapEnabled = true;
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

        static Texture2D Tex(string file) => string.IsNullOrEmpty(file) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/Textures/{file}");

        static Dictionary<string, Material> Materials()
        {
            var t = JsonUtility.FromJson<Table>(File.ReadAllText(Dir + "/Weapons.json"));
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var d = new Dictionary<string, Material>();
            foreach (var r in t.materials)
            {
                string p = $"{Mats}/{r.name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, p); }
                m.shader = lit;
                var lin = new Color(r.color[0], r.color[1], r.color[2]);         // Blender linear
                var c = lin.gamma;                                                 // SetColor expects sRGB (Linear project)
                c.a = r.transparent ? 0.55f : 1f;
                m.SetColor("_BaseColor", string.IsNullOrEmpty(r.baseMap) ? c : new Color(1, 1, 1, c.a));
                if (!string.IsNullOrEmpty(r.baseMap))
                    m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/Textures/{r.baseMap}"));
                m.SetFloat("_Metallic", r.metallic);
                m.SetFloat("_Smoothness", Mathf.Clamp01(r.smoothness));
                if (Tex(r.normalMap) != null)
                {
                    m.SetTexture("_BumpMap", Tex(r.normalMap));
                    m.EnableKeyword("_NORMALMAP");
                }
                if (Tex(r.maskMap) != null)
                {
                    m.SetTexture("_MetallicGlossMap", Tex(r.maskMap));
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    m.SetFloat("_Smoothness", 1f);
                }
                if (r.transparent)
                {
                    m.SetFloat("_Surface", 1f);
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = (int)RenderQueue.Transparent;
                }
                if (r.twoSided) m.SetFloat("_Cull", 0f);
                if (r.emission > 0f)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", IslandBuilder.Hdr(lin * Mathf.Min(r.emission, 8f)));
                }
                EditorUtility.SetDirty(m);
                d[r.name] = m;
            }
            return d;
        }

        static Transform Find(Transform root, string prefix) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(prefix));

        static GameObject MakeHeld(string fbx, WeaponId id)
        {
            var root = new GameObject("Held_" + id);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), root.transform);
            var w = root.AddComponent<Weapon>();
            w.id = id;
            w.grip = Find(root.transform, "GRIP");
            w.tip = Find(root.transform, "TIP");
            w.muzzle = Find(root.transform, "MUZZLE");
            w.upRef = Find(root.transform, "UPREF");
            foreach (var c in root.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/Held_{id}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject MakePickup(GameObject held, WeaponId id, bool ammo)
        {
            var root = new GameObject((ammo ? "Ammo_" : "Pickup_") + id);
            var m = (GameObject)PrefabUtility.InstantiatePrefab(held, root.transform);
            if (ammo) m.transform.localScale = Vector3.one * 0.6f;
            var p = root.AddComponent<WeaponPickup>();
            p.weapon = id;
            p.ammoOnly = ammo;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/{root.name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
