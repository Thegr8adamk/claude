using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RexMagnum
{
    public enum QualityTier { Low, Medium, High, Ultra }

    /// <summary>
    /// Picks safe graphics settings from the hardware at start (no manual specs check needed):
    /// video memory, system memory, CPU cores and platform. The player can override it later in Settings
    /// (saved in PlayerPrefs "RexMagnum_Quality", -1 = auto). Tiers change render scale, shadows, MSAA / SMAA, SSAO,
    /// reflection probe size,
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
            UsePlayCopy();
            if (terrains == null || terrains.Length == 0) terrains = Terrain.activeTerrains;
            int saved = PlayerPrefs.GetInt(PrefKey, -1);
            Apply(saved >= 0 ? (QualityTier)Mathf.Clamp(saved, 0, 3) : Detect());
        }

        void Start()
        {
            DynamicGI.UpdateEnvironment();     // ambient light from the sky
        }

        // the tier's anti-aliasing, for cameras made after Apply (kill cam, selfie, ending): they had no
        // UniversalAdditionalCameraData, so URP gave them no SMAA / the asset's MSAA whatever the tier
        bool tierSmaa, tierMsaa;
        float camCheck;
        readonly System.Collections.Generic.HashSet<Camera> seenCams = new System.Collections.Generic.HashSet<Camera>();

        void Update()
        {
            if (Time.unscaledTime < camCheck) return;
            camCheck = Time.unscaledTime + 0.5f;           // twice a second is enough: Camera.allCameras allocates
            if (seenCams.Count > 64) seenCams.RemoveWhere(c => c == null);
            foreach (var c in Camera.allCameras)
                if (seenCams.Add(c)) TierAA(c);
        }

        void TierAA(Camera c)
        {
            if (c.targetTexture != null) return;           // mirror / photo cameras keep theirs
            if (!c.TryGetComponent(out UniversalAdditionalCameraData cd)) cd = c.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cd.antialiasing = tierSmaa ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            c.allowMSAA = tierMsaa;
        }

        // ---- Editor: tier changes must not be saved into the project's URP asset / renderer data.
        // Play mode runs on a copy of the URP asset; the original asset and the SSAO switch are put back on exit.
        // The renderer data (SSAO on/off and its settings) is shared with the original asset: also put back on exit,
        // on editor quit and around every save made during play (a save would write the DontSave copy, or the
        // tier's SSAO state, into the project settings).
#if UNITY_EDITOR
        static RenderPipelineAsset origDefault, origQuality, playCopy;
        static bool swapped;
        static readonly System.Collections.Generic.Dictionary<ScriptableRendererFeature, bool> featureState =
            new System.Collections.Generic.Dictionary<ScriptableRendererFeature, bool>();
        static readonly System.Collections.Generic.Dictionary<ScriptableRendererFeature, bool> featureNow =
            new System.Collections.Generic.Dictionary<ScriptableRendererFeature, bool>();
