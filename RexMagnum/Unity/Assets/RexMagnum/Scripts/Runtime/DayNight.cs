using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RexMagnum
{
    /// <summary>
    /// Day / night (brief section 6): one full cycle in ~20 minutes. The sun turns and changes colour; the sky,
    /// ambient light and fog follow. Night: blobs glow in their own colour, come out more and faster; torches,
    /// lamps and the hotel's warm windows switch on; guests go inside. Rex comments on his sunglasses.
    /// Night0..1 is public for other systems (cart headlights, music: Resources/Music/night_jungle).
    /// </summary>
    public class DayNight : MonoBehaviour
    {
        public Light sun;
        public float cycleMinutes = 20f;
        [Range(0f, 1f)] public float timeOfDay = 0.30f;     // 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset
        public float sunAzimuth = -145f;
        public Gradient sunColor, fogColor;
        public List<Light> nightLights = new List<Light>();  // torches, lamps, street lights
        public Renderer cloudDome;                          // RexMagnum/Clouds sphere (IslandBuilder.CloudDome); found by name if empty
        public static float Night { get; private set; }
        /// <summary>The cloud dome's own layer (unnamed user layer): the mirror and the interior / tunnel box probes
        /// leave it out of their culling mask (it sits at their far plane: clouds in a mirror at 27 m, in the rooms).</summary>
        public const int SkyLayer = 30;
        float baseSun, baseAmbient, baseFog, baseExposure;
        Color baseSkyTint, baseGround;
        // night floor: away from the lamps the island stays readable (it was near-black at 0.25 ambient / 0.12 sky)
        const float NightAmbient = 0.5f, NightExposure = 0.35f;
        static readonly Color MoonSkyTint = new Color(0.22f, 0.32f, 0.62f), MoonGround = new Color(0.12f, 0.15f, 0.24f);
        static readonly Color DuskSkyTint = new Color(0.75f, 0.5f, 0.42f);     // warm peach sky around sunrise / sunset
        Material sky, clouds;
        float baseCover = 0.45f;
        QualityAuto quality;
        float nextLine, nextEnv, glowNight = -1f;
        int probeBand = -1, probeId = -1;     // the island probe's day band (InteriorProbes' bands) / pass in flight
        ReflectionProbe islandProbe;
        BlobSpawner[] spawners;
        AudioSource nightMusic;
        float nightLevel;
        int skyDiscMode = -1;       // 0: the cloud layer draws sun + moon, 1: the sky's own sun disc, 2: none (Low tier at night)

        void Start()
        {
            if (sun == null) sun = RenderSettings.sun;
            baseSun = sun != null ? sun.intensity : 2f;
            baseAmbient = RenderSettings.ambientIntensity;
            baseFog = RenderSettings.fogDensity;
            sky = RenderSettings.skybox != null ? new Material(RenderSettings.skybox) : null;   // do not edit the asset
            RenderSettings.skybox = sky;
            if (sky != null && sky.HasProperty("_Exposure")) baseExposure = sky.GetFloat("_Exposure");
            if (sky != null && sky.HasProperty("_SkyTint")) baseSkyTint = sky.GetColor("_SkyTint");
            if (sky != null && sky.HasProperty("_GroundColor")) baseGround = sky.GetColor("_GroundColor");
            // the cloud layer: a copy of its material too (DayNight writes to it every frame)
            if (cloudDome == null) { var cd = GameObject.Find("CloudDome"); if (cd != null) cloudDome = cd.GetComponent<Renderer>(); }
            if (cloudDome != null && cloudDome.sharedMaterial != null)
            {
                clouds = new Material(cloudDome.sharedMaterial);
                cloudDome.sharedMaterial = clouds;
                if (clouds.HasProperty("_Cover")) baseCover = clouds.GetFloat("_Cover");
            }
            quality = FindAnyObjectByType<QualityAuto>();
            spawners = FindObjectsByType<BlobSpawner>(FindObjectsSortMode.None);   // every zone, not only the first
            foreach (var sp in spawners) if (sp.baseMax < 0) sp.baseMax = sp.maxAlive;
            if (sunColor == null || sunColor.colorKeys.Length < 2)
            {
                sunColor = new Gradient();
                sunColor.SetKeys(new[] { new GradientColorKey(new Color(0.35f, 0.45f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.3f), 0.25f),
                                         new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0.5f), new GradientColorKey(new Color(1f, 0.45f, 0.25f), 0.75f),
                                         new GradientColorKey(new Color(0.35f, 0.45f, 0.8f), 1f) },
                                 new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            }
            if (fogColor == null || fogColor.colorKeys.Length < 2)
            {
                fogColor = new Gradient();
                fogColor.SetKeys(new[] { new GradientColorKey(new Color(0.04f, 0.06f, 0.12f), 0f), new GradientColorKey(new Color(0.85f, 0.6f, 0.45f), 0.25f),
                                         new GradientColorKey(new Color(0.7f, 0.8f, 0.9f), 0.5f), new GradientColorKey(new Color(0.85f, 0.5f, 0.35f), 0.75f),
                                         new GradientColorKey(new Color(0.04f, 0.06f, 0.12f), 1f) },
                                 new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            }
            nightMusic = gameObject.AddComponent<AudioSource>();
            nightMusic.clip = Resources.Load<AudioClip>("Music/night_jungle");
            nightMusic.loop = true;
            nightMusic.volume = 0f;
            nightMusic.ignoreListenerPause = true;     // heard quietly in the pause menu (the Paused snapshot's music bus)
            if (nightMusic.clip != null) nightMusic.Play();
        }

        void Update()
        {
            timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime / (cycleMinutes * 60f), 1f);
            float elev = Mathf.Sin((timeOfDay - 0.25f) * Mathf.PI * 2f);        // -1 midnight .. 1 noon
            float day = Mathf.Clamp01(elev * 3f + 0.25f);
            Night = 1f - Mathf.Clamp01(elev * 4f + 0.6f);
            if (sun != null)
            {
                // never below 2 deg: a sun under the horizon lit the island from below at dawn / dusk
                float pitch = Mathf.Lerp(2f, 75f, Mathf.Max(0f, elev));
                // turn the sun in small steps: a tiny turn every frame makes all shadow edges crawl
                var want = Quaternion.Euler(elev > 0 ? pitch : 30f, sunAzimuth + (timeOfDay - 0.5f) * 180f, 0f);
                if (Quaternion.Angle(sun.transform.rotation, want) > 0.08f) sun.transform.rotation = want;
                sun.color = sunColor.Evaluate(timeOfDay);
                // low sun fades to moonlight strength by the horizon, so the switch to the high moon angle is not seen
                sun.intensity = Mathf.Lerp(0.12f, baseSun, Mathf.Clamp01(elev * 3f));   // night: moonlight
                sun.shadowStrength = Mathf.Lerp(0.5f, 0.9f, day);
            }
            RenderSettings.ambientIntensity = Mathf.Lerp(Mathf.Min(NightAmbient, baseAmbient), baseAmbient, day);
            RenderSettings.fogColor = fogColor.Evaluate(timeOfDay);
            RenderSettings.fogDensity = baseFog * Mathf.Lerp(1.6f, 1f, day);
            if (sky != null && sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", Mathf.Lerp(Mathf.Min(NightExposure, baseExposure), baseExposure, day));
            // cool moonlit sky (and so a cool skybox ambient) at night; the day colours stay exactly as built
            // sunrise / sunset: 1 with the sun at the horizon, 0 from ~15 deg up (and in deep night)
            float dusk = Mathf.Clamp01(1f - Mathf.Abs(elev) * 4f);
            if (sky != null && sky.HasProperty("_SkyTint"))
                sky.SetColor("_SkyTint", Color.Lerp(Color.Lerp(MoonSkyTint, baseSkyTint, day), DuskSkyTint, dusk * 0.35f));
            if (clouds != null)
            {
                clouds.SetFloat("_Dusk", dusk);
                clouds.SetFloat("_Night", Night);
                // the weather drifts: the coverage wanders slowly (a few changes per hour)
                clouds.SetFloat("_Cover", Mathf.Clamp01(baseCover + (Mathf.PerlinNoise(Time.time * 0.002f, 3.1f) - 0.5f) * 0.4f));
                // sun / moon discs: the true sun path (below the horizon at night, unlike the moonlight's fixed 30 deg
                // light), the moon opposite. By day it matches the light, so the disc sits where the shadows point from.
                float discPitch = elev > 0 ? Mathf.Lerp(2f, 75f, elev) : elev * 75f;
                Vector3 sunDir = -(Quaternion.Euler(discPitch, sunAzimuth + (timeOfDay - 0.5f) * 180f, 0f) * Vector3.forward);
                clouds.SetVector("_SunDir", sunDir);
                clouds.SetVector("_MoonDir", -sunDir);
            }
            if (sky != null && sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", Color.Lerp(MoonGround, baseGround, day));
            if (Time.time > nextLights) { nextLights = Time.time + 0.5f; NightLights(Night > 0.3f); }
            FadeLights();
            // the glow only when Night really changed (a property block write per blob per frame added up); new blobs set it in Start
            if (Mathf.Abs(Night - glowNight) > 0.01f || (Night == 0f && glowNight != 0f))      // and exactly off by day
            {
                glowNight = Night;
                foreach (var b in Blob.All) if (b != null) b.SetNightGlow(Night);
            }
            if (spawners != null)
                foreach (var sp in spawners)
                    if (sp != null) sp.maxAlive = Mathf.Max(1, Mathf.RoundToInt(sp.baseMax * Mathf.Lerp(1f, 1.8f, Night) * sp.factoryScale));
            // the slow fade keeps its own level; the voice duck (fast) is applied on top of it
            // King Glorp's fight: his power ballad plays, the night loop fades out in about a second (and back slowly after)
            bool boss = KingGlorpBoss.Fighting;
            // the music bus on top (not in the level): it follows the snapshots while paused (deltaTime 0 holds the level)
            nightLevel = Mathf.MoveTowards(nightLevel, boss ? 0f : Night * 0.5f, Time.deltaTime * (boss ? 0.5f : 0.1f));
            if (nightMusic != null) nightMusic.volume = nightLevel * AudioMix.Music * AudioMix.Duck;
            // ambient light and sky reflections follow the time of day (they were computed once, at start)
            if (Time.time > nextEnv)
            {
                nextEnv = Time.time + 2f;
                // the ambient probe re-bake only when the sky / ambient / sun colour really changed (it stalls a frame)
                float exposure = sky != null && sky.HasProperty("_Exposure") ? sky.GetFloat("_Exposure") : 0f;
                Color sc = sun != null ? sun.color : Color.black;
                if (Mathf.Abs(RenderSettings.ambientIntensity - envAmbient) > 0.03f || Mathf.Abs(exposure - envExposure) > 0.03f ||
                    Mathf.Abs(sc.r - envSun.r) + Mathf.Abs(sc.g - envSun.g) + Mathf.Abs(sc.b - envSun.b) > 0.06f)
                {
                    envAmbient = RenderSettings.ambientIntensity;
                    envExposure = exposure;
                    envSun = sc;
                    DynamicGI.UpdateEnvironment();
                }
                // the ambient probe may update a frame late: checked every 2 s, applied when it really changed
                ScaleProbes();
                // the cloud layer is ~10 noise lookups per sky pixel: off on the Low (phone) tier
                if (cloudDome != null)
                {
                    bool cloudsOn = quality == null || quality.Tier != QualityTier.Low;
                    if (cloudDome.enabled != cloudsOn) cloudDome.enabled = cloudsOn;
                    // the cloud layer draws the sun + moon discs: the Procedural sky's own sun disc goes off then (it
                    // followed the night light: a sun-like disc in the moonlight's place); without clouds (Low tier) it
                    // stays by day and goes off at night, for the same reason
                    bool discs = cloudsOn && clouds != null && clouds.HasProperty("_Discs");
                    int mode = discs ? 0 : Night > 0.5f ? 2 : 1;
                    if (mode != skyDiscMode)
                    {
                        skyDiscMode = mode;
                        if (clouds != null && clouds.HasProperty("_Discs")) clouds.SetFloat("_Discs", discs ? 1f : 0f);
                        if (sky != null && sky.HasProperty("_SunDisk"))
                        {
                            sky.SetFloat("_SunDisk", mode == 1 ? 2f : 0f);     // High Quality (as built) / None
                            foreach (var kw in new[] { "_SUNDISK_NONE", "_SUNDISK_SIMPLE", "_SUNDISK_HIGH_QUALITY" }) sky.DisableKeyword(kw);
                            sky.EnableKeyword(mode == 1 ? "_SUNDISK_HIGH_QUALITY" : "_SUNDISK_NONE");
                        }
                    }
                }
                if (islandProbe == null)
                {
                    var go = GameObject.Find("ReflectionProbe");
                    if (go != null && go.TryGetComponent(out islandProbe))
                    {
                        islandProbe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                        // one cube face per frame: a re-render is spread over ~9 frames, never six island renders in one
                        islandProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
                    }
                }
                // the 2.4 km probe renders the whole island: it rendered once on awake (OnAwake), then again only when
                // the day band changes (day / dawn-dusk / night, the same bands as InteriorProbes). It was every 30 s all
                // day. ZoneStreamer shows every zone while a pass is in flight (it culls the far ones for the player's
                // camera, and the probe would bake an island with holes); a pass is ~9 frames, a few times a day.
                int band = Night < 0.3f ? 0 : Night < 0.7f ? 1 : 2;
                if (probeBand < 0) probeBand = band;
                if (probeId >= 0 && (islandProbe == null || islandProbe.IsFinishedRendering(probeId)))
                {
                    probeId = -1;
                    ZoneStreamer.HoldAll = false;
                }
                if (islandProbe != null && islandProbe.isActiveAndEnabled && probeId < 0 && band != probeBand)
                {
                    ZoneStreamer.ShowAllNow();
                    probeId = islandProbe.RenderProbe();
                    probeBand = band;
                }
            }
            if (Night > 0.8f && Time.time > nextLine)
            {
                nextLine = Time.time + 240f;
                var rex = FindAnyObjectByType<RexController>();
                if (rex != null) RexVoice.Say(rex.gameObject, "Night", true);
            }
        }

        // ---- baked light probes vs day / night
        // Bake Lighting fills the probes with a flat dim ambient (all lights are realtime), so a probe only holds how
        // open it is (sky visibility / AO). Used as is, the baked SH replaced the live sky ambient: characters, LOD1
        // and trees on probes kept one fixed brightness day and night. Each probe keeps its baked SH (openness clamped
        // 0.05 - 1.2) with its own direction (light from the open side, dark under eaves); whenever the ambient changes
        // it is scaled per colour channel by the live ambient L0 over the open-air probes' baked L0. Robust against any bake ambient, and the sky colour (dawn, moon) comes through.
        // (It was the live ambient SH x openness: every probe got the sky's direction, also deep under a roof.)
        // The buildings use these probes too (Bake Lighting gives them no lightmaps: a lightmap holds one fixed
        // brightness, URP Lit cannot scale it at runtime, and lightmapped buildings stayed lit the same day and night).
        float[] probeOpen;
        SphericalHarmonicsL2[] probeNorm, probeLive;
        Vector3 probeRef;                                // open-air baked L0 per colour channel
        Vector3 probeAmb = new Vector3(-1f, -1f, -1f);

        static float L0(SphericalHarmonicsL2 sh) => sh[0, 0] * 0.3f + sh[1, 0] * 0.59f + sh[2, 0] * 0.11f;

        void InitProbes(LightProbes lp)
        {
            probeOpen = null;
            var baked = lp.bakedProbes;                  // a copy: the original data stays with the scene
            if (baked == null || baked.Length == 0) return;
#if UNITY_EDITOR
            // in the Editor these probes are the scene's LightingData asset itself: keep the baked values once and put
            // them back on play-mode exit (OnDestroy), or the scaled SH of the last frame stayed in the asset for good
            if (probeOrig == null || probeAsset != lp) { probeOrig = baked; probeAsset = lp; }
#endif
            var lum = new float[baked.Length];
            for (int i = 0; i < baked.Length; i++) lum[i] = L0(baked[i]);
            var sorted = (float[])lum.Clone();
            System.Array.Sort(sorted);
            // the 90th percentile = open air (most of the grid is outdoors); brighter ones (ground bounce) cap at 1.2
            float open = Mathf.Max(1e-4f, sorted[Mathf.Min(sorted.Length - 1, (int)(sorted.Length * 0.9f))]);
            probeOpen = new float[baked.Length];
            probeNorm = new SphericalHarmonicsL2[baked.Length];
            // the open-air probe's own colour, per channel (floored at 20% of its luminance: a channel near 0 in the
            // bake must not blow up the scale)
            int refI = System.Array.IndexOf(lum, open);
            var rs = refI >= 0 ? baked[refI] : baked[0];
            probeRef = new Vector3(Mathf.Max(rs[0, 0], open * 0.2f), Mathf.Max(rs[1, 0], open * 0.2f), Mathf.Max(rs[2, 0], open * 0.2f));
            for (int i = 0; i < baked.Length; i++)
            {
                probeOpen[i] = Mathf.Clamp(lum[i] / open, 0.05f, 1.2f);
                // the clamp as a factor on the baked SH (1 for nearly all probes), so the direction stays as baked
                probeNorm[i] = baked[i] * (lum[i] > 1e-6f ? probeOpen[i] * open / lum[i] : 0f);
                if (lum[i] <= 1e-6f) probeNorm[i].AddAmbientLight(Color.white * (0.05f * open));   // an all-black probe: the floor
            }
            probeLive = new SphericalHarmonicsL2[baked.Length];
            probeAmb = new Vector3(-1f, -1f, -1f);
        }

        void ScaleProbes()
        {
            var lp = LightmapSettings.lightProbes;
            if (lp == null || lp.count == 0) return;     // not baked: everything already uses the live ambient probe
            if (probeOpen == null || probeOpen.Length != lp.count) InitProbes(lp);   // first time, or a zone scene added probes
            if (probeOpen == null) return;
            var amb = RenderSettings.ambientProbe;
            var a = new Vector3(amb[0, 0], amb[1, 0], amb[2, 0]);
            // only on a noticeable change (2%): writing the probes re-uploads them
            if (Mathf.Abs(a.x - probeAmb.x) + Mathf.Abs(a.y - probeAmb.y) + Mathf.Abs(a.z - probeAmb.z) < 0.02f * Mathf.Max(0.05f, a.x + a.y + a.z)) return;
            probeAmb = a;
            var sc = new Vector3(a.x / probeRef.x, a.y / probeRef.y, a.z / probeRef.z);
            for (int i = 0; i < probeLive.Length; i++)
            {
                var sh = probeNorm[i];
                for (int k = 0; k < 9; k++) { sh[0, k] *= sc.x; sh[1, k] *= sc.y; sh[2, k] *= sc.z; }
                probeLive[i] = sh;
            }
            lp.bakedProbes = probeLive;
        }

#if UNITY_EDITOR
        SphericalHarmonicsL2[] probeOrig;
        LightProbes probeAsset;

        void OnDestroy()
        {
            // only when it is still the same data (a zone scene that added probes made a new set: nothing to undo then)
            if (probeOrig != null && probeAsset != null && probeAsset == LightmapSettings.lightProbes && probeAsset.count == probeOrig.Length)
                probeAsset.bakedProbes = probeOrig;
            probeOrig = null;
        }
#endif

        float envAmbient = -1f, envExposure = -1f, nextLights;
        Color envSun = new Color(-1f, -1f, -1f);
        // realtime point / spot lights are costly (and URP has a per-object light limit): only the nearest burn
        const int MaxNightLights = 12;
        Transform rexT;
        float[] lightDist;

        float[] lightBase;          // each light's built intensity (the fade goes 0 .. this)
        bool[] lightWant;           // chosen to burn (fades in); false: fades out, then disabled
        const float LightFade = 2f;  // per second, of the base intensity: ~0.5 s in / out (no popping lamps)
        const float SwapCloser = 0.85f * 0.85f;   // a light must be 15% closer than the weakest burning one to take its place

        /// <summary>Night: the MaxNightLights lights nearest to Rex on (with hysteresis, so they do not flicker
        /// at equal distances), all others off. Day: all off. Lights fade in / out in FadeLights.</summary>
        void NightLights(bool night)
        {
            int n = nightLights.Count;
            if (n == 0) return;
            if (lightBase == null || lightBase.Length != n)
            {
                lightBase = new float[n];
                lightWant = new bool[n];
                lightDist = new float[n];
                for (int i = 0; i < n; i++)
                {
                    var l = nightLights[i];
                    lightBase[i] = l != null ? l.intensity : 0f;
                    if (l != null && l.enabled) { l.enabled = false; l.intensity = 0f; }     // all start dark, chosen below
                }
            }
            if (!night)
            {
                for (int i = 0; i < n; i++) lightWant[i] = false;
                return;
            }
            if (rexT == null) { var rc = FindAnyObjectByType<RexController>(); if (rc != null) rexT = rc.transform; }
            Vector3 c = rexT != null ? rexT.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            for (int i = 0; i < n; i++)
                lightDist[i] = nightLights[i] != null ? (nightLights[i].transform.position - c).sqrMagnitude : float.MaxValue;
            // ranked once per pass (it was a full scan for every light switched: O(n^2) with many lamps)
            if (lightOrder == null || lightOrder.Length != n) { lightOrder = new int[n]; lightKey = new float[n]; }
            int wanted = 0, lit = 0;      // lit = enabled (also still fading out): never more than MaxNightLights
            for (int i = 0; i < n; i++)
            {
                if (nightLights[i] == null) lightWant[i] = false;
                else if (nightLights[i].enabled) lit++;
                if (lightWant[i]) wanted++;
                lightOrder[i] = i;
                lightKey[i] = lightDist[i];
            }
            System.Array.Sort(lightKey, lightOrder);                    // nearest first
            // nearest unchosen light from the front, farthest chosen one from the back. A dropped light is always
            // farther than the current nearest (and a newly chosen one nearer than the dropped), so both only move on.
            int np = 0, fp = n - 1;
            while (true)
            {
                while (np < n && (lightWant[lightOrder[np]] || nightLights[lightOrder[np]] == null)) np++;
                if (np >= n) break;
                int near = lightOrder[np];
                if (wanted < MaxNightLights)
                {
                    // a free slot (a fading-out light still counts as on until it is dark)
                    if (nightLights[near].enabled || lit < MaxNightLights)
                    {
                        if (!nightLights[near].enabled) lit++;
                        lightWant[near] = true; wanted++; SwitchOn(near);
                        continue;
                    }
                    break;
                }
                // full: the weakest burning light fades out only for a clearly nearer one (it switches on once its slot is free)
                while (fp >= 0 && !lightWant[lightOrder[fp]]) fp--;
                if (fp >= 0 && lightDist[near] < lightDist[lightOrder[fp]] * SwapCloser) { lightWant[lightOrder[fp]] = false; wanted--; continue; }
                break;
            }
        }

        int[] lightOrder;
        float[] lightKey;

        void SwitchOn(int i)
        {
            var l = nightLights[i];
            if (l == null || l.enabled) return;
            l.intensity = 0f;
            l.enabled = true;
        }

        /// <summary>Every frame: chosen lights fade up to their intensity, dropped ones fade out and switch off.</summary>
        void FadeLights()
        {
            if (lightBase == null || lightBase.Length != nightLights.Count) return;
            for (int i = 0; i < lightBase.Length; i++)
            {
                var l = nightLights[i];
                if (l == null || !l.enabled) continue;
                float target = lightWant[i] ? lightBase[i] : 0f;
                if (l.intensity != target) l.intensity = Mathf.MoveTowards(l.intensity, target, lightBase[i] * LightFade * Time.deltaTime);
                if (!lightWant[i] && l.intensity <= 0f) l.enabled = false;
            }
        }

        /// <summary>Night multiplier for blob speed (brief: many fast glowing blobs at night).</summary>
        public static float BlobSpeed => 1f + Night * 0.6f;
    }
}
