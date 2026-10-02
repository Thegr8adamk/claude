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
                               Logo, LogoShadow, Subtitle, MenuItem, MenuItemOn, Hint, Notes, Question, Toast;
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
            LogoShadow.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
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
            Tab = new GUIStyle(Button) { fontSize = F(20) };
            Option = new GUIStyle(Button) { fontSize = F(19), fixedHeight = 40 * s, margin = new RectOffset(3, 3, 3, 3) };
            Panel = new GUIStyle(GUI.skin.box) { border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(12, 12, 10, 10) };
            Panel.normal.background = panelTex;
            RowSel = new GUIStyle(GUI.skin.box) { border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(6, 6, 2, 2) };
            RowSel.normal.background = rowSelTex;
        }

        static GUIStyle Text(int size, FontStyle fs, TextAnchor a, Color c, bool wrap)
        {
            // rich text: controller buttons show as coloured tokens (Glyph)
            var st = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = a, wordWrap = wrap, richText = true };
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

        /// <summary>A dark translucent panel with a thin gold edge.</summary>
        public static void DrawPanel(Rect r)
        {
            if (Event.current.type == EventType.Repaint) Panel.Draw(r, false, false, false, false);
        }
    }
}
