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
        public static readonly Vector3 ElevatorShaft = new Vector3(81f, 0f, -300f);   // Island.json hotel elevatorX
        public static readonly Vector3 Stairwell = new Vector3(-2.2f, 0f, -300f);     // island_assets.py STAIR_X centre (hotel-local x -42.2) + hotel (40, -300)

        public static bool Visited(string zone) => Progress.Value("zone_" + zone) > 0;

        /// <summary>
        /// The next thing to do, from the save (zones, rescues, weapons, big goals). Null during the room tutorial.
        /// target / hasTarget: where the compass and the map point.
        /// </summary>
        public static string Objective(RexWeapons w, Vector3 rex, out Vector3 target, out bool hasTarget)
        {
            string s = Step(w, rex, out target, out hasTarget, out StepId);
            SideHasTarget = false;
            // a running set piece leads (the step id stays: no NEW GOAL when it ends). The bar: the marker on the
            // nearest wave blob (a stuck one far off is found, not hunted for); the cave siege: its own line and spot
            if (BarDefense.Active)
            {
                hasTarget = true;
                target = NearestBarBlob(rex, out Vector3 blobAt) ? blobAt : BarDefense.Center;
            }
            else if (SiegeObjective(out string siegeLine, out Vector3 siegeAt, out bool siegeHas))
            {
                s = siegeLine;
                if (siegeHas) { target = siegeAt; hasTarget = true; }
            }
            // the first night: a warning on its own line under the goal (the goal and StepId stay, so the old goal
            // is not announced again when the night ends)
            Warning = DayNight.Night > 0.6f && Progress.Value("nights") < 1 ? "NIGHT: blobs glow and come in numbers. Stay near lights." : null;
            // the sniper rifle is optional (King Glorp shrugs off long shots): a side note while Rex is at the hotel
            // anyway, never a step that sends him back 1 km before the boss
            Side = w != null && w.Has(WeaponId.Pistol) && !w.Has(WeaponId.Sniper) && InZone(Zones[0], rex)
                   && PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) != 1
                ? SniperSide : null;
            // Defend the Bar is easy to miss (it starts only when Rex walks into the lounge bar): after the pistol and
            // while the factory runs, a side line says so. It wins over the sniper note (it goes away with the factory);
            // at the hotel the marker leads to the bar (the main goal leads again once Rex is out of the hotel)
            if (BarOpen(w) && !BarDefense.Active)
            {
                Side = BarSide;
                if (InZone(Zones[0], rex) && StepId != "rescue") { target = BarDefense.Center; hasTarget = true; }   // a guest to save comes first
            }
            // the cave siege starts only at night near the cave mouth (after the factory): a night side line says so. The
            // main marker stays; the cave mouth gets a second (blue) one, or the main one when the step has no target
            // (not from the tunnels or the ship: those have their own way out)
            if (CaveSiegeOpen())
            {
                Side = CaveSide;
                bool outside = !InTunnels(rex) && !InZone(Zones[7], rex);
                // the boss route through the cave mouth meets the siege: say so on the goal line itself (fight, or come
                // back by day); the temple's back door from the ruins does not pass the mouth
                if (outside && StepId == "glorp" && !InZone(Zones[5], rex)) { s = SiegeHint; Side = null; }
                Vector3 caveAt = NightSiege.Center;
                if (outside && !hasTarget) { target = caveAt; hasTarget = true; }
                else if (outside && (target - caveAt).sqrMagnitude > 20f * 20f) { SideTarget = caveAt; SideHasTarget = true; }
            }
            // a set piece just won: its summary (LastSummary) as the side line for ~8 s
            string won = EventSummary();
            if (won != null) Side = won;
            return s;
        }

        public const string CaveSide = "Night: the cave is stirring. Defend the cave mouth (north-west), or come back by day.";

        /// <summary>A second marker (blue on the compass and the map) for a side event while the main step keeps its
        /// own: the cave mouth while the siege can start. SideHasTarget is false otherwise.</summary>
        public static Vector3 SideTarget;
        public static bool SideHasTarget;

        /// <summary>The cave siege can start: night, the factory stopped, not done, not running (pause map: the cave
        /// fast travel stop warns).</summary>
        public static bool CaveSiegeOpen() => NightSiege.Open && !KingGlorpBoss.Fighting && !BarDefense.Active;

        /// <summary>The boss route's goal line while the siege can start (the cave mouth is on the way).</summary>
        static string SiegeHint => NightSiege.Hint ?? CaveSide;

        /// <summary>A set piece running now (Defend the Bar, the cave siege): no fast travel.</summary>
        public static bool EventRunning => BarDefense.Active || NightSiege.Active;

        // ------------------------------------------------------------ event summaries

        static readonly string[] SummaryDoneKeys = { BarDefense.DoneKey, NightSiege.DoneKey };
        static readonly string[] summarySeen = new string[2];
        static bool summaryLooked;
        static string summaryShown;
        static float summaryUntil;

        /// <summary>The LastSummary of BarDefense / NightSiege when it changed after a win (its goal is saved
        /// done), for 8 s; else null. The first look after a load only notes the old text (not news).</summary>
        static string EventSummary()
        {
            for (int i = 0; i < summarySeen.Length; i++)
            {
                string v = i == 0 ? BarDefense.LastSummary : NightSiege.LastSummary;
                if (!summaryLooked) { summarySeen[i] = v; continue; }
                // a new text counts once the goal is saved done (the event may write the text and the save in either order)
                if (v == summarySeen[i] || (!string.IsNullOrEmpty(v) && Progress.Value(SummaryDoneKeys[i]) <= 0)) continue;
                if (!string.IsNullOrEmpty(v)) { summaryShown = v; summaryUntil = Time.unscaledTime + 8f; }
                summarySeen[i] = v;
            }
            summaryLooked = true;
            return Time.unscaledTime < summaryUntil ? summaryShown : null;
        }

        public const string SniperSide = "Side: the sniper rifle is on the hotel roof (stairs or elevator to the top).";
        public const string BarSide = "Side: Blobs are heading for the lounge bar! (hotel, ground floor)";

        /// <summary>Defend the Bar can start: the pistol, the factory still running, not done yet.</summary>
        static bool BarOpen(RexWeapons w) =>
            w != null && w.Has(WeaponId.Pistol) && !FactoryCore.Stopped && !BarDefense.Done && !KingGlorpBoss.Fighting;

        /// <summary>The living wave blob nearest Rex while the bar is defended (BarDefense.NearestBlob), if any.</summary>
        static bool NearestBarBlob(Vector3 rex, out Vector3 at)
        {
            var b = BarDefense.NearestBlob();
            at = b != null ? b.transform.position : Vector3.zero;
            return b != null;
        }

        // ------------------------------------------------------------ cave siege (another set piece)

        /// <summary>The cave siege's HUD line while it runs (NightSiege.Objective) and its marker (the nearest wave
        /// blob, else the cave mouth).</summary>
        public static bool SiegeObjective(out string line, out Vector3 at, out bool hasAt)
        {
            line = null; at = Vector3.zero; hasAt = false;
            if (!NightSiege.Active) return false;
            line = NightSiege.Objective;
            if (string.IsNullOrEmpty(line)) line = "Hold out at the cave until the blobs stop coming!";
            at = NightSiege.Target; hasAt = true;
            return true;
        }

        /// <summary>Which step Objective returned last ("pool", "rescue", "pistol", "cart", "clubs", "cave", "factory",
        /// "glorp", "explore_&lt;zone&gt;", "free"): the HUD announces a new goal when it changes.</summary>
        public static string StepId;
        /// <summary>A warning line under the goal (the first night), or null.</summary>
        public static string Warning;
        /// <summary>An optional side note under the goal (Defend the Bar after the pistol, the sniper rifle while at
        /// the hotel), or null.</summary>
        public static string Side;

        static float rescueSeen = -100f;
        static Vector3 rescueAt;
        static bool rescueCalm;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { SideHasTarget = false; rescueSeen = -100f; nextFind = 0f; carts = null; lifts = null; doors = null; rescueCalm = false; StepId = null; Warning = null; Side = null;
                                     summaryLooked = false; summaryShown = null; summaryUntil = 0f; summarySeen[0] = summarySeen[1] = null; }   // editor with Domain Reload off

        static string Step(RexWeapons w, Vector3 rex, out Vector3 target, out bool hasTarget, out string id)
        {
            target = Vector3.zero;
            hasTarget = true;
            id = "pool";
            if (!Visited("pool"))
            {
                // still upstairs in the hotel (floor 2 is ~7.1, the ground ~3.5-4.2): first out of the room and down
                if (rex.y > 6f && InZone(Zones[0], rex))
                { target = WayDown(rex); return "Leave the room. Take the stairs or the elevator down to the pool."; }
                target = Zones[1].at; return "Go down to the pool. Something is wrong.";
            }
            int rescues = Progress.Value("rescues");
            id = "rescue";
            if (rescues < 3)
            {
                if (NearestHidingGuest(rex, out target, out bool calm))
                {
                    rescueSeen = Time.unscaledTime; rescueAt = target; rescueCalm = calm;
                    return RescueText(rescues, calm);
                }
                // night: guests lock themselves in. The step stays (no NEW GOAL at dusk and dawn); the marker shows the next one
                if (Guest.NightLockdown)
                {
                    string next = Rest(w, rex, out target, out hasTarget, out _);
                    return $"Rescue hiding guests ({rescues}/3). Guests are locked in until morning." + (hasTarget ? " Meanwhile: " + next : "");
                }
                // a guest just out of reach (or walking home at dusk): keep the step a while, no goal flicker
                if (Time.unscaledTime - rescueSeen < 20f)
                { target = rescueAt; return RescueText(rescues, rescueCalm); }
            }
            return Rest(w, rex, out target, out hasTarget, out id);
        }

        // a calm guest (no blob around) is rescued right away: "clear the blobs" would be wrong there
        static string RescueText(int rescues, bool calm) =>
            $"Rescue hiding guests ({rescues}/3): go close, {Glyph.B("Use")}." + (calm ? "" : " Clear the blobs first.");

        /// <summary>The steps after the pool and the first rescues.</summary>
        static string Rest(RexWeapons w, Vector3 rex, out Vector3 target, out bool hasTarget, out string id)
        {
            target = Vector3.zero;
            hasTarget = true;
            id = "pistol";
            if (w != null && !w.Has(WeaponId.Pistol)) { target = Pistol; return "Get the chrome pistol in the casino lounge (hotel, ground floor)."; }
            // the cart first: the golf course is a long walk west, the cart waits by the pool
            id = "cart";
            if (Progress.Value("cart_m") < 100 && Progress.Value("cart_dead") == 0)
            {
                if (NearestCart(rex, out target)) return $"Drive the cart from the pool to the golf course (west). {Glyph.B("Use")}: get in.";
                target = ProShop; return $"Drive a golf cart to the golf course: pro shop, west. {Glyph.B("Use")}: get in.";
            }
            id = "clubs";
            if (w != null && !w.Has(WeaponId.GolfClub)) { target = ProShop; return "Get the golf clubs at the pro shop (golf course, west)."; }
            id = "cave";
            if (!FactoryCore.Stopped && !Visited("cave") && !Visited("ruins"))
            { target = Zones[6].at; return "The cave behind the waterfall (north-west) leads to the ruins."; }
            id = "factory";
            if (!FactoryCore.Stopped) return FactoryRoute(rex, out target);
            id = "glorp";
            if (PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) != 1) return BossRoute(rex, out target);
            int zones = Progress.Value("zones");
            foreach (var z in Zones)
                if (!Visited(z.id)) { id = "explore_" + z.id; target = z.at; return $"Explore: {z.name} ({zones}/{Zones.Length} zones)."; }
            // after the main chain: one side challenge at a time (the step id stays "free": no NEW GOAL for each)
            id = "free";
            hasTarget = false;
            return Challenge();
        }

        /// <summary>The ship plateau is too steep to climb overland (~40 degrees: Rex slides back). The way up is the
        /// cave tunnels' ship branch, so the marker leads there: the cave mouth (or the temple's back door from the
        /// ruins), the ship branch's end inside the tunnels, the ship itself once Rex is in its zone.</summary>
        static string BossRoute(Vector3 rex, out Vector3 target)
        {
            if (InZone(Zones[7], rex)) { target = Zones[7].at; return "Pop King Glorp (crashed ship)."; }
            FindThings();
            if (InTunnels(rex))
            {
                target = Door("ShipTunnel_DoorA", ShipBranchEnd);
                return "Take the ship branch at the fork: it goes up to the crashed ship.";
            }
            if (InZone(Zones[5], rex) && DoorAt("RuinsTunnel_DoorB", out target))
                return "Take the cave tunnels up to the crashed ship: the temple's back door leads in.";
            target = Door("Cave_DoorA", Zones[6].at);
            return "Take the cave tunnels (north-west) up to the crashed ship.";
        }

        /// <summary>After the cave the factory is reached through the tunnels, not overland through rock: inside the
        /// tunnels the marker leads to the ruins branch, near the cave mouth into the cave, else to the factory.</summary>
        static string FactoryRoute(Vector3 rex, out Vector3 target)
        {
            FindThings();
            if (InTunnels(rex) && DoorAt("RuinsTunnel_DoorA", out target))
                return "Take the ruins branch at the fork: it leads to the blob factory.";
            if (rex.y > -20f && InZone(Zones[6], rex) && DoorAt("Cave_DoorA", out target))
                return "Stop the blob factory: the cave tunnels lead to the jungle ruins.";
            target = Factory;
            return "Stop the blob factory (jungle ruins, east).";
        }

        // Island.json tunnels: ~40-60 m under the island between the cave mouth, the ruins and the ship
        static readonly Vector3 ShipBranchEnd = new Vector3(-20f, -38f, 316f);
        static bool InTunnels(Vector3 p) => p.y < -20f && p.x > -175f && p.x < 5f && p.z > 190f && p.z < 330f;

        static Vector3 Door(string name, Vector3 fallback) => DoorAt(name, out Vector3 at) ? at : fallback;

        static bool DoorAt(string name, out Vector3 at)
        {
            at = Vector3.zero;
            if (doors == null) return false;
            foreach (var d in doors)
                if (d != null && d.name == name) { at = d.transform.position; return true; }
            return false;
        }

        static readonly string[] PhotoKinds = { "Tourist", "Lifeguard", "Bouncer" };
        static readonly string[] PhotoKindNames = { "a green tourist blob", "a red lifeguard blob", "a purple bouncer blob" };

        /// <summary>Free play: the first side challenge not done yet (photos of each blob type, rescues, the sniper
        /// rifle, then the accomplishments list).</summary>
        static string Challenge()
        {
            var photos = Progress.Photos;
            for (int k = 0; k < PhotoKinds.Length; k++)
            {
                bool has = false;
                foreach (var p in photos) if (p.subject == PhotoKinds[k]) { has = true; break; }
                if (!has) return $"Side challenge: take a photo with {PhotoKindNames[k]} ({Glyph.B("Photo")}).";
            }
            int rescues = Progress.Value("rescues");
            if (rescues < 20) return $"Side challenge: rescue 20 guests ({rescues}/20).";
            foreach (var a in Progress.All)
                if (!Progress.IsUnlocked(a.id))
                {
                    int v = Mathf.Min(Progress.Value(a.counter), a.need);
                    return $"Side challenge: {a.name}. {a.desc}" + (a.need > 1 ? $" ({v}/{a.need})" : "");
                }
            return "Free play: rescue guests, take photos, pop blobs. Everything is done. Legend.";
        }

        // golf carts and the elevator, found again every 10 s (zones can load later; a new scene after NEW GAME)
        static GolfCart[] carts;
        static Elevator[] lifts;
        static ZoneDoor[] doors;
        static float nextFind;

        static void FindThings()
        {
            if (Time.unscaledTime < nextFind && carts != null) return;
            nextFind = Time.unscaledTime + 10f;
            carts = Object.FindObjectsByType<GolfCart>(FindObjectsSortMode.None);
            lifts = Object.FindObjectsByType<Elevator>(FindObjectsSortMode.None);
            doors = Object.FindObjectsByType<ZoneDoor>(FindObjectsSortMode.None);
        }

        static bool NearestCart(Vector3 from, out Vector3 at)
        {
            FindThings();
            at = Vector3.zero;
            float best = float.MaxValue;
            foreach (var c in carts)
            {
                if (c == null) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d < best) { best = d; at = c.transform.position; }
            }
            return best < float.MaxValue;
        }

        /// <summary>The nearer of the stairwell and the elevator, at Rex's height (the marker floats on his floor).</summary>
        static Vector3 WayDown(Vector3 rex)
        {
            FindThings();
            Vector3 lift = ElevatorShaft;
            foreach (var e in lifts)
                if (e != null && e.cab != null) { lift = e.cab.position; break; }
            Vector3 a = new Vector3(Stairwell.x, rex.y, Stairwell.z), b = new Vector3(lift.x, rex.y, lift.z);
            return (a - rex).sqrMagnitude <= (b - rex).sqrMagnitude ? a : b;
        }

        static bool NearestHidingGuest(Vector3 from, out Vector3 at, out bool calm)
        {
            at = Vector3.zero;
            calm = false;
            float best = float.MaxValue;
            foreach (var g in Guest.All)
            {
                if (g == null || !g.CanRescue) continue;
                float d = (g.transform.position - from).sqrMagnitude;
                if (d < best) { best = d; at = g.transform.position; calm = g.IsCalm; }
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
