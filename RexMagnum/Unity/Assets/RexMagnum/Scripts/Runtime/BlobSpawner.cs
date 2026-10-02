using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>Test spawner: keeps a few blobs alive around the test area (brief: they spawn from water later).</summary>
    public class BlobSpawner : MonoBehaviour
    {
        public Blob[] prefabs;
        public int maxAlive = 4;
        public float radius = 14f;
        public float interval = 6f;
        [Range(0f, 1f)] public float disguiseChance = 0.15f;   // tourist blobs in a dress + sun hat
        float next;

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
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                var b = Instantiate(prefabs[Random.Range(0, prefabs.Length)], hit.position, Quaternion.identity);
                if (b.kind == Blob.Kind.Tourist && !b.isHalf && Random.value < disguiseChance) b.gameObject.AddComponent<BlobDisguise>();
            }
        }
    }
}
