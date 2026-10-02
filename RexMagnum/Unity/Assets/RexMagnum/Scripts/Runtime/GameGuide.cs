using System.Collections.Generic;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Player guidance shared by the HUD, the pause map and the help pages: zone centres, the current objective
    /// ("NEXT: ..."), the island map picture's coordinates and the control list for the device used last.
    /// Positions are from Island/Island.json (Blender x east, y north) as Unity (x, 0, y).
    /// </summary>
    public static class GameGuide
    {
        public struct Zone { public string id, name; public Vector3 at; public float r; }
        public static readonly Zone[] Zones =
        {
            Z("hotel", "Resort hotel", 40, -300, 60), Z("pool", "Pool area", 40, -255, 60), Z("beach", "Beach", 40, -345, 80),
            Z("golf", "Golf course", -280, -40, 110), Z("jungle", "Thick jungle", 60, 60, 90), Z("ruins", "Jungle ruins", 300, 70, 45),
            Z("cave", "Cave", -160, 230, 25), Z("ship", "Crashed alien ship", 40, 300, 60),
        };
        static Zone Z(string id, string n, float x, float y, float r) => new Zone { id = id, name = n, at = new Vector3(x, 0f, y), r = r };

        public static readonly Vector3 ProShop = new Vector3(-280f, 0f, -126.4f);     // Island.json golf.proShop
        public static readonly Vector3 Factory = new Vector3(300f, 0f, 75f);          // Island.json factory.core

        public static bool Visited(string zone) => Progress.Value("zone_" + zone) > 0;

        /// <summary>
        /// The next thing to do, from the save (zones, rescues, weapons, big goals). Null during the room tutorial.
        /// target / hasTarget: where the compass and the map point.
        /// </summary>
        public static string Objective(RexWeapons w, Vector3 rex, out Vector3 target, out bool hasTarget)
        {
            target = Vector3.zero;
            hasTarget = true;
            if (!Visited("pool")) { target = Zones[1].at; return "Go down to the pool. Something is wrong."; }
            int rescues = Progress.Value("rescues");
            if (rescues < 3 && NearestHidingGuest(rex, out target))
                return $"Rescue hiding guests ({rescues}/3): go close, {RMInput.Btn("Use")}. Clear the blobs first.";
            if (w != null && !w.Has(WeaponId.GolfClub)) { target = ProShop; return "Get the golf clubs at the pro shop (golf course, west)."; }
            if (!FactoryCore.Stopped) { target = Factory; return "Stop the blob factory (jungle ruins, east)."; }
            if (PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) != 1) { target = Zones[7].at; return "Pop King Glorp (crashed ship, north)."; }
            int zones = Progress.Value("zones");
            foreach (var z in Zones)
                if (!Visited(z.id)) { target = z.at; return $"Explore: {z.name} ({zones}/{Zones.Length} zones)."; }
            hasTarget = false;
            return "Free play: rescue guests, take photos, pop blobs.";
        }

        static bool NearestHidingGuest(Vector3 from, out Vector3 at)
        {
            at = Vector3.zero;
            float best = float.MaxValue;
            foreach (var g in Guest.All)
            {
                if (g == null || !g.CanRescue) continue;
                float d = (g.transform.position - from).sqrMagnitude;
                if (d < best) { best = d; at = g.transform.position; }
            }
            return best < float.MaxValue;
        }

        /// <summary>World position -> island_map.png (Resources/UI/map) position, 0..1 from the top left.
        /// The picture is the 1000 m square centred on 0, north up (checked on the zone dots: 1.6 px per m).</summary>
        public static Vector2 MapUV(Vector3 p) => new Vector2(0.5f + p.x / 1000f, 0.5f - p.z / 1000f);

        /// <summary>The control list for the device used last (keys and buttons follow the remap).</summary>
        public static List<(string what, string how)> Controls()
        {
            string B(string a) => RMInput.Btn(a);
            var l = new List<(string, string)>();
            switch (RMInput.LastDevice)
            {
                case RMInput.DeviceKind.Touch:
                    l.Add(("Move", "Left stick (bottom left)"));
                    l.Add(("Look", "Drag on the right side"));
                    l.Add(("Run", "Push the stick to the edge"));
                    l.Add(("Jump", "Jump button"));
                    l.Add(("Attack", "Action button: tap = light, hold = heavy"));
                    l.Add(("Aim / scope", "Hold Action with a gun"));
                    l.Add(("Weapons", "Tap Weapon: next.  Hold Weapon: wheel"));
                    l.Add(("Use", "Use button (shows near things you can use)"));
                    break;
                case RMInput.DeviceKind.Gamepad:
                    l.Add(("Move", "Left stick"));
                    l.Add(("Look", "Right stick"));
                    l.Add(("Run", B("Run")));
                    l.Add(("Jump", B("Jump")));
                    l.Add(("Attack", $"{B("Action")}: tap = light, hold = heavy"));
                    l.Add(("Aim / scope", "hold " + B("Aim")));
                    l.Add(("Weapons", $"{B("NextWeapon")} / {B("PrevWeapon")}: next / previous.  {B("Wheel")}: wheel"));
                    l.Add(("Use", B("Use")));
                    break;
                default:
                    l.Add(("Move", $"{B("Forward")} {B("Left")} {B("Back")} {B("Right")}"));
                    l.Add(("Look", "Mouse"));
                    l.Add(("Run", "hold " + B("Run")));
                    l.Add(("Jump", B("Jump")));
                    l.Add(("Attack", $"{B("Action")}: tap = light, hold = heavy"));
                    l.Add(("Aim / scope", "hold " + B("Aim")));
                    l.Add(("Weapons", $"{B("NextWeapon")} / {B("PrevWeapon")} or mouse wheel.  {B("Wheel")}: wheel"));
                    l.Add(("Use", B("Use")));
                    break;
            }
            l.Add(("", "Use: pick up, eat, rescue a guest, drive the cart, call the elevator"));
            l.Add(("Hands down / up", B("Hands") + "  (talk to guests, run faster)"));
            l.Add(("Taunt", B("Taunt")));
            l.Add(("Photo", B("Photo")));
            l.Add(("First / third person", B("Camera")));
            if (RMInput.LastDevice != RMInput.DeviceKind.Touch) l.Add(("Swap shoulder", B("Shoulder")));
            l.Add(("Pause (map, help)", RMInput.LastDevice == RMInput.DeviceKind.Touch ? "II button (top corner)" : B("Pause")));
            return l;
        }
    }
}
