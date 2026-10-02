using System.Collections.Generic;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Weapon wheel (brief section 7): hold Tab / hold Y / hold the touch Weapon button. Time slows down,
    /// the owned weapons sit in a ring; point with the mouse, right stick or a drag, release to equip.
    /// </summary>
    public class WeaponWheel : MonoBehaviour
    {
        public float slowMotion = 0.25f;
        public bool IsOpen { get; private set; }

        RexWeapons weapons;
        RexController controller;
        readonly List<WeaponId> items = new List<WeaponId>();
        Vector2 pointer;
        int selected = -1;
        float savedScale = 1f;
        GUIStyle label, small;

        void Awake()
        {
            weapons = GetComponent<RexWeapons>();
            controller = GetComponent<RexController>();
        }

        void Update()
        {
            if (PauseMenu.Open) return;
            // opens only at normal speed (not in the pause menu, not during the kill cam slow motion)
            bool want = controller != null && controller.InputEnabled && RMInput.WheelHeld;
            if (want && !IsOpen && Mathf.Approximately(Time.timeScale, 1f)) Open();
            else if (IsOpen && !Mathf.Approximately(Time.timeScale, slowMotion)) Close(false);   // kill cam / pause took over
            else if (!want && IsOpen) Close(true);
            TouchState.WheelOpen = IsOpen;
            if (!IsOpen) return;
            Vector2 stick = RMInput.WheelStick;
            if (stick.magnitude > 0.5f) pointer = stick.normalized;
            else pointer = Vector2.ClampMagnitude(pointer + new Vector2(RMInput.WheelPointer.x, RMInput.WheelPointer.y), 1f);
            selected = -1;
            if (pointer.magnitude > 0.35f && items.Count > 0)
            {
                float a = Mathf.Atan2(pointer.x, pointer.y) * Mathf.Rad2Deg;      // 0 = up, clockwise
                selected = Mathf.RoundToInt(((a + 360f) % 360f) / (360f / items.Count)) % items.Count;
            }
        }

        void Open()
        {
            items.Clear();
            items.AddRange(weapons.OwnedInOrder());
            if (items.Count < 2) return;          // nothing to choose
            IsOpen = true;
            pointer = Vector2.zero;
            savedScale = Time.timeScale;
            Time.timeScale = slowMotion;
            RMInput.LookBlocked = true;
        }

        void Close(bool equip)
        {
            IsOpen = false;
            bool ours = Mathf.Approximately(Time.timeScale, slowMotion);   // the pause menu / kill cam may own it now
            if (ours) Time.timeScale = savedScale;
            RMInput.LookBlocked = false;
            if (equip && ours && selected >= 0 && selected < items.Count) weapons.Equip(items[selected]);
        }

        void OnDisable() { if (IsOpen) Close(false); }

        void OnGUI()
        {
            if (!IsOpen) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            }
            float s = Mathf.Min(Screen.width, Screen.height);
            label.fontSize = Mathf.RoundToInt(s * 0.024f);
            small.fontSize = Mathf.RoundToInt(s * 0.02f);
            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float r = s * 0.3f, w = s * 0.2f, h = s * 0.085f;
            for (int i = 0; i < items.Count; i++)
            {
                float a = i * Mathf.PI * 2f / items.Count;
                Vector2 p = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * r;
                var d = WeaponDef.Get(items[i]);
                bool sel = i == selected, cur = items[i] == weapons.Current.id;
                GUI.color = sel ? d.color : new Color(1f, 1f, 1f, cur ? 0.95f : 0.7f);
                string ammo = d.isGun ? $"\n{weapons.Ammo(d.id)}" : "";
                GUI.Box(new Rect(p.x - w / 2, p.y - h / 2, w * (sel ? 1.1f : 1f), h * (sel ? 1.1f : 1f)), d.name + ammo, label);
            }
            GUI.color = Color.white;
            string hint = selected >= 0 ? WeaponDef.Get(items[selected]).word.ToUpperInvariant() : "point at a weapon, release to take it";
            GUI.Label(new Rect(c.x - s * 0.3f, c.y - h / 2, s * 0.6f, h), hint, small);
        }
    }
}
