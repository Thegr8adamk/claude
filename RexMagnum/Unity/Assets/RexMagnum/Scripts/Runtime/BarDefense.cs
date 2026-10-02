using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>
    /// "Defend the bar" (mid-game set piece, one time). Starts when Rex walks into the lounge bar on the hotel's
    /// ground floor (within ~7 m of the counter for 1.5 s, not running past; a notice warns first) with the pistol, any time after that (before or after the blob factory,
    /// never in the boss fight or the cave siege: it can not be missed; it unlocks the cart horn). Three short waves of
    /// blobs come in from the pool doors, or the buffet-terrace doors when Rex stands at the pool doors (at most MaxAlive
    /// at once); bouncer halves count to their wave; a wave blob that makes no progress for StuckAfter s is moved next
    /// to the bar. The spawners around the hotel hold off meanwhile (BlobSpawner).
    /// Each wave puts a pistol ammo pickup on the counter (one at a time, removed at the end).
    /// Reward: all owned guns full + Progress "bar_defended" (saved: never again). Rex dies, goes more than
    /// AbortRadius away or stays upstairs over UpstairsAbort s: the event stops cleanly and can start again later
    /// (after a death the wave blobs go too). A win sets LastSummary (time, best time "bar_best_s", no hit).
    /// Done: Use at the counter (within ReplayRadius) starts a replay (no reward, score only), never while a guest,
    /// a golf cart or an elevator is within OtherUseRadius (they keep Use: OtherUseNear).
    /// No builder step: it attaches itself to Rex when a scene loads (like Ambience).
    /// HUD: Active, Wave, Waves, Left, Objective, NearestBlob(), Target, NoHitSoFar, ReplayHere, ReplayPrompt.
    /// </summary>
    public class BarDefense : MonoBehaviour
    {
        public const string DoneKey = "bar_defended";
        /// <summary>The lounge bar, hotel floor 1 (island_assets.py BarCounter, hotel-local (-31.5, 5.9) + hotel (40, -300)).</summary>
        public static readonly Vector3 Center = new Vector3(9f, 3.5f, -296f);
        // the hotel's doors to the pool (Island.json hotel.poolDoors), a few metres out on the pool side
        static readonly Vector3 SpawnAt = new Vector3(40f, 3.5f, -284f);
        // second way in: the buffet-terrace doors (hotel-local x 29.5-32.5), used when the pool doors are too close to Rex
        static readonly Vector3 SpawnAt2 = new Vector3(71f, 3.5f, -284f);
        /// <summary>Spawners closer than this to the bar do not spawn while the event runs.</summary>
        public const float HoldRadius = 80f;
        const float StartRadius = 7f, StartTop = 6.5f, AbortRadius = 60f, RetryAfter = 30f, SpawnEvery = 1.6f;
        const int MaxAlive = 4;
        const float StuckAfter = 20f, StuckRexDistance = 15f;
        const float UpstairsAbort = 20f;                                  // this long above the ground floor: he left the bar
        // start: this long near the counter, slower than a run (running past on the way to the pool doors is no start)
        const float StartDwell = 1.5f, StartMaxSpeed = 1.5f;     // a deliberate stand (walking past is ~3 m/s)
        // replay: a small fixed spot at the counter, and no other Use target close by
        const float ReplayRadius = 2.5f, OtherUseRadius = 3f;
        static readonly int[] WaveSize = { 4, 6, 7 };

        /// <summary>True while the waves run.</summary>
        public static bool Active { get; private set; }
        /// <summary>The wave running now (1..Waves), 0 when not active.</summary>
        public static int Wave { get; private set; }
        public static int Waves => WaveSize.Length;
        /// <summary>Blobs of this wave still to beat (alive + not yet in).</summary>
        public static int Left { get; private set; }
        /// <summary>Objective line for the HUD while active, else null.</summary>
        public static string Objective => Active ? $"{(Replaying ? "REPLAY - " : "")}DEFEND THE BAR! Wave {Wave}/{Waves}: {Left} blob{(Left == 1 ? "" : "s")} left" : null;
        /// <summary>Done before (saved).</summary>
        public static bool Done => Progress.Value(DoneKey) > 0;
        /// <summary>One line about the last won defence ("Bar held in 2:41 — best 2:30 — NO HIT!") for the UI, else null.</summary>
        public static string LastSummary { get; private set; }

        // score (the event's reward is one-time; a replay after it is done plays for the time only)
        public const string BestKey = "bar_best_s", NoHitKey = "bar_nohit";
        /// <summary>Best winning time in seconds (saved; 0 = none yet).</summary>
        public static int BestSeconds => Progress.Value(BestKey);
        /// <summary>True while the waves run and Rex has taken no damage since the start (HUD "NO HIT" tag).</summary>
        public static bool NoHitSoFar => Active && inst != null && (inst.rexHealth == null || inst.rexHealth.LastDamageTime < inst.startedAt);
        /// <summary>The run going now is a replay (no reward, score only).</summary>
        public static bool Replaying { get; private set; }
        /// <summary>Done before, Rex at the bar counter, nothing else going on: Use starts a replay.</summary>
        public static bool ReplayAvailable { get; private set; }
        /// <summary>True only when Use right now would start a replay: ReplayAvailable, Rex at the counter spot, no
        /// pickup / food, no guest, cart or elevator near (HUD: show the replay prompt only then).</summary>
        public static bool ReplayHere => ReplayAvailable && !Active && inst != null && !PauseMenu.Open
            && FoodPickup.Nearby == null && WeaponPickup.Nearby == null && inst.AtReplaySpot() && !OtherUseNear(inst.transform.position);
        /// <summary>HUD prompt while ReplayHere, else null (the HUD puts the Use button's name in front).</summary>
        public static string ReplayPrompt => ReplayHere
            ? $"Defend the bar again (score only{(BestSeconds > 0 ? ", best " + Clock(BestSeconds) : "")})" : null;
        static string Clock(int secs) => $"{secs / 60}:{secs % 60:00}";

        static BarDefense inst;

        /// <summary>The living wave blob nearest to Rex while active (HUD marker / compass), else null.</summary>
        public static Blob NearestBlob()
        {
            if (!Active || inst == null) return null;
            Vector3 from = inst.transform.position;
            Blob best = null;
            float bd = float.MaxValue;
            foreach (var b in inst.mine)
            {
                if (b == null || b.IsDead) continue;
                float d2 = (b.transform.position - from).sqrMagnitude;
                if (d2 < bd) { bd = d2; best = b; }
            }
            return best;
        }
        /// <summary>Where the HUD should point while active: the nearest wave blob, else the bar.</summary>
        public static Vector3 Target { get { var b = NearestBlob(); return b != null ? b.transform.position : Center; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false; Wave = 0; Left = 0; LastSummary = null; Replaying = false; ReplayAvailable = false;
            carts = null; elevators = null; nextFind = 0f;
        }   // editor with Domain Reload off

        static GolfCart[] carts;
        static Elevator[] elevators;
        static float nextFind;
        /// <summary>A guest not yet rescued, a golf cart or an elevator (cab or a landing door) within OtherUseRadius of p:
        /// Use belongs to them, never to a replay (NightSiege asks too). Carts / elevators are found again every 10 s.</summary>
        const float GuestUseR2 = 1.5f * 1.5f;
        public static bool OtherUseNear(Vector3 p)
        {
            const float r2 = OtherUseRadius * OtherUseRadius;
            foreach (var g in Guest.All)
                if (g != null && !g.Rescued && (g.transform.position - p).sqrMagnitude < GuestUseR2) return true;   // a guest: only right next to them (Rosa sits ~1 m from the counter)
            if (carts == null || Time.unscaledTime > nextFind || System.Array.Exists(carts, c => c == null) || System.Array.Exists(elevators, e => e == null))
            {
                nextFind = Time.unscaledTime + 10f;
                carts = RMFind.All<GolfCart>();
                elevators = RMFind.All<Elevator>();
            }
            foreach (var c in carts)
                if (c != null && (c.transform.position - p).sqrMagnitude < r2) return true;
            foreach (var e in elevators)
            {
                if (e == null) continue;
                if (e.cab != null && (e.cab.position - p).sqrMagnitude < r2) return true;
                if (e.landingDoors != null)
                    foreach (var door in e.landingDoors)
                        if (door != null && (door.position - p).sqrMagnitude < r2) return true;
            }
            return false;
        }

        /// <summary>Rex on the ground floor within ReplayRadius of the counter.</summary>
        bool AtReplaySpot()
        {
            Vector3 at = transform.position;
            if (at.y > StartTop) return false;
            Vector3 flat = at - Center; flat.y = 0f;
            return flat.sqrMagnitude < ReplayRadius * ReplayRadius;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            // NEW GAME reloads the scene (a new Rex): attach again on every load. -= first: never subscribed twice.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }
        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Attach();

        static void Attach()
        {
            var r = Object.FindAnyObjectByType<RexController>();
            if (r != null && r.GetComponent<BarDefense>() == null) r.gameObject.AddComponent<BarDefense>();
        }

        Health rexHealth;
        RexWeapons weapons;
        float nextCheck, retryAt, startedAt, highSince = -1f;
        float nearSince = -1f, noticeAgainAt, lastCheckTime;
        Vector3 lastCheckPos;
        WeaponPickup barAmmo;                                             // the pistol ammo on the counter (one per wave)
        int popped;
        readonly List<Blob> prefabs = new List<Blob>();
        readonly List<Blob> mine = new List<Blob>();

        void Start()
        {
            rexHealth = GetComponent<Health>();
            weapons = GetComponent<RexWeapons>();
        }

        void OnEnable() { inst = this; Blob.Split += OnSplit; }

        /// <summary>A wave bouncer popped: its halves belong to the wave too (it ends only when they are gone).</summary>
        void OnSplit(Blob parent, Blob half)
        {
            if (!Active || half == null || !mine.Contains(parent)) return;
            mine.Add(half);
            half.Alert(6f);
        }

        void OnDisable()
        {
            Blob.Split -= OnSplit;
            if (inst == this) inst = null;
            // a scene reload / NEW GAME mid-event: the spawners must not stay on hold
            if (Active) { Active = false; Wave = 0; Left = 0; }
            ClearAmmo();
            Replaying = false;
            ReplayAvailable = false;
        }

        void Update()
        {
            // replay: Use at the counter spot (other Use targets near it go first: a drink, a pickup, a guest, a cart)
            if (RMInput.Use && ReplayHere && FindPrefabs())
            {
                RMInput.ConsumeUse();
                Replaying = true;
                StartCoroutine(Run());
                return;
            }
            if (Active || Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;
            ReplayAvailable = CanReplay();
            // any time after the pistol (also after the factory): the event must not be missable (it unlocks the cart horn)
            if (Time.time < retryAt || Done || KingGlorpBoss.Fighting || NightSiege.Active || GameFlow.Busy) { nearSince = -1f; return; }
            if (weapons == null || !weapons.Has(WeaponId.Pistol) || (rexHealth != null && rexHealth.IsDead)) { nearSince = -1f; return; }
            Vector3 p = transform.position;
            // no surprise start: Rex must stay near the counter for StartDwell s, not just run past it (stairs -> pool doors)
            Vector3 moved = p - lastCheckPos; moved.y = 0f;
            float speed = moved.magnitude / Mathf.Max(0.01f, Time.time - lastCheckTime);
            lastCheckPos = p; lastCheckTime = Time.time;
            Vector3 flat = p - Center; flat.y = 0f;
            if (p.y > StartTop || flat.sqrMagnitude > StartRadius * StartRadius)
            { nearSince = -1f; return; }                                       // left: cancelled quietly
            if (speed > StartMaxSpeed) { if (nearSince >= 0f) nearSince = Time.time; return; }   // moving: the dwell starts over
            if (!FindPrefabs()) return;
            if (nearSince < 0f)
            {
                nearSince = Time.time;
                if (Time.time > noticeAgainAt) RexTestHUD.Notice("Stand at the bar to defend it.", 3f);
                noticeAgainAt = Time.time + 15f;                              // in and out at the edge: no notice spam
                return;
            }
            if (Time.time - nearSince < StartDwell) return;
            nearSince = -1f;
            StartCoroutine(Run());
        }

        /// <summary>A replay can start: done before, Rex alive at the counter spot, no other event / boss
        /// (checked every 0.5 s; ReplayHere adds the live spot / other-Use checks).</summary>
        bool CanReplay()
        {
            if (!Done || Time.time < retryAt || KingGlorpBoss.Fighting || NightSiege.Active || GameFlow.Busy || GolfCart.Driven != null) return false;
            if (rexHealth != null && rexHealth.IsDead) return false;
            return AtReplaySpot();
        }

        /// <summary>Blob prefabs copied from the island's spawners (no own prefab references: no builder change).</summary>
        bool FindPrefabs()
        {
            if (prefabs.Count > 0) return true;
            foreach (var sp in RMFind.All<BlobSpawner>())
            {
                if (sp.prefabs == null) continue;
                foreach (var b in sp.prefabs)
                    if (b != null && !b.isHalf && b.kind != Blob.Kind.KingGlorp && !prefabs.Contains(b)) prefabs.Add(b);
            }
            return prefabs.Count > 0;
        }

        Blob PrefabFor(int wave)
        {
            // wave 1: tourists; 2: + lifeguards; 3: + bouncers
            float r = Random.value;
            Blob.Kind k = wave == 0 ? Blob.Kind.Tourist
                        : wave == 1 ? (r < 0.65f ? Blob.Kind.Tourist : Blob.Kind.Lifeguard)
                        : (r < 0.4f ? Blob.Kind.Tourist : r < 0.75f ? Blob.Kind.Lifeguard : Blob.Kind.Bouncer);
            foreach (var b in prefabs) if (b.kind == k) return b;
            return prefabs[Random.Range(0, prefabs.Count)];
        }

        bool Aborted()
        {
            if (rexHealth != null && rexHealth.IsDead) return true;          // he wakes in room 304 (inside the 60 m)
            // upstairs (above the ground floor, like the start check) for UpstairsAbort s: the bar is left to them
            if (transform.position.y > StartTop)
            {
                if (highSince < 0f) highSince = Time.time;
                else if (Time.time - highSince > UpstairsAbort) return true;
            }
            else highSince = -1f;
            Vector3 flat = transform.position - Center; flat.y = 0f;
            return flat.sqrMagnitude > AbortRadius * AbortRadius;
        }

        void Abort()
        {
            ClearAmmo();
            Active = false;
            Wave = 0;
            Left = 0;
            // Rex died: the wave goes (bouncer halves too; a popping bouncer goes before it splits), so he does not
            // wake in room 304 with the wave still in the lobby. Else the blobs stay: they just stop being a wave.
            if (rexHealth != null && rexHealth.IsDead)
                foreach (var b in mine)
                {
                    if (b == null) continue;
                    SlimeFX.Splat(b.transform.position + Vector3.up * 0.3f, Vector3.up, b.BaseColor(), 8);
                    Destroy(b.gameObject);
                }
            mine.Clear();
            bool replay = Replaying;
            Replaying = false;
            retryAt = Time.time + (replay ? 5f : RetryAfter);              // a replay: try again soon (Use at the counter)
            RexTestHUD.Notice(replay ? "Replay over: the bar fell." : "The bar fell. Come back to defend it again.", 4f);
        }

        IEnumerator Run()
        {
            Active = true;
            ReplayAvailable = false;
            mine.Clear();
            startedAt = Time.time;
            highSince = -1f;
            popped = 0;
            RexVoice.Subtitle?.Invoke("REX: Nobody touches my bar. I have a tab here.");
            for (int w = 0; w < Waves; w++)
            {
                Wave = w + 1;
                RexTestHUD.Notice($"DEFEND THE BAR! Wave {Wave}/{Waves}", 3f);
                PutAmmo();
                int toSpawn = WaveSize[w];
                float nextSpawn = Time.time + 1.5f, nextAlert = 0f, lastProgress = Time.time;
                Left = toSpawn;
                int lastLeft = Left;
                while (toSpawn > 0 || mine.Count > 0)
                {
                    if (Aborted()) { Abort(); yield break; }
                    // a popped blob stays listed until it is gone (a bouncer's halves come out of it ~0.5 s after death)
                    int living = 0;
                    for (int i = mine.Count - 1; i >= 0; i--)
                    {
                        if (mine[i] == null) { mine.RemoveAt(i); popped++; }    // gone = popped (halves count too)
                        else if (!mine[i].IsDead) living++;
                    }
                    if (toSpawn > 0 && Time.time >= nextSpawn && mine.Count < MaxAlive && Blob.AliveCount() < BlobSpawner.GlobalCap)
                    {
                        nextSpawn = Time.time + SpawnEvery;
                        if (SpawnOne(w)) toSpawn--;
                    }
                    // they come for Rex from the pool doors (beyond their normal sight range)
                    if (Time.time >= nextAlert)
                    {
                        nextAlert = Time.time + 2f;
                        foreach (var b in mine) if (b != null) b.Alert(5f);
                    }
                    Left = toSpawn + living;
                    // no kill for StuckAfter s: blobs stuck far from Rex (a wall, the pool floor) are moved next to the bar
                    if (Left != lastLeft) { lastLeft = Left; lastProgress = Time.time; }
                    else if (living > 0 && Time.time - lastProgress > StuckAfter) { lastProgress = Time.time; Unstick(); }
                    yield return null;
                }
                if (w == Waves - 1) break;
                RexTestHUD.Notice($"Wave {Wave}/{Waves} cleared. More are coming!", 3f);
                for (float t = 0f; t < 4f; t += Time.deltaTime)                // a breather between the waves
                {
                    if (Aborted()) { Abort(); yield break; }
                    yield return null;
                }
            }
            Win();
        }

        /// <summary>A pistol ammo pickup on the bar counter at each wave start (one at a time: an untaken one stays).
        /// A copy of the guests' / blob loot pistol ammo, else of a world one.</summary>
        void PutAmmo()
        {
            if (barAmmo != null) return;
            GameObject src = null;
            if (Blob.Loot != null)
                foreach (var g in Blob.Loot)
                    if (g != null && g.TryGetComponent(out WeaponPickup wp) && wp.ammoOnly && wp.weapon == WeaponId.Pistol) { src = g; break; }
            bool fromWorld = false;
            if (src == null)
                foreach (var wp in RMFind.All<WeaponPickup>())
                    if (wp.ammoOnly && wp.weapon == WeaponId.Pistol) { src = wp.gameObject; fromWorld = true; break; }
            if (src == null) return;
            var go = Instantiate(src, Center + new Vector3(0f, 1.0f, 1.9f), Quaternion.identity);   // on the counter top (its centre ~1.9 m +z; in the 2 m reach)
            barAmmo = go.GetComponent<WeaponPickup>();
            barAmmo.respawnSeconds = 0f;                                  // one time (Start does it too after the first 2 s)
            if (fromWorld)
            {
                // a world copy brings its glow light and maybe its hidden (taken) state: fresh ones from Start
                foreach (var l in go.GetComponentsInChildren<Light>(true)) Destroy(l.gameObject);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            }
        }

        void ClearAmmo()
        {
            if (barAmmo != null) Destroy(barAmmo.gameObject);
            barAmmo = null;
        }

        /// <summary>Wave blobs far from Rex: warped to a dry NavMesh point near the bar (not next to Rex); none found:
        /// it counts as beaten (removed quietly).</summary>
        void Unstick()
        {
            Vector3 rexAt = transform.position;
            foreach (var b in mine.ToArray())
            {
                if (b == null || b.IsDead || (b.transform.position - rexAt).sqrMagnitude < StuckRexDistance * StuckRexDistance) continue;
                var agent = b.GetComponent<NavMeshAgent>();
                if (agent == null || !agent.enabled) continue;              // a lifeguard mid-dive comes up by itself
                bool moved = false;
                for (int tries = 0; tries < 8 && !moved; tries++)
                {
                    Vector2 o = Random.insideUnitCircle.normalized * Random.Range(3f, 7f);
                    if (!NavMesh.SamplePosition(Center + new Vector3(o.x, 0f, o.y), out NavMeshHit nh, 3f, NavMesh.AllAreas)) continue;
                    if (WaterVolume.Depth(nh.position + Vector3.up * 0.1f) > 0.2f || (nh.position - rexAt).sqrMagnitude < 6f * 6f) continue;
                    SlimeFX.Splat(b.transform.position + Vector3.up * 0.3f, Vector3.up, b.BaseColor(), 8);
                    agent.Warp(nh.position);
                    SlimeFX.Splat(nh.position + Vector3.up * 0.3f, Vector3.up, b.BaseColor(), 10);
                    b.Alert(6f);
                    moved = true;
                }
                if (!moved) Destroy(b.gameObject);
            }
        }

        /// <summary>One wave blob at the pool doors, or at the buffet-terrace doors when Rex is too close to the pool doors.</summary>
        bool SpawnOne(int wave) => SpawnNear(SpawnAt, wave) || SpawnNear(SpawnAt2, wave);

        bool SpawnNear(Vector3 at, int wave)
        {
            for (int tries = 0; tries < 6; tries++)
            {
                Vector2 o = Random.insideUnitCircle * 6f;
                Vector3 p = at + new Vector3(o.x, 0f, o.y);
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;
                if (WaterVolume.Depth(hit.position + Vector3.up * 0.1f) > 0.2f) continue;     // not on the pool floor
                if ((hit.position - transform.position).sqrMagnitude < 8f * 8f) continue;      // never next to Rex
                var b = Instantiate(PrefabFor(wave), hit.position, Quaternion.identity);
                b.Alert(6f);
                SlimeFX.Splat(hit.position + Vector3.up * 0.3f, Vector3.up, b.BaseColor(), 10);
                mine.Add(b);
                return true;
            }
            return false;
        }

        void Win()
        {
            ClearAmmo();
            Active = false;
            Wave = 0;
            Left = 0;
            bool replay = Replaying;
            Replaying = false;
            if (!replay) Progress.Set(DoneKey, 1);
            int secs = Mathf.Max(1, Mathf.RoundToInt(Time.time - startedAt));
            // score: best time (saved) and no hit (Rex took no damage in the whole event)
            bool noHit = rexHealth == null || rexHealth.LastDamageTime < startedAt;
            int best = BestSeconds;
            bool record = best > 0 && secs < best;
            if (best <= 0 || secs < best) Progress.Set(BestKey, secs);
            if (noHit) Progress.Max(NoHitKey, 1);
            LastSummary = $"{(replay ? "REPLAY: " : "")}Bar held in {Clock(secs)} — " +
                          (record ? "NEW BEST!" : $"best {Clock(best > 0 ? Mathf.Min(best, secs) : secs)}") + (noHit ? " — NO HIT!" : "");
            if (replay)
            {
                // a replay: the score only (no reward, no BIG GOAL banner)
                RexTestHUD.Notice(LastSummary, 5f);
                Sfx.Play("bottle_clink", transform.position, 0.9f);
                retryAt = Time.time + 5f;
                return;
            }
            // the reward: every gun Rex owns gets full ammo
            if (weapons != null)
                foreach (var id in weapons.OwnedInOrder())
                {
                    var d = WeaponDef.Get(id);
                    if (d.isGun) weapons.AddAmmo(id, d.maxAmmo);
                }
            Sfx.Play("bottle_clink", transform.position, 0.9f);           // cheers
            // GameFlow's BIG GOAL banner says the bar held: only the reward here
            RexTestHUD.Notice("Free refill: all your guns are full. (Added to your tab.)", 5f);
            RexVoice.Subtitle?.Invoke("REX: Bar's open. Put it on my tab.");
        }
    }
}
