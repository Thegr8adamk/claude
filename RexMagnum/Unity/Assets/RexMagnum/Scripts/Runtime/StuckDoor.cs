using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>Room 304's stuck door (brief: "punch stuck door"). It takes a few punches or hits; then it swings
    /// open on its hinge (this transform) with a bang, and Rex says a line. The door collider stays on the leaf.</summary>
    [RequireComponent(typeof(Health))]
    public class StuckDoor : MonoBehaviour
    {
        public float openAngle = -100f;
        public float openTime = 0.35f;
        public AudioClip bang;
        Health health;
        bool open;
        /// <summary>True once the door has been punched open (the tutorial waits for it).</summary>
        public static bool AnyOpen { get; private set; }

        void Awake()
        {
            health = GetComponent<Health>();
            AnyOpen = false;            // static: do not keep last play session's open door (domain reload off)
            health.Damaged += (a, p, by) => { if (!open) StartCoroutine(Shake()); };
            health.Died += by => { if (!open) StartCoroutine(Open(by)); };
        }

        /// <summary>A returning player: every stuck door stands open at once (no bang, no line, no tutorial wait).</summary>
        public static void OpenAll()
        {
            foreach (var d in RMFind.All<StuckDoor>())
            {
                if (d.open) continue;
                d.open = true;
                d.transform.localRotation = d.transform.localRotation * Quaternion.Euler(0f, d.openAngle, 0f);
                // an open door is out of the fight: its Health is spent (no damage, no bottle hit counted on it;
                // open is set first, so no bang / line) and its carving obstacle goes off (agents walk through)
                var dh = d.GetComponent<Health>();
                if (dh != null && !dh.IsDead) dh.Damage(dh.maxHealth + 1f, d.transform.position);
                foreach (var ob in d.GetComponentsInChildren<NavMeshObstacle>()) ob.enabled = false;
            }
            AnyOpen = true;
        }

        IEnumerator Shake()
        {
            var r = transform.localRotation;
            for (float t = 0; t < 0.15f; t += Time.deltaTime)
            {
                transform.localRotation = r * Quaternion.Euler(0f, Mathf.Sin(t * 120f) * 1.5f, 0f);
                yield return null;
            }
            transform.localRotation = r;
        }

        IEnumerator Open(GameObject by)
        {
            open = true;
            AnyOpen = true;
            var src = GetComponent<AudioSource>();
            if (src != null && bang != null) src.PlayOneShot(bang, AudioMix.Effects);     // effects volume setting
            else Sfx.Play("door_bang", transform.position);
            if (by != null) RexVoice.Say(by, "Door", true);
            // like OpenAll: the carving obstacle goes off, agents walk through the open door
            foreach (var ob in GetComponentsInChildren<NavMeshObstacle>()) ob.enabled = false;
            var a = transform.localRotation;
            var b = a * Quaternion.Euler(0f, openAngle, 0f);
            for (float t = 0; t < 1f; t += Time.deltaTime / openTime)
            {
                float e = 1f - Mathf.Pow(1f - t, 3f);
                transform.localRotation = Quaternion.SlerpUnclamped(a, b, e + Mathf.Sin(t * Mathf.PI) * 0.08f);
                yield return null;
            }
            transform.localRotation = b;
        }
    }
}
