using System.Collections;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>Rex's health reactions: hurt animation + line; at 0 he falls, says a line, and wakes up again
    /// (brief: wakes in room 304 and keeps his weapons; in the test scene: at the spawn point).</summary>
    [RequireComponent(typeof(Health))]
    public class RexLife : MonoBehaviour
    {
        /// <summary>Low health (under 25 %): Rex limps (animator bool Hurt). Checked on damage and twice a second.</summary>
        void UpdateLimp()
        {
            if (ctrl == null || health == null) return;
            bool limp = !health.IsDead && health.Fraction < 0.25f;
            ctrl.Limping = limp;                       // speed cap 2 m/s, no run (RexController)
            if (ctrl.bodyAnimator != null) ctrl.bodyAnimator.SetBool("Hurt", limp);
        }

        void OnEnable() => InvokeRepeating(nameof(UpdateLimp), 1f, 0.5f);
        void OnDisable() => CancelInvoke(nameof(UpdateLimp));

        public Transform respawnPoint;
        Health health;
        RexController ctrl;
        Vector3 startPos;
        float startYaw;
        bool lowSaid;      // the low-health line once per drop below 25 %

        void Update()
        {
            if (lowSaid && health.Fraction > 0.5f) lowSaid = false;
            // slow regen out of the fight: +2 HP/s after 6 s without damage and no living blob within 15 m
            // (food still matters in a fight; a long walk does not leave Rex at 10 HP). Blob check twice a second.
            if (health.IsDead || health.Fraction >= 1f || Time.time - health.LastDamageTime < 6f) return;
            if (Time.time > nextCalmCheck) { nextCalmCheck = Time.time + 0.5f; calm = Blob.LivingNear(transform.position, 15f) == 0; }
            if (calm) health.Heal(2f * Time.deltaTime);       // Heal clamps at max
        }
        float nextCalmCheck;
        bool calm;

        void Awake()
        {
            health = GetComponent<Health>();
            ctrl = GetComponent<RexController>();
            startPos = transform.position;
            startYaw = transform.eulerAngles.y;
            health.Damaged += (a, p, by) =>
            {
                if (health.IsDead || health.Current <= 0f) return;    // the killing blow: no flinch saved for the respawn
                ctrl.Hurt();
                if (health.Fraction < 0.25f && !lowSaid) { lowSaid = true; RexVoice.Say(gameObject, "LowHealth", true); }
                else RexVoice.Say(gameObject, "Hurt");
            };
            health.Died += by => StartCoroutine(DieRoutine());
            health.Damaged += (a, p, by) => UpdateLimp();
        }

        IEnumerator DieRoutine()
        {
            ctrl.Die();
            Progress.Add("deaths");
            RexVoice.Say(gameObject, "Death", true);
            yield return new WaitForSeconds(3.5f);
            Vector3 p = respawnPoint != null ? respawnPoint.position : startPos;
            float y = respawnPoint != null ? respawnPoint.eulerAngles.y : startYaw;
            ctrl.Teleport(p, y);
            if (ctrl.cameraRig != null) ctrl.cameraRig.SetYaw(y);      // the view faces the way Rex wakes up
            if (ctrl.bodyAnimator) ctrl.bodyAnimator.Play("Locomotion", 0, 0f);
            if (ctrl.armsAnimator && ctrl.armsAnimator.gameObject.activeInHierarchy) ctrl.armsAnimator.Play("Locomotion", 0, 0f);
            health.ResetHealth();
            ctrl.Revive();
            RexVoice.Say(gameObject, "Respawn", true);
        }
    }
}
