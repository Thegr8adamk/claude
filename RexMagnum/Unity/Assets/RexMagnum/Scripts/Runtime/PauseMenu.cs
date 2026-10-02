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
    /// MAP: FAST TRAVEL to the cart charge stations and the cave mouth in visited zones (Up / Down + OK, or click;
    /// Down past the last button scrolls on, PageUp / PageDown always scroll), only with no blob within 25 m and not in
    /// an event, the boss fight or the tutorial. A stop with blobs (or a blob spawner) near is greyed; the cave stop at
    /// night while the siege can start asks twice (warning).
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
                // PageUp / PageDown always scroll (also past the MAP's travel buttons)
                if (tab != 3 && k != null && k[Key.PageUp].wasPressedThisFrame) scroll.y = Mathf.Max(0f, scroll.y - 420f * Screen.height / 1080f);
                if (tab != 3 && k != null && k[Key.PageDown].wasPressedThisFrame) scroll.y += 420f * Screen.height / 1080f;
                if (tab == 0 && travelOk && travel.Count > 0 && travelSel < travel.Count)
                {
                    // MAP with fast travel: Up / Down pick a TRAVEL button, OK goes (the stick still scrolls). Down
                    // past the last button goes on to scrolling the lists below (zones, big goals, clues)
                    if (MenuNav.Up && travelSel > 0) { travelSel--; travelConfirm = -1; MenuNav.Click(); }
                    if (MenuNav.Down) { travelSel++; travelConfirm = -1; if (travelSel >= travel.Count) scroll.y += 140f * Screen.height / 1080f; MenuNav.Click(); }
                    if (travelSel < travel.Count && MenuNav.Ok)
                    {
                        MenuNav.Consume(); pendingTravel = travelSel;
                        if (travel[travelSel].why != null) MenuNav.Click();      // greyed: a click and a notice (StartTravel), not silence
                    }
                }
                else
                {
                    // the MAP scrolled back to the top: Up picks the last TRAVEL button again
                    if (tab == 0 && travelOk && travel.Count > 0 && MenuNav.Up && scroll.y <= 0.5f) { travelSel = travel.Count - 1; MenuNav.Click(); }
                    else if (tab != 3 && MenuNav.Up) scroll.y = Mathf.Max(0f, scroll.y - 140f * Screen.height / 1080f);
                    if (tab != 3 && MenuNav.Down) scroll.y += 140f * Screen.height / 1080f;
                }
                if (tab != 3 && (MenuNav.Left || MenuNav.PagePrev)) SetTab(tab - 1);
                if (tab != 3 && (MenuNav.Right || MenuNav.PageNext)) SetTab(tab + 1);
                // controller Y / key T: QUIT TO TITLE (asks first)
                if ((g != null && g.buttonNorth.wasPressedThisFrame) || (k != null && k[Key.T].wasPressedThisFrame)) { confirmQuit = true; quitSel = 1; MenuNav.Click(); return; }
                if (RMInput.Pause && !panel.Waiting && !MenuNav.Consumed) SetOpen(false);
                return;
            }
            if (GameFlow.Busy || traveling) return;
            if (RMInput.Pause) SetOpen(true);
        }

        int pendingTab = -1;
        float scaleBefore = 1f;
        bool rexHadInput = true;

        void LateUpdate()
        {
            // a tab clicked in OnGUI changes here, between GUI passes (changing it mid-event breaks the IMGUI layout)
            if (pendingTab >= 0) { SetTab(pendingTab); pendingTab = -1; }
            if (pendingTravel >= 0) { int go = pendingTravel; pendingTravel = -1; StartTravel(go); }
        }

        void SetTab(int t)
        {
            tab = (t + Tabs.Length) % Tabs.Length;
            scroll = Vector2.zero;
            travelSel = 0; travelConfirm = -1;      // back on the MAP: the first TRAVEL button, the list at the top
            if (tab == 0 && Open) BuildTravel();    // the stops again (blobs come and go while the other tabs were up)
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
            statsCache = null;           // the stats list is built once per opening (not per OnGUI call)
            if (o) { GameFlow.FlushPlayTime(); BuildTravel(); }      // the play time in the stats is up to date
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
            GameFlow.FlushPlayTime();       // the unsaved play seconds (SetOpen's Progress.Flush writes them)
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
            if (travelFade > 0.001f && !PhotoAlbum.Capturing)
            {
                // fast travel: the screen fades to black and back (the menu is closed by then)
                GUI.depth = -100;
                GUI.color = new Color(0, 0, 0, travelFade);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
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
            // fast travel stops: small white pins (greyed while blocked)
            foreach (var ts in travel)
                Dot(P(ts.at), 9 * s, ts.why == null ? Color.white : new Color(0.6f, 0.6f, 0.6f));
            var old = GUI.matrix;
            if (objectiveHas && GameGuide.SideHasTarget)
            {
                // a side event's spot (the cave siege at night): a smaller blue diamond
                var sp = P(GameGuide.SideTarget);
                GUIUtility.RotateAroundPivot(45f, sp);
                Dot(sp, 15 * s, new Color(0.35f, 0.7f, 1f));
                GUI.matrix = old;
            }
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
            // "Blue" only while a blue marker is on the map (the cave siege's side target)
            GUILayout.Label(objectiveHas && GameGuide.SideHasTarget ? "Red arrow = you.  Green = visited.  Yellow = next goal.  Blue = night event.  White = fast travel."
                                                    : "Red arrow = you.  Green = visited.  Yellow = next goal.  White = fast travel.", small);
            TravelSection();
            Section("ISLAND ZONES");
            foreach (var z in GameGuide.Zones)
                GUILayout.Label($"{(GameGuide.Visited(z.id) ? "[x]" : "[ ]")}  {z.name}", label);
            Section("BIG GOALS");
            GUILayout.Label($"{(FactoryCore.Stopped ? "[x]" : "[ ]")}  Stop the blob factory (ruins)", label);
            GUILayout.Label($"{(PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) == 1 ? "[x]" : "[ ]")}  Pop King Glorp (ship)", label);
            GUILayout.Label($"Blobs popped: {Progress.Value("kills")}    Guests rescued: {Progress.Value("rescues")}    Photos: {Progress.Value("photos")}", small);
            AbilitiesSection();
            Section("CLUES");
            var clues = Progress.Clues;
            // the way to the boss is not overland (too steep): always said here once the factory is down
            if (FactoryCore.Stopped && PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) != 1)
                GUILayout.Label("- The cave tunnels go up to the ship: take the ship branch at the fork.", small);
            if (clues.Count == 0) GUILayout.Label("Rescue hiding guests: each one tells you a clue.", small);
            for (int i = clues.Count - 1; i >= 0; i--) GUILayout.Label("- " + clues[i], small);
        }

        /// <summary>MAP + HELP: the 3 abilities, unlocked or how to earn them, and how to use them.</summary>
        void AbilitiesSection()
        {
            Section("ABILITIES");
            foreach (var ab in GameGuide.AbilitiesCached())
            {
                GUILayout.Label(ab.on ? $"[x]  {ab.name}" : $"[ ]  {ab.name}  (locked: {ab.earn})", ab.on ? label : small);
                GUILayout.Label("      " + ab.use, small);
            }
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
            AbilitiesSection();
            GUILayout.Space(16);
            GUILayout.Label("Keys and buttons: Settings > CONTROLS.  The NEXT line and the compass show a goal; the MAP tab shows where you are.", small);
        }

        void AlbumTab(float s, float width, float viewH)
        {
            var photos = Progress.Photos;
            if (photos.Count == 0) { GUILayout.Label($"No photos yet. Press {Glyph.B("Photo")} near a blob or a guest.", label); return; }
            int cols = 3;
            float cw = width / cols - 20, th = cw * 9f / 16f;
            // thumbnails load lazily: only rows near the view, at most 1 per frame (a JPG decode each) (no hitch on a big album); grey until then
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
            if (thumbLoads >= 1) return null;             // the rest load in the next frames
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

        List<(string what, string value)> statsCache;

        // ------------------------------------------------------------ fast travel (MAP tab)

        /// <summary>why: this stop is off now (blobs there), else null. siege: the cave stop while the night siege can start.</summary>
        struct TravelSpot { public string name, why; public Vector3 at; public float yaw; public bool siege; }
        readonly List<TravelSpot> travel = new List<TravelSpot>();
        bool travelOk, traveling;
        string travelWhy;
        int travelSel, pendingTravel = -1, travelConfirm = -1;
        float travelFade, travelNoteUntil;

        /// <summary>The fast travel stops when the menu opens (nothing moves while paused): per visited zone the cart
        /// charge station nearest its landmark (pool deck, pro shop, factory door), and the cave mouth (the cave door's
        /// outside exit). Each stop is a free spot ~3 m beside the station, on the ground, clear of the parked cart.</summary>
        void BuildTravel()
        {
            travel.Clear();
            travelSel = 0;
            pendingTravel = -1;
            travelConfirm = -1;
            travelWhy = TravelBlock(FindAnyObjectByType<RexController>());
            travelOk = travelWhy == null;
            var best = new Dictionary<string, Vector3>();
            var bestD = new Dictionary<string, float>();
            foreach (var cs in RMFind.All<ChargeStation>())
            {
                Vector3 p = cs.transform.position;
                string zone = StopZone(p, out Vector3 mark);
                if (!GameGuide.Visited(zone)) continue;
                Vector3 f = p - mark; f.y = 0f;
                if (bestD.TryGetValue(zone, out float d) && d <= f.sqrMagnitude) continue;
                best[zone] = p; bestD[zone] = f.sqrMagnitude;
            }
            foreach (var z in GameGuide.Zones)       // the map's zone order (resort first)
                if (best.TryGetValue(z.id, out Vector3 station))
                {
                    Vector3 at = FreeSpot(station, out float yaw);
                    travel.Add(new TravelSpot { name = StopName(z.id), at = at, yaw = yaw, why = StopBlock(at) });
                }
            if (GameGuide.Visited("cave"))
            {
                var ex = GameObject.Find("Cave_ExitA");        // outside the cave door, facing away from it (IslandBuilder.DoorPair)
                if (ex != null)
                {
                    Vector3 cave = CaveStop(ex.transform);
                    bool siege = GameGuide.CaveSiegeOpen();
                    travel.Add(new TravelSpot { name = "Cave mouth (north-west)" + (siege ? "  (night siege!)" : ""), at = cave,
                                                yaw = ex.transform.eulerAngles.y, why = StopBlock(cave), siege = siege });
                }
            }
        }

        static string SiegeWarning => NightSiege.Hint ?? "The cave is besieged at night: fight, or come back at dawn.";

        /// <summary>Why this stop is off right now (Rex would land among blobs), or null: a living blob within 25 m,
        /// or within reach of an active blob spawner (radius + 5 m: it would fill up around him).</summary>
        static string StopBlock(Vector3 at)
        {
            // a living blob within 25 m on the same floor (6 m up or down: not the hotel room over the lobby), and not
            // a disguised one (it looks like a guest: a greyed stop would give it away)
            foreach (var b in Blob.All)
            {
                if (b == null || b.IsDead || b.Disguised) continue;
                Vector3 d = b.transform.position - at;
                if (Mathf.Abs(d.y) < 6f && d.sqrMagnitude < 25f * 25f) return "blobs there";
            }
            foreach (var sp in RMFind.All<BlobSpawner>())
            {
                if (sp == null || !sp.isActiveAndEnabled || sp.prefabs == null || sp.prefabs.Length == 0) continue;
                Vector3 f = sp.transform.position - at; f.y = 0f;
                float r = sp.radius + 5f;
                if (f.sqrMagnitude < r * r && Mathf.Abs(sp.transform.position.y - at.y) < 20f) return "blobs there";
            }
            return null;
        }

        /// <summary>The cave stop: ~15 m out from the cave door along its way out (the exit faces away from the door),
        /// on the ground and the NavMesh, clear of the night siege's fight right at the mouth. Else the exit itself.</summary>
        static Vector3 CaveStop(Transform exit)
        {
            Vector3 fwd = exit.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) return exit.position;
            fwd.Normalize();
            for (float d = 15f; d >= 5f; d -= 5f)
            {
                Vector3 p = exit.position + fwd * d;
                if (!Physics.Raycast(p + Vector3.up * 15f, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.point.y - exit.position.y) > 6f) continue;            // a cliff or a ledge on the way
                if (!UnityEngine.AI.NavMesh.SamplePosition(hit.point, out UnityEngine.AI.NavMeshHit nav, 2f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                if (WaterVolume.Depth(nav.position + Vector3.up * 0.1f) > 0.2f) continue;  // the waterfall pool
                return nav.position + Vector3.up * 0.1f;
            }
            return exit.position;
        }

        /// <summary>Why fast travel is off right now, or null.</summary>
        static string TravelBlock(RexController rex)
        {
            if (rex == null) return "No fast travel here.";
            var hp = rex.GetComponent<Health>();
            if (hp != null && hp.IsDead) return "No fast travel now.";
            if (GameFlow.Busy || GameFlow.TutorialActive) return "Fast travel opens after the room tutorial.";
            if (KingGlorpBoss.Fighting || EndingSequence.Playing) return "No fast travel during the boss fight.";
            if (GameGuide.EventRunning) return "No fast travel during an event. Finish it first.";
            if (GolfCart.Driven != null) return "Get out of the golf cart to fast travel.";
            if (Time.time < PhotoAlbum.PoseUntil || PhotoAlbum.Capturing) return "No fast travel during a photo.";
            if (Blob.LivingNear(rex.transform.position, 25f) > 0) return "Blobs nearby: no fast travel with a blob within 25 m.";
            return null;
        }

        /// <summary>The zone a charge station belongs to (nearest zone centre; the resort's hotel / pool / beach count as
        /// the pool) and the landmark its stop should be nearest.</summary>
        static string StopZone(Vector3 p, out Vector3 mark)
        {
            var zones = GameGuide.Zones;
            int near = 0;
            float nd = float.MaxValue;
            for (int i = 0; i < zones.Length; i++)
            {
                Vector3 f = p - zones[i].at; f.y = 0f;
                if (f.sqrMagnitude < nd) { nd = f.sqrMagnitude; near = i; }
            }
            string id = zones[near].id;
            if (id == "hotel" || id == "beach") id = "pool";
            mark = id == "pool" ? zones[1].at : id == "golf" ? GameGuide.ProShop : id == "ruins" ? GameGuide.Factory : zones[near].at;
            return id;
        }

        static string StopName(string zone)
        {
            switch (zone)
            {
                case "pool": return "Resort: pool deck";
                case "golf": return "Golf course: pro shop";
                case "ruins": return "Jungle ruins: factory door";
            }
            foreach (var z in GameGuide.Zones) if (z.id == zone) return z.name;
            return zone;
        }

        /// <summary>A spot ~3 m around a charge station on the ground at its height, with room for Rex (not in the
        /// parked cart or a wall); Rex faces the station. Else just on it.</summary>
        static Vector3 FreeSpot(Vector3 station, out float yaw)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.back;
                Vector3 p = station + dir * 3f;
                if (!Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)) continue;
                p = hit.point + Vector3.up * 0.1f;
                if (Mathf.Abs(p.y - station.y) > 1.5f) continue;          // a ledge, a roof, a ditch
                if (Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                yaw = Quaternion.LookRotation(-dir).eulerAngles.y;
                return p;
            }
            yaw = 0f;
            return station + Vector3.up * 0.1f;
        }

        /// <summary>MAP tab: the FAST TRAVEL buttons (mouse, or Up / Down + OK), or why it is off.</summary>
        void TravelSection()
        {
            Section("FAST TRAVEL");
            if (travel.Count == 0) { GUILayout.Label("Find the golf cart charge stations (resort, golf course, ruins) and the cave: you can travel there from here.", small); return; }
            if (!travelOk) GUILayout.Label("<color=#FF9C80>" + travelWhy + "</color>", small);
            else if (Time.unscaledTime < travelNoteUntil) GUILayout.Label("<color=#FF9C80>Blobs at that stop. Pick another one, or clear them first.</color>", small);
            // the cave stop at night (siege can start): one more OK first, with the warning
            if (travelOk && travelConfirm >= 0 && travelConfirm < travel.Count)
                GUILayout.Label("<color=#9CCBFF>" + SiegeWarning + "  Press TRAVEL again to go.</color>", small);
            for (int i = 0; i < travel.Count; i++)
            {
                var r = GUILayoutUtility.GetRect(10f, 54f * ui, GUILayout.ExpandWidth(true));
                bool can = travelOk && travel[i].why == null;
                bool hover = travelOk && r.Contains(Event.current.mousePosition);
                if (hover && travelSel != i && (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)) { travelSel = i; travelConfirm = -1; }
                if (Event.current.type == EventType.Repaint)
                {
                    var old = GUI.color;
                    if (!can) GUI.color = new Color(old.r, old.g, old.b, old.a * 0.45f);      // greyed: not now
                    string text = "TRAVEL:  " + travel[i].name + (travel[i].why != null ? "  (" + travel[i].why + ")" : "");
                    button.Draw(r, travelConfirm == i ? "AGAIN TO GO:  " + travel[i].name : text, hover, false, travelOk && travelSel == i && MenuNav.PadOrKeys, false);
                    GUI.color = old;
                }
                if (travelOk && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover)
                {
                    Event.current.Use();
                    MenuNav.Click();
                    pendingTravel = i;      // in LateUpdate: closing the menu mid-layout breaks IMGUI
                }
                GUILayout.Space(4 * ui);
            }
        }

        void StartTravel(int i)
        {
            if (i < 0 || i >= travel.Count || !travelOk || traveling || !Open) return;
            var spot = travel[i];
            // greyed: a notice in the menu (the HUD's notice is under the paused menu and waits)
            if (spot.why != null) { travelNoteUntil = Time.unscaledTime + 3f; return; }
            // into the night siege: a warning and one more OK first
            if (spot.siege && travelConfirm != i) { travelConfirm = i; return; }
            travelConfirm = -1;
            var rex = FindAnyObjectByType<RexController>();
            if (rex == null) return;
            SetOpen(false);
            StartCoroutine(Travel(rex, spot));
        }

        /// <summary>Short fade out, Rex moved (Teleport turns the CharacterController off while he moves), the camera
        /// behind him, 2 s untouchable from the arrival, fade in. Unscaled time: a kill cam's slow motion does not stretch it.</summary>
        System.Collections.IEnumerator Travel(RexController rex, TravelSpot spot)
        {
            traveling = true;
            var hp = rex.GetComponent<Health>();
            if (hp != null) hp.Invulnerable(2f);
            rex.InputEnabled = false;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f) { travelFade = t; yield return null; }
            travelFade = 1f;
            if (rex != null)
            {
                rex.Teleport(spot.at, spot.yaw);
                if (rex.cameraRig != null) rex.cameraRig.SetYaw(spot.yaw);
                if (hp != null) hp.Invulnerable(2f);
            }
            NightSiege.Breather(60f);       // a breather after the arrival: the night siege does not start while the screen fades in
            yield return new WaitForSecondsRealtime(0.15f);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.4f) { travelFade = t; yield return null; }
            travelFade = 0f;
            if (spot.siege) RexTestHUD.Notice(SiegeWarning, 5f);
            // input back as the menu's resume gives it (not at the cart's wheel, not in the ending)
            if (rex != null && GolfCart.Driven == null && !Open && !EndingSequence.Playing) rex.InputEnabled = true;
            traveling = false;
        }

        void AccomplishmentsTab()
        {
            string group = null;
            int done = 0;
            foreach (var a in Progress.All) if (Progress.IsUnlocked(a.id)) done++;
            GUILayout.Label($"{done} / {Progress.All.Length}", title);
            // the run in numbers first (the same list as the card after the ending)
            Section("STATS");
            if (statsCache == null) statsCache = Progress.Stats();
            foreach (var st in statsCache)
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
