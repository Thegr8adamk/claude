using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>Test spawner: keeps a few blobs alive around the test area (brief: they spawn from water later).</summary>
    public class BlobSpawner : MonoBehaviour
    {
        public Blob[] prefabs;
        public int maxAlive = 4;
        /// <summary>Set by FactoryCore when the blob factory is stopped (brief: fewer blobs after). DayNight multiplies by it.</summary>
        [HideInInspector] public float factoryScale = 1f;
        [HideInInspector] public int baseMax = -1;
        public float radius = 14f;
        public float interval = 6f;
        [Range(0f, 1f)] public float disguiseChance = 0.15f;   // tourist blobs in a dress + sun hat
        float next;

        /// <summary>Nearest walkable point that is NOT under water (the pool floor / seabed are on the NavMesh too):
        /// rings outward from the water point.</summary>
        static bool DryShore(Vector3 water, out NavMeshHit hit)
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
            int near = 0;
            float r2 = (radius * 3f) * (radius * 3f);
            foreach (var b in Blob.All)
                if (b && !b.IsDead && (b.transform.position - transform.position).sqrMagnitude < r2) near++;
            if (near >= maxAlive) return;
            Vector2 c = Random.insideUnitCircle.normalized * radius;
            Vector3 p = transform.position + new Vector3(c.x, 0, c.y);
            // brief section 4: blobs come out of the water (pool, sea, green river) when there is water near
            bool fromWater = WaterVolume.RandomSurfacePoint(transform.position, radius * 1.5f, out Vector3 water);
            if (fromWater) p = water;
            NavMeshHit hit = default;
            bool found = fromWater ? DryShore(water, out hit) : NavMesh.SamplePosition(p, out hit, 4f, NavMesh.AllAreas);
            if (found)
            {
                var b = Instantiate(prefabs[Random.Range(0, prefabs.Length)], hit.position, Quaternion.identity);
                if (b.kind == Blob.Kind.Tourist && !b.isHalf && Random.value < disguiseChance) b.gameObject.AddComponent<BlobDisguise>();
                if (fromWater) StartCoroutine(Rise(b, water));
            }
        }
    }
}
