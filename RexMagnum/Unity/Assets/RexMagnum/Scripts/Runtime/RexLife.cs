using System.Collections;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>Rex's health reactions: hurt animation + line; at 0 he falls, says a line, and wakes up again
    /// (brief: wakes in room 304 and keeps his weapons; in the test scene: at the spawn point). Far zones (cave, ruins,
    /// ship) and the boss fight set a checkpoint: after a death there Rex wakes near it, not a 2-minute walk away.</summary>
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

        // ---- checkpoint (this session only): where Rex stood (on the ground, not in water) when he entered the cave,
        // the ruins or the ship, or when the King Glorp fight started
        static readonly string[] CheckpointZones = { "cave", "ruins", "ship" };
        bool hasCheckpoint;
        Vector3 checkpointPos;
        float checkpointYaw, nextCheckpointCheck;
        string checkpointKey;

        void UpdateCheckpoint()
        {
            if (health.IsDead || ctrl == null || GameFlow.TutorialActive || !ctrl.InputEnabled) return;
            if (!ctrl.Grounded || ctrl.IsSwimming || ctrl.WaterDepth > 0f || ctrl.Locked) return;   // not mid-jump, in water or on the cart
            string key = KingGlorpBoss.Fighting ? "boss" : null;
            if (key == null)
                foreach (var z in GameGuide.Zones)
                    if (System.Array.IndexOf(CheckpointZones, z.id) >= 0 && GameGuide.InZone(z, transform.position)) { key = z.id; break; }
            if (key == null) { checkpointKey = null; return; }       // left the zone: the next entry sets a new one
            if (key == checkpointKey) return;                         // set once per entry, not every step
            // walking along a zone edge (out and in again) near the checkpoint: keep it, no new notice
            if (hasCheckpoint && key != "boss" && (transform.position - checkpointPos).sqrMagnitude < 900f) { checkpointKey = key; return; }
            checkpointKey = key;
            hasCheckpoint = true;
            checkpointPos = transform.position;
            checkpointYaw = transform.eulerAngles.y;
            RexTestHUD.Notice("CHECKPOINT", 1.2f);
        }

        void Update()
        {
            if (Time.time > nextCheckpointCheck) { nextCheckpointCheck = Time.time + 0.5f; UpdateCheckpoint(); }
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
            // the checkpoint only when Rex died nearer to it than to his room (a death back at the resort wakes in 304)
            if (hasCheckpoint && (transform.position - checkpointPos).sqrMagnitude < (transform.position - p).sqrMagnitude)
            { p = checkpointPos; y = checkpointYaw; }
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
