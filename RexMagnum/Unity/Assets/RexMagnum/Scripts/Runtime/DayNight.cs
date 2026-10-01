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
        float nextLine;
        BlobSpawner spawner;
        int baseMax;
        AudioSource nightMusic;

        void Start()
        {
            if (sun == null) sun = RenderSettings.sun;
            baseSun = sun != null ? sun.intensity : 2f;
            baseAmbient = RenderSettings.ambientIntensity;
            baseFog = RenderSettings.fogDensity;
            sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_Exposure")) baseExposure = sky.GetFloat("_Exposure");
            spawner = FindFirstObjectByType<BlobSpawner>();
            if (spawner != null) baseMax = spawner.maxAlive;
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
                float pitch = Mathf.Lerp(-10f, 75f, Mathf.Max(0f, elev));
                sun.transform.rotation = Quaternion.Euler(elev > 0 ? pitch : 30f, sunAzimuth + (timeOfDay - 0.5f) * 180f, 0f);
                sun.color = sunColor.Evaluate(timeOfDay);
                sun.intensity = Mathf.Lerp(0.12f, baseSun, day);                 // night: moonlight
                sun.shadowStrength = Mathf.Lerp(0.5f, 0.9f, day);
            }
            RenderSettings.ambientIntensity = Mathf.Lerp(0.25f, baseAmbient, day);
            RenderSettings.fogColor = fogColor.Evaluate(timeOfDay);
            RenderSettings.fogDensity = baseFog * Mathf.Lerp(1.6f, 1f, day);
            if (sky != null && sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", Mathf.Lerp(0.12f, baseExposure, day));
            foreach (var l in nightLights) if (l != null) l.enabled = Night > 0.3f;
            foreach (var b in Blob.All) if (b != null) b.SetNightGlow(Night);
            if (spawner != null) spawner.maxAlive = Mathf.RoundToInt(baseMax * Mathf.Lerp(1f, 2.5f, Night));
            if (nightMusic != null) nightMusic.volume = Mathf.MoveTowards(nightMusic.volume, Night * 0.5f, Time.deltaTime * 0.1f);
            if (Night > 0.8f && Time.time > nextLine)
            {
                nextLine = Time.time + 240f;
                var rex = FindFirstObjectByType<RexController>();
                if (rex != null) RexVoice.Say(rex.gameObject, "Night", true);
            }
        }

        /// <summary>Night multiplier for blob speed (brief: many fast glowing blobs at night).</summary>
        public static float BlobSpeed => 1f + Night * 0.6f;
    }
}
