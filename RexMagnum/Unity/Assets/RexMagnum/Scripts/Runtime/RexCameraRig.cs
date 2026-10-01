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
        public float Pitch { get; private set; }
        public bool IsAiming { get; private set; }
        public bool InputEnabled { get; set; } = true;

        float side = 1f, currentDist, blend;
        bool wasScoped;
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
                if (armsHead != null) armsHeadLocal = fpArms.transform.InverseTransformPoint(armsHead.position);
                foreach (var r in fpArms.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            int def = PlayerPrefs.GetInt("RexMagnum_CamDefault", 2);     // settings: 0 FP, 1 TP, 2 remember last
            bool fp = def == 0 || (def == 2 && PlayerPrefs.GetInt(PrefKey, 1) == 0);
            SetMode(fp ? CameraMode.FirstPerson : CameraMode.ThirdPerson, true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void SetMode(CameraMode m, bool instant = false)
        {
            Mode = m;
            PlayerPrefs.SetInt(PrefKey, m == CameraMode.FirstPerson ? 0 : 1);
            bool fp = m == CameraMode.FirstPerson;
            foreach (var (r, mode) in saved)
                if (r != null) r.shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : mode;
            if (fpArms != null) fpArms.SetActive(fp);
            if (instant) blend = fp ? 1f : 0f;
        }

        void LateUpdate()
        {
            if (target == null || cam == null) return;
            float dt = Time.deltaTime;
            if (InputEnabled)
            {
                if (RMInput.CameraToggle)
                    SetMode(Mode == CameraMode.FirstPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson);
                if (RMInput.SwapShoulder) side = -side;
                Vector2 look = RMInput.LookDelta;
                Yaw += look.x;
                Pitch = Mathf.Clamp(Pitch - look.y, -75f, 80f);
            }
            IsAiming = InputEnabled && RMInput.Aim;
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

            // First-person position: Rex's eyes
            Vector3 eye = head != null
                ? head.position + target.rotation * eyeOffset
                : target.position + Vector3.up * 1.62f;

            // Third-person position: over the shoulder, no wall clipping
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            float want = IsAiming ? aimDistance : distance;
            currentDist = Mathf.Lerp(currentDist, want, dt * 8f);
            Vector3 offset = rot * new Vector3(shoulderOffset * side, 0.15f, -currentDist);
            Vector3 tp = pivot + offset;
            if (Physics.SphereCast(pivot, collisionRadius, offset.normalized, out RaycastHit hit, offset.magnitude,
                    collisionMask, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(target))
                tp = pivot + offset.normalized * Mathf.Max(0.2f, hit.distance - 0.05f);

            float b = blend * blend * (3f - 2f * blend);
            transform.SetPositionAndRotation(Vector3.Lerp(tp, eye, b), rot);
            cam.fieldOfView = scoped ? 12f : Mathf.Lerp(Mathf.Lerp(fov, aimFov, IsAiming ? 1f : 0f), fpFov, b);

            if (fpArms != null && fpArms.activeSelf)
            {
                // Put the arms so their head bone sits at the camera (arms follow the view)
                fpArms.transform.localRotation = Quaternion.identity;
                fpArms.transform.localPosition = -armsHeadLocal + armsOffset;
            }
        }

        /// <summary>World point the player aims at (crosshair), for both modes.</summary>
        public Vector3 AimPoint(float range = 200f)
        {
            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, range, collisionMask, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(target))
                return hit.point;
            return ray.GetPoint(range);
        }
    }
}
