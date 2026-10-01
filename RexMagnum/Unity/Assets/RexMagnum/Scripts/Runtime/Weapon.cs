using UnityEngine;

namespace RexMagnum
{
    /// <summary>On every weapon model: grip, tip, muzzle and up markers (from Blender empties).</summary>
    public class Weapon : MonoBehaviour
    {
        public WeaponId id;
        public Transform grip, tip, muzzle, upRef;

        public Transform GripPoint => grip != null ? grip : transform;

        /// <summary>Weapon's own forward (grip -> tip/muzzle) and up (grip -> UPREF), in its local space.</summary>
        public Quaternion LocalFrame()
        {
            Transform front = muzzle != null ? muzzle : tip;
            Vector3 f = front != null ? transform.InverseTransformPoint(front.position) - transform.InverseTransformPoint(GripPoint.position) : Vector3.forward;
            Vector3 u = upRef != null ? transform.InverseTransformPoint(upRef.position) - transform.InverseTransformPoint(GripPoint.position) : Vector3.up;
            if (f.sqrMagnitude < 1e-6f) f = Vector3.forward;
            return Quaternion.LookRotation(f.normalized, Vector3.ProjectOnPlane(u, f).normalized);
        }
    }
}
