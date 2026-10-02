using UnityEngine;

namespace RexMagnum
{
    /// <summary>Slime effects made in code: jelly material, splats, pop burst with decals.</summary>
    public static class SlimeFX
    {
        public static Material SlimeMaterial(Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.75f));
            m.SetFloat("_Smoothness", 0.95f);
            return m;
        }

        static readonly System.Collections.Generic.Dictionary<Color32, Material> shared =
            new System.Collections.Generic.Dictionary<Color32, Material>();

        /// <summary>One shared slime material per colour (splats, decals, shots, puddles: puddles fade with a
        /// MaterialPropertyBlock, so they share too). SlimeMaterial makes a new one (only for per-object changes).</summary>
        public static Material SharedSlime(Color color)
        {
            Color32 key = color;
            if (!shared.TryGetValue(key, out var m) || m == null) shared[key] = m = SlimeMaterial(color);
            return m;
        }

        /// <summary>A pooled slime drop: switches itself off when its time is up (SlimeFX reuses it).</summary>
        sealed class SlimeDrop : MonoBehaviour
        {
            public float until;
            public Rigidbody rb;
            public Renderer rend;
            void Update() { if (Time.time >= until) gameObject.SetActive(false); }
        }

        // drops all live 1.2 s, so the queue is oldest first: reuse the front when it is off or the pool is full
        const int MaxDrops = 120;
        const float DropLife = 1.2f;
        static readonly System.Collections.Generic.Queue<SlimeDrop> drops = new System.Collections.Generic.Queue<SlimeDrop>();

        static SlimeDrop GetDrop()
        {
            while (drops.Count > 0 && drops.Peek() == null) drops.Dequeue();         // destroyed with a scene
            if (drops.Count > 0 && (drops.Count >= MaxDrops || !drops.Peek().gameObject.activeSelf)) return drops.Dequeue();
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = "SlimeDrop";
            Object.Destroy(g.GetComponent<Collider>());
            var d = g.AddComponent<SlimeDrop>();
            d.rb = g.AddComponent<Rigidbody>();
            d.rend = g.GetComponent<Renderer>();
            d.rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return d;
        }

        /// <summary>A pooled wall / floor slime decal: switches itself off after DecalLife (SlimeFX reuses it).</summary>
        sealed class SlimeDecal : MonoBehaviour
        {
            public float until;
            public Renderer rend;
            void Update() { if (Time.time >= until) gameObject.SetActive(false); }
        }

        // same scheme as the drops: every pop used to make 10 new quads; now the oldest is reused
        const int MaxDecals = 80;
        const float DecalLife = 30f;
        static readonly System.Collections.Generic.Queue<SlimeDecal> decals = new System.Collections.Generic.Queue<SlimeDecal>();

        static SlimeDecal GetDecal()
        {
            while (decals.Count > 0 && decals.Peek() == null) decals.Dequeue();     // destroyed with a scene
            if (decals.Count > 0 && (decals.Count >= MaxDecals || !decals.Peek().gameObject.activeSelf)) return decals.Dequeue();
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "SlimeDecal";
            Object.Destroy(q.GetComponent<Collider>());
            var d = q.AddComponent<SlimeDecal>();
            d.rend = q.GetComponent<Renderer>();
            d.rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return d;
        }

        public static void Splat(Vector3 at, Vector3 normal, Color color, int count = 8)
        {
            var mat = SharedSlime(color);
            for (int i = 0; i < count; i++)
            {
                var d = GetDrop();
                var g = d.gameObject;
                g.SetActive(false);
                g.transform.position = at + normal * 0.05f;
                g.transform.localScale = Vector3.one * Random.Range(0.04f, 0.1f);
                d.rend.sharedMaterial = mat;
                g.SetActive(true);
                d.rb.position = g.transform.position;
                d.rb.angularVelocity = Vector3.zero;
                d.rb.linearVelocity = (normal + Random.insideUnitSphere * 0.8f) * Random.Range(2f, 4f);
                d.until = Time.time + DropLife;
                drops.Enqueue(d);
            }
        }

        /// <summary>Pop: slime burst + puddle + slime decals on walls and floor around.</summary>
        public static void Burst(Vector3 at, Color color, float size)
        {
            Splat(at, Vector3.up, color, 18);
            SlimePuddle.Spawn(new Vector3(at.x, at.y - size * 0.5f, at.z), color, size * 1.2f);
            for (int i = 0; i < 10; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (StaticHit(at, dir, size * 4f, out RaycastHit hit))
                {
                    var d = GetDecal();
                    var q = d.gameObject;
                    q.transform.SetPositionAndRotation(hit.point + hit.normal * 0.01f,
                        Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, Random.Range(0, 360f)));
                    q.transform.localScale = Vector3.one * Random.Range(0.3f, 0.7f) * size;
                    d.rend.sharedMaterial = SharedSlime(color);
                    d.until = Time.time + DecalLife;
                    q.SetActive(true);
                    decals.Enqueue(d);
                }
            }
        }

        /// <summary>A ray that passes through characters (Rex, blobs, guests, props with a rigidbody):
        /// a decal stuck to something that moves away would float in the air.</summary>
        static bool StaticHit(Vector3 from, Vector3 dir, float range, out RaycastHit hit)
        {
            for (int i = 0; i < 4; i++)                                    // at most a few bodies in the way
            {
                if (!Physics.Raycast(from, dir, out hit, range, ~0, QueryTriggerInteraction.Ignore)) return false;
                var c = hit.collider;
                bool character = c is CharacterController || c.attachedRigidbody != null ||
                                 c.GetComponentInParent<Health>() != null || c.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null;
                // the elevator cab moves too: a decal on it would hang in the shaft (no decal there, the ray stops)
                if (c.GetComponentInParent<Elevator>() != null) return false;
                if (!character) return true;
                from = hit.point + dir * 0.05f;
                range -= hit.distance + 0.05f;
                if (range <= 0f) return false;
            }
            hit = default;
            return false;
        }
    }
}
