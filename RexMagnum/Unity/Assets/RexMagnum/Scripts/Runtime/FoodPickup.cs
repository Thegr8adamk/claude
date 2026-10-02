using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Food and drinks heal Rex (brief section 8: "Health bar: food/drinks heal"). Press Use (F / X) near it.
    /// The model is made in code (no asset needed): a cocktail with a paper umbrella, a pizza slice or a fruit
    /// plate. Bar / buffet items come back after a while; rescue rewards do not.
    /// </summary>
    public class FoodPickup : MonoBehaviour
    {
        public enum Kind { Cocktail, Pizza, Fruit }
        public Kind kind = Kind.Cocktail;
        public float radius = 1.4f;
        public float respawnSeconds = 120f;     // 0 = one time only
        public static FoodPickup Nearby { get; private set; }
        public float LastDist { get; private set; } = float.MaxValue;

        static readonly string[] Names = { "cocktail", "pizza slice", "fruit plate" };
        static readonly float[] HealAmount = { 30f, 25f, 20f };
        GameObject model;
        Vector3 basePos;
        float backAt = -1f;
        Transform rex;

        public string Label => $"{RMInput.Btn("Use")}: {(kind == Kind.Cocktail ? "drink" : "eat")} the {Names[(int)kind]} (+{HealAmount[(int)kind]:0})";

        /// <summary>Spawn a food pickup (rescue reward, bars, buffet).</summary>
        public static FoodPickup Spawn(Kind kind, Vector3 at, float respawn, Transform parent = null)
        {
            var go = new GameObject("Food_" + kind);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = at;
            var f = go.AddComponent<FoodPickup>();
            f.kind = kind;
            f.respawnSeconds = respawn;
            return f;
        }

        void Start()
        {
            basePos = transform.position;
            model = Build(kind, transform);
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) rex = p.transform;
        }

        void Update()
        {
            if (backAt > 0f)
            {
                if (Time.time < backAt) return;
                backAt = -1f;
                model.SetActive(true);
            }
            model.transform.position = basePos + Vector3.up * (0.05f + Mathf.Sin(Time.time * 2f) * 0.02f);
            model.transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
            if (rex == null) return;
            float dist = Vector3.Distance(rex.position + Vector3.up, basePos);
            bool near = dist < radius;
            if (near && (Nearby == null || Nearby == this || dist < Nearby.LastDist)) Nearby = this;
            else if (!near && Nearby == this) Nearby = null;
            LastDist = dist;
            // only the closest food reacts, and a weapon / ammo pickup in reach goes first
            if (Nearby != this || WeaponPickup.Nearby != null || !RMInput.Use) return;
            var h = rex.GetComponent<Health>();
            if (h == null || h.IsDead || h.Fraction >= 1f) return;         // full: keep it for later
            RMInput.ConsumeUse();
            h.Heal(HealAmount[(int)kind]);
            Progress.Add(kind == Kind.Cocktail ? "drinks" : "snacks");
            Sfx.Play(kind == Kind.Cocktail ? "drink" : "eat", basePos);
            if (Nearby == this) Nearby = null;
            if (respawnSeconds > 0f) { model.SetActive(false); backAt = Time.time + respawnSeconds; }
            else Destroy(gameObject);
        }

        // ---------------------------------------------------------------- models (primitives, URP Lit)

        static readonly System.Collections.Generic.Dictionary<(Color, float, float), Material> cache =
            new System.Collections.Generic.Dictionary<(Color, float, float), Material>();

        static Material Mat(Color c, float smooth = 0.4f, float alpha = 1f)
        {
            if (cache.TryGetValue((c, smooth, alpha), out var cached) && cached != null) return cached;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (alpha < 1f)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = 3000;
            }
            m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
            m.SetFloat("_Smoothness", smooth);
            cache[(c, smooth, alpha)] = m;
            return m;
        }

        static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(t);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localEulerAngles = euler;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static GameObject Build(Kind kind, Transform parent)
        {
            var root = new GameObject("Model").transform;
            root.SetParent(parent, false);
            switch (kind)
            {
                case Kind.Cocktail:
                    Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.005f, 0), new Vector3(0.07f, 0.005f, 0.07f), Mat(Color.white, 0.9f, 0.4f));   // foot
                    Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.05f, 0), new Vector3(0.012f, 0.045f, 0.012f), Mat(Color.white, 0.9f, 0.4f)); // stem
                    Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.14f, 0), new Vector3(0.09f, 0.05f, 0.09f), Mat(Color.white, 0.95f, 0.3f));  // bowl
                    Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.13f, 0), new Vector3(0.08f, 0.035f, 0.08f), Mat(new Color(1f, 0.45f, 0.2f), 0.8f, 0.85f)); // drink
                    Prim(PrimitiveType.Cylinder, root, new Vector3(0.02f, 0.22f, 0), new Vector3(0.004f, 0.06f, 0.004f), Mat(new Color(0.9f, 0.8f, 0.6f)), new Vector3(0, 0, -20)); // stick
                    Prim(PrimitiveType.Sphere, root, new Vector3(0.045f, 0.28f, 0), new Vector3(0.11f, 0.025f, 0.11f), Mat(new Color(1f, 0.3f, 0.55f), 0.2f), new Vector3(0, 0, -20)); // umbrella
                    Prim(PrimitiveType.Sphere, root, new Vector3(-0.055f, 0.17f, 0), new Vector3(0.035f, 0.035f, 0.035f), Mat(new Color(1f, 0.8f, 0.1f), 0.6f)); // fruit
                    break;
                case Kind.Pizza:
                    Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.005f, 0), new Vector3(0.24f, 0.005f, 0.24f), Mat(Color.white, 0.6f)); // plate
                    Prim(PrimitiveType.Cube, root, new Vector3(0, 0.02f, 0), new Vector3(0.12f, 0.015f, 0.17f), Mat(new Color(0.95f, 0.7f, 0.25f), 0.35f), new Vector3(0, 30, 0)); // slice
                    Prim(PrimitiveType.Cube, root, new Vector3(-0.03f, 0.022f, 0.06f), new Vector3(0.13f, 0.02f, 0.03f), Mat(new Color(0.75f, 0.45f, 0.15f), 0.2f), new Vector3(0, 30, 0)); // crust
                    for (int i = 0; i < 4; i++)
                        Prim(PrimitiveType.Cylinder, root, new Vector3(0.02f - i * 0.012f, 0.03f, -0.04f + i * 0.025f), new Vector3(0.03f, 0.003f, 0.03f), Mat(new Color(0.7f, 0.1f, 0.08f), 0.5f)); // pepperoni
                    break;
                default:
                    Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.005f, 0), new Vector3(0.26f, 0.006f, 0.26f), Mat(Color.white, 0.6f)); // plate
                    Color[] c = { new Color(0.95f, 0.75f, 0.1f), new Color(0.9f, 0.2f, 0.25f), new Color(0.4f, 0.75f, 0.25f), new Color(1f, 0.5f, 0.1f), new Color(0.55f, 0.2f, 0.5f) };
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * Mathf.PI * 2f / 7f;
                        Prim(PrimitiveType.Sphere, root, new Vector3(Mathf.Cos(a) * 0.07f, 0.03f, Mathf.Sin(a) * 0.07f), Vector3.one * 0.05f, Mat(c[i % c.Length], 0.55f));
                    }
                    Prim(PrimitiveType.Sphere, root, new Vector3(0, 0.05f, 0), new Vector3(0.09f, 0.06f, 0.06f), Mat(new Color(0.95f, 0.85f, 0.3f), 0.5f)); // pineapple slice
                    break;
            }
            return root.gameObject;
        }
    }
}
