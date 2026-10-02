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
        public Vector3 armsOffset = new Vector3(0f, -0.04f, -0.06f);

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

        float side = 1f, currentDist, blend, aimT, wallDist = -1f, shake;

        /// <summary>Short camera shake (heavy hit, Rex hit). amount ~0.1 small .. 0.4 big; fades in ~0.25 s.</summary>
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);
        bool wasScoped, snapHeld, bodyHiddenNear;
        readonly RaycastHit[] camHits = new RaycastHit[16];
        Transform head;
        Vector3 armsHeadLocal;
        readonly List<(Renderer r, ShadowCastingMode m)> saved = new List<(Renderer, ShadowCastingMode)>();

        const string PrefKey = "RexMagnum_CameraMode";

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
                var arm = fpArms.GetComponentInChildren<Animator>();
                Transform armsHead = arm != null && arm.isHuman ? arm.GetBoneTransform(HumanBodyBones.Head) : null;
                if (armsHead == null) armsHead = FindDeep(fpArms.transform, "mixamorig:Head");     // not humanoid: find the bone by name
                if (armsHead != null) armsHeadLocal = fpArms.transform.InverseTransformPoint(armsHead.position);
                foreach (var r in fpArms.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            int def = PlayerPrefs.GetInt("RexMagnum_CamDefault", 2);     // settings: 0 FP, 1 TP, 2 remember last
            bool fp = def == 0 || (def == 2 && PlayerPrefs.GetInt(PrefKey, 1) == 0);
            SetMode(fp ? CameraMode.FirstPerson : CameraMode.ThirdPerson, true);
            // Rex is hit: a small shake (bigger hits shake more)
            var hp = target.GetComponent<Health>();
            if (hp != null) hp.Damaged += (a, p, by) => { if (!hp.IsDead) { Shake(Mathf.Clamp(a * 0.012f, 0.08f, 0.3f)); RMInput.Rumble(0.5f, 0.8f, 0.18f); } };
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
            if (shake > 0.001f)
            {
                // unscaled: a shake during the hit-stop still plays out; random roll/pitch kick, no position jump through walls.
                // Scaled by the field of view: a kick of N degrees fills more of a narrow (aim zoom) view, so it is smaller there.
                float s = shake * cam.fieldOfView / 65f;
                transform.rotation *= Quaternion.Euler(Random.Range(-1f, 1f) * s * 8f, Random.Range(-1f, 1f) * s * 8f, Random.Range(-1f, 1f) * s * 4f);
                shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.6f);
            }
            fov = FovSetting; fpFov = FovSetting + 10f; aimFov = FovSetting - 15f;
            if (!VRControlled)      // in VR the headset sets the field of view
            {
                // aim zoom blends over ~0.15 s (no snap); unscaled so it also works in the weapon wheel's slow motion
                aimT = Mathf.MoveTowards(aimT, IsAiming ? 1f : 0f, Time.unscaledDeltaTime / 0.15f);
                float ak = aimT * aimT * (3f - 2f * aimT);
                cam.fieldOfView = scoped ? 12f : Mathf.Lerp(Mathf.Lerp(fov, aimFov, ak), fpFov, b);
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
                // Put the arms so their head bone sits at the camera (arms follow the view)
                fpArms.transform.localRotation = Quaternion.identity;
                fpArms.transform.localPosition = -armsHeadLocal + armsOffset;
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
            return null;
        }

        static Quaternion rot0(float yaw) => Quaternion.Euler(0f, yaw, 0f);
        bool headBob = true;
        float nextBobPref, bobPhase, steadyY = float.NaN;
        RexController rexBody;

        /// <summary>World point the player aims at (crosshair), for both modes.</summary>
        Quaternion aimRot = Quaternion.identity;

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
                float bestAng = 4f * cam.fieldOfView / 65f;
                foreach (var b in Blob.All)
                {
                    if (b == null || b.IsDead) continue;
                    Vector3 c = b.transform.position + Vector3.up * 1.0f;
                    float d = Vector3.Distance(c, transform.position);
                    if (d > range) continue;
                    float ang = Vector3.Angle(fwd, c - transform.position);
                    if (ang >= bestAng) continue;
                    // in clear view: nothing between the camera and the blob, or the first thing hit is this blob
                    bool seen = !Physics.Linecast(transform.position, c, out RaycastHit wall, collisionMask, QueryTriggerInteraction.Ignore)
                                || wall.collider.GetComponentInParent<Blob>() == b;
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
