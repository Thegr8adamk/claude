using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RexMagnum
{
    /// <summary>
    /// Pause menu (brief section 7: Pause = map, album, accomplishments, settings). Esc / Menu / touch II.
    /// Tabs: Map (zones visited, big goals), Album (photos + captions), Accomplishments (by group),
    /// Settings (graphics, camera, look speed X/Y, invert Y, kill cam, subtitles, volume), Controls (keyboard remap).
    /// Settings are saved in PlayerPrefs.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static bool Open { get; private set; }
        int tab;
        Vector2 scroll;
        string waitingFor;
        readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
        static readonly string[] Tabs = { "MAP", "ALBUM", "ACCOMPLISHMENTS", "SETTINGS", "CONTROLS" };
        GUIStyle title, label, button, small;

        void Start() => ApplySettings();

        public static void ApplySettings()
        {
            RMInput.MouseSensitivity = PlayerPrefs.GetFloat("RexMagnum_LookSpeed", 0.08f);
            RMInput.LookScaleX = PlayerPrefs.GetFloat("RexMagnum_LookX", 1f);
            RMInput.LookScaleY = PlayerPrefs.GetFloat("RexMagnum_LookY", 1f);
            RMInput.InvertY = PlayerPrefs.GetInt("RexMagnum_InvertY", 0) == 1;
            AudioListener.volume = PlayerPrefs.GetFloat("RexMagnum_Volume", 1f);
        }

        void Update()
        {
            if (waitingFor != null)
            {
                var k = Keyboard.current;
                if (k != null)
                    foreach (var key in k.allKeys)
                        if (key.wasPressedThisFrame)
                        {
                            if (key.keyCode != Key.Escape || waitingFor == "Pause") RMInput.Rebind(waitingFor, key.keyCode);
                            waitingFor = null;
                            break;
                        }
                return;
            }
            if (RMInput.Pause) SetOpen(!Open);
        }

        void SetOpen(bool o)
        {
            Open = o;
            Time.timeScale = o ? 0f : 1f;
            AudioListener.pause = o;
            Cursor.lockState = o ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = o;
            var rex = FindFirstObjectByType<RexController>();
            if (rex != null && GolfCart.Driven == null) rex.InputEnabled = !o;
            var rig = FindFirstObjectByType<RexCameraRig>();
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
                if (GUI.Toggle(new Rect(i * tw + 6, 80 * s, tw - 12, 46 * s), tab == i, Tabs[i], button)) tab = i;
            var area = new Rect(w * 0.08f, 150 * s, w * 0.84f, h - 260 * s);
            GUILayout.BeginArea(area);
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: MapTab(); break;
                case 1: AlbumTab(s, area.width); break;
                case 2: AccomplishmentsTab(); break;
                case 3: SettingsTab(); break;
                case 4: ControlsTab(); break;
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
            if (photos.Count == 0) { GUILayout.Label("No photos yet. Press P (D-pad right) near a blob or a guest.", label); return; }
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

        static float Slider(string name, string key, float def, float min, float max, GUIStyle st)
        {
            float v = PlayerPrefs.GetFloat(key, def);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{name}: {v:0.00}", st, GUILayout.Width(360));
            float n = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(400));
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(n, v)) { PlayerPrefs.SetFloat(key, n); ApplySettings(); }
            return n;
        }

        void SettingsTab()
        {
            GUILayout.Label("GRAPHICS", title);
            int q = PlayerPrefs.GetInt(QualityAuto.PrefKey, -1);
            int nq = GUILayout.Toolbar(q + 1, new[] { "AUTO", "LOW", "MEDIUM", "HIGH", "ULTRA" }, button) - 1;
            if (nq != q) FindFirstObjectByType<QualityAuto>()?.SetFromSettings(nq);
            GUILayout.Space(16);
            GUILayout.Label("CAMERA", title);
            int cm = PlayerPrefs.GetInt("RexMagnum_CamDefault", 2);
            int ncm = GUILayout.Toolbar(cm, new[] { "FIRST PERSON", "THIRD PERSON", "REMEMBER LAST" }, button);
            if (ncm != cm) PlayerPrefs.SetInt("RexMagnum_CamDefault", ncm);
            bool bob = PlayerPrefs.GetInt("RexMagnum_HeadBob", 1) == 1;
            bool nb = GUILayout.Toggle(bob, " Head bob (first person)", label);
            if (nb != bob) PlayerPrefs.SetInt("RexMagnum_HeadBob", nb ? 1 : 0);
            Slider("Look speed", "RexMagnum_LookSpeed", 0.08f, 0.02f, 0.25f, label);
            Slider("Look speed X", "RexMagnum_LookX", 1f, 0.3f, 2f, label);
            Slider("Look speed Y", "RexMagnum_LookY", 1f, 0.3f, 2f, label);
            bool inv = PlayerPrefs.GetInt("RexMagnum_InvertY", 0) == 1;
            bool ni = GUILayout.Toggle(inv, " Invert Y", label);
            if (ni != inv) { PlayerPrefs.SetInt("RexMagnum_InvertY", ni ? 1 : 0); ApplySettings(); }
            GUILayout.Space(16);
            GUILayout.Label("KILL CAM", title);
            int kc = PlayerPrefs.GetInt("RexMagnum_KillCam", 2);
            int nkc = GUILayout.Toolbar(kc, new[] { "OFF", "LESS OFTEN", "ALWAYS" }, button);
            if (nkc != kc) PlayerPrefs.SetInt("RexMagnum_KillCam", nkc);
            GUILayout.Space(16);
            GUILayout.Label("SOUND", title);
            Slider("Volume", "RexMagnum_Volume", 1f, 0f, 1f, label);
            GUILayout.Label("Subtitles: always on (all lines have subtitles).", small);
        }

        void ControlsTab()
        {
            GUILayout.Label("KEYBOARD", title);
            GUILayout.Label("Click an action, then press the new key. If a key is in use, the two actions swap.", small);
            foreach (var a in RMInput.Actions)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(a, label, GUILayout.Width(300));
                string k = waitingFor == a ? "press a key..." : RMInput.KeyFor(a).ToString();
                if (GUILayout.Button(k, button, GUILayout.Width(260))) waitingFor = a;
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(10);
            if (GUILayout.Button("RESET KEYBOARD", button, GUILayout.Width(300))) RMInput.ResetKeys();
            GUILayout.Space(10);
            GUILayout.Label("Controller and touch use the layout from the brief. Mouse: left = Action, right = Aim, wheel = weapons.", small);
        }
    }
}
