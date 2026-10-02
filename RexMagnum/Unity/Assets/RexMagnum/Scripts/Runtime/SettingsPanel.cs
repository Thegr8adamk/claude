using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RexMagnum
{
    /// <summary>
    /// The settings screen, shared by the title screen and the pause menu. Five pages:
    /// GRAPHICS (quality, render scale, frame limit, V-sync, full screen, field of view),
    /// CAMERA &amp; VR (first / third person, head bob, look speeds, invert, kill cam, VR mode, VR turning),
    /// SOUND (master, music, voices, effects, subtitles),
    /// CONTROLS (keyboard keys, controller buttons, controller and touch options),
    /// HELP (the controls of the device used last, from GameGuide.Controls).
    /// Mouse: click. Keyboard / controller: Up/Down choose a line, Left/Right change it, Enter / A press it,
    /// Q/E or LB/RB change the page, Esc / B go back. Everything is saved in PlayerPrefs at once.
    /// </summary>
    public class SettingsPanel
    {
        public static readonly string[] Pages = { "GRAPHICS", "CAMERA & VR", "SOUND", "CONTROLS", "HELP" };
        public const int HelpPage = 4;
        public int page;
        /// <summary>Set by the pause menu: close the menu so the player can drag the touch buttons.</summary>
        public Action editTouchLayout;

        int sel = -1;
        Vector2 scroll;
        bool scrollToSel;
        float selY, selH, viewH;
        string waitKey, waitPad;
        int waitFrame;
        public bool Waiting => waitKey != null || waitPad != null;
        /// <summary>The menu closed (mouse BACK / RESUME) while a rebind waited: stop waiting.</summary>
        public void CancelWait() { waitKey = null; waitPad = null; }

        class Row
        {
            public string label, note;
            public bool header;
            public string[] opts; public Func<int> get; public Action<int> set;
            public float min, max; public Func<float> getF; public Action<float> setF; public Func<float, string> fmt;
            public Func<string> pressText; public Action press;
            public string info;                     // HELP: read-only "label   value" line
            public bool Selectable => !header && note == null && info == null;
        }

        readonly List<Row> rows = new List<Row>();

        // ------------------------------------------------------------ apply all saved settings

        public static void ApplyAll()
        {
            RMInput.LoadOptions();
            RMInput.MouseSensitivity = PlayerPrefs.GetFloat("RexMagnum_LookSpeed", 0.08f);
            RMInput.StickSensitivity = PlayerPrefs.GetFloat("RexMagnum_StickSpeed", 180f);
            RMInput.LookScaleX = PlayerPrefs.GetFloat("RexMagnum_LookX", 1f);
            RMInput.LookScaleY = PlayerPrefs.GetFloat("RexMagnum_LookY", 1f);
            RMInput.InvertY = PlayerPrefs.GetInt("RexMagnum_InvertY", 0) == 1;
            RexCameraRig.FovSetting = PlayerPrefs.GetFloat("RexMagnum_FOV", 65f);
            AudioMix.Load();
        }

        // ------------------------------------------------------------ rows

        void Header(string t) => rows.Add(new Row { header = true, label = t });
        void Note(string t) => rows.Add(new Row { note = t });
        void Info(string label, string value) => rows.Add(new Row { label = label, info = value });
        void Choice(string label, string[] opts, Func<int> get, Action<int> set) =>
            rows.Add(new Row { label = label, opts = opts, get = get, set = set });
        void Toggle(string label, string key, int def, Action after = null) =>
            Choice(label, new[] { "OFF", "ON" }, () => PlayerPrefs.GetInt(key, def),
                   v => { PlayerPrefs.SetInt(key, v); ApplyAll(); after?.Invoke(); });
        void Slider(string label, string key, float def, float min, float max, Func<float, string> fmt, Action after = null) =>
            rows.Add(new Row { label = label, min = min, max = max, fmt = fmt, getF = () => PlayerPrefs.GetFloat(key, def),
                               setF = v => { PlayerPrefs.SetFloat(key, v); ApplyAll(); after?.Invoke(); } });
        void Button(string label, Func<string> text, Action press) =>
            rows.Add(new Row { label = label, pressText = text, press = press });

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

        static void ReapplyQuality()
        {
            var qa = UnityEngine.Object.FindAnyObjectByType<QualityAuto>();
            if (qa != null) qa.SetFromSettings(PlayerPrefs.GetInt(QualityAuto.PrefKey, -1));
            else QualityAuto.ApplyDisplay();
        }

        void Build()
        {
            rows.Clear();
            switch (page)
            {
                case 0:
                    Header("GRAPHICS");
                    Choice("Quality", new[] { "AUTO", "LOW", "MEDIUM", "HIGH", "ULTRA" },
                        () => PlayerPrefs.GetInt(QualityAuto.PrefKey, -1) + 1,
                        v => { PlayerPrefs.SetInt(QualityAuto.PrefKey, v - 1); ReapplyQuality(); });
                    Note("AUTO picks from this computer: " + QualityAuto.Detect().ToString().ToUpper() + ".");
                    Choice("Render scale", QualityAuto.RenderScaleNames, () => PlayerPrefs.GetInt("RexMagnum_RenderScale", 0),
                        v => { PlayerPrefs.SetInt("RexMagnum_RenderScale", v); ReapplyQuality(); });
                    Choice("Frame limit (V-sync off)", QualityAuto.FpsNames, () => PlayerPrefs.GetInt("RexMagnum_FpsLimit", QualityAuto.FpsDefault),
                        v => { PlayerPrefs.SetInt("RexMagnum_FpsLimit", v); QualityAuto.ApplyDisplay(); });
                    Choice("V-sync (no tearing)", new[] { "OFF", "ON" }, () => PlayerPrefs.GetInt("RexMagnum_VSync", Application.isMobilePlatform ? 0 : 1),
                        v => { PlayerPrefs.SetInt("RexMagnum_VSync", v); QualityAuto.ApplyDisplay(); });
                    if (!Application.isMobilePlatform)
                        Choice("Display", new[] { "WINDOW", "FULL SCREEN" }, () => PlayerPrefs.GetInt("RexMagnum_Fullscreen", 1),
                            v => { PlayerPrefs.SetInt("RexMagnum_Fullscreen", v); QualityAuto.ApplyDisplay(); });
                    Slider("Field of view", "RexMagnum_FOV", 65f, 55f, 95f, v => Mathf.RoundToInt(v) + "°");
                    break;

                case 1:
                    Header("CAMERA");
                    Choice("View", new[] { "THIRD PERSON", "FIRST PERSON" }, ViewIndex, SetView);
                    Note($"In the game, {RMInput.Btn("Camera")} switches the view at any time.");
                    Toggle("Head bob (first person)", "RexMagnum_HeadBob", 1);
                    Slider("Mouse look speed", "RexMagnum_LookSpeed", 0.08f, 0.02f, 0.25f, v => v.ToString("0.00"));
                    Slider("Controller look speed", "RexMagnum_StickSpeed", 180f, 60f, 360f, v => Mathf.RoundToInt(v).ToString());
                    Slider("Look speed left/right", "RexMagnum_LookX", 1f, 0.3f, 2f, v => v.ToString("0.0") + "x");
                    Slider("Look speed up/down", "RexMagnum_LookY", 1f, 0.3f, 2f, v => v.ToString("0.0") + "x");
                    Toggle("Invert up/down", "RexMagnum_InvertY", 0);
                    Choice("Kill cam", new[] { "OFF", "LESS OFTEN", "ALWAYS" }, () => PlayerPrefs.GetInt("RexMagnum_KillCam", 1),
                        v => PlayerPrefs.SetInt("RexMagnum_KillCam", v));
                    Header("VR");
                    Choice("VR mode", new[] { "OFF", "ON" }, () => VRMode.Wanted ? 1 : 0, v => VRMode.Set(v == 1));
                    Note(VRMode.Status);
                    Choice("VR turning", new[] { "SMOOTH", "SNAP 30°" }, () => PlayerPrefs.GetInt(VRMode.SnapKey, 1),
                        v => PlayerPrefs.SetInt(VRMode.SnapKey, v));
                    Button("Recenter the view", () => "RECENTER", VRMode.Recenter);
                    Note("VR needs a PC headset (Quest Link, SteamVR or Windows Mixed Reality). VR is always first person. " +
                         "The menus show on the PC screen: use the controller.");
                    break;

                case 2:
                    Header("SOUND");
                    Slider("Master volume", "RexMagnum_Volume", 1f, 0f, 1f, Pct);
                    Slider("Music", "RexMagnum_MusicVol", 1f, 0f, 1f, Pct);
                    Slider("Voices", "RexMagnum_VoiceVol", 1f, 0f, 1f, Pct);
                    Slider("Sound effects", "RexMagnum_SfxVol", 1f, 0f, 1f, Pct);
                    Toggle("Subtitles", "RexMagnum_Subtitles", 1);
                    break;

                case 3:
                    Header("KEYBOARD");
                    Note("Choose an action, then press the new key. If the key is in use, the two actions swap.");
                    foreach (var a in RMInput.Actions)
                    {
                        string act = a;
                        Button(Nice(act), () => waitKey == act ? "press a key..." : RMInput.KeyFor(act).ToString(),
                               () => { waitKey = act; waitFrame = Time.frameCount; });
                    }
                    Button("Reset keyboard", () => "RESET", RMInput.ResetKeys);
                    Header("CONTROLLER");
                    Note(Gamepad.current == null ? "No controller found. Connect an Xbox, PlayStation or Switch Pro controller (USB or Bluetooth)."
                                                 : "Controller: " + Gamepad.current.displayName + ". Choose an action, then press the new button.");
                    foreach (var a in RMInput.PadActions)
                    {
                        string act = a;
                        Button(Nice(act), () => waitPad == act ? "press a button..." : RMInput.PadName(RMInput.PadFor(act)),
                               () => { if (Gamepad.current != null) { waitPad = act; waitFrame = Time.frameCount; } });
                    }
                    Note("Fixed: RT action, LT aim, RB / LB next / previous weapon, sticks move and look.");
                    Button("Reset controller buttons", () => "RESET", RMInput.ResetPad);
                    Toggle("Swap sticks (left-handed)", "RexMagnum_SwapSticks", 0);
                    Toggle("Swap triggers and bumpers", "RexMagnum_SwapTriggers", 0);
                    Toggle("Aim assist (controller + phone)", "RexMagnum_AimAssist", 1);
                    Toggle("Controller vibration", "RexMagnum_Rumble", 1);
                    Header("HOLD OR TAP");
                    Toggle("Run: tap to toggle", "RexMagnum_RunToggle", 0);
                    Toggle("Aim: tap to toggle", "RexMagnum_AimToggle", 0);
                    Toggle("Hands down: hold", "RexMagnum_HandsHold", 0);
                    if (Application.isMobilePlatform || Touchscreen.current != null)
                    {
                        Header("TOUCH BUTTONS");
                        if (editTouchLayout != null)
                            Button("Move and resize the buttons", () => "EDIT", () => { TouchControls.EditMode = true; editTouchLayout(); });
                        Button("Touch layout", () => "RESET", TouchControls.ResetLayout);
                    }
                    Button("All controller + touch options", () => "RESET", () =>
                    {
                        foreach (var k in new[] { "SwapSticks", "SwapTriggers", "AimAssist", "Rumble", "RunToggle", "AimToggle", "HandsHold" })
                            PlayerPrefs.DeleteKey("RexMagnum_" + k);
                        ApplyAll();
                    });
                    break;

                case HelpPage:
                    var dev = RMInput.LastDevice;
                    Header(dev == RMInput.DeviceKind.Touch ? "HELP: TOUCH" : dev == RMInput.DeviceKind.Gamepad ? "HELP: CONTROLLER" : "HELP: KEYBOARD + MOUSE");
                    Note("These follow the device you used last, and your own keys (CONTROLS page).");
                    foreach (var c in GameGuide.Controls()) Info(c.what, c.how);
                    Header("HOW TO PLAY");
                    Note("The whole island is open. The NEXT line and the compass at the top of the screen show a goal. " +
                         "Pause > MAP shows where you are, the zones you visited and the clues from rescued guests.");
                    Note("Hands down: guests talk to you. Rescue hiding guests when no blob is near. Food and drinks heal. " +
                         "If you die, you wake up in room 304 and keep your weapons.");
                    break;
            }
            if (sel < 0 || sel >= rows.Count || !rows[sel].Selectable) sel = Next(-1, 1);
        }

        static string Nice(string action)
        {
            switch (action)
            {
                case "Use": return "Use / pick up";
                case "Hands": return "Hands down / up";
                case "Camera": return "First / third person";
                case "Shoulder": return "Swap shoulder";
                case "NextWeapon": return "Next weapon";
                case "PrevWeapon": return "Previous weapon";
                case "Wheel": return "Weapon wheel";
                default: return action;
            }
        }

        static int ViewIndex()
        {
            var rig = UnityEngine.Object.FindAnyObjectByType<RexCameraRig>();
            if (rig != null && rig.isActiveAndEnabled) return rig.Mode == RexCameraRig.CameraMode.FirstPerson ? 1 : 0;   // in game: what is on screen
            int def = PlayerPrefs.GetInt("RexMagnum_CamDefault", 2);       // 0 FP, 1 TP, 2 remember last
            bool fp = def == 0 || (def == 2 && PlayerPrefs.GetInt("RexMagnum_CameraMode", 1) == 0);
            return fp ? 1 : 0;
        }

        static void SetView(int v)
        {
            bool fp = v == 1;
            PlayerPrefs.SetInt("RexMagnum_CamDefault", fp ? 0 : 1);
            PlayerPrefs.SetInt("RexMagnum_CameraMode", fp ? 0 : 1);
            var rig = UnityEngine.Object.FindAnyObjectByType<RexCameraRig>();
            if (rig != null) rig.SetModeFromSettings(fp ? RexCameraRig.CameraMode.FirstPerson : RexCameraRig.CameraMode.ThirdPerson);
        }

        int Next(int from, int d)
        {
            for (int k = 1; k <= rows.Count; k++)
            {
                int i = ((from + d * k) % rows.Count + rows.Count) % rows.Count;
                if (rows[i].Selectable) return i;
            }
            return -1;
        }

        void SetPage(int p)
        {
            page = (p + Pages.Length) % Pages.Length;
            sel = -1;
            scroll = Vector2.zero;
        }

        // ------------------------------------------------------------ input (call from Update)

        /// <summary>Keyboard / controller input for the panel. Returns true when the player goes back (Esc / B).</summary>
        public bool Update()
        {
            if (waitPad != null)
            {
                if (Time.frameCount == waitFrame) return false;
                var g = Gamepad.current;
                if (g == null) { waitPad = null; return false; }
                foreach (var b in RMInput.PadChoices)
                    if (g[b].wasPressedThisFrame) { RMInput.RebindPad(waitPad, b); waitPad = null; MenuNav.Consume(); break; }
                return false;
            }
            if (waitKey != null)
            {
                if (Time.frameCount == waitFrame) return false;
                var gp = Gamepad.current;      // a controller player chose a keyboard row: B / Start cancels the wait
                if (gp != null && (gp.buttonEast.wasPressedThisFrame || gp.startButton.wasPressedThisFrame))
                { waitKey = null; MenuNav.Consume(); return false; }
                var k = Keyboard.current;
                if (k == null) { waitKey = null; return false; }
                foreach (var key in k.allKeys)
                    if (key != null && key.wasPressedThisFrame)
                    {
                        if (key.keyCode != Key.Escape || waitKey == "Pause") RMInput.Rebind(waitKey, key.keyCode);
                        waitKey = null;
                        MenuNav.Consume();
                        break;
                    }
                return false;
            }
            Build();
            if (MenuNav.PagePrev) { SetPage(page - 1); return false; }
            if (MenuNav.PageNext) { SetPage(page + 1); return false; }
            if (page == HelpPage)
            {
                // nothing to choose on HELP: Up / Down scroll the list
                if (MenuNav.Up) scroll.y = Mathf.Max(0f, scroll.y - 120f);
                if (MenuNav.Down) scroll.y += 120f;
            }
            if (MenuNav.Up) { sel = Next(sel, -1); scrollToSel = true; }
            if (MenuNav.Down) { sel = Next(sel, 1); scrollToSel = true; }
            var r = sel >= 0 && sel < rows.Count ? rows[sel] : null;
            if (r != null)
            {
                int d = MenuNav.Left ? -1 : MenuNav.Right ? 1 : 0;
                if (d != 0 && r.opts != null) r.set(Mathf.Clamp(r.get() + d, 0, r.opts.Length - 1));
                else if (d != 0 && r.getF != null) r.setF(Mathf.Clamp(r.getF() + d * (r.max - r.min) / 20f, r.min, r.max));
                if (MenuNav.Ok)
                {
                    MenuNav.Consume();
                    if (r.press != null) r.press();
                    else if (r.opts != null) r.set((r.get() + 1) % r.opts.Length);
                }
            }
            if (MenuNav.Back) { MenuNav.Consume(); return true; }
            return false;
        }

        // ------------------------------------------------------------ drawing (call from OnGUI)

        GUIStyle hdr, lbl, small, btn, rowSel, hdrValue;
        float styleScale = -1f;

        void Styles(float s)
        {
            // the shared menu look (MenuSkin builds them once per screen height)
            MenuSkin.Use(s);
            styleScale = s;
            hdr = MenuSkin.Header; lbl = MenuSkin.LabelRow; hdrValue = MenuSkin.Value; small = MenuSkin.Small;
            btn = MenuSkin.Option; rowSel = MenuSkin.RowSel;
        }

        public void Draw(Rect area, float s)
        {
            Styles(s);
            if (Event.current.type == EventType.Layout) Build();
            float tw = area.width / Pages.Length;
            for (int i = 0; i < Pages.Length; i++)
                if (GUI.Toggle(new Rect(area.x + i * tw + 4, area.y, tw - 8, 46 * s), page == i, Pages[i], MenuSkin.Tab) && page != i) { SetPage(i); MenuNav.Click(); }
            GUI.Label(new Rect(area.x, area.y + 50 * s, area.width, 26 * s),
                      Gamepad.current != null ? "LB / RB: page     D-pad: choose and change     A: press     B: back"
                                              : "Q / E: page     Arrows: choose and change     Enter: press     Esc: back", small);
            var inner = new Rect(area.x, area.y + 84 * s, area.width, area.height - 84 * s);
            viewH = inner.height;
            GUILayout.BeginArea(inner);
            scroll = GUILayout.BeginScrollView(scroll);
            float labelW = inner.width * 0.36f;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.header) { GUILayout.Space(12 * s); GUILayout.Label(r.label, hdr); continue; }
                if (r.note != null) { GUILayout.Label(r.note, small, GUILayout.Width(inner.width - 40)); continue; }
                if (r.info != null)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(r.label, lbl, GUILayout.Width(labelW), GUILayout.Height(34 * s));
                    GUILayout.Label(r.info, hdrValue, GUILayout.Height(34 * s));
                    GUILayout.EndHorizontal();
                    continue;
                }
                bool on = i == sel && MenuNav.PadOrKeys;
                if (on) GUILayout.BeginHorizontal(rowSel); else GUILayout.BeginHorizontal();
                GUILayout.Label(r.label, lbl, GUILayout.Width(labelW), GUILayout.Height(40 * s));
                if (r.opts != null)
                {
                    int v = r.get();
                    int n = GUILayout.Toolbar(v, r.opts, btn, GUILayout.MaxWidth(inner.width - labelW - 50));
                    if (n != v) { r.set(n); sel = i; MenuNav.Click(); }
                }
                else if (r.getF != null)
                {
                    float v = r.getF();
                    GUILayout.Label(r.fmt(v), lbl, GUILayout.Width(110 * s), GUILayout.Height(40 * s));
                    GUILayout.BeginVertical();
                    GUILayout.Space(16 * s);
                    float n = GUILayout.HorizontalSlider(v, r.min, r.max, GUILayout.Width(inner.width * 0.4f));
                    GUILayout.EndVertical();
                    if (!Mathf.Approximately(n, v)) { r.setF(n); sel = i; }
                }
                else if (r.press != null)
                {
                    if (GUILayout.Button(r.pressText(), btn, GUILayout.Width(Mathf.Min(360 * s, inner.width - labelW - 50)))) { sel = i; r.press(); MenuNav.Click(); }
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                if (Event.current.type == EventType.Repaint && i == sel)
                {
                    var rr = GUILayoutUtility.GetLastRect();
                    selY = rr.y; selH = rr.height;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (scrollToSel && Event.current.type == EventType.Repaint)
            {
                if (selY < scroll.y + 10) scroll.y = Mathf.Max(0f, selY - 60 * s);
                else if (selY + selH > scroll.y + viewH - 30) scroll.y = selY + selH - viewH + 60 * s;
                scrollToSel = false;
            }
        }
    }
}
