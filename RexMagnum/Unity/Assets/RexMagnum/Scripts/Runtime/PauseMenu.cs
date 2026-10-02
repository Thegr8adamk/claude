using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RexMagnum
{
    /// <summary>
    /// Pause menu (brief section 7: Pause = map, album, accomplishments, settings). Esc / Menu / touch II.
    /// Tabs: Map (zones visited, big goals), Album (photos + captions), Accomplishments (by group),
    /// Settings (SettingsPanel: graphics, camera + VR, sound, controls with keyboard and controller remap).
    /// Controller: LT / RT or keys 1-4 change the tab, B / Menu resume. Settings are saved in PlayerPrefs.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static bool Open { get; private set; }
        int tab;
        Vector2 scroll;
        readonly SettingsPanel panel = new SettingsPanel();
        readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
        static readonly string[] Tabs = { "MAP", "ALBUM", "ACCOMPLISHMENTS", "SETTINGS" };
        GUIStyle title, label, button, small;

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
        }

        void SetOpen(bool o)
        {
            panel.CancelWait();
            Open = o;
            Time.timeScale = o ? 0f : 1f;
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
            title = new GUIStyle(GUI.skin.label) { fontSize = (int)(34 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(1f, 0.8f, 0.25f);
            label = new GUIStyle(GUI.skin.label) { fontSize = (int)(22 * s), wordWrap = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = (int)(17 * s) };
            button = new GUIStyle(GUI.skin.button) { fontSize = (int)(20 * s) };
        }

        void OnGUI()
        {
            if (!Open) return;
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            Styles(s);
            GUI.color = new Color(0, 0, 0, 0.82f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, 20 * s, w, 50 * s), "PAUSED", title);
            float tw = w / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
                if (GUI.Toggle(new Rect(i * tw + 6, 80 * s, tw - 12, 46 * s), tab == i, Tabs[i], button) && tab != i) pendingTab = i;
            var area = new Rect(w * 0.08f, 150 * s, w * 0.84f, h - 260 * s);
            if (tab == 3)
            {
                panel.Draw(area, s);
                if (GUI.Button(new Rect(w * 0.5f - 160 * s, h - 90 * s, 320 * s, 60 * s), "RESUME", button)) SetOpen(false);
                return;
            }
            GUILayout.BeginArea(area);
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: MapTab(); break;
                case 1: AlbumTab(s, area.width); break;
                case 2: AccomplishmentsTab(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (GUI.Button(new Rect(w * 0.5f - 160 * s, h - 90 * s, 320 * s, 60 * s), "RESUME", button)) SetOpen(false);
        }

        void MapTab()
        {
            GUILayout.Label("ISLAND ZONES", title);
            foreach (var z in new[] { ("hotel", "Resort hotel"), ("pool", "Pool area"), ("beach", "Beach"), ("golf", "Golf course"),
                                      ("jungle", "Thick jungle"), ("ruins", "Jungle ruins"), ("cave", "Cave"), ("ship", "Crashed alien ship") })
                GUILayout.Label($"{(Progress.Value("zone_" + z.Item1) > 0 ? "[x]" : "[ ]")}  {z.Item2}", label);
            GUILayout.Space(20);
            GUILayout.Label("BIG GOALS", title);
            GUILayout.Label($"{(FactoryCore.Stopped ? "[x]" : "[ ]")}  Stop the blob factory (ruins)", label);
            GUILayout.Label($"{(PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) == 1 ? "[x]" : "[ ]")}  Pop King Glorp (ship)", label);
            GUILayout.Label($"Blobs popped: {Progress.Value("kills")}    Guests rescued: {Progress.Value("rescues")}    Photos: {Progress.Value("photos")}", small);
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
            string path = Path.Combine(Progress.AlbumDir, file);
            t = null;
            if (File.Exists(path)) { t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(path)); }
            thumbs[file] = t;
            return t;
        }

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
