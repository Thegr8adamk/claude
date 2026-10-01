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

        public static void Play(string name, Vector3 pos, float volume = 1f)
        {
            var c = Get(name);
            if (c != null) AudioSource.PlayClipAtPoint(c, pos, volume);
        }
    }
}
