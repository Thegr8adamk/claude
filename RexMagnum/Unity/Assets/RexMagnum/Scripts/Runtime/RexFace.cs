using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Face blend shapes: random blinks, expressions (Smile, SmugGrin) and simple
    /// mouth movement while a voice line plays (Mouth_A/E/O/M).
    /// </summary>
    public class RexFace : MonoBehaviour
    {
        public SkinnedMeshRenderer face;
        public AudioSource voice;

        int blinkL = -1, blinkR = -1, smile = -1, smug = -1, mA = -1, mE = -1, mO = -1, mM = -1;
        float nextBlink, blinkT = -1f, exprUntil;
        int exprIndex = -1;
        float[] samples = new float[256];

        void Start()
        {
            if (face == null) return;
            var m = face.sharedMesh;
            blinkL = m.GetBlendShapeIndex("Blink_L");
            blinkR = m.GetBlendShapeIndex("Blink_R");
            smile = m.GetBlendShapeIndex("Smile");
            smug = m.GetBlendShapeIndex("SmugGrin");
            mA = m.GetBlendShapeIndex("Mouth_A");
            mE = m.GetBlendShapeIndex("Mouth_E");
            mO = m.GetBlendShapeIndex("Mouth_O");
            mM = m.GetBlendShapeIndex("Mouth_M");
            for (int i = 0; i < m.blendShapeCount; i++) face.SetBlendShapeWeight(i, 0f);
            nextBlink = Time.time + Random.Range(1f, 4f);
        }

        public void Expression(string name, float seconds)
        {
            exprIndex = name == "Smile" ? smile : name == "SmugGrin" ? smug : -1;
            exprUntil = Time.time + seconds;
        }

        void Set(int i, float w)
        {
            if (i >= 0) face.SetBlendShapeWeight(i, Mathf.Clamp(w, 0f, 100f));
        }

        void Update()
        {
            if (face == null) return;
            // Blink: 0.15 s close-open, every 2-6 s
            if (Time.time >= nextBlink && blinkT < 0f) blinkT = 0f;
            float b = 0f;
            if (blinkT >= 0f)
            {
                blinkT += Time.deltaTime / 0.15f;
                b = Mathf.Sin(Mathf.Clamp01(blinkT) * Mathf.PI) * 100f;
                if (blinkT >= 1f)
                {
                    blinkT = -1f;
                    nextBlink = Time.time + Random.Range(2f, 6f);
                }
            }
            Set(blinkL, b);
            Set(blinkR, b);

            // Expressions fade in/out
            float e = Time.time < exprUntil ? 100f : 0f;
            foreach (int i in new[] { smile, smug })
                if (i >= 0) Set(i, Mathf.MoveTowards(face.GetBlendShapeWeight(i), i == exprIndex ? e : 0f, Time.deltaTime * 400f));

            // Talk: mouth opens with the loudness of the voice line
            float level = 0f;
            if (voice != null && voice.isPlaying && Application.platform == RuntimePlatform.WebGLPlayer)
                level = 0.5f;          // web audio has no sample read-back (GetOutputData): a steady talk flap
            else if (voice != null && voice.isPlaying)
            {
                voice.GetOutputData(samples, 0);
                foreach (float s in samples) level += s * s;
                level = Mathf.Sqrt(level / samples.Length) * 8f;
            }
            float t = Time.time * 9f;
            Set(mA, level * 100f * Mathf.Abs(Mathf.Sin(t)));
            Set(mO, level * 60f * Mathf.Abs(Mathf.Sin(t * 0.7f + 1f)));
            Set(mE, level * 50f * Mathf.Abs(Mathf.Sin(t * 1.3f + 2f)));
        }
    }
}
