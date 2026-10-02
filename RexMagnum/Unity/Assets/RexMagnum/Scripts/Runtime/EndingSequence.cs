using UnityEngine;

namespace RexMagnum
{
    /// <summary>The ending (brief section 2), about 26 s, skippable after 3 s:
    ///  1. a guest's phone video: Rex slips on slime (portrait phone frame, REC, the view count climbs to 41 million);
    ///  2. Marty calls: the floor-cleaner TV ad;
    ///  3. the resort's bar-tab receipt with the damage on it;
    ///  4. Rex in his hero pose: "I'm back."
    /// Then free play continues.
    /// VR: the headset camera is never taken over (a moving camera the head does not control makes people sick):
    /// the same story plays from Rex's own view as a dimmed view + subtitles / notices on the HUD.</summary>
    public class EndingSequence : MonoBehaviour
    {
        float t;
        Camera cam;
        /// <summary>True while the ending plays (GolfCart: no driving input, Rex may be at the wheel).</summary>
        public static bool Playing { get; private set; }

        /// <summary>The ending has played (ended or skipped) in this scene: GameFlow's King Glorp banner and the
        /// stats card wait for it (King Glorp pops ~5 s before the ending starts). Cleared on every scene load.</summary>
        public static bool Finished { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Playing = false; Finished = false; }      // editor with Domain Reload off

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void HookSceneLoad()
        {
            // QUIT TO TITLE / NEW GAME reload the scene but statics stay: a new run has not seen the ending (-= first: once)
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Finished = false;
        RexController rex;
        bool slipped, heroed;
        int lastCall = -1, nextCall;
        // the call is timed by its voice clips (Marty's long line is 5 s): the receipt and "I'm back" follow it
        float nextCallAt = 7f, tReceipt = 99f, tBack = 99f, tEnd = 99f;
        AudioSource voice;

        static readonly string[] Marty =
        {
            "MARTY: Rex, babe! You're trending!",
            "MARTY: Floor cleaner. National TV ad. They want the slip. The SLIP, Rex.",
            "REX: Do I get a trailer?",
            "MARTY: You get a mop. Love ya, babe!",
        };
        static readonly (string item, string price)[] Tab =
        {
            ("Bar tab (carried over)", "$11,480.00"), ("Lobby glass, 3 panes", "$2,950.00"), ("Vending machine (face)", "$1,200.00"),
            ("Golf cart, battery abuse", "$640.00"), ("Pool cleaning (slime, King-size)", "$8,800.00"),
            ("Frozen swordfish, used", "$310.00"), ("Mini-bar, room 304", "$497.50"),
        };

        void Start()
        {
            Playing = true;
            rex = FindAnyObjectByType<RexController>();
            if (rex != null)
            {
                rex.InputEnabled = false;
                // Rex stands still for the whole ending: no damage (a slime shot / shockwave still in the air, a blob off the
                // stun list); renewed every frame in Update, so it lasts as long as the ending (its length depends on the clips)
                rexHealth = rex.GetComponent<Health>();
                if (rexHealth != null) rexHealth.Invulnerable(2f);
                // Rex cannot move in the cutscene: blobs still near him stop for its length (about 26 s)
                foreach (var b in Blob.All)
                    if (b != null && !b.IsDead && Vector3.Distance(b.transform.position, rex.transform.position) < 40f) { b.Stun(45f); stunned.Add(b); }
            }
            var main = Camera.main;
            if (main == null)                                   // the kill cam may still have it switched off
            {
                var mg = GameObject.FindGameObjectWithTag("MainCamera");
                if (mg != null) main = mg.GetComponent<Camera>();
            }
            vr = VRMode.Active;
            if (vr)
            {
                // no camera of our own: a black panel in front of the player's eyes fades in and out
                voice = gameObject.AddComponent<AudioSource>();
                voice.spatialBlend = 0f;
                var cg = new GameObject("EndingFade", typeof(Canvas));
                cg.transform.SetParent(transform, false);
                var cv = cg.GetComponent<Canvas>();
                cv.renderMode = main != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
                cv.worldCamera = main;
                cv.planeDistance = 0.35f;                      // behind the HUD (0.3 m): the subtitles stay readable
                cv.sortingOrder = -10;
                var img = new GameObject("Black", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                img.transform.SetParent(cg.transform, false);
                var rt = img.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                vrFade = img.GetComponent<UnityEngine.UI.Image>();
                vrFade.raycastTarget = false;
                vrFade.color = new Color(0, 0, 0, 0);
                RexTestHUD.Notice("GUEST VIDEO: Rex slips on slime.  41,000,000 views  #maximumjustice #fail", 6.5f);
                return;
            }
            cam = new GameObject("EndingCam").AddComponent<Camera>();
            voice = cam.gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0f;
            if (main != null)
            {
                cam.CopyFrom(main);
                cam.depth = main.depth + 20f;
                // CopyFrom does not copy URP's camera data: same post-processing and anti-aliasing as the game
                if (main.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var md))
                {
                    if (!cam.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var ed))
                        ed = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    ed.renderPostProcessing = md.renderPostProcessing;
                    ed.antialiasing = md.antialiasing;
                }
                // the ending covers the whole view: do not render the game camera too (its AudioListener stays on)
                main.enabled = false;
                hiddenMain = main;
            }
            cam.enabled = true;
            cam.fieldOfView = 50f;
        }

