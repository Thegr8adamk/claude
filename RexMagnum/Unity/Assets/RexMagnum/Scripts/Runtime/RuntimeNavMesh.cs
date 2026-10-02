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
            BlockStuckDoors(bounds);
        }

        /// <summary>The stuck door has Health (it is punched open), so the rule above leaves it out of the mesh and
        /// agents would walk through it. A carving obstacle cuts it out instead, and goes off when the door opens.</summary>
        static void BlockStuckDoors(Bounds bounds)
        {
            foreach (var door in FindObjectsByType<StuckDoor>(FindObjectsSortMode.None))
            {
                var dh = door.GetComponent<Health>();
                if (dh == null || dh.IsDead || !bounds.Contains(door.transform.position)) continue;
                foreach (var col in door.GetComponentsInChildren<Collider>())
                {
                    if (col.isTrigger || col.GetComponent<NavMeshObstacle>() != null) continue;   // another RuntimeNavMesh did it
                    var ob = col.gameObject.AddComponent<NavMeshObstacle>();
                    ob.shape = NavMeshObstacleShape.Box;
                    if (col is BoxCollider bc) { ob.center = bc.center; ob.size = bc.size; }
                    else
                    {
                        // other collider shapes: their world box, in the collider's local space
                        var t = col.transform;
                        Vector3 ls = t.lossyScale;
                        ob.center = t.InverseTransformPoint(col.bounds.center);
                        ob.size = new Vector3(col.bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(ls.x)),
                                              col.bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(ls.y)),
                                              col.bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(ls.z)));
                    }
                    ob.carving = true;
                    dh.Died += _ => { if (ob != null) ob.enabled = false; };   // punched open: the doorway is free
                }
            }
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
