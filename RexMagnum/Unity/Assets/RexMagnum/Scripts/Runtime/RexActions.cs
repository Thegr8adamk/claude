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
        public float holdTime = 0.3f;

        static readonly int Jab = Animator.StringToHash("Jab");
        static readonly int Heavy = Animator.StringToHash("Heavy");
        static readonly int Taunt = Animator.StringToHash("Taunt");
        static readonly int Photo = Animator.StringToHash("Photo");
        static readonly int HandsDownHash = Animator.StringToHash("HandsDown");

        float pressTime = -1f, busyUntil, faceAimUntil;

        public string ActionWord => controller.HandsDown ? "Talk" : "Punch";

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

            if (!controller.HandsDown)
            {
                if (RMInput.ActionDown) pressTime = Time.time;
                if (pressTime >= 0f && !busy)
                {
                    if (RMInput.ActionUp && Time.time - pressTime < holdTime)
                    {
                        Do(Jab, 0.45f);
                        pressTime = -1f;
                    }
                    else if (RMInput.ActionHeld && Time.time - pressTime >= holdTime)
                    {
                        Do(Heavy, 0.9f);
                        pressTime = -1f;
                    }
                }
                if (RMInput.ActionUp) pressTime = -1f;
            }

            if (RMInput.Taunt && !busy)
            {
                Do(Taunt, 1.6f);
                if (face) face.Expression("SmugGrin", 1.6f);
            }
            if (RMInput.Photo && !busy)
            {
                Do(Photo, 1.9f);
                if (face) face.Expression("Smile", 1.9f);
            }
            controller.FaceAim = Time.time < faceAimUntil;
        }

        void Do(int trigger, float seconds)
        {
            controller.SetTrigger(trigger);
            busyUntil = Time.time + seconds * 0.8f;
            faceAimUntil = Time.time + seconds;
        }
    }
}
