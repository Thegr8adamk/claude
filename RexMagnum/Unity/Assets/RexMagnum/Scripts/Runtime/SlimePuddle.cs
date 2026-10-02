using UnityEngine;

namespace RexMagnum
{
    /// <summary>Slime puddle (brief: Rex slips on slime puddles - comedy fall). Fades out after a while.</summary>
    public class SlimePuddle : MonoBehaviour
    {
        public float slipChance = 0.6f;     // when Rex runs over it
        public float lifetime = 25f;
        float born;
        Material mat;
        Color c0;

        public static GameObject Spawn(Vector3 at, Color color, float size = 0.9f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "SlimePuddle";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = at + Vector3.up * 0.01f;
            go.transform.localScale = new Vector3(size, 0.004f, size * Random.Range(0.7f, 1.1f));
            go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var r = go.GetComponent<Renderer>();
            r.material = SlimeFX.SlimeMaterial(color);
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
            mat = GetComponent<Renderer>().material;
            c0 = mat.GetColor("_BaseColor");
        }

        void Update()
        {
            float a = 1f - Mathf.Clamp01((Time.time - born - lifetime + 3f) / 3f);
            mat.SetColor("_BaseColor", new Color(c0.r, c0.g, c0.b, c0.a * a));
            if (Time.time - born > lifetime) Destroy(gameObject);
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }     // its own fading material

        void OnTriggerEnter(Collider other)
        {
            var rex = other.GetComponent<RexController>();
            if (rex != null && rex.IsRunning && Random.value < slipChance) rex.Slip();
        }
    }
}
