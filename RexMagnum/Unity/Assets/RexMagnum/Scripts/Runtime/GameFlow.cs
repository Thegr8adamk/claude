using System.Collections;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// First playable flow (brief section 2 "Opening"):
    ///  1. Title screen: picture of the hotel and the beach (Resources/UI/title_bg), title, PLAY / SETTINGS / QUIT
    ///     (mouse, keyboard or controller), content warning, title music. SETTINGS opens the shared SettingsPanel.
    ///  2. Chaos sounds, fade in on Rex face-down on the bed in room 304, wake-up line.
    ///  3. Rex stands next to the bed; controls are taught one hint at a time (look/walk, punch the stuck door,
    ///     pick up the beer, taunt at the mirror, camera switch, towel on the balcony rail). A hint stays until the player
    ///     does it (or a time-out), then the next one shows. Day music loops.
    /// Music and sounds load from Resources/Music and Resources/Sfx when the ElevenLabs files are added
    /// (title_theme, day_resort, chaos); without them the flow still works, silently.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public Camera titleCamera;
        public Camera playerCamera;
        public RexController rex;
        public Transform bedPoint, wakePoint;
        public Behaviour[] disableDuringTitle;
        public bool skipTitle;

        enum State { Title, Intro, Play }
        State state = State.Title;
        float fade = 1f, fadeTarget;
        AudioSource music, fight;
        float fightLevel, dayLevel = DayVolume;
        Texture2D titleBg;
        int menuSel;
        bool inSettings;
        readonly SettingsPanel settings = new SettingsPanel();
        RexWeapons weapons;
        Vector3 hintStartPos;
        static readonly string[] MenuItems = { "PLAY", "SETTINGS", "QUIT" };
        const float DayVolume = 0.35f, FightVolume = 0.55f;
        int hint;
        float hintTime;
        Vector3 titleOrbitCenter;
        float titleAngle;
        GUIStyle big, mid, small, box;

        const int HintCount = 6;

        /// <summary>The tutorial hint with the keys / buttons the player uses now (remapped ones too).</summary>
        static string Hint(int i)
        {
            string B(string a) => RMInput.Btn(a);
            bool kb = RMInput.LastDevice == RMInput.DeviceKind.Keyboard;
            switch (i)
            {
                case 0: return kb ? $"Mouse: look.   {B("Forward")} {B("Left")} {B("Back")} {B("Right")}: walk.   {B("Run")}: run."
                                  : $"Right stick: look.   Left stick: walk.   {B("Run")}: run.";
                case 1: return $"The door is stuck. {B("Action")}: punch it.";
                case 2: return $"{B("Use")}: pick up the full beer bottle on the table. It is your first weapon.";
                case 3: return $"{B("Taunt")}: taunt. Try it at the mirror.";
                case 4: return $"{B("Camera")}: first person or third person.   {B("Shoulder")}: other shoulder.";
                default: return $"The beach towel is on the balcony rail ({B("Use")}). Look at the pool. Something is wrong.";
            }
        }

        public static bool Busy { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Busy = false;      // editor with Domain Reload off

        void Start()
        {
            Busy = !skipTitle;
            SettingsPanel.ApplyAll();
            titleBg = Resources.Load<Texture2D>("UI/title_bg");
            if (rex != null) weapons = rex.GetComponent<RexWeapons>();
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.spatialBlend = 0f;
            music.volume = 0.7f * AudioMix.Music;
            // fight music (80s hair metal) fades in when 2+ living blobs are near Rex, day music ducks under it
            fight = gameObject.AddComponent<AudioSource>();
            fight.clip = Resources.Load<AudioClip>("Music/fight");
            fight.loop = true;
            fight.spatialBlend = 0f;
            fight.volume = 0f;
            if (titleCamera != null) titleOrbitCenter = titleCamera.transform.position;
            if (titleCamera != null && titleBg != null)
            {
                // the picture covers the screen: the title camera draws nothing (no GPU cost behind the menu)
                titleCamera.cullingMask = 0;
                titleCamera.clearFlags = CameraClearFlags.SolidColor;
                titleCamera.backgroundColor = Color.black;
            }
            if (skipTitle) { StartCoroutine(Intro()); return; }
            SetPlayerActive(false);
            Play("Music/title_theme");
        }

        void SetPlayerActive(bool on)
        {
            if (rex != null) rex.InputEnabled = on;
            foreach (var b in disableDuringTitle) if (b != null) b.enabled = on;
            var pr = playerCamera ? playerCamera.GetComponent<RexCameraRig>() : null;
            if (pr) pr.InputEnabled = on;
            if (titleCamera != null) titleCamera.enabled = !on && state == State.Title;
            if (playerCamera != null) playerCamera.enabled = on || state != State.Title;
        }

        void Play(string res)
        {
            var clip = Resources.Load<AudioClip>(res);
            if (clip == null) return;
            music.clip = clip;
            music.Play();
        }

        void Update()
        {
            fade = Mathf.MoveTowards(fade, fadeTarget, Time.unscaledDeltaTime * 0.9f);
            if (state == State.Title)
            {
                if (titleCamera != null)
                {
                    titleAngle += Time.deltaTime * 1.5f;
                    titleCamera.transform.rotation = Quaternion.Euler(8f, -16f + Mathf.Sin(titleAngle * 0.05f) * 12f, 0f);
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                music.volume = 0.7f * AudioMix.Music;
                if (fade < 0.05f) TitleMenu();
            }
            else if (state == State.Play)
            {
                UpdateHint();
                FightMusic();
            }
        }

        void TitleMenu()
        {
            if (inSettings)
            {
                if (settings.Update()) { inSettings = false; settings.CancelWait(); PlayerPrefs.Save(); }
                return;
            }
            int n = Application.isMobilePlatform ? 2 : 3;      // phones have no QUIT
            if (MenuNav.Up) menuSel = (menuSel + n - 1) % n;
            if (MenuNav.Down) menuSel = (menuSel + 1) % n;
            var g = UnityEngine.InputSystem.Gamepad.current;
            if (MenuNav.Ok || (g != null && g.startButton.wasPressedThisFrame)) { MenuNav.Consume(); Choose(menuSel); }
        }

        void Choose(int i)
        {
            if (state != State.Title) return;
            Sfx.Play("bottle_clink", playerCamera != null ? playerCamera.transform.position : Vector3.zero, 0.5f);   // at the listener
            if (i == 0) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; StartCoroutine(Intro()); }
            else if (i == 1) { inSettings = true; settings.page = 0; }
            else if (i == 2) Application.Quit();
        }

        /// <summary>The next hint shows when the player has done this one (or after a time-out).</summary>
        void UpdateHint()
        {
            if (hint >= HintCount) return;
            float t = Time.time - hintTime;
            bool done;
            switch (hint)
            {
                case 0: done = (rex != null && Vector3.Distance(rex.transform.position, hintStartPos) > 2.5f && t > 4f) || t > 14f; break;
                case 1: done = StuckDoor.AnyOpen || t > 45f; break;
                case 2: done = (weapons != null && weapons.Has(WeaponId.Bottle)) || t > 45f; break;
                case 3: done = (RMInput.Taunt && t > 1f) || t > 25f; break;
                case 4: done = (RMInput.CameraToggle && t > 1f) || t > 20f; break;
                default: done = (weapons != null && weapons.Has(WeaponId.Towel)) || t > 60f; break;
            }
            if (done) { hint++; hintTime = Time.time; }
        }

        void FightMusic()
        {
            if (fight == null || fight.clip == null || rex == null) return;
            bool battle = !KingGlorpBoss.Fighting && Blob.LivingNear(rex.transform.position, 20f) >= 2;
            float target = battle ? FightVolume : 0f;
            // in fast (1 s), out slowly (4 s) so short pauses between blobs do not cut the song
            fightLevel = Mathf.MoveTowards(fightLevel, target, Time.deltaTime * (battle ? FightVolume : FightVolume / 4f));
            fight.volume = fightLevel * AudioMix.Music;
            if (fightLevel > 0f && !fight.isPlaying) fight.Play();
            else if (fightLevel <= 0f && fight.isPlaying) fight.Pause();
            music.volume = KingGlorpBoss.Fighting ? 0f : dayLevel * (1f - DayNight.Night) * (1f - fightLevel / FightVolume) * AudioMix.Music;   // night jungle music takes over
        }

        IEnumerator Intro()
        {
            state = State.Intro;
            fadeTarget = 1f;
            while (fade < 0.99f) yield return null;
            Sfx.Play("chaos", bedPoint != null ? bedPoint.position : Vector3.zero, 1f);     // follows Settings > Sound > Sound effects
            music.Stop();
            if (titleCamera != null) titleCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = true;
            if (rex != null && bedPoint != null)
            {
                rex.Teleport(bedPoint.position, bedPoint.eulerAngles.y);
                rex.Die();                       // face-down pose
                var rig = playerCamera ? playerCamera.GetComponent<RexCameraRig>() : null;
                if (rig) { rig.InputEnabled = false; rig.enabled = true; }
            }
            yield return new WaitForSeconds(0.6f);
            fadeTarget = 0f;
            yield return new WaitForSeconds(1.2f);
            if (rex != null) RexVoice.Say(rex.gameObject, "Wake", true);
            yield return new WaitForSeconds(2.6f);
            fadeTarget = 1f;
            while (fade < 0.99f) yield return null;
            if (rex != null && wakePoint != null)
            {
                rex.Revive();
                rex.Teleport(wakePoint.position, wakePoint.eulerAngles.y);
                var wrig = playerCamera ? playerCamera.GetComponent<RexCameraRig>() : null;
                if (wrig) wrig.SetYaw(wakePoint.eulerAngles.y);
                if (rex.bodyAnimator) rex.bodyAnimator.Play("Locomotion", 0, 0f);
                if (rex.armsAnimator && rex.armsAnimator.gameObject.activeInHierarchy) rex.armsAnimator.Play("Locomotion", 0, 0f);
            }
            state = State.Play;
            Busy = false;
            SetPlayerActive(true);
            Play("Music/day_resort");
            music.volume = DayVolume * AudioMix.Music;
            fadeTarget = 0f;
            hintTime = Time.time;
            if (rex != null) hintStartPos = rex.transform.position;
        }

        GUIStyle menuBtn, menuSelStyle;

        void DrawTitle(float w, float h, float s)
        {
            if (titleBg != null)
            {
                // slow push-in on the picture (aspect fill)
                float t = Time.unscaledTime * 0.012f;
                float zoom = 1.04f + Mathf.Sin(t) * 0.03f;
                float ia = (float)titleBg.width / titleBg.height, sa = w / h;
                float uw = ia > sa ? sa / ia : 1f, uh = ia > sa ? 1f : ia / sa;
                uw /= zoom; uh /= zoom;
                var uv = new Rect(0.5f - uw / 2f + Mathf.Sin(t * 0.7f) * 0.01f, 0.5f - uh / 2f, uw, uh);
                GUI.DrawTextureWithTexCoords(new Rect(0, 0, w, h), titleBg, uv);
            }
            // dark band on the left: the menu reads on any picture
            for (int i = 0; i < 24; i++)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.62f * (1f - i / 24f));
                GUI.DrawTexture(new Rect(i * w * 0.025f, 0, w * 0.025f + 1, h), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            var bl = new GUIStyle(big) { alignment = TextAnchor.MiddleLeft, fontSize = (int)(96 * s) };
            var sh = new GUIStyle(bl);
            sh.normal.textColor = new Color(0, 0, 0, 0.7f);
            float x = w * 0.06f;
            GUI.Label(new Rect(x + 5 * s, h * 0.12f + 5 * s, w, 130 * s), "REX MAGNUM", sh);
            GUI.Label(new Rect(x, h * 0.12f, w, 130 * s), "REX MAGNUM", bl);
            var md = new GUIStyle(mid) { alignment = TextAnchor.MiddleLeft, fontSize = (int)(40 * s) };
            GUI.Label(new Rect(x + 6 * s, h * 0.12f + 118 * s, w, 60 * s), "VACATION VIOLENCE", md);

            if (menuBtn == null || menuBtn.fontSize != (int)(40 * s))
            {
                menuBtn = new GUIStyle(GUI.skin.label) { fontSize = (int)(40 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                menuBtn.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
                menuSelStyle = new GUIStyle(menuBtn);
                menuSelStyle.normal.textColor = new Color(1f, 0.8f, 0.25f);
            }

            if (inSettings)
            {
                var area = new Rect(x, h * 0.3f, w * 0.88f - x, h * 0.56f);
                GUI.color = new Color(0f, 0f, 0f, 0.75f);
                GUI.DrawTexture(new Rect(area.x - 20 * s, area.y - 20 * s, area.width + 40 * s, area.height + 110 * s), Texture2D.whiteTexture);
                GUI.color = Color.white;
                settings.Draw(area, s);
                var back = new Rect(area.x, area.yMax + 20 * s, 300 * s, 60 * s);
                GUI.Label(back, "< BACK", back.Contains(Event.current.mousePosition) ? menuSelStyle : menuBtn);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && back.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    inSettings = false; settings.CancelWait();
                    PlayerPrefs.Save();
                }
                return;
            }

            int n = Application.isMobilePlatform ? 2 : 3;
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(x, h * 0.42f + i * 78 * s, 420 * s, 66 * s);
                bool hover = r.Contains(Event.current.mousePosition);
                if (hover && (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)) menuSel = i;
                bool on = i == menuSel;
                if (on)
                {
                    GUI.color = new Color(1f, 0.75f, 0.2f, 0.9f);
                    GUI.DrawTexture(new Rect(r.x - 22 * s, r.y + 14 * s, 8 * s, r.height - 28 * s), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                GUI.Label(r, (on ? "  " : "") + MenuItems[i], on ? menuSelStyle : menuBtn);
                if (fade < 0.05f && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover)
                {
                    menuSel = i;
                    Event.current.Use();
                    Choose(i);
                }
            }
            var sm = new GUIStyle(small) { alignment = TextAnchor.LowerLeft, fontSize = (int)(19 * s) };
            var g = UnityEngine.InputSystem.Gamepad.current;
            GUI.Label(new Rect(x, h * 0.42f + n * 78 * s + 16 * s, w * 0.5f, 40 * s),
                      g != null ? "Controller: " + g.displayName + "   (D-pad / stick, A = OK)" : "Mouse, or arrow keys + Enter. Controllers work too.", sm);
            GUI.Label(new Rect(x, h - 80 * s, w * 0.6f, 60 * s),
                      "CONTENT WARNING: crude adult humor, rude gestures, strong language, cartoon slime violence.", sm);
        }

        void Styles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = new Color(1f, 0.8f, 0.25f);
            mid = new GUIStyle(big) { fontSize = 30 };
            mid.normal.textColor = new Color(1f, 0.92f, 0.8f);
            small = new GUIStyle(big) { fontSize = 18, fontStyle = FontStyle.Normal, wordWrap = true };
            small.normal.textColor = Color.white;
            box = new GUIStyle(GUI.skin.box) { fontSize = 20, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            box.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            if (PhotoAlbum.Capturing) return;
            Styles();
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            if (state == State.Title) DrawTitle(w, h, s);
            else if (state == State.Play && hint < HintCount && !PauseMenu.Open)
            {
                box.fontSize = (int)(30 * s);
                var r = new Rect(w * 0.18f, h * 0.05f, w * 0.64f, 92 * s);
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.78f, 0.25f, 0.95f);
                GUI.DrawTexture(new Rect(r.x, r.yMax - 4 * s, r.width, 4 * s), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(r, Hint(hint), box);
            }
            if (fade > 0.001f)
            {
                GUI.color = new Color(0, 0, 0, fade);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }
    }
}
