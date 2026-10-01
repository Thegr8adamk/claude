using UnityEngine;
using UnityEngine.InputSystem;

namespace RexMagnum
{
    /// <summary>
    /// All player input in one place (Unity Input System package).
    /// Keyboard + mouse and controller work at the same time. Buttons follow the
    /// project brief (section 7). Touch: TouchControls fills TouchState. Keyboard keys can be remapped (PauseMenu > Controls).
    /// </summary>
    /// <summary>On-screen touch input (filled by TouchControls each frame).</summary>
    public static class TouchState
    {
        public static bool Active;
        public static bool AnyTap;
        public static Vector2 Move;
        public static Vector2 LookDelta;           // degrees this frame
        static readonly System.Collections.Generic.Dictionary<string, (bool held, int down, int up)> b =
            new System.Collections.Generic.Dictionary<string, (bool, int, int)>();

        public static void Set(string name, bool held)
        {
            b.TryGetValue(name, out var s);
            if (held && !s.held) s.down = Time.frameCount;
            if (!held && s.held) s.up = Time.frameCount;
            s.held = held;
            b[name] = s;
        }
        public static bool Held(string n) => Active && b.TryGetValue(n, out var s) && s.held;
        public static bool Down(string n) => Active && b.TryGetValue(n, out var s) && s.down == Time.frameCount;
        public static bool Up(string n) => Active && b.TryGetValue(n, out var s) && s.up == Time.frameCount;
    }

    public static class RMInput
    {
        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;
        static Gamepad G => Gamepad.current;

        // ---- keyboard remap (brief section 7): action -> key, saved in PlayerPrefs "RexMagnum_Key_<action>"
        public static readonly string[] Actions = { "Forward", "Back", "Left", "Right", "Run", "Jump", "Taunt", "Photo",
                                                    "Hands", "Use", "Camera", "Shoulder", "NextWeapon", "PrevWeapon", "Pause" };
        static readonly Key[] Defaults = { Key.W, Key.S, Key.A, Key.D, Key.LeftShift, Key.Space, Key.T, Key.P,
                                           Key.H, Key.F, Key.V, Key.C, Key.E, Key.Q, Key.Escape };
        static Key[] keys;
        public static Key KeyFor(string action)
        {
            if (keys == null) LoadKeys();
            int i = System.Array.IndexOf(Actions, action);
            return i < 0 ? Key.None : keys[i];
        }
        public static void LoadKeys()
        {
            keys = new Key[Actions.Length];
            for (int i = 0; i < Actions.Length; i++)
                keys[i] = (Key)PlayerPrefs.GetInt("RexMagnum_Key_" + Actions[i], (int)Defaults[i]);
        }
        /// <summary>Assign a key; if another action had it, the two swap (no conflicts).</summary>
        public static void Rebind(string action, Key k)
        {
            if (keys == null) LoadKeys();
            int i = System.Array.IndexOf(Actions, action);
            int other = System.Array.IndexOf(keys, k);
            if (other >= 0 && other != i) { keys[other] = keys[i]; PlayerPrefs.SetInt("RexMagnum_Key_" + Actions[other], (int)keys[other]); }
            keys[i] = k;
            PlayerPrefs.SetInt("RexMagnum_Key_" + action, (int)k);
        }
        public static void ResetKeys()
        {
            for (int i = 0; i < Actions.Length; i++) PlayerPrefs.DeleteKey("RexMagnum_Key_" + Actions[i]);
            LoadKeys();
        }
        static bool KHeld(string a) { var k = KeyFor(a); return K != null && k != Key.None && K[k].isPressed; }
        static bool KDown(string a) { var k = KeyFor(a); return K != null && k != Key.None && K[k].wasPressedThisFrame; }

        public static float MouseSensitivity = 0.08f;   // degrees per pixel
        public static float StickSensitivity = 180f;    // degrees per second
        public static bool InvertY;
        public static float LookScaleX = 1f, LookScaleY = 1f;     // separate X / Y look speed (settings)

