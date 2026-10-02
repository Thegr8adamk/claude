using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RexMagnum.EditorTools
{
    /// <summary>
    /// Menu "Rex Magnum > Make Windows Game (.exe)" and "Make Android Game (.apk)" (brief section 10).
    /// Output: &lt;project&gt;/Builds/Windows/RexMagnum.exe and &lt;project&gt;/Builds/Android/RexMagnum.apk.
    /// Android: IL2CPP, ARM64, landscape, ASTC textures, Vulkan + OpenGL ES 3, minimum Android 8.
    /// </summary>
    public static class BuildTools
    {
        const string Product = "Rex Magnum: Vacation Violence";

        static string[] Scenes()
        {
            var s = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
            if (s.Length == 0) throw new System.Exception("No scenes. Run Rex Magnum > Build Island first.");
            return s;
        }

        static void Common()
        {
            PlayerSettings.companyName = "Rex Magnum";
            PlayerSettings.productName = Product;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            IncludeShader("Universal Render Pipeline/Unlit");
            IslandBuilder.EnsureOcclusion();      // bake occlusion culling once if the island scene has none (slow once)
        }

        /// <summary>Shaders the game only finds by name (Shader.Find: aim line, harpoon rope) are left out of a build
        /// unless a material uses them; add them to Graphics Settings > Always Included Shaders.</summary>
        static void IncludeShader(string name)
        {
            var sh = Shader.Find(name);
            if (sh == null) return;
            var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var list = gs.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) return;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
            gs.ApplyModifiedProperties();
        }

        static void Report(BuildReport r, string path)
        {
            if (r.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Rex Magnum] Build done: {path} ({r.summary.totalSize / (1024 * 1024)} MB)");
                EditorUtility.RevealInFinder(path);
            }
            else Debug.LogError($"[Rex Magnum] Build failed: {r.summary.result}. See the Console for errors.");
        }

        [MenuItem("Rex Magnum/Make Windows Game (.exe)", priority = 20)]
        public static void Windows()
        {
            Common();
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Windows", "RexMagnum.exe");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes(), locationPathName = path, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            Report(r, path);
        }

        [MenuItem("Rex Magnum/Make Android Game (.apk)", priority = 21)]
        public static void Android()
        {
            Common();
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.rexmagnum.vacationviolence");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.buildAppBundle = false;
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Android", "RexMagnum.apk");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes(), locationPathName = path, target = BuildTarget.Android, options = BuildOptions.None
            });
            Report(r, path);
        }
    }
}
