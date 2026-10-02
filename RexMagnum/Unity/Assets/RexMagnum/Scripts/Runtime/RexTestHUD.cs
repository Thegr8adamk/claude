using UnityEngine;
using UnityEngine.UI;

namespace RexMagnum
{
    /// <summary>
    /// HUD: crosshair + hit marker, health bar (smooth drain), action word, weapon + ammo, compass with the next goal,
    /// "NEXT:" objective line (GameGuide), Use prompts (pickups, guests, golf cart, elevator), damage flash and
    /// direction arc, subtitles and notices, a diamond + distance over the goal in the world, a "NEW GOAL" notice + click.
    /// The first 15 s say where the control list is (Pause > HELP); the room hints teach the controls.
    /// Touch: compass, objective and health move to the top left (the move stick and the top buttons need the space).
    /// </summary>
    public class RexTestHUD : MonoBehaviour
    {
        public RexCameraRig rig;
        public RexActions actions;
        public Health rexHealth;
        Text info, action, subtitle, hp, prompt;
        GameObject scope;
        float subtitleUntil;
        GameObject cross;
        bool paused, hudHidden;

        static RexTestHUD inst;
        static float hitUntil;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { inst = null; hitUntil = 0f; }      // editor with Domain Reload off

        /// <summary>Rex hit a blob: the crosshair hit marker shows for 0.1 s (weapons call this; blob damage by Rex too).</summary>
        public static void HitMarker() => hitUntil = Time.unscaledTime + 0.1f;

        /// <summary>A game message on its own line above the subtitles (shown also with subtitles off).</summary>
        public static void Notice(string text, float seconds)
        {
            if (inst == null || inst.notice == null) return;
            inst.notice.text = text;
            inst.noticeUntil = Time.unscaledTime + seconds;
        }

        void Awake()
        {
            // the menus' OS font (one look for menus and HUD); Unity's built-in one if the system has none of them
            var font = MenuSkin.UiFont != null ? MenuSkin.UiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;          // ultrawide and tall phones: text does not grow with width only
            // one line for the first 15 s: where the full control list is (the room hints teach the rest)
            info = MakeText(go.transform, font, new Vector2(0, 0.5f), new Vector2(30, 0), 22, TextAnchor.MiddleLeft);
            info.rectTransform.sizeDelta = new Vector2(900, 40);
            info.horizontalOverflow = HorizontalWrapMode.Overflow;
            hudRoot = go.GetComponent<RectTransform>();
            action = MakeText(go.transform, font, new Vector2(1, 0), new Vector2(-40, 40), 40, TextAnchor.LowerRight);
            subtitle = MakeText(go.transform, font, new Vector2(0.5f, 0), new Vector2(0, 120), 34, TextAnchor.LowerCenter);
            subtitle.rectTransform.sizeDelta = new Vector2(1600, 200);
            hp = MakeText(go.transform, font, new Vector2(0, 0), new Vector2(40, 72), 24, TextAnchor.LowerLeft);
            hp.rectTransform.sizeDelta = new Vector2(500, 34);
            RexVoice.Subtitle = line => ShowLine(line, 3f);
            RexVoice.SubtitleTimed = ShowLine;
            RexVoice.SubtitleCrowd = line => { if (Time.unscaledTime > subtitleUntil - 0.2f) ShowLine(line, 1.6f); };   // never over a real line
            weapon = MakeText(go.transform, font, new Vector2(1, 0), new Vector2(-40, 92), 26, TextAnchor.LowerRight);
            prompt = MakeText(go.transform, font, new Vector2(0.5f, 0.5f), new Vector2(0, -80), 28, TextAnchor.MiddleCenter);
            prompt.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            scope = new GameObject("Scope", typeof(RectTransform), typeof(Image));
            scope.transform.SetParent(go.transform, false);
            var srt = scope.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.sizeDelta = Vector2.zero;
            scope.GetComponent<Image>().sprite = ScopeSprite();
            scope.GetComponent<Image>().preserveAspect = false;
            scope.SetActive(false);
            cross = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            cross.transform.SetParent(go.transform, false);
            cross.GetComponent<RectTransform>().sizeDelta = new Vector2(6, 6);
            cross.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);      // a dot that shows on a white sky
            inst = this;
            var root = go.transform;
            notice = MakeText(root, font, new Vector2(0.5f, 0), new Vector2(0, 250), 30, TextAnchor.LowerCenter);
            notice.rectTransform.sizeDelta = new Vector2(1500, 120);
            notice.color = new Color(1f, 0.88f, 0.4f);
            objectiveText = MakeText(root, font, new Vector2(0.5f, 1), new Vector2(0, -84), 26, TextAnchor.UpperCenter);
            objectiveText.color = new Color(1f, 0.85f, 0.3f);
            BuildCompass(root, font);
            BuildWorldGoal(root, font);
            BuildHealthBar(root);
            BuildHitMarker(root);
            // damage: red screen edges + an arc that points at the attacker
            flash = MakeImage(root, "DamageFlash", Color.clear, EdgeSprite());
            flash.rectTransform.anchorMin = Vector2.zero;
            flash.rectTransform.anchorMax = Vector2.one;
            flash.rectTransform.sizeDelta = Vector2.zero;
            arc = MakeImage(root, "DamageArc", Color.clear, ArcSprite());
            arc.rectTransform.sizeDelta = new Vector2(520, 520);
            arc.transform.SetAsFirstSibling();          // under the text and the scope
            flash.transform.SetAsFirstSibling();
            ApplyLayout(false);
        }

