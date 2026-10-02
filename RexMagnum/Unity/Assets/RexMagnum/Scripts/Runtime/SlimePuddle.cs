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

        // pooled like SlimeFX's decals (every pop, drip and shot used to make a new cylinder): the oldest is reused
        const int MaxPuddles = 60;
        static readonly System.Collections.Generic.Queue<SlimePuddle> pool = new System.Collections.Generic.Queue<SlimePuddle>();

        public static GameObject Spawn(Vector3 at, Color color, float size = 0.9f)
        {
            while (pool.Count > 0 && pool.Peek() == null) pool.Dequeue();             // destroyed with a scene / an elevator cab
            SlimePuddle p;
            if (pool.Count > 0 && (pool.Count >= MaxPuddles || !pool.Peek().gameObject.activeSelf)) p = pool.Dequeue();
            else
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                g.name = "SlimePuddle";
                Destroy(g.GetComponent<Collider>());
                var trig = g.AddComponent<SphereCollider>();
                trig.isTrigger = true;
                trig.radius = 0.5f;
                p = g.AddComponent<SlimePuddle>();
                p.rend = g.GetComponent<Renderer>();
                p.rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var go = p.gameObject;
            go.SetActive(false);
            go.transform.SetParent(null, false);                // the elevator parents its puddles to the cab
            go.transform.position = at + Vector3.up * 0.01f;
            go.transform.localScale = new Vector3(size, 0.004f, size * Random.Range(0.7f, 1.1f));
            go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var mat = SlimeFX.SharedSlime(color);               // one material per colour (batches); the fade is a property block
            p.rend.sharedMaterial = mat;
            p.c0 = mat.GetColor("_BaseColor");
            p.born = Time.time;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            p.rend.GetPropertyBlock(mpb);                       // a reused puddle may still carry its old fade
            mpb.SetColor("_BaseColor", p.c0);
            p.rend.SetPropertyBlock(mpb);
            go.SetActive(true);
            pool.Enqueue(p);
            return go;
        }

        void Update()
        {
            float age = Time.time - born;
            if (age > lifetime) { gameObject.SetActive(false); return; }     // back to the pool
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
