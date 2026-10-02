using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Rex movement for both camera modes. Moves relative to the camera.
    /// Third person: Rex turns toward where he walks (or toward the aim when aiming / attacking).
    /// First person: Rex always faces the camera direction.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(MartyPhone), typeof(BaldGlint))]
    public class RexController : MonoBehaviour
    {
        [Header("Links")]
        public RexCameraRig cameraRig;
        public Animator bodyAnimator;
        public Animator armsAnimator;     // first-person arms (optional)

        [Header("Move")]
        public float walkSpeed = 2.2f;
        public float runSpeed = 6.0f;
        public float handsDownBonus = 1.15f;   // brief: hands down = runs faster
        public float acceleration = 10f;
        public float turnSpeed = 720f;
        public float jumpHeight = 1.0f;         // he is 58
        public float gravity = -20f;

        [Header("Swim (pool, sea, river)")]
        public float swimSpeed = 1.5f;
        public float swimFastSpeed = 2.6f;
        public float swimDepth = 1.3f;          // feet this far under the surface: head stays out of the water
        public float idleLineAfter = 35f;       // brief: idle lines when the player does nothing

        public bool InputEnabled { get; set; } = true;
        public bool HandsDown { get; set; }
        public bool FaceAim { get; set; }      // set by actions (attack, aim)
        public Vector3 Velocity => velocity;
        public bool IsRunning { get; private set; }
        public bool Grounded => cc.isGrounded;
        public bool IsSwimming { get; private set; }
        public float WaterDepth { get; private set; }

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");
        static readonly int JumpHash = Animator.StringToHash("Jump");
        static readonly int SlipHash = Animator.StringToHash("Slip");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int DeathHash = Animator.StringToHash("Death");
        static readonly int SwimHash = Animator.StringToHash("Swim");
        float lockedUntil, lastInputTime;
        bool hasSwimParam, saidSlimeSwim;

        /// <summary>Comedy fall on slime. Rex cannot move for a moment.</summary>
        public void Slip()
        {
            if (Time.time < lockedUntil) return;
            Progress.Add("slips");
            SetTrigger(SlipHash);
            lockedUntil = Time.time + 1.7f;
            velocity.x *= 0.3f;
            velocity.z *= 0.3f;
            RexVoice.Say(gameObject, "Slip");
        }

        /// <summary>Hit by slime (a line, and later a slime look on the shirt).</summary>
        public void Slimed() => RexVoice.Say(gameObject, "Slime");

        public void Hurt() => SetTrigger(HitHash);

        public void Die()
        {
            SetTrigger(DeathHash);
            lockedUntil = Time.time + 999f;
        }

        public void Revive() => lockedUntil = 0f;

        float faceAimUntil;
        public void FaceAimFor(float seconds) => faceAimUntil = Mathf.Max(faceAimUntil, Time.time + seconds);

        CharacterController cc;
        Vector3 velocity;
        float planarSpeed;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (bodyAnimator != null)
                foreach (var p in bodyAnimator.parameters)
                    if (p.nameHash == SwimHash && p.type == AnimatorControllerParameterType.Bool) hasSwimParam = true;
        }

        void Update()
        {
            if (!cc.enabled) return;              // driving the golf cart
            float dt = Time.deltaTime;
            Vector2 input = InputEnabled ? RMInput.Move : Vector2.zero;
            if (Time.time < lockedUntil) input = Vector2.zero;
            bool run = InputEnabled && RMInput.Run && input.magnitude > 0.1f;
            IsRunning = run && new Vector2(velocity.x, velocity.z).magnitude > walkSpeed;

            float yaw = cameraRig != null ? cameraRig.Yaw : transform.eulerAngles.y;
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 wish = yawRot * new Vector3(input.x, 0f, input.y);
            UpdateWater();
            float target = IsSwimming ? (run ? swimFastSpeed : swimSpeed) * input.magnitude
                                      : (run ? runSpeed : walkSpeed) * (HandsDown ? handsDownBonus : 1f) * input.magnitude;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            planar = Vector3.MoveTowards(planar, wish.normalized * target, acceleration * Mathf.Max(target, 2f) * dt);
            velocity.x = planar.x;
            velocity.z = planar.z;

            if (IsSwimming)
            {
                // float: hold the head above the surface; Jump = a kick up (climb out at the pool edge)
                float want = (WaterDepth - swimDepth) * 3f;
                velocity.y = Mathf.MoveTowards(velocity.y, Mathf.Clamp(want, -2f, 2f), 12f * dt);
                if (InputEnabled && RMInput.Jump && Time.time >= lockedUntil) velocity.y = 4.5f;
            }
            else
            {
                if (cc.isGrounded)
                {
                    if (velocity.y < 0f) velocity.y = -2f;
                    if (InputEnabled && RMInput.Jump && Time.time >= lockedUntil)
                    {
                        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                        SetTrigger(JumpHash);
                    }
                }
                velocity.y += gravity * dt;
            }
            cc.Move(velocity * dt);

            // idle line (brief section 9) when the player does nothing for a while
            if (input.sqrMagnitude > 0.01f || RMInput.LookDelta.sqrMagnitude > 0.01f || RMInput.ActionHeld) lastInputTime = Time.time;
            else if (InputEnabled && Time.time - lastInputTime > idleLineAfter && Time.time >= lockedUntil)
            {
                RexVoice.Say(gameObject, "Idle");
                lastInputTime = Time.time;
            }

            // Turning
            bool fp = cameraRig != null && cameraRig.Mode == RexCameraRig.CameraMode.FirstPerson;
            if (fp || FaceAim || Time.time < faceAimUntil || (cameraRig != null && cameraRig.IsAiming))
                transform.rotation = Quaternion.RotateTowards(transform.rotation, yawRot, (fp ? 5000f : turnSpeed) * dt);
            else if (planar.sqrMagnitude > 0.05f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(planar.normalized), turnSpeed * dt);

            planarSpeed = planar.magnitude;
            if (bodyAnimator != null)
            {
                bodyAnimator.SetFloat(SpeedHash, planarSpeed, 0.1f, dt);
                bodyAnimator.SetBool(GroundedHash, cc.isGrounded || IsSwimming);
                if (hasSwimParam) bodyAnimator.SetBool(SwimHash, IsSwimming);
            }
            if (armsAnimator != null)
            {
                // First-person arms do not swing with the legs: they stay in idle while walking
                armsAnimator.SetFloat(SpeedHash, 0f);
                armsAnimator.SetBool(GroundedHash, cc.isGrounded);
            }
        }

        /// <summary>Start swimming when the water reaches Rex's chest; stop when he can stand (hysteresis).</summary>
        void UpdateWater()
        {
            bool inWater = WaterVolume.Surface(transform.position, out float surface, out bool slime);
            WaterDepth = inWater ? surface - transform.position.y : 0f;
            if (!IsSwimming && WaterDepth > swimDepth - 0.05f)
            {
                IsSwimming = true;
                Progress.Add("swims");
                if (slime && !saidSlimeSwim) { saidSlimeSwim = true; Slimed(); }
            }
            else if (IsSwimming && (WaterDepth < swimDepth - 0.35f || !inWater)) IsSwimming = false;
        }

        public void SetTrigger(int hash)
        {
            if (bodyAnimator != null) bodyAnimator.SetTrigger(hash);
            if (armsAnimator != null) armsAnimator.SetTrigger(hash);
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            cc.enabled = true;
            velocity = Vector3.zero;
        }
    }
}
