using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>Builds the NavMesh at start from the level colliders (no extra package). Blobs walk on it.</summary>
    [DefaultExecutionOrder(-200)]
    public class RuntimeNavMesh : MonoBehaviour
    {
        public Vector3 center = Vector3.zero;
        public Vector3 size = new Vector3(80f, 30f, 80f);
        NavMeshDataInstance instance;

        void Awake()
        {
            var sources = new List<NavMeshBuildSource>();
            var bounds = new Bounds(center, size);
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component != null &&
                                   (s.component.GetComponentInParent<Health>() != null ||
                                    s.component.GetComponentInParent<NavMeshAgent>() != null ||      // guests: no hole where they stand
                                    (s.component is Collider c && c.isTrigger)));
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.5f;
            settings.agentHeight = 1.2f;
            settings.agentClimb = 0.4f;
            settings.agentSlope = 40f;
            instance = NavMesh.AddNavMeshData(
                NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity));
        }

        void OnDestroy()
        {
            if (instance.valid) instance.Remove();
        }
    }
}
