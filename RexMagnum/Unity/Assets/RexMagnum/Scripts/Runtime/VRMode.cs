using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace RexMagnum
{
    /// <summary>
    /// VR mode (Settings > Camera &amp; VR > VR mode). Works with any OpenXR headset on PC (Quest Link, SteamVR, WMR).
    /// Needs the "XR Plug-in Management" + "OpenXR" packages (menu Rex Magnum > Add VR Support) with OpenXR ticked in
    /// Project Settings > XR Plug-in Management. "Initialize XR on Startup" stays OFF: XR starts here only when the
    /// player turns VR on, so the normal game never needs a headset.
    ///
    /// In VR: first person only; the headset turns the view (look up/down, around); the right stick / mouse turns Rex
    /// (smooth or 30-degree snap turn, setting); the gun aims where the player looks; the HUD floats in front of the eyes.
    /// The packages are found by reflection, so the game compiles and runs without them (VR then reports "not installed").
    /// </summary>
    [DefaultExecutionOrder(200)]          // after RexCameraRig (100): the head pose goes on top of the rig's position
    public class VRMode : MonoBehaviour
    {
        public const string PrefKey = "RexMagnum_VR";
        public const string SnapKey = "RexMagnum_VRSnapTurn";
        public static bool Wanted => PlayerPrefs.GetInt(PrefKey, 0) == 1;
        public static bool Active { get; private set; }
        public static string Status { get; private set; } = "VR is off.";
        public static event Action<bool> Changed;

        static VRMode inst;
        RexCameraRig rig;
        Camera cam;
        bool busy, loaderOn, stopAfterStart;
        Quaternion headRot = Quaternion.identity;
        Vector3 headPos, headZero;
        bool zeroed;
        float yawZero;

        void Awake()
        {
            inst = this;
            rig = GetComponent<RexCameraRig>();
            cam = GetComponent<Camera>();
        }

        void Start()
        {
            if (Wanted) StartCoroutine(StartXR());
        }

        void OnDestroy()
        {
            if (inst == this) inst = null;
            if (Active || loaderOn) StopXRNow();
        }

        /// <summary>Settings toggle. Saves the choice and starts or stops VR now.</summary>
        public static void Set(bool on)
        {
            PlayerPrefs.SetInt(PrefKey, on ? 1 : 0);
            PlayerPrefs.Save();
            if (inst == null) { Status = on ? "VR starts when the game starts." : "VR is off."; return; }
            if (inst.busy) { inst.stopAfterStart = !on; return; }     // remembered: applied when the start finishes
            if (on && !Active) inst.StartCoroutine(inst.StartXR());
            else if (!on && (Active || inst.loaderOn)) inst.StopXRNow();
        }

        /// <summary>Put the view straight ahead again (Settings button and when VR starts).</summary>
        public static void Recenter() { if (inst != null) inst.zeroed = false; }

        // ------------------------------------------------------------ XR start / stop (by reflection)

        static object Manager()
        {
            var t = Type.GetType("UnityEngine.XR.Management.XRGeneralSettings, Unity.XR.Management");
            var gs = t?.GetProperty("Instance")?.GetValue(null);
            return gs?.GetType().GetProperty("Manager")?.GetValue(gs);
        }

        IEnumerator StartXR()
        {
            busy = true;
            Status = "Starting VR...";
            var m = Manager();
            if (m == null)
            {
                Status = "VR is not installed. In Unity: Rex Magnum > Add VR Support.";
                busy = false;
                yield break;
            }
            var init = m.GetType().GetMethod("InitializeLoader")?.Invoke(m, null) as IEnumerator;
            if (init != null) yield return init;
            var loader = m.GetType().GetProperty("activeLoader")?.GetValue(m);
            if (loader == null)
            {
                Status = "No VR headset found. Connect the headset (Quest Link / SteamVR), then turn VR on again.";
                busy = false;
                yield break;
            }
            loaderOn = true;
            m.GetType().GetMethod("StartSubsystems")?.Invoke(m, null);
            // an OpenXR session can take a few seconds to come up
            for (float t = 0f; t < 6f && !XRSettings.isDeviceActive; t += Time.unscaledDeltaTime) yield return null;
            busy = false;
            if (!XRSettings.isDeviceActive)
            {
                StopXRNow();
                Status = "The headset did not start. Put it on, check the headset app (Quest Link / SteamVR), then turn VR on again.";
                yield break;
            }
            Active = true;
            Status = "VR is on: " + XRSettings.loadedDeviceName;
            Enter();
            if (stopAfterStart) { stopAfterStart = false; StopXRNow(); }
        }

        void StopXRNow()
        {
            var m = Manager();
            if (m != null)
            {
                m.GetType().GetMethod("StopSubsystems")?.Invoke(m, null);
                m.GetType().GetMethod("DeinitializeLoader")?.Invoke(m, null);
            }
            loaderOn = false;
            Active = false;
            Status = "VR is off.";
            Leave();
        }

        // ------------------------------------------------------------ view

        void Enter()
        {
            zeroed = false;
            // Unity's own camera tracking would add the pose a second time: this script places the camera itself
            var dev = typeof(XRDevice).GetMethod("DisableAutoXRCameraTracking");
            if (dev != null && cam != null) dev.Invoke(null, new object[] { cam, true });
            if (rig != null)
            {
                rig.SetMode(RexCameraRig.CameraMode.FirstPerson, true);
                rig.VRControlled = true;
            }
            if (cam != null) cam.nearClipPlane = 0.05f;
            Changed?.Invoke(true);
        }

        void Leave()
        {
            if (rig != null) rig.VRControlled = false;
            if (cam != null) cam.nearClipPlane = 0.03f;
            Changed?.Invoke(false);
        }

        static readonly List<InputDevice> heads = new List<InputDevice>();

        void LateUpdate()
        {
            if (!Active) return;
            if (!XRSettings.isDeviceActive) { StopXRNow(); Status = "The headset was disconnected. VR is off."; return; }
            InputDevices.GetDevicesAtXRNode(XRNode.Head, heads);
            if (heads.Count > 0)
            {
                var h = heads[0];
                if (h.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion r)) headRot = r;
                if (h.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 p)) headPos = p;
            }
            if (!zeroed)
            {
                // straight ahead = the way Rex faces now; small head moves (lean) are kept, standing height is not
                headZero = headPos;
                yawZero = headRot.eulerAngles.y;
                zeroed = true;
            }
            if (rig == null) return;
            if (!rig.isActiveAndEnabled) { transform.rotation = Quaternion.Euler(0f, rig.Yaw, 0f) * headRot; return; }   // title / intro: the rig does not reset the position, so no lean
            Quaternion local = Quaternion.Euler(0f, -yawZero, 0f) * headRot;     // head turn from "straight ahead"
            rig.HeadYaw = Mathf.DeltaAngle(0f, local.eulerAngles.y);
            Quaternion body = Quaternion.Euler(0f, rig.Yaw, 0f);
            Vector3 lean = Vector3.ClampMagnitude(Quaternion.Euler(0f, -yawZero, 0f) * (headPos - headZero), 0.35f);
            transform.SetPositionAndRotation(transform.position + body * lean, body * local);
        }
    }
}
