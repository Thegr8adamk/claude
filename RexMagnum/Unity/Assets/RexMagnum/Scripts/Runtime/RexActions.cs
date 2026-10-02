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
        static readonly int Comb = Animator.StringToHash("Comb");
        static readonly int HandsDownHash = Animator.StringToHash("HandsDown");

        float pressTime = -1f, busyUntil, faceAimUntil;
        public float jabDamage = 12f, heavyDamage = 30f;

        public string ActionWord => weapons != null ? weapons.ActionWord : controller.HandsDown ? "Talk" : "Punch";
        public Color ActionColor => weapons != null ? weapons.ActionColor : new Color(1f, 0.6f, 0.2f);

        void Update()
        {
            if (!controller.InputEnabled) return;
            if (controller.Locked) { controller.FaceAim = false; return; }   // dead or slipped: no punch, taunt, photo
            bool busy = Time.time < busyUntil;

            // hold mode: the hands follow the held state (an event toggle could drift and stick the hands inverted)
            bool handsChange = RMInput.HandsHold ? RMInput.HandsHoldDown != controller.HandsDown : RMInput.Hands;
            if (handsChange)
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
                        Sfx.Play("swing", transform.position, 0.45f, 0.55f);     // charge cue (as RexWeapons)
                        StartCoroutine(HitAfter(0.45f, heavyDamage, 1.6f, true));
                        pressTime = -1f;
                    }
                }
                if (RMInput.ActionUp) pressTime = -1f;
            }

            if (RMInput.Taunt && !busy && PlanarMirror.Near(transform.position))
            {
                // brief section 8: at a mirror Rex combs his thin side hair over the bald spot (PlanarMirror says the line)
                Do(Comb, 2.4f);
                if (face) face.Expression("SmugGrin", 2.4f);
            }
            else if (RMInput.Taunt && !busy)
            {
                Do(Taunt, 1.6f);
                if (face) face.Expression("SmugGrin", 1.6f);
                RexVoice.Say(gameObject, "Taunt");
                Progress.Add("taunts");
                // Blobs that see it get angry and answer with a rude tentacle
                bool answered = false;
                foreach (var b in FindObjectsByType<Blob>(FindObjectsSortMode.None))
                    if (Vector3.Distance(b.transform.position, transform.position) < 30f) { if (b.Disguised) b.Reveal(); else b.OnTaunted(); answered = true; }
                if (answered && Random.value < 0.5f) StartCoroutine(SayLater("TauntAnswer", 2.8f));   // after the rude tentacle
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
                // brief section 8: the nearest blob OR guest; a guest closer than any blob is the one in the photo
                Guest guest = null;
                foreach (var g in FindObjectsByType<Guest>(FindObjectsSortMode.None))
                {
                    if (g.LockedIn) continue;                 // behind a locked door (hidden) for the night
                    float d = Vector3.Distance(g.transform.position, transform.position);
                    if (d < best) { best = d; guest = g; nearest = null; }
                }
                if (nearest != null) nearest.OnPhoto();
                if (guest != null) Progress.Add("guest_photos");
                RexVoice.Say(gameObject, guest != null ? "PhotoGuest" : nearest != null ? "Photo" : "PhotoAlone");
                // a stunned blob or a guest gets pulled in; an un-stunned blob stays (and fights back in the shot)
                Transform pulled = guest != null ? guest.transform
                                 : nearest != null && nearest.IsStunned && nearest.GetComponent<KingGlorpBoss>() == null ? nearest.transform : null;
                PhotoAlbum.Take(this, guest != null ? "Guest" : nearest == null ? "" :
                                      nearest.GetComponent<KingGlorpBoss>() != null ? "KingGlorp" : nearest.kind.ToString(),
                                transform, pulled);
            }
            controller.FaceAim = Time.time < faceAimUntil;
        }

        System.Collections.IEnumerator SayLater(string group, float delay)
        {
            yield return new WaitForSeconds(delay);
            RexVoice.Say(gameObject, group, true);
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
                if (wasAlive && h.GetComponent<Blob>() != null) RexWeapons.HitConfirm(WeaponId.Fists, c);
                if (wasAlive && h.IsDead)
                {
                    RexVoice.Say(gameObject, "Fists");
                    // brief: fist kill cam (X-ray, fist in the jelly) on a heavy punch or the last blob of a group
                    if (heavy || (h.GetComponent<Blob>() != null && Blob.LivingNear(transform.position, 30f) == 0))
                        KillCam.Show(h.transform, transform.position, KillCam.Style.Fist);
                }
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
