using UnityEngine;
using UnityEngine.UI;

namespace RexMagnum
{
    /// <summary>Test-scene HUD: crosshair, camera mode, action word and the control list.</summary>
    public class RexTestHUD : MonoBehaviour
    {
        public RexCameraRig rig;
        public RexActions actions;
        public Health rexHealth;
        Text info, action, subtitle, hp, prompt;
        GameObject scope;
        float subtitleUntil;
        GameObject cross;
        bool paused;

        void Awake()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            info = MakeText(go.transform, font, new Vector2(0, 1), new Vector2(30, -30), 24, TextAnchor.UpperLeft);
            action = MakeText(go.transform, font, new Vector2(1, 0), new Vector2(-40, 40), 40, TextAnchor.LowerRight);
            subtitle = MakeText(go.transform, font, new Vector2(0.5f, 0), new Vector2(0, 120), 34, TextAnchor.LowerCenter);
            subtitle.rectTransform.sizeDelta = new Vector2(1600, 200);
            hp = MakeText(go.transform, font, new Vector2(0, 0), new Vector2(40, 40), 36, TextAnchor.LowerLeft);
            RexVoice.Subtitle = line => { subtitle.text = line; subtitleUntil = Time.time + 3f; };
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

        Text MakeText(Transform parent, Font font, Vector2 anchor, Vector2 pos, int size, TextAnchor align)
        {
            var g = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Shadow));
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

        void Update()
        {
            if (canvas == null) canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null) canvas.enabled = !GameFlow.Busy;     // no HUD over the title / intro
            if (false && RMInput.Pause)   // PauseMenu handles pause now
            {
                paused = !paused;
                Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = paused;
                rig.InputEnabled = !paused;
                actions.controller.InputEnabled = !paused;
            }
            string B(string a) => RMInput.Btn(a);
            bool kb = RMInput.LastDevice == RMInput.DeviceKind.Keyboard;
            info.text = $"REX MAGNUM\nCamera: {(rig.Mode == RexCameraRig.CameraMode.FirstPerson ? "FIRST person" : "THIRD person")}  ({B("Camera")})\n" +
                        (kb ? $"{B("Forward")}{B("Left")}{B("Back")}{B("Right")} move · {B("Run")} run · {B("Jump")} jump · Mouse look\n"
                            : $"Left stick move · {B("Run")} run · {B("Jump")} jump · right stick look\n") +
                        $"{B("Action")}: tap = light, hold = heavy · {B("Wheel")}: weapon wheel\n" +
                        $"{B("Taunt")} taunt · {B("Photo")} photo · {B("Hands")} hands down/up · {B("Shoulder")} swap shoulder · {B("Pause")} pause";
            action.text = actions.ActionWord;
            if (Time.time > subtitleUntil) subtitle.text = "";
            prompt.text = WeaponPickup.Nearby != null ? WeaponPickup.Nearby.Label : FoodPickup.Nearby != null ? FoodPickup.Nearby.Label : "";
            if (actions.weapons != null) scope.SetActive(actions.weapons.Scoped);
            if (rexHealth != null)
            {
                int n = Mathf.CeilToInt(rexHealth.Fraction * 20);
                hp.text = "HEALTH " + new string('|', n) + new string('.', 20 - n);
                hp.color = Color.Lerp(new Color(1f, 0.3f, 0.2f), new Color(0.6f, 1f, 0.5f), rexHealth.Fraction);
            }
            action.color = actions.ActionWord == "Talk" ? new Color(0.5f, 0.9f, 1f) : new Color(1f, 0.6f, 0.2f);
        }
    }
}
