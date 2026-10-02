using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RexMagnum
{
    /// <summary>
    /// Pause menu (brief section 7: Pause = map, album, accomplishments, settings). Esc / Menu / touch II.
    /// Tabs: Map (island picture with Rex, zones and the next goal; next goal, zones visited, big goals, rescue clues),
    /// Album (photos + captions), Accomplishments (the run's stats, then the list by group),
    /// Settings (SettingsPanel: graphics, camera + VR, sound, controls with keyboard and controller remap, help),
    /// Help (the controls of the device used last).
    /// Controller: LT / RT or keys 1-5 change the tab, Up / Down scroll the lists, B / Menu resume,
    /// Y / key T: QUIT TO TITLE (asks first, saves, loads the scene again). Settings are saved in PlayerPrefs.
    /// Ultrawide: the panel stops at ~1.9 x the screen height.
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
        GUIStyle title, header, label, button, small, tabStyle;
        float ui = 1f;          // screen height / 1080 (section rules in the GUILayout tabs)

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
                RexVoice.Freeze(Time.unscaledDeltaTime);      // voice lines, the duck and the subtitles wait while paused
                if (confirmQuit)
                {
                    // QUIT TO TITLE asks first: YES / NO (NO is chosen first)
                    if (MenuNav.Up || MenuNav.Down || MenuNav.Left || MenuNav.Right) quitSel = 1 - quitSel;
                    if (MenuNav.Back) { MenuNav.Consume(); confirmQuit = false; }
                    else if (MenuNav.Ok) { MenuNav.Consume(); if (quitSel == 0) QuitToTitle(); else confirmQuit = false; }
                    return;
                }
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
                // arrows / W S / D-pad scroll the long lists (accomplishments, album, map info); the scroll view clamps the end
                if (tab != 3 && MenuNav.Up) scroll.y = Mathf.Max(0f, scroll.y - 140f * Screen.height / 1080f);
                if (tab != 3 && MenuNav.Down) scroll.y += 140f * Screen.height / 1080f;
                if (tab != 3 && (MenuNav.Left || MenuNav.PagePrev)) SetTab(tab - 1);
                if (tab != 3 && (MenuNav.Right || MenuNav.PageNext)) SetTab(tab + 1);
                // controller Y / key T: QUIT TO TITLE (asks first)
                if ((g != null && g.buttonNorth.wasPressedThisFrame) || (k != null && k[Key.T].wasPressedThisFrame)) { confirmQuit = true; quitSel = 1; MenuNav.Click(); return; }
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
            tabSince = Time.unscaledTime;
            MenuNav.Click();
        }

        void SetOpen(bool o)
        {
            panel.CancelWait();
            Open = o;
            confirmQuit = false;
            if (o) { openSince = Time.unscaledTime; tabSince = openSince; MenuNav.Opened(); }
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

        bool confirmQuit;
        int quitSel = 1;
        float openSince, tabSince;      // menu / tab transitions (unscaled: the game is paused)

        /// <summary>QUIT TO TITLE (after YES): save, then load the scene again; GameFlow starts on the title screen.</summary>
        void QuitToTitle()
        {
            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
            RexLife.SaveQuitSpot();         // where Rex stands (calm spots only); before SetOpen: its PlayerPrefs.Save writes it
            SetOpen(false);                 // saves (Progress.Flush, PlayerPrefs.Save), sound back on
            Time.timeScale = 1f;            // no kill cam slow motion carried over
            if (scene < 0) return;          // scene not in the build list (editor test scene): just resume
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }

        /// <summary>"Quit to the title?" with YES / NO (mouse; keys and controller in Update).</summary>
        void DrawQuitConfirm(float w, float h, float s)
        {
            var r = new Rect(w * 0.5f - 420 * s, h * 0.3f, 840 * s, 330 * s);
            MenuSkin.DrawPanel(r);
            GUI.Label(new Rect(r.x + 30 * s, r.y + 24 * s, r.width - 60 * s, 160 * s),
                      "Quit to the title screen?\nYour progress is saved. You wake up here when you play again\n(at your last checkpoint if blobs are near).", MenuSkin.Question);
            string[] opts = { "YES, QUIT TO TITLE", "NO, GO BACK" };
            for (int i = 0; i < 2; i++)
            {
                var b = new Rect(r.x + 30 * s + i * 400 * s, r.yMax - 100 * s, 380 * s, 66 * s);
                bool hover = b.Contains(Event.current.mousePosition);
                if (hover && (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)) quitSel = i;
                if (Event.current.type == EventType.Repaint) button.Draw(b, opts[i], hover, false, quitSel == i, false);   // gold = chosen
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover)
                {
                    Event.current.Use();
                    MenuNav.Click();
                    if (i == 0) QuitToTitle(); else confirmQuit = false;
                    return;
                }
            }
        }

        void Styles(float s)
        {
            // the shared menu look (built once per screen height, not per OnGUI event)
            MenuSkin.Use(s);
            ui = s;
            title = MenuSkin.Title; header = MenuSkin.Header; label = MenuSkin.Label; small = MenuSkin.Small; button = MenuSkin.Button; tabStyle = MenuSkin.Tab;
        }

        void OnGUI()
        {
            if (!Open) return;
            GUI.depth = -100;            // on top of every other OnGUI (ending bars, door fade, boss bar, toasts)
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            Styles(s);
            // opening: the dim fades in and the menu rises 24 px into place (0.22 s); a new tab slides in from the right
            float oa = MenuSkin.Appear(openSince), ta = MenuSkin.Appear(tabSince);
            GUI.color = new Color(0, 0, 0, 0.72f * oa);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, oa);
            var lift = Matrix4x4.TRS(new Vector3(0f, (1f - oa) * 24 * s, 0f), Quaternion.identity, Vector3.one);
            GUI.matrix = lift;
            GUI.Label(new Rect(0, 20 * s, w, 50 * s), "PAUSED", title);
            if (confirmQuit) { DrawQuitConfirm(w, h, s); GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; return; }
            // ultrawide: the panel stops at ~1.9 x the screen height and stays centred (lists do not stretch apart)
            float pw = Mathf.Min(w * 0.84f, SettingsPanel.MaxWidth), px = (w - pw) * 0.5f;
            float tw = pw / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
                if (GUI.Toggle(new Rect(px + i * tw + 6, 80 * s, tw - 12, 50 * s), tab == i, Tabs[i], tabStyle) && tab != i) pendingTab = i;
            var area = new Rect(px, 150 * s, pw, h - 260 * s);
            MenuSkin.DrawPanel(new Rect(area.x - 16 * s, area.y - 12 * s, area.width + 32 * s, area.height + 24 * s));
            BottomButtons(w, h, s);
            if (!Open) { GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; return; }    // RESUME clicked (thumbs freed)
            // the tab's content: fade + 30 px slide (the panel and the buttons stay put)
            GUI.color = new Color(1f, 1f, 1f, oa * ta);
            GUI.matrix = lift * Matrix4x4.TRS(new Vector3((1f - ta) * 30 * s, 0f, 0f), Quaternion.identity, Vector3.one);
            if (tab == 3)
            {
                panel.Draw(area, s);
                GUI.matrix = Matrix4x4.identity;
                GUI.color = Color.white;
                return;
            }
            if (tab == 0)
            {
                // the island picture on the left, the lists beside it
                float m = Mathf.Min(area.height, area.width * 0.52f);
                DrawMap(new Rect(area.x, area.y, m, m), s);
                GUI.color = new Color(1f, 1f, 1f, oa * ta);       // DrawMap ends on white
                area = new Rect(area.x + m + 24 * s, area.y, area.width - m - 24 * s, area.height);
            }
            GUILayout.BeginArea(area);
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: MapTab(); break;
                case 1: AlbumTab(s, area.width, area.height); break;
                case 2: AccomplishmentsTab(); break;
                case 4: HelpTab(s, area.width); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
        }

        /// <summary>RESUME and QUIT TO TITLE under the panel (QUIT asks first). The keys / buttons show beside them.</summary>
        void BottomButtons(float w, float h, float s)
        {
            var dev = RMInput.LastDevice;
            string resume = dev == RMInput.DeviceKind.Gamepad ? "RESUME  " + Glyph.Token("B") : dev == RMInput.DeviceKind.Keyboard ? "RESUME  (Esc)" : "RESUME";
            string quit = dev == RMInput.DeviceKind.Gamepad ? "QUIT TO TITLE  " + Glyph.Token("Y") : dev == RMInput.DeviceKind.Keyboard ? "QUIT TO TITLE  (T)" : "QUIT TO TITLE";
            if (GUI.Button(new Rect(w * 0.5f - 350 * s, h - 90 * s, 340 * s, 60 * s), resume, button)) SetOpen(false);
            if (GUI.Button(new Rect(w * 0.5f + 10 * s, h - 90 * s, 340 * s, 60 * s), quit, button)) { confirmQuit = true; quitSel = 1; MenuNav.Click(); }
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
            if (!string.IsNullOrEmpty(objective) && GameGuide.Warning != null) GUILayout.Label("<color=#9CCBFF>" + GameGuide.Warning + "</color>", label);
            if (!string.IsNullOrEmpty(objective) && GameGuide.Side != null) GUILayout.Label("<color=#B8E6A0>" + GameGuide.Side + "</color>", label);
            GUILayout.Label("Red arrow = you.  Green = visited.  Yellow = next goal.", small);
            Section("ISLAND ZONES");
            foreach (var z in GameGuide.Zones)
                GUILayout.Label($"{(GameGuide.Visited(z.id) ? "[x]" : "[ ]")}  {z.name}", label);
            Section("BIG GOALS");
            GUILayout.Label($"{(FactoryCore.Stopped ? "[x]" : "[ ]")}  Stop the blob factory (ruins)", label);
            GUILayout.Label($"{(PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) == 1 ? "[x]" : "[ ]")}  Pop King Glorp (ship)", label);
            GUILayout.Label($"Blobs popped: {Progress.Value("kills")}    Guests rescued: {Progress.Value("rescues")}    Photos: {Progress.Value("photos")}", small);
            Section("CLUES");
            var clues = Progress.Clues;
            // the way to the boss is not overland (too steep): always said here once the factory is down
            if (FactoryCore.Stopped && PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) != 1)
                GUILayout.Label("- The cave tunnels go up to the ship: take the ship branch at the fork.", small);
            if (clues.Count == 0) GUILayout.Label("Rescue hiding guests: each one tells you a clue.", small);
            for (int i = clues.Count - 1; i >= 0; i--) GUILayout.Label("- " + clues[i], small);
        }

        /// <summary>A list section: space, a left-aligned gold header and a thin gold rule (the centred big title
        /// stays for the page itself: PAUSED, the count, the device name).</summary>
        void Section(string text)
        {
            GUILayout.Space(18 * ui);
            GUILayout.Label(text, header);
            var rule = GUILayoutUtility.GetRect(1f, 2f * ui, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                var old = GUI.color;
                GUI.color = new Color(MenuSkin.Gold.r, MenuSkin.Gold.g, MenuSkin.Gold.b, 0.45f * old.a);
                GUI.DrawTexture(new Rect(rule.x, rule.y, rule.width * 0.6f, rule.height), Texture2D.whiteTexture);
                GUI.color = old;
            }
            GUILayout.Space(6 * ui);
        }

        void HelpTab(float s, float width)
        {
            var dev = RMInput.LastDevice;
            Section(dev == RMInput.DeviceKind.Touch ? "TOUCH" : dev == RMInput.DeviceKind.Gamepad ? "CONTROLLER" : "KEYBOARD + MOUSE");
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

        void AlbumTab(float s, float width, float viewH)
        {
            var photos = Progress.Photos;
            if (photos.Count == 0) { GUILayout.Label($"No photos yet. Press {Glyph.B("Photo")} near a blob or a guest.", label); return; }
            int cols = 3;
            float cw = width / cols - 20, th = cw * 9f / 16f;
            // thumbnails load lazily: only rows near the view, at most 3 per frame (no hitch on a big album); grey until then
            float rowH = th + 70f * s, rowTop = 0f;
            for (int i = photos.Count - 1; i >= 0; i -= cols)
            {
                bool near = rowTop < scroll.y + viewH + rowH && rowTop + rowH > scroll.y - rowH;
                GUILayout.BeginHorizontal();
                for (int c = 0; c < cols && i - c >= 0; c++)
                {
                    var p = photos[i - c];
                    GUILayout.BeginVertical(GUILayout.Width(cw));
                    var t = Thumb(p.file, near);
                    var box = GUILayoutUtility.GetRect(cw, th, GUILayout.Width(cw), GUILayout.Height(th));
                    if (Event.current.type == EventType.Repaint)
                    {
                        if (t != null) GUI.DrawTexture(box, t, ScaleMode.ScaleToFit);
                        else
                        {
                            var old = GUI.color;
                            GUI.color = new Color(0.3f, 0.3f, 0.32f, old.a);
                            GUI.DrawTexture(box, Texture2D.whiteTexture);
                            GUI.color = old;
                        }
                    }
                    GUILayout.Label(p.caption, small);
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                rowTop += rowH;
            }
        }

        int thumbFrame, thumbLoads;

        Texture2D Thumb(string file, bool load)
        {
            if (thumbs.TryGetValue(file, out var t)) return t;
            if (!load) return null;
            if (thumbFrame != Time.frameCount) { thumbFrame = Time.frameCount; thumbLoads = 0; }
            if (thumbLoads >= 3) return null;             // the rest load in the next frames
            thumbLoads++;
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
            // the run in numbers first (the same list as the card after the ending)
            Section("STATS");
            foreach (var st in Progress.Stats())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(st.what, label, GUILayout.Width(300 * ui));
                GUILayout.Label(st.value, label);
                GUILayout.EndHorizontal();
            }
            foreach (var a in Progress.All)
            {
                if (a.group != group) { group = a.group; Section(group.ToUpper()); }
                bool ok = Progress.IsUnlocked(a.id);
                int v = Mathf.Min(Progress.Value(a.counter), a.need);
                GUILayout.Label($"{(ok ? "[x]" : "[ ]")}  {a.name} — {a.desc}{(ok || a.need <= 1 ? "" : $"  ({v}/{a.need})")}", ok ? label : small);
            }
        }
    }
}
