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
        Text info, action, subtitle, hp;
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
            RexVoice.Subtitle = line => { subtitle.text = "REX: " + line; subtitleUntil = Time.time + 3f; };
            cross = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            cross.transform.SetParent(go.transform, false);
            cross.GetComponent<RectTransform>().sizeDelta = new Vector2(6, 6);
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

        void Update()
        {
            if (RMInput.Pause)
            {
                paused = !paused;
                Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = paused;
                rig.InputEnabled = !paused;
                actions.controller.InputEnabled = !paused;
            }
            info.text = $"REX MAGNUM — test scene\nCamera: {(rig.Mode == RexCameraRig.CameraMode.FirstPerson ? "FIRST person" : "THIRD person")}  (V / R-stick)\n" +
                        "WASD move · Shift run · Space jump · Mouse look\nLeft click: tap = jab, hold = heavy punch\n" +
                        "T taunt · P photo · H hands down/up · C swap shoulder · Esc pause";
            action.text = actions.ActionWord;
            if (Time.time > subtitleUntil) subtitle.text = "";
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