        public static Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;
                if (K != null)
                {
                    v.x = (KHeld("Right") ? 1 : 0) - (KHeld("Left") ? 1 : 0);
                    v.y = (KHeld("Forward") ? 1 : 0) - (KHeld("Back") ? 1 : 0);
                }
                if (G != null) v += G.leftStick.ReadValue();
                if (TouchState.Active) v += TouchState.Move;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>Look change in degrees for this frame (x = yaw, y = pitch).</summary>
        public static Vector2 LookDelta
        {
            get
            {
                Vector2 v = Vector2.zero;
                if (M != null) v += M.delta.ReadValue() * MouseSensitivity;
                if (G != null) v += G.rightStick.ReadValue() * StickSensitivity * Time.unscaledDeltaTime;
                if (TouchState.Active) v += TouchState.LookDelta;
                v.x *= LookScaleX; v.y *= LookScaleY;
                if (InvertY) v.y = -v.y;
                return v;
            }
        }

        public static bool Run => (KHeld("Run")) || (TouchState.Active && TouchState.Move.magnitude > 0.95f) ||
                                  (G != null && (G.leftStickButton.isPressed || G.leftStick.ReadValue().magnitude > 0.95f));
        public static bool Jump => TouchState.Down("Jump") || (KDown("Jump")) || (G != null && G.buttonSouth.wasPressedThisFrame);
        public static bool ActionDown => TouchState.Down("Action") || (M != null && M.leftButton.wasPressedThisFrame) ||
                                         (G != null && G.rightTrigger.wasPressedThisFrame);
        public static bool ActionHeld => TouchState.Held("Action") || (M != null && M.leftButton.isPressed) || (G != null && G.rightTrigger.isPressed);
        public static bool ActionUp => TouchState.Up("Action") || (M != null && M.leftButton.wasReleasedThisFrame) ||
                                       (G != null && G.rightTrigger.wasReleasedThisFrame);
        public static bool Aim => TouchState.Held("Aim") || (M != null && M.rightButton.isPressed) || (G != null && G.leftTrigger.isPressed);
        public static bool CameraToggle => TouchState.Down("Camera") || (KDown("Camera")) ||
                                           (G != null && G.rightStickButton.wasPressedThisFrame);
        public static bool SwapShoulder => (KDown("Shoulder")) ||
                                           (G != null && G.dpad.left.wasPressedThisFrame);
        public static bool Taunt => TouchState.Down("Taunt") || (KDown("Taunt")) || (G != null && G.dpad.up.wasPressedThisFrame);
        public static bool Photo => TouchState.Down("Photo") || (KDown("Photo")) || (G != null && G.dpad.right.wasPressedThisFrame);
        public static bool Hands => TouchState.Down("Hands") || (KDown("Hands")) || (G != null && G.dpad.down.wasPressedThisFrame);
        public static bool Use => TouchState.Down("Use") || (KDown("Use")) || (G != null && G.buttonWest.wasPressedThisFrame);
        public static bool Pause => TouchState.Down("Pause") || (KDown("Pause")) || (G != null && G.startButton.wasPressedThisFrame);
        public static bool NextWeapon => TouchState.Down("Weapon") || (KDown("NextWeapon")) || (M != null && M.scroll.ReadValue().y < -0.1f) ||
                                         (G != null && G.rightShoulder.wasPressedThisFrame);
        public static bool PrevWeapon => (KDown("PrevWeapon")) || (M != null && M.scroll.ReadValue().y > 0.1f) ||
                                         (G != null && G.leftShoulder.wasPressedThisFrame);
        public static bool AnyKey => TouchState.AnyTap || (K != null && K.anyKey.wasPressedThisFrame) || AnyClick ||
                                     (G != null && G.buttonSouth.wasPressedThisFrame);
        public static bool AnyClick => M != null && M.leftButton.wasPressedThisFrame;
    }
}
