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

        public static int Setting
        {
            get => PlayerPrefs.GetInt("RexMagnum_KillCam", 2);
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

        static bool Allowed() => Setting != 0 && !Instance.running && (Setting == 2 || Random.value <= 0.35f);

        public static void Show(Transform target, Vector3 from, bool bullet) => Show(target, from, bullet ? Style.Bullet : Style.Orbit);

        public static void Show(Transform target, Vector3 from, Style style)
        {
            if (target == null || !Allowed()) return;
            Instance.StartCoroutine(Instance.Run(target, target.position + Vector3.up * 0.8f, from, style));
        }

        static bool riding, rideKilled;

        /// <summary>Coconut (brief: camera rides the coconut, slime rain): ride it in slow motion when blobs are near.</summary>
        public static void WatchCoconut(Transform t)
        {
            coconut = t;
            if (t != null && Blob.LivingNear(t.position, 25f) > 0 && Allowed()) Instance.StartCoroutine(Instance.Ride(t));
        }

        public static void CoconutDone(bool killed)
        {
            if (riding) rideKilled = killed;                 // the ride shows the blast and the pop
            else if (killed && coconut != null) Show(coconut, coconut.position + Vector3.up * 2f, Style.Orbit);
            coconut = null;
        }

        Camera MakeCam()
        {
            var main = Camera.main;
            var c = new GameObject("KillCamCamera").AddComponent<Camera>();
            if (main != null) c.CopyFrom(main);
            c.fieldOfView = 45f;
            c.depth = (main != null ? main.depth : 0) + 1;
            return c;
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
            while (c != null && t < 4f)
            {
                t += Time.unscaledDeltaTime;
                Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
                if (v.sqrMagnitude > 1f) dir = Vector3.Lerp(dir, v.normalized, Time.unscaledDeltaTime * 6f);
                last = c.position;
                cam.transform.position = last - dir * 2.0f + Vector3.up * 0.6f;
                cam.transform.LookAt(last + dir * 1.5f);
                if (t > 0.3f && RMInput.AnyKey) break;
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
            Destroy(cam.gameObject);
            running = riding = false;
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
                if (t > 0.3f && RMInput.AnyKey) break;     // skippable
                yield return null;
            }
            if (Time.timeScale > 0f) Time.timeScale = 1f;   // not while the pause menu holds it at 0 (its key also skips the cam)
            XRay(target, false);
            Destroy(cam.gameObject);
            running = false;
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
            foreach (var b in Object.FindObjectsByType<Blob>(FindObjectsSortMode.None))
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