        // ------------------------------------------------------------ new HUD parts

        Text notice, objectiveText, compassDist;
        float noticeUntil;
        Image flash, arc, hpFillImg;
        RectTransform compass, compassMarker, hpBar, hpFill, hpGhost;
        CanvasGroup markerGroup, hitGroup;
        readonly Text[] compassLabels = new Text[8];
        static readonly string[] Dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        const float HpW = 420f, CompassRange = 150f;      // health bar width; compass shows 150 degrees
        float compassW = 640f;

        static Image MakeImage(Transform parent, string name, Color c, Sprite sprite = null)
        {
            var g = new GameObject(name, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(parent, false);
            var im = g.GetComponent<Image>();
            im.sprite = sprite;
            im.color = c;
            im.raycastTarget = false;
            return im;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
        }

        void BuildCompass(Transform root, Font font)
        {
            var back = MakeImage(root, "Compass", new Color(0f, 0f, 0f, 0.5f));      // dark band: reads on a bright sky
            compass = back.rectTransform;
            var tick = MakeImage(compass, "Heading", Color.white);
            Place(tick.rectTransform, new Vector2(0.5f, 1f), Vector2.zero);
            tick.rectTransform.sizeDelta = new Vector2(3, 10);
            for (int i = 0; i < 8; i++)
            {
                var t = MakeText(compass, font, new Vector2(0.5f, 0.5f), Vector2.zero, i % 2 == 0 ? 28 : 18, TextAnchor.MiddleCenter);
                t.rectTransform.sizeDelta = new Vector2(60, 44);
                t.text = Dirs[i];
                compassLabels[i] = t;
            }
            var m = new GameObject("Goal", typeof(RectTransform), typeof(CanvasGroup));
            m.transform.SetParent(compass, false);
            compassMarker = m.GetComponent<RectTransform>();
            markerGroup = m.GetComponent<CanvasGroup>();
            var d = MakeImage(compassMarker, "Diamond", new Color(1f, 0.85f, 0.15f));
            d.rectTransform.sizeDelta = new Vector2(14, 14);
            d.rectTransform.anchoredPosition = new Vector2(0, -22);
            d.rectTransform.localEulerAngles = new Vector3(0, 0, 45f);
            d.gameObject.AddComponent<Outline>().effectColor = Color.black;
            compassDist = MakeText(compassMarker, font, new Vector2(0.5f, 0.5f), new Vector2(0, -42), 18, TextAnchor.MiddleCenter);
            compassDist.rectTransform.sizeDelta = new Vector2(140, 24);
            compassDist.color = new Color(1f, 0.85f, 0.3f);
        }

        RectTransform hudRoot, worldGoal;
        CanvasGroup worldGoalGroup;
        Text worldGoalDist;

        /// <summary>The goal in the world: a small diamond + distance over the goal while it is in front of the camera.</summary>
        void BuildWorldGoal(Transform root, Font font)
        {
            var g = new GameObject("WorldGoal", typeof(RectTransform), typeof(CanvasGroup));
            g.transform.SetParent(root, false);
            worldGoal = g.GetComponent<RectTransform>();
            worldGoal.sizeDelta = Vector2.zero;
            worldGoalGroup = g.GetComponent<CanvasGroup>();
            worldGoalGroup.alpha = 0f;
            worldGoalGroup.blocksRaycasts = false;
            var d = MakeImage(worldGoal, "Diamond", new Color(1f, 0.85f, 0.15f, 0.9f));
            d.rectTransform.sizeDelta = new Vector2(16, 16);
            d.rectTransform.localEulerAngles = new Vector3(0, 0, 45f);
            d.gameObject.AddComponent<Outline>().effectColor = Color.black;
            worldGoalDist = MakeText(worldGoal, font, new Vector2(0.5f, 0.5f), new Vector2(0, -24), 20, TextAnchor.MiddleCenter);
            worldGoalDist.rectTransform.sizeDelta = new Vector2(160, 26);
            worldGoalDist.color = new Color(1f, 0.85f, 0.3f);
        }

        void BuildHealthBar(Transform root)
        {
            var back = MakeImage(root, "HealthBar", new Color(0f, 0f, 0f, 0.6f));
            back.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.35f);   // edge on dark ground too
            hpBar = back.rectTransform;
            hpBar.sizeDelta = new Vector2(HpW + 6, 28);
            hpGhost = Bar(hpBar, new Color(1f, 0.95f, 0.8f, 0.85f)).rectTransform;   // the part just lost, drains slowly
            hpFillImg = Bar(hpBar, Color.green);
            hpFill = hpFillImg.rectTransform;
        }

