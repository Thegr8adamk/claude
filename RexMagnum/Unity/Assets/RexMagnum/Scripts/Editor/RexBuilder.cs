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

            var (rex, controller) = BuildAssets();
            BuildTestScene(rex, controller);
            Debug.Log("[Rex Magnum] Done. Open " + Scenes + "/RexTest.unity and press Play. V = first/third person.");
        }

        /// <summary>Materials, models, animator controller and the Rex prefab (no scene).</summary>
        public static (GameObject rex, AnimatorController controller) BuildAssets()
        {
            foreach (var f in new[] { Mats, Prefabs, Scenes }) EnsureFolder(f);
            AssetDatabase.Refresh();
            ConfigureTextures();
            var mats = BuildMaterials();
            ImportModel(Char + "/Rex.fbx", mats);
            // the arms file has the full skeleton but only arm skin: Unity cannot auto-map its legs,
            // so it copies Rex's avatar (same "Rex_Rig" hierarchy) instead of making its own
            ImportModel(Char + "/Rex_FPArms.fbx", mats, AssetDatabase.LoadAllAssetsAtPath(Char + "/Rex.fbx").OfType<Avatar>().FirstOrDefault());
            ImportSwimClips();
            var controller = BuildController();
            return (BuildRexPrefab(controller), controller);
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
            AssetDatabase.StartAssetEditing();     // one import pass for all files (much faster)
            try
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
            finally { AssetDatabase.StopAssetEditing(); }
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

        static void ImportModel(string path, Dictionary<string, Material> mats, Avatar copyFrom = null)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            if (mi == null) throw new Exception("Missing " + path);
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importBlendShapes = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = copyFrom != null ? ModelImporterAvatarSetup.CopyFromOther : ModelImporterAvatarSetup.CreateFromThisModel;
            if (copyFrom != null) mi.sourceAvatar = copyFrom;
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

        /// <summary>Swim clips (Rex_Tread, Rex_Paddle) come in a small extra FBX (Blender/swim_export.py):
        /// armature only, Humanoid with Rex's own avatar, so they retarget exactly like his other clips.</summary>
        /// <summary>Clips in Rex_Swim.fbx (Blender/swim_export.py EXTRA).</summary>
        static readonly string[] ExtraClips = { "Rex_Tread", "Rex_Paddle", "Rex_Comb", "Rex_Throw", "Rex_Stab", "Rex_GolfSwing", "Rex_Drive", "Rex_Push", "Rex_Limp", "Rex_Victory" };

        static void ImportSwimClips()
        {
            string path = Char + "/Rex_Swim.fbx";
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Char + "/Rex.fbx").OfType<Avatar>().FirstOrDefault();
            if (mi == null || avatar == null) { Debug.LogWarning("[Rex Magnum] no swim clips (Rex_Swim.fbx)"); return; }
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            mi.sourceAvatar = avatar;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.SaveAndReimport();
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var c in mi.defaultClipAnimations)
            {
                string n = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                if (!ExtraClips.Contains(n)) continue;
                c.name = n;
                c.loopTime = c.loopPose = n == "Rex_Tread" || n == "Rex_Paddle" || n == "Rex_Drive" || n == "Rex_Push" || n == "Rex_Limp";   // the rest play once
                c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
                clips.Add(c);
            }
            mi.clipAnimations = clips.ToArray();
            mi.SaveAndReimport();
        }

        static AnimationClip Clip(string name)
        {
            foreach (var fbx in new[] { "/Rex.fbx", "/Rex_Swim.fbx" })
            {
                var c = AssetDatabase.LoadAllAssetsAtPath(Char + fbx).OfType<AnimationClip>().FirstOrDefault(x => x.name == name);
                if (c != null) return c;
            }
            return null;
        }

        static AnimatorController BuildController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ac.AddParameter("HandsDown", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Swim", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Drive", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Push", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Hurt", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Dead", AnimatorControllerParameterType.Bool);     // a hit or a taunt must not stand a dead Rex up
            string[] triggers = { "Jump", "Jab", "Heavy", "Swing", "Whip", "Taunt", "Photo", "Hit", "Slip", "Death", "Comb", "Throw", "Stab", "GolfSwing", "Victory" };
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

            // swimming (pool, sea, river): tread water in place, doggy paddle when moving
            var tread = Clip("Rex_Tread");
            if (tread != null)
            {
                var swim = ac.CreateBlendTreeInController("Swim", out BlendTree swimTree, 0);
                swimTree.blendType = BlendTreeType.Simple1D;
                swimTree.blendParameter = "Speed";
                swimTree.useAutomaticThresholds = false;
                swimTree.AddChild(tread, 0f);
                swimTree.AddChild(Clip("Rex_Paddle") ?? tread, 1.5f);
                var toSwim = loco.AddTransition(swim);
                toSwim.AddCondition(AnimatorConditionMode.If, 0, "Swim");
                toSwim.hasExitTime = false;
                toSwim.duration = 0.3f;
                var toLand = swim.AddTransition(loco);
                toLand.AddCondition(AnimatorConditionMode.IfNot, 0, "Swim");
                toLand.hasExitTime = false;
                toLand.duration = 0.3f;
            }

            // golf cart: sitting at the wheel while Drive is on
            var driveClip = Clip("Rex_Drive");
            if (driveClip != null)
            {
                var drive = sm.AddState("Drive");
                drive.motion = driveClip;
                var toDrive = sm.AddAnyStateTransition(drive);
                toDrive.AddCondition(AnimatorConditionMode.If, 0, "Drive");
                toDrive.canTransitionToSelf = false;
                toDrive.duration = 0.25f;
                var outDrive = drive.AddTransition(loco);
                outDrive.AddCondition(AnimatorConditionMode.IfNot, 0, "Drive");
                outDrive.hasExitTime = false;
                outDrive.duration = 0.25f;
            }

            // pushing the dead golf cart (Push bool), like Drive
            var pushClip = Clip("Rex_Push");
            if (pushClip != null)
            {
                var push = sm.AddState("Push");
                push.motion = pushClip;
                var toPush = sm.AddAnyStateTransition(push);
                toPush.AddCondition(AnimatorConditionMode.If, 0, "Push");
                toPush.canTransitionToSelf = false;
                toPush.duration = 0.25f;
                var outPush = push.AddTransition(loco);
                outPush.AddCondition(AnimatorConditionMode.IfNot, 0, "Push");
                outPush.hasExitTime = false;
                outPush.duration = 0.25f;
            }
            // low health: idle + limp instead of idle / walk / run
            var limpClip = Clip("Rex_Limp");
            if (limpClip != null)
            {
                var limp = ac.CreateBlendTreeInController("Limp", out BlendTree limpTree, 0);
                limpTree.blendType = BlendTreeType.Simple1D;
                limpTree.blendParameter = "Speed";
                limpTree.useAutomaticThresholds = false;
                limpTree.AddChild(Clip("Rex_Idle"), 0f);
                limpTree.AddChild(limpClip, 1.6f);
                var toLimp = loco.AddTransition(limp);
                toLimp.AddCondition(AnimatorConditionMode.If, 0, "Hurt");
                toLimp.hasExitTime = false;
                toLimp.duration = 0.3f;
                var outLimp = limp.AddTransition(loco);
                outLimp.AddCondition(AnimatorConditionMode.IfNot, 0, "Hurt");
                outLimp.hasExitTime = false;
                outLimp.duration = 0.3f;
                // hurt + in water: back to Locomotion, which then goes to Swim (else he limps while swimming)
                toLimp.AddCondition(AnimatorConditionMode.IfNot, 0, "Swim");
                var limpSwim = limp.AddTransition(loco);
                limpSwim.AddCondition(AnimatorConditionMode.If, 0, "Swim");
                limpSwim.hasExitTime = false;
                limpSwim.duration = 0.1f;
            }

            var map = new Dictionary<string, string>
            {
                { "Jump", "Rex_Jump" }, { "Jab", "Rex_PunchJab" }, { "Heavy", "Rex_PunchHeavy" }, { "Swing", "Rex_Swing" },
                { "Whip", "Rex_Whip" }, { "Taunt", "Rex_Taunt" }, { "Photo", "Rex_Photo" }, { "Hit", "Rex_Hit" },
                { "Slip", "Rex_Slip" }, { "Death", "Rex_Death" }, { "Comb", "Rex_Comb" },
                { "Throw", "Rex_Throw" }, { "Stab", "Rex_Stab" }, { "GolfSwing", "Rex_GolfSwing" }, { "Victory", "Rex_Victory" },
            };
            foreach (var kv in map)
            {
                var clip = Clip(kv.Value);
                if (clip == null) { Debug.LogWarning("[Rex Magnum] missing clip " + kv.Value); continue; }
                var st = sm.AddState(kv.Key);
                st.motion = clip;
                var tin = sm.AddAnyStateTransition(st);
                tin.AddCondition(AnimatorConditionMode.If, 0, kv.Key);
                if (kv.Key != "Death") tin.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
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
            if (!go.TryGetComponent<Animator>(out var anim)) anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // A Blender model can come in facing backward: turn it to face +Z
            bool human = anim.avatar != null && anim.avatar.isHuman;
            if (!human) Debug.LogWarning("[Rex Magnum] " + Path.GetFileName(fbx) + ": avatar is not humanoid");
            var l = human ? anim.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
            var r = human ? anim.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
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

            var sun = UnityEngine.Object.FindAnyObjectByType<Light>();
            if (sun != null)
            {
                sun.color = new Color(1f, 0.95f, 0.85f);
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            }

            AddPlayer(rexPrefab, controller, Vector3.zero, 0f, new Vector3(-8f, 0f, -6f), 500f);

            string path = Scenes + "/RexTest.unity";
            EditorSceneManager.SaveScene(scene, path);
            var list = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            list.Add(new EditorBuildSettingsScene(path, true));      // after the island: the .exe must start on Island
            EditorBuildSettings.scenes = list.ToArray();
        }


        /// <summary>Rex + camera rig + FP arms + weapons + blob spawner + test HUD at a position.
        /// pickupRow: where the row of weapon pickups starts (y is the ground height).</summary>
        public static GameObject AddPlayer(GameObject rexPrefab, AnimatorController controller, Vector3 pos, float yaw,
                                           Vector3? pickupRow, float farClip)
        {
            // Rex
            var rex = (GameObject)PrefabUtility.InstantiatePrefab(rexPrefab);
            rex.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var ctrl = rex.GetComponent<RexController>();

            // Camera rig + first-person arms
            var camGo = new GameObject("RexCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = farClip;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var rig = camGo.AddComponent<RexCameraRig>();
            camGo.AddComponent<VRMode>();                 // Settings > Camera & VR > VR mode (OpenXR headset)
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
            if (pickupRow.HasValue)
            foreach (var kv in wprefabs.pickups)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(kv.Value);
                p.transform.position = pickupRow.Value + new Vector3(slot * 1.6f, 0f, 0f);
                slot++;
            }

            camGo.transform.position = pos + new Vector3(0f, 1.7f, -3f);
            var hudGo = new GameObject("TestHUD");
            var hud = hudGo.AddComponent<RexTestHUD>();
            hudGo.AddComponent<TouchControls>();
            hudGo.AddComponent<PauseMenu>();
            hudGo.AddComponent<ProgressTracker>();
            hud.rexHealth = rex.GetComponent<Health>();
            hud.rig = rig;
            hud.actions = rex.GetComponent<RexActions>();

            return rex;
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
