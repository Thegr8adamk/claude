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
        Light glow;
        Vector3 basePos;
        public static WeaponPickup Nearby { get; private set; }

        void Start()
        {
            basePos = transform.position;
            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.type = LightType.Point;
            glow.range = 1.5f;
            glow.color = WeaponDef.Get(weapon).color;
            glow.shadows = LightShadows.None;
        }

        void Update()
        {
            transform.position = basePos + Vector3.up * (0.25f + Mathf.Sin(Time.time * 2f) * 0.05f);
            transform.Rotate(0, 60f * Time.deltaTime, 0, Space.World);
            glow.intensity = 1.2f + Mathf.Sin(Time.time * 3f) * 0.4f;
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            bool near = Vector3.Distance(p.transform.position, basePos) < radius;
            if (near) Nearby = this;
            else if (Nearby == this) Nearby = null;
            if (near && RMInput.Use)
            {
                var w = p.GetComponent<RexWeapons>();
                if (w == null) return;
                RMInput.ConsumeUse();
                if (ammoOnly) w.AddAmmo(weapon, ammoAmount);
                else w.Give(weapon);
                if (Nearby == this) Nearby = null;
                Destroy(gameObject);
            }
        }

        public string Label => ammoOnly ? $"F: take ammo ({WeaponDef.Get(weapon).name})" : $"F: pick up {WeaponDef.Get(weapon).name}";
    }
}