        static Image Bar(RectTransform parent, Color c)
        {
            var im = MakeImage(parent, "Bar", c);
            var rt = im.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(3f, 0f);
            rt.sizeDelta = new Vector2(HpW, -6f);
            return im;
        }

        void BuildHitMarker(Transform root)
        {
            var g = new GameObject("HitMarker", typeof(RectTransform), typeof(CanvasGroup));
            g.transform.SetParent(root, false);
            hitGroup = g.GetComponent<CanvasGroup>();
            hitGroup.alpha = 0f;
            for (int i = 0; i < 4; i++)
            {
                // four short diagonal lines around the crosshair
                var im = MakeImage(g.transform, "Tick", Color.white);
                im.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                float sx = i % 2 == 0 ? 1f : -1f, sy = i < 2 ? 1f : -1f;
                im.rectTransform.anchoredPosition = new Vector2(sx * 13f, sy * 13f);
                im.rectTransform.sizeDelta = new Vector2(14f, 3f);
                im.rectTransform.localEulerAngles = new Vector3(0f, 0f, sx * sy * 45f);
            }
        }

        /// <summary>Red screen edges (clear in the middle), stretched over the screen.</summary>
        static Sprite EdgeSprite()
        {
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - s / 2f) / (s / 2f), dy = (y - s / 2f) / (s / 2f);
                float d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 0.6f + Mathf.Sqrt(dx * dx + dy * dy) * 0.3f;
                float a = Mathf.Clamp01((d - 0.5f) / 0.45f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        /// <summary>A ring piece at the top (60 degrees); the HUD turns it toward the attacker.</summary>
        static Sprite ArcSprite()
        {
            const int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - s / 2f) / (s / 2f), dy = (y - s / 2f) / (s / 2f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Abs(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);        // 0 = straight up
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.07f) * Mathf.Clamp01((30f - ang) / 10f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        bool layoutTouch;

        /// <summary>Touch: compass, objective and health go to the top left (move stick bottom left, buttons along the top).</summary>
        void ApplyLayout(bool touch)
        {
            layoutTouch = touch;
            compassW = touch ? 420f : 640f;
            compass.sizeDelta = new Vector2(compassW, 44);
            Place(compass, touch ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f), touch ? new Vector2(24, -10) : new Vector2(0, -8));
            // touch: the objective wraps under the compass (420 wide, up to 3 lines), clear of the top button row
            Place(objectiveText.rectTransform, touch ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f), touch ? new Vector2(24, -84) : new Vector2(0, -84));
            objectiveText.alignment = touch ? TextAnchor.UpperLeft : TextAnchor.UpperCenter;
            objectiveText.fontSize = touch ? 22 : 26;
            objectiveText.rectTransform.sizeDelta = new Vector2(touch ? 420 : 1400, touch ? 84 : 80);
            objectiveText.verticalOverflow = VerticalWrapMode.Overflow;       // the night line under a long goal still shows
            Place(hpBar, touch ? new Vector2(0f, 1f) : new Vector2(0f, 0f), touch ? new Vector2(24, -214) : new Vector2(40, 40));
            Place(hp.rectTransform, touch ? new Vector2(0f, 1f) : new Vector2(0f, 0f), touch ? new Vector2(24, -178) : new Vector2(40, 72));
        }

