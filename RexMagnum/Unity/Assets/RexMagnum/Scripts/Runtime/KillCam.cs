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

        public static void Show(Transform target, Vector3 from, bool bullet)
        {
            if (Setting == 0 || target == null || Instance.running) return;
            if (Setting == 1 && Random.value > 0.35f) return;
            Instance.StartCoroutine(Instance.Run(target.position + Vector3.up * 0.8f, from, bullet));
        }

        public static void WatchCoconut(Transform t) => coconut = t;

        public static void CoconutDone(bool killed)
        {
            if (killed && coconut != null) Show(coconut, coconut.position + Vector3.up * 2f, false);
            coconut = null;
        }

        IEnumerator Run(Vector3 target, Vector3 from, bool bullet)
        {
            running = true;
            var main = Camera.main;
            cam = new GameObject("KillCamCamera").AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.depth = (main != null ? main.depth : 0) + 1;
            XRay(target, true);
            Time.timeScale = 0.15f;
            float t = 0f, dur = bullet ? 3.2f : 2.6f;
            Vector3 side = Vector3.Cross((target - from).normalized, Vector3.up).normalized;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / (dur * 0.6f));
                Vector3 pos;
                if (bullet)    // ride along the bullet path, then orbit the blob
                    pos = Vector3.Lerp(from, target - (target - from).normalized * 1.2f, k) + side * 0.3f + Vector3.up * 0.15f;
                else
                    pos = target + Quaternion.Euler(0, t * 60f, 0) * (side * 2.2f + Vector3.up * 0.6f);
                cam.transform.position = pos;
                cam.transform.LookAt(target);
                if (t > 0.3f && RMInput.AnyKey) break;     // skippable
                yield return null;
            }
            Time.timeScale = 1f;
            XRay(target, false);
            Destroy(cam.gameObject);
            running = false;
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
