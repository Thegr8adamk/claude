using UnityEngine;

namespace RexMagnum
{
    /// <summary>Spit slime (or a thrown object) on an arc. Hits Rex: damage + slime; misses: leaves a puddle.</summary>
    public class SlimeProjectile : MonoBehaviour
    {
        Vector3 velocity;
        float damage, life;
        GameObject owner;
        Color color;
        const float Gravity = -12f;

        public static void Launch(Vector3 from, Vector3 to, float damage, GameObject owner, Color color, GameObject model = null)
        {
            GameObject go;
            if (model != null) go = Instantiate(model, from, Random.rotation);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = Vector3.one * 0.22f;
                go.transform.position = from;
                var r = go.GetComponent<Renderer>();
                r.material = SlimeFX.SlimeMaterial(color);
            }
            go.name = "SlimeShot";
            var p = go.AddComponent<SlimeProjectile>();
            p.damage = damage;
            p.owner = owner;
            p.color = color;
            // aim with an arc: flight time from distance
            Vector3 d = to - from;
            float t = Mathf.Clamp(d.magnitude / 12f, 0.35f, 1.4f);
            p.velocity = new Vector3(d.x / t, d.y / t - 0.5f * Gravity * t, d.z / t);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            Vector3 step = velocity * dt;
            velocity.y += Gravity * dt;
            if (Physics.SphereCast(transform.position, 0.12f, step.normalized, out RaycastHit hit, step.magnitude,
                    ~0, QueryTriggerInteraction.Ignore) && (owner == null || !hit.transform.IsChildOf(owner.transform)))
            {
                var h = hit.collider.GetComponentInParent<Health>();
                if (h != null && h.CompareTag("Player"))
                {
                    h.Damage(damage, hit.point, owner);
                    var rex = h.GetComponent<RexController>();
                    if (rex != null) rex.Slimed();
                }
                else if (Vector3.Dot(hit.normal, Vector3.up) > 0.6f)
                    SlimePuddle.Spawn(hit.point, color);
                SlimeFX.Splat(hit.point, hit.normal, color);
                Destroy(gameObject);
                return;
            }
            transform.position += step;
            if (life > 5f) Destroy(gameObject);
        }
    }
}
