using UnityEngine;

namespace RexMagnum
{
    /// <summary>Points the right arm (and the gun) at the crosshair when a gun is out. Needs IK Pass on the layer.</summary>
    [RequireComponent(typeof(Animator))]
    public class RexAimIK : MonoBehaviour
    {
        public RexWeapons weapons;
        public RexCameraRig rig;
        Animator anim;
        float weight;

        void Awake() => anim = GetComponent<Animator>();

        void OnAnimatorIK(int layer)
        {
            if (weapons == null || rig == null) return;
            bool gun = weapons.Current.isGun && weapons.Current.grip == Grip.Gun && !weapons.controller.HandsDown;
            weight = Mathf.MoveTowards(weight, gun ? 1f : 0f, Time.deltaTime * 6f);
            if (weight <= 0f) return;
            var shoulder = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Vector3 aim = (rig.AimPoint() - shoulder.position).normalized;
            Vector3 hand = shoulder.position + aim * 0.55f + Vector3.down * 0.05f;
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, weight * 0.8f);
            anim.SetIKPosition(AvatarIKGoal.RightHand, hand);
            anim.SetIKRotation(AvatarIKGoal.RightHand, Quaternion.LookRotation(aim, Vector3.up));
            anim.SetLookAtWeight(weight * 0.6f, 0.3f, 0.6f);
            anim.SetLookAtPosition(rig.AimPoint());
        }
    }
}
