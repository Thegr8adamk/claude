using UnityEngine;

namespace RexMagnum
{
    /// <summary>World pickup copy of a weapon or ammo, with a soft glow (brief). Press Use (F / X) near it.</summary>
    public class WeaponPickup : MonoBehaviour
    {
        public WeaponId weapon;
        public bool ammoOnly;
        public int ammoAmount = 12;
        public float radius = 2f;
        /// <summary>World ammo comes back this many seconds after it is taken (0 = one time: weapons, blob loot).</summary>
        public float respawnSeconds = 300f;
        float backAt = -1f;
        Light glow;
        Vector3 basePos;
        public static WeaponPickup Nearby { get; private set; }
        GameObject player;

        void Start()
        {
            basePos = transform.position;
            if (Time.timeSinceLevelLoad > 2f) respawnSeconds = 0f;     // dropped in play (guest reward, blob loot): one time
            glow =new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.type = LightType.Point;
            glow.range = 1.5f;
            glow.color = WeaponDef.Get(weapon).color;
            glow.shadows = LightShadows.None;
        }

        void Update()
        {
            if (backAt > 0f)
            {
                if (Time.time < backAt) return;
                backAt = -1f;
                Show(true);
            }
            transform.position = basePos + Vector3.up * (0.25f + Mathf.Sin(Time.time * 2f) * 0.05f);
            transform.Rotate(0, 60f * Time.deltaTime, 0, Space.World);
            glow.intensity = 1.2f + Mathf.Sin(Time.time * 3f) * 0.4f;
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");     // found once, not every frame
                // a weapon kept from the last session (saved inventory): its world copy is gone, so a restart gives no free ammo
                var pw = player != null ? player.GetComponent<RexWeapons>() : null;
                if (!ammoOnly && pw != null && pw.Has(weapon)) { Destroy(gameObject); return; }
            }
            var p = player;
            if (p == null) return;
            // flat distance (a pickup on a table or a rail is higher than Rex's feet) plus a height window
            Vector3 dp = basePos - p.transform.position;
            bool near = new Vector2(dp.x, dp.z).magnitude < radius && dp.y > -1f && dp.y < 2.2f;
            if (near) Nearby = this;
            else if (Nearby == this) Nearby = null;
            if (near && RMInput.Use && !PauseMenu.Open && !GameFlow.Busy)   // not from the pause menu or the title
            {
                var w = p.GetComponent<RexWeapons>();
                if (w == null) return;
                RMInput.ConsumeUse();
                if (ammoOnly && weapon == WeaponId.Coconut && !w.Has(weapon)) w.Give(weapon);   // a coconut is its own weapon
                else if (ammoOnly) w.AddAmmo(weapon, ammoAmount);
                else w.Give(weapon);
                RexTestHUD.Notice($"GOT: {WeaponDef.Get(weapon).name.ToUpper()}   ({RMInput.Btn("Wheel")}: weapon wheel)", 2.5f);
                Sfx.Play("bottle_clink", basePos, 0.6f);
                if (Nearby == this) Nearby = null;
                if (ammoOnly && respawnSeconds > 0f) { Show(false); backAt = Time.time + respawnSeconds; }
                else Destroy(gameObject);
            }
        }

        void Show(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            if (glow != null) glow.enabled = on;
        }

        public string Label => ammoOnly ? $"{RMInput.Btn("Use")}: take ammo ({WeaponDef.Get(weapon).name})"
                                        : $"{RMInput.Btn("Use")}: pick up {WeaponDef.Get(weapon).name}";
    }
}
