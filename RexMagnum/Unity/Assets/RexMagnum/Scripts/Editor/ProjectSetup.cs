using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RexMagnum.EditorTools
{
    /// <summary>
    /// Runs once when the project opens (and from menu "Rex Magnum > Setup Project"): checks URP, linear colour,
    /// the Input System, and Android texture compression, and fixes what it can. Shows one dialog with the result.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string Done = "RexMagnum_SetupDone_v1";

        static ProjectSetup()
        {
            if (SessionState.GetBool(Done, false) || Application.isBatchMode) return;
            SessionState.SetBool(Done, true);
            EditorApplication.delayCall += () => Run(false);
        }

        [MenuItem("Rex Magnum/Setup Project", priority = -10)]
        public static void Menu() => Run(true);

        static void Run(bool always)
        {
            var notes = new System.Text.StringBuilder();
            bool restart = false;
            // 1. URP: make a pipeline asset if the project has none
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset))
            {
                var existing = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset")
                    .Select(g => AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(g)))
                    .FirstOrDefault(a => a != null);
                if (existing == null)
                {
                    if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
                    var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rd, "Assets/Settings/RexMagnum_Renderer.asset");
                    existing = UniversalRenderPipelineAsset.Create(rd);
                    AssetDatabase.CreateAsset(existing, "Assets/Settings/RexMagnum_URP.asset");
                }
                GraphicsSettings.defaultRenderPipeline = existing;
                int level = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = existing;
                }
                QualitySettings.SetQualityLevel(level, false);   // keep the editor on the level it had
                notes.AppendLine("- URP turned on.");
            }
            var urp = (UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline;
            urp.supportsCameraDepthTexture = true;
            // shadow defaults = Ultra tier (QualityAuto sets the real tier at start)
            // (reflection helper: some of these setters are internal in URP 17)
            RexMagnum.QualityAuto.SetUrp(urp, "shadowCascadeCount", "m_ShadowCascadeCount", 4);
            RexMagnum.QualityAuto.SetUrp(urp, "mainLightShadowmapResolution", "m_MainLightShadowmapResolution", 4096);
            RexMagnum.QualityAuto.SetUrp(urp, "supportsMainLightShadows", "m_MainLightShadowsSupported", true);
            RexMagnum.QualityAuto.SetUrp(urp, "supportsSoftShadows", "m_SoftShadowsSupported", true);
            RexMagnum.QualityAuto.SetUrp(urp, "supportsAdditionalLightShadows", "m_AdditionalLightShadowsSupported", true);
            // box-projected interior reflection probes (room 304, lobby, corridors, tunnels) + soft blend between probes
            RexMagnum.QualityAuto.SetUrp(urp, "reflectionProbeBoxProjection", "m_ReflectionProbeBoxProjection", true);
            RexMagnum.QualityAuto.SetUrp(urp, "reflectionProbeBlending", "m_ReflectionProbeBlending", true);
            EditorUtility.SetDirty(urp);
            // 2. Linear colour space
            if (PlayerSettings.colorSpace != ColorSpace.Linear) { PlayerSettings.colorSpace = ColorSpace.Linear; notes.AppendLine("- Colour space set to Linear."); }
            // 3. Input System: active input handling must include the new Input System (0 old, 1 new, 2 both)
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (ps != null)
            {
                var so = new SerializedObject(ps);
                var p = so.FindProperty("activeInputHandler");
                if (p != null && p.intValue == 0)
                {
                    p.intValue = 2;
                    so.ApplyModifiedProperties();
                    restart = true;
                    notes.AppendLine("- Input handling set to 'Both'. Unity must restart.");
                }
            }
            if (System.Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem") == null)
                notes.AppendLine("- MISSING: Input System package. Window > Package Manager > Unity Registry > Input System > Install.");
            // 4. Android: ASTC textures
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            AssetDatabase.SaveAssets();
            string msg = notes.Length == 0 ? "Project is ready." : notes.ToString();
            msg += "\nNext: menu Rex Magnum > Build Island, then open Assets/RexMagnum/Scenes/Island.unity and press Play.";
            if (always || notes.Length > 0) EditorUtility.DisplayDialog("Rex Magnum setup", msg, "OK");
            if (restart && EditorUtility.DisplayDialog("Rex Magnum setup", "Restart Unity now?", "Restart", "Later"))
                EditorApplication.OpenProject(System.IO.Directory.GetCurrentDirectory());
        }
    }
}
