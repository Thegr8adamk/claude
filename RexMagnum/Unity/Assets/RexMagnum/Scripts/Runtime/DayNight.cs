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
        Color baseSkyTint, baseGround;
        // night floor: away from the lamps the island stays readable (it was near-black at 0.25 ambient / 0.12 sky)
        const float NightAmbient = 0.5f, NightExposure = 0.35f;
        static readonly Color MoonSkyTint = new Color(0.22f, 0.32f, 0.62f), MoonGround = new Color(0.12f, 0.15f, 0.24f);
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
            if (sky != null && sky.HasProperty("_SkyTint")) baseSkyTint = sky.GetColor("_SkyTint");
            if (sky != null && sky.HasProperty("_GroundColor")) baseGround = sky.GetColor("_GroundColor");
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
            RenderSettings.ambientIntensity = Mathf.Lerp(Mathf.Min(NightAmbient, baseAmbient), baseAmbient, day);
            RenderSettings.fogColor = fogColor.Evaluate(timeOfDay);
            RenderSettings.fogDensity = baseFog * Mathf.Lerp(1.6f, 1f, day);
            if (sky != null && sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", Mathf.Lerp(Mathf.Min(NightExposure, baseExposure), baseExposure, day));
            // cool moonlit sky (and so a cool skybox ambient) at night; the day colours stay exactly as built
            if (sky != null && sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", Color.Lerp(MoonSkyTint, baseSkyTint, day));
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
            nightLevel = Mathf.MoveTowards(nightLevel, boss ? 0f : Night * 0.5f * AudioMix.Music, Time.deltaTime * (boss ? 0.5f : 0.1f));
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
