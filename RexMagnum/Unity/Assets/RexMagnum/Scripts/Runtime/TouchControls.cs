using System.Collections.Generic;
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
        // ---- layout editor (brief: drag / resize / opacity per button, saved, reset)
        public static bool EditMode;
        static readonly string[] AllButtons = { "Action", "Jump", "Use", "Pause", "Taunt", "Photo", "Hands", "Weapon", "Camera", "Move" };
        string selected = "Action";
        bool dragging;
        Vector2 lastPointer;
        static Vector2 Offset(string n) => new Vector2(PlayerPrefs.GetFloat($"RexMagnum_Touch_{n}_x", 0f), PlayerPrefs.GetFloat($"RexMagnum_Touch_{n}_y", 0f));
        static float Scale(string n) => PlayerPrefs.GetFloat($"RexMagnum_Touch_{n}_s", 1f);
        public static void ResetLayout()
        {
            foreach (var n in AllButtons) foreach (var k in new[] { "x", "y", "s" }) PlayerPrefs.DeleteKey($"RexMagnum_Touch_{n}_{k}");
            PlayerPrefs.DeleteKey("RexMagnum_TouchOpacity");
        }
        Texture2D circle;
        int moveId = -1, lookId = -1, weaponId = -1;
        Vector2 moveStart, movePos, lookLast, weaponLast;
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
        Vector2 Btn(string n) => BaseBtn(n) + Offset(n) * H;          // offsets are saved in screen heights

        Vector2 BaseBtn(string n)
        {
            switch (n)
            {
                case "Action": return new Vector2(W - H * 0.17f, H * 0.2f);
                case "Jump": return new Vector2(W - H * 0.17f, H * 0.45f);
                case "Use": return new Vector2(W - H * 0.42f, H * 0.14f);
                case "Pause": return new Vector2(W - H * 0.08f, H * 0.92f);
                case "Move": return new Vector2(H * 0.22f, H * 0.22f);
                default:
                    int i = System.Array.IndexOf(top, n);
                    return new Vector2(W * 0.5f + (i - 2) * H * 0.2f, H * 0.9f);
            }
        }
        float Rad(string n) => (n == "Action" || n == "Move" ? H * 0.12f : n == "Pause" ? H * 0.055f : H * 0.075f) * Scale(n);

        string HitButton(Vector2 p)
        {
            foreach (var n in new[] { "Action", "Jump", "Use", "Pause", "Taunt", "Photo", "Hands", "Weapon", "Camera" })
                if (Vector2.Distance(p, Btn(n)) < Rad(n) * 1.15f) return n;
            return null;
        }

        void EditUpdate()
        {
            // one pointer (finger or mouse): press on a button selects it, drag moves it
            Vector2 p; bool down, held;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed || (ts != null && ts.primaryTouch.press.wasReleasedThisFrame))
            { p = ts.primaryTouch.position.ReadValue(); down = ts.primaryTouch.press.wasPressedThisFrame; held = ts.primaryTouch.press.isPressed; }
            else if (Mouse.current != null)
            { p = Mouse.current.position.ReadValue(); down = Mouse.current.leftButton.wasPressedThisFrame; held = Mouse.current.leftButton.isPressed; }
            else return;
            if (p.y > H * 0.82f && p.x > W * 0.25f && p.x < W * 0.75f) { dragging = false; return; }   // edit toolbar area
            if (down)
            {
                foreach (var n in AllButtons)
                    if (Vector2.Distance(p, Btn(n)) < Rad(n) * 1.2f) { selected = n; dragging = true; break; }
                lastPointer = p;
            }
            if (held && dragging)
            {
                var o = Offset(selected) + (p - lastPointer) / H;
                PlayerPrefs.SetFloat($"RexMagnum_Touch_{selected}_x", o.x);
                PlayerPrefs.SetFloat($"RexMagnum_Touch_{selected}_y", o.y);
                lastPointer = p;
            }
            if (!held) dragging = false;
        }

        void Update()
        {
            opacity = PlayerPrefs.GetFloat("RexMagnum_TouchOpacity", 0.45f);
            if (EditMode) { TouchState.Active = false; EditUpdate(); return; }
            var ts = Touchscreen.current;
            TouchState.Active = ts != null && Gamepad.current == null;
            TouchState.AnyTap = false;
            if (!TouchState.Active) return;
            var held = new System.Collections.Generic.HashSet<string>();
            Vector2 look = Vector2.zero, wheel = Vector2.zero;
            bool weaponSeen = false;
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
                    if (HitButton(p) == "Weapon") { weaponId = id; weaponLast = p; }
                    if (HitButton(p) == null)
                    {
                        if (p.x < W * 0.45f && moveId < 0) { moveId = id; moveStart = movePos = p; }
                        else if (p.x >= W * 0.45f && lookId < 0) { lookId = id; lookLast = p; }
                    }
                }
                // the finger that holds Weapon steers the open weapon wheel, and keeps it open off the button
                if (id == weaponId)
                {
                    if (ended) weaponId = -1;
                    else
                    {
                        weaponSeen = true;
                        if (TouchState.WheelOpen) { held.Add("Weapon"); wheel += p - weaponLast; weaponLast = p; continue; }
                        weaponLast = p;
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
            TouchState.WheelDelta = wheel;
            if (!weaponSeen) weaponId = -1;
            bool act = held.Contains("Action");
            if (act && !TouchState.Held("Action")) actionStart = Time.time;
            foreach (var n in new[] { "Action", "Jump", "Use", "Pause", "Taunt", "Photo", "Hands", "Weapon", "Camera" })
                TouchState.Set(n, held.Contains(n));
            TouchState.Set("Aim", act && Time.time - actionStart > 0.3f);
        }

        readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        Texture2D Icon(string name)
        {
            if (!icons.TryGetValue(name, out var t)) { t = Resources.Load<Texture2D>("UI/Icons/" + name); icons[name] = t; }
            return t;
        }

        void OnGUI()
        {
            if (!TouchState.Active && !EditMode) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                fontSize = Mathf.RoundToInt(H * 0.028f) };
            style.normal.textColor = Color.white;
            void Draw(Vector2 c, float rad, string label, float a)
            {
                var rect = new Rect(c.x - rad, H - c.y - rad, rad * 2, rad * 2);
                GUI.color = new Color(1, 1, 1, a);
                GUI.DrawTexture(rect, circle);
                var icon = label != null ? Icon(label) : null;
                if (icon != null)                       // drawn icon (Resources/UI/Icons/<button>.png)
                {
                    GUI.color = new Color(1, 1, 1, Mathf.Clamp01(a * 1.8f));
                    float k = rad * 0.55f;
                    GUI.DrawTexture(new Rect(c.x - k, H - c.y - k, k * 2, k * 2), icon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                    return;
                }
                GUI.color = Color.white;
                if (label != null) GUI.Label(rect, label, style);
            }
            string word = "Punch";
            if (actions == null) actions = FindFirstObjectByType<RexActions>();
            var rw = actions;
            if (rw != null && !string.IsNullOrEmpty(rw.ActionWord)) word = rw.ActionWord;
            Draw(Btn("Action"), Rad("Action"), word, opacity);
            Draw(Btn("Jump"), Rad("Jump"), "Jump", opacity);
            Draw(Btn("Use"), Rad("Use"), "Use", (WeaponPickup.Nearby != null || FoodPickup.Nearby != null) ? opacity * 1.6f : opacity * 0.4f);
            Draw(Btn("Pause"), Rad("Pause"), "Pause", opacity);
            foreach (var n in top) Draw(Btn(n), Rad(n), n, opacity);
            if (moveId >= 0)
            {
                Draw(moveStart, H * 0.12f, null, opacity * 0.6f);
                Draw(movePos, H * 0.05f, null, opacity * 1.4f);
            }
            else Draw(Btn("Move"), Rad("Move"), "Move", opacity * 0.5f);
            if (EditMode)
            {
                var sel = Btn(selected);
                GUI.color = new Color(1f, 0.8f, 0.2f, 0.9f);
                GUI.DrawTexture(new Rect(sel.x - Rad(selected) * 1.15f, H - sel.y - Rad(selected) * 1.15f, Rad(selected) * 2.3f, Rad(selected) * 2.3f), circle);
                GUI.color = Color.white;
                float bw = W * 0.08f, bh = H * 0.07f, x0 = W * 0.5f - bw * 3f, y0 = H * 0.03f;
                var bs = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(H * 0.025f) };
                void S(float k) => PlayerPrefs.SetFloat($"RexMagnum_Touch_{selected}_s", Mathf.Clamp(Scale(selected) * k, 0.5f, 2f));
                if (GUI.Button(new Rect(x0, y0, bw, bh), "SIZE -", bs)) S(0.9f);
                if (GUI.Button(new Rect(x0 + bw, y0, bw, bh), "SIZE +", bs)) S(1.1f);
                if (GUI.Button(new Rect(x0 + bw * 2, y0, bw, bh), "FADE -", bs)) PlayerPrefs.SetFloat("RexMagnum_TouchOpacity", Mathf.Max(0.1f, opacity - 0.1f));
                if (GUI.Button(new Rect(x0 + bw * 3, y0, bw, bh), "FADE +", bs)) PlayerPrefs.SetFloat("RexMagnum_TouchOpacity", Mathf.Min(1f, opacity + 0.1f));
                if (GUI.Button(new Rect(x0 + bw * 4, y0, bw, bh), "RESET", bs)) ResetLayout();
                if (GUI.Button(new Rect(x0 + bw * 5, y0, bw, bh), "DONE", bs)) { EditMode = false; PlayerPrefs.Save(); }
                GUI.Label(new Rect(x0, y0 + bh, bw * 6, bh), $"Drag a button to move it. Selected: {selected}", style);
            }
        }
    }
}
