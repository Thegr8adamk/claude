using System.Collections.Generic;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Swimmable water (brief section 6: pool and sea; the green river too). Found at scene load from every
    /// renderer that uses the RexMagnum/Water shader: its bounds give the area, the top of the bounds is the
    /// water surface. Water under a roof (caves, hotel floors below sea level) does not count.
    /// </summary>
    public static class WaterVolume
    {
        struct Area { public Bounds b; public bool slime; }
        static readonly List<Area> areas = new List<Area>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Scan()
        {
            areas.Clear();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.shader != null && m.shader.name == "RexMagnum/Water")
                    {
                        areas.Add(new Area { b = r.bounds, slime = m.name.ToLowerInvariant().Contains("slime") });
                        break;
                    }
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
                float top = b.max.y;
                if (p.y > top || p.y < top - 6f || top <= surfaceY) continue;
                surfaceY = top;
                slime = a.slime;
            }
            if (surfaceY == float.MinValue) return false;
            // a roof between Rex and the surface: not open water
            Vector3 head = new Vector3(p.x, Mathf.Min(p.y + 1.7f, surfaceY - 0.05f), p.z);
            if (head.y < surfaceY - 0.1f &&
                Physics.Raycast(head, Vector3.up, surfaceY - head.y + 0.05f, ~0, QueryTriggerInteraction.Ignore)) return false;
            return true;
        }

        /// <summary>A random point on a water surface within radius of c (for blobs that rise from the water).</summary>
        public static bool RandomSurfacePoint(Vector3 c, float radius, out Vector3 p)
        {
            p = c;
            var near = new List<Bounds>();
            foreach (var a in areas)
            {
                var b = a.b;
                if (b.max.y < c.y - 15f || b.max.y > c.y + 15f) continue;
                Vector3 q = b.ClosestPoint(new Vector3(c.x, b.max.y, c.z));
                if (new Vector2(q.x - c.x, q.z - c.z).magnitude < radius) near.Add(b);
            }
            if (near.Count == 0) return false;
            var w = near[Random.Range(0, near.Count)];
            for (int i = 0; i < 8; i++)
            {
                Vector2 o = Random.insideUnitCircle * radius;
                var t = new Vector3(Mathf.Clamp(c.x + o.x, w.min.x, w.max.x), w.max.y, Mathf.Clamp(c.z + o.y, w.min.z, w.max.z));
                if (Surface(t + Vector3.down * 0.5f, out _, out _)) { p = t; return true; }
            }
            return false;
        }

        /// <summary>How deep p is under the water surface (0 when not in water).</summary>
        public static float Depth(Vector3 p) => Surface(p, out float s, out _) ? s - p.y : 0f;
    }
}
