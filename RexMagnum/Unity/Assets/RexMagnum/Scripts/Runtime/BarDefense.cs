using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>
    /// "Defend the bar" (mid-game set piece, one time). Starts when Rex walks into the lounge bar on the hotel's
    /// ground floor with the pistol, while the blob factory still runs. Three short waves of blobs come in from the
    /// pool doors (at most MaxAlive at once); the spawners around the hotel hold off meanwhile (BlobSpawner).
    /// Reward: all owned guns full + Progress "bar_defended" (saved: never again). Rex dies or goes more than
    /// AbortRadius away: the event stops cleanly and can start again later.
    /// No builder step: it attaches itself to Rex when a scene loads (like Ambience).
    /// HUD: Active, Wave, Waves, Left, Objective.
    /// </summary>
    public class BarDefense : MonoBehaviour
    {
        public const string DoneKey = "bar_defended";
        /// <summary>The lounge bar, hotel floor 1 (island_assets.py BarCounter, hotel-local (-31.5, 5.9) + hotel (40, -300)).</summary>
        public static readonly Vector3 Center = new Vector3(9f, 3.5f, -296f);
        // the hotel's doors to the pool (Island.json hotel.poolDoors), a few metres out on the pool side
        static readonly Vector3 SpawnAt = new Vector3(40f, 3.5f, -284f);
        /// <summary>Spawners closer than this to the bar do not spawn while the event runs.</summary>
        public const float HoldRadius = 80f;
        const float StartRadius = 12f, StartTop = 6.5f, AbortRadius = 60f, RetryAfter = 30f, SpawnEvery = 1.6f;
        const int MaxAlive = 4;
        static readonly int[] WaveSize = { 4, 6, 7 };

        /// <summary>True while the waves run.</summary>
        public static bool Active { get; private set; }
        /// <summary>The wave running now (1..Waves), 0 when not active.</summary>
        public static int Wave { get; private set; }
        public static int Waves => WaveSize.Length;
        /// <summary>Blobs of this wave still to beat (alive + not yet in).</summary>
        public static int Left { get; private set; }
        /// <summary>Objective line for the HUD while active, else null.</summary>
        public static string Objective => Active ? $"DEFEND THE BAR! Wave {Wave}/{Waves}: {Left} blob{(Left == 1 ? "" : "s")} left" : null;
        /// <summary>Done before (saved).</summary>
        public static bool Done => Progress.Value(DoneKey) > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = false; Wave = 0; Left = 0; }   // editor with Domain Reload off

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
        float nextCheck, retryAt;
        readonly List<Blob> prefabs = new List<Blob>();
        readonly List<Blob> mine = new List<Blob>();

        void Start()
        {
            rexHealth = GetComponent<Health>();
            weapons = GetComponent<RexWeapons>();
        }

        void OnDisable()
        {
            // a scene reload / NEW GAME mid-event: the spawners must not stay on hold
            if (Active) { Active = false; Wave = 0; Left = 0; }
        }

        void Update()
        {
            if (Active || Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;
            if (Time.time < retryAt || Done || FactoryCore.Stopped || KingGlorpBoss.Fighting || GameFlow.Busy) return;
            if (weapons == null || !weapons.Has(WeaponId.Pistol) || (rexHealth != null && rexHealth.IsDead)) return;
            Vector3 p = transform.position;
            if (p.y > StartTop) return;                                       // the ground floor only
            Vector3 flat = p - Center; flat.y = 0f;
            if (flat.sqrMagnitude > StartRadius * StartRadius) return;
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
            RexTestHUD.Notice("The bar fell. Come back to defend it again.", 4f);
        }

        IEnumerator Run()
        {
            Active = true;
            mine.Clear();
            RexVoice.Subtitle?.Invoke("REX: Nobody touches my bar. I have a tab here.");
            for (int w = 0; w < Waves; w++)
            {
                Wave = w + 1;
                RexTestHUD.Notice($"DEFEND THE BAR! Wave {Wave}/{Waves}", 3f);
                int toSpawn = WaveSize[w];
                float nextSpawn = Time.time + 1.5f, nextAlert = 0f;
                Left = toSpawn;
                while (toSpawn > 0 || mine.Count > 0)
                {
                    if (Aborted()) { Abort(); yield break; }
                    for (int i = mine.Count - 1; i >= 0; i--) if (mine[i] == null || mine[i].IsDead) mine.RemoveAt(i);
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
                    Left = toSpawn + mine.Count;
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

        bool SpawnOne(int wave)
        {
            for (int tries = 0; tries < 6; tries++)
            {
                Vector2 o = Random.insideUnitCircle * 6f;
                Vector3 p = SpawnAt + new Vector3(o.x, 0f, o.y);
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
            Active = false;
            Wave = 0;
            Left = 0;
            Progress.Set(DoneKey, 1);
            // the reward: every gun Rex owns gets full ammo
            if (weapons != null)
                foreach (var id in weapons.OwnedInOrder())
                {
                    var d = WeaponDef.Get(id);
                    if (d.isGun) weapons.AddAmmo(id, d.maxAmmo);
                }
            Sfx.Play("bottle_clink", transform.position, 0.9f);           // cheers
            RexTestHUD.Notice("THE BAR HOLDS! Free refill: all your guns are full. (Added to your tab.)", 5f);
            RexVoice.Subtitle?.Invoke("REX: Bar's open. Put it on my tab.");
        }
    }
}
