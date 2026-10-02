using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RexMagnum
{
    /// <summary>
    /// First playable flow (brief section 2 "Opening"):
    ///  1. Title screen: picture of the hotel and the beach (Resources/UI/title_bg), title, PLAY / NEW GAME / SETTINGS /
    ///     HELP / QUIT (mouse, keyboard or controller), content warning, title music. SETTINGS and HELP open the shared
    ///     SettingsPanel. NEW GAME asks first, then clears the save (Progress.ResetAll) and reloads the scene.
    ///  2. Chaos sounds, fade in on Rex face-down on the bed in room 304, wake-up line.
    ///  3. Rex stands next to the bed; controls are taught one hint at a time (look/walk, punch the stuck door,
    ///     pick up the beer, taunt at the mirror, camera switch, dodge, towel on the balcony rail). A hint stays until the player
    ///     does it (or a time-out), then the next one shows. Day music loops.
    ///     The room hints end when Rex leaves the room (downstairs or along the corridor).
    ///  4. The first fight teaches dodge (unless the room did), heavy (hold) and combo (3 taps) the same way, each once
    ///     per save, drawn under the NEXT goal line. A returning player wakes at the saved far checkpoint.
    ///  5. A big goal done (factory, King Glorp, bar defense): "BIG GOAL COMPLETE" banner + a short music sting.
    ///     After King Glorp (the ending) a stats card follows: play time, blobs, guests, photos, deaths, goals.
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
        static readonly string[] MenuItems = { "PLAY", "NEW GAME", "SETTINGS", "HELP", "QUIT" };
        bool confirmNew;          // NEW GAME asks first: the save is cleared
        int confirmSel = 1;       // NO is chosen first
        const float DayVolume = 0.35f, FightVolume = 0.55f;
        int hint;
        float hintTime;
        Vector3 titleOrbitCenter;
        float titleAngle;

        const int HintCount = 7;

        /// <summary>The tutorial hint with the keys / buttons the player uses now (remapped ones too).</summary>
        static string Hint(int i)
        {
            string B(string a) => Glyph.B(a);          // controller: coloured "[A]" tokens
            bool kb = RMInput.LastDevice == RMInput.DeviceKind.Keyboard;
            switch (i)
            {
                case 0: return kb ? $"Mouse: look.   {B("Forward")} {B("Left")} {B("Back")} {B("Right")}: walk.   {B("Run")}: run."
                                  : $"Right stick: look.   Left stick: walk.   {B("Run")}: run.";
                case 1: return $"The door is stuck. {B("Action")}: punch it.";
                case 2: return $"{B("Use")}: pick up the full beer bottle on the table. It is your first weapon.";
                case 3: return $"{B("Taunt")}: taunt. Try it at the mirror.";
                case 4: return $"{B("Camera")}: first person or third person.   {B("Shoulder")}: other shoulder.";
                case 5: return $"{B("Dodge")}: dodge. Blobs glow red just before they hit.";
                default: return $"The beach towel is on the balcony rail ({B("Use")}). Look at the pool. Something is wrong.";
            }
        }

        const int CombatHintCount = 3;

        /// <summary>The fight hints, after the first fight starts (one at a time, each shown once per save).</summary>
        static string CombatHint(int i)
        {
            string B(string a) => Glyph.B(a);
            switch (i)
            {
                case 0: return $"{B("Dodge")}: dodge. A blob glows red just before it hits: dodge then.";
                case 1: return $"Hold {B("Action")}: heavy attack. Slower, about 3x the damage, knocks blobs back.";
                default: return $"Tap {B("Action")} 3 times fast: combo. The 3rd hit is a finisher.";
            }
        }

        public static bool Busy { get; private set; }
        /// <summary>The room tutorial hints still show (the HUD hides the objective line meanwhile).</summary>
        public static bool TutorialActive { get; private set; }
        /// <summary>Seconds since Rex woke up (the HUD shows the control list for the first minute).</summary>
        public static float PlaySeconds => playStart < 0f ? Time.timeSinceLevelLoad : Time.time - playStart;   // no GameFlow (test scene): since load
        static float playStart = -1f;
        static bool newGameStart;        // set before the NEW GAME reload: skip the title once

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Busy = false; TutorialActive = false; playStart = -1f; newGameStart = false; }   // editor with Domain Reload off

        void Start()
        {
            Busy = !skipTitle;
            SettingsPanel.ApplyAll();
            // QUIT TO TITLE reloads the scene but statics stay: no play time from the last session (PLAY sets it again)
            playStart = Time.time;
            TutorialActive = false;
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
            if (newGameStart)
            {
                // NEW GAME reloaded the scene: straight to the wake-up in room 304 (the screen is still black)
                newGameStart = false;
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                StartCoroutine(Intro());
                return;
            }
            MenuNav.Opened();         // a key still held from play does not move the title menu
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
                CombatHints();
                BigGoals();
                FightMusic();
                CountPlayTime();
            }
            TutorialActive = state == State.Play && hint < HintCount;
        }

        void TitleMenu()
        {
            if (inSettings)
            {
                if (settings.Update()) { inSettings = false; settings.CancelWait(); PlayerPrefs.Save(); }
                return;
            }
            if (confirmNew)
            {
                if (MenuNav.Up || MenuNav.Down || MenuNav.Left || MenuNav.Right) confirmSel = 1 - confirmSel;
                if (MenuNav.Back) { MenuNav.Consume(); confirmNew = false; }
                else if (MenuNav.Ok) { MenuNav.Consume(); Confirm(confirmSel == 0); }
                return;
            }
            int n = MenuCount;
            if (MenuNav.Up) menuSel = (menuSel + n - 1) % n;
            if (MenuNav.Down) menuSel = (menuSel + 1) % n;
            var g = UnityEngine.InputSystem.Gamepad.current;
            if (MenuNav.Ok || (g != null && g.startButton.wasPressedThisFrame)) { MenuNav.Consume(); Choose(menuSel); }
        }

        static int MenuCount => Application.isMobilePlatform ? MenuItems.Length - 1 : MenuItems.Length;      // phones have no QUIT

        void Choose(int i)
        {
            if (state != State.Title) return;
            MenuNav.Click();          // the menus' common click (once per frame: a keyboard OK already clicked)
            if (i == 0) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; StartCoroutine(Intro()); }
            else if (i == 1) { confirmNew = true; confirmSel = 1; }
            else if (i == 2) { inSettings = true; settings.page = 0; }
            else if (i == 3) { inSettings = true; settings.page = SettingsPanel.HelpPage; }
            else if (i == 4) Application.Quit();
        }

        /// <summary>NEW GAME answer. Yes: clear the save (settings and keys stay), reload the scene, wake up in room 304.</summary>
        void Confirm(bool yes)
        {
            confirmNew = false;
            if (!yes) return;
            Progress.ResetAll();
            int scene = SceneManager.GetActiveScene().buildIndex;
            if (scene < 0) { Choose(0); return; }      // scene not in the build list (editor test scene): just play
            newGameStart = true;
            Busy = true;
            SceneManager.LoadScene(scene);
        }

        /// <summary>The next hint shows when the player has done this one (or after a time-out).</summary>
        void UpdateHint()
        {
            if (hint >= HintCount) return;
            // out of room 304 (downstairs, or off along the corridor): the room hints end, the goal line takes over
            if (rex != null && LeftRoom(rex.transform.position)) { hint = HintCount; return; }
            float t = Time.time - hintTime;
            bool done;
            switch (hint)
            {
                case 0: done = (rex != null && Vector3.Distance(rex.transform.position, hintStartPos) > 2.5f && t > 4f) || t > 14f; break;
                case 1: done = StuckDoor.AnyOpen || t > 45f; break;
                case 2: done = (weapons != null && weapons.Has(WeaponId.Bottle)) || t > 45f; break;
                case 3: done = (RMInput.Taunt && t > 1f) || t > 25f; break;
                case 4: done = (RMInput.CameraToggle && t > 1f) || t > 20f; break;
                case 5: done = (RMInput.Dodge && t > 1f) || t > 20f; break;
                default: done = (weapons != null && weapons.Has(WeaponId.Towel)) || t > 60f; break;
            }
            if (!done) return;
            if (hint == 5) Progress.Max("combat_tips", 1);      // the room taught dodge: the first fight starts at heavy
            hint++; hintTime = Time.time;
        }

        // room 304 (Island.json hotel, floor 3 ~10.7 m): left when below the 2nd floor or ~18 m away on the flat
        static readonly Vector3 Room304 = new Vector3(16f, 10.7f, -293.7f);

        bool LeftRoom(Vector3 p)
        {
            if (p.y < 6f) return true;
            Vector3 c = wakePoint != null ? wakePoint.position : Room304;
            Vector3 d = p - c; d.y = 0f;
            return d.sqrMagnitude > 18f * 18f;
        }

        int combatHint = -1, comboTaps;
        float combatHintTime, actionDownAt = -1f, lastTapAt = -10f;

        /// <summary>After the room hints: the first time a blob comes close, dodge, heavy and combo are taught one at a
        /// time. Each stays until the player does it (or a time-out); "combat_tips" in the save counts the ones shown.</summary>
        void CombatHints()
        {
            if (hint < HintCount || rex == null) return;
            int shown = Progress.Value("combat_tips");
            if (shown >= CombatHintCount) { combatHint = -1; return; }
            if (combatHint < 0)
            {
                if (!FightNear(rex.transform.position, 15f)) return;      // waits for a fight
                combatHint = shown; combatHintTime = Time.time; comboTaps = 0; actionDownAt = -1f;
                Sfx.PlayUI("ui_click", 0.22f, 0.8f);         // soft cue: a tip came up
            }
            float t = Time.time - combatHintTime;
            if (RMInput.ActionDown)
            {
                actionDownAt = Time.time;
                comboTaps = Time.time - lastTapAt <= 0.8f ? comboTaps + 1 : 1;       // RexWeapons' combo window
                lastTapAt = Time.time;
            }
            bool done;
            switch (combatHint)
            {
                case 0: done = (RMInput.Dodge && t > 1f) || t > 20f; break;
                case 1: done = (actionDownAt >= 0f && RMInput.ActionHeld && Time.time - actionDownAt >= 0.3f && t > 1f) || t > 25f; break;
                default: done = (comboTaps >= 3 && t > 1f) || t > 25f; break;
            }
            if (!done) return;
            Progress.Set("combat_tips", combatHint + 1);
            combatHint = -1;          // the next one shows right away while the fight goes on, else at the next fight
        }

        /// <summary>A living blob within r that is not in disguise (a sundress tourist strolling by is no fight yet).</summary>
        static bool FightNear(Vector3 p, float r)
        {
            float r2 = r * r;
            foreach (var b in Blob.All)
                if (b != null && !b.IsDead && !b.Disguised && (b.transform.position - p).sqrMagnitude < r2) return true;
            return false;
        }

        // ------------------------------------------------------------ big goals: banner + music sting

        static readonly string[] GoalKeys = { "factory", "glorp", "bar_defended", "cave_siege" };
        // the reward line: RexLife raises max health by 25 for the factory and King Glorp (its own notice says so)
        static readonly string[] GoalNames = { "The blob factory is stopped.   MAX HEALTH +25", "King Glorp is popped.   MAX HEALTH +25",
                                                   "The bar held. Free refill: all your guns are full.",
                                                   "The cave siege is over. +5 coconuts and ammo for all your guns." };   // the events show no notice of their own
        AudioSource sting;
        float bannerUntil, stingStart = -100f, nextGoalCheck;
        string bannerLine;

        /// <summary>A big goal is done: "BIG GOAL COMPLETE" + its reward line and a short music sting,
        /// once per goal per save ("goal_banner_&lt;key&gt;"). Waits while the ending plays.</summary>
        void BigGoals()
        {
            if (Time.unscaledTime < nextGoalCheck) return;
            nextGoalCheck = Time.unscaledTime + 0.5f;
            if (EndingSequence.Playing || Time.unscaledTime < bannerUntil) return;
            for (int i = 0; i < GoalKeys.Length; i++)
            {
                if (Progress.Value(GoalKeys[i]) <= 0 || Progress.Value("goal_banner_" + GoalKeys[i]) > 0) continue;
                Progress.Set("goal_banner_" + GoalKeys[i], 1);
                bannerLine = GoalNames[i];
                bannerUntil = Time.unscaledTime + 5f;
                // King Glorp (after the ending): the run's stats card follows the banner
                if (GoalKeys[i] == "glorp") { statsFrom = bannerUntil; statsUntil = statsFrom + StatsSeconds; }
                Sting();
                return;
            }
        }

        float statsFrom = -100f, statsUntil = -100f, playTime;
        const float StatsSeconds = 10f;

        /// <summary>Play time for the stats ("play_s" in the save): unpaused seconds in play, written every 5 s.</summary>
        void CountPlayTime()
        {
            if (PauseMenu.Open || Busy) return;
            playTime += Time.unscaledDeltaTime;
            if (playTime < 5f) return;
            int whole = (int)playTime;
            playTime -= whole;
            Progress.Add("play_s", whole);
        }

        /// <summary>After the ending: the run in numbers (Progress.Stats) on a card in the middle, 10 s.</summary>
        void DrawStats(float w, float h, float s)
        {
            var stats = Progress.Stats();
            float a = MenuSkin.Appear(statsFrom, 0.4f) * Mathf.Clamp01((statsUntil - Time.unscaledTime) / 0.8f);
            float rowH = 44 * s;
            var r = new Rect(w * 0.3f, h * 0.24f, w * 0.4f, 110 * s + stats.Count * rowH);
            GUI.color = new Color(1f, 1f, 1f, a);
            MenuSkin.DrawPanel(r);
            GUI.color = new Color(1f, 0.78f, 0.25f, 0.95f * a);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 4 * s, r.width, 4 * s), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(r.x, r.y + 10 * s, r.width, 80 * s), "YOUR VACATION", MenuSkin.Title);
            for (int i = 0; i < stats.Count; i++)
            {
                float y = r.y + 92 * s + i * rowH;
                GUI.Label(new Rect(r.x + 40 * s, y, r.width * 0.55f, rowH), stats[i].what, MenuSkin.Label);
                GUI.Label(new Rect(r.x + r.width * 0.6f, y, r.width * 0.4f - 40 * s, rowH), stats[i].value, MenuSkin.Label);
            }
            GUI.color = Color.white;
        }

        /// <summary>Goals done before this save knew banners (an old save): marked shown, no banner on load.</summary>
        static void MarkGoalsShown()
        {
            foreach (var k in GoalKeys)
                if (Progress.Value(k) > 0 && Progress.Value("goal_banner_" + k) <= 0) Progress.Set("goal_banner_" + k, 1);
        }

        /// <summary>The title theme's first ~4 s as a sting (no extra clip needed); the day / fight music dips under it.</summary>
        void Sting()
        {
            var clip = Resources.Load<AudioClip>("Music/title_theme");
            if (clip == null) return;
            if (sting == null)
            {
                sting = gameObject.AddComponent<AudioSource>();
                sting.loop = false;
                sting.spatialBlend = 0f;
                sting.playOnAwake = false;
            }
            sting.clip = clip;
            sting.time = 0f;
            // the level before Play (else the first frame plays at the last sting's faded-out volume, or full)
            sting.volume = 0.55f * AudioMix.Music * AudioMix.Duck;
            sting.Play();
            stingStart = Time.unscaledTime;
        }

        const float StingLength = 4f;

        /// <summary>0..1: how far the sting ducks the other music (in 0.2 s, out over the sting's last second).</summary>
        float StingDuck
        {
            get
            {
                float t = Time.unscaledTime - stingStart;
                if (t < 0f || t > StingLength + 0.5f) return 0f;
                return Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((StingLength + 0.5f - t) / 1f);
            }
        }

        void UpdateSting()
        {
            if (sting == null || !sting.isPlaying) return;
            float t = Time.unscaledTime - stingStart;
            if (t > StingLength) { sting.Stop(); return; }
            // fades out over its last 1.2 s (a cut-off song, but soft)
            sting.volume = 0.55f * AudioMix.Music * Mathf.Clamp01((StingLength - t) / 1.2f) * AudioMix.Duck;
        }

        void FightMusic()
        {
            UpdateSting();
            if (rex == null) return;
            if (fight != null && fight.clip != null)
            {
                bool battle = !KingGlorpBoss.Fighting && Blob.LivingNear(rex.transform.position, 20f) >= 2;
                float target = battle ? FightVolume : 0f;
                // in fast (1 s), out slowly (4 s) so short pauses between blobs do not cut the song
                fightLevel = Mathf.MoveTowards(fightLevel, target, Time.deltaTime * (battle ? FightVolume : FightVolume / 4f));
                // AudioMix.Duck: music dips to 60% while a voice line plays (no AudioMixer asset needed)
                fight.volume = fightLevel * AudioMix.Music * AudioMix.Duck * (1f - 0.8f * StingDuck);
                if (fightLevel > 0f && !fight.isPlaying) fight.Play();
                else if (fightLevel <= 0f && fight.isPlaying) fight.Pause();
            }
            // without the fight clip the day music still follows night, the boss and the voice duck
            music.volume = KingGlorpBoss.Fighting ? 0f : dayLevel * (1f - DayNight.Night) * (1f - fightLevel / FightVolume) * AudioMix.Music * AudioMix.Duck
                           * (1f - 0.8f * StingDuck);   // night jungle music takes over; the big goal sting ducks it
        }

        IEnumerator Intro()
        {
            state = State.Intro;
            fadeTarget = 1f;
            while (fade < 0.99f) yield return null;
            // a returning player (has been down at the pool before): no face-down wake-up, no room hints, the door
            // already open, the goal right away
            bool returning = GameGuide.Visited("pool");
            music.Stop();
            if (titleCamera != null) titleCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = true;
            if (returning)
            {
                hint = HintCount;
                StuckDoor.OpenAll();
            }
            else
            {
                Sfx.Play("chaos", bedPoint != null ? bedPoint.position : Vector3.zero, 1f);     // follows Settings > Sound > Sound effects
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
            }
            if (rex != null && wakePoint != null)
            {
                // a returning player wakes at the last saved far checkpoint (RexLife, PlayerPrefs "rm_cp"), else in room 304
                Vector3 wakeAt = wakePoint.position;
                float wakeYaw = wakePoint.eulerAngles.y;
                if (returning && RexLife.TryLoadSavedCheckpoint(out Vector3 cpAt, out float cpYaw)) { wakeAt = cpAt; wakeYaw = cpYaw; }
                rex.Revive();
                rex.Teleport(wakeAt, wakeYaw);
                // wake grace: a blob waiting at the checkpoint gets no free hit while the screen fades in (3 s from here)
                if (returning) RexLife.WakeGrace(3f);
                var wrig = playerCamera ? playerCamera.GetComponent<RexCameraRig>() : null;
                if (wrig) wrig.SetYaw(wakeYaw);
                if (rex.bodyAnimator) rex.bodyAnimator.Play("Locomotion", 0, 0f);
                if (rex.armsAnimator && rex.armsAnimator.gameObject.activeInHierarchy) rex.armsAnimator.Play("Locomotion", 0, 0f);
            }
            MarkGoalsShown();
            state = State.Play;
            Busy = false;
            SetPlayerActive(true);
            Play("Music/day_resort");
            music.volume = DayVolume * AudioMix.Music;
            fadeTarget = 0f;
            hintTime = Time.time;
            if (rex != null) hintStartPos = rex.transform.position;
            playStart = Time.time;
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
            float x = w * 0.06f;
            // the logo slides down into place once the title fades in
            if (fade > 0.35f) titleSince = Time.unscaledTime;
            float la = MenuSkin.Appear(titleSince, 0.6f);
            MenuSkin.DrawLogo(new Rect(x, h * 0.12f - (1f - la) * 30 * s, w, 130 * s), "REX MAGNUM", s, la);
            GUI.color = new Color(1f, 1f, 1f, la);
            GUI.Label(new Rect(x + 6 * s, h * 0.12f + 118 * s, w, 60 * s), "VACATION VIOLENCE", MenuSkin.Subtitle);
            menuBtn = MenuSkin.MenuItem;
            menuSelStyle = MenuSkin.MenuItemOn;
            // menu, settings and the NEW GAME question: a short fade + slide from the left when one opens
            int view = inSettings ? 2 : confirmNew ? 1 : 0;
            if (view != menuView) { menuView = view; viewSince = Time.unscaledTime; }
            ua = MenuSkin.Appear(viewSince);
            x -= (1f - ua) * 40 * s;
            GUI.color = UA;

            if (inSettings)
            {
                var area = new Rect(x, h * 0.3f, Mathf.Min(w * 0.88f - x, SettingsPanel.MaxWidth), h * 0.56f);   // ultrawide: not stretched
                MenuSkin.DrawPanel(new Rect(area.x - 20 * s, area.y - 20 * s, area.width + 40 * s, area.height + 110 * s));
                settings.Draw(area, s);
                var back = new Rect(area.x, area.yMax + 20 * s, 300 * s, 60 * s);
                GUI.Label(back, "< BACK", back.Contains(Event.current.mousePosition) ? menuSelStyle : menuBtn);
                GUI.color = Color.white;
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && back.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    inSettings = false; settings.CancelWait();
                    MenuNav.Click();
                    PlayerPrefs.Save();
                }
                return;
            }

            if (confirmNew) { DrawConfirm(x, w, h, s); GUI.color = Color.white; return; }
            int n = MenuCount;
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(x, h * 0.36f + i * 72 * s, 420 * s, 66 * s);
                bool hover = r.Contains(Event.current.mousePosition);
                if (hover && menuSel != i && (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)) { menuSel = i; MenuNav.Click(); }
                bool on = i == menuSel;
                if (on)
                {
                    GUI.color = new Color(1f, 0.75f, 0.2f, 0.9f * ua);
                    GUI.DrawTexture(new Rect(r.x - 22 * s, r.y + 14 * s, 8 * s, r.height - 28 * s), Texture2D.whiteTexture);
                    GUI.color = UA;
                }
                GUI.Label(r, (on ? "  " : "") + MenuItems[i], on ? menuSelStyle : menuBtn);
                if (fade < 0.05f && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover)
                {
                    menuSel = i;
                    Event.current.Use();
                    Choose(i);
                }
            }
            var sm = MenuSkin.Notes;
            var g = UnityEngine.InputSystem.Gamepad.current;
            GUI.Label(new Rect(x, h * 0.36f + n * 72 * s + 16 * s, w * 0.5f, 40 * s),
                      g != null ? "Controller: " + g.displayName + "   (D-pad / stick, A = OK)" : "Mouse, or arrow keys + Enter. Controllers work too.", sm);
            GUI.Label(new Rect(x, h - 80 * s, w * 0.6f, 60 * s),
                      "CONTENT WARNING: crude adult humor, rude gestures, strong language, cartoon slime violence.", sm);
            GUI.color = Color.white;
        }

        /// <summary>"BIG GOAL COMPLETE" across the upper middle: fades in, holds, fades out (5 s).</summary>
        void DrawBanner(float w, float h, float s)
        {
            float left = bannerUntil - Time.unscaledTime;
            float a = MenuSkin.Appear(bannerUntil - 5f, 0.35f) * Mathf.Clamp01(left / 0.8f);
            var r = new Rect(w * 0.2f, h * 0.36f, w * 0.6f, 150 * s);
            GUI.color = new Color(1f, 1f, 1f, a);
            MenuSkin.DrawPanel(r);
            GUI.color = new Color(1f, 0.78f, 0.25f, 0.95f * a);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 4 * s, r.width, 4 * s), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(r.x, r.y + 10 * s, r.width, 80 * s), "BIG GOAL COMPLETE", MenuSkin.Title);
            GUI.Label(new Rect(r.x, r.y + 80 * s, r.width, 56 * s), bannerLine, MenuSkin.Hint);
            GUI.color = Color.white;
        }

        int menuView = -1, hintDrawn = -1;
        float hintSince;
        float viewSince, titleSince, ua = 1f;
        Color UA => new Color(1f, 1f, 1f, ua);

        /// <summary>NEW GAME: "are you sure" with YES / NO (mouse, keys, controller).</summary>
        void DrawConfirm(float x, float w, float h, float s)
        {
            var q = MenuSkin.Question;
            GUI.Label(new Rect(x, h * 0.36f, w * 0.5f, 170 * s),
                      "Start a NEW GAME?\nThis clears your progress, accomplishments, photo album, clues, weapons and big goals.\n" +
                      "Settings and key bindings stay.", q);
            string[] opts = { "YES, START OVER", "NO, GO BACK" };
            for (int i = 0; i < 2; i++)
            {
                var r = new Rect(x, h * 0.36f + 190 * s + i * 72 * s, 520 * s, 66 * s);
                bool hover = r.Contains(Event.current.mousePosition);
                if (hover && (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)) confirmSel = i;
                bool on = i == confirmSel;
                if (on)
                {
                    GUI.color = i == 0 ? new Color(1f, 0.35f, 0.25f, 0.9f * ua) : new Color(1f, 0.75f, 0.2f, 0.9f * ua);
                    GUI.DrawTexture(new Rect(r.x - 22 * s, r.y + 14 * s, 8 * s, r.height - 28 * s), Texture2D.whiteTexture);
                    GUI.color = UA;
                }
                GUI.Label(r, (on ? "  " : "") + opts[i], on ? menuSelStyle : menuBtn);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover)
                {
                    Event.current.Use();
                    Confirm(i == 0);
                    return;
                }
            }
        }

        void OnGUI()
        {
            if (PhotoAlbum.Capturing) return;
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            MenuSkin.Use(s);              // the shared menu look (title, pause, settings, hints)
            if (state == State.Title) DrawTitle(w, h, s);
            else if (state == State.Play && (hint < HintCount || combatHint >= 0) && !PauseMenu.Open)
            {
                int shownHint = hint < HintCount ? hint : HintCount + combatHint;
                // under the HUD compass. Touch: below the top button row (centres 0.1 H from the top, radius 0.075 H,
                // bigger if resized) and right of the top-left compass / health, clear of Pause and Jump on the right.
                // Combat tips: lower, under the NEXT goal line (it shows again after the room hints, up to 3 lines)
                bool touch = RMInput.LastDevice == RMInput.DeviceKind.Touch, combatTip = hint >= HintCount;
                var r = touch
                    ? new Rect(w * 0.26f, h * (combatTip ? 0.32f : 0.22f), w * 0.58f, 92 * s)
                    : new Rect(w * 0.18f, (combatTip ? 180 : 74) * s, w * 0.64f, 92 * s);
                // each new hint fades in and drops 16 px into place (unscaled: slow-mo does not stretch it)
                if (shownHint != hintDrawn) { hintDrawn = shownHint; hintSince = Time.unscaledTime; }
                float a = MenuSkin.Appear(hintSince, 0.3f);
                r.y -= (1f - a) * 16 * s;
                GUI.color = new Color(1f, 1f, 1f, a);
                MenuSkin.DrawPanel(r);
                GUI.color = new Color(1f, 0.78f, 0.25f, 0.95f * a);
                GUI.DrawTexture(new Rect(r.x, r.yMax - 4 * s, r.width, 4 * s), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.Label(r, hint < HintCount ? Hint(hint) : CombatHint(combatHint), MenuSkin.Hint);
                GUI.color = Color.white;
            }
            if (state == State.Play && Time.unscaledTime < bannerUntil && !PauseMenu.Open && !EndingSequence.Playing) DrawBanner(w, h, s);
            else if (state == State.Play && Time.unscaledTime >= statsFrom && Time.unscaledTime < statsUntil && !PauseMenu.Open) DrawStats(w, h, s);
            if (fade > 0.001f)
            {
                GUI.color = new Color(0, 0, 0, fade);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }
    }
}