        Camera hiddenMain;
        Health rexHealth;
        bool vr, vrReceipt;
        UnityEngine.UI.Image vrFade;

        readonly System.Collections.Generic.List<Blob> stunned = new System.Collections.Generic.List<Blob>();

        void OnDestroy()
        {
            Playing = false;
            Finished = true;
            if (cam != null) Destroy(cam.gameObject);
            if (hiddenMain != null) hiddenMain.enabled = true;      // the game view is back
            foreach (var b in stunned) if (b != null && !b.IsDead) b.Stun(0f);     // ending skipped: blobs move again with Rex
            // still off if he is at the golf cart's wheel or the pause menu is open (either may change during the ending)
            if (rex != null) rex.InputEnabled = GolfCart.Driven == null && !PauseMenu.Open;
        }

        void Update()
        {
            if (hiddenMain != null && hiddenMain.enabled) hiddenMain.enabled = false;   // a kill cam ending late turned it back on
            if (rexHealth != null) rexHealth.Invulnerable(2f);                           // ends ~2 s after the ending
            if (PauseMenu.Open) return;      // wait behind the pause menu (it runs on unscaled time; keys there must not skip it)
            t += Time.unscaledDeltaTime;
            if (t > 3f && RMInput.AnyKey && t < tBack)                       // skip to "I'm back."
            {
                if (voice != null) voice.Stop();
                tReceipt = tBack = t;
                tEnd = Mathf.Max(t, 7f) + 4f;      // the hero shot comes after the phone video
                lastCall = nextCall = Marty.Length;
            }
            if (t >= 7f && nextCall < Marty.Length && t >= nextCallAt)
            {
                lastCall = nextCall++;
                float len = PlayLine($"Voice/Ending/call_{lastCall + 1:00}");
                nextCallAt = t + len + 0.3f;
                if (vr) RexVoice.ShowSubtitle(Marty[lastCall], len + 0.3f);   // VR: no IMGUI in the headset
            }
            else if (nextCall == Marty.Length && tReceipt > 98f && t >= nextCallAt)
            {
                tReceipt = t;
                tBack = t + 8f;
                tEnd = tBack + 4f;
            }
            if (vr) { UpdateVR(); return; }
            if (rex == null) { if (t > tEnd) Destroy(gameObject); return; }
            Vector3 p = rex.transform.position, f = rex.transform.forward, r = rex.transform.right;
            if (t < 7f)
            {
                // the phone video: hand-held, low, a little shaky, zooming in
                if (!slipped && t > 1.2f) { slipped = true; rex.SetTrigger(Animator.StringToHash("Slip")); }
                float zoom = Mathf.Lerp(5f, 3f, t / 7f);
                cam.transform.position = p + f * zoom + r * 1.2f + Vector3.up * 1.1f + Random.insideUnitSphere * 0.015f;
                cam.transform.LookAt(p + Vector3.up * 0.8f);
            }
            else if (t < tBack)
            {
                // the call and the receipt: slow push-in on Rex
                float k = Mathf.Clamp01((t - 7f) / 15f);
                cam.transform.position = p + f * Mathf.Lerp(3f, 2f, k) + r * 0.6f + Vector3.up * 1.6f;
                cam.transform.LookAt(p + Vector3.up * 1.5f);
            }
            else
            {
                // "I'm back.": low hero angle, hero pose
                if (!heroed) { heroed = true; rex.SetTrigger(Animator.StringToHash("Victory")); RexVoice.Subtitle?.Invoke("REX: I'm back."); PlayLine("Voice/Ending/imback_01"); }
                cam.transform.position = p + f * 3.2f - r * 0.8f + Vector3.up * 0.5f;
                cam.transform.LookAt(p + Vector3.up * 1.4f);
            }
            if (t > tEnd) Destroy(gameObject);
        }

