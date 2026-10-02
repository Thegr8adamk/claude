using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>
    /// "Cave siege" (set piece between the factory and the boss, one time). After the blob factory is stopped, at night,
    /// when Rex walks toward the cave mouth (ZoneDoor "Cave_DoorA") within StartRadius, or stands within NearCenter of
    /// the fight's centre (never within 60 s of a respawn / fast travel: Breather; MaxFails deaths in one night: the
    /// blobs give up till dawn, SkippedTonight): two waves of glowing blobs come out
    /// of the cave (at most MaxAlive at once; bouncer halves count to their wave); the spawners near the cave hold off
    /// meanwhile (BlobSpawner). Reward: coconuts + ammo for every owned gun, Progress "cave_siege" (saved: never again).
    /// Rex dies, goes more than AbortRadius away (or 15 m up/down: the tunnels) or the night ends: the event stops
    /// cleanly and can start again later (after a death the wave blobs go too). The fight is MouthOut m out in front of
    /// the door, and the cave doors do not take Rex while it runs (ZoneDoor). A win sets LastSummary.
    /// Score: best time "siege_best_s" and no hit in LastSummary. Done: Use at the cave mouth at night starts a replay
    /// (no reward, score only). Never starts while Rex drives the golf cart.
    /// No builder step: it attaches itself to Rex when a scene loads (like BarDefense).
    /// HUD: Active, Wave, Waves, Left, Objective, NearestBlob(), Target, NoHitSoFar, ReplayAvailable, ReplayPrompt.
    /// </summary>
    public class NightSiege : MonoBehaviour
    {
        public const string DoneKey = "cave_siege";
        /// <summary>The cave zone (Island.json zones.cave): fallback when the cave door is not in the scene.</summary>
        static readonly Vector3 CaveZone = new Vector3(-160f, 0f, 230f);
        /// <summary>Spawners closer than this to the cave mouth do not spawn while the event runs.</summary>
        public const float HoldRadius = 70f;
        const float StartRadius = 25f, AbortRadius = 60f, RetryAfter = 30f, SpawnEvery = 1.8f, StartNight = 0.6f, EndNight = 0.25f;
        // not a trap on the boss route: it starts only when Rex heads for the door (or stands at the fight's centre),
        // never right after a respawn / fast travel, and after MaxFails deaths in one night the blobs give up till dawn
        const float NearCenter = 10f, TowardSpeed = 0.5f, BreatherAfterJump = 60f, JumpDistance = 25f;
        const int MaxFails = 2;
        const float StuckAfter = 20f, StuckRexDistance = 15f;
        const float MouthOut = 9f, DoorClear = 4f, AbortHeight = 15f;
        const int MaxAlive = 4;
        static readonly int[] WaveSize = { 5, 7 };

        /// <summary>True while the waves run.</summary>
        public static bool Active { get; private set; }
        /// <summary>The wave running now (1..Waves), 0 when not active.</summary>
        public static int Wave { get; private set; }
        public static int Waves => WaveSize.Length;
        /// <summary>Blobs of this wave still to beat (alive + not yet out).</summary>
        public static int Left { get; private set; }
        /// <summary>The fight's centre: MouthOut m in front of the cave door when found, else the cave zone at Rex's height.</summary>
        public static Vector3 Center { get; private set; } = CaveZone;
        /// <summary>Objective line for the HUD while active, else null.</summary>
        public static string Objective => Active ? $"{(Replaying ? "REPLAY - " : "")}CAVE SIEGE! Wave {Wave}/{Waves}: {Left} blob{(Left == 1 ? "" : "s")} left" : null;
        /// <summary>Done before (saved).</summary>
        public static bool Done => Progress.Value(DoneKey) > 0;
        /// <summary>No auto-start for this many seconds (fast travel, respawn; a big jump of Rex sets it too).</summary>
        public static void Breather(float seconds) => breatherUntil = Mathf.Max(breatherUntil, Time.time + seconds);
        static float breatherUntil;
        /// <summary>Rex died in it MaxFails times tonight: no siege till the next night (the cave doors are open).</summary>
        public static bool SkippedTonight { get; private set; }
        static int failsTonight;
        /// <summary>The siege can start tonight (night, the factory stopped, not done, not running, not given up).</summary>
        public static bool Open => !Active && !Done && !SkippedTonight && FactoryCore.Stopped && DayNight.Night >= StartNight;
        /// <summary>Objective / side line while the siege can start tonight, else null.</summary>
        public static string Hint => Open ? "The cave is besieged at night: fight, or come back at dawn." : null;
        /// <summary>One line about the last won siege ("Cave held in 2:41 - 17 blobs popped") for the UI, else null.</summary>
        public static string LastSummary { get; private set; }

        // score (the reward is one-time; a replay after it is done plays for the time only)
        public const string BestKey = "siege_best_s", NoHitKey = "siege_nohit";
        /// <summary>Best winning time in seconds (saved; 0 = none yet).</summary>
        public static int BestSeconds => Progress.Value(BestKey);
        /// <summary>True while the waves run and Rex has taken no damage since the start (HUD "NO HIT" tag).</summary>
        public static bool NoHitSoFar => Active && inst != null && (inst.rexHealth == null || inst.rexHealth.LastDamageTime < inst.startedAt);
        /// <summary>The run going now is a replay (no reward, score only).</summary>
        public static bool Replaying { get; private set; }
        /// <summary>Done before, night, Rex at the cave mouth, nothing else going on: Use starts a replay.</summary>
        public static bool ReplayAvailable { get; private set; }
        /// <summary>HUD prompt while ReplayAvailable, else null (the HUD puts the Use button's name in front).</summary>
        public static string ReplayPrompt => ReplayAvailable
            ? $"Fight the cave siege again (score only{(BestSeconds > 0 ? ", best " + Clock(BestSeconds) : "")})" : null;
        static string Clock(int secs) => $"{secs / 60}:{secs % 60:00}";

        static NightSiege inst;

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
        /// <summary>Where the HUD should point while active: the nearest wave blob, else the cave mouth.</summary>
        public static Vector3 Target { get { var b = NearestBlob(); return b != null ? b.transform.position : Center; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false; Wave = 0; Left = 0; Center = CaveZone; LastSummary = null;
            breatherUntil = 0f; SkippedTonight = false; failsTonight = 0; Replaying = false; ReplayAvailable = false;
        }   // editor with Domain Reload off

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            // NEW GAME reloads the scene (a new Rex): attach again on every load. -= first: never subscribed twice.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }
        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            // NEW GAME / a load: last session's night (deaths, give-up, breather) does not carry over
            SkippedTonight = false; failsTonight = 0; breatherUntil = 0f; Replaying = false; ReplayAvailable = false;
            Attach();
        }

        static void Attach()
        {
            var r = Object.FindAnyObjectByType<RexController>();
            if (r != null && r.GetComponent<NightSiege>() == null) r.gameObject.AddComponent<NightSiege>();
        }

        Health rexHealth;
        RexWeapons weapons;
        float nextCheck, retryAt, startedAt;
        Vector3 lastPos, checkPos;                                            // last frame (jump check) / last 0.5 s check (heading)
        bool hasLastPos;
        QualityAuto quality;
        int popped;
        bool mouthFound;
        Vector3 doorAt;                                                       // the cave door's trigger (spawns stay clear of it)
        Light waveLight;                                                      // one glow for the wave, not one per blob
        readonly List<Blob> prefabs = new List<Blob>();
        readonly List<Blob> mine = new List<Blob>();

        void Start()
        {
            rexHealth = GetComponent<Health>();
            weapons = GetComponent<RexWeapons>();
            quality = FindAnyObjectByType<QualityAuto>();
        }

        void OnEnable() { inst = this; Blob.Split += OnSplit; }

        void OnDisable()
        {
            Blob.Split -= OnSplit;
            if (inst == this) inst = null;
            // a scene reload / NEW GAME mid-event: the spawners must not stay on hold
            if (Active) { Active = false; Wave = 0; Left = 0; }
            Replaying = false;
            ReplayAvailable = false;
            DropLight();
        }

        /// <summary>A wave bouncer popped: its halves belong to the wave too.</summary>
        void OnSplit(Blob parent, Blob half)
        {
            if (!Active || half == null || !mine.Contains(parent)) return;
            mine.Add(half);
            half.Alert(6f);
        }

        /// <summary>The fight's centre: MouthOut m out of the cave door (ZoneDoor "Cave_DoorA") along its way out (toward
        /// the tunnel door's exit point "Cave_ExitA" in front of it), on the NavMesh; no door: the cave zone at Rex's height.</summary>
        void FindMouth()
        {
            if (mouthFound) return;
            ZoneDoor mouth = null;
            Transform outside = null;
            foreach (var zd in FindObjectsByType<ZoneDoor>(FindObjectsSortMode.None))
            {
                if (zd == null) continue;
                if (zd.name == "Cave_DoorA") mouth = zd;
                else if (zd.name == "Cave_DoorB" && zd.exit != null) outside = zd.exit;
            }
            if (mouth == null) { Center = new Vector3(CaveZone.x, transform.position.y, CaveZone.z); return; }
            doorAt = mouth.transform.position;
            Vector3 dir = outside != null ? outside.position - doorAt : -mouth.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;                 // the cave faces south (Island.json mouth -> mouthOut)
            Vector3 c = doorAt + dir.normalized * MouthOut;
            if (NavMesh.SamplePosition(c, out NavMeshHit nh, 4f, NavMesh.AllAreas)) c = nh.position;
            Center = c;
            mouthFound = true;
        }

        void Update()
        {
            // a big jump of Rex (fast travel, respawn, a door): a breather, he does not fade in into a siege
            Vector3 pos = transform.position;
            if (hasLastPos && (pos - lastPos).sqrMagnitude > JumpDistance * JumpDistance) { Breather(BreatherAfterJump); checkPos = pos; }
            if (!hasLastPos) checkPos = pos;
            lastPos = pos;
            hasLastPos = true;
            if (DayNight.Night < EndNight && (failsTonight > 0 || SkippedTonight)) { failsTonight = 0; SkippedTonight = false; }   // a new day
            // replay: Use at the cave mouth (other Use targets near him go first: food, a pickup)
            if (!Active && ReplayAvailable && RMInput.Use && !PauseMenu.Open && FoodPickup.Nearby == null && WeaponPickup.Nearby == null && FindPrefabs())
            {
                RMInput.ConsumeUse();
                Replaying = true;
                StartCoroutine(Run());
                return;
            }
            if (Active || Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;
            Vector3 moved = pos - checkPos;                                  // the last 0.5 s
            checkPos = pos;
            ReplayAvailable = CanReplay(pos);
            if (Time.time < retryAt || Time.time < breatherUntil || SkippedTonight || Done || !FactoryCore.Stopped || DayNight.Night < StartNight) return;
            // never while he drives: the cart would roll him into the waves (and out of the fight)
            if (KingGlorpBoss.Fighting || BarDefense.Active || GameFlow.Busy || GolfCart.Driven != null || (rexHealth != null && rexHealth.IsDead)) return;
            FindMouth();
            Vector3 off = pos - Center;
            if (Mathf.Abs(off.y) > 15f) return;                              // not from the tunnels under the island
            off.y = 0f;
            if (off.sqrMagnitude > StartRadius * StartRadius) return;
            // heading for the cave: walking toward the centre (or already at it); passing by / walking away: no siege
            if (off.sqrMagnitude > NearCenter * NearCenter)
            {
                moved.y = 0f;
                if (moved.magnitude < TowardSpeed * 0.5f || Vector3.Dot(moved, -off) <= 0f) return;
            }
            if (!FindPrefabs()) return;
            StartCoroutine(Run());
        }

        /// <summary>A replay can start: done before, night, Rex alive on foot at the cave mouth, no other event / boss.</summary>
        bool CanReplay(Vector3 rexAt)
        {
            if (!Done || Time.time < retryAt || DayNight.Night < StartNight || GolfCart.Driven != null) return false;
            if (KingGlorpBoss.Fighting || BarDefense.Active || GameFlow.Busy || (rexHealth != null && rexHealth.IsDead)) return false;
            FindMouth();
            Vector3 fromCenter = rexAt - Center;
            if (Mathf.Abs(fromCenter.y) > 15f) return false;               // not from the tunnels
            fromCenter.y = 0f;
            return fromCenter.sqrMagnitude < StartRadius * StartRadius;
        }

        /// <summary>Blob prefabs copied from the island's spawners (no own prefab references: no builder change).</summary>
        bool FindPrefabs()
        {
            if (prefabs.Count > 0) return true;
            foreach (var sp in FindObjectsByType<BlobSpawner>(FindObjectsSortMode.None))
            {
                if (sp.prefabs == null) continue;
                foreach (var b in sp.prefabs)
                    if (b != null && !b.isHalf && b.kind != Blob.Kind.KingGlorp && !prefabs.Contains(b)) prefabs.Add(b);
            }
            return prefabs.Count > 0;
        }

        Blob PrefabFor(int wave)
        {
            // wave 1: tourists + lifeguards; 2: + bouncers
            float r = Random.value;
            Blob.Kind k = wave == 0 ? (r < 0.6f ? Blob.Kind.Tourist : Blob.Kind.Lifeguard)
                        : (r < 0.35f ? Blob.Kind.Tourist : r < 0.7f ? Blob.Kind.Lifeguard : Blob.Kind.Bouncer);
            foreach (var b in prefabs) if (b.kind == k) return b;
            return prefabs[Random.Range(0, prefabs.Count)];
        }

        bool Aborted()
        {
            if (rexHealth != null && rexHealth.IsDead) return true;
            if (DayNight.Night < EndNight) return true;                     // dawn: they crawl back in
            Vector3 flat = transform.position - Center;
            if (Mathf.Abs(flat.y) > AbortHeight) return true;              // in the tunnels under the island (or way up)
            flat.y = 0f;
            return flat.sqrMagnitude > AbortRadius * AbortRadius;
        }

        void Abort()
        {
            Active = false;
            Wave = 0;
            Left = 0;
            // Rex died: the wave goes (bouncer halves too; a popping bouncer goes before it splits), so he does not
            // come back into the same siege. Else the blobs stay: they just stop being a wave.
            if (rexHealth != null && rexHealth.IsDead)
                foreach (var b in mine)
                {
                    if (b == null) continue;
                    SlimeFX.Splat(b.transform.position + Vector3.up * 0.3f, Vector3.up, b.BaseColor(), 8);
                    Destroy(b.gameObject);
                }
            bool died = rexHealth != null && rexHealth.IsDead;
            mine.Clear();
            DropLight();
            bool replay = Replaying;
            Replaying = false;
            retryAt = Time.time + (replay ? 5f : RetryAfter);
            if (replay)
            {
                // a replay he chose: no give-up count (the boss route is open anyway), only a breather after a death
                if (died) Breather(BreatherAfterJump);
                RexTestHUD.Notice("Replay over: the cave siege is off.", 4f);
                return;
            }
            if (died)
            {
                Breather(BreatherAfterJump);                                   // the respawn: not straight back into it
                if (++failsTonight >= MaxFails)
                {
                    SkippedTonight = true;                                     // the boss route is not a death loop
                    RexTestHUD.Notice("The blobs went back in. The cave is open — for now.", 5f);
                    return;
                }
            }
            RexTestHUD.Notice("The cave siege is off. Come back at night to finish it.", 4f);
        }

        IEnumerator Run()
        {
            Active = true;
            ReplayAvailable = false;
            mine.Clear();
            startedAt = Time.time;
            popped = 0;
            RexVoice.Subtitle?.Invoke("REX: Something's glowing in that cave. Lots of somethings.");
            for (int w = 0; w < Waves; w++)
            {
                Wave = w + 1;
                RexTestHUD.Notice($"CAVE SIEGE! Wave {Wave}/{Waves}", 3f);
                int toSpawn = WaveSize[w];
                float nextSpawn = Time.time + 1.5f, nextAlert = 0f, lastProgress = Time.time;
                Left = toSpawn;
                int lastLeft = Left;
                while (toSpawn > 0 || mine.Count > 0)
                {
                    if (Aborted()) { Abort(); yield break; }
                    // a popped blob stays listed until it is gone (a bouncer's halves come out of it)
                    int living = 0;
                    for (int i = mine.Count - 1; i >= 0; i--)
                    {
                        if (mine[i] == null) { mine.RemoveAt(i); popped++; }    // gone = popped (halves count too)
                        else if (!mine[i].IsDead) living++;
                    }
                    FollowLight();
                    if (toSpawn > 0 && Time.time >= nextSpawn && mine.Count < MaxAlive && Blob.AliveCount() < BlobSpawner.GlobalCap)
                    {
                        nextSpawn = Time.time + SpawnEvery;
                        if (SpawnOne(w)) toSpawn--;
                    }
                    if (Time.time >= nextAlert)
                    {
                        nextAlert = Time.time + 2f;
                        foreach (var b in mine) if (b != null) b.Alert(5f);
                    }
                    Left = toSpawn + living;
                    if (Left != lastLeft) { lastLeft = Left; lastProgress = Time.time; }
                    else if (living > 0 && Time.time - lastProgress > StuckAfter) { lastProgress = Time.time; Unstick(); }
                    yield return null;
                }
                DropLight();
                if (w == Waves - 1) break;
                RexTestHUD.Notice($"Wave {Wave}/{Waves} cleared. The cave is still glowing!", 3f);
                for (float t = 0f; t < 5f; t += Time.deltaTime)
                {
                    if (Aborted()) { Abort(); yield break; }
                    yield return null;
                }
            }
            Win();
        }

        /// <summary>Out in front of the cave mouth (2-6 m around Center, never on the door); Rex stands right there: a
        /// wider ring (10-16 m).</summary>
        bool SpawnOne(int wave)
        {
            for (int tries = 0; tries < 10; tries++)
            {
                Vector2 o = Random.insideUnitCircle.normalized * (tries < 5 ? Random.Range(2f, 6f) : Random.Range(10f, 16f));
                Vector3 p = Center + new Vector3(o.x, 0f, o.y);
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;
                if (WaterVolume.Depth(hit.position + Vector3.up * 0.1f) > 0.2f) continue;
                if ((hit.position - transform.position).sqrMagnitude < 8f * 8f) continue;      // never next to Rex
                if (mouthFound && (hit.position - doorAt).sqrMagnitude < DoorClear * DoorClear) continue;   // not in the doorway
                var b = Instantiate(PrefabFor(wave), hit.position, Quaternion.identity);
                if (wave == 1 && b.kind == Blob.Kind.Lifeguard && Random.value < 0.35f) b.SetVariant(Blob.Variant.GlowSpitter);   // the cave's own spitters
                b.Alert(6f);
                // the wave's glow: one light in the first blob's colour (readable in the dark cave mouth)
                if (waveLight == null && !LowTier())                          // Low tier: no extra light (DayNight's budget)
                {
                    waveLight = new GameObject("SiegeGlow").AddComponent<Light>();
                    waveLight.type = LightType.Point;
                    waveLight.range = 9f;
                    waveLight.intensity = 2.5f;
                    waveLight.color = b.BaseColor();
                    waveLight.shadows = LightShadows.None;
                    waveLight.transform.position = hit.position + Vector3.up * 1.5f;
                }
                SlimeFX.Splat(hit.position + Vector3.up * 0.3f, Vector3.up, b.BaseColor(), 10);
                mine.Add(b);
                return true;
            }
            return false;
        }

        /// <summary>The wave's light drifts to the middle of its living blobs (one light for the whole wave).</summary>
        void FollowLight()
        {
            if (waveLight == null) return;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var b in mine) if (b != null && !b.IsDead) { sum += b.transform.position; n++; }
            if (n == 0) return;
            Vector3 to = sum / n + Vector3.up * 1.5f;
            waveLight.transform.position = Vector3.Lerp(waveLight.transform.position, to, Mathf.Clamp01(Time.deltaTime * 3f));
        }

        bool LowTier() => quality != null && quality.Tier == QualityTier.Low;

        void DropLight()
        {
            if (waveLight != null) Destroy(waveLight.gameObject);
            waveLight = null;
        }

        /// <summary>Wave blobs far from Rex: warped to a dry NavMesh point near the cave mouth (not next to Rex); none
        /// found: it counts as beaten (removed quietly).</summary>
        void Unstick()
        {
            Vector3 rexAt = transform.position;
            foreach (var b in mine.ToArray())
            {
                if (b == null || b.IsDead || (b.transform.position - rexAt).sqrMagnitude < StuckRexDistance * StuckRexDistance) continue;
                var agent = b.GetComponent<NavMeshAgent>();
                if (agent == null || !agent.enabled) continue;
                bool moved = false;
                for (int tries = 0; tries < 8 && !moved; tries++)
                {
                    Vector2 o = Random.insideUnitCircle.normalized * Random.Range(3f, 8f);
                    if (!NavMesh.SamplePosition(Center + new Vector3(o.x, 0f, o.y), out NavMeshHit nh, 3f, NavMesh.AllAreas)) continue;
                    if (WaterVolume.Depth(nh.position + Vector3.up * 0.1f) > 0.2f || (nh.position - rexAt).sqrMagnitude < 6f * 6f) continue;
                    if (mouthFound && (nh.position - doorAt).sqrMagnitude < DoorClear * DoorClear) continue;
                    agent.Warp(nh.position);
                    SlimeFX.Splat(nh.position + Vector3.up * 0.3f, Vector3.up, b.BaseColor(), 10);
                    b.Alert(6f);
                    moved = true;
                }
                if (!moved) Destroy(b.gameObject);
            }
        }

        void Win()
        {
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
            LastSummary = $"{(replay ? "REPLAY: " : "")}Cave held in {Clock(secs)} — " +
                          (record ? "NEW BEST!" : $"best {Clock(best > 0 ? Mathf.Min(best, secs) : secs)}") + (noHit ? " — NO HIT!" : "");
            if (replay)
            {
                // a replay: the score only (no reward, no BIG GOAL banner)
                RexTestHUD.Notice(LastSummary, 5f);
                retryAt = Time.time + 5f;
                return;
            }
            // the reward: coconuts (+ the coconut itself when he never found one) + half a load for every gun he owns
            if (weapons != null)
            {
                // never found a coconut: he gets the throw too, so the reward is usable (+ its start ammo)
                if (!weapons.Has(WeaponId.Coconut)) weapons.Give(WeaponId.Coconut, false);   // added, not equipped mid-fight
                weapons.AddAmmo(WeaponId.Coconut, 5);
                foreach (var id in weapons.OwnedInOrder())
                {
                    var d = WeaponDef.Get(id);
                    if (d.isGun && id != WeaponId.Coconut) weapons.AddAmmo(id, Mathf.Max(1, d.maxAmmo / 2));
                }
            }
            // the reward line is in GameFlow's BIG GOAL banner
            RexVoice.Subtitle?.Invoke("REX: And stay in there. Lights out.");
        }
    }
}
