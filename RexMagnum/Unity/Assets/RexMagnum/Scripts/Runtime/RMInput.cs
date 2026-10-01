using UnityEngine;
using UnityEngine.InputSystem;

namespace RexMagnum
{
    /// <summary>
    /// All player input in one place (Unity Input System package).
    /// Keyboard + mouse and controller work at the same time. Buttons follow the
    /// project brief (section 7). Remapping and touch come in a later step.
    /// </summary>
    public static class RMInput
    {
        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;
        static Gamepad G => Gamepad.current;

        public static float MouseSensitivity = 0.08f;   // degrees per pixel
        public static float StickSensitivity = 180f;    // degrees per second
        public static bool InvertY;

        public static Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;
                if (K != null)
                {
                    v.x = (K.dKey.isPressed ? 1 : 0) - (K.aKey.isPressed ? 1 : 0);
                    v.y = (K.wKey.isPressed ? 1 : 0) - (K.sKey.isPressed ? 1 : 0);
                }
                if (G != null) v += G.leftStick.ReadValue();
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
                if (InvertY) v.y = -v.y;
                return v;
            }
        }

        public static bool Run => (K != null && K.leftShiftKey.isPressed) ||
                                  (G != null && (G.leftStickButton.isPressed || G.leftStick.ReadValue().magnitude > 0.95f));
        public static bool Jump => (K != null && K.spaceKey.wasPressedThisFrame) || (G != null && G.buttonSouth.wasPressedThisFrame);
        public static bool ActionDown => (M != null && M.leftButton.wasPressedThisFrame) ||
                                         (G != null && G.rightTrigger.wasPressedThisFrame);
        public static bool ActionHeld => (M != null && M.leftButton.isPressed) || (G != null && G.rightTrigger.isPressed);
        public static bool ActionUp => (M != null && M.leftButton.wasReleasedThisFrame) ||
                                       (G != null && G.rightTrigger.wasReleasedThisFrame);
        public static bool Aim => (M != null && M.rightButton.isPressed) || (G != null && G.leftTrigger.isPressed);
        public static bool CameraToggle => (K != null && K.vKey.wasPressedThisFrame) ||
                                           (G != null && G.rightStickButton.wasPressedThisFrame);
        public static bool SwapShoulder => (K != null && K.cKey.wasPressedThisFrame) ||
                                           (G != null && G.dpad.left.wasPressedThisFrame);
        public static bool Taunt => (K != null && K.tKey.wasPressedThisFrame) || (G != null && G.dpad.up.wasPressedThisFrame);
        public static bool Photo => (K != null && K.pKey.wasPressedThisFrame) || (G != null && G.dpad.right.wasPressedThisFrame);
        public static bool Hands => (K != null && K.hKey.wasPressedThisFrame) || (G != null && G.dpad.down.wasPressedThisFrame);
        public static bool Use => (K != null && K.fKey.wasPressedThisFrame) || (G != null && G.buttonWest.wasPressedThisFrame);
        public static bool Pause => (K != null && K.escapeKey.wasPressedThisFrame) || (G != null && G.startButton.wasPressedThisFrame);
        public static bool NextWeapon => (K != null && K.eKey.wasPressedThisFrame) || (M != null && M.scroll.ReadValue().y < -0.1f) ||
                                         (G != null && G.rightShoulder.wasPressedThisFrame);
        public static bool PrevWeapon => (K != null && K.qKey.wasPressedThisFrame) || (M != null && M.scroll.ReadValue().y > 0.1f) ||
                                         (G != null && G.leftShoulder.wasPressedThisFrame);
        public static bool AnyKey => (K != null && K.anyKey.wasPressedThisFrame) || AnyClick ||
                                     (G != null && G.buttonSouth.wasPressedThisFrame);
        public static bool AnyClick => M != null && M.leftButton.wasPressedThisFrame;
    }
}
