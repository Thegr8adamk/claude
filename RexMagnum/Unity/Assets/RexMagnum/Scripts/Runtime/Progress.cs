using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Save system + accomplishments (brief section 2 / step 8). One JSON file in Application.persistentDataPath
    /// (save.json): counters, unlocked accomplishments, photo album, visited zones. Saved on every change (small file).
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
            D("die", "Rex", "Finally. Publicity.", "Die. Wake up in room 304.", "deaths", 1),
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
            return data ?? (data = new Data());
        }

        static bool dirty;
        static void Save() => dirty = true;

        /// <summary>Writes the file if something changed (called every 2 s by ProgressTracker, and on quit).</summary>
        public static void Flush()
        {
            if (!dirty) return;
            dirty = false;
            try { File.WriteAllText(PathFile, JsonUtility.ToJson(Get())); } catch (Exception e) { Debug.LogWarning(e); }
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
    }

    /// <summary>Shows a toast when an accomplishment unlocks; tracks zone visits and nights survived.</summary>
    public class ProgressTracker : MonoBehaviour
    {
        string toast; float toastUntil;
        Transform rex;
        bool wasNight;
        static readonly (string zone, Vector3 at, float r)[] Zones =
        {
            ("hotel", new Vector3(40, 0, -300), 60), ("pool", new Vector3(40, 0, -255), 60), ("beach", new Vector3(40, 0, -345), 80),
            ("golf", new Vector3(-280, 0, -40), 110), ("jungle", new Vector3(60, 0, 60), 90), ("ruins", new Vector3(300, 0, 70), 45),
            ("cave", new Vector3(-160, 0, 230), 25), ("ship", new Vector3(40, 0, 300), 60),
        };

        void OnEnable() => Progress.Unlocked += Show;
        void OnDisable() => Progress.Unlocked -= Show;
        void Show(Progress.Def d) { toast = $"ACCOMPLISHMENT: {d.name}\n{d.desc}"; toastUntil = Time.unscaledTime + 4f; }

        void Start() { var r = FindFirstObjectByType<RexController>(); if (r != null) rex = r.transform; }

        float nextFlush;
        void OnApplicationPause(bool p) { if (p) Progress.Flush(); }
        void OnApplicationQuit() => Progress.Flush();

        void Update()
        {
            if (Time.unscaledTime > nextFlush) { nextFlush = Time.unscaledTime + 2f; Progress.Flush(); }
            if (rex != null && Time.frameCount % 30 == 0)
                foreach (var z in Zones)
                {
                    var p = rex.position; p.y = 0;
                    if (Vector3.Distance(p, z.at) < z.r) Progress.Visit(z.zone);
                }
            bool night = DayNight.Night > 0.8f;
            if (wasNight && !night) Progress.Add("nights");
            wasNight = night;
        }

        void OnGUI()
        {
            if (Time.unscaledTime > toastUntil) return;
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            var r = new Rect(w - 520 * s, h * 0.25f, 480 * s, 90 * s);
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var st = new GUIStyle(GUI.skin.label) { fontSize = (int)(20 * s), wordWrap = true, alignment = TextAnchor.MiddleCenter };
            st.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(r, toast, st);
        }
    }
}
