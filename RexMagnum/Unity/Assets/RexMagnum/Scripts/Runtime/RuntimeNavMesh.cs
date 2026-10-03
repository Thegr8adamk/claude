using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>Builds the NavMesh at start from the level colliders (no extra package). Blobs walk on it.
    /// The build runs in the background (the title does not freeze); agents check isOnNavMesh meanwhile and are
    /// warped onto the mesh when it is done. Web player (no threads): built one 32 m tile at a time instead.</summary>
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
            Total++;
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
            if (Web)
            {
                // the web player has no worker threads: the "background" build ran on the main thread in one go
                // (whole island areas: the page froze for minutes). Coarser voxels (about 2x cheaper) and 32 m tiles
                // built one at a time (Update), the ones nearest Rex first
                settings.overrideVoxelSize = true;
                settings.voxelSize = 0.25f;
                settings.overrideTileSize = true;
                settings.tileSize = 128;
            }
            var data = new NavMeshData(settings.agentTypeID);
            instance = NavMesh.AddNavMeshData(data);
            if (Web) { webData = data; webSettings = settings; webSources = sources; webChunks = Chunks(bounds, settings.voxelSize * settings.tileSize); }
            else build = NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, bounds);
            BlockStuckDoors(bounds);
        }

        // ---- web player: the mesh is built tile by tile (one small synchronous build every few frames)
        static bool Web => Application.platform == RuntimePlatform.WebGLPlayer;
        /// <summary>NavMesh areas in the scene and how many are built (the web start log line).</summary>
        public static int Total, Built;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Total = Built = 0; webBusyFrame = -1; }
        static int webBusyFrame = -1;          // one tile per frame for all areas together
        NavMeshData webData;
        NavMeshBuildSettings webSettings;
        List<NavMeshBuildSource> webSources;
        List<Bounds> webChunks;
        float webNext;

        /// <summary>The area cut into tile-aligned cells (tiles sit on a grid from the world origin), slightly inset so
        /// a cell rebuilds only its own tile.</summary>
        static List<Bounds> Chunks(Bounds b, float tile)
        {
            var list = new List<Bounds>();
            int x0 = Mathf.FloorToInt(b.min.x / tile), x1 = Mathf.CeilToInt(b.max.x / tile);
            int z0 = Mathf.FloorToInt(b.min.z / tile), z1 = Mathf.CeilToInt(b.max.z / tile);
            for (int x = x0; x < x1; x++)
            for (int z = z0; z < z1; z++)
            {
                var cmin = new Vector3(Mathf.Max(b.min.x, x * tile + 0.05f), b.min.y, Mathf.Max(b.min.z, z * tile + 0.05f));
                var cmax = new Vector3(Mathf.Min(b.max.x, (x + 1) * tile - 0.05f), b.max.y, Mathf.Min(b.max.z, (z + 1) * tile - 0.05f));
                if (cmax.x > cmin.x && cmax.z > cmin.z) list.Add(new Bounds((cmin + cmax) * 0.5f, cmax - cmin));
            }
            return list;
        }

        /// <summary>Web: the next tile (nearest Rex). False when every tile is built.</summary>
        bool WebStep()
        {
            if (webChunks == null || webChunks.Count == 0) return false;
            if (webBusyFrame == Time.frameCount || Time.unscaledTime < webNext) return true;
            webBusyFrame = Time.frameCount;
            webNext = Time.unscaledTime + 0.05f;
            var rex = FindAnyObjectByType<RexController>();
            Vector3 p = rex != null ? rex.transform.position : center;
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < webChunks.Count; i++)
            {
                float d = webChunks[i].SqrDistance(p);
                if (d < bd) { bd = d; best = i; }
            }
            var cb = webChunks[best];
            webChunks.RemoveAt(best);
            NavMeshBuilder.UpdateNavMeshData(webData, webSettings, webSources, cb);
            return webChunks.Count > 0;
        }

        /// <summary>The stuck door has Health (it is punched open), so the rule above leaves it out of the mesh and
        /// agents would walk through it. A carving obstacle cuts it out instead, and goes off when the door opens.</summary>
        static void BlockStuckDoors(Bounds bounds)
        {
            foreach (var door in RMFind.All<StuckDoor>())
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
            if (Ready) return;
            if (webChunks != null) { if (WebStep()) return; webSources = null; }
            else if (build == null || !build.isDone) return;
            Ready = true;
            Built++;
            enabled = false;
            // agents that started before the mesh existed are not on it: put them on the nearest point
            var bounds = new Bounds(center, size);
            foreach (var a in RMFind.All<NavMeshAgent>())
                if (a.enabled && a.gameObject.activeInHierarchy && !a.isOnNavMesh && bounds.Contains(a.transform.position) &&
                    NavMesh.SamplePosition(a.transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    a.Warp(hit.position);
        }

        /// <summary>Turns a (disabled) agent on only where a NavMesh is: snapped to the nearest point within radius first.
        /// Agents start disabled (prefabs): enabling one off the mesh logs "Failed to create agent ... not close enough
        /// to the NavMesh" (the runtime mesh builds after the scene loads). False: no mesh there yet, quietly try later.</summary>
        public static bool TryEnable(NavMeshAgent a, float radius = 4f)
        {
            if (a == null || !a.gameObject.activeInHierarchy) return false;
            if (a.enabled) return a.isOnNavMesh;
            if (!NavMesh.SamplePosition(a.transform.position, out NavMeshHit hit, radius, NavMesh.AllAreas)) return false;
            a.transform.position = hit.position;
            a.enabled = true;
            if (a.isOnNavMesh) a.Warp(hit.position);
            return a.isOnNavMesh;
        }

        void OnDestroy()
        {
            if (instance.valid) instance.Remove();
            Total--;
            if (Ready) Built--;
        }
    }
}
