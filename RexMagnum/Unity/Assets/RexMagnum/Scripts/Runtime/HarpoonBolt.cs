using UnityEngine;

namespace RexMagnum
{
    /// <summary>Harpoon: fast steel spear with a rope back to the gun. Pins blobs (to the wall if close).</summary>
    public class HarpoonBolt : MonoBehaviour
    {
        Vector3 velocity;
        float damage, life;
        GameObject owner;
        LineRenderer rope;

        public static void Fire(Vector3 from, Vector3 dir, float damage, GameObject owner)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.name = "Harpoon";
            go.transform.localScale = new Vector3(0.012f, 0.5f, 0.012f);
            go.transform.SetPositionAndRotation(from, Quaternion.FromToRotation(Vector3.up, dir));
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.82f));
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 0.8f);
            go.GetComponent<Renderer>().material = m;
            var h = go.AddComponent<HarpoonBolt>();
            h.velocity = dir * 45f;
            h.damage = damage;
            h.owner = owner;
            h.rope = go.AddComponent<LineRenderer>();
            h.rope.startWidth = h.rope.endWidth = 0.006f;
            h.rope.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            h.rope.material.SetColor("_BaseColor", new Color(0.85f, 0.8f, 0.65f));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            Vector3 step = velocity * dt;
            velocity += Physics.gravity * 0.15f * dt;
            if (owner != null)
            {
                rope.positionCount = 2;
                rope.SetPosition(0, owner.transform.position + Vector3.up * 1.3f + owner.transform.forward * 0.4f);
                rope.SetPosition(1, transform.position);
            }
            if (Physics.Raycast(transform.position, step.normalized, out RaycastHit hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore) &&
                (owner == null || !hit.transform.IsChildOf(owner.transform)))
            {
                transform.position = hit.point;
                var hp = hit.collider.GetComponentInParent<Health>();
                var blob = hit.collider.GetComponentInParent<Blob>();
                if (hp != null && !hp.IsDead)
                {
                    if (blob != null) { blob.deathStyle = DeathStyle.Pinned; blob.Pin(3f); }
                    hp.Damage(damage, hit.point, owner);
                    transform.SetParent(hp.transform, true);
                    if (hp.IsDead) RexVoice.Say(owner, "Harpoon");
                    // pinned to a wall behind it?
                    if (blob != null && Physics.Raycast(hit.point, velocity.normalized, out RaycastHit wall, 2.5f, ~0, QueryTriggerInteraction.Ignore) &&
                        wall.collider.GetComponentInParent<Blob>() == null)
                        blob.transform.position += Vector3.ProjectOnPlane(wall.point - hit.point, Vector3.up) * 0.8f;
                }
                enabled = false;
                Destroy(gameObject, 6f);
                return;
            }
            transform.position += step;
            if (life > 3f) Destroy(gameObject);
        }
    }
}
