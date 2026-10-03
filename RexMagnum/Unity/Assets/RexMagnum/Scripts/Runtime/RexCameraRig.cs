using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RexMagnum
{
    /// <summary>
    /// First-person and third-person camera for Rex. Switch with V (keyboard) or
    /// right-stick click (controller). The last mode is remembered.
    ///
    /// Third person: over the shoulder, swap shoulder with C / D-pad left, camera does not
    /// go through walls, zooms in when aiming.
    /// First person: camera at Rex's eyes. The body only casts a shadow, and the
    /// separate high-detail arms model is shown.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class RexCameraRig : MonoBehaviour
    {
        public enum CameraMode { FirstPerson, ThirdPerson }

        [Header("Links")]
        public Transform target;            // Rex root
        public Animator bodyAnimator;       // to find the head bone
        public Camera cam;
        public GameObject fpArms;           // first-person arms model (child of the camera)
        public Renderer[] bodyRenderers;    // hidden (shadow only) in first person
        public RexWeapons weapons;          // sniper scope forces a first-person scope view

        [Header("Third person")]
        public float distance = 2.6f;
        public float aimDistance = 1.3f;
        public float shoulderOffset = 0.5f;
        public float pivotHeight = 1.55f;
        public float collisionRadius = 0.2f;
        public LayerMask collisionMask = ~0;

        [Header("Field of view")]
        public float fov = 65f;
        public float aimFov = 50f;
        public float fpFov = 75f;

        [Header("First person")]
        public Vector3 eyeOffset = new Vector3(0f, 0.07f, 0.11f);   // from the head bone
        public Vector3 armsOffset = new Vector3(0f, 0f, -0.05f);    // arms' head bone from the eye: the jab reaches into view

        public CameraMode Mode { get; private set; } = CameraMode.ThirdPerson;
        public float Yaw { get; private set; }
        /// <summary>Teleports (wake-up, respawn): the view turns with Rex.</summary>
        public void SetYaw(float yaw) { Yaw = yaw; Pitch = 0f; }
        public float Pitch { get; private set; }
        public bool IsAiming { get; private set; }
        public bool InputEnabled { get; set; } = true;
        /// <summary>VR mode: the headset turns the view (VRMode adds the head pose after this script).</summary>
        public bool VRControlled { get; set; }
        /// <summary>VR mode: head turn left/right from straight ahead, degrees.</summary>
        public float HeadYaw { get; set; }
        /// <summary>Direction for walking: the view's yaw (in VR the head counts too).</summary>
        public float MoveYaw => Yaw + (VRControlled ? HeadYaw : 0f);
        /// <summary>Field of view from Settings (third person; first person is 10 more).</summary>
        public static float FovSetting = 65f;
        /// <summary>Hit tilt (the view rolls away from a hit) on/off; SettingsPanel saves it as PlayerPrefs "rm_hittilt".</summary>
        public static bool HitTiltEnabled = true;
        public const string HitTiltKey = "rm_hittilt";

        float side = 1f, currentDist, blend, aimT, wallDist = -1f, shake;

        /// <summary>Short camera shake (heavy hit, Rex hit). amount ~0.1 small .. 0.4 big; fades in ~0.25 s.</summary>
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);
        /// <summary>Gun recoil: the view kicks up by this many degrees and settles back in ~0.2 s (the aim does not move).</summary>
        public void Kick(float degrees) => kick = Mathf.Min(kick + degrees, 6f);
        float kick;
        float hitRoll, hitRollAt = -10f;
        const float HitRollTime = 0.25f;
        /// <summary>Heavy attack wind-up cue: the view pushes in by this many degrees of FOV and eases back (~0.35 s).</summary>
        public void FovPush(float degrees) => fovPush = Mathf.Max(fovPush, degrees);
        float fovPush;          // < 0: a dodge kick (the view widens)
        /// <summary>Dodge cue: the view widens by this many degrees of FOV and eases back (~0.4 s). Not in VR / the scope.</summary>
        public void DodgeKick(float degrees) => fovPush = Mathf.Min(fovPush, -degrees);
        /// <summary>Melee soft aim: turn the view (and so Rex) this many degrees over ~0.1 s. Not in VR (sickness).</summary>
        public void NudgeYaw(float degrees) { if (!VRControlled) nudge = degrees; }
        float nudge;
        bool wasScoped, snapHeld, bodyHiddenNear;
        readonly RaycastHit[] camHits = new RaycastHit[16];
        Transform head;
        Vector3 armsHeadLocal;
        readonly List<(Renderer r, ShadowCastingMode m)> saved = new List<(Renderer, ShadowCastingMode)>();

        const string PrefKey = "RexMagnum_CameraMode";

        void Awake() => HitTiltEnabled = PlayerPrefs.GetInt(HitTiltKey, 1) == 1;

        void Start()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (bodyAnimator != null && bodyAnimator.isHuman) head = bodyAnimator.GetBoneTransform(HumanBodyBones.Head);
            Yaw = target != null ? target.eulerAngles.y : 0f;
            currentDist = distance;
            if (bodyRenderers == null || bodyRenderers.Length == 0)
                bodyRenderers = target.GetComponentsInChildren<Renderer>();
            foreach (var r in bodyRenderers) saved.Add((r, r.shadowCastingMode));
            if (fpArms != null)
            {
                // the arms model's "eye": head bone, else neck / shoulders / chest / hands by name, else the mesh top
                armsHeadLocal = ArmsEyePoint(fpArms.transform, out _);
                foreach (var r in fpArms.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            int def = PlayerPrefs.GetInt("RexMagnum_CamDefault", 2);     // settings: 0 FP, 1 TP, 2 remember last
            bool fp = def == 0 || (def == 2 && PlayerPrefs.GetInt(PrefKey, 1) == 0);
            SetMode(fp ? CameraMode.FirstPerson : CameraMode.ThirdPerson, true);
            // Rex is hit: a shake by the hit's weight (RexController.HitWeight): a tick for light hits, a hard
            // jolt and a longer rumble for a bouncer slam or King Glorp's big hits
            var hp = target.GetComponent<Health>();
            if (hp != null) hp.Damaged += (a, p, by) =>
            {
                if (hp.IsDead) return;
                int w = RexController.HitWeight(a, by);
                if (w >= 2) { Shake(0.45f); RMInput.Rumble(0.9f, 1f, 0.3f); }
                else if (w == 1) { Shake(Mathf.Clamp(a * 0.012f, 0.1f, 0.3f)); RMInput.Rumble(0.5f, 0.8f, 0.18f); }
                else { Shake(0.06f); RMInput.Rumble(0.3f, 0.5f, 0.1f); }
                // directional reaction: the view tilts away from the side the hit came from (the attacker; Rex's own
                // coconut: the blast point). Light 3, heavy 6 degrees, eased back over 0.25 s. Not in VR.
                if (by == null || VRControlled || !HitTiltEnabled) return;
                Vector3 src = by.transform.IsChildOf(target) ? p : by.transform.position;
                float sideDot = Vector3.Dot(transform.right, Vector3.ProjectOnPlane(src - target.position, Vector3.up));
                if (Mathf.Abs(sideDot) < 0.2f) return;            // straight ahead / behind: the shake alone
                hitRoll = Mathf.Sign(sideDot) * (w >= 2 ? 6f : 3f);    // + rolls the view's top to the left: away from a hit on the right
                hitRollAt = Time.unscaledTime;
            };
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Settings: change the view now (first or third person). VR stays first person.</summary>
        public void SetModeFromSettings(CameraMode m) { if (!VRControlled) SetMode(m); }

        public void SetMode(CameraMode m, bool instant = false)
        {
            if (VRControlled) m = CameraMode.FirstPerson;
            Mode = m;
            bodyHiddenNear = false;
            PlayerPrefs.SetInt(PrefKey, m == CameraMode.FirstPerson ? 0 : 1);
            bool fp = m == CameraMode.FirstPerson;
            foreach (var (r, mode) in saved)
                if (r != null) r.shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : mode;
            if (fpArms != null) fpArms.SetActive(fp);
            // the arms' animator starts fresh when shown: give it the hands down/up state again
            var rc = target != null ? target.GetComponent<RexController>() : null;
            if (fp && rc != null && rc.armsAnimator != null && rc.armsAnimator.isActiveAndEnabled)
                rc.armsAnimator.SetBool("HandsDown", rc.HandsDown);
            if (instant) blend = fp ? 1f : 0f;
        }

        void OnDisable() => RMInput.StopRumble();

        /// <summary>Window focus back (alt-tab, the editor's Game view clicked again): lock the cursor again when in
        /// play (not over a menu, the title or the intro) and drop the first mouse moves (one big jump).</summary>
        void OnApplicationFocus(bool focused)
        {
            RMInput.SkipMouseLook(focused ? 3 : 1);
            if (!focused || Application.isMobilePlatform || !InputEnabled || PauseMenu.Open || GameFlow.Busy) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void LateUpdate()
        {
            RMInput.TickRumble();
            if (target == null || cam == null) return;
            float dt = Time.deltaTime;
            if (InputEnabled)
            {
                if (RMInput.CameraToggle && !(weapons != null && weapons.Scoped))     // not while looking through the scope
                    SetMode(Mode == CameraMode.FirstPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson);
                if (RMInput.SwapShoulder) side = -side;
                Vector2 look = RMInput.LookDelta;
                if (weapons != null && weapons.Scoped) look *= 12f / Mathf.Max(1f, FovSetting);   // scope (12°): same feel as unzoomed
                // aim assist slow-down (controller stick, not scoped): the look slows to 55% while the crosshair is
                // within ~3 degrees of a living blob's centre, so the stick does not sail past it (the AimPoint pull stays)
                // (guns only: with a melee weapon the slow-down just dragged the turn while fighting a crowd)
                else if (!VRControlled && RMInput.AimAssist && RMInput.LookIsStick && !TouchState.Active
                         && (weapons == null || weapons.Current.isGun || weapons.Aiming)
                         && look.sqrMagnitude > 0f && CrosshairOnBlob((SettingsPanel.Easy ? 4.5f : 3f) * cam.fieldOfView / 65f))
                    look *= 0.55f;
                if (VRControlled)
                {
                    // the headset does up/down; the stick or mouse turns Rex (smooth, or 30-degree snaps: less sickness)
                    Pitch = 0f;
                    if (PlayerPrefs.GetInt(VRMode.SnapKey, 1) == 1)
                    {
                        float stick = RMInput.LookStickX;
                        if (Mathf.Abs(stick) > 0.7f && !snapHeld) { Yaw += Mathf.Sign(stick) * 30f; snapHeld = true; }
                        else if (Mathf.Abs(stick) < 0.3f) snapHeld = false;
                        if (!RMInput.LookIsStick) Yaw += look.x;
                    }
                    else Yaw += look.x;
                }
                else
                {
                    Yaw += look.x;
                    Pitch = Mathf.Clamp(Pitch - look.y, -75f, 80f);
                }
            }
            if (Mathf.Abs(nudge) > 0.05f) { float st = nudge * Mathf.Min(1f, dt * 18f); Yaw += st; nudge -= st; }
            else nudge = 0f;
            // only a gun aims (zoom + face the crosshair); right click with fists or a towel does nothing
            IsAiming = InputEnabled && RMInput.Aim && (weapons == null || weapons.Aiming);
            bool scoped = weapons != null && weapons.Scoped;
            bool fp = Mode == CameraMode.FirstPerson || scoped;
            if (scoped != wasScoped)
            {
                // brief: the sniper scope always uses the first-person view, then goes back
                foreach (var (r, mode) in saved)
                    if (r != null) r.shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : mode;
                if (fpArms != null) fpArms.SetActive(fp && !scoped);
                wasScoped = scoped;
                if (!scoped) SetMode(Mode);
            }
            blend = Mathf.MoveTowards(blend, fp ? 1f : 0f, dt * 5f);

            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);

            // First-person position: Rex's eyes. Head bob setting (Pause > Settings): on = follow the head bone plus a
            // small walk bob; off = steady eye height (the head bone's walk motion is filtered out).
            Vector3 eye = head != null
                ? head.position + target.rotation * eyeOffset
                : target.position + Vector3.up * 1.62f;
            if (Time.unscaledTime > nextBobPref) { nextBobPref = Time.unscaledTime + 1f; headBob = PlayerPrefs.GetInt("RexMagnum_HeadBob", 1) == 1; }
            if (rexBody == null) rexBody = target.GetComponent<RexController>();
            float speed = rexBody != null && rexBody.Grounded ? new Vector2(rexBody.Velocity.x, rexBody.Velocity.z).magnitude : 0f;
            if (headBob)
            {
                bobPhase += dt * Mathf.Lerp(0f, 11f, Mathf.Clamp01(speed / 6f)) + dt * Mathf.Clamp01(speed) * 2f;
                float amp = Mathf.Clamp01(speed / 6f) * 0.035f;
                eye += Vector3.up * (Mathf.Abs(Mathf.Sin(bobPhase)) * amp) + rot0(Yaw) * Vector3.right * (Mathf.Sin(bobPhase) * amp * 0.5f);
            }
            else
            {
                // low-pass the eye height: steps and slopes still follow, the walk bob (about 2 per second) does not
                steadyY = float.IsNaN(steadyY) || Mathf.Abs(steadyY - eye.y) > 1f ? eye.y : Mathf.Lerp(steadyY, eye.y, dt * 3f);
                eye.y = steadyY;
            }

            // Third-person position: over the shoulder, no wall clipping
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            float want = IsAiming ? aimDistance : distance;
            currentDist = Mathf.Lerp(currentDist, want, dt * 8f);
            Vector3 offset = rot * new Vector3(shoulderOffset * side, 0.15f, -currentDist);
            Vector3 tp = pivot + offset;
            // only walls and static things pull the camera in; a blob, guest, cart or bottle passing between Rex
            // and the camera does not make it jump
            int n = Physics.SphereCastNonAlloc(pivot, collisionRadius, offset.normalized, camHits, offset.magnitude,
                                               collisionMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = camHits[i];
                if (h.distance <= 0f || h.transform.IsChildOf(target) || h.rigidbody != null) continue;
                if (h.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
                nearest = Mathf.Min(nearest, h.distance);
            }
            // fast in, slow out: a wall pulls the camera in at once; when it clears, the camera eases back out
            // (no pop from 0.5 m to 2.6 m in one frame when Rex steps past a pillar)
            float free = nearest < float.MaxValue ? Mathf.Max(0.2f, nearest - 0.05f) : offset.magnitude;
            wallDist = wallDist < 0f || free < wallDist ? free : Mathf.MoveTowards(wallDist, free, dt * 4f);
            tp = pivot + offset.normalized * Mathf.Min(wallDist, offset.magnitude);

            float b = blend * blend * (3f - 2f * blend);
            transform.SetPositionAndRotation(Vector3.Lerp(tp, eye, b), rot);
            aimRot = rot;                                      // the aim ignores the shake below
            if (VRControlled || scoped) shake = 0f;            // no shake in a headset (sickness) or through the scope
            if (VRControlled) kick = 0f;
            if (shake > 0.001f)
            {
                // unscaled: a shake during the hit-stop still plays out; smooth Perlin wobble (not a new random angle each
                // frame), rotation only (no position jump through walls), fading with the amount.
                // Scaled by the field of view: a kick of N degrees fills more of a narrow (aim zoom) view, so it is smaller there.
                float s = shake * cam.fieldOfView / 65f, nt = Time.unscaledTime * 22f;
                transform.rotation *= Quaternion.Euler((Mathf.PerlinNoise(nt, 0.1f) * 2f - 1f) * s * 8f,
                                                       (Mathf.PerlinNoise(3.7f, nt) * 2f - 1f) * s * 8f,
                                                       (Mathf.PerlinNoise(nt, 7.3f) * 2f - 1f) * s * 4f);
                shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.6f);
            }
            if (hitRoll != 0f)
            {
                float rt = (Time.unscaledTime - hitRollAt) / HitRollTime;
                if (rt >= 1f || VRControlled || !HitTiltEnabled) hitRoll = 0f;
                else transform.rotation *= Quaternion.Euler(0f, 0f, hitRoll * (1f - rt * rt * (3f - 2f * rt)));   // eased back
            }
            if (kick > 0.01f)
            {
                // through the scope the same degrees fill far more of the narrow view: scale like the shake
                float climb = scoped ? kick * cam.fieldOfView / 65f : kick;
                transform.rotation *= Quaternion.Euler(-climb, 0f, 0f);      // muzzle climb, view only
                kick = Mathf.Lerp(kick, 0f, Time.unscaledDeltaTime * 14f);
            }
            fov = FovSetting; fpFov = FovSetting + 10f; aimFov = FovSetting - 15f;
            if (!VRControlled)      // in VR the headset sets the field of view
            {
                // aim zoom blends over ~0.15 s (no snap); unscaled so it also works in the weapon wheel's slow motion
                aimT = Mathf.MoveTowards(aimT, IsAiming ? 1f : 0f, Time.unscaledDeltaTime / 0.15f);
                float ak = aimT * aimT * (3f - 2f * aimT);
                cam.fieldOfView = scoped ? 12f : Mathf.Lerp(Mathf.Lerp(fov, aimFov, ak), fpFov, b) - fovPush;
            }
            if (fovPush != 0f)
            {
                if (VRControlled || scoped) fovPush = 0f;
                fovPush = Mathf.MoveTowards(fovPush, 0f, Time.unscaledDeltaTime * 12f);
            }

            // third person against a wall: the camera comes very close to Rex; hide his body (shadow stays) so the
            // view is not inside his head
            bool near = !fp && !scoped && b < 0.5f && Vector3.Distance(transform.position, pivot) < 0.9f;
            if (!fp && near != bodyHiddenNear)
            {
                bodyHiddenNear = near;
                foreach (var (r, mode) in saved)
                    if (r != null) r.shadowCastingMode = near ? ShadowCastingMode.ShadowsOnly : mode;
            }

            if (fpArms != null && fpArms.activeSelf)
            {
                // Put the arms so their head bone (or its estimate) sits at the camera (arms follow the view)
                fpArms.transform.localRotation = Quaternion.identity;
                fpArms.transform.localPosition = -armsHeadLocal + armsOffset;
            }
        }

        /// <summary>Point in the arms root's space that belongs at the camera (Rex's head bone). Without a head bone
        /// (an import that dropped it) it is estimated from the neck, shoulders, chest or hands (Rex's proportions),
        /// then from the top of the mesh, so the arms never hang with their feet at the camera.</summary>
        public static Vector3 ArmsEyePoint(Transform arms, out string how)
        {
            var an = arms.GetComponentInChildren<Animator>();
            Transform hd = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
            if (hd == null) hd = FindBone(arms, "Head");
            if (hd != null) { how = "head bone"; return arms.InverseTransformPoint(hd.position); }
            var nk = FindBone(arms, "Neck");
            if (nk != null) { how = "neck bone"; return arms.InverseTransformPoint(nk.position) + Vector3.up * 0.09f; }
            Transform sl = FindBone(arms, "LeftArm"), sr = FindBone(arms, "RightArm");
            if (sl != null && sr != null) { how = "shoulder bones"; return arms.InverseTransformPoint((sl.position + sr.position) * 0.5f) + Vector3.up * 0.19f; }
            var ch = FindBone(arms, "Spine2");
            if (ch != null) { how = "chest bone"; return arms.InverseTransformPoint(ch.position) + Vector3.up * 0.43f; }
            Transform hl = FindBone(arms, "LeftHand"), hr = FindBone(arms, "RightHand");
            if (hl != null && hr != null) { how = "hand bones"; return arms.InverseTransformPoint((hl.position + hr.position) * 0.5f) + new Vector3(0f, 0.49f, -0.15f); }
            var rs = arms.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                how = "mesh bounds";
                return arms.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.center.z)) + Vector3.up * 0.12f;
            }
            how = "fixed height";
            return new Vector3(0f, 1.55f, 0f);
        }

        /// <summary>Bone by Mixamo name with any prefix ("mixamorig:Head", "Head"); exact, so "LeftArm" is not "LeftForeArm".</summary>
        public static Transform FindBone(Transform t, string name)
        {
            if (t.name == name || t.name.EndsWith(":" + name)) return t;
            foreach (Transform c in t) { var f = FindBone(c, name); if (f != null) return f; }
            return null;
        }

        static Quaternion rot0(float yaw) => Quaternion.Euler(0f, yaw, 0f);
        bool headBob = true;
        float nextBobPref, bobPhase, steadyY = float.NaN;
        RexController rexBody;

        /// <summary>World point the player aims at (crosshair), for both modes.</summary>
        Quaternion aimRot = Quaternion.identity;
        /// <summary>Steady aim direction: the crosshair without shake / recoil kick (VR: the head).</summary>
        public Vector3 AimForward => VRControlled ? transform.forward : aimRot * Vector3.forward;

        /// <summary>A living blob's centre (1 m up) within deg of the steady crosshair, in clear view, within 120 m.</summary>
        bool CrosshairOnBlob(float deg)
        {
            Vector3 eyePos = transform.position, fwd = aimRot * Vector3.forward;
            foreach (var b in Blob.All)
            {
                if (b == null || b.IsDead) continue;
                Vector3 c = b.transform.position + Vector3.up * 1.0f;
                if ((c - eyePos).sqrMagnitude > 120f * 120f || Vector3.Angle(fwd, c - eyePos) >= deg) continue;
                // in clear view: nothing static between (Rex himself and other blobs do not block)
                if (!Physics.Linecast(eyePos, c, out RaycastHit block, collisionMask, QueryTriggerInteraction.Ignore) ||
                    block.transform.IsChildOf(target) || block.collider.GetComponentInParent<Blob>() != null) return true;
            }
            return false;
        }

        public Vector3 AimPoint(float range = 200f)
        {
            // the crosshair direction without the camera shake (VR: the head direction set by VRMode)
            Vector3 fwd = VRControlled ? transform.forward : aimRot * Vector3.forward;
            Ray ray = new Ray(transform.position, fwd);
            // aim assist (phone + controller, setting): pull the aim to the nearest blob within 4 degrees
            // not for mouse players, not through the scope (precise brain shots), only for blobs in clear view
            if (RMInput.AimAssist && (TouchState.Active || RMInput.LookIsStick) && !(weapons != null && weapons.Scoped))
            {
                Blob best = null;
                float bestAng = (SettingsPanel.Easy ? 6.5f : 4f) * cam.fieldOfView / 65f;     // Difficulty EASY: a wider pull
                foreach (var b in Blob.All)
                {
                    if (b == null || b.IsDead) continue;
                    Vector3 c = b.transform.position + Vector3.up * 1.0f;
                    float d = Vector3.Distance(c, transform.position);
                    if (d > range) continue;
                    float ang = Vector3.Angle(fwd, c - transform.position);
                    if (ang >= bestAng) continue;
                    // in clear view: nothing between the camera and the blob, or the first thing hit is this blob
                    // (the line from the camera often passes through Rex himself: look again from just past him)
                    bool blocked = Physics.Linecast(transform.position, c, out RaycastHit wall, collisionMask, QueryTriggerInteraction.Ignore);
                    if (blocked && target != null && wall.transform.IsChildOf(target))
                        blocked = Physics.Linecast(wall.point + (c - transform.position).normalized * 0.7f, c, out wall,
                                                   collisionMask, QueryTriggerInteraction.Ignore);
                    bool seen = !blocked || wall.collider.GetComponentInParent<Blob>() == b;
                    if (seen) { bestAng = ang; best = b; }
                }
                if (best != null) return best.transform.position + Vector3.up * 1.0f;
            }
            if (Physics.Raycast(ray, out RaycastHit hit, range, collisionMask, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(target))
                return hit.point;
            return ray.GetPoint(range);
        }
    }
}
