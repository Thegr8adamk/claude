using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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
        public static bool WheelOpen;              // set by WeaponWheel
        public static Vector2 WheelDelta;          // pixels moved this frame by the finger that holds Weapon
        static readonly System.Collections.Generic.Dictionary<string, (bool held, int down, int up, float t)> b =
            new System.Collections.Generic.Dictionary<string, (bool, int, int, float)>();

        public static void Set(string name, bool held)
        {
            b.TryGetValue(name, out var s);
            if (held && !s.held) { s.down = Time.frameCount; s.t = Time.unscaledTime; }
            if (!held && s.held) s.up = Time.frameCount;
            s.held = held;
            b[name] = s;
        }
        public static bool Held(string n) => Active && b.TryGetValue(n, out var s) && s.held;
        public static bool Down(string n) => Active && b.TryGetValue(n, out var s) && s.down == Time.frameCount;
        public static bool Up(string n) => Active && b.TryGetValue(n, out var s) && s.up == Time.frameCount;
        /// <summary>Seconds since the button went down (held, or released this frame).</summary>
        public static float HeldFor(string n) => b.TryGetValue(n, out var s) ? Time.unscaledTime - s.t : 0f;
    }

    public static class RMInput
    {
        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;
        static Gamepad G => Gamepad.current;

        // ---- keyboard remap (brief section 7): action -> key, saved in PlayerPrefs "RexMagnum_Key_<action>"
        public static readonly string[] Actions = { "Forward", "Back", "Left", "Right", "Run", "Jump", "Taunt", "Photo",
                                                    "Hands", "Use", "Camera", "Shoulder", "NextWeapon", "PrevWeapon", "Pause", "Wheel" };
        static readonly Key[] Defaults = { Key.W, Key.S, Key.A, Key.D, Key.LeftShift, Key.Space, Key.T, Key.P,
                                           Key.H, Key.F, Key.V, Key.C, Key.E, Key.Q, Key.Escape, Key.Tab };
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

        // ---- controller + general options (brief section 7), loaded from PlayerPrefs by LoadOptions()
        public static bool SwapSticks, SwapTriggers, AimAssist = true, RunToggle, AimToggle, HandsHold;
        public static void LoadOptions()
        {
            SwapSticks = PlayerPrefs.GetInt("RexMagnum_SwapSticks", 0) == 1;
            SwapTriggers = PlayerPrefs.GetInt("RexMagnum_SwapTriggers", 0) == 1;
            AimAssist = PlayerPrefs.GetInt("RexMagnum_AimAssist", 1) == 1;
            RunToggle = PlayerPrefs.GetInt("RexMagnum_RunToggle", 0) == 1;
            AimToggle = PlayerPrefs.GetInt("RexMagnum_AimToggle", 0) == 1;
            HandsHold = PlayerPrefs.GetInt("RexMagnum_HandsHold", 0) == 1;
        }
        static Vector2 MoveStick => G == null ? Vector2.zero : (SwapSticks ? G.rightStick.ReadValue() : G.leftStick.ReadValue());
        static Vector2 LookStick => G == null ? Vector2.zero : (SwapSticks ? G.leftStick.ReadValue() : G.rightStick.ReadValue());
        static UnityEngine.InputSystem.Controls.ButtonControl ActTrig => SwapTriggers ? G.rightShoulder : G.rightTrigger;
        static UnityEngine.InputSystem.Controls.ButtonControl AimTrig => SwapTriggers ? G.leftShoulder : G.leftTrigger;
        static UnityEngine.InputSystem.Controls.ButtonControl NextBtn => SwapTriggers ? G.rightTrigger : G.rightShoulder;
        static UnityEngine.InputSystem.Controls.ButtonControl PrevBtn => SwapTriggers ? G.leftTrigger : G.leftShoulder;
        // toggle states (tap once = on, tap again = off)
        static bool runOn, aimOn;
        static int runFrame = -1, aimFrame = -1;
        static bool RunRaw => KHeld("Run") || (TouchState.Active && TouchState.Move.magnitude > 0.95f) ||
                              (G != null && (PB("Run").isPressed || MoveStick.magnitude > 0.95f));
        static bool RunPressed => KDown("Run") || (G != null && PB("Run").wasPressedThisFrame);
        static bool AimRaw => TouchState.Held("Aim") || (M != null && M.rightButton.isPressed) || (G != null && AimTrig.isPressed);
        static bool AimPressed => (M != null && M.rightButton.wasPressedThisFrame) || (G != null && AimTrig.wasPressedThisFrame);

        public static float MouseSensitivity = 0.08f;   // degrees per pixel
        public static float StickSensitivity = 180f;    // degrees per second
        // ---- on-screen button names follow the device used last (brief section 7: icons change per device)
        public enum DeviceKind { Keyboard, Gamepad, Touch }
        public static DeviceKind LastDevice
        {
            get
            {
                if (TouchState.Active) return DeviceKind.Touch;
                double k = System.Math.Max(K != null ? K.lastUpdateTime : 0, M != null ? M.lastUpdateTime : 0);
                return G != null && G.lastUpdateTime > k ? DeviceKind.Gamepad : DeviceKind.Keyboard;
            }
        }

        static readonly System.Collections.Generic.Dictionary<string, string> PadNames = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Use", "X" }, { "Jump", "A" }, { "Taunt", "D-pad up" }, { "Photo", "D-pad right" }, { "Hands", "D-pad down" },
            { "Camera", "R-stick click" }, { "Shoulder", "D-pad left" }, { "Pause", "Menu" }, { "Wheel", "hold Y" },
            { "Run", "L-stick click" }, { "Action", "RT" }, { "Aim", "LT" }, { "NextWeapon", "RB" }, { "PrevWeapon", "LB" },
        };

        /// <summary>The name to show for an action's button on the device used last ("F", "X", "Use").</summary>
        public static string Btn(string action)
        {
            switch (LastDevice)
            {
                case DeviceKind.Gamepad:
                    if (System.Array.IndexOf(PadActions, action) >= 0) return (action == "Wheel" ? "hold " : "") + PadName(PadFor(action));
                    return PadNames.TryGetValue(action, out var p) ? p : action;
                case DeviceKind.Touch: return action == "Wheel" ? "hold Weapon" : action == "NextWeapon" ? "Weapon" : action;
                default:
                    if (action == "Action") return "Left click";
                    if (action == "Aim") return "Right click";
                    var key = KeyFor(action);
                    string n = key.ToString();
                    return n.StartsWith("Left") || n.StartsWith("Right") ? n.Substring(n.StartsWith("Left") ? 4 : 5) : n;
            }
        }

        // ---- controller remap (brief section 7: button lists per device, swap on conflict)
        public static readonly string[] PadActions = { "Jump", "Use", "Wheel", "Taunt", "Photo", "Hands", "Shoulder", "Camera", "Run", "Pause" };
        static readonly GamepadButton[] PadDefaults = { GamepadButton.South, GamepadButton.West, GamepadButton.North,
            GamepadButton.DpadUp, GamepadButton.DpadRight, GamepadButton.DpadDown, GamepadButton.DpadLeft,
            GamepadButton.RightStick, GamepadButton.LeftStick, GamepadButton.Start };
        /// <summary>Buttons that can be given to an action (sticks and triggers have their own swap options).</summary>
        public static readonly GamepadButton[] PadChoices = { GamepadButton.South, GamepadButton.East, GamepadButton.West,
            GamepadButton.North, GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight,
            GamepadButton.LeftStick, GamepadButton.RightStick, GamepadButton.Start, GamepadButton.Select };
        static GamepadButton[] pad;
        public static GamepadButton PadFor(string action)
        {
            if (pad == null) LoadPad();
            int i = System.Array.IndexOf(PadActions, action);
            return i < 0 ? GamepadButton.Select : pad[i];
        }
        static void LoadPad()
        {
            pad = new GamepadButton[PadActions.Length];
            for (int i = 0; i < PadActions.Length; i++)
                pad[i] = (GamepadButton)PlayerPrefs.GetInt("RexMagnum_Pad_" + PadActions[i], (int)PadDefaults[i]);
        }
        /// <summary>Give an action a controller button; if another action had it, the two swap.</summary>
        public static void RebindPad(string action, GamepadButton b)
        {
            if (pad == null) LoadPad();
            int i = System.Array.IndexOf(PadActions, action);
            if (i < 0) return;
            int other = System.Array.IndexOf(pad, b);
            if (other >= 0 && other != i) { pad[other] = pad[i]; PlayerPrefs.SetInt("RexMagnum_Pad_" + PadActions[other], (int)pad[other]); }
            pad[i] = b;
            PlayerPrefs.SetInt("RexMagnum_Pad_" + action, (int)b);
        }
        public static void ResetPad()
        {
            foreach (var a in PadActions) PlayerPrefs.DeleteKey("RexMagnum_Pad_" + a);
            LoadPad();
        }
        static UnityEngine.InputSystem.Controls.ButtonControl PB(string action) => G[PadFor(action)];
        public static string PadName(GamepadButton b)
        {
            switch (b)
            {
                case GamepadButton.South: return "A";
                case GamepadButton.East: return "B";
                case GamepadButton.West: return "X";
                case GamepadButton.North: return "Y";
                case GamepadButton.DpadUp: return "D-pad up";
                case GamepadButton.DpadDown: return "D-pad down";
                case GamepadButton.DpadLeft: return "D-pad left";
                case GamepadButton.DpadRight: return "D-pad right";
                case GamepadButton.LeftStick: return "L-stick click";
                case GamepadButton.RightStick: return "R-stick click";
                case GamepadButton.Start: return "Menu";
                case GamepadButton.Select: return "View";
                default: return b.ToString();
            }
        }

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
                if (G != null) v += MoveStick;
                if (TouchState.Active) v += TouchState.Move;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>Look change in degrees for this frame (x = yaw, y = pitch).</summary>
        /// <summary>VR snap turn: the look stick's left/right value (-1..1).</summary>
        public static float LookStickX => LookBlocked || G == null ? 0f : LookStick.x;
        public static bool LookIsStick => LastDevice == DeviceKind.Gamepad;

        public static Vector2 LookDelta
        {
            get
            {
                Vector2 v = Vector2.zero;
                if (LookBlocked) return v;
                if (M != null) v += M.delta.ReadValue() * MouseSensitivity;
                if (G != null) v += LookStick * StickSensitivity * Time.unscaledDeltaTime;
                if (TouchState.Active) v += TouchState.LookDelta;
                v.x *= LookScaleX; v.y *= LookScaleY;
                if (InvertY) v.y = -v.y;
                return v;
            }
        }

        public static bool Run
        {
            get
            {
                if (!RunToggle) return RunRaw;
                if (runFrame != Time.frameCount) { runFrame = Time.frameCount; if (RunPressed) runOn = !runOn; if (Move.sqrMagnitude < 0.01f) runOn = false; }
                return runOn || (TouchState.Active && TouchState.Move.magnitude > 0.95f);
            }
        }
        public static bool Jump => TouchState.Down("Jump") || (KDown("Jump")) || (G != null && PB("Jump").wasPressedThisFrame);
        public static bool ActionDown => TouchState.Down("Action") || (M != null && M.leftButton.wasPressedThisFrame) ||
                                         (G != null && ActTrig.wasPressedThisFrame);
        public static bool ActionHeld => TouchState.Held("Action") || (M != null && M.leftButton.isPressed) || (G != null && ActTrig.isPressed);
        public static bool ActionUp => TouchState.Up("Action") || (M != null && M.leftButton.wasReleasedThisFrame) ||
                                       (G != null && ActTrig.wasReleasedThisFrame);
        public static bool Aim
        {
            get
            {
                if (!AimToggle) return AimRaw;
                if (aimFrame != Time.frameCount) { aimFrame = Time.frameCount; if (AimPressed) aimOn = !aimOn; }
                return aimOn || TouchState.Held("Aim");
            }
        }
        public static bool CameraToggle => TouchState.Down("Camera") || (KDown("Camera")) ||
                                           (G != null && PB("Camera").wasPressedThisFrame);
        public static bool SwapShoulder => (KDown("Shoulder")) ||
                                           (G != null && PB("Shoulder").wasPressedThisFrame);
        public static bool Taunt => TouchState.Down("Taunt") || (KDown("Taunt")) || (G != null && PB("Taunt").wasPressedThisFrame);
        public static bool Photo => TouchState.Down("Photo") || (KDown("Photo")) || (G != null && PB("Photo").wasPressedThisFrame);
        /// <summary>Hands down/up: tap toggles (default); with "hold" the hands stay down only while the key is held
        /// (the controller reads it as press and release events).</summary>
        public static bool Hands => HandsHold
            ? (KDown("Hands") || (K != null && KeyFor("Hands") != Key.None && K[KeyFor("Hands")].wasReleasedThisFrame) ||
               (G != null && (PB("Hands").wasPressedThisFrame || PB("Hands").wasReleasedThisFrame)) || TouchState.Down("Hands") || TouchState.Up("Hands"))
            : (TouchState.Down("Hands") || KDown("Hands") || (G != null && PB("Hands").wasPressedThisFrame));
        public static bool Use => usedFrame != Time.frameCount &&
                                  (TouchState.Down("Use") || (KDown("Use")) || (G != null && PB("Use").wasPressedThisFrame));
        static int usedFrame = -1;
        /// <summary>The Use press did something (pick up, eat, rescue...): nothing else reacts to it this frame.</summary>
        public static void ConsumeUse() => usedFrame = Time.frameCount;
        public static bool Pause => TouchState.Down("Pause") || (KDown("Pause")) || (G != null && PB("Pause").wasPressedThisFrame);
        /// <summary>Touch Weapon button: a short tap = next weapon, a hold opens the weapon wheel.</summary>
        public const float WheelHoldTime = 0.35f;
        public static bool WheelHeld => KHeld("Wheel") || (G != null && PB("Wheel").isPressed) ||
                                        (TouchState.Held("Weapon") && TouchState.HeldFor("Weapon") >= WheelHoldTime);
        /// <summary>Wheel pointer: mouse / right stick / touch drag (screen-space direction).</summary>
        public static Vector2 WheelPointer
        {
            get
            {
                Vector2 v = Vector2.zero;
                if (M != null) v += M.delta.ReadValue() * 0.02f;
                if (TouchState.Active) v += (TouchState.LookDelta * 0.05f) + TouchState.WheelDelta * 0.006f;
                return v;
            }
        }
        public static Vector2 WheelStick => G == null ? Vector2.zero : LookStick;
        /// <summary>The weapon wheel is open: the camera must not turn.</summary>
        public static bool LookBlocked;
        public static bool NextWeapon => (TouchState.Up("Weapon") && TouchState.HeldFor("Weapon") < WheelHoldTime) || (KDown("NextWeapon")) || (M != null && M.scroll.ReadValue().y < -0.1f) ||
                                         (G != null && NextBtn.wasPressedThisFrame);
        public static bool PrevWeapon => (KDown("PrevWeapon")) || (M != null && M.scroll.ReadValue().y > 0.1f) ||
                                         (G != null && PrevBtn.wasPressedThisFrame);
        public static bool AnyKey => TouchState.AnyTap || (K != null && K.anyKey.wasPressedThisFrame) || AnyClick ||
                                     (G != null && G.buttonSouth.wasPressedThisFrame);
        public static bool AnyClick => M != null && M.leftButton.wasPressedThisFrame;
    }
}
