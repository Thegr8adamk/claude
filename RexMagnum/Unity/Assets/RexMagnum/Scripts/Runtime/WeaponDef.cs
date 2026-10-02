using UnityEngine;

namespace RexMagnum
{
    public enum WeaponId { Fists, Bottle, BottleBroken, Towel, Knife, GolfClub, Swordfish, Pistol, Harpoon, Sniper, Coconut }
    public enum DeathStyle { Pop, Leak, Freeze, Fling, Pinned, Blast }
    public enum Grip { Melee, Gun, Throw, None }

    /// <summary>Weapon table from the project brief (section 5). Melee: the hold (heavy) attack does ~3x the tap
    /// (blobs: tourist 40, lifeguard 30, bouncer 90 / half 40), so a heavy is worth the wind-up.</summary>
    public class WeaponDef
    {
        public WeaponId id;
        public string name, word, model;
        public Color color;
        public string tapTrigger, holdTrigger;
        public float tapDamage, holdDamage, reach;
        public Grip grip;
        public bool isGun;
        public int maxAmmo, startAmmo;
        public DeathStyle tapDeath = DeathStyle.Pop, holdDeath = DeathStyle.Pop;
        public bool killCamOnHold = true;

        static readonly Color Orange = new Color(1f, 0.55f, 0.15f), Red = new Color(1f, 0.25f, 0.2f),
            Cyan = new Color(0.4f, 0.9f, 1f), Yellow = new Color(1f, 0.85f, 0.2f), Green = new Color(0.5f, 1f, 0.4f);

        public static readonly WeaponDef[] All =
        {
            new WeaponDef { id = WeaponId.Fists, name = "Fists", word = "Punch", color = Orange, tapTrigger = "Jab", holdTrigger = "Heavy",
                tapDamage = 12, holdDamage = 36, reach = 1.4f, grip = Grip.None },
            new WeaponDef { id = WeaponId.Bottle, name = "Beer bottle", word = "Swing", model = "Weapon_Bottle", color = Yellow,
                tapTrigger = "Swing", holdTrigger = "Heavy", tapDamage = 15, holdDamage = 45, reach = 1.6f, grip = Grip.Melee },
            new WeaponDef { id = WeaponId.BottleBroken, name = "Broken bottle", word = "Stab", model = "Weapon_BottleBroken", color = Red,
                tapTrigger = "Stab", holdTrigger = "Heavy", tapDamage = 18, holdDamage = 54, reach = 1.4f, grip = Grip.Melee,
                tapDeath = DeathStyle.Leak, holdDeath = DeathStyle.Leak },
            new WeaponDef { id = WeaponId.Towel, name = "Beach towel", word = "Whip", model = "Weapon_Towel", color = Cyan,
                tapTrigger = "Whip", holdTrigger = "Whip", tapDamage = 8, holdDamage = 24, reach = 2.2f, grip = Grip.Melee },
            new WeaponDef { id = WeaponId.Knife, name = "Steak knife", word = "Stab", model = "Weapon_Knife", color = Red,
                tapTrigger = "Stab", holdTrigger = "Stab", tapDamage = 25, holdDamage = 75, reach = 1.4f, grip = Grip.Melee,
                tapDeath = DeathStyle.Leak, holdDeath = DeathStyle.Leak },
            new WeaponDef { id = WeaponId.GolfClub, name = "Golf club", word = "Swing", model = "Weapon_GolfClub", color = Green,
                tapTrigger = "Swing", holdTrigger = "GolfSwing", tapDamage = 22, holdDamage = 66, reach = 2.0f, grip = Grip.Melee,
                holdDeath = DeathStyle.Fling },
            new WeaponDef { id = WeaponId.Swordfish, name = "Frozen swordfish", word = "Swing", model = "Weapon_Swordfish", color = Cyan,
                tapTrigger = "Swing", holdTrigger = "Heavy", tapDamage = 28, holdDamage = 84, reach = 2.4f, grip = Grip.Melee,
                tapDeath = DeathStyle.Freeze, holdDeath = DeathStyle.Freeze },
            new WeaponDef { id = WeaponId.Pistol, name = "Chrome pistol", word = "Shoot", model = "Weapon_Pistol", color = Orange,
                tapDamage = 25, holdDamage = 35, reach = 120, grip = Grip.Gun, isGun = true, maxAmmo = 60, startAmmo = 24 },
            new WeaponDef { id = WeaponId.Harpoon, name = "Harpoon gun", word = "Shoot", model = "Weapon_HarpoonGun", color = Yellow,
                tapDamage = 45, holdDamage = 45, reach = 40, grip = Grip.Gun, isGun = true, maxAmmo = 12, startAmmo = 6,
                tapDeath = DeathStyle.Pinned, holdDeath = DeathStyle.Pinned },
            new WeaponDef { id = WeaponId.Sniper, name = "Sniper rifle", word = "Shoot", model = "Weapon_Sniper", color = Red,
                tapDamage = 150, holdDamage = 150, reach = 400, grip = Grip.Gun, isGun = true, maxAmmo = 20, startAmmo = 8 },
            new WeaponDef { id = WeaponId.Coconut, name = "Exploding coconut", word = "Throw", model = "Weapon_Coconut", color = Orange,
                tapDamage = 90, holdDamage = 90, reach = 4, grip = Grip.Throw, isGun = true, maxAmmo = 10, startAmmo = 3,
                tapDeath = DeathStyle.Blast, holdDeath = DeathStyle.Blast },
        };

        public static WeaponDef Get(WeaponId id) => All[(int)id];
    }
}
