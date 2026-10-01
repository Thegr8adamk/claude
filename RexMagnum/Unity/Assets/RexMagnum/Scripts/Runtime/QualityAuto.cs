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
            float[] detailDist     = { 40f,   70f,   110f,  160f };
            float[] detailDensity  = { 0.25f, 0.5f,  0.8f,  1f };
            float[] lodBias        = { 0.6f,  1f,    1.5f,  2f };
            float[] pixelError     = { 12f,   6f,    3f,    2f };
            int i = (int)t;
            if (urp != null)
            {
                urp.renderScale = renderScale[i];
                urp.shadowDistance = shadowDist[i];
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
            Debug.Log($"[Rex Magnum] Graphics: {t} (GPU {SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsMemorySize} MB, " +
                      $"RAM {SystemInfo.systemMemorySize} MB, {SystemInfo.processorCount} cores)");
        }

        /// <summary>Settings menu: -1 = auto, 0..3 = fixed tier.</summary>
        public void SetFromSettings(int value)
        {
            PlayerPrefs.SetInt(PrefKey, value);
            Apply(value >= 0 ? (QualityTier)value : Detect());
        }
    }
}
