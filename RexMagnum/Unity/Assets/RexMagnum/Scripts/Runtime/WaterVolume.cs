using System.Collections.Generic;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Swimmable water (brief section 6: pool and sea; the green river too). Found at scene load from every
    /// renderer part (submesh) that uses the RexMagnum/Water shader. The surface is the top of that part (the ocean:
    /// the middle, its wave bounds are symmetric around sea level). Sloped water (a river part more than 2.5 m
    /// high but not ocean-sized) is skipped. Water under a roof (caves, hotel floors) does not count.
    /// </summary>
    public static class WaterVolume
    {
        struct Area { public Bounds b; public float surface; public bool slime; }
        static readonly List<Area> areas = new List<Area>();
        static readonly RaycastHit[] hits = new RaycastHit[8];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Scan()
        {
            areas.Clear();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var mats = r.sharedMaterials;
                var mesh = r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.shader == null || m.shader.name != "RexMagnum/Water") continue;
                    // the water part only: pool water is joined with slides, umbrellas and the stage in one mesh,
                    // whose whole bounds are far taller than the water
                    var b = mesh != null && i < mesh.subMeshCount ? WorldBounds(mesh.GetSubMesh(i).bounds, r.localToWorldMatrix) : r.bounds;
                    bool ocean = b.size.x > 1000f || b.size.z > 1000f;
                    if (!ocean && b.size.y > 2.5f) continue;            // sloped river / falls: not swimmable as a box
                    // ocean: waves move symmetric around sea level; a pool or plunge pool: the top of the water
                    areas.Add(new Area { b = b, surface = ocean ? b.center.y : b.max.y, slime = IsSlime(m) });
                }
            }
        }

        static Bounds WorldBounds(Bounds local, Matrix4x4 m)
        {
            var w = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int k = 0; k < 8; k++)
                w.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                    new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1))));
            return w;
        }

        static bool IsSlime(Material m)
        {
            if (m.name.ToLowerInvariant().Contains("slime")) return true;
            if (!m.HasProperty("_ShallowColor")) return false;
            var c = m.GetColor("_ShallowColor");
            return c.g > c.r * 1.3f && c.g > c.b * 1.3f;          // green water = the slime river / plunge pool
        }

        /// <summary>Water surface above point p (open sky above the water). False if p is not in water.</summary>
        public static bool Surface(Vector3 p, out float surfaceY, out bool slime)
        {
            surfaceY = float.MinValue;
            slime = false;
            foreach (var a in areas)
            {
                var b = a.b;
                if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
                if (p.y > a.surface || p.y < a.surface - 40f || a.surface <= surfaceY) continue;
                surfaceY = a.surface;
                slime = a.slime;
            }
            if (surfaceY == float.MinValue) return false;
            return !Roofed(p, surfaceY);
        }

        /// <summary>Something solid above p (up to 3 m over the surface): a cave ceiling or a hotel floor.
        /// Characters, blobs, pickups and the golf cart (anything with a Rigidbody, Health or CharacterController)
        /// do not count.</summary>
        static bool Roofed(Vector3 p, float surfaceY)
        {
            Vector3 from = p + Vector3.up * 0.2f;
            int n = Physics.RaycastNonAlloc(from, Vector3.up, hits, surfaceY - from.y + 3f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c.attachedRigidbody != null || c.GetComponentInParent<Health>() != null ||
                    c.GetComponentInParent<CharacterController>() != null) continue;
                return true;
            }
            return false;
        }

        /// <summary>A random point on a water surface within radius of c (for blobs that rise from the water).</summary>
        public static bool RandomSurfacePoint(Vector3 c, float radius, out Vector3 p)
        {
            p = c;
            for (int i = 0; i < 10; i++)
            {
                Vector2 o = Random.insideUnitCircle * radius;
                var q = new Vector3(c.x + o.x, c.y, c.z + o.y);
                foreach (var a in areas)
                {
                    var b = a.b;
                    if (q.x < b.min.x || q.x > b.max.x || q.z < b.min.z || q.z > b.max.z) continue;
                    if (Mathf.Abs(a.surface - c.y) > 15f) continue;
                    var t = new Vector3(q.x, a.surface, q.z);
                    // real water there: the ground under the point is below the surface, and no roof
                    if (!Physics.Raycast(t + Vector3.up * 0.1f, Vector3.down, out RaycastHit g, 50f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (a.surface - g.point.y < 0.6f || Roofed(g.point, a.surface)) continue;
                    p = t;
                    return true;
                }
            }
            return false;
        }

        /// <summary>How deep p is under the water surface (0 when not in water).</summary>
        public static float Depth(Vector3 p) => Surface(p, out float s, out _) ? s - p.y : 0f;
    }
}
