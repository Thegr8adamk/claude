using UnityEngine;

namespace RexMagnum
{
    /// <summary>Things the lifeguard blob throws (brief section 4: floats and chairs): a red-and-white ring float and
    /// a folding pool chair, built once in code and kept hidden as templates. SlimeProjectile copies one per throw.</summary>
    public static class ThrowProps
    {
        static GameObject ring, chair;

        public static GameObject Random01() => UnityEngine.Random.value < 0.6f ? Ring() : Chair();

        static Material Mat(Color c, float smooth)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        static GameObject Ring()
        {
            if (ring != null) return ring;
            ring = new GameObject("ThrownFloat");
            ring.SetActive(false);
            Object.DontDestroyOnLoad(ring);
            // torus 0.55 m across, 4 alternating red/white segments (two sub-meshes)
            const int seg = 32, side = 10;
            float R = 0.28f, r = 0.09f;
            var v = new Vector3[(seg + 1) * (side + 1)];
            var n = new Vector3[v.Length];
            for (int i = 0; i <= seg; i++)
                for (int j = 0; j <= side; j++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f, b = j / (float)side * Mathf.PI * 2f;
                    var c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    n[i * (side + 1) + j] = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    v[i * (side + 1) + j] = c * R + n[i * (side + 1) + j] * r;
                }
            var red = new System.Collections.Generic.List<int>();
            var white = new System.Collections.Generic.List<int>();
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                {
                    int a0 = i * (side + 1) + j, a1 = a0 + side + 1;
                    var t = (i / (seg / 4)) % 2 == 0 ? red : white;
                    t.AddRange(new[] { a0, a0 + 1, a1, a1, a0 + 1, a1 + 1 });
                }
            var mesh = new Mesh { name = "FloatRing", vertices = v, normals = n, subMeshCount = 2 };
            mesh.SetTriangles(red, 0);
            mesh.SetTriangles(white, 1);
            ring.AddComponent<MeshFilter>().sharedMesh = mesh;
            ring.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mat(new Color(0.85f, 0.08f, 0.06f), 0.6f), Mat(Color.white, 0.6f) };
            return ring;
        }

        static GameObject Chair()
        {
            if (chair != null) return chair;
            chair = new GameObject("ThrownChair");
            chair.SetActive(false);
            Object.DontDestroyOnLoad(chair);
            var frame = Mat(new Color(0.85f, 0.85f, 0.88f), 0.7f);
            var cloth = Mat(new Color(0.1f, 0.45f, 0.7f), 0.2f);
            void Part(Vector3 pos, Vector3 size, Vector3 euler, Material m)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(chair.transform, false);
                p.transform.localPosition = pos;
                p.transform.localEulerAngles = euler;
                p.transform.localScale = size;
                p.GetComponent<Renderer>().sharedMaterial = m;
            }
            Part(new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.03f, 0.5f), Vector3.zero, cloth);           // seat
            Part(new Vector3(0f, 0.55f, -0.27f), new Vector3(0.5f, 0.5f, 0.03f), new Vector3(-15f, 0f, 0f), cloth);  // back
            foreach (var x in new[] { -0.24f, 0.24f })
            {
                Part(new Vector3(x, 0.15f, 0.2f), new Vector3(0.03f, 0.32f, 0.03f), new Vector3(20f, 0f, 0f), frame);
                Part(new Vector3(x, 0.15f, -0.2f), new Vector3(0.03f, 0.32f, 0.03f), new Vector3(-20f, 0f, 0f), frame);
            }
            return chair;
        }
    }
}
