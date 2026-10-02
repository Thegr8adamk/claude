using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>Keeps a few blobs alive around its area; they rise out of water when there is some near (brief section 4).
    /// Fair play: weighted types, never within 8 m of Rex, a 30 s break after the area is cleared, night cap 1.8x.</summary>
    public class BlobSpawner : MonoBehaviour
    {
        public Blob[] prefabs;
        public int maxAlive = 4;
        /// <summary>Set by FactoryCore when the blob factory is stopped (brief: fewer blobs after). DayNight multiplies by it.</summary>
        [System.NonSerialized] public float factoryScale = 1f;
        [System.NonSerialized] public int baseMax = -1;
        public float radius = 14f;
        public float interval = 6f;
        [Range(0f, 1f)] public float disguiseChance = 0.15f;   // tourist blobs in a dress + sun hat
        [Header("Fairness")]
        public float minRexDistance = 8f;          // never pops up right next to Rex
        public float clearedBreak = 30f;           // area cleared: a breather before it fills again
        public float nightMaxScale = 1.8f;         // at most this many times baseMax at full night
        // type weights (Tourist, Lifeguard, Bouncer): mostly easy tourists by day, more of the hard ones at night
        static readonly float[] DayWeights = { 70f, 20f, 10f }, NightWeights = { 50f, 30f, 20f };
        float next;
        int lastNear;
        Transform rex;

        /// <summary>A prefab picked by the type weights (day / night blend). KingGlorp or unknown kinds weigh 0.</summary>
        Blob PickPrefab()
        {
            float night = DayNight.Night, sum = 0f;
            foreach (var p in prefabs) sum += Weight(p, night);
            if (sum <= 0f) return prefabs[Random.Range(0, prefabs.Length)];
            float r = Random.value * sum;
            foreach (var p in prefabs)
            {
                r -= Weight(p, night);
                if (r <= 0f) return p;
            }
            return prefabs[prefabs.Length - 1];
        }

        /// <summary>Blobs alive on the whole island before no spawner adds more.</summary>
        public const int GlobalCap = 24;
        /// <summary>Spawners farther than this from Rex do nothing: the cull distance minus how far out this spawner
        /// places blobs (water up to 1.5x radius, plus the shore search), so it never spawns blobs Blob.CullCheck removes.</summary>
        float SleepDistance => Mathf.Max(30f, Blob.CullDistance - radius * 1.5f - 14f);

        Blob TouristPrefab()
        {
            foreach (var p in prefabs) if (p != null && p.kind == Blob.Kind.Tourist && !p.isHalf) return p;
            return null;
        }

        static float Weight(Blob p, float night)
        {
            if (p == null || p.isHalf || (int)p.kind > 2) return 0f;
            int k = (int)p.kind;
            return Mathf.Lerp(DayWeights[k], NightWeights[k], night);
        }

        /// <summary>Nearest walkable point that is NOT under water (the pool floor / seabed are on the NavMesh too):
        /// rings outward from the water point.</summary>
        public static bool DryShore(Vector3 water, out NavMeshHit hit)
        {
            for (float r = 1f; r <= 14f; r += 1.5f)
                for (int k = 0; k < 10; k++)
                {
                    float a = k * Mathf.PI * 2f / 10f;
                    Vector3 q = water + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (NavMesh.SamplePosition(q, out hit, 2f, NavMesh.AllAreas) && WaterVolume.Depth(hit.position + Vector3.up * 0.1f) < 0.2f)
                        return true;
                }
            hit = default;
            return false;
        }

        /// <summary>The new blob rises out of the water with a splash, then slides to the shore point.</summary>
        System.Collections.IEnumerator Rise(Blob b, Vector3 water)
        {
            var agent = b.GetComponent<NavMeshAgent>();
            Vector3 shore = b.transform.position;
            if (agent) agent.enabled = false;
            Vector3 under = new Vector3(water.x, water.y - 1.6f, water.z);
            SlimeFX.Splat(water, Vector3.up, new Color(0.75f, 0.9f, 1f), 14);
            for (float t = 0f; t < 1.6f && b != null; t += Time.deltaTime)
            {
                float k = t / 1.6f;
                b.transform.position = k < 0.5f ? Vector3.Lerp(under, water, k * 2f)
                                                : Vector3.Lerp(water, shore, (k - 0.5f) * 2f) + Vector3.up * Mathf.Sin((k - 0.5f) * 2f * Mathf.PI) * 0.6f;
                yield return null;
            }
            if (b == null || b.IsDead) yield break;
            b.transform.position = shore;
            if (agent) { agent.enabled = true; agent.Warp(shore); }
        }

        void Update()
        {
            if (prefabs == null || prefabs.Length == 0 || Time.time < next) return;
            next = Time.time + interval;
            if (rex == null) { var pl = GameObject.FindGameObjectWithTag("Player"); if (pl != null) rex = pl.transform; }
            // Rex is far away: this area sleeps (no spawn, no cleared-break bookkeeping, no blobs for the cull to remove)
            float sleep = SleepDistance;
            if (rex != null && (rex.position - transform.position).sqrMagnitude > sleep * sleep) return;
            // "Defend the bar" runs its own waves: the spawners around the hotel hold off (no pile-up on top of them)
            if (BarDefense.Active && (transform.position - BarDefense.Center).sqrMagnitude < BarDefense.HoldRadius * BarDefense.HoldRadius) return;
            // the cave siege too (its waves come out of the cave mouth)
            if (NightSiege.Active && (transform.position - NightSiege.Center).sqrMagnitude < NightSiege.HoldRadius * NightSiege.HoldRadius) return;
            int near = 0;
            float r2 = (radius * 3f) * (radius * 3f);
            foreach (var b in Blob.All)
                if (b && !b.IsDead && (b.transform.position - transform.position).sqrMagnitude < r2) near++;
            // Rex cleared the area: a break before new blobs come
            if (near == 0 && lastNear > 0) { lastNear = 0; next = Time.time + clearedBreak; return; }
            lastNear = near;
            // DayNight scales maxAlive up at night; cap it here so night stays hard but fair
            int cap = baseMax >= 0 ? Mathf.Max(1, Mathf.RoundToInt(baseMax * nightMaxScale * factoryScale)) : maxAlive;
            // first fight (until 4 kills): tourists only, and at most 4 at the pool (the builder's spawner on "Game")
            bool firstFight = Progress.Value("kills") < 4;
            if (firstFight && gameObject.name == "Game") cap = Mathf.Min(cap, 4);
            if (near >= Mathf.Min(maxAlive, cap)) return;
            // global cap: all zones together (popping blobs count until they are gone)
            if (Blob.AliveCount() >= GlobalCap) return;
            Vector2 c = Random.insideUnitCircle.normalized * radius;
            Vector3 p = transform.position + new Vector3(c.x, 0, c.y);
            // brief section 4: blobs come out of the water (pool, sea, green river) when there is water near
            bool fromWater = WaterVolume.RandomSurfacePoint(transform.position, radius * 1.5f, out Vector3 water);
            if (fromWater) p = water;
            NavMeshHit hit = default;
            bool found = fromWater ? DryShore(water, out hit) : NavMesh.SamplePosition(p, out hit, 4f, NavMesh.AllAreas);
            // too close to Rex (the shore point or the rise from the water): try again soon, somewhere else
            if (found && rex != null && ((hit.position - rex.position).sqrMagnitude < minRexDistance * minRexDistance ||
                                         (fromWater && (water - rex.position).sqrMagnitude < minRexDistance * minRexDistance)))
            {
                next = Time.time + 1f;
                return;
            }
            if (found)
            {
                Blob pick = firstFight ? TouristPrefab() : null;
                var b = Instantiate(pick != null ? pick : PickPrefab(), hit.position, Quaternion.identity);
                if (b.kind == Blob.Kind.Tourist && !b.isHalf && Random.value < disguiseChance) b.gameObject.AddComponent<BlobDisguise>();
                if (fromWater) StartCoroutine(Rise(b, water));
            }
        }
    }
}
