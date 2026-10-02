using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Save system + accomplishments (brief section 2 / step 8). One JSON file in Application.persistentDataPath
    /// (save.json): counters, unlocked accomplishments, photo album, visited zones, rescue clues, owned weapons + ammo.
    /// Saved on every change (small file).
    /// Accomplishment groups: Fight, Sniper, Rescue, Explore, Big goals, Rex, Photos.
    /// </summary>
    public static class Progress
    {
        [Serializable] public class Photo { public string file, caption, subject; public long time; }
        [Serializable] class Data
        {
            public List<string> keys = new List<string>();
            public List<int> values = new List<int>();
            public List<string> unlocked = new List<string>();
            public List<Photo> photos = new List<Photo>();
            public List<string> clues = new List<string>();          // rescue clues, shown in the pause map
            public List<int> weapons = new List<int>();             // owned WeaponIds (survive a relaunch)
            public List<int> ammoIds = new List<int>(), ammo = new List<int>();
        }

        public class Def { public string id, group, name, desc, counter; public int need; }
        public static readonly Def[] All =
        {
            D("first_pop", "Fight", "Booger Buster", "Pop your first blob.", "kills", 1),
            D("pop_50", "Fight", "Clean-Up at the Pool", "Pop 50 blobs.", "kills", 50),
            D("pop_250", "Fight", "Professional Janitor", "Pop 250 blobs.", "kills", 250),
            D("bottle_break", "Fight", "Last Call", "Break the beer bottle on a blob.", "bottle_breaks", 1),
            D("towel_stun", "Fight", "Locker Room Rules", "Stun 10 blobs with the towel snap.", "towel_stuns", 10),
            D("fish", "Fight", "Catch of the Day", "Pop 10 blobs with the frozen swordfish.", "kills_Swordfish", 10),
            D("golf", "Fight", "Hole in One", "Send 10 blobs flying with the golf club.", "kills_GolfClub", 10),
            D("coconut", "Fight", "Go Nuts", "Pop 3 blobs with one coconut.", "coconut_multi", 1),
            D("split", "Fight", "Twins", "Split a bouncer blob.", "bouncer_splits", 1),
            D("sniper_1", "Sniper", "That's a Wrap", "Pop a blob with the sniper rifle.", "kills_Sniper", 1),
            D("sniper_brain", "Sniper", "Head Shot. Eight by Ten.", "Hit 10 brains with the sniper rifle.", "sniper_brains", 10),
            D("sniper_long", "Sniper", "Long-Distance Relationship", "Sniper pop from 150 m or more.", "sniper_long", 1),
            D("rescue_1", "Rescue", "Tag Me", "Rescue a guest.", "rescues", 1),
            D("rescue_20", "Rescue", "Tell a Casting Director", "Rescue 20 guests.", "rescues", 20),
            D("carmen", "Rescue", "Still Got It", "Flirt with Carmen 3 times. (She says no.)", "carmen_flirts", 3),
            D("explore_all", "Explore", "I Know This Island", "Visit all 8 zones.", "zones", 8),
            D("cave", "Explore", "Spelunker", "Go through the cave tunnels.", "zone_cave", 1),
            D("cart_km", "Explore", "Battery Not Included", "Drive the golf cart 5 km.", "cart_m", 5000),
            D("cart_dead", "Explore", "Pushing It", "Run the cart battery flat.", "cart_dead", 1),
            D("night", "Explore", "Sunglasses at Night", "Survive a full night.", "nights", 1),
            D("factory", "Big goals", "Production Shut Down", "Destroy the blob factory core.", "factory", 1),
            D("glorp", "Big goals", "Long Live the King. Nah.", "Pop King Glorp.", "glorp", 1),
            D("taunt_25", "Rex", "Here's My Autograph", "Taunt 25 times.", "taunts", 25),
            D("slip_10", "Rex", "Stunt Fall", "Slip on slime 10 times.", "slips", 10),
            D("mirror", "Rex", "Hello, Handsome", "Admire yourself in a mirror.", "mirror", 1),
            D("die", "Rex", "Finally. Publicity.", "Die. Wake up in room 304, or at a checkpoint (cave, ruins, ship, boss).", "deaths", 1),
            D("photo_1", "Photos", "Me and a Fan", "Take a photo with a blob.", "photos_blob", 1),
            D("photo_10", "Photos", "Album", "Take 10 photos.", "photos", 10),
            D("photo_glorp", "Photos", "Me and Royalty", "Take a photo with King Glorp.", "photos_glorp", 1),
        };
        static Def D(string id, string g, string n, string d, string c, int need) =>
            new Def { id = id, group = g, name = n, desc = d, counter = c, need = need };

        static Data data;
        static string PathFile => System.IO.Path.Combine(Application.persistentDataPath, "save.json");
        public static string AlbumDir => System.IO.Path.Combine(Application.persistentDataPath, "Album");
        public static event Action<Def> Unlocked;

        static Data Get()
        {
            if (data != null) return data;
            try { data = File.Exists(PathFile) ? JsonUtility.FromJson<Data>(File.ReadAllText(PathFile)) : new Data(); }
            catch { data = new Data(); }
            if (data == null) data = new Data();
            // saves from before these lists existed load them as null
            if (data.clues == null) data.clues = new List<string>();
            if (data.weapons == null) data.weapons = new List<int>();
            if (data.ammoIds == null || data.ammo == null || data.ammoIds.Count != data.ammo.Count) { data.ammoIds = new List<int>(); data.ammo = new List<int>(); }
            return data;
        }

        static bool dirty;
        static void Save() => dirty = true;

        /// <summary>Writes the file if something changed (called every 2 s by ProgressTracker, and on quit).</summary>
        public static void Flush()
        {
            if (!dirty) return;
            dirty = false;
            try
            {
                // write a new file, then swap it in: a crash during the write cannot leave a broken save
                string tmp = PathFile + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(Get()));
                if (!File.Exists(PathFile)) File.Move(tmp, PathFile);
                else
                {
                    try { File.Replace(tmp, PathFile, null); }
                    catch (PlatformNotSupportedException) { File.Copy(tmp, PathFile, true); File.Delete(tmp); }   // some phones
                }
            }
            catch (Exception e) { Debug.LogWarning(e); }
        }

        public static int Value(string key)
        {
            var d = Get();
            int i = d.keys.IndexOf(key);
            return i < 0 ? 0 : d.values[i];
        }

        public static void Add(string key, int amount = 1) => Set(key, Value(key) + amount);
        public static void Max(string key, int v) { if (v > Value(key)) Set(key, v); }

        public static void Set(string key, int v)
        {
            var d = Get();
            int i = d.keys.IndexOf(key);
            if (i < 0) { d.keys.Add(key); d.values.Add(v); } else d.values[i] = v;
            foreach (var a in All)
                if (a.counter == key && v >= a.need && !d.unlocked.Contains(a.id))
                {
                    d.unlocked.Add(a.id);
                    Unlocked?.Invoke(a);
                    dirty = true;
                    Flush();
                }
            Save();
        }

        public static bool IsUnlocked(string id) => Get().unlocked.Contains(id);
        public static IReadOnlyList<Photo> Photos => Get().photos;

        public static void AddPhoto(Photo p)
        {
            Get().photos.Add(p);
            Add("photos");
            Save();
        }

        /// <summary>Zones (brief section 6) are visited when Rex comes near their centre.</summary>
        public static void Visit(string zone)
        {
            if (Value("zone_" + zone) > 0) return;
            Set("zone_" + zone, 1);
            Add("zones");
        }

        // ------------------------------------------------------------ rescue clues

        public static IReadOnlyList<string> Clues => Get().clues;

        /// <summary>Keeps a clue for the pause map (once). Returns false if it was known already.</summary>
        public static bool AddClue(string clue)
        {
            var d = Get();
            if (d.clues.Contains(clue)) return false;
            d.clues.Add(clue);
            Save();
            return true;
        }

        // ------------------------------------------------------------ weapons + ammo (RexWeapons calls these)

        /// <summary>Remembers the owned weapons and their ammo. Cheap: marks the file dirty only on a change
        /// (ProgressTracker writes it within 2 s), so it can be called after every shot or pickup.</summary>
        public static void SaveWeapons(IEnumerable<WeaponId> owned, Func<WeaponId, int> ammoOf)
        {
            var d = Get();
            bool changed = false;
            // the saved list is the inventory now: a weapon that is gone (beer bottle broken) is gone after a reload too
            var now = new HashSet<int>();
            foreach (var id in owned) now.Add((int)id);
            if (d.weapons.RemoveAll(w => !now.Contains(w)) > 0) changed = true;
            foreach (var id in owned)
            {
                if (!d.weapons.Contains((int)id)) { d.weapons.Add((int)id); changed = true; }
                int a = ammoOf(id), i = d.ammoIds.IndexOf((int)id);
                if (i < 0) { d.ammoIds.Add((int)id); d.ammo.Add(a); changed = true; }
                else if (d.ammo[i] != a) { d.ammo[i] = a; changed = true; }
            }
            if (changed) Save();
        }

        /// <summary>Puts the saved weapons and ammo into RexWeapons' lists (call in its Start, before Equip).
        /// Returns false when nothing is saved (new game).</summary>
        public static bool LoadWeapons(List<WeaponId> owned, Dictionary<WeaponId, int> ammo)
        {
            var d = Get();
            foreach (int w in d.weapons)
                if (Enum.IsDefined(typeof(WeaponId), w) && !owned.Contains((WeaponId)w)) owned.Add((WeaponId)w);
            for (int i = 0; i < d.ammoIds.Count; i++)
                if (Enum.IsDefined(typeof(WeaponId), d.ammoIds[i])) ammo[(WeaponId)d.ammoIds[i]] = d.ammo[i];
            return d.weapons.Count > 0;
        }

        // ------------------------------------------------------------ stats (pause menu, after the ending)

        /// <summary>The run in numbers: play time ("play_s", GameFlow counts it), blobs popped, guests rescued, photos,
        /// deaths, big goals and accomplishments. A new list per call: callers keep it while their page is open.</summary>
        public static List<(string what, string value)> Stats()
        {
            int sec = Value("play_s"), min = sec / 60;
            int goals = (Value("factory") > 0 ? 1 : 0) + (Value("glorp") > 0 ? 1 : 0) + (Value("bar_defended") > 0 ? 1 : 0)
                      + (Value("cave_siege") > 0 ? 1 : 0);      // GameFlow's GoalKeys: factory, King Glorp, the bar, the cave siege
            int done = 0;
            foreach (var a in All) if (IsUnlocked(a.id)) done++;
            return new List<(string, string)>
            {
                ("Play time", min >= 60 ? $"{min / 60} h {min % 60:00} min" : $"{min} min"),
                ("Blobs popped", Value("kills").ToString()),
                ("Guests rescued", Value("rescues").ToString()),
                ("Photos", Value("photos").ToString()),
                ("Deaths", Value("deaths").ToString()),
                ("Big goals", $"{goals} / 4"),
                ("Accomplishments", $"{done} / {All.Length}"),
            };
        }

        // ------------------------------------------------------------ new game

        /// <summary>
        /// NEW GAME (title menu): clears counters, accomplishments, the album (list and picture files), clues, weapons,
        /// and the factory / King Glorp / voice mail / checkpoint flags. Settings and key bindings stay.
        /// </summary>
        public static void ResetAll()
        {
            try { if (Directory.Exists(AlbumDir)) Directory.Delete(AlbumDir, true); }
            catch (Exception e) { Debug.LogWarning(e); }
            data = new Data();
            dirty = true;
            Flush();
            PlayerPrefs.DeleteKey(FactoryCore.SaveKey);
            PlayerPrefs.DeleteKey(KingGlorpBoss.SaveKey);
            PlayerPrefs.DeleteKey("RexMagnum_VoiceMail");      // MartyPhone: the voice mails start again
            RexLife.ClearSavedCheckpoint();                    // the zone checkpoint and the quit spot: wake up in room 304 again
            PlayerPrefs.Save();
        }
    }
}