        static Sprite ScopeSprite()
        {
            const int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - s / 2f) / (s / 2f), dy = (y - s / 2f) / (s / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                bool cross = (Mathf.Abs(dx) < 0.006f || Mathf.Abs(dy) < 0.006f) && d < 0.8f;
                float a = d > 0.82f ? 1f : cross ? 0.9f : 0f;
                tex.SetPixel(x, y, new Color(0, 0, 0, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        Text weapon;
        WeaponId shownWeapon = (WeaponId)(-1);
        int shownAmmo = int.MinValue, shownHp = -1, shownCompassM = -1, shownWorldM = -1;

        void ShowLine(string line, float seconds)
        {
            if (!AudioMix.Subtitles) return;        // Settings > Sound > Subtitles
            subtitle.text = line;
            subtitleUntil = Time.unscaledTime + Mathf.Max(1.2f, seconds);
        }

        Text MakeText(Transform parent, Font font, Vector2 anchor, Vector2 pos, int size, TextAnchor align)
        {
            // black outline: the text stays readable on a bright sky, sand or a white wall
            var g = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
            var ol = g.GetComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
            ol.effectDistance = new Vector2(2f, -2f);
            g.transform.SetParent(parent, false);
            var rt = g.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(900, 400);
            var t = g.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            return t;
        }

        Canvas canvas;

        void OnEnable() => VRMode.Changed += OnVR;
        void OnDisable() => VRMode.Changed -= OnVR;

        /// <summary>VR: a screen overlay is not drawn in the headset, so the HUD moves onto a plane 1 m in front of the eyes.</summary>
        void OnVR(bool on)
        {
            if (canvas == null) canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null) return;
            canvas.renderMode = on ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = on && rig != null ? rig.cam : null;
            canvas.planeDistance = 0.3f;      // nearer than walls in the small room (the UI is depth-tested in VR)
            var sc = canvas.GetComponent<CanvasScaler>();
            if (sc != null) sc.referenceResolution = on ? new Vector2(2600, 1460) : new Vector2(1920, 1080);   // VR: keep text inside the view
        }

        void Update()
        {
            if (canvas == null) canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null && hudHidden != GameFlow.Busy)
            {
                // no HUD over the title / intro, but the subtitles stay (the wake-up line)
                hudHidden = GameFlow.Busy;
                foreach (Transform c in canvas.transform)
                    if (c != subtitle.transform && c != scope.transform) c.gameObject.SetActive(!hudHidden);
            }
            if (canvas != null) canvas.enabled = !PhotoAlbum.Capturing;
            if (false && RMInput.Pause)   // PauseMenu handles pause now
            {
                paused = !paused;
                Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = paused;
                rig.InputEnabled = !paused;
                actions.controller.InputEnabled = !paused;
            }
            bool touch = RMInput.LastDevice == RMInput.DeviceKind.Touch;
            if (touch != layoutTouch) ApplyLayout(touch);
            ControlList();
            action.text = actions.ActionWord;
            if (actions.weapons != null)
            {
                var w = actions.weapons;
                var d = w.Current;
                int ammo = d.isGun ? w.Ammo(d.id) : -1;
                // the string is built only when the weapon or the ammo changes (no garbage every frame)
                if (d.id != shownWeapon || ammo != shownAmmo)
                {
                    shownWeapon = d.id; shownAmmo = ammo;
                    weapon.text = d.isGun ? $"{d.name.ToUpper()}  {ammo}" : d.name.ToUpper();
                    weapon.color = d.color;
                }
            }
            if (PauseMenu.Open)
            {
                // paused: the subtitle and the notice wait (the voice clip is paused too, RexVoice.Freeze)
                subtitleUntil += Time.unscaledDeltaTime;
                noticeUntil += Time.unscaledDeltaTime;
            }
            if (Time.unscaledTime > subtitleUntil) subtitle.text = "";
            if (Time.unscaledTime > noticeUntil) notice.text = "";
            prompt.text = UsePrompt();
            if (actions.weapons != null) scope.SetActive(actions.weapons.Scoped);
            HealthBar();
            action.color = actions.ActionWord == "Talk" ? new Color(0.5f, 0.9f, 1f) : new Color(1f, 0.6f, 0.2f);
            HookBlobs();
            hitGroup.alpha = Time.unscaledTime < hitUntil ? 1f : 0f;
            Damage();
            Objective();
            Compass();
        }

        // ------------------------------------------------------------ where the controls are (first 15 s of play)

        /// <summary>No control list over the first minute: the room hints teach the controls one at a time, and
        /// Pause > HELP has the full list. One line says so for 15 s.</summary>
        void ControlList()
        {
            float t = GameFlow.PlaySeconds;
            if (GameFlow.Busy || t > 15f) { info.text = ""; return; }
            info.color = new Color(1f, 1f, 1f, Mathf.Clamp01((15f - t) / 2f));      // fades out at the end
            string p = RMInput.LastDevice == RMInput.DeviceKind.Touch ? "II button" : Glyph.B("Pause");
            info.text = $"{p}: pause.  Pause > HELP shows all controls.";
        }

        // ------------------------------------------------------------ health bar

        float hpShown = 1f, hpLost = 1f, hpLostHold;

        void HealthBar()
        {
            bool on = rexHealth != null;
            hpBar.gameObject.SetActive(on && !hudHidden);
            if (!on) { hp.text = ""; return; }
            float f = rexHealth.Fraction;
            hpShown = Mathf.Lerp(hpShown, f, 1f - Mathf.Exp(-12f * Time.deltaTime));     // quick, smooth
            if (f >= hpLost) hpLost = f;                                                  // healing: no white part
            else if (Time.time > hpLostHold) hpLost = Mathf.MoveTowards(hpLost, f, Time.deltaTime * 0.5f);
            hpFill.sizeDelta = new Vector2(HpW * hpShown, -6f);
            hpGhost.sizeDelta = new Vector2(HpW * hpLost, -6f);
            var c = Color.HSVToRGB(Mathf.Lerp(0f, 0.33f, f), 0.85f, 1f);                 // red -> yellow -> green
            if (f < 0.25f && f > 0f) c = Color.Lerp(c, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * 10f)));   // low: pulse
            hpFillImg.color = c;
            int hpNow = Mathf.CeilToInt(rexHealth.Current);
            if (hpNow != shownHp) { shownHp = hpNow; hp.text = "HEALTH  " + hpNow; }
            hp.color = Color.white;
        }

        // ------------------------------------------------------------ hits and damage

        readonly System.Collections.Generic.HashSet<Blob> hooked = new System.Collections.Generic.HashSet<Blob>();

        /// <summary>Hit marker for every weapon (also the harpoon and the coconut): listen to each blob's damage.</summary>
        void HookBlobs()
        {
            if (Time.frameCount % 15 != 0) return;
            if (hooked.Count > 400) hooked.RemoveWhere(b => b == null);
            foreach (var b in Blob.All)
            {
                if (b == null || hooked.Contains(b)) continue;
                hooked.Add(b);
                var h = b.GetComponent<Health>();
                if (h != null) h.Damaged += OnBlobDamaged;
            }
        }

        static void OnBlobDamaged(float amount, Vector3 point, GameObject attacker)
        {
            if (attacker != null && attacker.GetComponentInParent<RexController>() != null) HitMarker();
        }

        float flashA, arcA;
        Vector3 dmgFrom;

        void Start()
        {
            if (rexHealth != null) rexHealth.Damaged += OnRexDamaged;
        }

        void OnDestroy()
        {
            if (rexHealth != null) rexHealth.Damaged -= OnRexDamaged;
            if (inst == this) inst = null;
        }

        void OnRexDamaged(float amount, Vector3 point, GameObject attacker)
        {
            flashA = Mathf.Clamp(0.35f + amount / 40f, 0.35f, 0.8f);
            hpLostHold = Time.time + 0.5f;
            // the attacker's position (blob melee passes Rex's own position as the point)
            Vector3 src = attacker != null ? attacker.transform.position : point;
            Vector3 d = src - rexHealth.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.25f) { dmgFrom = src; arcA = 1f; }
        }

        Camera Cam => rig != null && rig.cam != null ? rig.cam : Camera.main;

        void Damage()
        {
            flashA = Mathf.MoveTowards(flashA, 0f, Time.deltaTime * 1.4f);
            float low = rexHealth != null && !rexHealth.IsDead && rexHealth.Fraction < 0.25f
                ? 0.12f + 0.08f * Mathf.Sin(Time.time * 4f) : 0f;                       // low health: a soft red pulse
            flash.color = new Color(0.9f, 0.05f, 0.02f, Mathf.Max(flashA, low));
            arcA = Mathf.MoveTowards(arcA, 0f, Time.deltaTime * 0.8f);
            var cam = Cam;
            if (arcA > 0f && cam != null)
            {
                // follows the attacker while Rex turns
                Vector3 d = dmgFrom - cam.transform.position;
                Vector3 fwd = cam.transform.forward; fwd.y = 0f;
                float a = Mathf.Atan2(Vector3.Dot(d, cam.transform.right), Vector3.Dot(d, fwd.normalized)) * Mathf.Rad2Deg;
                arc.rectTransform.localEulerAngles = new Vector3(0f, 0f, -a);
            }
            arc.color = new Color(1f, 0.15f, 0.1f, arcA * 0.9f);
        }

        // ------------------------------------------------------------ objective + compass

        string objective;
        Vector3 objectiveAt;
        bool objectiveHas;
        float nextObjective;

        Transform RexT => actions != null && actions.controller != null ? actions.controller.transform : rig != null ? rig.target : null;

        void Objective()
        {
            if (Time.unscaledTime < nextObjective) return;
            nextObjective = Time.unscaledTime + 0.5f;
            var rex = RexT;
            // no goal over the title / intro or the room hints: the first goal is announced when the tutorial ends
            string warning = null, side = null;
            if (GameFlow.Busy || GameFlow.TutorialActive || rex == null) { objective = null; objectiveHas = false; }
            else { objective = GameGuide.Objective(actions.weapons, rex.position, out objectiveAt, out objectiveHas); warning = GameGuide.Warning; side = GameGuide.Side; }
            // the bar defense set piece: its own line while the waves come (the step id stays: no NEW GOAL after it)
            if (objective != null && BarActive(out int wave, out int waves))
            {
                objective = waves > 0 ? $"Defend the bar! Wave {Mathf.Max(1, wave)}/{waves}." : "Defend the bar!";
                side = null;
            }
            // the night warning is its own line under the goal (blue): the goal stays, so it is not announced again
            if (warning != null && layoutTouch) warning = "NIGHT: stay near lights.";
            if (side != null && layoutTouch) side = "Side: sniper rifle on the hotel roof.";
            // built only when one of the parts changed (twice a second otherwise: small garbage, but none needed)
            if (objective != shownObjective || warning != shownWarning || side != shownSide)
            {
                shownObjective = objective; shownWarning = warning; shownSide = side;
                objectiveText.text = objective == null ? "" : "NEXT: " + objective
                    + (warning != null ? "\n<color=#9CCBFF>" + warning + "</color>" : "")
                    + (side != null ? "\n<color=#B8E6A0>" + side + "</color>" : "");
            }
            // the notice once per night (GameGuide.Warning only comes on the first night ever: Progress "nights");
            // it may show again the next night only after real daylight (no repeat when Night wobbles near 0.6)
            if (warning != null && !nightShown) { nightShown = true; Notice(warning, 4f); MenuNav.Click(); }
            else if (warning == null && DayNight.Night < 0.2f) nightShown = false;
            AnnounceGoal(objective != null ? GameGuide.StepId : null);
        }

        static System.Type barType;
        static bool barLooked;

        /// <summary>BarDefense.Active / Wave / Waves, read by name (static field or property): the HUD compiles and
        /// works without the bar set piece in the build.</summary>
        static bool BarActive(out int wave, out int waves)
        {
            wave = 0; waves = 0;
            if (!barLooked) { barLooked = true; barType = System.Type.GetType("RexMagnum.BarDefense"); }
            if (barType == null || !(BarStatic("Active") is bool on) || !on) return false;
            if (BarStatic("Wave") is int wv) wave = wv;
            if (BarStatic("Waves") is int wn) waves = wn;
            return true;
        }

        static object BarStatic(string name)
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
            var pi = barType.GetProperty(name, F);
            if (pi != null) return pi.GetValue(null, null);
            var fi = barType.GetField(name, F);
            return fi != null ? fi.GetValue(null) : null;
        }

        string goalShown, goalPending, shownObjective, shownWarning, shownSide;
        float goalPendingSince;
        bool nightShown;

        /// <summary>A new goal: a short notice + click. The step must hold for 1.5 s first (guests that stream in and
        /// out would flip "rescue" and the next step back and forth).</summary>
        void AnnounceGoal(string step)
        {
            if (step == null || step == goalShown) { goalPending = null; return; }
            if (step != goalPending) { goalPending = step; goalPendingSince = Time.unscaledTime; return; }
            if (Time.unscaledTime - goalPendingSince < 1.5f) return;
            Notice((goalShown == null ? "GOAL: " : "NEW GOAL: ") + objective, 4f);
            goalShown = step;
            goalPending = null;
            Sfx.PlayUI("ui_click", 0.22f, 0.8f);       // soft and low: a cue, not a menu click
        }

        /// <summary>Diamond + distance over the goal while it is in front of the camera (not too close, not in the scope).</summary>
        void WorldGoal(Camera cam, Transform rex)
        {
            bool show = objectiveHas && rex != null && cam != null && !hudHidden && (actions.weapons == null || !actions.weapons.Scoped);
            Vector3 at = objectiveAt;
            if (show)
            {
                // zone centres have no height (y = 0): float the mark 3 m over Rex's height there
                if (Mathf.Approximately(at.y, 0f)) at.y = rex.position.y + 3f; else at.y += 2.2f;
                Vector3 flat = at - rex.position; flat.y = 0f;
                Vector3 sp = cam.WorldToScreenPoint(at);
                show = flat.magnitude > 8f && sp.z > 0f && sp.x > 0f && sp.x < Screen.width && sp.y > 0f && sp.y < Screen.height;
                if (show && RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot, sp,
                        canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null, out var local))
                {
                    worldGoal.anchoredPosition = local;
                    int m = Mathf.RoundToInt(flat.magnitude);
                    if (m != shownWorldM) { shownWorldM = m; worldGoalDist.text = m + " m"; }
                }
                else show = false;
            }
            worldGoalGroup.alpha = show ? 0.9f : 0f;
        }

        void Compass()
        {
            var cam = Cam;
            if (cam == null) return;
            float yaw = cam.transform.eulerAngles.y;          // 0 = north (+Z), 90 = east (+X)
            float px = compassW / CompassRange;
            for (int i = 0; i < 8; i++)
            {
                float delta = Mathf.DeltaAngle(yaw, i * 45f);
                var t = compassLabels[i];
                t.rectTransform.anchoredPosition = new Vector2(delta * px, 0f);
                float a = Mathf.Clamp01((CompassRange * 0.5f - Mathf.Abs(delta)) / 12f);   // fade at the ends
                t.color = i == 0 ? new Color(1f, 0.45f, 0.35f, a) : new Color(1f, 1f, 1f, a);
            }
            var rex = RexT;
            WorldGoal(cam, rex);
            markerGroup.alpha = objectiveHas && rex != null ? 1f : 0f;
            if (!objectiveHas || rex == null) return;
            Vector3 to = objectiveAt - rex.position;
            to.y = 0f;
            float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float dm = Mathf.Clamp(Mathf.DeltaAngle(yaw, bearing), -CompassRange * 0.5f, CompassRange * 0.5f);   // off the strip: at its end
            compassMarker.anchoredPosition = new Vector2(dm * px, 0f);
            int dm2 = Mathf.RoundToInt(to.magnitude);
            if (dm2 != shownCompassM) { shownCompassM = dm2; compassDist.text = dm2 + " m"; }
        }

        // ------------------------------------------------------------ Use prompts

        GolfCart[] carts;
        Elevator[] elevators;
        float nextFind, drivingSince = -1f;

        /// <summary>What Use (or Action) does here: pickups, guests (rescue / knock / talk), golf cart, elevator.</summary>
        string UsePrompt()
        {
            int kind = PromptKind(out Object subj);
            // the string is built only when what is near (or the device / key names) changes: no garbage every frame
            var dev = RMInput.LastDevice;
            // the in-cab prompt names the next floor: refreshed 4 times a second while Rex rides
            bool stale = kind == 8 && Time.unscaledTime > promptRefresh;
            if (kind == promptKind && subj == promptSubj && dev == promptDevice && !stale && !PauseMenu.Open) return promptText;
            promptKind = kind; promptSubj = subj; promptDevice = dev;
            promptRefresh = Time.unscaledTime + 0.25f;
            promptText = BuildPrompt(kind, subj);
            return promptText;
        }

        int promptKind = -1;
        float promptRefresh;
        Object promptSubj;
        RMInput.DeviceKind promptDevice;
        string promptText = "";

        string BuildPrompt(int kind, Object subj)
        {
            string U = Glyph.B("Use");
            var g = subj as Guest;
            switch (kind)
            {
                case 1: return Glyph.Swap(((WeaponPickup)subj).Label, "Use");
                case 2: return Glyph.Swap(((FoodPickup)subj).Label, "Use");
                case 3: return $"{U}: get out of the cart";
                case 4: return $"{U}: knock on the door";
                case 5: return $"Pop the blobs near {g.guestName} first, then {U}: rescue";
                case 6: return $"{U}: rescue {g.guestName}";
                case 7: return $"{U}: drive the golf cart";
                case 8: return $"{U}: {((Elevator)subj).CabPrompt}";
                case 9: return $"{U}: call the elevator";
                case 10: return $"{Glyph.B("Action")}: talk to {g.guestName}";
                case 11: return $"{Glyph.B("Hands")}: hands down to talk to {g.guestName}";
                case 12: return $"{U}: calm down {g.guestName}";
                default: return "";
            }
        }

        /// <summary>What Use (or Action) does here as a kind + the thing it is about (0 = nothing).</summary>
        int PromptKind(out Object subj)
        {
            subj = null;
            if (WeaponPickup.Nearby != null) { subj = WeaponPickup.Nearby; return 1; }
            if (FoodPickup.Nearby != null) { subj = FoodPickup.Nearby; return 2; }
            var rex = RexT;
            if (rex == null || GameFlow.Busy) return 0;
            if (GolfCart.Driven != null)
            {
                if (drivingSince < 0f) drivingSince = Time.time;
                return Time.time - drivingSince < 8f ? 3 : 0;
            }
            drivingSince = -1f;
            if (Time.unscaledTime > nextFind)
            {
                // cached: found again every 10 s (zones can load later)
                nextFind = Time.unscaledTime + 10f;
                carts = FindObjectsByType<GolfCart>(FindObjectsSortMode.None);
                elevators = FindObjectsByType<Elevator>(FindObjectsSortMode.None);
            }
            Vector3 p = rex.position;
            Guest near = null;
            float nearD = 3f;
            foreach (var g in Guest.All)
            {
                if (g == null || g.Rescued) continue;
                float d = Vector3.Distance(g.transform.position, p);
                if (d < nearD) { nearD = d; near = g; }
            }
            subj = near;
            if (near != null && near.LockedIn) return 4;
            if (near != null && near.CanRescue)
                return Blob.LivingNear(near.transform.position, Guest.SafeRadius) > 0 ? 5 : near.IsCalm ? 12 : 6;
            subj = null;
            foreach (var c in carts)
                if (c != null && Vector3.Distance(c.transform.position, p) < 3f) return 7;
            foreach (var e in elevators)
            {
                if (e == null || e.cab == null) continue;
                Vector3 l = e.cab.InverseTransformPoint(p + Vector3.up * 0.5f);
                if (Mathf.Abs(l.x) < e.cabHalfSize.x && l.y > -0.3f && l.y < e.cabHalfSize.y * 2f && Mathf.Abs(l.z) < e.cabHalfSize.z)
                { subj = e; return 8; }
                if (e.landingDoors != null)
                    foreach (var door in e.landingDoors)
                        if (door != null && Vector3.Distance(door.position, p) < 3f) return 9;
            }
            subj = near;
            if (near != null && nearD < 2.5f)
                return actions.controller != null && actions.controller.HandsDown ? 10 : 11;
            subj = null;
            return 0;
        }
    }
}
