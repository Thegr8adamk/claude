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
        public float walkSpeed = 3.0f;
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
        /// <summary>Low health (set by RexLife): no run, top speed LimpSpeed (the limp clip cannot go faster).</summary>
        public bool Limping { get; set; }
        public const float LimpSpeed = 2.0f;
        /// <summary>Low health with a blob near (set by RexLife): no limp (a 2 m/s limp vs 5.7 m/s blobs is a death
        /// spiral), but tired: walk 3, run capped at HurtRunSpeed.</summary>
        public bool HurtInFight { get; set; }
        public const float HurtRunSpeed = 5.0f;    // under the blob's 5.7 (it still catches up), not hopeless
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
        float kickUntil, nextSlip;

        /// <summary>Comedy fall on slime. Rex cannot move for a moment.</summary>
        public void Slip()
        {
            if (Time.time < lockedUntil || Time.time < nextSlip) return;
            nextSlip = Time.time + 4f;                 // a slime-covered fight is not a chain of falls
            Progress.Add("slips");
            SetTrigger(SlipHash);
            lockedUntil = Time.time + 1.1f;
            velocity.x *= 0.3f;
            velocity.z *= 0.3f;
            RexVoice.Say(gameObject, "Slip");
        }

        /// <summary>Hit by slime: a line, and the shirt gets a wet green slime look (brief section 7a) that dries off
        /// slowly and washes off in clean water.</summary>
        public void Slimed()
        {
            RexVoice.Say(gameObject, "Slime");
            SlimeAmount = Mathf.Min(1f, SlimeAmount + 0.45f);
        }

        /// <summary>0 = clean shirt, 1 = covered in slime.</summary>
        public float SlimeAmount { get; private set; }
        Renderer[] shirt;
        Color[] shirtColor;
        float[] shirtSmooth;
        MaterialPropertyBlock slimeBlock;
        float shownSlime;
        bool inSlimeWater;

        void UpdateSlimeLook()
        {
            float wash = IsSwimming ? (inSlimeWater ? -0.2f : 0.35f) : 0.012f;   // slime water adds, clean water washes
            SlimeAmount = Mathf.Clamp01(SlimeAmount - wash * Time.deltaTime);
            if (Mathf.Abs(SlimeAmount - shownSlime) < 0.01f && (SlimeAmount > 0f || shownSlime == 0f)) return;
            shownSlime = SlimeAmount;
            if (shirt == null)
            {
                shirt = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.name.Contains("Shirt"));
                slimeBlock = new MaterialPropertyBlock();
                // the shirt's own color and shine: the slime look is blended over them, not over plain white
                shirtColor = new Color[shirt.Length];
                shirtSmooth = new float[shirt.Length];
                for (int i = 0; i < shirt.Length; i++)
                {
                    var m = shirt[i].sharedMaterial;
                    shirtColor[i] = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                    shirtSmooth[i] = m != null && m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.3f;
                }
            }
            for (int i = 0; i < shirt.Length; i++)
            {
                var r = shirt[i];
                if (r == null) continue;
                if (SlimeAmount <= 0f) { r.SetPropertyBlock(null); continue; }        // clean: the shirt's own material again
                r.GetPropertyBlock(slimeBlock);
                slimeBlock.SetColor("_BaseColor", shirtColor[i] * Color.Lerp(Color.white, new Color(0.55f, 1f, 0.4f), SlimeAmount * 0.8f));
                slimeBlock.SetFloat("_Smoothness", Mathf.Lerp(shirtSmooth[i], 0.85f, SlimeAmount));   // wet shine
                r.SetPropertyBlock(slimeBlock);
            }
        }

        public void Hurt() => SetTrigger(HitHash);

        public void Die()
        {
            SetTrigger(DeathHash);
            SetDead(true);
            lockedUntil = Time.time + 999f;
        }

        public void Revive() { lockedUntil = 0f; SetDead(false); }

        static readonly int DeadHash = Animator.StringToHash("Dead");
        void SetDead(bool on)
        {
            // controllers built before the "Dead" parameter existed simply skip it
            foreach (var a in new[] { bodyAnimator, armsAnimator })
                if (a != null && a.isActiveAndEnabled)
                    foreach (var p in a.parameters) if (p.nameHash == DeadHash) { a.SetBool(DeadHash, on); break; }
        }

        /// <summary>Dead (or on the floor after a slip): no attacks.</summary>
        public bool Locked => Time.time < lockedUntil;

        float faceAimUntil;
        bool wasGrounded;
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
            UpdateSlimeLook();
            if (!cc.enabled) return;              // driving the golf cart
            float dt = Time.deltaTime;
            Vector2 input = InputEnabled ? RMInput.Move : Vector2.zero;
            if (Time.time < lockedUntil) input = Vector2.zero;
            bool run = InputEnabled && RMInput.Run && input.magnitude > 0.1f && !(Limping && !IsSwimming);
            IsRunning = run && new Vector2(velocity.x, velocity.z).magnitude > walkSpeed;

            float yaw = cameraRig != null ? cameraRig.MoveYaw : transform.eulerAngles.y;   // VR: where the head looks
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 wish = yawRot * new Vector3(input.x, 0f, input.y);
            UpdateWater();
            float target = IsSwimming ? (run ? swimFastSpeed : swimSpeed) * input.magnitude
                                      : (run ? runSpeed : walkSpeed) * (HandsDown ? handsDownBonus : 1f) * input.magnitude;
            if (Limping && !IsSwimming) target = Mathf.Min(target, LimpSpeed);    // the limp: no sliding feet
            else if (HurtInFight && !IsSwimming) target = Mathf.Min(target, run ? HurtRunSpeed : walkSpeed);
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            planar = Vector3.MoveTowards(planar, wish.normalized * target, acceleration * Mathf.Max(target, 2f) * dt);
            // dodge (Ctrl / B / touch Dodge): a quick ~4 m burst, untouchable for a moment. Fast at the start, easing out.
            if (InputEnabled && RMInput.Dodge && cc.isGrounded && !IsSwimming && Time.time >= lockedUntil && Time.time >= nextDodge)
                StartDodge(input.sqrMagnitude > 0.01f ? wish.normalized : -transform.forward, input.sqrMagnitude <= 0.01f);
            bool dodging = Time.time < dodgeUntil;
            if (dodging) planar = dodgeDir * Mathf.Lerp(18f, 8.7f, 1f - (dodgeUntil - Time.time) / DodgeTime);   // avg ~13.3 m/s = 4 m
            velocity.x = planar.x;
            velocity.z = planar.z;

            if (IsSwimming)
            {
                // float: hold the head above the surface; Jump = a kick up (climb out at the pool edge)
                float want = (WaterDepth - swimDepth) * 3f;
                if (Time.time > kickUntil) velocity.y = Mathf.MoveTowards(velocity.y, Mathf.Clamp(want, -2f, 2f), 12f * dt);
                else velocity.y += gravity * 0.5f * dt;                 // the kick: up and over the pool edge
                if (InputEnabled && RMInput.Jump && Time.time >= lockedUntil && Time.time > kickUntil)
                {
                    velocity.y = 7.5f;          // ~1.6 m rise: enough to get over the pool coping
                    kickUntil = Time.time + 0.45f;
                }
            }
            else
            {
                if (cc.isGrounded)
                {
                    if (velocity.y < 0f) velocity.y = -2f - new Vector2(velocity.x, velocity.z).magnitude;   // stays on slopes and steps down
                    if (InputEnabled && RMInput.Jump && Time.time >= lockedUntil)
                    {
                        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                        SetTrigger(JumpHash);
                    }
                }
                else if (wasGrounded && velocity.y < -2f) velocity.y = -2f;   // ran off a ledge: start the fall gently
                wasGrounded = cc.isGrounded;
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
            else if (planar.sqrMagnitude > 0.05f && !(dodging && dodgeBack))    // a back dodge keeps facing the enemy
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

        const float DodgeTime = 0.3f;
        static readonly int DodgeHash = Animator.StringToHash("Dodge");
        float dodgeUntil, nextDodge;
        Vector3 dodgeDir;
        bool dodgeBack;
        Health hp;

        void StartDodge(Vector3 dir, bool back)
        {
            dodgeDir = dir;
            dodgeBack = back;
            dodgeUntil = Time.time + DodgeTime;
            nextDodge = Time.time + 1.0f;       // 1 s cooldown: no chain-dodging through every attack
            if (hp == null) hp = GetComponent<Health>();
            if (hp != null) hp.Invulnerable(0.35f);
            SetTrigger(DodgeHash);
            Sfx.Play("swing", transform.position + Vector3.up, 0.5f, 0.7f);    // low whoosh
        }

        /// <summary>Start swimming when the water reaches Rex's chest; stop when he can stand (hysteresis).</summary>
        void UpdateWater()
        {
            bool inWater = WaterVolume.Surface(transform.position, out float surface, out bool slime);
            inSlimeWater = inWater && slime;
            WaterDepth = inWater ? surface - transform.position.y : 0f;
            if (!IsSwimming && WaterDepth > swimDepth - 0.05f)
            {
                IsSwimming = true;
                velocity.y = Mathf.Max(velocity.y, -3f);                // a fall into deep water stops near the top
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
