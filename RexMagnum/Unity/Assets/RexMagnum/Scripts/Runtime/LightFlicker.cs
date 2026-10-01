using UnityEngine;

namespace RexMagnum
{
    /// <summary>TV glow: the light changes colour and strength like a film playing.</summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        public float baseIntensity = 1.2f;
        public Color a = new Color(1f, 0.55f, 0.25f), b = new Color(0.6f, 0.75f, 1f);
        Light l;
        float seed;
        void Awake() { l = GetComponent<Light>(); seed = Random.value * 100f; }
        void Update()
        {
            float t = Time.time * 1.7f + seed;
            float n = Mathf.PerlinNoise(t, 0.3f);
            l.intensity = baseIntensity * (0.55f + 0.7f * n);
            l.color = Color.Lerp(a, b, Mathf.PerlinNoise(0.7f, t * 0.4f));
        }
    }
}
