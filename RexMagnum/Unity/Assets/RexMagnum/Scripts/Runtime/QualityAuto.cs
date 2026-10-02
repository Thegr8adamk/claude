using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RexMagnum
{
    public enum QualityTier { Low, Medium, High, Ultra }

    /// <summary>
    /// Picks safe graphics settings from the hardware at start (no manual specs check needed):
    /// video memory, system memory, CPU cores and platform. The player can override it later in Settings
    /// (saved in PlayerPrefs "RexMagnum_Quality", -1 = auto). Tiers change render scale, shadows, MSAA,
    /// terrain trees / grass distance and density, LOD bias and frame-rate target.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class QualityAuto : MonoBehaviour
    {
        public const string PrefKey = "RexMagnum_Quality";
        public Terrain[] terrains;
        public QualityTier Tier { get; private set; }

        void Awake()
        {
            if (terrains == null || terrains.Length == 0) terrains = Terrain.activeTerrains;
            int saved = PlayerPrefs.GetInt(PrefKey, -1);
            Apply(saved >= 0 ? (QualityTier)Mathf.Clamp(saved, 0, 3) : Detect());
        }

        void Start()
        {
            DynamicGI.UpdateEnvironment();     // ambient light from the sky
        }

        public static QualityTier Detect()
        {
            int vram = SystemInfo.graphicsMemorySize;      // MB
            int ram = SystemInfo.systemMemorySize;         // MB
            int cores = SystemInfo.processorCount;
            if (Application.isMobilePlatform)
                return (ram >= 8000 && vram >= 3000) ? QualityTier.Medium : QualityTier.Low;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return QualityTier.Low;
            bool integrated = SystemInfo.graphicsDeviceName.ToLower().Contains("intel") &&
                              !SystemInfo.graphicsDeviceName.ToLower().Contains("arc");
            if (integrated || vram < 2500 || ram < 7000) return QualityTier.Low;
            if (vram < 5000 || ram < 12000 || cores < 6) return QualityTier.Medium;
            if (vram < 9000 || ram < 24000) return QualityTier.High;
            return QualityTier.Ultra;
        }

        public void Apply(QualityTier t)
        {
            Tier = t;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            //                         Low    Medium High   Ultra
            float[] renderScale    = { 0.75f, 0.9f,  1f,    1f };
            float[] shadowDist     = { 40f,   80f,   140f,  220f };
            int[]   msaa           = { 1,     2,     4,     4 };
            float[] treeDist       = { 250f,  400f,  650f,  900f };
            // grass: max 1 clump (~200 tris) per m2 in the map; worst-case view ~0.1 / 0.6 / 1.4 / 2.6 M triangles
            float[] detailDist     = { 35f,   60f,   80f,   100f };
            float[] detailDensity  = { 0.15f, 0.25f, 0.35f, 0.45f };
            float[] lodBias        = { 0.6f,  1f,    1.5f,  2f };
            float[] pixelError     = { 12f,   6f,    3f,    2f };
            int[]   cascades       = { 1,     2,     4,     4 };
            int[]   shadowRes      = { 1024,  2048,  4096,  4096 };
            int i = (int)t;
            if (urp != null)
            {
                urp.renderScale = renderScale[i];
                urp.shadowDistance = shadowDist[i];
                SetUrp(urp, "shadowCascadeCount", "m_ShadowCascadeCount", cascades[i]);
                SetUrp(urp, "mainLightShadowmapResolution", "m_MainLightShadowmapResolution", shadowRes[i]);
                SetUrp(urp, "supportsSoftShadows", "m_SoftShadowsSupported", true);
                SetUrp(urp, "supportsAdditionalLightShadows", "m_AdditionalLightShadowsSupported", t >= QualityTier.High);   // lamp / torch shadows
                urp.msaaSampleCount = msaa[i];
                urp.supportsCameraDepthTexture = true;    // water depth colour + shore foam
                urp.supportsCameraOpaqueTexture = false;
            }
            QualitySettings.lodBias = lodBias[i];
            QualitySettings.anisotropicFiltering = t >= QualityTier.High ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            Application.targetFrameRate = Application.isMobilePlatform ? 30 : 60;
            QualitySettings.vSyncCount = Application.isMobilePlatform ? 0 : 1;
            foreach (var ter in terrains)
            {
                if (ter == null) continue;
                ter.treeDistance = treeDist[i];
                ter.treeBillboardDistance = treeDist[i] * 0.5f;
                ter.detailObjectDistance = detailDist[i];
                ter.detailObjectDensity = detailDensity[i];
                ter.heightmapPixelError = pixelError[i];
                ter.basemapDistance = t >= QualityTier.High ? 400f : 150f;
                ter.drawInstanced = true;
            }
            ApplyDisplay();
            Debug.Log($"[Rex Magnum] Graphics: {t} (GPU {SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsMemorySize} MB, " +
                      $"RAM {SystemInfo.systemMemorySize} MB, {SystemInfo.processorCount} cores)");
        }

        /// <summary>Sets a URP asset setting on every URP version: the public property if it has a public setter,
        /// otherwise its serialized field (some shadow settings only have internal setters). Enums take the int value.</summary>
        public static void SetUrp(UniversalRenderPipelineAsset urp, string prop, string field, object value)
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance |
                                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            if (urp == null) return;
            var t = typeof(UniversalRenderPipelineAsset);
            try
            {
                var p = t.GetProperty(prop, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                var set = p != null ? p.GetSetMethod(false) : null;
                if (set != null) { set.Invoke(urp, new[] { ToType(value, p.PropertyType) }); return; }
                var f = t.GetField(field, F);
                if (f != null) f.SetValue(urp, ToType(value, f.FieldType));
            }
            catch (System.Exception e) { Debug.LogWarning($"[Rex Magnum] URP setting {prop}: {e.Message}"); }
        }

        static object ToType(object v, System.Type to) =>
            to.IsEnum ? System.Enum.ToObject(to, v) : System.Convert.ChangeType(v, to);

        // ---- Settings > Graphics extras (saved in PlayerPrefs; 0 / default = follow the quality tier)
        public static readonly string[] RenderScaleNames = { "FROM QUALITY", "50%", "67%", "75%", "85%", "100%", "125%" };
        static readonly float[] RenderScales = { 0f, 0.5f, 0.67f, 0.75f, 0.85f, 1f, 1.25f };
        public static readonly string[] FpsNames = { "30", "60", "120", "NO LIMIT" };
        static readonly int[] FpsValues = { 30, 60, 120, -1 };
        public static int FpsDefault => Application.isMobilePlatform ? 0 : 1;

        /// <summary>Render scale, frame limit, V-sync and full screen on top of the tier.</summary>
        public static void ApplyDisplay()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            int rs = Mathf.Clamp(PlayerPrefs.GetInt("RexMagnum_RenderScale", 0), 0, RenderScales.Length - 1);
            if (rs > 0 && urp != null) urp.renderScale = RenderScales[rs];
            QualitySettings.vSyncCount = PlayerPrefs.GetInt("RexMagnum_VSync", Application.isMobilePlatform ? 0 : 1);
            Application.targetFrameRate = FpsValues[Mathf.Clamp(PlayerPrefs.GetInt("RexMagnum_FpsLimit", FpsDefault), 0, FpsValues.Length - 1)];
            if (!Application.isMobilePlatform && !Application.isEditor)
            {
                var mode = PlayerPrefs.GetInt("RexMagnum_Fullscreen", 1) == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (Screen.fullScreenMode != mode)
                {
                    // full screen at the monitor's own size; a window at 3/4 of it (not a window as big as the screen)
                    int w = Display.main.systemWidth, h = Display.main.systemHeight;
                    if (mode == FullScreenMode.Windowed) { w = w * 3 / 4; h = h * 3 / 4; }
                    Screen.SetResolution(w, h, mode);
                }
            }
        }

        /// <summary>Settings menu: -1 = auto, 0..3 = fixed tier.</summary>
        public void SetFromSettings(int value)
        {
            PlayerPrefs.SetInt(PrefKey, value);
            Apply(value >= 0 ? (QualityTier)value : Detect());
        }
    }
}
