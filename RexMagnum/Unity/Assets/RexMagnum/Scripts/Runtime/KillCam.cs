using System.Collections;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Kill cam (brief section 8): on a hold-attack kill, a sniper kill or the last blob of a group.
    /// Slow motion, the camera follows the bullet / weapon, X-ray jelly (brain visible), one-liner.
    /// 3-5 s, skip with any key. Setting (PlayerPrefs "RexMagnum_KillCam"): 0 off, 1 less often, 2 always.
    /// </summary>
    public class KillCam : MonoBehaviour
    {
        static KillCam inst;
        static Transform coconut;
        Camera cam;
        bool running;
        public static bool Active => inst != null && inst.running;

        public static int Setting
        {
            get => PlayerPrefs.GetInt("RexMagnum_KillCam", 1);     // default "less often": the fight keeps its pace
            set => PlayerPrefs.SetInt("RexMagnum_KillCam", value);
        }

        static KillCam Instance
        {
            get
            {
                if (inst == null) inst = new GameObject("KillCam").AddComponent<KillCam>();
                return inst;
            }
        }

        /// <summary>Camera moves per weapon (brief section 5): Orbit (generic X-ray), Bullet (follow the shot),
        /// Follow (golf: track the flying blob), Fist (close-up, fist in the jelly, shake), Towel (very slow, low,
        /// waves through the jelly).</summary>
        public enum Style { Orbit, Bullet, Follow, Fist, Towel }

        // VR: a second camera would take the view away from the headset (and fly it around: sickness), so no kill cam
        static bool Allowed() => Setting != 0 && !VRMode.Active && !Instance.running && (Setting == 2 || Random.value <= 0.35f);

        public static void Show(Transform target, Vector3 from, bool bullet) => Show(target, from, bullet ? Style.Bullet : Style.Orbit);

        public static void Show(Transform target, Vector3 from, Style style)
        {
            if (target == null || !Allowed()) return;
            Instance.StartCoroutine(Instance.Run(target, target.position + Vector3.up * 0.8f, from, style));
        }

        static bool riding, rideKilled;

        // editor with Domain Reload off: statics from the last play must not carry over
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { riding = rideKilled = false; inst = null; coconut = null; }

        /// <summary>Coconut (brief: camera rides the coconut, slime rain): ride it in slow motion only when it will land
        /// near a blob (a living blob within 6 m of the predicted landing point); a throw at nothing is not a show.</summary>
        public static void WatchCoconut(Transform t)
        {
            coconut = t;
            if (t == null) return;
            var rb = t.GetComponent<Rigidbody>();
            if (rb == null || !PredictLanding(t.position, rb.linearVelocity, rb, out Vector3 land)) return;
            if (Blob.LivingNear(land, 6f) > 0 && Allowed()) Instance.StartCoroutine(Instance.Ride(t));
        }

        /// <summary>First thing the throw arc hits (ballistic steps of 0.04 s, up to the 3 s fuse). Rex and the
        /// coconut itself do not count.</summary>
        static bool PredictLanding(Vector3 p, Vector3 v, Rigidbody self, out Vector3 land)
        {
            const float step = 0.04f;
            for (float t = 0f; t < RexWeapons.CoconutFuse; t += step)    // the fuse (2.2 s with the COCONUT POUCH)
            {
                Vector3 n = p + v * step + 0.5f * Physics.gravity * step * step;
                Vector3 seg = n - p;
                foreach (var h in Physics.RaycastAll(p, seg.normalized, seg.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.rigidbody == self || h.collider.GetComponentInParent<RexController>() != null) continue;
                    land = h.point;
                    return true;
                }
                v += Physics.gravity * step;
                p = n;
            }
            land = p;
            return true;               // still flying when the fuse ends: it blows up in the air there
        }

        public static void CoconutDone(Transform c, bool killed)
        {
            bool ridden = riding && c != null && c == coconut;   // another coconut's blast: not the one the camera rides
            if (ridden) rideKilled = killed;                 // the ride shows the blast and the pop
            else if (killed && c != null) Show(c, c.position + Vector3.up * 2f, Style.Orbit);
            if (c == coconut) coconut = null;
        }

        Camera MakeCam()
        {
            var main = Camera.main;
            var c = new GameObject("KillCamCamera").AddComponent<Camera>();
            if (main != null) c.CopyFrom(main);
            c.fieldOfView = 45f;
            c.depth = (main != null ? main.depth : 0) + 1;
            if (main != null)
            {
                // CopyFrom does not copy URP's camera data: same post-processing and anti-aliasing as the game
                var md = main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (md != null)
                {
                    if (!c.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var kd))
                        kd = c.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    kd.renderPostProcessing = md.renderPostProcessing;
                    kd.antialiasing = md.antialiasing;
                }
                // the game view is fully covered: do not render it twice (the AudioListener on its object stays on)
                main.enabled = false;
                hiddenMain = main;
            }
            return c;
        }

        Camera hiddenMain;

        /// <summary>Kill cam over (done, skipped, or this object is going away): drop its camera, the game camera renders again.</summary>
        void EndCam()
        {
            if (hiddenMain != null) hiddenMain.enabled = true;
            hiddenMain = null;
            if (cam != null) Destroy(cam.gameObject);
            cam = null;
        }

        // scene unload / destroyed mid-show: the coroutine never reaches its end
        void OnDestroy()
        {
            if (running && Time.timeScale > 0f) Time.timeScale = 1f;
            running = riding = rideKilled = false;
            coconut = null;
            EndCam();
        }

        IEnumerator Ride(Transform c)
        {
            running = riding = true;
            rideKilled = false;
            cam = MakeCam();
            cam.fieldOfView = 60f;
            Time.timeScale = 0.5f;
            var rb = c.GetComponent<Rigidbody>();
            Vector3 last = c.position, dir = Vector3.forward;
            float t = 0f;
            while (c != null && t < 6.5f)          // the 3 s fuse is 6 s at half speed: stay on it until the blast
            {
                if (PauseMenu.Open) { yield return null; continue; }     // waits behind the pause menu (Esc is not a skip)
                t += Time.unscaledDeltaTime;
                Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
                if (v.sqrMagnitude > 1f) dir = Vector3.Lerp(dir, v.normalized, Time.unscaledDeltaTime * 6f);
                last = c.position;
                cam.transform.position = last - dir * 2.0f + Vector3.up * 0.6f;
                cam.transform.LookAt(last + dir * 1.5f);
                if (t > 0.3f && RMInput.AnyKey) { RMInput.ActionBlockedFrame = Time.frameCount; break; }     // skippable; the skip click does not attack
                yield return null;
            }
            // blast: hold on the spot, then a short X-ray orbit if it killed (slime rain falls)
            float h = 0f;
            while (c == null && h < 1.4f)
            {
                h += Time.unscaledDeltaTime;
                Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
                cam.transform.position = last + Quaternion.Euler(0, h * 50f, 0) * (side * 4f + Vector3.up * 2f);
                cam.transform.LookAt(last);
                if (rideKilled) XRay(last, true);
                yield return null;
            }
            if (Time.timeScale > 0f) Time.timeScale = 1f;   // not while the pause menu holds it at 0 (its key also skips the cam)
            XRay(last, false);
            EndCam();
            running = riding = false;
            RMInput.ActionBlockedFrame = Time.frameCount + 1;   // one frame of grace: a press as the cam closes does nothing
        }

        IEnumerator Run(Transform tr, Vector3 target, Vector3 from, Style style)
        {
            running = true;
            cam = MakeCam();
            XRay(target, true);
            Time.timeScale = style == Style.Follow ? 0.35f : style == Style.Fist ? 0.1f : style == Style.Towel ? 0.08f : 0.15f;
            float t = 0f, dur = style == Style.Bullet ? 3.2f : style == Style.Follow ? 3.0f : style == Style.Fist ? 2.4f : 2.6f;
            Vector3 side = Vector3.Cross((target - from).normalized, Vector3.up).normalized;
            Vector3 flat = Vector3.ProjectOnPlane(target - from, Vector3.up).normalized;
            while (t < dur)
            {
                if (PauseMenu.Open) { yield return null; continue; }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / (dur * 0.6f));
                if (tr != null && style == Style.Follow) target = tr.position + Vector3.up * 0.8f;   // the blob is flying
                Vector3 pos;
                switch (style)
                {
                    case Style.Bullet:   // ride along the bullet path, then close on the blob
                        pos = Vector3.Lerp(from, target - (target - from).normalized * 1.2f, k) + side * 0.3f + Vector3.up * 0.15f;
                        break;
                    case Style.Follow:   // golf: chase cam behind and beside the flying blob
                        pos = target - flat * 4f + side * 1.5f + Vector3.up * 1.4f;
                        break;
                    case Style.Fist:     // fist in the jelly: very close, a hard shake that settles
                        pos = target - flat * 0.9f + side * 0.35f + Vector3.up * 0.05f + Random.insideUnitSphere * 0.06f * (1f - k);
                        break;
                    case Style.Towel:    // towel tip: low, slow side pass while waves run through the jelly
                        pos = target + side * Mathf.Lerp(1.6f, -1.6f, k) - flat * 1.2f - Vector3.up * 0.3f;
                        XRayWave(target, t);
                        break;
                    default:             // generic X-ray orbit
                        pos = target + Quaternion.Euler(0, t * 60f, 0) * (side * 2.2f + Vector3.up * 0.6f);
                        break;
                }
                cam.transform.position = pos;
                cam.transform.LookAt(target);
                if (t > 0.3f && RMInput.AnyKey) { RMInput.ActionBlockedFrame = Time.frameCount; break; }     // skippable
                yield return null;
            }
            if (Time.timeScale > 0f) Time.timeScale = 1f;   // not while the pause menu holds it at 0 (its key also skips the cam)
            XRay(target, false);
            EndCam();
            running = false;
            RMInput.ActionBlockedFrame = Time.frameCount + 1;   // one frame of grace: a press as the cam closes does nothing
        }

        /// <summary>Towel kill cam: the jelly's see-through level pulses, so a wave seems to run through it.</summary>
        static void XRayWave(Vector3 at, float t)
        {
            foreach (var b in Blob.All)
            {
                if (b == null || b.jellyRenderer == null || Vector3.Distance(b.transform.position, at) > 6f) continue;
                var mpb = new MaterialPropertyBlock();
                b.jellyRenderer.GetPropertyBlock(mpb);
                var c = b.BaseColor();
                mpb.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.2f + 0.25f * Mathf.Abs(Mathf.Sin(t * 9f))));
                b.jellyRenderer.SetPropertyBlock(mpb);
            }
        }

        /// <summary>X-ray look: jelly becomes very clear so the pink brain shows.</summary>
        static void XRay(Vector3 at, bool on)
        {
            foreach (var b in RMFind.All<Blob>())
            {
                if (b.jellyRenderer == null || Vector3.Distance(b.transform.position, at) > 6f) continue;
                var mpb = new MaterialPropertyBlock();
                b.jellyRenderer.GetPropertyBlock(mpb);
                var c = b.BaseColor();
                mpb.SetColor("_BaseColor", new Color(c.r, c.g, c.b, on ? 0.25f : c.a));
                b.jellyRenderer.SetPropertyBlock(mpb);
            }
        }
    }
}
