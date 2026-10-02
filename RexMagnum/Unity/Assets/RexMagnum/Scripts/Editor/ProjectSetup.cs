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
            // every URP asset in use: the default and each quality level's own (the game renders with the level's
            // asset when it has one: QualityAuto reads currentRenderPipeline)
            var assets = new System.Collections.Generic.List<UniversalRenderPipelineAsset>
                { (UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline };
            for (int q = 0; q < QualitySettings.names.Length; q++)
                if (QualitySettings.GetRenderPipelineAssetAt(q) is UniversalRenderPipelineAsset qa && !assets.Contains(qa)) assets.Add(qa);
            foreach (var urp in assets)
            {
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
                // dithered LOD cross-fade (tree impostors fade in instead of popping); URP 14+ only, skipped before
                RexMagnum.QualityAuto.SetUrp(urp, "enableLODCrossFade", "m_EnableLODCrossFade", true);
                AddAuxRenderer(urp);
                EditorUtility.SetDirty(urp);
            }
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

        /// <summary>A second, lightweight renderer in the asset's renderer list: a copy of the default one without renderer
        /// features (no SSAO pass / depth-normals prepass). The mirror and pool reflection cameras pick it by name
        /// (QualityAuto.UseAuxRenderer). Made once in Assets/Settings, shared by every URP asset.</summary>
        static void AddAuxRenderer(UniversalRenderPipelineAsset urp)
        {
            var so = new SerializedObject(urp);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0) return;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererData d && d.name == RexMagnum.QualityAuto.AuxRendererName) return;
            string path = "Assets/Settings/" + RexMagnum.QualityAuto.AuxRendererName + ".asset";
            var aux = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (aux == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
                aux = ScriptableObject.CreateInstance<UniversalRendererData>();
                // same rendering path / depth settings as the default renderer (one set of shader variants), no features
                if (list.GetArrayElementAtIndex(0).objectReferenceValue is UniversalRendererData main)
                {
                    EditorUtility.CopySerialized(main, aux);
                    var aso = new SerializedObject(aux);
                    var feats = aso.FindProperty("m_RendererFeatures");
                    if (feats != null) feats.arraySize = 0;
                    var map = aso.FindProperty("m_RendererFeatureMap");
                    if (map != null) map.arraySize = 0;
                    aso.ApplyModifiedPropertiesWithoutUndo();
                }
                aux.name = RexMagnum.QualityAuto.AuxRendererName;
                AssetDatabase.CreateAsset(aux, path);
            }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = aux;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
