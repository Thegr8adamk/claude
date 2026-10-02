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
        /// <summary>top: Rex must be below this height (0 = any height).</summary>
        public struct Zone { public string id, name; public Vector3 at; public float r, top; }
        // Room 304 is at (16, 10.7, -293.7): inside the hotel (25 m from its centre), 45 m from the pool centre and
        // 57 m from the beach centre. Pool and beach stay smaller than that and count only on the ground (y < 6;
        // ground ~3.5-4.2, floor 3 is 10.7), so the room never visits them and "Go down to the pool" shows.
        public static readonly Zone[] Zones =
        {
            Z("hotel", "Resort hotel", 40, -300, 60), Z("pool", "Pool area", 40, -255, 25, 6f), Z("beach", "Beach", 40, -345, 35, 6f),
            Z("golf", "Golf course", -280, -40, 110), Z("jungle", "Thick jungle", 60, 60, 90), Z("ruins", "Jungle ruins", 300, 70, 45),
            Z("cave", "Cave", -160, 230, 25), Z("ship", "Crashed alien ship", 40, 300, 60),
        };
        static Zone Z(string id, string n, float x, float y, float r, float top = 0f) =>
            new Zone { id = id, name = n, at = new Vector3(x, 0f, y), r = r, top = top };

        /// <summary>Rex is in this zone (flat distance to the centre, and below the zone's top if it has one).</summary>
        public static bool InZone(Zone z, Vector3 p)
        {
            if (z.top > 0f && p.y >= z.top) return false;
            p.y = 0f;
            return (p - z.at).sqrMagnitude < z.r * z.r;
        }

        public static readonly Vector3 ProShop = new Vector3(-280f, 0f, -126.4f);     // Island.json golf.proShop
        public static readonly Vector3 Factory = new Vector3(300f, 0f, 75f);          // Island.json factory.core
        public static readonly Vector3 Pistol = new Vector3(54f, 4.28f, -304f);       // Island.json hotel pistol (casino lounge, floor 1)
        public static readonly Vector3 Sniper = new Vector3(82f, 21.53f, -292f);      // Island.json hotel sniper (roof)

        public static bool Visited(string zone) => Progress.Value("zone_" + zone) > 0;

        /// <summary>
        /// The next thing to do, from the save (zones, rescues, weapons, big goals). Null during the room tutorial.
        /// target / hasTarget: where the compass and the map point.
        /// </summary>
        public static string Objective(RexWeapons w, Vector3 rex, out Vector3 target, out bool hasTarget)
        {
            string s = Step(w, rex, out target, out hasTarget, out StepId);
            // the first night: a warning on its own line under the goal (the goal and StepId stay, so the old goal
            // is not announced again when the night ends)
            Warning = DayNight.Night > 0.6f && Progress.Value("nights") < 1 ? "NIGHT: blobs glow and come in numbers. Stay near lights." : null;
            return s;
        }

        /// <summary>Which step Objective returned last ("pool", "rescue", "pistol", "clubs", "cart", "cave", "factory",
        /// "sniper", "glorp", "explore_&lt;zone&gt;", "free"): the HUD announces a new goal when it changes.</summary>
        public static string StepId;
        /// <summary>A warning line under the goal (the first night), or null.</summary>
        public static string Warning;

        static string Step(RexWeapons w, Vector3 rex, out Vector3 target, out bool hasTarget, out string id)
        {
            target = Vector3.zero;
            hasTarget = true;
            id = "pool";
            if (!Visited("pool")) { target = Zones[1].at; return "Go down to the pool. Something is wrong."; }
            int rescues = Progress.Value("rescues");
            id = "rescue";
            if (rescues < 3 && NearestHidingGuest(rex, out target))
                return $"Rescue hiding guests ({rescues}/3): go close, {Glyph.B("Use")}. Clear the blobs first.";
            id = "pistol";
            if (w != null && !w.Has(WeaponId.Pistol)) { target = Pistol; return "Get the chrome pistol in the casino lounge (hotel, ground floor)."; }
            id = "clubs";
            if (w != null && !w.Has(WeaponId.GolfClub)) { target = ProShop; return "Get the golf clubs at the pro shop (golf course, west)."; }
            id = "cart";
            if (Progress.Value("cart_m") < 100 && Progress.Value("cart_dead") == 0)
            { target = ProShop; return $"Drive a golf cart: pro shop, west. {Glyph.B("Use")}: get in."; }
            id = "cave";
            if (!FactoryCore.Stopped && !Visited("cave") && !Visited("ruins"))
            { target = Zones[6].at; return "The cave behind the waterfall (north-west) leads to the ruins."; }
            id = "factory";
            if (!FactoryCore.Stopped) { target = Factory; return "Stop the blob factory (jungle ruins, east)."; }
            id = "sniper";
            if (w != null && !w.Has(WeaponId.Sniper) && PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) != 1)
            { target = Sniper; return "Get the sniper rifle on the hotel roof (stairs or elevator)."; }
            id = "glorp";
            if (PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) != 1) { target = Zones[7].at; return "Pop King Glorp (crashed ship, north)."; }
            int zones = Progress.Value("zones");
            foreach (var z in Zones)
                if (!Visited(z.id)) { id = "explore_" + z.id; target = z.at; return $"Explore: {z.name} ({zones}/{Zones.Length} zones)."; }
            id = "free";
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
            string B(string a) => Glyph.B(a);          // controller: coloured "[A]" tokens
            var l = new List<(string, string)>();
            switch (RMInput.LastDevice)
            {
                case RMInput.DeviceKind.Touch:
                    l.Add(("Move", "Left stick (bottom left)"));
                    l.Add(("Look", "Drag on the right side"));
                    l.Add(("Run", "Push the stick to the edge"));
                    l.Add(("Jump", "Jump button"));
                    l.Add(("Dodge", "Dodge button (left of Jump): quick dash, untouchable for a moment"));
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
                    l.Add(("Dodge", B("Dodge") + "  (quick dash, untouchable for a moment; no direction = back)"));
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
                    l.Add(("Dodge", B("Dodge") + "  (quick dash, untouchable for a moment; no direction = back)"));
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
