namespace RexMagnum
{
    /// <summary>
    /// Controller button names as coloured glyph-style tokens for hints, HELP and prompts ("[A]" green, "[B]" red,
    /// "[X]" blue, "[Y]" yellow, the rest light grey) when the controller was used last. Rich text colour tags:
    /// uGUI Text and the MenuSkin IMGUI styles (richText on) show them; no font asset needed.
    /// Keyboard and touch: the plain name from RMInput.Btn.
    /// </summary>
    public static class Glyph
    {
        public static bool Pad => RMInput.LastDevice == RMInput.DeviceKind.Gamepad;

        /// <summary>RMInput.Btn as a coloured token on the controller ("hold [Y]").</summary>
        public static string B(string action)
        {
            string n = RMInput.Btn(action);
            if (!Pad) return n;
            if (n.StartsWith("hold ")) return "hold " + Token(n.Substring(5));
            return Token(n);
        }

        /// <summary>One button name as a coloured "[name]" token.</summary>
        public static string Token(string name) => $"<color={ColorOf(name)}><b>[{name}]</b></color>";

        static string ColorOf(string n)
        {
            switch (n)
            {
                case "A": return "#5CDB5C";
                case "B": return "#FF5B4A";
                case "X": return "#55A8FF";
                case "Y": return "#FFD83A";
                case "LB": case "RB": case "LT": case "RT": return "#F2F2F2";
                default: return "#C9D2DE";      // D-pad, stick clicks, Menu, View
            }
        }

        /// <summary>A prompt from another script that starts with the button name ("X: pick up ..."):
        /// the name becomes a token on the controller.</summary>
        public static string Swap(string text, string action)
        {
            if (!Pad || string.IsNullOrEmpty(text)) return text;
            string n = RMInput.Btn(action);
            return text.StartsWith(n) ? B(action) + text.Substring(n.Length) : text;
        }
    }
}