#endif

        static void UsePlayCopy()
        {
#if UNITY_EDITOR
            if (swapped || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;
            swapped = true;
            origDefault = GraphicsSettings.defaultRenderPipeline;
            origQuality = QualitySettings.renderPipeline;
            var copy = Instantiate(urp);
            copy.name = urp.name + " (play copy)";
            copy.hideFlags = HideFlags.DontSave;
            playCopy = copy;
            SetPipeline(copy);
            UnityEditor.EditorApplication.playModeStateChanged += RestoreAfterPlay;
            UnityEditor.EditorApplication.quitting += RestoreOnQuit;
#endif
        }

#if UNITY_EDITOR
        static void SetPipeline(RenderPipelineAsset a)
        {
            if (origQuality != null) QualitySettings.renderPipeline = a;   // the quality level's asset wins if set
            else GraphicsSettings.defaultRenderPipeline = a;
        }

        /// <summary>Original asset, SSAO switch and SSAO settings back in place (what the project should save).</summary>
        static void PutBackOriginals()
        {
            SetPipeline(origQuality != null ? origQuality : origDefault);
            foreach (var kv in featureState) if (kv.Key != null) FeatureSet(kv.Key, kv.Value);
            foreach (var kv in ssaoOrig) kv.Key.f.SetValue(kv.Key.o, kv.Value);
        }

        static void RestoreAfterPlay(UnityEditor.PlayModeStateChange s)
        {
            if (s != UnityEditor.PlayModeStateChange.ExitingPlayMode && s != UnityEditor.PlayModeStateChange.EnteredEditMode) return;
            UnityEditor.EditorApplication.playModeStateChanged -= RestoreAfterPlay;
            UnityEditor.EditorApplication.quitting -= RestoreOnQuit;
            if (!swapped) return;
            swapped = false;
            PutBackOriginals();
            featureState.Clear();
            featureNow.Clear();
            ssaoOrig.Clear();
            playCopy = null;
        }

        static void RestoreOnQuit() => RestoreAfterPlay(UnityEditor.PlayModeStateChange.ExitingPlayMode);

        /// <summary>A save during play (Ctrl+S, File > Save Project): originals in for the save, the play state after.</summary>
        class SaveGuard : UnityEditor.AssetModificationProcessor
        {
            static string[] OnWillSaveAssets(string[] paths)
            {
                if (!swapped) return paths;
                PutBackOriginals();
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (!swapped || !Application.isPlaying) return;
                    SetPipeline(playCopy);
                    foreach (var kv in featureNow) if (kv.Key != null) FeatureSet(kv.Key, kv.Value);
                    foreach (var kv in ssaoNow) kv.Key.f.SetValue(kv.Key.o, kv.Value);
                };
                return paths;
            }
        }
