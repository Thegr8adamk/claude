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

        /// <summary>One-shot 3D sound. Pitch varies +-8% each time (the same hit twice in a row sounds less canned).
        /// Returns the source (null when the clip is missing) so a caller can cut it short.</summary>
        public static AudioSource Play(string name, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            var c = Get(name);
            if (c == null) return null;
            // own source instead of PlayClipAtPoint: that one has no pitch
            var go = new GameObject("Sfx_" + name);
            go.transform.position = pos;
            var a = go.AddComponent<AudioSource>();
            a.clip = c;
            a.spatialBlend = 1f;
            a.volume = volume * AudioMix.Effects;
            a.pitch = pitch * Random.Range(0.92f, 1.08f);
            a.Play();
            Object.Destroy(go, c.length / Mathf.Max(0.1f, a.pitch) + 0.1f);
            return a;
        }
    }
}
