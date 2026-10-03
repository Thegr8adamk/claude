using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// One look for every IMGUI menu (title, pause, settings, tutorial hints, toasts): dark translucent panels,
    /// gold for the selection, big readable buttons. Built once per screen height (call Use(s) at the top of OnGUI)
    /// and shared, so no GUIStyle is made per OnGUI event.
    /// </summary>
    public static class MenuSkin
    {
        public static readonly Color Gold = new Color(1f, 0.8f, 0.25f);
        public static readonly Color Pale = new Color(1f, 0.92f, 0.8f);

        public static GUIStyle Title, Header, Label, LabelRow, Value, Small, Button, Option, Tab, Panel, RowSel,
                               Logo, LogoShadow, LogoEdge, LogoShine, Subtitle, MenuItem, MenuItemOn, Hint, Notes, Question, Toast;
        static float scale = -1f;
        static Texture2D panelTex, btnTex, btnHoverTex, btnOnTex, btnOnHoverTex, rowSelTex;

        /// <summary>Builds the styles for this scale (screen height / 1080) if needed. Only from OnGUI (needs GUI.skin).</summary>
        public static void Use(float s)
        {
            if (Mathf.Approximately(s, scale) && Title != null && panelTex != null) return;
            scale = s;
            if (panelTex == null)
            {
                panelTex = Tex(new Color(0.03f, 0.04f, 0.06f, 0.78f), new Color(1f, 0.8f, 0.25f, 0.35f));
                btnTex = Tex(new Color(0.08f, 0.09f, 0.12f, 0.88f), new Color(1f, 1f, 1f, 0.18f));
                btnHoverTex = Tex(new Color(0.2f, 0.17f, 0.09f, 0.94f), new Color(1f, 0.8f, 0.25f, 0.8f));
                btnOnTex = Tex(new Color(0.95f, 0.72f, 0.18f, 0.95f), new Color(1f, 0.92f, 0.6f, 1f));
                btnOnHoverTex = Tex(new Color(1f, 0.8f, 0.3f, 1f), Color.white);
                rowSelTex = Tex(new Color(1f, 0.75f, 0.2f, 0.24f), new Color(1f, 0.8f, 0.25f, 0.9f));
            }
            int F(float px) => Mathf.Max(8, (int)(px * s));
            var dark = new Color(0.08f, 0.06f, 0.02f);

            Title = Text(F(34), FontStyle.Bold, TextAnchor.MiddleCenter, Gold, false);
            Header = Text(F(28), FontStyle.Bold, TextAnchor.MiddleLeft, Gold, false);
            Label = Text(F(22), FontStyle.Normal, TextAnchor.UpperLeft, Color.white, true);
            LabelRow = Text(F(22), FontStyle.Normal, TextAnchor.MiddleLeft, Color.white, false);
            Value = Text(F(22), FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.6f), false);
            Small = Text(F(17), FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.82f, 0.86f, 0.9f), true);
            Logo = Text(F(96), FontStyle.Bold, TextAnchor.MiddleLeft, Gold, false);
            LogoShadow = new GUIStyle(Logo);
            LogoShadow.normal.textColor = new Color(0f, 0f, 0f, 0.55f);
            LogoEdge = new GUIStyle(Logo);                                     // dark brown outline around the letters
            LogoEdge.normal.textColor = new Color(0.16f, 0.07f, 0.01f, 1f);
            LogoShine = new GUIStyle(Logo);                                    // pale top half: a chrome / 80s poster look
            LogoShine.normal.textColor = new Color(1f, 0.97f, 0.78f, 1f);
            Subtitle = Text(F(40), FontStyle.Bold, TextAnchor.MiddleLeft, Pale, false);
            MenuItem = Text(F(40), FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.85f), false);
            MenuItemOn = new GUIStyle(MenuItem);
            MenuItemOn.normal.textColor = Gold;
            Hint = Text(F(30), FontStyle.Normal, TextAnchor.MiddleCenter, Color.white, true);
            Notes = Text(F(19), FontStyle.Normal, TextAnchor.LowerLeft, Color.white, true);
            Question = Text(F(30), FontStyle.Bold, TextAnchor.UpperLeft, Pale, true);
            Toast = Text(F(20), FontStyle.Bold, TextAnchor.MiddleCenter, Gold, true);

            Button = new GUIStyle(GUI.skin.button) { fontSize = F(22), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                                                     border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(10, 10, 6, 6), richText = true };
            Skin(Button, Color.white, dark);
            if (UiFont != null) Button.font = UiFont;
            Tab = new GUIStyle(Button) { fontSize = F(20) };
            Option = new GUIStyle(Button) { fontSize = F(19), fixedHeight = 40 * s, margin = new RectOffset(3, 3, 3, 3) };
            Panel = new GUIStyle(GUI.skin.box) { border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(12, 12, 10, 10) };
            Panel.normal.background = panelTex;
            RowSel = new GUIStyle(GUI.skin.box) { border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(6, 6, 2, 2) };
            RowSel.normal.background = rowSelTex;
        }

        static Font uiFont;
        static bool fontTried;
        /// <summary>A clean system sans-serif (installed on the player's machine; nothing is downloaded), or null for
        /// Unity's built-in font. The HUD's uGUI text uses it too (one font for menus and HUD).</summary>
        public static Font UiFont
        {
            get
            {
                if (!fontTried)
                {
                    fontTried = true;
                    // the web player has no OS fonts: an OS font there drew no text (title menu empty) and logged
                    // "Unable to load font face" for every label every frame (12,900 warnings, part of the 1 fps)
                    if (Application.platform == RuntimePlatform.WebGLPlayer) uiFont = BuiltinFont();
                    else
                    {
                        try { uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Helvetica Neue", "Roboto", "Noto Sans", "Arial" }, 32); }
                        catch (System.Exception) { uiFont = null; }
                    }
                }
                return uiFont;
            }
        }

        /// <summary>Unity's built-in font (its glyph data ships in the build): "LegacyRuntime.ttf" since Unity 2022.2,
        /// "Arial.ttf" before. Null when neither exists (IMGUI then uses the skin's own font).</summary>
        public static Font BuiltinFont()
        {
            foreach (var n in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try { var f = Resources.GetBuiltinResource<Font>(n); if (f != null) return f; }
                catch (System.Exception) { }
            }
            return null;
        }

        static GUIStyle Text(int size, FontStyle fs, TextAnchor a, Color c, bool wrap)
        {
            // rich text: controller buttons show as coloured tokens (Glyph)
            var st = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = a, wordWrap = wrap, richText = true };
            if (UiFont != null) st.font = UiFont;
            st.normal.textColor = c;
            return st;
        }

        /// <summary>Dark button, gold edge on hover, gold fill when on (toolbar / tab / toggle) or pressed.</summary>
        static void Skin(GUIStyle b, Color text, Color onText)
        {
            b.normal.background = btnTex; b.normal.textColor = text;
            b.hover.background = btnHoverTex; b.hover.textColor = Gold;
            b.focused.background = btnHoverTex; b.focused.textColor = Gold;
            b.active.background = btnOnTex; b.active.textColor = onText;
            b.onNormal.background = btnOnTex; b.onNormal.textColor = onText;
            b.onHover.background = btnOnHoverTex; b.onHover.textColor = onText;
            b.onFocused.background = btnOnTex; b.onFocused.textColor = onText;
            b.onActive.background = btnOnHoverTex; b.onActive.textColor = onText;
        }

        /// <summary>8x8 fill with a 1 px edge (the 2 px style border keeps the edge sharp when stretched).</summary>
        static Texture2D Tex(Color fill, Color edge)
        {
            const int n = 8;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                t.SetPixel(x, y, x == 0 || y == 0 || x == n - 1 || y == n - 1 ? edge : fill);
            t.Apply();
            return t;
        }

        /// <summary>
        /// The title logo: soft drop shadow, a dark outline (8 offsets), gold letters with a pale upper half (clipped
        /// copy) and a thin gold rule under it. a = alpha (menu transitions).
        /// </summary>
        public static void DrawLogo(Rect r, string text, float s, float a = 1f)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            // shadow: two offsets, a soft edge without a blur shader
            GUI.color = new Color(1f, 1f, 1f, a * 0.6f);
            GUI.Label(new Rect(r.x + 7 * s, r.y + 8 * s, r.width, r.height), text, LogoShadow);
            GUI.Label(new Rect(r.x + 4 * s, r.y + 5 * s, r.width, r.height), text, LogoShadow);
            GUI.color = new Color(1f, 1f, 1f, a);
            float o = Mathf.Max(1f, 3f * s);
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI / 4f;
                GUI.Label(new Rect(r.x + Mathf.Cos(ang) * o, r.y + Mathf.Sin(ang) * o, r.width, r.height), text, LogoEdge);
            }
            GUI.Label(r, text, Logo);
            // the upper ~45% of the letters in pale gold (a group clips the copy)
            GUI.BeginGroup(new Rect(r.x, r.y, r.width, r.height * 0.47f));
            GUI.Label(new Rect(0f, 0f, r.width, r.height), text, LogoShine);
            GUI.EndGroup();
            // gold rule under the logo, as wide as the text
            float tw = Logo.CalcSize(new GUIContent(text)).x;
            GUI.color = new Color(Gold.r, Gold.g, Gold.b, a * 0.9f);
            GUI.DrawTexture(new Rect(r.x + 4 * s, r.y + r.height * 0.88f, tw * Mathf.Clamp01(a * 1.2f), 4 * s), Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>0 -> 1 over `seconds` after `since` (unscaled time: works in the pause menu), ease-out. For short
        /// fade + slide transitions when a menu or a tab opens.</summary>
        public static float Appear(float since, float seconds = 0.22f)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - since) / seconds);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        /// <summary>A dark translucent panel with a thin gold edge.</summary>
        public static void DrawPanel(Rect r)
        {
            if (Event.current.type == EventType.Repaint) Panel.Draw(r, false, false, false, false);
        }
    }
}
