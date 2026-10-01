using System.Collections;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>Rex's health reactions: hurt animation + line; at 0 he falls, says a line, and wakes up again
    /// (brief: wakes in room 304 and keeps his weapons; in the test scene: at the spawn point).</summary>
    [RequireComponent(typeof(Health))]
    public class RexLife : MonoBehaviour
    {
        public Transform respawnPoint;
        Health health;
        RexController ctrl;
        Vector3 startPos;
        float startYaw;

        void Awake()
        {
            health = GetComponent<Health>();
            ctrl = GetComponent<RexController>();
            startPos = transform.position;
            startYaw = transform.eulerAngles.y;
            health.Damaged += (a, p, by) =>
            {
                if (!health.IsDead) { ctrl.Hurt(); RexVoice.Say(gameObject, "Hurt"); }
            };
            health.Died += by => StartCoroutine(DieRoutine());
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
            if (ctrl.bodyAnimator) ctrl.bodyAnimator.Play("Locomotion", 0, 0f);
            if (ctrl.armsAnimator) ctrl.armsAnimator.Play("Locomotion", 0, 0f);
            health.ResetHealth();
            ctrl.Revive();
            RexVoice.Say(gameObject, "Respawn", true);
        }
    }
}
