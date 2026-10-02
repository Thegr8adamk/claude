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
                // lamp shadow atlas: 4096 with 512 px tiles = 64 maps (QualityAuto lets at most 36 cast at once)
                RexMagnum.QualityAuto.SetUrp(urp, "additionalLightsShadowmapResolution", "m_AdditionalLightsShadowmapResolution", 4096);
                RexMagnum.QualityAuto.SetUrp(urp, "additionalLightsShadowResolutionTierHigh", "m_AdditionalLightsShadowResolutionTierHigh", 512);
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

        /// <summary>A second, lightweight renderer in the asset's renderer list: a copy of the asset's default renderer without
        /// renderer features (no SSAO pass / depth-normals prepass). The mirror and pool reflection cameras pick it by name
        /// prefix (QualityAuto.UseAuxRenderer). One per source renderer ("RexMagnum_AuxRenderer_&lt;source name&gt;" in
        /// Assets/Settings), re-copied from the source each run so it follows changes to it.</summary>
        internal static void AddAuxRenderer(UniversalRenderPipelineAsset urp)
        {
            var so = new SerializedObject(urp);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0) return;
            // same rendering path / depth settings as the default renderer (one set of shader variants), no features
            if (!(list.GetArrayElementAtIndex(0).objectReferenceValue is UniversalRendererData source)) return;
            if (source.name.StartsWith(RexMagnum.QualityAuto.AuxRendererName, System.StringComparison.Ordinal)) return;
            string auxName = RexMagnum.QualityAuto.AuxRendererName + "_" + source.name;
            string path = "Assets/Settings/" + auxName + ".asset";
            var aux = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (aux == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
                aux = ScriptableObject.CreateInstance<UniversalRendererData>();
                aux.name = auxName;
                AssetDatabase.CreateAsset(aux, path);
            }
            // re-copied each run, but dirtied only when it changed (no asset write / reimport on every setup)
            string before = EditorJsonUtility.ToJson(aux);
            EditorUtility.CopySerialized(source, aux);
            var aso = new SerializedObject(aux);
            var feats = aso.FindProperty("m_RendererFeatures");
            if (feats != null) feats.arraySize = 0;
            var map = aso.FindProperty("m_RendererFeatureMap");
            if (map != null) map.arraySize = 0;
            aso.ApplyModifiedPropertiesWithoutUndo();
            aux.name = auxName;                            // CopySerialized brings the source's name along
            if (EditorJsonUtility.ToJson(aux) != before) EditorUtility.SetDirty(aux);
            // this asset's list: our aux once; other aux entries (the old shared one, another source's) out, so the
            // camera's prefix match finds this one
            bool has = false;
            var defIdx = so.FindProperty("m_DefaultRendererIndex");
            // a delete shifts the later slots down: keep the default renderer pointing at the same renderer
            void Removed(int at) { if (defIdx == null) return; if (defIdx.intValue > at) defIdx.intValue--; else if (defIdx.intValue == at) defIdx.intValue = 0; }
            for (int i = list.arraySize - 1; i >= 1; i--)
            {
                var e = list.GetArrayElementAtIndex(i);
                if (e.objectReferenceValue == null) { list.DeleteArrayElementAtIndex(i); Removed(i); continue; }   // a leftover empty slot
                if (!(e.objectReferenceValue is ScriptableRendererData d) ||
                    !d.name.StartsWith(RexMagnum.QualityAuto.AuxRendererName, System.StringComparison.Ordinal)) continue;
                if (d == aux && !has) { has = true; continue; }
                e.objectReferenceValue = null;             // (older Unity only nulls an object entry on the first delete)
                list.DeleteArrayElementAtIndex(i);
                Removed(i);
            }
            if (!has)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = aux;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
