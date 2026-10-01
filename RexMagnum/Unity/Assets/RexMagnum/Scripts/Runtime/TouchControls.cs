using UnityEngine;
using UnityEngine.InputSystem;

namespace RexMagnum
{
    /// <summary>
    /// Phone controls (brief section 7): move stick bottom-left (push to the edge = run), drag the right side to look,
    /// Action bottom-right (hold = heavy / aim with a gun), Jump above Action, Use beside it (bright when something is
    /// near), Taunt / Photo / Hands / Weapon / Camera along the top, Pause in the top corner. Landscape.
    /// Hidden when a controller is connected.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        public float lookSpeed = 0.12f;          // degrees per pixel
        public float opacity = 0.45f;
        Texture2D circle;
        int moveId = -1, lookId = -1;
        Vector2 moveStart, movePos, lookLast;
        float actionStart;
        RexActions actions;
        readonly string[] top = { "Taunt", "Photo", "Hands", "Weapon", "Camera" };

        void Awake()
        {
            circle = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 64f;
                    float a = Mathf.Clamp01((1f - d) * 20f) * (d > 0.88f ? 1f : 0.45f);
                    circle.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            circle.Apply();
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        float H => Screen.height;
        float W => Screen.width;
        // button centres in screen pixels (origin bottom-left) and radius
        Vector2 Btn(string n)
        {
            switch (n)
            {
                case "Action": return new Vector2(W - H * 0.17f, H * 0.2f);
                case "Jump": return new Vector2(W - H * 0.17f, H * 0.45f);
                case "Use": return new Vector2(W - H * 0.42f, H * 0.14f);
                case "Pause": return new Vector2(W - H * 0.08f, H * 0.92f);
                default:
                    int i = System.Array.IndexOf(top, n);
                    return new Vector2(W * 0.5f + (i - 2) * H * 0.2f, H * 0.9f);
            }
        }
        float Rad(string n) => n == "Action" ? H * 0.12f : n == "Pause" ? H * 0.055f : H * 0.075f;

        string HitButton(Vector2 p)
        {
            foreach (var n in new[] { "Action", "Jump", "Use", "Pause", "Taunt", "Photo", "Hands", "Weapon", "Camera" })
                if (Vector2.Distance(p, Btn(n)) < Rad(n) * 1.15f) return n;
            return null;
        }

        void Update()
        {
            var ts = Touchscreen.current;
            TouchState.Active = ts != null && Gamepad.current == null;
            TouchState.AnyTap = false;
            if (!TouchState.Active) return;
            var held = new System.Collections.Generic.HashSet<string>();
            Vector2 look = Vector2.zero;
            bool moveSeen = false, lookSeen = false;
            foreach (var t in ts.touches)
            {
                var ph = t.phase.ReadValue();
                if (ph == UnityEngine.InputSystem.TouchPhase.None) continue;
                int id = t.touchId.ReadValue();
                Vector2 p = t.position.ReadValue();
                bool ended = ph == UnityEngine.InputSystem.TouchPhase.Ended || ph == UnityEngine.InputSystem.TouchPhase.Canceled;
                if (ph == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    TouchState.AnyTap = true;
                    if (HitButton(p) == null)
                    {
                        if (p.x < W * 0.45f && moveId < 0) { moveId = id; moveStart = movePos = p; }
                        else if (p.x >= W * 0.45f && lookId < 0) { lookId = id; lookLast = p; }
                    }
                }
                if (id == moveId)
                {
                    if (ended) moveId = -1; else { movePos = p; moveSeen = true; }
                    continue;
                }
                if (id == lookId)
                {
                    if (ended) lookId = -1;
                    else { look += (p - lookLast) * lookSpeed; lookLast = p; lookSeen = true; }
                    continue;
                }
                if (!ended)
                {
                    var b = HitButton(p);
                    if (b != null) held.Add(b);
                }
            }
            if (!moveSeen) moveId = -1;
            if (!lookSeen) lookId = -1;
            float r = H * 0.12f;
            TouchState.Move = moveId >= 0 ? Vector2.ClampMagnitude((movePos - moveStart) / r, 1f) : Vector2.zero;
            TouchState.LookDelta = look;
            bool act = held.Contains("Action");
            if (act && !TouchState.Held("Action")) actionStart = Time.time;
            foreach (var n in new[] { "Action", "Jump", "Use", "Pause", "Taunt", "Photo", "Hands", "Weapon", "Camera" })
                TouchState.Set(n, held.Contains(n));
            TouchState.Set("Aim", act && Time.time - actionStart > 0.3f);
        }

        void OnGUI()
        {
            if (!TouchState.Active) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                fontSize = Mathf.RoundToInt(H * 0.028f) };
            style.normal.textColor = Color.white;
            void Draw(Vector2 c, float rad, string label, float a)
            {
                var rect = new Rect(c.x - rad, H - c.y - rad, rad * 2, rad * 2);
                GUI.color = new Color(1, 1, 1, a);
                GUI.DrawTexture(rect, circle);
                GUI.color = Color.white;
                if (label != null) GUI.Label(rect, label, style);
            }
            string word = "Punch";
            if (actions == null) actions = FindFirstObjectByType<RexActions>();
            var rw = actions;
            if (rw != null && !string.IsNullOrEmpty(rw.ActionWord)) word = rw.ActionWord;
            Draw(Btn("Action"), Rad("Action"), word, opacity);
            Draw(Btn("Jump"), Rad("Jump"), "Jump", opacity);
            Draw(Btn("Use"), Rad("Use"), "Use", WeaponPickup.Nearby != null ? opacity * 1.6f : opacity * 0.4f);
            Draw(Btn("Pause"), Rad("Pause"), "II", opacity);
            foreach (var n in top) Draw(Btn(n), Rad(n), n, opacity);
            if (moveId >= 0)
            {
                Draw(moveStart, H * 0.12f, null, opacity * 0.6f);
                Draw(movePos, H * 0.05f, null, opacity * 1.4f);
            }
            else Draw(new Vector2(H * 0.22f, H * 0.22f), H * 0.12f, "Move", opacity * 0.5f);
        }
    }
}
