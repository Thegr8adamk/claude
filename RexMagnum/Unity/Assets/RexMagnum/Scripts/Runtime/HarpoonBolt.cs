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
        Blob hooked;            // a living blob on the line: Rex can pull it in (Use / F / X)
        float hookedUntil, pullUntil;

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
            if (hooked != null || hookedUntil > 0f) { Reel(dt); return; }
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
                    if (hp.IsDead)
                    {
                        RexVoice.Say(owner, "Harpoon");
                        if (blob != null && owner != null) KillCam.Show(blob.transform, owner.transform.position + Vector3.up * 1.3f, KillCam.Style.Bullet);
                    }
                    // pinned to a wall behind it (brief: blob pinned to the wall): slide it back onto the wall
                    if (blob != null && Physics.Raycast(hit.point, velocity.normalized, out RaycastHit wall, 3.5f, ~0, QueryTriggerInteraction.Ignore) &&
                        wall.collider.GetComponentInParent<Blob>() == null)
                    {
                        var ag = blob.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (ag != null) ag.enabled = false;            // stuck to the wall while pinned
                        blob.transform.position += Vector3.ProjectOnPlane(wall.point - hit.point, Vector3.up) - velocity.normalized * 0.35f;
                        if (ag != null) StartCoroutineOn(blob, ag, 3f);
                    }
                    else if (blob != null && !hp.IsDead)
                    {
                        hooked = blob;                                   // on the line: pull it in
                        hookedUntil = Time.time + 4f;
                        RexVoice.Subtitle?.Invoke("F / X: pull the rope");
                        return;
                    }
                }
                enabled = false;
                Destroy(gameObject, 6f);
                return;
            }
            transform.position += step;
            if (life > 3f) Destroy(gameObject);
        }
        /// <summary>Brief: pull the rope to drag the blob. While a living blob is on the line (4 s), Use pulls it
        /// towards Rex for half a second at a time.</summary>
        void Reel(float dt)
        {
            if (owner != null)
            {
                rope.positionCount = 2;
                rope.SetPosition(0, owner.transform.position + Vector3.up * 1.3f + owner.transform.forward * 0.4f);
                rope.SetPosition(1, transform.position);
            }
            if (hooked == null || hooked.IsDead || Time.time > hookedUntil || owner == null)
            {
                hooked = null;
                hookedUntil = 0f;
                enabled = false;
                Destroy(gameObject, 3f);
                return;
            }
            if (RMInput.Use) { pullUntil = Time.time + 0.5f; hookedUntil = Time.time + 2.5f; }
            if (Time.time < pullUntil)
            {
                Vector3 to = owner.transform.position - hooked.transform.position;
                to.y = 0f;
                if (to.magnitude > 1.6f)
                {
                    Vector3 step = to.normalized * 7f * dt;
                    var ag = hooked.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (ag != null && ag.enabled && ag.isOnNavMesh) ag.Move(step); else hooked.transform.position += step;
                }
            }
        }

        static void StartCoroutineOn(Blob b, UnityEngine.AI.NavMeshAgent ag, float after) => b.StartCoroutine(ReEnable(ag, after));

        static System.Collections.IEnumerator ReEnable(UnityEngine.AI.NavMeshAgent ag, float after)
        {
            yield return new WaitForSeconds(after);
            if (ag != null) ag.enabled = true;
        }
    }
}
