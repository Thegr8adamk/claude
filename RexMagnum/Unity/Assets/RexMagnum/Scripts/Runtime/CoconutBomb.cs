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
        static PhysicsMaterial bounce;      // shared by every coconut (a new one per throw was never freed)

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
            rb.interpolation = RigidbodyInterpolation.Interpolate;     // smooth in the slow-motion ride cam
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            if (bounce == null) bounce = new PhysicsMaterial { bounciness = 0.35f, dynamicFriction = 0.6f };
            sc.sharedMaterial = bounce;
            // thrown from inside Rex's capsule reach: it must not bounce off Rex himself
            if (owner != null && owner.TryGetComponent(out CharacterController occ)) Physics.IgnoreCollision(sc, occ);
            var b = go.AddComponent<CoconutBomb>();
            b.fuse = RexWeapons.CoconutFuse;          // COCONUT POUCH (cave siege won): shorter fuse
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
            int kills = 0;
            // each Health once (a blob / Rex with several colliders took the blast per collider), and only with a line
            // from the blast: a wall or door between blocks it (a Health in the way, e.g. another blob, does not)
            var struck = new System.Collections.Generic.HashSet<Health>();
            Vector3 eye = p + Vector3.up * 0.1f;           // off the floor the coconut lies on
            foreach (var col in Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var h = col.GetComponentInParent<Health>();
                if (h == null || h.IsDead || struck.Contains(h)) continue;
                if (!BlastClear(eye, col.ClosestPoint(p)) && !BlastClear(eye, col.bounds.center)) continue;
                struck.Add(h);
                float k = 1f - Vector3.Distance(p, col.ClosestPoint(p)) / radius;
                var blob = h.GetComponent<Blob>();
                if (blob != null) blob.deathStyle = DeathStyle.Blast;
                if (blob != null) blob.creditWeapon = WeaponId.Coconut;
                h.Damage(damage * Mathf.Clamp01(k + 0.2f) * (h.CompareTag("Player") ? 0.4f : 1f), p, owner);
                if (blob != null) blob.creditWeapon = null;
                if (h.IsDead && blob != null) { killed = true; kills++; }
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
            Sfx.Play("coconut_boom", p);
            // close to Rex: the controller kicks (and the camera, a little)
            if (owner != null && Vector3.Distance(owner.transform.position, p) < 10f)
            {
                RMInput.Rumble(0.8f, 1f, 0.3f);
                var rig = owner.GetComponent<RexController>();
                if (rig != null && rig.cameraRig != null) rig.cameraRig.Shake(0.25f);
            }
            if (killed) RexVoice.Say(owner, "Coconut");
            if (kills >= 3) Progress.Add("coconut_multi");
            KillCam.CoconutDone(killed);
            Destroy(gameObject);
        }

        static bool BlastClear(Vector3 from, Vector3 to) =>
            !Physics.Linecast(from, to, out RaycastHit block, ~0, QueryTriggerInteraction.Ignore) ||
            block.collider.GetComponentInParent<Health>() != null || block.collider.GetComponent<CoconutBomb>() != null;
    }
}
