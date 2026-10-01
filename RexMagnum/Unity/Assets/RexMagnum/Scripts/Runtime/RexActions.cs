using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Rex's action buttons (brief section 5 and 8): punch (tap = jab, hold = heavy),
    /// taunt, photo, hands down/up. Sends the same triggers to the body and the
    /// first-person arms, and sets the face blend shapes.
    /// </summary>
    public class RexActions : MonoBehaviour
    {
        public RexController controller;
        public RexFace face;
        public RexWeapons weapons;
        public float holdTime = 0.3f;

        static readonly int Jab = Animator.StringToHash("Jab");
        static readonly int Heavy = Animator.StringToHash("Heavy");
        static readonly int Taunt = Animator.StringToHash("Taunt");
        static readonly int Photo = Animator.StringToHash("Photo");
        static readonly int HandsDownHash = Animator.StringToHash("HandsDown");

        float pressTime = -1f, busyUntil, faceAimUntil;
        public float jabDamage = 12f, heavyDamage = 30f;

        public string ActionWord => weapons != null ? weapons.ActionWord : controller.HandsDown ? "Talk" : "Punch";
        public Color ActionColor => weapons != null ? weapons.ActionColor : new Color(1f, 0.6f, 0.2f);

        void Update()
        {
            if (!controller.InputEnabled) return;
            bool busy = Time.time < busyUntil;

            if (RMInput.Hands)
            {
                controller.HandsDown = !controller.HandsDown;
                if (controller.bodyAnimator) controller.bodyAnimator.SetBool(HandsDownHash, controller.HandsDown);
                if (controller.armsAnimator) controller.armsAnimator.SetBool(HandsDownHash, controller.HandsDown);
            }

            if (!controller.HandsDown && weapons == null)
            {
                if (RMInput.ActionDown) pressTime = Time.time;
                if (pressTime >= 0f && !busy)
                {
                    if (RMInput.ActionUp && Time.time - pressTime < holdTime)
                    {
                        Do(Jab, 0.45f);
                        StartCoroutine(HitAfter(0.18f, jabDamage, 1.4f, false));
                        pressTime = -1f;
                    }
                    else if (RMInput.ActionHeld && Time.time - pressTime >= holdTime)
                    {
                        Do(Heavy, 0.9f);
                        StartCoroutine(HitAfter(0.45f, heavyDamage, 1.6f, true));
                        pressTime = -1f;
                    }
                }
                if (RMInput.ActionUp) pressTime = -1f;
            }

            if (RMInput.Taunt && !busy)
            {
                Do(Taunt, 1.6f);
                if (face) face.Expression("SmugGrin", 1.6f);
                RexVoice.Say(gameObject, "Taunt");
                // Blobs that see it get angry and answer with a rude tentacle
                foreach (var b in FindObjectsByType<Blob>(FindObjectsSortMode.None))
                    if (Vector3.Distance(b.transform.position, transform.position) < 30f) b.OnTaunted();
            }
            if (RMInput.Photo && !busy)
            {
                Do(Photo, 1.9f);
                if (face) face.Expression("Smile", 1.9f);
                Blob nearest = null;
                float best = 6f;
                foreach (var b in FindObjectsByType<Blob>(FindObjectsSortMode.None))
                {
                    float d = Vector3.Distance(b.transform.position, transform.position);
                    if (d < best) { best = d; nearest = b; }
                }
                if (nearest != null) nearest.OnPhoto();
                RexVoice.Say(gameObject, nearest != null ? "Photo" : "PhotoAlone");
            }
            controller.FaceAim = Time.time < faceAimUntil;
        }

        System.Collections.IEnumerator HitAfter(float delay, float damage, float reach, bool heavy)
        {
            yield return new WaitForSeconds(delay);
            Vector3 c = transform.position + Vector3.up * 1.2f + transform.forward * (reach * 0.6f);
            foreach (var col in Physics.OverlapSphere(c, reach * 0.55f, ~0, QueryTriggerInteraction.Ignore))
            {
                var h = col.GetComponentInParent<Health>();
                if (h == null || h.gameObject == gameObject) continue;
                bool wasAlive = !h.IsDead;
                h.Damage(damage, col.ClosestPoint(c), gameObject);
                if (wasAlive && h.IsDead) RexVoice.Say(gameObject, "Fists");
                break;
            }
        }

        void Do(int trigger, float seconds)
        {
            controller.SetTrigger(trigger);
            busyUntil = Time.time + seconds * 0.8f;
            faceAimUntil = Time.time + seconds;
        }
    }
}