        /// <summary>VR: Rex's own view, dimmed (fade in 0.6 s, out over the last second), the story as HUD text.</summary>
        void UpdateVR()
        {
            if (vrFade != null)
            {
                float a = Mathf.Min(Mathf.Clamp01(t / 0.6f), Mathf.Clamp01(tEnd - t)) * 0.85f;
                vrFade.color = new Color(0, 0, 0, a);
            }
            if (rex != null && !slipped && t > 1.2f) { slipped = true; rex.SetTrigger(Animator.StringToHash("Slip")); }
            if (!vrReceipt && t >= tReceipt && t < tBack)
            {
                vrReceipt = true;
                RexTestHUD.Notice("ISLA GRANDE RESORT: the damage has been added to your tab.  TOTAL DUE: $25,877.50", 6f);
            }
            if (t >= tBack && !heroed)
            {
                heroed = true;
                if (rex != null) rex.SetTrigger(Animator.StringToHash("Victory"));
                RexVoice.Subtitle?.Invoke("REX: I'm back.");
                PlayLine("Voice/Ending/imback_01");
            }
            if (t > tEnd) Destroy(gameObject);
        }

        /// <summary>Plays a line on the ending's own voice source (Voices volume, stops on skip); returns its length.</summary>
        float PlayLine(string path)
        {
            var clip = Resources.Load<AudioClip>(path);
            if (clip == null) return 1.75f;
            if (voice != null) { voice.PlayOneShot(clip, AudioMix.Voice); RexVoice.Speaking(clip.length); }
            return clip.length;
        }

        GUIStyle big, mid, mono, head, monoCenter, tot;

