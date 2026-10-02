using UnityEngine;
using UnityEngine.InputSystem;

namespace RexMagnum
{
    /// <summary>
    /// Menu navigation with keyboard and controller (title screen, settings, pause menu). Works while the game is
    /// paused (unscaled time). Up/Down: arrows, W/S, D-pad, left stick (held = repeats). Left/Right: change a value.
    /// OK: Enter, Space, A. Back: Esc, Backspace, B. Page: Q/E or LB/RB.
    /// Every move, confirm and back clicks (Sfx ui_click, Click()): the menus' common sound.
    /// </summary>
    public static class MenuNav
    {
        static Keyboard K => Keyboard.current;
        static Gamepad G => Gamepad.current;
        static int dir, lastDir, frame = -1;
        static float nextRepeat;
        static int consumedFrame = -1;

        static void Poll()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            int d = 0;   // 1 up, 2 down, 3 left, 4 right
            if (K != null)
            {
                if (K[Key.UpArrow].isPressed || K[Key.W].isPressed) d = 1;
                else if (K[Key.DownArrow].isPressed || K[Key.S].isPressed) d = 2;
                else if (K[Key.LeftArrow].isPressed || K[Key.A].isPressed) d = 3;
                else if (K[Key.RightArrow].isPressed || K[Key.D].isPressed) d = 4;
            }
            if (d == 0 && G != null)
            {
                Vector2 st = G.leftStick.ReadValue();
                if (G.dpad.up.isPressed || st.y > 0.6f) d = 1;
                else if (G.dpad.down.isPressed || st.y < -0.6f) d = 2;
                else if (G.dpad.left.isPressed || st.x < -0.6f) d = 3;
                else if (G.dpad.right.isPressed || st.x > 0.6f) d = 4;
            }
            dir = 0;
            if (d != lastDir) { dir = d; nextRepeat = Time.unscaledTime + 0.4f; }
            else if (d != 0 && Time.unscaledTime > nextRepeat) { dir = d; nextRepeat = Time.unscaledTime + 0.09f; }
            lastDir = d;
            if (dir != 0) Click();
        }

        public static bool Up { get { Poll(); return dir == 1; } }
        public static bool Down { get { Poll(); return dir == 2; } }
        public static bool Left { get { Poll(); return dir == 3; } }
        public static bool Right { get { Poll(); return dir == 4; } }

        public static bool Ok => consumedFrame != Time.frameCount &&
            ((K != null && (K[Key.Enter].wasPressedThisFrame || K[Key.NumpadEnter].wasPressedThisFrame || K[Key.Space].wasPressedThisFrame)) ||
             (G != null && G.buttonSouth.wasPressedThisFrame));
        public static bool Back => consumedFrame != Time.frameCount &&
            ((K != null && (K[Key.Escape].wasPressedThisFrame || K[Key.Backspace].wasPressedThisFrame)) ||
             (G != null && G.buttonEast.wasPressedThisFrame));
        public static bool PagePrev => (K != null && K[Key.Q].wasPressedThisFrame) || (G != null && G.leftShoulder.wasPressedThisFrame);
        public static bool PageNext => (K != null && K[Key.E].wasPressedThisFrame) || (G != null && G.rightShoulder.wasPressedThisFrame);
        /// <summary>The press opened something: no other menu reacts to it this frame.</summary>
        public static void Consume() { consumedFrame = Time.frameCount; Click(); }      // a confirm or back: click
        public static bool Consumed => consumedFrame == Time.frameCount;
        /// <summary>A controller or keyboard was used for the menu last (show the selection frame).</summary>
        public static bool PadOrKeys => RMInput.LastDevice != RMInput.DeviceKind.Touch;

        // ------------------------------------------------------------ menu sound

        static AudioSource clickSrc;
        static int clickFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { clickSrc = null; clickFrame = -1; consumedFrame = -1; frame = -1; }   // editor with Domain Reload off

        /// <summary>The menu click (Sfx ui_click), quiet, once per frame. 2D (at the listener) and it plays while the
        /// pause menu has AudioListener.pause on (ignoreListenerPause); audio does not follow Time.timeScale.</summary>
        public static void Click()
        {
            if (clickFrame == Time.frameCount) return;
            clickFrame = Time.frameCount;
            var clip = Sfx.Get("ui_click");
            if (clip == null) return;
            if (clickSrc == null)
            {
                var go = new GameObject("MenuClick");
                Object.DontDestroyOnLoad(go);
                clickSrc = go.AddComponent<AudioSource>();
                clickSrc.playOnAwake = false;
                clickSrc.spatialBlend = 0f;
                clickSrc.ignoreListenerPause = true;
            }
            clickSrc.PlayOneShot(clip, 0.35f * AudioMix.Effects);
        }
    }
}
