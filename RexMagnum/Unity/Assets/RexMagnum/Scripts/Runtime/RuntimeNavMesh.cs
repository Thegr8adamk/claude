using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>Builds the NavMesh at start from the level colliders (no extra package). Blobs walk on it.
    /// The build runs in the background (the title does not freeze); agents check isOnNavMesh meanwhile and are
    /// warped onto the mesh when it is done.</summary>
    [DefaultExecutionOrder(-200)]
    public class RuntimeNavMesh : MonoBehaviour
    {
        public Vector3 center = Vector3.zero;
        public Vector3 size = new Vector3(80f, 30f, 80f);
        NavMeshDataInstance instance;
        AsyncOperation build;
        /// <summary>True once this NavMesh is built.</summary>
        public bool Ready { get; private set; }

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
            var data = new NavMeshData(settings.agentTypeID);
            instance = NavMesh.AddNavMeshData(data);
            build = NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, bounds);
        }

        void Update()
        {
            if (Ready || build == null || !build.isDone) return;
            Ready = true;
            enabled = false;
            // agents that started before the mesh existed are not on it: put them on the nearest point
            var bounds = new Bounds(center, size);
            foreach (var a in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
                if (a.enabled && a.gameObject.activeInHierarchy && !a.isOnNavMesh && bounds.Contains(a.transform.position) &&
                    NavMesh.SamplePosition(a.transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    a.Warp(hit.position);
        }

        void OnDestroy()
        {
            if (instance.valid) instance.Remove();
        }
    }
}
