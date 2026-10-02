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
    /// Menu "Rex Magnum > Build Blobs": imports Characters/Blobs/Blob_*.fbx (Generic rig),
    /// makes the jelly / brain / eye materials, one Animator Controller per blob and the prefabs
    /// (NavMeshAgent + Health + Blob AI) in Prefabs/Blobs.
    /// </summary>
    public static class BlobBuilder
    {
        const string Dir = "Assets/RexMagnum/Characters/Blobs";
        const string Mats = Dir + "/Materials";
        const string Prefabs = "Assets/RexMagnum/Prefabs/Blobs";

        static readonly string[] Loops = { "Idle", "Slide", "SlideFast", "Stun", "Angry", "Pinned" };

        [Serializable] class Entry { public string kind; public float height; public float[] light, mid, dark; public string[] clips; }

        [MenuItem("Rex Magnum/Build Blobs", priority = 1)]
        public static void BuildMenu() => Build();

        public static Dictionary<string, Blob> Build()
        {
            var result = new Dictionary<string, Blob>();
            if (!AssetDatabase.IsValidFolder(Dir)) return result;
            Ensure(Mats);
            Ensure(Prefabs);
            var colors = ReadColors();
            foreach (var path in Directory.GetFiles(Dir, "Blob_*.fbx"))
            {
                string p = path.Replace('\\', '/');
                string kind = Path.GetFileNameWithoutExtension(p).Substring(5);
                var mats = Materials(kind, colors.TryGetValue(kind, out var c) ? c : new[] { Color.green, Color.green, Color.green });
                Import(p, kind, mats);
                var ctrl = Controller(p, kind);
                result[kind] = Prefab(p, kind, ctrl);
            }
            // bouncer splits into a half-size copy of itself
            if (result.TryGetValue("Bouncer", out var b))
            {
                var path = AssetDatabase.GetAssetPath(b);
                var halfPath = path.Replace(".prefab", "_Half.prefab");
                AssetDatabase.DeleteAsset(halfPath);
                AssetDatabase.CopyAsset(path, halfPath);                 // a separate prefab (no self-reference)
                var root = PrefabUtility.LoadPrefabContents(path);
                root.GetComponent<Blob>().splitPrefab = AssetDatabase.LoadAssetAtPath<Blob>(halfPath);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Rex Magnum] Blobs built: {string.Join(", ", result.Keys)}");
            return result;
        }

        static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Ensure(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        static Dictionary<string, Color[]> ReadColors()
        {
            var d = new Dictionary<string, Color[]>();
            string json = Dir + "/Blobs.json";
            if (!File.Exists(json)) return d;
            // Blobs.json: {"blobs":[{"kind":..,"colors":[[r,g,b],[r,g,b],[r,g,b]]...}]}  (nested arrays -> parse by hand)
            string text = File.ReadAllText(json);
            foreach (var part in text.Split(new[] { "\"kind\":" }, StringSplitOptions.RemoveEmptyEntries).Skip(1))
            {
                string kind = part.Split('"')[1];
                int ci = part.IndexOf("\"colors\"", StringComparison.Ordinal);
                if (ci < 0) continue;
                var nums = new List<float>();
                foreach (var tok in part.Substring(ci).Split(new[] { '[', ']', ',', ' ', '\n', ':' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (float.TryParse(tok, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f))
                        nums.Add(f);
                    if (nums.Count == 9) break;
                }
                if (nums.Count == 9)
                    d[kind] = new[] { new Color(nums[0], nums[1], nums[2]), new Color(nums[3], nums[4], nums[5]), new Color(nums[6], nums[7], nums[8]) };
            }
            return d;
        }

        static Dictionary<string, Material> Materials(string kind, Color[] c)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material Make(string name, Action<Material> set)
            {
                string path = $"{Mats}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
                m.shader = lit;
                set(m);
                EditorUtility.SetDirty(m);
                return m;
            }
            var jelly = Make("Blob_Jelly_" + kind, m =>
            {
                // Clear gummy jelly: transparent, very smooth, 2-3 shades (mid color + darker base via fresnel look)
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 1);   // stops sorting problems inside the body
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetColor("_BaseColor", new Color(c[1].r, c[1].g, c[1].b, 0.72f));
                m.SetFloat("_Smoothness", 0.96f);
                m.SetFloat("_Metallic", 0f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c[2] * 0.15f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });
            var brain = Make("Blob_Brain", m =>
            {
                m.SetColor("_BaseColor", new Color(1f, 0.35f, 0.6f));
                m.SetFloat("_Smoothness", 0.6f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.55f) * 0.6f);
            });
            var white = Make("Blob_EyeWhite", m => { m.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.93f)); m.SetFloat("_Smoothness", 0.9f); });
            var pupil = Make("Blob_Pupil", m => { m.SetColor("_BaseColor", new Color(0.01f, 0.01f, 0.012f)); m.SetFloat("_Smoothness", 0.95f); });
            return new Dictionary<string, Material>
            {
                { "Blob_Jelly_" + kind, jelly }, { "Blob_Brain", brain }, { "Blob_EyeWhite", white }, { "Blob_Pupil", pupil },
            };
        }

        static string ClipName(string take, string kind)
        {
            string n = take.Contains("|") ? take.Substring(take.LastIndexOf('|') + 1) : take;
            string prefix = "Blob" + kind + "_";
            return n.StartsWith(prefix) ? n.Substring(prefix.Length) : n;
        }

        static void Import(string path, string kind, Dictionary<string, Material> mats)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in mats) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            mi.SaveAndReimport();
            var clips = new List<ModelImporterClipAnimation>();
            var seen = new HashSet<string>();
            foreach (var c in mi.defaultClipAnimations)
            {
                string n = ClipName(c.takeName, kind);
                if (!seen.Add(n)) continue;
                c.name = n;
                c.loopTime = Loops.Contains(n);
                clips.Add(c);
            }
            mi.clipAnimations = clips.ToArray();
            mi.SaveAndReimport();
        }

        static AnimatorController Controller(string fbx, string kind)
        {
            string path = $"{Dir}/Blob_{kind}.controller";
            AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var sm = ac.layers[0].stateMachine;
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToList();
            AnimationClip Get(string n) => clips.FirstOrDefault(c => c.name == n);
            var loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            if (Get("Idle")) tree.AddChild(Get("Idle"), 0f);
            if (Get("Slide")) tree.AddChild(Get("Slide"), 1.2f);
            if (Get("SlideFast")) tree.AddChild(Get("SlideFast"), 4f);
            sm.defaultState = loco;
            // One state per clip, named like the clip; the Blob script cross-fades to them by name.
            foreach (var c in clips)
            {
                if (c.name == "Idle" || c.name == "Slide" || c.name == "SlideFast") continue;
                var st = sm.AddState(c.name);
                st.motion = c;
                if (c.name == "Pop" || Loops.Contains(c.name)) continue;
                var back = st.AddTransition(loco);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.15f;
            }
            // Stun/Angry/Pinned loop; the Blob script leaves them by cross-fading back
            AssetDatabase.SaveAssets();
            return ac;
        }

        static Blob Prefab(string fbx, string kind, AnimatorController ctrl)
        {
            var root = new GameObject("Blob_" + kind) { layer = 0 };
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), root.transform);
            model.name = "Model";
            if (!model.TryGetComponent<Animator>(out var anim)) anim = model.AddComponent<Animator>();
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            // Blender front is -Y; make the blob face +Z (eyes are in front)
            var eye = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.Contains("_Eye"));
            if (eye != null && eye.position.z < model.transform.position.z)
                model.transform.localRotation = Quaternion.Euler(0, 180, 0) * model.transform.localRotation;

            float h = kind == "Tourist" ? 1.25f : kind == "Lifeguard" ? 1.9f : kind == "Bouncer" ? 2.15f : 4.2f;
            float r = kind == "Tourist" ? 0.5f : kind == "Lifeguard" ? 0.35f : kind == "Bouncer" ? 0.85f : 1.8f;
            var col = root.AddComponent<CapsuleCollider>();
            col.height = h;
            col.radius = r;
            col.center = new Vector3(0, h * 0.5f, 0);
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = r;
            agent.height = h;
            agent.baseOffset = 0f;
            root.AddComponent<Health>();
            var blob = root.AddComponent<Blob>();
            blob.kind = (Blob.Kind)Enum.Parse(typeof(Blob.Kind), kind);
            blob.animator = anim;
            blob.jellyRenderer = model.GetComponentsInChildren<Renderer>()
                .FirstOrDefault(x => x.sharedMaterials.Any(m => m != null && m.name.StartsWith("Blob_Jelly")));
            foreach (var rr in model.GetComponentsInChildren<Renderer>())
                rr.shadowCastingMode = rr == blob.jellyRenderer ? ShadowCastingMode.On : ShadowCastingMode.Off;
            string path = $"{Prefabs}/Blob_{kind}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<Blob>();
        }
    }
}
