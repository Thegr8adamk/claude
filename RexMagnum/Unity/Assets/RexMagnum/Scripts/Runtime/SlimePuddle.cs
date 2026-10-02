using UnityEngine;

namespace RexMagnum
{
    /// <summary>Slime puddle (brief: Rex slips on slime puddles - comedy fall). Fades out after a while.</summary>
    public class SlimePuddle : MonoBehaviour
    {
        public float slipChance = 0.25f;    // when Rex runs over it (RexController keeps 4 s between slips)
        public float lifetime = 25f;
        float born;
        Renderer rend;
        Color c0;
        static MaterialPropertyBlock mpb;

        public static GameObject Spawn(Vector3 at, Color color, float size = 0.9f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "SlimePuddle";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = at + Vector3.up * 0.01f;
            go.transform.localScale = new Vector3(size, 0.004f, size * Random.Range(0.7f, 1.1f));
            go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = SlimeFX.SharedSlime(color);      // one material per colour (batches); the fade is a property block
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var trig = go.AddComponent<SphereCollider>();
            trig.isTrigger = true;
            trig.radius = 0.5f;
            go.AddComponent<SlimePuddle>();
            return go;
        }

        void Start()
        {
            born = Time.time;
            rend = GetComponent<Renderer>();
            c0 = rend.sharedMaterial.GetColor("_BaseColor");
            if (mpb == null) mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            float age = Time.time - born;
            if (age > lifetime) { Destroy(gameObject); return; }
            if (age < lifetime - 3f) return;                    // full colour until the last 3 s: no write
            float a = 1f - Mathf.Clamp01((age - lifetime + 3f) / 3f);
            rend.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", new Color(c0.r, c0.g, c0.b, c0.a * a));
            rend.SetPropertyBlock(mpb);
        }

        void OnTriggerEnter(Collider other)
        {
            var rex = other.GetComponent<RexController>();
            if (rex != null && rex.IsRunning && Random.value < slipChance) rex.Slip();
        }
    }
}
