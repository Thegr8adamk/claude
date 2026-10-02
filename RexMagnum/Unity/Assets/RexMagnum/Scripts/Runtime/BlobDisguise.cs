using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Disguised tourist blob (brief section 4): a flowered sundress and a sun hat, made in code to fit the blob.
    /// It wanders and leaves a slime trail (that gives it away). Rex comes close or hits it: it is found out,
    /// Rex says a line, the blob gets angry and attacks. The clothes stay on.
    /// </summary>
    [RequireComponent(typeof(Blob))]
    public class BlobDisguise : MonoBehaviour
    {
        public float revealDistance = 5f;
        public float trailStep = 1.1f;
        static readonly Color[] Dress = { new Color(0.95f, 0.45f, 0.6f), new Color(0.35f, 0.7f, 0.95f), new Color(1f, 0.8f, 0.3f), new Color(0.6f, 0.9f, 0.55f) };
        static Texture2D flowers;

        Blob blob;
        Transform rex;
        Vector3 lastTrail;

        void Start()
        {
            blob = GetComponent<Blob>();
            blob.Disguised = true;
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) rex = p.transform;
            GetComponent<Health>().Damaged += (a, pt, by) => Found();
            lastTrail = transform.position;
            Dressup();
        }

        void Update()
        {
            if (blob.IsDead) return;
            if ((transform.position - lastTrail).sqrMagnitude > trailStep * trailStep)
            {
                SlimePuddle.Spawn(transform.position, blob.BaseColor(), 0.3f);
                lastTrail = transform.position;
            }
            if (blob.Disguised && rex != null && Vector3.Distance(rex.position, transform.position) < revealDistance) Found();
        }

        void Found()
        {
            if (!blob.Disguised) return;
            blob.Reveal();
            Progress.Add("disguises");
            if (rex != null) RexVoice.Say(rex.gameObject, "Disguise", true);
        }

        // ---------------------------------------------------------------- clothes (lathe meshes)

        static Material[] dressMats;
        static Material hatMat;
        static Mesh dressMesh, hatMesh;

        void Dressup()
        {
            // size from the body capsule (the jelly mesh bounds include the long tentacles)
            var cap = GetComponent<CapsuleCollider>();
            float h = cap != null ? cap.height : 1.25f, r = cap != null ? cap.radius : 0.5f;
            Vector3 local0 = cap != null ? cap.center - Vector3.up * h * 0.5f : Vector3.zero;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (dressMats == null)
            {
                dressMats = new Material[Dress.Length];
                for (int i = 0; i < Dress.Length; i++)
                {
                    var m = new Material(lit) { name = "Disguise_Dress" };
                    m.SetTexture("_BaseMap", Flowers());
                    m.SetColor("_BaseColor", Dress[i]);
                    m.SetFloat("_Smoothness", 0.25f);
                    m.SetFloat("_Cull", 0f);                                  // two-sided
                    dressMats[i] = m;
                }
                hatMat = new Material(lit) { name = "Disguise_Hat" };
                hatMat.SetColor("_BaseColor", new Color(0.93f, 0.85f, 0.62f));
                hatMat.SetFloat("_Smoothness", 0.1f);
                hatMat.SetFloat("_Cull", 0f);
                // unit-size meshes (height 1, radius 1), scaled per blob
                // sundress: from under the eyes down to just above the slime base, flaring out
                dressMesh = Lathe(new[] { 0.62f, 0.55f, 0.42f, 0.28f, 0.16f }, new[] { 0.86f, 0.98f, 1.05f, 1.14f, 1.22f }, 1f, 1f, Vector3.zero);
                // sun hat: crown + wide brim on top
                hatMesh = Lathe(new[] { 1.16f, 1.1f, 1.03f, 1.0f, 0.99f, 1.0f }, new[] { 0.0f, 0.32f, 0.38f, 0.42f, 0.95f, 1.0f }, 1f, 1f, Vector3.zero);
            }
            Part("Dress", dressMesh, dressMats[Random.Range(0, dressMats.Length)], local0, new Vector3(r, h, r));
            Part("SunHat", hatMesh, hatMat, local0 - Vector3.up * 0.02f * h, new Vector3(r * 0.75f, h, r * 0.75f));
        }

        void Part(string name, Mesh mesh, Material mat, Vector3 at, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>Surface of revolution: rings at height ys*h with radius rs*r (blob-local space).</summary>
        static Mesh Lathe(float[] ys, float[] rs, float h, float r, Vector3 origin, int segs = 28)
        {
            var v = new Vector3[ys.Length * (segs + 1)];
            var uv = new Vector2[v.Length];
            var tri = new System.Collections.Generic.List<int>();
            for (int i = 0; i < ys.Length; i++)
                for (int k = 0; k <= segs; k++)
                {
                    float a = k * Mathf.PI * 2f / segs;
                    float fold = 1f + 0.03f * Mathf.Sin(a * 7f) * (i / (float)(ys.Length - 1));
                    v[i * (segs + 1) + k] = origin + new Vector3(Mathf.Cos(a) * rs[i] * r * fold, ys[i] * h, Mathf.Sin(a) * rs[i] * r * fold);
                    uv[i * (segs + 1) + k] = new Vector2(k / (float)segs * 3f, i / (float)(ys.Length - 1));
                }
            for (int i = 0; i < ys.Length - 1; i++)
                for (int k = 0; k < segs; k++)
                {
                    int a = i * (segs + 1) + k, c = a + segs + 1;
                    tri.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                }
            var m = new Mesh { name = "Disguise", vertices = v, uv = uv, triangles = tri.ToArray() };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Small white flowers on a light ground (tinted by the dress colour).</summary>
        static Texture2D Flowers()
        {
            if (flowers != null) return flowers;
            const int n = 128;
            flowers = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Disguise_Flowers", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.85f, 0.85f, 0.85f);
            var rng = new System.Random(7);
            for (int f = 0; f < 18; f++)
            {
                int cx = rng.Next(n), cy = rng.Next(n);
                for (int p = 0; p < 5; p++)                                  // 5 petals + a yellow centre
                {
                    float a = p * Mathf.PI * 2f / 5f;
                    Dot(px, n, cx + Mathf.Cos(a) * 4f, cy + Mathf.Sin(a) * 4f, 3f, Color.white);
                }
                Dot(px, n, cx, cy, 2f, new Color(1f, 0.85f, 0.2f));
            }
            flowers.SetPixels(px);
            flowers.Apply(true);
            return flowers;
        }

        static void Dot(Color[] px, int n, float x, float y, float rad, Color c)
        {
            for (int j = -4; j <= 4; j++)
                for (int i = -4; i <= 4; i++)
                    if (i * i + j * j <= rad * rad)
                        px[((Mathf.RoundToInt(y) + j + n) % n) * n + (Mathf.RoundToInt(x) + i + n) % n] = c;
        }
    }
}
