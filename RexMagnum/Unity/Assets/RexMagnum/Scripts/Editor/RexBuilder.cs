using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RexMagnum.EditorTools
{
    /// <summary>
    /// Menu "Rex Magnum > Build Rex + Test Scene".
    ///  1. Textures -> URP Lit materials (from Rex_Materials.json)
    ///  2. Rex.fbx + Rex_FPArms.fbx -> Humanoid avatars, blend shapes, animation clips (loop flags)
    ///  3. Animator Controller (locomotion blend tree + action triggers)
    ///  4. Prefabs: Rex (third person body) + first-person arms + camera rig
    ///  5. Scenes/RexTest.unity: a small test level to try first/third person
    /// Batch mode: -executeMethod RexMagnum.EditorTools.RexBuilder.BuildFromCommandLine
    /// </summary>
    public static class RexBuilder
    {
        const string Root = "Assets/RexMagnum";
        const string Char = Root + "/Characters/Rex";
        const string Tex = Char + "/Textures";
        const string Mats = Char + "/Materials";
        const string Prefabs = Root + "/Prefabs";
        const string Scenes = Root + "/Scenes";
        const string ControllerPath = Char + "/Rex.controller";

        static readonly string[] LoopClips = { "Rex_Idle", "Rex_IdleHero", "Rex_Walk", "Rex_Run" };

        [Serializable] class MatRow
        {
            public string name, baseMap, normalMap, maskMap;
            public float[] color;
            public float metallic, smoothness;
            public bool alphaClip;
        }
        [Serializable] class Manifest { public MatRow[] materials; public string[] animations; }

        [MenuItem("Rex Magnum/Build Rex + Test Scene", priority = 0)]
        public static void Build()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                throw new Exception("Rex Magnum needs URP (Project Settings > Graphics).");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var f in new[] { Mats, Prefabs, Scenes }) EnsureFolder(f);
            AssetDatabase.Refresh();

            ConfigureTextures();
            var mats = BuildMaterials();
            ImportModel(Char + "/Rex.fbx", mats);
            ImportModel(Char + "/Rex_FPArms.fbx", mats);
            var controller = BuildController();
            var rex = BuildRexPrefab(controller);
            BuildTestScene(rex, controller);
            Debug.Log("[Rex Magnum] Done. Open " + Scenes + "/RexTest.unity and press Play. V = first/third person.");
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

        // ------------------------------------------------------------ textures + materials

        static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Tex }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                string n = Path.GetFileNameWithoutExtension(p);
                bool normal = n.EndsWith("_N"), mask = n.EndsWith("_MS");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !(normal || mask);
                ti.alphaSource = mask || n.Contains("HairCard") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                ti.alphaIsTransparency = n.Contains("HairCard");
                ti.mipmapEnabled = true;
                ti.anisoLevel = 4;
                ti.maxTextureSize = 2048;   // brief: 2K on PC
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                var android = ti.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = 1024;   // brief: 1K on phones
                android.format = TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(android);
                ti.SaveAndReimport();
            }
        }

        static Dictionary<string, Material> BuildMaterials()
        {
            var json = File.ReadAllText(Char + "/Rex_Materials.json");
            var man = JsonUtility.FromJson<Manifest>(json);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var result = new Dictionary<string, Material>();
            foreach (var r in man.materials)
            {
                string path = $"{Mats}/{r.name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(lit);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = lit;
                m.enableInstancing = true;
                var c = r.color != null && r.color.Length >= 3 ? new Color(r.color[0], r.color[1], r.color[2]) : Color.white;
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Metallic", r.metallic);
                m.SetFloat("_Smoothness", r.smoothness);
                if (!string.IsNullOrEmpty(r.baseMap))
                {
                    m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Tex}/{r.baseMap}"));
                    m.SetColor("_BaseColor", Color.white);
                }
                if (!string.IsNullOrEmpty(r.normalMap))
                {
                    m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Tex}/{r.normalMap}"));
                    m.EnableKeyword("_NORMALMAP");
                }
                if (!string.IsNullOrEmpty(r.maskMap))
                {
                    m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Tex}/{r.maskMap}"));
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    m.SetFloat("_Smoothness", 1f);
                }
                if (r.alphaClip)
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.35f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Cull", 0f);              // hair cards: both sides
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                }
                EditorUtility.SetDirty(m);
                result[r.name] = m;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        // ------------------------------------------------------------ models + clips

        static void ImportModel(string path, Dictionary<string, Material> mats)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            if (mi == null) throw new Exception("Missing " + path);
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importBlendShapes = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in mats)
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            mi.SaveAndReimport();

            // One clip per Blender action. Skip duplicate shape-key takes.
            var clips = new List<ModelImporterClipAnimation>();
            var seen = new HashSet<string>();
            foreach (var c in mi.defaultClipAnimations)
            {
                if (c.takeName.StartsWith("Key|")) continue;
                string n = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                if (!n.StartsWith("Rex_") || !seen.Add(n)) continue;
                c.name = n;
                c.loopTime = LoopClips.Contains(n);
                c.loopPose = c.loopTime;
                c.lockRootRotation = true;
                c.lockRootHeightY = !(n == "Rex_Death" || n == "Rex_Slip" || n == "Rex_Jump");
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
                clips.Add(c);
            }
            mi.clipAnimations = clips.ToArray();
            mi.SaveAndReimport();
            Debug.Log($"[Rex Magnum] {Path.GetFileName(path)}: {clips.Count} clips, avatar human: {mi.animationType}");
        }

        static AnimationClip Clip(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(Char + "/Rex.fbx").OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == name);
        }

        static AnimatorController BuildController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ac.AddParameter("HandsDown", AnimatorControllerParameterType.Bool);
            string[] triggers = { "Jump", "Jab", "Heavy", "Swing", "Whip", "Taunt", "Photo", "Hit", "Slip", "Death" };
            foreach (var t in triggers) ac.AddParameter(t, AnimatorControllerParameterType.Trigger);

            var layers = ac.layers;
            layers[0].iKPass = true;          // aim the gun arm with IK
            ac.layers = layers;
            var sm = ac.layers[0].stateMachine;
            var loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("Rex_Idle"), 0f);
            tree.AddChild(Clip("Rex_Walk"), 2.2f);
            tree.AddChild(Clip("Rex_Run"), 6.0f);
            sm.defaultState = loco;

            var map = new Dictionary<string, string>
            {
                { "Jump", "Rex_Jump" }, { "Jab", "Rex_PunchJab" }, { "Heavy", "Rex_PunchHeavy" }, { "Swing", "Rex_Swing" },
                { "Whip", "Rex_Whip" }, { "Taunt", "Rex_Taunt" }, { "Photo", "Rex_Photo" }, { "Hit", "Rex_Hit" },
                { "Slip", "Rex_Slip" }, { "Death", "Rex_Death" },
            };
            foreach (var kv in map)
            {
                var clip = Clip(kv.Value);
                if (clip == null) { Debug.LogWarning("[Rex Magnum] missing clip " + kv.Value); continue; }
                var st = sm.AddState(kv.Key);
                st.motion = clip;
                var tin = sm.AddAnyStateTransition(st);
                tin.AddCondition(AnimatorConditionMode.If, 0, kv.Key);
                tin.duration = 0.08f;
                tin.canTransitionToSelf = false;
                if (kv.Key == "Death") continue;   // stays down
                var tout = st.AddTransition(loco);
                tout.hasExitTime = true;
                tout.exitTime = kv.Key == "Slip" ? 0.95f : 0.85f;
                tout.duration = 0.15f;
            }
            AssetDatabase.SaveAssets();
            return ac;
        }

        // ------------------------------------------------------------ prefab

        static GameObject Instance(string fbx, Transform parent, AnimatorController controller)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // A Blender model can come in facing backward: turn it to face +Z
            var l = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var r = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (l != null && r != null)
            {
                Vector3 fwd = Vector3.Cross(r.position - l.position, Vector3.up);
                if (Vector3.Dot(fwd, parent != null ? parent.forward : Vector3.forward) < 0f)
                    go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * go.transform.localRotation;
            }
            return go;
        }

        static GameObject BuildRexPrefab(AnimatorController controller)
        {
            var root = new GameObject("Rex") { tag = "Player" };
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.72f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0f, 0.86f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 45f;
            var model = Instance(Char + "/Rex.fbx", root.transform, controller);
            model.name = "Body";
            var ctrl = root.AddComponent<RexController>();
            ctrl.bodyAnimator = model.GetComponent<Animator>();
            var face = root.AddComponent<RexFace>();
            face.face = model.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(s => s.name == "Rex");
            var voice = root.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;
            voice.playOnAwake = false;
            face.voice = voice;
            var hp = root.AddComponent<Health>();
            hp.maxHealth = 100f;
            root.AddComponent<RexLife>();
            var act = root.AddComponent<RexActions>();
            act.controller = ctrl;
            act.face = face;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.On;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/Rex.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------ test scene

        static void BuildTestScene(GameObject rexPrefab, AnimatorController controller)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var oldCam = Camera.main;
            if (oldCam != null) UnityEngine.Object.DestroyImmediate(oldCam.gameObject);

            // Ground + simple blocks (a wall to test camera collision, a ramp, boxes)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            var gm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            gm.SetColor("_BaseColor", new Color(0.82f, 0.76f, 0.62f));   // sand
            AssetDatabase.CreateAsset(gm, Mats + "/Test_Ground.mat");
            ground.GetComponent<Renderer>().sharedMaterial = gm;
            var bm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bm.SetColor("_BaseColor", new Color(0.9f, 0.9f, 0.88f));
            AssetDatabase.CreateAsset(bm, Mats + "/Test_Block.mat");
            Block("Wall", new Vector3(0, 1.5f, 6), new Vector3(10, 3, 0.4f), Quaternion.identity, bm);
            Block("Ramp", new Vector3(-6, 0.6f, 2), new Vector3(3, 0.3f, 6), Quaternion.Euler(-12, 0, 0), bm);
            Block("Box1", new Vector3(4, 0.5f, 2), Vector3.one, Quaternion.Euler(0, 20, 0), bm);
            Block("Box2", new Vector3(5.2f, 0.5f, 3), Vector3.one, Quaternion.identity, bm);
            Block("Column", new Vector3(2, 2, -4), new Vector3(0.8f, 4, 0.8f), Quaternion.identity, bm);

            var sun = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.color = new Color(1f, 0.95f, 0.85f);
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            }

            // Rex
            var rex = (GameObject)PrefabUtility.InstantiatePrefab(rexPrefab);
            rex.transform.position = Vector3.zero;
            var ctrl = rex.GetComponent<RexController>();

            // Camera rig + first-person arms
            var camGo = new GameObject("RexCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 500f;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var rig = camGo.AddComponent<RexCameraRig>();
            rig.target = rex.transform;
            rig.cam = cam;
            rig.bodyAnimator = ctrl.bodyAnimator;
            var armsRoot = new GameObject("FPArms").transform;
            armsRoot.SetParent(camGo.transform, false);
            var arms = Instance(Char + "/Rex_FPArms.fbx", armsRoot, controller);
            arms.name = "ArmsModel";
            rig.fpArms = armsRoot.gameObject;
            rig.bodyRenderers = rex.GetComponentsInChildren<Renderer>();
            ctrl.cameraRig = rig;
            ctrl.armsAnimator = arms.GetComponent<Animator>();

            // Blobs: NavMesh is built at start; a spawner keeps a few blobs around
            var blobs = BlobBuilder.Build();
            var game = new GameObject("Game");
            game.AddComponent<RuntimeNavMesh>();
            if (blobs.Count > 0)
            {
                var sp = game.AddComponent<BlobSpawner>();
                sp.prefabs = blobs.Where(kv => kv.Key != "KingGlorp").Select(kv => kv.Value).ToArray();
            }

            // Weapons: Rex's inventory + one pickup of each weapon and ammo in a row
            var wprefabs = WeaponBuilder.Build();
            var rw = rex.AddComponent<RexWeapons>();
            rw.controller = ctrl;
            rw.cameraRig = rig;
            rw.prefabs = new GameObject[11];
            foreach (var kv in wprefabs.held) rw.prefabs[(int)kv.Key] = kv.Value;
            rig.weapons = rw;
            rex.GetComponent<RexActions>().weapons = rw;
            foreach (var an in new[] { ctrl.bodyAnimator, ctrl.armsAnimator })
            {
                if (an == null) continue;
                var ik = an.gameObject.AddComponent<RexAimIK>();
                ik.weapons = rw;
                ik.rig = rig;
            }
            int slot = 0;
            foreach (var kv in wprefabs.pickups)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(kv.Value);
                p.transform.position = new Vector3(-8f + slot * 1.6f, 0f, -6f);
                slot++;
            }

            var hudGo = new GameObject("TestHUD");
            var hud = hudGo.AddComponent<RexTestHUD>();
            hud.rexHealth = rex.GetComponent<Health>();
            hud.rig = rig;
            hud.actions = rex.GetComponent<RexActions>();

            string path = Scenes + "/RexTest.unity";
            EditorSceneManager.SaveScene(scene, path);
            var list = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void Block(string name, Vector3 pos, Vector3 size, Quaternion rot, Material m)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetPositionAndRotation(pos, rot);
            b.transform.localScale = size;
            b.GetComponent<Renderer>().sharedMaterial = m;
        }
    }
}