#endif

        // SSAO settings (m_Settings of the feature, by reflection: not public in URP): first value seen per field, and
        // the tier's value, so High / Ultra get the original back and the editor can undo it
        static readonly System.Collections.Generic.Dictionary<(object o, System.Reflection.FieldInfo f), object> ssaoOrig =
            new System.Collections.Generic.Dictionary<(object o, System.Reflection.FieldInfo f), object>();
        static readonly System.Collections.Generic.Dictionary<(object o, System.Reflection.FieldInfo f), object> ssaoNow =
            new System.Collections.Generic.Dictionary<(object o, System.Reflection.FieldInfo f), object>();

        /// <summary>Medium: SSAO at half resolution with the low sample count (about a quarter of the cost);
        /// High / Ultra: the asset's own settings. Skips quietly when this URP version has other field names.</summary>
        static void SsaoCost(ScriptableRendererFeature ssao, bool cheap)
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance |
                                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            try
            {
                var settings = ssao.GetType().GetField("m_Settings", F)?.GetValue(ssao);
                if (settings == null) return;
                var st = settings.GetType();
                void Set(string name, System.Func<System.Type, object> cheapValue)
                {
                    var f = st.GetField(name, F);
                    if (f == null) return;
                    var key = (settings, f);
                    if (!ssaoOrig.ContainsKey(key)) ssaoOrig[key] = f.GetValue(settings);
                    object v = cheap ? cheapValue(f.FieldType) : ssaoOrig[key];
                    if (v == null) return;
                    f.SetValue(settings, v);
                    ssaoNow[key] = v;
                }
                Set("Downsample", t => t == typeof(bool) ? (object)true : null);
                // URP 14+: enum AOSampleOption (High / Medium / Low); older: int SampleCount
                Set("Samples", t => t.IsEnum && System.Enum.IsDefined(t, "Low") ? System.Enum.Parse(t, "Low") : null);
                if (st.GetField("Samples", F) == null) Set("SampleCount", t => t == typeof(int) ? (object)4 : null);
            }
            catch (System.Exception e) { Debug.LogWarning($"[Rex Magnum] SSAO settings: {e.Message}"); }
        }

        // renderer feature on/off by reflection (isActive / SetActive), like SetUrp
        static bool FeatureActive(object f) => f.GetType().GetProperty("isActive")?.GetValue(f) is bool b ? b : true;
        static void FeatureSet(object f, bool on) => f.GetType().GetMethod("SetActive", new[] { typeof(bool) })?.Invoke(f, new object[] { on });

        /// <summary>The SSAO renderer feature on the first renderer of the URP asset (added by Build Island), or null.
        /// The renderer list is not public in every URP version: read the serialized field.</summary>
        static ScriptableRendererFeature FindSSAO(UniversalRenderPipelineAsset urp)
        {
            var f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            var list = f != null ? f.GetValue(urp) as ScriptableRendererData[] : null;
            if (list == null) return null;
            foreach (var data in list)
            {
                if (data == null) continue;
                foreach (var feat in data.rendererFeatures)
                    if (feat != null && feat.GetType().Name.Contains("AmbientOcclusion")) return feat;
            }
            return null;
        }

        static readonly System.Collections.Generic.Dictionary<ReflectionProbe, int> probeRes =
            new System.Collections.Generic.Dictionary<ReflectionProbe, int>();

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
            // one anti-aliasing per tier: Low none, Medium SMAA (post, cheap on desktop), High / Ultra MSAA 2x / 4x.
            // Phones: MSAA 2x on Medium instead of SMAA (MSAA is nearly free on tiled mobile GPUs, SMAA is not)
            bool mobile = Application.isMobilePlatform;
            int[]   msaa           = { 1,     mobile ? 2 : 1, 2, 4 };
            bool smaa = t == QualityTier.Medium && !mobile;
            float[] treeDist       = { 250f,  400f,  650f,  900f };
            // grass: max 1 clump (~200 tris) per m2 in the map; worst-case view ~0.1 / 0.6 / 1.4 / 2.6 M triangles
            float[] detailDist     = { 35f,   60f,   80f,   100f };
            float[] detailDensity  = { 0.15f, 0.25f, 0.35f, 0.45f };
            float[] lodBias        = { 0.6f,  1f,    1.5f,  2f };
            float[] pixelError     = { 12f,   6f,    3f,    2f };
            int[]   cascades       = { 1,     2,     4,     4 };
            int[]   shadowRes      = { 1024,  2048,  2048,  4096 };
            int i = (int)t;
            if (urp != null)
            {
                urp.renderScale = renderScale[i];
                urp.shadowDistance = shadowDist[i];
                SetUrp(urp, "shadowCascadeCount", "m_ShadowCascadeCount", cascades[i]);
                SetUrp(urp, "mainLightShadowmapResolution", "m_MainLightShadowmapResolution", shadowRes[i]);
                // soft (filtered) shadow edges from High up, and Medium on desktop: hard 1-tap edges looked jagged
                SetUrp(urp, "supportsSoftShadows", "m_SoftShadowsSupported", t >= QualityTier.High || (t == QualityTier.Medium && !mobile));
                // lamp / torch / tunnel-lamp shadows (point + spot) only on Ultra: they are the expensive ones
                SetUrp(urp, "supportsAdditionalLightShadows", "m_AdditionalLightShadowsSupported", t == QualityTier.Ultra);
                urp.msaaSampleCount = msaa[i];
                urp.supportsCameraDepthTexture = true;    // water depth colour + shore foam
                urp.supportsCameraOpaqueTexture = false;
                // SSAO only from Medium up (a full-screen pass + depth normals prepass: too slow on Low)
                var ssao = FindSSAO(urp);
                if (ssao != null)
                {
#if UNITY_EDITOR
                    if (!featureState.ContainsKey(ssao)) featureState[ssao] = FeatureActive(ssao);
                    featureNow[ssao] = t >= QualityTier.Medium;
#endif
                    FeatureSet(ssao, t >= QualityTier.Medium);
                    SsaoCost(ssao, t == QualityTier.Medium);
                }
            }
            tierSmaa = smaa;
            tierMsaa = msaa[i] > 1;
            seenCams.Clear();                              // Update gives every live camera the new tier's AA
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.targetTexture != null || !cam.TryGetComponent(out UniversalAdditionalCameraData cd)) continue;  // mirror / photo cameras keep theirs
                cd.antialiasing = smaa ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
                cam.allowMSAA = msaa[i] > 1;
            }
            // reflection probes: 128 px cubemaps on Low (the island probe is 256), original size otherwise
            foreach (var rp in FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!probeRes.TryGetValue(rp, out int baseRes)) probeRes[rp] = baseRes = rp.resolution;
                rp.resolution = t == QualityTier.Low ? Mathf.Min(baseRes, 128) : baseRes;
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
