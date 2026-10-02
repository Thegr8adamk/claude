using UnityEngine;

namespace RexMagnum
{
    /// <summary>Rex pushes small physics props he walks into (empty bottles, pizza boxes): brief "walk (kick bottles)".</summary>
    [RequireComponent(typeof(CharacterController))]
    public class RexKick : MonoBehaviour
    {
        public float force = 2.5f;
        public AudioClip clink;
        float nextSound;

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic || rb.mass > 20f) return;
            Vector3 dir = new Vector3(hit.moveDirection.x, 0.25f, hit.moveDirection.z).normalized;
            float speed = GetComponent<CharacterController>().velocity.magnitude;
            // this runs every frame of contact: push only while the prop is slow (a big impulse each frame sent
            // bottles through the room's thin walls)
            if (rb.linearVelocity.sqrMagnitude < 4f)
                rb.AddForceAtPosition(dir * Mathf.Clamp(speed, 1f, 3f), hit.point, ForceMode.VelocityChange);
            if (Time.time > nextSound)
            {
                nextSound = Time.time + 0.15f;
                if (clink != null) AudioSource.PlayClipAtPoint(clink, hit.point, 0.6f);
                else Sfx.Play("bottle_clink", hit.point, 0.6f);
            }
        }
    }
}
