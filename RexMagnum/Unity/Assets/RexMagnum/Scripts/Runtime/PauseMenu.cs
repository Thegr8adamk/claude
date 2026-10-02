using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RexMagnum
{
    /// <summary>
    /// Pause menu (brief section 7: Pause = map, album, accomplishments, settings). Esc / Menu / touch II.
    /// Tabs: Map (island picture with Rex, zones and the next goal; next goal, zones visited, big goals, rescue clues),
    /// Album (photos + captions), Accomplishments (by group),
    /// Settings (SettingsPanel: graphics, camera + VR, sound, controls with keyboard and controller remap, help),
    /// Help (the controls of the device used last).
    /// Controller: LT / RT or keys 1-5 change the tab, B / Menu resume. Settings are saved in PlayerPrefs.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static bool Open { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Open = false;      // editor with Domain Reload off
        int tab;
        Vector2 scroll;
        readonly SettingsPanel panel = new SettingsPanel();
        readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
        static readonly string[] Tabs = { "MAP", "ALBUM", "ACCOMPLISHMENTS", "SETTINGS", "HELP" };
        GUIStyle title, label, button, small, tabStyle;

        void Start()
        {
            ApplySettings();
            panel.editTouchLayout = () => SetOpen(false);
        }

        public static void ApplySettings() => SettingsPanel.ApplyAll();

        void Update()
        {
            if (Open)
            {
                if (tab == 3)
                {
                    if (panel.Update()) { SetOpen(false); return; }
                    if (panel.Waiting || MenuNav.Consumed) return;      // a key that just finished a rebind does nothing else
                }
                else if (MenuNav.Back) { MenuNav.Consume(); SetOpen(false); return; }
                var g = UnityEngine.InputSystem.Gamepad.current;
                var k = Keyboard.current;
                if (g != null && g.leftTrigger.wasPressedThisFrame) SetTab(tab - 1);
                if (g != null && g.rightTrigger.wasPressedThisFrame) SetTab(tab + 1);
                if (k != null)
                    for (int i = 0; i < Tabs.Length; i++)
                        if (k[Key.Digit1 + i].wasPressedThisFrame) SetTab(i);
                if (tab != 3 && g != null)
                    scroll.y += -g.leftStick.ReadValue().y * 900f * Time.unscaledDeltaTime;   // scroll the lists with the stick
                if (tab != 3 && (MenuNav.Left || MenuNav.PagePrev)) SetTab(tab - 1);
                if (tab != 3 && (MenuNav.Right || MenuNav.PageNext)) SetTab(tab + 1);
                if (RMInput.Pause && !panel.Waiting && !MenuNav.Consumed) SetOpen(false);
                return;
            }
            if (GameFlow.Busy) return;
            if (RMInput.Pause) SetOpen(true);
        }

        int pendingTab = -1;
        float scaleBefore = 1f;
        bool rexHadInput = true;

        void LateUpdate()
        {
            // a tab clicked in OnGUI changes here, between GUI passes (changing it mid-event breaks the IMGUI layout)
            if (pendingTab >= 0) { SetTab(pendingTab); pendingTab = -1; }
        }

        void SetTab(int t)
        {
            tab = (t + Tabs.Length) % Tabs.Length;
            scroll = Vector2.zero;
            MenuNav.Click();
        }

        void SetOpen(bool o)
        {
            panel.CancelWait();
            Open = o;
            MenuNav.Click();
            if (!o) FreeThumbs();        // album pictures only live while the menu is open (phone memory)
            else { rexCached = null; nextObjective = 0f; }
            if (o) scaleBefore = Time.timeScale;
            // back to the kill cam's slow motion only while it still runs (else normal speed: no slow motion left behind)
            Time.timeScale = o ? 0f : (KillCam.Active && scaleBefore > 0f ? scaleBefore : 1f);
            AudioListener.pause = o;
            Cursor.lockState = o ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = o;
            var rex = FindAnyObjectByType<RexController>();
            // resume gives back the input Rex had (still off at the golf cart's wheel or in the ending)
            if (rex != null && o) rexHadInput = rex.InputEnabled || GolfCart.Driven != null;
            if (rex != null && GolfCart.Driven == null) rex.InputEnabled = !o && rexHadInput;
            var rig = FindAnyObjectByType<RexCameraRig>();
            if (rig != null) rig.InputEnabled = !o;
            if (!o) { Progress.Flush(); PlayerPrefs.Save(); }
        }

        void Styles(float s)
        {
            // the shared menu look (built once per screen height, not per OnGUI event)
            MenuSkin.Use(s);
            title = MenuSkin.Title; label = MenuSkin.Label; small = MenuSkin.Small; button = MenuSkin.Button; tabStyle = MenuSkin.Tab;
        }

        void OnGUI()
        {
            if (!Open) return;
            GUI.depth = -100;            // on top of every other OnGUI (ending bars, door fade, boss bar, toasts)
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            Styles(s);
            GUI.color = new Color(0, 0, 0, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, 20 * s, w, 50 * s), "PAUSED", title);
            float tw = w / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
                if (GUI.Toggle(new Rect(i * tw + 6, 80 * s, tw - 12, 50 * s), tab == i, Tabs[i], tabStyle) && tab != i) pendingTab = i;
            var area = new Rect(w * 0.08f, 150 * s, w * 0.84f, h - 260 * s);
            MenuSkin.DrawPanel(new Rect(area.x - 16 * s, area.y - 12 * s, area.width + 32 * s, area.height + 24 * s));
            if (tab == 3)
            {
                panel.Draw(area, s);
                if (GUI.Button(new Rect(w * 0.5f - 160 * s, h - 90 * s, 320 * s, 60 * s), "RESUME", button)) SetOpen(false);
                return;
            }
            if (tab == 0)
            {
                // the island picture on the left, the lists beside it
                float m = Mathf.Min(area.height, area.width * 0.52f);
                DrawMap(new Rect(area.x, area.y, m, m), s);
                area = new Rect(area.x + m + 24 * s, area.y, area.width - m - 24 * s, area.height);
            }
            GUILayout.BeginArea(area);
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: MapTab(); break;
                case 1: AlbumTab(s, area.width); break;
                case 2: AccomplishmentsTab(); break;
                case 4: HelpTab(s, area.width); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (GUI.Button(new Rect(w * 0.5f - 160 * s, h - 90 * s, 320 * s, 60 * s), "RESUME", button)) SetOpen(false);
        }

        Texture2D mapTex, arrowTex;
        RexController rexCached;
        RexWeapons rexWeapons;
        float nextObjective;
        string objective;
        Vector3 objectiveAt;
        bool objectiveHas;

        /// <summary>Island picture (Resources/UI/map) with the zones (green = visited), the next goal and Rex's arrow.</summary>
        void DrawMap(Rect r, float s)
        {
            if (mapTex == null) mapTex = Resources.Load<Texture2D>("UI/map");
            if (arrowTex == null) arrowTex = ArrowTexture();
            // Rex and the goal are found when the menu opens, then twice a second (OnGUI runs several times a frame)
            if (rexCached == null) { rexCached = FindAnyObjectByType<RexController>(); rexWeapons = rexCached != null ? rexCached.GetComponent<RexWeapons>() : null; }
            var rex = rexCached;
            if (Time.unscaledTime >= nextObjective)
            {
                nextObjective = Time.unscaledTime + 0.5f;
                objective = GameGuide.Objective(rexWeapons, rex != null ? rex.transform.position : Vector3.zero, out objectiveAt, out objectiveHas);
                if (GameFlow.TutorialActive) { objective = null; objectiveHas = false; }     // the room hints first (as the HUD)
            }
            if (mapTex != null) GUI.DrawTexture(r, mapTex, ScaleMode.StretchToFill);
            else { GUI.color = new Color(0.1f, 0.3f, 0.5f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
            Vector2 P(Vector3 world) { var uv = GameGuide.MapUV(world); return new Vector2(r.x + uv.x * r.width, r.y + uv.y * r.height); }
            void Dot(Vector2 p, float size, Color c)
            {
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(p.x - size / 2 - 2 * s, p.y - size / 2 - 2 * s, size + 4 * s, size + 4 * s), Texture2D.whiteTexture);
                GUI.color = c;
                GUI.DrawTexture(new Rect(p.x - size / 2, p.y - size / 2, size, size), Texture2D.whiteTexture);
            }
            foreach (var z in GameGuide.Zones)
                Dot(P(z.at), 16 * s, GameGuide.Visited(z.id) ? new Color(0.3f, 1f, 0.35f) : new Color(0.55f, 0.55f, 0.55f));
            var old = GUI.matrix;
            if (objectiveHas)
            {
                // the next goal: a yellow diamond that pulses
                var p = P(objectiveAt);
                float k = 22 * s * (1f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f));
                GUIUtility.RotateAroundPivot(45f, p);
                Dot(p, k, new Color(1f, 0.85f, 0.15f));
                GUI.matrix = old;
            }
            if (rex != null)
            {
                var p = P(rex.transform.position);
                float a = 34 * s;
                GUIUtility.RotateAroundPivot(rex.transform.eulerAngles.y, p);       // 0 = north (up), 90 = east (right)
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(p.x - a / 2, p.y - a / 2, a, a), arrowTex);
                GUI.matrix = old;
            }
            GUI.color = Color.white;
        }

        /// <summary>"You are here": a red arrow with a white edge, pointing up.</summary>
        static Texture2D ArrowTexture()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // y = 0 is the bottom row: tip at the top, notch at the bottom
                float u = (x - n / 2f) / (n / 2f), v = y / (float)n;
                float half = (1f - v) * 0.8f;                        // width at this height
                bool inside = Mathf.Abs(u) < half && v > 0.12f + Mathf.Abs(u) * 0.35f;
                bool core = Mathf.Abs(u) < half - 0.16f && v > 0.22f + Mathf.Abs(u) * 0.35f && v < 0.86f;
                t.SetPixel(x, y, core ? new Color(0.95f, 0.15f, 0.1f) : inside ? Color.white : Color.clear);
            }
            t.Apply();
            return t;
        }

        void MapTab()
        {
            if (!string.IsNullOrEmpty(objective)) GUILayout.Label("NEXT: " + objective, label);
            GUILayout.Label("Red arrow = you.  Green = visited.  Yellow = next goal.", small);
            GUILayout.Space(10);
            GUILayout.Label("ISLAND ZONES", title);
            foreach (var z in GameGuide.Zones)
                GUILayout.Label($"{(GameGuide.Visited(z.id) ? "[x]" : "[ ]")}  {z.name}", label);
            GUILayout.Space(20);
            GUILayout.Label("BIG GOALS", title);
            GUILayout.Label($"{(FactoryCore.Stopped ? "[x]" : "[ ]")}  Stop the blob factory (ruins)", label);
            GUILayout.Label($"{(PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) == 1 ? "[x]" : "[ ]")}  Pop King Glorp (ship)", label);
            GUILayout.Label($"Blobs popped: {Progress.Value("kills")}    Guests rescued: {Progress.Value("rescues")}    Photos: {Progress.Value("photos")}", small);
            GUILayout.Space(20);
            GUILayout.Label("CLUES", title);
            var clues = Progress.Clues;
            if (clues.Count == 0) GUILayout.Label("Rescue hiding guests: each one tells you a clue.", small);
            for (int i = clues.Count - 1; i >= 0; i--) GUILayout.Label("- " + clues[i], small);
        }

        void HelpTab(float s, float width)
        {
            var dev = RMInput.LastDevice;
            GUILayout.Label(dev == RMInput.DeviceKind.Touch ? "TOUCH" : dev == RMInput.DeviceKind.Gamepad ? "CONTROLLER" : "KEYBOARD + MOUSE", title);
            foreach (var c in GameGuide.Controls())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(c.what, label, GUILayout.Width(width * 0.3f));
                GUILayout.Label(c.how, label);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(16);
            GUILayout.Label("Keys and buttons: Settings > CONTROLS.  The NEXT line and the compass show a goal; the MAP tab shows where you are.", small);
        }

        void AlbumTab(float s, float width)
        {
            var photos = Progress.Photos;
            if (photos.Count == 0) { GUILayout.Label($"No photos yet. Press {RMInput.Btn("Photo")} near a blob or a guest.", label); return; }
            int cols = 3;
            float cw = width / cols - 20;
            for (int i = photos.Count - 1; i >= 0; i -= cols)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < cols && i - c >= 0; c++)
                {
                    var p = photos[i - c];
                    GUILayout.BeginVertical(GUILayout.Width(cw));
                    var t = Thumb(p.file);
                    if (t != null) GUILayout.Box(t, GUILayout.Width(cw), GUILayout.Height(cw * 9f / 16f));
                    GUILayout.Label(p.caption, small);
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
        }

        Texture2D Thumb(string file)
        {
            if (thumbs.TryGetValue(file, out var t)) return t;
            t = PhotoAlbum.LoadThumb(file);      // 384x216 thumb_<file> (made once from the full JPG for older photos)
            thumbs[file] = t;
            return t;
        }

        /// <summary>The menu closed: free the album pictures (loaded again on the next visit to ALBUM).</summary>
        void FreeThumbs()
        {
            foreach (var t in thumbs.Values) if (t != null) Destroy(t);
            thumbs.Clear();
        }

        void OnDestroy() => FreeThumbs();

        void AccomplishmentsTab()
        {
            string group = null;
            int done = 0;
            foreach (var a in Progress.All) if (Progress.IsUnlocked(a.id)) done++;
            GUILayout.Label($"{done} / {Progress.All.Length}", title);
            foreach (var a in Progress.All)
            {
                if (a.group != group) { group = a.group; GUILayout.Space(10); GUILayout.Label(group.ToUpper(), title); }
                bool ok = Progress.IsUnlocked(a.id);
                int v = Mathf.Min(Progress.Value(a.counter), a.need);
                GUILayout.Label($"{(ok ? "[x]" : "[ ]")}  {a.name} — {a.desc}{(ok || a.need <= 1 ? "" : $"  ({v}/{a.need})")}", ok ? label : small);
            }
        }
    }
}
