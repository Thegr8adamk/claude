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

        // First-person arms: the body clips hold the hands at the hips (idle / walk: ~0.65 m under the eye, out of
        // view), so with the hands up the fists are held up in front of the camera. An attack clip (jab, swing...)
        // takes over at once; the guard comes back when the clip ends.
        float guard;
        bool? isArms;
        static readonly Vector3 GuardR = new Vector3(0.17f, -0.14f, 0.30f), GuardL = new Vector3(-0.16f, -0.16f, 0.27f);
        static readonly Vector3 ElbowR = new Vector3(0.32f, -0.40f, 0.05f), ElbowL = new Vector3(-0.32f, -0.40f, 0.05f);

        static bool Calm(AnimatorStateInfo s) => s.IsName("Locomotion") || s.IsName("Limp");

        void OnAnimatorIK(int layer)
        {
            if (weapons == null || rig == null) return;
            bool gun = weapons.Current.isGun && weapons.Current.grip == Grip.Gun && !weapons.controller.HandsDown;
            weight = Mathf.MoveTowards(weight, gun ? 1f : 0f, Time.deltaTime * 6f);
            if (isArms == null) isArms = rig.fpArms != null && transform.IsChildOf(rig.fpArms.transform);
            if (isArms.Value && anim.isHuman)
            {
                bool calm = anim.IsInTransition(0) ? Calm(anim.GetNextAnimatorStateInfo(0)) : Calm(anim.GetCurrentAnimatorStateInfo(0));
                bool up = calm && !weapons.controller.HandsDown;
                guard = Mathf.MoveTowards(guard, up ? 1f : 0f, Time.deltaTime * (up ? 4f : 16f));
                if (guard > 0f)
                {
                    // camera space (the arms are a child of the camera); position only: the wrists keep the clip's turn
                    Transform c = rig.transform;
                    float k = guard * guard * (3f - 2f * guard), kr = k * (1f - weight);   // a gun aims the right hand
                    anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, k);
                    anim.SetIKPosition(AvatarIKGoal.LeftHand, c.TransformPoint(GuardL));
                    anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, k);
                    anim.SetIKHintPosition(AvatarIKHint.LeftElbow, c.TransformPoint(ElbowL));
                    anim.SetIKPositionWeight(AvatarIKGoal.RightHand, kr);
                    anim.SetIKPosition(AvatarIKGoal.RightHand, c.TransformPoint(GuardR));
                    anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, kr);
                    anim.SetIKHintPosition(AvatarIKHint.RightElbow, c.TransformPoint(ElbowR));
                }
            }
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
