using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Re-uses one guest body as many guests (owner request): each copy gets its own clothing colours (top, bottom,
    /// shoes) and a slightly different hair shade. Uses MaterialPropertyBlocks, so all copies share one material
    /// (fast, GPU instancing still works). Seed 0 = keep the original outfit.
    /// </summary>
    public class GuestOutfit : MonoBehaviour
    {
        public int seed;
        static readonly Color[] Palette =
        {
            new Color(0.85f, 0.15f, 0.12f), new Color(0.95f, 0.55f, 0.1f), new Color(0.95f, 0.85f, 0.2f),
            new Color(0.2f, 0.6f, 0.25f), new Color(0.1f, 0.55f, 0.6f), new Color(0.1f, 0.25f, 0.65f),
            new Color(0.03f, 0.05f, 0.15f), new Color(0.45f, 0.2f, 0.6f), new Color(0.95f, 0.45f, 0.6f),
            new Color(0.95f, 0.95f, 0.93f), new Color(0.08f, 0.08f, 0.08f), new Color(0.6f, 0.5f, 0.35f),
            new Color(0.55f, 0.75f, 0.9f), new Color(0.8f, 0.7f, 0.55f),
        };

        void Start() => Apply();

        public void Apply()
        {
            if (seed == 0) return;
            var rng = new System.Random(seed);
            var mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string n = mats[i].name;
                    Color c;
                    if (n.EndsWith("_Top") || n.EndsWith("_Bottom")) c = Palette[rng.Next(Palette.Length)];
                    else if (n.EndsWith("_Sole") || n.EndsWith("_Socks")) c = Palette[rng.Next(Palette.Length)] * 0.6f;
                    else if (n.EndsWith("_Hair") || n.EndsWith("_HairCap"))
                        c = mats[i].GetColor("_BaseColor") * (0.75f + (float)rng.NextDouble() * 0.5f);
                    else continue;
                    r.GetPropertyBlock(mpb, i);
                    mpb.SetColor("_BaseColor", c);
                    r.SetPropertyBlock(mpb, i);
                }
            }
        }
    }
}
