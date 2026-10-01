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
        float next;

        void Update()
        {
            if (prefabs == null || prefabs.Length == 0 || Time.time < next) return;
            next = Time.time + interval;
            if (FindObjectsByType<Blob>(FindObjectsSortMode.None).Length >= maxAlive) return;
            Vector2 c = Random.insideUnitCircle.normalized * radius;
            Vector3 p = transform.position + new Vector3(c.x, 0, c.y);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                Instantiate(prefabs[Random.Range(0, prefabs.Length)], hit.position, Quaternion.identity);
        }
    }
}