        void OnGUI()
        {
            if (PauseMenu.Open || vr) return;
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            // styles made once (a new GUIStyle per OnGUI call is garbage every frame), sizes set per call
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold };
                mid = new GUIStyle(big);
                mono = new GUIStyle(GUI.skin.label) { font = null };
                head = new GUIStyle(mono) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                monoCenter = new GUIStyle(mono) { alignment = TextAnchor.MiddleCenter };
                tot = new GUIStyle(head) { alignment = TextAnchor.MiddleLeft };
            }
            big.fontSize = (int)(44 * s);
            mid.fontSize = (int)(30 * s);
            mono.fontSize = monoCenter.fontSize = (int)(26 * s);
            head.fontSize = tot.fontSize = (int)(30 * s);
            if (t < 7f)
            {
                // portrait phone frame around the middle of the screen
                float pw = h * 0.5f, ph = h * 0.9f, px = (w - pw) / 2f, py = h * 0.05f;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, px, h), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(px + pw, 0, w - px - pw, h), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(px, 0, pw, py), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(px, py + ph, pw, h - py - ph), Texture2D.whiteTexture);
                GUI.color = Mathf.Repeat(t, 1f) < 0.6f ? Color.red : new Color(1, 0, 0, 0.3f);
                GUI.DrawTexture(new Rect(px + 20 * s, py + 20 * s, 18 * s, 18 * s), Texture2D.whiteTexture);
                GUI.color = Color.white;
                mono.normal.textColor = Color.white;
                GUI.Label(new Rect(px + 46 * s, py + 12 * s, pw, 34 * s), "REC", mono);
                long views = (long)(41_000_000 * Mathf.Pow(Mathf.Clamp01((t - 2f) / 5f), 3f));
                big.normal.textColor = Color.white;
                GUI.Label(new Rect(px, py + ph - 200 * s, pw, 60 * s), $"{views:N0} views", big);
                mid.normal.textColor = new Color(0.8f, 0.9f, 1f);
                GUI.Label(new Rect(px, py + ph - 140 * s, pw, 120 * s), "washed-up action star vs. slime #maximumjustice #fail", mid);
                return;
            }
            GUI.color = new Color(0, 0, 0, 0.7f);
            if (t < tReceipt)
            {
                GUI.DrawTexture(new Rect(0, h * 0.72f, w, h * 0.2f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                int i = Mathf.Clamp(lastCall, 0, Marty.Length - 1);
                mid.normal.textColor = new Color(1f, 0.85f, 0.4f);
                GUI.Label(new Rect(w * 0.1f, h * 0.72f, w * 0.8f, h * 0.2f), (t < 7.8f ? "(phone rings) " : "") + Marty[i], mid);
                return;
            }
            if (t < tBack)
            {
                // the receipt
                float rw = h * 0.55f, rh = h * 0.8f, rx = w * 0.62f, ry = h * 0.1f;
                GUI.color = new Color(0.97f, 0.96f, 0.9f);
                GUI.DrawTexture(new Rect(rx, ry, rw, rh), Texture2D.whiteTexture);
                GUI.color = Color.white;
                mono.normal.textColor = new Color(0.1f, 0.1f, 0.1f);
                head.normal.textColor = monoCenter.normal.textColor = tot.normal.textColor = mono.normal.textColor;
                GUI.Label(new Rect(rx, ry + 20 * s, rw, 40 * s), "ISLA GRANDE RESORT", head);
                GUI.Label(new Rect(rx, ry + 60 * s, rw, 30 * s), "Guest: R. MAGNUM, room 304", monoCenter);
                int shown = Mathf.Clamp((int)((t - tReceipt) / 0.6f), 0, Tab.Length);
                for (int i = 0; i < shown; i++)
                {
                    GUI.Label(new Rect(rx + 24 * s, ry + (120 + i * 46) * s, rw * 0.65f, 40 * s), Tab[i].item, mono);
                    GUI.Label(new Rect(rx + rw * 0.68f, ry + (120 + i * 46) * s, rw * 0.3f, 40 * s), Tab[i].price, mono);
                }
                if (shown == Tab.Length)
                {
                    GUI.Label(new Rect(rx + 24 * s, ry + rh - 90 * s, rw, 40 * s), "TOTAL DUE:  $25,877.50", tot);
                    GUI.Label(new Rect(rx + 24 * s, ry + rh - 50 * s, rw, 34 * s), "\"The damage has been added to your tab.\"", mono);
                }
                return;
            }
            GUI.color = Color.white;
            big.fontSize = (int)(110 * s);
            big.normal.textColor = new Color(1f, 0.8f, 0.25f);
            GUI.Label(new Rect(0, h * 0.62f, w, h * 0.3f), "I'M BACK.", big);
        }
    }
}
