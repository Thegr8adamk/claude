using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>
    /// "Cave siege" (set piece between the factory and the boss, one time). After the blob factory is stopped, at night,
    /// when Rex comes within StartRadius of the cave mouth (ZoneDoor "Cave_DoorA"): two waves of glowing blobs come out
    /// of the cave (at most MaxAlive at once; bouncer halves count to their wave); the spawners near the cave hold off
    /// meanwhile (BlobSpawner). Reward: coconuts + ammo for every owned gun, Progress "cave_siege" (saved: never again).
    /// Rex dies, goes more than AbortRadius away or the night ends: the event stops cleanly and can start again later.
    /// No builder step: it attaches itself to Rex when a scene loads (like BarDefense).
    /// HUD: Active, Wave, Waves, Left, Objective, NearestBlob(), Target.
    /// </summary>
    public class NightSiege : MonoBehaviour
    {
        public const string DoneKey = "cave_siege";
        /// <summary>The cave zone (Island.json zones.cave): fallback when the cave door is not in the scene.</summary>
        static readonly Vector3 CaveZone = new Vector3(-160f, 0f, 230f);
        /// <summary>Spawners closer than this to the cave mouth do not spawn while the event runs.</summary>
        public const float HoldRadius = 70f;
        const float StartRadius = 25f, AbortRadius = 60f, RetryAfter = 30f, SpawnEvery = 1.8f, StartNight = 0.6f, EndNight = 0.25f;
        const float StuckAfter = 20f, StuckRexDistance = 15f;
        const int MaxAlive = 4;
        static readonly int[] WaveSize = { 5, 7 };

        /// <summary>True while the waves run.</summary>
        public static bool Active { get; private set; }
        /// <summary>The wave running now (1..Waves), 0 when not active.</summary>
        public static int Wave { get; private set; }
        public static int Waves => WaveSize.Length;
        /// <summary>Blobs of this wave still to beat (alive + not yet out).</summary>
        public static int Left { get; private set; }
        /// <summary>The cave mouth (the cave door when found, else the cave zone at Rex's height).</summary>
        public static Vector3 Center { get; private set; } = CaveZone;
        /// <summary>Objective line for the HUD while active, else null.</summary>
        public static string Objective => Active ? $"CAVE SIEGE! Wave {Wave}/{Waves}: {Left} blob{(Left == 1 ? "" : "s")} left" : null;
        /// <summary>Done before (saved).</summary>
        public static bool Done => Progress.Value(DoneKey) > 0;

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
        static void ResetStatics() { Active = false; Wave = 0; Left = 0; Center = CaveZone; }   // editor with Domain Reload off

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
            if (r != null && r.GetComponent<NightSiege>() == null) r.gameObject.AddComponent<NightSiege>();
        }

        Health rexHealth;
        RexWeapons weapons;
        float nextCheck, retryAt;
        bool mouthFound;
        readonly List<Blob> prefabs = new List<Blob>();
        readonly List<Blob> mine = new List<Blob>();

        void Start()
        {
            rexHealth = GetComponent<Health>();
            weapons = GetComponent<RexWeapons>();
        }

        void OnEnable() { inst = this; Blob.Split += OnSplit; }

        void OnDisable()
        {
            Blob.Split -= OnSplit;
            if (inst == this) inst = null;
            // a scene reload / NEW GAME mid-event: the spawners must not stay on hold
            if (Active) { Active = false; Wave = 0; Left = 0; }
        }

        /// <summary>A wave bouncer popped: its halves belong to the wave too.</summary>
        void OnSplit(Blob parent, Blob half)
        {
            if (!Active || half == null || !mine.Contains(parent)) return;
            mine.Add(half);
            half.Alert(6f);
        }

        /// <summary>The cave mouth: the cave door (ZoneDoor "Cave_DoorA") once found, else the cave zone at Rex's height.</summary>
        void FindMouth()
        {
            if (mouthFound) return;
            foreach (var d in FindObjectsByType<ZoneDoor>(FindObjectsSortMode.None))
                if (d != null && d.name == "Cave_DoorA") { Center = d.transform.position; mouthFound = true; return; }
            Center = new Vector3(CaveZone.x, transform.position.y, CaveZone.z);
        }

        void Update()
        {
            if (Active || Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;
            if (Time.time < retryAt || Done || !FactoryCore.Stopped || DayNight.Night < StartNight) return;
            if (KingGlorpBoss.Fighting || BarDefense.Active || GameFlow.Busy || (rexHealth != null && rexHealth.IsDead)) return;
            FindMouth();
            Vector3 off = transform.position - Center;
            if (Mathf.Abs(off.y) > 15f) return;                              // not from the tunnels under the island
            off.y = 0f;
            if (off.sqrMagnitude > StartRadius * StartRadius) return;
            if (!FindPrefabs()) return;
            StartCoroutine(Run());
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
            Vector3 flat = transform.position - Center; flat.y = 0f;
            return flat.sqrMagnitude > AbortRadius * AbortRadius;
        }

        void Abort()
        {
            Active = false;
            Wave = 0;
            Left = 0;
            mine.Clear();                                                     // the blobs stay: they just stop being a wave
            retryAt = Time.time + RetryAfter;
            RexTestHUD.Notice("The cave siege is off. Come back at night to finish it.", 4f);
        }

        IEnumerator Run()
        {
            Active = true;
            mine.Clear();
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
                        if (mine[i] == null) mine.RemoveAt(i);
                        else if (!mine[i].IsDead) living++;
                    }
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

        /// <summary>Out of the cave mouth (2-6 m around it); Rex stands right there: a wider ring (10-16 m).</summary>
        bool SpawnOne(int wave)
        {
            for (int tries = 0; tries < 10; tries++)
            {
                Vector2 o = Random.insideUnitCircle.normalized * (tries < 5 ? Random.Range(2f, 6f) : Random.Range(10f, 16f));
                Vector3 p = Center + new Vector3(o.x, 0f, o.y);
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;
                if (WaterVolume.Depth(hit.position + Vector3.up * 0.1f) > 0.2f) continue;
                if ((hit.position - transform.position).sqrMagnitude < 8f * 8f) continue;      // never next to Rex
                var b = Instantiate(PrefabFor(wave), hit.position, Quaternion.identity);
                b.Alert(6f);
                // glowing: a small light in its own colour (readable in the dark cave mouth; at most MaxAlive lights)
                var lt = new GameObject("SiegeGlow").AddComponent<Light>();
                lt.transform.SetParent(b.transform, false);
                lt.transform.localPosition = Vector3.up * 1.2f;
                lt.type = LightType.Point;
                lt.range = 5f;
                lt.intensity = 2f;
                lt.color = b.BaseColor();
                lt.shadows = LightShadows.None;
                SlimeFX.Splat(hit.position + Vector3.up * 0.3f, Vector3.up, b.BaseColor(), 10);
                mine.Add(b);
                return true;
            }
            return false;
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
            Progress.Set(DoneKey, 1);
            // the reward: coconuts (kept even before Rex has found one) + half a load for every gun he owns
            if (weapons != null)
            {
                weapons.AddAmmo(WeaponId.Coconut, 5);
                foreach (var id in weapons.OwnedInOrder())
                {
                    var d = WeaponDef.Get(id);
                    if (d.isGun && id != WeaponId.Coconut) weapons.AddAmmo(id, Mathf.Max(1, d.maxAmmo / 2));
                }
            }
            RexTestHUD.Notice("THE CAVE IS QUIET. Reward: +5 coconuts and ammo for all your guns.", 5f);
            RexVoice.Subtitle?.Invoke("REX: And stay in there. Lights out.");
        }
    }
}
