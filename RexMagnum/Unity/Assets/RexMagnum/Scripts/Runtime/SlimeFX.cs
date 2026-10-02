using UnityEngine;

namespace RexMagnum
{
    /// <summary>Slime effects made in code: jelly material, splats, pop burst with decals.</summary>
    public static class SlimeFX
    {
        public static Material SlimeMaterial(Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.75f));
            m.SetFloat("_Smoothness", 0.95f);
            return m;
        }

        static readonly System.Collections.Generic.Dictionary<Color32, Material> shared =
            new System.Collections.Generic.Dictionary<Color32, Material>();

        /// <summary>One shared slime material per colour (splats, decals, shots do not fade, so they can share).
        /// Use SlimeMaterial only when the material is changed per object (puddles fade out).</summary>
        public static Material SharedSlime(Color color)
        {
            Color32 key = color;
            if (!shared.TryGetValue(key, out var m) || m == null) shared[key] = m = SlimeMaterial(color);
            return m;
        }

        public static void Splat(Vector3 at, Vector3 normal, Color color, int count = 8)
        {
            for (int i = 0; i < count; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(g.GetComponent<Collider>());
                g.transform.position = at + normal * 0.05f;
                g.transform.localScale = Vector3.one * Random.Range(0.04f, 0.1f);
                g.GetComponent<Renderer>().sharedMaterial = SharedSlime(color);
                var rb = g.AddComponent<Rigidbody>();
                rb.linearVelocity = (normal + Random.insideUnitSphere * 0.8f) * Random.Range(2f, 4f);
                Object.Destroy(g, 1.2f);
            }
        }

        /// <summary>Pop: slime burst + puddle + slime decals on walls and floor around.</summary>
        public static void Burst(Vector3 at, Color color, float size)
        {
            Splat(at, Vector3.up, color, 18);
            SlimePuddle.Spawn(new Vector3(at.x, at.y - size * 0.5f, at.z), color, size * 1.2f);
            for (int i = 0; i < 10; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (Physics.Raycast(at, dir, out RaycastHit hit, size * 4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.Destroy(q.GetComponent<Collider>());
                    q.transform.position = hit.point + hit.normal * 0.01f;
                    q.transform.rotation = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, Random.Range(0, 360f));
                    q.transform.localScale = Vector3.one * Random.Range(0.3f, 0.7f) * size;
                    q.GetComponent<Renderer>().sharedMaterial = SharedSlime(color);
                    Object.Destroy(q, 30f);
                }
            }
        }
    }
}
