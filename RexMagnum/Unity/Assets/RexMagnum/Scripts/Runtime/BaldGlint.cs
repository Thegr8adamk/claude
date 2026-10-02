using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Sun glints off Rex's bald spot (brief section 8). Third person, daytime: when the sun reflects off the top
    /// of his head toward the camera, a small star flashes there. Drawn on screen, so it costs almost nothing.
    /// </summary>
    public class BaldGlint : MonoBehaviour
    {
        public float sharpness = 60f;            // higher = a smaller sweet spot
        public float maxSize = 70f;              // pixels at 1080p
        RexController ctrl;
        Transform head;
        float glint;
        Vector3 screen;
        static Texture2D star;

        void Start()
        {
            ctrl = GetComponent<RexController>();
            if (ctrl.bodyAnimator != null && ctrl.bodyAnimator.isHuman) head = ctrl.bodyAnimator.GetBoneTransform(HumanBodyBones.Head);
        }

        void LateUpdate()
        {
            glint = 0f;
            var rig = ctrl.cameraRig;
            var sun = RenderSettings.sun;
            if (head == null || rig == null || rig.cam == null || sun == null || !sun.enabled) return;
            if (rig.Mode != RexCameraRig.CameraMode.ThirdPerson || DayNight.Night > 0.5f) return;
            Vector3 top = head.position + head.up * 0.17f - head.forward * 0.02f;     // crown of the head
            Vector3 n = (head.up * 0.85f - head.forward * 0.3f).normalized;           // bald spot faces up and back
            Vector3 toSun = -sun.transform.forward;
            if (Vector3.Dot(toSun, Vector3.up) < 0.05f) return;                      // sun below the horizon
            Vector3 toCam = (rig.cam.transform.position - top).normalized;
            Vector3 r = Vector3.Reflect(-toSun, n);
            float g = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(r, toCam)), sharpness) * (1f - DayNight.Night * 2f);
            if (g < 0.02f) return;
            screen = rig.cam.WorldToScreenPoint(top);
            if (screen.z < 0f) return;
            if (Physics.Linecast(rig.cam.transform.position, top, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(transform)) return;                         // something in front of his head
            glint = g;
        }

        void OnGUI()
        {
            if (glint <= 0f) return;
            if (star == null) star = MakeStar();
            float size = maxSize * Screen.height / 1080f * Mathf.Lerp(0.4f, 1f, glint) * (0.9f + 0.1f * Mathf.Sin(Time.time * 40f));
            GUI.color = new Color(1f, 0.97f, 0.85f, glint);
            GUI.DrawTexture(new Rect(screen.x - size / 2, Screen.height - screen.y - size / 2, size, size), star);
            GUI.color = Color.white;
        }

        static Texture2D MakeStar()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BaldGlint" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - n / 2f + 0.5f) / (n / 2f), dy = (y - n / 2f + 0.5f) / (n / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float core = Mathf.Clamp01(1f - d * 3f);
                    float rays = Mathf.Clamp01(1f - Mathf.Abs(dx) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(dy)) +
                                 Mathf.Clamp01(1f - Mathf.Abs(dy) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(dx));
                    float a = Mathf.Clamp01(core + rays * 0.8f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            t.Apply();
            return t;
        }
    }
}
