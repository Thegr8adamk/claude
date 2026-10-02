using System.Collections.Generic;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Sound effects from Resources/Sfx/&lt;name&gt; (the ElevenLabs takes, brief section 9). A missing file is
    /// silent, so the game runs before the audio is added. Names:
    /// blob_idle, blob_slide, blob_spit, blob_angry, blob_hit, blob_pop, towel_crack, bottle_break, knife_stab,
    /// golf_swing, swordfish, pistol, harpoon, sniper, coconut, chaos, door_bang, bottle_clink.
    /// </summary>
    public static class Sfx
    {
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static AudioClip Get(string name)
        {
            if (!cache.TryGetValue(name, out var c))
            {
                c = Resources.Load<AudioClip>("Sfx/" + name);
                cache[name] = c;
            }
            return c;
        }

        // a small pool of sources (a new GameObject per sound was garbage and Destroy calls in every fight):
        // a free one if there is one, else the one that started longest ago
        const int PoolSize = 24;
        static readonly AudioSource[] pool = new AudioSource[PoolSize];
        static readonly float[] started = new float[PoolSize];
        static Transform poolRoot;

        static AudioSource Source()
        {
            if (poolRoot == null)
            {
                var root = new GameObject("SfxPool");
                Object.DontDestroyOnLoad(root);
                poolRoot = root.transform;
                System.Array.Clear(pool, 0, PoolSize);
            }
            int best = 0;
            for (int i = 0; i < PoolSize; i++)
            {
                if (pool[i] == null)
                {
                    var go = new GameObject("Sfx");
                    go.transform.SetParent(poolRoot, false);
                    pool[i] = go.AddComponent<AudioSource>();
                    pool[i].playOnAwake = false;
                    pool[i].spatialBlend = 1f;
                    best = i;
                    break;
                }
                if (!pool[i].isPlaying) { best = i; break; }
                if (started[i] < started[best]) best = i;
            }
            started[best] = Time.unscaledTime;
            return pool[best];
        }

        /// <summary>One-shot 3D sound. Pitch varies +-8% each time (the same hit twice in a row sounds less canned).
        /// Returns the source (null when the clip is missing) so a caller can cut it short right away; it is pooled,
        /// so do not keep it for later (it may be playing another sound by then).</summary>
        public static AudioSource Play(string name, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            var c = Get(name);
            if (c == null) return null;
            // own source instead of PlayClipAtPoint: that one has no pitch
            var a = Source();
            a.Stop();
            a.transform.position = pos;
            a.clip = c;
            a.spatialBlend = 1f;
            a.volume = volume * AudioMix.Effects;
            a.pitch = pitch * Random.Range(0.92f, 1.08f);
            a.Play();
            return a;
        }
    }
}
