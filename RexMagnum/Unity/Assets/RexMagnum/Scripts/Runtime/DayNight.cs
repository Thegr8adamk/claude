using System.Collections.Generic;
using UnityEngine;

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
        public static float Night { get; private set; }
        float baseSun, baseAmbient, baseFog, baseExposure;
        Material sky;
        float nextLine, nextEnv, nextProbe = 30f, probeTime = -1f, glowNight = -1f;
        ReflectionProbe islandProbe;
        BlobSpawner[] spawners;
        AudioSource nightMusic;
        float nightLevel;

        void Start()
        {
            if (sun == null) sun = RenderSettings.sun;
            baseSun = sun != null ? sun.intensity : 2f;
            probeTime = timeOfDay;      // the probe renders once on awake with this sky
            baseAmbient = RenderSettings.ambientIntensity;
            baseFog = RenderSettings.fogDensity;
            sky = RenderSettings.skybox != null ? new Material(RenderSettings.skybox) : null;   // do not edit the asset
            RenderSettings.skybox = sky;
            if (sky != null && sky.HasProperty("_Exposure")) baseExposure = sky.GetFloat("_Exposure");
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
            RenderSettings.ambientIntensity = Mathf.Lerp(0.25f, baseAmbient, day);
            RenderSettings.fogColor = fogColor.Evaluate(timeOfDay);
            RenderSettings.fogDensity = baseFog * Mathf.Lerp(1.6f, 1f, day);
            if (sky != null && sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", Mathf.Lerp(0.12f, baseExposure, day));
            if (Time.time > nextLights) { nextLights = Time.time + 0.5f; NightLights(Night > 0.3f); }
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
            nightLevel = Mathf.MoveTowards(nightLevel, Night * 0.5f * AudioMix.Music, Time.deltaTime * 0.1f);
            if (nightMusic != null) nightMusic.volume = nightLevel * AudioMix.Duck;
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
                if (islandProbe == null)
                {
                    var go = GameObject.Find("ReflectionProbe");
                    if (go != null && go.TryGetComponent(out islandProbe)) islandProbe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                }
                // the 2.4 km probe is expensive: at most every 30 s, and only when the sky has really changed
                float dt = Mathf.Abs(Mathf.DeltaAngle(timeOfDay * 360f, probeTime * 360f)) / 360f;
                if (islandProbe != null && Time.time >= nextProbe && dt > 0.015f)
                {
                    islandProbe.RenderProbe();
                    probeTime = timeOfDay;
                    nextProbe = Time.time + 30f;
                }
            }
            if (Night > 0.8f && Time.time > nextLine)
            {
                nextLine = Time.time + 240f;
                var rex = FindAnyObjectByType<RexController>();
                if (rex != null) RexVoice.Say(rex.gameObject, "Night", true);
            }
        }

        float envAmbient = -1f, envExposure = -1f, nextLights;
        Color envSun = new Color(-1f, -1f, -1f);
        // realtime point / spot lights are costly (and URP has a per-object light limit): only the nearest burn
        const int MaxNightLights = 12;
        Transform rexT;
        float[] lightDist;

        /// <summary>Night: the MaxNightLights lights nearest to Rex on, all others off. Day: all off.</summary>
        void NightLights(bool night)
        {
            int n = nightLights.Count;
            if (!night || n == 0)
            {
                foreach (var l in nightLights) if (l != null && l.enabled) l.enabled = false;
                return;
            }
            if (rexT == null) { var rc = FindAnyObjectByType<RexController>(); if (rc != null) rexT = rc.transform; }
            Vector3 c = rexT != null ? rexT.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            if (lightDist == null || lightDist.Length != n) lightDist = new float[n];
            for (int i = 0; i < n; i++)
                lightDist[i] = nightLights[i] != null ? (nightLights[i].transform.position - c).sqrMagnitude : float.MaxValue;
            // the distance of the MaxNightLights-th nearest (a few passes over the list, no sorting garbage)
            float limit = float.MaxValue;
            if (n > MaxNightLights)
            {
                float below = -1f;
                for (int k = 0; k < MaxNightLights; k++)
                {
                    float m = float.MaxValue;
                    for (int i = 0; i < n; i++) if (lightDist[i] > below && lightDist[i] < m) m = lightDist[i];
                    below = m;
                }
                limit = below;
            }
            for (int i = 0; i < n; i++)
            {
                var l = nightLights[i];
                if (l == null) continue;
                bool on = lightDist[i] <= limit;
                if (l.enabled != on) l.enabled = on;
            }
        }

        /// <summary>Night multiplier for blob speed (brief: many fast glowing blobs at night).</summary>
        public static float BlobSpeed => 1f + Night * 0.6f;
    }
}
