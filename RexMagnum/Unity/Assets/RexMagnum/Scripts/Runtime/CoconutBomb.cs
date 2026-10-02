using UnityEngine;

namespace RexMagnum
{
    /// <summary>Exploding coconut: 3-second fuse, bounces, then boom (area damage + slime rain).
    /// The kill cam rides the coconut when it kills (brief).</summary>
    public class CoconutBomb : MonoBehaviour
    {
        float fuse = 3f, damage;
        GameObject owner;
        Light spark;

        public static void Throw(GameObject prefab, Vector3 from, Vector3 velocity, float damage, GameObject owner)
        {
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, from, Random.rotation);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = Vector3.one * 0.13f;
                go.transform.position = from;
            }
            go.name = "Coconut (lit)";
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var sc = go.AddComponent<SphereCollider>();
            sc.radius = 0.065f / Mathf.Max(0.001f, go.transform.lossyScale.x);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.5f;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            var mat = new PhysicsMaterial { bounciness = 0.35f, dynamicFriction = 0.6f };
            sc.material = mat;
            // thrown from inside Rex's capsule reach: it must not bounce off Rex himself
            if (owner != null && owner.TryGetComponent(out CharacterController occ)) Physics.IgnoreCollision(sc, occ);
            var b = go.AddComponent<CoconutBomb>();
            b.damage = damage;
            b.owner = owner;
            b.spark = new GameObject("Spark").AddComponent<Light>();
            b.spark.transform.SetParent(go.transform, false);
            b.spark.type = LightType.Point;
            b.spark.color = new Color(1f, 0.6f, 0.2f);
            b.spark.range = 2f;
            KillCam.WatchCoconut(go.transform);
        }

        void Update()
        {
            fuse -= Time.deltaTime;
            if (spark) spark.intensity = 1.5f + Mathf.Sin(Time.time * 40f) * 1f;
            if (fuse > 0f) return;
            Vector3 p = transform.position;
            float radius = 4f;
            bool killed = false;
            foreach (var col in Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var h = col.GetComponentInParent<Health>();
                if (h == null || h.IsDead) continue;
                float k = 1f - Vector3.Distance(p, col.ClosestPoint(p)) / radius;
                var blob = h.GetComponent<Blob>();
                if (blob != null) blob.deathStyle = DeathStyle.Blast;
                h.Damage(damage * Mathf.Clamp01(k + 0.2f) * (h.CompareTag("Player") ? 0.4f : 1f), p, owner);
                if (h.IsDead && blob != null) killed = true;
            }
            for (int i = 0; i < 6; i++)                    // slime rain
                SlimePuddle.Spawn(p + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f)), new Color(0.3f, 0.8f, 0.2f), 0.6f);
            SlimeFX.Splat(p, Vector3.up, new Color(1f, 0.6f, 0.2f), 30);
            var flash = new GameObject("Boom").AddComponent<Light>();
            flash.transform.position = p;
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.6f, 0.25f);
            flash.range = 10f;
            flash.intensity = 8f;
            Destroy(flash.gameObject, 0.15f);
            if (killed) RexVoice.Say(owner, "Coconut");
            KillCam.CoconutDone(killed);
            Destroy(gameObject);
        }
    }
}
